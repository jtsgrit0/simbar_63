"""Pack the two retained ImageGen furniture cells; no creative drawing occurs.

The alpha >128 contour measures each prop, like the existing character atlas
packer. Original edge alpha is retained inside a small two-pixel source margin.
Nearest-neighbor fit keeps the source proportions and leaves transparent space.
"""
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / 'ArtSource/PixelArt/OwnerVenues/FFStandingFurniture.source.png'
OUTPUT = ROOT / 'Tools/WebGLTemplate/FastGame/art'


def prepare_ff_tables(output=OUTPUT):
    output = Path(output)
    output.mkdir(parents=True, exist_ok=True)
    with Image.open(SOURCE) as raw:
        sheet = raw.convert('RGBA')
    for index, (name, width, height) in enumerate([
            ('standing-table.png', 40, 38), ('standing-stool.png', 24, 30)]):
        cell = sheet.crop((round(index * sheet.width / 2), 0,
                           round((index + 1) * sheet.width / 2), sheet.height))
        bounds = cell.getchannel('A').point(lambda a: 255 if a > 128 else 0).getbbox()
        if bounds is None:
            raise ValueError(f'Missing generated furniture cell: {name}')
        x0, y0, x1, y1 = bounds
        cell = cell.crop((max(0, x0 - 2), max(0, y0 - 2),
                          min(cell.width, x1 + 2), min(cell.height, y1 + 2)))
        scale = min((width - 2) / cell.width, (height - 2) / cell.height)
        cell = cell.resize((max(1, round(cell.width * scale)),
                            max(1, round(cell.height * scale))), Image.Resampling.NEAREST)
        canvas = Image.new('RGBA', (width, height))
        canvas.paste(cell, ((width - cell.width) // 2, height - 1 - cell.height))
        canvas.quantize(colors=128, method=Image.Quantize.FASTOCTREE,
                        dither=Image.Dither.NONE).save(output / name, optimize=True)
        print(f'{name}: {width}x{height}, {(output / name).stat().st_size:,} bytes')


if __name__ == '__main__':
    prepare_ff_tables()
