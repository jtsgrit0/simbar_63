// Real StreamingAssets tracks are fetched only after a user gesture.
export class MusicPlayer{
  constructor({getIndex,tracks,baseURL,AudioClass=globalThis.Audio,onchange=()=>{}}){
    this.getIndex=getIndex;this.tracks=tracks;this.baseURL=baseURL;this.AudioClass=AudioClass;this.onchange=onchange;
    this.audio=null;this.index=-1;this.awake=false;this.paused=false;this.failed=false;
  }
  wake(){if(!this.awake){this.awake=true;this.sync();}}
  sync(){
    if(!this.awake||!this.AudioClass)return;
    const index=this.getIndex();if(index===this.index)return;
    this.audio?.pause();if(this.audio){this.audio.removeAttribute?.('src');this.audio.load?.();}
    const track=this.tracks[index];if(!track)return;
    this.index=index;this.paused=false;this.failed=false;
    const audio=this.audio=new this.AudioClass();audio.preload='none';audio.loop=true;audio.volume=.14;
    audio.src=new URL('StreamingAssets/Music/'+encodeURIComponent(track.filename),this.baseURL).href;
    audio.addEventListener?.('error',()=>{if(this.audio===audio){this.failed=true;this.onchange();}});
    this.play();this.onchange();
  }
  play(){try{const pending=this.audio?.play();pending?.catch(()=>{this.paused=true;this.onchange();});}catch{this.paused=true;this.onchange();}}
  toggle(){if(!this.awake){this.wake();return;}this.sync();this.paused=!this.paused;if(this.paused)this.audio?.pause();else this.play();this.onchange();}
  select(){this.awake=true;const old=this.index;this.sync();if(this.index===old){this.paused=false;this.play();this.onchange();}}
  destroy(){this.audio?.pause();this.audio?.removeAttribute?.('src');this.audio?.load?.();this.audio=null;}
}
