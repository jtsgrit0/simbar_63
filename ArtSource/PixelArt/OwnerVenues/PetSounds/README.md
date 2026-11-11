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
Room layout, audience capacity, furniture masks and walking routes are unchanged.
