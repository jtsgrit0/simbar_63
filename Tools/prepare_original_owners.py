"""Pack the original owner figures over their restored live-house backdrops.

The supplied sheet and full-size paper crops remain unchanged. Foreground
matting is deterministic: flood only border-connected ivory paper, plus the
two enclosed paper openings in Jessy's hair. Every retained figure RGB value
is copied from the original nearest-neighbor crop, without redraw or recolor.
The background-only room plates are separately restored production assets.
"""
from collections import deque
from pathlib import Path
import hashlib
import json

from PIL import Image, ImageDraw, ImageOps

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / 'ArtSource/Owners/Original-owner_p.png'
OUTPUT = ROOT / 'Tools/WebGLTemplate/FastGame/art'
FULL_CROPS = ROOT / 'ArtSource/Owners/OriginalCrops'
BACKDROPS = ROOT / 'ArtSource/Owners/Backdrops'
FOREGROUND_MASKS = ROOT / 'ArtSource/Owners/ForegroundMasks'
SOURCE_SHA256 = 'a030c8958e0a4aa7e881bf1028f145e9b6f6a3a9e03c6c2bf08648c8b476b36c'
RUNTIME_SIZE = (400, 350)
BACKGROUND_PIXEL_SIZE = (200, 175)

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
    manifest = {'source': SOURCE.relative_to(ROOT).as_posix(),
                'sha256': hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
                'method': ('Original-RGB figure matte over separately restored room backdrops; nearest-neighbor only'
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
            runtime = composite_owner(runtime, matte, Image.open(background_path))
            extra = {'backdrop': background_path.relative_to(ROOT).as_posix(),
                     'backdropSha256': hashlib.sha256(background_path.read_bytes()).hexdigest(),
                     'backgroundPixelSize': BACKGROUND_PIXEL_SIZE,
                     'foregroundMask': matte_path.relative_to(ROOT).as_posix(),
                     'foregroundMaskSha256': hashlib.sha256(matte_path.read_bytes()).hexdigest(),
                     'paperOpeningSeeds': PAPER_OPENINGS[owner['name']],
                     'retainedCharacterPixels': sum(value != 0 for value in matte.tobytes()),
                     'retainedCharacterRGBExact': True}
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
