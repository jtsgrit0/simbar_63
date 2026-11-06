import hashlib
import json
from pathlib import Path

folder = Path(__file__).resolve().parent
root = folder.parents[3]
art = root / 'Tools/WebGLTemplate/FastGame/art'
owners = []
for filename, name in [('owner-female.png', 'OwnerFemale'), ('owner-ff.png', 'OwnerFF'), ('owner-pet.png', 'OwnerPetSounds')]:
    source = (art / filename).read_bytes()
    startup = (root / 'Assets/Art/StartupTextures/Owners' / (name + '.png')).read_bytes()
    resource = (root / 'Assets/Resources/SimBarWebTextures' / (name + '.bytes')).read_bytes()
    assert source == startup == resource
    owners.append({'name': name, 'bytes': len(source), 'sha256': hashlib.sha256(source).hexdigest(), 'startupAndResourceExact': True})
native = json.loads((art / 'venue.json').read_text(encoding='utf-8'))
exported = json.loads((root / 'Assets/Resources/SimBarPixelArt/Presentation.json').read_text(encoding='utf-8'))
assert native['barRegion']['offset'] == [0, 17]
assert exported['barRegion']['offset'] == {'x': 0, 'y': 17}
report = {'owners': owners, 'barOffsetNative': native['barRegion']['offset'], 'barOffsetUnity': exported['barRegion']['offset']}
(folder / 'export-audit.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
print(json.dumps(report, indent=2))
