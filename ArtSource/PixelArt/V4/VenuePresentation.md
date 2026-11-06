# Seated guests and drum-kit presentation

`VenuePresentation.json` is the reviewed calibration for the unchanged 960×558
`venue.png` plate. `Tools/prepare_venue_presentation.py` copies it to the native
runtime, and both the general pixel-art and illustrated-band exporters invoke
that copier. Repacking older band sheets cannot restore the retired drum anchor.

The eleven seated guests use explicit visual feet and draw depths. Their
gameplay positions, save identifiers and routes are unchanged. Presentation
overrides apply only while a guest is actually seated, stationary, and in an
idle/sit/drink pose; moving or assisted people use ordinary actor projection.
The seated shadows are hidden because those sprites are supported by furniture.

Foreground masks reuse the source plate's own pixels. Rear tabletop surfaces,
front booth backrests and round stool front lips are repainted immediately after
their associated seated actors. Their `actors` lists prevent a sofa mask from
erasing unrelated people who walk in front of it. These masks do not cover the
empty floor or spaces between stool legs.

The drummer's visual foot is [741,265], behind the rack tom above the SIMBAR bass
drum. The retired [-4,+4] actor offset is removed. Nine separate source contours
cover the actual drum shells and cymbal plates at depth 310, leaving the gaps
between the kit visible. The performance-only depth cap remains; ordinary
walking depth lets the drummer move in front of the kit before descending the
steps. This avoids keeping an exiting musician hidden behind the drums.

Visual checks are in `Research/PixelArtVerification/SeatedDrummer/`.

The staff aisle now uses bar-region offset [0,17], raising the employees by
eight pixels from the former [0,25] calibration. The counter still covers the
legs while the standing upper body and forearms are visible. The bartender
uses the full Suit standing frame during stationary bar service and the
matching walking frames when patrolling between the two service ends.
The native patrol follows the existing Unity 9-second/.66m/s cycle; cleanup
and escort ownership continue to take precedence. Before/after crops and
pause/save/economy checks are in `Research/PixelArtVerification/BartenderPatrol/`.
