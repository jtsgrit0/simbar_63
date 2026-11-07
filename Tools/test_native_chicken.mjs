import {launchPreparedDesk} from './native_test_workflow.mjs';
import assert from 'node:assert/strict';
import {SimBarGame,catalog} from './WebGLTemplate/FastGame/core.js';
import {pixelPendingOrders} from './WebGLTemplate/FastGame/world.js';

const storage=()=>{const data=new Map();return{getItem:k=>data.get(k)||null,setItem:(k,v)=>data.set(k,String(v)),removeItem:k=>data.delete(k)};};
const open=(seed=1)=>{const g=new SimBarGame({seed,storage:storage()});g.act('start');launchPreparedDesk(g);while(g.phase==='guide')g.act('guide');return g;};
const bubbles=g=>pixelPendingOrders({phase:g.phase,actors:g.actors,orders:g.orders});
function awaitChicken(g){const randomItems=[];for(let n=0;n<200;n++){const front=g.orders[0];assert(front);randomItems.push(front.item);if(front.item===5)return randomItems;assert.equal(g._serve(front.item,true,.7).success,true);}assert.fail('Chicken never occurred through normal replacement-order RNG.');}

assert.equal(catalog.drinks.length,6);
assert.deepEqual(catalog.drinks.slice(0,5).map(d=>[d.nameEnglish,d.price,d.isFood]),[
  ['FAUST',14,false],['ADIOS MOTHERFUCKER',12,false],['PEPPERONI HALF PIZZA',9,true],['CHILI CHEESE FRIES',14,true],['CHILI CHEESE CHIPS',14,true]
],'The existing five menu choices and prices must stay intact.');
assert.deepEqual([catalog.drinks[5].ko,catalog.drinks[5].en,catalog.drinks[5].price,catalog.drinks[5].isFood],['후라이드 치킨','FRIED CHICKEN',18,true]);

const g=open(),randomItems=awaitChicken(g),id=g.orders[0].id;
assert(bubbles(g).some(b=>b.actor.id===id&&b.item===5),'A real chicken order needs the sixth icon column.');
assert.equal(g.act('menuItem',{item:0}).success,false);
assert.equal(g.pour,null);assert(bubbles(g).some(b=>b.actor.id===id&&b.item===5),'A wrong selection cannot clear the chicken order.');
g.tick(.2);
assert.equal(g.act('menuItem',{item:5}).success,true);
assert.equal(g.pour.item,5);assert.equal(g.pour.orderId,id);assert.equal(g.selectedItem,5);
g.tick(.2);g.act('pause');
const restored=new SimBarGame({storage:g.storage});
assert.equal(restored.selectedItem,5);
assert.equal(restored.pour,null,'Preparation remains transient across reload for every menu item.');
assert.equal(restored.orders[0].id,id);assert.equal(restored.orders[0].item,5);
restored.act('continue');
assert.equal(restored.act('prepare').success,true);assert.equal(restored.pour.item,5);
restored.tick(.2);assert.equal(restored.act('pourStop').success,true);
assert(!bubbles(restored).some(b=>b.actor.id===id),'Successful manual serving must remove the served customer bubble.');

const automatic=open(2);awaitChicken(automatic);const served=automatic.orders[0].id;
assert.equal(automatic._serve(5,true,.7).success,true);
assert(!automatic.orders.some(o=>o.id===served));assert(!bubbles(automatic).some(b=>b.actor.id===served));

const old=open(3),oldOrders=old.orders.map(o=>[o.id,o.item]),oldRaw=old.exportSave();
assert(oldOrders.every(([,item])=>item<5));
const legacy=new SimBarGame({storage:storage()});assert.equal(legacy.importSave(oldRaw),true);legacy.act('continue');
assert.deepEqual(legacy.orders.map(o=>[o.id,o.item]),oldOrders,'Five-item era pending orders still load unchanged.');

const closing=open(4);awaitChicken(closing);
for(let n=0;n<40&&closing.phase==='playing';n++)closing.tick(5);
assert.equal(closing.phase,'closing');assert.equal(closing.orders.length,0);assert.equal(bubbles(closing).length,0);
console.log(JSON.stringify({passed:true,checks:['Original five preserved','Random chicken orders','Wrong selection preserved','Menu item5 accepted','Selected/pending chicken survives transient-prep reload','Manual serving clears','Automatic serving clears','Old five-item save loads','Closing clears'],randomItems}));
