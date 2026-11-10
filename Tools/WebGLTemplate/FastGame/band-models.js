import {catalog} from './catalog.js';

const FALLBACK_TRACKS=Object.freeze({track_001:'freewill',track_002:'julywantsmore',track_003:'julywantsmore',track_004:'freewill',track_005:'freewill'});
const memberSeed=id=>{let hash=2166136261;for(const c of String(id))hash=Math.imul(hash^c.charCodeAt(0),16777619);return hash>>>0;};
// These are authored performance styles, rather than a claim about a song's BPM.
export function bandPerformanceProfile(state={},bandId=resolveBandId(catalog.playlists[state.playlist])){
  if(!Number.isInteger(state.owner)||state.owner<0||state.owner>2)return{speed:1,energy:1,jumpIntensity:0,legacy:true};
  const venueEnergy=[.95,1.2,.65][state.owner],gentle=bandId==='julywantsmore',energy=venueEnergy*(gentle?.78:1);
  return{speed:energy,energy,jumpIntensity:gentle||state.owner===2?0:state.owner===1?1.2:.55,legacy:false};
}
export function resolveBandId(track,manifest={}){
  const id=track?.trackId||track?.id;
  const declared=manifest.tracks?.find(entry=>entry.id===id)?.band;
  if(declared)return declared;
  const artist=String(track?.artist||'').toLowerCase().replace(/[^a-z]/g,'');
  if(artist.includes('freewill'))return'freewill';
  if(artist.includes('julywantsmore'))return'julywantsmore';
  return FALLBACK_TRACKS[id]||null;
}

// The four simulation actors keep their saved choreography. Additional guitar
// players are visual companions and follow that same stage departure and exit.
export function performingRoster(state={},manifest={}){
  const bandId=resolveBandId(catalog.playlists[state.playlist],manifest);
  const band=manifest.bands?.find(entry=>entry.id===bandId);
  if(!band)return[];
  const profile=bandPerformanceProfile(state,bandId);
  return band.members.flatMap(member=>{
    const actor=state.actors?.find(entry=>entry.id===member.anchor&&entry.role==='band');
    const seed=memberSeed(member.id),variation=profile.legacy?{}:{performanceSpeed:profile.speed*(.96+(seed%11)/100),performancePhase:(seed%997)/997*3.1,performanceEnergy:profile.energy,jumpIntensity:profile.jumpIntensity};
    return actor&&actor.visible!==false?[{bandId,band,member,actor,id:member.id,role:member.role,row:member.row,extra:!!member.extra,...variation}]:[];
  });
}

export function performingAction(entry){
  const actor=entry.actor;
  if(actor.moving||['walk','exit'].includes(actor.action))return'walk';
  if(actor.action==='perform')return'perform';
  if(!entry.extra&&['highfive','hug'].includes(actor.action))return actor.action;
  return'idle';
}

export function performingFrame(entry,manifest,seconds=0){
  const action=performingAction(entry),definition=manifest.actions?.find(value=>value.id===action);
  const frames=definition?.frames?.length?definition.frames:[8];
  const performanceTime=action==='perform'?seconds*(Number.isFinite(entry.performanceSpeed)?entry.performanceSpeed:1)+(Number.isFinite(entry.performancePhase)?entry.performancePhase:0):seconds;
  const at=definition?.progress?Math.min(frames.length-1,Math.floor(Math.max(0,Math.min(1,+entry.actor.actionProgress||0))*frames.length))
    :definition?.fps>0?Math.floor(performanceTime*definition.fps+(entry.member.row||0))%frames.length:0;
  return{action,column:frames[at]};
}
