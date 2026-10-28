"""Exercise a real WebGL night through its visible controls (no game debug API).

uv run --with playwright python Tools/qa_floor_missions.py [URL]

This takes about five minutes. PNGs are the evidence for the Korean copy,
particle effects, employee movement, guest escort, mobile layout and cash value.
The saved result panel is also compared pixel-for-pixel after a browser reload.
The script does not infer wallet correctness from the result panel alone.
"""

import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import time

from playwright.sync_api import sync_playwright


class NightClock:
    """Wall time excluding the floor drawer's deliberate performance pause."""

    def __init__(self):
        self.elapsed = 0.0
        self.started = time.monotonic()

    def pause(self):
        if self.started is not None:
            self.elapsed += time.monotonic() - self.started
            self.started = None

    def resume(self):
        if self.started is None:
            self.started = time.monotonic()

    def seconds(self):
        return self.elapsed + (time.monotonic() - self.started if self.started is not None else 0.0)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('url', nargs='?', default='http://127.0.0.1:8767/')
    parser.add_argument('--headful', action='store_true')
    parser.add_argument('--smoke', action='store_true', help='Verify the deployed build and responsive mission controls without another full night.')
    args = parser.parse_args()
    out = Path('Library/FloorQa/live' if args.url.startswith('https:') else 'Library/FloorQa/local')
    out.mkdir(parents=True, exist_ok=True)
    report = {'url': args.url, 'screenshots': str(out), 'errors': [], 'stages': [], 'checks': {}}
    began = time.monotonic()

    def log(message):
        print(f'[{time.monotonic() - began:6.1f}s] {message}', flush=True)

    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(
            executable_path=os.environ.get('SIMBAR_QA_CHROME', 'C:/Users/Jtsgr/AppData/Local/ms-playwright/chromium-1243/chrome-win64/chrome.exe'),
            headless=not args.headful,
            args=['--disable-background-timer-throttling', '--disable-renderer-backgrounding'],
        )
        context = browser.new_context(viewport={'width': 1280, 'height': 800}, device_scale_factor=2, locale='ko-KR', has_touch=True)
        page = context.new_page()
        page.set_default_timeout(90000)
        page.on('pageerror', lambda error: report['errors'].append(str(error)))
        page.on('console', lambda message: report['errors'].append(message.text) if message.type == 'error' else None)

        def rect():
            value = page.locator('#unity-canvas').bounding_box()
            assert value and value['width'] > 0 and value['height'] > 0, 'Unity canvas must be visible.'
            return value

        def capture(name):
            page.screenshot(path=str(out / f'{name}.png'))
            report['stages'].append(name)
            log(f'Captured {name}')

        def tap(x, y, delay=350):
            page.touchscreen.tap(x, y)
            page.wait_for_timeout(delay)

        def main_point(dx, down):
            box = rect()
            scale = math.sqrt((box['width'] / 1920) * (box['height'] / 1080))
            return box['x'] + box['width'] / 2 + dx * scale, box['y'] + box['height'] / 2 + down * scale

        def floor_geometry():
            box = rect()
            width = min(420, box['width'] - 24)
            height = min(720, box['height'] - 24)
            header = 120 if width < 300 else 106
            return {
                'x': box['x'] + box['width'] - width - 12,
                'y': box['y'] + 12,
                'width': width,
                'height': height,
                'header': header,
                'viewport': height - header - 104,
            }

        def floor_button(card, index=0, scroll='top'):
            geo = floor_geometry()
            starts = {'performance': 0, 'cleanup': 298, 'guest': 638}
            travel = max(0, 1068 - geo['viewport'])
            offset = {'top': 0, 'cleanup': min(298, travel), 'bottom': travel}[scroll]
            if card == 'performance':
                by, bh = (136 if index == 0 else 187), 43
            elif card == 'cleanup':
                by, bh = 214, 45
            else:
                by, bh = 220 + index * 64, 58
            return geo['x'] + geo['width'] / 2, geo['y'] + geo['header'] + starts[card] - offset + by + bh / 2

        def scroll_floor_bottom():
            geo = floor_geometry()
            page.mouse.move(geo['x'] + geo['width'] / 2, geo['y'] + geo['header'] + geo['viewport'] / 2)
            for _ in range(4):
                page.mouse.wheel(0, 5000)
                page.wait_for_timeout(250)

        def wait_night(clock, target):
            while clock.seconds() < target:
                remaining = target - clock.seconds()
                log(f'Night running: about {clock.seconds():.0f}s active; next check at {target:.0f}s')
                page.wait_for_timeout(min(remaining, 20) * 1000)

        def receipt_clip():
            # This area lies entirely inside the opaque, static result card.
            box = rect()
            scale = math.sqrt((box['width'] / 1920) * (box['height'] / 1080))
            cx, cy = box['x'] + box['width'] / 2, box['y'] + box['height'] / 2
            return {'x': cx - 480 * scale, 'y': cy - 320 * scale, 'width': 960 * scale, 'height': 525 * scale}

        try:
            log('Loading a fresh Korean browser profile')
            page.goto(args.url)
            page.wait_for_function('window.simbarGameReadyAt > 0')
            report['readyMs'] = page.evaluate('window.simbarGameReadyAt')
            capture('01-title')
            tap(640, 678)
            box = rect()
            # The fresh planning desk opens on Posters. Switch to booking so its
            # footer opens a community mic instead of researching a poster.
            tap(box['x'] + 182, box['y'] + 121)
            capture('02-day-planning')
            tap(1060, 755)
            for _ in range(3):
                tap(*main_point(0, 235))
            clock = NightClock()
            capture('03-show-started')

            for number, effect in enumerate([0, 1, 0, 1], start=1):
                clock.pause()
                page.keyboard.press('h')
                page.wait_for_timeout(350)
                if number == 1:
                    capture('04-performance-drawer')
                tap(*floor_button('performance', effect), delay=120)
                clock.resume()
                capture(f'05-effect-{number}-' + ('confetti' if effect == 0 else 'tissue'))
                page.wait_for_timeout(450)
            clock.pause()
            page.keyboard.press('h')
            page.wait_for_timeout(350)
            capture('06-both-effects-used-twice')
            page.keyboard.press('h')
            page.wait_for_timeout(300)
            clock.resume()
            report['checks']['stageEffectControlsExercised'] = ['confetti', 'tissue', 'confetti', 'tissue']

            if args.smoke:
                page.keyboard.press('h')
                page.wait_for_timeout(350)
                page.set_viewport_size({'width': 393, 'height': 852})
                page.wait_for_timeout(500)
                capture('smoke-mobile-performance-controls')
                scroll_floor_bottom()
                capture('smoke-mobile-guest-controls')
                assert not report['errors'], report['errors']
                report['completed'] = True
                report['checks']['responsiveMissionSmoke'] = True
                return

            wait_night(clock, 73)
            clock.pause()
            page.keyboard.press('h')
            page.wait_for_timeout(350)
            scroll_floor_bottom()
            capture('07-guest-step-one-desktop')
            # The final step is deliberately attempted first: no progress should
            # be granted. The image is retained for visual sequence validation.
            tap(*floor_button('guest', 2, 'bottom'))
            capture('08-guest-wrong-order-feedback')
            page.set_viewport_size({'width': 393, 'height': 852})
            page.wait_for_timeout(500)
            capture('09-mobile-drawer-after-resize')
            scroll_floor_bottom()
            capture('10-mobile-guest-actions')
            for choice in range(3):
                tap(*floor_button('guest', choice, 'bottom'))
                capture(f'11-mobile-guest-action-{choice + 1}')
            clock.resume()
            page.set_viewport_size({'width': 1280, 'height': 800})
            page.wait_for_timeout(500)
            capture('12-staff-escort-started')
            page.wait_for_timeout(6500)
            capture('13-guest-departure')
            report['checks']['courteousGuestControlsExercised'] = ['wrong-final-step', 'offer-water', 'polite-request', 'belongings-and-escort']

            wait_night(clock, 184)
            capture('14-after-show-cleanup-assignment')
            tap(*floor_button('cleanup', scroll='cleanup'))
            capture('15-employees-walking-to-clean')
            page.wait_for_timeout(12000)
            capture('16-cleanup-before-reload')
            log('Reloading while employees are cleaning')
            page.reload()
            page.wait_for_function('window.simbarGameReadyAt > 0')
            capture('17-saved-after-show-lobby')
            tap(640, 678)
            capture('18-restored-cleanup-drawer')
            page.keyboard.press('h')
            page.wait_for_timeout(350)
            resumed = time.monotonic()
            for target in [5, 18, 35, 55, 75, 95, 115, 135, 150]:
                while time.monotonic() - resumed < target:
                    remaining = target - (time.monotonic() - resumed)
                    log(f'Watching resumed employee work: next evidence at {target}s')
                    page.wait_for_timeout(min(remaining, 20) * 1000)
                capture(f'19-cleanup-watch-{target:03d}s')
            capture('20-night-receipt')
            original_receipt = page.screenshot(path=str(out / 'receipt-before-reload.png'), clip=receipt_clip())
            report['receiptSha256Before'] = hashlib.sha256(original_receipt).hexdigest()
            # Return to lobby, reload the persisted receipt, and review it again.
            tap(*main_point(322, 257))
            capture('21-lobby-after-completion')
            page.reload()
            page.wait_for_function('window.simbarGameReadyAt > 0')
            tap(*main_point(-283, 369))
            capture('22-receipt-after-reload')
            restored_receipt = page.screenshot(path=str(out / 'receipt-after-reload.png'), clip=receipt_clip())
            report['receiptSha256After'] = hashlib.sha256(restored_receipt).hexdigest()
            report['checks']['savedReceiptPixelsUnchanged'] = original_receipt == restored_receipt
            report['checks']['walletReviewEvidence'] = ['20-night-receipt.png', '22-receipt-after-reload.png']
            assert report['checks']['savedReceiptPixelsUnchanged'], 'Saved receipt changed after reload, or the expected result screen was not reached. Inspect evidence.'
            assert not report['errors'], report['errors']
            report['completed'] = True
            log('Visible-control QA completed; inspect the PNGs for effect, motion, copy and wallet values.')
        except Exception as error:
            report['completed'] = False
            report['failure'] = repr(error)
            try:
                capture('failure')
            except Exception:
                pass
            raise
        finally:
            report['durationSeconds'] = round(time.monotonic() - began, 2)
            (out / 'run.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
            print(json.dumps(report, ensure_ascii=False), flush=True)
            context.close()
            browser.close()


if __name__ == '__main__':
    main()
