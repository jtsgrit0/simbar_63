"""Composite the authored Pet Sounds neon sign onto Jaiden's retained room.

This is an art packaging step: alpha-bounded cropping, proportional nearest-
neighbor sizing and compositing only. The generated sign already supplies its
wall perspective. The original room is rebuilt for every run, so repeated runs
cannot accumulate glow. Freebird/FF plates and room calibration are untouched.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / 'ArtSource/PixelArt/OwnerVenues/PetSounds'
NEON_SOURCE = SOURCE / 'petsounds-neon-wall.v2.source.png'
LAYOUT = SOURCE / 'petsounds-neon.layout.json'
BASE_SOURCES = (ROOT / 'ArtSource/PixelArt/V4/VenueForeignMusicians.png',
                ROOT / 'ArtSource/PixelArt/V4/VenueNoPlants.png')
OUTPUT = ROOT / 'Tools/WebGLTemplate/FastGame/art'
UNITY_VENUE = ROOT / 'Assets/Resources/SimBarPixelArt/Venue.bytes'
ROOM_SIZE = (960, 558)


def _integer(layout, key, minimum):
    value = layout.get(key)
    if isinstance(value, bool) or not isinstance(value, int) or value < minimum:
        raise ValueError(f'Pet Sounds layout {key} must be an integer >= {minimum}')
    return value


def prepare_petsounds_neon(output=OUTPUT, sync_unity=False):
    """Write venue.png; optionally copy only that PNG stream to Unity Venue.bytes.

    Layout JSON uses x/y for the top-left of the alpha-cropped sign and width
    for its requested width. Optional height makes width/height a containing
    rectangle, preserving the source aspect ratio rather than stretching it.
    """
    # Read and validate every input before changing the existing room artwork.
    if not NEON_SOURCE.is_file():
        raise FileNotFoundError(f'Missing authored Pet Sounds sign: {NEON_SOURCE}')
    if not LAYOUT.is_file():
        raise FileNotFoundError(f'Missing Pet Sounds sign layout: {LAYOUT}')
    base_source = next((path for path in BASE_SOURCES if path.is_file()), None)
    if base_source is None:
        raise FileNotFoundError('Missing the original V4 Jaiden room source')
    layout = json.loads(LAYOUT.read_text(encoding='utf-8'))
    if not isinstance(layout, dict):
        raise ValueError('Pet Sounds layout must be a JSON object')
    x, y = _integer(layout, 'x', 0), _integer(layout, 'y', 0)
    width = _integer(layout, 'width', 1)
    height = _integer(layout, 'height', 1) if 'height' in layout else None
    with Image.open(NEON_SOURCE) as original:
        sign = original.convert('RGBA')
    alpha = sign.getchannel('A')
    if alpha.getextrema()[0] != 0:
        raise ValueError('Pet Sounds sign needs a genuine transparent background')
    bounds = alpha.getbbox()
    if bounds is None:
        raise ValueError('Pet Sounds sign has no visible pixels')
    sign = sign.crop(bounds)
    scale = width / sign.width
    if height is not None:
        scale = min(scale, height / sign.height)
    size = (max(1, round(sign.width * scale)), max(1, round(sign.height * scale)))
    if x + size[0] > ROOM_SIZE[0] or y + size[1] > ROOM_SIZE[1]:
        raise ValueError('Pet Sounds sign must fit entirely inside the 960x558 room')
    sign = sign.resize(size, Image.Resampling.NEAREST)
    with Image.open(base_source) as original:
        base = original.convert('RGBA').resize(ROOM_SIZE, Image.Resampling.NEAREST)
    # Match the existing room preparation exactly before applying the sign.
    # Do not quantize the composite: that could recolor unrelated room pixels.
    base = base.quantize(colors=256, method=Image.Quantize.FASTOCTREE,
                         dither=Image.Dither.NONE).convert('RGBA')
    base.alpha_composite(sign, (x, y))
    output = Path(output)
    output.mkdir(parents=True, exist_ok=True)
    target = output / 'venue.png'
    base.save(target, optimize=True)
    if sync_unity:
        UNITY_VENUE.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(target, UNITY_VENUE)
    return {'file': str(target), 'baseSource': str(base_source),
            'source': str(NEON_SOURCE), 'sourceBounds': list(bounds),
            'placement': {'x': x, 'y': y, 'width': size[0], 'height': size[1]},
            'sourceSha256': hashlib.sha256(NEON_SOURCE.read_bytes()).hexdigest(),
            'sha256': hashlib.sha256(target.read_bytes()).hexdigest(),
            'bytes': target.stat().st_size, 'unitySynced': bool(sync_unity)}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=OUTPUT,
                        help='Native art output directory (writes only venue.png)')
    parser.add_argument('--sync-unity', action='store_true',
                        help='Also copy only venue.png to Unity Venue.bytes')
    args = parser.parse_args()
    print(json.dumps(prepare_petsounds_neon(args.output, args.sync_unity), indent=2))
