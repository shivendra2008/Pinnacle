# Play-test evidence: 18 Nov 2026

Captured by the automated play-test (`Docs/PLAYTEST.md`) running the real game in Unity 6000.5.9f1 (batch mode),
Apple M1. These are game-camera frames: on-screen UI (hover ring, subtitles) is not in them. No frame-time
profiling was done; batch mode is not representative of real frame rates.

| File | What it shows |
|---|---|
| `results_act2_before_fix.txt` | Act II, before: 3 walkers abnormal. Walker15 / Walker1 moved at up to 180° to their facing for ~21% of frames |
| `results_act2.txt` | Act II, after the steering rewrite: no NPC over 5%; worst walker 1.2%, cart puller 3.7%; 0 walk-timeout teleports (4 per 40 s before the no-teleport change) |
| `act2_before_market.jpg` / `act2_after_market.jpg` | Shop fronts: glowing white panels with saturated boxes → shelves of scanned brass, enamel, baskets and produce |
| `act1_clues_before.jpg` | Act I clues before; laptop / sticky note / latch frames look at a wall because the intro's camera turn kept running after a skip (bug, fixed) |
| `act1_after_room_and_face.jpg` | After: room view with the scanned chair; Kamala; a Curator interlude |
| `results_main.txt` | Main run: 48 of 51 checks pass; per-clue hint rates (old → new); saves; pause; prologue and interlude skip |
| `kamala_blink_check.jpg` | Left: eyes open (camera moving). Right: mid-blink, with the eyelid **bones** fully closed, yet the eyes are still visibly open. Blinks do not work on these models |
| `curator_prologue_frames.jpg`, `curator_interlude1_frames.jpg` | Curator scenes, frame by frame |

## Clue hint rates (share of aims at visible parts of each clue that show the hint)
| Clue | Before | After |
|---|---|---|
| Half-packed suitcase | 31% | 100% |
| Sticky note | 65% | 100% |
| Laptop | 75% | 95% |
| Diary | 27% | 85% |
| Two chai cups | 47% | 82% |
| Torn photograph | 0% | 49% |
| Clippings, burner phone, window latch | 100% | 100% |
| **All** | **57.4%** | **89.7%** |

## Cast: Indian and Hindu (19 Nov 2026)
| File | What it shows |
|---|---|
| `cast_before_faces.jpg`, `cast_before_bodies.jpg` | The 14 models as shipped: Female_Adult_06 in a hijab-style head covering (was Kamala), Male_Adult_15 in a skullcap (was Vikram, Pandit and crowd men) |
| `cast_after_faces.jpg` | The cast as used now: skullcap removed (hair; shaved head with a tilak for the priest), bindis, warmed complexions, brown irises; Female_Adult_06 no longer used |
| `cast_after_in_game.jpg` | In the game: Kamala in Act I; the Act II market crowd |
