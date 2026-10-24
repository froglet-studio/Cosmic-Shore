#!/usr/bin/env python3
"""
take_parity - does every part move in every take (and every morph) the way the REFERENCE file's
part does, and does every material carry the reference's values?

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


def disconnect_all(arm):
    """Unity has no connected bones: a take's translation on a bone whose head sits on its parent's
    tail (the Dolphin's root.002 slides 1.27 in startcharge) moves it in the game, while Blender
    ignores pose location on a CONNECTED bone. Disconnect so this reader sees what Unity plays."""
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    for eb in arm.data.edit_bones:
        eb.use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")

def armature_actions(arm):
    """(take, action, slot) for every action that animates the ARMATURE. The importer also makes a
    'Key|...' action per take for blend-shape curves (the artist Dolphin): keyed under the same take
    name it overwrote the armature take, and both readers measured a stale pose (2026-10-18)."""
    out = []
    for act in bpy.data.actions:
        slot = None
        if hasattr(act, "slots") and len(act.slots):
            slot = next((sl for sl in act.slots if sl.target_id_type == "OBJECT"), None)
            if slot is None:
                continue
        elif act.name.startswith("Key|"):
            continue
        out.append((act.name.split("|", 1)[-1], act, slot))
    return out

def assign(arm, act, slot):
    arm.animation_data.action = act
    if slot is not None:
        try:
            arm.animation_data.action_slot = slot
        except Exception:
            pass
import numpy as np
path, out = sys.argv[-2], sys.argv[-1]

def load(use_anim):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path, ignore_leaf_bones=False, automatic_bone_orientation=False, use_anim=use_anim)
    arm = [o for o in bpy.data.objects if o.type == "ARMATURE"][0]
    mesh = [o for o in bpy.data.objects if o.type == "MESH"][0]
    disconnect_all(arm)
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
rep = {"file": path, "diag": float(np.linalg.norm(hi - lo)), "groups": {}, "takes": {}, "shapes": {}}
for g, idx in members.items():
    P = rest[idx]
    c = P.mean(0)
    rep["groups"][g] = {"n": len(idx), "centre": c.tolist(), "radius": float(np.linalg.norm(P - c, axis=1).max())}
# every element morph at full weight, alone: per part the mean and the largest vertex displacement
if mesh.data.shape_keys:
    kbs = [k for k in mesh.data.shape_keys.key_blocks if k.name != mesh.data.shape_keys.reference_key.name]
    for k in kbs:
        for o in kbs:
            o.value = 0.0
        k.value = 1.0
        D = positions(mesh) - rest
        k.value = 0.0
        sh = {"max": float(np.linalg.norm(D, axis=1).max()), "parts": {}}
        for g, idx in members.items():
            d = D[idx]
            sh["parts"][g] = {"mean": d.mean(0).tolist(), "max": float(np.linalg.norm(d, axis=1).max())}
        rep["shapes"][k.name] = sh

# pass 2: every take, at its last frame (a pose take has one key; a motion take ends on its pose). The
# importer also builds actions for blend-shape curves (the artist Dolphin's takes key all four morphs at a
# constant 100); a take is compared as BONE motion, so the shape-key animation is cleared - the exporter
# strips those curves from our files, and an inflated reference read as every part landing elsewhere
arm, mesh = load(True)
if mesh.data.shape_keys and mesh.data.shape_keys.animation_data:
    mesh.data.shape_keys.animation_data_clear()
if mesh.data.shape_keys:
    for k in mesh.data.shape_keys.key_blocks:
        k.value = 0.0
if arm.animation_data is None:
    arm.animation_data_create()
acts = armature_actions(arm)
assert len({t for t, _a, _s in acts}) == len(acts), "two armature actions share a take name: %s" % [t for t, _a, _s in acts]
for take, act, slot in acts:
    assign(arm, act, slot)
    bpy.context.scene.frame_set(int(act.frame_range[1]))
    posed = positions(mesh)
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


def disconnect_all(arm):
    """Unity has no connected bones: a take's translation on a bone whose head sits on its parent's
    tail (the Dolphin's root.002 slides 1.27 in startcharge) moves it in the game, while Blender
    ignores pose location on a CONNECTED bone. Disconnect so this reader sees what Unity plays."""
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    for eb in arm.data.edit_bones:
        eb.use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")

def armature_actions(arm):
    """(take, action, slot) for every action that animates the ARMATURE. The importer also makes a
    'Key|...' action per take for blend-shape curves (the artist Dolphin): keyed under the same take
    name it overwrote the armature take, and both readers measured a stale pose (2026-10-18)."""
    out = []
    for act in bpy.data.actions:
        slot = None
        if hasattr(act, "slots") and len(act.slots):
            slot = next((sl for sl in act.slots if sl.target_id_type == "OBJECT"), None)
            if slot is None:
                continue
        elif act.name.startswith("Key|"):
            continue
        out.append((act.name.split("|", 1)[-1], act, slot))
    return out

def assign(arm, act, slot):
    arm.animation_data.action = act
    if slot is not None:
        try:
            arm.animation_data.action_slot = slot
        except Exception:
            pass
path, out = sys.argv[-2], sys.argv[-1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=path, ignore_leaf_bones=False, automatic_bone_orientation=False, use_anim=True)
arm = [o for o in bpy.data.objects if o.type == "ARMATURE"][0]
disconnect_all(arm)
if arm.animation_data is None:
    arm.animation_data_create()
rep = {"fps": bpy.context.scene.render.fps, "arm_world": [list(r) for r in arm.matrix_world], "takes": {}}
acts = armature_actions(arm)
assert len({t for t, _a, _s in acts}) == len(acts), "two armature actions share a take name: %s" % [t for t, _a, _s in acts]
for take, act, slot in acts:
    assign(arm, act, slot)
    f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
    frames = {}
    for f in range(f0, f1 + 1):
        bpy.context.scene.frame_set(f)
        bpy.context.view_layer.update()
        frames[f] = {pb.name: [list(r) for r in pb.matrix] for pb in arm.pose.bones}
    rep["takes"][take] = {"name": act.name, "frames": frames}
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


def compare_shapes(ref, cand, allow=None, rel=0.25, pos_frac=0.01):
    """
    Every reference morph must exist in the candidate and move every part as the reference's
    does: per part the mean displacement and the largest vertex displacement at full weight,
    within max(pos_frac * diagonal, rel * the reference value). `allow` = {shape: {parts} or
    {"*"}} for a morph changed on purpose (the Squirrel's strengthened Mass).
    """
    import numpy as np
    allow = allow or {}
    diag = ref["diag"]
    failures, rows = [], []
    for name, rs in sorted(ref.get("shapes", {}).items()):
        cs = cand.get("shapes", {}).get(name)
        if cs is None:
            failures.append("morph %r: the candidate has no such shape key" % name)
            continue
        ok = set(allow.get(name, ()))
        if "*" in ok:
            continue
        for g, rp in sorted(rs["parts"].items()):
            if g in ok:
                continue
            cp = cs["parts"].get(g)
            rm, rx = np.array(rp["mean"]), rp["max"]
            if cp is None:
                if rx > pos_frac * diag:
                    failures.append("morph %r: part %s moves %.2f in the reference but has no rigid part in the candidate" % (name, g, rx))
                continue
            cm, cx = np.array(cp["mean"]), cp["max"]
            if max(rx, cx) < pos_frac * diag:
                continue
            em, ex = float(np.linalg.norm(cm - rm)), abs(cx - rx)
            tol = max(pos_frac * diag, rel * rx)
            bad = em > tol or ex > tol
            rows.append((name, g, rx, cx, em, ex, bad))
            if bad:
                failures.append("morph %r: part %s %s (ref max %.2f, cand max %.2f, mean off by %.2f; tolerance %.2f)" % (
                    name, g, "holds still" if cx < 0.25 * rx else ("moves where the reference holds" if rx < 0.25 * cx else "moves differently"), rx, cx, em, tol))
    return failures, rows


MATERIAL_KEYS = ("DiffuseColor", "DiffuseFactor", "EmissiveColor", "EmissiveFactor", "TransparentColor",
                 "TransparencyFactor", "Opacity", "SpecularColor", "SpecularFactor", "Shininess")


def materials(path):
    """name -> {property: values} for every FBX material (the values Unity's importer builds an
    un-remapped slot's material from). AmbientColor is left out: Unity ignores it."""
    sys.path.insert(0, os.path.dirname(HERE))
    import fbx_binary as F
    nodes, _v, _f = F.read(path)
    objs = [n for n in nodes if n.name == "Objects"][0]
    out = {}
    for o in objs.children:
        if o.name != "Material":
            continue
        raw = o.props[1][1]
        name = (raw.decode("utf-8", "replace") if isinstance(raw, bytes) else str(raw)).split("\x00")[0]
        props = {}
        for c in o.children:
            if c.name == "Properties70":
                for pr in c.children:
                    vals = [x[1] for x in pr.props]
                    key = vals[0].decode() if isinstance(vals[0], bytes) else str(vals[0])
                    if key in MATERIAL_KEYS:
                        props[key] = [float(x) for x in vals[4:] if isinstance(x, (int, float))]
        out[name] = props
    return out


def compare_materials(ref_path, cand_path, tol=0.02):
    """Every reference material must exist in the candidate with the same Diffuse / Emissive /
    transparency / specular values (an un-remapped slot wears exactly these in the game)."""
    ref, cand = materials(ref_path), materials(cand_path)
    failures = []
    for name, rp in sorted(ref.items()):
        cp = cand.get(name)
        if cp is None:
            failures.append("material %r: missing in the candidate" % name)
            continue
        for k, rv in rp.items():
            cv = cp.get(k)
            if k == "EmissiveFactor" and not any(x > 1e-4 for x in rp.get("EmissiveColor", [1])):
                continue
            if cv is None:
                if any(abs(x) > tol for x in rv) and k not in ("SpecularColor", "SpecularFactor", "Shininess"):
                    failures.append("material %r: %s %s in the reference, absent in the candidate" % (name, k, rv))
                continue
            scale = max([1.0] + [abs(x) for x in rv])
            if len(cv) != len(rv) or max(abs(a - b) for a, b in zip(rv, cv)) > tol * scale:
                failures.append("material %r: %s %s, the reference has %s" % (name, k, [round(x, 3) for x in cv], [round(x, 3) for x in rv]))
    return failures


def compare_moving_nodes(ref_path, cand_path):
    """
    The second reader: for every take, the set of nodes whose curves move (fbx_probe, straight from
    the FBX curves, rotation offsets measured as angles) must be the same in both files. It needs no
    Blender evaluation, so a reader bug that makes BOTH files look the same in `compare` (a stale pose
    measured twice) cannot hide here: it caught the Dolphin's takes keyed to one constant pose.
    """
    sys.path.insert(0, HERE)
    import fbx_probe as P
    def moving(path):
        r = P.probe(path, want_takes=True)
        return {t["name"].split("|", 1)[-1]: set(t.get("moving_detail", {}).keys()) for t in r["takes"]}
    ref, cand = moving(ref_path), moving(cand_path)
    failures = []
    for take, rm in sorted(ref.items()):
        cm = cand.get(take)
        if cm is None:
            continue  # compare() reports a missing take
        if rm != cm:
            failures.append("take %r: the file moves %d node(s), the reference %d (only ours: %s; only the reference's: %s)" % (
                take, len(cm), len(rm), sorted(cm - rm)[:6], sorted(rm - cm)[:6]))
    return failures


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
    sfail, srows = compare_shapes(ref, cand)
    mfail = compare_materials(a.reference, a.candidate)
    nfail = compare_moving_nodes(a.reference, a.candidate)
    for f_ in nfail:
        print("  " + f_)
    failures = failures + nfail
    for f_ in sfail + mfail:
        print("  " + f_)
    print("morphs: %d part-morphs compared, %d difference(s); materials: %d difference(s)" % (len(srows), len(sfail), len(mfail)))
    failures = failures + sfail + mfail
    print("reference %s (diag %.2f), candidate %s" % (a.reference, ref["diag"], a.candidate))
    print("%-34s %-28s %9s %6s %9s %6s %8s %7s" % ("take", "part", "ref move", "ref deg", "cand move", "c deg", "pos err", "rot err"))
    for (take, g, dr, ar, dc, ac, pe, re_, bad) in rows:
        if a.all or bad:
            print("%-34s %-28s %9.3f %6.1f %9.3f %6.1f %8.3f %7.1f %s" % (take[:34], g[:28], dr, ar, dc, ac, pe, re_, "<-- DIFFERS" if bad else ""))
    if failures:
        print("FAIL: %d difference(s)" % len(failures))
        sys.exit(1)
    print("OK: every part moves in every take as the reference's does (%d moving part-takes compared)" % len(rows))


if __name__ == "__main__":
    main()
