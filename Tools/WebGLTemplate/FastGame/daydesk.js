import {catalog} from './catalog.js';

export const DESK_ART=['office/desk-jessy.png','office/desk-edison.png','office/desk-jaiden.png'];
const PORTRAITS=['owner-female.png','owner-ff.png','owner-pet.png'];
const CHANNELS=['speech','monitor','phone'];
const esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const action=(type,label,payload={},disabled=false,extra='')=>`<button type="button" data-action="${type}" data-payload="${esc(JSON.stringify(payload))}" ${disabled?'disabled':''} ${extra}>${label}</button>`;

/** A selected-owner office scene. Art is requested only when the desk is opened. */
export function createDayDesk({g,asset,t,name}){
  if(typeof document!=='undefined'&&!document.querySelector('link[data-daydesk-dialogue-style]')){
    const stylesheet=document.createElement('link');stylesheet.rel='stylesheet';stylesheet.href=new URL('./daydesk-dialogue.css',import.meta.url).href;stylesheet.dataset.daydeskDialogueStyle='';document.head.append(stylesheet);
  }
  let manual=0,dialogueTurn=0,dialogueBand='freewill',lastShowId='',workPanel=null,workScroll=0;
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
    const selectedName=showName(selected),items=selected?.preferredItems||[],hour=m.hour??10;
    const thought=isConfirmed?t('The lineup is confirmed. Check the forecast and open the doors when you’re ready.','출연진 일정 확인 완료. 예상 정산을 살피고 준비가 되면 문을 열자.'):isPlanned?t('The plan is saved. I’ll confirm the artists on the phone.','공연 기획 저장 완료. 휴대폰에서 출연진 일정을 확인하자.'):selected?t('Let’s set ticket prices and promotion for this crowd before saving the plan.','이번 관객에 맞춰 티켓 가격과 홍보를 정하고 기획을 저장하자.'):t('Every show brings a different crowd. Who should play tonight?','공연마다 찾아오는 관객이 달라. 오늘은 누구를 무대에 세울까?');
    const ready=typeof m.canLaunch==='boolean'?m.canLaunch:!!(selected&&p.deskDoneMask===3);
    const options=(m.campaignOptions||[]).map(entry=>{const show=entry.show||entry,chosen=p.showId===show.id,reason=t(entry.reasonEn,entry.reasonKo)||(typeof entry.reason==='object'?name(entry.reason):entry.reason);return action('selectShow',`<span class="show-card-top"><strong>${esc(showName(show))}</strong><b>${chosen?t('SELECTED ✓','선택됨 ✓'):entry.locked?t('LOCKED','잠김'):t('SELECT','선택')}</b></span><span class="show-card-description">${esc(t(show.descriptionEn||'',show.descriptionKo||''))}</span><span class="show-card-fee">${t('ARTIST FEE','출연료')} ${money(show.fee)} · ${t('SETTLED AFTER THE SHOW','공연 후 정산')}</span>${entry.locked?`<span class="show-card-lock">${esc(reason||t('Complete more nights to unlock.','영업을 더 완료하면 열립니다.'))}</span>`:''}`,{showId:show.id},!!entry.locked,`class="desk-show-card ${chosen?'selected':''} ${entry.locked?'locked':''}" aria-pressed="${chosen}"`);}).join('');
    const forecast=`<section class="desk-forecast" aria-label="${t('Ticket forecast','티켓 예상 정산')}"><div><span>${t('EXPECTED AUDIENCE','예상 관객')}</span><strong>${m.attendance||0}<small> / ${m.venueCapacity||'—'}</small></strong></div><dl><div><dt>${t('GROSS TICKETS · MAX','티켓 총매출 · 최대')}</dt><dd>${money(m.ticketIncome)}</dd></div><div><dt>${t('ARTIST FEE','출연료')}</dt><dd>−${money(m.artistFee)}</dd></div><div class="net"><dt>${t('NET TICKETS · MAX','티켓 순정산 · 최대')}</dt><dd>${money(m.netTicketIncome)}</dd></div></dl><p>${t('Maximum forecast. Objective stars realize 80–100% of ticket sales. Artist fees come out of settlement, with no upfront payment.','최대 예상치입니다. 목표 달성 별점에 따라 티켓 매출의 80–100%를 정산합니다. 출연료는 선불 없이 정산 시 차감됩니다.')}</p></section>`;
    return `<section class="daydesk campaign-desk" data-owner-index="${o}" data-focus="${CHANNELS[manual]}" aria-label="${esc(t('Live house booking office','라이브하우스 공연 기획실'))}">
      <header class="desk-header"><div><div class="desk-eyebrow">SIMBAR · ${g.journey.day}${t(' DAY','일 차')} · ${t('CHAPTER','성장 단계')} ${stage}/3</div><h1>${esc(ownerName())}${t('’S BOOKING OFFICE','의 공연 기획실')}</h1></div><div class="desk-header-actions"><span class="desk-cash">${t('CASH','보유 현금')}<strong>${money(g.state.money)}</strong></span><time>${String(hour).padStart(2,'0')}:00</time>${action('ui:language',g.language==='en'?'한국어':'ENGLISH')}${action('lobby',t('SAVE · LOBBY','저장 · 로비'))}</div></header>
      <main class="daydesk-scroll"><section class="desk-visual"><div class="desk-stage"><div class="desk-loading-owner"><img src="${esc(asset(PORTRAITS[o]))}" alt="${esc(ownerName())}" draggable="false"><span>${t('Opening the office…','기획실을 여는 중…')}</span></div><img class="desk-scene-image" src="${esc(asset(DESK_ART[o]))}" alt="${esc(ownerName()+t(' planning tonight at a desk','의 공연 기획 장면'))}" draggable="false"><div class="desk-speech"><span class="desk-speaker">${esc(ownerName())}</span><p>${esc(thought)}</p><i></i></div><div class="desk-monitor-light" aria-hidden="true"><b>${t('SHOW PLAN','공연 기획')}</b><span>${esc(selectedName)}</span><i></i><i></i><em></em></div><div class="desk-phone-light" aria-hidden="true"><span>LIVE HOUSE</span><i></i><i></i><b>···</b></div></div><div class="desk-scene-caption"><span class="desk-record-dot"></span>${esc(ownerName())} · <span data-desk-caption></span></div><section class="desk-growth"><span>${t('CHAPTER','성장 단계')} ${stage} / 3</span><h2>${stageTitle(stage)}</h2><p>${nextUnlock(stage)}</p><div class="desk-invest-actions">${action('ui:staff',t('HIRE STAFF','직원 고용'))}${action('ui:upgrades',t('UPGRADE THE VENUE','시설 투자'))}</div></section>${forecast}</section>
      <aside class="desk-console"><nav class="desk-channels" aria-label="${esc(t('Switch work screen','업무 화면 전환'))}">${[[t('MEMO','메모'),'speech','◌'],[t('SHOW PLAN','공연 기획'),'monitor','▣'],[t('CONFIRM','일정 확인'),'phone','▯']].map(([label,channel,icon])=>action('ui:deskFocus',`<span aria-hidden="true">${icon}</span>${label}`,{channel},false,`data-desk-channel="${channel}" aria-pressed="${CHANNELS[manual]===channel}"`)).join('')}</nav>
      <section class="desk-pane desk-thought-pane" data-desk-pane="speech"><div class="desk-pane-top">${t('TONIGHT STARTS HERE','오늘의 공연은 여기서 시작')}</div><h2>${stageTitle(stage)}</h2><p class="desk-thought">${esc(thought)}</p><ol class="desk-task-list">${[[!!selected,t('Choose a show, ticket price and promotion','공연·티켓 가격·홍보 선택')],[isPlanned,t('Save the show plan','공연 기획 저장')],[isConfirmed,t('Confirm the artists’ schedule','출연진 일정 확인')]].map(([done,label],i)=>`<li class="${done?'done':''}"><b>0${i+1}</b><span>${label}</span><i>${done?'✓':'—'}</i></li>`).join('')}</ol>${action('ui:deskFocus',t('PLAN TONIGHT’S SHOW →','오늘의 공연 기획 →'),{channel:'monitor'},false,'class="desk-main-action"')}</section>
      <section class="desk-pane desk-computer-pane" data-desk-pane="monitor" hidden><div class="desk-pane-top">DESKTOP / ${t('LINEUP & TICKETS','출연진과 티켓')}</div><h2>${t('WHO’S PLAYING TONIGHT?','오늘의 출연진은?')}</h2><div class="desk-show-list">${options}</div><div class="desk-ticket-options"><h3>${t('TICKET PRICE','티켓 가격')}</h3><div>${[0,1,2].map(tier=>action('ticket',money(10+tier*5),{tier},!selected,`class="${p.ticketTier===tier?'selected':''}" aria-pressed="${p.ticketTier===tier}"`)).join('')}</div><small>${t('Lower prices draw more guests. Higher prices earn more per ticket.','낮은 가격은 관객 수를, 높은 가격은 1인당 매출을 높입니다.')}</small></div><div class="desk-promotion"><div><strong>${t('LOCAL PROMOTION','동네 공연 홍보')}</strong><small>${t('Once per night: +8 expected guests.','하루 한 번: 예상 관객 +8명.')} (${p.promotions||0}/1)</small></div>${action('promote',t('PROMOTE ','홍보 ')+money(60),{},!selected||(p.promotions||0)>=1||g.state.money<60)}</div>${selected?`<p class="desk-crowd-hint">${t('CROWD FAVORITES · +20% TIPS','관객 선호 메뉴 · 팁 +20%')} · ${items.map(i=>esc(name(catalog.drinks[i]))).join(' / ')||'—'}${p.showId==='double'?'<br>'+t('FIRST SET: Freewill · SECOND SET: July (after 90 sec)','1부: 프리윌 · 2부: 줄라이원츠모어 (90초 후 교대)'):''}</p>`:''}${action('deskWork',isPlanned?t('PLAN SAVED ✓','기획 저장 완료 ✓'):t('SAVE SHOW PLAN','공연 기획 저장'),{task:0},!selected||isPlanned,'class="desk-main-action"')}${isPlanned?action('ui:deskFocus',t('CONFIRM ON THE PHONE →','휴대폰에서 일정 확인 →'),{channel:'phone'},false,'class="desk-secondary-action"'):''}</section>
      <section class="desk-pane desk-mobile-pane" data-desk-pane="phone" hidden><div class="desk-pane-top">PHONE / ${t('ARTIST CONFIRMATION','출연진 일정 확인')}</div><h2>${esc(selectedName)}</h2><div class="desk-phone-display"><div class="desk-contact"><span>♪</span><div><strong>${esc(selectedName)}</strong><small>${t('TONIGHT’S LIVE LINEUP','오늘의 라이브 출연진')}</small></div></div><p class="desk-message mine">${selected?t('Let’s confirm tonight’s lineup and ticket plan.','오늘 출연진과 티켓 기획을 확인할게요.'):t('Choose your show on the computer first.','먼저 컴퓨터에서 공연을 선택하세요.')}</p>${isConfirmed?`<p class="desk-message reply">${t('Confirmed! See you on stage tonight.','일정 확인 완료! 오늘 무대에서 만나요.')}</p>`:`<div class="desk-message-await">${isPlanned?t('The saved plan is ready to confirm.','저장한 기획대로 일정을 확인합니다.'):t('Save the show plan before confirming.','공연 기획을 저장한 뒤 확인할 수 있습니다.')}</div>`}</div>${action('deskWork',isConfirmed?t('LINEUP CONFIRMED ✓','출연진 확인 완료 ✓'):t('CONFIRM TONIGHT’S LINEUP','오늘 출연진 일정 확인'),{task:1},!selected||!isPlanned||isConfirmed,'class="desk-main-action"')}<p class="desk-confirm-note">${t('Changing the show, ticket price or promotion clears confirmation.','공연·티켓 가격·홍보를 바꾸면 기획과 일정 확인을 다시 진행합니다.')}</p></section></aside></main>
      <footer class="desk-footer"><div><strong>${ready?t('READY · ','준비 완료 · ')+esc(selectedName):isPlanned?t('NEXT · CONFIRM THE LINEUP ON THE PHONE','다음 · 휴대폰에서 출연진 일정 확인'):t('NEXT · CHOOSE AND SAVE THE SHOW PLAN','다음 · 공연을 선택하고 기획 저장')}</strong><span>${esc(g.message||nextUnlock(stage))}</span></div>${action('launch',t('OPEN TONIGHT’S VENUE →','밤 영업 시작 →'),{},!ready,'class="desk-launch"')}</footer></section>`;
  }
  /** Keep the authored campaign controls intact while presenting their real state as a conversation. */
  function dialogueMarkup(){
    const p=plan(),m=g.metrics(),o=owner(),selected=m.show,isPlanned=planned(),isConfirmed=confirmed(),stage=m.campaignStage||p.campaignStage||1;
    if(lastShowId!==(p.showId||'')){lastShowId=p.showId||'';dialogueTurn=0;dialogueBand=p.showId==='july'?'july':'freewill';}
    const band=p.showId==='double'?dialogueBand:p.showId==='july'?'july':'freewill';
    const bandName=band==='july'?t('July Wants More','줄라이원츠모어'):t('Freewill','프리윌');
    const speakingBand=!!selected&&dialogueTurn===0,speaker=speakingBand?bandName:ownerName(),ready=!!m.canLaunch;
    const selectedName=showName(selected),tracks=(selected?.tracks||[]).map(i=>esc(name(catalog.playlists[i]))).join(' · ')||'—';
    let copy;
    if(!selected)copy=t('Every show brings a different crowd. Who should play tonight?\nChoose a show, check the forecast, then save the plan and confirm by phone.','공연마다 찾아오는 관객이 달라요. 오늘은 누구를 무대에 세울까요?\n공연을 선택한 뒤 예상 정산을 확인하고, 기획 저장과 전화 확인을 차례로 진행해요.');
    else if(isConfirmed)copy=speakingBand?t('The schedule is confirmed! See you on stage tonight.\nCheck the plan once more, then open the venue when you’re ready.','공연 일정 확인 완료! 오늘 무대에서 만나요.\n선택한 기획을 한 번 더 살펴보고, 준비가 되면 영업을 시작해 주세요.'):t('The lineup and schedule are confirmed.\nLet’s open the venue and welcome tonight’s crowd.','출연진과 공연 일정까지 확인했어요.\n이제 문을 열고 오늘의 관객을 맞이해 볼까요?');
    else if(isPlanned)copy=speakingBand?t('The show plan is saved.\nPlease confirm the artists’ schedule by phone before opening the venue.','공연 기획이 저장되었네요.\n공연을 시작하기 전에 전화로 출연진 일정을 최종 확인해 주세요.'):t('The show plan is saved.\nI’ll confirm the artists’ schedule by phone and get the venue ready.','공연 기획 저장을 마쳤어요.\n전화로 출연진 일정을 확인하고 오늘의 공연을 준비할게요.');
    else if(speakingBand)copy=p.showId==='double'?t('Tonight’s double bill features Freewill and July Wants More.\nPlease check the combined artist fee, expected crowd and all five tracks.','오늘은 프리윌과 줄라이원츠모어의 합동 공연이에요.\n두 팀의 출연료와 예상 관객, 공연에서 재생할 다섯 곡을 확인해 주세요.'):band==='july'?t('Let’s take a look at July Wants More Live.\nOur tracks are Hey and No Matter How. Please check the fee and audience forecast.','이번에는 줄라이원츠모어 라이브를 살펴볼까요?\n공연 음원은 Hey와 No Matter How예요. 출연료와 예상 관객도 확인해 주세요.'):t('Let’s play Freewill Live tonight.\nPlease check the artist fee, expected crowd and the tracks in tonight’s show.','오늘은 프리윌 라이브로 함께해요.\n출연료와 예상 관객을 확인하고, 공연에서 재생할 곡도 살펴봐 주세요.');
    else copy=t('The artist fee comes out of ticket settlement after the show.\nI’ll check the tracks, save the plan, then confirm the schedule by phone.','좋아요. 출연료는 공연 후 티켓 정산에서 반영되는군요.\n음원 목록까지 확인한 뒤, 기획을 저장하고 전화로 최종 확인할게요.');
    const source=document.createElement('template');source.innerHTML=campaignMarkup();
    const header=source.content.querySelector('.desk-header');header.className='dialogue-header';header.querySelector('h1').textContent=t('SHOW PLANNING · BOOKING','공연 기획 · 섭외');
    const controls=source.content.querySelector('.desk-console'),forecast=source.content.querySelector('.desk-forecast'),growth=source.content.querySelector('.desk-growth');
    controls.classList.add('dialogue-work-panel');controls.querySelector('[data-desk-pane="phone"]').prepend(forecast);controls.append(growth);
    controls.insertAdjacentHTML('afterbegin',`<header class='dialogue-work-heading'><h2>${t('SHOW PLAN · TICKETS · FORECAST','공연 기획 · 티켓 · 예상 정산')}</h2>${action('ui:deskFocus',t('RETURN TO DIALOGUE','대화로 돌아가기'),{channel:'speech'},false,'class="dialogue-work-close"')}</header>`);
    const feedback=g.message||(!selected?t('Choose tonight’s show first.','오늘의 공연을 먼저 선택하세요.'):isConfirmed?t('Phone confirmation complete. The show is ready.','전화 확인 완료. 공연을 시작할 수 있어요.'):isPlanned?t('Show planned. Confirm by phone.','공연 기획 완료. 전화로 최종 확인하세요.'):t('Check the artist fee, expected crowd and tracks.','출연료 · 예상 관객 · 음원을 확인하세요.'));
    const save=()=>action('deskWork',isPlanned?t('PLAN SAVED ✓','기획 저장 완료 ✓'):t('SAVE PLAN','기획 저장'),{task:0},!selected||isPlanned,'class="dialogue-save"');
    const confirm=()=>action('deskWork',isConfirmed?t('CONFIRMED ✓','전화 확인 완료 ✓'):t('CONFIRM BY PHONE','전화 확인'),{task:1},!selected||!isPlanned||isConfirmed,'class="dialogue-confirm"');
    return `<section class='daydesk campaign-desk dialogue-desk' data-owner-index='${o}' data-focus='${CHANNELS[manual]}' aria-label='${esc(t('Live house booking office','라이브하우스 공연 기획실'))}'>
      <main class='daydesk-scroll dialogue-scroll'><div class='dialogue-layout'>
        <img class='dialogue-office-background desk-scene-image' src='${esc(asset(DESK_ART[o]))}' alt='' aria-hidden='true' draggable='false'><div class='dialogue-scene-shade' aria-hidden='true'></div>
        ${header.outerHTML}
        <nav class='dialogue-steps' aria-label='${t('Show preparation','공연 준비 순서')}'>${action('ui:deskFocus',t('SHOW · TICKETS','공연 선택 · 기획 설정'),{channel:'monitor'},false,'data-desk-channel="monitor"')}${save()}<i>›</i>${confirm()}<i>›</i>${action('launch',t('OPEN TONIGHT’S VENUE','공연 시작'),{},!ready,'class="dialogue-launch"')}</nav>
        <section class='dialogue-stage' aria-label='${t('Owner and artists’ conversation','사장과 출연진 대화')}'>
          <figure class='dialogue-owner-illustration ${speakingBand?'inactive':'active'}'><img src='${esc(asset(PORTRAITS[o]))}' alt='${esc(ownerName())}' draggable='false'><figcaption><b>${esc(ownerName())}</b><span>${t('OWNER','사장')}</span></figcaption></figure>
          <figure class='dialogue-band-illustration ${speakingBand?'active':'inactive'}'><div class='dialogue-band-crop'>${selected?`<img class='${band==='july'?'dialogue-july-illustration':'dialogue-freewill-illustration'}' src='${esc(asset('dialogue/'+(band==='july'?'july-lineup-approved.png':'freewill-stage-approved.png')))}' alt='${esc(bandName+t(' original group illustration',' 기존 밴드 일러스트'))}' draggable='false'>`:`<div class='dialogue-empty'><strong>${t('WHO’S PLAYING TONIGHT?','오늘의 출연진은?')}</strong>${action('ui:deskFocus',t('CHOOSE TONIGHT’S SHOW','오늘의 공연 선택'),{channel:'monitor'})}</div>`}</div><figcaption><b>${selected?esc(bandName):t('SELECT THE LINEUP','출연진 선택')}</b><span>${t('ARTISTS','출연진')}</span>${p.showId==='double'?`<div class='dialogue-band-switch'>${action('ui:deskDialogue',t('FREEWILL','프리윌'),{band:'freewill'},false,`aria-pressed='${band==='freewill'}'`)}${action('ui:deskDialogue',t('JULY WANTS MORE','줄라이원츠모어'),{band:'july'},false,`aria-pressed='${band==='july'}'`)}</div>`:''}</figcaption></figure>
        </section>
        <section class='dialogue-facts-strip' aria-label='${t('Selected show forecast','선택한 공연 예상 정보')}'><div><span>${t('TONIGHT','오늘의 공연')}</span><strong>${esc(selectedName)}</strong></div><div><span>${t('ARTIST FEE','출연료')}</span><strong>${money(m.artistFee)}</strong></div><div><span>${t('AUDIENCE','예상 관객')}</span><strong>${m.attendance||0} / ${m.venueCapacity||'—'}${t('','명')}</strong></div><div><span>${t('NET TICKETS · MAX','티켓 순정산 · 최대')}</span><strong>${money(m.netTicketIncome)}</strong></div><div class='dialogue-tracks'><span>${t('TRACKS','공연 음원')}</span><strong>${tracks}</strong></div></section>
        <section class='dialogue-box' aria-live='polite'><div class='dialogue-speaker-name'>${esc(speaker)}</div><div class='dialogue-topline'><span>${esc(selectedName)} · ${speakingBand?t('ARTISTS’ REPLY','출연진 응답'):t('OWNER’S REPLY','사장 응답')}</span><small>${selected?dialogueTurn+1:1} / ${selected?2:1}</small></div><p class='dialogue-copy'>${esc(copy)}</p><div class='dialogue-bottom'><span class='dialogue-feedback'>${esc(feedback)}</span><div class='dialogue-actions'>${action('ui:deskDialogue',t('PREVIOUS','이전 대사'),{step:'previous'},!selected||dialogueTurn===0)}${save()}${confirm()}${action('ui:deskDialogue',t('NEXT ▶','다음 대사 ▶'),{step:'next'},!selected||dialogueTurn===1,'class="dialogue-next"')}</div></div></section>
        <footer class='dialogue-footer'><span>${esc(nextUnlock(stage))}</span><div>${action('ui:staff',t('HIRE STAFF','직원 고용'))}${action('ui:upgrades',t('FACILITY INVESTMENT','시설 투자'))}${action('ui:deskFocus',t('FORECAST · PHONE','정산 · 일정 확인'),{channel:'phone'},false,'data-desk-channel="phone"')}</div></footer>
      </div></main>
      <div class='dialogue-work-overlay' ${manual===0?'hidden':''}>${controls.outerHTML}</div>
    </section>`;
  }
  function markup(){
    if(workPanel?.isConnected)workScroll=workPanel.scrollTop;
    return plan()?.deskVersion===2?dialogueMarkup():legacyMarkup();
  }
  function focus(channel){
    const index=CHANNELS.indexOf(channel);if(index<0)return;
    if(manual!==index){workPanel=null;workScroll=0;}
    manual=index;
  }
  function dialogue({step,band}={}){
    if(plan()?.deskVersion!==2)return;
    if(step==='next')dialogueTurn=Math.min(1,dialogueTurn+1);
    if(step==='previous')dialogueTurn=Math.max(0,dialogueTurn-1);
    if(plan()?.showId==='double'&&(band==='freewill'||band==='july'))dialogueBand=band;
  }
  function reset(){manual=0;dialogueTurn=0;dialogueBand='freewill';lastShowId='';workPanel=null;workScroll=0;}
  function update(root,now=performance.now()){
    const desk=root.querySelector('.daydesk');if(!desk)return;
    const index=manual,channel=CHANNELS[index];
    desk.dataset.focus=channel;
    const work=desk.querySelector('.dialogue-work-overlay');if(work)work.hidden=manual===0;
    for(const pane of desk.querySelectorAll('[data-desk-pane]'))pane.hidden=pane.dataset.deskPane!==channel;
    for(const button of desk.querySelectorAll('[data-desk-channel]'))button.setAttribute('aria-pressed',String(button.dataset.deskChannel===channel));
    const panel=desk.querySelector('.dialogue-work-panel');if(panel&&panel!==workPanel){workPanel=panel;panel.scrollTop=workScroll;}
    const image=desk.querySelector('.desk-scene-image');
    desk.classList.toggle('desk-image-ready',!!(image?.complete&&image.naturalWidth));
    const caption=desk.querySelector('[data-desk-caption]');if(caption)caption.textContent=[t('THINKING ABOUT TONIGHT','오늘 공연 구상 중'),t('WORKING ON THE COMPUTER','컴퓨터로 기획 중'),t('CHECKING THE PHONE','휴대폰으로 일정 확인 중')][index];
  }
  return{markup,focus,dialogue,reset,update};
}
