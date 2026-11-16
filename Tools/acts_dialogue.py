#!/usr/bin/env python3
"""Acts III-V dialogue: ONE source for both the voice recordings and the subtitles.

Run from the project root:   python3 Tools/acts_dialogue.py          (only missing lines)
                             python3 Tools/acts_dialogue.py --all    (re-record everything)
Writes  Assets/Resources/VO/<id>.wav   (macOS `say` + ffmpeg)
and     Assets/Scripts/DialogueDB.cs   (id -> speaker + subtitle text, used by the game)
"""
import os, subprocess, sys, tempfile

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
OUT = os.path.join(ROOT, "Assets", "Resources", "VO")
CS = os.path.join(ROOT, "Assets", "Scripts", "DialogueDB.cs")

# (voice, rate, pitch factor, extra ffmpeg filter)
ARJUN = ("Rishi", 158, 1.0, "")
SHARMA = ("Aman", 148, 0.88, "")
PANDIT = ("Aman", 136, 0.84, "")
CALLER = ("Rishi", 145, 0.72, "highpass=f=350,lowpass=f=3200,acrusher=bits=10:mode=log:aa=1:mix=0.35")
RECORDING = ("Aman", 150, 0.8, "highpass=f=300,lowpass=f=2800,acrusher=bits=8:mode=log:aa=1:mix=0.5")
RAJ = ("Aman", 150, 0.93, "")
ROHAN = ("Aman", 170, 1.07, "")
KAMALA = ("Tara", 150, 1.0, "")
REDDY = ("Tara", 160, 0.9, "")
PRIYA = ("Tara", 156, 1.06, "")
RESIDENT = ("Tara", 150, 0.95, "")
VIKRAM = ("Aman", 150, 0.86, "")
THUG = ("Rishi", 140, 0.80, "")
JUDGE = ("Aman", 138, 0.82, "aecho=0.6:0.5:40:0.15")
LAWYER = ("Rishi", 165, 0.95, "")
CURATOR = ("Daniel", 138, 0.86, "aecho=0.8:0.8:70|140:0.3|0.18")

# id: (speaker label shown in subtitles, voice, text)
LINES = [
 # ---------------- ACT III: THE FILE
 ("a3_vo_intro", "ARJUN (V.O.)", ARJUN, "The Old Chandnipur Library. Two hundred years of the city's memory, locked up for the night. If Priya found the truth anywhere, she found it here."),
 ("a3_sharma_1", "SHARMA-JI", SHARMA, "Library is closed, sahib. Nine o'clock means closed. Who are you?"),
 ("a3_arjun_letter", "ARJUN", ARJUN, "Arjun Mehra. Kamala Kapoor sent me. Here is her letter."),
 ("a3_sharma_2", "SHARMA-JI", SHARMA, "Kamala-ji's daughter... Priya madam used to sit here till midnight with her files. Go in, sahib. The computer at the front desk still works. And the record room is downstairs. She spent many nights down there."),
 ("a3_arjun_usb", "ARJUN", ARJUN, "Vikram's USB drive. Let's see what Priya was hiding."),
 ("a3_arjun_backup", "ARJUN", ARJUN, "Priya kept a backup of her research on this machine. She would have locked it."),
 ("a3_arjun_pass", "ARJUN", ARJUN, "Encrypted. Her diary said: where it all began for us, the year Nani told us stories. Kamala-ji sent me a photo of the children at their grandmother's house."),
 ("a3_audio", "RECORDING", RECORDING, "the hearing is Thursday... make sure the originals are destroyed... Reddy is getting too close..."),
 ("a3_arjun_unlock", "ARJUN", ARJUN, "This is it. The smoking gun. The Renewal Project is built on a lie. And someone named Reddy is involved. The same Reddy who has been following me?"),
 ("a3_arjun_basement", "ARJUN", ARJUN, "Block C, Shelf 14, the years 1940 to 1945. That is where Priya said the original deed would be."),
 ("a3_arjun_cage", "ARJUN", ARJUN, "Locked. A wire cage and a padlock. The key has to be somewhere in this building."),
 ("a3_arjun_calendar", "ARJUN", ARJUN, "Three dates circled in red. Independence Day, Republic Day, Gandhi Jayanti. Someone wanted to remember 1947. Backwards, maybe."),
 ("a3_arjun_desk", "ARJUN", ARJUN, "The librarian's desk. A four digit lock on the drawer."),
 ("a3_arjun_key", "ARJUN", ARJUN, "A small brass key. The cage key, I would bet."),
 ("a3_arjun_empty", "ARJUN", ARJUN, "The 1942 folder. It's empty. Just a sign-out sheet."),
 ("a3_arjun_reddy", "ARJUN", ARJUN, "Last accessed two weeks ago, by S. Reddy. Inspector Reddy took the original deed. Is she destroying evidence for Deshpande? Or building a case of her own?"),
 ("a3_vo_temple", "ARJUN (V.O.)", ARJUN, "The Kali Temple. Priya told Vikram that Kali Ma would protect the truth. Maybe she meant it more literally than he thought."),
 ("a3_pandit_1", "PANDIT HARISHANKAR", PANDIT, "You are looking for Priya. I can see it in your eyes. You have the same look she had when she came to me. Desperate, and determined."),
 ("a3_arjun_q", "ARJUN", ARJUN, "She left something with you?"),
 ("a3_pandit_2", "PANDIT HARISHANKAR", PANDIT, "A sealed envelope. She said someone would come for it, but that I should only give it to someone who understands what matters. So tell me, young man. What is more important? The truth, or the people the truth protects?"),
 ("a3_ans_a", "ARJUN", ARJUN, "The truth. Always. Without truth, there is no justice."),
 ("a3_ans_b", "ARJUN", ARJUN, "The people. Truth is meaningless if it doesn't serve those who suffer."),
 ("a3_ans_c", "ARJUN", ARJUN, "They're the same thing. The truth protects the people, and the people deserve the truth."),
 ("a3_pandit_a", "PANDIT HARISHANKAR", PANDIT, "A journalist's answer. Very well. Take it."),
 ("a3_pandit_b", "PANDIT HARISHANKAR", PANDIT, "A compassionate answer. Priya would agree. She told me to tell you: the hearing is tomorrow at ten in the morning, at the Municipal Court. She wants the documents there."),
 ("a3_pandit_c", "PANDIT HARISHANKAR", PANDIT, "Now that is the answer she hoped for. She is safe. She is at the old school building near the river, the one the NGO uses as a shelter. She is waiting for tomorrow."),
 ("a3_letter", "PRIYA (LETTER)", PRIYA, "If you're reading this, I hope you're someone I can trust. The councilor, the builder, and someone in the police are all connected. The hearing is tomorrow morning at ten, at the Municipal Court. Five hundred families are counting on this. I'm trusting you with their futures. Please don't let me down. Priya."),
 ("a3_caller", "UNKNOWN CALLER", CALLER, "Mister Mehra. You've been busy tonight. I'm impressed. Most people would have given up after the apartment. I have information about Priya that you haven't found yet. Information that could save her life, or end it. Cafe Irani. Thirty minutes. Come alone."),
 ("a3_vo_crossroads", "ARJUN (V.O.)", ARJUN, "Two calls, and time for one. Or I could stop running, and call the one person who keeps turning up in this story."),
 ("a3_vo_end", "ARJUN (V.O.)", ARJUN, "Every choice closes a door. In Chandnipur, closed doors have a way of locking behind you."),

 # ---------------- ACT IV A: CAFE IRANI
 ("a4a_vo", "ARJUN (V.O.)", ARJUN, "Cafe Irani. Checked tablecloths, Osmania biscuits, and at this hour, nobody who wants to be seen."),
 ("a4a_raj_1", "RAJ MALHOTRA", RAJ, "Sit down, Mister Mehra. And before you say anything, yes, I know who I am. The big bad builder who wants to demolish a slum. That's what Priya Kapoor believes. And she's not entirely wrong."),
 ("a4a_arjun_why", "ARJUN", ARJUN, "Then why did you call me?"),
 ("a4a_raj_2", "RAJ MALHOTRA", RAJ, "Because I'm in over my head. I built affordable homes for twenty years. Then Deshpande offered me the Renewal Project. I didn't know the land documents were forged, I swear it. By the time I found out, I was in too deep. If I back out, she destroys me. If I stay, five hundred families lose their homes."),
 ("a4a_q_a", "ARJUN", ARJUN, "Why should I trust you?"),
 ("a4a_raj_a", "RAJ MALHOTRA", RAJ, "You shouldn't. Not yet. But this should earn a little trust. Six months of recorded conversations between me and Deshpande. The forged documents, the bribes, the plan to destroy the originals. It's all on this phone."),
 ("a4a_q_b", "ARJUN", ARJUN, "Where is Priya?"),
 ("a4a_raj_b", "RAJ MALHOTRA", RAJ, "I've known for twenty four hours. Deshpande ordered my people to track her. I told them she'd gone to Nagpur. She's at the old NGO shelter near the river. The school building."),
 ("a4a_q_c", "ARJUN", ARJUN, "What do you want in return?"),
 ("a4a_raj_c", "RAJ MALHOTRA", RAJ, "Immunity. I'll testify against Deshpande, but I want my name kept out of the criminal case. I have a daughter Priya's age, Mister Mehra."),
 ("a4a_raj_made", "RAJ MALHOTRA", RAJ, "We've been made. The man in the corner is on the phone. Back door. Now."),
 ("a4a_arjun_kitchen", "ARJUN", ARJUN, "The kitchen. Pots everywhere. Quietly."),
 ("a4a_raj_clang", "RAJ MALHOTRA", RAJ, "Too loud! They heard that. Back, back. Quietly this time."),
 ("a4a_arjun_dead", "ARJUN", ARJUN, "Dead end. Back to the fork."),
 ("a4a_arjun_hide", "ARJUN", ARJUN, "Footsteps. Behind the dumpster, now!"),
 ("a4a_thug_search", "DESHPANDE'S MAN", THUG, "They came this way. Check the alley. The councilor wants them both."),
 ("a4a_caught", "DESHPANDE'S MAN", THUG, "There! Behind the dumpster!"),
 ("a4a_raj_dhaba", "RAJ MALHOTRA", RAJ, "Tomorrow morning. The hearing. I'll be there, and I'll testify. Make sure Priya is there too."),
 ("a4a_vo_end", "ARJUN (V.O.)", ARJUN, "A frightened builder with a phone full of recordings. In this city, that's the closest thing to an army."),

 # ---------------- ACT IV B: KAPOOR HOUSE
 ("a4b_vo", "ARJUN (V.O.)", ARJUN, "The Kapoor house. The front door was open. That's never a good sign."),
 ("a4b_kamala_1", "KAMALA KAPOOR", KAMALA, "They came an hour ago. Two men. They said they were from the Municipal Office, looking for misplaced documents. They tore the house apart. They kept asking about a file, and a deed."),
 ("a4b_arjun_find", "ARJUN", ARJUN, "Did they find anything?"),
 ("a4b_kamala_2", "KAMALA KAPOOR", KAMALA, "No. Whatever they wanted, it wasn't here."),
 ("a4b_arjun_print", "ARJUN", ARJUN, "A muddy boot print. Heavy boots. These weren't clerks."),
 ("a4b_arjun_pass", "ARJUN", ARJUN, "A visitor pass from the Municipal Office. One of them dropped it."),
 ("a4b_arjun_laptop", "ARJUN", ARJUN, "Rohan's laptop. Still logged into his Municipal Office email. An intern shouldn't be able to see these."),
 ("a4b_arjun_confront", "ARJUN", ARJUN, "Rohan. These emails. How do you have them?"),
 ("a4b_rohan_1", "ROHAN KAPOOR", ROHAN, "It's my fault. All of it. I found the forged documents. I was filing papers at the office and I saw the acquisition order. The dates didn't match, the signatures were different. The whole thing was fake."),
 ("a4b_arjun_told", "ARJUN", ARJUN, "And you told Priya."),
 ("a4b_rohan_2", "ROHAN KAPOOR", ROHAN, "She's my sister. I thought she'd file a complaint. I didn't think she'd take the original deed from the library herself. And now they're hunting her, because of me."),
 ("a4b_q_a", "ARJUN", ARJUN, "Can you take me to her?"),
 ("a4b_rohan_a", "ROHAN KAPOOR", ROHAN, "Yes. I've been in contact with her. She's at the old NGO shelter near the river. She made me promise not to tell anyone, but she needs help now. I'll take you."),
 ("a4b_q_b", "ARJUN", ARJUN, "We need to go to the police."),
 ("a4b_rohan_b", "ROHAN KAPOOR", ROHAN, "The police are part of the problem, Arjun-ji. The only cop I'd trust is Inspector Reddy, and I don't even know whose side she's on."),
 ("a4b_q_c", "ARJUN", ARJUN, "We need to protect these documents first."),
 ("a4b_rohan_c", "ROHAN KAPOOR", ROHAN, "I have copies of everything on a cloud drive. The emails, the forged orders, Priya's analysis. Even if they burn the papers, the digital evidence is safe."),
 ("a4b_kamala_3", "KAMALA KAPOOR", KAMALA, "Bring her home, Arjun. Please. Both of you, come back safe."),
 ("a4b_vo_end", "ARJUN (V.O.)", ARJUN, "A guilty brother and a cloud drive full of emails. Sometimes the truth hides in the most ordinary places."),

 # ---------------- ACT IV C: INSPECTOR REDDY
 ("a4c_vo", "ARJUN (V.O.)", ARJUN, "Chandnipur Police Station. Half the city's secrets end up here. The other half start here."),
 ("a4c_reddy_1", "INSPECTOR REDDY", REDDY, "Mister Mehra. I was wondering when you'd figure out I was following you. You're better than I expected."),
 ("a4c_arjun_id", "ARJUN", ARJUN, "You've been one step behind me all night. Not very undercover."),
 ("a4c_reddy_2", "INSPECTOR REDDY", REDDY, "Maybe I wanted you to notice. Sit down. We need to talk."),
 ("a4c_reddy_r1", "INSPECTOR REDDY", REDDY, "I don't know any Priya Kapoor. I'm a crime branch officer. Organised crime, not missing persons."),
 ("a4c_reddy_r1ok", "INSPECTOR REDDY", REDDY, "Alright. You've done your homework. Yes, I know the Kapoor case. It's connected to an ongoing investigation. I can't share details."),
 ("a4c_reddy_wrong", "INSPECTOR REDDY", REDDY, "That proves nothing, Mister Mehra. Try harder, or go home."),
 ("a4c_reddy_r2", "INSPECTOR REDDY", REDDY, "My investigation is classified. You're seeing a fraction of the picture. Stay out of it."),
 ("a4c_reddy_r2ok", "INSPECTOR REDDY", REDDY, "Where did you get that? Never mind. I can see you're not going to stop. Fine. Let me tell you what you don't know."),
 ("a4c_reddy_r3", "INSPECTOR REDDY", REDDY, "You think I took the original deed to destroy it for Deshpande. That's what it looks like, isn't it?"),
 ("a4c_q_a", "ARJUN", ARJUN, "I have something you don't. A letter, in Priya's own hand."),
 ("a4c_reddy_a", "INSPECTOR REDDY", REDDY, "She's braver than all of us. Alright, Mister Mehra. Cards on the table."),
 ("a4c_q_b", "ARJUN", ARJUN, "Are you protecting Deshpande, or investigating her?"),
 ("a4c_reddy_b", "INSPECTOR REDDY", REDDY, "Investigating her. For six months. I'm undercover. I took the original deed as evidence. It's locked in a secure evidence room right now."),
 ("a4c_q_c", "ARJUN", ARJUN, "Priya's life is in danger. Politics can wait."),
 ("a4c_reddy_c", "INSPECTOR REDDY", REDDY, "You're right. I've been so focused on building the perfect case that I almost lost sight of the person at the centre of it. Let me help."),
 ("a4c_reddy_reveal", "INSPECTOR REDDY", REDDY, "I've been protecting Priya for two weeks. My men are watching the NGO shelter by the river. I have the deed, the financial records, the witnesses. I need one more thing. I need Priya to testify in person."),
 ("a4c_vo_end", "ARJUN (V.O.)", ARJUN, "An honest cop in Chandnipur. I'd almost stopped believing in them."),

 # ---------------- ACT V: SUNRISE
 ("a5_vo_intro", "ARJUN (V.O.)", ARJUN, "The old school by the river. Four in the morning. Somewhere upstairs, a single lamp is still burning."),
 ("a5_resident", "RESIDENT", RESIDENT, "Upstairs, bhaiya. Priya didi hasn't slept in days."),
 ("a5_priya_1", "PRIYA KAPOOR", PRIYA, "I knew someone would come. I just didn't know if they'd come to help me, or to silence me."),
 ("a5_arjun_mother", "ARJUN", ARJUN, "Your mother sent me. She's worried sick."),
 ("a5_priya_2", "PRIYA KAPOOR", PRIYA, "Maa... I called her. I told her I loved her. I thought it might be the last time I could say it."),
 ("a5_priya_3", "PRIYA KAPOOR", PRIYA, "Five hundred families, Arjun. Children, grandparents, people who've lived in Basti Nagar for generations. The courts, the police, the politicians. They're all part of the same machine. I had to do this myself."),
 ("a5_priya_4", "PRIYA KAPOOR", PRIYA, "I have the originals. The hearing is in four hours. But if I walk into that courtroom, Deshpande will have me arrested for stealing government property. So tell me, Mister Mehra. You've seen the evidence. What should I do?"),

 # ---------------- THE CURATOR'S GALLERY
 ("gal_1", "THE CURATOR", CURATOR, "Ah. There you are. I was beginning to wonder if you'd lost your way in all those narrow lanes, and narrower consciences."),
 ("gal_2", "THE CURATOR", CURATOR, "Welcome to my collection. Every object you see is a story. A life, a choice, a consequence. And now, yours. The missing girl. The forged deed. Five hundred families sleeping under tin roofs."),
 ("gal_3", "THE CURATOR", CURATOR, "You followed every thread I laid out for you. But investigation is merely the appetiser, my friend. The main course is choice."),
 ("gal_4", "THE CURATOR", CURATOR, "Three caskets. Three inscriptions. Three futures. In a play by a certain English bard, a merchant's daughter offered her suitors a similar test. Most chose wrong. I wonder. Will you?"),
 ("gal_5", "THE CURATOR", CURATOR, "Step forward. Read the inscriptions. Choose your casket. And remember: in my collection there are no wrong endings. Only consequences."),
 ("gal_bronze", "THE CURATOR", CURATOR, "Bronze. The metal of heroes and monuments. Justice, the clean courtroom kind. But read it carefully. A single throat to the wolf. Choose this, and Priya walks into the lion's den."),
 ("gal_bone", "THE CURATOR", CURATOR, "Bone. The material of endings. Choose this, and Arjun takes the evidence to court alone. Priya stays hidden. She survives. But sanctuaries built on silence have a way of collapsing."),
 ("gal_iron", "THE CURATOR", CURATOR, "Iron. The metal of revolution. Choose this, and the evidence goes to the press. The people rise. But fire does not discriminate, my friend. It burns the guilty and the innocent alike."),
 ("gal_sealed_bronze", "THE CURATOR", CURATOR, "Justice demands proof, detective, and your pockets are rather light. The bronze will not open for you."),
 ("gal_sealed_iron", "THE CURATOR", CURATOR, "A fire needs fuel. Recordings, emails, something the city can read. You have none. The iron stays cold."),
 ("gal_chosen", "THE CURATOR", CURATOR, "So be it."),

 # ---------------- ENDING 1: JUSTICE
 ("end1_vo_dawn", "ARJUN (V.O.)", ARJUN, "Dawn. Shutters rising, temple bells, chai stoves. And outside the Municipal Court, Basti Nagar had come to watch."),
 ("end1_lawyer", "DESHPANDE'S LAWYER", LAWYER, "Your Honour, the Renewal Project is a vision for a modern Chandnipur. The acquisition order of 1965 is entirely in order."),
 ("end1_priya", "PRIYA KAPOOR", PRIYA, "Your Honour, I am Priya Kapoor, advocate for the residents of Basti Nagar. The acquisition order of 1965 is a forgery. The original deed of 1942 proves the land belongs to its residents, in perpetuity. I have it here."),
 ("end1_reddy", "INSPECTOR REDDY", REDDY, "Inspector Sonal Reddy, Crime Branch. I have a warrant for the arrest of Councilor Meghna Deshpande, for fraud, forgery and criminal conspiracy."),
 ("end1_raj", "RAJ MALHOTRA", RAJ, "I am Raj Malhotra. I signed the Renewal contracts. And I have six months of recordings of the councilor planning this fraud. I will play them for the court."),
 ("end1_rohan", "ROHAN KAPOOR", ROHAN, "I'm Rohan Kapoor. I was an intern at the Municipal Office. These internal emails prove the forgery was known, and approved, at the highest level."),
 ("end1_judge", "THE JUDGE", JUDGE, "The Chandnipur Renewal Project is hereby stayed, pending a full investigation into the land records. The residents of Basti Nagar shall not be displaced."),
 ("end1_vo_end", "ARJUN (V.O.)", ARJUN, "In Chandnipur, the truth is a fragile thing. But sometimes one person with a spine of steel and a folder full of evidence can crack the whole thing open. Today was one of those days."),
 ("end1_curator", "THE CURATOR", CURATOR, "I must confess, I don't get many of these. Most of my stories end in blood, or silence. This one ends in sunlight. Bravo. A rare, happy ending for my collection."),

 # ---------------- ENDING 2: SACRIFICE
 ("end2_vo", "ARJUN (V.O.)", ARJUN, "I walked into the Municipal Court alone. A few lawyers, a bored clerk, and the councilor in the front row, looking bored too."),
 ("end2_lawyer", "DESHPANDE'S LAWYER", LAWYER, "Your Honour, these documents were allegedly stolen from a government facility by a fugitive. The chain of custody is broken. Without the person who obtained them, this evidence is inadmissible."),
 ("end2_kamala", "KAMALA KAPOOR", KAMALA, "Your Honour, I am Kamala Kapoor, Priya's mother. My daughter has been threatened. Our home was broken into. I am a teacher, and I know the difference between truth and lies."),
 ("end2_vikram", "VIKRAM SAHU", VIKRAM, "I am Vikram Sahu. I sell chai near the market. Two men broke my kettle and told me to keep quiet. I am not quiet any more."),
 ("end2_judge", "THE JUDGE", JUDGE, "This court cannot rule without proper standing. But it orders a full investigation into the land records, and stays the eviction until that investigation is complete."),
 ("end2_vo_end", "ARJUN (V.O.)", ARJUN, "Not every battle ends with a bang. Some end with a whisper. A judge's order, a mother's courage. The truth is out there now, but it's fragile. It needs protecting."),
 ("end2_curator", "THE CURATOR", CURATOR, "You saved the girl. Congratulations. Was she worth five hundred families? I'm not judging. Actually, I am. That's rather my job."),

 # ---------------- ENDING 3: REVOLUTION
 ("end3_priya_1", "PRIYA KAPOOR", PRIYA, "Once this is out, there's no taking it back. Deshpande will come after us with everything she has."),
 ("end3_arjun_1", "ARJUN", ARJUN, "She'll come after us anyway. At least this way, the whole city will be watching."),
 ("end3_priya_2", "PRIYA KAPOOR", PRIYA, "The truth is out. But truth without a courtroom is just noise, until enough people listen. I hope they're listening. Did we do the right thing?"),
 ("end3_arjun_2", "ARJUN", ARJUN, "Ask me in a year."),
 ("end3_curator", "THE CURATOR", CURATOR, "I have many stories of revolution in my collection. They all end the same way. The people cheering, and the hero weeping. The crowd goes home. The hero stays in the fire."),
]


def cs_escape(s):
    return s.replace("\\", "\\\\").replace('"', '\\"')


def write_db():
    rows = []
    for lid, speaker, _, text in LINES:
        shown = text.replace("Mister ", "Mr. ").replace("Cafe Irani", "Café Irani")
        rows.append('        {"%s", new[] { "%s", "%s" }},' % (lid, cs_escape(speaker), cs_escape(shown)))
    body = "\n".join(rows)
    src = f'''// GENERATED by Tools/acts_dialogue.py - edit the dialogue there, then re-run the script.
using System.Collections.Generic;

/// <summary>Acts III-V: subtitle text and speaker for every voiced line (the audio is Resources/VO/&lt;id&gt;.wav).</summary>
public static class DialogueDB
{{
    static readonly Dictionary<string, string[]> lines = new Dictionary<string, string[]>
    {{
{body}
    }};

    public static string Speaker(string id) {{ string[] l; return lines.TryGetValue(id, out l) ? l[0] : ""; }}
    public static string Text(string id) {{ string[] l; return lines.TryGetValue(id, out l) ? l[1] : id; }}
}}
'''
    with open(CS, "w") as f:
        f.write(src)


def record(only_new):
    os.makedirs(OUT, exist_ok=True)
    for lid, _, (voice, rate, pitch, extra), text in LINES:
        out = os.path.join(OUT, lid + ".wav")
        if only_new and os.path.exists(out):
            continue
        with tempfile.TemporaryDirectory() as d:
            aiff = os.path.join(d, "x.aiff")
            subprocess.run(["say", "-v", voice, "-r", str(rate), "-o", aiff, text], check=True)
            chain = "aresample=44100"
            if pitch != 1.0:
                chain += ",asetrate=%d,aresample=44100,atempo=%.4f" % (int(44100 * pitch), 1.0 / pitch)
            if extra:
                chain += "," + extra
            chain += ",loudnorm=I=-16:TP=-1.5,aresample=44100"
            subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", aiff, "-ac", "1", "-ar", "44100", "-af", chain, out], check=True)
        print("ok", lid)


if __name__ == "__main__":
    write_db()
    record("--all" not in sys.argv)
