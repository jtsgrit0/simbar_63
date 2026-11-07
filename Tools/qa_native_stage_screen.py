"""Render real album jackets on the actual LED and verify ended-show lights."""
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
import hashlib
import json
from pathlib import Path
import threading

from playwright.sync_api import sync_playwright
from benchmark_web_loading import chromium_executable

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / 'Research/PixelArtVerification/AlbumStageScreen/Renderer'
VIEW = ROOT / 'Library/StageScreenQA/index.html'
HTML = '''<!doctype html><meta charset="utf-8"><style>html,body{margin:0;background:#10121e}canvas{display:block;width:960px;height:558px;image-rendering:pixelated}</style><canvas width="960" height="558"></canvas><script type="module">
import {createWorld,pixelLedCoverFit,PIXEL_LED_LAMPS} from '/Tools/WebGLTemplate/FastGame/world.js';
const base='/Tools/WebGLTemplate/FastGame/art/';
const load=async file=>{const image=new Image();image.src=base+file;await image.decode();return image;};
const [spriteMeta,venueMeta]=await Promise.all(['sprites.json','venue.json'].map(async f=>(await fetch(base+f)).json()));
const atlases=Object.fromEntries(await Promise.all(Object.entries(spriteMeta.atlases).map(async([key,entry])=>[key,await load(entry.file)])));
const [venue,seoul]=await Promise.all([load('venue.png'),load('seoul.png')]);
const albumCovers={},canvas=document.querySelector('canvas'),ctx=canvas.getContext('2d');
const world=createWorld(canvas,{assets:{venue,seoul,spriteMeta,venueMeta,atlases,albumCovers}});
let draws=[],gradients=0;const draw=ctx.drawImage.bind(ctx),gradient=ctx.createLinearGradient.bind(ctx);
ctx.drawImage=(...args)=>{if(Object.values(albumCovers).includes(args[0]))draws.push({src:args[0].src||'diagnostic',rect:args.slice(1)});return draw(...args);};
ctx.createLinearGradient=(...args)=>{gradients++;return gradient(...args);};
const render=(index=0,phase='playing',journey={})=>{draws=[];gradients=0;world.render(0,{phase,paused:true,playlist:index,actors:[],floor:{},journey:{session:1,secondsLeft:90,...journey}});return{index,phase,draws,gradients};};
const loadCovers=async()=>{const manifest=await(await fetch(base+'album-covers.json')).json(),cache=new Map();
 for(const entry of manifest.tracks){if(!cache.has(entry.cover))cache.set(entry.cover,await load(entry.cover));albumCovers[entry.index]=cache.get(entry.cover);}return manifest;};
const diagnostic=(w,h)=>{const c=document.createElement('canvas');c.width=w;c.height=h;const p=c.getContext('2d');p.fillStyle='#efb537';p.fillRect(0,0,w,h);p.fillStyle='#e02839';p.fillRect(0,0,w*.18,h);p.fillStyle='#2363c7';p.fillRect(w*.82,0,w*.18,h);p.fillStyle='#3bcc6c';p.fillRect(0,0,w,h*.12);p.fillStyle='#f3e7ca';p.fillRect(0,h*.88,w,h*.12);albumCovers[0]=c;return pixelLedCoverFit(w,h);};
window.qa={world,canvas,ctx,albumCovers,render,loadCovers,diagnostic,fit:pixelLedCoverFit,lamps:PIXEL_LED_LAMPS};window.ready=true;
</script>'''


class QuietHandler(SimpleHTTPRequestHandler):
    def log_message(self, *_args):
        pass


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    VIEW.parent.mkdir(parents=True, exist_ok=True)
    VIEW.write_text(HTML, encoding='utf-8', newline='\n')
    server = ThreadingHTTPServer(('127.0.0.1', 0), partial(QuietHandler, directory=ROOT))
    threading.Thread(target=server.serve_forever, daemon=True).start()
    report = {'passed': False, 'checks': [], 'errors': [], 'httpErrors': [], 'screenshots': [],
              'rendererSha256': hashlib.sha256((ROOT / 'Tools/WebGLTemplate/FastGame/world.js').read_bytes()).hexdigest()}
    try:
        with sync_playwright() as pw:
            executable = chromium_executable(None)
            browser = pw.chromium.launch(**({'executable_path': executable} if executable else {}))
            page = browser.new_page(viewport={'width': 960, 'height': 558})
            page.on('pageerror', lambda error: report['errors'].append(str(error)))
            page.on('response', lambda response: report['httpErrors'].append({'url': response.url, 'status': response.status}) if response.status >= 400 else None)
            cover_requests = []
            page.on('request', lambda request: cover_requests.append(request.url) if '/album-covers' in request.url else None)

            def shot(label):
                page.screenshot(path=str(OUT / (label+'.png')))
                report['screenshots'].append(label+'.png')

            page.goto(f'http://127.0.0.1:{server.server_port}/Library/StageScreenQA/index.html')
            page.wait_for_function('window.ready===true')
            fallback = page.evaluate('qa.render()')
            assert not cover_requests and not fallback['draws']
            page.evaluate("qa.render(0,'planning')")
            reference_rim = page.evaluate('[[552,302],[791,378],[872,316]].map(([x,y])=>Array.from(qa.ctx.getImageData(x,y,1,1).data))')
            reference_overlaps = page.evaluate('[[725,260],[739,263],[716,265],[740,280]].map(([x,y])=>Array.from(qa.ctx.getImageData(x,y,1,1).data))')
            report['checks'].append('The first rendered frame requires no cover image or manifest request.')
            report['manifest'] = page.evaluate('qa.loadCovers()')
            tracks = report['manifest']['tracks']
            assert [entry['index'] for entry in tracks] == list(range(5))
            report['tracks'] = []
            for entry in tracks:
                index = entry['index']
                frame = page.evaluate('index=>qa.render(index)', index)
                assert len(frame['draws']) == 1 and frame['gradients'] == 4
                cover = frame['draws'][0]
                assert cover['src'].endswith('/'+entry['cover'])
                assert cover['rect'][:4] == [0, 0, entry['width'], entry['height']]
                fit = page.evaluate('size=>qa.fit(...size)', [entry['width'], entry['height']])
                assert abs(fit['width']/fit['height'] - entry['width']/entry['height']) < 1e-9
                report['tracks'].append({**frame, 'fit': fit})
                if index < 3:
                    shot(f'0{index+1}-track-{index}-authentic-jacket')
            assert len(set(cover_requests)) == 4  # one manifest plus three unique originals
            assert page.evaluate('qa.albumCovers[0]===qa.albumCovers[3] && qa.albumCovers[3]===qa.albumCovers[4]')
            report['checks'].append('All five tracks use the manifest-matched original; shared album tracks reuse one decoded jacket.')
            report['checks'].append('Each original is drawn in full, centered with contain sizing in the normalized LED plane.')

            page.evaluate("qa.render(0,'playing')")
            frozen = page.evaluate('qa.canvas.toDataURL()')
            lit_lamps = page.evaluate('[[643,121],...qa.lamps].map(([x,y])=>Array.from(qa.ctx.getImageData(x,y,1,1).data))')
            lit_pools = page.evaluate('[[706,333],[802,309]].map(([x,y])=>Array.from(qa.ctx.getImageData(x,y,1,1).data))')
            page.evaluate("qa.render(0,'playing')")
            assert page.evaluate('qa.canvas.toDataURL()') == frozen
            report['checks'].append('Pausing an unfinished performance freezes its existing lighting.')
            page.evaluate('window.qaBarBefore=Array.from(qa.ctx.getImageData(180,160,220,160).data)')
            closing = page.evaluate("qa.render(0,'closing',{floorClosing:true,secondsLeft:0,musicPaused:true})")
            assert closing['gradients'] == 0
            ended = page.evaluate('qa.canvas.toDataURL()')
            replay = page.evaluate("qa.render(0,'closing',{floorClosing:true,secondsLeft:0,musicPaused:false})")
            assert replay['gradients'] == 0 and page.evaluate('qa.canvas.toDataURL()') == ended
            results = page.evaluate("qa.render(0,'results',{secondsLeft:0,musicPaused:false})")
            assert results['gradients'] == 0 and page.evaluate('qa.canvas.toDataURL()') == ended
            dark_lamps = page.evaluate('[[643,121],...qa.lamps].map(([x,y])=>Array.from(qa.ctx.getImageData(x,y,1,1).data))')
            dark_pools = page.evaluate('[[706,333],[802,309]].map(([x,y])=>Array.from(qa.ctx.getImageData(x,y,1,1).data))')
            report['lampPixels'] = {'playing': lit_lamps, 'ended': dark_lamps}
            report['poolPixels'] = {'playing': lit_pools, 'ended': dark_pools}
            assert all(max(pixel[:3]) < 70 for pixel in dark_lamps)
            assert all(sum(dark[:3]) < sum(lit[:3])*.5 for lit, dark in zip(lit_pools, dark_pools))
            assert page.evaluate('[[552,302],[791,378],[872,316]].map(([x,y])=>Array.from(qa.ctx.getImageData(x,y,1,1).data))') == reference_rim
            ended_overlaps = page.evaluate('[[725,260],[739,263],[716,265],[740,280]].map(([x,y])=>Array.from(qa.ctx.getImageData(x,y,1,1).data))')
            report['overlappingOccluders'] = {'before': reference_overlaps, 'ended': ended_overlaps}
            for original, dimmed in zip(reference_overlaps, ended_overlaps):
                for channel, dark in enumerate([5, 9, 18]):
                    # Antialiased mask-edge pixels may retain more of the
                    # original drum rim, but repeated masks must never make
                    # an overlap darker than one full deck treatment.
                    assert dimmed[channel] >= original[channel]*.28+dark*.72-3
            assert page.evaluate('Array.from(qa.ctx.getImageData(180,160,220,160).data).every((p,i)=>p===qaBarBefore[i])')
            shot('04-ended-show-stage-lights-off')
            report['checks'].append('Closing/results have no dynamic beam/pool/flare pass; replaying music cannot turn stage effects back on.')
            report['checks'].append('Existing bar and ambient artwork stays pixel-identical when the stage effects turn off.')
            report['checks'].append('All five baked lamp lenses become unlit and the bright stage pools dim while cyan trim stays unchanged.')
            report['checks'].append('Overlapping drum occluders never darken beyond one deck treatment.')

            report['diagnostics'] = []
            for label, size in [('portrait', [160, 240]), ('wide', [320, 120])]:
                fit = page.evaluate('size=>qa.diagnostic(...size)', size)
                frame = page.evaluate("qa.render(0,'results',{secondsLeft:0})")
                assert frame['draws'][0]['rect'][:4] == [0, 0, *size]
                assert abs(fit['width']/fit['height'] - size[0]/size[1]) < 1e-9
                a, b, c, d, *_ = fit['transform']
                assert abs((a*a+b*b)**.5 - 1) < 1e-9 and abs((c*c+d*d)**.5 - 1) < 1e-9
                assert fit['x'] >= 0 and fit['y'] >= 0
                report['diagnostics'].append({'label': label, 'sourceSize': size, 'fit': fit, 'draw': frame['draws']})
                shot('05-diagnostic-'+label+'-full-image')
            report['checks'].append('Portrait and wide diagnostic sources retain all four source edges and original aspect with dark bars.')
            report['coverRequests'] = cover_requests
            assert not report['errors'], report['errors']
            assert not report['httpErrors'], report['httpErrors']
            report['passed'] = True
            browser.close()
    except Exception as error:
        report['failure'] = str(error)
        raise
    finally:
        server.shutdown();server.server_close()
        (OUT / 'renderer-qa.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf-8', newline='\n')
    print(json.dumps({'passed': report['passed'], 'checks': report['checks'], 'errors': report['errors']}))


if __name__ == '__main__':
    main()
