import assert from 'node:assert/strict';
import {pathfind,segment,setRoute,setCrowdRoute,move,distance} from './WebGLTemplate/FastGame/navigation.js';

const exit={x:8.8,z:-5.55};
const clearance=(a,b,p)=>{
  const dx=b.x-a.x,dz=b.z-a.z,length=dx*dx+dz*dz;
  const t=length?Math.max(0,Math.min(1,((p.x-a.x)*dx+(p.z-a.z)*dz)/length)):0;
  return Math.hypot(p.x-a.x-t*dx,p.z-a.z-t*dz);
};

// All three venue meshes share an exit coordinate but have different furniture.
// Warming the static exit cache must never retain another patron's clearance.
const routes=new Map();
for(const layout of [undefined,'ff','freebird']){
  const start={x:0,z:-3,layout},body={x:7.6,z:-5};
  const empty=pathfind(start,exit),occupied=pathfind(start,exit,{people:[body]});
  assert(empty.length>1&&occupied.length>1);
  assert.notDeepEqual(occupied,empty,'A newly occupied aisle reused cached body clearance');
  assert.deepEqual(pathfind(start,exit),empty,'A departing patron remained in the terrain cache');
  for(const [index,p]of occupied.entries()){
    const previous=index?occupied[index-1]:start;
    assert(segment(previous,p,false,false,layout),'Exit route crosses venue furniture');
    assert(clearance(previous,p,body)>=.38-1e-8,'Exit route crosses a waiting patron');
  }
  routes.set(layout,empty);

  const actor={...start,id:'leaving-guest',role:'guest',visible:true,y:0};
  const waiting={...body,id:'waiting-guest',role:'guest',visible:true,y:0};
  assert(setRoute(actor,exit,[actor,waiting]));
  for(let frame=0;frame<300&&distance(actor,exit)>.18;frame++){
    const previous={x:actor.x,z:actor.z};
    move(actor,.05,1.45,[actor,waiting]);
    assert(distance(actor,previous)<.09,'An exiting patron teleported between waypoints');
    assert(distance(actor,waiting)>=.34,'An exiting patron walked through a waiting patron');
  }
  assert(distance(actor,exit)<=.18,'A cached route no longer reaches the actual doorway');
}
for(const [layout,expected]of routes)assert.deepEqual(pathfind({x:0,z:-3,layout},exit),expected,'Owner switching leaked a different terrain mesh');

// A closing-only precision step can cross a genuine movement-width gap when
// no coarse mesh endpoint remains. Corrupt/old optional history is bounded
// before inspecting it, and the public target remains the actual destination.
for(const history of ['old-invalid-value',{},[null,{x:Infinity,z:0},{x:8,z:-5}],Array(10000).fill({x:0,z:0})]){
  const actor={id:'precision-guest',role:'guest',layout:'ff',x:8.5075,z:-5.4,y:0,visible:true,_crowdHistory:history};
  const others=[{id:'north',x:8.8,z:-5.1633445395759745},{id:'south',x:8.433102903476762,z:-5.766897096523239}]
    .map(a=>({...a,role:'guest',y:0,visible:true}));
  const people=[actor,...others];
  assert.equal(setRoute(actor,exit,people),false,'The fixture no longer exercises a blocked mesh endpoint');
  assert.equal(setCrowdRoute(actor,exit,people),true);
  assert.equal(actor.targetX,exit.x);assert.equal(actor.targetZ,exit.z);
  assert(actor._crowdHistory.length<=6);
  const old={x:actor.x,z:actor.z};
  assert.equal(move(actor,.05,1.45,people),false,'A local precision step counted as arrival at the full goal');
  assert(distance(actor,old)<=1.45*.05+1e-8);
  assert(others.every(p=>distance(actor,p)>=.34-1e-8));
}

console.log('PASS: cached exit terrain respects moving bodies, all three owner meshes, continuous walking and physical doorway arrival.');
