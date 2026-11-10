"""Exercise live crowd controls and venue props through an isolated browser."""
import argparse
import base64
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
import json
from pathlib import Path
import threading

from playwright.sync_api import sync_playwright
from benchmark_web_loading import chromium_executable

ROOT = Path(__file__).resolve().parent.parent
ACTIONS = ['slam','mosh','surf','jump','horns','airguitar','sing','encore','headbang','dance']
INSTRUMENT = r'''() => {
  const AudioClass=window.Audio;window.qaAudios=[];
  window.Audio=function(...args){const a=new AudioClass(...args);qaAudios.push(a);return a;};
  window.Audio.prototype=AudioClass.prototype;Object.setPrototypeOf(window.Audio,AudioClass);
  const draw=CanvasRenderingContext2D.prototype.drawImage;window.qaDraws=[];
  CanvasRenderingContext2D.prototype.drawImage=function(image,...args){
    if(this.canvas.id==='unity-canvas'&&image?.src&&/festival-|freebird-arch|freebird-wall-table|cocktail-glass/.test(image.src)){
      qaDraws.push({file:image.src.split('/').pop(),args});if(qaDraws.length>3000)qaDraws.splice(0,1000);
    }
    return draw.call(this,image,...args);
  };
}'''
SNAPSHOT = '''() => ({festival:simbarGame.festival,owner:simbarGame.owner,elapsed:simbarGame.elapsed,
  actors:simbarGame.actors.filter(a=>a.audienceVersion).map(a=>({id:a.id,role:a.role,x:a.x,z:a.z,y:a.y,homeX:a.homeX,homeZ:a.homeZ,action:a.action,behavior:a.behavior,seat:a.seat,waitingSeat:a.waitingSeat,seatReleased:a.seatReleased,seatStageReached:a.seatStageReached,tableId:a.tableId,festivalAction:a.festivalAction,festivalLead:a.festivalLead,festivalSupport:a.festivalSupport,festivalLiftReady:a.festivalLiftReady})),
  band:simbarApplication.world.getBandPresentation(),draws:qaDraws.slice(-300),
  audio:qaAudios.at(-1)?{src:qaAudios.at(-1).src,paused:qaAudios.at(-1).paused,readyState:qaAudios.at(-1).readyState,currentTime:qaAudios.at(-1).currentTime}:null})'''

class Quiet(SimpleHTTPRequestHandler):
    def log_message(self,*_args): pass

def launch(page,owner):
    page.wait_for_function('window.simbarGameReadyAt>0&&document.querySelectorAll(".owner-card").length===3')
    page.evaluate('''() => {const w=simbarApplication.world,render=w.render;w.render=function(...args){window.qaDraws=[];return render.apply(this,args);};}''')
    page.locator('.owner-card').nth(owner).click()
    page.locator('[data-action="start"]').click()
    page.locator('[data-desk-channel="monitor"]').click()
    page.locator('.desk-show-card:not([disabled])').first.click()
    page.locator('.desk-computer-pane [data-action="deskWork"]').click()
    page.locator('[data-desk-channel="phone"]').click()
    page.locator('.desk-mobile-pane [data-action="deskWork"]').click()
    page.locator('[data-action="launch"]').click()
    while page.evaluate('simbarGame.phase')=='guide':page.locator('[data-action="guide"]').click()
    page.wait_for_function('simbarGame.playing&&!simbarGame.paused')
    page.locator('[data-action="ui:liveTab"][data-payload*="music"]').click()
    page.locator('[data-playlist="0"]').click()
    page.wait_for_function('qaAudios.at(-1)?.readyState>=2&&!qaAudios.at(-1).paused&&qaAudios.at(-1).currentTime>0')
    page.locator('[data-action="ui:liveTab"][data-payload*="festival"]').click()
    page.wait_for_function('document.querySelectorAll("[data-festival-action]").length===10')

def advance(page,seconds):
    page.evaluate('''seconds=>{for(let t=0;t<seconds;t+=.1){simbarGame.tick(.1);if(simbarGame.reaction?.activeKind)simbarGame.act('quick',{kind:simbarGame.reaction.activeKind});}}''',seconds)
    page.wait_for_timeout(100)

def canvas(page,path):
    data=page.eval_on_selector('#unity-canvas','c=>c.toDataURL("image/png").split(",")[1]')
    path.write_bytes(base64.b64decode(data))

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--package',type=Path,default=ROOT/'docs')
    parser.add_argument('--url')
    parser.add_argument('--output',type=Path,default=ROOT/'Temp/Festival-20261110/qa')
    args=parser.parse_args();out=args.output.resolve();out.mkdir(parents=True,exist_ok=True)
    server=thread=None
    if args.url:url=args.url
    else:
        server=ThreadingHTTPServer(('127.0.0.1',0),partial(Quiet,directory=str(args.package.resolve())))
        thread=threading.Thread(target=server.serve_forever,daemon=True);thread.start();url=f'http://127.0.0.1:{server.server_port}/'
    report={'passed':False,'url':url,'errors':[],'consoleErrors':[],'httpErrors':[],'owners':[]}
    try:
        with sync_playwright() as pw:
            browser=pw.chromium.launch(executable_path=chromium_executable(None),args=['--disable-gpu'])
            try:
                for owner in range(3):
                    context=browser.new_context(viewport={'width':1920,'height':1080},locale='ko-KR')
                    context.add_init_script('('+INSTRUMENT+')()');page=context.new_page()
                    page.on('pageerror',lambda e:report['errors'].append(str(e)))
                    page.on('console',lambda m:report['consoleErrors'].append(m.text) if m.type=='error' else None)
                    page.on('response',lambda r:report['httpErrors'].append({'url':r.url,'status':r.status}) if r.status>=400 else None)
                    page.goto(url);launch(page,owner)
                    item={'owner':owner,'initial':page.evaluate(SNAPSHOT),'actions':[]}
                    if owner==0:
                        assert sum(bool(a.get('waitingSeat')) for a in item['initial']['actors'])==6
                        assert {'freebird-arch.png','freebird-wall-table.png'}<={d['file'] for d in item['initial']['draws']}
                    if owner==1:
                        assert sum(bool(a.get('tableId')) for a in item['initial']['actors'])==10
                        assert sum(d['file']=='cocktail-glass.png' for d in item['initial']['draws'])==10
                    if owner==2:
                        assert not any(d['file'] in {'freebird-arch.png','freebird-wall-table.png','cocktail-glass.png'} for d in item['initial']['draws'])
                    page.screenshot(path=str(out/f'venue-{owner}-waiting.png'));canvas(page,out/f'venue-{owner}-waiting-world.png')
                    assert page.locator('[data-festival-action]').count()==10
                    panel=page.locator('.live-console').bounding_box()
                    for button in page.locator('[data-festival-action]').all():
                        b=button.bounding_box();assert b['x']>=panel['x'] and b['y']>=panel['y'] and b['x']+b['width']<=panel['x']+panel['width'] and b['y']+b['height']<=panel['y']+panel['height']
                    for action in (ACTIONS if owner==0 else ['horns','dance']):
                        page.locator(f'[data-festival-action="{action}"]').click()
                        page.wait_for_function('id=>simbarGame.festival.activeId===id',arg=action)
                        if action=='surf':
                            page.evaluate('''() => {for(let i=0;i<65&&!simbarGame.actors.some(a=>a.festivalLead&&a.festivalLiftReady);i++)simbarGame.tick(.1);}''')
                            assert page.evaluate('simbarGame.actors.some(a=>a.festivalLead&&a.festivalLiftReady)'),'No physical surfing support formation'
                            advance(page,1.3)
                        page.wait_for_timeout(220);state=page.evaluate(SNAPSHOT)
                        assert state['festival']['liveAttendance']<=state['festival']['capacity']
                        assert state['festival']['orderMultiplier']<=1
                        if not item['actions']:assert state['festival']['orderMultiplier']<1
                        assert any(a.get('festivalAction')==action for a in state['actors'])
                        if action=='surf':
                            leader=next(a for a in state['actors'] if a.get('festivalLead'))
                            supports=[a for a in state['actors'] if a.get('festivalSupport')]
                            assert leader['y']>.8 and len(supports)==3 and all((a['x']-leader['x'])**2+(a['z']-leader['z'])**2<.85**2 for a in supports)
                        item['actions'].append({'id':action,'festival':state['festival']})
                        if action in ('horns','surf'):
                            page.screenshot(path=str(out/f'venue-{owner}-{action}.png'));canvas(page,out/f'venue-{owner}-{action}-world.png')
                        advance(page,state['festival']['remaining']+.3)
                    item['final']=page.evaluate(SNAPSHOT)
                    if owner==0:
                        waiting={a['id'] for a in item['initial']['actors'] if a.get('waitingSeat')}
                        departed=[a for a in item['final']['actors'] if a['id'] in waiting]
                        assert len(departed)==6 and all(a.get('seatReleased') and a.get('seatStageReached') and a['role']=='guest' and not a.get('waitingSeat') for a in departed)
                        assert item['final']['festival']['liveAttendance']>item['initial']['festival']['liveAttendance']
                    page.keyboard.press('p');before=page.evaluate('structuredClone(simbarGame.journey.festival)')
                    page.wait_for_timeout(220);assert page.evaluate('simbarGame.journey.festival')==before
                    page.evaluate('simbarGame.save()');page.reload();page.wait_for_function('window.simbarGameReadyAt>0')
                    assert page.evaluate('simbarGame.journey.festival')==before
                    item['pauseAndReload']=True;report['owners'].append(item);context.close()
                assert not report['errors'] and not report['consoleErrors'] and not report['httpErrors'],report
                report['passed']=True
            finally:browser.close()
    finally:
        if server:server.shutdown();server.server_close();thread.join()
        (out/'browser-qa.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print(json.dumps({'passed':report['passed'],'owners':len(report['owners']),'output':str(out)}))

if __name__=='__main__':main()
