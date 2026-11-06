"""Copy the reviewed source-plate seating/kit calibration, without redrawing art."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / 'ArtSource/PixelArt/V4/VenuePresentation.json'
OUTPUT = ROOT / 'Tools/WebGLTemplate/FastGame/art'


def prepare_venue_presentation(output=OUTPUT):
    data = json.loads(SOURCE.read_text(encoding='utf-8'))
    if len(data.get('seatedActors', {})) != 11:
        raise ValueError('The reviewed venue calibration needs all eleven seated actors.')
    for layer in data['occluders']:
        if len(layer['points']) < 3:
            raise ValueError('Every foreground contour must contain an actual polygon.')
        if any(actor not in data['seatedActors'] for actor in layer.get('actors', [])):
            raise ValueError('Targeted furniture masks are only for authored seated actors.')
    output = Path(output)
    output.mkdir(parents=True, exist_ok=True)
    target = output / 'venue.json'
    target.write_text(json.dumps(data, indent=2) + '\n', encoding='utf-8', newline='\n')
    return data


if __name__ == '__main__':
    prepare_venue_presentation()
