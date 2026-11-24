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

## Session 3 (19-20 Nov 2026): the Curator scenes (`Docs/CURATOR.md`)
- [x] Baseline recorded with sound and measured (feet, hands, camera, light, sound); rig probe of the Curator model
- [x] Hero performance layers for the Curator only: stride-wheel walk, planted feet with stepping turns, wrist-orientation IK onto grips, finger poses, props owned by the hand (code + footage)
- [x] Visemes from the actual audio for all 92 Curator lines (`Tools/lipsync`); playback on the voice's real position (code + footage)
- [x] Blinks that close the lids (generated blendshapes) (footage)
- [x] Lens-based framing on his animated eyes, beat-driven shots, eyeline on the investigator (code + footage)
- [x] Gallery look rebuilt; F2 tiers now cover the Gallery's volume; storm with lightning through the east window (footage)
- [x] Recorded storm, footsteps, pages and book sounds (Commons PD / CC BY 4.0), contact-driven (code + footage)
- [x] First full recording reviewed shot by shot: 9 faults found and fixed (insert held over the next line, blank and vanishing pages, exhibit framing, book pops, hand jumps on release, a jump cut, square smoke, black Gallery windows, sound beds cut dead) (`CURATOR.md` 4.1)
- [x] All 8 Curator segments re-recorded with sound and measured against the baseline: foot slip 8% -> 1% / 3-4% -> 0%, 0 prop pops, 0 hand discontinuities, 0 lens changes inside a shot (`CURATOR.md` 4.2, `Docs/curator/`)
- [x] Curator play-test suite 56/56: pause at four moments, line skip, scene skips at five beats, missing audio, restoration; it caught a real fault (he sagged into the clip pose while paused), fixed (`CURATOR.md` 4.3)
- [x] Standalone macOS build plays the prologue; run-time shader variants now actually kept (13 materials, 0 keywords lost; before, built skin was mirror-glossy and procedural surfaces lost their maps) (`CURATOR.md` 4.4)
- [x] Real-time profile, MacBook Air M1, 1920x1080, top tier: 22.9 ms median, 27.1 ms p99, GPU-bound (`Docs/curator/profile_summary.txt`)
- [ ] Profile on the target PC class (GTX 1660 / Ryzen 5 3600, 1080p)
- [ ] Recorded match strike, candle blow-out and cloth movement (not found under a usable licence)
- [ ] Recorded VO and a scan-quality hero head with blendshapes (see `Docs/CURATOR.md` section 5)

## Session 4 (21 Nov 2026): Acts I-II, the market, Curator scenes 0-1 (`Docs/ACTS_1_2.md`)
- [x] Supplied `Pinnacle_Acts_1_2.patch` applied once, unchanged, as its own commit (it was not yet applied)
- [x] Scripted route for player builds, any version of the code (`PlayRoute.cs`, `Docs/PLAYTEST.md`): timing by section, render counters, Profiler markers (dev build), checks, footage with UI and sound
- [x] Market baseline measured on the original code (3 runs) and compared with interleaved runs (section 7 of `ACTS_1_2.md`)
- [x] Market GPU cost: dressing props were ~95% of triangles; small props no longer cast moon shadows, distant props simplified or dropped, 2 cascades in the market (A/B measured, after-build measured)
- [x] Cold entry: control 15.7-16.0 s -> 2.3-2.9 s (final interleaved runs); no rendering behind the loading card; probe rendered once (measured)
- [x] Player no longer shoved or lifted during conversations; Vikram in frame at every decision (measured)
- [x] Stall boards: fitted, hung under the rail on straps, lit paint, single-sided; awnings rest on their rails; bulbs beside the boards (visually checked)
- [x] Shop frames, hoarding and OM plaque attached; windows, stair pane, window head and latch fixed (visually checked; motion review pending)
- [x] One decision compass at every decision in Acts I-II; input tests (keys, gamepad, held key, pause) pass (measured)
- [x] Act I control 30 s -> 1.2 s with voices over free movement; Act II pacing beats (measured / code)
- [x] Radio on a stall, hallway steps that travel, creak at the door, bed ducking (code; listening pending)
- [x] Curator scene 1 skip hands over to Act III once with camera/time/sound restored (measured)
- [ ] Listen on headphones and laptop speakers (balance, panning, attack/falloff)
- [ ] Recorded door creak, horn, phone and engines (no licensed recordings in the project)
- [ ] A person plays Act I end to end (nine clues, Kamala, door, note) and the market by hand (control feel)

## Session 5 (22 Nov 2026): Arjun's recorded voice (now in `Docs/VOICE_CHECK.md`)
- [x] 50 recordings by the voice actor (`arjun voice overs 2/`) replace 46 of Arjun's text-to-speech lines and voice 6 that had none: his three questions to Mrs. Kapoor were silent, and his three answers to Priya in Act V (one per ending) are new (`Tools/arjun_recordings.py`, now `Tools/vo_recordings.py`)
- [x] Trimmed, the Act I opening split at its pauses, levelled to the rest of the VO (-16 LUFS); every line transcribed on the Mac and compared with its subtitle: 45 of 52 word for word, the other 7 differ only in names the recogniser mishears (measured)
- [x] Five subtitles changed to the recorded wording (`a3_arjun_unlock`, `a3_arjun_reddy`, `a4b_arjun_told`, `a4c_arjun_id`, `a4c_q_a`)
- [x] Acts I-II subtitles change phrase on the recorded words (`VoTiming.cs`); Acts III-V show each line for its recording's length
- [x] The text-to-speech generators never overwrite a recorded line, even with `--all`
- [ ] 39 Arjun lines still have no recording and stay text-to-speech (listed in `Docs/VOICE_CHECK.md`)
- [ ] Reddy's reply to "I have evidence you don't" still says "She's braver than all of us", written for the old line about Priya's letter
- [ ] Listen to the recorded lines in the game against the other characters' voices

## Session 6 (22 Nov 2026): cinematic pass (`Docs/CINEMATIC_PASS.md`)
- [x] Compass drawn whenever a decision is open (it was gated on the subtitle text); larger dial below the eyeline, needle at rest, swing, tick, commit ring, full line on focus (measured: `kamala` 20/20 at 1280x720 and 1920x1080)
- [x] Compass act-scoped into Act III (Pandit, crossroads) and the café; Act IV house/police and Act V keep their panels (measured: Pandit, crossroads x3, Act IV opens at each place)
- [x] Mrs. Kapoor steps into the room to ask (lit, 1.75 m, eyes above the dial); Vikram in frame at both decisions (measured)
- [x] Held keys, pause, journal and cursor over a decision (measured)
- [x] Café Irani counter, biscuit case, owner's hand-IK task, night street (measured: grip within 0.4-2.0 cm, cup on the worktop; seen)
- [x] Act I window view at three depths, stairwell window opening, nameplates on the wall and depth-tested, door latch/hinge/frame sounds (measured: door log and mix; seen)
- [x] Act II: no horns over conversations, narration fade, fewer toasts, Vikram checks the lane, kettle (code)
- [x] Act III props: desk and CRT, drawer, bookcases, shelving, cage, calendar, envelope (seen in part; puzzles re-run)
- [x] Act III from a fresh start: guard, computer, Record Room, cage, calendar, drawer, key, Pandit, letter, gate, call, crossroads, Curator, Act IV at the café (measured: `act3full` 41/42, the one failure the route's own timing, fixed)
- [x] Café sign readable from the entrance (two-line board clear of the pergola) and the parked car's windows on its body (seen: `final/cafe_1080` stills)
- [x] Visemes for Kamala, Bhaskar, Vikram, Sharma-ji, the Pandit, Raj; loudness fallback; nods from stressed words (measured: quiet, fast, interrupted lines, silent listener)
- [ ] Listen to the door, horns and market mix on headphones and speakers
- [ ] A person plays the compass with a real mouse, trackpad, gamepad and the Arduino
- [ ] Foot planting in turns on the spot and on steps; no-rig lip-sync fallback run
- [x] Frame time measured before/after at 1080p (`CINEMATIC_PASS.md` section 8): market unchanged and café +3 ms on a quiet machine; p95 under 33.3 ms in steady play and the café in every run
- [ ] SUV chase p95 at or under 33.3 ms on a warm M1 (35-44 ms in three of six warm runs)

## 23 Nov 2026: the cast's recorded voices (`Docs/VOICE_CHECK.md`)
- [x] 45 recordings in `Voice Overs/` (Kamala 9, Bhaskar 5, Vikram 12, Harishankar 5, the Act III caller 1, Raj 8, Rohan 5) replace 44 text-to-speech lines (`Tools/vo_recordings.py`, which now handles Arjun's too): trimmed, levelled to -16 LUFS like the rest; two lines are two takes joined (`v_t2_intro`, `a4a_raj_2`), one take voices two lines (`a4a_raj_b`, `a4a_raj_where`)
- [x] Every line transcribed on the Mac against its subtitle: of all 96 recorded lines 65 word for word, the other 31 differ only in names, numbers written as digits and Hindi words (measured)
- [x] The takes follow an older, longer script: their subtitles were changed to the recorded words (Acts I-II in `PriyaRoomStory.cs` / `MarketStory.cs`, checked against the tool's table on every run; Acts III-V in `acts_dialogue.py`)
- [x] Where a take leaves out what the story needs: the caller's meeting ("Café Irani. Thirty minutes. Come alone.") is a text message after the call; Raj hands over the phone as he answers "Why should I trust you?" (asked before the dial, which shows it as asked); Rohan's take is cut where it had Priya take the deed from the library (Reddy signed it out); Harishankar's question is implied, not spoken, and the hearing time and shelter he used to add (and their case-file notes) are gone: the letter and Act IV carry them
- [x] Lip-sync re-made from the recordings for the 43 lines spoken by a face (measured)
- [x] Played in a player build (`Builds/voice-overs/Pinnacle Voices.app`): routes act1intro, kamala, market, act3, cafefull, house, act5 (audio captured) and again in real time; 29 of the 44 lines were heard, each at its recording's length (`voices.txt`), and in the captured mix the recogniser found the actors' own words for 18 of the 19 checked (the 19th was cut off by the route). Route checks in real time: act1intro 6/6, market 14/14, market input tests 22/22 (the build before: 22/22), act3 24/25, cafefull 35/36; audio-captured only: kamala 20/20, house 26/27, act5 26/26. The misses are the two listed below, or came from audio capture itself (the dial's arming delay runs on real time, the route on stepped time: dropped key presses, the needle 16 degrees off) (measured; `Recordings/voices/`)
- [ ] 11 lines of these characters still have no recording (listed in `Docs/VOICE_CHECK.md`): Kamala 3, Bhaskar 1, Vikram 3, Raj 3, Rohan 1
- [ ] Not reached by a route, checked in the files only (15): Kamala's welcome and two of her answers, Vikram's replies to the other two challenges and his later answers, Harishankar's first answer, Raj's answer about Priya (the same take played at the dhaba), Rohan's last two answers
- [ ] Listen to the recorded lines in the game: the joins, the cut in Rohan's line, the recorded voices next to the remaining text-to-speech ones
- [x] The Curator: 10 takes in `Voice Overs/Curator/` (one per scene) split at their pauses into 65 lines: the Act I welcome, the interludes after Acts II-IV, the three verdicts, the library and alley visits; 64 word for word apart from names and digits, one subtitle changed to the recording (`cur1_05` "had"), and `cur2_06` says "Panditji" again, as recorded (measured). In the player build his interlude after Act II played all 8 lines at their recorded lengths (route `market` with the Curator, 19/19; measured); the other scenes are checked in the files only
- [x] His reactions to the Act III answer and the Act IV ally are not recorded: a recorded interlude leaves out the answer line and plays the general line for the ally (`CuratorInterlude.Voiced`), so his voice never turns into text-to-speech mid-scene; a reaction joins again once it is recorded (code)
- [ ] The Curator's 27 unrecorded lines (the gallery, the Act V "other side" scene, the six reactions) stay text-to-speech; the recordings are dry, without the room echo his text-to-speech had
- [ ] Existing route failures seen in the build before this work too: the café owner's hand leaves his cup (30-32 cm), Raj in cover ("holds still" check)

## 24 Nov 2026: Reddy, Priya, the judge and the lawyer (`Docs/VOICE_CHECK.md`)
- [x] 23 takes (Reddy 11 in `Voice Overs/reddy/`, Priya 10 in `Voice Overs/priya/`, the judge and Deshpande's lawyer one each) voice 21 lines: Priya's last Act V line and her last line on the roof are two takes joined; every line matches its subtitle word for word apart from names and digits (measured)
- [x] Subtitles follow the recorded words; three passages are cut where a take contradicts the game: Reddy's "Where did you get that USB? Never mind." (it follows either winning piece, and neither is a USB stick), "She's braver than all of us" (written for an older line of Arjun's; she now answers "I have evidence you don't" with "All right, Mr. Mehra. Cards on the table."), and Priya's "The hearing is in four hours" (it is 4 AM and the hearing is at ten). Each cut sits in the quiet between words, 55-65 dB below speech (measured)
- [x] Reddy's reveal now ends on Deshpande sending everyone she has before 10 a.m.; "I need Priya to testify in person" is no longer said (the lawyer's "Without the testimony of the person who obtained these documents" in the Sacrifice ending still carries it). Priya's "I have the originals" is the forged 1965 file, as the story bible has it
- [x] Priya's spare take "All right, let's finish this." is a new line (`a5_priya_finish`), played over the white after Arjun's Justice answer only (code)
- [x] Lip-sync re-made from the recordings for the 21 lines (measured)
- [x] Played in a new player build (`Builds/voice-overs/Pinnacle Voices 2.app`, with the committed packages, Unity AI Assistant and AI Inference included: the first build with them), in real time: routes reddy 30/30, act5 Justice 34/34, Sacrifice 21/21, Revolution 25/25. 19 of the 21 lines played, each at its recording's length (`voices.txt`); Priya's new line played after the Justice answer and not in the other two endings (measured; `Recordings/voices2/`)
- [ ] Not reached by a route, checked in the files only: Reddy's answers to "Show my hand" and "Priya first" (`a4c_reddy_a`, `a4c_reddy_c`)
- [ ] Still text-to-speech, so the voice changes mid-scene: Reddy's "That's my signature", "That proves nothing", her eight weak-piece answers and her court line; Priya's reading on the stairs; the judge in the Sacrifice ending; the lawyer in the Justice ending (list in `Docs/VOICE_CHECK.md`)
- [ ] Listen in the game: the three cuts, the two joins, and Priya's new line over the white

## Next
- [ ] Justice gate: case conditions instead of `EvidenceCount >= 18`, shown on the casket card
- [ ] Deduction step: link evidence to questions (fact vs. interpretation), one fair contradiction per act
- [ ] Settings screen (persistent): volumes, subtitles + size, sensitivity, invert, FOV, head bob, motion blur, timed-choice assist, quality, display
- [ ] `GameState.Mode` for explore / dialogue / inspect / cutscene / transition; route input by mode
- [ ] Blink + gaze layer for key NPCs
- [ ] Runtime NavMesh (`NavMeshSurface.BuildNavMesh` after `BuildWorld`) for market and shelter crowds
- [ ] Shot lists + polish: Kamala, SUV decision, Act IV escapes, Priya's reveal, Gallery and endings
- [ ] Switch the remaining world TextMeshes (signs) to `GameKit.Solid`
- [ ] Check Acts I-V in a standalone build (run-time shader variants are now kept, `BuildPlayer.EnsureVariantMaterials`; only the Curator prologue has been looked at in a build)
- [ ] Act IV mid-act checkpoint (after the ally is won)
- [ ] Music ducking under dialogue; distinct ambience per location
- [ ] Profile on an identified target machine (provisional: GTX 1660 / Ryzen 5 3600 class, 1080p, 16.6 ms frame)
