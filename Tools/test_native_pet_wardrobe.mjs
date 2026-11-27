import assert from 'node:assert/strict';
import fs from 'node:fs';
import {Audience} from './WebGLTemplate/FastGame/audience.js';
import {SimBarGame} from './WebGLTemplate/FastGame/core.js';
import {launchPreparedDesk} from './native_test_workflow.mjs';
import {pixelStyle} from './WebGLTemplate/FastGame/world.js';

const meta=JSON.parse(fs.readFileSync(new URL('./WebGLTemplate/FastGame/art/sprites.json',import.meta.url)));
assert.equal(pixelStyle({id:'bartender'},meta.roles.bartender,meta),'Suit');
assert.equal(pixelStyle({id:'server'},meta.roles.server,meta),'Suit');
const employee=new Set(['Suit','Worker','Adventurer']);
function check(actors){
  for(const a of actors.filter(Audience.isAudience))assert(!employee.has(a.styleKey),a.id);
  const floor=actors.filter(a=>a.role==='guest'),counts=new Map();
  for(const a of floor)counts.set(a.styleKey,(counts.get(a.styleKey)||0)+1);
  if(floor.length>=16){
    assert.equal(counts.size,5);
    assert(Math.max(...counts.values())<=Math.ceil(floor.length/5)+3);
  }
  const seats=actors.filter(a=>a.role==='seated'&&a.id.startsWith('bar-seat-')).sort((a,b)=>a.id.localeCompare(b.id));
  for(let i=1;i<seats.length;i++)assert.notEqual(seats[i-1].styleKey,seats[i].styleKey);
  const lounge=actors.filter(a=>a.role==='seated'&&a.id.startsWith('lounge-'));
  assert.equal(new Set(lounge.map(a=>a.styleKey)).size,lounge.length);
}
for(let seed=0;seed<250;seed++)for(const attendance of [4,8,12,20,32,44]){
  const actors=Audience.build({owner:2,attendance,crowdSeed:seed,showId:'freewill'});
  check(actors);
  const arrived=Audience.arrival({owner:2,crowdSeed:seed,actors,serial:seed+1});
  if(arrived)assert(!employee.has(arrived.styleKey));
}
const store=new Map(),storage={getItem:k=>store.get(k)??null,setItem:(k,v)=>store.set(k,v)};
const g=new SimBarGame({storage});
g.act('owner',{index:2});g.act('start');launchPreparedDesk(g);
while(g.phase==='guide')g.act('guide');
assert.equal(g.phase,'playing');
const restore=()=>{const r=new SimBarGame({storage});assert(r.act('continue').success);return r;};
const identity=r=>r.actors.filter(Audience.isAudience).map(a=>[a.id,a.name,a.x,a.z]);
const styles=r=>r.actors.filter(Audience.isAudience).map(a=>a.styleKey);
check(g.actors);
const current=styles(g),original=identity(g),orders=g.orders.map(o=>[o.id,o.item]);
g.save();
assert.deepEqual(styles(restore()),current);
for(const a of g.actors.filter(Audience.isAudience)){a.styleKey='Suit';delete a.petWardrobeVersion;}
g.save();
const migrated=restore();
check(migrated.actors);
assert.deepEqual(identity(migrated),original);
assert.deepEqual(migrated.orders.map(o=>[o.id,o.item]),orders);
migrated.save();
assert.deepEqual(styles(restore()),styles(migrated));
console.log('1500 Pet Sounds casts, late arrivals, and live-save wardrobe upgrade passed.');
