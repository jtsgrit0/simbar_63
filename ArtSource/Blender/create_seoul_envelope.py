"""Replace only the saved venue's brown floor and full-height brick walls.

Run: blender --background ArtSource/Blender/SimBarVenue.blend --python this_file.py
Use -- --dry-run to inspect or -- --verify-only to validate the saved result.
The eight other joined meshes, their topology, materials and transforms must be
byte-for-byte unchanged. Upper casement windows are separate runtime scenery.
"""
import bpy
import bmesh
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
PATCH_KEY = 'SimBarSeoulEnvelopeVersion'
DIGEST_KEY = 'SimBarSeoulUnchangedMeshDigests'
VERSION = 1
TARGETS = {'SimBar_Floor', 'SimBar_BrickWalls'}
OLD_COUNTS = {'SimBar_Floor': (18984, 36612), 'SimBar_BrickWalls': (35672, 68796)}
MATERIALS = {'FloorConcrete': ((.39, .42, .45), .68),
             'WallCharcoal': ((.075, .085, .10), .82)}
ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
DRY_RUN = '--dry-run' in ARGS
VERIFY_ONLY = '--verify-only' in ARGS


def geometry_digest(obj):
    """Hash geometry, topology, material slots and object transforms exactly."""
    digest = hashlib.sha256()
    mesh = obj.data
    for vertex in mesh.vertices:
        digest.update(struct.pack('<3f', *vertex.co))
    for edge in mesh.edges:
        digest.update(struct.pack('<2I', *edge.vertices))
    for face in mesh.polygons:
        digest.update(struct.pack('<II', len(face.vertices), face.material_index))
        digest.update(struct.pack('<' + 'I' * len(face.vertices), *face.vertices))
    digest.update(repr(tuple(material.name if material else None for material in mesh.materials)).encode())
    digest.update(repr(tuple(tuple(row) for row in obj.matrix_world)).encode())
    return digest.hexdigest()


def unity_bounds(obj):
    points = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    return {'x': [min(p.x for p in points), max(p.x for p in points)],
            'y': [min(p.z for p in points), max(p.z for p in points)],
            'z': [min(p.y for p in points), max(p.y for p in points)]}


def material(name):
    color, roughness = MATERIALS[name]
    result = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    result.diffuse_color = (*color, 1)
    result.use_nodes = True
    shader = result.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = (*color, 1)
    shader.inputs['Roughness'].default_value = roughness
    shader.inputs['Metallic'].default_value = 0
    return result


def replace_with_boxes(obj, boxes, material_name, bevel_width):
    vertices, faces = [], []
    for center, size in boxes:
        x, z, y = size[0] / 2, size[1] / 2, size[2] / 2
        cx, cz, cy = center
        start = len(vertices)
        vertices.extend((cx + vx, cy + vy, cz + vz) for vx, vy, vz in
                        ((-x, -y, -z), (x, -y, -z), (x, y, -z), (-x, y, -z),
                         (-x, -y, z), (x, -y, z), (x, y, z), (-x, y, z)))
        faces.extend(tuple(start + index for index in face) for face in
                     ((3, 2, 1, 0), (0, 1, 5, 4), (1, 2, 6, 5),
                      (2, 3, 7, 6), (3, 0, 4, 7), (4, 5, 6, 7)))
    mesh = bpy.data.meshes.new(obj.name + '_SeoulEnvelope')
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(material(material_name))
    mesh.update()
    old_mesh = obj.data
    obj.data = mesh
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bevel = obj.modifiers.new('Crafted bevel edges', 'BEVEL')
    bevel.width = bevel_width
    bevel.segments = 2
    bevel.affect = 'EDGES'
    bevel.limit_method = 'ANGLE'
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    normals = obj.modifiers.new('Weighted corner normals', 'WEIGHTED_NORMAL')
    bpy.ops.object.modifier_apply(modifier=normals.name)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=.000001)
    bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=.0000001)
    bmesh.ops.triangulate(bm, faces=list(bm.faces), quad_method='BEAUTY', ngon_method='BEAUTY')
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    mesh[PATCH_KEY] = VERSION
    if old_mesh.users == 0:
        bpy.data.meshes.remove(old_mesh)
    obj.select_set(False)


def verify_envelope(meshes, other_digests):
    assert len(meshes) == 10, 'Venue must retain its ten joined mesh groups'
    assert all(geometry_digest(obj) == other_digests[obj.name]
               for obj in meshes if obj.name not in TARGETS), 'Another venue mesh changed'
    for name, material_name in [('SimBar_Floor', 'FloorConcrete'), ('SimBar_BrickWalls', 'WallCharcoal')]:
        obj = bpy.data.objects[name]
        assert int(obj.data.get(PATCH_KEY, 0)) == VERSION, 'Envelope patch marker missing'
        assert tuple(m.name for m in obj.data.materials) == (material_name,), 'Unexpected envelope materials'
        assert all(face.material_index == 0 and len(face.vertices) == 3 for face in obj.data.polygons), 'Non-triangle or old floor/wall material'
        shader = obj.data.materials[0].node_tree.nodes.get('Principled BSDF')
        expected, roughness = MATERIALS[material_name]
        assert all(abs(a - b) < .000001 for a, b in zip(shader.inputs['Base Color'].default_value, (*expected, 1))), 'Unexpected envelope color'
        assert abs(shader.inputs['Roughness'].default_value - roughness) < .000001, 'Unexpected envelope roughness'
    floor = unity_bounds(bpy.data.objects['SimBar_Floor'])
    for axis, expected in {'x': (-10.5, 10.5), 'y': (-.28, .0615), 'z': (-7.75, 7.75)}.items():
        assert all(abs(a - b) < .000005 for a, b in zip(floor[axis], expected)), 'Walkable floor footprint/top changed'
    walls = unity_bounds(bpy.data.objects['SimBar_BrickWalls'])
    assert abs(walls['y'][0]) < .000005 and abs(walls['y'][1] - 1.67) < .000005, 'Charcoal walls must occupy the lower third only'
    assert len(bpy.data.objects['SimBar_Floor'].data.vertices) < 100, 'Floor still has a checker grid'
    assert len(bpy.data.objects['SimBar_BrickWalls'].data.vertices) < 150, 'Walls still contain bricks/crown'
    return floor, walls


if os.path.normcase(os.path.abspath(bpy.data.filepath)) != os.path.normcase(BLEND):
    bpy.ops.wm.open_mainfile(filepath=BLEND)
meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
assert len(meshes) == 10 and TARGETS <= {obj.name for obj in meshes}, 'Unexpected venue mesh groups'
assert abs(float(bpy.data.objects['SimBar_BarCounter'].data.get('SimBarStaffAisleOffsetZ', 0)) - .25) < .000001, 'Staff aisle patch is required'
assert int(bpy.data.objects['SimBar_PlantsAndWallArt'].data.get('SimBarBakedPosterAssembliesRemoved', 0)) == 3, 'Baked poster removal is required'
before_other = {obj.name: geometry_digest(obj) for obj in meshes if obj.name not in TARGETS}
patch_states = [int(bpy.data.objects[name].data.get(PATCH_KEY, 0)) for name in sorted(TARGETS)]
assert patch_states in ([0, 0], [VERSION, VERSION]), 'Partially applied or incompatible envelope patch'
was_patched = patch_states == [VERSION, VERSION]
if was_patched:
    recorded_digests = json.loads(bpy.data.objects['SimBar_Floor'].data.get(DIGEST_KEY, '{}'))
    if recorded_digests != before_other:
        # A later, separately guarded edit replaces only the four plants. Keep
        # the original envelope snapshots rather than silently rebasing them.
        column_name = 'SimBar_PlantsAndWallArt'
        columns = bpy.data.objects[column_name].data
        proof = json.loads(columns.get('SimBarGrayColumnsProof', '{}'))
        assert int(columns.get('SimBarGrayColumnsVersion', 0)) == 1, 'An unrelated mesh changed after the envelope patch'
        assert set(recorded_digests) == set(before_other), 'Envelope mesh groups changed'
        assert all(recorded_digests[name] == before_other[name] for name in recorded_digests if name != column_name), 'Another unrelated mesh changed after the envelope patch'
        assert recorded_digests[column_name] == proof.get('replacesGeometryDigest'), 'Column patch does not descend from the recorded plant mesh'
        assert before_other[column_name] == proof.get('resultGeometryDigest'), 'Column mesh changed after its guarded replacement'
else:
    for name in TARGETS:
        obj = bpy.data.objects[name]
        assert (len(obj.data.vertices), len(obj.data.polygons)) == OLD_COUNTS[name], 'Unexpected original floor/wall topology'
        assert not obj.modifiers, 'Unexpected live source modifier'
        assert all(abs(obj.matrix_world[row][col] - (1 if row == col else 0)) < .000001
                   for row in range(4) for col in range(4)), 'Source mesh origin/transform changed'
if VERIFY_ONLY:
    assert was_patched, 'Saved scene has not been patched'
if not DRY_RUN and not VERIFY_ONLY and not was_patched:
    replace_with_boxes(bpy.data.objects['SimBar_Floor'], [((0, -.10925, 0), (21, .3415, 15.5))], 'FloorConcrete', .009)
    replace_with_boxes(bpy.data.objects['SimBar_BrickWalls'],
                       [((0, .835, 7.55), (21, 1.67, .29)),
                        ((-10.35, .835, 0), (.29, 1.67, 15.5))], 'WallCharcoal', .016)
    bpy.data.objects['SimBar_Floor'].data[DIGEST_KEY] = json.dumps(before_other, sort_keys=True)

if was_patched or not DRY_RUN:
    floor_bounds, wall_bounds = verify_envelope(meshes, before_other)
else:
    floor_bounds = {'x': [-10.5, 10.5], 'y': [-.28, .0615], 'z': [-7.75, 7.75]}
    wall_bounds = {'x': [-10.5, 10.5], 'y': [0, 1.67], 'z': [-7.75, 7.75]}
report = {'version': VERSION, 'changedMeshes': sorted(TARGETS), 'unchangedOtherMeshes': len(before_other),
          'unchangedGeometryDigests': before_other, 'floorUnityBounds': floor_bounds,
          'wallUnityBounds': wall_bounds, 'lowerWallHeight': 1.67,
          'rearInteriorZ': 7.405, 'leftInteriorX': -10.205,
          'floorMaterial': 'FloorConcrete', 'wallMaterial': 'WallCharcoal',
          'dryRun': DRY_RUN, 'verifyOnly': VERIFY_ONLY, 'wasAlreadyPatched': was_patched}
print('SIMBAR_SEOUL_ENVELOPE_VERIFIED', json.dumps(report, sort_keys=True), flush=True)

if not DRY_RUN and not VERIFY_ONLY:
    temporary_fbx = os.path.join(SOURCE, 'SimBarVenue.seoul_envelope_export.fbx')
    try:
        bpy.ops.object.select_all(action='DESELECT')
        for obj in meshes:
            obj.select_set(True)
        bpy.ops.export_scene.fbx(filepath=temporary_fbx, use_selection=True, object_types={'MESH'},
            axis_forward='-Z', axis_up='Y', apply_unit_scale=True, global_scale=1,
            bake_space_transform=True, use_mesh_modifiers=True, mesh_smooth_type='FACE',
            use_tspace=False, path_mode='AUTO', add_leaf_bones=False, bake_anim=False)
        verify_envelope(meshes, before_other)
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=BLEND)
        os.replace(temporary_fbx, FBX)
    finally:
        if os.path.exists(temporary_fbx):
            os.remove(temporary_fbx)
    with open(MANIFEST, encoding='utf-8') as handle:
        manifest = json.load(handle)
    for name in TARGETS:
        mesh = bpy.data.objects[name].data
        manifest['meshes'][name] = {'vertices': len(mesh.vertices), 'triangles': len(mesh.polygons),
                                   'nonTriangleFaces': sum(1 for face in mesh.polygons if len(face.vertices) != 3)}
    for name, (color, roughness) in MATERIALS.items():
        manifest['materials'][name] = {'color': list(color), 'roughness': roughness, 'metallic': 0, 'emission': 0}
    manifest['seoulEnvelope'] = {**report,
        'upperWindows': 'Runtime hinged casement glazing and Seoul neon streetscape',
        'replaces': 'Full-height brown brick walls and brown checkerboard floor'}
    with open(MANIFEST, 'w', encoding='utf-8') as handle:
        json.dump(manifest, handle, indent=2)
    print('SIMBAR_SEOUL_ENVELOPE_SAVED', BLEND, FBX, flush=True)
