"""
squirrel - the artist's hull, kept, with the framework on top (the artist-hull path, LESSONS 38).

The first rebuild redrew this vessel from primitives and the owner judged it a cheap remake
(2026-10-18). This spec keeps the artist's geometry, slots and colours EXACTLY as the game
draws them and adds what the file lacked:
  - the parked pose the player sees is baked into the rest (mesh and bones), so the rest IS the
    drawn hull and every take is authored against what the player sees;
  - a tail SPINE in ten segments, one skinned to each chevron bone, so the nine floating ribs
    hang on something and bend together as a chain under the takes;
  - the nine takes re-authored with the chain tail and the whole-body turn directions the
    artist's own takes use (a right turn is -Z, roll right drops the right side), measured by
    the pose gate;
  - Mass strengthened from 2.46% of the diagonal (the fleet floor; invisible from the chase
    camera) to above 4%, on the hull and pods; Time / Space / Charge kept as the artist made them.
LEGACY BINDINGS (file path, node / geometry / armature / bone names, slot order, take names,
frame) are the artist's and are untouched: the prefab, its .meta and the Animator bind them.
  file      Assets/_Models/Vessel Models/SquirrelVessel_CosmicShoresTest1.fbx (in place, same guid)
  takes     a_Squirrel_Armature|squirrelanim_{idle,pitchup,pitchdown,yawleft,yawright,rollleft,
            rollright,speedup,speeddown}  (single-frame POSES the controller's 2D blend trees mix)
  slots     0 Domain, 1 Body, 2 Window, 3 Engine (the artist's order and colours)
  frame     preset "zup": nose -Y, up +Z, .L on +X in the Blender frame (the importer's frame)
"""

import math

NAME = "Squirrel"
OUTPUT = "Assets/_Models/Vessel Models/SquirrelVessel_CosmicShoresTest1.fbx"

ARMATURE = "a_Squirrel_Armature"
MESH_NODE = "a_SquirrelShipMesh_nearfinal"
GEOMETRY = "Plane.011"
SLOTS = ("Domain", "Body", "Window", "Engine")
D, H, W, J = 0, 1, 2, 3
TAKE_PREFIX = "squirrelanim_"

SIDES = (("L", 1), ("R", -1))  # the artist's .L is +X in Blender
CHEVRONS = ["bbone_Tail1First", "bbone_Tail2", "bbone_Tail3", "bbone_Tail4", "bbone_Tail5",
            "bbone_Tail6", "bbone_Tail7", "bbone_Tail8", "bbone_Tail9Last"]


def build(ctx):
    f, rig, morphs, takes = ctx.forms, ctx.rig, ctx.morphs, ctx.takes
    from mathutils import Vector

    # ------------------------------------------------------------------ THE ARTIST'S HULL, AS DRAWN
    arm, mesh = rig.import_shipped_model(ctx.artist(OUTPUT))
    assert arm.name == ARMATURE, arm.name
    bone_names = [b.name for b in arm.data.bones]
    baked = rig.bake_parked_pose(mesh, arm, deform_mesh=True)  # the artist parks 27 bones ~11 degrees: the drawn hull becomes the rest
    ctx.log("artist hull: %d verts, %d tris, %d bones; parked pose baked on %d bone(s)" % (
        len(mesh.data.vertices), f.tri_count(mesh), len(bone_names), len(baked)))

    def centroid(group):
        vg = mesh.vertex_groups[group]
        pts = [mesh.matrix_world @ v.co for v in mesh.data.vertices if any(g.group == vg.index and g.weight > 0.5 for g in v.groups)]
        return sum(pts, Vector()) / len(pts)

    # ------------------------------------------------------------------ THE SPINE the chevrons hang on
    # the artist's nine chevrons float (dossier 9, "chips in orbit"); a tapered boom runs from inside the
    # cabin through every chevron's centre to just past the last, in segments skinned to the chevron each
    # one leads to, so it bends WITH the chain the takes make of them
    cents = [centroid(b) for b in CHEVRONS]
    TAIL_ROOT = (0.0, cents[0].y - 0.40, cents[0].z + 0.01)
    tip = cents[-1] + (cents[-1] - cents[-2]).normalized() * 0.20
    spine_pts = [Vector(TAIL_ROOT)] + [Vector((0.0, c.y, c.z)) for c in cents] + [Vector((0.0, tip.y, tip.z))]
    spine_r = [0.17, 0.155, 0.14, 0.125, 0.11, 0.10, 0.09, 0.08, 0.065, 0.05, 0.035]
    segs = []
    for k in range(len(spine_pts) - 1):
        bone = "bbone_WholeTail" if k == 0 else CHEVRONS[k - 1]
        seg = f.tube_along(bone, [tuple(spine_pts[k]), tuple(spine_pts[k + 1])], [spine_r[k], spine_r[k + 1]], segments=12)
        f.paint(seg, H)
        f.paint(seg, D, lambda c, n, i: n.z > 0.7)   # a domain stripe along the top, like the chevrons' tops
        f.bevel(seg, width=0.008, segments=1, angle_deg=30)
        f.apply_modifiers(seg)
        segs.append(seg)
    tail_rows = [(b, c.y, 0.0, c.z, 0.0) for b, c in zip(CHEVRONS, cents)]  # (name, y centre, -, z centre, -) for the pose builders
    mesh = f.join([mesh] + segs, MESH_NODE, SLOTS)   # the artist's mesh is active: its shape keys and groups survive the join
    mesh.data.name = GEOMETRY
    ctx.log("spine: %d segments joined; hull now %d verts, %d tris" % (len(segs), len(mesh.data.vertices), f.tri_count(mesh)))
    bones = arm.data.bones

    # ------------------------------------------------------------------ MORPHS: the artist's four, Mass strengthened
    ms = morphs.MaskSet(mesh)
    hull = ms.mask("abone_MAIN_Cockpit", "bbone_LowerBody", "bbone_BodyWing", "bbone_UpperHelmet", "bbone_LowerHelmet",
                   "bbone_cheek.L", "bbone_cheek.R")
    pods = ms.mask(*[b.name for b in bones if "Engine" in b.name])
    kb, mx, frac, moved = morphs.augment_shape(mesh, "Mass", [morphs.inflate(0.30, hull), morphs.inflate(0.14, pods)])
    ctx.log("shape Mass strengthened: travel %.3f (%.1f%% of diag), %d verts" % (mx, 100 * frac, moved))
    ctx.log("shapes Time / Space / Charge: the artist's, kept")

    # ------------------------------------------------------------------ POSES (deg about WORLD X=pitch, Y=roll, Z=yaw; nose -Y)
    # The tail bones are SIBLINGS under bbone_WholeTail (not a chain), so the gross swing lives on
    # WholeTail and the per-chevron curl rides on top. The pods are siblings of the wing: `follow`
    # makes them ride the wing's bank so the forearm struts stay in their sockets. Magnitudes are
    # big on purpose: the controller's top blend tree HALVES every pose (dossier 3.2); the artist's
    # takes drop the tail tip 1.1-1.4 units for pitch and whip it 2.2-2.5 sideways for yaw.
    PODS = ("bbone_FrontEngineBase", "bbone_FrontEngineTip", "bbone_FrontEngineTrim")
    TAIL_BONES = ["bbone_WholeTail"] + [name for (name, *_r) in tail_rows]

    def whole_tail(rot):
        """The whole tail swings rigidly about its root: WholeTail and every sibling chevron bone ride the same rotation."""
        return {b: takes.follow(arm, "bbone_WholeTail", b, rot, pivot=TAIL_ROOT) for b in TAIL_BONES}

    def tail_chain(root_deg, tip_curl_deg, axis):
        """
        The tail as a kinematic CHAIN from TAIL_ROOT about one world axis (0 X pitch, 1 Y twist, 2 Z yaw):
        the spine root (WholeTail) turns by root_deg; chevron k turns by root_deg plus an accumulated curl
        that reaches tip_curl_deg at the last chevron, and SITS where the bent chain puts it. The chevrons
        are siblings in the artist's rig, so a rigid swing plus a curl about each chevron's own head fanned
        them apart and left the spine poking out (sheet, round 3); a chain keeps spine and chevrons together.
        """
        import rig as _rig
        from mathutils import Vector as _V, Euler as _E

        def R(deg):
            e = [0.0, 0.0, 0.0]
            e[axis] = math.radians(deg)
            return _E(e, "XYZ").to_matrix()

        def euler(deg):
            v = [0.0, 0.0, 0.0]
            v[axis] = deg
            return tuple(v)
        out = {}
        root = _V(TAIL_ROOT)
        wt = _rig.bone_world_head(arm, "bbone_WholeTail")
        out["bbone_WholeTail"] = {"rot": euler(root_deg), "loc": tuple(root + R(root_deg) @ (wt - root) - wt)}
        w = [0.25 + 0.75 * i / 8.0 for i in range(len(tail_rows))]
        inc = [tip_curl_deg * wi / sum(w) for wi in w]
        prev, prev_new, th = root, root, root_deg
        for (name, *_r), dk in zip(tail_rows, inc):
            h = _rig.bone_world_head(arm, name)
            h_new = prev_new + R(th) @ (h - prev)   # this link carries the orientation of the joint before it
            th += dk
            out[name] = {"rot": euler(th), "loc": tuple(h_new - h)}
            prev, prev_new = h, h_new
        return out

    def add_curl(d, curl):
        """Compose a per-chevron curl onto the gross swing (small angles: Euler sum)."""
        for b, rot in curl.items():
            if b in d and isinstance(d[b], dict):
                r0 = d[b]["rot"]
                d[b] = {"rot": (r0[0] + rot[0], r0[1] + rot[1], r0[2] + rot[2]), "loc": d[b]["loc"]}
            else:
                d[b] = rot
        return d

    def tail_curl(sign, amount, axis=0):
        out = {}
        for i, (name, *_r) in enumerate(tail_rows):
            v = [0, 0, 0]
            v[axis] = sign * amount * (0.25 + 0.75 * i / 8.0)
            out[name] = tuple(v)
        return out

    def wing_bank(l_rot, r_rot, pods=True):
        """The whole wing (shoulder, feathers) and, riding it, the front pods."""
        out = {}
        for tag, rot in (("L", l_rot), ("R", r_rot)):
            for k in range(1, 7):
                out["bbone_WingPiece%d.%s" % (k, tag)] = rot
            out["bbone_WholeWing." + tag] = rot
            if pods:
                for pb in PODS:
                    out["%s.%s" % (pb, tag)] = takes.follow(arm, "bbone_WholeWing." + tag, "%s.%s" % (pb, tag), rot)
        return out

    def pose(p):
        if p == "idle":
            return {}
        if p == "pitchup":    # nose up: the tail drops as a chain (-X), curling further toward the tip; head lifts, wing noses up
            d = tail_chain(-12, -24, 0)
            d.update(wing_bank((-16, 0, 0), (-16, 0, 0)))
            d.update({"bbone_UpperHelmet": (10, 0, 0), "bbone_LowerHelmet": (10, 0, 0), "bbone_Ears": (14, 0, 0)})
            return d
        if p == "pitchdown":
            d = tail_chain(12, 24, 0)
            d.update(wing_bank((16, 0, 0), (16, 0, 0)))
            d.update({"bbone_UpperHelmet": (-10, 0, 0), "bbone_LowerHelmet": (-10, 0, 0), "bbone_Ears": (-14, 0, 0)})
            return d
        if p in ("yawleft", "yawright"):
            # nose -Y, pilot's left +X: a right turn noses toward -X, so the tail swings to the OUTSIDE,
            # +X, which is a NEGATIVE rotation about Z for a tail that points +Y (measured on the artist's
            # yawright: tail tip dx +2.46)
            s = -1 if p == "yawright" else 1
            d = tail_chain(s * 20, s * 30, 2)
            d.update(wing_bank((0, 0, s * 12), (0, 0, s * 12), pods=False))
            d.update({"bbone_UpperHelmet": (0, 0, -s * 9), "bbone_LowerHelmet": (0, 0, -s * 9), "bbone_Ears": (0, 0, -s * 10)})
            return d
        if p in ("rollleft", "rollright"):  # a bank about the hull's long axis: wings and pods go round, the tail twists
            s = -1 if p == "rollright" else 1  # +Y rotation drops the +X (.L) wing: rollright is NEGATIVE about Y
            d = wing_bank((0, s * 32, 0), (0, s * 32, 0))
            d.update(tail_chain(s * 10, s * 16, 1))
            d.update({"bbone_cheek.L": (0, 0, s * 10), "bbone_cheek.R": (0, 0, s * 10)})
            return d
        if p == "speedup":   # everything streamlines: the fan folds back, the paws reach, the tail straightens
            d = wing_bank((0, 0, 18), (0, 0, -18), pods=False)
            d.update({"%s.%s" % (pb, t): {"loc": (0, -dy, 0)} for t in ("L", "R") for pb, dy in zip(PODS, (0.10, 0.16, 0.14))})
            d["bbone_Ears"] = (-10, 0, 0)
            d.update(tail_chain(4, 6, 0))
            return d
        if p == "speeddown":  # the fan spreads as an airbrake, the paws pull in, the tail flares
            d = wing_bank((-6, 0, -14), (-6, 0, 14), pods=False)
            d.update({"%s.%s" % (pb, t): {"loc": (0, dy, 0)} for t in ("L", "R") for pb, dy in zip(PODS, (0.08, 0.12, 0.10))})
            d["bbone_Ears"] = (6, 0, 0)
            d.update(tail_chain(-5, -8, 0))
            return d
        return {}

    acts = takes.standard_pose_set(arm, pose, prefix=TAKE_PREFIX)

    lo, hi = f.bounds(mesh)
    ctx.log("bounds (blender) %s .. %s" % (tuple(round(v, 3) for v in lo), tuple(round(v, 3) for v in hi)))
    return {
        "armature": arm, "mesh": mesh,
        "spec": {
            "preset": "zup", "armature": ARMATURE, "mesh_node": MESH_NODE,
            "bones": bone_names,
            "takes": [ARMATURE + "|" + TAKE_PREFIX + p for p in takes.STANDARD_POSES],
            "pose_takes": [ARMATURE + "|" + TAKE_PREFIX + p for p in takes.STANDARD_POSES],
            # each take must move the named part the named way (re-import, centroid deltas; LESSONS 34)
            "pose_expect": {
                TAKE_PREFIX + "pitchup": [("bbone_Tail9Last", "z", -1, 0.8)],
                TAKE_PREFIX + "pitchdown": [("bbone_Tail9Last", "z", +1, 0.8)],
                TAKE_PREFIX + "yawright": [("bbone_Tail9Last", "x", +1, 1.0)],   # the tail swings to the OUTSIDE (+X, the pilot's left)
                TAKE_PREFIX + "yawleft": [("bbone_Tail9Last", "x", -1, 1.0)],
                TAKE_PREFIX + "rollright": [("bbone_FrontEngineBase.L", "z", +1, 0.2), ("bbone_FrontEngineBase.R", "z", -1, 0.2)],
                TAKE_PREFIX + "rollleft": [("bbone_FrontEngineBase.R", "z", +1, 0.2), ("bbone_FrontEngineBase.L", "z", -1, 0.2)],
                TAKE_PREFIX + "speedup": [("bbone_WingPiece6.R", "x", +1, 0.15)],   # the fan folds inward
                TAKE_PREFIX + "speeddown": [("bbone_WingPiece6.R", "x", -1, 0.15)],  # the fan spreads
            },
            "shapes": ["Time", "Mass", "Space", "Charge"],
            "slots": list(SLOTS),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.025,
        },
    }
