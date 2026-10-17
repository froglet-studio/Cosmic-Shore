"""
rhino - the bulldozer: a 16-unit armoured hull that ploughs prisms aside with its bow, swings
an energy sword (a separate prefab) and carries ten jets. Its live prefab still draws the
7-mesh placeholder (`Rhino_Test.fbx`); the RIG it will be swapped onto is this file, whose four
element shapes shipped EMPTY and whose wings sit 1.38x wider than the placeholder's. This spec
rewrites the rig file with a real hull and real morphs so the editor swap (VesselRigSwapper,
Rhino row: fit z -1.5545, re-parent the wing jets to jet.l/.r) has something worth swapping to:

LEGACY BINDINGS, not design: the file path, node / geometry / armature / bone names, slot order,
take names and file frame below are what this vessel's prefab, .meta and driver bind TODAY;
.claude/skills/vessel-model/references/FLEET_CONVENTION.md section 7 names the step that retires
each one. Everything under 'Design intent' and the morph / pose meanings follow the convention.


  file      Assets/_Models/Vessel Models/rhino_shapekey_with_animations.fbx (the rig, same guid)
  armature  rhino    mesh node  fusalage.002    geometry  Circle.004
  bones     the shipped 12, reused: root, fuse, winghold1/2.l/r, wing1.l/r (front wings, the code's
            LeftWing/RightWing), wing2.l/r (back wings, collider hosts), jet.l/r (the wing pods,
            the code's LeftEngine/RightEngine)
  takes     the nine standard poses as rhino|<pose> (RhinoAnimation is code puppetry; unbound)
  slots     0 accent.005 (DOMAIN), 1 BASE.005, 2 windsheild.005, 3 N.005 (ENGINE) - the rig's names
  frame     preset "yup" (roots R(-90) S(100), vertices in metres) as the rig was shipped
  size      Blender frame (nose -Y): x +-8.0, y -8.0 (bow) .. +12.4 (back-wing tips), z -8.9 .. +6.5;
            camera -120: only the biggest shapes read - the bow blade, the two wing pods, the
            broad armoured back, the ten exhausts

Design: a charging rhino. A broad, flat-topped, deep-keeled hull with a plough blade and a
horn at the bow, armour plates stepped down its back, a sunk canopy slit, two massive wing pods
on short struts with ring intakes forward and lime exhausts aft, front wings as thick swept
plates, back wings as taller fins, and eight stern nozzles in two rows.
Morphs: charge = the horn and bow blade grow and the pods flare; mass = the hull deepens and
widens; space = the wings reach outward; time = the hull sharpens and sweeps, the nozzles lengthen.
"""

import math

from _common import flip, retag, box_uv, finish, rot_x, rot_z, translate

NAME = "Rhino"
OUTPUT = "Assets/_Models/Vessel Models/rhino_shapekey_with_animations.fbx"

ARMATURE = "rhino"
MESH_NODE = "fusalage.002"
GEOMETRY = "Circle.004"
SLOTS = ("accent.005", "BASE.005", "windsheild.005", "N.005")
D, H, W, J = 0, 1, 2, 3
SIDES = (("l", 1), ("r", -1))


def build(ctx):
    f, rig, morphs, takes = ctx.forms, ctx.rig, ctx.morphs, ctx.takes
    parts = []

    # ------------------------------------------------------------------ HULL (fuse)
    hull = f.fuselage("fuse_hull", 16.8, nose=(0, -8.0, -1.0), axis=(0, 1, 0), segments=32, stations=[
        (0.00, 0.80, 1.20, 1.40, 2.6, 2.4, 0.00),
        (0.08, 2.00, 2.40, 2.80, 3.0, 2.6, 0.30),
        (0.22, 2.70, 3.20, 3.80, 3.4, 2.8, 0.60),
        (0.42, 2.85, 3.40, 4.20, 3.6, 2.8, 0.70),
        (0.65, 2.80, 3.30, 4.00, 3.6, 2.8, 0.60),
        (0.85, 2.40, 2.70, 3.20, 3.2, 2.6, 0.40),
        (1.00, 1.60, 1.80, 2.20, 2.8, 2.4, 0.20),
    ])
    f.paint(hull, H)
    # canopy slit, sunk, over the forward deck
    f.inset_faces(hull, lambda c, n, i: n.z > 0.6 and -4.6 < c.y < -2.2 and abs(c.x) < 1.0, 0.12, depth=0.18, slot=W)
    # three armour steps down the back, domain-edged
    for (y0, y1) in ((-1.5, 1.0), (1.6, 4.2), (4.8, 7.0)):
        f.inset_faces(hull, lambda c, n, i, y0=y0, y1=y1: i == H and n.z > 0.55 and y0 < c.y < y1 and abs(c.x) < 2.3, 0.14, depth=0.16, slot=D)
    # flank domain bands
    f.inset_faces(hull, lambda c, n, i: i == H and abs(n.x) > 0.7 and -3.0 < c.y < 5.0 and -1.2 < c.z < 1.6, 0.12, depth=0.10, slot=D)
    finish(f, hull, bevel_width=0.06)
    # the plough blade: a broad angled plate across the bow
    blade = f.plate("fuse_blade", f.rounded_rect(7.2, 5.2, r=0.9), thickness=0.6, normal=(0, -0.85, 0.53), origin=(0, -8.3, -1.4), right=(1, 0, 0))
    f.paint(blade, D)
    f.paint(blade, H, lambda c, n, i: n.y > 0.3)
    f.inset_faces(blade, lambda c, n, i: n.y < -0.5, 0.25, depth=0.12)
    finish(f, blade, bevel_width=0.08)
    # the horn
    horn = f.cone("fuse_horn", 0.55, 3.6, axis=(0, -0.7, 0.72), origin=(0, -6.6, 1.6), segments=12)
    f.paint(horn, D)
    # eight stern nozzles in two rows
    nozzles = []
    for k, (nx, nz) in enumerate(((-1.8, 0.9), (-0.6, 1.1), (0.6, 1.1), (1.8, 0.9), (-1.8, -1.3), (-0.6, -1.6), (0.6, -1.6), (1.8, -1.3))):
        nz_ = f.lathe("nozzle_%d" % k, [(0.0, 0.0), (0.45, 0.0), (0.55, 0.15), (0.55, 1.0), (0.62, 1.1), (0.62, 1.5), (0.52, 1.65), (0.0, 1.65)],
                      segments=14, axis=(0, 1, 0), origin=(nx, 7.9, nz))
        f.paint(nz_, H)
        f.inset_faces(nz_, lambda c, n, i: n.y > 0.8, 0.08, depth=0.3, slot=J)
        f.sharpen_by_angle(nz_, 35)
        nozzles.append(nz_)
    fuse = f.join([hull, blade, horn] + nozzles, "fuse", SLOTS)
    retag(f, fuse)
    parts.append(fuse)

    # ------------------------------------------------------------------ WINGS, PODS, STRUTS
    for tag, sx in SIDES:
        # front wing: a thick swept plate from the flank out to the pod
        w1 = f.wing("wing1." + tag, root=(sx * 2.6, -4.5, 0.1), span=3.3, chord_root=7.6, chord_tip=4.6,
                    sweep=1.0, dihedral_deg=4.0, twist_deg=0.0, thickness=0.18, sections=5, segments=14, side=sx, taper_power=1.0, tip_round=False)
        f.paint(w1, H)
        f.paint(w1, D, lambda c, n, i: n.y < -0.5 or n.z > 0.8 and False)
        f.inset_faces(w1, lambda c, n, i: i == H and n.z > 0.7, 0.18, depth=0.1, slot=D)
        finish(f, w1, bevel_width=0.05)
        # back wing: a taller fin behind the pod
        w2 = f.wing("wing2." + tag, root=(sx * 5.0, 1.6, 0.8), span=2.6, chord_root=5.2, chord_tip=2.0,
                    sweep=4.2, dihedral_deg=42.0, twist_deg=0.0, thickness=0.14, sections=5, segments=14, side=sx, taper_power=1.0, tip_round=False)
        f.paint(w2, H)
        f.paint(w2, D, lambda c, n, i: n.y < -0.5)
        finish(f, w2, bevel_width=0.05)
        # the wing pod: a big drum with a ring intake forward and a lime exhaust aft
        pod = f.lathe("jet." + tag, [(0.0, 0.0), (1.5, 0.0), (1.85, 0.3), (1.95, 0.8), (1.95, 2.6), (2.1, 2.8), (2.1, 3.3), (1.95, 3.5), (1.95, 5.0), (1.75, 5.5), (0.0, 5.5)],
                      segments=24, axis=(0, 1, 0), origin=(sx * 5.37, -0.4, 0.46))
        f.paint(pod, H)
        f.paint(pod, D, lambda c, n, i: abs(n.y) < 0.5 and 2.3 < c.y < 2.9)
        f.inset_faces(pod, lambda c, n, i: n.y < -0.8, 0.25, depth=0.5, slot=J)   # the intake ring (lit)
        f.inset_faces(pod, lambda c, n, i: n.y > 0.8, 0.25, depth=0.7, slot=J)    # the exhaust
        finish(f, pod, bevel_width=0.06)
        # struts from the hull to the pod (two)
        st = []
        for yy in (0.6, 3.2):
            s_ = f.tube_along("jetstrut%d.%s" % (int(yy * 10), tag), [(sx * 2.4, yy, 0.6), (sx * 4.2, yy, 0.55), (sx * 5.37, yy, 0.46)], [0.5, 0.42, 0.35], segments=10)
            f.paint(s_, H)
            st.append(s_)
        parts += [w1, w2, pod] + st

    # ------------------------------------------------------------------ JOIN
    mesh = f.join(parts, MESH_NODE, SLOTS)
    mesh.data.name = GEOMETRY
    f.set_smooth(mesh, True)
    f.sharpen_by_angle(mesh, 44)
    box_uv(mesh)
    ctx.log("rhino joined: %d verts, %d tris" % (len(mesh.data.vertices), f.tri_count(mesh)))

    # ------------------------------------------------------------------ RIG: the shipped armature
    arm = rig.import_shipped_armature(OUTPUT)
    assert arm.name == ARMATURE, arm.name
    bone_names = [b.name for b in arm.data.bones]
    g2b = {}
    for tag, _sx in SIDES:
        for yy in (6, 32):
            g2b["jetstrut%d.%s" % (yy, tag)] = "wing1." + tag
    rig.skin_rigid(mesh, arm, group_to_bone=g2b, default_bone="fuse")
    ctx.log("rig: %d bones reused" % len(bone_names))

    # ------------------------------------------------------------------ MORPHS
    msk = morphs.MaskSet(mesh)
    fuse_m = msk.mask("fuse")
    wings = msk.mask("wing1.l", "wing1.r", "wing2.l", "wing2.r")
    pods = msk.mask("jet.l", "jet.r")
    bow = morphs.falloff_mask(mesh, fuse_m, (0, 0, 0), (0, -1, 0), 4.0, 7.5)
    stern = morphs.falloff_mask(mesh, fuse_m, (0, 0, 0), (0, 1, 0), 6.0, 7.9)
    shapes = {
        "charge": [  # the weapon: the horn and blade grow forward, the pods flare
            morphs.stretch_along((0, -1, 0), (0, -5.0, 0), 1.45, bow, ramp=(0.0, 1.0)),
            morphs.scale_about(1.18, (0, 0, 0.46), pods, axes=(1, 0, 1)),
        ],
        "mass": [  # the hull deepens and widens
            morphs.scale_about(1.25, (0, 0, -0.5), fuse_m, axes=(1, 0, 1)),
            morphs.inflate(0.25, fuse_m),
        ],
        "space": [  # reach: wings and pods move outward
            morphs.translate((1.6, 0, 0), msk.mask("wing1.l", "wing2.l", "jet.l")),
            morphs.translate((-1.6, 0, 0), msk.mask("wing1.r", "wing2.r", "jet.r")),
            morphs.scale_about(1.25, (0, 0, 0), morphs.falloff_mask(mesh, wings, (0, 0, 0), (0, 1, 0), 2.0, 8.0), axes=(0, 1, 1)),
        ],
        "time": [  # speed: the hull sharpens and sweeps, the nozzles lengthen
            morphs.stretch_along((0, -1, 0), (0, -2.0, 0), 1.25, bow, ramp=(0.0, 2.0)),
            morphs.stretch_along((0, 1, 0), (0, 7.9, 0), 1.9, stern, ramp=(0.0, 0.1)),
            morphs.sweep((1, 0, 0), (0, 0, 0), (0, 0.35, 0), morphs.falloff_mask(mesh, wings, (0, 0, 0), (1, 0, 0), 0.0, 0.5)),
            morphs.sweep((-1, 0, 0), (0, 0, 0), (0, 0.35, 0), morphs.falloff_mask(mesh, wings, (0, 0, 0), (-1, 0, 0), 0.0, 0.5)),
        ],
    }
    for name, defs in shapes.items():
        kb, mx, frac, moved = morphs.add_shape(mesh, name, defs)
        ctx.log("shape %s: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))

    # ------------------------------------------------------------------ POSES (world axes; nose -Y)
    def pose(p):
        ws = ["wing1.l", "wing1.r"]
        js = ["jet.l", "jet.r"]
        if p == "pitchup":
            return {**{n: (-18, 0, 0) for n in ws + js}, "fuse": (-6, 0, 0)}
        if p == "pitchdown":
            return {**{n: (18, 0, 0) for n in ws + js}, "fuse": (6, 0, 0)}
        if p in ("yawleft", "yawright"):
            s = 1 if p == "yawright" else -1
            return {**{n: (0, 0, s * 18) for n in ws + js}, "fuse": (0, 0, s * 6)}
        if p in ("rollleft", "rollright"):
            s = 1 if p == "rollright" else -1
            return {**{n: (0, s * 22, 0) for n in ws + js}, "fuse": (0, s * 8, 0)}
        if p == "speedup":
            return {"wing1.l": (0, 0, 20), "wing1.r": (0, 0, -20), **{n: {"loc": (0, 1.0, 0)} for n in js}}
        if p == "speeddown":
            return {"wing1.l": (0, 0, -14), "wing1.r": (0, 0, 14), **{n: {"loc": (0, -0.8, 0)} for n in js}}
        return {}

    acts = takes.standard_pose_set(arm, pose)

    lo, hi = f.bounds(mesh)
    ctx.log("bounds (blender) %s .. %s" % (tuple(round(v, 3) for v in lo), tuple(round(v, 3) for v in hi)))
    return {
        "armature": arm, "mesh": mesh,
        "spec": {
            "preset": "yup", "armature": ARMATURE, "mesh_node": MESH_NODE,
            "bones": bone_names,
            "takes": [ARMATURE + "|" + p for p in takes.STANDARD_POSES],
            "pose_takes": [ARMATURE + "|" + p for p in takes.STANDARD_POSES],
            "shapes": ["charge", "mass", "space", "time"],
            "slots": list(SLOTS),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.04,
        },
    }
