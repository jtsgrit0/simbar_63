"""Refresh only the three approved Jessy texture streams, preserving every importer."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
from PIL import Image

FOLDER = Path(__file__).resolve().parent
ROOT = FOLDER.parents[3]
PAIRS = [
    ('Tools/WebGLTemplate/FastGame/art/owner-female.png', 'Assets/Art/StartupTextures/Owners/OwnerFemale.png'),
    ('Tools/WebGLTemplate/FastGame/art/owner-female.png', 'Assets/Resources/SimBarWebTextures/OwnerFemale.bytes'),
    ('Tools/WebGLTemplate/FastGame/art/office/desk-jessy.png', 'Assets/Resources/SimBarOffice/desk-jessy.bytes'),
]
UNCHANGED = [
    'Tools/WebGLTemplate/FastGame/art/owner-ff.png',
    'Tools/WebGLTemplate/FastGame/art/owner-pet.png',
    'Tools/WebGLTemplate/FastGame/art/office/desk-edison.png',
    'Tools/WebGLTemplate/FastGame/art/office/desk-jaiden.png',
    'Assets/Art/StartupTextures/Owners/OwnerFF.png',
    'Assets/Art/StartupTextures/Owners/OwnerPetSounds.png',
    'Assets/Resources/SimBarWebTextures/OwnerFF.bytes',
    'Assets/Resources/SimBarWebTextures/OwnerPetSounds.bytes',
    'Assets/Resources/SimBarOffice/desk-edison.bytes',
    'Assets/Resources/SimBarOffice/desk-jaiden.bytes',
]
sha = lambda path: hashlib.sha256((ROOT / path).read_bytes()).hexdigest()

parser = argparse.ArgumentParser()
parser.add_argument('--baseline', action='store_true')
parser.add_argument('--copy', action='store_true')
parser.add_argument('--selector-sha')
parser.add_argument('--desk-sha')
args = parser.parse_args()
baseline_file = FOLDER / 'asset-baseline.json'
if args.baseline:
    assert not baseline_file.exists(), 'Keep the original baseline intact.'
    protected = UNCHANGED + [target + '.meta' for _, target in PAIRS]
    baseline_file.write_text(json.dumps({p: sha(p) for p in protected}, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'baseline': len(protected)}))
elif args.copy:
    assert args.selector_sha and args.desk_sha, 'Require both root-approved frozen source hashes.'
    assert sha(PAIRS[0][0]) == args.selector_sha
    assert sha(PAIRS[2][0]) == args.desk_sha
    with Image.open(ROOT / PAIRS[0][0]) as image:
        assert image.size == (400, 350)
    with Image.open(ROOT / PAIRS[2][0]) as image:
        assert image.size == (640, 360)
    baseline = json.loads(baseline_file.read_text(encoding='utf-8'))
    assert all(sha(p) == expected for p, expected in baseline.items()), 'Protected files changed before this scoped refresh.'
    for source, target in PAIRS:
        shutil.copyfile(ROOT / source, ROOT / target)
    assert all(sha(p) == expected for p, expected in baseline.items()), 'A protected file changed during refresh.'
    records = [{'source': source, 'target': target, 'bytes': (ROOT / target).stat().st_size, 'sha256': sha(target), 'byteIdentical': sha(source) == sha(target)} for source, target in PAIRS]
    (FOLDER / 'asset-copy.json').write_text(json.dumps({'copies': records, 'allOtherOwnerImageStreamsUnchanged': True, 'allThreeTargetImporterBytesUnchanged': True}, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(records, indent=2))
else:
    baseline = json.loads(baseline_file.read_text(encoding='utf-8'))
    assert all(sha(p) == expected for p, expected in baseline.items())
    print(json.dumps({'protectedFilesUnchanged': len(baseline)}))
