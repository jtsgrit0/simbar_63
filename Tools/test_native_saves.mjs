import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {parseUnityPrefs,md5,unityPreferencePath,migrateLegacySave} from './WebGLTemplate/FastGame/legacy-save.js';
import {SimBarGame,SAVE_KEY} from './WebGLTemplate/FastGame/core.js';
import {Challenges,Floor} from './WebGLTemplate/FastGame/floor.js';

const storage=()=>{const map=new Map();return{getItem:k=>map.get(k)??null,setItem:(k,v)=>map.set(k,String(v))};};
for(const text of ['', 'https://jtsgrit0.github.io/simbar_63', '홍대 이태원'])assert.equal(md5(text),createHash('md5').update(text).digest('hex'));
assert.equal(unityPreferencePath('https://example.org/simbar/index.html'),'/idbfs/'+md5('https://example.org/simbar')+'/PlayerPrefs');
const data=storage(),g=new SimBarGame({storage:data,seed:1});g.act('owner',{index:2});g.act('start');g.save();
const raw=data.getItem(SAVE_KEY);
const binary=(values)=>{
  const parts=[Buffer.from('UnityPrf'),Buffer.alloc(8)];
  for(const [key,value] of Object.entries(values)){
    const k=Buffer.from(key),v=Buffer.from(value);let n;
    if(v.length<128)n=Buffer.from([v.length]);else{n=Buffer.alloc(5);n[0]=128;n.writeUInt32LE(v.length,1);}
    parts.push(Buffer.from([k.length]),k,n,v);
  }return Buffer.concat(parts);
};
const blob=binary({[SAVE_KEY]:raw,'SimBar.Owner':'pet-sounds','SimBar.Language':'en'});
assert.equal(parseUnityPrefs(blob)[SAVE_KEY],raw);
assert.equal(parseUnityPrefs(blob)['SimBar.Owner'],'pet-sounds');
assert.deepEqual(parseUnityPrefs(new Uint8Array(20)),{});
assert.equal(parseUnityPrefs(blob.subarray(0,45))[SAVE_KEY],undefined);
function fakeIDB(delay=1){return{
  databases:async()=>[{name:'/idbfs'}],
  open(){const request={};setTimeout(()=>{request.result={objectStoreNames:{contains:()=>true},close(){},transaction(){return{objectStore(){return{get(){const r={};setTimeout(()=>{r.result={contents:blob};r.onsuccess();},delay);return r;}};}};}};request.onsuccess();},0);return request;}
};}
const migrated=storage(),result=await migrateLegacySave({storage:migrated,indexedDB:fakeIDB(),url:'https://example.org/simbar/',timeoutMs:100});
assert(result.imported);assert.equal(migrated.getItem('SimBar.LegacyUnity.v1'),raw);assert.equal(migrated.getItem('SimBar.Language'),'en');
const newProgress=storage(),late=await migrateLegacySave({storage:newProgress,indexedDB:fakeIDB(30),url:'https://example.org/simbar/',timeoutMs:1});
assert.equal(late.reason,'pending');newProgress.setItem(SAVE_KEY,'new progress');await late.pending;assert.equal(newProgress.getItem(SAVE_KEY),'new progress');

// A real seeded night generates its own paper and guest events; no event-spawn action.
const live=new SimBarGame({storage:storage(),seed:1});live.act('start');live.act('launch');for(let i=0;i<3;i++)live.act('guide');
assert.equal(live.phase,'playing');assert.equal(live.act('throwConfetti').success,false);
const before=live.journey.secondsLeft;live.act('pause');live.tick(2);assert.equal(live.journey.secondsLeft,before);live.act('resume');
let guest=false,paper=false,arrived=false,withoutInput=false;
for(let i=0;i<6000&&live.phase!=='results';i++){
  live.tick(.1);guest ||= live.floor.guestSpawned;paper ||= live.floor.litter>0;
  if(live.farewellPending)live.act('farewellAck');
  if(live.closing&&live.cleanupReadyWorkers>0){arrived=true;if(!withoutInput){const cleaned=live.floor.cleaned;live.tick(1);assert.equal(live.floor.cleaned,cleaned);withoutInput=true;}}
  if(live.reaction.activeKind)live.act('quick',{kind:live.reaction.activeKind});
}
assert(guest&&paper&&arrived&&withoutInput);assert.equal(live.phase,'results');assert(live.floor.cleanupComplete&&live.floor.guestComplete);assert.equal(live.journey.resultFloorBonus,70);
live.save();const savedWallet=live.state.money,savedXP=live.journey.xp;
const reloaded=new SimBarGame({storage:live.storage});reloaded.act('continue');reloaded.tick(3);assert.equal(reloaded.state.money,savedWallet);assert.equal(reloaded.journey.xp,savedXP);
assert.equal(Floor.settle(reloaded.floor).success,false);
const challenge=Challenges.newState(7);Challenges.start(challenge,2);assert(Challenges.press(challenge,2).accepted);assert.equal(Challenges.press(challenge,2).accepted,false);
const remaining=challenge.secondsLeft;Challenges.tick(challenge,4,false);assert.equal(challenge.secondsLeft,remaining);Challenges.tick(challenge,7);assert.equal(challenge.timeouts,1);
console.log('PASS: Unity binary preferences, delayed migration preserves new progress, real random night + employee arrival + input gating, once-only receipt, pause/debounce/timeout');
