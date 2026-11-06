"""Run the frozen C# regression against the actual connected Unity Editor."""
import json
from pathlib import Path
import subprocess

folder = Path(__file__).resolve().parent
root = folder.parents[3]
unity = str(Path.home() / "AppData/Local/Unity/bin/unity.exe")
results = []

def invoke(command, *args):
    call = subprocess.run([unity, "command", command, "--port", "7800", "--json", "--", *args], cwd=root, capture_output=True, text=True, encoding="utf-8")
    payload = json.loads(call.stdout)
    result = payload.get("data", {}).get("result", {})
    if not payload.get("success") or isinstance(result, dict) and result.get("success") is False:
        raise RuntimeError(call.stdout)
    value = result.get("result", result) if isinstance(result, dict) else result
    results.append({"command": command, "entry": args[1] if len(args) > 1 else None, "result": value})
    return value

def qa(name, file="PixelV4QA"):
    result = invoke("run_script", str(folder.relative_to(root) / (file + ".cs")), file + "." + name)
    print(name + ": " + json.dumps(result, ensure_ascii=False), flush=True)
    return result

invoke("editor_play")
visual = qa("VisualContract")
assert visual["performanceFrames"] >= 4 and visual["lightsVisible"]
qa("Begin")
assert qa("HighFive")["reached"]
assert qa("CaptureHighFive")["pixelDistance"] <= 21
assert qa("Hug")["reached"]
assert qa("CaptureHug")["pixelDistance"] <= 16
queue = qa("QueueCheckpoint")
assert queue["reached"] and queue["onePhysicalWalker"]
assert all(qa("PauseContract").values())
resume = qa("Resume")
assert all(resume[key] for key in ("samePositions", "sameAudience", "sameElapsed", "sameReturningFan")) and resume["returningFan"]
complete = qa("Complete")
assert all(complete[key] for key in ("pendingGate", "bandOnlyGate", "allGone", "allFans", "allFansAtHome", "completed", "settled", "onlyOnce")) and complete["phase"] == "Results"
blocked = qa("BlockedStation", "PixelCleanupConcurrencyQA")
assert blocked["heldHomeClearance"] >= .449 and blocked["cachedStable"] and blocked["readyWorkers"] > 0 and blocked["qteBudgetUnchanged"] and blocked["cleaned"] == 0
assert qa("HiredParkReservation", "PixelCleanupConcurrencyQA")["clear"]
returned = qa("MidRouteCompletion", "PixelCleanupConcurrencyQA")
assert all(returned[key] for key in ("heldForPhysicalReturn", "allReturned", "physicallyMoved", "noTeleports", "settled")) and returned["phase"] == "Results"
invoke("editor_stop")
qa("Restore")
preserved = qa("PreservationContract")
assert preserved["savedValueRestored"] and not preserved["sceneDirty"] and not preserved["playing"]
(folder / "final-editor-validation.json").write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding="utf-8")
print("FINAL EDITOR VALIDATION PASSED", flush=True)
