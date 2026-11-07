"""Exercise the actual order queue and rendered food bubbles in Chromium."""
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
import json
from pathlib import Path
import threading

from playwright.sync_api import sync_playwright
from benchmark_web_loading import chromium_executable

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / 'Research/PixelArtVerification/OrderBubbles'
VIEW = ROOT / 'Library/OrderBubbleQA/view.html'

HTML = '''<!doctype html><meta charset="utf-8"><style>
html,body{margin:0;background:#10121e}canvas{display:block;width:960px;height:558px;image-rendering:pixelated}
</style><canvas width="960" height="558"></canvas><script type="module">
import {SimBarGame,catalog} from '/Tools/WebGLTemplate/FastGame/core.js';
import {createWorld,pixelPendingOrders} from '/Tools/WebGLTemplate/FastGame/world.js';
const base='/Tools/WebGLTemplate/FastGame/art/';
const load=async name=>{const image=new Image();image.src=base+name;await image.decode();return image;};
const spriteMeta=await(await fetch(base+'sprites.json')).json();
const venueMeta=await(await fetch(base+'venue.json')).json();
const atlases=Object.fromEntries(await Promise.all(Object.entries(spriteMeta.atlases).map(async([key,entry])=>[key,await load(entry.file)])));
const [venue,seoul]=await Promise.all([load('venue.png'),load('seoul.png')]);
const store=new Map(),g=new SimBarGame({seed:1,storage:{getItem:k=>store.get(k)||null,setItem:(k,v)=>store.set(k,String(v)),removeItem:k=>store.delete(k)}});
g.act('start');g.act('launch');while(g.phase==='guide')g.act('guide');
g.state.upgrades[2]=2;
g._assignOrder('patron-12',3);g._assignOrder('patron-13',4);
if(catalog.drinks.length>5)g._assignOrder('patron-14',5);
const canvas=document.querySelector('canvas');
const world=createWorld(canvas,{assets:{venue,seoul,spriteMeta,venueMeta,atlases},getState:()=>({phase:g.phase,paused:g.paused,actors:g.actors,orders:g.orders,journey:g.journey,floor:g.floor})});
const context=canvas.getContext('2d'),original=context.drawImage.bind(context);
let iconDraws=[];
context.drawImage=(...args)=>{if(args[0]===atlases.orders)iconDraws.push(args.slice(1));return original(...args);};
let timestamp=0;
const render=()=>{iconDraws=[];world.render(timestamp+=100);return{orders:pixelPendingOrders({phase:g.phase,actors:g.actors,orders:g.orders}).map(o=>({id:o.actor.id,item:o.item})),draws:iconDraws,phase:g.phase};};
window.qa={g,world,canvas,render,catalog};window.ready=true;
</script>'''


class QuietHandler(SimpleHTTPRequestHandler):
    def log_message(self, *_args):
        pass


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    VIEW.parent.mkdir(parents=True, exist_ok=True)
    VIEW.write_text(HTML, encoding='utf-8', newline='\n')
    server = ThreadingHTTPServer(('127.0.0.1', 0), partial(QuietHandler, directory=ROOT))
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    report = {'errors': [], 'checks': []}
    try:
        with sync_playwright() as p:
            browser = p.chromium.launch(executable_path=chromium_executable(None))
            page = browser.new_page(viewport={'width': 960, 'height': 558})
            page.on('pageerror', lambda error: report['errors'].append(str(error)))
            page.goto(f'http://127.0.0.1:{server.server_port}/Library/OrderBubbleQA/view.html')
            page.wait_for_function('window.ready===true')
            page.evaluate('for(let i=0;i<80;i++)qa.g.tick(.05)')
            first = page.evaluate('qa.render()')
            assert {order['item'] for order in first['orders']} == set(range(page.evaluate('qa.catalog.drinks.length')))
            assert len(first['draws']) == len(first['orders'])
            assert [draw[0] // 32 for draw in first['draws']] == [o['item'] for o in first['orders']]
            for i, draw in enumerate(first['draws']):
                for other in first['draws'][i+1:]:
                    x, y, ox, oy = draw[4], draw[5], other[4], other[5]
                    assert x+24 < ox or ox+24 < x or y+24 < oy or oy+24 < y
            page.screenshot(path=str(OUT / 'pending-orders.png'))
            report['checks'].append('Every real pending menu item draws its own atlas cell.')
            report['checks'].append('Neighbouring customer icons remain separately visible and keep their own head pointers.')
            report['before'] = first

            wrong = page.evaluate('''() => {const o=qa.g.orders[0];const before=qa.g.orders.map(o=>o.id);const result=qa.g._serve((o.item+1)%qa.catalog.drinks.length,false,1);return{result,before,after:qa.render()};}''')
            assert not wrong['result']['success']
            assert wrong['before'] == [o['id'] for o in wrong['after']['orders']]
            report['checks'].append('Rejected serving leaves the real pending bubble intact.')

            served = page.evaluate('''() => {const id=qa.g.orders[0].id,item=qa.g.orders[0].item;const result=qa.g._serve(item,true,.7);return{id,result,after:qa.render()};}''')
            assert served['result']['success']
            assert not any(o['id'] == served['id'] for o in served['after']['orders'])
            assert len(served['after']['draws']) == len(served['after']['orders'])
            page.screenshot(path=str(OUT / 'after-service.png'))
            report['checks'].append('Successful serving removes that customer bubble on the next paint.')
            report['served'] = served

            moved = page.evaluate('''() => {const before=qa.render();for(let i=0;i<30;i++)qa.g.tick(.05);return{before,after:qa.render()};}''')
            assert moved['before']['draws'] != moved['after']['draws']
            report['checks'].append('Bubble drawing coordinates move with the walking customers.')

            manual = page.evaluate('''() => {const id=qa.g.orders[0].id,started=qa.g.act('prepare'),during=qa.render();qa.g.tick(.2);const result=qa.g.act('pourStop');return{id,started,during,result,after:qa.render()};}''')
            assert manual['started']['success'] and manual['result']['success']
            assert any(o['id'] == manual['id'] for o in manual['during']['orders'])
            assert not any(o['id'] == manual['id'] for o in manual['after']['orders'])
            report['checks'].append('Manual prepare keeps its bubble until actual pourStop serving succeeds.')

            paused = page.evaluate('''() => {qa.g.act('pause');qa.render();const before=qa.canvas.toDataURL();qa.g.tick(1);qa.render();return before===qa.canvas.toDataURL();}''')
            assert paused
            report['checks'].append('Paused world and its pending bubbles remain pixel-identical.')
            page.evaluate("qa.g.act('resume')")
            expired = page.evaluate('''() => {const id=qa.g.orders[0].id;qa.g.orders[0].patience=-1;qa.g._requestTimer=999;qa.g.tick(.05);return{id,after:qa.render()};}''')
            assert not any(o['id'] == expired['id'] for o in expired['after']['orders'])
            report['checks'].append('Expired orders remove their customer bubbles.')

            closing = page.evaluate('qa.g._beginClosing();qa.render()')
            assert closing['phase'] == 'closing' and not closing['orders'] and not closing['draws']
            page.screenshot(path=str(OUT / 'after-show-no-orders.png'))
            report['checks'].append('Closing clears all pending order bubbles.')
            assert not report['errors'], report['errors']
            report['passed'] = True
            browser.close()
    finally:
        server.shutdown()
        server.server_close()
    (OUT / 'renderer-qa.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf-8', newline='\n')
    print(json.dumps({'passed': report['passed'], 'checks': report['checks'], 'errors': report['errors']}))


if __name__ == '__main__':
    main()
