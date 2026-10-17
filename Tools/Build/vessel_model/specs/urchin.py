"""
urchin - the fleet's smallest hull: a spined ball that rides prismscapes, fires chain spikes
from two side guns, and flies backwards as easily as forwards. Its live prefab still draws the
14-mesh placeholder (`Urchan_Test.fbx`); the RIG it will be swapped onto is this file, whose
four element shapes shipped EMPTY. This spec rewrites the rig file with a real hull and real
morphs so the swap (editor: `FrogletTools > Vessels > Wiring > Swap Vessel Rig`, extended from
the Dolphin's table) is worth doing:

LEGACY BINDINGS, not design: the file path, node / geometry / armature / bone names, slot order,
take names and file frame below are what this vessel's prefab, .meta and driver bind TODAY;
.claude/skills/vessel-model/references/FLEET_CONVENTION.md section 7 names the step that retires
each one. Everything under 'Design intent' and the morph / pose meanings follow the convention.


  file      Assets/_Models/Vessel Models/urchan_shapekey_with_animations.fbx (the rig, same guid)
  armature  Armature    mesh node  Body.001    geometry  Sphere.042
  bones     the shipped 38, reused: `fuse` root, and the code's parts `gunM.l/r`, `jetT.l/r`,
            `jetB.l/r`, `wingconrotT/B.l/r`, `sheildconrot.l/r` under their hold/rot chains
            (UrchinAnimation resolves exactly these names)
  takes     eleven documentary poses with the rig's names are not bound; the nine standard ones
            are written as Armature|<pose>
  slots     0 accent (DOMAIN), 1 BASE, 2 windsheild, 3 N (ENGINE) - the fleet order
  frame     preset "yup_baked" (mesh node identity, data in cm, armature R(-90) S(100)) as shipped
  size      Blender frame (nose -Y): a 0.91-unit ball, x +-0.455, y -0.435 .. +0.438, z +-0.427.
            The swap tool places this rig at localScale 0.4749 over the 0.43-unit placeholder.
            Camera -6.67: at that range the ball IS the read - a spined sphere with a bubble
            canopy forward, a ringed exhaust aft, two guns, two shields, four jets.

Morphs: charge = the guns lengthen and the spines rise; mass = the ball swells; space = the
shield pads and shrouds spread outward; time = the ball lengthens into a bullet and the jets
stretch aft.
"""

import math

from _common import flip, retag, box_uv, finish, rot_x, rot_z, translate

NAME = "Urchin"
OUTPUT = "Assets/_Models/Vessel Models/urchan_shapekey_with_animations.fbx"

ARMATURE = "Armature"
MESH_NODE = "Body.001"
GEOMETRY = "Sphere.042"
SLOTS = ("accent", "BASE", "windsheild", "N")
D, H, W, J = 0, 1, 2, 3
SIDES = (("l", 1), ("r", -1))

R = 0.425


def build(ctx):
    f, rig, morphs, takes = ctx.forms, ctx.rig, ctx.morphs, ctx.takes
    parts = []

    # ------------------------------------------------------------------ the BALL (fuse)
    ball = f.sphere("fuse_ball", R, center=(0, 0, 0), segments=40, rings=24)
    f.paint(ball, H)
    # bubble canopy: the forward cap, sunk and glassed, with a frame
    f.inset_faces(ball, lambda c, n, i: n.y < -0.72 and abs(c.x) < 0.263 and abs(c.z) < 0.263, 0.012, depth=0.02, slot=W)
    # two meridian domain bands (the spine lines) and an equatorial band
    f.paint(ball, D, lambda c, n, i: i == H and abs(c.x) < 0.035 and abs(n.y) < 0.75)
    f.paint(ball, D, lambda c, n, i: i == H and abs(c.z) < 0.03 and abs(n.y) < 0.75 and abs(c.x) > 0.12)
    # two sunk latitude bands of plating either side of the equator
    for (lo_, hi_) in ((0.1003, 0.19), (0.2585, 0.34)):  # thresholds sit between face-centre latitudes (forms.UnstableSelection)
        f.inset_faces(ball, lambda c, n, i, lo_=lo_, hi_=hi_: i == H and lo_ < abs(c.y) < hi_ and abs(c.x) > 0.06, 0.006, depth=0.006)
    finish(f, ball, bevel_width=0.0, sharp_deg=60)
    # exhaust: a ringed cone at the stern (+Y), lime inside
    exhaust = f.lathe("fuse_exhaust", [(0.0, 0.0), (0.16, 0.0), (0.20, 0.03), (0.20, 0.08), (0.17, 0.10), (0.14, 0.10), (0.12, 0.04), (0.0, 0.04)],
                      segments=24, axis=(0, 1, 0), origin=(0, R - 0.03, 0.0), cap=False)
    f.paint(exhaust, H)
    f.paint(exhaust, J, lambda c, n, i: n.y < -0.3 or (abs(n.y) < 0.6 and ((c.x ** 2 + c.z ** 2) ** 0.5) < 0.15))
    f.sharpen_by_angle(exhaust, 35)
    # eight spines around the equator-ish, the urchin's name
    spines = []
    for k in range(8):
        a = math.radians(22.5 + 45 * k)
        d = (math.cos(a) * 0.72, 0.2, math.sin(a) * 0.72)
        sp = f.cone("spine_%d" % k, 0.028, 0.16, axis=d, origin=(d[0] * R * 0.95, d[1] * R * 0.95, d[2] * R * 0.95), segments=8)
        f.paint(sp, D)
        spines.append(sp)
    fuse = f.join([ball, exhaust] + spines, "fuse", SLOTS)
    retag(f, fuse)
    parts.append(fuse)

    # ------------------------------------------------------------------ GUNS, SHIELDS, JETS, SHROUDS
    for tag, sx in SIDES:
        # the side gun: a barrel along -Y on a collar at the ball's equator
        gun = f.lathe("gunM." + tag, [(0.0, 0.0), (0.045, 0.0), (0.055, 0.02), (0.055, 0.17), (0.07, 0.19), (0.07, 0.24), (0.05, 0.27), (0.0, 0.27)],
                      segments=14, axis=(0, -1, 0), origin=(sx * 0.35, 0.05, 0.0))
        f.paint(gun, H)
        f.paint(gun, D, lambda c, n, i: abs(n.y) < 0.5 and -0.16 < c.y < -0.12)
        f.inset_faces(gun, lambda c, n, i: n.y < -0.8, 0.012, depth=0.03, slot=J)
        finish(f, gun, bevel_width=0.004)
        parts.append(gun)
        # the shield pad: a curved plate standing off the flank
        pad = f.plate("sheildconrot." + tag, f.rounded_rect(0.30, 0.30, r=0.08), thickness=0.035, normal=(sx, 0, 0), origin=(sx * 0.44, 0.0, 0.0), right=(0, 1, 0))
        f.paint(pad, D)
        f.paint(pad, H, lambda c, n, i, sx=sx: n.x * sx < 0.5)
        finish(f, pad, bevel_width=0.006)
        parts.append(pad)
        # jets: top and bottom pods tilted outward, lime mouths aft
        for name, zc, zs in (("jetT." + tag, 0.275, 1), ("jetB." + tag, -0.273, -1)):
            axis = (sx * 0.25, 1.0, zs * 0.18)
            pod = f.lathe(name, [(0.0, 0.0), (0.05, 0.0), (0.065, 0.02), (0.065, 0.14), (0.075, 0.16), (0.075, 0.20), (0.06, 0.22), (0.0, 0.22)],
                          segments=14, axis=axis, origin=(sx * 0.22, -0.16, zc))
            f.paint(pod, H)
            f.paint(pod, D, lambda c, n, i: abs(n.y) < 0.4 and -0.05 < c.y < 0.0)
            f.inset_faces(pod, lambda c, n, i: n.y > 0.75, 0.01, depth=0.03, slot=J)
            finish(f, pod, bevel_width=0.004)
            parts.append(pod)
            # the shroud over each jet: a curved fin plate
            sh = f.plate("wingconrot%s.%s" % ("T" if zs > 0 else "B", tag), f.feather(0.34, 0.22, tip_round=0.6, root_width=0.12), thickness=0.03,
                         normal=(sx * 0.3, 0, zs * 0.95), origin=(sx * 0.26, 0.03, zc + zs * 0.08), right=(0, 1, 0))
            f.paint(sh, D)
            f.paint(sh, H, lambda c, n, i, zs=zs: n.z * zs < 0.2)
            finish(f, sh, bevel_width=0.005)
            parts.append(sh)

    # ------------------------------------------------------------------ JOIN
    mesh = f.join(parts, MESH_NODE, SLOTS)
    mesh.data.name = GEOMETRY
    f.set_smooth(mesh, True)
    f.sharpen_by_angle(mesh, 48)
    box_uv(mesh)
    ctx.log("urchin joined: %d verts, %d tris" % (len(mesh.data.vertices), f.tri_count(mesh)))

    # ------------------------------------------------------------------ RIG: the shipped armature
    arm = rig.import_shipped_armature(ctx.artist(OUTPUT))
    assert arm.name == ARMATURE, arm.name
    bone_names = [b.name for b in arm.data.bones]
    rig.skin_rigid(mesh, arm, default_bone="fuse")
    ctx.log("rig: %d bones reused" % len(bone_names))

    # ------------------------------------------------------------------ MORPHS
    msk = morphs.MaskSet(mesh)
    fuse_m = msk.mask("fuse")
    guns = msk.mask("gunM.l", "gunM.r")
    pads = msk.mask("sheildconrot.l", "sheildconrot.r")
    jets = msk.mask("jetT.l", "jetT.r", "jetB.l", "jetB.r")
    shrouds = msk.mask("wingconrotT.l", "wingconrotT.r", "wingconrotB.l", "wingconrotB.r")
    # the spines are the only fuse vertices OUTSIDE the shell radius
    spine_m = [w if (mesh.data.vertices[i].co.length > R * 1.03 and mesh.data.vertices[i].co.y < R * 0.9) else 0.0 for i, w in enumerate(fuse_m)]
    shapes = {
        "charge": [  # the chain spikes: the guns lengthen and thicken, the spines rise
            morphs.stretch_along((0, -1, 0), (0, 0.05, 0), 1.6, guns, ramp=(0.0, 0.1)),
            morphs.scale_about(1.3, (0, 0, 0), guns, axes=(1, 0, 1)),
            morphs.scale_about(1.6, (0, 0, 0), spine_m, axes=(1, 1, 1)),
        ],
        "mass": [  # the ball swells
            morphs.inflate(0.07, fuse_m), morphs.inflate(0.02, jets),
        ],
        "space": [  # reach: the shield pads and shrouds spread outward, the jets move out
            morphs.translate((0.09, 0, 0), msk.mask("sheildconrot.l", "jetT.l", "jetB.l", "wingconrotT.l", "wingconrotB.l")),
            morphs.translate((-0.09, 0, 0), msk.mask("sheildconrot.r", "jetT.r", "jetB.r", "wingconrotT.r", "wingconrotB.r")),
            morphs.scale_about(1.3, (0, 0, 0), pads, axes=(0, 1, 1)),
        ],
        "time": [  # speed: the ball stretches into a bullet, the jets reach aft
            morphs.stretch_along((0, 1, 0), (0, -0.1, 0), 1.22, fuse_m),
            morphs.stretch_along((0, 1, 0), (0, -0.16, 0), 1.45, jets, ramp=(0.0, 0.05)),
            morphs.sweep((0, 0, 1), (0, 0, 0), (0, 0.12, 0), shrouds),
        ],
    }
    for name, defs in shapes.items():
        kb, mx, frac, moved = morphs.add_shape(mesh, name, defs)
        ctx.log("shape %s: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))

    # ------------------------------------------------------------------ POSES (world axes; nose -Y)
    def pose(p):
        jt = ["jetT.l", "jetT.r", "jetB.l", "jetB.r"]
        sh = ["wingconrotT.l", "wingconrotT.r", "wingconrotB.l", "wingconrotB.r"]
        if p == "pitchup":
            return {**{n: (-25, 0, 0) for n in jt + sh}, "gunM.l": (-10, 0, 0), "gunM.r": (-10, 0, 0)}
        if p == "pitchdown":
            return {**{n: (25, 0, 0) for n in jt + sh}, "gunM.l": (10, 0, 0), "gunM.r": (10, 0, 0)}
        if p in ("yawleft", "yawright"):
            s = 1 if p == "yawright" else -1
            return {**{n: (0, 0, s * 25) for n in jt + sh}, "sheildconrot.l": (0, 0, s * 15), "sheildconrot.r": (0, 0, s * 15)}
        if p in ("rollleft", "rollright"):
            s = 1 if p == "rollright" else -1
            return {**{n: (0, s * 25, 0) for n in jt + sh}}
        if p == "speedup":
            return {**{n: {"loc": (0, 0.06, 0)} for n in jt}, "wingconrotT.l": (0, 0, 20), "wingconrotT.r": (0, 0, -20), "wingconrotB.l": (0, 0, 20), "wingconrotB.r": (0, 0, -20)}
        if p == "speeddown":
            return {**{n: {"loc": (0, -0.05, 0)} for n in jt}, "sheildconrot.l": (0, 0, -20), "sheildconrot.r": (0, 0, 20)}
        return {}

    acts = takes.standard_pose_set(arm, pose)

    lo, hi = f.bounds(mesh)
    ctx.log("bounds (blender) %s .. %s" % (tuple(round(v, 3) for v in lo), tuple(round(v, 3) for v in hi)))
    return {
        "armature": arm, "mesh": mesh,
        "spec": {
            "preset": "yup_baked", "armature": ARMATURE, "mesh_node": MESH_NODE,
            "slot_reorder_from": ("BASE", "accent", "windsheild", "N"),  # deliberate migration: the artist rig had BASE at slot 0; the convention paints slot 0 as the domain band (accent)
            "bones": bone_names,
            "takes": [ARMATURE + "|" + p for p in takes.STANDARD_POSES],
            "pose_takes": [ARMATURE + "|" + p for p in takes.STANDARD_POSES],
            "shapes": ["charge", "mass", "space", "time"],
            "slots": list(SLOTS),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.04,
        },
    }
