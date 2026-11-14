#!/usr/bin/env python3
"""The PILOTS tab's backdrops as the game plays them (Rev 99 C, picked by Shombith 2026-11-13; Docs/MENU_REDESIGN_B.md
section 191): the crew PDA lying on the hangar's workbench, and the same PDA held up square to the view. Both are
Blender stills (r109_ark_pda_render.py: `pda_c` with PDA_KIND=nautilus, and the same with PDA_HELD=1 PDA_HELD_DIST=0.545;
Rev 109 B, picked by Shombith 2026-11-13; r99_ark_pilots_render.py drew the Rev 99 C slab), graded with Rev 61 C's grade
(r67_grade.py, the hangar's lift, as the Squads clips are), scaled to 1920x1080 and written as H.264 into
Assets/_Graphics/Video/Ark/ (ArkBackdropConfigSO names them; author_ark_backdrop_assets.py writes the table):

    bench_Nocturne_loop.mp4     the PDA lying on the bench (2 s, one still)
    bench_Nocturne_pickup.mp4   picked up (0.5 s): the view closes in on the PDA as the held still dissolves over it
    bench_Nocturne_held.mp4     held up (2 s, one still); the game lays the crew on its glass
    bench_Nocturne_putdown.mp4  laid down: the pick-up played backwards

    python3 r99_pilots_bench_videos.py <rest.png> <held.png> [out_dir]
"""
import os
import subprocess
import sys
import tempfile

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import r67_grade as grade  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
DST = os.path.join(REPO, "Assets", "_Graphics", "Video", "Ark")
W, H, FPS = 1920, 1080, 30
MOVE_FRAMES = 15                       # the pick-up, 0.5 s
REST_GLASS = (944.9, 777.4)            # the display's centre lying down (the nautilus's sheet, Rev 109 B), at 1920 x 1080 (author_menu_ark.PDA_REST_CORNERS)
HELD_GLASS = (960.0, 598.5)            # and held up (GLASS_X/Y/W/H)
ZOOM = 2.4                             # how far the view closes in on the lying PDA by the end of the move


def graded(src, tmp, tag):
    tagged = os.path.join(tmp, f"lobby_pads_{tag}.png")       # the grade lifts frames named as the hangar's were
    os.symlink(os.path.abspath(src), tagged)
    out = os.path.join(tmp, f"lobby_pads_{tag}.jpg")
    grade.grade(tagged, out)
    return Image.open(out).convert("RGB").resize((W, H), Image.LANCZOS)


def zoomed(im, centre, z, to):
    """`im` scaled by `z` about `centre`, with `centre` carried to `to` (a camera closing in)."""
    cx, cy = centre
    tx, ty = to
    # output pixel p comes from (p - to) / z + centre
    return im.transform((W, H), Image.AFFINE, (1 / z, 0, cx - tx / z, 0, 1 / z, cy - ty / z), Image.BICUBIC)


def ease(t):
    return t * t * (3 - 2 * t)


def move_frames(rest, held):
    frames = []
    for k in range(MOVE_FRAMES):
        t = ease(k / (MOVE_FRAMES - 1))
        z = 1 + (ZOOM - 1) * t
        to = tuple(REST_GLASS[i] + (HELD_GLASS[i] - REST_GLASS[i]) * t for i in range(2))
        a = np.asarray(zoomed(rest, REST_GLASS, z, to)).astype(np.float32)
        hz = 0.82 + 0.18 * t                                           # the held PDA rises into place as it appears
        b = np.asarray(zoomed(held, HELD_GLASS, hz, HELD_GLASS)).astype(np.float32)
        mix = np.clip((t - 0.25) / 0.6, 0, 1)
        frames.append(Image.fromarray((a * (1 - mix) + b * mix).astype(np.uint8)))
    return frames


def encode(frames, name, seconds=None):
    tmp = tempfile.mkdtemp()
    out = os.path.join(DST, name + ".mp4")
    if seconds:
        frames[0].save(os.path.join(tmp, "still.jpg"), quality=92)
        cmd = ["ffmpeg", "-loglevel", "error", "-y", "-loop", "1", "-t", str(seconds), "-framerate", str(FPS),
               "-i", os.path.join(tmp, "still.jpg")]
    else:
        for i, f in enumerate(frames):
            f.save(os.path.join(tmp, f"f_{i:04d}.jpg"), quality=92)
        cmd = ["ffmpeg", "-loglevel", "error", "-y", "-framerate", str(FPS), "-i", os.path.join(tmp, "f_%04d.jpg")]
    cmd += ["-c:v", "libx264", "-preset", "slow", "-crf", "24", "-pix_fmt", "yuv420p", "-movflags", "+faststart", "-an", out]
    subprocess.run(cmd, check=True)
    print(f"{name}.mp4  {len(frames) if not seconds else 1} frame(s)  {os.path.getsize(out) // 1024} KB", flush=True)


def main():
    global DST
    rest_png, held_png = sys.argv[1:3]
    if len(sys.argv) > 3:
        DST = sys.argv[3]
    os.makedirs(DST, exist_ok=True)
    tmp = tempfile.mkdtemp()
    rest, held = graded(rest_png, tmp, "rest"), graded(held_png, tmp, "held")
    encode([rest], "bench_Nocturne_loop", seconds=2.0)
    encode([held], "bench_Nocturne_held", seconds=2.0)
    move = move_frames(rest, held)
    encode(move, "bench_Nocturne_pickup")
    encode(move[::-1], "bench_Nocturne_putdown")


if __name__ == "__main__":
    main()
