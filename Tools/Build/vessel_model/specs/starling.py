"""
starling - a NEW body (no artist file) for one MEMBER of the Starlings swarm (VesselClassType.Starling = 21). The
Starlings are a swarm, not a hull: every member is drawn with this one model, so it is small, cheap (one skinned mesh,
five bones) and reads at swarm distance. Design: Docs/Studios/SWARM_CARD_VESSEL.md (the cards), Docs/Studios/
STARLING_LAB.md (the lab), Assets/_Scripts/Controller/Vessel/R_VesselActions/STARLING.md (the Unity vessel).

Named by Garrett, 2026-10-20: "the krill will be a swarm of vessels lets change the to the starlings" - a murmuration.
What it must read as: a starling, as a spaceship (Garrett, 2026-10-18: "don't forget they are spaceships inspired by
earth clades"). The view is the swarm view (StarlingCameraSettingsSO followOffset (0, 0, -1200), about 1.3 world units
a pixel at 1080p), and the prefab draws a member at StarlingConfigSO.memberScale, so a member is ten-odd pixels on
screen: the silhouette (the starling's flying "star": a plump body, pointed triangular wings, a short square tail and a
long pointed bill), the domain stripe and speckles and the glowing bill carry the read. Clade -> ship:
  plump body, short neck -> a rounded fuselage, a domain stripe down its spine
  speckled plumage       -> domain speckle studs along each flank (the starling's white winter spots)
  long pointed bill      -> a glowing lance prong at the bow (Engine; the yellow bill of the breeding bird)
  dark eye               -> two sensor-glass domes (Window)
  triangular wings       -> two swept, pointed wing plates, a domain leading edge and wingtip
  short square tail      -> a short tail fan stabiliser, domain-edged, with a small engine mouth at its root

Contract (FLEET_CONVENTION sections 1-4 for a NEW vessel):
  file      Assets/_Models/Vessel Models/Starling.fbx (new guid), preset "zup"
  nodes     mesh `mesh`, armature `rig`; root bone `hull`
  bones     hull; head (the bill and eyes); wing.L / wing.R (the wing roots); tail (the tail fan's root)
  size      about 3.2 long (bill to tail), 4.2 across the wings, 0.8 tall (the prefab scales it by memberScale)
  slots     0 Domain, 1 Body, 2 Window (the eyes), 3 Engine (the bill, the belly lamps, the engine mouth)
  shapes    the four element jobs (SWARM_CARD_VESSEL.md), at the convention's 4-12% travel:
            Charge = current: the bill lengthens into a long lance and the lamps swell (lasers, arcs, stings);
            Mass   = volume: the body fattens (prisms, shells, the heavy forms);
            Space  = reach: the wings and the tail fan span wide (more prisms in reach);
            Time   = speed: the body stretches sleek and the wings sweep back (speed, agility)
  takes     none drive it (StarlingSwarm poses the members in code); three documentation poses for the sheet:
            flap (both wings up), turnright, turnleft
"""

import math
from _common import box_uv, finish

NAME = "Starling"
OUTPUT = "Assets/_Models/Vessel Models/Starling.fbx"

ARMATURE = "rig"
MESH_NODE = "mesh"
SLOTS = ("Domain", "Body", "Window", "Engine")
D, H, W, J = 0, 1, 2, 3

HEAD_ROOT = (0.0, -0.95, 0.10)
BILL_ROOT = (0.0, -1.30, 0.10)
WING_ROOT = (0.30, -0.30, 0.10)
TAIL_ROOT = (0.0, 0.80, 0.05)


def section(w, ht, hb, n=20, nt=2.2, nb=2.0):
    """A hull cross-section: a superellipse, its top and bottom halves of their own heights and squareness."""
    pts = []
    for i in range(n):
        t = 2 * math.pi * i / n
        c, s = math.cos(t), math.sin(t)
        if s >= 0:
            pts.append((w * math.copysign(abs(c) ** (2 / nt), c), ht * abs(s) ** (2 / nt)))
        else:
            pts.append((w * math.copysign(abs(c) ** (2 / nb), c), -hb * abs(s) ** (2 / nb)))
    return pts


def build(ctx):
    import bpy  # noqa: F401
    import bmesh
    f, rig, morphs, takes = ctx.forms, ctx.rig, ctx.morphs, ctx.takes
    Station = f.Station
    parts = []

    def loft(name, stations, n=20, nt=2.2, nb=2.0, slot=H):
        obj = f.loft(name, [Station((0, y, dz), section(w, ht, hb, n=n, nt=nt, nb=nb)) for (y, w, ht, hb, dz) in stations])
        f.ensure_slots(obj, SLOTS)
        f.paint(obj, slot)
        return obj

    # ------------------------------------------------------------------ THE BODY (hull): plump, short-necked
    body = loft("hull", [
        (-1.34, 0.05, 0.05, 0.04, 0.11), (-1.22, 0.18, 0.18, 0.13, 0.12), (-1.00, 0.27, 0.26, 0.20, 0.10),
        (-0.70, 0.33, 0.31, 0.27, 0.05), (-0.30, 0.40, 0.35, 0.33, 0.01), (0.10, 0.38, 0.33, 0.31, 0.00),
        (0.45, 0.29, 0.26, 0.23, 0.02), (0.78, 0.15, 0.13, 0.11, 0.05), (0.92, 0.08, 0.07, 0.06, 0.06)], n=22)
    # the domain stripe down the spine
    f.paint(body, D, lambda c, n, k: n.z > 0.88 and -1.05 < c.y < 0.60)
    finish(f, body, bevel_width=0.0, sharp_deg=40)
    f.subsurf(body, 1)
    f.apply_modifiers(body)
    parts.append(body)

    # ------------------------------------------------------------------ THE BILL: a glowing lance prong
    bill = f.tube_along("bill", [BILL_ROOT, (0.0, -1.55, 0.08), (0.0, -1.80, 0.05), (0.0, -1.98, 0.03)],
                        [0.075, 0.055, 0.030, 0.008], segments=8)
    f.ensure_slots(bill, SLOTS)
    f.paint(bill, J)
    parts.append(bill)

    # ------------------------------------------------------------------ THE EYES: sensor-glass domes
    for tag, sx in (("L", 1), ("R", -1)):
        eye = f.sphere("eye." + tag, 0.075, center=(sx * 0.17, -1.07, 0.21), segments=12, rings=8)
        f.ensure_slots(eye, SLOTS)
        f.paint(eye, W)
        parts.append(eye)

    # ------------------------------------------------------------------ THE SPECKLES: domain studs on the flanks
    speck_names = []
    for k, (y, z) in enumerate(((-0.55, 0.12), (-0.20, 0.02), (0.15, 0.10), (0.42, 0.0))):
        for tag, sx in (("L", 1), ("R", -1)):
            name = "speck.%d.%s" % (k, tag)
            x = sx * (0.31 if abs(y) < 0.3 else 0.27)
            sp = f.sphere(name, 0.045, center=(x, y, z), segments=8, rings=5, scale=(0.6, 1.0, 1.0))
            f.ensure_slots(sp, SLOTS)
            f.paint(sp, D)
            parts.append(sp)
            speck_names.append(name)

    # ------------------------------------------------------------------ THE WINGS: swept, pointed, triangular
    # plan outline (x out, y back) from the root: a long leading edge raking back to a sharp tip
    wing_out = [(0.0, -0.32), (0.70, -0.22), (1.40, 0.02), (1.95, 0.32), (2.05, 0.42), (1.60, 0.46),
                (1.05, 0.40), (0.50, 0.36), (0.0, 0.38)]
    for tag, sx in (("L", 1), ("R", -1)):
        pts = [(sx * x, y) for (x, y) in wing_out]
        if sx < 0:
            pts = list(reversed(pts))   # the mirrored outline keeps its winding, so its faces point out
        wing = f.plate("wing." + tag, pts, 0.07, normal=(0, 0, 1), origin=(sx * WING_ROOT[0], WING_ROOT[1], WING_ROOT[2]),
                       right=(1, 0, 0))
        f.ensure_slots(wing, SLOTS)
        f.paint(wing, H)
        f.paint(wing, D, lambda c, n, i, sx=sx: abs(c.x) > 1.85)                                     # the wingtip
        f.paint(wing, D, lambda c, n, i, sx=sx: abs(n.z) > 0.6 and c.y - WING_ROOT[1] < -0.30 + 0.27 * (abs(c.x) - WING_ROOT[0]) + 0.06
                and abs(c.x) > WING_ROOT[0] + 0.25)                                                  # the leading edge
        f.bevel(wing, width=0.012, segments=1, angle_deg=30)
        f.apply_modifiers(wing)
        parts.append(wing)

    # ------------------------------------------------------------------ THE TAIL: a short square fan
    fan_outline = [(-0.12, 0.0), (0.12, 0.0), (0.30, 0.50), (0.0, 0.46), (-0.30, 0.50)]
    bm = bmesh.new()
    top = [bm.verts.new((TAIL_ROOT[0] + u, TAIL_ROOT[1] + v, TAIL_ROOT[2] + 0.025 - 0.05 * v)) for (u, v) in fan_outline]
    bot = [bm.verts.new((TAIL_ROOT[0] + u, TAIL_ROOT[1] + v, TAIL_ROOT[2] - 0.025 - 0.05 * v)) for (u, v) in fan_outline]
    bm.faces.new(top)
    bm.faces.new(list(reversed(bot)))
    for i in range(len(top)):
        j = (i + 1) % len(top)
        bm.faces.new([top[i], bot[i], bot[j], top[j]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    tail = f.new_mesh_object("tail", bm, smooth=False)
    f.ensure_slots(tail, SLOTS)
    f.paint(tail, H)
    f.paint(tail, D, lambda c, n, i: c.y > TAIL_ROOT[1] + 0.38)                                      # the domain edge
    f.bevel(tail, width=0.008, segments=1, angle_deg=30)
    f.apply_modifiers(tail)
    parts.append(tail)

    # the engine mouth at the tail's root
    mouth = f.lathe("engine", [(0.0, 0.0), (0.06, 0.02), (0.07, 0.08), (0.05, 0.11), (0.0, 0.11)], segments=10,
                    axis=(0, 1, 0), origin=(0.0, 0.84, 0.0))
    f.ensure_slots(mouth, SLOTS)
    f.paint(mouth, H)
    f.paint(mouth, J, lambda c, n, k: c.y > 0.92)
    parts.append(mouth)

    # ------------------------------------------------------------------ THE LAMPS under the belly
    lamp_names = []
    for k, (y, z) in enumerate(((-0.75, -0.24), (-0.25, -0.32), (0.25, -0.29))):
        lamp = f.sphere("lamp.%d" % k, 0.06, center=(0.0, y, z), segments=10, rings=6, scale=(1.2, 1.0, 0.8))
        f.ensure_slots(lamp, SLOTS)
        f.paint(lamp, J)
        parts.append(lamp)
        lamp_names.append(("lamp.%d" % k, (0.0, y, z)))

    # ------------------------------------------------------------------ JOIN, n-gons out
    for p in parts:
        f.set_smooth(p, True)
    mesh = f.join(parts, MESH_NODE, SLOTS)
    mesh.data.name = MESH_NODE
    bm = bmesh.new()
    bm.from_mesh(mesh.data)
    big = [fc for fc in bm.faces if len(fc.verts) > 4]
    if big:
        bmesh.ops.triangulate(bm, faces=big, quad_method="BEAUTY", ngon_method="BEAUTY")
    bm.to_mesh(mesh.data)
    bm.free()
    f.set_smooth(mesh, True)
    f.sharpen_by_angle(mesh, 42)
    box_uv(mesh)
    ctx.log("starling joined: %d verts, %d tris (n-gons triangulated: %d)" % (len(mesh.data.vertices), f.tri_count(mesh), len(big)))

    # masks BEFORE skinning (skinning folds the sub-part groups into their bones)
    ms = morphs.MaskSet(mesh)
    body_m = ms.mask("hull", *speck_names, *[n for n, _ in lamp_names])
    head_m = ms.mask("bill", "eye.L", "eye.R")
    bill_m = ms.mask("bill")
    wings = {"L": ms.mask("wing.L"), "R": ms.mask("wing.R")}
    tail_m = ms.mask("tail", "engine")
    fan_m = ms.mask("tail")

    # ------------------------------------------------------------------ RIG
    B = rig.BoneSpec
    bones = [B("hull", (0, 0, 0), (0, -0.5, 0)),
             B("head", HEAD_ROOT, (0, HEAD_ROOT[1] - 0.4, HEAD_ROOT[2]), parent="hull"),
             B("tail", TAIL_ROOT, (0, TAIL_ROOT[1] + 0.4, TAIL_ROOT[2] - 0.02), parent="hull")]
    for tag, sx in (("L", 1), ("R", -1)):
        root = (sx * WING_ROOT[0], WING_ROOT[1], WING_ROOT[2])
        bones.append(B("wing." + tag, root, (sx * (WING_ROOT[0] + 1.6), WING_ROOT[1] + 0.3, WING_ROOT[2]), parent="hull"))
    arm = rig.build_armature(ARMATURE, bones)
    g2b = {"bill": "head", "eye.L": "head", "eye.R": "head", "wing.L": "wing.L", "wing.R": "wing.R",
           "tail": "tail", "engine": "tail"}
    rig.skin_rigid(mesh, arm, group_to_bone=g2b, default_bone="hull")
    bone_names = [b.name for b in arm.data.bones]

    # ------------------------------------------------------------------ MORPHS (4-12% of the ~5.3 diagonal)
    lamp_grow = [morphs.scale_about(2.0, c, ms.mask(name)) for (name, c) in lamp_names]
    shapes = {
        "Charge": [  # current: the bill lengthens into a lance, the lamps swell
            morphs.stretch_along((0, -1, 0), BILL_ROOT, 1.7, bill_m),
            *lamp_grow,
        ],
        "Mass": [  # volume: the body fattens about its axis
            morphs.scale_about(1.75, (0, 0, 0), body_m, axes=(1, 0, 1)),
            morphs.scale_about(1.3, HEAD_ROOT, head_m, axes=(1, 0, 1)),
        ],
        "Space": [  # reach: the wings and the tail fan span wide
            *[morphs.stretch_along((sx, 0, 0), (sx * WING_ROOT[0], 0, 0), 1.18, wings[t]) for t, sx in (("L", 1), ("R", -1))],
            morphs.scale_about(1.6, TAIL_ROOT, fan_m, axes=(1, 1, 0)),
        ],
        "Time": [  # speed: the body stretches sleek, the wings sweep back
            morphs.stretch_along((0, -1, 0), (0, 0.1, 0), 1.15, [max(a, b) for a, b in zip(body_m, head_m)]),
            morphs.stretch_along((0, 1, 0), (0, 0.1, 0), 1.15, tail_m),
            *[morphs.sweep((sx, 0, 0), (sx * WING_ROOT[0], 0, 0), (0, 0.22, 0), wings[t]) for t, sx in (("L", 1), ("R", -1))],
        ],
    }
    for name in ("Charge", "Mass", "Space", "Time"):
        kb, mx, frac, moved = morphs.add_shape(mesh, name, shapes[name])
        ctx.log("shape %s: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))

    # ------------------------------------------------------------------ DOCUMENTATION POSES (StarlingSwarm drives the members)
    # About the WORLD axes of the Blender frame: a +X wing rotated -Y lifts its tip (+Z); -Z rotation turns a rearward part
    # to the LEFT (+X), so the nose turns right.
    poses = {
        "flap": {"wing.L": (0, -35, 0), "wing.R": (0, 35, 0)},
        "turnright": {"tail": (0, 0, -22), "head": (0, 0, -10), "wing.L": (0, -12, 0), "wing.R": (0, -12, 0)},
        "turnleft": {"tail": (0, 0, 22), "head": (0, 0, 10), "wing.L": (0, 12, 0), "wing.R": (0, 12, 0)},
    }
    names = ("flap", "turnright", "turnleft")
    for p in names:
        takes.pose_take(arm, p, poses[p])
    takes.clear_pose(arm)

    lo, hi = f.bounds(mesh)
    ctx.log("bounds (blender) %s .. %s" % (tuple(round(v, 3) for v in lo), tuple(round(v, 3) for v in hi)))
    return {
        "armature": arm, "mesh": mesh,
        "spec": {
            "preset": "zup", "armature": ARMATURE, "mesh_node": MESH_NODE,
            "bones": bone_names,
            "takes": [ARMATURE + "|" + p for p in names],
            "pose_takes": [ARMATURE + "|" + p for p in names],
            "sheet_poses": names,
            "pose_expect": {
                "flap": [("wing.L", "z", +1, 0.15), ("wing.R", "z", +1, 0.15)],
                "turnright": [("tail", "x", +1, 0.05)],
                "turnleft": [("tail", "x", -1, 0.05)],
            },
            "shapes": ["Charge", "Mass", "Space", "Time"],
            "slots": list(SLOTS),
            "size_ref_blender": ((-2.36, -1.98, -0.36), (2.36, 1.30, 0.42)), "size_tol": (0.12, 0.08, 0.15),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.04,
        },
    }
