#!/usr/bin/env python3
"""Per-section frame time for act5 route runs (Tools/timing_act5.sh): cold entry (request to control) apart from play.

    python3 Tools/act5_timing.py <run dir> [...]

For each run and pooled per build (base / after, from the directory name): the moments printed by the route, then per
section (shelter, upstairs, to_gallery, gallery, ending) the frame count, median, p95, p99, worst and frames over 50 ms
of the wall-clock frame time, and the mean triangles, shadow casters, visible skinned meshes and SetPass calls.
"""
import csv, math, os, re, sys
from collections import defaultdict

SECTIONS = ["entry", "shelter", "upstairs", "to_gallery", "gallery", "ending"]


def pct(v, p):
    if not v: return float("nan")
    v = sorted(v); return v[min(len(v) - 1, max(0, math.ceil(len(v) * p) - 1))]


def load(run):
    rows = defaultdict(list)
    with open(os.path.join(run, "frames.csv")) as f:
        for r in csv.DictReader(f):
            rows[r["section"]].append(r)
    moments = ""
    sp = os.path.join(run, "summary.txt")
    if os.path.exists(sp):
        for line in open(sp):
            if line.startswith("Moments"): moments = line.strip()
    return rows, moments


def stats(rs):
    ft = [float(r["frame_ms"]) for r in rs]
    def mean(k):
        vals = [float(r[k]) for r in rs if k in r and r[k] not in ("", "-1")]
        return sum(vals) / len(vals) if vals else float("nan")
    return (len(ft), pct(ft, 0.5), pct(ft, 0.95), pct(ft, 0.99), max(ft) if ft else float("nan"), sum(1 for x in ft if x > 50),
            mean("Triangles_Count"), mean("Shadow_Casters_Count"), mean("Visible_Skinned_Meshes_Count"), mean("SetPass_Calls_Count"))


def show(name, rows):
    print("  %-11s %6s %7s %7s %7s %7s %5s %9s %7s %5s %6s" % ("section", "frames", "median", "p95", "p99", "worst", ">50", "tris", "shadow", "skin", "setpass"))
    for s in SECTIONS:
        if not rows.get(s): continue
        n, m, p95, p99, w, h, tr, sc, sk, sp = stats(rows[s])
        print("  %-11s %6d %7.1f %7.1f %7.1f %7.1f %5d %9.0f %7.0f %5.1f %6.0f" % (s, n, m, p95, p99, w, h, tr, sc, sk, sp))


def main():
    pooled = defaultdict(lambda: defaultdict(list))
    for run in sys.argv[1:]:
        if not os.path.exists(os.path.join(run, "frames.csv")): continue
        rows, moments = load(run)
        print(os.path.basename(run.rstrip("/")), "|", moments)
        show(run, rows)
        kind = "after" if "_after_" in os.path.basename(run) else "base"
        for s, rs in rows.items(): pooled[kind][s].extend(rs)
    for kind in ("base", "after"):
        if kind in pooled:
            print("POOLED", kind)
            show(kind, pooled[kind])


if __name__ == "__main__":
    main()
