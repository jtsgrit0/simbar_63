import assert from 'node:assert/strict';
import {SimBarGame,catalog} from './WebGLTemplate/FastGame/core.js';
import {MusicPlayer} from './WebGLTemplate/FastGame/music.js';

const flush=async()=>{await Promise.resolve();await Promise.resolve();};
const memory=()=>{const values=new Map();return{getItem:k=>values.get(k)??null,setItem:(k,v)=>values.set(k,String(v))};};
function harness(showId='freewill'){
  const game=new SimBarGame({storage:memory(),seed:71});game.journey.completedNights=2;game.journey.day=3;game.state.upgrades[1]=1;
  game.act('start');assert(game.act('selectShow',{showId}).success);game.act('deskWork',{task:0});game.act('deskWork',{task:1});game.act('launch');while(game.phase==='guide')game.act('guide');
  const audios=[],moves=[];
  class Audio{
    constructor(){this.listeners=new Map();this.plays=[];this.paused=true;this.ended=false;this.readyState=4;this.currentSrc='';this.error=null;audios.push(this);}
    addEventListener(name,callback){if(!this.listeners.has(name))this.listeners.set(name,[]);this.listeners.get(name).push(callback);}
    removeEventListener(name,callback){this.listeners.set(name,(this.listeners.get(name)||[]).filter(value=>value!==callback));}
    dispatch(name){for(const callback of this.listeners.get(name)||[])callback();}
    emit(name){if(name==='ended'){this.ended=true;this.paused=true;}if(name==='playing'){this.ended=false;this.paused=false;this.readyState=4;this.currentSrc=this.src;this.error=null;}if(name==='waiting')this.readyState=2;if(name==='error')this.error={code:2};this.dispatch(name);}
    play(){this.ended=false;this.paused=false;const request={};this.plays.push(request);return new Promise((resolve,reject)=>Object.assign(request,{resolve:()=>{this.emit('playing');resolve();},reject}));}
    pause(){this.paused=true;this.dispatch('pause');}
    removeAttribute(key){delete this[key];}
    load(){this.ended=false;this.paused=true;this.readyState=0;this.currentSrc='';this.error=null;this.dispatch('emptied');}
  }
  const music=new MusicPlayer({getIndex:()=>game.state.playlist,getPaused:()=>game.journey.musicPaused,tracks:catalog.playlists,AudioClass:Audio,onended:index=>{
    if(game.journey.musicPaused||!['playing','closing','guide','results'].includes(game.phase)||game.state.playlist!==index)return;
    const tracks=game.metrics().allowedTracks,next=tracks[(tracks.indexOf(index)+1)%tracks.length];
    game.act('musicState',{paused:false});assert(game.act('playlist',{index:next}).success);moves.push(next);music.select();
  }});
  music.wake();return{game,music,audios,moves};
}
async function heard(h){h.audios.at(-1).plays.at(-1).resolve();await flush();assert(h.music.playing);}

// Real ended signals advance only the booked lineup in authored order and
// persist the selected row. The final allowed song wraps to its first song.
for(const [showId,expected] of [['freewill',[0,3,4,0]],['july',[1,2,1]],['double',[0,1,2,3,4,0]]]){
  const h=harness(showId);assert.equal(h.game.state.playlist,expected[0]);
  for(const next of expected.slice(1)){
    await heard(h);const old=h.music.audio,oldCallbacks=[...old.listeners.get('ended')];assert.equal(old.loop,false);old.emit('ended');
    assert.equal(h.game.state.playlist,next);assert.equal(h.music.index,next);assert.equal(h.music.audio,old,'Automatic progression must retain WebKit element permission');
    assert.equal(h.audios.length,1,'A natural ending created another restricted media element');
    assert.equal(h.music.playing,false,'A pending next track must not count as heard music');
    const saved=new SimBarGame({storage:h.game.storage});saved.act('continue');assert.equal(saved.state.playlist,next);
    assert.equal(h.music.audio.src,catalog.playlists[next].audio_url);
    const selected=h.music.audio,count=h.moves.length;for(const callback of oldCallbacks)callback();old.dispatch('ended');old.dispatch('error');old.dispatch('playing');
    assert.equal(h.music.audio,selected);assert.equal(h.moves.length,count,'An obsolete audio element advanced the new song');
  }
  h.music.destroy();
}

// A dispatched event without a native ended state is not a song completion.
// A consumed ended event cannot repeatedly advance a one-track consumer.
{
  const h=harness();await heard(h);const audio=h.music.audio;audio.dispatch('ended');
  assert.equal(h.moves.length,0);assert(h.music.playing);
  let callbacks=0;h.music.onended=()=>{callbacks++;};audio.emit('ended');audio.emit('ended');
  assert.equal(callbacks,1);assert.equal(h.music.playing,false);h.music.destroy();audio.emit('ended');assert.equal(callbacks,1);
}

// Reused elements retain only the current source listeners. Native events
// queued by the previous load cannot silence an already playing replacement.
{
  const h=harness();await heard(h);const audio=h.music.audio;audio.emit('ended');await heard(h);
  const count=h.moves.length;for(const name of ['pause','waiting','stalled','emptied','error','ended'])audio.dispatch(name);
  assert.equal(h.moves.length,count);assert(h.music.playing);assert.equal(h.music.failed,false);h.music.destroy();
}
{
  const h=harness();await heard(h);let rounds=0;h.music.onended=()=>{rounds++;h.music.select();};
  const audio=h.music.audio;audio.emit('ended');audio.dispatch('ended');assert.equal(rounds,1);
  await heard(h);audio.emit('ended');assert.equal(rounds,2);assert.equal(h.audios.length,1);h.music.destroy();
}

// Waiting, stalls, failures and pauses never skip a song. A failed automatic
// play stays on the next selected song, waiting for a successful user gesture.
{
  const h=harness();await heard(h);const audio=h.music.audio;
  for(const event of ['waiting','stalled','pause','emptied','error']){audio.emit(event);assert.equal(h.moves.length,0);assert.equal(h.game.state.playlist,0);}
  audio.emit('playing');audio.emit('ended');assert.equal(h.game.state.playlist,3);
  const next=h.music.audio;next.plays.at(-1).reject(Object.assign(new Error('Gesture required'),{name:'NotAllowedError'}));await flush();
  assert.equal(h.music.blocked,true);assert.equal(h.game.journey.musicPaused,false);assert.equal(h.game.journey.musicPauseIntent,false);
  const requests=next.plays.length;for(let i=0;i<20;i++)h.music.sync();assert.equal(next.plays.length,requests);
  h.music.wake();assert.equal(next.plays.length,requests+1);await heard(h);assert.equal(h.game.state.playlist,3);h.music.destroy();
}

// Explicit saved pause wins even if the end notification races ahead of
// sync(). Context suspension and lobby/planning races do not change selection.
for(const scenario of ['saved-pause','suspend','lobby','planning']){
  const h=harness();await heard(h);const audio=h.music.audio;
  if(scenario==='saved-pause')h.game.act('musicState',{paused:true});
  if(scenario==='suspend')h.music.suspend();
  if(scenario==='lobby')h.game.act('lobby');
  if(scenario==='planning')h.game.phase='planning';
  audio.emit('ended');assert.equal(h.game.state.playlist,0,scenario);assert.equal(h.moves.length,0,scenario);h.music.destroy();
}

// Manual song selection before the end callback and late play promises from
// the old element cannot redirect or activate the replacement.
{
  const h=harness();const old=h.music.audio,oldRequest=old.plays[0];
  h.game.act('playlist',{index:4});old.emit('ended');assert.equal(h.moves.length,0);h.music.select();
  const current=h.music.audio;oldRequest.resolve();await flush();old.emit('ended');assert.equal(h.music.audio,current);assert.equal(h.game.state.playlist,4);
  await heard(h);current.emit('ended');assert.equal(h.game.state.playlist,0);h.music.destroy();
}

// Music remains independent of the gameplay pause flag. Manual replay in
// closing/results continues its lineup, but the automatic last-call music
// pause prevents an end callback from starting another song.
for(const phase of ['playing','guide','closing','results']){
  const h=harness();await heard(h);h.game.phase=phase;h.game.paused=true;
  h.music.audio.emit('ended');assert.equal(h.game.state.playlist,3,phase);h.music.destroy();
}
{
  const h=harness();await heard(h);h.game.journey.secondsLeft=.05;h.game.tick(.05);
  assert.equal(h.game.journey.musicPaused,true);h.music.audio.emit('ended');assert.equal(h.game.state.playlist,0);h.music.destroy();
}
console.log('PASS: natural track advance, booked order and wrap, saved selection, pending playback gate, stale/duplicate end isolation, blocked retry, explicit pause/context guards and manual replay');
