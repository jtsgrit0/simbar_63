"""Exercise responsive HUD, venue fixtures and inspection in a disposable browser."""
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
    page.wait_for_function('simbarGame.playing&&!simbarGame.paused')
    page.evaluate(r'''() => {const g=simbarGame;window.qaTick=g.tick.bind(g);g.tick=()=>{};
      window.qaAdvance=seconds=>{for(let i=0;i<seconds*20;i++)qaTick(.05);};
      window.qaDraws=[];const w=simbarApplication.world,render=w.render;
      w.render=function(...args){qaDraws=[];return render.apply(w,args);};
      const draw=CanvasRenderingContext2D.prototype.drawImage;
      CanvasRenderingContext2D.prototype.drawImage=function(image,...args){
        if(this.canvas.id==='unity-canvas'&&image?.src&&/\/(inspectors|venue-features|band-freewill|band-july)\.png$/.test(image.src))qaDraws.push({file:image.src.split('/').at(-1),args});
        return draw.call(this,image,...args);
      };
    }''')


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
                    context=browser.new_context(viewport={'width':1600,'height':900},locale='ko-KR');page=context.new_page()
                    page.on('pageerror',lambda e:report['errors'].append(str(e)))
                    page.on('response',lambda r:report['httpErrors'].append({'url':r.url,'status':r.status}) if r.status>=400 else None)
                    page.goto(url);launch(page,owner)
                    item={'owner':owner,'viewports':[]};report['owners'].append(item)
                    for width,height in [(1600,900),(844,390),(915,412),(390,844)]:
                        page.set_viewport_size({'width':width,'height':height});page.wait_for_timeout(150)
                        data=page.evaluate('''() => {const box=s=>{const r=document.querySelector(s).getBoundingClientRect();return{x:r.x,y:r.y,w:r.width,h:r.height};};
                          return{upgrade:box('[data-action="ui:upgrades"]'),event:box('[data-action="ui:events"]'),
                            quest:document.querySelector('.quest-toggle').innerText,bounds:simbarApplication.world.getVenueBounds(),
                            font:getComputedStyle(document.querySelector('.festival-actions button')).fontSize,
                            overflow:document.documentElement.scrollWidth>innerWidth};}''')
                        assert abs(data['upgrade']['w']-data['event']['w'])<1 and abs(data['upgrade']['h']-data['event']['h'])<1,data
                        assert '퀘스트 목록' in data['quest'] and '웨스트' not in data['quest'],data
                        assert float(data['font'].removesuffix('px'))>=12,data
                        assert not data['overflow'],data
                        for key in ['upgrade','event']:
                            b=data[key];assert b['x']>=0 and b['y']>=0 and b['x']+b['w']<=width+1 and b['y']+b['h']<=height+1,data
                        if width<height:assert data['bounds']['width']>width*.9 and data['bounds']['bottom']<height-420,data
                        if owner==0 and width==844:
                            before=page.eval_on_selector('.festival-actions','e=>{e.scrollTop=e.scrollHeight;return e.scrollTop;}')
                            assert before>0
                            page.locator('.festival-actions button').last.click()
                            page.wait_for_timeout(100)
                            assert page.eval_on_selector('.festival-actions','e=>e.scrollTop')>=before-1,'Crowd action selection reset the phone list scroll'
                            page.eval_on_selector('.festival-actions','e=>e.scrollTop=0')
                        page.screenshot(path=str(out/f'owner-{owner}-{width}x{height}-actions.png'))
                        page.locator('[data-action="ui:liveTab"]').filter(has_text='재생').click()
                        page.screenshot(path=str(out/f'owner-{owner}-{width}x{height}-playlist.png'))
                        assert page.locator('.track-list button').count()==5
                        page.locator('[data-action="ui:liveTab"]').filter(has_text='호응').click()
                        item['viewports'].append(data)
                    page.set_viewport_size({'width':1600,'height':900})
                    page.evaluate('''() => {simbarGame.state.upgrades=[3,3,3];simbarGame.state.activeEvent=2;simbarGame.state.eventRemaining=120;}''')
                    page.wait_for_timeout(200)
                    item['features']=page.evaluate('simbarApplication.world.getVenueFeaturePresentation()')
                    item['featureDraws']=page.evaluate('qaDraws.filter(d=>d.file==="venue-features.png")')
                    assert len(item['features'])==4 and len(item['featureDraws'])==4,item
                    page.screenshot(path=str(out/f'owner-{owner}-features.png'))
                    assert page.evaluate('simbarGame.act("inspection").success')
                    page.evaluate('qaAdvance(8)');page.wait_for_timeout(200)
                    item['inspection']=page.evaluate('''() => ({view:simbarGame.inspection,officers:simbarApplication.world.getInspectorPresentation(),band:simbarApplication.world.getBandPresentation(),audience:simbarApplication.world.getAudiencePresentation(),draws:qaDraws.filter(d=>d.file==='inspectors.png')})''')
                    assert item['inspection']['view']['officersPresent']==3,item['inspection']['view']
                    assert len(item['inspection']['draws'])==3,item['inspection']['draws']
                    assert all(a['action']=='idle' for a in item['inspection']['band'])
                    assert all(a['action']=='idle' and a['festivalAction'] is None for a in item['inspection']['audience'])
                    page.locator('.festival-actions button').first.click()
                    page.wait_for_timeout(100)
                    assert page.evaluate('simbarGame.lastResult.code')=='inspection_blocked'
                    assert '단속반이 지나가면' in page.locator('.venue-ui .toast').inner_text()
                    page.screenshot(path=str(out/f'owner-{owner}-inspection.png'))
                    before=page.evaluate('simbarApplication.world.getBandPresentation()')
                    page.evaluate('qaAdvance(1)');page.wait_for_timeout(200)
                    assert page.evaluate('simbarApplication.world.getBandPresentation()')==before
                    page.evaluate('qaAdvance(45)');page.wait_for_timeout(100)
                    item['resumed']=page.evaluate('''() => ({view:simbarGame.inspection,band:simbarApplication.world.getBandPresentation()})''')
                    assert item['resumed']['view']['phase']=='done' and all(a['action']=='perform' for a in item['resumed']['band'])
                    context.close()
                assert not report['errors'] and not report['httpErrors'],report
                report['passed']=True
            finally:browser.close()
    finally:
        (out/'report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
        if server:server.shutdown()
    print(json.dumps({'passed':report['passed'],'report':str(out/'report.json'),'owners':len(report['owners']),'errors':report['errors'],'httpErrors':report['httpErrors']},ensure_ascii=False))


if __name__=='__main__':main()
