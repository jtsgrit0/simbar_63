# Owner refinements verification

[`audit.json`](audit.json) and [`comparison.png`](comparison.png) verify:

- The supplied original sheet and three full-size approved crops stay
  byte-for-byte unchanged.
- Jessy's visible working pixels come directly from the reviewed edited source
  through nearest-neighbor sampling. Her configured scale is 115%, rotation is
  8° counterclockwise, and no auto-fit step reduces that enlargement. Her pink
  nails remain visible after the approved lower-body crop.
- Edison retains the original figure RGB and positions, with a separate black
  DJ support behind the laptop/arms and touching the image's bottom edge.
- Jaiden retains the original RGB and shape and moves down 11px to the bottom.
- All three outside-only light masks have opacity 0.5, peak alpha 128, and do not
  color opaque pixels inside the figure. The final cards match the stored
  background, figure and lighting layers exactly.

Run `uv run --with pillow python Research/PixelArtVerification/OwnerRefinements/audit_sources.py`
from the repository root. The audit records 619,666 runtime bytes in total.
The packer was rerun with all three runtime PNG hashes remaining identical.

Sources, exact built-in ImageGen prompts and framing details:
[`ArtSource/Owners/Refinements`](../../../ArtSource/Owners/Refinements/README.md).
