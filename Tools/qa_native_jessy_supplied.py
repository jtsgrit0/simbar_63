"""Inspect the supplied Jessy card in the real responsive owner-selection UI."""
from functools import partial
from http.server import ThreadingHTTPServer
import hashlib
import json
from pathlib import Path
import threading

from playwright.sync_api import sync_playwright
from benchmark_web_loading import chromium_executable
from qa_native_show_finale import HTML, SourceHandler

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / 'Research/PixelArtVerification/JessySupplied'
VIEW = ROOT / 'Library/JessySuppliedQA/index.html'
USER_SOURCE = Path('C:/Users/Jtsgr/OneDrive/Desktop/owner_p_j.PNG')
ARCHIVE = ROOT / 'ArtSource/Owners/Supplied/Jessy-owner_p_j.PNG'
BASELINES = {
    'Tools/WebGLTemplate/FastGame/art/owner-ff.png': 'be581e80e16730260d2468d17e3e3fb2897062e35eaec463fdcee72220e2a350',
    'Tools/WebGLTemplate/FastGame/art/owner-pet.png': 'f139dd68c81f915f20c1380eba95482a0524081393f965070b7f04358a57f417',
    'Tools/WebGLTemplate/FastGame/world.js': 'fa5dadb22bf2dd5ef3c5b1879b9117c8b0b9ad29790a4f0d328581d7bb24a4dd',
    'Tools/WebGLTemplate/FastGame/game.js': 'e86686530f10493b870c6085831d52904387d1b9c80908b4f53be330781fc5b9',
    'Tools/WebGLTemplate/FastGame/music.js': '62e168584358dde3b452c112b2ee2fe2f4cb53aef408e871accee942c656d199',
    'Tools/WebGLTemplate/FastGame/album-art.js': 'b19cffd29ae71fa5a700980bd6a658eb660775235777336f674551f1e5cc6139',
}
TAGLINES = ['자기야 오늘밤 라이브 하러와', '평생 Rock&Roll', '오늘밤은 무슨 음악을 들을까?']


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def main():
    from PIL import Image

    OUT.mkdir(parents=True, exist_ok=True)
    VIEW.parent.mkdir(parents=True, exist_ok=True)
    VIEW.write_text(HTML, encoding='utf-8', newline='\n')
    report = {'passed': False, 'checks': [], 'errors': [], 'httpErrors': [], 'screenshots': [],
              'userSource': str(USER_SOURCE), 'userSourceSha256': sha(USER_SOURCE),
              'archiveSha256': sha(ARCHIVE),
              'unchangedHashes': {path: sha(ROOT/path) for path in BASELINES},
              'runtimeSha256': sha(ROOT/'Tools/WebGLTemplate/FastGame/art/owner-female.png')}
    for path, expected in BASELINES.items():
        assert report['unchangedHashes'][path] == expected, path+' unexpectedly changed'
    assert report['archiveSha256'] == report['userSourceSha256']
    supplied = Image.open(ARCHIVE).convert('RGBA')
    packed = Image.open(ROOT/'Tools/WebGLTemplate/FastGame/art/owner-female.png').convert('RGBA')
    assert supplied.size == (1087, 1447) and packed.size == (400, 350)
    assert packed.crop((68, 0, 331, 350)).tobytes() == supplied.resize((263, 350), Image.Resampling.NEAREST).tobytes()
    report['fullSourceContain'] = {'sourceSize': list(supplied.size), 'runtimeSize': list(packed.size),
                                 'sourcePlacement': [68, 0, 263, 350], 'pixelsExact': True}
    report['checks'].append('The complete supplied image is archived unchanged and packed pixel-exactly with nearest-neighbor contain.')
    server = ThreadingHTTPServer(('127.0.0.1', 0), partial(SourceHandler, directory=ROOT))
    threading.Thread(target=server.serve_forever, daemon=True).start()
    try:
        with sync_playwright() as pw:
            executable = chromium_executable(None)
            browser = pw.chromium.launch(**({'executable_path': executable} if executable else {}))
            context = browser.new_context(viewport={'width': 1280, 'height': 800}, device_scale_factor=2, locale='ko-KR', has_touch=True)
            page = context.new_page()
            page.on('pageerror', lambda error: report['errors'].append(str(error)))
            page.on('response', lambda response: report['httpErrors'].append({'url': response.url, 'status': response.status}) if response.status >= 400 else None)
            album_requests = []
            page.on('request', lambda request: album_requests.append(request.url) if '/album-covers' in request.url else None)

            def ready():
                page.wait_for_function('window.simbarGameReadyAt>0 && document.querySelectorAll(".owner-card").length===3', timeout=60000)

            def shot(label):
                page.screenshot(path=str(OUT/(label+'.png')))
                report['screenshots'].append(label+'.png')

            def owners():
                return page.evaluate('''() => [...document.querySelectorAll('.owner-card')].map(card=>{
                 const img=card.querySelector('img'),s=getComputedStyle(img),r=img.getBoundingClientRect(),b=card.getBoundingClientRect();
                 return{name:card.querySelector('.owner-name').textContent,tagline:card.querySelector('.owner-description').textContent,
                  src:img.src,naturalWidth:img.naturalWidth,naturalHeight:img.naturalHeight,rendering:s.imageRendering,
                  objectFit:s.objectFit,imageBox:{width:r.width,height:r.height},card:{x:b.x,y:b.y,right:b.right,bottom:b.bottom}};
                })''')

            page.goto(f'http://127.0.0.1:{server.server_port}/Library/JessySuppliedQA/index.html');ready()
            first = owners()
            assert [o['tagline'] for o in first] == TAGLINES
            assert [o['name'] for o in first] == ['제시', '에디슨', '제이든']
            assert first[0]['src'].endswith('/owner-female.png')
            for owner in first:
                assert owner['naturalWidth'] > 0 and owner['naturalHeight'] > 0
                assert owner['rendering'] in ['pixelated', 'crisp-edges']
                assert abs(owner['imageBox']['width']/owner['imageBox']['height']-owner['naturalWidth']/owner['naturalHeight']) < .001
            report['desktop'] = first
            shot('01-desktop-owner-selection')
            page.locator('.owner-card').nth(0).screenshot(path=str(OUT/'02-jessy-card-detail.png'))
            report['screenshots'].append('02-jessy-card-detail.png')
            for index in range(3):
                page.locator('.owner-card').nth(index).click();page.wait_for_timeout(60)
                assert page.evaluate('simbarGame.owner') == index
                assert page.locator('.owner-card.selected').get_attribute('data-payload') == '{"index":'+str(index)+'}'
                if index == 0:
                    assert page.locator('.owner-card').nth(0).evaluate('''card=>{const img=card.querySelector('img').getBoundingClientRect(),badge=card.querySelector('.badge').getBoundingClientRect();return badge.right<=img.x+img.width*68/400;}''')
                else:
                    assert page.locator('.owner-card').nth(0).locator('.badge').evaluate('el=>getComputedStyle(el).display') == 'none'
            report['checks'].append('All three desktop owner selections work; names and requested taglines remain exact.')
            report['checks'].append('Edison/Jaiden image bytes and album/light/music source hashes remain unchanged.')

            for label, size in [('mobile-portrait', {'width':390,'height':844}), ('mobile-landscape', {'width':844,'height':390})]:
                page.set_viewport_size(size);page.wait_for_timeout(120)
                cards = owners()
                assert [o['tagline'] for o in cards] == TAGLINES
                for card in cards:
                    assert card['card']['x'] >= -.5 and card['card']['y'] >= -.5
                    assert card['card']['right'] <= size['width']+.5 and card['card']['bottom'] <= size['height']+.5
                for index in range(3):
                    page.locator('.owner-card').nth(index).click();page.wait_for_timeout(60)
                    assert page.evaluate('simbarGame.owner') == index
                page.locator('.owner-card').nth(0).click();page.wait_for_timeout(60)
                report[label] = cards;shot('03-'+label+'-selection')
            report['checks'].append('390×844 portrait and844×390 landscape retain all three visible, working owner cards.')
            report['checks'].append('The displayed card image preserves its native aspect and nearest-neighbor CSS sampling.')
            page.reload();ready()
            assert page.evaluate('simbarGame.owner') == 0
            page.locator('button[data-action="start"]').click();page.wait_for_timeout(100)
            assert page.evaluate('simbarGame.phase') == 'planning'
            report['checks'].append('Jessy selection survives reload and starts the real planning flow.')
            assert not album_requests
            assert not report['errors'], report['errors']
            assert not report['httpErrors'], report['httpErrors']
            report['checks'].append('Owner selection triggers no album requests and no browser/HTTP errors.')
            report['passed'] = True
            context.close();browser.close()
    except Exception as error:
        report['failure'] = str(error)
        raise
    finally:
        server.shutdown();server.server_close()
        (OUT/'browser-qa.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf-8', newline='\n')
    print(json.dumps({'passed': report['passed'], 'checks': report['checks'], 'errors': report['errors']}))


if __name__ == '__main__':
    main()
