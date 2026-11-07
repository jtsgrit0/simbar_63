"""Exercise chicken through real order RNG, DOM menu/keys, Canvas and saves."""
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
OUT = ROOT / 'Research/PixelArtVerification/ChickenMenu'
VIEW = ROOT / 'Library/ChickenMenuQA/index.html'
STATE = '''() => ({phase:simbarGame.phase,paused:simbarGame.paused,selected:simbarGame.selectedItem,
 pour:simbarGame.pour,orders:simbarGame.orders.map(o=>({id:o.id,item:o.item})),
 draws:window.qaIconDraws||[],served:simbarGame.journey.orders,staff:simbarGame.state.staff})'''
FIXTURE = '''({seed,chicken}) => {
 const Game=simbarGame.constructor,mem=new Map(),storage={getItem:k=>mem.get(k)||null,setItem:(k,v)=>mem.set(k,String(v)),removeItem:k=>mem.delete(k)};
 const g=new Game({seed,storage});g.act('start');g.act('launch');while(g.phase==='guide')g.act('guide');
 const trace=[];if(chicken){for(let n=0;n<200;n++){trace.push(g.orders[0].item);if(g.orders[0].item===5)break;g._serve(g.orders[0].item,true,.7);}if(g.orders[0].item!==5)throw new Error('No random chicken order');}
 const orders=g.orders.map(o=>({id:o.id,item:o.item}));simbarGame.importSave(g.exportSave());return{trace,orders};
}'''
OBSERVE = '''() => {
 const app=simbarApplication,ctx=app.canvas.getContext('2d'),draw=ctx.drawImage.bind(ctx),render=app.world.render.bind(app.world);
 window.qaIconDraws=[];
 ctx.drawImage=(...args)=>{if(args[0]?.src?.endsWith('/order-icons.png'))qaIconDraws.push({sourceX:args[1],sourceY:args[2],x:args[5],y:args[6]});return draw(...args);};
 app.world.render=(...args)=>{window.qaIconDraws=[];return render(...args);};
}'''


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    VIEW.parent.mkdir(parents=True, exist_ok=True)
    VIEW.write_text(HTML, encoding='utf-8', newline='\n')
    server = ThreadingHTTPServer(('127.0.0.1', 0), partial(SourceHandler, directory=ROOT))
    threading.Thread(target=server.serve_forever, daemon=True).start()
    report = {'passed': False, 'checks': [], 'errors': [], 'httpErrors': [], 'screenshots': [],
              'sourceSha256': {name: hashlib.sha256((ROOT / 'Tools/WebGLTemplate/FastGame' / name).read_bytes()).hexdigest()
                               for name in ['catalog.js', 'core.js', 'game.js', 'world.js']}}
    try:
        with sync_playwright() as pw:
            executable = chromium_executable(None)
            browser = pw.chromium.launch(**({'executable_path': executable} if executable else {}))
            context = browser.new_context(viewport={'width': 1280, 'height': 800}, locale='ko-KR', has_touch=True)
            page = context.new_page()
            page.on('pageerror', lambda error: report['errors'].append(str(error)))
            page.on('response', lambda response: report['httpErrors'].append({'url': response.url, 'status': response.status}) if response.status >= 400 else None)

            def ready():
                page.wait_for_function('window.simbarGameReadyAt>0', timeout=60000)

            def paint():
                page.evaluate('() => new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)))')

            def shot(label):
                page.screenshot(path=str(OUT / (label+'.png')))
                report['screenshots'].append(label+'.png')

            def click(selector):
                control = page.locator(selector)
                control.click()
                page.wait_for_timeout(100)

            def load_fixture(seed, chicken):
                fixture = page.evaluate(FIXTURE, {'seed': seed, 'chicken': chicken})
                page.reload();ready();click('button[data-action="continue"]')
                page.evaluate(OBSERVE);paint()
                return fixture

            def chicken_cells_match():
                paint();state = page.evaluate(STATE)
                assert sum(d['sourceX'] == 160 for d in state['draws']) == sum(o['item'] == 5 for o in state['orders']), state
                assert all(d['sourceY'] == 0 for d in state['draws'])
                return state

            page.goto(f'http://127.0.0.1:{server.server_port}/Library/ChickenMenuQA/index.html');ready()
            report['randomFixture'] = load_fixture(1, True)
            before = chicken_cells_match();chicken_id = before['orders'][0]['id']
            assert before['orders'][0]['item'] == 5
            report['pending'] = before
            shot('01-real-random-chicken-order')
            report['checks'].append('Normal replacement-order RNG produces chicken and Canvas draws atlas column5.')

            page.set_viewport_size({'width': 390, 'height': 844});paint()
            click('button[data-action="ui:service"]')
            assert page.locator('.service-item').count() == 6
            assert page.locator('.service-section').nth(1).locator('.service-item').count() == 4
            chicken = page.locator('.service-item').nth(5)
            chicken.scroll_into_view_if_needed();paint()
            assert '후라이드 치킨' in chicken.inner_text() and '18,000' in chicken.inner_text()
            assert chicken.evaluate('el=>{const b=el.getBoundingClientRect(),s=el.closest(".service-scroll").getBoundingClientRect();return b.top>=s.top-.5&&b.bottom<=s.bottom+.5;}')
            assert '1–6' in page.locator('.service-hint').inner_text()
            shot('02-mobile-sixth-menu-item')
            report['checks'].append('390×844 menu scroll exposes four food choices including chicken at ₩18,000; help shows1–6.')

            page.locator('.service-item').nth(0).click();page.wait_for_timeout(220)
            page.set_viewport_size({'width': 1280, 'height': 800})
            wrong = chicken_cells_match()
            assert wrong['pour'] is None and wrong['orders'][0] == {'id': chicken_id, 'item': 5}
            report['checks'].append('Selecting a wrong cocktail leaves the chicken order and its icon pending.')
            page.keyboard.press('6');page.wait_for_timeout(180)
            assert page.evaluate('simbarGame.pour?.item') == 5
            assert '플레이팅' in page.locator('.pour-head').inner_text()
            assert '후라이드 치킨' in page.locator('.pour-subtitle').inner_text()
            shot('03-key6-chicken-plating')
            report['checks'].append('Keyboard6 selects chicken and opens food plating with the correct name.')

            page.keyboard.press('p');page.wait_for_timeout(60)
            saved = page.evaluate(STATE);assert saved['pour']['item'] == 5 and saved['selected'] == 5
            page.reload();ready()
            restored = page.evaluate(STATE)
            assert restored['pour'] is None and restored['selected'] == 5
            assert restored['orders'][0] == {'id': chicken_id, 'item': 5}
            click('button[data-action="continue"]');page.evaluate(OBSERVE)
            click('button[data-action="ui:service"]')
            page.locator('.service-item').nth(5).click();page.wait_for_timeout(180)
            assert page.evaluate('simbarGame.pour?.item') == 5
            assert '플레이팅' in page.locator('.pour-head').inner_text()
            report['checks'].append('Reload keeps selected/pending chicken, safely resets transient prep, and menu click prepares it again.')
            page.keyboard.press('Space');page.wait_for_timeout(100)
            assert page.evaluate('simbarGame.lastResult.success')
            assert not any(o['id'] == chicken_id for o in page.evaluate(STATE)['orders'])
            page.set_viewport_size({'width': 1280, 'height': 800})
            served = chicken_cells_match();report['manualServed'] = served
            shot('04-manual-serving-clears-chicken')
            report['checks'].append('Actual manual serving removes that customer bubble on the next paint.')

            report['autoFixture'] = load_fixture(2, True)
            auto_id = page.evaluate('simbarGame.orders[0].id')
            baseline = page.evaluate('simbarGame.journey.orders')
            click('button[data-action="ui:staff"]');page.locator('button[data-action="hire"]').nth(0).click();page.wait_for_timeout(100)
            click('button[data-action="ui:close"]')
            assert page.evaluate('simbarGame.state.staff[0]') == 1
            page.evaluate('() => {for(let n=0;n<3;n++)simbarGame.tick(5);}')
            automatic = chicken_cells_match()
            assert automatic['served'] > baseline and not any(o['id'] == auto_id for o in automatic['orders'])
            report['automaticServed'] = automatic
            shot('05-real-staff-auto-serves-chicken')
            report['checks'].append('Hired barback automatically serves the real chicken order and removes its bubble.')

            page.evaluate('() => {for(let n=0;n<40&&simbarGame.phase==="playing";n++)simbarGame.tick(5);}')
            paint();closing = page.evaluate(STATE)
            assert closing['phase'] == 'closing' and not closing['orders'] and not closing['draws']
            report['checks'].append('Natural closing clears every pending chicken order and bubble.')

            old = load_fixture(3, False)
            legacy = page.evaluate(STATE)
            assert all(o['item'] < 5 for o in old['orders']) and legacy['orders'] == old['orders']
            report['checks'].append('An unchanged five-item-era native pending-order save still loads correctly.')
            assert not report['errors'], report['errors']
            assert not report['httpErrors'], report['httpErrors']
            report['passed'] = True
            context.close();browser.close()
    except Exception as error:
        report['failure'] = str(error)
        raise
    finally:
        server.shutdown();server.server_close()
        (OUT / 'browser-qa.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf-8', newline='\n')
    print(json.dumps({'passed': report['passed'], 'checks': report['checks'], 'errors': report['errors']}))


if __name__ == '__main__':
    main()
