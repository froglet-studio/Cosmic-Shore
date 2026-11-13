#!/usr/bin/env python3
"""The Ark screen's backdrops as the game plays them (Shombith, 2026-11-12: "do exactly how you showed me on Frogma, no
need to use the actual model, it can be a video or gif of this high quality"): the Frogma renders (r67_ark_tabs_render.py,
r67_comp_converter.py) graded with Rev 61 C's grade (r67_grade.py), upscaled to 1920x1080 and blended from 10 to 30 fps,
written as H.264 into Assets/_Graphics/Video/Ark/ (VideoClip assets; ArkBackdropConfigSO names them).

    python3 r67_ark_backdrop_videos.py <render_out> <converter_out> <artifacts_out>
      squads:     <render_out>/squads_<Set>[_<Dom>]_loop          -> squads_<Set>_<Dom>_loop.mp4   (9)
      converter:  <converter_out>/converter_Nocturne_<run>        -> converter_Nocturne_<run>.mp4 (loop, convert, done)
      artifacts:  <artifacts_out>/art_ab_Nocturne_loop (one still) -> artifacts_Nocturne_loop.mp4 (2 s)
"""
import os
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import r67_grade as grade  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
DST = os.path.join(REPO, "Assets", "_Graphics", "Video", "Ark")
VF = "hqdn3d=2:2:8:8,scale=1920:1080:flags=lanczos,framerate=fps=30"   # the grade's per-frame grain, settled: a fifth of the bytes


def graded(src_dir):
    tmp = tempfile.mkdtemp()
    frames = sorted(f for f in os.listdir(src_dir) if f.endswith(".png"))
    for i, f in enumerate(frames):
        tagged = os.path.join(tmp, f"lobby_pads_{i:04d}.png")     # the grade lifts frames named as the hangar's were
        os.symlink(os.path.abspath(os.path.join(src_dir, f)), tagged)
        grade.grade(tagged, os.path.join(tmp, f"lobby_pads_{i:04d}.jpg"))
    return tmp, len(frames)


def encode(src_dir, name, still_seconds=0.0):
    tmp, n = graded(src_dir)
    out = os.path.join(DST, name + ".mp4")
    if n == 1:
        cmd = ["ffmpeg", "-loglevel", "error", "-y", "-loop", "1", "-t", str(still_seconds), "-framerate", "30",
               "-i", os.path.join(tmp, "lobby_pads_0000.jpg"), "-vf", "scale=1920:1080:flags=lanczos"]
    else:
        cmd = ["ffmpeg", "-loglevel", "error", "-y", "-framerate", "10", "-i", os.path.join(tmp, "lobby_pads_%04d.jpg"), "-vf", VF]
    cmd += ["-c:v", "libx264", "-preset", "slow", "-crf", "24", "-pix_fmt", "yuv420p", "-movflags", "+faststart", "-an", out]
    subprocess.run(cmd, check=True)
    print(f"{name}.mp4  {n} frames  {os.path.getsize(out) // 1024} KB", flush=True)


def main():
    render, conv, art = sys.argv[1:4]
    os.makedirs(DST, exist_ok=True)
    for s in ("Nocturne", "Photic", "DartFrog"):
        for d in ("Jade", "Ruby", "Gold"):
            src = os.path.join(render, f"squads_{s}{'' if d == 'Jade' else '_' + d}_loop")
            encode(src, f"squads_{s}_{d}_loop")
    for run in ("loop", "convert", "done"):
        encode(os.path.join(conv, f"converter_Nocturne_{run}"), f"converter_Nocturne_{run}")
    encode(os.path.join(art, "art_ab_Nocturne_loop"), "artifacts_Nocturne_loop", still_seconds=2.0)


if __name__ == "__main__":
    main()
