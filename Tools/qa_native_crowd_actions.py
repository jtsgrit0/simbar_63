"""Verify the two approved audience actions in real isolated browser sessions.

The existing ten buttons, live card, tabs and venue framing are compared to a
reviewed baseline at scroll position zero. New controls are clicked through the
real UI. Native audio uses a local WAV fixture; all other external traffic is
blocked so these checks never create play-count writes or touch user saves.
"""
import argparse
import base64
import hashlib
import io
import json
import math
from pathlib import Path
import re
import struct
import traceback
import wave

from playwright.sync_api import sync_playwright
import qa_native_audio_feedback as shared
from benchmark_web_loading import chromium_executable

ROOT = Path(__file__).resolve().parent.parent
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--package', type=Path, required=True)
parser.add_argument('--baseline-package', type=Path, required=True)
parser.add_argument('--output', type=Path, default=ROOT/'Temp/CrowdActionsApply/qa')
args = parser.parse_args()
OUT = args.output.resolve()
OUT.mkdir(parents=True, exist_ok=True)
report = {'passed':False, 'cases':[], 'errors':[], 'httpErrors':[],
          'remoteDenied':[], 'localMedia':[], 'scope':__doc__}
ORIGINAL = ['slam','mosh','surf','jump','horns','airguitar','sing','encore','headbang','dance']
INIT = r'''() => {window.crowdAudio=[];const Native=window.Audio;
window.Audio=function(...args){const a=new Native(...args);crowdAudio.push(a);return a;};
window.Audio.prototype=Native.prototype;Object.setPrototypeOf(window.Audio,Native);}'''
LAYOUT = r'''ids=>{
 const q=s=>document.querySelector(s), rect=e=>{const r=e.getBoundingClientRect();return{x:r.x,y:r.y,width:r.width,height:r.height};};
 const style=e=>{const s=getComputedStyle(e);return Object.fromEntries(['fontFamily','fontSize','fontWeight','fontStretch','lineHeight','letterSpacing','color','backgroundColor','borderRadius','borderTopWidth','borderRightWidth','borderBottomWidth','borderLeftWidth','borderTopColor','paddingTop','paddingRight','paddingBottom','paddingLeft'].map(k=>[k,s[k]]));};
 const button=id=>{const e=q('[data-festival-action="'+id+'"]');if(!e)throw Error('Missing action '+id);return{rect:rect(e),style:style(e)};};
 const tabs=[...document.querySelectorAll('.live-console-tabs button')].map(e=>({payload:e.dataset.payload,rect:rect(e),style:style(e)}));
 return{card:rect(q('.live-console')),tabs,buttons:Object.fromEntries(ids.map(id=>[id,button(id)])),camera:simbarApplication.world.getVenueBounds(),
  scroll:{top:q('.festival-actions').scrollTop,height:q('.festival-actions').scrollHeight,client:q('.festival-actions').clientHeight}};
}'''
STATE = r'''()=>{const g=simbarGame,s=g.journey.festival,actors=g.actors.filter(a=>a.festivalAction===s.activeId&&a.visible!==false);
 return{phase:g.phase,paused:g.paused,seconds:g.journey.secondsLeft,elapsed:g.elapsed,active:s.activeId,remaining:s.remaining,
  serial:s.serial,musicPaused:g.journey.musicPaused,musicActive:g._musicPlaybackActive,performance:g.state.performance,
  actors:actors.map(a=>({id:a.id,role:a.role,seat:!!a.seat,waiting:!!a.waitingSeat,moving:!!a.moving,action:a.action})),
  counts:{total:g.festival.liveAttendance,capacity:g.festival.capacity},
  audio:crowdAudio.map(a=>({paused:a.paused,time:a.currentTime,ready:a.readyState})),
  actions:Object.fromEntries(Object.entries(g.journey.feedback.guests||{}).map(([k,v])=>[k,{skank:v.actions?.skank||0,flash:v.actions?.flash||0}]))};}'''

def local_wav():
    data = io.BytesIO()
    with wave.open(data, 'wb') as stream:
        stream.setnchannels(1);stream.setsampwidth(2);stream.setframerate(8000)
        stream.writeframes(b''.join(struct.pack('<h', int(900*math.sin(2*math.pi*220*i/8000))) for i in range(80*8000)))
    return data.getvalue()

WAV = local_wav()

def context_page(browser, url, viewport):
    ctx = browser.new_context(viewport={'width':viewport[0], 'height':viewport[1]}, locale='ko-KR', device_scale_factor=1)
    ctx.add_init_script('('+INIT+')()')
    def remote(route):
        request = route.request
        if re.match(r'https://pub-[^/]+\.r2\.dev/.*\.mp3(?:\?.*)?$',request.url,re.I):
            report['localMedia'].append(request.url)
            match = re.match(r'bytes=(\d+)-(\d*)',request.headers.get('range',''))
            headers = {'Accept-Ranges':'bytes','Cache-Control':'no-store'}
            if match:
                start = int(match.group(1));end = min(len(WAV)-1,int(match.group(2)) if match.group(2) else len(WAV)-1)
                headers['Content-Range'] = f'bytes {start}-{end}/{len(WAV)}'
                route.fulfill(status=206,content_type='audio/wav',headers=headers,body=WAV[start:end+1])
            else:
                route.fulfill(status=200,content_type='audio/wav',headers=headers,body=WAV)
        else:
            report['remoteDenied'].append({'url':request.url,'method':request.method})
            route.abort()
    ctx.route(re.compile(r'^https?://(?!127\.0\.0\.1[:/]|localhost[:/])',re.I),remote)
    page = ctx.new_page()
    page.on('pageerror',lambda err:report['errors'].append(str(err)))
    page.on('response',lambda r:report['httpErrors'].append({'url':r.url,'status':r.status}) if r.url.startswith('http://127.0.0.1') and r.status>=400 else None)
    page.goto(url,wait_until='domcontentloaded')
    page.wait_for_function('window.simbarGameReadyAt>0',timeout=45000)
    return ctx,page

def prepare(page, owner):
    page.evaluate('''owner=>{const g=simbarGame;window.crowdAdvance=g.tick.bind(g);g.tick=()=>{};
      g.act('owner',{index:owner});g.act('language',{language:'ko'});g.journey.completedNights=2;g.journey.day=3;
      g.journey.tutorialComplete=true;g.state.fans=150;g.state.upgrades[1]=3;g.act('start');
      const selected=g.act('selectShow',{showId:'double'});if(!selected.success)throw Error(JSON.stringify(selected));
      g.act('ticket',{tier:0});g.act('promote');
      g.act('deskWork',{task:0});g.act('deskWork',{task:1});const launched=g.act('launch');if(!launched.success)throw Error(JSON.stringify(launched));
      while(g.phase==='guide')g.act('guide');}''',owner)
    shared.close_order(page)
    page.wait_for_function("simbarGame.playing&&!simbarGame.paused&&document.querySelectorAll('[data-festival-action]').length>=10")
    page.locator('.festival-actions').evaluate('(e)=>{e.scrollTop=0;}')
    shared.settle(page)

def advance(page, seconds):
    page.evaluate('''seconds=>{for(let left=seconds;left>1e-8;left-=.05)crowdAdvance(Math.min(left,.05));}''',seconds)
    shared.close_order(page)

def audio(page, playing):
    page.locator('[data-action="ui:liveTab"][data-payload*="music"]').click()
    shared.settle(page)
    paused=page.evaluate('simbarGame.journey.musicPaused')
    if paused==playing:
        page.locator('[data-action="ui:musicToggle"]').click()
    if playing:
        page.wait_for_function('crowdAudio.some(a=>!a.paused&&a.readyState>=2) && simbarGame._musicPlaybackActive===true',timeout=15000)
    else:
        page.wait_for_function('simbarGame.journey.musicPaused && crowdAudio.every(a=>a.paused)')
    page.locator('[data-action="ui:liveTab"][data-payload*="festival"]').click()
    shared.settle(page)

def start_action(page, action):
    shared.close_order(page)
    button = page.locator(f'[data-festival-action="{action}"]')
    button.scroll_into_view_if_needed()
    bounds = button.bounding_box()
    assert bounds and bounds['width']>0 and bounds['height']>0
    target = page.evaluate('''p=>{const e=document.elementFromPoint(p.x,p.y);return e?.closest('[data-festival-action]')?.dataset.festivalAction;}''',
                           {'x':bounds['x']+bounds['width']/2,'y':bounds['y']+bounds['height']/2})
    assert target==action,(action,target,bounds)
    button.click()
    page.wait_for_function('id=>simbarGame.journey.festival.activeId===id',arg=action)
    shared.settle(page)
    return page.evaluate(STATE)

def presentation(page, action):
    return page.evaluate('''id=>{const g=simbarGame,world=simbarApplication.world;
      return world.getAudiencePresentation({...g.state,owner:g.owner,phase:g.phase,paused:g.paused,journey:g.journey,
        floor:g.floor,reaction:g.reaction,inspection:g.inspection,actors:g.actors,orders:g.orders,effects:g.effects,elapsed:g.elapsed})
        .filter(a=>a.festivalAction===id).map(a=>({id:a.id,role:a.role,skankStep:a.skankStep,skankKnee:a.skankKnee,
          skankArm:a.skankArm,phoneLight:a.phoneLight,phoneWave:a.phoneWave}));}''',action)

def canvas_image(page, dest):
    raw = base64.b64decode(page.eval_on_selector('#unity-canvas','e=>e.toDataURL("image/png").split(",")[1]'))
    dest.write_bytes(raw)
    return hashlib.sha256(raw).hexdigest()

def finish_action(page):
    remaining = page.evaluate('simbarGame.journey.festival.remaining')
    advance(page,remaining+.3)
    page.wait_for_function("!simbarGame.journey.festival.activeId")
    shared.settle(page)

def real_frame_probe(page):
    result=page.evaluate('''async()=>{const g=simbarGame,w=simbarApplication.world,draw=w.render,started=performance.now(),elapsed=g.elapsed;let frames=0;
      w.render=function(...args){frames++;return draw.apply(this,args)};g.tick=window.crowdAdvance;
      await new Promise(resolve=>setTimeout(resolve,1100));g.tick=()=>{};w.render=draw;
      return{wall:(performance.now()-started)/1000,elapsed:g.elapsed-elapsed,frames};}''')
    assert result['frames']>=3 and result['elapsed']>=.2,(result,'Full-capacity game/render stalled')
    assert result['elapsed']<2.5,result
    return result

def availability_fixture(page):
    before=page.evaluate('''()=>Object.fromEntries([...document.querySelectorAll('[data-festival-action]')].map(e=>[e.dataset.festivalAction,e.disabled]))''')
    layout=page.evaluate('''()=>{const g=simbarGame,allowed=['guest','seated'];window.crowdSavedActors=[];
      const actors=g.actors.filter(a=>allowed.includes(a.role)&&a.visible!==false&&!a.drunkIncident&&!['queue','return','arrive','to-stage','exit','escorted'].includes(a.action));
      for(const a of actors){crowdSavedActors.push({actor:a,moving:a.moving,role:a.role,seat:a.seat,waitingSeat:a.waitingSeat});a.moving=a.role!=='seated';}
      let seat=actors.find(a=>a.role==='seated');if(!seat&&actors.length){seat=actors[0];seat.role='seated';seat.seat=true;seat.moving=false;}
      return{actors:actors.length,seated:actors.filter(a=>a.role==='seated').length};}''')
    assert layout['actors'] and layout['seated'],layout
    page.wait_for_function('''()=>document.querySelector('[data-festival-action="skank"]').disabled&&!document.querySelector('[data-festival-action="flash"]').disabled''')
    seated=page.evaluate('''()=>({skank:simbarGame.canFestivalParticipate('skank'),flash:simbarGame.canFestivalParticipate('flash'),
      old:Object.fromEntries([...document.querySelectorAll('[data-festival-action]')].map(e=>[e.dataset.festivalAction,e.disabled]))})''')
    assert not seated['skank'] and seated['flash'] and all(seated['old'][key]==before[key] for key in ORIGINAL),seated
    page.evaluate('crowdSavedActors.forEach(x=>x.actor.moving=true)')
    page.wait_for_function('''()=>document.querySelector('[data-festival-action="skank"]').disabled&&document.querySelector('[data-festival-action="flash"]').disabled''')
    all_moving=page.evaluate('''()=>({skank:simbarGame.canFestivalParticipate('skank'),flash:simbarGame.canFestivalParticipate('flash'),
      old:Object.fromEntries([...document.querySelectorAll('[data-festival-action]')].map(e=>[e.dataset.festivalAction,e.disabled]))})''')
    assert not all_moving['skank'] and not all_moving['flash'] and all(all_moving['old'][key]==before[key] for key in ORIGINAL),all_moving
    page.evaluate('''()=>{for(const x of crowdSavedActors){x.actor.moving=x.moving;x.actor.role=x.role;x.actor.seat=x.seat;x.actor.waitingSeat=x.waitingSeat;}delete window.crowdSavedActors;}''')
    page.wait_for_function('''()=>!document.querySelector('[data-festival-action="skank"]').disabled&&!document.querySelector('[data-festival-action="flash"]').disabled''')
    return {'seatedOnly':seated,'allMoving':all_moving,'originalActionsUnchanged':True}

def run_case(browser, url, old_url, owner, viewport):
    item = {'name':f'owner-{owner}-{viewport[0]}', 'owner':owner, 'viewport':list(viewport)}
    report['cases'].append(item)
    dest = OUT/item['name'];dest.mkdir(exist_ok=True)
    old_ctx,old_page = context_page(browser,old_url,viewport)
    try:
        prepare(old_page,owner)
        baseline = old_page.evaluate(LAYOUT,ORIGINAL)
    finally:
        old_ctx.close()
    ctx,page = context_page(browser,url,viewport)
    try:
        prepare(page,owner)
        current = page.evaluate(LAYOUT,ORIGINAL)
        for key in ['card','tabs','buttons','camera']:
            assert current[key]==baseline[key],(key,current[key],baseline[key])
        assert page.locator('[data-festival-action]').count()==12
        existing = page.locator('[data-festival-action="dance"]').evaluate('''e=>{const s=getComputedStyle(e);return ['fontFamily','fontSize','fontWeight','fontStretch','lineHeight','letterSpacing','color','backgroundColor','borderRadius','borderTopWidth','borderTopColor','paddingTop','paddingRight','paddingBottom','paddingLeft'].map(k=>s[k]);}''')
        for action in ['skank','flash']:
            same = page.locator(f'[data-festival-action="{action}"]').evaluate('''e=>{const s=getComputedStyle(e);return ['fontFamily','fontSize','fontWeight','fontStretch','lineHeight','letterSpacing','color','backgroundColor','borderRadius','borderTopWidth','borderTopColor','paddingTop','paddingRight','paddingBottom','paddingLeft'].map(k=>s[k]);}''')
            assert same==existing,(action,same,existing)
        item['originalGeometryAndStylesUnchanged'] = True
        item['baselineScroll'] = baseline['scroll'];item['newScroll'] = current['scroll']
        if owner==2:item['availability']=availability_fixture(page)
        if owner==1:
            count=page.evaluate('({current:simbarGame.festival.liveAttendance,max:simbarGame.festival.capacity})')
            assert count['current']==count['max']==80,count
            item['fullCapacity']=count

        # Wake native media with a trusted click, then explicitly pause it.
        audio(page,True);audio(page,False)
        before = page.evaluate(STATE)
        started = start_action(page,'skank')
        assert started['actors'] and all(a['role']=='guest' and not a['seat'] and not a['waiting'] for a in started['actors']),started
        advance(page,1.5)
        unheard = page.evaluate(STATE)
        assert unheard['performance']==before['performance'] and not any(v['skank'] for v in unheard['actions'].values()),(before,unheard)
        first = presentation(page,'skank');assert first and all(a['skankKnee'] is not None and a['skankArm'] is not None for a in first),first
        hash1 = canvas_image(page,dest/'skank-1.png');page.wait_for_timeout(300)
        second = presentation(page,'skank');hash2 = canvas_image(page,dest/'skank-2.png')
        common = {a['id']:a for a in first}.keys() & {a['id']:a for a in second}.keys()
        by1={a['id']:a for a in first};by2={a['id']:a for a in second}
        assert common and any((by1[i]['skankStep'],by1[i]['skankKnee'],by1[i]['skankArm'])!=(by2[i]['skankStep'],by2[i]['skankKnee'],by2[i]['skankArm']) for i in common)
        assert hash1!=hash2,'Canvas did not change between visible skank frames'
        item['skank']={'assigned':len(started['actors']),'poseChanged':True,'unheardFeedbackZero':True,'frames':[hash1,hash2]}
        if owner==1:
            audio(page,True);item['skank']['fullCapacityFrames']=real_frame_probe(page)
        finish_action(page)

        # The actual media resumes on the existing user control. Qualified
        # audience feedback depends on the real MusicPlayer signal, not a mock.
        audio(page,True)
        baseline_feedback = page.evaluate(STATE)['performance']
        flashed = start_action(page,'flash');assert flashed['actors'],flashed
        advance(page,1.5);lit = page.evaluate(STATE)
        assert lit['performance']>baseline_feedback and any(v['flash']>0 for v in lit['actions'].values()),(baseline_feedback,lit)
        lights1=presentation(page,'flash');assert lights1 and all(a['phoneLight'] is True for a in lights1),lights1
        flash1=canvas_image(page,dest/'flash-1.png');page.wait_for_timeout(300)
        lights2=presentation(page,'flash');flash2=canvas_image(page,dest/'flash-2.png')
        by1={a['id']:a for a in lights1};by2={a['id']:a for a in lights2};common=by1.keys()&by2.keys()
        assert common and any(by1[i]['phoneWave']!=by2[i]['phoneWave'] for i in common),(lights1,lights2)
        assert flash1!=flash2,'Canvas did not change between visible flash frames'
        if owner==1:item['flashCapacityFrames']=real_frame_probe(page)

        # Restore a real local save while the new action is active; this checks
        # persistence without changing any user's profile or remote state.
        if owner==0:
            saved=page.evaluate('''()=>{simbarGame.save();return{remaining:simbarGame.journey.festival.remaining,
              ids:simbarGame.actors.filter(a=>a.festivalAction==='flash'&&a.visible!==false).map(a=>a.id)};}''')
            page.reload(wait_until='domcontentloaded');page.wait_for_function('window.simbarGameReadyAt>0',timeout=45000)
            page.locator('.title-actions [data-action="continue"]').click()
            page.wait_for_function("simbarGame.playing&&simbarGame.journey.festival.activeId==='flash'",timeout=10000)
            restored=page.evaluate('''()=>{const g=simbarGame;window.crowdAdvance=g.tick.bind(g);g.tick=()=>{};
              return{remaining:g.journey.festival.remaining,ids:g.actors.filter(a=>a.festivalAction==='flash'&&a.visible!==false).map(a=>a.id)};}''')
            assert 0<restored['remaining']<=saved['remaining'] and saved['remaining']-restored['remaining']<2,(saved,restored)
            assert set(saved['ids'])==set(restored['ids']),(saved,restored)
            item['activeFlashSaveRestore']={'before':saved,'after':restored}

        # Pausing freezes both the active clock and the rendered phone wave.
        shared.close_order(page);page.locator('#unity-canvas').focus();page.keyboard.press('p')
        page.wait_for_function('simbarGame.paused');shared.settle(page)
        frozen1=page.evaluate(STATE);frozen_pose=presentation(page,'flash');page.wait_for_timeout(350)
        frozen2=page.evaluate(STATE);assert frozen1['remaining']==frozen2['remaining'] and frozen_pose==presentation(page,'flash')
        page.keyboard.press('p');page.wait_for_function('!simbarGame.paused');shared.settle(page)

        # An explicit inspection pauses the crowd and blocks the buttons.
        if owner==2:
            page.evaluate("()=>{simbarGame.journey.inspection.phase='scheduled';if(!simbarGame.act('inspection').success)throw Error('inspection fixture');}")
            for _ in range(80):
                if page.evaluate('simbarGame.inspection.freezePerformers'):break
                advance(page,.25)
            assert page.evaluate('simbarGame.inspection.freezePerformers')
            before=page.evaluate(STATE);assert before['active']=='flash',before
            frozen_lights=presentation(page,'flash');assert not frozen_lights or not any(a.get('phoneLight') for a in frozen_lights),frozen_lights
            page.evaluate('''()=>{window.crowdInspectionActors=simbarGame.actors.filter(a=>['guest','seated'].includes(a.role)&&a.visible!==false).map(a=>({actor:a,moving:a.moving}));for(const x of crowdInspectionActors)x.actor.moving=true;}''')
            page.wait_for_function('''()=>!simbarGame.canFestivalParticipate('skank')&&!simbarGame.canFestivalParticipate('flash')
              &&!document.querySelector('[data-festival-action="skank"]').disabled&&!document.querySelector('[data-festival-action="flash"]').disabled
              &&document.querySelector('[data-festival-cooldown="flash"]').textContent.includes('단속')''')
            button=page.locator('[data-festival-action="flash"]');button.scroll_into_view_if_needed();button.click();shared.settle(page)
            assert page.evaluate('simbarGame.journey.festival.serial')==before['serial']
            page.evaluate('''()=>{for(const x of crowdInspectionActors)x.actor.moving=x.moving;delete window.crowdInspectionActors;}''')
            advance(page,2);assert page.evaluate('simbarGame.journey.festival.remaining')==before['remaining']
            item['inspectionBlocked'] = True
            for _ in range(160):
                if not page.evaluate('simbarGame.inspection.freezePerformers'):break
                advance(page,.25)
            assert not page.evaluate('simbarGame.inspection.freezePerformers')
        finish_action(page);assert not presentation(page,'flash'),'Phone lights must disappear when the action ends'
        assert page.evaluate('simbarGame.festival.liveAttendance<=simbarGame.festival.capacity')
        assert page.evaluate(LAYOUT,ORIGINAL)['card']==current['card']
        item['flash']={'assigned':len(flashed['actors']),'seated':sum(a['role']=='seated' for a in flashed['actors']),
                       'audibleFeedbackIncreased':True,'phoneWaveChanged':True,'pausedFrozen':True,'offAfterEnd':True,'frames':[flash1,flash2]}
        item['passed']=True
    finally:
        ctx.close()
        (dest/'report.json').write_text(json.dumps(item,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    print(json.dumps({'case':item['name'],'passed':item.get('passed')},ensure_ascii=False),flush=True)

server,url=shared.serve(args.package.resolve());old_server,old_url=shared.serve(args.baseline_package.resolve())
try:
    with sync_playwright() as pw:
        browser=pw.chromium.launch(executable_path=chromium_executable(None),args=['--mute-audio'])
        for owner,viewport in [(0,(1600,900)),(1,(844,390)),(2,(1024,768))]:run_case(browser,url,old_url,owner,viewport)
        browser.close()
    assert not report['errors'] and not report['httpErrors'],report
    report['passed']=all(case.get('passed') for case in report['cases'])
except Exception:
    report['failure']=traceback.format_exc()
    raise
finally:
    server.shutdown();old_server.shutdown()
    (OUT/'report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print(json.dumps({'passed':report['passed'],'cases':len(report['cases']),'remoteDenied':len(report['remoteDenied'])},ensure_ascii=False))
