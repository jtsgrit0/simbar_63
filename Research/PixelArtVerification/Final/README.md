# Final owner and illustrated-band delivery

The active title uses the three separate crops of the supplied `owner_p.png`.
The source is retained unchanged in `ArtSource/Owners/Original-owner_p.png`;
`ArtSource/Owners/crops.json` records the source hash and each named polygon.
The browser portraits and decoded Unity resource portraits have identical RGBA
pixels. The original illustration is not regenerated or recolored.

The world uses the V4 room without indoor plants and with floor lettering for
The Beatles, Queen, Nirvana, Taylor Swift, Michael Jackson and AC/DC. Live sprites
remain separate from the room. The audience retains eight distinct wardrobes
and role-specific seated, serving, dancing and crowd-surfing directions.

The four musicians use the illustrated V4 performance, walking, jumping and
greeting atlases. Drums occlude the drummer's lower body; raised hands and held
sticks remain visible. The singer performs at the fixed microphone. Guitar and
bass have grounded sways and explicit jump/landing poses with a matching shadow.
Moving colored stage beams, pools and source flares are restored. The three
management buttons retain separate left icons and centered live labels.

After the show, musicians walk down the authored steps, meet individual fans
for high-fives and hugs, and walk to the exit. Contact distances are 0.60m and
0.43m; gesture progress, phase, partners and positions survive save/resume.
Closing staff finish the random QTE-driven cleanup or escort and physically
return to clear parking positions before settlement. Receipt rewards are paid
once. Navigation keeps physical furniture and body collision checks active.

The eight band atlases alone use the reviewed 256-color, no-dither runtime
encoding. Full-color generated sources remain in `ArtSource/PixelArt/V4/`.
Owners, audience and guest social art are excluded from that palette change.
See [`band-runtime-encoding.json`](band-runtime-encoding.json) for the recorded
pixel hashes and [`band-palette-comparison.png`](band-palette-comparison.png)
for the reviewed comparison.

The current release's browser checks are collected in [`ui/`](ui/),
[`visual/`](visual/), [`details/`](details/) and [`aftershow/`](aftershow/).
[`seed-sweep.json`](seed-sweep.json) records deterministic complete-night
navigation checks. Raw repeated-run videos stay local; selected frames and
reports are retained here. Unity Editor and compiled-player evidence is kept
in [`../V4/Unity/`](../V4/Unity/).

Mobile startup measurements use Chromium 390×844 CSS px, DPR2, touch, an Android
user agent, 10Mbps down/up, 70ms latency and 4× CPU slowdown. Each cold sample uses
a fresh browser cache. Readiness requires the real engine, decoded art and
painted controls, followed by Start→planning; a loading page does not qualify.
This is a repeatable desktop LTE simulation, not a physical handset test.
The measured release hash and all samples are retained in the local and public
mobile LTE JSON reports beside this file.

## Verified local release

Native release: `177c90f03ec2b563`; runtime files total 1,904,798B.

| Sample | Cold startup | Warm startup |
|---|---:|---:|
| 1 | 2.4325s | 0.5903s |
| 2 | 2.4045s | 0.6520s |
| 3 | 2.3916s | 0.9371s |

All six runs passed the 3s target. Each cold run transferred 1,919,408B including
HTTP overhead. All three Start interactions reached planning with no JavaScript
errors or failed requests. See [`local-mobile-lte.json`](local-mobile-lte.json).

All 37 Python packaging/cache/art-delivery tests and all six native presentation,
after-show, parity, pit-return, saved-after-show and save-migration suites passed.
Twenty complete normal nights with distinct deterministic seeds reached Results,
with all four musicians gone, all eight fans returned to their captured homes,
and no pending floor mission or escort. The maximum 50ms actor movement was 0.1593m;
the longest complete night was 245.7s including the 180s performance.

Actual desktop/mobile browser checks passed all19 assertions on the same
release. The19 include continuous movement, all eight fan returns, both staff
physically parked before the receipt, pause/save/resume, C/F input and one-time
settlement. The final Unity WebGL build succeeded with0 errors in229.872s;
the Reference scene, source/save preservation and output hashes are recorded
in [`../V4/Unity/build-final-summary.json`](../V4/Unity/build-final-summary.json).
The staged native manifest's45 file sizes and SHA-256 values were checked
against the actual Git index, along with both compiled Unity binary hashes.
