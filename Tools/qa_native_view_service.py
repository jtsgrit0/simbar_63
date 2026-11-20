"""Real-browser view-mode automatic service integration with no remote writes.

The core clock runs in real time for FIFO/price/pause/orientation observations.
Short phase fixtures exercise UI/core reset wiring without replaying a full show.
"""
import argparse,io,json,math,re,struct,traceback,wave
from pathlib import Path
from playwright.sync_api import sync_playwright
import qa_native_audio_feedback as shared

ROOT=Path(__file__).resolve().parent.parent
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--package',type=Path)
parser.add_argument('--output',type=Path,default=ROOT/'Temp/ViewServiceApply-20261115/browser')
parser.add_argument('--resume',type=Path,help='Reuse completed real-time checks; run only corrected lifecycle and staff scopes')
args=parser.parse_args();OUT=args.output.resolve();OUT.mkdir(parents=True,exist_ok=True)
report={'passed':False,'cases':[],'errors':[],'blockedRemote':[],'localMedia':[],
 'scope':'Actual UI entry/return, real 3-second core ticks, FIFO sale-only receipts, native local-WAV playback, staff arbitration, pause/portrait and fresh/reload/phase/destroy reset. No R2/Supabase writes.'}
if args.resume:
 report=json.loads(args.resume.read_text(encoding='utf8'))
 assert not report['errors'] and len(report['cases'])==1
 previous=report['cases'][0]
 assert all(previous.get(key) for key in ['threeServices','portraitStoppedService','nativeMusicControls','returnOffAndManualPopup','reloadOff','closingOff'])
 previous['passed']=True;previous['remainingLifecycleCase']='phase-reset-fixture'
 report.pop('failure',None);report['fixtureCorrection']='Initial synthetic results fixture omitted booking.completed. Remaining lifecycle scope now uses a completed booking before next-day planning.'

def wav():
 data=io.BytesIO()
 with wave.open(data,'wb') as target:
  target.setnchannels(1);target.setsampwidth(2);target.setframerate(8000)
  target.writeframes(b''.join(struct.pack('<h',int(700*math.sin(2*math.pi*220*i/8000))) for i in range(60*8000)))
 return data.getvalue()
WAV=wav()
INIT='''(()=>{window.serviceQA={audio:[]};const Native=window.Audio;window.Audio=function(...args){const a=new Native(...args);serviceQA.audio.push(a);return a;};window.Audio.prototype=Native.prototype;})()'''
SNAP='''()=>{const g=simbarGame,host=document.querySelector('.fast-game'),box=e=>{if(!e)return null;const r=e.getBoundingClientRect();return{x:r.x,y:r.y,width:r.width,height:r.height}},visible=e=>{const s=getComputedStyle(e),r=e.getBoundingClientRect(),h=host.getBoundingClientRect();return s.display!=='none'&&s.visibility!=='hidden'&&+s.opacity>.01&&r.width>0&&r.height>0&&r.right>h.left&&r.left<h.right&&r.bottom>h.top&&r.top<h.bottom&&!e.closest('[hidden]')};
 return{phase:g.phase,session:g.journey.session,paused:g.paused,active:g.viewServiceActive,elapsed:g.elapsed,secondsLeft:g.journey.secondsLeft,money:g.state.money,income:g.journey.drinkIncome,ordersServed:g.journey.orders,pours:g.state.pours,score:g.journey.score,combo:g.journey.combo,perfects:g.journey.manualPerfects,staffTimer:g.state.autoServeTimer,queue:g.orders.map(o=>({id:o.id,item:o.item,patience:o.patience})),mode:host.dataset.viewMode,transition:host.dataset.viewModeTransition,card:box(host.querySelector('.live-console')),tab:box(host.querySelector('[data-action="ui:liveTab"][data-payload*="music"]')),camera:simbarApplication.world.getVenueBounds(),popup:!!host.querySelector('.order-dialog'),visibleButtons:[...host.querySelectorAll('.venue-ui button,.css-layer button,.reference-ui button')].filter(visible).map(e=>({action:e.dataset.action,mode:e.hasAttribute('data-view-mode'),playlist:!!e.closest('.live-console')})),media:serviceQA.audio.map(a=>({paused:a.paused,time:a.currentTime,readyState:a.readyState})),receipts:window.serviceReceipts||[],arrivals:window.serviceArrivals||[]};}'''

def new_page(browser,url,viewport):
 context=browser.new_context(viewport=viewport,locale='ko-KR');context.add_init_script(INIT)
 def remote(route):
  request=route.request
  if re.match(r'https://pub-[^/]+\.r2\.dev/.*\.mp3(?:\?.*)?$',request.url,re.I):
   report['localMedia'].append(request.url);route.fulfill(status=200,content_type='audio/wav',body=WAV,headers={'Accept-Ranges':'bytes','Cache-Control':'no-store'})
  else:report['blockedRemote'].append({'url':request.url,'method':request.method});route.abort()
 context.route(re.compile(r'^https?://(?!127\.0\.0\.1[:/]|localhost[:/])'),remote)
 page=context.new_page();page.on('pageerror',lambda error:report['errors'].append(error.stack));page.goto(url,wait_until='domcontentloaded');page.wait_for_function('window.simbarGameReadyAt>0',timeout=45000)
 assert page.evaluate("typeof simbarGame.setViewServiceActive==='function'"),'Core view-service API is not ready'
 assert page.evaluate('simbarGame.viewServiceActive===false'),'A fresh app started automatic service'
 return context,page

def snap(page):shared.settle(page);return page.evaluate(SNAP)
def mode(page,value):page.wait_for_function("value=>{const h=document.querySelector('.fast-game');return h.dataset.viewMode===value&&h.dataset.viewModeTransition==='settled'}",arg=value,timeout=7000);shared.settle(page)
def only_view(state):
 assert sum(button['action']=='ui:settings' for button in state['visibleButtons'])==1,state['visibleButtons']
 assert all(button['mode'] or button['playlist'] or button['action']=='ui:settings' for button in state['visibleButtons']),state['visibleButtons']
 assert not state['popup'],'Automatic viewing opened a manual order popup'
def equal_geometry(before,after):
 for key in ['card','tab','camera']:assert before[key]==after[key],(key,before[key],after[key])

def prepare(page,staff=False):
 page.evaluate('''staff=>{const g=simbarGame;window.serviceTick=g.tick.bind(g);g.tick=()=>{};g.act('owner',{index:2});g.act('language',{language:'ko'});g.journey.completedNights=2;g.journey.day=3;g.journey.tutorialComplete=true;g.act('start');g.act('selectShow',{showId:'freewill'});g.act('deskWork',{task:0});g.act('deskWork',{task:1});if(!g.act('launch').success)throw Error('Launch failed');while(g.phase==='guide')g.act('guide');
  if(staff){const hired=g.act('hire',{index:0});if(!hired.success)throw Error('Staff fixture: '+JSON.stringify(hired));g.state.autoServeTimer=.05;g.state.wageTimer=60;}
 }''',staff)
 shared.close_order(page);page.locator('[data-action="ui:liveTab"][data-payload*="music"]').click();page.wait_for_function('serviceQA.audio.some(a=>!a.paused&&a.readyState>=3)')
 page.evaluate('''async()=>{const boot=document.querySelector('script[src$="/boot.js"]').src,{catalog}=await import(new URL('core.js',boot)),g=simbarGame;window.serviceReceipts=[];window.serviceArrivals=[];
  const serve=g._serve.bind(g);g._serve=(...args)=>{const front=g.orders[0],price=front?catalog.drinks[front.item].price:0,revenue=1+g.state.upgrades[0]*.08+(g.state.activeEvent===0?.1:g.state.activeEvent===2?.15:0),before={front:front?{id:front.id,item:front.item}:null,money:g.state.money,income:g.journey.drinkIncome,pours:g.state.pours,orders:g.journey.orders,score:g.journey.score,combo:g.journey.combo,perfects:g.journey.manualPerfects,elapsed:g.elapsed,active:g.viewServiceActive};const result=serve(...args);serviceReceipts.push({before,result,expectedSale:Math.round(price*revenue),after:{money:g.state.money,income:g.journey.drinkIncome,pours:g.state.pours,orders:g.journey.orders,score:g.journey.score,combo:g.journey.combo,perfects:g.journey.manualPerfects,elapsed:g.elapsed},automatic:args[1],context:args[3]?.viewService===true});return result;};
  const assign=g._assignOrder.bind(g);g._assignOrder=(...args)=>{const old=new Set(g.orders.map(o=>o.id)),result=assign(...args);for(const order of g.orders)if(!old.has(order.id))serviceArrivals.push({id:order.id,item:order.item,elapsed:g.elapsed});return result;};
 }''')

def verify_receipt(receipt):
 before,after,result=receipt['before'],receipt['after'],receipt['result'];assert result['success'] and result['code']=='view_served',receipt
 assert receipt['automatic'] and receipt['context'] and result['tip']==0 and result['sale']==receipt['expectedSale'],receipt
 assert result['orderId']==before['front']['id'];assert after['money']-before['money']==receipt['expectedSale'];assert after['income']-before['income']==receipt['expectedSale']
 assert after['pours']==before['pours']+1 and after['orders']==before['orders']+1
 for key in ['score','combo','perfects']:assert after[key]==before[key],receipt

def enter(page):
 page.locator('button[data-view-mode]').click();assert page.evaluate('simbarGame.viewServiceActive===true'),'View entry did not activate the core';mode(page,'viewing')
def leave(page):
 page.locator('button[data-view-mode]').click();assert page.evaluate('simbarGame.viewServiceActive===false'),'Return must stop service immediately, before the reverse slide ends';mode(page,'management')

def main_case(browser,url):
 context,page=new_page(browser,url,{'width':1600,'height':900});case={'name':'real-clock-sale-only-fifo-and-lifecycle'};report['cases'].append(case)
 try:
  prepare(page);before=snap(page);case['before']=before;enter(page);view=snap(page);only_view(view);equal_geometry(before,view);initial_ids=[order['id'] for order in view['queue']]
  page.evaluate('simbarGame.tick=window.serviceTick');page.wait_for_timeout(2500);early=snap(page);assert not early['receipts'],'A service occurred before its 3-second interval'
  page.wait_for_function('serviceReceipts.length>=3',timeout=10000);state=snap(page);only_view(state);equal_geometry(view,state)
  for receipt in state['receipts']:verify_receipt(receipt)
  assert [r['before']['front']['id'] for r in state['receipts'][:3]]==initial_ids[:3],state['receipts']
  assert len(state['arrivals'])>=1,'The normal arrival timer did not create another order'
  times=[r['before']['elapsed'] for r in state['receipts'][:3]];assert times[0]-view['elapsed']>=2.95 and all(b-a>=2.95 for a,b in zip(times,times[1:])),times
  case['threeServices']=state
  # Native pause leaves both the real clock and automatic service idle.
  page.evaluate("simbarGame.act('pause',{reason:'qa'})");held=snap(page);page.wait_for_timeout(2100);paused=snap(page);assert paused['ordersServed']==held['ordersServed'] and paused['money']==held['money'] and paused['elapsed']==held['elapsed'];case['paused']=paused;page.evaluate("simbarGame.act('resume')")
  # The pre-existing portrait gate stops core ticks and resumes the same mode.
  page.set_viewport_size({'width':390,'height':844});page.wait_for_function('simbarApplication.orientationBlocked');portrait=snap(page);page.wait_for_timeout(2100);portrait_after=snap(page);assert portrait_after['ordersServed']==portrait['ordersServed'] and portrait_after['money']==portrait['money'] and portrait_after['elapsed']==portrait['elapsed'];page.set_viewport_size({'width':1600,'height':900});page.wait_for_function('!simbarApplication.orientationBlocked');mode(page,'viewing');case['portraitStoppedService']=True
  # Retained transport uses actual native playback, independently of earnings.
  page.locator('[data-action="ui:musicToggle"]').click();page.wait_for_function('simbarGame.journey.musicPaused&&serviceQA.audio.every(a=>a.paused)');page.locator('[data-action="ui:musicToggle"]').click();page.wait_for_function('!simbarGame.journey.musicPaused&&serviceQA.audio.some(a=>!a.paused&&a.readyState>=3)');old=page.evaluate('simbarGame.state.playlist');page.locator('[data-action="ui:musicNext"]').click();page.wait_for_function('old=>simbarGame.state.playlist!==old',arg=old);only_view(snap(page));case['nativeMusicControls']=True
  page.screenshot(path=str(OUT/'viewing-sale-only.png'));leave(page);page.locator('.order-dialog').wait_for(state='visible',timeout=5000);off=snap(page);page.wait_for_timeout(3250);off_after=snap(page);assert not off_after['active'] and off_after['ordersServed']==off['ordersServed'] and off_after['money']==off['money'];case['returnOffAndManualPopup']=True
  shared.close_order(page);enter(page);page.evaluate('simbarGame.exportSave()');page.reload(wait_until='domcontentloaded');page.wait_for_function('window.simbarGameReadyAt>0');assert page.evaluate('simbarGame.viewServiceActive===false&&simbarApplication.viewMode.viewing===false');page.evaluate("simbarGame.act('continue')");page.wait_for_function("simbarGame.phase==='playing'");continued=snap(page);page.wait_for_timeout(3250);assert snap(page)['ordersServed']==continued['ordersServed'];case['reloadOff']=True
  shared.close_order(page);enter(page);page.evaluate('simbarGame._beginClosing()');page.wait_for_function("simbarGame.phase==='closing'&&simbarGame.viewServiceActive===false");case['closingOff']=True
  page.evaluate("()=>{const g=simbarGame;g.phase='results';g.journey.inProgress=false;g.journey.floorClosing=false;g.journey.settledSession=g.journey.session;g.journey.booking.completed=true;g.paused=true;g.pauseReason='results';}");page.wait_for_function("simbarApplication.viewMode.viewing===false");old_session=page.evaluate('simbarGame.journey.session');page.evaluate("()=>{const g=simbarGame;g.act('next');g.act('selectShow',{showId:'freewill'});g.act('deskWork',{task:0});g.act('deskWork',{task:1});const result=g.act('launch');if(!result.success)throw Error('Next-night launch failed: '+JSON.stringify(result));while(g.phase==='guide')g.act('guide');}");assert page.evaluate('simbarGame.viewServiceActive===false');assert page.evaluate('simbarGame.journey.session')!=old_session;case['newNightOff']=True
  shared.close_order(page);enter(page);page.evaluate('''()=>{window.destroyedServiceGame=simbarGame;simbarApplication.destroy();}''');assert page.evaluate('destroyedServiceGame.viewServiceActive===false');case['destroyOff']=True;case['passed']=True
 finally:context.close()

def phase_case(browser,url):
 context,page=new_page(browser,url,{'width':1600,'height':900});case={'name':'phase-reset-fixture'};report['cases'].append(case)
 try:
  prepare(page);enter(page);page.evaluate("simbarGame.act('lobby')");page.wait_for_function("simbarGame.phase==='lobby'&&simbarGame.viewServiceActive===false&&simbarApplication.viewMode.viewing===false");page.evaluate("simbarGame.act('continue')");page.wait_for_function("simbarGame.phase==='playing'");assert page.evaluate('simbarGame.viewServiceActive===false');case['lobbyAndContinueOff']=True
  shared.close_order(page);enter(page);page.evaluate('simbarGame._beginClosing()');page.wait_for_function("simbarGame.phase==='closing'&&simbarGame.viewServiceActive===false");case['closingOff']=True
  page.evaluate("()=>{const g=simbarGame;g.phase='results';g.journey.inProgress=false;g.journey.floorClosing=false;g.journey.settledSession=g.journey.session;g.journey.booking.completed=true;g.paused=true;g.pauseReason='results';}");page.wait_for_function("simbarApplication.viewMode.viewing===false");old_session=page.evaluate('simbarGame.journey.session')
  page.evaluate("()=>{const g=simbarGame;g.act('next');g.act('selectShow',{showId:'freewill'});g.act('deskWork',{task:0});g.act('deskWork',{task:1});const result=g.act('launch');if(!result.success)throw Error('Next-night fixture: '+JSON.stringify(result));while(g.phase==='guide')g.act('guide');}");assert page.evaluate('simbarGame.viewServiceActive===false');assert page.evaluate('simbarGame.journey.session')!=old_session;case['newNightOff']=True
  shared.close_order(page);enter(page);page.evaluate('''()=>{window.destroyedServiceGame=simbarGame;simbarApplication.destroy();}''');assert page.evaluate('destroyedServiceGame.viewServiceActive===false');case['destroyOff']=True;case['passed']=True
 finally:context.close()

def staff_case(browser,url):
 context,page=new_page(browser,url,{'width':844,'height':390});case={'name':'small-screen-staff-arbitration'};report['cases'].append(case)
 try:
  prepare(page,staff=True);before=snap(page);enter(page);view=snap(page);only_view(view);equal_geometry(before,view);page.evaluate('simbarGame.tick=window.serviceTick');page.wait_for_timeout(2500);early=snap(page);assert not early['receipts'],'An already-due barback bypassed view-only arbitration';assert abs(early['staffTimer']-.05)<1e-8,'View mode consumed the saved barback timer'
  page.wait_for_function('serviceReceipts.length>=1',timeout=2500);state=snap(page);assert len(state['receipts'])==1;verify_receipt(state['receipts'][0]);only_view(state);case['viewing']=state;page.screenshot(path=str(OUT/'small-viewing-staff.png'))
  leave(page);page.wait_for_function('serviceReceipts.some(r=>r.result.code==="auto_served")',timeout=2500);normal=next(r for r in snap(page)['receipts'] if r['result']['code']=='auto_served');assert normal['result']['income']>normal['expectedSale'],'Normal hired barback lost its existing tip behavior';assert not normal['context']
  ids=[r['before']['front']['id'] for r in snap(page)['receipts'] if r['result']['success']];assert len(ids)==len(set(ids)),'Two service systems consumed the same order';case['normalStaffRestored']=True;case['passed']=True
 finally:context.close()

server,url=shared.serve(args.package.resolve() if args.package else ROOT,source=not args.package)
try:
 with sync_playwright() as pw:
  browser=pw.chromium.launch(executable_path=shared.chromium_executable(None),args=['--mute-audio'])
  (phase_case if args.resume else main_case)(browser,url);staff_case(browser,url);browser.close()
 assert not report['errors'],report['errors'];report['passed']=all(case.get('passed') for case in report['cases'])
except Exception:report['failure']=traceback.format_exc();raise
finally:server.shutdown();(OUT/'report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print(json.dumps({'passed':report['passed'],'cases':len(report['cases']),'errors':report['errors'],'remoteDenied':len(report['blockedRemote'])}))
