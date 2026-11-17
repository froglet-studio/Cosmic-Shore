#!/usr/bin/env python3
"""Draw laid Regatta course genomes as PNG stills: every prism the GAME laid (rails and placements, from
Tools/Build/regatta_genome_harness/run.sh lay), seen in the course's own plane and edge-on, beside what each placement
is and what the arena carved from it (Plan step: time-arena-course; Regatta flywheel W2).

Colours are the style guide's (Docs/UI/froglet-tokens.json, deep-sea): the three team rails and any prism in a team
colour in Jade / Ruby / Gold, Blue (neutral) in the control blue, a Danger prism outlined in the danger red, the rings
in the line colour. Nearer prisms are drawn over farther ones and larger.

    python3 Tools/Build/regatta_genome_render.py LAID.json [LAID.json ...] --out DIR   # one PNG each + sheet.png

A READER tool: it writes stills to the folder it is given and nothing into the project.
"""
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
W, H = 1600, 900
FONT = os.path.join(ROOT, "Assets", "_Graphics", "Fonts", "ChakraPetch", "ChakraPetch-SemiBold.ttf")
DOMAIN = {1: "jade", 2: "ruby", 3: "control", 4: "gold"}   # CosmicShore.Data.Domains
DANGER = 1                                                  # PrismKind.Danger


def tokens():
    try:
        return json.load(open(os.path.join(ROOT, "Docs", "UI", "froglet-tokens.json"), encoding="utf-8"))["themes"]["deep-sea"]
    except (OSError, KeyError, ValueError):
        return {"ground": "#00041F", "surface": "#222645", "line": "#434C89", "text": "#E6E9FF", "text-soft": "#747BAD",
                "danger": "#FF4B3A", "control": "#00D4FF", "jade": "#21C0BD", "ruby": "#BD1BC8", "gold": "#FF9B11"}


def font(size):
    from PIL import ImageFont
    try:
        return ImageFont.truetype(FONT, size)
    except OSError:
        return ImageFont.load_default(size=size)


def rgb(h, a=255):
    h = h.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


def frame(points):
    """The course plane: two orthonormal axes over the gates' widest extent, and its normal."""
    n = len(points)
    c = [sum(p[k] for p in points) / n for k in range(3)]
    cov = [[sum((p[i] - c[i]) * (p[j] - c[j]) for p in points) / n for j in range(3)] for i in range(3)]

    def top(m, avoid=()):
        v = [1.0, 0.7, 0.3]
        for _ in range(100):
            w = [sum(m[i][j] * v[j] for j in range(3)) for i in range(3)]
            for a in avoid:
                d = sum(w[k] * a[k] for k in range(3))
                w = [w[k] - d * a[k] for k in range(3)]
            s = math.sqrt(sum(x * x for x in w)) or 1.0
            v = [x / s for x in w]
        return v
    u = top(cov)
    v = top(cov, (u,))
    nrm = [u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0]]
    return c, u, v, nrm


def ring(g, steps=40):
    a = g["a"]
    ref = (0.0, 1.0, 0.0) if abs(a[1]) < 0.9 else (1.0, 0.0, 0.0)
    e1 = [a[1] * ref[2] - a[2] * ref[1], a[2] * ref[0] - a[0] * ref[2], a[0] * ref[1] - a[1] * ref[0]]
    s = math.sqrt(sum(x * x for x in e1)) or 1.0
    e1 = [x / s for x in e1]
    e2 = [a[1] * e1[2] - a[2] * e1[1], a[2] * e1[0] - a[0] * e1[2], a[0] * e1[1] - a[1] * e1[0]]
    return [[g["p"][k] + g["r"] * (math.cos(2 * math.pi * i / steps) * e1[k] + math.sin(2 * math.pi * i / steps) * e2[k])
             for k in range(3)] for i in range(steps + 1)]


def panel(img, laid, box, axes, T, sizes):
    """Draw every prism and ring in `box`, projected on axes (x axis, y axis, depth axis)."""
    from PIL import ImageDraw
    d = ImageDraw.Draw(img, "RGBA")
    c, ax, ay, az = axes
    proj = lambda q: (sum((q[k] - c[k]) * ax[k] for k in range(3)), sum((q[k] - c[k]) * ay[k] for k in range(3)),
                      sum((q[k] - c[k]) * az[k] for k in range(3)))
    span = 1250.0                                   # the membrane's radius, a little over: every course at one scale
    sc = min(box[2] - box[0], box[3] - box[1]) / (2 * span)
    mx, my = (box[0] + box[2]) / 2, (box[1] + box[3]) / 2
    d.ellipse([mx - 1200 * sc, my - 1200 * sc, mx + 1200 * sc, my + 1200 * sc], outline=rgb(T["line"], 160), width=1)
    d.ellipse([mx - 392 * sc, my - 392 * sc, mx + 392 * sc, my + 392 * sc], outline=rgb(T["line"], 120), width=1)
    pr = [(proj(p[:3]), p) for p in laid["prisms"]]
    pr.sort(key=lambda t: t[0][2])
    zs = [t[0][2] for t in pr] or [0.0]
    z0, z1 = min(zs), max(zs)
    for (x, y, z), p in pr:
        depth = (z - z0) / (z1 - z0 + 1e-6)
        rails = p[6] < 0
        col = T.get(DOMAIN.get(int(p[4]), "text-soft"), T["text-soft"])
        a = int(110 + 140 * depth) if not rails else 255
        r = max(0.8, min(9.0, p[3] * sc * sizes * (0.7 + 0.6 * depth)))
        px, py = mx + x * sc, my - y * sc
        if int(p[5]) == DANGER:
            d.ellipse([px - r - 1, py - r - 1, px + r + 1, py + r + 1], outline=rgb(T["danger"], a), width=1)
        d.ellipse([px - r, py - r, px + r, py + r], fill=rgb(col, a))
    for g in laid["gates"]:
        pts = [proj(q) for q in ring(g)]
        d.line([(mx + q[0] * sc, my - q[1] * sc) for q in pts], fill=rgb(T["text"], 230), width=2)


def draw(laid, path, label):
    from PIL import Image, ImageDraw
    T = tokens()
    img = Image.new("RGB", (W, H), rgb(T["ground"])[:3])
    gates = [g["p"] for g in laid["gates"]]
    c, u, v, n = frame(gates)
    panel(img, laid, (20, 60, 860, 880), (c, u, v, n), T, 1.0)
    panel(img, laid, (880, 60, 1180, 360), (c, u, n, v), T, 0.6)
    d = ImageDraw.Draw(img)
    d.text((24, 16), label, font=font(28), fill=rgb(T["text"])[:3])
    g = laid["genome"]
    reps = laid.get("reports", [])
    laid_n = sum(r.get("laid", 0) for r in reps)
    d.text((884, 366), "edge-on", font=font(16), fill=rgb(T["text-soft"])[:3])
    lines = [f"intensity {g.get('intensity')}  seed {g.get('seed')}  line tube {g.get('line_clearance', 0):.0f} u",
             f"{laid['rail_prisms']} rail prisms + {laid_n} placement prisms, laid in {laid.get('ms', 0):.0f} ms",
             f"{len(g.get('placements', []))} placements, {len(g['features']['Sections'])} rail sections, "
             f"{len(g['features']['AlternateGates'])} alternate rings, {laid.get('sites', 0)} planting sites", ""]
    for p, r in zip(g.get("placements", []), reps):
        if r.get("failure"):
            lines.append(f"{p['kind']:<16} FAILED {r['failure'][:40]}")
            continue
        cv = r.get("carved", {})
        carved = sum(cv.values())
        col = "" if p.get("domain", "Native") == "Native" else f" {p['domain']}"
        kind = "" if p.get("prism", "Native") == "Native" else f" {p['prism']}"
        lines.append(f"{p['kind']:<16} r={p['scale']:>4.0f}u{col}{kind}  {r['laid']:>4} laid, {carved} carved")
    y = 400
    for line in lines[:26]:
        d.text((884, y), line, font=font(17), fill=rgb(T["text"] if line and not line.startswith(" ") else T["text-soft"])[:3])
        y += 19
    if len(lines) > 26:
        d.text((884, y), f"... {len(lines) - 26} more", font=font(17), fill=rgb(T["text-soft"])[:3])
    img.save(path)
    return img


def sheet(images, path, cols=4):
    from PIL import Image
    tw, th = W // 4, H // 4
    rows = (len(images) + cols - 1) // cols
    out = Image.new("RGB", (tw * cols, th * rows), (0, 4, 31))
    for i, im in enumerate(images):
        out.paste(im.resize((tw, th)), ((i % cols) * tw, (i // cols) * th))
    out.save(path)


def main(argv):
    if "--out" not in argv:
        print(__doc__)
        return 2
    out = argv[argv.index("--out") + 1]
    files = [a for a in argv if a.endswith(".json") and a != out]
    os.makedirs(out, exist_ok=True)
    images = []
    for i, f in enumerate(files):
        laid = json.load(open(f, encoding="utf-8"))
        label = os.path.splitext(os.path.basename(f))[0].replace("_", " ")
        images.append(draw(laid, os.path.join(out, f"{os.path.splitext(os.path.basename(f))[0]}.png"), label))
    if images:
        sheet(images, os.path.join(out, "sheet.png"))
    print(f"wrote {len(images)} stills and sheet.png to {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
