"""Verify the requested translated labels and circular controls in a real browser.

The game runs in disposable contexts. Only the target controls and protected
venue bounds are inspected. Audio URLs receive a local WAV fixture; all other
external requests, including play-count writes, are blocked.
"""
import argparse
import io
import json
import math
import re
import struct
import traceback
import wave
from pathlib import Path

from playwright.sync_api import sync_playwright
import qa_native_audio_feedback as shared

ROOT = Path(__file__).resolve().parent.parent
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--package', type=Path, required=True)
parser.add_argument('--output', type=Path, default=ROOT / 'Temp/RoundLocaleQA')
parser.add_argument('--baseline', type=Path, required=True, help='JSON measurements from the prior public English UI.')
parser.add_argument('--view-settings', action='store_true', help='Expect the existing settings gear to remain visible in viewing mode.')
args = parser.parse_args()
baseline = {tuple(row['viewport']): row for row in json.loads(args.baseline.read_text(encoding='utf-8-sig'))}
OUT = args.output.resolve()
OUT.mkdir(parents=True, exist_ok=True)
runtime = re.search(r'FastGame/([a-f0-9]{16})/', (args.package/'index.html').read_text(encoding='utf8'))[1]
report = {'passed':False, 'runtime':runtime, 'viewSettings':args.view_settings, 'cases':[], 'errors':[], 'httpErrors':[], 'remoteBlocked':0, 'localAudio':0}

buffer = io.BytesIO()
with wave.open(buffer, 'wb') as output:
    output.setnchannels(1);output.setsampwidth(2);output.setframerate(8000)
    output.writeframes(b''.join(struct.pack('<h', int(500*math.sin(2*math.pi*220*i/8000))) for i in range(12*8000)))
WAV = buffer.getvalue()

SNAPSHOT = r'''() => {
  const q=s=>document.querySelector(s),world=simbarApplication.world,canvas=q('#unity-canvas'),c=canvas.getBoundingClientRect();
  const rect=e=>{const r=e.getBoundingClientRect();return{x:r.x,y:r.y,width:r.width,height:r.height,right:r.right,bottom:r.bottom};};
  const style=e=>{const s=getComputedStyle(e);return Object.fromEntries(['fontFamily','fontSize','fontWeight','lineHeight','color','backgroundColor','borderRadius','borderTopWidth','borderTopColor','paddingTop','paddingRight','paddingBottom','paddingLeft'].map(k=>[k,s[k]]));};
  const control=e=>{const r=rect(e),icon=e.querySelector('.tool-icon-crop,.view-mode-icon-crop,.nav-icon'),label=e.querySelector('.tool-label-crop,.nav-label'),img=icon?.querySelector('img'),accessible=e.querySelector('.nav-label');
    return{action:e.dataset.action,label:e.getAttribute('aria-label')||e.textContent.trim(),rect:r,style:style(e),icon:img?.getAttribute('src'),
      badge:{content:getComputedStyle(e,'::after').content,background:getComputedStyle(e,'::after').backgroundImage},
      iconRect:icon?rect(icon):null,labelRect:label?rect(label):null,text:accessible?.textContent.trim(),labelImage:e.querySelector('.tool-label-crop img')?.getAttribute('src'),labelCrop:label?.classList.contains('tool-label-crop')?{width:getComputedStyle(label).width,height:getComputedStyle(label).height,imageLeft:getComputedStyle(label.querySelector('img')).left,imageTop:getComputedStyle(label.querySelector('img')).top,imageWidth:getComputedStyle(label.querySelector('img')).width,imageHeight:getComputedStyle(label.querySelector('img')).height,blend:getComputedStyle(label.querySelector('img')).mixBlendMode}:null,
      visible:getComputedStyle(e).visibility!=='hidden'&&!e.closest('[inert]')&&r.right>0&&r.bottom>0,
      topmost:(()=>{const h=document.elementFromPoint(r.x+r.width/2,r.y+r.height/2);return h===e||e.contains(h);})()};};
  const qctrl=s=>control(q(s)),gear=qctrl('.hud-settings'),tools=[...document.querySelectorAll('.bottom-tabs > button')].map(control),mode=qctrl('button[data-view-mode]');
  const ref=world.project(0,0),screen=world.worldToScreen(0,0),sx=c.width/canvas.width,sy=c.height/canvas.height;
  const ox=c.left+screen.x-ref.x*sx,oy=c.top+screen.y-ref.y*sy;
  const region=(x1,y1,x2,y2)=>({x:ox+x1*sx,y:oy+y1*sy,right:ox+x2*sx,bottom:oy+y2*sy});
  const labels={tabs:[...document.querySelectorAll('.live-console-tabs button')].map(e=>e.textContent.trim()),
    menu:q('.menu-ready-label')?.textContent.trim(),mood:[...document.querySelectorAll('.mood-label')].map(e=>e.textContent.trim()),
    quest:q('.quest-toggle')?.textContent.replace(/\s+/g,' ').trim(),day:q('.hud-day span')?.textContent.trim(),
    feedback:q('[data-bind="festivalFeedback"]')?.textContent.trim(),
    actions:Object.fromEntries([...document.querySelectorAll('[data-festival-action]')].map(e=>[e.dataset.festivalAction,e.querySelector('.festival-action-name').textContent.trim()]))};
  const lines=[...document.querySelectorAll('[data-festival-action]')].map(e=>{
    const n=e.querySelector('.festival-action-name'),node=n.firstChild,ranges=[],text=node?.textContent||'';let offset=0;
    for(const ch of text){const start=offset;offset+=ch.length;if(/\s/u.test(ch))continue;const r=document.createRange();r.setStart(node,start);r.setEnd(node,offset);const b=r.getBoundingClientRect();if(b.width)ranges.push({x:b.x,y:b.y,right:b.right,bottom:b.bottom});}
    const rows=[];for(const r of ranges)if(!rows.some(y=>Math.abs(y-r.y)<1))rows.push(r.y);
    const b=rect(e),ns=getComputedStyle(n),clips=v=>v==='hidden'||v==='clip';return{id:e.dataset.festivalAction,text,lines:rows.length,
      clip:clips(ns.overflowX)&&n.scrollWidth>n.clientWidth+1||clips(ns.overflowY)&&n.scrollHeight>n.clientHeight+1,
      glyphOutside:ranges.some(r=>r.x<b.x-1||r.right>b.right+1||r.y<b.y-1||r.bottom>b.bottom+1)};
  });
  const tracklist=[...document.querySelectorAll('.track-list [data-playlist]')].map(e=>({index:e.dataset.playlist,title:e.querySelector('.track-title-text')?.textContent.split(' · ')[0]}));
  return{language:simbarGame.language,phase:simbarGame.phase,mode:q('.fast-game').dataset.viewMode,gear,tools,modeButton:mode,labels,lines,
    camera:world.getVenueBounds(),protected:{stage:region(550,90,885,385),sign:region(370,25,565,115)},
    card:rect(q('.live-console')),tabs:[...document.querySelectorAll('.live-console-tabs button')].map(rect),
    tracks:tracklist,transport:[...document.querySelectorAll('.music-controls button')].map(e=>e.dataset.action),
    documentScroll:{width:document.documentElement.scrollWidth,client:document.documentElement.clientWidth}};
}'''

def snapshot(page):
    shared.settle(page)
    return page.evaluate(SNAPSHOT)

def click_visible_mode(page):
    box=page.locator('button[data-view-mode]').bounding_box()
    page.mouse.click(box['x']+box['width']/2,box['y']+box['height']/2)

def relative(box, parent):
    return tuple(round(box[k] - parent[k] if k in ('x','y') else box[k], 2) for k in ('x','y','width','height'))

def intersects(a, b):
    return a['x'] < b['right'] and a['right'] > b['x'] and a['y'] < b['bottom'] and a['bottom'] > b['y']

def equivalent(a, b):
    return all(abs(a[k]-b[k]) < .05 for k in ('x','y','width','height'))

def case(browser, url, width, height):
    result = {'viewport':[width,height], 'passed':False, 'languages':{}}
    report['cases'].append(result)
    context = browser.new_context(viewport={'width':width,'height':height}, locale='ko-KR', device_scale_factor=1)
    def route(request_route):
        u=request_route.request.url
        if re.match(r'^https?://(?:127\.0\.0\.1|localhost)(?::\d+)?/',u,re.I):request_route.continue_()
        elif u.startswith('https://pub-f61dee98441a42719d944abea5e0e1d2.r2.dev/'):
            report['localAudio']+=1;request_route.fulfill(status=200,content_type='audio/wav',body=WAV)
        else:report['remoteBlocked']+=1;request_route.abort()
    context.route('**/*',route)
    page=context.new_page()
    page.on('pageerror',lambda error:report['errors'].append(str(error)))
    page.on('response',lambda response:report['httpErrors'].append({'url':response.url,'status':response.status}) if response.url.startswith('http://127.0.0.1') and response.status>=400 else None)
    try:
        page.goto(url,wait_until='domcontentloaded')
        page.wait_for_function('window.simbarGameReadyAt>0',timeout=60000)
        page.evaluate('''()=>{const g=simbarGame;g.act('owner',{index:2});g.journey.tutorialComplete=true;
          g.act('start');g.act('selectShow',{showId:'freewill'});g.act('deskWork',{task:0});g.act('deskWork',{task:1});
          if(!g.act('launch').success)throw Error('Launch failed');while(g.phase==='guide')g.act('guide');
          g.journey.day=15;g.tick=()=>{};}''')
        shared.close_order(page)
        expected={'ko':{'tools':['업그레이드','이벤트','감상모드'],'manage':'경영모드','tabs':['호응 액션','재생 목록'],
                        'menu':'메뉴 준비','mood':['손님 만족도','공연 호응도'],'quest':'퀘스트 목록','day':'날짜 (15일 차)',
                        'actions':{'surf':'서핑','airguitar':'에어기타'}},
                  'en':{'tools':['UPGRADE','EVENTS','VIEW'],'manage':'MANAGE','tabs':['ACTIONS','PLAYLIST'],
                        'menu':'MENU','mood':['RATING','HYPE'],'quest':'QUESTS','day':'DAY 15',
                        'actions':{'slam':'SLAM','mosh':'MOSH','surf':'SURF','jump':'JUMP','horns':'ROCK','airguitar':'GUITAR',
                                   'sing':'SING','headbang':'HEAD','skank':'SKANK','flash':'FLASH'}}}
        measurements={}
        for language in ('ko','en'):
            page.evaluate('language=>simbarGame.act("language",{language})',language)
            page.wait_for_function('language=>simbarGame.language===language',arg=language)
            management=snapshot(page);want=expected[language];assert management['phase']=='playing' and management['mode']=='management',management
            assert [t['label'] for t in management['tools']]==want['tools'],management['tools']
            assert management['labels']['tabs']==want['tabs'],management['labels']['tabs']
            assert management['labels']['menu']==want['menu'] and management['labels']['mood']==want['mood'],management['labels']
            assert management['labels']['quest'].startswith(want['quest']) and management['labels']['day']==want['day'],management['labels']
            if language=='en':
                assert management['labels']['feedback']=='PICK A MOVE',management['labels']['feedback']
                for line in management['lines']:
                    if line['id'] in ('sing','headbang','skank','flash'):
                        assert line['lines']==1 and not line['clip'] and not line['glyphOutside'],line
            for key,value in want['actions'].items():assert management['labels']['actions'][key].endswith(value),(key,management['labels']['actions'][key])
            for b in management['tools']:
                assert b['visible'] and b['topmost'] and b['style']['borderRadius']=='50%',b
                assert b['iconRect'] and b['labelRect'],b
                if language=='ko':
                    assert b['rect']['width']==84 and b['rect']['height']==84 and b['style']['fontSize']=='14px',b
                    assert b['labelImage']==b['icon'] and b['labelCrop']=={'width':'76px','height':'20px','imageLeft':'-4px','imageTop':'-52px','imageWidth':'84px','imageHeight':'88px','blend':'lighten'},b
            if language=='en':
                for current,old in zip(management['tools'],baseline[(width,height)]['buttons']):
                    assert equivalent(current['rect'],old['rect']),(current,old)
                    for key in ('fontSize','lineHeight','borderRadius','backgroundColor','borderTopWidth','paddingTop','paddingRight','paddingBottom','paddingLeft'):
                        assert current['style'][key]==old['style'][key],(key,current,old)
                    assert current['icon'].split('/art/')[-1]==old['icon'].split('/art/')[-1],(current,old)
            for b in management['tools'][:2]:
                assert (b['badge']['content']!='none')==(language=='ko'),b
            gear=management['gear'];assert gear['visible'] and gear['topmost'] and gear['rect']['width']==49 and gear['rect']['height']==45,gear
            assert management['documentScroll']['width']<=management['documentScroll']['client'],management['documentScroll']
            for b in [gear,*management['tools']]:
                assert not intersects(b['rect'],management['protected']['stage']) and not intersects(b['rect'],management['protected']['sign']),(b,management['protected'])
            page.locator('.venue-tools.bottom-tabs').screenshot(path=str(OUT/f'{width}-{language}-management.png'))
            click_visible_mode(page)
            page.wait_for_function("document.querySelector('.fast-game').dataset.viewMode==='viewing'&&document.querySelector('.fast-game').dataset.viewModeTransition==='settled'",timeout=6000)
            viewing=snapshot(page);assert viewing['modeButton']['label']==want['manage'] and viewing['modeButton']['text']==want['manage'],viewing['modeButton']
            assert viewing['modeButton']['visible'] and viewing['modeButton']['topmost'] and viewing['modeButton']['rect']['width']==84 and viewing['modeButton']['rect']['height']==84,viewing['modeButton']
            if language=='ko':
                assert equivalent(viewing['modeButton']['rect'],management['tools'][0]['rect']) and viewing['modeButton']['labelImage']==viewing['modeButton']['icon'],viewing['modeButton']
            if args.view_settings:
                assert viewing['gear']['visible'] and viewing['gear']['topmost'] and equivalent(viewing['gear']['rect'],gear['rect']),viewing['gear']
            else:
                assert not viewing['gear']['visible'] and not viewing['gear']['topmost'],viewing['gear']
            assert viewing['camera']==management['camera'] and equivalent(viewing['card'],management['card']) and viewing['tabs']==management['tabs'], {'camera':(management['camera'],viewing['camera']),'card':(management['card'],viewing['card']),'tabs':(management['tabs'],viewing['tabs'])}
            for b in [viewing['gear'],viewing['modeButton']]:
                if not b['visible']:continue
                assert not intersects(b['rect'],viewing['protected']['stage']) and not intersects(b['rect'],viewing['protected']['sign']),(b,viewing['protected'])
            assert len(viewing['tracks'])==5 and [p['index'] for p in viewing['tracks']]==['0','1','2','3','4'],viewing['tracks']
            assert viewing['transport']==['ui:musicPrevious','ui:musicToggle','ui:musicNext'],viewing['transport']
            page.locator('.view-mode-slot').screenshot(path=str(OUT/f'{width}-{language}-viewing.png'))
            measurements[language]={'management':management,'viewing':viewing}
            click_visible_mode(page)
            page.wait_for_function("document.querySelector('.fast-game').dataset.viewMode==='management'&&document.querySelector('.fast-game').dataset.viewModeTransition==='settled'",timeout=6000)
        ko,en=measurements['ko'],measurements['en']
        a,b=ko['viewing']['modeButton'],en['viewing']['modeButton']
        assert equivalent(a['rect'],b['rect']) and a['style']==b['style'] and a['icon']==b['icon'] and relative(a['iconRect'],a['rect'])==relative(b['iconRect'],b['rect']) and relative(a['labelRect'],a['rect'])==relative(b['labelRect'],b['rect']),(a,b)
        assert [x['title'] for x in ko['viewing']['tracks']]==[x['title'] for x in en['viewing']['tracks']]
        result.update(passed=True,languages=measurements,englishLabelDiagnostics=[x for x in en['management']['lines'] if x['lines']>1 or x['clip'] or x['glyphOutside']])
    finally:
        context.close()
    print(json.dumps({'viewport':[width,height],'passed':result['passed'],'englishLabelDiagnostics':result.get('englishLabelDiagnostics')},ensure_ascii=False),flush=True)

server,url=shared.serve(args.package.resolve())
try:
    with sync_playwright() as pw:
        browser=pw.chromium.launch(executable_path=shared.chromium_executable(None),args=['--mute-audio'])
        for width,height in ((1600,900),(844,390),(1024,768),(1366,768),(1280,390)):case(browser,url,width,height)
        browser.close()
    assert not report['errors'] and not report['httpErrors'],report
    report['passed']=all(c['passed'] for c in report['cases'])
except Exception:
    report['failure']=traceback.format_exc()
    raise
finally:
    server.shutdown()
    (OUT/'report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print(json.dumps({'passed':report['passed'],'runtime':runtime,'cases':len(report['cases']),'errors':report['errors'],'remoteBlocked':report['remoteBlocked']},ensure_ascii=False))
