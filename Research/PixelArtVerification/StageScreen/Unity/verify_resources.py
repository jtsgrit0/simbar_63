"""Verify that Unity uses the approved native jacket streams without re-encoding."""
import hashlib
import json
from pathlib import Path

folder = Path(__file__).resolve().parent
root = folder.parents[3]
source = root / 'Tools/WebGLTemplate/FastGame/art'
dest = root / 'Assets/Resources/SimBarAlbumJackets'
manifest = json.loads((source / 'album-covers.json').read_text(encoding='utf-8'))
tracks = json.loads((dest / 'Tracks.json').read_text(encoding='utf-8'))['tracks']
assert len(tracks) == 5
records = []
seen = set()
for entry in manifest['tracks']:
    assert tracks[entry['index']]['resource'] == 'SimBarAlbumJackets/' + Path(entry['cover']).stem
    if entry['cover'] in seen:
        continue
    seen.add(entry['cover'])
    original = (source / entry['cover']).read_bytes()
    exported = (dest / (Path(entry['cover']).stem + '.bytes')).read_bytes()
    assert original == exported
    records.append({'source': entry['cover'], 'bytes': len(original), 'sha256': hashlib.sha256(original).hexdigest(), 'byteIdentical': True})
result = {'tracks': tracks, 'uniqueCovers': records, 'totalBytes': sum(record['bytes'] for record in records)}
(folder / 'resource-integrity.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
print(json.dumps(result, indent=2))
