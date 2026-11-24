# Polish pass: audit, verification status and production list

Started 18 Nov 2026. Unity **6000.5.9f1**, URP 17.5, glTFast 6.20, Input System, AI Navigation 2.0.14,
Timeline 1.8.12, Test Framework 1.7.0 (versions read from `ProjectSettings/ProjectVersion.txt` and
`Packages/manifest.json`, none changed). The progress checklist is `Docs/POLISH_CHECKLIST.md`.

## What could and could not be verified in this session
- **Done:** read the source. Compiled every script against the real Unity 6 assemblies with Unity's bundled
  Roslyn (0 errors). Unit-tested the save rules (34 checks pass). Checked the Unity Editor log for import errors
  (the Curator model and the new shader imported cleanly).
- **Not done:** no standalone build (the project is open in a running Unity Editor, which locks it against a
  batch-mode build). No play-through and no screenshots. No profiling. Nothing below marked "runtime" has been
  reproduced in a build. "Source defect" means confirmed by reading the code.

## Issue table
Priority: P0 blocker or state corruption, P1 narrative coherence or interaction reliability, P2 cinematic quality, P3 cosmetic.

| # | Where | Evidence | Player impact | Pri | Change | Status / acceptance |
|---|---|---|---|---|---|---|
| 1 | `SaveSystem.ResetAct(2)` | Cleared **all** evidence, including Acts III–IV notes; never cleared later acts' choices | Replaying Act II from the menu wiped the evidence the Justice gate counts; replaying Act III left Act IV allies standing | P0 source | `SaveRules.ResetFrom`: clears the act and all later acts; evidence tagged by act | **Fixed.** Tests "restarting Act III…" and "restarting Act II…" |
| 2 | `GameMenu` act list | Replay ran on the live campaign, and finishing it rolled forward into the next acts | Replaying a chapter silently overwrote the main campaign | P0 source | Replay sandbox (`SaveSystem.BeginReplay`), chapter-start snapshots in the profile | **Fixed** in code; runtime unverified |
| 3 | `SaveSystem.Save` | `File.WriteAllText` straight over the only file | A crash mid-write could destroy the save | P0 source | tmp + flush + `File.Replace`, `.bak` recovery, `.corrupt` copy | **Fixed**; recovery path not exercised at runtime |
| 4 | `SaveData` | No version handling, no validation | Hand-edited or damaged values could break acts | P1 source | v2 + migration + `Validate` | **Fixed.** Tests |
| 5 | `Act5Story.Run` | `endingsSeen.Add(chosen)` before the ending plays | Quitting mid-ending still counted it | P1 source | `SaveSystem.RecordEnding` in `RollCredits` | **Fixed** |
| 6 | `GameMenu.InProgress` | Acts I–III only | Act V always restarted at the shelter | P1 source | Act V Gallery checkpoint (`act5Stage`) | **Fixed.** Act IV still restarts at its start (documented) |
| 7 | `ActStoryBase.Talk/Narrate/Choose/ShowCard`, `MarketStory.Line/Menu`, `PriyaRoomStory` Kamala choices | Loops poll `Keyboard.current` every frame; pausing sets `timeScale = 0` but `yield return null` still runs | Dialogue answers and skips accepted **behind the pause menu** | P0 source | `GameState` owns pause; every loop yields while paused; `GameInput.InteractPressed` false while paused; code-entry typing blocked | **Fixed** in all three systems; runtime unverified |
| 8 | Three `SetPaused` copies | Each owned timeScale, audio pause, cursor and player lock | State left inconsistent across act changes | P1 source | `GameState.SetPaused`, `GameState.ResetForNewScene` on act load, interlude start, menu return and act destroy | **Fixed** |
| 9 | `CharacterNPC.WalkTo` | Loops until within 6 cm; no exit if blocked | A blocked NPC hangs the scripted scene forever | P0 source | Timeout (expected time at half speed + 4 s) then placed at the target; `StopWalking` cancellation; `LastWalk` result; exits if the NPC is destroyed | **Fixed** (existing calls get the default timeout) |
| 10 | Act IV café | Title "7:05 AM", morning sun, but Raj says the hearing is "tomorrow morning" and Act V is 4 AM | Broken chronology | P1 source | 11:05 PM and night lighting (moon, string lights, window glow, street lamp) | **Fixed**; the lighting is unseen (needs a look in play mode) |
| 11 | `a5_priya_4` | "The hearing is in four hours" at 4 AM; hearing is 10 AM | Contradiction | P1 source | "six hours" | **Fixed, VO re-recorded** |
| 12 | Deed custody | Reddy signed out the deed; Rohan says Priya took it; Priya says "I have the originals… I have it here" | Who holds the deed is unclear | P1 source | Story bible §3: Reddy holds the 1942 deed, Priya holds the forged 1965 file | **Fixed:** `a4b_rohan_2`, `a5_priya_4`, `end1_priya` (VO re-recorded), Municipal email card text |
| 13 | Justice gate | `EvidenceCount >= 18` (raw count of clues + notes) | Progress is "collect everything"; the count doesn't say *why* the case holds | P1 design | Case conditions (story bible §5) | **Open:** designed, not implemented |
| 14 | Detective loop | Evidence is a list of notes; no deductions or contradictions | Weak "connect / test" stages | P1 design | Deduction board linking notes to questions | **Open** |
| 15 | Settings | No settings menu (only F2 quality) | No volume, sensitivity, subtitles size, FOV, head bob, motion blur, rebinding | P1 | Persistent settings screen | **Open** |
| 16 | Gameplay state model | `GameState` covers pause only | Exploration / dialogue / cutscene / transition are still per-script phases | P1 | Extend `GameState.Mode` and route input by it | **Partial** |
| 17 | Navigation | AI Navigation installed, no NavMesh built (worlds built at runtime) | NPCs walk straight lines; rely on sidestep | P2 | `NavMeshSurface.BuildNavMesh()` after `BuildWorld`, agents for key NPCs | **Open** |
| 18 | Facial performance | Jaw follows audio loudness; no visemes, no blinks | Close-ups look stiff | P2 asset | Blink + gaze layer now; visemes need rigged faces (see the asset list) | **Open** |
| 19 | World text | TextMesh uses the GUI text shader (depth follows IMGUI) | Text drawn through objects | P2 source | `Pinnacle/WorldText` shader, `GameKit.Solid` | **Fixed for Curator text**; market signs and other world text still to switch |
| 20 | Standalone shader variants | GRAPHICS.md warns that runtime-created URP Lit materials need normal-map and alpha-clip variants | Missing textures in a build | P1 risk | Shader variant collection or "always included" | **Unverified** (needs a build) |

## Branch and checkpoint validation matrix
✔ = covered by unit tests of the rules. **Manual** = needs a play-through (not done yet).

| Case | Rule-level | In game |
|---|---|---|
| New game → Act I | ✔ (`ResetFrom 1`) | Manual |
| Continue mid-Act I / II / III | ✔ (`InProgress`) | Manual |
| Continue at the Act IV start, each route 0/1/2 | ✔ (branch kept) | Manual ×3 |
| Continue at the Act V Gallery | ✔ | Manual |
| Justice / Sacrifice / Revolution recorded at credits | ✔ (`RecordEnding`) | Manual ×3 |
| Chapter replay leaves the campaign file untouched | code path | Manual |
| v1 save migrates | ✔ | Manual (load an old file) |
| Damaged save repaired / backup restored | ✔ repair; backup path untested | Manual (corrupt the file) |
| Pause during choice / line / code entry / card | code path | Manual |
| Blocked NPC walk times out | code path | Manual |

## Asset and VO production list (what code cannot supply)
| Need | Purpose | Spec | Status |
|---|---|---|---|
| Facial rigs with visemes + blink shapes for Priya, Kamala, Reddy, Raj, Rohan, Vikram, the Curator | Close-up dialogue | ARKit-style 52 blendshapes or at least 15 visemes + blink L/R; head and hands ≥ 2k textures; Humanoid rig | Not available. The current Rocketbox heads have a jaw bone only |
| Interaction clips | Sitting, picking up documents, cups, handlebars | Humanoid FBX, 30 fps, root motion off: sit down/idle/stand, reach low/mid/high, hand-over, phone | Not available. Hands use IK onto props today |
| Recorded VO | Replace macOS TTS | 48 kHz WAV mono, −16 LUFS, same file names in `Assets/Resources/VO` | Recorded (23 Nov 2026, `Docs/VOICE_CHECK.md`): Arjun, Kamala, Bhaskar, Vikram, Harishankar, Raj, Rohan and the Act III caller, 96 lines. Still TTS: the lines of theirs listed there, and everyone else (Reddy, Priya, Sharma-ji, the Curator, the court, the thugs) |
| Hero evidence models | Deed, 1965 file, USB, burner phone, diary | 2k PBR, real scale, <5k tris | Primitive placeholders |
| Temple imagery review | Cultural suitability of the Kali temple and idol | Review by someone familiar with the practice | Not reviewed |
| Music | Restrained score per act | Stems, loopable | Synthesised beds only |

## Known limitations (honest)
- None of this session's changes have been played. Pause gating, replay isolation, the Act V checkpoint, the night
  café and the timeouts are source fixes compiled against Unity, with no runtime check.
- Act IV has no mid-act checkpoint.
- Act I–II VO pipeline (`Tools/make_voices.py`) was not touched; Act I–II subtitles live in their scripts.

## Next highest-value tasks
1. Play-test this pass in the Editor: pause during choices, chapter replay, Act V Gallery resume, night café, the Curator prologue.
2. Make a standalone build and check shader variants, VO and models.
3. Replace the Justice evidence count with case conditions, and show them on the casket card.
4. Settings screen (volumes, subtitles size, sensitivity, invert, FOV, head bob, motion blur), all persistent.
5. Blink and gaze layer for the key NPCs. NavMesh built at runtime for the market and the shelter.
6. Shot lists and polish for the Kamala encounter, the SUV decision, the Act IV escape, Priya's reveal.
