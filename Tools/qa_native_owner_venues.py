"""Exercise the real owner selector and compare rendered venue pixels.

Run ``uv run --with playwright python Tools/qa_native_owner_venues.py`` for the
working source, or append ``--package Temp/NativeVenuePreview`` for a package.
Every run uses fresh, disposable browser contexts and never the user's profile.
"""

import argparse
import base64
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
import json
from pathlib import Path
import subprocess
import threading

from playwright.sync_api import sync_playwright

from benchmark_web_loading import chromium_executable


ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / 'Temp/VenueInteriors-20261110/qa'
FILES = ['venue-freebird.png', 'venue-ff.png', 'venue.png']
NAMES = ['jessy-freebird', 'edison-ff', 'jaiden-original']
# Pin the pre-interior release so this comparison stays meaningful after commit.
BASELINE_REF = '12b9acf3aaa811021c23448aa137e13afb263ebb'


class QuietHandler(SimpleHTTPRequestHandler):
    source_html = None

    def log_message(self, *_args):
        pass

    def do_GET(self):
        route = self.path.split('?', 1)[0]
        payload = None
        content_type = 'text/javascript; charset=utf-8'
        if route == '/__owner-venues-qa__/world-before.js':
            payload = (OUT / 'world-before.js').read_bytes()
        elif route == '/__owner-venues-qa__/index.html' and self.source_html:
            payload = self.source_html.encode('utf-8')
            content_type = 'text/html; charset=utf-8'
        if payload is not None:
            self.send_response(200)
            self.send_header('Content-Type', content_type)
            self.send_header('Content-Length', str(len(payload)))
            self.end_headers()
            self.wfile.write(payload)
        else:
            super().do_GET()


# Record the actual draw source and real state passed to both rendering paths.
# Pixel comparisons below independently verify the completed canvas buffers.
INSTRUMENT = r'''() => {
  const original=CanvasRenderingContext2D.prototype.drawImage;
  window.venueDraws={};
  CanvasRenderingContext2D.prototype.drawImage=function(image,...args){
    if(['unity-canvas','mini-map'].includes(this.canvas.id)&&image?.src&&/\/venue(?:-freebird|-ff)?\.png(?:\?|$)/.test(image.src)){
      window.venueDraws[this.canvas.id]={src:image.src,args};
    }
    return original.call(this,image,...args);
  };
}'''


PIXELS = r'''async expectedFile => {
  const app=window.simbarApplication;
  const boot=document.querySelector('script[src$="/boot.js"]').src;
  const base=new URL('art/',boot);
  const load=async file=>{const i=new Image();i.src=new URL(file,base);await i.decode();return i;};
  const images=await Promise.all(['venue-freebird.png','venue-ff.png','venue.png'].map(load));
  const selected=['venue-freebird.png','venue-ff.png','venue.png'].indexOf(expectedFile);
  const close=(a,b,index)=>Math.abs(a[index]-b[index])+Math.abs(a[index+1]-b[index+1])+Math.abs(a[index+2]-b[index+2])<=6;
  const result={owner:app.g.owner,phase:app.g.phase,draws:structuredClone(window.venueDraws),state:structuredClone(window.venueStates)};
  for(const [key,canvas] of [['main',app.canvas],['minimap',document.querySelector('#mini-map')]]){
    if(!canvas)throw new Error('Missing '+key+' canvas');
    const w=canvas.width,h=canvas.height,actual=canvas.getContext('2d').getImageData(0,0,w,h).data;
    const references=images.map(image=>{
      const c=document.createElement('canvas');c.width=w;c.height=h;const ctx=c.getContext('2d');ctx.imageSmoothingEnabled=false;
      if(key==='main')ctx.drawImage(image,Math.round((w-960)/2),0,960,558);
      else{ctx.fillStyle='#1e2632';ctx.fillRect(0,0,w,h);const s=Math.min((w-8)/960,(h-8)/558);ctx.drawImage(image,Math.round((w-960*s)/2),Math.round((h-558*s)/2),Math.round(960*s),Math.round(558*s));}
      return ctx.getImageData(0,0,w,h).data;
    });
    const expected=references[selected];let eligible=0,matched=0,distinctive=0,distinctiveMatched=0;
    for(let y=8;y<h-8;y+=2)for(let x=8;x<w-8;x+=2){
      const i=(y*w+x)*4;if(expected[i+3]<250)continue;
      eligible++;const same=close(actual,expected,i);if(same)matched++;
      if(references.every((other,j)=>j===selected||!close(expected,other,i))){distinctive++;if(same)distinctiveMatched++;}
    }
    result[key]={width:w,height:h,eligible,matched,fraction:matched/eligible,distinctive,distinctiveMatched,distinctiveFraction:distinctiveMatched/distinctive};
  }
  return result;
}'''


BASELINE = r'''async () => {
  const app=window.simbarApplication,boot=document.querySelector('script[src$="/boot.js"]').src,base=new URL('art/',boot);
  const [{createWorld:before},{createWorld:after},spriteMeta,venueMeta]=await Promise.all([
    import('/__owner-venues-qa__/world-before.js'),import(new URL('world.js',boot)),
    fetch(new URL('sprites.json',base)).then(r=>r.json()),fetch(new URL('venue.json',base)).then(r=>r.json())
  ]);
  const load=async file=>{const i=new Image();i.src=new URL(file,base);await i.decode();return i;};
  const [venue,seoul,freebird,ff]=await Promise.all(['venue.png','seoul.png','venue-freebird.png','venue-ff.png'].map(load));
  const atlases=Object.fromEntries(await Promise.all(Object.entries(spriteMeta.atlases).map(async([k,v])=>[k,await load(v.file)])));
  const assets=()=>({venue,seoul,spriteMeta,venueMeta,atlases,albumCovers:{},venues:{freebird,ff}});
  const canvas=()=>{const c=document.createElement('canvas');c.width=960;c.height=558;c.style.cssText='position:fixed;left:-10000px;top:0;width:960px;height:558px';document.body.append(c);return c;};
  const a=canvas(),b=canvas(),old=before(a,{assets:assets()}),current=after(b,{assets:assets()}),output=[];
  const map=()=>{const c=document.createElement('canvas');c.width=320;c.height=186;return c;};
  const ma=map(),mb=map();
  const equal=(x,y)=>{const p=x.getContext('2d').getImageData(0,0,x.width,x.height).data,q=y.getContext('2d').getImageData(0,0,y.width,y.height).data;let differences=0;for(let i=0;i<p.length;i++)if(p[i]!==q[i])differences++;return{equal:differences===0,differingChannels:differences,channels:p.length};};
  try{
    for(const [label,actors] of [['empty',[]],['frozen-real-actors',structuredClone(app.g.actors)]]){
      const state={...app.g.state,owner:2,phase:'playing',paused:true,journey:structuredClone(app.g.journey),floor:structuredClone(app.g.floor),actors,orders:[],effects:[]};
      old.render(0,state);current.render(0,state);old.renderOverview(ma.getContext('2d'),state);current.renderOverview(mb.getContext('2d'),state);
      output.push({label,actorCount:actors.length,main:equal(a,b),minimap:equal(ma,mb)});
    }
    return output;
  }finally{old.destroy();current.destroy();a.remove();b.remove();}
}'''


def save_canvas(page, selector, target):
    encoded = page.eval_on_selector(selector, 'c=>c.toDataURL("image/png").split(",")[1]')
    target.write_bytes(base64.b64decode(encoded))


def ready(page):
    page.wait_for_function('window.simbarGameReadyAt>0 && window.simbarApplication && document.querySelectorAll(".owner-card").length===3')
    page.wait_for_function('document.fonts.status==="loaded"')
    page.evaluate('''() => {
      window.venueStates={};const w=simbarApplication.world;
      for(const method of ['render','renderOverview']){
        const original=w[method];w[method]=function(first,state){window.venueStates[method]={owner:state?.owner,phase:state?.phase};return original.call(this,first,state);};
      }
    }''')


def proof(page, owner):
    page.wait_for_function('(i)=>simbarGame.owner===i && window.venueStates?.render?.owner===i && window.venueStates?.renderOverview?.owner===i', arg=owner)
    result = page.evaluate(PIXELS, FILES[owner])
    for canvas_id in ['unity-canvas', 'mini-map']:
        assert result['draws'][canvas_id]['src'].split('/')[-1] == FILES[owner], result
    for method in ['render', 'renderOverview']:
        assert result['state'][method]['owner'] == owner, result
    for key in ['main', 'minimap']:
        assert result[key]['eligible'] > 1000, result
        assert result[key]['fraction'] >= .60, result
        assert result[key]['distinctive'] > 50, result
        assert result[key]['distinctiveFraction'] >= .55, result
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--package', type=Path, help='Serve an already prepared native package; otherwise test actual source.')
    parser.add_argument('--output', type=Path, default=OUT)
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    OUT.mkdir(parents=True, exist_ok=True)
    before = subprocess.check_output(['git', 'show', BASELINE_REF + ':Tools/WebGLTemplate/FastGame/world.js'], cwd=ROOT)
    (OUT / 'world-before.js').write_bytes(before)
    handler = QuietHandler
    if not args.package:
        html = (ROOT / 'Tools/WebGLTemplate/FastGame/index.html').read_text(encoding='utf-8')
        handler.source_html = html.replace('__SIMBAR_ASSET_HINTS__', '').replace('__SIMBAR_STYLESHEET__', '/Tools/WebGLTemplate/style.css').replace('__SIMBAR_RUNTIME__', '/Tools/WebGLTemplate/FastGame').replace('href="TemplateData/favicon.ico"', 'href="data:,"')
    document_root = args.package.resolve() if args.package else ROOT
    server = ThreadingHTTPServer(('127.0.0.1', 0), partial(handler, directory=str(document_root)))
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    url = f'http://127.0.0.1:{server.server_port}/' + ('' if args.package else '__owner-venues-qa__/index.html')
    report = {'passed': False, 'url': url, 'viewport': {'width': 1920, 'height': 1080}, 'owners': [], 'checks': [], 'errors': [], 'consoleErrors': [], 'httpErrors': [], 'failedRequests': [], 'abortedRequests': [], 'screenshots': []}
    try:
        with sync_playwright() as pw:
            executable = chromium_executable(None)
            browser = pw.chromium.launch(**({'executable_path': executable} if executable else {}))
            try:
                for owner, label in enumerate(NAMES):
                    context = browser.new_context(viewport=report['viewport'], device_scale_factor=1, locale='ko-KR')
                    context.add_init_script('(' + INSTRUMENT + ')()')
                    page = context.new_page()
                    page.on('pageerror', lambda e: report['errors'].append(str(e)))
                    page.on('console', lambda m: report['consoleErrors'].append(m.text) if m.type == 'error' else None)
                    page.on('response', lambda r: report['httpErrors'].append({'url': r.url, 'status': r.status}) if r.status >= 400 else None)
                    def failed(request):
                        entry = {'url': request.url, 'failure': request.failure}
                        report['abortedRequests' if 'ERR_ABORTED' in (request.failure or '') else 'failedRequests'].append(entry)
                    page.on('requestfailed', failed)
                    try:
                        page.goto(url)
                        ready(page)
                        page.locator('.owner-card').nth(owner).click()
                        selected = proof(page, owner)
                        stored_owner = page.evaluate('localStorage.getItem("SimBar.Owner")')
                        assert stored_owner, 'Owner key was not saved'
                        page.reload()
                        ready(page)
                        assert page.evaluate('simbarGame.owner') == owner
                        assert page.evaluate('localStorage.getItem("SimBar.Owner")') == stored_owner
                        after_reload = proof(page, owner)
                        page.locator('button[data-action="start"]').click()
                        page.wait_for_function('simbarGame.phase==="planning"')
                        assert page.locator('.daydesk').get_attribute('data-owner-index') == str(owner)
                        planning = proof(page, owner)
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
                        # Pause through the normal control for a stable, inspectable frame.
                        page.keyboard.press('p')
                        page.wait_for_function('simbarGame.paused')
                        # Close only the pause overlay in this disposable QA document.
                        # Gameplay stays paused; the captured room, actors and HUD are real.
                        pause_style = page.add_style_tag(content='.pause-ref{display:none!important}.screen-shade:has(.pause-ref){display:none!important}')
                        night = proof(page, owner)
                        screenshot = label + '.png'
                        page.screenshot(path=str(output / screenshot))
                        save_canvas(page, '#unity-canvas', output / (label + '-world.png'))
                        save_canvas(page, '#mini-map', output / (label + '-minimap.png'))
                        report['screenshots'].extend([screenshot, label + '-world.png', label + '-minimap.png'])
                        if owner == 2:
                            report['jaidenBaseline'] = page.evaluate(BASELINE)
                            for item in report['jaidenBaseline']:
                                assert item['main']['equal'] and item['minimap']['equal'], item
                            assert report['jaidenBaseline'][1]['actorCount'] > 0
                        pause_style.evaluate('s=>s.remove()')
                        page.locator('button[data-action="lobby"]').click()
                        page.wait_for_function('simbarGame.phase==="lobby"')
                        other = (owner + 1) % 3
                        page.locator('.owner-card').nth(other).click()
                        switched = proof(page, other)
                        page.reload()
                        ready(page)
                        assert page.evaluate('simbarGame.owner') == other
                        switched_reload = proof(page, other)
                        report['owners'].append({'owner': owner, 'label': label, 'savedOwner': stored_owner, 'selected': selected, 'reloaded': after_reload, 'planning': planning, 'night': night, 'lobbySwitch': switched, 'switchedReload': switched_reload})
                    finally:
                        context.close()
                assert not report['errors'], report['errors']
                assert not report['consoleErrors'], report['consoleErrors']
                assert not report['httpErrors'], report['httpErrors']
                assert not report['failedRequests'], report['failedRequests']
                report['checks'] = [
                    'All three real owner buttons select the expected plate in both completed canvas buffers, including owner-distinctive pixels.',
                    'The actual main and minimap render calls receive the selected owner; both draw the matching artwork.',
                    'Owner selection survives reload, real desk planning, real booking confirmation and launch into the night.',
                    'Returning to the lobby and selecting another owner updates both canvases and persists across another reload.',
                    'Jaiden main/minimap buffers are byte-identical to the pre-interior release renderer with original art, empty and frozen real actor states.',
                    'Three actual 1920x1080 game screenshots plus raw world and minimap PNGs were saved from isolated browser contexts.',
                    'No JavaScript console errors, failed requests or HTTP errors.'
                ]
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
    print(json.dumps({'passed': report['passed'], 'owners': len(report['owners']), 'checks': report['checks'], 'output': str(output)}, ensure_ascii=False))


if __name__ == '__main__':
    main()
