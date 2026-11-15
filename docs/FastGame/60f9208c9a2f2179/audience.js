import {AUDIENCE_VENUES} from './owner-venues.js';
import {walkable,reserved,FF_LAYOUT,FREEBIRD_LAYOUT} from './navigation.js';

const TAU=Math.PI*2,MIN_SPACING=.42;
const whole=(value,min=0,max=1e9)=>Math.trunc(Math.max(min,Math.min(max,Number.isFinite(+value)?+value:min)));
const ownerIndex=owner=>Number.isInteger(owner)&&owner>=0&&owner<3?owner:2;
const names=['Mina','Jae','Sora','Noel','Dani','Yuna','Theo','Ari','Ivy','Rin'];
const wardrobes=['Casual','Suit','Beach','Worker','Farmer','Adventurer','Casual2','Punk'];
const distance=(a,b)=>Math.hypot(a.x-b.x,a.z-b.z);
const layoutFor=owner=>owner===0?FREEBIRD_LAYOUT:owner===1?FF_LAYOUT:null;
const originalQueue=[[-1.5,-1.75],[-.85,-2.25],[-.15,-2.7],[.6,-3.1],[1.4,-3.5],[2.2,-3.7],[3,-4]].map(([x,z])=>({x,z}));
const hash=(...values)=>{
  let value=2166136261;
  for(const part of values)for(const char of String(part)){value=Math.imul(value^char.charCodeAt(0),16777619)>>>0;}
  return value||1;
};
const random=seed=>()=>{seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed/4294967296;};
const round=value=>Math.round(value*10000)/10000;
const facing=(x,z)=>Math.atan2(5.75-x,3.5-z);

function clearStaffAisles(point,owner){
  // Keep the exit and the service queue clear even before orders arrive.
  if(point.x>8.15&&point.z< -2.6)return false;
  if(distance(point,{x:8.7,z:-5.4})<.7)return false;
  const layout=layoutFor(owner);
  if(layout){
    if(layout.orderSlots.some(slot=>distance(point,slot)<.58))return false;
    for(const id of ['bartender','server','barback','floorhost'])if(layout[id]&&distance(point,layout[id])<.65)return false;
  }else{
    if(originalQueue.some(slot=>distance(point,slot)<.58)||distance(point,{x:-.4,z:-4.9})<.65)return false;
  }
  if(owner===0&&point.x> -7.45&&point.x< -6.15&&point.z>1.2)return false;
  if(owner===1&&point.x> -2.3&&point.x<1.95&&point.z>1.4)return false;
  return true;
}

function floorCandidates(owner,seed){
  const profile=AUDIENCE_VENUES[owner],layout=layoutFor(owner),next=random(seed),candidates=[];
  // Jittered grid points retain at least .48m spacing before the final pairwise
  // check. Selection is shuffled within the venue's distinct social areas.
  for(const zone of profile.zones){
    for(let z=zone.z0;z<=zone.z1+.001;z+=.74)for(let x=zone.x0;x<=zone.x1+.001;x+=.74){
      const point={x:round(x+(next()-.5)*.22),z:round(z+(next()-.5)*.22)};
      if(!walkable(point,false,true,layout?.id)||reserved(point)||!clearStaffAisles(point,owner))continue;
      candidates.push({...point,zone:zone.key,score:next()/zone.weight});
    }
  }
  return candidates.sort((a,b)=>a.score-b.score||a.z-b.z||a.x-b.x);
}

function standingBehavior(owner,showId,next){
  const roll=next();
  if(owner===2)return roll<.62?'listen':'sway';
  if(showId==='july')return roll<.35?'listen':roll<.9?'sway':'cheer';
  if(owner===0)return roll<.3?'listen':roll<.65?'sway':roll<.86?'headbang':'cheer';
  return roll<.18?'listen':roll<.48?'sway':roll<.83?'headbang':'cheer';
}

function build({owner=2,attendance=0,showId='freewill',day=1,session=1}={}){
  owner=ownerIndex(owner);
  const profile=AUDIENCE_VENUES[owner],total=whole(attendance,0,profile.capacity),seed=hash(owner,day,session,showId),next=random(seed),layout=layoutFor(owner),actors=[];
  const make=(id,role,slot,point,behavior,extra={})=>{
    const descriptor={id,name:names[slot%names.length]||'Guest',role,slot,audienceVersion:1,behavior,behaviorPhase:round(next()*TAU),animationSpeed:round(.72+next()*.56),
      x:point.x,z:point.z,y:0,homeX:point.x,homeZ:point.z,targetX:point.x,targetZ:point.z,facing:point.facing??facing(point.x,point.z),
      moving:false,action:behavior,visible:true,_path:[],_pathAt:0,_wait:round(2+next()*5),...extra};
    if(layout){descriptor.layout=layout.id;descriptor.layoutVersion=layout.version;}
    actors.push(descriptor);return descriptor;
  };

  // These eleven locations are authored chairs/sofas, intentionally outside
  // the walking floor. Standing guests are always tested against navigation.
  for(const [index,point]of (profile.seats||[]).entries()){
    if(actors.length>=total)break;
    make(point.id,'seated',100+index,point,index%3===0?'listen':'sway',
      {seat:true,...(owner===0?{waitingSeat:true,waitRemaining:point.waitSeconds??12+index*3.2,styleKey:index%2?'Suit':'Casual'}:{} )});
  }

  for(const [index,point]of (profile.standingSpots||[]).entries()){
    if(actors.length>=total)break;
    if(!walkable(point,false,true,layout?.id)||actors.some(actor=>distance(actor,point)<MIN_SPACING))continue;
    make(point.id,'guest',120+index,point,point.behavior||'dance',{tableId:point.tableId,audienceZone:'standing-tables'});
  }

  const energetic=showId!=='july'&&owner!==2;
  const moshCount=!energetic?0:owner===1&&total>=16?6:owner===0&&total>=18?Math.min(6,3+Math.floor((total-18)/8)):0;
  const slamCount=energetic&&owner===1&&total>=16?2:0;
  const surfTeam=showId==='double'&&owner!==2&&total>=(owner===0?28:32);
  let guestSlot=0;
  const actualMosh=Math.min(moshCount,total-actors.length);
  for(let index=0;index<actualMosh;index++){
    const angle=index*TAU/actualMosh,point={x:round(5.75+Math.cos(angle)*1.58),z:round(-1.25+Math.sin(angle)*1.58)};
    make('patron-'+guestSlot,'guest',guestSlot++,point,'mosh',{pitIndex:index,pitCount:actualMosh,pitRadius:1.58,pitAngle:angle});
  }
  for(let index=0;index<slamCount&&actors.length<total;index++){
    const side=index===0?-1:1;
    make('patron-'+guestSlot,'guest',guestSlot++,{x:5.75+side*.68,z:-1.25},'slam',{slamSide:side});
  }
  if(surfTeam&&total-actors.length>=4){
    for(let index=0;index<3;index++)make('support-'+index,'support',200+index,{x:6+(index===0?-.65:index===1?.65:0),z:-4.25+(index===2?-.65:0)},'support');
    make('crowd-surfer','surfer',203,{x:6,z:-4.25},'surf');
  }

  const candidates=floorCandidates(owner,seed^0x9e3779b9);
  const frontReservations=[];
  for(const actor of actors.filter(a=>a.waitingSeat)){
    const target=candidates.find(p=>p.zone==='front-floor'&&clearAudiencePoint(p,actors,{front:true})
      &&frontReservations.every(other=>distance(p,other)>=1));
    if(target){actor.seatStageTarget={x:target.x,z:target.z};frontReservations.push(target);}
  }
  for(const point of candidates){
    if(actors.length>=total)break;
    if(frontReservations.some(target=>distance(point,target)<.78))continue;
    if(actors.some(actor=>distance(actor,point)<MIN_SPACING))continue;
    const slot=guestSlot++,id=slot<16?'patron-'+slot:'audience-'+slot;
    make(id,'guest',slot,point,standingBehavior(owner,showId,next),{audienceZone:point.zone});
  }
  if(actors.length!==total)throw new Error(`Audience floor ${profile.key} has ${actors.length} safe slots for ${total} guests`);
  return actors;
}

function clearAudiencePoint(point,actors,{front=false}={}){
  if(actors.some(actor=>actor.visible!==false&&distance(actor,point)<.57))return false;
  if(actors.some(actor=>actor.visible!==false&&Number.isFinite(actor.homeX)&&distance({x:actor.homeX,z:actor.homeZ},point)<.53))return false;
  if(actors.some(actor=>actor.visible!==false&&actor.waitingSeat&&actor.seatStageTarget&&distance(actor.seatStageTarget,point)<.78))return false;
  if(actors.some(actor=>['mosh','slam'].includes(actor.behavior))&&distance(point,{x:5.75,z:-1.25})<2.25)return false;
  if(actors.some(actor=>actor.behavior==='surf')&&distance(point,{x:6,z:-4.25})<1.25)return false;
  return !front||point.x>=.6;
}

function frontTarget({owner=0,actor,actors=[],day=1,session=1}={}){
  owner=ownerIndex(owner);
  const target=actor?.seatStageTarget;
  if(target&&walkable(target,false,true,layoutFor(owner)?.id))return actors.every(a=>a===actor||a.visible===false||distance(a,target)>=.48)?target:null;
  const points=floorCandidates(owner,hash(owner,day,session,actor?.id||'', 'front'));
  return points.filter(p=>['front-floor','stage-floor','listening-floor'].includes(p.zone)&&clearAudiencePoint(p,actors.filter(a=>a!==actor),{front:true}))
    .sort((a,b)=>distance(a,actor)-distance(b,actor))[0]||null;
}

function safePoint({owner=0,actor,actors=[],day=1,session=1}={}){
  owner=ownerIndex(owner);
  return floorCandidates(owner,hash(owner,day,session,actor?.id||'','migrate'))
    .filter(p=>clearAudiencePoint(p,actors.filter(a=>a!==actor))).sort((a,b)=>distance(a,actor)-distance(b,actor))[0]||null;
}

function arrival({owner=2,showId='freewill',day=1,session=1,serial=1,actors=[]}={}){
  owner=ownerIndex(owner);const seed=hash(owner,day,session,serial,'arrival'),next=random(seed),layout=layoutFor(owner);
  const point=floorCandidates(owner,seed).find(point=>clearAudiencePoint(point,actors));if(!point)return null;
  const slot=300+serial,behavior=standingBehavior(owner,showId,next);
  return{id:`arrival-${session}-${serial}`,name:names[slot%names.length],role:'guest',slot,audienceVersion:1,
    audienceArrival:true,audienceZone:point.zone,behavior,behaviorPhase:round(next()*TAU),animationSpeed:round(.72+next()*.56),
    x:point.x,z:point.z,y:0,homeX:point.x,homeZ:point.z,targetX:point.x,targetZ:point.z,facing:facing(point.x,point.z),
    moving:false,action:behavior,visible:true,_path:[],_pathAt:0,_wait:3,
    ...(layout?{layout:layout.id,layoutVersion:layout.version}:{})};
}

export const Audience={
  version:1,
  profile:owner=>AUDIENCE_VENUES[ownerIndex(owner)],
  capacity:owner=>AUDIENCE_VENUES[ownerIndex(owner)].capacity,
  demand:owner=>AUDIENCE_VENUES[ownerIndex(owner)].demandMultiplier,
  isAudience:actor=>!!actor&&['guest','seated','support','surfer'].includes(actor.role),
  wardrobe:actor=>actor.styleKey||wardrobes[((actor.slot||0)%wardrobes.length+wardrobes.length)%wardrobes.length],
  build,arrival,frontTarget,safePoint
};
