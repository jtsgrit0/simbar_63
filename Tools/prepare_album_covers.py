"""Prepare the three verified jackets for the five provided music tracks.

uv run --with pillow==13.0.0 python Tools/prepare_album_covers.py

Original publisher JPEGs and source hashes are retained in ArtSource and
Research. Only uniform, uncropped resizing and RGB PNG encoding are applied.
The cover manifest is optional: boot must not preload it or these images.
"""

import hashlib
import json
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parent.parent
ART = ROOT / "Tools/WebGLTemplate/FastGame/art"
RESEARCH = ROOT / "Research/AlbumJackets"
SIZE = 224
TRACKS = (
    ("Electric Shocker.mp3", "Electric Shocker", "프리윌(freewill)", "Electric Shocker", "freewill-electric-shocker"),
    ("Hey_spotdown.mp3", "Hey", "Julywantsmore", "Hey", "julywantsmore-hey"),
    ("No Matter How.mp3", "No Matter How", "Julywantsmore", "No Matter How", "julywantsmore-no-matter-how"),
    ("Bedroom Manifesto.mp3", "Bedroom Manifesto", "프리윌(freewill)", "Electric Shocker", "freewill-electric-shocker"),
    ("Electric Shocker - Korean Version.mp3", "Electric Shocker (Korean Version)", "프리윌(freewill)", "Electric Shocker", "freewill-electric-shocker"),
)


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def write_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")


def main():
    sources = json.loads((RESEARCH / "downloaded-sources.json").read_text(encoding="utf-8"))["releases"]
    output = ART / "album-covers"
    output.mkdir(parents=True, exist_ok=True)
    covers = {}
    for source in sources:
        original = ROOT / source["original"]
        if sha256(original.read_bytes()) != source["originalSha256"]:
            raise ValueError("Publisher original hash changed: " + original.name)
        image = Image.open(original).convert("RGB")
        image.thumbnail((SIZE, SIZE), Image.Resampling.LANCZOS)
        target = output / (source["id"] + ".png")
        image.save(target, optimize=True, compress_level=9)
        covers[source["id"]] = {**source, "cover": "album-covers/" + target.name,
                                "width": image.width, "height": image.height,
                                "runtimeBytes": target.stat().st_size,
                                "runtimeSha256": sha256(target.read_bytes())}
    total = sum(entry["runtimeBytes"] for entry in covers.values())
    if total > 150000:
        raise ValueError(f"Optional runtime jacket budget exceeded: {total:,} bytes")
    tracks = []
    for index, (filename, title, artist, release, cover_id) in enumerate(TRACKS):
        entry = covers[cover_id]
        tracks.append({"index": index, "filename": filename, "title": title,
                       "artist": artist, "release": release, "cover": entry["cover"],
                       "sourceUrl": entry["sourceUrl"], "sourceImageUrl": entry["sourceImageUrl"],
                       "width": entry["width"], "height": entry["height"]})
    manifest = {"version": 1, "tracks": tracks}
    write_json(ART / "album-covers.json", manifest)
    write_json(RESEARCH / "runtime-packing.json", {
        "version": 1, "method": "Uniform LANCZOS resize within 224x224; full uncropped artwork; 24-bit RGB PNG; no palette quantization, redraw or synthesis",
        "optionalLazyAssets": True, "runtimeTotalBytes": total, "uniqueJackets": len(covers),
        "manifest": "Tools/WebGLTemplate/FastGame/art/album-covers.json",
        "covers": list(covers.values())})
    print(json.dumps({"tracks": len(tracks), "uniqueJackets": len(covers), "runtimeBytes": total}))


if __name__ == "__main__":
    main()
