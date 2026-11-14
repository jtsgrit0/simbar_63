"""Pack upgraded ImageGen fixtures and unchanged legacy event pixels.

Technical operations only: transparent-gutter splitting, alpha cropping,
whole-prop translation and proportional nearest-neighbor sizing. The three
levels in each upgrade family share one scale, including the three genuinely
different FF tap orientations. No drawing, recoloring, warping or mirroring
occurs. Legacy event cells are copied losslessly at their original size.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

from PIL import Image
from prepare_performing_bands import grid_boundaries, opaque, visible_bytes

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / 'ArtSource/PixelArt/VenueFeatures'
OUTPUT = ROOT / 'Tools/WebGLTemplate/FastGame/art'
CELL, COLUMNS, ROWS, FOOT_X, FOOT_Y = 96, 5, 3, 48, 88
UPGRADE_SOURCE = 'venue-upgrades-isometric-v2.source.png'
EVENT_SOURCE = 'venue-features-v1.runtime.png'


def source_cells(path):
    """Split columns first: source rows have different transparent gutters."""
    with Image.open(path) as raw:
        if 'A' not in raw.getbands():
            raise ValueError(f'{path.name} needs genuine transparent alpha.')
        original = raw.convert('RGBA')
    if original.getchannel('A').getextrema()[0] != 0:
        raise ValueError(f'{path.name} has no transparent background.')
    columns = grid_boundaries(original, 4, 'x')
    boundaries, rows = [], [[] for _ in range(ROWS)]
    for column in range(4):
        strip = original.crop((columns[column], 0, columns[column + 1], original.height))
        vertical = grid_boundaries(strip, ROWS, 'y')
        boundaries.append(vertical)
        for row in range(ROWS):
            rect = (columns[column], vertical[row], columns[column + 1], vertical[row + 1])
            cell = original.crop(rect)
            bounds = opaque(cell).getbbox()
            if bounds is None:
                raise ValueError(f'Empty source prop: {row},{column}')
            x0, y0, x1, y1 = bounds
            if x0 == 0 or y0 == 0 or x1 == cell.width or y1 == cell.height:
                raise ValueError(f'Artwork touches source cell boundary: {row},{column}')
            image = cell.crop((max(0, x0 - 2), max(0, y0 - 2),
                               min(cell.width, x1 + 2), min(cell.height, y1 + 2)))
            ink = opaque(image).getbbox()
            rows[row].append({'image': image, 'sourceCell': list(rect),
                              'sourceRow': row, 'sourceColumn': column,
                              'sourceOpaqueBounds': list(bounds),
                              'anchor': ((ink[0] + ink[2]) / 2, ink[3])})
    return original, rows, columns, boundaries


def group_scale(cells):
    scale = min(88 / max(cell['image'].width for cell in cells),
                84 / max(cell['image'].height for cell in cells))
    for cell in cells:
        image, (fx, fy) = cell['image'], cell['anchor']
        scale = min(scale, 47 / max(1, fx),
                    47 / max(1, image.width - fx),
                    87 / max(1, fy), 7 / max(1, image.height - fy))
    return scale


def prepare_venue_features(source=SOURCE, output=OUTPUT):
    source, output = Path(source), Path(output)
    path, event_path = source / UPGRADE_SOURCE, source / EVENT_SOURCE
    original, source_rows, columns, boundaries = source_cells(path)
    with Image.open(event_path) as raw:
        legacy = raw.convert('RGBA')
    if legacy.size != (CELL * 4, CELL * ROWS):
        raise ValueError('Legacy event source must be the preserved 384x288 runtime atlas.')
    atlas = Image.new('RGBA', (COLUMNS * CELL, ROWS * CELL))
    report = {'passed': True, 'source': path.name,
              'sourceSize': list(original.size),
              'sourceSha256': hashlib.sha256(path.read_bytes()).hexdigest(),
              'sourceAlphaExtrema': list(original.getchannel('A').getextrema()),
              'sourceColumnBoundaries': columns,
              'sourceRowBoundariesByColumn': boundaries,
              'eventSource': event_path.name,
              'eventSourceSha256': hashlib.sha256(event_path.read_bytes()).hexdigest(),
              'size': list(atlas.size), 'encoding': 'lossless RGBA, no palette quantization',
              'groups': [], 'rows': [{'row': row, 'cells': []} for row in range(ROWS)]}

    def pack(cell, row, column, scale):
        image, (fx, fy) = cell['image'], cell['anchor']
        size = (max(1, round(image.width * scale)), max(1, round(image.height * scale)))
        x, y = round(FOOT_X - fx * scale), round(FOOT_Y - fy * scale)
        if min(x, y) < 0 or x + size[0] > CELL or y + size[1] > CELL:
            raise ValueError(f'Venue feature overflow: {row},{column}')
        packed = image.resize(size, Image.Resampling.NEAREST)
        atlas.paste(packed, (column * CELL + x, row * CELL + y))
        report['rows'][row]['cells'].append({
            'column': column, 'source': path.name, 'sourceCell': cell['sourceCell'],
            'sourceRow': cell['sourceRow'], 'sourceColumn': cell['sourceColumn'],
            'sourceOpaqueBounds': cell['sourceOpaqueBounds'],
            'scale': scale, 'placedRect': [x, y, *size]})

    for row, category in enumerate(['draught', 'sound', 'vip']):
        group = source_rows[row][:3]
        scale = group_scale(group)
        report['groups'].append({'name': category, 'scale': scale,
                                 'runtimeCells': [[row, column] for column in range(3)]})
        for column, cell in enumerate(group):
            pack(cell, row, column, scale)
    ff_group = [source_rows[row][3] for row in range(ROWS)]
    ff_scale = group_scale(ff_group)
    report['groups'].append({'name': 'ff-draught', 'scale': ff_scale,
                             'runtimeCells': [[row, 4] for row in range(ROWS)]})
    for row, cell in enumerate(ff_group):
        pack(cell, row, 4, ff_scale)
    for row in range(ROWS):
        rect = (3 * CELL, row * CELL, 4 * CELL, (row + 1) * CELL)
        event = legacy.crop(rect)
        atlas.paste(event, (3 * CELL, row * CELL))
        report['rows'][row]['cells'].append({
            'column': 3, 'source': event_path.name, 'sourceCell': list(rect),
            'scale': 1, 'placedRect': [0, 0, CELL, CELL],
            'preservedEventRGBA': True,
            'sourceCellRGBAsha256': hashlib.sha256(event.tobytes()).hexdigest()})

    output.mkdir(parents=True, exist_ok=True)
    target = output / 'venue-features.png'
    atlas.save(target, optimize=True)
    with Image.open(target) as raw:
        if raw.mode != 'RGBA':
            raise ValueError('Runtime atlas must retain lossless RGBA encoding.')
        encoded = raw.convert('RGBA')
    if encoded.tobytes() != atlas.tobytes():
        raise ValueError('Runtime PNG encoding changed RGBA pixels.')
    hashes, event_checks = [], []
    for row in report['rows']:
        row['cells'].sort(key=lambda cell: cell['column'])
        for cell in row['cells']:
            column = cell['column']
            rect = (column * CELL, row['row'] * CELL,
                    (column + 1) * CELL, (row['row'] + 1) * CELL)
            frame = encoded.crop(rect)
            bounds = opaque(frame).getbbox()
            if bounds is None:
                raise ValueError(f'Empty venue feature: {row["row"]},{column}')
            cell['packedOpaqueBounds'] = list(bounds)
            cell['packedVisibleSha256'] = hashlib.sha256(visible_bytes(frame)).hexdigest()
            hashes.append(cell['packedVisibleSha256'])
            if column == 3:
                same = frame.tobytes() == legacy.crop(rect).tobytes()
                event_checks.append(same)
                if not same:
                    raise ValueError(f'Legacy event RGBA pixels changed: row {row["row"]}')
                cell['packedRGBAsha256'] = hashlib.sha256(frame.tobytes()).hexdigest()
    if len(set(hashes)) != ROWS * COLUMNS:
        raise ValueError('Venue features must contain fifteen unique props.')
    metadata = json.loads((source / 'venue-features.json').read_text(encoding='utf-8'))
    if metadata['columns'] != COLUMNS or metadata['rows'] != ROWS:
        raise ValueError('Runtime metadata dimensions do not match the atlas.')
    for level in range(1, 4):
        if metadata['items'][f'draught-{level}']['variants']['ff'] != {'row': level - 1, 'column': 4}:
            raise ValueError('FF draught variant metadata does not match the packed cell.')
    report.update(file=target.name, bytes=target.stat().st_size,
                  sha256=hashlib.sha256(target.read_bytes()).hexdigest(),
                  alphaExtrema=list(encoded.getchannel('A').getextrema()),
                  nonEmptyCells=15, uniqueProps=len(set(hashes)),
                  generatedUpgradeProps=12, preservedEventProps=3,
                  allEventPixelsUnchanged=all(event_checks))
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
                      ['passed', 'file', 'size', 'bytes', 'nonEmptyCells', 'uniqueProps',
                       'allEventPixelsUnchanged', 'sha256']}))
