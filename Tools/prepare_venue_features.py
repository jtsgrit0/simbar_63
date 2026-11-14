"""Pack existing upgrade fixtures and newly matched ImageGen event props.

Technical operations only: transparent-gutter splitting, alpha cropping,
whole-prop translation and proportional nearest-neighbor sizing. The three
levels in each upgrade family share one scale, including the three genuinely
different FF tap orientations. No drawing, recoloring, warping or mirroring
occurs. All twelve upgrade cells must remain RGBA-identical to the retained
v2 runtime atlas. Three distinct event drawings are fit independently.
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
EVENT_SOURCE = 'venue-events-isometric-v3.source.png'
UPGRADE_BASELINE = 'venue-features-v2.runtime.png'


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


def event_cells(path):
    """Read the three full transparent event drawings from one source row."""
    with Image.open(path) as raw:
        if 'A' not in raw.getbands():
            raise ValueError(f'{path.name} needs genuine transparent alpha.')
        original = raw.convert('RGBA')
    if original.getchannel('A').getextrema()[0] != 0:
        raise ValueError(f'{path.name} has no transparent background.')
    columns = grid_boundaries(original, 3, 'x')
    cells = []
    for column in range(3):
        rect = (columns[column], 0, columns[column + 1], original.height)
        cell = original.crop(rect)
        bounds = opaque(cell).getbbox()
        if bounds is None:
            raise ValueError(f'Empty event source prop: {column}')
        x0, y0, x1, y1 = bounds
        if x0 == 0 or y0 == 0 or x1 == cell.width or y1 == cell.height:
            raise ValueError(f'Event artwork touches source cell boundary: {column}')
        image = cell.crop((max(0, x0 - 2), max(0, y0 - 2),
                           min(cell.width, x1 + 2), min(cell.height, y1 + 2)))
        ink = opaque(image).getbbox()
        cells.append({'image': image, 'sourceCell': list(rect),
                      'sourceRow': 0, 'sourceColumn': column,
                      'sourceOpaqueBounds': list(bounds),
                      'anchor': ((ink[0] + ink[2]) / 2, ink[3])})
    return original, cells, columns


def prepare_venue_features(source=SOURCE, output=OUTPUT):
    source, output = Path(source), Path(output)
    path, event_path = source / UPGRADE_SOURCE, source / EVENT_SOURCE
    original, source_rows, columns, boundaries = source_cells(path)
    event_original, events, event_columns = event_cells(event_path)
    baseline_path = source / UPGRADE_BASELINE
    with Image.open(baseline_path) as raw:
        baseline = raw.convert('RGBA')
    if baseline.size != (CELL * COLUMNS, CELL * ROWS):
        raise ValueError('Upgrade baseline must be the preserved 480x288 v2 runtime atlas.')
    atlas = Image.new('RGBA', (COLUMNS * CELL, ROWS * CELL))
    report = {'passed': True, 'source': path.name,
              'sourceSize': list(original.size),
              'sourceSha256': hashlib.sha256(path.read_bytes()).hexdigest(),
              'sourceAlphaExtrema': list(original.getchannel('A').getextrema()),
              'sourceColumnBoundaries': columns,
              'sourceRowBoundariesByColumn': boundaries,
              'eventSource': event_path.name,
              'eventSourceSize': list(event_original.size),
              'eventSourceSha256': hashlib.sha256(event_path.read_bytes()).hexdigest(),
              'eventSourceAlphaExtrema': list(event_original.getchannel('A').getextrema()),
              'eventSourceColumnBoundaries': event_columns,
              'upgradeBaseline': baseline_path.name,
              'upgradeBaselineSha256': hashlib.sha256(baseline_path.read_bytes()).hexdigest(),
              'size': list(atlas.size), 'encoding': 'lossless RGBA, no palette quantization',
              'groups': [], 'rows': [{'row': row, 'cells': []} for row in range(ROWS)]}

    def pack(cell, row, column, scale, source_path=path):
        image, (fx, fy) = cell['image'], cell['anchor']
        size = (max(1, round(image.width * scale)), max(1, round(image.height * scale)))
        x, y = round(FOOT_X - fx * scale), round(FOOT_Y - fy * scale)
        if min(x, y) < 0 or x + size[0] > CELL or y + size[1] > CELL:
            raise ValueError(f'Venue feature overflow: {row},{column}')
        packed = image.resize(size, Image.Resampling.NEAREST)
        atlas.paste(packed, (column * CELL + x, row * CELL + y))
        report['rows'][row]['cells'].append({
            'column': column, 'source': source_path.name, 'sourceCell': cell['sourceCell'],
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
    for row, (event_id, cell) in enumerate(zip(
            ['open-mic', 'rock-showcase', 'fan-festival'], events)):
        scale = group_scale([cell])
        report['groups'].append({'name': event_id, 'scale': scale,
                                 'runtimeCells': [[row, 3]]})
        pack(cell, row, 3, scale, source_path=event_path)

    # Check the preservation contract before replacing the runtime file.
    for row in range(ROWS):
        for column in [0, 1, 2, 4]:
            rect = (column * CELL, row * CELL, (column + 1) * CELL, (row + 1) * CELL)
            if atlas.crop(rect).tobytes() != baseline.crop(rect).tobytes():
                raise ValueError(f'Upgrade RGBA pixels changed: {row},{column}')

    output.mkdir(parents=True, exist_ok=True)
    target = output / 'venue-features.png'
    atlas.save(target, optimize=True)
    with Image.open(target) as raw:
        if raw.mode != 'RGBA':
            raise ValueError('Runtime atlas must retain lossless RGBA encoding.')
        encoded = raw.convert('RGBA')
    if encoded.tobytes() != atlas.tobytes():
        raise ValueError('Runtime PNG encoding changed RGBA pixels.')
    hashes, upgrade_checks, event_changes = [], [], []
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
            cell['packedRGBAsha256'] = hashlib.sha256(frame.tobytes()).hexdigest()
            same = frame.tobytes() == baseline.crop(rect).tobytes()
            if column != 3:
                upgrade_checks.append(same)
                if not same:
                    raise ValueError(f'Upgrade RGBA pixels changed: {row["row"]},{column}')
                cell['preservedUpgradeRGBA'] = True
                cell['baselineCellRGBAsha256'] = hashlib.sha256(baseline.crop(rect).tobytes()).hexdigest()
            else:
                event_changes.append(not same)
                if same:
                    raise ValueError(f'Event did not change from v2 baseline: row {row["row"]}')
    if len(set(hashes)) != ROWS * COLUMNS:
        raise ValueError('Venue features must contain fifteen unique props.')
    metadata = json.loads((source / 'venue-features.json').read_text(encoding='utf-8'))
    if metadata['columns'] != COLUMNS or metadata['rows'] != ROWS:
        raise ValueError('Runtime metadata dimensions do not match the atlas.')
    if metadata['sources']['events'] != EVENT_SOURCE or metadata['sources']['upgradeBaseline'] != UPGRADE_BASELINE:
        raise ValueError('Runtime metadata does not name the current event source and upgrade baseline.')
    for level in range(1, 4):
        if metadata['items'][f'draught-{level}']['variants']['ff'] != {'row': level - 1, 'column': 4}:
            raise ValueError('FF draught variant metadata does not match the packed cell.')
    report.update(file=target.name, bytes=target.stat().st_size,
                  sha256=hashlib.sha256(target.read_bytes()).hexdigest(),
                  alphaExtrema=list(encoded.getchannel('A').getextrema()),
                  nonEmptyCells=15, uniqueProps=len(set(hashes)),
                  preservedUpgradeProps=12, generatedEventProps=3,
                  allUpgradePixelsUnchanged=all(upgrade_checks),
                  changedEventCells=sum(event_changes))
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
                       'allUpgradePixelsUnchanged', 'changedEventCells', 'sha256']}))
