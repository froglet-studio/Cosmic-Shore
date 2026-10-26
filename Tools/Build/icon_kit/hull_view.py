#!/usr/bin/env python3
"""Project a vessel's FBX mesh to a top / side / front silhouette mask - the reference a hull's own icon silhouette
is drawn from (Garrett 2026-10-25: "it should read as the squirrel silhouette vs a generic one").

    python3 Tools/Build/icon_kit/hull_view.py "Assets/_Models/Vessel Models/SquirrelVessel_CosmicShoresTest1.fbx"
    python3 Tools/Build/icon_kit/hull_view.py <file.fbx> --out <dir> --size 1024

Writes <out>/<stem>_top.png, _side.png, _front.png (white on black, nose UP in the top view, nose RIGHT in the side
view). Reads the binary FBX with the repo's own codec (Tools/Build/fbx_binary.py), takes every Geometry/Mesh at its
file-frame coordinates (file: UpAxis +Z, FrontAxis +Y = the nose, per Docs/VesselModels/<Hull>.md §1.4) and fills
each polygon; the bind pose, not a take - good enough to measure proportions from and LOOK at. No Blender needed.
Reader only; nothing under Assets/ changes.
"""
import argparse
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "vessel_model"))
import fbx_binary as F  # noqa: E402
from fbx_probe import arr, subclass_of  # noqa: E402

VIEWS = {"top": (0, 1, True), "side": (1, 2, True), "front": (0, 2, True)}   # (across, up, flip up)


def polygons(path):
    nodes, _version, _footer = F.read(path)
    objects = {n.name: n for n in nodes}["Objects"]
    polys, allv = [], []
    for o in objects.children:
        if o.name == "Geometry" and subclass_of(o) == "Mesh":
            V = np.array(arr(o, "Vertices"), dtype=float).reshape(-1, 3)
            allv.append(V)
            poly = []
            for i in arr(o, "PolygonVertexIndex"):
                if i < 0:
                    poly.append(~i)
                    polys.append(V[poly])
                    poly = []
                else:
                    poly.append(i)
    return polys, np.vstack(allv)


def render(polys, V, view, size):
    ax0, ax1, flip = VIEWS[view]
    lo, hi = V.min(0), V.max(0)
    ext = max(hi[ax0] - lo[ax0], hi[ax1] - lo[ax1]) * 1.08
    cx, cy = (lo[ax0] + hi[ax0]) / 2, (lo[ax1] + hi[ax1]) / 2
    im = Image.new("L", (size, size), 0)
    d = ImageDraw.Draw(im)
    for p in polys:
        pts = [((q[ax0] - cx) / ext * size + size / 2, (q[ax1] - cy) / ext * size * (-1 if flip else 1) + size / 2) for q in p]
        d.polygon(pts, fill=255)
    return im


def main(argv):
    ap = argparse.ArgumentParser()
    ap.add_argument("fbx")
    ap.add_argument("--out", default=None, help="output folder (default: the scratchpad-like /tmp/hull_view)")
    ap.add_argument("--size", type=int, default=1024)
    a = ap.parse_args(argv)
    out = a.out or os.path.join("/tmp", "hull_view")
    os.makedirs(out, exist_ok=True)
    polys, V = polygons(a.fbx)
    stem = os.path.splitext(os.path.basename(a.fbx))[0]
    print(f"{stem}: {len(polys)} polygons, bounds {V.min(0).round(3)} .. {V.max(0).round(3)} (file frame; nose = +Y)")
    for view in VIEWS:
        p = os.path.join(out, f"{stem}_{view}.png")
        render(polys, V, view, a.size).save(p)
        print("  " + p)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
