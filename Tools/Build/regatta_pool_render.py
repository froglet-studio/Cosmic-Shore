#!/usr/bin/env python3
"""Draw Regatta pool candidates as PNG stills: the circuit from above (rings, the three rails, neutral stretches, gaps)
beside its numbers, for Garrett to pick a pool from (Plan step: time-arena-course).

Every course is re-laid from its entry (the RegattaCourse settings row, the seed step, the neutral share) by the
same generator the search raced (Tools/Compute/evaluators/regatta_pool.py -> course_port.py), so the still shows the
course the numbers belong to. The rails are drawn in the style guide's team colours (Docs/UI/froglet-tokens.json,
deep-sea), a neutral stretch (RegattaCourse.InNeutralStretch: the middle share of every leg, by the nearest gate
chord) in pale grey (shore theme line), a gap (brief 10, `metrics.gaps` as [leg, t0, t1]) left out. The view is the course's own plane
(the two widest axes of its gates), so a course that climbs reads as a flatter loop than it flies.

    python3 Tools/Build/regatta_pool_render.py <pool.json | archive.json> --out <dir>   # one PNG per entry + a sheet
    python3 Tools/Build/regatta_pool_render.py --self-test

A READER tool: it writes stills to the folder it is given and nothing into the project.
"""
import argparse
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path[:0] = [HERE, os.path.join(ROOT, "Tools", "Compute", "evaluators")]

W, H = 1600, 900
FONT = os.path.join(ROOT, "Assets", "_Graphics", "Fonts", "ChakraPetch", "ChakraPetch-SemiBold.ttf")


def tokens():
    try:
        themes = json.load(open(os.path.join(ROOT, "Docs", "UI", "froglet-tokens.json"), encoding="utf-8"))["themes"]
        t, shore = themes["deep-sea"], themes.get("shore", {})
    except (OSError, KeyError, ValueError):
        t, shore = {}, {}
    get = lambda k, d: t.get(k, d)
    return {"ground": get("ground", "#00041F"), "surface": get("surface", "#222645"), "line": get("line", "#434C89"),
            "text": get("text", "#E6E9FF"), "soft": get("text-soft", "#747BAD"), "cta": get("cta", "#99FF80"),
            "danger": get("danger", "#FF4B3A"), "lanes": [get("jade", "#21C0BD"), get("ruby", "#BD1BC8"),
                                                          get("gold", "#FF8F11")],
            "neutral": shore.get("line", "#B8C0F0")}


def font(size):
    from PIL import ImageFont
    try:
        return ImageFont.truetype(FONT, size)
    except OSError:
        return ImageFont.load_default(size=size)


def plane(points):
    """Two orthonormal axes spanning the points' widest extent (power iteration on the covariance), and their centre."""
    n = len(points)
    c = [sum(p[k] for p in points) / n for k in range(3)]
    cov = [[sum((p[i] - c[i]) * (p[j] - c[j]) for p in points) / n for j in range(3)] for i in range(3)]

    def top(m, avoid=None):
        v = [1.0, 0.7, 0.3]
        for _ in range(100):
            w = [sum(m[i][j] * v[j] for j in range(3)) for i in range(3)]
            if avoid:
                d = sum(w[k] * avoid[k] for k in range(3))
                w = [w[k] - d * avoid[k] for k in range(3)]
            s = math.sqrt(sum(x * x for x in w)) or 1.0
            v = [x / s for x in w]
        return v
    u = top(cov)
    v = top(cov, u)
    return c, u, v


def neutral_t(p, gates):
    """RegattaCourse.InNeutralStretch's t: where p projects on the nearest gate-to-gate chord, 0..1."""
    best, bt, bi = 1e30, 0.0, 0
    n = len(gates)
    for i in range(n):
        a, b = gates[i]["p"], gates[(i + 1) % n]["p"]
        ab = [b[k] - a[k] for k in range(3)]
        l2 = sum(x * x for x in ab)
        if l2 < 1e-6:
            continue
        t = min(1.0, max(0.0, sum((p[k] - a[k]) * ab[k] for k in range(3)) / l2))
        d = sum((a[k] + ab[k] * t - p[k]) ** 2 for k in range(3))
        if d < best:
            best, bt, bi = d, t, i
    return bi, bt


def ring_points(g, steps=28):
    a = g["a"]
    ref = (0.0, 1.0, 0.0) if abs(a[1]) < 0.9 else (1.0, 0.0, 0.0)
    e1 = [a[1] * ref[2] - a[2] * ref[1], a[2] * ref[0] - a[0] * ref[2], a[0] * ref[1] - a[1] * ref[0]]
    s = math.sqrt(sum(x * x for x in e1)) or 1.0
    e1 = [x / s for x in e1]
    e2 = [a[1] * e1[2] - a[2] * e1[1], a[2] * e1[0] - a[0] * e1[2], a[0] * e1[1] - a[1] * e1[0]]
    return [[g["p"][k] + g["r"] * (math.cos(2 * math.pi * i / steps) * e1[k] + math.sin(2 * math.pi * i / steps) * e2[k])
             for k in range(3)] for i in range(steps + 1)]


def lay(entry):
    import regatta_pool as P
    params = entry.get("params") or {}
    _, _, course = P.lay(params)
    return course


def draw(entry, path, label=None):
    from PIL import Image, ImageDraw
    T = tokens()
    m = entry.get("metrics") or {}
    course = lay(entry)
    gates = course["gates"]
    neutral = float(m.get("neutral_share", (entry.get("params") or {}).get("NeutralShare", 0.0)))
    gaps = m.get("gaps") or []
    img = Image.new("RGB", (W, H), T["ground"])
    d = ImageDraw.Draw(img)
    c, u, v = plane([g["p"] for g in gates] + [q for lane in course["lanes"] for q in lane[::8]])
    pts = [q for lane in course["lanes"] for q in lane]
    proj = lambda q: (sum((q[k] - c[k]) * u[k] for k in range(3)), sum((q[k] - c[k]) * v[k] for k in range(3)))
    xy = [proj(q) for q in pts] + [proj(q) for g in gates for q in ring_points(g, 8)]
    xs, ys = [p[0] for p in xy], [p[1] for p in xy]
    box = (40, 60, 940, 860)
    sc = min((box[2] - box[0]) / (max(xs) - min(xs) + 1e-6), (box[3] - box[1]) / (max(ys) - min(ys) + 1e-6)) * 0.94
    cx, cy = (max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2
    to = lambda q: ((box[0] + box[2]) / 2 + (proj(q)[0] - cx) * sc, (box[1] + box[3]) / 2 - (proj(q)[1] - cy) * sc)
    d.rounded_rectangle(box, 14, outline=T["line"], width=1)

    for li, lane in enumerate(course["lanes"]):
        prev, prev_kind = None, None
        for q in lane + lane[:1]:
            leg, t = neutral_t(q, gates)
            kind = "gap" if any(int(g[0]) == leg and g[1] <= t <= g[2] for g in gaps) else (
                "neutral" if abs(t - 0.5) < 0.5 * neutral else "team")
            p = to(q)
            if prev is not None and kind != "gap" and prev_kind != "gap":
                col = T["neutral"] if kind == "neutral" else T["lanes"][li % 3]
                d.line([prev, p], fill=col, width=3 if kind == "team" else 2)
            prev, prev_kind = p, kind
    for i, g in enumerate(gates):
        ring = [to(q) for q in ring_points(g)]
        d.line(ring, fill=T["cta"] if i == 0 else T["text"], width=3 if i == 0 else 2)
        gx, gy = to(g["p"])
        d.text((gx + 10, gy - 30), str(i + 1), fill=T["cta"] if i == 0 else T["soft"], font=font(22))

    f_big, f, f_small = font(40), font(24), font(18)
    x0 = 980
    d.text((x0, 50), label or entry.get("name") or "Regatta course", fill=T["text"], font=f_big)
    rows = [("spread", f"{m.get('spread_x', 0):.2f}x  ({m.get('fastest', '?')} fastest, {m.get('slowest', '?')} slowest)"),
            ("chaos", "not measured" if m.get("chaos") is None else f"{m['chaos']:.3f}  (seeds: {m.get('seed_spreads')})"),
            ("race", f"{m.get('race_length', 0):.2f}x today's"),
            ("corners", f"hardest {max(course['turnDegrees']):.0f} deg, mean {sum(course['turnDegrees']) / len(gates):.0f} deg"),
            ("mouth", f"{min(g['r'] for g in gates):.0f} u tightest"),
            ("rail", f"{100 * (1 - neutral):.0f}% in team colour, {100 * neutral:.0f}% neutral" + (f", {len(gaps)} gaps" if gaps else "")),
            ("seed", f"step {m.get('seed_step', (entry.get('params') or {}).get('seed', 0))}, generator {m.get('generator_seed', course['seed'])}")]
    y = 120
    for k, val in rows:
        d.text((x0, y), k, fill=T["soft"], font=f_small)
        d.text((x0 + 110, y - 3), val, fill=T["text"], font=f_small if len(val) > 44 else f)
        y += 40
    secs = m.get("seconds") or {}
    scored = set(m.get("scored") or secs)
    if secs:
        y += 14
        d.text((x0, y), "seconds, three laps (model)", fill=T["soft"], font=f_small)
        y += 30
        top = max(secs.values())
        lo = min(t for h, t in secs.items() if h in scored)
        for h in sorted(secs, key=secs.get):
            t = secs[h]
            wbar = int(300 * t / top)
            col = T["cta"] if (h in scored and abs(t - lo) < 1e-6) else (T["lanes"][0] if h in scored else T["line"])
            d.rounded_rectangle((x0 + 110, y + 4, x0 + 110 + wbar, y + 24), 4, fill=col)
            d.text((x0, y), h, fill=T["text"] if h in scored else T["soft"], font=f_small)
            d.text((x0 + 120 + wbar, y), f"{t:.0f}" + ("" if h in scored else " (not scored)"), fill=T["soft"], font=f_small)
            y += 30
    d.text((box[0], box[3] + 8), "rings numbered from the start (lime); rails in team colours, neutral stretches grey",
           fill=T["soft"], font=f_small)
    d.text((x0, H - 60), "A MODEL (measured roster), not a real-game race.", fill=T["danger"], font=f_small)
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    img.save(path)
    return path


def sheet(paths, out, cols=2):
    from PIL import Image
    if not paths:
        return None
    ims = [Image.open(p) for p in paths]
    tw, th = W // 2, H // 2
    rows = (len(ims) + cols - 1) // cols
    canvas = Image.new("RGB", (tw * cols, th * rows), tokens()["ground"])
    for i, im in enumerate(ims):
        canvas.paste(im.resize((tw, th)), ((i % cols) * tw, (i // cols) * th))
    canvas.save(out)
    return out


def entries_of(doc):
    if "entries" in doc:
        return doc["entries"]
    if "cells" in doc:
        return [dict(e, name=f"cell {k}") for k, e in sorted(doc["cells"].items(), key=lambda kv: kv[1]["loss"])]
    return [doc]


def self_test():
    import tempfile
    import regatta_pool as P
    import regatta_measured_roster as mr
    bad = []
    roster = mr.roster()
    roster = {h: roster[h] for h in ("Manta", "Urchin")}
    ctx = {"root": ROOT, "args": {"roster": roster, "chaos_seeds": 1, "chaos_gate": 99}}
    P._CTX.clear()
    base = P.baseline(ctx["args"])
    e = {"params": dict(base, NeutralShare=0.4), **P.evaluate(dict(base, NeutralShare=0.4), ctx)}
    with tempfile.TemporaryDirectory() as tmp:
        a = draw(e, os.path.join(tmp, "a.png"))
        b = draw(dict(e, params=dict(e["params"], NeutralShare=0.0), metrics=dict(e["metrics"], neutral_share=0.0)),
                 os.path.join(tmp, "b.png"))
        from PIL import Image
        ia, ib = Image.open(a).convert("RGB"), Image.open(b).convert("RGB")
        T = tokens()
        grey = tuple(int(T["neutral"][i:i + 2], 16) for i in (1, 3, 5))
        count = lambda im: sum(1 for px in im.crop((40, 60, 940, 860)).get_flattened_data() if px == grey)
        if count(ia) < 200:
            bad.append(f"regatta_pool_render: a 0.4 neutral share drew {count(ia)} neutral pixels")
        if count(ib) > 0:        # negative control: no neutral share, no neutral stretch
            bad.append(f"regatta_pool_render: a course with no neutral share drew {count(ib)} neutral pixels")
        if sheet([a, b], os.path.join(tmp, "s.png")) is None:
            bad.append("regatta_pool_render: no contact sheet")
    P._CTX.clear()
    return bad


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("doc", nargs="?")
    ap.add_argument("--out", default=".")
    ap.add_argument("--limit", type=int, default=12)
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args()
    if a.self_test:
        problems = self_test()
        print("\n".join(problems) or "regatta_pool_render --self-test: ok")
        return 1 if problems else 0
    doc = json.load(open(a.doc, encoding="utf-8"))
    paths = []
    for i, e in enumerate(entries_of(doc)[:a.limit]):
        name = e.get("name") or f"course {i + 1}"
        paths.append(draw(e, os.path.join(a.out, f"course_{i + 1:02d}.png"), name))
        print(paths[-1])
    s = sheet(paths, os.path.join(a.out, "sheet.png"))
    if s:
        print(s)
    return 0


if __name__ == "__main__":
    sys.exit(main())
