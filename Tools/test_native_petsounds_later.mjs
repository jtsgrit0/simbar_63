import assert from 'node:assert/strict';
import {SimBarGame} from './WebGLTemplate/FastGame/core.js';
import {Encore} from './WebGLTemplate/FastGame/desk-encore.js';
import {LaterDesk} from './WebGLTemplate/FastGame/desk-later.js';

const storage=()=>{const m=new Map();return{getItem:k=>m.get(k)??null,setItem:(k,v)=>m.set(k,String(v))};};
function game(day,owner=2,sound=1,s=storage()){
  const g=new SimBarGame({storage:s,seed:13});
  assert(g.act('owner',{index:owner}).success);
  g.journey.day=day;g.journey.completedNights=day-1;
  g.state.upgrades[1]=sound;
  assert(g.act('start').success);
  return g;
}
const freshPlan=showId=>({day:5,showId,deskVersion:2,campaignContext:{venueOwner:2}});
const histories={};
for(const show of ['freewill','july']){
 let history=histories[show]=LaterDesk.sanitizeHistory(null);
 const seen=new Set();let last=null;
 for(let i=0;i<10;i++){
  const p=freshPlan(show);LaterDesk.prepare(p,history,()=>.49);
  assert(LaterDesk.scene(p));
  const id=p.laterScenes[show];assert(!seen.has(id));seen.add(id);last=id;
  p.locked=true;history=LaterDesk.consume(p,history);
 }
 assert.equal(seen.size,10);
 const p=freshPlan(show);LaterDesk.prepare(p,history,()=>.49);
 assert.notEqual(p.laterScenes[show],last);
 assert.equal(history[show].mask,1023);
 assert.equal(history[show==='july'?'freewill':'july'].mask,0);
}
let g=game(5),p=g.journey.booking;
assert(g.metrics().petSoundsLater&&!g.metrics().petSoundsEncore);
const drawn={...p.laterScenes};
assert.equal(g.act('launch').code,'desk_incomplete');
assert(g.act('selectShow',{showId:'freewill'}).success);
assert(g.act('deskEncoreChoice',{choice:0}).success);
const title=Encore.scene(p).title;
assert.deepEqual(LaterDesk.sanitizeHistory(g.journey.petDeskHistory),LaterDesk.sanitizeHistory(null));
g=new SimBarGame({storage:g.storage});assert(g.act('continue').success);p=g.journey.booking;
assert.deepEqual(p.laterScenes,drawn);assert.deepEqual(Encore.scene(p).title,title);assert.equal(p.encoreChoice,0);
assert(g.act('selectShow',{showId:'july'}).success);
assert(g.act('selectShow',{showId:'freewill'}).success);
assert.deepEqual(p.laterScenes,drawn);assert.equal(p.encoreChoice,undefined);
assert(g.act('deskEncoreChoice',{choice:1}).success);assert(g.metrics().canLaunch);
const order=g.menuEntry(Encore.effect(p).item);
assert(order&&!order.isFood);
assert(g.act('launch').success);
assert.equal(g.journey.petDeskHistory.freewill.mask,1<<drawn.freewill);
assert.equal(g.journey.petDeskHistory.july.mask,0);
const resumed=new SimBarGame({storage:g.storage});assert(resumed.act('continue').success);
assert.equal(resumed.journey.petDeskHistory.freewill.mask,1<<drawn.freewill);
assert.deepEqual(resumed.journey.booking.laterScenes,drawn);
for(let i=0;i<9000&&g.phase!=='results';i++){
 if(g.phase==='guide')g.act('guide');
 if(g.farewellPending)g.act('farewellAck');
 if(g.reaction.activeKind)g.act('quick',{kind:g.reaction.activeKind});
 g.tick(.1);
}
assert.equal(g.phase,'results');
assert(g.act('next').success);assert.equal(g.journey.day,6);
assert.notEqual(g.journey.booking.laterScenes.freewill,drawn.freewill);
assert(g.act('selectShow',{showId:'double'}).success);
assert(g.act('deskEncoreChoice',{choice:1}).success);
assert.equal(Encore.effect(g.journey.booking).speaker,'owner');
assert(g.act('launch').success);
assert.equal(g.journey.petDeskHistory.freewill.mask,1<<drawn.freewill);
assert.equal(Encore.scene(freshPlan('double')),Encore.scene({...freshPlan('double'),day:90}));
assert(!game(5,0).metrics().petSoundsLater);
assert(!game(5,1).metrics().petSoundsLater);
assert.equal(game(5,2,0).act('selectShow',{showId:'double'}).code,'show_locked');
assert(!game(4).metrics().petSoundsLater);
const old=game(7);old.act('selectShow',{showId:'july'});old.act('deskWork',{task:0});old.act('deskWork',{task:1});
delete old.journey.booking.laterScenes;delete old.journey.petDeskHistory;old.save();
const migrated=new SimBarGame({storage:old.storage});assert(migrated.act('continue').success);
assert.equal(migrated.journey.booking.showId,'july');
assert.equal(migrated.journey.booking.deskDoneMask,3);
assert(!migrated.metrics().canLaunch);assert(migrated.act('deskEncoreChoice',{choice:0}).success);
assert(migrated.menuEntry(Encore.effect(migrated.journey.booking).item).isFood);
assert(migrated.act('launch').success);
console.log('PASS: 10 unique dialogues per solo band, no immediate repeat after cycle, fixed joint show, owner scope, reload and old planning migration');
