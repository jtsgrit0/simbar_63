// Shared, server-confirmed totals. Analytics failures never interrupt music.
const QUEUE_KEY='simbar.music-qualified-plays.v1';
const ID=/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const TRACK=/^track_00[1-5]$/;
const countValue=value=>{if(typeof value!=='number'&&!(typeof value==='string'&&/^\d+$/.test(value)))return null;const n=Number(value);return Number.isSafeInteger(n)&&n>=0?n:null;};
export class PlayStats{
  constructor({url,publishableKey,storage,fetcher=globalThis.fetch?.bind(globalThis),onchange=()=>{},source=globalThis.location?.protocol==='simbar:'?'windows':'web',now=()=>Date.now(),setTimer=(callback,delay)=>globalThis.setTimeout(callback,delay),clearTimer=timer=>globalThis.clearTimeout(timer)}={}){
    try{storage=storage??globalThis.localStorage;}catch{storage=null;}
    this.url=url?.replace(/\/$/,'');this.key=publishableKey;this.storage=storage;this.fetcher=fetcher;this.onchange=onchange;this.source=source;this.now=now;this.setTimer=setTimer;this.clearTimer=clearTimer;
    this.counts=new Map();this.state='loading';this.entries=new Map();this.closed=false;this.timer=null;this.flushing=null;this.refreshing=null;
    try{const saved=JSON.parse(storage?.getItem(QUEUE_KEY)||'[]');if(Array.isArray(saved))for(const entry of saved.slice(-100))if(ID.test(entry.playId)&&TRACK.test(entry.trackId)&&entry.qualified===true&&Number.isFinite(entry.createdAt)&&this.now()-entry.createdAt<7*86400000)this.entries.set(entry.playId,{...entry,busy:null,retryAt:0});}catch{}
  }
  count(trackId){return this.counts.has(trackId)?this.counts.get(trackId):null;}
  _notify(){try{this.onchange();}catch{}}
  _save(){try{this.storage?.setItem(QUEUE_KEY,JSON.stringify([...this.entries.values()].filter(e=>e.qualified).slice(-100).map(({playId,trackId,source,qualified,createdAt,ticket,expiresAt,readyAt})=>({playId,trackId,source,qualified,createdAt,ticket,expiresAt,readyAt}))));}catch{}}
  _entry({playId,trackId}={}){
    if(this.closed||!ID.test(playId)||!TRACK.test(trackId))return null;
    const existing=this.entries.get(playId);if(existing)return existing.trackId===trackId?existing:null;
    // Abandoned, unqualified starts never become persistent listening records.
    const unused=[...this.entries.values()].filter(e=>!e.qualified);for(const old of unused.slice(0,Math.max(0,unused.length-9)))this.entries.delete(old.playId);
    if([...this.entries.values()].filter(e=>e.qualified).length>=100)return null;
    const entry={playId,trackId,source:this.source,createdAt:this.now(),qualified:false,retryAt:0,attempts:0};this.entries.set(playId,entry);return entry;
  }
  start(play){const entry=this._entry(play);if(entry)this._ticket(entry).catch(()=>{});}
  qualify(play){if(play?.seconds!==30)return;const entry=this._entry(play);if(!entry)return;entry.qualified=true;this._save();void this.flush();}
  async _post(body){
    if(!this.fetcher||!this.url||!this.key)throw new Error('No analytics connection');
    const controller=new AbortController(),timeout=this.setTimer(()=>controller.abort(),10000);timeout?.unref?.();
    try{
      const response=await this.fetcher(this.url+'/functions/v1/track-play',{method:'POST',headers:{apikey:this.key,'Content-Type':'application/json'},body:JSON.stringify(body),signal:controller.signal});
      const data=await response.json();if(!response.ok){const error=new Error(data?.code||'Analytics request failed');error.status=response.status;error.code=data?.code;error.retryAfter=Number(data?.retryAfter||response.headers?.get('Retry-After'));throw error;}return data;
    }finally{this.clearTimer(timeout);}
  }
  async _ticket(entry){
    if(entry.ticket&&entry.expiresAt>this.now()+1000)return;
    if(entry.busy)return entry.busy;
    entry.busy=(async()=>{const data=await this._post({action:'start',playId:entry.playId,trackId:entry.trackId,source:entry.source});
      if(typeof data.ticket!=='string'||!Number.isFinite(data.expiresAt))throw new Error('Invalid analytics ticket');
      entry.ticket=data.ticket;entry.expiresAt=data.expiresAt;entry.readyAt=this.now()+30000;if(entry.qualified)this._save();
    })().finally(()=>{entry.busy=null;});return entry.busy;
  }
  _schedule(){
    if(this.closed)return;this.clearTimer(this.timer);this.timer=null;
    const pending=[...this.entries.values()].filter(e=>e.qualified);if(!pending.length)return;
    const delay=Math.max(100,Math.min(...pending.map(e=>Math.max(e.retryAt||0,e.readyAt||0)-this.now())));
    this.timer=this.setTimer(()=>{this.timer=null;void this.flush();},delay);this.timer?.unref?.();
  }
  async flush(){
    if(this.closed)return;if(this.flushing)return this.flushing;
    this.flushing=(async()=>{for(const entry of [...this.entries.values()].filter(e=>e.qualified)){
      if(this.closed)break;if(Math.max(entry.retryAt||0,entry.readyAt||0)>this.now())continue;
      try{
        await this._ticket(entry);if(this.closed)break;if(entry.readyAt>this.now())continue;
        const data=await this._post({action:'qualify',playId:entry.playId,trackId:entry.trackId,ticket:entry.ticket,seconds:30});
        const count=countValue(data.playCount);if(data.trackId!==entry.trackId||count===null)throw new Error('Invalid play total');
        this.counts.set(entry.trackId,Math.max(this.counts.get(entry.trackId)||0,count));this.entries.delete(entry.playId);this._save();this._notify();
      }catch(error){
        if(error.code==='EXPIRED_TICKET'){entry.ticket=null;entry.expiresAt=0;entry.readyAt=0;entry.retryAt=this.now()+1000;}
        else if(error.code==='PLAY_ID_CONFLICT'||error.status>=400&&error.status<500&&![401,408,409,429].includes(error.status)){this.entries.delete(entry.playId);}
        else{entry.attempts=(entry.attempts||0)+1;entry.retryAt=this.now()+Math.max(1000,Math.min(300000,(Number.isFinite(error.retryAfter)&&error.retryAfter>0?error.retryAfter*1000:1000*2**Math.min(entry.attempts,8))));}
        this._save();
      }
    }})().finally(()=>{this.flushing=null;this._schedule();});return this.flushing;
  }
  async refresh(){
    if(this.closed)return;if(this.refreshing)return this.refreshing;
    this.refreshing=(async()=>{const controller=new AbortController(),timeout=this.setTimer(()=>controller.abort(),10000);timeout?.unref?.();
      try{
        const response=await this.fetcher(this.url+'/rest/v1/track_play_counts?select=track_id,play_count',{headers:{apikey:this.key},signal:controller.signal,cache:'no-store'});if(!response.ok)throw new Error('Counts unavailable');
        const rows=await response.json();if(!Array.isArray(rows))throw new Error('Invalid play totals');
        const values=new Map(rows.filter(row=>TRACK.test(row.track_id)&&countValue(row.play_count)!==null).map(row=>[row.track_id,countValue(row.play_count)]));if(values.size!==5)throw new Error('Incomplete play totals');
        for(const [trackId,count] of values)this.counts.set(trackId,Math.max(this.counts.get(trackId)||0,count));this.state='ready';
      }catch{this.state='unavailable';}finally{this.clearTimer(timeout);if(!this.closed)this._notify();}
    })().finally(()=>{this.refreshing=null;});return this.refreshing;
  }
  resume(){if(this.closed)return;void this.refresh();for(const entry of this.entries.values())entry.retryAt=0;void this.flush();}
  destroy(){this.closed=true;this.clearTimer(this.timer);this._save();}
}
