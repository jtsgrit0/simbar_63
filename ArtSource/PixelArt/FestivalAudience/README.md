# Festival audience poses

The built-in ImageGen tool created additional poses for the eight retained
guest wardrobe styles. The existing `wardrobe.reference.png` is the appearance
reference. Source prompts are in `image-prompts.json`, with the targeted vocal
spacing correction in `vocals-correction-prompt.txt` and the surfing request in
`surf-prompt.txt`.

Final source images are `gestures.source.png`, `vocals.corrected.source.png`
and `surf.source.png`. The first vocal attempt is retained separately because
its bottom shoes and row spacing were unsuitable for packing.

`Tools/prepare_festival_audience.py` performs only whole-character cropping,
proportional nearest-neighbor sizing, fixed contact-point alignment and PNG
palette encoding. It does not invent, mirror, recolor or rearrange limbs.
Runtime assets are `festival-gestures.png` and `festival-vocals.png` (each
768×512, 96 cells), `festival-surf.png` (256×512, 32 cells), and the shared
`festival-models.json`. Every four-frame group has distinct visible adjacent
frames; the measurements and source hashes are in `packing-report.json`.

The seven authored actions are horns, air guitar, headbang, sing-along, encore
clapping, dance and crowd surfing. They use a fixed three-quarter front view.
Jumping uses an authored horns pose with a separate body lift and grounded
shadow. Slam, mosh and supporters use the existing pit drawings; their floor
choreography remains simulation-owned. A crowd-surf leader stays upright while
the real supporters form, and uses horizontal artwork only after lift contact.
Waiting seated guests keep bent-knee
seated drawings until the simulation routes them toward the stage.

The renderer reads per-actor festival metadata without mutating the save.
The shared presentation clock freezes animations on pause. Order queues,
walking and departures take priority over reaction poses. `getAudiencePresentation`
exposes the selected rows, columns, contact points and seats for QA.

Checks: `Tools/test_native_festival_rendering.mjs` and the existing
`Tools/tests_pixel_presentation.mjs`.
