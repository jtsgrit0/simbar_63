"""Actual-browser unattended view-mode closing and lifecycle checks.

Uses disposable profiles, the real renderer and normal core actions/ticks.
R2/Supabase requests are denied. Boundary and scheduled-guest fixtures do not
change actor routes, cleanup progress, greeting phases or settlement results.
"""
import argparse,json,re,time,traceback
from pathlib import Path
from playwright.sync_api import sync_playwright
import qa_native_audio_feedback as shared
ROOT=Path(__file__).resolve().parent.parent
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--package',type=Path,required=True);p.add_argument('--output',type=Path,default=ROOT/'Temp/ViewClosingApply-20261115/browser');p.add_argument('--owners',type=int,nargs='+',default=[0,1,2]);p.add_argument('--skip-lifecycle',action='store_true');a=p.parse_args();OUT=a.output.resolve();OUT.mkdir(parents=True,exist_ok=True)
R={'passed':False,'runtime':re.search(r'FastGame/([a-f0-9]{16})/',(a.package/'index.html').read_text(encoding='utf8'))[1],'cases':[],'errors':[],'httpErrors':[],'blockedRemote':[]}
INIT="""window.closingInputs=[];for(const type of ['click','keydown'])document.addEventListener(type,e=>{if(e.isTrusted)closingInputs.push({type,action:e.target.closest?.('[data-action]')?.dataset.action||null,key:e.key||null});},true);"""
SNAP=r'''()=>{const g=simbarGame,w=simbarApplication.world,h=document.querySelector('.fast-game'),box=e=>{if(!e)return null;const r=e.getBoundingClientRect();return{x:r.x,y:r.y,width:r.width,height:r.height}},visible=e=>{const s=getComputedStyle(e),r=e.getBoundingClientRect();return s.visibility!=='hidden'&&s.display!=='none'&&+s.opacity>.01&&r.width>0&&r.height>0&&r.left<innerWidth&&r.right>0&&r.top<innerHeight&&r.bottom>0&&!e.closest('[hidden]')};return{phase:g.phase,paused:g.paused,pauseReason:g.pauseReason,elapsed:g.elapsed,closingActive:g.viewClosingActive,serviceActive:g.viewServiceActive,mode:h.dataset.viewMode,transition:h.dataset.viewModeTransition,money:g.state.money,xp:g.journey.xp,completed:g.journey.completedNights,session:g.journey.session,settled:g.journey.settledSession,farewell:g.farewellPending,floorTasks:g.floorTasksPending,staffReturn:g.staffReturnPending,guestExitFinished:g.guestExitFinished,floor:structuredClone(g.floor),reaction:structuredClone(g.reaction),escort:structuredClone(g.escort),afterShow:structuredClone(g.journey.afterShow),crew:g.crew,actors:g.actors.map(a=>({id:a.id,role:a.role,x:a.x,z:a.z,y:a.y,action:a.action,moving:a.moving,visible:a.visible,park:a._cleanupParkGoal})),ready:g.cleanupReadyWorkers,card:box(h.querySelector('.live-console')),camera:w.getVenueBounds(),visibleButtons:[...h.querySelectorAll('button')].filter(visible).map(e=>({action:e.dataset.action,mode:e.hasAttribute('data-view-mode'),playlist:!!e.closest('.live-console')})),results:[...h.querySelectorAll('.result-actions button')].map(e=>({action:e.dataset.action,visible:visible(e)})),inputs:closingInputs,qa:window.closingQA||null};}'''
OBSERVE=r'''()=>{const g=simbarGame,tick=window.closingNativeTick||g.tick.bind(g),quick=g._quick.bind(g),act=g.act.bind(g);window.closingQA={ticks:0,gameSeconds:0,actions:[],quick:[],workerActions:{},sweepIds:[],progress:[],noArrivalProgress:[],phaseSeen:[],escortPhases:[],greetingPhases:{},maxOrdinaryMoving:0,moveJumps:[],earlyHidden:[]};
 g._quick=kind=>{const before={kind,stage:g.reaction?.activeStage,elapsed:g.elapsed,active:g.viewClosingActive,ready:g.cleanupReadyWorkers},result=quick(kind);closingQA.quick.push({...before,result});return result;};
 g.act=(type,payload)=>{const before={type,active:g.viewClosingActive,phase:g.phase,completed:g.journey.completedNights},result=act(type,payload);if(['farewellAck','pause','resume'].includes(type))closingQA.actions.push({...before,result,after:g.phase});return result;};
 window.closingStep=dt=>{const q=closingQA,cleaned=g.floor.cleaned,ready=g.cleanupReadyWorkers,old=new Map(g.actors.map(a=>[a.id,{x:a.x,z:a.z,visible:a.visible}]));tick(dt);q.ticks++;q.gameSeconds+=dt;if(!q.phaseSeen.includes(g.phase))q.phaseSeen.push(g.phase);if(g.floor.cleaned>cleaned){q.progress.push({elapsed:g.elapsed,cleaned:g.floor.cleaned,ready,afterReady:g.cleanupReadyWorkers});if(!ready&&!g.cleanupReadyWorkers)q.noArrivalProgress.push(g.elapsed);}
 for(const id of g.crew){const worker=g.actors.find(a=>a.id===id);if(!worker)continue;const actions=q.workerActions[id]??=[];if(!actions.includes(worker.action))actions.push(worker.action);if(worker.action==='sweep'&&!q.sweepIds.includes(id))q.sweepIds.push(id);}
 if(g.escort&&!q.escortPhases.includes(g.escort.phase))q.escortPhases.push(g.escort.phase);
 const after=g.journey.afterShow;if(after){for(const m of after.members){const seen=q.greetingPhases[m.id]??=[];if(!seen.includes(m.phase))seen.push(m.phase);}const parallel=new Set(after.audience.filter(a=>a.parallel).map(a=>a.id));q.maxOrdinaryMoving=Math.max(q.maxOrdinaryMoving,g.actors.filter(a=>parallel.has(a.id)&&a.moving&&a.visible!==false).length);const participants=new Set([...after.members,...after.audience].map(a=>a.id));for(const actor of g.actors){const before=old.get(actor.id);if(!before||!participants.has(actor.id))continue;const d=Math.hypot(actor.x-before.x,actor.z-before.z);if(d>2.4*dt+.036)q.moveJumps.push({id:actor.id,d,dt});if(before.visible!==false&&actor.visible===false&&Math.hypot(actor.x-8.8,actor.z+5.55)>.181)q.earlyHidden.push(actor.id);}}
 };g.tick=()=>{};window.closingRun=async({seconds,perFrame=2,stopFarewell=false})=>{const until=closingQA.gameSeconds+seconds;let n=0;while(closingQA.gameSeconds<until-.0001&&g.phase!=='results'&&!(stopFarewell&&g.farewellPending)){closingStep(.05);if(++n%perFrame===0)await new Promise(r=>requestAnimationFrame(r));}await new Promise(r=>requestAnimationFrame(r));};}'''

def snap(page):shared.settle(page);return page.evaluate(SNAP)
def shot(page,dest,name):page.screenshot(path=str(dest/(name+'.png')))
def ready(page):page.wait_for_function('window.simbarGameReadyAt>0',timeout=60000)
def wait_mode(page,value):page.wait_for_function("v=>{const h=document.querySelector('.fast-game');return h.dataset.viewMode===v&&h.dataset.viewModeTransition==='settled'}",arg=value,timeout=7000)
def click_mode(page,value):
 b=page.locator('button[data-view-mode]').bounding_box();page.mouse.click(b['x']+b['width']/2,b['y']+b['height']/2);wait_mode(page,value)
def page_new(browser,url,size,dest,video=False):
 opts={'viewport':{'width':size[0],'height':size[1]},'locale':'ko-KR','device_scale_factor':1}
 if video:opts.update(record_video_dir=str(dest/'video'),record_video_size=opts['viewport'])
 context=browser.new_context(**opts);context.add_init_script(INIT)
 def deny(route):R['blockedRemote'].append({'url':route.request.url,'method':route.request.method});route.abort()
 context.route(re.compile(r'^https?://(?!127\.0\.0\.1[:/]|localhost[:/])',re.I),deny)
 page=context.new_page();page.on('pageerror',lambda e:R['errors'].append(str(e)));page.on('response',lambda r:R['httpErrors'].append({'url':r.url,'status':r.status}) if r.url.startswith('http://127.0.0.1') and r.status>=400 else None);page.goto(url,wait_until='domcontentloaded');ready(page);assert page.evaluate("typeof simbarGame.setViewClosingActive==='function'&&simbarGame.viewClosingActive===false");return context,page

def prepare(page,owner,guest=False):
 page.evaluate('''({owner,guest})=>{const g=simbarGame;window.closingNativeTick=g.tick.bind(g);g.tick=()=>{};g.act('owner',{index:owner});g.act('language',{language:'ko'});g.journey.completedNights=2;g.journey.day=3;g.journey.tutorialComplete=true;g.state.upgrades[1]=1;g.state.fans=1000;g.state.money=100000;g.act('start');for(const [type,payload] of [['selectShow',{showId:'double'}],['ticket',{tier:0}],['promote',{}],['deskWork',{task:0}],['deskWork',{task:1}],['launch',{}]])if(!g.act(type,payload).success)throw Error('Fixture '+type);while(g.phase==='guide')g.act('guide');if(guest){g.reaction.guestScheduled=true;g.reaction.guestAt=40;g.reaction.guestFired=false;g.journey.secondsLeft=140.05;closingNativeTick(.1);if(!g.floor.guestActive)throw Error('Scheduled guest fixture failed');}g.journey.secondsLeft=.3;}''',{'owner':owner,'guest':guest})
 shared.close_order(page);page.evaluate(OBSERVE)

def until_results(page,dest,max_seconds=180,stop_farewell=False):
 captures=set();start=time.monotonic()
 while time.monotonic()-start<100:
  s=snap(page)
  if s['phase']=='results' or stop_farewell and s['farewell']:return s
  if s['qa']['gameSeconds']>max_seconds:break
  if s['qa']['sweepIds'] and 'sweep' not in captures:shot(page,dest,'sweeping');captures.add('sweep')
  if s['escort'] and 'escort' not in captures:shot(page,dest,'escort');captures.add('escort')
  page.evaluate('window.closingRun({seconds:1,perFrame:2,stopFarewell:'+str(stop_farewell).lower()+'})')
 raise AssertionError('Closing did not finish: '+json.dumps(s,ensure_ascii=False)[:3000])

def receipt(s):return[s['money'],s['xp'],s['completed'],s['settled']]
def validate_final(s,initial,guest):
 q=s['qa'];assert s['phase']=='results' and not s['closingActive'] and not s['serviceActive'] and s['mode']=='management',s
 assert s['floor']['cleanupComplete'] and s['floor']['cleanupRewardClaimed'] and s['floor']['cleaned']==s['floor']['litter']
 assert q['progress'] and q['sweepIds'] and not q['noArrivalProgress']
 assert not s['staffReturn'] and all(not x.get('park') or ((x['x']-x['park']['x'])**2+(x['z']-x['park']['z'])**2)**.5<=.051 for x in s['actors'] if x['id'] in s['crew'])
 assert s['afterShow']['completed'] and s['afterShow']['noticeAcknowledged'] and all(m['phase']=='gone' and m['greeting']==2 for m in s['afterShow']['members'])
 assert all({'highfive','hug','exit','gone'}.issubset(phases) for phases in q['greetingPhases'].values())
 assert not q['moveJumps'] and not q['earlyHidden'] and q['maxOrdinaryMoving']>=8
 assert s['completed']==initial['completed']+1 and s['settled']==s['session']
 assert len(s['results'])==3 and all(b['visible'] for b in s['results']),s['results']
 assert s['camera']==initial['camera']
 if guest:assert s['floor']['guestComplete'] and s['guestExitFinished'] and s['escort'] is None and {'join','exit','return'}.issubset(q['escortPhases']),q['escortPhases']

def main_case(browser,url,owner):
 name='owner-'+str(owner);dest=OUT/name;dest.mkdir(exist_ok=True);video=owner in [1,2];ctx,page=page_new(browser,url,(844,390) if owner==1 else (1600,900),dest,video);r={'name':name,'owner':owner,'guestFixture':owner in [1,2]};R['cases'].append(r)
 try:
  prepare(page,owner,r['guestFixture']);initial=snap(page);r['initial']=initial;click_mode(page,'viewing');assert page.evaluate('simbarGame.viewServiceActive&&!simbarGame.viewClosingActive');seal=page.evaluate('closingInputs.length');shot(page,dest,'01-view-entry')
  page.evaluate('simbarGame.tick=window.closingStep');page.wait_for_timeout(3200);page.evaluate('simbarGame.tick=()=>{}');closing=snap(page);r['realRAFClosing']=closing
  assert closing['phase']=='closing' and closing['closingActive'] and not closing['serviceActive'];assert sum(b['action']=='ui:settings' for b in closing['visibleButtons'])==1,closing['visibleButtons'];assert all(b['mode'] or b['playlist'] or b['action']=='ui:settings' for b in closing['visibleButtons']),closing['visibleButtons'];shot(page,dest,'02-automatic-closing')
  final=until_results(page,dest);page.wait_for_function("simbarGame.phase==='results'&&!simbarApplication.viewMode.viewing");final=snap(page);r['final']=final;validate_final(final,initial,r['guestFixture']);assert len(final['inputs'])==seal,'A manual input was required after view entry';assert any(x['type']=='farewellAck' and x['active'] for x in final['qa']['actions']);shot(page,dest,'03-results')
  before=receipt(final);page.reload(wait_until='domcontentloaded');ready(page);assert page.evaluate('!simbarGame.viewClosingActive&&!simbarGame.viewServiceActive&&!simbarApplication.viewMode.viewing');page.locator('[data-action="continue"]').click();shared.settle(page);page.evaluate("simbarGame.tick(1);simbarGame.act('farewellAck')");after=snap(page);assert receipt(after)==before;r['receiptReload']=receipt(after);r['noInputAfterEntry']=True;r['passed']=True
 finally:
  native=page.video if video else None;ctx.close()
  if native:r['video']=str(native.path())
  (dest/'report.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
 print(json.dumps({'case':name,'passed':r.get('passed'),'sweeps':r.get('final',{}).get('qa',{}).get('sweepIds')},ensure_ascii=False),flush=True)

def lifecycle_case(browser,url):
 dest=OUT/'lifecycle';dest.mkdir(exist_ok=True);ctx,page=page_new(browser,url,(1600,900),dest);r={'name':'return-reload-pauses-farewell-entry'};R['cases'].append(r)
 try:
  prepare(page,2);click_mode(page,'viewing');page.evaluate('window.closingRun({seconds:4})');s=snap(page);assert s['closingActive'];page.evaluate("simbarGame.act('pause',{reason:'qa-focus'})");held=snap(page);page.evaluate('simbarGame.tick=window.closingStep');page.wait_for_timeout(600);paused=snap(page);assert paused['elapsed']==held['elapsed'] and paused['floor']['cleaned']==held['floor']['cleaned'] and paused['afterShow']==held['afterShow'];page.evaluate("simbarGame.tick=()=>{};simbarGame.act('resume')");r['explicitPausePreserved']=True
  page.set_viewport_size({'width':390,'height':844});page.wait_for_function('simbarApplication.orientationBlocked');held=snap(page);page.evaluate('simbarGame.tick=window.closingStep');page.wait_for_timeout(600);portrait=snap(page);assert portrait['elapsed']==held['elapsed'] and portrait['floor']['cleaned']==held['floor']['cleaned'] and portrait['afterShow']==held['afterShow'];page.evaluate('simbarGame.tick=()=>{}');page.set_viewport_size({'width':1600,'height':900});page.wait_for_function('!simbarApplication.orientationBlocked');r['portraitFrozen']=True
  click_mode(page,'management');assert page.evaluate('!simbarGame.viewClosingActive&&!simbarGame.viewServiceActive');count=page.evaluate('closingQA.quick.length');page.evaluate('window.closingRun({seconds:2})');assert page.evaluate('closingQA.quick.length')==count;r['returnOff']=True
  # Exhaust any previously authorized work, then use one real manual button.
  for _ in range(60):
   s=snap(page)
   if s['farewell']:break
   if s['reaction']['activeKind'] and page.locator('.reaction-press:enabled').count():
    before=(s['reaction']['presses'],s['reaction']['completedRounds']);page.locator('.reaction-press:enabled').click();after=snap(page);assert after['reaction']['presses']>before[0] or after['reaction']['completedRounds']>before[1];r['manualReactionRestored']=True;break
   page.evaluate('window.closingRun({seconds:1,stopFarewell:true})')
  assert r.get('manualReactionRestored'),'Manual closing reaction did not return'
  click_mode(page,'viewing');page.evaluate('window.closingRun({seconds:1})');page.evaluate('simbarGame.save()');page.reload(wait_until='domcontentloaded');ready(page);assert page.evaluate('!simbarGame.viewClosingActive&&!simbarApplication.viewMode.viewing');page.locator('[data-action="continue"]').click();page.evaluate('window.closingNativeTick=simbarGame.tick.bind(simbarGame)');page.evaluate(OBSERVE);s=snap(page);assert not s['closingActive'] and s['mode']=='management';count=page.evaluate('closingQA.quick.length');page.evaluate('window.closingRun({seconds:1,stopFarewell:true})');assert page.evaluate('closingQA.quick.length')==count;r['reloadNormal']=True
  # With automation off, the unchanged manual farewell barrier is reached.
  manual=until_results(page,dest,stop_farewell=True);assert manual['farewell'] and manual['paused'] and manual['pauseReason']=='farewell';r['manualFarewellPreserved']=True
  # The modal intentionally covers pointer hit-testing; dispatch the existing
  # mode button's own click handler while the fixture clock is frozen.
  page.locator('button[data-view-mode]').evaluate('(button)=>button.click()');wait_mode(page,'viewing');assert page.evaluate('simbarGame.viewClosingActive&&simbarGame.farewellPending');page.set_viewport_size({'width':390,'height':844});page.wait_for_function('simbarApplication.orientationBlocked');page.evaluate('simbarGame.tick=window.closingStep');page.wait_for_timeout(500);assert page.evaluate('simbarGame.farewellPending'),'Setter advanced farewell while portrait prevented ticks';page.evaluate('simbarGame.tick=()=>{}');page.set_viewport_size({'width':1600,'height':900});page.wait_for_function('!simbarApplication.orientationBlocked');r['farewellEntryPortraitNoProgress']=True
  initial_completed=snap(page)['completed'];final=until_results(page,dest);assert final['completed']==initial_completed+1 and final['afterShow']['noticeAcknowledged'];assert any(x['type']=='farewellAck' and x['active'] for x in final['qa']['actions']);assert not final['closingActive'] and final['mode']=='management';shot(page,dest,'farewell-entry-results');r['farewellEntryRecovered']=True;r['final']=final;r['passed']=True
 finally:ctx.close();(dest/'report.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
 print(json.dumps({'case':r['name'],'passed':r.get('passed')},ensure_ascii=False),flush=True)

server,url=shared.serve(a.package.resolve())
try:
 with sync_playwright() as pw:
  browser=pw.chromium.launch(executable_path=shared.chromium_executable(None),args=['--mute-audio'])
  for owner in a.owners:main_case(browser,url,owner)
  if not a.skip_lifecycle:lifecycle_case(browser,url)
  browser.close()
 assert not R['errors'] and not R['httpErrors'],R;R['passed']=all(c.get('passed') for c in R['cases'])
except Exception:R['failure']=traceback.format_exc();raise
finally:server.shutdown();(OUT/'report.json').write_text(json.dumps(R,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print(json.dumps({'passed':R['passed'],'runtime':R['runtime'],'cases':len(R['cases']),'errors':R['errors'],'remoteDenied':len(R['blockedRemote'])},ensure_ascii=False))
