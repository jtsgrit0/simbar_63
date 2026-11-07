import json
from pathlib import Path
import subprocess
import sys

folder = Path(__file__).resolve().parent
root = folder.parents[3]
unity = str(Path.home() / 'AppData/Local/Unity/bin/unity.exe')
results = []


def invoke(command, *args):
    process = subprocess.run([unity, 'command', command, '--port', '7800', '--json', '--', *args], cwd=root, capture_output=True, text=True, encoding='utf-8')
    payload = json.loads(process.stdout)
    if not payload.get('success'):
        raise RuntimeError(process.stdout)
    result = payload['data']['result']
    if isinstance(result, dict) and result.get('success') is False:
        raise RuntimeError(process.stdout)
    return result.get('result', result) if isinstance(result, dict) else result


def qa(name):
    value = invoke('run_script', str(folder.relative_to(root) / 'StageScreenQA.cs'), 'StageScreenQA.' + name)
    results.append({'entry': name, 'result': value})
    print(name + ': ' + json.dumps(value, ensure_ascii=False), flush=True)
    return value


qa('Backup')
try:
    invoke('editor_play')
    invoke('editor_focus')
    for name in sys.argv[1:] or ['Startup', 'Performance', 'Track0', 'Track1', 'Track2', 'Track3', 'Track4', 'FitFixtures', 'Pause', 'Closing', 'ClosingTrack']:
        qa(name)
finally:
    invoke('editor_stop')
    preserved = qa('Restore')
    assert preserved['prefsRestored'] and preserved['gameViewRestored'] and not preserved['sceneDirty'] and not preserved['playing'] and not preserved['runInBackground']
    (folder / 'validation.json').write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding='utf-8')

print('STAGE LIGHTING / AUTHENTIC LED FIT / EXISTING PLAYLIST QA PASSED', flush=True)
