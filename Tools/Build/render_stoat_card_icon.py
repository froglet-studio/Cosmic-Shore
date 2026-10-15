#!/usr/bin/env python3
"""Render the Stoat's CARD ICONS (SO_Class_Stoat.IconActive / IconInactive) from its own procedural hull.

    python3 Tools/Build/render_stoat_card_icon.py            # export the hull, render, write the PNGs + metas
    python3 Tools/Build/render_stoat_card_icon.py --check    # re-render and compare to the committed PNGs
    python3 Tools/Build/render_stoat_card_icon.py --obj DIR  # render from an existing `stoat_hull_harness --obj` export

The fleet's card icons are 256 px renders of each hull in the house card look (gold plates, dark outline, green
accents - Assets/_Graphics/CardImages/Squirrel.png). The Stoat's hull has no model asset: it is generated at runtime
by StoatHullForm. So the geometry comes from Tools/Build/stoat_hull_harness (which compiles and RUNS the shipped
StoatHullForm and exports its parts as OBJ), and this script rasterizes it - a small z-buffered, flat-shaded,
supersampled software renderer (numpy), the same three-quarter pose the Squirrel's card is drawn in. Change the hull
and re-run this; the card follows it.

  Stoat.png            the active card: gold body, green domain accents (the slot-1 parts: fins, eyes, crystal),
                       a dark outline
  Stoat_Inactive.png   the same render, faded toward white and partly desaturated (the Squirrel's inactive look)

--check re-renders and fails when any pixel differs from the committed file by more than 3/255 (rasterization is
deterministic; the tolerance only absorbs float differences between machines). Needs numpy + Pillow and, without
--obj, the dotnet SDK the harness needs.
"""
import hashlib
import os
import re
import subprocess
import sys
import tempfile

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "Assets", "_Graphics", "CardImages")
DONOR_META = os.path.join(OUT_DIR, "Squirrel.png.meta")
ACTIVE = "Stoat.png"
INACTIVE = "Stoat_Inactive.png"
# Deterministic guids (SO_Class_Stoat names them; author_stoat_assets.py reads the same constants).
GUIDS = {ACTIVE: "627c06caaa88435fa2c44e98ad1fb611", INACTIVE: "d845d4e49ee148f9b787599a3755eaa9"}

SIZE, SS = 256, 3
BODY = np.array([245, 179, 33], dtype=np.float64) / 255.0      # the card gold
ACCENT = np.array([70, 205, 60], dtype=np.float64) / 255.0     # the card green (slot 1: fins, eyes, crystal)
OUTLINE = np.array([22, 22, 48], dtype=np.float64) / 255.0     # the card's dark navy rim
LIGHT = np.array([-0.35, 0.75, 0.55])
# The pose: nose up and to the right, seen a little from above and from the left flank.
YAW, PITCH, ROLL = 22.0, -20.0, 32.0


def load_obj(path):
    verts, tris = [], []
    material = "body"
    for line in open(path, encoding="utf-8"):
        if line.startswith("v "):
            verts.append([float(x) for x in line.split()[1:4]])
        elif line.startswith("usemtl"):
            material = line.split()[1]
        elif line.startswith("f "):
            idx = [int(tok.split("/")[0]) - 1 for tok in line.split()[1:]]
            for i in range(1, len(idx) - 1):
                tris.append((idx[0], idx[i], idx[i + 1], material == "accent"))
    return np.array(verts), tris


def rotation():
    def rx(a):
        c, s = np.cos(np.radians(a)), np.sin(np.radians(a))
        return np.array([[1, 0, 0], [0, c, -s], [0, s, c]])

    def ry(a):
        c, s = np.cos(np.radians(a)), np.sin(np.radians(a))
        return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])

    def rz(a):
        c, s = np.cos(np.radians(a)), np.sin(np.radians(a))
        return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])

    return rz(ROLL) @ rx(PITCH) @ ry(YAW)


def render(verts, tris):
    """RGBA float image, SIZE x SIZE: z-buffered flat shading at SS x supersampling, then the outline."""
    r = rotation()
    # Hull space is +Z forward; turn it so +Z points to screen-right (x), +Y up (y), camera looking down -z.
    to_view = np.array([[0, 0, 1], [0, 1, 0], [-1, 0, 0]], dtype=np.float64)
    v = verts @ to_view.T @ r.T
    n_px = SIZE * SS
    lo, hi = v[:, :2].min(0), v[:, :2].max(0)
    scale = (n_px * 0.84) / max(hi - lo)
    centre = (lo + hi) / 2
    sx = (v[:, 0] - centre[0]) * scale + n_px / 2
    sy = n_px / 2 - (v[:, 1] - centre[1]) * scale
    sz = v[:, 2]
    color = np.zeros((n_px, n_px, 3))
    depth = np.full((n_px, n_px), -np.inf)
    light = LIGHT / np.linalg.norm(LIGHT)
    for a, b, c, accent in tris:
        p, q, w = v[a], v[b], v[c]
        normal = np.cross(q - p, w - p)
        length = np.linalg.norm(normal)
        if length < 1e-12:
            continue
        normal /= length
        if normal[2] <= 0:          # back face (the hull is wound outward; the camera looks down -z)
            continue
        base = ACCENT if accent else BODY
        shade = 0.38 + 0.62 * max(0.0, float(normal @ light))
        rim = 0.25 * (1.0 - normal[2]) ** 3
        rgb = np.clip(base * shade + rim, 0, 1)
        xs = np.array([sx[a], sx[b], sx[c]]); ys = np.array([sy[a], sy[b], sy[c]]); zs = np.array([sz[a], sz[b], sz[c]])
        x0, x1 = int(max(0, np.floor(xs.min()))), int(min(n_px - 1, np.ceil(xs.max())))
        y0, y1 = int(max(0, np.floor(ys.min()))), int(min(n_px - 1, np.ceil(ys.max())))
        if x1 < x0 or y1 < y0:
            continue
        gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        d = (ys[1] - ys[2]) * (xs[0] - xs[2]) + (xs[2] - xs[1]) * (ys[0] - ys[2])
        if abs(d) < 1e-12:
            continue
        l0 = ((ys[1] - ys[2]) * (gx - xs[2]) + (xs[2] - xs[1]) * (gy - ys[2])) / d
        l1 = ((ys[2] - ys[0]) * (gx - xs[2]) + (xs[0] - xs[2]) * (gy - ys[2])) / d
        l2 = 1 - l0 - l1
        inside = (l0 >= -1e-6) & (l1 >= -1e-6) & (l2 >= -1e-6)
        z = l0 * zs[0] + l1 * zs[1] + l2 * zs[2]
        tile = depth[y0:y1 + 1, x0:x1 + 1]
        win = inside & (z > tile)
        tile[win] = z[win]
        color[y0:y1 + 1, x0:x1 + 1][win] = rgb

    covered = np.isfinite(depth)
    # The outline: the silhouette dilated by ~2 output pixels, drawn in navy behind the hull.
    grow = SS * 2
    ring = covered.copy()
    for dy in range(-grow, grow + 1):
        for dx in range(-grow, grow + 1):
            if dx * dx + dy * dy > grow * grow:
                continue
            ring |= np.roll(np.roll(covered, dy, 0), dx, 1)
    rgba = np.zeros((n_px, n_px, 4))
    rgba[ring, :3] = OUTLINE
    rgba[ring, 3] = 1.0
    rgba[covered, :3] = color[covered]
    # Downsample (box filter) with premultiplied alpha.
    pre = rgba.copy()
    pre[..., :3] *= pre[..., 3:4]
    pre = pre.reshape(SIZE, SS, SIZE, SS, 4).mean(axis=(1, 3))
    out = pre.copy()
    alpha = out[..., 3:4]
    out[..., :3] = np.where(alpha > 1e-6, out[..., :3] / np.maximum(alpha, 1e-6), 0)
    return out


def inactive(img):
    out = img.copy()
    grey = out[..., :3] @ np.array([0.299, 0.587, 0.114])
    out[..., :3] = 0.5 * out[..., :3] + 0.5 * grey[..., None]
    out[..., :3] = out[..., :3] * 0.7 + 0.3
    out[..., 3] *= 0.85
    return out


def to_png(img, path):
    Image.fromarray(np.clip(np.round(img * 255), 0, 255).astype(np.uint8), "RGBA").save(path, optimize=False, compress_level=9)


def sprite_meta(guid):
    donor = open(DONOR_META, encoding="utf-8").read()
    out = re.sub(r"^guid: [0-9a-f]{32}$", f"guid: {guid}", donor, count=1, flags=re.M)
    assert out.count(guid) == 1, "donor meta did not take the guid"
    return out


def export_obj(directory):
    harness = os.path.join(ROOT, "Tools", "Build", "stoat_hull_harness", "run.sh")
    subprocess.run(["bash", harness, "--obj", directory], check=True, stdout=subprocess.DEVNULL)
    return os.path.join(directory, "base.obj")


def main(argv):
    check = "--check" in argv
    if "--obj" in argv:
        obj = os.path.join(argv[argv.index("--obj") + 1], "base.obj")
        verts, tris = load_obj(obj)
    else:
        with tempfile.TemporaryDirectory() as tmp:
            verts, tris = load_obj(export_obj(tmp))
    active = render(verts, tris)
    images = {ACTIVE: active, INACTIVE: inactive(active)}
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
        print("render_stoat_card_icon: " + ("OK" if not bad else f"{len(bad)} finding(s)"))
        return 0 if not bad else 1
    for name, img in images.items():
        path = os.path.join(OUT_DIR, name)
        to_png(img, path)
        with open(path + ".meta", "w", encoding="utf-8", newline="\n") as f:
            f.write(sprite_meta(GUIDS[name]))
        print(f"wrote {os.path.relpath(path, ROOT)} (guid {GUIDS[name]}, coverage {float((img[..., 3] > 0.5).mean()):.3f})")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
