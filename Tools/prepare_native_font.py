"""Rebuild the sharp web UI font from the retained OFL source and game strings.

uv run --with fonttools --with brotli python Tools/prepare_native_font.py
"""
from pathlib import Path
import re
from fontTools import subset
from fontTools.ttLib import TTFont
from prepare_korean_font import rename_derivative

ROOT = Path(__file__).resolve().parent.parent
RUNTIME = ROOT / 'Tools/WebGLTemplate/FastGame'

def main():
    text = ''.join(chr(n) for n in range(32, 127)) + '₩…·×★☆♥♪♫▶◀Ⅱ→←✓✕⌛'
    for path in sorted(list(RUNTIME.glob('*.js')) + list((ROOT / 'Assets/Scripts').rglob('*.cs'))):
        content = path.read_text(encoding='utf-8-sig')
        text += content
        text += ''.join(chr(int(x, 16)) for x in re.findall(r'\\u([0-9a-fA-F]{4})', content))
    font = TTFont(ROOT / 'ArtSource/Fonts/NotoSansKR-Regular.full.otf', recalcTimestamp=False)
    wanted = {ord(c) for c in text} & set(font.getBestCmap())
    options = subset.Options()
    options.name_IDs = ['*']
    options.name_languages = ['*']
    options.recalc_timestamp = False
    worker = subset.Subsetter(options=options)
    worker.populate(unicodes=wanted)
    worker.subset(font)
    rename_derivative(font)
    font.flavor = 'woff2'
    output = RUNTIME / 'font.woff2'
    font.save(output)
    actual = TTFont(output).getBestCmap()
    assert wanted <= set(actual), 'UI font is missing game characters'
    print(f'{len(wanted)} codepoints; {output.stat().st_size:,} bytes')

if __name__ == '__main__':
    main()
