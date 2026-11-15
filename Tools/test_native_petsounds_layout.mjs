import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {SimBarGame,catalog} from './WebGLTemplate/FastGame/core.js';
import {Audience} from './WebGLTemplate/FastGame/audience.js';
import {AUDIENCE_VENUES,PETSOUNDS_STANDING_SPOTS} from './WebGLTemplate/FastGame/owner-venues.js';
import {PETSOUNDS_LAYOUT,walkable,segment,pathfind,setRoute,move,distance} from './WebGLTemplate/FastGame/navigation.js';
import {EXIT_POINT} from './WebGLTemplate/FastGame/aftershow.js';

const memory=()=>{const values=new Map();return{getItem:key=>values.get(key)??null,setItem:(key,value)=>values.set(key,String(value))};};
const people=g=>g.actors.filter(a=>Audience.isAudience(a));
const snapshot=g=>people(g).map(a=>({id:a.id,role:a.role,visible:a.visible,x:a.x,z:a.z,homeX:a.homeX,homeZ:a.homeZ,slot:a.slot})).sort((a,b)=>a.id.localeCompare(b.id));
const meta=JSON.parse(readFileSync(new URL('./WebGLTemplate/FastGame/art/venue.json',import.meta.url),'utf8'));
assert.equal(Audience.capacity(2),44);assert.equal(AUDIENCE_VENUES[2].seats.length,8);
assert.deepEqual(AUDIENCE_VENUES[2].seats.filter(s=>s.id.startsWith('lounge-')).map(s=>s.id),['lounge-0','lounge-1','lounge-2','lounge-3']);
assert.deepEqual(PETSOUNDS_STANDING_SPOTS.map(s=>s.id),['lounge-4','lounge-5','lounge-6']);
assert.equal(AUDIENCE_VENUES[0].seats.length,6);assert.equal(AUDIENCE_VENUES[1].standingSpots.length,10);
for(const [index,feet]of [[0,[319,150]],[1,[365,163]],[2,[444,189]],[3,[488,202]]])assert.deepEqual(meta.seatedActors['lounge-'+index].feet,feet);
for(const spot of PETSOUNDS_STANDING_SPOTS)assert.equal(meta.seatedActors[spot.id],undefined);
assert(!meta.occluders.some(o=>o.id?.startsWith('front-sofa-')));
for(const id of ['rear-table-left','rear-table-right','front-table-left-top','front-table-right-top','petsounds-dj-front','petsounds-dj-side'])assert(meta.occluders.some(o=>o.id===id),id);

// The footprint is the actual angled case rather than its entire sprite box.
// An approach route must turn around its front without crossing its padding.
for(const staff of [false,true])assert.equal(walkable({x:.15,z:3},staff),false);
const from={x:-1.4,z:3.2},to={x:1.1,z:1.8};
assert(walkable(from));assert(walkable(to));assert.equal(segment(from,to),false);
const around=pathfind(from,to);assert(around.length>1);
for(let i=0;i<around.length;i++)assert(segment(i?around[i-1]:from,around[i]),'A DJ approach path crossed the booth');
for(const table of PETSOUNDS_LAYOUT.tables)assert.equal(walkable({x:table.x,z:table.z+.65}),false,'Retained table bases must remain solid');

// All three former seats are reachable standing floor. Walking is continuous,
// reaches the same physical doorway and never crosses the retained tables.
for(const spot of PETSOUNDS_STANDING_SPOTS){
  assert(walkable(spot,false,true),spot.id+' standing home is blocked');
  const a={...spot,role:'guest',visible:true,y:0};assert(setRoute(a,EXIT_POINT,[a]));
  for(let frame=0;frame<500&&distance(a,EXIT_POINT)>.2;frame++){
    const old={x:a.x,z:a.z};move(a,.05,1.45,[a]);
    assert(distance(old,a)<.09,'Standing guest teleported');assert(walkable(a));
  }
  assert(distance(a,EXIT_POINT)<=.2,spot.id+' cannot leave the cleared floor');
}

function open(){
  const g=new SimBarGame({storage:memory(),seed:17});assert(g.act('owner',{index:2}).success);
  g.journey.completedNights=2;g.journey.day=3;g.state.fans=100;g.state.upgrades[1]=1;
  assert(g.act('start').success);assert(g.act('selectShow',{showId:'double'}).success);
  assert(g.act('deskWork',{task:0}).success);assert(g.act('deskWork',{task:1}).success);assert(g.act('launch').success);
  while(g.phase==='guide')g.act('guide');return g;
}
const g=open();assert.equal(g.metrics().attendance,44);assert.equal(people(g).filter(a=>a.visible!==false).length,44);
for(const spot of PETSOUNDS_STANDING_SPOTS){const a=g.actors.find(a=>a.id===spot.id);assert(a);assert.equal(a.role,'guest');assert.equal(a.seat,false);assert(walkable(a));}
const before=snapshot(g),reloaded=new SimBarGame({storage:g.storage});reloaded.act('continue');
assert.deepEqual(snapshot(reloaded),before,'Saving a revised room changed its audience');

// A real pre-revision shape retains the same cast, clothes/slots and wall
// seats. Only the removed sofa seating turns into standing at the same pixels.
const old=JSON.parse(g.exportSave()),oldHomes=[[-8.35,-6.5],[-6.85,-6.5],[-3.45,-6.5]];
const retired=new Set(PETSOUNDS_STANDING_SPOTS.map(s=>s.id));
old.native.orders=old.native.orders.filter(o=>!retired.has(o.id));
for(const [index,spot]of PETSOUNDS_STANDING_SPOTS.entries()){
  const a=old.native.actors.find(a=>a.id===spot.id),[x,z]=oldHomes[index];
  Object.assign(a,{role:'seated',seat:true,seatReleased:false,waitingSeat:false,x,z,homeX:x,homeZ:z,targetX:x,targetZ:z,moving:false,action:'listen',slot:108+index,_path:[],_pathAt:0});
}
const savedCast=old.native.actors.filter(Audience.isAudience).map(a=>a.id).sort();
const migrated=new SimBarGame({storage:memory(),seed:1});assert(migrated.importSave(old));migrated.act('continue');
assert.deepEqual(people(migrated).map(a=>a.id).sort(),savedCast);assert.equal(people(migrated).filter(a=>a.visible!==false).length,44);
for(const [index,spot]of PETSOUNDS_STANDING_SPOTS.entries()){
  const a=migrated.actors.find(a=>a.id===spot.id);assert.equal(a.role,'guest');assert.equal(a.slot,108+index);assert.equal(a.seat,false);
  assert.equal(a.x,spot.x);assert.equal(a.z,spot.z);assert(walkable(a));
}
for(let index=0;index<4;index++){
  const id='lounge-'+index,a=migrated.actors.find(a=>a.id===id),original=old.native.actors.find(a=>a.id===id);
  assert.equal(a.role,'seated');assert.equal(a.x,original.x);assert.equal(a.z,original.z);
}
const migratedSnapshot=snapshot(migrated);migrated.save();const again=new SimBarGame({storage:migrated.storage});again.act('continue');assert.deepEqual(snapshot(again),migratedSnapshot,'Seat migration repeated on resume');

// A guest saved inside the newly installed DJ case receives a safe clear
// floor home once, without changing the paid attendance or other venue choice.
const blocked=JSON.parse(g.exportSave()),fan=blocked.native.actors.find(a=>a.id==='patron-0');
Object.assign(fan,{x:.15,z:3,homeX:.15,homeZ:3,targetX:.15,targetZ:3});
const recovered=new SimBarGame({storage:memory()});assert(recovered.importSave(blocked));recovered.act('continue');
const safe=recovered.actors.find(a=>a.id===fan.id);assert(walkable(safe));assert(walkable({x:safe.homeX,z:safe.homeZ}));assert.equal(people(recovered).filter(a=>a.visible!==false).length,44);
assert.equal(catalog.owners[recovered.owner].id,catalog.owners[2].id);

// Exercise the paid full-capacity room through real greetings, cleared-floor
// departures, cleanup prompts and settlement. A closing save may not restart
// any of those steps or alter already departing seat listeners.
g.journey.secondsLeft=.05;g.tick(.05);assert.equal(g.phase,'closing');
let checkedResume=false;
for(let time=0;time<300&&g.phase!=='results';time+=.05){
  const beforeMove=new Map(people(g).map(a=>[a.id,{x:a.x,z:a.z}]));g.tick(.05);
  for(const a of people(g)){const old=beforeMove.get(a.id);assert(Math.hypot(a.x-old.x,a.z-old.z)<=.19,'Closing teleported '+a.id);}
  if(!checkedResume&&g.journey.afterShow.departureStarted){
    const beforeSave=snapshot(g);g.save();const resumed=new SimBarGame({storage:g.storage});resumed.act('continue');assert.deepEqual(snapshot(resumed),beforeSave);checkedResume=true;
  }
  if(g.farewellPending)g.act('farewellAck');
  if(g.reaction.activeKind)g.act('quick',{kind:g.reaction.activeKind});
}
assert(checkedResume);assert.equal(g.phase,'results');assert(people(g).every(a=>a.visible===false));
const receipt=[g.state.money,g.journey.xp,g.journey.completedNights];g.save();
const settled=new SimBarGame({storage:g.storage});settled.act('continue');settled.tick(1);assert.deepEqual([settled.state.money,settled.journey.xp,settled.journey.completedNights],receipt);
console.log('PASS: Petsounds eight preserved seats, three standing former sofa guests, 44-person fresh/saved cast, physical clear-floor exits, DJ/table collision, precise occluders and safe legacy migration');
