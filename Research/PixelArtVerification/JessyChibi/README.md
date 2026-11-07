# Jessy proportions and complete upper hair

Jessy's selector and office scenes now use a rounded chibi face and compact
torso to match Edison and Jaiden. The complete upper curls fit inside both
frames. The selector's highest outline has seven pixels of top clearance;
crossed arms remain near the lower edge and the common badge does not replace
any part of the hair. The office retains the same keyboard-hand pose and shared
monitor/phone geometry. Root visual review approved both images before export.

The built-in ImageGen edit sources and exact prompts are retained in
`ArtSource/Owners/ChibiJessy` and `ArtSource/DayDesk/JessyChibi`. Original supplied
and earlier generated sources are unchanged. Selector export is a centered
nearest-neighbor fit with less than one source pixel of horizontal background
trim and no vertical crop, followed by an opaque RGBA working card with identical
RGB. The existing packer uses its complete `(0,0)–(400,350)` rectangle. Office
export is complete-source nearest-neighbor resize to 640×360, copied unchanged.

Only Jessy's two native PNGs and three matching Unity resource files are changed.
Edison/Jaiden images, import GUIDs, game logic, UI/CSS, fonts, Freewill-only desk
workflow, existing saves, music, stage presentation and floor missions retain
their prior behavior. Source/export hashes and exact pixel proofs are in
`ArtSource/Owners/ChibiJessy/delivery.json` and the new browser/Unity reports here.

`browserQA/browser-qa.json` and `browserQA/daydesk/browser-qa.json` verify both selector
viewports, all three selection states and six actual owner/desk workflows.
The raw → canonical → runtime chain is pixel-exact and 56 protected hashes match
the previous release. Root reviewed the actual desktop three-card screen and
Unity selector/mobile-office captures: the top curls are visible with no badge
occlusion, identity UVs and nearest-neighbor/Point sampling. The normal Freewill
workflow, keyboard focus, partial save/reload and farewell/cleanup/results pass.
The final cohort has no JavaScript, HTTP or failed-resource errors and no
deferred art/audio before Ready. An initial local test-server refusal is retained
separately; the unchanged artifact passed after QA-only accept-backlog hardening
and a live-thread/exact-asset HTTP preflight.

`Unity/` records eight desktop/mobile visual checks and an exact three-file
copy. Ten other-owner image streams and all three target importer files remain
baseline-identical. Original preferences and Game View presets/index were
restored, with zero custom presets, clean stopped Reference and background=false.
C#, scene, profile and ProjectSettings remain unchanged. The matching Reference
WebGL build succeeded in 244,989 ms with zero errors and three warnings; four
artifact hashes are in `Unity/build-final-summary.json`.

Release `ef3109e361da0274` has 55 runtime files / 3,491,534 bytes: 2,115,835 startup
bytes, 1,228,339 deferred office bytes and 147,360 deferred album bytes. The
selector is 280,650 bytes and Jessy's office is 412,009 bytes, both smaller than
the previous images.

`local-mobile-lte.json` records three fresh cold/warm pairs at 390×844 / DPR 2,
10 Mbps, 70 ms latency and 4× CPU slowdown. Cold Ready times were
2.617 / 2.564 / 2.589 seconds; warm times were 0.428 / 0.455 / 0.464 seconds.
All six were below three seconds, with no JS, HTTP or failed-request errors.
Both CDP and Resource Timing recorded zero office, album manifest/image or MP3
requests before Ready; actual Start → planning loaded only the selected Jessy
office at 640×360. The owned local host used backlog 128 plus an exact-manifest
HTTP/live-thread preflight and was closed afterward. This is synthetic LTE
verification, not a guarantee for every physical mobile network.
