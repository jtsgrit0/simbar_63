# Visible curtain paper and concurrent cleanup

Run these checks from the repository root:

```text
node Tools/test_native_finale.mjs
node Tools/test_native_paper_presentation.mjs
uv run --with playwright python Tools/qa_native_curtain_cleanup.py
uv run --with playwright python Tools/qa_native_show_finale.py
```

The current browser outputs are `Concurrent` and `DOM` here. Historical
`ShowFinale`, `AlbumStageScreen` and `JessySupplied` evidence is unchanged. The
harnesses load the actual native game through temporary local servers, then
close their servers and browsers.

`Concurrent/browser-qa.json` uses a normal quiet night selected through the
game's seed and save APIs. No paper effect, actor position or choreography phase
is injected. First closing emits the missing confetti and tissue through the
real game rules, producing 56 litter debt. The renderer shows 56 colored confetti
pieces and 28 tissue streamers, with connected 10–16 pixel ribbons. Stable ground
marks represent the saved debt and disappear as the actual `cleaned` value rises.

The captured concurrent frame has the guitarist in a real highfive at 0.406s,
the bartender sweeping and 2.478/56 debt cleaned. The half-cleaned frame records
28.005 cleaned and ground marks reduced from 40 to 20. Actual delegated C-response
buttons authorize that work while the greeting remains in progress. Paper is
drawn below the DOM HUD; no spawn or performance-trigger button was added.

The report also checks pixel-exact global pause, a same-session import using the
existing renderer, partial-cleanup reload, no replayed explosion or extra throws,
zero debris at completion and a single final receipt. Transient particles reset
when the actual floor object or session changes; ground debris comes directly
from persisted throw counts and remaining cleanup debt.

`DOM/dom-audio-qa.json` retains the full playlist/audio, album-image, all-greeting
departure, popup, saved-continuation and settlement coverage. Farewell and results
contain no music controls; the original playlist remains. Results footer and
actions fit desktop and 390×844 mobile viewports. No raster artwork or startup
asset was added; the supplied Jessy card and existing album/stage presentation
remain unchanged.

`test_native_finale.mjs` exercises a genuine zero-paper night, unchanged random
mid-show timing, each missing-kind combination, active F input preservation,
C priority and actual greeting/work overlap, old saves, repeated closing and
one-time settlement. The 20-seed full-night sweep in
`../ShowDeparture/seed-sweep.json` passed; existing lifecycle, save, music,
chicken, album, pixel/stage checks and all 37 Python tests also passed.

Matching Unity checks and captures are in [PaperFinale/Unity](../PaperFinale/Unity).
The focused floor-rule verifier passed 35 checks. Two full closing runs passed;
the final fresh-Editor diagnostic also captured actual bassist/guest hugging at
16.2 seconds while an employee cleaned, retained the C work budget through a
save/reload, and reduced ground marks from 40 to 20 at half cleanup. The final
captures advance particle time with controller time, so expired airborne paper
does not linger during the greeting. Farewell and closing-results captures have
no duplicate music controls. Original preferences, Game View, stopped clean
Reference scene, Desktop profile and `runInBackground=false` were preserved.

One earlier warm automated Unity attempt raised an index exception. Its original
reflection wrapper discarded the source stack. Source review, two complete runs
and a bounded fresh-Editor repeat with preserved stacks did not reproduce it or
identify a concrete unsafe index; no speculative production change was made.
The exact limitation and the separate recovered Editor audio-thread deadlock are
recorded in [exception-audit.json](../PaperFinale/Unity/exception-audit.json).

The final explicit Reference WebGL build succeeded in 214,982 ms with zero errors
and 18 existing warnings; all four compiled artifact hashes are recorded in
[build-final-summary.json](../PaperFinale/Unity/build-final-summary.json).
The packaged native release is `217be496c01b340f`: 51 files / 2,181,720 bytes,
including 147,360 bytes of deferred album covers and 2,034,360 startup bytes.

[local-mobile-lte.json](local-mobile-lte.json) records three fresh cold/warm pairs
at 390×844 / DPR 2, 10 Mbps, 70 ms latency and 4× CPU slowdown. Cold Ready times
were 2.506 / 2.448 / 2.465 seconds; warm times were 0.500 / 0.460 / 0.464 seconds.
All six were below three seconds and each pair exercised real Start → planning.
CDP and Resource Timing both confirmed zero album manifest/image or MP3 requests
before Ready. There were no JavaScript, network or HTTP errors. This is a
repeatable LTE simulation, not a guarantee for every physical mobile network.
