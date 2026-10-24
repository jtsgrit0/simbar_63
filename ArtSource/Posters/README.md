# Music wall collage

The collage also uses a [worn gig-paper texture](PaperWear.md) for creases, faded photocopy ink and glue stains.

Six original illustrated music posters generated with the built-in `image_gen` tool on 2026-10-24. They use artist-name typography and original instrument/concert graphics rather than portraits, official logos or reproduced album covers.

## Saved artwork textures

| Artist / design | Asset | Current use |
| --- | --- | --- |
| Nirvana | `Assets/Resources/SimBarArt/Posters/Nirvana.png` | Pasted papers on both walls |
| The Beatles | `Assets/Resources/SimBarArt/Posters/TheBeatles.png` | Pasted papers on both walls |
| BLACKPINK | `Assets/Resources/SimBarArt/Posters/Blackpink.png` | Pasted papers on both walls |
| Queen | `Assets/Resources/SimBarArt/Posters/Queen.png` | Pasted papers on both walls |
| POP NIGHT | `Assets/Resources/SimBarArt/Posters/PopNight.png` | Pasted papers on both walls |
| Taylor Swift | `Assets/Resources/SimBarArt/Posters/TaylorSwift.png` | Pasted papers on both walls |

PNG originals are copied into Resources without editing; the generated originals remain in the image-generation output directory. Unity imports them with a 512-pixel maximum, mipmaps, preserved non-power-of-two proportions and clamped edges. RGB textures use about 5.2 MB of GPU memory for all six textures, including mipmaps. Opaque Unlit materials keep the paper artwork visible under changing stage lights; the Resources material template retains that shader variant in web builds.

The current treatment is a grungy London pub flyposter collage adapted to the charcoal knee walls and casement windows: eight papers on the rear wall and eight in compact clusters at the lower front-left windows. All six artwork textures appear on both walls. Whole assemblies are uniformly scaled to 48–60 percent, including their borders and masking tape, and overlap at different heights and angles. Torn mesh edges, old paper remnants, small gig flyers and aged masking tape make the arrangement look pasted over time. Rotated paper and tape stay below 2.35 m so the [Seoul neon street view](../Seoul/README.md) remains clear above them. Artwork proportions remain intact; the irregular paper outlines slightly trim the outer print edges. There are no poster frames or poster colliders.

The three legacy rear-wall frame/art assemblies were removed from the authoring Blender scene and exported venue FBX. The later [Seoul envelope patch](../Blender/SEOUL_ENVELOPE.md) replaces only the floor and brick-wall meshes with continuous gray concrete and lower charcoal walls; all eight remaining mesh groups, including plants and sconces, retain identical geometry/material/transform hashes. See also [the original wall-art patch notes](../Blender/WALL_ART.md).

Separate [floor graffiti assets and prompts](../Graffiti/README.md) provide artist-name spray lettering across the continuous gray floor: gold for Queen, orange for Nirvana, mint for The Beatles, lavender for Taylor Swift and pink for BLACKPINK, with irregular surrounding spray droplets.

## Final generation prompts

### Queen

Create one original decorative rock music poster for a 3D pub game's brick wall. Portrait 5:6 aspect ratio, flat edge-to-edge print artwork, no frame, no wall, no perspective. Large exact title text "QUEEN" across the top in bold original cream and gold lettering. Below the title, an original expressive illustration of a freestanding vintage microphone and microphone stand, an electric guitar, a small simple golden crown and energetic abstract stage-light rays. Purple-black and warm gold screen-print palette, strong shapes and restrained aged ink grain. No people or faces. No existing band logo, no album-cover imitation, no tour dates, no extra words, no tiny copy, no watermark. Keep the title and instrument graphics inside an 8 percent safe margin. Designed to read beautifully when viewed as a small framed poster in a stylized music venue.

### Nirvana

Create one original decorative rock music poster for a stylized 3D pub game. Flat portrait 5:6 rectangular print artwork, edge-to-edge, no wall, no frame, no perspective. Exact large readable title "NIRVANA" in bold original ivory block lettering along the top. An expressive distressed screen-print illustration of a cream electric guitar leaning against a black amplifier with curling cable, abstract energetic soundwave shapes and deep rust/ochre light rays. Charcoal black, mustard yellow, warm cream and muted burnt orange. Strong graphic silhouette, beautiful rough ink grain. No people, faces, smiley-face logo, existing band logos, album-cover imitation, tour dates, extra text, watermark or tiny copy. Keep title and graphics inside an 8 percent safe margin. Distinctive original grunge poster readable at small game scale.

### TheBeatles

Create one original decorative classic pop-rock music poster for a stylized 3D live-music pub game. A flat portrait 5:6 rectangular print, edge-to-edge, no wall, frame, perspective, or mockup. Exact title "THE BEATLES" across the upper quarter in large dark navy original retro lettering, highly readable. Below, arrange four different vintage band instruments with clear spacing: a violin-shaped bass guitar, two electric guitars and a compact drum kit, with playful mid-century stars and broad geometric stage rays. Elegant retro screen-print illustration with muted cream, teal, navy, and mustard colors; only restrained paper grain, strong uncluttered silhouettes. No people or faces, no official logo, no album-cover recreation, no walking-crosswalk scene, no tour dates, no extra words, watermark or small copy. Keep all key art and text inside an 8 percent safe margin.

### TaylorSwift

Create one original decorative pop music tribute poster for a stylized 3D live-music pub game. A flat edge-to-edge portrait 5:6 rectangular print, no wall, frame, perspective or mockup. Exact large title "TAYLOR SWIFT" on two lines in original elegant bold ivory lettering across the upper quarter, readable at small scale. Below, feature a richly detailed warm wooden acoustic guitar beside a silver vintage microphone with broad lavender and navy concert spotlights, scattered small silver stars and a quiet glitter-like screen-print texture. No people or faces. A beautiful original illustration using lavender, deep indigo, warm cream and touches of rose-gold, sophisticated and uncluttered. No album-cover recreation, official branding, lyrics, tour dates, extra words, watermark or tiny copy. Keep all important art and lettering inside an 8 percent safe margin.

### Blackpink

Create one original decorative K-pop music poster for a stylized 3D pub game. Flat edge-to-edge portrait 5:6 rectangular print artwork only, no wall, no frame, no perspective or mockup. Exact large title "BLACKPINK" across the upper quarter in thick original pale-pink lettering, highly readable. Below, four sleek microphones standing at four different heights with clear spacing, under intersecting vivid pink concert spotlights and bold geometric shards. No people or faces. Modern high-contrast black, hot pink, pale pink and restrained silver screen-print illustration, strong sharp silhouettes, subtle grain and stylish graphic energy. No existing band logo, official branding imitation, album artwork recreation, lyrics, tour dates, additional text, watermark or tiny copy. Keep title and graphic inside an 8 percent safe margin, polished and coherent with original collectible music posters.

### PopNight

Create one original decorative POP NIGHT music poster for a stylized 3D pub game. Portrait 5:6, edge-to-edge flat print artwork, no frame, wall, perspective or mockup. Exact large title "POP NIGHT" in cream white original lettering across the top. Original graphic composition of a silver microphone, disco ball, vinyl record, neon turquoise and coral stage-light arcs on deep midnight blue. Festive retro pop concert atmosphere, bold readable shapes, restrained print grain. No people, faces, celebrity references, artist names, existing logos, album covers, dates, small text or watermark. Keep title and main icons within an 8 percent safe margin. The design should read clearly as a small framed music-venue poster.
