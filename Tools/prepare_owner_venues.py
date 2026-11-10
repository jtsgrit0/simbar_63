"""Encode ImageGen owner interiors at the existing room's authored resolution.

Only sizing and palette encoding happen here. The original Jaiden plate and
the shared actor/navigation calibration are never rewritten.
"""
from pathlib import Path
import json
import shutil

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / 'ArtSource/PixelArt/OwnerVenues'
OUTPUT = ROOT / 'Tools/WebGLTemplate/FastGame/art'
VARIANTS = {'ClubFreebird.v2.source.png': 'venue-freebird.png',
            'ClubFF.v2.source.png': 'venue-ff.png'}


def prepare_owner_venues(output=OUTPUT):
    output = Path(output)
    output.mkdir(parents=True, exist_ok=True)
    for source, target in VARIANTS.items():
        with Image.open(SOURCE / source) as raw:
            image = raw.convert('RGBA')
        if image.getchannel('A').getextrema() != (0, 255):
            raise ValueError(f'{source} must retain a transparent cutaway background.')
        image = image.resize((960, 558), Image.Resampling.NEAREST)
        image = image.quantize(colors=256, method=Image.Quantize.FASTOCTREE,
                               dither=Image.Dither.NONE)
        image.save(output / target, optimize=True)
        print(f'{target}: 960x558, {(output / target).stat().st_size:,} bytes')
    for owner, filename in [('ff', 'ClubFF.layout.json'), ('freebird', 'ClubFreebird.layout.json')]:
        layout = SOURCE / filename
        metadata = json.loads(layout.read_text(encoding='utf-8'))
        if metadata.get('layout', {}).get('id') != owner:
            raise ValueError(f'{owner} requires its own layout metadata.')
        if owner == 'ff' and metadata.get('seatedActors'):
            raise ValueError('FF uses standing audience spots rather than seated actors.')
        shutil.copy2(layout, output / ('venue-' + owner + '.json'))
    from prepare_ff_tables import prepare_ff_tables
    prepare_ff_tables(output)
    from prepare_social_venue_props import prepare_social_venue_props
    prepare_social_venue_props(output)


if __name__ == '__main__':
    prepare_owner_venues()
