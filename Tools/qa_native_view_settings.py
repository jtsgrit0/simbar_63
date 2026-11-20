"""Exercise viewing-mode settings, focus loss and real local audio in a browser.

Uses disposable profiles, denies external requests, and serves a local WAV for
the R2 tracks. Pass --package to test an assembled build; source is the default.
"""
import argparse, io, json, math, re, struct, traceback, wave
from pathlib import Path
from playwright.sync_api import sync_playwright
import qa_native_audio_feedback as shared

ROOT=Path(__file__).resolve().parent.parent
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--package',type=Path)
parser.add_argument('--output',type=Path,default=ROOT/'Temp/ViewSettingsApply-20261121/qa')
args=parser.parse_args(); OUT=args.output.resolve(); OUT.mkdir(parents=True,exist_ok=True)
report={'passed':False,'cases':[],'errors':[],'blockedRemote':[]}

data=io.BytesIO()
with wave.open(data,'wb') as output:
 output.setnchannels(1);output.setsampwidth(2);output.setframerate(8000)
 output.writeframes(b''.join(struct.pack('<h',int(900*math.sin(2*math.pi*220*i/8000))) for i in range(30*8000)))
WAV=data.getvalue()
INIT='''(()=>{window.qaAudio=[];const A=window.Audio;window.Audio=function(...args){const a=new A(...args);qaAudio.push(a);return a};window.Audio.prototype=A.prototype})()'''
SNAP='''()=>({paused:simbarGame.paused,reason:simbarGame.pauseReason,elapsed:simbarGame.elapsed,remaining:simbarGame.journey.secondsLeft,language:simbarGame.language,viewing:simbarApplication.viewMode.viewing,hidden:document.hidden,settings:!!document.querySelector('.settings-ref'),pause:!!document.querySelector('.pause-ref'),audio:qaAudio.map(a=>({paused:a.paused,time:a.currentTime,ready:a.readyState}))})'''

def case(browser,url,width,height):
 context=browser.new_context(viewport={'width':width,'height':height},locale='ko-KR');context.add_init_script(INIT)
 def remote(route):
  request=route.request
  if re.match(r'https://pub-[^/]+\.r2\.dev/.*\.mp3(?:\?.*)?$',request.url,re.I):route.fulfill(status=200,content_type='audio/wav',body=WAV,headers={'Accept-Ranges':'bytes','Cache-Control':'no-store'})
  else:report['blockedRemote'].append({'url':request.url,'method':request.method});route.abort()
 context.route(re.compile(r'^https?://(?!127\.0\.0\.1[:/]|localhost[:/])',re.I),remote)
 page=context.new_page();page.on('pageerror',lambda e:report['errors'].append(str(e)))
 result={'viewport':[width,height]};report['cases'].append(result)
 try:
  page.goto(url,wait_until='domcontentloaded');page.wait_for_function('window.simbarGameReadyAt>0',timeout=45000)
  page.evaluate('''()=>{const g=simbarGame;g.act('owner',{index:2});g.act('language',{language:'ko'});g.journey.completedNights=2;g.journey.tutorialComplete=true;g.act('start');g.act('selectShow',{showId:'freewill'});g.act('deskWork',{task:0});g.act('deskWork',{task:1});if(!g.act('launch').success)throw Error('launch failed');while(g.phase==='guide')g.act('guide');}''')
  shared.close_order(page);page.locator('[data-action="ui:liveTab"][data-payload*="music"]').click()
  page.wait_for_function('qaAudio.some(a=>!a.paused&&a.readyState>=3)')
  page.locator('button[data-view-mode]').click();page.wait_for_function("simbarApplication.viewMode.viewing&&document.querySelector('.fast-game').dataset.viewModeTransition==='settled'")
  settings=page.locator('.hud-settings');assert settings.count()==1 and settings.is_visible();settings.click()
  page.locator('.settings-ref').wait_for(state='visible');page.wait_for_function('simbarGame.paused')
  before=page.evaluate(SNAP);page.wait_for_timeout(500);during=page.evaluate(SNAP)
  assert before['elapsed']==during['elapsed'] and before['remaining']==during['remaining'],(before,during)
  assert any(not a['paused'] and a['time']>0 for a in during['audio']),during
  page.locator('.settings-ref [data-action="language"][data-payload*="en"]').click();page.wait_for_function("simbarGame.language==='en'")
  page.locator('.settings-ref [data-action="language"][data-payload*="ko"]').click();page.wait_for_function("simbarGame.language==='ko'");shared.settle(page)
  page.locator('.settings-ref [data-action="ui:close"]').scroll_into_view_if_needed();page.locator('.settings-ref [data-action="ui:close"]').click()
  page.wait_for_function("!document.querySelector('.settings-ref')&&!simbarGame.paused");assert page.evaluate('simbarApplication.viewMode.viewing')
  result['settingsPausedAndRestored']=True;result['languageToggles']=True
  # Simulate visibility loss in Chromium; the game scheduler and audio stay real.
  page.evaluate('''()=>{Object.defineProperty(document,'hidden',{configurable:true,get:()=>true});document.dispatchEvent(new Event('visibilitychange'));window.dispatchEvent(new Event('blur'))}''')
  start=page.evaluate(SNAP);page.wait_for_timeout(2200);end=page.evaluate(SNAP)
  assert end['elapsed']>start['elapsed'] and end['remaining']<start['remaining'] and not end['paused'],(start,end)
  assert not end['settings'] and not end['pause'],end
  assert any(not a['paused'] and a['time']>start['audio'][i]['time']+.2 for i,a in enumerate(end['audio'])),(start,end)
  page.evaluate('''()=>{delete document.hidden;document.dispatchEvent(new Event('visibilitychange'));window.dispatchEvent(new Event('focus'))}''');result['simulatedBackgroundClockAndAudio']=True
  # Explicit pause through core remains effective even while the page is backgrounded.
  page.evaluate("simbarGame.act('pause',{reason:'qa-manual'})");held=page.evaluate(SNAP)
  page.evaluate('''()=>{Object.defineProperty(document,'hidden',{configurable:true,get:()=>true});document.dispatchEvent(new Event('visibilitychange'));window.dispatchEvent(new Event('blur'))}''');page.wait_for_timeout(800);still=page.evaluate(SNAP)
  assert still['paused'] and still['elapsed']==held['elapsed'] and still['remaining']==held['remaining'],(held,still)
  page.evaluate('''()=>{delete document.hidden;document.dispatchEvent(new Event('visibilitychange'));window.dispatchEvent(new Event('focus'))}''');page.evaluate("simbarGame.act('resume')");page.wait_for_function('!simbarGame.paused')
  result['manualPauseAndResume']=True
  # The approved pause control returns to management before opening its existing dialog.
  page.locator('.hud-settings').click();page.locator('.settings-ref').wait_for(state='visible')
  pause_button=page.locator('.settings-ref [data-action="ui:holdPause"]');pause_button.scroll_into_view_if_needed();pause_button.click()
  page.wait_for_function("!simbarApplication.viewMode.viewing&&document.querySelector('.fast-game').dataset.viewModeTransition==='settled'&&document.querySelector('.pause-ref')&&simbarGame.paused")
  paused=page.evaluate(SNAP);page.wait_for_timeout(500);still_paused=page.evaluate(SNAP)
  assert still_paused['elapsed']==paused['elapsed'] and still_paused['remaining']==paused['remaining'],(paused,still_paused)
  assert any(not a['paused'] for a in still_paused['audio']),still_paused
  page.locator('.pause-ref [data-action="resume"]').click();page.wait_for_function("!simbarGame.paused&&!document.querySelector('.pause-ref')")
  resumed=page.evaluate(SNAP);page.wait_for_timeout(300);assert page.evaluate(SNAP)['elapsed']>resumed['elapsed']
  result['settingsPauseSwitchAndResume']=True;result['passed']=True
 finally:context.close()

server,url=shared.serve(args.package.resolve() if args.package else ROOT,source=not args.package)
try:
 with sync_playwright() as pw:
  browser=pw.chromium.launch(executable_path=shared.chromium_executable(None),args=['--mute-audio'])
  for width,height in [(1600,900),(1024,768),(844,390)]:case(browser,url,width,height)
  browser.close()
 assert not report['errors'],report['errors'];report['passed']=all(c.get('passed') for c in report['cases'])
except Exception:report['failure']=traceback.format_exc();raise
finally:server.shutdown();(OUT/'report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print(json.dumps({'passed':report['passed'],'cases':len(report['cases']),'blockedRemote':len(report['blockedRemote'])}))
