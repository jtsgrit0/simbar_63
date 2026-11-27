'use strict';
const {app,BrowserWindow,net,protocol,session,shell}=require('electron');
const ipc=require('node:net');
const fs=require('node:fs');
const path=require('node:path');
const {pathToFileURL}=require('node:url');

const origin='simbar://app';
const root=process.env.SIMBAR_PREVIEW_GAME;
const pipe=process.env.SIMBAR_PREVIEW_PIPE;
const profile=process.env.SIMBAR_PREVIEW_PROFILE;
if(!root||!profile||!pipe||!path.isAbsolute(root)||!path.isAbsolute(profile))throw new Error('Missing Unity preview configuration.');
app.setName('SimBar Unity Preview');
fs.mkdirSync(profile,{recursive:true});
app.setPath('userData',profile);
app.setPath('sessionData',profile);
protocol.registerSchemesAsPrivileged([{scheme:'simbar',privileges:{standard:true,secure:true,supportFetchAPI:true,corsEnabled:true,stream:true}}]);
const csp="default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob: https:; media-src 'self' blob: https:; connect-src 'self' https:; font-src 'self'; object-src 'none'; base-uri 'self'; frame-src 'none'; form-action 'none'";
const peer=ipc.createConnection(pipe);
let window;
function isLocal(address){
  try{const u=new URL(address);return u.protocol==='simbar:'&&u.hostname==='app'&&!u.port&&!u.username&&!u.password;}
  catch{return false;}
}
function openExternal(address){
  try{const u=new URL(address);if(u.protocol==='https:'||u.protocol==='http:')shell.openExternal(u.href).catch(()=>{});}
  catch{}
}
function send(kind,bytes){
  if(peer.destroyed||!peer.writable||bytes.length>8000000)return false;
  const packet=Buffer.allocUnsafe(5+bytes.length);
  packet[0]=kind;packet.writeUInt32LE(bytes.length,1);bytes.copy(packet,5);
  return peer.write(packet);
}
function fail(error){
  const data=Buffer.from(String(error?.message||error).slice(0,1500));
  if(!peer.destroyed&&peer.writable){send(2,data);peer.end();setTimeout(()=>app.quit(),500);}
  else app.quit();
}
app.on('window-all-closed',()=>app.quit());
peer.on('error',()=>app.quit());
peer.on('close',()=>app.quit());
peer.once('connect',()=>{
  app.whenReady().then(async()=>{
    protocol.handle('simbar',async request=>{
      if(!isLocal(request.url))return new Response('Not found',{status:404});
      let pathname;
      try{pathname=decodeURIComponent(new URL(request.url).pathname);}
      catch{return new Response('Not found',{status:404});}
      if(pathname.includes('\0')||pathname.includes('\\'))return new Response('Not found',{status:404});
      const file=path.resolve(root,'.'+(pathname==='/'?'/index.html':pathname));
      const relative=path.relative(root,file);
      if(!relative||relative==='..'||relative.startsWith('..'+path.sep)||path.isAbsolute(relative)||!fs.existsSync(file)||!fs.statSync(file).isFile())
        return new Response('Not found',{status:404});
      const real=path.relative(root,fs.realpathSync(file));
      if(!real||real==='..'||real.startsWith('..'+path.sep)||path.isAbsolute(real))
        return new Response('Not found',{status:404});
      const response=await net.fetch(pathToFileURL(file).href);
      const headers=new Headers(response.headers);
      headers.set('Content-Security-Policy',csp);
      headers.set('X-Content-Type-Options','nosniff');
      return new Response(response.body,{status:response.status,headers});
    });
    session.defaultSession.setPermissionRequestHandler((_web,permission,callback,details)=>callback(permission==='fullscreen'&&isLocal(details.requestingUrl)));
    session.defaultSession.setPermissionCheckHandler((_web,permission,url)=>permission==='fullscreen'&&isLocal(url));
    window=new BrowserWindow({width:1600,height:900,useContentSize:true,show:false,backgroundColor:'#101417',
      webPreferences:{offscreen:true,nodeIntegration:false,contextIsolation:true,sandbox:true,webSecurity:true,backgroundThrottling:false}});
    const web=window.webContents;
    web.setWindowOpenHandler(({url})=>{openExternal(url);return{action:'deny'};});
    web.on('will-navigate',(event,url)=>{if(!isLocal(url)){event.preventDefault();openExternal(url);}});
    web.on('will-attach-webview',event=>event.preventDefault());
    web.on('render-process-gone',(_event,details)=>fail('Electron renderer stopped: '+details.reason));
    web.setFrameRate(30);
    web.on('paint',(_event,_dirty,image)=>{
      if(!image.isEmpty()&&peer.writableLength<500000)send(1,image.toPNG());
    });
    web.on('did-finish-load',()=>send(3,Buffer.from(web.getURL())));
    let incoming='';
    peer.setEncoding('utf8');
    peer.on('data',chunk=>{
      incoming+=chunk;
      if(incoming.length>65536){fail('Oversized Unity preview input.');return;}
      for(let end;(end=incoming.indexOf('\n'))>=0;){
        const line=incoming.slice(0,end);incoming=incoming.slice(end+1);
        try{
          const data=JSON.parse(line);
          if(data.type==='close'){app.quit();return;}
          if(data.type==='focus'){web.focus();continue;}
          if(['mouseMove','mouseDown','mouseUp','mouseWheel','keyDown','keyUp','char'].includes(data.type)){
            if(data.type==='mouseDown')web.focus();
            if(data.type.startsWith("mouse")){
              const [width,height]=window.getContentSize();
              data.x=Math.max(0,Math.min(width-1,Math.floor(data.x*width/1600)));
              data.y=Math.max(0,Math.min(height-1,Math.floor(data.y*height/900)));
            }
            if(!data.button)delete data.button;
            if(!data.keyCode)delete data.keyCode;
            web.sendInputEvent(data);
          }
        }catch{}
      }
    });
    await window.loadURL(origin+'/index.html');
    web.focus();
  }).catch(fail);
});
