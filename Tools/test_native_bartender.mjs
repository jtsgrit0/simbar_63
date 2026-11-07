import assert from 'node:assert/strict';
import {SimBarGame} from './WebGLTemplate/FastGame/core.js';
import {walkable} from './WebGLTemplate/FastGame/navigation.js';

const night=seed=>{
  const mem=new Map(),storage={getItem:k=>mem.get(k)||null,setItem:(k,v)=>mem.set(k,String(v))};
  const g=new SimBarGame({storage,seed});g.act('start');g.act('launch');while(g.phase==='guide')g.act('guide');return g;
};
const actor=g=>g.actors.find(a=>a.id==='bartender');
const g=night(17),baseline=night(17);
baseline._tickBartender=a=>{a.moving=false;a.action='serve';};
let minX=Infinity,maxX=-Infinity,maxStep=0,walking=0,serving=0,left=false,right=false;
for(let i=0;i<540;i++){
  const a=actor(g),before={x:a.x,z:a.z};g.tick(.05);baseline.tick(.05);
  const step=Math.hypot(a.x-before.x,a.z-before.z);maxStep=Math.max(maxStep,step);
  assert(step<=.033001,'Bartender teleported or exceeded its authored walking speed');
  assert.equal(a.z,-.565,'Bartender left the clear staff aisle');
  assert(walkable(a,true,false),'Bartender crossed solid furniture');
  minX=Math.min(minX,a.x);maxX=Math.max(maxX,a.x);
  if(a.moving){walking++;left ||= a.x<before.x;right ||= a.x>before.x;assert.equal(a.action,'walk');}
  else if(a.action==='serve')serving++;
}
assert(minX<=-8.915&&maxX>=-7.085,'The bartender never traversed both service ends');
assert(left&&right&&walking>100&&serving>20,'Walk and stationary service phases are missing');
assert.equal(g._seed,baseline._seed,'Patrol changed the gameplay random sequence');
for(const key of ['money','fans','pours','satisfaction','performance'])assert.equal(g.state[key],baseline.state[key],key);
assert.deepEqual(g.orders,baseline.orders,'Decorative staff movement changed orders');

g.act('pause');const paused={x:actor(g).x,z:actor(g).z,elapsed:g.elapsed};
for(let i=0;i<10;i++)g.tick(.1);
assert.deepEqual({x:actor(g).x,z:actor(g).z,elapsed:g.elapsed},paused,'Pause moved the bartender');
g.act('resume');g.tick(.35);g.save();
const loaded=new SimBarGame({storage:g.storage});loaded.act('continue');
assert.equal(actor(loaded).x,actor(g).x);assert.equal(actor(loaded).z,actor(g).z);
g.tick(.05);loaded.tick(.05);
assert.equal(actor(loaded).x,actor(g).x,'Restored patrol resumed at a different position');
assert.equal(actor(loaded).action,actor(g).action);

// Existing floor-mission ownership must replace the service route, not compete
// with patrol. Use the public closing transition and respond to actual prompts.
while(g.phase!=='results'&&g.elapsed<430){
  g.tick(.05);
  if(g.farewellPending)g.act('farewellAck');
  if(g.reaction.activeKind)g.act('quick',{kind:g.reaction.activeKind});
}
assert.equal(g.phase,'results','Bartender patrol blocked cleanup/after-show settlement');
assert.equal(g.floorTasksPending,false);
console.log(JSON.stringify({passed:true,minX,maxX,maxStep,walking,serving,pause:true,saveResume:true,closing:true}));
