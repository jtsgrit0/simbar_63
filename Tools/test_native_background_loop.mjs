import assert from 'node:assert/strict';
import {createBackgroundLoop} from './WebGLTemplate/FastGame/background-loop.js';

function target(){
  const listeners=new Map();return{
    addEventListener(type,callback){if(!listeners.has(type))listeners.set(type,new Set());listeners.get(type).add(callback);},
    removeEventListener(type,callback){listeners.get(type)?.delete(callback);},
    dispatch(type){for(const callback of [...listeners.get(type)||[]])callback();},
    count(){return[...listeners.values()].reduce((n,set)=>n+set.size,0);}
  };
}
function fixture({hidden=false,focused=true,initial=0}={}){
  let stamp=initial,next=0;const rafs=new Map(),timers=new Map(),frames=[],doc=target(),view=target();
  Object.assign(doc,{hidden,visibilityState:hidden?'hidden':'visible',hasFocus:()=>focused});
  const loop=createBackgroundLoop({document:doc,window:view,now:()=>stamp,onframe:(now,dt,mode)=>frames.push({now,dt,...mode}),
    requestFrame:callback=>{const id=next++;rafs.set(id,callback);return id;},cancelFrame:id=>rafs.delete(id),
    setTimer:(callback,delay)=>{const id=next++;timers.set(id,{callback,at:stamp+delay});return id;},clearTimer:id=>timers.delete(id)});
  const clock=at=>{assert(at>=stamp,'The test clock must be monotonic');stamp=at;};
  return{loop,frames,rafs,timers,doc,view,clock,
    raf(at){clock(at);const entry=rafs.entries().next().value;assert(entry,'No animation frame is scheduled');rafs.delete(entry[0]);entry[1](at);},
    timer(at){clock(at);const entry=timers.entries().next().value;assert(entry,'No timer is scheduled');assert(at>=entry[1].at,'Timer fired before its deadline');timers.delete(entry[0]);entry[1].callback();},
    focus(value,at){clock(at);focused=value;view.dispatch(value?'focus':'blur');},
    visibility(value,at){clock(at);doc.hidden=value;doc.visibilityState=value?'hidden':'visible';doc.dispatch('visibilitychange');}
  };
}
const evidence={};

// The foreground keeps its previous 100 ms cap and one continuous clock.
{
  const h=fixture();assert.equal(h.loop.running,false);assert(h.loop.start());assert(h.loop.start());
  assert.equal(h.rafs.size,1);assert.equal(h.timers.size,0);assert.equal(h.doc.count(),1);assert.equal(h.view.count(),2);
  h.raf(16);h.raf(16);h.raf(1016);assert.deepEqual(h.frames,[{now:16,dt:.016,background:false,hidden:false},{now:1016,dt:.1,background:false,hidden:false}]);
  h.loop.destroy();evidence.foregroundCapAndIdempotentStart=true;
}

// Losing focus does not turn a visible venue into a 4 fps timer. The watchdog
// skips recent real frames, but takes over if the browser stops delivering them.
{
  const h=fixture();h.loop.start();h.focus(false,0);assert(h.loop.background);assert.equal(h.loop.hidden,false);assert.equal(h.rafs.size,1);assert.equal(h.timers.size,1);
  for(let at=16;at<=240;at+=16)h.raf(at);
  const count=h.frames.length;h.timer(250);assert.equal(h.frames.length,count);h.raf(256);assert(h.frames.every(x=>x.dt===.016&&x.background&&!x.hidden));
  h.timer(500);assert.equal(h.frames.length,count+1);h.timer(750);assert.equal(h.frames.at(-1).dt,.494);
  const afterWatchdog=h.frames.length;h.raf(750);assert.equal(h.frames.length,afterWatchdog,'RAF and watchdog counted the same time twice');assert.equal(h.rafs.size,1);
  h.loop.destroy();evidence.visibleUnfocused={normalFrameCount:count+1,watchdogDelta:.494,noDuplicateTick:true};
}

// Hidden tabs cancel RAF and account for actual elapsed timer time rather than
// assuming 250 ms. Long suspension consumes at most five seconds, without a
// later backlog or replay of canceled callbacks.
{
  const h=fixture();h.loop.start();h.raf(16);const staleRaf=h.rafs.values().next().value;
  h.visibility(true,20);assert.equal(h.rafs.size,0);assert.equal(h.timers.size,1);assert.deepEqual(h.frames.at(-1),{now:20,dt:.004,background:true,hidden:true});
  h.clock(80);staleRaf(80);assert.equal(h.frames.at(-1).now,20);
  h.timer(270);h.timer(2270);h.timer(12270);h.timer(12520);assert.deepEqual(h.frames.slice(-4).map(x=>x.dt),[.25,2,5,.25]);
  const staleTimer=h.timers.values().next().value.callback;h.visibility(false,12700);
  assert.equal(h.rafs.size,1);assert.equal(h.timers.size,0);assert.deepEqual(h.frames.at(-1),{now:12700,dt:.18,background:false,hidden:false});
  h.raf(12716);const count=h.frames.length;staleTimer();assert.equal(h.frames.length,count);assert.equal(h.frames.at(-1).dt,.016);
  h.loop.destroy();evidence.hiddenTimer={deltas:[.25,2,5,.25],visibleReturnDelta:.18,staleCallbacksIgnored:true};
}

// Reappearing or focusing after a throttled gap first consumes that background
// gap with the background cap, then resumes ordinary small foreground frames.
{
  const h=fixture({hidden:true,focused:false,initial:10000});h.loop.start();assert.equal(h.rafs.size,0);h.timer(16000);assert.equal(h.frames.at(-1).dt,5);
  h.visibility(false,19000);assert.deepEqual(h.frames.at(-1),{now:19000,dt:3,background:true,hidden:false});assert.equal(h.rafs.size,1);assert.equal(h.timers.size,1);
  h.focus(true,20000);assert.deepEqual(h.frames.at(-1),{now:20000,dt:1,background:false,hidden:false});assert.equal(h.timers.size,0);h.raf(20016);assert.equal(h.frames.at(-1).dt,.016);
  h.loop.destroy();evidence.lastBackgroundGapBeforeFocus=true;
}

// Teardown owns every resource, including requests whose callback was already
// queued by the browser. It cannot restart itself or retain document listeners.
{
  const h=fixture({focused:false});h.loop.start();const raf=h.rafs.values().next().value,timer=h.timers.values().next().value.callback;
  h.loop.destroy();h.loop.destroy();assert.equal(h.loop.running,false);assert.equal(h.rafs.size,0);assert.equal(h.timers.size,0);assert.equal(h.doc.count()+h.view.count(),0);assert.equal(h.loop.start(),false);
  h.clock(300);raf(300);timer();h.doc.dispatch('visibilitychange');h.view.dispatch('blur');h.view.dispatch('focus');assert.equal(h.frames.length,0);assert.equal(h.rafs.size+h.timers.size,0);
  const unused=fixture();unused.loop.destroy();assert.equal(unused.loop.start(),false);evidence.fullTeardownAndStaleNoOp=true;
}
console.log(JSON.stringify({passed:true,checks:['foreground clock and clamp','visible unfocused keeps RAF','watchdog skips active RAF and shares clock','hidden timer elapsed time and five-second cap','immediate visible/focus return','stale callback and resource cleanup'],evidence},null,2));
