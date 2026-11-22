# Cinematic pass: the compass, Acts I-III, Café Irani (22 Nov 2026)

Branch `polish/cinematic-pass` (from `polish/acts-1-2`, which `main` had merged as PR #2). Every result below is
labelled **measured** (numbers or checks from a scripted run of a player build), **seen** (I looked at footage or
stills from a player build) or **not verified** (needs a person, a device or a check I could not make).

## 0. Which build to play

| | |
|---|---|
| Project folder | `/Users/user/Pinnacle` (Unity 6000.5.9f1, URP 17.5) |
| Startup scene | `Assets/Scenes/SampleScene.unity` (the only scene in the build) |
| Branch / commit | `polish/cinematic-pass`; the player was built from `4e0bef6` (printed on the title menu, bottom left: `polish/cinematic-pass · 4e0bef6 · 22 Nov 2026`); later commits on the branch change only docs and the benchmark scripts in `Tools/` |
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
| The SUV | `market` | the clock gives 15 s; Hide (result 0); with no answer the window runs out to the original walk-on result (1); Read the plate saves the plate; Escape inside the choice freezes its clock (13.49 s -> 13.44 s left over 3 s) and resume returns to it (section 9) |
| The Pandit, 1280x720 | `act3` | "The truth", "The people", "Both are one"; his eyes in frame above the dial; the click commits "Both are one" (index 2), Arjun's answer and the Pandit's reply play, the answer is saved |
| The crossroads | `act3` x3 | "Meet the caller", "Protect the family", "Confront Reddy", each previewing its destination ("Go to Café Irani - meet the mysterious caller", ...); the click commits; the Curator's visit follows and Act IV opens at the chosen place: "Café Irani | 11:05 PM", "The Kapoor house | 11:10 PM", "Chandnipur Police Station | 11:15 PM" |

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
- **The signboard** is a two-line board ("CAFÉ IRANI" over "SINCE 1932") hung over the counter window, clear of the
  pergola. Making its lettering depth-tested (it was drawn with Unity's default font material, which shows through
  everything) exposed that the old 5.6 m board ran under the pergola's roof: from the entrance the slats and a fan
  blade hid "CAFÉ IRANI - SIN", leaving "CE 1932". The old shader had painted the letters over the slats.
- **The owner** stands on the staff side, feet on the floor, facing the room, and works: his left hand wipes the worktop
  with a cloth in slow circles (one hand always occupied); every few seconds his right hand reaches for his cup (two-bone
  IK onto a grip point), lifts it, carries it beside the jug and sets it down; he glances at the entrance, and goes back
  to the work. The cup is carried by the hand's goal, so the hand cannot slide off it, and it is put back upright on
  the worktop. Raj's table, the watcher and the kitchen door keep clear sightlines (the case is 39 cm tall).

Checked (measured, after-build): the owner's wrist stayed within 0.4 cm (Act III run, 140 held frames), 2.0 cm
(café run, 46 frames) and 0.7 cm (the stills run, 145 frames) of its grip while carrying the cup; the cup stands on
the worktop at y 1.005 m after release.

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
under the sodium lamp (tyres resting on the road; its side windows sit in the cabin's upright band, framed in chrome
and split by the door pillar, after the first version's pane stood 12 cm off the body like a black slab), a car under a cover on the far side, a red scooter on its side stand
by the shop, lamps off. A passer-by walks the far pavement every 20-30 s. All of it is outside the terrace's walls and
invisible fences, and the stealth (kitchen, alleys) is elsewhere: collision and navigation are unchanged.

### 3.4 Before and after, the same views (seen)

The `cafe` route takes the same seven stills in both builds at 1920x1080, each once the view has been still for 12
frames: `Recordings/cinematic/stills/before_cafe/` and `Recordings/cinematic/stills/after_cafe/` (git-ignored), named
`cafe_01_entrance` (the approach from the terrace entrance), `cafe_02_counter` (at the counter), `cafe_03_owner`,
`cafe_04_street_from_counter`, `cafe_05_street_from_terrace`, `cafe_06_case_side` (the case from the side) and
`cafe_07_outward_behind_owner` (outward from behind the owner). Before: a white box on a pale slab with the owner's head
just showing over it, the window behind blown to white, and a black void past the terrace wall. After: the panelled
counter, the glass case with the biscuits readable through it, the kettle, jug and cups in the owner's reach, and the
lit street behind the terrace.

Footage from the delivered build: `Recordings/cinematic/final/cafe_1080/cafe/final_cafe_1080.mp4` (1920x1080, 30
fps, 31 s, with the mix): from the terrace entrance (the sign readable over the counter window), the walk up to the
counter with the owner at work behind the case, then along the terrace to the wall over the street (the parked car,
the shuttered shops, the lamps). The side view of the case and the outward view from behind the owner are stills,
beside it in `final/cafe_1080/`. The route's checks in that run: 3/3 (his wrist within 1.1 cm of the grip for the
77 frames he held the cup; the cup back on the worktop at y 1.005).

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

### 4.1 The look-back turn (measured, seen)

`Recordings/cinematic/final/market_lookback/lookback/market_lookback_1080.mp4`: the 180-degree turn back through the
crowd, a hold, a 120-degree turn, 150 frames at 1920x1080. The frame's mean brightness changes smoothly (0.13 luma steps
per frame at the median, i.e. in the hold; 3.8 at p95 while the turn sweeps new things into view; 7.5 at most; no
isolated jump), and the stall bulbs, the stove's glow, the bunting lights and the lit boards hold steady across
consecutive frames. I could not reproduce fixture flicker on this route; if you see it, the fixture and where you stand
would let me capture exactly that view.

## 5. Act I: the window, the landing, the nameplates, the door

| What | Reproduced | Cause | Fix | Evidence |
|---|---|---|---|---|
| Priya's window view | an emissive gradient quad 32 m out and sixteen box "rooftops" whose tops sat at or below eye level 20 m away; nothing near enough to move against the sky | no near layer, no real distance; the room's fog (tuned for 5 m) also browned anything far | three depths: the neighbours' row across an 8 m lane (three houses of 3-4 storeys, roofs just under the eye, parapets with copings, water tanks, a stair head with a bulb over its door, one curtained window lit, washing on a line), old-quarter roofs 30-60 m out (dark against the sky, the sun behind them), and a painted, fog-free backdrop: the far city and the Renewal Project's towers in haze, a temple shikhara, the sunset sky with its brightest band at eye height and its glow where the room's sunlight comes from (12 degrees up, 10 degrees right of the window's axis); towers cut to 8-15 degrees above the horizon so they read as distant | seen: sidestepping along the sill, the neighbours' roofline slides across the towers and roofs; the latch stays on its mullion, reachable, with its examine prompt |
| The stairwell window | a self-lit orange board on a solid wall | no opening | the end wall is cut round a real opening with a frame in its reveal, dusty glass (transparent, highlights kept), bars, and outside a lower neighbour 3.5 m away with one lit window and a drain pipe, the dusk sky above its parapet | seen from the far end of the landing and close up (`act1_stair_a`, `act1_stair_b`): a framed opening with bars, a sill and dusty glass where the before build has a blown-out board; through the dust the neighbour's lit window blooms into a warm spot |
| Nameplates (all four flats) | the plates floated 2.5 cm off the wall; their lettering used Unity's default font material, which draws through walls while the on-screen UI is up (so "KAPOOR" could show inside Priya's room) | `DoorDecor` placed the plate at the door frame's depth and the text as separate objects with the default material | each plate is screwed flat to the plaster (back face on the wall), clear of the frame and the door's swing, with its lettering 1 mm proud in the depth-tested `Pinnacle/WorldText` material, both under one mount so they cannot drift apart | measured: all eight labels use the depth-tested material (route check); seen from the landing |
| The door's sound | one 1.2 s creak clip played from beside the hinge whatever the door did, no latch, no contact | | the latch releases at the handle (a brass click on the leaf, moving with it), the hinge sounds only while the leaf turns (level and pitch follow its angular speed, a short fade when it stops), and reaching the frame the leaf knocks and the latch catches at the strike side; all three are 3D sources at those parts; the approaching steps keep their own moving source | measured: see below |
| Dust in the light | hard white squares floating in the window light | the particle material had no texture | a soft round mote | code |

The door, measured (`act1door` route, the door's own sound log plus the recorded mix): the steps come up, the door
swings shut by itself: hinge 1.63 s, then the frame knock and the latch catch at the strike side (-1.50, 1.00, -2.50);
paused during the steps, the door does not move and resumes; opened by hand: the latch at the handle (-1.63, 1.00,
-2.45), the hinge 0.17 s later for 1.30 s, and no contact at the open stop (there is nothing there to hit); a second E
straight after opening triggers nothing (two latch sounds, two hinge runs, one frame contact in all). In the mix the
knock and the latch fall 20 dB within 90-100 ms (dry: no reverb tail) and sit 5-9 dB under the dialogue; the hinge
lifts the room bed by about 3 dB only while the door turns (raised a little since). No reverb zone reaches Act I (the
Gallery's is 400 m away and leaves with the interlude). I measured these; I have not listened to them.

Before and after, the same views (seen; `act1` route at 1920x1080, each still taken once the view has been still for
12 frames): `Recordings/cinematic/stills/before_act1/` and `Recordings/cinematic/stills/after_act1/`. `act1_sill_left`
and `act1_sill_right` are the window from either end of the sill. Before: two glass towers filling half the window
close up and a flat pale plane below the skyline. After: the neighbours' row across the lane, roofs dark against the
sunset, the towers small in the haze. `act1_latch_*` show the latch on its mullion with its examine prompt.
`act1_stair_a` and `act1_stair_b` are the stairwell window close up, and `act1_landing_far` the same window from the far
end of the landing. `act1_labels_*` are the nameplates from the room and both sides of the landing.

## 6. Act III: the library, the record room, the temple (`Assets/Scripts/Act3Story.Props.cs`)

Built from the big shapes and contact points out, nearest the camera first. The collision volumes and every interaction
target stay where they were (the computer's spot now aims at the screen's centre; the drawer's at the desk's original
volume), so the puzzles play the same.

| Place | Before | Now |
|---|---|---|
| Front desk and computer | a 2.6 m box, a beige cube "monitor" with a glowing slab, a flat keyboard | a teak top on a framed modesty panel and plinth, panelled ends, a drawer pedestal on the staff side (drawers set into their frame with a shadow gap, brass pulls); a CRT with a tapered case, bezel, dark glass with lines of green text and a power LED, on a swivel foot; a keyboard with key rows, a mouse on a pad, the tower under the desk, a register and a counter bell; the "ENQUIRY" sign standing on the desk, depth-tested |
| The librarian's drawer | a slab with a box "lock" | the drawer front set 6 mm back in its opening on two runners, a brass escutcheon round a four-dial combination lock, a pedestal of drawers; the yellowed note taped on the apron beside the lock as paper, its lines depth-tested, lit by the lamp's kicker |
| Bookshelves | solid 2.4 x 2.8 m slabs with 56 cm "books" | double-sided bookcases with sides, a spine, five shelves, a plinth set back as a kick and a cornice, filled with books of real sizes (spines 2-6 cm, 20-32 cm tall, 15-22 cm deep), a few leaning, gaps where books are out; six cloth colours, one combined mesh per colour per case |
| Record room shelving | uprights and boards | foot plates under the uprights and X-bracing across each end |
| The cage at Block C, Shelf 14 | bars and rails | a welded wire mesh on a 10 cm grid, hinges, a hasp for the padlock |
| The calendar | coloured boxes and floating text | a printed municipal calendar sheet (twelve small months, 26 January, 15 August and 2 October ringed in red) hanging from its nail on a string, the lower edge curled, the printed and handwritten lines kept and depth-tested |
| Priya's envelope | only a toast and a card | a sealed envelope (red wax seal, thread) on a brass plate on the alcove's mat beside the Pandit; it goes when he hands it over |

Materials: aged wood (the scanned wood set, tinted per piece), dull metal (metallic 0.65-0.85, smoothness 0.45-0.55),
paper (smoothness 0.1-0.15), the temple's worn sandstone unchanged. Checked (measured): the Pandit's talk, the letter,
the gate, the call and the crossroads from a temple save, all three branches; the fresh run (guard, computer, drawer,
cage, priest, crossroads) is in section 9.

## 7. Faces, lip-sync and movement

### 7.1 What the faces did

Traced per character: every NPC speaks through its own `CharacterNPC.voice` AudioSource. The Curator alone had visemes
(`HeroFace`, from `Tools/lipsync`). Everyone else opened the jaw with the voice's loudness (up to 11 degrees) and nudged
the upper lip, and the head bobbed at a fixed 6.5 Hz scaled by the same loudness: head gestures were tied to the mouth.
The rigs are Rocketbox avatars with facial bones (`Bip01 MJaw`, upper and lower lips, mouth corners, eyelids, brows)
and no blendshapes.

### 7.2 What they do now

- **Visemes for the faces in scope**: `Tools/lipsync/npc_lipsync.py` makes tracks for Kamala, Bhaskar, Vikram,
  Sharma-ji, the Pandit and Raj (47 lines, `Assets/Resources/VO/Visemes/`). It is the Curator's pipeline with the word
  timing taken from on-device recognition of the shipped audio (`Tools/transcribe_words.swift`), because those TTS voices
  are not AVSpeech voices and many files were rendered by an older `say`, so the Curator's TTS word markers cannot be
  carried over; words outside the lexicon are spelled by rule (digraphs, a silent final e, soft c and g). Recognition
  mishears some names (the report lists every spelled word): those words get approximate shapes.
- **Driven by the clip's own time**: `CharacterNPC.SpeechMouth` takes the weights at `voice.time` (so pausing,
  skipping and cutting a line keep the mouth in step), poses the jaw, lips and corners with the Curator's shape table
  scaled to each face (eye spacing), closes the lips on P/B/M, opens on the open vowels, and settles to rest within about
  0.1 s of silence. Eyes, blinks and brows keep their own layer; nods now come from the line's stressed words, not from
  the loudness.
- **Fallbacks**: a line without a track opens the jaw with the voice's loudness (as before, a little smaller); a rig
  without jaw and lip bones keeps a still mouth while the voice and subtitles play (`HasMouthRig`, `MouthMode`).

### 7.3 Measured (after-build routes)

| Test | Result |
|---|---|
| A quiet short line in close-up (Kamala, "Have you found anything?") | visemes on 37 of 38 frames, jaw 0-8.3 degrees; at rest (0.00 degrees) when it ends |
| The listener stays silent (Kamala while Arjun asks his question) | jaw at most 0.00 degrees, mode "rest" throughout |
| A long, fast line (Vikram, 19 s, 65 words) | visemes on all 926 frames, jaw 0.1-9.4 degrees, 12 lip closures |
| A line cut off mid-word (Kamala's answer skipped with Space) | 3 lip closures in its first 4.5 s; the mouth at rest 0.10 s after the cut |

Not done: an explicit run of the no-rig fallback (no NPC in Acts I-III uses a rig without facial bones, so it is a code
path, not a tested one); a review of real-time audio and video together by eye (the footage is captured with the mix in
step, but I have not judged the sync by eye frame by frame); tongue and teeth.

### 7.4 Movement and performance

Checked in code and kept: stride follows the measured ground speed (no sliding on walks), walk starts and stops blend,
head turns are eased (no snapping), idle and walk clips start at random phases (no crowd breathing in unison). Staged:
Kamala watches Arjun for her answer (she holds his eyes through the dial), Vikram checks the lane both ways before he
confides, the café owner keeps one hand occupied with a cloth and works with the other. Not done: foot planting during
180-degree turns on the spot and on steps (no turn-in-place clips for these avatars), hand-to-body intersection checks,
Reddy's mount onto the bike reviewed frame by frame.

## 8. Frame time (measured)

**Method.** Two release players built the same way (batch mode, `BuildPlayer.MacOS`, the committed packages): *before*
is the branch's base (`04150c9`, the commit before this pass) and *after* is the delivered player (`4e0bef6`). Both
carry the same `PlayRoute`, so they play identical routes. Machine: MacBook Air M1 (8 GB, fanless), macOS 27.2, Low
Power Mode off (power source in the results). Window: 1920x1080, quality "PC", look tier Cinematic, vSync off, no frame cap. No capture or stills during
timing. Runs alternate before/after in matched pairs, each after at least 60 s idle and once the 1-minute load average is
under 3.5, because this machine throttles over consecutive runs (`Docs/ACTS_1_2.md`). Frame time is the wall-clock
interval between frames, which is what the player sees. The GPU counter is not used: on this machine it reads 35-38 ms at the median for frames that
arrive every 14-17 ms, so it cannot be per-frame GPU work. Cold entry, from the chapter-select request until control, is reported apart
from play:

- **Market**: *steady* is Bhaskar, the gate, the walk to Vikram, both decisions, the look back, Reddy's escape and the
  card. *SUV* is the chase, the timed choice and its outcome (Hide).
- **Café**: *traversal* is the approach to the counter and the walk along the terrace wall. The fixed-view stills and
  the wait for the owner's task are left out.

Tools: `Tools/timing_pairs.sh <before.app> <after.app> Recordings/cinematic/timing 3` runs the pairs (each run through
`Tools/route_run.sh`), then `Tools/route_timing.py` prints the statistics per run and pooled per build.

**Results.** Two sets, because the machine's state changed between them. Wall-clock frame time in ms at 1920x1080.

*Set A: three matched pairs, 20:42-21:17.* The Mac was charging on mains for the first seven runs and on battery for
the last five (the charger came out at about 21:02). A browser and this app were open. Every frame of a build's runs is
pooled, with the per-run p95 in brackets:

| Scene | Part | Build | Frames | Median | p95 (per run) | p99 | Worst | > 50 ms |
|---|---|---|---|---|---|---|---|---|
| Market | steady play | before | 15993 | 17.6 | 23.5 (19.5, 18.0, 26.7) | 27.6 | 125.4 | 20 |
| Market | steady play | after | 15114 | 19.6 | 22.0 (20.7, 22.5, 21.8) | 24.4 | 77.8 | 6 |
| Market | SUV chase | before | 8370 | 21.5 | 32.8 (25.2, 23.1, 35.0) | 36.8 | 188.4 | 24 |
| Market | SUV chase | after | 6669 | 26.4 | 37.4 (25.6, 43.9, 35.9) | 46.0 | 68.2 | 18 |
| Café | traversal | before | 8230 | 10.0 | 15.2 (15.3, 14.7, 15.4) | 16.1 | 38.3 | 0 |
| Café | traversal | after | 5329 | 16.2 | 21.0 (17.6, 21.8, 20.9) | 22.4 | 205.9 | 2 |

*Set B: two matched pairs, 18:54-19:17, on battery (charge 17% falling to 7%), nothing else running.* Pair 1's frame
files were overwritten by a bug in the timing script (its idle-wait reused the loop counter, so pair 2 was saved under
pair 1's names). Its statistics below are the ones printed when it finished. Pair 2's files are in `timing/battery_pair2/`.
Values are pair 1 / pair 2:

| Scene | Part | Build | Median | p95 | p99 | Worst | > 50 ms |
|---|---|---|---|---|---|---|---|
| Market | steady play | before | 16.9 / 16.9 | 17.8 / 17.7 | 18.4 / 19.1 | 52.0 / 48.9 | 1 / 0 |
| Market | steady play | after | 16.9 / 16.9 | 17.8 / 17.6 | 18.1 / 18.1 | 50.3 / 50.0 | 1 / 1 |
| Market | SUV chase | before | 18.9 / 19.0 | 22.5 / 22.3 | 22.9 / 22.6 | 120.5 / 117.4 | 2 / 1 |
| Market | SUV chase | after | 19.0 / 19.0 | 22.8 / 23.0 | 23.1 / 23.2 | 34.8 / 33.6 | 0 / 0 |
| Café | traversal | before | 9.3 / 9.4 | 13.1 / 13.0 | 13.4 / 13.4 | 24.5 / 23.9 | 0 / 0 |
| Café | traversal | after | 12.2 / 12.2 | 15.0 / 15.0 | 15.6 / 15.5 | 23.8 / 26.5 | 0 / 0 |

**Cold entry**, from the chapter-select request until control. Set A, run by run: market before 5.97 / 4.94 / 5.14 s,
after 1.98 / 1.99 / 2.01 s; café before 17.52 / 17.41 / 17.50 s, after 13.34 / 13.35 / 13.40 s. Set B: market before
4.91 / 4.48 s, after 1.79 / 1.76 s; café before 16.64 / 16.80 s, after 13.14 / 13.17 s. The before build also has one
long frame while loading: 1.1-1.2 s in the market and 4.8 s in the café, against 0.2 s and 0.7 s after. I have not
traced which load got faster. The café hands over control after its title and narration, about 13 s by design.

What it says:

- **On a quiet machine (set B), this pass leaves the market as it was.** Median and p95 match within 0.3 ms in steady
  play and in the SUV chase.
- **The café costs about 3 ms more per frame** on a quiet machine: median 9.3 to 12.2 ms, p95 13.1 to 15.0 ms. That
  pays for the counter, the glass case with its 64 biscuits, the street frontage, and three street lamps with shadows.
  In set A it costs 4-8 ms more at the median, 2-7 ms at p95. GPU-heavy additions slow down more as this fanless
  machine warms, which is my guess at why; I did not measure temperatures.
- **On a warmer, busier machine (set A), both builds are slower and noisier.** In the market there is no consistent
  winner. The after build's steady median is 2-3 ms higher in pairs 1 and 2. The before build is higher in pair 3.
- **Hitches over 50 ms**: in steady play, 0-13 per run before and 1-2 per run after. In the café traversal there are
  none, except two in one after run in set A (worst 206 ms).
- **Against the target (p95 at or under 33.3 ms at 1080p)**: met in steady play and in the café in every run (the worst
  runs: 26.7 ms and 21.8 ms). The SUV chase met it in every set B run (22-23 ms) but missed in three of the six set A
  runs (35.0 ms before; 43.9 and 35.9 ms after). The previous pass measured the same market route on this machine at
  44-84 ms p95 (`Docs/ACTS_1_2.md`; hot, with background load). The machine's state moves these numbers more than
  either build does. On these runs the target holds on a cool machine, but not in the chase once the machine is warm.

## 9. Playthroughs (scripted, player builds, every check printed by the route)

| Run | Covers | Result |
|---|---|---|
| `kamala`, 1280x720 and 1920x1080 | Act I from a save with two clues: a third found by walking to it, Mrs. Kapoor's talk on the compass, lip-sync, held key, pause, journal, cursor | 20/20 each |
| `market`, mouse, "Show the note", Hide, 1920x1080 | Bhaskar's gate to Vikram, both decisions in frame, trust WARMING, Vikram's fast line lip-sync, the watcher bolts, her ride, the card, the phone, the SUV's 15 s, Hide, the act's end | 20/20 |
| `market`, "Show the note", no answer | the look-back turn captured (section 4.1); the SUV's window runs out: walk on (result 1), the act completes | 14/14 |
| `act3` x3 (Pandit 2/1/0, branch 0/1/2) | from a temple save: the Pandit on the compass, the letter, the gate, the call, the crossroads, the Curator's visit skipped after 5 s, Act IV at Café Irani / the Kapoor house / the police station | all destination checks pass; two route checks failed on a one-frame race (they read the save the frame before the story writes it) and were fixed in the route |
| `act3full`, 1280x720 (fresh) | from chapter select: the night guard's scene plays out (the route waits for the computer objective), the computer and its password, the Record Room, the locked cage at Block C / Shelf 14, the calendar, back upstairs, the librarian's drawer (7491), the key, the cage opened, the Pandit on the compass (each bearing swung to and picked, "Both are one" committed and saved), the letter, the gate, the call, the crossroads on the compass (each destination previewed, "Meet the caller" committed, branch 0 saved), the Curator's visit, Act IV at Café Irani, the owner's task | 41/42. The one failure was the route's own timing: its fixed 14 s window ended while the owner was carrying the cup, so it read the cup in his hand, 6 cm up (y 1.065). The check now waits for him to set it down (rerun in the final café run below). The route walks in straight lines with no pathfinding: twice furniture stopped the walk (18.2 m and 4.3 m short) and the route placed Arjun beside the spot, so walking between those rooms is not part of this check |
| `cafe`, 1920x1080 | chapter-select entry, the walk to the counter, the owner's task (grip, cup on the worktop) | 3/3 in each run, the last on the delivered build (`final/cafe_1080`) |
| `act1`, 1920x1080 | the window sidestep, the latch, the stairwell window, the landing turn, the nameplates (all eight depth-tested) | 8/8 on the delivered build; the before build fails all eight nameplate checks (its lettering uses the GUI text material) |
| `act1door` | steps, pause/resume, the swing shut, opening by hand (a second E ignored), the note, a skipped line; the door's sound log | all pass |

## 10. Still open, with how to reproduce

| Issue | Reproduce | Why it is open |
|---|---|---|
| The footage is not an OS screen recording | run any route with `PINNACLE_ROUTE_CAPTURE` (section 1) | macOS refuses screen capture to the app I run in (Screen Recording permission); the clips are the player's own final frame with the UI, which is the window's picture, captured at a fixed 30 fps with the mix rendered offline. Grant the permission (System Settings > Privacy & Security > Screen Recording) if you want OS-level window recordings |
| Sound judged by measurement only | play Act I's ending (all nine clues, wait for the steps, open the door) and the market on headphones and laptop speakers | I can measure onsets, levels, tails and balance; I cannot listen. The door, horn, phone and engines are still synthesised (no licensed recordings in the project) |
| The compass by hand | any conversation, with a real mouse, a trackpad, a gamepad and the Arduino | the routes drive virtual devices; the mouse gain (36 points across the dial's radius) may want tuning to your hand |
| The other Act IV routes and Act V keep the numbered panel | choose "Protect the family" or "Confront Reddy" at the crossroads; reach Act V | by design (act-scoped): you asked for those to be preserved |
| Foot planting in turns on the spot and on steps | watch Kamala turn at the threshold, or any NPC turning in place | the avatars have no turn-in-place clips; the body yaws over the idle |
| Lip-sync for a rig without facial bones | none in Acts I-III | the fallback (a still mouth, voice and subtitles play) is a code path, not a tested one |
| Words the recogniser mishears get approximate mouth shapes | read `Tools/lipsync/npc_report.txt` ("SPELLED" words) | on-device recognition of names (Mehra, Deshpande, Basti Nagar) is imperfect |
| Act IV at the café opens on its title and narration (about 13 s before control) | chapter select, Act IV with the café branch | the act's opening was left as authored |
| Through the stairwell window's dusty glass, the neighbour's lit window blooms into a bright spot that could be read as a sun (the sun is on the other side of the building) | walk to the end of the landing and look at the window (`stills/after_act1/act1_stair_a.png`) | a judgement call: it reads as warm light outside at dusk to me, but lowering `StairViewWindow`'s glow from 0.8 would make it read as a window |
| The SUV chase misses the 33.3 ms p95 target once the M1 is warm (35-44 ms in three of six later runs; 22-23 ms when cool) | run the market several times in a row at 1920x1080 (`Docs/PLAYTEST.md`, route `market`), or play Act II through to the chase straight after another act | the frame-time work in the market was the previous pass's; the next levers it named (2x MSAA, half-resolution SSAO in the market) are still open |
| The Ambassador-style saloon is a simple lofted body | look at the parked car from the terrace (`cafe_05_street_from_terrace`) | built in code; its windows now sit on the body with a pillar and chrome frames, but a scanned period car would read better |
