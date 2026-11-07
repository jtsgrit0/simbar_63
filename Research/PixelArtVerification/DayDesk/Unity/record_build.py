"""Record final explicit Reference build output and all four compiled artifact hashes."""
import hashlib
import json
from pathlib import Path
import subprocess

folder = Path(__file__).resolve().parent
root = folder.parents[3]
unity = str(Path.home() / 'AppData/Local/Unity/bin/unity.exe')
process = subprocess.run([unity, 'command', 'build_status', '--port', '7800', '--json'], cwd=root, capture_output=True, text=True, encoding='utf-8')
payload = json.loads(process.stdout)
assert payload['success'], payload
result = payload['data']['result']
if result['status'] != 'completed':
    print(json.dumps(result, indent=2))
    raise SystemExit(1)
(folder / 'build-status.json').write_text(json.dumps(payload, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
assert result['result'] == 'Succeeded' and result['totalErrors'] == 0, result
artifacts = root / 'Builds/Pages/SimBarPlayerWebGL/Build'
files = []
for path in sorted(artifacts.iterdir()):
    if path.is_file() and path.name.endswith(('.framework.js', '.wasm', '.data', '.loader.js')):
        files.append({'name': path.name, 'bytes': path.stat().st_size, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})
assert len(files) == 4
summary = {key: result[key] for key in ('buildId', 'result', 'platform', 'outputPath', 'buildTimeMs', 'totalErrors', 'totalWarnings')}
summary.update({'scene': 'Assets/Scenes/SimBar_Reference.unity', 'options': ['DetailedBuildReport'], 'profile': None, 'files': files})
(folder / 'build-final-summary.json').write_text(json.dumps(summary, indent=2) + '\n', encoding='utf-8')
print(json.dumps(summary, indent=2))
