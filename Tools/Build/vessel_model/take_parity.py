#!/usr/bin/env python3
"""
take_parity - does every part move in every take the way the REFERENCE file's part does?

The gate that should have caught what Garrett saw on the first artist-hull Squirrel (2026-10-18):
"the rear jet ports are staying stationary, among other little differences like the roll twisting
the wing panels but not the wing". The pose-direction gate (`pose_expect`) checked one named part
per take; nothing compared the WHOLE take with the artist's. This does:

  for every take the reference has, for every bone-skinned part (vertex group) of the mesh,
  fit the part's rigid motion from the drawn rest to the take's pose (Kabsch on the vertices
  rigidly bound to that bone), and compare the candidate's motion with the reference's at the
  reference part's own centre: position error and rotation error. A part the reference moves
  that the candidate holds still, one the candidate moves the reference holds still, one that
  turns about a different axis (the panels twist where the wing should bank) - each fails.

The rest is the file AS DRAWN (no action, the parked pose, every shape key at 0), so a file that
bakes the park into its rest compares like for like with the artist's.

    python3 Tools/Build/vessel_model/take_parity.py <reference.fbx> <candidate.fbx> [--allow take:group,...]

Exit 0 = every take matches within tolerance; 1 = a difference (the table names it).
`retarget_dump(path)` is the other half: the reference's posed bone matrices per take and frame,
which `takes.retarget_from` keys onto a rebuilt armature so its takes ARE the artist's.
"""
import json
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))

_PROBE = r'''
import bpy, sys, json, math
import numpy as np
path, out = sys.argv[-2], sys.argv[-1]

def load(use_anim):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path, ignore_leaf_bones=False, automatic_bone_orientation=False, use_anim=use_anim)
    arm = [o for o in bpy.data.objects if o.type == "ARMATURE"][0]
    mesh = [o for o in bpy.data.objects if o.type == "MESH"][0]
    if mesh.data.shape_keys:
        for k in mesh.data.shape_keys.key_blocks:
            k.value = 0.0
    return arm, mesh

def positions(mesh):
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    ev = mesh.evaluated_get(dg)
    me = ev.to_mesh()
    mw = np.array(mesh.matrix_world)
    co = np.array([v.co[:] for v in me.vertices])
    ev.to_mesh_clear()
    return co @ mw[:3, :3].T + mw[:3, 3]

# pass 1: the drawn rest (no action; the parked pose; shape keys at 0) and the rigid bone membership
arm, mesh = load(False)
bones = {b.name for b in arm.data.bones}
rest = positions(mesh)
gname = {g.index: g.name for g in mesh.vertex_groups}
members = {}
for v in mesh.data.vertices:
    gs = [(gname[g.group], g.weight) for g in v.groups if gname.get(g.group) in bones and g.weight > 0.0]
    tot = sum(w for _, w in gs)
    if gs and tot > 0:
        g, w = max(gs, key=lambda x: x[1])
        if w / tot > 0.95:
            members.setdefault(g, []).append(v.index)
members = {g: idx for g, idx in members.items() if len(idx) >= 4}
lo, hi = rest.min(0), rest.max(0)
rep = {"file": path, "diag": float(np.linalg.norm(hi - lo)), "groups": {}, "takes": {}}
for g, idx in members.items():
    P = rest[idx]
    c = P.mean(0)
    rep["groups"][g] = {"n": len(idx), "centre": c.tolist(), "radius": float(np.linalg.norm(P - c, axis=1).max())}

# pass 2: every take, at its last frame (a pose take has one key; a motion take ends on its pose)
arm, mesh = load(True)
if arm.animation_data is None:
    arm.animation_data_create()
for act in bpy.data.actions:
    arm.animation_data.action = act
    if hasattr(act, "slots") and len(act.slots):
        try:
            arm.animation_data.action_slot = act.slots[0]
        except Exception:
            pass
    bpy.context.scene.frame_set(int(act.frame_range[1]))
    posed = positions(mesh)
    take = act.name.split("|", 1)[-1]
    tr = {}
    for g, idx in members.items():
        P, Q = rest[idx], posed[idx]
        cp, cq = P.mean(0), Q.mean(0)
        H = (P - cp).T @ (Q - cq)
        U, S, Vt = np.linalg.svd(H)
        d = np.sign(np.linalg.det(Vt.T @ U.T))
        R = Vt.T @ np.diag([1, 1, d]) @ U.T
        t = cq - R @ cp
        tr[g] = {"R": R.tolist(), "t": t.tolist()}
    rep["takes"][take] = tr
    arm.animation_data.action = None
json.dump(rep, open(out, "w"))
print("PARITY_PROBE_OK")
'''

_RETARGET = r'''
import bpy, sys, json
path, out = sys.argv[-2], sys.argv[-1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=path, ignore_leaf_bones=False, automatic_bone_orientation=False, use_anim=True)
arm = [o for o in bpy.data.objects if o.type == "ARMATURE"][0]
if arm.animation_data is None:
    arm.animation_data_create()
rep = {"fps": bpy.context.scene.render.fps, "arm_world": [list(r) for r in arm.matrix_world], "takes": {}}
for act in bpy.data.actions:
    arm.animation_data.action = act
    if hasattr(act, "slots") and len(act.slots):
        try:
            arm.animation_data.action_slot = act.slots[0]
        except Exception:
            pass
    f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
    frames = {}
    for f in range(f0, f1 + 1):
        bpy.context.scene.frame_set(f)
        bpy.context.view_layer.update()
        frames[f] = {pb.name: [list(r) for r in pb.matrix] for pb in arm.pose.bones}
    rep["takes"][act.name.split("|", 1)[-1]] = {"name": act.name, "frames": frames}
    arm.animation_data.action = None
json.dump(rep, open(out, "w"))
print("RETARGET_DUMP_OK")
'''


def _run(code, path, tag):
    import tempfile
    fd, out = tempfile.mkstemp(suffix=".json")
    os.close(fd)
    try:
        p = subprocess.run([sys.executable, "-c", code, path, out], capture_output=True, text=True, timeout=1800)
        if tag not in p.stdout:
            raise RuntimeError("%s failed on %s:\n%s" % (tag, path, (p.stdout + p.stderr)[-1500:]))
        return json.load(open(out))
    finally:
        os.remove(out)


def probe(path):
    """Per take, per bone-skinned part: the rigid motion (R, t) from the drawn rest to the take's pose."""
    return _run(_PROBE, path, "PARITY_PROBE_OK")


def retarget_dump(path):
    """Per take, per frame, every bone's posed armature-space matrix in the file as Unity plays it."""
    return _run(_RETARGET, path, "RETARGET_DUMP_OK")


def _angle_deg(R):
    import math
    tr = max(-1.0, min(3.0, R[0][0] + R[1][1] + R[2][2]))
    return math.degrees(math.acos(max(-1.0, min(1.0, (tr - 1.0) / 2.0))))


def compare(ref, cand, allow=None, pos_frac=0.015, rel=0.25, rot_floor=5.0):
    """
    Compare two probes. Returns (failures, rows). A part counts as MOVING in a take when its
    reference centre travels more than 1% of the diagonal or it turns more than 3 degrees.
    It FAILS when, evaluated at the reference part's centre, the candidate's motion differs by
    more than max(pos_frac * diagonal, rel * the reference travel) in position, or by more
    than max(rot_floor, rel * the reference angle) in rotation. `allow` = {take or '*': {groups}}
    for differences that are meant.
    """
    import numpy as np
    allow = allow or {}
    diag = ref["diag"]
    failures, rows = [], []
    for take, rtr in sorted(ref["takes"].items()):
        ctr = cand["takes"].get(take)
        if ctr is None:
            failures.append("take %r: the candidate has no such take" % take)
            continue
        ok_groups = set(allow.get(take, ())) | set(allow.get("*", ()))
        for g, rm in sorted(rtr.items()):
            if g in ok_groups:
                continue
            c = np.array(ref["groups"][g]["centre"])
            rad = ref["groups"][g]["radius"]
            Rr, tr_ = np.array(rm["R"]), np.array(rm["t"])
            dr = Rr @ c + tr_ - c
            ar = _angle_deg(Rr)
            cm = ctr.get(g)
            if cm is None:
                if np.linalg.norm(dr) > 0.01 * diag or ar > 3.0:
                    failures.append("take %r: part %s moves in the reference (%.2f, %.0f deg) but the candidate has no rigid part on that bone" % (take, g, np.linalg.norm(dr), ar))
                continue
            Rc, tc = np.array(cm["R"]), np.array(cm["t"])
            dc = Rc @ c + tc - c
            ac = _angle_deg(Rc)
            perr = float(np.linalg.norm(dc - dr))
            rerr = _angle_deg(Rr.T @ Rc)
            moving = np.linalg.norm(dr) > 0.01 * diag or ar > 3.0 or np.linalg.norm(dc) > 0.01 * diag or ac > 3.0
            if not moving:
                continue
            ptol = max(pos_frac * diag, rel * float(np.linalg.norm(dr)))
            rtol = max(rot_floor, rel * ar)
            bad = perr > ptol or rerr > rtol
            rows.append((take, g, float(np.linalg.norm(dr)), ar, float(np.linalg.norm(dc)), ac, perr, rerr, bad))
            if bad:
                what = []
                if np.linalg.norm(dr) > 0.01 * diag and np.linalg.norm(dc) < 0.25 * np.linalg.norm(dr):
                    what.append("holds still where the reference moves %.2f" % np.linalg.norm(dr))
                elif np.linalg.norm(dc) > 0.01 * diag and np.linalg.norm(dr) < 0.25 * np.linalg.norm(dc):
                    what.append("moves %.2f where the reference holds still" % np.linalg.norm(dc))
                else:
                    what.append("lands %.2f from where the reference puts it (tolerance %.2f)" % (perr, ptol))
                if rerr > rtol:
                    what.append("turns %.0f deg away from the reference's rotation (ref %.0f, cand %.0f)" % (rerr, ar, ac))
                failures.append("take %r: part %s %s" % (take, g, "; ".join(what)))
    for take in cand["takes"]:
        if take not in ref["takes"]:
            rows.append((take, "(new take, no reference)", 0, 0, 0, 0, 0, 0, False))
    return failures, rows


def main():
    import argparse
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("reference")
    ap.add_argument("candidate")
    ap.add_argument("--allow", default="", help="take:group pairs, comma separated ('*:group' for every take)")
    ap.add_argument("--all", action="store_true", help="print every moving part, not only the failures")
    a = ap.parse_args()
    allow = {}
    for item in filter(None, a.allow.split(",")):
        t, g = item.split(":", 1)
        allow.setdefault(t, set()).add(g)
    ref, cand = probe(a.reference), probe(a.candidate)
    failures, rows = compare(ref, cand, allow)
    print("reference %s (diag %.2f), candidate %s" % (a.reference, ref["diag"], a.candidate))
    print("%-34s %-28s %9s %6s %9s %6s %8s %7s" % ("take", "part", "ref move", "ref deg", "cand move", "c deg", "pos err", "rot err"))
    for (take, g, dr, ar, dc, ac, pe, re_, bad) in rows:
        if a.all or bad:
            print("%-34s %-28s %9.3f %6.1f %9.3f %6.1f %8.3f %7.1f %s" % (take[:34], g[:28], dr, ar, dc, ac, pe, re_, "<-- DIFFERS" if bad else ""))
    if failures:
        print("FAIL: %d difference(s) across %d take(s)" % (len(failures), len({f.split("'")[1] for f in failures if "'" in f})))
        sys.exit(1)
    print("OK: every part moves in every take as the reference's does (%d moving part-takes compared)" % len(rows))


if __name__ == "__main__":
    main()
