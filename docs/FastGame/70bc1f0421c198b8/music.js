import {QualifiedPlayTracker} from './play-tracker.js';

// Remote MP3s stream directly through the media element after a user gesture.
export class MusicPlayer{
  constructor({getIndex,getPaused,tracks,baseURL,AudioClass=globalThis.Audio,onchange=()=>{},onended=()=>{},onplaystart=()=>{},onqualified=()=>{}}){
    this.getIndex=getIndex;this.tracks=tracks;this.baseURL=baseURL;this.AudioClass=AudioClass;this.onchange=onchange;
    this.getPaused=getPaused;this.onended=onended;this.onplaystart=onplaystart;this.onqualified=onqualified;
    this.audio=null;this.index=-1;this.awake=false;this.paused=!!getPaused?.();this.failed=false;this.blocked=false;this.suspended=false;this.playRequest=0;this.playPending=false;this._playbackActive=false;this._playbackChange=0;
    this._audioGeneration=0;this._unbindAudio=()=>{};this._endedAudio=null;this._listenTracker=null;
  }
  get playing(){return this._playbackActive&&!this.paused&&!this.suspended&&!this.blocked&&!this.failed&&!!this.audio&&this.audio.paused!==true&&this.audio.ended!==true;}
  // Called synchronously from a user gesture. A browser restriction is not a
  // saved pause choice, and only a later gesture should retry a rejected play.
  wake(){if(!this.awake){this.awake=true;this.sync();return;}this.sync();if((this.blocked||this.failed)&&!this.paused&&!this.suspended&&!this.playPending){if(this.failed)this.audio?.load?.();this.play();}}
  sync(){
    const desired=this.getPaused?!!this.getPaused():null;
    if(!this.awake||!this.AudioClass){if(desired!==null)this.paused=desired;return;}
    const index=this.getIndex();if(index===this.index){if(desired!==null&&desired!==this.paused)this.setPaused(desired);return;}
    const previous=this.audio,reuse=previous&&this._endedAudio===previous;
    this._listenTracker?.stop(previous);this.playRequest++;this._audioGeneration++;this._unbindAudio();this.playPending=false;this._playbackActive=false;previous?.pause();
    if(previous&&!reuse){previous.removeAttribute?.('src');previous.load?.();}
    const track=this.tracks[index];if(!track)return;
    this.index=index;this.paused=desired??false;this.failed=false;this.blocked=false;
    // WebKit grants playback permission per element. Natural progression
    // changes the source on the already unlocked element, keeping that grant.
    const audio=this.audio=reuse?previous:new this.AudioClass();audio.preload='none';audio.loop=false;audio.volume=.14;let endedHandled=false;
    audio.src=track.audio_url;
    if(reuse)audio.load?.();
    const generation=this._audioGeneration,source=audio.src,listeners=[];
    const tracker=this._listenTracker=track.trackId?new QualifiedPlayTracker({trackId:track.trackId,onplaystart:this.onplaystart,onqualified:this.onqualified}):null;
    const current=()=>this.audio===audio&&this._audioGeneration===generation&&this.getIndex()===index;
    const sourceCurrent=()=>typeof audio.currentSrc!=='string'||audio.currentSrc===source;
    const listen=(name,callback)=>{audio.addEventListener?.(name,callback);listeners.push([name,callback]);};
    this._unbindAudio=()=>{for(const [name,callback] of listeners)audio.removeEventListener?.(name,callback);};
    listen('playing',()=>{if(!current()||!sourceCurrent()||this.paused||this.suspended||audio.paused===true||audio.ended===true||audio.readyState<3)return;endedHandled=false;this._playbackActive=true;this.blocked=false;this.failed=false;tracker?.playing(audio);this.onchange();});
    listen('timeupdate',()=>{if(!current()||!sourceCurrent())return;if(this.paused||this.getPaused?.()||this.suspended)tracker?.stop(audio);else tracker?.progress(audio);});
    listen('seeking',()=>{if(current()&&sourceCurrent())tracker?.seeking(audio);});
    listen('seeked',()=>{if(current()&&sourceCurrent()&&!this.paused&&!this.getPaused?.()&&!this.suspended)tracker?.seeked(audio);});
    for(const event of ['pause','waiting','stalled','emptied','error'])listen(event,()=>{
      if(!current())return;
      if(event==='emptied'){if(typeof audio.readyState==='number'&&audio.readyState!==0)return;}
      else if(!sourceCurrent()&&!(event==='error'&&audio.error))return;
      if((event==='pause'&&audio.paused===false)||(event==='waiting'&&audio.readyState>=3)||(event==='error'&&'error'in audio&&!audio.error))return;
      if(event==='stalled'&&this._playbackActive&&audio.readyState>=3&&audio.paused===false)return;
      tracker?.stop(audio);this._playbackActive=false;this._playbackChange++;if(event==='error')this.failed=true;this.onchange();
    });
    listen('ended',()=>{
      if(!current()||!sourceCurrent()||audio.ended!==true||endedHandled)return;
      endedHandled=true;
      tracker?.ended(audio);
      this._playbackActive=false;this._playbackChange++;this.onchange();
      if(this.paused||this.getPaused?.()||this.suspended||this.blocked||this.failed)return;
      this._endedAudio=audio;try{this.onended(index);}finally{this._endedAudio=null;}
    });
    if(!this.paused&&!this.suspended)this.play();this.onchange();
  }
  play(){
    const audio=this.audio;if(!audio||this.paused||this.suspended)return;
    const request=++this.playRequest,playbackChange=this._playbackChange;this.playPending=true;this._playbackActive=false;
    const current=()=>this.audio===audio&&request===this.playRequest;
    const accepted=()=>{if(!current())return;this.playPending=false;this.blocked=false;if(playbackChange===this._playbackChange){this._playbackActive=true;this.failed=false;}this.onchange();};
    const rejected=()=>{if(!current())return;this.playPending=false;this._playbackActive=false;this.blocked=true;this.onchange();};
    try{const pending=audio.play();if(pending&&typeof pending.then==='function')pending.then(accepted,rejected);else{this.playPending=false;this.onchange();}}catch{rejected();}
  }
  setPaused(paused){this.paused=!!paused;if(this.paused){this.playRequest++;this.playPending=false;this._playbackActive=false;this.audio?.pause();}else if(this.awake&&!this.suspended)this.play();this.onchange();}
  suspend(){if(this.suspended)return;this.suspended=true;this.playRequest++;this.playPending=false;this._playbackActive=false;this.audio?.pause();}
  resumeContext(){if(!this.suspended)return;this.suspended=false;if(this.awake&&!this.paused)this.play();}
  toggle(){if(!this.awake){this.wake();return;}this.sync();this.setPaused(!this.paused);}
  select(){this.awake=true;this.suspended=false;const old=this.index;this.sync();if(this.index===old){this.paused=false;this.play();this.onchange();}}
  destroy(){this._listenTracker?.stop(this.audio);this._listenTracker=null;this.playRequest++;this._audioGeneration++;this._unbindAudio();this.playPending=false;this._playbackActive=false;this.audio?.pause();this.audio?.removeAttribute?.('src');this.audio?.load?.();this.audio=null;}
}
