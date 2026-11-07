# Owner pose, framing and pixel lighting

Jessy's current presentation uses the later user-supplied complete illustration
documented in [`../Supplied/README.md`](../Supplied/README.md). Its `cardSource`
override bypasses the older Jessy pose, rotation, scale, matte and lighting below.
Those earlier assets remain archived; Edison and Jaiden still use the reviewed
configuration described here.

The original supplied sheet and the three approved full-size paper crops are
unchanged. This follow-up changes the presentation requested for each owner;
it does not replace the restored live-house backgrounds.

| Asset | Purpose | Authoring |
|---|---|---|
| `Jessy.reference.png` | Original foreground supplied to the edit | Exact original pixels and reviewed paper matte |
| [Jessy.left-wrist.source.png](Jessy.left-wrist.source.png) | Raised left wrist and visible pink nails | Built-in ImageGen edit; [exact prompt](Jessy.left-wrist.prompt.md) |
| `Edison.reference.png` | Original figure reference | Exact original pixels and reviewed paper matte |
| [Edison.dj-box.source.png](Edison.dj-box.source.png) | Black DJ controller below the laptop | Built-in ImageGen support asset; [exact prompt](Edison.dj-box.prompt.md) |

The generated source PNGs are retained unchanged. Their almost invisible
alpha 1–7 residue is ignored only in the working presentation; alpha 8–255 and
RGB values are retained. The threshold is recorded in `presentation.json` and
the runtime manifest. No figure palette reduction or smoothing is applied.

`Tools/prepare_original_owners.py` reads [`presentation.json`](presentation.json):

- Jessy uses the reviewed pose edit, an 8° counterclockwise correction, and a
  literal 115% nearest-neighbor scale. The visible transformed figure is 348×392
  pixels, placed at (26,2) in the 400×350 card. The lower 44px are clipped by the
  card edge; hair and the raised pink-nailed hand remain visible. It is not
  fitted back down after scaling.
- Edison keeps all original figure RGB and positions. The separately generated
  black DJ support occupies (18,289)–(380,350) behind the laptop and arms, so the
  bottom gap is filled without redrawing the person.
- Jaiden keeps his original RGB and shape and moves down 11px. The character
  bounds now end exactly at the bottom of the 350px image; the room stays fixed.

All three figures have a separate 2px exterior rim in their room's magenta,
cyan or amber. The opacity is 0.5, stored as alpha 128 where fully covered. It is
a crisp pixel ring, not a blurred glow; opaque character pixels are unchanged
by the light layer. The generated Jessy pose itself is an edited source, so its
pixels are not falsely described as identical to the original supplied pose.

`DisplayFigures/` retains the final character and support layers before room
composition. `DisplayMasks/` and `LightMasks/` retain the actual silhouette and
lighting coverage. File hashes, transforms and provenance are recorded in
[`../crops.json`](../crops.json).

Independent checks and the before/after sheet are in
[`OwnerRefinements`](../../../Research/PixelArtVerification/OwnerRefinements/).
The three runtime cards total 619,666B, only 768B more than the previous release.
Repacking is byte-identical.
