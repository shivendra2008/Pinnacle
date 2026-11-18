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
