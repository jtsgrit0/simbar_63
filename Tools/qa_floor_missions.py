"""Visible-control QA for spontaneous WebGL floor events and rapid responses.

uv run --with pillow --with playwright python Tools/qa_floor_missions.py [URL]
uv run --with pillow --with playwright python Tools/qa_floor_missions.py URL --smoke

Full QA plays natural random nights, up to three fresh profiles, until guest and
cleanup prompts have both been seen. It never seeds, reads or changes game state.
PNG color probes identify the visible response card; all input is actual keyboard
or touch input. Screenshots retain the evidence for copy, timing, effects, employee
motion, layouts and wallet values. An unchanged saved receipt is pixel-compared
after reload; this alone is not a wallet correctness assertion.
"""
import argparse
import hashlib
from io import BytesIO
import json
import math
import os
from pathlib import Path
import time

from PIL import Image
from playwright.sync_api import sync_playwright

DESKTOP = {'width': 1280, 'height': 800}
PORTRAIT = {'width': 393, 'height': 852}
LANDSCAPE = {'width': 844, 'height': 390}
CHROME = 'C:/Users/Jtsgr/AppData/Local/ms-playwright/chromium-1243/chrome-win64/chrome.exe'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('url', nargs='?', default='http://127.0.0.1:8767/')
    parser.add_argument('--smoke', action='store_true', help='Only load and exercise the live read-only H panel for 30 seconds.')
    parser.add_argument('--max-nights', type=int, default=3)
    parser.add_argument('--headful', action='store_true')
    args = parser.parse_args()
    out = Path('Library/FloorQa/live' if args.url.startswith('https:') else 'Library/FloorQa/local')
    if args.smoke:
        out = out / 'smoke'
    out.mkdir(parents=True, exist_ok=True)
    report = {'url': args.url, 'smoke': args.smoke, 'screenshots': str(out), 'errors': [], 'nights': [], 'coverage': {
        'guestPromptSeen': False, 'cleanupPromptSeen': False, 'guestTimeoutObserved': False,
        'guestKeyboardBurstSent': False, 'guestTouchBurstSent': False, 'cleanupTouchBurstSent': False,
    }}
    began = time.monotonic()
    guest_bursts = 0
    cleanup_bursts = 0

    def log(message):
        print(f'[{time.monotonic() - began:6.1f}s] {message}', flush=True)

    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(
            executable_path=os.environ.get('SIMBAR_QA_CHROME', CHROME), headless=not args.headful,
            args=['--disable-background-timer-throttling', '--disable-renderer-backgrounding'])
        try:
            for night_index in range(1, 2 if args.smoke else max(1, args.max_nights) + 1):
                night_out = out / f'night-{night_index}'
                night_out.mkdir(parents=True, exist_ok=True)
                night = {'number': night_index, 'screenshots': str(night_out), 'stages': [], 'inputs': [], 'errors': [], 'requestedBuildAssets': []}
                report['nights'].append(night)
                context = browser.new_context(viewport=DESKTOP, device_scale_factor=2, locale='ko-KR', has_touch=True)
                page = context.new_page()
                page.set_default_timeout(90000)
                page.on('pageerror', lambda error: night['errors'].append(str(error)))
                page.on('console', lambda message: night['errors'].append(message.text) if message.type == 'error' else None)
                page.on('request', lambda request: night['requestedBuildAssets'].append(request.url) if '/Build/' in request.url else None)

                def rect():
                    value = page.locator('#unity-canvas').bounding_box()
                    assert value and value['width'] > 0 and value['height'] > 0, 'Unity canvas is not visible.'
                    return value

                def capture(name):
                    page.screenshot(path=str(night_out / f'{name}.png'))
                    night['stages'].append(name)
                    log(f'Night {night_index}: captured {name}')

                def tap(x, y, delay=350):
                    page.touchscreen.tap(x, y)
                    page.wait_for_timeout(delay)

                def click(x, y, delay=750):
                    # Keep the edge across several Unity frames while fresh UI
                    # fonts and layouts settle, rather than an instantaneous tap.
                    page.mouse.click(x, y, delay=90)
                    page.wait_for_timeout(delay)

                def set_viewport(value):
                    if page.viewport_size != value:
                        page.set_viewport_size(value)
                        page.wait_for_timeout(300)

                def scale_and_center():
                    box = rect()
                    scale = math.sqrt((box['width'] / 1920) * (box['height'] / 1080))
                    return box, scale, box['x'] + box['width'] / 2, box['y'] + box['height'] / 2

                def main_point(dx, down):
                    _, scale, cx, cy = scale_and_center()
                    return cx + dx * scale, cy + down * scale

                def reaction_geometry():
                    box = rect()
                    compact = box['height'] < 500
                    width = min(340, box['width'] - 24)
                    top = 74 if compact else 180
                    return {'x': box['x'] + box['width'] - width - 12, 'y': box['y'] + top,
                            'width': width, 'height': min(260 if compact else 300, box['height'] - top - 12),
                            'buttonY': 157 if compact else 186}

                def image_and_pixel():
                    shot = page.screenshot()
                    image = Image.open(BytesIO(shot)).convert('RGB')
                    viewport = page.viewport_size
                    sx, sy = image.width / viewport['width'], image.height / viewport['height']

                    def pixel(x, y):
                        px, py = min(image.width - 1, max(0, round(x * sx))), min(image.height - 1, max(0, round(y * sy)))
                        return image.getpixel((px, py))
                    return image, pixel, sx, sy

                def probe():
                    image, pixel, sx, sy = image_and_pixel()
                    geo = reaction_geometry()
                    background = pixel(geo['x'] + 6, geo['y'] + 6)
                    dark = all(abs(background[i] - (11, 15, 21)[i]) <= 6 for i in range(3))
                    button = pixel(geo['x'] + 21, geo['y'] + geo['buttonY'] + 29)
                    red, green, blue = button
                    cleanup = green - red > 35 and green > 78 and blue > 55
                    guest = red > 65 and 42 < green < 85 and 12 < blue < 55 and red - green > 12
                    kind = 'C' if dark and cleanup else 'F' if dark and guest else None

                    # Ground truth for a result screen uses several opaque panel
                    # and stat-card pixels plus its gold heading, not elapsed time.
                    _, scale, cx, cy = scale_and_center()
                    backgrounds = [pixel(cx + dx * scale, cy + dy * scale) for dx, dy in
                                   [(-500, -360), (500, -360), (-500, 350), (500, 350)]]
                    panel = all(all(abs(rgb[i] - (11, 14, 24)[i]) <= 5 for i in range(3)) for rgb in backgrounds)
                    stats = [pixel(cx + dx * scale, cy - 3 * scale) for dx in [-445, 185]]
                    stat_cards = all(all(abs(rgb[i] - (26, 33, 48)[i]) <= 6 for i in range(3)) for rgb in stats)
                    gold = 0
                    if panel and stat_cards:
                        bounds = (round((cx - 400 * scale) * sx), round((cy - 355 * scale) * sy),
                                  round((cx + 400 * scale) * sx), round((cy - 310 * scale) * sy))
                        gold = sum(1 for r, g, b in image.crop(bounds).getdata() if r > 180 and g > 130 and 45 < b < 160 and r > g * 1.1)
                    return {'kind': kind, 'cardVisible': dark, 'background': background, 'button': button,
                            'result': panel and stat_cards and gold > 50}

                def receipt_clip():
                    _, scale, cx, cy = scale_and_center()
                    return {'x': cx - 480 * scale, 'y': cy - 320 * scale, 'width': 960 * scale, 'height': 525 * scale}

                def clock_clip():
                    box, scale, _, _ = scale_and_center()
                    return {'x': box['x'] + (541 - 64) * scale, 'y': box['y'] + (43 - 21) * scale,
                            'width': 128 * scale, 'height': 42 * scale}

                def clock_glyphs(png):
                    image = Image.open(BytesIO(png)).convert('RGB')
                    return bytes(1 if min(rgb) > 200 else 0 for rgb in image.getdata())

                def planning_visible():
                    _, pixel, _, _ = image_and_pixel()
                    box = rect()
                    samples = [pixel(box['x'] + 8, box['y'] + 12),
                               pixel(box['x'] + box['width'] / 2, box['y'] + 8),
                               pixel(box['x'] + 8, box['y'] + box['height'] - 100)]
                    return all(all(abs(rgb[i] - (245, 240, 224)[i]) <= 5 for i in range(3)) for rgb in samples)

                def playing_visible():
                    _, pixel, _, _ = image_and_pixel()
                    box = rect()
                    hud = pixel(box['x'] + box['width'] - 236, box['y'] + 118)
                    dark_hud = all(abs(hud[i] - (11, 15, 21)[i]) <= 6 for i in range(3))
                    clock = clock_glyphs(page.screenshot(clip=clock_clip()))
                    return dark_hud and sum(clock) > 20 and not planning_visible()

                def rapid_burst(kind, mode):
                    target = 10 if kind == 'C' else 8
                    if mode == 'keyboard':
                        for _ in range(target):
                            page.keyboard.press(kind.lower(), delay=35)
                            page.wait_for_timeout(80)
                    else:
                        geo = reaction_geometry()
                        for _ in range(target):
                            tap(geo['x'] + geo['width'] / 2, geo['y'] + geo['buttonY'] + 29, delay=115)
                    night['inputs'].append({'key': kind, 'method': mode, 'pressesSent': target,
                                            'viewport': page.viewport_size, 'atSeconds': round(time.monotonic() - night_started, 2)})

                try:
                    log(f'Loading natural random night {night_index}')
                    page.goto(args.url)
                    page.wait_for_function('window.simbarGameReadyAt > 0')
                    night['readyMs'] = page.evaluate('window.simbarGameReadyAt')
                    capture('01-title')
                    click(640, 678, delay=900)
                    box = rect()
                    click(box['x'] + 182, box['y'] + 121, delay=900)
                    capture('02-planning-no-event-trigger')
                    assert planning_visible(), 'The title start control did not open the daytime planning desk.'
                    for attempt in range(1, 4):
                        if not planning_visible():
                            break
                        box = rect()
                        click(box['x'] + box['width'] - 209, box['y'] + box['height'] - 45, delay=900)
                        if planning_visible():
                            capture(f'02-launch-retry-{attempt}')
                    assert not planning_visible(), 'The visible planning launch button did not open the guide after three real clicks.'
                    for step in range(1, 6):
                        if playing_visible():
                            break
                        click(*main_point(0, 235), delay=650)
                    assert playing_visible(), 'The guide did not reach a visible running night; refusing to test a stationary planning/guide screen.'
                    night_started = time.monotonic()
                    capture('03-natural-show-started')
                    page.wait_for_timeout(2000)
                    clock_before = page.screenshot(path=str(night_out / 'clock-before-H.png'), clip=clock_clip())
                    page.keyboard.press('h')
                    page.wait_for_timeout(2500)
                    capture('04-read-only-status-live-time')
                    clock_after = page.screenshot(path=str(night_out / 'clock-during-H.png'), clip=clock_clip())
                    before_glyphs, after_glyphs = clock_glyphs(clock_before), clock_glyphs(clock_after)
                    night['clockDigitsChangeWhileStatusOpen'] = any(before_glyphs) and any(after_glyphs) and before_glyphs != after_glyphs
                    assert night['clockDigitsChangeWhileStatusOpen'], 'The visible night clock did not advance while H status was open.'
                    page.keyboard.press('h')
                    page.wait_for_timeout(300)

                    if args.smoke:
                        while time.monotonic() - night_started < 30:
                            log('Smoke: show continues with spontaneous-event status; no manual trigger was used')
                            page.wait_for_timeout(min(10, 30 - (time.monotonic() - night_started)) * 1000)
                        capture('05-smoke-30-seconds')
                        night['smokeCompleted'] = True
                    else:
                        scheduled_shots = {60: False, 120: False, 180: False}
                        next_log = 0
                        saw_result = False
                        while time.monotonic() - night_started < 365:
                            elapsed = time.monotonic() - night_started
                            current = probe()
                            if 'firstProbe' not in night:
                                night['firstProbe'] = current
                            for at, saved in list(scheduled_shots.items()):
                                if not saved and elapsed >= at:
                                    capture(f'05-spontaneous-scene-{at:03d}s')
                                    scheduled_shots[at] = True
                            if elapsed >= next_log:
                                log(f'Night {night_index}: {elapsed:.0f}s real play, visible reaction={current["kind"]}, result={current["result"]}')
                                next_log = elapsed + 15
                            if current['result']:
                                saw_result = True
                                break
                            kind = current['kind']
                            if kind == 'F':
                                report['coverage']['guestPromptSeen'] = True
                                if not report['coverage']['guestTimeoutObserved']:
                                    set_viewport(DESKTOP)
                                    capture('06-first-guest-prompt')
                                    # One physical key-down is held, then released.
                                    # No further presses are sent until the round times out.
                                    page.keyboard.down('f')
                                    page.wait_for_timeout(800)
                                    page.keyboard.up('f')
                                    capture('07-guest-single-key-held-800ms')
                                    timeout_deadline = time.monotonic() + 8
                                    observed = False
                                    while time.monotonic() < timeout_deadline:
                                        waiting = probe()
                                        if waiting['cardVisible'] and waiting['kind'] is None:
                                            observed = True
                                            capture('08-guest-timeout-retry-feedback')
                                            break
                                        page.wait_for_timeout(200)
                                    report['coverage']['guestTimeoutObserved'] = observed
                                    night['timeoutHoldProbeCaptured'] = True
                                    page.wait_for_timeout(2000)
                                    if not observed:
                                        capture('08-timeout-not-detected-inspect-manually')
                                    continue
                                if guest_bursts == 0:
                                    set_viewport(DESKTOP)
                                    mode, label = 'keyboard', 'desktop-keyboard-F'
                                elif guest_bursts == 1:
                                    set_viewport(PORTRAIT)
                                    mode, label = 'touch', 'portrait-touch-F'
                                else:
                                    set_viewport(LANDSCAPE)
                                    mode, label = 'touch', 'landscape-touch-F'
                                capture(f'09-guest-{guest_bursts + 1}-{label}-before')
                                rapid_burst('F', mode)
                                report['coverage']['guestKeyboardBurstSent'] |= mode == 'keyboard'
                                report['coverage']['guestTouchBurstSent'] |= mode == 'touch'
                                capture(f'10-guest-{guest_bursts + 1}-{label}-after')
                                guest_bursts += 1
                                set_viewport(DESKTOP)
                            elif kind == 'C':
                                report['coverage']['cleanupPromptSeen'] = True
                                # Cleanup prompts arise only from actual spontaneous
                                # paper left on the floor and an employee's arrival.
                                view = PORTRAIT if cleanup_bursts % 2 == 0 else LANDSCAPE
                                set_viewport(view)
                                label = 'portrait' if view == PORTRAIT else 'landscape'
                                capture(f'11-cleanup-{cleanup_bursts + 1}-{label}-before')
                                rapid_burst('C', 'touch')
                                report['coverage']['cleanupTouchBurstSent'] = True
                                capture(f'12-cleanup-{cleanup_bursts + 1}-{label}-after')
                                cleanup_bursts += 1
                                set_viewport(DESKTOP)
                            else:
                                page.wait_for_timeout(450)
                        night['resultDetected'] = saw_result
                        if not saw_result:
                            capture('failure-no-result-within-365s')
                            raise AssertionError('The real night plus at most 185 seconds of closing did not reach the result screen; inspect screenshots/probes.')
                        set_viewport(DESKTOP)
                        page.wait_for_timeout(350)
                        capture('13-night-receipt')
                        original = page.screenshot(path=str(night_out / 'receipt-before-reload.png'), clip=receipt_clip())
                        night['receiptSha256Before'] = hashlib.sha256(original).hexdigest()
                        tap(*main_point(322, 257))
                        capture('14-completed-night-lobby')
                        page.reload()
                        page.wait_for_function('window.simbarGameReadyAt > 0')
                        tap(*main_point(-283, 369))
                        capture('15-receipt-after-reload')
                        restored = page.screenshot(path=str(night_out / 'receipt-after-reload.png'), clip=receipt_clip())
                        night['receiptSha256After'] = hashlib.sha256(restored).hexdigest()
                        night['savedReceiptPixelsUnchanged'] = original == restored
                        night['walletVisualEvidence'] = ['13-night-receipt.png', '15-receipt-after-reload.png']
                        assert night['savedReceiptPixelsUnchanged'], 'The saved result changed after reload, or its review control did not open the same receipt.'
                        night['completed'] = True
                    assert not night['errors'], night['errors']
                except Exception as error:
                    night['failure'] = repr(error)
                    try:
                        capture('failure')
                    except Exception:
                        pass
                    raise
                finally:
                    report['errors'].extend(night['errors'])
                    (night_out / 'run.json').write_text(json.dumps(night, ensure_ascii=False, indent=2), encoding='utf-8')
                    context.close()

                coverage = report['coverage']
                if args.smoke or (coverage['guestPromptSeen'] and coverage['cleanupPromptSeen']
                                  and coverage['guestTimeoutObserved'] and coverage['guestKeyboardBurstSent']
                                  and coverage['guestTouchBurstSent'] and coverage['cleanupTouchBurstSent']):
                    break
                log('This natural random night did not cover every scenario; starting another fresh profile.')

            report['completedNights'] = sum(1 for item in report['nights'] if item.get('completed'))
            report['completed'] = not report['errors'] and (args.smoke or (report['completedNights'] > 0 and all(report['coverage'].values())))
            if not report['completed']:
                report['incompleteCoverage'] = 'Native random events did not supply every requested scenario within the configured fresh nights. This is a QA coverage limit, not a claimed game failure.'
            log('Browser run finished. Inspect PNGs for paper effects, visible employees, courteous copy, mobile targets and wallet values.')
        finally:
            report['durationSeconds'] = round(time.monotonic() - began, 2)
            (out / 'run.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
            print(json.dumps(report, ensure_ascii=False), flush=True)
            browser.close()
    return 0 if report.get('completed') else 2


if __name__ == '__main__':
    raise SystemExit(main())
