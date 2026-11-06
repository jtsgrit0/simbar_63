"""Re-encode only V4 runtime atlases without changing any visible RGBA pixel.

RGB under alpha zero is not displayed. Canonicalizing that RGB to zero improves
PNG compression without changing alpha, palette colors that are displayed, or
any nonzero-alpha pixel. Source and owner artwork are deliberately out of scope.
The packers call ``save_runtime_png`` so a later repack retains this optimization.
For additional exact PNG compression, run:
  uv run --with pillow --with zopfli python Tools/optimize_runtime_atlases.py --zopfli
"""

import argparse
import hashlib
import io
import json
from pathlib import Path
import struct

from PIL import Image


ROOT = Path(__file__).resolve().parent.parent
ART = ROOT / "Tools/WebGLTemplate/FastGame/art"
FILES = (
    "v4-band-bass-performance.png", "v4-band-drums-performance.png",
    "v4-band-greet-a.png", "v4-band-greet-b.png",
    "v4-band-guitar-performance.png", "v4-band-jump.png",
    "v4-band-singer-performance.png", "v4-band-walk.png", "v4-guest-social.png",
)


def visible_pixels(image):
    """Canonical RGBA: exact RGB/A when visible; RGB=0 only when A=0."""
    rgba = image.convert("RGBA")
    pixels = bytearray(rgba.tobytes())
    cleared = 0
    for offset in range(0, len(pixels), 4):
        if pixels[offset + 3] == 0 and any(pixels[offset:offset + 3]):
            pixels[offset:offset + 3] = b"\0\0\0"
            cleared += 1
    return rgba.size, bytes(pixels), cleared


def visible_sha256(image):
    size, pixels, _ = visible_pixels(image)
    return hashlib.sha256(struct.pack(">II", *size) + pixels).hexdigest()


def encode_runtime_png(image):
    size, pixels, cleared = visible_pixels(image)
    canonical = Image.frombytes("RGBA", size, pixels)
    candidates = [canonical]
    # Keep exact indexed PNGs indexed; expanding the guest atlas to RGBA would
    # triple its bytes. Palette replacement is accepted only after exact decode
    # comparison, including all original alpha and all visible RGB values.
    if image.mode == "P":
        indexed = image.copy()
        palette = indexed.getpalette("RGB")
        alpha_by_index = {}
        for index, rgba in zip(image.get_flattened_data(), image.convert("RGBA").get_flattened_data()):
            alpha_by_index[index] = rgba[3]
        for index, alpha in alpha_by_index.items():
            if alpha == 0:
                palette[index * 3:index * 3 + 3] = [0, 0, 0]
        indexed.putpalette(palette, rawmode="RGB")
        # putpalette may interact with alpha palettes on unusual inputs. The
        # hash assertion below prevents even a single visible pixel changing.
        if visible_pixels(indexed)[:2] == (size, pixels):
            candidates.append(indexed)
        candidates.append(image)
    streams = []
    for candidate in candidates:
        stream = io.BytesIO()
        candidate.save(stream, format="PNG", optimize=True, compress_level=9)
        streams.append(stream.getvalue())
    encoded = min(streams, key=len)
    decoded = Image.open(io.BytesIO(encoded))
    if visible_pixels(decoded)[:2] != (size, pixels):
        raise AssertionError("PNG optimization changed a visible RGBA pixel")
    return encoded, {"size": list(size), "clearedInvisibleRgbPixels": cleared,
                     "visibleSha256": visible_sha256(decoded),
                     "nonzeroAlphaPixelsUnchanged": True, "alphaUnchanged": True}


def save_runtime_png(image, path):
    path = Path(path)
    if path.parent.resolve() != ART.resolve() or path.name not in FILES:
        raise ValueError("Only approved V4 runtime atlases may be optimized")
    encoded, report = encode_runtime_png(image)
    # Preserve a previously stronger lossless recompression across an unchanged
    # source repack. A different visible image always replaces the old stream.
    if path.is_file():
        previous = path.read_bytes()
        if (len(previous) <= len(encoded)
                and visible_sha256(Image.open(io.BytesIO(previous))) == report["visibleSha256"]):
            encoded = previous
    path.write_bytes(encoded)
    return report


def optimize(path, *, use_zopfli=False):
    original = path.read_bytes()
    image = Image.open(io.BytesIO(original))
    before = visible_sha256(image)
    encoded, report = encode_runtime_png(image)
    if use_zopfli:
        import zopfli.png
        recompressed = zopfli.png.optimize(encoded, lossy_transparent=False, lossy_8bit=False,
                                            num_iterations=5, num_iterations_large=5)
        if visible_pixels(Image.open(io.BytesIO(recompressed)))[:2] != visible_pixels(image)[:2]:
            raise AssertionError("Zopfli optimization changed a visible RGBA pixel")
        encoded = min((encoded, recompressed), key=len)
    # Recompression may not beat an already optimized stream. Retain the
    # smaller exact original in that case, avoiding needless file growth.
    if len(encoded) <= len(original):
        path.write_bytes(encoded)
    after = path.read_bytes()
    if before != visible_sha256(Image.open(io.BytesIO(after))):
        raise AssertionError("Runtime visible-pixel hash changed")
    return {"file": path.name, "beforeBytes": len(original), "afterBytes": len(after),
            "savedBytes": len(original) - len(after), **report,
            "visibleSha256Before": before,
            "fileSha256": hashlib.sha256(after).hexdigest()}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--zopfli", action="store_true", help="Use optional zopfli for stronger lossless PNG compression")
    parser.add_argument("--report", type=Path,
                        default=ROOT / "Research/PixelArtVerification/V4/runtime-png-lossless.json")
    args = parser.parse_args()
    reports = [optimize(ART / name, use_zopfli=args.zopfli) for name in FILES]
    result = {"method": "Lossless visible RGBA; zero RGB only where alpha is zero; PNG compression level 9",
              "zopfli": args.zopfli,
              "sourceAndOwnerImagesUntouched": True, "files": reports,
              "beforeBytes": sum(item["beforeBytes"] for item in reports),
              "afterBytes": sum(item["afterBytes"] for item in reports)}
    result["savedBytes"] = result["beforeBytes"] - result["afterBytes"]
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(json.dumps(result))


if __name__ == "__main__":
    main()
