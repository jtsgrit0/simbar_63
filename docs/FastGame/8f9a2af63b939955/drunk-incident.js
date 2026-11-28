import {Audience} from './audience.js';
import {distance,walkable,pathfind,setCrowdRoute,move} from './navigation.js';
import {prepareCustomerExit,tickCustomerExit,SURFER_LOWER_SPEED} from './aftershow.js';

export const DRUNK_FIGHT_SECONDS=4;
const phases=new Set(['forming','fight','exit','done']);
const finite=(value,fallback=0,min=-20,max=20)=>Number.isFinite(+value)?Math.max(min,Math.min(max,+value)):fallback;
const customerId=id=>typeof id==='string'&&/^(?:patron-\d+|audience-\d+|arrival-\d+-\d+|freebird-wall-\d+|ff-table-\d+-guest-[01]|bar-seat-\d+|lounge-\d+|support-[0-2]|crowd-surfer)$/.test(id);
const find=(actors,id)=>actors.find(actor=>actor.id===id);
const clearPerformance=actor=>{for(const key of ['festivalAction','festivalSerial','festivalRemaining','festivalDuration','festivalLead','festivalSupport','festivalTarget','festivalLiftReady','festivalLiftElapsed','_festivalRouteRetry'])delete actor[key];actor._partner=null;actor._path=[];actor._pathAt=0;};

export function sanitizeDrunkIncident(value){
  if(!value||value.version!==1||!phases.has(value.phase)||!Array.isArray(value.participants))return null;
  const seen=new Set(),participants=[];
  for(const item of value.participants.slice(0,5)){
    if(!item||!customerId(item.id)||seen.has(item.id))continue;seen.add(item.id);
    participants.push({id:item.id,phase:item.phase==='gone'?'gone':'active',x:finite(item.x),z:finite(item.z),y:finite(item.y,0,0,3),ready:item.ready===true,
      standingReleased:item.standingReleased===true,egressPrepared:item.egressPrepared===true,egressAt:Math.trunc(finite(item.egressAt,0,0,3)),
      standGoal:item.standGoal&&Number.isFinite(item.standGoal.x)&&Number.isFinite(item.standGoal.z)?{x:finite(item.standGoal.x),z:finite(item.standGoal.z)}:null,
      egress:Array.isArray(item.egress)?item.egress.slice(0,3).filter(p=>Number.isFinite(p?.x)&&Number.isFinite(p?.z)).map(p=>({x:finite(p.x),z:finite(p.z)})):[]});
  }
  return{version:1,phase:participants.every(item=>item.phase==='gone')?'done':value.phase,elapsed:finite(value.elapsed,0,0,DRUNK_FIGHT_SECONDS),participants};
}
function pose(state,item,actor,index,actors){
  const gone=item.phase==='gone'||state.phase==='done';actor.drunkIncident=gone?'gone':state.phase==='exit'?'exit':state.phase==='fight'?'fight':'gather';
  actor.fightIndex=index;actor.fightElapsed=state.elapsed;actor.fightRemaining=Math.max(0,DRUNK_FIGHT_SECONDS-state.elapsed);
  actor.fightPartner=state.participants[(index+1)%state.participants.length]?.id||'';
  if(gone){actor.visible=false;actor.moving=false;return;}
  if(state.phase==='fight'){actor.action='fight';actor.moving=false;const partner=find(actors,actor.fightPartner);if(partner)actor.facing=Math.atan2(partner.x-actor.x,partner.z-actor.z);}
  else if(state.phase==='forming'&&item.ready){actor.action='listen';actor.moving=false;}
  else if(state.phase==='exit')actor.action='walk';
}
export function createDrunkIncident(actors,origin){
  const selected=origin?actors.filter(actor=>Audience.isAudience(actor)&&actor.visible!==false&&!actor.drunkIncident&&actor.id!==origin.id)
    .sort((a,b)=>distance(a,origin)-distance(b,origin)||a.id.localeCompare(b.id)).slice(0,5):[];
  const state={version:1,phase:selected.length?'forming':'done',elapsed:0,participants:selected.map(actor=>({id:actor.id,phase:'active',x:actor.x,z:actor.z,y:actor.y||0,ready:false,standingReleased:false,egressPrepared:false,egress:[],egressAt:0}))};
  for(const [index,item]of state.participants.entries()){
    const actor=find(actors,item.id);clearPerformance(actor);prepareCustomerExit(item,actor);
    item.ready=walkable(actor,false,false,actor.layout)&&(actor.y||0)<=.01&&(!actor.id.startsWith('support-')||(find(actors,'crowd-surfer')?.y||0)<=.01);
    if(item.ready){item.egress=[];item.egressAt=0;}pose(state,item,actor,index,actors);
  }
  if(state.participants.length&&state.participants.every(item=>item.ready)){state.phase='fight';for(const [index,item]of state.participants.entries())pose(state,item,find(actors,item.id),index,actors);}
  return state;
}
export function syncDrunkIncident(state,actors){
  if(!state)return;for(const item of state.participants){const actor=find(actors,item.id);if(actor){item.x=actor.x;item.z=actor.z;item.y=actor.y||0;}}
}
export function restoreDrunkIncident(state,actors){
  if(!state)return;
  for(const [index,item]of state.participants.entries()){
    const actor=find(actors,item.id);if(!actor){item.phase='gone';continue;}
    clearPerformance(actor);actor.x=item.x;actor.z=item.z;actor.y=item.y;
    if(item.standingReleased){actor.role='guest';actor.seat=false;actor.waitingSeat=false;actor.seatReleased=true;}
    actor.visible=item.phase!=='gone'&&state.phase!=='done';pose(state,item,actor,index,actors);
  }
}
function corridorDistance(point,item,actors){
  const actor=find(actors,item.id);if(!actor||item.ready||item.phase==='gone')return Infinity;
  let start=actor,best=Infinity;
  for(const goal of item.egress.slice(item.egressAt)){
    const dx=goal.x-start.x,dz=goal.z-start.z,size=dx*dx+dz*dz,t=size?Math.max(0,Math.min(1,((point.x-start.x)*dx+(point.z-start.z)*dz)/size)):0;
    best=Math.min(best,Math.hypot(point.x-start.x-t*dx,point.z-start.z-t*dz));start=goal;
  }
  return best;
}
function blocksEgress(point,item,actors,clearance=.48,from=null){
  if(corridorDistance(point,item,actors)<clearance)return true;
  const end=!item.ready&&item.phase!=='gone'&&item.egress.length>item.egressAt?item.egress.at(-1):null;
  // Keep the narrow turn after the chair exit clear too. A short yield may
  // clear it in several real steps rather than requiring one distant goal.
  const clearanceRadius=end?.z>2?1.6:1.05,radius=from?Math.min(clearanceRadius,distance(from,end||from)+.45):clearanceRadius;
  return!!end&&distance(point,end)<radius;
}
function yieldGoal(actor,item,state,actors){
  const candidates=[];
  for(const radius of [.65,.85,1])for(let turn=0;turn<16;turn++){
    const angle=turn*Math.PI/8,goal={x:actor.x+Math.cos(angle)*radius,z:actor.z+Math.sin(angle)*radius};
    if(!walkable(goal,false,false,actor.layout)||actors.some(other=>other!==actor&&other.visible!==false&&distance(goal,other)<.43))continue;
    if(state.participants.some(other=>other!==item&&(other.standGoal&&distance(goal,other.standGoal)<.5||blocksEgress(goal,other,actors,.5,actor))))continue;
    candidates.push(goal);
  }
  const people=actors.filter(other=>other!==actor&&other.visible!==false&&other.role!=='band'&&other.id!=='crowd-surfer').map(other=>({x:other.x,z:other.z}));
  return candidates.sort((a,b)=>distance(actor,a)-distance(actor,b)||a.z-b.z||a.x-b.x).find(goal=>pathfind(actor,goal,{people,layout:actor.layout}).length)||null;
}
function walkAside(actor,item,actors,dt){
  actor.action='walk';
  const nearby=actors.filter(other=>other===actor||other.visible!==false&&distance(actor,other)<1.6);
  actor._incidentRetry=Math.max(0,(actor._incidentRetry||0)-dt);
  if(!actor._incidentRetry&&(!actor._path?.length||actor._pathAt>=actor._path.length||distance({x:actor.targetX,z:actor.targetZ},item.standGoal)>.04||actor._blocked>.3)){setCrowdRoute(actor,item.standGoal,actors,false);actor._incidentRetry=.25;}
  const before={x:actor.x,z:actor.z};move(actor,dt,1.45,nearby);
  actor._incidentBlocked=distance(actor,before)>.001?0:(actor._incidentBlocked||0)+dt;
  if(actor._incidentBlocked>.8){item.ready=true;item.standGoal=null;actor._incidentRetry=0;actor._incidentBlocked=0;return;}
  if(distance(actor,item.standGoal)<=.05){item.ready=true;item.standGoal=null;actor._path=[];actor._pathAt=0;}
}
export function tickDrunkIncident(state,actors,dt){
  if(!state||state.phase==='done')return;
  if(state.phase==='forming'){
    const surfer=find(actors,'crowd-surfer'),stepped=new Set();
    for(const [index,item]of state.participants.entries()){
      const actor=find(actors,item.id);if(!actor||actor.visible===false){item.phase='gone';continue;}
      if((actor.y||0)>.01){actor.y=Math.max(0,actor.y-dt*SURFER_LOWER_SPEED);actor.action=actor.id==='crowd-surfer'?'surf':'listen';actor.moving=false;continue;}
      if(actor.id.startsWith('support-')&&(surfer?.y||0)>.01){actor.action='support';actor.moving=false;continue;}
      if(item.standGoal){walkAside(actor,item,actors,dt);stepped.add(item.id);continue;}
      if(!item.ready){
        if(!walkable(actor,false,false,actor.layout)){actor.action='walk';tickCustomerExit(item,actor,actors,dt,1.45);stepped.add(item.id);}
        if(walkable(actor,false,false,actor.layout)){item.ready=true;item.egress=[];item.egressAt=0;actor._path=[];actor._pathAt=0;}
      }
      pose(state,item,actor,index,actors);
    }
    // Authored chair exits can share a narrow lane. A person who has stood up
    // walks a short real route aside instead of blocking the next chair exit.
    for(const item of state.participants){
      const actor=find(actors,item.id);if(!actor||!item.ready||item.phase==='gone')continue;
      if(state.participants.some(other=>other!==item&&blocksEgress(actor,other,actors))){
        const goal=yieldGoal(actor,item,state,actors);if(goal){item.ready=false;item.standGoal=goal;actor._incidentRetry=0;if(!stepped.has(item.id))walkAside(actor,item,actors,dt);}
      }
    }
    if(state.participants.every(item=>item.phase==='gone'||item.ready)){state.phase='fight';state.elapsed=0;}
  }else if(state.phase==='fight'){
    state.elapsed=Math.min(DRUNK_FIGHT_SECONDS,state.elapsed+dt);
    if(state.elapsed>=DRUNK_FIGHT_SECONDS-1e-8){state.elapsed=DRUNK_FIGHT_SECONDS;state.phase='exit';}
  }
  for(const [index,item]of state.participants.entries()){
    const actor=find(actors,item.id);if(!actor||actor.visible===false){item.phase='gone';continue;}
    pose(state,item,actor,index,actors);
    if(state.phase==='exit'&&tickCustomerExit(item,actor,actors,dt)){
      item.phase='gone';actor.drunkIncident='gone';actor.visible=false;actor.moving=false;actor._path=[];actor._pathAt=0;
    }
  }
  if(state.participants.every(item=>item.phase==='gone'))state.phase='done';
  syncDrunkIncident(state,actors);
}
