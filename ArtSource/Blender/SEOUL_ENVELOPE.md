# Seoul window envelope

`create_seoul_envelope.py` updates only the two existing joined meshes
`SimBar_Floor` and `SimBar_BrickWalls` in the saved venue. Their names stay
stable for the Unity prefab. At that patch, all eight furniture, stage,
instrument, plant and lighting meshes retained identical geometry, topology,
material-slot and transform SHA-256 digests, including the staff aisle.
The later [grey column patch](GRAY_COLUMNS.md) replaces only the plant mesh;
the verifier accepts its exact original-to-result hash chain without rebasing
the original envelope snapshots. The other seven hashes still match directly.

The new floor is one continuous bevelled concrete slab, with no brown tile grid
or brass floor edge. Its Unity bounds are X ±10.5 m, Z ±7.75 m,
Y −0.28 to 0.0615 m. The walkable height and footprint are unchanged.

The rear and left walls are solid charcoal knee walls from Y 0 to 1.67 m.
The rear interior face stays at Z 7.405 m and the left interior face stays at
X −10.205 m. Upper hinged windows and the Seoul night street are separate
runtime scenery; no opaque upper brick, wood crown or skirting remains here.

| Material | RGB | Roughness | Metallic |
| --- | --- | --- | --- |
| `WallCharcoal` | 0.075, 0.085, 0.10 | 0.82 | 0 |
| `FloorConcrete` | 0.39, 0.42, 0.45 | 0.68 | 0 |

The generator's floor/wall sections use the same dimensions and materials.
The former brick loops still consume their original seeded random choices so
later bottle placement does not change during regeneration. The current
generator authors four concrete columns at the former planter positions and
also consumes the old plant random draws to preserve the remaining sequence.

Run from the project root:

```powershell
& E:\Blender\blender.exe --background ArtSource/Blender/SimBarVenue.blend --python ArtSource/Blender/create_seoul_envelope.py -- --dry-run
& E:\Blender\blender.exe --background ArtSource/Blender/SimBarVenue.blend --python ArtSource/Blender/create_seoul_envelope.py
& E:\Blender\blender.exe --background ArtSource/Blender/SimBarVenue.blend --python ArtSource/Blender/create_seoul_envelope.py -- --verify-only
```

The patch guards the original mesh counts, required earlier patches and identity
transforms. It is idempotent, exports to a temporary FBX before replacement,
records all eight retained hashes in the floor mesh and manifest, and verifies
those hashes again after saving and reloading. Dry-run, apply and fresh-process
verify-only passed on Blender 5.2.2 LTS.
