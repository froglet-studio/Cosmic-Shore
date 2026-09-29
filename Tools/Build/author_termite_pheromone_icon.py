#!/usr/bin/env python3
"""Author the Termite queen's TIME card icon: the PHEROMONE drop (a placeholder pending an art pass).

The three card slots draw the prototype's own card faces (_Graphics/VesselButtons/TermiteCard_Front_*);
the Time slot is not a card - it is the pheromone tank every card is paid out of - so it gets a mark
of its own: a white drop with three rising scent arcs, drawn as a PURE WHITE silhouette on
transparency so the lockup can tint it like every other ability icon.

Deterministic (no randomness, fixed supersample), so `--check` is a byte comparison.
Run:  python3 Tools/Build/author_termite_pheromone_icon.py [--check]
"""
import io, math, os, sys
from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
assert os.path.isdir(os.path.join(ROOT, "Assets")), ROOT
OUT = os.path.join(ROOT, "Assets/_Graphics/VesselButtons/TermiteCard_Pheromone.png")
META_TEMPLATE = os.path.join(ROOT, "Assets/_Graphics/VesselButtons/TermiteCard_Front_Autothysis.png.meta")
# Fixed guid so the HUD variant and this tool agree across re-runs.
GUID = "5f3a8e21c6b94d0e9a7b2c4d1e8f6a03"
SIZE, SS = 256, 4


def draw() -> bytes:
    W = SIZE * SS
    im = Image.new("L", (W, W), 0)
    d = ImageDraw.Draw(im)
    cx, cy, r = W * 0.5, W * 0.64, W * 0.22
    # The drop: a circle with a tangent point above it.
    tip = (cx, cy - r * 2.35)
    a = math.asin(r / (cy - tip[1]))
    left = (cx - r * math.cos(a), cy - r * math.sin(a))
    right = (cx + r * math.cos(a), cy - r * math.sin(a))
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=255)
    d.polygon([tip, left, right], fill=255)
    # Three scent arcs rising to the right of the tip.
    for k in range(3):
        rr = W * (0.09 + 0.07 * k)
        ox, oy = cx + W * 0.16, cy - r * 1.6
        box = [ox - rr, oy - rr, ox + rr, oy + rr]
        d.arc(box, start=-70, end=10, fill=255, width=int(W * 0.028))
    im = im.resize((SIZE, SIZE), Image.LANCZOS)
    rgba = Image.new("RGBA", (SIZE, SIZE), (255, 255, 255, 0))
    rgba.putalpha(im)
    buf = io.BytesIO()
    rgba.save(buf, format="PNG", optimize=False)
    return buf.getvalue()


def meta() -> str:
    import re
    t = open(META_TEMPLATE).read()
    return re.sub(r"^guid: [0-9a-f]{32}", "guid: " + GUID, t, flags=re.M)


def main():
    check = "--check" in sys.argv
    png, mt = draw(), meta()
    drift = []
    for path, data, mode in ((OUT, png, "wb"), (OUT + ".meta", mt, "w")):
        cur = open(path, "rb" if mode == "wb" else "r").read() if os.path.exists(path) else None
        if cur != data:
            drift.append(path)
            if not check:
                with open(path, mode) as fh:
                    fh.write(data)
    if check and drift:
        sys.exit("DRIFT: " + ", ".join(os.path.relpath(p, ROOT) for p in drift))
    print(("checked" if check else "wrote") + " " + os.path.relpath(OUT, ROOT))


if __name__ == "__main__":
    main()
