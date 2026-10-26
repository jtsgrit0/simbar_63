"""Measure the deployed SimBar player with a clean browser cache, then reload it.

Example (requires Playwright and an installed Chromium):
  uv run --with playwright python Tools/benchmark_web_loading.py \
      --url https://jtsgrit0.github.io/simbar_63/ --label live

No network throttling is applied unless --mbps or --latency-ms is supplied.
Unity's first frame can still contain its splash screen. For an actual-game
target, use --require-game-ready with a runtime signal that sets
window.simbarGameReadyAt = performance.now() after rendering the real UI.
"""

import argparse
from collections import Counter
import json
import os
from pathlib import Path
import time

from playwright.sync_api import sync_playwright


PROJECT = Path(__file__).resolve().parent.parent
RESPONSE_HEADERS = {
    "content-type", "content-encoding", "content-length", "cache-control",
    "age", "etag", "last-modified", "server", "x-cache", "x-cache-hits",
    "x-served-by", "via", "vary", "accept-ranges",
}


def chromium_executable(override):
    if override:
        return str(Path(override).resolve())
    cache = Path(os.environ.get("LOCALAPPDATA", "")) / "ms-playwright"
    candidates = sorted(cache.glob("chromium-*/chrome-win64/chrome.exe"), reverse=True)
    return str(candidates[0]) if candidates else None


def snapshot_metrics(page):
    return page.evaluate("""() => {
        try { return window.simbarBenchmarkInstance?.GetMetricsInfo?.() || null; }
        catch (error) { return {error: String(error)}; }
    }""")


def run_benchmark(args):
    args.output.parent.mkdir(parents=True, exist_ok=True)
    result = {
        "url": args.url,
        "networkMbps": args.mbps or None,
        "networkLatencyMs": args.latency_ms,
        "targetMs": args.target_ms,
        "samples": [],
    }
    with sync_playwright() as playwright:
        launch_options = {"headless": True}
        executable = chromium_executable(args.chromium)
        if executable:
            launch_options["executable_path"] = executable
        browser = playwright.chromium.launch(**launch_options)
        result["browserVersion"] = browser.version
        for sample_number in range(1, args.samples + 1):
            # Each pair starts with no HTTP cache, Cache API, IndexedDB or cookies.
            context = browser.new_context(viewport={"width": 1280, "height": 800}, locale="ko-KR")
            page = context.new_page()
            page.set_default_timeout(args.timeout_ms)
            cdp = context.new_cdp_session(page)
            cdp.send("Network.enable")
            if args.mbps or args.latency_ms:
                throughput = args.mbps * 1000000 / 8 if args.mbps else -1
                cdp.send("Network.emulateNetworkConditions", {
                    "offline": False, "latency": args.latency_ms,
                    "downloadThroughput": throughput, "uploadThroughput": throughput,
                })
            requests, errors, failures, console = {}, [], [], []

            def request_started(event):
                requests[event["requestId"]] = {
                    "url": event["request"]["url"], "bytes": 0,
                    "startedAt": event["timestamp"], "method": event["request"]["method"],
                }

            def response_received(event):
                item = requests.get(event["requestId"])
                if item is None:
                    return
                response = event["response"]
                item.update({
                    "status": response["status"], "responseAt": event["timestamp"],
                    "diskCache": response.get("fromDiskCache", False),
                    "serviceWorker": response.get("fromServiceWorker", False),
                    "protocol": response.get("protocol"),
                    "timing": response.get("timing"),
                    "headers": {key.lower(): value for key, value in response.get("headers", {}).items()
                                if key.lower() in RESPONSE_HEADERS},
                })

            def request_finished(event):
                item = requests.get(event["requestId"])
                if item is not None:
                    item.update({"bytes": event["encodedDataLength"], "finishedAt": event["timestamp"]})
                    item["durationMs"] = round((event["timestamp"] - item["startedAt"]) * 1000)

            cdp.on("Network.requestWillBeSent", request_started)
            cdp.on("Network.responseReceived", response_received)
            cdp.on("Network.loadingFinished", request_finished)
            page.on("pageerror", lambda error: errors.append(str(error)))
            page.on("requestfailed", lambda request: failures.append({"url": request.url, "failure": request.failure}))
            page.on("console", lambda message: console.append({"type": message.type, "text": message.text})
                    if message.type in ("error", "warning") or "UnityCache" in message.text else None)

            sample = {"sample": sample_number, "runs": []}
            result["samples"].append(sample)
            for mode in ("cold", "warm"):
                requests.clear()
                errors.clear()
                failures.clear()
                console.clear()
                started = time.perf_counter()
                page.goto(args.url, wait_until="domcontentloaded", timeout=args.timeout_ms)
                page.wait_for_function("window.simbarPlayerReady !== undefined", timeout=args.timeout_ms)
                ready = page.evaluate("""() => Promise.race([
                    window.simbarPlayerReady.then(instance => {
                        window.simbarBenchmarkInstance = instance;
                        return {readyMs: performance.now(), metrics: instance.GetMetricsInfo?.() || null};
                    }),
                    new Promise((_, reject) => setTimeout(() => reject(new Error('Player startup timed out')), 120000))
                ])""")
                ready_wall_ms = round((time.perf_counter() - started) * 1000)
                prefix = args.output.parent / f"{args.label}-{sample_number}-{mode}"
                # GPU readback for a screenshot can stall WebGL's first render.
                # Observe readiness before taking any screenshots.
                page.wait_for_function("""() => {
                    try { return window.simbarBenchmarkInstance?.GetMetricsInfo?.().pageLoadTimeToFrame1 > 0; }
                    catch (_) { return false; }
                }""", timeout=args.timeout_ms)
                if args.require_game_ready:
                    page.wait_for_function("typeof window.simbarGameReadyAt === 'number' && window.simbarGameReadyAt > 0",
                                           timeout=args.timeout_ms)
                game_ready_ms = page.evaluate("() => typeof window.simbarGameReadyAt === 'number' ? window.simbarGameReadyAt : null")
                metrics = snapshot_metrics(page)
                page.screenshot(path=str(prefix) + "-ready.png")
                page.screenshot(path=str(prefix) + "-first-frame.png")
                # Keep a settled screenshot for older players without a runtime
                # signal. It is visual evidence, not a timing assertion.
                page.wait_for_timeout(args.settle_ms)
                page.screenshot(path=str(prefix) + "-settled.png")
                navigation = page.evaluate("() => performance.getEntriesByType('navigation')[0]?.toJSON()")
                resources = page.evaluate("() => performance.getEntriesByType('resource').map(item => item.toJSON())")
                first_frame_ms = metrics.get("pageLoadTimeToFrame1") if metrics else None
                run = {
                    "run": mode, "readyMs": ready_wall_ms, "browserReadyMs": round(ready["readyMs"]),
                    "firstFrameMs": first_frame_ms,
                    "gameReadyMs": round(game_ready_ms) if game_ready_ms is not None else None,
                    "gameReadyVerified": game_ready_ms is not None,
                    "meetsTarget": game_ready_ms <= args.target_ms if game_ready_ms is not None else None,
                    "firstFrameMeetsTarget": first_frame_ms is not None and first_frame_ms <= args.target_ms,
                    "metricsAtReady": ready["metrics"], "metricsAtFirstFrame": metrics,
                    "totalTransferBytes": sum(int(item["bytes"]) for item in requests.values()),
                    "errors": list(errors), "failedRequests": list(failures),
                    "consoleCounts": dict(Counter(item["type"] for item in console)),
                    "consoleErrorCounts": dict(Counter(item["text"] for item in console if item["type"] == "error")),
                    "console": list(console), "requests": list(requests.values()),
                    "navigation": navigation, "resources": resources,
                    "playerState": page.locator("#game-shell").get_attribute("data-player-state"),
                    "screenshots": {"ready": str(prefix) + "-ready.png", "firstFrame": str(prefix) + "-first-frame.png",
                                    "settled": str(prefix) + "-settled.png"},
                }
                sample["runs"].append(run)
                args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
                print(json.dumps({"sample": sample_number, **{key: run[key] for key in (
                    "run", "readyMs", "firstFrameMs", "gameReadyMs", "meetsTarget", "totalTransferBytes", "playerState", "errors")}},
                    ensure_ascii=False), flush=True)
            # The title's start button at this fixed reference viewport opens the real game.
            page.mouse.click(640, 678)
            page.wait_for_timeout(2000)
            page.screenshot(path=str(args.output.parent / f"{args.label}-{sample_number}-game.png"))
            context.close()
        browser.close()
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--url", required=True)
    parser.add_argument("--label", default="live")
    parser.add_argument("--output", type=Path, default=PROJECT / "Temp/perf/live.json")
    parser.add_argument("--chromium", help="Optional Chromium executable; otherwise use installed Playwright Chromium.")
    parser.add_argument("--samples", type=int, default=1)
    parser.add_argument("--mbps", type=float, default=0)
    parser.add_argument("--latency-ms", type=float, default=0)
    parser.add_argument("--timeout-ms", type=int, default=180000)
    parser.add_argument("--target-ms", type=int, default=3000)
    parser.add_argument("--settle-ms", type=int, default=5000,
                        help="Extra screenshot delay for visual verification; never counted as a readiness result.")
    parser.add_argument("--require-game-ready", action="store_true",
                        help="Require the game's post-render simbarGameReadyAt signal instead of claiming the splash is playable.")
    args = parser.parse_args()
    if args.samples < 1 or args.mbps < 0 or args.latency_ms < 0:
        parser.error("Samples must be positive; network limits cannot be negative.")
    result = run_benchmark(args)
    runs = [run for sample in result["samples"] for run in sample["runs"]]
    if any(run["errors"] or run["playerState"] != "ready" for run in runs):
        raise SystemExit(2)
    if args.require_game_ready and any(not run["meetsTarget"] for run in runs):
        raise SystemExit(1)


if __name__ == "__main__":
    main()
