import assert from 'node:assert/strict';
import {SimBarGame,Floor,Challenges} from './WebGLTemplate/FastGame/core.js';
import {Audience} from './WebGLTemplate/FastGame/audience.js';
import {distance,walkable} from './WebGLTemplate/FastGame/navigation.js';
import {EXIT_POINT} from './WebGLTemplate/FastGame/aftershow.js';
import {createDrunkIncident,tickDrunkIncident,DRUNK_FIGHT_SECONDS} from './WebGLTemplate/FastGame/drunk-incident.js';

const memory=()=>{const m=new Map();return{getItem:k=>m.get(k)??null,setItem:(k,v)=>m.set(k,String(v))};};
function night(owner=2,{full=false}={}){
  const g=new SimBarGame({storage:memory(),seed:37});g.act('owner',{index:owner});
  if(full){g.journey.completedNights=2;g.journey.day=3;g.state.fans=150;g.state.upgrades[1]=3;}
  g.act('start');g.act('selectShow',{showId:full?'double':'freewill'});if(full){g.act('ticket',{tier:0});g.act('promote');}
  g.act('deskWork',{task:0});g.act('deskWork',{task:1});g.journey.tutorialComplete=true;assert(g.act('launch').success);g.journey.inspection.phase='done';
  assert(Floor.spawnGuest(g.floor,1).success);g._ensureGuest();return g;
}
function until(g,predicate,limit=240){let time=0;for(;!predicate()&&time<limit;time+=.05)g.tick(.05);assert(predicate(),JSON.stringify({phase:g.phase,incident:g.floor.guestIncident,after:g.journey.afterShow,escort:g.escort}));return time;}
const ids=g=>g.floor.guestIncident.participants.map(item=>item.id);
const snapshot=g=>g.floor.guestIncident.participants.map(item=>{const a=g.actors.find(a=>a.id===item.id);return{id:a.id,x:a.x,z:a.z,y:a.y,visible:a.visible,action:a.action,marker:a.drunkIncident};});
const evidence={venues:[]};

// Only the six-second guest response windows count. Existing cleanup failures,
// manual pauses and a real inspector freeze cannot consume one of the strikes.
{
  const f=Floor.newState();assert.equal(Floor.missGuest(f),false);assert.equal(f.guestMisses,0);
  const g=night();g._ensureReaction();Challenges.start(g.reaction,Challenges.Cleanup);until(g,()=>g.reaction.timeouts===1,10);assert.equal(g.floor.guestMisses,0);
  until(g,()=>g.reaction.activeKind===Challenges.Guest,3);const remaining=g.reaction.secondsLeft;g.act('pause');g.tick(5);assert.equal(g.reaction.secondsLeft,remaining);assert.equal(g.floor.guestMisses,0);g.act('resume');
  g.journey.inspection.phase='scheduled';assert(g.act('inspection').success);until(g,()=>g.inspection.freezePerformers,10);const frozen=g.reaction.secondsLeft,misses=g.floor.guestMisses;g.tick(1);assert.equal(g.reaction.secondsLeft,frozen);assert.equal(g.floor.guestMisses,misses);assert.equal(g.floor.guestIncident,null);
  evidence.timeoutScopePauseAndInspection=true;
}

// Maximum real crowds use exactly the nearest five original people. They keep
// their IDs, depart once at the real door, and are never recreated by reload.
for(const owner of [0,1,2]){
  const g=night(owner,{full:true}),population=g.festival.liveAttendance,cast=g.actors.map(a=>a.id),start=g._startDrunkIncident.bind(g);let nearest;
  g._startDrunkIncident=()=>{const origin=g.actors.find(a=>a.id==='assisted-guest');nearest=g.actors.filter(a=>Audience.isAudience(a)&&a.visible!==false&&!a.drunkIncident).sort((a,b)=>distance(a,origin)-distance(b,origin)||a.id.localeCompare(b.id)).slice(0,5).map(a=>a.id);start();};
  until(g,()=>g.floor.guestMisses===1,10);assert.equal(g.floor.guestIncident,null);until(g,()=>g.floor.guestMisses===2,10);assert.equal(g.floor.guestIncident,null);
  until(g,()=>!!g.floor.guestIncident,10);assert.deepEqual(ids(g),nearest);assert.equal(ids(g).length,5);assert.deepEqual(g.actors.map(a=>a.id),cast);
  const incident=g.floor.guestIncident;assert(g.orders.every(order=>!nearest.includes(order.id)));assert(!g.pour||!nearest.includes(g.pour.orderId));assert(nearest.every(id=>!g._orderGuest(g.actors.find(a=>a.id===id))));
  until(g,()=>incident.phase==='fight',30);assert(nearest.every(id=>g.actors.find(a=>a.id===id).action==='fight'));
  g.act('pause');const elapsed=incident.elapsed,positions=snapshot(g);g.tick(5);assert.equal(incident.elapsed,elapsed);assert.deepEqual(snapshot(g),positions);g.act('resume');
  const fightingStart=g.elapsed;until(g,()=>incident.phase==='exit',5);assert(Math.abs(g.elapsed-fightingStart-DRUNK_FIGHT_SECONDS)<1e-6);
  const leavingStart=g.elapsed;let previous=new Map(g.actors.map(a=>[a.id,{x:a.x,z:a.z,visible:a.visible}]));
  while(incident.phase!=='done'&&g.elapsed-leavingStart<100){
    g.tick(.05);
    for(const id of nearest){const a=g.actors.find(a=>a.id===id),p=previous.get(id);assert(distance(a,p)<=.135001,'A departing participant teleported');if(p.visible&&a.visible===false)assert(distance(a,EXIT_POINT)<=.180001,'Participant vanished away from the real exit');}
    previous=new Map(g.actors.map(a=>[a.id,{x:a.x,z:a.z,visible:a.visible}]));
  }
  assert.equal(incident.phase,'done');assert.equal(g.festival.liveAttendance,population-5);assert.equal(g.floor.guestMisses,3);assert.equal(g.actors.length,cast.length);
  const raw=g.exportSave(),loaded=new SimBarGame({storage:g.storage,seed:37});loaded.act('continue');assert.deepEqual(ids(loaded),nearest);assert(nearest.every(id=>loaded.actors.find(a=>a.id===id)?.visible===false));assert.equal(loaded.festival.liveAttendance,population-5);
  const oldIncident=loaded.floor.guestIncident;loaded.tick(10);assert.equal(loaded.floor.guestIncident,oldIncident);assert(nearest.every(id=>loaded.actors.find(a=>a.id===id).visible===false));assert(raw.includes('guestMisses'));
  const serial=loaded.journey.festival.arrivalSerial;loaded.journey.festival.arrivalCredit=1;loaded._festivalTick(.05);assert.equal(loaded.journey.festival.arrivalSerial,serial+1,'Normal earned walk-in credit was blocked');assert(nearest.every(id=>loaded.actors.find(a=>a.id===id).visible===false));
  evidence.venues.push({owner,capacity:population,departed:5,remaining:population-5,fightSeconds:4,exitSeconds:g.elapsed-leavingStart});
}

// Simultaneous authored bar-chair exits must clear their common lane through
// real short yielding walks before the complete four-second fight starts.
{
  const actors=Audience.build({owner:2,attendance:44,showId:'freewill',day:5,session:1}),state=createDrunkIncident(actors,{id:'assisted-guest',x:-7,z:-3.15});
  const selected=state.participants.map(item=>item.id),wardrobes=new Map(actors.map(a=>[a.id,a.styleKey]));let fight=0,forming=0;
  for(let i=0;i<2400&&state.phase!=='done';i++){
    const before=new Map(actors.map(a=>[a.id,{x:a.x,z:a.z,visible:a.visible}]));const prior=state.phase;tickDrunkIncident(state,actors,.05);if(prior==='fight')fight+=.05;if(prior==='forming')forming+=.05;
    for(const id of selected){const a=actors.find(a=>a.id===id),p=before.get(id);assert(distance(a,p)<=.135001);if(p.visible&&a.visible===false)assert(distance(a,EXIT_POINT)<=.180001);if(a.drunkIncident==='fight')assert(walkable(a,false,false,a.layout));assert.equal(a.styleKey,wardrobes.get(id));}
  }
  assert.equal(state.phase,'done');assert(Math.abs(fight-4)<1e-6);assert(forming>0&&forming<20);assert(selected.includes('bar-seat-0'));evidence.sharedSeatEgress={selected,formingSeconds:forming,fightSeconds:4};
}

// These three fixed-room layouts exposed different stationary-crowd failures:
// a sofa's narrow turn and two routes repeatedly forgetting distant bystanders.
{
  const records=[];
  for(const [owner,origin]of [[2,{x:-7,z:6.4}],[2,{x:0,z:0}],[1,{x:-7,z:6.4}]]){
    const actors=Audience.build({owner,attendance:Audience.capacity(owner),showId:'double',day:5,session:1}),state=createDrunkIncident(actors,{id:'assisted-guest',...origin});let seconds=0;
    for(;seconds<60&&state.phase!=='done';seconds+=.05){const previous=new Map(actors.map(a=>[a.id,{x:a.x,z:a.z,visible:a.visible}]));tickDrunkIncident(state,actors,.05);for(const item of state.participants){const actor=actors.find(a=>a.id===item.id),before=previous.get(item.id);assert(distance(actor,before)<=.135001);if(before.visible&&actor.visible===false)assert(distance(actor,EXIT_POINT)<=.180001);}}
    assert.equal(state.phase,'done');assert.equal(actors.filter(a=>a.drunkIncident==='gone'&&a.visible===false).length,5);records.push({owner,origin,seconds});
  }
  evidence.stationaryCrowdDetours=records;
}

// A midnight transition and saves during either the fight or exit keep the
// incident as the sole movement owner; farewell and settlement wait for it.
{
  let g=night(2);until(g,()=>g.floor.guestIncident?.phase==='fight',40);g.tick(1);g.journey.secondsLeft=.05;g.tick(.05);assert(g.closing);const selected=ids(g),before=snapshot(g),elapsed=g.floor.guestIncident.elapsed;
  const load=game=>{game.exportSave();const loaded=new SimBarGame({storage:game.storage,seed:37});loaded.act('continue');return loaded;};
  g=load(g);assert.equal(g.floor.guestIncident.elapsed,elapsed);assert.deepEqual(snapshot(g),before);assert(selected.every(id=>{const item=g.journey.afterShow.audience.find(a=>a.id===id);return!item||item.phase==='gone';}));
  until(g,()=>g.floor.guestIncident.phase==='exit',5);g.tick(.25);const exiting=snapshot(g);g=load(g);assert.deepEqual(snapshot(g),exiting);
  let receipts=0,settle=g._settleNight.bind(g);g._settleNight=()=>{const n=g.journey.completedNights;settle();if(g.journey.completedNights>n)receipts++;};
  for(let i=0;i<7000&&g.phase!=='results';i++){g.setViewClosingActive(true);g.tick(.05);if(g.phase==='results')assert.equal(g.floor.guestIncident.phase,'done');}
  assert.equal(g.phase,'results');assert.equal(receipts,1);assert(selected.every(id=>g.actors.find(a=>a.id===id).visible===false));const cash=g.cash,completed=g.journey.completedNights;g.tick(5);assert.equal(g.cash,cash);assert.equal(g.journey.completedNights,completed);
  evidence.midnightFightExitSaveAndSettlement=true;
}
console.log(JSON.stringify({passed:true,checks:['guest-only strike count, pause and inspection freeze','three maximum crowds nearest existing five, exact fight and physical exits','no duplicate incident or tombstone resurrection','shared chair egress yields without teleporting','midnight fight/exit save ownership and one settlement'],evidence},null,2));
