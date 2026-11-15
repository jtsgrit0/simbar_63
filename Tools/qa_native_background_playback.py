"""Actual native-hidden-tab playback and game-clock QA.

Chromium is launched separately and connected with no_defaults=True: Playwright
normally forces every page focused/visible, which cannot verify background work.
Only local WAV bytes replace original R2 MP3 responses; all Supabase traffic is
denied. No game, audio events, visibility getters, clocks or media time are faked.
"""
import argparse,atexit,io,json,math,re,socket,struct,subprocess,time,traceback,urllib.request,wave
from pathlib import Path
from playwright.sync_api import sync_playwright
import qa_native_audio_feedback as shared
ROOT=Path(__file__).resolve().parent.parent
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--package',type=Path,required=True);p.add_argument('--output',type=Path,default=ROOT/'Temp/BackgroundPlaybackApply-20261115/browser');p.add_argument('--window-only',action='store_true');a=p.parse_args();OUT=a.output.resolve();OUT.mkdir(parents=True,exist_ok=True)
R={'passed':False,'runtime':re.search(r'FastGame/([a-f0-9]{16})/',(a.package/'index.html').read_text(encoding='utf8'))[1],'cases':[],'errors':[],'httpErrors':[],'blockedRemote':[],'localWAV':[],'nativeVisibilityMethod':'Raw Chromium default context; Playwright CDP no_defaults=True; real tab activation. No visibility emulation/getter overrides.'}
buffer=io.BytesIO()
with wave.open(buffer,'wb') as target:
 target.setnchannels(1);target.setsampwidth(2);target.setframerate(8000);target.writeframes(b''.join(struct.pack('<h',int(700*math.sin(2*math.pi*220*i/8000))) for i in range(4*8000)))
WAV=buffer.getvalue()
INIT=r'''window.backgroundQA={media:[],events:[],visibility:[],ticks:[]};const Native=window.Audio;window.Audio=function(...args){const m=new Native(...args),id=backgroundQA.media.length;backgroundQA.media.push(m);for(const type of ['playing','pause','ended','error'])m.addEventListener(type,e=>backgroundQA.events.push({id,type,trusted:e.isTrusted,time:performance.now(),currentTime:m.currentTime,src:m.currentSrc,hidden:document.hidden}));return m;};window.Audio.prototype=Native.prototype;for(const type of ['visibilitychange','blur','focus'])((type==='visibilitychange'?document:window).addEventListener(type,()=>backgroundQA.visibility.push({type,hidden:document.hidden,focus:document.hasFocus(),time:performance.now()})));'''
SNAP=r'''()=>{const g=simbarGame;return{time:performance.now(),hidden:document.hidden,visibility:document.visibilityState,focus:document.hasFocus(),phase:g.phase,paused:g.paused,pauseReason:g.pauseReason,elapsed:g.elapsed,secondsLeft:g.journey.secondsLeft,money:g.state.money,served:g.journey.orders,income:g.journey.drinkIncome,musicPaused:g.journey.musicPaused,musicIntent:g.journey.musicPauseIntent,index:g.state.playlist,service:g.viewServiceActive,closing:g.viewClosingActive,cleaned:g.floor.cleaned,cleanup:g.floor.cleanupActive,mode:document.querySelector('.fast-game').dataset.viewMode,pausePopup:[...document.querySelectorAll('.pause-shade')].some(e=>getComputedStyle(e).visibility!=='hidden'&&getComputedStyle(e).display!=='none'&&!e.closest('[inert]')),media:backgroundQA.media.map(m=>({time:m.currentTime,paused:m.paused,ended:m.ended,ready:m.readyState,src:m.currentSrc})),events:backgroundQA.events,visibilityEvents:backgroundQA.visibility,ticks:backgroundQA.ticks,workers:g.actors.filter(a=>g.crew.includes(a.id)).map(a=>({id:a.id,x:a.x,z:a.z,action:a.action}))};}'''

def snap(page):return page.evaluate(SNAP)
def raw_browser(pw,dest,headed=False):
 sock=socket.socket();sock.bind(('127.0.0.1',0));port=sock.getsockname()[1];sock.close()
 process=subprocess.Popen([(shared.chromium_executable(None) or pw.chromium.executable_path),*(['--window-position=-2400,-2400','--disable-backgrounding-occluded-windows','--disable-features=CalculateNativeWinOcclusion'] if headed else ['--headless=new']),'--remote-debugging-port='+str(port),'--user-data-dir='+str(dest/'profile'),'--no-first-run','--no-default-browser-check','--disable-background-networking','--disable-component-update','about:blank'],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
 atexit.register(lambda: process.terminate() if process.poll() is None else None)
 for _ in range(80):
  try:urllib.request.urlopen('http://127.0.0.1:'+str(port)+'/json/version',timeout=.5).close();break
  except Exception:time.sleep(.1)
 browser=pw.chromium.connect_over_cdp('http://127.0.0.1:'+str(port),no_defaults=True);return process,browser

def setup(pw,url,name,view=False):
 dest=OUT/name;dest.mkdir(exist_ok=True);process,browser=raw_browser(pw,dest,headed=name=='visible-unfocused-window');context=browser.contexts[0];context.add_init_script(INIT)
 def route(r):
  u=r.request.url
  if re.match(r'https://pub-[^/]+\.r2\.dev/.*\.mp3(?:\?.*)?$',u,re.I):R['localWAV'].append(u);r.fulfill(status=200,content_type='audio/wav',body=WAV,headers={'Accept-Ranges':'bytes','Cache-Control':'no-store'})
  else:R['blockedRemote'].append({'url':u,'method':r.request.method});r.abort()
 context.route(re.compile(r'^https?://(?!127\.0\.0\.1[:/]|localhost[:/])',re.I),route)
 page=context.pages[0];page.set_viewport_size({'width':1600,'height':900});page.on('pageerror',lambda e:R['errors'].append(str(e)));page.on('response',lambda r:R['httpErrors'].append({'url':r.url,'status':r.status}) if r.url.startswith('http://127.0.0.1') and r.status>=400 else None);page.goto(url,wait_until='domcontentloaded');page.wait_for_function('window.simbarGameReadyAt>0',timeout=15000 if name=='visible-unfocused-window' else 60000)
 page.evaluate('''()=>{const g=simbarGame;g.act('owner',{index:2});g.act('language',{language:'ko'});g.journey.completedNights=2;g.journey.day=3;g.journey.tutorialComplete=true;g.act('start');g.act('selectShow',{showId:'freewill'});g.act('deskWork',{task:0});g.act('deskWork',{task:1});if(!g.act('launch').success)throw Error('Launch');while(g.phase==='guide')g.act('guide');const tick=g.tick.bind(g);g.tick=dt=>{const before=g.elapsed;tick(dt);backgroundQA.ticks.push({at:performance.now(),dt,advanced:g.elapsed-before,hidden:document.hidden});};}''')
 shared.close_order(page);page.locator('[data-action="ui:liveTab"][data-payload*="music"]').click();page.wait_for_function('backgroundQA.media.some(m=>!m.paused&&m.readyState>=3)')
 if view:
  box=page.locator('button[data-view-mode]').bounding_box();page.mouse.click(box['x']+box['width']/2,box['y']+box['height']/2);page.wait_for_function("document.querySelector('.fast-game').dataset.viewMode==='viewing'&&document.querySelector('.fast-game').dataset.viewModeTransition==='settled'")
 page.bring_to_front();assert not page.evaluate('document.hidden');return dest,process,browser,context,page

def hide(context,page):
 other=context.new_page();other.goto('about:blank');other.bring_to_front();page.wait_for_function('document.hidden===true',timeout=5000);assert not page.evaluate('document.hasFocus()');return other

def restore(page,other):
 page.bring_to_front();page.wait_for_function('document.hidden===false&&document.hasFocus()',timeout=5000);other.close()
def check_clock(before,after,minimum=4):
 dt=(after['time']-before['time'])/1000;advance=after['elapsed']-before['elapsed'];assert advance>=minimum and .65<=advance/dt<=1.35,(dt,advance);return{'wall':dt,'game':advance,'ratio':advance/dt}
def finish(process,browser):
 try:browser.close()
 finally:
  if process.poll() is None:process.terminate()
  process.wait(timeout=8)

def hidden_case(pw,url,view):
 name='view-hidden' if view else 'management-hidden';r={'name':name};R['cases'].append(r);dest,process,browser,context,page=setup(pw,url,name,view)
 try:
  initial=snap(page);page.screenshot(path=str(dest/'01-before.png'));other=hide(context,page);before=snap(page);page.wait_for_timeout(6200);after=snap(page);r.update(before=before,after=after);assert after['hidden'] and not after['paused'] and not after['pausePopup'];r['hiddenClock']=check_clock(before,after)
  ended=[e for e in after['events'] if e['type']=='ended' and e['time']>before['time']];assert ended and all(e['trusted'] for e in ended),ended;assert after['index']!=before['index'] or len(ended)>=3;assert any(not m['paused'] and m['time']>.1 for m in after['media']);r['nativeEOF']=ended
  if view:assert after['service'] and after['served']>before['served'] and after['income']>before['income']
  restore(page,other);returned=snap(page);page.wait_for_timeout(1600);focused=snap(page);r['returnClock']=check_clock(returned,focused,1);assert not focused['paused'] and not focused['pausePopup'];page.screenshot(path=str(dest/'02-returned.png'))
  if view:
   page.evaluate('simbarGame.journey.secondsLeft=.1');page.wait_for_function("simbarGame.phase==='closing'");page.wait_for_function('simbarGame.viewClosingActive');other=hide(context,page);before=snap(page);page.wait_for_timeout(4500);after=snap(page);assert after['hidden'] and not after['paused'] and not after['pausePopup'] and after['closing'];r['closingClock']=check_clock(before,after,3);r['closingBackground']={'before':before,'after':after};assert before['workers']!=after['workers'] or after['cleaned']>before['cleaned'];restore(page,other)
  r['passed']=True
 finally:finish(process,browser);(dest/'report.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
 print(json.dumps({'case':name,'passed':r.get('passed'),'clock':r.get('hiddenClock')},ensure_ascii=False),flush=True)

def pause_case(pw,url,music):
 name='explicit-music-pause' if music else 'explicit-game-pause';r={'name':name};R['cases'].append(r);dest,process,browser,context,page=setup(pw,url,name)
 try:
  if music:
   page.locator('[data-action="ui:musicToggle"]').click();page.wait_for_function('simbarGame.journey.musicPaused&&backgroundQA.media.every(m=>m.paused)')
  else:
   shared.close_order(page);page.locator('#unity-canvas').focus();page.keyboard.press('p');page.wait_for_function('simbarGame.paused')
  other=hide(context,page);before=snap(page);page.wait_for_timeout(4200);after=snap(page);r.update(before=before,after=after);assert after['hidden']
  if music:
   r['gameContinues']=check_clock(before,after,3);assert after['musicPaused'] and after['musicIntent'] and all(m['paused'] for m in after['media']);assert [m['time'] for m in before['media']]==[m['time'] for m in after['media']];assert not any(e['type']=='playing' and e['time']>before['time'] for e in after['events'])
  else:assert after['paused'] and after['elapsed']==before['elapsed'] and after['money']==before['money'] and after['cleaned']==before['cleaned']
  restore(page,other);returned=snap(page)
  if music:assert returned['musicPaused'] and all(m['paused'] for m in returned['media'])
  else:
   assert returned['paused'];page.locator('.pause-shade [data-action="resume"]').click();page.wait_for_function('!simbarGame.paused');before=snap(page);page.wait_for_timeout(1300);r['manualResume']=check_clock(before,snap(page),.8)
  page.screenshot(path=str(dest/'returned.png'));r['passed']=True
 finally:finish(process,browser);(dest/'report.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
 print(json.dumps({'case':name,'passed':r.get('passed')},ensure_ascii=False),flush=True)

def separate_window_case(pw,url):
 name='visible-unfocused-window';r={'name':name};R['cases'].append(r);dest,process,browser,context,page=setup(pw,url,name)
 try:
  cdp=browser.new_browser_cdp_session();target=cdp.send('Target.createTarget',{'url':'about:blank','newWindow':True});cdp.send('Target.activateTarget',{'targetId':target['targetId']});page.wait_for_function('!document.hasFocus()',timeout=5000);before=snap(page);assert not before['hidden'],'Separate-window scope must exercise visible/unfocused watchdog';page.wait_for_timeout(4500);after=snap(page);r.update(before=before,after=after);assert not after['hidden'] and not after['focus'] and not after['paused'] and not after['pausePopup'];r['clock']=check_clock(before,after,3.5);assert any(e['type']=='ended' and e['trusted'] and e['time']>before['time'] for e in after['events']);cdp.send('Target.closeTarget',{'targetId':target['targetId']});page.bring_to_front();page.wait_for_function('document.hasFocus()');page.screenshot(path=str(dest/'returned.png'));r['passed']=True
 finally:finish(process,browser);(dest/'report.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
 print(json.dumps({'case':name,'passed':r.get('passed'),'clock':r.get('clock')},ensure_ascii=False),flush=True)

server,url=shared.serve(a.package.resolve())
try:
 with sync_playwright() as pw:
  if a.window_only:separate_window_case(pw,url)
  else:hidden_case(pw,url,False);hidden_case(pw,url,True);pause_case(pw,url,False);pause_case(pw,url,True);separate_window_case(pw,url)
 assert not R['errors'] and not R['httpErrors'],R;R['passed']=all(c.get('passed') for c in R['cases'])
except Exception:R['failure']=traceback.format_exc();raise
finally:server.shutdown();(OUT/'report.json').write_text(json.dumps(R,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print(json.dumps({'passed':R['passed'],'runtime':R['runtime'],'cases':len(R['cases']),'errors':R['errors'],'remoteDenied':len(R['blockedRemote']),'localWAV':len(R['localWAV'])},ensure_ascii=False))
