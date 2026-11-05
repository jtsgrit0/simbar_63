"""Exercise actual native game UI and save behavior in Chromium. Pass URL as argv1."""
from playwright.sync_api import sync_playwright
from benchmark_web_loading import chromium_executable
from pathlib import Path
import json,sys,time
url=sys.argv[1] if len(sys.argv)>1 else 'http://127.0.0.1:8769/'
out=Path(sys.argv[2] if len(sys.argv)>2 else 'Research/PixelArtVerification/NaturalNight');out.mkdir(parents=True,exist_ok=True)
report={'url':url,'errors':[],'guest':False,'cleanup':False,'timeout':False,'passed':False}
with sync_playwright() as p:
    browser=p.chromium.launch(**({'executable_path':chromium_executable(None)} if chromium_executable(None) else {}))
    context=browser.new_context(viewport={'width':1280,'height':800},device_scale_factor=2,has_touch=True)
    page=context.new_page();page.on('pageerror',lambda e:report['errors'].append(str(e)))
    page.goto(url);page.wait_for_function('window.simbarGame && document.querySelector(".owner-card")')
    report['runtime']=page.locator('script[type="module"]').get_attribute('src')
    (out/'run.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    def click(action,n=0):page.locator('button[data-action="'+action+'"]').nth(n).click();page.wait_for_timeout(100)
    def shot(name):page.screenshot(path=str(out/(name+'.png')));print(name,flush=True)
    click('start');click('ui:tab',1);click('ui:planPrimary')
    for _ in range(3):click('guide')
    started=time.monotonic();last_log=0;guest_rounds=0;cleanup_rounds=0
    while time.monotonic()-started<380:
        data=page.evaluate('({phase:simbarGame.phase,kind:simbarGame.reaction?.activeKind||0,stage:simbarGame.reaction?.activeStage||0,presses:simbarGame.reaction?.presses||0,timeouts:simbarGame.reaction?.timeouts||0,ready:simbarGame.cleanupReadyWorkers})')
        if time.monotonic()-started>last_log:
            print(round(time.monotonic()-started),data,flush=True);last_log+=20
            report['lastObserved']=data
            report['duration']=time.monotonic()-started
            (out/'run.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
        if data['phase']=='results':break
        if data['kind']==2 and not report['timeout']:
            report['guest']=True;shot('guest-natural-start')
            page.keyboard.down('f');page.wait_for_timeout(800);page.keyboard.up('f')
            assert page.evaluate('simbarGame.reaction.presses')<=1,'Holding F repeatedly filled the counter.'
            page.wait_for_function('simbarGame.reaction.timeouts>'+str(data['timeouts']),timeout=12000)
            report['timeout']=True;shot('guest-timeout');page.wait_for_timeout(1700);continue
        if data['kind'] in (1,2):
            kind=data['kind'];target=10 if kind==1 else 8
            if kind==1:
                report['cleanup']=True;assert data['ready']>0
                page.set_viewport_size({'width':844,'height':390} if cleanup_rounds%2 else {'width':393,'height':852});cleanup_rounds+=1
            elif guest_rounds==0:page.set_viewport_size({'width':1280,'height':800})
            elif guest_rounds==1:page.set_viewport_size({'width':393,'height':852})
            else:page.set_viewport_size({'width':844,'height':390})
            page.wait_for_timeout(120);shot(('cleanup' if kind==1 else 'guest')+'-round-'+str(cleanup_rounds if kind==1 else guest_rounds))
            for _ in range(target):
                if kind==2 and guest_rounds==0:page.keyboard.press('f',delay=25)
                else:
                    q=page.locator('.reaction-press:enabled')
                    if not q.count():break
                    q.tap()
                page.wait_for_timeout(95)
            if kind==2:guest_rounds+=1
            page.set_viewport_size({'width':1280,'height':800});page.wait_for_timeout(100)
        else:page.wait_for_timeout(200)
    assert page.evaluate('simbarGame.phase')=='results','Natural night did not settle.'
    shot('night-complete')
    report['receipt']=page.evaluate('({cash:simbarGame.state.money,xp:simbarGame.journey.xp,bonus:simbarGame.journey.resultFloorBonus,tasks:simbarGame.journey.resultFloorTasks,guest:simbarGame.floor.guestComplete,clean:simbarGame.floor.cleanupComplete})')
    cash=report['receipt']['cash'];xp=report['receipt']['xp']
    if report['cleanup']:assert report['receipt']['bonus']==70 and report['receipt']['clean']
    page.reload();page.wait_for_function('window.simbarGame && document.querySelector(".owner-card")');click('continue');shot('receipt-reloaded')
    assert page.evaluate('simbarGame.state.money')==cash and page.evaluate('simbarGame.journey.xp')==xp
    assert not report['errors'],report['errors']
    report['passed']=True;report['duration']=time.monotonic()-started
    (out/'run.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(report,ensure_ascii=False),flush=True)
    context.close();browser.close()
