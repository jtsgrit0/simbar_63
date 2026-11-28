import assert from 'node:assert/strict';
import fs from 'node:fs';
import {assignPatronModels,audienceIdentity,patronPools,authoredFrame,drawWholePatron} from './WebGLTemplate/FastGame/patron-models.js';
import {Audience} from './WebGLTemplate/FastGame/audience.js';
import {Festival} from './WebGLTemplate/FastGame/festival.js';
import {SimBarGame} from './WebGLTemplate/FastGame/core.js';
import {launchPreparedDesk} from './native_test_workflow.mjs';
const art=new URL('./WebGLTemplate/FastGame/art/audience/',import.meta.url);
const meta=JSON.parse(fs.readFileSync(new URL('whole-models.json',art)));
const ids=new Set(patronPools.flat());
assert.deepEqual(patronPools, [[1,4,5,9,14,15,24,26,53,94],[6,7,8,10,12,13,17,27,44],[2,3,11,16,18,19,20,21,22,23,33,57]]);
assert.equal(ids.size,31);
assert.deepEqual(new Set(meta.entries.map(e=>e.id)),ids);
assert.equal(meta.entries.length,ids.size);
const dims=meta.pages.map(f=>{const b=fs.readFileSync(new URL(f,art));return [b.readUInt32BE(16),b.readUInt32BE(20)];});
for(const e of meta.entries){
  assert.equal(e.sheet,4);
  const [width,height]=dims[e.page];
  for(const dir of ['FR','FL','BR','BL'])for(const action of ['idle','walk','highfive','hug',...Festival.actions.map(x=>x.id)]){
    for(const role of ['seated','guest'])for(const stage of [0,.3,.7,1]){
      const p={role,y:action==='surf'?1.2:0,festivalLead:action==='surf',festivalLiftReady:action==='surf',actionProgress:stage};
      const f=authoredFrame(p,{animationSeconds:stage},action,dir,true,e.sheet);
      let painted=0;
      assert(drawWholePatron({save(){},scale(){},restore(){},drawImage(im,x,y,w,h,dx,dy){assert(x>=0&&y>=0&&x+w<=width&&y+h<=height);assert.equal(dy,f.horizontal?-26:-49);painted++;}},{...e,image:{}},f));
      assert.equal(painted,1);
    }
  }
}
for(let owner=0;owner<3;owner++)for(let seed=1;seed<=30;seed++){
  const full=Audience.build({owner,attendance:Audience.capacity(owner),crowdSeed:seed,showId:'double'});
  assignPatronModels(full,owner,seed);
  const count=full.filter(a=>a.appearanceId!=null);
  assert.deepEqual(new Set(count.map(a=>a.appearanceId)),new Set(patronPools[owner]));
  assert.equal(count.length,patronPools[owner].length);
  const visitor={id:'assisted-guest',role:'assisted',assistedSourceId:count[0].id};
  assignPatronModels([...full,visitor],owner,seed);
  assert.equal(visitor.appearanceId,count[0].appearanceId);
  assert.equal(audienceIdentity({id:'bartender',role:'bartender',appearanceId:count[0].appearanceId,patronArtVersion:2},owner),null);
}
for(let owner=0;owner<3;owner++){
  const mem=new Map(),storage={getItem:k=>mem.get(k)??null,setItem:(k,v)=>mem.set(k,v)};
  const g=new SimBarGame({storage,seed:71});g.act('owner',{index:owner});g.act('start');
  launchPreparedDesk(g);
  while(g.phase==='guide')g.act('guide');
  assert.equal(g.phase,'playing');
  const models=r=>r.actors.filter(Audience.isAudience).map(a=>[a.id,a.appearanceId,a.patronArtVersion]);
  assert(g.act('festival',{id:'horns'}).success);
  g.journey.festival.arrivalCredit=1;g._festivalTick(.05);
  const before=models(g); g.save();
  const restored=new SimBarGame({storage});assert(restored.act('continue').success);
  assert.deepEqual(models(restored),before);
  assert.equal(restored.journey.festival.activeId,'horns');
}

const scheduledSurfer={role:'surfer',behavior:'surf',y:1.85};
const activeSurf={festivalAction:'surf'};
const surfFrame=(a,p={},action='surf')=>authoredFrame(a,p,action,'FR',true,4);
const timedFrame=surfFrame(scheduledSurfer);
assert.equal(timedFrame.clip,'surf');
assert.equal(timedFrame.horizontal,true);
assert.equal(surfFrame({...scheduledSurfer,y:0},{},'listen').clip,'idle');
assert.equal(surfFrame({role:'guest',festivalLead:true,festivalLiftReady:true,y:1},activeSurf).clip,'surf');
assert.equal(surfFrame({role:'guest',festivalLead:true,festivalLiftReady:false,y:1},activeSurf).clip,'horns');
assert.equal(surfFrame({role:'guest',y:1},activeSurf).clip,'horns');
assert.equal(surfFrame({role:'support',festivalSupport:true},activeSurf).clip,'support');
assert.equal(surfFrame({role:'seated',y:1},activeSurf).clip,'seatedReaction');
console.log('Native audience model metadata, frame bounds, festival actions, role safety, walk-ins and save migration passed.');
