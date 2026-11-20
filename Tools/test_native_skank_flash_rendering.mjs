import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {createWorld,pixelFestivalPose,pixelFestivalFrame} from './WebGLTemplate/FastGame/world.js';

const state={phase:'playing',owner:2,journey:{secondsLeft:120},paused:false};
const make=(id,slot=0)=>({id:'patron-'+slot,slot,role:'guest',audienceVersion:1,x:1,z:-1,y:0,action:'listen',behavior:'listen',visible:true,moving:false,
  festivalAction:id,festivalRemaining:6,festivalDuration:9,behaviorPhase:0,animationSpeed:1});
const a=make('skank'),before=JSON.stringify(a),left=pixelFestivalPose(a,state,.1),right=pixelFestivalPose(a,state,.6);
assert.equal(left.festivalAction,'skank');assert.equal(left.skankStep,1);assert.equal(right.skankStep,-1);
assert(left.skankKnee>.5&&right.skankKnee>.5);assert(left.skankArm>0&&right.skankArm<0);
assert.equal(JSON.stringify(a),before,'A presentation never moves or changes the actual actor');
assert.equal(pixelFestivalPose({...a,moving:true},state,1),null);
assert.equal(pixelFestivalPose({...a,role:'seated'},state,1),null);
assert.equal(pixelFestivalPose({...a,seat:true},state,1),null);
assert.equal(pixelFestivalPose({...a,drunkIncident:'fight'},state,1),null);
assert.equal(pixelFestivalPose(a,{...state,inspection:{freezePerformers:true}},1),null);
assert.equal(pixelFestivalPose(a,{...state,phase:'closing'},1),null);
assert.equal(pixelFestivalPose({...a,festivalRemaining:0,behavior:'listen'},state,1),null);
const flash=make('flash'),flashPose=pixelFestivalPose(flash,state,.2);
assert(flashPose.phoneLight);assert.equal(flashPose.action,'highfive');
assert.notEqual(flashPose.phoneWave,pixelFestivalPose(flash,state,.9).phoneWave);
assert.equal(pixelFestivalPose({...flash,role:'seated'},state,.2).phoneLight,true);
assert.equal(pixelFestivalPose({...flash,festivalRemaining:0},state,.2),null);
assert.equal(pixelFestivalPose(flash,{...state,inspection:{freezePerformers:true}},.2),null);

function context(){return new Proxy({draws:[],rects:[],createLinearGradient(){return{addColorStop(){}};},drawImage(...args){this.draws.push(args);},fillRect(...args){this.rects.push({color:this.fillStyle,args});}},{get:(o,k)=>k in o?o[k]:(()=>{})});}
function canvas(){const ctx=context(),c={width:960,height:558,style:{},getContext:()=>ctx,getBoundingClientRect:()=>({width:960,height:558}),ctx};ctx.canvas=c;return c;}
globalThis.document={createElement:()=>canvas()};globalThis.ResizeObserver=class{observe(){}disconnect(){}};
const art=name=>new URL('./WebGLTemplate/FastGame/art/'+name,import.meta.url),meta=JSON.parse(readFileSync(art('sprites.json'))),festival=JSON.parse(readFileSync(art('festival-models.json')));
const png=file=>{const b=readFileSync(art(file));return{id:file,width:b.readUInt32BE(16),height:b.readUInt32BE(20)};};
const atlases=Object.fromEntries(Object.entries(meta.atlases).map(([key,v])=>[key,png(v.file)]));
const festivalAtlases=Object.fromEntries(Object.entries(festival.atlases).map(([key,v])=>[key,png(v.file)]));
const target=canvas(),world=createWorld(target,{assets:{venue:{width:960,height:558},spriteMeta:meta,atlases,festivalModels:festival,festivalAtlases,
  venueMeta:{occluders:[],seatedActors:{'freebird-wall-0':{feet:[250,300],depth:301,hideShadow:true}}}}});
let now=0;
for(let slot=0;slot<8;slot++){
  const actor=make('skank',slot);target.ctx.draws=[];world.render(now+=100,{...state,actors:[actor]});
  const limbs=target.ctx.draws.filter(draw=>draw[0]===atlases.walk||draw[0]===atlases.wardrobe);
  assert.equal(limbs.length,6,'The original actor is reassembled into head, torso, two arms and two alternating legs');
  assert(limbs.every(draw=>Math.floor(draw[2]/64)===slot),'Every body part keeps the original wardrobe');
}
const phone=make('flash');target.ctx.rects=[];world.render(now+=100,{...state,actors:[phone]});
assert(target.ctx.rects.some(r=>r.color==='#283540'&&r.args[2]===3&&r.args[3]===5),'A real local pixel phone is drawn');
assert(target.ctx.rects.some(r=>r.color==='#eaf8ff38'&&r.args[2]===11&&r.args[3]===11),'Light stays local to the person');
const seated={...make('flash'),id:'freebird-wall-0',role:'seated',waitingSeat:true,styleKey:'Suit'};
target.ctx.rects=[];world.render(now+=100,{...state,owner:0,actors:[seated]});
const seatedPresentation=world.getAudiencePresentation({...state,owner:0,actors:[seated]})[0];
assert(seatedPresentation.seated&&seatedPresentation.phoneLight,'A seated spectator keeps its seat and raises the phone');
assert(target.ctx.rects.some(r=>r.color==='#283540'));
world.render(now+=100,{...state,actors:[phone]});const paused={...state,paused:true,actors:[phone]};
world.render(now+=100,paused);const frozenPhone=world.getAudiencePresentation(paused);world.render(now+=500,paused);assert.deepEqual(world.getAudiencePresentation(paused),frozenPhone,'Phone wave freezes on manual pause');
const ended={...phone,festivalRemaining:0};target.ctx.rects=[];world.render(now+=100,{...state,actors:[ended]});assert(!target.ctx.rects.some(r=>r.color==='#283540'),'No phone is drawn after the action expires');
target.ctx.rects=[];world.render(now+=100,{...state,inspection:{freezePerformers:true},actors:[phone]});assert(!target.ctx.rects.some(r=>r.color==='#283540'),'Inspection freezes the performer and removes active lighting');
world.destroy();
console.log('PASS: alternating knees and bent-arm motion using eight preserved outfits, local phone lights including seats, action end, inspection, pause, and immutable actor positions.');
