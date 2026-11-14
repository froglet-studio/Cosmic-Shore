#!/usr/bin/env python3
"""The Squads backdrops with EMPTY pads (Rev 110 built, Docs/MENU_REDESIGN_B.md section 202; Shombith, 2026-11-14, of Rev 110's
check: "Yeah lets build that"). The shipped clips bake the Dolphin, Sparrow and Serpent into the video, so changing a
seat's vessel changed nothing on screen. Now the hangar plays with its three pads empty and the game draws each seat's
real vessel on its pad, live (SquadVesselLayer).

The hangar is NOT re-rendered whole: the Voyage Board charts and the cell picture the first renders used are not in the
repo, and stand-ins would show. Only the box around the three hulls is re-rendered (r67_ark_tabs_render.py with
ARK_LINE=none, ARK_BORDER, the clips' own 32-frame loop and 10 samples), graded and blown up exactly as the clips were
(r67_ark_backdrop_videos.py), and laid into the shipped clip frame by frame through the hulls' mask (their union over the
loop, from an ARK_PASS=vessels render, widened and feathered). Where a side window sits behind a hull, the window is
filled from the clip around it instead of the stand-in picture.

    python3 r110_squads_empty_pads.py render  <work> <assets>    # Blender (bpy): the mask, then 3 domains x 3 sets
    python3 r110_squads_empty_pads.py comp    <work>             # the 9 clips into Assets/_Graphics/Video/Ark/
    python3 r110_squads_empty_pads.py preview <work> <out.jpg>   # frame 0 of each clip, before and after

<assets> as r67_ark_tabs_render.py wants it, with 32-frame maps (r67_map_frames.py <assets> 32 <domain>, all three).
"""
import os
import shutil
import subprocess
import sys
import tempfile

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
DST = os.path.join(REPO, "Assets", "_Graphics", "Video", "Ark")
RENDER = os.path.join(HERE, "r67_ark_tabs_render.py")
SETS = ("Nocturne", "Photic", "DartFrog")
DOMS = ("Jade", "Ruby", "Gold")
W, H, SAMPLES, LOOP = 960, 540, 10, 32                  # as the shipped clips were rendered (section 190)
BORDER = (0, 680, 57, 394)                              # the box around the hulls at 960 x 540 (x0, x1, y0, y1)
WINDOWS = ((0, 54, 55, 295), (610, 656, 85, 255))       # the side windows' boxes at 960 x 540: never the stand-in picture
GROW, FEATHER = 15, 8                                   # the mask: the hulls' union widened, then softened (960 px)


def run_dir(work, s, d):
    return os.path.join(work, "pads", f"squads_{s}{'' if d == 'Jade' else '_' + d}_loop")


def render(work, assets):
    env = dict(os.environ, ARK_LOOP=str(LOOP), COSMIC_SHORE_REPO=REPO)
    mask = os.path.join(work, "mask")
    subprocess.run([sys.executable, RENDER, mask, assets, "squads", "Nocturne", str(W), str(H), "4"], check=True,
                   env=dict(env, ARK_PASS="vessels", ARK_ONLY=",".join(str(f) for f in range(0, LOOP, 4))))
    for d in DOMS:
        subprocess.run([sys.executable, RENDER, os.path.join(work, "pads"), assets, "squads", ",".join(SETS),
                        str(W), str(H), str(SAMPLES)], check=True,
                       env=dict(env, ARK_LINE="none", ARK_DOMAIN=d, ARK_BORDER=",".join(str(v) for v in BORDER)))


def masks(work, scale):
    """(blend, window_fill): the plate's weight per pixel, and the window pixels a hull covered (filled from the clip)."""
    import cv2
    src = os.path.join(work, "mask", "squads_Nocturne_loop")
    u = None
    for f in sorted(os.listdir(src)):
        a = cv2.imread(os.path.join(src, f), cv2.IMREAD_UNCHANGED)[..., 3]
        u = a if u is None else np.maximum(u, a)
    hull = ((u > 8).astype(np.uint8) * 255)
    hull = cv2.resize(hull, None, fx=scale, fy=scale, interpolation=cv2.INTER_NEAREST)
    g = 2 * GROW * scale + 1
    m = cv2.dilate(hull, np.ones((g, g), np.uint8))
    win = np.zeros_like(m)
    for x0, x1, y0, y1 in WINDOWS:
        win[y0 * scale:y1 * scale, x0 * scale:x1 * scale] = 255
    k = 9 * scale
    fill = ((cv2.dilate(hull, np.ones((k, k), np.uint8)) > 0) & (win > 0)).astype(np.uint8) * 255
    m[win > 0] = 0
    blend = cv2.GaussianBlur(m, (0, 0), FEATHER * scale).astype(np.float32)[..., None] / 255.0
    return blend, fill


def frames_of(video, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    subprocess.run(["ffmpeg", "-loglevel", "error", "-y", "-i", video, os.path.join(out_dir, "v_%04d.png")], check=True)
    return sorted(os.path.join(out_dir, f) for f in os.listdir(out_dir))


def plate_frames(src_dir, out_dir):
    """The re-rendered box, graded and blown up to 1920 x 1080 at 30 fps exactly as the clips were (same filter chain)."""
    import r67_ark_backdrop_videos as V
    tmp, _n = V.graded(src_dir)
    os.makedirs(out_dir, exist_ok=True)
    subprocess.run(["ffmpeg", "-loglevel", "error", "-y", "-framerate", "10", "-i", os.path.join(tmp, "lobby_pads_%04d.jpg"),
                    "-vf", V.VF, os.path.join(out_dir, "p_%04d.png")], check=True)
    shutil.rmtree(tmp, ignore_errors=True)
    return sorted(os.path.join(out_dir, f) for f in os.listdir(out_dir))


def comp_one(work, s, d, blend, fill, keep=False):
    import cv2
    name = f"squads_{s}_{d}_loop"
    clip = os.path.join(DST, name + ".mp4")
    orig = os.path.join(work, "orig", name + ".mp4")
    os.makedirs(os.path.dirname(orig), exist_ok=True)
    if not os.path.exists(orig):                             # the first composite keeps the shipped clip to work from
        shutil.copy(clip, orig)
    tmp = tempfile.mkdtemp(dir=work)
    vs = frames_of(orig, os.path.join(tmp, "v"))
    ps = plate_frames(run_dir(work, s, d), os.path.join(tmp, "p"))
    assert len(vs) == len(ps), (name, len(vs), len(ps))
    out = os.path.join(tmp, "o")
    os.makedirs(out)
    for i, (vf, pf) in enumerate(zip(vs, ps)):
        v = cv2.imread(vf)
        p = cv2.imread(pf).astype(np.float32)
        v = cv2.inpaint(v, fill, 5, cv2.INPAINT_TELEA).astype(np.float32)
        cv2.imwrite(os.path.join(out, f"o_{i:04d}.png"), np.clip(v * (1 - blend) + p * blend, 0, 255).astype(np.uint8))
    subprocess.run(["ffmpeg", "-loglevel", "error", "-y", "-framerate", "30", "-i", os.path.join(out, "o_%04d.png"),
                    "-c:v", "libx264", "-preset", "slow", "-crf", "24", "-pix_fmt", "yuv420p", "-movflags", "+faststart",
                    "-an", clip], check=True)
    print(f"{name}.mp4  {len(vs)} frames  {os.path.getsize(clip) // 1024} KB", flush=True)
    if not keep:
        shutil.rmtree(tmp, ignore_errors=True)
    return tmp


def comp(work):
    blend, fill = masks(work, 2)
    for s in SETS:
        for d in DOMS:
            comp_one(work, s, d, blend, fill)


def preview(work, out):
    import cv2
    rows = []
    for s in SETS:
        for d in DOMS:
            name = f"squads_{s}_{d}_loop"
            pair = []
            for path in (os.path.join(work, "orig", name + ".mp4"), os.path.join(DST, name + ".mp4")):
                ok, f = cv2.VideoCapture(path).read()
                pair.append(cv2.resize(f, (480, 270)))
            rows.append(np.concatenate(pair, 1))
    cv2.imwrite(out, np.concatenate(rows, 0), [cv2.IMWRITE_JPEG_QUALITY, 85])


if __name__ == "__main__":
    cmd = sys.argv[1]
    if cmd == "render":
        render(sys.argv[2], sys.argv[3])
    elif cmd == "comp":
        comp(sys.argv[2])
    elif cmd == "preview":
        preview(sys.argv[2], sys.argv[3])
    else:
        sys.exit(__doc__)
