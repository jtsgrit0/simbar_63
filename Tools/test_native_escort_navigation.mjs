import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {SimBarGame} from './WebGLTemplate/FastGame/core.js';
import {Audience} from './WebGLTemplate/FastGame/audience.js';
import {distance,segment} from './WebGLTemplate/FastGame/navigation.js';

const data=new Map(),storage={getItem:k=>data.get(k)??null,setItem:(k,v)=>data.set(k,String(v))};
const g=new SimBarGame({storage});
// Untouched export from a real near-capacity FF browser run: every concert
// participant has left and cleanup is complete, but the escort's fixed offset
// is inside the stage/DJ boundary. Waiting longer never cleared this save.
assert(g.importSave(readFileSync(new URL('./fixtures/aftershow-ff-escort-dj-stalled.json',import.meta.url),'utf8')));
g.act('continue');
assert.equal(g.owner,1);assert.equal(g.escort.phase,'exit');
const guest=g.actors.find(a=>a.id==='assisted-guest'),worker=g.actors.find(a=>a.id===g.escort.worker);
const blocked={x:guest.x-.36,z:guest.z+.18};
assert.equal(g._walkable(blocked,true,false),false);
const side=g._escortFollowGoal(worker,guest,blocked);
assert(side&&g._walkable(side,true,false),'The escort must choose a real clear side');
assert(distance(side,guest)<=.76);

let seconds=0;
for(;seconds<45&&g.phase!=='results';seconds+=.05){
  const before=[worker,guest].map(a=>({x:a.x,z:a.z,visible:a.visible}));
  g.tick(.05);
  for(const [index,actor]of [worker,guest].entries()){
    assert(distance(actor,before[index])<=.19,'The escort teleported around an obstacle');
    assert(segment({...before[index],layout:'ff'},actor,index===0,false),'The escort crossed furniture');
  }
  if(g.farewellPending)g.act('farewellAck');
  if(g.reaction?.activeKind)g.act('quick',{kind:g.reaction.activeKind});
  if(guest.visible!==false)assert.notEqual(g.phase,'results','A receipt was paid before the actual guest left');
}
assert.equal(g.phase,'results');assert.equal(g.escort,null);assert.equal(g.floorTasksPending,false);
assert.equal(guest.visible,false);assert(distance(guest,{x:8.7,z:-5.4})<=.2);
assert(g.actors.filter(Audience.isAudience).every(a=>a.visible===false));
const receipt={cash:g.cash,xp:g.journey.xp,completed:g.journey.completedNights};
const reloaded=new SimBarGame({storage});reloaded.act('continue');reloaded.tick(1);reloaded.act('farewellAck');
assert.deepEqual({cash:reloaded.cash,xp:reloaded.journey.xp,completed:reloaded.journey.completedNights},receipt);
console.log(JSON.stringify({passed:true,scenario:'FF stage-edge escort stall',seconds:Math.round(seconds*100)/100,actualExit:true,continuousMovement:true,onceOnlyReceipt:true}));
