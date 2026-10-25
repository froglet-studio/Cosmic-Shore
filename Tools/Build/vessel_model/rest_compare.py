#!/usr/bin/env python3
"""
rest_compare.py - do two FBX files put the same bones in the same places?

A prefab mounts jets, colliders and FX on BONES by name, with offsets authored in bone space.
A replacement model that keeps the bone names but moves or re-orients a bone silently moves
everything hung on it. This compares the WORLD rest transform of every bone and every node
between two files and reports the largest position and orientation differences per node.

Both files are read by BLENDER'S FBX importer (each in its own fresh process), not by hand-
rolled matrix math: the first version of this script evaluated the FBX node transforms itself
and disagreed with Blender by a constant 10.8 degrees on every bone of the shipped Squirrel -
the artist's armature carries a (71.9, -180, 0) object rotation and the bones' Lcl Rotations
compose with it in a way the naive T*R*S walk got wrong. A reader that already agrees with
the FBX SDK on every artist file in the repo is the reference, not a re-derivation of it.

    python3 Tools/Build/vessel_model/rest_compare.py artist.fbx rebuilt.fbx [--tol-pos 0.02] [--tol-deg 1.0] [--allow-moved a,b]

Two readers, both must say 0: Blender (each bone AS DRAWN, i.e. its posed matrix: the artist
files park their node-vs-bind difference as a pose) and assimp (the node chain composed the way
an engine does). A file whose bones only match in the BIND pose fails the assimp reader.

Exit 1 when any node shared by both files moves more than the tolerances. Nodes only one file
has are listed; a LOST node (in the first file, not the second) fails.
"""

import argparse
import json
import math
import os
import subprocess
import sys

_READER = r'''
import bpy, sys, json, math
from mathutils import Vector
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=sys.argv[-1], ignore_leaf_bones=False, automatic_bone_orientation=False, use_anim=False)
out = {}
for o in bpy.data.objects:
    mw = o.matrix_world
    out[o.name] = {"kind": o.type, "pos": list(mw.to_translation()), "quat": list(mw.to_quaternion()), "scale": list(mw.to_scale())}
    if o.type == "ARMATURE":
        for b in o.data.bones:
            # the bone AS DRAWN: Blender derives the rest from the skin's bind matrix and parks the
            # file's node-vs-bind difference as a pose, so the posed matrix (rest x parked pose) is
            # what the node chain holds and what Unity builds the bone Transform from. A file with
            # no parked pose gives the same numbers as the rest.
            pb = o.pose.bones[b.name]
            mp = mw @ pb.matrix
            head = mp.to_translation()
            axis = (mp.to_3x3() @ Vector((0.0, 1.0, 0.0))).normalized()
            q = mp.to_quaternion()
            out["bone:" + b.name] = {"kind": "BONE", "pos": list(head), "axis": list(axis), "quat": list(q),
                                     "parent": b.parent.name if b.parent else None,
                                     "bind_pos": list(mw @ b.head_local)}
print("REST_JSON " + json.dumps(out))
'''


def read_rest(path):
    out = subprocess.run([sys.executable, "-c", _READER, path], capture_output=True, text=True, timeout=600)
    for line in out.stdout.splitlines():
        if line.startswith("REST_JSON "):
            return json.loads(line[len("REST_JSON "):])
    raise RuntimeError("blender could not read %s:\n%s" % (path, (out.stdout + out.stderr)[-1500:]))


def _qangle(a, b):
    import bpy  # noqa: F401  (registers mathutils)
    from mathutils import Quaternion
    qa, qb = Quaternion(a), Quaternion(b)
    ang = math.degrees(qa.rotation_difference(qb).angle)
    return 360 - ang if ang > 180 else ang


def compare(a_path, b_path, tol_pos=0.02, tol_deg=1.0, verbose=True, allow_scale_only=False):
    ra, rb = read_rest(a_path), read_rest(b_path)
    shared = sorted(set(ra) & set(rb))
    only_a, only_b = sorted(set(ra) - set(rb)), sorted(set(rb) - set(ra))
    rows, bad = [], []
    for n in shared:
        pa, pb = ra[n]["pos"], rb[n]["pos"]
        dp = math.sqrt(sum((pa[i] - pb[i]) ** 2 for i in range(3)))
        dang = _qangle(ra[n]["quat"], rb[n]["quat"])
        sa, sb = ra[n].get("scale", [1, 1, 1]), rb[n].get("scale", [1, 1, 1])
        if max(abs(x - y) / max(abs(x), 1e-9) for x, y in zip(sa, sb)) > 0.01 and not allow_scale_only:
            dang = max(dang, tol_deg + 1.0)  # a scale change fails like a rotation would; the printout shows both files
        rows.append((n, dp, dang, pa, pb))
        if dp > tol_pos or dang > tol_deg:
            bad.append((n, dp, dang))
    if verbose:
        print("shared %d, only in A %d %s, only in B %d %s" % (len(shared), len(only_a), only_a[:6], len(only_b), only_b[:6]))
        rows.sort(key=lambda r: -(r[1] + r[2] / 100))
        for (n, dp, dang, pa, pb) in rows[:16]:
            flag = "  <-- MOVED" if (dp > tol_pos or dang > tol_deg) else ""
            print("  %-32s dpos %.4f  dang %6.2f deg  A %s  B %s%s" % (
                n, dp, dang, tuple(round(v, 3) for v in pa), tuple(round(v, 3) for v in pb), flag))
    return bad, only_a, only_b


_ASSIMP_NOTE = """
assimp reads the node chain the way an engine does (Lcl Translation / Rotation / Scaling with any
PreRotation, composed down the hierarchy), while Blender's importer derives bone rest from the
skin's bind matrices. A file can agree with one reader and not the other; both must say 0.
"""


def assimp_chain(path):
    """Every node's world matrix from assimp's assxml export (None if assimp or numpy is missing)."""
    import re
    import shutil
    import tempfile
    if not shutil.which("assimp"):
        return None
    try:
        import numpy as np
    except ImportError:
        return None
    tmp = tempfile.mktemp(suffix=".assxml")
    try:
        subprocess.run(["assimp", "export", path, tmp], check=True, capture_output=True, timeout=300)
        txt = open(tmp).read()
    finally:
        if os.path.exists(tmp):
            os.remove(tmp)
    stack, out = [], {}
    for m in re.finditer(r'<Node name="([^"]*)">|</Node>|<Matrix4>\s*([-0-9.eE\s]+?)\s*</Matrix4>', txt):
        tok = m.group(0)
        if tok.startswith("<Node"):
            stack.append([m.group(1), None])
        elif tok.startswith("</Node"):
            stack.pop()
        elif stack and stack[-1][1] is None:
            vals = [float(x) for x in m.group(2).split()]
            if len(vals) != 16:
                continue
            local = np.array(vals).reshape(4, 4)
            parent = stack[-2][1] if len(stack) > 1 and stack[-2][1] is not None else np.eye(4)
            stack[-1][1] = parent @ local
            out[stack[-1][0]] = stack[-1][1]
    return out


def compare_assimp(a_path, b_path, tol_pos=0.02, tol_deg=1.0, allow_scale_only=False):
    """Returns (moved, lost) node name lists from assimp's reading, or None if assimp is unavailable."""
    import numpy as np
    ca, cb = assimp_chain(a_path), assimp_chain(b_path)
    if ca is None or cb is None:
        return None
    moved, lost = [], []
    for n, ma in ca.items():
        if n not in cb:
            lost.append(n)
            continue
        mb = cb[n]
        dp = float(np.linalg.norm(ma[:3, 3] - mb[:3, 3]))
        ra, rb = ma[:3, :3], mb[:3, :3]
        ra = ra / np.cbrt(abs(np.linalg.det(ra)) or 1.0)
        rb = rb / np.cbrt(abs(np.linalg.det(rb)) or 1.0)
        c = max(-1.0, min(1.0, (np.trace(ra.T @ rb) - 1) / 2))
        dang = math.degrees(math.acos(c))
        sa = [float(np.linalg.norm(ma[:3, i])) for i in range(3)]
        sb = [float(np.linalg.norm(mb[:3, i])) for i in range(3)]
        ds = max(abs(x - y) / max(abs(x), 1e-9) for x, y in zip(sa, sb))
        if dp > tol_pos or dang > tol_deg or (ds > 0.01 and not allow_scale_only):
            moved.append(n)
            print("  assimp: %-28s dpos %.4f  dang %6.2f deg  dscale %.3f  <-- MOVED" % (n, dp, dang, ds))
        elif ds > 0.01:
            print("  assimp: %-28s scale differs by %.3f (position and rotation match; accepted by --allow-scale-only)" % (n, ds))
    return moved, lost


def main():
    ap = argparse.ArgumentParser(description=__doc__ + _ASSIMP_NOTE, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("a")
    ap.add_argument("b")
    ap.add_argument("--tol-pos", type=float, default=0.02)
    ap.add_argument("--tol-deg", type=float, default=1.0)
    ap.add_argument("--no-assimp", action="store_true", help="skip the assimp node-chain reader")
    ap.add_argument("--allow-moved", default="", help="comma-separated bone names a spec moved on purpose (rig.move_bones)")
    ap.add_argument("--allow-scale-only", action="store_true",
                    help="accept nodes whose position and rotation match but whose node SCALE differs (an artist rig's node scale baked into geometry and bone lengths: a Blender bone cannot rest at a scale)")
    a = ap.parse_args()
    allowed = {n.strip() for n in a.allow_moved.split(",") if n.strip()}
    bad, only_a, only_b = compare(a.a, a.b, a.tol_pos, a.tol_deg, allow_scale_only=a.allow_scale_only)
    def _name(x):
        return (x[0] if isinstance(x, (tuple, list)) else str(x)).replace("bone:", "").replace("assimp:", "")
    bad = [n for n in bad if _name(n) not in allowed]
    if not a.no_assimp:
        res = compare_assimp(a.a, a.b, a.tol_pos, a.tol_deg, allow_scale_only=a.allow_scale_only)
        if res is None:
            print("assimp reader: not available (assimp binary or numpy missing)")
        else:
            moved, lost = res
            print("assimp reader: %d node(s) moved, %d lost" % (len(moved), len(lost)))
            bad = list(bad) + [("assimp:" + n) for n in moved if n not in allowed]
            only_a = list(only_a) + [("assimp:" + n) for n in lost]
    lost = list(only_a)  # a lost mesh or armature node detaches the prefab's stripped stubs as silently as a lost bone
    if lost:
        print("LOST nodes (in A, not in B): %s" % lost)
    if bad or lost:
        print("FAIL: %d node(s) moved, %d node(s) lost" % (len(bad), len(lost)))
        sys.exit(1)
    print("OK: every shared node rests within %.3f units / %.1f deg" % (a.tol_pos, a.tol_deg))


if __name__ == "__main__":
    main()
