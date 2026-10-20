"""
krill - a NEW body (no artist file) for one MEMBER of the Krill swarm (VesselClassType.Krill = 21). The Krill is a
swarm, not a hull: every member is drawn with this one model, so it is small, cheap (one skinned mesh, five bones) and
reads at swarm distance. Design: Docs/Studios/SWARM_CARD_VESSEL.md (the cards), Docs/Studios/KRILL_LAB.md (the lab),
Assets/_Scripts/Controller/Vessel/R_VesselActions/KRILL.md (the Unity vessel).

What it must read as: a krill, as a spaceship (Garrett, 2026-10-18: "don't forget they are spaceships inspired by earth
clades"). The view is the swarm view (KrillCameraSettingsSO followOffset (0, 0, -1200), about 1.3 world units a pixel at
1080p), and the prefab draws a member at KrillConfigSO.memberScale (4), so a member is about 16 units long and ten-odd
pixels on screen: the silhouette (a curled shrimp with long antennae and a tail fan), the domain stripe and the glowing
belly lamps carry the read. Clade -> ship:
  carapace (cephalothorax) -> an armoured forward hull, a domain stripe down its spine
  stalked compound eyes    -> two sensor-glass domes (Window) on short collars at the bow
  long antennae            -> two long sensor masts sweeping back and out from the bow
  abdomen (six segments)   -> a tapering tail boom of ring plates, each rim domain-trimmed, curling slightly down
  telson + uropods         -> a five-blade tail fan stabiliser (domain), the bone the tail-flip swings
  pleopods (swimmerets)    -> paired thruster vanes under the boom
  photophores (real krill glow: they carry light organs along the belly) -> glowing lamps (Engine) under the hull and
                              the boom, plus a small engine mouth at the fan's root

Contract (FLEET_CONVENTION sections 1-4 for a NEW vessel):
  file      Assets/_Models/Vessel Models/Krill.fbx (new guid), preset "zup"
  nodes     mesh `mesh`, armature `rig`; root bone `hull`
  bones     hull; tail (the boom's root) > fan (the tail fan's root); antenna.L / antenna.R (the masts' roots);
            legs (the thruster vanes, a child of tail)
  size      4 long, 1.5 wide over the fan, 1.0 tall (the prefab scales it by memberScale)
  slots     0 Domain, 1 Body, 2 Window (the eyes), 3 Engine (the belly lamps, the engine mouth)
  shapes    the four element jobs (SWARM_CARD_VESSEL.md), at the convention's 4-12% travel:
            Charge = current: the antennae lengthen into long prongs and the lamps swell (lasers, arcs, stings);
            Mass   = volume: the carapace and the boom fatten (prisms, shells, the heavy forms);
            Space  = reach: the tail fan and the thruster vanes span wide (more prisms in reach);
            Time   = speed: the body stretches sleek and the antennae sweep back along it (speed, agility)
  takes     none drive it (KrillSwarm swings the tail and fan in code); three documentation poses for the sheet:
            flip (the escape tail-flip: the boom and fan curl under), turnright, turnleft
"""

import math
from _common import box_uv, finish

NAME = "Krill"
OUTPUT = "Assets/_Models/Vessel Models/Krill.fbx"

ARMATURE = "rig"
MESH_NODE = "mesh"
SLOTS = ("Domain", "Body", "Window", "Engine")
D, H, W, J = 0, 1, 2, 3

TAIL_ROOT = (0.0, 0.15, 0.02)
FAN_ROOT = (0.0, 1.78, -0.18)
ANT_ROOT = (0.16, -1.62, 0.14)
LEGS_ROOT = (0.0, 0.85, -0.22)


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

    # ------------------------------------------------------------------ THE CARAPACE (hull)
    body = loft("hull", [
        (-1.80, 0.06, 0.05, 0.04, 0.10), (-1.62, 0.20, 0.17, 0.10, 0.10), (-1.35, 0.30, 0.27, 0.17, 0.08),
        (-0.95, 0.36, 0.33, 0.22, 0.05), (-0.45, 0.38, 0.35, 0.25, 0.03), (0.00, 0.35, 0.32, 0.24, 0.02),
        (0.22, 0.31, 0.28, 0.21, 0.02)], n=22)
    # the domain stripe down the spine and a sunken domain panel on each flank
    f.paint(body, D, lambda c, n, k: n.z > 0.86 and -1.40 < c.y < 0.20)
    f.inset_faces(body, lambda c, n, k: k == H and 0.15 < n.z < 0.55 and abs(n.x) > 0.6 and -1.05 < c.y < -0.15,
                  0.02, depth=0.01, slot=D)
    finish(f, body, bevel_width=0.0, sharp_deg=40)
    f.subsurf(body, 1)
    f.apply_modifiers(body)
    parts.append(body)

    # ------------------------------------------------------------------ THE EYES: sensor-glass domes on collars
    for tag, sx in (("L", 1), ("R", -1)):
        col = f.lathe("eyecollar." + tag, [(0.07, 0.0), (0.09, 0.04), (0.09, 0.10), (0.07, 0.13)], segments=10,
                      axis=(sx * 0.7, -0.5, 0.5), origin=(sx * 0.16, -1.42, 0.16))
        f.ensure_slots(col, SLOTS)
        f.paint(col, D)
        parts.append(col)
        eye = f.sphere("eye." + tag, 0.11, center=(sx * 0.27, -1.50, 0.25), segments=12, rings=8)
        f.ensure_slots(eye, SLOTS)
        f.paint(eye, W)
        parts.append(eye)

    # ------------------------------------------------------------------ THE ANTENNAE: long sensor masts
    ant_pts = {}
    for tag, sx in (("L", 1), ("R", -1)):
        pts = [(sx * ANT_ROOT[0], ANT_ROOT[1], ANT_ROOT[2]), (sx * 0.30, -1.95, 0.32), (sx * 0.55, -2.05, 0.42),
               (sx * 0.90, -1.85, 0.40), (sx * 1.20, -1.30, 0.30), (sx * 1.38, -0.55, 0.18)]
        ant_pts[tag] = pts
        mast = f.tube_along("antenna." + tag, pts, [0.045, 0.038, 0.032, 0.026, 0.020, 0.012], segments=6)
        f.ensure_slots(mast, SLOTS)
        f.paint(mast, H)
        f.paint(mast, D, lambda c, n, k, sx=sx: abs(c.x) > 1.25)   # the mast's lit tip
        parts.append(mast)

    # ------------------------------------------------------------------ THE TAIL BOOM: five ring plates
    rings = [(0.15, 0.30, 0.26, 0.20, 0.02), (0.50, 0.27, 0.23, 0.18, -0.01), (0.82, 0.23, 0.20, 0.16, -0.05),
             (1.12, 0.19, 0.17, 0.13, -0.09), (1.40, 0.15, 0.13, 0.10, -0.13), (1.68, 0.10, 0.09, 0.07, -0.17)]
    for k in range(len(rings) - 1):
        y0, w0, ht0, hb0, z0 = rings[k]
        y1, w1, ht1, hb1, z1 = rings[k + 1]
        seg = loft("tail.%d" % k, [(y0, w0, ht0, hb0, z0), (y0 + 0.04, w0 * 1.04, ht0 * 1.05, hb0 * 1.03, z0),
                                   (y1 - 0.02, w1 * 1.02, ht1 * 1.02, hb1, z1)], n=18)
        f.paint(seg, D, lambda c, n, i, y0=y0: c.y < y0 + 0.06 and n.z > -0.2)   # the ring's rim
        finish(f, seg, bevel_width=0.0, sharp_deg=40)
        parts.append(seg)

    # the engine mouth at the fan's root
    mouth = f.lathe("engine", [(0.0, 0.0), (0.07, 0.02), (0.08, 0.10), (0.06, 0.13), (0.0, 0.13)], segments=10,
                    axis=(0, 1, -0.25), origin=(0.0, 1.66, -0.16))
    f.ensure_slots(mouth, SLOTS)
    f.paint(mouth, H)
    f.paint(mouth, J, lambda c, n, k: c.y > 1.76)
    parts.append(mouth)

    # ------------------------------------------------------------------ THE TAIL FAN: five domain blades
    fan_names = []
    for k, ang in enumerate((-48, -24, 0, 24, 48)):
        a = math.radians(ang)
        length = 0.62 if ang == 0 else 0.56
        width = 0.13 if ang == 0 else 0.15
        outline = [(-width * 0.5, 0.0), (width * 0.5, 0.0), (width * 0.65, length * 0.7), (0.0, length), (-width * 0.65, length * 0.7)]
        # in the fan's plane (horizontal), rotated by ang about Z, starting at the fan's root
        pts = []
        for (u, v) in outline:
            x = u * math.cos(a) + v * math.sin(a)
            y = -u * math.sin(a) + v * math.cos(a)
            pts.append((FAN_ROOT[0] + x, FAN_ROOT[1] + y, FAN_ROOT[2] - 0.08 * v))
        bm = bmesh.new()
        top = [bm.verts.new((x, y, z + 0.02)) for (x, y, z) in pts]
        bot = [bm.verts.new((x, y, z - 0.02)) for (x, y, z) in pts]
        bm.faces.new(top)
        bm.faces.new(list(reversed(bot)))
        n = len(pts)
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new([top[i], bot[i], bot[j], top[j]])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        name = "fan.%d" % k
        blade = f.new_mesh_object(name, bm, smooth=False)
        f.ensure_slots(blade, SLOTS)
        f.paint(blade, D)
        f.paint(blade, H, lambda c, n, i: c.y < FAN_ROOT[1] + 0.12)
        f.bevel(blade, width=0.008, segments=1, angle_deg=30)
        f.apply_modifiers(blade)
        parts.append(blade)
        fan_names.append(name)

    # ------------------------------------------------------------------ THE THRUSTER VANES (pleopods) under the boom
    leg_names = []
    for k, y in enumerate((0.38, 0.68, 0.98, 1.26)):
        for tag, sx in (("L", 1), ("R", -1)):
            w = 0.16 - 0.02 * k
            z = -0.18 - 0.03 * k
            outline = [(0.0, 0.0), (0.06, 0.0), (0.05, -0.30), (0.0, -0.34), (-0.02, -0.28)]
            vane = f.plate("leg.%d.%s" % (k, tag), [(y + u, z + v) for (u, v) in outline], 0.03,
                           normal=(1, 0, 0), origin=(sx * w, 0, 0), right=(0, 1, 0))
            f.ensure_slots(vane, SLOTS)
            f.paint(vane, H)
            f.paint(vane, D, lambda c, n, i, z=z: c.z < z - 0.24)
            parts.append(vane)
            leg_names.append("leg.%d.%s" % (k, tag))

    # ------------------------------------------------------------------ THE LAMPS (photophores) under hull and boom
    lamp_names = []
    for k, (y, z) in enumerate(((-1.05, -0.22), (-0.35, -0.25), (0.35, -0.20), (0.95, -0.15), (1.45, -0.20))):
        lamp = f.sphere("lamp.%d" % k, 0.065, center=(0.0, y, z), segments=10, rings=6, scale=(1.2, 1.0, 0.8))
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
    ctx.log("krill joined: %d verts, %d tris (n-gons triangulated: %d)" % (len(mesh.data.vertices), f.tri_count(mesh), len(big)))

    # masks BEFORE skinning (skinning folds the sub-part groups into their bones)
    ms = morphs.MaskSet(mesh)
    tail_names = ["tail.%d" % k for k in range(len(rings) - 1)]
    hull_m = ms.mask("hull", "eyecollar.L", "eyecollar.R", "eye.L", "eye.R")
    boom_m = ms.mask(*tail_names, "engine")
    fan_m = ms.mask(*fan_names)
    legs_m = ms.mask(*leg_names)
    ants = {"L": ms.mask("antenna.L"), "R": ms.mask("antenna.R")}

    # ------------------------------------------------------------------ RIG
    B = rig.BoneSpec
    bones = [B("hull", (0, 0, 0), (0, -0.5, 0)),
             B("tail", TAIL_ROOT, (0, TAIL_ROOT[1] + 0.5, TAIL_ROOT[2] - 0.04), parent="hull"),
             B("fan", FAN_ROOT, (0, FAN_ROOT[1] + 0.4, FAN_ROOT[2] - 0.03), parent="tail"),
             B("legs", LEGS_ROOT, (0, LEGS_ROOT[1], LEGS_ROOT[2] - 0.3), parent="tail"),
             B("antenna.L", ANT_ROOT, (ANT_ROOT[0] + 0.2, ANT_ROOT[1] - 0.3, ANT_ROOT[2] + 0.1), parent="hull"),
             B("antenna.R", (-ANT_ROOT[0], ANT_ROOT[1], ANT_ROOT[2]),
               (-ANT_ROOT[0] - 0.2, ANT_ROOT[1] - 0.3, ANT_ROOT[2] + 0.1), parent="hull")]
    arm = rig.build_armature(ARMATURE, bones)
    g2b = {"eyecollar.L": "hull", "eyecollar.R": "hull", "eye.L": "hull", "eye.R": "hull", "engine": "fan"}
    for name in tail_names:
        g2b[name] = "tail"
    for name in fan_names:
        g2b[name] = "fan"
    for name in leg_names:
        g2b[name] = "legs"
    for name, (_, y, _) in lamp_names:
        g2b[name] = "hull" if y < TAIL_ROOT[1] else "tail"
    rig.skin_rigid(mesh, arm, group_to_bone=g2b, default_bone="hull")
    bone_names = [b.name for b in arm.data.bones]

    # ------------------------------------------------------------------ MORPHS (4-12% of the ~4.6 diagonal)
    lamp_grow = [morphs.scale_about(1.9, c, ms.mask(name)) for (name, c) in lamp_names]
    shapes = {
        "Charge": [  # current: the antennae lengthen into long prongs, the lamps swell
            *[morphs.scale_about(1.32, (sx * ANT_ROOT[0], ANT_ROOT[1], ANT_ROOT[2]), ants[t]) for t, sx in (("L", 1), ("R", -1))],
            *lamp_grow,
        ],
        "Mass": [  # volume: the carapace and the boom fatten about the body's axis
            morphs.scale_about(1.6, (0, 0, 0), hull_m, axes=(1, 0, 1)),
            morphs.scale_about(1.55, (0, 0.9, -0.05), boom_m, axes=(1, 0, 1)),
        ],
        "Space": [  # reach: the tail fan and the thruster vanes span wide
            morphs.scale_about(1.7, FAN_ROOT, fan_m, axes=(1, 1, 0)),
            morphs.scale_about(1.6, (0, 0.85, -0.2), legs_m, axes=(1, 0, 1)),
        ],
        "Time": [  # speed: the body stretches sleek, the antennae sweep back along it
            morphs.stretch_along((0, -1, 0), (0, 0.2, 0), 1.18, hull_m),
            morphs.stretch_along((0, 1, 0), TAIL_ROOT, 1.15, [max(a, b, c) for a, b, c in zip(boom_m, fan_m, legs_m)]),
            *[morphs.sweep((sx, 0, 0), (sx * ANT_ROOT[0], ANT_ROOT[1], 0), (0, 0.5, 0), ants[t]) for t, sx in (("L", 1), ("R", -1))],
        ],
    }
    for name in ("Charge", "Mass", "Space", "Time"):
        kb, mx, frac, moved = morphs.add_shape(mesh, name, shapes[name])
        ctx.log("shape %s: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))

    # ------------------------------------------------------------------ DOCUMENTATION POSES (KrillSwarm drives the bones)
    # About the WORLD axes of the Blender frame: a +Y-pointing (rearward) part rotated -X curls DOWN; -Z rotation turns
    # a rearward part to the LEFT (+X), so the nose turns right.
    poses = {
        "flip": {"tail": (-35, 0, 0), "fan": (-30, 0, 0), "legs": (-20, 0, 0)},
        "turnright": {"tail": (0, 0, -22), "fan": (0, 0, -14), "antenna.L": (0, 0, -10), "antenna.R": (0, 0, -10)},
        "turnleft": {"tail": (0, 0, 22), "fan": (0, 0, 14), "antenna.L": (0, 0, 10), "antenna.R": (0, 0, 10)},
    }
    names = ("flip", "turnright", "turnleft")
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
                "flip": [("fan", "z", -1, 0.15)],
                "turnright": [("fan", "x", +1, 0.15)],
                "turnleft": [("fan", "x", -1, 0.15)],
            },
            "shapes": ["Charge", "Mass", "Space", "Time"],
            "slots": list(SLOTS),
            "size_ref_blender": ((-1.45, -2.10, -0.60), (1.45, 2.45, 0.50)), "size_tol": (0.12, 0.08, 0.12),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.04,
        },
    }
