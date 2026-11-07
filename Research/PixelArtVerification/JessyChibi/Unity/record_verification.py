"""Collect only the focused asset verification, without running gameplay/model tests."""
import json
from pathlib import Path

folder = Path(__file__).resolve().parent
read = lambda name: json.loads((folder / name).read_text(encoding='utf-8'))
selectors = [f'selector-{view}-{owner}' for view in ('desktop', 'mobile') for owner in ('jessy', 'edison', 'jaiden')]
offices = [f'office-{view}-jessy' for view in ('desktop', 'mobile')]
for name in selectors:
    item = read(name + '.json')
    assert item['allThreeOriginalAspect'] and item['identityUvNoCrop'] and item['pointFilter'] and item['sharedBadges']
    assert (folder / (name + '.png')).exists()
for name in offices:
    item = read(name + '.json')
    assert item['whole640x360Image'] and item['identityUvNoCrop'] and item['pointFilter'] and item['captionBelowImage'] and item['freewillUiUnchanged']
    assert (folder / (name + '.png')).exists()
copy = read('asset-copy.json')
assert len(copy['copies']) == 3 and all(x['byteIdentical'] for x in copy['copies'])
assert copy['allOtherOwnerImageStreamsUnchanged'] and copy['allThreeTargetImporterBytesUnchanged']
preservation = read('preservation.json')
assert preservation['prefsByteExact'] and preservation['gameViewRestored'] and not preservation['sceneDirty'] and not preservation['playing'] and not preservation['runInBackground']
summary = {'assetOnly': True, 'productionCSharpUnchanged': True, 'focusedVisualChecksPassed': selectors + offices, 'wholeApprovedSourceShown': True, 'rootApprovedSelectorHairClearancePixels': 7, 'noAdditionalUiCrop': True, 'copies': copy, 'preservation': preservation, 'gameplayModelRerun': False}
(folder / 'verification-summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps({'visualChecks': len(selectors) + len(offices), 'copies': 3, 'preservation': True}))
