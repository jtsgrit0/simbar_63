"""Audit the approved uniform Jessy crop and unchanged owner resources/GUIDs."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
from PIL import Image

folder = Path(__file__).resolve().parent
root = folder.parents[3]
baseline_path = folder / 'asset-baseline.json'
protected = [
    'Assets/Art/StartupTextures/Owners/' + name + '.png.meta'
    for name in ('OwnerFemale', 'OwnerFF', 'OwnerPetSounds')
] + [
    'Assets/Resources/SimBarWebTextures/' + name + '.bytes.meta'
    for name in ('OwnerFemale', 'OwnerFF', 'OwnerPetSounds')
] + [
    'Assets/Art/StartupTextures/Owners/' + name + '.png'
    for name in ('OwnerFF', 'OwnerPetSounds')
] + [
    'Assets/Resources/SimBarWebTextures/' + name + '.bytes'
    for name in ('OwnerFF', 'OwnerPetSounds')
] + [
    'Tools/WebGLTemplate/FastGame/art/' + name + '.png'
    for name in ('owner-ff', 'owner-pet')
]


def digest(data):
    return hashlib.sha256(data).hexdigest()


if '--baseline' in sys.argv:
    assert not baseline_path.exists(), 'Do not overwrite the pre-copy baseline'
    hashes = {path: digest((root / path).read_bytes()) for path in protected}
    for path, sha in hashes.items():
        committed = subprocess.check_output(['git', 'show', 'HEAD:' + path], cwd=root)
        assert digest(committed) == sha, 'Protected asset already differs from HEAD: ' + path
    baseline_path.write_text(json.dumps(hashes, indent=2) + '\n', encoding='utf-8')
    print('Saved HEAD-matching baseline for ' + str(len(protected)) + ' protected owner/meta files')
    raise SystemExit(0)

baseline = json.loads(baseline_path.read_text(encoding='utf-8'))
assert all(digest((root / path).read_bytes()) == sha for path, sha in baseline.items()), 'Protected owner or GUID changed'
native = root / 'Tools/WebGLTemplate/FastGame/art/owner-female.png'
supplied = root / 'ArtSource/Owners/Supplied/Jessy-owner_p_j.PNG'
source = Image.open(supplied).convert('RGBA')
assert source.size == (1087, 1447) and digest(supplied.read_bytes()) == 'a27e380f6743eb2cf3359e2e593cff12f63f5dbc371de112d6e0196924775961'
expected = source.crop((3, 280, 1083, 1225)).resize((400, 350), Image.Resampling.NEAREST)
reference = Image.open(native).convert('RGBA')
assert reference.size == expected.size and reference.tobytes() == expected.tobytes(), 'Runtime is not the literal approved uniform source crop'
assert digest(native.read_bytes()) == '8d85760c18009360078d98158b03d2eae5cb423dde3cdb8cea13fb1105de3fa0'
records = []
for path in ('Assets/Art/StartupTextures/Owners/OwnerFemale.png', 'Assets/Resources/SimBarWebTextures/OwnerFemale.bytes'):
    target = root / path
    image = Image.open(target).convert('RGBA')
    assert image.size == reference.size and image.tobytes() == reference.tobytes(), 'Unity Jessy pixels differ: ' + path
    records.append({'path': path, 'width': image.width, 'height': image.height, 'rgbaIdentical': True, 'byteIdentical': target.read_bytes() == native.read_bytes(), 'sha256': digest(target.read_bytes())})
result = {'protectedOtherOwnerFilesAndMetas': len(protected), 'allProtectedFilesUnchanged': True, 'supplied': {'path': str(supplied.relative_to(root)), 'sha256': digest(supplied.read_bytes()), 'size': list(source.size)}, 'framing': {'crop': [3, 280, 1083, 1225], 'cropSize': [1080, 945], 'uniformAspect': '8:7', 'resampling': 'NEAREST', 'outputSize': [400, 350], 'exactRGBA': True, 'letterboxing': False}, 'nativeSHA256': digest(native.read_bytes()), 'unityCopies': records}
(folder / 'resource-integrity.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
print(json.dumps(result, indent=2))
