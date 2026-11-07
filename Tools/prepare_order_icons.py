"""Pack the approved menu drawings and register their small shared icon atlas.

uv run --with pillow python Tools/prepare_order_icons.py
The source images remain unchanged. Packing only crops transparent space and
uses nearest-neighbor sizing; it never draws or recolors a menu item.
"""
from pathlib import Path
import hashlib
import json
import struct

ROOT = Path(__file__).resolve().parent.parent
ART = ROOT / 'Tools/WebGLTemplate/FastGame/art'
SOURCE = ROOT / 'ArtSource/OrderIcons/MenuIcons.source.png'
OUTPUT = 'order-icons.png'
ITEMS = ('faust', 'adios-motherfucker', 'pepperoni-half-pizza',
         'chili-cheese-fries', 'chili-cheese-chips', 'fried-chicken')
CELL = 32
SOURCE_COLUMNS, SOURCE_ROWS = 3, 2


def register_order_icons(output=ART):
    output = Path(output)
    icon = output / OUTPUT
    if not icon.is_file():
        return False
    raw = icon.read_bytes()
    if raw[:8] != b'\x89PNG\r\n\x1a\n' or struct.unpack('>II', raw[16:24]) != (CELL * len(ITEMS), CELL):
        raise ValueError('Order icon atlas must contain six 32x32 cells.')
    target = output / 'sprites.json'
    data = json.loads(target.read_text(encoding='utf-8'))
    data.setdefault('atlases', {})['orders'] = {
        'file': OUTPUT, 'frameWidth': CELL, 'frameHeight': CELL, 'columns': len(ITEMS)}
    encoded = json.dumps(data, ensure_ascii=False, indent=2) + '\n'
    target.write_text(encoded, encoding='utf-8', newline='\n')
    (ROOT / 'ArtSource/PixelArt/DirectionalSpriteContract.json').write_text(
        encoded, encoding='utf-8', newline='\n')
    return True


def pack_order_icons(source=SOURCE, output=ART):
    from PIL import Image

    source, output = Path(source), Path(output)
    image = Image.open(source).convert('RGBA')
    width, height = image.size
    atlas = Image.new('RGBA', (CELL * len(ITEMS), CELL))
    entries = []
    for index, item in enumerate(ITEMS):
        column, row = index % SOURCE_COLUMNS, index // SOURCE_COLUMNS
        left, right = round(width * column / SOURCE_COLUMNS), round(width * (column + 1) / SOURCE_COLUMNS)
        top, bottom = round(height * row / SOURCE_ROWS), round(height * (row + 1) / SOURCE_ROWS)
        cell = image.crop((left, top, right, bottom))
        # Ignore almost-invisible extraction residue for the working alpha only.
        alpha = cell.getchannel('A').point(lambda value: value if value >= 8 else 0)
        cell.putalpha(alpha)
        bounds = alpha.getbbox()
        if bounds is None:
            raise ValueError('Empty menu icon cell: ' + item)
        cropped = cell.crop(bounds)
        ratio = min(28 / cropped.width, 28 / cropped.height)
        size = (max(1, round(cropped.width * ratio)), max(1, round(cropped.height * ratio)))
        cropped = cropped.resize(size, Image.Resampling.NEAREST)
        position = (index * CELL + (CELL - size[0]) // 2, (CELL - size[1]) // 2)
        atlas.paste(cropped, position)
        entries.append({'item': item, 'column': index, 'sourceCell': [left, top, right, bottom],
                        'visibleBounds': list(bounds), 'packedSize': list(size)})
    output.mkdir(parents=True, exist_ok=True)
    target = output / OUTPUT
    atlas.save(target, optimize=True)
    manifest = {'source': source.relative_to(ROOT).as_posix(),
                'sourceSha256': hashlib.sha256(source.read_bytes()).hexdigest(),
                'runtime': target.relative_to(ROOT).as_posix(),
                'sha256': hashlib.sha256(target.read_bytes()).hexdigest(),
                'bytes': target.stat().st_size, 'cellSize': CELL, 'columns': len(ITEMS),
                'sourceGrid': [SOURCE_COLUMNS, SOURCE_ROWS],
                'method': 'Technical alpha crop and nearest-neighbor packing; no redraw or recolor',
                'entries': entries}
    (ROOT / 'ArtSource/OrderIcons/packing.json').write_text(
        json.dumps(manifest, indent=2) + '\n', encoding='utf-8', newline='\n')
    register_order_icons(output)
    print(f'Order icons ready: {target}; {target.stat().st_size:,} bytes')
    return manifest


if __name__ == '__main__':
    pack_order_icons()
