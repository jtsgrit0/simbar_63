import {clamp,int} from './booking.js';
const f=Math.fround;
export const Challenges={None:0,Cleanup:1,Guest:2,GuestPresses:8,GuestSeconds:6,CleanupPresses:10,CleanupSeconds:7,CleanupPulseWork:32,MinimumPressInterval:.06,
  newState(seed,elapsed=0){
    const s={version:1,seed:(seed&0x7fffffff)||1,paperCount:0,paperFired:0,paperKind0:0,paperKind1:0,paperKind2:0,paperAt0:0,paperAt1:0,paperAt2:0,
      guestScheduled:false,guestFired:false,guestAt:0,activeKind:0,activeStage:0,presses:0,secondsLeft:0,cooldown:0,cleanupWorkBudget:0,pressAge:0,timeouts:0,completedRounds:0};
    if(!Number.isFinite(elapsed)||elapsed>=165)return s;elapsed=Math.max(0,elapsed);let random=s.seed>>>0;
    const next=()=>random=(Math.imul(random,1664525)+1013904223)>>>0;
    const between=(a,b)=>f(a+f(f(b-a)*f((next()&0xffffff)/16777215)));
    const min=Math.max(20,elapsed+8);
    if(min<=135&&next()%100<80){
      s.paperCount=1+next()%3;let c=0,t=0,entries=[];
      for(let i=0;i<s.paperCount;i++){let kind=next()%2;if(c>=2)kind=1;else if(t>=2)kind=0;if(kind===0)c++;else t++;entries.push({kind,at:between(min,135)});}
      entries.sort((a,b)=>a.at-b.at);entries.forEach((x,i)=>{s['paperKind'+i]=x.kind;s['paperAt'+i]=x.at;});
    }
    const gmin=Math.max(40,elapsed+8);if(gmin<=140&&next()%100<75){s.guestScheduled=true;s.guestAt=between(gmin,140);}return s;
  },
  clear(s){s.activeKind=0;s.activeStage=0;s.presses=0;s.secondsLeft=0;s.pressAge=0;},
  sanitize(s){
    if(!s)return;s.version=1;s.seed=(s.seed&0x7fffffff)||1;s.paperCount=int(s.paperCount,0,3);s.paperFired=int(s.paperFired,0,s.paperCount);
    let c=0,t=0,previous=20;for(let i=0;i<s.paperCount;i++){
      const at=s['paperAt'+i],kind=s['paperKind'+i];if(kind===0)c++;else if(kind===1)t++;
      if(!Number.isFinite(at)||at<previous||at>135||(kind!==0&&kind!==1)||c>2||t>2){s.paperCount=Math.max(s.paperFired,i);break;}previous=at;
    }
    if(!Number.isFinite(s.guestAt)||s.guestAt<40||s.guestAt>140){s.guestScheduled=false;s.guestAt=0;}
    s.cooldown=clamp(s.cooldown,0,1.5);s.cleanupWorkBudget=clamp(s.cleanupWorkBudget,0,112);s.pressAge=clamp(s.pressAge,0,180);
    s.timeouts=int(s.timeouts);s.completedRounds=int(s.completedRounds);
    if(s.activeKind!==1&&s.activeKind!==2)this.clear(s);else{s.activeStage=int(s.activeStage,0,s.activeKind===2?2:1000);s.presses=int(s.presses,0,(s.activeKind===1?10:8)-1);s.secondsLeft=clamp(s.secondsLeft,0,s.activeKind===1?7:6);}
  },
  advance(s,elapsed,running=true){
    const out={confetti:0,tissue:0,guest:false};if(!s)return out;this.sanitize(s);if(!running||!Number.isFinite(elapsed)||elapsed<0)return out;
    while(s.paperFired<s.paperCount&&s['paperAt'+s.paperFired]<=elapsed){if(s['paperKind'+s.paperFired]===0)out.confetti++;else out.tissue++;s.paperFired++;}
    if(s.guestScheduled&&!s.guestFired&&s.guestAt<=elapsed){s.guestFired=true;out.guest=true;}return out;
  },
  start(s,kind,stage=0){if(!s||(kind!==1&&kind!==2))return false;this.sanitize(s);if(s.activeKind||s.cooldown>0)return false;s.activeKind=kind;s.activeStage=int(stage,0,kind===2?2:1000);s.presses=0;s.secondsLeft=kind===1?7:6;s.pressAge=.06;return true;},
  tick(s,dt,running=true){
    let out={timedOut:false,kind:0,stage:0};if(!s)return out;this.sanitize(s);if(!running||!Number.isFinite(dt)||dt<=0)return out;dt=Math.min(dt,180);
    s.cooldown=Math.max(0,s.cooldown-dt);s.pressAge=Math.min(180,s.pressAge+dt);if(!s.activeKind)return out;s.secondsLeft=Math.max(0,s.secondsLeft-dt);
    if(s.secondsLeft<=0){out={timedOut:true,kind:s.activeKind,stage:s.activeStage};this.clear(s);s.cooldown=1.5;s.timeouts=int(s.timeouts+1);}return out;
  },
  press(s,kind,running=true){
    const out={accepted:false,completed:false,kind:0,stage:0};if(!s)return out;this.sanitize(s);
    if(!running||!kind||kind!==s.activeKind||s.secondsLeft<=0||s.cooldown>0||s.pressAge+.000001<.06)return out;
    out.accepted=true;out.kind=kind;out.stage=s.activeStage;s.pressAge=0;s.presses++;
    if(s.presses<(kind===1?10:8))return out;out.completed=true;if(kind===1)s.cleanupWorkBudget=Math.min(112,s.cleanupWorkBudget+32);
    s.completedRounds=int(s.completedRounds+1);this.clear(s);s.cooldown=.5;return out;
  }
};
const fail=code=>({success:false,code,fanGain:0,cashDelta:0,energyGain:0});
const ok=(code,more={})=>({success:true,code,fanGain:0,cashDelta:0,energyGain:0,...more});
export const Floor={
  newState:()=>({version:1,confettiThrows:0,tissueThrows:0,litter:0,cleaned:0,finaleEnsured:false,cleanupStarted:false,cleanupActive:false,cleanupComplete:false,cleanupWorkers:0,cleanupRewardClaimed:false,
    guestSpawned:false,guestActive:false,guestComplete:false,guestStep:0,guestIndex:-1,guestSetbacks:0,reaction:null}),
  remaining:s=>s?int(Math.ceil(s.litter-s.cleaned),0,112):0,
  progress:s=>s?.litter>0?clamp(s.cleaned/s.litter,0,1):0,
  sanitize(s){
    if(!s)return;s.version=1;if(s.reaction)Challenges.sanitize(s.reaction);
    // Historical closing saves must not acquire more paper after any cleanup
    // has begun, including a completed zero-litter record normalized below.
    s.finaleEnsured=s.finaleEnsured===true||s.cleanupStarted===true||s.cleanupComplete===true||s.cleanupRewardClaimed===true||(Number.isFinite(+s.cleaned)&&+s.cleaned>0);
    s.confettiThrows=int(s.confettiThrows,0,2);s.tissueThrows=int(s.tissueThrows,0,2);s.litter=s.confettiThrows*24+s.tissueThrows*32;s.cleanupWorkers=int(s.cleanupWorkers,0,5);s.cleaned=clamp(s.cleaned,0,s.litter);
    if(s.cleanupRewardClaimed&&s.litter>0){s.cleanupStarted=true;s.cleaned=s.litter;}
    if(!s.cleanupStarted||s.litter===0){s.cleanupStarted=false;s.cleaned=0;s.cleanupComplete=false;s.cleanupActive=false;s.cleanupWorkers=0;}
    else{s.cleanupComplete=s.cleaned>=s.litter;s.cleanupActive=!s.cleanupComplete&&s.cleanupWorkers>0;}
    s.guestSetbacks=int(s.guestSetbacks,0,1000);
    if(s.guestComplete){s.guestSpawned=true;s.guestActive=false;s.guestStep=3;s.guestIndex=int(s.guestIndex,0,127);}
    else if(!s.guestSpawned){s.guestActive=false;s.guestStep=0;s.guestIndex=-1;}
    else{s.guestActive=true;s.guestStep=int(s.guestStep,0,2);s.guestIndex=int(s.guestIndex,0,127);}
  },
  throw(s,kind,playing){
    if(!playing)return fail('not_performing');if(kind!==0&&kind!==1)return fail('invalid_effect');if(s.cleanupStarted||s.cleanupComplete||s.cleanupRewardClaimed)return fail('cleanup_started');
    const key=kind===0?'confettiThrows':'tissueThrows';if(s[key]>=2)return fail('effect_limit');s[key]++;const litterAdded=kind===0?24:32;s.litter+=litterAdded;return ok(kind===0?'confetti':'tissue',{fanGain:2,energyGain:.04,litterAdded});
  },
  ensureFinale(s,eligible=true){
    if(!s||s.finaleEnsured===true)return[];
    s.finaleEnsured=true;
    if(!eligible||s.cleanupStarted||s.cleanupComplete||s.cleanupRewardClaimed||s.cleaned>0)return[];
    const bursts=[];
    for(const kind of [0,1]){
      const key=kind===0?'confettiThrows':'tissueThrows';
      if(s[key]>0)continue;
      const result=this.throw(s,kind,true);if(result.success)bursts.push({kind,...result});
    }
    return bursts;
  },
  startCleanup(s,workers,over){if(!over)return fail('performance_running');if(s.cleanupRewardClaimed||s.cleanupComplete)return fail('already_clean');if(!s.litter)return fail('no_litter');if(workers<1||workers>5)return fail('no_workers');if(s.cleanupActive)return fail('already_cleaning');s.cleanupStarted=true;s.cleanupActive=true;s.cleanupWorkers=workers;return ok('cleanup_started');},
  tick(s,dt,ready,running=true){
    if(!running)return fail('paused');if(!Number.isFinite(dt)||dt<=0)return fail('invalid_delta');if(!s.cleanupActive||s.cleanupComplete)return fail('not_cleaning');
    const workers=Math.min(s.cleanupWorkers,int(ready,0,5));if(!workers)return fail('no_workers');s.cleaned=Math.min(s.litter,s.cleaned+Math.min(dt,180)*workers*3);
    if(s.cleaned>=s.litter-.000001){s.cleaned=s.litter;s.cleanupComplete=true;s.cleanupActive=false;return ok('cleanup_complete',{cleanupFinished:true});}return ok('cleaning');
  },
  settle(s){if(s.cleanupRewardClaimed)return fail('already_settled');if(!s.cleanupStarted||!s.litter||!s.cleanupComplete||s.cleaned<s.litter)return fail('incomplete_cleanup');s.cleanupRewardClaimed=true;return ok('cleanup_reward',{cashDelta:70,fanGain:3});},
  spawnGuest(s,index,eligible=true){if(!eligible)return fail('guest_not_eligible');if(s.guestSpawned)return fail('guest_already_spawned');if(index<0||index>127)return fail('invalid_guest');s.guestSpawned=true;s.guestActive=true;s.guestStep=0;s.guestIndex=index;return ok('guest_arrived');},
  resolveGuest(s,choice){if(s.guestComplete)return fail('guest_already_complete');if(!s.guestActive)return fail('no_guest');if(choice<0||choice>3)return fail('invalid_choice');if(choice===3){s.guestStep=Math.max(0,s.guestStep-1);s.guestSetbacks=int(s.guestSetbacks+1,0,1000);return ok('deescalate');}if(choice!==s.guestStep)return fail('sequence_needed');s.guestStep++;if(s.guestStep<3)return ok(s.guestStep===1?'water_offered':'escort_offered');s.guestComplete=true;s.guestActive=false;return ok('guest_complete',{guestCompleted:true,fanGain:4});}
};
