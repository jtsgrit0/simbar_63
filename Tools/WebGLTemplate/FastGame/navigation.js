// Public aisles and the clear staff aisle follow the authored venue's metre coordinates.
const obstacles=[[-10.02,-2.32,-2.02,-.88],[-9.745,-2.625,-2.255,-2.535],[-9.44,-3.49,-2.56,-2.81],[-9.54,-7.14,-5.66,-3.64],[-4.64,-7.14,-.76,-3.64],[-9.615,3.41,-5.985,7.07],[-5.515,3.41,-1.885,7.07],[1.4625,2.2075,9.3375,6.5],[4.3,1.6025,6.4,2.245]];
export const distance=(a,b)=>Math.hypot(a.x-b.x,a.z-b.z);
export const reserved=p=>Math.hypot(p.x-5.75,p.z+1.25)<2.05||(p.x>4.3&&p.x<7.8&&p.z>-5.25&&p.z<-3.15);
export function walkable(p,staff=false,floor=false){
  if(p.x>9.1||p.z< -5.85||p.z>3.45)return false;
  if(p.x< -1.65&&(!staff||p.x< -9.4||p.z< -.62||p.z>1.2))return false;
  if(obstacles.some(([x0,z0,x1,z1])=>p.x>x0-.23&&p.x<x1+.23&&p.z>z0-.23&&p.z<z1+.23))return false;
  return !floor||!reserved(p);
}
export function segment(a,b,staff=false,floor=false){
  const steps=Math.max(1,Math.ceil(distance(a,b)/.1));for(let i=1;i<=steps;i++)if(!walkable({x:a.x+(b.x-a.x)*i/steps,z:a.z+(b.z-a.z)*i/steps},staff,floor))return false;return true;
}
const grids=new Map();
function grid(staff,floor){
  const key=staff+':'+floor;if(grids.has(key))return grids.get(key);
  const width=staff?48:28,x0=staff?-9.6:-1.6,points=Array.from({length:width*24},(_,i)=>({x:x0+(i%width)*.4,z:-5.8+Math.floor(i/width)*.4}));
  const valid=points.map(p=>walkable(p,staff,floor)),neighbors=points.map(()=>[]);
  for(let i=0;i<points.length;i++)if(valid[i])for(let dz=-1;dz<=1;dz++)for(let dx=-1;dx<=1;dx++){
    const x=i%width+dx,z=Math.floor(i/width)+dz,j=z*width+x;
    if((dx||dz)&&x>=0&&x<width&&z>=0&&z<24&&valid[j]&&segment(points[i],points[j],staff,floor))neighbors[i].push(j);
  }
  const data={points,valid,neighbors};grids.set(key,data);return data;
}
export function pathfind(start,goal,{staff=false,floor=false,people=[]}={}){
  if(!walkable(start,staff,floor)||!walkable(goal,staff,floor))return [];
  const {points,valid,neighbors}=grid(staff,floor);
  const blocked=p=>people.some(body=>distance(p,body)<.39);
  const bodiesClear=(a,b)=>people.every(body=>{
    const before=distance(a,body),after=distance(b,body);
    if(before<.39)return after>=before-.00001;
    const dx=b.x-a.x,dz=b.z-a.z,len=dx*dx+dz*dz,t=len?Math.max(0,Math.min(1,((body.x-a.x)*dx+(body.z-a.z)*dz)/len)):0;
    return Math.hypot(body.x-a.x-t*dx,body.z-a.z-t*dz)>=.38;
  });
  const closest=p=>{let best=-1,score=Infinity;for(let i=0;i<points.length;i++){const d=distance(p,points[i]);if(valid[i]&&d<score&&segment(p,points[i],staff,floor)&&!blocked(points[i])&&bodiesClear(p,points[i])){score=d;best=i;}}return best;};
  const first=closest(start),last=closest(goal);if(first<0||last<0)return [];
  const scores=new Float64Array(points.length).fill(Infinity),parents=new Int32Array(points.length).fill(-1),closed=new Uint8Array(points.length),open=[first];scores[first]=0;
  while(open.length){
    let best=0,cost=Infinity;for(let i=0;i<open.length;i++){const n=open[i],f=scores[n]+distance(points[n],points[last]);if(f<cost){cost=f;best=i;}}
    const current=open.splice(best,1)[0];if(current===last){const route=[];for(let n=last;n!==first;n=parents[n]){if(n<0)return [];route.push(points[n]);}route.push(points[first]);route.reverse();route.push({x:goal.x,z:goal.z});return route;}
    closed[current]=1;for(const n of neighbors[current]){
      if(closed[n]||blocked(points[n])||!bodiesClear(points[current],points[n]))continue;let penalty=0;for(const body of people){const d=distance(points[n],body);if(d<.9&&distance(start,body)>.4)penalty+=(.9-d)*2;}
      const candidate=scores[current]+distance(points[current],points[n])+penalty;if(candidate>=scores[n])continue;scores[n]=candidate;parents[n]=current;if(!open.includes(n))open.push(n);
    }
  }return [];
}
export function setRoute(actor,goal,actors=[],floor=false){
  actor.targetX=goal.x;actor.targetZ=goal.z;actor._floorRoute=floor;
  actor._path=pathfind(actor,goal,{staff:actor.role==='bartender'||actor.role==='server'||actor.role.startsWith('hired'),floor,people:actors.filter(a=>a!==actor&&a.visible&&a.role!=='band'&&a.role!=='seated'&&a.id!==actor._partner).map(a=>({x:a.x,z:a.z}))});
  actor._pathAt=0;actor._blocked=0;return actor._path.length>0;
}
export function move(actor,dt,speed,actors=[]){
  const start={x:actor.x,z:actor.z},staff=actor.role==='bartender'||actor.role==='server'||actor.role.startsWith('hired');
  let budget=speed*dt;
  while(actor._path&&actor._pathAt<actor._path.length&&budget>0){
    const target=actor._path[actor._pathAt],d=distance(actor,target);
    if(d<.015){actor.x=target.x;actor.z=target.z;actor._pathAt++;continue;}
    const step=Math.min(budget,d),next={x:actor.x+(target.x-actor.x)*step/d,z:actor.z+(target.z-actor.z)*step/d};
    const blocked=!segment(actor,next,staff,actor._floorRoute)||actors.some(other=>other!==actor&&other.visible&&other.role!=='band'&&other.id!==actor._partner&&other.id!=='crowd-surfer'&&Math.abs((other.y||0)-(actor.y||0))<1&&distance(next,other)<.34&&distance(next,other)<distance(actor,other)-.00001);
    if(blocked){actor._blocked=(actor._blocked||0)+dt;if(actor._blocked>.8)setRoute(actor,{x:actor.targetX,z:actor.targetZ},actors,actor._floorRoute);break;}
    actor.x=next.x;actor.z=next.z;actor.facing=Math.atan2(next.x-start.x,next.z-start.z);budget-=step;actor._blocked=0;
  }
  actor.moving=distance(actor,start)>.001;return distance(actor,{x:actor.targetX,z:actor.targetZ})<=.2;
}
