"""Open the saved venue's staff aisle without rebuilding any other furniture.

Run: Blender --background ArtSource/Blender/SimBarVenue.blend --python this_file.py
Add -- --dry-run to inspect, or -- --verify-only to check an already patched scene.
Source coordinates are Unity X / height Y / depth Z in metres; Blender world
coordinates are X / Unity Z / Unity Y. FBX options match the venue generator.
"""
import bpy
import hashlib
import json
import os
import struct
import sys
from mathutils import Vector

PROJECT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
SOURCE = os.path.join(PROJECT, 'ArtSource', 'Blender')
BLEND = os.path.join(SOURCE, 'SimBarVenue.blend')
FBX = os.path.join(PROJECT, 'Assets', 'Resources', 'SimBarArt', 'SimBarVenue.fbx')
MANIFEST = os.path.join(SOURCE, 'SimBarVenue_manifest.json')
OFFSET_KEY = 'SimBarStaffAisleOffsetZ'
TARGET_OFFSET = 0.25
TARGET_NAME = 'SimBar_BarCounter'
ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
DRY_RUN = '--dry-run' in ARGS
VERIFY_ONLY = '--verify-only' in ARGS


def geometry_digest(obj):
    """Includes geometry, topology, material slots and object transforms."""
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


def unity_position(world):
    return (world.x, world.z, world.y)


def bounds(points):
    values = list(points)
    return {'x': [min(p[0] for p in values), max(p[0] for p in values)],
            'y': [min(p[1] for p in values), max(p[1] for p in values)],
            'z': [min(p[2] for p in values), max(p[2] for p in values)]}


def connected_selection(mesh, selected):
    """Reject a partial component: a translated component must keep its shape."""
    neighbors = [[] for _ in mesh.vertices]
    for edge in mesh.edges:
        a, b = edge.vertices
        neighbors[a].append(b)
        neighbors[b].append(a)
    visited = set()
    components = []
    for start in sorted(selected):
        if start in visited:
            continue
        pending = [start]
        component = set()
        while pending:
            index = pending.pop()
            if index in component:
                continue
            component.add(index)
            pending.extend(neighbors[index])
        assert component <= selected, 'Selection cuts through a connected furniture component'
        visited.update(component)
        components.append(len(component))
    return components


if os.path.normcase(os.path.abspath(bpy.data.filepath)) != os.path.normcase(BLEND):
    bpy.ops.wm.open_mainfile(filepath=BLEND)

target = bpy.data.objects.get(TARGET_NAME)
assert target is not None and target.type == 'MESH', 'Missing joined bar-counter mesh'
mesh = target.data
meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
assert len(meshes) == 10, 'Expected the original ten venue mesh groups'
before_other = {obj.name: geometry_digest(obj) for obj in meshes if obj != target}
before_positions = [vertex.co.copy() for vertex in mesh.vertices]
before_world = [target.matrix_world @ position for position in before_positions]
topology = (len(mesh.vertices), len(mesh.edges), len(mesh.polygons),
            tuple(tuple(p.vertices) for p in mesh.polygons),
            tuple(p.material_index for p in mesh.polygons),
            tuple(material.name if material else None for material in mesh.materials))
previous_offset = float(mesh.get(OFFSET_KEY, 0.0))
assert abs(previous_offset) < 0.000001 or abs(previous_offset - TARGET_OFFSET) < 0.000001, 'Unexpected previous aisle offset'
delta = TARGET_OFFSET - previous_offset
selected = set()
for index, world in enumerate(before_world):
    x, height, depth = unity_position(world)
    original_depth = depth - previous_offset
    if -9.75 <= x <= -2.45 and 0.90 <= height <= 1.58 and -0.52 <= original_depth <= 0.22:
        selected.add(index)
assert 100 <= len(selected) <= 2000, 'Unexpected worktop/sink selection size'
components = connected_selection(mesh, selected)
assert len(components) >= 3, 'Expected a worktop, sink parts and faucet'
if VERIFY_ONLY:
    assert abs(delta) < 0.000001, 'The saved venue has not been patched'

if not DRY_RUN and not VERIFY_ONLY and abs(delta) > 0.000001:
    inverse = target.matrix_world.inverted()
    for index in selected:
        moved = before_world[index] + Vector((0.0, delta, 0.0))
        mesh.vertices[index].co = inverse @ moved
    mesh.update()
    mesh[OFFSET_KEY] = TARGET_OFFSET

applied_delta = delta if not DRY_RUN and not VERIFY_ONLY else 0.0
for index, vertex in enumerate(mesh.vertices):
    if index in selected:
        actual = target.matrix_world @ vertex.co
        expected = before_world[index] + Vector((0.0, applied_delta, 0.0))
        assert (actual - expected).length < 0.000002, 'Selected vertex moved by an unexpected amount'
    else:
        assert tuple(vertex.co) == tuple(before_positions[index]), 'Unselected bar geometry changed'
assert topology == (len(mesh.vertices), len(mesh.edges), len(mesh.polygons),
                    tuple(tuple(p.vertices) for p in mesh.polygons),
                    tuple(p.material_index for p in mesh.polygons),
                    tuple(material.name if material else None for material in mesh.materials)), 'Bar topology/materials changed'
assert all(geometry_digest(obj) == before_other[obj.name] for obj in meshes if obj != target), 'Another venue mesh changed'

selected_bounds = bounds(unity_position(target.matrix_world @ mesh.vertices[index].co) for index in selected)
report = {'mesh': TARGET_NAME, 'selectedVertices': len(selected),
          'unselectedVertices': len(mesh.vertices) - len(selected),
          'selectedComponentSizes': components, 'previousOffsetZ': previous_offset,
          'appliedOffsetZ': applied_delta, 'selectedUnityBounds': selected_bounds,
          'unchangedOtherMeshes': len(before_other), 'topologyAndMaterialsUnchanged': True,
          'dryRun': DRY_RUN, 'verifyOnly': VERIFY_ONLY}
print('SIMBAR_STAFF_AISLE_VERIFIED', json.dumps(report, sort_keys=True), flush=True)

if not DRY_RUN and not VERIFY_ONLY:
    # Export away from Assets, then replace the final FBX only once it is complete.
    # This prevents Unity from importing a partially written file.
    temporary_fbx = os.path.join(SOURCE, 'SimBarVenue.staff_aisle_export.fbx')
    try:
        bpy.ops.object.select_all(action='DESELECT')
        for obj in meshes:
            obj.select_set(True)
        bpy.ops.export_scene.fbx(filepath=temporary_fbx, use_selection=True, object_types={'MESH'},
            axis_forward='-Z', axis_up='Y', apply_unit_scale=True, global_scale=1,
            bake_space_transform=True, use_mesh_modifiers=True, mesh_smooth_type='FACE',
            use_tspace=False, path_mode='AUTO', add_leaf_bones=False, bake_anim=False)
        assert all(geometry_digest(obj) == before_other[obj.name] for obj in meshes if obj != target), 'Export changed another mesh'
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=BLEND)
        os.replace(temporary_fbx, FBX)
    finally:
        if os.path.exists(temporary_fbx):
            os.remove(temporary_fbx)
    with open(MANIFEST, encoding='utf-8') as handle:
        manifest = json.load(handle)
    manifest['staffAisle'] = {'workSurfaceOffsetZ': TARGET_OFFSET, 'z': [-0.88, -0.25],
        'width': 0.63, 'centerZ': -0.565, 'radius': 0.24, 'walkX': [-9.0, -2.8],
        'sourceMesh': TARGET_NAME, 'selectedVertices': len(selected),
        'unchangedOtherMeshes': len(before_other)}
    with open(MANIFEST, 'w', encoding='utf-8') as handle:
        json.dump(manifest, handle, indent=2)
    print('SIMBAR_STAFF_AISLE_SAVED', BLEND, FBX, flush=True)
