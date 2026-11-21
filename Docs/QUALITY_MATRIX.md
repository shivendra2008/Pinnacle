# Quality matrix vs. the references (Little Hope / House of Ashes)

Written 18 Nov 2026 from the source and asset files. **Nothing here comes from playing the game.** The project is
Unity 6000.5.9f1 + URP 17.5, and the worlds are built at runtime by C# scripts. The Editor was open on the project
during this session, which blocks a second (batch-mode) instance, so there was no play-through, no build and no
screenshots. Categories: **A** reproduced bug · **B** established by reading the code or assets · **C** suspected,
needs a runtime check · **D** subjective improvement.

| Area | Current implementation (evidence) | Reference target | Verified gap | Fix | Pri | Cat | Dependency |
|---|---|---|---|---|---|---|---|
| Key NPC models | Microsoft Rocketbox avatars (MIT), `Resources/Characters/RB/*.fbx` (~500 KB meshes, 2k body maps), Humanoid, mocap idle/walk | Scanned 4D-captured heads, hair grooms, cloth sim | Dated mid-poly game characters; generic faces tinted for skin and clothing; few South Asian base heads | Keep as the base; specify replacements (see `POLISH_AUDIT.md`, asset list) | P1 | B | New licensed character assets |
| Children | Quaternius low-poly (`NpcStyle.Child`, CC0) | Realistic | Cartoon placeholder next to realistic adults (Act V shelter, market) | Replace with realistic child avatars, or stage the children out of close shots | P1 | B | Asset |
| Facial animation | Jaw and head nod from voice loudness only (`CharacterNPC.RbClips`) | Performance capture | No eyes, blinks or brows moving | **Done:** face layer on the Rocketbox facial *bones* (eye gaze + saccades, blinks, lids following gaze, brow lift on stressed words, upper/lower lip), plus glancing away instead of constant staring. Still **not** phoneme lip sync | P1 | B | Visemes/blendshapes for real lip sync |
| Clue targeting (Act I) | Crosshair matched to each clue's centre and size (`PriyaRoomStory.LookedAtClue`); small ring and gold text | Clear, diegetic, readable | Large or low clues (suitcase) can miss the centre test; marker small and low-contrast in dark rooms | **Done:** exact ray against each clue's hit-box first, old test as fallback; larger ring with dark halo, name + "Press E to examine" tag kept on screen; soft warm light fades in on the hovered clue | P0 (user report) | B/C | Runtime check |
| Investigation view | Document card with text (`ShowCard`) and voiced line; no 3D inspect | Rotatable inspection close-ups | No object inspection view | Inspect view: clue model in front of the camera, rotate and zoom | P1 | B | Better clue models |
| Lighting | Runtime point lights, trilight ambient, soft shadows, SSAO, film volume (`CinematicLook`), photographed skies in two scenes | Motivated practicals, GI, contact shadows | Runtime-only lighting (no bake possible for runtime-built worlds); some acts tuned bright | Keep practical-motivated lights per scene; the Gallery now uses candles and moonlight | P2 | D | Editor prefab workflow if baking is wanted |
| Post-processing | ACES, bloom, vignette, grain 0.24–0.4, chromatic aberration, motion blur, DOF in conversations; F2 tiers | Restrained | Grain and aberration are strong in the Gallery mood volume; no player-facing option | Settings: motion blur / grain / DOF toggles | P2 | D | Settings screen |
| Curator | `CuratorInterlude` cutscenes, cut-based camera, hand IK candle and book, Gallery set (`CuratorGallery`) | Composed, unsettling host | Same Rocketbox suit model as the others; no hand grip poses (wrist IK only) | Face layer now applies to him too; grip poses need hand animation clips | P1 | B/C | Hand poses, a bespoke Curator head |
| Saves / progression | v2 saves, replay sandbox, checkpoints (session 1) | Reliable | Unverified at runtime | Play-test matrix in `POLISH_AUDIT.md` | P0 | C | Play time |
| Pause / input | Central `GameState`, paused loops ignore input (session 1) | Reliable | Unverified at runtime | Same | P0 | C | |
| Navigation | Straight-line `WalkTo` + sidestep; now times out | Pathfinding | No NavMesh at runtime | `NavMeshSurface.BuildNavMesh` after `BuildWorld` | P2 | B | |
| Menus / settings | IMGUI menu and pause, F2 quality only | Full options | No volume, subtitles size, sensitivity, FOV, motion options | Settings screen | P1 | B | |
| Audio | Synthesised beds and tones, TTS dialogue, footsteps by surface (wood/street) | Mixed, spatial, restrained score | TTS voices; no music ducking | Ducking; recorded VO | P2 | B | Voice actors |
| Performance | Not measured | Declared target | Unknown | Profile on a named machine (provisional: GTX 1660 / Ryzen 5 3600, 1080p, 16.6 ms) | P1 | C | Hardware time |

## Narrative changes to confirm
Session 1 (the previous request) changed story content when it was asked to resolve contradictions. This request
says story conflicts should be flagged, not decided, so here they are for you to confirm or revert:
1. **Café Irani:** 7:05 AM in morning light changed to 11:05 PM at night (the café's own dialogue says "at this hour"
   and the hearing is "tomorrow morning").
2. **Priya's line at 4 AM:** "four hours" to the hearing changed to "six hours" (the hearing is at 10 AM).
3. **Deed custody:** Reddy holds the original 1942 deed (her signature on the sign-out sheet). Priya holds the forged
   1965 file, not the deed. Rohan's, Priya's and the hearing line were reworded and re-recorded (`a4b_rohan_2`,
   `a5_priya_4`, `end1_priya`), and so was the Municipal email card. The old text is in git history (the commit before "Curator interludes, shared Gallery, and polish pass 1").
4. **Curator welcome placement:** before Act I, at your request, although its text recaps Act I events.
