import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {SimBarGame} from './WebGLTemplate/FastGame/core.js';
import {Audience} from './WebGLTemplate/FastGame/audience.js';
import {createAfterShow,tickAfterShow,EXIT_POINT} from './WebGLTemplate/FastGame/aftershow.js';

// Actual FF78 service-run export, seed2: the returning employee and outbound
// audience surrounded the final mesh cells. Fifty patrons still remained and
// the last greeting was waiting while the employee could not leave the door.
const memory=()=>{const map=new Map();return{getItem:k=>map.get(k)??null,setItem:(k,v)=>map.set(k,String(v))};};
const storage=memory(),game=new SimBarGame({storage});
assert.equal(game.importSave(readFileSync(new URL('./fixtures/aftershow-dense-ff-doorway.json',import.meta.url),'utf8')),true);
game.act('continue');assert.equal(game.phase,'closing');
assert.equal(game.metrics().attendance,78);assert.equal(game.floorTasksPending,true);
const waiting=game.actors.filter(a=>Audience.isAudience(a)&&a.visible!==false).length;
assert.equal(waiting,50);assert(game.journey.afterShow.members.some(m=>m.greeting<2));
let seconds=0;
for(;seconds<150&&game.phase!=='results';seconds+=.05){
  const before=new Map(game.actors.map(a=>[a.id,{x:a.x,z:a.z,visible:a.visible}]));
  game.tick(.05);
  for(const actor of game.actors){
    const old=before.get(actor.id);if(!old)continue;
    assert(Math.hypot(actor.x-old.x,actor.z-old.z)<=.19,'Exit recovery teleported '+actor.id);
    if(old.visible&&actor.visible===false&&(Audience.isAudience(actor)||actor.role==='band'))
      assert(Math.hypot(actor.x-EXIT_POINT.x,actor.z-EXIT_POINT.z)<=.181,'Disappeared away from the doorway: '+actor.id);
  }
  if(game.reaction?.activeKind)game.act('quick',{kind:game.reaction.activeKind});
  if(game.farewellPending)game.act('farewellAck');
}
assert.equal(game.phase,'results','A dense saved exit must complete without skipping the crowd');
assert.equal(game.actors.filter(a=>Audience.isAudience(a)&&a.visible!==false).length,0);
assert(game.journey.afterShow.members.every(m=>m.phase==='gone'));
const receipt=[game.cash,game.journey.completedNights,game.journey.xp];
game.save();const restored=new SimBarGame({storage});restored.act('continue');restored.tick(1);
assert.deepEqual([restored.cash,restored.journey.completedNights,restored.journey.xp],receipt,'Door recovery paid the same receipt twice');

// Isolate the exact triangular last-step blockage. The conservative40cm mesh
// has no endpoint, while a continuous34cm-clear path exists for the first body.
const actors=[
  {id:'audience-18',x:8.5075,z:-5.4},
  {id:'audience-59',x:8.8,z:-5.1633445395759745},
  {id:'audience-61',x:8.433102903476762,z:-5.766897096523239},
].map(a=>({...a,role:'guest',visible:true,layout:'ff',y:0,_path:[],_pathAt:0}));
const state=createAfterShow(actors,1);state.departureStarted=true;
for(const member of state.members){member.phase='gone';member.greeting=2;}
for(let t=0;t<5&&!state.completed;t+=.05){
  const before=actors.map(a=>({x:a.x,z:a.z}));tickAfterShow(state,actors,.05);
  for(let i=0;i<actors.length;i++){
    const actor=actors[i];assert(Math.hypot(actor.x-before[i].x,actor.z-before[i].z)<=1.45*.05+1e-8);
    if(!actor.visible)assert(Math.hypot(actor.x-EXIT_POINT.x,actor.z-EXIT_POINT.z)<=.181);
    for(let j=i+1;j<actors.length;j++)if(actor.visible&&actors[j].visible)
      assert(Math.hypot(actor.x-actors[j].x,actor.z-actors[j].z)>=.34-1e-8,'Doorway fallback crossed another person');
  }
}
assert(state.completed&&actors.every(a=>!a.visible),'A short reachable doorway segment was rejected by the coarse mesh');
console.log(JSON.stringify({passed:true,savedWaiting:waiting,closingSeconds:Math.round(seconds*10)/10,checks:['continuous movement','doorway clearance','actual exits','saved dense crowd','once-only receipt']}));
