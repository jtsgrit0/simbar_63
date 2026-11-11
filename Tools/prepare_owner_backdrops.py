"""Pack the authored, map-referenced owner street illustrations for the game.

Only center cropping, nearest-neighbor sizing and palette encoding occur here.
Google/Naver maps and street photographs are references, not shipped textures.
"""
import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageOps

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / 'ArtSource/PixelArt/OwnerVenues/Backdrops'
OUTPUT = ROOT / 'Tools/WebGLTemplate/FastGame/art'
VARIANTS = {'FreebirdStreet.source.png': 'seoul-freebird.png',
            'FFStreet.source.png': 'seoul-ff.png'}
SIZE = (640, 360)


def prepare_owner_backdrops(output=OUTPUT):
    """Encode both independent streets; leave Jaiden's seoul.png untouched."""
    # Validate every source before writing either member of the paired export.
    images = []
    for source, target in VARIANTS.items():
        with Image.open(SOURCE / source) as original:
            image = original.convert('RGBA')
        if image.getchannel('A').getextrema() != (255, 255):
            raise ValueError(f'{source} must be a fully opaque street backdrop')
        image = ImageOps.fit(image, SIZE, method=Image.Resampling.NEAREST,
                             centering=(.5, .5))
        image = image.quantize(colors=256, method=Image.Quantize.FASTOCTREE,
                               dither=Image.Dither.NONE)
        images.append((image, target))
    output = Path(output)
    output.mkdir(parents=True, exist_ok=True)
    result = []
    for image, target in images:
        path = output / target
        image.save(path, optimize=True)
        result.append({'file': target, 'width': SIZE[0], 'height': SIZE[1],
                       'bytes': path.stat().st_size,
                       'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=OUTPUT)
    args = parser.parse_args()
    print(json.dumps(prepare_owner_backdrops(args.output), indent=2))
