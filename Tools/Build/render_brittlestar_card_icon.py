#!/usr/bin/env python3
"""Render the Brittle Star's CARD ICONS (SO_Class_BrittleStar.IconActive / IconInactive) from its own procedural hull.

    python3 Tools/Build/render_brittlestar_card_icon.py            # export the hull, render, write the PNGs + metas
    python3 Tools/Build/render_brittlestar_card_icon.py --check    # re-render and compare to the committed PNGs
    python3 Tools/Build/render_brittlestar_card_icon.py --obj DIR  # render from an existing `brittlestar_harness --obj` export

The Thresher's card renderer (render_thresher_card_icon.py) with the Brittle Star's hull and pose: the geometry comes from
Tools/Build/brittlestar_harness (which compiles and RUNS the shipped BrittleStarHullForm and exports it as OBJ), the
rasterizer, the house card look (gold plates, green slot-1 accents, a navy outline) and the inactive fade are imported
from that script, so the two cards cannot drift apart in style. The pose looks at the pentagon three-quarters on, so
the five points read. Change the hull and re-run this; the card follows it.

--check re-renders and fails when any pixel differs from the committed file by more than 3/255. Needs numpy + Pillow and,
without --obj, the dotnet SDK the harness needs.
"""
import os
import subprocess
import sys
import tempfile

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import render_thresher_card_icon as R  # noqa: E402  (the shared rasterizer; importing it has no side effects)

ROOT = os.path.dirname(os.path.dirname(HERE))
OUT_DIR = os.path.join(ROOT, "Assets", "_Graphics", "CardImages")
ACTIVE = "BrittleStar.png"
INACTIVE = "BrittleStar_Inactive.png"
# Deterministic guids (SO_Class_BrittleStar names them; author_brittlestar_assets.py --check reads the class asset).
GUIDS = {ACTIVE: "b5a17e5c3d0f4a7e9c1b2d4f6a8c0e21", INACTIVE: "b5a17e5c3d0f4a7e9c1b2d4f6a8c0e22"}
# Three-quarters on to the ring, nose to screen-right and toward the viewer, seen a little from above.
POSE = (70.0, -30.0, 0.0)


def export_obj(directory):
    harness = os.path.join(HERE, "brittlestar_harness", "run.sh")
    subprocess.run(["bash", harness, "--obj", directory], check=True, stdout=subprocess.DEVNULL)
    return os.path.join(directory, "base.obj")


def main(argv):
    check = "--check" in argv
    R.YAW, R.PITCH, R.ROLL = POSE
    if "--obj" in argv:
        verts, tris = R.load_obj(os.path.join(argv[argv.index("--obj") + 1], "base.obj"))
    else:
        with tempfile.TemporaryDirectory() as tmp:
            verts, tris = R.load_obj(export_obj(tmp))
    active = R.render(verts, tris)
    images = {ACTIVE: active, INACTIVE: R.inactive(active)}
    if check:
        bad = []
        for name, img in images.items():
            path = os.path.join(OUT_DIR, name)
            if not os.path.exists(path):
                bad.append(f"{name} missing")
                continue
            have = np.asarray(Image.open(path).convert("RGBA")).astype(np.int16)
            want = np.clip(np.round(img * 255), 0, 255).astype(np.int16)
            worst = int(np.abs(have - want).max()) if have.shape == want.shape else 999
            if worst > 3:
                bad.append(f"{name} differs by {worst}/255 - re-run without --check")
            meta = open(path + ".meta", encoding="utf-8").read() if os.path.exists(path + ".meta") else ""
            if f"guid: {GUIDS[name]}" not in meta:
                bad.append(f"{name}.meta does not carry guid {GUIDS[name]}")
        for b in bad:
            print("FAIL:", b)
        print("render_brittlestar_card_icon: " + ("OK" if not bad else f"{len(bad)} finding(s)"))
        return 0 if not bad else 1
    for name, img in images.items():
        path = os.path.join(OUT_DIR, name)
        R.to_png(img, path)
        with open(path + ".meta", "w", encoding="utf-8", newline="\n") as f:
            f.write(R.sprite_meta(GUIDS[name]))
        print(f"wrote {os.path.relpath(path, ROOT)} (guid {GUIDS[name]}, coverage {float((img[..., 3] > 0.5).mean()):.3f})")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
