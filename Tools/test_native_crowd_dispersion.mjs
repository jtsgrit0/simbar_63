import assert from 'node:assert/strict';
import {Audience} from './WebGLTemplate/FastGame/audience.js';
import {SimBarGame} from './WebGLTemplate/FastGame/core.js';
import {walkable} from './WebGLTemplate/FastGame/navigation.js';

const gap=(a,b)=>Math.hypot(a.x-b.x,a.z-b.z);
const regular=a=>a.role==='guest'&&!a.waitingSeat&&!a.tableId&&!['mosh','slam'].includes(a.behavior);
const authored=a=>a.role==='seated'||a.tableId||a.role==='surfer'||a.role==='support'||['mosh','slam'].includes(a.behavior);
const locations=actors=>actors.map(a=>[a.id,a.role,a.homeX,a.homeZ]);
for(const [owner,attendance] of [30,36,24].entries()){
  const tiles=new Set(),layouts=new Set();
  let close=0;
  const options={owner,attendance,showId:'freewill'};
  for(let crowdSeed=1;crowdSeed<=24;crowdSeed++){
    const params={...options,crowdSeed},audience=Audience.build(params),people=audience.filter(regular);
    assert.equal(audience.length,attendance);
    assert.deepEqual(locations(audience),locations(Audience.build(params)));
    layouts.add(JSON.stringify(locations(people)));
    tiles.add(new Set(people.map(p=>[Math.floor(p.x/2),Math.floor(p.z/2)].join(','))).size);
    for(let i=0;i<audience.length;i++){
      const a=audience[i];
      if(a.role!=='seated')assert(walkable(a,false,false,a.layout),a.id);
      for(let j=0;j<i;j++)assert(gap(a,audience[j])>=.38,'Overlapping opening audience');
    }
    for(let i=0;i<people.length;i++)for(let j=0;j<i;j++)if(gap(people[i],people[j])<1.35)close++;
    const visitor=Audience.arrival({...params,actors:audience});
    assert(visitor&&walkable(visitor,false,true,visitor.layout));
    assert(audience.every(a=>gap(a,visitor)>=.53),'Walk-in added to an occupied spot');
  }
  assert.equal(layouts.size,24,'Different performances reused the same layout');
  assert(tiles.size>=3,'Opening layouts differ only in small offsets, not in dispersion');
  assert(close/24<=[8,5,3][owner],'Same-area clustering regressed');
  for(const attendance of [4,12,Audience.capacity(owner)])for(const showId of ['freewill','july','double']){
    const options={owner,attendance,day:3,session:3,showId};
    const fixed=locations(Audience.build(options).filter(authored));
    for(let crowdSeed=1;crowdSeed<=12;crowdSeed++){
      const crowd=Audience.build({...options,crowdSeed});
      assert.equal(crowd.length,attendance);
      assert.deepEqual(locations(crowd.filter(authored)),fixed,'Fixed seats, tables, or physical acts moved');
    }
  }
}
// The wider layouts must still let nearby supporters reach and lift the
// performer without jumping to their destinations or changing the timing.
for(const owner of [0,1,2])for(const crowdSeed of [1,3,6,8,11]){
  const data=new Map(),storage={getItem:k=>data.get(k)??null,setItem:(k,v)=>data.set(k,String(v))};
  const game=new SimBarGame({storage,seed:42});
  game.act('owner',{index:owner});game.journey.tutorialComplete=true;game.act('start');
  game.act('selectShow',{showId:'freewill'});game.act('deskWork',{task:0});game.act('deskWork',{task:1});
  assert(game.act('launch').success);
  game.journey.crowdSeed=crowdSeed;game._buildActors();game.orders=[];
  assert(game.act('festival',{id:'surf'}).success);
  const lead=game.actors.find(a=>a.festivalLead);
  assert(lead,'No surf team formed on a spread layout');
  for(let i=0;i<130&&!(lead.y>.05&&lead.festivalLiftReady);i++)game.tick(.05);
  assert(lead.y>.05&&lead.festivalLiftReady,'Spread layout kept a valid team from lifting');
}
console.log('Seeded crowd dispersion, attendance, fixed seats, walk-in clearance and real surf lifts passed.');
