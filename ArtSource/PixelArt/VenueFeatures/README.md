# Venue upgrade and event props

The venue's three upgrade categories retain three visible levels each. The
current atlas retains the twelve common/FF upgrade drawings and replaces
the three event drawings with matching isometric art. VIP fixtures remain standing
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
  before the upgrade edit. Its original event cells were retained in the v2
  release; it remains unchanged as historical source evidence.
- `venue-features-v2.runtime.png` is the retained 480 × 288 RGBA atlas from
  before the event edit. All twelve upgrade cells in the current output must
  match its full RGBA bytes exactly.
- `venue-events-isometric-v3.source.png` is the separate 1774 × 887 transparent
  ImageGen event sheet. It contains open-mic equipment, rock-showcase equipment
  and a fan-festival banner in three columns of one row. The rooms and current
  upgrade artwork supplied perspective, material and lighting references.
  The exact prompt and input/output provenance are in `image-prompt-v3.json`.

The v2 upgrade source has four columns and three rows:

| Row | Column 0 | Column 1 | Column 2 | Column 3 |
| --- | --- | --- | --- | --- |
| 0 | Two-tap draught | Three-tap draught | Four-tap draught | FF two-tap draught |
| 1 | Speaker + lamp | Speakers + two lamps | Subwoofer + four lamps | FF three-tap draught |
| 2 | Dark stanchions | Brass stanchions + teal star | Three stanchions + bulb star | FF four-tap draught |

The FF fixtures are separate source drawings viewed along the opposite room
axis, not flipped copies of the common fixtures. The technical packer performs
no drawing, recoloring, rotation, perspective warp or mirroring.

The v3 event source has one row of three independent drawings:

| Column 0 | Column 1 | Column 2 |
| --- | --- | --- |
| Microphone stand + amber music sign | Amplifier + guitar + magenta lightning sign | Two banner posts + bunting + heart/music lights |

## Runtime contract

`Tools/WebGLTemplate/FastGame/art/venue-features.png` is a lossless RGBA
480 × 288 atlas containing fifteen unique 96 × 96 cells. Each generated prop
is bottom-centered at anchor `(48, 88)`.

| Runtime row | Column 0 | Column 1 | Column 2 | Column 3, v3 events | Column 4 |
| --- | --- | --- | --- | --- | --- |
| 0 | Draught L1 | Draught L2 | Draught L3 | Open mic | FF draught L1 |
| 1 | Sound L1 | Sound L2 | Sound L3 | Rock showcase | FF draught L2 |
| 2 | VIP L1 | VIP L2 | VIP L3 | Fan festival | FF draught L3 |

The metadata keeps the twelve original `items` keys. Each draught item also
declares `variants.ff`, selecting column four at the corresponding level's
row. Other upgrade and event identifiers remain unchanged. `sources` names
the retained upgrade source, the new event source and the upgrade-preservation
baseline.

Common levels share one source-to-runtime scale per category. All three FF
draught levels share another scale, retaining their tier progression. The
three event drawings each use an independent proportional fit and the same
bottom-center anchor. Runtime venue positions, small placement corrections and ambient
lighting belong to the renderer rather than this source-packing step.

## Rebuild and verification

```powershell
uv run --with pillow python Tools/prepare_venue_features.py
```

The upgrade source's row gutters differ by column. The packer splits at the real
transparent column gutters, then finds each column's three row gutters before
alpha-cropping individual drawings. This keeps complete objects even where
the FF drawing's vertical extent overlaps another column's row interval.
The helper rejects source-boundary contact, cell overflow, empty cells and
duplicate drawings.

Only alpha cropping, proportional nearest-neighbor sizing and whole-object
translation are used. The existing common/FF upgrade packing path is unchanged,
and all twelve resulting upgrade cells are checked against the v2 runtime
baseline before the runtime file is replaced. The new event sheet is split at
its real transparent column gutters and each of its three drawings is fit
separately. The output is saved as RGBA without palette quantization;
decode-and-compare checks verify lossless output, byte-for-byte upgrade RGBA
preservation and replacement of all three event cells.

`packing-report.json` records both source hashes, gutter boundaries, source
rectangles, group scale factors, packed bounds and visible hashes. The final
checks require fifteen nonempty unique props, twelve preserved upgrade
drawings, three new events, `allUpgradePixelsUnchanged: true` and
`changedEventCells: 3`.
The retained source and resulting atlas were also visually inspected.
