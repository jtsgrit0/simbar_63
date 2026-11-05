# Pixel-art production sources

Generated with the built-in image generation tool on 2026-11-05. Original generated rasters are retained here; the build script only sizes and encodes PNGs. UI remains DOM text and original icons. The room has no baked characters; sprites are independently animated by the game.

## Empty room

Source: `VenueEmpty.png`. Reference: the previously approved pixel-art screenshot `exec-9e1dc673-d01b-48a5-b9ef-447675f0d225.png`.

Use case: precise-object-edit.
Asset type: production empty room plate for a playable 2D isometric pixel-art bar game.
Input image 1 is the APPROVED exact visual style and room layout. Extract/recreate ONLY its detailed isometric room in a landscape 1.72:1 frame, with TRANSPARENT exterior. Remove ALL UI, browser header, buttons, panels, text overlays, minimap and ALL PEOPLE including seated people, bartender, crowd, musicians, shadows of people. Reconstruct the hidden empty surfaces cleanly.
Preserve the approved sophisticated hand-placed pixel-art: muted gray speckled concrete floor with colored music graffiti, richly shaded red leather booths front left and back left, long warm wooden bar and dense multicolor liquor bottle shelves left center, dark gray structural columns and glazed black window frames across the back/left, cyan-magenta outlined right stage, detailed red drumkit cymbals, guitars on stands, amplifiers, monitors, mic stands and black truss. ROCK ON / LIVE sign on right stage. Warm amber candles vs cool cyan-magenta neon. Realistic object scale and detailed tiny pixel clusters, no smooth 3D, no crude block placeholders, no chibi.
Camera/layout must match reference. Empty floor footprint in 960x558 logical coordinates: left corner(50,360), front corner(669,542), right corner(906,282), back corner(286,100). Wooden bar at left middle runs parallel vector(29.5,8.7); stage at back right. Exact orthographic axes: x-axis right/down (29.5,8.7), z-axis right/up (15.25,-16.77), height straight up. Overall room fills the frame, no excessive transparent margin. Walls/windows rise above back edges; outside windows is transparent. Room has cutaway front and right edges, no new enclosing front wall.
Keep the reference room's GREY concrete/graffiti floor and dark grey wall/window architecture. No beige checkerboard tiles and no orange brick walls. Make ALL space exterior to room genuinely alpha transparent; no city painted in, no black backdrop, no checkerboard pattern. No characters whatsoever. Single empty room plate, no sprite sheet, no UI. Output landscape 1720x1000 or matching 1.72 ratio.

## Characters

Source: `CharacterAtlas.png`.

Use case: style-transfer. Asset type: production TRANSPARENT pixel-art character animation atlas for the same isometric pub as reference image. Reference image1 establishes detailed pixel cluster style and ADULT proportions only.
Create ONE regular grid with exactly 4 columns and 8 rows, total32 complete standalone characters. No room, no props outside characters, NO BACKGROUND, no labels, no grid lines. Transparent alpha.
Output1024x2048 so each cell256x256, to be used as64x64 logical sprite cells. In EACH cell, feet align at horizontalcenter128, vertical240. Adult figure occupies height~208px (logical52px), width~70-100px. Human head small, ~1/7 total height, adult natural proportions, never chibi or square oversized head. Isometric3/4 view with slight view from above, matching the pub. Crisp pixel clusters on64px logical grid, rich4-6 tone shading, dark subtle outlines, dark shoes and realistic anatomy.
Rows EXACT from top:
row1 male indie guest, black short hair, olive green casual jacket, black pants.
row2 female indie guest, dark long hair, orange/brown fitted top, dark pants.
row3 bartender, dark hair, cream shirt and black vest/apron, dark pants.
row4 server, dark hair, purple shirt and black apron, dark pants.
row5 female singer, dark hair, pink/purple jacket, dark pants, holding small microphone.
row6 male guitarist, brown hair, denim jacket, dark pants, holding sunburst electric guitar.
row7 male bassist, dark hair, teal shirt, black pants, holding amber bass guitar.
row8 drummer, dark hair, cream shirt, dark trousers, holding two sticks (no drums, drumkit is separate).
Columns EXACT: 1 idle pose facing front-right, 2 walking first step one leg forward, 3 walking opposite step, 4 role action: rows1&2 cheer/dance, row3 hold serving glass towardleft, row4 sweeping a long broom with small bristles atfoot, row5 sing microphone atmouth, row6 strumguitar, row7 pluckbass, row8 arms raiseddrumsticks.
Same person/clothes/scale/center/baseline across each row. Fully separated sprites with at least32px empty margins around; no overlapadjacentcells. Nearest-neighbor pixelart, no blur, no smooth illustration, no gradients, no ground shadow painted into frame. Transparent background. Exactly4columns8rows, zero extra sprites.

## Owners and Seoul

`OwnerFemale.png`, `OwnerFF.png`, `OwnerPetSounds.png` and `SeoulNightDistricts.png` recover the previously generated pixel-art production images. These preserve the approved identities and Seoul Hongdae/Itaewon/Haebangchon night mood. They are separate from UI labels, selection frames and controls.
