import assert from 'node:assert/strict';
import {SimBarGame,Floor} from './WebGLTemplate/FastGame/core.js';

function memory(){const map=new Map();return{getItem:k=>map.get(k)??null,setItem:(k,v)=>map.set(k,String(v))};}
function night(seed=2){const g=new SimBarGame({storage:memory(),seed});g.act('start');g.act('launch');while(g.phase==='guide')g.act('guide');return g;}
function end(g){g.journey.secondsLeft=.05;g.tick(.05);assert.equal(g.phase,'closing');}
function restore(raw){const g=new SimBarGame({storage:memory()});assert(g.importSave(raw));g.act('continue');return g;}
function finish(g){for(let i=0;i<6000&&g.phase!=='results';i++){g.tick(.05);if(g.farewellPending)g.act('farewellAck');if(g.reaction.activeKind)g.act('quick',{kind:g.reaction.activeKind});}assert.equal(g.phase,'results');}
const counters=g=>[g.floor.confettiThrows,g.floor.tissueThrows,g.floor.litter];

// Seed 2 genuinely schedules no mid-set paper; seed 5 schedules both kinds at
// distinct random times. Finale guarantees do not alter this random schedule.
const empty=night(2),random=night(5);
assert.equal(empty.reaction.paperCount,0);
assert.deepEqual([0,1,2].map(i=>[random.reaction['paperKind'+i],random.reaction['paperAt'+i]]),
  [[0,24.369552612304688],[0,101.77439880371094],[1,130.70846557617188]]);
for(let i=0;i<3599;i++){
  empty.tick(.05);random.tick(.05);
  if(random.reaction.activeKind)random.act('quick',{kind:random.reaction.activeKind});
}
assert.deepEqual(counters(empty),[0,0,0]);assert.deepEqual(counters(random),[2,1,80]);
assert.equal(empty.floor.finaleEnsured,false);assert.equal(random.floor.finaleEnsured,false);
const fans=empty.state.fans,serial=empty._eventSerial;
end(empty);end(random);
assert.deepEqual(counters(empty),[1,1,56]);assert.equal(empty.floor.finaleEnsured,true);
assert.equal(empty.state.fans,fans+4);assert.equal(empty._eventSerial,serial+2);
assert.deepEqual(empty.effects.map(e=>e.kind),[0,1]);assert(empty.effects.every(e=>e.time===empty.elapsed));
assert.deepEqual(counters(random),[2,1,80]);assert.equal(random.effects.length,0,'An existing kind was thrown again at the finale');
assert.equal(empty.floor.cleanupActive,true);assert.equal(empty.journey.afterShow.started,true);assert.equal(empty.journey.afterShow.departureStarted,false);
assert.equal(empty.act('throwConfetti').success,false,'Finale accidentally exposed a manual spawn action');
const opening=empty.exportSave(),openingSerial=empty._eventSerial;
for(let i=0;i<5;i++)empty._beginClosing();
assert.deepEqual(counters(empty),[1,1,56]);assert.equal(empty._eventSerial,openingSerial);assert.equal(empty.state.fans,fans+4);
const resumed=restore(opening);assert.deepEqual(counters(resumed),[1,1,56]);assert.equal(resumed.effects.length,0,'Reload replayed the finale animation/reward');

// Cleanup proceeds while the four musicians are still meeting the audience.
// No automatic work is awarded without the rapid C response, and active F
// input at closing survives until its normal completion/timeout.
assert(empty.floor.guestActive,'Seed 2 must leave a real random guest task active');
if(empty.reaction.activeKind===2){
  empty.act('quick',{kind:2});const presses=empty.reaction.presses;
  empty._beginClosing();assert.equal(empty.reaction.activeKind,2);assert.equal(empty.reaction.presses,presses,'Closing erased partial guest input');
}
let sawPromptDuringGreetings=false,sawCleanedDuringGreetings=false,sawExactGestureWithWork=false,checkedInputGate=false;
for(let t=0;t<70&&!empty.journey.afterShow.departureStarted;t+=.05){
  const before=empty.floor.cleaned;empty.tick(.05);
  const greeting=empty.journey.afterShow.members.some(m=>['stage','approach','highfive','hug'].includes(m.phase));
  if(empty.reaction.activeKind===1){
    sawPromptDuringGreetings ||= greeting;
    if(!checkedInputGate){const cleaned=empty.floor.cleaned;empty.tick(.25);assert.equal(empty.floor.cleaned,cleaned,'Employees cleaned without a C work budget');checkedInputGate=true;}
    empty.act('quick',{kind:1});
  }
  if(empty.floor.cleaned>before){
    sawCleanedDuringGreetings ||= greeting;
    sawExactGestureWithWork ||= empty.journey.afterShow.members.some(m=>['highfive','hug'].includes(m.phase));
  }
}
assert(sawPromptDuringGreetings,'Guest F windows starved the C prompt until after greetings');
assert(sawCleanedDuringGreetings,'Staff cleanup never progressed during the greetings');
assert(sawExactGestureWithWork,'Cleanup never overlapped an actual high-five/hug');
assert(checkedInputGate);assert(empty.floor.guestActive,'The test accidentally completed the guest task instead of proving cleanup priority');
finish(empty);assert.equal(empty.journey.resultFloorBonus,70);assert.equal(empty.floor.cleanupRewardClaimed,true);
const paid=[empty.state.money,empty.journey.xp,empty.journey.completedNights];empty._settleNight();assert.deepEqual([empty.state.money,empty.journey.xp,empty.journey.completedNights],paid);

// A new finale fills only the missing kind, respecting the original caps and
// debt. The latch does not add work after any historical cleanup has begun.
const onlyTissue=Floor.newState();Floor.throw(onlyTissue,1,true);Floor.throw(onlyTissue,1,true);
assert.deepEqual(Floor.ensureFinale(onlyTissue).map(e=>e.kind),[0]);assert.deepEqual([onlyTissue.confettiThrows,onlyTissue.tissueThrows,onlyTissue.litter],[1,2,88]);
assert.deepEqual(Floor.ensureFinale(onlyTissue),[]);
for(const historic of [{cleanupStarted:true},{cleanupComplete:true},{cleanupRewardClaimed:true},{cleaned:8}]){
  const f={...Floor.newState(),confettiThrows:1,litter:24,...historic};delete f.finaleEnsured;
  Floor.sanitize(f);const before=[f.confettiThrows,f.tissueThrows,f.litter,f.cleaned];
  assert.equal(f.finaleEnsured,true);assert.deepEqual(Floor.ensureFinale(f),[]);assert.deepEqual([f.confettiThrows,f.tissueThrows,f.litter,f.cleaned],before);
}

// Old active greeting saves with no cleanup can acquire the missing finale.
// Already-completed farewells and settled receipts remain historical instead.
const old=JSON.parse(opening),j=JSON.parse(old.journeyJson);j.floorMissions=Floor.newState();delete j.floorMissions.finaleEnsured;old.journeyJson=JSON.stringify(j);
const legacy=restore(old);assert.deepEqual(counters(legacy),[1,1,56]);assert.equal(legacy.effects.length,2);assert.equal(legacy.floor.cleanupActive,true);
j.afterShow.completed=true;j.afterShow.departureStarted=true;j.afterShow.members.forEach(m=>m.phase='gone');j.afterShow.audience.forEach(a=>a.phase='gone');old.journeyJson=JSON.stringify(j);
const completed=restore(old);assert.deepEqual(counters(completed),[0,0,0]);assert.equal(completed.effects.length,0);assert.equal(completed.floor.finaleEnsured,true);assert(completed.farewellPending);
completed.act('farewellAck');assert.equal(completed.phase,'results');assert.equal(completed.journey.resultFloorBonus,0);
const historical=restore(completed.exportSave());assert.equal(historical.phase,'results');assert.deepEqual(counters(historical),[0,0,0]);assert.equal(historical.effects.length,0);

console.log('PASS: random mid-set schedule retained, both missing finale kinds create real effects/debt, cleanup C runs during greetings without starving behind F, idempotent closing/save migrations and once-only cleanup reward');
