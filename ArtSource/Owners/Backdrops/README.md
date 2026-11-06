# Restored owner-card room plates

These background-only images restore the record shelves and band sets that
were behind the earlier owner portraits. They were edited using the built-in
ImageGen tool, not the CLI/API fallback. All selected outputs are 1341×1173px,
opaque room plates; foreground people are removed. Exact edit prompts are
retained beside each output.

| Owner | Input reference | Saved output | Exact prompt |
|---|---|---|---|
| Jessy | `ArtSource/PixelArt/V2/OwnerFemale.png` | [Jessy.png](Jessy.png) | [Jessy.prompt.md](Jessy.prompt.md) |
| Edison | `ArtSource/PixelArt/V2/OwnerFF.png` | [Edison.png](Edison.png) | [Edison.prompt.md](Edison.prompt.md) |
| Jaiden | `ArtSource/PixelArt/V2/OwnerPetSounds.png` | [Jaiden.png](Jaiden.png) | [Jaiden.prompt.md](Jaiden.prompt.md) |

The approved new owner figures are not ImageGen outputs. The supplied original
sheet and approved paper crops stay unchanged. `Tools/prepare_original_owners.py`
copies their retained RGB pixels over these room plates using technical paper
matting. The room is sampled to 200×175 and enlarged with nearest-neighbor to
400×350; no palette reduction or color change is applied to the figures.

The three runtime files remain `owner-female.png`, `owner-ff.png` and
`owner-pet.png` under `Tools/WebGLTemplate/FastGame/art/`. No additional runtime
image request is needed. Source/mask hashes and retained-pixel counts are in
[`../crops.json`](../crops.json); independent original-file and figure-RGB
verification is in
[`../../../Research/PixelArtVerification/OwnerCardBackdrops/owner-pixel-audit.json`](../../../Research/PixelArtVerification/OwnerCardBackdrops/owner-pixel-audit.json).
