"""Copy the approved native art and calibration into Unity without creative edits."""
import json
from pathlib import Path
import re
import shutil
import uuid

ROOT=Path(__file__).resolve().parent.parent
SOURCE=ROOT/'Tools/WebGLTemplate/FastGame/art'
DEST=ROOT/'Assets/Resources/SimBarPixelArt'
ATLAS_NAMES={'sprites.png':'Characters','venue.png':'Venue','seoul.png':'Seoul'}

def convert_manifest(raw,sprites):
    """Convert dictionary metadata to JsonUtility-compatible named lists."""
    atlases=[]
    for key,entry in sprites.get('atlases',{}).items():
        filename=entry['file'];name=ATLAS_NAMES.get(filename,Path(filename).stem)
        atlas={**entry,'id':key,'resource':'SimBarPixelArt/'+name}
        atlas['directionColumns']=[{'id':direction,'column':column} for direction,column in entry.get('directionColumns',{}).items()]
        atlases.append(atlas)
    point=lambda p:{'x':p[0],'y':p[1]}
    bar={**raw.get('barRegion',{})}
    if bar:bar['offset']=point(bar['offset'])
    result={'actorOffsets':[{'id':key,'offset':point(value)} for key,value in raw.get('actorOffsets',{}).items()],
      'bandStageFeet':[{'id':key,'offset':point(value)} for key,value in raw.get('bandStageFeet',{}).items()],
      'stageElevationPixels':raw.get('stageElevationPixels',0),
      'drumForegroundDepth':raw.get('drumForegroundDepth',0),
      'barRegion':bar,'occluders':[{'points':[point(p) for p in layer['points']],'depth':layer['depth']} for layer in raw.get('occluders',[])],
      'frameWidth':sprites['frameWidth'],'frameHeight':sprites['frameHeight'],'columns':sprites['columns'],
      'drawHeight':sprites['drawHeight'],'footX':sprites['footX'],'footY':sprites['footY'],'defaultAtlas':sprites.get('defaultAtlas','animation'),'roles':[],'atlases':atlases}
    def convert_entry(entry):
        value={**entry};value['hasRow']='row' in entry
        value['variantStyles']=[{'id':style,**({'row':row} if isinstance(row,int) else row)} for style,row in entry.get('variantStyles',{}).items()]
        value['directionColumns']=[{'id':direction,'column':column} for direction,column in entry.get('directionColumns',{}).items()]
        value['directionRows']=[{'id':direction,'row':row} for direction,row in entry.get('directionRows',{}).items()]
        value['directionRowOffsets']=[{'id':direction,'row':row} for direction,row in entry.get('directionRowOffsets',{}).items()]
        return value
    for key,entry in sprites['roles'].items():
        role=convert_entry({'id':key,**entry})
        role['actions']=[convert_entry({'id':action,**values}) for action,values in entry.get('actions',{}).items()]
        result['roles'].append(role)
    return result

def meta(path,texture=False):
    target=Path(str(path)+'.meta')
    if target.exists():return
    if texture:
        text=(ROOT/'Assets/Art/StartupTextures/Owners/OwnerFF.png.meta').read_text(encoding='utf-8')
        text=re.sub(r'guid: [0-9a-f]+','guid: '+uuid.uuid4().hex,text,count=1)
        for field,value in [('filterMode',0),('enableMipMap',0),('alphaUsage',1),('alphaIsTransparency',1),('textureCompression',0)]:
            text=re.sub(r'(?m)^(\s*'+field+r':) \d+',r'\g<1> '+str(value),text)
    else:text='fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    target.write_text(text,encoding='utf-8',newline='\n')

def compact_image(source,name):
    """Keep the exact compact PNG stream; Unity decodes it once on presentation load."""
    target=DEST/(name+'.bytes');shutil.copy2(SOURCE/source,target);meta(target)
    # These were generated only by the previous version of this tool. No authored
    # scene refers to their GUIDs; the renderer uses stable resource names.
    for old in (DEST/(name+'.png'),DEST/(name+'.png.meta')):
        if old.is_file():old.unlink()

def main():
    DEST.mkdir(parents=True,exist_ok=True)
    folder=Path(str(DEST)+'.meta')
    if not folder.exists():folder.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n',encoding='utf-8')
    for source,name in [('venue.png','Venue'),('sprites.png','Characters'),('seoul.png','Seoul')]:compact_image(source,name)
    for name in ('OwnerFemale','OwnerFF','OwnerPetSounds'):
        for old in (DEST/(name+'.png'),DEST/(name+'.png.meta')):
            if old.is_file():old.unlink()
    # Existing authoring references keep their GUIDs. Retain each pre-pixel source once.
    previous=ROOT/'ArtSource/PixelArt/BeforePixelArt';previous.mkdir(parents=True,exist_ok=True)
    for source,subfolder,name in [('owner-female.png','Owners','OwnerFemale'),('owner-ff.png','Owners','OwnerFF'),('owner-pet.png','Owners','OwnerPetSounds'),('seoul.png','Seoul','SeoulNeonNight'),('seoul.png','Seoul','SeoulNightDistricts')]:
        target=ROOT/'Assets/Art/StartupTextures'/subfolder/(name+'.png')
        if target.exists() and not (previous/target.name).exists():shutil.copy2(target,previous/target.name)
        shutil.copy2(SOURCE/source,target)
        importer=Path(str(target)+'.meta')
        if importer.exists():
            text=importer.read_text(encoding='utf-8')
            for field,value in [('filterMode',0),('enableMipMap',0),('textureCompression',0)]:text=re.sub(r'(?m)^(\s*'+field+r':) \d+',r'\g<1> '+str(value),text)
            importer.write_text(text,encoding='utf-8',newline='\n')
        encoded=ROOT/'Assets/Resources/SimBarWebTextures'/(name+'.bytes')
        if encoded.exists():shutil.copy2(SOURCE/source,encoded)
    raw=json.loads((SOURCE/'venue.json').read_text(encoding='utf-8'))
    sprites=json.loads((SOURCE/'sprites.json').read_text(encoding='utf-8'))
    for key,entry in sprites.get('atlases',{}).items():
        filename=entry['file'];name=ATLAS_NAMES.get(filename,Path(filename).stem)
        if (SOURCE/filename).is_file():compact_image(filename,name)
        else:raise FileNotFoundError('Missing directional atlas: '+filename)
    # These old generated PNG streams have no authored GUID consumers. Once V4
    # is active, leaving them under Resources still bundles obsolete model art.
    if sprites.get('version',0)>=4:
        active={ATLAS_NAMES.get(entry['file'],Path(entry['file']).stem) for entry in sprites.get('atlases',{}).values()}
        for stale in ['band','band-performance','band-walk','band-social','guest-social']:
            if stale in active:continue
            for old in [DEST/(stale+'.bytes'),DEST/(stale+'.bytes.meta')]:
                if old.is_file():old.unlink()
    result=convert_manifest(raw,sprites)
    target=DEST/'Presentation.json';target.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');meta(target)
    print('Unity pixel art prepared:',DEST)

if __name__=='__main__':main()
