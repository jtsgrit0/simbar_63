const people=new Set(['guest','seated','support','surfer','assisted']);
const order=Array.from({length:100},(_,i)=>(i*37)%100);
export const patronPools=Object.freeze([order.slice(0,33),order.slice(33,66),order.slice(66)].map(Object.freeze));
const mix=(seed,key)=>{
  let n=(seed^0x9e3779b9)>>>0;
  for(const c of String(key)){n=Math.imul(n^c.charCodeAt(0),16777619)>>>0;}
  n^=n>>>16;n=Math.imul(n,0x7feb352d);n^=n>>>15;n=Math.imul(n,0x846ca68b);
  return(n^n>>>16)>>>0;
};
export function audienceIdentity(actor,owner,seed=0){
  const pool=patronPools[owner];
  if(!pool||!people.has(actor?.role))return null;
  if(pool.includes(actor.appearanceId))return actor.appearanceId;
  return pool[mix(seed,actor.id)%pool.length];
}

export function assignPatronModels(actors,owner,seed=0){
  const pool=patronPools[owner];if(!pool)return;
  const ordered=pool.slice().sort((a,b)=>mix(seed,a)-mix(seed,b));
  const assigned=[],pending=[];
  for(const a of actors){
    if(!people.has(a.role)||a.role==='assisted')continue;
    if(pool.includes(a.appearanceId))assigned.push(a);
    else pending.push(a);
  }
  for(const actor of pending){
    const counts=new Map(),neighbors=new Map();
    for(const a of assigned){
      if(a.visible===false)continue;
      const id=a.appearanceId,gap=Math.hypot((a.homeX??a.x)-(actor.homeX??actor.x),(a.homeZ??a.z)-(actor.homeZ??actor.z));
      counts.set(id,(counts.get(id)||0)+1);
      if(gap<2.5)neighbors.set(id,(neighbors.get(id)||0)+(2.5-gap));
    }
    let min=Infinity;
    for(let i=0;i<ordered.length;i++){
      const id=ordered[i],score=(counts.get(id)||0)*4+(neighbors.get(id)||0)*10+((i+mix(seed,actor.id)%pool.length)%pool.length)/pool.length;
      if(score<min){min=score;actor.appearanceId=id;}
    }
    assigned.push(actor);
  }
  for(const a of actors)if(a.role==='assisted'){
    const source=assigned.find(x=>x.id===a.assistedSourceId);
    a.appearanceId=source?.appearanceId??audienceIdentity(a,owner,seed);
  }
}

const pi=Math.PI;
const sin=Math.sin;
const step=(t,f)=>sin(t*f);
export function patronMotion(actor,pose,action,{active=true,seated=false}={}){
  const id=pose.festivalAction||pose.kind||action,t=active?pose.animationSeconds||0:0,w=step(t,7),v=step(t,5),h=step(t,10);
  let stride=0,left=0,right=0,reach=0,head=0,chest=0,knee=0;
  if(active)switch(id){
    case 'walk':case 'arrive':case 'return':case 'exit':case 'escorted':case 'to-stage':
      stride=w*3.5;knee=Math.abs(w);chest=-Math.abs(w)*.7;left=-w*.65;right=w*.65;break;
    case 'skank':stride=(pose.skankStep??Math.sign(w))*4;knee=pose.skankKnee??Math.abs(w);left=-(pose.skankArm??w)*1.2;right=(pose.skankArm??w)*1.2;chest=v;break;
    case 'rock':case 'horns':right=2.75+v*.25;reach=2;break;
    case 'jump':left=1.8+h*.2;right=2.65;stride=w*1.45;knee=.8;break;
    case 'encore':left=2.45+w*.35;right=2.45-w*.35;reach=2;chest=v;break;
    case 'sing':right=.85+Math.max(0,v)*.75;head=-.7+v*.42;reach=1;break;
    case 'headbang':head=h*2.75;chest=h*.6;right=.5;break;
    case 'dance':stride=w*1.9;knee=Math.abs(w)*.5;chest=v*1.4;left=.8+w*1.1;right=.8-w*1.1;break;
    case 'airguitar':left=.48+w*.45;right=1.35;reach=5;chest=v;break;
    case 'slam':stride=w*2.6;left=.8+h*.48;right=1.25-h*.45;reach=3;head=w*1.3;break;
    case 'mosh':stride=w*2.9;left=1.15+w*.5;right=1.1-w*.5;reach=3;head=h*1.1;break;
    case 'surf':
      if(actor.festivalSupport||action==='support'){left=2.4;right=2.7;knee=.2;}
      else{left=1.5+v*.5;right=2.1;head=v;}
      break;
    case 'support':left=2.4;right=2.7;break;
    case 'flash':right=1.73+v*.2;reach=4;break;
    case 'cheer':left=1.2;right=1.75+w*.3;break;
    case 'fight':left=1.48;right=pose.strike?1.52:1.3;reach=pose.strike?8:2;stride=w*1.2;break;
    case 'highfive':right=2+v*.4;reach=6;break;
    case 'hug':left=.8;right=1.45;reach=7;break;
    case 'queue':case 'served':right=.2+Math.max(0,w)*.16;break;
    case 'sway':chest=v*.5;break;
    case 'listen':head=v*.35;break;
  }
  // Transport takes precedence over standing gestures even during a shared action.
  if(active&&action==='walk'&&id!=='skank'&&id!=='walk'){stride=w*3.5;knee=Math.abs(w);left=-w*.65;right=w*.65;}
  if(seated){stride=0;knee=0;chest*=.4;left*=.62;right*=.69;head*=.48;}
  return {stride,left,right,reach,head,chest,knee,seated,frozen:!active};
}

const cut=(ctx,sheet,ox,oy,sx,sy,w,h,x,y,dw=w,dh=h)=>{
  if(w>0&&h>0&&dw>0&&dh>0)ctx.drawImage(sheet,ox+sx,oy+sy,w,h,Math.round(x),Math.round(y),Math.round(dw),Math.round(dh));
};
function arm(ctx,x,y,angle,reach,cloth,skin,side){
  if(Math.abs(angle)<.62)return;
  const spread=side*(4+reach*.35),handx=x+Math.round(spread+Math.cos(angle)*side*2),
    handy=y+Math.round(Math.cos(angle)*11),elbowx=x+Math.round(spread*.55),elbowy=y+Math.round(Math.cos(angle)*6);
  const stroke=(a,b,c,d,color,width)=>{
    const n=Math.max(Math.abs(c-a),Math.abs(d-b),1);
    ctx.fillStyle=color;
    for(let i=0;i<=n;i++)ctx.fillRect(Math.round(a+(c-a)*i/n-width/2),Math.round(b+(d-b)*i/n-width/2),width,width);
  };
  stroke(x,y,elbowx,elbowy,'#221b22',5);
  stroke(elbowx,elbowy,handx,handy,'#221b22',5);
  stroke(x,y,elbowx,elbowy,cloth,3);
  stroke(elbowx,elbowy,handx,handy,cloth,3);
  ctx.fillStyle=skin;ctx.fillRect(handx-1,handy-1,3,3);
  if(angle>2.3){ctx.fillRect(handx-2,handy-3,1,3);ctx.fillRect(handx+2,handy-3,1,3);}
}

export function drawPatronModel(ctx,sheet,rig,id,direction,motion){
  const [top,neck,shoulder,hip,l,r,split,feet,cloth,skin,legs,hair]=rig,
    ox=id%10*64,oy=Math.floor(id/10)*64,mirror=direction.endsWith('L')?-1:1,back=direction.startsWith('B');
  const {stride,left,right,reach,head,chest,knee,seated}=motion;
  const base=.63,bodyUp=seated?5:0;
  ctx.save();ctx.scale(base*mirror,base);ctx.translate(-feet,-61);
  if(seated){
    // Compress and shift the thigh, then plant the shins and shoes under the seat.
    const shins=Math.min(10,61-hip),thigh=61-hip-shins;
    cut(ctx,sheet,ox,oy,0,hip,64,thigh,3,hip+bodyUp,64,Math.max(4,thigh-4));
    cut(ctx,sheet,ox,oy,0,61-shins,64,shins,5,61-shins,64,shins);
  }else if(Math.abs(stride)>.2){
    const slide=Math.round(stride),lift=Math.round(knee*3);
    for(let y=hip;y<61;y+=3){
      const n=Math.min(3,61-y),part=(y-hip)/(61-hip),dx=Math.round(slide*part),dy=Math.round(lift*part);
      cut(ctx,sheet,ox,oy,0,y,split,n,-dx,y-(slide<0?dy:0));
      cut(ctx,sheet,ox,oy,split,y,64-split,n,split+dx,y-(slide>0?dy:0));
    }
  }else cut(ctx,sheet,ox,oy,0,hip,64,61-hip,0,hip);
  const drift=Math.round(chest);
  arm(ctx,l+3+drift,shoulder+bodyUp,left,reach,cloth,skin,-1);
  cut(ctx,sheet,ox,oy,0,neck,64,hip-neck+1,drift,neck+bodyUp);
  const dip=Math.round(head);
  cut(ctx,sheet,ox,oy,0,top,64,neck-top+2,drift,top+bodyUp+dip);
  if(back){
    // The approved sheets show one side; obscure the front face on away facings.
    const x=Math.round((l+r)/2+drift),y=neck+bodyUp+dip-5;
    ctx.fillStyle=hair;ctx.fillRect(x+1,y,4,3);ctx.fillRect(x+2,y-2,4,3);
  }
  arm(ctx,r-3+drift,shoulder+bodyUp,right,reach,cloth,skin,1);
  ctx.restore();
}
