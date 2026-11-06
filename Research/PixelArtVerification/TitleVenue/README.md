# Owner selection venue background

The owner selection screen now layers the existing transparent `venue.png`
over the Seoul night background with pixel rendering and aspect-preserving
viewport cover. The three supplied original owner portraits, cards, labels
and controls are unchanged. No new image is generated or downloaded.

Native release: `9f75a37229a893d8`; 1,904,842 runtime bytes. The change adds
44 bytes of markup and reuses artwork already decoded before readiness.

Desktop 1920×1080 and portrait 390×844 screenshots are retained here. The visual
check confirms the venue-over-city order, pixel rendering, all three 400×350
portraits, switching owners and entering daytime planning without errors.
See [`visual-check.json`](visual-check.json).

The Unity title uses the same venue plate over its existing city underlay.
Its screen capture, compile and compiled-player reports are in
[`../OwnerVenueBackdrop/Unity/`](../OwnerVenueBackdrop/Unity/). LTE startup
verification is recorded beside this file. LTE measurements use desktop
Chromium mobile emulation at 10Mbps down/up,
70ms latency and 4× CPU slowdown; they are not physical handset measurements.

Local cold starts were 2.4503s, 2.3844s and 2.4027s; warm starts were 0.5437s,
0.4535s and 0.4852s. All six passed the 3s target, with zero JavaScript errors
or failed requests and three working Start-to-planning interactions. See
[`local-mobile-lte.json`](local-mobile-lte.json).
