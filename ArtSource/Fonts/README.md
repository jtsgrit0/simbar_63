# Korean UI font

The complete original `NotoSansKR-Regular.full.otf` (Noto Sans KR 2.004) is archived here outside Unity's runtime Resources. It was copied from the project's existing `Assets/Resources/Fonts/NotoSansKR-Regular.otf`, without downloading or altering the source. Its SHA-256 is `69975a0ac8472717870aefeab0a4d52739308d90856b9955313b2ad5e0148d68`.

`OFL.txt` preserves the original SIL Open Font License. The modified runtime font is named **SimBar Korean UI**, PostScript name **SimBarKoreanUI-Regular**. Its asset filename and Unity GUID stay unchanged so existing references continue to work.

Rebuild with:

```
uv run --with fonttools==4.67.0 python Tools/prepare_korean_font.py
```

The generator removes only unused CJK ideographs in U+3400–9FFF and U+20000–323AF, retaining any found in current runtime scripts/resource text. It retains all 11,172 Hangul syllables, Jamo, compatibility ideographs, Latin, punctuation and other original Unicode coverage outside those ranges, together with layout closure and hinting. When adding Hanja to runtime text, rerun this generator before building.

Every retained glyph's drawing commands and horizontal/vertical metrics are compared with the full source before the runtime asset is replaced. Global font metrics and copyright/license records are verified as well. Use `--verify-only` to check the current asset. The initial derivative is 2,418,872 bytes versus the original 4,644,748 bytes (47.92% smaller), with 17,134 verified glyphs and 14,856 Unicode entries.
