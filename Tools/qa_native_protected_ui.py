"""Verify the real landscape UI never paints over the sign or complete stage.

Run with: uv run --with playwright --with pillow python Tools/qa_native_protected_ui.py
The disposable browser changes only its own game fixtures. Source art is read
and hashed, never edited. PNG comparison uses actual browser screenshots.
"""
import argparse
from functools import partial
import hashlib
from http.server import ThreadingHTTPServer
from io import BytesIO
import json
import math
from pathlib import Path
import subprocess
import threading

from playwright.sync_api import sync_playwright
from PIL import Image
from benchmark_web_loading import chromium_executable
from qa_native_owner_venues import QuietHandler

ROOT = Path(__file__).resolve().parent.parent
VIEWS = [(1660,624),(1600,900),(844,390),(915,412),(667,375)]
HUD = '.hud-settings,.hud-score,.hud-session,.quest-panel,.venue-show-badge,.ops-status,.venue-tools button,.menu-ready,.mood,.live-console,.venue-ui .toast'
MEDIA_CAPTURE = r'''(() => {window.qaMedia=[];window.qaMediaCalls=[];const NativeAudio=window.Audio;window.Audio=function(...args){const media=new NativeAudio(...args);qaMedia.push(media);for(const method of ['play','pause']){const original=media[method].bind(media);media[method]=(...values)=>{qaMediaCalls.push({method,at:performance.now(),portrait:window.simbarApplication?.orientationBlocked,stack:new Error().stack});return original(...values);};}return media;};window.Audio.prototype=NativeAudio.prototype;})();'''

PROBE = r'''hudSelector => {
 const app=simbarApplication,root=app.root,canvas=app.canvas,world=app.world;
 const box=e=>{const r=e.getBoundingClientRect();return{left:r.left,top:r.top,right:r.right,bottom:r.bottom,width:r.width,height:r.height};};
 const visible=e=>{if(!e||!e.getClientRects().length)return false;for(let p=e;p&&p!==document.documentElement;p=p.parentElement){const s=getComputedStyle(p);if(s.display==='none'||s.visibility==='hidden'||s.visibility==='collapse'||+s.opacity===0)return false;}const r=e.getBoundingClientRect();return r.width>.2&&r.height>.2;};
 const intersect=(a,b)=>({left:Math.max(a.left,b.left),top:Math.max(a.top,b.top),right:Math.min(a.right,b.right),bottom:Math.min(a.bottom,b.bottom)});
 const clipped=e=>{let r=box(e);for(let p=e.parentElement;p&&p!==root.parentElement;p=p.parentElement){const s=getComputedStyle(p),b=box(p);if(/hidden|clip|auto|scroll/.test(s.overflowX)){r.left=Math.max(r.left,b.left);r.right=Math.min(r.right,b.right);}if(/hidden|clip|auto|scroll/.test(s.overflowY)){r.top=Math.max(r.top,b.top);r.bottom=Math.min(r.bottom,b.bottom);}}return r;};
 const name=e=>[e.tagName.toLowerCase(),e.className,e.dataset.action||''].join(' ').slice(0,200);
 const windows=[...root.querySelectorAll('.venue-window')].filter(visible);
 const hud=[...root.querySelectorAll(hudSelector)].filter(e=>visible(e)&&(e.textContent.trim()||e.tagName==='BUTTON'));
 const contentRoots=[...new Set([...windows,...hud.filter(e=>e.matches('.quest-panel,.live-console'))])];
 const panels=[...new Set([...windows,...hud])].map(e=>({name:name(e),...box(e),isWindow:e.matches('.venue-window')}));
 const descendants=contentRoots.flatMap(w=>[...w.querySelectorAll('*')].filter(visible).filter(e=>{
   const s=getComputedStyle(e);return e.matches('button,input,select,textarea,img,canvas')||[...e.childNodes].some(n=>n.nodeType===3&&n.textContent.trim())||s.backgroundColor!=='rgba(0, 0, 0, 0)'&&s.backgroundColor!=='transparent';
 }).map(e=>({name:name(e),...clipped(e)}))).filter(r=>r.right>r.left&&r.bottom>r.top);
 const alpha=s=>{if(s==='transparent')return 0;const m=s.match(/rgba?\(([^)]+)\)/);return m?(m[1].split(',').length===4?+m[1].split(',')[3]:1):1;};
 const scrims=[...root.querySelectorAll('*')].filter(e=>visible(e)&&!e.matches('canvas,.orientation-gate,.fast-game')&&!e.closest('.orientation-gate')).filter(e=>{
   const r=box(e),s=getComputedStyle(e);return r.width>=innerWidth*.9&&r.height>=innerHeight*.9&&(alpha(s.backgroundColor)>.001||s.backdropFilter&&s.backdropFilter!=='none');
 }).map(e=>({name:name(e),...box(e),background:getComputedStyle(e).backgroundColor}));
 const scrolls=[],unreachable=[];
 for(const w of contentRoots){
   const originals=[w,...w.querySelectorAll('*')].filter(e=>e.scrollHeight>e.clientHeight+2||e.scrollWidth>e.clientWidth+2).map(e=>({e,top:e.scrollTop,left:e.scrollLeft}));
   for(const {e}of originals){const s=getComputedStyle(e);if(e.scrollHeight>e.clientHeight+2&&/auto|scroll/.test(s.overflowY)){e.scrollTop=e.scrollHeight;scrolls.push({name:name(e),vertical:true,reachesEnd:e.scrollTop+e.clientHeight>=e.scrollHeight-2});}if(e.scrollWidth>e.clientWidth+2&&/auto|scroll/.test(s.overflowX)){e.scrollLeft=e.scrollWidth;scrolls.push({name:name(e),horizontal:true,reachesEnd:e.scrollLeft+e.clientWidth>=e.scrollWidth-2});}}
   for(const e of w.querySelectorAll('button,input,select,textarea,[tabindex="0"]')){
     if(!visible(e))continue;const original=box(e),painted=clipped(e);let reachableByScroll=false;
     for(let p=e.parentElement;p&&p!==root;p=p.parentElement){const s=getComputedStyle(p);if(/auto|scroll/.test(s.overflowY)&&p.scrollHeight>p.clientHeight+1||/auto|scroll/.test(s.overflowX)&&p.scrollWidth>p.clientWidth+1)reachableByScroll=true;}
     if((painted.right-painted.left<original.width-1||painted.bottom-painted.top<original.height-1)&&!reachableByScroll)unreachable.push({name:name(e),reason:'clipped without a user-scrollable ancestor',raw:original,painted});
     e.scrollIntoView({block:'nearest',inline:'nearest'});const raw=box(e),r=clipped(e);
     if(r.right-r.left<Math.min(raw.width,12)-1||r.bottom-r.top<Math.min(raw.height,12)-1)unreachable.push({name:name(e),raw,painted:r});
   }
   for(const {e,top,left}of originals){e.scrollTop=top;e.scrollLeft=left;}
 }
 const points=[[0,0],[480,279],[960,558]].map(p=>world.imageToScreen(...p));
 const cssRect=r=>{const a=world.imageToScreen(r.left,r.top),b=world.imageToScreen(r.right,r.bottom);return{left:a.x,top:a.y,right:b.x,bottom:b.y};};
 const bubbles=world.getOrderBubblePresentation().map(b=>({...b,cssBody:cssRect(b.body),cssTail:b.tailVisible?cssRect(b.tail.bounds):null}));
 const toast=root.querySelector('.toast'),toastContent=visible(toast)?{text:toast.innerText,height:toast.clientHeight,scrollHeight:toast.scrollHeight,width:toast.clientWidth,scrollWidth:toast.scrollWidth}:null;
 return{viewport:{width:innerWidth,height:innerHeight},canvas:{width:canvas.width,height:canvas.height,box:box(canvas)},points,
   regions:world.getProtectedRegions(),bubbles,panels,descendants,scrims,scrolls,unreachable,windowCount:windows.length,
   visibleHudCount:hud.filter(e=>!e.closest('.venue-window')&&!e.matches('.toast')).length,
   visibleHudNames:hud.filter(e=>!e.matches('.toast')).map(name),uiFocus:root.dataset.uiFocus||'',
   renderCalls:window.qaRenderCalls||0,lastRenderAge:performance.now()-(window.qaLastRenderAt||0),venueDraws:window.qaLastVenueDraws||[],
   toastContent,documentOverflow:document.documentElement.scrollWidth>innerWidth+1||document.documentElement.scrollHeight>innerHeight+1};
}'''

INSTRUMENT = r'''() => {
 const g=simbarGame,w=simbarApplication.world;
 window.qaTick=g.tick.bind(g);window.qaRealtime=false;window.qaTickCalls=0;
 g.tick=dt=>{qaTickCalls++;if(qaRealtime)qaTick(dt);};
 window.qaAdvance=seconds=>{for(let left=seconds;left>1e-8;left-=.05)qaTick(Math.min(.05,left));};
 const draw=CanvasRenderingContext2D.prototype.drawImage;window.qaFrameVenueDraws=[];window.qaRenderCalls=0;
 CanvasRenderingContext2D.prototype.drawImage=function(image,...args){
   if(this.canvas.id==='unity-canvas'&&image?.src&&/\/venue(?:-freebird|-ff)?\.png(?:[?#]|$)/.test(image.src))qaFrameVenueDraws.push({file:image.src.split('/').at(-1),loaded:!!image.complete&&image.naturalWidth>0,args});
   return draw.call(this,image,...args);
 };
 const render=w.render.bind(w);window.qaVisualFrozen=true;window.qaVisualTime=performance.now();window.qaHideCanvasOrders=false;
 w.render=(now,state)=>{qaFrameVenueDraws=[];const painted=qaHideCanvasOrders&&state?{...state,orders:[]}:state;const result=render(qaVisualFrozen?qaVisualTime:now,painted);qaRenderCalls++;window.qaLastVenueDraws=qaFrameVenueDraws.slice();window.qaLastRenderAt=performance.now();return result;};
 window.qaSetVisualFreeze=frozen=>{qaVisualTime=performance.now();qaVisualFrozen=!!frozen;};
}'''


def png_pixels(data):
    """Read screenshot pixels; this never rewrites any game art or screenshot."""
    with Image.open(BytesIO(data)) as source:
        pixels=source.convert('RGBA')
        return pixels.width,pixels.height,4,pixels.tobytes()


def crop_bytes(decoded,region):
    width,height,channels,pixels=decoded
    left=max(0,math.floor(region['left']));right=min(width,math.ceil(region['right']))
    top=max(0,math.floor(region['top']));bottom=min(height,math.ceil(region['bottom']))
    assert right>left and bottom>top,region
    return b''.join(pixels[(y*width+left)*channels:(y*width+right)*channels] for y in range(top,bottom))


def overlaps(rect,region,margin=4):
    return rect['left']<region['right']+margin and rect['right']>region['left']-margin and rect['top']<region['bottom']+margin and rect['bottom']>region['top']-margin


def settle(page):
    page.evaluate('() => new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)))')


def action(page,name,payload=None,within=None):
    settle(page)
    scope=page.locator(within) if within else page
    candidates=scope.locator('[data-action="'+name+'"]:visible')
    if payload is not None:
        for index in range(candidates.count()):
            button=candidates.nth(index)
            if json.loads(button.get_attribute('data-payload') or '{}')==payload:
                button.click();settle(page);return
        raise AssertionError('Missing visible action '+name+' '+str(payload))
    assert candidates.count(),name
    candidates.first.click();settle(page)


def art_evidence():
    paths=sorted((ROOT/'Tools/WebGLTemplate/FastGame/art').rglob('*'))
    hashes={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths if p.is_file() and p.suffix.lower() in ('.png','.json')}
    changed=subprocess.run(['git','diff','--name-only','HEAD','--','Tools/WebGLTemplate/FastGame/art','ArtSource/PixelArt'],cwd=ROOT,capture_output=True,text=True,check=True).stdout.splitlines()
    status=subprocess.run(['git','status','--short','--untracked-files=all','--','Tools/WebGLTemplate/FastGame/art','ArtSource/PixelArt'],cwd=ROOT,capture_output=True,text=True,check=True).stdout.splitlines()
    return{'sha256':hashes,'trackedArtChanges':changed,'gitStatus':status}


class Audit:
    def __init__(self,page,owner,out,views,report):
        self.page=page;self.owner=owner;self.out=out;self.views=views;self.report=report;self.calibrations={}

    def check(self,label,window=False,pixels=True):
        page=self.page;settle(page);data=page.evaluate(PROBE,HUD)
        width=data['viewport']['width'];height=data['viewport']['height'];slug=f'owner-{self.owner}-{width}x{height}-{label}'
        self.report['activeCase']={'owner':self.owner,'label':label,'width':width,'height':height}
        self.report['lastProbe']=data
        assert data['canvas']['height']==558,('The authored camera height changed',data)
        assert data['renderCalls']>0 and data['lastRenderAge']<250 and any(d['loaded'] for d in data['venueDraws']),('The real venue renderer stopped or no authored venue PNG was painted',slug,data)
        assert abs(data['points'][0]['y'])<.01 and abs(data['points'][2]['y']-height)<.1,data['points']
        assert abs(data['points'][1]['x']-width/2)<1 and abs(data['points'][1]['y']-height/2)<1,data['points']
        assert abs(data['points'][2]['x']-data['points'][0]['x']-960*height/558)<1.5,data['points']
        key=(width,height);calibration={'canvas':data['canvas'],'points':data['points'],'regions':data['regions']}
        if key in self.calibrations:assert calibration==self.calibrations[key],('Opening UI moved the camera or protected region',slug)
        else:self.calibrations[key]=calibration
        assert any(r['kind']=='stage' for r in data['regions']),data
        if self.owner==2:assert any(r['kind']=='sign' for r in data['regions']),data
        for region in data['regions']:
            assert region['left']>=0 and region['top']>=0 and region['right']<=width and region['bottom']<=height,('The complete protected object is outside the viewport',slug,region)
        for bubble in data['bubbles']:
            parts=[bubble['body']]+([bubble['tail']['bounds']] if bubble['tailVisible'] else [])
            assert bubble['tailVisible']==(bubble['tail'] is not None),bubble
            for part in parts:
                for region in data['regions']:
                    left,top,right,bottom=region['authored'];authored={'left':left,'top':top,'right':right,'bottom':bottom}
                    assert not overlaps(part,authored,4),('Canvas order UI touches protected art',slug,bubble,region)
            for part in [bubble['cssBody']]+([bubble['cssTail']] if bubble['tailVisible'] else []):
                assert part['left']>=0 and part['top']>=0 and part['right']<=width and part['bottom']<=height,('Canvas order UI is clipped offscreen',slug,bubble)
        assert not data['documentOverflow'] and not data['scrims'] and not data['unreachable'],data
        if data['toastContent']:
            toast=data['toastContent'];assert toast['scrollHeight']<=toast['height']+1 and toast['scrollWidth']<=toast['width']+1,('A timed notification is clipped or unreadable',slug,toast)
        assert all(s['reachesEnd'] for s in data['scrolls']),data['scrolls']
        if window:assert data['windowCount']>0,('Expected a protected venue window',data)
        if data['windowCount']:assert data['visibleHudCount']==0,('A venue window must hide the regular HUD; a protected toast is allowed',data)
        if data['uiFocus'] in ('quest','live'):
            assert len(data['visibleHudNames'])==1 and ('quest-panel' if data['uiFocus']=='quest' else 'live-console') in data['visibleHudNames'][0],('Focused cards must hide other regular HUD panels',data)
        for rect in data['panels']+data['descendants']:
            assert rect['left']>=-.5 and rect['top']>=-.5 and rect['right']<=width+.5 and rect['bottom']<=height+.5,('UI outside viewport',slug,rect)
            for region in data['regions']:assert not overlaps(rect,region),('UI touches a protected object',slug,rect,region)
        if pixels:
            before=page.screenshot()
            orders_before=page.evaluate('JSON.stringify(simbarGame.orders)')
            page.evaluate('qaHideCanvasOrders=true')
            hidden=page.add_style_tag(content='.venue-ui,.venue-window,.reference-ui,.css-layer{visibility:hidden!important}')
            try:settle(page);after=page.screenshot()
            finally:page.evaluate('qaHideCanvasOrders=false');hidden.evaluate('e=>e.remove()');settle(page)
            assert page.evaluate('JSON.stringify(simbarGame.orders)')==orders_before,'A screenshot reference changed the real order queue'
            first=png_pixels(before);second=png_pixels(after);comparisons=[]
            for region in data['regions']:
                a=crop_bytes(first,region);b=crop_bytes(second,region);same=a==b
                stride=max(1,len(a)//4//4096)*4;samples=[a[i:i+3] for i in range(0,len(a),stride)];colors=len(set(samples));lit=sum(max(p)>12 for p in samples)
                assert colors>=16 and lit>=len(samples)*.2,('Protected art is blank/black rather than the authored venue',slug,region,colors,lit,len(samples))
                comparisons.append({'kind':region['kind'],'identical':same,'sampledColors':colors,'litSamples':lit,'samples':len(samples),'shownSha256':hashlib.sha256(a).hexdigest(),'hiddenSha256':hashlib.sha256(b).hexdigest()})
                if not same:
                    (self.out/(slug+'-shown.png')).write_bytes(before);(self.out/(slug+'-hidden.png')).write_bytes(after)
                    raise AssertionError(('Protected pixels differ with UI shown',slug,region))
            data['protectedPixels']=comparisons
            data['referenceHides']='DOM UI and canvas order cards/tails (copied render state, real orders unchanged)'
            if label in ('hud','order','pause','settings','inspection','results','farewell'):
                (self.out/(slug+'.png')).write_bytes(before);data['screenshot']=slug+'.png'
        self.report['checks'].append({'owner':self.owner,'label':label,**data})
        print('PASS '+slug,flush=True)
        return data

    def matrix(self,label,window=False,pixels=True):
        for width,height in self.views:
            before=self.page.evaluate('qaRenderCalls')
            self.page.set_viewport_size({'width':width,'height':height});settle(self.page)
            data=self.check(label,window=window,pixels=pixels)
            assert data['renderCalls']>before,('The animation frame loop stopped after resize',label,width,height)


def launch_guide(page,owner):
    page.wait_for_function('window.simbarGameReadyAt>0')
    action(page,'owner',{'index':owner});page.evaluate('simbarGame._seed=1721');action(page,'start')
    page.locator('[data-desk-channel="monitor"]').click();page.locator('.desk-show-card:not([disabled])').first.click()
    action(page,'deskWork',{'task':0});page.locator('[data-desk-channel="phone"]').click();action(page,'deskWork',{'task':1});action(page,'launch')
    page.wait_for_function('simbarGame.phase==="guide"');page.evaluate(INSTRUMENT)


def close_window(page):
    settle(page)
    for name in ['ui:close','ui:floorClose']:
        if page.locator('.venue-window [data-action="'+name+'"]:visible').count():action(page,name,within='.venue-window');return
    if page.locator('.quest-panel.is-open .quest-toggle:visible').count():action(page,'ui:quests');return
    if page.locator('.live-collapse[aria-expanded="true"]:visible').count():action(page,'ui:liveFold');return
    raise AssertionError('The active venue panel has no documented close action')


def finish_ticks(page,stop_at_farewell=True,maximum=420):
    advanced=0
    while advanced<maximum:
        result=page.evaluate('''stop => {const g=simbarGame;let count=0;for(;count<100&&g.phase!=='results'&&!(stop&&g.farewellPending);count++){
          if(g.paused&&!g.farewellPending)throw new Error('Unexpected pause while finishing the actual closing');
          if(g.reaction?.activeKind)g.act('quick',{kind:g.reaction.activeKind});qaTick(.05);
        }return{count,phase:g.phase,farewell:g.farewellPending};}''',stop_at_farewell)
        advanced+=result['count']*.05
        if result['phase']=='results' or stop_at_farewell and result['farewell']:return{'seconds':advanced,**result}
        settle(page)
    raise AssertionError('Closing did not finish: '+str(page.evaluate('({phase:simbarGame.phase,paused:simbarGame.paused,afterShow:simbarGame.journey.afterShow,floor:simbarGame.floor})')))


def orientation_once(page,owner,out,report):
    original=page.viewport_size
    page.evaluate('qaSetVisualFreeze(false);qaRealtime=true');page.wait_for_timeout(120)
    assert page.evaluate('simbarGame.playing&&!simbarGame.paused')
    choice=page.evaluate('({playlist:simbarGame.state.playlist,musicPaused:simbarGame.journey.musicPaused})')
    snapshot=r'''() => {const g=simbarGame;return{elapsed:g.elapsed,seconds:g.journey.secondsLeft,festival:structuredClone(g.journey.festival),inspection:structuredClone(g.journey.inspection),orders:structuredClone(g.orders),pour:structuredClone(g.pour),request:g._requestTimer,reaction:structuredClone(g.reaction),message:g._messageTimer,paused:g.paused,pauseReason:g.pauseReason,selected:g.selectedItem,
      actors:g.actors.map(a=>({id:a.id,x:a.x,z:a.z,y:a.y,visible:a.visible})),band:simbarApplication.world.getBandPresentation(),focus:simbarApplication.root.dataset.uiFocus};}'''
    page.set_viewport_size({'width':390,'height':844})
    page.wait_for_function('simbarApplication.orientationBlocked&&document.querySelector(".orientation-gate").hidden===false')
    settle(page);page.wait_for_timeout(100)
    inert=page.evaluate('''() => {const app=simbarApplication;return['canvas','.venue-ui','.reference-ui','.css-layer'].every(s=>app.root.querySelector(s).inert);}''')
    assert inert,'Underlying game controls are not inert in portrait'
    gate=page.locator('.orientation-gate').bounding_box()
    assert gate and gate['x']==0 and gate['y']==0 and gate['width']==390 and gate['height']==844,gate
    assert page.evaluate('''() => [[8,8],[382,8],[8,836],[382,836],[195,422]].every(([x,y])=>!!document.elementFromPoint(x,y)?.closest('.orientation-gate'))'''),'Portrait does not fully conceal the game UI'
    before=page.evaluate(snapshot);calls=page.evaluate('qaTickCalls')
    page.wait_for_timeout(400)
    for key in ['m','e','1','Space','p','Escape','h','c','f']:page.keyboard.press(key)
    page.evaluate('''() => {for(const s of ['.menu-ready','[data-action="ui:quests"]','[data-action="ui:musicNext"]','[data-action="pause"]'])document.querySelector(s)?.click();}''')
    settle(page);assert page.evaluate(snapshot)==before,'Portrait changed a live clock/position or accepted hidden gameplay input'
    assert page.evaluate('qaTickCalls')==calls,'Portrait called the live game tick'
    media=page.evaluate('''() => ({portrait:simbarApplication.orientationBlocked,media:qaMedia.map(a=>({paused:a.paused,time:a.currentTime,src:a.currentSrc,ready:a.readyState,network:a.networkState})),calls:qaMediaCalls.slice(-20)})''')
    if not all(a['paused'] for a in media['media']):report['orientationFailure']=media
    assert all(a['paused'] for a in media['media']),('Music was not suspended in portrait',media)
    assert page.evaluate('({playlist:simbarGame.state.playlist,musicPaused:simbarGame.journey.musicPaused})')==choice
    page.screenshot(path=str(out/f'owner-{owner}-portrait.png'))
    page.set_viewport_size(original);page.wait_for_function('!simbarApplication.orientationBlocked&&document.querySelector(".orientation-gate").hidden===true')
    settle(page);page.wait_for_timeout(180)
    page.evaluate('qaRealtime=false;qaSetVisualFreeze(true)');settle(page)
    after=page.evaluate(snapshot);delta=after['elapsed']-before['elapsed']
    assert 0<delta<.6 and not after['paused'],('Landscape did not resume smoothly',delta,after)
    assert page.evaluate('({playlist:simbarGame.state.playlist,musicPaused:simbarGame.journey.musicPaused})')==choice,'Orientation changed the chosen music state'
    if not choice['musicPaused']:page.wait_for_function('qaMedia.some(a=>a.src&&!a.paused)',timeout=5000)
    report['orientation'].append({'owner':owner,'portraitClockAndPositionsExact':True,'inertAndVisuallyHidden':True,'blockedInputs':True,'musicChoicePreserved':True,'smoothResumeSeconds':delta})


def event_windows(page,audit,report):
    # Import the already loaded production module only to create legitimate
    # game-event fixtures; no core or renderer function is replaced here.
    page.evaluate(r'''async () => {const boot=[...document.scripts].find(s=>/\/FastGame\/.*boot\.js(?:$|\?)/.test(s.src));
      if(!boot)throw new Error('Missing production boot module');window.qaModules=await import(new URL('./core.js',boot.src));
      const {Floor,Challenges}=qaModules,g=simbarGame;Floor.spawnGuest(g.floor,1);g._ensureGuest();Challenges.clear(g.reaction);g.reaction.cooldown=0;Challenges.start(g.reaction,2,0);
    }''')
    for stage in range(3):
        page.wait_for_function('simbarGame.reaction.activeKind===2')
        audit.matrix('guest-reaction-'+str(stage),window=True)
        if stage==0:
            page.keyboard.press('e');page.wait_for_function('!!simbarGame.pour');settle(page)
            assert page.locator('.venue-window.has-multiple-windows').count()==1
            audit.matrix('reaction-and-pour',window=True)
            page.evaluate('''() => {const p=simbarGame.pour;qaAdvance(Math.max(.16,(p.target-.04)/.82-p.elapsed));}''');action(page,'pourStop')
            page.evaluate('qaAdvance(.2)');settle(page)
            if page.locator('.venue-window .order-dialog').is_visible():close_window(page)
        for _ in range(8):action(page,'quick',{'kind':2});page.evaluate('qaAdvance(.07)')
        if stage<2:page.evaluate('qaAdvance(.6)')
    for _ in range(45):
        if page.evaluate('simbarGame.guestExitFinished'):break
        page.evaluate('qaAdvance(1)')
    assert page.evaluate('simbarGame.guestExitFinished'),'The real assisted guest never reached the exit'
    page.locator('#unity-canvas').focus();page.keyboard.press('h');settle(page);audit.matrix('floor-status',window=True);close_window(page)
    action(page,'ui:liveFold');action(page,'ui:liveTab',{'tab':'festival'});action(page,'festival',{'id':'horns'});close_window(page)
    assert page.evaluate('simbarGame.act("inspection").success');settle(page);audit.matrix('inspection-warning')
    page.evaluate('qaAdvance(8)');settle(page)
    assert page.evaluate('simbarGame.inspection.officersPresent')==3
    assert all(a['action']=='idle' for a in page.evaluate('simbarApplication.world.getAudiencePresentation()'))
    page.evaluate('qaSetVisualFreeze(false)');frames=[]
    for _ in range(8):page.wait_for_timeout(100);frames.append(page.evaluate('simbarApplication.world.getBandPresentation()'))
    assert all(all(a['action']=='perform' for a in frame) for frame in frames)
    assert len({tuple(a['column'] for a in frame) for frame in frames})>1,'Inspection stopped the stage performance'
    page.evaluate('qaSetVisualFreeze(true)');audit.matrix('inspection')
    action(page,'ui:liveFold')
    before=page.evaluate('JSON.stringify({festival:simbarGame.journey.festival,feedback:simbarGame.journey.feedback,money:simbarGame.state.money,fans:simbarGame.state.fans,score:simbarGame.journey.score})')
    action(page,'festival',{'id':'dance'})
    assert page.locator('[data-bind="toast"]:visible').inner_text().strip()=='단속반이 지나가면 가능합니다.'
    assert page.evaluate('simbarGame.lastResult.code')=='inspection_blocked'
    assert page.evaluate('JSON.stringify({festival:simbarGame.journey.festival,feedback:simbarGame.journey.feedback,money:simbarGame.state.money,fans:simbarGame.state.fans,score:simbarGame.journey.score})')==before
    audit.matrix('inspection-message')
    page.evaluate('qaAdvance(45)');settle(page)
    if page.locator('.venue-window .order-dialog').is_visible():close_window(page)
    assert page.evaluate('simbarGame.inspection.phase')=='done'
    report['inspection'].append({'owner':audit.owner,'officers':3,'audienceIdle':True,'bandContinues':True,'exactMessage':True,'noRewards':True})
    page.evaluate('''() => {simbarGame.journey.secondsLeft=.05;qaAdvance(.1);}''');audit.matrix('closing')
    for _ in range(100):
        if page.evaluate('simbarGame.reaction.activeKind===1'):break
        page.evaluate('qaAdvance(.25)')
    assert page.evaluate('simbarGame.reaction.activeKind')==1
    audit.matrix('cleanup-reaction',window=True)
    for _ in range(10):action(page,'quick',{'kind':1});page.evaluate('qaAdvance(.07)')
    first=finish_ticks(page);assert page.evaluate('simbarGame.farewellPending')
    audit.matrix('farewell',window=True);action(page,'farewellAck')
    second=finish_ticks(page,stop_at_farewell=False);assert page.evaluate('simbarGame.phase')=='results'
    assert not page.evaluate('simbarGame.floorTasksPending')
    audit.matrix('results',window=True)
    report['closing'].append({'owner':audit.owner,'beforeFarewell':first,'afterAcknowledgement':second,'allExited':page.evaluate('simbarGame.actors.filter(a=>["guest","seated","support","surfer","band"].includes(a.role)).every(a=>a.visible===false)')})


def exercise_owner(page,owner,out,views,report):
    """Actions use the production controls; direct fixtures only create events."""
    launch_guide(page,owner);audit=Audit(page,owner,out,views,report)
    initial=page.evaluate('''() => ({orders:simbarGame.orders.map(o=>({id:o.id,item:o.item})),positions:simbarGame.actors.map(a=>({id:a.id,x:a.x,z:a.z,y:a.y}))})''')
    assert len(initial['orders'])==3,'The fresh seed1721 fixture must retain the natural three opening orders'
    for step in range(3):audit.matrix('guide-'+str(step+1),window=True);action(page,'guide')
    page.wait_for_function('simbarGame.playing');page.locator('.venue-window .order-dialog').wait_for(state='visible')
    assert page.evaluate('simbarGame.actors.map(a=>({id:a.id,x:a.x,z:a.z,y:a.y}))')==initial['positions'],'Opening order UI moved actors before the first gameplay tick'
    assert page.evaluate('simbarApplication.world.getOrderBubblePresentation().length')==3
    report['initialOrders'].append({'owner':owner,'seed':1721,'orders':initial['orders'],'beforeAnyGuestMovement':True})
    audit.matrix('order',window=True);assert page.locator('.order-dialog [data-action="menuItem"]').count()==6
    close_window(page);audit.matrix('hud')
    action(page,'ui:quests');audit.matrix('quests');close_window(page)
    action(page,'ui:liveFold');audit.matrix('crowd-actions')
    action(page,'ui:liveTab',{'tab':'music'});audit.matrix('playlist')
    assert page.locator('.track-list button').count()==5
    catalog_artists=page.evaluate(r'''async () => {const boot=[...document.scripts].find(s=>/\/FastGame\/.*boot\.js(?:$|\?)/.test(s.src));const {catalog}=await import(new URL('./core.js',boot.src));return catalog.playlists.map(p=>p.artist);}''')
    for index,expected in enumerate(catalog_artists):
        assert page.locator(f'.track-list [data-playlist="{index}"] .track-artist').inner_text()==expected
    artists=page.locator('.track-list').inner_text().casefold()
    assert 'freewill' in artists and ('julywantsmore' in artists.replace(' ','')),artists
    music_paused=page.evaluate('simbarGame.journey.musicPaused')
    action(page,'ui:musicToggle');assert page.evaluate('simbarGame.journey.musicPaused')!=music_paused
    action(page,'ui:musicToggle');assert page.evaluate('simbarGame.journey.musicPaused')==music_paused
    current=page.evaluate('simbarGame.state.playlist');action(page,'ui:musicNext');action(page,'ui:musicPrevious')
    assert page.evaluate('simbarGame.state.playlist')==current
    close_window(page)
    action(page,'ui:settings');audit.matrix('settings',window=True);close_window(page)
    page.keyboard.press('p');settle(page);audit.matrix('pause',window=True)
    reason=page.evaluate('simbarGame.pauseReason');action(page,'ui:settings');close_window(page)
    assert page.evaluate('simbarGame.paused') and page.evaluate('simbarGame.pauseReason')==reason
    action(page,'resume')
    orientation_once(page,owner,out,report);audit.matrix('orientation-recovered')
    page.evaluate('''() => {simbarGame.state.money=1000000;simbarGame.journey.completedNights=10;}''')
    action(page,'ui:upgrades')
    for tab in range(4):
        action(page,'ui:opPage',{'index':tab});audit.matrix('operations-'+str(tab),window=True)
    close_window(page)
    for category in range(3):
        for level in range(1,4):
            page.evaluate('''({category,level})=>{simbarGame.state.upgrades[category]=level-1;}''',{'category':category,'level':level})
            action(page,'ui:upgrades');action(page,'ui:visualPreview',{'type':'upgrade','index':category,'level':level})
            audit.matrix(f'upgrade-preview-{category}-{level}',window=True)
            selected=page.evaluate('simbarApplication.world.getVenueFeaturePresentation()')
            assert any(f['type']=='upgrade' and f['index']==category for f in selected),selected
            action(page,'ui:previewBack');action(page,'upgrade',{'index':category});assert page.evaluate(f'simbarGame.state.upgrades[{category}]')==level
            close_window(page)
    for index in range(3):
        page.evaluate('''() => {simbarGame.state.activeEvent=-1;simbarGame.state.eventRemaining=0;simbarGame.state.cooldowns=[0,0,0];}''')
        action(page,'ui:events');action(page,'ui:visualPreview',{'type':'event','index':index})
        audit.matrix('event-preview-'+str(index),window=True);action(page,'ui:previewBack');action(page,'event',{'index':index})
        assert page.evaluate('simbarGame.state.activeEvent')==index;close_window(page)
    action(page,'ui:service');action(page,'prepare');page.wait_for_function('!!simbarGame.pour')
    audit.matrix('pour',window=True)
    page.evaluate('''() => {const p=simbarGame.pour;qaAdvance(Math.max(.16,(p.target-.04)/.82-p.elapsed));}''');action(page,'pourStop')
    page.evaluate('qaAdvance(.2)');settle(page)
    if page.locator('.venue-window .order-dialog').is_visible():close_window(page)
    event_windows(page,audit,report)


def exercise_canvas_case(page,owner,out,view,report,overlay=False,inspection=False):
    """Fresh seed1721 opening + a few real walking seconds, per viewport."""
    launch_guide(page,owner);audit=Audit(page,owner,out,[view],report)
    positions=page.evaluate('simbarGame.actors.map(a=>({id:a.id,x:a.x,z:a.z,y:a.y}))')
    for _ in range(3):action(page,'guide')
    page.locator('.venue-window .order-dialog').wait_for(state='visible')
    assert page.evaluate('simbarGame.orders.length')==3
    assert page.evaluate('simbarGame.actors.map(a=>({id:a.id,x:a.x,z:a.z,y:a.y}))')==positions
    assert page.evaluate('simbarApplication.world.getOrderBubblePresentation().length')==3
    report['initialOrders'].append({'owner':owner,'width':view[0],'height':view[1],'seed':1721,'orders':page.evaluate('simbarGame.orders.map(o=>({id:o.id,item:o.item}))'),'beforeAnyGuestMovement':True})
    audit.check('canvas-initial',window=True);close_window(page)
    page.evaluate('qaAdvance(5)');audit.check('canvas-route-5s')
    page.evaluate('qaAdvance(7)');audit.check('canvas-route-12s')
    if overlay:
        action(page,'ui:settings');audit.check('canvas-settings',window=True);close_window(page)
        orientation_once(page,owner,out,report);audit.check('canvas-orientation-recovered')
    if inspection:
        action(page,'ui:liveTab',{'tab':'festival'});action(page,'festival',{'id':'horns'});close_window(page)
        assert page.evaluate('simbarGame.act("inspection").success')
        page.evaluate('qaAdvance(8)');action(page,'ui:liveTab',{'tab':'festival'})
        assert page.evaluate('simbarGame.inspection.officersPresent')==3
        page.evaluate('qaSetVisualFreeze(false)');frames=[]
        for _ in range(8):page.wait_for_timeout(100);frames.append(page.evaluate('simbarApplication.world.getBandPresentation()'))
        assert all(all(a['action']=='perform' for a in frame) for frame in frames)
        assert len({tuple(a['column'] for a in frame) for frame in frames})>1
        assert all(a['action']=='idle' for a in page.evaluate('simbarApplication.world.getAudiencePresentation()'))
        page.evaluate('qaSetVisualFreeze(true)');audit.check('canvas-inspection-expanded')
        before=page.evaluate('JSON.stringify({festival:simbarGame.journey.festival,feedback:simbarGame.journey.feedback,money:simbarGame.state.money,fans:simbarGame.state.fans,score:simbarGame.journey.score})')
        action(page,'festival',{'id':'dance'})
        assert page.locator('[data-bind="toast"]:visible').inner_text().strip()=='단속반이 지나가면 가능합니다.'
        assert page.evaluate('simbarGame.lastResult.code')=='inspection_blocked'
        assert page.evaluate('JSON.stringify({festival:simbarGame.journey.festival,feedback:simbarGame.journey.feedback,money:simbarGame.state.money,fans:simbarGame.state.fans,score:simbarGame.journey.score})')==before
        audit.check('canvas-inspection-blocked')
        report['inspection'].append({'owner':owner,'officers':3,'audienceIdle':True,'bandContinues':True,'exactMessage':True,'noRewards':True})


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--package',type=Path);parser.add_argument('--url')
    parser.add_argument('--owners',default='0,1,2');parser.add_argument('--views',default=','.join(f'{w}x{h}' for w,h in VIEWS))
    parser.add_argument('--canvas-only',action='store_true',help='Focused fresh-queue/canvas/rotate coverage composed with a previous complete DOM sweep.')
    parser.add_argument('--dom-report',type=Path,help='Previous complete DOM report used by a focused follow-up.')
    parser.add_argument('--output',type=Path,default=ROOT/'Temp/ProtectedUI-20261114/source');args=parser.parse_args()
    views=[tuple(map(int,item.split('x'))) for item in args.views.split(',')];out=args.output.resolve();out.mkdir(parents=True,exist_ok=True)
    art_before=art_evidence();assert not art_before['trackedArtChanges'] and not art_before['gitStatus'],art_before
    server=None
    if args.url:url=args.url
    else:
        directory=args.package.resolve() if args.package else ROOT
        QuietHandler.source_html=None if args.package else (ROOT/'Tools/WebGLTemplate/FastGame/index.html').read_text(encoding='utf-8').replace('__SIMBAR_ASSET_HINTS__','').replace('__SIMBAR_STYLESHEET__','/Tools/WebGLTemplate/style.css').replace('__SIMBAR_RUNTIME__','/Tools/WebGLTemplate/FastGame').replace('href="TemplateData/favicon.ico"','href="data:,"')
        server=ThreadingHTTPServer(('127.0.0.1',0),partial(QuietHandler,directory=str(directory)));threading.Thread(target=server.serve_forever,daemon=True).start()
        url=f'http://127.0.0.1:{server.server_port}/'+('' if args.package else '__owner-venues-qa__/index.html')
    report={'passed':False,'url':url,'checks':[],'initialOrders':[],'orientation':[],'inspection':[],'closing':[],'errors':[],'httpErrors':[],
      'profile':'focused canvas + orientation' if args.canvas_only else 'complete protected UI',
      'pendingCases':['1024x768 tablet camera comparison awaits the user decision; no production pan is approved.'],
      'art':{'count':len(art_before['sha256']),'sha256Before':art_before['sha256']}}
    if args.dom_report:
        data=args.dom_report.read_bytes();previous=json.loads(data);assert previous['passed']
        report['composition']={'domReport':str(args.dom_report.resolve()),'domReportSha256':hashlib.sha256(data).hexdigest(),'domChecks':len(previous['checks']),'domPassed':True,'additionalCoverage':'Fresh initial3-order queues and actual card/tail geometry/reference removal, genuine portrait cycle per owner; renderer unit order-protection snapshots are reported separately.'}
    try:
        with sync_playwright() as pw:
            browser=pw.chromium.launch(executable_path=chromium_executable(None))
            try:
                cases=[(owner,view) for owner in map(int,args.owners.split(',')) for view in (views if args.canvas_only else [views[0]])]
                for owner,view in cases:
                    context=browser.new_context(viewport={'width':view[0],'height':view[1]},locale='ko-KR',device_scale_factor=1)
                    context.add_init_script(MEDIA_CAPTURE)
                    page=context.new_page();page.on('pageerror',lambda e:report['errors'].append(str(e)));page.on('response',lambda r:report['httpErrors'].append({'url':r.url,'status':r.status}) if r.status>=400 else None)
                    page.goto(url)
                    try:
                        if args.canvas_only:exercise_canvas_case(page,owner,out,view,report,overlay=view==views[0],inspection=view==views[-1])
                        else:exercise_owner(page,owner,out,views,report)
                    except Exception as exc:
                        report['failure']={'owner':owner,'error':repr(exc)}
                        page.screenshot(path=str(out/f'failure-owner-{owner}.png'))
                        raise
                    context.close()
                assert not report['errors'] and not report['httpErrors'],report
                art_after=art_evidence();assert art_after==art_before,'QA or implementation changed source art'
                report['art']['unchanged']=True;report['passed']=True
            finally:browser.close()
    finally:
        (out/'report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
        if server:server.shutdown();server.server_close()
    print(json.dumps({'passed':report['passed'],'checks':len(report['checks']),'report':str(out/'report.json'),'errors':report['errors'],'httpErrors':report['httpErrors']}))


if __name__=='__main__':main()
