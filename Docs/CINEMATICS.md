# Cinematics: shot lists

Every cutscene must support pause, subtitles, skip (Enter: the same end state as watching it, applied once, never
choosing a dialogue option), missing-audio fallback (lines then time from their text length) and a clean return
of the camera and controls. Branching state lives in `SaveData`; cutscenes only read it.

## The Curator: Welcome (prologue, before Act I). Implemented in `CuratorInterlude.Welcome`
Rebuilt 19-20 Nov 2026 (`Docs/CURATOR.md`). Lenses are focal lengths on a Super 35 gate and never zoom inside a shot;
framing is solved from the lens and his animated eyes; his eyeline is the investigator across the table, the lens only
where marked. Exit and skip as before: candle out, fade, camera back in Arjun's head, Act I.

| # | Beat | Lens / size | Camera | Performance | Sound |
|---|---|---|---|---|---|
| 1 | One light in the dark | 85 mm f/2, flame close-up to a close shot of his face | Pulls back and up over 5.4 s, focus from the flame to his eyes | Eyes on the flame, then up the hall | Match (placeholder) |
| 2 | The hall comes up | 24 mm f/8, high composed wide | Slow descent | Hall candles light outwards; he sets off; a distant flash in the high windows | Distant thunder |
| 3 | The walk | 35 mm f/4, wide-medium | Side track at his height | Stride-matched walk | Footsteps from his feet |
| 4 | The bookcase | 32 mm, wide-medium, locked | Along the shelves | He stops, steps round to face the shelf | |
| 5 | The reach | 35 mm f/2.8 over his left shoulder | Held | Fingertips hooked over the spine | |
| 6 | The book | 40 mm f/4 insert from the side | Held | Tips it out, takes it by the top of the spine, draws it | Book taken |
| 7 | He turns to us | 40 mm, medium | Held | Steps round, the book's cover towards us | |
| 8 | "There you are." | 75 mm f/2, close-up, slow push | | Into the lens (the only direct look) | `cur0_01` |
| 9 | The introduction | 55 mm medium close-up, 29 deg round from shot 8 (a new angle, not a jump cut) | Steady | To the investigator | `cur0_02`, `03` |
| 10 | Down to his table | 35 mm, leading him | Backs away in front | Walks and talks | `cur0_04` |
| 11 | The book on the velvet | 28 mm high, from our side | Held | Bows; lays it far edge first and lets it tip flat; fingers slide out; lifts the cover open | Book down, cover |
| 12 | The welcome | 60 mm medium close-up | Steady | | `cur0_05`, `06` |
| 13 | Her things | 40 mm insert, from our side, on the book where it lies | Held | Pinches the page corner and turns it; the page carries its words over and lands above the cover | Page, `cur0_07` |
| 14 | The phone, the number | 50 mm three-quarter | | | `cur0_08`, `09` |
| 15 | The note; the storm | 40 mm medium from his right | Held through the strike | Stops; eyes, then head, to the window; brows up; waits for the thunder | Thunder ~2.8 s after the flash, `cur0_10`, `11` |
| 16 | The key | 65 mm insert framed from the exhibit's bounds, 35 deg down from our side | Slow drift in | Eyes on it | `cur0_12` |
| 17 | The landing | 85 mm f/2 close, 16 deg off the MCU's axis, slow push | | Intent | `cur0_13`, `14` |
| 18 | Candle out | 65 mm, medium close-up | Slow push | Raises the candle under his chin, last line, rounds his lips, blows; a thin wisp of smoke; the hall goes out in a wave | `cur0_15`, blow (placeholder), boom; rain and room fade with the picture |

The interludes after Acts II-IV (`CuratorInterlude.Return`) use the same vocabulary: an opening wide with a distant
flash, the walk-and-talk, the book laid down and opened, a new set-up every two lines, the exhibit insert when he
names it, a lightning beat on one line per scene (`cur1_06`, `cur2_08`, `cur3_02`), two real page turns for
`cur3_07` (the chapter page, "the hour when cities confess", then "what you can prove"), and the candle. After an
insert or the storm beat the next line always cuts back to a set-up on him.

## Scenes still to write a shot list for
- Act I: Kamala encounter; footsteps, the shadow under the door, the note.
- Act II: Reddy leaving the market; the SUV decision (15 s timed choice).
- Act IV: each route's confrontation or escape.
- Act V: Priya's reveal upstairs; the Gallery casket choice; each ending.
