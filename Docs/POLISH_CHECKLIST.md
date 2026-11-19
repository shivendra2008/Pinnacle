# Polish pass: progress checklist

Durable across sessions: tick items here when they are verified, not just written. "Code" = written and compiled;
"Verified" = seen working in the Editor or a build. Decisions live in the docs linked from each line.

Docs: audit and production list `POLISH_AUDIT.md` · story `STORY_BIBLE.md` · saves `SAVES.md` · shots `CINEMATICS.md`

## Session 1 (18 Nov 2026)
- [x] Curator reverted to the Rocketbox suit model (custom .glb removed from the project; generic Mixamo .glb loader kept in `CharacterNPC.TryBuildMixamo`, unused)
- [x] Audit and issue table
- [x] Story bible: timeline, document custody, motives, branches
- [x] Saves: campaign/profile split, v2 + migration, validation, atomic write + .bak + .corrupt (code; 34 unit checks pass)
- [x] Restarting an act clears it and all later acts, evidence tagged by act (code + tests)
- [x] Chapter replay sandbox with chapter-start snapshots (code)
- [x] Act V Gallery checkpoint; endings recorded at credits (code)
- [x] Central pause (`GameState`); dialogue/choice/card/code input ignored while paused, in Acts I–V (code)
- [x] `WalkTo` timeout, cancellation, destroyed-NPC exit (code)
- [x] Chronology: café 11:05 PM + night lighting; "six hours"; deed custody lines + email card (code + VO re-recorded)
- [x] Curator text depth-tested (`Pinnacle/WorldText`)
- [ ] **Verify all of the above in play mode** (see the matrix in `POLISH_AUDIT.md`)
- [ ] Standalone build check

## Session 2 (18 Nov 2026)
- [x] Quality matrix vs. the references, with narrative changes listed for confirmation (`QUALITY_MATRIX.md`)
- [x] Act I clue hover: exact ray against clue hit-boxes first, clearer marker with name + "Press E to examine", soft hover light (code)
- [x] Face layer on Rocketbox facial bones: gaze + saccades, blinks, brows, lips, glances away (code; `CharacterNPC.FaceLayer` switch)
- [x] Automated play-test harness (`PLAYTEST.md`); evidence in `playtest/`
- [x] Act I clue hints **verified**: 57% → 90% of aims overall, suitcase 31% → 100%
- [x] Intro camera turn kept running after a skip: **reproduced, fixed, verified**
- [x] Act II "zombie" NPCs: **reproduced** (walkers sliding at 90-180° to their facing, ~21% of frames), root cause (sideways nudges, unstick slides, turn-around carry, cart pushing), steering rewrite, **verified** (worst 1.2%)
- [x] Crowd walk timeouts teleported people (regression from session 1): fixed, verified 4 → 0
- [x] Market shop shelves: scanned props fitted to shelves instead of glowing panels and rainbow boxes (captured)
- [x] Saves at runtime: backup written, corrupted file recovered (verified)
- [x] Curator prologue and interlude skip restore camera, controls, time and audio (verified)
- [ ] **Blinks do not show on the Rocketbox models** (bones move, visible lids do not: `playtest/kamala_blink_check.jpg`): needs faces with blendshapes
- [ ] Torn photograph (49%), chai cups (82%), diary (85%) hint rates under 90%
- [ ] Bed, desk, bedside table: available scans too different in proportion (x1.6-2.8 stretch); need fitting models
- [ ] Realistic child avatars (or stage children out of close shots)
- [ ] Clue inspect view (rotate and zoom)

## Next
- [ ] Justice gate: case conditions instead of `EvidenceCount >= 18`, shown on the casket card
- [ ] Deduction step: link evidence to questions (fact vs. interpretation), one fair contradiction per act
- [ ] Settings screen (persistent): volumes, subtitles + size, sensitivity, invert, FOV, head bob, motion blur, timed-choice assist, quality, display
- [ ] `GameState.Mode` for explore / dialogue / inspect / cutscene / transition; route input by mode
- [ ] Blink + gaze layer for key NPCs
- [ ] Runtime NavMesh (`NavMeshSurface.BuildNavMesh` after `BuildWorld`) for market and shelter crowds
- [ ] Shot lists + polish: Kamala, SUV decision, Act IV escapes, Priya's reveal, Gallery and endings
- [ ] Switch the remaining world TextMeshes (signs) to `GameKit.Solid`
- [ ] Shader variant collection for the standalone build
- [ ] Act IV mid-act checkpoint (after the ally is won)
- [ ] Music ducking under dialogue; distinct ambience per location
- [ ] Profile on an identified target machine (provisional: GTX 1660 / Ryzen 5 3600 class, 1080p, 16.6 ms frame)
