import assert from 'node:assert/strict';
import fs from 'node:fs';
import {AUDIENCE_VENUES} from './WebGLTemplate/FastGame/owner-venues.js';
import {FREEBIRD_LAYOUT,walkable,pathfind,reserved,setRoute} from './WebGLTemplate/FastGame/navigation.js';

const metadata=name=>JSON.parse(fs.readFileSync(new URL('./WebGLTemplate/FastGame/art/venue-'+name+'.json',import.meta.url)));
const freebird=AUDIENCE_VENUES[0],ff=AUDIENCE_VENUES[1],fbMeta=metadata('freebird'),ffMeta=metadata('ff');
assert.equal(freebird.seats.length,6);
assert.equal(ff.standingSpots.length,10);
assert.equal(fbMeta.props.filter(prop=>prop.asset==='standing-stool').length,6);
assert.equal(ffMeta.props.filter(prop=>prop.asset==='cocktail-glass').length,10);
assert.ok(walkable(freebird.entry,false,false,'freebird'));
assert.ok(pathfind(freebird.entry,{x:-1,z:1},{layout:'freebird',floor:true}).length);
const [x0,z0,x1,z1]=FREEBIRD_LAYOUT.wallTable;
assert.equal(walkable({x:(x0+x1)/2,z:(z0+z1)/2},false,false,'freebird'),false);
for(const [profile,spots]of [[freebird,freebird.seats],[ff,ff.standingSpots]])for(const spot of spots){
  assert.ok(walkable(spot,false,true,profile.key),spot.id+' must allow standing/departure');
  assert.equal(reserved(spot),false,spot.id+' must keep the pit/surf lane clear');
  assert.ok(pathfind(spot,{x:1,z:0},{layout:profile.key,floor:true}).length,spot.id+' needs a route toward the stage');
  if(profile===freebird){
    assert.ok(fbMeta.seatedActors[spot.id],spot.id+' needs a rendered seat anchor');
    assert.ok(spot.waitSeconds>=12&&spot.waitSeconds<=30);
  }
}
// A waiting guest occupies walkable floor. Plans must avoid that body just as
// move() does, while excluding the moving guest itself from its own exit path.
const walker={id:'test-walker',role:'guest',layout:'freebird',x:-5,z:-1,visible:true};
const waiting={id:'test-waiter',role:'seated',waitingSeat:true,x:-4,z:-1,visible:true};
assert.ok(setRoute(walker,{x:-3,z:-1},[walker,waiting]));
let previous={x:walker.x,z:walker.z};
for(const point of walker._path){
  const dx=point.x-previous.x,dz=point.z-previous.z,length=dx*dx+dz*dz;
  const t=length?Math.max(0,Math.min(1,((waiting.x-previous.x)*dx+(waiting.z-previous.z)*dz)/length)):0;
  assert.ok(Math.hypot(waiting.x-previous.x-t*dx,waiting.z-previous.z-t*dz)>=.379,'route must not cross a waiting body');
  previous=point;
}
const seatedWalker={...waiting,id:'departing-waiter',layout:'freebird'};
assert.ok(setRoute(seatedWalker,{x:-3,z:-1},[seatedWalker]),'a seated guest must not block its own departure');
console.log(JSON.stringify({passed:true,waitingSeats:6,standingAudienceSpots:10,cocktailGlasses:10,entryRoute:true,stageRoutes:16,waitingBodyAvoidance:true,ownSeatDeparture:true}));
