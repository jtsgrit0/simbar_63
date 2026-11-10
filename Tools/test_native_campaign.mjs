import assert from 'node:assert/strict';
import {SimBarGame,Booking,catalog,SAVE_KEY} from './WebGLTemplate/FastGame/core.js';
import {Campaign} from './WebGLTemplate/FastGame/campaign.js';

const memory=()=>{const data=new Map();return{getItem:key=>data.get(key)??null,setItem:(key,value)=>data.set(key,String(value))};};
const game=(seed=11)=>new SimBarGame({storage:memory(),seed});
const option=(g,id)=>g.metrics().campaignOptions.find(show=>show.id===id);
const payment=g=>({cash:g.cash,xp:g.journey.xp,nights:g.journey.completedNights,
  tickets:g.journey.resultTicketIncome,fee:g.journey.resultArtistFee,gross:g.journey.resultTicketGross});
function confirm(g,id){
  assert.equal(g.phase,'planning');
  assert.equal(g.act('selectShow',{showId:id}).success,true,id);
  assert.equal(g.act('deskWork',{task:0}).success,true);
  assert.equal(g.act('deskWork',{task:1}).success,true);
}
function open(g,id){
  confirm(g,id);const cash=g.cash;
  assert.equal(g.act('launch').success,true);
  assert.equal(g.cash,cash,'Booking fees must be paid from the ticket receipt, not twice');
  while(g.phase==='guide')assert.equal(g.act('guide').success,true);
  assert.equal(g.phase,'playing');
}

// Play the actual service timer, random floor prompts, closing choreography and
// confirmation. No result fields, positions, secondsLeft or settlement guards
// are changed by this driver. The player serves roughly once per eight seconds
// and misses every seventh attempt rather than playing every order perfectly.
function playNight(g,{serve=true}={}){
  const items=[],start=g.elapsed;let nextServe=g.elapsed,attempt=0,mode='good',checkedSecondSet=false;
  const doubleBill=g.journey.booking.showId==='double';
  if(doubleBill){assert.equal(g.state.playlist,0);assert.equal(g.journey.campaignSecondSet,false);}
  for(let frame=0;frame<14000&&g.phase!=='results';frame++){
    if(g.farewellPending)g.act('farewellAck');
    if(g.reaction?.activeKind)g.act('quick',{kind:g.reaction.activeKind});
    if(doubleBill&&g.playing&&!checkedSecondSet&&g.journey.secondsLeft<=90){
      assert.equal(g.state.playlist,1,'The second booked band must take over halfway through the double bill');
      assert.equal(g.journey.campaignSecondSet,true);
      const restored=new SimBarGame({storage:memory()});
      assert.equal(restored.importSave(g.exportSave()),true);restored.act('continue');
      assert.equal(restored.state.playlist,1);assert.equal(restored.journey.campaignSecondSet,true);
      assert.equal(restored.journey.secondsLeft,g.journey.secondsLeft);
      checkedSecondSet=true;
    }
    if(serve&&g.playing&&!g.paused){
      if(g.pour){
        const p=g.pour,target=mode==='miss'?.20:p.target+.09;
        if(p.elapsed>=.16&&p.marker>=target){
          const r=g.act('pourStop');
          if(r.success)nextServe=g.elapsed+7;
        }
      }else if(g.orders.length&&g.elapsed>=nextServe){
        const item=g.orders[0].item,r=g.act('prepare');
        if(r.success){items.push(item);attempt++;mode=attempt%7===0?'miss':'good';}
      }
    }
    g.tick(.05);
  }
  assert.equal(g.phase,'results',JSON.stringify({day:g.journey.day,elapsed:g.elapsed-start,floor:g.floor,afterShow:g.journey.afterShow}));
  assert.equal(g.floorTasksPending,false);
  if(doubleBill)assert(checkedSecondSet,'The complete double bill never reached its second set');
  assert.equal(g.journey.afterShow.noticeAcknowledged,true);
  assert.equal(g.journey.resultTicketIncome,g.journey.resultTicketGross-g.journey.resultArtistFee);
  assert(g.journey.resultTicketIncome>=0,'A low-wallet show must be able to pay its fee from tickets');
  const receipt=payment(g),restored=new SimBarGame({storage:g.storage});
  restored.act('continue');restored.tick(1);restored.act('farewellAck');
  assert.deepEqual(payment(restored),receipt,'Reloading completed receipts must not repeat cash, XP, fees or progression');
  assert.equal(Booking.complete(g.journey.booking,g.journey.bookingCareer,3).success,false);
  assert.deepEqual(payment(g),receipt,'Repeated completion must not alter the wallet');
  const outcome={day:g.journey.day,show:g.journey.booking.showId,seconds:Math.round(g.elapsed-start),
    served:g.journey.resultServed,stars:g.journey.resultStars,profit:g.journey.resultProfit,
    cash:g.cash,attendance:g.journey.resultAttendance,gross:g.journey.resultTicketGross,
    fee:g.journey.resultArtistFee,net:g.journey.resultTicketIncome,items};
  console.log('NIGHT',JSON.stringify(outcome));
  return outcome;
}

// A new owner cannot buy every facility before their first show. Night count
// gates give the wallet a purpose without taking purchased facilities away.
{
  const g=game();assert.equal(g.cash,650);assert.equal(g.state.fans,0);
  g.act('start');assert.equal(Booking.isCampaign(g.journey.booking),true);
  assert.deepEqual(g.goalTargets(),[8,2,3]);
  assert.equal(option(g,'freewill').locked,false);
  assert.equal(option(g,'july').locked,true);assert.equal(option(g,'double').locked,true);
  for(const type of ['hire','upgrade'])for(let index=0;index<3;index++)
    assert.equal(g.act(type,{index}).success,false,`${type} ${index} before first night`);
  assert.equal(g.act('selectShow',{showId:'july'}).success,false);
  assert.equal(g.act('selectShow',{showId:'missing'}).success,false);
  assert.equal(g.act('launch').success,false);
  assert.equal(g.cash,650);
  for(const level of [1,2,3]){
    const required=[1,3,5][level-1];
    assert.equal(Campaign.purchaseGate('upgrade',1,level,required-1).allowed,false);
    assert.equal(Campaign.purchaseGate('upgrade',1,level,required).allowed,true);
  }
  assert.equal(Campaign.purchaseGate('hire',0,1,1).allowed,true);
  assert.equal(Campaign.purchaseGate('hire',1,1,1).allowed,true);
  assert.equal(Campaign.purchaseGate('hire',2,1,1).allowed,false);
  assert.equal(Campaign.purchaseGate('hire',2,1,2).allowed,true);
  const unprepared=Booking.newCampaignPlan(3,0,{completedNights:2,fans:20,upgrades:[0,0,0],career:Booking.newCareer()});
  const double=Booking.campaignOptions(unprepared).find(show=>show.id==='double');
  assert.equal(double.locked,true);assert.equal(double.reason,'sound_required');
}

// Ticket and promotion changes invalidate confirmation and affect forecasts;
// partial plans survive the actual save envelope and lobby continuation.
{
  let g=game();g.act('start');confirm(g,'freewill');
  const standard=g.metrics();
  assert.equal(g.act('ticket',{tier:0}).success,true);assert.equal(g.journey.booking.deskDoneMask,0);
  const affordable=g.metrics();assert(affordable.attendance>=standard.attendance);
  assert(affordable.ticketPrice<standard.ticketPrice);
  confirm(g,'freewill');assert.equal(g.act('ticket',{tier:2}).success,true);
  const premium=g.metrics();assert(premium.attendance<=standard.attendance);
  assert(premium.ticketPrice>standard.ticketPrice);
  const before=g.cash,attendance=premium.attendance;
  assert.equal(g.act('promote').success,true);assert.equal(g.cash,before-60);
  assert(g.metrics().attendance>attendance);
  assert.equal(g.act('promote').success,false);assert.equal(g.cash,before-60);
  g.act('deskWork',{task:0});
  const raw=g.exportSave(),plan=structuredClone(g.journey.booking),cash=g.cash;
  g=new SimBarGame({storage:g.storage});g.act('continue');
  assert.equal(g.phase,'planning');assert.deepEqual(g.journey.booking,plan);assert.equal(g.cash,cash);
  assert.equal(g.act('deskWork',{task:1}).success,true);
  assert.equal(g.act('launch').success,true);while(g.phase==='guide')g.act('guide');
  const locked=structuredClone(g.journey.booking.lockedMetrics),session=g.journey.session;
  assert.equal(g.act('selectShow',{showId:'july'}).success,false);
  assert.equal(g.act('ticket',{tier:0}).success,false);
  assert.equal(g.act('promote').success,false);assert.equal(g.act('launch').success,false);
  assert.deepEqual(g.journey.booking.lockedMetrics,locked);assert.equal(g.journey.session,session);
  assert.equal(g.act('playlist',{index:1}).success,false,'July tracks cannot replace a booked Freewill show');
  assert.equal(g.act('playlist',{index:3}).success,true);
  g.tick(5);const remaining=g.journey.secondsLeft,wallet=g.cash;
  g.save();g=new SimBarGame({storage:g.storage});g.act('continue');
  assert.equal(g.phase,'playing');assert.equal(g.journey.secondsLeft,remaining);
  assert.equal(g.cash,wallet);assert.deepEqual(g.journey.booking.lockedMetrics,locked);
  assert.equal(JSON.parse(raw).version,1,'The retained save envelope remains readable');
}

// Clearing the queue is a real saved state, not a signal to seed a fresh set
// of orders. Resuming must preserve the remaining time to the next arrival.
{
  const g=game(31);g.act('start');open(g,'freewill');
  for(let n=0;n<6&&g.orders.length;n++){
    assert.equal(g.act('prepare').success,true);g.tick(.3);
    assert.equal(g.act('pourStop').success,true);g.tick(.2);
  }
  assert.equal(g.orders.length,0,'The public service actions should clear the opening queue');
  assert(g._requestTimer>.1);
  const requestTimer=g._requestTimer,restored=new SimBarGame({storage:memory()});
  assert.equal(restored.importSave(g.exportSave()),true);restored.act('continue');
  assert.equal(restored.orders.length,0,'Reloading an intentionally empty campaign queue added unearned orders');
  assert.equal(restored._requestTimer,requestTimer);
  restored.tick(.05);assert.equal(restored.orders.length,0);
  for(let n=0;n<200&&!restored.orders.length;n++)restored.tick(.1);
  assert(restored.orders.length>0,'The saved queue must still receive its next normal timed arrival');
}

// Three distinct evenings with ordinary mistakes, a real purchase between
// shows, different music rosters and a single fee deduction on each receipt.
const outcomes=[];
{
  const g=game(11);g.act('start');open(g,'freewill');
  assert.deepEqual(g.metrics().allowedTracks,[0,3,4]);
  const firstPreferences=g.metrics().preferredItems.slice();
  outcomes.push(playNight(g));assert.equal(g.journey.completedNights,1);
  g.act('next');assert.deepEqual(g.goalTargets(),[12,3,4]);
  assert.equal(option(g,'july').locked,false);assert.equal(option(g,'double').locked,true);
  assert(g.cash>=catalog.upgrades[1].cost[0],'A modest first night must fund the first sound investment');
  const purchaseCash=g.cash;
  assert.equal(g.act('upgrade',{index:1}).success,true);
  assert.equal(g.cash,purchaseCash-catalog.upgrades[1].cost[0]);
  assert.equal(g.state.upgrades[1],1);
  assert.equal(g.act('upgrade',{index:1}).success,false,'Level 2 requires further growth');
  open(g,'july');assert.deepEqual(g.metrics().allowedTracks,[1,2]);
  assert.notDeepEqual(g.metrics().preferredItems,firstPreferences);
  assert.equal(g.act('playlist',{index:0}).success,false);
  outcomes.push(playNight(g));assert.equal(g.journey.completedNights,2);
  g.act('next');assert.deepEqual(g.goalTargets(),[16,4,5]);
  assert.equal(option(g,'double').locked,false);
  open(g,'double');assert.equal(g.metrics().lineup.length,2);
  assert.deepEqual(g.metrics().allowedTracks,[0,1,2,3,4]);
  outcomes.push(playNight(g));assert.equal(g.journey.completedNights,3);
  assert.equal(g.state.upgrades[1],1);
  assert(outcomes.every(o=>o.fee>0&&o.gross>=o.fee));
  assert(outcomes[2].attendance>outcomes[0].attendance);
  assert.notDeepEqual(outcomes[0].items,outcomes[1].items);
  g.act('next');assert.equal(Campaign.purchaseGate('upgrade',1,2,g.journey.completedNights).allowed,true);
}

// Poor play and an empty wallet still reach a paid receipt. Artist fees cannot
// trap the player outside the opening-night loop or send cash below zero.
{
  const g=game(2);g.state.money=0;g.act('start');open(g,'freewill');
  const poor=playNight(g,{serve:false});assert(g.cash>0);assert.equal(poor.served,0);
  outcomes.push({...poor,scenario:'empty-wallet-no-service'});
}

// Historical money, hired staff, maxed upgrades and a frozen v1 contract are
// grandfathered; opening the new release cannot confiscate any of them.
{
  const g=game();g.state.money=12340;g.state.staff=[1,1,1];g.state.upgrades=[3,3,3];
  g.journey.booking=Booking.newDeskPlan(1,0);g.act('start');
  g.act('deskSelect',{selected:true});g.act('deskWork',{task:0});g.act('deskWork',{task:1});
  g.act('launch');while(g.phase==='guide')g.act('guide');g.save();
  const raw=JSON.parse(g.storage.getItem(SAVE_KEY)),locked=structuredClone(g.journey.booking.lockedMetrics);
  const restored=new SimBarGame({storage:g.storage});restored.act('continue');
  assert.equal(restored.cash,raw.money);assert.deepEqual(restored.state.staff,[1,1,1]);
  assert.deepEqual(restored.state.upgrades,[3,3,3]);assert.equal(restored.journey.booking.deskVersion,1);
  assert.deepEqual(restored.journey.booking.lockedMetrics,locked);assert.equal(restored.phase,'playing');
}

console.log(JSON.stringify({passed:true,checks:['three full nights through real service/closing/settlement','growth gates and affordable first sound investment','ticket/promotion tradeoffs','saved planning, active contracts and empty queues','booked-artist music restriction','double-bill second set and saved continuation','once-only receipts','empty-wallet recovery','legacy ownership and frozen contract preservation'],outcomes},null,2));
