import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {SimBarGame} from './WebGLTemplate/FastGame/core.js';
import {createWorld,protectOrderBubblePlacements} from './WebGLTemplate/FastGame/world.js';
import {VENUE_PROPS} from './WebGLTemplate/FastGame/owner-venues.js';
import {launchPreparedDesk} from './native_test_workflow.mjs';

const authored=[[[500,74,950,412]],[[564,77,947,400]],[[367,36,552,134],[549,76,917,395]]];
const regionsFor=owner=>authored[owner].map(([left,top,right,bottom])=>({left,top,right,bottom}));
const intersects=(a,b,pad=4)=>a.left<b.right+pad&&a.right>b.left-pad&&a.top<b.bottom+pad&&a.bottom>b.top-pad;
const body=b=>({left:b.x,top:b.y,right:b.x+b.width,bottom:b.y+b.height});
const inside=(r,b)=>r.left>=b.left&&r.top>=b.top&&r.right<=b.right&&r.bottom<=b.bottom;
const pointBounds=points=>({left:Math.min(...points.map(p=>p.x)),top:Math.min(...points.map(p=>p.y)),right:Math.max(...points.map(p=>p.x)),bottom:Math.max(...points.map(p=>p.y))});
const freeze=value=>{for(const child of Object.values(value))if(child&&typeof child==='object')freeze(child);return Object.freeze(value);};
const bubble=(id,x,y)=>({actor:{id},item:+id.split('-').at(-1)%6,x,y,width:32,height:28,tail:4,tipX:x+16,tipY:y+32,neckX:x+16});
const bounds={left:4,top:4,right:956,bottom:554};

// Ordinary cards keep the same object and every original geometry field.
const ordinary=freeze([bubble('guest-0',100,160),bubble('guest-1',150,160)]);
const untouched=protectOrderBubblePlacements(ordinary,regionsFor(0),bounds);
untouched.forEach((value,index)=>assert.equal(value,ordinary[index]));
const clutter=freeze(Array.from({length:7},(_,i)=>bubble('guest-'+i,700,280))),clutterBefore=JSON.stringify(clutter);
const protectedClutter=protectOrderBubblePlacements(clutter,regionsFor(0),bounds);
assert.equal(protectedClutter.length,7,'Protection cannot delete an actual open tab');
for(const [index,value] of protectedClutter.entries()){
  assert.equal(value.actor,clutter[index].actor);assert.equal(value.item,clutter[index].item);
  assert(inside(body(value),bounds));assert(regionsFor(0).every(region=>!intersects(body(value),region)));
  assert.equal(value.tailVisible,false,'A guest anchor inside the stage cannot have a pointer painted across it');
  for(const peer of protectedClutter.slice(0,index))assert(!intersects(body(value),body(peer)),'Seven orders must remain separately readable');
}
assert.equal(JSON.stringify(clutter),clutterBefore,'Canvas placement cannot mutate orders or actor data');
assert.deepEqual(protectOrderBubblePlacements(clutter,regionsFor(0),bounds),protectedClutter,'Safe relocation must be deterministic');
const longTail=freeze({...bubble('guest-0',450,420),tipX:700,tipY:320});
const shortened=protectOrderBubblePlacements([longTail],regionsFor(0),bounds)[0];
assert.equal(shortened.x,longTail.x);assert.equal(shortened.y,longTail.y);assert.equal(shortened.tailVisible,false,'A safe body needs no unnecessary move when only its decorative tail is unsafe');

function context(){let path=[];return new Proxy({draws:[],polygons:[],createLinearGradient(){return{addColorStop(){}};},
  drawImage(...args){this.draws.push(args);},beginPath(){path=[];},moveTo(x,y){path.push({x,y});},lineTo(x,y){path.push({x,y});},
  fill(){this.polygons.push({color:this.fillStyle,points:[...path]});}
},{get:(value,key)=>key in value?value[key]:()=>{}});}
function canvas(width=960,height=558){const ctx=context(),value={width,height,style:{},ctx,getContext:()=>ctx,getBoundingClientRect:()=>({width,height})};ctx.canvas=value;return value;}
globalThis.document={createElement:()=>canvas()};globalThis.ResizeObserver=class{observe(){}disconnect(){}};
const art=new URL('./WebGLTemplate/FastGame/art/',import.meta.url),json=name=>JSON.parse(readFileSync(new URL(name,art),'utf8')),cache=new Map();
function image(file){if(!cache.has(file)){const bytes=readFileSync(new URL(file,art));cache.set(file,{id:file,width:bytes.readUInt32BE(16),height:bytes.readUInt32BE(20)});}return cache.get(file);}
const spriteMeta=json('sprites.json'),atlases=Object.fromEntries(Object.entries(spriteMeta.atlases).map(([key,value])=>[key,image(value.file)]));
const assets=()=>({spriteMeta,atlases,venue:image('venue.png'),venueMeta:json('venue.json'),venues:{freebird:image('venue-freebird.png'),ff:image('venue-ff.png')},venueMetas:{freebird:json('venue-freebird.json'),ff:json('venue-ff.json')},propImages:Object.fromEntries(Object.entries(VENUE_PROPS).map(([key,file])=>[key,image(file)]))});
const sizes=[[1660,624],[1600,900],[844,390],[915,412],[667,375]];
let snapshots=0,movedInitialFreebird=0;
for(const owner of [0,1,2]){
  const game=new SimBarGame({seed:1721,storage:{getItem(){return null;},setItem(){},removeItem(){}}});
  game.act('owner',{index:owner});game.act('start');launchPreparedDesk(game);while(game.phase==='guide')game.act('guide');
  const worlds=sizes.map(([width,height])=>{const target=canvas(width,height);return{target,world:createWorld(target,{assets:assets()})};});
  let last=0;
  for(const seconds of [0,.25,.5,1,2,3,5,8]){
    for(let left=seconds-last;left>1e-8;left-=.05)game.tick(Math.min(.05,left));last=seconds;
    const state={...game.state,owner,phase:game.phase,paused:game.paused,journey:game.journey,floor:game.floor,inspection:game.inspection,actors:game.actors,orders:game.orders,effects:game.effects,elapsed:game.elapsed};
    const before=JSON.stringify(state);
    for(const {world,target} of worlds){
      target.ctx.draws=[];target.ctx.polygons=[];world.render(0,state);
      const presentation=world.getOrderBubblePresentation(state),icons=target.ctx.draws.filter(args=>args[0]===atlases.orders),regions=regionsFor(owner);
      assert.equal(presentation.length,game.orders.length);assert.equal(icons.length,game.orders.length);
      assert.deepEqual(presentation.map(value=>[value.actorId,value.item]),game.orders.map(order=>[order.id,order.item]));
      const origin=world.imageToScreen(0,0),scale=world.imageToScreen(1,0).x-origin.x,offset=origin.x/scale;
      const viewport={left:-offset+4,top:4,right:target.width-offset-4,bottom:target.height-4};
      for(const [index,value] of presentation.entries()){
        assert(inside(value.body,viewport),JSON.stringify({owner,seconds,value,viewport}));
        assert(regions.every(region=>!intersects(value.body,region)),JSON.stringify({owner,seconds,value}));
        if(value.tailVisible){assert(value.tail);assert(inside(value.tail.bounds,viewport));assert(regions.every(region=>!intersects(value.tail.bounds,region)));}
        else assert.equal(value.tail,null);
        for(const peer of presentation.slice(0,index))assert(!intersects(value.body,peer.body));
        const draw=icons[index];assert.deepEqual(draw.slice(1,5),[game.orders[index].item*32,0,32,32],'Every canvas icon samples its real existing order cell');
        assert.deepEqual(draw.slice(5),[value.body.left+4,value.body.top+2,24,24],'The unchanged icon is drawn inside the reported unchanged-size card');
        if(owner===0&&seconds===0&&value.relocated)movedInitialFreebird++;
      }
      const tails=target.ctx.polygons.filter(polygon=>['#182333','#fff5d8'].includes(polygon.color));
      assert.equal(tails.length,presentation.filter(value=>value.tailVisible).length*2,'Unsafe decorative tails must actually stop painting');
      for(const tail of tails){assert.equal(tail.points.length,3);assert(regions.every(region=>!intersects(pointBounds(tail.points),region)),'The actual filled tail polygon must clear protected artwork');}
      assert.equal(JSON.stringify(state),before,'A render must preserve actor positions, orders, scores and gameplay state');
      assert.equal(target.height,558,'Order relocation cannot resize the venue camera');
      snapshots++;
    }
  }
  for(const {world} of worlds)world.destroy();
}
assert(movedInitialFreebird>=3,'The naturally occurring seed1721 initial Freebird problem must exercise protection');
console.log(`PASS: ${snapshots} natural order snapshots across 3 owners × 5 unchanged-camera viewports; seven-card clutter, identity/icon preservation, exact unaffected geometry, safe bodies/tails and gameplay state preservation.`);
