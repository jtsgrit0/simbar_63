"""Encode the approved eight runtime band sheets with the reviewed NPC palette.

Profile: libimagequant, 256 colors, quality 100, zero dithering, then lossless
PNG recompression. This changes visible RGBA colors/alpha and is not lossless.
Generated and full-color packed sources remain untouched. Owner, guest and
crowd images are excluded. Reproduce from full-color sources with:
  uv run --python 3.13 --with pillow==13.0.0 --with imagequant==1.1.5 --with zopfli==0.4.3 python ArtSource/PixelArt/V4/pack_band_atlases.py
"""

import json
from importlib.metadata import version
import math
from pathlib import Path

from PIL import Image

from optimize_runtime_atlases import ART, FILES, ROOT, optimize, save_runtime_png, visible_sha256


BAND_FILES = tuple(name for name in FILES if name.startswith("v4-band-"))
REPORT = ROOT / "Research/PixelArtVerification/Final/band-runtime-encoding.json"
PROFILE = {"encoder": "libimagequant", "colors": 256, "ditheringLevel": 0,
           "minQuality": 0, "maxQuality": 100, "visibleRgbaLossless": False,
           "pngRecompression": "Zopfli, 5 iterations, exact decoded RGBA"}


def difference(reference, candidate):
    """Measure actual composited display differences including alpha changes."""
    changed, alpha_changed, alpha_max = 0, 0, 0
    total, squares, count, maximum = 0, 0, 0, 0
    for a, b in zip(reference.get_flattened_data(), candidate.get_flattened_data()):
        changed += a != b and (a[3] != 0 or b[3] != 0)
        alpha_changed += a[3] != b[3]
        alpha_max = max(alpha_max, abs(a[3] - b[3]))
        if a[3] or b[3]:
            for background in ((0, 0, 0), (32, 35, 43), (255, 255, 255)):
                for channel in range(3):
                    left = round(a[channel] * a[3] / 255 + background[channel] * (255 - a[3]) / 255)
                    right = round(b[channel] * b[3] / 255 + background[channel] * (255 - b[3]) / 255)
                    error = abs(left - right)
                    total += error
                    squares += error * error
                    count += 1
                    maximum = max(maximum, error)
    return {"visibleRgbaPixelsChanged": changed, "alphaPixelsChanged": alpha_changed,
            "maxAlphaErrorOf255": alpha_max,
            "compositeBackgrounds": [[0, 0, 0], [32, 35, 43], [255, 255, 255]],
            "compositeMeanChannelErrorOf255": round(total / count, 4),
            "compositeChannelRmseOf255": round(math.sqrt(squares / count), 4),
            "maxCompositeChannelErrorOf255": maximum}


def save_band_runtime_png(image, path):
    import imagequant

    path = Path(path)
    if path.parent.resolve() != ART.resolve() or path.name not in BAND_FILES:
        raise ValueError("Palette profile is restricted to the eight V4 band atlases")
    reference = image.convert("RGBA")
    indexed = imagequant.quantize_pil_image(reference, dithering_level=0,
                                           max_colors=256, min_quality=0, max_quality=100)
    decoded = indexed.convert("RGBA")
    report = {"file": path.name, "size": list(reference.size),
              "fullColorVisibleSha256": visible_sha256(reference),
              "runtimeVisibleSha256": visible_sha256(decoded),
              **difference(reference, decoded)}
    save_runtime_png(indexed, path)
    exact = optimize(path, use_zopfli=True)
    if visible_sha256(Image.open(path)) != report["runtimeVisibleSha256"]:
        raise AssertionError("PNG encoding changed the approved quantized output")
    report.update(bytes=path.stat().st_size, fileSha256=exact["fileSha256"])
    return report


def write_encoding_report(reports):
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    frame_count, minimum_change = 0, float("inf")
    for entry in reports:
        image = Image.open(ART / entry["file"]).convert("RGBA")
        for row in range(image.height // 64):
            cells = [image.crop((column * 64, row * 64, (column + 1) * 64, (row + 1) * 64))
                     for column in range(image.width // 64)]
            if (any(cell.getchannel("A").getbbox() is None for cell in cells)
                    or len({cell.tobytes() for cell in cells}) != len(cells)):
                raise AssertionError("Palette encoding lost a distinct/nonempty animation frame")
            for a, b in zip(cells, cells[1:]):
                minimum_change = min(minimum_change, sum(left != right for left, right in
                                     zip(a.get_flattened_data(), b.get_flattened_data())))
            frame_count += len(cells)
    data = {"profile": PROFILE,
            "packageVersions": {name: version(name) for name in ("Pillow", "imagequant", "zopfli")},
            "scope": "Eight V4 band runtime atlases only; owner, guest and crowd images excluded",
            "fullColorSourceLocation": "ArtSource/PixelArt/V4/*.source.png and *.packed-256.png",
            "sourceAndFullColorPackedPngsPreserved": True,
            "animationVerification": {"frames": frame_count, "allRowsDistinct": True,
                                      "allCellsNonempty": True, "minimumAdjacentChangedPixels": minimum_change},
            "comparison": "band-palette-comparison.png",
            "files": reports, "runtimeBytes": sum(entry["bytes"] for entry in reports)}
    REPORT.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8", newline="\n")
    return data


def main():
    # For the first application to existing full-color sheets. Later production
    # runs use the source packer so the reference never becomes a quantized PNG.
    images = [(ART / name, Image.open(ART / name)) for name in BAND_FILES]
    if any(image.mode == "P" for _, image in images):
        raise ValueError("Band runtime is already indexed; reproduce from the full-color source packer")
    reports = [save_band_runtime_png(image, path) for path, image in images]
    print(json.dumps(write_encoding_report(reports)))


if __name__ == "__main__":
    main()
