const clamp=(value,min,max)=>Math.max(min,Math.min(max,Number.isFinite(+value)?+value:min));
const action=(id,ko,en,icon,duration,cooldown,hype)=>Object.freeze({id,ko,en,icon,duration,cooldown,hype});

export const Festival=Object.freeze({
  version:1,
  actions:Object.freeze([
    action('slam','슬램','Slam','⚡',7,30,.15),
    action('mosh','모슁','Mosh','↻',9,34,.17),
    action('surf','크라우드 서핑','Crowd surf','≈',9,40,.18),
    action('jump','점프','Jump','↑',7,24,.13),
    action('horns','락손','Rock horns','🤘',8,22,.12),
    action('airguitar','에어기타','Air guitar','♫',8,28,.14),
    action('sing','떼창','Sing along','♪',10,32,.16),
    action('encore','앵콜','Encore','★',10,44,.18),
    action('headbang','헤드뱅','Headbang','↕',8,26,.14),
    action('dance','댄스','Dance','◈',9,26,.14)
  ]),
  create(){return{version:1,venueOwner:null,activeId:'',remaining:0,duration:0,globalRemaining:0,cooldowns:Object.fromEntries(this.actions.map(a=>[a.id,0])),hype:0,serial:0,actionsUsed:0,arrivalCredit:0,arrivalRetry:0,arrivals:0,arrivalSerial:0};},
  sanitize(value){
    const state={...this.create(),...(value&&typeof value==='object'?value:{})};
    state.version=1;state.activeId=this.actions.some(a=>a.id===state.activeId)?state.activeId:'';
    state.venueOwner=Number.isInteger(state.venueOwner)&&state.venueOwner>=0&&state.venueOwner<3?state.venueOwner:null;
    state.duration=clamp(state.duration,0,12);state.remaining=clamp(state.remaining,0,state.duration);
    if(!state.remaining)state.activeId='';
    state.globalRemaining=clamp(state.globalRemaining,0,12);
    state.cooldowns=Object.fromEntries(this.actions.map(a=>[a.id,clamp(state.cooldowns?.[a.id],0,a.cooldown)]));
    state.hype=clamp(state.hype,0,1);state.arrivalCredit=clamp(state.arrivalCredit,0,1);
    state.arrivalRetry=clamp(state.arrivalRetry,0,1);
    for(const key of ['serial','actionsUsed','arrivalSerial'])state[key]=Math.trunc(clamp(state[key],0,1e7));
    state.arrivals=Math.trunc(clamp(state.arrivals,0,80));
    return state;
  },
  response(satisfaction,performance){
    return Math.sqrt(clamp(satisfaction,0,1))*clamp(performance,0,1);
  },
  modifiers(state,satisfaction,performance){
    const earned=state.actionsUsed>0?state.hype*this.response(satisfaction,performance):0;
    return{response:this.response(satisfaction,performance),orderMultiplier:1/(1+.65*earned),cocktailBoost:.36*earned,arrivalRate:.28*earned};
  },
  tick(state,dt,satisfaction,performance){
    state.globalRemaining=Math.max(0,state.globalRemaining-dt);
    for(const action of this.actions)state.cooldowns[action.id]=Math.max(0,(state.cooldowns[action.id]||0)-dt);
    state.remaining=Math.max(0,state.remaining-dt);if(!state.remaining)state.activeId='';
    state.arrivalRetry=Math.max(0,(state.arrivalRetry||0)-dt);
    state.hype=Math.max(0,state.hype-dt*.004);
    if(state.actionsUsed>0)state.arrivalCredit=Math.min(1,state.arrivalCredit+dt*this.modifiers(state,satisfaction,performance).arrivalRate);
  },
  begin(state,id){
    const selected=this.actions.find(action=>action.id===id);
    if(!selected)return{success:false,code:'invalid_festival_action'};
    if(state.remaining>0||state.globalRemaining>0||(state.cooldowns[id]||0)>0)return{success:false,code:'festival_cooldown'};
    state.activeId=id;state.duration=selected.duration;state.remaining=selected.duration;
    state.globalRemaining=selected.duration;state.cooldowns[id]=selected.cooldown;
    state.hype=Math.min(1,state.hype+selected.hype);state.serial++;state.actionsUsed++;
    return{success:true,code:'festival_started',action:selected};
  }
});
