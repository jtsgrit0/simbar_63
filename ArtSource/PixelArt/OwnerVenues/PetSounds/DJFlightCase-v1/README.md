# Petsounds DJ flight-case room

The user selected the flight-case booth, then approved moving it beside the
stage's left speaker and removing the corridor sofa plus both foreground sofas.
The rear wall sofas, tables, candles, bar, sign, stage and room silhouette remain.
The final request also approved cleaning the former sofa outlines into the floor.

## Preserved inputs

- `venue.pre-dj.source.png`: the exact original 960×558 room before this change.
- `flight-case.source.png`: selected ImageGen booth, unchanged source bytes.
- `sofa-A-corridor.source.png`: existing generated corridor floor plate.
- `sofa-B-foreground.source.png`: existing generated foreground floor plate.
- `generation-prompts.json`: the selected booth and two floor records copied
  unchanged from the original generation catalogue, including full prompts,
  reference paths and transparency settings. Their preview wording records the
  original generation stage; production was approved afterward.
- `approved-preview-config.json`: the exact earlier approved placement/config.
- `layout.json`: portable source paths and the approved final masks, including
  the small outline/shadow cleanup. No additional raster texture was painted.

The booth source crop is `[260,205,1015,662]`; its native destination remains
`[482,228,92,60]`. Only the floor visibility masks were extended to remove old
black sofa shadows. Existing generated plates supply all revealed floor pixels.

## Rebuild

With Node 20+ and Chromium available:

```powershell
uv run --with playwright python Tools/prepare_petsounds_dj.py
```

The default writes only `Tools/WebGLTemplate/FastGame/art/venue.png`. Use
`--output path/to/candidate.png` for a review artifact, or `--node path/to/node`
and `--chromium path/to/chrome` to select installed runtimes. The existing
repository Chromium discovery helper is used by default.

The source geometry is assembled as SVG: each floor patch has separate clipping,
preservation, color and feather masks; the chosen transparent booth is cropped
with a nested SVG viewBox. ChromeCanvas performs the technical render. The
dependency-free `Tools/petsounds_png_composite.mjs` then decodes the PNG streams
and retains original raw RGBA for every untouched pixel. This avoids changing
the slightly translucent original artwork through repeated alpha rounding.
The result is checked with an exact PNG RGBA roundtrip.

The builder starts from the preserved pre-DJ room on every run. If an approved
neon/base change is rebuilt, pass that new plate explicitly with `--baseline`;
run this DJ step after the neon/base step. Running the older neon step afterward
would replace the completed DJ room. No original source has been removed.

Navigation, retired seats and foreground occlusion are maintained separately in
the game metadata. This art builder does not change metadata, camera, rules,
audio, UI or the other owners' venues.
