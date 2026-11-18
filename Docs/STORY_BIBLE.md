# Shadows of Chandni: Story Bible

This is the single source of truth for the timeline, who knows what, where each piece of evidence comes from,
and what each branch and ending depends on. If dialogue, documents or VO disagree with this file, this file wins
and the line gets fixed (see "Changing a line" at the end).

Status: **v1, written in the polish pass of 18 Nov 2026.** Decisions marked **(decided)** resolve contradictions
found in the audit (`Docs/POLISH_AUDIT.md`).

---

## 1. Premise

Chandnipur is an old Indian city. Councilor **Meghna Deshpande** is pushing the *Chandnipur Renewal Project*: a
mall over **Basti Nagar**, home to about 500 families. The legal basis is a **1965 acquisition order**, which is
forged. The **1942 deed of grant** from the Maharaja gives the land to its residents "in perpetuity".
Advocate **Priya Kapoor** found the proof and went into hiding. Private investigator **Arjun Mehra** has one night
to find her. The game is non-violent: the stakes are trust, exposed witnesses, compromised evidence, public
disclosure and institutional retaliation.

## 2. Timeline (decided)

All of it happens over one night, **Tuesday into Wednesday**. The hearing is **Wednesday 10:00 AM** at the Municipal Court.

| When | Where | Act | What happens |
|---|---|---|---|
| ~3 weeks before | Vikram's stall | (backstory) | Priya starts investigating; she confides in Vikram. |
| 2 weeks before | Library record room | (backstory) | Priya finds the 1942 deed. Reddy, who has been watching the record room, **signs it out officially in her own name** and locks it in the Crime Branch evidence room (chain of custody). Priya makes a **certified copy** first and leaves it with Panditji. |
| ~1 week before | Municipal Office | (backstory) | Priya takes the **original 1965 acquisition file**, the forged order with the officials' signatures, out of the Municipal Office. That is the "government property" Deshpande can have her arrested for. Rohan, an intern there, had told her where it was kept. |
| 2 days before | Vikram's stall | (backstory) | Priya gives Vikram the USB drive. Two men break his kettle. |
| ~1 day before | | (backstory) | Priya vanishes. She hides at the NGO shelter by the river, where Reddy's people watch over her. |
| Tue 6:00 PM | Priya's flat | I | Arjun searches the flat and meets Kamala. The note arrives under the door. |
| Tue 7:30 PM | Market | II | Bhaskar, Vikram, Reddy watching, the black SUV. |
| Tue 9:00 PM | Library | III | USB, record room, the empty folder and Reddy's signature. |
| Tue 10:10 PM | Kali temple | III | Panditji's question; the envelope with the certified copy; Arjun picks the branch. |
| Tue ~11:05–11:15 PM | Café Irani / Kapoor house / police station | IV | **(decided)** All three routes start around 11 PM. The café route used to say 7:05 AM, which broke the night. |
| Wed ~4:00 AM | NGO shelter | V | Priya upstairs. **(decided)** The hearing is **six hours** away (it used to say four). |
| Wed 4–6 AM | The Gallery | V | The casket choice. |
| Wed 10:00 AM | Municipal Court | Endings | The hearing (Justice / Sacrifice), or the public leak (Revolution). |

## 3. The documents and who holds them (decided)

| Document | What it is | Held by, and when | Who knows |
|---|---|---|---|
| **1942 deed (original)** | The Maharaja's grant to Basti Nagar | Library record room until 2 weeks ago, then the **Crime Branch evidence room** under Reddy's signature | Reddy; Arjun once he sees the sign-out sheet (Act III); he learns the reason only on Reddy's route (Act IV). Deshpande's people know only that "S. Reddy" took it (the Municipal emails). |
| **1942 deed (certified copy)** | Admissible copy Priya made before Reddy took the original | Panditji, then Arjun (end of Act III) | Priya, Panditji, Arjun |
| **1942 deed (scan)** | Photo on the USB | Vikram, then Arjun (Act II) | Priya, Vikram, Arjun |
| **1965 acquisition order (forged original file)** | The forgery with officials' signatures | Taken by **Priya** from the Municipal Office; she has it at the shelter | Priya; Rohan (he told her where it was); Arjun in Act V |
| **Raj's recordings** | 6 months of Raj and Deshpande planning the fraud | Raj's phone, then Arjun (Act IV, café route) | Raj, Arjun |
| **Municipal emails** | Officials knew the 1965 order was forged and planned to seize the deed | Rohan, then Arjun (Act IV, Kapoor house route) | Rohan, Arjun |

So "the originals" Priya mentions in Act V means the forged 1965 file. The 1942 deed itself is with Reddy.
In the Justice hearing Priya presents the 1965 file and the certified copy, and the original deed is in Crime Branch
custody (Reddy brings it in person if she is an ally).

## 4. Characters: motive and knowledge

| Character | Wants | Fears | Knows at the start | Can lose |
|---|---|---|---|---|
| **Arjun Mehra** | To find Priya; to finish one case cleanly | Becoming the kind of PI who looks away | Nothing; Kamala's call | His witnesses' trust; his licence (Revolution) |
| **Priya Kapoor** | The families keep their homes | Arrest for taking the 1965 file; her mother being hurt | Everything about the forgery | Her freedom (Justice), her peace (Revolution), her courage (Sacrifice) |
| **Kamala Kapoor** | Her daughter alive | Priya ending up like the cases Priya fights | Priya was scared; forgery was mentioned on the phone | Her home is ransacked (Act IV, Kapoor route) |
| **Rohan Kapoor** | To undo the harm he caused | Being the reason his sister is hunted | Where the 1965 file was kept; the emails | His sister's trust |
| **Insp. Sonal Reddy** | A case that holds up in court | Exposing her undercover work too early | Has the deed; protects the shelter | Her cover |
| **Raj Malhotra** | To come out of this as "useful" | Prison; Deshpande | The whole plan, recorded | Everything, if he testifies |
| **Vikram Sahu** | To protect Priya and his basti | The men who broke his kettle | Priya's research (USB), her mention of the temple | His livelihood |
| **Bhaskar** | Fares, gossip | | Drove Priya toward the old mill, then back | |
| **Pandit Harishankar** | That the truth serves people | | Holds the certified copy | |
| **Councilor Deshpande** | The Renewal Project | Exposure | Reddy signed out the deed; a PI is asking questions | Her seat, her freedom |
| **The Curator** | To collect the story of how it ends | Nothing | Everything, but he only remarks on what Arjun actually did | |

## 5. Branches and conditions (as built today)

| Decision | Where | Stored as | Effect today |
|---|---|---|---|
| Vikram's trust tier | Act II | `vikramDone`, evidence notes | Unlocks the USB and his warnings |
| Note the SUV's plate / hide / walk away | Act II | `hasPlate`, evidence | Evidence note |
| Panditji's question (truth / people / both) | Act III | `panditAnswer` 0/1/2 | Panditji's reply; the Curator's swap line after Act III |
| Act IV route (café / Kapoor house / police) | Act III end | `branch` 0/1/2 | Which act IV variant; which ally you can win |
| Ally won | Act IV | `allyRaj` / `allyRohan` / `allyReddy` | Who testifies in Justice; the Curator's swap line after Act IV |
| Recordings / emails | Act IV | `hasRecordings` / `hasEmails` | Revolution needs one of them |
| Casket | Act V | `endingsSeen` (profile) | Justice / Sacrifice / Revolution |

**Ending gates today:** Justice needs `EvidenceCount >= 18` (clues + evidence notes); Revolution needs recordings
or emails; Sacrifice is always open. **Planned replacement** (not implemented yet, see the checklist): Justice
needs an authenticated deed (certified copy, or Reddy as ally), at least two independent corroborations (USB scan,
sign-out sheet, recordings, emails, Vikram's testimony) and a protected witness. Revolution needs publishable
material (recordings or emails). The casket card will show exactly which conditions are met.

## 6. Ending consequences

| Ending | What happens | Cost |
|---|---|---|
| **Justice** | Priya testifies, allies testify, Deshpande is arrested, the project is halted | Priya walks into the courtroom she could be arrested in |
| **Sacrifice** | Arjun goes in alone, the case stalls, the eviction is frozen, Priya stays hidden | The families' fight drags on; Priya's guilt |
| **Revolution** | Everything leaks to the press, national protest, Deshpande flees | No courtroom win; Priya needs police protection |

## 7. The Curator

The host of a "collection" of stories, outside time. He is urbane, dry, never cruel without cause, and he only
comments on what the player actually did. He is never shown in Chandnipur itself. He is heard once in each of
Acts III and IV (the "flashes") and seen between acts in his Gallery.
- **Prologue (before Act I):** his welcome. He takes *Shadows of Chandni* down from the shelf.
- **After Acts II, III and IV:** short visits with a candle. Each has one exhibit from the act that just ended
  (kettle, deed, three figures) and a swap line for the Act III answer and the Act IV ally.
- **Act V:** the casket choice and his verdict on each ending. **No candle**: it is the last time the book is open.

## Changing a line

1. Edit the text in `Tools/acts_dialogue.py` (Acts III–V and the Curator) or `Tools/make_voices.py` (Acts I–II, whose
   subtitles also live in `PriyaRoomStory.cs` / `MarketStory.cs`).
2. Delete the old `Assets/Resources/VO/<id>.wav` so it is re-recorded, then run the script.
3. Update this bible if the change moves a fact.
