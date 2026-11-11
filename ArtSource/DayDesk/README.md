# Day-desk owner scenes

The current production scenes use the separate owner-specific environments in
[V2](V2/README.md), based on each owner's approved room illustration. The
original shared-office sources and provenance below are retained as history;
`manifest.json` points to the current V2 sources and runtime assets.

These three production scenes were generated with the built-in
`image_gen.imagegen` tool. The current approved owner portraits were the
authoritative identity references. Jessy established the daytime office;
Edison and Jaiden were separate identity-preserving edits of that same scene,
using their own approved portrait as the second reference. Only one owner
appears in each scene. No selector asset was changed.

Each `desk-*.source.png` is an unchanged1672×941 tool output copied from the
generated-image library. Its exact prompt is in the matching `desk-*.prompt.md`.
`manifest.json` records the original generated path, input reference hashes,
source/runtime hashes, measured geometry and visual identity checks.

Runtime assets are:

- `Tools/WebGLTemplate/FastGame/art/office/desk-jessy.png`
- `Tools/WebGLTemplate/FastGame/art/office/desk-edison.png`
- `Tools/WebGLTemplate/FastGame/art/office/desk-jaiden.png`

They are640×360 optimized RGB PNGs, exported by complete-source nearest-neighbor
downsampling. There is no crop, repaint, recolor or palette quantization. The
combined runtime size is1,240,386 bytes; only the selected owner's scene should
load after Start. These images are not part of the initial owner-selector load.

The blank desktop monitor remains on the left, keyboard and working hands in
the center, and blank face-up phone on the right. Measured monitor edges match
across all three scenes; phone edges vary by at most one runtime pixel. The
shared screen quads, safe inner quads, axis-aligned monitor rectangle and phone
hotspot are in `manifest.json`, both in pixels and normalized coordinates. The
screen geometry is independent of the selected owner.

Visual review confirmed Jessy's purple/copper curls, gold hoops, leather and
burgundy outfit and pink nails; Edison's bowl fringe, moustache, headphones and
denim; Jaiden's shaved undercut, swept hair, smiling eyes and white shirt. The
root task reviewed and approved all three runtime images before their freeze.

Jessy's current scene uses the later [chibi revision](JessyChibi/README.md), with
a compact torso and complete upper hair. The original three generated sources
remain archived; `manifest.json` points to the current Jessy revision while
retaining the shared screen geometry and unchanged Edison/Jaiden entries.
