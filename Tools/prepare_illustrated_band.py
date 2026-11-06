"""Register the approved illustrated band once every V4 atlas is complete.

The band is one costume-consistent set: performance, walking, greetings and
jumps switch together. Older FBX render sheets stay out of the essential list.
"""
import json
from pathlib import Path
import struct

ROOT=Path(__file__).resolve().parent.parent
ART=ROOT/'Tools/WebGLTemplate/FastGame/art'
DIRECTIONS={'FR':0,'BR':1,'BL':2,'FL':3}
ROLES={'singer':'singer','guitarist':'guitar','bassist':'bass','drummer':'drums'}
FILES={**{f'bandPerformance{role.title()}':(f'v4-band-{alias}-performance.png',8,4)
    for role,alias in ROLES.items()},
    'bandWalk':('v4-band-walk.png',4,16),
    'bandJump':('v4-band-jump.png',8,8),
    'bandGreetA':('v4-band-greet-a.png',8,8),
    'bandGreetB':('v4-band-greet-b.png',8,8),
    'guestSocial':('v4-guest-social.png',8,32)}

def register(data):
    for filename,cols,rows in FILES.values():
        raw=(ART/filename).read_bytes()
        if raw[:8]!=b'\x89PNG\r\n\x1a\n' or struct.unpack('>II',raw[16:24])!=(cols*64,rows*64):
            raise ValueError('Incomplete illustrated atlas: '+filename)
    data['version']=4
    atlases=data['atlases']
    for key in ['band','performance','bandSocial','bandWalk','bandJump','bandGreetA','bandGreetB']:
        atlases.pop(key,None)
    for key,(filename,cols,_) in FILES.items():
        atlases[key]={'file':filename,'frameWidth':64,'frameHeight':64,'columns':cols}
    for index,name in enumerate(ROLES):
        role=data['roles'][name]
        rows={direction:index*4+offset for direction,offset in DIRECTIONS.items()}
        role.update(atlas='bandWalk',row=index*4,directionRows=rows,frameColumns=[0])
        actions=role.setdefault('actions',{})
        facing='FR' if name=='guitarist' else 'FL'
        actions['perform']={'atlas':'bandPerformance'+name.title(),'directionRows':DIRECTIONS,'direction':facing,
            'frameColumns':list(range(8)),'fps':8}
        for action in ['walk','exit']:
            actions[action]={'atlas':'bandWalk','directionRows':rows,'frameColumns':list(range(4)),'fps':8}
        for action in ['idle','aftershow-wait']:
            actions[action]={'atlas':'bandWalk','directionRows':rows,'frameColumns':[0]}
        for action,first in [('highfive',0),('hug',4)]:
            actions[action]={'atlas':'bandGreetA' if index<2 else 'bandGreetB',
                'directionRows':{direction:(index%2)*4+offset for direction,offset in DIRECTIONS.items()},
                'frameColumns':list(range(first,first+4)),'frameCount':4,'progressFrames':True,'fps':8}
        if name in ['guitarist','bassist']:
            actions['jump']={'atlas':'bandJump',
                'direction':facing,
                'directionRows':{direction:(0 if name=='guitarist' else 4)+offset for direction,offset in DIRECTIONS.items()},
                'frameColumns':list(range(8)),'fps':8}
    guest_file=ART/'v4-guest-social.png'
    if guest_file.is_file():
        raw=guest_file.read_bytes()
        if struct.unpack('>II',raw[16:24])!=(512,2048):raise ValueError('Incomplete illustrated guest greetings')
        atlases['guestSocial']={'file':guest_file.name,'frameWidth':64,'frameHeight':64,'columns':8}
        styles=['Casual','Suit','Beach','Worker','Farmer','Adventurer','Casual2','Punk']
        for name in ['guest','support','surfer']:
            actions=data['roles'][name].setdefault('actions',{})
            for action,first in [('highfive',0),('hug',4)]:
                actions[action]={'atlas':'guestSocial','variantStyles':{style:index*4 for index,style in enumerate(styles)},
                    'directionRowOffsets':DIRECTIONS,'frameColumns':list(range(first,first+4)),
                    'frameCount':4,'progressFrames':True,'fps':8}
    # Loading uses this exact atlas list. Retain only sheets referenced by roles.
    used=set()
    for role in data['roles'].values():
        for entry in [role,*role.get('actions',{}).values()]:
            if entry.get('atlas'):used.add(entry['atlas'])
            used.update(entry.get('atlasFrames',[]))
    if data.get('defaultAtlas'):used.add(data['defaultAtlas'])
    data['atlases']={key:value for key,value in atlases.items() if key in used}
    return data

def main():
    target=ART/'sprites.json'
    data=register(json.loads(target.read_text(encoding='utf-8')))
    encoded=json.dumps(data,ensure_ascii=False,indent=2)+'\n'
    target.write_text(encoded,encoding='utf-8')
    (ROOT/'ArtSource/PixelArt/DirectionalSpriteContract.json').write_text(encoded,encoding='utf-8')
    venue=ART/'venue.json'
    room=json.loads(venue.read_text(encoding='utf-8'))
    room['bandStageFeet']={'singer':[676,326],'guitarist':[635,306],'bassist':[820,331],'drummer':[741,287]}
    # The authored foreground kit covers the drummer's lower body and keeps the
    # cymbals/sticks attached to the same illustrated drum position.
    if room.get('occluders'):room['occluders'][-1]['depth']=310
    room['drumForegroundDepth']=310
    venue.write_text(json.dumps(room,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Registered illustrated V4 band:',', '.join(data['atlases']))

if __name__=='__main__':main()
