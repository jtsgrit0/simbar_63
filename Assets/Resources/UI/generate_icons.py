from pathlib import Path
import math
from PIL import Image, ImageDraw

root = Path(r'E:\simbar_u_f_u\Assets\Resources\UI\IconsPng')
root.mkdir(parents=True, exist_ok=True)

white = (255, 255, 255, 255)
soft = (214, 221, 255, 255)
cyan = (95, 220, 255, 255)
gold = (255, 211, 84, 255)

for name, color in [
    ('cog', white),
    ('music', soft),
    ('playlist-music', soft),
    ('star', gold),
    ('play', cyan),
]:
    img = Image.new('RGBA', (128, 128), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    cx, cy = 64, 64

    if name == 'cog':
        for i in range(8):
            ang = math.radians(i * 45)
            x = cx + (42 + 18) * math.cos(ang)
            y = cy + (42 + 18) * math.sin(ang)
            r = 12
            draw.ellipse((x - r, y - r, x + r, y + r), fill=color)
        draw.ellipse((cx - 42, cy - 42, cx + 42, cy + 42), outline=color, width=10)
        draw.ellipse((cx - 18, cy - 18, cx + 18, cy + 18), outline=color, width=10)

    elif name == 'music':
        draw.rounded_rectangle((34, 28, 50, 96), radius=8, fill=color)
        draw.rounded_rectangle((58, 20, 78, 90), radius=8, fill=color)
        draw.polygon([(76, 18), (94, 30), (94, 72), (76, 60)], fill=color)
        draw.line((48, 96, 48, 110), fill=color, width=8)
        draw.line((74, 92, 74, 106), fill=color, width=8)

    elif name == 'playlist-music':
        draw.rounded_rectangle((26, 24, 92, 32), radius=5, fill=color)
        draw.rounded_rectangle((26, 46, 92, 54), radius=5, fill=color)
        draw.rounded_rectangle((26, 68, 82, 76), radius=5, fill=color)
        draw.rounded_rectangle((88, 20, 102, 82), radius=6, fill=color)
        draw.line((94, 82, 94, 94), fill=color, width=8)

    elif name == 'star':
        pts = []
        for i in range(10):
            ang = math.radians(-90 + i * 36)
            r = 42 if i % 2 == 0 else 18
            x = cx + r * math.cos(ang)
            y = cy + r * math.sin(ang)
            pts.append((int(x), int(y)))
        draw.polygon(pts, fill=color)

    elif name == 'play':
        draw.polygon([(38, 28), (98, 64), (38, 100)], fill=color)

    out = root / f'{name}.png'
    img.save(out)
    print(f'created {out}')
