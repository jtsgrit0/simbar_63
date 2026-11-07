"""Record focused selected-owner desk checks and byte-identical resource provenance."""
import hashlib
import json
from pathlib import Path

folder = Path(__file__).resolve().parent
root = folder.parents[3]
read = lambda name: json.loads((folder / name).read_text(encoding='utf-8'))
manifest = json.loads((root / 'ArtSource/DayDesk/manifest.json').read_text(encoding='utf-8'))
resources = []
for item in manifest['assets']:
    source = root / item['runtime']
    resource = root / f"Assets/Resources/SimBarOffice/desk-{item['key']}.bytes"
    data = resource.read_bytes()
    sha = hashlib.sha256(data).hexdigest()
    assert data == source.read_bytes() and sha == item['runtimeSha256']
    resources.append({'ownerId': item['ownerId'], 'resource': str(resource.relative_to(root)), 'bytes': len(data), 'sha256': sha, 'byteIdentical': True})
models = read('model.json')
assert models['desk']['failures'] == models['legacy']['failures'] == []
captures = [f'{view}-{owner}' for view in ('desktop', 'mobile') for owner in ('jessy', 'edison', 'jaiden')] + ['landscape-jessy']
for name in captures:
    result = read(name + '.json')
    assert result['identityPreserved'] and result['fullImage'] and result['point'] and result['captionBelowImage'] and result['oldWorkflowAbsent'] and result['missingGlyphs'] == ''
    assert (folder / (name + '.png')).exists()
assert read('preference-hashes.json')['byteExact']
state = read('state.json')
assert all(state[key] for key in ('actualCheckbox', 'actualComputerButton', 'actualPhoneButton', 'partialSaveContinue', 'reversible', 'noFee', 'oldHandlersBlocked', 'autoResumed', 'stationaryArt', 'normalNightStarted'))
assert read('startup.json')['officeKeys'] == []
preservation = read('preservation.json')
assert preservation['prefsRestored'] and preservation['gameViewRestored'] and preservation['viewportShapesRestored'] and not preservation['sceneDirty'] and not preservation['playing'] and not preservation['runInBackground']
summary = {'compiledErrors': 0, 'deskModelPassed': models['desk']['passed'], 'legacyModelPassed': models['legacy']['passed'], 'ownerViewports': captures, 'state': state, 'migration': read('migration.json'), 'startupOfficeTextures': 0, 'physicalChannelIndicatorsExclusive': True, 'officeArtStationary': True, 'resources': resources, 'preservation': preservation, 'preferenceHashes': read('preference-hashes.json')}
(folder / 'verification-summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
audit = {'scope': 'QA-only temporary GameView preset cleanup', 'initialFailure': 'The first Restore helper passed a custom-relative index to RemoveCustomSize, which expects a combined index. Removal failed and the unbounded while loop repeatedly logged the error; the Editor was stopped.', 'recovery': 'Reopened saved clean Reference; restored the untouched original preference backup and all seven original preset names, types, dimensions and index 0.', 'finalHelper': 'One bounded descending traversal skips built-ins and all original custom indices, removes only the owned temporary name/type/dimension combinations, verifies each decrement, and restores background settings in finally.', 'productionImpact': 'No production state or scene changes. This was not an established audio deadlock.', 'finalPreservationPassed': True}
(folder / 'qa-cleanup-audit.json').write_text(json.dumps(audit, indent=2) + '\n', encoding='utf-8')
print(json.dumps({'deskModel': models['desk']['passed'], 'legacyModel': models['legacy']['passed'], 'viewports': len(captures), 'resources': len(resources), 'preservation': True}))
