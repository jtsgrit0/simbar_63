// Real StreamingAssets tracks are fetched only after a user gesture.
export class MusicPlayer{
  constructor({getIndex,getPaused,tracks,baseURL,AudioClass=globalThis.Audio,onchange=()=>{},onPauseChange=()=>{}}){
    this.getIndex=getIndex;this.tracks=tracks;this.baseURL=baseURL;this.AudioClass=AudioClass;this.onchange=onchange;
    this.getPaused=getPaused;this.onPauseChange=onPauseChange;
    this.audio=null;this.index=-1;this.awake=false;this.paused=!!getPaused?.();this.failed=false;this.suspended=false;this.playRequest=0;
  }
  wake(){if(!this.awake){this.awake=true;this.sync();}}
  sync(){
    const desired=this.getPaused?!!this.getPaused():null;
    if(!this.awake||!this.AudioClass){if(desired!==null)this.paused=desired;return;}
    const index=this.getIndex();if(index===this.index){if(desired!==null&&desired!==this.paused)this.setPaused(desired);return;}
    this.playRequest++;this.audio?.pause();if(this.audio){this.audio.removeAttribute?.('src');this.audio.load?.();}
    const track=this.tracks[index];if(!track)return;
    this.index=index;this.paused=desired??false;this.failed=false;this.suspended=false;
    const audio=this.audio=new this.AudioClass();audio.preload='none';audio.loop=true;audio.volume=.14;
    audio.src=new URL('StreamingAssets/Music/'+encodeURIComponent(track.filename),this.baseURL).href;
    audio.addEventListener?.('error',()=>{if(this.audio===audio){this.failed=true;this.onchange();}});
    if(!this.paused)this.play();this.onchange();
  }
  play(){const audio=this.audio,request=++this.playRequest;const rejected=()=>{if(this.audio!==audio||request!==this.playRequest)return;this.paused=true;this.onPauseChange(true);this.onchange();};try{const pending=audio?.play();pending?.catch(rejected);}catch{rejected();}}
  setPaused(paused){this.paused=!!paused;if(this.paused){this.playRequest++;this.audio?.pause();}else if(this.awake&&!this.suspended)this.play();this.onchange();}
  suspend(){if(this.suspended)return;this.suspended=true;this.playRequest++;this.audio?.pause();}
  resumeContext(){if(!this.suspended)return;this.suspended=false;if(this.awake&&!this.paused)this.play();}
  toggle(){if(!this.awake){this.wake();return;}this.sync();this.paused=!this.paused;if(this.paused)this.audio?.pause();else this.play();this.onchange();}
  select(){this.awake=true;const old=this.index;this.sync();if(this.index===old){this.paused=false;this.suspended=false;this.play();this.onchange();}}
  destroy(){this.playRequest++;this.audio?.pause();this.audio?.removeAttribute?.('src');this.audio?.load?.();this.audio=null;}
}
