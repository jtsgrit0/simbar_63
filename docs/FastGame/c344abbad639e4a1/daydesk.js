import {Encore} from './desk-encore.js';
import {catalog} from './catalog.js';

export const DESK_ART=['office/desk-jessy.png','office/desk-edison.png','office/desk-jaiden.png'];
const PORTRAITS=['owner-female.png','owner-ff.png','owner-pet.png'];
const CHANNELS=['speech','monitor','phone'];
const esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const action=(type,label,payload={},disabled=false,extra='')=>`<button type="button" data-action="${type}" data-payload="${esc(JSON.stringify(payload))}" ${disabled?'disabled':''} ${extra}>${label}</button>`;

/** A selected-owner office scene. Art is requested only when the desk is opened. */
export function createDayDesk({g,asset,t,name}){
  let manual=0;
  const plan=()=>g.journey.booking;
  const owner=()=>Math.max(0,Math.min(2,g.owner|0));
  const ownerName=()=>name(catalog.owners[owner()]);
  const planned=()=>!!(plan()?.deskDoneMask&1);
  const confirmed=()=>!!(plan()?.deskDoneMask&2);
  const speech=()=>confirmed()
    ?t('Freewill is confirmed. Let’s get the venue ready for tonight!','프리윌 일정 확인 완료! 이제 오늘 밤의 공연을 준비하자.')
    :planned()
      ?t('The running order is saved. I’ll confirm with Freewill on my phone.','공연 기획은 저장했어. 휴대폰으로 프리윌 일정을 확인하자.')
      :plan()?.freewillSelected
        ?t('Freewill tonight! First, I’ll save the show plan on the computer.','오늘은 프리윌과 함께! 먼저 컴퓨터에 공연 기획을 저장하자.')
        :t('Who’s playing tonight? I’ll check Freewill on the booking list.','오늘 공연을 준비해 볼까? 섭외 목록에서 프리윌을 체크하자.');
  const status=()=>confirmed()?t('READY FOR TONIGHT','오늘의 공연 준비 완료')
    :planned()?t('NEXT: CONFIRM ON THE PHONE','다음: 휴대폰으로 일정 확인')
      :plan()?.freewillSelected?t('NEXT: SAVE THE COMPUTER PLAN','다음: 컴퓨터에서 기획 저장')
        :t('NEXT: CHECK FREEWILL ON THE COMPUTER','다음: 컴퓨터에서 프리윌 체크');

  function legacyMarkup(){
    const p=plan(),o=owner(),isPlanned=planned(),isConfirmed=confirmed();
    const hour=g.metrics().hour??10;
    return `<section class="daydesk" data-owner-index="${o}" data-focus="speech" aria-label="${esc(t('Daytime owner office','낮의 사장님 기획실'))}">
      <header class="desk-header"><div><div class="desk-eyebrow">SIMBAR · ${g.journey.day}${t(' DAY','일 차')}</div><h1>${esc(ownerName())}${t('’S DAYTIME DESK','의 낮 기획실')}</h1></div><div class="desk-header-actions"><time>${String(hour).padStart(2,'0')}:00</time>${action('ui:language',g.language==='en'?'한국어':'ENGLISH')}${action('lobby',t('SAVE · LOBBY','저장 · 로비'))}</div></header>
      <main class="daydesk-scroll"><section class="desk-visual" aria-label="${esc(ownerName()+t(' working at the desk','의 책상 업무 장면'))}"><div class="desk-stage">
        <div class="desk-loading-owner"><img src="${esc(asset(PORTRAITS[o]))}" alt="${esc(ownerName())}" draggable="false"><span>${t('Opening the office…','기획실을 여는 중…')}</span></div>
        <img class="desk-scene-image" src="${esc(asset(DESK_ART[o]))}" alt="${esc(ownerName()+t(' at a desk with a desktop computer and phone',' — 데스크탑과 휴대폰이 놓인 책상'))}" draggable="false">
        <div class="desk-speech"><span class="desk-speaker">${esc(ownerName())}</span><p>${esc(speech())}</p><i></i></div>
        <div class="desk-monitor-light" aria-hidden="true"><b>${t('SHOW PLAN','공연 기획')}</b><span>FREEWILL</span><i></i><i></i><em></em></div>
        <div class="desk-phone-light" aria-hidden="true"><span>FREEWILL</span><i></i><i></i><b>···</b></div>
        </div><div class="desk-scene-caption"><span class="desk-record-dot"></span>${esc(ownerName())} · <span data-desk-caption></span></div>
      </section><aside class="desk-console" aria-label="${esc(t('Work screens','업무 화면'))}">
        <nav class="desk-channels" aria-label="${esc(t('Switch work screen','업무 화면 전환'))}">${[
          [t('THOUGHTS','말풍선'),'speech','◌'],[t('COMPUTER','컴퓨터'),'monitor','▣'],[t('PHONE','휴대폰'),'phone','▯']
        ].map(([label,channel,icon])=>action('ui:deskFocus',`<span aria-hidden="true">${icon}</span>${label}`,{channel},false,`data-desk-channel="${channel}" aria-pressed="false"`)).join('')}</nav>
        <section class="desk-pane desk-thought-pane" data-desk-pane="speech"><div class="desk-pane-top">${t('TODAY’S WORK','오늘의 업무')}</div><h2>${esc(ownerName())}${t('’S MEMO','의 메모')}</h2><p class="desk-thought">${esc(speech())}</p><ol class="desk-task-list"><li class="${p.freewillSelected?'done':''}"><b>01</b><span>${t('Check Freewill on the list','프리윌 섭외 체크')}</span><i>${p.freewillSelected?'✓':'—'}</i></li><li class="${isPlanned?'done':''}"><b>02</b><span>${t('Save the show plan','컴퓨터에 공연 기획 저장')}</span><i>${isPlanned?'✓':'—'}</i></li><li class="${isConfirmed?'done':''}"><b>03</b><span>${t('Confirm the schedule by phone','휴대폰으로 일정 확인')}</span><i>${isConfirmed?'✓':'—'}</i></li></ol>${action('ui:deskFocus',t('OPEN THE COMPUTER','컴퓨터 화면 보기'),{channel:'monitor'},false,'class="desk-main-action"')}</section>
        <section class="desk-pane desk-computer-pane" data-desk-pane="monitor" hidden><div class="desk-pane-top">DESKTOP / ${t('SHOW PLANNING','공연 기획')}</div><h2>${t('TONIGHT’S BOOKING LIST','오늘의 섭외 목록')}</h2><p class="desk-pane-intro">${t('One band is available for booking right now.','지금 섭외할 수 있는 밴드는 프리윌 한 팀이에요.')}</p><label class="desk-band-option"><input type="checkbox" data-desk-selected ${p.freewillSelected?'checked':''}><span class="desk-band-check" aria-hidden="true"></span><span><strong>${t('FREEWILL','프리윌')} <small>freewill</small></strong><em>${t('INDIE BAND · LIVE SET','인디밴드 · 라이브 공연')}</em></span></label><div class="desk-document"><span>${t('SHOW PLAN','공연 기획서')}</span><strong>${p.freewillSelected?'FREEWILL LIVE':t('CHECK THE BAND ABOVE','위에서 공연할 밴드를 체크하세요')}</strong><div class="desk-document-line"></div><div class="desk-document-line short"></div><small>${isPlanned?t('Saved · now confirm the schedule on the phone.','저장 완료 · 휴대폰에서 일정을 확인하세요.'):t('Save your checked lineup before calling.','체크한 출연진을 저장한 뒤 일정 확인을 진행해요.')}</small></div>${action('deskWork',isPlanned?t('PLAN SAVED ✓','기획 저장 완료 ✓'):t('SAVE SHOW PLAN','공연 기획 저장'),{task:0},!p.freewillSelected||isPlanned,'class="desk-main-action"')}${isPlanned?action('ui:deskFocus',t('CONTINUE ON THE PHONE →','휴대폰으로 이어서 →'),{channel:'phone'},false,'class="desk-secondary-action"'):''}</section>
        <section class="desk-pane desk-mobile-pane" data-desk-pane="phone" hidden><div class="desk-pane-top">PHONE / ${t('BOOKING CONTACT','섭외 일정 확인')}</div><h2>${t('CONFIRM WITH FREEWILL','프리윌 일정 확인')}</h2><div class="desk-phone-display"><div class="desk-contact"><span>F</span><div><strong>${t('Freewill','프리윌 · freewill')}</strong><small>${t('TODAY’S LIVE BAND','오늘의 라이브 밴드')}</small></div></div><p class="desk-message mine">${t('Freewill, let’s confirm tonight’s show schedule.','프리윌, 오늘 공연 일정을 확인할게요.')}</p>${isConfirmed?`<p class="desk-message reply">${t('Schedule confirmed. See you at the show!','공연 일정 확인 완료! 오늘 무대에서 만나요!')}</p>`:`<div class="desk-message-await">${isPlanned?t('Ready to confirm the saved plan.','저장한 기획대로 일정을 확인해요.'):t('Save the plan on the computer first.','먼저 컴퓨터에서 기획을 저장해 주세요.')}</div>`}<div class="desk-typing" aria-hidden="true"><i></i><i></i><i></i></div></div>${action('deskWork',isConfirmed?t('SCHEDULE CONFIRMED ✓','일정 확인 완료 ✓'):t('CONFIRM THE SCHEDULE','공연 일정 확인'),{task:1},!p.freewillSelected||!isPlanned||isConfirmed,'class="desk-main-action"')}</section>
      </aside></main><footer class="desk-footer"><div><strong>${status()}</strong><span>${t('Computer → phone → tonight’s live show','컴퓨터 기획 → 휴대폰 일정 확인 → 밤 라이브 공연')}</span></div>${action('launch',t('OPEN TONIGHT’S VENUE →','밤 영업 시작 →'),{},!(p.freewillSelected&&p.deskDoneMask===3),'class="desk-launch"')}</footer>
    </section>`;
  }
  const money=v=>'₩'+Math.round((+v||0)*1000).toLocaleString('ko-KR');
  const showName=show=>show?name(show):t('Choose tonight’s show','오늘의 공연을 선택하세요');
  const stageTitle=stage=>[t('FIRST OPENING','첫 영업'),t('A DIFFERENT CROWD','새로운 관객'),t('BUILD YOUR LIVE HOUSE','라이브하우스 성장')][Math.min(2,Math.max(0,(stage||1)-1))];
  const nextUnlock=stage=>stage<2?t('Complete tonight to unlock July Wants More and your first investments.','오늘 영업을 마치면 줄라이원츠모어와 첫 직원·시설 투자가 열립니다.'):stage<3?t('Complete two nights and buy Sound & Lights Lv.1 to unlock the double bill.','영업 2회 완료 후 음향·조명 1레벨을 구매하면 합동 공연이 열립니다.'):t('Grow your crew and facilities, then choose the show that fits your house.','직원과 시설을 키우고 우리 매장에 맞는 공연을 선택하세요.');
  function campaignMarkup(){
    const p=plan(),m=g.metrics(),o=owner(),isPlanned=planned(),isConfirmed=confirmed(),selected=m.show,stage=m.campaignStage||p.campaignStage||1;
    const selectedName=showName(selected),items=g.showFavorites(selected),hour=m.hour??10;
    const thought=isConfirmed?t('The lineup is confirmed. Check the forecast and open the doors when you’re ready.','출연진 일정 확인 완료. 예상 정산을 살피고 준비가 되면 문을 열자.'):isPlanned?t('The plan is saved. I’ll confirm the artists on the phone.','공연 기획 저장 완료. 휴대폰에서 출연진 일정을 확인하자.'):selected?t('Let’s set ticket prices and promotion for this crowd before saving the plan.','이번 관객에 맞춰 티켓 가격과 홍보를 정하고 기획을 저장하자.'):t('Every show brings a different crowd. Who should play tonight?','공연마다 찾아오는 관객이 달라. 오늘은 누구를 무대에 세울까?');
    const ready=typeof m.canLaunch==='boolean'?m.canLaunch:!!(selected&&p.deskDoneMask===3);
    const options=(m.campaignOptions||[]).map(entry=>{const show=entry.show||entry,chosen=p.showId===show.id,reason=t(entry.reasonEn,entry.reasonKo)||(typeof entry.reason==='object'?name(entry.reason):entry.reason);return action('selectShow',`<span class="show-card-top"><strong>${esc(showName(show))}</strong><b>${chosen?t('SELECTED ✓','선택됨 ✓'):entry.locked?t('LOCKED','잠김'):t('SELECT','선택')}</b></span><span class="show-card-description">${esc(t(show.descriptionEn||'',show.descriptionKo||''))}</span><span class="show-card-fee">${t('ARTIST FEE','출연료')} ${money(show.fee)} · ${t('SETTLED AFTER THE SHOW','공연 후 정산')}</span>${entry.locked?`<span class="show-card-lock">${esc(reason||t('Complete more nights to unlock.','영업을 더 완료하면 열립니다.'))}</span>`:''}`,{showId:show.id},!!entry.locked,`class="desk-show-card ${chosen?'selected':''} ${entry.locked?'locked':''}" aria-pressed="${chosen}"`);}).join('');
    const forecast=`<section class="desk-forecast" aria-label="${t('Ticket forecast','티켓 예상 정산')}"><div><span>${t('EXPECTED AUDIENCE','예상 관객')}</span><strong>${m.attendance||0}<small> / ${m.venueCapacity||'—'}</small></strong></div><dl><div><dt>${t('GROSS TICKETS · MAX','티켓 총매출 · 최대')}</dt><dd>${money(m.ticketIncome)}</dd></div><div><dt>${t('ARTIST FEE','출연료')}</dt><dd>−${money(m.artistFee)}</dd></div><div class="net"><dt>${t('NET TICKETS · MAX','티켓 순정산 · 최대')}</dt><dd>${money(m.netTicketIncome)}</dd></div></dl><p>${t('Maximum forecast. Objective stars realize 80–100% of ticket sales. Artist fees come out of settlement, with no upfront payment.','최대 예상치입니다. 목표 달성 별점에 따라 티켓 매출의 80–100%를 정산합니다. 출연료는 선불 없이 정산 시 차감됩니다.')}</p></section>`;
    return `<section class="daydesk campaign-desk" data-owner-index="${o}" data-focus="${CHANNELS[manual]}" aria-label="${esc(t('Live house booking office','라이브하우스 공연 기획실'))}">
      <header class="desk-header"><div><div class="desk-eyebrow">SIMBAR · ${g.journey.day}${t(' DAY','일 차')} · ${t('CHAPTER','성장 단계')} ${stage}/3</div><h1>${esc(ownerName())}${t('’S BOOKING OFFICE','의 공연 기획실')}</h1></div><div class="desk-header-actions"><span class="desk-cash">${t('CASH','보유 현금')}<strong>${money(g.state.money)}</strong></span><time>${String(hour).padStart(2,'0')}:00</time>${action('ui:language',g.language==='en'?'한국어':'ENGLISH')}${action('lobby',t('SAVE · LOBBY','저장 · 로비'))}</div></header>
      <main class="daydesk-scroll"><section class="desk-visual"><div class="desk-stage"><div class="desk-loading-owner"><img src="${esc(asset(PORTRAITS[o]))}" alt="${esc(ownerName())}" draggable="false"><span>${t('Opening the office…','기획실을 여는 중…')}</span></div><img class="desk-scene-image" src="${esc(asset(DESK_ART[o]))}" alt="${esc(ownerName()+t(' planning tonight at a desk','의 공연 기획 장면'))}" draggable="false"><div class="desk-speech"><span class="desk-speaker">${esc(ownerName())}</span><p>${esc(thought)}</p><i></i></div><div class="desk-monitor-light" aria-hidden="true"><b>${t('SHOW PLAN','공연 기획')}</b><span>${esc(selectedName)}</span><i></i><i></i><em></em></div><div class="desk-phone-light" aria-hidden="true"><span>LIVE HOUSE</span><i></i><i></i><b>···</b></div></div><div class="desk-scene-caption"><span class="desk-record-dot"></span>${esc(ownerName())} · <span data-desk-caption></span></div><section class="desk-growth"><span>${t('CHAPTER','성장 단계')} ${stage} / 3</span><h2>${stageTitle(stage)}</h2><p>${nextUnlock(stage)}</p><div class="desk-invest-actions">${action('ui:staff',t('HIRE STAFF','직원 고용'))}${action('ui:upgrades',t('UPGRADE THE VENUE','시설 투자'))}</div></section>${forecast}</section>
      <aside class="desk-console"><nav class="desk-channels" aria-label="${esc(t('Switch work screen','업무 화면 전환'))}">${[[t('MEMO','메모'),'speech','◌'],[t('SHOW PLAN','공연 기획'),'monitor','▣'],[t('CONFIRM','일정 확인'),'phone','▯']].map(([label,channel,icon])=>action('ui:deskFocus',`<span aria-hidden="true">${icon}</span>${label}`,{channel},false,`data-desk-channel="${channel}" aria-pressed="${CHANNELS[manual]===channel}"`)).join('')}</nav>
      <section class="desk-pane desk-thought-pane" data-desk-pane="speech"><div class="desk-pane-top">${t('TONIGHT STARTS HERE','오늘의 공연은 여기서 시작')}</div><h2>${stageTitle(stage)}</h2><p class="desk-thought">${esc(thought)}</p><ol class="desk-task-list">${[[!!selected,t('Choose a show, ticket price and promotion','공연·티켓 가격·홍보 선택')],[isPlanned,t('Save the show plan','공연 기획 저장')],[isConfirmed,t('Confirm the artists’ schedule','출연진 일정 확인')]].map(([done,label],i)=>`<li class="${done?'done':''}"><b>0${i+1}</b><span>${label}</span><i>${done?'✓':'—'}</i></li>`).join('')}</ol>${action('ui:deskFocus',t('PLAN TONIGHT’S SHOW →','오늘의 공연 기획 →'),{channel:'monitor'},false,'class="desk-main-action"')}</section>
      <section class="desk-pane desk-computer-pane" data-desk-pane="monitor" hidden><div class="desk-pane-top">DESKTOP / ${t('LINEUP & TICKETS','출연진과 티켓')}</div><h2>${t('WHO’S PLAYING TONIGHT?','오늘의 출연진은?')}</h2><div class="desk-show-list">${options}</div><div class="desk-ticket-options"><h3>${t('TICKET PRICE','티켓 가격')}</h3><div>${[0,1,2].map(tier=>action('ticket',money(10+tier*5),{tier},!selected,`class="${p.ticketTier===tier?'selected':''}" aria-pressed="${p.ticketTier===tier}"`)).join('')}</div><small>${t('Lower prices draw more guests. Higher prices earn more per ticket.','낮은 가격은 관객 수를, 높은 가격은 1인당 매출을 높입니다.')}</small></div><div class="desk-promotion"><div><strong>${t('LOCAL PROMOTION','동네 공연 홍보')}</strong><small>${t('Once per night: +8 expected guests.','하루 한 번: 예상 관객 +8명.')} (${p.promotions||0}/1)</small></div>${action('promote',t('PROMOTE ','홍보 ')+money(60),{},!selected||(p.promotions||0)>=1||g.state.money<60)}</div>${selected?`<p class="desk-crowd-hint">${t('CROWD FAVORITES · +20% TIPS','관객 선호 메뉴 · 팁 +20%')} · ${items.map(i=>esc(name(g.menuEntry(i)))).join(' / ')||'—'}${p.showId==='double'?'<br>'+t('FIRST SET: Freewill · SECOND SET: July (after 90 sec)','1부: 프리윌 · 2부: 줄라이원츠모어 (90초 후 교대)'):''}</p>`:''}${action('deskWork',isPlanned?t('PLAN SAVED ✓','기획 저장 완료 ✓'):t('SAVE SHOW PLAN','공연 기획 저장'),{task:0},!selected||isPlanned,'class="desk-main-action"')}${isPlanned?action('ui:deskFocus',t('CONFIRM ON THE PHONE →','휴대폰에서 일정 확인 →'),{channel:'phone'},false,'class="desk-secondary-action"'):''}</section>
      <section class="desk-pane desk-mobile-pane" data-desk-pane="phone" hidden><div class="desk-pane-top">PHONE / ${t('ARTIST CONFIRMATION','출연진 일정 확인')}</div><h2>${esc(selectedName)}</h2><div class="desk-phone-display"><div class="desk-contact"><span>♪</span><div><strong>${esc(selectedName)}</strong><small>${t('TONIGHT’S LIVE LINEUP','오늘의 라이브 출연진')}</small></div></div><p class="desk-message mine">${selected?t('Let’s confirm tonight’s lineup and ticket plan.','오늘 출연진과 티켓 기획을 확인할게요.'):t('Choose your show on the computer first.','먼저 컴퓨터에서 공연을 선택하세요.')}</p>${isConfirmed?`<p class="desk-message reply">${t('Confirmed! See you on stage tonight.','일정 확인 완료! 오늘 무대에서 만나요.')}</p>`:`<div class="desk-message-await">${isPlanned?t('The saved plan is ready to confirm.','저장한 기획대로 일정을 확인합니다.'):t('Save the show plan before confirming.','공연 기획을 저장한 뒤 확인할 수 있습니다.')}</div>`}</div>${action('deskWork',isConfirmed?t('LINEUP CONFIRMED ✓','출연진 확인 완료 ✓'):t('CONFIRM TONIGHT’S LINEUP','오늘 출연진 일정 확인'),{task:1},!selected||!isPlanned||isConfirmed,'class="desk-main-action"')}<p class="desk-confirm-note">${t('Changing the show, ticket price or promotion clears confirmation.','공연·티켓 가격·홍보를 바꾸면 기획과 일정 확인을 다시 진행합니다.')}</p></section></aside></main>
      <footer class="desk-footer"><div><strong>${ready?t('READY · ','준비 완료 · ')+esc(selectedName):isPlanned?t('NEXT · CONFIRM THE LINEUP ON THE PHONE','다음 · 휴대폰에서 출연진 일정 확인'):t('NEXT · CHOOSE AND SAVE THE SHOW PLAN','다음 · 공연을 선택하고 기획 저장')}</strong><span>${esc(g.message||nextUnlock(stage))}</span></div>${action('launch',t('OPEN TONIGHT’S VENUE →','밤 영업 시작 →'),{},!ready,'class="desk-launch"')}</footer></section>`;
  }
  function petSoundsMarkup(){
    const p=plan(),minutes=p.soundcheckMinutes;
    const chosen=minutes===1050||minutes===1080;
    const time=minutes===1050?'17:30':'18:00';
    const note=chosen?t('Soundcheck saved to the running order.','공연 진행표에 사운드체크 시간을 저장했어요.'):t('Choose one time to add it to tonight\'s running order.','고른 시간을 오늘 공연 진행표에 기록해요.');
    const reply=(portrait,speaker,line,band=false)=>`<div class="pilot-line"><img src="${esc(asset('office/'+portrait))}" alt="" width="58" height="58" draggable="false"><div class="pilot-bubble${band?' pilot-band':''}"><strong>${esc(speaker)}</strong><p>${esc(line)}</p></div></div>`;
    const option=(value,label)=>action('deskSoundcheck',label,{minutes:value},false,`class="pilot-option${minutes===value?' selected':''}" aria-pressed="${minutes===value}"`);
    return `<section class="daydesk petsounds-pilot" data-owner-index="2" aria-label="${t('Pet Sounds first show planning','펫사운즈 첫 공연 기획')}">
      <header class="pilot-header"><div><span>SIMBAR / PET SOUNDS</span><h1>${t("Jaiden's booking office",'제이든의 공연 기획실')}</h1></div><div class="pilot-meta"><b>${t('Show 1 / Freewill','1회차 · 프리윌')}</b>${action('ui:language',g.language==='en'?'한국어':'English')}${action('lobby',t('Lobby','로비'))}</div></header>
      <div class="pilot-main"><figure class="pilot-visual">
        <div class="pilot-art"><img src="${esc(asset('office/pilot-jaiden-desk.png'))}" alt="${t('Jaiden at the Pet Sounds desk','펫사운즈 책상에 앉은 제이든')}" draggable="false"><span>PET SOUNDS / ${t('DAYTIME','낮 기획')}</span></div>
        <figcaption>${esc(note)}</figcaption>
      </figure><section class="pilot-dialogue" aria-labelledby="pilot-heading">
        <span class="pilot-kicker">${t('FREEWILL / SHOW PREP','프리윌 단독 · 공연 준비')}</span><h2 id="pilot-heading">${t('When should we soundcheck?','사운드체크는 몇 시에?')}</h2>
        ${reply('pilot-jaiden.png',t('Jaiden','제이든'),t("Let's do a soundcheck before the show.",'공연 전에 사운드체크 한 번 하죠.'))}
        ${reply('pilot-freewill.png',t('Freewill vocalist','프리윌 보컬'),t('5:30 or 6 works for us. When should we come in?','5시 반도, 6시도 괜찮아요. 언제 갈까요?'),true)}
        <div class="pilot-schedule" aria-live="polite"><span>${t('RUNNING ORDER / SOUNDCHECK','공연 진행표 · 사운드체크')}</span><strong>${chosen?time:t('Not set','시간 미정')}</strong></div>
      </section></div>
      <footer class="pilot-footer"><div class="pilot-options" role="group" aria-label="${t('Soundcheck time','사운드체크 시간')}">${option(1050,t('17:30 · More check time','17:30 · 무대 점검 여유'))}${option(1080,t('18:00 · More travel time','18:00 · 밴드 도착 여유'))}</div>${action('launch',t('Open tonight','오늘 밤 공연 시작'),{},!chosen||!g.metrics().canLaunch,'class="pilot-launch"')}</footer>
    </section>`;
  }
  function julyMarkup(){
    const choice=plan().entranceMusicChoice,chosen=choice==='owner'||choice==='band';
    const ownerChoice=t("Jaiden's pick",'제이든이 고른 입장곡'),bandChoice=t("Band's pick",'밴드가 고른 입장곡');
    const option=(value,label)=>action('deskEntranceChoice',label,{choice:value},false,
      `class="pilot-option${value===choice?' selected':''}" aria-pressed="${value===choice}"`);
    const reply=(portrait,speaker,line,band=false)=>`<div class="pilot-line"><img src="${esc(asset('office/'+portrait))}" alt="" width="58" height="58" draggable="false"><div class="pilot-bubble${band?' pilot-band':''}"><strong>${esc(speaker)}</strong><p>${esc(line)}</p></div></div>`;
    return `<section class="daydesk petsounds-pilot petsounds-july" data-owner-index="2" aria-label="${t('Pet Sounds second show planning','펫사운즈 2회차 공연 기획')}">
      <header class="pilot-header"><div><span>SIMBAR / PET SOUNDS</span><h1>${t("Jaiden's booking office",'제이든의 공연 기획실')}</h1></div><div class="pilot-meta"><b>${t('Show 2 / Solo','2회차 / 단독')}</b>${action('ui:language',g.language==='en'?'한국어':'English')}${action('lobby',t('Lobby','로비'))}</div></header>
      <div class="pilot-main"><figure class="pilot-visual">
        <div class="pilot-art"><img src="${esc(asset('office/pilot-jaiden-desk.png'))}" alt="${t('Jaiden at the Pet Sounds desk','펫사운즈 책상 앞의 제이든')}" draggable="false"><span>PET SOUNDS / ${t('DAYTIME','낮 기획')}</span></div>
        <figcaption>${t('JulyWantsMore solo show','줄라이원츠모어 단독 공연')}</figcaption>
      </figure><section class="pilot-dialogue" aria-labelledby="pilot-heading">
        <span class="pilot-kicker">${t('JULYWANTSMORE / DOORS OPEN','줄라이원츠모어 / 입장 준비')}</span><h2 id="pilot-heading">${t('One song before doors open','문 열기 전 한 곡')}</h2>
        ${reply('pilot-july-jaiden.png',t('Jaiden','제이든'),t('Should we play one of your tracks when doors open?','입장 음악까지 줄라이 곡으로 채울까요?'))}
        ${reply('pilot-july-vocal.png',t('JulyWantsMore vocalist','줄라이원츠모어 보컬'),t('That would be a little embarrassing! We’d rather hear what you usually play here.','아뇨, 그럼 저희도 민망해요. 사장님이 여기서 자주 트는 곡 듣고 싶어요.'),true)}
        <div class="pilot-schedule" aria-live="polite"><span>${t('ENTRANCE MUSIC','입장 음악')}</span><strong>${choice==='owner'?ownerChoice:choice==='band'?bandChoice:t('Not chosen','선택 전')}</strong></div>
      </section></div>
      <footer class="pilot-footer"><div class="pilot-options" role="group" aria-label="${t('Who picks the entrance song?','입장 음악 선택')}">${option('owner',ownerChoice)}${option('band',bandChoice)}</div>${action('launch',t('Open tonight','오늘 밤 공연 시작'),{},!chosen||!g.metrics().canLaunch,'class="pilot-launch"')}</footer>
    </section>`;
  }
  function doubleMarkup(m){
    const p=plan(),choice=p.secondSetIntro,ready=m.campaignOptions.some(show=>show.id==='double'&&!show.locked);
    const freewill=t('Freewill introduces JulyWantsMore','프리윌이 줄라이를 소개'),jaiden=t('Jaiden introduces both bands','제이든이 두 팀을 소개');
    const reply=(portrait,speaker,line,variant='')=>`<div class="pilot-line"><img src="${esc(asset('office/'+portrait))}" alt="" width="58" height="58" draggable="false"><div class="pilot-bubble ${variant}"><strong>${esc(speaker)}</strong><p>${esc(line)}</p></div></div>`;
    const option=(speaker,label)=>action('deskSecondSetIntro',label,{speaker},!ready,`class="pilot-option${choice===speaker?' selected':''}" aria-pressed="${choice===speaker}"`);
    const soundCost=catalog.upgrades[1].cost[0];
    const needsCash=!ready&&g.state.money<soundCost;
    const footer=ready?`<div class="pilot-options" role="group" aria-label="${t('Second-set host','2부 소개 담당')}">${option('freewill',freewill)}${option('owner',jaiden)}</div>${action('launch',t('Open tonight','오늘 밤 공연 시작'),{},!m.canLaunch,'class="pilot-launch"')}`:
      `<div class="pilot-options">${action('ui:upgrades',t('Sound & Lights Lv.1','음향·조명 1레벨'),{},false,'class="pilot-option"')}${needsCash?action('selectShow',t('Plan a solo show instead','단독 공연 먼저 기획'),{showId:'freewill'},false,'class="pilot-option"'):''}</div>${action('launch',t('Open tonight','오늘 밤 공연 시작'),{},true,'class="pilot-launch"')}`;
    return `<section class="daydesk petsounds-pilot petsounds-double" data-owner-index="2" aria-label="${t('Pet Sounds third show planning','펫사운즈 3회차 공연 기획')}">
      <header class="pilot-header"><div><span>SIMBAR / PET SOUNDS</span><h1>${t("Jaiden's booking office",'제이든의 공연 기획실')}</h1></div><div class="pilot-meta"><b>${t('Show 3 / Double bill','3회차 / 합동 공연')}</b>${action('ui:language',g.language==='en'?'한국어':'English')}${action('lobby',t('Lobby','로비'))}</div></header>
      <div class="pilot-main"><figure class="pilot-visual">
        <div class="pilot-art"><img src="${esc(asset('office/pilot-jaiden-desk.png'))}" alt="${t('Jaiden at the Pet Sounds desk','펫사운즈 책상 앞의 제이든')}" draggable="false"><span>PET SOUNDS / ${t('DAYTIME','낮 기획')}</span></div>
        <figcaption class="double-running-order">${t('Set 1: Freewill / Set 2: JulyWantsMore','1부 프리윌 → 2부 줄라이원츠모어')}</figcaption>
      </figure><section class="pilot-dialogue" aria-labelledby="pilot-heading">
        <span class="pilot-kicker">${t('DOUBLE BILL / CHANGEOVER','합동 공연 / 무대 전환')}</span><h2 id="pilot-heading">${t('Who introduces the second set?','누가 2부를 소개할까요?')}</h2>
        ${reply('pilot-july-jaiden.png',t('Jaiden','제이든'),t('JulyWantsMore is up after Freewill. Who should introduce them?','프리윌 무대가 끝나면 줄라이 차례예요. 다음 팀 소개는 누가 할까요?'))}
        ${reply('pilot-freewill.png',t('Freewill vocalist','프리윌 보컬'),t('I can introduce them as I hand over the mic.','제가 마이크 넘기면서 소개할게요. 관객도 다음 팀을 바로 알 수 있게요.'),'pilot-band')}
        ${reply('pilot-july-vocal.png',t('JulyWantsMore vocalist','줄라이원츠모어 보컬'),t('If you introduce us, Jaiden, we can say hello and go straight into our first song.','사장님이 소개해 주시면, 저희는 짧게 인사하고 바로 첫 곡 갈게요.'),'pilot-july-bubble')}
        <div class="pilot-schedule" role="status"><span>${ready?t('INTRO BY','소개 담당'):t('REQUIRED','해금 조건')}</span><strong>${ready?(choice==='freewill'?t('Freewill','프리윌'):choice==='owner'?t('Jaiden','제이든'):t('Not chosen','선택 전')):t('Sound & Lights Lv.1','음향·조명 1레벨')}</strong></div>
      </section></div>
      <footer class="pilot-footer">${footer}</footer>
    </section>`;
  }
  function encoreMarkup(m){
    const p=plan(),scene=Encore.scene(p),effect=Encore.effect(p);
    const local=parts=>t(...parts);
    const ready=m.campaignOptions.some(show=>show.id===p.showId&&!show.locked);
    const choices=m.campaignOptions.filter(show=>!show.locked).map(show=>action('selectShow',esc(name(show)),{showId:show.id},false,
      `class="encore-show${show.id===p.showId?' selected':''}" aria-pressed="${show.id===p.showId}"`)).join('');
    const lines=(portrait,speaker,line,band=false)=>`<div class="pilot-line"><img src="${esc(asset('office/'+portrait))}" alt="" width="58" height="58" draggable="false"><div class="pilot-bubble${band?' pilot-band':''}"><strong>${esc(speaker)}</strong><p>${esc(local(line))}</p></div></div>`;
    const opts=scene?.options.map((option,choice)=>action('deskEncoreChoice',esc(local(option.label)),{choice},!ready,
      `class="pilot-option${p.encoreChoice===choice?' selected':''}" aria-pressed="${p.encoreChoice===choice}"`)).join('')||'';
    const item=Number.isInteger(effect?.item)?g.menuEntry(effect.item):null;
    const isJuly=p.showId==='july';
    const speaker=p.showId==='double'?t('Both bands','두 밴드'):isJuly?t('JulyWantsMore vocalist','줄라이원츠모어 보컬'):t('Freewill vocalist','프리윌 보컬');
    const cue=p.showId==='double'?t('Second-set intro','2부 소개'):m.petSoundsLater?t('House recommendation','오늘의 추천 메뉴'):t('First order','첫 주문');
    const ownerLine=scene?lines('pilot-july-jaiden.png',t('Jaiden','제이든'),scene.jaiden):'';
    const bandLine=scene?lines(isJuly?'pilot-july-vocal.png':'pilot-freewill.png',speaker,scene.band,true):'';
    const dialogue=p.showId==='double'?ownerLine+bandLine:bandLine+ownerLine;
    return `<section class="daydesk petsounds-pilot petsounds-encore" data-owner-index="2" aria-label="${(m.petSoundsLater?t('Pet Sounds show planning','펫사운즈 공연 기획'):t('Pet Sounds fourth show planning','펫사운즈 4회차 공연 기획'))}">
      <header class="pilot-header"><div><span>SIMBAR / PET SOUNDS</span><h1>${t("Jaiden's booking office",'제이든의 공연 기획실')}</h1></div><div class="pilot-meta"><b>${t('Show '+p.day,p.day+'회차')}</b>${action('ui:language',g.language==='en'?'한국어':'English')}${action('lobby',t('Lobby','로비'))}</div></header>
      <div class="pilot-main"><figure class="pilot-visual">
        <div class="encore-shows" role="group" aria-label="${t('Choose a lineup','공연 선택')}">${choices}</div>
        <div class="pilot-art"><img src="${esc(asset('office/pilot-jaiden-desk.png'))}" alt="${t('Jaiden at the Pet Sounds desk','펫사운즈 책상 앞의 제이든')}" draggable="false"></div>
      </figure><section class="pilot-dialogue" aria-labelledby="pilot-heading">
        <span class="pilot-kicker">${t('TONIGHT AT PET SOUNDS','펫사운즈의 오늘 밤')}</span>
        <h2 id="pilot-heading">${esc(scene?local(scene.title):t('Who plays tonight?','오늘은 어떤 공연을 열까요?'))}</h2>
        ${scene?dialogue:`<p class="encore-hint">${t('Choose a lineup to see one situation for tonight.','공연을 고르면 오늘의 상황과 선택지가 나와요.')}</p>`}
        ${scene?`<div class="pilot-schedule" role="status"><span>${cue}</span><strong>${esc(item?name(item):effect?.speaker==='freewill'?t('Freewill','프리윌'):effect?.speaker==='owner'?t('Jaiden','제이든'):t('Not chosen','선택 전'))}</strong></div>`:''}
      </section></div>
      <footer class="pilot-footer"><div class="pilot-options" role="group" aria-label="${t('Tonight’s decision','오늘의 선택')}">${opts}</div>${action('launch',t('Open tonight','오늘 밤 공연 시작'),{},!ready||!m.canLaunch,'class="pilot-launch"')}</footer>
    </section>`;
  }
  function markup(){
    const m=g.metrics();
    if(m.petSoundsEncore||m.petSoundsLater)return encoreMarkup(m);
    if(m.petSoundsDoublePilot)return doubleMarkup(m);
    if(m.petSoundsJulyPilot)return julyMarkup();
    return g.metrics().petSoundsPilot?petSoundsMarkup():plan()?.deskVersion===2?campaignMarkup():legacyMarkup();
  }
  function focus(channel){
    const index=CHANNELS.indexOf(channel);if(index<0)return;
    manual=index;
  }
  function reset(){manual=0;}
  function update(root,now=performance.now()){
    const desk=root.querySelector('.daydesk');if(!desk)return;
    const index=manual,channel=CHANNELS[index];
    desk.dataset.focus=channel;
    for(const pane of desk.querySelectorAll('[data-desk-pane]'))pane.hidden=pane.dataset.deskPane!==channel;
    for(const button of desk.querySelectorAll('[data-desk-channel]'))button.setAttribute('aria-pressed',String(button.dataset.deskChannel===channel));
    const image=desk.querySelector('.desk-scene-image');
    desk.classList.toggle('desk-image-ready',!!(image?.complete&&image.naturalWidth));
    const caption=desk.querySelector('[data-desk-caption]');if(caption)caption.textContent=[t('THINKING ABOUT TONIGHT','오늘 공연 구상 중'),t('WORKING ON THE COMPUTER','컴퓨터로 기획 중'),t('CHECKING THE PHONE','휴대폰으로 일정 확인 중')][index];
  }
  return{markup,focus,reset,update};
}
