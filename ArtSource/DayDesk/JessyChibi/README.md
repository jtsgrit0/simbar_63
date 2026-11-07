# Jessy chibi desk revision

The built-in `image_gen.imagegen` tool edited only Jessy's proportions in the
approved office composition. Her rounded chibi face and compact torso match the
other owners, with the entire hairstyle inside the image. The previous desk
source remains unchanged at `../desk-jessy.source.png`.

The exact edit prompt is [prompt.md](prompt.md). The unchanged generated RGB
source is `desk-jessy-chibi.source.png` (1672×941). The complete image is exported
to 640×360 using nearest-neighbor sampling. The runtime PNG is the approved
export's unchanged byte stream; no crop, palette reduction or smoothing is used.

`candidate-manifest.json` records references, source/export hashes and approval.
`previous-jessy-asset.json` retains the former manifest entry. The shared monitor
edges match exactly, and phone edges differ by at most one runtime pixel. Existing
screen overlays and both other owners' source/runtime streams are unchanged.
The current entry in `../manifest.json` points to this revision.
