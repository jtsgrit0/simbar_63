import assert from 'node:assert/strict';
import {SimBarGame,Booking,catalog,SAVE_KEY} from './WebGLTemplate/FastGame/core.js';
import {FREEWILL_INDEX} from './WebGLTemplate/FastGame/booking.js';
import {createAfterShow} from './WebGLTemplate/FastGame/aftershow.js';
import {launchPreparedDesk} from './native_test_workflow.mjs';

const memory=()=>{const data=new Map();return{getItem:key=>data.get(key)??null,setItem:(key,value)=>data.set(key,String(value))};};
// These checks cover retained v1 contracts. New-game campaign behavior is
// exercised separately in test_native_campaign.mjs.
const game=()=>{const g=new SimBarGame({storage:memory(),seed:11});g.journey.booking=Booking.newDeskPlan(1,0);return g;};
const freewillMask=1<<24;
const legacyIds=['amp-sprout-17','lamp-rabbit-quartet','paper-siren-09','rooftop-syllable','pixel-tide-27','pocket-orchard','tin-meteor-06','velvet-rain-check','honey-voltage-32','slow-bridge-11','odd-spoon-orbit','lime-lantern-04','poster-elliword','poster-sign-of-elma','poster-quinzel','poster-birthday-syndrome','poster-signal-intrusion','poster-long-time-no-shit','poster-alone-wolf-elegy-club','poster-rux','poster-yeastrio','poster-oklp-group','poster-lagrangian','poster-newsboy-men'];
assert.deepEqual(catalog.bands.slice(0,24).map(band=>band.id),legacyIds);
assert.equal(catalog.bands.length,26);assert.equal(FREEWILL_INDEX,24);
assert.equal(catalog.bands[24].id,'freewill');assert.equal(catalog.bands[24].isReal,true);
assert.equal(catalog.bands[24].sourceUrl,'https://music.bugs.co.kr/album/20850049');
assert.equal(catalog.bands[25].id,'julywantsmore');
assert(catalog.availableBandIndices.includes(24));

// Old plan factories and pure booking rules remain available for saved contracts.
{
  const plan=Booking.newPlan(1,0),career=Booking.newCareer();
  assert.equal(Booking.isDesk(plan),false);assert.equal(Booking.chance(plan,career,0),66);
  assert.equal(Booking.fee(0),100);assert.equal(Booking.fee(0,1),135);
  assert.equal(Booking.listen(plan,career,0).success,true);assert.equal(plan.blocks,1);
  assert.equal(Booking.chance(plan,career,0),86);
  const result=Booking.offer(plan,career,0,0,1000);
  assert.equal(result.code,Booking.roll(1,0,0,0,0)<86?'accepted':'rejected');
  assert.equal(plan.blocks,2);assert.equal(Booking.offer(plan,career,0,0,1000).code,'already_attempted');
  Booking.setTicket(plan,2);assert.equal(plan.ticketTier,2);
}

// Actual host actions enforce an explicit checkbox, ordered checks and no money effects.
{
  const g=game();g.act('start');const cash=g.cash;
  assert.equal(Booking.isDesk(g.journey.booking),true);assert.equal(g.metrics().hour,10);
  assert.equal('remainingBlocks' in g.metrics(),false);assert.equal('blocks' in g.metrics(),false);
  assert.deepEqual(g.metrics().lineup,[]);assert.equal(g.act('launch').code,'band_required');
  assert.equal(g.act('deskSelect',{selected:'true'}).success,false);
  assert.equal(g.act('deskWork',{task:0}).code,'band_required');
  for(const action of ['scout','listen','offer','promote','ticket','venue'])
    assert.equal(g.act(action,{poster:0,band:0,premium:0,tier:2,venue:3}).success,false,action);
  assert.equal(g.cash,cash);assert.equal(g.journey.booking.venue,0);
  g.act('deskSelect',{selected:true});assert.equal(g.metrics().hour,13);
  assert.deepEqual(g.metrics().lineup.map(band=>band.id),['freewill']);
  assert.equal(g.bookingChance(24),0);assert.equal(g.bookingFee(24),0);
  assert.equal(g.act('deskWork',{task:1}).code,'plan_required');
  assert.equal(g.act('deskWork',{task:'0'}).code,'invalid_task');
  assert.equal(g.act('deskWork',{task:2}).code,'invalid_task');
  g.act('deskWork',{task:0});assert.equal(g.metrics().hour,16);
  assert.equal(g.act('launch').code,'desk_incomplete');
  const planned=JSON.stringify(g.journey.booking);
  assert.equal(g.act('deskWork',{task:0}).code,'already_done');
  assert.equal(JSON.stringify(g.journey.booking),planned);
  g.act('deskWork',{task:1});assert.equal(g.metrics().hour,18);assert.equal(g.metrics().canLaunch,true);
  const confirmed=JSON.stringify(g.journey.booking);
  assert.equal(g.act('deskWork',{task:1}).code,'already_done');
  assert.equal(JSON.stringify(g.journey.booking),confirmed);assert.equal(g.cash,cash);
  g.act('deskSelect',{selected:true});assert.equal(g.journey.booking.deskDoneMask,3);
  g.act('deskSelect',{selected:false});assert.equal(g.journey.booking.deskDoneMask,0);assert.equal(g.metrics().hour,10);
  g.act('deskSelect',{selected:true});assert.equal(g.journey.booking.deskDoneMask,0);
  assert.equal(g.cash,cash);
}

// Every partial desk checkpoint survives the real save envelope and lobby continue.
for(const stage of [0,1,2,3]){
  let g=game();g.act('start');
  if(stage>=1)g.act('deskSelect',{selected:true});
  if(stage>=2)g.act('deskWork',{task:0});
  if(stage>=3)g.act('deskWork',{task:1});
  const plan=JSON.stringify(g.journey.booking),cash=g.cash;
  g=new SimBarGame({storage:g.storage});assert.equal(g.phase,'lobby');g.act('continue');
  assert.equal(g.phase,'planning');assert.equal(JSON.stringify(g.journey.booking),plan);
  assert.equal(g.metrics().hour,[10,13,16,18][stage]);assert.equal(g.cash,cash);
}

// Lock only commits Freewill, snapshots growth and never charges or launches twice.
{
  const g=game();g.act('start');g.journey.bookingCareer.growth[24]=43;
  const cash=g.cash;launchPreparedDesk(g);
  const p=g.journey.booking,session=g.journey.session,metrics=structuredClone(p.lockedMetrics);
  assert.equal(p.bookedMask,freewillMask);assert.equal(p.attemptedMask,freewillMask);
  assert.equal(p.attempts,1);assert.equal(p.blocks,0);assert.equal(p.bookedGrowth[24],43);
  assert.equal(p.bookedGrowth.filter(value=>value!==0).length,1);assert.equal(g.cash,cash);
  assert.equal(p.locked,true);assert.equal(p.hasLockedMetrics,true);
  g.journey.bookingCareer.growth[24]=99;
  assert.deepEqual(Booking.forecast(p),metrics);
  const restored=new SimBarGame({storage:memory()});restored.importSave(g.exportSave());restored.act('continue');
  assert.equal(restored.journey.booking.locked,true);assert.equal(restored.journey.booking.deskDoneMask,3);
  assert.equal(restored.journey.booking.bookedGrowth[24],43);assert.deepEqual(restored.metrics().lineup.map(band=>band.id),['freewill']);
  assert.deepEqual(Booking.forecast(restored.journey.booking),metrics);assert.equal(restored.cash,cash);
  assert.equal(g.act('launch').success,false);assert.equal(g.journey.session,session);
  assert.equal(Booking.lockDesk(p,g.journey.bookingCareer).code,'locked');
  assert.equal(Booking.deskSelect(p,false).code,'locked');assert.equal(g.cash,cash);
  const receipt=Booking.complete(p,g.journey.bookingCareer,3);
  assert.equal(receipt.success,true);const growth=g.journey.bookingCareer.growth.slice();
  assert.equal(Booking.complete(p,g.journey.bookingCareer,3).code,'already_completed');
  assert.deepEqual(g.journey.bookingCareer.growth,growth);
  Booking.sanitize(p,g.journey.bookingCareer);assert.equal(p.completed,true);assert.equal(p.locked,true);
}

function oldDay({opening=1000,trusted=true,inProgress=false,locked=false}={}){
  const g=game(),p=Booking.newPlan(4,2);
  Object.assign(p,{blocks:2,attempts:1,bookedMask:1,attemptedMask:1,promotions:1});p.bookedGrowth[0]=20;
  if(locked)Booking.lock(p);
  Object.assign(g.journey,{day:4,booking:p,dayOpeningCash:opening,hasDayOpeningCash:trusted,inProgress,
    session:inProgress?8:0,settledSession:inProgress?7:0,secondsLeft:73});
  g.state.money=840;g.save();return new SimBarGame({storage:g.storage});
}

// Migration refunds only a trusted opening-wallet debit, once, on daytime entry.
{
  let g=oldDay();const snapshot=JSON.stringify(g.journey.booking);
  assert.equal(g.cash,840);assert.equal(Booking.isDesk(g.journey.booking),false);
  g.act('continue');assert.equal(g.phase,'planning');assert.equal(g.cash,1000);
  assert.equal(g.journey.deskMigrationCredit,160);assert.equal(g.journey.deskMigrationDay,4);
  assert.equal(g.journey.deskMigrationTrustedOpening,true);assert.equal(g.journey.legacyDeskPlanJson,snapshot);
  assert.equal(g.journey.booking.venue,2);assert.equal(g.journey.booking.day,4);
  assert.equal(g.journey.booking.freewillSelected,false);assert.equal(g.journey.booking.deskDoneMask,0);
  g=new SimBarGame({storage:g.storage});g.act('continue');g.act('lobby');g.act('continue');
  assert.equal(g.cash,1000);assert.equal(g.journey.deskMigrationCredit,160);
  const j=JSON.parse(JSON.parse(g.storage.getItem(SAVE_KEY)).journeyJson);
  assert.equal(j.deskMigrationTrustedOpening,true);assert.equal(j.legacyDeskPlanJson,snapshot);
}
for(const options of [{trusted:false},{opening:800},{opening:Infinity},{opening:'1000'}]){
  const g=oldDay(options);g.act('continue');assert.equal(g.cash,840);
  assert.equal(g.journey.deskMigrationCredit,0);assert.equal(g.journey.deskMigrationTrustedOpening,false);
  assert(g.journey.legacyDeskPlanJson);assert.equal(Booking.isDesk(g.journey.booking),true);
}
{
  const g=game();g.journey.booking=Booking.newPlan(1,1);g.journey.dayOpeningCash=g.cash;g.journey.hasDayOpeningCash=true;
  const cash=g.cash;g.act('continue');assert.equal(Booking.isDesk(g.journey.booking),true);
  assert.equal(g.cash,cash);assert.equal(g.journey.deskMigrationCredit,0);assert.equal(g.journey.booking.venue,1);
}

// Locked and older unlocked active-night contracts never migrate or gain desk refunds.
for(const locked of [false,true]){
  const g=oldDay({inProgress:true,locked});
  assert.equal(Booking.isDesk(g.journey.booking),false);assert.equal(g.journey.booking.locked,true);
  const metrics=structuredClone(g.journey.booking.lockedMetrics),cash=g.cash;
  g.act('continue');assert.equal(g.phase,'playing');assert.equal(g.journey.secondsLeft,73);
  assert.equal(Booking.isDesk(g.journey.booking),false);assert.equal(g.cash,cash);
  assert.deepEqual(g.journey.booking.lockedMetrics,metrics);assert.equal(g.journey.legacyDeskPlanJson,'');
}

// An old frozen receipt settles once, and its next day becomes the new desk.
{
  let g=oldDay({inProgress:true,locked:true});g.act('continue');
  const locked=structuredClone(g.journey.booking.lockedMetrics);
  const state=createAfterShow(g.actors,g.journey.session);
  for(const actor of [...state.members,...state.audience])actor.phase='gone';
  state.completed=true;state.noticeAcknowledged=true;g.journey.afterShow=state;
  g.journey.floorClosing=true;g.phase='closing';g._settleNight();
  assert.equal(g.phase,'results');assert.equal(g.journey.booking.completed,true);
  assert.equal(g.journey.resultTicketIncome,Math.floor(locked.ticketIncome*(70+g.journey.resultStars*10)/100));
  assert.deepEqual(g.journey.booking.lockedMetrics,locked);
  const cash=g.cash,xp=g.journey.xp,career=structuredClone(g.journey.bookingCareer);
  g=new SimBarGame({storage:g.storage});g.act('continue');g._settleNight();
  assert.equal(g.cash,cash);assert.equal(g.journey.xp,xp);assert.deepEqual(g.journey.bookingCareer,career);
  g.act('next');assert.equal(g.phase,'planning');assert.equal(Booking.isDesk(g.journey.booking),true);
  assert.equal(g.journey.booking.venue,2);assert.equal(g.journey.booking.freewillSelected,false);assert.equal(g.cash,cash);
}

console.log('PASS: append-only26band catalog, retained v1 desk, ordered/idempotent zero-cost actions,4save checkpoints, once-only launch/receipt, trusted migration credit+fallback, active/locked legacy settlement');
