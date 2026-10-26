"""Turn the hybrid NCA's whale-jelly chimera into the Tandava chimera's two body plans (TANDAVA.md §3.13).

The chimera is not drawn by hand: it is GROWN. The hybrid 3D NCA (branch `claude/hybrid-creatures`, Tools/NCA/hybrid3d.py:
one rule for lizard, whale and jelly, the form chosen by genome channels) was spliced - a grown whale's head half on a
grown jelly's tail half - and left to run; the body it grows keeps reshaping between a jelly-bell-headed whale and a
whale (the lab's `splice_WJ` probe). Two moments of that recording, each an 8-pose swim cycle of live voxels with their
genomes, are this script's input; its output is the committed `tandava_chimera.json` that tandava_plans.py bakes into
`chimera_whale` and `chimera_jelly` (pure Python - the bake never needs numpy).

Per shape: the body's SKIN voxels (live, with a dead 6-neighbour) are scaled to plan voxels, N units are picked by
farthest-point sampling on frame 0 (an even spread, never closer than tandava_plans.MIN_GAP), and every later frame's
units are the same members carried to that frame's skin (each to its nearest unclaimed skin point, nearest pairs first).
Both shapes share one census - the same members re-arranged, so turning is a pose change: Space for the most jelly-genome
units (the bell), Time for the next (its trailing threads), a few Charge at the whale's fluke end, Mass for the rest.

    python3 Tools/Build/tandava_chimera_extract.py <chimera.json from the probe> [--units 190]
"""
from __future__ import annotations

import argparse
import json
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "tandava_chimera.json")
MIN_GAP = 2.1
CENSUS = {"charge": 10, "space": 0.26, "time": 0.1}   # Charge a count; Space and Time shares of N; Mass the rest


def skin(vox):
    """The live voxels with at least one dead 6-neighbour."""
    s = {tuple(v) for v in vox}
    out = []
    for v in vox:
        x, y, z = v
        if any((x + dx, y + dy, z + dz) not in s for dx, dy, dz in
               ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1))):
            out.append(v)
    return out


def fps(points, n, seed_ix=0):
    """Farthest-point sampling: n indices into points, each the farthest from those already picked."""
    p = np.asarray(points, dtype=np.float64)
    picked = [seed_ix]
    d = np.linalg.norm(p - p[seed_ix], axis=1)
    for _ in range(n - 1):
        i = int(np.argmax(d))
        picked.append(i)
        d = np.minimum(d, np.linalg.norm(p - p[i], axis=1))
    return picked


def carry(prev, cand):
    """Each unit of prev to its nearest unclaimed candidate, nearest pairs first (a greedy assignment)."""
    prev = np.asarray(prev); cand = np.asarray(cand)
    d = np.linalg.norm(prev[:, None, :] - cand[None, :, :], axis=2)
    order = np.dstack(np.unravel_index(np.argsort(d, axis=None), d.shape))[0]
    out = [None] * len(prev); used_u = set(); used_c = set()
    for u, c in order:
        if u in used_u or c in used_c:
            continue
        out[u] = cand[c].tolist(); used_u.add(u); used_c.add(c)
        if len(used_u) == len(prev):
            break
    return out


def spread(units, gap):
    """Push apart any two units closer than gap (a few relaxation passes; the skin is dense, so it barely moves)."""
    u = np.asarray(units, dtype=np.float64)
    for _ in range(40):
        moved = False
        d = np.linalg.norm(u[:, None, :] - u[None, :, :], axis=2) + np.eye(len(u)) * 1e9
        close = np.argwhere(d < gap)
        for a, b in close:
            if a >= b:
                continue
            v = u[a] - u[b]; L = np.linalg.norm(v) or 1e-3
            push = (gap - L) / 2 + 0.01
            u[a] += v / L * push; u[b] -= v / L * push; moved = True
        if not moved:
            break
    return u.tolist()


def shape(frames, n, scale, axes, length_axis_sign):
    """frames: list of voxel lists [(a, b, c, ..., gWhale, gJelly)]. Returns (units per frame, jelly genome per unit,
    position along the body per unit) in plan axes (x forward, y up, z side)."""
    def to_plan(v):
        c = [v[axes["x"]], v[axes["y"]], v[axes["z"]]]
        return [length_axis_sign * c[0] * scale, c[1] * scale, c[2] * scale]
    sk0 = skin([tuple(int(t) for t in v[:3]) for v in frames[0]])
    gen = {tuple(int(t) for t in v[:3]): v[-1] for v in frames[0]}
    pts0 = [to_plan(v) for v in sk0]
    pick = fps(pts0, n)
    units = [[pts0[i] for i in pick]]
    jelly = [float(gen[sk0[i]]) for i in pick]
    for f in frames[1:]:
        sk = skin([tuple(int(t) for t in v[:3]) for v in f])
        units.append(carry(units[-1], [to_plan(v) for v in sk]))
    units = [spread(u, MIN_GAP + 0.05) for u in units]
    return units, jelly


def census_for(units, jelly):
    """One element per unit: Space the most jelly, Time the next, Charge the rearmost (the fluke), Mass the rest."""
    n = len(jelly)
    n_space = round(CENSUS["space"] * n); n_time = round(CENSUS["time"] * n); n_charge = CENSUS["charge"]
    elem = [1] * n   # Mass
    by_jelly = sorted(range(n), key=lambda i: -jelly[i])
    for i in by_jelly[:n_space]:
        elem[i] = 2
    for i in by_jelly[n_space:n_space + n_time]:
        elem[i] = 3
    rear = sorted((i for i in range(n) if elem[i] == 1), key=lambda i: units[0][i][0])
    for i in rear[:n_charge]:
        elem[i] = 0
    return elem


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("src")
    ap.add_argument("--units", type=int, default=190)
    ap.add_argument("--scale", type=float, default=2.4)
    ap.add_argument("--axes", default="x=2,y=0,z=1", help="which voxel coord is plan x (forward), y (up), z (side)")
    ap.add_argument("--flip", type=int, default=1, help="+1/-1: the sign that points plan x at the head")
    a = ap.parse_args()
    axes = {k: int(v) for k, v in (kv.split("=") for kv in a.axes.split(","))}
    src = json.load(open(a.src))
    out = {"source": "hybrid NCA splice_WJ (claude/hybrid-creatures, model_04000), via Tools/Build/tandava_chimera_extract.py",
           "units": a.units, "scale": a.scale, "shapes": {}}
    for name in ("whale", "jelly"):
        frames = src[name]["frames"]
        frames = (frames * 8)[:8] if len(frames) < 8 else frames[:8]
        units, jelly = shape(frames, a.units, a.scale, axes, a.flip)
        elem = census_for(units, jelly)
        # centre on frame 0
        c = [sum(u[k] for u in units[0]) / len(units[0]) for k in range(3)]
        out["shapes"][name] = {
            "step": src[name].get("step"),
            "elem": elem,
            "jelly": [round(j, 3) for j in jelly],
            "pos": [[[round(u[k] - c[k], 3) for k in range(3)] for u in fr] for fr in units],
        }
        ext = np.ptp(np.asarray(units[0]), axis=0)
        print(f"{name}: {len(units[0])} units, census {[elem.count(e) for e in range(4)]}, extent {np.round(ext, 1).tolist()} plan voxels")
    json.dump(out, open(OUT, "w"), separators=(",", ":"))
    print("wrote", OUT, os.path.getsize(OUT), "bytes")


if __name__ == "__main__":
    main()
