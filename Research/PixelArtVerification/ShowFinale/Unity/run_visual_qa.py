import json
from pathlib import Path
import subprocess
import sys

folder = Path(__file__).resolve().parent
root = folder.parents[3]
unity = str(Path.home() / 'AppData/Local/Unity/bin/unity.exe')
names = sys.argv[1:] or ['Orders', 'OrderLifecycle', 'BeginClosing', 'GreetingBarrier', 'MidExitSave', 'Farewell', 'ClearedFloor', 'Acknowledge', 'PendingCleanup', 'LegacyMigration']
results = json.loads((folder / 'validation.json').read_text(encoding='utf-8')) if sys.argv[1:] and (folder / 'validation.json').exists() else []


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
    value = invoke('run_script', str(folder.relative_to(root) / 'ShowFinaleQA.cs'), 'ShowFinaleQA.' + name)
    results[:] = [entry for entry in results if entry['entry'] != name]
    results.append({'entry': name, 'result': value})
    print(name + ': ' + json.dumps(value, ensure_ascii=False), flush=True)
    return value


qa('Backup')
try:
    invoke('editor_play')
    invoke('editor_focus')
    for name in names:
        if name == 'Farewell':
            for attempt in range(25):
                value = qa(name)
                if not value.get('pending'):
                    break
            else:
                raise RuntimeError('Physical departure did not finish in 500 simulated seconds; inspect departure-progress.json')
        else:
            qa(name)
finally:
    invoke('editor_stop')
    preserved = qa('Restore')
    assert preserved['prefsRestored'] and preserved['gameViewRestored'] and not preserved['sceneDirty'] and not preserved['playing'] and not preserved['runInBackground']
    (folder / 'validation.json').write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding='utf-8')

print('ORDER BUBBLES / MUSIC / GROUP DEPARTURE / SAVE / FAREWELL QA PASSED', flush=True)
