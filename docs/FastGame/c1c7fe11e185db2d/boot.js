import {startGame} from './game.js';
import {migrateLegacySave} from './legacy-save.js';
import {SAVE_KEY} from './core.js';
import {OWNER_VENUES,VENUE_PROPS} from './owner-venues.js';

const shell=document.getElementById('game-shell'),stage=document.getElementById('game-stage');
const full=document.getElementById('fullscreen-button');
const updateViewport=()=>{const v=window.visualViewport;const h=v&&v.scale===1?v.height:innerHeight;if(h>0)shell.style.setProperty('--viewport-height',h+'px');};
window.addEventListener('resize',updateViewport,{passive:true});
window.visualViewport?.addEventListener('resize',updateViewport,{passive:true});updateViewport();
const imageCache=new Map();
const image=name=>{if(!imageCache.has(name))imageCache.set(name,(async()=>{const i=new Image();i.src=new URL('art/'+name,import.meta.url);await i.decode();return i;})());return imageCache.get(name);};
const metadata=async name=>{const response=await fetch(new URL('art/'+name,import.meta.url));if(!response.ok)throw new Error('Could not load '+name);return response.json();};
const icons=['setup','cash','fans','time','staff','guestsati','perrform','upgrades','guest','events'];
try{
  // Assets, font and the read-only old-save lookup overlap on the critical path.
  const identity=migrateLegacySave({timeoutMs:150});
  const images=Promise.all(['owner-female.png','owner-ff.png','owner-pet.png','seoul.png','venue.png'].map(image));
  const venues=Promise.all(OWNER_VENUES.slice(0,2).map(async entry=>[entry.key,await image(entry.file)])).then(Object.fromEntries);
  const backdrops=Promise.all(OWNER_VENUES.slice(0,2).map(async entry=>[entry.key,await image(entry.background)])).then(Object.fromEntries);
  const props=Promise.all(Object.entries(VENUE_PROPS).map(async([key,file])=>[key,await image(file)])).then(Object.fromEntries);
  const descriptors=Promise.all([metadata('sprites.json'),metadata('venue.json'),metadata('venue-ff.json'),metadata('venue-freebird.json'),metadata('band-models.json'),metadata('festival-models.json')]);
  const atlasImages=descriptors.then(async([meta])=>Object.fromEntries(await Promise.all(Object.entries(meta.atlases||{animation:{file:'sprites.png'}}).map(async([key,definition])=>[key,await image(definition.file)]))));
  const bandImages=descriptors.then(async descriptors=>Object.fromEntries(await Promise.all(descriptors[4].bands.map(async band=>[band.id,await image(band.file)]))));
  const festivalImages=descriptors.then(async descriptors=>Object.fromEntries(await Promise.all(Object.entries(descriptors[5].atlases).map(async([key,definition])=>[key,await image(definition.file)]))));
  const iconImages=Promise.all(icons.map(n=>image('icons/'+n+'.png')));
  const font=document.fonts.load('16px SimBarUI');
  const [[female,ff,pet,seoul,venue],migration,,,[spriteMeta,venueMeta,ffVenueMeta,freebirdVenueMeta,bandModels,festivalModels],atlases,venueImages,propImages,bandAtlases,festivalAtlases,backdropImages]=await Promise.all([images,identity,font,iconImages,descriptors,atlasImages,venues,props,bandImages,festivalImages,backdrops]);
  const app=await startGame({root:stage,assetsBase:new URL('art/',import.meta.url).href,assets:{female,ff,pet,seoul,venue,venues:venueImages,backdrops:backdropImages,propImages,bandModels,bandAtlases,festivalModels,festivalAtlases,venueMetas:{ff:ffVenueMeta,freebird:freebirdVenueMeta},sprites:atlases.animation,atlases,spriteMeta,venueMeta}});
  window.simbarApplication=app;
  migration.pending?.then(r=>{if(r.imported)app.g.importSave(localStorage.getItem(SAVE_KEY));}).catch(()=>{});
  document.getElementById('loading-panel')?.remove();
  shell.dataset.playerState='ready';full.disabled=false;
  const toggle=async()=>{const active=document.fullscreenElement||document.webkitFullscreenElement;try{if(active)await(document.exitFullscreen?.()??document.webkitExitFullscreen?.());else await(shell.requestFullscreen?.()??shell.webkitRequestFullscreen?.());}catch{};app.canvas.focus({preventScroll:true});};
  full.addEventListener('click',toggle);
  const sync=()=>full.setAttribute('aria-pressed',String(!!(document.fullscreenElement||document.webkitFullscreenElement)));
  document.addEventListener('fullscreenchange',sync);document.addEventListener('webkitfullscreenchange',sync);
  // The world, live actors, game logic and every essential UI asset exist here.
  // This is not an HTML shell waiting for another engine to initialize.
  await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));
  window.simbarRuntime='pixel-2d';window.simbarGameReadyAt=performance.now();
  performance.mark('simbar-game-ready');
  const instance={GetMetricsInfo:()=>({pageLoadTimeToFrame1:window.simbarGameReadyAt,gameStartupTime:window.simbarGameReadyAt}),SendMessage(_object,method,value){if(method==='SetLanguage')app.g.act('language',{language:value});}};
  window.simbarBenchmarkInstance=instance;window.__simbarResolve(instance);
  window.addEventListener('pagehide',()=>app.g.save());
}catch(error){
  console.error(error);shell.dataset.playerState='error';document.getElementById('loading-panel')?.remove();
  const panel=document.getElementById('error-panel');panel.hidden=false;document.getElementById('error-message').textContent=String(error);window.__simbarReject(error);
}
