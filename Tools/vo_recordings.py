#!/usr/bin/env python3
"""Puts the voice actors' recordings in place of the text-to-speech lines, and checks every one.

    python3 Tools/vo_recordings.py            convert the recordings, then check them
    python3 Tools/vo_recordings.py --check    only check what is in Assets/Resources/VO

The recordings are MP3s named by act and scene: Arjun's in "arjun voice overs 2/", the rest of the cast's in
"Voice Overs/<character>/". MAP says which line(s) each one voices. For each line this
  - trims the silence at both ends (keeping 60 ms before the first sound and 250 ms after the last),
  - splits a take that holds several lines (the Act I opening) at its pauses into the lines the game plays,
  - joins a line recorded in two takes, with a breath between them,
  - cuts out a passage the story no longer has (Rohan's deed from the library), in the quiet either side of it,
  - brings it to the loudness of the rest of the VO (-16 LUFS integrated, one gain for the whole line, a fast limiter
    holding the odd plosive under -1.5 dB true peak) as 44.1 kHz mono 16-bit WAV, written over the text-to-speech file of
    the same line in Assets/Resources/VO (a line that had no voice before also gets a .meta with the other VO clips' import
    settings).
Then it transcribes every written file on the Mac (Tools/transcribe_words.swift: macOS's on-device recogniser, nothing
is uploaded), compares the words heard with the subtitle the game shows, and writes
  - Assets/Scripts/VoTiming.cs: where each subtitle word ends in its recording, so the subtitles change phrase when
    the voice does;
  - Docs/VOICE_CHECK.md: per character and line, the recording, its length, the subtitle and what was heard, plus the
    lines of those characters that have no recording yet.
make_voices.py and acts_dialogue.py skip every line listed here, even with --all, so `say` never overwrites them.
After a run, re-make the lip-sync of the faces that speak them: python3 Tools/lipsync/npc_lipsync.py <ids>.
"""
import array, difflib, json, math, os, re, subprocess, sys, tempfile, uuid

TOOLS = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(TOOLS, ".."))
VO = os.path.join(ROOT, "Assets", "Resources", "VO")
TIMING_CS = os.path.join(ROOT, "Assets", "Scripts", "VoTiming.cs")
REPORT = os.path.join(ROOT, "Docs", "VOICE_CHECK.md")
ACT12_CS = [os.path.join(ROOT, "Assets", "Scripts", n) for n in ("PriyaRoomStory.cs", "MarketStory.cs")]
META_TEMPLATE = os.path.join(VO, "arjun_clue_1_Laptop.wav.meta")
LOCALE = "en-IN"

# Acts I and II keep their subtitles in PriyaRoomStory.cs and MarketStory.cs: these are the exact strings the game
# shows with each recording (checked against those files on every run). Acts III-V take theirs from acts_dialogue.py.
ACT12 = {
    "arjun_intro1": "In Chandnipur, everyone has a secret. The city is old enough to keep them.",
    "arjun_intro2": "But sometimes a secret gets too big for the walls that hold it, and it starts to leak through the cracks.",
    "arjun_intro3": "That's when people like me get a phone call.",
    "arjun_closing": "Someone is watching me. And they know Priya. The question is, are they protecting her, or protecting themselves?",
    "q_k_a": "Did she mention anyone threatening her?",
    "q_k_b": "Was she working on anything dangerous?",
    "q_k_c": "Did the police search this place?",
    "arjun_clue_1_Laptop": "Someone cleaned this. Thoroughly. Priya is careful, but this looks rushed. She was in a hurry, or someone else did this after she left.",
    "arjun_clue_2_StickyNote": "She hid this deliberately. This number is important enough to keep off the laptop.",
    "arjun_clue_3_NewspaperClippings": "Priya was fighting this project. Hard. This wasn't casual activism. This was an obsession.",
    "arjun_clue_4_HalfPackedSuitcase": "She was planning to run. But she didn't make it to the station. Something stopped her. Or someone.",
    "arjun_clue_5_TwoChaiCups": "She had a visitor. Someone she was comfortable enough to share chai with. This wasn't a stranger.",
    "arjun_clue_6_TornPhotograph": "She destroyed the other person's face on purpose. She doesn't want anyone to know who this is. Or she's angry at them.",
    "arjun_clue_7_BurnerPhone": "A secret phone. V could be anyone. But Safe, that sounds like a location. Or a person she trusts.",
    "arjun_clue_8_Diary": "She found something in the land records. Something big enough to tear pages out of her own diary to protect.",
    "arjun_clue_9_BrokenWindowLatch": "Someone broke in. Recently. The rust on the broken metal is fresh. This wasn't Priya. She would have used the door.",
    "q_b_a": "Do you know a woman named Priya Kapoor? Lives in the old quarter?",
    "q_b_a2": "She's missing.",
    "q_b_b": "What did the person on the phone sound like?",
    "q_b_c": "Did anyone follow you?",
    "q_v_1a": "This is your number, Vikram. Priya hid it in her apartment. She trusted you.",
    "q_v_1b": "Her mother is terrified, Vikram. She thinks something terrible has happened to her daughter. If you know anything, please.",
    "q_v_1c": "I know you're lying. I can involve the police if I have to.",
    "q_v_2a": "What did she find?",
    "q_v_2b": "Who is she afraid of?",
    "q_v_2c": "Where is she now?",
    "arjun_reddy": "A cop. Plainclothes. She's been following me, or following Vikram. Whose side is she on?",
    "arjun_act2_end": "The streets of Chandnipur know everything. The question is whether they'll tell you the truth before it's too late.",
    # Kamala, Act I (PriyaRoomStory.cs)
    "kamala_welcome1": "Mr. Mehra? Thank you for coming. Please, come inside. The police say she left on her own. But a mother knows. Something is wrong.",
    "kamala_welcome2": "I haven't touched anything. I thought... I thought someone should look at it properly.",
    "kamala_ask": "Have you found anything?",
    "kamala_a": "Not directly. But two weeks ago, she stopped coming home for dinner. She said she was working late at the NGO. But I could hear her on the phone, arguing with someone. She used words like 'forgery' and 'original documents'. I didn't understand, but I understood her voice. She was scared.",
    "kamala_b": "Priya's work is always dangerous, Mr. Mehra. She fights for people who have no one else to fight for them. The slum families in Basti Nagar, they were going to lose their homes. Priya was trying to stop it. She told me, 'Maa, if I can prove the land belongs to them, no one can touch them.' She was so hopeful. And then, suddenly, she wasn't.",
    "kamala_c": "An inspector came. A woman. She looked around for ten minutes, asked me a few questions, and left. She didn't seem interested. She said Priya probably went to visit a friend. But Priya's phone is here. Her wallet is here. What kind of person leaves without their phone?",
    # Bhaskar and Vikram, Act II (MarketStory.cs)
    "b_greet": "Where to, sir? Market? Good, good. Traffic is less now. The gods are kind.",
    "b_a": "Priya madam? Of course! She takes my auto sometimes. Going to the court, going to the slum. Good woman. Always pays full fare, never bargains. Why?",
    "b_a2": "Missing? Arre bhai... actually, I drove her two nights ago. Late, around ten. She flagged me down near the temple. She was on the phone, very tense. She kept saying, don't come, I'll handle it. Then she told me to take her to the old mill. You know, the abandoned textile mill near the river. But halfway there, she got another call, and her face went white. She said, take me back. Take me back, right now. I asked if everything was okay. She gave me five hundred rupees for a fifty rupee ride, and said, forget this ride. You never saw me tonight.",
    "b_b": "I couldn't hear clearly, but it sounded like a man. Older. Speaking Hindi with an English accent, you know, the rich people type. Priya madam was saying, I have the documents, you can't stop me. Then later, I know what you did to the records. I don't know what it means, but it sounded serious.",
    "b_c": "Now that you mention it... there was a black car. An SUV. It was behind us for a while. I thought it was just traffic, but it turned when I turned. When Priya madam told me to go back, the SUV was gone. Maybe it was nothing. Maybe it wasn't.",
    "v_cold": "Chai? Cutting or full? ... No, I don't know any Priya Kapoor. You have the wrong stall, brother.",
    "v_t1a": "Where did you get that? ... Okay. Okay. Sit down. But keep your voice low.",
    "v_t1b": "Kamala-ji... she's a good woman. ... Alright. Sit. But if anyone asks, you're just drinking chai.",
    "v_t1c": "Then involve the police. I have nothing to say to you.",
    "v_t2_intro": "Priya is like a daughter to me. I've known her since she was this tall. She came to me three weeks ago. She was investigating the Renewal Project, the one that's going to destroy Basti Nagar. My home, brother. I grew up there. My father built our house with his own hands. And now Councilor Deshpande wants to bulldoze it for a shopping mall.",
    "v_t2a": "She found that the land records were forged. The government claims the Basti Nagar land was acquired in 1965 under eminent domain. But Priya found the original deed from 1942. It shows the land was granted to the residents by the Maharaja himself. It was never acquired. The entire Renewal Project is illegal.",
    "v_t2b": "Councilor Meghna Deshpande. She is the one behind the project. But it's not just her. There's a builder, Raj Malhotra, and people inside the police. Priya said the corruption goes all the way up. She was scared, but she wouldn't stop. That girl has a spine of steel.",
    "v_t2c": "I don't know exactly. But the last time I spoke to her, she mentioned the Kali Temple. She said, Kali Ma will protect the truth. I thought she was being poetic, but now I think she meant it literally.",
    "v_t3a": "Wait. There's something else.",
    "v_t3b": "Be careful, Mr. Mehra. The people behind this, they don't play games. Two days ago, two men came to my stall. They asked if a woman had been asking questions about the land records. I said no. They broke my kettle and told me to keep saying no. These are not people you argue with.",
    "v_phone": "They know you're here. Deshpande's people. They were watching the stall. You need to leave. Now.",
}

A = "arjun voice overs 2/"
K = "Voice Overs/kamala voice overs/"
B = "Voice Overs/bhaskar/"
V = "Voice Overs/vikram/"
P = "Voice Overs/panditji/"
R = "Voice Overs/raj malhotra/"
RO = "Voice Overs/rohan/"
C = "Voice Overs/Curator/"

# recording -> the line(s) it voices, in the order they are spoken. A tuple of recordings is one line in several takes,
# joined with a breath between them. drop=(first words, last words) cuts that passage out of the take.
MAP = [
    # ---- Arjun
    (A + "act1 arjun1.mp3", ["arjun_intro1", "arjun_intro2", "arjun_intro3"]),
    (A + "act1 arjun2.mp3", ["arjun_closing"]),
    (A + "act1 arjunchoice1.mp3", ["q_k_a"]),
    (A + "act1 arjunchoice2.mp3", ["q_k_b"]),
    (A + "act1 arjunchoice3.mp3", ["q_k_c"]),
    (A + "act1 arjuncluelaptop.mp3", ["arjun_clue_1_Laptop"]),
    (A + "act1 arjuncluestickynote.mp3", ["arjun_clue_2_StickyNote"]),
    (A + "act1 arjuncluenewspaperclipping.mp3", ["arjun_clue_3_NewspaperClippings"]),
    (A + "act1 arjuncluesuitcase.mp3", ["arjun_clue_4_HalfPackedSuitcase"]),
    (A + "act1 arjuncluechaicups.mp3", ["arjun_clue_5_TwoChaiCups"]),
    (A + "act1 arjuncluetornphoto(trashbin).mp3", ["arjun_clue_6_TornPhotograph"]),
    (A + "act1 arjuncluephone.mp3", ["arjun_clue_7_BurnerPhone"]),
    (A + "act1 arjuncluediary.mp3", ["arjun_clue_8_Diary"]),
    (A + "act1 arjuncluewindow.mp3", ["arjun_clue_9_BrokenWindowLatch"]),
    (A + "act2 arjunbhaskarchoice1part1.mp3", ["q_b_a"]),
    (A + "act2 arjunbhaskarchoice1part2.mp3", ["q_b_a2"]),
    (A + "act2 arjunbhaskarchoice2.mp3", ["q_b_b"]),
    (A + "act2 arjunbhaskarchoice3.mp3", ["q_b_c"]),
    (A + "act2 arjunvikramchoice1.mp3", ["q_v_1a"]),
    (A + "act2 arjunvikramchoice2.mp3", ["q_v_1b"]),
    (A + "act2 arjunvikramchoice3(wrongchoice).mp3", ["q_v_1c"]),
    (A + "act2 warmingvikramchoice1.mp3", ["q_v_2a"]),
    (A + "act2 warmingvikramchoice2.mp3", ["q_v_2b"]),
    (A + "act2 warmingvikramchoice3.mp3", ["q_v_2c"]),
    (A + "act2 arjunfollowlady.mp3", ["arjun_reddy"]),
    (A + "act2 arjun(endvoiceover).mp3", ["arjun_act2_end"]),
    (A + "act3 arjunkalipart1.mp3", ["a3_arjun_q"]),
    (A + "act3 arjunkalichoice1.mp3", ["a3_ans_a"]),
    (A + "act3 arjunkalichoice2.mp3", ["a3_ans_b"]),
    (A + "act3 arjunkalichoice3.mp3", ["a3_ans_c"]),
    (A + "act3 arjunpuzzle1.mp3", ["a3_arjun_unlock"]),
    (A + "act3 arjunpuzzle2.mp3", ["a3_arjun_reddy"]),
    (A + "act3 arjunfinalvoiceover.mp3", ["a3_vo_end"]),
    (A + "act4 arjuncafe1.mp3", ["a4a_arjun_why"]),
    (A + "act4 arjuncafechoice1.mp3", ["a4a_q_a"]),
    (A + "act4 arjuncafechoice2.mp3", ["a4a_q_b"]),
    (A + "act4 arjuncafechoice3.mp3", ["a4a_q_c"]),
    (A + "act4 arjunkapoor1.mp3", ["a4b_arjun_find"]),
    (A + "act4 arjunkapoor2.mp3", ["a4b_arjun_told"]),
    (A + "act4 arjunkapoorchoice1.mp3", ["a4b_q_a"]),
    (A + "act4 arjunkapoorchoice2.mp3", ["a4b_q_b"]),
    (A + "act4 arjunkapoorchoice3.mp3", ["a4b_q_c"]),
    (A + "act4 arjunreddy1.mp3", ["a4c_arjun_id"]),
    (A + "act4 arjunreddychoice1.mp3", ["a4c_q_a"]),
    (A + "act4 arjunreddychoice2.mp3", ["a4c_q_b"]),
    (A + "act4 arjunreddychoice3.mp3", ["a4c_q_c"]),
    (A + "act5 arjun1.mp3", ["a5_arjun_mother"]),
    (A + "act5 arjunend1.mp3", ["a5_arjun_justice"]),
    (A + "act5 arjunend2.mp3", ["a5_arjun_sacrifice"]),
    (A + "act5 arjunend3.mp3", ["a5_arjun_revolution"]),
    # ---- Kamala Kapoor
    (K + "act1 kamala1.mp3", ["kamala_welcome1"]),
    (K + "act1 kamala2.mp3", ["kamala_welcome2"]),
    (K + "act1 kamala3.mp3", ["kamala_ask"]),
    (K + "act1 kamala4.mp3", ["kamala_a"]),
    (K + "act1 kamala5.mp3", ["kamala_b"]),
    (K + "act1 kamala6.mp3", ["kamala_c"]),
    (K + "act4 kamala1.mp3", ["a4b_kamala_1"]),
    (K + "act4 kamala2.mp3", ["a4b_kamala_2"]),
    (K + "end2 kamala.mp3", ["end2_kamala"]),
    # ---- Bhaskar
    (B + "act2 bhaskar1.mp3", ["b_greet"]),
    (B + "act2 bhaskar2.mp3", ["b_a"]),
    (B + "act2 bhaskar3.mp3", ["b_a2"]),
    (B + "act2 bhaskar4.mp3", ["b_b"]),
    (B + "act2 bhaskar5.mp3", ["b_c"]),
    # ---- Vikram Sahu
    (V + "act2 vikram1.mp3", ["v_cold"]),
    (V + "act2 vikram2.mp3", ["v_t1a"]),
    (V + "act2 vikram3.mp3", ["v_t1b"]),
    (V + "act2 vikram4.mp3", ["v_t1c"]),
    ((V + "act2 vikram5.mp3", V + "act2 vikram6.mp3"), ["v_t2_intro"]),
    (V + "act2 vikram7.mp3", ["v_t2a"]),
    (V + "act2 vikram8.mp3", ["v_t2b"]),
    (V + "act2 vikram9.mp3", ["v_t2c"]),
    (V + "act2 vikram10.mp3", ["v_t3a"]),
    (V + "act2 vikram11.mp3", ["v_t3b"]),
    (V + "act2 vikram12.mp3", ["v_phone"]),
    # ---- Harishankar
    (P + "act3 panditji1.mp3", ["a3_pandit_1"]),
    (P + "act3 panditji2.mp3", ["a3_pandit_2"]),
    (P + "act3 panditji3.mp3", ["a3_pandit_a"]),
    (P + "act3 panditji4.mp3", ["a3_pandit_b"]),
    (P + "act3 panditji5.mp3", ["a3_pandit_c"]),
    # ---- the unknown caller (already through the phone: no filter added)
    ("Voice Overs/act3 distorted voice.mp3", ["a3_caller"]),
    # ---- Raj Malhotra
    (R + "act4 raj1.mp3", ["a4a_raj_1"]),
    ((R + "act4 raj2.mp3", R + "act4 raj3.mp3"), ["a4a_raj_2"]),
    (R + "act4 raj4.mp3", ["a4a_raj_a"]),
    (R + "act4 raj5.mp3", ["a4a_raj_b"]),
    (R + "act4 raj5.mp3", ["a4a_raj_where"]),       # the same lead, volunteered at the dhaba when Arjun never asked
    (R + "act4 raj6.mp3", ["a4a_raj_c"]),
    (R + "act4 raj7.mp3", ["a4a_raj_made"]),
    (R + "act4 raj8.mp3", ["a4a_raj_dhaba"]),
    # ---- Rohan Kapoor
    (RO + "act4 rohan1.mp3", ["a4b_rohan_1"]),
    # the take has Priya breaking into the library for the deed; Reddy signed the deed out (Act III), Priya took the 1965 file
    (RO + "act4 rohan2.mp3", ["a4b_rohan_2"], ("I didn't think", "took it,")),
    (RO + "act4 rohan3.mp3", ["a4b_rohan_a"]),
    (RO + "act4 rohan4.mp3", ["a4b_rohan_b"]),
    (RO + "act4 rohan5.mp3", ["a4b_rohan_c"]),
    # ---- the Curator: one take per scene, split at its pauses
    (C + "Curator 1.mp3", ["cur0_%02d" % i for i in range(1, 16)]),
    (C + "Curator 2.mp3", ["cur1_%02d" % i for i in range(1, 9)]),
    (C + "Curator 3.mp3", ["cur2_%02d" % i for i in range(1, 8)]),
    (C + "Curator 4.mp3", ["cur2_%02d" % i for i in range(8, 12)]),
    (C + "Curator 5.mp3", ["cur3_%02d" % i for i in range(1, 11)]),
    (C + "Curator 6.mp3", ["endJ_%02d" % i for i in range(1, 6)]),
    (C + "Curator 7.mp3", ["endS_%02d" % i for i in range(1, 6)]),
    (C + "Curator 8.mp3", ["endR_%02d" % i for i in range(1, 6)]),
    (C + "Curator 9.mp3", ["flash1_%02d" % i for i in range(1, 4)]),
    (C + "Curator 10.mp3", ["flash2_%02d" % i for i in range(1, 4)]),
]

# who speaks, for the report: the speaker names used in make_voices.py and acts_dialogue.py
CHARACTERS = [
    ("Arjun Mehra", {"ARJUN", "ARJUN (V.O.)"}),
    ("Kamala Kapoor", {"KAMALA", "KAMALA KAPOOR"}),
    ("Bhaskar", {"BHASKAR"}),
    ("Vikram Sahu", {"VIKRAM", "VIKRAM SAHU"}),
    ("Harishankar", {"HARISHANKAR"}),
    ("The unknown caller", {"UNKNOWN CALLER"}),
    ("Raj Malhotra", {"RAJ MALHOTRA"}),
    ("Rohan Kapoor", {"ROHAN KAPOOR"}),
    ("The Curator", {"THE CURATOR"}),
]

# make_voices.py's voice constants that are not character names
SPEAKER_NAMES = {"THUG": "Deshpande's men", "DESHPANDE'S MAN": "Deshpande's men", "DESHPANDE'S LAWYER": "Deshpande's lawyer",
                 "VEND1": "the market's hawkers", "VEND2": "the market's hawkers", "SHOPPER": "a shopper",
                 "THE JUDGE": "the judge", "RECORDING": "the recording in Act III",
                 "PRIYA (LETTER)": "Priya's letter", "REPORTER": "the reporter", "RESIDENT": "the shelter's resident",
                 "TELEVISION": "the television news"}

LEAD, TAIL = 0.06, 0.25          # seconds of quiet kept before the first sound and after the last
THRESHOLD = "-50dB"              # quieter than this counts as silence (the recordings' floor is about -60 dB)
BREATH = 0.55                    # seconds between two takes of one line


def entries():
    """(recordings, line ids, passage to drop) for every row of MAP."""
    for row in MAP:
        srcs = row[0] if isinstance(row[0], tuple) else (row[0],)
        yield srcs, row[1], row[2] if len(row) > 2 else None


def recorded_ids():
    """Every line a voice actor recorded. The text-to-speech generators leave these alone."""
    return {lid for _, ids, _ in entries() for lid in ids}


# ------------------------------------------------------------------ text
def tts_act12():
    """Acts I-II lines in make_voices.py: id -> (speaker constant, text). (Parsed: importing it would run it.)"""
    with open(os.path.join(TOOLS, "make_voices.py")) as f:
        return {m.group(1): (m.group(2), m.group(3)) for m in
                re.finditer(r'^\s*"(\w+)": \(([A-Z0-9]+), "(.*)"\),\s*$', f.read(), re.M)}


def subtitles():
    """id -> the subtitle the game shows, id -> speaker, and every text-to-speech line (id, speaker, text)."""
    sys.path.insert(0, TOOLS)
    import acts_dialogue
    text = dict(ACT12)
    speaker = {lid: who for lid, (who, _) in tts_act12().items()}
    tts = [(lid, who, line) for lid, (who, line) in tts_act12().items()]
    for lid, who, _voice, line in acts_dialogue.LINES:
        text.setdefault(lid, acts_dialogue.shown(line))
        speaker.setdefault(lid, who)
        tts.append((lid, who, acts_dialogue.shown(line)))
    return text, speaker, tts


def cs(s):
    return s.replace("\\", "\\\\").replace('"', '\\"')


def check_act12_sources():
    """Every Acts I-II subtitle here must be the string the game shows, or its word timing would never be used."""
    src = "".join(open(p).read() for p in ACT12_CS)
    bad = [lid for lid, line in ACT12.items() if '%s"' % cs(line) not in src]       # (a literal may lead with its speaker)
    if bad:
        sys.exit("these subtitles are not in PriyaRoomStory.cs / MarketStory.cs as written here: " + ", ".join(bad))


def tokens(text):
    """The subtitle's words, each with the character offset where it ends."""
    return [(m.group(0), m.end()) for m in re.finditer(r"\S+", text)]


def norm(word):
    return re.sub(r"[^a-z0-9]", "", word.lower())


def align(text, words):
    """Matches the subtitle's words to the words heard. Returns, per subtitle word, (start, end, heard?) in seconds;
    a word the recogniser heard differently (names, mostly) takes its time from its neighbours."""
    sub = tokens(text)
    a = [norm(w) for w, _ in sub]
    b = [norm(w["w"]) for w in words]
    times = [None] * len(sub)
    sm = difflib.SequenceMatcher(None, a, b, autojunk=False)
    for blk in sm.get_matching_blocks():
        for k in range(blk.size):
            w = words[blk.b + k]
            times[blk.a + k] = (w["s"], w["e"], True)
    # a replaced run spans the heard words it replaced, shared out by length
    for op, i1, i2, j1, j2 in sm.get_opcodes():
        if op == "replace" and j2 > j1:
            s, e = words[j1]["s"], words[j2 - 1]["e"]
            total = sum(len(a[i]) + 1 for i in range(i1, i2))
            acc = 0
            for i in range(i1, i2):
                t0 = s + (e - s) * acc / total
                acc += len(a[i]) + 1
                times[i] = (t0, s + (e - s) * acc / total, False)
    # anything left (said but not heard) sits between its neighbours
    end = words[-1]["e"] if words else 0.0
    for i in range(len(times)):
        if times[i] is None:
            prev = next((times[j][1] for j in range(i - 1, -1, -1) if times[j]), 0.0)
            nxt = next((times[j][0] for j in range(i + 1, len(times)) if times[j]), end)
            times[i] = (prev, max(prev, nxt), False)
    return times, sm.get_opcodes(), a, b


def find_words(words, phrase):
    """Index of the first heard word of phrase in words (matched on letters and digits)."""
    want = [norm(w) for w in phrase.split()]
    have = [norm(w["w"]) for w in words]
    for i in range(len(have) - len(want) + 1):
        if have[i:i + len(want)] == want:
            return i
    sys.exit("could not find %r in the take (heard: %s)" % (phrase, " ".join(w["w"] for w in words)))


# ------------------------------------------------------------------ audio
def ffmpeg(*args):
    return subprocess.run(["ffmpeg", "-hide_banner", "-nostats"] + list(args), capture_output=True, text=True, check=True)


def duration(path):
    r = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", path],
                       capture_output=True, text=True, check=True)
    return float(r.stdout.strip())


def energy(path):
    """10 ms RMS levels in dBFS."""
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-ac", "1", "-ar", "44100", "-f", "s16le", "-"],
                         capture_output=True, check=True).stdout
    pcm = array.array("h"); pcm.frombytes(raw)
    out = []
    for i in range(0, len(pcm) - 441 + 1, 441):
        s = pcm[i:i + 441]
        r = math.sqrt(sum(x * x for x in s) / 441.0) / 32768.0
        out.append(20 * math.log10(r) if r > 1e-9 else -150.0)
    return out


def quietest(levels, t0, t1):
    """The middle of the quietest 30 ms between t0 and t1 (seconds)."""
    lo, hi = max(1, int(t0 * 100)), min(len(levels) - 2, int(t1 * 100))
    best = min(range(lo, max(lo + 1, hi)), key=lambda i: levels[i - 1] + levels[i] + levels[i + 1])
    return (best + 0.5) / 100.0


def edges():
    """Trims the quiet ends of a stream."""
    edge = "silenceremove=start_periods=1:start_threshold=%s:start_silence=%.2f:detection=rms"
    return ",".join([edge % (THRESHOLD, LEAD), "areverse", edge % (THRESHOLD, TAIL), "areverse"])


def graph(srcs, start=None, end=None, drop=None):
    """The filter graph for one line, ending in [line]: the takes cut to [start, end], a passage dropped, their quiet
    ends trimmed, joined with a breath, all at 44.1 kHz mono."""
    fmt = "aresample=44100,aformat=sample_fmts=fltp:channel_layouts=mono"
    parts, labels = [], []
    for k in range(len(srcs)):
        cut = ""
        if start is not None:
            cut = "atrim=start=%.3f%s,asetpts=PTS-STARTPTS," % (start, ":end=%.3f" % end if end is not None else "")
        if drop and k == 0:
            # the take before and after the passage, joined by a 30 ms crossfade in the quiet either side of it
            d0, d1 = drop
            parts.append("[%d:a]%s,asplit=2[k%da][k%db]" % (k, fmt, k, k))
            parts.append("[k%da]atrim=end=%.3f,asetpts=PTS-STARTPTS[k%dc]" % (k, d0, k))
            parts.append("[k%db]atrim=start=%.3f,asetpts=PTS-STARTPTS[k%dd]" % (k, d1, k))
            parts.append("[k%dc][k%dd]acrossfade=d=0.03:c1=tri:c2=tri,%s%s[t%d]" % (k, k, cut, edges(), k))
        else:
            parts.append("[%d:a]%s,%s%s[t%d]" % (k, fmt, cut, edges(), k))
        labels.append("[t%d]" % k)
    if len(srcs) == 1:
        parts.append("[t0]anull[line]")
    else:
        joined = []
        for k, lab in enumerate(labels):
            if k:
                parts.append("aevalsrc=0:c=mono:s=44100:d=%.2f,aformat=sample_fmts=fltp:channel_layouts=mono[g%d]" % (BREATH, k))
                joined.append("[g%d]" % k)
            joined.append(lab)
        parts.append("%sconcat=n=%d:v=0:a=1[line]" % ("".join(joined), len(joined)))
    return ";".join(parts)


def convert(srcs, out, start=None, end=None, drop=None):
    """One line: cut, join, trim the quiet ends, normalise, write 44.1 kHz mono 16-bit WAV."""
    inputs = []
    for s in srcs: inputs += ["-i", s]
    g = graph(srcs, start, end, drop)
    probe = ffmpeg(*inputs, "-filter_complex", g + ";[line]loudnorm=I=-16:TP=-1.5:LRA=11:print_format=json[m]",
                   "-map", "[m]", "-f", "null", "-")
    m = json.loads(probe.stderr[probe.stderr.rindex("{"):probe.stderr.rindex("}") + 1])
    # one gain for the whole line; where a plosive would then pass the peak ceiling, a fast limiter catches just it
    gain = -16.0 - float(m["input_i"])
    level = "volume=%.2fdB" % gain
    if float(m["input_tp"]) + gain > -1.5:
        level += ",alimiter=limit=%.4f:attack=1:release=50:level=0" % (10 ** (-2.0 / 20))
    ffmpeg("-y", *inputs, "-filter_complex", g + ";[line]%s,aresample=44100[o]" % level, "-map", "[o]",
           "-ac", "1", "-ar", "44100", "-c:a", "pcm_s16le", "-map_metadata", "-1", out)


def ensure_meta(wav):
    meta = wav + ".meta"
    if os.path.exists(meta):
        return False
    with open(META_TEMPLATE) as f:
        t = f.read()
    with open(meta, "w") as f:
        f.write(re.sub(r"guid: [0-9a-f]{32}", "guid: " + uuid.uuid4().hex, t, count=1))
    return True


# ------------------------------------------------------------------ recognition
def transcriber():
    exe = os.path.join(tempfile.gettempdir(), "pinnacle_transcribe_words")
    src = os.path.join(TOOLS, "transcribe_words.swift")
    if not os.path.exists(exe) or os.path.getmtime(exe) < os.path.getmtime(src):
        subprocess.run(["swiftc", "-parse-as-library", "-o", exe, src], check=True)
    return exe


def transcribe(paths):
    with tempfile.TemporaryDirectory() as d:
        out = os.path.join(d, "words.json")
        subprocess.run([transcriber(), LOCALE, out] + list(paths), check=True)
        with open(out) as f:
            return {r["file"]: r for r in json.load(f)}


# ------------------------------------------------------------------ steps
def build(text, only=None):
    rows = [(srcs, ids, drop) for srcs, ids, drop in entries() if not only or any(i in only for i in ids)]
    special = [srcs[0] for srcs, ids, drop in rows if len(ids) > 1 or drop]
    heard = transcribe([os.path.join(ROOT, s) for s in special]) if special else {}
    made = []
    for srcs, ids, drop in rows:
        paths = [os.path.join(ROOT, s) for s in srcs]
        if drop:
            # out from the quiet before its first word to the quiet after its last
            words = heard[os.path.basename(srcs[0])]["words"]
            i = find_words(words, drop[0])
            j = find_words(words, drop[1]) + len(drop[1].split()) - 1
            levels = energy(paths[0])
            d0 = quietest(levels, words[i - 1]["e"] - 0.1, words[i]["s"] + 0.15)
            d1 = quietest(levels, words[j]["e"] - 0.1, words[j + 1]["s"] + 0.15 if j + 1 < len(words) else words[j]["e"] + 0.5)
            convert(paths, os.path.join(VO, ids[0] + ".wav"), drop=(d0, d1))
            print("%s: dropped %.2f-%.2f s (%s ... %s)" % (ids[0], d0, d1, drop[0], drop[1]))
        elif len(ids) == 1:
            convert(paths, os.path.join(VO, ids[0] + ".wav"))
        else:
            # one take, several lines: cut in the quietest moment between the last word of one and the first of the next
            full = " ".join(text[i] for i in ids)
            times, _, _, _ = align(full, heard[os.path.basename(srcs[0])]["words"])
            levels = energy(paths[0])
            cuts, n = [], 0
            for lid in ids[:-1]:
                n += len(tokens(text[lid]))
                last, first = times[n - 1], times[n]
                cuts.append(quietest(levels, (last[0] + last[1]) / 2, (first[0] + first[1]) / 2))
            bounds = [0.0] + cuts + [None]
            for k, lid in enumerate(ids):
                convert(paths, os.path.join(VO, lid + ".wav"), bounds[k], bounds[k + 1])
            print("split %s at %s s" % (srcs[0], ", ".join("%.2f" % c for c in cuts)))
        for lid in ids:
            if ensure_meta(os.path.join(VO, lid + ".wav")):
                made.append(lid)
    print("wrote %d lines from %d recordings%s" % (sum(len(r[1]) for r in rows), sum(len(r[0]) for r in rows),
                                                    "; new .meta for " + ", ".join(made) if made else ""))


def check(text, speaker, tts):
    ids, source = [], {}
    for srcs, group, _ in entries():
        for lid in group:
            ids.append(lid)
            source[lid] = " + ".join(os.path.basename(s) for s in srcs)
    heard = transcribe([os.path.join(VO, lid + ".wav") for lid in ids])
    rows, timing, worst = {}, [], []
    for lid in ids:
        h = heard[lid + ".wav"]
        words = h.get("words", [])
        times, ops, a, b = align(text[lid], words)
        same = sum(1 for t in times if t[2])
        diffs = []
        for op, i1, i2, j1, j2 in ops:
            if op != "equal":
                diffs.append("%s → %s" % (" ".join(w for w, _ in tokens(text[lid])[i1:i2]) or "(nothing)",
                                          " ".join(w["w"] for w in words[j1:j2]) or "(nothing)"))
        length = duration(os.path.join(VO, lid + ".wav"))
        rows[lid] = (lid, source[lid], length, text[lid], h.get("text", h.get("error", "")), same, len(times), diffs)
        if same < len(times):
            worst.append((lid, diffs))
        timing.append((lid, text[lid], [(end, t[1]) for (_, end), t in zip(tokens(text[lid]), times)]))

    # the subtitles' word timing
    body = []
    for lid, line, marks in timing:
        body.append('        {"%s", new Line("%s",\n            new[] { %s },\n            new[] { %s })},'
                    % (lid, cs(line), ", ".join(str(c) for c, _ in marks), ", ".join("%.2ff" % t for _, t in marks)))
    with open(TIMING_CS, "w") as f:
        f.write("""// GENERATED by Tools/vo_recordings.py from the recorded lines - re-run it after changing a recording or one of
// these subtitles.
using System.Collections.Generic;

/// <summary>
/// The recorded lines: where each word of the subtitle ends in its recording (character offset in the subtitle,
/// seconds into the clip), measured by on-device speech recognition. StoryCompass changes subtitle phrase when the
/// voice finishes it; a line whose subtitle no longer matches the text stored here falls back to even timing.
/// </summary>
public static class VoTiming
{
    public sealed class Line
    {
        public readonly string text; public readonly int[] chars; public readonly float[] times;
        public Line(string text, int[] chars, float[] times) { this.text = text; this.chars = chars; this.times = times; }
    }

    static readonly Dictionary<string, Line> lines = new Dictionary<string, Line>
    {
%s
    };

    /// <summary>The word timing of a recorded clip (by clip name), or null.</summary>
    public static Line Get(string clip) { Line l; return clip != null && lines.TryGetValue(clip, out l) ? l : null; }
}
""" % "\n".join(body))

    # the report, one section per character
    recorded = recorded_ids()
    total = len(rows)
    exact = sum(1 for r in rows.values() if r[5] == r[6])
    out = ["# The cast's recorded lines, checked",
           "",
           "Generated by `Tools/vo_recordings.py`. Each line below is a voice actor's recording (`arjun voice overs 2/`,",
           "`Voice Overs/`) as the game plays it from `Assets/Resources/VO/`, transcribed on this Mac by macOS's speech",
           "recogniser and compared word by word with the subtitle the game shows. The recogniser often mishears names",
           "(Chandnipur, Mehra, Kapoor, Reddy, Deshpande, Basti Nagar, the letter V) and Hindi words (chai, arre bhai, -ji): a",
           "difference in one of those alone is the recogniser, not the recording.",
           "",
           "%d lines from %d recordings; %d match their subtitle word for word, %d differ only as listed." % (
               total, len({p for s, _, _ in entries() for p in s}), exact, total - exact),
           ""]
    todo_all = []
    for name, who in CHARACTERS:
        mine = [lid for lid in ids if speaker.get(lid) in who]
        todo = [(lid, line) for lid, w, line in tts if w in who and lid not in recorded]
        todo_all += [(name, lid, line) for lid, line in todo]
        out += ["## %s" % name, "",
                "%d recorded, %d still text-to-speech." % (len(mine), len(todo)), "",
                "| Line | Recording | Length | Subtitle | Heard | Words |",
                "|---|---|---|---|---|---|"]
        for lid in mine:
            _, src, length, sub, said, same, n, diffs = rows[lid]
            out.append("| `%s` | %s | %.2f s | %s | %s | %d/%d%s |" % (
                lid, src.replace("|", "/"), length, sub.replace("|", "/"), said.replace("|", "/"), same, n,
                ("<br>" + "<br>".join(diffs)) if diffs else ""))
        out.append("")
    out += ["## Lines of these characters with no recording yet (still text-to-speech)", "",
            "%d lines. Record them and add them to `MAP` in the tool (the file names are free) to finish the voices." % len(todo_all), ""]
    last = None
    for name, lid, line in todo_all:
        if name != last:
            out += ["", "**%s**" % name, ""]; last = name
        out.append("- `%s`: %s" % (lid, line))
    others = sorted({SPEAKER_NAMES.get(w, " ".join(x[:1] + x[1:].lower() if x[:1] != "(" else x.lower() for x in w.split()))
                     for lid, w, _ in tts if lid not in recorded and not any(w in who for _, who in CHARACTERS)})
    out += ["", "Every line of the other speakers (%s) is still text-to-speech." % ", ".join(others)]
    with open(REPORT, "w") as f:
        f.write("\n".join(out) + "\n")

    print("checked %d lines: %d word for word, %d with differences:" % (total, total - len(worst), len(worst)))
    for lid, diffs in worst:
        print("  %-32s %s" % (lid, "; ".join(diffs)))
    print("wrote", os.path.relpath(TIMING_CS, ROOT), "and", os.path.relpath(REPORT, ROOT))


if __name__ == "__main__":
    text, speaker, tts = subtitles()
    missing = [lid for _, ids, _ in entries() for lid in ids if lid not in text]
    if missing:
        sys.exit("no subtitle for: " + ", ".join(missing))
    check_act12_sources()
    only = {a for a in sys.argv[1:] if not a.startswith("--")}
    if "--check" not in sys.argv:
        build(text, only)
    check(text, speaker, tts)
