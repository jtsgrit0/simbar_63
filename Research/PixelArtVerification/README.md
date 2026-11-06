# Pixel-art game verification

The latest owner pose/size/layout, caption, rim-light and standing bartender
follow-up is verified in [`OwnerRefinement/`](OwnerRefinement/),
[`OwnerRefinements/`](OwnerRefinements/) and [`BartenderPatrol/`](BartenderPatrol/).

The earlier owner-card backgrounds, seated furniture masks and drummer
placement are verified in [`SeatedDrummer/`](SeatedDrummer/) and
[`OwnerCardBackdrops/`](OwnerCardBackdrops/).

The earlier selector-background follow-up is in [`TitleVenue/`](TitleVenue/):
the supplied owner portraits are unchanged and the existing pixel venue is
layered over the city on the owner selection screen.

Current owner, illustrated-band, stage-light and after-show verification is
collected in [`Final/`](Final/). The retained measurements and screenshots below
describe the earlier baseline release and should not be used as the current
release's performance result. Unity parity evidence is in [`V4/Unity/`](V4/Unity/).

## Earlier baseline release

The primary browser player runs its complete game logic directly in JavaScript/Canvas2D. The first interactive frame includes the approved-style empty venue, independently moving adult pixel sprites, owners, city, fonts and original UI icons. There is no background Unity startup and no title-only readiness shortcut. Music and researched poster images load on demand.

Runtime release: `ccda33d8a6e50196`; 805,353 bytes before HTTP headers/compression. Images are generated production assets, not a screenshot with baked UI or people. The same production art and calibration are also copied into the Unity presentation by `Tools/prepare_unity_pixel_art.py`.

## Local mobile test

Chromium, 390×844 CSS pixels, DPR2, touch, Android user agent, 10Mbps down/up, 70ms latency, CPU slowdown4×, fresh browser cache for each cold sample. The runner waits for the real engine and its painted controls, then exercises Start → planning. This is desktop browser emulation, not measurement on a physical LTE handset.

| Sample | Cold start | Warm start |
|---|---:|---:|
| 1 | 1.848s | 0.610s |
| 2 | 1.594s | 0.524s |
| 3 | 1.541s | 0.461s |

All runs: no JavaScript errors, failed requests, HTTP errors or Unity WASM/data downloads. Cold wire transfer816,390B. See [raw measurements](mobile-lte-local.json).

## Behavior and visual checks

- Desktop, portrait and landscape render actual moving sprites. Pause freezes the world. Furniture and drum masks draw people in the correct layers. UI remains sharp.
- Owner selection, real/day-specific poster research, 24 artists, offers, tickets, promotion, guide, all5 menu items, pour timing, staff, upgrades, events, both languages, save/resume and5 music tracks passed browser UI checks. See [UI result](UI/result.json).
- A real224.07s night naturally generated paper and a guest. Holding F counted once; a timed-out round could be retried. Existing staff walked to clean; two C rounds completed. The receipt awarded70 wallet units (₩70,000), two tasks and40XP; wallet/XP stayed identical after reload. See [night result](NaturalNight/run.json).
- 33 Python packaging/cache/delivery tests pass. Native parity tests cover daily seeded lineups, saved venues, poster masks and audio lifecycle. Save tests cover actual Unity binary preference parsing, delayed migration, input gating, a naturally scheduled complete night and once-only rewards.

Run from the project root:

```powershell
python -m unittest discover -s Tools -p "test_*.py"
node Tools/test_native_parity.mjs
node Tools/test_native_saves.mjs
uv run --with playwright python Tools/qa_native_ui.py http://127.0.0.1:9873/
uv run --with playwright python Tools/qa_native_night.py http://127.0.0.1:9873/
uv run --with playwright python Tools/qa_pixel_art.py --url http://127.0.0.1:9873/
uv run --with playwright python Tools/benchmark_mobile_lte.py --url http://127.0.0.1:9873/ --samples 3 --mbps 10 --latency-ms 70 --cpu-rate 4
```

Screenshots: [owners](desktop-title.png), [game](desktop-game.png), [mobile game](mobile-game.png), [landscape](mobile-landscape-game.png). Generated full rasters, approved reference and exact new prompts are in `ArtSource/PixelArt/`. The local `Preview/` folder and repeated diagnostic screenshots are not deployment input.
