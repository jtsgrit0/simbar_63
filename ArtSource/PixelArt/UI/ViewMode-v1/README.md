# View mode buttons — selected concept 4

The user selected the film/play viewing button and approved the shared
briefcase/chart return button. Both are native 84×88 transparent PNGs.

- `view-mode.source.png` → `mobile-re-v20261115-view-mode.png` in runtime
  `art/ui-reference/`, labelled **감상모드**.
- `management-mode.source.png` → `mobile-re-v20261115-management-mode.png`,
  labelled **경영모드**.

The runtime copies are byte-identical to the source PNGs in this folder. The
other three proposals are not included in this source package.

`view-mode.svg` and `management-mode.svg` are self-contained source artwork.
The reference button PNG is embedded as data, and the existing NotoSansKR
glyphs are SVG paths. No external raster image, font file, font download or
ImageGen input is needed to render these SVGs. The approved PNG files remain
the exact pixel/byte reference for packaging.

The existing event button supplies its original border, outline, corners and
background. Only the new icon/label area changes; its baked notification badge
is replaced using the matching opposite corner of that same reference image.
All previously existing runtime button PNGs remain untouched.

`provenance.json` records the selected preview and its current corrected hashes,
with the original hashes retained. `verification.json` records installed hashes
and the original preview checks; its nested `productFilesWritten: false`
describes the earlier preview stage.
`export-mask.json` is the technical content/badge mask used during export.
ChromeCanvas renders the code-native SVG, then the existing dependency-free
PNG compositor preserves the original raw RGBA outside those regions to avoid
alpha-rounding changes to the border. No raster texture was painted or generated.

The upper-right corner seam was corrected without changing either icon or label.
The original badge-removal layer remains intact. A second embedded reference
image uses `translate(82 0)` through the `corner-seam` clip at `[69,0,13,23]`,
joining its white outline at x77 to the untouched right edge below row23. Only
228 pixels per button changed, all within that corner strip; raw RGBA outside
it, including both full icon/label regions, is identical. Rendering the complete
updated SVG through the existing export mask reproduces the corrected PNG's
raw RGBA exactly. Both states receive the same corner correction, while the
existing event and upgrade reference PNGs remain byte-identical.

This package only supplies button assets. Game state, layout, camera,
transitions and return behavior are implemented separately.
