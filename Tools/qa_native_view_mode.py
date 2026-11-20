"""Actual browser integration for the approved view/management UI mode.

Use disposable contexts and a local WAV transport fixture; all Supabase/R2
network requests stay local or are denied. No product save or remote writes.
"""
import argparse,base64,hashlib,io,json,math,re,struct,traceback,wave
from pathlib import Path
from playwright.sync_api import sync_playwright
import qa_native_audio_feedback as shared

ROOT=Path(__file__).resolve().parent.parent
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--package',type=Path)
parser.add_argument('--output',type=Path,default=ROOT/'Temp/ViewModeApply-20261115/qa')
parser.add_argument('--owners',type=int,nargs='+',choices=[0,1,2],default=[0,1,2])
parser.add_argument('--visibility-only',action='store_true',help='Rerun only the dynamic HUD text/visibility fix and restoration scope.')
args=parser.parse_args();OUT=args.output.resolve();OUT.mkdir(parents=True,exist_ok=True)
report={'passed':False,'scope':'Actual new UI in six disposable owner/viewport sessions. Native audio transport uses a local WAV at unchanged R2 URL; all statistics requests are denied. Closing fixture tests UI availability without replaying a whole game night.','cases':[],'errors':[],'localHTTPFailures':[],'blockedRemote':[],'localMediaFixtures':[]}
if args.package:
 report['package']=str(args.package.resolve());report['runtime']=re.search(r'FastGame/([a-f0-9]{16})/',(args.package/'index.html').read_text(encoding='utf8'))[1]
SNAPSHOT=r'''() => {
 const host=document.querySelector('.fast-game'),g=simbarGame,card=host.querySelector('.live-console'),mode=host.querySelector('button[data-view-mode]'),tabs=[...card.querySelectorAll('.live-console-tabs button')],playlist=tabs.find(b=>b.dataset.payload?.includes('music')),crowd=tabs.find(b=>b!==playlist);
 const box=e=>{if(!e)return null;const r=e.getBoundingClientRect();return{x:r.x,y:r.y,width:r.width,height:r.height};},style=e=>{const s=getComputedStyle(e);return Object.fromEntries(['fontFamily','fontSize','fontWeight','fontStretch','letterSpacing','lineHeight','color','backgroundColor','borderRadius','borderTopWidth','borderRightWidth','borderBottomWidth','borderLeftWidth','borderTopColor','paddingTop','paddingRight','paddingBottom','paddingLeft'].map(k=>[k,s[k]]));};
 const visible=e=>{const s=getComputedStyle(e),r=e.getBoundingClientRect(),h=host.getBoundingClientRect();return s.display!=='none'&&s.visibility!=='hidden'&&+s.opacity>.01&&r.width>0&&r.height>0&&r.right>h.left&&r.left<h.right&&r.bottom>h.top&&r.top<h.bottom&&!e.closest('[hidden]');};
 return{owner:g.owner,phase:g.phase,paused:g.paused,elapsed:g.elapsed,secondsLeft:g.journey.secondsLeft,viewMode:host.dataset.viewMode,transition:host.dataset.viewModeTransition,card:box(card),cardStyle:style(card),playlist:box(playlist),playlistStyle:style(playlist),crowdVisibility:getComputedStyle(crowd).visibility,mode:{label:mode?.getAttribute('aria-label'),box:box(mode),src:mode?.querySelector('img')?.getAttribute('src')},camera:simbarApplication.world.getVenueBounds(),visibleButtons:[...host.querySelectorAll('.venue-ui button,.reference-ui button,.css-layer button')].filter(visible).map(e=>({action:e.dataset.action,viewMode:e.hasAttribute('data-view-mode'),insidePlaylist:!!e.closest('.live-console')})),leakingHiddenTargets:[...host.querySelectorAll('[aria-hidden="true"][inert]')].filter(e=>visible(e)&&e.textContent.trim()).map(e=>({className:e.className,text:e.textContent.trim(),box:box(e)})),tools:[...host.querySelectorAll('.venue-tools > button')].map(e=>({action:e.dataset.action,box:box(e)})),media:(window.viewAudio||[]).map(a=>({paused:a.paused,time:a.currentTime,readyState:a.readyState,ended:a.ended})),orders:g.orders.map(o=>o.id),pour:!!g.pour};
}'''
INIT=r'''() => {window.viewAudio=[];const NativeAudio=window.Audio;window.Audio=function(...args){const a=new NativeAudio(...args);viewAudio.push(a);return a;};window.Audio.prototype=NativeAudio.prototype;}'''

def wav():
 data=io.BytesIO()
 with wave.open(data,'wb') as output:
  output.setnchannels(1);output.setsampwidth(2);output.setframerate(8000)
  output.writeframes(b''.join(struct.pack('<h',int(900*math.sin(2*math.pi*220*i/8000))) for i in range(20*8000)))
 return data.getvalue()

WAV=wav()
def context_page(browser,url,width,height):
 context=browser.new_context(viewport={'width':width,'height':height},locale='ko-KR',device_scale_factor=1)
 context.add_init_script('('+INIT+')()')
 def remote(route):
  request=route.request
  if re.match(r'https://pub-[^/]+\.r2\.dev/.*\.mp3(?:\?.*)?$',request.url,re.I):
   report['localMediaFixtures'].append({'url':request.url,'method':request.method})
   route.fulfill(status=200,content_type='audio/wav',body=WAV,headers={'Accept-Ranges':'bytes','Cache-Control':'no-store'})
  else:report['blockedRemote'].append({'url':request.url,'method':request.method});route.abort()
 context.route(re.compile(r'^https?://(?!127\.0\.0\.1[:/]|localhost[:/])',re.I),remote)
 page=context.new_page();page.on('pageerror',lambda error:report['errors'].append(str(error)))
 page.on('response',lambda response:report['localHTTPFailures'].append({'url':response.url,'status':response.status}) if response.url.startswith('http://127.0.0.1') and response.status>=400 else None)
 page.goto(url,wait_until='domcontentloaded');page.wait_for_function('window.simbarGameReadyAt>0',timeout=45000);return context,page

def settle(page):shared.settle(page)
def snapshot(page):settle(page);return page.evaluate(SNAPSHOT)
def equal_geometry(a,b):
 for key in ['card','cardStyle','playlist','playlistStyle','camera']:assert a[key]==b[key],(key,a[key],b[key])
def wait_mode(page,mode):
 page.wait_for_function("mode=>{const h=document.querySelector('.fast-game');return h.dataset.viewMode===mode&&h.dataset.viewModeTransition==='settled'}",arg=mode,timeout=8000);settle(page)
def only_view_controls(state):
 assert sum(b['action']=='ui:settings' for b in state['visibleButtons'])==1,state['visibleButtons']
 assert state['visibleButtons'] and all(b['viewMode'] or b['insidePlaylist'] or b['action']=='ui:settings' for b in state['visibleButtons']),state['visibleButtons']
 assert state['crowdVisibility']=='hidden',state
 assert not state['leakingHiddenTargets'],state['leakingHiddenTargets']
def canvas_hash(page):return hashlib.sha256(base64.b64decode(page.eval_on_selector('#unity-canvas','c=>c.toDataURL("image/png").split(",")[1]'))).hexdigest()

def prepare(page,owner):
 page.evaluate('''owner=>{const g=simbarGame;window.qaOriginalTick=g.tick.bind(g);g.tick=()=>{};g.act('owner',{index:owner});g.act('language',{language:'ko'});g.journey.completedNights=2;g.journey.day=3;g.journey.tutorialComplete=true;g.state.upgrades[1]=1;g.act('start');g.act('selectShow',{showId:'double'});g.act('deskWork',{task:0});g.act('deskWork',{task:1});if(!g.act('launch').success)throw Error('launch failed');while(g.phase==='guide')g.act('guide');}''',owner)
 shared.close_order(page);page.locator('[data-action="ui:liveTab"][data-payload*="music"]').click();settle(page)
 page.wait_for_function('viewAudio.some(a=>!a.paused&&a.readyState>=3)')
 page.evaluate('''()=>{const w=simbarApplication.world;window.qaOriginalRender=w.render.bind(w);w.render=(_time,state)=>window.qaOriginalRender(1000,state);}''');settle(page)

def main_case(browser,url,owner,width,height):
 context,page=context_page(browser,url,width,height);item={'owner':owner,'viewport':[width,height]};report['cases'].append(item)
 try:
  prepare(page,owner);before=snapshot(page);item['before']=before
  assert before['mode']['label']=='감상모드',before['mode'];assert before['mode']['box']['width']==84 and before['mode']['box']['height']==84
  assert [b['action'] for b in before['tools'][:2]]==['ui:upgrades','ui:events'];assert len(before['tools'])==3,before['tools']
  original_canvas=canvas_hash(page);page.locator('button[data-view-mode]').click()
  item['entryAnimations']=page.evaluate('''()=>document.getAnimations().map(a=>({duration:a.effect?.getTiming().duration,easing:a.effect?.getTiming().easing,frames:a.effect?.getKeyframes().map(k=>({easing:k.easing,translate:k.translate,transform:k.transform}))}))''')
  assert any(a['duration']==360 and (a['easing']=='ease-out' or any(f.get('easing')=='ease-out' for f in a.get('frames') or [])) for a in item['entryAnimations']),item['entryAnimations']
  wait_mode(page,'viewing');view=snapshot(page);item['viewing']=view;equal_geometry(before,view);only_view_controls(view)
  assert view['mode']['label']=='경영모드';assert view['mode']['box']==before['tools'][0]['box'];assert view['paused']==before['paused'];assert canvas_hash(page)==original_canvas,'View mode changed frozen venue pixels'
  page.screenshot(path=str(OUT/f'owner-{owner}-{width}-viewing.png'))
  # Block all shortcuts that could activate hidden management/service/reaction UI.
  page.evaluate('''()=>{window.qaCoreActions=[];const g=simbarGame;window.qaOriginalAct=g.act.bind(g);g.act=(action,payload)=>{qaCoreActions.push(action);return qaOriginalAct(action,payload);};}''')
  page.locator('#unity-canvas').focus()
  for key in ['m','h','e','c','f','1','2','3','4','5','6','p','Escape','Space']:page.keyboard.press(key)
  item['blockedKeyboardCoreActions']=page.evaluate('qaCoreActions');assert item['blockedKeyboardCoreActions']==[],item['blockedKeyboardCoreActions'];page.evaluate('simbarGame.act=window.qaOriginalAct');only_view_controls(snapshot(page))
  # A new front order and the real stats callback must rebuild while staying hidden.
  page.evaluate('''()=>{window.qaOldCard=document.querySelector('.live-console');const g=simbarGame;if(!g.orders[0])throw Error('No front order fixture');g.orders[0]={...g.orders[0],id:g.orders[0].id+'-view-qa'};}''');page.wait_for_function('qaOldCard!==document.querySelector(".live-console")');after_order=snapshot(page);equal_geometry(view,after_order);only_view_controls(after_order)
  page.evaluate('''()=>{window.qaOldCard=document.querySelector('.live-console');window.dispatchEvent(new Event('online'));}''');page.wait_for_function('qaOldCard!==document.querySelector(".live-console")');after_stats=snapshot(page);equal_geometry(view,after_stats);only_view_controls(after_stats);item['rebuilds']={'frontOrder':True,'statsOnchange':True}
  # The retained native transport stays clickable, and rebuilds keep view mode.
  page.locator('[data-action="ui:musicToggle"]').click();page.wait_for_function('simbarGame.journey.musicPaused&&viewAudio.every(a=>a.paused)');page.locator('[data-action="ui:musicToggle"]').click();page.wait_for_function('!simbarGame.journey.musicPaused&&viewAudio.some(a=>!a.paused&&a.readyState>=3)');old_index=page.evaluate('simbarGame.state.playlist');page.locator('[data-action="ui:musicNext"]').click();page.wait_for_function('index=>simbarGame.state.playlist!==index',arg=old_index);page.wait_for_function('viewAudio.some(a=>!a.paused&&a.readyState>=3)');only_view_controls(snapshot(page));item['nativeMusicControls']=True
  # Resize and portrait use the game's existing orientation gate.
  page.set_viewport_size({'width':width-40,'height':height});settle(page);page.wait_for_timeout(80);only_view_controls(snapshot(page));page.set_viewport_size({'width':390,'height':844});page.wait_for_function('!document.querySelector(".orientation-gate").hidden');assert page.evaluate('simbarApplication.orientationBlocked');page.set_viewport_size({'width':width,'height':height});page.wait_for_function('document.querySelector(".orientation-gate").hidden');only_view_controls(snapshot(page));item['resizeAndPortrait']=True
  # Resume actual core/rAF for a short, real progression check.
  page.evaluate('''()=>{simbarGame.tick=window.qaOriginalTick;simbarApplication.world.render=window.qaOriginalRender;}''');time_before=snapshot(page);hash_before=canvas_hash(page);page.wait_for_timeout(300);time_after=snapshot(page);assert time_after['secondsLeft']<time_before['secondsLeft'] and time_after['elapsed']>time_before['elapsed'];assert not time_after['paused'];assert canvas_hash(page)!=hash_before;item['clockWorldContinue']={'secondsDelta':time_before['secondsLeft']-time_after['secondsLeft'],'elapsedDelta':time_after['elapsed']-time_before['elapsed']}
  page.evaluate('simbarGame.tick=()=>{}');page.locator('button[data-view-mode]').click();wait_mode(page,'management');shared.close_order(page);returned=snapshot(page);item['returned']=returned;equal_geometry(before,returned);assert returned['mode']['label']=='감상모드';assert returned['mode']['box']==before['mode']['box'];assert len(returned['visibleButtons'])>len(view['visibleButtons']);page.screenshot(path=str(OUT/f'owner-{owner}-{width}-management.png'))
  if owner==2 and width==1600:phase_checks(page,item)
  item['passed']=True
 finally:context.close()

def phase_checks(page,item):
 page.locator('button[data-view-mode]').click();wait_mode(page,'viewing')
 # A valid completed-aftershow fixture exposes the real manual farewell action.
 page.evaluate('''async()=>{const boot=document.querySelector('script[src$="/boot.js"]').src,{createAfterShow}=await import(new URL('aftershow.js',boot)),g=simbarGame,s=createAfterShow(g.actors,g.journey.session);for(const a of [...s.members,...s.audience])a.phase='gone';s.completed=true;s.noticeAcknowledged=false;g.journey.afterShow=s;g.journey.floorClosing=true;g.journey.inProgress=true;g.phase='closing';g.paused=true;g.pauseReason='farewell';}''');settle(page);assert page.evaluate('simbarGame.farewellPending');assert page.evaluate('document.querySelector(".fast-game").dataset.viewMode')=='viewing';assert page.locator('button[data-view-mode]').is_visible();only_view_controls(snapshot(page));page.locator('button[data-view-mode]').click();wait_mode(page,'management');page.locator('[data-action="farewellAck"]').wait_for(state='visible');page.locator('[data-action="farewellAck"]').click();settle(page);assert not page.evaluate('simbarGame.farewellPending');item['farewellManualReturn']=True
 if page.evaluate('simbarGame.phase')!='results':
  # Only the UI phase reset is under test; floor progression has separate tests.
  page.evaluate("()=>{const g=simbarGame;g.phase='results';g.journey.inProgress=false;g.paused=true;g.pauseReason='results';}")
 settle(page);assert page.evaluate('document.querySelector(".fast-game").dataset.viewMode')=='management';page.locator('[data-action="next"]').click();page.wait_for_function('simbarGame.phase==="planning"');assert page.evaluate('document.querySelector(".fast-game").dataset.viewMode')=='management';assert page.locator('.daydesk').is_visible();assert not page.locator('button[data-view-mode]').is_visible();item['resultsPlanningReset']=True

def visibility_case(browser,url,owner,width,height):
 context,page=context_page(browser,url,width,height);item={'owner':owner,'viewport':[width,height],'scope':'Dynamic text visibility, rebuilt HUD and resize, then restoration only'};report['cases'].append(item)
 try:
  prepare(page,owner);before=snapshot(page);page.locator('button[data-view-mode]').click();wait_mode(page,'viewing');view=snapshot(page);equal_geometry(before,view);only_view_controls(view);item['viewing']=view
  for kind in ['order','stats']:
   page.evaluate('''kind=>{window.qaOldCard=document.querySelector('.live-console');if(kind==='order'){const g=simbarGame;g.orders[0]={...g.orders[0],id:g.orders[0].id+'-visibility-qa'};}else window.dispatchEvent(new Event('online'));}''',kind);page.wait_for_function('qaOldCard!==document.querySelector(".live-console")');state=snapshot(page);equal_geometry(view,state);only_view_controls(state);item[kind+'Rebuild']=state['leakingHiddenTargets']
  page.set_viewport_size({'width':width-40,'height':height});settle(page);page.wait_for_timeout(60);only_view_controls(snapshot(page));page.set_viewport_size({'width':width,'height':height});settle(page);page.wait_for_timeout(60);only_view_controls(snapshot(page));page.screenshot(path=str(OUT/f'owner-{owner}-{width}-viewing.png'))
  page.locator('button[data-view-mode]').click();wait_mode(page,'management');shared.close_order(page);returned=snapshot(page);equal_geometry(before,returned);assert returned['mode']['label']=='감상모드';assert returned['mode']['box']==before['mode']['box'];assert page.locator('.ops-status').evaluate('(e)=>getComputedStyle(e).visibility==="visible"&&e.getBoundingClientRect().right>0');item['restoredStatusVisible']=True;page.screenshot(path=str(OUT/f'owner-{owner}-{width}-management.png'));item['passed']=True
 finally:context.close()

server,url=shared.serve(args.package.resolve() if args.package else ROOT,source=not args.package)
try:
 with sync_playwright() as pw:
  browser=pw.chromium.launch(executable_path=shared.chromium_executable(None),args=['--mute-audio'])
  for owner in args.owners:
   for width,height in [(1600,900),(844,390)]:(visibility_case if args.visibility_only else main_case)(browser,url,owner,width,height)
  browser.close()
 assert not report['errors'] and not report['localHTTPFailures'],report
 report['passed']=all(case.get('passed') for case in report['cases'])
except Exception:report['failure']=traceback.format_exc();raise
finally:server.shutdown();(OUT/'report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print(json.dumps({'passed':report['passed'],'cases':len(report['cases']),'errors':report['errors'],'remoteRequestsDenied':len(report['blockedRemote']),'localMediaFixtures':len(report['localMediaFixtures'])}))
