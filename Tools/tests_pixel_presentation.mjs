import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {createWorld,pixelDirection,pixelFacing,pixelStyle} from './WebGLTemplate/FastGame/world.js';
import {SimBarGame} from './WebGLTemplate/FastGame/core.js';

assert.deepEqual([[0,1],[1,0],[0,-1],[-1,0]].map(([x,z])=>pixelDirection(x,z)),['BR','FR','FL','BL']);
assert.equal(pixelFacing({id:'bar-seat-0',facing:Math.PI},'barseated','idle'),'BR');
assert.equal(pixelFacing({id:'lounge-0'},'seated','idle'),'FL');
assert.equal(pixelFacing({id:'lounge-4'},'seated','idle'),'BR');
assert.equal(pixelFacing({id:'bartender',x:-7,z:-.565,moving:false,facing:0},'bartender','serve'),'FL');
assert.equal(pixelFacing({id:'bartender',x:-7,z:-.565,moving:true,facing:Math.PI/2},'bartender','walk'),'FR');
assert.equal(pixelFacing({id:'singer',x:5.5,z:2.7,facing:0},'singer','perform'),'FL');
assert.equal(pixelFacing({id:'guitarist',x:3,z:3.8,facing:0},'guitarist','perform'),'FR');
assert.deepEqual([[6.75,-1.25],[5.75,-.25],[4.75,-1.25],[5.75,-2.25]].map(([x,z])=>pixelFacing({x,z},'guest','mosh')),['BR','BL','FL','FR']);
assert.equal(pixelFacing({x:5.1,z:-1.25},'guest','slam'),'FR');
assert.equal(pixelFacing({x:6.4,z:-1.25},'guest','slam'),'BL');
assert.equal(pixelFacing({x:5,z:-4.25},'support','support',{actors:[{id:'crowd-surfer',x:6,z:-4.25}]}),'FR');

const styles={Casual:0,Suit:1,Beach:2,Worker:3,Farmer:4,Adventurer:5,Casual2:6,Punk:7};
assert.equal(pixelStyle({id:'patron-3',slot:3},{variantStyles:styles}),'Worker');
assert.equal(pixelStyle({id:'patron-3',styleKey:'Guest/Punk'},{variantStyles:styles}),'Punk');
assert.equal(pixelStyle({id:'bartender'},{style:'Suit'}),'Suit');

function context(){return new Proxy({draws:[],scales:[],rotations:[],drawImage(...args){this.draws.push(args);},scale(...args){this.scales.push(args);},rotate(value){this.rotations.push(value);}},{get(o,key){return key in o?o[key]:(()=>{});}});}
function canvas(){const ctx=context(),value={width:960,height:558,style:{},getContext(){return ctx;},getBoundingClientRect(){return{width:960,height:558};},ctx};ctx.canvas=value;return value;}
globalThis.document={createElement:()=>canvas()};
globalThis.ResizeObserver=class{observe(){}disconnect(){}};
const definition=file=>({file,frameWidth:64,frameHeight:64,columns:4,directionColumns:{FR:0,BR:1,BL:2,FL:3}});
const meta={frameWidth:64,frameHeight:64,drawHeight:54,footX:32,footY:60,defaultAtlas:'wardrobe',atlases:{wardrobe:definition('wardrobe.png'),walk:definition('walk.png'),poses:definition('poses.png')},roles:{
  guest:{atlas:'wardrobe',variantStyles:styles,actions:{walk:{atlasFrames:['wardrobe','walk']},mosh:{atlas:'poses',row:5},dance:{atlas:'poses',variantStyles:{Adventurer:5},preserveStyle:true}}},
  bartender:{atlas:'wardrobe',style:'Suit',variantStyles:styles,actions:{serve:{atlas:'poses',row:0}}},
  barseated:{atlas:'poses',row:1},seated:{atlas:'poses',row:3},
  surfer:{atlas:'wardrobe',style:'Casual',row:0,actions:{surf:{atlas:'poses',row:7,horizontal:true,footX:32,footY:32}}}
}};
const atlases=Object.fromEntries(Object.keys(meta.atlases).map(key=>[key,{id:key,width:256,height:512}]));
const assets={venue:{id:'venue',width:960,height:558},atlases,spriteMeta:meta,venueMeta:{occluders:[]}};
assert.throws(()=>createWorld(canvas(),{assets:{...assets,atlases:{wardrobe:atlases.wardrobe}}}),/atlas must finish loading/);
const target=canvas(),world=createWorld(target,{assets});
const draw=(actor,time=0)=>{target.ctx.draws=[];target.ctx.scales=[];target.ctx.rotations=[];world.render(time,{phase:'playing',actors:[actor]});return target.ctx.draws.find(args=>Object.values(atlases).includes(args[0]));};
for(const [facing,column] of [[0,1],[Math.PI/2,0],[Math.PI,3],[-Math.PI/2,2]]){
  const args=draw({id:'patron-3',slot:3,role:'guest',x:1,z:1,moving:true,action:'walk',facing});
  assert.equal(args[1],column*64);assert.equal(args[2],3*64);assert.equal(args[4],64);
  assert.equal(target.ctx.scales.length,0,'Four real direction drawings never need a mirrored front.');
}
const files=new Set();for(let n=0;n<12;n++)files.add(draw({id:'patron-0',slot:0,role:'guest',x:1,z:1,moving:true,action:'walk',facing:0},n*100)[0].id);
assert.deepEqual([...files].sort(),['walk','wardrobe']);
let args=draw({id:'bartender',role:'bartender',x:-7,z:-.565,action:'serve'});
assert.equal(args[0],atlases.poses);assert.equal(args[2],0,'An explicit action row wins over the base wardrobe style row.');
args=draw({id:'bar-seat-0',role:'seated',x:-7,z:-3,action:'idle'});assert.equal(args[0],atlases.poses);assert.equal(args[1],64);assert.equal(args[4],64,'The seated view uses the complete bent-knee drawing.');
args=draw({id:'patron-0',slot:0,role:'guest',x:1,z:1,action:'dance'});assert.equal(args[0],atlases.wardrobe,'An unsupported action style keeps the original outfit.');
args=draw({id:'crowd-surfer',role:'surfer',x:6,z:-4.25,y:1.85,action:'surf'});assert.equal(args[0],atlases.poses);assert.equal(args[2],7*64);assert.equal(args[6],-27);assert.equal(target.ctx.rotations.length,0,'A drawn horizontal surf pose is not a rotated standing person.');
world.destroy();
const contract=JSON.parse(readFileSync(new URL('../ArtSource/PixelArt/DirectionalSpriteContract.json',import.meta.url),'utf8'));
const contractAtlases=Object.fromEntries(Object.keys(contract.atlases).map(key=>[key,{id:key,width:256,height:key==='band'?256:512}]));
const realWorld=createWorld(canvas(),{assets:{venue:assets.venue,atlases:contractAtlases,spriteMeta:contract,venueMeta:{occluders:[]}}});
const game=new SimBarGame({storage:{getItem(){return null;},setItem(){},removeItem(){}}});
game.act('start');game.act('launch');game.act('guide');game.act('guide');game.act('guide');
realWorld.render(0,{phase:game.phase,actors:game.actors});
for(const actor of game.actors.filter(a=>a.slot>=0&&a.slot<8)){actor.action=actor.slot<6?'mosh':'slam';actor.moving=true;}
const surfer=game.actors.find(a=>a.id==='crowd-surfer');surfer.action='surf';surfer.y=1.85;
for(const actor of game.actors.filter(a=>a.role==='support'))actor.action='support';
realWorld.render(100,{phase:'playing',actors:game.actors});
realWorld.destroy();
console.log('PASS: directional furniture/band/pit facing, costume identity, full seated poses, genuine direction cells, two walking poses, explicit action rows, horizontal surfing, and essential atlas gate.');
