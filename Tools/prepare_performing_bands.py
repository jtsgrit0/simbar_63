"""Pack retained artist-specific ImageGen sheets into the game's 64px cells.

This tool performs technical cropping, whole-sprite translation, uniform
nearest-neighbor sizing and PNG palette encoding only. It does not paint,
recolor, mirror, move limbs or synthesize animation frames. Each generated
source performance/walk cell remains a separate genuine frame. The corrected
Freewill sheet supplies eleven columns; runtime column 11 holds the existing
column-10 hug-open image. That technical repeat is recorded, never reported
as a newly drawn source frame.
The retained source stays full-color; 256-color runtime encoding can change
RGBA values, as in the existing band atlas delivery pipeline.
"""
import argparse
import hashlib
import json
from pathlib import Path
from statistics import median
import shutil

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / 'ArtSource/PixelArt/PerformingBands'
OUTPUT = ROOT / 'Tools/WebGLTemplate/FastGame/art'
CELL, FOOT_X, FOOT_Y = 64, 32, 60
COLUMNS = 12
BANDS = [('freewill', 'freewill.corrected.source.png', 'band-freewill.png', 6, 5, 11),
         ('julywantsmore', 'july.source.png', 'band-july.png', 5, 4, 12)]


def opaque(image):
    return image.getchannel('A').point(lambda a: 255 if a > 128 else 0)


def grid_boundaries(image, count, axis):
    """Split at real transparent gutters, retaining unequal source spacing.

    ImageGen can make a compact seated final row rather than equal-height
    rows. Equal division would cut feet or mix two actors. The longest full
    transparent runs separate the requested figures; narrow accidental gaps
    within a stick/hand silhouette do not define another sprite.
    """
    solid = opaque(image)
    length = image.height if axis == 'y' else image.width
    if axis == 'y':
        counts = [solid.crop((0, i, image.width, i + 1)).histogram()[255]
                  for i in range(length)]
    else:
        counts = [solid.crop((i, 0, i + 1, image.height)).histogram()[255]
                  for i in range(length)]
    runs, start = [], None
    for position, total in enumerate(counts + [1]):
        if total == 0 and start is None:
            start = position
        elif total and start is not None:
            if start > 0 and position < length and position - start >= 2:
                runs.append((start, position))
            start = None
    if len(runs) < count - 1:
        raise ValueError(f'Only {len(runs)} transparent {axis} gutters for {count} source cells.')
    chosen = sorted(sorted(runs, key=lambda run: -(run[1] - run[0]))[:count - 1])
    return [0] + [(start + end) // 2 for start, end in chosen] + [length]


def read_cells(path, rows, source_columns=COLUMNS):
    with Image.open(path) as raw:
        if 'A' not in raw.getbands():
            raise ValueError(f'{path.name} needs genuine transparent alpha, not an RGB backdrop.')
        image = raw.convert('RGBA')
    if image.getchannel('A').getextrema()[0] != 0:
        raise ValueError(f'{path.name} has no transparent background.')
    vertical = grid_boundaries(image, rows, 'y')
    result = []
    for row in range(rows):
        row_image = image.crop((0, vertical[row], image.width, vertical[row + 1]))
        horizontal = grid_boundaries(row_image, source_columns, 'x')
        line = []
        for col in range(source_columns):
            rect = (horizontal[col], vertical[row], horizontal[col + 1], vertical[row + 1])
            cell = image.crop(rect)
            bounds = opaque(cell).getbbox()
            if bounds is None:
                raise ValueError(f'Empty source cell {path.name}: row {row}, column {col}')
            x0, y0, x1, y1 = bounds
            if x0 == 0 or y0 == 0 or x1 == cell.width or y1 == cell.height:
                raise ValueError(f'Artwork touches a source cell boundary: {path.name}:{row},{col}')
            crop = cell.crop((max(0, x0 - 2), max(0, y0 - 2),
                              min(cell.width, x1 + 2), min(cell.height, y1 + 2)))
            solid = opaque(crop)
            box = solid.getbbox()
            band_top = max(0, box[3] - max(3, round((box[3] - box[1]) * .12)))
            feet = solid.crop((0, band_top, crop.width, box[3])).getbbox()
            foot = ((feet[0] + feet[2]) / 2 if feet else (box[0] + box[2]) / 2,
                    box[3])
            line.append({'image': crop, 'foot': foot, 'bodyHeight': box[3] - box[1],
                         'sourceColumn': col, 'sourceCell': list(rect),
                         'sourceOpaqueBounds': list(bounds)})
        if source_columns == 11:
            # The final runtime slot is an explicit hold, not a new drawing.
            line.append(dict(line[-1]))
        result.append(line)
    return image, result, vertical


def visible_bytes(image):
    # Transparent RGB does not count as a visible frame change.
    return bytes(channel if pixel[3] else 0 for pixel in image.get_flattened_data() for channel in pixel)


def changed_pixels(before, after):
    return sum(1 for a, b in zip(before.get_flattened_data(), after.get_flattened_data())
               if a != b and (a[3] or b[3]))


def pack_band(source_path, rows, drummer_row, output_path, source_columns=COLUMNS):
    original, cells, row_boundaries = read_cells(source_path, rows, source_columns)
    atlas = Image.new('RGBA', (COLUMNS * CELL, rows * CELL))
    report = {'source': source_path.name, 'sourceSize': list(original.size),
              'sourceSha256': hashlib.sha256(source_path.read_bytes()).hexdigest(),
              'sourceAlphaExtrema': list(original.getchannel('A').getextrema()),
              'file': output_path.name, 'size': list(atlas.size),
              'sourceColumns': source_columns, 'sourceFrameCount': rows * source_columns,
              'runtimeRepeatedColumns': [11] if source_columns == 11 else [],
              'rowBoundaries': row_boundaries, 'missingCells': [], 'rows': []}
    for row, line in enumerate(cells):
        row_report = {'row': row, 'groups': [], 'cells': []}
        for action, columns in [('perform', range(0, 4)), ('walk', range(4, 8)),
                                ('expressions', range(8, 12))]:
            target = 40 if row == drummer_row and action == 'perform' else 56
            entries = [line[col] for col in columns]
            scale = target / median(entry['bodyHeight'] for entry in entries)
            scale = min(scale, 60 / max(entry['image'].width for entry in entries),
                        60 / max(entry['image'].height for entry in entries))
            # Fit every complete sprite at the same floor anchor. The scale is
            # shared within an actor/action group so it cannot pulse each frame.
            for entry in entries:
                crop, (fx, fy) = entry['image'], entry['foot']
                scale = min(scale, (FOOT_X - 1) / max(1, fx),
                            (CELL - FOOT_X - 1) / max(1, crop.width - fx),
                            (FOOT_Y - 1) / max(1, fy),
                            (CELL - FOOT_Y - 1) / max(1, crop.height - fy))
            row_report['groups'].append({'action': action, 'targetBodyHeight': target,
                                         'scale': scale})
            for col in columns:
                entry = line[col]
                crop, (fx, fy) = entry['image'], entry['foot']
                width, height = max(1, round(crop.width * scale)), max(1, round(crop.height * scale))
                resized = crop.resize((width, height), Image.Resampling.NEAREST)
                x, y = round(FOOT_X - fx * scale), round(FOOT_Y - fy * scale)
                if x < 0 or y < 0 or x + width > CELL or y + height > CELL:
                    raise ValueError(f'Cell overflow {source_path.name}:{row},{col}: {(x,y,width,height)}')
                atlas.paste(resized, (col * CELL + x, row * CELL + y))
                row_report['cells'].append({'column': col, 'action': action,
                    'sourceColumn': entry['sourceColumn'],
                    'sourceCell': entry['sourceCell'], 'sourceOpaqueBounds': entry['sourceOpaqueBounds'],
                    'scale': scale, 'placedRect': [x, y, width, height]})
        report['rows'].append(row_report)

    output_path.parent.mkdir(parents=True, exist_ok=True)
    atlas.quantize(colors=256, method=Image.Quantize.FASTOCTREE,
                   dither=Image.Dither.NONE).save(output_path, optimize=True)
    encoded = Image.open(output_path).convert('RGBA')
    report['bytes'] = output_path.stat().st_size
    report['sha256'] = hashlib.sha256(output_path.read_bytes()).hexdigest()
    report['alphaExtrema'] = list(encoded.getchannel('A').getextrema())
    report['transparentPixels'] = encoded.getchannel('A').histogram()[0]
    report['expectedCells'] = rows * COLUMNS
    report['alphaCorners'] = [encoded.getpixel(point)[3] for point in [
        (0, 0), (encoded.width - 1, 0), (0, encoded.height - 1),
        (encoded.width - 1, encoded.height - 1)]]
    for row in report['rows']:
        images = [encoded.crop((col * CELL, row['row'] * CELL,
                                 (col + 1) * CELL, (row['row'] + 1) * CELL))
                  for col in range(COLUMNS)]
        for name, indices in [('perform', range(4)), ('walk', range(4, 8))]:
            frames = [images[index] for index in indices]
            unique = len({hashlib.sha256(visible_bytes(frame)).hexdigest() for frame in frames})
            changes = [changed_pixels(frames[i], frames[(i + 1) % len(frames)])
                       for i in range(len(frames))]
            row[name + 'UniqueFrames'] = unique
            row[name + 'AdjacentChangedPixels'] = changes
            if unique != 4 or not all(changes):
                raise ValueError(f'Non-distinct genuine {name} frames in {source_path.name}, row {row["row"]}')
        for col, frame in enumerate(images):
            box = opaque(frame).getbbox()
            if box is None:
                report['missingCells'].append([row['row'], col])
            row['cells'][col]['packedOpaqueBounds'] = list(box) if box else None
        row['runtimeHeldColumns'] = [col for col in range(1, COLUMNS)
            if visible_bytes(images[col]) == visible_bytes(images[col - 1])]
    if report['missingCells']:
        raise ValueError(f'Empty runtime cells: {report["missingCells"]}')
    report['nonEmptyCells'] = rows * COLUMNS - len(report['missingCells'])
    report['passed'] = True
    return report


def prepare_performing_bands(source=SOURCE, output=OUTPUT):
    source, output = Path(source), Path(output)
    reports = [pack_band(source / filename, rows, drummer, output / target, columns)
               for _, filename, target, rows, drummer, columns in BANDS]
    manifest_path = source / 'band-models.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    if manifest.get('frameWidth') != CELL or manifest.get('columns') != COLUMNS:
        raise ValueError('Artist manifest does not match the retained sheet contract.')
    shutil.copy2(manifest_path, output / 'band-models.json')
    report = {'passed': True, 'cell': [CELL, CELL], 'anchor': [FOOT_X, FOOT_Y],
              'columns': COLUMNS, 'bands': reports}
    (source / 'packing-report.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, default=SOURCE)
    parser.add_argument('--output', type=Path, default=OUTPUT)
    args = parser.parse_args()
    report = prepare_performing_bands(args.source, args.output)
    print(json.dumps({'passed': report['passed'], 'bands': [
        {'file': band['file'], 'size': band['size'], 'bytes': band['bytes'],
         'missingCells': band['missingCells']} for band in report['bands']]}, ensure_ascii=False))


if __name__ == '__main__':
    main()
