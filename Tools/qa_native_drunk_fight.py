"""Browser QA of the third ignored guest response and five real departures.

A normal scheduled guest fixture is advanced only with the public tick method.
No actor, fight phase, duration, pose or departure is assigned by this helper.
Original R2 MP3 URLs receive a small native WAV; every Supabase request is denied.
"""
import argparse,io,json,math,re,struct,time,traceback,wave
from pathlib import Path
from playwright.sync_api import sync_playwright
import qa_native_audio_feedback as shared
ROOT=Path(__file__).resolve().parent.parent
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--package',type=Path,required=True);p.add_argument('--output',type=Path,default=ROOT/'Temp/DrunkFightApply-20261115/browser');p.add_argument('--owners',type=int,nargs='+',default=[0,1,2]);p.add_argument('--inspection-only',action='store_true');p.add_argument('--view-visual-only',action='store_true');a=p.parse_args();OUT=a.output.resolve();OUT.mkdir(parents=True,exist_ok=True)
R={'passed':False,'runtime':re.search(r'FastGame/([a-f0-9]{16})/',(a.package/'index.html').read_text(encoding='utf8'))[1],'cases':[],'errors':[],'httpErrors':[],'blockedRemote':[],'localWAV':[]}
b=io.BytesIO()
with wave.open(b,'wb') as w:
 w.setnchannels(1);w.setsampwidth(2);w.setframerate(8000);w.writeframes(b''.join(struct.pack('<h',int(600*math.sin(2*math.pi*220*i/8000))) for i in range(4*8000)))
WAV=b.getvalue()
SNAP=r'''()=>{const g=simbarGame;return{phase:g.phase,paused:g.paused,elapsed:g.elapsed,misses:g.floor.guestMisses,incident:structuredClone(g.floor.guestIncident),pending:g.drunkIncidentPending,live:g.festival.liveAttendance,floor:structuredClone(g.floor),reaction:structuredClone(g.reaction),inspection:g.inspection,actors:g.actors.map(a=>({id:a.id,role:a.role,x:a.x,z:a.z,y:a.y,visible:a.visible,moving:a.moving,action:a.action,drunkIncident:a.drunkIncident,fightIndex:a.fightIndex,fightElapsed:a.fightElapsed,fightRemaining:a.fightRemaining,fightPartner:a.fightPartner})),money:g.state.money,xp:g.journey.xp,completed:g.journey.completedNights,session:g.journey.session,settled:g.journey.settledSession,camera:simbarApplication.world.getVenueBounds(),qa:window.fightQA||null};}'''
OBSERVE=r'''()=>{const g=simbarGame,tick=window.fightNativeTick||g.tick.bind(g),start=g._startDrunkIncident.bind(g);window.fightQA={seconds:0,triggers:[],jump:[],hiddenAway:[],reappeared:[],everGone:[],phases:[]};
 g._startDrunkIncident=()=>{const origin=g.actors.find(a=>a.id==='assisted-guest'&&a.visible!==false),candidates=g.actors.filter(a=>['guest','seated','support','surfer'].includes(a.role)&&a.visible!==false&&!a.drunkIncident&&a.id!==origin?.id).map(a=>({id:a.id,x:a.x,z:a.z,role:a.role,distance:Math.hypot(a.x-origin.x,a.z-origin.z)})).sort((a,b)=>a.distance-b.distance||a.id.localeCompare(b.id)),beforeIds=g.actors.map(a=>a.id),live=g.festival.liveAttendance,result=start();fightQA.triggers.push({origin:{id:origin.id,x:origin.x,z:origin.z},candidates,expected:candidates.slice(0,5).map(a=>a.id),beforeIds,afterIds:g.actors.map(a=>a.id),live,actual:g.floor.guestIncident.participants.map(a=>a.id)});return result;};
 window.fightStep=dt=>{const q=fightQA,before=new Map(g.actors.map(a=>[a.id,{x:a.x,z:a.z,visible:a.visible}]));tick(dt);q.seconds+=dt;const state=g.floor.guestIncident;if(!state)return;if(!q.phases.includes(state.phase))q.phases.push(state.phase);const ids=new Set(state.participants.map(a=>a.id));for(const actor of g.actors){if(!ids.has(actor.id))continue;const old=before.get(actor.id);if(old&&Math.hypot(actor.x-old.x,actor.z-old.z)>2.5*dt+.04)q.jump.push({id:actor.id,dt});if(old&&old.visible!==false&&actor.visible===false&&Math.hypot(actor.x-8.8,actor.z+5.55)>.181)q.hiddenAway.push(actor.id);if(q.everGone.includes(actor.id)&&actor.visible!==false)q.reappeared.push(actor.id);if(actor.visible===false&&!q.everGone.includes(actor.id))q.everGone.push(actor.id);}};
 g.tick=()=>{};window.fightRun=async({seconds,stopMiss=0,stopPhase=null})=>{const target=fightQA.seconds+seconds;let steps=0;while(fightQA.seconds<target-.0001&&g.phase!=='results'&&!(stopMiss&&g.floor.guestMisses>=stopMiss)&&!(stopPhase&&g.floor.guestIncident?.phase===stopPhase)){fightStep(.05);if(++steps%2===0)await new Promise(r=>requestAnimationFrame(r));}await new Promise(r=>requestAnimationFrame(r));};}'''
def snap(page):shared.settle(page);return page.evaluate(SNAP)
def shot(page,d,name):page.screenshot(path=str(d/(name+'.png')))
def ready(page):page.wait_for_function('window.simbarGameReadyAt>0',timeout=60000)
def new(browser,url,dest,size):
 ctx=browser.new_context(viewport={'width':size[0],'height':size[1]},locale='ko-KR')
 def route(r):
  if re.match(r'https://pub-[^/]+\.r2\.dev/.*\.mp3(?:\?.*)?$',r.request.url,re.I):R['localWAV'].append(r.request.url);r.fulfill(status=200,content_type='audio/wav',body=WAV,headers={'Accept-Ranges':'bytes','Cache-Control':'no-store'})
  else:R['blockedRemote'].append({'url':r.request.url,'method':r.request.method});r.abort()
 ctx.route(re.compile(r'^https?://(?!127\.0\.0\.1[:/]|localhost[:/])',re.I),route)
 page=ctx.new_page();page.on('pageerror',lambda e:R['errors'].append(str(e)));page.on('response',lambda r:R['httpErrors'].append({'url':r.url,'status':r.status}) if r.url.startswith('http://127.0.0.1') and r.status>=400 else None);page.goto(url,wait_until='domcontentloaded');ready(page);return ctx,page

def prepare(page,owner):
 page.evaluate('''owner=>{const g=simbarGame;window.fightNativeTick=g.tick.bind(g);g.tick=()=>{};g.act('owner',{index:owner});g.act('language',{language:'ko'});g.journey.completedNights=2;g.journey.day=3;g.journey.tutorialComplete=true;g.state.upgrades[1]=1;g.state.fans=1000;g.state.money=100000;g.act('start');for(const [type,payload] of [['selectShow',{showId:'double'}],['ticket',{tier:0}],['promote',{}],['deskWork',{task:0}],['deskWork',{task:1}],['launch',{}]])if(!g.act(type,payload).success)throw Error('Fixture '+type);while(g.phase==='guide')g.act('guide');g.journey.inspection.triggerAt=150;g.reaction.guestScheduled=true;g.reaction.guestAt=40;g.reaction.guestFired=false;g.journey.secondsLeft=140.05;fightNativeTick(.1);if(!g.floor.guestActive)throw Error('Scheduled guest did not start');g.journey.festival.arrivalCredit=0;}''',owner)
 shared.close_order(page);page.locator('[data-action="ui:liveTab"][data-payload*="music"]').click();page.evaluate(OBSERVE)

def mode(page):
 b=page.locator('button[data-view-mode]').bounding_box();page.mouse.click(b['x']+b['width']/2,b['y']+b['height']/2);page.wait_for_function("document.querySelector('.fast-game').dataset.viewMode==='viewing'&&document.querySelector('.fast-game').dataset.viewModeTransition==='settled'")

def wait_phase(page,phase,limit=80):
 for _ in range(limit):
  s=snap(page)
  if s['incident'] and s['incident']['phase']==phase:return s
  page.evaluate('window.fightRun({seconds:1,stopPhase:'+json.dumps(phase)+'})')
 raise AssertionError('Incident did not reach '+phase+': '+json.dumps(s,ensure_ascii=False)[:1500])

def pose_pixels(page,ids,first):
 return page.evaluate('''async({ids,first})=>{const app=simbarApplication,c=app.canvas,r=c.getBoundingClientRect(),points=simbarGame.actors.filter(a=>ids.includes(a.id)).map(a=>app.world.worldToScreen(a.x,a.z,a.y||0)),sx=c.width/r.width,sy=c.height/r.height;let x=Math.max(0,Math.floor((Math.min(...points.map(p=>p.x))-30)*sx)),y=Math.max(0,Math.floor((Math.min(...points.map(p=>p.y))-58)*sy)),right=Math.min(c.width,Math.ceil((Math.max(...points.map(p=>p.x))+30)*sx)),bottom=Math.min(c.height,Math.ceil((Math.max(...points.map(p=>p.y))+12)*sy));if(first)window.fightPixelROI={x,y,w:right-x,h:bottom-y};const roi=window.fightPixelROI,data=c.getContext('2d').getImageData(roi.x,roi.y,roi.w,roi.h).data;let changed=0;if(first)window.fightFirstPixels=new Uint8ClampedArray(data);else for(let i=0;i<data.length;i+=4)if(data[i]!==fightFirstPixels[i]||data[i+1]!==fightFirstPixels[i+1]||data[i+2]!==fightFirstPixels[i+2]||data[i+3]!==fightFirstPixels[i+3])changed++;const boot=document.querySelector('script[src$="/boot.js"]').src,{pixelDrunkFightPose}=await import(new URL('world.js',boot));return{clip:{x:r.x+roi.x/sx,y:r.y+roi.y/sy,width:roi.w/sx,height:roi.h/sy},changed,poses:simbarGame.actors.filter(a=>ids.includes(a.id)).map(a=>({id:a.id,pose:pixelDrunkFightPose(a,{phase:simbarGame.phase,inspection:simbarGame.inspection})}))};}''',{'ids':ids,'first':first})

def main_case(browser,url,owner):
 dest=OUT/('owner-'+str(owner));dest.mkdir(exist_ok=True);r={'name':'owner-'+str(owner)};R['cases'].append(r);ctx,page=new(browser,url,dest,(844,390) if owner==1 else (1600,900))
 try:
  prepare(page,owner);initial=snap(page);r['initial']=initial
  for n in [1,2]:
   page.evaluate('window.fightRun({seconds:10,stopMiss:'+str(n)+'})');s=snap(page);assert s['misses']==n and s['incident'] is None and not s['qa']['triggers'];r['timeout'+str(n)]=s
  page.evaluate('window.fightRun({seconds:10,stopMiss:3})');selected=snap(page);r['selection']=selected;assert selected['misses']==3 and len(selected['qa']['triggers'])==1;trigger=selected['qa']['triggers'][0];assert trigger['actual']==trigger['expected'] and len(trigger['actual'])==5 and trigger['beforeIds']==trigger['afterIds'];ids=trigger['actual'];r['selectedIds']=ids
  fighting=wait_phase(page,'fight');r['fightStart']=fighting;assert fighting['incident']['elapsed']<.06 and all(a['visible'] is not False and a['action']=='fight' for a in fighting['actors'] if a['id'] in ids)
  page.evaluate('simbarGame.tick=window.fightStep');first=pose_pixels(page,ids,True);shot(page,dest,'fight-frame-1');page.screenshot(path=str(dest/'fight-crop-1.png'),clip=first['clip']);page.wait_for_timeout(370);second=pose_pixels(page,ids,False);shot(page,dest,'fight-frame-2');page.screenshot(path=str(dest/'fight-crop-2.png'),clip=second['clip']);r['renderedFight']={'first':first,'second':second,'state':snap(page)};assert second['changed']>0 and first['poses']!=second['poses'],{'changed':second['changed'],'firstPoses':first['poses'],'secondPoses':second['poses']};assert all(x['pose'] and x['pose']['kind']=='fight' for x in second['poses']),second['poses']
  page.wait_for_function("simbarGame.floor.guestIncident.phase==='exit'",timeout=10000);page.evaluate('simbarGame.tick=()=>{}');exiting=snap(page);r['exitStart']=exiting;assert abs(exiting['incident']['elapsed']-4)<1e-6 and any(a['action']=='walk' and a['moving'] for a in exiting['actors'] if a['id'] in ids);shot(page,dest,'walk-exit')
  gone=wait_phase(page,'done');r['gone']=gone;assert not gone['qa']['jump'] and not gone['qa']['hiddenAway'] and not gone['qa']['reappeared'];assert all(a['visible'] is False and a['drunkIncident']=='gone' for a in gone['actors'] if a['id'] in ids)
  introduced=sum(a['visible'] is not False and a['id'] not in trigger['beforeIds'] and a['role'] in ['guest','seated','support','surfer','assisted'] for a in gone['actors']);assert gone['live']-introduced==trigger['live']-5,(gone['live'],introduced,trigger['live']);page.evaluate('window.fightRun({seconds:3})');stable=snap(page);assert all(a['visible'] is False for a in stable['actors'] if a['id'] in ids);assert len({a['id'] for a in stable['actors']})==len(stable['actors']);r['noImmediateRespawn']=True
  raw=page.evaluate('simbarGame.exportSave()');(dest/'departed-save.json').write_text(raw,encoding='utf8');saved=json.loads(raw);assert all(any(a['id']==id and a['visible'] is False for a in saved['native']['actors']) for id in ids)
  page.reload(wait_until='domcontentloaded');ready(page);page.locator('[data-action="continue"]').click();page.evaluate('window.fightNativeTick=simbarGame.tick.bind(simbarGame);simbarGame.tick=()=>{}');restored=snap(page);assert all(any(a['id']==id and a['visible'] is False and a['drunkIncident']=='gone' for a in restored['actors']) for id in ids);r['reloadTombstones']=True;page.evaluate(OBSERVE)
  shared.close_order(page);mode(page);page.evaluate('simbarGame.journey.secondsLeft=.1;fightStep(.1)');page.wait_for_function('simbarGame.viewClosingActive')
  for _ in range(180):
   final=snap(page)
   if final['phase']=='results':break
   page.evaluate('window.fightRun({seconds:1})')
  assert final['phase']=='results' and final['floor']['cleanupComplete'] and final['floor']['cleanupRewardClaimed'];assert final['completed']==restored['completed']+1 and final['settled']==final['session'];assert final['incident']['phase']=='done';assert all(a['visible'] is False for a in final['actors'] if a['id'] in ids);assert initial['camera']==final['camera'];r['final']=final;shot(page,dest,'cleanup-results');r['passed']=True
 finally:ctx.close();(dest/'report.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
 print(json.dumps({'case':r['name'],'passed':r.get('passed'),'ids':r.get('selectedIds')},ensure_ascii=False),flush=True)

def inspection_case(browser,url):
 dest=OUT/'inspection';dest.mkdir(exist_ok=True);ctx,page=new(browser,url,dest,(1600,900));r={'name':'inspection-does-not-count-misses'};R['cases'].append(r)
 try:
  prepare(page,2);assert page.evaluate("simbarGame.act('inspection').success")
  for _ in range(30):
   s=snap(page)
   if s['inspection']['freezePerformers']:break
   page.evaluate('window.fightRun({seconds:.25})')
  assert s['inspection']['freezePerformers'];before=snap(page);page.evaluate('window.fightRun({seconds:6})');after=snap(page);assert after['inspection']['freezePerformers'];assert after['reaction']['secondsLeft']==before['reaction']['secondsLeft'] and after['misses']==before['misses'] and after['incident'] is None;r.update(before=before,after=after,passed=True);shot(page,dest,'frozen-no-false-misses')
 finally:ctx.close();(dest/'report.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
 print(json.dumps({'case':r['name'],'passed':r.get('passed')},ensure_ascii=False),flush=True)

def view_visual_case(browser,url):
 dest=OUT/'small-view-visual';dest.mkdir(exist_ok=True);r={'name':'small-view-visual','scope':'Existing view mode reveals the same actor event; management layout, cards, camera and source art remain unchanged.'};R['cases'].append(r);ctx,page=new(browser,url,dest,(844,390))
 try:
  prepare(page,1);page.evaluate('window.fightRun({seconds:30,stopMiss:3})');fighting=wait_phase(page,'fight');ids=[x['id'] for x in fighting['incident']['participants']];mode(page);assert page.evaluate('simbarGame.floor.guestMisses===3');before=snap(page);page.evaluate('simbarGame.tick=window.fightStep');first=pose_pixels(page,ids,True);shot(page,dest,'fight-view-frame-1');page.screenshot(path=str(dest/'fight-view-crop-1.png'),clip=first['clip']);page.wait_for_timeout(370);second=pose_pixels(page,ids,False);shot(page,dest,'fight-view-frame-2');page.screenshot(path=str(dest/'fight-view-crop-2.png'),clip=second['clip']);assert second['changed']>0 and first['poses']!=second['poses'] and all(x['pose'] and x['pose']['kind']=='fight' for x in second['poses']);r['canvasHits']=page.evaluate('ids=>{const app=simbarApplication,c=app.canvas,r=c.getBoundingClientRect();return simbarGame.actors.filter(a=>ids.includes(a.id)).map(a=>{const p=app.world.worldToScreen(a.x,a.z,a.y||0),e=document.elementFromPoint(p.x+r.x,p.y+r.y-18);return{id:a.id,canvas:e===c,element:e?.className||e?.tagName};});}',ids);assert all(x['canvas'] for x in r['canvasHits']),r['canvasHits'];assert before['camera']==snap(page)['camera'];r.update(selectedIds=ids,first=first,second=second,passed=True)
 finally:ctx.close();(dest/'report.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
 print(json.dumps({'case':r['name'],'passed':r.get('passed'),'changed':r.get('second',{}).get('changed')},ensure_ascii=False),flush=True)

server,url=shared.serve(a.package.resolve())
try:
 with sync_playwright() as pw:
  browser=pw.chromium.launch(executable_path=shared.chromium_executable(None),args=['--mute-audio'])
  if a.view_visual_only:view_visual_case(browser,url)
  else:
   if not a.inspection_only:
    for owner in a.owners:main_case(browser,url,owner)
   inspection_case(browser,url)
   if not a.inspection_only:view_visual_case(browser,url)
  browser.close()
 assert not R['errors'] and not R['httpErrors'],R;R['passed']=all(c.get('passed') for c in R['cases'])
except Exception:R['failure']=traceback.format_exc();raise
finally:server.shutdown();(OUT/'report.json').write_text(json.dumps(R,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print(json.dumps({'passed':R['passed'],'runtime':R['runtime'],'cases':len(R['cases']),'errors':R['errors'],'remoteDenied':len(R['blockedRemote'])},ensure_ascii=False))
