"""Encode retained ImageGen production art for the native game. No creative edits.

Run: uv run --with pillow python Tools/prepare_pixel_art.py
Nearest-neighbor sizing preserves pixel clusters; PNG retains alpha losslessly.
"""
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / 'ArtSource/PixelArt'
OUTPUT = ROOT / 'Tools/WebGLTemplate/FastGame/art'
ASSETS = {
    'VenueEmpty.png': ('venue.png', (960, 558)),
    'CharacterAtlas.png': ('sprites.png', (256, 512)),
    'OwnerFemale.png': ('owner-female.png', (240, 300)),
    'OwnerFF.png': ('owner-ff.png', (240, 300)),
    'OwnerPetSounds.png': ('owner-pet.png', (240, 300)),
    'SeoulNightDistricts.png': ('seoul.png', (640, 360)),
}
V2_ASSETS = {
    'VenueEmpty.png': ('venue.png', (960, 558)),
    'OwnerFemale.png': ('owner-female.png', (240, 300)),
    'OwnerFF.png': ('owner-ff.png', (240, 300)),
    'OwnerPetSounds.png': ('owner-pet.png', (240, 300)),
    'Wardrobe.png': ('wardrobe.png', (256, 512)),
    'Walk.png': ('walk.png', (256, 512)),
    'Pit.png': ('pit.png', (256, 512)),
    'Poses.png': ('poses.png', (256, 512)),
    'Band.png': ('band.png', (256, 256)),
}

def pack_pose_cutout(path):
    """Pack generated cutout cells without stretching their adult proportions.

    Background extraction retained the 4x8 layout but changed inter-row spacing.
    Alpha is used only to measure cell bounds; generated pixels are unchanged.
    """
    source = Image.open(path).convert('RGBA')
    alpha = source.getchannel('A')
    rows = [y for y in range(source.height)
            if sum(v > 128 for v in alpha.crop((0, y, source.width, y + 1)).get_flattened_data()) > 8]
    bands = []
    start = previous = rows[0]
    for y in rows[1:]:
        if y - previous > 12:
            bands.append((start, previous + 1))
            start = y
        previous = y
    bands.append((start, previous + 1))
    if len(bands) != 8:
        raise ValueError(f'Expected 8 isolated cutout rows, found {len(bands)}')
    atlas = Image.new('RGBA', (256, 512))
    scale = 56 / max(end - start for start, end in bands[:7])
    for row, (top, bottom) in enumerate(bands):
        for col in range(4):
            left, right = round(col * source.width / 4), round((col + 1) * source.width / 4)
            cell = source.crop((left, top, right, bottom))
            bounds = cell.getchannel('A').point(lambda a: 255 if a > 128 else 0).getbbox()
            if not bounds:
                raise ValueError(f'Missing pose {row}, {col}')
            # Retain a small margin of the original generated edge alpha.
            x0, y0, x1, y1 = bounds
            cell = cell.crop((max(0, x0 - 2), max(0, y0 - 2), min(cell.width, x1 + 2), min(cell.height, y1 + 2)))
            sizing = min(scale, 58 / cell.width, 57 / cell.height)
            if row == 7:
                sizing = min(58 / cell.width, 36 / cell.height)
            cell = cell.resize((max(1, round(cell.width * sizing)), max(1, round(cell.height * sizing))), Image.Resampling.NEAREST)
            x = col * 64 + (64 - cell.width) // 2
            y = row * 64 + ((64 - cell.height) // 2 if row == 7 else 60 - cell.height)
            atlas.paste(cell, (x, y))
    return atlas

def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for name, (target, size) in ASSETS.items():
        image = Image.open(SOURCE / name).convert('RGBA')
        image = image.resize(size, Image.Resampling.NEAREST)
        if target == 'sprites.png':
            # Atlas packing only: retain every frame and its generated alpha.
            guests = Image.open(SOURCE / 'GuestAtlas.png').convert('RGBA')
            guests = guests.resize((256, 384), Image.Resampling.NEAREST)
            atlas = Image.new('RGBA', (256, 896))
            atlas.paste(image, (0, 0))
            atlas.paste(guests, (0, 512))
            image = atlas
            size = image.size
        image = image.quantize(colors=256, method=Image.Quantize.FASTOCTREE,
                               dither=Image.Dither.NONE)
        image.save(OUTPUT / target, optimize=True)
        print(f'{target}: {size[0]}x{size[1]}, {(OUTPUT / target).stat().st_size:,} bytes')
    if (SOURCE / 'V2').is_dir():
        for name, (target, size) in V2_ASSETS.items():
            cutout = SOURCE / 'V2/PosesCutout.png'
            if target == 'poses.png' and cutout.exists():
                image = pack_pose_cutout(cutout)
            else:
                image = Image.open(SOURCE / 'V2' / name).convert('RGBA')
                image = image.resize(size, Image.Resampling.NEAREST)
            image = image.quantize(colors=256, method=Image.Quantize.FASTOCTREE,
                                   dither=Image.Dither.NONE)
            image.save(OUTPUT / target, optimize=True)
            print(f'v2 {target}: {size}, {(OUTPUT / target).stat().st_size:,} bytes')
    musicians = SOURCE / 'V3/VenueMusicians.png'
    if musicians.exists():
        image = Image.open(musicians).convert('RGBA').resize((960, 558), Image.Resampling.NEAREST)
        image.quantize(colors=256, method=Image.Quantize.FASTOCTREE,
                       dither=Image.Dither.NONE).save(OUTPUT / 'venue.png', optimize=True)
    no_plants = SOURCE / 'V4/VenueForeignMusicians.png'
    if not no_plants.exists():
        no_plants = SOURCE / 'V4/VenueNoPlants.png'
    if no_plants.exists():
        image = Image.open(no_plants).convert('RGBA').resize((960, 558), Image.Resampling.NEAREST)
        image.quantize(colors=256, method=Image.Quantize.FASTOCTREE,
                       dither=Image.Dither.NONE).save(OUTPUT / 'venue.png', optimize=True)
    for name, target in [('BandPerformance.png', 'band-performance.png'),
                         ('BandWalk.png', 'band-walk.png'),
                         ('BandSocial.png', 'band-social.png'),
                         ('GuestSocial.png', 'guest-social.png')]:
        path = SOURCE / 'V3' / name
        if not path.exists():
            continue
        image = Image.open(path).convert('RGBA')
        if image.width % 64 or image.height % 64:
            raise ValueError(f'Invalid original-FBX frame grid: {name}')
        image = image.quantize(colors=256, method=Image.Quantize.FASTOCTREE,
                               dither=Image.Dither.NONE)
        image.save(OUTPUT / target, optimize=True)
        print(f'FBX {target}: {image.size}, {(OUTPUT / target).stat().st_size:,} bytes')
    # Reuse the original supplied UI glyphs, excluding their baked English labels.
    for name, bounds in [('upgrades', (8, 5, 33, 31)),
                         ('guest', (8, 4, 31, 34)),
                         ('events', (7, 5, 31, 33))]:
        image = Image.open(ROOT / 'Assets/Art/Img' / (name + '.png')).convert('RGBA')
        image.crop(bounds).save(OUTPUT / 'icons' / (name + '.png'), optimize=True)
    # The user's original owner portraits take priority over all generated sets.
    # Keep this last so an older V1/V2 owner export cannot replace their artwork.
    from prepare_original_owners import prepare_owners
    prepare_owners(OUTPUT)

if __name__ == '__main__':
    main()
