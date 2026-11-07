# Real DOM and audio finale verification

Run `uv run --with playwright python Tools/qa_native_show_finale.py` from the
repository root. It serves an ephemeral local entry page that imports the real
`startGame`, renderer, game rules, styles and MP3 assets. The HTTP server closes
after testing. Audio observation retains actual `HTMLAudioElement` playback;
neither audio methods nor gameplay are stubbed.

The fixture selects a normal seeded night with both random paper and guest
events, then imports it through the public save API. Public `tick(.05)` calls
accelerate normal simulation time without assigning actor positions, clocks or
choreography phases. Actual delegated DOM buttons resume music, acknowledge the
popup and complete remaining reaction missions.

`dom-audio-qa.json` records source hashes, real audio state, gesture/departure
phases, all 24 departing native actors, outstanding floor tasks, the final
receipt, browser errors and checks. The screenshots include desktop and 390×844
mobile closing replay, the farewell popup and results music controls.

Coverage includes one-time audio pause at natural performance end, persistent
manual replay, saved greeting progress, the shared greeting/departure barrier,
the exact Korean farewell, modal music access, saved popup continuation,
outstanding cleanup after acknowledgment, results music access, and a reload
that cannot repeat the popup or financial settlement.
