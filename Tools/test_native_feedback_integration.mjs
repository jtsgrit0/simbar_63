import assert from 'node:assert/strict';
import {SimBarGame,Booking} from './WebGLTemplate/FastGame/core.js';
import {Audience} from './WebGLTemplate/FastGame/audience.js';
import {launchPreparedDesk} from './native_test_workflow.mjs';

const memory=()=>{const map=new Map();return{getItem:key=>map.get(key)??null,setItem:(key,value)=>map.set(key,String(value)),removeItem:key=>map.delete(key)};};
const meters=g=>({satisfaction:g.state.satisfaction,performance:g.state.performance});
const percentages=g=>Object.fromEntries(Object.entries(meters(g)).map(([key,value])=>[key,Math.round(value*10000)/100]));
const evidence={};
function launch(owner=1,seed=71){
  const g=new SimBarGame({storage:memory(),seed});
  assert.equal(g.act('owner',{index:owner}).success,true);assert.equal(g.act('start').success,true);
  assert(Booking.isCampaign(g.journey.booking));launchPreparedDesk(g);
  while(g.phase==='guide')assert.equal(g.act('guide').success,true);
  assert.equal(g.phase,'playing');return g;
}
function restore(raw){
  const g=new SimBarGame({storage:memory()});assert.equal(g.importSave(raw),true);assert.equal(g.act('continue').success,true);
  assert.equal(g.phase,'playing');return g;
}
function advance(g,seconds){for(let t=0;t<seconds;t+=.1)g.tick(Math.min(.1,seconds-t));}
function serveFront(g,{quality=1,wait=0}={}){
  const order=g.orders[0];assert(order,'The real campaign should have a demand-seeded front order');
  order.patience=order.maxPatience*(1-wait);
  const result=g._serve(order.item,false,quality);assert.equal(result.success,true);return result;
}

// The actual campaign launch and the public feedback getter both begin at zero.
for(const owner of [0,1,2]){
  const g=launch(owner);
  assert.deepEqual(meters(g),{satisfaction:0,performance:0});
  assert.equal(g.spectatorFeedback.satisfaction,0);assert.equal(g.spectatorFeedback.performance,0);
  assert.equal(g.spectatorFeedback.audienceCount,g.actors.filter(a=>Audience.isAudience(a)&&a.visible!==false).length);
  assert.equal(g.journey.feedback.session,g.journey.session);assert.equal(g.journey.feedback.owner,owner);
  advance(g,4);assert.deepEqual(meters(g),{satisfaction:0,performance:0},'Merely opening a venue is not positive experience');
}

// Start from the exact same real campaign/customer/order snapshot. Changing
// quality or actual waiting time changes service judgement at equal order count.
const seed=launch().exportSave();
const fast=restore(seed),late=restore(seed),poor=restore(seed);
serveFront(fast);serveFront(late,{wait:.98});serveFront(poor,{quality:.2});
assert.equal(fast.journey.orders,1);assert.equal(late.journey.orders,1);assert.equal(poor.journey.orders,1);
assert(fast.state.satisfaction>late.state.satisfaction);assert(fast.state.satisfaction>poor.state.satisfaction);
for(const g of [fast,late,poor])assert.equal(g.state.performance,0,'The service hook must not grant concert points');
assert.equal(fast.spectatorFeedback.satisfaction,fast.state.satisfaction);
evidence.equalOrderCounts={fastPerfect:percentages(fast),latePerfect:percentages(late),poor:percentages(poor)};

// Pressing the real action only schedules a physical audience reaction. Its
// witnessed duration is evaluated on later actual game ticks, independently.
const active=restore(seed),before=meters(active);
assert.equal(active.act('festival',{id:'horns'}).success,true);
assert.deepEqual(meters(active),before,'A button click cannot instantly award either meter');
assert(active.actors.some(a=>a.festivalAction==='horns'));
advance(active,5);
assert(active.state.performance>0);assert(active.state.performance>active.state.satisfaction*4);
evidence.afterFiveSecondsOfHorns=percentages(active);

const muted=restore(seed);assert.equal(muted.act('musicState',{paused:true}).success,true);
assert.equal(muted.act('festival',{id:'horns'}).success,true);advance(muted,5);
assert.deepEqual(meters(muted),{satisfaction:0,performance:0},'A muted show is not heard engagement');

// A genuine ledger and its two earned meters survive the same public save /
// Continue path the player uses; no evidence is granted twice during restore.
serveFront(active,{quality:.65,wait:.2});active.act('pause');
const earned=meters(active),ledger=structuredClone(active.journey.feedback),saved=active.exportSave();
const resumed=restore(saved);
assert.deepEqual(resumed.journey.feedback,ledger);
assert.deepEqual(meters(resumed),earned);assert.equal(resumed.spectatorFeedback.satisfaction,earned.satisfaction);
assert.equal(resumed.state.money,active.state.money);assert.equal(resumed.journey.secondsLeft,active.journey.secondsLeft);
evidence.savedAndContinued=percentages(resumed);

// Old saves have no judgement evidence. Preserve the actual business progress,
// wallet, orders and clock while refusing to import the old decorative94/98%.
const old=JSON.parse(saved),oldJourney=JSON.parse(old.journeyJson);delete oldJourney.feedback;
old.journeyJson=JSON.stringify(oldJourney);old.satisfaction=.94;old.performance=.98;
const legacy=restore(JSON.stringify(old));
assert.deepEqual(meters(legacy),{satisfaction:0,performance:0});
assert.equal(legacy.state.money,old.money);assert.equal(legacy.journey.orders,oldJourney.orders);
assert.equal(legacy.journey.secondsLeft,oldJourney.secondsLeft);assert.equal(legacy.journey.session,oldJourney.session);
assert.deepEqual(legacy.orders,old.native.orders);assert.equal(legacy.journey.booking.showId,oldJourney.booking.showId);
evidence.legacyMigration={...percentages(legacy),preservedCash:legacy.state.money,preservedOrders:legacy.journey.orders,preservedSeconds:legacy.journey.secondsLeft};

// Enter closing through the real show timer. Departure, floor events, closing
// save/Continue and acknowledgement must never erase or inflate earned feedback.
const end=restore(saved),ending=meters(end),endingLedger=structuredClone(end.journey.feedback);
end.journey.secondsLeft=.05;end.tick(.05);assert.equal(end.phase,'closing');
assert.deepEqual(meters(end),ending);assert.deepEqual(end.journey.feedback,endingLedger);
end.save();const closingSave=end.exportSave(),continued=new SimBarGame({storage:memory()});
assert.equal(continued.importSave(closingSave),true);continued.act('continue');assert.equal(continued.phase,'closing');
assert.deepEqual(meters(continued),ending);assert.deepEqual(continued.journey.feedback,endingLedger);
let closingSeconds=0;
for(;closingSeconds<420&&continued.phase!=='results';closingSeconds+=.1){
  continued.tick(.1);
  if(continued.reaction?.activeKind)continued.act('quick',{kind:continued.reaction.activeKind});
  if(continued.farewellPending)continued.act('farewellAck');
  assert.deepEqual(meters(continued),ending);
}
assert.equal(continued.phase,'results','Actual after-show choreography and floor work should finish');
assert(continued.actors.filter(Audience.isAudience).every(a=>a.visible===false));
assert.deepEqual(continued.journey.feedback,endingLedger);
evidence.afterClosing={...percentages(continued),closingSeconds:Math.round(closingSeconds*10)/10};

// A subsequent launch is a new judgement, even when it reuses spectator IDs.
const previousSession=continued.journey.session;assert.equal(continued.act('next').success,true);
assert.equal(continued.phase,'planning');launchPreparedDesk(continued);
while(continued.phase==='guide')continued.act('guide');
assert.equal(continued.journey.session,previousSession+1);assert.deepEqual(meters(continued),{satisfaction:0,performance:0});
assert.equal(continued.spectatorFeedback.satisfaction,0);assert.equal(continued.spectatorFeedback.performance,0);
assert(Object.values(continued.journey.feedback.guests).every(g=>g.orders===0&&g.enjoyedSeconds===0));
evidence.nextNight=percentages(continued);
console.log(JSON.stringify(evidence,null,2));
console.log('PASS: actual campaign0% launch, independent service/action evaluation, exact resume, safe legacy migration, frozen closing and next-night reset.');
