# Standing bartender and service patrol

The native bartender now walks between x=-8.95 and x=-7.05 in the authored
staff aisle z=-0.565, using the existing Unity 9-second cycle and 0.66m/s speed.
Each half-cycle includes a short stationary service interval. Pour preparation
uses the existing Unity service target override. This movement does not enqueue,
serve, remove or price orders, and consumes no gameplay random values.

While stationary behind the counter, the bartender uses the full Suit standing
frame instead of the shorter serving pose. During movement, the existing Suit
wardrobe/walk frames alternate. Other floor-service and cleanup poses remain.
The bar employee calibration is raised from [0,25] to [0,17]: the upper torso
and forearms read as standing, while the actual counter still covers the legs.
The eleven seat anchors, sofa/stool masks and drum-kit calibration are unchanged.

`offset-25-bar.png`, `offset-19-bar.png` and `offset-17-bar.png` compare the
same real actor/time with three presentation offsets. The approved value is 17.
`left-bar.png` and `right-bar.png` are final actual normal-game patrol captures.
`visual.json` records their actual actor states and pixel-identical pause.

`Tools/test_native_bartender.mjs` verifies both walking directions, stationary
work phases, continuous bounded movement, the physical aisle, pause, exact saved
positions/resume, unchanged RNG/orders/economy and completed closing settlement.
The existing pixel-presentation suite, after-show suite and all five actual saved
after-show fixtures passed. `seed-sweep.json` records 20 complete normal seeded
nights; all reached Results with all fans/staff home and no pending missions.
