"""Exercise FF's standing tables in the actual native player.

Use --package docs for a prepared release. Baseline is the preceding immutable
release, and CPU canvas rasterization keeps byte comparisons deterministic.
"""
import argparse
from functools import partial
from http.server import ThreadingHTTPServer
import json
from pathlib import Path
import threading

from playwright.sync_api import sync_playwright

from benchmark_web_loading import chromium_executable
from qa_native_owner_venues import QuietHandler, INSTRUMENT, ready, proof, save_canvas
from qa_native_freebird_layout import launch_night

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / 'Temp/FFTables-20261110/qa'
SNAPSHOT = ROOT / 'Temp/FFTables-20261110/baseline/FastGame'
RETAINED = ROOT / 'docs/FastGame/e9e06549a3abf519'


class Handler(QuietHandler):
    baseline = SNAPSHOT

    def translate_path(self, path):
        if path.startswith('/__fftablebaseline__/'):
            relative = path.split('?', 1)[0].removeprefix('/__fftablebaseline__/')
            target = (self.baseline / relative).resolve()
            if target.is_relative_to(self.baseline.resolve()):
                return str(target)
            return str(self.baseline / '__invalid__')
        return super().translate_path(path)


BEHAVIOR = r'''async () => {
  const boot=document.querySelector('script[src$="/boot.js"]').src;
  const [{SimBarGame:Before},{SimBarGame:After},oldNav,nav]=await Promise.all([
    import('/__fftablebaseline__/core.js'),import(new URL('core.js',boot)),
    import('/__fftablebaseline__/navigation.js'),import(new URL('navigation.js',boot))
  ]);
  const make=(Class,owner,seed=481251)=>{
    const values=new Map(),storage={getItem:k=>values.get(k)??null,setItem:(k,v)=>values.set(k,v)};
    const g=new Class({storage,seed});g.act('owner',{index:owner});
    g.act('start');g.act('deskSelect',{selected:true});g.act('deskWork',{task:0});
    g.act('deskWork',{task:1});g.act('launch');while(g.phase==='guide')g.act('guide');return g;
  };
  const output={otherOwners:[],navigationParity:{samples:0,differences:0}};
  output.preservedArt=[];
  for(const file of ['venue-freebird.png','venue-freebird.json','venue.png','venue.json']){
    const [a,b]=await Promise.all([fetch('/__fftablebaseline__/art/'+file).then(r=>r.arrayBuffer()),fetch(new URL('art/'+file,boot)).then(r=>r.arrayBuffer())]);
    const x=new Uint8Array(a),y=new Uint8Array(b);output.preservedArt.push({file,equal:x.length===y.length&&x.every((v,i)=>v===y[i])});
  }
  for(const owner of [0,2]){
    const before=make(Before,owner),after=make(After,owner);
    for(let i=0;i<300;i++){before.tick(.1);after.tick(.1);}
    output.otherOwners.push({owner,elapsed:after.elapsed,actorCount:after.actors.length,equal:before.exportSave()===after.exportSave()});
  }
  for(const layout of ['freebird',undefined])for(let x=-10;x<=9;x+=.5)for(let z=-6;z<=7;z+=.5)for(const staff of [false,true])for(const floor of [false,true]){
    output.navigationParity.samples++;
    if(oldNav.walkable({x,z},staff,floor,layout)!==nav.walkable({x,z},staff,floor,layout))output.navigationParity.differences++;
  }
  const g=make(After,1),profile=nav.FF_LAYOUT;
  const meta=await fetch(new URL('art/venue-ff.json',boot)).then(r=>r.json());
  const occupied=g.actors.filter(a=>a.role==='seated');
  output.ff={profile,actorCount:g.actors.length,occupied:occupied.map(a=>({id:a.id,x:a.x,z:a.z,homeX:a.homeX,homeZ:a.homeZ})),
    presentation:{layout:meta.layout,seatedActors:meta.seatedActors,props:meta.props,staticLayers:meta.occluders.filter(a=>!a.actors?.length).length,
      targetedFurniture:meta.occluders.filter(a=>a.actors?.length).length,ledQuad:meta.ledQuad,stageDeck:meta.stageDeck}};
  const solids=[...(profile.tables||[]).map((a,i)=>({...a,id:'table-'+i})),...(profile.stools||[]).map((a,i)=>({...a,id:'stool-'+i}))];
  output.ff.furniture=solids.map(p=>({...p,publicBlocked:!nav.walkable(p,false,false,'ff'),staffBlocked:!nav.walkable(p,true,false,'ff')}));
  output.ff.stageFront={samples:0,differences:0};
  for(let x=2;x<=9;x+=.4)for(let z=-5.4;z<=1.1;z+=.4){
    output.ff.stageFront.samples++;
    if(oldNav.walkable({x,z},false,false,'ff')!==nav.walkable({x,z},false,false,'ff'))output.ff.stageFront.differences++;
  }
  output.ff.staffRoutes=[];
  for(const start of g.actors.filter(a=>['bartender','server'].includes(a.role))){
    for(const goal of [{x:2.2,z:-3.9},{x:2.2,z:.75},{x:8.7,z:-5.4}]){
      const route=nav.pathfind(start,goal,{staff:true,layout:'ff'});let previous=start;
      const clear=route.length>0&&route.every(p=>{const valid=nav.segment(previous,p,true,false,'ff');previous=p;return valid;});
      output.ff.staffRoutes.push({start:start.id,goal,length:route.length,clear});
    }
  }
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
  const invalid=(game,layout)=>game.actors.filter(a=>a.visible&&a.role!=='band'&&a.role!=='seated'&&!nav.walkable(a,['bartender','server'].includes(a.role)||a.id.startsWith('hired-'),false,layout)).map(a=>({id:a.id,x:a.x,z:a.z}));
  const loadedValues=new Map([['SimBar.Owner','ff'],['SimBar.Operations.v1',g.exportSave()]]);
  const loaded=new After({storage:{getItem:k=>loadedValues.get(k)??null,setItem:(k,v)=>loadedValues.set(k,v)}});
  output.ff.reload={cashPreserved:g.cash===loaded.cash,clockPreserved:g.journey.secondsLeft===loaded.journey.secondsLeft,
    seated:loaded.actors.filter(a=>a.role==='seated').length,invalid:invalid(loaded,'ff')};
  const old=make(Before,1);for(let i=0;i<300;i++)old.tick(.1);
  const oldValues=new Map([['SimBar.Owner','ff'],['SimBar.Operations.v1',old.exportSave()]]);
  const migrated=new After({storage:{getItem:k=>oldValues.get(k)??null,setItem:(k,v)=>oldValues.set(k,v)}});
  output.ff.migration={cashPreserved:old.cash===migrated.cash,clockPreserved:old.journey.secondsLeft===migrated.journey.secondsLeft,
    oldSeated:old.actors.filter(a=>a.role==='seated').length,seated:migrated.actors.filter(a=>a.role==='seated').length,
    versions:[...new Set(migrated.actors.map(a=>a.layoutVersion))],invalid:invalid(migrated,'ff')};
  output.ff.boundaries=[];
  for(const owner of [0,2,1]){
    const cash=migrated.cash,clock=migrated.journey.secondsLeft;migrated.act('owner',{index:owner});
    output.ff.boundaries.push({owner:migrated.owner,cashPreserved:cash===migrated.cash,clockPreserved:clock===migrated.journey.secondsLeft,
      seated:migrated.actors.filter(a=>a.role==='seated').length,invalid:invalid(migrated,owner===0?'freebird':owner===1?'ff':undefined)});
  }
  output.ff.closingSwitch=[];
  for(const [owner,id,layout] of [[0,'female','freebird'],[2,'pet-sounds',undefined]]){
    const closing=make(After,1),a=closing.actors.find(a=>a.id==='patron-0');a.x=-6.1;a.z=-1.65;
    closing.journey.secondsLeft=.05;closing.tick(.1);
    const values=new Map([['SimBar.Owner',id],['SimBar.Operations.v1',closing.exportSave()]]);
    const restored=new After({storage:{getItem:k=>values.get(k)??null,setItem:(k,v)=>values.set(k,v)}});restored.act('continue');
    output.ff.closingSwitch.push({owner:restored.owner,phase:restored.phase,cashPreserved:restored.cash===closing.cash,invalid:invalid(restored,layout)});
  }
  const night=make(After,1,418251);for(let i=0;i<3;i++)night.act('hire',{index:i});
  let ticks=0;for(;ticks<10000&&night.phase!=='results';ticks++){
    night.tick(.1);if(night.reaction?.activeKind)night.act('quick',{kind:night.reaction.activeKind});if(night.farewellPending)night.act('farewellAck');
  }
  output.ff.fullNight={phase:night.phase,elapsed:night.elapsed,ticks,workers:night.crew.length,litter:night.floor.litter,cleaned:night.floor.cleaned,
    cleanupComplete:night.floor.cleanupComplete,guestSpawned:night.floor.guestSpawned,guestComplete:night.floor.guestComplete,
    guestExitFinished:night.guestExitFinished,afterShowComplete:night.journey.afterShow?.completed};
  return output;
}'''


RENDER_PARITY = r'''async () => {
  const app=simbarApplication,owner=app.g.owner,boot=document.querySelector('script[src$="/boot.js"]').src,base=new URL('art/',boot);
  const [{createWorld:before},{createWorld:after},spriteMeta,venueMeta,ffMeta,freebirdMeta]=await Promise.all([
    import('/__fftablebaseline__/world.js'),import(new URL('world.js',boot)),fetch(new URL('sprites.json',base)).then(r=>r.json()),
    fetch(new URL('venue.json',base)).then(r=>r.json()),fetch(new URL('venue-ff.json',base)).then(r=>r.json()),fetch(new URL('venue-freebird.json',base)).then(r=>r.json())
  ]);
  const load=async file=>{const i=new Image();i.src=new URL(file,base);await i.decode();return i;};
  const [venue,seoul,freebird,ff]=await Promise.all(['venue.png','seoul.png','venue-freebird.png','venue-ff.png'].map(load));
  const atlases=Object.fromEntries(await Promise.all(Object.entries(spriteMeta.atlases).map(async([k,v])=>[k,await load(v.file)])));
  const propImages={'standing-table':await load('standing-table.png'),'standing-stool':await load('standing-stool.png')};
  const assets=()=>({venue,seoul,spriteMeta,venueMeta,atlases,propImages,albumCovers:{},venues:{freebird,ff},venueMetas:{ff:ffMeta,freebird:freebirdMeta}});
  const canvas=()=>{const c=document.createElement('canvas');c.width=960;c.height=558;c.style.cssText='position:fixed;left:-10000px;top:0;width:960px;height:558px';document.body.append(c);return c;};
  const a=canvas(),b=canvas(),old=before(a,{assets:assets()}),current=after(b,{assets:assets()});
  const ma=document.createElement('canvas'),mb=document.createElement('canvas');ma.width=mb.width=320;ma.height=mb.height=186;
  const equal=(x,y)=>{const p=x.getContext('2d').getImageData(0,0,x.width,x.height).data,q=y.getContext('2d').getImageData(0,0,y.width,y.height).data;let differences=0;for(let i=0;i<p.length;i++)if(p[i]!==q[i])differences++;return{equal:differences===0,differingChannels:differences};};
  const results=[];
  try{
    for(const [label,actors]of[['empty',[]],['real-actors',structuredClone(app.g.actors)]]){
      const state={...app.g.state,owner,phase:'playing',paused:true,journey:structuredClone(app.g.journey),floor:structuredClone(app.g.floor),actors,orders:[],effects:[]};
      old.render(0,state);current.render(0,state);old.renderOverview(ma.getContext('2d'),state);current.renderOverview(mb.getContext('2d'),state);
      results.push({owner,label,actorCount:actors.length,main:equal(a,b),minimap:equal(ma,mb)});
    }
    return results;
  }finally{old.destroy();current.destroy();a.remove();b.remove();}
}'''


def check_behavior(report):
    assert all(a['equal'] for a in report['preservedArt']), report['preservedArt']
    assert all(a['equal'] for a in report['otherOwners']), report['otherOwners']
    assert report['navigationParity']['differences'] == 0, report['navigationParity']
    ff = report['ff']
    profile, presentation = ff['profile'], ff['presentation']
    assert len(profile['tables']) == 5 and len(profile['stools']) == 10, profile
    assert ff['actorCount'] == 40 and len(ff['occupied']) == 0, ff['occupied']
    assert not presentation['seatedActors'], presentation
    assert len(presentation['props']) == 15, presentation
    assert sum(a['asset'] == 'standing-table' for a in presentation['props']) == 5, presentation
    assert sum(a['asset'] == 'standing-stool' for a in presentation['props']) == 10, presentation
    positions = lambda values: sorted((round(a['x'], 6), round(a['z'], 6)) for a in values)
    assert positions(a for a in presentation['props'] if a['asset'] == 'standing-table') == positions(profile['tables']), presentation
    assert positions(a for a in presentation['props'] if a['asset'] == 'standing-stool') == positions(profile['stools']), presentation
    assert all(a['drawWidth'] > 0 and a['drawHeight'] > 0 for a in presentation['props']), presentation
    assert presentation['layout']['version'] == profile['version'], presentation
    assert all(p['x'] + p['radius'] < 2 for p in ff['furniture']), ff['furniture']
    assert all(p['publicBlocked'] and p['staffBlocked'] for p in ff['furniture']), ff['furniture']
    assert ff['stageFront']['differences'] == 0, ff['stageFront']
    assert all(a['clear'] for a in ff['staffRoutes']), ff['staffRoutes']
    assert ff['movement']['checks'] >= 600 and not ff['movement']['violations'], ff['movement']
    assert ff['reload']['cashPreserved'] and ff['reload']['clockPreserved'] and ff['reload']['seated'] == 0 and not ff['reload']['invalid'], ff['reload']
    migration = ff['migration']
    assert migration['cashPreserved'] and migration['clockPreserved'] and migration['oldSeated'] == 0 and migration['seated'] == 0, migration
    assert migration['versions'] == [profile['version']] and not migration['invalid'], migration
    assert [a['owner'] for a in ff['boundaries']] == [0, 2, 1], ff['boundaries']
    assert [a['seated'] for a in ff['boundaries']] == [0, 11, 0], ff['boundaries']
    assert all(a['cashPreserved'] and a['clockPreserved'] and not a['invalid'] for a in ff['boundaries']), ff['boundaries']
    assert all(a['phase'] == 'closing' and a['cashPreserved'] and not a['invalid'] for a in ff['closingSwitch']), ff['closingSwitch']
    night = ff['fullNight']
    assert night['phase'] == 'results' and night['workers'] == 5 and night['cleanupComplete'] and night['cleaned'] == night['litter'], night
    assert night['guestExitFinished'] and night['afterShowComplete'], night
    if night['guestSpawned']:
        assert night['guestComplete'], night


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
        raise FileNotFoundError('The pre-table release baseline is required.')
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
    report = {'passed': False, 'url': url, 'baseline': str(baseline), 'rasterMode': 'CPU', 'errors': [], 'consoleErrors': [], 'httpErrors': [], 'owners': [], 'screenshots': []}
    try:
        with sync_playwright() as pw:
            executable = chromium_executable(None)
            browser = pw.chromium.launch(args=['--disable-gpu'], **({'executable_path': executable} if executable else {}))
            try:
                for owner, label in [(1, 'edison-ff-tables'), (0, 'jessy-preserved'), (2, 'jaiden-preserved')]:
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
                        item = {'owner': owner, 'pixels': proof(page, owner)}
                        page.screenshot(path=str(output / (label + '.png')))
                        save_canvas(page, '#unity-canvas', output / (label + '-world.png'))
                        save_canvas(page, '#mini-map', output / (label + '-minimap.png'))
                        report['screenshots'].extend([label + '.png', label + '-world.png', label + '-minimap.png'])
                        if owner == 1:
                            page.evaluate('''() => {simbarGame.act('resume');for(let i=0;i<180;i++)simbarGame.tick(.1);simbarGame.act('pause',{reason:'qa'});}''')
                            page.wait_for_timeout(120)
                            page.screenshot(path=str(output / (label + '-service.png')))
                            save_canvas(page, '#unity-canvas', output / (label + '-service-world.png'))
                            page.evaluate('''() => {window.qaNormalRender=simbarApplication.world.render;simbarApplication.world.render=(timestamp,state)=>window.qaNormalRender(timestamp,{...state,orders:[]});}''')
                            page.wait_for_timeout(120)
                            page.screenshot(path=str(output / (label + '-service-clear.png')))
                            save_canvas(page, '#unity-canvas', output / (label + '-service-clear-world.png'))
                            page.evaluate('''() => {simbarApplication.world.render=window.qaNormalRender;delete window.qaNormalRender;}''')
                            report['screenshots'].extend([label + '-service.png', label + '-service-clear.png'])
                            report['behavior'] = page.evaluate(BEHAVIOR)
                            check_behavior(report['behavior'])
                        else:
                            item['baseline'] = page.evaluate(RENDER_PARITY)
                            assert all(p['main']['equal'] and p['minimap']['equal'] for p in item['baseline']), item['baseline']
                        report['owners'].append(item)
                    finally:
                        context.close()
                assert not report['errors'] and not report['consoleErrors'] and not report['httpErrors'], report
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
