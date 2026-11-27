import assert from 'node:assert/strict';
import {SimBarGame,Challenges} from './WebGLTemplate/FastGame/core.js';
import {Audience} from './WebGLTemplate/FastGame/audience.js';
import {launchPreparedDesk} from './native_test_workflow.mjs';

const storage=(entries=[])=>{const values=new Map(entries);return {values,getItem:k=>values.get(k)??null,setItem:(k,v)=>values.set(k,String(v))};};
function begin(g){
  launchPreparedDesk(g);
  while(g.phase==='guide')g.act('guide');
  assert.equal(g.phase,'playing');
  return g;
}
const cast=g=>g.actors.filter(Audience.isAudience).map(a=>[a.id,a.homeX,a.homeZ,Audience.wardrobe(a),a.name]);
const fixed=g=>g.actors.filter(a=>a.seat||a.tableId&&a.audienceZone==='standing-tables').map(a=>[a.id,a.homeX,a.homeZ]);
const lineup=g=>g.orders.map(o=>o.id);
const schedule=g=>({
  inspector:[g.journey.inspection.phase,g.journey.inspection.triggerAt],
  guest:g.reaction.guestScheduled?g.reaction.guestAt:null,
  paper:Array.from({length:g.reaction.paperCount},(_,i)=>[g.reaction['paperKind'+i],g.reaction['paperAt'+i]])
});
const different=values=>new Set(values.map(v=>JSON.stringify(v))).size;
for(const owner of [0,1,2]){
  const first=new SimBarGame({storage:storage()});first.act('owner',{index:owner});first.act('start');
  const template=[...first.storage.values];
  const games=Array.from({length:9},()=>{
    const g=new SimBarGame({storage:storage(template)});
    assert(g.act('continue').success);
    return begin(g);
  });
  for(const g of games){
    assert(g.journey.crowdSeed);
    assert.equal(g.orders.length,3);
    assert.equal(new Set(lineup(g)).size,3);
    assert.deepEqual(fixed(g),fixed(games[0]));
  }
  assert(different(games.map(g=>cast(g).map(a=>a.slice(0,3))))>1,'standing locations are unchanged');
  assert(different(games.map(g=>cast(g).map(a=>a[3])))>1,'wardrobes are unchanged');
  assert(different(games.map(lineup))>1,'opening order guests are unchanged');
  assert(different(games.map(schedule))>1,'event schedules are unchanged');
  const running=games[0],before={cast:cast(running),orders:lineup(running),schedule:schedule(running)};
  running.save();
  const restored=new SimBarGame({storage:running.storage});
  assert(restored.act('continue').success);
  assert.deepEqual({cast:cast(restored),orders:lineup(restored),schedule:schedule(restored)},before);
  console.log('owner',owner,'safe fixed spots',fixed(running).length,'variant casts',different(games.map(g=>cast(g).map(a=>a.slice(0,4)))),'order crews',different(games.map(lineup)),'event schedules',different(games.map(schedule)));
}

for(const day of [1,2,3,30]){
  const profiles=Array.from({length:120},(_,i)=>Challenges.eventPlan(i+1,{day}));
  assert(profiles.some(p=>p.guestAt>0)&&profiles.some(p=>!p.guestAt));
  assert(profiles.some(p=>p.guestAt>p.inspectionAt&&p.inspectionAt)&&profiles.some(p=>p.guestAt&&p.inspectionAt>p.guestAt));
  if(day<=2)assert(profiles.every(p=>p.inspectionAt>=55&&p.inspectionAt<=90));
  else assert(profiles.some(p=>p.inspectionAt===0));
}
