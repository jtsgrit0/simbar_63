import assert from 'node:assert/strict';
import fs from 'node:fs';
import {assignPatronModels,audienceIdentity,patronPools,patronMotion,drawPatronModel} from './WebGLTemplate/FastGame/patron-models.js';
import {Audience} from './WebGLTemplate/FastGame/audience.js';
import {Festival} from './WebGLTemplate/FastGame/festival.js';
import {SimBarGame} from './WebGLTemplate/FastGame/core.js';
import {pixelAudiencePose} from './WebGLTemplate/FastGame/world.js';
import {launchPreparedDesk} from './native_test_workflow.mjs';
const rigs=JSON.parse(fs.readFileSync(new URL('./WebGLTemplate/FastGame/art/audience/patrons.json',import.meta.url)));
const png=fs.readFileSync(new URL('./WebGLTemplate/FastGame/art/audience/patrons.png',import.meta.url));
assert.equal(rigs.length,100);
assert.equal(png.readUInt32BE(16),640);
assert.equal(png.readUInt32BE(20),640);
assert.deepEqual(patronPools.map(p=>p.length),[33,33,34]);
assert.equal(new Set(patronPools.flat()).size,100);
const state={phase:'playing',owner:0};
const actions=['idle','walk','queue','return','arrive','exit','highfive','hug','support',...Festival.actions.map(x=>x.id)];
const traces=new Map();
for(const [i,rig] of rigs.entries()){
  assert(rig[0]<rig[1]&&rig[1]<rig[2]&&rig[2]<rig[3]&&rig[3]<61&&rig[4]<rig[5],i);
  for(const seated of [false,true])for(const direction of ['FL','FR','BL','BR'])for(const action of actions){
    const a={id:'patron-'+i,role:seated?'seated':'guest',action:'idle',visible:true,festivalAction:action,festivalRemaining:8};
    const sig=[];
    for(const t of [.3,.7,1.1]){
      const p=pixelAudiencePose(a,state,t);
      const m=patronMotion(a,p,action,{active:true,seated});
      assert(Object.values(m).every(x=>typeof x==='boolean'||Number.isFinite(x)));
      const calls=[];
      const ctx={save(){},restore(){},scale(...v){calls.push(v);},translate(...v){calls.push(v);},fillRect(...v){calls.push(v);},drawImage(sheet,x,y,w,h,...v){assert(x>=0&&y>=0&&x+w<=640&&y+h<=640);calls.push([x,y,w,h,...v]);}};
      drawPatronModel(ctx,{},rig,i,direction,m);
      assert(calls.some(x=>x.length===8),[i,action,direction]);
      sig.push(calls);
    }
    if(i===0&&!seated&&direction==='FR')traces.set(action,JSON.stringify(sig));
  }
}
assert.equal(new Set(Festival.actions.map(a=>traces.get(a.id))).size,Festival.actions.length);
for(let owner=0;owner<3;owner++)for(let seed=1;seed<=100;seed++){
  const full=Audience.build({owner,attendance:Audience.capacity(owner),crowdSeed:seed,showId:'double'});
  assignPatronModels(full,owner,seed);
  assert.equal(new Set(full.map(a=>a.appearanceId)).size,patronPools[owner].length);
  assert(Math.max(...patronPools[owner].map(id=>full.filter(a=>a.appearanceId===id).length))<=3);
  const crowd=Audience.build({owner,attendance:24,crowdSeed:seed,showId:'freewill'});
  assignPatronModels(crowd,owner,seed);
  assert.equal(new Set(crowd.map(a=>a.appearanceId)).size,24);
  const visitor={id:'assisted-guest',role:'assisted',assistedSourceId:crowd[0].id};
  crowd.push(visitor);
  assignPatronModels(crowd,owner,seed);
  assert.equal(visitor.appearanceId,crowd[0].appearanceId);
  assert.equal(audienceIdentity({id:'bartender',role:'bartender'},owner,seed),null);
}
for(let owner=0;owner<3;owner++){
  const mem=new Map(),storage={getItem:k=>mem.get(k)??null,setItem:(k,v)=>mem.set(k,v)};
  const g=new SimBarGame({storage,seed:71});g.act('owner',{index:owner});g.act('start');
  launchPreparedDesk(g);
  while(g.phase==='guide')g.act('guide');
  assert.equal(g.phase,'playing');
  const models=r=>r.actors.filter(Audience.isAudience).map(a=>[a.id,a.appearanceId]);
  const original=models(g);
  assert(original.every(([,id])=>patronPools[owner].includes(id)));
  assert(g.act('festival',{id:'horns'}).success);
  g.journey.festival.arrivalCredit=1;g._festivalTick(.05);
  const walkIn=g.actors.find(a=>a.audienceArrival);
  if(owner===2)assert(walkIn);
  if(walkIn)assert(patronPools[owner].includes(walkIn.appearanceId));
  g.save();
  const restored=new SimBarGame({storage});assert(restored.act('continue').success);
  assert.deepEqual(models(restored),models(g));
  assert.equal(restored.journey.festival.activeId,'horns');
}
console.log('100 distinct patron models, 12 festival actions, three venue pools, save and walk-in checks passed.');
