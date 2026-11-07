# Supplied Jessy illustration

`Jessy-owner_p_j.PNG` is the unchanged file supplied by the user as
`C:/Users/Jtsgr/OneDrive/Desktop/owner_p_j.PNG`.

- Original: 1087×1447 RGBA; SHA-256
  `a27e380f6743eb2cf3359e2e593cff12f63f5dbc371de112d6e0196924775961`.
- Runtime: source crop `(3,280)–(1083,1225)`, resized from 1080×945 to the existing
  400×350 card using nearest-neighbor sampling. Both sizes have the exact 8:7
  ratio, so the portrait has one uniform scale and fills the card without bars.
- The portrait is about 53% larger than the previous full-height contain. The
  crossed forearms and cuffs meet the lower image border; the supplied record
  shelves and stage occupy the full width. Upper outer curls and the baked 01
  badge fall outside this close-up; the face and crossed hands remain visible.
- The ordinary generated 01/selected badge now uses the same layout as the other
  owners. Edison, Jaiden and all three taglines are unchanged. Sampling selects
  exact source pixels, with no smoothing, recoloring, rotation or new rim.

`Tools/prepare_original_owners.py` reads Jessy's `cardSource` configuration in
`../Refinements/presentation.json` before any previous figure/room processing.
Run `uv run --with pillow python Tools/prepare_original_owners.py` to reproduce.
The old original sheet, crops and earlier refinement sources remain archived.
The earlier complete-image contain is recorded in
`Research/PixelArtVerification/JessySupplied`; current framing checks are in
`Research/PixelArtVerification/OwnerFraming`.
