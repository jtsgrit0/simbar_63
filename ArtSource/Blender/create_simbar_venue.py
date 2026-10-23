"""Author the SimBar concert pub as editable, bevelled Blender meshes.

Run: blender --background --python ArtSource/Blender/create_simbar_venue.py
All public helpers accept Unity world coordinates (X, height Y, depth Z).
The Blender working scene is Z-up, with Unity +Z mapped to Blender +Y.
FBX is exported -Z forward / Y up with baked axis conversion.
"""
import bpy
import bmesh
import math
import os
import json
import random
from mathutils import Vector

random.seed(63)
PROJECT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
SOURCE = os.path.join(PROJECT, 'ArtSource', 'Blender')
OUTPUT = os.path.join(PROJECT, 'Assets', 'Resources', 'SimBarArt')
os.makedirs(SOURCE, exist_ok=True)
os.makedirs(OUTPUT, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for data in list(bpy.data.materials):
    bpy.data.materials.remove(data)

materials = {}
manifest = {'generator': 'create_simbar_venue.py', 'blender': bpy.app.version_string,
            'units': 'meters', 'unityBounds': {'x': [-10.5,10.5], 'y': [-0.2,6.6], 'z': [-7.75,7.75]},
            'layout': {'bar': {'center':[-6,0,-1.5], 'counterTop':1.55},
                       'stage': {'center':[5.4,0.62,4.4], 'size':[7.8,0.62,4.2]},
                       'drums':[6,0.62,5.2], 'vocal':[5.5,0.62,2.7],
                       'guitarLeft':[3,0.62,3.8], 'guitarRight':[8,0.62,3.8]},
            'staffAisle': {'workSurfaceOffsetZ':0.25, 'z':[-0.88,-0.25],
                           'width':0.63, 'centerZ':-0.565, 'radius':0.24,
                           'walkX':[-9.0,-2.8], 'sourceMesh':'SimBar_BarCounter'},
            'materials': {}}

def material(name, color, roughness=0.55, metallic=0, emission=0):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color,1)
    m.use_nodes = True
    bsdf=m.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value=(*color,1)
    bsdf.inputs['Roughness'].default_value=roughness
    bsdf.inputs['Metallic'].default_value=metallic
    if emission:
        bsdf.inputs['Emission Color'].default_value=(*color,1)
        bsdf.inputs['Emission Strength'].default_value=emission
    materials[name]=m
    manifest['materials'][name]={'color':list(color), 'roughness':roughness, 'metallic':metallic, 'emission':emission}
    return m

for args in [
    ('CreamTile',(0.78,0.67,0.52),.66), ('IvoryTile',(0.91,0.82,0.65),.64),
    ('FloorGrout',(0.42,.34,.29),.8), ('WallMortar',(.39,.18,.12),.9),
    ('BrickTerracotta',(.55,.18,.10),.88), ('BrickWarm',(.67,.23,.13),.86),
    ('BrickBurnt',(.43,.11,.065),.9), ('BrickPeach',(.73,.30,.17),.9),
    ('DarkWalnut',(.16,.055,.025),.35), ('WarmOak',(.43,.16,.055),.32),
    ('HoneyWood',(.59,.28,.10),.36), ('WoodHighlight',(.72,.36,.14),.4),
    ('UpholsteryRed',(.38,.045,.055),.7), ('UpholsteryHighlight',(.54,.075,.08),.7),
    ('LeatherBlack',(.021,.026,.032),.7), ('StageBlack',(.026,.032,.052),.65),
    ('MetalBlack',(.035,.041,.052),.4,.68), ('Chrome',(.55,.66,.68),.2,.83),
    ('Brass',(.76,.48,.13),.28,.72), ('BeerGold',(.95,.46,.025),.24),
    ('BeerFoam',(.99,.9,.65),.75), ('GlassPale',(.72,.88,.87),.17,.25),
    ('BottleGreen',(.055,.3,.11),.21), ('BottleTeal',(.025,.5,.43),.21),
    ('BottleAmber',(.48,.14,.025),.22), ('BottleBlue',(.035,.27,.55),.2),
    ('BottleClear',(.57,.77,.74),.2), ('LabelCream',(.97,.84,.55),.8),
    ('LabelRed',(.68,.055,.04),.8), ('LabelNavy',(.045,.08,.14),.7),
    ('Pink',(.95,.055,.34),.35), ('DrumRed',(.58,.025,.1),.3,.35),
    ('DrumHead',(.91,.88,.78),.72), ('Foliage',(.13,.4,.055),.75),
    ('FoliageLight',(.3,.57,.075),.72), ('Ceramic',(.78,.6,.38),.7),
    ('PosterPaper',(.95,.79,.57),.7), ('PosterPurple',(.29,.065,.33),.7),
    ('PosterOrange',(.94,.28,.05),.7), ('PosterTeal',(.035,.62,.54),.7),
    ('NeonCyan',(.025,.78,1),.24,0,3), ('NeonPink',(1,.035,.51),.24,0,3),
    ('NeonGold',(1,.62,.08),.24,0,2), ('WarmBulb',(1,.7,.27),.25,0,2),
]: material(*args)

groups={}
active_group='Venue'
def register(obj,name,mat):
    obj.name=name
    if mat: obj.data.materials.append(materials[mat] if isinstance(mat,str) else mat)
    groups.setdefault(active_group,[]).append(obj)
    return obj

def pos(p): return (p[0],p[2],p[1])
def mesh_object(name,vertices,faces,mat,location=(0,0,0),smooth=False):
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(vertices,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj)
    obj.location=location
    register(obj,name,mat)
    if smooth:
        for poly in mesh.polygons:poly.use_smooth=True
    return obj

def box(name,p,size,mat,bevel=.025):
    x,y,z=size[0]/2,size[2]/2,size[1]/2
    vertices=[(-x,-y,-z),(x,-y,-z),(x,y,-z),(-x,y,-z),(-x,-y,z),(x,-y,z),(x,y,z),(-x,y,z)]
    faces=[(3,2,1,0),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7)]
    return mesh_object(name,vertices,faces,mat,pos(p))

def sphere(name,p,size,mat,segments=12):
    vertices=[];faces=[];rings=8
    for j in range(rings+1):
        a=math.pi*j/rings
        for i in range(segments):
            b=2*math.pi*i/segments
            vertices.append((math.sin(a)*math.cos(b)*size[0],math.sin(a)*math.sin(b)*size[2],math.cos(a)*size[1]))
    for j in range(rings):
        for i in range(segments):
            ni=(i+1)%segments
            faces.append((j*segments+i,j*segments+ni,(j+1)*segments+ni,(j+1)*segments+i))
    return mesh_object(name,vertices,faces,mat,pos(p),True)

def cyl(name,p,r,depth,mat,vertices=16,axis=None,r2=None):
    count=vertices;verts=[];faces=[]
    for z,radius in [(-depth/2,r),(depth/2,r if r2 is None else r2)]:
        for i in range(count):
            a=2*math.pi*i/count;verts.append((math.cos(a)*radius,math.sin(a)*radius,z))
    faces.append(tuple(reversed(range(count))));faces.append(tuple(range(count,count*2)))
    for i in range(count):
        ni=(i+1)%count;faces.append((i,ni,ni+count,i+count))
    o=mesh_object(name,verts,faces,mat,pos(p))
    if axis:
        o.rotation_euler=Vector((0,0,1)).rotation_difference(Vector(pos(axis)).normalized()).to_euler()
    return o

def rod(name,a,b,r,mat):
    direction=Vector(b)-Vector(a)
    return cyl(name,tuple((Vector(a)+Vector(b))*.5),r,direction.length,mat,12,tuple(direction))

def ring(name,p,major,minor,mat,axis=None):
    vertices=[];faces=[]
    for i in range(24):
        a=2*math.pi*i/24
        for j in range(6):
            b=2*math.pi*j/6
            vertices.append(((major+minor*math.cos(b))*math.cos(a),(major+minor*math.cos(b))*math.sin(a),minor*math.sin(b)))
    for i in range(24):
        for j in range(6):faces.append((i*6+j,((i+1)%24)*6+j,((i+1)%24)*6+(j+1)%6,i*6+(j+1)%6))
    o=mesh_object(name,vertices,faces,mat,pos(p),True)
    if axis:o.rotation_euler=Vector((0,0,1)).rotation_difference(Vector(pos(axis)).normalized()).to_euler()
    return o

def text(name,body,p,size,mat,extrude=.007):
    curve=bpy.data.curves.new(name,'FONT');curve.body=body;curve.align_x='CENTER';curve.align_y='CENTER';curve.size=size;curve.extrude=extrude;curve.bevel_depth=.004;curve.bevel_resolution=2
    o=bpy.data.objects.new(name,curve);bpy.context.collection.objects.link(o);o.location=pos(p)
    # Face the room/front (-Unity Z), with glyph-up along Blender +Z.
    o.rotation_euler=(math.pi/2,0,0)
    register(o,name,mat)
    bpy.context.view_layer.objects.active=o;o.select_set(True)
    bpy.ops.object.convert(target='MESH');o.select_set(False)
    return o

def bottle(x,y,z,kind,scale=1):
    r=.095*scale
    cyl('Bottle body',(x,y+.24*scale,z),r,.45*scale,kind,12)
    cyl('Bottle shoulder',(x,y+.49*scale,z),r,.1*scale,kind,12,r2=.045*scale)
    cyl('Bottle neck',(x,y+.62*scale,z),.044*scale,.2*scale,kind,12)
    cyl('Bottle cap',(x,y+.73*scale,z),.048*scale,.047*scale,'Brass',12)
    box('Bottle paper label',(x,y+.25*scale,z-.091*scale),(.138*scale,.17*scale,.009),random.choice(['LabelCream','LabelRed','LabelNavy']),.005)
    box('Bottle label detail',(x,y+.25*scale,z-.098*scale),(.08*scale,.027*scale,.005),'Brass',.002)

def pint(x,y,z):
    cyl('Pint glass',(x,y+.20,z),.11,.4,'GlassPale',16,r2=.14)
    cyl('Golden beer',(x,y+.23,z),.105,.32,'BeerGold',16,r2=.128)
    cyl('Creamy beer head',(x,y+.404,z),.13,.06,'BeerFoam',16)

def cocktail(x,y,z):
    cyl('Cocktail foot',(x,y+.014,z),.12,.025,'GlassPale')
    cyl('Cocktail stem',(x,y+.13,z),.018,.24,'GlassPale')
    cyl('Cocktail bowl',(x,y+.33,z),.045,.2,'GlassPale',16,r2=.17)
    cyl('Cocktail drink',(x,y+.365,z),.11,.08,random.choice(['Pink','BottleTeal','BeerGold']),16,r2=.155)
    sphere('Cocktail garnish',(x+.14,y+.44,z),(.07,.065,.025),'FoliageLight')

active_group='Floor'
print('SIMBAR_BUILD floor',flush=True)
box('Foundation',(0,-.14,0),(21,.28,15.5),'DarkWalnut',.07)
box('Grout surface',(0,.005,0),(20.9,.04,15.4),'FloorGrout',0)
for ix in range(21):
    for iz in range(16):
        x=-10+ix;z=-7.25+iz*.96
        box('Cream stone tile',(x,.034,z),(.984,.055,.946),'IvoryTile' if (ix+iz)%3 else 'CreamTile',.009)
box('Front brass floor edge',(0,.05,-7.72),(20.9,.07,.055),'Brass',.012)

active_group='BrickWalls'
print('SIMBAR_BUILD brick walls',flush=True)
box('Back wall backing',(0,2.5,7.6),(21,5,.18),'WallMortar',.015)
box('Left wall backing',(-10.4,2.5,.05),(.18,5,15.25),'WallMortar',.015)
brickmats=['BrickTerracotta','BrickWarm','BrickBurnt','BrickPeach']
for row in range(15):
    h=.18+row*.326
    for col in range(25):
        x=-10.1+col*.85+(row%2)*.425
        if x<10.32:box('Back wall terracotta brick',(x,h,7.47),(.814,.294,.13),random.choice(brickmats),.016)
    for col in range(18):
        z=-7.35+col*.85+(row%2)*.425
        if z<7.4:box('Left wall terracotta brick',(-10.27,h,z),(.13,.294,.814),random.choice(brickmats),.016)
box('Back wall crown',(0,5.03,7.58),(21.1,.15,.3),'DarkWalnut',.025)
box('Left wall crown',(-10.39,5.03,0),(.3,.15,15.5),'DarkWalnut',.025)
box('Back wall skirting',(0,.16,7.37),(21,.25,.18),'DarkWalnut',.02)
box('Left wall skirting',(-10.18,.16,0),(.18,.25,15.5),'DarkWalnut',.02)

active_group='BackBar'
print('SIMBAR_BUILD illuminated bottle shelves',flush=True)
# Shelving backboard lies behind bottles, toward +Z. Open front faces camera.
box('Open bottle cabinet back',(-6.05,2.45,1.13),(7.7,3.72,.2),'DarkWalnut',.03)
box('Cabinet left frame',(-9.92,2.45,.78),(.15,3.9,.82),'WarmOak',.025)
box('Cabinet right frame',(-2.18,2.45,.78),(.15,3.9,.82),'WarmOak',.025)
box('Cabinet crown',(-6.05,4.45,.78),(7.92,.22,.86),'DarkWalnut',.03)
for level in range(4):
    h=1.03+level*.82
    box('Bottle shelf',(-6.05,h,.72),(7.65,.12,.72),'WarmOak',.018)
    box('Cyan illuminated shelf lip',(-6.05,h-.008,.325),(7.58,.035,.04),'NeonCyan',.009)
    for i in range(19):
        bottle(-9.6+i*.39,h+.08,.69,random.choice(['BottleGreen','BottleTeal','BottleAmber','BottleBlue','BottleClear']),random.uniform(.74,1.0))
for x in [-7.48,-4.71]:box('Cabinet upright',(x,2.55,.82),(.13,3.65,.65),'DarkWalnut',.01)
box('Back bar base',(-6.05,.56,.9),(7.7,1.06,.8),'DarkWalnut',.04)

active_group='BarCounter'
print('SIMBAR_BUILD detailed bar counter',flush=True)
box('Bar panel base',(-6.02,.72,-1.56),(7.5,1.35,1.06),'WarmOak',.035)
for x in [-8.95,-7.45,-5.95,-4.45,-2.95]:
    box('Raised panel border',(x,.77,-2.107),(1.35,.95,.055),'DarkWalnut',.025)
    box('Inset oak panel',(x,.77,-2.148),(1.20,.80,.036),'HoneyWood',.025)
    box('Panel face insert',(x,.77,-2.172),(1.03,.64,.025),'WarmOak',.015)
box('Counter bottom plinth',(-6.02,.13,-1.6),(7.7,.18,1.23),'DarkWalnut',.025)
box('Thick walnut counter',(-6.02,1.48,-1.56),(7.95,.22,1.36),'DarkWalnut',.055)
for j in range(5):
    box('Counter wood plank',(-6.02,1.599,-2.04+j*.245),(7.87,.035,.23),'WarmOak' if j%2 else 'HoneyWood',.009)
box('Counter front bullnose',(-6.02,1.56,-2.25),(8.0,.12,.14),'DarkWalnut',.055)
rod('Brass bar foot rail',(-9.7,.31,-2.58),(-2.3,.31,-2.58),.045,'Brass')
for x in [-9.5,-7.6,-5.6,-3.4]:
    rod('Footrail bracket',(x,.16,-2.12),(x,.31,-2.58),.03,'Brass')
# Move the worktop and its sink +0.25 Unity Z, leaving a 0.63 m staff aisle
# between the main counter's back edge (-0.88) and this top's front (-0.25).
# Absolute coordinates match open_staff_aisle.py's idempotent saved-scene patch.
box('Bartender work surface',(-6.1,.99,.10),(7.25,.12,.7),'MetalBlack',.025)
box('Sink metal surround',(-8.1,1.065,.09),(1.03,.03,.48),'Chrome',.045)
box('Sink dark basin',(-8.1,1.078,.08),(.77,.03,.33),'StageBlack',.045)
rod('Sink faucet riser',(-8.1,1.08,.33),(-8.1,1.53,.33),.028,'Chrome')
rod('Sink faucet spout',(-8.1,1.53,.33),(-8.1,1.53,.06),.028,'Chrome')
# Taps and beer tower remain on the original customer counter.
for x in [-5.9,-4.95]:
    cyl('Beer tower pedestal',(x,1.65,-1.13),.15,.10,'Chrome')
    rod('Beer tower vertical',(x,1.68,-1.13),(x,2.25,-1.13),.075,'Chrome')
    rod('Beer tower top',(x-.3,2.25,-1.13),(x+.3,2.25,-1.13),.07,'Chrome')
    for dx in [-.22,0,.22]:
        rod('Beer tap nozzle',(x+dx,2.25,-1.13),(x+dx,2.13,-1.35),.028,'Chrome')
        rod('Beer tap handle',(x+dx,2.23,-1.14),(x+dx,2.45,-1.14),.027,'MetalBlack')
        sphere('Colored tap badge',(x+dx,2.43,-1.14),(.062,.07,.025),random.choice(['LabelCream','BottleGreen','LabelRed']))
box('Tap drip tray',(-5.44,1.635,-1.5),(1.75,.045,.47),'MetalBlack',.025)
for x in [-8.9,-7.2,-3.1]:pint(x,1.62,-1.76)
for x in [-9.4,-6.6,-3.65]:cocktail(x,1.62,-1.43)
box('Service towel',(-4.1,1.625,-1.65),(.52,.015,.34),'PosterPaper',.015)
for x in [-9.1,-7.55,-6.0,-4.45,-2.9]:
    cyl('Barstool foot',(x,.11,-3.15),.31,.07,'MetalBlack')
    cyl('Barstool stem',(x,.53,-3.15),.065,.82,'Chrome')
    ring('Barstool footring',(x,.4,-3.15),.22,.022,'Chrome')
    cyl('Barstool upholstered seat',(x,1.00,-3.15),.34,.16,'UpholsteryRed',24)

def booth(x,z,width=3.0):
    box('Booth black plinth',(x,.22,z),(width,.36,1.24),'DarkWalnut',.045)
    box('Booth deep red seat',(x,.52,z-.11),(width,.26,1.06),'UpholsteryRed',.10)
    box('Booth padded back',(x,1.0,z+.45),(width,1.0,.23),'UpholsteryRed',.09)
    for dx in [-width*.32,0,width*.32]:
        box('Booth back cushion',(x+dx,1.03,z+.30),(width*.30,.81,.12),'UpholsteryHighlight',.045)
    for dx in [-width/2,width/2]:box('Booth arm',(x+dx,.78,z-.02),(.18,.6,1.22),'DarkWalnut',.045)

def table(x,z,w=2.4,d=1.25):
    box('Cafe tabletop',(x,.98,z),(w,.15,d),'HoneyWood',.065)
    box('Cafe tabletop inset',(x,1.064,z),(w-.12,.025,d-.1),'WarmOak',.015)
    box('Table pedestal base',(x,.12,z),(.75,.12,.66),'MetalBlack',.035)
    box('Table pedestal',(x,.54,z),(.17,.78,.17),'MetalBlack',.025)
    pint(x-.65,1.08,z-.23);pint(x+.55,1.08,z+.18)

active_group='BoothsAndTables'
print('SIMBAR_BUILD booth furniture',flush=True)
for x in [-7.8,-3.7]:
    booth(x,6.45,3.45);table(x,5.15,2.65,1.25)
    box('Opposite booth seat',(x,.5,3.87),(3.45,.28,.85),'UpholsteryRed',.10)
    box('Opposite booth back',(x,.88,3.52),(3.45,1.05,.22),'DarkWalnut',.07)
    box('Opposite padded back',(x,.96,3.69),(3.15,.83,.14),'UpholsteryRed',.06)
for x in [-7.6,-2.7]:
    booth(x,-6.5,3.7);table(x,-5.12,2.65,1.25)
    # Front seating faces inward; thin upholstered bench keeps room visible.
    box('Front cafe bench',(x,.45,-4.0),(3.4,.27,.72),'UpholsteryRed',.08)
    for dx in [-1.3,1.3]:box('Bench leg',(x+dx,.23,-4.0),(.12,.42,.48),'DarkWalnut',.025)

active_group='Stage'
print('SIMBAR_BUILD stage',flush=True)
box('Stage riser',(5.4,.31,4.4),(7.8,.62,4.2),'StageBlack',.06)
box('Stage purple front fascia',(5.4,.31,2.27),(7.76,.48,.08),'PosterPurple',.018)
box('Stage cyan front light',(5.4,.61,2.24),(7.8,.055,.065),'NeonCyan',.012)
box('Stage cyan left light',(1.49,.61,4.38),(.055,.055,4.2),'NeonCyan',.012)
box('Stage cyan right light',(9.31,.61,4.38),(.055,.055,4.2),'NeonCyan',.012)
for z in [2.72,3.18,3.64,4.1,4.56,5.02,5.48,5.94,6.4]:
    box('Stage plank seam',(5.4,.631,z),(7.7,.008,.012),'MetalBlack',0)
box('Stage step',(5.35,.16,1.94),(2.1,.32,.61),'StageBlack',.035)
box('Stage step edge',(5.35,.325,1.62),(2.1,.035,.035),'NeonPink',.009)
for x in [2.1,8.65]:
    box('PA speaker cabinet',(x,1.48,6.21),(.88,1.68,.75),'MetalBlack',.055)
    box('Speaker grille',(x,1.48,5.813),(.76,1.5,.035),'StageBlack',.025)
    for h,r in [(1.15,.26),(1.85,.16)]:
        cyl('Visible speaker driver',(x,h,5.78),r,.035,'LeatherBlack',24,(0,0,1))
        ring('Speaker driver rim',(x,h,5.752),r,.017,'Chrome',(0,0,1))
    box('Speaker gold badge',(x,2.1,5.75),(.22,.05,.02),'Brass',.005)
for x in [3.6,7.7]:
    o=box('Wedge stage monitor',(x,.84,2.92),(1.05,.39,.65),'MetalBlack',.045)
    o.rotation_euler.x=math.radians(-12)
    box('Monitor grille',(x,1.04,2.90),(.9,.035,.49),'StageBlack',.018)

active_group='TrussAndNeon'
print('SIMBAR_BUILD neon and truss',flush=True)
# Two towers and overhead triangular truss, intentionally open to the audience.
for x in [1.73,9.12]:
    for dx in [-.18,.18]:
        for dz in [-.14,.14]:rod('Truss upright',(x+dx,.66,6.45+dz),(x+dx,5.9,6.45+dz),.037,'MetalBlack')
    for k in range(8):
        h=.75+k*.63
        rod('Truss crossbrace',(x-.18,h,6.30),(x+.18,h+.58,6.30),.025,'Chrome')
        rod('Truss crossbrace',(x+.18,h,6.60),(x-.18,h+.58,6.60),.025,'Chrome')
for h,z in [(5.78,6.27),(5.78,6.63),(6.1,6.45)]:rod('Truss horizontal',(1.53,h,z),(9.32,h,z),.045,'MetalBlack')
for k in range(12):
    x=1.6+k*.64
    rod('Truss triangle brace',(x,5.78,6.27),(x+.63,6.1,6.45),.028,'Chrome')
    rod('Truss triangle brace',(x,5.78,6.63),(x+.63,6.1,6.45),.028,'Chrome')
for i,x in enumerate([2.2,3.7,5.25,6.8,8.4]):
    rod('Light hanging bracket',(x,5.78,6.38),(x,5.48,6.38),.035,'MetalBlack')
    cyl('Stage lamp housing',(x,5.31,6.23),.19,.42,'MetalBlack',16,(0,-.75,-.65))
    cyl('Stage lamp glowing lens',(x,5.16,6.10),.165,.022,'NeonCyan' if i%2==0 else 'NeonPink',16,(0,-.75,-.65))
box('Neon sign charcoal backing',(5.4,3.66,7.23),(6.0,2.78,.08),'StageBlack',.045)
for x in [2.58,8.22]:rod('Neon cyan border',(x,2.46,7.14),(x,4.84,7.14),.022,'NeonCyan')
for h in [2.46,4.84]:rod('Neon cyan border',(2.58,h,7.14),(8.22,h,7.14),.022,'NeonCyan')
text('Neon ROCK ON','ROCK ON',(5.4,4.13,7.10),.83,'NeonPink')
text('Neon LIVE','LIVE',(5.4,3.10,7.10),.79,'NeonCyan')
text('Stage subtitle','SIMBAR  /  EST. 2026',(5.4,2.66,7.10),.17,'NeonGold')

active_group='DrumKit'
print('SIMBAR_BUILD drum kit',flush=True)
# Front kick drum lies along Unity Z, drum skins toward the audience (-Z).
cyl('Burgundy kick shell',(6.0,1.22,5.35),.59,.72,'DrumRed',32,(0,0,1))
for z in [4.975,5.725]:
    cyl('Kick drum head',(6,1.22,z),.57,.025,'DrumHead',32,(0,0,1))
    ring('Kick drum chrome hoop',(6,1.22,z-.015),.59,.035,'Chrome',(0,0,1))
text('Kick drum logo','SIMBAR',(6,1.23,4.945),.17,'MetalBlack')
for x,z,r,h in [(5.46,5.17,.32,1.94),(6.27,5.26,.35,1.97),(6.98,5.64,.43,1.12),(5.0,4.83,.33,1.22)]:
    cyl('Tom shell',(x,h,z),r,.43,'DrumRed',24)
    cyl('Tom drum skin',(x,h+.23,z),r*.96,.025,'DrumHead',24)
    ring('Tom top hoop',(x,h+.225,z),r,.023,'Chrome')
    ring('Tom bottom hoop',(x,h-.21,z),r,.023,'Chrome')
    for a in range(0,360,60):
        dx=math.sin(math.radians(a))*r;dz=math.cos(math.radians(a))*r
        rod('Drum tension rod',(x+dx,h-.16,z+dz),(x+dx,h+.17,z+dz),.012,'Chrome')
    rod('Tom stand',(x,.68,z),(x,h-.25,z),.025,'Chrome')
def cymbal(x,z,h,r):
    rod('Cymbal stand',(x,.66,z),(x,h,z),.023,'Chrome')
    for a in [0,120,240]:
        dx=math.sin(math.radians(a))*.35;dz=math.cos(math.radians(a))*.35
        rod('Cymbal tripod',(x,.95,z),(x+dx,.65,z+dz),.02,'Chrome')
    cyl('Golden cymbal',(x,h,z),r,.035,'Brass',32,r2=r*.98)
    sphere('Cymbal bell',(x,h+.025,z),(.105,.065,.105),'Brass')
    cyl('Cymbal felt',(x,h+.07,z),.028,.09,'MetalBlack')
for v in [(4.73,5.67,2.42,.54),(7.2,5.05,2.3,.52),(6.9,6.1,2.5,.47),(4.8,4.5,1.65,.31)]:cymbal(*v)
cyl('Drum throne',(6,1.03,6.23),.3,.15,'LeatherBlack')
rod('Drum throne stem',(6,.66,6.23),(6,1.0,6.23),.06,'Chrome')
box('Kick pedal',(6,.69,4.75),(.16,.07,.36),'Chrome',.015)

active_group='MicrophonesAndInstruments'
print('SIMBAR_BUILD stage instruments',flush=True)
def mic(x,z):
    rod('Microphone stand',(x,.65,z),(x,2.3,z),.022,'Chrome')
    for a in [0,120,240]:
        rod('Microphone tripod',(x,.9,z),(x+math.sin(math.radians(a))*.36,.65,z+math.cos(math.radians(a))*.36),.022,'MetalBlack')
    rod('Microphone boom',(x,2.24,z),(x-.28,2.55,z-.14),.018,'Chrome')
    cyl('Microphone handle',(x-.30,2.57,z-.15),.04,.20,'MetalBlack',16,(-.8,.5,-.3))
    sphere('Microphone grille',(x-.38,2.62,z-.18),(.062,.062,.062),'Chrome')
    # Low cable segments deliberately lie on stage, no line-renderer dependency.
    points=[(x,.658,z),(x+.12,.658,z-.32),(x+.5,.658,z-.43),(x+.88,.658,z-.2),(x+1.1,.658,z+.1)]
    for a,b in zip(points,points[1:]):rod('Microphone floor cable',a,b,.011,'LeatherBlack')
mic(5.5,2.72);mic(3.1,3.55);mic(8,3.5)
# A spare electric guitar and bass in stands, plus amps at the back.
for x,z,col in [(2.9,5.8,'Pink'),(8.05,5.65,'HoneyWood')]:
    box('Guitar amplifier',(x,.99,z),(.95,.69,.46),'MetalBlack',.045)
    box('Amp fabric grille',(x,1.0,z-.244),(.83,.47,.025),'StageBlack',.025)
    text('Amp logo','SIM AMP',(x,1.1,z-.265),.10,'Brass')
    for dx in [-.3,-.15,0,.15,.3]:cyl('Amp control',(x+dx,1.27,z-.255),.025,.018,'Chrome',12,(0,0,1))
    gx=x+.65;gz=z-.2
    sphere('Spare guitar lower body',(gx,1.18,gz),(.27,.35,.085),col)
    sphere('Spare guitar upper body',(gx,1.49,gz),(.21,.24,.08),col)
    box('Spare guitar neck',(gx,1.99,gz-.015),(.075,.85,.07),'DarkWalnut',.01)
    box('Spare guitar headstock',(gx,2.47,gz-.015),(.13,.21,.07),'HoneyWood',.025)
    box('Spare guitar pickguard',(gx+.02,1.43,gz-.087),(.18,.29,.016),'PosterPaper',.025)
    for dx in [-.022,-.01,.01,.022]:rod('Guitar string',(gx+dx,1.22,gz-.106),(gx+dx,2.53,gz-.055),.0025,'Chrome')
    for i in range(11):box('Guitar fret',(gx,1.69+i*.056,gz-.057),(.076,.008,.008),'Chrome',0)
    rod('Guitar stand',(gx,.66,gz+.12),(gx,1.6,gz+.12),.028,'MetalBlack')
    for dx in [-.24,.24]:rod('Guitar stand foot',(gx,.85,gz+.12),(gx+dx,.65,gz-.15),.025,'MetalBlack')

def plant(x,z,scale=1):
    cyl('Ceramic plant pot',(x,.29*scale,z),.29*scale,.55*scale,'Ceramic',20,r2=.37*scale)
    cyl('Plant soil',(x,.57*scale,z),.33*scale,.04*scale,'DarkWalnut')
    for i in range(12):
        angle=i*2.399
        height=random.uniform(.8,1.65)*scale
        end=(x+math.sin(angle)*random.uniform(.35,.7)*scale,.57*scale+height,z+math.cos(angle)*random.uniform(.35,.7)*scale)
        rod('Plant green stem',(x,.57*scale,z),end,.019*scale,'Foliage')
        mid=tuple((Vector((x,.57*scale,z))+Vector(end))*.5)
        o=sphere('Long glossy plant leaf',mid,(.11*scale,height*.45,.055*scale),'FoliageLight' if i%3==0 else 'Foliage')
        o.rotation_euler=Vector((0,0,1)).rotation_difference(Vector(pos(end))-Vector(pos((x,.57*scale,z)))).to_euler()

active_group='PlantsAndWallArt'
print('SIMBAR_BUILD plants and posters',flush=True)
for x,z,s in [(-9.55,-5.9,1.35),(-1.25,5.75,1.5),(-9.4,6.7,1.25),(9.73,6.65,1.0)]:plant(x,z,s)
# Music art is supplied by the runtime pasted-paper collage. Keep the brick
# wall clear here; baking the old three framed posters would show through it.
# Wall sconces, lights have mesh bulbs plus scene lights for source render.
for x in [-9.75,-6.5,-2.3]:
    box('Wall sconce bracket',(x,2.8,7.18),(.19,.5,.11),'MetalBlack',.035)
    sphere('Warm wall bulb',(x,2.9,7.02),(.13,.23,.14),'WarmBulb')

# Apply all bevels, join by logical subassembly, and bake origin to world zero.
counts={}
for group,objects in groups.items():
    print('SIMBAR_MERGE',group,len(objects),flush=True)
    bpy.ops.object.select_all(action='DESELECT')
    meshes=[]
    for o in objects:
        if o.name not in bpy.data.objects: continue
        meshes.append(o)
    if not meshes:continue
    for o in meshes:o.select_set(True)
    bpy.context.view_layer.objects.active=meshes[0]
    bpy.ops.object.join()
    joined=bpy.context.object;joined.name='SimBar_'+group
    # One modifier per assembled group keeps rebuilds fast and applies a
    # consistent tactile edge to every disconnected furniture component.
    bevel=joined.modifiers.new('Crafted bevel edges','BEVEL')
    bevel.width=.009 if group=='Floor' else .016
    bevel.segments=2;bevel.affect='EDGES';bevel.limit_method='ANGLE'
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    normal=joined.modifiers.new('Weighted corner normals','WEIGHTED_NORMAL')
    bpy.ops.object.modifier_apply(modifier=normal.name)
    # FBX importers differ in how they tessellate bevel/text n-gons. Deliver
    # explicit, clean triangles to avoid discarded self-intersecting polygons.
    bm=bmesh.new();bm.from_mesh(joined.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001)
    bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=.0000001)
    bmesh.ops.triangulate(bm,faces=list(bm.faces),quad_method='BEAUTY',ngon_method='BEAUTY')
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(joined.data);bm.free();joined.data.update()
    if group=='BarCounter': joined.data['SimBarStaffAisleOffsetZ']=0.25
    if group=='PlantsAndWallArt': joined.data['SimBarBakedPosterAssembliesRemoved']=3
    bpy.context.scene.cursor.location=(0,0,0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    counts[joined.name]={'vertices':len(joined.data.vertices),'triangles':sum(len(p.vertices)-2 for p in joined.data.polygons)}
    joined.select_set(False)

manifest['meshes']=counts
manifest['wallArt']={'bakedFrames':0, 'legacyAssembliesOmitted':3,
                     'sourceMesh':'SimBar_PlantsAndWallArt',
                     'replacement':'Runtime overlapping pasted-paper music collage'}
manifest['fbxAxis']={'forward':'-Z','up':'Y','baked':True,'workingScene':'X=Unity X, Y=Unity Z, Z=Unity Y'}

scene=bpy.context.scene
scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
scene.world.color=(.13,.13,.13)
scene.render.engine='CYCLES'
scene.cycles.samples=32
scene.cycles.use_denoising=True
scene.render.resolution_x=1600;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.view_settings.view_transform='AgX'

def area(name,p,energy,color,size,target):
    ld=bpy.data.lights.new(name,'AREA');ld.energy=energy;ld.color=color;ld.shape='DISK';ld.size=size
    o=bpy.data.objects.new(name,ld);bpy.context.collection.objects.link(o);o.location=pos(p)
    o.rotation_euler=(Vector(pos(target))-o.location).to_track_quat('-Z','Y').to_euler()
area('Warm softbox',(0,13,-8),2200,(1,.76,.52),11,(0,0,0))
area('Cool stage fill',(6,6,1.2),800,(.08,.5,1),5,(5,.6,4))
area('Pink stage fill',(9,5,5.5),700,(1,.05,.4),4,(5,.6,3))
area('Amber bar fill',(-6,6,-2),900,(1,.65,.27),5,(-6,1.5,0))
area('Top room fill',(-2,12,6),1500,(1,.87,.7),10,(0,0,0))
camd=bpy.data.cameras.new('VenuePreviewCamera');cam=bpy.data.objects.new('VenuePreviewCamera',camd);bpy.context.collection.objects.link(cam)
cam.location=pos((19,24,-28));target=Vector(pos((0,1.6,.9)));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
camd.type='ORTHO';camd.ortho_scale=27.2;scene.camera=cam
scene.render.filepath=os.path.join(SOURCE,'SimBarVenue_preview.png')

# FBX meshes only: preview cameras and lighting remain editable in the .blend.
bpy.ops.object.select_all(action='DESELECT')
for obj in scene.objects:
    if obj.type=='MESH':obj.select_set(True)
fbx_path=os.path.join(OUTPUT,'SimBarVenue.fbx')
bpy.ops.export_scene.fbx(filepath=fbx_path,use_selection=True,object_types={'MESH'},
    axis_forward='-Z',axis_up='Y',apply_unit_scale=True,global_scale=1,
    bake_space_transform=True,use_mesh_modifiers=True,mesh_smooth_type='FACE',
    use_tspace=False,path_mode='AUTO',add_leaf_bones=False,bake_anim=False)
blend_path=os.path.join(SOURCE,'SimBarVenue.blend')
bpy.ops.wm.save_as_mainfile(filepath=blend_path)
with open(os.path.join(SOURCE,'SimBarVenue_manifest.json'),'w',encoding='utf-8') as f:json.dump(manifest,f,indent=2)
print('SIMBAR_ASSET_READY',json.dumps({'fbx':fbx_path,'blend':blend_path,'meshes':len(counts),'triangles':sum(x['triangles'] for x in counts.values())}))
bpy.ops.render.render(write_still=True)
print('SIMBAR_PREVIEW_READY',scene.render.filepath)
