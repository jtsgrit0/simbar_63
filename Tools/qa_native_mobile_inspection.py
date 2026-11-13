"""Compatibility CLI for the approved protected-venue UI checks.

Run with uv --with playwright --with pillow. Earlier landscape helpers remain
below for historical fixtures; the command now verifies the user's stricter
sign/stage protection rules instead of requiring centered overlays.
"""
import argparse
from functools import partial
from http.server import ThreadingHTTPServer
import json
from pathlib import Path
import threading

from playwright.sync_api import sync_playwright
from benchmark_web_loading import chromium_executable
from qa_native_owner_venues import QuietHandler

ROOT = Path(__file__).resolve().parent.parent
LANDSCAPES = [(1660,624),(1600,900),(844,390),(915,412),(667,375)]

MEDIA_CAPTURE = r'''() => {
  window.qaMedia=[];const NativeAudio=window.Audio;
  window.Audio=function(...args){const media=new NativeAudio(...args);qaMedia.push(media);return media;};
  window.Audio.prototype=NativeAudio.prototype;
}'''

SNAPSHOT = r'''() => {const g=simbarGame;return {
  elapsed:g.elapsed,secondsLeft:g.journey.secondsLeft,fever:g.journey.feverSeconds,
  festival:structuredClone(g.journey.festival),inspection:structuredClone(g.journey.inspection),
  orders:structuredClone(g.orders),pour:structuredClone(g.pour),requestTimer:g._requestTimer,
  reaction:structuredClone(g.reaction),messageTimer:g._messageTimer,paused:g.paused,pauseReason:g.pauseReason,
  selectedItem:g.selectedItem,lastResult:structuredClone(g.lastResult),
  actors:g.actors.map(a=>({id:a.id,x:a.x,z:a.z,y:a.y,visible:a.visible})),
  band:simbarApplication.world.getBandPresentation(),dialog:!!document.querySelector('.order-dialog'),
};}'''


def close_order(page):
    # Core actions finish synchronously; their old dialog is replaced on RAF.
    # Let that render settle before deciding whether a menu still needs closing.
    page.evaluate('() => new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)))')
    if page.locator('.order-dialog').is_visible():
        page.locator('.order-dialog [data-action="ui:close"]').first.click()
        page.wait_for_function('!document.querySelector(".order-dialog")')


def central_dialog(page,width,height):
    page.locator('.order-dialog').wait_for(state='visible')
    data=page.locator('.order-dialog').evaluate('''e=>{const r=e.getBoundingClientRect();return{x:r.x,y:r.y,w:r.width,h:r.height,overflow:e.scrollWidth>e.clientWidth};}''')
    assert abs(data['x']+data['w']/2-width/2)<4 and abs(data['y']+data['h']/2-height/2)<4,data
    assert data['x']>=0 and data['y']>=0 and data['x']+data['w']<=width+1 and data['y']+data['h']<=height+1,data
    assert not data['overflow'],data
    assert page.locator('.order-dialog [data-action="menuItem"]').count()==6
    assert page.locator('.order-dialog [data-action="prepare"]').count()==1
    return data


def launch(page, owner):
    page.wait_for_function('window.simbarGameReadyAt>0')
    page.locator('.owner-card').nth(owner).click()
    page.locator('[data-action="start"]').click()
    page.locator('[data-desk-channel="monitor"]').click()
    page.locator('.desk-show-card:not([disabled])').first.click()
    page.locator('.desk-computer-pane [data-action="deskWork"]').click()
    page.locator('[data-desk-channel="phone"]').click()
    page.locator('.desk-mobile-pane [data-action="deskWork"]').click()
    page.locator('[data-action="launch"]').click()
    while page.evaluate('simbarGame.phase==="guide"'):
        page.locator('[data-action="guide"]').click()
    page.wait_for_function('simbarGame.playing')
    page.evaluate(r'''() => {const g=simbarGame;window.qaTick=g.tick.bind(g);window.qaTickCalls=0;window.qaRealtime=false;
      g.tick=dt=>{qaTickCalls++;if(qaRealtime)qaTick(dt);};
      window.qaAdvance=seconds=>{for(let left=seconds;left>1e-8;left-=.05)qaTick(Math.min(.05,left));};
      window.qaDraws=[];const w=simbarApplication.world,render=w.render;
      w.render=function(...args){qaDraws=[];return render.apply(w,args);};
      const draw=CanvasRenderingContext2D.prototype.drawImage;
      CanvasRenderingContext2D.prototype.drawImage=function(image,...args){
        if(this.canvas.id==='unity-canvas'&&image?.src&&/\/(inspectors|venue-features|band-freewill|band-july)\.png$/.test(image.src))qaDraws.push({file:image.src.split('/').at(-1),args});
        return draw.call(this,image,...args);
      };
    }''')
    # Initial orders may immediately open the automatic central menu as the
    # guide ends. Close through the real control before inspecting the HUD.
    page.locator('.order-dialog').wait_for(state='visible');close_order(page)
    page.wait_for_function('simbarGame.playing&&!simbarGame.paused')


def service_flow(page,out,owner):
    # Use the real order-arrival function to produce a known food request;
    # actual menu/prepare/numeric/stop inputs must perform the entire service.
    page.evaluate('''() => {const g=simbarGame;g.orders=[];g._routeOrders();const a=g.actors.find(a=>g._orderGuest(a));g._assignOrder(a.id,5);}''')
    page.locator('.order-dialog').wait_for(state='visible')
    assert page.evaluate('simbarGame.paused&&simbarGame.pauseReason==="menu"')
    auto=central_dialog(page,1600,900)
    page.screenshot(path=str(out/f'owner-{owner}-automatic-food-order.png'))
    close_order(page);page.wait_for_timeout(150)
    assert not page.locator('.order-dialog').is_visible(),'The same front order reopened after dismissal'
    page.keyboard.press('m');central_dialog(page,1600,900)
    mistakes=page.evaluate('simbarGame.journey.mistakes')
    page.locator('.order-dialog [data-action="menuItem"]').first.click()
    assert page.evaluate('!simbarGame.pour&&simbarGame.journey.mistakes')==mistakes+1,'Wrong menu selection did not register a mistake'
    page.evaluate('qaAdvance(.2)');page.locator('.order-dialog').wait_for(state='visible')
    central_dialog(page,1600,900)
    page.locator('.order-dialog [data-action="prepare"]').click()
    page.wait_for_function('!!simbarGame.pour&&!simbarGame.paused&&!document.querySelector(".order-dialog")')
    assert page.evaluate('simbarGame.pour.item')==5
    finish_pour(page)
    food=page.evaluate('({result:simbarGame.lastResult,orders:simbarGame.journey.orders})')
    assert food['result']['success'] and food['orders']>=1,food
    close_order(page);page.evaluate('qaAdvance(.2)')
    page.evaluate('''() => {const g=simbarGame;const a=g.actors.find(a=>g._orderGuest(a)&&!g.orders.some(o=>o.id===a.id));g._assignOrder(a.id,0);}''')
    page.locator('.order-dialog').wait_for(state='visible')
    page.keyboard.press('1');page.wait_for_function('!!simbarGame.pour&&!simbarGame.paused')
    assert page.evaluate('simbarGame.pour.item')==0
    finish_pour(page,keyboard=True);close_order(page)
    return {'automaticPopup':auto,'wrongItemRecovered':True,'food':food,'numericShortcut':page.evaluate('simbarGame.lastResult')}


def finish_pour(page,keyboard=False):
    page.evaluate('''() => {const p=simbarGame.pour;qaAdvance(Math.max(.16,(p.target-.04)/.82-p.elapsed));}''')
    if keyboard:page.keyboard.press('Space')
    else:page.locator('[data-action="pourStop"].pour-stop').click()
    page.wait_for_function('!simbarGame.pour')


def orientation_cycle(page,out,owner,label,expect_paused=False):
    # Re-enable genuine requestAnimationFrame-driven simulation here. Counting
    # every tick invocation catches a broken frame gate even in paused menus.
    page.evaluate('qaRealtime=true');page.wait_for_timeout(100)
    page.set_viewport_size({'width':390,'height':844})
    page.wait_for_function('simbarApplication.orientationBlocked===true&&simbarApplication.root.dataset.orientation==="portrait"')
    page.locator('.orientation-gate').wait_for(state='visible');page.wait_for_timeout(80)
    gate=page.locator('.orientation-gate').bounding_box()
    assert gate and abs(gate['x'])<1 and abs(gate['y'])<1 and gate['width']>=389 and gate['height']>=843,gate
    assert '가로' in page.locator('.orientation-gate').inner_text()
    before=page.evaluate(SNAPSHOT);calls=page.evaluate('qaTickCalls')
    assert before['paused']==expect_paused,before['pauseReason']
    page.wait_for_timeout(350)
    for key in ['m','e','1','Space','p','Escape','h','c','f']:page.keyboard.press(key)
    page.evaluate('''() => {for(const selector of ['.menu-ready','.festival-actions button','[data-action="pourStop"].pour-stop','[data-action="pause"]','.order-dialog [data-action="prepare"]','.order-dialog [data-action="ui:close"]'])document.querySelector(selector)?.click();}''')
    page.wait_for_timeout(100)
    assert page.evaluate(SNAPSHOT)==before,f'{label}: portrait input or a frame changed frozen game state'
    assert page.evaluate('qaTickCalls')==calls,f'{label}: portrait still called the game tick'
    media=page.evaluate('(window.qaMedia||[]).map(a=>({paused:a.paused,currentTime:a.currentTime}))')
    assert all(a['paused'] for a in media),f'{label}: music continued in portrait'
    page.screenshot(path=str(out/f'owner-{owner}-portrait-{label}.png'))
    page.set_viewport_size({'width':1600,'height':900})
    page.wait_for_function('simbarApplication.orientationBlocked===false&&simbarApplication.root.dataset.orientation==="landscape"')
    page.locator('.orientation-gate').wait_for(state='hidden');page.wait_for_timeout(200)
    page.evaluate('qaRealtime=false');after=page.evaluate(SNAPSHOT)
    assert after['paused']==expect_paused,f'{label}: rotating changed the existing pause state'
    delta=after['elapsed']-before['elapsed']
    if expect_paused:assert delta==0,f'{label}: rotating resumed a manual/menu pause'
    else:assert 0<delta<.55,f'{label}: landscape did not resume smoothly: {delta}'
    return {'portraitFrozenExactly':True,'blockedInput':True,'musicSuspended':all(a['paused'] for a in media),'resumeDelta':delta,'keptPause':expect_paused,'gate':gate}


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--package',type=Path)
    parser.add_argument('--url')
    parser.add_argument('--output',type=Path,default=ROOT/'Temp/MobileInspection-20261114/source')
    args=parser.parse_args();out=args.output.resolve();out.mkdir(parents=True,exist_ok=True)
    server=None
    if args.url:url=args.url
    else:
        directory=args.package.resolve() if args.package else ROOT
        QuietHandler.source_html=None if args.package else (ROOT/'Tools/WebGLTemplate/FastGame/index.html').read_text(encoding='utf-8').replace('__SIMBAR_ASSET_HINTS__','').replace('__SIMBAR_STYLESHEET__','/Tools/WebGLTemplate/style.css').replace('__SIMBAR_RUNTIME__','/Tools/WebGLTemplate/FastGame').replace('href="TemplateData/favicon.ico"','href="data:,"')
        server=ThreadingHTTPServer(('127.0.0.1',0),partial(QuietHandler,directory=str(directory)))
        threading.Thread(target=server.serve_forever,daemon=True).start()
        url=f'http://127.0.0.1:{server.server_port}/'+('' if args.package else '__owner-venues-qa__/index.html')
    report={'passed':False,'url':url,'errors':[],'httpErrors':[],'owners':[]}
    try:
        with sync_playwright() as pw:
            browser=pw.chromium.launch(executable_path=chromium_executable(None))
            try:
                for owner in range(3):
                    context=browser.new_context(viewport={'width':390,'height':844},locale='ko-KR')
                    context.add_init_script('('+MEDIA_CAPTURE+')()');page=context.new_page()
                    page.on('pageerror',lambda e:report['errors'].append(str(e)))
                    page.on('response',lambda r:report['httpErrors'].append({'url':r.url,'status':r.status}) if r.status>=400 else None)
                    page.goto(url);page.wait_for_function('window.simbarGameReadyAt>0&&simbarApplication.orientationBlocked===true')
                    page.locator('.orientation-gate').wait_for(state='visible')
                    initial=page.evaluate('({phase:simbarGame.phase,owner:simbarGame.owner,elapsed:simbarGame.elapsed})')
                    page.evaluate('document.querySelectorAll(".owner-card")[2].click()');page.keyboard.press('m')
                    assert page.evaluate('({phase:simbarGame.phase,owner:simbarGame.owner,elapsed:simbarGame.elapsed})')==initial
                    page.screenshot(path=str(out/f'owner-{owner}-portrait-boot.png'))
                    page.set_viewport_size({'width':1600,'height':900})
                    page.wait_for_function('simbarApplication.orientationBlocked===false');launch(page,owner)
                    item={'owner':owner,'viewports':[]};report['owners'].append(item)
                    for width,height in LANDSCAPES:
                        page.set_viewport_size({'width':width,'height':height});page.wait_for_timeout(150)
                        assert not page.locator('.orientation-gate').is_visible()
                        if page.locator('.quest-toggle').get_attribute('aria-expanded')!='true':page.locator('.quest-toggle').click()
                        assert page.locator('.quest-body').is_visible()
                        data=page.evaluate('''() => {const box=s=>{const r=document.querySelector(s).getBoundingClientRect();return{x:r.x,y:r.y,w:r.width,h:r.height};};
                          return{upgrade:box('[data-action="ui:upgrades"]'),event:box('[data-action="ui:events"]'),
                            menu:box('.menu-ready'),mood:box('.mood'),live:box('.live-console'),quest:document.querySelector('.quest-toggle').innerText,
                            questExpanded:document.querySelector('.quest-toggle').getAttribute('aria-expanded'),bounds:simbarApplication.world.getVenueBounds(),
                            font:getComputedStyle(document.querySelector('.festival-actions button')).fontSize,
                            serviceConsole:document.querySelectorAll('.service-console').length,
                            overflow:document.documentElement.scrollWidth>innerWidth};}''')
                        assert abs(data['upgrade']['w']-data['event']['w'])<1 and abs(data['upgrade']['h']-data['event']['h'])<1,data
                        assert '퀘스트 목록' in data['quest'] and '웨스트' not in data['quest'],data
                        assert data['questExpanded']=='true' and data['serviceConsole']==0,data
                        assert float(data['font'].removesuffix('px'))>=12,data
                        assert not data['overflow'],data
                        for key in ['upgrade','event','menu','mood','live']:
                            b=data[key];assert b['x']>=0 and b['y']>=0 and b['x']+b['w']<=width+1 and b['y']+b['h']<=height+1,data
                        assert data['menu']['x']+data['menu']['w']<=data['mood']['x']+1,data
                        assert data['live']['y']+data['live']['h']<=data['mood']['y']+1,data
                        assert data['bounds']['right']<=data['live']['x']-12 and data['bounds']['left']>=16,'Venue/stage extends behind the right console: '+str(data)
                        if owner==0 and width==844:
                            before=page.eval_on_selector('.festival-actions','e=>{e.scrollTop=e.scrollHeight;return e.scrollTop;}')
                            if before>0:
                                page.locator('.festival-actions button').last.click()
                                page.wait_for_timeout(100)
                                assert page.eval_on_selector('.festival-actions','e=>e.scrollTop')>=before-1,'Crowd action selection reset the phone list scroll'
                            page.eval_on_selector('.festival-actions','e=>e.scrollTop=0')
                        page.screenshot(path=str(out/f'owner-{owner}-{width}x{height}-actions.png'))
                        page.locator('[data-action="ui:liveTab"]').filter(has_text='재생').click()
                        page.screenshot(path=str(out/f'owner-{owner}-{width}x{height}-playlist.png'))
                        assert page.locator('.track-list button').count()==5
                        data['playlist']=page.locator('.track-list').evaluate('''e=>({height:e.clientHeight,scrollHeight:e.scrollHeight,rows:[...e.querySelectorAll('button')].map(b=>{const r=b.getBoundingClientRect(),p=e.getBoundingClientRect();return{inside:r.top>=p.top-1&&r.bottom<=p.bottom+1,font:getComputedStyle(b).fontSize};})})''')
                        if width>=1600:
                            assert data['playlist']['scrollHeight']<=data['playlist']['height']+1 and all(r['inside'] for r in data['playlist']['rows']),data['playlist']
                        page.locator('[data-action="ui:liveTab"]').filter(has_text='호응').click()
                        page.locator('.menu-ready').click();data['orderDialog']=central_dialog(page,width,height)
                        page.screenshot(path=str(out/f'owner-{owner}-{width}x{height}-central-order.png'))
                        close_order(page)
                        item['viewports'].append(data)
                    page.set_viewport_size({'width':1600,'height':900})
                    item['service']=service_flow(page,out,owner)
                    page.evaluate('''() => {simbarGame.state.upgrades=[3,3,3];simbarGame.state.activeEvent=2;simbarGame.state.eventRemaining=120;}''')
                    page.wait_for_timeout(200)
                    item['features']=page.evaluate('simbarApplication.world.getVenueFeaturePresentation()')
                    item['featureDraws']=page.evaluate('qaDraws.filter(d=>d.file==="venue-features.png")')
                    assert len(item['features'])==4 and len(item['featureDraws'])==4,item
                    page.screenshot(path=str(out/f'owner-{owner}-features.png'))
                    if not page.evaluate('!!simbarGame.journey.festival.activeId'):
                        assert page.evaluate('simbarGame.act("festival",{id:"horns"}).success')
                    assert page.evaluate('simbarGame.act("inspection").success')
                    page.evaluate('qaAdvance(8)');page.wait_for_timeout(200)
                    item['inspection']=page.evaluate('''() => ({view:simbarGame.inspection,officers:simbarApplication.world.getInspectorPresentation(),band:simbarApplication.world.getBandPresentation(),audience:simbarApplication.world.getAudiencePresentation(),draws:qaDraws.filter(d=>d.file==='inspectors.png')})''')
                    assert item['inspection']['view']['officersPresent']==3,item['inspection']['view']
                    assert len(item['inspection']['draws'])==3,item['inspection']['draws']
                    assert all(a['action']=='perform' for a in item['inspection']['band'])
                    assert all(a['action']=='idle' and a['festivalAction'] is None for a in item['inspection']['audience'])
                    page.locator('.festival-actions button').first.click()
                    page.wait_for_timeout(100)
                    assert page.evaluate('simbarGame.lastResult.code')=='inspection_blocked'
                    assert page.locator('.venue-ui .toast').inner_text().strip()=='단속반이 지나가면 가능합니다.'
                    page.screenshot(path=str(out/f'owner-{owner}-inspection.png'))
                    page.keyboard.press('e');page.wait_for_function('!!simbarGame.pour')
                    assert page.evaluate('simbarGame.journey.festival.remaining>0&&simbarGame.inspection.active')
                    item['orientationLive']=orientation_cycle(page,out,owner,'live-festival-inspection-pour')
                    assert page.evaluate('!!simbarGame.pour&&simbarGame.inspection.active')
                    finish_pour(page)
                    page.evaluate('qaAdvance(1)')
                    band_frames=[]
                    for _ in range(8):
                        page.wait_for_timeout(100)
                        band_frames.append(page.evaluate('simbarApplication.world.getBandPresentation()'))
                    assert all(all(a['action']=='perform' for a in frame) for frame in band_frames)
                    assert len({tuple(a['column'] for a in frame) for frame in band_frames})>1,'Inspection stopped the band animation'
                    assert all(a['action']=='idle' and a['festivalAction'] is None for a in page.evaluate('simbarApplication.world.getAudiencePresentation()'))
                    page.evaluate('qaAdvance(45)');page.wait_for_timeout(100);close_order(page)
                    item['resumed']=page.evaluate('''() => ({view:simbarGame.inspection,band:simbarApplication.world.getBandPresentation()})''')
                    assert item['resumed']['view']['phase']=='done' and all(a['action']=='perform' for a in item['resumed']['band'])
                    page.keyboard.press('p');page.wait_for_function('simbarGame.paused');page.wait_for_timeout(100)
                    manual_reason=page.evaluate('simbarGame.pauseReason')
                    item['orientationManualPause']=orientation_cycle(page,out,owner,'manual-pause',expect_paused=True)
                    assert page.evaluate('simbarGame.pauseReason')==manual_reason
                    paused_time=page.evaluate('({elapsed:simbarGame.elapsed,secondsLeft:simbarGame.journey.secondsLeft})')
                    page.locator('.pause-ref [data-action="ui:settings"]').click()
                    page.locator('.settings-ref').wait_for(state='visible')
                    assert page.evaluate('simbarGame.paused') and page.evaluate('simbarGame.pauseReason')==manual_reason
                    page.screenshot(path=str(out/f'owner-{owner}-paused-settings.png'))
                    page.locator('.settings-ref [data-action="ui:close"]').click()
                    page.locator('.settings-ref').wait_for(state='hidden')
                    page.keyboard.press('1');page.keyboard.press('e');page.wait_for_timeout(100)
                    assert page.evaluate('simbarGame.paused&&!simbarGame.pour')
                    assert page.evaluate('simbarGame.pauseReason')==manual_reason
                    assert page.evaluate('({elapsed:simbarGame.elapsed,secondsLeft:simbarGame.journey.secondsLeft})')==paused_time
                    item['pausedSettings']={'pauseReasonPreserved':True,'shortcutsStayedPaused':True,'clockUnchanged':True}
                    page.keyboard.press('p');page.wait_for_function('!simbarGame.paused')
                    page.locator('.menu-ready').click();central_dialog(page,1600,900)
                    item['orientationMenu']=orientation_cycle(page,out,owner,'central-menu',expect_paused=True)
                    assert page.locator('.order-dialog').is_visible()
                    close_order(page);assert page.evaluate('!simbarGame.paused')
                    context.close()
                assert not report['errors'] and not report['httpErrors'],report
                report['passed']=True
            finally:browser.close()
    finally:
        (out/'report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
        if server:server.shutdown()
    print(json.dumps({'passed':report['passed'],'report':str(out/'report.json'),'owners':len(report['owners']),'errors':report['errors'],'httpErrors':report['httpErrors']},ensure_ascii=False))


if __name__=='__main__':
    from qa_native_protected_ui import main as protected_main
    protected_main()
