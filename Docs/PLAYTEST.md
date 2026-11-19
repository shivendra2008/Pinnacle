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
