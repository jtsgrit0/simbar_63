"""Focused browser verification of real Canvas after-show choreography.

Uses normal public actions, advances the normal three-minute performance through
the public tick method, then records the after-show with the real RAF loop.
"""
import argparse
import base64
import json
from pathlib import Path
import time

from playwright.sync_api import sync_playwright
from benchmark_web_loading import chromium_executable

PROJECT = Path(__file__).resolve().parent.parent
STATE = """() => {
 const g=window.simbarGame,a=g.journey.afterShow;
 return {phase:g.phase,paused:g.paused,elapsed:g.elapsed,secondsLeft:g.journey.secondsLeft,
  session:g.journey.session,settledSession:g.journey.settledSession,money:g.state.money,
  xp:g.journey.xp,completedNights:g.journey.completedNights,floor:g.floor,
  escort:g.escort,crew:g.crew,
  floorTasksPending:g.floorTasksPending,afterShowPending:g.afterShowPending,staffReturnPending:g.staffReturnPending,
  afterShow:a,actors:g.actors.filter(x=>x.role==='band'||x.role==='bartender'||x.role==='server'||x.id.startsWith('hired-')||x._partner||x.id==='assisted-guest'||a?.audience?.some(f=>f.id===x.id))};
}"""


def run(args):
    out = args.output.resolve()
    out.mkdir(parents=True, exist_ok=True)
    report = {"url": args.url, "errors": [], "failedRequests": [], "httpErrors": [],
              "consoleErrors": [], "timeline": [], "screenshots": [], "assertions": {}}
    with sync_playwright() as pw:
        opts = {"headless": True}
        executable = chromium_executable(None)
        if executable:
            opts["executable_path"] = executable
        browser = pw.chromium.launch(**opts)
        report["browserVersion"] = browser.version
        ctx = browser.new_context(viewport={"width": 1440, "height": 1000},
                                  device_scale_factor=1, locale="ko-KR",
                                  record_video_dir=str(out / "video"),
                                  record_video_size={"width": 1440, "height": 1000})
        page = ctx.new_page()
        page.on("pageerror", lambda e: report["errors"].append(str(e)))
        page.on("requestfailed", lambda r: report["failedRequests"].append({"url": r.url, "failure": r.failure}))
        page.on("response", lambda r: report["httpErrors"].append({"url": r.url, "status": r.status}) if r.status >= 400 else None)
        page.on("console", lambda m: report["consoleErrors"].append(m.text) if m.type == "error" else None)

        def ready():
            page.wait_for_function("window.simbarGameReadyAt>0", timeout=60000)

        def observe_movement():
            # Observe each real simulation tick, including RAF; no actor state,
            # route, choreographic phase or clock is assigned by this observer.
            page.evaluate("""() => {
              const g=window.simbarGame,tick=g.tick.bind(g);
              window.simbarMovementQA={violations:[],escortReturns:[],ticks:0,phases:{}};
              window.simbarContactFrames={};
              g.tick=dt=>{
                const before=new Map(g.actors.map(a=>[a.id,{x:a.x,z:a.z}])),escort=g.escort,phase=g.phase;
                tick(dt);const qa=window.simbarMovementQA;qa.ticks++;
                for(const m of g.journey.afterShow?.members||[]){const phases=qa.phases[m.id]??=[];if(!phases.includes(m.phase))phases.push(m.phase);}
                for(const a of g.actors){const p=before.get(a.id);if(!p)continue;
                  const step=Math.hypot(a.x-p.x,a.z-p.z);
                  if(step>dt*3+.035)qa.violations.push({id:a.id,phase,dt,step});}
                if(escort?.phase==='return'&&!g.escort){
                  const w=g.actors.find(a=>a.id===escort.worker),cleaning=g.crew.includes(w.id)&&g.floor.cleanupActive;
                  const goal=cleaning?g._cleanGoal(g.crew.indexOf(w.id)):{x:escort.homeX,z:escort.homeZ};
                  qa.escortReturns.push({worker:w.id,goal,x:w.x,z:w.z,distance:Math.hypot(w.x-goal.x,w.z-goal.z)});
                }
              };
              const app=window.simbarApplication,render=app.world.render.bind(app.world);
              app.world.render=(...args)=>{const result=render(...args),c=app.canvas,b=c.getBoundingClientRect();
                for(const m of g.journey.afterShow?.members||[]){
                  if(!['highfive','hug'].includes(m.phase))continue;
                  const p=m.timer/(m.phase==='highfive'?1.1:1.5),key=m.id+'-'+m.phase;
                  if(p<.45||p>.7||window.simbarContactFrames[key])continue;
                  window.simbarContactFrames[key]={data:c.toDataURL('image/png').split(',')[1],width:c.width,height:c.height,
                    actors:g.actors.map(a=>{const p=app.world.worldToScreen(a.x,a.z,a.y||0);return{...a,canvasPoint:{x:p.x*c.width/b.width,y:p.y*c.height/b.height}};})};
                }return result;};
            }""")

        def harvest_contacts():
            frames = page.evaluate("() => window.simbarContactFrames || {}")
            for key, frame in frames.items():
                label = 'gesture-' + key
                (out / (label + '-canvas.png')).write_bytes(base64.b64decode(frame.pop('data')))
                (out / (label + '-frame.json')).write_text(json.dumps(frame, ensure_ascii=False, indent=2), encoding='utf-8')
            report.setdefault('actualRenderedContacts', []).extend(frames.keys())

        def capture(label, screen=True):
            path = out / (label + ".png")
            canvas = out / (label + "-canvas.png")
            frame = page.evaluate("""() => {
              const app=window.simbarApplication,c=app.canvas,b=c.getBoundingClientRect();
              return {data:c.toDataURL('image/png').split(',')[1],width:c.width,height:c.height,
                actors:app.g.actors.map(a=>{const p=app.world.worldToScreen(a.x,a.z,a.y||0);
                  return {...a,canvasPoint:{x:p.x*c.width/b.width,y:p.y*c.height/b.height}};})};
            }""")
            canvas.write_bytes(base64.b64decode(frame.pop("data")))
            (out / (label + "-frame.json")).write_text(json.dumps(frame, ensure_ascii=False, indent=2), encoding="utf-8")
            if screen:
                page.screenshot(path=str(path))
            report["screenshots"].append({"label": label, "page": str(path), "canvas": str(canvas)})

        page.goto(args.url, wait_until="domcontentloaded")
        ready()
        report["release"] = page.evaluate("() => [...document.scripts].find(s=>s.type==='module').src.split('/').at(-2)")
        if args.fixture:
            raw = args.fixture.read_text(encoding="utf-8")
            assert page.evaluate("raw=>window.simbarGame.importSave(raw)", raw), "Fixture import failed"
            page.reload(wait_until="domcontentloaded")
            ready()
            page.locator("button[data-action='continue']").click()
            report["fixture"] = str(args.fixture.resolve())
        else:
            page.evaluate("() => {const g=window.simbarGame;g.act('start');g.act('launch');while(g.phase==='guide')g.act('guide');}")
        schedule = page.evaluate("() => window.simbarGame.reaction")
        # A naturally random night can omit either floor task. Only then create
        # a seeded normal night through the same core and save API, never by
        # assigning missions, actor positions, clocks or choreography phases.
        if not args.fixture and (not schedule["paperCount"] or not schedule["guestScheduled"]):
            report["seededFixture"] = True
            page.evaluate("""async () => {
              const src=[...document.scripts].find(s=>s.type==='module').src;
              const {SimBarGame}=await import(new URL('core.js',src));
              for(let seed=1;seed<200;seed++){
                const mem=new Map(),storage={getItem:k=>mem.get(k)||null,setItem:(k,v)=>mem.set(k,v)};
                const g=new SimBarGame({seed,storage});g.act('start');g.act('launch');while(g.phase==='guide')g.act('guide');
                if(g.reaction.paperCount&&g.reaction.guestScheduled){
                  window.simbarGame.importSave(g.exportSave());return seed;
                }
              }
              throw new Error('No floor-event QA seed found');
            }""")
            page.reload(wait_until="domcontentloaded")
            ready()
            page.locator("button[data-action='continue']").click()
        else:
            report["seededFixture"] = False
        report["schedule"] = page.evaluate("() => window.simbarGame.reaction")
        report["initialAfterShow"] = page.evaluate("() => window.simbarGame.journey.afterShow")
        observe_movement()
        report["movement"] = []
        capture("01-performance")
        report["performance"] = page.evaluate(STATE)
        report["publicFastForward"] = page.evaluate("""() => {
          const g=window.simbarGame;let ticks=0;
          while(g.phase==='playing'&&ticks<4000){g.tick(.05);ticks++;}
          return {ticks,dt:.05,state:g.phase,secondsLeft:g.journey.secondsLeft};
        }""")
        page.wait_for_timeout(50)
        capture("02-stage-descent")
        seen = {x: set() for x in ("singer", "guitarist", "bassist", "drummer")}
        # The two initial screenshot readbacks may consume a saved member's
        # remaining stage step. Its initial phase was already actually observed.
        if report["initialAfterShow"]:
            for member in report["initialAfterShow"]["members"]:
                seen[member["id"]].add(member["phase"])
        gesture_captures = set()
        transitions = {}
        paused_once = False
        keys = {"c": 0, "f": 0}
        started = time.perf_counter()
        while time.perf_counter() - started < args.max_seconds:
            state = page.evaluate(STATE)
            after = state["afterShow"]
            if after:
                for member in after["members"]:
                    mid, phase = member["id"], member["phase"]
                    seen[mid].add(phase)
                    if transitions.get(mid) != phase:
                        report["timeline"].append({"wallSeconds": round(time.perf_counter()-started, 3),
                                                   "member": mid, "phase": phase, "state": state})
                        transitions[mid] = phase
                    if not paused_once and phase == "highfive" and member["timer"] >= .2:
                        # Pause immediately. GPU screenshot readback must not
                        # consume the rest of this short gesture first.
                        page.keyboard.press("p")
                        before = page.evaluate(STATE)
                        raw = page.evaluate("() => window.simbarGame.exportSave()")
                        (out / "mid-gesture-save.json").write_text(raw, encoding="utf-8")
                        capture("03-gesture-paused")
                        page.wait_for_timeout(500)
                        still = page.evaluate(STATE)
                        active = [m for m in before["afterShow"]["members"] if m["phase"] == "highfive" and 0 < m["timer"] < 1.1]
                        report["assertions"]["pauseDuringActualGesture"] = bool(active)
                        report["assertions"]["pauseFreezesGesture"] = before["afterShow"] == still["afterShow"]
                        report["movement"].append(page.evaluate("() => window.simbarMovementQA"))
                        harvest_contacts()
                        page.reload(wait_until="domcontentloaded")
                        ready()
                        restored = page.evaluate(STATE)
                        report["assertions"]["reloadPreservesChoreography"] = before["afterShow"] == restored["afterShow"]
                        page.locator("button[data-action='continue']").click()
                        report["pauseResume"] = {"before": before, "restored": restored, "resumed": page.evaluate(STATE)}
                        observe_movement()
                        paused_once = True
                        break
                    progress = member["timer"] / (1.1 if phase == "highfive" else 1.5)
                    if phase in ("highfive", "hug") and .45 <= progress <= .7:
                        key = (mid, phase)
                        if key not in gesture_captures:
                            gesture_captures.add(key)
                            report.setdefault("contacts", []).append({"member": mid, "phase": phase, "state": state})
                if state["afterShowPending"] and state["settledSession"] == state["session"]:
                    report["assertions"]["receiptWaitsForBandExit"] = False
            if state["phase"] == "results":
                report["final"] = state
                break
            kind = page.evaluate("() => window.simbarGame.reaction.activeKind")
            if kind:
                key = "c" if kind == 1 else "f"
                page.keyboard.press(key)
                keys[key] += 1
            page.wait_for_timeout(80)

        report["wallSeconds"] = round(time.perf_counter()-started, 3)
        report["seenPhases"] = {k: sorted(v) for k, v in seen.items()}
        report["keys"] = keys
        report["final"] = page.evaluate(STATE)
        (out / "final-save.json").write_text(page.evaluate("() => window.simbarGame.exportSave()"), encoding="utf-8")
        capture("04-final")
        final = report["final"]
        report["movement"].append(page.evaluate("() => window.simbarMovementQA"))
        harvest_contacts()
        for movement in report['movement']:
            for member, phases in movement.get('phases', {}).items():
                seen[member].update(phases)
        report['seenPhases'] = {k: sorted(v) for k, v in seen.items()}
        report["assertions"].setdefault("receiptWaitsForBandExit", True)
        if report["initialAfterShow"]:
            expected = {}
            for member in report["initialAfterShow"]["members"]:
                sequence = ["stage", "approach", "highfive", "hug", "exit", "gone"]
                if member["greeting"] >= 1 and member["phase"] == "approach":
                    sequence.remove("highfive")
                expected[member["id"]] = sequence[sequence.index(member["phase"]):]
            report["expectedRemainingPhases"] = expected
            report["assertions"]["allSavedRemainingChoreographyFinished"] = all(set(v).issubset(seen[k]) for k,v in expected.items())
        else:
            report["assertions"]["allFourWalkGreetHugExit"] = all(
                {"stage", "approach", "highfive", "hug", "exit", "gone"}.issubset(v) for v in seen.values())
        report["assertions"]["resultsReached"] = final["phase"] == "results"
        report["assertions"]["allFourInvisibleAtExit"] = all(a.get("visible") is False for a in final["actors"] if a["role"] == "band")
        report["audienceReturnDistances"] = [{"id": fan["id"], "phase": fan["phase"],
            "distance": ((actor["x"]-fan["homeX"])**2+(actor["z"]-fan["homeZ"])**2)**.5}
            for fan in (final["afterShow"] or {}).get("audience", [])
            for actor in final["actors"] if actor["id"] == fan["id"]]
        report["assertions"]["allFansActuallyReturnBeforeReceipt"] = bool(report["audienceReturnDistances"]) and all(
            fan["phase"] == "done" and fan["distance"] <= .22 for fan in report["audienceReturnDistances"])
        report['staffReturnDistances'] = [{"id": actor['id'], "distance": ((actor['x']-actor['_cleanupParkGoal']['x'])**2+(actor['z']-actor['_cleanupParkGoal']['z'])**2)**.5}
            for actor in final['actors'] if actor['id'] in final['crew'] and actor.get('_cleanupParkGoal')]
        report['assertions']['staffActuallyParkBeforeReceipt'] = not final['staffReturnPending'] and all(a['distance'] <= .051 for a in report['staffReturnDistances'])
        report["assertions"]["continuousAfterShowMovementWithoutTeleport"] = not any(v["phase"] == "closing" for m in report["movement"] for v in m["violations"])
        report["assertions"]["continuousMovementWithoutTeleport"] = not any(m["violations"] for m in report["movement"])
        returned = [r for m in report["movement"] for r in m["escortReturns"]]
        report["assertions"]["staffReachAssignedReturnGoal"] = all(r["distance"] <= .22 for r in returned)
        report["assertions"]["guestExitAndReturnFinished"] = final["escort"] is None and not any(a["id"] == "assisted-guest" and a.get("visible") for a in final["actors"])
        report["assertions"]["scheduledFloorTasksHandled"] = bool(
            (not report["schedule"]["paperCount"] or final["floor"]["cleanupComplete"])
            and (not report["schedule"]["guestScheduled"] or final["floor"]["guestComplete"]))
        if report["schedule"]["paperCount"] and report["schedule"]["guestScheduled"]:
            report["assertions"]["randomCAndFHandled"] = bool(keys["c"] and keys["f"] and final["floor"]["cleanupComplete"] and final["floor"]["guestComplete"])
        report["assertions"]["noAutoSpawnButton"] = page.locator("button[data-action='spawnGuest'],button[data-action='paper'],button[data-action='confetti']").count() == 0
        if final["phase"] == "results":
            receipt_before = {k: final[k] for k in ("money", "xp", "completedNights", "settledSession")}
            page.reload(wait_until="domcontentloaded")
            ready()
            page.locator("button[data-action='continue']").click()
            page.wait_for_timeout(200)
            receipt_after = page.evaluate(STATE)
            report["assertions"]["receiptIdempotentAfterReload"] = all(receipt_after[k] == v for k,v in receipt_before.items())
            report["receiptReload"] = receipt_after
        report["assertions"]["zeroJsErrors"] = not report["errors"] and not report["consoleErrors"]
        report["assertions"]["noMissingAssets"] = not report["failedRequests"] and not report["httpErrors"]
        video = page.video
        ctx.close()
        report["video"] = str(video.path())
        browser.close()
    (out / "aftershow-browser-qa.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"release": report.get("release"), "wallSeconds": report["wallSeconds"],
                      "assertions": report["assertions"], "errors": report["errors"],
                      "report": str(out / "aftershow-browser-qa.json")}, ensure_ascii=False), flush=True)
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--url", default="http://127.0.0.1:9882/")
    parser.add_argument("--output", type=Path, default=PROJECT / "Research/PixelArtVerification/V3/aftershow")
    parser.add_argument("--max-seconds", type=float, default=110)
    parser.add_argument("--fixture", type=Path, help="Resume an actual exported QA save through normal storage/continue.")
    run(parser.parse_args())
