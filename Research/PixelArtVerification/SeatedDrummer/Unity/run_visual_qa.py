import json
from pathlib import Path
import subprocess

folder=Path(__file__).resolve().parent
root=folder.parents[3]
unity=str(Path.home()/"AppData/Local/Unity/bin/unity.exe")
results=[]

def invoke(command,*args):
    process=subprocess.run([unity,"command",command,"--port","7800","--json","--",*args],cwd=root,capture_output=True,text=True,encoding="utf-8")
    payload=json.loads(process.stdout)
    if not payload.get("success"):raise RuntimeError(process.stdout)
    result=payload["data"]["result"]
    if isinstance(result,dict) and result.get("success") is False:raise RuntimeError(process.stdout)
    return result.get("result",result) if isinstance(result,dict) else result

def qa(name):
    value=invoke("run_script",str(folder.relative_to(root)/"SeatingParityQA.cs"),"SeatingParityQA."+name)
    results.append({"entry":name,"result":value})
    print(name+": "+json.dumps(value,ensure_ascii=False),flush=True)
    return value

try:
    assert len(qa("Owners")["portraits"])==3
    scene=qa("Performance")
    assert scene["seatCount"]==11 and scene["globalLayers"]==17
    assert all(scene[key] for key in ("anchors","depths","shadows","masks","drummerBehindKit"))
    assert all(seat["worldPositionUnchanged"] for seat in scene["seats"])
    assert all(qa("StandingGuard").values())
    assert all(qa("Pause").values())
    qa("BeginAfterShow")
    assert qa("DrummerBehind")["reached"]
    assert qa("DrummerFront")["reached"]
    assert qa("DrummerFloor")["reached"]
    finished=qa("FinishAfterShow")
    assert finished["phase"]=="Results" and all(finished[key] for key in ("complete","allBandGone","allFansHome"))
finally:
    invoke("editor_stop")
    preserved=qa("Restore")
    assert preserved["prefsRestored"] and not preserved["sceneDirty"] and not preserved["playing"] and not preserved["runInBackground"]
    (folder/"validation.json").write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding="utf-8")

print("SEATING / DRUMMER / OWNER VISUAL QA PASSED",flush=True)
