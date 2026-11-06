"""Pack the user's original owner sheet, retaining its original portrait pixels.

This is deterministic UI cropping, with no generation, redraw, or recoloring.
The simple clip paths exclude neighboring portraits and baked labels. Full-size
lossless crops retain the original pixels in ArtSource. Runtime copies use only
nearest-neighbor pixel selection, sized for the 400x350 owner-card display.
An 8:7 paper canvas retains the hair, hands, headphones, laptop, and record.
"""
from pathlib import Path
import hashlib
import json

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / 'ArtSource/Owners/Original-owner_p.png'
OUTPUT = ROOT / 'Tools/WebGLTemplate/FastGame/art'
FULL_CROPS = ROOT / 'ArtSource/Owners/OriginalCrops'
SOURCE_SHA256 = 'a030c8958e0a4aa7e881bf1028f145e9b6f6a3a9e03c6c2bf08648c8b476b36c'

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


def prepare_owners(output=OUTPUT, runtime_size=(400, 350)):
    if not SOURCE.is_file():
        raise FileNotFoundError('The supplied original owner sheet is required: ' + str(SOURCE))
    source = Image.open(SOURCE).convert('RGBA')
    if source.size != (1086, 1448):
        raise ValueError('Owner clip coordinates require the original 1086x1448 sheet.')
    if hashlib.sha256(SOURCE.read_bytes()).hexdigest() != SOURCE_SHA256:
        raise ValueError('The original owner sheet changed; review its clip coordinates first.')
    output.mkdir(parents=True, exist_ok=True)
    FULL_CROPS.mkdir(parents=True, exist_ok=True)
    background = source.getpixel((10, 10))
    manifest = {'source': SOURCE.relative_to(ROOT).as_posix(),
                'sha256': hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
                'method': 'Lossless original-pixel polygon crops; nearest-neighbor runtime copies; no generation or recoloring',
                'owners': []}
    for owner in OWNERS:
        mask = Image.new('L', source.size)
        ImageDraw.Draw(mask).polygon(owner['clip'], fill=255)
        bounds = mask.getbbox()
        crop = source.crop(bounds)
        clip = mask.crop(bounds)
        image = Image.new('RGBA', owner['size'], background)
        offset = ((image.width - crop.width) // 2, (image.height - crop.height) // 2)
        if min(offset) < 0:
            raise ValueError('Original portrait does not fit its paper canvas: ' + owner['name'])
        image.paste(crop, offset, clip)
        full_target = FULL_CROPS / (owner['name'].title() + '.png')
        image.save(full_target, optimize=True)
        target = output / owner['file']
        image.resize(runtime_size, Image.Resampling.NEAREST).save(target, optimize=True)
        manifest['owners'].append({**owner, 'bounds': bounds, 'offset': offset,
                                   'originalCrop': full_target.relative_to(ROOT).as_posix(),
                                   'runtimeSize': runtime_size,
                                   'sha256': hashlib.sha256(target.read_bytes()).hexdigest(),
                                   'bytes': target.stat().st_size})
        print(f"original {owner['name']}: {image.width}x{image.height}; runtime {runtime_size[0]}x{runtime_size[1]}, {target.stat().st_size:,} bytes")
    return manifest


if __name__ == '__main__':
    manifest = prepare_owners()
    (ROOT / 'ArtSource/Owners/crops.json').write_text(
        json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
