# Owner-specific office environments

The three offices now use the corresponding approved owner-card room
illustrations in `ArtSource/Owners/Backdrops` as environmental references.
The built-in ImageGen tool separately edited each existing office, retaining
the owner's recognizable chibi identity, typing pose and foreground desk work.
The changes are illustrated environment edits, not claims that the generated
foreground pixels remain identical to the previous scene.

- Jessy / Freebird: reddish wood, vinyl shelves, warm brass lamps, pinned concert
  artwork and a glazed view into the magenta-lit stage.
- Edison / FF: raw concrete, exposed pipes, acoustic panels, cyan control-room
  lights, an observation window, amplifier stack, road cases and a DJ shelf.
- Jaiden / Pet Sounds: gray plaster, walnut record cabinet and turntable, warm
  amber pendant, green accent tube and an open doorway into the amber stage.

The exact prompts accompany the unchanged generated `*.v2.source.png` files.
`previous-manifest.json` preserves the earlier asset and shared-screen records.
`office-layout.json` records the current owner-specific safe screen geometry.
Jessy's monitor/phone have a small vertical offset; the native CSS scopes that
calibration to owner 0. Edison's and Jaiden's shared safe regions still fit.

Run `Tools/prepare_owner_office_backgrounds.py --sync-unity` to downsample the
complete generated sources to the existing 640×360 runtime image paths with
nearest-neighbor sampling, refresh `../manifest.json`, and optionally mirror
the exact PNG streams to Unity Resources. No crop, palette reduction or repaint
is performed. Office artwork remains selected-owner-only and deferred until
the planning screen opens.
