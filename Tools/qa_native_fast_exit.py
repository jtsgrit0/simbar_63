"""Actual browser QA of fast concurrent non-greeter exits.

Disposable contexts and all artifacts remain on the supplied workspace.
Remote audio/statistics traffic is denied. Fixtures use normal launch/continue;
after closing starts no actor positions or choreographic phases are assigned.
The first departure segment runs on actual RAF, then public tick(.05) batches
finish the scope while the real renderer remains active between batches.
"""
import argparse,json,re,time,traceback
from pathlib import Path
from playwright.sync_api import sync_playwright
import qa_native_audio_feedback as shared

ROOT=Path(__file__).resolve().parent.parent
P=argparse.ArgumentParser(description=__doc__)
P.add_argument('--package',type=Path,required=True)
P.add_argument('--output',type=Path,default=ROOT/'Temp/FastExitApply-20261115/browser')
P.add_argument('--max-game-seconds',type=float,default=180)
P.add_argument('--owners',type=int,nargs='+',default=[0,1,2])
P.add_argument('--skip-saved',action='store_true')
A=P.parse_args();OUT=A.output.resolve();OUT.mkdir(parents=True,exist_ok=True)
REPORT={'passed':False,'scope':__doc__,'package':str(A.package.resolve()),'cases':[],'errors':[],'httpErrors':[],'blockedRemote':[]}
REPORT['runtime']=re.search(r'FastGame/([a-f0-9]{16})/',(A.package/'index.html').read_text(encoding='utf8'))[1]
STATE=r'''() => {
 const g=simbarGame,a=g.journey.afterShow,w=simbarApplication.world;
 return{phase:g.phase,paused:g.paused,session:g.journey.session,settledSession:g.journey.settledSession,day:g.journey.day,
  cash:g.state.money,xp:g.journey.xp,completedNights:g.journey.completedNights,farewellPending:g.farewellPending,
  floorTasksPending:g.floorTasksPending,afterShowPending:g.afterShowPending,staffReturnPending:g.staffReturnPending,
  afterShow:a,actors:g.actors.map(a=>({id:a.id,role:a.role,x:a.x,z:a.z,y:a.y,visible:a.visible,moving:a.moving,action:a.action,stageLevel:a.stageLevel})),
  camera:w.getVenueBounds(),audiencePresentation:w.getAudiencePresentation(),bandPresentation:w.getBandPresentation(),observer:window.exitQA};
}'''
INSTALL=r'''() => {
 const g=simbarGame,nativeTick=window.exitQABaseTick||g.tick.bind(g),customer=a=>['guest','seated','support','surfer'].includes(a.role);
 window.exitQA={ticks:0,gameSeconds:0,maxParallelMoving:0,maxParallelMovingDuringGesture:0,parallelMovedBeforeGreetingBarrier:false,
  greetIds:[],greetingPhases:{},greetingDurations:[],bandPhases:{},teleports:[],visibilityViolations:[],earlyGreetingExit:[],prematureSettlement:[],settlements:[],
  allGreetingsBeforeBarrier:true,modelWalkSeen:[],audienceWalkSeen:[],presentationColumns:{},maxVisibleAudience:0};
 window.exitQATick=dt=>{
  const qa=exitQA,before=new Map(g.actors.map(a=>[a.id,{x:a.x,z:a.z,y:a.y,visible:a.visible,role:a.role}])),old=g.journey.afterShow,
   members=new Map((old?.members||[]).map(m=>[m.id,{phase:m.phase,timer:m.timer,greeting:m.greeting}])),completed=g.journey.completedNights;
  nativeTick(dt);const state=g.journey.afterShow;qa.ticks++;qa.gameSeconds+=dt;
  if(!state)return;
  qa.greetIds=state.audience.filter(a=>!a.parallel).map(a=>a.id);
  const parallel=new Set(state.audience.filter(a=>a.parallel).map(a=>a.id));
  const moving=g.actors.filter(a=>parallel.has(a.id)&&a.visible!==false&&a.moving).length;
  qa.maxParallelMoving=Math.max(qa.maxParallelMoving,moving);
  const gesture=state.members.some(m=>['highfive','hug'].includes(m.phase));
  if(gesture)qa.maxParallelMovingDuringGesture=Math.max(qa.maxParallelMovingDuringGesture,moving);
  if(!state.departureStarted&&moving)qa.parallelMovedBeforeGreetingBarrier=true;
  qa.maxVisibleAudience=Math.max(qa.maxVisibleAudience,g.actors.filter(a=>customer(a)&&a.visible!==false).length);
  if(state.departureStarted&&!state.members.every(m=>m.greeting>=2||m.phase==='gone'))qa.allGreetingsBeforeBarrier=false;
  const participants=new Set([...state.members,...state.audience].map(a=>a.id));
  for(const a of g.actors){const p=before.get(a.id);if(!p||!participants.has(a.id))continue;
   const distance=Math.hypot(a.x-p.x,a.z-p.z);
   if(distance>dt*2.4+.036)qa.teleports.push({id:a.id,dt,distance});
   if(p.visible!==false&&a.visible===false&&Math.hypot(a.x-8.8,a.z+5.55)>.181)qa.visibilityViolations.push({id:a.id,x:a.x,z:a.z});
  }
  for(const fan of state.audience.filter(a=>!a.parallel)){
   const phases=qa.greetingPhases[fan.id]??=[];if(!phases.includes(fan.phase))phases.push(fan.phase);
   if(fan.phase==='gone'&&!state.departureStarted&&before.get(fan.id)?.visible!==false)qa.earlyGreetingExit.push(fan.id);
  }
  for(const m of state.members){
   const phases=qa.bandPhases[m.id]??=[];if(!phases.includes(m.phase))phases.push(m.phase);
   const p=members.get(m.id);if(p&&['highfive','hug'].includes(p.phase)&&m.phase!==p.phase)
    qa.greetingDurations.push({id:m.id,phase:p.phase,elapsed:Math.round((p.timer+dt)*1e6)/1e6});
  }
  if(g.journey.completedNights!==completed)qa.settlements.push({before:completed,after:g.journey.completedNights,allGone:[...state.members,...state.audience].every(a=>a.phase==='gone')});
  if(g.journey.settledSession===g.journey.session&&!state.completed)qa.prematureSettlement.push(state.elapsed);
 };
 g.tick=()=>{};
 window.exitQARender=()=>{
  const qa=exitQA,w=simbarApplication.world;
  for(const a of w.getAudiencePresentation())if(a.moving&&a.action==='walk'){
   if(!qa.audienceWalkSeen.includes(a.id))qa.audienceWalkSeen.push(a.id);
   const columns=qa.presentationColumns['audience:'+a.id]??=[];if(!columns.includes(a.column))columns.push(a.column);
  }
  for(const m of w.getBandPresentation())if(m.action==='walk'){
   if(!qa.modelWalkSeen.includes(m.id))qa.modelWalkSeen.push(m.id);
   const columns=qa.presentationColumns['band:'+m.id]??=[];if(!columns.includes(m.column))columns.push(m.column);
  }
 };
 window.exitQARun=async({seconds,perFrame=2})=>{
  const target=exitQA.gameSeconds+seconds;let steps=0;
  while(exitQA.gameSeconds<target-.0001&&g.phase!=='results'&&!g.farewellPending){
   exitQATick(.05);steps++;
   if(g.reaction?.activeKind)g.act('quick',{kind:g.reaction.activeKind});
   if(steps%perFrame===0){await new Promise(r=>requestAnimationFrame(r));exitQARender();}
  }
  await new Promise(r=>requestAnimationFrame(r));exitQARender();
 };
}'''

def state(page):shared.settle(page);return page.evaluate(STATE)
def capture(page,directory,name):
 path=directory/(name+'.png');page.screenshot(path=str(path));return str(path)
def ready(page):page.wait_for_function('window.simbarGameReadyAt>0',timeout=60000)
def receipt(s):return {key:s[key] for key in ['cash','xp','completedNights','settledSession','session']}

def run_case(browser,url,owner,width,height,fixture=None,video=False):
 label='petsounds-day5-saved' if fixture else 'owner-'+str(owner)+'-max'
 dest=OUT/label;dest.mkdir(exist_ok=True)
 item={'label':label,'owner':owner,'viewport':[width,height],'assertions':{},'screenshots':[]};REPORT['cases'].append(item)
 options={'viewport':{'width':width,'height':height},'device_scale_factor':1,'locale':'ko-KR'}
 if video:options.update(record_video_dir=str(dest/'video'),record_video_size={'width':width,'height':height})
 context=browser.new_context(**options)
 def remote(route):REPORT['blockedRemote'].append({'url':route.request.url,'method':route.request.method});route.abort()
 context.route(re.compile(r'^https?://(?!127\.0\.0\.1[:/]|localhost[:/])',re.I),remote)
 page=context.new_page();page.on('pageerror',lambda e:REPORT['errors'].append(str(e)))
 page.on('response',lambda r:REPORT['httpErrors'].append({'url':r.url,'status':r.status}) if r.url.startswith('http://127.0.0.1') and r.status>=400 else None)
 started=time.monotonic()
 try:
  page.goto(url,wait_until='domcontentloaded');ready(page)
  if fixture:
   raw=fixture.read_text(encoding='utf8');assert page.evaluate('raw=>simbarGame.importSave(raw)',raw),'Saved fixture import failed'
   page.reload(wait_until='domcontentloaded');ready(page);page.evaluate(INSTALL)
   page.locator('button[data-action="continue"]').click();shared.settle(page)
   item['fixture']=str(fixture);initial=state(page)
   assert initial['day']==5 and initial['phase']=='closing',initial
  else:
   page.evaluate('''owner=>{const g=simbarGame;window.exitQABaseTick=g.tick.bind(g);g.tick=()=>{};g.act('owner',{index:owner});g.act('language',{language:'ko'});g.journey.completedNights=2;g.journey.day=3;g.journey.tutorialComplete=true;g.state.upgrades[1]=1;g.state.fans=1000;g.state.money=100000;g.act('start');for(const [type,payload] of [['selectShow',{showId:'double'}],['ticket',{tier:0}],['promote',{}],['deskWork',{task:0}],['deskWork',{task:1}],['launch',{}]])if(!g.act(type,payload).success)throw Error('Fixture '+type+' failed');while(g.phase==='guide')g.act('guide');}''',owner)
   shared.close_order(page)
   item['launch']=page.evaluate('''()=>({phase:simbarGame.phase,capacity:simbarGame.festival.capacity,attendance:simbarGame.metrics().attendance,visible:simbarGame.actors.filter(a=>['guest','seated','support','surfer'].includes(a.role)&&a.visible!==false).length})''')
   expected=[64,80,44][owner];assert item['launch']['attendance']==expected and item['launch']['visible']==expected,item['launch']
   page.evaluate(INSTALL)
   # Boundary fixture mirrors core choreography tests. The normal tick invokes
   # last-call, clears orders, creates closing state, and keeps actual positions.
   page.evaluate('simbarGame.journey.secondsLeft=.05;exitQATick(.05)')
   initial=state(page);assert initial['phase']=='closing',initial
  item['initial']=initial;item['screenshots'].append(capture(page,dest,'01-closing'))
  # Observe a real RAF segment, without changing dt or bypassing tick.
  page.evaluate('simbarGame.tick=window.exitQATick');page.wait_for_timeout(2600)
  page.evaluate('simbarGame.tick=()=>{};exitQARender()')
  item['realRAFSeconds']=2.6
  item['screenshots'].append(capture(page,dest,'02-concurrent-exit'))
  greeting_capture=False;farewell_capture=False;seen=[]
  while time.monotonic()-started<110:
   current=state(page);after=current['afterShow']
   if current['phase']=='results':break
   if current['farewellPending']:
    if not farewell_capture:item['screenshots'].append(capture(page,dest,'04-doorway-empty'));farewell_capture=True
    page.locator('button[data-action="farewellAck"]').click();shared.settle(page);continue
   if current['observer']['gameSeconds']>A.max_game_seconds:break
   if not greeting_capture and after and any(m['phase'] in ['highfive','hug'] for m in after['members']):
    item['screenshots'].append(capture(page,dest,'03-greeting-with-exit'));item['greetingFrame']=current;greeting_capture=True
   page.evaluate('window.exitQARun({seconds:1,perFrame:2})')
   observed=state(page);seen.append({'seconds':observed['observer']['gameSeconds'],'phase':observed['phase'],'elapsed':observed['afterShow']['elapsed'],'visible':sum(a.get('visible') is not False for a in observed['afterShow']['audience']),'moving':sum(a.get('moving') and a.get('visible') is not False for a in observed['actors'])})
  final=state(page);item['final']=final;item['timeline']=seen
  qa=final['observer'];a=final['afterShow'];assert a
  checks=item['assertions'];checks['resultsReached']=final['phase']=='results';checks['allActualAudienceGone']=all(x['phase']=='gone' for x in a['audience']);checks['allFourBandGone']=len(a['members'])==4 and all(x['phase']=='gone' and x['greeting']==2 for x in a['members'])
  checks['allHiddenAtRealDoorway']=all(actor['visible'] is False and ((actor['x']-8.8)**2+(actor['z']+5.55)**2)**.5<=.181 for actor in final['actors'] if actor['id'] in {x['id'] for x in a['members']+a['audience']} and any(actor['id']==x['id'] and x.get('visible') is not False for x in initial['actors']))
  checks['continuousMovement']=not qa['teleports'];checks['noDisappearanceBeforeDoor']=not qa['visibilityViolations'];checks['allGreetingsBeforeBarrier']=qa['allGreetingsBeforeBarrier'] and not qa['earlyGreetingExit'];checks['noPrematureSettlement']=not qa['prematureSettlement'];checks['oneSettlement']=len(qa['settlements'])<=1 and final['completedNights']==initial['completedNights']+1 and final['settledSession']==final['session']
  checks['eightGreetingAudience']=sorted(qa['greetIds'])==sorted('patron-'+str(i) for i in range(8,16))
  if not fixture:
   checks['concurrentExit']=qa['maxParallelMoving']>=8;checks['ordinaryExitDuringGreeting']=qa['maxParallelMovingDuringGesture']>0;checks['ordinaryExitBeforeGreetingBarrier']=qa['parallelMovedBeforeGreetingBarrier'];checks['allEightGreetingsPreserved']=len(qa['greetingDurations'])==8 and all(abs(g['elapsed']-({'highfive':1.1,'hug':1.5}[g['phase']]))<=.051 for g in qa['greetingDurations']);checks['walkingAudienceActuallyRendered']=len(qa['audienceWalkSeen'])>=8;checks['walkingBandActuallyRendered']=len(qa['modelWalkSeen'])>=4
  checks['cameraUnchanged']=initial['camera']==final['camera']
  item['screenshots'].append(capture(page,dest,'05-receipt'))
  (dest/'final-save.json').write_text(page.evaluate('simbarGame.exportSave()'),encoding='utf8')
  before=receipt(final)
  page.reload(wait_until='domcontentloaded');ready(page);page.locator('button[data-action="continue"]').click();shared.settle(page)
  page.evaluate("simbarGame.tick(1);simbarGame.act('farewellAck')")
  reloaded=state(page);item['receiptReload']={key:reloaded[key] for key in before};checks['receiptIdempotentAfterReload']=receipt(reloaded)==before
  item['wallSeconds']=round(time.monotonic()-started,3);item['passed']=all(checks.values())
  assert item['passed'],{key:value for key,value in checks.items() if not value}
 finally:
  native_video=page.video if video else None;context.close()
  if native_video:item['video']=str(native_video.path())
  (dest/'report.json').write_text(json.dumps(item,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
 print(json.dumps({'case':label,'passed':item.get('passed'),'seconds':item.get('wallSeconds'),'parallel':item.get('final',{}).get('observer',{}).get('maxParallelMoving')},ensure_ascii=False),flush=True)

server,url=shared.serve(A.package.resolve())
try:
 with sync_playwright() as pw:
  browser=pw.chromium.launch(executable_path=shared.chromium_executable(None),args=['--mute-audio'])
  for owner in A.owners:run_case(browser,url,owner,*( (844,390) if owner==2 else (1600,900) ),video=owner in [0,2])
  if not A.skip_saved:run_case(browser,url,2,1600,900,ROOT/'Tools/fixtures/aftershow-petsounds-day5-gather-blocked.json')
  browser.close()
 assert not REPORT['errors'] and not REPORT['httpErrors'],REPORT
 REPORT['passed']=all(case.get('passed') for case in REPORT['cases'])
except Exception:REPORT['failure']=traceback.format_exc();raise
finally:server.shutdown();(OUT/'report.json').write_text(json.dumps(REPORT,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print(json.dumps({'passed':REPORT['passed'],'runtime':REPORT['runtime'],'cases':len(REPORT['cases']),'errors':REPORT['errors'],'remoteDenied':len(REPORT['blockedRemote'])},ensure_ascii=False))


