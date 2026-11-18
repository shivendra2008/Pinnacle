# Cinematics: shot lists

Every cutscene must support pause, subtitles, skip (Enter: the same end state as watching it, applied once, never
choosing a dialogue option), missing-audio fallback (lines then time from their text length) and a clean return
of the camera and controls. Branching state lives in `SaveData`; cutscenes only read it.

## The Curator: Welcome (prologue, before Act I). Implemented in `CuratorInterlude.Welcome`
Exit state: candle out, fade to black, camera back in Arjun's head in the flat, Act I's title card. Skip: fade out
and the same exit (nothing to record).

| # | Purpose | Framing / lens | Camera | Blocking / eyeline | Props / animation | Dialogue / sound | Duration |
|---|---|---|---|---|---|---|---|
| 1 | Hook: one light in the dark | Extreme close-up on the flame, 18° → 30° | Pull back and up to his face | Curator still, looking ahead | Candle in the left hand (IK) | Match strike; drone fades in | 5.5 s |
| 2 | Reveal the hall | High wide crane, 44° → 38° | Descends and pushes in | He starts walking | Candles and chandeliers light outward from him | Low boom | 4.5 s |
| 3 | Journey | Low tracking side shot, 34° | Tracks beside him | Walking down the aisle | Mocap walk; footsteps | Silence and footsteps | ~10 s (walk) |
| 4 | Destination | Over the shoulder, 30° → 27° | Slow push toward the shelf | Arrives, turns to the bookcase, head on the book | | Clock ticking starts | ~6 s |
| 5 | The book | Insert, 22° → 18°, focus on the spine | Slow push | Right hand reaches, tilts the book out by the top, slides it free | Spine title: SHADOWS OF CHANDNI | Sting | ~4 s |
| 6 | He turns to us | Low frontal medium, 32° → 29° | Slight push | Turns 180°, book cover toward the lens | Book moves to the right-hand hold | | 3 s |
| 7–9 | Introduction | Eyes ECU → MCU → high wide | Cut per line | Looks into the lens | | `cur0_01`–`03` | per line |
| 10 | Walk to the table | Reverse dolly in front | Backs away with him | Walking toward camera | | `cur0_04` | walk/line |
| 11 | Book down and open | High over the shoulder | Held | Leans 16°, sets the book flat, opens the cover | Page: CHAPTER ONE | Page sound | ~3 s |
| 12–20 | The welcome | Book insert, low CU, orbit, profile, MCU, page flip, ECU, key insert, figure POV, low CU | One set-up per line | Eyeline to the lens; the POV shot is from behind a draped figure | Key exhibit on the velvet | `cur0_05`–`14` | per line |
| 21 | Ending | CU, 22° → 19° | Push in | Raises the candle under his chin, leans in, blows | Smoke; hall snuffs out in a wave | `cur0_15`, then blow, boom, silence | ~6 s |

## Scenes still to write a shot list for
- Act I: Kamala encounter; footsteps, the shadow under the door, the note.
- Act II: Reddy leaving the market; the SUV decision (15 s timed choice).
- Act IV: each route's confrontation or escape.
- Act V: Priya's reveal upstairs; the Gallery casket choice; each ending.
