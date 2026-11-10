import assert from 'node:assert/strict';
import {SimBarGame,Booking,Floor} from './WebGLTemplate/FastGame/core.js';
import {Audience} from './WebGLTemplate/FastGame/audience.js';
import {Festival} from './WebGLTemplate/FastGame/festival.js';
import {walkable} from './WebGLTemplate/FastGame/navigation.js';

const memory=()=>{const data=new Map();return{getItem:key=>data.get(key)??null,setItem:(key,value)=>data.set(key,String(value))};};
const people=g=>g.actors.filter(a=>Audience.isAudience(a)&&a.visible!==false);
const evidence={};
function launch(owner=0,{showId='freewill',full=false,seed=43}={}){
  const g=new SimBarGame({storage:memory(),seed});g.act('owner',{index:owner});
  if(showId!=='freewill'||full)Object.assign(g.journey,{day:3,completedNights:2});
  if(full){g.state.fans=150;g.state.upgrades=[0,3,0];}
  g.act('start');assert.equal(g.act('selectShow',{showId}).success,true);
  if(full){g.act('ticket',{tier:0});g.act('promote');}
  g.act('deskWork',{task:0});g.act('deskWork',{task:1});g.act('launch');while(g.phase==='guide')g.act('guide');
  return g;
}
function serveQueue(g){const pending=g.orders.length;for(let i=0;i<pending&&g.orders.length;i++)assert.equal(g._serve(g.orders[0].item,true,.7).success,true);}
function advance(g,seconds,{actions=false}={}){
  let action=0;
  for(let t=0;t<seconds;t+=.25){
    if(actions&&g.festival.canAct){
      const options=Festival.actions.filter(a=>!g.festival.cooldowns[a.id]);
      if(options.length)assert.equal(g.act('festival',{id:options[action++%options.length].id}).success,true);
    }
    serveQueue(g);g.tick(.25);
    if(g.reaction?.activeKind)g.act('quick',{kind:g.reaction.activeKind});
  }
}

assert.deepEqual(Festival.actions.map(a=>a.id),['slam','mosh','surf','jump','horns','airguitar','sing','encore','headbang','dance']);
{
  const g=new SimBarGame({storage:memory()});
  assert.equal(g.act('festival',{id:'horns'}).success,false);
  assert.equal(g.festival.hype,0);assert.equal(g.festival.canAct,false);
}
for(const definition of Festival.actions){
  const g=launch(1),before={s:g.state.satisfaction,p:g.state.performance},attendance=people(g).length;
  assert.equal(g.act('festival',{id:definition.id}).success,true);
  assert.equal(g.festival.activeId,definition.id);
  assert(g.state.satisfaction>=before.s&&g.state.performance>=before.p);
  assert(g.actors.some(a=>a.festivalAction===definition.id));
  assert.equal(g.act('festival',{id:definition.id}).code,'festival_cooldown');
  assert.equal(g.act('festival',{id:'dance'}).code,'festival_cooldown');
  g.act('pause');const frozen=structuredClone(g.journey.festival);g.tick(4);
  assert.deepEqual(g.journey.festival,frozen,'Pause consumed the crowd action or cooldown');
  assert.equal(g.act('festival',{id:'horns'}).success,false);
  const saved=g.exportSave(),copy=new SimBarGame({storage:g.storage});
  assert.equal(copy.importSave(saved),true);copy.act('continue');
  assert.deepEqual(copy.journey.festival,frozen,'Reload changed an earned response or cooldown');
  assert.equal(people(copy).length,attendance);
  if(definition.id==='surf'){
    const lifted=copy.actors.filter(a=>a.festivalLead);
    assert.equal(lifted.length,1);assert.equal(lifted[0].y,0);assert.equal(lifted[0].festivalLiftReady,false);
    assert.equal(copy.actors.filter(a=>a.festivalSupport).length,3);
    for(let frame=0;frame<60&&!lifted[0].festivalLiftReady;frame++)copy.tick(.1);
    assert(lifted[0].festivalLiftReady,'Surfer formation did not make physical contact');
    copy.tick(.4);assert(lifted[0].y>0);
    assert(copy.actors.filter(a=>a.festivalSupport).every(a=>Math.hypot(a.x-lifted[0].x,a.z-lifted[0].z)<.85));
  }
  advance(copy,definition.duration+1);
  assert.equal(copy.festival.activeId,'');assert(copy.festival.cooldowns[definition.id]>0);
  assert(copy.actors.every(a=>!a.festivalAction&&!a.festivalLead&&!a.festivalSupport));
}

// A highly satisfied room alone cannot silently fill or alter order demand.
{
  const g=launch(0),count=people(g).length;
  advance(g,35);assert.equal(people(g).length,count);assert.equal(g.festival.orderMultiplier,1);
  assert.equal(g.festival.cocktailBoost,0);assert.equal(g.journey.festival.arrivals,0);
}

// Historical contracts can use the new controls without losing their old
// ticket math or live additions on reload. Their first action locks the room.
{
  const g=new SimBarGame({storage:memory(),seed:59});g.act('owner',{index:0});
  g.journey.booking=Booking.newDeskPlan(1,0);g.act('start');g.act('deskSelect',{selected:true});
  g.act('deskWork',{task:0});g.act('deskWork',{task:1});g.act('launch');while(g.phase==='guide')g.act('guide');
  const booked=structuredClone(g.journey.booking.lockedMetrics);g.act('festival',{id:'horns'});
  g.journey.festival.arrivalCredit=1;g._festivalTick(.05);
  assert(g.festival.arrivals>0);g.act('lobby');assert.equal(g.act('owner',{index:1}).code,'venue_locked');
  const copy=new SimBarGame({storage:g.storage});assert.equal(copy.importSave(g.exportSave()),true);copy.act('continue');
  assert.equal(copy.owner,0);assert.equal(copy.festival.liveAttendance,g.festival.liveAttendance);
  assert.deepEqual(copy.journey.booking.lockedMetrics,booked);
}

// The ordered items themselves change, even for July's food-oriented show.
{
  const quiet=launch(1,{showId:'july',seed:71}),hyped=launch(1,{showId:'july',seed:71});
  Object.assign(hyped.journey.festival,{actionsUsed:5,hype:1});
  let baseline=0,boosted=0;
  for(let i=0;i<10000;i++){if(quiet._orderItem()<2)baseline++;if(hyped._orderItem()<2)boosted++;}
  assert(boosted>baseline+1800,JSON.stringify({baseline,boosted}));
  const high=Festival.modifiers(hyped.journey.festival,1,1),low=Festival.modifiers(hyped.journey.festival,.35,.36);
  assert(high.orderMultiplier<.7);assert(low.orderMultiplier>.99);
  assert(high.arrivalRate>low.arrivalRate&&high.cocktailBoost>low.cocktailBoost);
}

// Sustained real actions increase timed order traffic and introduce actual
// walking guests. Their ticket forecast stays frozen and saves keep the cast.
{
  const quiet=launch(0,{seed:81}),hyped=launch(0,{seed:81}),booked=structuredClone(Booking.forecast(hyped.journey.booking));
  let quietOrders=0,hypedOrders=0;
  for(const [game,increment]of [[quiet,()=>quietOrders++],[hyped,()=>hypedOrders++]]){
    const original=game._assignOrder.bind(game);
    game._assignOrder=(...args)=>{const before=game.orders.length;original(...args);if(game.orders.length>before)increment();};
  }
  advance(quiet,90);advance(hyped,90,{actions:true});
  assert(hypedOrders>quietOrders,JSON.stringify({quietOrders,hypedOrders}));
  Object.assign(evidence,{quietOrders,hypedOrders,quietPeople:people(quiet).length,hypedPeople:people(hyped).length});
  assert(people(hyped).length>people(quiet).length,'Earned response did not introduce live guests');
  assert(people(hyped).length<=64);assert.deepEqual(Booking.forecast(hyped.journey.booking),booked,'Live hype rewrote paid ticket metrics');
  assert(hyped.orders.length<=4+hyped.state.upgrades[2]);
  const arrivals=hyped.actors.filter(a=>a.audienceArrival);assert(arrivals.length>0);
  assert(arrivals.every(a=>walkable(a,false,false,a.layout)));
  const before=hyped.actors.map(a=>({id:a.id,x:a.x,z:a.z,action:a.action,visible:a.visible}));
  const copy=new SimBarGame({storage:hyped.storage});assert.equal(copy.importSave(hyped.exportSave()),true);copy.act('continue');
  assert.deepEqual(copy.actors.map(a=>({id:a.id,x:a.x,z:a.z,action:a.action,visible:a.visible})),before,'Reload removed or moved live arrivals');
  assert.equal(copy.festival.liveAttendance,hyped.festival.liveAttendance);
  copy._lastCall();assert.equal(copy.act('festival',{id:'horns'}).success,false);
  assert(copy.actors.every(a=>!a.festivalAction));
}

// Capacity is enforced by the actual actor population at all three venues.
for(const owner of [0,1,2]){
  const g=launch(owner,{showId:'double',full:true});
  assert.equal(people(g).length,Audience.capacity(owner));
  g.act('festival',{id:'encore'});g.journey.festival.arrivalCredit=1;
  g._festivalTick(.05);assert.equal(people(g).length,Audience.capacity(owner));
  assert.equal(g.journey.festival.arrivals,0);
  for(const attendance of [0,1,6,10,15,16,17,18,Audience.capacity(owner)-1,Audience.capacity(owner)])
    assert.equal(Audience.build({owner,attendance,showId:'double'}).length,attendance,'Authored table guests overflowed a small crowd');
}

// Authored wall-table guests really stand and walk to the front. Their clothes
// remain the same seated, walking and standing; no population is manufactured.
{
  const g=launch(0),seats=g.actors.filter(a=>a.waitingSeat),count=people(g).length;
  assert.equal(seats.length,6);assert(seats.every(a=>a.role==='seated'&&['Casual','Suit'].includes(a.styleKey)));
  assert(g.actors.filter(a=>a.audienceZone==='left-social').length>=3,'Freebird left social area is empty');
  for(let frame=0;frame<240;frame++){
    const before=new Map(seats.map(a=>[a.id,{x:a.x,z:a.z}]));serveQueue(g);g.tick(.25);
    if(g.reaction?.activeKind)g.act('quick',{kind:g.reaction.activeKind});
    for(const a of seats){const p=before.get(a.id);assert(Math.hypot(a.x-p.x,a.z-p.z)<=.38,'A waiting guest teleported to the stage');assert(walkable(a,false,false,a.layout));}
  }
  assert(seats.every(a=>a.seatReleased&&a.seatStageReached&&!a.waitingSeat&&a.role==='guest'&&a.homeX>=.6),JSON.stringify(seats.map(a=>({id:a.id,x:a.x,z:a.z,homeX:a.homeX,homeZ:a.homeZ,action:a.action,reached:a.seatStageReached,path:a._path?.length,pathAt:a._pathAt,blocked:a._blocked}))));
  assert.equal(people(g).length,count);
  const ff=launch(1),tableGuests=ff.actors.filter(a=>a.tableId);
  assert.equal(tableGuests.length,10);assert.deepEqual(new Set(tableGuests.map(a=>a.behavior)),new Set(['horns','dance']));
}

// Adding the new wall table cannot erase an old, already-paid active cast.
// Only coordinates made solid by the room renovation are moved safely.
{
  const g=launch(0),raw=JSON.parse(g.exportSave()),beforeMoney=g.cash,beforeMetrics=structuredClone(g.metrics());
  let changed=0;
  for(const a of raw.native.actors)if(a.audienceVersion===1){
    a.layoutVersion=2;
    if(a.id.startsWith('freebird-wall-')){
      const i=+a.id.slice(-1);a.id='audience-'+(24+i);a.role='guest';a.seat=false;a.waitingSeat=false;
      a.x=a.homeX=a.targetX=-8.65;a.z=a.homeZ=a.targetZ=-3.5+i;a.action='listen';changed++;
    }
  }
  assert.equal(changed,6);
  const expectedIds=raw.native.actors.filter(Audience.isAudience).map(a=>a.id).sort();
  const copy=new SimBarGame({storage:g.storage});assert.equal(copy.importSave(raw),true);copy.act('continue');
  assert.deepEqual(copy.actors.filter(Audience.isAudience).map(a=>a.id).sort(),expectedIds);
  assert.equal(copy.cash,beforeMoney);assert.equal(copy.metrics().attendance,beforeMetrics.attendance);
  assert.deepEqual(copy.journey.booking.lockedMetrics,g.journey.booking.lockedMetrics);
  assert(copy.actors.filter(Audience.isAudience).every(a=>walkable(a,false,false,a.layout)),'Saved guest remains inside the new wall table');
}

// A full room's assistance mission reuses a real customer body instead of
// producing an 81st customer. A later walk-in can fill the vacated place, and
// restoring that replacement must account for the reserved hidden base actor.
{
  const g=launch(1,{showId:'double',full:true}),capacity=Audience.capacity(1);
  Floor.spawnGuest(g.floor,0);const visitor=g._ensureGuest();
  assert(visitor.assistedSourceId);const source=g.actors.find(a=>a.id===visitor.assistedSourceId);
  assert.equal(source.visible,false);assert.equal(visitor.styleKey,Audience.wardrobe(source));
  assert.equal(g.festival.liveAttendance,capacity);assert.equal(g._ensureGuest(),visitor);
  const active=new SimBarGame({storage:g.storage});active.importSave(g.exportSave());active.act('continue');
  assert.equal(active.festival.liveAttendance,capacity);assert.equal(active.actors.find(a=>a.id===visitor.assistedSourceId).visible,false);
  visitor.visible=false;g.floor.guestComplete=true;g.floor.guestActive=false;
  assert.equal(g.festival.liveAttendance,capacity-1);
  g.act('festival',{id:'horns'});g.journey.festival.arrivalCredit=1;g._festivalTick(.05);
  assert.equal(g.journey.festival.arrivals,1);assert.equal(g.festival.liveAttendance,capacity);
  const members=g.actors.map(a=>({id:a.id,visible:a.visible,role:a.role})).sort((a,b)=>a.id.localeCompare(b.id));
  const restored=new SimBarGame({storage:g.storage});restored.importSave(g.exportSave());restored.act('continue');
  assert.equal(restored.festival.liveAttendance,capacity);assert.equal(restored.journey.festival.arrivals,1);
  assert.deepEqual(restored.actors.map(a=>({id:a.id,visible:a.visible,role:a.role})).sort((a,b)=>a.id.localeCompare(b.id)),members,'A full-room replacement disappeared or duplicated on reload');
}

console.log(JSON.stringify({passed:true,actions:Festival.actions.length,capacities:[64,80,44],...evidence,checks:['cooldowns','pause/save','actual cocktail mix','timed demand','live arrivals','locked receipts','population caps','surf group','closing guard','waiting seats walk','FF table cast','legacy room migration']}));
