# Show finale release

Native runtime: `52ee91d18ab74ef7`, 46 required files, 2,030,617 bytes.

The live order queue controls head-level menu balloons. Serving, expiration and
closing dismiss them immediately; a replacement order excludes the just-served
customer. The menu remains the existing five items. The generated six-cell atlas
also retains a chicken drawing for a future menu entry; it does not mislabel the
fries or chips. Source art and the built-in ImageGen prompt are retained in
`ArtSource/OrderIcons`.

The performance-end pause is latched per session. Manual replay is available
above active closing missions, within the farewell popup, and on the results
screen. The saved music choice and in-flight choreography survive reloads.

All four musicians finish high-fives and hugs before departure begins. Every
standing concert guest then walks through the door with the band. The native
scene has 20 such audience members and Unity has 28; their departure journals
cover all actual actors in each scene. The 11 seated customers stay. The exact
Korean farewell appears only after the full departure and does not discard
pending floor tasks or repeat a settlement.

Validation:

- 37 Python tests and nine focused JavaScript suites passed.
- Real Chromium DOM/audio finale checks passed on desktop and 390×844 mobile,
  including actual MP3 playback, unobstructed controls, saved popup continuation,
  remaining reaction missions and once-only receipts.
- Order balloon Chromium checks cover all actual menu items and manual/automatic
  serving, rejected service, movement, expiration, pause and closing.
- The 20-seed native full-night sweep and five recorded closing saves reach one
  final receipt with continuous physical movement.
- Unity export retains original decoded owner/night-background pixels and GUIDs;
  the order atlas is byte-identical to the native atlas. The actual visible-actor
  census verifies 32 departures and a cleared standing audience area.

See the sibling JSON reports and Unity captures for exact checks and source
hashes. Mobile LTE measurements use fresh desktop Chromium contexts with a
390×844 touch viewport, 10 Mbps, 70 ms latency and 4× CPU slowdown. They are
synthetic mobile measurements, not a physical LTE-network guarantee.

The final Reference WebGL build `build_ba984c1fd153` succeeded in 218,788 ms with
zero errors (18 existing warnings). The native default and compiled fallback are
packaged together. `Unity/build-final-summary.json` records all four file hashes.

Local LTE cold runs: 2,527.5 / 2,484.7 / 2,460.8 ms. Warm runs: 745.7 / 438.0 /
445.2 ms. All six pass the 3,000 ms target with no script or resource errors, and
the actual Start control opens planning in all three sessions. The order atlas
finishes before Ready in every run. See `local-mobile-lte.json`.
