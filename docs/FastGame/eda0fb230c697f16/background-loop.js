// Keep one game-time clock across visible animation frames and background
// timers. This schedules updates only; pause, audio and orientation stay owned
// by the game rather than being inferred from window focus here.
export function createBackgroundLoop({onframe,document:doc=globalThis.document,window:view=globalThis.window,
  now=()=>globalThis.performance.now(),requestFrame=callback=>view.requestAnimationFrame(callback),cancelFrame=id=>view.cancelAnimationFrame(id),
  setTimer=(callback,delay)=>view.setTimeout(callback,delay),clearTimer=id=>view.clearTimeout(id)}={}){
  if(typeof onframe!=='function')throw new TypeError('onframe must be a function');
  const interval=250;
  let running=false,destroyed=false,mode={background:false,hidden:false},last=0,lastRaf=-Infinity,raf=null,timer=null,rafGeneration=0,timerGeneration=0;
  const readMode=()=>{const hidden=doc.hidden===true||doc.visibilityState==='hidden';return{hidden,background:hidden||(typeof doc.hasFocus==='function'&&!doc.hasFocus())};};
  const time=()=>{const value=now();return Number.isFinite(value)?Math.max(last,value):last;};
  const cancelRaf=()=>{rafGeneration++;if(raf!==null)cancelFrame(raf);raf=null;};
  const cancelTimer=()=>{timerGeneration++;if(timer!==null)clearTimer(timer);timer=null;};
  const deliver=(stamp,wasBackground=mode.background)=>{
    const elapsed=Math.max(0,(stamp-last)/1000);last=stamp;
    if(elapsed>0)onframe(stamp,Math.min(wasBackground?5:.1,elapsed),{background:mode.background,hidden:mode.hidden});
  };
  const scheduleRaf=()=>{
    if(!running||mode.hidden||raf!==null)return;
    const token=++rafGeneration;
    raf=requestFrame(()=>{
      if(!running||token!==rafGeneration||mode.hidden)return;
      raf=null;const stamp=time();lastRaf=stamp;scheduleRaf();deliver(stamp);
    });
  };
  const scheduleTimer=()=>{
    if(!running||!mode.background||timer!==null)return;
    const token=++timerGeneration;
    timer=setTimer(()=>{
      if(!running||token!==timerGeneration)return;
      timer=null;const stamp=time();scheduleTimer();
      // A visible window on another monitor keeps its normal frame rate. The
      // watchdog updates only if those frames have actually stopped arriving.
      if(mode.hidden||stamp-lastRaf>=interval)deliver(stamp);
    },interval);
  };
  const reconcile=()=>{
    if(!running)return;
    const next=readMode();if(next.hidden===mode.hidden&&next.background===mode.background)return;
    const wasBackground=mode.background;mode=next;
    if(mode.hidden)cancelRaf();else scheduleRaf();
    if(mode.background)scheduleTimer();else cancelTimer();
    // Consume the final background gap before restoring the foreground clamp.
    // Metadata describes the current visibility, so hidden rendering may stop.
    deliver(time(),wasBackground);
  };
  return{
    get running(){return running;},get background(){return mode.background;},get hidden(){return mode.hidden;},
    start(){
      if(destroyed)return false;if(running)return true;
      mode=readMode();last=time();lastRaf=-Infinity;running=true;
      doc.addEventListener('visibilitychange',reconcile);view.addEventListener('focus',reconcile);view.addEventListener('blur',reconcile);
      scheduleRaf();scheduleTimer();return true;
    },
    destroy(){
      if(destroyed)return;running=false;destroyed=true;cancelRaf();cancelTimer();
      doc.removeEventListener('visibilitychange',reconcile);view.removeEventListener('focus',reconcile);view.removeEventListener('blur',reconcile);
    }
  };
}
