"""Independent pixel identity and real RAF jump/contact evidence."""
from pathlib import Path
import base64, hashlib, json, re, time
from PIL import Image, ImageDraw
from playwright.sync_api import sync_playwright
from benchmark_web_loading import chromium_executable
from prepare_original_owners import SOURCE, SOURCE_SHA256, OWNERS

ROOT=Path(__file__).resolve().parent.parent
OUT=ROOT/'Research/PixelArtVerification/Final/details'
OUT.mkdir(parents=True,exist_ok=True)
ART=ROOT/'Tools/WebGLTemplate/FastGame/art'
report={'ownerChecks':[], 'jumpDraws':[], 'errors':[]}
package=ROOT/'Library/FinalNativeQA'
version=re.search(r'FastGame/([a-f0-9]{16})/boot\.js',(package/'index.html').read_text(encoding='utf-8')).group(1)
release=package/'FastGame'/version
manifest=json.loads((release/'runtime-manifest.json').read_text(encoding='utf-8'))
for file in manifest['files']:
    data=(release/file['path']).read_bytes()
    assert len(data)==file['bytes'] and hashlib.sha256(data).hexdigest()==file['sha256'],file['path']
    source=ROOT/'Tools/WebGLTemplate/FastGame'/file['path']
    assert source.read_bytes()==data,'Packaged/source mismatch: '+file['path']
report['release']=version
report['packageIntegrity']={'filesVerified':len(manifest['files']),'runtimeBytes':sum(f['bytes'] for f in manifest['files']),'matchesSource':True}
src=Image.open(SOURCE).convert('RGBA')
assert hashlib.sha256(SOURCE.read_bytes()).hexdigest()==SOURCE_SHA256
for owner in OWNERS:
    mask=Image.new('L',src.size);ImageDraw.Draw(mask).polygon(owner['clip'],fill=255)
    bounds=mask.getbbox();crop=src.crop(bounds)
    expected=Image.new('RGBA',owner['size'],src.getpixel((10,10)))
    expected.paste(crop,((expected.width-crop.width)//2,(expected.height-crop.height)//2),mask.crop(bounds))
    expected=expected.resize((400,350),Image.Resampling.NEAREST)
    actual=Image.open(ART/owner['file']).convert('RGBA')
    assert expected.size==actual.size and expected.tobytes()==actual.tobytes(),owner['name']
    report['ownerChecks'].append({'name':owner['name'],'size':actual.size,'exactOriginalNearestCrop':True})

afterdir=OUT.parent/'aftershow'
saved=json.loads((afterdir/'final-save.json').read_text(encoding='utf-8'))
actors=saved['native']['actors'] if 'native' in saved else saved.get('nativeState',{}).get('actors',[])
# Detect the actual serialized actor envelope without assuming its key name.
if not actors:
    actors=next((v['actors'] for v in saved.values() if isinstance(v,dict) and 'actors' in v),[])
qa=json.loads((afterdir/'aftershow-browser-qa.json').read_text(encoding='utf-8'))
report['fansAtReceipt']=[]
for fan in qa['final']['afterShow']['audience']:
    a=next((a for a in actors if a['id']==fan['id']),fan)
    d=((a['x']-fan['homeX'])**2+(a['z']-fan['homeZ'])**2)**.5
    report['fansAtReceipt'].append({'id':fan['id'],'phase':fan['phase'],'distanceFromCapturedHome':d})

for path in afterdir.glob('gesture-*-canvas.png'):
    info=json.loads(path.with_name(path.name.replace('-canvas.png','-frame.json')).read_text(encoding='utf-8'))
    mid=path.name.split('-')[1];a=next(a for a in info['actors'] if a['id']==mid)
    partner=next((p for p in info['actors'] if p['id']==a.get('_partner')),a)
    x=(a['canvasPoint']['x']+partner['canvasPoint']['x'])/2;y=(a['canvasPoint']['y']+partner['canvasPoint']['y'])/2
    Image.open(path).crop((int(x-70),int(y-75),int(x+70),int(y+25))).resize((560,400),Image.Resampling.NEAREST).save(OUT/path.name.replace('-canvas','-contact-4x'))

with sync_playwright() as pw:
    browser=pw.chromium.launch(headless=True,executable_path=chromium_executable(None))
    ctx=browser.new_context(viewport={'width':1586,'height':992},record_video_dir=str(OUT/'video'))
    page=ctx.new_page();page.on('pageerror',lambda e:report['errors'].append(str(e)))
    page.goto('http://127.0.0.1:9888/');page.wait_for_function('window.simbarGameReadyAt>0')
    page.evaluate("""() => {
      const app=simbarApplication,ctx=app.canvas.getContext('2d'),draw=ctx.drawImage.bind(ctx),render=app.world.render.bind(app.world);
      window.jumpQA={frames:{},draws:[],current:[],allAtlasFiles:new Set()};
      ctx.drawImage=(im,...args)=>{const name=im.src?.split('/').at(-1);if(name)jumpQA.allAtlasFiles.add(name);
        if(name==='v4-band-jump.png')jumpQA.current.push({name,args});return draw(im,...args);};
      app.world.render=(...args)=>{jumpQA.current=[];const v=render(...args);
        for(const d of jumpQA.current){const key=d.args[1]+'-'+d.args[0];if(!jumpQA.frames[key]){
          jumpQA.frames[key]=app.canvas.toDataURL('image/png').split(',')[1];jumpQA.draws.push({...d,key,now:performance.now(),elapsed:app.g.elapsed});}}
        return v;};
      const g=app.g;g.act('start');g.act('launch');while(g.phase==='guide')g.act('guide');
    }""")
    page.wait_for_timeout(12500)
    report['actualActorAppearance']=page.evaluate("""async () => {
      const src=[...document.scripts].find(s=>s.type==='module').src;
      const {pixelStyle,pixelFacing}=await import(new URL('world.js',src));
      const meta=await fetch(new URL('art/sprites.json',src)).then(r=>r.json());
      const c=simbarApplication.canvas,b=c.getBoundingClientRect();
      return simbarGame.actors.filter(a=>a.visible!==false&&['guest','seated'].includes(a.role)).map(a=>{
        const role=a.id.startsWith('bar-seat-')?'barseated':a.role,entry=meta.roles[role];
        const p=simbarApplication.world.worldToScreen(a.x,a.z,a.y||0);
        return {id:a.id,role,style:pixelStyle(a,entry,meta),facing:pixelFacing(a,role,a.action,{actors:simbarGame.actors}),canvasPoint:{x:p.x*c.width/b.width,y:p.y*c.height/b.height}};});
    }""")
    report['buttonAlignment']=page.evaluate("""() => [...document.querySelectorAll('.bottom-tabs button')].map(b=>{
      const r=b.getBoundingClientRect(),l=b.querySelector('.nav-label').getBoundingClientRect(),i=b.querySelector('img').getBoundingClientRect();
      const range=document.createRange();range.selectNodeContents(b.querySelector('.nav-label'));const t=range.getBoundingClientRect();
      return {action:b.dataset.action,label:b.querySelector('.nav-label').textContent,
        textCenterOffset:(t.left+t.right-r.left-r.right)/2,iconLeftOfText:i.right<=t.left,iconSource:b.querySelector('img').src};});""")
    data=page.evaluate('({frames:jumpQA.frames,draws:jumpQA.draws,allAtlasFiles:[...jumpQA.allAtlasFiles]})')
    report['jumpDraws']=data['draws'];report['actualLoadedDrawnFiles']=data['allAtlasFiles']
    for key,frame in data['frames'].items():
        path=OUT/f'actual-jump-{key}.png';path.write_bytes(base64.b64decode(frame))
        # Fixed venue region, derived from the rendered stage in the actual frame.
        im=Image.open(path);ox=(im.width-960)//2
        im.crop((ox+555,200,ox+950,405)).resize((1185,615),Image.Resampling.NEAREST).save(OUT/f'actual-jump-{key}-stage-3x.png')
    (OUT/'actual-live-canvas.png').write_bytes(base64.b64decode(page.evaluate('simbarApplication.canvas.toDataURL("image/png").split(",")[1]')))
    page.screenshot(path=str(OUT/'actual-live-after-jumps.png'))
    video=page.video;ctx.close();report['video']=str(video.path());browser.close()
report['actualJumpFramesCaptured']=len(report['jumpDraws'])
(OUT/'details.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k not in ['jumpDraws']},ensure_ascii=False))
