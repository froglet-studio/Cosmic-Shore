#!/usr/bin/env python3
"""Draw a fauna spec as a four-view sheet, before anything is built (a design aid, not the game's look).

    python3 Tools/Build/fauna_kit/preview.py bee --out <png>

Spindles are drawn as three-strand braids (the shipped spindle is a braid of three strands round a straight centreline,
Docs/SPINDLE_LOD.md section 1), health prisms as boxes, the stinger and other danger prisms in danger red, the
Etherion as an octahedron at the origin. Views: down the travel axis (from ahead), from the side, from three-quarters,
and from behind. A READER: it writes only the PNG it is told to.
"""
import math
import os
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import celestial as C  # noqa: E402

BG, SPINDLE, PRISM, DANGER, HEART, TEXT = (14, 18, 30), (150, 175, 210), (225, 230, 240), (255, 70, 90), (90, 230, 255), (200, 205, 215)


def braid(a, b, turns=2.5, radius=0.35, steps=40):
    """Three strands spiralling round the segment a-b (the spindle's look)."""
    r, u, f = C.frame(C.sub(b, a))
    strands = []
    for k in range(3):
        pts = []
        for i in range(steps + 1):
            t = i / steps
            ph = 2 * math.pi * (turns * t + k / 3)
            w = radius * (0.25 + 0.75 * math.sin(math.pi * min(1.0, t * 1.15)))   # pinched at the root, open at the tip
            c = C.add(a, C.mul(C.sub(b, a), t))
            pts.append(C.add(c, C.add(C.mul(r, w * math.cos(ph)), C.mul(u, w * math.sin(ph)))))
        strands.append(pts)
    return strands


def view_matrix(yaw, pitch):
    cy, sy, cp, sp = math.cos(yaw), math.sin(yaw), math.cos(pitch), math.sin(pitch)
    def proj(p):
        x = cy * p[0] + sy * p[2]
        z = -sy * p[0] + cy * p[2]
        y = cp * p[1] - sp * z
        d = sp * p[1] + cp * z
        return (x, y, d)
    return proj


def draw_view(spec, proj, size, title):
    img = Image.new("RGB", (size, size), BG)
    g = ImageDraw.Draw(img)
    items = []   # (depth, kind, points)
    for s in spec["spindles"]:
        for strand in braid(s["from"], s["to"]):
            for i in range(len(strand) - 1):
                items.append((proj(strand[i])[2], "line", SPINDLE, [proj(strand[i]), proj(strand[i + 1])]))
    for p in spec["prisms"]:
        cs = C.box_corners(p)
        faces = [(0, 1, 3, 2), (4, 5, 7, 6), (0, 1, 5, 4), (2, 3, 7, 6), (0, 2, 6, 4), (1, 3, 7, 5)]
        col = DANGER if p.get("kind") == "danger" else PRISM
        for fi, f in enumerate(faces):
            pts = [proj(cs[i]) for i in f]
            shade = 0.55 + 0.45 * ((fi % 3) / 2)
            items.append((sum(q[2] for q in pts) / 4, "poly", tuple(int(c * shade) for c in col), pts))
    h = C.HEART_WORLD / 2
    oct_pts = [[h, 0, 0], [-h, 0, 0], [0, h, 0], [0, -h, 0], [0, 0, h], [0, 0, -h]]
    for tri in [(0, 2, 4), (2, 1, 4), (1, 3, 4), (3, 0, 4), (2, 0, 5), (1, 2, 5), (3, 1, 5), (0, 3, 5)]:
        pts = [proj(oct_pts[i]) for i in tri]
        items.append((sum(q[2] for q in pts) / 3, "poly", HEART, pts))
    allp = [q for it in items for q in it[3]]
    xs, ys = [q[0] for q in allp], [q[1] for q in allp]
    span = max(max(xs) - min(xs), max(ys) - min(ys), 1e-6)
    sc, cx, cy = size * 0.82 / span, (max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2
    to_px = lambda q: (size / 2 + (q[0] - cx) * sc, size / 2 - (q[1] - cy) * sc)
    for d, kind, col, pts in sorted(items, key=lambda it: -it[0]):
        px = [to_px(q) for q in pts]
        if kind == "line":
            g.line(px, fill=col, width=2)
        else:
            g.polygon(px, fill=col, outline=BG)
    g.text((10, 8), title, fill=TEXT)
    return img


def main(argv):
    if not argv or argv[0].startswith("-"):
        print(__doc__)
        return 2
    name = argv[0]
    out = argv[argv.index("--out") + 1] if "--out" in argv else f"{name}_preview.png"
    spec = C.load(name)
    size = 520
    views = [("from ahead (down the travel axis)", view_matrix(math.pi, 0.0)),
             ("side", view_matrix(math.pi / 2, 0.0)),
             ("three-quarters", view_matrix(math.radians(140), math.radians(25))),
             ("from behind", view_matrix(0.0, 0.0))]
    sheet = Image.new("RGB", (size * 2, size * 2), BG)
    for k, (title, proj) in enumerate(views):
        sheet.paste(draw_view(spec, proj, size, f"{spec['species']} - {title}"), ((k % 2) * size, (k // 2) * size))
    sheet.save(out)
    print(out)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
