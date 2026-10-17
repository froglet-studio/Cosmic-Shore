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

    python3 Tools/Build/vessel_model/rest_compare.py shipped.fbx rebuilt.fbx [--tol-pos 0.02] [--tol-deg 1.0]

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
    out[o.name] = {"kind": o.type, "pos": list(mw.to_translation()), "quat": list(mw.to_quaternion())}
    if o.type == "ARMATURE":
        for b in o.data.bones:
            head = mw @ b.head_local
            tail = mw @ b.tail_local
            axis = (tail - head).normalized()
            # the bone's full orientation: its rest matrix carried into world space
            q = (mw.to_3x3() @ b.matrix_local.to_3x3()).to_quaternion()
            out["bone:" + b.name] = {"kind": "BONE", "pos": list(head), "axis": list(axis), "quat": list(q),
                                     "parent": b.parent.name if b.parent else None}
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


def compare(a_path, b_path, tol_pos=0.02, tol_deg=1.0, verbose=True):
    ra, rb = read_rest(a_path), read_rest(b_path)
    shared = sorted(set(ra) & set(rb))
    only_a, only_b = sorted(set(ra) - set(rb)), sorted(set(rb) - set(ra))
    rows, bad = [], []
    for n in shared:
        pa, pb = ra[n]["pos"], rb[n]["pos"]
        dp = math.sqrt(sum((pa[i] - pb[i]) ** 2 for i in range(3)))
        dang = _qangle(ra[n]["quat"], rb[n]["quat"])
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


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("a")
    ap.add_argument("b")
    ap.add_argument("--tol-pos", type=float, default=0.02)
    ap.add_argument("--tol-deg", type=float, default=1.0)
    a = ap.parse_args()
    bad, only_a, only_b = compare(a.a, a.b, a.tol_pos, a.tol_deg)
    lost = [n for n in only_a if n.startswith("bone:")]
    if lost:
        print("LOST bones (in A, not in B): %s" % lost)
    if bad or lost:
        print("FAIL: %d node(s) moved, %d bone(s) lost" % (len(bad), len(lost)))
        sys.exit(1)
    print("OK: every shared node rests within %.3f units / %.1f deg" % (a.tol_pos, a.tol_deg))


if __name__ == "__main__":
    main()
