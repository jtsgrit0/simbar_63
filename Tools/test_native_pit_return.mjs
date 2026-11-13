import {launchPreparedDesk} from './native_test_workflow.mjs';
import assert from 'node:assert/strict';
import {SimBarGame,Booking} from './WebGLTemplate/FastGame/core.js';

// This regression concerns the retained legacy pit's fixed .395m/s orbit.
// Campaign crowds have venue/actor-specific motion speeds and a different
// opening queue; test_native_audience covers that newer cast independently.
function night(seed=12){const mem=new Map(),storage={getItem:k=>mem.get(k)||null,setItem:(k,v)=>mem.set(k,String(v))};const g=new SimBarGame({storage,seed});g.journey.booking=Booking.newDeskPlan(1,0);g.act('start');launchPreparedDesk(g);while(g.phase==='guide')g.act('guide');return g;}
const g=night(),returns=new Set(),walkedHome=new Set();
// Let real initial orders expire. Patrons 1 and 4 must walk home and rejoin the
// moving pit after its shared phase has advanced during their bar queue.
for(let t=0;t<170;t+=.05){
  const wasFrozen=g.inspection.freezePerformers;
  const before=new Map(g.actors.filter(a=>a.role==='guest'&&a.slot<8).map(a=>[a.id,{x:a.x,z:a.z,action:a.action}]));
  g.tick(.05);
  for(const a of g.actors.filter(a=>a.role==='guest'&&a.slot<8)){
    const p=before.get(a.id),step=Math.hypot(a.x-p.x,a.z-p.z);
    assert(step<=.09,`${a.id} snapped ${step}m while ${p.action} became ${a.action}`);
    if(p.action==='return'&&a.action==='idle')walkedHome.add(a.id);
    if(p.action==='idle'&&a.action==='mosh'&&walkedHome.has(a.id)){returns.add(a.id);assert(Number.isFinite(a._pitPhaseOffset));}
    if(wasFrozen&&g.inspection.freezePerformers)assert.equal(step,0,'Inspection did not stop the pit');
    if(!wasFrozen&&!g.inspection.freezePerformers&&p.action==='mosh'&&a.action==='mosh'&&Math.abs(Math.hypot(p.x-5.75,p.z+1.25)-1.58)<1e-6){
      assert(Math.abs(step/.05-.395)<.001,'Normal orbit speed changed');
    }
  }
}
assert(returns.has('patron-4'),'Expired order patron did not walk home and rejoin the pit');

// Native actor saves retain the phase offset and continue the same trajectory.
const dancer=g.actors.find(a=>a.action==='mosh'),offset=dancer._pitPhaseOffset;
g.save();const reloaded=new SimBarGame({storage:g.storage});reloaded.act('continue');
assert.equal(reloaded.actors.find(a=>a.id===dancer.id)._pitPhaseOffset,offset);
const p={x:dancer.x,z:dancer.z};reloaded.tick(.05);
const continued=reloaded.actors.find(a=>a.id===dancer.id);
assert(Math.hypot(continued.x-p.x,continued.z-p.z)<.04);

// A new night may inherit a nonzero application clock. Its first orbit tick
// must start at each newly built actor's current angle too.
const fresh=night(21);fresh.elapsed=313.7;
const before=fresh.actors.filter(a=>a.role==='guest'&&a.slot<6).map(a=>({id:a.id,x:a.x,z:a.z}));
fresh.tick(.05);
for(const p of before){const a=fresh.actors.find(a=>a.id===p.id);assert(Math.hypot(a.x-p.x,a.z-p.z)<.09);}
console.log('PASS: queued patrons walk back into the pit continuously, orbit at .395m/s, preserve phase in saves and initialize at a nonzero night clock');
