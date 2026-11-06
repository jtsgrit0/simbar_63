These files are untouched native save exports from actual Chromium runs. They
are test inputs and are excluded from the packaged game runtime.

- `aftershow-floor-missions.json`: `Research/PixelArtVerification/V3/aftershow/mid-gesture-save.json`.
  The closing night includes cleanup, a guest in assistance stage 3, and a
  musician approaching a fan who is still gathering. It exposed stale approach
  coordinates and blocked escort return routes.
- `aftershow-mid-gesture.json`: `Research/PixelArtVerification/V3/aftershow-raw/mid-gesture-save.json`.
  The singer is in an actual high five at 0.2667 seconds. Cleanup is pending.
- `aftershow-cleanup-stalled.json`: `Research/PixelArtVerification/V3/aftershow-raw/final-save.json`.
  All band members have exited, but 25.952 pieces of paper remain and the real
  browser run did not reach results.
- `aftershow-seed-3.json` and `aftershow-seed-10.json`: captured closing-night
  states from deterministic crowd runs. They preserve the original actors and
  mission progress for cleanup, fan-home reservation and staff-return regressions.

`Tools/test_native_saved_aftershow.mjs` imports these through the public save
API, continues, ticks normally, and sends only the currently prompted quick
input. It checks movement bounds, the escort's actual return position, results,
and receipt idempotence. No imported actor coordinate or phase is changed.
