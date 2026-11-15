# View mode buttons — selected concept 4

The user selected the film/play viewing button and approved the shared
briefcase/chart return button. Both are native 84×88 transparent PNGs.

- `view-mode.source.png` → `mobile-re-v20261115-view-mode.png` in runtime
  `art/ui-reference/`, labelled **감상모드**.
- `management-mode.source.png` → `mobile-re-v20261115-management-mode.png`,
  labelled **경영모드**.

The runtime copies are byte-identical to these approved preview assets. The
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

`provenance.json` retains the selected preview creation records unchanged.
`verification.json` records installed hashes and the original preview checks;
the nested `productFilesWritten: false` describes the earlier preview stage.
`export-mask.json` is the technical content/badge mask used during export.
ChromeCanvas renders the code-native SVG, then the existing dependency-free
PNG compositor preserves the original raw RGBA outside those regions to avoid
alpha-rounding changes to the border. No raster texture was painted or generated.

This package only supplies button assets. Game state, layout, camera,
transitions and return behavior are implemented separately.
