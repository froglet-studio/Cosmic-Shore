#!/usr/bin/env python3
"""
part_map.py - where every part of a shipped model sits, measured, so a replacement lands on it.

Imports an FBX into a fresh Blender scene (so the file's own axis conversion is applied) and
prints, in Blender world space (nose -Y, up +Z, right +X after Blender's import of a Y-up
file; see export.py), the bounds and centre of:
  - every mesh object (part-per-mesh models: Urchan_Test, Rhino_Test, Dolphin_Test)
  - every vertex group / bone on a skinned mesh (the rigs: which vertices a bone owns)
  - every bone's head and tail in world space
plus the whole-model bounds and the proportions a modeller reads first: length, span, height,
where along the length the widest point is, and the nose/tail ends.

    python3 Tools/Build/vessel_model/part_map.py "Assets/_Models/Vessel Models/SquirrelVessel_CosmicShoresTest1.fbx"

Reader only. The numbers are the targets a new spec's parts must hit for the prefab's
colliders, jets and camera to keep landing on geometry.
"""

import sys

import bpy
from mathutils import Vector


def bounds_of(points):
    if not points:
        return None
    lo = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    hi = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    return lo, hi


def fmt(v):
    return "(%7.3f %7.3f %7.3f)" % tuple(v)


def main(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path, ignore_leaf_bones=False)
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    arms = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    allpts = []
    print("=" * 78)
    print(path)
    print("objects:")
    for o in bpy.data.objects:
        print("  %-36s %-9s parent=%-28s loc=%s rot=%s scale=%s" % (
            o.name, o.type, o.parent.name if o.parent else "-", fmt(o.location),
            "(%.1f %.1f %.1f)" % tuple(__import__("math").degrees(a) for a in o.rotation_euler), fmt(o.scale)))
    print("meshes (world space):")
    for o in meshes:
        pts = [o.matrix_world @ v.co for v in o.data.vertices]
        allpts += pts
        b = bounds_of(pts)
        if not b:
            continue
        lo, hi = b
        print("  %-30s verts %6d  min %s max %s  size %s" % (o.name, len(pts), fmt(lo), fmt(hi), fmt(hi - lo)))
        groups = {}
        for v in o.data.vertices:
            for g in v.groups:
                if g.weight > 0.01:
                    groups.setdefault(o.vertex_groups[g.group].name, []).append(o.matrix_world @ v.co)
        for name, gpts in sorted(groups.items()):
            lo, hi = bounds_of(gpts)
            print("      group %-26s verts %5d  min %s max %s  centre %s" % (name, len(gpts), fmt(lo), fmt(hi), fmt((lo + hi) / 2)))
    for a in arms:
        print("armature %s bones (world head -> tail):" % a.name)
        for b in a.data.bones:
            h = a.matrix_world @ b.head_local
            t = a.matrix_world @ b.tail_local
            print("  %-26s parent=%-22s head %s tail %s len %.3f" % (b.name, b.parent.name if b.parent else "-", fmt(h), fmt(t), (t - h).length))
    b = bounds_of(allpts)
    if b:
        lo, hi = b
        size = hi - lo
        print("whole model: min %s max %s size %s (x span %.3f, y length %.3f, z height %.3f)" % (fmt(lo), fmt(hi), fmt(size), size.x, size.y, size.z))
        # widest station along Y
        ys = sorted(set(round(p.y, 1) for p in allpts))
        best = None
        for y in ys[::max(1, len(ys) // 40)]:
            xs = [p.x for p in allpts if abs(p.y - y) < 0.05 * size.y]
            if xs:
                w = max(xs) - min(xs)
                if best is None or w > best[1]:
                    best = (y, w)
        if best:
            print("widest station: y=%.3f (%.0f%% from -Y end), width %.3f" % (best[0], 100 * (best[0] - lo.y) / max(size.y, 1e-6), best[1]))
        # which end is the nose? the narrower cross-section at the ends
        for label, y in (("-Y end", lo.y + 0.08 * size.y), ("+Y end", hi.y - 0.08 * size.y)):
            xs = [p.x for p in allpts if abs(p.y - y) < 0.05 * size.y]
            zs = [p.z for p in allpts if abs(p.y - y) < 0.05 * size.y]
            if xs:
                print("  cross-section near %s: width %.3f height %.3f" % (label, max(xs) - min(xs), max(zs) - min(zs)))


if __name__ == "__main__":
    for p in sys.argv[1:]:
        main(p)
