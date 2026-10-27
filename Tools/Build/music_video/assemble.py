#!/usr/bin/env python3
"""Lay the recorded shots on the song and write the MP4 (plus a contact sheet to read without playing it).

    python3 Tools/Build/music_video/assemble.py PLAN.json [--frames DIR] [--out video.mp4] [--captions] [--height 720]

Each shot's frames carry the game time they were drawn at (frames.csv), so a frame is held for
exactly the game time until the next one, whatever the wall time the software renderer took.
The audio is the plan's segment of the song, cut at the first shot's t0 and the last shot's t1.
A shot that came back short is padded on its last frame (and reported), never stretched.
The contact sheet (first / middle / last frame of every shot, labelled) is what a session looks
at first; the MP4 is what the person watches.
"""
import argparse
import csv
import json
import os
import shutil
import subprocess
import sys

FFMPEG = shutil.which("ffmpeg") or "ffmpeg"


def shot_frames(d):
    p = os.path.join(d, "frames.csv")
    if not os.path.exists(p):
        return []
    rows = list(csv.DictReader(open(p)))
    return [(os.path.join(d, r["file"]), float(r["time"])) for r in rows if os.path.exists(os.path.join(d, r["file"]))]


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("plan")
    ap.add_argument("--frames", help="frames root (default <plan dir>/frames)")
    ap.add_argument("--out", help="output MP4 (default <plan dir>/video.mp4)")
    ap.add_argument("--captions", action="store_true", help="burn the shot name and beat features into the picture (review cuts)")
    ap.add_argument("--height", type=int, default=900)
    a = ap.parse_args(argv)
    plan = json.load(open(a.plan))
    pdir = os.path.dirname(os.path.abspath(a.plan))
    root = a.frames or os.path.join(pdir, "frames")
    out = a.out or os.path.join(pdir, "video.mp4")
    fps = plan.get("fps", 30)
    lines, t, report, sheet = ["ffconcat version 1.0"], 0.0, [], []
    for shot in plan["shots"]:
        want = shot["t1"] - shot["t0"]
        frames = shot_frames(os.path.join(root, shot["name"]))
        if not frames:
            report.append(f"{shot['name']}: NO FRAMES (black for {want:.2f} s)")
            continue
        times = [ft for _, ft in frames]
        steps = [b - c for c, b in zip(times, times[1:])]
        steps.append(sorted(steps)[len(steps) // 2] if steps else 1.0 / fps)
        got = sum(steps)
        pad = max(0.0, want - got)
        for i, ((f, _), dt) in enumerate(zip(frames, steps)):
            if i == len(frames) - 1:
                dt += pad
            lines.append(f"file '{f}'")
            lines.append(f"duration {dt:.6f}")
        sheet.append((shot, [frames[0][0], frames[len(frames) // 2][0], frames[-1][0]]))
        t += want
        report.append(f"{shot['name']}: {len(frames)} frames, {got:.2f} s of {want:.2f} s{' (padded %.2f s on the last frame)' % pad if pad > 0.05 else ''}")
    lines.append(lines[-2])
    lst = os.path.join(pdir, "concat.txt")
    open(lst, "w").write("\n".join(lines) + "\n")
    t0 = plan["shots"][0]["t0"]
    dur = plan["shots"][-1]["t1"] - t0
    vf = [f"fps={fps}", f"scale=-2:{a.height}:flags=lanczos", "format=yuv420p"]
    if a.captions:
        # one drawtext per shot, enabled over its video time span
        at = 0.0
        for shot in plan["shots"]:
            d = shot["t1"] - shot["t0"]
            label = f"{shot['name']}  I{shot.get('intensity', '?')}  E{shot['features']['energy']:.2f} on{shot['features']['onset_density']:.2f} b{shot['features']['brightness']:.2f}"
            label = label.replace(":", "\\:").replace("'", "")
            vf.append(f"drawtext=text='{label}':x=20:y=h-40:fontsize=22:fontcolor=white:box=1:boxcolor=black@0.5:enable='between(t,{at:.3f},{at + d:.3f})'")
            at += d
    cmd = [FFMPEG, "-y", "-loglevel", "error", "-f", "concat", "-safe", "0", "-i", lst,
           "-ss", f"{t0:.3f}", "-t", f"{dur:.3f}", "-i", plan["song"],
           "-vf", ",".join(vf), "-c:v", "libx264", "-preset", "medium", "-crf", "20",
           "-c:a", "aac", "-b:a", "192k", "-shortest", "-movflags", "+faststart", out]
    subprocess.run(cmd, check=True)
    # contact sheet: 3 frames per shot, labelled
    try:
        from PIL import Image, ImageDraw
        tiles = []
        for shot, files in sheet:
            for f in files:
                im = Image.open(f).convert("RGB"); im.thumbnail((480, 270)); tiles.append((shot["name"], im))
        if tiles:
            cols = 3; rows = (len(tiles) + cols - 1) // cols
            W, H = 480, 270 + 24
            sheet_im = Image.new("RGB", (cols * W, rows * H), (12, 12, 20))
            dr = ImageDraw.Draw(sheet_im)
            for i, (name, im) in enumerate(tiles):
                x, y = (i % cols) * W, (i // cols) * H
                sheet_im.paste(im, (x, y + 24))
                dr.text((x + 6, y + 5), f"{name}  [{['first', 'middle', 'last'][i % 3]}]", fill=(230, 230, 240))
            sp = os.path.splitext(out)[0] + "_sheet.png"
            sheet_im.save(sp)
            report.append(f"contact sheet -> {sp}")
    except Exception as e:  # the sheet is a convenience
        report.append(f"no contact sheet: {e}")
    report.append(f"video {dur:.2f} s -> {out}")
    print("\n".join(report))
    return 0


if __name__ == "__main__":
    sys.exit(main())
