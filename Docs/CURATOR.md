# The Curator scenes: how they are built, what was measured, what is still missing

Covers the prologue (scene 0), the interludes after Acts II-IV (scenes 1-3), the Act V Gallery and the three ending
verdicts. Started 19 Nov 2026 from the commit "Play-tested fixes, Indian Hindu cast, and an automated play-test harness". Unity 6000.5.9f1, URP 17.5 (Forward+), no HDRP.

Everything below marked **measured** comes from recordings of the real scenes (see "Tools" at the end) or from
the rig probe; **designed** means written and compiled but not yet seen in a recording.

## 1. Baseline (before), measured

Recorded with `CuratorCapture` (fixed 30 fps, offline audio in sync) on the code of that commit.

| Area | What the footage and the tracking data showed | Kind |
|---|---|---|
| Feet | Planted feet slid in 8% of their frames in the prologue (5.7 m per foot) and in 2-4% in the interludes (1.5-1.9 m per foot); the walk clip's stance foot travels 1.69-1.83 m/s on this skeleton but the code assumed 1.3 m/s, so at 0.8 m/s the feet skated backwards ~0.2 m/s; 61 frames of turning on the spot with no steps | A reproduced, B cause in `CharacterNPC.RbClips` |
| Hands | The candle and the book were parented to the IK goals, not held by the hand: the candle stayed 62-68 mm from the wrist only because the goal carried it (no grip, no wrist orientation, no fingers); the book sat 0.10-0.35 m from the hand that was meant to hold it | A, B |
| Face | Jaw opened with the voice's loudness (gaping mouth on every loud syllable); blinks moved the lid bones but the lids did not close (the upper-lid bone owns ~25 vertices at 68% weight, measured by the rig probe) | A, B |
| Camera | Prologue: 23 shots (median 6.1 s), 16 of them animating the field of view; interludes: 11-14 shots, 6-10 animating it (a cut is a camera jump > 0.4 m or a one-frame lens change). The head cropped at the eyes in the final close-up; the Curator's head turned to every new camera | A |
| Light | Gallery volume: exposure +1.15, contrast 30, bloom 0.95 at threshold 0.85, red vignette 0.42, grain 0.4, aberration 0.14. Faces blown out orange near the candle, the hall black; grain and aberration stayed on in "Performance" because the Gallery's own volume overrode the F2 tiers | A, B |
| Sound | All synthesised (drone, ticks, noise bursts); footsteps on a 0.62 s timer; TTS dialogue | B, C |
| Model | Rocketbox `Business_Male_04`: head 1,710 vertices / 3,076 triangles, 1024 px textures (importer cap), no blendshapes, one material for skin, lips and eyes, no hair/lash cards; the suit tint multiplied the hands (shared body texture) | C |

## 2. What was built

**One owner per thing, one order per frame** (after the motion-capture clips): locomotion → feet → spine →
head and eyes → hands → props → face. Scene scripts only set goals.

### Body: `Assets/Scripts/CharacterNPC.Hero.cs` (the Curator only; every other character keeps the old path)
- **Locomotion, one authority**: the script moves him; the walk clip plays in place and its phase follows the
  distance walked (a stride wheel calibrated on this skeleton at this scale when he is created). Acceleration and
  braking limits (he stops on the spot, no snapping), turning along the path, never moving sideways.
- **Feet**: a foot that the clip plants is locked in the world (two-bone leg IK, knee towards the toes); only the
  middle of the stance is locked so heel strike and toe roll stay animated; a lock that would over-stretch the leg
  lets go; the pelvis drops at most 3.5 cm. Standing still, the body can turn and the feet **step** to follow.
  Every landing raises `FootContact` (the footstep sound).
- **Hands**: goals are palm poses (palm frame from the hand's own bones: +Z along the fingers, +Y out of the palm).
  Wrist position **and** orientation, roll shared between forearm and hand (no twist bones), bend limited, elbow
  direction per goal, a collar-bone assist at the reach limit, a curved path out from the body and over the table.
  Finger poses per grip (`Fingers.Dish`, `Hook`, `Edge`, `Pinch`, ...) from the bind pose's own curl axes.
- **Props**: a prop held in a hand follows that hand after IK. Taking it records where it is in the hand as the hand
  really is (not the IK target: at the high shelf the wrist runs out of bend and the palm stops ~10 cm short, which
  made the book jump into the hand); `Regrip` then slides the hand into the proper grip; `PalmFor` places it.
  The book is laid down as people do it: far edge onto the velvet first, the near end still in the hand, then let go
  to tip flat about that edge; it never snaps (a shortfall settles straight down and is logged). Letting a hand go
  fades the IK out from where the hand is (it used to restart from where the last reach began: a 13 cm jump), and
  the collar-bone assist fades with it (unweighted, it snapped the shoulder ~10 cm when the fade ended).
- **Grips** (`StoryBookModel.Grip`): the candle in a chamberstick held from under the dish; the book taken from
  the shelf with fingertips hooked over the spine, tipped out about its bottom edge, then held by the top of the
  spine (one grip from shelf to table: tipped towards him it lies flat, cover up, fingers sliding out from under
  the spine edge); the cover lifted by its free edge; a page pinched at the corner. The book is laid where he can
  reach it from his place behind the table (0.57 m in front of him; at 0.7 m it was out of reach).
- **Pages** (`BookLeaf`): the turned page carries its own words over (the next page is printed under it), curves up
  out of the gutter and lands above the opened cover (it used to slide under the cover and vanish); no page is
  blank.

### Face: `Assets/Scripts/HeroFace.cs`, `Tools/lipsync/`
- **Visemes from the actual audio**: `Tools/lipsync/curator_lipsync.py` checks the VO file is still exactly what
  the TTS pipeline makes (it is, sample for sample), gets word starts from AVSpeechSynthesizer word markers on the
  same voice, DTW-aligns them onto the `say` render (alignment cost 0.000: identical audio), spells the words from
  `curator_lexicon.txt` (British, non-rhotic, written for this script), snaps lip closures (P B M) onto loudness
  dips and F/V onto bursts of frication, and writes `Assets/Resources/VO/Visemes/<id>.txt` (92 lines).
- **Playback**: weights from a coarticulation (dominance) model, read at the voice's real playback position, so
  pause, resume and skipping stay in sync and silence is the rest pose. Poses on the Rocketbox face bones (jaw,
  tongue, upper and lower lips, mouth corners, cheeks).
- **Blinks**: generated eyelid-closing blendshapes on his own copy of the mesh (the lid turns about the eyeball's
  centre, scaled by the lid bone's own skin weights); more blinks at phrase ends; lids follow a downward look.
- **Gaze and brows**: eyes on the eyeline with small saccades, glances (the lightning); brows lift on the stressed
  words the aligner marks; head nods and tilts on those accents and slow noise drift (no sine waves).
- **Materials** (`CharacterNPC.HeroLook.cs`): a smoothness map made from the skin weights (wet eyes that catch the
  candle, glossier lips, a faint sheen on nose and scalp, varied skin), and the suit tint kept off the hands.

### Camera: `Assets/Scripts/CuratorInterlude.cs`
- Shots are built around beats; a shot can hold several lines; cuts come with an action, an insert or a reaction.
- Lenses are focal lengths on a Super 35 gate (`usePhysicalProperties`); the field of view never animates inside a
  shot (moves are dollies). Framing is solved from the lens: shot size (frame height at his distance) and where his
  **animated eyes** sit; focus is on his eyes, snapped on cuts; the camera is pulled in if something is in the way.
- He looks at the investigator (a point across the table), not at whichever camera is live; he looks into the lens
  only on chosen lines ("There you are.").
- An insert (the book, the exhibit) or the storm beat ends its shot: the next line always cuts back to a set-up on
  him (an insert used to stay up for the following line: 19 s on a page). Consecutive face set-ups differ in angle
  by 20 degrees or more and stay on one side of his eyeline, except the lightning medium.
- Exhibit inserts are framed from the exhibit's own bounds (a key, a kettle, a deed and three figures differ in size
  by 4x; fixed offsets framed empty cloth for the kettle and cropped the deed's title).
- Prologue: flame close-up pulling back to his face; a composed wide as the hall lights; a side track down the
  aisle; the bookcase; the reach; the book insert; the turn; close-up direct address; walk-and-talk; the book laid
  down and opened (from our side, so it reads to us); a page turned; a lightning beat (he stops, eyes then head to
  the window, waits for the thunder); the key insert; a slow push-in; the candle raised and blown out.

### Light and storm: `CuratorGallery.cs`, `GalleryStorm.cs`, `Resources/Shaders/RainGlass.shader`
- Warm practicals (candles, chandeliers, two sconces on the bookcase) as key; a dim cold night-sky fill from the
  windows; ambient and wood albedo raised so the dark keeps its shape. The three high windows behind the bookcase
  show cloud lit from below by the town (they rendered black at rest and only showed in a flash; the old ones were
  flat lilac light boxes).
- Dust and the candle's smoke are soft round particles (both were untextured squares, visible in close-up); the
  smoke is a faint, continuous wisp of overlapping puffs that widens and curls as it climbs.
- The Gallery volume is light (contrast 6, saturation -6, bloom 0.22 above 1.25, vignette 0.16, fine grain 0.08,
  no aberration, no lens distortion, no motion blur) and follows the F2 tier (`LookController.Register`).
- `GalleryExposure` meters the light on his face and sets post-exposure slowly within -0.7..+1.5 EV; it ignores
  lightning.
- Storm: rain on every pane (procedural drops and runners, written for this project), rain falling outside the tall
  east window (a real opening), lightning as a cold shadow-casting light through that window (2-3 strokes and an
  afterglow, never a strobe), thunder after distance / 343 m/s. `GalleryStorm` is the only writer of the storm and
  night lights; candles belong to `CandleFlicker` (which now gutters when blown).

### Sound: `CuratorSound.cs`, `Tools/make_curator_sfx.py`, `Resources/Audio/Curator`
- 44 takes cut from Wikimedia Commons recordings (9 public domain, 2 CC BY 4.0; credits in
  `Resources/Audio/Curator/CREDITS.txt` and the credits roll): rain outside (two 43 s loops), rain on the window,
  room tone (the storm through the walls), 3 near and 6 far thunders, 12 footsteps (9 from the wooden floor of the
  Kunsthistorisches Museum's picture gallery), 10 page turns, cover and book sounds.
- Footsteps come from the feet landing; page and cover sounds from the hand beats; thunder from the storm.
  Random takes, never the same twice, small pitch and level variation; partly spatial (0.55) inside a damped hall
  reverb; the beds duck ~4 dB under his voice (attack 0.15 s, release 0.7 s); dialogue stays dry and centred.
- The beds fade out with the picture at the end of a scene, and over 0.3 s when a scene is skipped (they were cut
  off mid-sample when the scene was destroyed: an audible hard stop at -31 dB at the end of the prologue).

### Act V
The Gallery in Act V uses the same hall, storm, sound and hero layers; the Curator's eyeline is the player (he talks
to Arjun in first person); framing turns use his real eye position instead of `position + 1.7 m`.

## 3. Tools

| Tool | What it does |
|---|---|
| `Assets/Scripts/Playtest/CuratorCapture.cs` | `PINNACLE_CAPTURE=prologue,int1,int2,int3,gallery,verdictJ,verdictS,verdictR,lab_walk,lab_hands,lab_face,lab_storm`: fixed 30 fps frames + offline audio in sync, subtitles, a per-frame track (feet, hands, props, head, camera, the hero's foot flags) |
| `Tools/review_capture.py` | MP4 with the cutscene bars and a subtitle track, slow-motion clips, contact sheets, metrics (foot slip while planted, hand-prop distance, prop pops and hand jumps, cuts and lens changes, EBU R128 loudness) |
| `Assets/Scripts/Playtest/RigProbe.cs` | `PINNACLE_PROBE=<avatar>`: what each face bone really does to the skin (BakeMesh), the walk calibration |
| `PlaytestDriver` mode `curator` | pause while walking / holding / speaking / lightning, line skip, scene skip at several beats, missing audio, restoration checks |
| `Assets/Editor/BuildPlayer.cs` | `-executeMethod BuildPlayer.MacOS`: standalone build; keeps the run-time shader variants (`Resources/ShaderVariants`: one material per keyword set, with dummy textures so URP keeps the keywords, read back at build time) |
| `Assets/Scripts/Playtest/ProfileRun.cs` | `PINNACLE_PROFILE=1` (with `PINNACLE_SAVE_DIR`, so the new game it starts never touches real saves) in a player build: real-time frame, CPU and GPU times through the prologue, by section |
| `Tools/compile_check.py` | compiles the scripts with Unity's Roslyn without the Editor |

All of them run on a copy of the project (see `Docs/PLAYTEST.md`); nothing happens in normal play.

## 4. Verification (after)

### 4.1 Review of the first full recording (run 1), and what it changed
Every scene was recorded once with the new code and gone through shot by shot and frame by frame (track data).
It showed problems the earlier bench tests had not; all were fixed in code before the final recording (4.2).
`Docs/curator/run1_problems.jpg` (top left to bottom right: the blank page, the kettle insert, the smoke, the Gallery).

| Seen in run 1 | Cause | Fix |
|---|---|---|
| Act IV: the camera held a book insert for 19 s, over a page that had gone blank | after an insert the next line only cut if it was an even-numbered line; the second page turn printed nothing | inserts and the storm beat always hand the next line to a new face set-up; every page has words |
| A turned page vanished as it landed | the opened cover lies ~12 mm higher than the page block; the page landed under it | the page curves up out of the gutter and lands above the cover; its words travel with it |
| Act II: the kettle insert showed empty table cloth; Act III: the deed's title cropped | one fixed camera offset for four exhibits of very different sizes | inserts framed from the exhibit's bounds |
| The book jumped 13 cm when set down (every scene) and 11 cm when taken from the shelf | set-down snapped it to a spot 0.7 m in front of him (out of reach); taking it measured the grip against the IK target, not the real hand | spot within reach, a deeper bow, and the book laid far edge first then tipped flat (the hand never has to reach the table); no snap: a shortfall settles straight down; grips measured on the real hand, then the hand slides into the proper grip |
| The right hand jumped 13 cm in one frame when letting go (every scene), and 10-12 cm a second later | releasing faded the IK out from where the previous reach *started*; the collar-bone assist ignored the IK weight and switched off when the fade ended | the release starts from where the hand is; the assist is weighted like the arm |
| A near-axial jump cut after "There you are." (75 mm at 0 deg to 55 mm at 3 deg) | the medium close-up's angle | 29 deg round, same side of his eyeline; the closing push-in moved off the MCU's axis too |
| Candle smoke and dust drawn as grey squares | untextured sprite material | soft round particles; the smoke is many faint overlapping puffs (a first fix read as a string of beads) |
| Act V: the three high windows were black | their night sky was far below what the exposure shows | cloud lit from below by the town |
| The rain and room tone stopped dead when a scene ended or was skipped | the sound objects were destroyed with the scene | fade with the picture; a 0.3 s fade on any exit |

The review tool now reports these classes of fault on every recording (prop pops, one-frame hand jumps, lens changes
as cuts), so they cannot come back silently.

### 4.2 Final recordings, before and after (measured)
Every Curator scene recorded on 20 Nov 2026 with `CuratorCapture` on an Apple M1 (fixed 30 fps, 1280x720 after,
960x540 before, offline audio in sync), measured with `Tools/review_capture.py`; before = that same commit, after = the committed code (two later changes
are not in these recordings because no recording exercises them: the pause path, and the build's variant materials). Full
footage with sound: `Recordings/curator/before|final/<scene>/` (git-ignored, ~0.5 GB). Line-matched stills (same
line of dialogue, left before, right after): `Docs/curator/prologue_before_after.jpg`, `act2_before_after.jpg`,
`act4_before_after.jpg`; `gallery_before_after.jpg` (top: Act V from the door; bottom: the Sacrifice ending).

| Scene | | Foot slip while planted (frames; metres, both feet) | Book pops / hand discontinuities | Shots (median) / lens changes inside a shot | Loudness / true peak |
|---|---|---|---|---|---|
| Prologue | before | 8% / 11.45 m | 0 / 2 | 23 (6.1 s) / 16 | -17.8 LUFS / -2.4 dBFS |
|  | after | 1% / 0.54 m | 0 / 0 | 19 (6.4 s) / 0 | -17.8 LUFS / -2.8 dBFS |
| After Act II | before | 4% / 3.61 m | 0 / 2 | 10 (8.4 s) / 6 | -16.6 LUFS / -2.6 dBFS |
|  | after | 0% / 0.16 m | 0 / 0 | 9 (9.9 s) / 0 | -16.7 LUFS / -3.1 dBFS |
| After Act III | before | 3% / 3.43 m | 0 / 2 | 14 (9.4 s) / 10 | -16.7 LUFS / -2.1 dBFS |
|  | after | 0% / 0.16 m | 0 / 0 | 11 (10.7 s) / 0 | -16.6 LUFS / -2.5 dBFS |
| After Act IV | before | 3% / 2.94 m | 0 / 2 | 13 (7.3 s) / 9 | -17.0 LUFS / -2.3 dBFS |
|  | after | 0% / 0.12 m | 0 / 0 | 10 (10.1 s) / 0 | -16.8 LUFS / -2.9 dBFS |
| Act V Gallery | before | 0% / 0.00 m | 0 / 0 | 2 (67.0 s) / 0 | -20.1 LUFS / -6.6 dBFS |
|  | after | 0% / 0.00 m | 0 / 0 | 2 (67.0 s) / 0 | -20.0 LUFS / -6.3 dBFS |
| Ending: Justice | before | 1% / 0.03 m | 0 / 0 | 1 (43.5 s) / 0 | -17.9 LUFS / -3.9 dBFS |
|  | after | 0% / 0.00 m | 0 / 0 | 1 (43.5 s) / 0 | -17.8 LUFS / -3.9 dBFS |
| Ending: Sacrifice | before | 0% / 0.02 m | 0 / 0 | 1 (46.2 s) / 0 | -18.3 LUFS / -4.3 dBFS |
|  | after | 0% / 0.00 m | 0 / 0 | 1 (46.2 s) / 0 | -18.1 LUFS / -3.8 dBFS |
| Ending: Revolution | before | 1% / 0.03 m | 0 / 0 | 1 (43.6 s) / 0 | -18.1 LUFS / -4.1 dBFS |
|  | after | 0% / 0.00 m | 0 / 0 | 1 (43.6 s) / 0 | -18.0 LUFS / -4.0 dBFS |

- **Feet**: slip while planted 8% -> 1% (prologue) and 3-4% -> 0% (interludes); the book is never popped and the hand
  never jumps (the old book could not pop only because it was glued to the IK target, not held).
- **Camera**: no shot changes its lens while it runs (16 / 6 / 10 / 9 did before); fewer, longer shots, each cut on
  an action, an insert or a reaction.
- **Book set-down**: the hand gets the book to within 2.8-3.4 cm of its touch-down pose in all four scenes (logged by
  the scene itself); the rest is the tip onto the velvet. Peak hand speed 2.1-2.5 m/s (the page-corner reach in Act
  IV is the fastest: brisk, not jerky; 4.5-4.7 m/s before).
- **Loudness**: dialogue-led scenes sit at -16.6..-17.8 LUFS integrated with true peaks of -2.5..-3.1 dBFS (the
  Gallery, seen from the door with the Curator 15 m away, is -20 LUFS before and after). The thunder and rain are
  under the voice, not over it.
- Gallery and endings: the camera is the player's own, the Curator stands at the table, so these numbers do not
  move; what changed there is light, materials, sky and face (see the stills).
- What the numbers cannot show was judged from the footage: lip shapes against the words, blinks, the page turn,
  the inserts, the light. Run 1's review (4.1) is where that judgement found the faults.

### 4.3 Play-test suite (measured)
`PINNACLE_PLAYTEST=1 PINNACLE_PLAYTEST_MODE=curator` on the final code: **56 of 56 checks pass** (log:
`Recordings/curator/playtest/`). Pausing while he walks, holds the book, speaks and during a lightning strike freezes
body, voice, mouth and storm and resumes without a jump; a skipped line stops its own voice, the mouth comes to rest
(viseme weight 0.00) and the next line follows; skipping the whole scene at 0.5, 6, 14 and 30 s and at mid-prologue
runs the exit exactly once and gives back the camera (parent, lens, near plane, physical camera), depth of field and
focus, controls, time, sound, the room's ambient light and fog, removes the storm and leaves no interlude; a line
with no audio shows its subtitle and does not move the mouth; no character is left on the hero path.

The first run of the suite failed 6 checks. Two were faults in the test (it read the pose one frame before the pause
took effect; a "skip" flag could leak into the next line). Four were a real fault: **while paused, he sagged into the
bare clip pose and the candle stayed in the air**, because the animation is re-evaluated every frame even at time
scale 0 and his layers skipped those frames. They now re-apply, frozen (`CharacterNPC.RbClips`).

The capture tool itself had a fault that hid two endings: it destroyed the act after a segment, and the player lives
under the act's root, so every ending after the first failed to load. Fixed; all three endings are recorded.

### 4.4 Standalone build and real-time profile (measured)
`BuildPlayer.MacOS` builds the game (0 errors). The first build rendered the Curator's skin wet and mirror-glossy and
every procedural surface without its normal and gloss maps (`Docs/curator/build_variant_fix.jpg`, left): URP's
material validation had stripped the map and emission keywords from the variant materials meant to keep those
variants in the build, so the build fell back to variants without them. The variant materials now carry a small
texture in each slot their keywords need, the build step reads the keywords back (13 materials, 0 keywords lost),
and the build renders like the Editor (right). This affected every run-time-made material in standalone builds, not
only the Curator; it was never visible before because no build had been checked.

Real time, prologue, `ProfileRun` (`Docs/curator/profile_summary.txt`): **MacBook Air M1 (MacBookAir10,1, 8 GB,
fanless), 1920x1080 windowed, quality "PC", top look tier, vSync off**:

| | median | p95 | p99 | mean |
|---|---|---|---|---|
| Frame (wall clock) | 22.9 ms | 25.2 ms | 27.1 ms | 22.3 ms (~45 fps) |
| CPU main thread | 4.3 ms | 5.5 ms | 7.3 ms | 4.7 ms |
| CPU render thread | 2.6 ms | 4.6 ms | 4.9 ms | 3.0 ms |
| Walking / dialogue / lightning (frame) | 20.8 / 23.1 / 24.0 ms | | | |

- GPU-bound: the main thread uses a fifth of the frame. The GPU time the platform reports (~58 ms) is longer than
  the frame itself, so on this Mac it measures latency across frames in flight, not GPU work; it is not used here.
- The lightning (a shadow-casting light while it is lit) costs about 1 ms over dialogue frames.
- The same prologue measured 41.5 ms median (p99 55.6 ms) on the earlier build right after ~2 h of sustained compile
  and capture load, with Spotlight indexing the captures: this machine's state alone roughly doubles the frame time.
- Not met: the provisional target (16.6 ms at 1080p on a GTX 1660 / Ryzen 5 3600 class PC) cannot be judged from a
  MacBook Air; it needs a run on that class of machine. There is no profile of the old build to compare against.

### 4.5 Still short of the reference, seen in the final footage
- **Face and voice**: the mouth shapes follow the words exactly (they are timed from the audio), but the voice is
  TTS with no acting, and the Rocketbox head has no blendshapes, 1024 px textures and mid-poly geometry; close-ups show
  it. These are the two asset blockers in section 5.
- **Hands**: reaching, taking, carrying, setting down, opening and turning are IK and finger poses, now without pops
  or jumps, but they are not motion capture; the page-corner reach in Act IV is brisk. Interaction clips (section 5).
- **Exhibits** are built from primitives (key, kettle, deed, three figures); they read in the inserts, they are not
  scanned props.
- **Sound**: no recorded match strike, candle blow-out or cloth (placeholders are used); the mix was set by
  measurement (loudness, peaks, ducking) and listened to only in these recordings.

## 5. Still missing, and what to get

| Need | Why | Spec |
|---|---|---|
| Recorded performance of the Curator's lines | TTS cannot act; the visemes are exact to the TTS, not a performance | 48 kHz / 24-bit mono WAV, -16 LUFS, file names = line ids in `Resources/VO`; ideally with facial capture (ARKit 52 or similar) of the same takes. Re-run `Tools/lipsync` with a forced aligner instead of the TTS word markers |
| Hero head and body | The Rocketbox head is mid-poly with 1024 px maps and no blendshapes; grading cannot make it photographic | A licensed, scan-based male head (~20-60k tris for the head), 4k albedo / normal / roughness (+ cavity), separate eye, cornea, teeth, tongue and mouth-interior meshes, a groom or no hair, ARKit-52 or FACS blendshapes, Humanoid-compatible skeleton with finger bones; a tailored three-piece suit with its own textures. Import through `RocketboxImport`-style settings (no 1024 cap, blendshapes on), and map its blendshapes in `HeroFace` instead of bone poses |
| Interaction clips | Reach, take, set down, turn a page, raise a candle are procedural (IK + finger poses) | Humanoid FBX, 30 fps, root motion off, contact markers: shelf reach-and-pull, book set-down, cover open, page turn, candle raise and blow |
| Match strike, candle blown out, cloth movement | Not found under CC0 / CC BY / public domain | 3-5 takes each, mono WAV, 48 kHz, close-miked, no room; drop into `Resources/Audio/Curator/match`, `candle_blow`, `cloth` |
| Kenney "RPG Audio" and "Impact Sounds" (CC0) | More book, cloth and wood-step variety | Download in a browser from kenney.nl (the site only gives the file to a browser), unzip, copy the wanted takes into the category folders |
