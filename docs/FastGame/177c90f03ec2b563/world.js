/* Presentation of the real room and real game actors. Render timestamps are ms.
 * The HUD-free venue plate is static art; live actor behavior and effects remain
 * in core.js. Employee movement, guest exit, paper debt and rewards are unchanged. */
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
const rng=seed=>{let n=seed>>>0;return()=>((n=(Math.imul(n,1664525)+1013904223)>>>0)/4294967296);};
const surface=(w,h)=>{const c=document.createElement('canvas');c.width=w;c.height=h;return c;};
const polygon=(c,points,color)=>{c.fillStyle=color;c.beginPath();points.forEach((p,i)=>i?c.lineTo(Math.round(p.x),Math.round(p.y)):c.moveTo(Math.round(p.x),Math.round(p.y)));c.closePath();c.fill();};
function ellipse(c,x,y,rx,ry,color){c.fillStyle=color;for(let row=-ry;row<=ry;row++){const w=Math.round(rx*Math.sqrt(Math.max(0,1-row*row/(ry*ry||1))));c.fillRect(Math.round(x-w),Math.round(y+row),w*2+1,1);}}
function hull(points){const p=points.slice().sort((a,b)=>a.x-b.x||a.y-b.y),turn=(a,b,c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);let low=[],high=[];for(const a of p){while(low.length>1&&turn(low.at(-2),low.at(-1),a)<=0)low.pop();low.push(a);}for(const a of p.reverse()){while(high.length>1&&turn(high.at(-2),high.at(-1),a)<=0)high.pop();high.push(a);}return low.slice(0,-1).concat(high.slice(0,-1));}

// The four views are actual drawings. Mirroring a front view cannot create a back.
export function pixelDirection(dx,dz){
  const right=RIGHT[0]*dx+RIGHT[2]*dz,back=UP[0]*dx+UP[2]*dz;
  return(back>=0?'B':'F')+(right>=0?'R':'L');
}
export function pixelFacing(actor,role,action,state={}){
  if(role==='barseated')return'BR';
  if(role==='seated')return Number(String(actor.id).split('-').at(-1))<4?'FL':'BR';
  const target=(x,z)=>pixelDirection(x-finite(actor.x),z-finite(actor.z));
  if(['highfive','hug','aftershow-wait'].includes(action)&&actor._partner){
    const partner=state.actors?.find(a=>a.id===actor._partner);if(partner)return target(partner.x,partner.z);
  }
  if(['singer','guitarist','bassist','drummer'].includes(role)&&['perform','idle'].includes(action)&&!actor.afterShowPhase)return target(5.75,-1.25);
  if(action==='mosh')return pixelDirection(-(finite(actor.z)+1.25),finite(actor.x)-5.75);
  if(action==='slam')return target(5.75,-1.25);
  if(action==='support'){
    const surfer=state.actors?.find(a=>a.id==='crowd-surfer');
    return target(surfer?.x??6,surfer?.z??-4.25);
  }
  if(action==='queue'){
    const employee=state.actors?.find(a=>a.id==='server')||state.actors?.find(a=>a.id==='bartender');
    return target(employee?.x??-4.2,employee?.z??-.565);
  }
  const employee=['bartender','server'].includes(role)||actor.id?.startsWith('hired-');
  if(employee&&!actor.moving&&['idle','serve'].includes(action)&&finite(actor.x)<-1.35&&finite(actor.z)>-1.5&&finite(actor.z)<.5)return'FL';
  if(!actor.moving&&role==='guest'&&action==='idle')return target(5.5,2.7);
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
export function pixelBandJump(role,action,seconds,active=true){
  if(!active||action!=='perform'||!['guitarist','bassist'].includes(role))return null;
  const phase=(Math.max(0,seconds)+(role==='bassist'?2.6:0))%10.4;
  if(phase<8.9||phase>=9.9)return null;
  const progress=phase-8.9,air=Math.max(0,Math.sin(clamp((progress-.125)/.75,0,1)*Math.PI));
  return{frame:Math.min(7,Math.floor(progress*8)),lift:Math.round(air*7),shadow:1-air*.32};
}

export function createWorld(canvas,{assets={},getState=()=>({})}={}){
  const ctx=canvas.getContext('2d',{alpha:false});if(!ctx)throw new Error('A 2D game canvas is required.');
  if(!ready(assets.venue))throw new Error('The detailed room artwork must finish loading before the game starts.');
  if(!assets.spriteMeta?.roles?.guest)throw new Error('The pixel character atlas metadata is missing.');
  if(!assets.spriteMeta.atlases&&!ready(assets.sprites))throw new Error('The pixel character artwork must finish loading before the game starts.');
  for(const key of Object.keys(assets.spriteMeta.atlases||{}))if(!ready(assets.atlases?.[key]))throw new Error('The '+key+' character atlas must finish loading before the game starts.');
  const particles=[],seenEffects=new Set();
  let width=960,height=558,offsetX=0,cssW=960,cssH=558,last=null,seconds=0,night=null,city=null,occluders=[],plate=null,destroyed=false;
  const project=(x,z,y=0)=>{
    const calibration=assets.venueMeta?.projection;
    if(calibration){const {origin,xAxis,zAxis,yAxis}=calibration;return{x:origin[0]+x*xAxis[0]+z*zAxis[0]+y*yAxis[0],y:origin[1]+x*xAxis[1]+z*zAxis[1]+y*yAxis[1]};}
    const position=[x-EYE[0],y-EYE[1],z-EYE[2]],scale=REF_H/(2*ORTHO);
    return{x:REF_W/2+dot(position,RIGHT)*scale,y:REF_H/2-dot(position,UP)*scale};
  };
  const actorOffset=a=>{
    const offset=assets.venueMeta?.actorOffsets?.[a.id]||[0,0],region=assets.venueMeta?.barRegion;
    const employee=['bartender','server'].includes(a.role)||a.id?.startsWith('hired-');
    const weight=employee&&region&&a.z>=region.zMin&&a.z<=region.zMax?clamp((region.xMax-a.x)/region.xFade,0,1):0;
    const stageWeight=a.role==='band'&&Number.isFinite(a.stageLevel)?clamp(a.stageLevel,0,1):1;
    const stageFoot=a.role==='band'&&assets.venueMeta?.bandStageFeet?.[a.id];
    if(stageFoot){const home=project(finite(a.homeX,a.x),finite(a.homeZ,a.z),finite(a.homeY));return{x:(stageFoot[0]-home.x)*stageWeight,y:(stageFoot[1]-home.y)*stageWeight};}
    const elevation=finite(assets.venueMeta?.stageElevationPixels)*(a.role==='band'?stageWeight:0);
    return{x:offset[0]*stageWeight+(region?.offset?.[0]||0)*weight,y:offset[1]*stageWeight+(region?.offset?.[1]||0)*weight-elevation};
  };
  const actorPoint=(a,y=0)=>{const p=project(finite(a.x),finite(a.z),y),o=actorOffset(a);return{x:p.x+o.x,y:p.y+o.y};};
  function resize(){const b=canvas.getBoundingClientRect();cssW=Math.max(1,b.width);cssH=Math.max(1,b.height);width=Math.max(160,Math.min(2304,Math.round(REF_H*cssW/cssH)));height=REF_H;offsetX=(width-REF_W)/2;if(canvas.width!==width||canvas.height!==height){canvas.width=width;canvas.height=height;city=null;}ctx.imageSmoothingEnabled=false;}
  const observer=new ResizeObserver(resize);observer.observe(canvas);resize();
  canvas.style.imageRendering='pixelated';
  function background(){if(!city){city=surface(width,height);const c=city.getContext('2d');c.imageSmoothingEnabled=false;c.fillStyle='#10121e';c.fillRect(0,0,width,height);if(ready(assets.seoul)){const s=Math.max(width/assets.seoul.width,height/assets.seoul.height);c.drawImage(assets.seoul,Math.round((width-assets.seoul.width*s)/2),Math.round((height-assets.seoul.height*s)/2),Math.round(assets.seoul.width*s),Math.round(assets.seoul.height*s));}}ctx.drawImage(city,0,0);}
  function boxMask(x0,x1,z0,z1,h){return hull([project(x0,z0),project(x1,z0),project(x1,z1),project(x0,z1),project(x0,z0,h),project(x1,z0,h),project(x1,z1,h),project(x0,z1,h)]);}
  function roomLayers(){
    if(plate===assets.venue)return;plate=assets.venue;occluders=[];
    const supplied=assets.venueMeta?.occluders;
    const bar=Array.from({length:8},(_,i)=>{const x0=-9.95+i*1.09,x1=x0+1.1;return{points:boxMask(x0,x1,-2.35,-.88,1.48),worldX:(x0+x1)/2,worldZ:-2.35};});
    const boxes=(assets.venueMeta?.occluderBoxes||[]).map(({x0,x1,z0,z1,height,worldX=(x0+x1)/2,worldZ=z0})=>({points:boxMask(x0,x1,z0,z1,height),worldX,worldZ}));
    const layers=[...(supplied||bar),...boxes];
    for(const layer of layers){if(!layer.points?.length)continue;const p=layer.points.map(p=>Array.isArray(p)?{x:p[0],y:p[1]}:p),xs=p.map(v=>v.x),ys=p.map(v=>v.y),left=Math.floor(Math.min(...xs)),top=Math.floor(Math.min(...ys)),w=Math.ceil(Math.max(...xs))-left+1,h=Math.ceil(Math.max(...ys))-top+1;if(w<=0||h<=0)continue;const mask=surface(w,h),c=mask.getContext('2d');c.imageSmoothingEnabled=false;c.translate(-left,-top);c.beginPath();p.forEach((v,i)=>i?c.lineTo(Math.round(v.x),Math.round(v.y)):c.moveTo(Math.round(v.x),Math.round(v.y)));c.closePath();c.clip();c.drawImage(plate,0,0,REF_W,REF_H);occluders.push({image:mask,x:left,y:top,depth:Number.isFinite(layer.depth)?layer.depth:project(layer.worldX||0,layer.worldZ||0).y});}
  }
  function atlasFrame(actor,role,action,frame,state,frameOverride=null){
    const meta=assets.spriteMeta||{};
    let entry=meta.roles?.[role]??meta.roles?.[actor.role]??meta.roles?.guest??meta.roles?.default;
    if(entry==null)return null;if(typeof entry==='number')entry={row:entry};
    const style=pixelStyle(actor,entry,meta);
    let selected=entry.actions?.[action]||entry.actions?.[['dance','mosh','slam','support'].includes(action)?'dance':action==='walk'?'walk':action==='clean'?'clean':'idle']||entry;
    if(selected.preserveStyle&&selected.variantStyles&&selected.variantStyles[style]==null)selected=entry;
    const phase=['playing','closing'].includes(state.phase),fps=finite(selected.fps||entry.fps);
    if(fps>0)frame=phase?Math.floor(seconds*fps+(action==='perform'?0:hash(actor.id)%4)):0;
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
  function drawActor(a,state){
    let role=String(a.role||'guest').toLowerCase();if(role==='band')role=String(a.id||'singer');if(a.id?.startsWith('bar-seat-'))role='barseated';
    let action=String(a.action||'idle');if(action==='sweep')action='clean';else if(a.moving&&!['mosh','slam','surf','support'].includes(action))action='walk';
    const active=['playing','closing'].includes(state.phase);
    const band=role==='singer'||role==='guitarist'||role==='bassist'||role==='drummer';
    const jump=assets.spriteMeta.roles?.[role]?.actions?.jump?pixelBandJump(role,action,seconds,active):null;
    if(jump)action='jump';
    const frame=active?Math.floor(seconds*(action==='walk'?7:action==='clean'?6:band?8:5)+hash(a.id)%4):0;
    const p=actorPoint(a,finite(a.y)),ground=actorPoint(a,role==='surfer'?0:finite(a.y)),surf=(action==='surf'||role==='surfer')&&finite(a.y)>.1;
    if(role!=='drummer'||action!=='perform')ellipse(ctx,ground.x,ground.y+1,Math.round(4*(jump?.shadow||1)),1,jump?'#16182348':'#16182360');
    const atlas=atlasFrame(a,role,action,frame,state,jump?.frame),meta=atlas?.meta||{},entry=atlas?.entry||{},size=entry.drawHeight||meta.drawHeight||40;
    const bob=!band&&active&&['dance','mosh','slam'].includes(action)?Math.round(Math.sin(seconds*5+hash(a.id)%17)*.6):0;
    ctx.save();ctx.translate(Math.round(p.x),Math.round(p.y+bob-(jump?.lift||0)));
    if(surf&&!entry.horizontal){ctx.rotate(-Math.PI/2);ctx.translate(0,-7);}
    if(atlas){const scale=size/atlas.sh,dw=entry.drawWidth||meta.drawWidth||atlas.sw*scale,dh=size,footX=entry.footX??meta.footX??atlas.sw/2,footY=entry.footY??meta.footY??atlas.sh;if(atlas.flip)ctx.scale(-1,1);ctx.drawImage(atlas.sheet,atlas.sx,atlas.sy,atlas.sw,atlas.sh,Math.round(-footX*dw/atlas.sw),Math.round(-footY*dh/atlas.sh),Math.round(dw),Math.round(dh));}
    else throw new Error('No pixel character atlas entry for '+role);
    ctx.restore();
    if(action==='clean'){const swing=Math.sin(seconds*7+hash(a.id)%5)*2;polygon(ctx,[{x:p.x+4,y:p.y-16},{x:p.x+5,y:p.y-16},{x:p.x+10+swing,y:p.y+2},{x:p.x+9+swing,y:p.y+2}],'#b69061');ctx.fillStyle='#c7b27e';ctx.fillRect(Math.round(p.x+6+swing),Math.round(p.y+2),7,2);ctx.fillStyle='#857150';for(let i=0;i<3;i++)ctx.fillRect(Math.round(p.x+6+swing+i*2),Math.round(p.y+4),1,2);}
    if((a.id==='assisted-guest'||role==='assisted')&&state.floor?.guestActive){ctx.fillStyle='#f4c86e';ctx.fillRect(Math.round(p.x),Math.round(p.y-48),2,5);ctx.fillRect(Math.round(p.x),Math.round(p.y-41),2,2);}
    if(action==='queue'){ctx.fillStyle='#e1ba73';ctx.fillRect(Math.round(p.x-2),Math.round(p.y-46),5,4);ctx.fillStyle='#34414e';ctx.fillRect(Math.round(p.x-1),Math.round(p.y-45),3,2);}
  }
  function paper(state,dt){
    const f=state.floor||{},session=state.journey?.session||0;
    if(night!==session){night=session;seenEffects.clear();particles.length=0;}
    for(const event of state.effects||[]){const id=[event.serial,event.kind,event.time].join(':');if(seenEffects.has(id))continue;seenEffects.add(id);if(finite(state.elapsed)-finite(event.time)>1.2)continue;const random=rng(hash(id));for(let i=0;i<(event.kind?22:32);i++){if(particles.length>=90)particles.shift();particles.push({x:3+random()*5,z:2.8+random()*.5,y:2.4+random()*1.6,vx:(random()-.5)*2.5,vz:-1.5-random()*2.5,vy:.5+random()*1.8,age:0,kind:event.kind,color:event.kind?'#f1ede1':['#ec719c','#61d6d8','#eed16e','#a99be0'][i%4]});}}
    const throws=finite(f.confettiThrows)+finite(f.tissueThrows),remaining=f.litter?clamp(1-f.cleaned/f.litter,0,1):0,count=Math.ceil(Math.min(42,throws*13)*remaining),random=rng(44091);
    for(let i=0;i<42;i++){const x=2+random()*7.2,z=-4.1+random()*5.2,p=project(x,z,.025);if(i>=count)continue;const tissue=i%Math.max(1,throws)<finite(f.tissueThrows);ctx.fillStyle=tissue?'#e9e5d9':['#d37da0','#62bdc6','#d4b25c','#9281b4'][i%4];ctx.fillRect(Math.round(p.x),Math.round(p.y),tissue?4:2,1);if(tissue)ctx.fillRect(Math.round(p.x+2),Math.round(p.y+1),3,1);}
    return()=>{for(let i=particles.length-1;i>=0;i--){const a=particles[i];a.age+=dt;a.vy-=dt*3.2;a.x+=a.vx*dt;a.z+=a.vz*dt;a.y+=a.vy*dt;if(a.y<.03||a.age>7){particles.splice(i,1);continue;}const p=project(a.x,a.z,a.y);ctx.fillStyle=a.color;ctx.fillRect(Math.round(p.x),Math.round(p.y),a.kind?4:2,1);if(a.kind)ctx.fillRect(Math.round(p.x+2),Math.round(p.y+1),3,1);}};
  }
  function stageLights(state){
    if(!['playing','closing'].includes(state.phase))return;
    // Match the illustrated truss; targets and alternating 120 BPM banks follow
    // SimBarReferenceLighting. The gameplay clock freezes these lights on pause.
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
      const light=rig[i],phase=(seconds+.12+(i%2)*.5)%1;
      const pulse=smooth(phase/.08)*(1-smooth((phase-.30)/.18));
      const sway=Math.sin(seconds*.72+i*1.7)*.48+Math.sin(seconds*Math.PI*4+i*Math.PI)*.08;
      const [tx,tz,ty]=light.target,x=tx+sway,z=tz+Math.cos(seconds*.58+i*1.4)*.24;
      const source=project(...light.source),end=project(x,z,ty),radius=i%2?1.18:1.48;
      // Several projected rings make a feathered pool without blurring the art.
      for(let layer=0;layer<4;layer++){
        ctx.globalAlpha=.05+layer*.018+pulse*.032;
        polygon(ctx,ring(x,z,ty+.006,radius*(1.25-layer*.23)),light.color);
      }
      const haze=ctx.createLinearGradient(source.x,source.y,end.x,end.y);
      haze.addColorStop(0,light.color+'d9');haze.addColorStop(.3,light.color+'b3');haze.addColorStop(1,light.color+'73');
      for(let layer=0;layer<3;layer++){
        ctx.globalAlpha=[.11,.065,.04][layer]+[.07,.045,.025][layer]*pulse;
        polygon(ctx,hull([source,...ring(x,z,ty,radius*[1,.7,.38][layer])]),haze);
      }
      // Small pixel-stepped source flares stay attached to the visible lamps.
      ctx.globalAlpha=.08+pulse*.08;ellipse(ctx,source.x,source.y,12,8,light.color);
      ctx.globalAlpha=.2+pulse*.15;ellipse(ctx,source.x,source.y,6,4,light.color);
      ctx.globalAlpha=.7+pulse*.2;ellipse(ctx,source.x,source.y,2,2,'#fff6df');
      ctx.globalAlpha=.22+pulse*.1;ctx.fillStyle=light.color;
      ctx.fillRect(Math.round(source.x-9),Math.round(source.y),19,1);
      ctx.fillRect(Math.round(source.x),Math.round(source.y-7),1,15);
    }
    ctx.restore();
  }
  function render(timestamp,state=getState()){
    if(destroyed)return;state=state||{};const now=finite(timestamp,performance.now()),dt=last==null?0:clamp((now-last)/1000,0,.1);last=now;const running=['playing','closing'].includes(state.phase)&&!state.paused;if(running)seconds+=dt;
    background();roomLayers();ctx.save();ctx.translate(Math.round(offsetX),0);ctx.imageSmoothingEnabled=false;ctx.drawImage(assets.venue,0,0,REF_W,REF_H);stageLights(state);const airborne=paper(state,running?dt:0);
    const drawables=occluders.map(layer=>({depth:layer.depth,draw:()=>ctx.drawImage(layer.image,layer.x,layer.y)}));
    for(const actor of state.actors||[]){if(actor.visible===false)continue;let depth=actorPoint(actor).y+(actor.action==='surf'?2:0)+(actor.id?.startsWith('bar-seat-')?25:0);if(actor.id==='drummer'&&actor.action==='perform'&&assets.venueMeta?.drumForegroundDepth>0)depth=Math.min(depth,assets.venueMeta.drumForegroundDepth-1);drawables.push({depth,draw:()=>drawActor(actor,state)});}
    drawables.sort((a,b)=>a.depth-b.depth);for(const item of drawables)item.draw();airborne();ctx.restore();
  }
  function worldToScreen(x,z,y=0){const p=project(x,z,y);return{x:(p.x+offsetX)*cssW/width,y:p.y*cssH/height};}
  function renderOverview(target,state=getState()){
    const w=target.canvas.width,h=target.canvas.height,scale=Math.min((w-8)/REF_W,(h-8)/REF_H),left=(w-REF_W*scale)/2,top=(h-REF_H*scale)/2;
    target.imageSmoothingEnabled=false;target.fillStyle='#1e2632';target.fillRect(0,0,w,h);
    target.drawImage(assets.venue,Math.round(left),Math.round(top),Math.round(REF_W*scale),Math.round(REF_H*scale));
    for(const actor of state.actors||[]){
      if(actor.visible===false)continue;
      const p=actorPoint(actor,finite(actor.y)),employee=['bartender','server'].includes(actor.role)||actor.id?.startsWith('hired-'),size=employee?4:3;
      const x=Math.round(left+p.x*scale-size/2),y=Math.round(top+p.y*scale-size/2);
      target.fillStyle='#111721';target.fillRect(x-1,y-1,size+2,size+2);
      target.fillStyle=actor.id==='assisted-guest'?'#ffd36f':employee?'#6ee59a':actor.role==='band'?'#65dbef':'#f5e4c8';
      target.fillRect(x,y,size,size);
    }
  }
  function destroy(){destroyed=true;observer.disconnect();particles.length=0;occluders=[];city=null;}
  return{render,resize,destroy,worldToScreen,project,renderOverview};
}
