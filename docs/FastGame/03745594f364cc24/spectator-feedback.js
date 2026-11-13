// Earned audience judgements. Service and concert experience are independent
// evidence, rather than two fixed rewards for the same button or order count.
const clamp=(value,min=0,max=1)=>Math.max(min,Math.min(max,Number.isFinite(+value)?+value:min));
const integer=(value,max=1e6)=>Math.trunc(clamp(value,0,max));
const ACTIONS=['slam','mosh','surf','jump','horns','airguitar','sing','encore','headbang','dance'];
const HARD=new Set(['slam','mosh','surf','headbang']);
const SOCIAL=new Set(['sing','dance','horns','airguitar']);
const ROLES=new Set(['guest','seated','support','surfer','assisted']);
const BUSY=new Set(['queue','return','arrive','to-stage','exit','escorted']);
const MAX_GUESTS=160;
const validId=id=>typeof id==='string'&&/^[a-zA-Z0-9_-]{1,80}$/.test(id)&&!['__proto__','constructor','prototype'].includes(id);
const hash=id=>{let n=2166136261;for(const c of id)n=Math.imul(n^c.charCodeAt(0),16777619)>>>0;return n>>>0;};
const ownerIndex=owner=>Number.isInteger(owner)&&owner>=0&&owner<3?owner:2;
function actualAudience(actors){
  const audience=new Map();
  for(const actor of Array.isArray(actors)?actors:[]){
    if(!actor||!ROLES.has(actor.role)||actor.visible===false||!validId(actor.id))continue;
    // Assistance temporarily replaces the physical body of an existing guest;
    // it is still that person's experience, not a new unserved attendee.
    const id=actor.role==='assisted'&&validId(actor.assistedSourceId)?actor.assistedSourceId:actor.id;
    audience.set(id,id===actor.id?actor:{...actor,id});
  }
  return [...audience.values()];
}
const freshGuest=()=>({orders:0,serviceTotal:0,enjoyedSeconds:0,frustration:0,discomfort:0,actions:{}});
function guest(state,id){
  if(!validId(id))return null;
  if(!Object.hasOwn(state.guests,id)){
    if(Object.keys(state.guests).length>=MAX_GUESTS)return null;
    state.guests[id]=freshGuest();
  }
  return state.guests[id];
}
function remember(state,actors){const audience=actualAudience(actors);for(const actor of audience)guest(state,actor.id);return audience;}
function actionFit(actor,id,state,context){
  const taste=hash(actor.id)%997/997;
  let fit=.86+(.5-taste)*.24;
  if(HARD.has(id)){
    fit*=state.owner===1?1.14:state.owner===0?.92:.40;
    if(['listen','sway'].includes(actor.behavior))fit*=.78;
    if(['mosh','slam','headbang','cheer'].includes(actor.behavior))fit*=1.12;
    if(actor.role==='seated'||actor.waitingSeat)fit*=.46;
    const softSet=context.showId==='july'||context.showId==='double'&&context.secondsLeft<=90;
    if(softSet)fit*=.62;
  }else if(SOCIAL.has(id)){
    if(state.owner===2)fit*=id==='horns'?.91:1.04;
    if(['listen','sway'].includes(actor.behavior)&&['sing','dance'].includes(id))fit*=1.08;
  }
  if(id==='jump'&&(actor.role==='seated'||actor.waitingSeat))fit*=.45;
  if(id==='encore'){
    const progress=1-clamp(context.secondsLeft??180,0,context.duration||180)/(context.duration||180);
    fit*=progress<.55?.16:progress<.78?.55:1.18;
  }
  // A surfer only earns the lifted experience after the support formation
  // makes physical contact. Other observers can enjoy their own raised hands.
  if(id==='surf'){
    if(actor.festivalLead&&!actor.festivalLiftReady)return 0;
    if(actor.festivalSupport&&!context.surfReady)return 0;
    if(!actor.festivalLead&&!actor.festivalSupport)fit*=.65;
  }
  const occupancy=clamp(context.crowdCount/Math.max(1,context.capacity||80));
  if(HARD.has(id))fit*=1-.35*clamp((occupancy-.82)/.18);
  fit*=1+.035*integer(context.soundLevel,3);
  fit*=1-.14*clamp(context.floorLitter/50);
  if(context.guestAssistanceActive&&HARD.has(id))fit*=.65;
  return clamp(fit,0,1.25);
}
const serviceValue=g=>g.orders?(1-Math.exp(-g.orders/.9))*clamp(g.serviceTotal/g.orders):0;
function guestScores(g){
  const service=serviceValue(g),concert=1-Math.exp(-g.enjoyedSeconds/36);
  const performance=clamp(concert*(.9+.1*service)-g.frustration*.22-g.discomfort*.16);
  const satisfaction=clamp(service*.86+performance*.14*(1-g.frustration*.5)-g.frustration*.30-g.discomfort*.12);
  return{satisfaction,performance};
}

export const SpectatorFeedback=Object.freeze({
  version:1,
  create({session=0,owner=2}={}){
    return{version:1,session:integer(session,1e9),owner:ownerIndex(owner),guests:{},recent:[],lastSerial:0,lastAction:'',lastParticipation:0,lastReason:'no-experience'};
  },
  sanitize(value,{session,owner}={}){
    const next=this.create({session:session??value?.session,owner:owner??value?.owner});
    if(!value||value.version!==1||Number.isInteger(session)&&value.session!==session||Number.isInteger(owner)&&value.owner!==owner)return next;
    for(const [id,raw]of Object.entries(value.guests||{}).slice(0,MAX_GUESTS)){
      if(!validId(id)||!raw||typeof raw!=='object')continue;
      const orders=integer(raw.orders),actions={};
      for(const id of ACTIONS)if(raw.actions?.[id]>0)actions[id]=clamp(raw.actions[id],0,3600);
      next.guests[id]={orders,serviceTotal:clamp(raw.serviceTotal,0,orders),enjoyedSeconds:clamp(raw.enjoyedSeconds,0,3600),
        frustration:clamp(raw.frustration),discomfort:clamp(raw.discomfort),actions};
    }
    next.recent=(Array.isArray(value.recent)?value.recent:[]).filter(id=>ACTIONS.includes(id)).slice(-6);
    next.lastSerial=integer(value.lastSerial);next.lastAction=ACTIONS.includes(value.lastAction)?value.lastAction:'';
    next.lastParticipation=clamp(value.lastParticipation);
    next.lastReason=['no-experience','service-good','service-disappointing','wrong-item','timeout','unsafe','participation','poor-fit','waiting'].includes(value.lastReason)?value.lastReason:'no-experience';
    return next;
  },
  service(state,{guestId,item,quality=.7,automatic=false,patience,maxPatience,preferred=false,preferredItems,crowdCount=0,capacity=80}={}){
    const g=guest(state,guestId);if(!g)return false;
    quality=clamp(quality);
    const trait=hash(guestId),wait=clamp(1-clamp(patience,0,Math.max(1,maxPatience||1))/Math.max(1,maxPatience||1));
    const late=clamp((wait-.2)/.8),waitSensitivity=.85+(trait%101)/250;
    const qualityExperience=quality>=.5?.60+.40*(quality-.5)/.5:quality*.30;
    const favorite=preferred===true||Array.isArray(preferredItems)&&preferredItems.includes(item);
    const occupancy=clamp(crowdCount/Math.max(1,capacity));
    const personalPreference=Number.isInteger(item)&&item===trait%6?.035:0;
    const experience=clamp(qualityExperience+(favorite?.06:0)+personalPreference-(automatic?.015:0)
      -.62*Math.pow(late,1.2)*waitSensitivity-.07*clamp((occupancy-.88)/.12));
    g.orders++;g.serviceTotal+=experience;
    if(experience>=.55)g.frustration*=.45;
    else g.frustration=clamp(g.frustration+(.55-experience)*.22);
    state.lastReason=experience>=.55?'service-good':'service-disappointing';
    return true;
  },
  disappoint(state,{guestId,reason='wrong-item',severity=1}={}){
    const g=guest(state,guestId);if(!g)return false;
    const weight=reason==='timeout'?.55:reason==='unsafe'?.34:.22;
    g.frustration=clamp(g.frustration+weight*clamp(severity)*(.88+(hash(guestId)%101)/400));
    state.lastReason=['wrong-item','timeout','unsafe'].includes(reason)?reason:'wrong-item';
    return true;
  },
  tick(state,dt,context={}){
    if(context.paused||!Number.isFinite(dt)||dt<=0)return;
    dt=Math.min(5,dt);
    const actors=remember(state,context.actors),crowdCount=actors.length;
    for(const order of Array.isArray(context.serviceQueue)?context.serviceQueue:[]){
      const g=state.guests[order.id];if(!g)continue;
      const waited=1-clamp(order.patience,0,Math.max(1,order.maxPatience||1))/Math.max(1,order.maxPatience||1);
      if(waited>.5){g.frustration=clamp(g.frustration+dt*.006*(waited-.5)*2);state.lastReason='waiting';}
    }
    state.lastParticipation=0;
    const id=context.actionId;
    if(context.musicPlaying!==true||!ACTIONS.includes(id)||!crowdCount)return;
    const participants=actors.filter(a=>a.festivalAction===id&&!a.moving&&!BUSY.has(a.action));
    if(!participants.length)return;
    const serial=integer(context.actionSerial);
    if(state.lastSerial!==serial||state.lastAction!==id){
      state.recent.push(id);state.recent=state.recent.slice(-6);state.lastSerial=serial;state.lastAction=id;
    }
    let streak=0;for(let i=state.recent.length-1;i>=0&&state.recent[i]===id;i--)streak++;
    const repetition=Math.max(.25,Math.pow(.72,Math.max(0,streak-1)));
    const fitContext={...context,crowdCount,surfReady:actors.some(a=>a.festivalLead&&a.festivalLiftReady)};
    let enjoyed=0;
    for(const actor of participants){
      const g=state.guests[actor.id],fit=actionFit(actor,id,state,fitContext);if(!g)continue;
      const prior=g.actions[id]||0,distinct=Object.keys(g.actions).length+(prior?0:1);
      const noveltySeconds=24*Math.log((24+prior+dt)/(24+prior));
      const variety=Math.min(1.12,.88+distinct*.04);
      g.actions[id]=prior+dt;
      g.enjoyedSeconds=clamp(g.enjoyedSeconds+noveltySeconds*fit*repetition*variety,0,3600);
      if(fit<.5)g.discomfort=clamp(g.discomfort+(.5-fit)*dt*.025);
      if(fit>.5)enjoyed++;
    }
    state.lastParticipation=enjoyed/crowdCount;
    state.lastReason=enjoyed>participants.length*.5?'participation':'poor-fit';
  },
  scores(state,{actors}={}){
    if(!state||state.version!==1)return{satisfaction:0,performance:0,serviceCoverage:0,participation:0,audienceCount:0,reason:'no-experience'};
    // Remember real arrivals, but retain the night's witnesses after they leave.
    // Post-show choreography must not make the earned score vanish or jump up.
    if(actors)remember(state,actors);
    const guests=Object.values(state.guests),count=guests.length;
    let satisfaction=0,performance=0,served=0;
    for(const g of guests){const values=guestScores(g);satisfaction+=values.satisfaction;performance+=values.performance;if(g.orders)served++;}
    return{satisfaction:count?clamp(satisfaction/count):0,performance:count?clamp(performance/count):0,
      serviceCoverage:count?served/count:0,participation:state.lastParticipation,audienceCount:count,reason:state.lastReason};
  }
});
