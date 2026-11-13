import assert from 'node:assert/strict';
import {SimBarGame,Booking,Floor,Challenges} from './WebGLTemplate/FastGame/core.js';
import {Audience} from './WebGLTemplate/FastGame/audience.js';
import {Inspection} from './WebGLTemplate/FastGame/inspection.js';
import {distance,walkable} from './WebGLTemplate/FastGame/navigation.js';
import {launchPreparedDesk} from './native_test_workflow.mjs';

const memory=()=>{const data=new Map();return{getItem:key=>data.get(key)??null,setItem:(key,value)=>data.set(key,String(value))};};
const inspectors=g=>g.actors.filter(a=>a.role==='inspector');
const frozenCast=g=>g.actors.filter(a=>a.visible!==false&&(Audience.isAudience(a)||a.role==='band'||a.role==='assisted'));
const positions=g=>frozenCast(g).map(a=>({id:a.id,x:a.x,z:a.z}));
function launch(owner=0,{seed=47,legacy=false,staff=false,full=false}={}){
  const g=new SimBarGame({storage:memory(),seed});g.act('owner',{index:owner});
  if(legacy)g.journey.booking=Booking.newDeskPlan(1,0);
  if(full){g.journey.day=3;g.journey.completedNights=2;g.state.fans=150;g.state.upgrades=[0,3,0];}
  g.act('start');
  if(full){
    assert(g.act('selectShow',{showId:owner===1?'double':'freewill'}).success);g.act('ticket',{tier:0});g.act('promote');
    g.act('deskWork',{task:0});g.act('deskWork',{task:1});assert(g.act('launch').success);
  }else launchPreparedDesk(g);
  while(g.phase==='guide')g.act('guide');
  if(staff){g.state.staff=[1,1,1];g.state.money=1e6;g._applyOwnerLayout({reset:true});g.tick(.05);}
  return g;
}
function advance(g,seconds){for(let t=0;t<seconds;t+=.05)g.tick(Math.min(.05,seconds-t));}
function waitFor(g,predicate,max=35){for(let t=0;t<max&&!predicate();t+=.05)g.tick(.05);assert(predicate(),JSON.stringify({inspection:g.journey.inspection,officers:inspectors(g)}));}

for(const owner of [0,1,2]){
  const g=launch(owner),count=g.festival.liveAttendance;
  assert.equal(g.act('festival',{id:'horns'}).success,true);
  assert.equal(g.act('inspection').code,'inspection_warning');
  assert.equal(g.inspection.phase,'warning');assert.equal(g.inspection.active,false);
  assert.match(g.message,/춤추지 말라는 신고/);
  advance(g,1);assert.equal(g.inspection.active,false,'Warning must allow the set to continue');
  waitFor(g,()=>g.inspection.active);
  assert.equal(inspectors(g).length,3);assert.equal(g.festival.liveAttendance,count);
  const before=positions(g),feedback=structuredClone(g.journey.feedback),festival=structuredClone(g.journey.festival),left=g.journey.secondsLeft;
  advance(g,.5);
  assert.deepEqual(positions(g),before,'A guest or musician moved during the patrol');
  assert(frozenCast(g).every(a=>!a.moving&&a.y===0),'Frozen performers must stand on the floor');
  assert.deepEqual(g.journey.festival,festival,'Patrol consumed an active crowd action or cooldown');
  assert.deepEqual(g.journey.feedback,feedback,'Standing still earned spectator feedback');
  assert(g.journey.secondsLeft<left,'The show clock must continue during an inspection');
  assert.equal(g.act('festival',{id:'dance'}).code,'inspection_blocked','Inspection must take priority over an existing cooldown');
  assert.match(g.message,/단속반이 지나가면/);
  assert.equal(g.festival.blockedByInspection,true);assert.equal(g.festival.canAct,false);
  g.act('pause');const paused=JSON.parse(g.exportSave()),ledger=structuredClone(g.journey.inspection),cast=structuredClone(g.actors);
  g.tick(3);assert.deepEqual(g.journey.inspection,ledger);assert.deepEqual(g.actors,cast);
  const restored=new SimBarGame({storage:g.storage});
  assert.deepEqual(restored.journey.inspection,ledger,'Reload changed the patrol clock or route');
  assert.deepEqual(restored.actors.filter(a=>a.role==='inspector'),cast.filter(a=>a.role==='inspector'),'Reload moved an inspector');
  assert.deepEqual(positions(restored),positions(g));
  restored.act('continue');let maxVisible=restored.inspection.officersPresent,previous=new Map(inspectors(restored).map(a=>[a.id,{x:a.x,z:a.z}]));
  for(let t=0;t<35&&restored.inspection.phase!=='done';t+=.05){
    const still=positions(restored),wasFrozen=restored.inspection.freezePerformers;restored.tick(.05);
    maxVisible=Math.max(maxVisible,restored.inspection.officersPresent);
    if(wasFrozen&&restored.inspection.freezePerformers)assert.deepEqual(positions(restored),still);
    for(const a of inspectors(restored)){
      const p=previous.get(a.id);assert(distance(a,p)<.1,'An inspector teleported');
      assert(walkable(a,false,false,a.layout),'Inspector crossed the venue furniture');
      previous.set(a.id,{x:a.x,z:a.z});
    }
  }
  assert.equal(restored.inspection.phase,'done');assert.equal(maxVisible,3);assert.equal(restored.inspection.officersPresent,0);
  assert.equal(inspectors(restored).length,0);assert.equal(restored.festival.liveAttendance,count);
  assert(restored.journey.inspection.officers.every(a=>a.status==='left'&&distance(a,{x:8.7,z:-5.4})<=.15),'An inspector disappeared before reaching the exit');
  const resumed=positions(restored);restored.tick(.5);assert.notDeepEqual(positions(restored),resumed,'Audience movement never resumed');
  assert.equal(restored.act('festival',{id:'dance'}).code,'festival_cooldown');
  assert.equal(restored.act('inspection').success,false,'Inspection repeated within the same night');
  assert.equal(JSON.parse(paused.journeyJson).inspection.phase,'patrol');
}

// The naturally scheduled complaint is stable across reloads and occurs only
// once. No extra random draws may alter drink or floor-mission schedules.
{
  const a=launch(1,{seed:82}),b=launch(1,{seed:82});
  assert.equal(a.inspection.triggerAt,b.inspection.triggerAt);assert(a.inspection.triggerAt>=55&&a.inspection.triggerAt<=90);
  const rng=a._seed;Inspection.create({session:1,day:1,owner:1,seed:rng});assert.equal(a._seed,rng);
  advance(a,a.inspection.triggerAt+.2);assert.equal(a.inspection.phase,'warning');
  const copy=new SimBarGame({storage:a.storage});copy.importSave(a.exportSave());copy.act('continue');
  assert.equal(copy.journey.inspection.warningRemaining,a.journey.inspection.warningRemaining);
  waitFor(copy,()=>copy.inspection.phase==='done');advance(copy,10);assert.equal(copy.journey.inspection.serial,1);assert.equal(copy.inspection.phase,'done');
}

// Authored legacy visitors and the promoter can stand in the default patrol
// aisle. Observation spots and paths must adapt without moving those guests.
for(const owner of [0,1,2])for(const legacy of [false,true]){
  const g=launch(owner,{legacy,staff:true,full:!legacy});g.act('inspection');
  waitFor(g,()=>g.inspection.active);waitFor(g,()=>g.inspection.phase==='done',45);
  assert(g.journey.inspection.officers.every(a=>a.status==='left'));
}

for(const owner of [0,1,2]){
  const g=launch(owner,{seed:219+owner,full:true}),count=g.festival.liveAttendance;
  assert.equal(count,Audience.capacity(owner),'Dense natural-patrol fixture is below capacity');
  let maximum=0;
  for(let t=0;t<145&&g.inspection.phase!=='done';t+=.1){g.tick(.1);maximum=Math.max(maximum,g.inspection.officersPresent);}
  assert.equal(g.inspection.phase,'done');assert.equal(maximum,3);assert.equal(g.journey.inspection.serial,1);
  assert.equal(g.festival.liveAttendance,count);assert(g.journey.inspection.officers.every(a=>a.status==='left'&&distance(a,{x:8.7,z:-5.4})<=.15));
}

// A guest already receiving help watches quietly too. Their response deadline
// and the new-order timer wait; a musician's authored stage height is retained.
{
  const g=launch(1);Floor.spawnGuest(g.floor,1);const guest=g._ensureGuest();Challenges.start(g.reaction,2,0);
  const singer=g.actors.find(a=>a.id==='singer');singer.y=.35;
  g.act('inspection');waitFor(g,()=>g.inspection.active);
  const response=structuredClone(g.reaction),before=positions(g),orders=g.orders.length;
  g._requestTimer=0;advance(g,.5);
  assert.equal(g.orders.length,orders);assert.equal(g._requestTimer,0);assert.deepEqual(g.reaction,response);assert.deepEqual(positions(g),before);
  assert.equal(singer.y,.35);assert.equal(guest.moving,false);assert.equal(g.act('quick',{kind:2}).code,'inspection_blocked');
  // An imported in-flight escort must also stop, even though fresh inspections
  // normally wait for an existing escort to finish using the public doorway.
  g._beginEscort();const pair=[guest,g.actors.find(a=>a.id===g.escort.worker)].map(a=>({id:a.id,x:a.x,z:a.z}));
  advance(g,.5);for(const p of pair){const a=g.actors.find(a=>a.id===p.id);assert.equal(a.x,p.x);assert.equal(a.z,p.z);assert.equal(a.moving,false);}
  waitFor(g,()=>g.inspection.phase==='done');
}

{
  const g=launch(0);Floor.spawnGuest(g.floor,1);g._ensureGuest();g._beginEscort();g.act('inspection');
  advance(g,3.1);assert.equal(g.inspection.phase,'warning');assert.equal(g.inspection.active,false,'Inspectors entered while a guest escort used the door');
  waitFor(g,()=>g.inspection.phase==='done',55);assert(g.guestExitFinished);
}

// Ending a show mid-patrol orders a physical departure before greetings and
// settlement. The inspector roster never becomes part of the farewell cast.
{
  const g=launch(0);g.act('inspection');waitFor(g,()=>g.inspection.officersPresent===3);
  g.journey.secondsLeft=.05;g.tick(.05);assert.equal(g.phase,'closing');assert.equal(g.inspection.phase,'exiting');
  assert(!g.journey.afterShow.audience.some(a=>a.id.startsWith('inspector-')));
  const before=positions(g);g.tick(.1);assert.deepEqual(positions(g),before,'Farewell began while inspectors were still present');
  waitFor(g,()=>g.inspection.phase==='done',20);
  for(let t=0;t<260&&g.phase!=='results';t+=.05){g.tick(.05);if(g.farewellPending)g.act('farewellAck');if(g.reaction.activeKind)g.act('quick',{kind:g.reaction.activeKind});}
  assert.equal(g.phase,'results','Inspection prevented closing from settling');assert.equal(g.floorTasksPending,false);
}

console.log(JSON.stringify({passed:true,owners:3,inspectors:3,checks:['warning','frozen performers','cooldown priority','no unearned feedback','show clock','pause/save','physical patrol and exit','promoter and legacy aisle','natural full-capacity patrols','guest deadline and escort freeze','authored band height','closing settlement']}));
