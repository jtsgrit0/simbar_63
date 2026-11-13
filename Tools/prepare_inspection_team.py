"""Pack generated inspection staff into the existing 64px character contract.

Only whole-sprite alpha cropping, frame reordering, proportional nearest-neighbor
sizing, foot alignment and PNG palette encoding occur here. No frames are
painted, recolored, mirrored or synthesized; all 36 source poses are retained.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

from PIL import Image
from prepare_performing_bands import read_cells, opaque, visible_bytes, changed_pixels

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / 'ArtSource/PixelArt/InspectionTeam'
OUTPUT = ROOT / 'Tools/WebGLTemplate/FastGame/art'
CELL, COLUMNS, ROWS, FOOT_X, FOOT_Y = 64, 12, 3, 32, 60


def prepare_inspection_team(source=SOURCE, output=OUTPUT):
    source, output = Path(source), Path(output)
    path = source / 'inspectors.source.png'
    original, source_rows, vertical = read_cells(path, 6, 6)
    atlas = Image.new('RGBA', (COLUMNS * CELL, ROWS * CELL))
    report = {'passed': True, 'source': path.name,
              'sourceSize': list(original.size),
              'sourceSha256': hashlib.sha256(path.read_bytes()).hexdigest(),
              'sourceAlphaExtrema': list(original.getchannel('A').getextrema()),
              'sourceRowBoundaries': vertical, 'size': list(atlas.size), 'rows': []}
    for row in range(ROWS):
        # First source row supplies FR/BR, the next supplies BL/FL.
        cells = source_rows[row * 2] + source_rows[row * 2 + 1]
        scale = min(56 / max(cell['bodyHeight'] for cell in cells),
                    60 / max(cell['image'].width for cell in cells),
                    60 / max(cell['image'].height for cell in cells))
        for cell in cells:
            image, (fx, fy) = cell['image'], cell['foot']
            scale = min(scale, 31 / max(1, fx),
                        31 / max(1, image.width - fx),
                        59 / max(1, fy), 3 / max(1, image.height - fy))
        details = {'row': row, 'scale': scale, 'cells': []}
        for column, cell in enumerate(cells):
            image, (fx, fy) = cell['image'], cell['foot']
            size = (max(1, round(image.width * scale)), max(1, round(image.height * scale)))
            x, y = round(FOOT_X - fx * scale), round(FOOT_Y - fy * scale)
            if min(x, y) < 0 or x + size[0] > CELL or y + size[1] > CELL:
                raise ValueError(f'Inspection sprite overflow: {row},{column}')
            packed = image.resize(size, Image.Resampling.NEAREST)
            atlas.paste(packed, (column * CELL + x, row * CELL + y))
            details['cells'].append({'column': column, 'sourceCell': cell['sourceCell'],
                                     'placedRect': [x, y, *size]})
        report['rows'].append(details)
    output.mkdir(parents=True, exist_ok=True)
    target = output / 'inspectors.png'
    atlas.quantize(colors=256, method=Image.Quantize.FASTOCTREE,
                   dither=Image.Dither.NONE).save(target, optimize=True)
    encoded = Image.open(target).convert('RGBA')
    for row in report['rows']:
        frames = [encoded.crop((c * CELL, row['row'] * CELL,
                                (c + 1) * CELL, (row['row'] + 1) * CELL))
                  for c in range(COLUMNS)]
        if any(opaque(frame).getbbox() is None for frame in frames):
            raise ValueError(f'Empty inspection frame: {row["row"]}')
        row['directions'] = []
        for start in range(0, COLUMNS, 3):
            group = frames[start:start + 3]
            unique = len({hashlib.sha256(visible_bytes(frame)).hexdigest() for frame in group})
            differences = [changed_pixels(group[i], group[i + 1]) for i in range(2)]
            if unique != 3 or not all(differences):
                raise ValueError(f'Inspection idle/walking poses are not distinct: {row["row"]}:{start}')
            row['directions'].append({'firstColumn': start, 'uniqueFrames': unique,
                                      'adjacentChangedPixels': differences})
    report.update(file=target.name, bytes=target.stat().st_size,
                  sha256=hashlib.sha256(target.read_bytes()).hexdigest(),
                  alphaExtrema=list(encoded.getchannel('A').getextrema()), nonEmptyCells=36)
    shutil.copy2(source / 'inspection-models.json', output / 'inspection-models.json')
    (source / 'packing-report.json').write_text(json.dumps(report, indent=2) + '\n',
                                               encoding='utf-8', newline='\n')
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, default=SOURCE)
    parser.add_argument('--output', type=Path, default=OUTPUT)
    args = parser.parse_args()
    result = prepare_inspection_team(args.source, args.output)
    print(json.dumps({key: result[key] for key in ['passed', 'file', 'size', 'bytes', 'nonEmptyCells', 'sha256']}))
