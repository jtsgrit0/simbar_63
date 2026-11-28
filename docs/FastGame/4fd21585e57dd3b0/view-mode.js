// Presentation only: the game clock, media, actors and camera remain owned by
// their existing controllers. A rebuilt HUD always adopts the requested mode.
export function createViewMode({host,hudRoot,referenceRoot,cssRoot,onchange=()=>{}}){
  const duration=360,easing='ease-out',view=host.ownerDocument.defaultView;
  let viewing=false,transitioning=false,destroyed=false,session=null,animations=[],generation=0,pending=null;
  const rect=element=>{const r=element.getBoundingClientRect();return{x:r.x,y:r.y,width:r.width,height:r.height};};
  const connected=element=>!!element?.isConnected&&host.contains(element);
  const reduced=()=>!!view.matchMedia?.('(prefers-reduced-motion: reduce)').matches;
  const mark=state=>{host.dataset.viewMode=viewing?'viewing':'management';host.dataset.viewModeTransition=state;};
  const notify=()=>{try{onchange(viewing);}catch{}};
  const settle=value=>{const resolve=pending;pending=null;resolve?.(value);};
  const cancel=()=>{generation++;for(const animation of animations)animation.cancel();animations=[];transitioning=false;};
  const remember=(state,element)=>{
    if(!state.saved.has(element))state.saved.set(element,{stylePresent:element.hasAttribute('style'),styles:new Map(),attrs:new Map()});
    return state.saved.get(element);
  };
  const style=(state,element,name,value)=>{
    const saved=remember(state,element);if(!saved.styles.has(name))saved.styles.set(name,[element.style.getPropertyValue(name),element.style.getPropertyPriority(name)]);
    // An !important translate would outrank the Web Animation on the return
    // trip. Layout pins are important; animated translations remain ordinary.
    element.style.setProperty(name,value,name==='translate'?'':'important');
  };
  const attr=(state,element,name,value)=>{
    const saved=remember(state,element);if(!saved.attrs.has(name))saved.attrs.set(name,element.getAttribute(name));
    if(value===null)element.removeAttribute(name);else element.setAttribute(name,value);
  };
  const mute=(state,element)=>{style(state,element,'pointer-events','none');attr(state,element,'inert','');attr(state,element,'aria-hidden','true');};
  const restore=()=>{
    const state=session;if(!state)return;
    for(const [element,saved]of state.saved){
      for(const [name,[value,priority]]of saved.styles)if(value)element.style.setProperty(name,value,priority);else element.style.removeProperty(name);
      for(const [name,value]of saved.attrs)if(value===null)element.removeAttribute(name);else element.setAttribute(name,value);
      if(!saved.stylePresent&&!element.style.cssText)element.removeAttribute('style');
    }
    if(state.placeholder.isConnected&&state.button.isConnected)state.placeholder.replaceWith(state.button);
    else state.placeholder.remove();
    state.overlay.remove();session=null;
  };
  const label=active=>{
    const button=session?.button||hudRoot.querySelector('[data-view-mode]');if(!button)return;
    const images=button.querySelectorAll('img.reference-button-image'),span=button.querySelector('[data-view-mode-label]');
    const text=active?(button.dataset.managementLabel||'경영모드'):(button.dataset.viewLabel||'감상모드');
    const source=active?button.dataset.managementSrc:button.dataset.viewSrc;if(source)for(const image of images)if(image.getAttribute('src')!==source)image.setAttribute('src',source);
    if(span)span.textContent=text;button.setAttribute('aria-label',text);button.setAttribute('title',text);button.setAttribute('aria-pressed',String(active));
  };
  const coordinates=(element,box)=>{
    const parent=element.offsetParent||hudRoot,p=parent.getBoundingClientRect(),sx=p.width/(parent.offsetWidth||p.width)||1,sy=p.height/(parent.offsetHeight||p.height)||1;
    return{left:(box.x-p.left)/sx,top:(box.y-p.top)/sy,width:box.width/sx,height:box.height/sy,sx,sy};
  };
  const translate=(element)=>{
    const value=view.getComputedStyle(element).translate;if(!value||value==='none')return[0,0];
    const parts=value.split(/\s+/),box=rect(element),number=(part,size)=>part?.endsWith('%')?parseFloat(part)*size/100:parseFloat(part)||0;
    return[number(parts[0],box.width),number(parts[1],box.height)];
  };
  const bind=()=>{
    const button=hudRoot.querySelector('[data-view-mode]'),card=hudRoot.querySelector('.live-console'),upgrade=hudRoot.querySelector('[data-reference-button="upgrades"],.venue-tools [data-action="ui:upgrades"]');
    if(!connected(button)||!connected(card)||!connected(upgrade))return null;
    const state={button,card,playlistTab:card.querySelector('[data-payload*="music"]'),saved:new Map(),targets:[],initialButton:rect(button),upgradeBox:rect(upgrade),cardBox:rect(card)};
    state.tabBox=state.playlistTab?rect(state.playlistTab):null;
    const placeholder=state.placeholder=host.ownerDocument.createElement('span');placeholder.setAttribute('aria-hidden','true');placeholder.setAttribute('inert','');placeholder.dataset.viewModePlaceholder='';
    const buttonStyle=view.getComputedStyle(button);Object.assign(placeholder.style,{display:'block',width:buttonStyle.width,height:buttonStyle.height,flex:'0 0 auto'});button.before(placeholder);
    const overlay=state.overlay=host.ownerDocument.createElement('nav');overlay.className='venue-tools view-mode-slot';overlay.dataset.viewModeOverlay='';hudRoot.append(overlay);overlay.append(button);
    Object.assign(overlay.style,{position:'absolute',right:'auto',bottom:'auto',margin:'0',padding:'0',gap:'0',zIndex:'20'});
    const position=coordinates(overlay,state.initialButton);Object.assign(overlay.style,{left:position.left+'px',top:position.top+'px',width:position.width+'px',height:position.height+'px'});
    state.modeDelta=[(state.upgradeBox.x-state.initialButton.x)/position.sx,(state.upgradeBox.y-state.initialButton.y)/position.sy];
    const parent=coordinates(card,state.cardBox);
    for(const [name,value]of Object.entries({position:'absolute',left:parent.left+'px',top:parent.top+'px',right:'auto',bottom:'auto',width:parent.width+'px',height:parent.height+'px','box-sizing':'border-box','max-width':'none','max-height':'none'}))style(state,card,name,value);
    for(const tab of card.querySelectorAll('.live-console-tabs button'))if(tab!==state.playlistTab){style(state,tab,'visibility','hidden');mute(state,tab);}
    const boundary=host.getBoundingClientRect(),seen=new Set();
    const add=(element,direction)=>{
      if(seen.has(element)||element===card||element===overlay||element===button)return;seen.add(element);
      const r=element.getBoundingClientRect(),visible=r.width>0&&r.height>0;
      const delta={up:[0,-(visible?r.bottom-boundary.top+24:boundary.height+24)],down:[0,visible?boundary.bottom-r.top+24:boundary.height+24],left:[-(visible?r.right-boundary.left+24:boundary.width+24),0],right:[visible?boundary.right-r.left+24:boundary.width+24,0]}[direction];
      const base=translate(element);state.targets.push({element,direction,base,off:[base[0]+delta[0],base[1]+delta[1]]});mute(state,element);remember(state,element);
    };
    for(const [selector,direction]of [['.hud-score,.hud-session','up'],['.quest-panel,.mood','right'],['.venue-show-badge,.venue-tools:not(.view-mode-slot),.ops-status','left'],['.menu-ready','down'],['.toast','up']])for(const element of hudRoot.querySelectorAll(selector))add(element,direction);
    for(const element of hudRoot.children)if(!element.classList.contains('venue-topbar'))add(element,'up');
    for(const root of [referenceRoot,cssRoot])if(root)for(const element of root.children)if(!element.matches('.settings-shade'))add(element,element.matches('.reaction-card,.floor-status')?'left':'down');
    state.targets.push({element:overlay,direction:'return-button',base:[0,0],off:state.modeDelta});
    session=state;label(true);return state;
  };
  const put=(state,active)=>{
    for(const target of state.targets){
      const value=active?target.off:target.base;style(state,target.element,'translate',value[0]+'px '+value[1]+'px');
      // Live bindings can grow after a rebuilt HUD was measured. Keep settled
      // outgoing UI hidden even when a later label exceeds that measurement.
      if(active&&target.element!==state.overlay)style(state,target.element,'visibility','hidden');
    }
  };
  const finish=(active,token)=>{
    if(token!==generation||destroyed)return;
    const state=session;if(state&&active)put(state,true);
    for(const animation of animations)animation.cancel();animations=[];transitioning=false;
    if(!active)restore();label(active);mark('settled');settle(true);
  };
  const animate=(active)=>{
    const state=session,token=++generation;transitioning=true;mark(active?'entering':'leaving');if(active)label(true);
    if(!active){
      const boundary=host.getBoundingClientRect();
      for(const target of state.targets){
        const original=state.saved.get(target.element)?.styles.get('visibility');if(!original)continue;
        // A label may have widened while hidden. Start its return fully beyond
        // the edge, then restore its original visibility before sliding in.
        const r=target.element.getBoundingClientRect(),position=translate(target.element);
        if(target.direction==='left')position[0]-=Math.max(0,r.right-boundary.left+24);
        if(target.direction==='right')position[0]+=Math.max(0,boundary.right-r.left+24);
        if(target.direction==='up')position[1]-=Math.max(0,r.bottom-boundary.top+24);
        if(target.direction==='down')position[1]+=Math.max(0,boundary.bottom-r.top+24);
        style(state,target.element,'translate',position[0]+'px '+position[1]+'px');
        if(original[0])target.element.style.setProperty('visibility',original[0],original[1]);else target.element.style.removeProperty('visibility');
      }
    }
    const starts=state.targets.map(target=>translate(target.element));for(const animation of animations)animation.cancel();animations=[];
    for(const [index,target]of state.targets.entries()){
      // Reserve the property before animations so every exit/destroy restores
      // its original inline value without undoing unrelated live UI updates.
      const saved=remember(state,target.element);if(!saved.styles.has('translate'))saved.styles.set('translate',[target.element.style.getPropertyValue('translate'),target.element.style.getPropertyPriority('translate')]);
      const end=active?target.off:target.base;
      if(reduced()||!target.element.animate){style(state,target.element,'translate',end[0]+'px '+end[1]+'px');continue;}
      const start=starts[index],animation=target.element.animate([{translate:start[0]+'px '+start[1]+'px'},{translate:end[0]+'px '+end[1]+'px'}],{duration,easing,fill:'forwards'});animations.push(animation);
    }
    if(!animations.length){finish(active,token);return;}
    Promise.all(animations.map(animation=>animation.finished.catch(()=>{}))).then(()=>finish(active,token));
  };
  const rebind=()=>{
    if(destroyed)return;cancel();restore();
    if(viewing){const state=bind();if(state)put(state,true);label(true);}
    mark('settled');settle(!viewing||!!session);
  };
  const setViewing=value=>{
    if(destroyed)return Promise.resolve(false);value=!!value;
    if(value===viewing){if(transitioning)return new Promise(resolve=>{const old=pending;pending=result=>{old?.(result);resolve(result);};});return Promise.resolve(!value||!!session);}
    settle(false);viewing=value;
    const promise=new Promise(resolve=>{pending=resolve;});
    if(!session&&value)bind();
    if(session)animate(value);else{mark('settled');settle(!value);}
    notify();return promise;
  };
  const reset=()=>{if(destroyed)return;const changed=viewing;viewing=false;cancel();restore();label(false);mark('settled');settle(true);if(changed)notify();};
  const snapshot=()=>({viewing,active:!!session||transitioning,transitioning,duration,easing,card:session&&connected(session.card)?rect(session.card):null,cardBefore:session?.cardBox||null,playlistTab:session?.playlistTab&&connected(session.playlistTab)?rect(session.playlistTab):null,playlistTabBefore:session?.tabBox||null,button:session&&connected(session.button)?rect(session.button):null,initialButton:session?.initialButton||null,upgrade:session?.upgradeBox||null,targets:session?.targets.map(target=>({direction:target.direction,connected:connected(target.element),inert:target.element.hasAttribute('inert'),box:rect(target.element)}))||[]});
  mark('settled');
  return{get viewing(){return viewing;},get active(){return!!session||transitioning;},get transitioning(){return transitioning;},setViewing,onHUDRendered:rebind,resize:rebind,reset,snapshot,destroy(){if(destroyed)return;cancel();restore();viewing=false;label(false);settle(false);delete host.dataset.viewMode;delete host.dataset.viewModeTransition;destroyed=true;}};
}
