"""Capture actual pixel-art gameplay and check live/paused renderer behavior."""
import argparse
import base64
import hashlib
import json
import os
from pathlib import Path

from playwright.sync_api import sync_playwright


def capture(args):
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    report = {"url": args.url, "errors": [], "checks": []}
    executable = sorted((Path(os.environ["LOCALAPPDATA"]) / "ms-playwright").glob(
        "chromium-*/chrome-win64/chrome.exe"), reverse=True)[0]
    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(headless=True, executable_path=str(executable))
        for label, viewport, mobile in [
            ("desktop", {"width": 1586, "height": 992}, False),
            ("mobile", {"width": 390, "height": 844}, True),
        ]:
            context = browser.new_context(viewport=viewport, locale="ko-KR",
                                          is_mobile=mobile, has_touch=mobile,
                                          device_scale_factor=2 if mobile else 1)
            page = context.new_page()
            page.on("pageerror", lambda error: report["errors"].append(str(error)))
            page.goto(args.url)
            page.wait_for_function("window.simbarGameReadyAt>0")
            assert page.evaluate("window.simbarRuntime") == "pixel-2d"
            report["checks"].append(label + ": real pixel runtime ready")
            page.screenshot(path=str(output / (label + "-title.png")))
            # Exercise the same public action dispatch used by the real controls.
            page.evaluate("""() => { const g=simbarGame; g.act('start');
                g.act('launch'); for(let n=0;n<3;n++)g.act('guide'); }""")
            page.wait_for_timeout(200)
            assert page.evaluate("simbarGame.phase") == "playing"
            page.screenshot(path=str(output / (label + "-game.png")))
            if not mobile:
                frame = lambda: page.evaluate('simbarApplication.canvas.toDataURL("image/png").split(",")[1]')
                before = frame()
                page.wait_for_timeout(900)
                after = frame()
                assert before != after, "Live people must animate and move."
                (output / "world.png").write_bytes(base64.b64decode(after))
                report["checks"].append("Live world changes with real actors")
                page.evaluate("simbarGame.act('pause')")
                page.wait_for_timeout(150)
                before = frame()
                page.wait_for_timeout(400)
                after = frame()
                assert before == after, "Paused world must remain frozen."
                report["checks"].append("Paused world remains frozen")
                page.evaluate("simbarGame.act('resume')")
                page.wait_for_timeout(100)
                report["resources"] = page.evaluate("""performance.getEntriesByType('resource')
                    .filter(r=>/world.js|sprites|venue/.test(r.name)).map(r=>r.name)""")
            else:
                page.set_viewport_size({"width": 844, "height": 390})
                page.wait_for_timeout(200)
                page.screenshot(path=str(output / "mobile-landscape-game.png"))
            context.close()
        browser.close()
    assert not report["errors"], report["errors"]
    report["artSha256"] = {name: hashlib.sha256((Path(__file__).resolve().parent /
        "WebGLTemplate/FastGame/art" / name).read_bytes()).hexdigest()
        for name in ("venue.png", "sprites.png", "venue.json", "sprites.json")}
    (output / "report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps({"output": str(output), "checks": report["checks"], "errors": report["errors"]}))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--url", default="http://127.0.0.1:9871/")
    parser.add_argument("--output", type=Path, default=Path("Research/PixelArtVerification"))
    capture(parser.parse_args())
