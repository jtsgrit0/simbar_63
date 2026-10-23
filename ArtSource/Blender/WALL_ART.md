# Pasted music collage source

The venue has no baked poster frames. `SimBarMusicPosters` adds overlapping
paper artwork against the original brick walls at runtime. Floor graffiti is
also an independent decoration layer, so it does not alter floor geometry.

`create_simbar_venue.py` omits the legacy three framed poster assemblies.
For the already-authored venue, the smaller patch below removes only those
complete connected mesh components and re-exports `SimBarVenue.fbx`:

```powershell
& 'E:/Blender/blender.exe' --background 'ArtSource/Blender/SimBarVenue.blend' --python 'ArtSource/Blender/remove_framed_wall_art.py'
```

Append `-- --dry-run` to inspect, or `-- --verify-only` to validate the saved
scene without writing files. The patch checks exact retained plant and sconce
triangles, material slots, and SHA-256 geometry digests of the other nine mesh
groups. It removes 40,338 vertices / 80,232 triangles belonging to the legacy
frames, mats, lettering and graphics. The ten mesh-group names stay stable so
Unity's generated venue prefab continues to refer to the same imported meshes.

FBX export drops the four now-unused poster material slots from the wall-art
mesh. The matching renderer in `SimBarVenueReady.prefab` is remapped by source
material name to FoliageLight, Foliage, WarmBulb, MetalBlack, DarkWalnut and
Ceramic. All ten prefab material arrays were checked against their imported
source submesh order after this change.

The tile surface is at Unity Y = 0.0615 m. Decorations sit slightly above that
surface and have no colliders. Wall interior faces are Z = 7.405 m at the rear
and X = -10.205 m at the left.
