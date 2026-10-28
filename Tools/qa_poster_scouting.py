"""Pointer/touch QA for the built WebGL poster desk. Requires playwright.

uv run --with playwright python Tools/qa_poster_scouting.py [URL]
"""
import json
from pathlib import Path
import sys
from playwright.sync_api import sync_playwright

url=sys.argv[1] if len(sys.argv)>1 else 'http://127.0.0.1:8767/'
out=Path('Library/PosterQa/live' if url.startswith('https:') else 'Library/PosterQa/local')
out.mkdir(parents=True,exist_ok=True)
with sync_playwright() as p:
    browser=p.chromium.launch(executable_path='C:/Users/Jtsgr/AppData/Local/ms-playwright/chromium-1243/chrome-win64/chrome.exe')
    context=browser.new_context(viewport={'width':1280,'height':800},device_scale_factor=2,locale='ko-KR',has_touch=True)
    page=context.new_page(); page.set_default_timeout(90000)
    errors=[]; posters=[]
    page.on('pageerror',lambda error:errors.append(str(error)))
    page.on('console',lambda msg:errors.append(msg.text) if msg.type=='error' else None)
    page.on('request',lambda request:posters.append(request.url) if '/StreamingAssets/ScoutPosters/' in request.url else None)
    page.goto(url); page.wait_for_function('window.simbarGameReadyAt>0')
    ready=page.evaluate('window.simbarGameReadyAt')
    assert not posters, 'Posters must not download during initial title loading.'
    page.touchscreen.tap(640,678)
    page.wait_for_timeout(600)
    rect=page.locator('#unity-canvas').bounding_box(); oy=rect['y']
    page.screenshot(path=str(out/'desktop-first-poster.png'))
    # Each venue loads its image only when selected.
    for name,x,file in [('pet',490,'pet-20240315.jpg'),('boogie',790,'boogie-weekly.png'),('subriot',1090,'subriot-20251212.png')]:
        with page.expect_response(lambda response: file in response.url) as response:
            page.touchscreen.tap(x,oy+295)
        assert response.value.status==200,(file,response.value.status)
        assert response.value.body(), 'Poster response body must contain image bytes.'
        page.wait_for_timeout(350)
        page.screenshot(path=str(out/f'{name}-poster.png'))
    # The second Pet Sounds poster is reachable through the gallery's next control.
    page.touchscreen.tap(490,oy+295); page.wait_for_timeout(350)
    page.mouse.move(1050,550)
    for _ in range(4):
        page.mouse.wheel(0,5000); page.wait_for_timeout(250)
    page.screenshot(path=str(out/'pet-gallery-bottom.png'))
    with page.expect_response(lambda response:'pet-20260613.png' in response.url,timeout=10000) as response:
        page.touchscreen.tap(942,670)
    assert response.value.status==200 and response.value.body()
    page.wait_for_timeout(400)
    page.screenshot(path=str(out/'pet-linked-instagram-poster.png'))
    page.touchscreen.tap(1200,oy+67); page.wait_for_timeout(350)
    page.screenshot(path=str(out/'english-poster.png'))
    page.touchscreen.tap(1200,oy+67); page.wait_for_timeout(350)
    page.touchscreen.tap(185,oy+295); page.wait_for_timeout(400)
    page.mouse.move(1050,550); page.mouse.wheel(0,2000); page.wait_for_timeout(450)
    page.screenshot(path=str(out/'desktop-full-poster-scrolled.png'))
    # Sticky action researches the poster, then opens the first discovered artist.
    page.touchscreen.tap(1060,755); page.wait_for_timeout(350)
    page.screenshot(path=str(out/'discovered.png'))
    page.touchscreen.tap(1060,755); page.wait_for_timeout(350)
    page.screenshot(path=str(out/'real-artist-details.png'))
    page.touchscreen.tap(1055,oy+410); page.wait_for_timeout(250)
    page.touchscreen.tap(670,oy+461); page.wait_for_timeout(350)
    page.touchscreen.tap(1055,oy+461); page.wait_for_timeout(350)
    page.screenshot(path=str(out/'real-booking-confirmed.png'))
    page.touchscreen.tap(1200,oy+31); page.wait_for_timeout(350)
    page.reload(); page.wait_for_function('window.simbarGameReadyAt>0')
    page.touchscreen.tap(640,678); page.wait_for_timeout(400)
    page.touchscreen.tap(74,oy+120); page.wait_for_timeout(400)
    page.screenshot(path=str(out/'discovery-after-reload.png'))
    # Mobile poster scrolling and full-size opening through a real tap.
    page.set_viewport_size({'width':393,'height':852}); page.wait_for_timeout(400)
    page.screenshot(path=str(out/'portrait-poster.png'))
    with context.expect_page(timeout=10000) as popup:
        page.touchscreen.tap(196,620)
    image_page=popup.value
    image_page.wait_for_url('**/StreamingAssets/ScoutPosters/**',wait_until='commit')
    image_page.close(); page.bring_to_front()
    page.set_viewport_size({'width':844,'height':390}); page.wait_for_timeout(350)
    page.screenshot(path=str(out/'landscape-poster.png'))
    page.set_viewport_size({'width':1280,'height':800}); page.wait_for_timeout(350)
    page.touchscreen.tap(1060,755); page.wait_for_timeout(350)
    page.screenshot(path=str(out/'real-details-restored.png'))
    page.touchscreen.tap(1060,755); page.wait_for_timeout(300)
    for _ in range(3):
        page.touchscreen.tap(640,585); page.wait_for_timeout(300)
    page.screenshot(path=str(out/'real-band-live.png'))
    page.keyboard.press('m'); page.wait_for_timeout(300)
    page.screenshot(path=str(out/'service-menu.png'))
    assert not errors, errors
    assert len(set(posters))==5,posters
    print(json.dumps({'url':url,'readyMs':ready,'startupPosterRequests':0,'loadedPosters':len(set(posters)),'errors':errors,'screenshots':str(out)}))
    context.close(); browser.close()
