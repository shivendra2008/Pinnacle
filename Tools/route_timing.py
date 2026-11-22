#!/usr/bin/env python3
"""Frame-time statistics from PlayRoute runs (frames.csv), cold entry separated from play.

    python3 Tools/route_timing.py <run dir> [<run dir> ...]

Groups: market  - entry | steady (bhaskar, walk, approach, talk, lookback, reddy, card) | suv (suv, choice, outcome)
        cafe    - entry | traversal (cafe_approach, cafe_street)   (the fixed-view stills and the owner wait are left out)
Prints median, p95, p99, worst, frames over 50 ms and the count, per group and run.
"""
import csv, math, os, sys

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


def main():
    for d in sys.argv[1:]:
        f = os.path.join(d, "frames.csv")
        if not os.path.exists(f): print(d, ": no frames.csv"); continue
        rows = list(csv.DictReader(open(f)))
        secs = {r["section"] for r in rows}
        kind = "market" if "walk" in secs or "talk" in secs else "cafe" if any(s.startswith("cafe") for s in secs) else None
        print("==", d, "(%s)" % kind)
        if kind is None: continue
        for name, want in GROUPS[kind]:
            print("   %-10s %s" % (name, stats([float(r["frame_ms"]) for r in rows if r["section"] in want])))
        summ = os.path.join(d, "summary.txt")
        if os.path.exists(summ):
            for line in open(summ):
                if line.startswith("Moments"): print("   " + line.strip()[:200]); break


if __name__ == "__main__":
    main()
