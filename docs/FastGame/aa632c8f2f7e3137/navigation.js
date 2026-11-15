// Public aisles and the clear staff aisle follow the authored venue's metre coordinates.
const obstacles=[[-10.02,-2.32,-2.02,-.88],[-9.745,-2.625,-2.255,-2.535],[-9.44,-3.49,-2.56,-2.81],[-9.54,-7.14,-5.66,-3.64],[-4.64,-7.14,-.76,-3.64],[-9.615,3.41,-5.985,7.07],[-5.515,3.41,-1.885,7.07],[1.4625,2.2075,9.3375,6.5],[4.3,1.6025,6.4,2.245]];
// FF replaces the sofa/bar footprint. Every query receives its actor's layout;
// one game or minimap must never change another instance's navigation mesh.
const pt=(x,z)=>Object.freeze({x,z});
const ffTables=Object.freeze([[-6.7,3.6],[-4,4.75],[-3.6,.15],[-5.9,-4.8],[-1.7,-5]]
  .map(([x,z])=>Object.freeze({x,z,radius:.55})));
const ffStools=Object.freeze(ffTables.flatMap((table,index)=>[-.9,.9]
  .map(offset=>Object.freeze({x:table.x+offset,z:table.z,radius:.22,table:index}))));
export const FF_LAYOUT=Object.freeze({
  id:'ff',version:4,
  counter:Object.freeze([-8.15,-3,-6.95,1.3]),
  dj:Object.freeze([-1.85,2.25,1.75,4.35]),
  bartender:pt(-8.9,-1.2),server:pt(-6.1,-.5),barback:pt(-8.9,1.1),floorhost:pt(.5,-4.9),
  tables:ffTables,stools:ffStools,
  orderSlots:Object.freeze([pt(-6.1,-1.65),pt(-6.1,-2.45),pt(-5.7,-3.2),pt(-4.9,-3.6),pt(-4.1,-3.8),pt(-3.3,-3.8),pt(-2.5,-3.8)]),
  standing:Object.freeze([pt(-4.25,-5.25),pt(-3.3,-4.7),pt(-1.2,-3.9),pt(-.3,-4.7),pt(-5,1.7),pt(-3.4,2.5),pt(-1.8,1.25),pt(-.2,1.5),pt(-5.4,.25),pt(-1.9,-.15),pt(-1,.5)])
});
export const FREEBIRD_LAYOUT=Object.freeze({
  id:'freebird',version:3,
  counter:Object.freeze([-6,3,-2,4.2]),
  stage:Object.freeze([-.3,2.2075,9.7,6.5]),
  bartender:pt(-4.8,4.85),server:pt(-1,2.7),barback:pt(-3.1,4.85),
  staffExit:pt(-6.8,4.85),
  wallTable:Object.freeze([-9.4,-4.4,-8.75,2]),
  orderSlots:Object.freeze([pt(-4.8,1.75),pt(-3.95,1.75),pt(-3.1,1.75),pt(-2.25,1.75),pt(-1.4,1.45),pt(-.55,.65),pt(.3,-.15)]),
  standing:Object.freeze([pt(-4.8,-4.8),pt(-3.3,-4.7),pt(-1.8,-4.7),pt(-.3,-4.7),pt(-7,1),pt(-7.2,2.5),pt(-2.3,.8),pt(-.3,.7),pt(-6,-1),pt(-4.2,-.8),pt(-2.6,-.9)])
});
const ffObstacles=[FF_LAYOUT.counter,FF_LAYOUT.dj,...obstacles.slice(-2)];
const ffRoundObstacles=[...FF_LAYOUT.tables,...FF_LAYOUT.stools];
const freebirdObstacles=[FREEBIRD_LAYOUT.counter,FREEBIRD_LAYOUT.stage,FREEBIRD_LAYOUT.wallTable,obstacles.at(-1)];
export const distance=(a,b)=>Math.hypot(a.x-b.x,a.z-b.z);
export const reserved=p=>Math.hypot(p.x-5.75,p.z+1.25)<2.05||(p.x>4.3&&p.x<7.8&&p.z>-5.25&&p.z<-3.15);
export function walkable(p,staff=false,floor=false,layout=p?.layout){
  if(layout==='freebird'){
    if(p.x< -9.4||p.x>9.1||p.z< -5.85||p.z>5.8)return false;
    if(!staff&&p.x> -6.5&&p.x< -1.5&&p.z>4.43)return false;
    if(freebirdObstacles.some(([x0,z0,x1,z1])=>p.x>x0-.23&&p.x<x1+.23&&p.z>z0-.23&&p.z<z1+.23))return false;
    return !floor||!reserved(p);
  }
  if(layout==='ff'){
    if(p.x< -9.4||p.x>9.1||p.z< -5.85||p.z>5.8)return false;
    if(!staff&&p.x< -6.6&&p.z> -3.7&&p.z<1.9)return false;
    if(ffObstacles.some(([x0,z0,x1,z1])=>p.x>x0-.23&&p.x<x1+.23&&p.z>z0-.23&&p.z<z1+.23))return false;
    if(ffRoundObstacles.some(circle=>Math.hypot(p.x-circle.x,p.z-circle.z)<circle.radius+.23))return false;
    return !floor||!reserved(p);
  }
  if(p.x>9.1||p.z< -5.85||p.z>3.45)return false;
  if(p.x< -1.65&&(!staff||p.x< -9.4||p.z< -.62||p.z>1.2))return false;
  if(obstacles.some(([x0,z0,x1,z1])=>p.x>x0-.23&&p.x<x1+.23&&p.z>z0-.23&&p.z<z1+.23))return false;
  return !floor||!reserved(p);
}
export function segment(a,b,staff=false,floor=false,layout=a?.layout){
  const steps=Math.max(1,Math.ceil(distance(a,b)/.1));for(let i=1;i<=steps;i++)if(!walkable({x:a.x+(b.x-a.x)*i/steps,z:a.z+(b.z-a.z)*i/steps},staff,floor,layout))return false;return true;
}
const grids=new Map();
function grid(staff,floor,layout){
  const custom=layout==='ff'||layout==='freebird';
  const key=(custom?layout+':':'')+staff+':'+floor;if(grids.has(key))return grids.get(key);
  const width=custom||staff?48:28,height=custom?30:24,x0=custom||staff?-9.6:-1.6,points=Array.from({length:width*height},(_,i)=>({x:x0+(i%width)*.4,z:-5.8+Math.floor(i/width)*.4}));
  const valid=points.map(p=>walkable(p,staff,floor,layout)),neighbors=points.map(()=>[]);
  for(let i=0;i<points.length;i++)if(valid[i])for(let dz=-1;dz<=1;dz++)for(let dx=-1;dx<=1;dx++){
    const x=i%width+dx,z=Math.floor(i/width)+dz,j=z*width+x;
    if((dx||dz)&&x>=0&&x<width&&z>=0&&z<height&&valid[j]&&segment(points[i],points[j],staff,floor,layout))neighbors[i].push(j);
  }
  const data={points,valid,neighbors,endpoints:new Map()};grids.set(key,data);return data;
}
export function pathfind(start,goal,{staff=false,floor=false,people=[],layout=start?.layout}={}){
  if(!walkable(start,staff,floor,layout)||!walkable(goal,staff,floor,layout))return [];
  const {points,valid,neighbors,endpoints}=grid(staff,floor,layout);
  const blocked=p=>people.some(body=>distance(p,body)<.39);
  const bodiesClear=(a,b)=>people.every(body=>{
    const before=distance(a,body),after=distance(b,body);
    // An initially crowded actor must move away from the body immediately.
    // A farther endpoint alone can still send its first step into that body.
    if(before<.39)return after>=before-.00001&&(b.x-a.x)*(a.x-body.x)+(b.z-a.z)*(a.z-body.z)>=-.00001;
    const dx=b.x-a.x,dz=b.z-a.z,len=dx*dx+dz*dz,t=len?Math.max(0,Math.min(1,((body.x-a.x)*dx+(body.z-a.z)*dz)/len)):0;
    return Math.hypot(body.x-a.x-t*dx,body.z-a.z-t*dz)>=.38;
  });
  // Try the nearest mesh points first. Row-order scanning repeatedly checked
  // long, impossible sight lines before reaching an actor's local aisle. With
  // a whole room leaving together those synchronous segment scans stalled the
  // game frame. Distance ordering selects the same nearest reachable point;
  // the index tie-break preserves the mesh's original deterministic ordering.
  const closest=(p,staticGoal=false)=>{
    const key=p.x+','+p.z;let cached=staticGoal&&endpoints.get(key);
    if(!cached){
      const candidates=[];
      for(let i=0;i<points.length;i++)if(valid[i]){const dx=p.x-points[i].x,dz=p.z-points[i].z;candidates.push({i,d:dx*dx+dz*dz});}
      candidates.sort((a,b)=>a.d-b.d||a.i-b.i);
      cached={candidates,visible:new Int8Array(points.length)};
      if(staticGoal){if(endpoints.size>=64)endpoints.delete(endpoints.keys().next().value);endpoints.set(key,cached);}
    }
    for(const {i}of cached.candidates){
      // Bodies move while the venue's furniture does not. Reject blocked
      // approaches cheaply first; never retain body clearance across routes.
      if(blocked(points[i])||!bodiesClear(p,points[i]))continue;
      let visible=cached.visible[i];
      if(!visible){visible=segment(p,points[i],staff,floor,layout)?1:-1;cached.visible[i]=visible;}
      if(visible>0)return i;
    }
    return -1;
  };
  const first=closest(start);if(first<0)return [];
  const last=closest(goal,true);if(last<0)return [];
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
  actor._path=pathfind(actor,goal,{staff:actor.role==='bartender'||actor.role==='server'||actor.role.startsWith('hired'),floor,layout:actor.layout,people:actors.filter(a=>a!==actor&&a.visible&&a.role!=='band'&&(a.role!=='seated'||a.waitingSeat)&&a.id!==actor._partner).map(a=>({x:a.x,z:a.z}))});
  actor._pathAt=0;actor._blocked=0;return actor._path.length>0;
}
// Closing crowds can pack between the40cm mesh nodes while still leaving a
// safe34cm movement gap. Keep the mesh route whenever one exists; otherwise
// take a short, swept, collision-checked step until a complete route opens.
export function setCrowdRoute(actor,goal,actors=[],floor=false){
  if(setRoute(actor,goal,actors,floor)){actor._crowdHistory=[];return true;}
  const staff=actor.role==='bartender'||actor.role==='server'||actor.role.startsWith('hired');
  const people=actors.filter(a=>a!==actor&&a.visible!==false&&a.role!=='band'&&a.id!==actor._partner&&a.id!=='crowd-surfer'&&Math.abs((a.y||0)-(actor.y||0))<1);
  const clear=next=>{
    if(!segment(actor,next,staff,floor,actor.layout))return false;
    const dx=next.x-actor.x,dz=next.z-actor.z,length=dx*dx+dz*dz;
    return people.every(other=>{
      const before=distance(actor,other),after=distance(next,other);
      if(before<.34)return after>=before-.00001&&dx*(actor.x-other.x)+dz*(actor.z-other.z)>=-.00001;
      const t=length?Math.max(0,Math.min(1,((other.x-actor.x)*dx+(other.z-actor.z)*dz)/length)):0;
      return Math.hypot(other.x-actor.x-t*dx,other.z-actor.z-t*dz)>=.34;
    });
  };
  const angle=Math.atan2(goal.z-actor.z,goal.x-actor.x),before=distance(actor,goal);
  const history=Array.isArray(actor._crowdHistory)?actor._crowdHistory.slice(-6).filter(p=>p&&Number.isFinite(p.x)&&Number.isFinite(p.z)&&Math.abs(p.x)<20&&Math.abs(p.z)<20):[];
  let best=null;
  for(let i=0;i<32;i++){
    const turn=i===0?0:Math.ceil(i/2)*(i%2?1:-1),theta=angle+turn*Math.PI/16;
    const next={x:actor.x+Math.cos(theta)*.32,z:actor.z+Math.sin(theta)*.32};
    if(!clear(next))continue;
    const recent=history.reduce((penalty,p,index)=>penalty+Math.max(0,.25-distance(next,p))*(index+1)/Math.max(1,history.length),0);
    const score=before-distance(next,goal)-recent*2;
    if(!best||score>best.score)best={next,score};
  }
  if(!best)return false;
  actor._path=[best.next];actor._pathAt=0;actor._blocked=0;
  actor._crowdHistory=[...history,{x:actor.x,z:actor.z}].slice(-6);
  return true;
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
