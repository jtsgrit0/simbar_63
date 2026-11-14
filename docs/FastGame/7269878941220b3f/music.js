// Remote MP3s stream directly through the media element after a user gesture.
export class MusicPlayer{
  constructor({getIndex,getPaused,tracks,baseURL,AudioClass=globalThis.Audio,onchange=()=>{}}){
    this.getIndex=getIndex;this.tracks=tracks;this.baseURL=baseURL;this.AudioClass=AudioClass;this.onchange=onchange;
    this.getPaused=getPaused;
    this.audio=null;this.index=-1;this.awake=false;this.paused=!!getPaused?.();this.failed=false;this.blocked=false;this.suspended=false;this.playRequest=0;this.playPending=false;this._playbackActive=false;this._playbackChange=0;
  }
  get playing(){return this._playbackActive&&!this.paused&&!this.suspended&&!this.blocked&&!this.failed&&!!this.audio&&this.audio.paused!==true&&this.audio.ended!==true;}
  // Called synchronously from a user gesture. A browser restriction is not a
  // saved pause choice, and only a later gesture should retry a rejected play.
  wake(){if(!this.awake){this.awake=true;this.sync();return;}this.sync();if((this.blocked||this.failed)&&!this.paused&&!this.suspended&&!this.playPending){if(this.failed)this.audio?.load?.();this.play();}}
  sync(){
    const desired=this.getPaused?!!this.getPaused():null;
    if(!this.awake||!this.AudioClass){if(desired!==null)this.paused=desired;return;}
    const index=this.getIndex();if(index===this.index){if(desired!==null&&desired!==this.paused)this.setPaused(desired);return;}
    this.playRequest++;this.playPending=false;this._playbackActive=false;this.audio?.pause();if(this.audio){this.audio.removeAttribute?.('src');this.audio.load?.();}
    const track=this.tracks[index];if(!track)return;
    this.index=index;this.paused=desired??false;this.failed=false;this.blocked=false;
    const audio=this.audio=new this.AudioClass();audio.preload='none';audio.loop=true;audio.volume=.14;
    audio.src=track.audio_url;
    audio.addEventListener?.('playing',()=>{if(this.audio!==audio||this.paused||this.suspended||audio.paused===true||audio.ended===true)return;this._playbackActive=true;this.blocked=false;this.failed=false;this.onchange();});
    for(const event of ['pause','waiting','stalled','ended','emptied','error'])audio.addEventListener?.(event,()=>{if(this.audio!==audio)return;if(event==='stalled'&&this._playbackActive&&audio.readyState>=3&&audio.paused===false)return;this._playbackActive=false;this._playbackChange++;if(event==='error')this.failed=true;this.onchange();});
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
  destroy(){this.playRequest++;this.playPending=false;this._playbackActive=false;this.audio?.pause();this.audio?.removeAttribute?.('src');this.audio?.load?.();this.audio=null;}
}
