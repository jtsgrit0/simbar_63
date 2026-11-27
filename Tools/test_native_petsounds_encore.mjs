import assert from 'node:assert/strict';
import {SimBarGame,Booking} from './WebGLTemplate/FastGame/core.js';
import {Encore} from './WebGLTemplate/FastGame/desk-encore.js';
const memory=()=>{const data=new Map();return{getItem:k=>data.get(k)??null,setItem:(k,v)=>data.set(k,String(v))}};
function newGame(owner=2,sound=1,day=4){
  const g=new SimBarGame({storage:memory(),seed:81});g.act('owner',{index:owner});g.journey.day=day;g.journey.completedNights=day-1;g.state.upgrades[1]=sound;g.act('start');return g;
}
for (const showId of ['freewill','july','double']){
  for (const choice of [0,1]){
    let g=newGame();const initial=g.journey.booking.encoreSeed;
    assert.ok(initial);assert.equal(g.metrics().petSoundsEncore,true);assert.equal(g.act('launch').success,false);
    assert.equal(g.act('deskEncoreChoice',{choice}).success,false);
    assert.equal(g.act('selectShow',{showId}).success,true);
    const scene=Encore.scene(g.journey.booking);assert.ok(scene);assert.equal(g.act('launch').success,false);
    assert.equal(g.act('deskEncoreChoice',{choice:2}).success,false);
    assert.equal(g.act('deskEncoreChoice',{choice}).success,true);
    g=new SimBarGame({storage:g.storage});g.act('continue');
    assert.equal(g.journey.booking.encoreSeed,initial);assert.equal(Encore.scene(g.journey.booking),scene);
    assert.equal(g.journey.booking.encoreChoice,choice);assert.equal(g.metrics().canLaunch,true);
    const effect=Encore.effect(g.journey.booking);
    assert.equal(g.act('launch').success,true);
    if(showId!=='double')assert.equal(g.orders[0]?.item,effect.item);
    else assert.equal(effect.speaker,choice?'owner':'freewill');
  }
}
const g=newGame(2,0);
assert.equal(g.act('selectShow',{showId:'double'}).success,false);
assert.equal(g.act('selectShow',{showId:'july'}).success,true);
assert.equal(g.act('deskEncoreChoice',{choice:0}).success,true);
const seed=g.journey.booking.encoreSeed;
g.act('selectShow',{showId:'freewill'});
assert.equal(g.journey.booking.encoreChoice,undefined);
assert.equal(g.journey.booking.encoreSeed,seed);
g.act('selectShow',{showId:'july'});
for(const key of ['freewill','july']){
  const a=Encore.scene({day:4,showId:key,encoreSeed:4});
  const b=Encore.scene({day:4,showId:key,encoreSeed:7});
  assert.notEqual(a.title[0],b.title[0]);
}
const malformed={...g.journey.booking,encoreSeed:-1,encoreChoice:1};
Booking.sanitize(malformed,Booking.newCareer());
assert.equal(malformed.encoreSeed,undefined);
assert.equal(malformed.encoreChoice,undefined);
assert.equal(newGame(0).metrics().petSoundsEncore,false);
assert.equal(newGame(2,1,5).metrics().petSoundsEncore,false);
console.log('Encore cases, saved selection, first orders, fallback and scope passed');
