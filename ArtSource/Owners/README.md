# Owner selection illustrations

## Current portraits: user-supplied original

The title uses the user's `owner_p.png`, retained byte-for-byte as
[`Original-owner_p.png`](Original-owner_p.png). Jessy is the woman at the top,
Edison is the man with headphones and a laptop at the right, and Jaiden is the
man holding a record at the lower left. Names and buttons remain live UI.

`Tools/prepare_original_owners.py` separates the three people with explicit
crop polygons, removes the surrounding printed names/signature, and fits each
complete bust into the existing 400×350 portrait slot using nearest-neighbor
scaling. Hair, hands, headphones, laptop and record are kept inside their
respective cards. The supplied sheet and full-resolution approved paper crops
in `OriginalCrops/` remain unchanged.

The ivory paper behind each figure is replaced with that owner's former
live-house setting: Jessy's magenta record shelves and band set, Edison's cyan
stage and amplifiers, and Jaiden's amber record shelves and band set. Built-in
ImageGen restored the background-only rooms from the retained V2 portraits;
the selected 1341×1173 plates and exact prompts are documented in
[`Backdrops/README.md`](Backdrops/README.md). This uses the built-in ImageGen
tool, not the CLI/API fallback. Prompt inputs are the three retained
`ArtSource/PixelArt/V2/Owner*.png` portraits, with the former foreground people
removed from the room plates.
These generated backgrounds contain no replacement foreground person.

Foreground matting is mechanical: remove only border-connected ivory paper
and two reviewed empty openings between Jessy's hair outlines. Enclosed eye,
skin, headphone, laptop and white-shirt colors stay. The masks are retained in
`ForegroundMasks/`. Every retained character RGB pixel is copied exactly from
the approved original nearest-neighbor crop; no face, clothes or pose is
regenerated or recolored. The background is sampled at 200×175 and enlarged
with nearest-neighbor to keep its pixel spacing consistent behind the figure.
The 400×350 cards are lossless PNGs, with no palette reduction of the figure.
Source hashes, masks, paper-opening seeds, retained-pixel counts and runtime
mapping are recorded in [`crops.json`](crops.json).

| Character | Existing save ID | Native image | Unity image |
|---|---|---|---|
| 제시 / Jessy | `female` | `owner-female.png` | `OwnerFemale.png` |
| 에디슨 / Edison | `ff` | `owner-ff.png` | `OwnerFF.png` |
| 제이든 / Jaiden | `pet-sounds` | `owner-pet.png` | `OwnerPetSounds.png` |

`prepare_pixel_art.py` applies these crops last, and the Unity resource sync
preserves the same decoded portrait pixels and existing importer GUIDs.

## Earlier portrait production history

The following generated portraits are historical and are superseded by the
supplied original above.

These portraits are project-bound raster assets for the three-owner title screen.
The game renders names, selection highlights and buttons as native Unity UI.
All illustrations use the built-in `image_gen` tool; the selected files are
copied unchanged into `Assets/Art/StartupTextures/Owners/` and their generated
originals are retained at the default output location.

## Original woman owner

- Runtime asset: `Assets/Art/StartupTextures/Owners/OwnerFemale.png`.
- Source size: 1122 × 1402 pixels; the title crops the image without stretching.
- Original: `C:/Users/Jtsgr/.codex/generated_images/01a15d64-7e98-7de3-b801-d7f23d06a304/exec-f5055d55-99dd-44d9-804e-45c675c7ac9e.png`.
- An original fictional adult character; no real person's likeness is used.

Final prompt:

> Use case: stylized-concept. Asset type: portrait card illustration for a Seoul live music pub simulator's three-owner character selection title screen. Create ONE original adult Korean woman pub owner, approximately 35 years old, confident warm smile, shoulder-length black hair with a subtle magenta highlight, black leather jacket over a simple burgundy shirt, holding one vinyl record sleeve against her waist, facing camera in a relaxed commanding stance. Medium waist-up portrait, vertical 4:5 composition, face large and unobstructed in upper third, head fully inside frame with generous 8 percent top margin; hands visible naturally lower down. Style: Grand Theft Auto loading-screen-inspired hand-painted comic poster illustration, heavy clean black ink outlines, angular cel-shaded painted skin, bold graphic shadow shapes, richly illustrated rather than photorealistic or 3D, gritty screenprint texture, polished premium game key art. Backdrop: a dim Seoul rock pub with blurred vinyl shelves and pink neon stage lights, secondary to character. Palette: hot pink rim light, burgundy, charcoal and warm skin. No text, no logos, no lettering, no watermark, no panel borders. Entire single illustration full bleed, one person only. She is an entirely fictional original woman; do not resemble a celebrity. Neutral music-business portrayal.

## FF owner

- Runtime asset: `Assets/Art/StartupTextures/Owners/OwnerFF.png`.
- Source size: 1122 × 1402 pixels.
- Original: `C:/Users/Jtsgr/.codex/generated_images/01a15d64-7e98-7de3-b801-d7f23d06a304/exec-dce6c997-ef14-4ae2-a587-dccff3fcda05.png`.
- Face reference: the user's attached `스크린샷 2026-10-25 124427.png`, explicitly selected by the user for the FF owner. The source photo is retained locally and is not copied into the repository.
- The illustration uses the supplied uncovered face, fringe, facial proportions and moustache. This replaces the previous masked portrait.
- Medium waist-up composition, cyan club lighting, bold outlines and flat comic shading. Existing resource path and importer GUID are retained.

Final prompt:

> Use case: stylized-concept. Asset type: ONE replacement portrait card for the FF owner in a Seoul live music pub game. Input image is the USER-SPECIFIED FACE REFERENCE. Transform the single foreground man's likeness into bold Grand Theft Auto loading-screen cartoon key art. Preserve this actual adult man's distinctive broad rounded face, full cheeks, small relaxed eyes and eyelids, eyebrow shape, broad nose, slightly parted lips, short dark moustache and light chin stubble, and thick short straight black hair with a heavy uneven fringe across the forehead. Keep his facial proportions and mature character. Do not substitute a generic handsome young man. His entire unobstructed face must be visible; no mask, no sunglasses. Compensate for the reference's orange nightclub lighting with natural warm skin tones while retaining the facial features. Medium waist-up, man looking toward viewer with the same relaxed slightly serious expression, face large in upper third, head fully inside frame with 8 percent top margin. Black tee under an open dark denim overshirt, DJ headphones resting around his neck without covering chin. One relaxed hand near the headphones, other hand down. STYLE: clearly drawn graphic cartoon, very bold clean black contour lines, sharply separated flat cel-shaded color blocks, angular shadow shapes, limited tones per surface, stylized strong comic facial structure, light paper grain only. Make it recognizably GTA loading-screen illustration, not a photo filter, not a photorealistic painting, not a 3D render, no tiny skin-pore detail, no anime. Backdrop: simplified dim Hongdae live rock pub, cyan-blue stage lights, speaker and metal grille as graphic silhouettes. Palette: vivid cyan edge light, deep teal, black ink and warm skin. Vertical 4:5 portrait full bleed, one person only. No text, no lettering, no logo, no watermark, no frame.

## Pet Sounds owner

- Runtime asset: `Assets/Art/StartupTextures/Owners/OwnerPetSounds.png`.
- Source size: 1122 × 1402 pixels.
- Original: `C:/Users/Jtsgr/.codex/generated_images/01a15d64-7e98-7de3-b801-d7f23d06a304/exec-cd6bfc00-cda6-498b-a1ef-e260909ff5da.png`.
- Face reference: [Jazz Avenue, 부기우기 대표 황순재 interview](https://www.youtube.com/watch?v=8r510wNulKA), January 5, 2022. The title and description identify the sole interview subject as 황순재.
- [Public interview thumbnail](https://i.ytimg.com/vi/8r510wNulKA/maxresdefault.jpg).
- Pet Sounds ownership is independently described in [Genie / Biscuit Sound's Boogie Woogie interview](https://www.genie.co.kr/magazine/subMain?ctid=16&mgz_seq=16275).
- Medium waist-up composition, amber club lighting, bold outlines and flat comic shading. Existing resource path and importer GUID are retained.

Final prompt:

> Use case: stylized-concept. Asset type: ONE replacement portrait card for the Pet Sounds owner in a Seoul live music pub game, matching a bold Grand Theft Auto loading-screen cartoon title. Input image is the FACE REFERENCE: depict ONLY the single adult man at left of the interview thumbnail; discard all text, map graphics and layout. Preserve his distinctive facial proportions, broad round face, smiling eyes, nose, lifted cheeks, friendly smile, light facial stubble, and short black hair swept upward with closely cut sides. Do not turn him into a generic young model. Medium waist-up, turned just slightly three-quarter toward viewer, with his friendly interview expression, face large in upper third, head fully inside frame with 8 percent top margin. Keep off-white tee with simplified abstract dark ink marks but no letters, holding a plain vinyl sleeve naturally at waist level. Entire face visible. STYLE: clearly drawn graphic cartoon, very bold clean black contour lines, sharply separated flat cel-shaded color blocks, angular shadow shapes, limited tones per surface, stylized strong comic facial structure, light paper grain only. Make it recognizably GTA loading-screen illustration, not a photo filter, not a photorealistic painting, not a 3D render, no tiny skin-pore detail, no anime. Backdrop: simplified dim Gyeongnidan live music bar, vinyl shelves and warm amber pendant lights drawn as graphic shapes, stage silhouette secondary. Palette: amber-gold rim light, ochre, charcoal, black ink, off-white and natural warm skin. Vertical 4:5 portrait full bleed, one person only. No text, no lettering, no logos, no watermark, no frame.

## Production notes

The two male portraits were refined again on October 26, 2026, keeping their facial contours and expressions while simplifying the shading and strengthening the GTA-style ink outlines. The exact edit prompts and selected outputs are in [GTA-20261026.md](GTA-20261026.md).

All three selected illustrations were generated with the built-in image generation tool. The two male portraits use photo references; the fictional woman uses no reference. The current male portraits use the stronger GTA loading-screen cartoon treatment requested on October 25, 2026. Reference photos are not distributed in the game. The illustrated selection changes player identity only.
