"""
serpent - the one-thumb sniper: an insect-serpent HEAD that flies face-first with four long
tendrils trailing behind it, a carapace plate over its back and a three-pad engine block at
the rear. The scope is its eye. Written IN PLACE over the live model:

  file      Assets/_Models/Vessel Models/SerpentExport.fbx (same guid; serpent.fbx is a vestige)
  armature  SerpentArmature    mesh node  SerpentMesh    geometry  Cube.190
  bones     the shipped 29 (HeadRootBone root; EngineBone, ChassisBone; HeadTail1-5 + HeadTailSpike
            .L/.R; BotTail1-5 + BotTailSpike .L/.R) reused as-is: the prefab overrides 24 of their
            rest transforms and the DampedTransform chains bind them by path
  takes     SerpentArmature|Serpent{Idle,Up,Down,Left,Right,SpeedIncrease}Anim - 1-key poses that
            lean the WHOLE hull (HeadRootBone is the skeleton root) 25-30 degrees
  slots     0 Body, 1 Engine, 2 Window, 3 Domain  (the shipped order: slot 0 takes the domain paint
            today; the dossier's alternative - fleet order + a prefab edit - is a separate call)
  frame     preset "zup_identity": the file IS the Blender frame. NOSE ON +Y, up +Z, the pilot's
            RIGHT on +X (.R bones at +X). The prefab's instance turns and scales it.
  size      x +-4.2, y -10.1 (tendril tips) .. +1.49 (visor), z -5.2 .. +6.1; origin inside the
            head; camera -250 (x2 instance): only silhouette, the eye and the tendril sweep read

Design: a hooded serpent head. A crowned skull with a big sunk visor (the eye of the scope)
on the face, a ridged carapace over the back, cheek plates, four banded tendrils that trail up
and down behind, each with a side spike, and a stern block with three lime exhaust pads.
Morphs (dossier 4.2): charge = a stinger barrel extends from the visor and the spikes grow;
mass = the carapace and the engine block swell; space = the tendrils spread and the visor
grows; time = the tendrils sweep back and flatten, the pads lengthen aft.
"""

import math

from _common import flip, retag, box_uv, finish, rot_x, rot_z, translate

NAME = "Serpent"
OUTPUT = "Assets/_Models/Vessel Models/SerpentExport.fbx"

ARMATURE = "SerpentArmature"
MESH_NODE = "SerpentMesh"
GEOMETRY = "Cube.190"
SLOTS = ("Body", "Engine", "Window", "Domain")
H, J, W, D = 0, 1, 2, 3
TAKES = ("Idle", "Up", "Down", "Left", "Right", "SpeedIncrease")
SIDES = (("R", 1), ("L", -1))  # .R is +X here (the pilot's right)

# bone chains (Blender frame = file frame), from part_map on the shipped rig
HEAD_CHAIN = [(1.416, -2.240, 2.197), (1.416, -3.796, 2.831), (2.709, -4.699, 3.278), (2.874, -6.537, 4.671), (3.40, -8.6, 5.6), (3.9, -10.1, 6.1)]
BOT_CHAIN = [(1.03, 0.495, -1.134), (1.224, -1.042, -1.784), (2.096, -3.595, -2.572), (3.256, -5.290, -3.200), (3.75, -7.6, -4.2), (4.2, -10.0, -5.2)]


def build(ctx):
    f, rig, morphs, takes = ctx.forms, ctx.rig, ctx.morphs, ctx.takes
    parts = []

    # ------------------------------------------------------------------ HEAD (HeadRootBone)
    # the skull: a broad hooded dome, nose (visor) at +Y 1.49, back of the skull at -2.5
    skull = f.fuselage("head_skull", 4.00, nose=(0, 1.49, 0.20), axis=(0, -1, 0), segments=32, stations=[
        (0.00, 0.95, 0.80, 0.70, 2.8, 2.6, 0.00),
        (0.10, 1.35, 1.25, 1.05, 3.0, 2.8, 0.10),
        (0.30, 1.66, 1.70, 1.35, 3.2, 2.8, 0.25),
        (0.55, 1.66, 1.80, 1.40, 3.2, 2.8, 0.30),
        (0.80, 1.45, 1.55, 1.25, 3.0, 2.6, 0.25),
        (1.00, 0.95, 0.95, 0.80, 2.6, 2.4, 0.05),
    ])
    f.paint(skull, H)
    # the visor: the whole face, sunk and glassed (the scope's eye) - the dossier asks for a BIG eye
    f.inset_faces(skull, lambda c, n, i: n.y > 0.25 and c.y > 0.45 and c.z > -0.45 and abs(c.x) < 1.35, 0.06, depth=0.10, slot=W)
    # cheek panels and a brow ridge wear the domain trim
    f.inset_faces(skull, lambda c, n, i: i == H and abs(n.x) > 0.6 and -1.6 < c.y < 0.4 and -0.6 < c.z < 1.2, 0.05, depth=0.04, slot=D)
    f.paint(skull, D, lambda c, n, i: i == H and n.z > 0.75 and -1.2 < c.y < 0.5 and abs(c.x) < 0.26)
    # a crown ridge
    crown = f.plate("head_crown", f.blade(2.2, 0.5, tip=0.3), thickness=0.12, normal=(1, 0, 0), origin=(0, 0.6, 1.75), right=(0, -1, 0.25))
    f.paint(crown, D)
    finish(f, crown, bevel_width=0.015)
    finish(f, skull, bevel_width=0.02)
    # jaw: an under-bite plate with a domain chevron
    jaw = f.fuselage("head_jaw", 2.6, nose=(0, 1.25, -0.95), axis=(0, -1, 0), segments=20, stations=[
        (0.00, 0.40, 0.18, 0.22, 3.0, 2.6, 0.00),
        (0.35, 1.05, 0.30, 0.42, 3.6, 2.8, 0.00),
        (0.75, 1.10, 0.30, 0.44, 3.6, 2.8, 0.05),
        (1.00, 0.70, 0.20, 0.30, 3.0, 2.6, 0.10),
    ])
    f.paint(jaw, H)
    f.paint(jaw, D, lambda c, n, i: n.z < -0.6 and abs(c.x) < 0.45 and c.y > -0.6)
    finish(f, jaw, bevel_width=0.015)
    # two horns on the crown
    horns = []
    for tag, sx in SIDES:
        hn = f.cone("horn_" + tag, 0.22, 1.25, axis=(sx * 0.35, -0.55, 0.75), origin=(sx * 0.75, -0.95, 1.45), segments=12)
        f.paint(hn, D)
        horns.append(hn)
    # a short stinger at the visor's centre (the Charge morph extends it)
    stinger = f.cone("head_stinger", 0.16, 0.45, axis=(0, 1, 0), origin=(0, 1.35, 0.05), segments=12)
    f.paint(stinger, D)
    head = f.join([skull, jaw, stinger, crown] + horns, "HeadRootBone", SLOTS)
    retag(f, head)
    parts.append(head)

    # ------------------------------------------------------------------ CARAPACE (ChassisBone)
    cara = f.fuselage("ChassisBone", 4.60, nose=(0, 0.60, 1.25), axis=(0, -1, 0), segments=26, stations=[
        (0.00, 0.80, 0.30, 0.20, 3.4, 2.4, 0.00),
        (0.25, 1.55, 0.80, 0.35, 3.6, 2.4, 0.20),
        (0.55, 1.80, 1.05, 0.40, 3.6, 2.4, 0.35),
        (0.80, 1.60, 0.90, 0.35, 3.4, 2.4, 0.40),
        (1.00, 1.00, 0.45, 0.25, 3.0, 2.4, 0.35),
    ])
    f.paint(cara, H)
    # three ridged plates across the back wear the domain
    for (y0, y1) in ((0.2, -0.8), (-1.2, -2.2), (-2.6, -3.4)):
        f.inset_faces(cara, lambda c, n, i, y0=y0, y1=y1: i == H and n.z > 0.45 and y1 < c.y < y0, 0.05, depth=0.05, slot=D)
    finish(f, cara, bevel_width=0.02)
    parts.append(cara)

    # ------------------------------------------------------------------ ENGINE BLOCK (EngineBone)
    eng = f.fuselage("engine_block", 1.70, nose=(0, -3.00, 0.05), axis=(0, -1, 0), segments=24, stations=[
        (0.00, 1.20, 0.90, 0.85, 3.8, 3.4, 0.00),
        (0.40, 1.70, 1.25, 1.10, 4.0, 3.6, 0.05),
        (0.80, 1.65, 1.20, 1.05, 4.0, 3.6, 0.05),
        (1.00, 1.30, 0.95, 0.85, 3.6, 3.2, 0.00),
    ])
    f.paint(eng, H)
    f.inset_faces(eng, lambda c, n, i: i == H and abs(n.x) > 0.7 and -4.4 < c.y < -3.3, 0.04, depth=0.03, slot=D)
    finish(f, eng, bevel_width=0.015)
    pads = []
    for k, (px, pz) in enumerate(((0.0, 0.55), (-1.05, -0.35), (1.05, -0.35))):
        pad = f.lathe("pad_%d" % k, [(0.0, 0.0), (0.42, 0.0), (0.52, 0.08), (0.52, 0.40), (0.46, 0.46), (0.0, 0.46)],
                      segments=18, axis=(0, -1, 0), origin=(px, -4.45, pz))
        f.paint(pad, H)
        f.inset_faces(pad, lambda c, n, i: n.y < -0.8, 0.05, depth=0.10, slot=J)
        finish(f, pad, bevel_width=0.01)
        pads.append(pad)
    engine = f.join([eng] + pads, "EngineBone", SLOTS)
    retag(f, engine)
    parts.append(engine)

    # ------------------------------------------------------------------ TENDRILS (the bone chains)
    for tag, sx in SIDES:
        for chain, prefix, spike_at in ((HEAD_CHAIN, "HeadTail", 1), (BOT_CHAIN, "BotTail", 2)):
            pts = [(sx * x, y, z) for (x, y, z) in chain]
            radii = [0.34, 0.30, 0.25, 0.20, 0.14, 0.06]
            tube = f.tube_along("%s_tube.%s" % (prefix, tag), pts, radii, segments=14)
            f.paint(tube, H)
            # banded tips: alternating domain rings along the last third
            f.paint(tube, D, lambda c, n, i, pts=pts: c.y < pts[3][1] and int((pts[3][1] - c.y) / 0.45) % 2 == 0)
            f.sharpen_by_angle(tube, 40)
            # the side spike at the second joint
            sp = f.cone("%sSpike.%s" % (prefix, tag), 0.17, 1.40, axis=(sx * 0.75, -0.45, 0.45 if prefix == "HeadTail" else -0.6),
                        origin=pts[spike_at], segments=10)
            f.paint(sp, D)
            # split the tube into its bone segments by distance along the chain
            _assign_chain_groups(tube, pts, ["%s%d.%s" % (prefix, k, tag) for k in (1, 2, 3, 4, 5)])
            parts += [tube, sp]

    # ------------------------------------------------------------------ JOIN
    mesh = f.join(parts, MESH_NODE, SLOTS)
    mesh.data.name = GEOMETRY
    f.set_smooth(mesh, True)
    f.sharpen_by_angle(mesh, 44)
    box_uv(mesh)
    ctx.log("serpent joined: %d verts, %d tris" % (len(mesh.data.vertices), f.tri_count(mesh)))

    # ------------------------------------------------------------------ RIG: the shipped armature
    arm = rig.import_shipped_armature(OUTPUT)
    assert arm.name == ARMATURE, arm.name
    bone_names = [b.name for b in arm.data.bones]
    g2b = {}
    for tag, _sx in SIDES:
        for prefix in ("HeadTail", "BotTail"):
            g2b["%s_tube.%s" % (prefix, tag)] = "%s1.%s" % (prefix, tag)  # leftovers of the tube go to segment 1
    rig.skin_rigid(mesh, arm, group_to_bone=g2b, default_bone="HeadRootBone")
    ctx.log("rig: %d bones reused" % len(bone_names))

    # ------------------------------------------------------------------ MORPHS (dossier 4.2)
    msk = morphs.MaskSet(mesh)
    head = msk.mask("HeadRootBone")
    cara_m = msk.mask("ChassisBone")
    eng_m = msk.mask("EngineBone")
    tendrils = msk.mask(*[b for b in bone_names if ("Tail" in b and "Spike" not in b)])
    spikes = msk.mask(*[b for b in bone_names if "Spike" in b])
    stinger_m = morphs.radial_mask(mesh, head, (0, 1.6, 0.05), 0.25, 0.55)
    shapes = {
        "Charge": [  # the rifle: the stinger extrudes 2 units from the visor, the spikes grow, the brow thickens
            morphs.stretch_along((0, 1, 0), (0, 1.35, 0), 5.0, stinger_m, ramp=(0.0, 0.05)),
            morphs.scale_about(1.6, (0, -3.5, 0), spikes, axes=(1, 1, 1)),
        ],
        "Mass": [  # girth: the carapace and the engine block swell, the crown widens
            morphs.scale_about(1.30, (0, -1.5, 0.9), cara_m, axes=(1, 0, 1)), morphs.inflate(0.22, cara_m),
            morphs.scale_about(1.32, (0, -3.8, 0.05), eng_m, axes=(1, 0, 1)), morphs.inflate(0.20, eng_m),
            morphs.scale_about(1.14, (0, -0.5, 0.3), head, axes=(1, 0, 0)),
        ],
        "Space": [  # width and the eye: the tendrils spread outward, the visor grows
            morphs.scale_about(1.36, (0, 0, 0), morphs.falloff_mask(mesh, tendrils, (0, 0, 0), (0, -1, 0), 2.0, 5.0), axes=(1, 0, 0)),
            morphs.inflate(0.12, morphs.radial_mask(mesh, head, (0, 1.4, 0.3), 0.6, 1.6)),
        ],
        "Time": [  # sweep: the tendrils fold back and flatten toward the centreline, the pads lengthen aft
            morphs.sweep((0, -1, 0), (0, -2.0, 0), (0, -0.25, 0), tendrils, ramp=(0.0, 3.0)),
            morphs.scale_about(0.72, (0, 0, 0), morphs.falloff_mask(mesh, tendrils, (0, 0, 0), (0, -1, 0), 3.0, 6.0), axes=(1, 0, 1)),
            morphs.stretch_along((0, -1, 0), (0, -4.4, 0), 3.0, morphs.falloff_mask(mesh, eng_m, (0, 0, 0), (0, -1, 0), 4.3, 4.5), ramp=(0.0, 0.05)),
        ],
    }
    for name, defs in shapes.items():
        kb, mx, frac, moved = morphs.add_shape(mesh, name, defs)
        ctx.log("shape %s: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))

    # ------------------------------------------------------------------ POSES (world axes; nose +Y, right +X)
    def pose(p):
        if p == "Up":
            return {"HeadRootBone": (30, 0, 0)}          # +X rotation lifts the +Y nose
        if p == "Down":
            return {"HeadRootBone": (-30, 0, 0)}
        if p == "Left":                                    # nose to -X and bank left-side-down
            return {"HeadRootBone": (0, -25, 25)}
        if p == "Right":
            return {"HeadRootBone": (0, 25, -25)}
        if p == "SpeedIncrease":                           # the telescoping stern
            return {"ChassisBone": {"loc": (0, -3.566, 0)}, "EngineBone": {"loc": (0, -1.09, 0)}}
        return {}

    acts = {}
    for p in TAKES:
        acts[p] = takes.pose_take(arm, "Serpent%sAnim" % p, pose(p))
    takes.clear_pose(arm)

    lo, hi = f.bounds(mesh)
    ctx.log("bounds (blender) %s .. %s" % (tuple(round(v, 3) for v in lo), tuple(round(v, 3) for v in hi)))
    return {
        "armature": arm, "mesh": mesh,
        "spec": {
            "preset": "zup_identity", "armature": ARMATURE, "mesh_node": MESH_NODE, "nose": +1,
            "sheet_poses": ("up", "right", "left", "speedincrease"),
            "bones": bone_names,
            "takes": [ARMATURE + "|Serpent%sAnim" % p for p in TAKES],
            "pose_takes": [ARMATURE + "|Serpent%sAnim" % p for p in TAKES],
            "shapes": ["Space", "Mass", "Time", "Charge"],
            "slots": list(SLOTS),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.03,
        },
    }


def _assign_chain_groups(obj, pts, names):
    """Split a tube's vertices into vertex groups by the nearest chain segment, so each bone
    of a DampedTransform chain bends its own stretch of tendril."""
    import bpy  # noqa: F401
    from mathutils import Vector
    segs = []
    for i in range(len(pts) - 1):
        a, b = Vector(pts[i]), Vector(pts[i + 1])
        segs.append((a, b, names[min(i, len(names) - 1)]))
    groups = {}
    for v in obj.data.vertices:
        best, best_d = None, 1e9
        for a, b, n in segs:
            ab = b - a
            t = max(0.0, min(1.0, (v.co - a).dot(ab) / max(ab.length_squared, 1e-9)))
            d = (a + ab * t - v.co).length
            if d < best_d:
                best, best_d = n, d
        groups.setdefault(best, []).append(v.index)
    for vg in list(obj.vertex_groups):
        obj.vertex_groups.remove(vg)
    for n, idx in groups.items():
        obj.vertex_groups.new(name=n).add(idx, 1.0, "REPLACE")
