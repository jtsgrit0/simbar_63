import assert from 'node:assert/strict';
import {QualifiedPlayTracker} from './WebGLTemplate/FastGame/play-tracker.js';
import {MusicPlayer} from './WebGLTemplate/FastGame/music.js';

function pure(){
  let now=0,next=0;const starts=[],qualified=[],audio={currentTime:0,paused:false,ended:false,seeking:false,readyState:4};
  const tracker=new QualifiedPlayTracker({trackId:'track_001',clock:()=>now,idFactory:()=>`cycle-${++next}`,onplaystart:event=>starts.push(event),onqualified:event=>qualified.push(event)});
  const advance=(media,elapsed=media)=>{now+=elapsed;audio.currentTime+=media;tracker.progress(audio);};
  return{tracker,audio,starts,qualified,advance,wait:seconds=>{now+=seconds;}};
}

// Wall time alone never qualifies, and exactly thirty seconds qualifies once.
{
  const h=pure();h.wait(60);h.tracker.progress(h.audio);assert.equal(h.starts.length,0);
  h.tracker.playing(h.audio);h.advance(0,60);assert.equal(h.qualified.length,0);
  h.advance(29.999);assert.equal(h.qualified.length,0);h.advance(.001);
  assert.deepEqual(h.qualified,[{playId:'cycle-1',trackId:'track_001',seconds:30}]);
  h.advance(60);h.tracker.playing(h.audio);h.advance(30);assert.equal(h.starts.length,1);assert.equal(h.qualified.length,1);
}

// Pause, buffering and context suspension preserve a cycle but earn no time.
{
  const h=pure();h.tracker.playing(h.audio);h.advance(10);
  h.audio.paused=true;h.tracker.stop(h.audio);h.wait(120);h.tracker.progress(h.audio);
  h.audio.paused=false;h.tracker.playing(h.audio);h.advance(8);
  h.audio.readyState=2;h.tracker.stop(h.audio);h.wait(80);h.audio.readyState=4;h.tracker.playing(h.audio);
  h.advance(11);assert.equal(h.qualified.length,0);h.advance(1);assert.equal(h.qualified.length,1);assert.equal(h.starts.length,1);
}

// Both seek directions discard the position jump. Duplicate playing events
// sample the existing interval, rather than restarting or double-counting it.
{
  const h=pure();h.tracker.playing(h.audio);h.advance(9);
  h.audio.seeking=true;h.audio.currentTime=1000;h.tracker.seeking(h.audio);h.wait(10);h.tracker.progress(h.audio);
  h.audio.seeking=false;h.tracker.seeked(h.audio);h.advance(5);h.tracker.playing(h.audio);h.tracker.playing(h.audio);
  h.audio.seeking=true;h.audio.currentTime=0;h.tracker.seeking(h.audio);h.audio.seeking=false;h.tracker.seeked(h.audio);
  h.advance(15);assert.equal(h.qualified.length,0);h.advance(1);assert.equal(h.qualified.length,1);assert.equal(h.starts.length,1);
}

// A position leap cannot earn more than actual elapsed time; an EOF flush
// includes the last interval, while replay after EOF starts a new cycle.
{
  const h=pure();h.tracker.playing(h.audio);h.advance(1000,.1);assert.equal(h.qualified.length,0);assert.equal(h.tracker.seconds,.1);
  h.advance(29);h.wait(.9);h.audio.currentTime+=.9;h.audio.ended=true;h.audio.paused=true;h.tracker.ended(h.audio);
  assert.equal(h.qualified.length,1);
  h.audio.currentTime=0;h.audio.ended=false;h.audio.paused=false;h.tracker.playing(h.audio);h.advance(29);
  assert.equal(h.starts.length,2);assert.equal(h.qualified.length,1);h.advance(1);assert.equal(h.qualified.length,2);
  assert.notEqual(h.qualified[0].playId,h.qualified[1].playId);
}

// Invalid samples and listener failures must not interrupt playback tracking.
{
  const h=pure();h.audio.currentTime=NaN;h.tracker.playing(h.audio);h.wait(100);h.tracker.progress(h.audio);assert.equal(h.qualified.length,0);
  h.audio.currentTime=0;h.tracker.progress(h.audio);h.advance(30);assert.equal(h.qualified.length,1);
  let now=0;const tracker=new QualifiedPlayTracker({trackId:'track_001',clock:()=>now,onplaystart:()=>{throw Error('offline');},onqualified:()=>Promise.reject(Error('offline'))});
  const audio={currentTime:0,paused:false,readyState:4};tracker.playing(audio);now=30;audio.currentTime=30;assert.doesNotThrow(()=>tracker.progress(audio));
  await Promise.resolve();
}

const originalPerformance=globalThis.performance;let now=0;
Object.defineProperty(globalThis,'performance',{configurable:true,value:{now:()=>now*1000}});
const flush=async()=>{await Promise.resolve();await Promise.resolve();};
function player(){
  let index=0,paused=false;const audios=[],starts=[],qualified=[];
  class Audio{
    constructor(){this.listeners=new Map();this.paused=true;this.ended=false;this.readyState=4;this.currentTime=0;this.currentSrc='';this.seeking=false;this.error=null;audios.push(this);}
    addEventListener(name,callback){if(!this.listeners.has(name))this.listeners.set(name,[]);this.listeners.get(name).push(callback);}
    removeEventListener(name,callback){this.listeners.set(name,(this.listeners.get(name)||[]).filter(value=>value!==callback));}
    emit(name){for(const callback of this.listeners.get(name)||[])callback();}
    playing(){this.paused=false;this.ended=false;this.readyState=4;this.currentSrc=this.src;this.emit('playing');}
    play(){if(this.ended)this.currentTime=0;this.paused=false;this.ended=false;return Promise.resolve();}
    pause(){this.paused=true;this.emit('pause');}
    removeAttribute(name){delete this[name];}
    load(){this.currentTime=0;this.currentSrc='';this.readyState=0;this.ended=false;this.paused=true;this.emit('emptied');}
    finish(){this.paused=true;this.ended=true;this.emit('ended');}
  }
  const music=new MusicPlayer({getIndex:()=>index,getPaused:()=>paused,tracks:[{trackId:'track_001',audio_url:'https://audio.test/1.mp3'},{trackId:'track_002',audio_url:'https://audio.test/2.mp3'}],AudioClass:Audio,onplaystart:event=>starts.push(event),onqualified:event=>qualified.push(event),onended:()=>{index=(index+1)%2;music.select();}});
  const advance=(seconds,{event=true}={})=>{now+=seconds;music.audio.currentTime+=seconds;if(event)music.audio.emit('timeupdate');};
  music.wake();return{music,audios,starts,qualified,advance,select:value=>{index=value;music.select();},pause:value=>{paused=value;music.sync();}};
}

try{
  // play() resolution is not proof of listened duration. Actual native events
  // qualify the current source, while auto-next retains the unlocked element.
  {
    const h=player();await flush();assert.equal(h.starts.length,0);h.music.audio.playing();
    assert.match(h.starts[0].playId,/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i);
    h.advance(29);assert.equal(h.qualified.length,0);h.advance(1);assert.equal(h.qualified.length,1);
    const audio=h.music.audio,stale=[...audio.listeners.get('timeupdate'),...audio.listeners.get('playing'),...audio.listeners.get('ended')];
    audio.finish();assert.equal(h.music.audio,audio);assert.equal(h.audios.length,1);assert.equal(h.starts.length,1);
    for(const callback of stale)callback();assert.equal(h.qualified.length,1);assert.equal(h.starts.length,1);
    audio.playing();h.advance(30);assert.equal(h.starts.length,2);assert.equal(h.qualified.length,2);assert.equal(h.qualified[1].trackId,'track_002');
    assert.notEqual(h.starts[0].playId,h.starts[1].playId);h.music.destroy();for(const callback of stale)callback();assert.equal(h.qualified.length,2);
  }

  // Saved music pause, context suspension and buffering are one listening
  // cycle. A network stall with buffered audio continues actual listening.
  {
    const h=player();h.music.audio.playing();h.advance(10);h.pause(true);now+=100;h.music.audio.emit('timeupdate');h.pause(false);h.music.audio.playing();
    h.advance(5);h.music.suspend();now+=100;h.music.resumeContext();h.music.audio.playing();
    h.music.audio.readyState=2;h.music.audio.emit('waiting');now+=100;h.music.audio.playing();h.advance(5);
    h.music.audio.emit('stalled');h.advance(9);assert.equal(h.qualified.length,0);h.advance(1);assert.equal(h.qualified.length,1);assert.equal(h.starts.length,1);
    h.music.destroy();
  }

  // Explicitly leaving a partial song discards its partial cycle. Returning
  // creates a fresh ID. Selecting the already playing song is only a resume.
  {
    const h=player();h.music.audio.playing();h.advance(20);const old=h.music.audio,stale=[...old.listeners.get('timeupdate')];
    h.select(1);h.music.audio.playing();h.advance(4);h.select(0);h.music.audio.playing();
    old.currentTime=100;now+=100;for(const callback of stale)callback();assert.equal(h.qualified.length,0);
    h.advance(29);h.select(0);h.music.audio.playing();assert.equal(h.starts.length,3);h.advance(1);assert.equal(h.qualified.length,1);
    assert.equal(h.qualified[0].playId,h.starts[2].playId);assert.notEqual(h.starts[0].playId,h.starts[2].playId);h.music.destroy();
  }

  // EOF replay of a single retained source is a new cycle even though neither
  // the selected index nor the Audio identity changes. Short audio is uncounted.
  {
    const h=player();h.music.onended=()=>h.music.select();const audio=h.music.audio;
    audio.playing();h.advance(5);audio.finish();audio.playing();h.advance(29);assert.equal(h.qualified.length,0);h.advance(1);
    assert.equal(h.starts.length,2);assert.equal(h.qualified.length,1);assert.equal(h.audios.length,1);assert.notEqual(h.starts[0].playId,h.starts[1].playId);h.music.destroy();
  }

  // Seek jumps do not count, and delayed events from a different currentSrc
  // cannot qualify or start the current generation.
  {
    const h=player();const audio=h.music.audio;audio.playing();h.advance(10);
    audio.seeking=true;audio.currentTime=1000;audio.emit('seeking');now+=30;audio.seeking=false;audio.emit('seeked');h.advance(19);assert.equal(h.qualified.length,0);
    const source=audio.currentSrc;audio.currentSrc='https://audio.test/old.mp3';h.advance(100);audio.emit('playing');assert.equal(h.qualified.length,0);assert.equal(h.starts.length,1);
    audio.currentSrc=source;audio.currentTime=1019;audio.emit('seeking');audio.emit('seeked');h.advance(1);assert.equal(h.qualified.length,1);h.music.destroy();
  }
}finally{Object.defineProperty(globalThis,'performance',{configurable:true,value:originalPerformance});}

console.log('PASS: thirty actual seconds, pause/buffer/context continuity, seek exclusion and clock cap, UUID cycles, automatic shared-Audio next, EOF replay, stale events, short audio and callback failure isolation');
