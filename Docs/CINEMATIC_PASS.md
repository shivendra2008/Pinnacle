# Cinematic pass: the compass, Acts I-III, Café Irani (22 Nov 2026)

Branch `polish/cinematic-pass` (from `polish/acts-1-2`, which `main` had merged as PR #2). Every result below is
labelled **measured** (numbers or checks from a scripted run of a player build), **seen** (I looked at footage or
stills from a player build) or **not verified** (needs a person, a device or a check I could not make).

## 0. Which build to play

| | |
|---|---|
| Project folder | `/Users/user/Pinnacle` (Unity 6000.5.9f1, URP 17.5) |
| Startup scene | `Assets/Scenes/SampleScene.unity` (the only scene in the build) |
| Branch / commit | `polish/cinematic-pass` (the commit is printed on the title menu, bottom left) |
| Player | `Builds/cinematic-pass/Pinnacle Cinematic.app` (double-click it), or its binary `Builds/cinematic-pass/Pinnacle Cinematic.app/Contents/MacOS/Pinnacle` |
| How it was built | `BuildPlayer.MacOS` in batch mode on an APFS clone of the project, with the committed `Packages/manifest.json` (your uncommitted Unity AI Assistant packages and their scripting defines are editor tooling and were left out, as before) |

The title menu shows a small stamp (`polish/cinematic-pass · <commit> · 22 Nov 2026`) so the build you are playing can
be told apart from older ones (`Builds/acts12/Pinnacle.app` is the previous branch's build and has no compass in Act
III; `Builds/macOS/Pinnacle.app` predates the compass entirely). Editor play shows no stamp.

## 1. How it was checked

`Assets/Scripts/Playtest/PlayRoute.cs` plays routes in a player build with a virtual keyboard, a virtual gamepad and,
new here, a virtual mouse that swings the compass needle the way a hand on a mouse or trackpad would. Footage is
captured inside the player from the final frame (UI included) at a fixed 30 fps with the mix rendered offline in step:
macOS refused this session's screen capture (`screencapture`: "could not create image from display"; Screen Recording
permission is not granted to the app running me, and changing it is your call), so the footage is the player's own
back buffer, which is the same picture the window shows. New routes: `kamala`, `act3` (with `PINNACLE_ROUTE_BRANCH`,
`PINNACLE_ROUTE_PANDIT`), `act3full`, `cafe`, `act1door`, and `act1` extended (see `Docs/PLAYTEST.md`).

Machine: MacBook Air M1 (MacBookAir10,1, 8 GB, fanless), macOS 27.2, windowed 1280x720 and 1920x1080, quality "PC",
look tier Cinematic.

## 2. The decision compass

### 2.1 What was wrong, traced

| Path | Before | Now |
|---|---|---|
| Act I, Mrs. Kapoor (`PriyaRoomStory.KamalaDialogue`) and "move on?" (`MoveOnChoice`) | compass present, but drawn only while `dlgText` was non-empty (`OnGUI`), so a decision whose line had cleared drew nothing; the needle appeared only once something was focused; dial 68 GUI units across; Mrs. Kapoor stopped on the landing behind the door jamb, small, backlit and half hidden from most of the room | drawn whenever a decision is open; needle visible at rest; dial 92 units with labels on dark pills; she steps over the threshold (and a little further over open floor if Arjun is across the room) so her lit face is 1.75 m away and above the dial (route: eyes at 0.48 of the frame height, dial top at 0.40) |
| Act II, Bhaskar, Vikram (`MarketStory.Menu`), the SUV (`HookSequence`, `DrawChoice`) | same draw guard; the SUV's own dial drawn separately | same guard fix (the SUV still draws only through `DrawChoice`, with its 15 s clock); the SUV is framed above the timed dial |
| Act III, the Pandit (`PanditTalk`) and the crossroads (`PhoneHook`) | numbered panel ("Press 1: ...") through `ActStoryBase.Choose` and `DrawDialogue` | the compass, act-scoped: `ActStoryBase.UseCompass` is on in Act III and the café, off in the Kapoor house, the police station and Act V, which keep their panels unchanged; Act III's lines and narration speak through the same phrase subtitles as Acts I-II |
| Act IV café, Raj (`RajTalk`) | numbered panel with "(asked)" | the compass, asked questions ticked |

The story coroutines still own every index and consequence: the compass only reports the committed index, so every
reply plays its original line, trust result, evidence and destination (checked below by the trust value, the saved
Pandit answer and branch, and the Act IV place that loads).

### 2.2 How it behaves

- **Layout** (in the story scripts' GUI space, the screen scaled to 900 units high, so 1280x720 and 1920x1080 lay out
  identically): dial centre at 70.5% of the height, its cluster's top at 60.5%, below a witness's eyeline (eyes sit near
  50%); the focused line under the dial where subtitles sit; the controls hint on the letterbox bar. Kept inside
  `Screen.safeArea`.
- **Needle**: rests on the empty lower-left diagonal (225 degrees, pointing at no answer) with a slight sway; follows the
  input on a damped spring (16 rad/s, damping 0.75) so it swings and settles; a soft tick when the candidate changes; a
  brass ring and the label lifting in gold for 0.65 s when a bearing is committed.
- **Input**: relative mouse or trackpad movement (36 points across the radius), the arrows, either stick, the d-pad and
  the Arduino's right stick point; click, E, Enter, Space, the pad's south button and the Arduino's D4 confirm; 1-4
  answer directly. Only presses count (`wasPressedThisFrame`), and nothing counts in the first 0.22 s after the dial
  opens, so the key that skipped the line before cannot answer it.
- **Waits**: ordinary conversations wait for ever; the SUV keeps its 15 s clock and its original timeout result
  (walk on, result 1).
- **State**: hidden and deaf while paused, back unanswered on resume; J does nothing over a decision; the cursor stays
  captured (the needle reads relative movement).

### 2.3 Checked in the player (measured, after-build, virtual mouse)

| Decision | Route | Result |
|---|---|---|
| Mrs. Kapoor after the third clue, 1280x720 and 1920x1080 | `kamala` | 20/20 checks each: the dial opens 0.10-0.30 s after her question ends, needle at rest at 225 degrees with nothing picked, her eyes in frame above the dial, the cursor captured, J ignored, Escape pauses and a number pressed behind the pause is ignored, resume returns to the open dial; the needle swings to each of the four bearings and picks it (0-7 degrees off) with the full question previewed; a click commits "Threats"; Arjun asks it in his recorded voice; she answers; Space held through the end of her answer does not answer the next question |
| Vikram's first challenge, 1920x1080 | `market`, mouse | "Show the note", "Appeal to him", "Threaten police" with their full lines previewed; the click commits "Show the note", Arjun's recording (`q_v_1a`) plays, trust becomes WARMING; Vikram's face in frame at both decisions; the conversation saved; 20/20 checks |
| The SUV | `market` | the clock gives 15 s; Hide (result 0) taken; the act completes (timeout and the other outcomes: section 2.4) |
| The Pandit, 1280x720 | `act3` | "The truth", "The people", "Both are one"; his eyes in frame above the dial; the click commits "Both are one" (index 2), Arjun's answer and the Pandit's reply play, the answer is saved |
| The crossroads | `act3` x3 | "Meet the caller", "Protect the family", "Confront Reddy", each previewing its destination ("Go to Café Irani - meet the mysterious caller", ...); the click commits; the Curator's visit follows and Act IV opens at the chosen place (section 2.4) |

Footage (`Recordings/cinematic/after/`, git-ignored): `kamala_720/kamala/kamala_compass_720.mp4`,
`kamala_1080/kamala/kamala_compass_1080.mp4`, `vikram_suv_1080/approach/vikram_compass_1080.mp4`,
`vikram_suv_1080/suv/suv_choice_1080.mp4`, `act3_cafe_720/pandit/pandit_compass_720.mp4`,
`act3_cafe_720/crossroads/crossroads_compass_720.mp4` (each from the last spoken line through the dial, the needle
moving, the commit and the reaction), with contact sheets beside them.

## 3. Café Irani (Act IV, the café branch): the counter, the owner, the street

### 3.1 The white slab, diagnosed

`Act4Story.BuildCafe` built the counter from a 2.6 x 1 x 0.8 m box with a pale glossy top, and the "display case" as a
solid 1.5 x 0.4 x 0.6 m box in a pale colour at smoothness 0.95, with twelve biscuit cylinders *inside* it (so hidden).
Two lights made it white: the café window right behind the counter carried a 1.4-intensity point light 0.9 m from the
case, and the window glass itself was self-lit at 1.1, which clipped to white with bloom. Material loading was fine
(the wood and marble sets load); one prop was missing: `metal_jug` has textures but no model in the project, so the
jug never appeared (`PropKit.Place` returned null).

### 3.2 What it is now (`Assets/Scripts/Act4Story.Cafe.cs`)

- **Counter**: a teak carcass with a framed front of four raised panels (stiles and rails 22 mm proud), a dark toe kick
  set back 7 cm, panelled ends, open shelves on the staff side (stacked cups, rusk jars, a cash box, a steel bin), and a
  3 cm honed marble worktop (albedo ~0.65, not near-white). The player still meets the same collision volume.
- **Biscuit case**: a wooden plinth, a dull aluminium frame (posts and rings), clear glass front, ends and top and two
  sliding glass doors on the staff side (URP Lit, transparent, premultiplied so the lamps' highlights stay on the glass;
  environment reflections off, because they turned the top pane into a milky sheet at grazing angles), a steel tray,
  and 64 Osmania biscuits in overlapping rows plus four short stacks (three bakes of slightly different colour, three
  combined meshes, one draw each), and a handwritten "OSMANIA Rs 10" card.
- **Within the owner's reach**: the kettle on a trivet, a steel milk jug (turned on a lathe, since the scanned one is
  missing), a tray of cups and saucers, and the owner's own cup.
- **Light**: the window lamp beside the counter moved to the glass and brought down (0.55, 2.4 m); the window glass
  glows at 0.42 behind half-drawn curtains; a green enamel pendant on a bracket from the café front, a warm 2.3-intensity
  spot with soft shadows (the contact shadows under the case, kettle, jug and cups), soft contact shades under the
  small things, and a low spill on the customer side so the panelled front reads.
- **The owner** stands on the staff side, feet on the floor, facing the room, and works: his left hand wipes the worktop
  with a cloth in slow circles (one hand always occupied); every few seconds his right hand reaches for his cup (two-bone
  IK onto a grip point), lifts it, carries it beside the jug and sets it down; he glances at the entrance, and goes back
  to the work. The cup is carried by the hand's goal, so the hand cannot slide off it, and it is put back upright on
  the worktop. Raj's table, the watcher and the kitchen door keep clear sightlines (the case is 39 cm tall).

Checked (measured, after-build): the owner's wrist stayed within 0.4 cm (Act III run, 140 held frames) and 2.0 cm
(café run, 46 frames) of its grip while carrying the cup; the cup stands on the worktop at y 1.005 m after release.

### 3.3 The street

Across a 1.5 m pavement with a stone kerb (14 cm) and a 6.5 m asphalt road with a worn centre line and a drain: the
opposite row (fronts 10 m from the terrace wall), three houses of different heights and colours: a shuttered provision
store (three rolling shutters, padlocked, under a painted bilingual fascia, "Gupta General Store", lit like the board
it is painted on) with two floors of flats over it; a taller block with a recessed doorway up two steps, a bare bulb
over it and a shuttered shop beside it; a two-storey corner house with a medical store whose side wall turns into a
side street. Windows are openings with frames, sills and concrete sunshades; about four in ten are lit, behind
curtains. Parapets with copings, water tanks and a stair head break the roofline against a faint sodium glow painted
far behind it. Lamps: a sodium lamp on this side (a downward spot with soft shadows, so the parked car and the road get
a pool of light), a cooler lamp and an older one on the far side. Parked: a white Ambassador-style saloon at the kerb
under the sodium lamp (tyres resting on the road), a car under a cover on the far side, a red scooter on its side stand
by the shop, lamps off. A passer-by walks the far pavement every 20-30 s. All of it is outside the terrace's walls and
invisible fences, and the stealth (kitchen, alleys) is elsewhere: collision and navigation are unchanged.

## 4. Act II: the market

| What | Found | Change | Evidence |
|---|---|---|---|
| Fixtures flickering as the camera turns | not reproduced as a light problem: the market has 28 lights (3 casting shadows: the moon and the SUV's headlamps), so neither Forward+'s light limit nor the additional-light shadow atlas drops anything; lamps beyond 30-46 m fade out smoothly | none to the lights | measured (census); the look-back turn: section 4.1 |
| Horns over the witnesses | a vehicle queued behind Arjun while he talked to Vikram could honk over the conversation (horns were allowed in any phase) | drivers wait silently while a conversation, the phone call or the SUV holds Arjun | code |
| The opening narration and a witness | it already ran over free movement and stopped when a conversation began, with a hard cut | a 0.14 s fade when a witness (or a skip) takes over | code |
| Repeated invitations | "Evidence noted" after every one of up to six answers | taught once, confirmed once, then the soft chime only | code |
| Dead time | 3.5 s of nothing after the SUV outcome before the closing line | 2.4 s | code |
| Vikram before he confides | he answered straight on | checks the lane both ways (about 1.4 s) before "Priya is like a daughter to me"; the 0.75 s reaction beat after Arjun's challenge is kept | code |
| The SUV under the dial | the dial's top label and heading sat on the SUV | the camera tilts down a little once it stops, so the SUV sits above the dial | code |
| Vikram's kettle and glasses | a polished metallic cylinder that mirrored the stove and read as a white block beside him; white tumblers | a lathe-turned, dull, soot-dark aluminium kettle with lid and knob; dull steel tumblers | code |

Kept as the previous pass left them (measured then, unchanged here): the radio at its stall, horns on their vehicles, the
phone at the stall, the engine on Reddy's moving bike, music and vendors ducked under speech, walking during the
opening narration, signs fastened to their boards on straps.
