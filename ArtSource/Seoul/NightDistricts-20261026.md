# Hongdae / Itaewon / Haebangchon night backdrop — 2026-10-26

New production asset generated with the built-in `image_gen` tool, using the existing Seoul panorama as a style/lighting reference and the gameplay screenshot as a layout reference. The result is an imagined blend of the three neighborhoods, not a documentary street view. The generated PNG is copied unchanged into the project.

- Source asset: `Assets/Art/StartupTextures/Seoul/SeoulNightDistricts.png`.
- Generated original: `C:/Users/Jtsgr/.codex/generated_images/01a17266-4037-7981-acdb-b1043e404811/exec-87a8e2c3-2efc-4e8e-9a2a-0114d9bd73d1.png`.
- Compact runtime asset: `Assets/Resources/SimBarWebTextures/SeoulNightDistricts.bytes` (JPEG 94, prepared by the existing build exporter).
- Consumer: `SimBarNightBackdrop`, one camera-facing unlit quad behind the isometric venue, with aspect-preserving center crop. Layer 30 is excluded from the minimap. The existing title art and window panorama keep their current texture.

## Final prompt

```
Use case: stylized-concept.
Asset type: ONE production landscape background texture behind the isometric bar in the SimBar game, replacing the black empty area outside the floating cutaway venue.
Input images: Image 1 is a STYLE / NIGHT-LIGHTING REFERENCE ONLY (existing Seoul street panorama). Image 2 is a LAYOUT REFERENCE ONLY (current gameplay screenshot showing the centered isometric venue, HUD and black surrounding area). Do not reproduce the interface, venue, characters, labels or screenshot.
Primary request: create a cohesive imagined Seoul night-street panorama that naturally blends Hongdae, Itaewon and Haebangchon's distinctive nightlife into ONE continuous environment, not a literal map, collage panels, triptych or divided district cards.
Scene: Hongdae's lively indie live-music club entrances and glowing small commercial signs; Itaewon's eclectic brick bar facades, narrow nightlife lanes and rooftop terraces; Haebangchon's steep hillside alleys, steps and warmly lit low-rise houses climbing toward the Namsan hillside. Blend these architecture/street details gradually and convincingly. A small distant N Seoul Tower silhouette may rise behind the layered rooftops. Korean city atmosphere, no Japanese torii or Japanese lantern streets, no fantasy skyscraper canyon.
Composition: wide 16:9 landscape, preferably 2048 by 1152, opaque full bleed. Elevated three-quarter establishing view of connected narrow streets and sloping rooftops so it harmonizes with an isometric game. Interesting recognizable neighborhood architecture and night lights along the outer left, upper edge and right edge; the central and lower middle area will sit behind the large game venue, so keep it lower contrast, dark indigo pavement and soft distant building shapes. No central hero object. Fill the corners and edges with real scenery rather than black void. No oversized foreground objects.
Style/medium: polished hand-illustrated game environment, crisp architectural silhouettes, controlled graphic/cel-shaded color planes with painterly light bloom, slightly softened secondary detail; compatible with the existing vivid Seoul game artwork and GTA-inspired illustrated nightlife mood. This is environmental art, not a photorealistic photograph or a game UI mockup.
Lighting/palette: deep midnight navy and indigo with localized magenta/cyan club neon and warm amber windows and rooftop string lights; rain-dark paving with restrained colored reflections. Ambient city brightness readable around the venue, while the bar gameplay can remain the brightest foreground. No large pure white billboards or huge saturated signs. Sparse small indistinct sign shapes; no requirement for readable lettering.
Constraints: output ONLY the outdoor city background; no isometric bar interior, no cutaway room, no floating platform, no people or vehicles in the foreground, no HUD, no numbers, no labels for district names, no logos, no watermarks, no borders, no blank black frame.
```

