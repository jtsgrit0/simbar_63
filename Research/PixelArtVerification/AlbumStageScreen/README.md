# Album LED and ended-show lighting verification

Run these focused checks from the repository root:

```text
node Tools/test_native_stage_screen.mjs
uv run --with playwright python Tools/qa_native_stage_screen.py
uv run --with playwright python Tools/qa_native_show_finale.py
```

The last two now write to `Renderer` and `DOM` here. Historical evidence in
`ShowFinale` is unchanged. Each browser harness starts and closes its own
temporary local HTTP server; both use the actual game renderer and assets.

`Renderer/renderer-qa.json` records all five track mappings to the three verified
album jackets, full source-image rectangles, centered contain dimensions,
normalized LED-plane transforms, request counts, light-pass checks and errors.
Actual jackets remain square in the LED plane; separate portrait and wide
diagnostic canvases test the dark bars without modifying production art. The
screen interior is the measured quad `(665,117), (842,163), (842,230), (665,184)`
at the authored 960×558 resolution. Four clipped source-plate lamp silhouettes
remain in front of the screen while the performance is active.

After the performance ends, dynamic stage beams, colored pools and flares stop
even when the user manually resumes music. A calibrated deck-only dark overlay
also dims baked light pools, and five small lens overlays make the hanging lamps
visibly unlit. The deck treatment is reapplied to each static stage occluder so
drum masks do not restore lit patches; overlapping drum pixels are checked to
never darken beyond one deck treatment. The LED image, cyan rim, bar and ambient artwork
remain. Pausing an unfinished performance freezes the current light frame and
selected jacket. Initial lobby and planning artwork is unchanged. No cover or
manifest request gates the first frame.

`DOM/dom-audio-qa.json` records actual `HTMLAudioElement` playback, all five
playlist-button selections, one lazy manifest request and three jacket requests,
and the existing playlist control working on desktop and 390×844 mobile while
a floor mission remains active. There is no extra left-side music control. It
also checks the all-greetings-before-exit barrier, all 24 native departures,
exact farewell popup, saved continuation, real cleanup buttons, results music
access and one-time settlement. Settings, footer, music and main results actions
fit inside the panel and viewport on both tested sizes.

The matching Unity captures, actual raycast checks and final Reference WebGL
build report are retained in `../StageScreen/Unity`. Authentic originals, primary
release links and packing instructions are in `ArtSource/AlbumJackets/README.md`;
`Research/AlbumJackets/track-jackets.json` also records MP3 tags, durations, hashes
and Apple catalog identity checks for all five provided files.

Native immutable release: `52e153f5c07b9690`. Its 51 files total 2,185,050 bytes;
the optional manifest and three jackets total 147,360 bytes. The 47 startup files
total 2,037,690 bytes. `album-art.js` requests the optional local files only after
music interaction, shares one image for the three freewill tracks, publishes
decoded images only, and safely handles delayed decode, destruction and retry.
`Tools/test_native_album_art.mjs` exercises those asynchronous boundaries.

Reference WebGL build `build_0d9e0d58af1c` succeeded in 266,587 ms with zero
errors and 18 existing warnings. The compiled fallback and default native release
are packaged together. Source resources and all four compiled hashes are
verified; the Editor is stopped/clean and the original preferences, GameView and
profile are restored. Generated performance-test metadata was removed by Unity's
build postprocessor.

Local LTE cold: 2,477.0 / 2,472.8 / 2,539.9 ms. Warm: 423.9 / 397.5 / 393.2 ms.
All six meet 3,000 ms, with a real ready engine and no script/resource failures;
all three actual Start controls reach planning. Both CDP and ResourceTiming show
zero jacket-manifest, jacket-image or MP3 requests before Ready. Measurements use
fresh Chromium 390×844 touch contexts, 10 Mbps, 70 ms latency and 4× CPU slowdown;
they are synthetic mobile measurements, not a physical LTE network guarantee.
See `local-mobile-lte.json`; post-deployment checks stay in
`Library/AlbumStagePublicQA` to avoid a second evidence-only deployment.
