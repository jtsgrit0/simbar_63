import {launchPreparedDesk} from './native_test_workflow.mjs';
import assert from 'node:assert/strict';
import {SimBarGame,catalog} from './WebGLTemplate/FastGame/core.js';
import {MusicPlayer} from './WebGLTemplate/FastGame/music.js';

function memory(){const map=new Map();return{getItem:k=>map.get(k)??null,setItem:(k,v)=>map.set(k,String(v))};}
function night(storage=memory()){
  const game=new SimBarGame({storage,seed:12});game.act('start');launchPreparedDesk(game);while(game.phase==='guide')game.act('guide');return game;
}
function audioHarness(game){
  const audios=[],pauseChanges=[];
  class FakeAudio{
    constructor(){this.plays=[];this.pauseCount=0;this.loadCount=0;this.listeners=new Map();audios.push(this);}
    play(){const request={};this.plays.push(request);return new Promise((resolve,reject)=>Object.assign(request,{resolve,reject}));}
    pause(){this.pauseCount++;}
    load(){this.loadCount++;}
    removeAttribute(name){delete this[name];}
    addEventListener(name,callback){this.listeners.set(name,callback);}
  }
  const music=new MusicPlayer({getIndex:()=>game.state.playlist,getPaused:()=>game.journey.musicPaused,
    tracks:catalog.playlists,baseURL:new URL('https://example.test/simbar/'),AudioClass:FakeAudio,
    onPauseChange:paused=>{pauseChanges.push(paused);game.act('musicState',{paused});}});
  return{music,audios,pauseChanges};
}
const flush=async()=>{await Promise.resolve();await Promise.resolve();};

// The live game clock ends the show once. Repeated state synchronization and
// Continue must preserve a user's subsequent replay rather than re-pausing it.
const game=night(),live=audioHarness(game);
live.music.sync();assert.equal(live.audios.length,0,'Pre-gesture sync fetched music');
live.music.wake();const track=live.audios[0];assert.equal(track.plays.length,1);
assert.equal(track.preload,'none');assert.equal(track.loop,true);
game.journey.secondsLeft=.05;game.tick(.05);assert.equal(game.phase,'closing');
assert.equal(game.journey.musicPauseSession,game.journey.session);assert.equal(game.journey.musicPaused,true);
live.music.sync();assert.equal(live.music.paused,true);assert.equal(track.pauseCount,1);
for(let i=0;i<5;i++){live.music.sync();game.act('continue');}
assert.equal(track.pauseCount,1,'The same show ending repeatedly paused its audio');
assert.equal(track.plays.length,1,'Closing synchronization restarted the music');
track.plays[0].reject(new Error('The original play was interrupted by show-end pause'));await flush();
assert.deepEqual(live.pauseChanges,[],'An obsolete pre-pause failure changed saved state');
game.act('musicState',{paused:false});live.music.sync();assert.equal(track.plays.length,2);assert.equal(live.music.paused,false);
game.act('continue');live.music.sync();assert.equal(track.plays.length,2);assert.equal(game.journey.musicPaused,false);
game.save();const replay=new SimBarGame({storage:game.storage});replay.act('continue');
assert.equal(replay.journey.musicPauseSession,replay.journey.session);assert.equal(replay.journey.musicPaused,false);
const resumed=audioHarness(replay);resumed.music.sync();assert.equal(resumed.audios.length,0);
resumed.music.wake();assert.equal(resumed.audios[0].plays.length,1,'Manual replay was not retained after save/reload');

// A persisted paused closing may be opened before any audio interaction. The
// first gesture creates the media element without starting it automatically.
game.act('musicState',{paused:true});const pausedSave=new SimBarGame({storage:game.storage});pausedSave.act('continue');
const paused=audioHarness(pausedSave);assert.equal(paused.music.paused,true);paused.music.sync();assert.equal(paused.audios.length,0);
paused.music.wake();assert.equal(paused.audios.length,1);assert.equal(paused.audios[0].plays.length,0);
pausedSave.act('musicState',{paused:false});paused.music.sync();assert.equal(paused.audios[0].plays.length,1);

// Leaving the gameplay context only suspends the element. It must neither
// persist a pause choice nor discard an existing user pause on returning.
const suspensionChanges=paused.pauseChanges.length,suspensionPaused=pausedSave.journey.musicPaused;
paused.music.suspend();paused.music.suspend();assert.equal(paused.audios[0].pauseCount,1);
assert.equal(pausedSave.journey.musicPaused,suspensionPaused);assert.equal(paused.pauseChanges.length,suspensionChanges);
paused.music.resumeContext();assert.equal(paused.audios[0].plays.length,2);assert.equal(pausedSave.journey.musicPaused,false);
paused.audios[0].plays[0].reject(new Error('Suspended play completed late'));await flush();
assert.equal(paused.music.paused,false);assert.equal(pausedSave.journey.musicPaused,false);assert.equal(paused.pauseChanges.length,suspensionChanges);
pausedSave.act('musicState',{paused:true});paused.music.sync();const playCount=paused.audios[0].plays.length;
paused.music.suspend();paused.music.resumeContext();assert.equal(paused.audios[0].plays.length,playCount,'Context return ignored the user pause');
assert.equal(pausedSave.journey.musicPaused,true);

// A current failure blocks physical playback without changing the saved pause
// choice. Older requests cannot undo a newer user gesture or track selection.
const raceGame=night(),race=audioHarness(raceGame);race.music.wake();const first=race.audios[0];
raceGame.act('musicState',{paused:true});race.music.sync();raceGame.act('musicState',{paused:false});race.music.sync();
first.plays[0].reject(new Error('Stale initial playback'));await flush();
assert.deepEqual(race.pauseChanges,[]);assert.equal(raceGame.journey.musicPaused,false);
first.plays[1].reject(new Error('Current playback failure'));await flush();
assert.deepEqual(race.pauseChanges,[]);assert.equal(raceGame.journey.musicPaused,false);assert.equal(race.music.paused,false);assert.equal(race.music.blocked,true);assert.equal(race.music.playing,false);
race.music.wake();const oldTrackPlay=first.plays.at(-1);assert.equal(first.plays.length,3);
assert.equal(raceGame.act('playlist',{index:3}).success,true);race.music.select();const second=race.audios[1];assert.equal(second.plays.length,1);
assert(first.pauseCount>=2);assert.equal(first.src,undefined);assert.equal(first.loadCount,1);
oldTrackPlay.reject(new Error('Old track failed after replacement'));await flush();
assert.deepEqual(race.pauseChanges,[]);assert.equal(race.music.paused,false);assert.equal(raceGame.journey.musicPaused,false);
race.music.destroy();second.plays[0].reject(new Error('Disposed track failed late'));await flush();
assert.deepEqual(race.pauseChanges,[]);assert.equal(race.music.audio,null);assert.equal(raceGame.journey.musicPaused,false);assert.equal(race.music.playing,false);

// Browser engines can throw synchronously as well as return a rejected play
// promise. Both stop physical playback without persisting a user pause.
const throwing=night(),syncFailure=audioHarness(throwing);syncFailure.music.wake();syncFailure.audios[0].play=()=>{throw Error('Synchronous browser playback failure');};
syncFailure.music.play();assert.deepEqual(syncFailure.pauseChanges,[]);assert.equal(throwing.journey.musicPaused,false);assert.equal(syncFailure.music.blocked,true);assert.equal(syncFailure.music.playing,false);

for(const h of [live,resumed,paused,syncFailure])h.music.destroy();
console.log('PASS: real end-of-show pauses once, persisted manual replay, paused first gesture, context suspension, blocked playback and stale rejection isolation');
