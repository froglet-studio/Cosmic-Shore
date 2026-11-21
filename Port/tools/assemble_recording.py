"""Assemble recorded frame segments into one MP4 at true game speed.

Each segment is a --record directory with frames.csv (file, frame, game time). A frame is held
for the game time until the next frame, divided by the segment's speed-up. Prints chapter marks
(video seconds) for chosen game frames as JSON.
"""
import csv, json, os, subprocess, sys

# usage: assemble_recording.py REC_ROOT OUT.mp4 [FFMPEG]
# REC_ROOT holds the --record directories menu/, load/, fly/ (see tools/record_session.sh).
S = os.path.abspath(sys.argv[1])
OUT = sys.argv[2]
if len(sys.argv) > 3:
    FFMPEG = sys.argv[3]
else:
    import imageio_ffmpeg  # pip install imageio-ffmpeg: a static ffmpeg build
    FFMPEG = imageio_ffmpeg.get_ffmpeg_exe()

# (dir, first file index, speed-up, chapters {label: game frame})
SEGMENTS = [
    ("menu", 10, 1.0, {"Menu": 450, "Arcade": 700, "Launch panel": 840}),
    ("load", 0, 3.0, {"Loading Bloomrush": 1010}),
    ("fly", 0, 1.0, {"Flight": 2160}),
]

lines, chapters, t = ["ffconcat version 1.0"], [], 0.0
for d, first, speed, marks in SEGMENTS:
    rows = list(csv.DictReader(open(os.path.join(S, d, "frames.csv"))))[first:]
    if not rows:
        continue
    times = [float(r["time"]) for r in rows]
    steps = [b - a for a, b in zip(times, times[1:])] or [1 / 30]
    steps.append(sorted(steps)[len(steps) // 2])
    pending = dict(marks)
    for r, dt in zip(rows, steps):
        f = int(r["frame"])
        for label, mf in list(pending.items()):
            if f >= mf:
                chapters.append({"label": label, "t": round(t, 2)})
                del pending[label]
        dur = dt / speed
        lines.append(f"file '{os.path.join(S, d, r['file'])}'")
        lines.append(f"duration {dur:.5f}")
        t += dur
lines.append(lines[-2])  # concat demuxer: repeat the last file so its duration applies

lst = os.path.join(S, "concat.txt")
open(lst, "w").write("\n".join(lines) + "\n")
subprocess.run([FFMPEG, "-y", "-loglevel", "error", "-f", "concat", "-safe", "0", "-i", lst,
                "-vf", "fps=30,scale=1280:720:flags=lanczos,format=yuv420p",
                "-c:v", "libx264", "-preset", "slow", "-crf", "23", "-movflags", "+faststart", OUT],
               check=True)
print(json.dumps({"duration": round(t, 2), "chapters": chapters}))
