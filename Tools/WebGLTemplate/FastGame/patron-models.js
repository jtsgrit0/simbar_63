const sets=Object.freeze([[1,4,5,9,14,15,24,26,30,36,40,42,43,53,94],[6,7,8,10,12,13,17,25,27,31,37,38,41,44,46],[2,3,11,16,18,19,20,21,22,23,29,33,35,39,45,47,57]].map(Object.freeze));
export const patronPools=sets;
const roles=new Set(['seated','guest','support','surfer','assisted']);
function rank(key,seed){
  let h=(2166136261^seed)>>>0;
  for(const s of String(key))h=Math.imul(h^s.charCodeAt(0),16777619)>>>0;
  h=Math.imul(h^(h>>>16),0x45d9f3b);h=Math.imul(h^(h>>>16),0x45d9f3b);return (h^(h>>>16))>>>0;
}
export function audienceIdentity(a,owner){
  if(!roles.has(a?.role)||a.patronArtVersion!==2||!sets[owner]?.includes(a.appearanceId))return null;
  return a.appearanceId;
}
export function assignPatronModels(actors,owner,seed){
  const pool=sets[owner];if(!pool)return;
  const regular=actors.filter(a=>roles.has(a.role)&&a.role!=='assisted');
  const used=new Set(regular.map(a=>audienceIdentity(a,owner)).filter(n=>n!=null));
  const pending=regular.filter(a=>a.patronArtVersion!==2).sort((a,b)=>rank(a.id,seed)-rank(b.id,seed));
  const available=pool.filter(id=>!used.has(id)).sort((a,b)=>rank(a,seed)-rank(b,seed));
  for(const a of pending){a.appearanceId=available.shift()??null;a.patronArtVersion=2;}
  for(const a of actors)if(a.role==='assisted'){
    const source=regular.find(x=>x.id===a.assistedSourceId);
    a.appearanceId=source?.appearanceId??null;a.patronArtVersion=2;
  }
}
const aliases=Object.freeze({arrive:'walk',return:'walk',exit:'walk',escorted:'walk','to-stage':'walk',fight:'slam',cheer:'horns',rock:'horns',served:'idle',queue:'idle',listen:'idle',sway:'idle','needs-help':'idle','aftershow-wait':'idle'});
const baseRows=Object.freeze({walk:[0,1],sit:[2,3],seatedReaction:[2,3],horns:[4],headbang:[5],idle:[3]});
const extraRows=Object.freeze({slam:[1,0],mosh:[1,1],surf:[1,2],support:[1,3],jump:[1,4],airguitar:[1,5],sing:[2,0],encore:[2,1],dance:[2,2],skank:[2,3],flash:[2,4],highfive:[2,5],hug:[3,0]});
export function authoredFrame(a,pose,action,dir,active,sheetCount=1){
  const back=dir.startsWith('B'),flip=dir.endsWith('L'),seated=(a.role==='seated'&&!a.moving&&action!=='walk'),gesture=pose.festivalAction||pose.kind||action;
  const timedLift=!pose.festivalAction&&a.role==='surfer'&&a.behavior==='surf'&&action==='surf'&&a.y>0;
  let clip=aliases[gesture]||gesture;
  if(!active)clip=seated?'sit':'idle';
  else if(action==='walk'&&gesture!=='skank')clip='walk';
  else if(seated)clip=gesture==='flash'?'sit':pose.festivalAction?'seatedReaction':'sit';
  else if(gesture==='surf'&&a.festivalSupport)clip='support';
  else if(gesture==='surf'&&!timedLift&&(!a.festivalLead||!a.festivalLiftReady||!(a.y>.05)))clip='horns';
  let sheet=0,row,col=0,horizontal=false,fps=6,frames=4;
  if(extraRows[clip]&&sheetCount>extraRows[clip][0]){
    [sheet,row]=extraRows[clip];horizontal=clip==='surf';
    if(clip==='jump')fps=5;
    if(clip==='highfive'||clip==='hug'){
      const progression=Math.min(.999,Math.max(0,a.actionProgress??0));
      col=Math.floor(progression*4);
      return {sheet,row,col,flip,horizontal,clip};
    }
  }else{
    if(!baseRows[clip])clip=seated?'sit':'idle';
    if(clip==='idle')return {sheet,row:3,col:back?3:2,flip,horizontal,clip};
    if(clip==='sit')return {sheet,row:back?3:2,col:0,flip,horizontal,clip};
    if(clip==='seatedReaction'){row=back?3:2;frames=back?2:4;fps=4;}
    else if(clip==='walk'){row=back?1:0;fps=7;}
    else row=baseRows[clip][0];
  }
  col=Math.floor(Math.max(0,pose.animationSeconds||0)*fps)%frames;
  return {sheet,row,col,flip,horizontal,clip};
}
export const patronDrawSize=52;
export function drawWholePatron(ctx,art,f){
  const {sheet,image,offsetX,offsetY}=art;
  if(f.sheet>=sheet)return false;
  const dim=patronDrawSize;
  ctx.save();
  if(f.flip)ctx.scale(-1,1);
  ctx.drawImage(image,offsetX+f.sheet*256+f.col*64,offsetY+f.row*64,64,64,-26,f.horizontal?-26:-49,dim,dim);
  ctx.restore();
  return true;
}
