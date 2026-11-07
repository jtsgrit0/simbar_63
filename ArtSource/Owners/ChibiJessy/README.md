# Jessy chibi proportions

The built-in `image_gen.imagegen` tool edited the approved Jessy identity to a
rounded larger face and compact torso matching Edison and Jaiden. Her complete
upper curls are visible, with seven runtime pixels above the highest outline.
Purple/copper curls, hoop earrings, burgundy/black leather, crossed arms and pink
nails remain recognizable. The original supplied illustration is unchanged.

- Exact prompt and input roles: [prompt.md](prompt.md).
- Unchanged generated RGB source: `selector-jessy-chibi.source.png`, 1341×1173.
- Technical export: centered `ImageOps.fit(..., (400,350), NEAREST)`. The source
  box is `(0.2142857143,0)–(1340.7857142857,1173)`, trimming less than one source
  pixel of background horizontally and no pixels from the top or bottom.
- Canonical working source: `selector-jessy-chibi.card.png`, 400×350 RGBA.
  It copies the approved export's exact RGB and adds opaque alpha; no second
  resampling, recoloring, smoothing or generation is applied.
- `presentation.json` uses the canonical card's full `(0,0)–(400,350)` rectangle.
  The runtime PNG is byte-identical to this canonical source.

`candidate-manifest.json` records raw sources, input hashes and approval;
`delivery.json` records the complete raw → fit → canonical → runtime proof.
Both other owners retain their encoded streams. The matching desk revision is
documented in [DayDesk/JessyChibi](../../DayDesk/JessyChibi/README.md).
