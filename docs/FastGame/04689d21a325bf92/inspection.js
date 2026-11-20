import {distance,walkable,setCrowdRoute,move,FREEBIRD_LAYOUT,FF_LAYOUT} from './navigation.js';

// A fictional venue interruption. Inspectors use the public right-hand aisle,
// outside the authored standing crowd, and never occupy an audience slot.
const ENTRY=Object.freeze({x:8.7,z:-5.4});
const POSTS=Object.freeze([-3,-3.8,-4.6].map(z=>Object.freeze({x:8.7,z})));
const PHASES=new Set(['scheduled','warning','patrol','exiting','done']);
const clamp=(n,a,b)=>Math.max(a,Math.min(b,Number.isFinite(+n)?+n:a));
const hash=text=>{let n=2166136261;for(const c of text)n=Math.imul(n^c.charCodeAt(0),16777619)>>>0;return n;};
const layoutFor=owner=>owner===0?FREEBIRD_LAYOUT:owner===1?FF_LAYOUT:null;
const inspector=id=>typeof id==='string'&&/^inspector-[0-2]$/.test(id);
const freshOfficer=index=>({id:`inspector-${index}`,index,status:'waiting',x:ENTRY.x,z:ENTRY.z,y:0,facing:0,visible:false,moving:false,_path:[],_pathAt:0});
const actorFor=(officer,owner)=>{
  const layout=layoutFor(owner);
  return{...officer,name:'District inspector '+(officer.index+1),role:'inspector',homeX:ENTRY.x,homeZ:ENTRY.z,
    targetX:Number.isFinite(officer.targetX)?officer.targetX:ENTRY.x,targetZ:Number.isFinite(officer.targetZ)?officer.targetZ:ENTRY.z,
    action:officer.action||(officer.moving?'walk':'listen'),layout:layout?.id,layoutVersion:layout?.version};
};
const fields=['status','action','x','z','y','facing','visible','moving','targetX','targetZ','_path','_pathAt','_floorRoute','_blocked','_crowdHistory','_inspectionRetry'];
const bodies=(actors,self)=>actors.filter(a=>a!==self&&a.visible!==false&&a.role!=='band');
function watchPosts(actors,owner){
  const layout=layoutFor(owner)?.id,occupied=bodies(actors),chosen=[];
  // The promoter and a restored legacy visitor can occupy this aisle. Pick
  // clear observation spots around their real bodies before freezing guests.
  for(const x of [8.7,8.05,7.4])for(const z of [-2.9,-3.65,-4.4,-5.05]){
    const p={x,z};
    if(!walkable(p,false,false,layout)||distance(p,ENTRY)<.6||occupied.some(a=>distance(a,p)<.5)||chosen.some(a=>distance(a,p)<.69))continue;
    chosen.push(p);if(chosen.length===3)return chosen.sort((a,b)=>distance(b,ENTRY)-distance(a,ENTRY));
  }
  return POSTS.map(p=>({...p}));
}

export const Inspection=Object.freeze({
  version:1,
  create({session=0,day=1,owner=0,seed=1}={}){
    return{version:1,session,phase:session?'scheduled':'done',triggerAt:55+hash(`${seed}:${day}:${session}:${owner}`)%36,
      serial:0,warningRemaining:0,patrolElapsed:0,holdRemaining:8,frozenSeconds:0,posts:[],officers:[]};
  },
  sanitize(value,context={}){
    const base=this.create(context),v=value&&typeof value==='object'?value:{};
    if(v.version!==1||v.session!==base.session)return base;
    const state={...base,...v,version:1,session:base.session};
    state.phase=PHASES.has(v.phase)?v.phase:base.phase;
    state.triggerAt=clamp(v.triggerAt,25,150);state.serial=Math.trunc(clamp(v.serial,0,1));
    state.warningRemaining=clamp(v.warningRemaining,0,3);state.patrolElapsed=clamp(v.patrolElapsed,0,1e5);
    state.holdRemaining=clamp(v.holdRemaining,0,8);state.frozenSeconds=clamp(v.frozenSeconds,0,1e6);
    state.posts=Array.isArray(v.posts)&&v.posts.length===3&&v.posts.every(p=>p&&Number.isFinite(p.x)&&Number.isFinite(p.z)&&Math.abs(p.x)<9.2&&Math.abs(p.z)<5.85)?v.posts.map(p=>({x:p.x,z:p.z})):POSTS.map(p=>({...p}));
    if(['warning','patrol','exiting'].includes(state.phase))state.serial=1;
    state.officers=state.serial?Array.from({length:3},(_,index)=>{
      const raw=Array.isArray(v.officers)?v.officers.find(o=>o?.id===`inspector-${index}`):null,officer={...freshOfficer(index),...raw,id:`inspector-${index}`,index};
      officer.status=['waiting','entering','watching','leaving','left'].includes(officer.status)?officer.status:'waiting';
      officer.x=clamp(officer.x,-9.4,9.1);officer.z=clamp(officer.z,-5.85,5.8);officer.y=0;
      officer.facing=clamp(officer.facing,-Math.PI*2,Math.PI*2);officer.visible=['entering','watching','leaving'].includes(officer.status);
      officer.moving=officer.visible&&officer.moving===true;
      officer._path=Array.isArray(officer._path)?officer._path.filter(p=>p&&Number.isFinite(p.x)&&Number.isFinite(p.z)&&Math.abs(p.x)<10&&Math.abs(p.z)<7).slice(0,300):[];
      officer._pathAt=Math.trunc(clamp(officer._pathAt,0,officer._path.length));
      officer.targetX=clamp(officer.targetX??ENTRY.x,-9.4,9.1);officer.targetZ=clamp(officer.targetZ??ENTRY.z,-5.85,5.8);
      if(state.phase==='done'){officer.status='left';officer.visible=false;officer.moving=false;}
      return officer;
    }):[];
    return state;
  },
  present(state,actors){return!!state&&['patrol','exiting'].includes(state.phase)&&actors.some(a=>a.role==='inspector'&&a.visible!==false);},
  view(state,actors){
    const officersPresent=actors.filter(a=>a.role==='inspector'&&a.visible!==false).length,active=this.present(state,actors);
    // Keep the public compatibility flag; only audience activity stops.
    // The stage performance and its presentation clock continue during patrol.
    return{phase:state?.phase||'done',active,freezePerformers:active,remaining:state?.phase==='warning'?state.warningRemaining:active?state.holdRemaining:0,
      triggerAt:state?.triggerAt||0,serial:state?.serial||0,officersPresent};
  },
  begin(state){
    if(!state||state.phase!=='scheduled'||state.serial)return false;
    state.phase='warning';state.serial=1;state.warningRemaining=3;state.patrolElapsed=0;state.holdRemaining=8;
    state.officers=Array.from({length:3},(_,index)=>freshOfficer(index));return true;
  },
  sync(state,actors){
    if(!state?.serial)return;
    for(const officer of state.officers){const actor=actors.find(a=>a.id===officer.id&&a.role==='inspector');if(actor)for(const field of fields)if(actor[field]!==undefined)officer[field]=actor[field];}
  },
  restore(state,actors,owner,savedActors){
    for(let i=actors.length-1;i>=0;i--)if(actors[i].role==='inspector'||inspector(actors[i].id))actors.splice(i,1);
    if(!state?.serial||!['patrol','exiting'].includes(state.phase))return;
    for(const officer of state.officers){
      const actor=actorFor(officer,owner);
      if(!walkable(actor,false,false,actor.layout)){actor.x=ENTRY.x;actor.z=ENTRY.z;actor._path=[];actor._pathAt=0;}
      actors.push(actor);
    }
    if(Array.isArray(savedActors)){
      const order=new Map(savedActors.map((a,i)=>[a.id,i]));actors.sort((a,b)=>(order.get(a.id)??1e6)-(order.get(b.id)??1e6));
    }
  },
  requestExit(state,actors){
    if(!state||state.phase==='done')return;
    if(!this.present(state,actors)){state.phase='done';for(const a of actors.filter(a=>a.role==='inspector')){a.visible=false;a.status='left';}return;}
    state.phase='exiting';state.holdRemaining=0;
    for(const a of actors.filter(a=>a.role==='inspector'&&a.status==='waiting')){a.status='left';a.visible=false;}
    this.sync(state,actors);
  },
  tick(state,dt,{actors,owner,playing,showElapsed,canEnter=true}={}){
    const notices=[],wasPresent=this.present(state,actors);
    if(state.phase==='scheduled'&&playing&&showElapsed>=state.triggerAt&&this.begin(state))notices.push('warning');
    if(state.phase==='warning'){
      state.warningRemaining=Math.max(0,state.warningRemaining-dt);
      if(!state.warningRemaining&&canEnter){state.phase='patrol';state.posts=watchPosts(actors,owner);this.restore(state,actors,owner);}
    }
    if(state.phase==='patrol'||state.phase==='exiting'){
      state.patrolElapsed+=dt;
      const officers=actors.filter(a=>a.role==='inspector').sort((a,b)=>a.index-b.index);
      for(const a of officers){
        if(state.phase==='patrol'&&a.status==='waiting'&&state.patrolElapsed>=a.index*1.1&&bodies(actors,a).every(b=>distance(b,ENTRY)>=.48)){
          a.x=ENTRY.x;a.z=ENTRY.z;a.y=0;a.visible=true;a.status='entering';a.action='walk';
          setCrowdRoute(a,state.posts[a.index],actors,false);
        }
        if(state.phase==='exiting'&&a.visible&&a.status!=='leaving'&&officers.every(other=>other.index<=a.index||other.status==='left')){
          a.status='leaving';setCrowdRoute(a,ENTRY,actors,false);
        }
        if(a.status==='entering'||a.status==='leaving'){
          const goal=a.status==='leaving'?ENTRY:state.posts[a.index];
          a._inspectionRetry=Math.max(0,(a._inspectionRetry||0)-dt);
          if((!a._path?.length||a._pathAt>=a._path.length)&&distance(a,goal)>.15&&!a._inspectionRetry){setCrowdRoute(a,goal,actors,false);a._inspectionRetry=.45;}
          move(a,dt,1.35,actors);a.action=a.moving?'walk':'listen';
          if(distance(a,goal)<=.15){
            a._path=[];a._pathAt=0;a.moving=false;
            if(a.status==='leaving'){a.status='left';a.visible=false;}else{a.status='watching';a.action='listen';}
          }
        }
        if(a.status==='watching'){a.moving=false;a.action='listen';a.facing=Math.atan2(5.75-a.x,3.5-a.z);}
      }
      if(state.phase==='patrol'&&officers.length===3&&officers.every(a=>a.status==='watching')){
        state.holdRemaining=Math.max(0,state.holdRemaining-dt);if(!state.holdRemaining)this.requestExit(state,actors);
      }
      this.sync(state,actors);
      if(state.phase==='exiting'&&officers.every(a=>a.status==='left')){
        state.phase='done';for(let i=actors.length-1;i>=0;i--)if(actors[i].role==='inspector')actors.splice(i,1);notices.push('done');
      }
    }
    const isPresent=this.present(state,actors);
    if(!wasPresent&&isPresent)notices.push('arrived');
    if(wasPresent||isPresent)state.frozenSeconds+=dt;
    return notices;
  }
});
