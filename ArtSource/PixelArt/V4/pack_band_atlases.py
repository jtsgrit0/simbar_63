"""Technical cell packing for retained V4 ImageGen character art.

Only crop, whole-cell translation, uniform nearest-neighbor sizing and atlas
layout are applied to the retained full-color packed source. Runtime sheets use
the reviewed libimagequant 256-color/no-dither palette; that encoding changes
visible RGBA and is not lossless. No painting, limb transforms, frame synthesis,
mirroring or replacement artwork. Run from workspace:
uv run --python 3.13 --with pillow==13.0.0 --with imagequant==1.1.5 --with zopfli==0.4.3 python ArtSource/PixelArt/V4/pack_band_atlases.py
"""
from pathlib import Path
import hashlib
import json
from statistics import median
import sys
from PIL import Image, ImageDraw

SOURCE = Path(__file__).resolve().parent
ROOT = SOURCE.parents[2]
sys.path.insert(0, str(ROOT / "Tools"))
from encode_band_runtime import save_band_runtime_png, write_encoding_report

OUTPUT = ROOT / "Tools/WebGLTemplate/FastGame/art"
CELL = 64
ANCHOR = (32, 60)
DIRECTIONS = ["FR", "BR", "BL", "FL"]
NEAREST = Image.Resampling.NEAREST
RUNTIME_ENCODING = []


def mask(image):
    return image.getchannel("A").point(lambda a: 255 if a > 128 else 0)


def row_bounds(image, rows):
    """Locate generated inter-row transparent gutters near the expected grid."""
    alpha = image.getchannel("A")
    counts = [sum(v > 128 for v in alpha.crop((0, y, image.width, y + 1)).get_flattened_data())
              for y in range(image.height)]
    bounds = [0]
    step = image.height / rows
    for row in range(1, rows):
        center = row * step
        lo, hi = max(bounds[-1] + 1, round(center - step * .23)), min(image.height - 1, round(center + step * .23))
        minimum = min(counts[lo:hi+1])
        candidates = [y for y in range(lo, hi+1) if counts[y] <= minimum + 1]
        nearest = min(candidates, key=lambda y: abs(y - center))
        run = [nearest]
        y = nearest - 1
        while y >= lo and counts[y] <= minimum + 1:
            run.append(y)
            y -= 1
        y = nearest + 1
        while y <= hi and counts[y] <= minimum + 1:
            run.append(y)
            y += 1
        bounds.append(round(sum(run) / len(run)))
    return bounds + [image.height]


def read_grid(name, rows, cols=8):
    image = Image.open(SOURCE / name).convert("RGBA")
    vertical = row_bounds(image, rows)
    cells = []
    for row in range(rows):
        line = []
        for col in range(cols):
            left, right = round(col * image.width / cols), round((col+1) * image.width / cols)
            cell = image.crop((left, vertical[row], right, vertical[row+1]))
            solid = mask(cell).getbbox()
            if solid is None:
                raise ValueError(f"Missing generated cell {name}:{row},{col}")
            x0, y0, x1, y1 = solid
            # Retain generated edge alpha in a small margin.
            crop = cell.crop((max(0,x0-2), max(0,y0-2), min(cell.width,x1+2), min(cell.height,y1+2)))
            solid = mask(crop).getbbox()
            foot_band = mask(crop).crop((0, max(0,solid[3]-max(3,round((solid[3]-solid[1])*.12))), crop.width, solid[3]))
            foot_box = foot_band.getbbox()
            foot_x = (foot_box[0]+foot_box[2])/2 if foot_box else (solid[0]+solid[2])/2
            line.append({"image":crop, "height":solid[3]-solid[1],
                         "width":solid[2]-solid[0], "foot":(foot_x,solid[3])})
        cells.append(line)
    return cells


def pack(cells, name, reference_columns=None):
    rows, columns = len(cells), len(cells[0])
    atlas = Image.new("RGBA", (columns*CELL, rows*CELL))
    high = Image.new("RGBA", (columns*256, rows*256))
    scales = []
    for row, line in enumerate(cells):
        refs = [line[c]["height"] for c in (reference_columns if reference_columns is not None else range(columns))]
        scale = min(56/median(refs), 58/max(c["width"] for c in line), 58/max(c["height"] for c in line))
        scales.append(scale)
        for col, entry in enumerate(line):
            crop, foot = entry["image"], entry["foot"]
            w, h = max(1,round(crop.width*scale)), max(1,round(crop.height*scale))
            resized = crop.resize((w,h), NEAREST)
            x = round(ANCHOR[0] - foot[0] * scale)
            y = round(ANCHOR[1] - foot[1] * scale)
            # Whole sprites should fit; fail instead of silently clipping artwork.
            if x < 0 or y < 0 or x+w > CELL or y+h > CELL:
                raise ValueError(f"Cell overflow {name}:{row},{col}: {(x,y,w,h)}")
            atlas.paste(resized, (col*CELL+x,row*CELL+y))
            big_scale = scale*4
            big = crop.resize((max(1,round(crop.width*big_scale)),max(1,round(crop.height*big_scale))),NEAREST)
            high.paste(big,(col*256+round(128-foot[0]*big_scale),row*256+round(240-foot[1]*big_scale)))
    # Apply only the reviewed runtime NPC palette. The full-color packed source
    # below retains every generated RGBA value before runtime quantization.
    RUNTIME_ENCODING.append(save_band_runtime_png(atlas, OUTPUT/name))
    high.save(SOURCE/(name.replace("v4-","").replace(".png",".packed-256.png")),optimize=True)
    return atlas, scales


def validate(image, name):
    result = {"file":name, "size":list(image.size), "alphaCorners":[], "rows":[]}
    for p in [(0,0),(image.width-1,0),(0,image.height-1),(image.width-1,image.height-1)]:
        result["alphaCorners"].append(image.getpixel(p)[3])
    for row in range(image.height//CELL):
        frames = [image.crop((col*CELL,row*CELL,(col+1)*CELL,(row+1)*CELL)) for col in range(image.width//CELL)]
        unique = len({hashlib.sha256(frame.tobytes()).hexdigest() for frame in frames})
        changes = []
        for a,b in zip(frames,frames[1:]):
            changes.append(sum(p!=q for p,q in zip(a.get_flattened_data(),b.get_flattened_data())))
        if unique != len(frames) or min(changes) < 4:
            raise ValueError(f"Static generated animation {name} row{row}: {unique}, {changes}")
        boxes = [mask(frame).getbbox() for frame in frames]
        if any(box is None for box in boxes):
            raise ValueError(f"Empty cell {name} row{row}")
        result["rows"].append({"row":row,"distinctFrames":unique,"changedPixelsAdjacent":changes,
                               "solidBounds":boxes})
    return result


def main():
    RUNTIME_ENCODING.clear()
    OUTPUT.mkdir(parents=True,exist_ok=True)
    assets = {}
    for role, source in [
        ("singer","singer-performance.source.png"),
        ("guitar","guitar-performance-facing.source.png"),
        ("bass","bass-performance-facing.source.png"),
        ("drums","drums-performance.source.png")]:
        name = f"v4-band-{role}-performance.png"
        assets[name], _ = pack(read_grid(source,4),name)
    guitar_jump = read_grid("band-jump.source.png",7)[:4]
    bass_jump = read_grid("bass-jump.source.png",4)
    assets["v4-band-jump.png"], _ = pack(guitar_jump+bass_jump,"v4-band-jump.png",[0,7])
    for letter, source in [("a","band-greet-a-transparent.source.png"),("b","band-greet-b.source.png")]:
        name = f"v4-band-greet-{letter}.png"
        assets[name], _ = pack(read_grid(source,8),name,[0,3,7])
    square_walk = read_grid("band-walk.source.png",8)
    walk = []
    for role in range(4):
        for row, side in [(role*2,0),(role*2,4),(role*2+1,0),(role*2+1,4)]:
            walk.append(square_walk[row][side:side+4])
    assets["v4-band-walk.png"], _ = pack(walk,"v4-band-walk.png")
    reports = [validate(image,name) for name,image in assets.items()]
    (SOURCE/"band-atlas-qa.json").write_text(json.dumps(reports,indent=2)+"\n",encoding="utf-8")
    write_encoding_report(RUNTIME_ENCODING)
    # QA contact sheet is technical nearest-neighbor presentation only.
    contact = Image.new("RGBA",(8*128, (4*4+8+8+8+16)*128),(32,35,43,255))
    y = 0
    labels = ImageDraw.Draw(contact)
    for name,image in assets.items():
        contact.alpha_composite(image.resize((image.width*2,image.height*2),NEAREST),(0,y))
        y += image.height*2
    contact.crop((0,0,contact.width,y)).convert("RGB").save(SOURCE/"band-atlas-contact-sheet.png")
    print(json.dumps({"files":[{"name":n,"size":list(i.size)} for n,i in assets.items()],"qa":"band-atlas-qa.json"},indent=2))


if __name__ == "__main__":
    main()
