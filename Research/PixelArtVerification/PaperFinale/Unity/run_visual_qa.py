import json
from pathlib import Path
import subprocess
import sys

folder = Path(__file__).resolve().parent
root = folder.parents[3]
unity = str(Path.home() / 'AppData/Local/Unity/bin/unity.exe')
results = []
concurrent_only = '--concurrent-only' in sys.argv
validation_name = 'concurrency-validation.json' if concurrent_only else 'validation.json'


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
    value = invoke('run_script', str(folder.relative_to(root) / 'PaperFinaleQA.cs'), 'PaperFinaleQA.' + name)
    results.append({'entry': name, 'result': value})
    (folder / validation_name).write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding='utf-8')
    print(name + ': ' + json.dumps(value, ensure_ascii=False), flush=True)
    return value


qa('Backup')
try:
    rules = qa('Rules')
    assert rules['passed'] > 30 and not rules['failures']
    invoke('editor_play')
    invoke('editor_focus')
    qa('BeginClosing')
    qa('PauseAndReplay')
    for _ in range(15):
        progress = qa('ConcurrentChunk')
        if progress['captured']:
            break
    assert progress['seenC'] and progress['concurrent'] and progress['captured'], progress
    qa('SaveResume')
    if concurrent_only:
        for _ in range(8):
            half = qa('HalfCleanChunk')
            if half['half']:
                break
        assert half['half'], half
    else:
        for _ in range(25):
            finished = qa('FinishChunk')
            if finished['results']:
                break
        assert finished['results'], finished
finally:
    invoke('editor_stop')
    preserved = qa('Restore')
    assert preserved['prefsRestored'] and preserved['gameViewRestored'] and not preserved['sceneDirty'] and not preserved['playing'] and not preserved['runInBackground']

print('REAL FINALE PAPER / CONCURRENT C CLEANUP AND GREETINGS / SAVE / POPUP REMOVAL QA PASSED', flush=True)
