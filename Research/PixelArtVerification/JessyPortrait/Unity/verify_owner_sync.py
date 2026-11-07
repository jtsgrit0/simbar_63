"""Audit the scoped Jessy replacement while preserving the other owners and GUIDs."""
import hashlib
import json
from pathlib import Path
import sys
from PIL import Image

folder = Path(__file__).resolve().parent
root = folder.parents[3]
baseline_path = folder / 'asset-baseline.json'
protected = [
    'Assets/Art/StartupTextures/Owners/' + name + '.png' + suffix
    for name in ('OwnerFemale', 'OwnerFF', 'OwnerPetSounds')
    for suffix in ('.meta',)
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


def digest(path):
    return hashlib.sha256((root / path).read_bytes()).hexdigest()


if '--baseline' in sys.argv:
    assert not baseline_path.exists(), 'Do not overwrite the pre-replacement baseline'
    baseline_path.write_text(json.dumps({path: digest(path) for path in protected}, indent=2) + '\n', encoding='utf-8')
    print('Saved GUID and other-owner baseline for ' + str(len(protected)) + ' files')
    raise SystemExit(0)

baseline = json.loads(baseline_path.read_text(encoding='utf-8'))
assert all(digest(path) == sha for path, sha in baseline.items()), 'A protected owner file or GUID changed'
native = root / 'Tools/WebGLTemplate/FastGame/art/owner-female.png'
targets = [root / 'Assets/Art/StartupTextures/Owners/OwnerFemale.png', root / 'Assets/Resources/SimBarWebTextures/OwnerFemale.bytes']
reference = Image.open(native).convert('RGBA')
supplied = root / 'ArtSource/Owners/Supplied/Jessy-owner_p_j.PNG'
original = Image.open(supplied).convert('RGBA')
assert original.size == (1087, 1447)
expected = Image.new('RGBA', (400, 350), (23, 10, 23, 255))
expected.paste(original.resize((263, 350), Image.Resampling.NEAREST), (68, 0))
assert expected.tobytes() == reference.tobytes(), 'Runtime portrait is not the literal full-source nearest-neighbor contain'
records = []
for target in targets:
    image = Image.open(target).convert('RGBA')
    assert image.size == reference.size and image.tobytes() == reference.tobytes(), 'Jessy pixels differ: ' + str(target)
    records.append({'path': str(target.relative_to(root)), 'width': image.width, 'height': image.height, 'rgbaIdentical': True, 'byteIdentical': target.read_bytes() == native.read_bytes(), 'sha256': hashlib.sha256(target.read_bytes()).hexdigest()})
result = {'protectedOtherOwnerFilesAndMetas': len(protected), 'allProtectedFilesUnchanged': True, 'supplied': {'path': str(supplied.relative_to(root)), 'sha256': hashlib.sha256(supplied.read_bytes()).hexdigest(), 'width': original.width, 'height': original.height}, 'framing': {'resampling': 'NEAREST', 'fullSource': True, 'drawSize': [263, 350], 'position': [68, 0], 'sidebarsRGBA': [23, 10, 23, 255], 'exactRGBA': True}, 'native': {'path': str(native.relative_to(root)), 'width': reference.width, 'height': reference.height, 'sha256': hashlib.sha256(native.read_bytes()).hexdigest()}, 'unityCopies': records}
(folder / 'resource-integrity.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
print(json.dumps(result, indent=2))
