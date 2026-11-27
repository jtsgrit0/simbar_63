
import assert from 'node:assert/strict';
import {SimBarGame,SAVE_KEY} from './WebGLTemplate/FastGame/core.js';
import {launchPreparedDesk} from './native_test_workflow.mjs';

function memory(entries=[]){
  const values=new Map(entries);
  return {getItem:key=>values.get(key)??null,setItem:(key,value)=>values.set(key,String(value))};
}
const select=(g,index)=>assert.equal(g.act('owner',{index}).success,true);
const economy=g=>[g.cash,g.state.staff.slice(),g.state.upgrades.slice(),g.state.wageDebt];
const clean=[650,[0,0,0],[0,0,0],0];
const records=g=>[g.journey.xp,g.journey.bestScore,g.journey.bestStars];
function settle(g){
  while(g.phase==='guide')g.act('guide');
  for(let i=0;i<6000&&g.phase!=='results';i++){
    if(g.farewellPending)g.act('farewellAck');
    if(g.reaction.activeKind)g.act('quick',{kind:g.reaction.activeKind});
    g.tick(.1);
  }
  assert.equal(g.phase,'results');
}
const storage=memory();
let g=new SimBarGame({storage,seed:1});
select(g,2);g.state.money=1250;g.act('start');launchPreparedDesk(g);
g.act('lobby');assert.equal(g.act('owner',{index:1}).code,'venue_locked');
g.act('continue');settle(g);
const firstCash=g.cash;
Object.assign(g.journey,{xp:240,bestScore:1600,bestStars:2});
const petRecords=records(g);
assert.equal(g.journey.completedNights,1);
assert(firstCash>1250);
g.act('next');
assert(g.act('hire',{index:0}).success);
assert(g.act('upgrade',{index:1}).success);
g.state.wageDebt=g.payroll;
const pet=economy(g);
assert.deepEqual(pet,[firstCash-1200,[1,0,0],[0,1,0],45]);
assert(g.act('deskEntranceChoice',{choice:'band'}).success);
g.act('lobby');select(g,1);
assert.deepEqual(economy(g),clean);
assert.equal(g.actors.find(a=>a.id==='hired-0').visible,false);
assert.equal(g.journey.completedNights,0);
assert.deepEqual(records(g),[0,0,0]);
assert.equal(g.hasSave,false);
g.act('start');
assert.equal(g.act('upgrade',{index:1}).code,'growth_locked');
assert(g.act('promote').success);
const ff=economy(g);
assert(ff[0]<650);
g.act('lobby');select(g,0);assert.deepEqual(economy(g),clean);
g=new SimBarGame({storage,seed:1});assert.equal(g.owner,0);assert.deepEqual(economy(g),clean);
select(g,2);assert.deepEqual(economy(g),pet);
g.act('continue');assert.equal(g.journey.booking.entranceMusicChoice,'band');
assert(g.act('payDebt').success);assert.equal(g.cash,pet[0]-45);
g.act('lobby');select(g,1);assert.deepEqual(economy(g),ff);
g.act('continue');assert.equal(g.journey.dayOpeningCash,650);
launchPreparedDesk(g);settle(g);
assert.equal(g.journey.resultProfit,g.cash-650);
const ffSettled=economy(g),ffRecords=records(g);
assert(ffRecords[0]>0);assert.notDeepEqual(ffRecords,petRecords);
g.act('lobby');select(g,2);
assert.equal(g.cash,pet[0]-45);assert.deepEqual(records(g),petRecords);
g=new SimBarGame({storage,seed:1});assert.equal(g.owner,2);assert.equal(g.cash,pet[0]-45);
select(g,1);assert.deepEqual(economy(g),ffSettled);assert.deepEqual(records(g),ffRecords);

const exported=g.exportSave();
const portable=new SimBarGame({storage:memory([['SimBar.Owner','female']]),seed:1});
assert(portable.importSave(exported));assert.equal(portable.owner,1);
select(portable,2);assert.deepEqual(economy(portable),[pet[0]-45,[1,0,0],[0,1,0],0]);
assert.deepEqual(records(portable),petRecords);
select(portable,0);assert.deepEqual(economy(portable),clean);assert.deepEqual(records(portable),[0,0,0]);
assert.equal(Object.keys(JSON.parse(portable.exportSave()).otherVenueSaves).length,2);

const legacy=JSON.parse(exported);
delete legacy.venueSaveId;delete legacy.otherVenueSaves;
Object.assign(legacy,{money:3210,staff:[1,0,1],upgrades:[2,1,3],wageDebt:95});
const old=memory([[SAVE_KEY,JSON.stringify(legacy)],['SimBar.Owner','ff']]);
g=new SimBarGame({storage:old,seed:1});
assert.deepEqual(economy(g),[3210,[1,0,1],[2,1,3],95]);
assert.deepEqual(records(g),ffRecords);
select(g,2);assert.deepEqual(economy(g),clean);assert.deepEqual(records(g),[0,0,0]);
select(g,0);assert.deepEqual(economy(g),clean);
g=new SimBarGame({storage:old});select(g,1);
assert.deepEqual(economy(g),[3210,[1,0,1],[2,1,3],95]);

const live=new SimBarGame({storage:memory(),seed:1});
select(live,2);live.act('start');launchPreparedDesk(live);
const raw=JSON.parse(live.exportSave());delete raw.venueSaveId;delete raw.otherVenueSaves;
const migrated=new SimBarGame({storage:memory([[SAVE_KEY,JSON.stringify(raw)],['SimBar.Owner','ff']])});
assert.equal(migrated.owner,2);
assert.equal(migrated.cash,live.cash);
migrated.act('continue');settle(migrated);migrated.act('lobby');
select(migrated,1);assert.deepEqual(economy(migrated),clean);
console.log('PASS: venue balances, staff, upgrades, wages, venue XP and records, planning, settlement, restart, export and legacy attribution');
