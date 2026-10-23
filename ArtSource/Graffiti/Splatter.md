# Spray splatter accents

Saved asset: `Assets/Resources/SimBarArt/Graffiti/SpraySplatter.png`.

Generated and refined with the built-in `image_gen` tool on 2026-10-24. The final transparent PNG was copied into the project without pixel editing; both default generation outputs remain preserved. Unity imports the stamp at 512px with input alpha, transparent-edge filtering, mipmaps and clamped wrapping. A shared white-pigment stamp is tinted to the five artist colors.

Each title has an uneven set of differently sized, rotated and mirrored splatter stamps, including loose tails and sparse satellite droplets. Independent seeded randomness controls their non-grid positions, with title-center and furniture/room-boundary rejection. Five batched meshes keep the renderer cost low; there are no colliders or per-frame updates.

The sparse pigment coverage needs wider physical stamps to remain visible in the full game view: large impacts span 1.3–1.75m, medium clusters 0.65–1.15m, and satellite clusters 0.4–0.75m. Most of each stamp remains transparent. Pigment opacity is 0.86; the title layer draws above the droplets.

## Initial generation prompt

Use case: stylized-concept. Asset type: reusable white aerosol-paint splatter stamp texture for a stylized 3D rock-pub floor. One square transparent image containing a sparse irregular asymmetric burst of bright neutral WHITE spray-paint droplets, a few medium ragged paint impacts, many distinct small round irregular flecks and a very light mist of tiny satellite specks. A believable quick accidental sideways aerosol splat: clustered on one side with an uneven loose tail, generous empty gaps between drops, no uniform radial symmetry. Crisp white pigment cores with grainy porous edges, some translucent fine overspray; strongest droplets visibly opaque. The paint marks should occupy only about 15 to 25 percent of the image area; most pixels remain fully transparent. Keep every visible mark inside a 10 percent transparent border so stamps never reveal square edges. Flat directly overhead graphic, no perspective. White pigment only: no colored paint, no black or grey backdrop, no floor, wall, paper, solid filled rectangular cloud, huge paint puddle, text, letters, icons, grid, frame, shadow or watermark. Genuinely transparent background throughout the negative spaces. Designed to be tinted gold/orange/mint/lavender/pink inside a game and scattered at different sizes and rotations around existing graffiti titles.

## Final refinement prompt

The first generated image is the edit target; all other project images stay unchanged.

Edit this transparent spray texture. Change only the paint distribution: remove the entire large connected cloud/puddle shape and replace it with separated white aerosol droplets and tiny flecks. Keep the transparent background and neutral white pigment. The final image must contain about 35 to 70 individual irregular paint dots, each isolated from neighboring dots by clear transparent space; most dots are very small, 3 to 12 pixels at this image scale, with only 4 or 5 medium rough impacts no larger than 35 pixels across. Arrange these isolated marks in an uneven off-center cluster and a loose tail, with large empty gaps, not a grid, not a ring, not a symmetric radial pattern. No fog, no large smear, no connected filled paint mass, no backdrop, no scene, no shadow, no text. Preserve a transparent 10 percent outer margin. Aim for 90 percent or more fully transparent pixels, with opaque white dot centers and lightly speckled edges.
