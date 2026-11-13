"""Pack twelve genuine ImageGen venue props into 96px transparent cells.

Only alpha cropping, whole-prop translation, proportional nearest-neighbor
sizing and PNG palette encoding occur. Upgrade levels share a scale within
their category; event decor is fit separately. No props are painted, mirrored,
recolored, merged, synthesized or substituted.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

from PIL import Image
from prepare_performing_bands import read_cells, opaque, visible_bytes

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / 'ArtSource/PixelArt/VenueFeatures'
OUTPUT = ROOT / 'Tools/WebGLTemplate/FastGame/art'
CELL, COLUMNS, ROWS, FOOT_X, FOOT_Y = 96, 4, 3, 48, 88


def prepare_venue_features(source=SOURCE, output=OUTPUT):
    source, output = Path(source), Path(output)
    path = source / 'venue-features.source.png'
    original, source_rows, boundaries = read_cells(path, ROWS, COLUMNS)
    atlas = Image.new('RGBA', (COLUMNS * CELL, ROWS * CELL))
    report = {'passed': True, 'source': path.name,
              'sourceSize': list(original.size),
              'sourceSha256': hashlib.sha256(path.read_bytes()).hexdigest(),
              'sourceAlphaExtrema': list(original.getchannel('A').getextrema()),
              'sourceRowBoundaries': boundaries, 'size': list(atlas.size), 'rows': []}
    for row, cells in enumerate(source_rows):
        details = {'row': row, 'groups': [], 'cells': []}
        for cell in cells:
            bounds = opaque(cell['image']).getbbox()
            # A prop's floor anchor is the center of its complete bottom bound.
            cell['anchor'] = ((bounds[0] + bounds[2]) / 2, bounds[3])
        for name, columns in [('upgradeLevels', range(3)), ('event', [3])]:
            group = [cells[column] for column in columns]
            scale = min(88 / max(cell['image'].width for cell in group),
                        84 / max(cell['image'].height for cell in group))
            for cell in group:
                image, (fx, fy) = cell['image'], cell['anchor']
                scale = min(scale, 47 / max(1, fx),
                            47 / max(1, image.width - fx),
                            87 / max(1, fy), 7 / max(1, image.height - fy))
            details['groups'].append({'name': name, 'scale': scale})
            for column in columns:
                cell = cells[column]
                image, (fx, fy) = cell['image'], cell['anchor']
                size = (max(1, round(image.width * scale)), max(1, round(image.height * scale)))
                x, y = round(FOOT_X - fx * scale), round(FOOT_Y - fy * scale)
                if min(x, y) < 0 or x + size[0] > CELL or y + size[1] > CELL:
                    raise ValueError(f'Venue feature overflow: {row},{column}')
                packed = image.resize(size, Image.Resampling.NEAREST)
                atlas.paste(packed, (column * CELL + x, row * CELL + y))
                details['cells'].append({'column': column, 'sourceCell': cell['sourceCell'],
                                         'scale': scale, 'placedRect': [x, y, *size]})
        report['rows'].append(details)
    output.mkdir(parents=True, exist_ok=True)
    target = output / 'venue-features.png'
    atlas.quantize(colors=256, method=Image.Quantize.FASTOCTREE,
                   dither=Image.Dither.NONE).save(target, optimize=True)
    encoded = Image.open(target).convert('RGBA')
    hashes = []
    for row in report['rows']:
        for cell in row['cells']:
            column = cell['column']
            frame = encoded.crop((column * CELL, row['row'] * CELL,
                                  (column + 1) * CELL, (row['row'] + 1) * CELL))
            bounds = opaque(frame).getbbox()
            if bounds is None:
                raise ValueError(f'Empty venue feature: {row["row"]},{column}')
            cell['packedOpaqueBounds'] = list(bounds)
            hashes.append(hashlib.sha256(visible_bytes(frame)).hexdigest())
    if len(set(hashes)) != ROWS * COLUMNS:
        raise ValueError('Venue features must contain twelve unique props.')
    report.update(file=target.name, bytes=target.stat().st_size,
                  sha256=hashlib.sha256(target.read_bytes()).hexdigest(),
                  alphaExtrema=list(encoded.getchannel('A').getextrema()),
                  nonEmptyCells=12, uniqueProps=len(set(hashes)))
    shutil.copy2(source / 'venue-features.json', output / 'venue-features.json')
    (source / 'packing-report.json').write_text(json.dumps(report, indent=2) + '\n',
                                               encoding='utf-8', newline='\n')
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, default=SOURCE)
    parser.add_argument('--output', type=Path, default=OUTPUT)
    args = parser.parse_args()
    result = prepare_venue_features(args.source, args.output)
    print(json.dumps({key: result[key] for key in
                      ['passed', 'file', 'size', 'bytes', 'nonEmptyCells', 'uniqueProps', 'sha256']}))
