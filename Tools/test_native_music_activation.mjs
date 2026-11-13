import assert from 'node:assert/strict';
import {SimBarGame,catalog} from './WebGLTemplate/FastGame/core.js';
import {MusicPlayer} from './WebGLTemplate/FastGame/music.js';
import {launchPreparedDesk} from './native_test_workflow.mjs';

const flush=async()=>{await Promise.resolve();await Promise.resolve();};
const memory=()=>{const values=new Map();return{getItem:k=>values.get(k)??null,setItem:(k,v)=>values.set(k,String(v))};};
function night(){const game=new SimBarGame({storage:memory(),seed:71});game.act('owner',{index:1});game.act('start');launchPreparedDesk(game);while(game.phase==='guide')game.act('guide');return game;}
function harness(game=night()){
  const audios=[],pauseChanges=[];
  class Audio{
    constructor(){this.plays=[];this.listeners=new Map();this.paused=true;this.ended=false;this.readyState=4;this.loadCount=0;audios.push(this);}
    play(){const request={};this.plays.push(request);return new Promise((resolve,reject)=>Object.assign(request,{resolve:()=>{this.paused=false;this.readyState=4;resolve();},reject}));}
    pause(){this.paused=true;this.listeners.get('pause')?.();}
    load(){this.loadCount++;this.emit('emptied');}
    removeAttribute(name){delete this[name];}
    addEventListener(name,callback){this.listeners.set(name,callback);}
    emit(name){if(name==='playing'){this.paused=false;this.readyState=4;}if(name==='waiting')this.readyState=2;if(name==='pause')this.paused=true;if(name==='ended')this.ended=true;this.listeners.get(name)?.();}
  }
  const music=new MusicPlayer({getIndex:()=>game.state.playlist,getPaused:()=>game.journey.musicPaused,tracks:catalog.playlists,AudioClass:Audio,onPauseChange:paused=>pauseChanges.push(paused)});
  return{game,music,audios,pauseChanges};
}
function advance(game,music,seconds){for(let left=seconds;left>1e-8;left-=.05){game.setMusicPlaybackActive(music.playing);game.tick(Math.min(.05,left));}}

// A rejected browser play changes no saved user choice. Frames cannot retry it;
// the next user gesture calls play synchronously and a successful play recovers.
const h=harness(),{game,music}=h;assert.equal(music.playing,false);const before=game.exportSave();
music.wake();const audio=h.audios[0];assert.equal(audio.plays.length,1);assert.equal(music.playing,false);
audio.plays[0].reject(Object.assign(new Error('Activation required'),{name:'NotAllowedError'}));await flush();
assert.equal(music.blocked,true);assert.equal(music.paused,false);assert.equal(music.playing,false);assert.equal(game.exportSave(),before);assert.deepEqual(h.pauseChanges,[]);
for(let frame=0;frame<100;frame++){music.sync();music.resumeContext();}assert.equal(audio.plays.length,1,'Animation frames retried blocked playback');
assert.equal(game.act('festival',{id:'horns'}).success,true);advance(game,music,2);assert.equal(game.state.performance,0,'Silent audience actions earned concert response');
music.wake();assert.equal(audio.plays.length,2,'The next gesture did not retry synchronously');music.wake();assert.equal(audio.plays.length,2,'Pending retry was duplicated');assert.equal(music.playing,false);
audio.plays[1].resolve();await flush();assert.equal(music.playing,true);assert.equal(music.blocked,false);assert.equal(music.failed,false);
advance(game,music,2);assert(game.state.performance>0,'Heard audience participation did not recover');

// Buffering/termination events stop physical credit until a real playing event.
for(const event of ['waiting','pause','emptied']){
  audio.emit(event);assert.equal(music.playing,false,event+' left playback active');
  const prior=game.state.performance;advance(game,music,.5);assert.equal(game.state.performance,prior,'Silent '+event+' interval earned response');
  audio.emit('canplay');assert.equal(music.playing,false,'canplay is not proof of playback');
  audio.emit('playing');assert.equal(music.playing,true);
}
// Network fetching can stall while already-buffered audio remains audible.
// Only an unbuffered stall stops credit; a fetch stall alone is not silence.
audio.readyState=4;audio.emit('stalled');assert.equal(music.playing,true,'Buffered audio was silenced by a fetch stall');
audio.readyState=2;audio.emit('stalled');assert.equal(music.playing,false,'An unbuffered stall kept playback active');audio.emit('playing');assert.equal(music.playing,true);
audio.emit('error');assert.equal(music.playing,false);assert.equal(music.failed,true);audio.emit('playing');assert.equal(music.playing,true);assert.equal(music.failed,false);
audio.emit('ended');assert.equal(music.playing,false);audio.ended=false;audio.emit('playing');assert.equal(music.playing,true);
audio.paused=true;assert.equal(music.playing,false);audio.paused=false;assert.equal(music.playing,true);

// A later buffering event wins over an earlier promise finishing late.
const buffering=harness();buffering.music.wake();const bufferingAudio=buffering.audios[0];bufferingAudio.emit('waiting');bufferingAudio.plays[0].resolve();await flush();assert.equal(buffering.music.playing,false);bufferingAudio.emit('playing');assert.equal(buffering.music.playing,true);

// A transient stream error retries the same source only on another gesture.
// Its error indicator remains until playback is actually confirmed again.
const network=harness();network.music.wake();const networkAudio=network.audios[0];networkAudio.plays[0].resolve();await flush();const originalURL=networkAudio.src;networkAudio.emit('error');assert.equal(network.music.failed,true);assert.equal(network.music.playing,false);
for(let frame=0;frame<100;frame++)network.music.sync();assert.equal(networkAudio.loadCount,0);assert.equal(networkAudio.plays.length,1);
network.music.wake();assert.equal(networkAudio.src,originalURL);assert.equal(networkAudio.loadCount,1);assert.equal(networkAudio.plays.length,2);assert.equal(network.music.failed,true);assert.equal(network.music.playing,false);network.music.wake();assert.equal(networkAudio.loadCount,1);
networkAudio.plays[1].reject(new Error('Temporary stream failure'));await flush();assert.equal(network.game.journey.musicPaused,false);network.music.wake();assert.equal(networkAudio.loadCount,2);networkAudio.plays[2].resolve();await flush();assert.equal(network.music.failed,false);assert.equal(network.music.blocked,false);assert.equal(network.music.playing,true);

// Explicit pause and context suspension prevent late promises from reviving
// physical playback, and generic gestures keep the player's pause choice.
game.act('musicState',{paused:true});music.sync();assert.equal(music.playing,false);const plays=audio.plays.length;music.wake();assert.equal(audio.plays.length,plays);audio.emit('playing');assert.equal(music.playing,false);
game.act('musicState',{paused:false});music.sync();const pending=audio.plays.at(-1);music.suspend();pending.resolve();await flush();assert.equal(music.playing,false);music.wake();assert.equal(audio.plays.at(-1),pending);music.resumeContext();const resumed=audio.plays.at(-1);assert.notEqual(resumed,pending);resumed.resolve();await flush();assert.equal(music.playing,true);

// Old track events and promises cannot affect its replacement or a destroyed
// player. A synchronous exception follows the same blocked/no-intent path.
music.play();const oldRequest=audio.plays.at(-1);game.act('playlist',{index:3});music.select();const replacement=h.audios[1];replacement.plays[0].resolve();await flush();assert.equal(music.playing,true);oldRequest.reject(new Error('Old track'));audio.emit('error');audio.emit('playing');await flush();assert.equal(music.playing,true);assert.equal(music.failed,false);assert.equal(music.blocked,false);
music.play();const disposed=replacement.plays.at(-1);music.destroy();disposed.resolve();replacement.emit('playing');await flush();assert.equal(music.playing,false);
const synchronous=harness();synchronous.music.wake();synchronous.audios[0].play=()=>{throw new Error('Synchronous play failure');};synchronous.music.play();assert.equal(synchronous.music.blocked,true);assert.equal(synchronous.music.playing,false);assert.equal(synchronous.game.journey.musicPaused,false);

// Older play() implementations returning nothing need a playing event before
// they can confirm heard playback; absence of a rejected promise is not proof.
const legacy=harness();legacy.music.wake();legacy.audios[0].play=()=>undefined;legacy.music.play();assert.equal(legacy.music.playing,false);legacy.audios[0].emit('playing');assert.equal(legacy.music.playing,true);
for(const item of [buffering,network,synchronous,legacy])item.music.destroy();
console.log('PASS: actual playback gate, silent interval0response, synchronous gesture retry without saved pause, no frame retry, buffering and stale event isolation');
