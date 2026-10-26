#!/usr/bin/env python3
"""Build and verify the game's OFL Korean UI font with unused Hanja removed.

Run from any directory:
    uv run --with fonttools==4.67.0 python Tools/prepare_korean_font.py
    uv run --with fonttools==4.67.0 python Tools/prepare_korean_font.py --verify-only

The full original stays in ArtSource, outside Unity's Assets import tree. Only
the runtime OTF is replaced; its filename and Unity .meta are never changed.
"""

from __future__ import annotations

import argparse
import hashlib
import io
import json
from pathlib import Path
import re
import sys

from fontTools import subset
from fontTools.pens.recordingPen import RecordingPen
from fontTools.ttLib import TTFont


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "ArtSource/Fonts/NotoSansKR-Regular.full.otf"
OUTPUT = ROOT / "Assets/Resources/Fonts/NotoSansKR-Regular.otf"
SOURCE_SHA256 = "69975a0ac8472717870aefeab0a4d52739308d90856b9955313b2ad5e0148d68"
FAMILY = "SimBar Korean UI"
POSTSCRIPT_NAME = "SimBarKoreanUI-Regular"
# These are the only Unicode ranges eligible for removal. Compatibility
# ideographs, all Hangul/Jamo, Latin, punctuation, Greek and symbols survive.
HANJA_RANGES = ((0x3400, 0x9FFF), (0x20000, 0x323AF))
TEXT_SUFFIXES = {
    ".asset", ".json", ".txt", ".csv", ".tsv", ".xml", ".yaml", ".yml",
    ".prefab", ".unity", ".mat", ".controller", ".overridecontroller",
    ".anim", ".playable", ".uxml", ".uss", ".shader", ".cs", ".md",
}
ESCAPE = re.compile(r"\\u([0-9a-fA-F]{4})|\\U([0-9a-fA-F]{8})")
SURROGATE_PAIR = re.compile(r"\\u([dD][89aAbB][0-9a-fA-F]{2})\\u([dD][c-fC-F][0-9a-fA-F]{2})")


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def is_hanja(codepoint: int) -> bool:
    return any(first <= codepoint <= last for first, last in HANJA_RANGES)


def used_hanja() -> tuple[set[int], list[str]]:
    """Conservatively include literals and C#/JSON Unicode escapes in text."""
    paths = set((ROOT / "Assets/Scripts").rglob("*.cs"))
    paths.update(
        path for path in (ROOT / "Assets/Resources").rglob("*")
        if path.is_file() and path.suffix.lower() in TEXT_SUFFIXES
    )
    found: set[int] = set()
    scanned: list[str] = []
    for path in sorted(paths):
        data = path.read_bytes()
        # Unity YAML/JSON/C# are UTF-8; accept BOM-marked UTF-16 text as well.
        encoding = "utf-16" if data[:2] in (b"\xff\xfe", b"\xfe\xff") else "utf-8-sig"
        try:
            text = data.decode(encoding)
        except UnicodeDecodeError as exc:
            raise ValueError(f"Cannot safely scan text file {path}: {exc}") from exc
        scanned.append(path.relative_to(ROOT).as_posix())
        found.update(ord(char) for char in text if is_hanja(ord(char)))
        for match in ESCAPE.finditer(text):
            value = int(match.group(1) or match.group(2), 16)
            if is_hanja(value):
                found.add(value)
        for match in SURROGATE_PAIR.finditer(text):
            high, low = (int(part, 16) for part in match.groups())
            value = 0x10000 + ((high - 0xD800) << 10) + low - 0xDC00
            if is_hanja(value):
                found.add(value)
    return found, scanned


def rename_derivative(font: TTFont) -> None:
    replacements = {
        1: FAMILY,
        2: "Regular",
        3: "2.004;SimBar;SimBarKoreanUI-Regular;HanjaSubset1",
        4: f"{FAMILY} Regular",
        6: POSTSCRIPT_NAME,
        16: FAMILY,
        17: "Regular",
        18: f"{FAMILY} Regular",
        21: FAMILY,
        22: "Regular",
        25: "SimBarKoreanUI",
    }
    # Keep all original language/platform records and all copyright/license
    # metadata. Only font identity records receive the derivative's own name.
    for record in font["name"].names:
        if record.nameID in replacements:
            record.string = replacements[record.nameID].encode(record.getEncoding())
    cff = font["CFF "].cff
    cff.fontNames = [POSTSCRIPT_NAME]
    top = cff.topDictIndex[0]
    top.FamilyName = FAMILY
    top.FullName = f"{FAMILY} Regular"
    if hasattr(top, "FontName"):
        top.FontName = POSTSCRIPT_NAME
    for dictionary in top.FDArray:
        if hasattr(dictionary, "FontName"):
            dictionary.FontName = dictionary.FontName.replace(
                "NotoSansKR-Regular", POSTSCRIPT_NAME
            )


def build(source_data: bytes, keep: set[int]) -> bytes:
    font = TTFont(io.BytesIO(source_data), recalcBBoxes=False, recalcTimestamp=False)
    options = subset.Options()
    options.layout_features = ["*"]
    options.layout_scripts = ["*"]
    options.name_IDs = ["*"]
    options.name_languages = ["*"]
    options.name_legacy = True
    options.glyph_names = True
    options.legacy_cmap = True
    options.symbol_cmap = True
    options.notdef_outline = True
    options.hinting = True
    options.recalc_bounds = False
    options.recalc_timestamp = False
    # Keep the original global metrics rather than recomputing text metrics
    # from a smaller glyph inventory.
    options.recalc_average_width = False
    options.prune_unicode_ranges = False
    options.prune_codepage_ranges = False
    subsetter = subset.Subsetter(options=options)
    subsetter.populate(unicodes=sorted(keep))
    subsetter.subset(font)
    rename_derivative(font)
    stream = io.BytesIO()
    font.save(stream, reorderTables=True)
    font.close()
    return stream.getvalue()


def verify(source_data: bytes, output_data: bytes, keep: set[int]) -> dict:
    original = TTFont(io.BytesIO(source_data), recalcTimestamp=False)
    result = TTFont(io.BytesIO(output_data), recalcTimestamp=False)
    before = original.getBestCmap()
    after = result.getBestCmap()
    if set(after) != keep:
        raise AssertionError("Runtime Unicode coverage differs from the keep policy")
    hangul = set(range(0xAC00, 0xD7A4))
    if not hangul <= set(after):
        raise AssertionError("Runtime font is missing a complete Hangul syllable")
    if any(before[point] != after[point] for point in keep):
        raise AssertionError("A retained character now maps to a different glyph")

    # Compare the complete drawing command stream for EVERY retained glyph,
    # including layout-closure glyphs; checking only sample syllables can miss
    # changed final consonants or substitutions. CFF subroutines may be packed
    # differently, but their resulting outlines must remain exactly identical.
    source_glyphs, target_glyphs = original.getGlyphSet(), result.getGlyphSet()
    outline_digest = hashlib.sha256()
    for name in result.getGlyphOrder():
        left, right = RecordingPen(), RecordingPen()
        source_glyphs[name].draw(left)
        target_glyphs[name].draw(right)
        if left.value != right.value:
            raise AssertionError(f"Outline changed: {name}")
        encoded = json.dumps([name, right.value], ensure_ascii=True, separators=(",", ":"))
        outline_digest.update(encoded.encode("ascii"))
        for table in ("hmtx", "vmtx"):
            if original[table].metrics[name] != result[table].metrics[name]:
                raise AssertionError(f"{table} metrics changed: {name}")

    # hhea/vhea counts may shrink as the glyph inventory changes; all other
    # global horizontal/vertical metrics and OS/2 values must be unchanged.
    for table, ignored in (
        ("head", {"checkSumAdjustment"}),
        ("hhea", {"numberOfHMetrics"}),
        ("vhea", {"numberOfVMetrics"}),
        ("OS/2", {"usFirstCharIndex", "usLastCharIndex"}),
    ):
        for key, value in vars(original[table]).items():
            actual = getattr(result[table], key)
            # Panose stores its metrics in an object without value equality.
            if hasattr(value, "__dict__"):
                value, actual = vars(value), vars(actual)
            if key not in ignored and actual != value:
                raise AssertionError(f"Global font metric changed: {table}.{key}")
    for name_id in (0, 7, 8, 9, 10, 11, 12, 13, 14):
        left = [(n.platformID, n.platEncID, n.langID, n.string)
                for n in original["name"].names if n.nameID == name_id]
        right = [(n.platformID, n.platEncID, n.langID, n.string)
                 for n in result["name"].names if n.nameID == name_id]
        if left != right:
            raise AssertionError(f"Attribution or license metadata changed: name ID {name_id}")
    if result["CFF "].cff.fontNames != [POSTSCRIPT_NAME]:
        raise AssertionError("Derivative PostScript name was not applied")
    for record in result["name"].names:
        if record.nameID in (1, 3, 4, 6, 16, 18, 21, 25) and "Noto" in record.toUnicode():
            raise AssertionError("Derivative still has the upstream primary font name")
    if original["CFF "].cff.topDictIndex[0].Notice != result["CFF "].cff.topDictIndex[0].Notice:
        raise AssertionError("CFF copyright notice changed")

    return {
        "source_bytes": len(source_data),
        "runtime_bytes": len(output_data),
        "saved_bytes": len(source_data) - len(output_data),
        "saved_percent": round(100 * (1 - len(output_data) / len(source_data)), 2),
        "source_glyphs": len(original.getGlyphOrder()),
        "runtime_glyphs": len(result.getGlyphOrder()),
        "source_unicode_entries": len(before),
        "runtime_unicode_entries": len(after),
        "hangul_syllables": len(hangul),
        "verified_outlines_and_both_advance_tables": len(result.getGlyphOrder()),
        "retained_outline_sha256": outline_digest.hexdigest(),
        "runtime_sha256": sha256(output_data),
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verify-only", action="store_true", help="verify the existing runtime OTF without writing it")
    args = parser.parse_args()
    source_data = SOURCE.read_bytes()
    if sha256(source_data) != SOURCE_SHA256:
        raise ValueError("Full source differs from the archived original; review provenance before rebuilding")
    original = TTFont(io.BytesIO(source_data), recalcTimestamp=False)
    cmap = original.getBestCmap()
    original.close()
    hanja, scanned = used_hanja()
    missing = sorted(hanja - set(cmap))
    if missing:
        raise ValueError("Current game text needs Hanja absent from the original font: "
                         + ", ".join(f"U+{point:04X}" for point in missing))
    keep = {point for point in cmap if not is_hanja(point) or point in hanja}
    metadata_path = OUTPUT.with_suffix(OUTPUT.suffix + ".meta")
    original_meta = metadata_path.read_bytes()
    output_data = OUTPUT.read_bytes() if args.verify_only else build(source_data, keep)
    report = verify(source_data, output_data, keep)
    if not args.verify_only:
        # Verify the result fully before replacing Unity's existing font asset.
        OUTPUT.write_bytes(output_data)
    if metadata_path.read_bytes() != original_meta:
        raise AssertionError("Unity font .meta changed")
    report.update({
        "mode": "verify-only" if args.verify_only else "build-and-verify",
        "text_files_scanned": len(scanned),
        "retained_used_hanja": [f"U+{point:04X}" for point in sorted(hanja)],
        "removed_unused_hanja_entries": len(cmap) - len(keep),
        "unity_meta_unchanged": True,
    })
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, ValueError, OSError) as error:
        sys.exit(f"Font preparation failed: {error}")
