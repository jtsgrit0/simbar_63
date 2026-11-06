import assert from 'node:assert/strict';
import {SimBarGame,SAVE_KEY} from './WebGLTemplate/FastGame/core.js';
import {BAND_MEMBERS,GREETING_SECONDS} from './WebGLTemplate/FastGame/aftershow.js';

function storage(){const map=new Map();return{getItem:k=>map.get(k)??null,setItem:(k,v)=>map.set(k,String(v))};}
function openNight(seed=12){const g=new SimBarGame({storage:storage(),seed});g.act('start');g.act('launch');for(let i=0;i<3;i++)g.act('guide');return g;}
function endPerformance(g){g.journey.secondsLeft=.05;g.tick(.05);assert.equal(g.phase,'closing');assert(g.afterShowPending);}
function finish(g,max=120){for(let t=0;t<max&&g.phase!=='results';t+=.05){g.tick(.05);if(g.reaction.activeKind)g.act('quick',{kind:g.reaction.activeKind});}return g;}

// A night without floor missions still shows the entire farewell before paying.
const g=openNight();const wallet=g.state.money;endPerformance(g);
assert.equal(g.journey.settledSession,0);assert.equal(g.state.money,wallet);
const visited=new Map(BAND_MEMBERS.map(id=>[id,new Set()]));
let previous=new Map(g.actors.filter(a=>a.role==='band').map(a=>[a.id,{x:a.x,z:a.z}]));
for(let t=0;t<120&&g.phase!=='results';t+=.05){
  g.tick(.05);
  for(const m of g.journey.afterShow.members){
    visited.get(m.id).add(m.phase);
    const a=g.actors.find(a=>a.id===m.id),before=previous.get(m.id);
    // The aisle walker can finish a sub-1.5 cm waypoint without spending its
    // remaining frame budget; allow that small correction, never a large jump.
    assert(Math.hypot(a.x-before.x,a.z-before.z)<=.12,`${m.id} teleported`);
    assert(a.stageLevel>=0&&a.stageLevel<=1);
    if(m.phase==='highfive'||m.phase==='hug'){
      assert.equal(a.stageLevel,0);assert.equal(a.moving,false);assert(a._partner);assert(a.actionProgress>=0&&a.actionProgress<=1);
      const fan=g.actors.find(f=>f.id===a._partner),radius=m.phase==='highfive'?.60:.43;
      assert(Math.abs(Math.hypot(a.x-fan.x,a.z-fan.z)-radius)<=.041,'Gesture began before precise contact approach finished');
    }
    previous.set(m.id,{x:a.x,z:a.z});
  }
  if(g.reaction.activeKind)g.act('quick',{kind:g.reaction.activeKind});
  if(g.afterShowPending)assert.equal(g.journey.settledSession,0,'Receipt paid before the band exited');
}
assert.equal(g.phase,'results',JSON.stringify(g.journey.afterShow));
assert(g.journey.afterShow.audience.every(a=>a.phase==='done'),'The last greeting guest must finish walking home before settlement freezes the room.');
for(const fan of g.journey.afterShow.audience){
  const a=g.actors.find(a=>a.id===fan.id);
  assert(Math.hypot(a.x-fan.homeX,a.z-fan.homeZ)<=.2,'Returned guest wandered away before the last greeting finished');
  assert.equal(a.moving,false);assert.equal(a._path.length,0);
}
for(const id of BAND_MEMBERS){
  for(const phase of ['stage','approach','highfive','hug','exit','gone'])assert(visited.get(id).has(phase),`${id} missed ${phase}`);
  const a=g.actors.find(a=>a.id===id);assert.equal(a.visible,false);assert(Math.hypot(a.x-8.8,a.z+5.55)<.22);
}

// Saving during an actual gesture preserves the partner, position and elapsed
// gesture time. Pausing, continuing and reloading cannot restart the handshake.
const mid=openNight(21);endPerformance(mid);
for(let t=0;t<60&&!mid.journey.afterShow.members.some(m=>m.phase==='highfive');t+=.05)mid.tick(.05);
const active=mid.journey.afterShow.members.find(m=>m.phase==='highfive');assert(active);
mid.tick(.35);mid.act('pause');const before=JSON.parse(mid.exportSave());
const restored=new SimBarGame({storage:mid.storage});
assert.deepEqual(restored.journey.afterShow,JSON.parse(before.journeyJson).afterShow);
const member=restored.journey.afterShow.members.find(m=>m.id===active.id),oldTimer=member.timer;
assert(oldTimer>0&&oldTimer<GREETING_SECONDS.highfive);
restored.tick(2);assert.equal(member.timer,oldTimer);restored.act('continue');restored.tick(.1);
assert(member.timer>oldTimer);assert(member.timer<oldTimer+.101);
finish(restored);assert.equal(restored.phase,'results');
const earned=restored.state.money,xp=restored.journey.xp,completed=restored.journey.completedNights;
restored.save();const receipt=new SimBarGame({storage:restored.storage});receipt.act('continue');receipt.tick(5);receipt._settleNight();
assert.equal(receipt.state.money,earned);assert.equal(receipt.journey.xp,xp);assert.equal(receipt.journey.completedNights,completed);
assert.equal(receipt.journey.afterShow.completed,true);

// A pre-feature closing save has no choreography fields. Continue starts the
// new routine from the actors' saved positions without paying a second receipt.
const legacy=openNight();endPerformance(legacy);const old=JSON.parse(legacy.exportSave());
const journey=JSON.parse(old.journeyJson);delete journey.afterShow;old.journeyJson=JSON.stringify(journey);legacy.storage.setItem(SAVE_KEY,JSON.stringify(old));
const upgraded=new SimBarGame({storage:legacy.storage});upgraded.act('continue');assert(upgraded.journey.afterShow?.started);assert.equal(upgraded.phase,'closing');finish(upgraded);assert.equal(upgraded.phase,'results');
console.log('PASS: walked stage descent, individual high-five and hug, complete exit before settlement, gesture pause/save/resume, legacy closing import, once-only receipt');
