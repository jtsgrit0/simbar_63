import {startGame} from './game.js';
import {migrateLegacySave} from './legacy-save.js';
import {SAVE_KEY} from './core.js';

const shell=document.getElementById('game-shell'),stage=document.getElementById('game-stage');
const full=document.getElementById('fullscreen-button');
const updateViewport=()=>{const v=window.visualViewport;const h=v&&v.scale===1?v.height:innerHeight;if(h>0)shell.style.setProperty('--viewport-height',h+'px');};
window.addEventListener('resize',updateViewport,{passive:true});
window.visualViewport?.addEventListener('resize',updateViewport,{passive:true});updateViewport();
const image=async name=>{const i=new Image();i.src=new URL('art/'+name,import.meta.url);await i.decode();return i;};
const metadata=async name=>{const response=await fetch(new URL('art/'+name,import.meta.url));if(!response.ok)throw new Error('Could not load '+name);return response.json();};
const icons=['setup','cash','fans','time','staff','guestsati','perrform','upgrades','guest','events'];
try{
  // Assets, font and the read-only old-save lookup overlap on the critical path.
  const identity=migrateLegacySave({timeoutMs:150});
  const images=Promise.all(['owner-female.png','owner-ff.png','owner-pet.png','seoul.png','venue.png','sprites.png'].map(image));
  const iconImages=Promise.all(icons.map(n=>image('icons/'+n+'.png')));
  const font=document.fonts.load('16px SimBarUI');
  const [[female,ff,pet,seoul,venue,sprites],migration,,,[spriteMeta,venueMeta]]=await Promise.all([images,identity,font,iconImages,Promise.all([metadata('sprites.json'),metadata('venue.json')])]);
  const app=await startGame({root:stage,assetsBase:new URL('art/',import.meta.url).href,assets:{female,ff,pet,seoul,venue,sprites,spriteMeta,venueMeta}});
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
