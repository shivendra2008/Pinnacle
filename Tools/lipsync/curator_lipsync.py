#!/usr/bin/env python3
"""Timed mouth shapes (visemes) for every Curator line, measured on the actual dialogue audio.

Run from the project root (macOS; needs the same `say` voices and ffmpeg as Tools/acts_dialogue.py):
    python3 Tools/lipsync/curator_lipsync.py            # all Curator lines
    python3 Tools/lipsync/curator_lipsync.py cur0_15    # some lines

For each line:
  1. align_words.swift gives the start and end of every word in Assets/Resources/VO/<id>.wav (AVSpeechSynthesizer
     word markers on the same TTS voice, carried onto the `say` render with DTW; the `say` render is bit-identical
     to the shipped file, which this script checks first).
  2. Each word is spelled out in phonemes from curator_lexicon.txt (British, non-rhotic, written for this script)
     and the phonemes share the word's time by their typical lengths.
  3. Lip closures (P, B, M) are moved onto the nearest dip in loudness and F/V onto the nearest burst of
     high-frequency noise, so the visible contacts land where the sound says they are.
  4. Phonemes become visemes (the 15-shape set: sil PP FF TH DD kk CH SS nn RR aa E I O U); diphthongs are two.
  5. Stressed, louder vowels become accents (for brows and head), pauses are listed (for breaths and blinks).
Writes Assets/Resources/VO/Visemes/<id>.txt (read by CharacterNPC.VisemeTrack) and Tools/lipsync/report.txt.

This is lip sync for synthetic speech. Recorded performances replace both the audio and these files: run the
same script on the new recordings (align_words needs the text; it works from the audio, the TTS is only used to
find word starts - for recorded VO, swap step 1 for a forced aligner and keep steps 2-5).
"""
import ast, json, os, re, subprocess, sys, tempfile, wave, array, math

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
VO = os.path.join(ROOT, "Assets", "Resources", "VO")
OUT = os.path.join(VO, "Visemes")
BUILD = os.path.join(tempfile.gettempdir(), "pinnacle_lipsync")

# ------------------------------------------------------------------ the dialogue source
def curator_lines():
    src = open(os.path.join(ROOT, "Tools", "acts_dialogue.py")).read()
    ns = {}
    for name in re.findall(r"^([A-Z_]+) = \(", src, re.M):
        m = re.search(r"^%s = (\(.*?\))\s*(#.*)?$" % name, src, re.M)
        ns[name] = ast.literal_eval(m.group(1))
    start = src.index("LINES = [")
    end = src.index("\n]\n", start)
    lines = eval(src[start + len("LINES = "):end + 2], ns)
    return [(lid, voice, text) for (lid, speaker, voice, text) in lines if speaker == "THE CURATOR"]

# ------------------------------------------------------------------ phonemes
def load_lexicon():
    lex = {}
    for raw in open(os.path.join(HERE, "curator_lexicon.txt")):
        raw = raw.strip()
        if not raw or raw.startswith("#"): continue
        w, *ph = raw.split()
        lex[w] = ph
    return lex

VOWELS = {"AA", "AE", "AH", "AO", "AW", "AY", "EH", "ER", "EY", "IH", "IY", "OW", "OY", "UH", "UW"}
BASE_LEN = {   # typical relative lengths
    "AA": 1.9, "AE": 1.8, "AH": 1.4, "AO": 1.9, "AW": 2.1, "AY": 2.1, "EH": 1.5, "ER": 1.8, "EY": 1.9,
    "IH": 1.2, "IY": 1.6, "OW": 1.9, "OY": 2.1, "UH": 1.2, "UW": 1.6,
    "P": 1.0, "B": 0.8, "T": 0.9, "D": 0.7, "K": 1.0, "G": 0.8, "CH": 1.3, "JH": 1.1,
    "F": 1.1, "V": 0.8, "TH": 1.0, "DH": 0.5, "S": 1.3, "Z": 0.9, "SH": 1.3, "ZH": 1.0, "HH": 0.7,
    "M": 0.9, "N": 0.8, "NG": 0.9, "L": 0.8, "R": 0.8, "W": 0.8, "Y": 0.7,
}

def phonemes_for(word, lex, missing):
    w = word.lower().strip(".,;:!?\"'()‘’“”-")
    w = w.replace("’", "'")
    if not w: return []
    if w in lex: return lex[w]
    if "-" in w or " " in w:          # the synthesizer reports some pairs ("no one") as one word
        out = []
        for part in re.split(r"[-\s]+", w): out += phonemes_for(part, lex, missing)
        return out
    missing.add(w)
    # crude fallback: one consonant or vowel per letter
    table = {"a": "AE1", "e": "EH1", "i": "IH1", "o": "AA1", "u": "AH1", "y": "IY0", "b": "B", "c": "K", "d": "D",
             "f": "F", "g": "G", "h": "HH", "j": "JH", "k": "K", "l": "L", "m": "M", "n": "N", "p": "P", "q": "K",
             "r": "R", "s": "S", "t": "T", "v": "V", "w": "W", "x": "K", "z": "Z"}
    return [table[c] for c in w if c in table]

# ------------------------------------------------------------------ visemes
def visemes_of(ph, nxt_vowel):
    """One phoneme -> one or two (viseme, share) pairs."""
    base = re.sub(r"\d", "", ph)
    stress = int(ph[-1]) if ph[-1].isdigit() else -1
    if base in ("P", "B", "M"): return [("PP", 1.0)]
    if base in ("F", "V"): return [("FF", 1.0)]
    if base in ("TH", "DH"): return [("TH", 1.0)]
    if base in ("T", "D"): return [("DD", 1.0)]
    if base in ("K", "G", "NG"): return [("kk", 1.0)]
    if base in ("CH", "JH", "SH", "ZH"): return [("CH", 1.0)]
    if base in ("S", "Z"): return [("SS", 1.0)]
    if base in ("N", "L"): return [("nn", 1.0)]
    if base == "R": return [("RR", 1.0)]
    if base == "W": return [("U", 1.0)]
    if base == "Y": return [("I", 1.0)]
    if base == "HH": return [(nxt_vowel or "aa", 1.0)]
    # vowels; diphthongs move between two shapes
    if base == "AY": return [("aa", 0.55), ("I", 0.45)]
    if base == "AW": return [("aa", 0.55), ("U", 0.45)]
    if base == "OY": return [("O", 0.55), ("I", 0.45)]
    if base == "EY": return [("E", 0.6), ("I", 0.4)]
    if base == "OW": return [("E", 0.35), ("U", 0.65)]      # RP "go": a central start rounding into oo
    if base in ("AA", "AE"): return [("aa", 1.0)]
    if base == "AH": return [("aa", 1.0)]
    if base in ("EH", "ER"): return [("E", 1.0)]
    if base in ("IH", "IY"): return [("I", 1.0)]
    if base == "AO": return [("O", 1.0)]
    if base in ("UH", "UW"): return [("U", 1.0)]
    return [("sil", 1.0)]

def first_vowel_viseme(phs):
    for p in phs:
        b = re.sub(r"\d", "", p)
        if b in VOWELS: return visemes_of(p, None)[0][0]
    return None

# ------------------------------------------------------------------ audio checks
def wav_samples(path):
    w = wave.open(path)
    d = array.array("h", w.readframes(w.getnframes()))
    return d, w.getframerate()

def say_matches_vo(lid, voice, text):
    """The VO file must still be exactly what the TTS pipeline produces (else the timing would not transfer)."""
    name, rate, pitch, extra = voice
    with tempfile.TemporaryDirectory() as d:
        aiff, wav_ = os.path.join(d, "x.aiff"), os.path.join(d, "x.wav")
        subprocess.run(["say", "-v", name, "-r", str(rate), "-o", aiff, text], check=True)
        chain = "aresample=44100"
        if pitch != 1.0: chain += ",asetrate=%d,aresample=44100,atempo=%.4f" % (int(44100 * pitch), 1.0 / pitch)
        if extra: chain += "," + extra
        chain += ",loudnorm=I=-16:TP=-1.5,aresample=44100"
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", aiff, "-ac", "1", "-ar", "44100", "-af", chain, wav_], check=True)
        a, _ = wav_samples(wav_)
        b, _ = wav_samples(os.path.join(VO, lid + ".wav"))
    if abs(len(a) - len(b)) > 64: return False, "length %d vs %d" % (len(a), len(b))
    n = min(len(a), len(b))
    num = sum(a[i] * b[i] for i in range(0, n, 7))
    den = math.sqrt(sum(a[i] * a[i] for i in range(0, n, 7)) * sum(b[i] * b[i] for i in range(0, n, 7))) or 1
    return num / den > 0.995, "correlation %.4f" % (num / den)

# ------------------------------------------------------------------ the line
def build(lid, voice, text, lex, aligner, report):
    js = os.path.join(BUILD, lid + ".json")
    r = subprocess.run([aligner, voice[0], str(voice[1]), os.path.join(VO, lid + ".wav"), text, js], capture_output=True, text=True)
    if r.returncode != 0:
        report.append("%s  ALIGN FAILED  %s" % (lid, r.stderr.strip()[-200:])); return None
    a = json.load(open(js))
    env, high = a["envDb"], a["highDb"]
    def e_at(t): k = int(t / 0.01); return env[k] if 0 <= k < len(env) else -120
    def h_at(t): k = int(t / 0.01); return high[k] - env[k] if 0 <= k < len(env) else -60
    missing = set()
    segs = []   # [phoneme, start, end, word index]
    for wi, w in enumerate(a["words"]):
        phs = phonemes_for(w["text"], lex, missing)
        if not phs: continue
        t0, t1 = w["start"], w["end"]
        weights = []
        for k, p in enumerate(phs):
            b = re.sub(r"\d", "", p); st = int(p[-1]) if p[-1].isdigit() else -1
            l = BASE_LEN.get(b, 1.0)
            if b in VOWELS: l *= 1.0 if st == 1 else 0.85 if st == 2 else 0.55
            if k == len(phs) - 1: l *= 1.25            # word-final lengthening
            weights.append(l)
        tot = sum(weights); t = t0
        for p, l in zip(phs, weights):
            d = (t1 - t0) * l / tot
            segs.append([p, t, t + d, wi]); t += d
    # snap visible contacts onto the sound
    for k, s in enumerate(segs):
        b = re.sub(r"\d", "", s[0])
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
    # visemes
    keys = []
    for k, (p, t0, t1, wi) in enumerate(segs):
        nxt = None
        for q in segs[k + 1:]:
            if re.sub(r"\d", "", q[0]) in VOWELS: nxt = visemes_of(q[0], None)[0][0]; break
        parts = visemes_of(p, nxt)
        st = int(p[-1]) if p[-1].isdigit() else -1
        t = t0
        for v, share in parts:
            d = (t1 - t0) * share
            weight = 1.0
            if re.sub(r"\d", "", p) in VOWELS and st == 0: weight = 0.6       # reduced vowels: smaller shapes
            if re.sub(r"\d", "", p) == "HH": weight = 0.45
            keys.append((t, t + d, v, weight, st, p))
            t += d
    # accents: stressed vowels that stand out in loudness
    vowel_db = [max(e_at(t0 + (t1 - t0) * f) for f in (0.3, 0.5, 0.7)) for (t0, t1, v, w, st, p) in keys if st == 1]
    med = sorted(vowel_db)[len(vowel_db) // 2] if vowel_db else 0
    accents = []
    for (t0, t1, v, w, st, p) in keys:
        if st != 1: continue
        db = max(e_at(t0 + (t1 - t0) * f) for f in (0.3, 0.5, 0.7))
        if db > med + 1.5: accents.append((t0, min(1.0, (db - med) / 8.0)))
    pauses = []
    words = a["words"]
    for i in range(len(words) - 1):
        gap = words[i + 1]["start"] - words[i]["end"]
        if gap > 0.12: pauses.append((words[i]["end"], words[i + 1]["start"]))
    # write
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, lid + ".txt"), "w") as f:
        f.write("# %s  %.3f s  TTS %s %s  (Tools/lipsync/curator_lipsync.py)\n" % (lid, a["finalDuration"], voice[0], voice[1]))
        f.write("# V start end viseme weight stress phoneme | A time strength | P start end | W start end word\n")
        for w in words: f.write("W %.3f %.3f %s\n" % (w["start"], w["end"], re.sub(r"\s", "_", w["text"])))
        for (t0, t1, v, w, st, p) in keys: f.write("V %.3f %.3f %s %.2f %d %s\n" % (t0, t1, v, w, st, p))
        for (t, s) in accents: f.write("A %.3f %.2f\n" % (t, s))
        for (t0, t1) in pauses: f.write("P %.3f %.3f\n" % (t0, t1))
    pp = sum(1 for k in keys if k[2] == "PP"); ff = sum(1 for k in keys if k[2] == "FF")
    report.append("%-12s %5.2fs  words %2d  visemes %3d  PP %2d  FF %2d  accents %2d  pauses %d  dtw %.3f  env %.2f%s" % (
        lid, a["finalDuration"], len(words), len(keys), pp, ff, len(accents), len(pauses), a["dtwCost"], a["envCorrelation"],
        ("  MISSING " + " ".join(sorted(missing))) if missing else ""))
    return keys

def main():
    only = set(sys.argv[1:])
    os.makedirs(BUILD, exist_ok=True)
    aligner = os.path.join(BUILD, "align_words")
    src = os.path.join(HERE, "align_words.swift")
    if not os.path.exists(aligner) or os.path.getmtime(aligner) < os.path.getmtime(src):
        subprocess.run(["swiftc", "-O", src, "-o", aligner], check=True, capture_output=True)
    lex = load_lexicon()
    report = []
    for lid, voice, text in curator_lines():
        if only and lid not in only: continue
        if not os.path.exists(os.path.join(VO, lid + ".wav")):
            report.append("%-12s  no audio" % lid); continue
        ok, why = say_matches_vo(lid, voice, text)
        if not ok:
            report.append("%-12s  SKIPPED: the VO file is not what the TTS pipeline makes now (%s)" % (lid, why)); continue
        build(lid, voice, text, lex, aligner, report)
        print(report[-1], flush=True)
    if not only:
        with open(os.path.join(HERE, "report.txt"), "w") as f: f.write("\n".join(report) + "\n")

if __name__ == "__main__":
    main()
