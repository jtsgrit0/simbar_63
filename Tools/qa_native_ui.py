from playwright.sync_api import sync_playwright
from pathlib import Path
import json,time,sys
from benchmark_web_loading import chromium_executable
OUT=Path(sys.argv[2] if len(sys.argv)>2 else 'Research/PixelArtVerification/UI');OUT.mkdir(parents=True,exist_ok=True)
with sync_playwright() as p:
    executable=chromium_executable(None)
    browser=p.chromium.launch(**({'executable_path':executable} if executable else {}))
    context=browser.new_context(viewport={'width':1280,'height':800},device_scale_factor=2,has_touch=True)
    page=context.new_page(); errors=[]
    page.on('pageerror',lambda e:errors.append(str(e)))
    page.goto(sys.argv[1] if len(sys.argv)>1 else 'http://127.0.0.1:8767/');page.wait_for_function('window.simbarGameReadyAt && document.querySelector(".owner-card")',timeout=90000)
    def shot(name):page.screenshot(path=str(OUT/(name+'.png')))
    def click(action,n=0):page.locator('button[data-action="'+action+'"]').nth(n).click();page.wait_for_timeout(150)
    shot('01-title');assert page.locator('button[data-action="owner"]').count()==3
    click('owner',1);assert page.evaluate('simbarGame.owner')==1
    click('start')
    if page.locator('button[data-action="ui:liveBriefingClose"]').count():
        shot('02-live-briefing');click('ui:liveBriefingClose')
    shot('02-posters');assert page.locator('.poster-frame img').count()==1
    assert page.locator('.poster-frame img').get_attribute('src').startswith('data:image/svg+xml')
    # The current C# daily lineup uses the already known twelve fictional acts.
    # Verified real posters remain independently researchable and unlock real acts.
    click('ui:realPosters');assert not page.locator('.poster-frame img').get_attribute('src').startswith('data:')
    click('ui:planPrimary');click('ui:planPrimary');assert page.locator('.artist-row').count()==24
    shot('03-booking');click('listen');click('offer');click('ticket',2)
    if page.locator('button[data-action="promote"]').is_enabled():click('promote')
    click('ui:planPrimary')
    for _ in range(3):click('guide')
    assert page.evaluate('simbarGame.phase')=='playing';shot('04-live')
    assert page.locator('button[data-action="playlist"]').count()==5
    assert page.evaluate('''() => {const panel=document.querySelector('.playlist').getBoundingClientRect();return [...document.querySelectorAll('[data-playlist]')].every(row=>{const r=row.getBoundingClientRect();return r.top>=panel.top&&r.bottom<=panel.bottom;});}''')
    click('ui:musicNext');assert page.evaluate('simbarGame.state.playlist')==1
    click('ui:musicPrevious');assert page.evaluate('simbarGame.state.playlist')==0
    click('ui:musicPrevious');assert page.evaluate('simbarGame.state.playlist')==4
    click('ui:musicToggle');assert page.locator('button[data-action="ui:musicToggle"]').inner_text()=='▶'
    click('ui:musicToggle');assert page.locator('button[data-action="ui:musicToggle"]').inner_text()=='Ⅱ'
    click('ui:service');shot('05-menu');assert page.locator('.service-item').count()==6,page.evaluate('({phase:simbarGame.phase,paused:simbarGame.paused,reason:simbarGame.pauseReason})')
    item=page.evaluate('simbarGame.orders[0].item');page.locator('.service-item').nth(item).click()
    page.wait_for_timeout(200);assert page.evaluate('!!simbarGame.pour');shot('06-pour')
    deadline=time.monotonic()+3
    while time.monotonic()<deadline:
        if page.evaluate('simbarGame.pour && Math.abs(simbarGame.pour.marker-simbarGame.pour.target)<.04'):break
        page.wait_for_timeout(15)
    page.keyboard.press('Space');page.wait_for_timeout(200);assert page.evaluate('simbarGame.journey.orders')==1
    click('ui:staff');shot('07-staff');click('hire',0);assert page.evaluate('simbarGame.state.staff[0]')==1;click('ui:close')
    click('ui:upgrades');click('upgrade',0);assert page.evaluate('simbarGame.state.upgrades[0]')==1;click('ui:close')
    click('ui:events');click('event',0);assert page.evaluate('simbarGame.state.activeEvent')==0;click('ui:close')
    click('ui:floor');before=page.evaluate('simbarGame.journey.secondsLeft');page.wait_for_timeout(700);assert not page.evaluate('simbarGame.paused') and page.evaluate('simbarGame.journey.secondsLeft')<before;shot('08-floor-readonly');click('ui:floorClose')
    click('ui:settings');click('language',1);assert page.evaluate('simbarGame.language')=='en';shot('09-english');click('ui:close')
    page.set_viewport_size({'width':393,'height':852});page.wait_for_timeout(200);click('ui:service');shot('10-mobile-menu');click('ui:close')
    page.keyboard.press('p');click('lobby');shot('11-mobile-title');click('continue');assert page.evaluate('simbarGame.phase')=='playing'
    page.set_viewport_size({'width':844,'height':390});page.wait_for_timeout(200);shot('12-landscape-live')
    assert not errors,errors
    result={'passed':True,'errors':errors,'phase':page.evaluate('simbarGame.phase'),'served':page.evaluate('simbarGame.journey.orders'),'dailyPoster':True,'realPosterResearch':True,'musicControls':True}
    (OUT/'result.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result))
    context.close();browser.close()
