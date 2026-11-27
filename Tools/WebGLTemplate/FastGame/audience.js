import {AUDIENCE_VENUES} from './owner-venues.js';
import {walkable,reserved,FF_LAYOUT,FREEBIRD_LAYOUT} from './navigation.js';

const TAU=Math.PI*2,MIN_SPACING=.42;
const whole=(value,min=0,max=1e9)=>Math.trunc(Math.max(min,Math.min(max,Number.isFinite(+value)?+value:min)));
const ownerIndex=owner=>Number.isInteger(owner)&&owner>=0&&owner<3?owner:2;
const nameGroups=[
  ["\uae40\uc11c\uc5f0","\ubc15\uc9c0\ud6c8","\uc774\ubbfc\uc900","\ucd5c\uc720\uc9c4","\uc815\ud558\ub298","\ud55c\uc9c0\uc6b0","\uc624\uc608\ub9b0","\uac15\ud604\uc6b0","\uc724\uc218\ube48","\uc784\ub3c4\uc724","\uc1a1\uc9c0\ubbfc","\uc870\ub2e4\uc740"],
  ["Oliver Bennett","Amelia Clarke","Alfie Turner","Poppy Hughes","Freddie Walker","Isla Campbell","Arthur Davies","Florence Reed","Ivy Morgan","Harry Wilson","Maisie Fletcher","Theo Brooks"],
  ["Maya Johnson","Jordan Rivera","Riley Carter","Zoe Mitchell","Ethan Parker","Avery Coleman","Miles Dawson","Nora Brooks","Sofia Ramirez","Caleb Foster","Taylor Reed","Morgan Hayes"],
  ["Lena M\u00fcller","Jonas Weber","Clara Fischer","Lukas Wagner","Emilia Becker","Noah Hoffmann","Mila Schulz","Felix Braun","Lea Klein","Oskar Neumann","Ida Kr\u00fcger","Anton Berg"],
  ["Camille Laurent","Hugo Bernard","L\u00e9a Dubois","Louis Moreau","Manon Petit","Jules Lef\u00e8vre","In\u00e8s Girard","Arthur Roux","Emma Fournier","Lucie Simon","Ma\u00ebl Fontaine","Chlo\u00e9 Garnier"],
  ["Luc\u00eda Moreno","Mateo Vega","Carmen Ruiz","Pablo Mart\u00edn","Noa Romero","Alba Navarro","Hugo Morales","Irene Santos","Diego Rojas","Vega Castillo","Elena Le\u00f3n","Adri\u00e1n Serrano"],
  ["Yui Tanaka","Ren Nakamura","Sora Ito","Hana Suzuki","Haru Sato","Aoi Kato","Yuna Yamamoto","Yuto Mori","Rin Ishikawa","Nao Fujita","Mio Watanabe","Kei Hayashi"],
  ["Sofia Rossi","Luca Romano","Giulia Marino","Matteo Costa","Alice Ricci","Marco Bruno","Elena Gallo","Pietro Conti","Anna Leone","Nico Rizzo","Chiara Greco","Leo Ferrari"]
];
const wardrobes=['Casual','Suit','Beach','Worker','Farmer','Adventurer','Casual2','Punk'];
// Use the colors already authored in both walking and gesture atlases.
const wardrobeWeights=[
  [3,1,1,2,1,3,2,4],
  [2,1,1,3,1,3,2,6],
  [4,3,1,1,1,2,4,1]
];
function wardrobeFor(owner,role,id,seed){
  const weights=role==='seated'?[wardrobeWeights[owner][0],wardrobeWeights[owner][1]]:wardrobeWeights[owner];
  let draw=random(hash('crowd-look',seed,owner,id))()*weights.reduce((sum,weight)=>sum+weight,0);
  return wardrobes[weights.findIndex(weight=>(draw-=weight)<0)]||wardrobes[0];
}
const petGuestStyles=['Casual','Beach','Farmer','Casual2','Punk'];
const petBarStyles=['Casual2','Punk'];
const petLoungeStyles=['Casual','Beach','Farmer','Punk'];
const petWardrobeVersion=1;
const petStyles=actor=>actor.role!=='seated'?petGuestStyles:actor.id.startsWith('bar-seat-')?petBarStyles:petLoungeStyles;
const petAudience=actor=>['guest','seated','support','surfer','assisted'].includes(actor.role);

function petWardrobeFor(actor,others,seed){
  let best,lowest=Infinity;
  for(const style of petStyles(actor)){
    let count=0,near=0;
    for(const other of others){
      if(other===actor||!petAudience(other)||other.visible===false||other.styleKey!==style||other.assistedSourceId===actor.id)continue;
      if(petStyles(actor)===petStyles(other))count++;
      const gap=Math.hypot((actor.homeX??actor.x)-(other.homeX??other.x),(actor.homeZ??actor.z)-(other.homeZ??other.z));
      if(gap<2.1)near+=1+(2.1-gap)/2.1;
    }
    const score=count+near*2.5+random(hash('pet-wardrobe',seed,actor.id,style))()*.65;
    if(score<lowest){lowest=score;best=style;}
  }
  return best;
}

function applyPetWardrobe(actors,seed){
  const assigned=actors.filter(actor=>petAudience(actor)&&actor.petWardrobeVersion===petWardrobeVersion&&petStyles(actor).includes(actor.styleKey));
  const pending=actors.filter(actor=>petAudience(actor)&&!assigned.includes(actor));
  pending.sort((a,b)=>Number(a.role!=='seated')-Number(b.role!=='seated')||(a.role==='seated'&&b.role==='seated'?a.id.localeCompare(b.id):0)||Number(a.role==='assisted')-Number(b.role==='assisted')
    ||hash('pet-order',seed,a.id)-hash('pet-order',seed,b.id));
  for(const actor of pending){
    const source=actor.assistedSourceId&&assigned.find(a=>a.id===actor.assistedSourceId);
    actor.styleKey=source?.styleKey||petWardrobeFor(actor,assigned,seed);
    actor.petWardrobeVersion=petWardrobeVersion;
    assigned.push(actor);
  }
}
const crowdFocus=[
  [[5.8,-3.6],[3.4,-1.4],[-5.7,-2.2]],
  [[4.7,-2.6],[1.8,-.8],[-4.4,-2.3],[-5.8,3.5]],
  [[5.7,-2.6],[2.0,-2.5],[5.9,-.1],[.3,.4]]
];
function orderFloor(points,owner,seed){
  if(!seed)return points;
  const anchors=crowdFocus[owner];
  const [x,z]=anchors[Math.floor(random(hash('crowd-focus',seed,owner))()*anchors.length)];
  return points.map(p=>{
    const dx=p.x-x,dz=p.z-z;
    const affinity=1+3.1*Math.exp(-(dx*dx+dz*dz)/8.2);
    const variation=random(hash('crowd-point',seed,p.x,p.z,p.zone))();
    return {...p,priority:(.12+p.score*.28+variation*.72)/affinity};
  }).sort((a,b)=>a.priority-b.priority||a.z-b.z||a.x-b.x);
}
const distance=(a,b)=>Math.hypot(a.x-b.x,a.z-b.z);
const layoutFor=owner=>owner===0?FREEBIRD_LAYOUT:owner===1?FF_LAYOUT:null;
const originalQueue=[[-1.5,-1.75],[-.85,-2.25],[-.15,-2.7],[.6,-3.1],[1.4,-3.5],[2.2,-3.7],[3,-4]].map(([x,z])=>({x,z}));
const hash=(...values)=>{
  let value=2166136261;
  for(const part of values)for(const char of String(part)){value=Math.imul(value^char.charCodeAt(0),16777619)>>>0;}
  return value||1;
};
const random=seed=>()=>{seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed/4294967296;};
function guestNames({owner=2,day=1,session=1,nameSeed=0}={}){
  const next=random(hash(owner,day,session,nameSeed,'guest-names'));
  const shuffle=items=>{
    const copy=items.slice();
    for(let i=copy.length-1;i>0;i--){const j=Math.floor(next()*(i+1));[copy[i],copy[j]]=[copy[j],copy[i]];}
    return copy;
  };
  const pools=nameGroups.map(shuffle),deck=[];
  for(let i=0;i<pools[0].length;i++)deck.push(...shuffle(pools).map(pool=>pool[i]));
  return deck;
}
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

function build({owner=2,attendance=0,showId='freewill',day=1,session=1,nameSeed=0,crowdSeed=0}={}){
  return buildCrowd({owner,attendance,showId,day,session,nameSeed,crowdSeed});
}
function buildCrowd({owner,attendance,showId,day,session,nameSeed,crowdSeed},legacyOrder=false){
  owner=ownerIndex(owner);
  const profile=AUDIENCE_VENUES[owner],total=whole(attendance,0,profile.capacity),seed=hash(owner,day,session,showId),next=random(seed),layout=layoutFor(owner),actors=[];
  const candidates=orderFloor(floorCandidates(owner,seed^0x9e3779b9),owner,legacyOrder?0:crowdSeed),names=guestNames({owner,day,session,nameSeed});
  const make=(id,role,slot,point,behavior,extra={})=>{
    const descriptor={id,name:names[actors.length]||'Guest',role,slot,audienceVersion:1,behavior,behaviorPhase:round(next()*TAU),animationSpeed:round(.72+next()*.56),
      x:point.x,z:point.z,y:0,homeX:point.x,homeZ:point.z,targetX:point.x,targetZ:point.z,facing:point.facing??facing(point.x,point.z),
      moving:false,action:behavior,visible:true,_path:[],_pathAt:0,_wait:round(2+next()*5),...extra};
    if(owner!==2&&crowdSeed&&(role==='guest'||role==='seated'))descriptor.styleKey=wardrobeFor(owner,role,id,crowdSeed);
    if(layout){descriptor.layout=layout.id;descriptor.layoutVersion=layout.version;}
    actors.push(descriptor);return descriptor;
  };

  // These eleven locations are authored chairs/sofas, intentionally outside
  // the walking floor. Standing guests are always tested against navigation.
  const freebirdSeatCount=owner===0?(total>=20?profile.seats.length:Math.floor(total*.3)):Infinity;
  for(const [index,point]of (profile.seats||[]).entries()){
    if(actors.length>=total)break;
    // Preserve the existing wall-guest IDs even for small crowds. Some of
    // those familiar guests start at the stage instead of occupying every
    // seat; old saves can therefore restore the original seated cast intact.
    if(owner===0&&index>=freebirdSeatCount){
      const front=candidates.find(p=>p.zone==='front-floor'&&p.x>=.6&&actors.every(a=>distance(a,p)>=MIN_SPACING));
      if(front){make(point.id,'guest',100+index,front,standingBehavior(owner,showId,next),{seat:false,seatReleased:true,audienceZone:'front-floor',styleKey:index%2?'Suit':'Casual'});continue;}
    }
    make(point.id,'seated',100+index,point,index%3===0?'listen':'sway',
      {seat:true,...(owner===0?{waitingSeat:true,waitRemaining:round(10+next()*22),styleKey:index%2?'Suit':'Casual'}:{} )});
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

  const frontReservations=[];
  for(const actor of actors.filter(a=>a.waitingSeat)){
    // Reserve reachable spots at the near edge of the stage floor. Keep these
    // spaces free so seated visitors can join even after the room fills up.
    const target=candidates.filter(p=>p.zone==='front-floor'&&clearAudiencePoint(p,actors,{front:true})
      &&frontReservations.every(other=>distance(p,other)>=1))
      .sort((a,b)=>distance(a,actor)-distance(b,actor))[0];
    if(target){actor.seatStageTarget={x:target.x,z:target.z};frontReservations.push(target);}
  }
  const place=point=>{
    if(actors.length>=total)return false;
    if(frontReservations.some(target=>distance(point,target)<.78))return false;
    if(actors.some(actor=>distance(actor,point)<MIN_SPACING))return false;
    const slot=guestSlot++,id=slot<16?'patron-'+slot:'audience-'+slot;
    make(id,'guest',slot,point,standingBehavior(owner,showId,next),{audienceZone:point.zone});
    return true;
  };
  if(owner===0){
    // Retain a few people in the social area, while placing most of the room
    // in front of the stage. Fixed mosh/surf performers count toward the front.
    const waiting=actors.filter(a=>a.waitingSeat).length;
    const social=Math.min(3,Math.floor(total*.15),Math.max(0,total-actors.length));
    let placed=0;
    for(const point of candidates){
      if(placed>=social||actors.length>=total)break;
      if(point.zone==='left-social'&&place(point))placed++;
    }
    const desired=Math.min(Math.ceil(total*.7),total-waiting-placed);
    let inFront=actors.filter(a=>!a.waitingSeat&&a.x>=.6&&a.z<2).length;
    for(const point of candidates){
      if(inFront>=desired||actors.length>=total)break;
      if(point.zone==='front-floor'&&point.x>=.6&&place(point))inFront++;
    }
  }
  for(const point of candidates){
    if(actors.length>=total)break;
    place(point);
  }
  // At capacity an unlucky greedy ordering must not reduce the paid crowd.
  if(actors.length!==total&&crowdSeed&&!legacyOrder)return buildCrowd({owner,attendance,showId,day,session,nameSeed,crowdSeed},true);
  if(actors.length!==total)throw new Error(`Audience floor ${profile.key} has ${actors.length} safe slots for ${total} guests`);
  if(owner===2)applyPetWardrobe(actors,crowdSeed||seed);
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
  if(target&&walkable(target,false,true,layoutFor(owner)?.id)&&actors.every(a=>a===actor||a.visible===false||distance(a,target)>=.48))return target;
  const points=floorCandidates(owner,hash(owner,day,session,actor?.id||'', 'front'));
  return points.filter(p=>['front-floor','stage-floor','listening-floor'].includes(p.zone)&&clearAudiencePoint(p,actors.filter(a=>a!==actor),{front:true}))
    .sort((a,b)=>distance(a,actor)-distance(b,actor))[0]||null;
}

function safePoint({owner=0,actor,actors=[],day=1,session=1}={}){
  owner=ownerIndex(owner);
  return floorCandidates(owner,hash(owner,day,session,actor?.id||'','migrate'))
    .filter(p=>clearAudiencePoint(p,actors.filter(a=>a!==actor))).sort((a,b)=>distance(a,actor)-distance(b,actor))[0]||null;
}

function arrival({owner=2,showId='freewill',day=1,session=1,serial=1,actors=[],nameSeed=0,crowdSeed=0}={}){
  owner=ownerIndex(owner);const seed=hash(owner,day,session,serial,'arrival'),next=random(seed),layout=layoutFor(owner);
  const candidates=orderFloor(floorCandidates(owner,seed),owner,crowdSeed);
  const ordered=owner===0?[...candidates.filter(point=>point.zone==='front-floor'&&point.x>=.6),...candidates.filter(point=>point.zone!=='front-floor'||point.x<.6)]:candidates;
  const point=ordered.find(point=>clearAudiencePoint(point,actors));if(!point)return null;
  const slot=300+serial,behavior=standingBehavior(owner,showId,next);
  const used=new Set(actors.filter(actor=>actor.visible!==false).map(actor=>actor.name));
  const names=guestNames({owner,day,session,nameSeed});
  const offset=(Math.max(1,serial)-1)%names.length;
  const name=names.slice(offset).concat(names.slice(0,offset)).find(candidate=>!used.has(candidate))||names[offset];
  const id=`arrival-${session}-${serial}`;
  return{id,name,role:'guest',slot,audienceVersion:1,
    ...(owner===2?{styleKey:petWardrobeFor({id,role:'guest',...point},actors,crowdSeed||seed),petWardrobeVersion}:crowdSeed?{styleKey:wardrobeFor(owner,'guest',id,crowdSeed)}:{}),
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
  build,arrival,frontTarget,safePoint,guestNames,applyPetWardrobe
};
