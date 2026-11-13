/* Screen-space UI placement only. The venue camera and art are never resized,
 * moved, clipped or replaced here. All regions arrive in CSS pixels. */
const clamp=(value,min,max)=>Math.max(min,Math.min(max,value));
const edge=(value,key,fallback)=>Number.isFinite(value?.[key])?value[key]:fallback;
const bounds=value=>{
  const left=edge(value,'left',0),top=edge(value,'top',0);
  return{left,top,right:edge(value,'right',left+edge(value,'width',0)),bottom:edge(value,'bottom',top+edge(value,'height',0))};
};
const rectangle=(left,top,width,height,hidden=false)=>({left,top,width,height,right:left+width,bottom:top+height,hidden});
const area=value=>(value.right-value.left)*(value.bottom-value.top);

// Two unscaled 292px cards, their 8px gap, the collapsed quest and the
// existing bottom tiles must all fit before the reference presentation is used.
export function canUseReferenceCards(width,height){
  const tiles=clamp(height*.23,78,140);
  return width>=height&&height-tiles>=666;
}

export function rectIntersects(first,second,padding=0){
  const a=bounds(first),b=bounds(second);
  return a.left<b.right+padding&&a.right>b.left-padding&&a.top<b.bottom+padding&&a.bottom>b.top-padding;
}

function freeRectangles(container,obstacles,padding){
  let spaces=[bounds(container)];
  for(const obstacle of obstacles){
    const b=bounds(obstacle),cut={left:b.left-padding,top:b.top-padding,right:b.right+padding,bottom:b.bottom+padding};
    spaces=spaces.flatMap(space=>{
      if(!rectIntersects(space,cut))return[space];
      return[
        {...space,bottom:Math.min(space.bottom,cut.top)},
        {...space,top:Math.max(space.top,cut.bottom)},
        {...space,right:Math.min(space.right,cut.left)},
        {...space,left:Math.max(space.left,cut.right)}
      ].filter(piece=>piece.right>piece.left&&piece.bottom>piece.top);
    });
    spaces=spaces.filter((space,index)=>!spaces.some((other,at)=>at!==index&&other.left<=space.left&&other.top<=space.top&&other.right>=space.right&&other.bottom>=space.bottom&&(area(other)>area(space)||at<index)));
  }
  return spaces;
}

/** All named rects are returned even while hidden. In compact focus mode the
 * focused quest/live rect occupies window; window itself stays hidden. With an
 * ordinary overlay, window is the sole visible panel. The reserved notification
 * can remain visible above either kind of panel when showMessage is true; its
 * space is reserved even when empty, so an expiring message never moves a menu.
 * There is one content window: callers prioritize competing dialogs or contain
 * related controls together inside it, rather than painting parallel popups.
 * No full-screen scrim is part of this plan. A viewport that cannot fit required controls fails loudly
 * rather than silently shrinking the game or deleting a feature. */
export function planVenueUI({width,height,regions=[],questOpen=false,liveOpen=false,hasOverlay=false,showMessage=false,referenceCards=false}={}){
  if(!(Number.isFinite(width)&&Number.isFinite(height)&&width>0&&height>0))throw new TypeError('Venue UI needs positive CSS viewport dimensions.');
  const protectedRegions=regions.map(region=>({...bounds(region),kind:region.kind}));
  if(protectedRegions.some(region=>region.right<=region.left||region.bottom<=region.top))throw new TypeError('Protected regions need positive area.');
  const stage=protectedRegions.filter(region=>region.kind==='stage'),signs=protectedRegions.filter(region=>region.kind==='sign');
  const padding=8,gap=6,rects={},placed=[];
  const inside=value=>value.left>=4&&value.top>=2&&value.right<=width-4&&value.bottom<=height-4;
  const clear=(value,others=placed)=>inside(value)&&![...protectedRegions,...others].some(obstacle=>rectIntersects(value,obstacle,4));
  const place=(name,left,top,w,h,optional=false)=>{
    let result=rectangle(left,top,w,h);
    if(!clear(result)){
      const spaces=freeRectangles({left:4,top:2,right:width-4,bottom:height-4},[...protectedRegions,...placed],4);
      const candidates=spaces.filter(space=>space.right-space.left>=w&&space.bottom-space.top>=h).map(space=>rectangle(clamp(left,space.left,space.right-w),clamp(top,space.top,space.bottom-h),w,h));
      candidates.sort((a,b)=>Math.hypot(a.left-left,a.top-top)-Math.hypot(b.left-left,b.top-top)||a.top-b.top||a.left-b.left);
      result=candidates.find(candidate=>clear(candidate));
      if(!result){
        if(optional){result=rectangle(left,top,w,h,true);rects[name]=result;return result;}
        throw new RangeError(`Cannot fit ${name} outside protected venue areas at ${width}×${height}.`);
      }
    }
    rects[name]=result;placed.push(result);return result;
  };
  // Reference PNGs retain their native pixels; only their safe screen placement changes.
  const settingsWidth=49,settingsHeight=45,scoreHeight=clamp(height*.1,40,58),scoreWidth=clamp(width*.21,172,340);
  const toolWidth=84,toolHeight=88,tileWidth=clamp(width*.075,70,120);
  const stageBottom=stage.length?Math.max(...stage.map(region=>region.bottom)):0;
  const tileHeight=Math.max(64,Math.min(clamp(height*.23,78,140),height-padding-stageBottom-padding));
  const tileTop=height-padding-tileHeight,toolsTop=height-padding-toolHeight;
  const rightCity=stage.length?width-padding-Math.max(...stage.map(region=>region.right))-padding:width*.3;
  const cardWidth=Math.min(referenceCards?clamp(width*.151,226,260):260,rightCity),expandedQuestHeight=clamp(height*.24,120,164),liveHeight=referenceCards?292:260;
  const cardLeft=width-padding-cardWidth,liveBottom=tileTop-padding;
  const fullQuest=rectangle(cardLeft,padding,cardWidth,expandedQuestHeight),fullLive=rectangle(cardLeft,liveBottom-liveHeight,cardWidth,liveHeight);
  const compact=cardWidth<226||fullLive.top<fullQuest.bottom+padding||!clear(fullQuest,[])||!clear(fullLive,[]);
  const focus=hasOverlay?null:compact?(questOpen?'quest':liveOpen?'live':null):null;

  const settings=place('settings',padding,padding,settingsWidth,settingsHeight);
  const score=place('score',settings.right+padding,padding,scoreWidth,scoreHeight);
  const questWidth=compact?clamp(width*.2,128,220):cardWidth;
  const quest=place('quest',width-padding-questWidth,compact?4:padding,questWidth,compact?40:questOpen?expandedQuestHeight:42);
  const sessionLeft=score.right+padding,sessionRight=quest.left-padding,sessionWidth=Math.min(520,sessionRight-sessionLeft);
  if(sessionWidth<120)throw new RangeError(`Cannot retain the session information at ${width}×${height}.`);
  const earliestTop=protectedRegions.length?Math.min(...protectedRegions.map(region=>region.top)):height;
  const sessionHeight=Math.max(18,Math.min(height<500?22:26,earliestTop-6));
  place('session',clamp((width-sessionWidth)/2,sessionLeft,sessionRight-sessionWidth),2,sessionWidth,sessionHeight);
  place('tools',padding,toolsTop,3*toolWidth+2*gap,toolHeight);
  const tileLeft=width-padding-3*tileWidth-2*gap;
  place('menu',tileLeft,tileTop,tileWidth,tileHeight);
  place('mood',tileLeft+tileWidth+gap,tileTop,2*tileWidth+gap,tileHeight);
  place('live',compact?padding:cardLeft,compact?toolsTop-padding-46:fullLive.top,compact?226:cardWidth,compact?46:liveHeight);
  const leftBoundary=Math.min(width-padding,...protectedRegions.map(region=>region.left));
  const toastWidth=Math.min(320,leftBoundary-2*padding);
  if(toastWidth<160)throw new RangeError(`Cannot retain a readable notification outside the venue at ${width}×${height}.`);
  const toast=place('toast',padding,Math.max(settings.bottom,score.bottom)+padding,toastWidth,60);
  const badge=place('badge',padding,toast.bottom+padding,Math.min(240,toastWidth),18,true);
  toast.hidden=!showMessage;
  if(showMessage)badge.hidden=true;

  const signBottom=signs.length?Math.max(...signs.map(region=>region.bottom)):0;
  const leftStage=stage.length?Math.min(...stage.map(region=>region.left)):width;
  const windowTop=Math.max(settings.bottom,score.bottom,sessionHeight+2,signBottom,toast.bottom)+padding;
  let windowRect=rectangle(padding,windowTop,Math.min(620,leftStage-2*padding),height-padding-windowTop,true);
  if(windowRect.width<240||windowRect.height<180||!clear(windowRect,[])){
    const spaces=freeRectangles({left:padding,top:windowTop,right:width-padding,bottom:height-padding},[...protectedRegions,toast],padding);
    const candidates=spaces.filter(space=>space.right-space.left>=240&&space.bottom-space.top>=180).map(space=>rectangle(space.left,space.top,Math.min(620,space.right-space.left),space.bottom-space.top,true));
    candidates.sort((a,b)=>area(b)-area(a)||a.left-b.left||a.top-b.top);
    windowRect=candidates[0];
    if(!windowRect)throw new RangeError(`Cannot fit a 240×180 venue window without covering protected art at ${width}×${height}.`);
  }
  rects.window=windowRect;
  if(hasOverlay||focus){
    for(const value of Object.values(rects))value.hidden=true;
    if(hasOverlay)rects.window.hidden=false;
    else rects[focus]={...windowRect,hidden:false};
    rects.toast.hidden=!showMessage;
  }
  return{rects,compact,focus,width,height};
}

/** Empty results mean all visible boxes fit, avoid protected regions, and avoid
 * each other. Hidden geometry is intentionally ignored. */
export function validateVenueUI(plan,{width=plan.width,height=plan.height,regions=[],padding=0}={}){
  const errors=[],visible=Object.entries(plan.rects).filter(([,value])=>!value.hidden);
  for(const [name,value] of visible){
    if(value.left<0||value.top<0||value.right>width||value.bottom>height)errors.push(`${name}: outside viewport`);
    for(const region of regions)if(rectIntersects(value,region,padding))errors.push(`${name}: covers ${region.kind||'protected area'}`);
  }
  for(let index=0;index<visible.length;index++)for(let next=index+1;next<visible.length;next++)if(rectIntersects(visible[index][1],visible[next][1],padding))errors.push(`${visible[index][0]}: overlaps ${visible[next][0]}`);
  return errors;
}
