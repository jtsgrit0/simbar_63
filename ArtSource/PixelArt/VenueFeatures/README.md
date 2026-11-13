# Venue upgrade and event props

Twelve standalone isometric pixel fixtures give visible changes to the existing venue's three upgrade categories and three event types. The VIP props are velvet stanchions and illuminated reserved-star signs; none contain seating. Draught fixtures contain only the chrome equipment and drip tray so they can replace equipment on an existing counter.

## Source and provenance

`venue-features.source.png` is the unchanged 1448 × 1086 transparent sheet returned by built-in `image_gen` on 2026-11-14. The existing `venue-ff.png` and `venue-freebird.png` supplied perspective, pixel style, and palette references. The exact prompt is retained in `image-prompt.json`.

Every runtime prop is a genuine distinct source drawing. The sheet has four columns and three rows:

| Row | Column 0 | Column 1 | Column 2 | Column 3 |
| --- | --- | --- | --- | --- |
| 0 | Two-tap draught | Three-tap draught | Four-tap draught | Open mic |
| 1 | Speaker + lamp | Speakers + two lamps | Subwoofer + four lamps | Rock showcase |
| 2 | Dark stanchions | Brass stanchions + cyan star | Three stanchions + illuminated star | Fan festival |

## Runtime contract

`Tools/WebGLTemplate/FastGame/art/venue-features.png` is 384 × 288, with twelve 96 × 96 cells. Each prop is bottom-centered at anchor `(48, 88)`. `venue-features.json` contains an `items` object keyed by `draught-1` through `draught-3`, `sound-1` through `sound-3`, `vip-1` through `vip-3`, `open-mic`, `rock-showcase`, and `fan-festival`. Each item declares its `row` and `column`.

Draw sizes and venue-specific positions belong to the game renderer. The first three props of each row share a source-to-runtime scale so upgrade progression remains visible. The event prop is fit separately.

## Rebuild and verification

```powershell
.\.venv\Scripts\python.exe Tools\prepare_venue_features.py
```

The helper performs technical alpha cropping, proportional nearest-neighbor sizing, whole-prop alignment, and PNG palette encoding. It does not paint, recolor, mirror, combine, or synthesize props. It copies the metadata and records hashes, source bounds, scale factors, packed bounds, alpha extrema, and unique-prop count in `packing-report.json`.

The source and finished atlas were visually inspected. All twelve cells are nonempty and unique, with genuine transparent alpha and clear upgrade progression. The helper rejects cell overflow, missing props, or duplicates.
