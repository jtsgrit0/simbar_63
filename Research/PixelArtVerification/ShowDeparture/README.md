# Complete show departures

`seed-sweep.json` records 20 full, seeded browser-native nights through greeting,
physical departure, farewell acknowledgement, outstanding floor work and final
settlement. No member starts leaving until all four finish both greetings.

The native venue contains 16 standing patrons, three supporters and a surfer;
all 20 leave with the four musicians. The Unity venue contains 24 standing
patrons, so its matching journal covers 28 audience members plus four musicians.
The 11 seated bar/lounge guests remain in both versions. Unity screenshots and
the actual visible-actor census are in `../ShowFinale/Unity`.

Participants walk through the exit route and disappear only at the door. The
surfer is lowered before departure. The queue and greeting phases persist in
saves; older closing saves are migrated without repeating a settled receipt.

Regression entry points:

```powershell
node Tools/test_native_aftershow.mjs
node Tools/test_native_saved_aftershow.mjs
node Tools/test_native_music_end.mjs
node Tools/qa_native_seed_sweep.mjs
```

The final popup gates the receipt but does not discard unfinished cleanup or
guest-assistance work. Acknowledging it resumes that work; completing it settles
the night exactly once. Music pauses once at performance end and a subsequent
manual playback choice survives saves and repeated closing transitions.
