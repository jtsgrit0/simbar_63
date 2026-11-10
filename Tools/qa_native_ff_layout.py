"""Verify the FF renovation in the real native player and preserve other owners.

Run with ``uv run --with playwright python Tools/qa_native_ff_layout.py``.
An optional ``--package docs`` checks the packaged game. Baseline references
come from the retained pre-renovation release, not from mutable git HEAD.
"""
import argparse
import base64
from functools import partial
from http.server import ThreadingHTTPServer
import json
from pathlib import Path
import threading

from playwright.sync_api import sync_playwright

from benchmark_web_loading import chromium_executable
from qa_native_owner_venues import QuietHandler, INSTRUMENT, ready, proof, save_canvas

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / 'Temp/FFLayout-20261110/qa'
SNAPSHOT = ROOT / 'Temp/FFLayout-20261110/baseline/FastGame'
RETAINED = ROOT / 'docs/FastGame/18d7a736632536f7'


class Handler(QuietHandler):
    baseline = SNAPSHOT

    def translate_path(self, path):
        if path.startswith('/__ffbaseline__/'):
            relative = path.split('?', 1)[0].removeprefix('/__ffbaseline__/')
            target = (self.baseline / relative).resolve()
            if target.is_relative_to(self.baseline.resolve()):
                return str(target)
            return str(self.baseline / '__invalid__')
        return super().translate_path(path)


BEHAVIOR = r'''async () => {
  const boot=document.querySelector('script[src$="/boot.js"]').src;
  const [{SimBarGame:Before},{SimBarGame:After},oldNav,nav]=await Promise.all([
    import('/__ffbaseline__/core.js'),import(new URL('core.js',boot)),
    import('/__ffbaseline__/navigation.js'),import(new URL('navigation.js',boot))
  ]);
  const make=(Class,owner,seed=481251)=>{
    const values=new Map(),storage={getItem:k=>values.get(k)??null,setItem:(k,v)=>values.set(k,v)};
    const g=new Class({storage,seed});g.act('owner',{index:owner});
    g.act('start');g.act('deskSelect',{selected:true});g.act('deskWork',{task:0});
    g.act('deskWork',{task:1});g.act('launch');while(g.phase==='guide')g.act('guide');return g;
  };
  const output={otherOwners:[],navigationParity:{samples:0,differences:0}};
  for(const owner of [0,2]){
    const before=make(Before,owner),after=make(After,owner);
    for(let i=0;i<300;i++){before.tick(.1);after.tick(.1);}
    const a=JSON.parse(before.exportSave()),b=JSON.parse(after.exportSave());
    output.otherOwners.push({owner,elapsed:after.elapsed,actorCount:after.actors.length,
      equal:JSON.stringify(a)===JSON.stringify(b)});
  }
  for(let x=-10;x<=9;x+=.5)for(let z=-6;z<=7;z+=.5)for(const staff of [false,true])for(const floor of [false,true]){
    output.navigationParity.samples++;
    if(oldNav.walkable({x,z},staff,floor)!==nav.walkable({x,z},staff,floor))output.navigationParity.differences++;
  }
  const g=make(After,1);
  const meta=await fetch(new URL('art/venue-ff.json',boot)).then(r=>r.json());
  output.ff={actorCount:g.actors.length,seated:g.actors.filter(a=>a.role==='seated').map(a=>a.id),
    presentation:{layout:meta.layout,seatedIds:Object.keys(meta.seatedActors||{}),barRegion:meta.barRegion,targetedFurniture:meta.occluders.filter(a=>a.actors?.length).length,foregroundLayers:meta.occluders.length},
    formerSeats:g.actors.filter(a=>/^(lounge-|bar-seat-)/.test(a.id)).map(a=>({id:a.id,role:a.role,x:a.x,z:a.z})),
    staff:g.actors.filter(a=>['bartender','server'].includes(a.role)||a.id.startsWith('hired-')).map(a=>({id:a.id,x:a.x,z:a.z,layout:a.layout}))};
  output.ff.furniture=[{name:'counter',x:-7.5,z:-1},{name:'DJ booth',x:-.5,z:4.2}].map(p=>({...p,publicBlocked:!nav.walkable(p,false,false,'ff'),staffBlocked:!nav.walkable(p,true,false,'ff')}));
  const routes=[];
  for(const start of g.actors.filter(a=>['bartender','server'].includes(a.role))){
    for(const goal of [{x:2.2,z:-3.9},{x:2.2,z:.75},{x:8.7,z:-5.4}]){
      const route=nav.pathfind(start,goal,{staff:true,layout:'ff'});
      let previous=start;const clear=route.length>0&&route.every(p=>{const valid=nav.segment(previous,p,true,false,'ff');previous=p;return valid;});
      routes.push({start:start.id,goal,length:route.length,clear});
    }
  }
  output.ff.staffRoutes=routes;
  // Exercise real actor updates without changing the on-screen QA game.
  let movementChecks=0;const bad=[];
  for(let i=0;i<300;i++){
    const previous=new Map(g.actors.map(a=>[a.id,{x:a.x,z:a.z}]));g.tick(.1);
    for(const a of g.actors){
      if(!a.visible||a.role==='band'||!['bartender','server'].includes(a.role)&&!a.id.startsWith('hired-'))continue;
      const p=previous.get(a.id);movementChecks++;
      if(!nav.walkable(a,true,false,'ff')||p&&!nav.segment(p,a,true,false,'ff'))bad.push({id:a.id,x:a.x,z:a.z,t:g.elapsed});
    }
  }
  output.ff.movement={checks:movementChecks,violations:bad};
  const save=g.exportSave(),loadedStorage=new Map([['SimBar.Owner','ff'],['SimBar.Operations.v1',save]]);
  const loaded=new After({seed:481251,storage:{getItem:k=>loadedStorage.get(k)??null,setItem:(k,v)=>loadedStorage.set(k,v)}});
  output.ff.reload={owner:loaded.owner,seated:loaded.actors.filter(a=>a.role==='seated').map(a=>a.id),
    formerSeatCount:loaded.actors.filter(a=>/^(lounge-|bar-seat-)/.test(a.id)).length,
    staff:loaded.actors.filter(a=>['bartender','server'].includes(a.role)).map(a=>({id:a.id,x:a.x,z:a.z,layout:a.layout}))};
  const oldFF=make(Before,1);for(let i=0;i<30;i++)oldFF.tick(.1);
  const oldSave=oldFF.exportSave(),migrationValues=new Map([['SimBar.Owner','ff'],['SimBar.Operations.v1',oldSave]]);
  const migrated=new After({seed:42,storage:{getItem:k=>migrationValues.get(k)??null,setItem:(k,v)=>migrationValues.set(k,v)}});
  output.ff.migration={owner:migrated.owner,oldCash:oldFF.cash,newCash:migrated.cash,oldSeconds:oldFF.journey.secondsLeft,newSeconds:migrated.journey.secondsLeft,
    formerlySeated:oldFF.actors.filter(a=>a.role==='seated').length,seated:migrated.actors.filter(a=>a.role==='seated').length,
    standing:migrated.actors.filter(a=>/^(lounge-|bar-seat-)/.test(a.id)&&a.role==='guest').length,
    allLayout:migrated.actors.every(a=>a.layout==='ff'),staff:migrated.actors.filter(a=>['bartender','server'].includes(a.role)).map(a=>({id:a.id,x:a.x,z:a.z}))};
  migrated.act('owner',{index:2});
  output.ff.switchBack={owner:migrated.owner,seated:migrated.actors.filter(a=>a.role==='seated').length,hasFFActors:migrated.actors.some(a=>a.layout==='ff'),
    invalid:migrated.actors.filter(a=>a.visible&&a.role!=='band'&&a.role!=='seated'&&!nav.walkable(a,['bartender','server'].includes(a.role)||a.id.startsWith('hired-'),false)).map(a=>({id:a.id,x:a.x,z:a.z}))};
  const closing=make(After,1),queued=closing.actors.find(a=>a.id==='patron-0');queued.x=-6.1;queued.z=-1.65;
  closing.journey.secondsLeft=.05;closing.tick(.1);const closingSave=closing.exportSave();
  const closingValues=new Map([['SimBar.Owner','pet-sounds'],['SimBar.Operations.v1',closingSave]]);
  const normalClosing=new After({storage:{getItem:k=>closingValues.get(k)??null,setItem:(k,v)=>closingValues.set(k,v)}});
  normalClosing.act('continue');
  output.ff.closingSwitch={owner:normalClosing.owner,phase:normalClosing.phase,cashPreserved:normalClosing.cash===closing.cash,
    invalid:normalClosing.actors.filter(a=>a.visible&&a.role!=='band'&&a.role!=='seated'&&!nav.walkable(a,['bartender','server'].includes(a.role)||a.id.startsWith('hired-'),false)).map(a=>({id:a.id,x:a.x,z:a.z}))};
  const night=make(After,1,418251);for(let i=0;i<3;i++)night.act('hire',{index:i});
  let ticks=0;for(;ticks<10000&&night.phase!=='results';ticks++){
    night.tick(.1);if(night.reaction?.activeKind)night.act('quick',{kind:night.reaction.activeKind});
    if(night.farewellPending)night.act('farewellAck');
  }
  output.ff.fullNight={phase:night.phase,elapsed:night.elapsed,ticks,workers:night.crew.length,
    litter:night.floor.litter,cleaned:night.floor.cleaned,cleanupComplete:night.floor.cleanupComplete,
    guestComplete:night.floor.guestComplete,guestExitFinished:night.guestExitFinished,afterShowComplete:night.journey.afterShow?.completed};
  return output;
}'''


RENDER_PARITY = r'''async () => {
  const app=simbarApplication,owner=app.g.owner,boot=document.querySelector('script[src$="/boot.js"]').src,base=new URL('art/',boot);
  const [{createWorld:before},{createWorld:after},spriteMeta,venueMeta]=await Promise.all([
    import('/__ffbaseline__/world.js'),import(new URL('world.js',boot)),
    fetch(new URL('sprites.json',base)).then(r=>r.json()),fetch(new URL('venue.json',base)).then(r=>r.json())
  ]);
  const load=async file=>{const image=new Image();image.src=new URL(file,base);await image.decode();return image;};
  const [venue,seoul,freebird,ff]=await Promise.all(['venue.png','seoul.png','venue-freebird.png','venue-ff.png'].map(load));
  const atlases=Object.fromEntries(await Promise.all(Object.entries(spriteMeta.atlases).map(async([key,entry])=>[key,await load(entry.file)])));
  const assets=()=>({venue,seoul,spriteMeta,venueMeta,atlases,albumCovers:{},venues:{freebird,ff}});
  const canvas=()=>{const c=document.createElement('canvas');c.width=960;c.height=558;c.style.cssText='position:fixed;left:-10000px;top:0;width:960px;height:558px';document.body.append(c);return c;};
  const a=canvas(),b=canvas(),old=before(a,{assets:assets()}),current=after(b,{assets:assets()});
  const ma=document.createElement('canvas'),mb=document.createElement('canvas');ma.width=mb.width=320;ma.height=mb.height=186;
  const equal=(x,y)=>{const p=x.getContext('2d').getImageData(0,0,x.width,x.height).data,q=y.getContext('2d').getImageData(0,0,y.width,y.height).data;let differences=0,maxDelta=0;const pixels=new Set(),samples=[];
    for(let i=0;i<p.length;i++)if(p[i]!==q[i]){differences++;maxDelta=Math.max(maxDelta,Math.abs(p[i]-q[i]));pixels.add(i>>2);}
    for(const pixel of pixels){if(samples.length>=60)break;const i=pixel*4;samples.push({x:pixel%x.width,y:Math.floor(pixel/x.width),before:Array.from(p.slice(i,i+4)),after:Array.from(q.slice(i,i+4))});}
    return{equal:differences===0,differingChannels:differences,differentPixels:pixels.size,maxDelta,samples};};
  const traces=[[],[]];for(const [i,c]of [a,b].entries())for(const method of ['drawImage','translate','rotate','transform']){const ctx=c.getContext('2d'),original=ctx[method].bind(ctx);ctx[method]=(...args)=>{traces[i].push([method,...args.map(arg=>typeof arg==='object'?{src:arg.src||null,width:arg.width,height:arg.height}:arg)]);return original(...args);};}
  const results=[];
  try{
    for(const [label,actors] of [['empty',[]],['real-actors',structuredClone(app.g.actors)]]){
      const state={...app.g.state,owner,phase:'playing',paused:true,journey:structuredClone(app.g.journey),floor:structuredClone(app.g.floor),actors,orders:[],effects:[]};
      old.render(0,state);current.render(0,state);old.renderOverview(ma.getContext('2d'),state);current.renderOverview(mb.getContext('2d'),state);
      const main=equal(a,b),minimap=equal(ma,mb),result={owner,label,actorCount:actors.length,main,minimap};
      if(!main.equal){result.traceEqual=JSON.stringify(traces[0])===JSON.stringify(traces[1]);result.actors=actors;result.beforePNG=a.toDataURL();result.afterPNG=b.toDataURL();result.traceBefore=traces[0];result.traceAfter=traces[1];}
      results.push(result);
    }
    return results;
  }finally{old.destroy();current.destroy();a.remove();b.remove();}
}'''


def launch_night(page):
    page.locator('button[data-action="start"]').click()
    page.wait_for_function('simbarGame.phase==="planning"')
    page.locator('[data-desk-channel="monitor"]').click()
    page.locator('[data-desk-selected]').check()
    page.locator('.desk-computer-pane button[data-action="deskWork"]').click()
    page.locator('[data-desk-channel="phone"]').click()
    page.locator('.desk-mobile-pane button[data-action="deskWork"]').click()
    page.locator('button[data-action="launch"]').click()
    page.wait_for_function('["guide","playing"].includes(simbarGame.phase)')
    while page.evaluate('simbarGame.phase') == 'guide':
        page.locator('button[data-action="guide"]').click()
        page.wait_for_timeout(50)
    page.wait_for_function('simbarGame.phase==="playing" && simbarGame.actors.length>0')
    page.keyboard.press('p')
    page.wait_for_function('simbarGame.paused')
    page.add_style_tag(content='.pause-ref{display:none!important}.screen-shade:has(.pause-ref){display:none!important}')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--package', type=Path)
    parser.add_argument('--baseline', type=Path)
    parser.add_argument('--output', type=Path, default=OUT)
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    baseline = args.baseline or (SNAPSHOT if SNAPSHOT.is_dir() else RETAINED)
    if not (baseline / 'world.js').is_file():
        raise FileNotFoundError('A retained pre-renovation FastGame baseline is required.')
    Handler.baseline = baseline.resolve()
    Handler.source_html = None
    if not args.package:
        html = (ROOT / 'Tools/WebGLTemplate/FastGame/index.html').read_text(encoding='utf-8')
        Handler.source_html = html.replace('__SIMBAR_ASSET_HINTS__', '').replace('__SIMBAR_STYLESHEET__', '/Tools/WebGLTemplate/style.css').replace('__SIMBAR_RUNTIME__', '/Tools/WebGLTemplate/FastGame').replace('href="TemplateData/favicon.ico"', 'href="data:,"')
    document_root = args.package.resolve() if args.package else ROOT
    server = ThreadingHTTPServer(('127.0.0.1', 0), partial(Handler, directory=str(document_root)))
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    url = f'http://127.0.0.1:{server.server_port}/' + ('' if args.package else '__owner-venues-qa__/index.html')
    report = {'passed': False, 'baseline': str(baseline), 'url': url, 'rasterMode': 'Chromium --disable-gpu for deterministic byte comparisons', 'errors': [], 'httpErrors': [], 'consoleErrors': [], 'owners': [], 'screenshots': []}
    try:
        with sync_playwright() as pw:
            executable = chromium_executable(None)
            # Accelerated Canvas 2D occasionally differs by 1–2 channel values
            # with identical draw calls. CPU rasterization keeps strict equality
            # reproducible; no color tolerance or ignored pixels are used.
            browser = pw.chromium.launch(args=['--disable-gpu'], **({'executable_path': executable} if executable else {}))
            try:
                for owner, label in [(1, 'edison-ff-renovated'), (0, 'jessy-preserved'), (2, 'jaiden-preserved')]:
                    context = browser.new_context(viewport={'width': 1920, 'height': 1080}, device_scale_factor=1, locale='ko-KR')
                    context.add_init_script('(' + INSTRUMENT + ')()')
                    page = context.new_page()
                    page.on('pageerror', lambda e: report['errors'].append(str(e)))
                    page.on('console', lambda m: report['consoleErrors'].append(m.text) if m.type == 'error' else None)
                    page.on('response', lambda r: report['httpErrors'].append({'url': r.url, 'status': r.status}) if r.status >= 400 else None)
                    try:
                        page.goto(url)
                        ready(page)
                        page.locator('.owner-card').nth(owner).click()
                        proof(page, owner)
                        page.reload()
                        ready(page)
                        assert page.evaluate('simbarGame.owner') == owner
                        launch_night(page)
                        pixels = proof(page, owner)
                        actors = page.evaluate('simbarGame.actors.filter(a=>a.visible).map(a=>({id:a.id,role:a.role,x:a.x,z:a.z,layout:a.layout}))')
                        item = {'owner': owner, 'pixels': pixels, 'actors': actors}
                        if owner == 1:
                            page.screenshot(path=str(output / (label + '.png')))
                            save_canvas(page, '#unity-canvas', output / (label + '-world.png'))
                            assert not [a for a in actors if a['role'] == 'seated'], actors
                            assert len([a for a in actors if a['id'].startswith(('lounge-', 'bar-seat-'))]) == 11, actors
                            report['behavior'] = page.evaluate(BEHAVIOR)
                            behavior = report['behavior']
                            assert all(a['equal'] for a in behavior['otherOwners']), behavior['otherOwners']
                            assert behavior['navigationParity']['differences'] == 0, behavior['navigationParity']
                            assert not behavior['ff']['seated'], behavior['ff']
                            presentation = behavior['ff']['presentation']
                            assert presentation['layout']['id'] == 'ff' and presentation['layout']['version'] == 3, presentation
                            assert presentation['layout']['counter'] == [-8.15, -3, -6.95, 1.3] and presentation['layout']['dj'] == [-1.85, 2.25, 1.75, 4.35], presentation
                            assert not presentation['seatedIds'] and presentation['barRegion'] is None and not presentation['targetedFurniture'], presentation
                            assert len(behavior['ff']['formerSeats']) == 11 and all(a['role'] == 'guest' for a in behavior['ff']['formerSeats']), behavior['ff']['formerSeats']
                            assert all(a['publicBlocked'] and a['staffBlocked'] for a in behavior['ff']['furniture']), behavior['ff']['furniture']
                            assert all(a['clear'] for a in behavior['ff']['staffRoutes']), behavior['ff']['staffRoutes']
                            assert not behavior['ff']['movement']['violations'], behavior['ff']['movement']
                            assert behavior['ff']['reload']['owner'] == 1 and not behavior['ff']['reload']['seated'] and behavior['ff']['reload']['formerSeatCount'] == 11, behavior['ff']['reload']
                            migration = behavior['ff']['migration']
                            assert migration['oldCash'] == migration['newCash'] and migration['oldSeconds'] == migration['newSeconds'], migration
                            assert migration['formerlySeated'] == 11 and migration['seated'] == 0 and migration['standing'] == 11 and migration['allLayout'], migration
                            assert behavior['ff']['switchBack'] == {'owner': 2, 'seated': 11, 'hasFFActors': False, 'invalid': []}, behavior['ff']['switchBack']
                            assert behavior['ff']['closingSwitch'] == {'owner': 2, 'phase': 'closing', 'cashPreserved': True, 'invalid': []}, behavior['ff']['closingSwitch']
                            night = behavior['ff']['fullNight']
                            assert night['phase'] == 'results' and night['workers'] == 5 and night['cleanupComplete'] and night['cleaned'] == night['litter'], night
                            assert night['guestComplete'] and night['guestExitFinished'] and night['afterShowComplete'], night
                        else:
                            item['baseline'] = page.evaluate(RENDER_PARITY)
                            for result in item['baseline']:
                                for key in ['beforePNG', 'afterPNG']:
                                    encoded = result.pop(key, None)
                                    if encoded:
                                        (output / (label + '-' + result['label'] + '-' + key + '.png')).write_bytes(base64.b64decode(encoded.split(',')[1]))
                            assert all(a['main']['equal'] and a['minimap']['equal'] for a in item['baseline']), item['baseline']
                        for suffix, selector in [('-world', '#unity-canvas'), ('-minimap', '#mini-map')]:
                            target = output / (label + suffix + '.png')
                            save_canvas(page, selector, target)
                            report['screenshots'].append(target.name)
                        page.screenshot(path=str(output / (label + '.png')))
                        report['screenshots'].append(label + '.png')
                        report['owners'].append(item)
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
    print(json.dumps({'passed': report['passed'], 'owners': len(report['owners']), 'output': str(output)}, ensure_ascii=False))


if __name__ == '__main__':
    main()
