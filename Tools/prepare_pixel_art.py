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

if __name__ == '__main__':
    main()
