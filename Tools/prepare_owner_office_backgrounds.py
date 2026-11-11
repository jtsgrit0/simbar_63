"""Pack the three retained owner-specific office illustrations without repainting.

Run with --sync-unity to copy the same encoded PNG streams into Unity Resources.
Only full-frame nearest-neighbor downsampling and lossless PNG encoding are used.
"""
import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / 'ArtSource/DayDesk/V2'
RUNTIME = ROOT / 'Tools/WebGLTemplate/FastGame/art/office'
NAMES = ('jessy', 'edison', 'jaiden')
GENERATED = (
    'exec-67ab158c-0258-40fe-990a-9d73c8a6165d.png',
    'exec-76ce0a56-91e2-471c-9ee6-2589354eaccc.png',
    'exec-94e59476-f4f5-4923-b5e1-e8dbe20f4fd1.png',
)
GENERATED_ROOT = 'C:/Users/Jtsgr/.codex/generated_images/01a1c87c-c1ce-7401-b532-0435c18ec1ea/'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def prepare_owner_office_backgrounds(output=RUNTIME, sync_unity=False):
    output = Path(output)
    if sync_unity and output.resolve() != RUNTIME.resolve():
        raise ValueError('Unity sync requires the canonical native office output directory')
    previous = json.loads((SOURCE / 'previous-manifest.json').read_text(encoding='utf-8'))
    layout = json.loads((SOURCE / 'office-layout.json').read_text(encoding='utf-8'))
    sources = [SOURCE / f'desk-{name}.v2.source.png' for name in NAMES]
    # Validate every image before replacing any runtime illustration.
    for path in sources:
        with Image.open(path) as image:
            if abs(image.width / image.height - 16 / 9) > .01:
                raise ValueError(f'Expected a complete 16:9 office illustration: {path}')
    output.mkdir(parents=True, exist_ok=True)
    assets = []
    for index, (name, path) in enumerate(zip(NAMES, sources)):
        old = previous['assets'][index]
        with Image.open(path) as image:
            size = list(image.size)
            packed = image.convert('RGB').resize((640, 360), Image.Resampling.NEAREST)
        target = output / f'desk-{name}.png'
        packed.save(target, optimize=True)
        prompt = SOURCE / f'desk-{name}.v2.prompt.md'
        reference = ROOT / 'ArtSource/Owners/Backdrops' / (name.title() + '.png')
        assets.append({
            'index': index, 'key': name, 'ownerId': old['ownerId'],
            'source': path.relative_to(ROOT).as_posix(), 'sourceSha256': sha(path),
            'sourceSize': size, 'generatedSource': GENERATED_ROOT + GENERATED[index],
            'prompt': prompt.relative_to(ROOT).as_posix(), 'promptSha256': sha(prompt),
            'reference': reference.relative_to(ROOT).as_posix(), 'referenceSha256': sha(reference),
            'editTarget': old['runtime'], 'editTargetSha256': old['runtimeSha256'],
            'runtime': (RUNTIME / target.name).relative_to(ROOT).as_posix(),
            'runtimeSize': [640, 360], 'runtimeSha256': sha(target),
            'runtimeBytes': target.stat().st_size,
            'processing': 'Complete-source nearest-neighbor resize; no crop, repaint, recolor or palette quantization',
            'pixelsExactToNearestResize': True, 'singleOwnerOnly': True,
            'identityReview': old['identityReview'], 'geometry': layout[name],
        })
    manifest = {
        **previous, 'version': 2,
        'workflow': 'Three separate environment edits of each approved owner office, using its matching owner-card room illustration as environment reference',
        'previousManifest': 'ArtSource/DayDesk/V2/previous-manifest.json',
        'geometry': {**previous['geometry'], 'notes': 'Default screen calibration retained for Edison and Jaiden; Jessy uses ownerSpecificGeometry after the V2 environment edit.'},
        'ownerSpecificGeometry': layout, 'assets': assets,
        'runtimeTotalBytes': sum(asset['runtimeBytes'] for asset in assets),
    }
    manifest.pop('currentJessyRevision', None)
    if output.resolve() == RUNTIME.resolve():
        (ROOT / 'ArtSource/DayDesk/manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    if sync_unity:
        from prepare_unity_office_art import main as prepare_unity_office_art
        prepare_unity_office_art()
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=RUNTIME)
    parser.add_argument('--sync-unity', action='store_true')
    args = parser.parse_args()
    manifest = prepare_owner_office_backgrounds(args.output, args.sync_unity)
    print(json.dumps({'version': manifest['version'], 'runtimeTotalBytes': manifest['runtimeTotalBytes'],
                      'assets': [{'key': a['key'], 'runtimeSha256': a['runtimeSha256'], 'runtimeBytes': a['runtimeBytes']} for a in manifest['assets']]}, indent=2))
