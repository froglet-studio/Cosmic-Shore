"""
pose_clearance - does a part that ANIMATES keep its clearance in every pose? (A READER: it writes nothing.)

`assembly_report.py` measures the REST pose. A part on a gimbal (the Ark's jets vectoring through pitch, yaw and roll,
a hangar door swinging open) can float clear at rest and still sweep into a neighbour mid-manoeuvre, which no rest
gate sees. This imports the FBX, plays each take it is asked about (the fleet's single-frame pose takes, and the last
key of a motion take), evaluates the skinned mesh in that pose, and measures each named part's gap to its nearest
OTHER part, in % of the REST diagonal, with the same sampling as the assembly gate (`assembly_report.part_gaps`).

    python3 Tools/Build/vessel_model/pose_clearance.py <fbx> --parts engine.0,engine.1 --takes yawleft,pitchup
    python3 Tools/Build/vessel_model/pose_clearance.py <fbx> --parts "engine.*" --takes all
    python3 Tools/Build/vessel_model/pose_clearance.py --self-test

`--parts` takes names or `prefix.*` globs; `--takes all` plays every action in the file; `--min-gap` defaults to the
assembly gate's 0.4 %. Exit 1 when any named part comes within --min-gap of a neighbour in any named take.
"""

import argparse
import fnmatch
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from assembly_report import part_gaps  # noqa: E402


def _labels(ob):
    names = {g.index: g.name for g in ob.vertex_groups}
    out = []
    for v in ob.data.vertices:
        gs = [g for g in v.groups if g.weight > 0]
        out.append(names[max(gs, key=lambda g: g.weight).group] if gs else ob.name)
    return np.array(out)


def _posed_points(bpy, mesh, arm, act):
    ad = arm.animation_data or arm.animation_data_create()
    ad.action = act
    if act is not None and hasattr(act, "slots") and len(act.slots):
        try:
            ad.action_slot = act.slots[0]
        except Exception:
            pass
    frame = int(act.frame_range[1]) if act is not None else 1
    bpy.context.scene.frame_set(frame)
    dg = bpy.context.evaluated_depsgraph_get()
    ev = mesh.evaluated_get(dg)
    me = ev.to_mesh()
    W = np.array(ev.matrix_world)
    co = np.array([list(v.co) + [1.0] for v in me.vertices]) @ W.T
    ev.to_mesh_clear()
    return co[:, :3]


def measure(path, parts, takes, min_gap=0.4):
    import bpy
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path, use_anim=True, ignore_leaf_bones=False, automatic_bone_orientation=False)
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    arms = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    if len(meshes) != 1 or len(arms) != 1:
        return ["expects one skinned mesh and one armature, found %d / %d" % (len(meshes), len(arms))], []
    mesh, arm = meshes[0], arms[0]
    labs = _labels(mesh)
    have = sorted(set(labs.tolist()))
    want = sorted({n for p in parts for n in have if fnmatch.fnmatchcase(n, p)})
    errs = [f"--parts {p}: matches no part" for p in parts if not any(fnmatch.fnmatchcase(n, p) for n in have)]
    acts = {a.name.split("|")[-1]: a for a in bpy.data.actions}
    names = sorted(acts) if takes == ["all"] else takes
    errs += [f"--takes {t}: the file has no such take (has {sorted(acts)})" for t in names if t not in acts]
    rest = _posed_points(bpy, mesh, arm, None)
    diag = float(np.linalg.norm(rest.max(0) - rest.min(0)))
    rows = []
    for t in ["(rest)"] + [n for n in names if n in acts]:
        pts = rest if t == "(rest)" else _posed_points(bpy, mesh, arm, acts[t])
        gaps = part_gaps(pts, labs)
        for p in want:
            g = gaps[p][0] / diag * 100.0
            rows.append((t, p, g))
            if g < min_gap:
                errs.append(f"{t}: {p} comes {g:.2f}% of the diagonal from a neighbour (< {min_gap}%): it collides in that pose")
    return errs, rows


def self_test():
    """Negative controls on synthetic parts: a part that swings into its neighbour must fail, one that clears must pass."""
    g = np.linspace(-0.5, 0.5, 9)
    face = np.array([(u, v) for u in g for v in g])
    surf = np.vstack([np.column_stack([np.full(len(face), s), face]) for s in (-0.5, 0.5)]
                     + [np.column_stack([face[:, 0], np.full(len(face), s), face[:, 1]]) for s in (-0.5, 0.5)]
                     + [np.column_stack([face, np.full(len(face), s)]) for s in (-0.5, 0.5)])
    labs = np.array(["fixed"] * len(surf) + ["swing"] * len(surf))
    fails = []
    for name, offset, want_ok in (("clears", 1.3, True), ("swings in", 1.0, False)):
        pts = np.vstack([surf, surf + np.array((offset, 0, 0))])
        diag = float(np.linalg.norm(np.array((2.5, 1, 1))))
        pct = part_gaps(pts, labs)["swing"][0] / diag * 100
        if (pct >= 0.4) != want_ok:
            fails.append(f"{name}: gap {pct:.2f}% read the wrong way")
    print("pose_clearance self-test: " + ("ok" if not fails else "FAILED\n  " + "\n  ".join(fails)))
    return 1 if fails else 0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("fbx", nargs="?")
    ap.add_argument("--parts", default="", help="comma-separated part names or prefix.* globs that animate")
    ap.add_argument("--takes", default="all", help="comma-separated take names (after the 'rig|'), or 'all'")
    ap.add_argument("--min-gap", type=float, default=0.4)
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else None)
    if a.self_test:
        return self_test()
    if not a.fbx or not a.parts:
        ap.error("an fbx and --parts are required")
    errs, rows = measure(a.fbx, [p for p in a.parts.split(",") if p], [t for t in a.takes.split(",") if t], a.min_gap)
    worst = {}
    for t, p, g in rows:
        if t not in worst or g < worst[t][1]:
            worst[t] = (p, g)
    print(f"== {os.path.basename(a.fbx)}: {len({p for _, p, _ in rows})} animated parts over {len(worst)} poses")
    for t, (p, g) in worst.items():
        print(f"   {t:14s} tightest {p:16s} {g:5.2f}%  {'clear' if g >= a.min_gap else 'COLLIDES'}")
    for e in errs:
        print("FAIL " + e)
    print("pose_clearance: " + ("OK" if not errs else f"{len(errs)} FAILURE(S)"))
    return 1 if errs else 0


if __name__ == "__main__":
    sys.exit(main())
