"""Verify visible real closing paper and cleanup during actual band greetings."""
from functools import partial
from http.server import ThreadingHTTPServer
import base64
import hashlib
import json
from pathlib import Path
import threading

from playwright.sync_api import sync_playwright
from benchmark_web_loading import chromium_executable
from qa_native_show_finale import HTML, SourceHandler

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / 'Research/PixelArtVerification/CurtainCleanup/Concurrent'
VIEW = ROOT / 'Library/CurtainCleanupQA/index.html'
STATE = '''() => {
 const g=simbarGame,marks=qaGround(g.floor),a=g.journey.afterShow;
 return{phase:g.phase,paused:g.paused,elapsed:g.elapsed,floor:g.floor,ready:g.cleanupReadyWorkers,
  effects:g.effects,afterShow:a,musicPaused:g.journey.musicPaused,settled:g.journey.settledSession,
  completedNights:g.journey.completedNights,marks:{confetti:marks.filter(p=>p.kind===0).length,tissue:marks.filter(p=>p.kind===1).length},
  pixels:{confetti:qaPaperFrame.colored.length,tissueAir:qaPaperFrame.tissueAir.length,tissueGround:qaPaperFrame.tissueGround.length},
  staff:g.actors.filter(p=>g.crew.includes(p.id)).map(p=>({id:p.id,action:p.action,x:p.x,z:p.z,ready:p._ready}))};
}'''
OBSERVE = '''async () => {
 const {pixelGroundPaper}=await import('/Tools/WebGLTemplate/FastGame/world.js');window.qaGround=pixelGroundPaper;
 const app=simbarApplication,ctx=app.canvas.getContext('2d'),fill=ctx.fillRect.bind(ctx),render=app.world.render.bind(app.world),colors=['#ff6fa9','#62e2ed','#ffe17c','#b9a0ff'];
 window.qaPaperFrame={colored:[],tissueAir:[],tissueGround:[]};window.qaClock=performance.now();
 ctx.fillRect=(...args)=>{const color=ctx.fillStyle;if(colors.includes(color))qaPaperFrame.colored.push(args);if(color==='#fff7e8'&&args[2]===3&&args[3]===2)qaPaperFrame.tissueAir.push(args);if(color==='#e8e2d5')qaPaperFrame.tissueGround.push(args);return fill(...args);};
 app.world.render=(...args)=>{window.qaPaperFrame={colored:[],tissueAir:[],tissueGround:[]};return render(...args);};
 window.qaAdvance=(count,respond=false,stop='')=>{
  const g=simbarGame;
  for(let n=0;n<count&&!g.paused;n++){
   const b=document.querySelector('.reaction-press');
   if(respond&&g.reaction.activeKind===1&&b&&!b.disabled&&JSON.parse(b.dataset.payload).kind===1)b.click();
   g.tick(.07);app.world.render(window.qaClock+=70);
   const gesture=g.journey.afterShow?.members.some(m=>['highfive','hug'].includes(m.phase)&&m.timer>.35);
   if(stop==='gesture'&&gesture&&g.floor.cleaned>0&&g.floor.cleaned<g.floor.litter&&g.actors.some(a=>g.crew.includes(a.id)&&a.action==='sweep')){g.act('pause');return true;}
   if(stop==='half'&&g.floor.cleaned>=g.floor.litter*.5&&g.floor.cleaned<g.floor.litter){g.act('pause');return true;}
   if(g.farewellPending)return false;
  }return false;
 };
}'''


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    VIEW.parent.mkdir(parents=True, exist_ok=True)
    VIEW.write_text(HTML, encoding='utf-8', newline='\n')
    server = ThreadingHTTPServer(('127.0.0.1', 0), partial(SourceHandler, directory=ROOT))
    threading.Thread(target=server.serve_forever, daemon=True).start()
    report = {'passed': False, 'checks': [], 'errors': [], 'httpErrors': [], 'screenshots': [],
              'sourceSha256': {name: hashlib.sha256((ROOT/'Tools/WebGLTemplate/FastGame'/name).read_bytes()).hexdigest()
                               for name in ['world.js', 'core.js', 'floor.js', 'game.js']},
              'jessySha256': hashlib.sha256((ROOT/'Tools/WebGLTemplate/FastGame/art/owner-female.png').read_bytes()).hexdigest()}
    try:
        with sync_playwright() as pw:
            executable = chromium_executable(None)
            browser = pw.chromium.launch(**({'executable_path':executable} if executable else {}))
            page = browser.new_page(viewport={'width':1280,'height':800}, locale='ko-KR')
            page.on('pageerror', lambda error: report['errors'].append(str(error)))
            page.on('response', lambda response: report['httpErrors'].append({'url':response.url,'status':response.status}) if response.status>=400 else None)

            def ready():
                page.wait_for_function('window.simbarGameReadyAt>0', timeout=60000)

            def paint():
                page.evaluate('() => new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)))')

            def shot(label, canvas=True, screen=True):
                if screen:
                    page.screenshot(path=str(OUT/(label+'.png')));report['screenshots'].append(label+'.png')
                if canvas:
                    raw=page.evaluate('simbarApplication.canvas.toDataURL().split(",")[1]')
                    (OUT/(label+'-canvas.png')).write_bytes(base64.b64decode(raw));report['screenshots'].append(label+'-canvas.png')

            def resume():
                if page.evaluate('simbarGame.paused'):
                    page.keyboard.press('p');paint()

            page.goto(f'http://127.0.0.1:{server.server_port}/Library/CurtainCleanupQA/index.html');ready()
            report['seed'] = page.evaluate('''() => {const Game=simbarGame.constructor;for(let seed=1;seed<500;seed++){
              const mem=new Map(),g=new Game({seed,storage:{getItem:k=>mem.get(k)||null,setItem:(k,v)=>mem.set(k,String(v)),removeItem:k=>mem.delete(k)}});
              g.act('start');g.act('launch');while(g.phase==='guide')g.act('guide');
              if(!g.reaction.paperCount&&!g.reaction.guestScheduled){simbarGame.importSave(g.exportSave());return seed;}
            }throw new Error('No normal seeded quiet night');}''')
            page.reload();ready();page.locator('button[data-action="continue"]').click();paint();page.evaluate(OBSERVE)
            assert page.evaluate('simbarGame.floor.litter') == 0
            page.evaluate('''() => {for(let n=0;n<4000&&simbarGame.phase==='playing';n++)simbarGame.tick(.05);}''')
            page.wait_for_function('qaPaperFrame.tissueAir.length>100 && qaPaperFrame.colored.length>24')
            burst = page.evaluate(STATE)
            assert burst['phase']=='closing' and burst['floor']['confettiThrows']==1 and burst['floor']['tissueThrows']==1
            assert burst['floor']['litter']==56 and burst['floor']['cleaned']==0 and len(burst['effects'])==2
            assert burst['marks']=={'confetti':24,'tissue':16}
            report['burst']=burst;shot('01-both-real-paper-bursts')
            report['checks'].append('A real zero-paper night emits both missing types once at closing: visible colored confetti and long tissue with actual56 litter debt.')

            page.keyboard.press('p');paint()
            frozen=page.evaluate('simbarApplication.canvas.toDataURL()');page.wait_for_timeout(250)
            assert page.evaluate('simbarApplication.canvas.toDataURL()')==frozen
            report['checks'].append('Global pause freezes flying paper, grounded debris and the rest of the canvas pixel-exactly.')
            imported = page.evaluate('''() => {const g=simbarGame,oldFloor=g.floor,world=simbarApplication.world,raw=g.exportSave();const success=g.importSave(raw);window.qaOriginalWorld=world;return{success,floorReplaced:g.floor!==oldFloor,session:g.journey.session};}''')
            assert imported['success'] and imported['floorReplaced']
            paint();page.locator('button[data-action="continue"]').click();paint()
            after_import=page.evaluate(STATE)
            assert page.evaluate('simbarApplication.world===qaOriginalWorld')
            assert not after_import['effects'] and not after_import['pixels']['tissueAir']
            assert after_import['marks']==burst['marks'] and after_import['pixels']['tissueGround']>0
            report['sameSessionImport']=after_import
            report['checks'].append('A same-session import resets the existing renderer’s old flying pool while preserving its saved ground litter.')

            concurrent=False
            for _ in range(100):
                concurrent=page.evaluate("qaAdvance(8,true,'gesture')")
                paint()
                if concurrent:
                    break
            assert concurrent, page.evaluate(STATE)
            during=page.evaluate(STATE)
            assert during['floor']['cleaned']>0 and during['floor']['cleaned']<during['floor']['litter']
            assert any(m['phase'] in ['highfive','hug'] for m in during['afterShow']['members'])
            assert any(worker['action']=='sweep' for worker in during['staff'])
            assert not during['afterShow']['completed']
            report['concurrentGreeting']=during;shot('02-cleaning-during-real-greeting',screen=False)
            report['checks'].append('Real C-response buttons authorize sweeping and reduce debt while a band member is actually high-fiving or hugging a fan.')

            saved_cleaned=during['floor']['cleaned']
            page.reload();ready();page.locator('button[data-action="continue"]').click();paint();page.evaluate(OBSERVE);paint()
            restored=page.evaluate(STATE)
            assert restored['floor']['confettiThrows']==1 and restored['floor']['tissueThrows']==1 and restored['floor']['finaleEnsured']
            assert restored['floor']['cleaned']>=saved_cleaned-.001 and not restored['effects'] and not restored['pixels']['tissueAir']
            assert restored['marks']['tissue']>0 and restored['pixels']['tissueGround']>0
            report['restored']=restored;shot('03-saved-litter-without-replayed-burst',screen=False)
            report['checks'].append('Reload preserves cleanup debt, visible litter and the greeting; it cannot replay the finale explosion or add throws.')

            half=False
            for _ in range(100):
                half=page.evaluate("qaAdvance(6,true,'half')");paint()
                if half:
                    break
            assert half, page.evaluate(STATE)
            halfway=page.evaluate(STATE)
            assert sum(halfway['marks'].values())<sum(burst['marks'].values()) and sum(halfway['marks'].values())>0
            report['halfCleaned']=halfway;shot('04-actual-debt-removes-ground-paper',screen=False)
            report['checks'].append('Ground confetti and folded tissue shrink from the same stable positions as actual cleaned debt increases.')
            resume()

            for _ in range(180):
                page.evaluate("qaAdvance(10,true)");paint()
                if page.evaluate('simbarGame.farewellPending'):
                    break
            page.locator('.farewell-card').wait_for(state='visible')
            final=page.evaluate(STATE)
            assert final['floor']['cleanupComplete'] and final['marks']=={'confetti':0,'tissue':0}
            assert not final['pixels']['tissueAir'] and not final['pixels']['tissueGround']
            assert page.locator('.farewell-card [data-action^="ui:music"]').count()==0
            assert page.locator('.farewell-card button').count()==1
            report['finalFarewell']=final;shot('05-clean-floor-farewell-without-music',canvas=False)
            page.locator('button[data-action="farewellAck"]').click();paint()
            for _ in range(80):
                if page.evaluate('simbarGame.phase')=='results':
                    break
                page.evaluate('qaAdvance(12,true)');paint()
            page.locator('.results-ref').wait_for(state='visible')
            assert page.locator('.results-ref [data-action^="ui:music"]').count()==0
            assert page.locator('.playlist [data-action="ui:musicToggle"]').count()==1
            result=page.evaluate(STATE);assert result['completedNights']==1
            report['result']=result;shot('06-results-without-duplicate-music',canvas=False)
            report['checks'].append('All litter clears, farewell/results omit music controls, the original playlist remains and the night settles once.')
            assert not report['errors'],report['errors']
            assert not report['httpErrors'],report['httpErrors']
            report['passed']=True;browser.close()
    except Exception as error:
        report['failure']=str(error)
        raise
    finally:
        server.shutdown();server.server_close()
        (OUT/'browser-qa.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print(json.dumps({'passed':report['passed'],'checks':report['checks'],'errors':report['errors']}))


if __name__=='__main__':
    main()
