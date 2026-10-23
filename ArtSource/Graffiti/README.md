# Floor spray graffiti

[Spray splatter accents](Splatter.md) add irregular artist-colored paint droplets around the lettering.

Five original artist-name spray-paint textures generated using the built-in `image_gen` tool on 2026-10-24. Original handwritten lettering, paint specks, overspray and worn gaps are stored on a genuinely transparent background. The source images are copied unmodified; the default generated originals are preserved.

## Saved assets and pigments

| Artist | Saved PNG | Floor pigment |
| --- | --- | --- |
| Queen | `Assets/Resources/SimBarArt/Graffiti/Queen.png` | Gold |
| Nirvana | `Assets/Resources/SimBarArt/Graffiti/Nirvana.png` | Orange, tinted from the generated mustard pigment |
| The Beatles | `Assets/Resources/SimBarArt/Graffiti/TheBeatles.png` | Mint turquoise |
| Taylor Swift | `Assets/Resources/SimBarArt/Graffiti/TaylorSwift.png` | Lavender / rose |
| Blackpink | `Assets/Resources/SimBarArt/Graffiti/Blackpink.png` | Hot pink |

Unity imports each image at a 512-pixel maximum with NPOT proportions preserved, input alpha, transparent-edge filtering, clamped edges and mipmaps. No image pixels are edited. An explicit transparent URP Unlit Resources material retains the shader variant in the web build. Floor planes sit about 1 cm above the tile surface, fit the source proportions, use no colliders, and are staggered across open floor areas. The texture alpha reveals tiles through the overspray; the material uses 0.88 opacity and a warm orange tint for Nirvana to distinguish it from Queen.

## Final generation prompts

### Queen

Use case: stylized-concept. Asset type: transparent spray-painted word decal texture for a 3D London rock pub floor. Primary request: exact clearly readable text "QUEEN", using large expressive hand-sprayed uppercase strokes with a small simple crown scribble. Colors: warm royal golden yellow. Wide horizontal 3:1 composition, entire name inside a generous 12 percent transparent margin. Original street graffiti lettering made from aerosol paint, with soft overspray haze, scattered paint specks, tiny drips, uneven worn paint coverage and a few dry gaps inside thick strokes. The large name must stay completely legible from above at small game scale. Flat orthographic artwork only, no perspective. Genuinely transparent background outside the colored letters and spray specks; no floor, wall, paper, rectangle, black backdrop or white background. No people, face, portraits, album artwork, official logo, tour dates, extra words, small text or watermark. Do not imitate an existing artist logo. Use bold medium-light pigment so the colored letters work on a cream tile floor. This is a graphic painted name, not a scene or mockup.

### Nirvana

Use case: stylized-concept. Asset type: transparent spray-painted word decal texture for a 3D London rock pub floor. Primary request: exact clearly readable text "NIRVANA", using rough condensed punk stencil capitals, loosely hand cut, with lightly chipped ink. Colors: acid mustard yellow with a few warm orange flecks. Wide horizontal 3:1 composition, entire name inside a generous 12 percent transparent margin. Original street graffiti lettering made from aerosol paint, with soft overspray haze, scattered paint specks, tiny drips, uneven worn paint coverage and a few dry gaps inside thick strokes. The large name must stay completely legible from above at small game scale. Flat orthographic artwork only, no perspective. Genuinely transparent background outside the colored letters and spray specks; no floor, wall, paper, rectangle, black backdrop or white background. No people, face, portraits, album artwork, official logo, tour dates, extra words, small text or watermark. Do not imitate an existing artist logo. Use bold medium-light pigment so the colored letters work on a cream tile floor. This is a graphic painted name, not a scene or mockup.

### TheBeatles

Use case: stylized-concept. Asset type: transparent spray-painted word decal texture for a 3D London rock pub floor. Primary request: exact clearly readable text "THE BEATLES", using loose energetic broad uppercase handwritten spray lettering; THE smaller above BEATLES. Colors: mint turquoise and pale seafoam green. Wide horizontal 3:1 composition, entire name inside a generous 12 percent transparent margin. Original street graffiti lettering made from aerosol paint, with soft overspray haze, scattered paint specks, tiny drips, uneven worn paint coverage and a few dry gaps inside thick strokes. The large name must stay completely legible from above at small game scale. Flat orthographic artwork only, no perspective. Genuinely transparent background outside the colored letters and spray specks; no floor, wall, paper, rectangle, black backdrop or white background. No people, face, portraits, album artwork, official logo, tour dates, extra words, small text or watermark. Do not imitate an existing artist logo. Use bold medium-light pigment so the colored letters work on a cream tile floor. This is a graphic painted name, not a scene or mockup.

### TaylorSwift

Use case: stylized-concept. Asset type: transparent spray-painted word decal texture for a 3D London rock pub floor. Primary request: exact clearly readable text "TAYLOR SWIFT", using fluid hand sprayed slanted uppercase lettering with a few star-shaped spray accents. Colors: pale lavender and touches of dusty rose pink. Wide horizontal 3:1 composition, entire name inside a generous 12 percent transparent margin. Original street graffiti lettering made from aerosol paint, with soft overspray haze, scattered paint specks, tiny drips, uneven worn paint coverage and a few dry gaps inside thick strokes. The large name must stay completely legible from above at small game scale. Flat orthographic artwork only, no perspective. Genuinely transparent background outside the colored letters and spray specks; no floor, wall, paper, rectangle, black backdrop or white background. No people, face, portraits, album artwork, official logo, tour dates, extra words, small text or watermark. Do not imitate an existing artist logo. Use bold medium-light pigment so the colored letters work on a cream tile floor. This is a graphic painted name, not a scene or mockup.

### Blackpink

Use case: stylized-concept. Asset type: transparent spray-painted word decal texture for a 3D London rock pub floor. Primary request: exact clearly readable text "BLACKPINK", using bold sharp original handwritten uppercase spray lettering with a few energetic underline strokes. Colors: vivid hot pink with pale pink highlight strokes. Wide horizontal 3:1 composition, entire name inside a generous 12 percent transparent margin. Original street graffiti lettering made from aerosol paint, with soft overspray haze, scattered paint specks, tiny drips, uneven worn paint coverage and a few dry gaps inside thick strokes. The large name must stay completely legible from above at small game scale. Flat orthographic artwork only, no perspective. Genuinely transparent background outside the colored letters and spray specks; no floor, wall, paper, rectangle, black backdrop or white background. No people, face, portraits, album artwork, official logo, tour dates, extra words, small text or watermark. Do not imitate an existing artist logo. Use bold medium-light pigment so the colored letters work on a cream tile floor. This is a graphic painted name, not a scene or mockup.
