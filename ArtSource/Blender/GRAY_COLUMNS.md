# Grey concrete columns

`create_gray_columns.py` replaces the four complete planter assemblies in
`SimBar_PlantsAndWallArt` with four square grey concrete columns. The existing
ten joined mesh names remain stable for Unity. The other nine meshes have
identical SHA-256 hashes of vertices, edges, triangle winding, material slots
and transforms before and after the patch. The three existing wall sconces
retain all 588 triangle signatures, including material indices and smoothing.

| Column | Unity X | Unity Z | Width and depth | Bottom Y | Top Y |
| --- | --- | --- | --- | --- | --- |
| Front left | -9.55 | -5.90 | 0.62 m | 0.0615 m | 5.0 m |
| Rear middle | -1.25 | 5.75 | 0.62 m | 0.0615 m | 5.0 m |
| Rear left | -9.40 | 6.70 | 0.62 m | 0.0615 m | 5.0 m |
| Rear right | 9.73 | 6.65 | 0.62 m | 0.0615 m | 5.0 m |

Columns use a 0.016 m bevel and `ColumnConcrete`, RGB (0.23, 0.25, 0.28),
roughness 0.78 and metallic 0. They rise from the existing floor surface to
the casement-window head. Their footprints are smaller than the former pots
and foliage; the furniture, bar, stage and staff aisle remain in place.
Runtime pasted-paper posters are attached separately to opaque walls and
column faces.

The guarded edit removes exactly 5,676 vertices and 10,720 triangles belonging
to all four pots, soil discs, 48 stems and 48 leaves. It accepts only complete
connected components with the expected plant materials inside each known
planter envelope. A saved proof records the previous and resulting target
hashes, all nine retained mesh hashes and the unchanged sconce signatures.
The patched group has 522 vertices and 1,020 triangles (including the original
sconces), down from 5,974 vertices and 11,308 triangles. The final FBX is
11,178,716 bytes and the editable Blender source is 7,572,156 bytes.
`--verify-only` validates those proofs in a fresh Blender process and confirms
four disconnected column components with exact bounds and no remaining plant
triangles. The FBX is exported to a temporary file before replacement.

`create_seoul_envelope.py` retains its original eight snapshots. Its verifier
accepts the later plant-to-column change only when the saved column proof links
the exact original plant hash to the exact current column hash; the other
seven envelope-external meshes must still match their original snapshots.
The original source generator now authors these same four columns instead of
plants and consumes the old plant random draws to preserve later random state.

```powershell
& E:\Blender\blender.exe --background ArtSource/Blender/SimBarVenue.blend --python ArtSource/Blender/create_gray_columns.py -- --dry-run
& E:\Blender\blender.exe --background ArtSource/Blender/SimBarVenue.blend --python ArtSource/Blender/create_gray_columns.py
& E:\Blender\blender.exe --background ArtSource/Blender/SimBarVenue.blend --python ArtSource/Blender/create_gray_columns.py -- --verify-only
& E:\Blender\blender.exe --background ArtSource/Blender/SimBarVenue.blend --python ArtSource/Blender/create_seoul_envelope.py -- --verify-only
```
