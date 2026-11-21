#!/usr/bin/env python3
"""Turns a CuratorCapture segment (Assets/Scripts/Playtest/CuratorCapture.cs) into footage and numbers.

    python3 Tools/review_capture.py <segment dir> [--keep-frames] [--sheet t1,t2,...] [--slow t0:t1]

Writes next to the frames:
  <seg>.mp4            30 fps H.264 + AAC, the letterbox bars the game draws in cutscenes, subtitles as a text track
  <seg>_slow_*.mp4     a stretch at quarter speed (with --slow), for hands, feet and faces
  <seg>_sheet.jpg      a contact sheet (with --sheet, times in seconds)
  <seg>_audio.png      waveform + spectrogram of the whole mix
  <seg>_metrics.txt    what the tracking data says:
     feet     - how far each foot slides while it is planted (a planted foot should not move at all)
     hands    - how much the distance between the holding hand and its prop changes (contact should hold)
     body     - teleports (jumps > 0.25 m in a frame), turning on the spot without stepping
     camera   - cuts, shot lengths, focal-length (FOV) changes inside a shot
     audio    - integrated loudness, loudness range, true peak (EBU R128)
Frames are deleted after encoding unless --keep-frames (they are large).
"""
import csv, glob, math, os, re, subprocess, sys


def run(cmd):
    return subprocess.run(cmd, capture_output=True, text=True)


def load_track(path):
    """Rows as dicts. Older recordings wrote the subtitle text unquoted, so a line with commas spread over several
    fields: the extra fields are folded back into "line" (it is the only free-text column)."""
    rows = []
    with open(path) as f:
        reader = csv.reader(f)
        head = next(reader)
        li = head.index("line")
        for rec in reader:
            extra = len(rec) - len(head)
            if extra > 0:
                rec = rec[:li] + [",".join(rec[li:li + extra + 1])] + rec[li + extra + 1:]
            rows.append(dict(zip(head, rec)))
    return rows


def vec(r, k):
    try:
        return (float(r[k + "x"]), float(r[k + "y"]), float(r[k + "z"]))
    except (ValueError, KeyError, TypeError):
        return None


def dist(a, b):
    return math.sqrt(sum((a[i] - b[i]) ** 2 for i in range(3)))


def hdist(a, b):
    return math.sqrt((a[0] - b[0]) ** 2 + (a[2] - b[2]) ** 2)


def metrics(rows, fps=30.0):
    out = []
    n = len(rows)
    out.append("frames %d (%.1f s)" % (n, n / fps))
    # ---- feet. With the hero layer's own flags (column "hero": speed walkBlend phase pelvisDrop footL footR, each foot
    # "P0.9" planted / "S" stepping / "-" free), slip = movement of a foot while it is planted at full weight. Without
    # them (the old code), a foot within 1.5 cm of its lowest height in the surrounding 2 s counts as planted.
    root = [vec(r, "root") for r in rows]
    jump = [i for i in range(1, n) if root[i] and root[i - 1] and hdist(root[i], root[i - 1]) > 0.25]     # teleports: not the feet's fault
    has_flags = any(r.get("hero") for r in rows)
    for side, key, col in (("left", "footL", 4), ("right", "footR", 5)):
        ys = [vec(r, key) for r in rows]
        slide_total, slide_frames, planted_frames, worst = 0.0, 0, 0, 0.0
        for i in range(1, n):
            a, b = ys[i - 1], ys[i]
            if a is None or b is None or i in jump:
                continue
            if has_flags:
                f0, f1 = (rows[i - 1].get("hero") or "").split(), (rows[i].get("hero") or "").split()
                planted = len(f0) > col and len(f1) > col and f0[col].startswith("P") and f1[col].startswith("P") and float(f1[col][1:]) >= 0.9
            else:
                lo = min(p[1] for p in ys[max(0, i - 30):min(n, i + 30)] if p is not None)
                planted = a[1] < lo + 0.015 and b[1] < lo + 0.015
            if planted:
                planted_frames += 1
                d = hdist(a, b)
                if d > 0.002:                       # under 2 mm per frame is noise
                    slide_total += d; slide_frames += 1; worst = max(worst, d * fps)
        out.append("feet  %-5s planted %5d frames, slides in %4d of them (%.0f%%), %.2f m slid in all, worst %.2f m/s%s"
                   % (side, planted_frames, slide_frames, 100.0 * slide_frames / max(1, planted_frames), slide_total, worst, "  [hero flags]" if has_flags else ""))
    if has_flags:
        drops = [float(r["hero"].split()[3]) for r in rows if r.get("hero") and len(r["hero"].split()) > 3]
        out.append("pelvis correction: mean %.1f mm, max %.1f mm" % (1000 * sum(drops) / max(1, len(drops)), 1000 * max(drops) if drops else 0))
    # ---- hands on props: the change in hand-prop distance while it is held (near: < 0.35 m)
    for hand, prop in (("handL", "candle"), ("handR", "book")):
        ds = []
        for r in rows:
            h, p = vec(r, hand), vec(r, prop)
            if h is None or p is None:
                ds.append(None); continue
            ds.append(dist(h, p))
        held = [d for d in ds if d is not None and d < 0.35]
        if held:
            jumps = [abs(ds[i] - ds[i - 1]) for i in range(1, n) if ds[i] is not None and ds[i - 1] is not None and ds[i] < 0.35 and ds[i - 1] < 0.35]
            out.append("hands %s-%s held %d frames: distance %.3f..%.3f m (spread %.3f m), largest change in one frame %.3f m"
                       % (hand, prop, len(held), min(held), max(held), max(held) - min(held), max(jumps) if jumps else 0))
        # pops: the prop jumps (> 5 cm in a frame) while the hand near it hardly moves. Hand jumps: a one-frame step
        # over 6 cm that is also 3x the steps either side of it - a switched IK target, not a fast but smooth reach
        # (those ramp up and down over several frames; their peak speed is reported separately)
        pops, hand_jumps = [], []
        steps = [None] * n
        for i in range(1, n):
            h0, h1 = vec(rows[i - 1], hand), vec(rows[i], hand)
            if None not in (h0, h1):
                steps[i] = dist(h0, h1)
        for i in range(1, n):
            hm = steps[i]
            if hm is None:
                continue
            nb = [steps[j] for j in (i - 1, i + 1) if 0 < j < n and steps[j] is not None]
            if hm > 0.06 and nb and hm > 3 * max(nb):
                hand_jumps.append("%.1fs" % (i / fps))
            p0, p1, h1 = vec(rows[i - 1], prop), vec(rows[i], prop), vec(rows[i], hand)
            if None not in (p0, p1) and dist(h1, p1) < 0.45:
                pm = dist(p0, p1)
                if pm > 0.05 and abs(pm - hm) > 0.04:
                    pops.append("%.1fs" % (i / fps))
        peak = max([x for x in steps if x is not None] or [0]) * fps
        out.append("hands %s-%s pops (prop jumps > 5 cm without the hand): %d %s; hand jumps (one-frame discontinuities): %d %s; peak hand speed %.1f m/s"
                   % (hand, prop, len(pops), " ".join(pops[:6]), len(hand_jumps), " ".join(hand_jumps[:6]), peak))
    # ---- body
    yaw = [float(r["yaw"]) if r["yaw"] else None for r in rows]
    tele = sum(1 for i in range(1, n) if root[i] and root[i - 1] and hdist(root[i], root[i - 1]) > 0.25)
    spin = 0
    for i in range(1, n):
        if yaw[i] is None or yaw[i - 1] is None or not root[i] or not root[i - 1]:
            continue
        dy = abs((yaw[i] - yaw[i - 1] + 180) % 360 - 180)
        if dy * fps > 40 and hdist(root[i], root[i - 1]) * fps < 0.1:
            spin += 1
    out.append("body  teleports %d, frames turning on the spot faster than 40 deg/s: %d" % (tele, spin))
    # ---- camera: cuts are jumps > 0.4 m in a frame, or a lens change in one frame (a cut between two set-ups
    # close together; a zoom changes the field of view by far less than 0.3 degrees per frame)
    cam = [vec(r, "cam") for r in rows]
    fov = [float(r["fov"]) if r["fov"] else None for r in rows]
    cuts = [0]
    for i in range(1, n):
        moved = cam[i] and cam[i - 1] and dist(cam[i], cam[i - 1]) > 0.4
        relens = fov[i] is not None and fov[i - 1] is not None and abs(fov[i] - fov[i - 1]) > 0.3
        if moved or relens:
            cuts.append(i)
    cuts.append(n)
    # a cut on the very last frame (the fade to the next scene) leaves a zero-length piece: not a shot
    cuts = [c for k, c in enumerate(cuts) if k == 0 or c - cuts[k - 1] > 1]
    lens = [(cuts[k + 1] - cuts[k]) / fps for k in range(len(cuts) - 1)]
    zooming = 0
    for k in range(len(cuts) - 1):
        f = [fov[i] for i in range(cuts[k], cuts[k + 1]) if fov[i] is not None]
        if f and max(f) - min(f) > 0.5:
            zooming += 1
    out.append("camera %d shots, length median %.1f s, shortest %.1f s, longest %.1f s; shots with a focal-length change: %d"
               % (len(lens), sorted(lens)[len(lens) // 2] if lens else 0, min(lens) if lens else 0, max(lens) if lens else 0, zooming))
    return out


def loudness(wav):
    r = run(["ffmpeg", "-hide_banner", "-nostats", "-i", wav, "-filter_complex", "ebur128=peak=true", "-f", "null", "-"])
    txt = r.stderr
    i = re.findall(r"I:\s+(-?[\d.]+) LUFS", txt)
    lra = re.findall(r"LRA:\s+([\d.]+) LU", txt)
    tp = re.findall(r"Peak:\s+(-?[\d.]+) dBFS", txt)
    return "audio integrated %s LUFS, range %s LU, true peak %s dBFS" % (i[-1] if i else "?", lra[-1] if lra else "?", tp[-1] if tp else "?")


def main():
    seg = os.path.abspath(sys.argv[1])
    name = os.path.basename(seg)
    keep = "--keep-frames" in sys.argv
    sheet = None; slow = []
    for k, a in enumerate(sys.argv):
        if a == "--sheet": sheet = [float(x) for x in sys.argv[k + 1].split(",")]
        if a == "--slow": slow.append(sys.argv[k + 1])
    wav = os.path.join(seg, name + ".wav")
    frames = sorted(glob.glob(os.path.join(seg, "f_*.jpg")))
    mp4 = os.path.join(seg, name + ".mp4")
    if frames:
        srt = os.path.join(seg, "lines.srt")
        bars = "drawbox=x=0:y=0:w=iw:h=ih*0.085:color=black:t=fill,drawbox=x=0:y=ih-ih*0.085:w=iw:h=ih*0.085:color=black:t=fill"
        cmd = ["ffmpeg", "-y", "-loglevel", "error", "-framerate", "30", "-i", os.path.join(seg, "f_%05d.jpg"), "-i", wav]
        if os.path.exists(srt) and os.path.getsize(srt) > 0:
            cmd += ["-i", srt, "-map", "0:v", "-map", "1:a", "-map", "2:s", "-c:s", "mov_text", "-metadata:s:s:0", "language=eng"]
        cmd += ["-vf", bars, "-c:v", "libx264", "-preset", "medium", "-crf", "20", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "192k", mp4]
        r = run(cmd)
        if r.returncode != 0: print(r.stderr); sys.exit(1)
        for s in slow:
            a, b = [float(x) for x in s.split(":")]
            out = os.path.join(seg, "%s_slow_%05.1f.mp4" % (name, a))
            run(["ffmpeg", "-y", "-loglevel", "error", "-ss", str(a), "-t", str(b - a), "-i", mp4, "-an", "-vf", "setpts=4.0*PTS", "-r", "30",
                 "-c:v", "libx264", "-crf", "20", "-pix_fmt", "yuv420p", out])
        if sheet:
            ins = []
            for t in sheet:
                p = os.path.join(seg, "_sheet_%07.2f.jpg" % t)
                run(["ffmpeg", "-y", "-loglevel", "error", "-ss", str(t), "-i", mp4, "-frames:v", "1", "-vf", "scale=480:-1", p])
                ins.append(p)
            cols = 4
            rowsn = (len(ins) + cols - 1) // cols
            run(["ffmpeg", "-y", "-loglevel", "error", "-pattern_type", "glob", "-i", os.path.join(seg, "_sheet_*.jpg"),
                 "-vf", "tile=%dx%d:padding=4:color=gray" % (cols, rowsn), "-frames:v", "1", os.path.join(seg, name + "_sheet.jpg")])
            for p in ins:
                if os.path.exists(p): os.remove(p)
        if not keep:
            for f in frames: os.remove(f)
    if os.path.exists(wav):
        run(["ffmpeg", "-y", "-loglevel", "error", "-i", wav, "-filter_complex",
             "[0:a]showwavespic=s=1600x200:split_channels=0:colors=0xd0b080[w];[0:a]showspectrumpic=s=1600x300:legend=0:scale=log[s];[w][s]vstack",
             "-frames:v", "1", os.path.join(seg, name + "_audio.png")])
    lines = metrics(load_track(os.path.join(seg, "track.csv")))
    if os.path.exists(wav): lines.append(loudness(wav))
    with open(os.path.join(seg, name + "_metrics.txt"), "w") as f: f.write("\n".join(lines) + "\n")
    print("\n".join(lines))


if __name__ == "__main__":
    main()
