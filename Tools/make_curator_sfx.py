#!/usr/bin/env python3
"""Cuts the Curator's Gallery sound effects out of licensed field recordings (Wikimedia Commons, public domain and
CC BY 4.0) into Assets/Resources/Audio/Curator/<category>/, and writes CREDITS.txt there.

    python3 Tools/make_curator_sfx.py <folder with the downloaded .ogg originals>

The originals (exact files, licences and authors are listed in SOURCES below and in the CREDITS file):
  thunder and rain    Thunderstorm after hot summer day 17 minutes 01/02 of 04 (stephan, public domain)
                      Medium shower accompanied by thunders (CutSomeSlackenstein, CC BY 4.0)
                      Rain and thunder (1), Thunder and rain on a veranda (ezwa, public domain)
  rain on the glass   Rain against the window (cori, public domain)
  footsteps           Creaking floors, recorded in the Picture Gallery of the Kunsthistorisches Museum
                      (Work With Sounds / Torsten Nilsson, CC BY 4.0); Church, people walking, steps (stephan, PD)
  book and pages      Old book, Turning a page, Looking up a word in the dictionary (cori, planish, ezwa; PD)

What it does (no hand-picked timestamps, so it can be re-run on other recordings):
  - thunder: peaks of the low band (below 200 Hz) more than 9 dB over the recording's median; each event is cut from
    0.4 s before its onset to where its rumble has decayed, then classed "near" (a strong high-frequency crack at the
    onset) or "far" (low rumble only);
  - rain beds: the longest stretches with no thunder, made into seamless loops with a 2 s crossfade;
  - footsteps and page sounds: onsets (energy jumps above the local level) cut into short takes with a fade-out;
  - room tone: the outside rain through thick walls (low-passed at 280 Hz), quiet.
Every take is peak-limited to -1 dBFS and loudness-matched per category, mono, 44.1 kHz, 16-bit WAV.
The match strike, the candle being blown out and clothing movement are NOT in these recordings: they stay on the
synthesised placeholders until recorded (see Docs/CURATOR.md).
"""
import array, math, os, subprocess, sys, wave

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "Audio", "Curator")
SR = 44100

SOURCES = {
    "Thunderstorm_after_hot_summer_day_17_minutes_01_of_04.ogg": ("Thunderstorm after hot summer day, 17 minutes, 01 of 04", "stephan", "Public domain", "https://commons.wikimedia.org/wiki/File:Thunderstorm_after_hot_summer_day_17_minutes_01_of_04.ogg"),
    "Thunderstorm_after_hot_summer_day_17_minutes_02_of_04.ogg": ("Thunderstorm after hot summer day, 17 minutes, 02 of 04", "stephan", "Public domain", "https://commons.wikimedia.org/wiki/File:Thunderstorm_after_hot_summer_day_17_minutes_02_of_04.ogg"),
    "Medium_shower_accompanied_by_thunders.ogg": ("Medium shower accompanied by thunders", "CutSomeSlackenstein", "CC BY 4.0", "https://commons.wikimedia.org/wiki/File:Medium_shower_accompanied_by_thunders.ogg"),
    "Rain_and_thunder_(1).ogg": ("Rain and thunder (1)", "ezwa", "Public domain", "https://commons.wikimedia.org/wiki/File:Rain_and_thunder_(1).ogg"),
    "Thunder_and_rain_on_a_v.ogg": ("Thunder and rain on a veranda", "ezwa", "Public domain", "https://commons.wikimedia.org/wiki/File:Thunder_and_rain_on_a_v.ogg"),
    "Rain_against_the_window.ogg": ("Rain against the window", "cori", "Public domain", "https://commons.wikimedia.org/wiki/File:Rain_against_the_window.ogg"),
    "WWS_Creakingfloors.ogg": ("Creaking floors (Picture Gallery, Kunsthistorisches Museum)", "Work With Sounds / Torsten Nilsson", "CC BY 4.0", "https://commons.wikimedia.org/wiki/File:WWS_Creakingfloors.ogg"),
    "Church_people_walking_steps_with_reverb.ogg": ("Church, people walking, steps with reverb", "stephan", "Public domain", "https://commons.wikimedia.org/wiki/File:Church_people_walking_steps_with_reverb.ogg"),
    "Old_book.ogg": ("Old book (leafing through pages, shutting book)", "cori", "Public domain", "https://commons.wikimedia.org/wiki/File:Old_book.ogg"),
    "Turning_a_page.ogg": ("Turning a page", "planish", "Public domain", "https://commons.wikimedia.org/wiki/File:Turning_a_page.ogg"),
    "Looking_up_a_word_in_the_dictionary.ogg": ("Looking up a word in the dictionary", "ezwa", "Public domain", "https://commons.wikimedia.org/wiki/File:Looking_up_a_word_in_the_dictionary.ogg"),
}


def decode(path, rate=SR, filt=None):
    cmd = ["ffmpeg", "-v", "error", "-i", path, "-ac", "1", "-ar", str(rate)]
    if filt: cmd += ["-af", filt]
    cmd += ["-f", "s16le", "-"]
    raw = subprocess.run(cmd, capture_output=True, check=True).stdout
    a = array.array("h"); a.frombytes(raw)
    return a


def env_db(a, rate, hop):
    out = []
    for i in range(0, len(a) - hop, hop):
        s = 0
        for k in range(i, i + hop, 2): s += a[k] * a[k]
        out.append(10 * math.log10(s / (hop / 2) + 1.0))
    return out


def median(x):
    s = sorted(x); return s[len(s) // 2] if s else 0


def write_wav(path, samples, rate=SR):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    w = wave.open(path, "wb"); w.setnchannels(1); w.setsampwidth(2); w.setframerate(rate)
    w.writeframes(array.array("h", [max(-32768, min(32767, int(v))) for v in samples]).tobytes()); w.close()


def finish(samples, peak_db=-1.0, fade_in=0.004, fade_out=0.25):
    n = len(samples)
    fi, fo = int(fade_in * SR), int(fade_out * SR)
    out = [float(v) for v in samples]
    for i in range(min(fi, n)): out[i] *= i / fi
    for i in range(min(fo, n)): out[n - 1 - i] *= i / fo
    pk = max(1.0, max(abs(v) for v in out))
    g = (10 ** (peak_db / 20) * 32767) / pk
    return [v * g for v in out]


def loop(samples, xfade=2.0):
    x = int(xfade * SR)
    body = samples[:len(samples) - x]
    for i in range(x):
        k = i / x
        body[i] = body[i] * k + samples[len(samples) - x + i] * (1 - k)
    return body


def thunder_events(path, report):
    low = decode(path, 4000, "lowpass=f=200")
    hi = decode(path, 16000, "highpass=f=2500")
    hop_l, hop_h = 200, 800                       # 50 ms
    el, eh = env_db(low, 4000, hop_l), env_db(hi, 16000, hop_h)
    base = median(el); hbase = median(eh)
    events, i = [], 0
    while i < len(el):
        if el[i] > base + 9:
            j = i
            while j < len(el) and el[j] > base + 3: j += 1        # until the rumble has decayed
            peak = max(el[i:j]) if j > i else el[i]
            crack = max(eh[i:min(len(eh), i + 8)]) - hbase if i < len(eh) else 0
            dur = (j - i) * 0.05
            if dur > 2.5 and peak - base >= 12: events.append((max(0.0, i * 0.05 - 0.4), min(dur + 1.5, 12.0), peak - base, crack))
            i = j + 20
        else:
            i += 1
    report.append("  %s: %d thunder events over a median low band of %.1f dB" % (os.path.basename(path), len(events), base))
    return events, base, el


def onsets(a, rate, min_gap, rise_db, hop=220):
    e = env_db(a, rate, hop)
    out, last = [], -1e9
    for i in range(4, len(e)):
        loc = median(e[max(0, i - 40):i])
        if e[i] - loc > rise_db and e[i] >= e[i - 1] and (i - last) * hop / rate > min_gap:
            out.append(i * hop / rate); last = i
    return out


def main():
    src = sys.argv[1]
    report = []
    full = {}
    def get(name):
        if name not in full: full[name] = decode(os.path.join(src, name))
        return full[name]
    # ---- thunder (near / far) and the rain beds between them
    near, far, beds = [], [], []
    for name in ["Thunderstorm_after_hot_summer_day_17_minutes_01_of_04.ogg", "Thunderstorm_after_hot_summer_day_17_minutes_02_of_04.ogg",
                 "Medium_shower_accompanied_by_thunders.ogg", "Rain_and_thunder_(1).ogg", "Thunder_and_rain_on_a_v.ogg"]:
        events, base, el = thunder_events(os.path.join(src, name), report)
        a = get(name)
        for (t0, d, strength, crack) in events:
            take = a[int(t0 * SR):int((t0 + d) * SR)]
            if len(take) < SR: continue
            (near if crack > 15 and strength >= 15 else far).append((strength, take, name, t0))
        # rain: the longest thunder-free stretches (at least 25 s), from the stereo storm recordings
        if "Thunderstorm" in name or "Medium_shower" in name:
            busy = [False] * len(el)
            for (t0, d, s, c) in events:
                for k in range(int(t0 / 0.05) - 20, int((t0 + d) / 0.05) + 40):
                    if 0 <= k < len(busy): busy[k] = True
            k = 0
            while k < len(busy):
                if not busy[k]:
                    j = k
                    while j < len(busy) and not busy[j]: j += 1
                    if (j - k) * 0.05 > 25: beds.append((j - k, a[int(k * 0.05 * SR):int(min(j * 0.05, k * 0.05 + 45) * SR)], name, k * 0.05))
                    k = j
                else: k += 1
    near.sort(key=lambda x: -x[0]); far.sort(key=lambda x: -x[0])
    for cat, items in (("thunder_near", near[:5]), ("thunder_far", far[:6])):
        for n, (s, take, name, t0) in enumerate(items):
            write_wav(os.path.join(OUT, cat, "%s_%02d.wav" % (cat, n)), finish(take, -1.0, 0.01, 1.2))
            report.append("  %s_%02d  %4.1f s  from %s at %.1f s" % (cat, n, len(take) / SR, name, t0))
    beds.sort(key=lambda x: -x[0])
    for n, (ln, take, name, t0) in enumerate(beds[:2]):
        write_wav(os.path.join(OUT, "rain_outside", "rain_outside_%02d.wav" % n), finish(loop(list(take)), -6.0, 0.0, 0.0))
        report.append("  rain_outside_%02d  %4.1f s loop  from %s at %.1f s" % (n, len(take) / SR - 2, name, t0))
        if n == 0:
            # room tone: the same storm through thick walls - low-passed, quiet
            tmp = os.path.join(OUT, "rain_outside", "rain_outside_00.wav")
            lp = decode(tmp, SR, "lowpass=f=280,lowpass=f=280")
            write_wav(os.path.join(OUT, "room_tone", "room_tone_00.wav"), finish(list(lp), -14.0, 0.0, 0.0))
    # ---- rain on the window glass: one long loop
    w = get("Rain_against_the_window.ogg")
    write_wav(os.path.join(OUT, "rain_window", "rain_window_00.wav"), finish(loop(list(w[int(2 * SR):int(62 * SR)])), -6.0, 0.0, 0.0))
    report.append("  rain_window_00  60 s loop  from Rain_against_the_window.ogg")
    # ---- footsteps: clean onsets (a sharp rise over the level just before), mostly the museum's wooden floor
    steps = []
    for name, gap, rise, keep in (("WWS_Creakingfloors.ogg", 0.35, 9.0, 9), ("Church_people_walking_steps_with_reverb.ogg", 0.35, 8.0, 3)):
        a = get(name)
        found = []
        for t in onsets(a, SR, gap, rise):
            take = a[max(0, int((t - 0.02) * SR)):int((t + 0.42) * SR)]
            pre = a[max(0, int((t - 0.25) * SR)):max(1, int((t - 0.03) * SR))]
            if len(take) < 0.3 * SR or len(pre) < 100: continue
            pk = max(abs(v) for v in take[:int(0.08 * SR)])
            floor = math.sqrt(sum(v * v for v in pre) / len(pre)) + 1.0
            tail = math.sqrt(sum(v * v for v in take[int(0.25 * SR):]) / max(1, len(take) - int(0.25 * SR))) + 1.0
            found.append((20 * math.log10(pk / floor) - 0.3 * 20 * math.log10(tail / floor), take, name, t))   # crisp onset, quiet tail
        found.sort(key=lambda x: -x[0])
        steps += found[:keep]
    for n, (q, take, name, t) in enumerate(steps):
        write_wav(os.path.join(OUT, "steps_wood", "step_%02d.wav" % n), finish(take, -3.0, 0.002, 0.18))
        report.append("  step_%02d  from %s at %.2f s (onset contrast %.0f dB)" % (n, name, t, q))
    # ---- pages, the cover, the book set down
    pages = []
    for name, gap, rise in (("Turning_a_page.ogg", 0.6, 8.0), ("Looking_up_a_word_in_the_dictionary.ogg", 0.5, 8.0), ("Old_book.ogg", 0.5, 9.0)):
        a = get(name)
        for t in onsets(a, SR, gap, rise):
            take = a[max(0, int((t - 0.03) * SR)):int((t + 0.65) * SR)]
            if len(take) > 0.4 * SR: pages.append((max(abs(v) for v in take), take, name, t))
    pages.sort(key=lambda x: -x[0])
    # the loudest, shortest-decaying events are cover slaps and the book being shut; the softer ones are page turns
    for n, (pk, take, name, t) in enumerate(pages[:3]):
        write_wav(os.path.join(OUT, "book_down", "book_down_%02d.wav" % n), finish(take, -2.0, 0.002, 0.2))
        report.append("  book_down_%02d  from %s at %.2f s" % (n, name, t))
    for n, (pk, take, name, t) in enumerate(pages[3:5]):
        write_wav(os.path.join(OUT, "book_open", "book_open_%02d.wav" % n), finish(take, -4.0, 0.004, 0.25))
        write_wav(os.path.join(OUT, "book_take", "book_take_%02d.wav" % n), finish(take, -6.0, 0.004, 0.25))
        report.append("  book_open/take_%02d  from %s at %.2f s" % (n, name, t))
    for n, (pk, take, name, t) in enumerate(pages[5:15]):
        write_wav(os.path.join(OUT, "page", "page_%02d.wav" % n), finish(take, -6.0, 0.004, 0.25))
        report.append("  page_%02d  from %s at %.2f s" % (n, name, t))
    # ---- credits
    lines = ["Sound recordings used for the Curator's Gallery (cut and level-matched by Tools/make_curator_sfx.py).", "",
             "CC BY 4.0 (attribution required: https://creativecommons.org/licenses/by/4.0/):"]
    for k, (title, who, lic, url) in SOURCES.items():
        if lic.startswith("CC BY"): lines.append("  \"%s\" by %s, %s, %s" % (title, who, lic, url))
    lines += ["", "Public domain (no attribution required; credited anyway):"]
    for k, (title, who, lic, url) in SOURCES.items():
        if lic == "Public domain": lines.append("  \"%s\" by %s, %s" % (title, who, url))
    lines += ["", "Not yet recorded (synthesised placeholders in the game): match strike, candle blown out, clothing movement.", "",
              "Cuts:"] + report
    with open(os.path.join(OUT, "CREDITS.txt"), "w") as f: f.write("\n".join(lines) + "\n")
    print("\n".join(report))


if __name__ == "__main__":
    main()
