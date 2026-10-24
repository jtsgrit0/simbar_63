"""Replace the four saved venue plants with grey concrete columns only.

Run with the saved SimBarVenue.blend. Use -- --dry-run or -- --verify-only.
The ten joined groups stay stable; all nine other groups and every existing
sconce face retain their exact geometry, topology, material and transform.
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
PATCH_KEY = 'SimBarGrayColumnsVersion'
DATA_KEY = 'SimBarGrayColumnsProof'
VERSION = 1
CENTERS = ((-9.55, -5.9), (-1.25, 5.75), (-9.4, 6.7), (9.73, 6.65))
WIDTH = .62
BOTTOM = .0615
TOP = 5.0
COLOR = (.23, .25, .28)
ROUGHNESS = .78
PLANT_MATERIALS = {'Ceramic', 'DarkWalnut', 'Foliage', 'FoliageLight'}
SCONCE_MATERIALS = {'MetalBlack', 'WarmBulb'}


def geometry_digest(obj):
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
        pending, selected = [start], set()
        while pending:
            index = pending.pop()
            if index in selected:
                continue
            selected.add(index)
            pending.extend(neighbors[index])
        visited.update(selected)
        yield selected


def signatures(mesh, excluded=frozenset(), materials=None):
    result = collections.Counter()
    for face in mesh.polygons:
        if any(index in excluded for index in face.vertices):
            continue
        name = mesh.materials[face.material_index].name
        if materials is not None and name not in materials:
            continue
        points = tuple(tuple(mesh.vertices[index].co) for index in face.vertices)
        rotations = tuple(points[i:] + points[:i] for i in range(len(points)))
        result[(min(rotations), face.material_index, face.use_smooth)] += 1
    return result


def signature_digest(value):
    return hashlib.sha256(repr(sorted(value.items())).encode()).hexdigest()


def create_column_mesh():
    vertices, faces = [], []
    half = WIDTH / 2
    for x, z in CENTERS:
        start = len(vertices)
        # Blender X / Unity depth Z / Unity height Y.
        vertices.extend((x + vx, z + vz, y) for vx, vz, y in
                        ((-half, -half, BOTTOM), (half, -half, BOTTOM),
                         (half, half, BOTTOM), (-half, half, BOTTOM),
                         (-half, -half, TOP), (half, -half, TOP),
                         (half, half, TOP), (-half, half, TOP)))
        faces.extend(tuple(start + index for index in face) for face in
                     ((3, 2, 1, 0), (0, 1, 5, 4), (1, 2, 6, 5),
                      (2, 3, 7, 6), (3, 0, 4, 7), (4, 5, 6, 7)))
    mesh = bpy.data.meshes.new('FourGreyConcreteColumns')
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new('TemporaryGreyColumns', mesh)
    bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bevel = obj.modifiers.new('Crafted bevel edges', 'BEVEL')
    bevel.width, bevel.segments, bevel.affect, bevel.limit_method = .016, 2, 'EDGES', 'ANGLE'
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=.000001)
    bmesh.ops.triangulate(bm, faces=list(bm.faces), quad_method='BEAUTY', ngon_method='BEAUTY')
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    bpy.data.objects.remove(obj, do_unlink=True)
    return mesh


def column_material():
    mat = bpy.data.materials.get('ColumnConcrete') or bpy.data.materials.new('ColumnConcrete')
    mat.diffuse_color = (*COLOR, 1)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = (*COLOR, 1)
    shader.inputs['Roughness'].default_value = ROUGHNESS
    shader.inputs['Metallic'].default_value = 0
    return mat


def verify(meshes, proof):
    assert len(meshes) == 10, 'Venue must retain its ten joined mesh groups'
    target = bpy.data.objects[TARGET_NAME]
    mesh = target.data
    assert int(mesh.get(PATCH_KEY, 0)) == VERSION, 'Column patch marker missing'
    assert all(geometry_digest(obj) == proof['unchangedGeometryDigests'][obj.name]
               for obj in meshes if obj != target), 'Another venue mesh changed'
    assert geometry_digest(target) == proof['resultGeometryDigest'], 'Column or sconce geometry changed'
    assert signature_digest(signatures(mesh, materials=SCONCE_MATERIALS)) == proof['retainedSconceFaceDigest'], 'Existing sconces changed'
    assert len(signatures(mesh, materials=SCONCE_MATERIALS)) == 588, 'Sconce triangles missing'
    assert all(len(face.vertices) == 3 for face in mesh.polygons), 'Non-triangle column mesh'
    used = {mesh.materials[face.material_index].name for face in mesh.polygons}
    assert used == SCONCE_MATERIALS | {'ColumnConcrete'}, 'Plant geometry remains or unexpected material added'
    column_components = []
    for component in components(mesh):
        faces = [face for face in mesh.polygons if face.vertices[0] in component]
        names = {mesh.materials[face.material_index].name for face in faces}
        if names != {'ColumnConcrete'}:
            assert names <= SCONCE_MATERIALS, 'Unexpected leftover non-column component'
            continue
        points = [target.matrix_world @ mesh.vertices[index].co for index in component]
        bounds = [[min(point[i] for point in points), max(point[i] for point in points)] for i in range(3)]
        center = ((bounds[0][0] + bounds[0][1]) / 2, (bounds[1][0] + bounds[1][1]) / 2)
        assert any(abs(center[0] - x) < .00001 and abs(center[1] - z) < .00001 for x, z in CENTERS), 'Column moved from the former plant'
        assert abs(bounds[0][1] - bounds[0][0] - WIDTH) < .00001, 'Column width changed'
        assert abs(bounds[1][1] - bounds[1][0] - WIDTH) < .00001, 'Column depth changed'
        assert abs(bounds[2][0] - BOTTOM) < .00001 and abs(bounds[2][1] - TOP) < .00001, 'Column height changed'
        column_components.append(center)
    assert len(column_components) == len(CENTERS) and len(set(column_components)) == len(CENTERS), 'Expected exactly four separate columns'
    mat = bpy.data.materials['ColumnConcrete']
    shader = mat.node_tree.nodes.get('Principled BSDF')
    assert all(abs(a - b) < .000001 for a, b in zip(shader.inputs['Base Color'].default_value, (*COLOR, 1))), 'Column colour changed'
    assert abs(shader.inputs['Roughness'].default_value - ROUGHNESS) < .000001, 'Column roughness changed'
    return {'vertices': len(mesh.vertices), 'triangles': len(mesh.polygons), 'columnCount': len(column_components)}


def main():
    args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    dry_run, verify_only = '--dry-run' in args, '--verify-only' in args
    if os.path.normcase(os.path.abspath(bpy.data.filepath)) != os.path.normcase(BLEND):
        bpy.ops.wm.open_mainfile(filepath=BLEND)
    target = bpy.data.objects[TARGET_NAME]
    mesh = target.data
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
    assert len(meshes) == 10 and not target.modifiers, 'Unexpected joined source meshes/modifiers'
    assert int(mesh.get('SimBarBakedPosterAssembliesRemoved', 0)) == 3, 'Baked poster removal is required'
    assert int(bpy.data.objects['SimBar_Floor'].data.get('SimBarSeoulEnvelopeVersion', 0)) == 1, 'Seoul envelope is required'
    assert all(abs(target.matrix_world[row][col] - (1 if row == col else 0)) < .000001 for row in range(4) for col in range(4)), 'Source mesh origin/transform changed'
    other_digests = {obj.name: geometry_digest(obj) for obj in meshes if obj != target}
    was_patched = int(mesh.get(PATCH_KEY, 0)) == VERSION
    if was_patched:
        proof = json.loads(mesh[DATA_KEY])
        result = verify(meshes, proof)
    else:
        assert not verify_only, 'Saved scene has not been patched'
        assert (len(mesh.vertices), len(mesh.polygons)) == (5974, 11308), 'Unexpected original plant/sconce topology'
        original_materials = tuple(material.name for material in mesh.materials)
        selected, per_plant = set(), collections.Counter()
        for component in components(mesh):
            faces = [face for face in mesh.polygons if face.vertices[0] in component]
            names = {mesh.materials[face.material_index].name for face in faces}
            if names <= SCONCE_MATERIALS:
                continue
            assert names and names <= PLANT_MATERIALS, 'Unexpected material in plant component'
            points = [target.matrix_world @ mesh.vertices[index].co for index in component]
            matches = [(x, z) for x, z in CENTERS if all(abs(point.x - x) < 1.05 and abs(point.y - z) < 1.05 and 0 <= point.z < 3.3 for point in points)]
            assert len(matches) == 1, 'Plant component is outside its known planter envelope'
            selected.update(component)
            per_plant[str(matches[0])] += 1
        assert len(selected) == 5676 and sorted(per_plant.values()) == [26, 26, 26, 26], 'Unexpected plant assembly count'
        removed_triangles = sum(1 for face in mesh.polygons if face.vertices[0] in selected)
        assert removed_triangles == 10720, 'Unexpected plant triangle count'
        retained = signatures(mesh, excluded=selected)
        assert sum(retained.values()) == 588, 'Expected only the three original wall sconces to remain'
        proof = {'version': VERSION, 'replacesGeometryDigest': geometry_digest(target),
                 'unchangedGeometryDigests': other_digests,
                 'retainedSconceFaceDigest': signature_digest(retained),
                 'removedPlantAssemblies': 4, 'removedVertices': len(selected), 'removedTriangles': removed_triangles,
                 'columnUnityCentersXZ': [list(center) for center in CENTERS],
                 'columnWidth': WIDTH, 'columnBottomY': BOTTOM, 'columnTopY': TOP,
                 'material': 'ColumnConcrete'}
        result = {'vertices': len(mesh.vertices), 'triangles': len(mesh.polygons), 'columnCount': 0}
        if not dry_run:
            column = create_column_mesh()
            mesh.materials.append(column_material())
            column_index = len(mesh.materials) - 1
            bm = bmesh.new()
            bm.from_mesh(mesh)
            bm.verts.ensure_lookup_table()
            bmesh.ops.delete(bm, geom=[bm.verts[index] for index in selected], context='VERTS')
            added = [bm.verts.new(vertex.co) for vertex in column.vertices]
            for face in column.polygons:
                created = bm.faces.new([added[index] for index in face.vertices])
                created.material_index = column_index
                created.smooth = face.use_smooth
            bm.to_mesh(mesh)
            bm.free()
            mesh.update()
            bpy.data.meshes.remove(column)
            assert signatures(mesh, materials=SCONCE_MATERIALS) == retained, 'Retained sconce face geometry/winding/materials changed'
            assert tuple(material.name for material in mesh.materials[:-1]) == original_materials, 'Existing material slots changed'
            mesh[PATCH_KEY] = VERSION
            proof['resultGeometryDigest'] = geometry_digest(target)
            mesh[DATA_KEY] = json.dumps(proof, sort_keys=True)
            result = verify(meshes, proof)
    report = {**proof, **result, 'unchangedOtherMeshes': len(other_digests),
              'dryRun': dry_run, 'verifyOnly': verify_only, 'wasAlreadyPatched': was_patched}
    print('SIMBAR_GRAY_COLUMNS_VERIFIED', json.dumps(report, sort_keys=True), flush=True)
    if dry_run or verify_only:
        return
    temporary_fbx = os.path.join(SOURCE, 'SimBarVenue.gray_columns_export.fbx')
    try:
        bpy.ops.object.select_all(action='DESELECT')
        for obj in meshes:
            obj.select_set(True)
        bpy.ops.export_scene.fbx(filepath=temporary_fbx, use_selection=True, object_types={'MESH'},
            axis_forward='-Z', axis_up='Y', apply_unit_scale=True, global_scale=1,
            bake_space_transform=True, use_mesh_modifiers=True, mesh_smooth_type='FACE',
            use_tspace=False, path_mode='AUTO', add_leaf_bones=False, bake_anim=False)
        verify(meshes, proof)
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=BLEND)
        os.replace(temporary_fbx, FBX)
    finally:
        if os.path.exists(temporary_fbx):
            os.remove(temporary_fbx)
    with open(MANIFEST, encoding='utf-8') as handle:
        manifest = json.load(handle)
    manifest['meshes'][TARGET_NAME] = {**result, 'nonTriangleFaces': 0}
    manifest['materials']['ColumnConcrete'] = {'color': list(COLOR), 'roughness': ROUGHNESS, 'metallic': 0, 'emission': 0}
    manifest['grayColumns'] = {**report, 'replaces': 'All four ceramic planters, soil, stems and foliage', 'sconcesUnchanged': True}
    with open(MANIFEST, 'w', encoding='utf-8') as handle:
        json.dump(manifest, handle, indent=2)
    print('SIMBAR_GRAY_COLUMNS_SAVED', BLEND, FBX, flush=True)


if __name__ == '__main__':
    main()
