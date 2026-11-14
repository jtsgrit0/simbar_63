import {catalog} from './catalog.js';
import {createDailyPoster,DAILY_POSTER_INDEX} from './daily-poster.js';
import {Booking,clamp,int,count} from './booking.js';
import {Campaign} from './campaign.js';
import {Audience} from './audience.js';
import {Festival} from './festival.js';
import {Inspection} from './inspection.js';
import {SpectatorFeedback} from './spectator-feedback.js';
import {ownerVenue} from './owner-venues.js';
import {Floor,Challenges} from './floor.js';
import {distance,walkable,reserved,setRoute,setCrowdRoute,move,FF_LAYOUT,FREEBIRD_LAYOUT} from './navigation.js';
import {createAfterShow,sanitizeAfterShow,syncAfterShow,restoreAfterShow,ownsAfterShowActor,tickAfterShow} from './aftershow.js';
export {catalog,Booking,Floor,Challenges};
export const SAVE_KEY='SimBar.Operations.v1';
const OWNER_KEY='SimBar.Owner',LANGUAGE_KEY='SimBar.Language';
const clone=value=>JSON.parse(JSON.stringify(value));
const finite=(n,fallback,a,b)=>Number.isFinite(+n)?clamp(+n,a,b):fallback;
const round=n=>{const f=Math.floor(n),r=n-f;return r===.5?(f%2===0?f:f+1):Math.round(n);};
const point=(x,z)=>({x,z});
const cleanStops=[point(2.2,-3.9),point(2.2,.75),point(8.45,.75),point(8.35,-3.8),point(4.2,-4.8),point(7.9,-4.8)];
const orderSlots=[point(-1.5,-1.75),point(-.85,-2.25),point(-.15,-2.7),point(.6,-3.1),point(1.4,-3.5),point(2.2,-3.7),point(3,-4)];
const loungeHomes=[[-8.55,6.4],[-7,6.4],[-4.45,6.4],[-2.95,6.4],[-8.35,-6.5],[-6.85,-6.5],[-3.45,-6.5]];
const seatedHome=id=>id.startsWith('bar-seat-')?point(-7.55+(+id.slice(9))*1.55,-3.15):point(...loungeHomes[+id.slice(7)]);
const layoutForOwner=owner=>owner===0?FREEBIRD_LAYOUT:owner===1?FF_LAYOUT:null;
let scheduleSerial=0;
const freshScheduleSeed=()=>{
  let entropy=0;
  try{const word=new Uint32Array(1);globalThis.crypto?.getRandomValues(word);entropy=word[0];}catch{}
  if(!entropy)entropy=(Date.now()^Math.floor(Math.random()*0xffffffff))>>>0;
  return(entropy^Math.imul(++scheduleSerial,0x9e3779b1))>>>0;
};
const mixScheduleSeed=(seed,owner,day,session,kind)=>{
  let n=(seed^Math.imul(owner+1,0x9e3779b1)^Math.imul(day,0x85ebca6b)^Math.imul(session,0xc2b2ae35)^kind)>>>0;
  n=Math.imul(n^(n>>>16),0x7feb352d);n=Math.imul(n^(n>>>15),0x846ca68b);
  return((n^(n>>>16))&0x7fffffff)||1;
};
const defaults=()=>({version:1,money:650,fans:0,satisfaction:0,performance:0,clock:18,playlist:0,pours:0,staff:[0,0,0],upgrades:[0,0,0],wageTimer:60,wageDebt:0,
  activeEvent:-1,eventRemaining:0,eventStartPours:0,eventFanTimer:0,cooldowns:[0,0,0],completedEvents:0,autoServeTimer:12,promoterTimer:0,journeyJson:''});
const newJourney=()=>({version:1,day:1,challenge:false,tutorialComplete:false,guideStep:0,guidePending:false,inProgress:false,session:0,settledSession:0,settlementPending:false,secondsLeft:180,
  score:0,combo:0,bestCombo:0,manualPerfects:0,orders:0,mistakes:0,successfulEvents:0,manualStreak:0,feverSeconds:0,feverCharge:0,openingCash:650,drinkIncome:0,xp:0,bestScore:0,bestStars:0,completedNights:0,
  resultStars:0,resultServed:0,resultXP:0,resultBonus:0,resultProfit:0,resultNewRecord:false,booking:null,bookingCareer:Booking.newCareer(),dayOpeningCash:0,hasDayOpeningCash:false,resultTicketIncome:0,resultAttendance:0,
  floorMissions:Floor.newState(),floorClosing:false,afterShow:null,musicPauseSession:0,musicPaused:false,musicPauseIntent:false,resultFloorBonus:0,resultFloorTasks:0,resultTicketGross:0,resultArtistFee:0,campaignSecondSet:false,
  legacyDeskPlanJson:'',deskMigrationCredit:0,deskMigrationDay:0,deskMigrationTrustedOpening:false,festival:Festival.create(),feedback:null,inspection:Inspection.create()});
const messages={
  scouted:['출연진 발굴 완료. 팀을 선택하고 섭외를 진행하세요.','Lineup discovered. Select an artist and make an offer.'],
  listened:['데모 검토 완료. 제안 수락 확률이 올랐어요.','Demo reviewed. This artist is more likely to accept.'],
  accepted:['섭외 확정! 출연료를 지급했습니다.','Booking confirmed. The artist fee is paid.'],rejected:['오늘의 제안은 거절됐어요. 다른 팀에게 제안해 보세요.','The artist declined today. Try another newcomer.'],
  promoted:['홍보 완료. 예상 관객이 늘었어요.','Promotion complete. More guests are expected.'],insufficient_cash:['보유금이 부족합니다.','Not enough cash.'],no_time:['기획 시간이 끝났어요. 오늘의 밤을 열어 주세요.','Planning is over. Open tonight’s show.'],
  not_discovered:['먼저 포스터를 조사해 이 팀을 발굴해 주세요.','Research the artist’s poster first.'],already_discovered:['이미 발굴한 출연진입니다. 섭외 탭에서 제안할 수 있어요.','This lineup is already discovered. Make an offer in booking.'],
  desk_selected:['프리윌을 선택했어요. 컴퓨터에서 공연을 기획하세요.','Freewill selected. Plan the show at the computer.'],
  desk_cleared:['출연 선택과 준비 확인을 해제했어요.','Artist selection and desk checks cleared.'],
  desk_planned:['공연 기획 완료. 전화로 최종 확인하세요.','Show planned. Confirm by phone.'],
  desk_confirmed:['전화 확인 완료. 공연을 시작할 수 있어요.','Phone confirmation complete. The show is ready.'],
  band_required:['프리윌을 먼저 선택하세요.','Select Freewill first.'],
  plan_required:['컴퓨터에서 공연을 먼저 기획하세요.','Plan the show at the computer first.'],
  desk_incomplete:['컴퓨터 기획과 전화 확인을 마쳐 주세요.','Finish computer planning and phone confirmation.'],
  already_done:['이미 완료한 준비입니다.','This preparation is already complete.'],
  unavailable:['지금은 이 행동을 진행할 수 없어요.','This action is unavailable right now.'],
  growth_locked:['영업을 더 마치면 이 투자가 열립니다. 잠금 조건을 확인해 주세요.','Complete more nights to unlock this investment. Check its requirement.'],
  track_not_booked:['오늘 섭외한 밴드의 곡만 재생할 수 있어요.','Only tonight’s booked artists can play this show.'],
  show_selected:['출연진을 선택했어요. 티켓과 예상 정산을 확인해 주세요.','Lineup selected. Review tickets and the forecast.'],
  show_locked:['아직 섭외 조건을 충족하지 못했어요.','This show’s booking requirements are not met yet.'],
  venue_locked:['진행 중인 공연을 마친 뒤 사장과 베뉴를 바꿀 수 있어요.','Finish the current show before changing owner and venue.'],
};

/** A complete small web player. Rules keep the authored save schema and stable catalog IDs. */
export class SimBarGame{
  constructor({storage,seed}={}){
    try{this.storage=storage??globalThis.localStorage;}catch{this.storage=null;}
    this._deterministicSeed=seed!=null;
    this._seed=((seed??(Date.now()^Math.floor(Math.random()*0x7fffffff)))>>>0)||1;
    this.state=defaults();this.journey=newJourney();this.phase='lobby';this.paused=true;this.pauseReason='lobby';
    this.owner=0;this.language='ko';this.orders=[];this.pour=null;this.effects=[];this.actors=[];this.crew=[];this.escort=null;
    this.selectedItem=0;this.lastQuality=null;this.lastResult={success:true,code:'ready'};this._message=['',''];this._messageTimer=0;this.elapsed=0;this._autosave=0;this._requestTimer=4;this._inputGuard=0;this._eventSerial=0;this._loading=false;this._musicPlaybackActive=null;
    this._buildActors();
    this.owner=Math.max(0,catalog.owners.findIndex(o=>o.id===this._get(OWNER_KEY)));
    this._applyOwnerLayout({reset:true});
    this.language=this._get(LANGUAGE_KEY)==='en'?'en':'ko';
    const raw=this._get(SAVE_KEY);if(raw)this.importSave(raw,{save:false});
    this._refreshFeedback();
    this.phase='lobby';this.paused=true;this.pauseReason='lobby';
    if(this.journey.settlementPending){this._settleNight();this.phase='lobby';this.paused=true;this.pauseReason='lobby';}
  }
  get floor(){return this.journey.floorMissions;}
  get reaction(){return this.floor.reaction;}
  get message(){return this._message[this.language==='en'?1:0]||'';}
  get cash(){return this.state.money;}
  get spectatorFeedback(){return this._spectatorScores||{satisfaction:0,performance:0,serviceCoverage:0,participation:0};}
  setMusicPlaybackActive(active){
    if(typeof active!=='boolean')return false;
    this._musicPlaybackActive=active;return true;
  }
  get hasSave(){return!!(this.journey.inProgress||this.journey.settledSession||this._activePlan());}
  get cleanupReadyWorkers(){return this._readyWorkers().length;}
  get guestExitFinished(){return!this.floor.guestComplete||(!this.escort&&!this.actors.find(a=>a.id==='assisted-guest'&&a.visible));}
  get availableWorkers(){return 2+this.state.staff.reduce((n,v)=>n+(v&&this.state.wageDebt===0?1:0),0);}
  get payroll(){return this.state.staff.reduce((n,v,i)=>n+v*catalog.staff[i].wage,0);}
  get playing(){return this.phase==='playing'&&this.journey.inProgress;}
  get inspection(){return Inspection.view(this.journey.inspection,this.actors);}
  get _performanceElapsed(){return this.elapsed-(this.journey.inspection?.frozenSeconds||0);}
  get festival(){
    const state=this.journey.festival||Festival.create(),liveAttendance=this.actors.filter(a=>(Audience.isAudience(a)||a.role==='assisted')&&a.visible!==false).length;
    return{...state,...Festival.modifiers(state,this.state.satisfaction,this.state.performance),liveAttendance,
      bookedAttendance:Booking.forecast(this.journey.booking).attendance,capacity:Audience.capacity(this.owner),
      blockedByInspection:this.inspection.freezePerformers,
      canAct:this.playing&&!this.paused&&liveAttendance>0&&!this.inspection.freezePerformers&&state.remaining<=0&&state.globalRemaining<=0};
  }
  get closing(){return this.phase==='closing'&&this.journey.inProgress;}
  get staffReturnPending(){return this.closing&&this.floor.cleanupComplete&&this.crew.some((id,i)=>{const a=this.actors.find(a=>a.id===id);return a?.visible&&a.id!==this.escort?.worker&&distance(a,this._parkGoal(i))>.05;});}
  get floorTasksPending(){return Floor.remaining(this.floor)>0||this.floor.guestActive||!this.guestExitFinished||this.staffReturnPending||this.inspection.active;}
  get afterShowPending(){return !this.journey.afterShow?.completed;}
  get farewellPending(){return this.journey.inProgress&&this.journey.floorClosing&&!!this.journey.afterShow?.completed&&!this.journey.afterShow.noticeAcknowledged;}
  get showFarewell(){return this.farewellPending;}
  goalTargets(){const p=this.journey.booking;return Booking.isCampaign(p)?Campaign.targets(p.campaignStage,this.journey.challenge):this.journey.challenge?[20,7,8]:[12,4,5];}
  purchaseGate(type,index){return Booking.isCampaign(this.journey.booking)?Campaign.purchaseGate(type,index,this.state.upgrades[index]+1,this.journey.completedNights):{allowed:true,requiredNights:0};}
  _campaignContext(){return{venueOwner:this.owner,completedNights:this.journey.completedNights,fans:this.state.fans,upgrades:this.state.upgrades,career:this.journey.bookingCareer};}
  _usesAudience(){return Booking.isCampaign(this.journey.booking)&&this.journey.booking.lockedMetrics?.audienceVersion===Audience.version;}
  _orderGuest(a){return a.visible!==false&&!a.festivalLead&&!a.festivalSupport&&!['arrive','to-stage'].includes(a.action)&&(a.id.startsWith('patron-')||a.audienceVersion===Audience.version&&a.role==='guest');}
  goals(){return[this.journey.orders,this.journey.manualPerfects,this.journey.bestCombo];}
  bookingChance(band,premium=0){return Booking.isDesk(this.journey.booking)?0:Booking.chance(this.journey.booking,this.journey.bookingCareer,+band,+premium);}
  bookingFee(band,premium=0){return Booking.isDesk(this.journey.booking)?0:Booking.fee(+band,+premium);}
  isDiscovered(band){return Booking.discovered(this.journey.bookingCareer,+band);}
  currentPoster(poster=DAILY_POSTER_INDEX){return+poster===DAILY_POSTER_INDEX?createDailyPoster(this.journey.day,this.journey.booking?.venue||0):catalog.posters[+poster];}
  isPosterDiscovered(poster){return+poster===DAILY_POSTER_INDEX?Booking.dailyPosterDiscovered(this.journey.bookingCareer,this.journey.day,this.journey.booking?.venue||0):Booking.posterDiscovered(this.journey.bookingCareer,+poster);}
  metrics(){
    const p=this.journey.booking,m=Booking.forecast(p);
    if(Booking.isCampaign(p)){
      const show=Campaign.show(p.showId);
      return{...m,deskVersion:2,campaignStage:p.campaignStage,show,campaignOptions:Booking.campaignOptions(p),
        artistFee:m.artistFee??m.fee??0,netTicketIncome:m.netIncome??0,ticketIncomeMin:Math.floor(m.ticketIncome*.8),
        netTicketIncomeMin:Math.floor(m.ticketIncome*.8)-(m.artistFee??m.fee??0),allowedTracks:show?show.tracks:[],
        canLaunch:!p.locked&&!p.completed&&!!show&&p.deskDoneMask===3,hour:Booking.hour(p),
        lineup:show?show.bandIndices.map(i=>catalog.bands[i]):[],ticketTier:p.ticketTier,venue:Number.isInteger(m.venueOwner)?ownerVenue(m.venueOwner):catalog.venues[p.venue]};
    }
    if(Booking.isDesk(p))return{...m,deskVersion:1,freewillSelected:p.freewillSelected,deskDoneMask:p.deskDoneMask,
      canLaunch:!p.locked&&!p.completed&&p.freewillSelected&&p.deskDoneMask===3,hour:Booking.hour(p),
      lineup:p.freewillSelected?catalog.availableBandIndices.map(i=>catalog.bands[i]):[],ticketTier:p.ticketTier,venue:catalog.venues[p.venue]};
    return{...m,hour:Booking.hour(p),blocks:p?.blocks||0,remainingBlocks:4-(p?.blocks||0),lineup:catalog.bands.filter(b=>p&&(p.bookedMask&(1<<b.index))),
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
    syncAfterShow(this.journey.afterShow,this.actors);
    Inspection.sync(this.journey.inspection,this.actors);
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
      for(const [key,a,b] of [['money',0,1e9],['fans',0,1e7],['satisfaction',0,1],['performance',0,1],['clock',18,24],['wageTimer',.1,60],['autoServeTimer',.1,12],['promoterTimer',0,20]])this.state[key]=finite(data[key],base[key],a,b);
      for(const [key,max]of[['staff',1],['upgrades',3]])this.state[key]=Array.from({length:3},(_,i)=>int(data[key]?.[i],0,max));
      this.state.cooldowns=Array.from({length:3},(_,i)=>clamp(data.cooldowns?.[i],0,600));this.state.wageDebt=int(data.wageDebt,0,this.payroll);
      this.state.activeEvent=int(data.activeEvent,-1,2);this.state.eventRemaining=clamp(data.eventRemaining,0,this.state.activeEvent<0?0:catalog.events[this.state.activeEvent].duration);if(!this.state.eventRemaining)this.state.activeEvent=-1;
      for(const key of ['pours','eventStartPours','completedEvents','eventFanTimer'])this.state[key]=int(data[key]);this.state.playlist=int(data.playlist,0,catalog.playlists.length-1);
      let j={};try{j=JSON.parse(data.journeyJson||'{}');}catch{}
      this.journey={...newJourney(),...j};this._sanitizeJourney();
      if(this.journey.inProgress&&this._usesAudience())this.owner=this.journey.booking.lockedMetrics.venueOwner;
      else if(this.journey.inProgress&&this.journey.festival.actionsUsed>0&&Number.isInteger(this.journey.festival.venueOwner))this.owner=this.journey.festival.venueOwner;
      this._buildActors(data.native?.version===1&&Array.isArray(data.native.actors)&&data.native.actors.length<100?data.native.actors:null);this.orders=[];this.crew=[];this.escort=null;this.pour=null;this.effects=[];
      if(Booking.isCampaign(this.journey.booking)&&this.journey.booking.locked){const tracks=this.metrics().allowedTracks;if(!tracks.includes(this.state.playlist))this.state.playlist=tracks[0]??0;}
      const n=data.native;let restoredEmptyQueue=false;
      const restoreCast=this.journey.inProgress||this.journey.session>0&&this.journey.settledSession===this.journey.session
        &&(!this._usesAudience()||this.journey.booking.lockedMetrics.venueOwner===this.owner);
      if(n?.version===1&&restoreCast&&Array.isArray(n.actors)&&n.actors.length<100){
        // An active audience contract keeps its original cast when a later
        // room revision adds authored chairs with different IDs. New nights
        // use those chairs; only newly blocked saved positions migrate below.
        if(this._usesAudience()&&this.owner===0){
          const saved=n.actors.filter(a=>Audience.isAudience(a)&&a.audienceVersion===Audience.version
            &&/^(?:patron-\d+|audience-\d+|support-[0-2]|crowd-surfer|arrival-\d+-\d+)$/.test(a.id)
            &&Number.isFinite(a.x)&&Number.isFinite(a.z)&&Math.abs(a.x)<20&&Math.abs(a.z)<20);
          const current=this.actors.filter(Audience.isAudience);
          if(saved.length===current.length&&new Set(saved.map(a=>a.id)).size===saved.length
            &&saved.some(a=>a.layout==='freebird'&&a.layoutVersion<FREEBIRD_LAYOUT.version))
            this.actors=[...this.actors.filter(a=>!Audience.isAudience(a)),...saved];
        }
        const allowed=new Set(this.actors.map(a=>a.id));allowed.add('assisted-guest');
        const restored=n.actors.filter(a=>allowed.has(a.id)&&Number.isFinite(a.x)&&Number.isFinite(a.z)&&Math.abs(a.x)<20&&Math.abs(a.z)<20).map(a=>({...a,_path:Array.isArray(a._path)?a._path.filter(p=>Number.isFinite(p.x)&&Number.isFinite(p.z)).slice(0,300):[],_pathAt:int(a._pathAt,0,300)}));
        if(restored.length>=this.actors.length-3){const byId=new Map(restored.map(a=>[a.id,a]));this.actors=this.actors.map(a=>byId.get(a.id)||a);if(byId.has('assisted-guest'))this.actors.push(byId.get('assisted-guest'));}
        // A campaign can legitimately finish the queue between timed arrivals.
        // Preserve that saved gap instead of granting three new orders on reload.
        restoredEmptyQueue=Booking.isCampaign(this.journey.booking)&&restored.length>=this.actors.length-3&&Array.isArray(n.orders)&&n.orders.length===0;
        this.orders=Array.isArray(n.orders)?n.orders.filter(o=>this.actors.some(a=>a.id===o.id)&&int(o.item,0,catalog.drinks.length-1)===o.item).slice(0,7).map(o=>({...o,patience:clamp(o.patience,0,200),maxPatience:clamp(o.maxPatience,1,200)})):[];
        this.crew=Array.isArray(n.crew)?n.crew.filter(id=>this.actors.some(a=>a.id===id)).slice(0,5):[];
        this.escort=n.escort&&this.actors.some(a=>a.id===n.escort.worker)?n.escort:null;
        this._seed=(n.seed>>>0)||this._seed;this.elapsed=clamp(n.elapsed,0,1e8);this._requestTimer=finite(n.requestTimer,3,0,10);this.selectedItem=int(n.selectedItem,0,catalog.drinks.length-1);this._eventSerial=int(n.eventSerial);
      }
      this.journey.feedback=SpectatorFeedback.sanitize(this.journey.feedback,{session:this.journey.session,owner:this.owner});
      restoreAfterShow(this.journey.afterShow,this.actors);
      if(this._applyOwnerLayout()){this._routeOrders();syncAfterShow(this.journey.afterShow,this.actors);}
      Inspection.restore(this.journey.inspection,this.actors,this.owner,data.native?.actors);
      if(!this.orders.length&&!restoredEmptyQueue&&this.journey.inProgress&&!this.journey.floorClosing)this._seedOrders();
      if(this.floor.guestSpawned&&(!this.floor.guestComplete||this.actors.some(a=>a.id==='assisted-guest'&&a.visible!==false)))this._ensureGuest();
      this._refreshFeedback();
      this.phase='lobby';this.paused=true;this.pauseReason='lobby';if(save)this.save();this._notify();return true;
    }catch{return false;}
  }
  _sanitizeJourney(){
    const j=this.journey;j.version=1;j.day=int(j.day,1,9999);j.session=int(j.session);j.settledSession=int(j.settledSession,0,j.session);j.secondsLeft=finite(j.secondsLeft,180,0,180);
    for(const key of ['score','combo','bestCombo','manualPerfects','orders','mistakes','successfulEvents','manualStreak','xp','bestScore','completedNights','resultServed','resultXP','resultBonus','resultTicketGross','resultArtistFee'])j[key]=int(j[key],0,1e8);
    j.bestCombo=Math.max(j.bestCombo,j.combo);j.orders=Math.max(j.orders,j.manualPerfects);j.guideStep=int(j.guideStep,0,2);j.feverSeconds=clamp(j.feverSeconds,0,12);j.feverCharge=clamp(j.feverCharge,0,1);j.resultStars=int(j.resultStars,0,3);j.bestStars=int(j.bestStars,0,3);
    j.openingCash=finite(j.openingCash,this.state.money,0,1e9);j.resultProfit=finite(j.resultProfit,0,-1e9,1e9);j.floorMissions={...Floor.newState(),...(j.floorMissions||{})};Floor.sanitize(j.floorMissions);
    j.bookingCareer={...Booking.newCareer(),...(j.bookingCareer||{})};if(j.booking)j.booking={...Booking.newPlan(j.day,0),...j.booking};Booking.sanitize(j.booking,j.bookingCareer);if(j.booking?.day!==j.day)j.booking=null;
    if(!j.session||j.settledSession>0&&j.session===j.settledSession){j.inProgress=false;j.settlementPending=false;j.floorClosing=false;}
    j.musicPauseSession=int(j.musicPauseSession,0,j.session);j.musicPaused=j.musicPaused===true;j.musicPauseIntent=j.musicPauseIntent===true;
    j.campaignSecondSet=j.campaignSecondSet===true;
    j.festival=Festival.sanitize(j.festival);
    j.inspection=Inspection.sanitize(j.inspection,{session:j.session,day:j.day,owner:this.owner,seed:this._seed});
    if(!j.inProgress||j.floorClosing&&['scheduled','warning'].includes(j.inspection.phase))j.inspection.phase='done';
    j.afterShow=sanitizeAfterShow(j.afterShow,j.session,{settled:j.session>0&&j.session===j.settledSession&&!j.inProgress});
    if(j.settlementPending&&(Floor.remaining(j.floorMissions)||j.floorMissions.guestActive||j.afterShow&&(!j.afterShow.completed||!j.afterShow.noticeAcknowledged))){j.settlementPending=false;j.inProgress=true;j.floorClosing=true;}
    if(j.floorClosing&&j.inProgress){j.secondsLeft=0;j.guidePending=false;j.settlementPending=false;}else j.floorClosing=false;
    if(j.floorClosing&&j.inProgress&&j.musicPauseSession!==j.session){j.musicPauseSession=j.session;j.musicPaused=true;}
    // Some old active-night saves predate the explicit booking lock. Recover it
    // without migrating the contract or changing its already-paid wallet.
    if(j.inProgress&&j.booking&&!Booking.isDesk(j.booking)&&!j.booking.locked&&!j.booking.completed)Booking.lock(j.booking);
    j.legacyDeskPlanJson=typeof j.legacyDeskPlanJson==='string'&&j.legacyDeskPlanJson.length<=8000?j.legacyDeskPlanJson:'';
    j.deskMigrationCredit=finite(j.deskMigrationCredit,0,0,1e9);j.deskMigrationDay=int(j.deskMigrationDay,0,9999);j.deskMigrationTrustedOpening=j.deskMigrationTrustedOpening===true;
    j.resultFloorBonus=clamp(j.resultFloorBonus,0,70);j.resultFloorTasks=int(j.resultFloorTasks,0,2);if(j.tutorialComplete)j.guidePending=false;
  }
  act(type,p={}){
    let r={success:true,code:type};const j=this.journey,s=this.state,idx=+p.index;
    switch(type){
      case'owner':{
        if(this.phase!=='lobby'||!Number.isInteger(idx)||idx<0||idx>2)return this._done(false);
        if(j.inProgress&&(this._usesAudience()||j.festival.actionsUsed>0)&&idx!==this.owner)return this._done(false,'venue_locked');
        const previous=this.owner;this.owner=idx;this._put(OWNER_KEY,catalog.owners[idx].id);
        if(previous!==idx){
          if(this._activePlan()&&Booking.isCampaign(j.booking))Booking.refreshCampaignContext(j.booking,this._campaignContext());
          this._applyOwnerLayout({reset:true});this._routeOrders();
        }
        break;
      }
      case'language':this.language=p.language==='en'?'en':'ko';this._put(LANGUAGE_KEY,this.language);break;
      case'musicState':if(typeof p.paused!=='boolean')return this._done(false);j.musicPaused=p.paused;j.musicPauseIntent=p.paused;break;
      case'farewellAck':
        if(!this.closing||!this.farewellPending)return this._done(false);
        j.afterShow.noticeAcknowledged=true;
        if(this.pauseReason==='farewell'){this.paused=false;this.pauseReason='';}
        if(!this.floorTasksPending&&!this.afterShowPending)this._settleNight();
        break;
      case'start':if(j.inProgress||this._activePlan())return this.act('continue');if(j.settledSession>0&&j.session===j.settledSession)j.day=int(j.day+1,1,9999);this._planning(!!p.challenge);break;
      case'continue':
        if(j.settlementPending)this._settleNight();
        if(!j.inProgress&&this._activePlan()){this._planning(j.challenge);break;}
        if(j.inProgress){
          // Old browser playback failures were persisted as if the player had
          // paused the music. Recover only a live show without a pause choice.
          if(!j.floorClosing&&j.secondsLeft>0&&j.musicPaused&&!j.musicPauseIntent)j.musicPaused=false;
          this._ensureReaction();this.phase=j.floorClosing||j.secondsLeft<=0?'closing':j.guidePending?'guide':'playing';this.paused=this.phase==='guide';this.pauseReason=this.paused?'guide':'';if(this.closing)this._beginClosing();
        }
        else if(j.settledSession){this.phase='results';this.paused=true;this.pauseReason='results';}else this._planning(j.challenge);break;
      case'next':if(this.phase!=='results'||j.inProgress)return this._done(false);j.day=int(j.day+1,1,9999);this._planning(j.challenge);break;
      case'retry':if(this.phase!=='results'||j.inProgress)return this._done(false);this._planning(j.challenge);break;
      case'lobby':this.pour=null;this.phase='lobby';this.paused=true;this.pauseReason='lobby';break;
      case'pause':if(this.playing||this.closing){this.paused=true;this.pauseReason=this.farewellPending?'farewell':p.reason||'pause';}break;
      case'resume':if(this.playing||this.closing){this.paused=this.farewellPending;this.pauseReason=this.paused?'farewell':'';}break;
      case'guide':if(this.phase!=='guide')return this._done(false);if(j.guideStep<2)j.guideStep++;else{j.tutorialComplete=true;j.guidePending=false;this.phase='playing';this.paused=false;this.pauseReason='';}break;
      case'deskSelect':case'deskWork':
        if(this.phase!=='planning')return this._done(false);
        r=type==='deskSelect'?Booking.deskSelect(j.booking,p.selected):Booking.deskWork(j.booking,p.task);
        this.state.clock=Booking.hour(j.booking);this._say(...(messages[r.code]||messages.unavailable));break;
      case'selectShow':
        if(this.phase!=='planning'||!Booking.isCampaign(j.booking))return this._done(false);
        r=Booking.selectShow(j.booking,p.showId);this.state.clock=Booking.hour(j.booking);
        this._say(...(messages[r.code]||messages.show_selected));break;
      case'venue':if(this.phase!=='planning'||Booking.isDesk(j.booking)||j.booking.blocks>0||!Number.isInteger(+p.venue)||+p.venue<0||+p.venue>3)return this._done(false);j.booking.venue=+p.venue;break;
      case'scout':case'listen':case'offer':case'promote':
        if(type==='promote'&&this.phase==='planning'&&Booking.isCampaign(j.booking)){
          r=Booking.promote(j.booking,j.bookingCareer,s.money);if(r.success)s.money=clamp(s.money+r.cashDelta,0,1e9);
          this.state.clock=Booking.hour(j.booking);this._say(...(messages[r.code]||messages.unavailable));break;
        }
        if(this.phase!=='planning'||Booking.isDesk(j.booking))return this._done(false);r=type==='scout'?(+p.poster===DAILY_POSTER_INDEX?Booking.scoutDaily(j.booking,j.bookingCareer):Booking.scout(j.booking,j.bookingCareer,+p.poster)):type==='listen'?Booking.listen(j.booking,j.bookingCareer,+p.band):type==='offer'?Booking.offer(j.booking,j.bookingCareer,+p.band,+p.premium||0,s.money):Booking.promote(j.booking,j.bookingCareer,s.money);
        if(r.success)s.money=clamp(s.money+r.cashDelta,0,1e9);this._say(...(messages[r.code]||messages.unavailable));s.clock=10+j.booking.blocks*2;break;
      case'ticket':
        if(this.phase!=='planning'||Booking.isDesk(j.booking)&&!Booking.isCampaign(j.booking)||!Number.isInteger(p.tier)||p.tier<0||p.tier>2)return this._done(false);
        r=Booking.setTicket(j.booking,p.tier)||r;this.state.clock=Booking.hour(j.booking);break;
      case'launch':
        if(this.phase!=='planning'||!this._activePlan())return this._done(false);
        if(Booking.isDesk(j.booking)){r=Booking.lockDesk(j.booking,j.bookingCareer);if(!r.success)return this._done(false,r.code);}else Booking.lock(j.booking);
        this._newNight();break;
      case'menuItem':this.selectedItem=int(p.item,0,catalog.drinks.length-1);if(this.pauseReason==='menu'){this.paused=false;this.pauseReason='';}r=this._startPour(this.selectedItem);break;
      case'prepare':r=this._startPour(this.orders[0]?.item??this.selectedItem);break;
      case'pourStop':r=this._stopPour();break;
      case'hire':case'upgrade':case'event':case'payDebt':r=this._manage(type,idx);if(!r.success)this._say(...(messages[r.code]||messages.unavailable));break;
      case'quick':r=this._quick(+p.kind);break;
      case'festival':r=this._festivalAction(p.id);break;
      case'inspection':
        if(!this.playing||this.paused||!Inspection.begin(j.inspection))return this._done(false);
        this._inspectionNotice('warning');r={success:true,code:'inspection_warning'};break;
      case'playlist':
        if(!Number.isInteger(idx)||idx<0||idx>=catalog.playlists.length)return this._done(false);
        if(Booking.isCampaign(j.booking)&&j.booking.showId&&!this.metrics().allowedTracks.includes(idx))return this._done(false,'track_not_booked');
        s.playlist=idx;
        this._say('재생 중: '+catalog.playlists[idx].name,'Now playing: '+catalog.playlists[idx].nameEnglish);break;
      default:return this._done(false,'unknown_action');
    }
    this.lastResult=r;this.save();this._notify();return r;
  }
  _done(success,code='unavailable'){const r={success,code};this.lastResult=r;if(!success)this._say(...(messages[code]||messages.unavailable));this._notify();return r;}
  _activePlan(){const p=this.journey.booking;return!!(p&&!p.locked&&!p.completed&&p.day===this.journey.day);}
  _planning(challenge){
    const j=this.journey;if(j.inProgress||j.booking?.locked&&!j.booking.completed)return;this.pour=null;
    if(this._activePlan()&&!Booking.isDesk(j.booking)){
      const previous=j.booking,opening=j.dayOpeningCash;
      const trusted=j.hasDayOpeningCash===true&&Number.isFinite(opening)&&opening>=this.state.money&&opening>=0&&opening<=1e9;
      j.legacyDeskPlanJson=JSON.stringify(previous);j.deskMigrationCredit=trusted?opening-this.state.money:0;
      j.deskMigrationDay=j.day;j.deskMigrationTrustedOpening=trusted;
      if(trusted)this.state.money=opening;
      j.booking=Booking.newDeskPlan(j.day,previous.venue);j.dayOpeningCash=this.state.money;j.hasDayOpeningCash=true;
    }else if(!this._activePlan()){
      const previousVenue=j.booking?.venue??(this.owner===2?1:0);j.booking=Booking.newCampaignPlan(j.day,int(previousVenue,0,3),this._campaignContext());
      j.dayOpeningCash=this.state.money;j.hasDayOpeningCash=true;
    }
    j.challenge=challenge;this.phase='planning';this.paused=true;this.pauseReason='planning';this.state.clock=Booking.hour(j.booking);
  }
  _newNight(){
    const j=this.journey;j.session=j.session>=1e9?1:j.session+1;if(j.session===1)j.settledSession=0;j.inProgress=true;j.settlementPending=false;j.floorClosing=false;j.afterShow=null;j.musicPauseSession=0;j.musicPaused=false;j.musicPauseIntent=false;j.secondsLeft=180;
    // Fresh nights have an independent schedule even when the same planning
    // save restores the gameplay RNG. The resulting journal stays stable on
    // continue/reload; explicit test seeds retain their authored sequence.
    const scheduleSeed=this._deterministicSeed?undefined:freshScheduleSeed();
    for(const k of ['score','combo','bestCombo','manualPerfects','orders','mistakes','successfulEvents','manualStreak','feverSeconds','feverCharge','drinkIncome','resultStars','resultServed','resultXP','resultBonus','resultProfit','resultTicketIncome','resultAttendance','resultFloorBonus','resultFloorTasks','resultTicketGross','resultArtistFee'])j[k]=0;
    j.resultNewRecord=false;j.campaignSecondSet=false;j.festival=Festival.create();j.openingCash=j.hasDayOpeningCash?j.dayOpeningCash:this.state.money;j.guideStep=0;j.guidePending=!j.tutorialComplete;j.floorMissions=Floor.newState();this._ensureReaction(scheduleSeed===undefined?undefined:mixScheduleSeed(scheduleSeed,this.owner,j.day,j.session,0x666c6f6f));
    this.state.clock=18;this.state.satisfaction=0;this.state.performance=0;
    j.feedback=SpectatorFeedback.create({session:j.session,owner:this.owner});
    j.inspection=Inspection.create({session:j.session,day:j.day,owner:this.owner,seed:scheduleSeed===undefined?this._seed:mixScheduleSeed(scheduleSeed,this.owner,j.day,j.session,0x70617472)});
    if(Booking.isCampaign(j.booking)){const tracks=this.metrics().allowedTracks;if(j.booking.showId==='double'||!tracks.includes(this.state.playlist))this.state.playlist=tracks[0]??0;}
    this._buildActors();this._refreshFeedback();this.crew=[];this.escort=null;this.effects=[];this.pour=null;this._seedOrders();this._requestTimer=3*(j.challenge?.72:1.18)*this.metrics().orderIntervalMultiplier;
    this.phase=j.guidePending?'guide':'playing';this.paused=j.guidePending;this.pauseReason=this.paused?'guide':'';this._say('영업 시작! 주문과 공연을 멋지게 운영해 보세요.','The show is open! Serve guests and keep the crowd glowing.');
  }
  _ensureReaction(scheduleSeed){if(!this.floor.reaction){const gameplaySeed=Math.floor(this._random()*0x7fffffff)||1;this.floor.reaction=Challenges.newState(scheduleSeed??gameplaySeed,180-this.journey.secondsLeft);}}
  _festivalAction(id){
    if(this.playing&&!this.paused&&this.inspection.freezePerformers){this._say('단속반이 지나가면 호응 액션을 다시 사용할 수 있어요.','Crowd actions will be available after the inspectors leave.',4);return{success:false,code:'inspection_blocked'};}
    if(!this.playing||this.paused||!this.festival.liveAttendance)return{success:false,code:'unavailable'};
    const state=this.journey.festival,result=Festival.begin(state,id);if(!result.success)return result;
    state.venueOwner=this.owner;
    const definition=result.action,s=this.state,response=Festival.response(s.satisfaction,s.performance);
    state.arrivalCredit=Math.min(1,state.arrivalCredit+.6*response);
    this._requestTimer=Math.min(this._requestTimer,2.2*this.festival.orderMultiplier);
    const eligible=this.actors.filter(a=>['guest','seated'].includes(a.role)&&a.visible!==false&&!a.moving&&!['queue','return','arrive','to-stage','exit','escorted'].includes(a.action));
    for(const actor of eligible){actor.festivalAction=id;actor.festivalSerial=state.serial;actor.festivalRemaining=state.remaining;actor.festivalDuration=state.duration;}
    if(id==='surf')this._formFestivalSurf(eligible);
    this._say(definition.ko+'! 호응이 높을수록 칵테일 주문과 현장 관객이 늘어나요.',definition.en+'! A responsive crowd brings more cocktail orders and walk-in guests.');
    return{success:true,code:'festival_started',id};
  }
  _clearFestivalActors(){
    for(const actor of this.actors){
      if(actor.festivalLead){actor.y=0;if(actor.action==='surf')actor.action='listen';}
      if(actor.festivalSupport&&['support','walk'].includes(actor.action)){
        actor.action=this.playing?'return':'listen';
        if(this.playing)setRoute(actor,point(actor.homeX,actor.homeZ),this.actors,false);
      }
      for(const key of ['festivalAction','festivalSerial','festivalRemaining','festivalDuration','festivalLead','festivalSupport','festivalTarget','festivalLiftReady','festivalLiftElapsed','_festivalRouteRetry'])delete actor[key];
    }
  }
  _formFestivalSurf(eligible){
    const floor=eligible.filter(a=>a.role==='guest'&&!a.seat&&this._walkable(a,false,true));let best=null;
    for(const lead of floor){
      const nearest=floor.filter(a=>a!==lead).sort((a,b)=>distance(a,lead)-distance(b,lead)).slice(0,3);
      if(nearest.length<3||nearest.some(a=>distance(a,lead)>2.1))continue;
      const team=[lead,...nearest];
      for(let turn=0;turn<8;turn++){
        const angle=turn*Math.PI/4,slots=[[-.55,-.15],[.55,-.15],[0,.57]].map(([x,z])=>point(lead.x+x*Math.cos(angle)-z*Math.sin(angle),lead.z+x*Math.sin(angle)+z*Math.cos(angle)));
        if(slots.some(p=>!this._walkable(p,false,true)||this.actors.some(a=>a.visible&&a.role!=='band'&&!team.includes(a)&&distance(a,p)<.43)))continue;
        const available=nearest.slice(),supporters=slots.map(target=>{
          available.sort((a,b)=>distance(a,target)-distance(b,target));return{actor:available.shift(),target};
        });
        const score=supporters.reduce((sum,member)=>sum+distance(member.actor,member.target),0)+distance(lead,point(5,-2))*.02;
        if(!best||score<best.score)best={lead,supporters,score};
      }
    }
    if(!best)return;
    best.lead.festivalLead=true;best.lead.festivalLiftReady=false;best.lead.festivalLiftElapsed=0;
    for(const {actor,target}of best.supporters){actor.festivalSupport=true;actor.festivalTarget=target;setRoute(actor,target,this.actors,false);}
  }
  _festivalTick(dt){
    const state=this.journey.festival;
    Festival.tick(state,dt,this.state.satisfaction,this.state.performance);
    if(!state.activeId)this._clearFestivalActors();
    else for(const actor of this.actors)if(actor.festivalSerial===state.serial)actor.festivalRemaining=state.remaining;
    if(state.arrivalCredit<1||state.arrivalRetry>0||this.festival.liveAttendance>=Audience.capacity(this.owner))return;
    state.arrivalRetry=.8;
    const serial=state.arrivalSerial+1,actor=Audience.arrival({owner:this.owner,showId:this.journey.booking?.showId,day:this.journey.day,session:this.journey.session,serial,actors:this.actors});
    if(!actor)return;
    const entry=Audience.profile(this.owner).entry||point(8.7,-5.4);
    if(!this._walkable(entry,false,false)||this.actors.some(a=>a.visible&&a.role!=='band'&&distance(a,entry)<.5))return;
    actor.x=entry.x;actor.z=entry.z;actor.action='arrive';
    if(!setRoute(actor,point(actor.homeX,actor.homeZ),this.actors,false))return;
    this.actors.push(actor);state.arrivalSerial=serial;state.arrivals++;state.arrivalCredit=0;
  }
  _refreshFeedback(){
    const j=this.journey;
    if(!j.feedback)j.feedback=SpectatorFeedback.create({session:j.session,owner:this.owner});
    this._spectatorScores=SpectatorFeedback.scores(j.feedback,{actors:this.actors,capacity:Audience.capacity(this.owner)});
    this.state.satisfaction=clamp(this._spectatorScores.satisfaction,0,1);
    this.state.performance=clamp(this._spectatorScores.performance,0,1);
  }
  _feedbackTick(dt){
    const j=this.journey;
    SpectatorFeedback.tick(j.feedback,dt,{actors:this.actors,actionId:j.festival.activeId,actionSerial:j.festival.serial,
      secondsLeft:j.secondsLeft,duration:180,showId:j.booking?.showId,capacity:Audience.capacity(this.owner),
      serviceQueue:this.orders,paused:this.paused,musicPlaying:!j.musicPaused&&this._musicPlaybackActive!==false,soundLevel:this.state.upgrades[1],
      floorLitter:Floor.remaining(this.floor),guestAssistanceActive:this.floor.guestActive});
    this._refreshFeedback();
  }
  _feedbackDisappointment(guestId,reason,severity=1){
    if(!this.playing)return;
    SpectatorFeedback.disappoint(this.journey.feedback,{guestId,reason,severity});this._refreshFeedback();
  }
  _restoreFestivalArrivals(savedActors){
    const state=this.journey.festival,restoreCast=this.journey.inProgress||this.journey.session>0&&this.journey.settledSession===this.journey.session
      &&(!this._usesAudience()||this.journey.booking.lockedMetrics.venueOwner===this.owner);
    if(!restoreCast||!state?.arrivals)return;
    const base=this.actors.filter(Audience.isAudience),savedBase=savedActors?.filter(a=>Audience.isAudience(a)&&!a.audienceArrival
      &&/^(?:patron-\d+|audience-\d+|support-[0-2]|crowd-surfer|bar-seat-[0-3]|lounge-[0-6]|freebird-wall-[0-5]|ff-table-[0-4]-guest-[01])$/.test(a.id));
    const validBase=savedBase?.length===base.length&&new Set(savedBase.map(a=>a.id)).size===savedBase.length;
    const occupied=validBase?savedBase.filter(a=>a.visible!==false).length:base.filter(a=>a.visible!==false).length;
    const visitor=savedActors?.some(a=>a.id==='assisted-guest'&&a.role==='assisted'&&a.visible!==false)?1:0;
    const hiddenArrivals=savedActors?.filter(a=>a.audienceArrival&&a.visible===false&&a.audienceVersion===Audience.version).length||0;
    const available=Math.max(0,Audience.capacity(this.owner)-occupied-visitor),requested=Math.min(state.arrivals,available+hiddenArrivals);
    let restored=0;
    for(let serial=1;serial<=requested;serial++){
      const id=`arrival-${this.journey.session}-${serial}`,saved=savedActors?.find(a=>a.id===id&&a.audienceArrival===true&&a.audienceVersion===Audience.version&&a.role==='guest'
        &&Number.isFinite(a.x)&&Number.isFinite(a.z)&&Math.abs(a.x)<20&&Math.abs(a.z)<20);
      const actor=saved?{...saved}:Audience.arrival({owner:this.owner,showId:this.journey.booking?.showId,day:this.journey.day,session:this.journey.session,serial,actors:this.actors});
      if(!actor)break;this.actors.push(actor);restored++;
    }
    state.arrivals=restored;state.arrivalSerial=restored;
  }
  _manage(type,index){
    const s=this.state;if(!this.playing&&this.phase!=='planning'||this.phase==='planning'&&type==='event')return{success:false,code:'unavailable'};
    if(type==='payDebt'){if(!s.wageDebt||s.money<s.wageDebt)return{success:false,code:'insufficient_cash'};s.money-=s.wageDebt;s.wageDebt=0;s.wageTimer=60;return{success:true,code:'wages_paid'};}
    if(!Number.isInteger(index)||index<0||index>2)return{success:false,code:'invalid_index'};
    if((type==='hire'&&!s.staff[index]||type==='upgrade'&&s.upgrades[index]<3)&&!this.purchaseGate(type,index).allowed)return{success:false,code:'growth_locked'};
    if(type==='hire'){
      if(s.staff[index])return{success:false,code:'already_hired'};if(s.wageDebt)return{success:false,code:'wages_due'};if(s.money<catalog.staff[index].cost)return{success:false,code:'insufficient_cash'};
      s.money-=catalog.staff[index].cost;s.staff[index]=1;if(index===0)s.autoServeTimer=12;const a=this.actors.find(a=>a.id==='hired-'+index);if(a)a.visible=true;this._say(catalog.staff[index].name+' 고용 완료.',catalog.staff[index].nameEnglish+' hired.');return{success:true,code:'hired'};
    }
    if(type==='upgrade'){
      const level=s.upgrades[index];if(level>=3)return{success:false,code:'max_level'};const price=catalog.upgrades[index].cost[level];if(s.money<price)return{success:false,code:'insufficient_cash'};s.money-=price;s.upgrades[index]++;
      if(this.phase==='planning'&&Booking.isCampaign(this.journey.booking))Booking.refreshCampaignContext(this.journey.booking,this._campaignContext());
      this._say('시설 업그레이드 완료.','Facility upgraded.');return{success:true,code:'upgraded'};
    }
    const e=catalog.events[index];if(s.activeEvent>=0||s.cooldowns[index]>0)return{success:false,code:'event_unavailable'};if(s.money<e.cost)return{success:false,code:'insufficient_cash'};
    s.money-=e.cost;s.activeEvent=index;s.eventRemaining=e.duration;s.eventStartPours=s.pours;s.eventFanTimer=0;s.cooldowns[index]=e.duration+e.cooldown;this._requestTimer=Math.min(1,this._requestTimer);return{success:true,code:'event_started'};
  }
  _onDuty(i){return!!this.state.staff[i]&&!this.state.wageDebt;}
  _opsTick(dt){
    const s=this.state;s.cooldowns=s.cooldowns.map(x=>Math.max(0,x-dt));s.wageTimer-=dt;
    if(s.wageTimer<=0){if(!this.payroll){s.wageDebt=0;s.wageTimer=60;}else{if(!s.wageDebt)s.wageDebt=this.payroll;if(s.money>=s.wageDebt){s.money-=s.wageDebt;s.wageDebt=0;s.wageTimer=60;this._say('직원 급여를 지급했습니다.','Staff payroll paid.');}else{s.wageTimer=10;this._say('급여 미지급으로 고용 직원의 근무가 중단됐어요.','Wages are due. Hired staff are off duty.');}}}
    if(this._onDuty(0)){s.autoServeTimer-=dt;if(s.autoServeTimer<=0&&!this.pour){s.autoServeTimer=Math.max(6,12*(1-s.upgrades[0]*.12));if(this.orders.length)this._serve(this.orders[0].item,true,.7);}}
    if(this._onDuty(2)){s.promoterTimer+=dt;if(s.promoterTimer>=20){const n=Math.floor(s.promoterTimer/20);s.promoterTimer-=n*20;s.fans+=n;}}
    if(s.activeEvent>=0){const d=Math.min(dt,s.eventRemaining);s.eventRemaining=Math.max(0,s.eventRemaining-dt);if(s.activeEvent===2){s.eventFanTimer+=d;if(s.eventFanTimer>=10){s.fans+=Math.floor(s.eventFanTimer/10)*2;s.eventFanTimer%=10;}}if(s.eventRemaining<=0)this._completeEvent();}
  }
  _completeEvent(){const s=this.state;if(s.activeEvent<0)return;const e=catalog.events[s.activeEvent],success=s.pours-s.eventStartPours>=e.target;s.money+=success?e.reward:50;s.fans+=success?e.fans:3;if(success){s.completedEvents++;this.journey.successfulEvents++;this.journey.score+=250;}s.cooldowns[s.activeEvent]=Math.min(s.cooldowns[s.activeEvent],e.cooldown);s.activeEvent=-1;s.eventRemaining=0;this._say(success?'이벤트 성공! 보상과 팬을 획득했어요.':'이벤트가 종료됐어요. 다음 목표에 도전해 보세요.',success?'Event success! Cash and fans awarded.':'Event ended. Try the next target.');}
  _orderItem(){
    const festival=this.journey.festival;
    if(festival?.actionsUsed&&this._random()<Festival.modifiers(festival,this.state.satisfaction,this.state.performance).cocktailBoost)return this._random()<.5?0:1;
    const show=Booking.isCampaign(this.journey.booking)?Campaign.show(this.journey.booking.showId):null;
    if(show&&this._random()<.72)return show.preferredItems[Math.floor(this._random()*show.preferredItems.length)];
    return Math.floor(this._random()*catalog.drinks.length);
  }
  _seedOrders(){
    this.orders=[];
    if(this._usesAudience()){
      const pool=this.actors.filter(a=>this._orderGuest(a));for(let i=0;i<Math.min(3,pool.length);i++)this._assignOrder(pool[i].id,this._orderItem());return;
    }
    for(const [id,item]of[[1,0],[4,1],[8,2]])this._assignOrder('patron-'+id,Booking.isCampaign(this.journey.booking)?this._orderItem():item);
  }
  _assignOrder(id,item){
    const a=this.actors.find(a=>a.id===id);if(!a||a.visible===false||this.orders.some(o=>o.id===id))return;
    const s=this.state,patience=52+this._random()*16+(this._onDuty(1)?18:0)+s.upgrades[2]*6+(s.activeEvent===0?8:0)+(this.journey.challenge?-12:18);
    this.orders.push({id,name:a.name,item,patience,maxPatience:patience});this._routeOrders();
  }
  _createOrder(excludeId){if(this.orders.length>=4+this.state.upgrades[2])return;const pool=this.actors.filter(a=>this._orderGuest(a)&&a.id!==excludeId&&!this.orders.some(o=>o.id===a.id));if(!pool.length)return;const a=pool[Math.floor(this._random()*pool.length)];this._assignOrder(a.id,this._orderItem());}
  _routeOrders(){
    const slots=layoutForOwner(this.owner)?.orderSlots||orderSlots;
    for(const a of this.actors.filter(a=>this._orderGuest(a))){const i=this.orders.findIndex(o=>o.id===a.id);if(i>=0){const goal=slots[Math.min(i,6)];if(a.action!=='queue'||Math.hypot(a.targetX-goal.x,a.targetZ-goal.z)>.1||!a._path?.length&&distance(a,goal)>.2){a.action='queue';setRoute(a,goal,this.actors);}}
      else if(a.action==='queue'){a.action='return';setRoute(a,point(a.homeX,a.homeZ),this.actors);}}
  }
  _startPour(item){
    if(!this.playing||this.paused||this.pour||this._inputGuard>0)return{success:false,code:'unavailable'};
    const front=this.orders[0];if(!front){this._say('아직 주문이 없어요. 공연을 즐겨 보세요!','No orders yet. Enjoy the set!');return{success:false,code:'no_orders'};}
    if(front.item!==item){this._feedbackDisappointment(front.id,'wrong-item',.35);this._mistake();this._inputGuard=.15;this._say('손님의 주문과 다른 메뉴예요.','That is not the guest’s order.');return{success:false,code:'wrong_item'};}
    this.pour={item,elapsed:0,duration:3,remaining:3,marker:.04,target:.65+Math.sin(this.state.pours*.87+item*2.1)*.1,perfectWidth:this.journey.challenge?.09:.16,goodWidth:this.journey.challenge?.28:.4,orderId:front.id};this._inputGuard=.12;return{success:true,code:'pour_started'};
  }
  _stopPour(){if(!this.pour||!this.playing||this.paused||this._inputGuard>0)return{success:false,code:'unavailable'};const d=Math.abs(this.pour.marker-this.pour.target),q=d<=this.pour.perfectWidth*.5?1:d<=this.pour.goodWidth*.5?.65:.2;return this._finishPour(q);}
  _finishPour(quality){const p=this.pour;if(!p||this.orders[0]?.id!==p.orderId){this.pour=null;return{success:false,code:'stale_order'};}this.pour=null;this._inputGuard=.15;this.lastQuality=quality;return this._serve(p.item,false,quality);}
  _mistake(){const j=this.journey;j.mistakes++;j.combo=0;j.manualStreak=0;j.feverSeconds=0;j.feverCharge=0;}
  _serve(item,automatic,quality){
    const o=this.orders[0],s=this.state,j=this.journey;if(!o||o.item!==item||!this.playing||this.paused)return{success:false,code:'wrong_item'};
    this.orders.shift();const qualityTip=automatic?1:quality>=.9?1.4:quality>=.5?1:0,revenue=1+s.upgrades[0]*.08+(s.activeEvent===0?.10:s.activeEvent===2?.15:0),tips=1+s.upgrades[1]*.1+(s.activeEvent===1?.4:s.activeEvent===2?.2:0);
    const show=Booking.isCampaign(j.booking)?Campaign.show(j.booking.showId):null,showTips=show?show.tipMultiplier*(show.preferredItems.includes(item)?1.2:1):1;
    const tip=round(catalog.drinks[item].price*clamp(o.patience/30,0,1)*.45*tips*showTips*(j.feverSeconds>0?1.75:1)*qualityTip),sale=round(catalog.drinks[item].price*revenue),income=tip+sale;
    s.money=clamp(s.money+income,0,1e9);s.fans+=automatic||quality>=.5?1+(!automatic&&quality>=.9?1:0):0;s.pours++;
    SpectatorFeedback.service(j.feedback,{guestId:o.id,item,quality,automatic,patience:o.patience,maxPatience:o.maxPatience,preferred:!!show?.preferredItems.includes(item),crowdCount:this.festival.liveAttendance,capacity:Audience.capacity(this.owner)});this._refreshFeedback();
    j.orders++;j.drinkIncome+=income;if(!automatic){if(quality<.5)this._mistake();else{j.combo++;j.bestCombo=Math.max(j.bestCombo,j.combo);if(quality>=.9)j.manualPerfects++;j.manualStreak++;let multiplier=1+Math.min(20,j.combo-1)*.05;if(j.feverSeconds>0)multiplier*=1.3;j.score+=round((70+130*quality)*multiplier);j.feverCharge=clamp(j.manualStreak/5,0,1);if(!j.feverSeconds&&j.manualStreak>=5&&s.performance>=.72){j.feverSeconds=12;j.manualStreak=0;j.feverCharge=0;}}}
    this._routeOrders();if(!show)this._createOrder(o.id);else if(!this.orders.length)this._requestTimer=Math.min(this._requestTimer,2*this.festival.orderMultiplier);
    this._say((automatic?'바 보조 서빙':quality>=.9?'완벽한 서빙!':quality>=.5?'잘 서빙했어요!':'타이밍을 놓쳤어요 — 팁 없음')+' +'+this.formatMoney(income),(automatic?'Barback served':quality>=.9?'Perfect service!':quality>=.5?'Good service!':'Missed service — no tip')+' +'+this.formatMoney(income));return{success:true,code:automatic?'auto_served':quality>=.9?'perfect':quality>=.5?'good':'miss',quality,income};
  }
  tick(dt){
    if(!Number.isFinite(dt)||dt<=0)return;dt=Math.min(dt,5);let remaining=dt;
    while(remaining>0){const step=Math.min(.05,remaining);remaining-=step;this._step(step);}this._notify();
  }
  _inspectionNotice(kind){
    if(kind==='warning')this._say('구청 민원 접수 · 춤추지 말라는 신고가 들어왔어요. 단속반 3명이 잠시 방문합니다.','A no-dancing complaint was reported. Three inspectors are visiting shortly.',5);
    else if(kind==='arrived')this._say('단속반 방문 중 · 모두 잠시 멈춰 공연을 지켜봅니다.','Inspectors are here. Everyone pauses to watch the stage.',4);
    else if(kind==='done')this._say('단속반이 지나갔어요. 다시 자유롭게 공연에 호응하세요!','The inspectors have left. Crowd actions are available again!',4);
  }
  _inspectionTick(dt){
    const notices=Inspection.tick(this.journey.inspection,dt,{actors:this.actors,owner:this.owner,playing:this.playing,showElapsed:180-this.journey.secondsLeft,canEnter:!this.escort||this.escort.phase==='return'});
    if(!this.inspection.freezePerformers)for(const actor of this.actors){
      if(!actor._inspectionPose)continue;
      actor.facing=actor._inspectionPose.facing;actor.y=this.playing||actor.role==='band'?actor._inspectionPose.y:0;delete actor._inspectionPose;
    }
    for(const notice of notices)this._inspectionNotice(notice);
  }
  _step(dt){
    if(this.paused||(!this.playing&&!this.closing))return;this.elapsed+=dt;this._inputGuard=Math.max(0,this._inputGuard-dt);this._messageTimer-=dt;if(this._messageTimer<=0)this._message=['',''];
    const j=this.journey,s=this.state;this.effects=this.effects.filter(e=>this.elapsed-e.time<8);
    this._inspectionTick(dt);const frozen=this.inspection.freezePerformers;
    if(this.playing){
      j.secondsLeft=Math.max(0,j.secondsLeft-dt);j.feverSeconds=Math.max(0,j.feverSeconds-dt);if(j.secondsLeft<=0){this._lastCall();return;}
      if(Booking.isCampaign(j.booking)&&j.booking.showId==='double'&&!j.campaignSecondSet&&j.secondsLeft<=90){
        j.campaignSecondSet=true;s.playlist=1;this._say('2부 시작! 줄라이원츠모어가 무대에 오릅니다.','Second set! JulyWantsMore takes the stage.');
      }
      if(!frozen)this._festivalTick(dt);
      if(this.pour){this.pour.elapsed+=dt;this.pour.remaining=Math.max(0,3-this.pour.elapsed);const v=(.04+this.pour.elapsed*.82)%2;this.pour.marker=v<=1?v:2-v;if(!this.pour.remaining)this._finishPour(.2);}
      this._opsTick(dt);s.clock=18+6*(1-j.secondsLeft/180);if(!frozen)this._requestTimer-=dt;if(!frozen&&this._requestTimer<=0){this._createOrder();this._requestTimer=(4.5+this._random()*3)*(this._onDuty(2)?.9:1)*(s.activeEvent===2?.7:1)*(j.challenge?.72:1.18)*this.metrics().orderIntervalMultiplier*this.festival.orderMultiplier;}
      if(!frozen)for(const o of this.orders)if(!this.pour||o.id!==this.pour.orderId)o.patience-=dt;
      if(this.orders[0]?.patience<=0&&!this.pour){const missed=this.orders.shift();this._feedbackDisappointment(missed.id,'timeout');s.fans=Math.max(0,s.fans-3);this._mistake();this._routeOrders();this._say('손님이 주문을 받지 못하고 떠났어요.','A guest left without their order.');}
    }
    this._tickActors(dt);if(this.playing&&!frozen)this._feedbackTick(dt);if(this.closing&&!frozen)tickAfterShow(j.afterShow,this.actors,dt);
    if(this.farewellPending){this.paused=true;this.pauseReason='farewell';this.save();}
    this._floorTick(dt);this._autosave+=dt;if(this._autosave>=3){this._autosave=0;this.save();}
  }
  _floorTick(dt){
    this._ensureReaction();const r=this.reaction,f=this.floor;
    if(this.playing&&!this.inspection.freezePerformers){const due=Challenges.advance(r,180-this.journey.secondsLeft,true);for(let i=0;i<due.confetti;i++)this._paper(0);for(let i=0;i<due.tissue;i++)this._paper(1);if(due.guest&&!f.guestSpawned){Floor.spawnGuest(f,this.journey.day%10);this._ensureGuest();this._say('손님 안내 미션! F 키나 버튼을 빠르게 연타하세요.','Guest assistance! Rapidly tap F or the button.');}}
    this._ensureCleanup();this._tickFloorActors(dt);
    const timeout=Challenges.tick(r,dt,!(this.inspection.freezePerformers&&(r.activeKind===2||f.guestActive)));if(timeout.timedOut){if(f.guestActive)this._feedbackDisappointment(this.actors.find(a=>a.id==='assisted-guest')?.assistedSourceId||'assisted-guest','unsafe',.3);this._say('시간을 놓쳤어요. 잠시 뒤 다시 연타로 대응하세요!','Time ran out. A new response window will appear shortly.');}
    if(this.closing){const ready=this.cleanupReadyWorkers,before=f.cleaned,workDt=ready?Math.min(dt,r.cleanupWorkBudget/(ready*3)):0,result=Floor.tick(f,workDt,ready,true);r.cleanupWorkBudget=Math.max(0,r.cleanupWorkBudget-(f.cleaned-before));if(r.cleanupWorkBudget<.001)r.cleanupWorkBudget=0;if(result.cleanupFinished){this.journey.score+=150;this._say('직원 청소 미션 완료!','Staff cleanup mission complete!');}}
    if(!r.activeKind&&r.cooldown<=0){if(this.closing&&f.cleanupActive&&this.cleanupReadyWorkers>0&&r.cleanupWorkBudget<=0)Challenges.start(r,1,Math.floor(f.cleaned/32));else if(f.guestActive)Challenges.start(r,2,f.guestStep);}
    if(this.closing&&!this.floorTasksPending&&!this.afterShowPending&&!this.farewellPending)this._settleNight();
  }
  _paper(kind){this._emitPaper(kind,Floor.throw(this.floor,kind,this.playing));}
  _emitPaper(kind,result){if(!result.success)return;this.state.fans+=result.fanGain;this.effects.push({kind,serial:++this._eventSerial,time:this.elapsed});this._say(kind===0?'밴드가 깜짝 꽃가루 퍼포먼스를 했어요!':'관객 위로 휴지가 날립니다!',kind===0?'The band unexpectedly throws confetti!':'Tissue streamers fly over the crowd!');}
  _quick(kind){
    if(this.paused||(!this.playing&&!this.closing)||!this.reaction)return{success:false,code:'unavailable'};
    if(kind===2&&this.inspection.freezePerformers){this._say('단속반이 지나가면 손님 안내를 다시 진행할 수 있어요.','Guest assistance resumes after the inspectors leave.',4);return{success:false,code:'inspection_blocked'};}
    if(kind===2&&(!this.floor.guestActive||this.reaction.activeStage!==this.floor.guestStep))return{success:false,code:'no_guest'};
    if(kind===1&&(!this.closing||!this.floor.cleanupActive||!this.cleanupReadyWorkers))return{success:false,code:'no_workers'};
    const result=Challenges.press(this.reaction,kind,true);if(result.completed){if(kind===2){const guest=Floor.resolveGuest(this.floor,result.stage);if(guest.guestCompleted){this.state.fans+=4;this.journey.score+=150;this._beginEscort();this._say('감사합니다. 직원이 출입구까지 동행하겠습니다.','Thank you. A staff member will accompany you to the exit.');}else this._say(result.stage===0?'추가 주문을 멈추고 물을 안내했어요.':'정중한 퇴점 요청을 전했어요. 소지품을 확인하세요.',result.stage===0?'Service paused and water offered.':'A courteous exit request was understood. Check belongings.');}else this._say('빠르게 대응했어요! 직원들이 바닥을 정리합니다.','Great response! Arrived staff are clearing the paper.');}return{...result,success:result.accepted,code:result.completed?'reaction_complete':result.accepted?'reaction_press':'reaction_ignored'};
  }
  _lastCall(){this.pour=null;this.journey.secondsLeft=0;this.state.clock=24;this._beginClosing();}
  _beginAfterShow(){
    const j=this.journey;
    if(!j.afterShow||j.afterShow.session!==j.session){j.afterShow=createAfterShow(this.actors,j.session);restoreAfterShow(j.afterShow,this.actors);}
  }
  _beginClosing(){
    const j=this.journey;this.phase='closing';j.floorClosing=true;j.inProgress=true;j.settlementPending=false;j.guidePending=false;
    Inspection.requestExit(j.inspection,this.actors);
    this._clearFestivalActors();j.festival.activeId='';j.festival.remaining=0;j.festival.globalRemaining=0;
    if(j.musicPauseSession!==j.session){j.musicPauseSession=j.session;j.musicPaused=true;}
    this._beginAfterShow();this.orders=[];this._routeOrders();this._ensureReaction();
    // The random mid-set schedule remains random; the final bow supplies only
    // missing kinds before staff claim cleanup, while greetings are starting.
    for(const burst of Floor.ensureFinale(this.floor,!j.afterShow.completed&&j.session!==j.settledSession))this._emitPaper(burst.kind,burst);
    this._ensureCleanup();
    this.paused=this.farewellPending;this.pauseReason=this.paused?'farewell':'';
    this._say('공연이 끝났어요. C 청소·F 손님 안내 미션을 마쳐 주세요.','Show over. Finish C cleanup and F guest assistance.');
  }
  _ensureCleanup(){
    const f=this.floor;if(!this.closing||Floor.remaining(f)<=0)return;if(!f.cleanupActive&&!f.cleanupComplete)Floor.startCleanup(f,this.availableWorkers,true);
    if(!this.crew.length){this.crew=this.actors.filter(a=>(a.id==='bartender'||a.id==='server'||a.id.startsWith('hired-')&&this._onDuty(+a.id.slice(-1)))&&a.visible).map(a=>a.id);f.cleanupWorkers=this.crew.length;for(let i=0;i<this.crew.length;i++){const a=this.actors.find(x=>x.id===this.crew[i]);a.action='cleaning';a._ready=false;setCrowdRoute(a,this._cleanGoal(i),this.actors,false);}}
  }
  _settleNight(){
    const j=this.journey;if(j.session<=0||j.session===j.settledSession){j.settlementPending=false;j.inProgress=false;j.floorClosing=false;this.phase='results';this.paused=true;this.pauseReason='results';return;}
    if(this.floorTasksPending||j.inProgress&&(this.afterShowPending||this.farewellPending)){this._beginClosing();return;}
    this._completeEvent();const targets=this.goalTargets(),values=this.goals(),goals=values.filter((v,i)=>v>=targets[i]).length,stars=goals===3?3:goals>=2?2:1;
    const bonus=(j.challenge?130:75)+goals*(j.challenge?100:65)+stars*40,xp=25+goals*25+stars*15+(j.challenge?35:0);
    j.resultStars=stars;j.resultServed=j.orders;j.resultXP=xp;j.resultBonus=bonus;j.resultNewRecord=j.score>j.bestScore;j.bestScore=Math.max(j.bestScore,j.score);j.bestStars=Math.max(j.bestStars,stars);j.xp+=xp;j.completedNights++;this.state.money+=bonus;
    if(j.booking?.locked&&!j.booking.completed){const forecast=Booking.forecast(j.booking),receipt=Booking.complete(j.booking,j.bookingCareer,stars);if(receipt.success){this.state.money+=receipt.cashDelta;this.state.fans+=forecast.fanBonus;j.resultTicketIncome=receipt.income;j.resultTicketGross=receipt.grossIncome??receipt.income;j.resultArtistFee=receipt.fee??0;j.resultAttendance=receipt.attendance;}}
    const cleanup=Floor.settle(this.floor);if(cleanup.success){this.state.money+=cleanup.cashDelta;this.state.fans+=cleanup.fanGain;j.resultFloorBonus=cleanup.cashDelta;}
    j.resultFloorTasks=(this.floor.cleanupComplete?1:0)+(this.floor.guestComplete?1:0);j.resultProfit=this.state.money-j.openingCash;j.settledSession=j.session;j.settlementPending=false;j.inProgress=false;j.floorClosing=false;j.guidePending=false;j.feverSeconds=0;j.combo=0;
    this.state.money=clamp(this.state.money,0,1e9);this.state.fans=int(this.state.fans,0,1e7);
    this.phase='results';this.paused=true;this.pauseReason='results';this.orders=[];this.pour=null;this.save();
  }
  _buildActors(savedActors=null){
    const make=(id,name,role,x,z,y=0)=>({id,name,role,x,z,y,homeX:x,homeZ:z,targetX:x,targetZ:z,moving:false,action:role==='band'?'perform':'idle',visible:true,facing:0,_path:[],_pathAt:0,_wait:2});
    this.actors=[make('bartender','Jessy Bartender','bartender',-7.3,-.565),make('server','Server','server',-4.2,-.565),make('singer','DP Singer / Dragon Pony','band',5.7,3.5),make('guitarist','RUX Guitarist','band',3.8,4.25),make('bassist','FW Bassist / freewill','band',7.7,4.25),make('drummer','JW Drummer / Julywantsmore','band',5.75,5.55)];
    const bandFacing={singer:-Math.PI/2,guitarist:-Math.PI*.38,bassist:Math.PI*.38,drummer:-Math.PI/2};
    for(const a of this.actors)if(a.role==='band'){a.facing=bandFacing[a.id]??0;a.stageLevel=1;a.actionProgress=0;}
    const positions=[[.1,.7],[1.5,.5],[2.85,.5],[-.1,-.8],[1.2,-1.1],[2.6,-1],[-.2,-2.55],[1.2,-2.6],[2.6,-2.75],[0,-4.4],[1.55,-4.3],[3,-4.55],[8.55,.65],[8.65,-1],[8.6,-2.7],[8.6,-4.7]];
    for(let i=0;i<16;i++){let [x,z]=positions[i];if(i<6){x=5.75+Math.cos(i*Math.PI/3)*1.58;z=-1.25+Math.sin(i*Math.PI/3)*1.58;}else if(i<8){x=5.75+(i===6?-.68:.68);z=-1.25;}const a=make('patron-'+i,catalog.guestNames[i%10],'guest',x,z);a.slot=i;this.actors.push(a);}
    for(let i=0;i<4;i++)this.actors.push(make('bar-seat-'+i,'Bar Guest '+i,'seated',-7.55+i*1.55,-3.15));
    loungeHomes.forEach(([x,z],i)=>this.actors.push(make('lounge-'+i,'Lounge Guest '+i,'seated',x,z)));
    this.actors.filter(a=>a.id?.startsWith('bar-seat-')).forEach((a,i)=>{a.facing=[Math.PI/2,-Math.PI/2,Math.PI*.2,-Math.PI*.2][i];});
    this.actors.filter(a=>a.id?.startsWith('lounge-')).forEach((a,i)=>{a.facing=[-Math.PI/2,Math.PI/2,0,Math.PI/2,-Math.PI/2,Math.PI*.25,-Math.PI*.25][i];});
    for(let i=0;i<3;i++){const a=make('hired-'+i,catalog.staff[i].nameEnglish,'hired-'+i,...[[-5.8,-.565],[-.4,-4.9],[8.75,-3.5]][i]);a.visible=!!this.state.staff[i]&&!this.state.wageDebt;this.actors.push(a);}
    for(let i=0;i<3;i++)this.actors.push(make('support-'+i,'Crowd Supporter '+i,'support',6+(i===0?-.65:i===1?.65:0),-4.25+(i===2?-.65:0)));
    this.actors.push(make('crowd-surfer','Crowd Surfer','surfer',6,-4.25));
    if(this._usesAudience()){
      const p=this.journey.booking,m=Booking.forecast(p);
      this.actors=this.actors.filter(a=>!Audience.isAudience(a));
      this.actors.push(...Audience.build({owner:m.venueOwner,attendance:m.attendance,showId:p.showId,day:p.day,session:this.journey.session}));
    }
    this._restoreFestivalArrivals(savedActors);
    if(layoutForOwner(this.owner))this._applyOwnerLayout({reset:true});
  }
  _walkable(p,staff=false,floor=false){return walkable(p,staff,floor,layoutForOwner(this.owner)?.id);}
  _applyOwnerLayout({reset=false}={}){
    const layout=layoutForOwner(this.owner);let changed=false;
    for(const a of this.actors){
      // Campaign audiences already occupy safe, venue-specific homes. Applying
      // the former eleven fixed standing positions would stack the new crowd.
      if(a.audienceVersion===Audience.version){
        if(layout&&(a.layout!==layout.id||a.layoutVersion!==layout.version)){
          const currentBlocked=!this._walkable(a,false,false),homeBlocked=!this._walkable(point(a.homeX,a.homeZ),false,false);
          if(currentBlocked||homeBlocked){
            const safe=Audience.safePoint({owner:this.owner,actor:a,actors:this.actors,day:this.journey.day,session:this.journey.session});
            if(safe){
              if(homeBlocked){a.homeX=safe.x;a.homeZ=safe.z;}
              if(currentBlocked){a.x=safe.x;a.z=safe.z;a.y=0;a.moving=false;}
            }
          }
          a.layout=layout.id;a.layoutVersion=layout.version;a._path=[];a._pathAt=0;a._blocked=0;
          if(a.action==='return'){a.targetX=a.homeX;a.targetZ=a.homeZ;}
          changed=true;
        }
        continue;
      }
      const migrated=layout?(a.layout!==layout.id||a.layoutVersion!==layout.version):a.layout==='ff'||a.layout==='freebird';
      if(layout){a.layout=layout.id;a.layoutVersion=layout.version;}else{delete a.layout;delete a.layoutVersion;}
      if(migrated||reset){changed=true;a._path=[];a._pathAt=0;a._blocked=0;a.moving=false;delete a._cleanupGoal;delete a._cleanupGoalSegment;delete a._cleanupParkGoal;delete a._cleanupParkSession;}
      let home=null;
      if(a.id.startsWith('bar-seat-')||a.id.startsWith('lounge-')){
        const index=a.id.startsWith('bar-seat-')?+a.id.slice(9):4+(+a.id.slice(7));
        home=layout?layout.standing[index]:seatedHome(a.id);a.role=layout||a.seatReleased?'guest':'seated';
      }else if(a.id==='bartender')home=layout?layout.bartender:point(-7.3,-.565);
      else if(a.id==='server')home=layout?layout.server:point(-4.2,-.565);
      else if(a.id==='hired-0')home=layout?layout.barback:point(-5.8,-.565);
      else if(a.id==='hired-1')home=layout?.floorhost||point(-.4,-4.9);
      if(home){
        a.homeX=home.x;a.homeZ=home.z;
        if(migrated||reset){a.x=a.targetX=home.x;a.z=a.targetZ=home.z;a.y=0;a.action='idle';}
      }else if((migrated||reset)&&a.role!=='band'){
        const staff=a.role==='bartender'||a.role==='server'||a.id.startsWith('hired-');
        // A guest at one owner's counter cannot remain inside another owner's
        // restored furniture or expanded stage when the layout changes.
        if(!this._walkable(a,staff,false)){
          const candidate=point(a.homeX,a.homeZ),fallback=point(1,0);
          const safe=Number.isFinite(candidate.x)&&Number.isFinite(candidate.z)&&this._walkable(candidate,staff,false)?candidate:fallback;
          a.x=a.targetX=safe.x;a.z=a.targetZ=safe.z;a.y=0;
        }
      }
    }
    return changed;
  }
  _cleanGoal(index){
    const segment=Math.min(5,Math.floor(Floor.progress(this.floor)*6));let goal={...cleanStops[(segment+(index%2)*3)%6]};if(index>=2){goal.x+=index<4?-.45:.38;goal.z+=index<4?.32:-.32;if(!this._walkable(goal,true,true))goal={...cleanStops[(segment+(index%2)*3)%6]};}
    const worker=this.actors.find(a=>a.id===this.crew[index]);if(!worker)return goal;
    // A returned fan keeps their home position. Staff choose a nearby clear
    // working spot and retain it until this section of the floor is finished.
    const bodies=this.actors.filter(a=>a!==worker&&a.visible&&a.role!=='band'&&a.role!=='seated'&&!this.crew.includes(a.id));
    const homes=(this.journey.afterShow?.audience||[]).map(a=>point(a.homeX,a.homeZ));
    const reserved=this.crew.filter(id=>id!==worker.id).map(id=>this.actors.find(a=>a.id===id))
      .filter(a=>a?._cleanupGoalSegment===segment&&a._cleanupGoal).map(a=>a._cleanupGoal);
    const clear=p=>this._walkable(p,true,true)&&bodies.every(a=>distance(a,p)>=.4)&&homes.every(p2=>distance(p2,p)>=.45)&&reserved.every(p2=>distance(p2,p)>=.45);
    if(worker._cleanupGoalSegment===segment&&worker._cleanupGoal&&clear(worker._cleanupGoal))return worker._cleanupGoal;
    const candidates=[goal];for(const radius of [.45,.7,1])for(let turn=0;turn<8;turn++){
      const angle=(turn+index*2)*Math.PI/4;candidates.push(point(goal.x+Math.cos(angle)*radius,goal.z+Math.sin(angle)*radius));
    }
    worker._cleanupGoal=candidates.find(clear)||goal;worker._cleanupGoalSegment=segment;
    return worker._cleanupGoal;
  }
  _parkGoal(index){
    const worker=this.actors.find(a=>a.id===this.crew[index]);if(!worker)return point(0,0);
    const homes=(this.journey.afterShow?.audience||[]).map(a=>point(a.homeX,a.homeZ));
    const bodies=this.actors.filter(a=>a!==worker&&a.visible&&a.role!=='band'&&a.role!=='seated'&&!this.crew.includes(a.id));
    const reserved=this.crew.filter(id=>id!==worker.id).map(id=>this.actors.find(a=>a.id===id))
      .filter(a=>a?._cleanupParkSession===this.journey.session&&a._cleanupParkGoal).map(a=>a._cleanupParkGoal);
    const clear=p=>this._walkable(p,true,false)&&homes.every(p2=>distance(p2,p)>=.45)&&bodies.every(a=>distance(a,p)>=.4)&&reserved.every(p2=>distance(p2,p)>=.45);
    if(worker._cleanupParkSession===this.journey.session&&worker._cleanupParkGoal&&clear(worker._cleanupParkGoal))return worker._cleanupParkGoal;
    const home=point(worker.homeX,worker.homeZ),candidates=[home];
    for(const radius of [.45,.7,1])for(let turn=0;turn<8;turn++){
      const angle=(turn+index*2)*Math.PI/4;candidates.push(point(home.x+Math.cos(angle)*radius,home.z+Math.sin(angle)*radius));
    }
    worker._cleanupParkGoal=candidates.find(clear)||home;worker._cleanupParkSession=this.journey.session;
    return worker._cleanupParkGoal;
  }
  _readyWorkers(){if(!this.floor.cleanupActive||this.floor.cleanupComplete)return[];return this.crew.map((id,i)=>({a:this.actors.find(a=>a.id===id),i})).filter(({a,i})=>a&&a.visible&&a.id!==this.escort?.worker&&(!a.id.startsWith('hired-')||this._onDuty(+a.id.slice(-1)))&&distance(a,this._cleanGoal(i))<=.22).map(x=>x.a);}
  _ensureGuest(){
    let actor=this.actors.find(a=>a.id==='assisted-guest');
    const created=!actor;
    if(!actor){
      actor={id:'assisted-guest',name:catalog.guestNames[this.floor.guestIndex%10]||'Guest',role:'assisted',x:-.65,z:-.25,y:0,homeX:-.65,homeZ:-.25,targetX:-.65,targetZ:-.25,moving:false,action:'needs-help',visible:true,_path:[],_pathAt:0};
      const layout=layoutForOwner(this.owner);if(layout){actor.layout=layout.id;actor.layoutVersion=layout.version;}
      this.actors.push(actor);
    }
    if(actor.visible!==false&&!actor.assistedSourceId){
      const candidates=this.actors.filter(a=>a.role==='guest'&&a.visible!==false&&!a.festivalLead&&!a.festivalSupport
        &&!['queue','return','arrive','to-stage'].includes(a.action)&&!this.orders.some(order=>order.id===a.id));
      candidates.sort((a,b)=>(a.id.startsWith('audience-')?0:1)-(b.id.startsWith('audience-')?0:1)||distance(a,point(-.65,-.25))-distance(b,point(-.65,-.25)));
      const source=candidates[0];
      if(source){
        actor.assistedSourceId=source.id;actor.styleKey=Audience.wardrobe(source);actor.slot=source.slot;
        actor.name=source.name;
        if(created){actor.x=actor.homeX=actor.targetX=source.x;actor.z=actor.homeZ=actor.targetZ=source.z;}
        source.visible=false;source.moving=false;source.reservedForAssistance=true;source._path=[];source._pathAt=0;
      }
    }
    return actor;
  }
  _escortFollowGoal(worker,guest,preferred){
    const clear=goal=>this._walkable(goal,true,false)&&this.actors.every(a=>a===worker||a===guest||a.visible===false||a.role==='band'||distance(a,goal)>=.37);
    if(clear(preferred))return preferred;
    // The fixed trailing side can be inside the FF DJ booth or stage edge.
    // Choose an actual clear side of the guest instead of waiting forever at
    // that blocked offset. The ordinary route/movement checks still apply.
    const alternatives=[];
    for(const radius of [.55,.75])for(let turn=0;turn<12;turn++){
      const angle=turn*Math.PI/6,goal=point(guest.x+Math.cos(angle)*radius,guest.z+Math.sin(angle)*radius);
      if(clear(goal))alternatives.push(goal);
    }
    alternatives.sort((a,b)=>distance(worker,a)+distance(preferred,a)*.2-distance(worker,b)-distance(preferred,b)*.2);
    return alternatives[0]||null;
  }
  _beginEscort(){const guest=this._ensureGuest(),worker=this.actors.find(a=>a.id==='server');this.escort={worker:worker.id,phase:'join',homeX:worker.homeX,homeZ:worker.homeZ};worker.action='escort';worker._partner=guest.id;guest._partner=worker.id;const goal=this._escortFollowGoal(worker,guest,point(guest.x-.5,guest.z-.3));if(goal)(this.closing?setCrowdRoute:setRoute)(worker,goal,this.actors,false);}
  _tickBartender(a,dt){
    if(!this.playing){a.moving=false;a.action='idle';return;}
    // The authored staff aisle is straight and clear. Keep the same guarded
    // 9-second work cycle as the Unity bartender, without a grid-path zigzag.
    const cycle=this.elapsed%9,x=this.pour?-9+2*clamp(this.pour.item/2,0,1):cycle<4.5?-8.95:-7.05;
    const goal=this.owner===0?point(this.pour?-5.3+1.25*clamp(this.pour.item/5,0,1):cycle<4.5?-5.3:-4.05,4.85)
      :this.owner===1?point(-8.9,this.pour?-1.2+1.7*clamp(this.pour.item/5,0,1):cycle<4.5?-1.2:.5):point(x,-.565);
    const changed=distance(point(a.targetX,a.targetZ),goal)>.01;
    if(changed||distance(a,goal)>.035&&(!a._path?.length||a._pathAt>=a._path.length)){
      a.targetX=goal.x;a.targetZ=goal.z;a._floorRoute=false;a._path=[goal];a._pathAt=0;a._blocked=0;
    }
    if(distance(a,goal)>.035)move(a,dt,.66,this.actors);
    if(distance(a,goal)<=.035){a._path=[];a._pathAt=0;a.moving=false;a.action='serve';}
    else a.action='walk';
  }
  _tickActors(dt){
    for(const a of this.actors){
      if(a.id.startsWith('hired-'))a.visible=this._onDuty(+a.id.slice(-1));if(!a.visible)continue;
      if(a.role==='inspector')continue;
      if(this.inspection.freezePerformers&&(Audience.isAudience(a)||a.role==='assisted'||a.id===this.escort?.worker&&this.escort.phase!=='return')){
        if(!a._inspectionPose)a._inspectionPose={facing:a.facing,y:a.y||0};
        a.moving=false;if(a.role!=='band'){a.y=0;a.facing=Math.atan2(5.75-a.x,3.5-a.z);}continue;
      }
      if(this.crew.includes(a.id)||a.id===this.escort?.worker||a.id==='assisted-guest')continue;
      if(this.closing&&ownsAfterShowActor(this.journey.afterShow,a.id))continue;
      if(a.role==='band'){
        if(this.playing){a.action='perform';a.moving=false;continue;}
        a.action='idle';a.moving=false;
        continue;
      }if(a.role==='seated'){
        if(a.waitingSeat&&this.playing){
          a.waitRemaining=Math.max(0,(a.waitRemaining||0)-dt);
          if(a.waitRemaining<=0){
            a._seatRouteRetry=Math.max(0,(a._seatRouteRetry||0)-dt);
            if(!a._seatRouteRetry){
              const goal=Audience.frontTarget({owner:this.owner,actor:a,actors:this.actors,day:this.journey.day,session:this.journey.session});
              if(goal&&setRoute(a,goal,this.actors,false)){a.role='guest';a.seat=false;a.waitingSeat=false;a.seatReleased=true;a.homeX=goal.x;a.homeZ=goal.z;a.behavior='cheer';a.action='to-stage';continue;}
              a._seatRouteRetry=1.2;
            }
          }
        }
        if(a.audienceVersion===Audience.version){a.action=this.playing?a.behavior:'idle';a.moving=false;}
        continue;
      }
      if(a.id==='bartender'){this._tickBartender(a,dt);continue;}
      if(!this.playing&&(a.action==='mosh'||a.action==='slam')){a.action='idle';a.moving=false;}
      if(['queue','return','arrive','to-stage'].includes(a.action)){
        if(['return','arrive','to-stage'].includes(a.action)&&(!a._path?.length||a._pathAt>=a._path.length)&&distance(a,point(a.targetX,a.targetZ))>.2){
          a._crowdRetry=Math.max(0,(a._crowdRetry||0)-dt);if(!a._crowdRetry){setRoute(a,point(a.targetX,a.targetZ),this.actors,false);a._crowdRetry=.8;}
        }
        if(move(a,dt,1.45,this.actors)){a.moving=false;if(a.action==='to-stage')a.seatStageReached=true;if(a.action!=='queue')a.action=a.behavior||'idle';}
        continue;
      }
      if(a.audienceVersion===Audience.version){this._tickAudience(a,dt);continue;}
      if(a.role==='guest'&&a.slot<6&&this.playing){
        // A patron returning from the bar rejoins at their current angle. The
        // shared clock has advanced while they queued, so its old phase would
        // otherwise snap them across the pit. The offset travels in native saves.
        if(a.action!=='mosh'||!Number.isFinite(a._pitPhaseOffset))a._pitPhaseOffset=Math.atan2(a.z+1.25,a.x-5.75)-a.slot*Math.PI/3-this._performanceElapsed*.25;
        const angle=a.slot*Math.PI/3+this._performanceElapsed*.25+a._pitPhaseOffset;
        const currentRadius=Math.hypot(a.x-5.75,a.z+1.25),radius=currentRadius+clamp(1.58-currentRadius,-dt*.65,dt*.65);
        a.x=5.75+Math.cos(angle)*radius;a.z=-1.25+Math.sin(angle)*radius;a.moving=true;a.action='mosh';a.facing=angle+Math.PI/2;
      }
      else if(a.role==='guest'&&a.slot<8&&this.playing){
        const side=a.slot===6?-1:1,separation=.44+Math.abs(Math.sin(this._performanceElapsed*1.6))*.24,goal=point(5.75+side*separation,-1.25);
        const d=distance(a,goal),step=Math.min(d,dt*.65);
        if(d>0){a.x+=(goal.x-a.x)*step/d;a.z+=(goal.z-a.z)*step/d;}
        a.action='slam';a.moving=step>.001;a.facing=side<0?Math.PI/2:-Math.PI/2;
      }
      else if(a.role==='surfer'){const t=this._performanceElapsed%24;a.y=this.playing&&t>=2&&t<=18?Math.min(1,(t-2)/2,(18-t)/2)*1.85:0;a.action=a.y>0?'surf':'idle';a.facing=Math.sin(this._performanceElapsed*.8)>.2?-Math.PI/2:Math.PI/2;}
      else if(a.role==='support'){a.action=this.playing?'support':'idle';const surfer=this.actors.find(v=>v.id==='crowd-surfer');if(surfer)a.facing=Math.atan2(surfer.z-a.z,surfer.x-a.x);}
      else if(a.role==='guest'&&a.action!=='queue'){
        if(a._pathAt<a._path.length){if(move(a,dt,1.1,this.actors)){a.action='idle';a._wait=2+this._random()*5;}}
        else{a.moving=false;a._wait-=dt;if(a._wait<=0){const goal=point(1+this._random()*7,-5+this._random()*5.5);if(this._walkable(goal,false,true)&&this._walkable(a,false,true)){a.action='wander';setRoute(a,goal,this.actors,true);}a._wait=3+this._random()*5;}}
      }else{a.moving=false;a.action=this.playing?'serve':'idle';}
    }
  }
  _tickAudience(a,dt){
    if(!this.playing){a.action='idle';a.moving=false;a.y=0;return;}
    if(a.festivalAction==='surf'&&a.festivalRemaining>0){
      if(a.festivalSupport){
        const target=a.festivalTarget;
        if(target&&distance(a,target)>.18){
          a._festivalRouteRetry=Math.max(0,(a._festivalRouteRetry||0)-dt);
          if((!a._path?.length||a._pathAt>=a._path.length)&&!a._festivalRouteRetry){setRoute(a,target,this.actors,false);a._festivalRouteRetry=.4;}
          move(a,dt,1.65,this.actors);a.action='walk';return;
        }
        a.action='support';a.moving=false;return;
      }
      if(a.festivalLead){
        const supporters=this.actors.filter(other=>other.festivalSupport&&other.festivalSerial===a.festivalSerial);
        a.festivalLiftReady=supporters.length===3&&supporters.every(other=>other.visible!==false&&other.action==='support'&&other.festivalTarget&&distance(other,other.festivalTarget)<=.22&&distance(other,a)<.85);
        a.festivalLiftElapsed=clamp((a.festivalLiftElapsed||0)+(a.festivalLiftReady?dt:-dt*2),0,1.2);
        a.y=Math.min(a.festivalLiftElapsed/1.2,a.festivalRemaining/1.2)*1.35;
        a.action=a.festivalLiftReady&&a.y>.05?'surf':'listen';a.moving=false;return;
      }
    }
    if(a.behavior==='mosh'){
      const spacing=Math.PI*2/Math.max(1,a.pitCount),base=(a.pitIndex||0)*spacing,speed=this.owner===1?.31:.21;
      if(a.action!=='mosh'||!Number.isFinite(a._pitPhaseOffset))a._pitPhaseOffset=Math.atan2(a.z+1.25,a.x-5.75)-base-this._performanceElapsed*speed;
      const angle=base+this._performanceElapsed*speed+a._pitPhaseOffset,current=Math.hypot(a.x-5.75,a.z+1.25),radius=current+clamp((a.pitRadius||1.58)-current,-dt*.65,dt*.65);
      a.x=5.75+Math.cos(angle)*radius;a.z=-1.25+Math.sin(angle)*radius;a.action='mosh';a.moving=true;a.facing=angle+Math.PI/2;return;
    }
    if(a.behavior==='slam'){
      const side=a.slamSide||1,separation=.44+Math.abs(Math.sin(this._performanceElapsed*1.85))*.24,goal=point(5.75+side*separation,-1.25),d=distance(a,goal),step=Math.min(d,dt*.65);
      if(d>0){a.x+=(goal.x-a.x)*step/d;a.z+=(goal.z-a.z)*step/d;}
      a.action='slam';a.moving=step>.001;a.facing=side<0?Math.PI/2:-Math.PI/2;return;
    }
    if(a.behavior==='surf'){
      const cycle=this.owner===1?22:31,t=(this._performanceElapsed+this.journey.day*3)%cycle;
      a.y=t>=3&&t<=15?Math.min(1,(t-3)/2,(15-t)/2)*1.85:0;a.action=a.y>0?'surf':'listen';a.moving=false;return;
    }
    if(a.behavior==='support'){
      const surfer=this.actors.find(v=>v.id==='crowd-surfer');a.action=surfer?.y>.1?'support':'listen';a.moving=false;
      if(surfer)a.facing=Math.atan2(surfer.z-a.z,surfer.x-a.x);return;
    }
    a.action=a.behavior||'listen';a.moving=false;
    a.facing=Math.atan2(5.75-a.x,3.5-a.z);
  }
  _tickFloorActors(dt){
    for(let i=0;i<this.crew.length;i++){
      const a=this.actors.find(a=>a.id===this.crew[i]);if(!a||!a.visible||a.id===this.escort?.worker)continue;
      const goal=this._cleanGoal(i);if(this.floor.cleanupActive){const floor=!this.closing;a._floorRetry=Math.max(0,(a._floorRetry||0)-dt);if(a._floorRoute!==floor||Math.hypot(a.targetX-goal.x,a.targetZ-goal.z)>.1||((!a._path?.length||a._pathAt>=a._path.length)&&!a._floorRetry)){(this.closing?setCrowdRoute:setRoute)(a,goal,this.actors,floor);a._floorRetry=.8;}const arrived=move(a,dt,1.55,this.actors);a._ready=arrived;a.action=arrived&&this.reaction?.cleanupWorkBudget>0?'sweep':arrived?'cleanup-wait':'cleaning';}
      else if(this.closing&&this.floor.cleanupComplete){
        const park=this._parkGoal(i);a._parkRetry=Math.max(0,(a._parkRetry||0)-dt);
        const changed=distance(point(a.targetX,a.targetZ),park)>.04,empty=!a._path?.length||a._pathAt>=a._path.length;
        if(changed||empty&&distance(a,park)>.05&&!a._parkRetry){setCrowdRoute(a,park,this.actors,false);a._parkRetry=.45;}
        if(distance(a,park)>.05)move(a,dt,1.55,this.actors);
        if(distance(a,park)<=.05){a._path=[];a._pathAt=0;a.moving=false;a.action='idle';}else a.action='walk';
        a._ready=false;
      }else{a.moving=false;a.action='idle';}
    }
    if(!this.escort)return;const e=this.escort,w=this.actors.find(a=>a.id===e.worker),g=this.actors.find(a=>a.id==='assisted-guest');if(!w||!g){this.escort=null;return;}
    if(this.inspection.freezePerformers&&e.phase!=='return'){w.moving=false;g.moving=false;return;}
    const retryRoute=(actor,goal,floor=false)=>{
      actor._escortRetry=Math.max(0,(actor._escortRetry||0)-dt);
      const changed=Math.hypot(actor.targetX-goal.x,actor.targetZ-goal.z)>.1||this.closing&&actor._floorRoute!==floor;
      const empty=!actor._path?.length||actor._pathAt>=actor._path.length;
      if(changed||empty&&distance(actor,goal)>.2&&!actor._escortRetry){(this.closing?setCrowdRoute:setRoute)(actor,goal,this.actors,floor);actor._escortRetry=.45;}
    };
    if(e.phase==='join'){
      const goal=this._escortFollowGoal(w,g,point(g.x-.5,g.z-.3));if(goal)retryRoute(w,goal);
      move(w,dt,2.5,this.actors);if(distance(w,g)<.95){e.phase='exit';(this.closing?setCrowdRoute:setRoute)(g,point(8.7,-5.4),this.actors,false);w._path=[];w._pathAt=0;}
    }else if(e.phase==='exit'){
      retryRoute(g,point(8.7,-5.4));
      if(distance(w,g)<1.35)move(g,dt,2.2,this.actors);
      const goal=this._escortFollowGoal(w,g,point(g.x-.36,g.z+.18));if(goal){retryRoute(w,goal);move(w,dt,2.9,this.actors);}
      if(distance(g,point(8.7,-5.4))<.2&&distance(w,g)<.9){g.visible=false;g.moving=false;e.phase='return';const ci=this.crew.indexOf(w.id),goal=ci>=0&&this.floor.cleanupActive?this._cleanGoal(ci):point(e.homeX,e.homeZ);(this.closing?setCrowdRoute:setRoute)(w,goal,this.actors,ci>=0&&this.floor.cleanupActive&&!this.closing);}
    }else if(e.phase==='return'){
      const cleaning=this.crew.includes(w.id)&&this.floor.cleanupActive;
      const goal=cleaning?this._cleanGoal(this.crew.indexOf(w.id)):point(e.homeX,e.homeZ);
      retryRoute(w,goal,cleaning&&!this.closing);
      if(move(w,dt,2.5,this.actors)){w._partner=null;g._partner=null;const ci=this.crew.indexOf(w.id);this.escort=null;if(ci>=0&&this.floor.cleanupActive){w.action='cleaning';(this.closing?setCrowdRoute:setRoute)(w,this._cleanGoal(ci),this.actors,!this.closing);}else{w.action='serve';w.moving=false;}}
    }
    if(this.escort){w.action='escort';g.action=e.phase==='join'?'wait-escort':'escorted';}
  }
}
