import assert from 'node:assert/strict';
import {SimBarGame} from './WebGLTemplate/FastGame/core.js';
const memory=()=>{const data=new Map();return{getItem:k=>data.get(k)??null,setItem:(k,v)=>data.set(k,String(v))};};
function night({staff=false}={}){
 const g=new SimBarGame({storage:memory(),seed:31});assert.equal(g.viewServiceActive,false);assert.equal(g.setViewServiceActive(true),false);
 g.act('owner',{index:2});g.act('start');assert.equal(g.setViewServiceActive(true),false);g.act('selectShow',{showId:'freewill'});g.act('deskWork',{task:0});g.act('deskWork',{task:1});assert.equal(g.act('launch').success,true);while(g.phase==='guide')g.act('guide');
 g._requestTimer=10;if(staff){g.state.staff[0]=1;g.state.autoServeTimer=.1;}return g;
}
function ledger(g){const records=[],serve=g._serve.bind(g);g._serve=(item,automatic,quality,context)=>{const order=g.orders[0],result=serve(item,automatic,quality,context);records.push({id:order?.id,item,automatic,quality,viewService:context?.viewService===true,result});return result;};return records;}
const manual=g=>Object.fromEntries(['score','combo','bestCombo','manualPerfects','manualStreak','feverCharge'].map(k=>[k,g.journey[k]]));
const evidence={};

// The frame may repeat true every tick. It cannot restart preparation, add a
// bonus tip, or take credit for manual timing/combos/fever rewards.
{
 const g=night(),front=g.orders[0];front.item=0;g.state.upgrades=[3,3,0];g.state.activeEvent=1;g.state.eventRemaining=40;
 Object.assign(g.journey,{score:1234,combo:3,bestCombo:5,manualPerfects:2,manualStreak:2,feverCharge:.4,feverSeconds:12});
 const records=ledger(g),cash=g.cash,served=g.journey.orders,pours=g.state.pours,prior=manual(g),fifo=g.orders.map(o=>({id:o.id,item:o.item}));g.setViewServiceActive(true);
 for(let i=0;i<59;i++){assert.equal(g.setViewServiceActive(true),true);g.tick(.05);}assert.equal(g.cash,cash);assert.equal(records.length,0);
 g.setViewServiceActive(true);g.tick(.05);assert.equal(records.length,1);assert.equal(records[0].result.code,'view_served');assert.equal(records[0].result.income,17);assert.equal(records[0].result.sale,17);assert.equal(records[0].result.tip,0);assert.equal(g.cash,cash+17);assert.equal(g.journey.drinkIncome,17);assert.equal(g.journey.orders,served+1);assert.equal(g.state.pours,pours+1);assert.deepEqual(manual(g),prior);assert(Math.abs(g.journey.feverSeconds-9)<1e-8);
 assert.equal(g.journey.feedback.guests[front.id].orders,1);g.tick(3);g.tick(3);assert.deepEqual(records.map(r=>({id:r.id,item:r.item})),fifo);assert(records.every(r=>r.automatic&&r.quality===.7&&r.result.tip===0));assert.deepEqual(manual(g),prior);evidence.salesAndFIFO=records.map(r=>r.result);
}

// Existing event sale bonuses remain real revenue; event tip boosts still do
// not become money in this mode.
for(const [event,expected]of [[0,19],[2,19]]){
 const g=night();g.orders[0].item=0;g.state.upgrades[0]=3;g.state.activeEvent=event;g.state.eventRemaining=30;const cash=g.cash,records=ledger(g);g.setViewServiceActive(true);g.tick(3);assert.equal(records.length,1);assert.equal(records[0].result.tip,0);assert.equal(records[0].result.sale,expected);assert.equal(g.cash,cash+expected);
}

// View service owns the queue exclusively. The purchased barback retains its
// own remaining timer and its existing normal-mode tip, rather than being nerfed.
{
 const g=night({staff:true});g.orders[0].item=0;g.orders[1].item=0;const records=ledger(g),cash=g.cash;g.setViewServiceActive(true);g.tick(3);assert.equal(records.length,1);assert.equal(g.state.autoServeTimer,.1);assert.equal(g.cash,cash+14);g.setViewServiceActive(false);g.tick(.11);assert.equal(records.length,2);assert.equal(records[1].viewService,false);assert.equal(records[1].result.code,'auto_served');assert.equal(records[1].result.income,22);assert.equal(g.cash,cash+14+22);evidence.barback={viewIncome:14,normalIncome:records[1].result.income,normalTimerPreserved:true};
}

// Waiting with no order cannot bank preparation. A new order from the same
// guest is a different order object and must receive its own full preparation.
{
 const g=night();g.orders=[];g._routeOrders();g.setViewServiceActive(true);g.tick(2.9);const guest=g.actors.find(a=>a.role==='guest'&&a.visible!==false);g._assignOrder(guest.id,0);const cash=g.cash;g.tick(.1);assert.equal(g.cash,cash);g.tick(2.9);assert.equal(g.cash,cash+14);evidence.emptyQueueFullPreparation=true;
 const h=night(),records=ledger(h);h.setViewServiceActive(true);h.tick(2);const old=h.orders[0];h.orders[0]={...old};assert.equal(h._serve(old.item,true,.7,{viewService:true,order:old}).code,'stale_order');assert.equal(h.journey.orders,0);records.length=0;h.tick(.99);assert.equal(records.length,0);h.tick(2.01);assert.equal(records.length,1);assert.equal(records[0].result.orderId,old.id);evidence.sameGuestOrderIdentity=true;
}

// Pausing preserves the remaining game-time preparation; leaving the mode
// cancels it. A manual pour is never consumed by the unattended path.
{
 const g=night(),cash=g.cash;g.setViewServiceActive(true);g.tick(1);const remaining=g._viewServiceTimer;g.act('pause');g.tick(5);assert.equal(g._viewServiceTimer,remaining);assert.equal(g.cash,cash);g.act('resume');g.tick(1.99);assert.equal(g.cash,cash);g.tick(.01);assert(g.cash>cash);
 const h=night(),records=ledger(h);h.setViewServiceActive(true);h.tick(2);h.setViewServiceActive(false);h.tick(1);assert.equal(records.length,0);h.setViewServiceActive(true);h.tick(2.99);assert.equal(records.length,0);h.tick(.01);assert.equal(records.length,1);
 const p=night(),front=p.orders[0];assert.equal(p.act('prepare').success,true);p.setViewServiceActive(true);p.tick(2.9);assert.equal(p.orders[0],front);assert.equal(p.journey.orders,0);assert(p.pour);evidence.pauseExitAndPourOwnership=true;
}

// The opt-in is not a save setting and cannot earn money offline or leak into
// a lobby, closing, results, the next planning desk, or a newly opened night.
{
 const g=night();g.setViewServiceActive(true);g.tick(1);const raw=g.exportSave(),loaded=new SimBarGame({storage:g.storage,seed:31});assert.equal(loaded.viewServiceActive,false);loaded.act('continue');assert.equal(loaded.viewServiceActive,false);const cash=loaded.cash;loaded.tick(3);assert.equal(loaded.cash,cash);assert(!Object.hasOwn(JSON.parse(raw),'viewServiceActive'));assert(!raw.includes('_viewServiceTimer'));
 assert.equal(g.importSave(raw,{save:false}),true);assert.equal(g.viewServiceActive,false);g.act('continue');g.setViewServiceActive(true);g.act('lobby');assert.equal(g.viewServiceActive,false);g.act('continue');assert.equal(g.viewServiceActive,false);g.setViewServiceActive(true);g.journey.secondsLeft=.025;const before=g.cash;g.tick(.05);assert.equal(g.phase,'closing');assert.equal(g.viewServiceActive,false);assert.equal(g.cash,before);assert.equal(g.setViewServiceActive(true),false);
 for(let i=0;i<12000&&g.phase!=='results';i++){if(g.farewellPending)g.act('farewellAck');if(g.reaction.activeKind)g.act('quick',{kind:g.reaction.activeKind});g.tick(.05);}assert.equal(g.phase,'results');assert.equal(g.viewServiceActive,false);assert.equal(g.act('next').success,true);assert.equal(g.phase,'planning');assert.equal(g.viewServiceActive,false);g.act('selectShow',{showId:'freewill'});g.act('deskWork',{task:0});g.act('deskWork',{task:1});g.act('launch');while(g.phase==='guide')g.act('guide');assert.equal(g.playing,true);assert.equal(g.viewServiceActive,false);evidence.reloadClosingAndNextNight=true;
}
console.log(JSON.stringify({passed:true,checks:['three-second FIFO/no-tip sale despite tip+fever bonuses','ordinary service feedback and route hooks','no manual score/perfect/combo/fever awards','exclusive view queue consumer and unchanged normal barback tips','empty/new/same-guest order preparation','pause, exit, manual pour ownership','ephemeral save/lobby/closing/results/next-night reset'],evidence},null,2));
