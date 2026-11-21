#!/usr/bin/env python3
"""
assembly_report.py - is a vessel model a HOVERING ASSEMBLY? How far each part floats from the others, measured.

Garrett, 2026-10-21, after flying the first new hulls: "the original vessels are based largely around the idea of free
floating parts that are associated and animated together but clearly distinct parts of a hovering assembly. this seems
to not be observed in the new models and needs correcting in how the vessel models are updated" - and "this will also
help keep the guns associated with the vessel but not have to worry about mating planar surfaces".

A PART is the set of vertices a bone owns (its dominant vertex group on the skinned mesh; on a part-per-mesh model, a
mesh object). For every part this prints its GAP: the shortest distance from any of its vertices to any vertex of
another part, as a percentage of the model's diagonal. A part whose gap is ~0 touches or intersects a neighbour (it is
welded on); a part with a clear gap floats.

    python3 Tools/Build/vessel_model/assembly_report.py "Assets/_Models/Vessel Models/Orca.fbx"
    python3 Tools/Build/vessel_model/assembly_report.py <fbx> --floating fin,wing.L.1,FastGunYaw+FastGunPitch --min-gap 0.4
    python3 Tools/Build/vessel_model/assembly_report.py --fleet        # the artist hulls' band
    python3 Tools/Build/vessel_model/assembly_report.py --self-test    # the gate fires on a welded pair, passes a floating one

A part spanning several bones (a turret's ring and yoke, an engine case and its bell) is listed as `a+b+c`: those bones'
vertices are measured as ONE part, so its own pieces touching each other is not a weld. With --floating, exits 1 when a listed part's gap is under --min-gap (default 0.4 % of the diagonal) or over --max-gap
(default 10 %, past which a part reads as detached rather than hovering), or when a listed part is missing. Without it,
it is a READER. The artist band (measured 2026-10-21): Manta wings 3.4-6.5 %, Dolphin wings 5.7 % and jaws 1.2-1.3 %,
Sparrow tail segments 0.8-1.1 % and shells 0.4-0.5 %, Squirrel wing pieces 0.9-1.2 % and engine tips 0.9 %, Serpent
tail spikes 0.7 %.
"""

import argparse
import os
import sys

import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
MODELS = os.path.join(ROOT, "Assets", "_Models", "Vessel Models")
# the artist hulls (rebuilt by the pipeline to the artists' geometry exactly, 2026-10-18)
FLEET = ["SparrowModel1.fbx", "Manta_shapekey_rigged.fbx", "dolphin_shapekey_with_animations.fbx",
         "SquirrelVessel_CosmicShoresTest1.fbx", "SerpentExport.fbx"]


def part_gaps(points: np.ndarray, labels: np.ndarray, sample: int = 400, others: int = 20000):
    """Per label: the shortest distance to a point of any other label. Brute force on samples, so a gap can read a
    little LARGE on a sparse part, never small."""
    out = {}
    for lab in np.unique(labels):
        mine, rest = points[labels == lab], points[labels != lab]
        if len(rest) == 0:
            continue
        a = mine[:: max(1, len(mine) // sample)]
        b = rest[:: max(1, len(rest) // others)]
        best = np.inf
        for k in range(0, len(a), 64):
            d = np.sqrt(((a[k:k + 64, None, :] - b[None, :, :]) ** 2).sum(-1)).min()
            best = min(best, d)
        out[lab] = (best, len(mine))
    return out


def read_model(path: str):
    import bpy
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    pts, labs = [], []
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    for ob in meshes:
        W = np.array(ob.matrix_world)
        names = {g.index: g.name for g in ob.vertex_groups}
        co = np.array([list(v.co) + [1.0] for v in ob.data.vertices]) @ W.T
        for i, v in enumerate(ob.data.vertices):
            gs = [g for g in v.groups if g.weight > 0]
            labs.append(names[max(gs, key=lambda g: g.weight).group] if gs and len(meshes) == 1 else ob.name)
            pts.append(co[i, :3])
    return np.array(pts), np.array(labs)


def report(path: str, floating=None, min_gap=0.4, max_gap=10.0, quiet=False):
    pts, labs = read_model(path)
    for name in floating or []:   # "a+b+c": one part over several bones
        bones = name.split("+")
        if len(bones) > 1:
            labs = np.where(np.isin(labs, bones), name, labs)
    diag = float(np.linalg.norm(pts.max(0) - pts.min(0)))
    gaps = part_gaps(pts, labs)
    rows = sorted(((g / diag * 100.0, n, lab) for lab, (g, n) in gaps.items()), key=lambda r: -r[1])
    if not quiet:
        print(f"== {os.path.basename(path)}: {len(rows)} parts, diagonal {diag:.2f}")
        for pct, n, lab in rows:
            print(f"   {lab:28s} {n:6d} verts  gap {pct:6.2f}%  {'floats' if pct >= min_gap else 'TOUCHES'}")
    floats = [r for r in rows if r[0] >= min_gap]
    if quiet:
        print(f"== {os.path.basename(path)}: {len(rows)} parts, diagonal {diag:.2f}")
    print(f"   {len(floats)}/{len(rows)} parts float (gap >= {min_gap}% of the diagonal); median gap "
          f"{np.median([r[0] for r in rows]):.2f}%")
    errs = []
    if floating:
        have = {lab: pct for pct, _, lab in rows}
        for name in floating:
            if name not in have:
                errs.append(f"{name}: no such part (no vertices weighted to it)")
            elif have[name] < min_gap:
                errs.append(f"{name}: gap {have[name]:.2f}% < {min_gap}% - it touches a neighbour (welded, not hovering)")
            elif have[name] > max_gap:
                errs.append(f"{name}: gap {have[name]:.2f}% > {max_gap}% - it reads as detached, not part of the assembly")
    return errs


def self_test():
    """Negative controls on synthetic parts: a welded pair must fail, a floating pair must pass."""
    g = np.linspace(-0.5, 0.5, 11)   # a unit cube's surface on a regular grid, as a mesh's vertices sit
    face = np.array([(u, v) for u in g for v in g])
    surf = np.vstack([np.column_stack([np.full(len(face), s), face]) for s in (-0.5, 0.5)]
                     + [np.column_stack([face[:, 0], np.full(len(face), s), face[:, 1]]) for s in (-0.5, 0.5)]
                     + [np.column_stack([face, np.full(len(face), s)]) for s in (-0.5, 0.5)])
    cube = lambda c: surf + np.array(c)
    fails = []
    for name, offset, want_float in (("welded", 1.0, False), ("floating", 1.12, True), ("detached", 4.0, None)):
        pts = np.vstack([cube((0, 0, 0)), cube((offset, 0, 0))])
        labs = np.array(["a"] * len(surf) + ["b"] * len(surf))
        diag = np.linalg.norm(pts.max(0) - pts.min(0))
        pct = part_gaps(pts, labs)["b"][0] / diag * 100
        floats = pct >= 0.4
        if want_float is not None and floats != want_float:
            fails.append(f"{name}: gap {pct:.2f}% read as {'floating' if floats else 'touching'}")
        if want_float is None and pct <= 10.0:
            fails.append(f"detached: gap {pct:.2f}% not over the 10% ceiling")
    print("assembly_report self-test: " + ("ok" if not fails else "FAILED\n  " + "\n  ".join(fails)))
    return 1 if fails else 0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("fbx", nargs="?")
    ap.add_argument("--floating", default="", help="comma-separated parts that must float")
    ap.add_argument("--min-gap", type=float, default=0.4)
    ap.add_argument("--max-gap", type=float, default=10.0)
    ap.add_argument("--fleet", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args()
    if a.self_test:
        return self_test()
    if a.fleet:
        for f in FLEET:
            report(os.path.join(MODELS, f), quiet=True)
        return 0
    if not a.fbx:
        ap.error("an FBX, --fleet or --self-test")
    errs = report(a.fbx, [p for p in a.floating.split(",") if p], a.min_gap, a.max_gap)
    for e in errs:
        print("FAIL " + e)
    if a.floating:
        print("assembly: " + ("OK" if not errs else f"{len(errs)} FAILURE(S)"))
    return 1 if errs else 0


if __name__ == "__main__":
    sys.exit(main())
