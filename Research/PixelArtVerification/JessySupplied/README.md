# Supplied Jessy portrait verification

Run `uv run --with playwright --with pillow python Tools/qa_native_jessy_supplied.py`.
The harness loads the actual native owner-selection UI through a temporary local
server, then closes the browser and server. It does not generate or edit art.

The supplied 1087×1447 portrait is retained byte-for-byte in
`ArtSource/Owners/Supplied/Jessy-owner_p_j.PNG`. The runtime card contains the
complete source, resized with nearest-neighbor sampling to263×350 at(68,0) in
the400×350 card. The test verifies every pixel of that contained source region.
The full hair, face, crossed hands, record bookcase, band set and baked01 are
visible in the captured card detail. The selected badge fits in the left bar;
the redundant unselected badge is hidden for this card.

`browser-qa.json` records source/runtime hashes, original-image containment,
unchanged Edison and Jaiden bytes, unchanged album/light/music source hashes,
exact names/taglines, responsive card bounds, working selection and saved Jessy
continuation into planning. Screenshots cover1280×800 desktop,390×844 mobile
portrait and844×390 landscape at device scale2.

The owner packer's `cardSource` branch runs before the original matte and figure
refinement branch. `prepare_pixel_art.py` calls this packer after its older V1/V2
exports, so the supplied complete card remains authoritative on regeneration.
`prepare_unity_pixel_art.py` copies that final runtime card into the existing
owner texture references while retaining their GUIDs.

Native immutable release: `968a25bf75ce2784`, 51 files / 2,179,670 bytes.
The four optional album-jacket files are unchanged (147,360 bytes); startup files
total 2,032,310 bytes. The original supplied source remains unchanged and the
new runtime is 206,393 bytes. Both other native owner PNGs remain byte-identical.

Matching Unity owner/selection captures and build reports are retained in
`../JessyPortrait/Unity`. Reference WebGL build `build_20b5631927a7` succeeded in
238,140 ms with zero errors and 18 existing warnings. All six portrait metas/
GUIDs and the protected Edison/Jaiden streams are unchanged. The build pipeline
re-encodes Jessy's resource PNG stream, but its decoded RGBA exactly matches the
approved native card. The original preferences, GameView and profile are restored
and the saved Reference scene is stopped/clean.

Local LTE cold: 2,583.9 / 2,518.1 / 2,502.1 ms. Warm: 458.2 / 390.8 / 396.8 ms.
All six pass 3,000 ms with no script/network/HTTP errors; all three actual Start
controls open planning. CDP and ResourceTiming contain zero optional jacket or
MP3 requests before Ready. Synthetic conditions are fresh Chromium 390×844 touch
contexts, 10 Mbps, 70 ms latency and 4× CPU slowdown, rather than a physical LTE
network guarantee. See `local-mobile-lte.json`; public measurements after
deployment remain in `Library/JessySuppliedPublicQA`.
