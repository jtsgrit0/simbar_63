"""Fetch credited public posters; transcode WebP to lossless PNG for Unity's decoder.

No resizing, cropping, recoloring or text replacement is performed.
Run: uv run --with pillow python Tools/fetch_scout_posters.py
"""
from pathlib import Path
import hashlib
import io
import json
import time
import urllib.request
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
manifest = ROOT / "Research/Venues/scout-poster-sources.json"
output = ROOT / "Assets/StreamingAssets/ScoutPosters"
output.mkdir(parents=True, exist_ok=True)
records = []
for source in json.loads(manifest.read_text(encoding="utf-8")):
    request = urllib.request.Request(source["url"], headers={"User-Agent": "Mozilla/5.0"})
    for attempt in range(3):
        try:
            with urllib.request.urlopen(request, timeout=20) as response:
                data = response.read()
            break
        except OSError:
            if attempt == 2:
                raise
            time.sleep(1)
    picture = Image.open(io.BytesIO(data))
    destination = output / source["file"]
    if picture.format == "WEBP":
        picture.save(destination, "PNG")
    else:
        destination.write_bytes(data)
    records.append({**source, "sourceFormat": picture.format, "width": picture.width, "height": picture.height,
                    "sourceSha256": hashlib.sha256(data).hexdigest(),
                    "gameSha256": hashlib.sha256(destination.read_bytes()).hexdigest()})
    print(source["file"], picture.size, len(data), "->", destination.stat().st_size)
(manifest.parent / "scout-poster-integrity.json").write_text(json.dumps(records, indent=2), encoding="utf-8")
