#!/usr/bin/env python3
"""Turns a PlayRoute capture (Assets/Scripts/Playtest/PlayRoute.cs, PINNACLE_ROUTE_CAPTURE=1) into footage.

    python3 Tools/route_video.py <capture dir> [--name NAME] [--sheet t1,t2,...] [--clip NAME:t0:t1 ...] [--keep-frames]

The capture dir holds frames/f_00000.jpg... (30 fps, UI included), route.wav (the mix rendered offline in step with
the frames) and sections.srt (which part of the route each stretch is). Writes next to them:
  <name>.mp4              H.264 + AAC, the route sections as a subtitle track
  <name>_<clip>.mp4       excerpts (--clip window_pan:12.5:31), cut from the full file
  <name>_sheet.jpg        a contact sheet at the given times (seconds)
Frames are deleted after encoding unless --keep-frames.
"""
import glob, os, shutil, subprocess, sys


def run(cmd):
    r = subprocess.run(cmd, capture_output=True, text=True)
    if r.returncode != 0:
        sys.stderr.write(r.stderr[-2000:])
    return r


def main():
    if len(sys.argv) < 2:
        print(__doc__); sys.exit(1)
    cap = sys.argv[1]
    args = sys.argv[2:]
    name = os.path.basename(os.path.normpath(cap))
    sheet, clips, keep = [], [], False
    i = 0
    while i < len(args):
        a = args[i]
        if a == "--name": name = args[i + 1]; i += 2; continue
        if a == "--sheet": sheet = [float(t) for t in args[i + 1].split(",") if t]; i += 2; continue
        if a == "--clip": clips.append(args[i + 1]); i += 2; continue
        if a == "--keep-frames": keep = True; i += 1; continue
        i += 1
    frames = sorted(glob.glob(os.path.join(cap, "frames", "f_*.jpg")))
    out = os.path.join(cap, name + ".mp4")
    if not frames and not os.path.exists(out):
        print("no frames in", cap); sys.exit(1)
    wav = os.path.join(cap, "route.wav")
    srt = os.path.join(cap, "sections.srt")
    cmd = ["ffmpeg", "-y", "-loglevel", "error", "-framerate", "30", "-i", os.path.join(cap, "frames", "f_%05d.jpg")]
    if os.path.exists(wav): cmd += ["-i", wav]
    if os.path.exists(srt): cmd += ["-i", srt]
    cmd += ["-map", "0:v"]
    if os.path.exists(wav): cmd += ["-map", "1:a", "-c:a", "aac", "-b:a", "160k"]
    if os.path.exists(srt): cmd += ["-map", "%d:s" % (2 if os.path.exists(wav) else 1), "-c:s", "mov_text"]
    cmd += ["-c:v", "libx264", "-preset", "medium", "-crf", "21", "-pix_fmt", "yuv420p", "-movflags", "+faststart", out]
    if frames:
        if run(cmd).returncode != 0:
            sys.exit(1)
        print("wrote", out, "(%d frames, %.1f s)" % (len(frames), len(frames) / 30.0))
    for c in clips:
        n, t0, t1 = c.split(":")
        co = os.path.join(cap, "%s_%s.mp4" % (name, n))
        run(["ffmpeg", "-y", "-loglevel", "error", "-ss", t0, "-to", t1, "-i", out, "-c:v", "libx264", "-crf", "20", "-pix_fmt", "yuv420p",
             "-c:a", "aac", "-b:a", "160k", "-movflags", "+faststart", co])
        print("wrote", co)
    if sheet:
        tmp = os.path.join(cap, "_sheet")
        os.makedirs(tmp, exist_ok=True)
        for k, t in enumerate(sheet):
            # (the Homebrew ffmpeg here has no drawtext: the sheet is in the order of the times given)
            run(["ffmpeg", "-y", "-loglevel", "error", "-ss", str(t), "-i", out, "-frames:v", "1", "-update", "1", "-vf", "scale=640:-1",
                 os.path.join(tmp, "s_%02d.jpg" % k)])
        cols = 3
        rows = (len(sheet) + cols - 1) // cols
        so = os.path.join(cap, name + "_sheet.jpg")
        run(["ffmpeg", "-y", "-loglevel", "error", "-pattern_type", "glob", "-i", os.path.join(tmp, "s_*.jpg"),
             "-vf", "tile=%dx%d:padding=4:color=black" % (cols, rows), "-frames:v", "1", "-update", "1", so])
        shutil.rmtree(tmp, ignore_errors=True)
        print("wrote", so)
    if not keep:
        shutil.rmtree(os.path.join(cap, "frames"), ignore_errors=True)


if __name__ == "__main__":
    main()
