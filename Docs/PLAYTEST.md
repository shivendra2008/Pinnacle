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
