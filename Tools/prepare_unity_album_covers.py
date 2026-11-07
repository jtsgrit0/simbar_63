"""Copy approved authentic jacket streams for Unity's on-demand rear LED display."""
import hashlib
import json
from pathlib import Path
import shutil
import uuid

from prepare_unity_pixel_art import meta

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / 'Tools/WebGLTemplate/FastGame/art'
DEST = ROOT / 'Assets/Resources/SimBarAlbumJackets'


def main():
    manifest = json.loads((SOURCE / 'album-covers.json').read_text(encoding='utf-8'))
    tracks = []
    copied = {}
    DEST.mkdir(parents=True, exist_ok=True)
    folder_meta = Path(str(DEST) + '.meta')
    if not folder_meta.exists():
        folder_meta.write_text('fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n', encoding='utf-8', newline='\n')
    for entry in manifest['tracks']:
        source = SOURCE / entry['cover']
        stem = source.stem
        target = DEST / (stem + '.bytes')
        if stem not in copied:
            shutil.copyfile(source, target)
            meta(target)
            copied[stem] = {'bytes': target.stat().st_size, 'sha256': hashlib.sha256(target.read_bytes()).hexdigest()}
        tracks.append({'index': entry['index'], 'resource': 'SimBarAlbumJackets/' + stem})
    target = DEST / 'Tracks.json'
    target.write_text(json.dumps({'tracks': tracks}, indent=2) + '\n', encoding='utf-8', newline='\n')
    meta(target)
    print(json.dumps({'tracks': tracks, 'copied': copied}, indent=2))


if __name__ == '__main__':
    main()
