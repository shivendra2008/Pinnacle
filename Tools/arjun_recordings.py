#!/usr/bin/env python3
"""Puts the voice actor's recordings of Arjun in place of his text-to-speech lines, and checks every one.

    python3 Tools/arjun_recordings.py            convert the recordings, then check them
    python3 Tools/arjun_recordings.py --check    only check what is in Assets/Resources/VO

The recordings are the MP3s in "arjun voice overs 2/" (named by act and scene). For each one this
  - trims the silence at both ends (keeping 60 ms before the first sound and 250 ms after the last),
  - splits the Act I opening, recorded in one take, at its pauses into the three lines the game plays,
  - brings it to the loudness of the rest of the VO (-16 LUFS integrated, one gain for the whole line, a fast limiter
    holding the odd plosive under -1.5 dB true peak) as 44.1 kHz mono 16-bit WAV, written over the text-to-speech file of the same line in Assets/Resources/VO
    (a line that had no voice before also gets a .meta with the other VO clips' import settings).
Then it transcribes every written file on the Mac (Tools/transcribe_words.swift: macOS's on-device recogniser, nothing
is uploaded), compares the words heard with the subtitle the game shows, and writes
  - Assets/Scripts/VoTiming.cs: where each subtitle word ends in its recording, so the subtitles change phrase when
    the voice does;
  - Docs/ARJUN_VOICE_CHECK.md: per line, the recording, its length, the subtitle and what was heard, plus Arjun's
    lines that have no recording yet.
make_voices.py and acts_dialogue.py skip every line listed here, even with --all, so `say` never overwrites them.
"""
import array, difflib, json, math, os, re, subprocess, sys, tempfile, uuid

TOOLS = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(TOOLS, ".."))
SRC = os.path.join(ROOT, "arjun voice overs 2")
VO = os.path.join(ROOT, "Assets", "Resources", "VO")
TIMING_CS = os.path.join(ROOT, "Assets", "Scripts", "VoTiming.cs")
REPORT = os.path.join(ROOT, "Docs", "ARJUN_VOICE_CHECK.md")
META_TEMPLATE = os.path.join(VO, "arjun_clue_1_Laptop.wav.meta")
LOCALE = "en-IN"

# Acts I and II keep their subtitles in PriyaRoomStory.cs and MarketStory.cs: these are the exact strings the game
# shows with each recording. Acts III-V take theirs from acts_dialogue.py.
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
}

# recording -> the line(s) it voices, in the order they are spoken
MAP = [
    ("act1 arjun1.mp3", ["arjun_intro1", "arjun_intro2", "arjun_intro3"]),
    ("act1 arjun2.mp3", ["arjun_closing"]),
    ("act1 arjunchoice1.mp3", ["q_k_a"]),
    ("act1 arjunchoice2.mp3", ["q_k_b"]),
    ("act1 arjunchoice3.mp3", ["q_k_c"]),
    ("act1 arjuncluelaptop.mp3", ["arjun_clue_1_Laptop"]),
    ("act1 arjuncluestickynote.mp3", ["arjun_clue_2_StickyNote"]),
    ("act1 arjuncluenewspaperclipping.mp3", ["arjun_clue_3_NewspaperClippings"]),
    ("act1 arjuncluesuitcase.mp3", ["arjun_clue_4_HalfPackedSuitcase"]),
    ("act1 arjuncluechaicups.mp3", ["arjun_clue_5_TwoChaiCups"]),
    ("act1 arjuncluetornphoto(trashbin).mp3", ["arjun_clue_6_TornPhotograph"]),
    ("act1 arjuncluephone.mp3", ["arjun_clue_7_BurnerPhone"]),
    ("act1 arjuncluediary.mp3", ["arjun_clue_8_Diary"]),
    ("act1 arjuncluewindow.mp3", ["arjun_clue_9_BrokenWindowLatch"]),
    ("act2 arjunbhaskarchoice1part1.mp3", ["q_b_a"]),
    ("act2 arjunbhaskarchoice1part2.mp3", ["q_b_a2"]),
    ("act2 arjunbhaskarchoice2.mp3", ["q_b_b"]),
    ("act2 arjunbhaskarchoice3.mp3", ["q_b_c"]),
    ("act2 arjunvikramchoice1.mp3", ["q_v_1a"]),
    ("act2 arjunvikramchoice2.mp3", ["q_v_1b"]),
    ("act2 arjunvikramchoice3(wrongchoice).mp3", ["q_v_1c"]),
    ("act2 warmingvikramchoice1.mp3", ["q_v_2a"]),
    ("act2 warmingvikramchoice2.mp3", ["q_v_2b"]),
    ("act2 warmingvikramchoice3.mp3", ["q_v_2c"]),
    ("act2 arjunfollowlady.mp3", ["arjun_reddy"]),
    ("act2 arjun(endvoiceover).mp3", ["arjun_act2_end"]),
    ("act3 arjunkalipart1.mp3", ["a3_arjun_q"]),
    ("act3 arjunkalichoice1.mp3", ["a3_ans_a"]),
    ("act3 arjunkalichoice2.mp3", ["a3_ans_b"]),
    ("act3 arjunkalichoice3.mp3", ["a3_ans_c"]),
    ("act3 arjunpuzzle1.mp3", ["a3_arjun_unlock"]),
    ("act3 arjunpuzzle2.mp3", ["a3_arjun_reddy"]),
    ("act3 arjunfinalvoiceover.mp3", ["a3_vo_end"]),
    ("act4 arjuncafe1.mp3", ["a4a_arjun_why"]),
    ("act4 arjuncafechoice1.mp3", ["a4a_q_a"]),
    ("act4 arjuncafechoice2.mp3", ["a4a_q_b"]),
    ("act4 arjuncafechoice3.mp3", ["a4a_q_c"]),
    ("act4 arjunkapoor1.mp3", ["a4b_arjun_find"]),
    ("act4 arjunkapoor2.mp3", ["a4b_arjun_told"]),
    ("act4 arjunkapoorchoice1.mp3", ["a4b_q_a"]),
    ("act4 arjunkapoorchoice2.mp3", ["a4b_q_b"]),
    ("act4 arjunkapoorchoice3.mp3", ["a4b_q_c"]),
    ("act4 arjunreddy1.mp3", ["a4c_arjun_id"]),
    ("act4 arjunreddychoice1.mp3", ["a4c_q_a"]),
    ("act4 arjunreddychoice2.mp3", ["a4c_q_b"]),
    ("act4 arjunreddychoice3.mp3", ["a4c_q_c"]),
    ("act5 arjun1.mp3", ["a5_arjun_mother"]),
    ("act5 arjunend1.mp3", ["a5_arjun_justice"]),
    ("act5 arjunend2.mp3", ["a5_arjun_sacrifice"]),
    ("act5 arjunend3.mp3", ["a5_arjun_revolution"]),
]

LEAD, TAIL = 0.06, 0.25          # seconds of quiet kept before the first sound and after the last
THRESHOLD = "-50dB"              # quieter than this counts as silence (the recordings' floor is about -60 dB)


def recorded_ids():
    """Every line the voice actor recorded. The text-to-speech generators leave these alone."""
    return {lid for _, ids in MAP for lid in ids}


# ------------------------------------------------------------------ text
def subtitles():
    sys.path.insert(0, TOOLS)
    import acts_dialogue
    text = dict(ACT12)
    for lid, _speaker, _voice, line in acts_dialogue.LINES:
        text.setdefault(lid, line)
    return text, acts_dialogue.LINES


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


def convert(src, out, start=None, end=None):
    """One line: cut, trim the quiet ends, normalise, write 44.1 kHz mono 16-bit WAV."""
    chain = ""
    if start is not None:
        chain = "atrim=start=%.3f%s,asetpts=PTS-STARTPTS," % (start, ":end=%.3f" % end if end is not None else "")
    edge = "silenceremove=start_periods=1:start_threshold=%s:start_silence=%.2f:detection=rms"
    chain += ",".join([edge % (THRESHOLD, LEAD), "areverse", edge % (THRESHOLD, TAIL), "areverse"])
    probe = ffmpeg("-i", src, "-af", chain + ",loudnorm=I=-16:TP=-1.5:LRA=11:print_format=json", "-f", "null", "-")
    m = json.loads(probe.stderr[probe.stderr.rindex("{"):probe.stderr.rindex("}") + 1])
    # one gain for the whole line; where a plosive would then pass the peak ceiling, a fast limiter catches just it
    gain = -16.0 - float(m["input_i"])
    level = chain + ",volume=%.2fdB" % gain
    if float(m["input_tp"]) + gain > -1.5:
        level += ",alimiter=limit=%.4f:attack=1:release=50:level=0" % (10 ** (-2.0 / 20))
    ffmpeg("-y", "-i", src, "-af", level + ",aresample=44100", "-ac", "1", "-ar", "44100",
           "-c:a", "pcm_s16le", "-map_metadata", "-1", out)


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
def build(text):
    multi = [s for s, ids in MAP if len(ids) > 1]
    heard = transcribe([os.path.join(SRC, s) for s in multi]) if multi else {}
    made = []
    for src_name, ids in MAP:
        src = os.path.join(SRC, src_name)
        if len(ids) == 1:
            convert(src, os.path.join(VO, ids[0] + ".wav"))
        else:
            # one take, several lines: cut in the quietest moment between the last word of one and the first of the next
            full = " ".join(text[i] for i in ids)
            times, _, _, _ = align(full, heard[src_name]["words"])
            levels = energy(src)
            cuts, n = [], 0
            for lid in ids[:-1]:
                n += len(tokens(text[lid]))
                last, first = times[n - 1], times[n]
                cuts.append(quietest(levels, (last[0] + last[1]) / 2, (first[0] + first[1]) / 2))
            bounds = [0.0] + cuts + [None]
            for k, lid in enumerate(ids):
                convert(src, os.path.join(VO, lid + ".wav"), bounds[k], bounds[k + 1])
            print("split %s at %s s" % (src_name, ", ".join("%.2f" % c for c in cuts)))
        for lid in ids:
            if ensure_meta(os.path.join(VO, lid + ".wav")):
                made.append(lid)
    print("wrote %d lines from %d recordings%s" % (len(recorded_ids()), len(MAP),
                                                    "; new .meta for " + ", ".join(made) if made else ""))


def cs(s):
    return s.replace("\\", "\\\\").replace('"', '\\"')


def check(text, lines):
    ids = [lid for _, group in MAP for lid in group]
    source = {lid: name for name, group in MAP for lid in group}
    heard = transcribe([os.path.join(VO, lid + ".wav") for lid in ids])
    rows, timing, worst = [], [], []
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
        rows.append((lid, source[lid], length, text[lid], h.get("text", h.get("error", "")), same, len(times), diffs))
        if same < len(times):
            worst.append((lid, diffs))
        timing.append((lid, text[lid], [(end, t[1]) for (_, end), t in zip(tokens(text[lid]), times)]))

    # the subtitles' word timing
    body = []
    for lid, line, marks in timing:
        body.append('        {"%s", new Line("%s",\n            new[] { %s },\n            new[] { %s })},'
                    % (lid, cs(line), ", ".join(str(c) for c, _ in marks), ", ".join("%.2ff" % t for _, t in marks)))
    with open(TIMING_CS, "w") as f:
        f.write("""// GENERATED by Tools/arjun_recordings.py from the recorded lines - re-run it after changing a recording or one of
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

    # the report
    todo = [(lid, line) for lid, speaker, _v, line in lines if speaker.startswith("ARJUN") and lid not in recorded_ids()]
    with open(os.path.join(TOOLS, "make_voices.py")) as f:
        act12_tts = re.findall(r'^\s*"(\w+)": \(ARJUN, "(.*)"\),\s*$', f.read(), re.M)
    todo = [(lid, line) for lid, line in act12_tts if lid not in recorded_ids()] + todo
    out = ["# Arjun's recorded lines, checked",
           "",
           "Generated by `Tools/arjun_recordings.py`. Each line below is the voice actor's recording (`arjun voice overs 2/`)",
           "as the game plays it from `Assets/Resources/VO/`, transcribed on this Mac by macOS's speech recogniser and",
           "compared word by word with the subtitle the game shows. The recogniser often mishears names (Chandnipur, Reddy,",
           "Deshpande, the letter V): a difference in a name alone is the recogniser, not the recording.",
           "",
           "%d lines from %d recordings; %d match their subtitle word for word, %d differ only as listed." % (
               len(rows), len(MAP), sum(1 for r in rows if r[5] == r[6]), sum(1 for r in rows if r[5] < r[6])),
           "",
           "| Line | Recording | Length | Subtitle | Heard | Words |",
           "|---|---|---|---|---|---|"]
    for lid, src, length, sub, said, same, total, diffs in rows:
        out.append("| `%s` | %s | %.2f s | %s | %s | %d/%d%s |" % (
            lid, src.replace("|", "/"), length, sub.replace("|", "/"), said.replace("|", "/"), same, total,
            ("<br>" + "<br>".join(diffs)) if diffs else ""))
    out += ["", "## Arjun's lines with no recording yet (still text-to-speech)", "",
            "%d lines. Record them under the same names (or add them to `MAP` in the tool) to finish the voice." % len(todo), ""]
    out += ["- `%s`: %s" % (lid, line) for lid, line in todo]
    with open(REPORT, "w") as f:
        f.write("\n".join(out) + "\n")

    print("checked %d lines: %d word for word, %d with differences:" % (len(rows), len(rows) - len(worst), len(worst)))
    for lid, diffs in worst:
        print("  %-32s %s" % (lid, "; ".join(diffs)))
    print("wrote", os.path.relpath(TIMING_CS, ROOT), "and", os.path.relpath(REPORT, ROOT))


if __name__ == "__main__":
    text, lines = subtitles()
    missing = [lid for _, ids in MAP for lid in ids if lid not in text]
    if missing:
        sys.exit("no subtitle for: " + ", ".join(missing))
    if "--check" not in sys.argv:
        build(text)
    check(text, lines)
