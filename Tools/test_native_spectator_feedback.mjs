import assert from 'node:assert/strict';
import {SpectatorFeedback as Feedback} from './WebGLTemplate/FastGame/spectator-feedback.js';

const crowd=(count=12,{seated=false}={})=>Array.from({length:count},(_,i)=>({id:'patron-'+i,role:seated?'seated':'guest',visible:true,
  behavior:i%3===0?'headbang':i%3===1?'sway':'listen',moving:false,action:'idle'}));
const fresh=(actors,owner=0)=>{const s=Feedback.create({session:1,owner});Feedback.scores(s,{actors});return s;};
const score=(s,actors)=>Feedback.scores(s,{actors});
function serve(s,id,{quality=1,wait=0,preferred=true,...extra}={}){
  Feedback.service(s,{guestId:id,item:0,quality,preferred,patience:80*(1-wait),maxPatience:80,crowdCount:12,capacity:64,...extra});
}
function react(s,actors,id,seconds,{serial=1,participating=actors.length,secondsLeft=110,capacity=64,...extra}={}){
  for(let i=0;i<actors.length;i++)actors[i].festivalAction=i<participating?id:'';
  for(let t=0;t<seconds;t+=.1)Feedback.tick(s,Math.min(.1,seconds-t),{actors,actionId:id,actionSerial:serial,secondsLeft,duration:180,
    showId:'freewill',musicPlaying:true,capacity,...extra});
}

// Empty evidence, idling, a muted track and queued customers never earn points.
{
  const a=crowd(),s=fresh(a);
  assert.equal(score(s,a).satisfaction,0);assert.equal(score(s,a).performance,0);
  for(let i=0;i<100;i++)Feedback.tick(s,.1,{actors:a,musicPlaying:true,actionId:'',capacity:64});
  assert.equal(score(s,a).satisfaction,0);assert.equal(score(s,a).performance,0);
  react(s,a,'horns',8,{musicPlaying:false});
  assert.equal(score(s,a).satisfaction,0);assert.equal(score(s,a).performance,0);
  react(s,a,'horns',8,{participating:0});assert.equal(score(s,a).performance,0);
  for(const actor of a){actor.festivalAction='horns';actor.action='queue';}
  Feedback.tick(s,5,{actors:a,actionId:'horns',musicPlaying:true});assert.equal(score(s,a).performance,0);
}

// Equal order counts produce different service judgements; orders themselves
// cannot award concert response. Actual quality, delay and preferences matter.
{
  const a=crowd(),good=fresh(a),late=fresh(a),poor=fresh(a),plain=fresh(a);
  serve(good,a[0].id);serve(late,a[0].id,{wait:.98});serve(poor,a[0].id,{quality:.2});serve(plain,a[0].id,{quality:.65,preferred:false});
  assert(score(good,a).satisfaction>score(plain,a).satisfaction);
  assert(score(plain,a).satisfaction>score(late,a).satisfaction);
  assert(score(good,a).satisfaction>score(poor,a).satisfaction);
  for(const s of [good,late,poor,plain])assert.equal(score(s,a).performance,0);
}

// Serving the same person repeatedly approaches their own endorsement cap;
// meeting more actual guests' needs has more room-wide impact.
{
  const a=crowd(),one=fresh(a),many=fresh(a);
  serve(one,a[0].id);const first=score(one,a).satisfaction;
  serve(one,a[0].id);const second=score(one,a).satisfaction-first;
  for(let i=0;i<58;i++)serve(one,a[0].id);
  assert(second<first);
  for(let i=0;i<6;i++)serve(many,a[i].id);
  assert(score(many,a).satisfaction>score(one,a).satisfaction*3);
  assert.equal(score(many,a).serviceCoverage,.5);
}

// Response comes from participating witnesses and elapsed experience, not an
// instant button reward. Identical buttons are weaker for a sparse response.
{
  const a=crowd(),all=fresh(a),few=fresh(a),pressed=fresh(a);
  react(pressed,a,'horns',0);assert.equal(score(pressed,a).performance,0);
  react(all,a,'horns',8);react(few,a,'horns',8,{participating:2});
  assert(score(all,a).performance>score(few,a).performance*4);
  assert(score(all,a).performance>score(all,a).satisfaction*4);
}

// The same crowd action is judged in context: venue, seating, set character,
// crowd density and the point in the set when an encore is requested.
{
  const a=crowd(),ff=fresh(a,1),listening=fresh(a,2),early=fresh(a),late=fresh(a),packed=fresh(a,1),soft=fresh(a,1);
  react(ff,a,'mosh',8);react(listening,a,'mosh',8);react(packed,a,'mosh',8,{capacity:12});react(soft,a,'mosh',8,{showId:'july'});
  assert(score(ff,a).performance>score(listening,a).performance*2);
  assert(score(ff,a).performance>score(packed,a).performance);
  assert(score(ff,a).performance>score(soft,a).performance);
  react(early,a,'encore',10,{secondsLeft:160});react(late,a,'encore',10,{secondsLeft:20});
  assert(score(late,a).performance>score(early,a).performance*4);
  const seats=crowd(12,{seated:true}),sitting=fresh(seats,1);react(sitting,seats,'mosh',8);
  assert(score(ff,a).performance>score(sitting,seats).performance);
}

// Repeated requests fatigue an audience; sustained different experiences with
// the same total time are more effective. Equipment only helps heard activity.
{
  const a=crowd(),repeat=fresh(a),variety=fresh(a),sound=fresh(a),normal=fresh(a);
  const actions=['horns','sing','dance','airguitar','headbang'];
  for(let i=0;i<actions.length;i++){react(repeat,a,'horns',8,{serial:i+1});react(variety,a,actions[i],8,{serial:i+1});}
  assert(score(variety,a).performance>score(repeat,a).performance);
  react(sound,a,'horns',8,{soundLevel:3});react(normal,a,'horns',8,{soundLevel:0});
  assert(score(sound,a).performance>score(normal,a).performance);
  const idle=fresh(a);for(const actor of a)actor.festivalAction='';Feedback.tick(idle,5,{actors:a,soundLevel:3,musicPlaying:true});
  assert.equal(score(idle,a).performance,0);
}

// An unformed surfer and walking/queued patrons are not fake participation.
{
  const a=crowd(1),s=fresh(a,1);Object.assign(a[0],{festivalLead:true,festivalLiftReady:false});
  react(s,a,'surf',5);assert.equal(score(s,a).performance,0);
  a[0].festivalLiftReady=true;react(s,a,'surf',5);assert(score(s,a).performance>0);
}

// A bad interaction can lower an already earned judgement and a long queue
// affects the person who waits; another customer's score is not subtracted.
{
  const a=crowd(),s=fresh(a);serve(s,a[0].id);react(s,a,'sing',12);
  const before=score(s,a);Feedback.disappoint(s,{guestId:a[0].id,reason:'timeout',severity:1});
  assert(score(s,a).satisfaction<before.satisfaction);assert(score(s,a).performance<before.performance);
  const other=structuredClone(s.guests[a[1].id]);
  Feedback.tick(s,5,{actors:a,musicPlaying:false,serviceQueue:[{id:a[0].id,patience:1,maxPatience:80}]});
  assert.deepEqual(s.guests[a[1].id],other);
}

// Real evidence survives pause/save/restore and post-show disappearance. A new
// night or an old save without a ledger has no invented 94% / 98% credit.
{
  const a=crowd(),s=fresh(a);serve(s,a[0].id);react(s,a,'dance',9);
  const original=score(s,a),saved=JSON.parse(JSON.stringify(s));
  Feedback.tick(s,5,{actors:a,actionId:'dance',musicPlaying:true,paused:true});assert.deepEqual(s,saved);
  const restored=Feedback.sanitize(saved,{session:1,owner:0});assert.deepEqual(score(restored,a),original);
  for(const actor of a)actor.visible=false;assert.deepEqual(score(restored,a),original);
  assert.equal(score(Feedback.create({session:2,owner:0}),a).satisfaction,0);
  assert.equal(score(Feedback.sanitize(null,{session:1,owner:0}),a).performance,0);
  const corrupt=Feedback.sanitize({version:1,session:1,owner:0,guests:{bad:{orders:1,serviceTotal:Infinity,enjoyedSeconds:Infinity,frustration:-4}}},{session:1,owner:0});
  const safe=score(corrupt,[]);assert(Number.isFinite(safe.satisfaction)&&Number.isFinite(safe.performance));
  assert(safe.satisfaction>=0&&safe.satisfaction<=1&&safe.performance>=0&&safe.performance<=1);
}
// A guest who needs assistance receives a temporary actor body, not a second
// spectator identity. That substitution cannot dilute the whole room's score.
{
  const a=crowd(),s=fresh(a);serve(s,a[0].id);react(s,a,'sing',5);
  const before=score(s,a);
  a[0].visible=false;
  a.push({...a[0],id:'assisted-guest',assistedSourceId:a[0].id,role:'assisted',action:'needs-help',festivalAction:'',visible:true});
  assert.deepEqual(score(s,a),before);
  Feedback.tick(s,.1,{actors:a,musicPlaying:false});
  assert.equal(score(s,a).audienceCount,12);
  assert.equal(Object.hasOwn(s.guests,'assisted-guest'),false);
}
console.log('PASS: earned independent spectator judgements, real participation, quality/wait/preferences, individual saturation, timing/venue/variety, persistence and zero-start.');
