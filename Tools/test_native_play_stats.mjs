import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import {PlayStats} from './WebGLTemplate/FastGame/play-stats.js';
import {playStatsConfig} from './WebGLTemplate/FastGame/play-stats-config.js';

const QUEUE_KEY='simbar.music-qualified-plays.v1';
const PUBLIC_KEY='sb_publishable_native_test';
const ENDPOINT='https://analytics.invalid';
const id=n=>`00000000-0000-4000-8000-${String(n).padStart(12,'0')}`;
const play=(n=1,trackId='track_001')=>({playId:id(n),trackId});
const settle=async()=>{for(let i=0;i<20;i++)await Promise.resolve();};
const memory=()=>{const data=new Map();return{getItem:key=>data.get(key)??null,setItem:(key,value)=>data.set(key,String(value))};};
const response=(body,status=200,headers={})=>({ok:status>=200&&status<300,status,headers:new Headers(headers),json:async()=>body});
function clock(){
  let now=1800000000000,sequence=0;
  const timers=new Map();
  return{
    now:()=>now,
    setTimer:(callback,delay)=>{const token=++sequence;timers.set(token,{callback,when:now+Math.max(0,delay)});return token;},
    clearTimer:token=>timers.delete(token),
    async advance(milliseconds){
      const target=now+milliseconds;
      for(let iteration=0;iteration<1000;iteration++){
        const next=[...timers].filter(([,timer])=>timer.when<=target).sort((a,b)=>a[1].when-b[1].when)[0];
        if(!next){now=target;await settle();return;}
        now=next[1].when;timers.delete(next[0]);next[1].callback();await settle();
      }
      throw new Error('Unbounded analytics timer loop');
    },
  };
}
function server(time){
  const calls=[],tickets=new Map(),ledger=new Map();
  const counts=new Map(Array.from({length:5},(_,index)=>[`track_00${index+1}`,0]));
  let sequence=0;
  const control={offline:false,readFailure:false,readRows:null,qualifyResponses:[],loseAcceptedResponse:false};
  const fetcher=async(url,options={})=>{
    assert.equal(options.headers.apikey,PUBLIC_KEY);
    assert.equal(options.headers.Authorization,undefined,'Browser analytics must not use a privileged JWT');
    const body=options.body?JSON.parse(options.body):null;
    calls.push({url,body,at:time.now()});
    if(control.offline)throw new TypeError('offline');
    if(url.includes('/rest/v1/')){
      assert.equal(url,ENDPOINT+'/rest/v1/track_play_counts?select=track_id,play_count');
      if(control.readFailure)return response({message:'unavailable'},503);
      return response(control.readRows??[...counts].map(([track_id,play_count])=>({track_id,play_count})));
    }
    assert.equal(url,ENDPOINT+'/functions/v1/track-play');
    if(body.action==='start'){
      const ticket=`ticket-${++sequence}`;
      tickets.set(ticket,{...body,issuedAt:time.now()});
      return response({ticket,notBefore:time.now()+30000,expiresAt:time.now()+86400000});
    }
    assert.equal(body.action,'qualify');assert.equal(body.seconds,30);
    if(control.qualifyResponses.length)return control.qualifyResponses.shift()();
    const ticket=tickets.get(body.ticket);
    assert(ticket,'Qualification must send a server-issued ticket');
    assert.equal(ticket.playId,body.playId);assert.equal(ticket.trackId,body.trackId);
    if(time.now()-ticket.issuedAt<30000)return response({code:'TOO_EARLY',retryAfter:5},409,{'Retry-After':'5'});
    const existing=ledger.get(body.playId);
    if(existing&&existing!==body.trackId)return response({code:'PLAY_ID_CONFLICT'},409);
    if(!existing){ledger.set(body.playId,body.trackId);counts.set(body.trackId,counts.get(body.trackId)+1);}
    if(control.loseAcceptedResponse){control.loseAcceptedResponse=false;throw new TypeError('response lost after commit');}
    return response({trackId:body.trackId,playCount:String(counts.get(body.trackId)),counted:!existing});
  };
  return{calls,counts,ledger,control,fetcher,posts:action=>calls.filter(call=>call.body?.action===action)};
}
function harness({storage=memory(),source='web',time=clock(),backend=null,fetcher=null,onchange=()=>{}}={}){
  backend??=server(time);
  const stats=new PlayStats({url:ENDPOINT,publishableKey:PUBLIC_KEY,storage,fetcher:fetcher??backend.fetcher,source,
    now:time.now,setTimer:time.setTimer,clearTimer:time.clearTimer,onchange});
  return{stats,time,backend,storage};
}
const passed=[];
async function test(name,run){await run();passed.push(name);}

await test('Unknown totals differ from a confirmed zero; shared reads coalesce',async()=>{
  const h=harness();assert.equal(h.stats.count('track_001'),null);assert.equal(h.stats.state,'loading');
  await Promise.all([h.stats.refresh(),h.stats.refresh()]);assert.equal(h.backend.calls.length,1);
  assert.equal(h.stats.state,'ready');for(let i=1;i<=5;i++)assert.equal(h.stats.count(`track_00${i}`),0);
  h.stats.destroy();
});

await test('Default timers preserve the browser Window receiver',async()=>{
  const originalSet=globalThis.setTimeout,originalClear=globalThis.clearTimeout;
  let sequence=0;
  globalThis.setTimeout=function(){assert.equal(this,globalThis,'Window.setTimeout called with a PlayStats receiver');return ++sequence;};
  globalThis.clearTimeout=function(){assert.equal(this,globalThis,'Window.clearTimeout called with a PlayStats receiver');};
  try{
    const rows=Array.from({length:5},(_,i)=>({track_id:`track_00${i+1}`,play_count:0}));
    const stats=new PlayStats({url:ENDPOINT,publishableKey:PUBLIC_KEY,storage:memory(),fetcher:async()=>response(rows)});
    await stats.refresh();assert.equal(stats.state,'ready');stats.destroy();
  }finally{globalThis.setTimeout=originalSet;globalThis.clearTimeout=originalClear;}
});

await test('Read failures preserve confirmed totals; stale snapshots never lower them',async()=>{
  const h=harness();h.backend.counts.set('track_001',17);await h.stats.refresh();
  h.backend.control.readFailure=true;await h.stats.refresh();assert.equal(h.stats.state,'unavailable');
  assert.equal(h.stats.count('track_001'),17);assert.equal(h.stats.count('track_002'),0);
  h.backend.control.readFailure=false;h.backend.counts.set('track_001',8);await h.stats.refresh();
  assert.equal(h.stats.state,'ready');assert.equal(h.stats.count('track_001'),17);h.stats.destroy();
});

await test('Incomplete/malformed totals cannot masquerade as five confirmed zeros',async()=>{
  const cases=[null,false,'', 'not-a-count',-1,1.5,Number.MAX_SAFE_INTEGER+1];
  for(const bad of cases){
    const h=harness();h.backend.control.readRows=Array.from({length:5},(_,i)=>({track_id:`track_00${i+1}`,play_count:i===0?bad:0}));
    await h.stats.refresh();assert.equal(h.stats.state,'unavailable',`Invalid count accepted: ${String(bad)}`);
    assert.equal(h.stats.count('track_001'),null);h.stats.destroy();
  }
  const h=harness();h.backend.control.readRows=[{track_id:'track_001',play_count:0}];await h.stats.refresh();
  assert.equal(h.stats.state,'unavailable');h.stats.destroy();
});

await test('Starts are transient; qualification persists immediately and counts one UUID once',async()=>{
  const h=harness();h.stats.start(play());await settle();
  assert.equal(h.storage.getItem(QUEUE_KEY),null,'An unqualified listen must not become a saved event');
  await h.time.advance(30000);h.stats.qualify({...play(),seconds:30});h.stats.qualify({...play(),seconds:30});
  await h.stats.flush();assert.equal(h.backend.posts('qualify').length,1);assert.equal(h.backend.ledger.size,1);
  assert.equal(h.stats.count('track_001'),1);assert.deepEqual(JSON.parse(h.storage.getItem(QUEUE_KEY)),[]);
  h.stats.destroy();
});

await test('No network promise blocks start/qualify; heard episodes persist before response',async()=>{
  let release;
  const h=harness({fetcher:()=>new Promise(resolve=>{release=resolve;})});
  assert.equal(h.stats.start(play()),undefined);
  assert.equal(h.stats.qualify({...play(),seconds:30}),undefined);
  const saved=JSON.parse(h.storage.getItem(QUEUE_KEY));assert.equal(saved.length,1);assert.equal(saved[0].qualified,true);
  release(response({ticket:'pending-ticket',notBefore:h.time.now()+30000,expiresAt:h.time.now()+86400000}));
  await settle();h.stats.destroy();
});

await test('Offline qualified plays survive reload and resume with the original UUID',async()=>{
  const h=harness();h.backend.control.offline=true;h.stats.start(play());await settle();
  h.stats.qualify({...play(),seconds:30});await h.stats.flush();
  assert.equal(JSON.parse(h.storage.getItem(QUEUE_KEY))[0].playId,id(1));h.stats.destroy();
  h.backend.control.offline=false;
  const again=harness({time:h.time,backend:h.backend,storage:h.storage});again.stats.resume();await settle();
  assert.equal(h.backend.posts('qualify').length,0,'A recovered offline episode still needs a valid server ticket');
  await h.time.advance(30000);await again.stats.flush();
  assert.equal(h.backend.ledger.size,1);assert.equal(h.backend.posts('qualify')[0].body.playId,id(1));
  assert.equal(again.stats.count('track_001'),1);again.stats.destroy();
});

await test('A lost response after commit retries the same UUID without inflating totals',async()=>{
  const h=harness();h.stats.start(play());await settle();await h.time.advance(30000);
  h.backend.control.loseAcceptedResponse=true;h.stats.qualify({...play(),seconds:30});await h.stats.flush();
  assert.equal(h.backend.ledger.size,1);assert.equal(h.stats.count('track_001'),null);
  await h.time.advance(2000);await h.stats.flush();
  assert.equal(h.backend.posts('qualify').length,2);assert.equal(h.backend.counts.get('track_001'),1);
  assert.equal(h.stats.count('track_001'),1);assert.deepEqual(JSON.parse(h.storage.getItem(QUEUE_KEY)),[]);h.stats.destroy();
});

await test('TOO_EARLY honors Retry-After and retains the qualified episode',async()=>{
  const h=harness();h.stats.start(play());await settle();await h.time.advance(30000);
  h.backend.control.qualifyResponses.push(()=>response({code:'TOO_EARLY',retryAfter:7},409,{'Retry-After':'7'}));
  h.stats.qualify({...play(),seconds:30});await h.stats.flush();assert.equal(h.backend.posts('qualify').length,1);
  assert.equal(JSON.parse(h.storage.getItem(QUEUE_KEY)).length,1);await h.time.advance(6999);
  assert.equal(h.backend.posts('qualify').length,1);await h.time.advance(1);await h.stats.flush();
  assert.equal(h.backend.posts('qualify').length,2);assert.equal(h.stats.count('track_001'),1);h.stats.destroy();
});

await test('Expired signed tickets are renewed, preserving the UUID and heard qualification',async()=>{
  const h=harness();h.stats.start(play());await settle();await h.time.advance(30000);
  h.backend.control.qualifyResponses.push(()=>response({code:'EXPIRED_TICKET'},410));
  h.stats.qualify({...play(),seconds:30});await h.stats.flush();assert.equal(h.backend.posts('start').length,1);
  await h.time.advance(1000);assert.equal(h.backend.posts('start').length,2);
  assert.equal(h.backend.posts('qualify').length,1);await h.time.advance(30000);await h.stats.flush();
  assert.equal(h.stats.count('track_001'),1);assert.equal(h.backend.ledger.size,1);
  for(const request of h.backend.calls.filter(call=>call.body))assert.equal(request.body.playId,id(1));h.stats.destroy();
});

await test('Terminal input or play-ID conflicts are discarded instead of retried forever',async()=>{
  for(const [code,status] of [['INVALID_TRACK',400],['PLAY_ID_CONFLICT',409]]){
    const h=harness();h.stats.start(play());await settle();await h.time.advance(30000);
    h.backend.control.qualifyResponses.push(()=>response({code},status));
    h.stats.qualify({...play(),seconds:30});await h.stats.flush();assert.deepEqual(JSON.parse(h.storage.getItem(QUEUE_KEY)),[],code);
    await h.time.advance(600000);assert.equal(h.backend.posts('qualify').length,1,code);h.stats.destroy();
  }
});

await test('Short/mismatched episodes and throwing storage never block the caller',async()=>{
  const brokenStorage={getItem(){throw new Error('blocked storage');},setItem(){throw new Error('quota');}};
  const h=harness({storage:brokenStorage,source:'windows'});
  h.stats.qualify({...play(),seconds:29.9});await settle();assert.equal(h.backend.calls.length,0);
  h.stats.start(play());await settle();h.stats.qualify({...play(1,'track_002'),seconds:30});await settle();
  assert.equal(h.backend.posts('start').length,1);assert.equal(h.backend.posts('start')[0].body.source,'windows');
  await h.time.advance(30000);assert.doesNotThrow(()=>h.stats.qualify({...play(),seconds:30}));await h.stats.flush();
  assert.equal(h.stats.count('track_001'),1);h.stats.destroy();
});

await test('Browser configuration contains only the project URL and public credential',async()=>{
  assert.match(playStatsConfig.url,/^https:\/\/[a-z0-9]+\.supabase\.co$/);
  assert.match(playStatsConfig.publishableKey,/^sb_publishable_/);
  const client=(await fs.readFile(new URL('./WebGLTemplate/FastGame/play-stats.js',import.meta.url),'utf8'))+
    (await fs.readFile(new URL('./WebGLTemplate/FastGame/play-stats-config.js',import.meta.url),'utf8'));
  assert.doesNotMatch(client,/sb_secret_|SUPABASE_SERVICE_ROLE_KEY|SUPABASE_SECRET_KEYS/);
});

console.log(`PASS: ${passed.length} play-stat scenarios: public totals, unknown/zero distinction, malformed reads, nonblocking playback, qualified persistence, offline reload, idempotent response-loss retries, 30-second tickets, retry timing, expiry renewal, terminal conflicts, storage denial and browser credential boundaries`);
