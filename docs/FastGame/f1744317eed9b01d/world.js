/* Presentation of the real room and real game actors. Render timestamps are ms.
 * The HUD-free venue plate is static art; live actor behavior and effects remain
 * in core.js. Employee movement, guest exit, paper debt and rewards are unchanged. */
import {ownerVenue} from './owner-venues.js';
import {menuForOwner} from './petsounds-menu.js';
import {performingRoster,performingFrame,bandPerformanceProfile} from './band-models.js';
const REF_W=960,REF_H=558,ORTHO=8.4;
const EYE=[13,21,-25],LOOK=[0,1.45,.15];
const unit=v=>{const n=Math.hypot(...v);return v.map(x=>x/n);};
const cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
const dot=(a,b)=>a[0]*b[0]+a[1]*b[1]+a[2]*b[2];
const FORWARD=unit(LOOK.map((n,i)=>n-EYE[i])),RIGHT=unit(cross([0,1,0],FORWARD)),UP=cross(FORWARD,RIGHT);
const clamp=(n,a,b)=>Math.max(a,Math.min(b,Number.isFinite(+n)?+n:a));
const finite=(n,f=0)=>Number.isFinite(+n)?+n:f;
const ready=i=>i&&((i.complete&&i.naturalWidth>0)||(i.width>0&&i.height>0&&!('complete'in i)));
const hash=v=>{let n=2166136261;for(const c of String(v))n=Math.imul(n^c.charCodeAt(0),16777619);return n>>>0;};
const frozenPerformer=(actor,state)=>!!state.inspection?.freezePerformers&&['guest','seated','support','surfer','assisted'].includes(actor.role);
const rng=seed=>{let n=seed>>>0;return()=>((n=(Math.imul(n,1664525)+1013904223)>>>0)/4294967296);};
const surface=(w,h)=>{const c=document.createElement('canvas');c.width=w;c.height=h;return c;};
const polygon=(c,points,color)=>{c.fillStyle=color;c.beginPath();points.forEach((p,i)=>i?c.lineTo(Math.round(p.x),Math.round(p.y)):c.moveTo(Math.round(p.x),Math.round(p.y)));c.closePath();c.fill();};
function ellipse(c,x,y,rx,ry,color){c.fillStyle=color;for(let row=-ry;row<=ry;row++){const w=Math.round(rx*Math.sqrt(Math.max(0,1-row*row/(ry*ry||1))));c.fillRect(Math.round(x-w),Math.round(y+row),w*2+1,1);}}
function hull(points){const p=points.slice().sort((a,b)=>a.x-b.x||a.y-b.y),turn=(a,b,c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);let low=[],high=[];for(const a of p){while(low.length>1&&turn(low.at(-2),low.at(-1),a)<=0)low.pop();low.push(a);}for(const a of p.reverse()){while(high.length>1&&turn(high.at(-2),high.at(-1),a)<=0)high.pop();high.push(a);}return low.slice(0,-1).concat(high.slice(0,-1));}

// Measured inside the cyan LED frame, leaving the authored truss and lamps intact.
export const PIXEL_LED_QUAD=Object.freeze([[665,117],[842,163],[842,230],[665,184]].map(Object.freeze));
export const PIXEL_LED_LAMPS=Object.freeze([[683,128],[735,140],[782,151],[822,162]].map(Object.freeze));
export const PIXEL_STAGE_LAMPS=Object.freeze([[643,121],...PIXEL_LED_LAMPS].map(Object.freeze));
export const PIXEL_STAGE_DECK=Object.freeze([[556,299],[612,242],[639,209],[854,266],[867,311],[789,373]].map(Object.freeze));
export function pixelLedCoverFit(imageWidth,imageHeight,quad=PIXEL_LED_QUAD){
  if(!(imageWidth>0&&imageHeight>0&&Number.isFinite(imageWidth)&&Number.isFinite(imageHeight)))return null;
  const [tl,tr,,bl]=quad,dx=tr[0]-tl[0],dy=tr[1]-tl[1],vx=bl[0]-tl[0],vy=bl[1]-tl[1];
  const planeWidth=Math.hypot(dx,dy),planeHeight=Math.hypot(vx,vy),scale=Math.min(planeWidth/imageWidth,planeHeight/imageHeight);
  const width=imageWidth*scale,height=imageHeight*scale;
  return{planeWidth,planeHeight,x:(planeWidth-width)/2,y:(planeHeight-height)/2,width,height,
    transform:[dx/planeWidth,dy/planeWidth,vx/planeHeight,vy/planeHeight,tl[0],tl[1]]};
}
export function pixelStageLightingActive(state={}){
  const journey=state.journey;
  return state.phase==='playing'&&!journey?.floorClosing&&!journey?.afterShow?.started&&!(Number.isFinite(journey?.secondsLeft)&&journey.secondsLeft<=0);
}
export function pixelStageLightingEnded(state={}){
  const journey=state.journey;
  return ['closing','results'].includes(state.phase)||!!journey?.floorClosing||!!journey?.afterShow?.started||journey?.session>0&&Number.isFinite(journey?.secondsLeft)&&journey.secondsLeft<=0;
}
export function pixelStageLightingProfile(state={}){
  const performance=bandPerformanceProfile(state);
  if(performance.legacy)return{speed:1,intensity:1,energy:1,colors:['#48d8ff','#ff50c9','#60dfff','#ffc16b']};
  const profiles=[
    {speed:.82,intensity:.78,colors:['#ffbb67','#e961a3','#58c9ca','#f3da83']},
    {speed:1.38,intensity:1.12,colors:['#50e7ff','#b366ff','#ff4da5','#83dfff']},
    {speed:.48,intensity:.52,colors:['#e9b675','#e89b7a','#a2c6c5','#f2d9a5']}
  ];
  const profile=profiles[state.owner],gentle=performance.energy/[.95,1.2,.65][state.owner];
  const level=clamp(state.visualPreview?.type==='upgrade'&&state.visualPreview.index===1?state.visualPreview.level??finite(state.upgrades?.[1])+1:state.upgrades?.[1],0,3);
  const event=state.visualPreview?.type==='event'?state.visualPreview.index:state.activeEvent;
  const colors=event===0?['#ffc877','#e994b8','#8ed6ce','#ffd9a2']:event===1?['#f456be','#8b82ff','#4ce7fc','#fa826b']:event===2?['#76f6c3','#d57dff','#ffce69','#7cdfff']:profile.colors;
  return{...profile,colors,speed:profile.speed*gentle,intensity:profile.intensity*(gentle<1?.86:1)*(1+level*.13),energy:performance.energy};
}

// Decorative fixtures follow the authored counter/stage/wall pixels. They do
// not introduce invisible navigation obstacles into the live audience floor.
export function pixelVenueFeatures(state={}){
  const owner=clamp(state.owner,0,2),upgrades=Array.from({length:3},(_,i)=>clamp(state.upgrades?.[i],0,3)),preview=state.visualPreview;
  if(preview?.type==='upgrade'&&Number.isInteger(preview.index)&&preview.index>=0&&preview.index<3)upgrades[preview.index]=clamp(preview.level??upgrades[preview.index]+1,1,3);
  const anchors=[[[402,211,66,263],[892,308,76,308],[259,244,76,244]],[[252,213,64,308],[896,307,76,307],[111,351,76,351]],[[286,273,70,359],[859,304,76,304],[122,285,76,285]]][owner];
  const result=upgrades.flatMap((level,index)=>level?[{id:['draught','sound','vip'][index]+'-'+level,type:'upgrade',index,level,venue:ownerVenue(owner).key,x:anchors[index][0],y:anchors[index][1],size:anchors[index][2],depth:anchors[index][3],preview:preview?.type==='upgrade'&&preview.index===index}]:[]);
  const event=preview?.type==='event'?preview.index:state.activeEvent;
  // Use the gap beside the vocalist, clear of the right-hand floor monitor.
  // The wider banner sits slightly left; authored performers stay in front.
  const eventAnchor=[[717,350,316],[728,350,324],[707,342,309]][owner];
  if(Number.isInteger(event)&&event>=0&&event<3)result.push({id:['open-mic','rock-showcase','fan-festival'][event],type:'event',index:event,venue:ownerVenue(owner).key,x:eventAnchor[0]-(event===2?8:0),y:eventAnchor[1],size:event===2?88:76,depth:eventAnchor[2],preview:preview?.type==='event'});
  return result;
}

// Final alignment/ambient light belongs to the room, not the saved purchase.
// Small Y shears retain vertical stems and cabinet edges; FF uses real
// opposite-facing draught artwork rather than severely skewing the front view.
export function pixelVenueFeatureStyle(feature={}){
  if(feature.type!=='upgrade'&&feature.type!=='event')return null;
  const venue=feature.venue||'original';
  const palettes={freebird:'brightness(0.88) saturate(0.72) sepia(0.08)',ff:'brightness(0.80) saturate(0.68)',original:'brightness(0.86) saturate(0.76) sepia(0.04)'};
  const shearY=feature.type==='event'?[.12,.12,.16][feature.index]||0:feature.index===0?(venue==='ff'?-.12:.10):feature.index===1?.12:.22;
  return{shearY,filter:palettes[venue]||palettes.original};
}

// Quiet audience moods use standing idle drawings; only an occasional cheer
// borrows an authored raised-hand social pose. Pit drawings stay in the pit.
// Offsets are only a
// small body pose: collision paths, feet, orders and serving stay in core.js.
export function pixelFestivalPose(actor={},state={},seconds=0){
  if(frozenPerformer(actor,state))return null;
  if(actor.drunkIncident)return null;
  if(state.phase!=='playing'||state.journey?.floorClosing||state.journey?.afterShow?.started||actor.visible===false||actor.moving||!['guest','seated','support','surfer'].includes(actor.role))return null;
  if(['queue','return','arrive','to-stage','exit','escorted','walk'].includes(actor.action))return null;
  let id=actor.festivalRemaining>0?actor.festivalAction:null;
  if(!id&&actor.audienceVersion&&['horns','dance'].includes(actor.behavior))id=actor.behavior;
  if(!['slam','mosh','surf','jump','horns','airguitar','sing','encore','headbang','dance','skank','flash'].includes(id))return null;
  if(id==='skank'&&(actor.role!=='guest'||actor.seat||actor.waitingSeat))return null;
  const phase=finite(actor.behaviorPhase,(hash(actor.id)%997)/997*Math.PI*2),speed=clamp(finite(actor.animationSpeed,1),.5,1.5),time=Math.max(0,finite(seconds))*speed+phase;
  const base={kind:'festival-'+id,festivalAction:id,action:'idle',animationSeconds:time,speed,phase,sway:0,bob:0,lean:0,lift:0,cheer:false};
  // A waiting spectator keeps the authored bent-knee seat drawing. A real
  // departure changes role/path in the simulation before any standing pose.
  if(actor.role==='seated'){
    if(id==='skank')return null;
    return{...base,kind:'seated-'+id,sway:Math.sin(time*2)*.3,bob:Math.sin(time*3)*.25,lean:Math.sin(time*2)*.009,
      ...(id==='flash'?{phoneLight:true,phoneWave:Math.sin(time*2.3)}:{})};
  }
  if(id==='surf'){
    if(actor.festivalLead)return actor.festivalLiftReady&&finite(actor.y)>.05
      ?{...base,action:'surf',atlasAction:'surf',lean:Math.sin(time*2.2)*.025}
      :{...base,atlasAction:'horns',sway:Math.sin(time*1.8)*.25};
    if(actor.festivalSupport)return{...base,action:'support',bob:Math.sin(time*4)*.45};
    return{...base,atlasAction:'horns',sway:Math.sin(time*2)*.45};
  }
  if(id==='jump')return{...base,atlasAction:'horns',lift:Math.max(0,Math.sin(time*4.4))*7,lean:Math.sin(time*2.2)*.012};
  if(id==='mosh')return{...base,action:'mosh',sway:Math.sin(time*4)*2.1,bob:Math.cos(time*4)*.8,lean:Math.sin(time*4)*.045};
  if(id==='slam')return{...base,action:'slam',sway:Math.sin(time*3.6)*2.6,bob:Math.abs(Math.sin(time*3.6))*.4,lean:Math.sin(time*3.6)*.065};
  if(id==='dance')return{...base,atlasAction:id,sway:Math.sin(time*3)*.8,bob:Math.sin(time*6)*.3};
  if(id==='airguitar')return{...base,atlasAction:id,sway:Math.sin(time*2.8)*.6};
  if(id==='skank'){
    const step=Math.sin(time*6.4);
    return{...base,action:'walk',skankStep:step>=0?1:-1,skankKnee:Math.abs(step),skankArm:step,
      sway:Math.sin(time*3.2)*.65,bob:-Math.abs(step)*.5,lean:Math.sin(time*3.2)*.025};
  }
  if(id==='flash')return{...base,action:'highfive',frameOverride:0,phoneLight:true,phoneWave:Math.sin(time*2.3),sway:Math.sin(time*1.4)*.35};
  return{...base,atlasAction:id,sway:Math.sin(time*1.8)*.25};
}

export function pixelFestivalFrame(actor={},state={},seconds=0,manifest={}){
  const pose=pixelFestivalPose(actor,state,seconds),action=pose?.atlasAction,definition=manifest.actions?.[action];
  if(!definition)return null;
  const styles=manifest.styles||{},keys=Object.keys(styles),explicit=String(actor.styleKey||actor.modelKey||'').split('/').at(-1);
  const index=Number.isInteger(actor.slot)?actor.slot:hash(actor.id),style=styles[explicit]!=null?explicit:keys[((index%keys.length)+keys.length)%keys.length];
  const frames=definition.frames?.length?definition.frames:[0],at=Math.floor(pose.animationSeconds*(definition.fps||6))%frames.length;
  return{action,atlas:definition.atlas,column:frames[at],row:styles[style]??0,style,pose};
}

// Reuse the same wardrobe and four directional, hand-forward drawings. Quick
// guard/jab/recoil poses distinguish a scuffle from the slow social greeting;
// this changes presentation only, never saved feet or navigation coordinates.
export function pixelDrunkFightPose(actor={},state={},seconds=0){
  if(!['playing','closing'].includes(state.phase)||actor.visible===false||actor.drunkIncident!=='fight'||actor.action!=='fight'||frozenPerformer(actor,state))return null;
  const age=Number.isFinite(actor.fightElapsed)?Math.max(0,actor.fightElapsed):Math.max(0,finite(seconds));
  const phase=clamp(actor.fightIndex,0,4)*.73,time=age+phase,step=Math.floor(time*9)%6;
  return{kind:'fight',action:'highfive',animationSeconds:time,speed:1,phase,
    frameOverride:[0,1,2,1,0,2][step],sway:Math.sin(time*13)*1.3,
    bob:-Math.max(0,Math.sin(time*13))*.7,lean:Math.sin(time*13)*.055,lift:0,
    cheer:false,strike:step===1||step===5};
}

export function pixelAudiencePose(actor={},state={},seconds=0){
  if(frozenPerformer(actor,state))return{kind:'inspection',action:'idle',animationSeconds:0,speed:0,phase:0,sway:0,bob:0,lean:0,lift:0,cheer:false,frameOverride:0};
  const fight=pixelDrunkFightPose(actor,state,seconds);if(fight)return fight;
  const festival=pixelFestivalPose(actor,state,seconds);if(festival)return festival;
  let action=String(actor.action||'idle');
  if(action==='idle'&&['listen','sway','cheer','headbang'].includes(actor.behavior))action=actor.behavior;
  const base={action,animationSeconds:seconds,speed:1,phase:0,sway:0,bob:0,lean:0,cheer:false};
  if(!['listen','sway','cheer','headbang'].includes(action)||actor.moving)return base;
  const seed=hash(actor.id),speed=clamp(finite(actor.animationSpeed,1),.35,1.8),phase=((finite(actor.behaviorPhase,(seed%997)/997*Math.PI*2)%(Math.PI*2))+Math.PI*2)%(Math.PI*2),time=Math.max(0,finite(seconds))*speed+phase;
  const pose={...base,kind:action,action:'idle',speed,phase};
  if(action==='listen')return{...pose,animationSeconds:time*.36,lean:Math.sin(time*.8)*.012};
  if(action==='sway')return{...pose,animationSeconds:time*.58,sway:Math.sin(time*1.6)*.8,bob:Math.sin(time*1.6+.4)*.25,lean:Math.sin(time*1.6)*.025};
  if(action==='headbang')return{...pose,animationSeconds:time*1.28,sway:Math.sin(time*3.8)*.25,bob:Math.max(0,Math.sin(time*5.2))*1.1,lean:Math.sin(time*5.2)*.04};
  const cycle=time%8.4,cheer=cycle>6.9,progress=clamp((cycle-6.9)/1.5,0,1);
  return{...pose,action:cheer?'highfive':'idle',animationSeconds:time*.95,sway:Math.sin(time*1.8)*.35,bob:cheer?-Math.sin(progress*Math.PI)*.7:0,lean:Math.sin(time*2.1)*.015,cheer,frameOverride:cheer?Math.min(3,Math.floor(progress*4)):null};
}
export const PIXEL_PAPER_COLORS=Object.freeze(['#ff6fa9','#62e2ed','#ffe17c','#b9a0ff']);
export function pixelGroundPaper(floor={}){
  const remaining=floor.litter>0?clamp(1-finite(floor.cleaned)/floor.litter,0,1):0;
  const counts=[Math.ceil(Math.min(64,Math.max(0,finite(floor.confettiThrows))*24)*remaining),Math.ceil(Math.min(40,Math.max(0,finite(floor.tissueThrows))*16)*remaining)];
  if(!counts[0]&&!counts[1])return[];
  const random=rng(44091),marks=[];
  for(let i=0;i<104;i++){
    const kind=i<64?0:1,index=kind?i-64:i,x=2.1+random()*7,z=-4.6+random()*5.4;
    if(index<counts[kind])marks.push({kind,index,x,z,color:kind?'#e8e2d5':PIXEL_PAPER_COLORS[index%4]});
  }
  return marks;
}
export function pixelPaperBurst(event={}){
  const kind=event.kind===1?1:0,random=rng((3917+finite(event.serial)*97+(kind?7001:0))>>>0),out=[];
  for(let i=0;i<(kind?28:56);i++)out.push({kind,index:i,x:3+random()*5,z:2.75+random()*.65,y:2.6+random()*1.6,
    vx:(random()-.5)*1.4,vz:-.65-random()*1.15,vy:(kind?.5:1)+random()*(kind?.8:1.2),phase:random()*Math.PI*2,age:0,
    color:kind?'#fff7e8':PIXEL_PAPER_COLORS[i%4]});
  return out;
}

// The four views are actual drawings. Mirroring a front view cannot create a back.
export function pixelDirection(dx,dz){
  const right=RIGHT[0]*dx+RIGHT[2]*dz,back=UP[0]*dx+UP[2]*dz;
  return(back>=0?'B':'F')+(right>=0?'R':'L');
}
export function pixelFacing(actor,role,action,state={}){
  if(actor.drunkIncident==='fight'){
    const partner=state.actors?.find(a=>a.id===actor.fightPartner);
    if(partner)return pixelDirection(partner.x-finite(actor.x),partner.z-finite(actor.z));
  }
  if(role==='barseated')return'BR';
  if(role==='seated'&&actor.waitingSeat)return pixelDirection(Math.sin(finite(actor.facing)),Math.cos(finite(actor.facing)));
  if(role==='seated')return Number(String(actor.id).split('-').at(-1))<4?'FL':'BR';
  const target=(x,z)=>pixelDirection(x-finite(actor.x),z-finite(actor.z));
  if(['highfive','hug','aftershow-wait'].includes(action)&&actor._partner){
    const partner=state.actors?.find(a=>a.id===actor._partner);if(partner)return target(partner.x,partner.z);
  }
  if(['singer','guitarist','bassist','drummer'].includes(role)&&['perform','idle'].includes(action)&&!actor.afterShowPhase)return target(5.75,-1.25);
  if(action==='mosh')return pixelDirection(-(finite(actor.z)+1.25),finite(actor.x)-5.75);
  if(action==='slam')return target(5.75,-1.25);
  if(action==='support'){
    const surfer=actor.festivalSupport?state.actors?.find(a=>a.festivalLead):state.actors?.find(a=>a.id==='crowd-surfer');
    return target(surfer?.x??6,surfer?.z??-4.25);
  }
  if(action==='queue'){
    const employee=state.actors?.find(a=>a.id===([0,1].includes(state.owner)?'bartender':'server'))||state.actors?.find(a=>a.id==='bartender');
    return target(employee?.x??-4.2,employee?.z??-.565);
  }
  const employee=['bartender','server'].includes(role)||actor.id?.startsWith('hired-');
  if(employee&&state.owner===0&&!actor.moving&&['idle','serve'].includes(action))return pixelDirection(0,-1);
  if(employee&&state.owner===1&&!actor.moving&&['idle','serve'].includes(action))return pixelDirection(1,0);
  if(employee&&!actor.moving&&['idle','serve'].includes(action)&&finite(actor.x)<-1.35&&finite(actor.z)>-1.5&&finite(actor.z)<.5)return'FL';
  if(!actor.moving&&role==='guest'&&(action==='idle'||['listen','sway','cheer','headbang'].includes(actor.action)||['listen','sway','cheer','headbang'].includes(actor.behavior)))return target(5.5,2.7);
  const facing=finite(actor.facing);return pixelDirection(Math.sin(facing),Math.cos(facing));
}

export function pixelStyle(actor,entry,meta={}){
  const explicit=actor.styleKey||actor.modelKey||meta.actorStyles?.[actor.id]||entry.style;
  const aliases=meta.styleAliases||{};
  if(explicit){const raw=String(explicit).split('/').at(-1),key=aliases[raw]||raw;if(!entry.variantStyles||entry.variantStyles[key]!=null)return key;}
  const styles=entry.styles||Object.keys(entry.variantStyles||{});
  if(!styles.length)return null;
  const number=Number(String(actor.id).split('-').at(-1));
  const index=Number.isInteger(actor.slot)?actor.slot:Number.isFinite(number)?number:hash(actor.id);
  return styles[((index%styles.length)+styles.length)%styles.length];
}

// A brief, staggered jump is presentation only. The gameplay positions and the
// grounded contact shadow continue to use the real stage floor throughout.
export function pixelBandJump(role,action,seconds,active=true,intensity=1){
  if(!active||!(intensity>0)||action!=='perform'||!['guitarist','bassist'].includes(role))return null;
  const phase=(Math.max(0,seconds)+(role==='bassist'?2.6:0))%10.4;
  if(phase<8.9||phase>=9.9)return null;
  const progress=phase-8.9,air=Math.max(0,Math.sin(clamp((progress-.125)/.75,0,1)*Math.PI));
  return{frame:Math.min(7,Math.floor(progress*8)),lift:Math.round(air*7*intensity),shadow:1-air*.32*Math.min(1.2,intensity)};
}

// Orders are the source of truth: serving, expiration or closing removes the
// bubble on the next painted frame, without a separate presentation timer.
export function pixelPendingOrders(state={},columns=6){
  if(state.phase!=='playing')return[];
  const actors=new Map((state.actors||[]).filter(a=>a.visible!==false).map(a=>[a.id,a])),seen=new Set();
  const menu=menuForOwner(state.owner);
  return(state.orders||[]).flatMap(order=>{
    const actor=actors.get(order.id),item=order.item;
    if(!actor||seen.has(order.id)||!Number.isInteger(item)||item<0||!menu[item]||item>=columns&&state.owner!==2)return[];
    seen.add(order.id);return[{actor,item}];
  });
}

export function pixelOrderBubbleLayout(actor,point,meta={}){
  const role=actor.role==='seated'&&actor.id?.startsWith('bar-seat-')?'barseated':actor.role;
  const entry=meta.roles?.[role]||meta.roles?.guest||{},action=entry.actions?.[actor.moving?'walk':actor.action]||{};
  const height=action.drawHeight||entry.drawHeight||meta.drawHeight||40;
  const frameHeight=action.cropHeight||entry.cropHeight||action.frameHeight||entry.frameHeight||meta.frameHeight||64;
  const footY=action.footY??entry.footY??meta.footY??frameHeight;
  const headOffset=Math.round(footY*height/frameHeight)-4;
  const width=32,bodyHeight=28,tail=4;
  return{x:Math.round(point.x-width/2),y:Math.round(point.y-headOffset-3-tail-bodyHeight),width,height:bodyHeight,tail};
}

// The card and its short tail move together with the customer's head.
export function pixelOrderBubblePlacements(state,pointFor,meta={},columns=6){
  return pixelPendingOrders(state,columns).map(order=>{
    const base=pixelOrderBubbleLayout(order.actor,pointFor(order.actor),meta);
    const tipX=base.x+base.width/2,tipY=base.y+base.height+base.tail;
    return{...order,...base,tipX,tipY,neckX:tipX};
  });
}

export function createWorld(canvas,{assets={},getState=()=>({})}={}){
  const ctx=canvas.getContext('2d',{alpha:false});if(!ctx)throw new Error('A 2D game canvas is required.');
  if(!ready(assets.venue))throw new Error('The detailed room artwork must finish loading before the game starts.');
  if(!assets.spriteMeta?.roles?.guest)throw new Error('The pixel character atlas metadata is missing.');
  if(!assets.spriteMeta.atlases&&!ready(assets.sprites))throw new Error('The pixel character artwork must finish loading before the game starts.');
  for(const key of Object.keys(assets.spriteMeta.atlases||{}))if(!ready(assets.atlases?.[key]))throw new Error('The '+key+' character atlas must finish loading before the game starts.');
  const originalVenue=assets.venue,originalVenueMeta=assets.venueMeta;
  const selectVenue=state=>{const key=ownerVenue(state.owner).key;assets.venue=assets.venues?.[key]||originalVenue;assets.venueMeta=assets.venueMetas?.[key]||originalVenueMeta;};
  const particles=[],seenEffects=new Set();
  let width=960,height=558,offsetX=0,offsetY=0,cssW=960,cssH=558,last=null,seconds=0,night=null,observedFloor,city=null,citySource=null,occluders=[],propLayers=[],plate=null,layerMeta=null,destroyed=false;
  const project=(x,z,y=0)=>{
    const calibration=assets.venueMeta?.projection;
    if(calibration){const {origin,xAxis,zAxis,yAxis}=calibration;return{x:origin[0]+x*xAxis[0]+z*zAxis[0]+y*yAxis[0],y:origin[1]+x*xAxis[1]+z*zAxis[1]+y*yAxis[1]};}
    const position=[x-EYE[0],y-EYE[1],z-EYE[2]],scale=REF_H/(2*ORTHO);
    return{x:REF_W/2+dot(position,RIGHT)*scale,y:REF_H/2-dot(position,UP)*scale};
  };
  const seatFor=a=>a.role==='seated'&&!a.moving&&(['idle','sit','drink','listen','sway','cheer'].includes(a.action||'idle')||a.action==='aftershow-wait'&&(!a.layout||a.layout==='original'))?assets.venueMeta?.seatedActors?.[a.seatId||a.id]:null;
  const actorOffset=a=>{
    const seat=seatFor(a);
    if(seat){const ground=project(finite(a.x),finite(a.z));return{x:seat.feet[0]-ground.x,y:seat.feet[1]-ground.y};}
    if(a._seatExitOrigin&&assets.venueMeta?.seatedActors?.[a.id]){
      const origin=a._seatExitOrigin,ground=project(finite(origin.x),finite(origin.z)),feet=assets.venueMeta.seatedActors[a.id].feet;
      const weight=1-clamp(Math.hypot(finite(a.x)-finite(origin.x),finite(a.z)-finite(origin.z)),0,1);
      if(weight>0)return{x:(feet[0]-ground.x)*weight,y:(feet[1]-ground.y)*weight};
    }
    const offset=assets.venueMeta?.actorOffsets?.[a.id]||[0,0],region=assets.venueMeta?.barRegion;
    const employee=['bartender','server'].includes(a.role)||a.id?.startsWith('hired-');
    let weight=employee&&region&&a.z>=region.zMin&&a.z<=region.zMax?clamp((region.xMax-a.x)/region.xFade,0,1):0;
    if(weight&&region.boundedX)weight*=clamp((a.x-region.xMin)/Math.max(.01,region.xFade),0,1);
    if(weight&&region.zFade>0)weight*=clamp((a.z-region.zMin)/region.zFade,0,1)*clamp((region.zMax-a.z)/region.zFade,0,1);
    const stageWeight=a.role==='band'&&Number.isFinite(a.stageLevel)?clamp(a.stageLevel,0,1):1;
    const stageFoot=a.role==='band'&&assets.venueMeta?.bandStageFeet?.[a.id];
    if(stageFoot){const home=project(finite(a.homeX,a.x),finite(a.homeZ,a.z),finite(a.homeY));return{x:(stageFoot[0]-home.x)*stageWeight,y:(stageFoot[1]-home.y)*stageWeight};}
    const elevation=finite(assets.venueMeta?.stageElevationPixels)*(a.role==='band'?stageWeight:0);
    return{x:offset[0]*stageWeight+(region?.offset?.[0]||0)*weight,y:offset[1]*stageWeight+(region?.offset?.[1]||0)*weight-elevation};
  };
  const actorPoint=(a,y=0)=>{const p=project(finite(a.x),finite(a.z),y),o=actorOffset(a);return{x:p.x+o.x,y:p.y+o.y};};
  // The rear booth guests pass behind the tall end of the shelf before
  // reaching the right-hand aisle. Clip only their moving exit sprites at the
  // shelf's right edge, so the person emerges progressively rather than
  // appearing above the shelf. Seated guests and other actors are unchanged.
  const behindRearShelf=(actor,state)=>{
    if(state.owner!==2||state.phase!=='closing'||actor.role==='seated'||!/^lounge-[0-3]$/.test(actor.id||''))return false;
    const item=state.journey?.afterShow?.audience?.find(entry=>entry.id===actor.id);
    return !!actor._rearExitActive||!!(item?.phase==='exit'&&Array.isArray(item.egress)&&item.egressAt<item.egress.length);
  };
  const modelRoster=state=>assets.bandModels?performingRoster(state,assets.bandModels):[];
  function modelPoint(entry){
    const p=actorPoint(entry.actor,finite(entry.actor.y));if(!entry.extra)return p;
    const member=entry.member,feet=assets.venueMeta?.bandStageFeet||{};
    const from=feet[member.from],to=feet[member.to],parent=feet[member.anchor];
    const weight=Number.isFinite(entry.actor.stageLevel)?clamp(entry.actor.stageLevel,0,1):1;
    const stage=from&&to&&parent?{x:from[0]+(to[0]-from[0])*member.mix-parent[0],y:from[1]+(to[1]-from[1])*member.mix-parent[1]}:{x:0,y:0};
    return{x:p.x+(stage.x+(member.stageOffset?.x||0))*weight+(member.floorOffset?.x||0)*(1-weight),
      y:p.y+(stage.y+(member.stageOffset?.y||0))*weight+(member.floorOffset?.y||0)*(1-weight)};
  }
  function drawBandModel(entry,state){
    const manifest=assets.bandModels,sheet=assets.bandAtlases?.[entry.bandId];
    if(!ready(sheet))throw new Error('Missing performing band atlas: '+entry.bandId);
    const {action,column}=bandFrame(entry,state),p=modelPoint(entry),active=['playing','closing'].includes(state.phase)&&!frozenPerformer(entry.actor,state);
    const jump=entry.jumpIntensity?pixelBandJump(entry.role,action,seconds*(entry.performanceSpeed||1)+(entry.performancePhase||0),active,entry.jumpIntensity):null;
    const fw=manifest.frameWidth||64,fh=manifest.frameHeight||64,dh=entry.member.drawHeight||manifest.drawHeight||54,dw=entry.member.drawWidth||manifest.drawWidth||dh;
    if(entry.role!=='drummer'||action!=='perform')ellipse(ctx,p.x,p.y+1,Math.round(4*(jump?.shadow||1)),1,'#16182360');
    ctx.drawImage(sheet,column*fw,entry.row*fh,fw,fh,Math.round(p.x-(manifest.footX??32)*dw/fw),Math.round(p.y-(manifest.footY??60)*dh/fh-(jump?.lift||0)),dw,dh);
  }
  function bandFrame(entry,state){
    if(frozenPerformer(entry.actor,state))return{action:'idle',column:assets.bandModels.actions?.find(a=>a.id==='idle')?.frames?.[0]??8};
    return performingFrame(entry,assets.bandModels,seconds);
  }
  function resize(){const b=canvas.getBoundingClientRect();cssW=Math.max(1,b.width);cssH=Math.max(1,b.height);const portrait=cssW<760&&cssH>cssW;
    width=portrait?REF_W:Math.max(160,Math.min(2304,Math.round(REF_H*cssW/cssH)));height=portrait?Math.round(REF_W*cssH/cssW):REF_H;
    offsetX=(width-REF_W)/2;offsetY=portrait?Math.round(Math.min(125,cssH*.16)*height/cssH):0;
    // Keep the stage beside the right-hand card on narrower landscapes. The
    // wide reference composition retains its original camera and pixel scale.
    const console=canvas.parentElement?.querySelector('.live-console'),consoleBox=console?.getBoundingClientRect();
    if(!portrait&&cssW>=cssH&&consoleBox?.width>0){
      const right=consoleBox.left-b.left-16,currentRight=(932+offsetX)*cssW/width;
      if(currentRight>right){const roomWidth=904*cssH/REF_H,available=Math.max(200,right-24),fit=Math.min(1,available/roomWidth);
        height=Math.round(REF_H/fit);width=Math.round(height*cssW/cssH);offsetX=(right-Math.min(roomWidth,available)/2)*width/cssW-480;offsetY=(height-REF_H)/2;}
    }
    if(canvas.width!==width||canvas.height!==height){canvas.width=width;canvas.height=height;city=null;}ctx.imageSmoothingEnabled=false;}
  const observer=new ResizeObserver(resize);observer.observe(canvas);resize();
  canvas.style.imageRendering='pixelated';
  function background(state){const image=assets.backdrops?.[ownerVenue(state.owner).key]||assets.seoul;if(!city||citySource!==image){citySource=image;city=surface(width,height);const c=city.getContext('2d');c.imageSmoothingEnabled=false;c.fillStyle='#10121e';c.fillRect(0,0,width,height);if(ready(image)){const s=Math.max(width/image.width,height/image.height);c.drawImage(image,Math.round((width-image.width*s)/2),Math.round((height-image.height*s)/2),Math.round(image.width*s),Math.round(image.height*s));}}ctx.drawImage(city,0,0);}
  function boxMask(x0,x1,z0,z1,h){return hull([project(x0,z0),project(x1,z0),project(x1,z1),project(x0,z1),project(x0,z0,h),project(x1,z0,h),project(x1,z1,h),project(x0,z1,h)]);}
  function roomLayers(){
    if(plate===assets.venue&&layerMeta===assets.venueMeta)return;plate=assets.venue;layerMeta=assets.venueMeta;occluders=[];propLayers=[];
    for(const prop of assets.venueMeta?.props||[]){
      const image=assets.propImages?.[prop.asset];if(!ready(image))throw new Error('Missing venue furniture: '+prop.asset);
      const point=project(prop.x,prop.z),x=Math.round(point.x-prop.footX),y=Math.round(point.y-prop.footY);
      propLayers.push({image,x,y,width:prop.drawWidth,height:prop.drawHeight,depth:point.y+finite(prop.depthOffset)});
    }
    const supplied=assets.venueMeta?.occluders;
    const bar=Array.from({length:8},(_,i)=>{const x0=-9.95+i*1.09,x1=x0+1.1;return{points:boxMask(x0,x1,-2.35,-.88,1.48),worldX:(x0+x1)/2,worldZ:-2.35};});
    const boxes=(assets.venueMeta?.occluderBoxes||[]).map(({x0,x1,z0,z1,height,worldX=(x0+x1)/2,worldZ=z0})=>({points:boxMask(x0,x1,z0,z1,height),worldX,worldZ}));
    const rearExit=assets.venueMeta?.seatedActors?.['lounge-0']?[{points:[[185,140],[427,207],[427,299],[185,232]],actors:['lounge-0','lounge-1','lounge-2','lounge-3'],rearExit:true}]:[];
    const layers=[...(supplied||bar),...boxes,...rearExit];
    for(const layer of layers){if(!layer.points?.length)continue;const p=layer.points.map(p=>Array.isArray(p)?{x:p[0],y:p[1]}:p),xs=p.map(v=>v.x),ys=p.map(v=>v.y),left=Math.floor(Math.min(...xs)),top=Math.floor(Math.min(...ys)),w=Math.ceil(Math.max(...xs))-left+1,h=Math.ceil(Math.max(...ys))-top+1;if(w<=0||h<=0)continue;const mask=surface(w,h),c=mask.getContext('2d');c.imageSmoothingEnabled=false;c.translate(-left,-top);c.beginPath();p.forEach((v,i)=>i?c.lineTo(Math.round(v.x),Math.round(v.y)):c.moveTo(Math.round(v.x),Math.round(v.y)));c.closePath();c.clip();c.drawImage(plate,0,0,REF_W,REF_H);occluders.push({image:mask,x:left,y:top,points:p,stage:left+w>556&&left<867&&top+h>209&&top<373,actors:layer.actors||null,rearExit:!!layer.rearExit,depth:Number.isFinite(layer.depth)?layer.depth:project(layer.worldX||0,layer.worldZ||0).y});}
  }
  function atlasFrame(actor,role,action,frame,state,frameOverride=null,animationSeconds=seconds){
    const meta=assets.spriteMeta||{};
    let entry=meta.roles?.[role]??meta.roles?.[actor.role]??meta.roles?.guest??meta.roles?.default;
    if(entry==null)return null;if(typeof entry==='number')entry={row:entry};
    const style=pixelStyle(actor,entry,meta);
    let selected=entry.actions?.[action]||entry.actions?.[['dance','mosh','slam','support'].includes(action)?'dance':action==='walk'?'walk':action==='clean'?'clean':'idle']||entry;
    if(selected.preserveStyle&&selected.variantStyles&&selected.variantStyles[style]==null)selected=entry;
    const phase=['playing','closing'].includes(state.phase),fps=finite(selected.fps||entry.fps);
    if(fps>0)frame=phase?Math.floor(animationSeconds*fps+(action==='perform'?0:hash(actor.id)%4)):0;
    if(selected.progressFrames){
      const count=selected.frameCount||selected.frameColumns?.length||8;
      frame=Math.min(count-1,Math.floor(clamp(actor.actionProgress,0,1)*count));
    }
    if(frameOverride!=null)frame=frameOverride;
    const atlasFrames=selected.atlasFrames||entry.atlasFrames;
    const atlasKey=(atlasFrames?.length?atlasFrames[frame%atlasFrames.length]:null)||selected.atlas||entry.atlas||meta.defaultAtlas||'animation',definition=meta.atlases?.[atlasKey]||{},sheet=assets.atlases?.[atlasKey]||(!meta.atlases?assets.sprites:null);
    if(!ready(sheet))throw new Error('Missing pixel character atlas: '+atlasKey);
    const layout={...meta,...definition,...entry,...selected},fw=layout.frameWidth||64,fh=layout.frameHeight||64,columns=layout.columns||Math.max(1,Math.floor(sheet.width/fw));
    const direction=selected.direction||entry.direction||pixelFacing(actor,role,action,state);
    const rowFor=value=>value.variantStyles?.[style]??(value.variantRows?.length?value.variantRows[hash(actor.id)%value.variantRows.length]:value.row);
    const ownRow=selected!==entry?rowFor(selected):undefined;
    let row=selected.directionRows?.[direction]??entry.directionRows?.[direction]??selected.rows?.[direction]??ownRow??rowFor(entry)??0;
    row+=(selected.directionRowOffsets||entry.directionRowOffsets||definition.directionRowOffsets)?.[direction]||0;
    const directionColumns=selected.directionColumns||entry.directionColumns||definition.directionColumns,directionalColumn=directionColumns?.[direction];
    const frameColumns=selected.frameColumns||entry.frameColumns;
    let frames=selected.frames||entry.frames||columns,col=directionalColumn!=null?(Array.isArray(directionalColumn)?directionalColumn[frame%directionalColumn.length]:directionalColumn):frameColumns?.length?frameColumns[frame%frameColumns.length]:(selected.column||0)+frame%Math.max(1,Math.min(frames,columns));
    if(Array.isArray(selected.rects)){const r=selected.rects[frame%selected.rects.length];return{sheet,sx:r[0],sy:r[1],sw:r[2],sh:r[3],flip:false,meta:layout,entry:layout,direction,style,atlasKey};}
    if(row<0||col<0||col>=columns||(row+1)*fh>sheet.height)throw new Error('Pixel character atlas frame is out of bounds.');
    return{sheet,sx:col*fw,sy:row*fh,sw:fw,sh:layout.cropHeight||fh,flip:false,meta:layout,entry:layout,direction,style,atlasKey};
  }
  function festivalAtlasFrame(actor,state){
    const manifest=assets.festivalModels;if(!manifest)return null;
    const selected=pixelFestivalFrame(actor,state,seconds,manifest);if(!selected)return null;
    const definition=manifest.actions[selected.action],layout={...manifest,...manifest.atlases?.[selected.atlas],...definition};
    const sheet=assets.festivalAtlases?.[selected.atlas];if(!ready(sheet))throw new Error('Missing festival audience atlas: '+selected.atlas);
    const fw=manifest.frameWidth||64,fh=manifest.frameHeight||64;
    return{sheet,sx:selected.column*fw,sy:selected.row*fh,sw:fw,sh:fh,flip:false,meta:layout,entry:layout,style:selected.style,atlasKey:'festival-'+selected.atlas};
  }
  // Reassemble the existing four-direction walking artwork rather than
  // recolouring or replacing the audience. Alternate the two lower limbs and
  // the existing bent-arm portions while the head, torso and feet stay rooted
  // to the same projected actor position used by navigation and shadows.
  function drawSkankFrame(atlas,dx,dy,dw,dh,pose){
    const sw=atlas.sw,sh=atlas.sh,head=Math.round(sh*.34),hip=Math.round(sh*.66),middle=Math.round(sw/2),left=Math.round(sw*.375),right=Math.round(sw*.625);
    const piece=(x,y,w,h,offsetX=0,offsetY=0)=>{
      const x0=Math.round(dx+x*dw/sw)+offsetX,y0=Math.round(dy+y*dh/sh)+offsetY;
      const x1=Math.round(dx+(x+w)*dw/sw)+offsetX,y1=Math.round(dy+(y+h)*dh/sh)+offsetY;
      ctx.drawImage(atlas.sheet,atlas.sx+x,atlas.sy+y,w,h,x0,y0,x1-x0,y1-y0);
    };
    const lift=Math.round(8*pose.skankKnee),forward=Math.round(2*pose.skankKnee);
    piece(0,hip,middle,sh-hip,pose.skankStep<0?-forward:0,pose.skankStep<0?-lift:0);
    piece(middle,hip,sw-middle,sh-hip,pose.skankStep>0?forward:0,pose.skankStep>0?-lift:0);
    const arm=(x,w,pivot,rotation)=>{
      const x0=Math.round(dx+pivot*dw/sw),y0=Math.round(dy+head*dh/sh);
      ctx.save();ctx.translate(x0,y0);ctx.rotate(rotation);ctx.translate(-x0,-y0);piece(x,head,w,hip-head);ctx.restore();
    };
    arm(0,left+2,left,pose.skankArm*.16);
    arm(right-2,sw-right+2,right,-pose.skankArm*.16);
    piece(left,head,right-left,hip-head);
    piece(0,0,sw,head);
  }
  // Local, softly stepped pixel light: each actual phone stays beside its
  // owner and participates in the same room depth ordering as that person.
  function drawPhoneLight(actor,role,action,pose,size,state){
    const side=pixelFacing(actor,role,action,state).endsWith('R')?1:-1;
    const x=Math.round(side*(10+pose.phoneWave*2)),y=-Math.round(size*.72)+Math.round(pose.phoneWave);
    if(actor.role==='seated'){
      const sx=side*6,sy=-Math.round(size*.48),steps=Math.max(1,Math.abs(y+3-sy));
      ctx.fillStyle='#d2a98b';
      for(let i=0;i<=steps;i++)ctx.fillRect(Math.round(sx+(x-sx)*i/steps),Math.round(sy+(y+3-sy)*i/steps),2,2);
    }
    ctx.fillStyle='#eaf8ff38';ctx.fillRect(x-5,y-5,11,11);
    ctx.fillStyle='#edf9ff68';ctx.fillRect(x-3,y-3,7,7);
    ctx.fillStyle='#283540';ctx.fillRect(x-1,y-2,3,5);
    ctx.fillStyle='#eaf9ff';ctx.fillRect(x,y-1,1,3);
    ctx.fillStyle='#fffde9';ctx.fillRect(x-1,y-4,3,2);ctx.fillRect(x-4,y-2,2,1);ctx.fillRect(x+3,y-2,2,1);ctx.fillRect(x,y-6,1,2);
  }
  function drawActor(a,state){
    if(a.role==='inspector'){drawInspector(a);return;}
    let role=String(a.role||'guest').toLowerCase();if(role==='band')role=String(a.id||'singer');if(role==='seated'&&a.id?.startsWith('bar-seat-'))role='barseated';
    if(a.drunkIncident==='fight'||a.drunkIncident==='exit')role='guest';
    const pose=pixelAudiencePose(a,state,seconds);
    const frozen=frozenPerformer(a,state);
    let action=pose.action;if(pose.kind==='cheer'&&action==='highfive'&&!assets.spriteMeta.roles?.[role]?.actions?.highfive)action='idle';if(action==='sweep')action='clean';else if(a.moving&&!frozen&&!['mosh','slam','surf','support'].includes(action))action='walk';
    const bar=assets.venueMeta?.barRegion;
    if(role==='bartender'&&!a.moving&&bar&&a.x<bar.xMax&&a.z>=bar.zMin&&a.z<=bar.zMax&&['idle','serve'].includes(action))action='idle';
    const active=['playing','closing'].includes(state.phase)&&!frozen;
    const band=role==='singer'||role==='guitarist'||role==='bassist'||role==='drummer';
    const performance=bandPerformanceProfile(state),bandTime=performance.legacy?seconds:seconds*performance.speed+hash(a.id)%17*.23;
    const jump=assets.spriteMeta.roles?.[role]?.actions?.jump?pixelBandJump(role,action,bandTime,active,performance.legacy?1:performance.jumpIntensity):null;
    if(jump)action='jump';
    const frame=active?Math.floor(seconds*(action==='walk'?7:action==='clean'?6:band?8:5)+hash(a.id)%4):0;
    const p=actorPoint(a,finite(a.y)),ground=actorPoint(a,role==='surfer'||a.festivalLead?0:finite(a.y)),surf=(action==='surf'||role==='surfer')&&finite(a.y)>.1;
    const liftedSurfer=a.festivalLead&&a.festivalLiftReady&&finite(a.y)>.05;
    if(!seatFor(a)?.hideShadow&&(role!=='drummer'||action!=='perform'))ellipse(ctx,ground.x,ground.y+1,liftedSurfer?12:Math.round(4*(jump?.shadow||1)),liftedSurfer?2:1,liftedSurfer||jump?'#16182348':'#16182360');
    const atlas=festivalAtlasFrame(a,state)||atlasFrame(a,a.festivalLead&&action==='surf'?'surfer':role,action,frame,state,frozen?0:jump?.frame??(action==='highfive'?pose.frameOverride:pose.festivalAction==='skank'?1:null),band&&action==='perform'?bandTime:pose.animationSeconds),meta=atlas?.meta||{},entry=atlas?.entry||{},size=entry.drawHeight||meta.drawHeight||40;
    const varied=!!pose.kind,bob=!band&&active?(varied?pose.bob:['dance','mosh','slam'].includes(action)?Math.round(Math.sin(seconds*5+hash(a.id)%17)*.6):0):0;
    ctx.save();ctx.translate(Math.round(p.x+(active?pose.sway:0)),Math.round(p.y+bob-(jump?.lift||0)-(active?pose.lift||0:0)));if(active&&pose.lean)ctx.rotate(pose.lean);
    if(surf&&!entry.horizontal){ctx.rotate(-Math.PI/2);ctx.translate(0,-7);}
    if(atlas){const scale=size/atlas.sh,dw=entry.drawWidth||meta.drawWidth||atlas.sw*scale,dh=size,footX=entry.footX??meta.footX??atlas.sw/2,footY=entry.footY??meta.footY??atlas.sh,dx=Math.round(-footX*dw/atlas.sw),dy=Math.round(-footY*dh/atlas.sh);if(atlas.flip)ctx.scale(-1,1);if(active&&pose.festivalAction==='skank')drawSkankFrame(atlas,dx,dy,Math.round(dw),Math.round(dh),pose);else ctx.drawImage(atlas.sheet,atlas.sx,atlas.sy,atlas.sw,atlas.sh,dx,dy,Math.round(dw),Math.round(dh));if(active&&pose.phoneLight)drawPhoneLight(a,role,action,pose,size,state);}
    else throw new Error('No pixel character atlas entry for '+role);
    ctx.restore();
    if(active&&pose.cheer){ctx.fillStyle='#ffe9ac';ctx.fillRect(Math.round(p.x-9),Math.round(p.y-size+6),1,3);ctx.fillRect(Math.round(p.x+8),Math.round(p.y-size+2),1,3);}
    if(active&&pose.kind==='fight'&&pose.strike){
      const side=pixelFacing(a,role,action,state).endsWith('R')?1:-1,x=Math.round(p.x+side*11),y=Math.round(p.y-size*.51);
      ctx.fillStyle='#edb778';ctx.fillRect(x,y,2,1);ctx.fillRect(x+side*3,y-3,1,2);
      ctx.fillStyle='#f4dfbd';ctx.fillRect(x+side*3,y+2,1,2);
    }
    if(action==='clean'){const swing=Math.sin(seconds*7+hash(a.id)%5)*2;polygon(ctx,[{x:p.x+4,y:p.y-16},{x:p.x+5,y:p.y-16},{x:p.x+10+swing,y:p.y+2},{x:p.x+9+swing,y:p.y+2}],'#b69061');ctx.fillStyle='#c7b27e';ctx.fillRect(Math.round(p.x+6+swing),Math.round(p.y+2),7,2);ctx.fillStyle='#857150';for(let i=0;i<3;i++)ctx.fillRect(Math.round(p.x+6+swing+i*2),Math.round(p.y+4),1,2);}
    if((a.id==='assisted-guest'||role==='assisted')&&state.floor?.guestActive){ctx.fillStyle='#f4c86e';ctx.fillRect(Math.round(p.x),Math.round(p.y-48),2,5);ctx.fillRect(Math.round(p.x),Math.round(p.y-41),2,2);}
  }
  function inspectorFrame(actor){
    const meta=assets.inspectionModels||{},direction=pixelDirection(Math.sin(finite(actor.facing)),Math.cos(finite(actor.facing)));
    const row=meta.models?.find(m=>m.id===actor.id)?.row??clamp(actor.index,0,2),frames=meta.walkFrames||[1,2];
    return{direction,row,column:(meta.directionColumns?.[direction]||0)+(actor.moving?frames[Math.floor(seconds*(meta.walkFps||6))%frames.length]:meta.idleFrame||0),...actorPoint(actor)};
  }
  function drawInspector(actor){
    const sheet=assets.inspectors,meta=assets.inspectionModels||{};if(!ready(sheet))throw new Error('Missing inspection team artwork.');
    const p=inspectorFrame(actor),fw=meta.frameWidth||64,fh=meta.frameHeight||64,dw=meta.drawWidth||54,dh=meta.drawHeight||54;
    ellipse(ctx,p.x,p.y+1,4,1,'#16182360');
    ctx.drawImage(sheet,p.column*fw,p.row*fh,fw,fh,Math.round(p.x-(meta.footX??32)*dw/fw),Math.round(p.y-(meta.footY??60)*dh/fh),dw,dh);
  }
  function drawMenuCategoryIcon(item,x,y){
    const rect=(color,dx,dy,w,h)=>{ctx.fillStyle=color;ctx.fillRect(x+dx,y+dy,w,h);};
    if(item.isFood){
      rect('#293548',3,16,18,3);rect('#293548',6,19,12,2);
      rect('#293548',6,11,12,5);rect('#293548',9,7,6,4);rect('#293548',11,5,2,2);
      rect('#e7a55c',8,12,8,3);rect('#ffe5a6',10,8,4,4);
    }else if(['draft','bottle','zeroBeer'].includes(item.group)){
      rect('#293548',5,5,12,15);rect('#293548',17,9,4,8);rect('#fff5d8',17,11,2,4);
      rect('#d28c39',7,9,8,9);rect('#ffe5a6',7,6,8,4);
    }else{
      rect('#293548',4,5,16,3);rect('#293548',6,8,12,3);rect('#293548',8,11,8,3);
      rect('#293548',11,14,2,6);rect('#293548',8,20,8,2);
      rect('#b75070',8,8,8,2);rect('#b75070',10,10,4,2);
    }
  }
  function orderBubbles(state){
    const definition=assets.spriteMeta?.atlases?.orders,columns=definition?.columns||6;
    const orders=pixelOrderBubblePlacements(state,a=>actorPoint(a,finite(a.y)),assets.spriteMeta,columns);if(!orders.length)return;
    const sheet=assets.atlases?.orders;
    if(!ready(sheet))throw new Error('The order icon atlas must finish loading before orders are shown.');
    const fw=definition?.frameWidth||32,fh=definition?.frameHeight||32;
    const menu=menuForOwner(state.owner);
    // Keep the first order readable when head-anchored cards overlap.
    for(const b of orders.slice().reverse()){
      if(b.x+offsetX+b.width<0||b.x+offsetX>width||b.y+b.height+b.tail<0||b.y>height)continue;
      const x=b.x,y=b.y,w=b.width,h=b.height,cx=b.neckX;
      ctx.fillStyle='#182333';ctx.fillRect(x+2,y,w-4,h);ctx.fillRect(x,y+2,w,h-4);
      polygon(ctx,[{x:cx-4,y:y+h-1},{x:cx+4,y:y+h-1},{x:b.tipX,y:b.tipY}],'#182333');
      ctx.fillStyle='#fff5d8';ctx.fillRect(x+2,y+1,w-4,h-2);ctx.fillRect(x+1,y+2,w-2,h-4);
      polygon(ctx,[{x:cx-2,y:y+h-1},{x:cx+2,y:y+h-1},{x:b.tipX,y:b.tipY-2}],'#fff5d8');
      if(b.item<columns)ctx.drawImage(sheet,b.item*fw,0,fw,fh,x+4,y+2,24,24);
      else drawMenuCategoryIcon(menu[b.item],x+4,y+2);

    }
  }
  function paper(state,dt){
    const f=state.floor||{},session=state.journey?.session||0;
    if(night!==session||observedFloor!==state.floor){night=session;observedFloor=state.floor;seenEffects.clear();particles.length=0;}
    for(const event of state.effects||[]){const id=[event.serial,event.kind,event.time].join(':');if(seenEffects.has(id))continue;seenEffects.add(id);if(finite(state.elapsed)-finite(event.time)>1.2)continue;particles.push(...pixelPaperBurst(event));if(particles.length>160)particles.splice(0,particles.length-160);}
    for(const mark of pixelGroundPaper(f)){
      const p=project(mark.x,mark.z,.03),x=Math.round(p.x),y=Math.round(p.y);
      ctx.fillStyle='#10172399';ctx.fillRect(x-1,y+2,mark.kind?11:6,1);ctx.fillStyle=mark.color;
      if(mark.kind){ctx.fillRect(x,y,4,2);ctx.fillRect(x+3,y+1,4,2);ctx.fillRect(x+6,y,3,2);ctx.fillStyle='#fff7e8';ctx.fillRect(x+1,y,3,1);}
      else ctx.fillRect(x,y,mark.index%3===1?2:4,mark.index%3===1?4:2);
    }
    return()=>{for(let i=particles.length-1;i>=0;i--){
      const a=particles[i];a.age+=dt;a.vy-=dt*(a.kind?.8:1.3);a.x+=a.vx*dt;a.z+=a.vz*dt;a.y+=a.vy*dt;
      if(a.y<.03||a.age>8||a.x>10.3||a.z<-7){particles.splice(i,1);continue;}
      const p=project(a.x,a.z,a.y),x=Math.round(p.x+Math.sin(a.phase+a.age*6)*1.5),y=Math.round(p.y);
      if(a.kind){
        for(let segment=0;segment<5+a.index%4;segment++){
          const sx=Math.round(x+Math.sin(a.phase+a.age*5+segment*.85)*2.5);
          ctx.fillStyle=segment%3===2?'#d6cee0':a.color;ctx.fillRect(sx,y+segment*2,3,2);
        }
      }else{
        const turn=Math.floor(a.age*8+a.phase)%3;ctx.fillStyle=a.color;ctx.fillRect(x,y,turn===1?2:turn===2?4:3,turn===1?4:turn===2?2:3);
      }
    }};
  }
  function stageLights(state){
    if(!pixelStageLightingActive(state))return;
    // Authored venue / artist lighting style. These speeds do not imply track
    // BPM. The shared presentation clock freezes all light movement on pause.
    const profile=pixelStageLightingProfile(state),lightTime=seconds*profile.speed;
    const rig=[
      {source:[2.55,5.8,4.6],target:[1.8,-1.2,.12],color:'#48d8ff'},
      {source:[3.85,5.8,4.74],target:[6.5,3,.65],color:'#ff50c9'},
      {source:[7.25,5.8,5],target:[3.9,-1.8,.12],color:'#60dfff'},
      {source:[8.6,5.8,5.1],target:[8.2,2.8,.65],color:'#ffc16b'}
    ];
    const smooth=n=>{n=clamp(n,0,1);return n*n*(3-2*n);};
    const ring=(x,z,y,radius)=>Array.from({length:20},(_,i)=>{const a=i*Math.PI/10;return project(x+Math.cos(a)*radius,z+Math.sin(a)*radius*.8,y);});
    ctx.save();ctx.globalCompositeOperation='screen';
    for(let i=0;i<rig.length;i++){
      const light={...rig[i],color:profile.colors[i]},phase=(lightTime+.12+(i%2)*.5)%1;
      const pulse=smooth(phase/.08)*(1-smooth((phase-.30)/.18));
      const sway=(Math.sin(lightTime*.72+i*1.7)*.48+Math.sin(lightTime*Math.PI*4+i*Math.PI)*.08)*Math.min(1.15,profile.energy);
      const [tx,tz,ty]=light.target,x=tx+sway,z=tz+Math.cos(lightTime*.58+i*1.4)*.24;
      const calibrated=assets.venueMeta?.lightSources?.[i],source=calibrated?{x:calibrated[0],y:calibrated[1]}:project(...light.source),end=project(x,z,ty),radius=i%2?1.18:1.48;
      // Several projected rings make a feathered pool without blurring the art.
      for(let layer=0;layer<4;layer++){
        ctx.globalAlpha=(.05+layer*.018+pulse*.032)*profile.intensity;
        polygon(ctx,ring(x,z,ty+.006,radius*(1.25-layer*.23)),light.color);
      }
      const haze=ctx.createLinearGradient(source.x,source.y,end.x,end.y);
      haze.addColorStop(0,light.color+'d9');haze.addColorStop(.3,light.color+'b3');haze.addColorStop(1,light.color+'73');
      for(let layer=0;layer<3;layer++){
        ctx.globalAlpha=([.11,.065,.04][layer]+[.07,.045,.025][layer]*pulse)*profile.intensity;
        polygon(ctx,hull([source,...ring(x,z,ty,radius*[1,.7,.38][layer])]),haze);
      }
      // Small pixel-stepped source flares stay attached to the visible lamps.
      ctx.globalAlpha=(.08+pulse*.08)*profile.intensity;ellipse(ctx,source.x,source.y,12,8,light.color);
      ctx.globalAlpha=(.2+pulse*.15)*profile.intensity;ellipse(ctx,source.x,source.y,6,4,light.color);
      ctx.globalAlpha=Math.min(1,(.7+pulse*.2)*profile.intensity);ellipse(ctx,source.x,source.y,2,2,'#fff6df');
      ctx.globalAlpha=(.22+pulse*.1)*profile.intensity;ctx.fillStyle=light.color;
      ctx.fillRect(Math.round(source.x-9),Math.round(source.y),19,1);
      ctx.fillRect(Math.round(source.x),Math.round(source.y-7),1,15);
    }
    ctx.restore();
  }
  function albumCover(target,state){
    const image=assets.albumCovers?.[state.playlist];if(!ready(image))return;
    const quad=assets.venueMeta?.ledQuad||PIXEL_LED_QUAD;
    const iw=image.naturalWidth||image.width,ih=image.naturalHeight||image.height,fit=pixelLedCoverFit(iw,ih,quad);if(!fit)return;
    target.save();target.beginPath();quad.forEach(([x,y],i)=>i?target.lineTo(x,y):target.moveTo(x,y));target.closePath();target.clip();
    target.transform(...fit.transform);target.fillStyle='#080b13';target.fillRect(0,0,fit.planeWidth,fit.planeHeight);
    target.imageSmoothingEnabled=true;
    target.drawImage(image,0,0,iw,ih,fit.x,fit.y,fit.width,fit.height);target.restore();
    // The screen sits behind the hanging lamp housings. Restore only those
    // original foreground silhouettes, rather than painting over the fixtures.
    target.save();target.beginPath();
    const fixtureScale=assets.venueMeta?.ledFixtureScale>0?assets.venueMeta.ledFixtureScale:1;
    for(const [x,y]of assets.venueMeta?.ledLamps||PIXEL_LED_LAMPS){
      const shape=[[-5,-12],[4,-11],[10,-4],[10,4],[4,10],[-5,9],[-11,3],[-10,-5]];
      shape.forEach(([dx,dy],i)=>i?target.lineTo(x+dx*fixtureScale,y+dy*fixtureScale):target.moveTo(x+dx*fixtureScale,y+dy*fixtureScale));target.closePath();
    }
    target.clip();target.drawImage(assets.venue,0,0,REF_W,REF_H);target.restore();
  }
  function dimStageDeck(target){
    polygon(target,(assets.venueMeta?.stageDeck||PIXEL_STAGE_DECK).map(([x,y])=>({x,y})),'rgba(5,9,18,0.72)');
  }
  function endedStage(target,state){
    if(!pixelStageLightingEnded(state))return;
    dimStageDeck(target);
    // Suppress the glow and bright glass baked into the authored lamp pixels.
    // Their housings remain visible; the separate LED and cyan rim stay lit.
    for(const [x,y]of assets.venueMeta?.stageLamps||PIXEL_STAGE_LAMPS){
      ellipse(target,x,y,12,11,'rgba(7,11,19,0.45)');
      ellipse(target,x,y,10,9,'rgba(7,11,19,0.70)');
      ellipse(target,x,y,7,6,'#17202c');ellipse(target,x,y,4,3,'#0b111a');
      target.fillStyle='#3c4652';target.fillRect(x-2,y-2,2,1);
    }
  }
  function drawLayer(layer,state){
    ctx.drawImage(layer.image,layer.x,layer.y);
    if(!layer.stage||!pixelStageLightingEnded(state))return;
    ctx.save();ctx.beginPath();layer.points.forEach((p,i)=>i?ctx.lineTo(p.x,p.y):ctx.moveTo(p.x,p.y));ctx.closePath();ctx.clip();dimStageDeck(ctx);ctx.restore();
  }
  function render(timestamp,state=getState()){
    if(destroyed)return;state=state||{};const now=finite(timestamp,performance.now()),dt=last==null?0:clamp((now-last)/1000,0,.1);last=now;const running=['playing','closing'].includes(state.phase)&&!state.paused;if(running)seconds+=dt;
    selectVenue(state);background(state);roomLayers();ctx.save();ctx.translate(Math.round(offsetX),offsetY);ctx.imageSmoothingEnabled=false;ctx.drawImage(assets.venue,0,0,REF_W,REF_H);albumCover(ctx,state);endedStage(ctx,state);stageLights(state);const airborne=paper(state,running?dt:0);
    const drawables=occluders.filter(layer=>!layer.actors?.length).map(layer=>({depth:layer.depth,draw:()=>drawLayer(layer,state)}));
    for(const prop of propLayers)drawables.push({depth:prop.depth,draw:()=>drawProp(ctx,prop)});
    for(const feature of pixelVenueFeatures(state))drawables.push({depth:feature.depth,draw:()=>drawFeature(ctx,feature)});
    const models=modelRoster(state);
    for(const actor of state.actors||[]){if(actor.visible===false||models.length&&actor.role==='band')continue;const seat=seatFor(actor);let depth=seat?.depth??actorPoint(actor).y+(actor.action==='surf'?2:0);if(actor.id==='drummer'&&actor.action==='perform'&&assets.venueMeta?.drumForegroundDepth>0)depth=Math.min(depth,assets.venueMeta.drumForegroundDepth-1);drawables.push({depth,draw:()=>{const rearClip=behindRearShelf(actor,state);if(rearClip){ctx.save();ctx.beginPath();ctx.rect(427,0,REF_W-427,REF_H);ctx.clip();}drawActor(actor,state);if(rearClip)ctx.restore();for(const layer of occluders)if(layer.actors?.includes(actor.id)&&((seat&&!layer.rearExit)||(state.phase==='closing'&&(layer.rearExit?actor._rearExitActive:actor._seatExitOrigin&&Math.hypot(actor.x-actor._seatExitOrigin.x,actor.z-actor._seatExitOrigin.z)<1))))drawLayer(layer,state);}});}
    for(const model of models){let depth=modelPoint(model).y;if(model.role==='drummer'&&model.actor.action==='perform'&&assets.venueMeta?.drumForegroundDepth>0)depth=Math.min(depth,assets.venueMeta.drumForegroundDepth-1);drawables.push({depth,draw:()=>drawBandModel(model,state)});}
    drawables.sort((a,b)=>a.depth-b.depth);for(const item of drawables)item.draw();airborne();orderBubbles(state);ctx.restore();
  }
  function worldToScreen(x,z,y=0){const p=project(x,z,y);return{x:(p.x+offsetX)*cssW/width,y:(p.y+offsetY)*cssH/height};}
  function getVenueBounds(){const left=Math.max(0,(28+offsetX)*cssW/width),right=Math.min(cssW,(932+offsetX)*cssW/width),top=offsetY*cssH/height,bottom=(REF_H+offsetY)*cssH/height;return{left,top,right,bottom,width:right-left,height:bottom-top};}
  function renderOverview(target,state=getState()){
    selectVenue(state);roomLayers();
    const w=target.canvas.width,h=target.canvas.height,scale=Math.min((w-8)/REF_W,(h-8)/REF_H),left=(w-REF_W*scale)/2,top=(h-REF_H*scale)/2;
    target.imageSmoothingEnabled=false;target.fillStyle='#1e2632';target.fillRect(0,0,w,h);
    target.drawImage(assets.venue,Math.round(left),Math.round(top),Math.round(REF_W*scale),Math.round(REF_H*scale));
    target.save();target.translate(left,top);target.scale(scale,scale);for(const prop of propLayers)drawProp(target,prop);for(const feature of pixelVenueFeatures(state))drawFeature(target,feature);albumCover(target,state);endedStage(target,state);target.restore();
    const models=modelRoster(state);
    for(const actor of state.actors||[]){
      if(actor.visible===false||models.length&&actor.role==='band')continue;
      const p=actorPoint(actor,finite(actor.y)),employee=['bartender','server'].includes(actor.role)||actor.id?.startsWith('hired-'),size=employee?4:3;
      const x=Math.round(left+p.x*scale-size/2),y=Math.round(top+p.y*scale-size/2);
      target.fillStyle='#111721';target.fillRect(x-1,y-1,size+2,size+2);
      target.fillStyle=actor.role==='inspector'?'#daff54':actor.id==='assisted-guest'?'#ffd36f':employee?'#6ee59a':actor.role==='band'?'#65dbef':'#f5e4c8';
      target.fillRect(x,y,size,size);
    }
    for(const model of models){const p=modelPoint(model),x=Math.round(left+p.x*scale-1.5),y=Math.round(top+p.y*scale-1.5);target.fillStyle='#111721';target.fillRect(x-1,y-1,5,5);target.fillStyle='#65dbef';target.fillRect(x,y,3,3);}
  }
  function drawProp(target,prop){target.drawImage(prop.image,prop.x,prop.y,prop.width,prop.height);}
  function drawFeature(target,feature){
    const meta=assets.venueFeatureModels,sheet=assets.venueFeatures,item=meta?.items?.[feature.id];
    if(!item||!ready(sheet))return;
    const fw=meta.frameWidth||96,fh=meta.frameHeight||96,size=feature.size;
    const cell=item.variants?.[feature.venue]||item,style=pixelVenueFeatureStyle(feature);
    if(!style){target.drawImage(sheet,cell.column*fw,cell.row*fh,fw,fh,Math.round(feature.x-(meta.footX??48)*size/fw),Math.round(feature.y-(meta.footY??88)*size/fh),size,size);return;}
    target.save();target.translate(feature.x,feature.y);target.transform(1,style.shearY,0,1,0,0);
    if(feature.type==='event'||feature.index!==0)ellipse(target,0,-1,Math.round(size*.24),Math.max(1,Math.round(size*.035)),'rgba(9,8,13,0.22)');
    target.filter=style.filter;
    target.drawImage(sheet,cell.column*fw,cell.row*fh,fw,fh,Math.round(-(meta.footX??48)*size/fw),Math.round(-(meta.footY??88)*size/fh),size,size);
    target.restore();
  }
  function destroy(){destroyed=true;observer.disconnect();particles.length=0;occluders=[];propLayers=[];city=null;}
  function getBandPresentation(state=getState()){
    selectVenue(state);
    return modelRoster(state).map(entry=>({id:entry.id,bandId:entry.bandId,role:entry.role,anchor:entry.member.anchor,row:entry.row,extra:entry.extra,file:entry.band.file,...bandFrame(entry,state),...modelPoint(entry)}));
  }
  function getAudiencePresentation(state=getState()){
    selectVenue(state);
    return(state.actors||[]).filter(actor=>actor.visible!==false&&['guest','seated','support','surfer'].includes(actor.role)).map(actor=>{
      const pose=pixelAudiencePose(actor,state,seconds),selected=assets.festivalModels?pixelFestivalFrame(actor,state,seconds,assets.festivalModels):null;
      return{id:actor.id,role:actor.role,seated:!!seatFor(actor),moving:!!actor.moving,action:pose.action,festivalAction:pose.festivalAction||null,
        atlas:selected?.atlas||null,row:selected?.row??null,column:selected?.column??null,style:selected?.style||null,lift:pose.lift||0,
        ...(pose.festivalAction==='skank'?{skankStep:pose.skankStep,skankKnee:pose.skankKnee,skankArm:pose.skankArm}:{}),
        ...(pose.festivalAction==='flash'?{phoneLight:!!pose.phoneLight,phoneWave:pose.phoneWave}:{}),...actorPoint(actor,finite(actor.y))};
    });
  }
  function getInspectorPresentation(state=getState()){selectVenue(state);return(state.actors||[]).filter(a=>a.role==='inspector'&&a.visible!==false).map(a=>({id:a.id,moving:!!a.moving,...inspectorFrame(a)}));}
  return{render,resize,destroy,worldToScreen,project,getVenueBounds,renderOverview,getBandPresentation,getAudiencePresentation,getInspectorPresentation,getVenueFeaturePresentation:state=>pixelVenueFeatures(state||getState())};
}
