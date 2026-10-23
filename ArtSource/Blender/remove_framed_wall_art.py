"""Remove only the three old baked poster/frame assemblies from the saved venue.

Run: Blender --background ArtSource/Blender/SimBarVenue.blend --python this_file.py
Use -- --dry-run to inspect or -- --verify-only to validate a patched scene.
The remaining plants/sconces and the other nine venue meshes stay unchanged.
Runtime pasted-paper artwork now supplies the wall's music collage.
"""
import bpy
import bmesh
import collections
import hashlib
import json
import os
import struct
import sys

PROJECT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
SOURCE = os.path.join(PROJECT, 'ArtSource', 'Blender')
BLEND = os.path.join(SOURCE, 'SimBarVenue.blend')
FBX = os.path.join(PROJECT, 'Assets', 'Resources', 'SimBarArt', 'SimBarVenue.fbx')
MANIFEST = os.path.join(SOURCE, 'SimBarVenue_manifest.json')
TARGET_NAME = 'SimBar_PlantsAndWallArt'
PATCH_KEY = 'SimBarBakedPosterAssembliesRemoved'
CENTERS = (-8.3, -4.8, -0.95)
ALLOWED_MATERIALS = {'DarkWalnut', 'PosterPaper', 'PosterOrange', 'PosterPurple', 'PosterTeal'}
ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
DRY_RUN = '--dry-run' in ARGS
VERIFY_ONLY = '--verify-only' in ARGS


def geometry_digest(obj):
    digest = hashlib.sha256()
    mesh = obj.data
    for vertex in mesh.vertices:
        digest.update(struct.pack('<3f', *vertex.co))
    for edge in mesh.edges:
        digest.update(struct.pack('<2I', *edge.vertices))
    for polygon in mesh.polygons:
        digest.update(struct.pack('<II', len(polygon.vertices), polygon.material_index))
        digest.update(struct.pack('<' + 'I' * len(polygon.vertices), *polygon.vertices))
    digest.update(repr(tuple(material.name if material else None for material in mesh.materials)).encode())
    digest.update(repr(tuple(tuple(row) for row in obj.matrix_world)).encode())
    return digest.hexdigest()


def face_signatures(mesh, excluded=frozenset()):
    """Index-independent exact geometry/material/winding check for retained faces."""
    result = collections.Counter()
    for face in mesh.polygons:
        if any(index in excluded for index in face.vertices):
            continue
        points = tuple(tuple(mesh.vertices[index].co) for index in face.vertices)
        rotations = tuple(points[i:] + points[:i] for i in range(len(points)))
        result[(min(rotations), face.material_index, face.use_smooth)] += 1
    return result


def components(mesh):
    neighbors = [[] for _ in mesh.vertices]
    for edge in mesh.edges:
        a, b = edge.vertices
        neighbors[a].append(b)
        neighbors[b].append(a)
    visited = set()
    for start in range(len(mesh.vertices)):
        if start in visited:
            continue
        pending = [start]
        selected = set()
        while pending:
            index = pending.pop()
            if index in selected:
                continue
            selected.add(index)
            pending.extend(neighbors[index])
        visited.update(selected)
        yield selected


def inside_poster_volume(position, center):
    # Blender scene coordinates are X / Unity Z / Unity height Y.
    return (abs(position.x - center) <= 1.04 and
            2.48 <= position.z <= 4.96 and
            7.12 <= position.y <= 7.36)


if os.path.normcase(os.path.abspath(bpy.data.filepath)) != os.path.normcase(BLEND):
    bpy.ops.wm.open_mainfile(filepath=BLEND)
target = bpy.data.objects.get(TARGET_NAME)
assert target is not None and target.type == 'MESH', 'Missing joined wall-art mesh'
mesh = target.data
meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
assert len(meshes) == 10, 'Expected the ten existing venue mesh groups'
other_digests = {obj.name: geometry_digest(obj) for obj in meshes if obj != target}
original_materials = tuple(material.name if material else None for material in mesh.materials)
original_vertex_count = len(mesh.vertices)
original_face_count = len(mesh.polygons)
selected = set()
selection_report = []
per_poster = {str(center): collections.Counter() for center in CENTERS}
for component in components(mesh):
    points = [target.matrix_world @ mesh.vertices[index].co for index in component]
    for center in CENTERS:
        if not all(inside_poster_volume(point, center) for point in points):
            continue
        component_faces = [face for face in mesh.polygons if face.vertices[0] in component]
        names = {mesh.materials[face.material_index].name for face in component_faces}
        assert names and names <= ALLOWED_MATERIALS, 'Unexpected non-poster material in deletion bounds'
        selected.update(component)
        for name in names:
            per_poster[str(center)][name] += 1
        selection_report.append({'centerX': center, 'vertices': len(component),
                                 'triangles': len(component_faces), 'materials': sorted(names)})
        break

was_patched = int(mesh.get(PATCH_KEY, 0)) == 3
if was_patched:
    assert not selected, 'Patched scene still contains old poster assemblies'
else:
    assert len(selected) == 40338, 'Unexpected old-poster vertex count: ' + str(len(selected))
    for center in CENTERS:
        materials = per_poster[str(center)]
        assert materials['DarkWalnut'] == 1, 'Expected exactly one complete frame per old poster'
        assert materials['PosterPaper'] >= 7, 'Expected mat, graphics and lettering components'
        assert sum(materials[name] for name in ('PosterOrange', 'PosterPurple', 'PosterTeal')) == 1, 'Expected one color block'
if VERIFY_ONLY:
    assert was_patched, 'Saved scene has not been patched'

remaining_faces = face_signatures(mesh, selected)
selected_faces = sum(1 for face in mesh.polygons if face.vertices[0] in selected)
if not was_patched:
    assert selected_faces == 80232, 'Unexpected old-poster triangle count'
if not DRY_RUN and not VERIFY_ONLY and not was_patched:
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.verts[index] for index in selected], context='VERTS')
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    mesh[PATCH_KEY] = 3
    assert len(mesh.vertices) == original_vertex_count - len(selected), 'Unexpected retained vertex count'
    assert len(mesh.polygons) == original_face_count - selected_faces, 'Unexpected retained face count'
    assert face_signatures(mesh) == remaining_faces, 'Retained plant/sconce geometry, winding or materials changed'
assert tuple(material.name if material else None for material in mesh.materials) == original_materials, 'Wall-art material slots changed'
assert all(geometry_digest(obj) == other_digests[obj.name] for obj in meshes if obj != target), 'Another venue mesh changed'

report = {'sourceMesh': TARGET_NAME, 'removedAssemblies': 3 if was_patched or not DRY_RUN else 0,
          'selectedVertices': len(selected), 'selectedTriangles': selected_faces,
          'selectedComponents': len(selection_report),
          'posterMaterialComponents': {center: dict(counts) for center, counts in per_poster.items()},
          'unchangedOtherMeshes': len(other_digests),
          'plantsAndSconcesUnchanged': True, 'materialSlotsUnchanged': True,
          'dryRun': DRY_RUN, 'verifyOnly': VERIFY_ONLY}
print('SIMBAR_BAKED_POSTERS_VERIFIED', json.dumps(report, sort_keys=True), flush=True)

if not DRY_RUN and not VERIFY_ONLY:
    temporary_fbx = os.path.join(SOURCE, 'SimBarVenue.wall_art_export.fbx')
    try:
        bpy.ops.object.select_all(action='DESELECT')
        for obj in meshes:
            obj.select_set(True)
        bpy.ops.export_scene.fbx(filepath=temporary_fbx, use_selection=True, object_types={'MESH'},
            axis_forward='-Z', axis_up='Y', apply_unit_scale=True, global_scale=1,
            bake_space_transform=True, use_mesh_modifiers=True, mesh_smooth_type='FACE',
            use_tspace=False, path_mode='AUTO', add_leaf_bones=False, bake_anim=False)
        assert all(geometry_digest(obj) == other_digests[obj.name] for obj in meshes if obj != target), 'Export changed another mesh'
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=BLEND)
        os.replace(temporary_fbx, FBX)
    finally:
        if os.path.exists(temporary_fbx):
            os.remove(temporary_fbx)
    with open(MANIFEST, encoding='utf-8') as handle:
        manifest = json.load(handle)
    manifest['meshes'][TARGET_NAME] = {'vertices': len(mesh.vertices), 'triangles': len(mesh.polygons),
                                      'nonTriangleFaces': sum(1 for face in mesh.polygons if len(face.vertices) != 3)}
    if not was_patched:
        manifest['wallArt'] = {'bakedFramesRemoved': 3, 'sourceMesh': TARGET_NAME,
            'removedVertices': len(selected), 'removedTriangles': selected_faces,
            'unchangedOtherMeshes': len(other_digests), 'plantsAndSconcesUnchanged': True,
            'replacement': 'Runtime overlapping pasted-paper music collage'}
    with open(MANIFEST, 'w', encoding='utf-8') as handle:
        json.dump(manifest, handle, indent=2)
    print('SIMBAR_BAKED_POSTERS_SAVED', BLEND, FBX, flush=True)
