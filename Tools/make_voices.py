#!/usr/bin/env python3
"""Regenerates every voice line for Shadows of Chandni using macOS `say` + ffmpeg.
Run:  python3 Tools/make_voices.py   (from the project root)
Edit the LINES table to change dialogue, then update the matching subtitle text in
Assets/Scripts/PriyaRoomStory.cs. Swap any .wav in Assets/Resources/VO for a real
recording with the same file name and the game will use it automatically. Arjun's recorded lines
(Tools/arjun_recordings.py) are skipped here, even with --all."""
import os, subprocess, tempfile

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Resources", "VO")
ARJUN = ("Rishi", 158)   # tired, thoughtful detective
KAMALA = ("Tara", 150)   # worried mother
BHASKAR = ("Aman", 172)  # talkative auto driver
VIKRAM = ("Aman", 150, 0.86)   # older, deeper (pitch-shifted)
THUG = ("Rishi", 140, 0.80)    # menacing, deeper
VEND1 = ("Aman", 178, 0.96)    # market hawkers
VEND2 = ("Rishi", 172, 1.06)
SHOPPER = ("Tara", 166)

LINES = {
 "arjun_intro1": (ARJUN, "In Chandnipur, everyone has a secret. The city is old enough to keep them."),
 "arjun_intro2": (ARJUN, "But sometimes a secret gets too big for the walls that hold it, and it starts to leak through the cracks."),
 "arjun_intro3": (ARJUN, "That's when people like me get a phone call."),
 "kamala_welcome1": (KAMALA, "Mr. Mehra? Thank you for coming. The police say she left on her own. But a mother knows. Something is wrong."),
 "kamala_welcome2": (KAMALA, "I haven't touched anything. I thought someone should look at it properly. Her room is the door at the end of the hallway. Here is the key."),
 "arjun_clue_1_Laptop": (ARJUN, "Someone cleaned this. Thoroughly. Priya is careful, but this looks rushed. She was in a hurry, or someone else did this after she left."),
 "arjun_clue_2_StickyNote": (ARJUN, "She hid this deliberately. This number is important enough to keep off the laptop."),
 "arjun_clue_3_NewspaperClippings": (ARJUN, "Priya was fighting this project. Hard. This wasn't casual activism. This was an obsession."),
 "arjun_clue_4_HalfPackedSuitcase": (ARJUN, "She was planning to run. But she didn't make it to the station. Something stopped her. Or someone."),
 "arjun_clue_5_TwoChaiCups": (ARJUN, "She had a visitor. Someone she was comfortable enough to share chai with. This wasn't a stranger."),
 "arjun_clue_6_TornPhotograph": (ARJUN, "She destroyed the other person's face on purpose. She doesn't want anyone to know who this is. Or she's angry at them."),
 "arjun_clue_7_BurnerPhone": (ARJUN, "A secret phone. V could be anyone. But Safe, that sounds like a location. Or a person she trusts."),
 "arjun_clue_8_Diary": (ARJUN, "She found something in the land records. Something big enough to tear pages out of her own diary to protect."),
 "arjun_clue_9_BrokenWindowLatch": (ARJUN, "Someone broke in. Recently. The rust on the broken metal is fresh. This wasn't Priya. She would have used the door."),
 "kamala_ask": (KAMALA, "Have you found anything?"),
 "q_k_a": (ARJUN, "Did she mention anyone threatening her?"),
 "q_k_b": (ARJUN, "Was she working on anything dangerous?"),
 "q_k_c": (ARJUN, "Did the police search this place?"),
 "kamala_a": (KAMALA, "Not directly. But two weeks ago, she stopped coming home for dinner. I could hear her on the phone, arguing with someone. She used words like forgery, and original documents. I didn't understand, but I understood her voice. She was scared."),
 "kamala_b": (KAMALA, "Priya's work is always dangerous, Mr. Mehra. She fights for people who have no one else. She told me, Maa, if I can prove the land belongs to them, no one can touch them. She was so hopeful. And then, suddenly, she wasn't."),
 "kamala_c": (KAMALA, "An inspector came. A woman. She looked around for ten minutes, asked me a few questions, and left. She said Priya probably went to visit a friend. But Priya's phone is here. Her wallet is here. What kind of person leaves without their phone?"),
 "kamala_more": (KAMALA, "Is there anything else you want to ask me?"),
 "kamala_bye": (KAMALA, "Please, Mr. Mehra. Find my daughter."),
 "arjun_note": (ARJUN, "Stop looking. She doesn't want to be found. A friend."),
 "arjun_handwriting": (ARJUN, "The handwriting matches the sticky note from under the keyboard. Whoever left this knows Priya. Someone who has been inside this apartment."),
 "arjun_closing": (ARJUN, "Someone is watching me. And they know Priya. The question is, are they protecting her, or protecting themselves?"),
}


LINES.update({
 # --- Act II
 "arjun_act2_intro": (ARJUN, "Night falls on Chandnipur, and the city changes its face. Neon, smoke, and a thousand strangers. Every shadow could be a threat now."),
 "b_greet": (BHASKAR, "Where to, sir? Market? Good, good. Traffic is less now. The gods are kind."),
 "q_b_a": (ARJUN, "Do you know a woman named Priya Kapoor? Lives in the old quarter?"),
 "b_a": (BHASKAR, "Priya madam? Of course! She takes my auto sometimes. Going to the court, going to the slum. Good woman. Always pays full fare, never bargains. Why?"),
 "q_b_a2": (ARJUN, "She's missing."),
 "b_a2": (BHASKAR, "Missing? Arre bhai... actually, I drove her two nights ago. Late, around ten. She flagged me down near the temple. She was on the phone, very tense. She kept saying, don't come, I'll handle it. Then she told me to take her to the old mill, the abandoned textile mill near the river. But halfway there, she got another call, and her face went white. She said, take me back, right now. She gave me five hundred rupees for a fifty rupee ride, and said, forget this ride. You never saw me tonight."),
 "q_b_b": (ARJUN, "What did the person on the phone sound like?"),
 "b_b": (BHASKAR, "I couldn't hear clearly, but it sounded like a man. Older. Speaking Hindi with an English accent, you know, the rich people type. Priya madam was saying, I have the documents, you can't stop me. Then later, I know what you did to the records. I don't know what it means, but it sounded serious."),
 "q_b_c": (ARJUN, "Did anyone follow you?"),
 "b_c": (BHASKAR, "Now that you mention it... there was a black car. An SUV. It was behind us for a while. I thought it was just traffic, but it turned when I turned. When Priya madam told me to go back, the SUV was gone. Maybe it was nothing. Maybe it wasn't."),
 "b_bye": (BHASKAR, "Here we are, sir, the edge of the market. If you need anything, call me. Priya madam is a good person. Find her, bhai."),
 "v_cold": (VIKRAM, "Chai? Cutting or full? No, I don't know any Priya Kapoor. You have the wrong stall, brother."),
 "q_v_1a": (ARJUN, "This is your number, Vikram. Priya hid it in her apartment. She trusted you."),
 "q_v_1b": (ARJUN, "Her mother is terrified, Vikram. She thinks something terrible has happened to her daughter. If you know anything, please."),
 "q_v_1c": (ARJUN, "I know you're lying. I can involve the police if I have to."),
 "v_t1a": (VIKRAM, "Where did you get that? Okay. Okay. Sit down. But keep your voice low."),
 "v_t1b": (VIKRAM, "Kamala-ji. She's a good woman. Alright. Sit. But if anyone asks, you're just drinking chai."),
 "v_t1c": (VIKRAM, "Then involve the police. I have nothing to say to you."),
 "v_partial": (VIKRAM, "Alright, alright. Priya came here once. She asked about the land records. That is all I know. Now please, go. I have customers."),
 "v_t2_intro": (VIKRAM, "Priya is like a daughter to me. I've known her since she was this tall. She came to me three weeks ago. She was investigating the Renewal Project, the one that's going to destroy Basti Nagar. My home, brother. I grew up there. My father built our house with his own hands. And now Councilor Deshpande wants to bulldoze it for a shopping mall."),
 "q_v_2a": (ARJUN, "What did she find?"),
 "q_v_2b": (ARJUN, "Who is she afraid of?"),
 "q_v_2c": (ARJUN, "Where is she now?"),
 "v_t2a": (VIKRAM, "She found that the land records were forged. The government claims the Basti Nagar land was acquired in 1965 under eminent domain. But Priya found the original deed from 1942. It shows the land was granted to the residents by the Maharaja himself. It was never acquired. The entire Renewal Project is illegal."),
 "v_t2b": (VIKRAM, "Councilor Meghna Deshpande. She's the one behind the project. But it's not just her. There's a builder, Raj Malhotra, and people inside the police. Priya said the corruption goes all the way up. She was scared, but she wouldn't stop. That girl has a spine of steel."),
 "v_t2c": (VIKRAM, "I don't know exactly. But the last time I spoke to her, she mentioned the Kali Temple. She said, Kali Ma will protect the truth. I thought she was being poetic, but now I think she meant it literally."),
 "v_t3a": (VIKRAM, "Wait. There's something else. Priya gave me this two days before she disappeared. She said, if anything happens to me, give this to someone who can use it. I didn't know what that meant until now. Take it. It has all her research. Copies of the forged documents, comparison with the originals, photographs, everything."),
 "v_t3b": (VIKRAM, "Be careful, Mr. Mehra. The people behind this, they don't play games. Two days ago, two men came to my stall. They asked if a woman had been asking questions about the land records. I said no. They broke my kettle and told me to keep saying no. These are not people you argue with."),
 "v_bye": (VIKRAM, "Go carefully, brother. And drink some chai next time."),
 "arjun_reddy": (ARJUN, "A cop. Plainclothes. She's been following me, or following Vikram. Whose side is she on?"),
 "arjun_usb": (ARJUN, "Priya's research. Everything she found, in one small drive. This could change everything."),
 "v_phone": (VIKRAM, "They know you're here. Deshpande's people. They were watching the stall. You need to leave. Now."),
 "thug_overhear": (THUG, "The councilor wants the file by tomorrow morning. Find the man who is asking questions."),
 "arjun_photo": (ARJUN, "One of them is photographing me. They know my face now."),
 "arjun_plate": (ARJUN, "M H, one two, A B, four four seven one. Got it."),
 "arjun_act2_end": (ARJUN, "The streets of Chandnipur know everything. The question is whether they'll tell you the truth before it's too late."),
})


LINES.update({
 "cry_1": (VEND1, "Chai garam, chai garam! Adrak wali chai!"),
 "cry_2": (VEND2, "Garam samosa, garam jalebi! Aa jao bhaiya, aa jao!"),
 "cry_3": (VEND1, "Taaza sabzi, taaza sabzi! Tamatar bees rupaye kilo!"),
 "cry_4": (VEND2, "Phool le lo, phool! Gende ke phool, gulab ke phool!"),
 "cry_5": (VEND1, "Garam bhutta, garam bhutta! Nimbu masala wala!"),
 "cry_6": (VEND2, "Pani puri, pani puri! Aa jao, aa jao, chatpati pani puri!"),
 "cry_7": (VEND1, "Gubbare, gubbare! Rang birange gubbare, le lo bachcho!"),
 "cry_8": (VEND2, "Nariyal pani, thanda nariyal pani! Sirf bees rupaye!"),
 "cry_9": (VEND1, "Saste kapde, saste kapde! Sirf sau rupaye, sirf sau rupaye!"),
 "cry_10": (SHOPPER, "Didi, chudiyan le lo, chudiyan! Naye design aaye hain!"),
 "cry_11": (SHOPPER, "Bhaiya, thoda kam karo na, bahut mehenga hai!"),
 "cry_12": (VEND2, "Arre madam, taaza hai taaza, ek dum fresh maal!"),
})

os.makedirs(OUT, exist_ok=True)
import sys
only_new = "--all" not in sys.argv   # skip files that already exist unless --all is passed
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from arjun_recordings import recorded_ids
recorded = recorded_ids()               # the voice actor's lines: never replaced by `say`
for name, (cfg, text) in LINES.items():
    out = os.path.join(OUT, name + ".wav")
    if name in recorded or (only_new and os.path.exists(out)):
        continue
    voice, rate = cfg[0], cfg[1]
    pitch = cfg[2] if len(cfg) > 2 else 1.0
    with tempfile.TemporaryDirectory() as d:
        aiff = os.path.join(d, "x.aiff")
        subprocess.run(["say", "-v", voice, "-r", str(rate), "-o", aiff, text], check=True)
        chain = "aresample=44100"
        if pitch != 1.0:
            chain += ",asetrate=%d,aresample=44100,atempo=%.4f" % (int(44100 * pitch), 1.0 / pitch)
        chain += ",loudnorm=I=-16:TP=-1.5,aresample=44100"
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", aiff, "-ac", "1", "-ar", "44100", "-af", chain, out], check=True)
    print("ok", name)
