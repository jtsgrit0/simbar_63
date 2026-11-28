const tracks=Object.freeze([
  'daydesk-audio/jessy-alt-rock.m4a',
  'daydesk-audio/edison-metal.m4a',
  'daydesk-audio/jaiden-jazz.m4a'
]);

export class DayDeskMusic {
  constructor(){
    this.context=null;this.owner=-1;this.voice=null;this.gain=null;
    this.buffers=new Map();this.pending=null;this.failed=false;
  }
  sync(owner,trigger=false){
    if(!Number.isInteger(owner)||!tracks[owner]){this.stop();return;}
    if(owner!==this.owner){this.stop();this.owner=owner;}
    if(trigger){
      this.failed=false;
      try{
        const AudioContext=globalThis.AudioContext||globalThis.webkitAudioContext;
        if(!AudioContext)return;
        this.context??=new AudioContext();
        if(this.context.state!=='running')void this.context.resume().catch(()=>{});
      }catch{return;}
    }
    const context=this.context;
    if(!context||context.state!=='running'||this.voice||this.pending||this.failed)return;
    const cached=this.buffers.get(owner);
    if(cached){this.play(cached);return;}
    const request=new AbortController();this.pending=request;
    void this.load(owner,context,request);
  }
  async load(owner,context,request){
    try{
      const response=await fetch(new URL(tracks[owner],import.meta.url),{signal:request.signal});
      if(!response.ok)throw new Error('Day desk audio is unavailable');
      const buffer=await context.decodeAudioData(await response.arrayBuffer());
      if(this.pending!==request||this.owner!==owner||request.signal.aborted)return;
      this.buffers.set(owner,buffer);
      this.pending=null;
      if(context.state==='running')this.play(buffer);
    }catch{
      if(this.pending===request){this.pending=null;this.failed=true;}
    }
  }
  play(buffer){
    try{
      const context=this.context;
      const gain=context.createGain(),voice=context.createBufferSource();
      voice.buffer=buffer;voice.loop=true;
      voice.connect(gain);gain.connect(context.destination);
      const time=context.currentTime;
      gain.gain.setValueAtTime(0,time);gain.gain.linearRampToValueAtTime(.24,time+.3);
      this.voice=voice;this.gain=gain;voice.start(time);
    }catch{this.stopVoice();this.failed=true;}
  }
  stopVoice(){
    if(!this.voice)return;
    try{this.voice.stop();}catch{}
    this.voice.disconnect();this.gain.disconnect();this.voice=null;this.gain=null;
  }
  stop(){
    if(this.owner<0)return;
    this.owner=-1;
    this.pending?.abort();this.pending=null;
    this.failed=false;this.stopVoice();
  }
  destroy(){
    this.stop();this.buffers.clear();
    if(this.context&&this.context.state!=='closed')void this.context.close().catch(()=>{});
    this.context=null;
  }
}
