"""Copy frozen selected-owner office scenes for on-demand Unity presentation."""
import hashlib
import json
from pathlib import Path
import shutil
import uuid
from prepare_unity_pixel_art import meta

ROOT = Path(__file__).resolve().parent.parent
DEST = ROOT / 'Assets/Resources/SimBarOffice'


def main():
    manifest = json.loads((ROOT / 'ArtSource/DayDesk/manifest.json').read_text(encoding='utf-8'))
    DEST.mkdir(parents=True, exist_ok=True)
    folder_meta = Path(str(DEST) + '.meta')
    if not folder_meta.exists():
        folder_meta.write_text('fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n', encoding='utf-8', newline='\n')
    copied = []
    for asset in manifest['assets']:
        source = ROOT / asset['runtime']
        data = source.read_bytes()
        sha = hashlib.sha256(data).hexdigest()
        assert sha == asset['runtimeSha256'] and asset['runtimeSize'] == [640, 360]
        target = DEST / ('desk-' + asset['key'] + '.bytes')
        shutil.copyfile(source, target)
        meta(target)
        copied.append({'ownerId': asset['ownerId'], 'resource': 'SimBarOffice/desk-' + asset['key'], 'bytes': len(data), 'sha256': sha, 'byteIdentical': True})
    print(json.dumps({'lazySelectedOwnerOnly': True, 'assets': copied}, indent=2))


if __name__ == '__main__':
    main()
