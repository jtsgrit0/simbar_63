import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {SimBarGame} from './WebGLTemplate/FastGame/core.js';
import {createAfterShow,tickAfterShow} from './WebGLTemplate/FastGame/aftershow.js';

// A nearby patron blocks the final 25.5 cm into the gathering mark. The fan
// should settle here and the drummer must approach this actual position.
{
  const mem=new Map(),storage={getItem:k=>mem.get(k)||null,setItem:(k,v)=>mem.set(k,String(v))};
  const g=new SimBarGame({storage,seed:12}),state=createAfterShow(g.actors,1);
  for(const a of g.actors)a.visible=false;
  for(const m of state.members)m.phase='gone';
  for(const item of state.audience)item.phase='done';
  const fan=g.actors.find(a=>a.id==='patron-15'),drummer=g.actors.find(a=>a.id==='drummer');
  Object.assign(fan,{visible:true,x:8,z:-4.6});Object.assign(drummer,{visible:true,x:7.1,z:-4.1,stageLevel:0});
  g.actors.push({id:'blocking-patron',role:'guest',visible:true,x:8.4,z:-4.6});
  const item=state.audience[7];item.phase='gather';
  const member=state.members[3];Object.assign(member,{phase:'approach',greeting:1});
  tickAfterShow(state,g.actors,.05);
  assert.equal(item.phase,'wait','Fan stalled within a safe gathering radius');
  assert.equal(fan.x,8);assert.equal(fan.z,-4.6);
  assert(Math.abs(Math.hypot(member.approachX-fan.x,member.approachZ-fan.z)-.43)<1e-6,'Approach ignored actual fan position');
}

const fixtures=['aftershow-floor-missions.json','aftershow-mid-gesture.json','aftershow-cleanup-stalled.json','aftershow-seed-3.json','aftershow-seed-10.json'];
const outcomes=[];
for(const fixture of fixtures){
  const mem=new Map(),storage={getItem:k=>mem.get(k)||null,setItem:(k,v)=>mem.set(k,String(v))};
  let g=new SimBarGame({storage});
  // These are untouched exports from actual V3 browser runs, including the
  // stopped cleanup and the guest task that previously never settled.
  g.importSave(readFileSync(new URL('./fixtures/'+fixture,import.meta.url),'utf8'));
  g=new SimBarGame({storage});g.act('continue');
  let seconds=0,guestReturned=false;
  const inputs={1:0,2:0};
  for(;seconds<180&&g.phase!=='results';seconds+=.05){
    const previous=new Map(g.actors.map(a=>[a.id,{x:a.x,z:a.z}]));
    const escort=g.escort;
    g.tick(.05);
    for(const a of g.actors){
      const p=previous.get(a.id);if(!p)continue;
      assert(Math.hypot(a.x-p.x,a.z-p.z)<=.19,`${fixture}: ${a.id} teleported`);
    }
    if(escort?.phase==='return'&&!g.escort){
      const worker=g.actors.find(a=>a.id===escort.worker);
      const cleaning=g.crew.includes(worker.id)&&g.floor.cleanupActive;
      const goal=cleaning?g._cleanGoal(g.crew.indexOf(worker.id)):{x:escort.homeX,z:escort.homeZ};
      assert(Math.hypot(worker.x-goal.x,worker.z-goal.z)<=.22,`${fixture}: escort cleared before worker returned`);
      guestReturned=true;
    }
    if(g.reaction.activeKind){const kind=g.reaction.activeKind;g.act('quick',{kind});inputs[kind]++;}
  }
  const outcome={fixture,seconds,phase:g.phase,inputs,guestReturned,escort:g.escort,
    floor:{cleaned:g.floor.cleaned,litter:g.floor.litter,cleanupComplete:g.floor.cleanupComplete,guestComplete:g.floor.guestComplete},
    staff:g.actors.filter(a=>a.id==='server'||a.id==='bartender').map(a=>({id:a.id,x:a.x,z:a.z,homeX:a.homeX,homeZ:a.homeZ,targetX:a.targetX,targetZ:a.targetZ,action:a.action})),
    members:g.journey.afterShow.members.map(m=>({id:m.id,phase:m.phase}))};
  outcomes.push(outcome);
  console.log(JSON.stringify(outcome));
  assert.equal(g.phase,'results',`${fixture}: replay did not reach results`);
  assert.equal(g.escort,null,`${fixture}: guest escort remains active`);
  assert.equal(g.floorTasksPending,false);
  assert(g.journey.afterShow.members.every(m=>m.phase==='gone'));
  if(fixture.startsWith('aftershow-seed-')){
    for(const fan of g.journey.afterShow.audience){const a=g.actors.find(a=>a.id===fan.id);assert(Math.hypot(a.x-fan.homeX,a.z-fan.homeZ)<=.201);assert.equal(a.moving,false);}
    for(const id of g.crew){const a=g.actors.find(a=>a.id===id);assert(Math.hypot(a.x-a._cleanupParkGoal.x,a.z-a._cleanupParkGoal.z)<=.05);assert.equal(a.moving,false);}
  }
  if(fixture==='aftershow-floor-missions.json')assert(guestReturned,'Saved guest task never returned its staff worker');
  const receipt=[g.state.money,g.journey.xp,g.journey.completedNights];
  g.save();const reloaded=new SimBarGame({storage});reloaded.act('continue');reloaded.tick(1);
  assert.deepEqual([reloaded.state.money,reloaded.journey.xp,reloaded.journey.completedNights],receipt);
}
console.log('PASS: actual browser save replays reach results with continuous movement, returned escort and once-only receipts');
