"""Check artist stage models through the real native game's controls.

Run ``uv run --with playwright python Tools/qa_native_band_models.py`` for
source, or add ``--package docs`` for the published folder. This uses isolated
browser profiles and a retained pre-change baseline; it never edits game saves
or the user's browser. Output includes six venue/artist screenshots and JSON.
"""

import argparse
import base64
from collections import Counter
from functools import partial
import hashlib
from http.server import ThreadingHTTPServer
import json
from pathlib import Path
import threading

from playwright.sync_api import sync_playwright

from benchmark_web_loading import chromium_executable
from qa_native_freebird_layout import launch_night
from qa_native_owner_venues import QuietHandler, ready, save_canvas


ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / 'Temp/BandSwitch-20261110/qa'
SNAPSHOT = ROOT / 'Temp/BandSwitch-20261110/baseline/FastGame'
RETAINED = ROOT / 'docs/FastGame/d76977a2fb1f532c'
UNCHANGED = ['core.js', 'aftershow.js', 'music.js', 'playlist.js', 'catalog.js',
             'navigation.js', 'floor.js', 'booking.js', 'owner-venues.js',
             'art/venue.png', 'art/venue-ff.png', 'art/venue-freebird.png',
             'art/venue.json', 'art/venue-ff.json', 'art/venue-freebird.json',
             'art/standing-table.png', 'art/standing-stool.png', 'art/sprites.json']
BANDS = ['freewill', 'julywantsmore', 'julywantsmore', 'freewill', 'freewill']
R2 = 'https://pub-f61dee98441a42719d944abea5e0e1d2.r2.dev/'
FILES = ['Electric%20Shocker.mp3', 'Hey_spotdown.mp3', 'No%20Matter%20How.mp3',
         'Bedroom%20Manifesto.mp3', 'Electric%20Shocker%20-%20Korean%20Version.mp3']
LABELS = ['jessy-freebird', 'edison-ff', 'jaiden-original']


class Handler(QuietHandler):
    baseline = SNAPSHOT

    def translate_path(self, path):
        if path.startswith('/__bandbaseline__/'):
            relative = path.split('?', 1)[0].removeprefix('/__bandbaseline__/')
            target = (self.baseline / relative).resolve()
            return str(target if target.is_relative_to(self.baseline.resolve())
                       else self.baseline / '__invalid__')
        return super().translate_path(path)


# The media elements are intentionally not inserted into the DOM. Observe the
# native constructor while preserving its prototype and normal network behavior.
INSTRUMENT = r'''() => {
  const AudioClass=window.Audio;
  window.qaAudios=[];
  window.Audio=function(...args){const audio=new AudioClass(...args);qaAudios.push(audio);return audio;};
  window.Audio.prototype=AudioClass.prototype;Object.setPrototypeOf(window.Audio,AudioClass);
  window.bandDraws=[];window.oldBandDraws=[];window.bandDots=0;
  const draw=CanvasRenderingContext2D.prototype.drawImage;
  CanvasRenderingContext2D.prototype.drawImage=function(image,...args){
    if(this.canvas.id==='unity-canvas'&&image?.src){
      const file=new URL(image.src).pathname.split('/').pop();
      if(/^band-(freewill|july)\.png$/.test(file))window.bandDraws.push({file,args});
      if(/^v4-band-/.test(file))window.oldBandDraws.push(file);
    }
    return draw.call(this,image,...args);
  };
  const fill=CanvasRenderingContext2D.prototype.fillRect;
  CanvasRenderingContext2D.prototype.fillRect=function(x,y,w,h){
    if(this.canvas.id==='mini-map'&&this.fillStyle==='#65dbef'&&w===3&&h===3)window.bandDots++;
    return fill.call(this,x,y,w,h);
  };
}'''


WRAP = r'''() => {
  const world=simbarApplication.world,main=world.render,map=world.renderOverview;
  world.render=function(...args){window.bandDraws=[];window.oldBandDraws=[];return main.apply(this,args);};
  world.renderOverview=function(...args){window.bandDots=0;return map.apply(this,args);};
}'''


SNAPSHOT_STATE = r'''() => ({
  cash:simbarGame.cash,elapsed:simbarGame.elapsed,secondsLeft:simbarGame.journey.secondsLeft,
  coreActors:simbarGame.actors.map(a=>({id:a.id,role:a.role,x:a.x,z:a.z,y:a.y,
    action:a.action,visible:a.visible,stageLevel:a.stageLevel})),
  band:simbarApplication.world.getBandPresentation(),
  playlist:simbarGame.state.playlist,musicPaused:simbarGame.journey.musicPaused,
  drawn:structuredClone(window.bandDraws),oldDrawn:structuredClone(window.oldBandDraws),
  minimapBandDots:window.bandDots,
  audio:qaAudios.at(-1)?{src:qaAudios.at(-1).src,paused:qaAudios.at(-1).paused,
    readyState:qaAudios.at(-1).readyState,currentTime:qaAudios.at(-1).currentTime,
    count:qaAudios.length}:null
})'''


ATLAS_QA = r'''async () => {
  const boot=document.querySelector('script[src$="/boot.js"]').src,base=new URL('art/',boot);
  const manifest=await fetch(new URL('band-models.json',base)).then(r=>r.json()),out=[];
  for(const band of manifest.bands){
    const image=new Image();image.src=new URL(band.file,base);await image.decode();
    const c=document.createElement('canvas');c.width=c.height=64;const ctx=c.getContext('2d'),rows=[];
    for(const member of band.members){
      const frames=[];
      for(let column=0;column<12;column++){
        ctx.clearRect(0,0,64,64);ctx.drawImage(image,column*64,member.row*64,64,64,0,0,64,64);
        const pixels=ctx.getImageData(0,0,64,64).data;let opaque=0,hash=2166136261;
        for(let i=0;i<pixels.length;i++){hash=Math.imul(hash^pixels[i],16777619);if(i%4===3&&pixels[i]>0)opaque++;}
        frames.push({column,opaque,hash:hash>>>0});
      }
      rows.push({id:member.id,row:member.row,frames,
        distinctPerform:new Set(frames.slice(0,4).map(f=>f.hash)).size,
        distinctWalk:new Set(frames.slice(4,8).map(f=>f.hash)).size,
        distinctGreeting:new Set(frames.slice(8,12).map(f=>f.hash)).size});
    }
    out.push({id:band.id,file:band.file,width:image.naturalWidth,height:image.naturalHeight,rows});
  }
  return out;
}'''


RENDER_PARITY = r'''async () => {
  const app=simbarApplication,boot=document.querySelector('script[src$="/boot.js"]').src,base=new URL('art/',boot);
  const [{createWorld:before},{createWorld:after},spriteMeta,venueMeta,ffMeta,freebirdMeta,bandModels]=await Promise.all([
    import('/__bandbaseline__/world.js'),import(new URL('world.js',boot)),
    ...['sprites.json','venue.json','venue-ff.json','venue-freebird.json','band-models.json'].map(f=>fetch(new URL(f,base)).then(r=>r.json()))
  ]);
  const load=async file=>{const image=new Image();image.src=new URL(file,base);await image.decode();return image;};
  const [venue,seoul,freebird,ff,table,stool]=await Promise.all(['venue.png','seoul.png','venue-freebird.png','venue-ff.png','standing-table.png','standing-stool.png'].map(load));
  const atlases=Object.fromEntries(await Promise.all(Object.entries(spriteMeta.atlases).map(async([k,v])=>[k,await load(v.file)])));
  const bandAtlases=Object.fromEntries(await Promise.all(bandModels.bands.map(async b=>[b.id,await load(b.file)])));
  const assets=()=>({venue,seoul,spriteMeta,venueMeta,atlases,albumCovers:{},venues:{freebird,ff},
    venueMetas:{ff:ffMeta,freebird:freebirdMeta},propImages:{'standing-table':table,'standing-stool':stool},bandModels,bandAtlases});
  const canvas=()=>{const c=document.createElement('canvas');c.width=960;c.height=558;c.style.cssText='position:fixed;left:-10000px;top:0;width:960px;height:558px';document.body.append(c);return c;};
  const a=canvas(),b=canvas(),old=before(a,{assets:assets()}),current=after(b,{assets:assets()});
  const ma=document.createElement('canvas'),mb=document.createElement('canvas');ma.width=mb.width=320;ma.height=mb.height=186;
  const equal=(x,y)=>{const p=x.getContext('2d').getImageData(0,0,x.width,x.height).data,q=y.getContext('2d').getImageData(0,0,y.width,y.height).data;
    let differences=0,maxDelta=0;for(let i=0;i<p.length;i++){if(p[i]!==q[i])differences++;maxDelta=Math.max(maxDelta,Math.abs(p[i]-q[i]));}
    return{equal:differences===0,differingChannels:differences,maxDelta,channels:p.length};};
  const results=[];
  try{
    for(const [label,actors] of [['empty',[]],['real-nonband-actors',structuredClone(app.g.actors.filter(a=>a.role!=='band'))]]){
      const state={...app.g.state,owner:app.g.owner,phase:'playing',paused:true,journey:structuredClone(app.g.journey),floor:structuredClone(app.g.floor),actors,orders:[],effects:[]};
      const frozen=JSON.stringify(state);
      old.render(0,state);current.render(0,state);old.renderOverview(ma.getContext('2d'),state);current.renderOverview(mb.getContext('2d'),state);
      results.push({label,actorCount:actors.length,main:equal(a,b),minimap:equal(ma,mb),stateUnchanged:JSON.stringify(state)===frozen});
    }
    return results;
  }finally{old.destroy();current.destroy();a.remove();b.remove();}
}'''


BEHAVIOR = r'''async () => {
  const boot=document.querySelector('script[src$="/boot.js"]').src;
  const [{SimBarGame:Before},{SimBarGame:After},manifest]=await Promise.all([
    import('/__bandbaseline__/core.js'),import(new URL('core.js',boot)),fetch(new URL('art/band-models.json',boot)).then(r=>r.json())
  ]);
  const make=(Class,owner,index)=>{
    const values=new Map(),storage={getItem:k=>values.get(k)??null,setItem:(k,v)=>values.set(k,v)};
    const g=new Class({storage,seed:418251});g.act('owner',{index:owner});g.act('start');
    g.act('deskSelect',{selected:true});g.act('deskWork',{task:0});g.act('deskWork',{task:1});g.act('launch');
    while(g.phase==='guide')g.act('guide');g.act('playlist',{index});for(let i=0;i<3;i++)g.act('hire',{index:i});return g;
  };
  const state=g=>({...g.state,owner:g.owner,phase:g.phase,paused:g.paused,journey:g.journey,floor:g.floor,
    actors:g.actors,orders:g.orders,effects:g.effects,elapsed:g.elapsed,cleanupReadyWorkers:g.cleanupReadyWorkers,guestExitFinished:g.guestExitFinished});
  const out=[];
  // All three rooms have exact pixel/asset guards above. Both complete artist
  // lifecycles in FF exercise the shared, unchanged four-anchor choreography.
  for(const owner of [1])for(const index of [0,1]){
    const old=make(Before,owner,index),g=make(After,owner,index),band=manifest.bands[index===0?0:1];
    const item={fixture:'targeted-closing',owner,index,bandId:band.id,initialActors:g.actors.length,initialCoreBand:g.actors.filter(a=>a.role==='band').map(a=>a.id),
      violations:[],extraFloorSamples:0,greetingSamples:0,phaseSamples:{},presentationSamples:0,initialSaveEqual:old.exportSave()===g.exportSave()};
    // The artist change does not alter any game rules (hash-guarded separately).
    // Enter closing directly to exercise both complete stage-to-exit visuals.
    g.journey.secondsLeft=.05;g.save();
    let ticks=0;
    const advance=game=>{game.tick(.1);if(game.reaction?.activeKind)game.act('quick',{kind:game.reaction.activeKind});if(game.farewellPending)game.act('farewellAck');};
    for(;ticks<3000&&g.phase!=='results';ticks++){
      advance(g);
      const input=state(g),frozen=ticks%20===0?JSON.stringify(input):null,models=simbarApplication.world.getBandPresentation(input);
      if(frozen!==null){item.presentationSamples++;if(JSON.stringify(input)!==frozen)item.violations.push({tick:ticks,kind:'presentation-mutated-state'});}
      const expected=band.members.filter(m=>g.actors.find(a=>a.id===m.anchor)?.visible!==false);
      if(models.length!==expected.length)item.violations.push({tick:ticks,kind:'model-count',actual:models.length,expected:expected.length});
      if(models.some(m=>m.bandId!==band.id))item.violations.push({tick:ticks,kind:'wrong-artist'});
      const guitarist=g.actors.find(a=>a.id==='guitarist'),primary=models.find(m=>m.anchor==='guitarist'&&!m.extra);
      for(const member of band.members.filter(m=>m.extra)){
        const model=models.find(m=>m.id===member.id);
        if(!!model!==(guitarist.visible!==false))item.violations.push({tick:ticks,kind:'extra-visibility',id:member.id});
        if(model&&primary&&guitarist.stageLevel===0){
          item.extraFloorSamples++;
          if(Math.abs(model.x-primary.x-member.floorOffset.x)>1e-7||Math.abs(model.y-primary.y-member.floorOffset.y)>1e-7)item.violations.push({tick:ticks,kind:'floor-offset',id:member.id});
        }
        if(model&&['highfive','hug'].includes(guitarist.action)&&model.action!=='idle')item.violations.push({tick:ticks,kind:'extra-false-greeting',id:member.id});
      }
      for(const model of models){
        item.phaseSamples[model.action]=(item.phaseSamples[model.action]||0)+1;
        if(['highfive','hug'].includes(model.action)){item.greetingSamples++;if(model.column<8||model.column>11)item.violations.push({tick:ticks,kind:'greeting-column'});}
      }
      if(ticks>0&&ticks%1000===0&&globalThis.bandQAProgress)await globalThis.bandQAProgress({kind:'tick',owner,index,ticks,phase:g.phase,elapsed:g.elapsed});
    }
    item.ticks=ticks;item.phase=g.phase;item.elapsed=g.elapsed;item.workers=g.crew.length;
    item.cleanupComplete=g.floor.cleanupComplete;item.litter=g.floor.litter;item.cleaned=g.floor.cleaned;
    item.guestSpawned=g.floor.guestSpawned;item.guestComplete=g.floor.guestComplete;item.guestExitFinished=g.guestExitFinished;
    item.afterShowComplete=g.journey.afterShow?.completed;
    item.finalRoster=simbarApplication.world.getBandPresentation(state(g));
    g.act('playlist',{index:1-index});item.rosterAfterResultTrackSwitch=simbarApplication.world.getBandPresentation(state(g));
    out.push(item);
    if(globalThis.bandQAProgress)await globalThis.bandQAProgress({kind:'complete',owner,index,phase:item.phase,elapsed:item.elapsed,violations:item.violations.length});
  }
  return out;
}'''


def check_models(snapshot, index):
    band = BANDS[index]
    count = 6 if band == 'freewill' else 5
    expected_file = 'band-freewill.png' if band == 'freewill' else 'band-july.png'
    models = snapshot['band']
    assert snapshot['playlist'] == index and len(models) == count, snapshot
    assert {m['bandId'] for m in models} == {band}, models
    assert Counter(m['role'] for m in models) == Counter(
        {'singer': 1, 'guitarist': count - 3, 'bassist': 1, 'drummer': 1}), models
    assert sorted(m['row'] for m in models) == list(range(count)), models
    assert len({m['id'] for m in models}) == count, models
    assert sum(bool(m['extra']) for m in models) == count - 4, models
    assert not snapshot['oldDrawn'], snapshot['oldDrawn']
    assert len(snapshot['drawn']) == count, snapshot
    assert {d['file'] for d in snapshot['drawn']} == {expected_file}, snapshot
    assert sorted(d['args'][1] // 64 for d in snapshot['drawn']) == list(range(count)), snapshot
    assert all(len(d['args']) == 8 and d['args'][2:4] == [64, 64]
               and 0 <= d['args'][0] // 64 < 12 for d in snapshot['drawn']), snapshot
    assert snapshot['minimapBandDots'] == count, snapshot
    return {'index': index, 'bandId': band, 'models': models,
            'drawn': snapshot['drawn'], 'minimapBandDots': snapshot['minimapBandDots'],
            'audio': snapshot['audio']}


def playing_audio(page, index):
    expected = R2 + FILES[index]
    page.wait_for_function('''src => {const a=qaAudios.at(-1);return a&&a.src===src&&a.readyState>=2&&!a.paused&&a.currentTime>0;}''',
                           arg=expected, timeout=30000)
    return expected


def stable_state(snapshot):
    return {key: snapshot[key] for key in ['cash', 'elapsed', 'secondsLeft', 'coreActors']}


def proof(page, index):
    page.wait_for_function('''i => simbarGame.state.playlist===i&&window.bandDraws?.length===
      ([0,3,4].includes(i)?6:5)&&window.bandDots===([0,3,4].includes(i)?6:5)''', arg=index)
    return page.evaluate(SNAPSHOT_STATE)


def run_owner(page, owner, output, report):
    page.locator('.owner-card').nth(owner).click()
    launch_night(page)
    page.evaluate(WRAP)
    page.wait_for_timeout(80)
    start = page.evaluate(SNAPSHOT_STATE)
    assert len(start['coreActors']) == 40, start
    item = {'owner': owner, 'tracks': [], 'screenshots': []}
    for index in range(5):
        before = page.evaluate(SNAPSHOT_STATE)
        page.locator(f'button[data-action="playlist"][data-playlist="{index}"]').click()
        playing_audio(page, index)
        shot = proof(page, index)
        assert stable_state(before) == stable_state(shot), {'before': before, 'after': shot}
        item['tracks'].append(check_models(shot, index))
        if index in (0, 1):
            label = LABELS[owner] + '-' + BANDS[index]
            page.screenshot(path=str(output / (label + '.png')))
            save_canvas(page, '#unity-canvas', output / (label + '-world.png'))
            save_canvas(page, '#mini-map', output / (label + '-minimap.png'))
            names = [label + suffix + '.png' for suffix in ['', '-world', '-minimap']]
            item['screenshots'].extend(names)
            report['screenshots'].extend(names)

    # Exercise real adjacent controls across the artist boundary, including wrap.
    controls = []
    for action, index in [('ui:musicNext', 0), ('ui:musicNext', 1), ('ui:musicPrevious', 0)]:
        page.locator(f'button[data-action="{action}"]').click()
        playing_audio(page, index)
        controls.append(check_models(proof(page, index), index))
    item['adjacentControls'] = controls

    # Audio pause preserves the selected artist and keeps the visible stage.
    playing = page.evaluate(SNAPSHOT_STATE)
    page.locator('button[data-action="ui:musicToggle"]').click()
    page.wait_for_function('simbarGame.journey.musicPaused&&qaAudios.at(-1).paused')
    paused = proof(page, 0)
    assert paused['band'] == playing['band'], {'playing': playing, 'paused': paused}
    assert paused['audio']['count'] == playing['audio']['count'], paused
    page.wait_for_timeout(180)
    assert stable_state(paused) == stable_state(page.evaluate(SNAPSHOT_STATE)), paused
    page.locator('button[data-action="ui:musicToggle"]').click()
    playing_audio(page, 0)
    item['musicPause'] = {'artistPreserved': True, 'elementPreserved': True, 'pausedCoreStatePreserved': True}

    # Rendering advances actual performance frames on the unpaused game clock.
    columns = []
    page.keyboard.press('p')
    page.wait_for_function('!simbarGame.paused')
    for _ in range(6):
        page.wait_for_timeout(140)
        columns.append(page.evaluate('simbarApplication.world.getBandPresentation().map(m=>m.column)'))
    page.keyboard.press('p')
    page.wait_for_function('simbarGame.paused')
    assert len({row[0] for row in columns}) >= 2, columns
    assert all(0 <= c < 4 for row in columns for c in row), columns
    item['animationColumns'] = columns

    # Keep the saved July choice and its music pause state across a real reload.
    page.locator('button[data-action="playlist"][data-playlist="1"]').click()
    playing_audio(page, 1)
    page.locator('button[data-action="ui:musicToggle"]').click()
    page.wait_for_function('simbarGame.journey.musicPaused&&qaAudios.at(-1).paused')
    saved = proof(page, 1)
    page.reload()
    ready(page)
    assert page.evaluate('simbarGame.owner') == owner
    assert page.evaluate('simbarGame.state.playlist') == 1
    assert page.evaluate('simbarGame.journey.musicPaused')
    assert page.evaluate('simbarGame.cash') == saved['cash']
    assert page.evaluate('simbarGame.journey.secondsLeft') == saved['secondsLeft']
    page.locator('button[data-action="continue"]').click()
    page.wait_for_function('simbarGame.phase==="playing"')
    page.keyboard.press('p')
    page.wait_for_function('simbarGame.paused')
    page.add_style_tag(content='.pause-ref{display:none!important}.screen-shade:has(.pause-ref){display:none!important}')
    page.evaluate(WRAP)
    restored = proof(page, 1)
    check_models(restored, 1)
    assert restored['cash'] == saved['cash'] and abs(restored['secondsLeft'] - saved['secondsLeft']) < .5, restored
    assert restored['musicPaused'] and restored['audio']['paused'], restored
    item['reload'] = {'artist': BANDS[1], 'count': len(restored['band']), 'cashPreserved': True,
                      'savedClockPreserved': True, 'musicPausePreserved': True}
    item['nonbandBaseline'] = page.evaluate(RENDER_PARITY)
    assert all(r['main']['equal'] and r['minimap']['equal'] and r['stateUnchanged']
               for r in item['nonbandBaseline']), item['nonbandBaseline']
    return item


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--package', type=Path)
    parser.add_argument('--baseline', type=Path)
    parser.add_argument('--output', type=Path, default=OUT)
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    baseline = (args.baseline or (SNAPSHOT if SNAPSHOT.is_dir() else RETAINED)).resolve()
    if not (baseline / 'world.js').is_file():
        raise FileNotFoundError('A retained pre-band-model FastGame baseline is required.')
    Handler.baseline = baseline
    Handler.source_html = None
    document_root = args.package.resolve() if args.package else ROOT
    if not args.package:
        html = (ROOT / 'Tools/WebGLTemplate/FastGame/index.html').read_text(encoding='utf-8')
        Handler.source_html = html.replace('__SIMBAR_ASSET_HINTS__', '').replace('__SIMBAR_STYLESHEET__', '/Tools/WebGLTemplate/style.css').replace('__SIMBAR_RUNTIME__', '/Tools/WebGLTemplate/FastGame').replace('href="TemplateData/favicon.ico"', 'href="data:,"')
    server = ThreadingHTTPServer(('127.0.0.1', 0), partial(Handler, directory=str(document_root)))
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    url = f'http://127.0.0.1:{server.server_port}/' + ('' if args.package else '__owner-venues-qa__/index.html')
    report = {'passed': False, 'baseline': str(baseline), 'url': url,
              'rasterMode': 'Chromium --disable-gpu, exact nonband canvas bytes',
              'errors': [], 'httpErrors': [], 'consoleErrors': [], 'owners': [], 'screenshots': []}
    try:
        with sync_playwright() as pw:
            executable = chromium_executable(None)
            browser = pw.chromium.launch(args=['--disable-gpu'], **({'executable_path': executable} if executable else {}))
            try:
                for owner in range(3):
                    context = browser.new_context(viewport={'width': 1920, 'height': 1080}, device_scale_factor=1, locale='ko-KR')
                    context.add_init_script('(' + INSTRUMENT + ')()')
                    page = context.new_page()
                    page.expose_function('bandQAProgress', lambda value: print(json.dumps({'fullNightProgress': value}), flush=True))
                    page.on('pageerror', lambda e: report['errors'].append(str(e)))
                    page.on('console', lambda m: report['consoleErrors'].append(m.text) if m.type == 'error' else None)
                    page.on('response', lambda r: report['httpErrors'].append({'url': r.url, 'status': r.status}) if r.status >= 400 else None)
                    try:
                        page.goto(url)
                        ready(page)
                        if owner == 0:
                            runtime = page.eval_on_selector('script[src$="/boot.js"]', 's=>new URL(".",s.src).pathname')
                            active = document_root / runtime.lstrip('/')
                            report['unchangedLogic'] = []
                            for name in UNCHANGED:
                                old = hashlib.sha256((baseline / name).read_bytes()).hexdigest()
                                new = hashlib.sha256((active / name).read_bytes()).hexdigest()
                                report['unchangedLogic'].append({'file': name, 'sha256': new, 'equal': old == new})
                            assert all(f['equal'] for f in report['unchangedLogic']), report['unchangedLogic']
                            report['atlases'] = page.evaluate(ATLAS_QA)
                            for atlas in report['atlases']:
                                expected_rows = 6 if atlas['id'] == 'freewill' else 5
                                assert atlas['width'] == 768 and atlas['height'] == expected_rows * 64, atlas
                                assert len(atlas['rows']) == expected_rows, atlas
                                for row in atlas['rows']:
                                    assert all(20 < f['opaque'] < 4096 for f in row['frames']), row
                                    assert min(row['distinctPerform'], row['distinctWalk'], row['distinctGreeting']) >= 2, row
                        report['owners'].append(run_owner(page, owner, output, report))
                        print(json.dumps({'ownerComplete': owner, 'trackVenueCases': len(report['owners']) * 5}), flush=True)
                        if owner == 2:
                            report['targetedClosings'] = page.evaluate(BEHAVIOR)
                            for night in report['targetedClosings']:
                                assert night['initialActors'] == 40 and night['initialCoreBand'] == ['singer', 'guitarist', 'bassist', 'drummer'], night
                                assert not night['violations'] and night['initialSaveEqual'], night
                                assert night['extraFloorSamples'] > 0 and night['greetingSamples'] > 0, night
                                assert night['phase'] == 'results' and night['workers'] == 5, night
                                assert night['cleanupComplete'] and night['cleaned'] == night['litter'], night
                                assert night['guestExitFinished'] and night['afterShowComplete'], night
                                assert not night['guestSpawned'] or night['guestComplete'], night
                                assert not night['finalRoster'] and not night['rosterAfterResultTrackSwitch'], night
                    finally:
                        context.close()
                assert not report['errors'], report['errors']
                assert not report['consoleErrors'], report['consoleErrors']
                assert not report['httpErrors'], report['httpErrors']
                report['passed'] = True
            finally:
                browser.close()
    except Exception as error:
        report['failure'] = str(error)
        raise
    finally:
        server.shutdown()
        server.server_close()
        thread.join()
        (output / 'browser-qa.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'passed': report['passed'], 'owners': len(report['owners']),
                      'trackVenueCases': sum(len(o['tracks']) for o in report['owners']),
                      'targetedClosings': len(report.get('targetedClosings', [])), 'output': str(output)}, ensure_ascii=False))


if __name__ == '__main__':
    main()
