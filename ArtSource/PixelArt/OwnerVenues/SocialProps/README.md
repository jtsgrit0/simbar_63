# Social venue props

The built-in ImageGen tool created the retained sources in this folder. The
existing Freebird/FF plates and pub table served as pixel-style and isometric
camera references. [prompts.json](prompts.json) retains the three original
prompts; [FreebirdArch.v2.prompt.txt](FreebirdArch.v2.prompt.txt) records the
targeted dark-passage correction. Both arch variants are retained.

`Tools/prepare_social_venue_props.py` crops only to the source alpha contour,
keeps a small original edge margin, scales proportionally with nearest-neighbor
sampling and encodes a palette PNG. It does not paint or synthesize content.
The selected sources and exported files are:

| Source | Native runtime image | Dimensions |
|---|---|---|
| FreebirdArch.v2.source.png | freebird-arch.png | 76×130 |
| FreebirdWallTable.source.png | freebird-wall-table.png | 142×158 |
| CocktailGlass.source.png | cocktail-glass.png | 12×16 |

The images are exported under `Tools/WebGLTemplate/FastGame/art`. The actual
measured sizes, source/runtime hashes and transparent-margin validation are in
[packing-report.json](packing-report.json).

The Freebird arch frame sits between the shortened bar and stage at world
(-1.12,4.7), with a walkable arrival point (-1.12,3.6) in front. It is drawn
behind live actors, with no solid collider across the open passage. The long
table follows the left wall at (-8.95,-1.2), with six independent stool props
and matching waiting seat anchors. Each seat is walkable so seated guests can
stand and depart naturally. The table itself blocks navigation within
x[-9.4,-8.75], z[-4.4,2], and Freebird layout version is now 3.

FF uses ten standing audience anchors, two behind each existing round table.
These keep clear of stool circles and the order queue. Two cocktail-glass props
are drawn atop each table. Their depth offset makes them render after the
tabletop. They do not add navigation obstacles beyond the tables themselves.

All six waiting seats and ten FF standing anchors were checked as walkable,
outside the protected mosh/surf zone, with valid paths into the clear floor.
Base venue plates and the original Jaiden presentation are unchanged.
