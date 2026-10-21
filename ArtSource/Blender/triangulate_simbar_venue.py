"""Repair the saved authoring scene for robust Unity FBX tessellation.

Run with Blender --background SimBarVenue.blend --python this_file.py.
No layout, axes, materials, lighting, or other project assets are changed.
"""
import bpy
import bmesh
import os
import json

project=os.path.abspath(os.path.join(os.path.dirname(__file__),'..','..'))
source=os.path.join(project,'ArtSource','Blender')
out=os.path.join(project,'Assets','Resources','SimBarArt','SimBarVenue.fbx')
counts={}
bpy.ops.object.select_all(action='DESELECT')
for obj in bpy.context.scene.objects:
    if obj.type!='MESH':continue
    bm=bmesh.new();bm.from_mesh(obj.data)
    oldverts=len(bm.verts);oldfaces=len(bm.faces)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001)
    bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=.0000001)
    bmesh.ops.triangulate(bm,faces=list(bm.faces),quad_method='BEAUTY',ngon_method='BEAUTY')
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(obj.data);bm.free();obj.data.update()
    nontri=sum(1 for p in obj.data.polygons if len(p.vertices)!=3)
    counts[obj.name]={'vertices':len(obj.data.vertices),'triangles':len(obj.data.polygons),'nonTriangleFaces':nontri}
    print('TRIANGULATED',obj.name,oldverts,oldfaces,counts[obj.name],flush=True)
    obj.select_set(True)
assert all(x['nonTriangleFaces']==0 for x in counts.values())
bpy.ops.export_scene.fbx(filepath=out,use_selection=True,object_types={'MESH'},
    axis_forward='-Z',axis_up='Y',apply_unit_scale=True,global_scale=1,
    bake_space_transform=True,use_mesh_modifiers=True,mesh_smooth_type='FACE',
    use_tspace=False,path_mode='AUTO',add_leaf_bones=False,bake_anim=False)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(source,'SimBarVenue.blend'))
manifest_path=os.path.join(source,'SimBarVenue_manifest.json')
with open(manifest_path,encoding='utf-8') as f:manifest=json.load(f)
manifest['meshes']=counts
manifest['tessellation']='Explicit triangles, welded duplicate vertices, degenerate edges dissolved, normals recalculated'
with open(manifest_path,'w',encoding='utf-8') as f:json.dump(manifest,f,indent=2)
print('SIMBAR_TRIANGLE_FBX_READY',out,sum(x['triangles'] for x in counts.values()),flush=True)
