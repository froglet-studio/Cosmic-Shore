#!/usr/bin/env python3
"""Grade and encode the Ark tabs' frames: every <out>/<scene>_<set>_<run>/f_NNNN.png is graded with Rev 61 C's grade
(r61_ark_lobby_grade: AgX frames lifted as the hangar's cameras were, bloom, fringing, vignette, a fresh grain per frame)
and encoded as an animated WebP at 10 fps - looping for a loop, playing once for the conversion - plus an MP4 preview.

    python3 r67_assemble.py <out_dir> <dst_dir> [only_prefix]
"""
import os
import shutil
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import r67_grade as grade  # noqa: E402  (r61_ark_lobby_grade with the per-frame grain)

src, dst = sys.argv[1], sys.argv[2]
only = sys.argv[3] if len(sys.argv) > 3 else ""
os.makedirs(dst, exist_ok=True)
for run in sorted(os.listdir(src)):
    d = os.path.join(src, run)
    if not os.path.isdir(d) or not run.startswith(only):
        continue
    frames = sorted(f for f in os.listdir(d) if f.endswith(".png"))
    if not frames:
        continue
    tmp = tempfile.mkdtemp()
    for f in frames:
        # "_pads_" in the source's name: graded as Rev 61 C's camera was (the hangar's lift reads the name)
        tagged = os.path.join(tmp, f"lobby_pads_{f[2:6]}.png")
        os.symlink(os.path.abspath(os.path.join(d, f)), tagged)
        grade.grade(tagged, os.path.join(tmp, f"lobby_pads_{f[2:6]}.jpg"))
    once = run.endswith("_convert")
    webp = os.path.join(dst, f"{run}.webp")
    subprocess.run(["ffmpeg", "-loglevel", "error", "-y", "-framerate", "10", "-i", os.path.join(tmp, "lobby_pads_%04d.jpg"),
                    "-c:v", "libwebp", "-lossless", "0", "-q:v", "74", "-loop", "1" if once else "0", "-an", webp], check=True)
    subprocess.run(["ffmpeg", "-loglevel", "error", "-y", "-stream_loop", "0" if once else "2", "-framerate", "10", "-i",
                    os.path.join(tmp, "lobby_pads_%04d.jpg"), "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "24",
                    os.path.join(dst, f"{run}.mp4")], check=True)
    shutil.copy(os.path.join(tmp, "lobby_pads_0000.jpg"), os.path.join(dst, f"{run}_first.jpg"))
    shutil.rmtree(tmp)
    print("encoded", run, len(frames), os.path.getsize(webp) // 1024, "KB", flush=True)
