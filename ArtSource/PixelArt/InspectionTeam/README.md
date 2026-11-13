# Inspection team pixel models

Three fictional municipal venue inspectors wear fluorescent reflective vests over navy clothing. Their silhouettes differ: cap and radio, dark ponytail and clipboard, and short gray hair and clipboard. They have no police insignia, weapons, or readable logos.

## Source and provenance

`inspectors.source.png` is the unchanged 1254 × 1254 transparent image returned by the built-in `image_gen` tool on 2026-11-14. `Tools/WebGLTemplate/FastGame/art/wardrobe.png` was provided as a reference for adult proportions and the existing isometric pixel style. The complete request and source-grid specification are retained in `image-prompt.json`.

Every one of the 36 poses was generated in the original sheet. The source has six columns and six rows. Each inspector occupies two rows: the first contains FR idle/walk A/walk B followed by BR idle/walk A/walk B; the second contains BL followed by FL in the same pose order.

## Runtime atlas

`Tools/WebGLTemplate/FastGame/art/inspectors.png` is 768 × 192 pixels: twelve 64 × 64 cells across and three inspector rows. The matching `inspection-models.json` declares foot coordinates `(32, 60)`, render size 54 × 54, and six walking frames per second.

| Columns | Direction | Poses |
| --- | --- | --- |
| 0–2 | FR | Idle, walk A, walk B |
| 3–5 | BR | Idle, walk A, walk B |
| 6–8 | BL | Idle, walk A, walk B |
| 9–11 | FL | Idle, walk A, walk B |

Rows 0, 1, and 2 represent cap/radio, ponytail/clipboard, and gray-hair/clipboard respectively. All four directional views are drawn in the source; no poses are mirrored or synthesized.

## Rebuild and verification

From the repository root:

```powershell
.\.venv\Scripts\python.exe Tools\prepare_inspection_team.py
```

The helper uses only technical alpha cropping, frame reordering, proportional nearest-neighbor sizing, foot alignment, and palette encoding. One shared scale per inspector keeps the twelve poses consistent. It writes the runtime PNG and metadata and updates `packing-report.json` with source/output hashes, frame bounds, transparency extrema, and pose-difference checks. It rejects clipping, empty cells, or repeated idle/walking frames.

The finished atlas was visually checked at its native size. Validation passed for all 36 cells and all twelve directional groups, each containing three distinct poses. Both source and export retain alpha transparency.
