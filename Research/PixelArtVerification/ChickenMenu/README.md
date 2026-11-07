# Fried chicken menu addition

The sixth item is `후라이드 치킨 / FRIED CHICKEN`, price 18 wallet units
(₩18,000), food item index 5 and keyboard shortcut 6. Existing item indices,
names and prices are unchanged. The existing chicken icon uses atlas column 5;
no additional image download is needed. Its source and original ImageGen prompt
remain in `ArtSource/OrderIcons`.

Native runtime: `2125f7b444293ff3`, 46 files / 2,031,162 bytes. The
updated 58,860-byte UI font contains the new Korean menu and description glyphs.

`browser-qa.json` records nine real Chromium checks against the source hashes:
normal RNG chicken orders, correct atlas sampling, wrong-choice retention,
keyboard/menu food plating, manual and hired-barback serving, safe save reload,
mobile menu reachability, natural closing and old-save compatibility.

The native save contract is unchanged: pending orders and selection persist;
in-progress preparation safely resets. Unity preserves its existing transient
queue behavior. Historical five-item fixtures remain unchanged compatibility
inputs. The OrderBubbles harness now uses actual native actor IDs and explicitly
requires all six distinct pending menu items before comparing atlas cells.

Verification commands:

```powershell
node Tools/test_native_chicken.mjs
uv run --with playwright python Tools/qa_native_chicken.py
uv run --with playwright python Tools/qa_native_order_bubbles.py
uv run python -m unittest discover -s Tools -p "test_*.py"
```

The existing nine JavaScript regression suites and 37 Python tests also pass.
Unity evidence and the final compiled build summary are in `Unity/`.

Unity compiled with zero errors/warnings and the existing Editor verifier passes
all six perfect-manual, automatic and wrong-item transaction cases. Actual Play
mode confirms the chicken menu button starts food plating, perfect service pays
18+11=29 exactly once, automatic service pays 18+8=26, and old saves/Continue and
closing remain compatible. Both language menus and the fourth food row were
visually checked. Unity CLI key injection reported delivery but produced no
observable Input System press in the harness, so that specific injected-key
check is unverified (`Unity/input-limitation.json`). The digit-6 branch follows
the existing digit-1..5 path; native Chromium keyboard-6 was exercised directly.

LTE benchmarks use fresh Chromium mobile contexts at 390×844, 10 Mbps, 70 ms
latency and 4× CPU slowdown. These are synthetic measurements rather than a
physical LTE network guarantee. Local results are retained here; public checks
after deployment are retained in `Library/ChickenMenuPublicQA`.

Reference WebGL build `build_8b8509fa0812` succeeded in 279,746 ms with zero
errors and 18 existing warnings. Its four artifact hashes are recorded in
`Unity/build-final-summary.json`; the default native and compiled fallback are
packaged together.

Local LTE cold: 2,490.3 / 2,519.8 / 2,877.9 ms. Warm: 401.4 / 430.4 / 568.0 ms.
All six runs pass 3,000 ms with a real ready engine, no script/resource errors,
and the order atlas loaded before Ready. All three actual Start controls open
planning. The temporary server and browsers were closed after measurement.
