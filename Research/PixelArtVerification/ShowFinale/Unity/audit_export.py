import hashlib
import json
import io
from pathlib import Path
import subprocess
from PIL import Image

folder = Path(__file__).resolve().parent
root = folder.parents[3]
source = root / 'Tools/WebGLTemplate/FastGame/art/order-icons.png'
resource = root / 'Assets/Resources/SimBarPixelArt/order-icons.bytes'
assert source.read_bytes() == resource.read_bytes()
manifest = json.loads((root / 'Assets/Resources/SimBarPixelArt/Presentation.json').read_text(encoding='utf-8'))
orders = next(atlas for atlas in manifest['atlases'] if atlas['id'] == 'orders')
assert orders['resource'] == 'SimBarPixelArt/order-icons'
assert (orders['frameWidth'], orders['frameHeight'], orders['columns']) == (32, 32, 6)
resources = []
for filename, subfolder, name in [('owner-female.png', 'Owners', 'OwnerFemale'), ('owner-ff.png', 'Owners', 'OwnerFF'), ('owner-pet.png', 'Owners', 'OwnerPetSounds'), ('seoul.png', 'Seoul', 'SeoulNeonNight'), ('seoul.png', 'Seoul', 'SeoulNightDistricts')]:
    approved = (root / 'Tools/WebGLTemplate/FastGame/art' / filename).read_bytes()
    png = (root / 'Assets/Art/StartupTextures' / subfolder / (name + '.png')).read_bytes()
    relative = 'Assets/Resources/SimBarWebTextures/' + name + '.bytes'
    packed = (root / relative).read_bytes()
    before = subprocess.check_output(['git', 'show', 'HEAD:' + relative], cwd=root)
    decode = lambda value: Image.open(io.BytesIO(value)).convert('RGBA')
    approved_image, png_image, packed_image, before_image = map(decode, (approved, png, packed, before))
    rgba = packed_image.tobytes()
    assert packed_image.size == approved_image.size == png_image.size
    assert rgba == approved_image.tobytes() == png_image.tobytes()
    resources.append({'name': name, 'dimensions': packed_image.size, 'approvedPngAndResourceRGBAExact': True, 'approvedStreamExact': approved == packed == png, 'previousResourceRGBAExact': before_image.size == packed_image.size and before_image.tobytes() == rgba, 'previousBytes': len(before), 'currentBytes': len(packed), 'guidPreserved': subprocess.check_output(['git', 'diff', '--', relative + '.meta'], cwd=root) == b''})
report = {'resourceExact': True, 'bytes': source.stat().st_size, 'sha256': hashlib.sha256(source.read_bytes()).hexdigest(), 'atlas': orders, 'existingResources': resources}
(folder / 'export-audit.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
print(json.dumps(report, indent=2))
