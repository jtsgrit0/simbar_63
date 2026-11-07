// Read-only migration of this game's Unity 6 WebGL PlayerPrefs. The original
// /idbfs database is left intact. A native save always wins over legacy data.
import {SAVE_KEY} from './core.js';
const encoder=new TextEncoder(),decoder=new TextDecoder();
export function md5(text){
  const input=encoder.encode(text),size=((input.length+8>>>6)+1)*64,bytes=new Uint8Array(size);bytes.set(input);bytes[input.length]=128;
  const view=new DataView(bytes.buffer);view.setUint32(size-8,input.length*8,true);view.setUint32(size-4,Math.floor(input.length/536870912),true);
  let a0=0x67452301,b0=0xefcdab89,c0=0x98badcfe,d0=0x10325476;
  const shifts=[[7,12,17,22],[5,9,14,20],[4,11,16,23],[6,10,15,21]];
  for(let offset=0;offset<size;offset+=64){let a=a0,b=b0,c=c0,d=d0;
    for(let i=0;i<64;i++){
      let f,g;if(i<16){f=(b&c)|(~b&d);g=i;}else if(i<32){f=(d&b)|(~d&c);g=(5*i+1)%16;}else if(i<48){f=b^c^d;g=(3*i+5)%16;}else{f=c^(b|~d);g=7*i%16;}
      const k=Math.floor(Math.abs(Math.sin(i+1))*4294967296),x=(a+f+k+view.getUint32(offset+g*4,true))>>>0,s=shifts[Math.floor(i/16)][i%4],next=(b+((x<<s)|(x>>>(32-s))))>>>0;
      a=d;d=c;c=b;b=next;
    }a0=(a0+a)>>>0;b0=(b0+b)>>>0;c0=(c0+c)>>>0;d0=(d0+d)>>>0;
  }
  return[a0,b0,c0,d0].map(x=>[0,8,16,24].map(s=>((x>>>s)&255).toString(16).padStart(2,'0')).join('')).join('');
}
export function unityPreferencePath(url){const base=new URL('.',url).href.replace(/\/$/,'');return'/idbfs/'+md5(base)+'/PlayerPrefs';}
export function parseUnityPrefs(input){
  const bytes=input instanceof Uint8Array?input:new Uint8Array(input||[]),out={};
  if(bytes.length<16||decoder.decode(bytes.subarray(0,8))!=='UnityPrf')return out;
  const view=new DataView(bytes.buffer,bytes.byteOffset,bytes.byteLength);
  for(const key of [SAVE_KEY,'SimBar.Owner','SimBar.Language']){
    const pattern=encoder.encode(key);
    for(let at=8;at+pattern.length+1<=bytes.length;at++){
      if(bytes[at-1]!==pattern.length)continue;
      let match=true;for(let i=0;i<pattern.length;i++)if(bytes[at+i]!==pattern[i]){match=false;break;}if(!match)continue;
      const end=at+pattern.length,marker=bytes[end];let length=marker,payload=end+1;
      // Unity's string length is one byte below128; 0x80 introduces a uint32
      // length for larger strings (the operations JSON). It is not a type tag.
      if(marker===128){if(end+5>bytes.length)continue;length=view.getUint32(end+1,true);payload=end+5;}else if(marker>128)continue;
      if(length>1_000_000||payload+length>bytes.length)continue;out[key]=decoder.decode(bytes.subarray(payload,payload+length));break;
    }
  }return out;
}
async function readPreferences(idb,url){
  if(typeof idb.databases==='function'){const names=await idb.databases();if(!names.some(x=>x.name==='/idbfs'))return null;}
  const db=await new Promise((resolve,reject)=>{const r=idb.open('/idbfs');r.onupgradeneeded=()=>{r.transaction.abort();reject(new Error('No existing Unity preferences'));};r.onsuccess=()=>resolve(r.result);r.onerror=()=>reject(r.error);});
  try{
    if(!db.objectStoreNames.contains('FILE_DATA'))return null;
    const record=await new Promise((resolve,reject)=>{const tx=db.transaction('FILE_DATA','readonly'),request=tx.objectStore('FILE_DATA').get(unityPreferencePath(url));request.onsuccess=()=>resolve(request.result);request.onerror=()=>reject(request.error);});
    return record?.contents?parseUnityPrefs(record.contents):null;
  }finally{db.close();}
}
export async function migrateLegacySave(options={}){
  let storage;try{storage=options.storage??globalThis.localStorage;}catch{return{imported:false,reason:'storage-unavailable'};}
  const idb=options.indexedDB??globalThis.indexedDB,url=options.url??globalThis.location?.href;
  if(!storage||!idb||!url)return{imported:false,reason:'unavailable'};
  try{
    if(storage.getItem(SAVE_KEY))return{imported:false,reason:'native-save-exists'};
    let timer,late=false;
    const pending=readPreferences(idb,url).then(prefs=>{
      if(!prefs?.[SAVE_KEY])return{imported:false,reason:'no-legacy-save'};
      const raw=prefs[SAVE_KEY],data=JSON.parse(raw);if(data.version!==1||!Number.isFinite(data.money)||data.money<0||typeof data.journeyJson!=='string')return{imported:false,reason:'invalid-legacy-save'};
      JSON.parse(data.journeyJson||'{}');
      // The first native user action saves this key. Never replace new progress,
      // including a save made while this old IndexedDB read was still pending.
      if(storage.getItem(SAVE_KEY))return{imported:false,reason:'native-save-exists'};
      storage.setItem('SimBar.LegacyUnity.v1',raw);storage.setItem(SAVE_KEY,raw);
      if(['female','ff','pet-sounds'].includes(prefs['SimBar.Owner'])&&!storage.getItem('SimBar.Owner'))storage.setItem('SimBar.Owner',prefs['SimBar.Owner']);
      if(['ko','en'].includes(prefs['SimBar.Language'])&&!storage.getItem('SimBar.Language'))storage.setItem('SimBar.Language',prefs['SimBar.Language']);
      const result={imported:true,reason:'imported',owner:prefs['SimBar.Owner'],language:prefs['SimBar.Language']};
      if(late)options.onLateImport?.(result);return result;
    }).catch(()=>({imported:false,reason:'unavailable'}));
    const result=await Promise.race([pending,new Promise(resolve=>{timer=setTimeout(()=>{late=true;resolve({imported:false,reason:'pending'});},options.timeoutMs??150);})]);clearTimeout(timer);
    return result.reason==='pending'?{...result,pending}:result;
  }catch{return{imported:false,reason:'unavailable'};}
}
