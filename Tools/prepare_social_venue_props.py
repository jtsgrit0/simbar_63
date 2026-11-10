"""Pack retained ImageGen social furniture into isolated pixel-art game props.

Only alpha-bounded cropping, proportional nearest-neighbor sizing and PNG
palette encoding occur here. Sources, generated transparency and appearance
are retained; this tool does not paint, recolor or synthesize image content.
"""
from pathlib import Path
import hashlib
import json

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / 'ArtSource/PixelArt/OwnerVenues/SocialProps'
OUTPUT = ROOT / 'Tools/WebGLTemplate/FastGame/art'
PROPS = [
    ('FreebirdArch.v2.source.png', 'freebird-arch.png', 76, 130),
    ('FreebirdWallTable.source.png', 'freebird-wall-table.png', 142, 158),
    ('CocktailGlass.source.png', 'cocktail-glass.png', 12, 16),
]


def prepare_social_venue_props(output=OUTPUT):
    output = Path(output)
    output.mkdir(parents=True, exist_ok=True)
    report = {'method': 'alpha crop, proportional nearest-neighbor sizing, PNG palette', 'props': []}
    for filename, target, width, height in PROPS:
        path = SOURCE / filename
        with Image.open(path) as original:
            source = original.convert('RGBA')
        if source.getchannel('A').getextrema()[0] != 0:
            raise ValueError(f'{filename} needs a genuine transparent background')
        bounds = source.getchannel('A').point(lambda a: 255 if a > 128 else 0).getbbox()
        if not bounds:
            raise ValueError(f'{filename} has no visible object')
        x0, y0, x1, y1 = bounds
        crop = source.crop((max(0, x0 - 2), max(0, y0 - 2),
                            min(source.width, x1 + 2), min(source.height, y1 + 2)))
        scale = min((width - 2) / crop.width, (height - 2) / crop.height)
        cropped_size = [max(1, round(crop.width * scale)), max(1, round(crop.height * scale))]
        crop = crop.resize(cropped_size, Image.Resampling.NEAREST)
        canvas = Image.new('RGBA', (width, height))
        canvas.paste(crop, ((width - crop.width) // 2, height - 1 - crop.height))
        destination = output / target
        canvas.quantize(colors=256, method=Image.Quantize.FASTOCTREE,
                        dither=Image.Dither.NONE).save(destination, optimize=True)
        packed = Image.open(destination).convert('RGBA')
        if any(packed.getpixel(point)[3] for point in [(0, 0), (width - 1, 0),
                                                      (0, height - 1), (width - 1, height - 1)]):
            raise ValueError(f'{target} lost its transparent outer margin')
        report['props'].append({'source': filename, 'file': target, 'size': [width, height],
                                'sourceSize': list(source.size), 'sourceBounds': list(bounds),
                                'sourceSha256': hashlib.sha256(path.read_bytes()).hexdigest(),
                                'sha256': hashlib.sha256(destination.read_bytes()).hexdigest(),
                                'bytes': destination.stat().st_size})
    report['passed'] = True
    (SOURCE / 'packing-report.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8', newline='\n')
    return report


if __name__ == '__main__':
    print(json.dumps(prepare_social_venue_props(), indent=2))
