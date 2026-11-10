"""Technically pack authored transparent festival poses into 64px runtime cells.

Only whole-sprite cropping, proportion-preserving nearest-neighbor sizing,
foot alignment and palette encoding are performed. Source poses are retained.
"""
import hashlib
import json
from pathlib import Path
import shutil

from PIL import Image
from prepare_performing_bands import read_cells, opaque, visible_bytes, changed_pixels

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / 'ArtSource/PixelArt/FestivalAudience'
OUTPUT = ROOT / 'Tools/WebGLTemplate/FastGame/art'
ROWS, COLUMNS, CELL, FOOT_X, FOOT_Y = 8, 12, 64, 32, 60
STYLES = ['Casual', 'Suit', 'Beach', 'Worker', 'Farmer', 'Adventurer', 'Casual2', 'Punk']


def prepare_festival_audience(source=SOURCE, output=OUTPUT):
    source, output = Path(source), Path(output)
    output.mkdir(parents=True, exist_ok=True)
    report = {'passed': True, 'frameWidth': CELL, 'frameHeight': CELL, 'atlases': []}
    for key, filename, columns in [('gestures', 'gestures.source.png', 12), ('vocals', 'vocals.corrected.source.png', 12), ('surf', 'surf.source.png', 4)]:
        path = source / filename
        original, rows, vertical = read_cells(path, ROWS, columns)
        atlas = Image.new('RGBA', (columns * CELL, ROWS * CELL))
        item = {'key': key, 'source': filename, 'sourceSha256': hashlib.sha256(path.read_bytes()).hexdigest(),
                'sourceSize': list(original.size), 'sourceAlphaExtrema': list(original.getchannel('A').getextrema()),
                'rowBoundaries': vertical, 'rows': []}
        for row, cells in enumerate(rows):
            if key == 'surf':
                for cell in cells:
                    cell['foot'] = (cell['image'].width / 2, cell['image'].height / 2)
            # One shared scale per identity prevents size pulsing between poses.
            scale = min(56 / max(cell['image'].height for cell in cells),
                        60 / max(cell['image'].width for cell in cells))
            for cell in cells:
                image, (fx, fy) = cell['image'], cell['foot']
                scale = min(scale, 31 / max(1, fx), 31 / max(1, image.width - fx),
                            (31 if key == 'surf' else 59) / max(1, fy),
                            (31 if key == 'surf' else 3) / max(1, image.height - fy))
            details = {'row': row, 'style': STYLES[row], 'scale': scale, 'cells': []}
            frames = []
            for column, cell in enumerate(cells):
                image, (fx, fy) = cell['image'], cell['foot']
                width, height = max(1, round(image.width * scale)), max(1, round(image.height * scale))
                x, y = round(FOOT_X - fx * scale), round((32 if key == 'surf' else FOOT_Y) - fy * scale)
                if min(x, y) < 0 or x + width > CELL or y + height > CELL:
                    raise ValueError(f'Festival sprite overflow: {key}:{row},{column}')
                resized = image.resize((width, height), Image.Resampling.NEAREST)
                atlas.paste(resized, (column * CELL + x, row * CELL + y))
                details['cells'].append({'column': column, 'sourceCell': cell['sourceCell'],
                                         'placedRect': [x, y, width, height]})
            item['rows'].append(details)
        target = output / f'festival-{key}.png'
        atlas.quantize(colors=256, method=Image.Quantize.FASTOCTREE, dither=Image.Dither.NONE).save(target, optimize=True)
        encoded = Image.open(target).convert('RGBA')
        for row in item['rows']:
            frames = [encoded.crop((c * CELL, row['row'] * CELL, (c + 1) * CELL, (row['row'] + 1) * CELL)) for c in range(columns)]
            if any(opaque(frame).getbbox() is None for frame in frames):
                raise ValueError(f'Empty festival frame: {key}:{row["row"]}')
            row['actions'] = []
            for at in ((0,) if key == 'surf' else (0, 4, 8)):
                group = frames[at:at + 4]
                unique = len({hashlib.sha256(visible_bytes(frame)).hexdigest() for frame in group})
                differences = [changed_pixels(group[i], group[(i + 1) % 4]) for i in range(4)]
                if unique < 2 or not all(differences):
                    raise ValueError(f'Non-animated festival group: {key}:{row["row"]}:{at}')
                row['actions'].append({'firstColumn': at, 'uniqueFrames': unique, 'adjacentChangedPixels': differences})
        item.update(file=target.name, bytes=target.stat().st_size, sha256=hashlib.sha256(target.read_bytes()).hexdigest(),
                    size=list(encoded.size), nonEmptyCells=ROWS * columns)
        report['atlases'].append(item)
    shutil.copy2(source / 'festival-models.json', output / 'festival-models.json')
    (source / 'packing-report.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8', newline='\n')
    return report


if __name__ == '__main__':
    result = prepare_festival_audience()
    print(json.dumps({'passed': result['passed'], 'atlases': [{k: a[k] for k in ('file', 'bytes', 'nonEmptyCells')} for a in result['atlases']]}))
