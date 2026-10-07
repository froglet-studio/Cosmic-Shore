"""PNG figures for results/arms (PIL only: this container has no matplotlib).

  curve.png    catch rate over training: current-vs-current, current predator vs the pool, current prey vs the pool
  matrix.png   the cross-generation play matrix as a heat map (rows predator generation, columns prey generation)
  eco.png      open-economy populations over time (prey and predators per seed)

    python Tools/NCA/arms_plots.py --run runs/arms_a3
"""
from __future__ import annotations

import argparse
import json
import os

import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "results", "arms")
BG, FG, MUTE, GRID = (250, 250, 248), (30, 32, 40), (120, 124, 135), (225, 226, 230)
SERIES = [(32, 100, 200), (220, 110, 30), (40, 150, 90), (150, 60, 160)]


def line_chart(path, xs, series, title, ylab, W=900, H=420, labels=()):
    im = Image.new("RGB", (W, H), BG); d = ImageDraw.Draw(im)
    L, R, T, B = 60, 170, 36, 40
    ymax = max(max(s) for s in series) * 1.05 or 1
    xmin, xmax = min(xs), max(xs) or 1
    X = lambda x: L + (x - xmin) / max(xmax - xmin, 1e-9) * (W - L - R)
    Y = lambda y: H - B - y / ymax * (H - T - B)
    for k in range(6):
        y = ymax * k / 5
        d.line([(L, Y(y)), (W - R, Y(y))], fill=GRID); d.text((8, Y(y) - 6), f"{y:.0f}", fill=MUTE)
    for k in range(6):
        x = xmin + (xmax - xmin) * k / 5
        d.text((X(x) - 10, H - B + 8), f"{x:.0f}", fill=MUTE)
    for s, c in zip(series, SERIES):
        pts = [(X(x), Y(y)) for x, y in zip(xs, s)]
        d.line(pts, fill=c, width=2)
    for i, (lab, c) in enumerate(zip(labels, SERIES)):
        d.line([(W - R + 12, T + 14 + 18 * i), (W - R + 30, T + 14 + 18 * i)], fill=c, width=3)
        d.text((W - R + 36, T + 8 + 18 * i), lab, fill=FG)
    d.text((L, 10), title, fill=FG); d.text((8, 10), ylab, fill=MUTE)
    d.text((W // 2 - 30, H - 16), "generation", fill=MUTE)
    im.save(path)


def curve(run, out):
    L = [json.loads(l) for l in open(os.path.join(run, "log.jsonl"))]
    w = 20
    xs, a, b, c = [], [], [], []
    for i in range(0, len(L) - w + 1, w // 2):
        ch = L[i:i + w]
        xs.append(ch[len(ch) // 2]["gen"])
        a.append(np.mean([r["cur_catch_pm"] for r in ch])); b.append(np.mean([r["predVpool_catch_pm"] for r in ch]))
        c.append(np.mean([r["preyVpool_catch_pm"] for r in ch]))
    line_chart(os.path.join(out, "curve.png"), xs, [a, b, c], "Catch rate over training (catches per minute per encounter, "
               "20-generation means)", "catch/min",
               labels=("current vs current", "pred vs pool prey", "prey vs pool pred"))


def heat(path, M, gens, title):
    M = np.asarray(M)
    n = len(gens); cell = max(28, min(56, 640 // n))
    L, T = 80, 50
    W, H = L + cell * n + 30, T + cell * n + 60
    im = Image.new("RGB", (W, H), BG); d = ImageDraw.Draw(im)
    lo, hi = float(M.min()), float(M.max())
    for i in range(n):
        for j in range(n):
            u = (M[i, j] - lo) / max(hi - lo, 1e-9)
            col = tuple(int(a + (b - a) * u) for a, b in zip((235, 242, 250), (180, 40, 30)))
            x0, y0 = L + j * cell, T + i * cell
            d.rectangle([x0, y0, x0 + cell - 1, y0 + cell - 1], fill=col)
            d.text((x0 + 3, y0 + cell // 2 - 6), f"{M[i, j]:.0f}", fill=FG if u < 0.6 else (255, 255, 255))
        d.text((8, T + i * cell + cell // 2 - 6), f"P{gens[i]}", fill=FG)
        d.text((L + i * cell + 2, T - 16), f"{gens[i]}", fill=FG)
    d.text((L, 10), title, fill=FG)
    d.text((L, H - 40), "rows: predator generation   columns: prey generation   (catches per minute)", fill=MUTE)
    im.save(path)


def eco_png(ev, out):
    for key, e in (ev.get("eco") or {}).items():
        xs = [s["t"] for s in e["series"]]
        prey = np.array([s["prey"] for s in e["series"]]); pred = np.array([s["pred"] for s in e["series"]])
        if not ("sated1.25 (10 min)" in key or key in ("0:0", "1460:1460")):
            continue
        name = "".join(c if c.isalnum() else "_" for c in key).strip("_")
        line_chart(os.path.join(out, f"eco_{name}.png"), xs,
                   [prey.mean(1), pred.mean(1) * 10], f"Open economy {key}: population over time (mean of "
                   f"{prey.shape[1]} seeds)", "count", labels=("prey", "predators x10"))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--run", default=os.path.join(HERE, "runs", "arms_a3"))
    ap.add_argument("--out", default=OUT)
    a = ap.parse_args()
    curve(a.run, a.out)
    mp = os.path.join(a.out, "matrix.json")
    if os.path.exists(mp):
        m = json.load(open(mp))
        heat(os.path.join(a.out, "matrix.png"), m["catch_per_min"], m["gens"], "Cross-generation play matrix")
    ep = os.path.join(a.out, "eval.json")
    if os.path.exists(ep):
        eco_png(json.load(open(ep)), a.out)


if __name__ == "__main__":
    main()
