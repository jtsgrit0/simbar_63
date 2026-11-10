import assert from 'node:assert/strict';
import {createDailyPoster,dailyPosterImage,DAILY_POSTER_INDEX} from './WebGLTemplate/FastGame/daily-poster.js';
import {Booking} from './WebGLTemplate/FastGame/booking.js';
import {SimBarGame,catalog} from './WebGLTemplate/FastGame/core.js';
import {MusicPlayer} from './WebGLTemplate/FastGame/music.js';
import {readFileSync} from 'node:fs';

const fixtures=[[1,0,[5,10,3]],[1,1,[11,4,9]],[1,2,[5,10,3]],[1,3,[7,0,5]],[2,0,[10,3,8]],[2,1,[8,1,6]],[2,2,[2,7,0]],[2,3,[0,5,10]],[29,2,[1,6,11]],[9999,1,[3,8,1]]];
for(const [day,venue,indices] of fixtures)assert.deepEqual(createDailyPoster(day,venue).artistIndices,indices);
for(let day=1;day<=9999;day+=37)for(let venue=0;venue<4;venue++){
  const p=createDailyPoster(day,venue);assert.equal(new Set(p.artistIndices).size,3);assert(p.artistIndices.every(i=>i>=0&&i<12));
  assert.equal(p.venueId,catalog.venues[venue].id);assert.equal(p.index,DAILY_POSTER_INDEX);
}
assert.notDeepEqual(createDailyPoster(1,0).artistIndices,createDailyPoster(2,0).artistIndices);
assert(dailyPosterImage(createDailyPoster(1,0)).startsWith('data:image/svg+xml;'));

const plan=Booking.newPlan(1,0),career={...Booking.newCareer(),discoveredMask:0};
assert.equal(Booking.scoutDaily(plan,career).code,'scouted');assert.equal(plan.blocks,1);assert.equal(plan.scoutedPosterMask,1<<30);
assert.equal(Booking.scoutDaily(plan,career).code,'already_scouted');
Booking.sanitize(plan,career);assert.equal(plan.scoutedPosterMask,1<<30);assert.equal(plan.blocks,1);
assert.equal(Booking.dailyPosterDiscovered(career,1,0),true);
assert.equal(Booking.scoutDaily(Booking.newPlan(),Booking.newCareer()).code,'already_discovered');

const data=new Map(),storage={getItem:k=>data.get(k)??null,setItem:(k,v)=>data.set(k,v)};
const game=new SimBarGame({storage,seed:123});game.act('owner',{index:2});game.act('start');assert.equal(game.journey.booking.venue,1);
assert.equal(game.act('venue',{venue:3}).success,false);
// A completed historical contract retains its venue for the next desk.
game.journey.booking=Booking.newPlan(1,3);game.journey.booking.completed=true;game.journey.booking.locked=true;game.journey.day=2;game._planning(false);
assert.equal(game.journey.booking.venue,3);game.save();const loaded=new SimBarGame({storage});loaded.act('continue');assert.equal(loaded.journey.booking.venue,3);
assert.equal(game.act('playlist',{index:4}).success,true);assert.equal(game.act('playlist',{index:5}).success,false);
assert.equal(catalog.playlists.length,5);assert.equal(catalog.playlists[4].filename,'Electric Shocker - Korean Version.mp3');
const configuredTracks=JSON.parse(readFileSync(new URL('../playlist.json',import.meta.url),'utf8')).tracks;
assert.deepEqual(catalog.playlists.map(track=>track.audio_url),configuredTracks.map(track=>track.audio_url));
assert.deepEqual(catalog.playlists.map(track=>track.trackId),configuredTracks.map(track=>track.id));
assert.equal(catalog.playlists[1].name,'Hey');

const audios=[];class FakeAudio{
  constructor(){audios.push(this);this.playCount=0;this.pauseCount=0;}play(){this.playCount++;return Promise.resolve();}pause(){this.pauseCount++;}load(){}removeAttribute(){}addEventListener(){}
}
let selected=0;const music=new MusicPlayer({getIndex:()=>selected,tracks:catalog.playlists,baseURL:new URL('https://example.test/simbar/'),AudioClass:FakeAudio});
assert.equal(audios.length,0);music.sync();assert.equal(audios.length,0);music.wake();assert.equal(audios.length,1);
assert.equal(audios[0].src,configuredTracks[0].audio_url);assert.equal(audios[0].loop,true);
music.sync();assert.equal(audios.length,1);music.toggle();assert.equal(music.paused,true);music.toggle();assert.equal(music.paused,false);
selected=4;music.select();assert.equal(audios.length,2);assert(audios[0].pauseCount>=1);assert(audios[1].src.endsWith('Electric%20Shocker%20-%20Korean%20Version.mp3'));
for(selected=0;selected<5;selected++){
  music.select();assert.equal(music.audio.src,configuredTracks[selected].audio_url);
  assert(!music.audio.src.includes('StreamingAssets/Music/'),'Streaming must not request the local MP3 copy');
  assert(!music.audio.src.includes('%2520'),'Already encoded object paths must not be encoded again');
}
music.destroy();assert.equal(music.audio,null);
console.log('PASS: daily C# schedule fixtures + 1084 lineups, bit30/save migration, venue persistence, five real tracks, audio gesture/select/pause lifecycle');
