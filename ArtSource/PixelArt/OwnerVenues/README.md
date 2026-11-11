# Owner-specific interiors

Jessy (owner 0) uses **Club Freebird**, Edison (owner 1) uses **Hongdae FF**,
and Jaiden (owner 2) retains the original room with the green
[Pet Sounds neon wordmark](PetSounds/README.md) added to `venue.png`.

These are original pixel-art interpretations adapted to the existing game
footprint, rather than surveyed venue replicas. FF's latest user-directed
base plate has gray concrete, no sofas or stools, a compact left-wall bar facing
the hall, and a DJ booth to the left of the concert stage. Its eleven former
seated guests remain in the game as standing guests.

FF layout version 4 adds five tall round tables with two high stools each,
outside the stage-front crowd area. The two transparent prop images are shared
by 15 depth-sorted instances in the main view and minimap; the venue plate,
bar, DJ booth and stage remain the same. Matching circular navigation obstacles
protect routes. Four standing guest homes and one floor staff home move clear
of the furniture. The stools are placed as empty furniture props.

`FFStandingFurniture.source.png` and `FFStandingFurniture.prompt.json` retain
the built-in ImageGen sprite sheet and prompt. `Tools/prepare_ff_tables.py`
packs its two isolated alpha cells, preserving proportions and original edge
alpha, into `standing-table.png` (40×38) and `standing-stool.png` (24×30).
Prop foot offsets are logical drawing pixels, shared by native and Unity.

Freebird's base plate removes all sofas and stools. Its bar is
half the original length and sits immediately left of the concert stage; the
stage is roughly 25% wider in the same isometric room. The active source is
`ClubFreebird.v2.source.png`, with prompts in `ClubFreebird.v2.prompts.json` and
independent calibration in `ClubFreebird.layout.json`. Freebird uses a regional,
fading staff offset to keep bartenders behind the counter while preserving
their floor positions when they leave the aisle.

The social layout adds a separate arched open doorway between the bar and stage,
a long left-wall table and six waiting stools, with matching seated audience
anchors and a walkable route toward the stage. FF adds ten standing audience
anchors beside its existing tables and ten small cocktail-glass props on their
tops. These are depth-sorted independent sprites; see
[social prop sources, prompts and calibration](SocialProps/README.md).

The 960×558 isometric format is shared. FF has its own furniture masks, stage
calibration, staff aisle, queue and navigation obstacles in `ClubFF.layout.json`
and the FF gameplay profile. Layout version 3 aligns the larger generated DJ
booth and standing guests. Old FF saves migrate positions and paths while
retaining money and session progress. Freebird has its own versioned profile;
Jaiden keeps the original room and navigation.

## Photo references

- [Club Freebird interior in a 2026 organizer's venue notice](https://event-us.kr/nomercycompany/event/123635)
  (April 6, 2026): rounded plaster arches, rose-grey walls, exposed ducts,
  black lighting truss, pale projection screen, blue/cyan stage and warm crowd light.
  The notice identifies the current former-Convent location, distinct from the
  original club closed in 2015 and other similarly named live halls.
- [Club FF stage photo](https://media.k-dj.jp/kdj/clubs/club-ff-hongdae/images/photo_1.jpg):
  red slanted FF mark on charcoal, orange drums, amp stacks and wooden stage.
- [Club FF firsthand interior photos, 2017](https://cyuancyuan.wordpress.com/2017/12/19/%E3%80%90%E9%9F%93%E5%9C%8B%E8%81%BD%E9%9F%B3%E6%A8%82%E3%80%91live-house-%ED%99%8D%EB%8C%80%EC%97%AC%EC%97%AC-club-ff/):
  wooden bar, rainbow handwritten menu boards, mesh and posters.
- [Club FF firsthand interior photos, 2023](https://wisdom0199.tistory.com/624):
  red and purple lighting, FF wall mark, request neon and exposed ventilation.

Reference photographs stay in ignored `Temp/VenueInteriors-20261110/references`;
they are not shipped as game textures. FF references establish recognizable
features, not a claim of an exact November 2026 floorplan.

## Production

The built-in ImageGen tool created the two non-destructive sibling source plates.
Initial prompts are retained in `prompts.json`; the FF renovation and targeted
bar placement correction are in `ClubFF.v2.prompts.json`. The active FF source
is `ClubFF.v2.source.png`; its previous source is retained separately.
`Tools/prepare_owner_venues.py`
uses only nearest-neighbor sizing and 256-color PNG encoding to export the
two runtime files and copies FF's reviewed calibration to `venue-ff.json`.
`Tools/prepare_unity_pixel_art.py` copies the identical PNG streams into Unity
Resources as `VenueFreebird.bytes` and `VenueFF.bytes`, and converts the FF
calibration to `PresentationFF.json`.

Keep Jaiden's original V4 room source and geometric calibration. Its native
`venue.png` and Unity `Venue.bytes` include the separately authored Pet Sounds
neon layer; `Tools/prepare_petsounds_neon.py` rebuilds it from the untouched V4
source. All texture-backed foreground masks must switch with the room plate.

## Verification

- `Tools/qa_native_owner_venues.py --package docs` exercises the real owner
  buttons, saving/reloading, daytime booking, night launch, return to lobby
  and another owner selection. It checks both completed canvas buffers.
- Jaiden's main view and minimap are byte-identical to the previous renderer
  with both an empty room and a frozen state containing 40 actual game actors.
- The native delivery, Unity metadata, streaming playlist and web delivery
  checks pass (32 Python tests); stage-screen and music lifecycle checks pass.
- `Tools/qa_native_ff_layout.py` records the FF renovation checks. After the
  separate Freebird renovation, `Tools/qa_native_freebird_layout.py` checks Freebird staff
  routes, migrations, cleanup/settlement and unchanged FF/Jaiden behavior and
  pixels against the preceding release. The latest FF table addition is checked
  by `Tools/qa_native_ff_tables.py --package docs`, including furniture circles,
  full-night completion and unchanged Freebird/Jaiden behavior and pixels.
- FF has 19 static foreground masks and 4 LED fixture masks, with no seating
  anchors. Other rooms retain 17 furniture, 11 seated and 4 LED fixture masks.
  The selected plate, furniture, LED and minimap use the same owner profile in
  native and Unity players. Freebird has 13 static masks, 4 LED fixtures and no
  seating anchors. Jaiden retains its original room; when artist-specific models
  are available, its Unity minimap uses the pixel overview to show the full roster.
- Actual screenshots and detailed QA records are retained locally in
  `Temp/VenueInteriors-20261110/qa-packaged` and `unity-qa`; FF renovation checks
  and screenshots are in `Temp/FFLayout-20261110`; the subsequent Freebird
  renovation records are in `Temp/FreebirdLayout-20261110`; the table addition
  records are in `Temp/FFTables-20261110`.

The local native delivery in `docs` includes these changes. Publication is a
separate release operation; the existing Unity WebGL binary is not rebuilt by
the native packaging command.
