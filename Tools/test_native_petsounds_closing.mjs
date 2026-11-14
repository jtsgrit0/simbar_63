import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {SimBarGame} from './WebGLTemplate/FastGame/core.js';
import {EXIT_POINT} from './WebGLTemplate/FastGame/aftershow.js';
import {Audience} from './WebGLTemplate/FastGame/audience.js';

const data=new Map(),storage={getItem:key=>data.get(key)??null,setItem:(key,value)=>data.set(key,String(value))};
const g=new SimBarGame({storage});
assert(g.importSave(readFileSync(new URL('./fixtures/aftershow-petsounds-day5-gather-blocked.json',import.meta.url),'utf8')));
g.act('continue');
assert.equal(g.owner,2);assert.equal(g.journey.day,5);assert.equal(g.phase,'closing');
assert.equal(g.floorTasksPending,false);assert.equal(g.afterShowPending,true);
const after=g.journey.afterShow,fan=g.actors.find(a=>a.id==='patron-9'),blocker=g.actors.find(a=>a.id==='patron-0');
const item=after.audience.find(a=>a.id===fan.id),singer=after.members.find(m=>m.id==='singer');
assert.equal(item.phase,'gather');assert.equal(singer.greeting,1);assert.equal(after.departureStarted,false);
assert(Math.hypot(fan.x-item.targetX,fan.z-item.targetZ)>.30);
assert(Math.hypot(blocker.x-item.targetX,blocker.z-item.targetZ)<.01);
const held={x:fan.x,z:fan.z};g.tick(.05);
assert.equal(item.phase,'wait');assert.deepEqual({x:fan.x,z:fan.z},held,'Settling at a reachable mark must not teleport the guest');

let hug=false,acknowledgements=0,seconds=0;
for(;seconds<150&&g.phase!=='results';seconds+=.05){
  const before=new Map(g.actors.map(a=>[a.id,{x:a.x,z:a.z,visible:a.visible}]));
  g.tick(.05);
  if(singer.phase==='hug'){
    hug=true;const artist=g.actors.find(a=>a.id==='singer');
    assert(Math.abs(Math.hypot(artist.x-fan.x,artist.z-fan.z)-.43)<.041,'The recovered greeting must still reach the actual guest');
  }
  for(const a of g.actors){
    const old=before.get(a.id);if(!old)continue;
    assert(Math.hypot(a.x-old.x,a.z-old.z)<=.19,'Recovery teleported '+a.id);
    if(old.visible&&a.visible===false&&(Audience.isAudience(a)||a.role==='band'))
      assert(Math.hypot(a.x-EXIT_POINT.x,a.z-EXIT_POINT.z)<=.181,'Recovery hid '+a.id+' away from the doorway');
  }
  if(g.farewellPending){acknowledgements++;g.act('farewellAck');}
}
assert(hug,'The pending second greeting was skipped');assert.equal(acknowledgements,1);
assert.equal(g.phase,'results');assert(after.members.every(m=>m.greeting===2&&m.phase==='gone'));
assert(after.audience.every(a=>a.phase==='gone'));assert.equal(after.noticeAcknowledged,true);
const receipt=[g.state.money,g.journey.xp,g.journey.completedNights];g.save();
const restored=new SimBarGame({storage});restored.act('continue');restored.tick(1);
assert.equal(restored.phase,'results');assert.deepEqual([restored.state.money,restored.journey.xp,restored.journey.completedNights],receipt,'Recovery duplicated the receipt');
restored.act('next');assert.equal(restored.phase,'planning');assert.equal(restored.journey.day,6);
console.log(JSON.stringify({passed:true,owner:2,day:5,recoveredClosingSeconds:Math.round(seconds*100)/100,acknowledgements,nextDay:restored.journey.day,checks:['occupied gather mark','physical greeting','continuous departures','doorway-only exits','saved recovery','once-only receipt','next day']}));
