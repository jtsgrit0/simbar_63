// Focused integration checks against actual runtime manifests and selected PNG pixels.
// The canvas records what would be painted; game behavior and renderer logic stay real.
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {readFileSync} from 'node:fs';
import {inflateSync} from 'node:zlib';
import {createWorld,pixelVenueFeatures,pixelVenueFeatureStyle,pixelFestivalFrame,pixelAudiencePose} from './WebGLTemplate/FastGame/world.js';
import {VENUE_PROPS} from './WebGLTemplate/FastGame/owner-venues.js';

const art=new URL('./WebGLTemplate/FastGame/art/',import.meta.url);
const manifest=name=>JSON.parse(readFileSync(new URL(name,art),'utf8'));
const images=new Map(),decoded=new Map();
function image(file){
  if(!images.has(file)){
    const bytes=readFileSync(new URL(file,art));
    assert.equal(bytes.subarray(1,4).toString(),'PNG',file);
    images.set(file,{id:file,width:bytes.readUInt32BE(16),height:bytes.readUInt32BE(20),bytes});
  }
  return images.get(file);
}

// Decode the actual PNG, including PNG scanline filters. This is deliberately
// independent of source packing reports: a stale or empty exported cell fails.
function pixels(sheet){
  if(decoded.has(sheet))return decoded.get(sheet);
  const bytes=sheet.bytes,depth=bytes[24],type=bytes[25],channels=({0:1,2:3,3:1,4:2,6:4})[type];
  assert(channels&&[1,2,4,8].includes(depth),`Unsupported PNG format: ${sheet.id}`);
  assert(type===3||depth===8,`Only indexed PNGs may use packed samples: ${sheet.id}`);
  assert.equal(bytes[28],0,'Test fixture PNG must be noninterlaced');
  const chunks=[];let palette,alpha;
  for(let at=8;at<bytes.length;){
    const length=bytes.readUInt32BE(at),kind=bytes.toString('ascii',at+4,at+8),data=bytes.subarray(at+8,at+8+length);
    if(kind==='IDAT')chunks.push(data);if(kind==='PLTE')palette=data;if(kind==='tRNS')alpha=data;
    at+=12+length;
  }
  const raw=inflateSync(Buffer.concat(chunks)),stride=Math.ceil(sheet.width*channels*depth/8),bpp=Math.max(1,Math.ceil(channels*depth/8));
  assert.equal(raw.length,(stride+1)*sheet.height);
  const scan=Buffer.alloc(stride*sheet.height),rgba=Buffer.alloc(sheet.width*sheet.height*4);
  const paeth=(a,b,c)=>{const p=a+b-c,da=Math.abs(p-a),db=Math.abs(p-b),dc=Math.abs(p-c);return da<=db&&da<=dc?a:db<=dc?b:c;};
  for(let y=0;y<sheet.height;y++){
    const filter=raw[y*(stride+1)];assert(filter<=4,'Invalid PNG filter');
    for(let x=0;x<stride;x++){
      const at=y*stride+x,a=x>=bpp?scan[at-bpp]:0,b=y?scan[at-stride]:0,c=y&&x>=bpp?scan[at-stride-bpp]:0;
      scan[at]=(raw[y*(stride+1)+1+x]+[0,a,b,Math.floor((a+b)/2),paeth(a,b,c)][filter])&255;
    }
    for(let x=0;x<sheet.width;x++){
      const out=(y*sheet.width+x)*4,at=y*stride+x*channels;
      if(type===3){
        const bit=x*depth,index=(scan[y*stride+(bit>>3)]>>(8-depth-(bit&7)))&((1<<depth)-1);
        assert(palette&&index*3+2<palette.length,'Palette index is out of bounds');
        rgba[out]=palette[index*3];rgba[out+1]=palette[index*3+1];rgba[out+2]=palette[index*3+2];rgba[out+3]=alpha?.[index]??255;
      }else{
        const gray=type===0||type===4;
        rgba[out]=scan[at];rgba[out+1]=gray?scan[at]:scan[at+1];rgba[out+2]=gray?scan[at]:scan[at+2];rgba[out+3]=type===6?scan[at+3]:type===4?scan[at+1]:255;
      }
    }
  }
  decoded.set(sheet,rgba);return rgba;
}
function paintedCell(draw){
  const [sheet,x,y,w,h]=draw.args;
  for(const value of [x,y,w,h])assert(Number.isInteger(value),'Sprite source coordinates must be integer pixels');
  assert(w>0&&h>0&&x>=0&&y>=0&&x+w<=sheet.width&&y+h<=sheet.height,`Source outside ${sheet.id}: ${[x,y,w,h]}`);
  const rgba=pixels(sheet),visible=Buffer.alloc(w*h*4);let count=0,left=w,top=h,right=0,bottom=0;
  for(let sy=0;sy<h;sy++)for(let sx=0;sx<w;sx++){
    const at=((y+sy)*sheet.width+x+sx)*4,out=(sy*w+sx)*4;
    if(rgba[at+3]){rgba.copy(visible,out,at,at+4);count++;left=Math.min(left,sx);top=Math.min(top,sy);right=Math.max(right,sx+1);bottom=Math.max(bottom,sy+1);}
  }
  assert(count>20,`Empty or unreadable source cell in ${sheet.id}: ${[x,y,w,h]}`);
  return{hash:createHash('sha256').update(visible).digest('hex'),bounds:[left,top,right,bottom],count};
}

function context(){
  let translation=[0,0],rotation=0,matrix=[1,0,0,1,0,0];const stack=[];
  return new Proxy({draws:[],filter:'none',createLinearGradient(){return{addColorStop(){}};},
    save(){stack.push({translation:[...translation],rotation,matrix:[...matrix],filter:this.filter});},restore(){const last=stack.pop();if(last){translation=last.translation;rotation=last.rotation;matrix=last.matrix;this.filter=last.filter;}},
    translate(x,y){translation=[translation[0]+x,translation[1]+y];},rotate(value){rotation+=value;},
    transform(a,b,c,d,e,f){const [aa,ab,ac,ad,ae,af]=matrix;matrix=[aa*a+ac*b,ab*a+ad*b,aa*c+ac*d,ab*c+ad*d,aa*e+ac*f+ae,ab*e+ad*f+af];},
    drawImage(...args){this.draws.push({args,translation:[...translation],rotation,matrix:[...matrix],filter:this.filter});}
  },{get:(value,key)=>key in value?value[key]:()=>{}});
}
function canvas(width=960,height=558){
  const ctx=context(),value={width,height,style:{},box:{width,height},ctx,getContext:()=>ctx,getBoundingClientRect(){return this.box;}};ctx.canvas=value;return value;
}
globalThis.document={createElement:()=>canvas()};
globalThis.ResizeObserver=class{observe(){}disconnect(){}};
const spriteMeta=manifest('sprites.json'),bandModels=manifest('band-models.json'),festivalModels=manifest('festival-models.json');
const inspectionModels=manifest('inspection-models.json'),venueFeatureModels=manifest('venue-features.json');
const atlases=Object.fromEntries(Object.entries(spriteMeta.atlases).map(([id,entry])=>[id,image(entry.file)]));
const assets=()=>({venue:image('venue.png'),venueMeta:manifest('venue.json'),spriteMeta,atlases,
  venues:{freebird:image('venue-freebird.png'),ff:image('venue-ff.png')},venueMetas:{freebird:manifest('venue-freebird.json'),ff:manifest('venue-ff.json')},
  propImages:Object.fromEntries(Object.entries(VENUE_PROPS).map(([id,file])=>[id,image(file)])),
  bandModels,bandAtlases:Object.fromEntries(bandModels.bands.map(band=>[band.id,image(band.file)])),
  festivalModels,festivalAtlases:Object.fromEntries(Object.entries(festivalModels.atlases).map(([id,entry])=>[id,image(entry.file)])),
  inspectionModels,inspectors:image('inspectors.png'),venueFeatureModels,venueFeatures:image('venue-features.png')});
const target=canvas(),world=createWorld(target,{assets:assets()});let milliseconds=0;
const render=state=>{target.ctx.draws=[];world.render(milliseconds,state);milliseconds+=100;return target.ctx.draws;};
const base={owner:0,phase:'playing',playlist:0,actors:[],upgrades:[0,0,0],activeEvent:null};
const featureDraws=draws=>draws.filter(draw=>draw.args[0]===image('venue-features.png'));
const snapshot=state=>JSON.stringify(state);
const freeze=value=>{for(const nested of Object.values(value))if(nested&&typeof nested==='object')freeze(nested);return Object.freeze(value);};

// A purchase at every category/level changes the actual selected drawing, not
// just a stat label. All owners must retain the same saved upgrade identities.
const expectedLevels=[['draught-1','sound-1','vip-1'],['draught-2','sound-2','vip-2'],['draught-3','sound-3','vip-3']];
const distinctFeatures=new Set();
for(const owner of [0,1,2])for(const level of [1,2,3]){
  const state=freeze({...base,owner,upgrades:[level,level,level]}),before=snapshot(state);
  assert.deepEqual(pixelVenueFeatures(state).map(item=>item.id),expectedLevels[level-1]);
  const draws=featureDraws(render(state));assert.equal(draws.length,3);
  const venue=['freebird','ff','original'][owner];
  const expectedCells=expectedLevels[level-1].map(id=>{const item=venueFeatureModels.items[id];return item.variants?.[venue]||item;}).map(cell=>`${cell.column*96},${cell.row*96}`).sort();
  assert.deepEqual(draws.map(draw=>draw.args.slice(1,3).join(',')).sort(),expectedCells);
  for(const draw of draws){
    assert.deepEqual(draw.args.slice(3,5),[96,96]);distinctFeatures.add(paintedCell(draw).hash);
    const feature=pixelVenueFeatures(state).find(feature=>{const item=venueFeatureModels.items[feature.id],cell=item.variants?.[venue]||item;return draw.args[1]===cell.column*96&&draw.args[2]===cell.row*96;});
    const style=pixelVenueFeatureStyle(feature);
    assert.deepEqual(draw.translation,[feature.x,feature.y],'An upgrade must retain its authored foot anchor');
    assert.equal(draw.rotation,0,'Upright equipment must not rotate');
    assert.deepEqual(draw.matrix,[1,style.shearY,0,1,0,0],'Final alignment must retain vertical stems and cabinet edges');
    assert.equal(draw.filter,style.filter);assert(+draw.filter.match(/brightness\(([\d.]+)\)/)[1]<1);assert(+draw.filter.match(/saturate\(([\d.]+)\)/)[1]<1);
  }
  assert.equal(snapshot(state),before,'Rendering a purchased upgrade must preserve saved state');
}
assert.equal(distinctFeatures.size,12,'Nine shared upgrades and three genuine FF draught views must be distinct');
const legacyBytes=readFileSync(new URL('../ArtSource/PixelArt/VenueFeatures/venue-features-v1.runtime.png',import.meta.url));
const legacy={id:'legacy-event-atlas',width:legacyBytes.readUInt32BE(16),height:legacyBytes.readUInt32BE(20),bytes:legacyBytes};
const eventIds=['open-mic','rock-showcase','fan-festival'];
for(const [activeEvent,id] of eventIds.entries()){
  const state=freeze({...base,activeEvent}),before=snapshot(state),features=pixelVenueFeatures(state),draws=featureDraws(render(state));
  assert.deepEqual(features.map(item=>item.id),[id]);assert.equal(draws.length,1);
  const cell=venueFeatureModels.items[id];assert.deepEqual(draws[0].args.slice(1,5),[cell.column*96,cell.row*96,96,96]);
  assert.deepEqual(draws[0].matrix,[1,0,0,1,0,0]);assert.equal(draws[0].filter,'none','Event decoration must retain its original lighting');
  assert.equal(paintedCell(draws[0]).hash,paintedCell({args:[legacy,288,activeEvent*96,96,96]}).hash,'Event pixels must remain identical');
  distinctFeatures.add(paintedCell(draws[0]).hash);assert.equal(snapshot(state),before);
}
assert.equal(distinctFeatures.size,15);
for(const index of [0,1,2]){
  const saved=freeze({...base,upgrades:[1,1,1],activeEvent:0}),before=snapshot(saved);
  const preview=freeze({...saved,visualPreview:{type:'upgrade',index}}),previewBefore=snapshot(preview);
  const items=pixelVenueFeatures(preview);assert.equal(items.find(item=>item.index===index&&item.type==='upgrade').id,expectedLevels[1][index]);
  const draws=featureDraws(render(preview));assert.equal(draws.length,4);
  const expectedCells=[0,1,2].map(category=>`${(category===index?1:0)*96},${category*96}`).concat('288,0').sort();
  assert.deepEqual(draws.map(draw=>draw.args.slice(1,3).join(',')).sort(),expectedCells,'The next-level preview must paint its own prop while retaining the other purchased props');
  assert.equal(snapshot(preview),previewBefore);
  assert.equal(snapshot(saved),before,'A visual preview cannot purchase or persist the next level');
  assert.deepEqual(pixelVenueFeatures(saved).map(item=>item.id),[...expectedLevels[0],eventIds[0]],'Closing the preview must restore the actual saved selections');
}
assert.deepEqual(pixelVenueFeatures({...base,upgrades:[3,0,0],visualPreview:{type:'upgrade',index:0}}).map(item=>item.id),['draught-3'],'Previewing a fully upgraded item must keep its final valid prop');
const eventSaved=freeze({...base,upgrades:[1,2,3],activeEvent:0}),eventBefore=snapshot(eventSaved);
for(const index of [1,2]){
  const preview=freeze({...eventSaved,visualPreview:{type:'event',index}}),before=snapshot(preview);
  assert.equal(pixelVenueFeatures(preview).filter(item=>item.type==='event')[0].id,eventIds[index]);
  const draws=featureDraws(render(preview));assert.equal(draws.length,4);
  assert(draws.some(draw=>draw.args[1]===288&&draw.args[2]===index*96),'Selecting an event must paint the selected event cell');
  assert.equal(snapshot(preview),before);
}
assert.equal(snapshot(eventSaved),eventBefore);assert.equal(pixelVenueFeatures(eventSaved).at(-1).id,'open-mic');

// All three staff identities use genuine front/back direction and walk cells.
const inspectors=inspectionModels.models.map((model,index)=>({id:model.id,index,role:'inspector',x:index-2,z:0,facing:0,moving:false,action:'idle'}));
const staffState={...base,actors:inspectors},staffRows=new Set(),staffFrames=new Set();
for(const [facing,direction] of [[0,'BR'],[Math.PI/2,'FR'],[Math.PI,'FL'],[-Math.PI/2,'BL']]){
  for(const actor of inspectors){actor.facing=facing;actor.moving=false;}
  const draws=render(staffState).filter(draw=>draw.args[0]===image('inspectors.png'));
  assert.equal(draws.length,3);
  const presentation=world.getInspectorPresentation(staffState);
  assert.deepEqual(presentation.map(item=>item.direction),[direction,direction,direction]);
  assert.deepEqual(presentation.map(item=>item.row),[0,1,2]);
  for(const draw of draws){
    assert.equal(draw.args[1],inspectionModels.directionColumns[direction]*64);
    staffRows.add(draw.args[2]);staffFrames.add(paintedCell(draw).hash);
  }
  for(const actor of inspectors)actor.moving=true;
  const walking=new Set();
  for(let tick=0;tick<8;tick++){
    const frames=render(staffState).filter(draw=>draw.args[0]===image('inspectors.png'));
    for(const draw of frames){
      const frame=draw.args[1]/64-inspectionModels.directionColumns[direction];
      assert([1,2].includes(frame),'Moving inspectors use the two actual walking poses');walking.add(frame);staffFrames.add(paintedCell(draw).hash);
    }
  }
  assert.deepEqual([...walking].sort(),[1,2]);
}
assert.equal(staffRows.size,3);assert.equal(staffFrames.size,36,'Every identity/direction/pose should have genuine distinct pixels');

// A district visit freezes audience drawings while the same authored band
// keeps playing. Neither role may be hidden or replaced by a different model.
const guest={id:'festival-test-guest',slot:0,role:'guest',styleKey:'Guest/Punk',x:1,z:0,y:0,moving:true,facing:0,action:'dance',festivalAction:'jump',festivalRemaining:30,behaviorPhase:0,animationSpeed:1};
const band=[['singer',5.7,3.5],['guitarist',3.7,3.8],['bassist',7.5,3.8],['drummer',5.6,4.7]].map(([id,x,z])=>({id,role:'band',x,z,homeX:x,homeZ:z,stageLevel:1,moving:false,action:'perform'}));
const frozenState=freeze({...base,owner:1,actors:[guest,...band],inspection:{active:true,freezePerformers:true}});
for(const role of ['guest','seated','support','surfer','assisted'])for(const time of [0,1.2,9.4,19.8]){
  const actor={...guest,role};assert.equal(pixelFestivalFrame(actor,frozenState,time,festivalModels),null);
  const pose=pixelAudiencePose(actor,frozenState,time);assert.equal(pose.action,'idle');
  assert.deepEqual([pose.sway,pose.bob,pose.lean,pose.lift],[0,0,0,0]);
}
const bandSheets=new Set(bandModels.bands.map(entry=>image(entry.file)));
const normalSheets=new Set(Object.values(atlases));
const bandDraws=draws=>draws.filter(draw=>bandSheets.has(draw.args[0]));
const normalDraws=draws=>draws.filter(draw=>normalSheets.has(draw.args[0]));
const drawSignature=draw=>[draw.args[0].id,...draw.args.slice(1),...draw.translation,draw.rotation];
let frozenAudience;const inspectedBandColumns=new Set(),inspectedBandDrawings=new Set();
const frozenBefore=snapshot(frozenState);
for(let tick=0;tick<125;tick++){
  const draws=render(frozenState),artists=bandDraws(draws),audience=normalDraws(draws);
  assert.equal(artists.length,6,'The active Freewill roster must keep all its visible members');assert.equal(audience.length,1);
  for(const artist of artists){
    assert(artist.args[1]/64>=0&&artist.args[1]/64<4,'The inspected band must keep its authored performance cells');
    inspectedBandColumns.add(artist.args[1]/64);
  }
  assert.equal(audience[0].args[0],atlases.wardrobe,'Inspection must suppress festival dance/jump artwork');
  assert.equal(audience[0].rotation,0,'An inspected guest must stand upright');
  const artistSignatures=artists.map(drawSignature),audienceSignature=drawSignature(audience[0]);
  inspectedBandDrawings.add(JSON.stringify(artistSignatures));
  if(tick===0){frozenAudience=audienceSignature;for(const draw of [...artists,...audience])paintedCell(draw);}
  else assert.deepEqual(audienceSignature,frozenAudience,'Advancing time must not sway or jump the inspected guest');
  assert(world.getBandPresentation(frozenState).every(item=>item.action==='perform'));
  const live=world.getAudiencePresentation(frozenState)[0];assert.equal(live.action,'idle');assert.equal(live.atlas,null);assert.equal(live.lift,0);
}
assert.equal(inspectedBandColumns.size,4,'The band must continue all four performance poses during inspection');
assert(inspectedBandDrawings.size>1,'The band presentation clock stopped during inspection');
assert.equal(snapshot(frozenState),frozenBefore);
const pausedInspection={...frozenState,paused:true};
const pausedBand=bandDraws(render(pausedInspection)).map(drawSignature);
for(let tick=0;tick<12;tick++)assert.deepEqual(bandDraws(render(pausedInspection)).map(drawSignature),pausedBand,'Manual pause must still stop the band during inspection');
const resumed={...frozenState,actors:[{...guest,moving:false},...band],inspection:{active:false,freezePerformers:false}},performanceColumns=new Set(),festivalColumns=new Set();
for(let tick=0;tick<30;tick++){
  const draws=render(resumed);
  for(const draw of bandDraws(draws)){assert(draw.args[1]/64>=0&&draw.args[1]/64<4);performanceColumns.add(draw.args[1]/64);}
  const audience=draws.filter(draw=>draw.args[0]===image('festival-gestures.png'));assert.equal(audience.length,1);festivalColumns.add(audience[0].args[1]/64);
  assert.equal(world.getAudiencePresentation(resumed)[0].festivalAction,'jump');
}
assert.equal(performanceColumns.size,4,'Artists resume all four authored performance poses');assert.equal(festivalColumns.size,4,'Audience reactions resume all four authored gesture poses');
world.destroy();

// The retained sprite-atlas band follows the same rule when the newer named
// band models are absent, including its original timed guitarist jump poses.
const legacyTarget=canvas(),legacyWorld=createWorld(legacyTarget,{assets:{...assets(),bandModels:null,bandAtlases:{}}});
const legacyState={...frozenState,actors:[band.find(a=>a.id==='guitarist')]},legacyFrames=new Set(),legacyFeet=new Set();
for(let tick=0;tick<125;tick++){
  legacyTarget.ctx.draws=[];legacyWorld.render(tick*100,legacyState);
  const drawings=normalDraws(legacyTarget.ctx.draws);assert.equal(drawings.length,1);
  const drawing=drawings[0];paintedCell(drawing);legacyFrames.add(drawing.args.slice(0,5).map(v=>v?.id||v).join(','));legacyFeet.add(drawing.translation[1]);
  assert.equal(pixelAudiencePose(legacyState.actors[0],legacyState,tick*.1).action,'perform');
}
assert(legacyFrames.size>1,'Legacy band frames stopped during inspection');
assert(legacyFeet.size>1,'Legacy guitarist jump stopped during inspection');
legacyWorld.destroy();

// Portrait mode contains the whole authored room; landscape retains its
// existing height-scaled camera and world-to-screen relationship.
const mobile=canvas(390,844),mobileWorld=createWorld(mobile,{assets:assets()});
for(const owner of [0,1,2]){
  mobile.ctx.draws=[];mobileWorld.render(0,{...base,owner});
  const bounds=mobileWorld.getVenueBounds();assert(bounds.left>=0&&bounds.right<=390&&bounds.top>=0&&bounds.bottom<=844);
  assert(bounds.width>360&&bounds.height>220,'The room must fit across a portrait phone instead of cropping to its center');
  const room=mobile.ctx.draws.find(draw=>draw.args[0]===image(owner===0?'venue-freebird.png':owner===1?'venue-ff.png':'venue.png'));
  assert(room);const [,,,roomWidth,roomHeight]=room.args;
  const painted={left:room.translation[0]*390/mobile.width,top:room.translation[1]*844/mobile.height,right:(room.translation[0]+roomWidth)*390/mobile.width,bottom:(room.translation[1]+roomHeight)*844/mobile.height};
  assert(painted.left>=0&&painted.top>=0&&painted.right<=390&&painted.bottom<=844,'The actual venue draw must fit inside the phone canvas');
}
mobileWorld.destroy();
const desktop=canvas(),desktopWorld=createWorld(desktop,{assets:assets()});
for(const point of [[0,0,0],[-8,3,0],[6,4,1]])assert.deepEqual(desktopWorld.worldToScreen(...point),desktopWorld.project(...point));
desktop.box={width:1660,height:624};desktopWorld.resize();
for(const point of [[0,0,0],[-8,3,0],[6,4,1]]){
  const projected=desktopWorld.project(...point),screen=desktopWorld.worldToScreen(...point);
  assert(Math.abs(screen.x-(830+(projected.x-480)*624/558))<.6);assert(Math.abs(screen.y-projected.y*624/558)<.01);
}
desktopWorld.destroy();
console.log('PASS: 9 upgrade props + 3 FF draught views, anchored upright perspective and room lighting, 3 pixel-identical events, nonmutating previews, real PNG cells, 36 inspector frames, audience-only inspection pause, portrait room fit and landscape projection.');
