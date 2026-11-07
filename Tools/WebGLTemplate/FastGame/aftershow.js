// Persisted movement and social choreography after the performance. No rewards
// or timers outside the active game clock are owned by this module.
import {distance,walkable,setRoute,move} from './navigation.js';

export const BAND_MEMBERS=['singer','guitarist','bassist','drummer'];
export const GREETING_SECONDS={highfive:1.1,hug:1.5};
const phases=new Set(['stage','approach','highfive','hug','await-exit','exit','gone']);
const audiencePhases=new Set(['gather','wait','greet','farewell-wait','exit','gone']);
const spots=[[3.05,.25],[2.8,-1.65],[7.7,.35],[8,-1.65],[3.1,-3.6],[4.05,-4.65],[7.65,-3.2],[8.25,-4.65]];
export const GREETING_AUDIENCE=spots.map((_,i)=>'patron-'+(8+i));
export const EXIT_ONLY_AUDIENCE=[...Array.from({length:8},(_,i)=>'patron-'+i),'support-0','support-1','support-2','crowd-surfer'];
export const AFTER_SHOW_AUDIENCE=[...GREETING_AUDIENCE,...EXIT_ONLY_AUDIENCE];
export const EXIT_POINT={x:8.8,z:-5.55};
export const SURFER_LOWER_SPEED=1.85;
const point=(x,z)=>({x,z});
const finite=(n,f=0,min=-20,max=20)=>Number.isFinite(+n)?Math.max(min,Math.min(max,+n)):f;
const integer=(n,max=100)=>Math.floor(finite(n,0,0,max));
const find=(actors,id)=>actors.find(a=>a.id===id);
const face=(a,b)=>{a.facing=Math.atan2(b.x-a.x,b.z-a.z);};
function stageRoute(id){
  const lane={singer:-.45,guitarist:-.15,bassist:.15,drummer:.45}[id]||0,x=5.35+lane;
  const first=id==='drummer'?[point(4.05,5.55),point(3.2,4.8),point(3.2,3.05)]:id==='guitarist'?[point(3.4,3.05)]:id==='bassist'?[point(7.75,3.05)]:[];
  return [...first,point(x,2.55),point(x,2.15),point(x,1.65),point(x,1.15)];
}

export function createAfterShow(actors,session){
  const members=BAND_MEMBERS.map((id,i)=>{
    const a=find(actors,id),onFloor=a&&walkable(a,false,false);
    return{id,phase:a?.visible===false?'gone':onFloor?'approach':'stage',waypoint:0,greeting:a?.visible===false?2:0,timer:0,delay:i*.7,
      x:a?.x??5.35,z:a?.z??3.5,stageLevel:onFloor?0:1,visible:a?.visible!==false,
      approachX:null,approachZ:null,blockedTime:0,standAttempt:0};
  });
  const audience=AFTER_SHOW_AUDIENCE.map((id,i)=>{
    const a=find(actors,id),exitOnly=i>=spots.length,[targetX,targetZ]=spots[i]||[a?.x??6,a?.z??-4.25];
    return{id,phase:a?.visible===false?'gone':exitOnly?'farewell-wait':'gather',exitOnly,
      homeX:a?.x??targetX,homeZ:a?.z??targetZ,homeFacing:a?.facing||0,
      x:a?.x??targetX,z:a?.z??targetZ,y:a?.y||0,targetX,targetZ,partner:'',visible:a?.visible!==false,capture:false};
  });
  return{version:2,session,started:true,completed:members.every(m=>m.phase==='gone')&&audience.every(a=>a.phase==='gone'),elapsed:0,
    departureStarted:false,departingActor:null,noticeAcknowledged:false,members,audience};
}

export function sanitizeAfterShow(value,session,{settled=false}={}){
  if(!value||![1,2].includes(value.version)||value.session!==session||!Array.isArray(value.members)||!Array.isArray(value.audience))return null;
  if(!BAND_MEMBERS.every(id=>value.members.some(m=>m?.id===id)))return null;
  const members=BAND_MEMBERS.map(id=>{
    const m=value.members.find(m=>m?.id===id);
    const phase=settled?'gone':value.version===1&&m.phase==='exit'?'await-exit':phases.has(m.phase)?m.phase:'stage';
    return {...m,id,phase,waypoint:integer(m.waypoint,8),greeting:Math.max(integer(m.greeting,2),['await-exit','exit','gone'].includes(phase)?2:0),
      timer:finite(m.timer,0,0,2),delay:finite(m.delay,0,0,3),x:finite(m.x,5.35),z:finite(m.z,3.5),
      stageLevel:finite(m.stageLevel,1,0,1),visible:phase!=='gone',
      approachX:Number.isFinite(m.approachX)?finite(m.approachX):null,approachZ:Number.isFinite(m.approachZ)?finite(m.approachZ):null,
      blockedTime:finite(m.blockedTime,0,0,3),standAttempt:integer(m.standAttempt,10000)};
  });
  const audience=AFTER_SHOW_AUDIENCE.map((id,i)=>{
    const old=value.audience.find(a=>a?.id===id),a=old||{},exitOnly=i>=spots.length;
    const [targetX,targetZ]=spots[i]||[finite(a.x,6),finite(a.z,-4.25)];
    const phase=settled?'gone':['return','done'].includes(a.phase)?'farewell-wait':audiencePhases.has(a.phase)?a.phase:exitOnly?'farewell-wait':'gather';
    return {...a,id,phase,exitOnly,homeX:finite(a.homeX,targetX),homeZ:finite(a.homeZ,targetZ),
      homeFacing:finite(a.homeFacing,0,-Math.PI,Math.PI),x:finite(a.x,targetX),z:finite(a.z,targetZ),y:finite(a.y,0,0,3),targetX,targetZ,
      partner:BAND_MEMBERS.includes(a.partner)?a.partner:'',visible:phase!=='gone',capture:!old&&!settled||a.capture===true};
  });
  const completed=members.every(m=>m.phase==='gone')&&audience.every(a=>a.phase==='gone');
  const departingActor=[...members,...audience].some(a=>a.id===value.departingActor&&a.phase==='exit')?value.departingActor:null;
  return{version:2,session,started:true,completed,elapsed:finite(value.elapsed,0,0,1e6),
    departureStarted:settled||value.departureStarted===true,departingActor,
    noticeAcknowledged:settled||value.noticeAcknowledged===true,members,audience};
}

export function syncAfterShow(state,actors){
  if(!state)return;
  for(const item of [...state.members,...state.audience]){
    const a=find(actors,item.id);if(!a)continue;item.x=a.x;item.z=a.z;item.y=a.y||0;item.visible=a.visible!==false;
    if(BAND_MEMBERS.includes(item.id)){item.stageLevel=a.stageLevel??item.stageLevel;item.visible=a.visible!==false;}
  }
}

export function restoreAfterShow(state,actors){
  if(!state)return;
  for(const item of [...state.members,...state.audience]){
    const a=find(actors,item.id);if(!a)continue;
    // New departure-only participants in a v1 save retain their actual saved
    // positions and surfing height; import must not snap them to default spots.
    if(item.capture){item.x=a.x;item.z=a.z;item.y=a.y||0;item.homeX=a.x;item.homeZ=a.z;item.capture=false;}
    a.x=item.x;a.z=item.z;a.y=item.y||0;a.moving=false;a.actionProgress=0;a.visible=item.phase!=='gone';
    if(BAND_MEMBERS.includes(item.id)){
      a.stageLevel=item.stageLevel;a.visible=item.phase!=='gone';a.afterShowPhase=item.phase;
      a.action=['highfive','hug'].includes(item.phase)?item.phase:'aftershow-wait';
      a.actionProgress=GREETING_SECONDS[item.phase]?Math.min(1,item.timer/GREETING_SECONDS[item.phase]):0;
      const target=state.audience[state.members.indexOf(item)*2+Math.min(1,item.greeting)];
      a._partner=['approach','highfive','hug'].includes(item.phase)?target?.id:null;
    }else{a._partner=item.partner||null;a.action=item.id==='crowd-surfer'&&a.y>.1?'surf':item.phase==='greet'?find(actors,item.partner)?.action||'aftershow-wait':'aftershow-wait';}
  }
}

export function ownsAfterShowActor(state,id){
  return !!state&&(state.members.some(m=>m.id===id)||state.audience.some(a=>a.id===id));
}

// On the public floor the musicians also occupy space. Stage traversal is
// handled by explicit authored waypoints because the normal aisle mesh excludes it.
function bodies(actors,actor){return actors.map(a=>a!==actor&&a.role==='band'&&(a.stageLevel||0)<.05?{...a,role:'aftershow-band'}:a);}
function clearStand(a,goal,actors){return bodies(actors,a).every(other=>other.id===a.id||other.visible===false||other.role==='band'||other.role==='seated'||other.id===a._partner||other.id==='crowd-surfer'||Math.abs((other.y||0)-(a.y||0))>=1||distance(other,goal)>=.4);}
function route(a,goal,actors,dt,speed=1.3,arrival=.2){
  a._afterShowRetry=Math.max(0,(a._afterShowRetry||0)-dt);
  const changed=Math.hypot((a.targetX??a.x)-goal.x,(a.targetZ??a.z)-goal.z)>.04;
  if(distance(a,goal)<=arrival){a.moving=false;return true;}
  if(changed||((!a._path?.length||a._pathAt>=a._path.length)&&!a._afterShowRetry)){
    setRoute(a,goal,bodies(actors,a),false);a._afterShowRetry=.35;
  }
  move(a,dt,speed,bodies(actors,a));
  return distance(a,goal)<=arrival;
}
function walkStage(a,m,dt){
  if(m.delay>0){m.delay=Math.max(0,m.delay-dt);a.action='aftershow-wait';a.moving=false;return;}
  const waypoints=stageRoute(m.id);let budget=dt*1.3;a.moving=false;
  while(m.waypoint<waypoints.length&&budget>0){
    const goal=waypoints[m.waypoint],d=distance(a,goal);
    if(d<.0001){m.waypoint++;continue;}
    const step=Math.min(d,budget);face(a,goal);a.x+=(goal.x-a.x)*step/d;a.z+=(goal.z-a.z)*step/d;budget-=step;a.moving=true;
    if(step>=d-.0001)m.waypoint++;
  }
  a.action='walk';a.y=0;a.stageLevel=a.z>=2.2?1:a.z>=1.65?.3+.7*(a.z-1.65)/.55:Math.max(0,.3*(a.z-1.15)/.5);
  if(m.waypoint>=waypoints.length){a.stageLevel=0;m.phase='approach';a._path=[];a._pathAt=0;m.approachX=null;m.approachZ=null;}
}
function findApproach(a,fan,item,m,actors){
  // Prefer a side-on greeting so the drawn palms and embracing arms remain
  // visible. The world axis projects horizontally in the illustrated room.
  const radius=m.greeting===0?.60:.43,axis=Math.atan2(.461,.887);
  const side=(a.x-fan.x)*.887+(a.z-fan.z)*.461<0?Math.PI:0,base=axis+side;
  a._partner=fan.id;
  for(let offset=0;offset<16;offset++){
    const index=(offset+(m.standAttempt||0))%16,turn=index===0?0:Math.ceil(index/2)*(index%2?1:-1);
    const angle=base+turn*Math.PI/8,goal=point(fan.x+Math.cos(angle)*radius,fan.z+Math.sin(angle)*radius);
    if(walkable(goal,false,false)&&clearStand(a,goal,actors)&&setRoute(a,goal,bodies(actors,a),false))return goal;
  }
  return null;
}
function releaseFan(item,fan){
  item.phase='farewell-wait';item.partner='';fan._partner=null;fan.actionProgress=0;fan._path=[];fan._pathAt=0;fan._afterShowRetry=0;fan.action='aftershow-wait';fan.moving=false;
}

function beginDepartures(state,actors){
  if(state.departureStarted||!state.members.every(m=>m.greeting>=2||m.phase==='gone'))return;
  // A crowd surfer is put down before anyone starts leaving. This also works
  // when an old save resumes directly after all greetings have finished.
  if(state.audience.some(item=>item.id==='crowd-surfer'&&(find(actors,item.id)?.y||0)>.01))return;
  state.departureStarted=true;state.departingActor=null;
  for(const item of [...state.members,...state.audience]){
    if(item.phase==='gone')continue;item.phase='exit';item.partner='';
    const a=find(actors,item.id);if(!a)continue;
    a._partner=null;a._path=[];a._pathAt=0;a._afterShowRetry=0;a.action='aftershow-wait';a.moving=false;
  }
}

function tickDepartures(state,actors,dt){
  const items=[...state.members,...state.audience];
  if(!items.some(item=>item.id===state.departingActor&&item.phase==='exit'))state.departingActor=null;
  if(!state.departingActor){
    // One clear doorway turn at a time prevents opposing routes from changing
    // underneath each other. Nearest first empties the narrow exit area first.
    const pending=items.filter(item=>item.phase==='exit').map(item=>({item,a:find(actors,item.id)}));
    for(const entry of pending)if(!entry.a||entry.a.visible===false)entry.item.phase='gone';
    const next=pending.filter(entry=>entry.a?.visible!==false&&entry.a).sort((a,b)=>distance(a.a,EXIT_POINT)-distance(b.a,EXIT_POINT)||a.item.id.localeCompare(b.item.id))[0];
    state.departingActor=next?.item.id||null;
  }
  for(const item of items){
    const a=find(actors,item.id);if(!a)continue;
    a._partner=null;a.actionProgress=0;
    if(item.phase==='gone'){a.visible=false;a.moving=false;continue;}
    a.y=0;a.stageLevel=0;
    if(item.id!==state.departingActor){a.action='aftershow-wait';a.moving=false;continue;}
    a.action='walk';
    if(route(a,EXIT_POINT,actors,dt,BAND_MEMBERS.includes(item.id)?1.65:1.45,.18)){
      item.phase='gone';item.visible=false;a.visible=false;a.moving=false;a._path=[];a._pathAt=0;
      if(BAND_MEMBERS.includes(item.id))a.afterShowPhase='gone';
      state.departingActor=null;
    }
  }
}

export function tickAfterShow(state,actors,dt){
  if(!state)return;
  state.elapsed+=dt;
  if(state.departureStarted){tickDepartures(state,actors,dt);state.completed=[...state.members,...state.audience].every(item=>item.phase==='gone');syncAfterShow(state,actors);return;}
  const surfer=find(actors,'crowd-surfer');
  if(surfer&&state.audience.some(item=>item.id===surfer.id&&item.phase!=='gone'))surfer.y=Math.max(0,(surfer.y||0)-dt*SURFER_LOWER_SPEED);
  for(const item of state.audience){
    const fan=find(actors,item.id);if(!fan)continue;
    if(item.phase==='gone'){fan.visible=false;fan.moving=false;continue;}
    if(item.exitOnly){
      fan.moving=false;fan.action=item.id==='crowd-surfer'&&fan.y>.1?'surf':item.id.startsWith('support-')&&surfer?.y>.1?'support':'aftershow-wait';
      continue;
    }
    if(item.phase==='gather'){
      fan.action='walk';if(route(fan,point(item.targetX,item.targetZ),actors,dt,1.15,.30)){item.phase='wait';fan.action='aftershow-wait';fan.moving=false;}
    }else if(item.phase==='wait'){fan.action='aftershow-wait';fan.moving=false;const m=state.members.find(m=>m.id===item.partner);const member=m&&find(actors,m.id);if(member)face(fan,member);}
    else if(item.phase==='farewell-wait'){fan.action='aftershow-wait';fan.moving=false;}
  }
  for(let i=0;i<state.members.length;i++){
    const m=state.members[i],a=find(actors,m.id);if(!a)continue;
    a.afterShowPhase=m.phase;a.actionProgress=0;
    if(m.phase==='gone'){a.visible=false;a.moving=false;continue;}
    if(m.phase==='stage'){walkStage(a,m,dt);continue;}
    if(m.phase==='await-exit'||m.phase==='exit'){m.phase='await-exit';a.action='aftershow-wait';a._partner=null;a.moving=false;continue;}
    const item=state.audience[i*2+Math.min(1,m.greeting)],fan=item&&find(actors,item.id);
    if(!fan||item.phase==='gone'){m.greeting++;m.phase=m.greeting>=2?'await-exit':'approach';continue;}
    a._partner=fan.id;fan._partner=a.id;item.partner=a.id;
    if(m.phase==='approach'){
      // Gather can end slightly short of the authored mark because another
      // audience member occupies its edge. Plan around the fan's settled
      // position; a route chosen while that fan is walking becomes stale.
      if(item.phase!=='wait'){a.action='aftershow-wait';a.moving=false;continue;}
      const radius=m.greeting===0?.60:.43;
      if(m.approachX!=null&&(Math.abs(distance(point(m.approachX,m.approachZ),fan)-radius)>.1||!clearStand(a,point(m.approachX,m.approachZ),actors))){m.approachX=null;m.approachZ=null;}
      if(m.approachX==null||m.approachZ==null){const p=findApproach(a,fan,item,m,actors);if(p){m.approachX=p.x;m.approachZ=p.z;}}
      const arrived=m.approachX!=null&&route(a,point(m.approachX,m.approachZ),actors,dt,1.3,.04);
      a.action=arrived?'aftershow-wait':'walk';
      m.blockedTime=a.moving?0:(m.blockedTime||0)+dt;
      if(!arrived&&m.blockedTime>2){m.standAttempt=(m.standAttempt||0)+1;m.approachX=null;m.approachZ=null;m.blockedTime=0;}
      if(arrived&&item.phase==='wait'&&distance(a,fan)<(m.greeting===0?1.2:.85)){
        m.phase=m.greeting===0?'highfive':'hug';m.timer=0;item.phase='greet';a._path=[];fan._path=[];a.moving=false;fan.moving=false;
      }
    }
    if(m.phase==='highfive'||m.phase==='hug'){
      m.timer+=dt;const duration=GREETING_SECONDS[m.phase],progress=Math.min(1,m.timer/duration);
      a.action=m.phase;fan.action=m.phase;a.actionProgress=progress;fan.actionProgress=progress;a.moving=false;fan.moving=false;face(a,fan);face(fan,a);
      if(m.timer>=duration){releaseFan(item,fan);m.greeting++;m.timer=0;m.phase=m.greeting>=2?'await-exit':'approach';m.approachX=null;m.approachZ=null;a._partner=null;a._path=[];a._pathAt=0;}
    }
  }
  beginDepartures(state,actors);
  state.completed=[...state.members,...state.audience].every(item=>item.phase==='gone');
  syncAfterShow(state,actors);
}
