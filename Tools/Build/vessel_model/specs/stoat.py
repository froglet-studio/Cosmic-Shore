"""
stoat - a NEW hull (no artist file): replaces the Stoat's procedural placeholder (StoatHullBuilder /
StoatHullForm: five diamond plates, a wedge head, a four-plate tail with a crystal tip, four strut
legs, 2 slots, flat-shaded) with a model built to the fleet convention and the fleet's look
(Garrett, 2026-10-18: "don't forget they are spaceships inspired by earth clades").

What it must read as (R_VesselActions/STOAT.md section 2.1/2.2): a long, low, sinuous interceptor
inspired by a stoat / ermine. From the chase camera (StoatCameraSettingsSO followOffset (0, 6.5, -21),
68 degree FOV; the root is scaled 0.8, so the 8-unit hull is 6.4 world units and ~65 px per world unit
at 1080p: the hull is ~45 px wide and its back is what the pilot sees) the read is the SEGMENTED SPINE
of armour plates with their domain panels, the dorsal fin row, the twin haunch engines glowing, and the
tail boom ending in its domain-coloured tip. Clade -> ship:
  body plates      -> five overlapping armour segments over a dark spine core (the lope arches them)
  dorsal fur ridge -> three swept emitter fins on plates 0 / 2 / 4 (Charge: they grow into blades)
  head             -> a pointed prow; the eyes are a split sensor visor (Window) either side of the ridge
  ears             -> two small swept sensor fins with domain tips
  legs             -> four landing struts ending in manoeuvring-thruster pods (Engine nozzles)
  haunches         -> twin engine nacelles at the hips, mouths where the prefab's two VesselJets sit
  tail             -> a four-segment dark boom of shingled collars
  black tail tip   -> a faceted DOMAIN-coloured beacon spike behind a dark collar (the "dark-tipped" tail,
                      in the domain colour so a pilot reads the vessel's domain from behind)

Contract (FLEET_CONVENTION sections 1-4 for a NEW vessel):
  file      Assets/_Models/Vessel Models/Stoat.fbx (new guid), preset "zup"
  nodes     mesh `mesh`, armature `rig`; root bone `hull` (the core plate, plate 2)
  bones     every bone a CHILD OF `hull`, head at the placeholder's part pivot, so StoatAnimation's lope
            (which poses every part from its rest in the hull's frame) moves them exactly as it moved
            the placeholder's parts. Unity (x, y, z) = Blender (-x, -z, y):
              plate.0 plate.1 hull plate.3 plate.4   body plates, nose plate first, at y -1.6 .. +1.6
              nose                                   the prow, at the neck (0, -2.0, 0)
              tail tail.2 tail.3 tail.4 tail.5      the boom segments' fronts (0, 2.0 + 0.525 j, 0.065), the tip
              leg.1.L/R  leg.2.L/R                   the struts' hips, front pair under plate 1, back under plate 3
  size      the placeholder's within ~6%: x +-0.45, y -3.17 .. 4.79 (nose to tip, 7.96), z -0.66 .. 0.82
            (the 1.2 x 1.4 x 7 hit box, the jets at the hips and the camera were tuned to it)
  slots     0 Domain (dorsal panels, trim lines, fins, ear tips, tail line, tail tip), 1 Body, 2 Window
            (the visor), 3 Engine (nacelle mouths and vents, strut nozzles)
  shapes    the placeholder's meanings at the convention's 4-12% travel:
            Charge = the dorsal emitter fins grow into tall blades, the tail beacon swells, the visor bulges;
            Mass   = the plates, prow, nacelles and boom fatten about the spine;
            Space  = the body draws out long, the boom further, the prow reaches;
            Time   = the struts lengthen and rake back, the sensor fins prick up and sweep, the prow sharpens
  takes     none drive it (StoatAnimation poses the bones in code); four documentation poses for the sheet:
            arch (top of the bound), land (bunched, struts reaching, tail up), turnright, turnleft
"""

import math
from _common import box_uv, finish

NAME = "Stoat"
OUTPUT = "Assets/_Models/Vessel Models/Stoat.fbx"

ARMATURE = "rig"
MESH_NODE = "mesh"
SLOTS = ("Domain", "Body", "Window", "Engine")
D, H, W, J = 0, 1, 2, 3

# ---- the placeholder's layout (StoatHullForm.Defaults), in the Blender frame -----------------
SEGMENTS = 5
BODY_LENGTH = 4.0
SEG = BODY_LENGTH / SEGMENTS
CHEST_R, HIP_R = 0.45, 0.26
PLATE_Y = [-(BODY_LENGTH * 0.5 - (i + 0.5) * SEG) for i in range(SEGMENTS)]      # -1.6 .. 1.6
PLATE_R = [CHEST_R + (HIP_R - CHEST_R) * i / (SEGMENTS - 1) for i in range(SEGMENTS)]
PLATE_BONE = ["plate.0", "plate.1", "hull", "plate.3", "plate.4"]
NECK_Y = -BODY_LENGTH * 0.5                                                       # -2.0
TAIL_Z = HIP_R * 0.25                                                             # 0.065
TAIL_SEG = 2.1 / 4
TAIL_Y = [BODY_LENGTH * 0.5 + j * TAIL_SEG for j in range(4)]                     # 2.0 .. 3.575
TIP_Y = BODY_LENGTH * 0.5 + 2.1                                                   # 4.1
TAIL_R0, TAIL_R1 = 0.16, 0.08
LEG_PLATES = {"leg.1": 1, "leg.2": 3}
JET_X, JET_Y, JET_Z = 0.30, 2.15, -0.05     # author_stoat_assets.py JETS_KEPT / JET_LOCAL_*: Unity (-+0.3, -0.05, -2.15)


def leg_hip(role, sx):
    i = LEG_PLATES[role]
    r = PLATE_R[i]
    return (sx * r * 0.55, PLATE_Y[i], -r * 0.55)


def armour(w, ht, hb, n=24, ridge=0.16, chine=0.0):
    """A plate's cross-section: a low superellipse, flatter below, with a raised spine ridge on top."""
    pts = []
    for i in range(n):
        t = 2 * math.pi * i / n
        c, s = math.cos(t), math.sin(t)
        if s >= 0:
            x = w * math.copysign(abs(c) ** (2 / 2.6), c)
            y = ht * abs(s) ** (2 / 2.6)
            y += ridge * ht * max(0.0, 1.0 - abs(x) / (0.38 * w))
        else:
            x = w * math.copysign(abs(c) ** (2 / 2.1), c)
            y = -hb * abs(s) ** (2 / 2.1)
        x *= 1.0 + chine * max(0.0, 1.0 - abs(s) / 0.35)   # the chine: a hard lateral edge at the waterline
        pts.append((x, y))
    return pts


def build(ctx):
    import bpy  # noqa: F401
    import bmesh
    from mathutils import Vector, Matrix
    f, rig, morphs, takes = ctx.forms, ctx.rig, ctx.morphs, ctx.takes
    Station = f.Station
    parts = []

    def segment(name, y0, y1, w, ht, hb, taper=0.9, z=0.0, n=24, ridge=0.16, lip=0.84, bevel_len=0.08, rings=None, chine=0.0):
        """One armour segment from y0 (front) to y1 (back): bevelled lips at both ends, the back narrower.
        `rings` overrides the stations: (y, scale) pairs."""
        sts = []
        for (y, s) in (rings or ((y0, lip), (y0 + bevel_len, 1.0), (y1 - bevel_len * 1.4, taper), (y1, taper * lip))):
            sts.append(Station((0, y, z), [(px * s, py * s) for px, py in armour(w, ht, hb, n=n, ridge=ridge, chine=chine)]))
        obj = f.loft(name, sts)
        f.ensure_slots(obj, SLOTS)
        f.paint(obj, H)
        return obj

    def finish_smooth(obj, levels=1):
        f.subsurf(obj, levels)
        f.apply_modifiers(obj)
        return obj

    # ------------------------------------------------------------------ BODY PLATES (+ dorsal domain panels)
    for i in range(SEGMENTS):
        y, r = PLATE_Y[i], PLATE_R[i]
        y0, y1 = y - SEG * 0.56, y + SEG * 0.56            # 12% overlap: an arched spine never opens a slit
        ht, hb = r * 0.74, r * 0.62
        # a bevelled front lip, the panel band, a waist, then a raised armour collar at the back edge
        rings = ((y0, 0.80), (y0 + 0.07, 1.0), (y0 + 0.24, 0.985), (y1 - 0.30, 0.94), (y1 - 0.17, 0.92),
                 (y1 - 0.11, 0.985), (y1 - 0.04, 0.985), (y1, 0.84))
        plate = segment(PLATE_BONE[i], y0, y1, r / 1.1, ht, hb, rings=rings, n=28, chine=0.10)
        # a domain panel either side of the dark ridge, sunk into the plate (the spine's read from behind)
        f.inset_faces(plate, lambda c, n, k, y0=y0, y1=y1, r=r: n.z > 0.5 and 0.35 * r < abs(c.x) < 0.78 * r
                      and y0 + 0.20 < c.y < y1 - 0.27, 0.018, depth=0.014, slot=D)
        # a domain trim line low on each flank
        f.inset_faces(plate, lambda c, n, k, y0=y0, y1=y1: abs(n.x) > 0.9 and n.z < -0.05
                      and y0 + 0.20 < c.y < y1 - 0.27 and k == H, 0.012, depth=0.006, slot=D)
        # the collar's top: a domain trim line across the back of every plate
        f.paint(plate, D, lambda c, n, k, y1=y1: y1 - 0.10 < c.y < y1 - 0.05 and n.z > 0.35 and k == H)
        finish(f, plate, bevel_width=0.008, sharp_deg=40)
        parts.append(plate)

    # the dark spine core under the plates: what shows between them when the lope arches the spine
    core = f.loft("core", [Station((0, y, -0.02), [(px * s, py * s) for px, py in armour(1.0, 0.7, 0.6, n=16, ridge=0.0)])
                           for (y, s) in ((NECK_Y + 0.05, 0.30), (-1.0, 0.31), (1.0, 0.24), (BODY_LENGTH * 0.5 + 0.1, 0.17))])
    f.ensure_slots(core, SLOTS)
    f.paint(core, H)
    parts.append(core)

    # dorsal emitter fins on plates 0, 2, 4 (the placeholder's ridge on every other plate)
    for i in (0, 2, 4):
        y, r = PLATE_Y[i], PLATE_R[i]
        base = r * 0.74 * 1.10
        h = r * 0.9
        outline = [(y - 0.24, base - 0.05), (y + 0.24, base - 0.05), (y + 0.30, base + h * 0.35),
                   (y + 0.26, base + h), (y + 0.12, base + h * 0.92), (y - 0.08, base + h * 0.35)]
        fin = f.plate("fin.%d" % i, outline, 0.05, normal=(1, 0, 0), origin=(0, 0, 0), right=(0, 1, 0))
        f.ensure_slots(fin, SLOTS)
        f.paint(fin, D)
        f.paint(fin, H, lambda c, n, k, base=base: c.z < base + 0.06)
        f.bevel(fin, width=0.012, segments=1, angle_deg=30)
        f.apply_modifiers(fin)
        parts.append(fin)

    # ------------------------------------------------------------------ PROW (nose): a pointed hull, a split sensor visor
    st = [(NECK_Y + 0.12, 0.44, 0.33, 0.28, 0.0), (NECK_Y - 0.05, 0.43, 0.32, 0.27, 0.0), (NECK_Y - 0.35, 0.38, 0.28, 0.23, -0.01),
          (NECK_Y - 0.65, 0.29, 0.21, 0.17, -0.03), (NECK_Y - 0.90, 0.18, 0.13, 0.11, -0.05), (NECK_Y - 1.08, 0.07, 0.05, 0.05, -0.06),
          (NECK_Y - 1.17, 0.012, 0.01, 0.01, -0.065)]
    prow = f.loft("nose", [Station((0, y, dz), armour(w / 1.1, ht, hb, n=24, ridge=0.14, chine=0.10)) for (y, w, ht, hb, dz) in st])
    f.ensure_slots(prow, SLOTS)
    f.paint(prow, H)
    # the visor: the upper flanks between the neck and the snout, glass sunk into the hull (the stoat's eyes)
    f.inset_faces(prow, lambda c, n, k: n.z > 0.25 and abs(n.x) > 0.33 and NECK_Y - 0.80 < c.y < NECK_Y - 0.12,
                  0.02, depth=0.012, slot=W)
    finish_smooth(prow)
    parts.append(prow)

    # sensor fins (the ears): small swept blades on the prow's shoulders, domain-tipped
    for tag, sx in (("L", 1), ("R", -1)):
        outline = [(-0.15, 0.0), (0.18, 0.0), (0.16, 0.15), (0.09, 0.38), (-0.03, 0.33)]
        ear = f.plate("ear." + tag, outline, 0.035, normal=(1, 0, 0), right=(0, 1, 0))
        f.ensure_slots(ear, SLOTS)
        f.paint(ear, H)
        f.paint(ear, D, lambda c, n, k: c.z > 0.23)
        f.bevel(ear, width=0.008, segments=1, angle_deg=30)
        f.apply_modifiers(ear)
        cant = math.radians(14) * sx
        rot = Matrix(((math.cos(cant), 0, math.sin(cant), 0), (0, 1, 0, 0), (-math.sin(cant), 0, math.cos(cant), 0), (0, 0, 0, 1)))
        f.transform_mesh(ear, Matrix.Translation(Vector((sx * 0.19, NECK_Y - 0.12, 0.22))) @ rot)
        parts.append(ear)

    # ------------------------------------------------------------------ ENGINES at the haunches (plate 4)
    for tag, sx in (("L", 1), ("R", -1)):
        y0 = JET_Y - 0.92
        nac = f.lathe("nacelle." + tag, [(0.035, 0.0), (0.085, 0.10), (0.115, 0.28), (0.117, 0.46), (0.118, 0.50), (0.118, 0.62), (0.118, 0.72), (0.128, 0.86),
                                         (0.124, 0.90), (0.092, 0.90), (0.075, 0.86)],
                      segments=14, axis=(0, 1, 0), origin=(sx * JET_X, y0, JET_Z))
        f.ensure_slots(nac, SLOTS)
        f.paint(nac, H)
        f.paint(nac, J, lambda c, n, k: c.y > y0 + 0.875)                       # the mouth: lip and glowing core
        f.paint(nac, D, lambda c, n, k: y0 + 0.52 < c.y < y0 + 0.60 and n.y < 0.5)   # a domain band
        f.inset_faces(nac, lambda c, n, k: abs(n.x) > 0.4 and n.x * sx > 0 and y0 + 0.30 < c.y < y0 + 0.45 and k == H,
                      0.008, depth=0.01, slot=J)                                     # a side vent
        finish_smooth(nac)
        parts.append(nac)

    # ------------------------------------------------------------------ TAIL BOOM + the domain-tipped beacon
    for j in range(4):
        r0 = TAIL_R0 + (TAIL_R1 - TAIL_R0) * j / 4
        r1 = TAIL_R0 + (TAIL_R1 - TAIL_R0) * (j + 1) / 4
        y0, y1 = TAIL_Y[j] - 0.07, TAIL_Y[j] + TAIL_SEG + 0.04
        seg = segment("tail" if j == 0 else "tail.%d" % (j + 1), y0, y1, r0 * 1.15, r0 * 0.95, r0 * 0.85,
                      taper=r1 / r0, z=TAIL_Z, n=16, ridge=0.12, bevel_len=0.05)
        finish(f, seg, bevel_width=0.006, sharp_deg=40)
        parts.append(seg)
    collar = f.lathe("collar", [(0.07, 0.0), (0.105, 0.03), (0.105, 0.10), (0.08, 0.14)], segments=12, axis=(0, 1, 0),
                     origin=(0, TIP_Y - 0.06, TAIL_Z))
    f.ensure_slots(collar, SLOTS)
    f.paint(collar, H)
    parts.append(collar)
    tip = f.lathe("tail.5", [(0.06, 0.0), (0.17, 0.16), (0.20, 0.30), (0.11, 0.52), (0.0, 0.70)], segments=6,
                  axis=(0, 1, 0), origin=(0, TIP_Y + 0.02, TAIL_Z))
    f.ensure_slots(tip, SLOTS)
    f.paint(tip, D)
    parts.append(tip)

    # ------------------------------------------------------------------ STRUTS (the legs) with thruster pods
    for role in ("leg.1", "leg.2"):
        for tag, sx in (("L", 1), ("R", -1)):
            hx, hy, hz = leg_hip(role, sx)
            r = PLATE_R[LEG_PLATES[role]]
            foot_z = hz - (r * 0.45 + 0.26) + 0.06
            strut = f.tube_along("%s.%s" % (role, tag), [(hx * 0.6, hy, hz + 0.08), (hx, hy, hz - 0.05), (hx * 1.15, hy + 0.04, foot_z)],
                                 [0.07, 0.06, 0.045], segments=6)
            f.ensure_slots(strut, SLOTS)
            f.paint(strut, H)
            parts.append(strut)
            pod = f.lathe("pod.%s.%s" % (role, tag), [(0.02, 0.0), (0.05, 0.05), (0.055, 0.16), (0.045, 0.21), (0.03, 0.21)],
                          segments=10, axis=(0, 1, 0), origin=(hx * 1.15, hy - 0.08, foot_z - 0.01))
            f.ensure_slots(pod, SLOTS)
            f.paint(pod, H)
            f.paint(pod, J, lambda c, n, k, y=hy: n.y > 0.6 and c.y > y + 0.1)
            parts.append(pod)

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
    ctx.log("stoat joined: %d verts, %d tris (n-gons triangulated: %d)" % (len(mesh.data.vertices), f.tri_count(mesh), len(big)))

    # masks BEFORE skinning (skinning folds the sub-part groups into their bones)
    ms = morphs.MaskSet(mesh)
    me = mesh.data
    plates = ms.mask(*PLATE_BONE)
    fins = ms.mask("fin.0", "fin.2", "fin.4")
    nose = ms.mask("nose")
    ears = {"L": ms.mask("ear.L"), "R": ms.mask("ear.R")}
    nacelles = ms.mask("nacelle.L", "nacelle.R")
    core_m = ms.mask("core")
    tail_m = ms.mask("tail", "tail.2", "tail.3", "tail.4", "collar", "tail.5")
    tip_m = ms.mask("tail.5")
    legs = {(role, tag): ms.mask("%s.%s" % (role, tag), "pod.%s.%s" % (role, tag)) for role in LEG_PLATES for tag in ("L", "R")}
    leg_all = ms.mask(*["%s.%s" % (r_, t_) for r_ in LEG_PLATES for t_ in ("L", "R")] +
                      ["pod.%s.%s" % (r_, t_) for r_ in LEG_PLATES for t_ in ("L", "R")])
    body = [max(a, b, c, d, e) for a, b, c, d, e in zip(plates, fins, nacelles, core_m, leg_all)]

    # ------------------------------------------------------------------ RIG (every bone a child of hull)
    B = rig.BoneSpec
    bones = [B("hull", (0, 0, 0), (0, -0.4, 0))]
    for i in (0, 1, 3, 4):
        bones.append(B(PLATE_BONE[i], (0, PLATE_Y[i], 0), (0, PLATE_Y[i] - 0.3, 0), parent="hull"))
    bones.append(B("nose", (0, NECK_Y, 0), (0, NECK_Y - 0.6, 0), parent="hull"))
    for j in range(4):
        bones.append(B("tail" if j == 0 else "tail.%d" % (j + 1), (0, TAIL_Y[j], TAIL_Z), (0, TAIL_Y[j] + 0.4, TAIL_Z), parent="hull"))
    bones.append(B("tail.5", (0, TIP_Y, TAIL_Z), (0, TIP_Y + 0.4, TAIL_Z), parent="hull"))
    for role in ("leg.1", "leg.2"):
        for tag, sx in (("L", 1), ("R", -1)):
            hip = leg_hip(role, sx)
            bones.append(B("%s.%s" % (role, tag), hip, (hip[0], hip[1], hip[2] - 0.35), parent="hull"))
    arm = rig.build_armature(ARMATURE, bones)
    g2b = {"fin.0": "plate.0", "fin.2": "hull", "fin.4": "plate.4", "core": "hull", "ear.L": "nose", "ear.R": "nose",
           "nacelle.L": "plate.4", "nacelle.R": "plate.4", "collar": "tail.4"}
    for role in LEG_PLATES:
        for tag in ("L", "R"):
            g2b["pod.%s.%s" % (role, tag)] = "%s.%s" % (role, tag)
    rig.skin_rigid(mesh, arm, group_to_bone=g2b, default_bone="hull")
    bone_names = [b.name for b in arm.data.bones]

    # ------------------------------------------------------------------ MORPHS (4-12% of the ~8.2 diagonal)
    fin_base = 0.24
    shapes = {
        "Charge": [  # the weapon grows: the emitter fins become tall blades, the beacon swells, the visor bulges
            morphs.stretch_along((0, 0, 1), (0, 0, fin_base), 2.1, fins),
            morphs.sweep((0, 0, 1), (0, 0, fin_base), (0, 0.35, 0), fins),
            morphs.scale_about(1.7, (0, TIP_Y + 0.32, TAIL_Z), tip_m),
            morphs.inflate(0.03, [w * (1.0 if v.co.z > 0.05 and v.co.y < NECK_Y - 0.1 else 0.0) for w, v in zip(nose, me.vertices)]),
        ],
        "Mass": [  # volume: the plates, prow, nacelles and core fatten about the spine; the boom thickens less
            morphs.scale_about(1.48, (0, 0, 0), body, axes=(1, 0, 1)),
            morphs.scale_about(1.30, (0, 0, 0), nose, axes=(1, 0, 1)),
            morphs.scale_about(1.25, (0, 0, TAIL_Z), tail_m, axes=(1, 0, 1)),
        ],
        "Space": [  # reach: the body draws out, the boom further, the prow reaches
            morphs.stretch_along((0, 1, 0), (0, 0, 0), 1.12, body),
            morphs.translate((0, -0.24, 0), nose),
            morphs.stretch_along((0, -1, 0), (0, NECK_Y, 0), 1.12, nose),
            morphs.translate((0, 0.24, 0), tail_m),
            morphs.stretch_along((0, 1, 0), (0, BODY_LENGTH * 0.5, 0), 1.22, tail_m),
        ],
        "Time": [  # rate: the struts lengthen and rake back, the sensor fins prick up and sweep, the prow sharpens
            *[morphs.stretch_along((0, 0, -1), (0, 0, leg_hip(role, 1)[2]), 1.8, legs[(role, tag)]) for role in LEG_PLATES for tag in ("L", "R")],
            *[morphs.sweep((0, 0, -1), (0, 0, leg_hip(role, 1)[2]), (0, 0.55, 0), legs[(role, tag)]) for role in LEG_PLATES for tag in ("L", "R")],
            *[morphs.stretch_along((0, 0, 1), (0, 0, 0.22), 1.7, ears[t]) for t in ("L", "R")],
            *[morphs.sweep((0, 0, 1), (0, 0, 0.22), (0, 0.6, 0), ears[t]) for t in ("L", "R")],
            morphs.stretch_along((0, -1, 0), (0, NECK_Y - 0.5, 0), 1.35, nose, ramp=(0.0, 0.3)),
        ],
    }
    for name in ("Charge", "Mass", "Space", "Time"):
        kb, mx, frac, moved = morphs.add_shape(mesh, name, shapes[name])
        ctx.log("shape %s: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))

    # ------------------------------------------------------------------ DOCUMENTATION POSES (StoatAnimation drives the bones)
    # StoatAnimation's lope at its extremes (spine +-0.45, the user's arch) and a full-stick turn, written about WORLD
    # axes of the Blender frame: +X rotation = nose DOWN, -Z rotation = nose to the pilot's RIGHT (-X).
    def lope(spine, legs01):
        out = {}
        for i in (0, 1, 3, 4):
            t = i / (SEGMENTS - 1)
            lift = 0.35 * spine * (math.sin(math.pi * t) - 1.0)
            tilt = math.degrees(math.atan(0.35 * spine * math.pi / BODY_LENGTH * math.cos(math.pi * t)))
            out[PLATE_BONE[i]] = {"rot": (tilt, 0, 0), "loc": (0, 0, lift)}
        out["nose"] = {"rot": (14 * spine, 0, 0), "loc": (0, 0, -0.35 * spine)}
        for j, b in enumerate(("tail", "tail.2", "tail.3", "tail.4", "tail.5")):
            out[b] = {"rot": (0, 0, 0), "loc": (0, 0, 0.45 * (j + 1) / 5 * legs01)}
        for role, sign in (("leg.1", -1), ("leg.2", 1)):
            for tag in ("L", "R"):
                out["%s.%s" % (role, tag)] = (sign * 52 * legs01, 0, 0)
        return out

    def turn(yaw):
        out = {}
        for i in (0, 1, 3, 4):
            t = i / (SEGMENTS - 1)
            out[PLATE_BONE[i]] = (0, 0, -yaw * 12 * (1 - 2 * t))
        out["nose"] = (0, 0, -yaw * 18)
        for j, b in enumerate(("tail", "tail.2", "tail.3", "tail.4", "tail.5")):
            out[b] = (0, 0, yaw * 28 * (j + 1) / 5)
        return out

    poses = {"arch": lope(0.45, 0.0), "land": lope(-0.45, 1.0), "turnright": turn(1.0), "turnleft": turn(-1.0)}
    names = ("arch", "land", "turnright", "turnleft")
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
                "arch": [("plate.0", "z", -1, 0.1), ("plate.4", "z", -1, 0.1), ("nose", "z", -1, 0.12)],
                "land": [("tail.5", "z", +1, 0.3), ("leg.1.L", "y", -1, 0.05), ("leg.2.R", "y", +1, 0.05), ("plate.0", "z", +1, 0.1)],
                "turnright": [("nose", "x", -1, 0.1), ("tail.5", "x", -1, 0.15)],
                "turnleft": [("nose", "x", +1, 0.1), ("tail.5", "x", +1, 0.15)],
            },
            "shapes": ["Charge", "Mass", "Space", "Time"],
            "slots": list(SLOTS),
            # the placeholder's envelope (Blender frame), which the hit box, jets and camera were tuned to
            "size_ref_blender": ((-0.45, -3.17, -0.66), (0.45, 4.79, 0.82)), "size_tol": (0.08, 0.05, 0.10),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.04,
        },
    }
