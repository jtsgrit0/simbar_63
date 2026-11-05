"""Measure cold/warm mobile game readiness under a declared LTE-like profile.

This is Chromium mobile emulation, not a physical LTE phone. It waits for the
post-render game signal, checks the actual native engine and records every
network response; it does not treat a loading shell as an interactive game.
Requires Playwright: uv run --with playwright python Tools/benchmark_mobile_lte.py
  --url http://127.0.0.1:8765/ --samples 3 --output Temp/perf/mobile-lte.json
"""

import argparse
import json
from pathlib import Path
import re
import time

from playwright.sync_api import sync_playwright

from benchmark_web_loading import chromium_executable


PROJECT = Path(__file__).resolve().parent.parent
USER_AGENT = ("Mozilla/5.0 (Linux; Android 14; Pixel 7) AppleWebKit/537.36 "
              "(KHTML, like Gecko) Chrome/145.0.0.0 Mobile Safari/537.36")


def run_benchmark(args):
    args.output.parent.mkdir(parents=True, exist_ok=True)
    result = {"url": args.url, "profile": {
        "viewport": {"width": 390, "height": 844}, "deviceScaleFactor": 2,
        "isMobile": True, "hasTouch": True, "cpuThrottlingRate": args.cpu_rate,
        "downloadMbps": args.mbps, "uploadMbps": args.mbps, "networkLatencyMs": args.latency_ms,
        "hardware": "Desktop Chromium synthetic mobile emulation; not a physical LTE phone",
        "userAgent": USER_AGENT}, "targetMs": args.target_ms, "samples": []}
    with sync_playwright() as playwright:
        options = {"headless": True}
        executable = chromium_executable(args.chromium)
        if executable:
            options["executable_path"] = executable
        browser = playwright.chromium.launch(**options)
        result["browserVersion"] = browser.version
        for number in range(1, args.samples + 1):
            context = browser.new_context(viewport={"width": 390, "height": 844},
                                          device_scale_factor=2, is_mobile=True, has_touch=True,
                                          locale="ko-KR", user_agent=USER_AGENT)
            page = context.new_page()
            page.set_default_timeout(args.timeout_ms)
            cdp = context.new_cdp_session(page)
            cdp.send("Network.enable")
            cdp.send("Network.emulateNetworkConditions", {
                "offline": False, "latency": args.latency_ms,
                "downloadThroughput": args.mbps * 1000000 / 8,
                "uploadThroughput": args.mbps * 1000000 / 8})
            cdp.send("Emulation.setCPUThrottlingRate", {"rate": args.cpu_rate})
            requests, errors, failures = {}, [], []

            def started(event):
                requests[event["requestId"]] = {"url": event["request"]["url"], "bytes": 0,
                                                 "startedAt": event["timestamp"]}

            def response(event):
                entry = requests.setdefault(event["requestId"], {"url": event["response"]["url"], "bytes": 0})
                data = event["response"]
                entry.update({"status": data["status"], "diskCache": data.get("fromDiskCache", False),
                              "serviceWorker": data.get("fromServiceWorker", False), "protocol": data.get("protocol")})

            def finished(event):
                if event["requestId"] in requests:
                    requests[event["requestId"]]["bytes"] = event["encodedDataLength"]

            cdp.on("Network.requestWillBeSent", started)
            cdp.on("Network.responseReceived", response)
            cdp.on("Network.loadingFinished", finished)
            page.on("pageerror", lambda error: errors.append(str(error)))
            page.on("requestfailed", lambda request: failures.append({"url": request.url, "failure": request.failure}))
            sample = {"sample": number, "runs": []}
            result["samples"].append(sample)
            for mode in ("cold", "warm"):
                requests.clear()
                errors.clear()
                failures.clear()
                start = time.perf_counter()
                page.goto(args.url, wait_until="domcontentloaded", timeout=args.timeout_ms)
                runtime_module = page.locator('script[type="module"][src$="/boot.js"]').get_attribute("src")
                if args.expected_release and not re.search(
                        r"(?:^|/)FastGame/" + re.escape(args.expected_release) + r"/boot\.js$", runtime_module or ""):
                    raise RuntimeError(f"Expected native release {args.expected_release}, served {runtime_module!r}")
                page.wait_for_function("(typeof window.simbarGameReadyAt === 'number' && window.simbarGameReadyAt > 0) || document.querySelector('#game-shell')?.dataset.playerState === 'error'")
                if page.locator("#game-shell").get_attribute("data-player-state") == "error":
                    message = page.locator("#error-message").inner_text()
                    page.screenshot(path=str(args.output.parent / f"{args.label}-{number}-{mode}-error.png"))
                    raise RuntimeError("Game startup failed: " + message)
                observed = round((time.perf_counter() - start) * 1000)
                state = page.evaluate("""() => ({
                    gameReadyMs: window.simbarGameReadyAt,
                    runtime: window.simbarRuntime,
                    enginePresent: typeof window.simbarGame?.act === 'function' && typeof window.simbarGame?.tick === 'function',
                    phase: window.simbarGame?.phase,
                    canvas: (()=>{const c=document.querySelector('#unity-canvas');return c?{width:c.width,height:c.height}:null})(),
                    metrics: window.simbarBenchmarkInstance?.GetMetricsInfo?.() || null,
                    resources: performance.getEntriesByType('resource').map(entry=>entry.toJSON()),
                    navigation: performance.getEntriesByType('navigation')[0]?.toJSON()
                })""")
                shot = args.output.parent / f"{args.label}-{number}-{mode}.png"
                page.screenshot(path=str(shot))
                run = {"run": mode, "observedWallMs": observed, "runtimeModule": runtime_module, **state,
                       "totalTransferBytes": sum(int(entry["bytes"]) for entry in requests.values()),
                       "requests": list(requests.values()), "errors": list(errors), "failedRequests": list(failures),
                       "playerState": page.locator("#game-shell").get_attribute("data-player-state"),
                       "screenshot": str(shot)}
                run["meetsTarget"] = (run["gameReadyMs"] <= args.target_ms and run["enginePresent"]
                                      and run["playerState"] == "ready" and not run["errors"] and not run["failedRequests"])
                sample["runs"].append(run)
                args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
                print(json.dumps({"sample": number, **{key: run[key] for key in (
                    "run", "gameReadyMs", "totalTransferBytes", "meetsTarget", "errors")}}), flush=True)
            # Exercise an actual owner/start control after both startup samples.
            # This proof is excluded from load timing and cannot prefetch a warm run.
            start_button = page.locator('[data-action="start"]').first
            if start_button.is_visible():
                start_button.click()
                page.wait_for_function("window.simbarGame?.phase === 'planning'")
                sample["interactionProof"] = {"clicked": "start", "phase": "planning"}
                page.screenshot(path=str(args.output.parent / f"{args.label}-{number}-interactive.png"))
                args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
            context.close()
        browser.close()
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--url", required=True)
    parser.add_argument("--output", type=Path, default=PROJECT / "Temp/perf/mobile-lte.json")
    parser.add_argument("--label", default="mobile-lte")
    parser.add_argument("--expected-release", help="Reject a stale or wrong runtime hash before measuring.")
    parser.add_argument("--chromium")
    parser.add_argument("--samples", type=int, default=3)
    parser.add_argument("--mbps", type=float, default=10)
    parser.add_argument("--latency-ms", type=float, default=70)
    parser.add_argument("--cpu-rate", type=float, default=4)
    parser.add_argument("--target-ms", type=float, default=3000)
    parser.add_argument("--timeout-ms", type=int, default=120000)
    args = parser.parse_args()
    if args.samples < 1 or args.mbps <= 0 or args.latency_ms < 0 or args.cpu_rate < 1:
        parser.error("Samples/bandwidth must be positive; latency nonnegative; CPU rate at least 1.")
    if args.expected_release and not re.fullmatch(r"[0-9a-f]{16}", args.expected_release):
        parser.error("Expected release must be a sixteen-character lowercase hexadecimal content hash.")
    result = run_benchmark(args)
    if any(not run["meetsTarget"] for sample in result["samples"] for run in sample["runs"]):
        raise SystemExit(1)


if __name__ == "__main__":
    main()
