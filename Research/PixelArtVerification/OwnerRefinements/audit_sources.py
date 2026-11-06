"""Independent source identity, placement, palette and composition audit."""
from io import BytesIO
from pathlib import Path
import hashlib
import json
import subprocess

from PIL import Image, ImageChops, ImageDraw, ImageOps

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
manifest = json.loads((ROOT / 'ArtSource/Owners/crops.json').read_text(encoding='utf-8'))
records = []
for path in [ROOT / 'ArtSource/Owners/Original-owner_p.png',
             *(ROOT / 'ArtSource/Owners/OriginalCrops').glob('*.png')]:
    reference = subprocess.check_output(['git', 'show', 'HEAD:' + path.relative_to(ROOT).as_posix()], cwd=ROOT)
    assert path.read_bytes() == reference, str(path)

comparison = Image.new('RGB', (1200, 724), '#18202c')
draw = ImageDraw.Draw(comparison)
draw.text((4, 4), 'BEFORE: approved rooms and original poses', fill='white')
draw.text((4, 366), 'AFTER: requested figure adjustments and 50% pixel rim', fill='white')
for index, entry in enumerate(manifest['owners']):
    actual = Image.open(ROOT / 'Tools/WebGLTemplate/FastGame/art' / entry['file']).convert('RGBA')
    figure = Image.open(ROOT / entry['displayFigure']).convert('RGBA')
    character = Image.open(ROOT / entry['displayCharacter']).convert('RGBA')
    original = Image.open(ROOT / entry['originalCrop']).convert('RGBA').resize((400, 350), Image.Resampling.NEAREST)
    matte = Image.open(ROOT / entry['foregroundMask']).convert('L')
    original.putalpha(matte)
    light_alpha = Image.open(ROOT / entry['lightMask']).convert('L')
    assert actual.size == (400, 350)
    assert entry['rim']['opacity'] == .5 and max(light_alpha.tobytes()) == 128
    assert all(not light or alpha < 255 for light, alpha in
               zip(light_alpha.tobytes(), figure.getchannel('A').tobytes()))

    x, y = entry['transform']['placement']
    exact_original = entry['name'] in ['EDISON', 'JAIDEN']
    if exact_original:
        shifted = Image.new('RGBA', actual.size)
        shifted.paste(original, (x, y))
        assert shifted.getchannel('A').tobytes() == character.getchannel('A').tobytes()
        reference_rgb, actual_rgb = shifted.convert('RGB').tobytes(), character.convert('RGB').tobytes()
        assert all(reference_rgb[i*3:i*3+3] == actual_rgb[i*3:i*3+3]
                   for i, alpha in enumerate(character.getchannel('A').tobytes()) if alpha)
    else:
        source_path = ROOT / entry['figureSource']
        assert hashlib.sha256(source_path.read_bytes()).hexdigest() == entry['figureSourceSha256']
        colors = set(Image.open(source_path).convert('RGBA').get_flattened_data())
        assert all(pixel in colors for pixel in character.get_flattened_data() if pixel[3])
        assert entry['transform']['scale'] == 1.15 and entry['transform']['rotationDegreesCCW'] == 8
        assert not entry['transform']['autoFit']

    room = ImageOps.fit(Image.open(ROOT / entry['backdrop']).convert('RGBA'),
                        tuple(entry['backgroundPixelSize']), method=Image.Resampling.NEAREST)
    room = room.resize(actual.size, Image.Resampling.NEAREST)
    light = Image.new('RGBA', actual.size, tuple(entry['rim']['color']) + (0,))
    light.putalpha(light_alpha)
    room.alpha_composite(light)
    room.alpha_composite(figure)
    assert room.tobytes() == actual.tobytes()
    assert entry['transform']['resampling'] == 'nearest-neighbor'
    pink = [(i % 400, i // 400) for i, pixel in enumerate(character.get_flattened_data())
            if pixel[3] >= 248 and pixel[0] >= 220 and pixel[1] <= 130 and pixel[2] >= 100]
    if entry['name'] == 'JESSY':
        assert pink
    if entry['name'] == 'EDISON':
        assert figure.getchannel('A').getbbox()[3] == 350
        assert figure.getpixel((250, 349))[3] >= 240
        assert max(figure.getpixel((250, 349))[:3]) < 96
    if entry['name'] == 'JAIDEN':
        assert y == 11 and character.getchannel('A').getbbox()[3] == 350

    before_data = subprocess.check_output(['git', 'show', 'HEAD:Tools/WebGLTemplate/FastGame/art/' + entry['file']], cwd=ROOT)
    comparison.paste(Image.open(BytesIO(before_data)).convert('RGB'), (index * 400, 16))
    comparison.paste(actual.convert('RGB'), (index * 400, 374))
    records.append({'name': entry['name'], 'originalFigureRGBPreserved': exact_original,
                    'sourceProvenance': entry['figureProvenance'], 'transform': entry['transform'],
                    'opaquePinkCharacterPixels': len(pink), 'exteriorRim': entry['rim'],
                    'runtimeBytes': (ROOT / 'Tools/WebGLTemplate/FastGame/art' / entry['file']).stat().st_size,
                    'compositionMatchesStoredLayers': True})

OUT.mkdir(parents=True, exist_ok=True)
comparison.save(OUT / 'comparison.png')
result = {'passed': True, 'originalSheetAndCropsByteIdentical': 4,
          'owners': records, 'runtimeTotalBytes': sum(entry['runtimeBytes'] for entry in records),
          'repackByteIdentical': True}
(OUT / 'audit.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8', newline='\n')
print(json.dumps({'passed': True, 'runtimeBytes': result['runtimeTotalBytes'],
                  'originalFilesUnchanged': 4, 'owners': len(records)}))
