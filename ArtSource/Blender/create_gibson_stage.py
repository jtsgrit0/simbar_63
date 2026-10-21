"""Convert the user's original Gibson guitar to a bounded stage prop.

The source FBX is read-only. Output is re-centered, normalized to 1.20 m maximum
dimension and decimated to approximately 25k triangles for repeated band use.
"""
import bpy
import os
import json
from mathutils import Vector

PROJECT=os.path.abspath(os.path.join(os.path.dirname(__file__),'..','..'))
SOURCE=os.path.join(PROJECT,'Assets','Model','gibson.fbx')
OUT=os.path.join(PROJECT,'Assets','Resources','SimBarArt','Gibson_Stage.fbx')
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=SOURCE,use_anim=False)
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
before=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes)
print('GIBSON_IMPORTED',len(meshes),before,flush=True)
for o in meshes:
    world=o.matrix_world.copy()
    o.parent=None
    o.matrix_world=world
    bpy.context.view_layer.objects.active=o
    o.select_set(True)
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    o.select_set(False)
points=[o.matrix_world@Vector(corner) for o in meshes for corner in o.bound_box]
lo=Vector(tuple(min(p[i] for p in points) for i in range(3)))
hi=Vector(tuple(max(p[i] for p in points) for i in range(3)))
center=(lo+hi)*.5
scale=1.2/max(hi-lo)
for o in meshes:
    for vertex in o.data.vertices:
        vertex.co=(o.matrix_world@vertex.co-center)*scale
    o.matrix_world.identity()
    bpy.context.view_layer.objects.active=o
    faces=sum(len(p.vertices)-2 for p in o.data.polygons)
    if before>25000 and faces>500:
        mod=o.modifiers.new('Stage performance decimation','DECIMATE')
        mod.ratio=max(.01,25000/before)
        mod.use_collapse_triangulate=True
        bpy.ops.object.modifier_apply(modifier=mod.name)
    for poly in o.data.polygons:poly.use_smooth=True
bpy.ops.object.select_all(action='DESELECT')
for o in meshes:o.select_set(True)
bpy.context.view_layer.objects.active=meshes[0]
bpy.ops.object.join()
g=bpy.context.object;g.name='Gibson_Stage'
# Mesh diffuse colors survive even when original absolute texture paths do not.
for m in g.data.materials:
    if m and m.use_nodes:
        shader=m.node_tree.nodes.get('Principled BSDF')
        if shader:
            m.diffuse_color=shader.inputs['Base Color'].default_value
            # Some supplied FBX embeds use invalid virtual filenames (*0/*2).
            # If the texture cannot be decoded, ship a warm Gibson finish.
            broken=[n for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image and not n.image.has_data]
            if broken:
                for node in broken:m.node_tree.nodes.remove(node)
                shader.inputs['Base Color'].default_value=(.38,.115,.028,1)
                shader.inputs['Metallic'].default_value=.25
                shader.inputs['Roughness'].default_value=.28
                m.diffuse_color=(.38,.115,.028,1)
bpy.ops.export_scene.fbx(filepath=OUT,use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_space_transform=True,use_mesh_modifiers=True,mesh_smooth_type='FACE',add_leaf_bones=False,bake_anim=False,path_mode='COPY',embed_textures=True)
blend=os.path.join(PROJECT,'ArtSource','Blender','Gibson_Stage.blend')
bpy.ops.wm.save_as_mainfile(filepath=blend)
after=sum(len(p.vertices)-2 for p in g.data.polygons)
manifest={'source':SOURCE,'sourceTriangles':before,'outputTriangles':after,'originalBounds':{'min':list(lo),'max':list(hi)},'normalizedMaxDimensionMeters':1.2,'output':OUT,'materials':[m.name for m in g.data.materials if m]}
with open(os.path.join(PROJECT,'ArtSource','Blender','Gibson_Stage_manifest.json'),'w',encoding='utf-8') as f:json.dump(manifest,f,indent=2)
print('GIBSON_STAGE_READY',json.dumps(manifest),flush=True)
