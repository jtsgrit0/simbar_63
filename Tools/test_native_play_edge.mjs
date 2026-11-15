import assert from 'node:assert/strict';
import {fileURLToPath,pathToFileURL} from 'node:url';
import path from 'node:path';
import fs from 'node:fs/promises';

// Runs the actual deployed handler with dummy credentials and a local RPC stub.
// No hosted project, production counters or network requests are touched.
const ROOT=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');

const env = {
  SUPABASE_PUBLISHABLE_KEYS: JSON.stringify({default:'sb_publishable_test_public'}),
  SUPABASE_SECRET_KEYS: JSON.stringify({default:'sb_secret_test_server_only'}),
  SUPABASE_URL: 'https://test.invalid',
};
let handler;
globalThis.Deno = {env:{get:name=>env[name]}, serve:fn=>{handler=fn;}};
const databaseCalls=[];
const events=new Map();
const counts=new Map();
const baseNow=Date.now();
let now=baseNow;
Date.now=()=>now;
let databaseFailure=false;
globalThis.fetch=async(url, options)=>{
  assert.equal(url,'https://test.invalid/rest/v1/rpc/record_music_play');
  assert.equal(options.headers.apikey,env.SUPABASE_SECRET_KEYS && 'sb_secret_test_server_only');
  assert.equal(options.headers.Authorization,undefined);
  const body=JSON.parse(options.body);
  databaseCalls.push(body);
  if(databaseFailure)return Response.json({code:'XX000'},{status:500});
  const old=events.get(body.p_play_id);
  if(old&&old!==body.p_track_id)return Response.json({code:'23505'},{status:409});
  if(!old){events.set(body.p_play_id,body.p_track_id);counts.set(body.p_track_id,(counts.get(body.p_track_id)||0)+1);}
  return Response.json([{track_id:body.p_track_id,play_count:String(counts.get(body.p_track_id)),counted:!old}]);
};
await import(pathToFileURL(path.resolve(ROOT,'supabase/functions/track-play/index.ts')).href);
const reports=[];
async function call(body, {key='sb_publishable_test_public',origin='https://jtsgrit0.github.io',method='POST',raw=null}={}){
  const headers={'content-type':'application/json'};
  if(key!==null)headers.apikey=key;
  if(origin!==null)headers.origin=origin;
  const response=await handler(new Request('https://test.invalid/functions/v1/track-play',{
    method,headers,body:method==='POST'?(raw??JSON.stringify(body)):undefined,
  }));
  const text=await response.text();
  return{status:response.status,body:text?JSON.parse(text):null,headers:response.headers};
}
async function check(name,fn){await fn();reports.push({name,passed:true});}
const id='a1234567-1234-4567-8123-123456789012';
let ticket;
await check('preflight without credential',async()=>{
  const r=await call(null,{key:null,method:'OPTIONS'});assert.equal(r.status,204);
  assert.equal(r.headers.get('access-control-allow-origin'),'https://jtsgrit0.github.io');
});
await check('reject missing/wrong apikey',async()=>{
  for(const key of [null,'sb_publishable_wrong'])assert.equal((await call({}, {key})).status,401);
});
await check('CORS allowed app/dev and reject foreign origin',async()=>{
  assert.equal((await call(null,{method:'OPTIONS',origin:'simbar://app',key:null})).status,204);
  for(const origin of ['http://localhost:8000','http://127.0.0.1:54321','http://[::1]:8000'])
    assert.equal((await call(null,{method:'OPTIONS',origin,key:null})).status,204);
  assert.equal((await call({}, {origin:'https://evil.invalid'})).status,403);
  assert.equal((await call({}, {origin:'https://jtsgrit0.github.io.evil.invalid'})).status,403);
});
await check('body validation and request size cap',async()=>{
  for(const body of [{},{action:'start',playId:'bad',trackId:'track_001',source:'web'},
    {action:'start',playId:id,trackId:'track_999',source:'web'},
    {action:'start',playId:id,trackId:'track_001',source:'unknown'}])assert.equal((await call(body)).status,400);
  assert.equal((await call({}, {raw:'x'.repeat(5000)})).status,400);
});
await check('start returns signed 30s/24h ticket and performs no database write',async()=>{
  const r=await call({action:'start',playId:id.toUpperCase(),trackId:'track_001',source:'web'});
  assert.equal(r.status,200);ticket=r.body.ticket;
  assert.equal(r.body.notBefore,now+30000);assert.equal(r.body.expiresAt,now+86400000);
  assert.equal(databaseCalls.length,0);
});
const qualify=(overrides={})=>call({action:'qualify',playId:id,trackId:'track_001',ticket,seconds:30,...overrides});
await check('early qualification is retryable and does not call database',async()=>{
  now=baseNow+5000;const r=await qualify();assert.equal(r.status,409);
  assert.equal(r.body.code,'TOO_EARLY');assert.equal(r.body.retryAfter,25);
  assert.equal(r.headers.get('Retry-After'),'25');assert.equal(databaseCalls.length,0);
});
await check('reject less than 30 actual seconds even after wallclock',async()=>{
  now=baseNow+31000;assert.equal((await qualify({seconds:29.999})).status,400);
});
await check('reject tampering and mismatched ticket track/play',async()=>{
  const pieces=ticket.split('.');const t=JSON.parse(Buffer.from(pieces[0],'base64url'));
  t.trackId='track_002';const forged=Buffer.from(JSON.stringify(t)).toString('base64url')+'.'+pieces[1];
  assert.equal((await qualify({ticket:forged,trackId:'track_002'})).status,400);
  assert.equal((await qualify({trackId:'track_002'})).status,400);
  assert.equal((await qualify({playId:'b1234567-1234-4567-8123-123456789012'})).status,400);
  assert.equal(databaseCalls.length,0);
});
await check('qualify returns decimal string aggregate; retry counts once',async()=>{
  const a=await qualify();assert.equal(a.status,200);
  assert.deepEqual(a.body,{trackId:'track_001',playCount:'1',counted:true});
  const b=await qualify();assert.deepEqual(b.body,{trackId:'track_001',playCount:'1',counted:false});
  assert.equal(events.size,1);assert.equal(databaseCalls[0].p_source,'web');
  assert.equal(databaseCalls[0].p_ticket_issued_at,new Date(baseNow).toISOString());
});
await check('same play id cannot be reused for another track',async()=>{
  const s=await call({action:'start',playId:id,trackId:'track_002',source:'windows'});
  now+=31000;const r=await qualify({trackId:'track_002',ticket:s.body.ticket});
  assert.equal(r.status,409);assert.equal(r.body.code,'PLAY_ID_CONFLICT');assert.equal(events.size,1);
});
await check('database failure is retryable without exposing server message',async()=>{
  databaseFailure=true;const r=await qualify();assert.equal(r.status,503);
  assert.equal(r.body.code,'SERVICE_UNAVAILABLE');assert.equal(r.headers.get('Retry-After'),'5');
  databaseFailure=false;
});
await check('expired ticket is final and does not reach database',async()=>{
  const before=databaseCalls.length;now=baseNow+86400000;
  const r=await qualify();assert.equal(r.status,410);assert.equal(r.body.code,'EXPIRED_TICKET');
  assert.equal(databaseCalls.length,before);
});
const report={passed:reports.every(x=>x.passed),checks:reports.length,reports};
const output=path.resolve(ROOT,'Temp/TrackPlayBackendTests');
await fs.mkdir(output,{recursive:true});
await fs.writeFile(path.join(output,'edge-report.json'),JSON.stringify(report,null,2));
console.log(JSON.stringify(report,null,2));
