/* Screen-space presentation only. Game rules, camera, visibility and popup
 * priority remain with the caller. An infeasible card is reported, never moved
 * into the venue or silently substituted with a different layout. */
export const POPUP_CARD_GAP=8;
export const POPUP_CARD_REFERENCE_HEIGHT=292;

const number=(value,fallback=0)=>Number.isFinite(value)?value:fallback;
function rectangle(value){
  const left=number(value?.left),top=number(value?.top);
  const width=number(value?.width,number(value?.right)-left);
  const height=number(value?.height,number(value?.bottom)-top);
  return{left,top,width,height,right:left+width,bottom:top+height};
}
function intersects(a,b,padding=0){
  return a.left<b.right+padding&&a.right>b.left-padding&&a.top<b.bottom+padding&&a.bottom>b.top-padding;
}

/** Propose a popup exactly as large as liveRect and exactly gap pixels above
 * it. regions are protected art; obstacles are other visible HUD rectangles.
 * The caller can retain its existing safe-window presentation when fits=false.
 * A short, expanded card uses its existing inner scrolling, not smaller button
 * padding or a second set of controls. Collapsed live headers are not cards. */
export function planPopupCard({width,height,liveRect,regions=[],obstacles=[],gap=POPUP_CARD_GAP,padding=4,minimumWidth=226,minimumHeight=180}={}){
  if(!(Number.isFinite(width)&&width>0&&Number.isFinite(height)&&height>0))throw new TypeError('Popup cards need positive CSS viewport dimensions.');
  if(!(Number.isFinite(gap)&&gap>=0&&Number.isFinite(padding)&&padding>=0))throw new TypeError('Popup card spacing must be nonnegative.');
  const live=rectangle(liveRect);
  if(!(live.width>0&&live.height>0))throw new TypeError('Popup cards need a positive live-card rectangle.');
  const rect=rectangle({left:live.left,top:live.top-gap-live.height,width:live.width,height:live.height});
  const errors=[];
  if(liveRect.hidden)errors.push({code:'live-hidden'});
  if(live.width<minimumWidth||live.height<minimumHeight)errors.push({code:'live-collapsed-or-too-small',minimumWidth,minimumHeight});
  if(rect.left<padding||rect.top<padding||rect.right>width-padding||rect.bottom>height-padding)errors.push({code:'outside-viewport'});
  for(const region of regions){const area=rectangle(region);if(intersects(rect,area,padding))errors.push({code:'protected-region',kind:region.kind||'protected'});}
  for(const [index,obstacle]of obstacles.entries()){
    if(obstacle.hidden)continue;
    if(intersects(rect,rectangle(obstacle),padding))errors.push({code:'hud-overlap',name:obstacle.name||obstacle.kind||String(index)});
  }
  return{fits:errors.length===0,rect,live,gap,errors,availableAbove:Math.max(0,live.top-padding),requiredAbove:live.height+gap,contentWillScroll:live.height<POPUP_CARD_REFERENCE_HEIGHT};
}

/** Apply to the existing .venue-window wrapper after the caller has selected
 * its existing order/reaction content. No elements, labels or handlers change. */
export function applyPopupCardPlacement(node,plan,{kind}={}){
  if(!node)return false;
  // Keep the caller's existing simultaneous pour/mission window presentation.
  if(!plan?.fits||node.childElementCount>1){clearPopupCardPlacement(node);return false;}
  if(kind!=='order'&&kind!=='reaction')throw new TypeError('Popup card kind must be order or reaction.');
  const rect=plan.rect;
  node.dataset.popupCard=kind;
  for(const name of ['left','top','width','height'])node.style[name]=rect[name]+'px';
  node.style.right='auto';node.style.bottom='auto';node.style.transform='none';
  return true;
}

/** Clear presentation ownership before the caller applies its normal existing
 * safe-window rectangle. It intentionally does not hide or resume anything. */
export function clearPopupCardPlacement(node){
  if(!node)return;
  delete node.dataset.popupCard;
}
