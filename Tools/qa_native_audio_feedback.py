"""Browser integration: autoplay rejection is not intent, unheard shows earn0.

Uses disposable contexts only. The first HTMLMediaElement.play rejects with a
real NotAllowedError; later calls use native playback of a local deterministic
WAV response. Browser playing/currentTime and the game's physical playback gate
are checked, without mocking the MusicPlayer or manufacturing feedback awards.
"""
import argparse
from functools import partial
from http.server import SimpleHTTPRequestHandler,ThreadingHTTPServer
import io
import json
import math
from pathlib import Path
import re
import struct
import threading
import traceback
import wave

from playwright.sync_api import sync_playwright
from benchmark_web_loading import chromium_executable

ROOT=Path(__file__).resolve().parent.parent
VIEWPORT={'width':1180,'height':668}
INIT=r'''() => {
  window.audioQA={attempts:[],events:[],media:[],rejectNext:true,lastGesture:null};
  document.addEventListener('click',e=>{if(e.isTrusted)audioQA.lastGesture={type:e.type,time:performance.now(),trusted:true};},true);
  const NativeAudio=window.Audio;
  window.Audio=function(...args){const audio=new NativeAudio(...args),id=audioQA.media.length;audioQA.media.push(audio);
    for(const type of ['play','playing','pause','waiting','stalled','error','ended','canplay'])audio.addEventListener(type,()=>audioQA.events.push({id,type,time:performance.now(),paused:audio.paused,currentTime:audio.currentTime,readyState:audio.readyState}));
    return audio;};window.Audio.prototype=NativeAudio.prototype;
  const nativePlay=HTMLMediaElement.prototype.play;
  HTMLMediaElement.prototype.play=function(...args){
    const attempt={id:audioQA.media.indexOf(this),time:performance.now(),gesture:audioQA.lastGesture,userActivation:!!navigator.userActivation?.isActive,status:'pending'};audioQA.attempts.push(attempt);
    if(audioQA.rejectNext){audioQA.rejectNext=false;attempt.status='rejected';attempt.error='NotAllowedError';return Promise.reject(new DOMException('Injected first autoplay restriction','NotAllowedError'));}
    try{return Promise.resolve(nativePlay.apply(this,args)).then(value=>{attempt.status='resolved';return value;},error=>{attempt.status='rejected';attempt.error=error.name;throw error;});}
    catch(error){attempt.status='rejected';attempt.error=error.name;throw error;}
  };
}'''
SNAPSHOT=r'''() => {const g=simbarGame,percent=key=>document.querySelector('[data-bind="'+key+'Pct"]')?.textContent||'';return{
  phase:g.phase,paused:g.paused,pauseReason:g.pauseReason,owner:g.owner,musicPaused:g.journey.musicPaused,musicPauseIntent:g.journey.musicPauseIntent??null,playbackActive:g._musicPlaybackActive??null,
  satisfaction:g.state.satisfaction,performance:g.state.performance,dom:{satisfaction:percent('satisfaction'),performance:percent('performance'),now:document.querySelector('[data-bind="nowPlaying"]')?.textContent||''},
  cash:g.state.money,served:g.journey.orders,manualPerfects:g.journey.manualPerfects,orders:structuredClone(g.orders),feedback:structuredClone(g.journey.feedback),
  participants:g.actors.filter(a=>a.festivalAction==='horns').length,elapsed:g.elapsed,secondsLeft:g.journey.secondsLeft,
  media:audioQA.media.map(a=>({paused:a.paused,ended:a.ended,currentTime:a.currentTime,readyState:a.readyState,error:a.error?.code||null})),attempts:structuredClone(audioQA.attempts),events:structuredClone(audioQA.events)
};}'''

def make_wav():
    data=io.BytesIO()
    with wave.open(data,'wb') as output:
        output.setnchannels(1);output.setsampwidth(2);output.setframerate(8000)
        output.writeframes(b''.join(struct.pack('<h',int(1800*math.sin(2*math.pi*440*i/8000))) for i in range(32000)))
    return data.getvalue()

def serve(directory,source=False):
    html=None
    if source:
        html=(directory/'Tools/WebGLTemplate/FastGame/index.html').read_text(encoding='utf-8').replace('__SIMBAR_ASSET_HINTS__','').replace('__SIMBAR_STYLESHEET__','/Tools/WebGLTemplate/style.css').replace('__SIMBAR_RUNTIME__','/Tools/WebGLTemplate/FastGame').replace('href="TemplateData/favicon.ico"','href="data:,"')
    class Handler(SimpleHTTPRequestHandler):
        def log_message(self,*_args):pass
        def do_GET(self):
            if self.path.split('?',1)[0]=='/__audio-qa__/index.html' and html:
                raw=html.encode('utf-8');self.send_response(200);self.send_header('Content-Type','text/html; charset=utf-8');self.send_header('Content-Length',str(len(raw)));self.end_headers();self.wfile.write(raw)
            else:super().do_GET()
    server=ThreadingHTTPServer(('127.0.0.1',0),partial(Handler,directory=str(directory)))
    threading.Thread(target=server.serve_forever,daemon=True).start()
    return server,f'http://127.0.0.1:{server.server_port}/'+('__audio-qa__/index.html' if source else '')

def settle(page):page.evaluate('() => new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r)))')

def close_order(page):
    settle(page)
    if page.locator('.order-dialog').is_visible():page.keyboard.press('Escape')
    settle(page)

def snapshot(page):
    settle(page);data=page.evaluate(SNAPSHOT)
    for key in ['satisfaction','performance']:
        expected=round(data[key]*1000)/10
        actual=float(re.search(r'[\d.]+',data['dom'][key]).group())
        assert abs(actual-expected)<.11,(key,actual,expected)
    return data

def advance(page,seconds):
    page.evaluate('seconds=>{for(let left=seconds;left>1e-8;left-=.05)audioAdvance(Math.min(.05,left));}',seconds)
    close_order(page)

def prepare(page,owner,raw=None):
    page.wait_for_function('window.simbarGameReadyAt>0',timeout=45000)
    page.evaluate('''owner=>{const g=simbarGame;window.audioAdvance=g.tick.bind(g);g.tick=()=>{};g._seed=71;g.act('owner',{index:owner});g.act('language',{language:'ko'});}''',owner)
    if raw is None:
        page.evaluate('''() => {const g=simbarGame;g.act('start');g.act('selectShow',{showId:'freewill'});g.act('deskWork',{task:0});g.act('deskWork',{task:1});const result=g.act('launch');if(!result.success)throw new Error(JSON.stringify(result));while(g.phase==='guide')g.act('guide');}''')
    else:
        assert page.evaluate('raw=>simbarGame.importSave(raw,{save:false})',raw)
        assert page.evaluate("simbarGame.act('continue').success")
    close_order(page)
    assert page.evaluate('simbarGame.playing&&!simbarGame.paused')

def perfect_service(page):
    result=page.evaluate('''() => {const g=simbarGame,started=g.act('prepare');if(!started.success)throw new Error(JSON.stringify(started));const target=(g.pour.target-.04)/.82;for(let left=Math.max(.16,target);left>1e-8;left-=.01)audioAdvance(Math.min(.01,left));const stopped=g.act('pourStop');for(let left=.2;left>1e-8;left-=.05)audioAdvance(Math.min(.05,left));return{started,stopped,quality:g.lastQuality};}''')
    close_order(page);assert result['stopped']['success'] and result['quality']==1,result
    return result

def context_page(browser,url,wav,errors):
    context=browser.new_context(viewport=VIEWPORT,locale='ko-KR')
    context.add_init_script('('+INIT+')()')
    def media(route):
        request_range=route.request.headers.get('range','')
        headers={'Accept-Ranges':'bytes','Cache-Control':'no-store'}
        match=re.match(r'bytes=(\d+)-(\d*)',request_range)
        if match:
            start=int(match.group(1));end=min(len(wav)-1,int(match.group(2)) if match.group(2) else len(wav)-1)
            headers['Content-Range']=f'bytes {start}-{end}/{len(wav)}'
            route.fulfill(status=206,content_type='audio/wav',headers=headers,body=wav[start:end+1])
        else:route.fulfill(status=200,content_type='audio/wav',headers=headers,body=wav)
    context.route(re.compile(r'https://pub-[^/]+\.r2\.dev/.*\.mp3(?:\?.*)?$',re.I),media)
    page=context.new_page();page.on('pageerror',lambda error:errors.append(str(error)));page.goto(url,wait_until='domcontentloaded')
    return context,page

def run_case(browser,url,owner,fixed,out,label,wav,errors):
    context,page=context_page(browser,url,wav,errors)
    try:
        prepare(page,owner);initial=snapshot(page)
        assert initial['satisfaction']==0 and initial['performance']==0
        service=perfect_service(page);served=snapshot(page)
        assert served['manualPerfects']==1 and served['served']==1 and served['satisfaction']>0 and served['performance']==0
        page.locator('[data-festival-action="horns"]').click();page.wait_for_function('audioQA.attempts.length>=1&&audioQA.attempts[0].status==="rejected"')
        immediate=snapshot(page);assert immediate['performance']==0
        advance(page,5);blocked=snapshot(page);assert blocked['participants']>0 and blocked['performance']==0
        assert all(guest['enjoyedSeconds']==0 for guest in blocked['feedback']['guests'].values())
        attempts=len(blocked['attempts']);page.wait_for_timeout(400);assert page.evaluate('audioQA.attempts.length')==attempts,'RAF must not retry rejected playback'
        blocked_save=page.evaluate('simbarGame.exportSave()')
        if fixed:
            assert not blocked['musicPaused'] and blocked['musicPauseIntent'] is False and blocked['playbackActive'] is False
        else:assert blocked['musicPaused'] is True,'The released rejection must reproduce the persisted-pause bug'
        page.screenshot(path=str(out/f'{label}-owner{owner}-blocked.png'))
        page.locator('.quest-toggle').click()
        if fixed:
            page.wait_for_function('audioQA.attempts.some(a=>a.status==="resolved")&&audioQA.media.some(a=>!a.paused&&a.currentTime>.05)&&simbarGame._musicPlaybackActive===true',timeout=15000)
            accepted=snapshot(page);assert accepted['performance']==0,'A gesture or accepted playback is not an instant feedback reward'
            advance(page,2);heard=snapshot(page);assert heard['performance']>0
            assert max(guest['actions'].get('horns',0) for guest in heard['feedback']['guests'].values())<=2.05,'Unheard five seconds must never be credited retroactively'
            count=len(heard['attempts']);page.wait_for_timeout(350);assert page.evaluate('audioQA.attempts.length')==count
            page.screenshot(path=str(out/f'{label}-owner{owner}-playing.png'))
            page.locator('[data-action="ui:liveTab"]').filter(has_text='재생').click();settle(page)
            page.locator('[data-action="ui:musicToggle"]').click();settle(page)
            explicit=snapshot(page);assert explicit['musicPaused'] and explicit['musicPauseIntent'] and explicit['playbackActive'] is False and all(media['paused'] for media in explicit['media'])
            explicit_save=page.evaluate('simbarGame.exportSave()')
        else:
            page.wait_for_timeout(350);heard=snapshot(page)
            assert len(heard['attempts'])==attempts and heard['musicPaused'] and heard['performance']==0
            accepted=None;explicit=None;explicit_save=None
        return{'owner':owner,'service':service,'initial':initial,'served':served,'blockedImmediate':immediate,'afterFiveUnheardSeconds':blocked,'afterRetryGesture':accepted,'afterHeardSeconds':heard,'explicitPause':explicit,'blockedSave':blocked_save,'explicitSave':explicit_save}
    finally:context.close()

def restore_case(browser,url,owner,raw,legacy,out,label,wav,errors):
    parsed=json.loads(raw);journey=json.loads(parsed['journeyJson'])
    if legacy:journey['musicPaused']=True;journey.pop('musicPauseIntent',None);parsed['journeyJson']=json.dumps(journey)
    context,page=context_page(browser,url,wav,errors)
    try:
        prepare(page,owner,json.dumps(parsed));restored=snapshot(page)
        assert restored['cash']==parsed['money'] and restored['served']==journey['orders'] and restored['orders']==parsed['native']['orders'] and restored['feedback']==journey['feedback'],'Continue must preserve ledger, money and orders'
        assert restored['musicPaused'] is (not legacy) and restored['musicPauseIntent'] is (not legacy)
        page.locator('.quest-toggle').click();settle(page)
        if legacy:page.wait_for_function('audioQA.attempts.length===1&&audioQA.attempts[0].status==="rejected"')
        else:assert page.evaluate('audioQA.attempts.length')==0,'An explicit pause cannot start playback on another UI gesture'
        advance(page,.5);after=snapshot(page)
        assert after['performance']==restored['performance'] and after['playbackActive'] is False
        if legacy:assert after['performance']==0 and not after['musicPaused']
        page.screenshot(path=str(out/(f'{label}-owner{owner}-'+('legacy' if legacy else 'explicit')+'.png')))
        return{'legacy':legacy,'restored':restored,'afterGestureAndTick':after,'businessProgressPreserved':True}
    finally:context.close()

def label_case(browser,url,owner,wav,errors):
    context,page=context_page(browser,url,wav,errors)
    try:
        prepare(page,owner);page.locator('[data-action="ui:liveTab"]').filter(has_text='재생').click()
        page.wait_for_function('audioQA.attempts.length===1&&audioQA.attempts[0].status==="rejected"')
        state=snapshot(page);assert '재생을 눌러주세요' in state['dom']['now'],state['dom']
        page.wait_for_timeout(300);assert page.evaluate('audioQA.attempts.length')==1
        return state
    finally:context.close()

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root',type=Path,default=ROOT)
    parser.add_argument('--package',type=Path)
    parser.add_argument('--url')
    parser.add_argument('--baseline-package',type=Path)
    parser.add_argument('--owners',default='1,2')
    parser.add_argument('--engines',default='chromium,webkit')
    parser.add_argument('--skip-restore',action='store_true',help='Run the playback paths and Korean prompt; omit optional Continue cases.')
    parser.add_argument('--output',type=Path,default=ROOT/'Temp/FeedbackParity-20261114/browser')
    args=parser.parse_args();out=args.output.resolve();out.mkdir(parents=True,exist_ok=True)
    servers=[];wav=make_wav();report={'passed':False,'viewport':VIEWPORT,'mediaFixture':'First real play rejects NotAllowedError; subsequent calls decode and play local WAV through native HTMLMediaElement. Game ticks are manually advanced; no feedback/quality fields are assigned.','engines':[],'errors':[]}
    if args.url:fixed_url=args.url
    else:
        server,fixed_url=serve((args.package or args.source_root).resolve(),source=not bool(args.package));servers.append(server)
    baseline_url=None
    if args.baseline_package:
        server,baseline_url=serve(args.baseline_package.resolve());servers.append(server)
    try:
        with sync_playwright() as pw:
            for engine in args.engines.split(','):
                item={'engine':engine,'cases':[]};report['engines'].append(item)
                try:browser=getattr(pw,engine).launch(**({'executable_path':chromium_executable(None)} if engine=='chromium' else {}))
                except Exception as error:
                    if engine=='webkit':item.update(skipped=True,reason=str(error));continue
                    raise
                try:
                    for owner in map(int,args.owners.split(',')):
                        if baseline_url:item['cases'].append({'kind':'released-baseline',**run_case(browser,baseline_url,owner,False,out,engine+'-baseline',wav,report['errors'])})
                        fixed=run_case(browser,fixed_url,owner,True,out,engine+'-fixed',wav,report['errors']);item['cases'].append({'kind':'fixed',**fixed})
                        if not args.skip_restore:
                            item['cases'].append({'kind':'legacy-continue',**restore_case(browser,fixed_url,owner,fixed['blockedSave'],True,out,engine,wav,report['errors'])})
                            item['cases'].append({'kind':'explicit-continue',**restore_case(browser,fixed_url,owner,fixed['explicitSave'],False,out,engine,wav,report['errors'])})
                    item['blockedLabel']=label_case(browser,fixed_url,1,wav,report['errors']);item['passed']=True
                finally:browser.close()
        assert not report['errors'],report['errors'];report['passed']=True
    except Exception:
        report['exception']=traceback.format_exc();raise
    finally:
        (out/'report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        for server in servers:server.shutdown()
    print(json.dumps({'passed':report['passed'],'engines':[{key:value for key,value in engine.items() if key in ['engine','passed','skipped','reason']} for engine in report['engines']],'errors':report['errors'],'report':str(out/'report.json')},ensure_ascii=False))

if __name__=='__main__':main()
