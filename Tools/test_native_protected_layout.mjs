import assert from 'node:assert/strict';
import {planVenueUI,rectIntersects,validateVenueUI} from './WebGLTemplate/FastGame/protected-layout.js';

// Author-measured regions include the whole stage, not just its floor deck.
// Transform them using the retained 960×558 height-fit camera, with no UI rail.
const authored=[
  [{kind:'stage',box:[500,74,950,412]}],
  [{kind:'stage',box:[564,77,947,400]}],
  [{kind:'stage',box:[549,76,917,395]},{kind:'sign',box:[367,36,552,134]}]
];
const viewports=[[667,375],[844,390],[915,412],[1660,624],[1600,900]];
const regionsFor=(owner,width,height)=>authored[owner].map(({kind,box:[x0,y0,x1,y1]})=>{
  const scale=height/558,offset=(width-960*scale)/2;
  return{kind,left:offset+x0*scale,top:y0*scale,right:offset+x1*scale,bottom:y1*scale};
});
const keys=['settings','score','session','quest','live','menu','mood','tools','badge','toast','window'];
let plans=0;
for(const owner of [0,1,2])for(const [width,height] of viewports){
  const regions=regionsFor(owner,width,height),before=JSON.stringify(regions);
  const scenarios=[{}, {questOpen:true}, {liveOpen:true}, {questOpen:true,liveOpen:true}, {hasOverlay:true,questOpen:true,liveOpen:true}];
  for(const flags of scenarios)for(const showMessage of [false,true]){
    const input={width,height,regions,...flags,showMessage},plan=planVenueUI(input);
    assert.deepEqual(planVenueUI(input),plan,'Identical screen state must produce a deterministic placement');
    assert.deepEqual(Object.keys(plan.rects).sort(),[...keys].sort());
    assert.deepEqual(validateVenueUI(plan,{regions,padding:4}),[],JSON.stringify({owner,width,height,flags,plan}));
    assert.equal(JSON.stringify(regions),before,'Planning UI cannot change the camera-space protected artwork');
    const window=plan.rects.window,stage=regions.find(region=>region.kind==='stage'),sign=regions.find(region=>region.kind==='sign');
    assert(window.width>=240&&window.height>=180,'All existing dialog content needs a usable scrolling window');
    assert(window.right<=stage.left-8+1e-8,'Prefer the floor left of the entire stage');
    assert(window.top>=plan.rects.toast.bottom+8-1e-8,'Every dialog reserves the notification space even when no message is shown');
    if(sign)assert(window.top>=sign.bottom+8-1e-8,'No dialog may cover the PetSounds sign');
    assert.equal(plan.rects.toast.hidden,!showMessage,'Inspection messages must remain visible during focused cards and overlays');
    if(showMessage)assert.equal(plan.rects.badge.hidden,true,'A message takes priority over the optional venue badge');
    const withoutMessage=planVenueUI({...input,showMessage:false});
    assert.deepEqual(plan.rects.window,withoutMessage.rects.window,'Message appearance and expiry cannot move an open dialog');
    for(const focused of ['quest','live'])if(plan.focus===focused)assert.deepEqual(plan.rects[focused],withoutMessage.rects[focused]);
    const visible=Object.entries(plan.rects).filter(([,rect])=>!rect.hidden).map(([key])=>key);
    if(flags.hasOverlay){
      assert.equal(plan.focus,null);assert.deepEqual(visible.sort(),(showMessage?['toast','window']:['window']).sort());
    }else if(plan.compact&&(flags.questOpen||flags.liveOpen)){
      const expected=flags.questOpen?'quest':'live';assert.equal(plan.focus,expected);assert.deepEqual(visible.sort(),(showMessage?[expected,'toast']:[expected]).sort());
      assert.equal(plan.rects[expected].left,window.left);assert.equal(plan.rects[expected].top,window.top);
      assert.equal(plan.rects[expected].width,window.width);assert.equal(plan.rects[expected].height,window.height);
      assert.equal(window.hidden,true,'The window geometry must not duplicate the visible focused panel');
    }else{
      assert.equal(plan.focus,null);assert.equal(window.hidden,true);
      assert(visible.includes('tools')&&visible.includes('menu')&&visible.includes('mood'));
      assert(plan.rects.settings.width>=40&&plan.rects.settings.height>=40);
      assert(plan.rects.quest.height>=38,'Collapsed quest header must remain touchable');
      assert(plan.rects.tools.height>=64,'Upgrade/event/mission buttons share the same square size');
      assert(plan.rects.menu.height>=64&&plan.rects.mood.height>=64);
      if(plan.compact)assert(plan.rects.live.height>=46,'Two 36px live tabs need their padding and border');
      else assert(plan.rects.live.height>=260&&plan.rects.live.width>=226,'Wide reference can keep the complete right-hand card');
    }
    if(width===1660)assert.equal(plan.compact,false,'The wide reference composition has safe city space for the full card');
    if(width===667)assert.equal(plan.compact,true,'A narrow landscape must fold the card rather than shrink the room');
    plans++;
  }
}
assert.equal(rectIntersects({left:0,top:0,right:10,bottom:10},{left:10,top:0,right:20,bottom:10}),false,'Touching borders do not visually overlap');
assert.equal(rectIntersects({left:0,top:0,width:10,height:10},{left:11,top:0,width:10,height:10},2),true,'A requested clearance must be enforced');
assert.throws(()=>planVenueUI({width:0,height:375}),TypeError);
assert.throws(()=>planVenueUI({width:300,height:200,regions:[{kind:'stage',left:50,top:10,right:290,bottom:190}]}),RangeError,'An impossible window must not silently resize artwork or remove controls');
const bad={width:100,height:100,rects:{a:{left:10,top:10,right:30,bottom:30},b:{left:20,top:20,right:40,bottom:40}}};
assert(validateVenueUI(bad).some(error=>error.includes('overlaps')),'The validator must catch real overlapping controls');
console.log(`PASS: ${plans} deterministic plans across 3 owners × 5 original-scale landscapes, protected sign/stage clearance, disjoint HUD controls, readable compact launchers, stable dialog geometry and visible messages in every focus/overlay state.`);
