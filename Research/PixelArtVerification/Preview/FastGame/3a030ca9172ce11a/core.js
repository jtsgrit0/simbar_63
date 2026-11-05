import {catalog} from './catalog.js';
import {createDailyPoster,DAILY_POSTER_INDEX} from './daily-poster.js';
import {Booking,clamp,int,count} from './booking.js';
import {Floor,Challenges} from './floor.js';
import {distance,walkable,reserved,setRoute,move} from './navigation.js';
export {catalog,Booking,Floor,Challenges};
export const SAVE_KEY='SimBar.Operations.v1';
const OWNER_KEY='SimBar.Owner',LANGUAGE_KEY='SimBar.Language';
const clone=value=>JSON.parse(JSON.stringify(value));
const finite=(n,fallback,a,b)=>Number.isFinite(+n)?clamp(+n,a,b):fallback;
const round=n=>{const f=Math.floor(n),r=n-f;return r===.5?(f%2===0?f:f+1):Math.round(n);};
const point=(x,z)=>({x,z});
const cleanStops=[point(2.2,-3.9),point(2.2,.75),point(8.45,.75),point(8.35,-3.8),point(4.2,-4.8),point(7.9,-4.8)];
const orderSlots=[point(-1.5,-1.75),point(-.85,-2.25),point(-.15,-2.7),point(.6,-3.1),point(1.4,-3.5),point(2.2,-3.7),point(3,-4)];
const defaults=()=>({version:1,money:12340,fans:256,satisfaction:.94,performance:.98,clock:18,playlist:0,pours:0,staff:[0,0,0],upgrades:[0,0,0],wageTimer:60,wageDebt:0,
  activeEvent:-1,eventRemaining:0,eventStartPours:0,eventFanTimer:0,cooldowns:[0,0,0],completedEvents:0,autoServeTimer:12,promoterTimer:0,journeyJson:''});
const newJourney=()=>({version:1,day:1,challenge:false,tutorialComplete:false,guideStep:0,guidePending:false,inProgress:false,session:0,settledSession:0,settlementPending:false,secondsLeft:180,
  score:0,combo:0,bestCombo:0,manualPerfects:0,orders:0,mistakes:0,successfulEvents:0,manualStreak:0,feverSeconds:0,feverCharge:0,openingCash:12340,drinkIncome:0,xp:0,bestScore:0,bestStars:0,completedNights:0,
  resultStars:0,resultServed:0,resultXP:0,resultBonus:0,resultProfit:0,resultNewRecord:false,booking:null,bookingCareer:Booking.newCareer(),dayOpeningCash:0,hasDayOpeningCash:false,resultTicketIncome:0,resultAttendance:0,
  floorMissions:Floor.newState(),floorClosing:false,resultFloorBonus:0,resultFloorTasks:0});
const messages={
  scouted:['출연진 발굴 완료. 팀을 선택하고 섭외를 진행하세요.','Lineup discovered. Select an artist and make an offer.'],
  listened:['데모 검토 완료. 제안 수락 확률이 올랐어요.','Demo reviewed. This artist is more likely to accept.'],
  accepted:['섭외 확정! 출연료를 지급했습니다.','Booking confirmed. The artist fee is paid.'],rejected:['오늘의 제안은 거절됐어요. 다른 팀에게 제안해 보세요.','The artist declined today. Try another newcomer.'],
  promoted:['홍보 완료. 예상 관객이 늘었어요.','Promotion complete. More guests are expected.'],insufficient_cash:['보유금이 부족합니다.','Not enough cash.'],no_time:['기획 시간이 끝났어요. 오늘의 밤을 열어 주세요.','Planning is over. Open tonight’s show.'],
  not_discovered:['먼저 포스터를 조사해 이 팀을 발굴해 주세요.','Research the artist’s poster first.'],already_discovered:['이미 발굴한 출연진입니다. 섭외 탭에서 제안할 수 있어요.','This lineup is already discovered. Make an offer in booking.'],
  unavailable:['지금은 이 행동을 진행할 수 없어요.','This action is unavailable right now.'],
};

/** A complete small web player. Rules keep the authored save schema and stable catalog IDs. */
export class SimBarGame{
  constructor({storage,seed}={}){
    try{this.storage=storage??globalThis.localStorage;}catch{this.storage=null;}
    this._seed=((seed??(Date.now()^Math.floor(Math.random()*0x7fffffff)))>>>0)||1;
    this.state=defaults();this.journey=newJourney();this.phase='lobby';this.paused=true;this.pauseReason='lobby';
    this.owner=0;this.language='ko';this.orders=[];this.pour=null;this.effects=[];this.actors=[];this.crew=[];this.escort=null;
    this.selectedItem=0;this.lastQuality=null;this.lastResult={success:true,code:'ready'};this._message=['',''];this._messageTimer=0;this.elapsed=0;this._autosave=0;this._requestTimer=4;this._inputGuard=0;this._eventSerial=0;this._loading=false;
    this._buildActors();
    this.owner=Math.max(0,catalog.owners.findIndex(o=>o.id===this._get(OWNER_KEY)));
    this.language=this._get(LANGUAGE_KEY)==='en'?'en':'ko';
    const raw=this._get(SAVE_KEY);if(raw)this.importSave(raw,{save:false});
    this.phase='lobby';this.paused=true;this.pauseReason='lobby';
    if(this.journey.settlementPending){this._settleNight();this.phase='lobby';this.paused=true;this.pauseReason='lobby';}
  }
  get floor(){return this.journey.floorMissions;}
  get reaction(){return this.floor.reaction;}
  get message(){return this._message[this.language==='en'?1:0]||'';}
  get cash(){return this.state.money;}
  get hasSave(){return!!(this.journey.inProgress||this.journey.settledSession||this._activePlan());}
  get cleanupReadyWorkers(){return this._readyWorkers().length;}
  get guestExitFinished(){return!this.floor.guestComplete||(!this.escort&&!this.actors.find(a=>a.id==='assisted-guest'&&a.visible));}
  get availableWorkers(){return 2+this.state.staff.reduce((n,v)=>n+(v&&this.state.wageDebt===0?1:0),0);}
  get payroll(){return this.state.staff.reduce((n,v,i)=>n+v*catalog.staff[i].wage,0);}
  get playing(){return this.phase==='playing'&&this.journey.inProgress;}
  get closing(){return this.phase==='closing'&&this.journey.inProgress;}
  get floorTasksPending(){return Floor.remaining(this.floor)>0||this.floor.guestActive||!this.guestExitFinished;}
  goalTargets(){return this.journey.challenge?[20,7,8]:[12,4,5];}
  goals(){return[this.journey.orders,this.journey.manualPerfects,this.journey.bestCombo];}
  bookingChance(band,premium=0){return Booking.chance(this.journey.booking,this.journey.bookingCareer,+band,+premium);}
  bookingFee(band,premium=0){return Booking.fee(+band,+premium);}
  isDiscovered(band){return Booking.discovered(this.journey.bookingCareer,+band);}
  currentPoster(poster=DAILY_POSTER_INDEX){return+poster===DAILY_POSTER_INDEX?createDailyPoster(this.journey.day,this.journey.booking?.venue||0):catalog.posters[+poster];}
  isPosterDiscovered(poster){return+poster===DAILY_POSTER_INDEX?Booking.dailyPosterDiscovered(this.journey.bookingCareer,this.journey.day,this.journey.booking?.venue||0):Booking.posterDiscovered(this.journey.bookingCareer,+poster);}
  metrics(){
    const p=this.journey.booking,m=Booking.forecast(p);
    return{...m,hour:10+2*(p?.blocks||0),blocks:p?.blocks||0,remainingBlocks:4-(p?.blocks||0),lineup:catalog.bands.filter(b=>p&&(p.bookedMask&(1<<b.index))),
      ticketTier:p?.ticketTier??1,venue:catalog.venues[p?.venue||0]};
  }
  text(ko,en){return this.language==='en'?en:ko;}
  formatMoney(value){return new Intl.NumberFormat(this.language==='ko'?'ko-KR':'en-US',{style:'currency',currency:'KRW',maximumFractionDigits:0}).format(value*1000);}
  _get(key){try{return this.storage?.getItem(key)??null;}catch{return null;}}
  _put(key,value){try{this.storage?.setItem(key,value);}catch{this.saveError=true;}}
  _random(){this._seed=(Math.imul(this._seed,1664525)+1013904223)>>>0;return this._seed/4294967296;}
  _say(ko,en,seconds=3){this._message=[ko,en];this._messageTimer=seconds;}
  _notify(){this.revision=(this.revision||0)+1;this.onchange?.(this);}
  save(){
    this.state.journeyJson=JSON.stringify(this.journey);
    this.state.native={version:1,seed:this._seed,elapsed:this.elapsed,orders:this.orders,actors:this.actors,crew:this.crew,escort:this.escort,requestTimer:this._requestTimer,selectedItem:this.selectedItem,eventSerial:this._eventSerial};
    this._put(SAVE_KEY,JSON.stringify(this.state));return!this.saveError;
  }
  exportSave(){this.save();return JSON.stringify(this.state);}
  importSave(raw,{save=true}={}){
    try{
      const data=typeof raw==='string'?JSON.parse(raw):clone(raw);if(!data||data.version!==1)return false;
      const ownerId=this._get(OWNER_KEY),savedOwner=catalog.owners.findIndex(o=>o.id===ownerId);if(savedOwner>=0)this.owner=savedOwner;
      const savedLanguage=this._get(LANGUAGE_KEY);if(savedLanguage==='en'||savedLanguage==='ko')this.language=savedLanguage;
      const base=defaults();this.state={...base,...data};
      for(const [key,a,b] of [['money',0,1e9],['fans',0,1e7],['satisfaction',.3,1],['performance',.35,1],['clock',18,24],['wageTimer',.1,60],['autoServeTimer',.1,12],['promoterTimer',0,20]])this.state[key]=finite(data[key],base[key],a,b);
      for(const [key,max]of[['staff',1],['upgrades',3]])this.state[key]=Array.from({length:3},(_,i)=>int(data[key]?.[i],0,max));
      this.state.cooldowns=Array.from({length:3},(_,i)=>clamp(data.cooldowns?.[i],0,600));this.state.wageDebt=int(data.wageDebt,0,this.payroll);
      this.state.activeEvent=int(data.activeEvent,-1,2);this.state.eventRemaining=clamp(data.eventRemaining,0,this.state.activeEvent<0?0:catalog.events[this.state.activeEvent].duration);if(!this.state.eventRemaining)this.state.activeEvent=-1;
      for(const key of ['pours','eventStartPours','completedEvents','eventFanTimer'])this.state[key]=int(data[key]);this.state.playlist=int(data.playlist,0,catalog.playlists.length-1);
      let j={};try{j=JSON.parse(data.journeyJson||'{}');}catch{}
      this.journey={...newJourney(),...j};this._sanitizeJourney();this._buildActors();this.orders=[];this.crew=[];this.escort=null;this.pour=null;this.effects=[];
      const n=data.native;
      if(n?.version===1&&this.journey.inProgress&&Array.isArray(n.actors)&&n.actors.length<100){
        const allowed=new Set(this.actors.map(a=>a.id));allowed.add('assisted-guest');
        const restored=n.actors.filter(a=>allowed.has(a.id)&&Number.isFinite(a.x)&&Number.isFinite(a.z)&&Math.abs(a.x)<20&&Math.abs(a.z)<20).map(a=>({...a,_path:Array.isArray(a._path)?a._path.filter(p=>Number.isFinite(p.x)&&Number.isFinite(p.z)).slice(0,300):[],_pathAt:int(a._pathAt,0,300)}));
        if(restored.length>=this.actors.length-3)this.actors=restored;
        this.orders=Array.isArray(n.orders)?n.orders.filter(o=>this.actors.some(a=>a.id===o.id)&&int(o.item,0,4)===o.item).slice(0,7).map(o=>({...o,patience:clamp(o.patience,0,200),maxPatience:clamp(o.maxPatience,1,200)})):[];
        this.crew=Array.isArray(n.crew)?n.crew.filter(id=>this.actors.some(a=>a.id===id)).slice(0,5):[];
        this.escort=n.escort&&this.actors.some(a=>a.id===n.escort.worker)?n.escort:null;
        this._seed=(n.seed>>>0)||this._seed;this.elapsed=clamp(n.elapsed,0,1e8);this._requestTimer=finite(n.requestTimer,3,0,10);this.selectedItem=int(n.selectedItem,0,4);this._eventSerial=int(n.eventSerial);
      }
      if(!this.orders.length&&this.journey.inProgress&&!this.journey.floorClosing)this._seedOrders();
      if(this.floor.guestSpawned&&!this.floor.guestComplete)this._ensureGuest();
      this.phase='lobby';this.paused=true;this.pauseReason='lobby';if(save)this.save();this._notify();return true;
    }catch{return false;}
  }
  _sanitizeJourney(){
    const j=this.journey;j.version=1;j.day=int(j.day,1,9999);j.session=int(j.session);j.settledSession=int(j.settledSession,0,j.session);j.secondsLeft=finite(j.secondsLeft,180,0,180);
    for(const key of ['score','combo','bestCombo','manualPerfects','orders','mistakes','successfulEvents','manualStreak','xp','bestScore','completedNights','resultServed','resultXP','resultBonus'])j[key]=int(j[key],0,1e8);
    j.bestCombo=Math.max(j.bestCombo,j.combo);j.orders=Math.max(j.orders,j.manualPerfects);j.guideStep=int(j.guideStep,0,2);j.feverSeconds=clamp(j.feverSeconds,0,12);j.feverCharge=clamp(j.feverCharge,0,1);j.resultStars=int(j.resultStars,0,3);j.bestStars=int(j.bestStars,0,3);
    j.openingCash=finite(j.openingCash,this.state.money,0,1e9);j.resultProfit=finite(j.resultProfit,0,-1e9,1e9);j.floorMissions={...Floor.newState(),...(j.floorMissions||{})};Floor.sanitize(j.floorMissions);
    j.bookingCareer={...Booking.newCareer(),...(j.bookingCareer||{})};if(j.booking)j.booking={...Booking.newPlan(j.day,0),...j.booking};Booking.sanitize(j.booking,j.bookingCareer);if(j.booking?.day!==j.day)j.booking=null;
    if(!j.session||j.settledSession>0&&j.session===j.settledSession){j.inProgress=false;j.settlementPending=false;j.floorClosing=false;}
    if(j.settlementPending&&(Floor.remaining(j.floorMissions)||j.floorMissions.guestActive)){j.settlementPending=false;j.inProgress=true;j.floorClosing=true;}
    if(j.floorClosing&&j.inProgress){j.secondsLeft=0;j.guidePending=false;j.settlementPending=false;}else j.floorClosing=false;
    j.resultFloorBonus=clamp(j.resultFloorBonus,0,70);j.resultFloorTasks=int(j.resultFloorTasks,0,2);if(j.tutorialComplete)j.guidePending=false;
  }
  act(type,p={}){
    let r={success:true,code:type};const j=this.journey,s=this.state,idx=+p.index;
    switch(type){
      case'owner':if(this.phase!=='lobby'||!Number.isInteger(idx)||idx<0||idx>2)return this._done(false);this.owner=idx;this._put(OWNER_KEY,catalog.owners[idx].id);break;
      case'language':this.language=p.language==='en'?'en':'ko';this._put(LANGUAGE_KEY,this.language);break;
      case'start':if(j.inProgress||this._activePlan())return this.act('continue');if(j.settledSession>0&&j.session===j.settledSession)j.day=int(j.day+1,1,9999);this._planning(!!p.challenge);break;
      case'continue':
        if(j.settlementPending)this._settleNight();
        if(this._activePlan()){this._planning(j.challenge);break;}
        if(j.inProgress){this._ensureReaction();this.phase=j.floorClosing||j.secondsLeft<=0?'closing':j.guidePending?'guide':'playing';this.paused=this.phase==='guide';this.pauseReason=this.paused?'guide':'';if(this.closing)this._beginClosing();}
        else if(j.settledSession){this.phase='results';this.paused=true;this.pauseReason='results';}else this._planning(j.challenge);break;
      case'next':if(this.phase!=='results'||j.inProgress)return this._done(false);j.day=int(j.day+1,1,9999);this._planning(j.challenge);break;
      case'retry':if(this.phase!=='results'||j.inProgress)return this._done(false);this._planning(j.challenge);break;
      case'lobby':this.pour=null;this.phase='lobby';this.paused=true;this.pauseReason='lobby';break;
      case'pause':if(this.playing||this.closing){this.paused=true;this.pauseReason=p.reason||'pause';}break;
      case'resume':if(this.playing||this.closing){this.paused=false;this.pauseReason='';}break;
      case'guide':if(this.phase!=='guide')return this._done(false);if(j.guideStep<2)j.guideStep++;else{j.tutorialComplete=true;j.guidePending=false;this.phase='playing';this.paused=false;this.pauseReason='';}break;
      case'venue':if(this.phase!=='planning'||j.booking.blocks>0||!Number.isInteger(+p.venue)||+p.venue<0||+p.venue>3)return this._done(false);j.booking.venue=+p.venue;break;
      case'scout':case'listen':case'offer':case'promote':
        if(this.phase!=='planning')return this._done(false);r=type==='scout'?(+p.poster===DAILY_POSTER_INDEX?Booking.scoutDaily(j.booking,j.bookingCareer):Booking.scout(j.booking,j.bookingCareer,+p.poster)):type==='listen'?Booking.listen(j.booking,j.bookingCareer,+p.band):type==='offer'?Booking.offer(j.booking,j.bookingCareer,+p.band,+p.premium||0,s.money):Booking.promote(j.booking,j.bookingCareer,s.money);
        if(r.success)s.money=clamp(s.money+r.cashDelta,0,1e9);this._say(...(messages[r.code]||messages.unavailable));s.clock=10+j.booking.blocks*2;break;
      case'ticket':if(this.phase!=='planning')return this._done(false);Booking.setTicket(j.booking,+p.tier);break;
      case'launch':if(this.phase!=='planning'||!this._activePlan())return this._done(false);Booking.lock(j.booking);this._newNight();break;
      case'menuItem':this.selectedItem=int(p.item,0,4);if(this.pauseReason==='menu'){this.paused=false;this.pauseReason='';}r=this._startPour(this.selectedItem);break;
      case'prepare':r=this._startPour(this.orders[0]?.item??this.selectedItem);break;
      case'pourStop':r=this._stopPour();break;
      case'hire':case'upgrade':case'event':case'payDebt':r=this._manage(type,idx);break;
      case'quick':r=this._quick(+p.kind);break;
      case'playlist':if(!Number.isInteger(idx)||idx<0||idx>=catalog.playlists.length)return this._done(false);s.playlist=idx;s.performance=Math.min(1,s.performance+.08);this._say('재생 중: '+catalog.playlists[idx].name,'Now playing: '+catalog.playlists[idx].nameEnglish);break;
      default:return this._done(false,'unknown_action');
    }
    this.lastResult=r;this.save();this._notify();return r;
  }
  _done(success,code='unavailable'){const r={success,code};this.lastResult=r;if(!success)this._say(...(messages[code]||messages.unavailable));this._notify();return r;}
  _activePlan(){const p=this.journey.booking;return!!(p&&!p.locked&&!p.completed&&p.day===this.journey.day);}
  _planning(challenge){const j=this.journey;if(j.inProgress)return;this.pour=null;if(!this._activePlan()){const previousVenue=j.booking?.venue??(this.owner===2?1:0);j.booking=Booking.newPlan(j.day,int(previousVenue,0,3));j.dayOpeningCash=this.state.money;j.hasDayOpeningCash=true;}j.challenge=challenge;this.phase='planning';this.paused=true;this.pauseReason='planning';this.state.clock=10+j.booking.blocks*2;}
  _newNight(){
    const j=this.journey;j.session=j.session>=1e9?1:j.session+1;if(j.session===1)j.settledSession=0;j.inProgress=true;j.settlementPending=false;j.floorClosing=false;j.secondsLeft=180;
    for(const k of ['score','combo','bestCombo','manualPerfects','orders','mistakes','successfulEvents','manualStreak','feverSeconds','feverCharge','drinkIncome','resultStars','resultServed','resultXP','resultBonus','resultProfit','resultTicketIncome','resultAttendance','resultFloorBonus','resultFloorTasks'])j[k]=0;
    j.resultNewRecord=false;j.openingCash=j.hasDayOpeningCash?j.dayOpeningCash:this.state.money;j.guideStep=0;j.guidePending=!j.tutorialComplete;j.floorMissions=Floor.newState();this._ensureReaction();
    this.state.clock=18;this.state.satisfaction=Math.max(this.state.satisfaction,j.challenge?.85:.94);this.state.performance=Math.max(this.state.performance,.92);
    this._buildActors();this.crew=[];this.escort=null;this.effects=[];this.pour=null;this._seedOrders();this._requestTimer=3*(j.challenge?.72:1.18)*this.metrics().orderIntervalMultiplier;
    this.phase=j.guidePending?'guide':'playing';this.paused=j.guidePending;this.pauseReason=this.paused?'guide':'';this._say('영업 시작! 주문과 공연을 멋지게 운영해 보세요.','The show is open! Serve guests and keep the crowd glowing.');
  }
  _ensureReaction(){if(!this.floor.reaction)this.floor.reaction=Challenges.newState(Math.floor(this._random()*0x7fffffff)||1,180-this.journey.secondsLeft);}
  _manage(type,index){
    const s=this.state;if(!this.playing)return{success:false,code:'unavailable'};
    if(type==='payDebt'){if(!s.wageDebt||s.money<s.wageDebt)return{success:false,code:'insufficient_cash'};s.money-=s.wageDebt;s.wageDebt=0;s.wageTimer=60;return{success:true,code:'wages_paid'};}
    if(!Number.isInteger(index)||index<0||index>2)return{success:false,code:'invalid_index'};
    if(type==='hire'){
      if(s.staff[index])return{success:false,code:'already_hired'};if(s.wageDebt)return{success:false,code:'wages_due'};if(s.money<catalog.staff[index].cost)return{success:false,code:'insufficient_cash'};
      s.money-=catalog.staff[index].cost;s.staff[index]=1;if(index===0)s.autoServeTimer=12;const a=this.actors.find(a=>a.id==='hired-'+index);a.visible=true;this._say(catalog.staff[index].name+' 고용 완료.',catalog.staff[index].nameEnglish+' hired.');return{success:true,code:'hired'};
    }
    if(type==='upgrade'){
      const level=s.upgrades[index];if(level>=3)return{success:false,code:'max_level'};const price=catalog.upgrades[index].cost[level];if(s.money<price)return{success:false,code:'insufficient_cash'};s.money-=price;s.upgrades[index]++;this._say('시설 업그레이드 완료.','Facility upgraded.');return{success:true,code:'upgraded'};
    }
    const e=catalog.events[index];if(s.activeEvent>=0||s.cooldowns[index]>0)return{success:false,code:'event_unavailable'};if(s.money<e.cost)return{success:false,code:'insufficient_cash'};
    s.money-=e.cost;s.activeEvent=index;s.eventRemaining=e.duration;s.eventStartPours=s.pours;s.eventFanTimer=0;s.cooldowns[index]=e.duration+e.cooldown;s.performance=Math.min(1,s.performance+.05);this._requestTimer=Math.min(1,this._requestTimer);return{success:true,code:'event_started'};
  }
  _onDuty(i){return!!this.state.staff[i]&&!this.state.wageDebt;}
  _opsTick(dt){
    const s=this.state;s.cooldowns=s.cooldowns.map(x=>Math.max(0,x-dt));s.wageTimer-=dt;
    if(s.wageTimer<=0){if(!this.payroll){s.wageDebt=0;s.wageTimer=60;}else{if(!s.wageDebt)s.wageDebt=this.payroll;if(s.money>=s.wageDebt){s.money-=s.wageDebt;s.wageDebt=0;s.wageTimer=60;this._say('직원 급여를 지급했습니다.','Staff payroll paid.');}else{s.wageTimer=10;this._say('급여 미지급으로 고용 직원의 근무가 중단됐어요.','Wages are due. Hired staff are off duty.');}}}
    if(this._onDuty(0)){s.autoServeTimer-=dt;if(s.autoServeTimer<=0&&!this.pour){s.autoServeTimer=Math.max(6,12*(1-s.upgrades[0]*.12));if(this.orders.length)this._serve(this.orders[0].item,true,.7);}}
    if(this._onDuty(1))s.satisfaction=Math.min(1,s.satisfaction+dt*.0007);
    if(this._onDuty(2)){s.promoterTimer+=dt;if(s.promoterTimer>=20){const n=Math.floor(s.promoterTimer/20);s.promoterTimer-=n*20;s.fans+=n;}}
    s.performance=Math.min(1,s.performance+s.upgrades[1]*dt*.0004);
    if(s.activeEvent>=0){const d=Math.min(dt,s.eventRemaining);s.eventRemaining=Math.max(0,s.eventRemaining-dt);if(s.activeEvent===1)s.performance=Math.min(1,s.performance+d*.002);if(s.activeEvent===2){s.eventFanTimer+=d;if(s.eventFanTimer>=10){s.fans+=Math.floor(s.eventFanTimer/10)*2;s.eventFanTimer%=10;}}if(s.eventRemaining<=0)this._completeEvent();}
  }
  _completeEvent(){const s=this.state;if(s.activeEvent<0)return;const e=catalog.events[s.activeEvent],success=s.pours-s.eventStartPours>=e.target;s.money+=success?e.reward:50;s.fans+=success?e.fans:3;if(success){s.completedEvents++;s.satisfaction=Math.min(1,s.satisfaction+.04);this.journey.successfulEvents++;this.journey.score+=250;}s.cooldowns[s.activeEvent]=Math.min(s.cooldowns[s.activeEvent],e.cooldown);s.activeEvent=-1;s.eventRemaining=0;this._say(success?'이벤트 성공! 보상과 팬을 획득했어요.':'이벤트가 종료됐어요. 다음 목표에 도전해 보세요.',success?'Event success! Cash and fans awarded.':'Event ended. Try the next target.');}
  _seedOrders(){this.orders=[];for(const [id,item]of[[1,0],[4,1],[8,2]])this._assignOrder('patron-'+id,item);}
  _assignOrder(id,item){
    const a=this.actors.find(a=>a.id===id);if(!a||this.orders.some(o=>o.id===id))return;
    const s=this.state,patience=52+this._random()*16+(this._onDuty(1)?18:0)+s.upgrades[2]*6+(s.activeEvent===0?8:0)+(this.journey.challenge?-12:18);
    this.orders.push({id,name:a.name,item,patience,maxPatience:patience});this._routeOrders();
  }
  _createOrder(){if(this.orders.length>=4+this.state.upgrades[2])return;const pool=this.actors.filter(a=>a.id.startsWith('patron-')&&!this.orders.some(o=>o.id===a.id));if(!pool.length)return;const a=pool[Math.floor(this._random()*pool.length)];this._assignOrder(a.id,Math.floor(this._random()*5));}
  _routeOrders(){
    for(const a of this.actors.filter(a=>a.id.startsWith('patron-'))){const i=this.orders.findIndex(o=>o.id===a.id);if(i>=0){const goal=orderSlots[Math.min(i,6)];if(a.action!=='queue'||Math.hypot(a.targetX-goal.x,a.targetZ-goal.z)>.1){a.action='queue';setRoute(a,goal,this.actors);}}
      else if(a.action==='queue'){a.action='return';setRoute(a,point(a.homeX,a.homeZ),this.actors);}}
  }
  _startPour(item){
    if(!this.playing||this.paused||this.pour||this._inputGuard>0)return{success:false,code:'unavailable'};
    const front=this.orders[0];if(!front){this._say('아직 주문이 없어요. 공연을 즐겨 보세요!','No orders yet. Enjoy the set!');return{success:false,code:'no_orders'};}
    if(front.item!==item){this.state.satisfaction=Math.max(.3,this.state.satisfaction-.025);this._mistake();this._inputGuard=.15;this._say('손님의 주문과 다른 메뉴예요.','That is not the guest’s order.');return{success:false,code:'wrong_item'};}
    this.pour={item,elapsed:0,duration:3,remaining:3,marker:.04,target:.65+Math.sin(this.state.pours*.87+item*2.1)*.1,perfectWidth:this.journey.challenge?.09:.16,goodWidth:this.journey.challenge?.28:.4,orderId:front.id};this._inputGuard=.12;return{success:true,code:'pour_started'};
  }
  _stopPour(){if(!this.pour||!this.playing||this.paused||this._inputGuard>0)return{success:false,code:'unavailable'};const d=Math.abs(this.pour.marker-this.pour.target),q=d<=this.pour.perfectWidth*.5?1:d<=this.pour.goodWidth*.5?.65:.2;return this._finishPour(q);}
  _finishPour(quality){const p=this.pour;if(!p||this.orders[0]?.id!==p.orderId){this.pour=null;return{success:false,code:'stale_order'};}this.pour=null;this._inputGuard=.15;this.lastQuality=quality;return this._serve(p.item,false,quality);}
  _mistake(){const j=this.journey;j.mistakes++;j.combo=0;j.manualStreak=0;j.feverSeconds=0;j.feverCharge=0;}
  _serve(item,automatic,quality){
    const o=this.orders[0],s=this.state,j=this.journey;if(!o||o.item!==item||!this.playing||this.paused)return{success:false,code:'wrong_item'};
    this.orders.shift();const qualityTip=automatic?1:quality>=.9?1.4:quality>=.5?1:0,revenue=1+s.upgrades[0]*.08+(s.activeEvent===0?.10:s.activeEvent===2?.15:0),tips=1+s.upgrades[1]*.1+(s.activeEvent===1?.4:s.activeEvent===2?.2:0);
    const tip=round(catalog.drinks[item].price*clamp(o.patience/30,0,1)*.45*tips*(j.feverSeconds>0?1.75:1)*qualityTip),sale=round(catalog.drinks[item].price*revenue),income=tip+sale;
    s.money=clamp(s.money+income,0,1e9);s.fans+=automatic||quality>=.5?1+(!automatic&&quality>=.9?1:0):0;s.pours++;s.satisfaction=clamp(s.satisfaction+(automatic?.012:quality>=.5?.01+quality*.015:-.025),.3,1);s.performance=clamp(s.performance+(automatic||quality>=.5?.014:-.008),0,1);
    j.orders++;j.drinkIncome+=income;if(!automatic){if(quality<.5)this._mistake();else{j.combo++;j.bestCombo=Math.max(j.bestCombo,j.combo);if(quality>=.9)j.manualPerfects++;j.manualStreak++;let multiplier=1+Math.min(20,j.combo-1)*.05;if(j.feverSeconds>0)multiplier*=1.3;j.score+=round((70+130*quality)*multiplier);j.feverCharge=clamp(j.manualStreak/5,0,1);if(!j.feverSeconds&&j.manualStreak>=5&&s.performance>=.72){j.feverSeconds=12;j.manualStreak=0;j.feverCharge=0;}}}
    this._routeOrders();this._createOrder();this._say((automatic?'바 보조 서빙':quality>=.9?'완벽한 서빙!':quality>=.5?'잘 서빙했어요!':'타이밍을 놓쳤어요 — 팁 없음')+' +'+this.formatMoney(income),(automatic?'Barback served':quality>=.9?'Perfect service!':quality>=.5?'Good service!':'Missed service — no tip')+' +'+this.formatMoney(income));return{success:true,code:automatic?'auto_served':quality>=.9?'perfect':quality>=.5?'good':'miss',quality,income};
  }
  tick(dt){
    if(!Number.isFinite(dt)||dt<=0)return;dt=Math.min(dt,5);let remaining=dt;
    while(remaining>0){const step=Math.min(.05,remaining);remaining-=step;this._step(step);}this._notify();
  }
  _step(dt){
    if(this.paused||(!this.playing&&!this.closing))return;this.elapsed+=dt;this._inputGuard=Math.max(0,this._inputGuard-dt);this._messageTimer-=dt;if(this._messageTimer<=0)this._message=['',''];
    const j=this.journey,s=this.state;this.effects=this.effects.filter(e=>this.elapsed-e.time<8);
    if(this.playing){
      j.secondsLeft=Math.max(0,j.secondsLeft-dt);j.feverSeconds=Math.max(0,j.feverSeconds-dt);if(j.secondsLeft<=0){this._lastCall();return;}
      if(this.pour){this.pour.elapsed+=dt;this.pour.remaining=Math.max(0,3-this.pour.elapsed);const v=(.04+this.pour.elapsed*.82)%2;this.pour.marker=v<=1?v:2-v;if(!this.pour.remaining)this._finishPour(.2);}
      this._opsTick(dt);s.clock=18+6*(1-j.secondsLeft/180);this._requestTimer-=dt;if(this._requestTimer<=0){this._createOrder();this._requestTimer=(4.5+this._random()*3)*(this._onDuty(2)?.9:1)*(s.activeEvent===2?.7:1)*(j.challenge?.72:1.18)*this.metrics().orderIntervalMultiplier;}
      for(const o of this.orders)if(!this.pour||o.id!==this.pour.orderId)o.patience-=dt;
      if(this.orders[0]?.patience<=0&&!this.pour){this.orders.shift();s.satisfaction=Math.max(.35,s.satisfaction-.07);s.performance=Math.max(.35,s.performance-.04);s.fans=Math.max(0,s.fans-3);this._mistake();this._routeOrders();this._say('손님이 주문을 받지 못하고 떠났어요.','A guest left without their order.');}
      s.performance=clamp(s.performance-dt*(.0015-this.metrics().energyBonus*.006),0,1);
    }
    this._tickActors(dt);this._floorTick(dt);this._autosave+=dt;if(this._autosave>=3){this._autosave=0;this.save();}
  }
  _floorTick(dt){
    this._ensureReaction();const r=this.reaction,f=this.floor;
    if(this.playing){const due=Challenges.advance(r,180-this.journey.secondsLeft,true);for(let i=0;i<due.confetti;i++)this._paper(0);for(let i=0;i<due.tissue;i++)this._paper(1);if(due.guest&&!f.guestSpawned){Floor.spawnGuest(f,this.journey.day%10);this._ensureGuest();this._say('손님 안내 미션! F 키나 버튼을 빠르게 연타하세요.','Guest assistance! Rapidly tap F or the button.');}}
    this._ensureCleanup();this._tickFloorActors(dt);
    const timeout=Challenges.tick(r,dt,true);if(timeout.timedOut){this.state.satisfaction=Math.max(.3,this.state.satisfaction-.015);this._say('시간을 놓쳤어요. 잠시 뒤 다시 연타로 대응하세요!','Time ran out. A new response window will appear shortly.');}
    if(this.closing){const ready=this.cleanupReadyWorkers,before=f.cleaned,workDt=ready?Math.min(dt,r.cleanupWorkBudget/(ready*3)):0,result=Floor.tick(f,workDt,ready,true);r.cleanupWorkBudget=Math.max(0,r.cleanupWorkBudget-(f.cleaned-before));if(r.cleanupWorkBudget<.001)r.cleanupWorkBudget=0;if(result.cleanupFinished){this.journey.score+=150;this._say('직원 청소 미션 완료!','Staff cleanup mission complete!');}}
    if(!r.activeKind&&r.cooldown<=0){if(f.guestActive)Challenges.start(r,2,f.guestStep);else if(this.closing&&f.cleanupActive&&this.cleanupReadyWorkers>0&&r.cleanupWorkBudget<=0)Challenges.start(r,1,Math.floor(f.cleaned/32));}
    if(this.closing&&!this.floorTasksPending)this._settleNight();
  }
  _paper(kind){const result=Floor.throw(this.floor,kind,this.playing);if(!result.success)return;this.state.fans+=result.fanGain;this.state.performance=Math.min(1,this.state.performance+result.energyGain);this.effects.push({kind,serial:++this._eventSerial,time:this.elapsed});this._say(kind===0?'밴드가 깜짝 꽃가루 퍼포먼스를 했어요!':'관객 위로 휴지가 날립니다!',kind===0?'The band unexpectedly throws confetti!':'Tissue streamers fly over the crowd!');}
  _quick(kind){
    if(this.paused||(!this.playing&&!this.closing)||!this.reaction)return{success:false,code:'unavailable'};
    if(kind===2&&(!this.floor.guestActive||this.reaction.activeStage!==this.floor.guestStep))return{success:false,code:'no_guest'};
    if(kind===1&&(!this.closing||!this.floor.cleanupActive||!this.cleanupReadyWorkers))return{success:false,code:'no_workers'};
    const result=Challenges.press(this.reaction,kind,true);if(result.completed){if(kind===2){const guest=Floor.resolveGuest(this.floor,result.stage);if(guest.guestCompleted){this.state.fans+=4;this.journey.score+=150;this._beginEscort();this._say('감사합니다. 직원이 출입구까지 동행하겠습니다.','Thank you. A staff member will accompany you to the exit.');}else this._say(result.stage===0?'추가 주문을 멈추고 물을 안내했어요.':'정중한 퇴점 요청을 전했어요. 소지품을 확인하세요.',result.stage===0?'Service paused and water offered.':'A courteous exit request was understood. Check belongings.');}else this._say('빠르게 대응했어요! 직원들이 바닥을 정리합니다.','Great response! Arrived staff are clearing the paper.');}return{...result,success:result.accepted,code:result.completed?'reaction_complete':result.accepted?'reaction_press':'reaction_ignored'};
  }
  _lastCall(){this.pour=null;this.journey.secondsLeft=0;this.state.clock=24;if(this.floorTasksPending)this._beginClosing();else this._settleNight();}
  _beginClosing(){this.phase='closing';this.paused=false;this.pauseReason='';this.journey.floorClosing=true;this.journey.inProgress=true;this.journey.settlementPending=false;this.journey.guidePending=false;this.orders=[];this._routeOrders();this._ensureReaction();this._ensureCleanup();this._say('공연이 끝났어요. C 청소·F 손님 안내 미션을 마쳐 주세요.','Show over. Finish C cleanup and F guest assistance.');}
  _ensureCleanup(){
    const f=this.floor;if(!this.closing||Floor.remaining(f)<=0)return;if(!f.cleanupActive&&!f.cleanupComplete)Floor.startCleanup(f,this.availableWorkers,true);
    if(!this.crew.length){this.crew=this.actors.filter(a=>(a.id==='bartender'||a.id==='server'||a.id.startsWith('hired-')&&this._onDuty(+a.id.slice(-1)))&&a.visible).map(a=>a.id);f.cleanupWorkers=this.crew.length;for(let i=0;i<this.crew.length;i++){const a=this.actors.find(x=>x.id===this.crew[i]);a.action='cleaning';a._ready=false;setRoute(a,this._cleanGoal(i),this.actors,true);}}
  }
  _settleNight(){
    const j=this.journey;if(j.session<=0||j.session===j.settledSession){j.settlementPending=false;j.inProgress=false;j.floorClosing=false;this.phase='results';this.paused=true;this.pauseReason='results';return;}
    if(this.floorTasksPending){this._beginClosing();return;}
    this._completeEvent();const targets=this.goalTargets(),values=this.goals(),goals=values.filter((v,i)=>v>=targets[i]).length,stars=goals===3?3:goals>=2?2:1;
    const bonus=(j.challenge?130:75)+goals*(j.challenge?100:65)+stars*40,xp=25+goals*25+stars*15+(j.challenge?35:0);
    j.resultStars=stars;j.resultServed=j.orders;j.resultXP=xp;j.resultBonus=bonus;j.resultNewRecord=j.score>j.bestScore;j.bestScore=Math.max(j.bestScore,j.score);j.bestStars=Math.max(j.bestStars,stars);j.xp+=xp;j.completedNights++;this.state.money+=bonus;
    if(j.booking?.locked&&!j.booking.completed){const forecast=Booking.forecast(j.booking),receipt=Booking.complete(j.booking,j.bookingCareer,stars);if(receipt.success){this.state.money+=receipt.cashDelta;this.state.fans+=forecast.fanBonus;j.resultTicketIncome=receipt.income;j.resultAttendance=receipt.attendance;}}
    const cleanup=Floor.settle(this.floor);if(cleanup.success){this.state.money+=cleanup.cashDelta;this.state.fans+=cleanup.fanGain;j.resultFloorBonus=cleanup.cashDelta;}
    j.resultFloorTasks=(this.floor.cleanupComplete?1:0)+(this.floor.guestComplete?1:0);j.resultProfit=this.state.money-j.openingCash;j.settledSession=j.session;j.settlementPending=false;j.inProgress=false;j.floorClosing=false;j.guidePending=false;j.feverSeconds=0;j.combo=0;
    this.state.money=clamp(this.state.money,0,1e9);this.state.fans=int(this.state.fans,0,1e7);
    this.phase='results';this.paused=true;this.pauseReason='results';this.orders=[];this.pour=null;this.save();
  }
  _buildActors(){
    const make=(id,name,role,x,z,y=0)=>({id,name,role,x,z,y,homeX:x,homeZ:z,targetX:x,targetZ:z,moving:false,action:role==='band'?'perform':'idle',visible:true,facing:0,_path:[],_pathAt:0,_wait:2});
    this.actors=[make('bartender','Bartender','bartender',-7.3,-.565),make('server','Server','server',-4.2,-.565),make('singer','Singer','band',5.5,2.7,.62),make('guitarist','Guitarist','band',3,3.8,.62),make('bassist','Bassist','band',8,4.6,.62),make('drummer','Drummer','band',6,6.2,.62)];
    const positions=[[.1,.7],[1.5,.5],[2.85,.5],[-.1,-.8],[1.2,-1.1],[2.6,-1],[-.2,-2.55],[1.2,-2.6],[2.6,-2.75],[0,-4.4],[1.55,-4.3],[3,-4.55],[8.55,.65],[8.65,-1],[8.6,-2.7],[8.6,-4.7]];
    for(let i=0;i<16;i++){let [x,z]=positions[i];if(i<6){x=5.75+Math.cos(i*Math.PI/3)*1.58;z=-1.25+Math.sin(i*Math.PI/3)*1.58;}else if(i<8){x=5.75+(i===6?-.68:.68);z=-1.25;}const a=make('patron-'+i,catalog.guestNames[i%10],'guest',x,z);a.slot=i;this.actors.push(a);}
    for(let i=0;i<4;i++)this.actors.push(make('bar-seat-'+i,'Bar Guest '+i,'seated',-7.55+i*1.55,-3.15));
    const seats=[[-8.55,6.4],[-7,6.4],[-4.45,6.4],[-2.95,6.4],[-8.35,-6.5],[-6.85,-6.5],[-3.45,-6.5]];seats.forEach(([x,z],i)=>this.actors.push(make('lounge-'+i,'Lounge Guest '+i,'seated',x,z)));
    for(let i=0;i<3;i++){const a=make('hired-'+i,catalog.staff[i].nameEnglish,'hired-'+i,...[[-5.8,-.565],[-.4,-4.9],[8.75,-3.5]][i]);a.visible=!!this.state.staff[i]&&!this.state.wageDebt;this.actors.push(a);}
    for(let i=0;i<3;i++)this.actors.push(make('support-'+i,'Crowd Supporter '+i,'support',6+(i===0?-.65:i===1?.65:0),-4.25+(i===2?-.65:0)));
    this.actors.push(make('crowd-surfer','Crowd Surfer','surfer',6,-4.25));
  }
  _cleanGoal(index){
    const segment=Math.min(5,Math.floor(Floor.progress(this.floor)*6));let goal={...cleanStops[(segment+(index%2)*3)%6]};if(index>=2){goal.x+=index<4?-.45:.38;goal.z+=index<4?.32:-.32;if(!walkable(goal,true,true))goal={...cleanStops[(segment+(index%2)*3)%6]};}return goal;
  }
  _readyWorkers(){if(!this.floor.cleanupActive||this.floor.cleanupComplete)return[];return this.crew.map((id,i)=>({a:this.actors.find(a=>a.id===id),i})).filter(({a,i})=>a&&a.visible&&a.id!==this.escort?.worker&&(!a.id.startsWith('hired-')||this._onDuty(+a.id.slice(-1)))&&distance(a,this._cleanGoal(i))<=.22).map(x=>x.a);}
  _ensureGuest(){let a=this.actors.find(a=>a.id==='assisted-guest');if(!a){a={id:'assisted-guest',name:catalog.guestNames[this.floor.guestIndex%10]||'Guest',role:'assisted',x:-.65,z:-.25,y:0,homeX:-.65,homeZ:-.25,targetX:-.65,targetZ:-.25,moving:false,action:'needs-help',visible:true,_path:[],_pathAt:0};this.actors.push(a);}return a;}
  _beginEscort(){const guest=this._ensureGuest(),worker=this.actors.find(a=>a.id==='server');this.escort={worker:worker.id,phase:'join',homeX:worker.homeX,homeZ:worker.homeZ};worker.action='escort';worker._partner=guest.id;guest._partner=worker.id;setRoute(worker,point(guest.x-.5,guest.z-.3),this.actors,true);}
  _tickActors(dt){
    for(const a of this.actors){
      if(a.id.startsWith('hired-'))a.visible=this._onDuty(+a.id.slice(-1));if(!a.visible)continue;
      if(this.crew.includes(a.id)||a.id===this.escort?.worker||a.id==='assisted-guest')continue;
      if(a.role==='band'){a.action=this.playing?'perform':'idle';continue;}if(a.role==='seated')continue;
      if(!this.playing&&(a.action==='mosh'||a.action==='slam')){a.action='idle';a.moving=false;}
      if(a.action==='queue'||a.action==='return'){if(move(a,dt,1.45,this.actors)){a.moving=false;if(a.action==='return')a.action='idle';}continue;}
      if(a.role==='guest'&&a.slot<6&&this.playing){const angle=a.slot*Math.PI/3+this.elapsed*.25,goal=point(5.75+Math.cos(angle)*1.58,-1.25+Math.sin(angle)*1.58);a.x=goal.x;a.z=goal.z;a.moving=true;a.action='mosh';}
      else if(a.role==='guest'&&a.slot<8&&this.playing){const side=a.slot===6?-1:1,separation=.44+Math.abs(Math.sin(this.elapsed*1.6))*.24;a.x=5.75+side*separation;a.z=-1.25;a.action='slam';a.moving=true;}
      else if(a.role==='surfer'){const t=this.elapsed%24;a.y=this.playing&&t>=2&&t<=18?Math.min(1,(t-2)/2,(18-t)/2)*1.85:0;a.action=a.y>0?'surf':'idle';}
      else if(a.role==='support'){a.action=this.playing?'support':'idle';}
      else if(a.role==='guest'&&a.action!=='queue'){
        if(a._pathAt<a._path.length){if(move(a,dt,1.1,this.actors)){a.action='idle';a._wait=2+this._random()*5;}}
        else{a.moving=false;a._wait-=dt;if(a._wait<=0){const goal=point(1+this._random()*7,-5+this._random()*5.5);if(walkable(goal,false,true)&&walkable(a,false,true)){a.action='wander';setRoute(a,goal,this.actors,true);}a._wait=3+this._random()*5;}}
      }else{a.moving=false;a.action=this.playing?'serve':'idle';}
    }
  }
  _tickFloorActors(dt){
    for(let i=0;i<this.crew.length;i++){
      const a=this.actors.find(a=>a.id===this.crew[i]);if(!a||!a.visible||a.id===this.escort?.worker)continue;
      const goal=this._cleanGoal(i);if(this.floor.cleanupActive){if(Math.hypot(a.targetX-goal.x,a.targetZ-goal.z)>.1)setRoute(a,goal,this.actors,true);const arrived=move(a,dt,1.55,this.actors);a._ready=arrived;a.action=arrived&&this.reaction?.cleanupWorkBudget>0?'sweep':arrived?'cleanup-wait':'cleaning';}else{a.moving=false;a.action='idle';}
    }
    if(!this.escort)return;const e=this.escort,w=this.actors.find(a=>a.id===e.worker),g=this.actors.find(a=>a.id==='assisted-guest');if(!w||!g){this.escort=null;return;}
    if(e.phase==='join'){
      move(w,dt,2.5,this.actors);if(distance(w,g)<.95){e.phase='exit';setRoute(g,point(8.7,-5.4),this.actors,false);w._path=[];w._pathAt=0;}
    }else if(e.phase==='exit'){
      if(distance(w,g)<1.35)move(g,dt,2.2,this.actors);
      const goal=point(g.x-.36,g.z+.18);if(walkable(goal,true,false)){if(Math.hypot(w.targetX-goal.x,w.targetZ-goal.z)>.25)setRoute(w,goal,this.actors,false);move(w,dt,2.9,this.actors);}
      if(distance(g,point(8.7,-5.4))<.2&&distance(w,g)<.9){g.visible=false;g.moving=false;e.phase='return';const ci=this.crew.indexOf(w.id),goal=ci>=0&&this.floor.cleanupActive?this._cleanGoal(ci):point(e.homeX,e.homeZ);setRoute(w,goal,this.actors,ci>=0&&this.floor.cleanupActive);}
    }else if(e.phase==='return'){
      if(move(w,dt,2.5,this.actors)){w._partner=null;g._partner=null;const ci=this.crew.indexOf(w.id);this.escort=null;if(ci>=0&&this.floor.cleanupActive){w.action='cleaning';setRoute(w,this._cleanGoal(ci),this.actors,true);}else{w.action='serve';w.moving=false;}}
    }
    if(this.escort){w.action='escort';g.action=e.phase==='join'?'wait-escort':'escorted';}
  }
}
