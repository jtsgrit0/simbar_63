"""Assemble editable four-member SimBar band from the user's original FBX.

Output stays in ArtSource so Unity can continue its build without reimporting.
Use -- --inspect to inspect original rig conventions without writing assets.
"""
import bpy
import math
import os
import sys
import json
import bmesh
from mathutils import Vector, Euler, Matrix

ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'..','..'))
SOURCE=os.path.join(ROOT,'ArtSource','Blender')

def import_fbx(path):
    before=set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path,use_anim=True,automatic_bone_orientation=False)
    return [o for o in bpy.data.objects if o not in before]

if '--inspect' in sys.argv:
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    for filename in ['Punk.fbx','Casual2.fbx','Singing.fbx','Guitar Playing.fbx']:
        objs=import_fbx(os.path.join(ROOT,'Assets','Model',filename))
        print('BAND_INSPECT_FILE',filename,flush=True)
        for o in objs:
            if o.type=='ARMATURE':
                print('RIG',o.name,'scale',list(o.scale),'rotation',list(o.rotation_euler),'bones',[(b.name,tuple(round(v,3) for v in b.head_local),tuple(round(v,3) for v in b.tail_local)) for b in o.data.bones],flush=True)
                if o.animation_data:print('ACTION',o.animation_data.action,flush=True)
            elif o.type=='MESH':
                print('MESH',o.name,list(o.dimensions),[m.name for m in o.data.materials if m],flush=True)
        for o in objs:bpy.data.objects.remove(o,do_unlink=True)
    print('ALL_ACTIONS',[(a.name,a.frame_range[:]) for a in bpy.data.actions],flush=True)
    sys.exit(0)

# Start from the authored venue. This file is read-only; the assembled band is
# saved to a separate source scene with the venue retained as presentation.
bpy.ops.wm.open_mainfile(filepath=os.path.join(SOURCE,'SimBarVenue.blend'))
scene=bpy.context.scene
scene.frame_start=1;scene.frame_end=120;scene.render.fps=24
band=bpy.data.collections.new('SimBarBand_Editable');scene.collection.children.link(band)
references=bpy.data.collections.new('RetargetSources_ReadOnly');scene.collection.children.link(references)

def relocate(objects,collection):
    for o in objects:
        for c in list(o.users_collection):c.objects.unlink(o)
        collection.objects.link(o)

def make_mat(name,rgb,metallic=0,rough=.5):
    m=bpy.data.materials.new(name);m.diffuse_color=(*rgb,1);m.use_nodes=True
    bsdf=m.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value=(*rgb,1)
    bsdf.inputs['Metallic'].default_value=metallic;bsdf.inputs['Roughness'].default_value=rough
    return m

def color_slot(mesh,original_name,rgb,role):
    for slot in mesh.material_slots:
        if slot.material and slot.material.name.split('.')[0]==original_name:
            m=slot.material.copy();m.name=role+'_'+original_name
            m.diffuse_color=(*rgb,1);m.use_nodes=True
            shader=m.node_tree.nodes.get('Principled BSDF')
            shader.inputs['Base Color'].default_value=(*rgb,1)
            slot.material=m

def create_member(role,filename,p,height=1.87):
    objs=import_fbx(os.path.join(ROOT,'Assets','Model',filename))
    relocate(objs,band)
    rig=next(o for o in objs if o.type=='ARMATURE')
    rig.name=role+'_Rig';rig.data.name=role+'_Skeleton'
    meshes=[o for o in objs if o.type=='MESH']
    points=[o.matrix_world@Vector(v) for o in meshes for v in o.bound_box]
    scale=height/(max(p.z for p in points)-min(p.z for p in points))
    rig.scale=(scale,scale,scale);rig.rotation_euler=(0,0,0)
    rig.location=(p[0],p[2],p[1])
    for o in meshes:o.name=role+'_'+o.name
    rig['Role']=role;rig['OriginalFBX']=filename;rig['HeightMeters']=height
    print('BAND_MEMBER',role,scale,height,flush=True)
    return rig,meshes

def reference(filename):
    objs=import_fbx(os.path.join(ROOT,'Assets','Model',filename))
    relocate(objs,references)
    rig=next(o for o in objs if o.type=='ARMATURE')
    for o in objs:o.hide_render=True
    return rig,objs

singref,sing_objects=reference('Singing.fbx')
guitref,guit_objects=reference('Guitar Playing.fbx')
singer,sing_mesh=create_member('01_PinkVocal','Punk.fbx',(5.5,.62,2.7),1.89)
guitar,guit_mesh=create_member('02_Guitar','Casual2.fbx',(3,.62,3.8),1.85)
bass,bass_mesh=create_member('03_Bass','Punk.fbx',(8,.62,4.6),1.86)
drummer,drum_mesh=create_member('04_Drums','Punk.fbx',(6,.62,6.2),1.84)
for o in sing_mesh:
    if 'Head' in o.name:
        color_slot(o,'Red',(1,.025,.28),'VocalHair')
        color_slot(o,'Red_Dark',(.63,.008,.15),'VocalHair')
for o in bass_mesh:
    if 'Head' in o.name:
        color_slot(o,'Red',(.02,.58,.64),'BassHair')
        color_slot(o,'Red_Dark',(.012,.2,.3),'BassHair')
    if 'Body' in o.name:color_slot(o,'White',(.022,.026,.04),'BassShirt')
for o in drum_mesh:
    if 'Head' in o.name:
        color_slot(o,'Red',(.21,.035,.37),'DrummerHair')
        color_slot(o,'Red_Dark',(.08,.015,.15),'DrummerHair')
    if 'Body' in o.name:color_slot(o,'White',(.028,.035,.045),'DrummerShirt')
for o in guit_mesh:
    if 'Body' in o.name:color_slot(o,'LightBrown',(.035,.12,.23),'GuitarJacket')

def reference_bone(name,source):
    if name in source.pose.bones:return source.pose.bones[name]
    for finger in ['Index','Middle','Ring','Pinky','Thumb']:
        if name.startswith(finger):
            pieces=name.split('.')
            number=int(pieces[0][-1])
            candidate=finger+str(max(1,number-1))+'.'+pieces[-1]
            return source.pose.bones.get(candidate)
    return None

def retarget_pose(target,source,frame,sourceframe):
    scene.frame_set(int(sourceframe),subframe=sourceframe-int(sourceframe))
    rotations={}
    for bone in target.pose.bones:
        src=reference_bone(bone.name,source)
        if not src:continue
        # Rest-roll conventions differ between the original character and its
        # Mixamo motion FBX. Transfer the armature-space deformation, then
        # recover target-local rotations rather than copying local quaternions.
        delta=src.matrix.to_quaternion()@src.bone.matrix_local.to_quaternion().inverted()
        rotations[bone.name]=delta@bone.bone.matrix_local.to_quaternion()
    for bone in target.pose.bones:
        if bone.name not in rotations:continue
        rest=bone.bone.matrix_local.to_quaternion()
        if bone.parent and bone.parent.name in rotations:
            parent_rest=bone.parent.bone.matrix_local.to_quaternion()
            local=rest.inverted()@parent_rest@rotations[bone.parent.name].inverted()@rotations[bone.name]
        else:local=rest.inverted()@rotations[bone.name]
        bone.rotation_mode='QUATERNION'
        bone.rotation_quaternion=local
        bone.location=(0,0,0);bone.scale=(1,1,1)
        bone.keyframe_insert('rotation_quaternion',frame=frame,group=bone.name)

# Retarget local joint rotations onto the original character skeleton. Source
# root motion is intentionally excluded so every musician stays at their mark.
for frame in list(range(1,121,3))+[120]:
    retarget_pose(singer,singref,frame,1+(frame-1)*419/119)
    retarget_pose(guitar,guitref,frame,1+(frame-1)*187/119)
    retarget_pose(bass,guitref,frame,1+((frame+37)%120)*187/119)
for rig in [singer,guitar,bass]:
    rig.animation_data.action.name=rig.name+'_RetargetedPerformance'
    rig['AnimationSource']='Singing.fbx' if rig==singer else 'Guitar Playing.fbx'

def aim_bone(rig,name,direction):
    bone=rig.pose.bones[name]
    current=(bone.tail-bone.head).normalized();desired=Vector(direction).normalized()
    turn=current.rotation_difference(desired).to_matrix().to_4x4()
    head=bone.head.copy()
    bone.matrix=Matrix.Translation(head)@turn@Matrix.Translation(-head)@bone.matrix
    bpy.context.view_layer.update()

def reach_hand(rig,side,target):
    upper=rig.pose.bones['UpperArm.'+side];lower=rig.pose.bones['LowerArm.'+side]
    s=upper.head.copy();t=Vector(target);d=t-s;dist=min(d.length,upper.length+lower.length-.008)
    direction=d.normalized();preferred=Vector((1 if side=='L' else -1,0,-1))
    bend=(preferred-direction*preferred.dot(direction)).normalized()
    along=(upper.length**2+dist**2-lower.length**2)/(2*dist)
    offset=math.sqrt(max(0,upper.length**2-along**2))
    elbow=s+direction*along+bend*offset
    aim_bone(rig,'UpperArm.'+side,elbow-s)
    aim_bone(rig,'LowerArm.'+side,t-lower.head)

# Seated drum pose with two-bone solved arm reaches, bent knees and a small
# alternate stroke. It is a stored action rather than an uneditable mesh bake.
scene.frame_set(35)
for frame in [1,30,60,90,120]:
    for bone in drummer.pose.bones:
        bone.rotation_mode='QUATERNION';bone.rotation_quaternion=(1,0,0,0);bone.location=(0,0,0)
    hips=drummer.pose.bones['Hips']
    # The drummer sits on a compact 0.43 m rear podium so their face clears
    # the tall authored rack toms. Pose remains seated relative to that riser.
    hips.location=hips.bone.matrix_local.to_3x3().inverted()@Vector((0,0,0))
    bpy.context.view_layer.update()
    aim_bone(drummer,'UpperLeg.L',(.12,-.75,-.14))
    aim_bone(drummer,'UpperLeg.R',(-.12,-.75,-.14))
    aim_bone(drummer,'LowerLeg.L',(0,-.20,-.62))
    aim_bone(drummer,'LowerLeg.R',(0,-.20,-.62))
    stroke=.12 if frame in [30,90] else 0
    reach_hand(drummer,'L',(.39,-.31,1.18+stroke))
    reach_hand(drummer,'R',(-.39,-.34,1.27-stroke))
    for bone in drummer.pose.bones:
        bone.keyframe_insert('rotation_quaternion',frame=frame,group=bone.name)
        if bone.name=='Hips':bone.keyframe_insert('location',frame=frame,group=bone.name)
drummer.animation_data.action.name='04_Drums_SeatedAlternatingStrokes'

# Retargeted motion FBX uses different hip proportions. Ground the evaluated
# shoes at each sampled key instead of carrying over the source root height.
for frame in list(range(1,121,3))+[120]:
    scene.frame_set(frame);bpy.context.view_layer.update()
    for rig,meshes in [(singer,sing_mesh),(guitar,guit_mesh),(bass,bass_mesh)]:
        feet=next(o for o in meshes if 'Feet' in o.name)
        evaluated=feet.evaluated_get(bpy.context.evaluated_depsgraph_get())
        mesh=evaluated.to_mesh()
        floor=min((evaluated.matrix_world@v.co).z for v in mesh.vertices)
        evaluated.to_mesh_clear()
        rig.location.z+=.625-floor
        rig.keyframe_insert('location',frame=frame,group='PlantedFeet')

for o in sing_objects+guit_objects:bpy.data.objects.remove(o,do_unlink=True)
bpy.data.collections.remove(references)
scene.frame_set(35);bpy.context.view_layer.update()

chrome=make_mat('Band_MicrophoneChrome',(.57,.65,.7),.8,.25)
black=make_mat('Band_MicrophoneBlack',(.016,.019,.026),.3,.4)
strapmat=make_mat('Band_LeatherStrap',(.075,.037,.018),0,.56)
stickmat=make_mat('Band_DrumstickMaple',(.8,.5,.2),0,.55)
fretmat=make_mat('Band_GuitarNickel',(.63,.68,.69),.82,.23)
bucklemat=make_mat('Band_StrapBuckle',(.7,.43,.13),.7,.32)

bpy.ops.mesh.primitive_cube_add(size=1,location=(6,6.05,.835))
podium=bpy.context.object;podium.name='Drummer_CompactPodium';podium.dimensions=(1.6,.9,.43)
bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
podium.data.materials.append(black);relocate([podium],band)
bevel=podium.modifiers.new('Podium rounded edge','BEVEL');bevel.width=.035;bevel.segments=2
bpy.context.view_layer.objects.active=podium;bpy.ops.object.modifier_apply(modifier=bevel.name)
bpy.ops.mesh.primitive_cylinder_add(vertices=24,radius=.29,depth=.12,location=(6,6.2,1.43))
throne=bpy.context.object;throne.name='Drummer_RaisedThrone';throne.data.materials.append(black);relocate([throne],band)

def bone_parent(o,rig,bone):
    bpy.context.view_layer.update()
    world=o.matrix_world.copy();o.parent=rig;o.parent_type='BONE';o.parent_bone=bone;o.matrix_world=world

def rod(name,a,b,r,mat,parent=None,bone=None):
    a=Vector(a);b=Vector(b);d=b-a
    bpy.ops.mesh.primitive_cylinder_add(vertices=12,radius=r,depth=d.length,location=(a+b)/2)
    o=bpy.context.object;o.name=name;o.rotation_euler=d.to_track_quat('Z','Y').to_euler();o.data.materials.append(mat)
    relocate([o],band)
    if parent:bone_parent(o,parent,bone)
    return o

def hand_world(rig,side):return rig.matrix_world@rig.pose.bones['Hand.'+side].head

def ribbon(name,points,width,mat,rig):
    """Solid leather strip following a route over the shoulder and back."""
    vertices=[];faces=[];previous=None
    points=[Vector(p) for p in points]
    for i,p in enumerate(points):
        tangent=(points[min(i+1,len(points)-1)]-points[max(i-1,0)]).normalized()
        across=tangent.cross(Vector((0,1,0)))
        if across.length<.15:across=Vector((1,0,0))
        across.normalize()
        if previous is not None and across.dot(previous)<0:across=-across
        previous=across
        normal=tangent.cross(across).normalized()*.004
        across*=width/2
        vertices.extend([tuple(p-across-normal),tuple(p+across-normal),tuple(p+across+normal),tuple(p-across+normal)])
        if i:
            a=(i-1)*4;b=i*4
            faces.extend([(a,a+1,b+1,b),(a+1,a+2,b+2,b+1),(a+2,a+3,b+3,b+2),(a+3,a,b,b+3)])
    faces.extend([(3,2,1,0),tuple(range((len(points)-1)*4,len(points)*4))])
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);band.objects.link(obj);obj.data.materials.append(mat)
    bone_parent(obj,rig,'Chest')
    return obj

def instrument_detail(name,obj,a,b,r,mat):
    detail=rod(name,obj.matrix_world@Vector(a),obj.matrix_world@Vector(b),r,mat)
    bpy.context.view_layer.update()
    world=detail.matrix_world.copy();detail.parent=obj;detail.matrix_world=world
    return detail

def guitar_prop(rig,name,scale=1):
    with bpy.data.libraries.load(os.path.join(SOURCE,'Gibson_Stage.blend'),link=False) as (data_from,data_to):
        data_to.objects=['Gibson_Stage']
    obj=data_to.objects[0];band.objects.link(obj);obj.name=name
    # Body sits against the upper waist. Blender +X is the neck direction;
    # negative Y is the musician's front, and -Y rotation raises that neck.
    obj.location=rig.matrix_world@Vector((.055,-.19,1.17))
    obj.rotation_euler=(0,math.radians(-17),0)
    obj.scale=(scale,scale,scale)
    bpy.context.view_layer.update();bone_parent(obj,rig,'Chest')
    # Neck-joint button -> anatomical left shoulder (+X in this skeleton)
    # -> over the shoulder -> back -> bottom button. Both ends really meet
    # the instrument, unlike a decorative line floating beside the torso.
    chest=rig.pose.bones['Chest']
    deformation=chest.matrix.to_3x3()@chest.bone.matrix_local.to_3x3().inverted()
    front=deformation@Vector((0,-1,0));up=deformation@Vector((0,0,1))
    shoulder=rig.pose.bones['Shoulder.L'].head
    route=[obj.matrix_world@Vector((-.10,-.055,.145)),
           rig.matrix_world@(shoulder+front*.095),
           rig.matrix_world@(shoulder+front*.045+up*.07),
           rig.matrix_world@(shoulder-front*.045+up*.075),
           rig.matrix_world@(shoulder-front*.095),
           rig.matrix_world@(rig.pose.bones['Torso'].head-front*.105),
           obj.matrix_world@Vector((-.555,.025,-.035))]
    ribbon(name+'_ShoulderStrap',route,.057,strapmat,rig)
    for i,p in enumerate([route[0],route[-1]]):
        rod(name+'_StrapButton_'+str(i),p-Vector((0,.012,0)),p+Vector((0,.012,0)),.020,bucklemat,rig,'Chest')
    # Reintroduce readable strings/frets over the reduced one-material original
    # while preserving its recognizable Gibson body geometry.
    for index in range(6):
        z=-.017+index*.0068
        instrument_detail(name+'_String_'+str(index),obj,(-.39,-.10,z),(.485,.026,z),.0009,fretmat)
    for index,x in enumerate([-.02,.026,.070,.111,.149,.185,.218,.248,.276,.302,.326,.348,.369,.388]):
        y=-.035+x*.082
        instrument_detail(name+'_Fret_'+str(index),obj,(x,y,-.028),(x,y,.028),.0015,fretmat)
    for index,x in enumerate([-.275,-.175]):
        instrument_detail(name+'_Pickup_'+str(index),obj,(x,-.075,-.039),(x,-.075,.039),.012,black)
    rig['Instrument']='Original gibson.fbx, reduced stage mesh'
    rig['GuitarPose']='Shoulder strap, left fretting hand, right rhythmic strum'
    return obj

electric=guitar_prop(guitar,'Gibson_ElectricGuitar',.88)
bassprop=guitar_prop(bass,'Gibson_BassVariant',.95)

def instrument_hand_pose(rig,obj,frame,phase=0):
    scene.frame_set(frame);bpy.context.view_layer.update()
    # Preserve the retargeted lower body, torso sway and finger articulation.
    # Solve both arms against the actual moving guitar, rather than borrowing
    # wrist positions from a motion captured with a different instrument.
    for side in ['L','R']:
        shoulder=rig.pose.bones['Shoulder.'+side]
        shoulder.rotation_quaternion=(1,0,0,0)
    bpy.context.view_layer.update()
    strum=.029*math.sin((frame+phase)*2*math.pi/12)
    fingering=.035*math.sin((frame+phase)*2*math.pi/60)
    grip=[('L',(.19+fingering,-.045,-.13),(0,0,1)),
          ('R',(-.245,-.12,.073+strum),(.12,-.035,-1))]
    errors=[]
    to_rig=rig.matrix_world.inverted()@obj.matrix_world
    for side,local_position,local_direction in grip:
        target=to_rig@Vector(local_position)
        reach_hand(rig,side,target)
        hand=rig.pose.bones['Hand.'+side]
        axis_x=(to_rig.to_3x3()@Vector((1 if side=='L' else -1,0,0))).normalized()
        axis_y=(to_rig.to_3x3()@Vector(local_direction)).normalized()
        axis_z=axis_x.cross(axis_y).normalized()
        axis_x=axis_y.cross(axis_z).normalized()
        matrix=Matrix((axis_x,axis_y,axis_z)).transposed().to_4x4()
        matrix.translation=hand.head
        hand.matrix=matrix;bpy.context.view_layer.update()
        # Curl actual finger chains around the fretboard / pick. The wrist is
        # below the neck, so the left palm crosses the strings rather than
        # pointing along the neck with splayed fingers.
        for finger in ['Index','Middle','Ring','Pinky']:
            for segment in [2,3,4]:
                name=finger+str(segment)+'.'+side
                if name not in rig.pose.bones:continue
                directions=([(0,.75,.35),(0,.18,-1),(0,-.8,-.25)] if side=='L'
                            else [(0,.15,-1),(0,.85,-.35),(0,.8,.3)])
                direction=to_rig.to_3x3()@Vector(directions[segment-2])
                aim_bone(rig,name,direction)
                rig.pose.bones[name].keyframe_insert('rotation_quaternion',frame=frame,group=name)
        for segment in [2,3]:
            name='Thumb'+str(segment)+'.'+side
            if name in rig.pose.bones:
                direction=to_rig.to_3x3()@Vector((-.5,.8,.1) if side=='L' else (.5,-.4,-.2))
                aim_bone(rig,name,direction)
                rig.pose.bones[name].keyframe_insert('rotation_quaternion',frame=frame,group=name)
        errors.append((rig.pose.bones['Hand.'+side].head-target).length)
        for name in ['Shoulder.','UpperArm.','LowerArm.','Hand.']:
            bone=rig.pose.bones[name+side]
            bone.keyframe_insert('rotation_quaternion',frame=frame,group=bone.name)
    return max(errors)

worst_error=0
for frame in range(1,121):
    worst_error=max(worst_error,instrument_hand_pose(guitar,electric,frame),instrument_hand_pose(bass,bassprop,frame,4))
print('GUITAR_WRIST_MAX_TARGET_ERROR_METERS',round(worst_error,5),flush=True)
scene.frame_set(35);bpy.context.view_layer.update()
# Put the handheld vocal microphone in whichever animated hand is nearest the
# face at the saved presentation frame; it remains bone-parented when played.
head=singer.matrix_world@singer.pose.bones['Head'].head
side=min(['L','R'],key=lambda s:(hand_world(singer,s)-head).length)
hand=hand_world(singer,side)
direction=(head-hand).normalized()
rod('Vocal_HandheldMic',hand-direction*.04,hand+direction*.15,.027,black,singer,'Hand.'+side)
rod('Vocal_MicGrille',hand+direction*.13,hand+direction*.21,.046,chrome,singer,'Hand.'+side)
for side in ['L','R']:
    hand=hand_world(drummer,side)
    target=Vector((5.7 if side=='L' else 6.4,4.95,1.8))
    direction=(target-hand).normalized()
    rod('Drummer_MapleStick_'+side,hand-direction*.07,hand+direction*.34,.010,stickmat,drummer,'Hand.'+side)

# All delivered band meshes are explicit triangles. Armature modifiers stay
# intact in the editable source and bake correctly into the FBX bind hierarchy.
for obj in band.objects:
    if obj.type!='MESH':continue
    bm=bmesh.new();bm.from_mesh(obj.data)
    bmesh.ops.triangulate(bm,faces=list(bm.faces),quad_method='BEAUTY',ngon_method='BEAUTY')
    bm.to_mesh(obj.data);bm.free();obj.data.update()

scene.frame_set(35);scene.render.filepath=os.path.join(SOURCE,'SimBarBand_preview.png')
scene.render.resolution_x=1400;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.cycles.samples=24
cam=scene.camera
cam.location=(13.8,-9.2,10.4)
target=Vector((5.5,4.6,1.8));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
cam.data.type='ORTHO';cam.data.ortho_scale=11.7
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCE,'SimBarBand.blend'))
bpy.ops.object.select_all(action='DESELECT')
for o in band.objects:
    if o.type in ['MESH','ARMATURE']:o.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(SOURCE,'SimBarBand.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},
    axis_forward='-Z',axis_up='Y',apply_unit_scale=True,global_scale=1,bake_space_transform=False,
    use_mesh_modifiers=True,mesh_smooth_type='FACE',add_leaf_bones=False,bake_anim=True,
    bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_step=2,bake_anim_simplify_factor=.5)
print('SIMBAR_BAND_READY',[(o.name,o.type) for o in band.objects],flush=True)
bpy.ops.render.render(write_still=True)
print('SIMBAR_BAND_PREVIEW_READY',scene.render.filepath,flush=True)
# A second view makes fretting, strumming, shoulder contact and both strap
# attachment points reviewable without searching around the .blend scene.
cam.location=(5.2,-.3,3.7)
target=Vector((3,3.55,1.65));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
cam.data.ortho_scale=2.7
equipment=bpy.data.objects.get('SimBar_MicrophonesAndInstruments')
if equipment:equipment.hide_render=True
scene.render.resolution_x=1100;scene.render.resolution_y=1100
scene.render.filepath=os.path.join(SOURCE,'SimBarBand_GuitarClose_preview.png')
bpy.ops.render.render(write_still=True)
print('SIMBAR_GUITAR_CLOSE_PREVIEW_READY',scene.render.filepath,flush=True)

