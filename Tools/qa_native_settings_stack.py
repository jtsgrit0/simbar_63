"""Native click/hit-test QA for lobby/results settings stacking only.

Compares the modal's existing computed geometry/typography against an optional
baseline package. A completed-results UI fixture avoids replaying gameplay.
All non-local requests are aborted; no source, images, camera or remote writes.
"""
import argparse,json,re,traceback
from pathlib import Path
from playwright.sync_api import sync_playwright
import qa_native_audio_feedback as shared
ROOT=Path(__file__).resolve().parent.parent
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--package',type=Path,required=True);p.add_argument('--baseline-package',type=Path);p.add_argument('--output',type=Path,default=ROOT/'Temp/SettingsFrontApply-20261115/browser');a=p.parse_args();OUT=a.output.resolve();OUT.mkdir(parents=True,exist_ok=True)
R={'passed':False,'runtime':re.search(r'FastGame/([a-f0-9]{16})/',(a.package/'index.html').read_text(encoding='utf8'))[1],'cases':[],'errors':[],'httpErrors':[],'blockedRemote':[],'scope':__doc__}
SNAP=r'''()=>{const modal=document.querySelector('.settings-ref'),box=e=>{const r=e.getBoundingClientRect();return{x:r.x,y:r.y,width:r.width,height:r.height}},style=e=>{const s=getComputedStyle(e);return Object.fromEntries(['fontFamily','fontSize','fontWeight','fontStretch','letterSpacing','lineHeight','color','backgroundColor','borderRadius','borderTopWidth','borderRightWidth','borderBottomWidth','borderLeftWidth','borderTopColor','borderRightColor','borderBottomColor','borderLeftColor','paddingTop','paddingRight','paddingBottom','paddingLeft','boxShadow','maxHeight','overflowX','overflowY'].map(k=>[k,s[k]]));},all=[modal,...modal.querySelectorAll('h1,h2,p,button')];return{phase:simbarGame.phase,owner:simbarGame.owner,language:simbarGame.language,modal:all.map(e=>({tag:e.tagName,action:e.dataset.action||null,text:e.textContent.trim(),box:box(e),style:style(e)})),layers:{css:getComputedStyle(document.querySelector('.css-layer')).zIndex,reference:getComputedStyle(document.querySelector('.reference-ui')).zIndex},overlay:document.querySelector('.fast-game').classList.contains('has-overlay-menu')};}'''
HITS=r'''()=>{const m=document.querySelector('.settings-ref');return{modal:[m.querySelector('h1'),m.querySelector('h2'),m.querySelector('p')].map(e=>{const r=e.getBoundingClientRect(),hit=document.elementFromPoint(r.x+r.width/2,r.y+r.height/2);return{tag:e.tagName,inside:!!hit&&m.contains(hit),hit:hit?.className||hit?.tagName};}),buttons:[...m.querySelectorAll('button')].map(e=>{const r=e.getBoundingClientRect(),hit=document.elementFromPoint(r.x+r.width/2,r.y+r.height/2);return{action:e.dataset.action,text:e.textContent.trim(),topmost:!!hit&&(hit===e||e.contains(hit)),inert:!!e.closest('[inert]'),pointer:getComputedStyle(e).pointerEvents};})};}'''

def new(browser,url,size):
 ctx=browser.new_context(viewport={'width':size[0],'height':size[1]},locale='ko-KR',device_scale_factor=1)
 def deny(route):R['blockedRemote'].append({'url':route.request.url,'method':route.request.method});route.abort()
 ctx.route(re.compile(r'^https?://(?!127\.0\.0\.1[:/]|localhost[:/])',re.I),deny)
 page=ctx.new_page();page.on('pageerror',lambda e:R['errors'].append(str(e)));page.on('response',lambda r:R['httpErrors'].append({'url':r.url,'status':r.status}) if r.url.startswith('http://127.0.0.1') and r.status>=400 else None);page.goto(url,wait_until='domcontentloaded');page.wait_for_function('window.simbarGameReadyAt>0',timeout=60000);page.evaluate("simbarGame.tick=()=>{};simbarGame.act('language',{language:'ko'})");return ctx,page

def prepare(page,phase):
 if phase=='results':
  page.evaluate('''()=>{const g=simbarGame;g.journey.tutorialComplete=true;g.act('start');g.act('selectShow',{showId:'freewill'});g.act('deskWork',{task:0});g.act('deskWork',{task:1});if(!g.act('launch').success)throw Error('UI fixture launch');while(g.phase==='guide')g.act('guide');g.journey.booking.completed=true;g.journey.inProgress=false;g.journey.floorClosing=false;g.journey.settledSession=g.journey.session;g.journey.completedNights=1;g.journey.resultStars=2;g.phase='results';g.paused=true;g.pauseReason='results';}''')
 shared.settle(page);page.locator('.owner-title' if phase=='lobby' else '.results-ref').wait_for(state='visible')

def entry(page,phase):return page.locator(('.owner-title' if phase=='lobby' else '.results-ref')+' .settings-entry')
def open_settings(page,phase):entry(page,phase).click();page.locator('.settings-ref').wait_for(state='visible');shared.settle(page)
def modal(page):return page.evaluate(SNAP)
def hit_assert(page):
 hits=page.evaluate(HITS);assert all(x['inside'] for x in hits['modal']),hits;assert all(x['topmost'] and not x['inert'] and x['pointer']!='none' for x in hits['buttons']),hits;return hits

def baseline(browser,url,size,phase):
 ctx,page=new(browser,url,size)
 try:prepare(page,phase);open_settings(page,phase);return modal(page)
 finally:ctx.close()

def run_case(browser,url,baseline_url,size,phase):
 label=phase+'-'+str(size[0]);dest=OUT/label;dest.mkdir(exist_ok=True);r={'name':label,'viewport':list(size),'phase':phase};R['cases'].append(r)
 reference=baseline(browser,baseline_url,size,phase) if baseline_url else None
 ctx,page=new(browser,url,size)
 try:
  prepare(page,phase);original=page.evaluate('({phase:simbarGame.phase,owner:simbarGame.owner,day:simbarGame.journey.day})');r['original']=original
  underlays=page.evaluate('''phase=>{const nodes=phase==='lobby'?[document.querySelector('.owner-card:not(.selected)'),document.querySelector('.title-actions [data-action="start"]')]:[document.querySelector('.result-actions [data-action="next"]')];return nodes.filter(Boolean).map(e=>({action:e.dataset.action,box:e.getBoundingClientRect().toJSON()}));}''',phase)
  open_settings(page,phase);r['opened']=modal(page);r['hits']=hit_assert(page)
  if reference:assert r['opened']['modal']==reference['modal'],'Existing modal style or position changed';r['baseline']=reference;r['geometryTypographyUnchanged']=True
  page.screenshot(path=str(dest/'01-settings-front.png'))
  r['underlyingBlocked']=[]
  for u in underlays:
   # Prefer a shade/non-interactive point over this real underlying button.
   # A coincident modal button is still a legitimate front-layer target, but
   # its own controls must not be confused with the protected underlying action.
   point=page.evaluate('''r=>{const m=document.querySelector('.settings-shade');let fallback=null;for(const fx of [.5,.1,.9,.25,.75])for(const fy of [.5,.1,.9,.25,.75]){const x=r.x+r.width*fx,y=r.y+r.height*fy,e=document.elementFromPoint(x,y);if(!e||!m.contains(e))continue;const action=e.closest('[data-action]')?.dataset.action;if(action==='ui:fullscreen')continue;const p={x,y,topAction:action||null,topTag:e.tagName};if(!action)return p;fallback??=p;}return fallback;}''',u['box'])
   assert point,('No settings-layer point covers underlying '+u['action'])
   before=page.evaluate('({phase:simbarGame.phase,owner:simbarGame.owner,day:simbarGame.journey.day})');page.mouse.click(point['x'],point['y']);shared.settle(page);after=page.evaluate('({phase:simbarGame.phase,owner:simbarGame.owner,day:simbarGame.journey.day})');assert before==after,(u,point,before,after);r['underlyingBlocked'].append({'action':u['action'],'hit':point,'unchanged':True})
   if not page.locator('.settings-ref').count():open_settings(page,phase)
   if page.evaluate('simbarGame.language')!='ko':page.locator('.settings-ref [data-action="language"][data-payload*="ko"]').click();shared.settle(page)
  hit_assert(page)
  page.locator('.settings-ref [data-action="language"][data-payload*="en"]').click();page.wait_for_function("simbarGame.language==='en'");shared.settle(page);hit_assert(page);r['englishSwitch']=True;page.screenshot(path=str(dest/'02-settings-english.png'))
  page.locator('.settings-ref [data-action="language"][data-payload*="ko"]').click();page.wait_for_function("simbarGame.language==='ko'");shared.settle(page);assert modal(page)['modal']==r['opened']['modal'];r['koreanRestore']=True
  page.locator('.settings-ref [data-action="ui:close"]').click();page.wait_for_function("!document.querySelector('.settings-ref')&&!document.querySelector('.fast-game').classList.contains('has-overlay-menu')");shared.settle(page);assert page.evaluate('simbarGame.phase')==phase;page.screenshot(path=str(dest/'03-closed.png'))
  if phase=='lobby':
   page.locator('.owner-card:not(.selected)').first.click();page.wait_for_function('owner=>simbarGame.owner!==owner',arg=original['owner']);page.locator('.title-actions [data-action="start"]').click()
  else:page.locator('.result-actions [data-action="next"]').click()
  page.wait_for_function("simbarGame.phase==='planning'");page.locator('.daydesk').wait_for(state='visible');r['originalScreenReusable']=True;r['passed']=True
 finally:ctx.close();(dest/'report.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
 print(json.dumps({'case':label,'passed':r.get('passed'),'sameStyle':r.get('geometryTypographyUnchanged')},ensure_ascii=False),flush=True)

server,url=shared.serve(a.package.resolve());oldserver=oldurl=None
if a.baseline_package:oldserver,oldurl=shared.serve(a.baseline_package.resolve());R['baselineRuntime']=re.search(r'FastGame/([a-f0-9]{16})/',(a.baseline_package/'index.html').read_text(encoding='utf8'))[1]
try:
 with sync_playwright() as pw:
  browser=pw.chromium.launch(executable_path=shared.chromium_executable(None),args=['--mute-audio'])
  for phase in ['lobby','results']:
   for size in [(1600,900),(844,390),(1024,768)]:run_case(browser,url,oldurl,size,phase)
  browser.close()
 assert not R['errors'] and not R['httpErrors'],R;R['passed']=all(c.get('passed') for c in R['cases'])
except Exception:R['failure']=traceback.format_exc();raise
finally:
 server.shutdown()
 if oldserver:oldserver.shutdown()
 (OUT/'report.json').write_text(json.dumps(R,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print(json.dumps({'passed':R['passed'],'runtime':R['runtime'],'cases':len(R['cases']),'errors':R['errors'],'remoteDenied':len(R['blockedRemote'])},ensure_ascii=False))
