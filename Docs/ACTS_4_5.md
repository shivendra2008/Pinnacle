# Acts IV and V, and three Act III fixes (23 Nov 2026)

Branch `polish/act4-5` (from `polish/cinematic-pass` at `2be3437`). Results are labelled **measured** (a scripted run
of a player build printed it), **seen** (I looked at stills or footage from a player build) or **not verified**.

## 0. Which build to play

| | |
|---|---|
| Project | `/Users/user/Pinnacle` (Unity 6000.5.9f1, URP 17.5), scene `Assets/Scenes/SampleScene.unity` |
| Player | `Builds/voice-overs/Pinnacle Voices 2.app` (24 Nov 2026: this work plus every recorded voice so far, Reddy, Priya, the judge and the lawyer included) |
| Earlier players | `Builds/act4-5/` (the build this record was first written from, and the game at `2be3437` for side-by-side runs) was deleted on 24 Nov 2026 to free disk; build either again from its commit |
| Built with | `BuildPlayer.MacOS` in batch mode on an APFS clone of the project, with the packages now committed (Unity AI Assistant and AI Inference included) |

## 1. What changed, by place

### 1.1 The decision compass and the evidence folio

`StoryCompass` has four bearings. Reddy's interview can put five pieces on the table (her ID, the sign-out sheet,
Priya's files, the cuttings, the plate or Bhaskar's account). Those go through a new `EvidenceFolio`
(`Assets/Scripts/EvidenceFolio.cs`): the pieces are laid on the chai-stall counter one at a time, and the row of their
names under them takes any number.

- **Inspect** (left / right on the arrows, A / D, the d-pad, either stick, the Arduino's right stick, sideways mouse or
  trackpad travel, the scroll wheel, or a number key) picks a piece up: it lifts off the counter and tips towards the
  camera, and what it shows is written under the row.
- **Present** is a separate press: E, Enter, Space, a click, the pad's south button, the Arduino's D4 or right button.
  A press within 0.2 s of the focus moving, or within 0.25 s of the folio opening, does nothing, so one input can never
  both inspect and present, and the key that skipped Reddy's line cannot answer her.
- **Back** (Backspace, right click, the pad's east button, the Arduino's left button) puts the piece down.
- **Locked** pieces (already accepted) can be picked up and read, not presented. Paused, nothing is read or drawn;
  resuming returns to the same piece in hand.
- The compass keeps conversational intentions: it is now on in all three Act IV branches (Rohan's question, Reddy's
  last question) and in Act V (the Gallery's confirmation). The story still owns every index: the folio and the compass
  only report what was committed.

### 1.2 Café Irani (Act IV A)

| Brief | Done |
|---|---|
| Counter, case, owner, street | Kept from the cinematic pass (glass case on a frame, marble top, panelled teak, the owner's cup task, the street corner with a lit upper window, a shuttered shop, kerb and a parked car) |
| Sitting with Raj | Raj waits **seated** at the corner table (a new seated pose: pelvis on the seat, feet on the floor by two-bone IK, head, hands and visemes as standing). Arjun pulls out the opposite chair (a scrape) and sits: his eye drops 47 cm |
| The phone | A fixed beat after Raj's second line, whatever is asked next: Arjun asks why he should trust him (`a4a_q_a`), and Raj takes the phone out of his jacket, lays it down and slides it across while he answers (`a4a_raj_a`, the actor's recording: "You shouldn't. Not yet. But I'm going to give you something that should earn a little trust."); Arjun takes it. "Trust him?" then shows as asked on the dial. Recordings and the case-file note are saved there |
| Follow-ups optional | The compass offers "Priya", "His price" and "Time to go" ("Trust him?" shows as asked: it came before the phone); any, all or none. Before each answer Raj reacts: he glances at the phone, at the entrance, at his hands |
| Location lead whatever is asked | If "Priya" was not asked, Raj gives the shelter at the dhaba (`a4a_raj_where`) and the same note is saved |
| The turn to danger | Raj's eyes go past Arjun; across the terrace the watcher folds his paper and pushes his chair back on the tiles; Arjun's head is free for 2.6 s (seated, look only). If the player looks at the watcher, the camera is not turned for them; if not, it turns |
| Kitchen | A pot touched rattles where it is and rocks to rest (a 3D sound you can place); footsteps come to the door and a searcher stands in it, sweeping his torch; a rattle while he is there turns him to the sound, he steps towards it and listens. Only being seen in his torchlight with nothing between (0.55 s of exposure, faster while he listens) is a detection: a short fade back to the last cover reached (never in front of him at the door), Raj beside Arjun, the searcher back at his post. Cover: a steel rack, sacks on a pallet, a cold cabinet |
| Alley hide | E at the dumpster takes Arjun to its far side and down; Raj steps into cover once and crouches there (set once: he was being placed every frame). Each retry after being caught starts clean: standing, unlocked, Raj on his feet, no searcher or torch left. (A real fault: after being caught the player stayed locked, so the retry could not be played) |
| Dhaba | Drivers on the charpais drink their chai out of step with each other, a cook at the tandoor, a small radio and men talking low (synthesised), Raj on the free charpai, Arjun on the plastic chair facing him |

### 1.3 The Kapoor house (Act IV B)

- Arjun arrives on **the landing outside flat 301**, as in Act I: the same cream nameplate (301 in red over KAPOOR,
  two brass screws), the doormat, the shoe rack. What changed: the door stands open with the latch keeper levered off
  the frame, the mat is kicked crooked with mud on it, the rack is knocked over and the family's shoes are scattered.
- Inside, the search has a direction: the steel almirah levered open (a door wrenched back, the lock bent, pry marks),
  its saris and document box dropped in front; papers in heaps where each place was searched (almirah, cabinet,
  shelf, sofa), none across the open floor; the law books under the shelf they were swept from; muddy boot prints from
  the almirah back out over the mat, fading.
- Kamala holds a folded sari she has picked up; asked "Did they find anything?", she looks round her own room before
  she answers.
- The laptop no longer starts the confession. Read it (the email card), then **close it**: the lid comes down with a
  click, Rohan stops mid-stride and looks over, then at the floor; Kamala turns to look at her son. Nobody speaks; the
  player turns to Rohan when ready ("Ask Rohan about the emails"). The clues stay optional; emails and ally kept.
- Removed: the big "Rohan joins you" banner after Kamala's line (the scene says it).

### 1.4 Inspector Reddy (Act IV C)

- She leans at the end of the stall's counter, one hand on it, watching the station; she turns her head to Arjun as he
  comes close. Before answering his first line she looks back at the station.
- The evidence is laid on the counter one piece at a time (the pieces this player actually has: her ID only if it was
  picked up, the cuttings only if they were found in Act I, the plate, or Bhaskar's account only if the player heard
  it). Each piece is a thing: her ID card, the Record Room's sign-out sheet with her signature, printed copies of Priya's
  files, the cuttings with Priya's red string, a notebook page.
- Presented, a piece is turned round and slid across to her. The sign-out sheet: she looks at it for a long moment and
  says "That's my signature." Her ID: she turns it face down under her hand without a word. A weak piece gets a short
  answer saying what it fails to establish (eight new lines, one per piece per round) and comes back; her question is
  not read out again, and there is no quiz buzzer or chime.
- Both rounds stay solvable with the least evidence a player can have: the sign-out sheet (Act III, mandatory) answers
  round 1 and Priya's files (Act III, mandatory) round 2.
- Her reply to "I have evidence you don't" answers that line: "All right, Mr. Mehra. Cards on the table." (her recorded
  take opened with "She's braver than all of us", written for an older line of Arjun's; that sentence is cut). Since 24
  Nov 2026 half of her lines are the actor's recordings (`Docs/VOICE_CHECK.md`): her reply after the second winning piece
  starts at "I can see you're not going to stop" (the take asked "Where did you get that USB?", and neither winning piece
  is a USB stick), and her reveal ends on Deshpande sending everyone she has before 10 a.m. instead of "I need Priya to
  testify in person". "That's my signature", "That proves nothing", the eight weak-piece answers and her court line are still
  text-to-speech.

### 1.5 Act V: the shelter and Priya

- The eight bright mats in a grid are gone. Three families along the walls, the floor between kept as a path: a
  mother and two children under faded durries, an old man with his stick, a couple by a steel trunk; sleepers are
  blanket shapes with a rolled blanket under the head; chappals by the mats; a family's papers pushed into a cloth bag.
- Awake at four, each with one thing to do, out of step: a mother watching her children, her hand on the nearer one's
  blanket; a grandmother telling her beads; a man fanning himself with a folded paper. The sleepers breathe, each at
  their own rate.
- A lamp at the head of the stairs throws a pool down them, and Priya's voice comes down too: she is reading her file
  aloud to herself (`a5_priya_upstairs`, low, through the stairwell; still text-to-speech: her other Act V and ending
  lines are the actor's recordings since 24 Nov 2026, and "The hearing is in four hours" is cut from her take, since it
  is four in the morning and the hearing is at ten).
- Upstairs she is at a trestle table with the 1965 file open. When Arjun comes within two steps she looks up, shuts the
  file and gathers it to her chest, then turns to him; she holds it through her lines.

### 1.6 The Gallery

- Three caskets made as what they are: **bronze** worn bright at its edges and ring pull, green-brown patina in the
  recessed panels, ball feet, heavy hinge knuckles; **bone** carved plaques (each a slightly different age, a carved
  lotus on each) on a dark wood carcass, brass corner mounts and small hinges; **iron** riveted straps, rust where water
  stood, a heavy hasp.
- Each opens with its own weight and sound: bronze slow to start, a low resonant creak and a ring; bone light and quick,
  a dry clack; iron grinding up, catching twice before it gives, and a clank.
- A brass plate in front of each carries its inscription in depth-tested lettering, readable from the table.
- Examining a casket: the Curator's remark the first time only; the card says **what choosing it leads to**, and if it
  is sealed, **why in the case's terms** with this player's numbers ("You have 17 pieces of evidence; a case this size
  needs 18. 1 more would have opened it." / "The press needs something it can publish: Raj's recordings or the
  municipal emails. You have neither."). The rules are unchanged (`Sealed()`: 18, always, recordings or emails).
- One earned callback from the route: the thing Act IV left the player with lies under a glass dome in front of the
  casket it bears on (Reddy's sign-out sheet by the bronze; Raj's phone or Rohan's printed email by the iron), and the
  bronze card names the ally who will stand up.
- The confirmation is its own decision on the compass ("Open it" / "Step back"): inspecting never chooses.
- The way in: after Priya's question the shelter's lights sink to the oil lamp's flame while the sound slows and drops
  away, the view settles on the flame, it goes out, and in the Gallery the first candle to light is the one in the
  middle of the view, where the flame was.

### 1.7 The endings

- **Justice**: over the white, after Arjun's answer, Priya takes him up on it: "All right. Let's finish this." (24 Nov
  2026, `a5_priya_finish`: a take she recorded that had no line in the game). Arjun sits at Priya's table (close enough to read her and Deshpande across the aisle). After Priya, the
  witness from the player's Act IV carries the object up and lays it on the clerk's table under the bench: Reddy the
  original 1942 deed in an evidence sleeve (her line now says so), Raj his phone, Rohan the printed email. The judge
  looks at it; Deshpande turns to it, then looks down; the judge rules; a silence, one stroke of the gavel, then the
  room; Priya turns to Arjun. The stacked cheers are gone.
- **Sacrifice**: the quieter court without Priya, Kamala and Vikram as before; then the court steps (a new small set):
  the doors swing shut behind Arjun, he sits on the top step, his phone buzzes and he reads Priya's message on its
  screen (its light on his hands), his reply appears under it. A shorter epilogue keeps the long-term facts.
- **Revolution**: the six timestamp cards become a clock to sunrise with three staged scenes: **5:10 AM** a reporter's
  phone wakes on the stool by her bed, the screen lights her face as she reads; **5:55 AM** a shopkeeper pulls his
  shutter up on Bazaar Road and the television inside is already on the story; **6:20 AM** upstairs in the shelter Priya
  hears the street before she sees it and goes to the window; **6:25 AM** the roof at sunrise. Each next place's sound
  starts before the cut (the shutter, the crowd). No beeps. The epilogue carries "by seven ... by ten".
  The old order ended on a 10 AM card followed by a sunrise; now title, sky and voice agree.
- Sets used only by an ending (courtroom, roof, steps, reporter's flat, shop) are built behind the white-out after the
  choice, not when Act V opens.

### 1.8 Act III

- **Harishankar** is a man from the lanes who keeps the temple's accounts in the evenings, not its priest: a faded
  cotton shirt and everyday trousers (the Wood_Male_01 model, whose face no one else in the city now wears: it was
  taken out of the crowds), his cloth bag on the back of his chair. He sits at the temple office's table inside the
  gate writing up the day's offerings in a ledger. His lines are now the actor's recordings (23 Nov 2026,
  `Docs/VOICE_CHECK.md`): he sees Priya's look in Arjun's eyes, and his question is implied ("someone who understands
  what matters") rather than asked; the dial's three intentions carry it. The beat: he looks up, asks his question, **closes the ledger** to hear Arjun's answer, studies him a
  moment, **takes the sealed envelope out of the table drawer** and holds it out, then answers.
  Spoken forms, objective, prompt, credits, the Curator's line (`cur2_06`, re-voiced) and the case-file wording are
  updated; the saved `panditAnswer`, the `a3_pandit_*` line ids and the evidence notes' stored strings are not (the
  case file shows the new wording for old notes through `EvidenceText`).
- **The murti**: see section 2.
- **Library and record room**: the two bookcases nearest the aisle have stitched municipal ledgers (thread bands,
  paper labels with years), books pushed in at different depths and more gaps showing the back panel; the others keep
  their grouped meshes. BLOCK and SHELF numbers are enamel plates in depth-tested lettering (they were default-material
  text, which draws through walls). The 1942 slot on Block C / 14 is an open, empty box-file with the sign-out sheet
  clipped beside it; opening the cage turns the view to the sheet and holds it before the card. The librarian's drawer
  slides out on the brass key when the code is right.

## 2. The Kali murti: what the asset is

`Assets/Resources/Idols/Kali.fbx` (41 MB) was inspected directly: it is FBX 7.7 from Blender 5.1.1, holding a Cinema
4D camera, one mesh named **ImagePlane** (130,827 vertices, about 215,000 triangles) with a **TrueDepthMaterial**, and
one embedded texture, **Maa Kali HF.png** (2048 x 3072, upscaled with Topaz Gigapixel; the file path says it came from
a "Copilot" folder). The texture is a full rectangular picture of Dakshina Kali (blue-skinned, four arms, sword and
severed head, the garland of heads, standing on Shiva) with its background (halo, trident, skulls, a lamp, a bowl of
blood) and realistic dripping blood. The mesh is that picture pushed into depth relief. It is in the player build
(7.1 MB) and loads; it is not a carved sculpture, so from the side its rectangle and stretched edges show.

What I did with it, without changing the asset: it is set as a painted relief icon in a carved sandstone frame whose
inner edges cover the picture's borders and whose depth hides its sides, set back on the pedestal; its paint is matt;
the garland hangs from the frame above the image and down its pilasters, clear of the face and hands (the old garland
crossed the middle of the image); a low warm lamp from the front left with soft shadows gives the relief its form and a
contact shadow on the pedestal, with a little cool light from the doorway. The old fallback to Uma-Maheshvara (a
different deity) is gone: if Kali does not load, the shrine shows a red-veiled murti with a silver crown, a development
build says so on screen, and the log records an error.

Your call (not done): a carved Kali murti from a documented, licensed source would replace the relief. The current
picture's realistic blood also sits oddly with a non-violent game. The Ganesha by the gate (a Minneapolis Institute of
Art scan, CC0) is a real sculpture and keeps its black stone.

## 3. Performance and faces

(to be filled from the runs)

## 4. Playthroughs

(to be filled from the runs)

## 5. Still open

(to be filled)
