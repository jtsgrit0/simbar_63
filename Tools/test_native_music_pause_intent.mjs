import assert from 'node:assert/strict';
import {SimBarGame} from './WebGLTemplate/FastGame/core.js';
import {launchPreparedDesk} from './native_test_workflow.mjs';

const memory=()=>{const values=new Map();return{getItem:key=>values.get(key)??null,setItem:(key,value)=>values.set(key,String(value))};};
const copy=value=>structuredClone(value);
const advance=(game,seconds)=>{for(let t=0;t<seconds;t+=.05)game.tick(Math.min(.05,seconds-t));};
const meters=game=>({satisfaction:game.state.satisfaction,performance:game.state.performance});
function night(owner=1){
  const game=new SimBarGame({storage:memory(),seed:71});
  game.act('owner',{index:owner});game.act('start');launchPreparedDesk(game);
  while(game.phase==='guide')game.act('guide');
  assert.equal(game.phase,'playing');return game;
}
function load(raw){
  const game=new SimBarGame({storage:memory(),seed:71});
  assert.equal(game.importSave(raw),true);assert.equal(game.phase,'lobby');return game;
}
function withJourney(raw,change){
  const data=JSON.parse(raw),journey=JSON.parse(data.journeyJson);change(journey);data.journeyJson=JSON.stringify(journey);return JSON.stringify(data);
}
function business(game){
  const state=copy(game.state);delete state.journeyJson;
  return{state,journey:copy(game.journey),actors:copy(game.actors),orders:copy(game.orders),elapsed:game.elapsed};
}

const evidence=[];
for(const owner of [0,1,2]){
  const game=night(owner);
  assert.equal(game.journey.musicPauseIntent,false);assert.deepEqual(meters(game),{satisfaction:0,performance:0});
  game._serve(game.orders[0].item,false,1);
  assert.equal(game.act('festival',{id:'horns'}).success,true);advance(game,5);
  assert(game.state.performance>0);game.act('pause');
  const oldFailure=withJourney(game.exportSave(),journey=>{journey.musicPaused=true;delete journey.musicPauseIntent;});
  const recovered=load(oldFailure),before=business(recovered);
  assert.equal(recovered.journey.musicPaused,true,'Import must not change a saved playback state before Continue');
  assert.equal(recovered.journey.musicPauseIntent,false);
  assert.equal(recovered.act('continue').success,true);
  assert.equal(recovered.phase,'playing');assert.equal(recovered.journey.musicPaused,false);
  const expected=copy(before);expected.journey.musicPaused=false;
  // Continue may resume presentation, but it must not grant points, advance
  // the show, change money, alter the queue or rewrite witnessed experience.
  assert.deepEqual(business(recovered),expected);
  evidence.push({owner,oldFailureRecovered:true,earnedMetersPreserved:meters(recovered)});
}

// Only a literal boolean marks an intentional pause. Malformed or old saves
// must not turn a transient playback failure into a permanent player choice.
const clean=night().exportSave();
for(const intent of [undefined,false,1,'true',{},null]){
  const game=load(withJourney(clean,journey=>{journey.musicPaused=true;if(intent===undefined)delete journey.musicPauseIntent;else journey.musicPauseIntent=intent;}));
  assert.equal(game.journey.musicPauseIntent,false);game.act('continue');assert.equal(game.journey.musicPaused,false);
}
const manual=night();
assert.equal(manual.act('musicState',{paused:true}).success,true);
assert.equal(manual.journey.musicPauseIntent,true);
assert.equal(manual.act('musicState',{paused:'false'}).success,false);
assert.equal(manual.journey.musicPauseIntent,true,'Invalid actions must not erase the pause choice');
const resumed=load(manual.exportSave());resumed.act('continue');
resumed.setMusicPlaybackActive(true);
assert.equal(resumed.journey.musicPaused,true);assert.equal(resumed.journey.musicPauseIntent,true);
assert.equal(resumed.act('festival',{id:'horns'}).success,true);advance(resumed,2);
resumed._serve(resumed.orders[0].item,false,1);
assert(resumed.state.satisfaction>0);assert.equal(resumed.state.performance,0,'Explicitly paused music must retain the existing feedback rule');
assert.equal(resumed.act('musicState',{paused:false}).success,true);
assert.equal(resumed.journey.musicPauseIntent,false);advance(resumed,3);
assert(resumed.state.performance>0,'Explicit play must allow actual sustained participation to earn feedback again');

// A pre-feedback save still starts with no evidence. Recovery of the audio
// flag cannot manufacture either meter; subsequent real participation can.
const legacy=load(withJourney(clean,journey=>{journey.musicPaused=true;delete journey.musicPauseIntent;delete journey.feedback;}));
legacy.act('continue');assert.deepEqual(meters(legacy),{satisfaction:0,performance:0});
advance(legacy,1);assert.deepEqual(meters(legacy),{satisfaction:0,performance:0});
legacy.act('festival',{id:'horns'});assert.deepEqual(meters(legacy),{satisfaction:0,performance:0});
advance(legacy,3);assert(legacy.state.performance>0);

// In the browser, an attempted/recovered play is not proof that music is
// audible. A transient media signal gates experience without changing saves.
const physical=load(withJourney(clean,journey=>{journey.musicPaused=true;delete journey.musicPauseIntent;}));
const beforeSignal=business(physical);
assert.equal(physical.setMusicPlaybackActive(false),true);
assert.deepEqual(business(physical),beforeSignal,'A media status signal must not grant or erase saved experience');
for(const invalid of [null,undefined,0,1,'true',{}])assert.equal(physical.setMusicPlaybackActive(invalid),false);
physical.act('continue');assert.equal(physical.journey.musicPaused,false);
physical._serve(physical.orders[0].item,false,1);
physical.act('festival',{id:'horns'});advance(physical,2);
assert(physical.state.satisfaction>0);assert.equal(physical.state.performance,0,'A recovered pause flag must not reward blocked, pending or waiting audio');
assert(Object.values(physical.journey.feedback.guests).every(guest=>guest.enjoyedSeconds===0));
const beforePlaying=business(physical);
physical.setMusicPlaybackActive(true);
assert.deepEqual(business(physical),beforePlaying,'Playback success alone must not instantly award points');
advance(physical,2);assert(physical.state.performance>0,'Actual playback plus witnessed action duration must earn concert experience');
const earned=meters(physical),earnedSeconds=Object.fromEntries(Object.entries(physical.journey.feedback.guests).map(([id,guest])=>[id,guest.enjoyedSeconds]));
physical.setMusicPlaybackActive(false);assert.deepEqual(meters(physical),earned);advance(physical,.5);
assert.deepEqual(Object.fromEntries(Object.entries(physical.journey.feedback.guests).map(([id,guest])=>[id,guest.enjoyedSeconds])),earnedSeconds,'Playback interruption must stop further concert experience');
const serialized=physical.exportSave();
assert(!serialized.includes('_musicPlaybackActive'));assert(!serialized.includes('musicPlaybackActive'));
const unbound=load(serialized);assert.equal(unbound._musicPlaybackActive,null,'Physical media state must not survive serialization');
evidence.push({physicalPlaybackGate:true,beforePlayingPerformance:0,earnedMeters:earned});

// The show's automatic ending pause is separate from explicit user intent.
// Neither a closing save nor settled results may be restarted by Continue.
const closing=physical;closing.journey.secondsLeft=.05;closing.tick(.05);
assert.equal(closing.phase,'closing');assert.equal(closing.journey.musicPaused,true);assert.equal(closing.journey.musicPauseIntent,false);
const restoredClosing=load(withJourney(closing.exportSave(),journey=>{delete journey.musicPauseIntent;}));
restoredClosing.act('continue');assert.equal(restoredClosing.phase,'closing');assert.equal(restoredClosing.journey.musicPaused,true);
const closingMeters=meters(restoredClosing),closingLedger=copy(restoredClosing.journey.feedback);
assert(closingMeters.performance>0);
for(let t=0;t<420&&restoredClosing.phase!=='results';t+=.1){
  restoredClosing.setMusicPlaybackActive(Math.floor(t)%2===0);
  restoredClosing.tick(.1);
  if(restoredClosing.reaction?.activeKind)restoredClosing.act('quick',{kind:restoredClosing.reaction.activeKind});
  if(restoredClosing.farewellPending)restoredClosing.act('farewellAck');
}
assert.equal(restoredClosing.phase,'results');
assert.deepEqual(meters(restoredClosing),closingMeters);assert.deepEqual(restoredClosing.journey.feedback,closingLedger);
const results=load(withJourney(restoredClosing.exportSave(),journey=>{journey.musicPaused=true;delete journey.musicPauseIntent;}));
const resultMeters=meters(results);results.act('continue');
assert.equal(results.phase,'results');assert.equal(results.journey.musicPaused,true);assert.deepEqual(meters(results),resultMeters);

results.act('musicState',{paused:true});assert.equal(results.journey.musicPauseIntent,true);
results.act('next');launchPreparedDesk(results);while(results.phase==='guide')results.act('guide');
assert.equal(results.phase,'playing');assert.equal(results.journey.musicPaused,false);assert.equal(results.journey.musicPauseIntent,false);
assert.deepEqual(meters(results),{satisfaction:0,performance:0});
assert(Object.values(results.journey.feedback.guests).every(guest=>guest.orders===0&&guest.enjoyedSeconds===0));

console.log(JSON.stringify(evidence,null,2));
console.log('PASS: old browser pause recovery, explicit pause intent, physical playback gating, transient signal validation, exact ledger preservation, safe legacy feedback, unchanged closing/results and fresh-night reset.');
