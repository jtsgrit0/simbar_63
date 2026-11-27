/* Compile the four approved review sheets into the game atlas and rig metadata.
   Run in the build harness with decoded sheet pixels in row-major order. */
export function packAudience(sheets){
  const N=64,C=10,atlas=new Uint8ClampedArray(N*C*N*C*4),rigs=[];
  const edge=(data,p)=>[data[p],data[p+1],data[p+2]];
  const close=(a,b)=>Math.max(...a.map((v,i)=>Math.abs(v-b[i])));
  const hex=a=>'#'+a.map(x=>x.toString(16).padStart(2,'0')).join('');
  for(const {width:w,height:h,data} of sheets)for(let row=0;row<5;row++)for(let col=0;col<5;col++){
    const x0=Math.round(col*w/5)+4,y0=Math.round(row*h/5)+4,x1=Math.round((col+1)*w/5)-4,y1=Math.round((row+1)*h/5)-4;
    const ww=x1-x0,hh=y1-y0,at=(x,y)=>((y+y0)*w+x+x0)*4;
    const seen=new Uint8Array(ww*hh),rest=new Uint8Array(ww*hh),todo=[];
    function push(x,y){
      if(x<0||y<0||x>=ww||y>=hh)return;
      const i=y*ww+x;if(seen[i])return;
      const rgb=edge(data,at(x,y)),mix=x/(ww-1),left=edge(data,at(0,y)),right=edge(data,at(ww-1,y));
      const bg=left.map((v,n)=>v*(1-mix)+right[n]*mix);
      if(close(rgb,bg)>9)return;
      seen[i]=1;todo.push(i);
    }
    for(let x=0;x<ww;x++){push(x,0);push(x,hh-1);}
    for(let y=0;y<hh;y++){push(0,y);push(ww-1,y);}
    for(let t=0;t<todo.length;t++){
      const i=todo[t],x=i%ww,y=Math.floor(i/ww);
      push(x-1,y);push(x+1,y);push(x,y-1);push(x,y+1);
    }
    let body=[];
    for(let i=0;i<seen.length;i++){
      if(seen[i]||rest[i])continue;
      const q=[i];rest[i]=1;
      for(let j=0;j<q.length;j++){
        const a=q[j],x=a%ww,y=Math.floor(a/ww);
        for(const b of [x>0?a-1:-1,x<ww-1?a+1:-1,y>0?a-ww:-1,y<hh-1?a+ww:-1]){
          if(b>=0&&!seen[b]&&!rest[b]){q.push(b);rest[b]=1;}
        }
      }
      if(q.length>body.length)body=q;
    }
    if(body.length<ww*hh*.08)throw Error('Missing patron '+rigs.length);
    const xs=body.map(i=>i%ww),ys=body.map(i=>Math.floor(i/ww));
    const minX=Math.min(...xs),minY=Math.min(...ys),bw=Math.max(...xs)-minX+1,bh=Math.max(...ys)-minY+1;
    if(bw>ww*.75||bh<hh*.55)throw Error('Unusable cutout '+rigs.length);
    const mask=new Uint8Array(ww*hh);for(const i of body)mask[i]=1;
    // Keep the approved relative body proportions and put both feet on the baseline.
    const ph=Math.min(58,Math.max(43,Math.round(bh/hh*59))),pw=Math.round(bw/bh*ph);
    const sx=Math.floor((N-pw)/2),sy=61-ph,ox=rigs.length%C*N,oy=Math.floor(rigs.length/C)*N;
    const colors=[],pants=[],skins=[],hair=[];
    const rows=Array.from({length:N},()=>[]);
    for(let y=0;y<ph;y++)for(let x=0;x<pw;x++){
      const xx=minX+Math.floor((x+.5)*bw/pw),yy=minY+Math.floor((y+.5)*bh/ph);
      if(!mask[yy*ww+xx])continue;
      const [r,g,b]=edge(data,at(xx,yy)),a=((oy+sy+y)*N*C+ox+sx+x)*4;
      atlas.set([r,g,b,255],a);
      rows[sy+y].push(sx+x);
      if(r>75&&r>g*1.14&&g>b*1.09&&y<ph*.33&&x>pw*.37)skins.push([r,g,b]);
      if(y<ph*.20&&x>pw*.18&&x<pw*.66&&r+g+b>40)hair.push([r,g,b]);
      if(y>ph*.31&&y<ph*.54&&x>pw*.16&&x<pw*.49&&r+g+b>68)colors.push([r,g,b]);
      if(y>ph*.70&&y<ph*.87&&x>pw*.25&&x<pw*.68&&r+g+b>65)pants.push([r,g,b]);
    }
    const popular=(points,fallback)=>{
      const groups=new Map();
      for(const v of points){const k=v.map(x=>x>>4).join(',');if(!groups.has(k))groups.set(k,[]);groups.get(k).push(v);}
      const group=[...groups.values()].sort((a,b)=>b.length-a.length)[0];
      return hex(group?group[Math.floor(group.length/2)]:fallback);
    };
    const bounds=(y,dy)=>{
      for(let i=0;i<dy;i++)if(rows[y+i]?.length>4)return [rows[y+i][0],rows[y+i].at(-1)];
      return [sx,sx+pw-1];
    };
    const neck=sy+Math.round(ph*.28),shoulder=sy+Math.round(ph*.37),hip=sy+Math.round(ph*.65);
    const [l,r]=bounds(shoulder,3),[hl,hr]=bounds(hip,4);
    const xFoot=rows[60].length?Math.round((rows[60][0]+rows[60].at(-1))/2):32;
    rigs.push([sy,neck,shoulder,hip,l,r,Math.round((hl+hr)/2),xFoot,popular(colors,[86,88,95]),popular(skins,[191,135,103]),popular(pants,[60,57,65]),popular(hair,[56,44,41])]);
  }
  if(rigs.length!==100)throw Error('Expected 100 approved patrons');
  return {atlas,rigs,width:N*C,height:N*C};
}
