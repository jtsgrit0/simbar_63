# Selected-owner daytime desk

The former poster research, demo/chance offers, four-block planning and artist
archive screens are replaced by the owner’s office. Only Freewill appears as a
selectable band. The player checks Freewill on the computer, saves the plan,
confirms the schedule on the phone, then opens the night. These preparation
actions are free, ordered, persisted and repeat-safe; deselecting the band clears
both preparation checks. Launch commits the one Freewill booking and existing
night/settlement flow once.

The scene uses the owner selected in the lobby: Jessy, Edison or Jaiden. Each
office PNG contains exactly that owner at the same desktop/keyboard/phone desk.
The same owner’s existing portrait is the loading fallback. Only the selected
office image is requested after entering planning; all three office images are
excluded from the initial owner-selector path. Built-in ImageGen sources,
identity references, exact prompts, runtime hashes and screen geometry are in
[`ArtSource/DayDesk`](../../../ArtSource/DayDesk/).

Speech, computer and phone work views alternate every 4.2 seconds. A channel can
also be selected directly; keyboard interaction holds its visible work pane.
Computer cursor and phone typing dots animate over the measured blank screens.
The speech bubble occupies the empty upper-left area and the caption sits below
the scene, so the owner’s face and physical phone remain visible. Reduced-motion
CSS disables blinking/transitions.

Freewill is appended at stable catalog index 24. Previous indices, locked or
active nights and one-time receipts remain compatible. An unlocked old daytime
plan is archived during migration. When a trusted day-opening wallet snapshot
exists, its exact recorded daytime debit is credited once; otherwise the wallet
is preserved without guessing past fees. The snapshot, credit and trust flag
are persisted in additive journey fields. Old planning actions are rejected for
desk plans and their controls/routes are absent from the new UI.

`Tools/test_native_day_desk.mjs` covers the new state transitions and migrations.
The existing parity, save, after-show, floor, band/guest, album/music and pixel
suites exercise the real new desk actions through `native_test_workflow.mjs`.
The 14 native delivery checks also pass with `daydesk.js` included in the package;
all three deferred office scenes are required in the release without receiving
startup preload hints.

`browserQA/browser-qa.json` records six actual owner/viewport workflows at
1280×800 and 390×844. Each selected owner’s 640×360 scene and held-loading
fallback, one Freewill checkbox, all work views, disabled-action guards, partial
save/resume, deselection reset and real night launch passed. One full night
continued through farewell acknowledgment, cleanup, its one-time receipt and
results. Separate probes verify Tab/Space in an automatically shown work pane
and the 844×390 layout. No browser/HTTP/failed-request errors occurred; CDP and
Resource Timing both showed no office, album or MP3 request before Ready and
only the selected office afterward. The root reviewed the desktop computer and
mobile phone captures, including the unobstructed physical phone.

`Unity/verification-summary.json` records zero compiler errors, 17 new desk and
37 legacy model checks, desktop/mobile captures for all three owners plus short
landscape, actual toggle/button/save/Continue/night-launch behavior and migration
checks. Physical work indicators are exclusive to their channel, the artwork
stays stationary, and manual/checkbox interaction holds its screen for seven
seconds before automatic cycling resumes. Startup office texture count is zero;
the three dedicated resource PNG streams match their runtime source bytes.

The original preferences were restored byte-exactly, along with all seven
original Game View preset names/types/dimensions, selected index zero and zero
custom presets. Reference is saved, clean and stopped, `runInBackground=false`,
and the Desktop profile is unchanged. `Unity/qa-cleanup-audit.json` records an
earlier QA-only preset-index cleanup loop and its bounded fix/recovery; it was
not an established audio deadlock. Final preservation checks passed.

The final Reference WebGL build succeeded in 257,306 ms with zero errors and
18 existing warnings; `Unity/build-final-summary.json` records four artifact
hashes. Native release `46fcd17b9c72c682` contains 55 files / 3,519,536 bytes:
2,131,790 startup bytes, 1,240,386 deferred office bytes and 147,360 deferred
album-cover bytes. Office scenes are required in the package but are not
preloaded. The final font has 593 codepoints / 59,352 bytes.

`local-mobile-lte.json` records three fresh cold/warm pairs at 390×844 / DPR 2,
10 Mbps, 70 ms latency and 4× CPU slowdown. Cold Ready times were
2.646 / 2.867 / 2.570 seconds; warm times were 0.570 / 0.490 / 0.470 seconds.
All six were below three seconds with the real engine ready and zero JavaScript,
network or HTTP errors. Both CDP and Resource Timing recorded zero office,
album-manifest/image or MP3 requests before Ready. After actual Start → planning,
each pair requested only the selected owner’s 640×360 office. This is synthetic
mobile LTE verification, not a guarantee for every physical network.
