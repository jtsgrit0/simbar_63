# Owner card refinements and standing bartender

Native release: `52288ceea581f302`; 2,005,026 runtime bytes.

The owner captions are exact and mapped to the existing owner IDs:

- Jessy: `자기야 오늘밤 라이브 하러와`
- Edison: `평생 Rock&Roll`
- Jaiden: `오늘밤은 무슨 음악을 들을까?`

Jessy's raised wrist and pink nails use the reviewed built-in ImageGen edit.
The foreground then rotates 8° counterclockwise and enlarges to 115% using only
nearest-neighbor sampling. The top remains inside the card, with the approved
lower jacket/waist crop. Edison keeps the original figure and receives a
separate black DJ support behind the laptop and arms. Jaiden's original figure
moves down 11px so its last visible row reaches the image bottom. All three
receive a crisp 2px exterior rim in the room color, opacity 0.5 (alpha 128).

Sources, exact prompts, profile, masks and deterministic geometry are in
[`ArtSource/Owners/Refinements`](../../../ArtSource/Owners/Refinements/README.md).
The original supplied sheet and all three original crops are unchanged.
Independent preservation, source-palette, placement and rim checks are in
[`../OwnerRefinements/audit.json`](../OwnerRefinements/audit.json).

The bartender uses the existing full standing Suit frame while stationary
behind the counter and the existing walk frames while traversing the service
aisle. The 9s patrol stays between x−8.95 and −7.05 at 0.66m/s, with service/pour
targets and floor duties taking precedence. The bar employee projection offset
is `[0,17]`, exposing the standing torso while the counter hides the legs.
Existing guest seats and drum masks remain unchanged. See
[`../BartenderPatrol/`](../BartenderPatrol/).

Actual desktop/mobile UI checks passed the exact Korean and English captions,
font coverage, selection, Start-to-planning and continuous bar patrol. See
[`Native/ui-check.json`](Native/ui-check.json). All 37 Python tests, the focused
bartender/pixel/after-show fixtures and 20 complete normal seeded nights passed.
Matching Unity owner UI, standing/serving/walking frames, patrol, pause and
preference preservation passed in [`Unity/`](Unity/).

Local LTE cold starts: 2.5006s / 2.4325s / 2.4220s. Warm starts: 0.7416s / 0.5003s
/ 0.4819s. All six passed the 3s target with a real ready engine, zero errors or
failed resources, and working Start-to-planning controls. See
[`local-mobile-lte.json`](local-mobile-lte.json). The profile is desktop Chromium
mobile emulation at 10Mbps down/up, 70ms latency and 4× CPU slowdown, not a physical
LTE handset measurement.
