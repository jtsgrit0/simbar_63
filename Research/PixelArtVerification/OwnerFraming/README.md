# Jessy card framing

The supplied original is unchanged. Its reviewed crop `(3,280)–(1083,1225)` is
1080×945, exactly the same 8:7 aspect as the 400×350 runtime card. One uniform
nearest-neighbor scale fills the card with the original record shelves and
stage, removes the two opaque side bars, and puts the crossed forearms/cuffs at
the lower image border. This makes the portrait about 53% larger than the prior
full-height contain. The top outer curls and baked 01 are outside this close-up;
the face and both crossed hands remain visible.

Every runtime RGBA pixel is an exact nearest-neighbor selection from that source
crop. No palette reduction or smoothing is used. Edison and Jaiden retain their
encoded PNG bytes, all three taglines are unchanged, and each owner now uses the
same generated number/selected badge. The current framing is reproducible with
`uv run --with pillow python Tools/prepare_original_owners.py`; its coordinates,
source hash and output hash are recorded in `ArtSource/Owners/crops.json`.

The prior complete-image contain remains documented in `../JessySupplied`.
Gameplay, confetti/cleanup, music, album artwork, lighting and save behavior are
unchanged in this revision.

`Tools/qa_native_owner_framing.py` checks the actual packaged game and writes
`browser-qa.json` plus desktop/mobile captures here. All checks passed: exact
crop RGBA, opaque varying side edges, unchanged source/other-owner/gameplay
hashes, all three working selections and badges, exact taglines, pixelated
sampling, persisted selection and actual Start → planning on 1280×800 and
390×844 viewports. No browser, HTTP, failed-request or optional-media errors
occurred. The 12 native delivery tests also passed.

Matching Unity evidence is in `Unity/`. The approved crop is copied only to
`OwnerFemale.png` and `OwnerFemale.bytes`; the other owner assets and all metas
remain unchanged. All three portraits use 400×350 identity UVs and Point
filtering; selection persistence and common badges passed the actual Editor
screen check. Editor screenshots use the existing landscape preset: no portrait
preset was present, and none was added. Native mobile verification covers the
deployed default game. Original preferences, Game View, stopped clean Reference
scene, Desktop profile and `runInBackground=false` were preserved.

The explicit Reference WebGL build succeeded in 232,993 ms with zero errors and
18 existing warnings. `Unity/build-final-summary.json` records all four output
hashes. Postbuild verification confirmed the approved source pixels, two
updated Jessy resources and 12 unchanged other-owner/meta files. The existing
build processor re-encodes the WebGL PNG stream, but decoded RGBA remains exact.

Native release `e2d68371bbaa0b3b` has 51 runtime files / 2,271,671 bytes, of which
147,360 bytes are deferred album covers and 2,124,311 bytes are startup assets.
`local-mobile-lte.json` records three fresh cold/warm pairs at 390×844 / DPR 2,
10 Mbps, 70 ms latency and 4× CPU slowdown. Cold Ready times were
2.539 / 2.549 / 2.616 seconds, warm times 0.435 / 0.382 / 0.417 seconds. All six
passed the three-second target, and each pair exercised real Start → planning.
CDP and Resource Timing both confirmed zero optional album manifest/image or
MP3 requests before Ready, with no JavaScript, network or HTTP errors. This is
synthetic mobile LTE verification, not a guarantee for every physical network.
