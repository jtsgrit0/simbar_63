"""Exercise the real packaged owner desk, deferred art, keyboard and night flow."""
import argparse
from functools import partial
import hashlib
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
import json
from pathlib import Path
import re
import threading

from PIL import Image
from playwright.sync_api import sync_playwright
from benchmark_web_loading import chromium_executable

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / 'Research/PixelArtVerification/DayDesk/browserQA'
OFFICES = ['desk-jessy.png', 'desk-edison.png', 'desk-jaiden.png']
PORTRAITS = ['owner-female.png', 'owner-ff.png', 'owner-pet.png']
OWNER_NAMES = ['제시', '에디슨', '제이든']
OPTIONAL = re.compile(r'/art/(?:office/|album-covers(?:/|\.json))|/StreamingAssets/Music/', re.I)
LEGACY_UI = '.planning-ref,.planning-scroll,.poster-card,.poster-grid,.live-briefing-ref,[data-action="scout"],[data-action="listen"],[data-action="offer"],[data-action="promote"],[data-action="ticket"],[data-action="venue"]'
STATE = '''() => ({owner:simbarGame.owner,phase:simbarGame.phase,cash:simbarGame.cash,
 selected:simbarGame.journey.booking?.freewillSelected,done:simbarGame.journey.booking?.deskDoneMask,
 hour:simbarGame.metrics().hour,lineup:simbarGame.metrics().lineup.map(b=>b.id),
 focus:document.querySelector('.daydesk')?.dataset.focus,
 launchDisabled:document.querySelector('.desk-launch')?.disabled})'''


class QuietHandler(SimpleHTTPRequestHandler):
    def log_message(self, format, *args):
        pass


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--package', type=Path, default=ROOT / 'Library/DayDeskPreview')
    parser.add_argument('--expected-release', default='46fcd17b9c72c682')
    args = parser.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    report = {'passed': False, 'checks': [], 'issues': [], 'errors': [], 'httpErrors': [],
              'failedRequests': [], 'screenshots': [], 'cases': []}
    server = thread = None
    try:
        release = re.search(r'FastGame/([a-f0-9]{16})/boot\.js', (args.package / 'index.html').read_text(encoding='utf-8')).group(1)
        assert release == args.expected_release, 'Wrong packaged release'
        folder = args.package / 'FastGame' / release
        manifest = json.loads((folder / 'runtime-manifest.json').read_text(encoding='utf-8'))
        for entry in manifest['files']:
            file = folder / entry['path']
            assert file.stat().st_size == entry['bytes'] and sha(file) == entry['sha256'], entry['path']
        report['package'] = {'release': release, 'files': len(manifest['files']),
                             'bytes': sum(entry['bytes'] for entry in manifest['files'])}
        report['officeAssets'] = {}
        for filename in OFFICES:
            path = folder / 'art/office' / filename
            assert Image.open(path).size == (640, 360)
            assert path.read_bytes() == (ROOT / 'Tools/WebGLTemplate/FastGame/art/office' / filename).read_bytes()
            report['officeAssets'][filename] = {'sha256': sha(path), 'size': [640, 360], 'bytes': path.stat().st_size}
        server = ThreadingHTTPServer(('127.0.0.1', 0), partial(QuietHandler, directory=str(args.package.resolve())))
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        with sync_playwright() as pw:
            executable = chromium_executable(None)
            browser = pw.chromium.launch(**({'executable_path': executable} if executable else {}))
            try:
                for viewport_name, viewport in [('desktop', {'width': 1280, 'height': 800}), ('mobile', {'width': 390, 'height': 844})]:
                    for owner in range(3):
                        label = f'{viewport_name}-owner-{owner}'
                        case = {'label': label, 'owner': owner, 'viewport': viewport, 'steps': [], 'screenshots': []}
                        report['cases'].append(case)
                        context = browser.new_context(viewport=viewport, device_scale_factor=2,
                                                      is_mobile=viewport_name == 'mobile', has_touch=True, locale='ko-KR')
                        monitor = {'active': True}
                        requests, pending = [], []
                        try:
                            page = context.new_page()
                            page.on('pageerror', lambda error: report['errors'].append({'case': label, 'error': str(error)}))
                            page.on('response', lambda response: report['httpErrors'].append({'case': label, 'url': response.url, 'status': response.status}) if monitor['active'] and response.status >= 400 else None)
                            page.on('requestfailed', lambda request: report['failedRequests'].append({'case': label, 'url': request.url, 'failure': request.failure}) if monitor['active'] else None)
                            cdp = context.new_cdp_session(page)
                            cdp.send('Network.enable')
                            cdp.on('Network.requestWillBeSent', lambda event: requests.append(event['request']['url']))

                            def shot(suffix, scene=False):
                                filename = label + '-' + suffix + '.png'
                                target = page.locator('.desk-stage') if scene else page
                                target.screenshot(path=str(OUT / filename))
                                report['screenshots'].append(filename)
                                case['screenshots'].append(filename)

                            def ready():
                                page.wait_for_function('window.simbarGameReadyAt>0 && document.querySelectorAll(".owner-card").length===3')
                                assert page.locator('#game-shell').get_attribute('data-player-state') == 'ready'
                                module = page.locator('script[type="module"][src$="/boot.js"]').get_attribute('src')
                                assert re.search(r'(?:^|/)FastGame/' + release + r'/boot\.js$', module)

                            def click(selector):
                                # State actions rebuild the DOM on the next animation frame.
                                page.evaluate('() => new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)))')
                                control = page.locator(selector)
                                control.scroll_into_view_if_needed()
                                assert control.is_visible() and control.is_enabled(), selector + ' unavailable'
                                hit = control.evaluate('el=>{const r=el.getBoundingClientRect();return el.contains(document.elementFromPoint(r.x+r.width/2,r.y+r.height/2));}')
                                assert hit, selector + ' covered'
                                control.click()
                                page.wait_for_timeout(55)

                            def focus(channel):
                                click(f'[data-desk-channel="{channel}"]')
                                page.wait_for_function('c=>document.querySelector(".daydesk")?.dataset.focus===c', arg=channel)
                                assert page.locator(f'[data-desk-pane="{channel}"]').is_visible()
                                assert page.locator(f'[data-desk-channel="{channel}"]').get_attribute('aria-pressed') == 'true'

                            def work(task):
                                return f'[data-action="deskWork"][data-payload=\'{{"task":{task}}}\']'

                            def bounds():
                                value = page.evaluate('''() => {const desk=document.querySelector('.daydesk'),r=desk.getBoundingClientRect(),s=desk.querySelector('.daydesk-scroll');return{desk:r.toJSON(),width:innerWidth,height:innerHeight,bodyWidth:document.documentElement.scrollWidth,scrollWidth:s.scrollWidth,clientWidth:s.clientWidth};}''')
                                r = value['desk']
                                assert r['x'] >= -.5 and r['y'] >= -.5 and r['right'] <= value['width'] + .5 and r['bottom'] <= value['height'] + .5
                                assert value['bodyWidth'] <= value['width'] + 1 and value['scrollWidth'] <= value['clientWidth'] + 1
                                return value

                            page.goto(f'http://127.0.0.1:{server.server_port}/')
                            ready()
                            timing = page.evaluate('performance.getEntriesByType("resource").map(r=>r.name)')
                            assert not [url for url in requests if OPTIONAL.search(url)]
                            assert not [url for url in timing if OPTIONAL.search(url)]
                            case['preReady'] = {'cdpOptionalRequests': [], 'resourceTimingOptionalRequests': [],
                                                'cdpRequests': requests[:], 'resourceTimingRequests': timing,
                                                'enginePresent': page.evaluate('typeof simbarGame.act==="function" && typeof simbarGame.tick==="function"')}
                            click(f'.owner-card[data-payload=\'{{"index":{owner}}}\']')
                            assert page.evaluate('simbarGame.owner') == owner
                            # Hold the selected PNG briefly to inspect the real loading fallback.
                            context.route('**/art/office/*.png', lambda route: pending.append(route))
                            click('[data-action="start"]')
                            page.wait_for_selector('.daydesk')
                            page.wait_for_timeout(80)
                            fallback = page.locator('.desk-loading-owner img')
                            assert fallback.is_visible() and fallback.get_attribute('src').endswith('/' + PORTRAITS[owner])
                            assert pending and all(route.request.url.endswith('/' + OFFICES[owner]) for route in pending)
                            case['fallback'] = {'selectedOwnerOnly': True, 'src': fallback.get_attribute('src')}
                            if viewport_name == 'desktop' and owner == 0:
                                shot('selected-owner-fallback')
                            for route in pending:
                                route.continue_()
                            context.unroute('**/art/office/*.png')
                            page.wait_for_selector('.desk-image-ready')
                            art = page.locator('.desk-scene-image').evaluate('el=>({src:el.src,width:el.naturalWidth,height:el.naturalHeight,rendering:getComputedStyle(el).imageRendering})')
                            assert art['src'].endswith('/' + OFFICES[owner]) and [art['width'], art['height']] == [640, 360]
                            assert art['rendering'] in ['pixelated', 'crisp-edges']
                            assert page.locator('.daydesk').get_attribute('data-owner-index') == str(owner)
                            assert OWNER_NAMES[owner] in page.locator('.desk-header h1').inner_text()
                            assert page.locator(LEGACY_UI).count() == 0
                            assert page.locator('.daydesk input[type="checkbox"]').count() == 1
                            assert 'freewill' in page.locator('.desk-band-option').inner_text().lower()
                            case['art'] = art
                            case['bounds'] = bounds()
                            assert page.evaluate(STATE)['hour'] == 10
                            shot('speech')

                            if viewport_name == 'desktop' and owner == 0:
                                cycle = [{'channel': page.locator('.daydesk').get_attribute('data-focus'), 'atMs': page.evaluate('performance.now()')}]
                                for channel in ['monitor', 'phone']:
                                    page.wait_for_function('c=>document.querySelector(".daydesk")?.dataset.focus===c', arg=channel, timeout=6000)
                                    cycle.append({'channel': channel, 'atMs': page.evaluate('performance.now()')})
                                    if channel == 'monitor':
                                        # Focus the genuinely auto-shown checkbox and ensure it stays on screen.
                                        page.locator('[data-desk-channel="phone"]').focus()
                                        page.keyboard.press('Tab')
                                        assert page.locator('[data-desk-selected]').evaluate('el=>document.activeElement===el')
                                        case['keyboardTabIntoAutoMonitor'] = True
                                        page.wait_for_timeout(150)
                                        focused = page.locator('.daydesk').get_attribute('data-focus')
                                        case['autoMonitorFocusStable'] = focused == 'monitor'
                                        if focused != 'monitor':
                                            report['issues'].append({'case': label, 'kind': 'auto-monitor-keyboard-focus', 'observed': focused, 'expected': 'monitor'})
                                        page.locator('.desk-header h1').click()
                                case['automaticChannels'] = cycle
                                assert [entry['channel'] for entry in cycle] == ['speech', 'monitor', 'phone']

                            focus('phone')
                            assert page.locator(work(1)).is_disabled() and page.locator('.desk-launch').is_disabled()
                            focus('monitor')
                            assert page.locator(work(0)).is_disabled()
                            if owner == 0:
                                page.locator('[data-desk-selected]').focus()
                                page.keyboard.press('Space')
                            else:
                                click('.desk-band-option')
                            page.wait_for_function('simbarGame.journey.booking.freewillSelected===true')
                            assert page.evaluate(STATE)['hour'] == 13
                            click(work(0))
                            page.wait_for_function('simbarGame.journey.booking.deskDoneMask===1')
                            assert page.locator(work(0)).is_disabled() and page.locator('.desk-launch').is_disabled()
                            assert page.evaluate(STATE)['hour'] == 16
                            shot('computer-plan')
                            if owner == 0:
                                click('[data-action="lobby"]')
                                page.reload()
                                ready()
                                click('[data-action="continue"]')
                                page.wait_for_selector('.desk-image-ready')
                                assert page.evaluate(STATE)['done'] == 1 and page.evaluate(STATE)['selected']
                                case['partialSaveReloadResume'] = page.evaluate(STATE)
                            focus('monitor')
                            click('.desk-band-option')
                            page.wait_for_function('!simbarGame.journey.booking.freewillSelected')
                            assert page.evaluate(STATE)['done'] == 0 and page.evaluate(STATE)['hour'] == 10
                            assert page.locator('.desk-launch').is_disabled()
                            click('.desk-band-option')
                            click(work(0))
                            focus('phone')
                            click(work(1))
                            page.wait_for_function('simbarGame.journey.booking.deskDoneMask===3')
                            assert page.locator(work(1)).is_disabled() and page.locator('.desk-launch').is_enabled()
                            assert page.evaluate(STATE)['hour'] == 18 and page.evaluate(STATE)['lineup'] == ['freewill']
                            shot('phone-confirmed')
                            shot('scene', scene=True)
                            case['readyDesk'] = page.evaluate(STATE)
                            case['steps'] = ['actual owner selection', 'selected-owner fallback', 'manual speech/monitor/phone', 'ordered disabled controls', 'checkbox/keyboard', 'computer plan', 'deselect resets checks', 'phone confirmation']
                            office_requests = [url for url in requests if '/art/office/' in url]
                            assert office_requests and all(url.endswith('/' + OFFICES[owner]) for url in office_requests)
                            case['officeRequests'] = office_requests[:]
                            assert not [url for url in requests if re.search(r'/art/album-covers|/StreamingAssets/Music/', url)]
                            click('.desk-launch')
                            page.wait_for_function('["guide","playing"].includes(simbarGame.phase)')
                            while page.evaluate('simbarGame.phase') == 'guide':
                                click('[data-action="guide"]')
                            assert page.evaluate('simbarGame.phase') == 'playing'
                            assert page.evaluate('simbarGame.journey.booking.bookedMask') == 1 << 24
                            case['actualLaunch'] = page.evaluate(STATE)
                            if viewport_name == 'mobile' and owner == 2:
                                page.evaluate('''() => {const g=simbarGame;for(let n=0;n<4000&&g.playing;n++)g.tick(.05);if(!g.closing)throw new Error('Natural end did not close');}''')
                                page.wait_for_function('simbarGame.closing && simbarGame.journey.musicPaused')
                                shot('natural-end')
                                assert page.evaluate('simbarGame.floor.litter>=56 && simbarGame.floor.cleanupStarted')
                                for _ in range(8):
                                    state = page.evaluate('''async () => {const g=simbarGame;for(let batch=0;batch<35&&!g.farewellPending&&g.phase!=='results';batch++){
                                      for(let n=0;n<30&&!g.paused;n++){const button=document.querySelector('.reaction-press');if(button&&!button.disabled)button.click();g.tick(.07);}
                                      await new Promise(requestAnimationFrame);
                                    }return{phase:g.phase,farewell:g.farewellPending,cleaned:g.floor.cleaned,litter:g.floor.litter};}''')
                                    if state['farewell']:
                                        shot('farewell')
                                        assert page.locator('.farewell-card [data-action="ui:musicToggle"]').count() == 0
                                        click('[data-action="farewellAck"]')
                                    if page.evaluate('simbarGame.phase') == 'results':
                                        break
                                assert page.evaluate('simbarGame.phase') == 'results'
                                assert page.evaluate('simbarGame.floor.cleanupComplete && simbarGame.floor.cleanupRewardClaimed')
                                shot('results')
                                case['naturalNightEnd'] = {'farewellAcknowledged': True, 'cleanupComplete': True, 'phase': 'results'}
                            assert not report['errors'] and not report['httpErrors'] and not report['failedRequests']
                        except Exception:
                            page.screenshot(path=str(OUT / (label + '-failure.png')))
                            raise
                        finally:
                            monitor['active'] = False
                            context.close()
                report['checks'] = ['All three selected-owner office PNGs verified at640x360 with packaged hashes.',
                                    'No office, jacket or MP3 requests before Ready in CDP and ResourceTiming for six fresh contexts.',
                                    'Only selected-owner office art requested after actual Start; real loading fallback keeps the same owner.',
                                    'Desktop/mobile manual channels, one automatic cycle, checkbox, ordered work, disabled controls, uncheck reset and partial-save resume exercised.',
                                    'All six actual desk launches reached playing with only Freewill booked; one natural finale completed farewell and cleanup.',
                                    'No old poster/demo/chance/roster UI, other band checkbox, horizontal overflow or covered controls; zero JavaScript/HTTP/failed-request errors.']
                report['passed'] = not report['issues']
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
    print(json.dumps({'passed': report['passed'], 'issues': report['issues'], 'cases': len(report['cases']), 'release': release}, ensure_ascii=False))
    if not report['passed']:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
