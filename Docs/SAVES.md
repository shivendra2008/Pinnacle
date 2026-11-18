# Saves, checkpoints and replay

Code: `Assets/Scripts/SaveModel.cs` (data and rules, no Unity), `Assets/Scripts/SaveSystem.cs` (files).
Tests: `dotnet run --project Tools/tests/SaveRulesTests` (34 checks, run 18 Nov 2026: all pass).

## Files (in `Application.persistentDataPath`)
| File | Holds | Lifetime |
|---|---|---|
| `shadows_of_chandni_save.json` | The active **campaign**: progress, evidence (each note tagged with the act it was found in), choices, checkpoints | Replaced by New Game |
| `shadows_of_chandni_profile.json` | The **player**: name, sessions, endings discovered, highest act completed (unlocks), a snapshot of the campaign at the start of each act | Kept across campaigns; erased only by "Reset all data" |
| `*.bak` | The previous version of each file | Restored automatically if the main file is unreadable |
| `*.corrupt` | A copy of a main file that could not be read | Kept for diagnosis |

**Writes are atomic:** the game writes `<file>.tmp`, flushes it to disk, then `File.Replace`s the real file, keeping the
old one as `.bak`. Where `File.Replace` is not supported it falls back to copy-then-move. An interrupted write leaves
either the old file or the new one, never only a half-written one.

**Versioning:** campaign v2, profile v1. A v1 save (all versions before this pass) is migrated on load: name,
sessions and endings move to the profile; untagged evidence is tagged as Act II (the old "restart Act II" cleared
all evidence, so behaviour is unchanged); the checkpoint becomes the next unfinished act. Out-of-range or missing
values are repaired and logged.

## What each menu action does
| Action | Campaign | Profile |
|---|---|---|
| **Start / Continue** (big button) | Resumes the current act at its last checkpoint (see below) | sessions +1 |
| **Play again** (after Act V) | New campaign from Act I | kept |
| **Act list: your current act** ("restart") | That act starts over: its state and every later act's state are cleared | kept |
| **Act list: any other unlocked act** ("replay") | **Not touched.** The act plays in a sandbox loaded from the snapshot taken when the campaign first reached it. Finishing it carries on into the next act, still in the sandbox. | Endings reached in a replay **are** recorded |
| **Quit to main menu** | Reloads from disk (leaves any replay) | |
| **Reset all data** | Erased | Erased |

A replay of a chapter that has no snapshot (a save from before snapshots existed) uses the current campaign with that act
and everything after it cleared, in the sandbox.

## Checkpoints (where Continue resumes)
| Act | Checkpoints | Restored on resume |
|---|---|---|
| I | Every clue found | Clues found, Kamala conversation done or not |
| II | Talks with Bhaskar / Vikram, evidence | Who was talked to, evidence, player position (existing `ApplyResume`) |
| III | Stage 1 USB read, 2 sign-out sheet found, 3 envelope received | Act III stage |
| IV | Act start only | The Act IV route (`branch`) chosen in Act III |
| V | Act start; **Gallery** (after Priya's question) | Straight to the Gallery and the casket choice |

The ending counts as discovered when its credits roll, not when the casket is picked (it used to be counted at
the pick, so quitting during an ending still counted it).
