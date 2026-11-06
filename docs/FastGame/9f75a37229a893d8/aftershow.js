// Persisted movement and social choreography after the performance. No rewards
// or timers outside the active game clock are owned by this module.
import {distance,walkable,setRoute,move} from './navigation.js';

export const BAND_MEMBERS=['singer','guitarist','bassist','drummer'];
export const GREETING_SECONDS={highfive:1.1,hug:1.5};
const phases=new Set(['stage','approach','highfive','hug','exit','gone']);
const audiencePhases=new Set(['gather','wait','greet','return','done']);
const spots=[[3.05,.25],[2.8,-1.65],[7.7,.35],[8,-1.65],[3.1,-3.6],[4.05,-4.65],[7.65,-3.2],[8.25,-4.65]];
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
    return{id,phase:a?.visible===false?'gone':onFloor?'approach':'stage',waypoint:0,greeting:0,timer:0,delay:i*.7,
      x:a?.x??5.35,z:a?.z??3.5,stageLevel:onFloor?0:1,visible:a?.visible!==false,
      approachX:null,approachZ:null,blockedTime:0,standAttempt:0};
  });
  const audience=spots.map(([targetX,targetZ],i)=>{
    const id='patron-'+(8+i),a=find(actors,id);
    return{id,phase:'gather',homeX:a?.x??targetX,homeZ:a?.z??targetZ,homeFacing:a?.facing||0,
      x:a?.x??targetX,z:a?.z??targetZ,targetX,targetZ,partner:''};
  });
  return{version:1,session,started:true,completed:members.every(m=>m.phase==='gone')&&audience.every(a=>a.phase==='done'),elapsed:0,returningFan:null,members,audience};
}

export function sanitizeAfterShow(value,session){
  if(!value||value.version!==1||value.session!==session||!Array.isArray(value.members)||!Array.isArray(value.audience))return null;
  if(!BAND_MEMBERS.every(id=>value.members.some(m=>m?.id===id)))return null;
  const members=BAND_MEMBERS.map(id=>{
    const m=value.members.find(m=>m?.id===id);
    return {...m,id,phase:phases.has(m.phase)?m.phase:'stage',waypoint:integer(m.waypoint,8),greeting:integer(m.greeting,2),
      timer:finite(m.timer,0,0,2),delay:finite(m.delay,0,0,3),x:finite(m.x,5.35),z:finite(m.z,3.5),
      stageLevel:finite(m.stageLevel,1,0,1),visible:m.phase!=='gone',
      approachX:Number.isFinite(m.approachX)?finite(m.approachX):null,approachZ:Number.isFinite(m.approachZ)?finite(m.approachZ):null,
      blockedTime:finite(m.blockedTime,0,0,3),standAttempt:integer(m.standAttempt,10000)};
  });
  const audience=spots.map(([targetX,targetZ],i)=>{
    const id='patron-'+(8+i),a=value.audience.find(a=>a?.id===id)||{};
    return {...a,id,phase:audiencePhases.has(a.phase)?a.phase:'gather',homeX:finite(a.homeX,targetX),homeZ:finite(a.homeZ,targetZ),
      homeFacing:finite(a.homeFacing,0,-Math.PI,Math.PI),x:finite(a.x,targetX),z:finite(a.z,targetZ),targetX,targetZ,
      partner:BAND_MEMBERS.includes(a.partner)?a.partner:''};
  });
  return{version:1,session,started:true,completed:members.every(m=>m.phase==='gone')&&audience.every(a=>a.phase==='done'),elapsed:finite(value.elapsed,0,0,1e6),returningFan:audience.some(a=>a.id===value.returningFan&&a.phase==='return')?value.returningFan:null,members,audience};
}

export function syncAfterShow(state,actors){
  if(!state)return;
  for(const item of [...state.members,...state.audience]){
    const a=find(actors,item.id);if(!a)continue;item.x=a.x;item.z=a.z;
    if(BAND_MEMBERS.includes(item.id)){item.stageLevel=a.stageLevel??item.stageLevel;item.visible=a.visible!==false;}
  }
}

export function restoreAfterShow(state,actors){
  if(!state)return;
  for(const item of [...state.members,...state.audience]){
    const a=find(actors,item.id);if(!a)continue;a.x=item.x;a.z=item.z;a.y=0;a.moving=false;a.actionProgress=0;
    if(BAND_MEMBERS.includes(item.id)){
      a.stageLevel=item.stageLevel;a.visible=item.phase!=='gone';a.afterShowPhase=item.phase;
      a.action=['highfive','hug'].includes(item.phase)?item.phase:'aftershow-wait';
      a.actionProgress=GREETING_SECONDS[item.phase]?Math.min(1,item.timer/GREETING_SECONDS[item.phase]):0;
      const target=state.audience[state.members.indexOf(item)*2+Math.min(1,item.greeting)];
      a._partner=['approach','highfive','hug'].includes(item.phase)?target?.id:null;
    }else{a._partner=item.partner||null;a.action=item.phase==='greet'?find(actors,item.partner)?.action||'aftershow-wait':'aftershow-wait';}
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
  item.phase='return';item.partner='';fan._partner=null;fan.actionProgress=0;fan._path=[];fan._pathAt=0;fan._afterShowRetry=0;fan.action='walk';
}

export function tickAfterShow(state,actors,dt){
  if(!state)return;
  state.elapsed+=dt;
  // Let each fan finish their walk home before the next one leaves the
  // greeting spot. Opposing home routes then remain clear in the busy room.
  if(!state.audience.some(item=>item.id===state.returningFan&&item.phase==='return'))state.returningFan=state.audience.find(item=>item.phase==='return')?.id||null;
  const yielding=new Set();
  for(let turn=0;turn<state.audience.length&&state.returningFan;turn++){
    const active=state.audience.find(item=>item.id===state.returningFan),home=point(active.homeX,active.homeZ);
    yielding.add(active.id);
    const blocker=state.audience.find(item=>item.phase==='return'&&item.id!==active.id&&!yielding.has(item.id)&&distance(find(actors,item.id)||home,home)<.4);
    if(!blocker)break;state.returningFan=blocker.id;
  }
  for(const item of state.audience){
    const fan=find(actors,item.id);if(!fan)continue;
    if(item.phase==='gather'){
      fan.action='walk';if(route(fan,point(item.targetX,item.targetZ),actors,dt,1.15,.30)){item.phase='wait';fan.action='aftershow-wait';fan.moving=false;}
    }else if(item.phase==='wait'){fan.action='aftershow-wait';fan.moving=false;const m=state.members.find(m=>m.id===item.partner);const member=m&&find(actors,m.id);if(member)face(fan,member);}
    else if(item.phase==='return'){
      if(item.id!==state.returningFan){fan.action='aftershow-wait';fan.moving=false;continue;}
      fan.action='walk';if(route(fan,point(item.homeX,item.homeZ),actors,dt,1.15,.02)){item.phase='done';state.returningFan=null;fan.action='idle';fan.moving=false;fan.facing=item.homeFacing;fan._path=[];fan._pathAt=0;fan._wait=30;}
    }
  }
  for(let i=0;i<state.members.length;i++){
    const m=state.members[i],a=find(actors,m.id);if(!a)continue;
    a.afterShowPhase=m.phase;a.actionProgress=0;
    if(m.phase==='gone'){a.visible=false;a.moving=false;continue;}
    if(m.phase==='stage'){walkStage(a,m,dt);continue;}
    if(m.phase==='exit'){
      a.action='walk';a._partner=null;a.stageLevel=0;
      if(route(a,point(8.8,-5.55),actors,dt,1.65)){m.phase='gone';m.visible=false;a.visible=false;a.moving=false;a.afterShowPhase='gone';}
      continue;
    }
    const item=state.audience[i*2+Math.min(1,m.greeting)],fan=item&&find(actors,item.id);
    if(!fan){m.greeting++;m.phase=m.greeting>=2?'exit':'approach';continue;}
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
      if(m.timer>=duration){releaseFan(item,fan);m.greeting++;m.timer=0;m.phase=m.greeting>=2?'exit':'approach';m.approachX=null;m.approachZ=null;a._partner=null;a._path=[];a._pathAt=0;}
    }
  }
  state.completed=state.members.every(m=>m.phase==='gone')&&state.audience.every(a=>a.phase==='done');
  syncAfterShow(state,actors);
}
