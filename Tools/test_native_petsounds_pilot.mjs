import assert from 'node:assert/strict';
import {SimBarGame,SAVE_KEY} from './WebGLTemplate/FastGame/core.js';
const data=new Map();
const storage={getItem:k=>data.get(k)??null,setItem:(k,v)=>data.set(k,String(v))};
const create=(env=storage)=>new SimBarGame({storage:env,seed:47});
let g=create();
g.act('owner',{index:2});
g.act('start');
assert.equal(g.journey.booking.showId,'freewill');
assert.equal(g.metrics().petSoundsPilot,true);
assert.equal(g.metrics().canLaunch,false);
assert.equal(g.act('launch').success,false);
assert.equal(g.act('deskSoundcheck',{minutes:1000}).success,false);
assert.equal(g.journey.booking.deskDoneMask,0);
const cash=g.cash;
for(const minutes of [1050,1080]){
  assert.equal(g.act('deskSoundcheck',{minutes}).success,true);
  assert.equal(g.cash,cash);
  assert.equal(g.metrics().canLaunch,true);
  assert.equal(g.journey.booking.deskDoneMask,3);
  assert.equal(JSON.parse(JSON.parse(storage.getItem(SAVE_KEY)).journeyJson).booking.soundcheckMinutes,minutes);
}
g=create();
g.act('continue');
assert.equal(g.journey.booking.soundcheckMinutes,1080);
assert.equal(g.act('launch').success,true);
assert.equal(g.journey.booking.soundcheckMinutes,1080);
assert.deepEqual(new Set(g.metrics().allowedTracks),new Set([0,3,4]));
assert.equal(g.act('deskSoundcheck',{minutes:1050}).success,false);
for(const owner of [0,1]){
  g=create({getItem:()=>null,setItem(){}});
  g.act('owner',{index:owner});
  g.act('start');
  assert.equal(g.metrics().petSoundsPilot,false);
  assert.equal(g.journey.booking.showId,'');
  assert.equal(g.act('deskSoundcheck',{minutes:1050}).success,false);
}
g=create();
g.act('continue');
while(g.phase==='guide')g.act('guide');
for(let i=0;i<14000&&g.phase!=='results';i++){
  if(g.farewellPending)g.act('farewellAck');
  if(g.reaction?.activeKind)g.act('quick',{kind:g.reaction.activeKind});
  g.tick(.05);
}
assert.equal(g.phase,'results');
assert.equal(g.journey.completedNights,1);
assert.equal(g.journey.booking.showId,'freewill');
assert.equal(g.act('next').success,true);
assert.equal(g.journey.day,2);
assert.equal(g.metrics().petSoundsPilot,false);
assert.equal(g.metrics().petSoundsJulyPilot,true);
assert.equal(g.journey.booking.showId,'july');
assert.equal(g.act('deskSoundcheck',{minutes:1050}).success,false);
assert.equal(g.act('deskEntranceChoice',{choice:'wrong'}).success,false);
assert.equal(g.act('launch').success,false);
const balance=g.cash;
for(const choice of ['owner','band']){
  assert.equal(g.act('deskEntranceChoice',{choice}).success,true);
  assert.equal(g.metrics().canLaunch,true);
  assert.equal(g.journey.booking.entranceMusicChoice,choice);
  assert.equal(g.cash,balance);
}
g=create();g.act('continue');
assert.equal(g.phase,'planning');
assert.equal(g.journey.booking.entranceMusicChoice,'band');
assert.equal(g.metrics().canLaunch,true);
assert.equal(g.act('launch').success,true);
assert.equal(g.journey.booking.entranceMusicChoice,'band');
assert.deepEqual(g.metrics().allowedTracks,[1,2]);
assert.equal(g.act('deskEntranceChoice',{choice:'owner'}).success,false);
for(let i=0;i<14000&&g.phase!=='results';i++){
  if(g.farewellPending)g.act('farewellAck');
  if(g.reaction?.activeKind)g.act('quick',{kind:g.reaction.activeKind});
  g.tick(.05);
}
assert.equal(g.phase,'results');
assert.equal(g.journey.completedNights,2);
assert.equal(g.act('next').success,true);
assert.equal(g.journey.day,3);
assert.equal(g.metrics().petSoundsDoublePilot,true);
assert.equal(g.metrics().canLaunch,false);
assert.equal(g.act('deskSecondSetIntro',{speaker:'owner'}).success,false);
assert.equal(g.act('launch').success,false);
assert.equal(g.act('upgrade',{index:1}).success,true);
for(const speaker of ['owner','freewill']){
  assert.equal(g.act('deskSecondSetIntro',{speaker}).success,true);
  assert.equal(g.journey.booking.showId,'double');
  assert.equal(g.journey.booking.secondSetIntro,speaker);
  assert.equal(g.metrics().canLaunch,true);
}
g=create();g.act('continue');
assert.equal(g.journey.booking.secondSetIntro,'freewill');
assert.equal(g.act('launch').success,true);
assert.equal(g.state.playlist,0);
for(let i=0;i<2400&&!g.journey.campaignSecondSet;i++)g.tick(.05);
assert.equal(g.journey.campaignSecondSet,true);
assert.equal(g.state.playlist,1);
assert.equal(g.journey.booking.secondSetIntro,'freewill');
console.log('Pet Sounds first three pilot state checks passed');