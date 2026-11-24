# Automated play-test

`Assets/Scripts/Playtest/PlaytestDriver.cs` plays the real game in the Unity Editor (batch mode) and records
results and camera images. It does nothing unless `PINNACLE_PLAYTEST=1` is set, so it never affects normal play.
It writes its saves to `PINNACLE_SAVE_DIR`, never to your real save.

What it covers:
1. **Curator prologue:** 10 frames of the opening shots, then a skip. Checks the camera, controls, time scale and
   audio come back and Act I resumes.
2. **Act I clue hints:** from up to 36 standing positions around every clue, aims at 7 points on it. For each aim
   it records whether the hint picks that clue, with the current and the previous targeting. It also captures one
   image per clue with the hover light on.
3. **Kamala's face:** an image with the eyes open and one mid-blink; measures the eyelid gap.
4. **Pause:** a virtual E press is ignored while paused and works after.
5. **Saves:** a backup is kept; a corrupted save file is recovered from it.
6. **Curator interlude after Act II:** 8 frames, then a skip; checks the exit runs once and the camera returns.

Limits: images come from the game camera, so on-screen UI (the hover ring and tag, subtitles) is **not** in them.
It does not play Acts II–V.

## Run it
The open Editor locks the project, so run it on a clone (APFS clones take almost no space):

```bash
S=/tmp/pinnacle_pt && rm -rf $S && mkdir -p $S/copy $S/saves $S/out && for d in Assets Packages ProjectSettings Library; do cp -cR $d $S/copy/; done
```

```bash
S=/tmp/pinnacle_pt && PINNACLE_PLAYTEST=1 PINNACLE_SAVE_DIR=$S/saves PINNACLE_PLAYTEST_OUT=$S/out /Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath $S/copy -executeMethod PlaytestHarness.Run -logFile $S/unity.log
```

Results: `$S/out/results.txt` and the `.jpg` frames. The exit code is 0 if every check passed.

## The Curator scenes (20 Nov 2026)
All of these use the same clone and `-executeMethod PlaytestHarness.Run`; the harness now waits until the copy has
finished compiling before it presses Play (a recompile during Play used to drop the recorder's files).

Make the clone with `cp -c -R Assets Packages ProjectSettings Library <clone>/` and then `find <clone> -exec touch -c {} +`:
`cp -c` keeps the files' old dates, and the macOS temp cleaner (and BuhoCleaner) delete files in /private/tmp that
look older than ~3 days, out from under a running Unity (it broke a build and two recordings on 20 Nov 2026).

Last run (20 Nov 2026): curator mode 56/56; all eight capture segments recorded; see `Docs/CURATOR.md` section 4.

| Variable | What runs |
|---|---|
| `PINNACLE_PLAYTEST=1 PINNACLE_PLAYTEST_MODE=curator` | Curator checks: pause while walking / holding the book / speaking / during lightning (nothing moves, the voice and mouth hold, nothing jumps on resume), a skipped line leaves the mouth at rest, scene skips at several beats (exit runs once; camera lens, near clip, depth of field, focus, controls, time, sound, ambient light and fog all given back), a line with no audio does not move the mouth |
| `PINNACLE_CAPTURE=prologue,int1,int2,int3,gallery,verdictJ,verdictS,verdictR` (+ `PINNACLE_CAPTURE_OUT`, `PINNACLE_CAPTURE_RES=1280x720`) | Footage with sound at a fixed 30 fps (offline audio in step), subtitles, per-frame tracking; then `python3 Tools/review_capture.py <out>/<segment>` for the MP4 and the numbers. Test benches: `lab_walk`, `lab_hands`, `lab_face`, `lab_storm` |
| `PINNACLE_PROBE=Business_Male_04` (+ `PINNACLE_PROBE_OUT`) | What each face bone moves on the skin, the walk clip's stance speed and contacts on the scaled character |

Footage from the capture says nothing about frame rate (time is stepped). Performance is measured in a player build:
`PINNACLE_BUILD_OUT=<dir> Unity -batchmode -projectPath <copy> -executeMethod BuildPlayer.MacOS`, then
`PINNACLE_PROFILE=1 PINNACLE_PROFILE_OUT=<dir> PINNACLE_SAVE_DIR=<dir>/saves <dir>/Pinnacle.app/Contents/MacOS/Pinnacle -screen-fullscreen 0 -screen-width 1920 -screen-height 1080`
(the save folder keeps the new game it starts away from the real saves).
See `Docs/CURATOR.md` for what was measured.

## Scripted routes in a player build (21 Nov 2026)
`Assets/Scripts/Playtest/PlayRoute.cs` plays the same route in any build (the original code too), with a virtual
keyboard and scripted look, and quits when done. It does nothing unless `PINNACLE_ROUTE` is set. Build the player
with `BuildPlayer.MacOS` (add `PINNACLE_BUILD_DEV=1` for a development player whose Profiler markers the route records),
then launch the binary directly so the environment reaches it:

```bash
PINNACLE_ROUTE=market PINNACLE_PROFILE_OUT=/tmp/run PINNACLE_SAVE_DIR=/tmp/run/saves Pinnacle.app/Contents/MacOS/Pinnacle -screen-fullscreen 0 -screen-width 1920 -screen-height 1080
```

| `PINNACLE_ROUTE` | What it plays |
|---|---|
| `market` | Act II from chapter select: cold entry, look at Bhaskar, walk through the bazaar gate to Vikram, talk (first answer as `PINNACLE_ROUTE_TRUST=note/appeal/threat`, then thank him), turn and look back through the crowd, walk at the watching woman until she bolts, follow her ride out, pick up her card, the phone, the SUV, the timed choice (`PINNACLE_ROUTE_OUTCOME=hide/walk/plate/timeout`), the outcome. `PINNACLE_ROUTE_RESUME=1` enters from a mid-act saved game; `PINNACLE_ROUTE_CURATOR=1` carries on into the Curator's visit and its hand-over to Act III |
| `walk` | The market's entry, walk and look back only (quick A/B runs) |
| `act1intro` | A new game: the prologue skipped, Act I's opening timed, its voices checked over free movement (the recorded opening, its subtitles timed on his words), then Mrs. Kapoor's questions: Arjun asks one in his recorded voice before she answers |
| `act1` | Act I resumed: slow pans across the broken latch and the stairwell window, a stop, backing away; a sidestep along the sill at eye height, the latch up close, the stairwell window from the far end of the landing; pans past every flat's nameplate from inside Priya's room and along the landing (checks every label uses the depth-tested text material) |
| `act1door` | Act I with all nine clues: the steps up the landing, the door swinging shut, a pause and resume during the steps, opening the door (a second E straight after), the note, a skipped line of the finale; reads the door's sound log (latch, hinge start/stop, frame contact: what played, where, how often) |
| `kamala` | Act I with two clues: walks to a third and examines it, Mrs. Kapoor comes in; her question, the compass (cursor capture, J, a pause with a number pressed behind it, resume), the needle swung by a virtual mouse to every bearing, a click, Arjun asks, her answer; lip-sync on her question, her silence while he speaks, her answer and its cut-off; a held Space through the end of her answer |
| `act3` | Act III from a save at the temple: the Pandit's question on the compass (`PINNACLE_ROUTE_PANDIT=0/1/2`), the letter, the gate, the call, the crossroads (`PINNACLE_ROUTE_BRANCH=0/1/2`: Café Irani, the Kapoor house, Inspector Reddy), the Curator's visit skipped, Act IV opening at the chosen place; branch 0 walks on into the café |
| `act3full` | Act III fresh from chapter select: the guard, the computer password, the basement, the cage, the calendar, the drawer code, the key, the cage, the temple, the Pandit, the crossroads |
| `cafe` | Act IV at Café Irani from chapter select (cold entry timed): from the entrance to the counter, the biscuit case, the owner, the street from the counter and the terrace; stills of the case from the side and of the street from behind the owner; checks the owner's hand stays on his cup while carrying it and the cup stands on the worktop after |

`PINNACLE_ROUTE_POINTER=mouse` answers the market's dials with the virtual mouse too (the `kamala` and `act3` routes
always do). `python3 Tools/route_timing.py <run dirs>` prints cold entry and play separately for market and café runs
(median, p95, p99, worst, frames over 50 ms).

Every route writes `voices.txt`: each clip that starts playing (time, source, clip name, its length), so a run shows
which recording of each line the build played. `PINNACLE_ROUTE_CAPTURE=1 PINNACLE_ROUTE_AUDIO_ONLY=1` records the
route's mix to `route/route.wav` without frames (to transcribe what was heard; footage costs ~1 GB per route).

Outputs in `PINNACLE_PROFILE_OUT`: `summary.txt` (cold entry, steady play and the SUV as separate samples: median,
p95, p99, worst, frames over 50 and 100 ms; CPU main-thread and GPU times; render counters by section), `frames.csv`,
`checks.txt` (what the route saw the game do: decisions offered, trust result, Vikram in frame at each decision,
reply-to-decision gaps, saves, outcomes), `markers.csv` and `spikes.txt` in a development build. Options:
`PINNACLE_ROUTE_SHOTS=1` stills (not for timing), `PINNACLE_ROUTE_CENSUS=1` triangles and shadow casters per mesh,
`PINNACLE_ROUTE_EXP=cascades=2,msaa=2,propshadows=0,...` A/B switches applied once the market is built,
`PINNACLE_ROUTE_CAPTURE=1` footage with the UI at a fixed 30 fps and the mix rendered offline (then
`python3 Tools/route_video.py <dir>` for the MP4).

The reference machine is a fanless MacBook Air M1: it throttles under sustained load, and other processes (browser,
macOS services) move the numbers. Compare builds with interleaved runs (base, after, base, after...) after a settle
wait, and keep the load printed with each run.

## Acts IV and V (23 Nov 2026)

`Assets/Scripts/Playtest/PlayRoute.Acts45.cs`. Each route seeds its own campaign in `PINNACLE_SAVE_DIR`, enters from
chapter select and uses the virtual mouse for the compass. Checks that need a field only newer builds have become
notes in older builds, so the same route runs on the committed game (the "before" player) and on this pass.

| `PINNACLE_ROUTE` | What it plays |
|---|---|
| `cafeminute` | Café Irani from control to Raj's reaction in one take: the walk to the counter, a sidestep along the biscuit case (A then D, eyes on the case), "Sit down with Raj", his lines, the first decision on the compass ("Trust him?"), five seconds of his reply. Capture segment `cafeminute` |
| `cafefull` | `cafeminute`, then "Time to go", the danger beat (`PINNACLE_ROUTE_LOOK=1` looks at the watcher with the mouse, `0` does not: the camera turns only then), the kitchen (a pot brushed on purpose: it rattles, footsteps, the searcher at the door; standing in his torchlight: a detection and the checkpoint; cover to cover to the exit), the alleys (caught three times in the open, each retry checked clean; then hidden: Raj crouched and still), the dhaba (ally, recordings and location lead saved whichever questions were asked) |
| `house` | The landing and flat 301's nameplate, Kamala, the boot print and the visitor pass, the laptop read (no confession), the laptop closed (Rohan stops, Kamala looks at him, then silence until the player acts), "Ask Rohan about the emails", the compass, the ally |
| `reddy` | `PINNACLE_ROUTE_EVIDENCE=full` (5 pieces: ID, sheet, files, cuttings, plate), `noid` (4), `heard` (no plate, Bhaskar's account heard: 4 + ID), `minimal` (sheet and files only). Space held across the folio's opening, a number (inspects only), a number and Enter together (nothing presented), pause with keys behind it, resume, Backspace, sideways mouse and a click on a weak piece (her answer is the right line), arrows and Enter on the sign-out sheet (she recognises her signature), a locked piece, the d-pad and the south button; the last question on the compass; the ally |
| `act5` | `PINNACLE_ROUTE_CASE=j18` (18 evidence, recordings), `j17` (17), `rec` (recordings only), `emails` (emails only), `neither`; `PINNACLE_ROUTE_ENDING=0/1/2`. The shelter (the stair voice, Priya shuts her file and holds it before her first line), the Gallery checkpoint, every other casket's card (sealed or open as `Sealed()` says), a step back, the chosen casket on the compass, the ending (cards closed), the credits, R to the menu. `PINNACLE_ROUTE_RESUME=1` enters at the Gallery checkpoint; `PINNACLE_ROUTE_QUIT=1` quits to the menu before committing and continues from the checkpoint (no ending recorded) |

`PINNACLE_ROUTE_FAST=1` skips spoken lines with Space where the run is about logic. `act3full` with
`PINNACLE_ROUTE_SHOTS=1` now also takes stills of the two nearest bookcases, the cage with the sign-out sheet, the murti
(straight on and three-quarter at a devotee's eye height), the Ganesha shrine and Harishankar's table.
