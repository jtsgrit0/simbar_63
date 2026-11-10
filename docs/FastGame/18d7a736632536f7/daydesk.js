import {catalog} from './catalog.js';

export const DESK_ART=['office/desk-jessy.png','office/desk-edison.png','office/desk-jaiden.png'];
const PORTRAITS=['owner-female.png','owner-ff.png','owner-pet.png'];
const CHANNELS=['speech','monitor','phone'];
const esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const action=(type,label,payload={},disabled=false,extra='')=>`<button type="button" data-action="${type}" data-payload="${esc(JSON.stringify(payload))}" ${disabled?'disabled':''} ${extra}>${label}</button>`;

/** A selected-owner office scene. Art is requested only when the desk is opened. */
export function createDayDesk({g,asset,t,name}){
  let started=0,manual=0,holdUntil=0;
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

  function markup(){
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
  function focus(channel,now=performance.now()){
    const index=CHANNELS.indexOf(channel);if(index<0)return;
    manual=index;holdUntil=now+7000;
  }
  function reset(now=performance.now()){started=now;manual=0;holdUntil=0;}
  function update(root,now=performance.now()){
    const desk=root.querySelector('.daydesk');if(!desk)return;
    if(!started)started=now;
    const focused=root.ownerDocument.activeElement,visiblePane=desk.querySelector('.desk-pane:not([hidden])');
    if(focused&&visiblePane?.contains(focused)){
      manual=CHANNELS.indexOf(visiblePane.dataset.deskPane);
      holdUntil=Math.max(holdUntil,now+300);
    }
    const index=now<holdUntil?manual:Math.floor((now-started)/4200)%3,channel=CHANNELS[index];
    desk.dataset.focus=channel;
    for(const pane of desk.querySelectorAll('[data-desk-pane]'))pane.hidden=pane.dataset.deskPane!==channel;
    for(const button of desk.querySelectorAll('[data-desk-channel]'))button.setAttribute('aria-pressed',String(button.dataset.deskChannel===channel));
    const image=desk.querySelector('.desk-scene-image');
    desk.classList.toggle('desk-image-ready',!!(image?.complete&&image.naturalWidth));
    const caption=desk.querySelector('[data-desk-caption]');if(caption)caption.textContent=[t('THINKING ABOUT TONIGHT','오늘 공연 구상 중'),t('WORKING ON THE COMPUTER','컴퓨터로 기획 중'),t('CHECKING THE PHONE','휴대폰으로 일정 확인 중')][index];
  }
  return{markup,focus,reset,update};
}
