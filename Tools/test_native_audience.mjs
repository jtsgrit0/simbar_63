import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {SimBarGame,Booking,catalog} from './WebGLTemplate/FastGame/core.js';
import {Audience} from './WebGLTemplate/FastGame/audience.js';
import {walkable} from './WebGLTemplate/FastGame/navigation.js';
import {pixelAudiencePose,pixelStageLightingProfile} from './WebGLTemplate/FastGame/world.js';
import {bandPerformanceProfile,performingRoster,performingFrame} from './WebGLTemplate/FastGame/band-models.js';

const memory=()=>{const data=new Map();return{getItem:key=>data.get(key)??null,setItem:(key,value)=>data.set(key,String(value))};};
const crowd=g=>g.actors.filter(a=>Audience.isAudience(a)&&a.visible!==false);
const fixedSnapshot=g=>g.actors.filter(Audience.isAudience).map(a=>({id:a.id,role:a.role,visible:a.visible,x:a.x,z:a.z,y:a.y,
  homeX:a.homeX,homeZ:a.homeZ,behavior:a.behavior,phase:a.behaviorPhase,speed:a.animationSpeed})).sort((a,b)=>a.id.localeCompare(b.id));
function launch(owner,{showId='freewill',completedNights=0,fans=0,day=completedNights+1,tier=1,promoted=false,upgrades=[0,0,0],seed=11,legacy=false}={}){
  const g=new SimBarGame({storage:memory(),seed});
  assert.equal(g.act('owner',{index:owner}).success,true);
  Object.assign(g.journey,{day,completedNights});g.state.fans=fans;g.state.upgrades=upgrades.slice();
  if(legacy)g.journey.booking=Booking.newDeskPlan(day,owner===2?1:0);
  assert.equal(g.act('start').success,true);
  assert.equal(g.act(legacy?'deskSelect':'selectShow',legacy?{selected:true}:{showId}).success,true);
  if(tier!==1)assert.equal(g.act('ticket',{tier}).success,true);
  if(promoted)assert.equal(g.act('promote').success,true);
  assert.equal(g.act('deskWork',{task:0}).success,true);
  assert.equal(g.act('deskWork',{task:1}).success,true);
  const forecast=structuredClone(g.metrics());
  assert.equal(g.act('launch').success,true);
  while(g.phase==='guide')assert.equal(g.act('guide').success,true);
  return{g,forecast};
}
function assertSafePopulation(g){
  const people=crowd(g),metrics=g.metrics(),positions=new Set();
  assert.equal(people.length,metrics.attendance,`${g.owner}/${metrics.showId}: visible audience does not match the paid forecast`);
  assert.equal(metrics.audienceVersion,1);assert.equal(metrics.venueOwner,g.owner);
  for(const actor of people){
    assert.equal(actor.audienceVersion,1);
    const position=[actor.x.toFixed(3),actor.z.toFixed(3),(actor.y||0).toFixed(3)].join(',');
    assert(!positions.has(position),`${actor.id}: duplicate audience position`);positions.add(position);
    if(actor.role!=='seated')assert(walkable(actor,false,false,actor.layout),`${g.owner}/${actor.id}: audience starts in solid furniture`);
  }
  for(let i=0;i<people.length;i++)for(let j=i+1;j<people.length;j++){
    const a=people[i],b=people[j];
    assert(Math.hypot(a.x-b.x,a.z-b.z)>=.38,`${g.owner}: ${a.id}/${b.id} overlap at the beginning of the show`);
  }
  assert(g.orders.every(order=>people.some(actor=>actor.id===order.id&&actor.role==='guest')),'The opening order queue includes a hidden/non-ordering spectator');
}
function restore(g){g.save();const restored=new SimBarGame({storage:g.storage});restored.act('continue');return restored;}

// The same opening show attracts distinct venue populations and those exact
// people are present in the scene. This checks the live integration rather
// than just the audience factory's requested array length.
const openings=[0,1,2].map(owner=>launch(owner));
assert.deepEqual(openings.map(({forecast})=>forecast.attendance),[30,36,24]);
assert.deepEqual(openings.map(({g})=>g.metrics().venueCapacity),[64,80,44]);
for(const {g,forecast}of openings){
  assertSafePopulation(g);assert.equal(crowd(g).length,forecast.attendance);
  const before=fixedSnapshot(g),loaded=restore(g);
  assert.deepEqual(fixedSnapshot(loaded),before,'Loading changed audience membership, positions or behavior phases');
  assert.equal(loaded.metrics().attendance,forecast.attendance);
  g.state.fans+=100;g.tick(.1);
  assert.equal(g.metrics().attendance,forecast.attendance,'Live fan gain rewrote the locked ticket/crowd contract');
  assert.equal(crowd(g).length,forecast.attendance);
}

// The paid room is part of an active contract. A different local preference
// cannot import FF's ticket receipt into another room or alter its crowd.
{
  const {g}=launch(1),before=fixedSnapshot(g),wrongPreference=memory();
  assert.equal(g.act('owner',{index:0}).success,false);assert.equal(g.owner,1);
  wrongPreference.setItem('SimBar.Owner',catalog.owners[0].id);
  const loaded=new SimBarGame({storage:wrongPreference});
  assert.equal(loaded.importSave(g.exportSave()),true);loaded.act('continue');
  assert.equal(loaded.owner,1);assert.equal(loaded.metrics().venueOwner,1);
  assert.deepEqual(fixedSnapshot(loaded),before);
  assert.equal(crowd(loaded).length,36);
}

// Test an adversarial queue fixture: only an extra, non-patron-ID spectator
// remains eligible. Hidden original patrons must not receive timed orders.
{
  const {g}=launch(1),extra=g.actors.find(a=>a.audienceVersion===1&&a.role==='guest'&&a.id.startsWith('audience-'));
  assert(extra);g.orders=[];
  for(const actor of g.actors)if(actor.role==='guest')actor.visible=actor===extra;
  g.tick(5);
  assert(g.orders.length>0,'Extra audience members were excluded from the actual timed order pool');
  assert(g.orders.every(order=>order.id===extra.id));
}

// Price, promotion and later shows change the actual population together with
// the forecast, including maximum-capacity crowds in every authored room.
const variations=[];
for(const owner of [0,1,2]){
  const base=openings[owner].forecast.attendance;
  const low=launch(owner,{tier:0,promoted:true}),high=launch(owner,{tier:2});
  assert(low.forecast.attendance>base);assert(high.forecast.attendance<base);
  for(const entry of [low,high])assertSafePopulation(entry.g);
  const later=launch(owner,{showId:'double',completedNights:2,fans:150,upgrades:[0,3,0],tier:0,promoted:true,seed:27});
  assertSafePopulation(later.g);assert.equal(later.forecast.attendance,Audience.capacity(owner));
  variations.push({owner,base,low:low.forecast.attendance,high:high.forecast.attendance,full:later.forecast.attendance});
}

// A compact listening venue never borrows the other rooms' pit choreography.
// July's softer crowd also retains that behavior in both energetic venues.
for(const owner of [0,1,2])for(const showId of ['freewill','july']){
  const {g}=launch(owner,{showId,completedNights:showId==='july'?1:0,seed:19});
  if(owner===2||showId==='july'){
    assert(crowd(g).every(a=>!['mosh','slam','support','surf'].includes(a.behavior)));
    for(let frame=0;frame<120;frame++){
      g.tick(.1);
      assert(crowd(g).every(a=>!['mosh','slam','support','surf'].includes(a.action)),`${owner}/${showId} acquired unrelated pit choreography`);
    }
  }
  for(const order of g.orders)assert(g.actors.some(a=>a.id===order.id&&a.visible!==false));
}

// Crowds use distinct animation phases within a venue, and each room/band has
// a different performance/lighting profile. Actual renderer outputs are used
// so behavior metadata cannot pass while presentation stays synchronized.
const manifest=JSON.parse(readFileSync(new URL('./WebGLTemplate/FastGame/art/band-models.json',import.meta.url),'utf8'));
const lighting=[],performance=[],motions=[];
for(const {g}of openings){
  const state={owner:g.owner,playlist:g.state.playlist,phase:'playing',journey:g.journey,actors:g.actors};
  const people=crowd(g).filter(a=>['listen','sway','cheer','headbang'].includes(a.behavior)).slice(0,10);
  const poses=people.map(a=>pixelAudiencePose({...a,action:a.behavior,moving:false},state,7));
  assert(new Set(poses.map(p=>p.animationSeconds.toFixed(5))).size>=5,'Audience sprite frames still share one clock');
  assert(poses.some((p,index)=>JSON.stringify(p)!==JSON.stringify(pixelAudiencePose({...people[index],action:people[index].behavior,moving:false},state,8))));
  lighting.push(pixelStageLightingProfile(state));performance.push(bandPerformanceProfile(state));
  motions.push(poses.map(p=>[p.action,p.animationSeconds,p.sway,p.bob]));
  const roster=performingRoster(state,manifest);assert(roster.length>0);
  assert(new Set(roster.map(member=>member.performancePhase)).size>1,'All band members share a phase');
  assert(roster.some(member=>JSON.stringify(performingFrame(member,manifest,7))!==JSON.stringify(performingFrame(member,manifest,7.3))));
  assert(pixelStageLightingProfile({...state,playlist:1}).energy<lighting.at(-1).energy);
}
assert.equal(new Set(lighting.map(value=>JSON.stringify(value))).size,3);
assert.equal(new Set(performance.map(value=>JSON.stringify(value))).size,3);
assert.equal(new Set(motions.map(value=>JSON.stringify(value))).size,3);

// Historical contracts without the marker keep their authored crowd and paid
// forecast even if those two numbers differ. Only a new contract opts in.
for(const owner of [0,1,2]){
  const {g:legacy}=launch(owner,{legacy:true}),before=fixedSnapshot(legacy),frozen=structuredClone(legacy.journey.booking.lockedMetrics);
  const loaded=restore(legacy);
  assert.deepEqual(fixedSnapshot(loaded),before);assert.deepEqual(loaded.journey.booking.lockedMetrics,frozen);
  assert.equal(loaded.journey.booking.deskVersion,1);
  const {g:current}=launch(owner),raw=JSON.parse(current.exportSave()),j=JSON.parse(raw.journeyJson);
  delete j.booking.lockedMetrics.audienceVersion;delete j.booking.lockedMetrics.venueOwner;
  raw.journeyJson=JSON.stringify(j);raw.native=JSON.parse(legacy.exportSave()).native;
  const oldCampaign=new SimBarGame({storage:current.storage});
  assert.equal(oldCampaign.importSave(raw),true);oldCampaign.act('continue');
  assert.equal(oldCampaign.journey.booking.lockedMetrics.audienceVersion,undefined);
  assert.deepEqual(fixedSnapshot(oldCampaign),before,'An unmarked old campaign acquired a different audience');
  assert.equal(oldCampaign.metrics().attendance,j.booking.lockedMetrics.attendance);
}

// Run a real whole night and the existing farewell/cleanup barriers per room.
// Every actual customer leaves, including table/wall listeners and the dynamic
// crowd. Save/continue cannot resurrect anyone. Receipts remain based on lock.
const receipts=[];
const closingCases=[...[0,1,2].map(owner=>({owner,options:{seed:17+owner},scenario:'opening'})),
  {owner:1,options:{showId:'double',completedNights:2,fans:150,upgrades:[0,3,0],tier:0,promoted:true,seed:27},scenario:'full-capacity-double'}];
for(const {owner,options,scenario}of (process.env.SIMBAR_AUDIENCE_DENSE_ONLY?closingCases.slice(-1):closingCases)){
  let {g,forecast}=launch(owner,options),checkedExitedSave=false;
  for(let frame=0;frame<4500&&g.phase!=='results';frame++){
    if(g.farewellPending)g.act('farewellAck');
    if(g.reaction?.activeKind)g.act('quick',{kind:g.reaction.activeKind});
    if(g.playing){
      assert(g.orders.every(order=>g.actors.some(a=>a.id===order.id&&a.visible!==false)),'A hidden audience member placed an order');
      if(owner===2)assert(crowd(g).every(a=>!['mosh','slam','support','surf'].includes(a.action)));
    }
    g.tick(.1);
    if(process.env.SIMBAR_AUDIENCE_PROGRESS&&frame%100===0)console.log('AUDIENCE PROGRESS',JSON.stringify({frame,phase:g.phase,seconds:Math.round(g.elapsed),departure:g.journey.afterShow?.departureStarted,members:g.journey.afterShow?.members.map(m=>[m.id,m.phase]),visible:crowd(g).length}));
    if(g.closing&&!checkedExitedSave){
      const gone=[...(g.journey.afterShow?.members||[]),...(g.journey.afterShow?.audience||[])].filter(item=>item.phase==='gone');
      const actual=gone.filter(item=>g.actors.some(a=>a.id===item.id&&a.visible===false));
      if(actual.length){
        const before=fixedSnapshot(g);g=restore(g);
        assert.deepEqual(fixedSnapshot(g),before,'Closing reload moved or resurrected audience');
        for(const item of actual)assert.equal(g.actors.find(a=>a.id===item.id).visible,false);
        checkedExitedSave=true;
      }
    }
  }
  assert.equal(g.phase,'results',`${owner}: the room's audience blocked closing settlement`);
  assert.equal(crowd(g).length,0,`${owner}: actual audience actors remained visible after the farewell`);
  assert(checkedExitedSave);assert.equal(g.floorTasksPending,false);
  assert.equal(g.journey.resultAttendance,forecast.attendance);
  const wallet=g.cash,nights=g.journey.completedNights,after=restore(g);
  assert.equal(after.cash,wallet);assert.equal(after.journey.completedNights,nights);
  assert.equal(crowd(after).length,0,'Reviewing a settled receipt resurrected customers');
  const membership=game=>fixedSnapshot(game).map(({x,z,y,...identity})=>identity);
  assert.deepEqual(membership(after),membership(g),'Reviewing a settled receipt resurrected or replaced audience members');
  receipts.push({owner,scenario,attendance:g.journey.resultAttendance,cash:g.cash,seconds:Math.round(g.elapsed)});
  console.log('AUDIENCE NIGHT',JSON.stringify(receipts.at(-1)));
}

console.log(JSON.stringify({passed:true,checks:['three venue counts match locked forecast','price/promotion/later-night and full-capacity population','safe unique audience placement','stable active and closing save snapshots','active venue lock and cross-profile import','extra audience can order while hidden guests cannot','original/July crowd choreography','distinct crowd/band/light timing','unmarked historical contract preservation','three venue and dense-double real closing settlements'],variations,receipts},null,2));
