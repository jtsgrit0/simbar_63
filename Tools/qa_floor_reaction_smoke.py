"""Check the visible countdown and tap target during natural random events.

uv run --with pillow --with playwright python Tools/qa_floor_reaction_smoke.py [URL]

Uses fresh browser profiles, visible controls, and unseeded gameplay only. Stops
after clock/button proof at desktop, portrait, landscape and tiny landscape.
"""
import argparse
from io import BytesIO
import json
import math
import os
from pathlib import Path
import time

from PIL import Image
from playwright.sync_api import sync_playwright

from qa_floor_missions import CHROME, DESKTOP, PORTRAIT, LANDSCAPE


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('url', nargs='?', default='http://127.0.0.1:8767/')
    parser.add_argument('--max-nights', type=int, default=3)
    parser.add_argument('--headful', action='store_true')
    args = parser.parse_args()
    out = Path('Library/FloorQa/live' if args.url.startswith('https:') else 'Library/FloorQa/local') / 'reaction-smoke'
    out.mkdir(parents=True, exist_ok=True)
    report = {'url': args.url, 'screenshots': str(out), 'nights': [], 'views': [], 'errors': []}
    began = time.monotonic()

    def log(message):
        print(f'[{time.monotonic() - began:6.1f}s] {message}', flush=True)

    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(
            executable_path=os.environ.get('SIMBAR_QA_CHROME', CHROME), headless=not args.headful,
            args=['--disable-background-timer-throttling', '--disable-renderer-backgrounding'])
        try:
            for number in range(1, max(1, args.max_nights) + 1):
                context = browser.new_context(viewport=DESKTOP, device_scale_factor=2, locale='ko-KR', has_touch=True)
                page = context.new_page()
                page.set_default_timeout(90000)
                night = {'number': number, 'errors': [], 'requestedBuildAssets': []}
                report['nights'].append(night)
                page.on('pageerror', lambda error: night['errors'].append(str(error)))
                page.on('console', lambda message: night['errors'].append(message.text) if message.type == 'error' else None)
                page.on('request', lambda request: night['requestedBuildAssets'].append(request.url) if '/Build/' in request.url else None)

                def canvas():
                    value = page.locator('#unity-canvas').bounding_box()
                    assert value and value['width'] > 0 and value['height'] > 0, 'Unity canvas is not visible.'
                    return value

                def read_image(png=None):
                    image = Image.open(BytesIO(png or page.screenshot())).convert('RGB')
                    viewport = page.viewport_size
                    sx, sy = image.width / viewport['width'], image.height / viewport['height']

                    def pixel(x, y):
                        return image.getpixel((min(image.width - 1, max(0, round(x * sx))),
                                               min(image.height - 1, max(0, round(y * sy)))))
                    return image, pixel, sx, sy

                def cream_planning():
                    _, pixel, _, _ = read_image()
                    box = canvas()
                    samples = [pixel(box['x'] + 8, box['y'] + 12),
                               pixel(box['x'] + box['width'] / 2, box['y'] + 8),
                               pixel(box['x'] + 8, box['y'] + box['height'] - 100)]
                    return all(all(abs(rgb[i] - (245, 240, 224)[i]) <= 5 for i in range(3)) for rgb in samples)

                def running_hud():
                    _, pixel, _, _ = read_image()
                    box = canvas()
                    rgb = pixel(box['x'] + box['width'] - 236, box['y'] + 118)
                    return all(abs(rgb[i] - (11, 15, 21)[i]) <= 6 for i in range(3)) and not cream_planning()

                def click(x, y, delay=750):
                    page.mouse.click(x, y, delay=90)
                    page.wait_for_timeout(delay)

                def next_guide():
                    box = canvas()
                    scale = math.sqrt((box['width'] / 1920) * (box['height'] / 1080))
                    click(box['x'] + box['width'] / 2, box['y'] + box['height'] / 2 + 235 * scale, delay=650)

                def geometry():
                    box = canvas()
                    # SimBarBookingLayout passes at least 240 CSS pixels to the
                    # UI even when the physical canvas is slightly shorter.
                    layout_width, layout_height = max(240, box['width']), max(240, box['height'])
                    compact, very_short = layout_height < 500, layout_height < 300
                    width = min(340, layout_width - 24)
                    top = 56 if very_short else 74 if compact else 180
                    height = min(260 if compact else 300, layout_height - top - (8 if very_short else 12))
                    tight = height < 245
                    button_height = 46 if tight else 58
                    button_y = height - 14 - button_height if tight else 157 if compact else 186
                    return {'x': box['x'] + layout_width - width - 12, 'y': box['y'] + top,
                            'width': width, 'height': height, 'buttonY': button_y,
                            'buttonHeight': button_height, 'tight': tight}

                def reaction_probe():
                    image, pixel, sx, sy = read_image()
                    geo = geometry()
                    background = pixel(geo['x'] + 6, geo['y'] + 6)
                    # The almost-opaque panel still picks up venue tint at some
                    # DPR/viewport combinations. Identify it by a dark panel and
                    # actual countdown glyphs, not an exact background RGB.
                    dark = max(background) < 55
                    clock_x, clock_y = geo['x'] + geo['width'] - 108, geo['y'] + 8
                    bounds = (round(clock_x * sx), round(clock_y * sy),
                              round((clock_x + 94) * sx), round((clock_y + 45) * sy))
                    glyphs = sum(1 for r, g, b in image.crop(bounds).getdata()
                                 if min(r, g, b) > 175 or (r > 175 and 45 < g < 150 and 30 < b < 130))
                    card = dark and glyphs > 20
                    rgb = pixel(geo['x'] + 21, geo['y'] + geo['buttonY'] + geo['buttonHeight'] / 2)
                    r, g, b = rgb
                    kind = 'C' if card and g - r > 35 and g > 78 and b > 55 else 'F' if card and r > 65 and 42 < g < 85 and 12 < b < 55 and r - g > 12 else None
                    return {'card': card, 'kind': kind, 'button': rgb,
                            'background': background, 'clockGlyphPixels': glyphs}

                def wait_for_card(seconds, active_only=False):
                    deadline = time.monotonic() + seconds
                    next_log = 0
                    while time.monotonic() < deadline:
                        current = reaction_probe()
                        if current['kind'] or (current['card'] and not active_only):
                            return current
                        if time.monotonic() >= next_log:
                            log(f'Night {number}: waiting for a natural random reaction ({deadline - time.monotonic():.0f}s left)')
                            next_log = time.monotonic() + 20
                        page.wait_for_timeout(450)
                    return None

                try:
                    log(f'Loading native random night {number}')
                    page.goto(args.url)
                    page.wait_for_function('window.simbarGameReadyAt > 0')
                    night['readyMs'] = page.evaluate('window.simbarGameReadyAt')
                    click(640, 678, delay=900)
                    box = canvas()
                    click(box['x'] + 182, box['y'] + 121, delay=900)
                    assert cream_planning(), 'Visible controls did not open daytime planning.'
                    for _ in range(3):
                        if not cream_planning():
                            break
                        box = canvas()
                        click(box['x'] + box['width'] - 209, box['y'] + box['height'] - 45, delay=900)
                    assert not cream_planning(), 'Planning launch did not open the guide.'
                    for _ in range(5):
                        if running_hud():
                            break
                        next_guide()
                    assert running_hud(), 'Guide did not reach a visible running night.'
                    page.screenshot(path=str(out / f'night-{number}-running.png'))
                    current = wait_for_card(250, active_only=True)
                    night['reactionFound'] = bool(current)
                    if not current:
                        page.screenshot(path=str(out / f'night-{number}-no-random-prompt.png'))
                        log('This native night supplied no observed prompt; trying another fresh profile.')
                        continue
                    night['firstReaction'] = current
                    for name, viewport in [('desktop', DESKTOP), ('portrait', PORTRAIT), ('landscape', LANDSCAPE),
                                           ('tiny-landscape', {'width': 640, 'height': 284})]:
                        if page.viewport_size != viewport:
                            page.set_viewport_size(viewport)
                            page.wait_for_timeout(250)
                        assert wait_for_card(12), f'No reaction or retry card visible at {name}.'
                        png = page.screenshot(path=str(out / f'{name}-reaction.png'))
                        image, pixel, sx, sy = read_image(png)
                        geo = geometry()
                        clock = {'x': geo['x'] + geo['width'] - 108, 'y': geo['y'] + 8, 'width': 94, 'height': 45}
                        bounds = (round(clock['x'] * sx), round(clock['y'] * sy),
                                  round((clock['x'] + clock['width']) * sx), round((clock['y'] + clock['height']) * sy))
                        clock_image = image.crop(bounds)
                        glyphs = sum(1 for r, g, b in clock_image.getdata()
                                     if min(r, g, b) > 175 or (r > 175 and 45 < g < 150 and 30 < b < 130))
                        page.screenshot(path=str(out / f'{name}-clock.png'), clip=clock)
                        button = {'x': geo['x'] + 14, 'y': geo['y'] + geo['buttonY'],
                                  'width': geo['width'] - 28, 'height': geo['buttonHeight']}
                        inside = button['x'] >= 0 and button['y'] >= 0 and button['x'] + button['width'] <= viewport['width'] + .5 and button['y'] + button['height'] <= viewport['height'] + .5
                        colors = [pixel(button['x'] + 7, button['y'] + offset)
                                  for offset in [6, button['height'] / 2, button['height'] - 6]]
                        colored = all(sum(abs(rgb[i] - (11, 15, 21)[i]) for i in range(3)) > 30 for rgb in colors)
                        view = {'name': name, 'viewport': viewport, 'canvas': canvas(), 'geometry': geo,
                                'clockGlyphPixels': glyphs, 'button': button, 'buttonColors': colors,
                                'buttonEntirelyVisible': inside and colored}
                        report['views'].append(view)
                        log(f'{name}: clock glyph pixels={glyphs}; complete colored tap button={inside and colored}')
                        assert glyphs > 20, f'Countdown glyphs are missing from the dedicated timer rectangle at {name}.'
                        assert inside and colored and button['height'] >= 44, f'The complete 44px+ response button is not visible at {name}.'
                    assert not night['errors'], night['errors']
                    report['completed'] = True
                    break
                except Exception as error:
                    report['failure'] = repr(error)
                    try:
                        page.screenshot(path=str(out / 'failure.png'))
                    except Exception:
                        pass
                    raise
                finally:
                    report['errors'].extend(night['errors'])
                    context.close()
            if not report.get('completed'):
                report['incompleteCoverage'] = 'No native random reaction was observed within the configured fresh nights; no state was forced.'
        finally:
            report['durationSeconds'] = round(time.monotonic() - began, 2)
            (out / 'run.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
            print(json.dumps(report, ensure_ascii=False), flush=True)
            browser.close()
    return 0 if report.get('completed') and not report['errors'] else 2


if __name__ == '__main__':
    raise SystemExit(main())
