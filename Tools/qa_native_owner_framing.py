"""Verify the reviewed Jessy close-up crop and the real three-owner selector.

Run with Python, Pillow and Playwright after preparing Library/OwnerFramingPreview.
Historical JessySupplied QA and evidence remain unchanged.
"""
import argparse
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
import hashlib
import json
from pathlib import Path
import re
import subprocess
import threading

from PIL import Image
from playwright.sync_api import sync_playwright
from benchmark_web_loading import chromium_executable

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / 'Research/PixelArtVerification/OwnerFraming'
USER_SOURCE = Path('C:/Users/Jtsgr/OneDrive/Desktop/owner_p_j.PNG')
ARCHIVE = ROOT / 'ArtSource/Owners/Supplied/Jessy-owner_p_j.PNG'
SOURCE_SHA = 'a27e380f6743eb2cf3359e2e593cff12f63f5dbc371de112d6e0196924775961'
RUNTIME_SHA = '8d85760c18009360078d98158b03d2eae5cb423dde3cdb8cea13fb1105de3fa0'
SOURCE_RECT = (3, 280, 1083, 1225)
BASELINE_REV = '261d7f3bab1ad5d1a7ba6b0f24ccb47e406dfc4d'
PROTECTED = [
    'art/owner-ff.png', 'art/owner-pet.png', 'art/album-covers.json',
    'art/album-covers/freewill-electric-shocker.png',
    'art/album-covers/julywantsmore-hey.png',
    'art/album-covers/julywantsmore-no-matter-how.png',
    'art/venue.png', 'art/seoul.png', 'world.js', 'core.js', 'floor.js',
    'game.js', 'music.js', 'album-art.js',
]
NAMES = ['제시', '에디슨', '제이든']
TAGLINES = ['자기야 오늘밤 라이브 하러와', '평생 Rock&Roll', '오늘밤은 무슨 음악을 들을까?']


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


class QuietHandler(SimpleHTTPRequestHandler):
    def log_message(self, format, *args):
        pass


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--package', type=Path, default=ROOT / 'Library/OwnerFramingPreview')
    parser.add_argument('--expected-release', default='e2d68371bbaa0b3b')
    args = parser.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    report = {'passed': False, 'checks': [], 'errors': [], 'httpErrors': [],
              'failedRequests': [], 'screenshots': [], 'viewports': {}}
    server = thread = None
    try:
        report.update(userSource=str(USER_SOURCE), userSourceSha256=sha(USER_SOURCE),
                      archiveSha256=sha(ARCHIVE), sourceRect=list(SOURCE_RECT))
        assert USER_SOURCE.read_bytes() == ARCHIVE.read_bytes(), 'Retained supplied source differs'
        assert report['archiveSha256'] == SOURCE_SHA, 'Supplied source hash changed'
        supplied = Image.open(ARCHIVE).convert('RGBA')
        runtime_path = ROOT / 'Tools/WebGLTemplate/FastGame/art/owner-female.png'
        packed = Image.open(runtime_path).convert('RGBA')
        assert supplied.size == (1087, 1447) and packed.size == (400, 350)
        expected = supplied.crop(SOURCE_RECT).resize((400, 350), Image.Resampling.NEAREST)
        assert expected.tobytes() == packed.tobytes(), 'Runtime differs from complete nearest-neighbor crop'
        assert packed.getchannel('A').getextrema() == (255, 255)
        edge_colors = [len(set(packed.crop(box).get_flattened_data()))
                       for box in [(0, 0, 1, 350), (399, 0, 400, 350)]]
        assert min(edge_colors) > 1, 'Opaque letterbox edge found'
        assert sha(runtime_path) == RUNTIME_SHA
        report['crop'] = {'sourceSize': list(supplied.size), 'viewportSize': [1080, 945],
                          'runtimeSize': list(packed.size), 'pixelsExact': True,
                          'aspectPreserved': 1080 * 350 == 945 * 400,
                          'opaqueFullFrame': True, 'edgeColorCounts': edge_colors,
                          'runtimeSha256': sha(runtime_path), 'embedded01OutsideCrop': True}
        config = json.loads((ROOT / 'ArtSource/Owners/Refinements/presentation.json').read_text(encoding='utf-8'))['owners']['JESSY']
        assert tuple(config['cardSourceRect']) == SOURCE_RECT and config['cardFit'] == 'cover'
        assert config['cardResampling'] == 'nearest-neighbor' and not config['sourceIncludesBadge']
        report['unchangedHashes'] = {}
        for relative in PROTECTED:
            path = 'Tools/WebGLTemplate/FastGame/' + relative
            baseline = subprocess.check_output(['git', 'show', BASELINE_REV + ':' + path], cwd=ROOT)
            assert (ROOT / path).read_bytes() == baseline, path + ' unexpectedly changed'
            report['unchangedHashes'][path] = sha(ROOT / path)
        report['checks'].append('Source is archived unchanged; every runtime RGBA pixel matches the reviewed 1080x945 source crop resized to 400x350 with nearest-neighbor sampling, without letterboxing.')
        report['checks'].append('Edison, Jaiden, authentic album covers, venue art, gameplay, stage-light and music code remain byte-identical to the prior release.')

        html = (args.package / 'index.html').read_text(encoding='utf-8')
        release = re.search(r'FastGame/([a-f0-9]{16})/boot\.js', html).group(1)
        assert release == args.expected_release, 'Wrong preview release'
        folder = args.package / 'FastGame' / release
        manifest = json.loads((folder / 'runtime-manifest.json').read_text(encoding='utf-8'))
        for entry in manifest['files']:
            file = folder / entry['path']
            assert file.stat().st_size == entry['bytes'] and sha(file) == entry['sha256'], entry['path']
            assert file.read_bytes() == (ROOT / 'Tools/WebGLTemplate/FastGame' / entry['path']).read_bytes()
        report['package'] = {'release': release, 'filesVerified': len(manifest['files']),
                             'bytes': sum(entry['bytes'] for entry in manifest['files'])}
        server = ThreadingHTTPServer(('127.0.0.1', 0), partial(QuietHandler, directory=str(args.package.resolve())))
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        with sync_playwright() as pw:
            executable = chromium_executable(None)
            browser = pw.chromium.launch(**({'executable_path': executable} if executable else {}))
            try:
                for viewport_name, viewport in [('desktop', {'width': 1280, 'height': 800}),
                                                ('mobile', {'width': 390, 'height': 844})]:
                    context = browser.new_context(viewport=viewport, device_scale_factor=2,
                                                  locale='ko-KR', has_touch=True,
                                                  is_mobile=viewport_name == 'mobile')
                    try:
                        page = context.new_page()
                        page.on('pageerror', lambda error: report['errors'].append(str(error)))
                        page.on('response', lambda response: report['httpErrors'].append({'url': response.url, 'status': response.status}) if response.status >= 400 else None)
                        page.on('requestfailed', lambda request: report['failedRequests'].append({'url': request.url, 'failure': request.failure}))
                        optional = []
                        page.on('request', lambda request: optional.append(request.url) if re.search(r'/art/album-covers(?:/|\.json)|/StreamingAssets/Music/', request.url) else None)
                        page.goto(f'http://127.0.0.1:{server.server_port}/')
                        page.wait_for_function('window.simbarGameReadyAt>0 && document.querySelectorAll(".owner-card").length===3')
                        page.wait_for_function('document.fonts.status === "loaded"')
                        assert page.locator('#game-shell').get_attribute('data-player-state') == 'ready'
                        states = []
                        for index in range(3):
                            page.locator('.owner-card').nth(index).click()
                            page.wait_for_function('(i)=>simbarGame.owner===i && document.querySelectorAll(".owner-card")[i].classList.contains("selected")', arg=index)
                            cards = page.evaluate('''() => [...document.querySelectorAll('.owner-card')].map(card=>{
                              const img=card.querySelector('img'),style=getComputedStyle(img),box=card.getBoundingClientRect();
                              const badge=card.querySelector('.badge'),bs=getComputedStyle(badge),br=badge.getBoundingClientRect();
                              const texts=['.owner-name','.owner-description'].map(selector=>{
                                const el=card.querySelector(selector),r=el.getBoundingClientRect(),range=document.createRange();range.selectNodeContents(el);const t=range.getBoundingClientRect();
                                // Glyph ascent may exceed its line box; the card is the actual clipping ancestor.
                                return {text:el.textContent,fits:t.left>=r.left-1&&t.right<=r.right+1&&t.top>=box.top-1&&t.bottom<=box.bottom+1};
                              });
                              return {name:texts[0].text,tagline:texts[1].text,textsFit:texts.every(t=>t.fits),
                                selected:card.classList.contains('selected'),naturalSize:[img.naturalWidth,img.naturalHeight],
                                rendering:style.imageRendering,objectFit:style.objectFit,src:img.src,
                                badge:{text:badge.textContent,visible:bs.display!=='none'&&bs.visibility!=='hidden'&&br.width>0&&br.height>0,
                                  contained:br.left>=box.left-1&&br.right<=box.right+1&&br.top>=box.top-1&&br.bottom<=box.bottom+1},
                                card:{x:box.x,y:box.y,right:box.right,bottom:box.bottom}};
                            })''')
                            assert [card['name'] for card in cards] == NAMES
                            assert [card['tagline'] for card in cards] == TAGLINES
                            for number, card in enumerate(cards):
                                assert card['selected'] == (number == index)
                                assert card['naturalSize'] == [400, 350]
                                assert card['rendering'] in ['pixelated', 'crisp-edges']
                                assert card['textsFit'], 'Owner name/tagline clipped'
                                assert card['badge']['visible'] and card['badge']['contained']
                                assert card['badge']['text'] == ('선택됨' if number == index else f'0{number+1}')
                                assert card['card']['x'] >= -.5 and card['card']['y'] >= -.5
                                assert card['card']['right'] <= viewport['width'] + .5 and card['card']['bottom'] <= viewport['height'] + .5
                            screenshot = f'{viewport_name}-owner-{index}-selected.png'
                            page.screenshot(path=str(OUT / screenshot))
                            report['screenshots'].append(screenshot)
                            if index == 0 and viewport_name == 'desktop':
                                page.locator('.owner-card').nth(0).screenshot(path=str(OUT / 'jessy-card-detail.png'))
                                report['screenshots'].append('jessy-card-detail.png')
                            states.append({'selectedIndex': index, 'cards': cards, 'screenshot': screenshot})
                        page.locator('.owner-card').nth(0).click()
                        page.wait_for_function('simbarGame.owner===0')
                        page.reload()
                        page.wait_for_function('window.simbarGameReadyAt>0 && document.querySelectorAll(".owner-card").length===3')
                        assert page.evaluate('simbarGame.owner') == 0
                        assert not optional, optional
                        page.locator('button[data-action="start"]').click()
                        page.wait_for_function('simbarGame.phase === "planning"')
                        proof = {'clicked': 'start', 'phase': page.evaluate('simbarGame.phase'), 'owner': page.evaluate('simbarGame.owner')}
                        screenshot = f'{viewport_name}-actual-start-planning.png'
                        page.screenshot(path=str(OUT / screenshot))
                        report['screenshots'].append(screenshot)
                        report['viewports'][viewport_name] = {'viewport': viewport, 'states': states,
                                                             'selectionReloadPreserved': True,
                                                             'interactionProof': proof,
                                                             'optionalRequestsBeforeStart': optional}
                    finally:
                        context.close()
                assert not report['errors'], report['errors']
                assert not report['httpErrors'], report['httpErrors']
                assert not report['failedRequests'], report['failedRequests']
                report['checks'].extend([
                    'All three owners select correctly at 1280x800 and 390x844; names, exact taglines and all 01/02/03/selected badge states are visible and contained.',
                    'Every owner image uses pixelated nearest-neighbor CSS sampling; Jessy has one ordinary UI badge because the embedded badge is outside the crop.',
                    'Jessy selection survives reload and the actual Start control reaches planning on both viewports.',
                    'No JavaScript, HTTP or failed-request errors; no optional album or music requests during owner selection.',
                ])
                report['passed'] = True
            finally:
                browser.close()
    except Exception as error:
        report['failure'] = str(error)
        raise
    finally:
        if server:
            server.shutdown()
            server.server_close()
        if thread:
            thread.join()
        (OUT / 'browser-qa.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print(json.dumps({'passed': report['passed'], 'checks': report['checks'], 'errors': report['errors'], 'release': report['package']['release']}, ensure_ascii=False))


if __name__ == '__main__':
    main()
