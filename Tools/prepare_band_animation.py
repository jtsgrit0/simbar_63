"""Register original-FBX pixel animation bakes; never change game state or raster art."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ART = ROOT / 'Tools/WebGLTemplate/FastGame/art'
DIRECTIONS = {'FR': 0, 'BR': 1, 'BL': 2, 'FL': 3}
STYLES = ['Casual', 'Suit', 'Beach', 'Worker', 'Farmer', 'Adventurer', 'Casual2', 'Punk']


def register_animation(data, available):
    # V4 is approved illustrated art. Re-running an older FBX export must never
    # silently restore the previous model renders in an established V4 release.
    if data.get('version',0)>=4:
        return data
    data['version'] = 3
    roles = data['roles']
    atlases = data.setdefault('atlases', {})
    definitions = {
        'performance': ('band-performance.png', 32),
        'bandWalk': ('band-walk.png', 8),
        'bandSocial': ('band-social.png', 8),
        'guestSocial': ('guest-social.png', 8),
    }
    for key, (file, columns) in definitions.items():
        if file in available:
            atlases[key] = {'file': file, 'frameWidth': 64, 'frameHeight': 64, 'columns': columns}
    for index, name in enumerate(['singer', 'guitarist', 'bassist', 'drummer']):
        role = roles[name]
        actions = role.setdefault('actions', {})
        rows = {direction: index * 4 + offset for direction, offset in DIRECTIONS.items()}
        if 'band-performance.png' in available:
            actions['perform'] = {'atlas': 'performance', 'directionRows': rows,
                'frameColumns': list(range(32)), 'fps': 8}
        if 'band-walk.png' in available:
            role.update(atlas='bandWalk', row=index * 4, directionRows=rows, frameColumns=[0])
            for action in ['walk', 'exit']:
                actions[action] = {'atlas': 'bandWalk', 'directionRows': rows,
                    'frameColumns': list(range(8)), 'fps': 8}
            for action in ['idle', 'aftershow-wait']:
                actions[action] = {'atlas': 'bandWalk', 'directionRows': rows, 'frameColumns': [0]}
        if 'band-social.png' in available:
            for action, offset in [('highfive', 0), ('hug', 16)]:
                actions[action] = {'atlas': 'bandSocial',
                    'directionRows': {direction: row + offset for direction, row in rows.items()},
                    'frameColumns': list(range(8)), 'frameCount': 8, 'progressFrames': True, 'fps': 8}
    if 'guest-social.png' in available:
        for name in ['guest', 'support', 'surfer']:
            actions = roles[name].setdefault('actions', {})
            for action, offset in [('highfive', 0), ('hug', 32)]:
                actions[action] = {'atlas': 'guestSocial',
                    'variantStyles': {style: index * 4 + offset for index, style in enumerate(STYLES)},
                    'directionRowOffsets': DIRECTIONS,
                    'frameColumns': list(range(8)), 'frameCount': 8, 'progressFrames': True, 'fps': 8}
    return data


def main():
    target = ART / 'sprites.json'
    data = json.loads(target.read_text(encoding='utf-8'))
    available = {path.name for path in ART.glob('*.png')}
    data = register_animation(data, available)
    encoded = json.dumps(data, ensure_ascii=False, indent=2) + '\n'
    target.write_text(encoded, encoding='utf-8')
    (ROOT / 'ArtSource/PixelArt/DirectionalSpriteContract.json').write_text(encoded, encoding='utf-8')
    print('Registered FBX animation atlases:', ', '.join(key for key in data['atlases']
        if key in ('performance', 'bandWalk', 'bandSocial', 'guestSocial')))


if __name__ == '__main__':
    main()
