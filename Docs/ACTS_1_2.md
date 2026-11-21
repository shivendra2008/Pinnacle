# Acts I and II, the market and the two early Curator scenes (21 Nov 2026)

Scope: Priya Kapoor's apartment (Act I), the Chandnipur market (Act II), the Curator's prologue (scene 0) and his
visit straight after Act II (scene 1), up to that visit's call into Act III. Acts III-V were not touched (shared code
changes are scoped to these sequences; the one shared helper that changed, the Arduino status panel, is noted below).

Every result is labelled **measured** (numbers from a scripted run of a player build), **visually checked** (I looked
at stills or footage from a player build) or **pending** (needs a person, a device or time I did not have).

Branch `polish/acts-1-2`, on top of the commit "Docs: refer to commits by title instead of ID". The supplied
`Pinnacle_Acts_1_2.patch` was not yet applied; it applied cleanly and is its own commit. Everything else builds on it.

## 1. How it was checked

`Assets/Scripts/Playtest/PlayRoute.cs` (see `Docs/PLAYTEST.md`) plays the same route in any player build, the original
code included, with a virtual keyboard (and a virtual gamepad for the input tests) and scripted looking. It labels every
frame by section, records CPU, GPU and render counters, checks what the game did, and can record footage with the UI
and sound. The market route: chapter-select entry into Act II, a look at Bhaskar, through the bazaar gate to Vikram,
his conversation, a 180-degree turn to look back through the crowd, walking at the watching woman until she bolts,
following her ride out, her card, the phone, the SUV, the timed choice, the outcome. Machine: MacBook Air M1
(MacBookAir10,1, 8 GB, fanless), macOS, 1920x1080 windowed, quality "PC", look tier Cinematic (the defaults), vSync off.

Footage (git-ignored, on this Mac, 30 fps with the UI and the offline mix; route sections as a subtitle track):

| File | What it shows |
|---|---|
| `Recordings/acts12/after_market/approach/stall_approach_and_choice.mp4` | through the gate to Vikram's stall, the talk prompt, his greeting, the compass at his first challenge, Arjun's line, his reaction |
| `Recordings/acts12/after_suv_final/suv/phone_suv_choice_outcome.mp4` | back at the stall: the greeting cut by the phone, the turn down the lane, the SUV, the timed compass, Hide (to the counter), the outcome, the act's last line |
| `Recordings/acts12/after_act1/act1/act1_latch_and_stair_window_pans.mp4` | slow pans across the broken latch and the stairwell window, a stop on each, backing away |
| `Recordings/acts12/after_prologue/prologue/prologue_skip_into_act1.mp4` | the prologue skipped mid-shot, Act I opening with control, the narration over free movement |
| `Recordings/acts12/after_curator1/curator/curator_visit_skip_handover.mp4` | the end of Act II into the Curator's visit, a skip after 12 s, the hand-over to Act III |

The final release player of this branch is `Builds/acts12/Pinnacle.app` (git-ignored); the route summaries, checks,
frame data, census, Profiler spikes and Curator play-test results behind the tables below are in
`Recordings/acts12/timing/`.

Stills in `Docs/acts12/`: the compass at Vikram's first challenge and at the SUV; VIKRAM CHAI from the front, the
side, behind and at a strap; SABZI from the side; a strap over the rail; contact sheets of the stall approach, the
SUV sequence, the Act I pans and the Curator hand-over.

## 2. Reproduced causes and fixes

| Area | What was reproduced | Fix | Evidence |
|---|---|---|---|
| Market GPU cost | Looking down the lane drew **25 M triangles a frame**; hiding the 3,439 small scanned props (1k Poly Haven scans: a lime 8,100 triangles, a basket 22,276, bananas 46,868, a tea set 40,296) dropped it to **1.3 M**. They all cast the moon's shadow into 4 cascades (4,830 shadow casters). A/B in one development build (consecutive walk runs, before the machine heated): 2 cascades over 28 m took the walk's p95 from 54 to 34 ms; no shadows from small props took it to 29.5 ms | Small props (< 1.2 m) stop casting the moon's shadow (the lane is lit by lamps that cast none); every detailed prop gets a simplified copy for the distance (vertex clustering, ~20% of the triangles) and is dropped below ~0.6% of the screen's height, by screen size so the licence-plate zoom keeps full detail; the market uses 2 cascades over 28 m and puts the pipeline's 4 over 40 m back when it goes | measured |
| Cold entry | Original: the whole lane built in one frame (a 0.9-2.1 s stall), then 15.3-16.5 s before control. With the patch's frame-by-frame build the camera kept rendering the half-built lane behind the loading card on every loading frame: 6.4 s to build | The camera draws nothing behind the card while the lane is built, then two full frames still under the card for first-use shader work. Control now **3.1 s** after the request | measured |
| Hitch mid-walk | The reflection probe re-rendered itself 0.6 s and 3 s after it was created; with control handed back early the second render landed during the walk (a hitch and a jump in the reflections) | Rendered once, after the cast is in, behind the card | measured: no frame over 80 ms in the first 6 s of free play in any after run (base: six, up to 108 ms) |
| Vikram out of frame | The route's framing check failed: during the conversation the held player was **shoved 2.5 m before the first decision and 12 m by the second** (walkers walking into his capsule; his controller's collision resolution pushed him down the lane), and on another run **lifted 1.4 m**: the camera kept its aim while it moved, so Vikram left the frame | Walkers step round Arjun as round a parked vehicle; while a conversation, the phone call or the SUV choice holds him, he stays exactly where he stood (and is put back on the street if he was lifted) | measured (route check) |
| Stall signs | The patch's board hung centred on the front poles: the bamboo rail ran through the top of the tallest boards, the stall bulb was hidden behind the board and lit only its back, and the shared awning mesh sagged 15-20 cm *through* the front rail. The lettering was a self-lit, double-sided sprite (full brightness at night, no fog) | A market awning that rests on its four rails and droops past them; a 24 mm board cut to its lettering, hung a hand's width under the rail on two jute straps looped over the bamboo and nailed to its top edge; the lettering is lit, alpha-to-coverage paint 1.5 mm proud of the face, single-sided (plain wood from behind); the bulb hangs on its flex beside the board (Vikram's at the stove end, out of the conversation's frame) so its light falls across the paint, the goods and the vendor | visually checked (stills) |
| Shop and gate signs | Shop sign frames floated 9 cm off the wall; the Renewal Project hoarding floated 27 cm off the gateway wall with its letters 7 cm in front of it; the OM plaque floated above the shrine facing the wall (mirrored from the lane) | Frames meet the wall; the hoarding is bracketed back to the wall with its letters on its face; OM is painted on the shrine's base, facing the lane; shuttered shops' signs get their tube's light | visually checked (stills) |
| Windows | Lit upper windows showed as a 14 cm sliver between closed shutters (a bright line that crawled in motion); the stairwell pane clipped to white with a bloom halo; the head of Priya's window overlapped its jambs with coplanar, separately textured faces (shimmering corners in motion); the broken latch hung in mid-air in the open window | Shutters folded back so a lit window reads as a window; the stair pane's emission brought under clipping; the head fits between the jambs; a mullion carries the latch and its keeper | visually checked (stills and the pan footage); the window corners in motion still want a closer look |
| Kettle | The stove's flame light sat 6 cm from the aluminium kettle and burnt it to white | The glow moved off the kettle's skin and lowered | visually checked |
| UI clutter | The Arduino controller's debug panel ("Status: DISCONNECTED", stick values) was drawn over the objective box in every act | Shown only while a controller is connected; F3 toggles it (shared code, all acts) | visually checked |

## 3. Decision compass (Acts I and II)

`StoryCompass` replaces every numbered panel: Kamala's questions, the "move ahead / keep searching" choice, Bhaskar's
optional questions, both parts of Vikram's interrogation, and the SUV. A small brass dial (drawn procedurally: rim,
enamel face, engraved bearings, a tapered needle) sits low in the frame (79% down) so the witness's eyes stay clear;
short intentions sit at its bearings ("Show the note", "Appeal to him", "Threaten police" at Vikram's first challenge,
each still mapped to its original line and trust result); the focused bearing previews the line Arjun will actually
say; asked questions carry a tick, gated ones a padlock and, when focused, the reason ("Ask him about Priya first.").
Relative mouse or trackpad movement turns the needle (a dot shows where it points), so do the arrows, either stick and
the d-pad; click, E, Enter, Space or the pad's south button commits, 1-4 answer directly. Presses in the first 0.22 s
are ignored (left over from skipping a line), the chosen bearing stays lit for half a second after the commit, and
ordinary questions wait; the SUV keeps its 15 s clock (brass marks going out round the rim), its three outcomes and its
walk-on timeout. Spoken lines show as phrases in step with the voice on a soft backing.

## 4. Pacing

| Moment | Before | After | |
|---|---|---|---|
| Act I: opening to first control | ~30 s, locked through the title, three narration lines, Kamala's welcome and two camera turns | **1.20-1.22 s**; the title, narration and welcome play over free movement; examining a clue or Enter stops them at once, and Mrs. Kapoor's welcome is left out if he is already in the room | measured |
| Act II: request to first control | 15.3-16.5 s | **3.1 s**; Arjun's opening line runs while walking and stops when Bhaskar is approached (the patch) | measured |
| Reply to the next decision (Vikram) | | 0.16-0.24 s | measured |

Also: Vikram holds for 0.75 s after Arjun's challenge so his reaction reads before he answers; on Arjun's return he
greets him as a customer ("Chai? Cutting or full?", cut at 1.27 s where the recording pauses) and the stall phone cuts
across him; the "SUV is crawling down the lane" caption over a shot of the SUV is gone; the notes key is taught once;
the door and note toasts that repeated the subtitle and the [E] prompt are gone; the clue card stays up for its
reading time (about 17 characters a second) as well as its voice; the black between the acts is shorter.

## 5. Sound

The market radio plays from a transistor radio on the recharge stall's counter, fully positional (it floated 3 m up,
mostly unpanned); the phone rings from Vikram's stall and horns from their vehicles (the patch); the evening bed drops
3 dB under speech as the music and vendors already did; in Act I the approaching steps move along the landing from the
stairwell to the door, and the creak comes from the door's hinge. Recorded foley used: the Gallery's wooden steps.
Still synthetic: the door creak, the horn, the phone, the engines (no recordings in the project; replacing them needs a
download, so it is listed as pending).

## 6. The Curator's two early scenes

The patch's changes (scene 0 and 1 own the room effects, thunder's low-pass no longer leaks into other sounds, the
skip clears the line state, a cut snaps the focus, the camera's pre-scene rotation is given back, the ambient is only
written when it changes, tighter waits, no doubled thunder) were reviewed in code and run through the existing
Curator play-test (`PINNACLE_PLAYTEST_MODE=curator`, 56 checks: pauses while walking, holding the book, speaking and
during the lightning; a skipped line; scene skips at five beats with the camera, lens, near clip, depth of field, focus,
controls, time, sound, ambient light and fog given back; missing audio).

- First run on this branch: **55 of 56**. Paused during the prologue's lightning, the Curator's mouth crept (0.1% over 40
  frames, past the test's 0.001): while paused he is re-posed with a 1e-6 s step and the viseme smoothing advanced by
  it; the patch's tighter prologue put the lightning right after a line, while the mouth was still settling.
  Fixed in `HeroFace`: nothing advances on that keep-alive step. Second run: **56 of 56** (measured).
- Scene 1 from the market, skipped after 12 s: ends once, hands over to Act III exactly once, camera back on the player,
  time, sound and pause given back (route, measured); footage of the hand-over (visually checked: one fade, one title).
- Still to look at by eye in motion: his eyes, fingers, the book's spine and the candle at each cut in both scenes, and
  the candle against his face in the closest shots (the stills I checked are exposed on the face, not burnt out).

## 7. Market timing, before and after (measured)

Same route, same machine, same settings: MacBook Air M1 (MacBookAir10,1, Apple M1 GPU, 8 GB), 1920x1080 windowed,
quality "PC", look tier Cinematic, vSync off, release players of the original code (`base`) and of this branch
(`after`). Wall-clock frame times. Cold entry = from the chapter-select request to the first controllable frame;
steady play = Bhaskar, the gate, Vikram's stall and conversation, the look back, Reddy, the card; SUV = the phone,
the SUV's arrival, the choice and the outcome. Interleaved runs (base, after, base, after, base, after), each after a
90 s settle and a wait for the load average to fall under 4.

| Run | First control | Entry worst | Steady median | Steady p95 | Steady p99 | Steady > 50 ms | SUV median | SUV p95 | Triangles (walk) | Shadow casters |
|---|---|---|---|---|---|---|---|---|---|---|
| base 1 | 16.02 s | 1470 ms | 55.7 | 77.5 | 94.7 | 1325 of 1831 | 59.4 | 81.3 | 26.0 M | 5111 |
| after 1 | **2.29 s** | 275 ms (under the card) | **43.0** | **56.1** | 62.9 | 337 of 2289 | **48.2** | **60.9** | **2.7 M** | **1442** |
| base 2 | 15.82 s | 1194 ms | 55.2 | 79.3 | 93.7 | 1304 of 1816 | 58.3 | 74.9 | 25.7 M | 5160 |
| after 2 | **2.45 s** | 327 ms | **47.4** | **58.8** | 68.6 | 701 of 2093 | **50.4** | **64.8** | **2.8 M** | **1439** |
| base 3 | 15.73 s | 1172 ms | 55.6 | 79.3 | 92.2 | 1337 of 1827 | 57.0 | 74.3 | 26.0 M | 5149 |
| after 3 | **2.90 s** | 459 ms | **46.3** | **63.9** | 72.8 | 732 of 2121 | **47.9** | **62.2** | **2.9 M** | **1440** |
| base, 5 min idle first | 16.03 s | 1411 ms | 52.3 | 84.1 | 97.1 | 1105 of 1862 | 52.1 | 68.6 | 25.4 M | 5115 |
| after, 5 min idle first | **2.31 s** | 300 ms | **40.0** | **52.0** | 63.3 | 167 of 2468 | 46.3 | 68.2 | **2.8 M** | **1438** |

In matched pairs the after-build is 15-23% faster at the median and 19-38% faster at p95 (frames over 50 ms in steady
play: 1105-1337 per run before, 167-732 after), with ~89% fewer triangles
and ~72% fewer shadow casters; control arrives in under 3 s instead of 16, and the entry's stalls are hidden behind the
loading card. **Neither target is met in these runs**: by then the fanless M1 had been building and playing for hours
and was throttled, with macOS's ANECompilerService and a browser (40-50% of a core) running throughout. The same base
build on the same route, measured cool at the start of the session, ran at 25.4 ms median / 38.1 ms p95 in steady play
(`base smoke`), and an hour of idling later Spotlight indexing (140% CPU) had taken over, so no clean cool pair of the
final builds exists; the A/B runs from the cool period put the two main fixes alone at 34.4 ms (2 cascades) and 29.5 ms
(no small-prop shadows) p95 for the walk. CPU is not the limit: in a development build the walk spends 0.9 ms culling,
0.5 ms in animators, 0.1 ms skinning, 0.2 ms physics and 0.3 ms in scripts per frame; the rest is waiting for the GPU,
and once throttled the remaining cost is fill (4x MSAA plus SMAA, full-resolution SSAO, motion blur, bloom). If the
33.3 ms p95 must hold on this machine when hot, the next steps are 2x MSAA and half-resolution SSAO in the market
(both global renderer settings today; not done, pending your call on the look). GPU times reported by Metal's
frame-timing API exceeded the wall-clock frame and are not used.

Profiler spikes over 50 ms (development build): at entry, asset loading (`Loading.ReadObject`, up to 206 ms), first
draws (`RenderLoop.ScheduleDraw`) and the world-building coroutines, now all behind the card; in free play, the
reflection probe's delayed re-render (`CoroutinesDelayedCalls` 74 ms at 3.7 s; removed) and frames where the main
thread waits on the GPU (`Gfx.WaitForGfxCommandsFromMainThread`).

## 8. Playthroughs (after-build, scripted, every check printed by the route)

| Run | What it covered | Result |
|---|---|---|
| Market, fresh Act II (chapter select), "Show the note", Hide | the whole route, Vikram in frame at both decisions (viewport 0.50, 0.54), trust WARMING, Reddy's escape, card, phone, SUV, outcome, act complete | 12/12 pass (measured) |
| Same, carried into the Curator's visit, skipped after 12 s | the visit follows the market, ends after the skip, hands over to Act III **exactly once**, camera back on the player, time, sound and pause given back | 17/17 pass (12 market + 5 Curator, measured) |
| Market from a saved game (Bhaskar and Vikram done) | resume path, Reddy, card, SUV | 9/9 pass (measured) |
| "Threaten police", read the plate, input tests | trust ANGRY (partial answers), plate saved; Bhaskar's gated question not answered by its number, the gamepad stick + south button choose, a key held across the dial's opening does not answer, arrows + Enter choose; Escape inside the SUV choice pauses it, a key is ignored, the clock stands still (13.49 s -> 13.41 s left over 3 s), resume returns to the open choice | 21/21 pass (measured) |
| "Appeal to him", walk calmly | trust WARMING, walk outcome | 12/12 pass (measured) |
| "Show the note", no answer | the 15 s window runs out, walk-on timeout (result 1) | 12/12 pass (measured) |
| New game, prologue skipped after 4 s | Act I's opening hands over control **1.20-1.22 s** after it begins; the narration is audible over free movement; Enter stops the voices and their subtitle at once | 4/4 pass, plus the timing (measured) |
| Act I resumed | slow pans across the latch and the stairwell window, stop, back away | footage (visually checked) |

Not automated: finding all nine clues and Kamala's interview in one fresh playthrough (the existing play-test aims at every
clue; a person should play Act I end to end once), the door suspense beat and the note (footage pending).

## 9. Still needs a person (pending)

- **Listening.** Headphones and laptop speakers: the radio, horn, phone and engines by distance and side, Reddy's ride
  past both ears, the hallway steps and the creak, dialogue over the radio and the vendors. I checked placement in code
  and the routes check that nothing is left playing, but I cannot listen.
- **Recorded foley** for the door creak, the horn, the phone and the engines (still synthesised; the project has no
  licensed recordings of them and fetching some is a download for you to approve).
- **Control feel.** The compass with a real mouse, trackpad, gamepad and the Arduino (the routes use virtual devices);
  the mouse turns the needle at 28 px per unit, which may want tuning.
- **Act I by hand.** All nine clues, Kamala's interview, the door beat and the note in one sitting; and whether skipping
  Mrs. Kapoor's welcome when Arjun is already in the room loses anything you want kept.
- **The Curator's two scenes shot by shot** at full length (I reviewed the transition and the pause/skip results, not
  every cut of both scenes by eye).
- **Motion review** of the latch, the window corners and the upper windows (fixed at the source; I looked at stills and
  the pan footage, not every angle).
- **Timing on a quiet machine.** The M1 throttles over consecutive runs and macOS's ANECompilerService and a browser were
  loading it throughout; and the provisional target PC (GTX 1660 / Ryzen 5 3600) is still unmeasured.
- **The kettle and the steel glasses** at Vikram's stall are still primitives (white cylinders); faces, children and
  hands are as before (see `POLISH_CHECKLIST.md`).
- **Creative calls** I made that you may want back: Mrs. Kapoor's welcome is skipped once Arjun is inside the room; the
  phone now cuts across Vikram's "Chai? Cutting or full?"; the phone call starts within 4.5 m of the stall (was 11 m, so
  "Return to Vikram's stall" now means returning); the SUV's "A black SUV is crawling down the lane" caption is gone.
