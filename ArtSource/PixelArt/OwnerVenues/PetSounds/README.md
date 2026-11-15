# Jaiden's Pet Sounds neon

The user's requested green Pet Sounds wordmark is a separate transparent
pixel-art asset added to Jaiden's existing room. This is a game adaptation of
the Gyeongnidan-gil music pub's sign, rather than a reconstruction of its room.

## Observed references

- [Actual stage video, 00:10](https://www.youtube.com/watch?v=pahySNL9S9Y&t=10s):
  the lowercase `petsounds` lettering, rounded tubes, pale mint core and green
  halo were inspected in a decoded video frame.
- [Wide interior photograph](https://mindtrip.ai/restaurant/seoul-south-korea/pet-sounds/re-ENueCYMf):
  the same sign is mounted on the dark wall above the small stage.
- [Seoul's official venue listing](https://english.visitseoul.net/itaewon/PetsSoundsMusicPub/ENP041790):
  confirms the Gyeongnidan-gil venue and links its official Instagram account.

Reference photographs and video frames stay in ignored
`Temp/PetSoundsNeon-20261111`; they are not shipped as game textures.

## Production

The built-in ImageGen tool produced `petsounds-neon.source.png` with real alpha.
The requested gray plaster wall and mounted neon are retained in the active
`petsounds-neon-wall.v2.source.png`, with both edit prompts in
`petsounds-neon-wall.v2.prompts.json`. The first standalone sign and its prompt
remain available separately. The active patch follows the descending rear-wall
perspective and replaces only the small area behind the neon.

`Tools/prepare_petsounds_neon.py` reconstructs the original 960×558 V4 plate,
then crops the generated sign to its alpha bounds, sizes it with nearest-neighbor
sampling, and alpha-composites it using `petsounds-neon.layout.json`. The normal
pixel-art preparation pipeline calls this step after the original room export.
The helper's `--sync-unity` option copies only the resulting PNG stream into
`Assets/Resources/SimBarPixelArt/Venue.bytes`.

The composition is part of `venue.png`, so the same sign appears in the owner
preview, main world and minimap. Freebird and FF use their independent plates.
The neon-only baseline keeps its room layout and walking routes. The active
room also includes the approved DJ renovation described below.

The 2026-11-15 renovation is retained in `DJFlightCase-v1/`: the selected
flight-case DJ cabinet sits beside the stage's left speaker, the small corridor
sofa and two foreground sofas are removed, and the wall sofas and cocktail
tables remain. The former sofa shadows and jagged edges are included in the
floor cleanup masks. The source PNGs, exact ImageGen prompts, approved placement
and pre-renovation native baseline are retained with the layout.

`Tools/prepare_petsounds_dj.py` is the active native-room builder. It renders
the authored SVG composition and uses `Tools/petsounds_png_composite.mjs` to
retain the original raw RGBA for every untouched pixel. This preserves the
original platform silhouette, neon, stage, bar and rear-wall furniture. Run
this builder when rebuilding the current room; the neon-only helper above
reconstructs the earlier baseline. The game metadata retains eight chairs,
converts the three removed sofa seats to standing guests, and provides DJ/table
collision and foreground masks. The room capacity remains 44.
