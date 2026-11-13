import assert from 'node:assert/strict';
import {createWorld,pixelPresentationGeometry} from './WebGLTemplate/FastGame/world.js';

const clamp=(value,min,max)=>Math.max(min,Math.min(max,value));
const owners=[[17,960],[27,960],[28,932]];
const views=[[1660,624],[1600,900],[1366,900],[1024,768],[844,390],[915,412],[667,375]];
const frameFor=(width,height,[left,right])=>({left:height>=900?24:4,right:height>=900?width-8-clamp(width*.151,226,260)-16:width-4,bounds:{left,right}});
const screen=(g,w,h,x,y)=>({x:(x+g.offsetX)*w/g.width,y:(y+g.offsetY)*h/g.height});
let checks=0;
for(const [width,height]of views){
  const base={width:clamp(Math.round(558*width/height),160,2304),height:558};
  base.offsetX=(base.width-960)/2;base.offsetY=0;
  assert.deepEqual(pixelPresentationGeometry(width,height),base,'No callback retains the original camera exactly');
  assert.deepEqual(pixelPresentationGeometry(width,height,null),base);
  for(const bounds of owners){
    const frame=frameFor(width,height,bounds),actual=pixelPresentationGeometry(width,height,frame);
    const left=screen(actual,width,height,bounds[0],0),right=screen(actual,width,height,bounds[1],558);
    // Public94 rounds the internal raster dimensions; their screen-space error stays below one CSS pixel.
    assert(left.x>=frame.left-1&&right.x<=frame.right+1,JSON.stringify({width,height,bounds,frame,actual,left,right}));
    assert(left.y>=0&&right.y<=height+1e-9,'Fitting may not clip the top or bottom');
    assert(actual.height>=558,'The frame can only zoom out');
    const oldLeft=screen(base,width,height,bounds[0],0).x,oldRight=screen(base,width,height,bounds[1],558).x;
    if(oldLeft>=frame.left&&oldRight<=frame.right)assert.deepEqual(actual,base,'Already-contained artwork must not move');
    checks++;
  }
}
for(const [width,height]of [[1600,900],[1366,900]]){
  const frame=frameFor(width,height,owners[2]),roomWidth=904*height/558,available=frame.right-frame.left,fit=Math.min(1,available/roomWidth);
  const rasterHeight=Math.round(558/fit),rasterWidth=Math.round(rasterHeight*width/height);
  assert.deepEqual(pixelPresentationGeometry(width,height,frame),{width:rasterWidth,height:rasterHeight,offsetX:(frame.right-Math.min(roomWidth,available)/2)*rasterWidth/width-480,offsetY:(rasterHeight-558)/2},'Jaiden PC uses the approved public94 formula exactly');
}
assert.deepEqual(pixelPresentationGeometry(844,390,frameFor(844,390,owners[2])),pixelPresentationGeometry(844,390),'A short viewport without clipping reserves no right rail');
assert.throws(()=>pixelPresentationGeometry(1600,900,{left:24,right:24,bounds:{left:28,right:932}}),TypeError);

// Construction must never evaluate a state closure whose application variables may still be in the TDZ.
let calls=0;const frame=frameFor(1600,900,owners[2]),ctx={},canvas={width:0,height:0,style:{},getContext:()=>ctx,getBoundingClientRect:()=>({width:1600,height:900})};
globalThis.ResizeObserver=class{observe(){}disconnect(){}};
const world=createWorld(canvas,{assets:{venue:{width:960,height:558},spriteMeta:{roles:{guest:{}},atlases:{}}},getState(){throw new Error('Game state read during construction');},getPresentationFrame(view){assert.deepEqual(view,{width:1600,height:900});calls++;return frame;}});
assert.equal(calls,1);assert.equal(canvas.height,pixelPresentationGeometry(1600,900,frame).height);world.destroy();
console.log(`PASS: ${checks} owner/view presentation frames, unchanged default geometry, exact public94 Jaiden PC formula and constructor state isolation.`);
