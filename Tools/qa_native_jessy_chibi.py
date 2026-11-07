"""Verify Jessy's versioned full-hair art chain and real selector/desk presentation."""
import argparse
from functools import partial
import hashlib
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
import json
from pathlib import Path
import re
import subprocess
import threading

from PIL import Image, ImageOps
from playwright.sync_api import sync_playwright
from benchmark_web_loading import chromium_executable
import qa_native_day_desk

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / 'Research/PixelArtVerification/JessyChibi/browserQA'
BASELINE = 'daff6d005a4b23a45401738d9fdd9a911b0f990c'
ART = ROOT / 'Tools/WebGLTemplate/FastGame/art'
SELECTOR_SHA = 'b2471d98bb0de20aaa297533caed9cc9d0d8a23ae9a486667c19feab0501113f'
OFFICE_SHA = '0083d70441456b0a2544433eca2271efc6abe6beabe6029bf74c9ad5667750df'
NAMES = ['제시', '에디슨', '제이든']
TAGLINES = ['자기야 오늘밤 라이브 하러와', '평생 Rock&Roll', '오늘밤은 무슨 음악을 들을까?']


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def old_bytes(relative):
    return subprocess.check_output(['git', 'show', BASELINE + ':' + relative], cwd=ROOT)


class QuietHandler(SimpleHTTPRequestHandler):
    def log_message(self, format, *args):
        pass


class QuietServer(ThreadingHTTPServer):
    request_queue_size = 128


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--package', type=Path, default=ROOT / 'Library/JessyChibiPackage')
    parser.add_argument('--expected-release', default='ef3109e361da0274')
    args = parser.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    report = {'passed': False, 'errors': [], 'httpErrors': [], 'failedRequests': [],
              'checks': [], 'screenshots': [], 'selectors': {}}
    server = thread = None
    try:
        selector_meta = json.loads((ROOT / 'ArtSource/Owners/ChibiJessy/candidate-manifest.json').read_text(encoding='utf-8'))
        office_meta = json.loads((ROOT / 'ArtSource/DayDesk/JessyChibi/candidate-manifest.json').read_text(encoding='utf-8'))
        config = json.loads((ROOT / 'ArtSource/Owners/Refinements/presentation.json').read_text(encoding='utf-8'))['owners']['JESSY']
        raw = ROOT / selector_meta['source']['path']
        canonical = ROOT / config['cardSource']
        assert sha(raw) == selector_meta['source']['sha256']
        source = Image.open(raw).convert('RGB')
        assert source.size == (1341, 1173)
        fitted = ImageOps.fit(source, (400, 350), Image.Resampling.NEAREST, centering=(.5, .5))
        card = Image.open(canonical)
        runtime = Image.open(ART / 'owner-female.png')
        assert card.mode == 'RGBA' and card.size == runtime.size == (400, 350)
        assert card.getchannel('A').getextrema() == (255, 255)
        assert fitted.tobytes() == card.convert('RGB').tobytes() == runtime.convert('RGB').tobytes()
        assert card.convert('RGBA').tobytes() == runtime.convert('RGBA').tobytes()
        assert sha(canonical) == sha(ART / 'owner-female.png') == config['cardSourceSha256'] == SELECTOR_SHA
        assert config['cardSourceRect'] == [0, 0, 400, 350] and config['cardFit'] == 'cover'
        assert not config['sourceIncludesBadge']
        box = selector_meta['technicalExport']['sourceBoxExact']
        assert box[1] == 0 and box[3] == source.height and abs(box[0] - 3 / 14) < .00001
        assert selector_meta['reviewNotes']['highestDarkCurlY'] == 7
        report['selectorArt'] = {'raw': selector_meta['source'], 'canonical': str(canonical.relative_to(ROOT)),
                                 'runtimeSha256': SELECTOR_SHA, 'rawFitCanonicalRuntimePixelsExact': True,
                                 'fullCanonicalRuntimeRGBAExact': True, 'sourceBoxExact': box,
                                 'verticalCropPixels': 0, 'rootReviewedTopCurlClearancePx': 7}
        office_raw = ROOT / office_meta['source']['path']
        assert sha(office_raw) == office_meta['source']['sha256']
        office_source = Image.open(office_raw).convert('RGB')
        office = Image.open(ART / 'office/desk-jessy.png').convert('RGB')
        assert office_source.size == (1672, 941) and office.size == (640, 360)
        assert office_source.resize((640, 360), Image.Resampling.NEAREST).tobytes() == office.tobytes()
        assert sha(ART / 'office/desk-jessy.png') == OFFICE_SHA
        office_manifest = json.loads((ROOT / 'ArtSource/DayDesk/manifest.json').read_text(encoding='utf-8'))
        old_manifest = json.loads(old_bytes('ArtSource/DayDesk/manifest.json'))
        assert office_manifest['geometry'] == old_manifest['geometry']
        assert office_manifest['assets'][1:] == old_manifest['assets'][1:]
        assert office_meta['reviewNotes']['monitorEdgeDeltaPx'] == 0
        assert office_meta['reviewNotes']['phoneEdgeMaximumDeltaPx'] <= 1
        report['officeArt'] = {'raw': office_meta['source'], 'runtimeSha256': OFFICE_SHA,
                               'wholeSourceNearestResizeExact': True, 'sharedGeometryUnchanged': True,
                               'reviewedPropEdgeDeltaPx': {'monitor': 0, 'phoneMaximum': 1}}
        html = (args.package / 'index.html').read_text(encoding='utf-8')
        release = re.search(r'FastGame/([a-f0-9]{16})/boot\.js', html).group(1)
        assert release == args.expected_release
        folder = args.package / 'FastGame' / release
        manifest = json.loads((folder / 'runtime-manifest.json').read_text(encoding='utf-8'))
        report['protectedHashes'] = {}
        for entry in manifest['files']:
            relative = 'Tools/WebGLTemplate/FastGame/' + entry['path']
            file = ROOT / relative
            assert (folder / entry['path']).read_bytes() == file.read_bytes()
            assert sha(file) == entry['sha256'] and file.stat().st_size == entry['bytes']
            if entry['path'] not in ['art/owner-female.png', 'art/office/desk-jessy.png']:
                assert file.read_bytes() == old_bytes(relative), relative + ' changed beyond Jessy art'
                report['protectedHashes'][relative] = sha(file)
        for relative in ['ArtSource/Owners/Supplied/Jessy-owner_p_j.PNG', 'ArtSource/Owners/Original-owner_p.png',
                         'ArtSource/DayDesk/desk-jessy.source.png']:
            assert (ROOT / relative).read_bytes() == old_bytes(relative)
            report['protectedHashes'][relative] = sha(ROOT / relative)
        report['package'] = {'release': release, 'files': len(manifest['files']),
                             'bytes': sum(entry['bytes'] for entry in manifest['files'])}
        report['checks'].append('Generated RAWs retained unchanged; selector ImageOps.fit to canonical RGBA to runtime is pixel-exact with no vertical crop, and the whole office RAW nearest resize is exact.')
        report['checks'].append('Only Jessy selector and office runtime PNGs changed; all other packaged images, logic, CSS, fonts, album art and original source archives match the previous release.')
        server = QuietServer(('127.0.0.1', 0), partial(QuietHandler, directory=str(args.package.resolve())))
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        with sync_playwright() as pw:
            executable = chromium_executable(None)
            browser = pw.chromium.launch(**({'executable_path': executable} if executable else {}))
            try:
                for name, viewport in [('desktop', {'width': 1280, 'height': 800}), ('mobile', {'width': 390, 'height': 844})]:
                    context = browser.new_context(viewport=viewport, device_scale_factor=2, is_mobile=name == 'mobile', has_touch=True, locale='ko-KR')
                    try:
                        page = context.new_page()
                        page.on('pageerror', lambda error: report['errors'].append(str(error)))
                        page.on('response', lambda response: report['httpErrors'].append({'url': response.url, 'status': response.status}) if response.status >= 400 else None)
                        page.on('requestfailed', lambda request: report['failedRequests'].append({'url': request.url, 'failure': request.failure}))
                        page.goto(f'http://127.0.0.1:{server.server_port}/')
                        page.wait_for_function('window.simbarGameReadyAt>0 && document.querySelectorAll(".owner-card").length===3')
                        states = []
                        for owner in range(3):
                            page.locator('.owner-card').nth(owner).click()
                            page.wait_for_function('i=>simbarGame.owner===i && document.querySelectorAll(".owner-card")[i].classList.contains("selected")', arg=owner)
                            page.evaluate('() => new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)))')
                            cards = page.evaluate('''() => [...document.querySelectorAll('.owner-card')].map(card=>{
                              const image=card.querySelector('img'),badge=card.querySelector('.badge'),r=card.getBoundingClientRect(),ir=image.getBoundingClientRect();
                              return {name:card.querySelector('.owner-name').textContent,tagline:card.querySelector('.owner-description').textContent,
                                selected:card.classList.contains('selected'),badge:badge.textContent,badgeVisible:getComputedStyle(badge).display!=='none',
                                src:image.src,naturalSize:[image.naturalWidth,image.naturalHeight],rendering:getComputedStyle(image).imageRendering,
                                displayAspect:ir.width/ir.height,bounds:r.toJSON()};
                            })''')
                            assert [card['name'] for card in cards] == NAMES
                            assert [card['tagline'] for card in cards] == TAGLINES
                            for i, card in enumerate(cards):
                                assert card['selected'] == (i == owner)
                                assert card['badgeVisible'] and card['badge'] == ('선택됨' if i == owner else f'0{i+1}')
                                assert card['naturalSize'] == [400, 350] and abs(card['displayAspect'] - 8 / 7) < .001
                                assert card['rendering'] in ['pixelated', 'crisp-edges']
                                r = card['bounds']
                                assert r['x'] >= -.5 and r['y'] >= -.5 and r['right'] <= viewport['width'] + .5 and r['bottom'] <= viewport['height'] + .5
                            filename = f'{name}-selector-owner-{owner}.png'
                            page.screenshot(path=str(OUT / filename))
                            report['screenshots'].append(filename)
                            if owner == 0:
                                filename = f'{name}-jessy-card-detail.png'
                                page.locator('.owner-card').nth(0).screenshot(path=str(OUT / filename))
                                report['screenshots'].append(filename)
                            states.append({'selectedOwner': owner, 'cards': cards})
                        report['selectors'][name] = states
                    finally:
                        context.close()
            finally:
                browser.close()
        server.shutdown(); server.server_close(); thread.join()
        server = thread = None
        assert not report['errors'] and not report['httpErrors'] and not report['failedRequests']
        desk_out = OUT / 'daydesk'
        desk_report = qa_native_day_desk.main(['--package', str(args.package), '--expected-release', release, '--output-dir', str(desk_out)])
        for case in desk_report['cases']:
            props = case['screenProps']
            for key, expected in [('monitor', [30, 176, 170, 99]), ('phone', [493, 293, 62, 33])]:
                assert all(abs(actual - target) <= 1.25 for actual, target in zip(props[key], expected)), (case['label'], key, props[key])
        report['dayDesk'] = {'report': 'daydesk/browser-qa.json', 'passed': desk_report['passed'],
                             'cases': len(desk_report['cases']), 'actualLaunches': 6,
                             'computedPhysicalScreenGeometryMatches': True,
                             'naturalNightEnd': desk_report['cases'][-1]['naturalNightEnd']}
        report['checks'].append('All three selector badges/states, names and taglines work on desktop and mobile; full images use pixelated sampling with unchanged aspect.')
        report['checks'].append('All six selected-owner office/fallback and Freewill-only desk workflows pass; computed monitor/phone overlays retain shared geometry, keyboard/save/launch and complete finale/cleanup pass.')
        report['passed'] = True
    except Exception as error:
        report['failure'] = str(error)
        raise
    finally:
        if server:
            server.shutdown(); server.server_close()
        if thread:
            thread.join()
        (OUT / 'browser-qa.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print(json.dumps({'passed': report['passed'], 'release': report['package']['release'],
                      'selectorViewports': len(report['selectors']), 'deskCases': report['dayDesk']['cases'],
                      'errors': report['errors']}, ensure_ascii=False))


if __name__ == '__main__':
    main()
