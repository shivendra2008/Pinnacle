#!/usr/bin/env python3
"""Timed mouth shapes (visemes) for the faces seen speaking in Acts I-III and at Café Irani: Kamala, Bhaskar, Vikram,
Sharma-ji, Pandit Harishankar and Raj Malhotra, measured on their actual dialogue audio.

Run from the project root (macOS; the same `say` voices and ffmpeg as Tools/make_voices.py and Tools/acts_dialogue.py):
    python3 Tools/lipsync/npc_lipsync.py            # every line of those speakers
    python3 Tools/lipsync/npc_lipsync.py kamala_a   # some lines

It is the Curator's pipeline (curator_lipsync.py) pointed at other speakers, with one change: word starts and ends come
from on-device recognition of the shipped audio (Tools/transcribe_words.swift), because these voices are not
AVSpeech voices and many files were rendered by an older `say`, so the TTS word markers cannot be carried over; phonemes from curator_lexicon.txt, and for words not in it a spelling-to-sound rule set
(digraphs, a silent final e, soft c and g) instead of one sound per letter; lip closures (P B M) moved onto the nearest
dip in loudness and F/V onto the nearest burst of high-frequency noise; accents and pauses for brows, nods and blinks.
Writes Assets/Resources/VO/Visemes/<id>.txt (read by VisemeTrack) and Tools/lipsync/npc_report.txt.
"""
import ast, os, re, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import curator_lipsync as cl

SPEAKERS = {"KAMALA", "KAMALA KAPOOR", "BHASKAR", "VIKRAM", "SHARMA-JI", "HARISHANKAR", "PANDIT HARISHANKAR", "RAJ MALHOTRA",
            # Acts IV-V faces seen speaking close up (added 22 Nov 2026)
            "ROHAN KAPOOR", "INSPECTOR REDDY", "PRIYA KAPOOR", "THE JUDGE", "DESHPANDE'S LAWYER", "VIKRAM SAHU", "REPORTER",
            "DESHPANDE'S MAN", "RESIDENT",
            # the Curator's recorded lines (23 Nov 2026): recognised words, like the others (curator_lipsync.py skips them)
            "THE CURATOR"}


def make_voices_lines():
    src = open(os.path.join(cl.ROOT, "Tools", "make_voices.py")).read()
    consts = {}
    for m in re.finditer(r"^([A-Z0-9]+) = (\(.*?\))", src, re.M):
        try: consts[m.group(1)] = ast.literal_eval(m.group(2))
        except Exception: pass
    out = []
    for m in re.finditer(r'^ "([a-z0-9_]+)": \(([A-Z0-9]+), "(.*)"\),\s*$', src, re.M):
        lid, who, text = m.group(1), m.group(2), m.group(3).replace('\\"', '"')
        if who in ("KAMALA", "BHASKAR", "VIKRAM") and who in consts:
            v = consts[who]
            out.append((lid, who, (v[0], v[1], v[2] if len(v) > 2 else 1.0, ""), text))
    return out


def acts_lines():
    src = open(os.path.join(cl.ROOT, "Tools", "acts_dialogue.py")).read()
    ns = {}
    for name in re.findall(r"^([A-Z_]+) = \(", src, re.M):
        m = re.search(r"^%s = (\(.*?\))\s*(#.*)?$" % name, src, re.M)
        ns[name] = ast.literal_eval(m.group(1))
    start = src.index("LINES = [")
    end = src.index("\n]\n", start)
    lines = eval(src[start + len("LINES = "):end + 2], ns)
    sys.path.insert(0, os.path.join(cl.ROOT, "Tools"))
    from vo_recordings import recorded_ids
    rec = recorded_ids()
    # the Curator's text-to-speech lines keep curator_lipsync.py's TTS word markers
    return [(lid, speaker, voice, text) for (lid, speaker, voice, text) in lines
            if speaker in SPEAKERS and (speaker != "THE CURATOR" or lid in rec)]


# ------------------------------------------------------------------ spelling to sound, for words not in the lexicon
GROUPS = [
    ("tion", ["SH", "AH0", "N"]), ("sion", ["ZH", "AH0", "N"]), ("ough", ["AO1"]), ("augh", ["AO1"]), ("igh", ["AY1"]),
    ("eigh", ["EY1"]), ("tch", ["CH"]), ("dge", ["JH"]), ("ph", ["F"]), ("th", ["TH"]), ("sh", ["SH"]), ("ch", ["CH"]),
    ("ck", ["K"]), ("wh", ["W"]), ("ng", ["NG"]), ("qu", ["K", "W"]), ("bh", ["B"]), ("dh", ["D"]), ("kh", ["K"]), ("gh", ["G"]),
    ("ee", ["IY1"]), ("ea", ["IY1"]), ("oo", ["UW1"]), ("ou", ["AW1"]), ("ow", ["OW1"]), ("ai", ["EY1"]), ("ay", ["EY1"]),
    ("oa", ["OW1"]), ("oi", ["OY1"]), ("oy", ["OY1"]), ("au", ["AO1"]), ("aw", ["AO1"]), ("aa", ["AA1"]), ("ii", ["IY1"]),
    ("ar", ["AA1", "R"]), ("er", ["ER0"]), ("ir", ["ER1"]), ("ur", ["ER1"]), ("or", ["AO1", "R"]),
]
SINGLE = {"a": ["AE1"], "e": ["EH1"], "i": ["IH1"], "o": ["AA1"], "u": ["AH1"], "b": ["B"], "d": ["D"], "f": ["F"], "g": ["G"],
          "h": ["HH"], "j": ["JH"], "k": ["K"], "l": ["L"], "m": ["M"], "n": ["N"], "p": ["P"], "q": ["K"], "r": ["R"],
          "s": ["S"], "t": ["T"], "v": ["V"], "w": ["W"], "x": ["K", "S"], "z": ["Z"]}
LONG = {"a": "EY1", "e": "IY1", "i": "AY1", "o": "OW1", "u": "UW1"}


def spell(word):
    w = re.sub(r"[^a-z]", "", word.lower())
    if not w: return []
    out = []
    # a silent final e makes the vowel before it long (make, time, home)
    long_at = -1
    if len(w) > 3 and w.endswith("e") and w[-2] not in "aeiouy" and w[-3] in "aeiou":
        long_at = len(w) - 3
        w = w[:-1]
    i = 0
    while i < len(w):
        hit = False
        for g, ph in GROUPS:
            if w.startswith(g, i):
                out += ph; i += len(g); hit = True; break
        if hit: continue
        ch = w[i]
        nxt = w[i + 1] if i + 1 < len(w) else ""
        if i == long_at and ch in LONG: out.append(LONG[ch])
        elif ch == "c": out.append("S" if nxt in ("e", "i", "y") else "K")
        elif ch == "g" and nxt in ("e", "i") and i > 0: out.append("JH")
        elif ch == "y": out.append("Y" if i == 0 else "IY0")
        elif ch in SINGLE: out += SINGLE[ch]
        i += 1
    return out


def phonemes_for(word, lex, missing):
    w = word.lower().strip(".,;:!?\"'()‘’“”-").replace("’", "'")
    if not w: return []
    if w in lex: return lex[w]
    if "-" in w or " " in w:
        out = []
        for part in re.split(r"[-\s]+", w): out += phonemes_for(part, lex, missing)
        return out
    if w.endswith("'s") and w[:-2] in lex: return lex[w[:-2]] + ["Z"]
    missing.add(w)
    return spell(w)


# ------------------------------------------------------------------ word times from the audio itself
LOCALE = "en-IN"


def transcribe(paths):
    """On-device recognition (Tools/transcribe_words.swift): words with start and end seconds, per file."""
    import json, subprocess, tempfile
    exe = os.path.join(tempfile.gettempdir(), "pinnacle_transcribe_words")
    src = os.path.join(cl.ROOT, "Tools", "transcribe_words.swift")
    if not os.path.exists(exe) or os.path.getmtime(exe) < os.path.getmtime(src):
        subprocess.run(["swiftc", "-parse-as-library", "-o", exe, src], check=True)
    with tempfile.TemporaryDirectory() as d:
        out = os.path.join(d, "words.json")
        subprocess.run([exe, LOCALE, out] + list(paths), check=True)
        with open(out) as f:
            return {r["file"]: r for r in json.load(f)}


def envelopes(path):
    """Loudness and high-frequency loudness in 10 ms frames (dB), from the file (no numpy here)."""
    d, sr = cl.wav_samples(path)
    hop = sr // 100
    env, high = [], []
    prev = 0
    for k in range(0, len(d) - hop, hop):
        s2 = h2 = 0.0
        for i in range(k, k + hop):
            x = d[i]; s2 += x * x
            dx = x - prev; prev = x; h2 += dx * dx
        import math
        env.append(10 * math.log10(s2 / hop / (32768.0 ** 2) + 1e-12))
        high.append(10 * math.log10(h2 / hop / (32768.0 ** 2) + 1e-12))
    return env, high, len(d) / float(sr)


def build_heard(lid, heard, lex, report):
    """cl.build's steps 2-5, from recognised word times and envelopes measured on the file."""
    import re as _re
    words = [{"text": w["w"], "start": w["s"], "end": w["e"]} for w in heard.get("words", []) if w["w"].strip()]
    if not words:
        report.append("%-14s  NOTHING HEARD" % lid); return None
    env, high, dur = envelopes(os.path.join(cl.VO, lid + ".wav"))
    def e_at(t): k = int(t / 0.01); return env[k] if 0 <= k < len(env) else -120
    def h_at(t): k = int(t / 0.01); return high[k] - env[k] if 0 <= k < len(env) else -60
    missing = set()
    segs = []
    for wi, w in enumerate(words):
        phs = phonemes_for(w["text"], lex, missing)
        if not phs: continue
        t0, t1 = w["start"], w["end"]
        weights = []
        for k, p in enumerate(phs):
            b = _re.sub(r"\d", "", p); st = int(p[-1]) if p[-1].isdigit() else -1
            l = cl.BASE_LEN.get(b, 1.0)
            if b in cl.VOWELS: l *= 1.0 if st == 1 else 0.85 if st == 2 else 0.55
            if k == len(phs) - 1: l *= 1.25
            weights.append(l)
        tot = sum(weights); t = t0
        for p, l in zip(phs, weights):
            dd = (t1 - t0) * l / tot
            segs.append([p, t, t + dd, wi]); t += dd
    for k, s in enumerate(segs):
        b = _re.sub(r"\d", "", s[0])
        if b not in ("P", "B", "M", "F", "V"): continue
        c, half = (s[1] + s[2]) / 2, (s[2] - s[1]) / 2
        lo_lim = segs[k - 1][1] + 0.025 + half if k > 0 else s[1]
        hi_lim = segs[k + 1][2] - 0.025 - half if k + 1 < len(segs) else s[2]
        rng = 0.045 if b in ("P", "B", "F", "V") else 0.035
        best, best_v = c, None
        t = max(lo_lim, c - rng)
        while t <= min(hi_lim, c + rng):
            v = -e_at(t) if b in ("P", "B", "M") else h_at(t)
            if best_v is None or v > best_v: best_v, best = v, t
            t += 0.005
        if abs(best - c) > 0.004:
            s[1], s[2] = best - half, best + half
            if k > 0: segs[k - 1][2] = s[1]
            if k + 1 < len(segs): segs[k + 1][1] = s[2]
    keys = []
    for k, (p, t0, t1, wi) in enumerate(segs):
        nxt = None
        for q in segs[k + 1:]:
            if _re.sub(r"\d", "", q[0]) in cl.VOWELS: nxt = cl.visemes_of(q[0], None)[0][0]; break
        parts = cl.visemes_of(p, nxt)
        st = int(p[-1]) if p[-1].isdigit() else -1
        t = t0
        for v, share in parts:
            dd = (t1 - t0) * share
            weight = 1.0
            if _re.sub(r"\d", "", p) in cl.VOWELS and st == 0: weight = 0.6
            if _re.sub(r"\d", "", p) == "HH": weight = 0.45
            keys.append((t, t + dd, v, weight, st, p))
            t += dd
    vowel_db = [max(e_at(t0 + (t1 - t0) * f) for f in (0.3, 0.5, 0.7)) for (t0, t1, v, w, st, p) in keys if st == 1]
    med = sorted(vowel_db)[len(vowel_db) // 2] if vowel_db else 0
    accents = []
    for (t0, t1, v, w, st, p) in keys:
        if st != 1: continue
        db = max(e_at(t0 + (t1 - t0) * f) for f in (0.3, 0.5, 0.7))
        if db > med + 1.5: accents.append((t0, min(1.0, (db - med) / 8.0)))
    pauses = []
    for i in range(len(words) - 1):
        gap = words[i + 1]["start"] - words[i]["end"]
        if gap > 0.12: pauses.append((words[i]["end"], words[i + 1]["start"]))
    os.makedirs(cl.OUT, exist_ok=True)
    with open(os.path.join(cl.OUT, lid + ".txt"), "w") as f:
        f.write("# %s  %.3f s  words heard on-device (Tools/lipsync/npc_lipsync.py)\n" % (lid, dur))
        f.write("# V start end viseme weight stress phoneme | A time strength | P start end | W start end word\n")
        for w in words: f.write("W %.3f %.3f %s\n" % (w["start"], w["end"], _re.sub(r"\s", "_", w["text"])))
        for (t0, t1, v, w, st, p) in keys: f.write("V %.3f %.3f %s %.2f %d %s\n" % (t0, t1, v, w, st, p))
        for (t, s_) in accents: f.write("A %.3f %.2f\n" % (t, s_))
        for (t0, t1) in pauses: f.write("P %.3f %.3f\n" % (t0, t1))
    pp = sum(1 for k in keys if k[2] == "PP"); ff = sum(1 for k in keys if k[2] == "FF")
    report.append("%-14s %5.2fs  words %2d  visemes %3d  PP %2d  FF %2d  accents %2d  pauses %d%s" % (
        lid, dur, len(words), len(keys), pp, ff, len(accents), len(pauses), ("  SPELLED " + " ".join(sorted(missing))) if missing else ""))
    return keys


def main():
    only = set(sys.argv[1:])
    os.makedirs(cl.BUILD, exist_ok=True)
    cl.phonemes_for = phonemes_for            # the spelling rules instead of one sound per letter
    lex = cl.load_lexicon()
    report = []
    todo = [(lid, speaker) for lid, speaker, voice, text in make_voices_lines() + acts_lines()
            if (not only or lid in only) and os.path.exists(os.path.join(cl.VO, lid + ".wav"))]
    # The TTS word markers cannot be used here (the voices are not AVSpeech voices, and many files were rendered by an
    # older `say`): the words are timed by on-device recognition of the shipped audio instead.
    heard = transcribe([os.path.join(cl.VO, lid + ".wav") for lid, _ in todo])
    for lid, speaker in todo:
        h = heard.get(lid + ".wav", {})
        if "error" in h: report.append("%-14s  RECOGNITION FAILED %s" % (lid, h["error"])); print(report[-1], flush=True); continue
        build_heard(lid, h, lex, report)
        report[-1] += "  [" + speaker + "]"
        print(report[-1], flush=True)
    if not only:
        with open(os.path.join(cl.HERE, "npc_report.txt"), "w") as f: f.write("\n".join(report) + "\n")


if __name__ == "__main__":
    main()
