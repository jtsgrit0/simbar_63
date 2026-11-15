const nowSeconds=()=>globalThis.performance?.now?globalThis.performance.now()/1000:Date.now()/1000;
const uuid=()=>{
  if(globalThis.crypto?.randomUUID)return globalThis.crypto.randomUUID();
  const bytes=new Uint8Array(16);
  if(globalThis.crypto?.getRandomValues)globalThis.crypto.getRandomValues(bytes);
  else for(let i=0;i<bytes.length;i++)bytes[i]=Math.floor(Math.random()*256);
  bytes[6]=(bytes[6]&15)|64;bytes[8]=(bytes[8]&63)|128;
  const hex=Array.from(bytes,value=>value.toString(16).padStart(2,'0')).join('');
  return`${hex.slice(0,8)}-${hex.slice(8,12)}-${hex.slice(12,16)}-${hex.slice(16,20)}-${hex.slice(20)}`;
};
const notify=(callback,event)=>{try{callback(event)?.catch?.(()=>{});}catch{}};

// One source playback cycle. Native media progress provides the evidence;
// elapsed wall time alone, play() requests and seeks never earn listening time.
export class QualifiedPlayTracker{
  constructor({trackId,onplaystart=()=>{},onqualified=()=>{},clock=nowSeconds,idFactory=uuid}){
    this.trackId=trackId;this.onplaystart=onplaystart;this.onqualified=onqualified;this.clock=clock;this.idFactory=idFactory;
    this.playId=null;this.seconds=0;this.active=false;this.finished=false;this.qualified=false;this.position=null;this.sampledAt=null;
  }
  _baseline(audio){this.position=Number.isFinite(audio?.currentTime)?audio.currentTime:null;this.sampledAt=this.clock();}
  _sample(audio){
    const position=Number.isFinite(audio?.currentTime)?audio.currentTime:null,now=this.clock();
    if(this.active&&!audio?.seeking&&position!==null&&this.position!==null&&Number.isFinite(now)&&Number.isFinite(this.sampledAt)){
      const media=position-this.position,elapsed=now-this.sampledAt;
      if(media>0&&elapsed>0)this.seconds=Math.min(30,this.seconds+Math.min(media,elapsed));
    }
    this.position=position;this.sampledAt=now;
    if(!this.qualified&&this.playId&&this.seconds>=30-1e-9){
      this.seconds=30;this.qualified=true;notify(this.onqualified,{playId:this.playId,trackId:this.trackId,seconds:30});
    }
  }
  playing(audio){
    if(audio?.paused===true||audio?.ended===true||audio?.seeking||audio?.readyState<3)return;
    if(this.finished){this.playId=null;this.seconds=0;this.qualified=false;this.finished=false;}
    if(this.active)this._sample(audio);else this._baseline(audio);
    this.active=true;
    if(!this.playId){this.playId=this.idFactory();notify(this.onplaystart,{playId:this.playId,trackId:this.trackId});}
  }
  progress(audio){
    if(!this.active)return;
    if(audio?.seeking){this.seeking(audio);return;}
    if(audio?.paused===true||audio?.ended===true||audio?.readyState<3){this.stop(audio);return;}
    this._sample(audio);
  }
  stop(audio){if(this.active)this._sample(audio);this.active=false;this._baseline(audio);}
  seeking(audio){this.active=false;this._baseline(audio);}
  seeked(audio){if(this.playId&&!this.finished)this.playing(audio);}
  ended(audio){this.stop(audio);this.finished=true;}
}
