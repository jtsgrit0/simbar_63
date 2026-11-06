# Owner backdrops and furniture occlusion

Native release: `41f4ca6a6f973eab`; 2,003,061 runtime bytes.

The owner cards replace their ivory paper with the previous magenta, cyan and
amber live-house settings. The supplied figures retain their exact original
RGB pixels. Full source images, technical mattes, built-in ImageGen background
restorations and exact prompts are documented in
[`ArtSource/Owners`](../../../ArtSource/Owners/README.md). Independent original
file and retained-pixel checks are in
[`../OwnerCardBackdrops/owner-pixel-audit.json`](../OwnerCardBackdrops/owner-pixel-audit.json).

The drummer's calibrated feet are `[741,265]`, directly behind the central
rack tom and SIMBAR bass drum. The foreground drum/cymbal masks cover the
correct body portions while the hands and held sticks animate above them.
Stage walking uses natural depth so the drummer can step in front of the kit.

Eleven seated guests use explicit seat anchors and depth instead of generic
floor projection. Targeted masks restore actual stool lips, sofa backs and
table fronts immediately after the appropriate seated figure; they do not
erase unrelated people in front. Seated floor shadows are hidden. Anchors and
masks release for moving or standing actors, including the escort proxy.

The source venue PNG is unchanged. The reproducible calibration is
[`VenuePresentation.json`](../../../ArtSource/PixelArt/V4/VenuePresentation.json),
applied by `Tools/prepare_venue_presentation.py` after the standard art packers.
The same data is exported to the Unity presentation.

Actual rendered evidence includes `final-drummer.png`, `final-seats-back.png`,
`final-seats-front.png`, `behind-kit-closeup.png` and `front-of-kit-closeup.png`.
[`renderer-qa.json`](renderer-qa.json) checks all eight drum frames, exact pause,
standing/escort release and drummer stage departure. Desktop/mobile owner UI,
live animation and pause checks passed in [`FinalLayout/`](FinalLayout/).
All 37 Python delivery/cache/export tests and the scoped pixel-presentation
JavaScript suite passed.

LTE reports use desktop Chromium mobile emulation at 10Mbps down/up, 70ms
latency and 4× CPU slowdown. They are not physical handset measurements.

Local cold starts were 2.4103s, 2.4139s and 2.4705s; warm starts were 0.3964s,
0.4257s and 0.4190s. All six runs passed the 3s target with a ready real engine,
zero errors/failed requests and working Start-to-planning controls. See
[`local-mobile-lte.json`](local-mobile-lte.json).

The matching Unity Editor checks passed all three owner backgrounds, eleven
seat anchors/depths, targeted mask ordering, hidden seat shadows, standing and
moving release, exact pause, drummer behind/front/floor transitions and the
completed after-show sequence. See [`Unity/`](Unity/).
