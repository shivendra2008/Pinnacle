#!/usr/bin/env python3
"""Frame-time statistics from PlayRoute runs (frames.csv), cold entry separated from play.

    python3 Tools/route_timing.py <run dir> [<run dir> ...]

Groups: market  - entry | steady (bhaskar, walk, approach, talk, lookback, reddy, card) | suv (suv, choice, outcome)
        cafe    - entry | traversal (cafe_approach, cafe_street)   (the fixed-view stills and the owner wait are left out)
Prints median, p95, p99, worst, frames over 50 ms and the count, per group and run. Runs named like <label>_<n>
(market_base_1, market_base_2, ...) are then pooled per label: every frame of the label's runs together, with the
spread of the per-run medians and p95s, and the cold-entry moments (first frame shown, first control) per run.
Frame time is the wall clock between frames (what the player sees), not the GPU counter.
"""
import csv, math, os, re, sys
from collections import OrderedDict

GROUPS = {
    "market": [("entry", {"entry"}), ("steady", {"bhaskar", "walk", "approach", "talk", "lookback", "reddy", "card"}), ("suv", {"suv", "choice", "outcome"})],
    "cafe": [("entry", {"entry"}), ("traversal", {"cafe_approach", "cafe_street"})],
}


def pct(s, p):
    if not s: return float("nan")
    return s[min(len(s) - 1, max(0, int(math.ceil(len(s) * p)) - 1))]


def stats(v):
    s = sorted(v)
    if not s: return "no frames"
    return "%5d frames  median %6.1f  p95 %6.1f  p99 %6.1f  worst %7.1f ms  >50 ms %4d" % (len(s), pct(s, 0.5), pct(s, 0.95), pct(s, 0.99), s[-1], sum(1 for x in s if x > 50))


def moments(d):
    summ = os.path.join(d, "summary.txt")
    if not os.path.exists(summ): return {}
    for line in open(summ):
        if line.startswith("Moments"):
            return {k: float(v) for k, v in re.findall(r"(\w+)=([0-9.]+)", line)}
    return {}


def main():
    pooled = OrderedDict()      # label -> {"kind", "groups": {name: [frames]}, "runs": {name: [(median, p95)]}, "moments": [dict]}
    for d in sys.argv[1:]:
        f = os.path.join(d, "frames.csv")
        if not os.path.exists(f): print(d, ": no frames.csv"); continue
        rows = list(csv.DictReader(open(f)))
        secs = {r["section"] for r in rows}
        kind = "market" if "walk" in secs or "talk" in secs else "cafe" if any(s.startswith("cafe") for s in secs) else None
        print("==", d, "(%s)" % kind)
        if kind is None: continue
        label = re.sub(r"_\d+$", "", os.path.basename(os.path.normpath(d)))
        p = pooled.setdefault(label, {"kind": kind, "groups": OrderedDict(), "runs": OrderedDict(), "moments": []})
        for name, want in GROUPS[kind]:
            v = [float(r["frame_ms"]) for r in rows if r["section"] in want]
            print("   %-10s %s" % (name, stats(v)))
            p["groups"].setdefault(name, []).extend(v)
            s = sorted(v)
            if s: p["runs"].setdefault(name, []).append((pct(s, 0.5), pct(s, 0.95)))
        m = moments(d)
        p["moments"].append(m)
        if m: print("   moments    " + "  ".join("%s %.2f s" % (k, v) for k, v in m.items()))
    if any(len(p["moments"]) > 1 for p in pooled.values()):
        print("\n== pooled per label (all runs' frames together)")
        for label, p in pooled.items():
            n = len(p["moments"])
            print("%s (%d runs)" % (label, n))
            for name, v in p["groups"].items():
                runs = p["runs"].get(name, [])
                spread = ""
                if runs:
                    spread = "   per run: median %.1f-%.1f, p95 %.1f-%.1f" % (min(r[0] for r in runs), max(r[0] for r in runs), min(r[1] for r in runs), max(r[1] for r in runs))
                print("   %-10s %s%s" % (name, stats(v), spread))
            fc = [m.get("first_control") for m in p["moments"] if m.get("first_control")]
            fv = [m.get("first_visible") for m in p["moments"] if m.get("first_visible")]
            if fc: print("   cold entry: first frame shown %s s, control %s s" % (", ".join("%.2f" % x for x in fv), ", ".join("%.2f" % x for x in fc)))


if __name__ == "__main__":
    main()
