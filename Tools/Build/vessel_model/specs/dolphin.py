"""
dolphin - "DolphinDarts: charge and release". A slim dart with two long clamp jaws ahead of
it (the blast emitter - the hull IS the gauge: the jaws gape with banked Energy), two long
thin swept wings, and six engine cases fanned round the stern with open exhaust bells.
Written IN PLACE over the live rig so the prefab (20 bound sub-objects, jets on the bell
bones, colliders on the case bones) needs no edit:

  file      Assets/_Models/Vessel Models/dolphin_shapekey_with_animations.fbx (same guid)
  armature  dolphin        mesh node  dolohin (sic - the prefab binds this misspelling)
  geometry  Chassis
  bones     the shipped 28 (root, fuse, jaw.u/.b, root.001/.002, winghold.l/.r, wing.l/.r,
            jethold{T,m,B}.{l,r}, jet{T,m,B}.{l,r}, jetin{t,m,b}.{l,r}) - reused as-is, so
            every bone head rests within 0.0 of the shipped one (rest_compare.py)
  takes     none are bound (code puppetry: RiptideAnimation); the ten shipped MOTION names are
            kept for documentation: jawcharge/startcharge + the eight stick poses, '_dolphin'
  slots     0 accent.001 (DOMAIN), 1 BASE.001 (BODY), 2 windsheild.001 (WINDOW), 3 N.001 (ENGINE)
  frame     preset "yup": Blender's default export (Y-up file, UnitScaleFactor 1, root nodes
            R(-90,0,0) S(100)), which is how this file was shipped
  size      Blender frame (nose -Y): x +-2.644, y -2.827 .. +2.471, z -0.567 .. +0.642;
            camera -20, the fleet's FX reference hull

Design intent (what reads at 20 u from the stern): the six-bell fan, the long open jaws and
the thin swept wings are the whole silhouette; spend the triangles there. Beat the shipped
file on: welded manifold parts (no 72-gon star cut-outs, no loose shells), a real cockpit
(the shipped Window slot is 10 polygons), crisp panel insets instead of holes, bells that
read as six separate mouths, a domain layout that lives on the jaws and wing leading edges.
Element morphs (dossier 4.1): charge = the jaws and throat thicken; mass = the fuselage belly
and the cases fatten; space = the jaws reach forward and the wing tips extend; time = the
bells lengthen aft, the wings sweep back and thin, the fuselage sharpens.
"""

import math

from _common import rot_x, rot_z, translate, aim, flip, retag, box_uv, finish, scale_axis

NAME = "Dolphin"
OUTPUT = "Assets/_Models/Vessel Models/dolphin_shapekey_with_animations.fbx"

ARMATURE = "dolphin"
MESH_NODE = "dolohin"
GEOMETRY = "Chassis"
SLOTS = ("accent.001", "BASE.001", "windsheild.001", "N.001")
D, H, W, J = 0, 1, 2, 3
TAKES = ("jawcharge", "pitchdown", "pitchup", "rollleft", "rollright", "speeddown", "speedup", "startcharge", "yawleft", "yawright")

SIDES = (("l", 1), ("r", -1))  # .l is +X in the Blender frame (pilot's left)

# bone heads (Blender frame, from part_map on the shipped rig)
CASES = {  # name -> (head, axis) ; bells are their children, 0.23 further along the axis
    "jetT": ((0.269, 1.901, 0.396), (0.099, 0.217, 0.098)),
    "jetm": ((0.373, 1.896, 0.201), (0.136, 0.218, 0.027)),
    "jetB": ((0.350, 1.892, -0.019), (0.129, 0.217, -0.053)),
}
BELLS = {"jetT": "jetint", "jetm": "jetinm", "jetB": "jetinb"}
BELL_HEADS = {"jetint": (0.359, 2.126, 0.485), "jetinm": (0.497, 2.122, 0.225), "jetinb": (0.467, 2.117, -0.067)}


def build(ctx):
    f, rig, morphs, takes = ctx.forms, ctx.rig, ctx.morphs, ctx.takes
    parts = []

    # ------------------------------------------------------------------ FUSELAGE (fuse)
    # a vertical oval dart from the throat (y -1.0) to the stern (y 2.47); fattest under the
    # wing root, with a dorsal spine and a flat-ish belly; the stern narrows to the hold
    hull = f.fuselage("fuse_hull", 3.47, nose=(0, -1.00, 0.02), axis=(0, 1, 0), segments=28, stations=[
        (0.00, 0.17, 0.26, 0.26, 2.4, 2.4, 0.00),
        (0.12, 0.27, 0.46, 0.40, 2.8, 2.6, 0.03),
        (0.32, 0.32, 0.58, 0.52, 3.0, 2.8, 0.04),
        (0.55, 0.31, 0.55, 0.50, 3.0, 2.8, 0.03),
        (0.78, 0.26, 0.44, 0.40, 2.8, 2.6, 0.05),
        (0.92, 0.19, 0.30, 0.26, 2.6, 2.4, 0.10),
        (1.00, 0.10, 0.14, 0.12, 2.4, 2.2, 0.14),
    ])
    f.paint(hull, H)
    # cockpit: a long sunken glass strip on the spine over the wing root
    f.inset_faces(hull, lambda c, n, i: n.z > 0.6 and -0.55 < c.y < 0.35 and abs(c.x) < 0.14, 0.02, depth=0.03, slot=W)
    # domain flank panels, two a side, sunk in
    for (y0, y1) in ((-0.80, -0.10), (0.45, 1.35)):
        f.inset_faces(hull, lambda c, n, i, y0=y0, y1=y1: i == H and abs(n.x) > 0.6 and y0 < c.y < y1 and -0.25 < c.z < 0.35, 0.02, depth=0.015, slot=D)
    # a dorsal domain stripe behind the cockpit
    f.paint(hull, D, lambda c, n, i: i == H and n.z > 0.85 and 0.45 < c.y < 1.70 and abs(c.x) < 0.07)
    finish(f, hull, bevel_width=0.012)
    # throat: the ribbed coil between the jaws
    coil = f.lathe("fuse_coil", [(0.0, 0.0)] + [(0.17 + 0.04 * (i % 2), 0.05 + 0.055 * i) for i in range(7)] + [(0.0, 0.42)],
                   segments=20, axis=(0, -1, 0), origin=(0, -0.85, 0.0))
    f.paint(coil, H)
    f.paint(coil, D, lambda c, n, i: abs(n.y) < 0.4 and (int((c.y + 0.85) / -0.055) % 2 == 0))
    f.sharpen_by_angle(coil, 30)
    # dorsal fin on the stern
    fin = f.plate("fuse_fin", f.blade(0.55, 0.30, tip=0.2), thickness=0.05, normal=(1, 0, 0),
                  origin=(0, 1.55, 0.45), right=(0, 0.40, 0.92))
    f.paint(fin, D)
    finish(f, fin, bevel_width=0.008)
    fuse = f.join([hull, coil, fin], "fuse", SLOTS)
    retag(f, fuse)
    parts.append(fuse)

    # ------------------------------------------------------------------ JAWS (jaw.u / jaw.b)
    # two clamp halves hinged at (0, -0.93, +-0.355), 2 units long, tapering to a point; the
    # inner faces carry the domain colour so the gape reads as a coloured bar
    for name, sz in (("jaw.u", 1), ("jaw.b", -1)):
        jaw = f.fuselage(name, 2.00, nose=(0, -0.83, sz * 0.235), axis=(0, -1, 0), segments=16, stations=[
            (0.00, 0.11, 0.185, 0.185, 3.5, 3.5, 0.00),
            (0.25, 0.115, 0.19, 0.19, 3.8, 3.8, sz * 0.01),
            (0.55, 0.105, 0.17, 0.17, 3.8, 3.8, sz * 0.02),
            (0.82, 0.08, 0.12, 0.12, 3.2, 3.2, sz * 0.03),
            (1.00, 0.02, 0.03, 0.03, 2.4, 2.4, sz * 0.03),
        ])
        f.paint(jaw, H)
        # inner (bite) face: domain
        f.paint(jaw, D, lambda c, n, i, sz=sz: n.z * sz < -0.6 and c.y < -1.0)
        # three sunken outer panels
        for (y0, y1) in ((-2.55, -2.15), (-2.05, -1.60), (-1.50, -1.05)):
            f.inset_faces(jaw, lambda c, n, i, y0=y0, y1=y1, sz=sz: i == H and n.z * sz > 0.6 and y0 < c.y < y1, 0.015, depth=0.012, slot=D)
        finish(f, jaw, bevel_width=0.008)
        parts.append(jaw)

    # ------------------------------------------------------------------ WINGS (wing.l / wing.r)
    for tag, sx in SIDES:
        w = f.wing("wing." + tag, root=(sx * 0.30, -0.22, 0.0), span=2.34, chord_root=0.80, chord_tip=0.30,
                   sweep=0.55, dihedral_deg=0.0, twist_deg=-2.0, thickness=0.16, camber=0.0, sections=8,
                   segments=18, side=sx, taper_power=0.85)
        f.paint(w, H)
        # the leading edge and the tip wear the domain
        f.paint(w, D, lambda c, n, i, sx=sx: n.y < -0.45 or abs(c.x) > 2.15)
        # a sunken top panel
        f.inset_faces(w, lambda c, n, i, sx=sx: i == H and n.z > 0.7 and 0.95 < abs(c.x) < 1.95, 0.02, depth=0.012, slot=D)
        finish(f, w, bevel_width=0.008, sharp_deg=45)
        parts.append(w)

    # ------------------------------------------------------------------ CASES + BELLS
    for tag, sx in SIDES:
        for cname, (head, axis) in CASES.items():
            hx, hy, hz = sx * head[0], head[1], head[2]
            ax = (sx * axis[0], axis[1], axis[2])
            # the case: a pod along its bone axis, 0.36 long, 0.13 radius, with a collar
            case = f.lathe(cname + "." + tag,
                           [(0.0, -0.02), (0.09, 0.0), (0.125, 0.06), (0.13, 0.22), (0.145, 0.25), (0.145, 0.30), (0.12, 0.34), (0.0, 0.36)],
                           segments=16, axis=ax, origin=(hx, hy, hz))
            f.paint(case, H)
            f.paint(case, D, lambda c, n, i: abs(n.y) < 0.6 and False)
            finish(f, case, bevel_width=0.006)
            # a short strut from the hold to the case so the fan is one structure
            strut = f.tube_along(cname + "_strut." + tag, [(0, 1.90, 0.18), (hx * 0.6, hy, hz * 0.6 + 0.07), (hx, hy + 0.05, hz)],
                                 [0.06, 0.05, 0.045], segments=8)
            f.paint(strut, H)
            # the bell: an open flaring mouth behind the case, lime inside
            bname = BELLS[cname]
            bh = BELL_HEADS[bname]
            bhx, bhy, bhz = sx * bh[0], bh[1], bh[2]
            bell = f.lathe(bname + "." + tag,
                           [(0.09, 0.0), (0.12, 0.03), (0.13, 0.10), (0.16, 0.17), (0.175, 0.19), (0.13, 0.19), (0.11, 0.12), (0.095, 0.04), (0.08, 0.0)],
                           segments=16, axis=ax, origin=(bhx, bhy, bhz), cap=False)
            f.paint(bell, H)
            # the inner throat glows: faces whose normal points back toward the bell's axis
            f.paint(bell, J, lambda c, n, i, ax=ax, o=(bhx, bhy, bhz): inward(c, n, o, ax))
            f.sharpen_by_angle(bell, 35)
            parts += [case, strut, bell]

    # ------------------------------------------------------------------ JOIN
    mesh = f.join(parts, MESH_NODE, SLOTS)
    mesh.data.name = GEOMETRY
    f.set_smooth(mesh, True)
    f.sharpen_by_angle(mesh, 42)
    box_uv(mesh)
    ctx.log("dolphin joined: %d verts, %d tris" % (len(mesh.data.vertices), f.tri_count(mesh)))

    # ------------------------------------------------------------------ RIG: the shipped armature
    arm = rig.import_shipped_armature(OUTPUT)
    assert arm.name == ARMATURE, arm.name
    bone_names = [b.name for b in arm.data.bones]
    g2b = {}
    for tag, _sx in SIDES:
        for cname in CASES:
            g2b[cname + "_strut." + tag] = cname + "." + tag
    rig.skin_rigid(mesh, arm, group_to_bone=g2b, default_bone="fuse")
    ctx.log("rig: %d bones reused" % len(bone_names))

    # ------------------------------------------------------------------ MORPHS (dossier 4.1)
    ms = morphs.MaskSet(mesh)
    jaws = ms.mask("jaw.u", "jaw.b")
    fuse_m = ms.mask("fuse")
    wings = ms.mask("wing.l", "wing.r")
    cases = ms.mask(*[c + "." + t for c in CASES for t, _ in SIDES])
    bells = ms.mask(*[BELLS[c] + "." + t for c in CASES for t, _ in SIDES])
    throat = morphs.falloff_mask(mesh, fuse_m, (0, -0.4, 0), (0, -1, 0), 0.0, 0.3)
    shapes = {
        "charge": [  # the weapon's girth: both jaw halves and the throat thicken
            morphs.scale_about(1.6, (0, 0, 0), jaws, axes=(1, 0, 1)),
            morphs.inflate(0.09, jaws),
            morphs.inflate(0.14, throat),
        ],
        "mass": [  # ammunition carried: the fuselage belly and the case bodies fatten
            morphs.scale_about(1.28, (0, 0, -0.05), morphs.falloff_mask(mesh, fuse_m, (0, 0, 0), (0, 1, 0), -0.7, -0.2), axes=(1, 0, 1)),
            morphs.inflate(0.05, fuse_m),
            morphs.inflate(0.06, cases),
        ],
        "space": [  # reach: the jaws lengthen forward, the wing tips extend
            morphs.stretch_along((0, -1, 0), (0, -0.93, 0), 1.35, jaws, ramp=(0.0, 0.4)),
            morphs.scale_about(1.13, (0, 0, 0), wings, axes=(1, 0, 0)),
        ],
        "time": [  # speed: bells lengthen aft, wings sweep back and thin, the nose sharpens
            morphs.stretch_along((0, 1, 0), (0, 2.10, 0), 1.9, bells, ramp=(0.0, 0.05)),
            morphs.sweep((1, 0, 0), (0, 0, 0), (0, 0.22, 0), morphs.falloff_mask(mesh, wings, (0, 0, 0), (1, 0, 0), 0.0, 0.05)),
            morphs.sweep((-1, 0, 0), (0, 0, 0), (0, 0.22, 0), morphs.falloff_mask(mesh, wings, (0, 0, 0), (-1, 0, 0), 0.0, 0.05)),
            morphs.scale_about(0.72, (0, 0, 0), wings, axes=(0, 0, 1)),
            morphs.taper((0, -1, 0), (0, 0.4, 0), 1.4, 0.70, fuse_m),
            morphs.stretch_along((0, -1, 0), (0, -0.93, 0), 1.12, jaws, ramp=(0.0, 0.4)),
        ],
    }
    for name, defs in shapes.items():
        kb, mx, frac, moved = morphs.add_shape(mesh, name, defs)
        ctx.log("shape %s: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))

    # ------------------------------------------------------------------ TAKES (motion, unbound; the artist's names)
    def case_fan(sign):
        d = {}
        for tag, sx in SIDES:
            for cname in CASES:
                d[cname + "." + tag] = (sign * 30, 0, 0)
        return d

    def pose(p):
        if p == "pitchup":
            d = {"wing.l": (-28, 0, 0), "wing.r": (-28, 0, 0), "fuse": (-10, 0, 0)}
            d.update(case_fan(-1))
            return d
        if p == "pitchdown":
            d = {"wing.l": (28, 0, 0), "wing.r": (28, 0, 0), "fuse": (10, 0, 0)}
            d.update(case_fan(1))
            return d
        if p in ("yawleft", "yawright"):
            s = 1 if p == "yawright" else -1
            return {"wing.l": (0, 0, s * 26), "wing.r": (0, 0, s * 26), "fuse": (0, 0, s * 12),
                    **{c + "." + t: (0, 0, s * 28) for c in CASES for t, _ in SIDES}}
        if p in ("rollleft", "rollright"):
            s = 1 if p == "rollright" else -1
            return {"wing.l": (0, s * 35, 0), "wing.r": (0, s * 35, 0), "fuse": (0, s * 20, 0),
                    **{c + "." + t: (0, s * 30, 0) for c in CASES for t, _ in SIDES}}
        if p == "speedup":
            return {"wing.l": {"rot": (0, 0, 45), "loc": (0, 0.25, 0)}, "wing.r": {"rot": (0, 0, -45), "loc": (0, 0.25, 0)},
                    **{c + "." + t: (0, 0, 0) for c in CASES for t, _ in SIDES}}
        if p == "speeddown":
            return {"wing.l": {"rot": (-12, 0, -30), "loc": (0, -0.2, 0)}, "wing.r": {"rot": (-12, 0, 30), "loc": (0, -0.2, 0)}}
        if p == "jawcharge":
            return {"jaw.u": (-24, 0, 0), "jaw.b": (24, 0, 0)}
        if p == "startcharge":
            return {"wing.l": {"loc": (0, -0.9, 0)}, "wing.r": {"loc": (0, -0.9, 0)}, "jaw.u": (-8, 0, 0), "jaw.b": (8, 0, 0)}
        return {}

    import bpy
    bpy.context.scene.render.fps = 24
    acts = {}
    for p in TAKES:
        acts[p] = takes.motion_take(arm, p + "_dolphin", [(1, {}), (21, pose(p))])
    takes.clear_pose(arm)

    lo, hi = f.bounds(mesh)
    ctx.log("bounds (blender) %s .. %s" % (tuple(round(v, 3) for v in lo), tuple(round(v, 3) for v in hi)))
    return {
        "armature": arm, "mesh": mesh,
        "spec": {
            "preset": "yup", "armature": ARMATURE, "mesh_node": MESH_NODE,
            "bones": bone_names,
            "takes": [ARMATURE + "|" + p + "_dolphin" for p in TAKES],
            "shapes": ["charge", "mass", "space", "time"],
            "slots": list(SLOTS),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.04,
        },
    }


def Vector_dot(n, ax):
    return n.x * ax[0] + n.y * ax[1] + n.z * ax[2]


def inward(c, n, origin, axis):
    """True for a face whose normal points toward the axis through `origin` (an inner wall)."""
    from mathutils import Vector
    a = Vector(axis).normalized()
    d = Vector(c) - Vector(origin)
    radial = d - a * d.dot(a)
    if radial.length < 1e-6:
        return False
    return Vector(n).dot(radial.normalized()) < -0.3
