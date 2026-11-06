"""Pack reviewed owner figures over their restored live-house backdrops.

The supplied sheet and full-size paper crops remain unchanged. Foreground
matting is deterministic: flood only border-connected ivory paper, plus the
two enclosed paper openings in Jessy's original hair. Edison and Jaiden retain
original figure RGB. Jessy's requested hand-pose edit is a separately reviewed
source. Presentation transforms use nearest-neighbor sampling and a separate
50-percent exterior pixel-light layer. Original source files stay unchanged.
"""
from collections import deque
from pathlib import Path
import hashlib
import json

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageOps

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / 'ArtSource/Owners/Original-owner_p.png'
OUTPUT = ROOT / 'Tools/WebGLTemplate/FastGame/art'
FULL_CROPS = ROOT / 'ArtSource/Owners/OriginalCrops'
BACKDROPS = ROOT / 'ArtSource/Owners/Backdrops'
FOREGROUND_MASKS = ROOT / 'ArtSource/Owners/ForegroundMasks'
REFINEMENTS = ROOT / 'ArtSource/Owners/Refinements'
PRESENTATION = REFINEMENTS / 'presentation.json'
DISPLAY_FIGURES = REFINEMENTS / 'DisplayFigures'
DISPLAY_MASKS = REFINEMENTS / 'DisplayMasks'
LIGHT_MASKS = REFINEMENTS / 'LightMasks'
SOURCE_SHA256 = 'a030c8958e0a4aa7e881bf1028f145e9b6f6a3a9e03c6c2bf08648c8b476b36c'
RUNTIME_SIZE = (400, 350)
BACKGROUND_PIXEL_SIZE = (200, 175)
RIM_COLORS = {'JESSY': (255, 62, 154), 'EDISON': (42, 211, 255),
              'JAIDEN': (255, 167, 48)}

# These are empty openings between hair outlines, not highlights or skin.
# Other enclosed pale regions (eyes, headphones, laptop and white shirt) stay.
PAPER_OPENINGS = {'JESSY': ((184, 47), (260, 36)), 'EDISON': (), 'JAIDEN': ()}

# Coordinates refer directly to the 1086x1448 supplied sheet. Polygons follow
# empty paper between the figures, never a face, hand, hair, or accessory.
OWNERS = (
    {'name': 'JESSY', 'id': 'female', 'file': 'owner-female.png',
     'asset': 'OwnerFemale', 'size': (800, 700),
     'clip': [(194, 17), (885, 17), (885, 437), (765, 437),
              (704, 481), (685, 547), (676, 598), (649, 644),
              (620, 704), (441, 704), (442, 641), (365, 635),
              (300, 631), (250, 623), (210, 611), (194, 589)]},
    {'name': 'EDISON', 'id': 'ff', 'file': 'owner-ff.png',
     'asset': 'OwnerFF', 'size': (656, 574),
     'clip': [(829, 440), (968, 449), (1056, 523), (1056, 1006),
              (450, 1006), (450, 943), (487, 941), (489, 893),
              (500, 847), (523, 795), (566, 749), (604, 719),
              (661, 709), (666, 655), (691, 632), (679, 600),
              (683, 551), (713, 500), (766, 462)]},
    {'name': 'JAIDEN', 'id': 'pet-sounds', 'file': 'owner-pet.png',
     'asset': 'OwnerPetSounds', 'size': (640, 560),
     'clip': [(14, 701), (483, 701), (483, 935), (450, 935),
              (450, 1250), (14, 1250)]},
)


def original_portrait(source, owner):
    """Return the same full-size paper crop that was approved previously."""
    mask = Image.new('L', source.size)
    ImageDraw.Draw(mask).polygon(owner['clip'], fill=255)
    bounds = mask.getbbox()
    crop = source.crop(bounds)
    image = Image.new('RGBA', owner['size'], source.getpixel((10, 10)))
    offset = ((image.width - crop.width) // 2, (image.height - crop.height) // 2)
    if min(offset) < 0:
        raise ValueError('Original portrait does not fit its canvas: ' + owner['name'])
    image.paste(crop, offset, mask.crop(bounds))
    return image, bounds, offset


def foreground_mask(portrait, name):
    """Technical paper matte; never key pale enclosed clothing or face areas."""
    if portrait.size != RUNTIME_SIZE:
        raise ValueError('Reviewed foreground matte requires the 400x350 crop.')
    width, height = portrait.size
    rgb = portrait.convert('RGB').tobytes()
    pixels = zip(rgb[0::3], rgb[1::3], rgb[2::3])
    paper = [r >= 225 and g >= 202 and b >= 185 and r - b <= 72
             for r, g, b in pixels]
    removed = bytearray(width * height)
    queue = deque()

    def seed(x, y):
        index = y * width + x
        if paper[index] and not removed[index]:
            removed[index] = 1
            queue.append(index)

    for x in range(width):
        seed(x, 0)
        seed(x, height - 1)
    for y in range(height):
        seed(0, y)
        seed(width - 1, y)
    for x, y in PAPER_OPENINGS[name]:
        if not paper[y * width + x]:
            raise ValueError('Reviewed enclosed-paper seed moved: ' + name)
        seed(x, y)
    while queue:
        index = queue.popleft()
        x, y = index % width, index // width
        if x > 0:
            seed(x - 1, y)
        if x + 1 < width:
            seed(x + 1, y)
        if y > 0:
            seed(x, y - 1)
        if y + 1 < height:
            seed(x, y + 1)
    matte = Image.frombytes('L', portrait.size,
                            bytes(0 if value else 255 for value in removed))
    return matte


def composite_owner(portrait, matte, backdrop):
    """A pixel-aligned room layer below the exact original character pixels."""
    room = ImageOps.fit(backdrop.convert('RGBA'), BACKGROUND_PIXEL_SIZE,
                        method=Image.Resampling.NEAREST, centering=(0.5, 0.5))
    room = room.resize(RUNTIME_SIZE, Image.Resampling.NEAREST)
    result = room.copy()
    result.paste(portrait, (0, 0), matte)
    # This is an invariant, not a claim of approximate likeness preservation.
    foreground = matte.tobytes()
    original = portrait.convert('RGB').tobytes()
    combined = result.convert('RGB').tobytes()
    if any(original[i * 3:i * 3 + 3] != combined[i * 3:i * 3 + 3]
           for i, alpha in enumerate(foreground) if alpha):
        raise ValueError('Original character RGB changed during composition.')
    return result


def relative(path):
    return Path(path).relative_to(ROOT).as_posix()


def file_hash(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def place_layer(canvas, layer, x, y):
    """Composite an integer-positioned layer, clipping only at the card border."""
    x, y = int(x), int(y)
    left, top = max(0, x), max(0, y)
    right, bottom = min(canvas.width, x + layer.width), min(canvas.height, y + layer.height)
    if right > left and bottom > top:
        fragment = layer.crop((left - x, top - y, right - x, bottom - y))
        canvas.alpha_composite(fragment, (left, top))


def original_figure(portrait, matte):
    figure = portrait.copy()
    figure.putalpha(matte)
    return figure


def clean_transparency(image, noise_floor):
    """Ignore nearly invisible generated coverage; keep source files and RGB intact."""
    if noise_floor:
        image = image.copy()
        image.putalpha(image.getchannel('A').point(lambda alpha: alpha if alpha >= noise_floor else 0))
    return image


def load_refinements():
    if not PRESENTATION.is_file():
        return None
    data = json.loads(PRESENTATION.read_text(encoding='utf-8'))
    if data.get('version') != 1 or tuple(data.get('runtimeSize', ())) != RUNTIME_SIZE:
        raise ValueError('Owner refinement presentation requires version 1 and 400x350 cards.')
    if set(data.get('owners', {})) != {owner['name'] for owner in OWNERS}:
        raise ValueError('All three owner refinement configurations are required.')
    return data


def refined_figure(portrait, matte, name, config):
    """Apply only pixel sampling, RGBA placement and a separate exterior light ring."""
    source = original_figure(portrait, matte)
    provenance = 'approved original figure RGB'
    metadata = {'originalForegroundMask': relative(FOREGROUND_MASKS / (name.title() + '.png'))}
    if config.get('source'):
        path = ROOT / config['source']
        if not path.is_file():
            raise FileNotFoundError('Reviewed owner refinement source is required: ' + str(path))
        source = Image.open(path).convert('RGBA')
        alpha = source.getchannel('A')
        if alpha.getextrema() != (0, 255) or not alpha.getbbox():
            raise ValueError('Edited foreground must be a reviewed transparent cutout: ' + name)
        source = clean_transparency(source, int(config.get('transparencyNoiseFloor', 0)))
        source = source.resize(RUNTIME_SIZE, Image.Resampling.NEAREST)
        metadata.update({'figureSource': relative(path), 'figureSourceSha256': file_hash(path)})
        provenance = config.get('provenance', 'reviewed generated foreground edit')

    angle = float(config.get('rotationDegreesCCW', 0))
    scale = float(config.get('scale', 1))
    transformed = source
    if angle:
        transformed = transformed.rotate(angle, Image.Resampling.NEAREST, expand=True,
                                         fillcolor=(0, 0, 0, 0))
    if scale != 1:
        size = (round(transformed.width * scale), round(transformed.height * scale))
        transformed = transformed.resize(size, Image.Resampling.NEAREST)
    bounds = transformed.getchannel('A').getbbox()
    if not bounds:
        raise ValueError('Owner figure has no visible pixels: ' + name)
    if angle or scale != 1:
        transformed = transformed.crop(bounds)
        x = (RUNTIME_SIZE[0] - transformed.width) // 2 + int(config.get('xOffset', 0))
        y = int(config.get('top', 2))
    else:
        x, y = int(config.get('xOffset', 0)), int(config.get('yOffset', 0))
        if config.get('align') == 'bottom':
            y = RUNTIME_SIZE[1] - bounds[3]

    character = Image.new('RGBA', RUNTIME_SIZE)
    place_layer(character, transformed, x, y)
    figure = Image.new('RGBA', RUNTIME_SIZE)
    support_meta = {}
    if config.get('support'):
        support_path = ROOT / config['support']
        support = clean_transparency(Image.open(support_path).convert('RGBA'),
                                     int(config.get('transparencyNoiseFloor', 0)))
        support_bounds = support.getchannel('A').getbbox()
        if not support_bounds or support.getchannel('A').getextrema()[0] != 0:
            raise ValueError('DJ support requires a transparent source asset.')
        support = support.crop(support_bounds)
        rectangle = config['supportRect']
        size = (int(rectangle[2] - rectangle[0]), int(rectangle[3] - rectangle[1]))
        support = ImageOps.fit(support, size, method=Image.Resampling.NEAREST)
        place_layer(figure, support, rectangle[0], rectangle[1])
        support_meta = {'supportSource': relative(support_path),
                        'supportSha256': file_hash(support_path), 'supportRect': rectangle}
    figure.alpha_composite(character)
    display_alpha = figure.getchannel('A')
    radius = int(config.get('rimWidth', 2))
    opacity = float(config.get('rimOpacity', .5))
    if radius < 1 or not 0 <= opacity <= 1:
        raise ValueError('Owner rim requires a positive width and normalized opacity.')
    outer = ImageChops.subtract(display_alpha.filter(ImageFilter.MaxFilter(radius * 2 + 1)),
                                display_alpha)
    light_alpha = outer.point(lambda value: round(value * opacity))
    color = tuple(config.get('rimColor', RIM_COLORS[name]))
    light = Image.new('RGBA', RUNTIME_SIZE, color + (0,))
    light.putalpha(light_alpha)

    for folder in (DISPLAY_FIGURES, DISPLAY_MASKS, LIGHT_MASKS):
        folder.mkdir(parents=True, exist_ok=True)
    character_path = DISPLAY_FIGURES / (name.title() + '-character.png')
    figure_path = DISPLAY_FIGURES / (name.title() + '.png')
    alpha_path = DISPLAY_MASKS / (name.title() + '.png')
    light_path = LIGHT_MASKS / (name.title() + '.png')
    character.save(character_path, optimize=True)
    figure.save(figure_path, optimize=True)
    display_alpha.save(alpha_path, optimize=True)
    light_alpha.save(light_path, optimize=True)
    metadata.update({
        'presentationVersion': 1, 'figureProvenance': provenance,
        'displayCharacter': relative(character_path), 'displayCharacterSha256': file_hash(character_path),
        'displayFigure': relative(figure_path), 'displayFigureSha256': file_hash(figure_path),
        'displayForegroundMask': relative(alpha_path), 'displayForegroundMaskSha256': file_hash(alpha_path),
        'lightMask': relative(light_path), 'lightMaskSha256': file_hash(light_path),
        'transform': {'rotationDegreesCCW': angle, 'scale': scale, 'placement': [x, y],
                      'unclippedSize': transformed.size, 'displayBounds': display_alpha.getbbox(),
                      'resampling': 'nearest-neighbor', 'autoFit': False},
        'rim': {'color': color, 'opacity': opacity, 'width': radius, 'maxAlpha': max(light_alpha.tobytes())},
        'transparencyNoiseFloor': int(config.get('transparencyNoiseFloor', 0)),
        **support_meta,
    })
    return figure, light, metadata


def composite_refined_owner(portrait, matte, backdrop, name, config):
    room = ImageOps.fit(backdrop.convert('RGBA'), BACKGROUND_PIXEL_SIZE,
                        method=Image.Resampling.NEAREST, centering=(.5, .5))
    room = room.resize(RUNTIME_SIZE, Image.Resampling.NEAREST)
    figure, light, metadata = refined_figure(portrait, matte, name, config)
    room.alpha_composite(light)
    room.alpha_composite(figure)
    return room, metadata


def prepare_owners(output=OUTPUT, runtime_size=RUNTIME_SIZE):
    if not SOURCE.is_file():
        raise FileNotFoundError('The supplied original owner sheet is required: ' + str(SOURCE))
    source = Image.open(SOURCE).convert('RGBA')
    if source.size != (1086, 1448):
        raise ValueError('Owner clip coordinates require the original 1086x1448 sheet.')
    if hashlib.sha256(SOURCE.read_bytes()).hexdigest() != SOURCE_SHA256:
        raise ValueError('The original owner sheet changed; review its clip coordinates first.')
    output.mkdir(parents=True, exist_ok=True)
    FULL_CROPS.mkdir(parents=True, exist_ok=True)
    backgrounds = [BACKDROPS / (owner['name'].title() + '.png') for owner in OWNERS]
    use_backdrops = any(path.exists() for path in backgrounds)
    if use_backdrops and not all(path.is_file() for path in backgrounds):
        raise FileNotFoundError('All three restored owner backdrops are required before packing.')
    if use_backdrops and runtime_size != RUNTIME_SIZE:
        raise ValueError('Backdrop portraits use the reviewed 400x350 display size.')
    if use_backdrops:
        FOREGROUND_MASKS.mkdir(parents=True, exist_ok=True)
    refinements = load_refinements()
    if refinements and not use_backdrops:
        raise ValueError('Owner refinements require the restored room backdrops.')
    manifest = {'source': SOURCE.relative_to(ROOT).as_posix(),
                'sha256': hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
                'method': ('Reviewed figure refinements, nearest-neighbor transforms and exterior pixel rim over restored rooms'
                           if refinements else
                           'Original-RGB figure matte over separately restored room backdrops; nearest-neighbor only'
                           if use_backdrops else
                           'Lossless original-pixel polygon crops; nearest-neighbor runtime copies; no generation or recoloring'),
                'owners': []}
    for owner, background_path in zip(OWNERS, backgrounds):
        image, bounds, offset = original_portrait(source, owner)
        full_target = FULL_CROPS / (owner['name'].title() + '.png')
        if full_target.is_file():
            if Image.open(full_target).convert('RGBA').tobytes() != image.tobytes():
                raise ValueError('Approved full-size original crop changed: ' + owner['name'])
        else:
            image.save(full_target, optimize=True)
        runtime = image.resize(runtime_size, Image.Resampling.NEAREST)
        extra = {}
        if use_backdrops:
            matte = foreground_mask(runtime, owner['name'])
            matte_path = FOREGROUND_MASKS / (owner['name'].title() + '.png')
            matte.save(matte_path, optimize=True)
            original_runtime = runtime
            runtime = composite_owner(runtime, matte, Image.open(background_path))
            extra = {'backdrop': background_path.relative_to(ROOT).as_posix(),
                     'backdropSha256': hashlib.sha256(background_path.read_bytes()).hexdigest(),
                     'backgroundPixelSize': BACKGROUND_PIXEL_SIZE,
                     'foregroundMask': matte_path.relative_to(ROOT).as_posix(),
                     'foregroundMaskSha256': hashlib.sha256(matte_path.read_bytes()).hexdigest(),
                     'paperOpeningSeeds': PAPER_OPENINGS[owner['name']],
                     'retainedCharacterPixels': sum(value != 0 for value in matte.tobytes()),
                     'retainedCharacterRGBExact': True}
            if refinements:
                runtime, presentation = composite_refined_owner(
                    original_runtime, matte, Image.open(background_path), owner['name'],
                    refinements['owners'][owner['name']])
                extra.update(presentation)
                # Translation preserves Jaiden's RGB, but only Edison keeps the old positions.
                extra['retainedCharacterRGBExact'] = owner['name'] in ('EDISON', 'JAIDEN')
                extra['retainedCharacterPositionsExact'] = owner['name'] == 'EDISON'
        target = output / owner['file']
        runtime.save(target, optimize=True)
        manifest['owners'].append({**owner, 'bounds': bounds, 'offset': offset,
                                   'originalCrop': full_target.relative_to(ROOT).as_posix(),
                                   'runtimeSize': runtime_size,
                                   **extra,
                                   'sha256': hashlib.sha256(target.read_bytes()).hexdigest(),
                                   'bytes': target.stat().st_size})
        print(f"original {owner['name']}: {image.width}x{image.height}; runtime {runtime_size[0]}x{runtime_size[1]}, {target.stat().st_size:,} bytes")
    (ROOT / 'ArtSource/Owners/crops.json').write_text(
        json.dumps(manifest, indent=2) + '\n', encoding='utf-8', newline='\n')
    return manifest


if __name__ == '__main__':
    prepare_owners()
