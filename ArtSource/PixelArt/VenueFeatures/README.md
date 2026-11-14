# Venue upgrade and event props

The venue's three upgrade categories retain three visible levels each. The
current atlas adds three genuinely different FF-facing draught drawings and
preserves all three existing event drawings. VIP fixtures remain standing
stanchions and reserved-star signs, without seating. Draught fixtures contain
the equipment and drip tray, without another counter beneath them.

## Source and provenance

- `venue-features.source.png` is the unchanged original 1448 × 1086 transparent
  sheet returned by built-in `image_gen` on 2026-11-14. Its exact prompt remains
  in `image-prompt.json`.
- `venue-upgrades-isometric-v2.source.png` is the separate 1448 × 1086
  transparent ImageGen edit. It supplies nine common upgrade drawings and
  three FF-facing draught drawings. The existing PetSounds, Freebird and FF
  room plates supplied perspective, materials, pixel density and lighting
  references; their source pixels were not edited. The exact edit prompt and
  input/output provenance are in `image-prompt-v2.json`.
- `venue-features-v1.runtime.png` is the preserved 384 × 288 encoded atlas from
  before the upgrade edit. Its original column-three event cells supply the
  runtime events without recoloring, resizing or palette conversion.

The new ImageGen source has four columns and three rows:

| Row | Column 0 | Column 1 | Column 2 | Column 3 |
| --- | --- | --- | --- | --- |
| 0 | Two-tap draught | Three-tap draught | Four-tap draught | FF two-tap draught |
| 1 | Speaker + lamp | Speakers + two lamps | Subwoofer + four lamps | FF three-tap draught |
| 2 | Dark stanchions | Brass stanchions + teal star | Three stanchions + bulb star | FF four-tap draught |

The FF fixtures are separate source drawings viewed along the opposite room
axis, not flipped copies of the common fixtures. The technical packer performs
no drawing, recoloring, rotation, perspective warp or mirroring.

## Runtime contract

`Tools/WebGLTemplate/FastGame/art/venue-features.png` is a lossless RGBA
480 × 288 atlas containing fifteen unique 96 × 96 cells. Each generated prop
is bottom-centered at anchor `(48, 88)`.

| Runtime row | Column 0 | Column 1 | Column 2 | Column 3, preserved | Column 4 |
| --- | --- | --- | --- | --- | --- |
| 0 | Draught L1 | Draught L2 | Draught L3 | Open mic | FF draught L1 |
| 1 | Sound L1 | Sound L2 | Sound L3 | Rock showcase | FF draught L2 |
| 2 | VIP L1 | VIP L2 | VIP L3 | Fan festival | FF draught L3 |

The metadata keeps the twelve original `items` keys. Each draught item also
declares `variants.ff`, selecting column four at the corresponding level's
row. Other upgrade and event identifiers remain unchanged. `sources` names
the generated upgrade source and the preserved event runtime source.

Common levels share one source-to-runtime scale per category. All three FF
draught levels share another scale, retaining their tier progression. The
preserved event cells use scale one and their complete original 96 × 96 RGBA
pixels. Runtime venue positions, small placement corrections and ambient
lighting belong to the renderer rather than this source-packing step.

## Rebuild and verification

```powershell
uv run --with pillow python Tools/prepare_venue_features.py
```

The new source's row gutters differ by column. The packer splits at the real
transparent column gutters, then finds each column's three row gutters before
alpha-cropping individual drawings. This keeps complete objects even where
the FF drawing's vertical extent overlaps another column's row interval.
The helper rejects source-boundary contact, cell overflow, empty cells and
duplicate drawings.

Only alpha cropping, proportional nearest-neighbor sizing and whole-object
translation are used for the twelve generated upgrade drawings. The three
event cells are copied directly from the retained runtime atlas. The output
is saved as RGBA without palette quantization; decode-and-compare checks
verify lossless output and byte-for-byte RGBA equality for each old event.

`packing-report.json` records both source hashes, gutter boundaries, source
rectangles, group scale factors, packed bounds and visible hashes. The final
checks require fifteen nonempty unique props, twelve generated upgrade
drawings, three preserved events and `allEventPixelsUnchanged: true`.
The retained source and resulting atlas were also visually inspected.
