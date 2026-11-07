import {catalog} from './catalog.js';
import {createDailyPoster,DAILY_POSTER_INDEX} from './daily-poster.js';

export const clamp=(n,a,b)=>Math.max(a,Math.min(b,Number.isFinite(+n)?+n:a));
export const int=(n,a=0,b=1e9)=>Math.trunc(clamp(n,a,b));
export const count=mask=>{let n=0;for(let x=mask>>>0;x;x=(x&(x-1))>>>0)n++;return n;};
export const FREEWILL_INDEX=24;
const freewillMask=1<<FREEWILL_INDEX;
const isDesk=p=>p?.deskVersion===1;
const mask=(1<<catalog.bands.length)-1, legacy=(1<<12)-1;
const allPosters=((1<<catalog.posters.length)-1)|(1<<DAILY_POSTER_INDEX);
const first=(bits,limit)=>{let out=0;for(let i=0;i<=30&&limit>0;i++)if(bits&(1<<i)){out|=1<<i;limit--;}return out;};
const arr=v=>Array.from({length:catalog.bands.length},(_,i)=>int(v?.[i],0,100));
const result=(code,success=false,extra={})=>({success,code,cashDelta:0,bandIndex:-1,...extra});
const validBand=i=>Number.isInteger(i)&&i>=0&&i<catalog.bands.length;
const validLegacyPlan=p=>p&&p.day>=1&&p.venue>=0&&p.venue<4&&p.blocks>=0&&p.blocks<=4&&p.promotions>=0&&p.promotions<=4
  &&p.ticketTier>=0&&p.ticketTier<=2&&!(p.listenedMask&~mask)&&!(p.attemptedMask&~mask)&&!(p.bookedMask&~mask)
  &&!(p.scoutedPosterMask&~allPosters)&&!(p.bookedMask&~p.attemptedMask)&&count(p.bookedMask)<=3
  &&count(p.listenedMask)+count(p.attemptedMask)+count(p.scoutedPosterMask)+p.promotions<=p.blocks;
const validDeskPlan=p=>p&&p.day>=1&&Number.isInteger(p.venue)&&p.venue>=0&&p.venue<4&&p.ticketTier===1
  &&typeof p.freewillSelected==='boolean'&&Number.isInteger(p.deskDoneMask)&&[0,1,3].includes(p.deskDoneMask)
  &&(p.freewillSelected||p.deskDoneMask===0)&&p.blocks===0&&p.promotions===0&&p.listenedMask===0&&p.scoutedPosterMask===0
  &&(!p.completed||p.locked)&&(p.locked?p.freewillSelected&&p.deskDoneMask===3&&p.bookedMask===freewillMask&&p.attemptedMask===freewillMask&&p.attempts===1:p.bookedMask===0&&p.attemptedMask===0&&p.attempts===0);
const validPlan=p=>isDesk(p)?validDeskPlan(p):validLegacyPlan(p);
const error=p=>!validPlan(p)?'invalid_plan':p.completed?'already_completed':p.locked?'locked':isDesk(p)?'desk_only':p.blocks>=4?'no_time':null;
const deskError=p=>!isDesk(p)||!validDeskPlan(p)?'invalid_plan':p.completed?'already_completed':p.locked?'locked':null;
const fit=(v,b)=>catalog.venues[v].preferred===catalog.bands[b].genreIndex?8:catalog.venues[v].alternate===catalog.bands[b].genreIndex?4:0;
const careerValue=(a,i)=>int(a?.[i],0,100);
const posterMask=i=>catalog.posters[i]?.artistIndices.reduce((bits,index)=>bits|(1<<index),0)||0;
const dailyMask=(day,venue)=>day>=1&&venue>=0&&venue<4?createDailyPoster(day,venue).artistIndices.reduce((bits,index)=>bits|(1<<index),0):0;

export const Booking={
  newPlan:(day=1,venue=0)=>({day:int(day,1,9999),venue:int(venue,0,3),blocks:0,listenedMask:0,attemptedMask:0,bookedMask:0,scoutedPosterMask:0,
    promotions:0,ticketTier:1,locked:false,completed:false,attempts:0,bookedGrowth:arr(),hasLockedMetrics:false,lockedMetrics:null}),
  newDeskPlan(day=1,venue=0){return{...this.newPlan(day,venue),deskVersion:1,freewillSelected:false,deskDoneMask:0};},
  isDesk,
  hour:p=>isDesk(p)?p.freewillSelected?(p.deskDoneMask===3?18:p.deskDoneMask&1?16:13):10:10+2*(p?.blocks||0),
  deskSelect(p,selected){
    const e=deskError(p);if(e)return result(e);if(typeof selected!=='boolean')return result('invalid_selection');
    p.freewillSelected=selected;if(!selected)p.deskDoneMask=0;return result(selected?'desk_selected':'desk_cleared',true);
  },
  deskWork(p,task){
    const e=deskError(p);if(e)return result(e);if(task!==0&&task!==1)return result('invalid_task');
    if(!p.freewillSelected)return result('band_required');if(task===1&&!(p.deskDoneMask&1))return result('plan_required');
    const bit=1<<task;if(p.deskDoneMask&bit)return result('already_done');p.deskDoneMask|=bit;
    return result(task===0?'desk_planned':'desk_confirmed',true);
  },
  lockDesk(p,c){
    const e=deskError(p);if(e)return result(e);if(!c)return result('invalid_career');
    if(!p.freewillSelected)return result('band_required');if(p.deskDoneMask!==3)return result('desk_incomplete');
    p.bookedMask=p.attemptedMask=freewillMask;p.attempts=1;p.bookedGrowth=arr();p.bookedGrowth[FREEWILL_INDEX]=careerValue(c.growth,FREEWILL_INDEX);
    p.lockedMetrics=this.calculate(p);p.hasLockedMetrics=true;p.locked=true;return result('desk_locked',true,{bandIndex:FREEWILL_INDEX});
  },
  newCareer:()=>({discoveredMask:legacy,relationships:arr(),growth:arr()}),
  discovered:(career,band)=>validBand(band)&&(band<12||!!(career?.discoveredMask&(1<<band))),
  posterDiscovered:(career,poster)=>!!posterMask(poster)&&((career?.discoveredMask&posterMask(poster))===posterMask(poster)),
  dailyPosterDiscovered:(career,day,venue)=>!!dailyMask(day,venue)&&!!career&&(career.discoveredMask&dailyMask(day,venue))===dailyMask(day,venue),
  fee:(band,premium=0)=>validBand(band)&&(premium===0||premium===1)?Math.fround(catalog.bands[band].baseFee*Math.fround(premium?1.35:1)):0,
  chance(p,c,b,premium=0){
    if(!validPlan(p)||!validBand(b)||(premium!==0&&premium!==1)||!this.discovered(c,b))return 0;
    return int(58+fit(p.venue,b)+(premium?15:0)+((p.listenedMask&(1<<b))?20:0)+Math.trunc(careerValue(c?.relationships,b)/12)+Math.trunc(careerValue(c?.growth,b)/20),15,95);
  },
  roll(day,venue,band,premium,attempt){
    let hash=2166136261;
    for(const n of [day,venue,band,premium,attempt])hash=Math.imul(hash^(n>>>0),16777619)>>>0;
    hash^=hash>>>16;hash=Math.imul(hash,2246822519)>>>0;hash^=hash>>>13;return(hash>>>0)%100;
  },
  scout(p,c,i){
    let e=error(p);if(e)return result(e);if(!c)return result('invalid_career');
    const artists=posterMask(i);if(!artists)return result('invalid_poster');
    if(p.scoutedPosterMask&(1<<i))return result('already_scouted');
    if((c.discoveredMask&artists)===artists)return result('already_discovered');
    p.scoutedPosterMask|=1<<i;p.blocks++;c.discoveredMask=(c.discoveredMask|artists|legacy)&mask;return result('scouted',true);
  },
  scoutDaily(p,c){
    const e=error(p);if(e)return result(e);if(!c)return result('invalid_career');
    const artists=dailyMask(p.day,p.venue);if(!artists)return result('invalid_poster');
    if(p.scoutedPosterMask&(1<<DAILY_POSTER_INDEX))return result('already_scouted');
    if((c.discoveredMask&artists)===artists)return result('already_discovered');
    p.scoutedPosterMask|=1<<DAILY_POSTER_INDEX;p.blocks++;c.discoveredMask=(c.discoveredMask|artists|legacy)&mask;return result('scouted',true);
  },
  listen(p,c,b){
    let e=error(p);if(e)return result(e);if(!validBand(b))return result('invalid_band');if(!this.discovered(c,b))return result('not_discovered');
    if(p.listenedMask&(1<<b))return result('already_listened');p.listenedMask|=1<<b;p.blocks++;return result('listened',true,{bandIndex:b});
  },
  offer(p,c,b,premium,money){
    let e=error(p);if(e)return result(e);if(!validBand(b))return result('invalid_band');if(!this.discovered(c,b))return result('not_discovered');
    if(premium!==0&&premium!==1)return result('invalid_package');if(!Number.isFinite(money))return result('invalid_cash');
    if(p.attemptedMask&(1<<b))return result('already_attempted');if(count(p.bookedMask)>=3)return result('lineup_full');
    const fee=this.fee(b,premium);if(money<fee)return result('insufficient_cash');
    let attempt=count(p.attemptedMask),accepted=this.roll(p.day,p.venue,b,premium,attempt)<this.chance(p,c,b,premium);
    p.attemptedMask|=1<<b;p.attempts=attempt+1;p.blocks++;if(!accepted)return result('rejected',false,{bandIndex:b});
    p.bookedMask|=1<<b;p.bookedGrowth=arr(p.bookedGrowth);p.bookedGrowth[b]=careerValue(c?.growth,b);
    return result('accepted',true,{bandIndex:b,cashDelta:-fee});
  },
  promote(p,c,money){const e=error(p);if(e)return result(e);if(!Number.isFinite(money))return result('invalid_cash');if(money<60)return result('insufficient_cash');p.promotions++;p.blocks++;return result('promoted',true,{cashDelta:-60});},
  setTicket(p,tier){if(!isDesk(p)&&validPlan(p)&&!p.locked&&!p.completed&&Number.isInteger(tier)&&tier>=0&&tier<=2)p.ticketTier=tier;},
  calculate(p){
    const bookedMask=isDesk(p)?p.freewillSelected?freewillMask:0:p.bookedMask;
    const bookedCount=count(bookedMask),venueCapacity=catalog.venues[p.venue].capacity;let draw=bookedCount?10:12,affinity=0;
    for(let i=0;i<catalog.bands.length;i++)if(bookedMask&(1<<i)){
      const f=fit(p.venue,i);affinity+=f;draw+=catalog.bands[i].draw+Math.trunc(f/2)+Math.trunc(careerValue(p.bookedGrowth,i)/20);if(p.listenedMask&(1<<i))draw+=2;
    }
    const attendance=int(Math.floor(Math.fround(draw*Math.fround(p.ticketTier===0?1.12:p.ticketTier===2?.8:1)))+p.promotions*8,0,venueCapacity),ticketPrice=10+p.ticketTier*5;
    return{attendance,ticketPrice,ticketIncome:bookedCount?attendance*ticketPrice:0,energyBonus:Math.min(.15,bookedCount*.025+p.promotions*.01+affinity*.001),
      fanBonus:bookedCount?Math.trunc(attendance/14)+bookedCount:0,orderIntervalMultiplier:Math.max(.75,1-bookedCount*.025-p.promotions*.015-attendance*.001),bookedCount,venueCapacity};
  },
  repairMetrics(m,venue){
    m=m||{};let venueCapacity=catalog.venues[venue].capacity,attendance=int(m.attendance,0,venueCapacity),bookedCount=int(m.bookedCount,0,3),ticketPrice=Number.isFinite(m.ticketPrice)?clamp(m.ticketPrice,10,20):15;
    return{venueCapacity,attendance,bookedCount,ticketPrice,ticketIncome:bookedCount?clamp(m.ticketIncome,0,attendance*ticketPrice):0,energyBonus:clamp(m.energyBonus,0,.15),fanBonus:int(m.fanBonus,0,12),orderIntervalMultiplier:Number.isFinite(m.orderIntervalMultiplier)?clamp(m.orderIntervalMultiplier,.75,1):1};
  },
  forecast(p){return!validPlan(p)?{attendance:0,ticketPrice:15,ticketIncome:0,energyBonus:0,fanBonus:0,orderIntervalMultiplier:1,bookedCount:0,venueCapacity:0}:p.locked&&p.hasLockedMetrics?this.repairMetrics(p.lockedMetrics,p.venue):this.calculate(p);},
  lock(p,c){if(isDesk(p))return this.lockDesk(p,c);if(!validPlan(p)||p.locked&&p.hasLockedMetrics)return;p.lockedMetrics=this.calculate(p);p.hasLockedMetrics=true;p.locked=true;},
  complete(p,c,stars){
    if(!p||!validPlan(p))return result('invalid_plan');if(p.completed)return result('already_completed');if(!c)return result('invalid_career');
    if(isDesk(p)&&!p.locked){const locked=this.lockDesk(p,c);if(!locked.success)return locked;}else if(!isDesk(p))this.lock(p);
    const m=this.forecast(p);stars=int(stars,0,3);c.relationships=arr(c.relationships);c.growth=arr(c.growth);
    for(let i=0;i<catalog.bands.length;i++)if(p.bookedMask&(1<<i)){c.relationships[i]=int(c.relationships[i]+4+stars,0,100);c.growth[i]=int(c.growth[i]+2+stars,0,100);}
    p.completed=true;const income=Math.floor(m.ticketIncome*(70+stars*10)/100);return result('complete',true,{attendance:m.attendance,income,cashDelta:income});
  },
  sanitize(p,c){
    if(c){c.relationships=arr(c.relationships);c.growth=arr(c.growth);c.discoveredMask=((c.discoveredMask|0)&mask)|legacy;}
    if(!p)return;p.day=int(p.day,1,9999);p.venue=int(p.venue,0,3);p.ticketTier=int(p.ticketTier,0,2);
    if(isDesk(p)){
      p.freewillSelected=p.freewillSelected===true;p.deskDoneMask=int(p.deskDoneMask,0,3);
      if(!p.freewillSelected||!(p.deskDoneMask&1))p.deskDoneMask=0;
      p.blocks=p.promotions=p.listenedMask=p.scoutedPosterMask=0;p.ticketTier=1;p.bookedGrowth=arr(p.bookedGrowth);
      if(p.completed)p.locked=true;
      if(p.locked){
        p.freewillSelected=true;p.deskDoneMask=3;p.bookedMask=p.attemptedMask=freewillMask;p.attempts=1;
        for(let i=0;i<catalog.bands.length;i++)if(i!==FREEWILL_INDEX)p.bookedGrowth[i]=0;
        p.lockedMetrics=p.hasLockedMetrics?this.repairMetrics(p.lockedMetrics,p.venue):this.calculate(p);p.hasLockedMetrics=true;
      }else{p.bookedMask=p.attemptedMask=p.attempts=0;p.bookedGrowth=arr();p.hasLockedMetrics=false;p.lockedMetrics=null;}
      return;
    }
    p.bookedMask=first((p.bookedMask|0)&mask,3);p.attemptedMask=p.bookedMask|first(((p.attemptedMask|0)&mask)&~p.bookedMask,4-count(p.bookedMask));p.attempts=count(p.attemptedMask);
    p.listenedMask=first((p.listenedMask|0)&mask,4-p.attempts);p.scoutedPosterMask=first((p.scoutedPosterMask|0)&allPosters,4-p.attempts-count(p.listenedMask));
    p.promotions=int(p.promotions,0,4-p.attempts-count(p.listenedMask)-count(p.scoutedPosterMask));p.blocks=int(Math.max(p.blocks||0,p.attempts+count(p.listenedMask)+count(p.scoutedPosterMask)+p.promotions),0,4);
    p.bookedGrowth=arr(p.bookedGrowth);for(let i=0;i<catalog.bands.length;i++)if(!(p.bookedMask&(1<<i)))p.bookedGrowth[i]=0;
    if(c){c.discoveredMask|=p.bookedMask|p.attemptedMask|p.listenedMask;for(let i=0;i<catalog.posters.length;i++)if(p.scoutedPosterMask&(1<<i))c.discoveredMask|=posterMask(i);if(p.scoutedPosterMask&(1<<DAILY_POSTER_INDEX))c.discoveredMask|=dailyMask(p.day,p.venue);}
    if(p.completed)p.locked=true;if(p.locked){p.lockedMetrics=p.hasLockedMetrics?this.repairMetrics(p.lockedMetrics,p.venue):this.calculate(p);p.hasLockedMetrics=true;}else p.hasLockedMetrics=false;
  }
};
