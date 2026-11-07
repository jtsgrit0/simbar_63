"""Verify the real DOM/audio finale with accelerated normal simulation ticks."""
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
import hashlib
import json
from pathlib import Path
import threading
from urllib.parse import unquote, urlsplit

from playwright.sync_api import sync_playwright
from benchmark_web_loading import chromium_executable

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / 'Research/PixelArtVerification/CurtainCleanup/DOM'
VIEW = ROOT / 'Library/ShowFinaleQA/index.html'
FAREWELL = '오늘의 라이브 밴드 공연은 하얗게 불태웠어요! 내일 또 공연 보러와요!'
HTML = '''<!doctype html><html lang="ko"><meta charset="utf-8">
<base href="/"><meta name="viewport" content="width=device-width,initial-scale=1">
<style>html,body{margin:0;width:100%;height:100%;background:#10121e}#game-stage{position:relative;width:100%;height:100%}</style>
<div id="game-stage"></div><script type="module">
import {startGame} from '/Tools/WebGLTemplate/FastGame/game.js';
window.simbarApplication=await startGame({root:document.querySelector('#game-stage'),assetsBase:'/Tools/WebGLTemplate/FastGame/art/'});
window.simbarGameReadyAt=performance.now();
</script></html>'''
OBSERVE_AUDIO = '''() => {
 window.qaAudioElements=[];window.qaAudioEvents=[];
 const OriginalAudio=window.Audio;
 window.Audio=new Proxy(OriginalAudio,{construct(target,args){
   const audio=Reflect.construct(target,args,target);window.qaAudioElements.push(audio);
   for(const name of ['play','pause','playing','error'])audio.addEventListener(name,()=>window.qaAudioEvents.push({event:name,paused:audio.paused,src:audio.src,error:audio.error?.code||0}));
   return audio;
 }});
}'''
SNAPSHOT = '''() => {
 const g=simbarGame,a=g.journey.afterShow,audio=qaAudioElements.at(-1);
 return {phase:g.phase,paused:g.paused,reason:g.pauseReason,musicPaused:g.journey.musicPaused,
  musicPauseSession:g.journey.musicPauseSession,session:g.journey.session,elapsed:g.elapsed,
  floorPending:g.floorTasksPending,floor:g.floor,reaction:g.reaction,
  farewellPending:g.farewellPending,afterShow:a,settledSession:g.journey.settledSession,
  money:g.state.money,xp:g.journey.xp,completedNights:g.journey.completedNights,
  audio:audio?{paused:audio.paused,currentTime:audio.currentTime,readyState:audio.readyState,error:audio.error?.code||0,src:audio.src}:null,
  stageRender:window.qaStageRender||null,
  people:[...(a?.members||[]),...(a?.audience||[])].map(p=>{const actor=g.actors.find(x=>x.id===p.id);return{id:p.id,phase:p.phase,x:actor.x,z:actor.z,visible:actor.visible,moving:actor.moving};})};
}'''
OBSERVE_STAGE = '''() => {
 const app=simbarApplication,ctx=app.canvas.getContext('2d'),draw=ctx.drawImage.bind(ctx),gradient=ctx.createLinearGradient.bind(ctx),render=app.world.render.bind(app.world);
 window.qaStageRender={gradients:0,covers:[]};
 ctx.drawImage=(...args)=>{if(args[0]?.src?.includes('/album-covers/'))qaStageRender.covers.push({src:args[0].src,rect:args.slice(1)});return draw(...args);};
 ctx.createLinearGradient=(...args)=>{qaStageRender.gradients++;return gradient(...args);};
 app.world.render=(...args)=>{window.qaStageRender={gradients:0,covers:[]};return render(...args);};
}'''
OBSERVE_TICKS = '''() => {
 const g=simbarGame,tick=g.tick.bind(g);
 window.qaChoreography={ticks:0,phases:{},violations:[],departureObserved:false};
 g.tick=dt=>{tick(dt);const qa=qaChoreography,a=g.journey.afterShow;qa.ticks++;if(!a)return;
  for(const m of a.members){const list=qa.phases[m.id]??=[];if(!list.includes(m.phase))list.push(m.phase);}
  if(a.departureStarted){qa.departureObserved=true;if(a.members.some(m=>m.greeting!==2))qa.violations.push('Departure preceded the final greeting');}
  else if([...a.members,...a.audience].some(m=>m.phase==='gone'))qa.violations.push('An actor left before the greeting barrier');
  if(g.farewellPending&&[...a.members,...a.audience].some(m=>m.phase!=='gone'))qa.violations.push('Popup preceded the final exit');
 };
}'''
RESULT_BOUNDS = '''() => {
 const rect=el=>{const b=el.getBoundingClientRect();return{x:b.x,y:b.y,right:b.right,bottom:b.bottom,width:b.width,height:b.height};};
 return {viewport:{width:innerWidth,height:innerHeight},panel:rect(document.querySelector('.results-ref')),
  controls:[...document.querySelectorAll('.results-ref .settings-entry,.result-saved,.result-music button,.result-actions button')].map(el=>({label:el.textContent.trim(),...rect(el)}))};
}'''


class SourceHandler(SimpleHTTPRequestHandler):
    def log_message(self, *_args):
        pass

    def translate_path(self, path):
        decoded = unquote(urlsplit(path).path)
        if decoded.startswith('/StreamingAssets/'):
            base = (ROOT / 'Assets/StreamingAssets').resolve()
            target = (base / decoded.removeprefix('/StreamingAssets/')).resolve()
            if target.is_relative_to(base):
                return str(target)
        return super().translate_path(path)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    VIEW.parent.mkdir(parents=True, exist_ok=True)
    VIEW.write_text(HTML, encoding='utf-8', newline='\n')
    server = ThreadingHTTPServer(('127.0.0.1', 0), partial(SourceHandler, directory=ROOT))
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    report = {'passed': False, 'checks': [], 'errors': [], 'httpErrors': [], 'screenshots': [],
              'sourceSha256': {name: hashlib.sha256((ROOT / 'Tools/WebGLTemplate/FastGame' / name).read_bytes()).hexdigest()
                               for name in ['game.js', 'core.js', 'aftershow.js', 'music.js', 'album-art.js', 'world.js', 'style.css']}}
    try:
        with sync_playwright() as pw:
            executable = chromium_executable(None)
            browser = pw.chromium.launch(**({'executable_path': executable} if executable else {}))
            context = browser.new_context(viewport={'width': 1280, 'height': 800}, locale='ko-KR')
            context.add_init_script('('+OBSERVE_AUDIO+')()')
            page = context.new_page()
            page.on('pageerror', lambda error: report['errors'].append(str(error)))
            page.on('response', lambda response: report['httpErrors'].append({'url': response.url, 'status': response.status}) if response.status >= 400 else None)
            cover_requests = []
            page.on('request', lambda request: cover_requests.append(request.url) if '/album-covers' in request.url else None)

            def ready():
                page.wait_for_function('window.simbarGameReadyAt>0', timeout=60000)

            def shot(label):
                page.screenshot(path=str(OUT / (label+'.png')))
                report['screenshots'].append(label+'.png')

            def click(selector):
                control = page.locator(selector)
                control.wait_for(state='visible')
                uncovered = control.evaluate('el=>{const b=el.getBoundingClientRect();return el.contains(document.elementFromPoint(b.x+b.width/2,b.y+b.height/2));}')
                if not uncovered:
                    report['coveredControl'] = {'selector': selector, 'details': control.evaluate('el=>{const b=el.getBoundingClientRect(),hit=document.elementFromPoint(b.x+b.width/2,b.y+b.height/2);return{bounds:b.toJSON(),hit:hit?.outerHTML?.slice(0,700)};}')}
                    shot('covered-control-failure')
                    raise AssertionError(selector+' is covered')
                control.click()
                page.wait_for_timeout(80)

            def results_bounds(label):
                bounds = page.evaluate(RESULT_BOUNDS)
                panel, viewport = bounds['panel'], bounds['viewport']
                assert panel['x'] >= -.5 and panel['y'] >= -.5
                assert panel['right'] <= viewport['width']+.5 and panel['bottom'] <= viewport['height']+.5
                for control in bounds['controls']:
                    assert control['x'] >= panel['x']-.5 and control['y'] >= panel['y']-.5, control
                    assert control['right'] <= panel['right']+.5 and control['bottom'] <= panel['bottom']+.5, control
                report.setdefault('resultsBounds', {})[label] = bounds

            page.goto(f'http://127.0.0.1:{server.server_port}/Library/ShowFinaleQA/index.html')
            ready()
            assert not cover_requests, 'Album assets entered the initial loading path.'
            report['checks'].append('Owner-selection readiness makes no album manifest or jacket request.')
            report['seed'] = page.evaluate('''async () => {
              const {SimBarGame}=await import('/Tools/WebGLTemplate/FastGame/core.js');
              for(let seed=1;seed<200;seed++){
                const mem=new Map(),storage={getItem:k=>mem.get(k)||null,setItem:(k,v)=>mem.set(k,String(v)),removeItem:k=>mem.delete(k)};
                const g=new SimBarGame({seed,storage});g.act('start');g.act('deskSelect',{selected:true});g.act('deskWork',{task:0});g.act('deskWork',{task:1});g.act('launch');while(g.phase==='guide')g.act('guide');
                if(g.reaction.paperCount&&g.reaction.guestScheduled){simbarGame.importSave(g.exportSave());return seed;}
              }throw new Error('No seeded night with both real random floor events');
            }''')
            page.reload();ready()
            click('button[data-action="continue"]')
            page.wait_for_function('qaAudioElements.at(-1)?.paused===false && qaAudioElements.at(-1)?.readyState>=2')
            assert page.evaluate('simbarGame.phase') == 'playing'
            page.evaluate(OBSERVE_STAGE)
            report['albumTrackSelections'] = []
            for index in range(5):
                click(f'.playlist button[data-playlist="{index}"]')
                page.wait_for_function('index=>!!simbarApplication.albumArt.covers[index]', arg=index)
                page.wait_for_timeout(50)
                stage = page.evaluate('qaStageRender')
                assert stage['gradients'] == 4 and len(stage['covers']) == 1
                assert stage['covers'][0]['rect'][:4] == [0, 0, 224, 224]
                report['albumTrackSelections'].append({'index': index, 'stageRender': stage})
            assert len(cover_requests) == 4 and len(set(cover_requests)) == 4
            report['initialAlbumRequests'] = cover_requests[:]
            report['checks'].append('All five actual playlist buttons lazily select full original jackets with one manifest and three shared images.')
            click('.playlist button[data-playlist="0"]')
            report['performance'] = page.evaluate(SNAPSHOT)
            page.evaluate(OBSERVE_TICKS)
            page.evaluate('''() => {let n=0;while(simbarGame.phase==='playing'&&n++<4000)simbarGame.tick(.05);if(simbarGame.phase!=='closing')throw new Error('Performance never ended');}''')
            page.wait_for_function('qaAudioElements.at(-1)?.paused===true')
            end = page.evaluate(SNAPSHOT)
            assert end['musicPaused'] and end['phase'] == 'closing' and not end['farewellPending']
            assert end['stageRender']['gradients'] == 0
            end['unlitLensPixels'] = page.evaluate('''() => {const c=simbarApplication.canvas,p=c.getContext('2d'),offset=Math.round((c.width-960)/2);return[[643,121],[683,128],[735,140],[782,151],[822,162]].map(([x,y])=>Array.from(p.getImageData(x+offset,y,1,1).data));}''')
            assert all(max(pixel[:3]) < 70 for pixel in end['unlitLensPixels'])
            report['endPerformance'] = end
            shot('01-performance-ended-audio-paused')
            report['checks'].append('Natural performance end pauses the actual HTMLAudioElement once.')

            assert page.locator('.closing-music').count() == 0, 'A removed extra left-side music control reappeared.'
            replay = '.playlist button[data-action="ui:musicToggle"]'
            click(replay)
            page.wait_for_function('!simbarGame.journey.musicPaused && qaAudioElements.at(-1)?.paused===false')
            page.wait_for_timeout(150)
            assert not page.evaluate(SNAPSHOT)['audio']['paused']
            assert page.evaluate('qaStageRender.gradients') == 0
            report['checks'].append('Manual replay after the performance survives subsequent rendering frames.')
            page.set_viewport_size({'width': 390, 'height': 844})
            page.wait_for_timeout(80)
            shot('01-mobile-closing-replay')
            click(replay)
            page.wait_for_function('simbarGame.journey.musicPaused && qaAudioElements.at(-1)?.paused===true')
            click(replay)
            page.wait_for_function('!simbarGame.journey.musicPaused && qaAudioElements.at(-1)?.paused===false')
            assert page.locator('.closing-music').count() == 0
            assert page.evaluate('qaStageRender.gradients') == 0
            report['checks'].append('Closing replay remains visible and touch-clickable on 390×844 with a floor reaction active.')
            page.set_viewport_size({'width': 1280, 'height': 800})

            page.evaluate('''() => {const g=simbarGame;for(let n=0;n<1800;n++){if(g.journey.afterShow.members.some(m=>m.phase==='highfive'&&m.timer>.35)){g.act('pause');return;}g.tick(.05);}throw new Error('No actual high five');}''')
            page.wait_for_timeout(60)
            shot('02-high-five-save')
            gesture = page.evaluate(SNAPSHOT)
            report['beforeReload'] = gesture
            report['beforeReloadChoreography'] = page.evaluate('qaChoreography')
            page.reload();ready()
            restored = page.evaluate(SNAPSHOT)
            report['afterReload'] = restored
            def durable_choreography(after):
                # Continuous mosh headings can exceed a full turn. The shared
                # save validator normalizes these unused home-facing fields.
                return {**after, 'audience': [{k: v for k, v in fan.items() if k != 'homeFacing'} for fan in after['audience']]}
            assert durable_choreography(restored['afterShow']) == durable_choreography(gesture['afterShow'])
            assert not restored['musicPaused']
            click('button[data-action="continue"]')
            page.wait_for_function('qaAudioElements.at(-1)?.paused===false')
            page.evaluate(OBSERVE_STAGE)
            assert not page.evaluate('simbarGame.journey.musicPaused')
            report['checks'].append('Reload preserves the in-progress greeting and manually resumed music choice.')
            page.evaluate(OBSERVE_TICKS)
            page.evaluate('''() => {for(let n=0;n<6000&&!simbarGame.farewellPending;n++)simbarGame.tick(.05);if(!simbarGame.farewellPending)throw new Error('No farewell after all exits');}''')
            page.locator('.farewell-card').wait_for(state='visible')
            farewell = page.evaluate(SNAPSHOT)
            choreography = page.evaluate('qaChoreography')
            assert len(farewell['people']) == 24
            assert all(p['phase'] == 'gone' and not p['visible'] and not p['moving'] for p in farewell['people'])
            assert all(((p['x']-8.8)**2+(p['z']+5.55)**2)**.5 < .22 for p in farewell['people'])
            assert not choreography['violations'] and choreography['departureObserved']
            for member in ['singer', 'guitarist', 'bassist', 'drummer']:
                phases = set(report['beforeReloadChoreography']['phases'].get(member, []) + choreography['phases'].get(member, []))
                assert {'highfive', 'hug', 'exit', 'gone'} <= phases, (member, phases)
            assert page.locator('.farewell-message').inner_text() == FAREWELL
            assert farewell['settledSession'] != farewell['session'] and farewell['floorPending']
            assert farewell['paused'] and farewell['reason'] == 'farewell'
            report['farewell'] = farewell
            report['choreography'] = choreography
            shot('03-all-departed-farewell')
            report['checks'].append('All greetings finish before 24 actors physically exit; only then does the exact Korean farewell appear.')

            page.set_viewport_size({'width': 390, 'height': 844})
            page.wait_for_timeout(80)
            shot('03-mobile-farewell')

            assert page.locator('.farewell-card [data-action^="ui:music"]').count() == 0
            before = page.evaluate(SNAPSHOT)
            page.wait_for_timeout(150)
            assert page.evaluate(SNAPSHOT)['elapsed'] == before['elapsed']
            report['checks'].append('Farewell has no duplicate music controls and freezes the game until acknowledged.')
            page.evaluate('simbarGame.save()')
            page.reload();ready()
            assert page.evaluate('simbarGame.phase') == 'lobby'
            assert page.locator('.farewell-card').count() == 0
            click('.title-screen button[data-action="ui:settings"]')
            page.locator('.settings-ref').wait_for(state='visible')
            click('.settings-ref button[data-action="ui:close"]')
            click('button[data-action="continue"]')
            page.locator('.farewell-card').wait_for(state='visible')
            assert page.locator('.farewell-message').inner_text() == FAREWELL
            assert not page.evaluate('simbarGame.journey.musicPaused')
            report['checks'].append('Saving and reloading the unacknowledged farewell restores the modal and music choice.')
            page.set_viewport_size({'width': 1280, 'height': 800})

            click('button[data-action="farewellAck"]')
            acknowledged = page.evaluate(SNAPSHOT)
            assert acknowledged['phase'] == 'closing' and not acknowledged['paused']
            assert acknowledged['floorPending'] and not acknowledged['farewellPending']
            assert acknowledged['settledSession'] != acknowledged['session']
            assert page.locator('.farewell-card').count() == 0
            shot('04-acknowledged-cleanup-remains')
            report['checks'].append('Acknowledging the popup resumes real outstanding cleanup without an early receipt.')

            # The real reaction buttons remain the only way to authorize work.
            # Normal accelerated ticks wait for staff arrival, stage changes and returns.
            pressed = {'cleanup': 0, 'guest': 0}
            # New reaction stages rebuild on RAF; repeat short simulation batches
            # so the actual delegated DOM buttons always have their current payload.
            for _ in range(60):
                if page.evaluate('simbarGame.phase') == 'results':
                    break
                page.wait_for_timeout(30)
                count = page.evaluate('''() => {const g=simbarGame,counts={cleanup:0,guest:0};for(let n=0;n<40&&g.phase!=='results';n++){const kind=g.reaction.activeKind,b=document.querySelector('.reaction-press');if(b&&!b.disabled&&kind&&JSON.parse(b.dataset.payload).kind===kind){b.click();counts[kind===1?'cleanup':'guest']++;}g.tick(.05);}return counts;}''')
                for kind in pressed:
                    pressed[kind] += count[kind]
            page.locator('.results-ref').wait_for(state='visible')
            result = page.evaluate(SNAPSHOT)
            assert result['phase'] == 'results' and not result['floorPending']
            assert result['settledSession'] == result['session'] and result['completedNights'] == 1
            report['reactionButtonPresses'] = pressed
            report['result'] = result
            results_bounds('desktop-1280x800')
            shot('05-results-without-popup-music')
            report['checks'].append('Real reaction buttons complete remaining floor missions and award exactly one final receipt.')
            assert page.locator('.results-ref [data-action^="ui:music"]').count() == 0
            assert page.locator('.playlist [data-action="ui:musicToggle"]').count() == 1
            report['checks'].append('Results has no duplicate music controls; the original playlist remains.')
            page.set_viewport_size({'width': 390, 'height': 844})
            page.wait_for_timeout(80)
            results_bounds('mobile-390x844')
            shot('05-mobile-results-without-popup-music')
            assert page.locator('.results-ref [data-action^="ui:music"]').count() == 0
            report['checks'].append('390×844 results remains within the viewport without popup music controls.')

            final = page.evaluate(SNAPSHOT)
            page.evaluate('simbarGame.save()')
            page.reload();ready();click('button[data-action="continue"]')
            page.locator('.results-ref').wait_for(state='visible')
            receipt = page.evaluate(SNAPSHOT)
            for key in ['money', 'xp', 'completedNights', 'settledSession']:
                assert receipt[key] == final[key], key
            assert not receipt['farewellPending'] and not receipt['musicPaused']
            assert page.locator('.farewell-card').count() == 0
            report['checks'].append('Reloading the results preserves money/XP/completion and never repeats the popup or payout.')
            report['coverRequests'] = cover_requests
            report['audioEvents'] = page.evaluate('qaAudioEvents')
            assert not report['errors'], report['errors']
            assert not report['httpErrors'], report['httpErrors']
            assert not report.get('coveredControls'), report.get('coveredControls')
            report['passed'] = True
            context.close();browser.close()
    except Exception as error:
        report['failure'] = str(error)
        raise
    finally:
        server.shutdown();server.server_close()
        (OUT / 'dom-audio-qa.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf-8', newline='\n')
    print(json.dumps({'passed': report['passed'], 'checks': report['checks'], 'errors': report['errors']}))


if __name__ == '__main__':
    main()
