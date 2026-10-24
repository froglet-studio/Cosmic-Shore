"""
angler - a NEW hull (no artist file) for the Angler (VesselClassType.Angler = 17). Until 2026-10-18 the Angler wore the
Stoat's procedural hull and lope as a stand-in body (ANGLER.md follow-up 1, "The Angler's own hull"); this is its own,
built to the fleet convention and the fleet's look (Garrett, 2026-10-18: "don't forget they are spaceships inspired by
earth clades").

What it must read as (ANGLER.md: the strategist that LURES fauna with the light hanging ahead of its nose, tames them and
commands them as a school): a heavy, wide-mouthed deep-sea hunter. The chase camera sits far back
(AnglerCameraSettingsSO followOffset (0, 0, -160), 62 degree FOV, ~10 px per world unit at 1080p; the root is scaled
0.8), so the silhouette and the colour fields carry the read: the broad bow, the open jaw, the dorsal mast arching
forward with its glowing bulb, the stabiliser fins and the twin engines. Clade -> ship:
  big head / jaw   -> a heavy armoured bow over a hinged lower jaw: a ram-scoop intake, dark inside, glowing lime at
                      the throat, its rim lined with domain-coloured intake vanes (the teeth)
  illicium + esca  -> a dorsal sensor mast arching forward on a domain-collared hinge, ending in the lure bulb (Engine
                      slot, so it glows; it swells while the lure is held - AnglerAnimation) - the beacon the fauna follow
  small eyes       -> sensor-glass slits (Window) on the bow's shoulders
  pectoral fins    -> swept stabilisers with domain trim
  dorsal fin       -> a low comb of three domain blades behind the mast
  tail / caudal    -> a short tail boom ending in a twin-lobed caudal stabiliser (domain)
  (the ship's own) -> twin engine nacelles on the tail root, mouths where the prefab's two VesselJets sit
  skin             -> a dark armoured hull with sunken domain panel lines on the flanks

Contract (FLEET_CONVENTION sections 1-4 for a NEW vessel):
  file      Assets/_Models/Vessel Models/Angler.fbx (new guid), preset "zup"
  nodes     mesh `mesh`, armature `rig`; root bone `hull` (the body)
  bones     hull; jaw.B (the lower jaw's hinge); lure (the mast's hinge on the head) > esca (the bulb's centre);
            fin.L / fin.R (the stabilisers' roots); tail (the boom's root) > tail.2 (the caudal's root)
  size      7.9 long like the stand-in it replaces (the hit box, the jets and the camera were tuned to an 8-unit
            hull), but an anglerfish's bow: 1.9 wide over the bow, 2.6 across the stabilisers, 2.2 tall with the
            mast. Wider and taller than the Stoat stand-in on purpose (an open decision for Garrett: the hit box,
            StoatBody 1.2 x 1.4 x 7, was left as it is)
  slots     0 Domain, 1 Body, 2 Window (sensor slits), 3 Engine (the lure bulb, the throat, the nacelle mouths)
  shapes    the Angler's abilities (ANGLER.md section 0) at the convention's 4-12% travel:
            Charge = the bite: the intake vanes grow into fangs and the jaw juts (Erase / Lock-on);
            Mass   = the lure and the volume: the bulb swells and the hull fattens (Lure, Tame & Grow);
            Space  = reach: the mast reaches further forward and the stabilisers span out (Paint);
            Time   = rate: the fins and the caudal sweep back, the tail lengthens (Dash)
  takes     none drive it (AnglerAnimation poses the bones in code); four documentation poses for the sheet:
            lure (the jaw open, the mast bowed forward), turnright, turnleft, dive (the stabilisers up)
"""

import math
from _common import box_uv, finish

NAME = "Angler"
OUTPUT = "Assets/_Models/Vessel Models/Angler.fbx"

ARMATURE = "rig"
MESH_NODE = "mesh"
SLOTS = ("Domain", "Body", "Window", "Engine")
D, H, W, J = 0, 1, 2, 3

JAW_HINGE = (0.0, -1.95, -0.20)
MAST_BASE = (0.0, -1.70, 0.70)
ESCA = (0.0, -4.12, 0.92)
FIN_ROOT = (0.70, -0.75, -0.12)
TAIL_ROOT = (0.0, 2.25, 0.02)
CAUDAL_ROOT = (0.0, 2.95, 0.02)
JET_X, JET_Y, JET_Z = 0.30, 2.15, -0.05     # the prefab's two VesselJets under the hull: Unity (-+0.3, -0.05, -2.15)


def section(w, ht, hb, n=24, nt=2.4, nb=2.2):
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
    from mathutils import Vector
    f, rig, morphs, takes = ctx.forms, ctx.rig, ctx.morphs, ctx.takes
    Station = f.Station
    parts = []

    def loft(name, stations, n=24, nt=2.4, nb=2.2):
        obj = f.loft(name, [Station((0, y, dz), section(w, ht, hb, n=n, nt=nt, nb=nb)) for (y, w, ht, hb, dz) in stations])
        f.ensure_slots(obj, SLOTS)
        f.paint(obj, H)
        return obj

    # ------------------------------------------------------------------ THE BOW AND BODY (hull)
    body = loft("hull", [
        (-3.38, 0.42, 0.16, 0.06, 0.32), (-3.18, 0.66, 0.34, 0.08, 0.30), (-2.80, 0.86, 0.54, 0.14, 0.29),
        (-2.30, 0.95, 0.68, 0.22, 0.24), (-1.85, 0.96, 0.74, 0.62, 0.14), (-1.20, 0.90, 0.72, 0.68, 0.05),
        (-0.45, 0.80, 0.64, 0.62, 0.0), (0.40, 0.64, 0.52, 0.50, 0.0), (1.20, 0.47, 0.39, 0.38, 0.0),
        (1.85, 0.32, 0.27, 0.27, 0.01), (2.30, 0.18, 0.17, 0.17, 0.02), (2.42, 0.11, 0.11, 0.11, 0.02)], n=28)
    # sensor-glass slits on the bow's shoulders (the eyes)
    f.inset_faces(body, lambda c, n, k: n.z > 0.25 and abs(n.x) > 0.45 and -2.75 < c.y < -2.05 and c.z > 0.45,
                  0.02, depth=0.015, slot=W)
    # sunken domain panel lines along the upper flanks of the body
    f.inset_faces(body, lambda c, n, k: k == H and 0.32 < n.z < 0.62 and abs(n.x) > 0.5 and -1.6 < c.y < 0.9,
                  0.03, depth=0.015, slot=D)
    # the bow's lip (its underside, over the mouth) is a dark plate; a domain trim line across the top of the bow
    f.paint(body, D, lambda c, n, k: k == H and n.z > 0.9 and -2.6 < c.y < -2.1)
    finish(f, body, bevel_width=0.0, sharp_deg=40)
    f.subsurf(body, 1)
    f.apply_modifiers(body)
    parts.append(body)

    # the throat: a recessed ram-scoop core glowing at the back of the mouth
    throat = f.lathe("throat", [(0.62, 0.0), (0.50, 0.10), (0.36, 0.18), (0.0, 0.20)], segments=20, axis=(0, -1, 0),
                     origin=(0, -1.92, 0.02))
    f.ensure_slots(throat, SLOTS)
    f.paint(throat, H)
    f.paint(throat, J, lambda c, n, k: n.y < -0.6)
    parts.append(throat)

    # ------------------------------------------------------------------ THE LOWER JAW (jaw.B): an armoured scoop
    jaw = loft("jaw.B", [(-1.80, 0.80, 0.12, 0.40, -0.36), (-2.40, 0.84, 0.12, 0.40, -0.34), (-3.05, 0.74, 0.12, 0.33, -0.28),
                         (-3.48, 0.56, 0.10, 0.22, -0.20), (-3.62, 0.36, 0.08, 0.12, -0.16)], n=24, nt=3.0, nb=2.2)
    f.paint(jaw, D, lambda c, n, k: n.z > 0.85 and c.y < -3.25)          # the jaw's lip
    f.inset_faces(jaw, lambda c, n, k: k == H and n.z < -0.6 and -3.2 < c.y < -2.0, 0.03, depth=0.012, slot=D)   # belly plate
    f.subsurf(jaw, 1)
    f.apply_modifiers(jaw)
    parts.append(jaw)

    # intake vanes (the teeth): along the bow's lip pointing down, along the jaw's rim pointing up
    teeth = []
    for row, z0, axis, cy, ry, rx, length, count in (("u", 0.26, (0, 0, -1), -2.62, 0.58, 0.60, 0.24, 7),
                                                     ("l", -0.17, (0, 0, 1), -2.95, 0.55, 0.56, 0.26, 8)):
        for k in range(count):
            a = math.radians(-72 + 144 * k / (count - 1))
            x, y = rx * math.sin(a), cy - ry * math.cos(a)
            tooth = f.cone("tooth.%s.%d" % (row, k), 0.055, length, axis=axis, origin=(x, y, z0), segments=6)
            f.ensure_slots(tooth, SLOTS)
            f.paint(tooth, D)
            parts.append(tooth)
            teeth.append(("tooth.%s.%d" % (row, k), (x, y, z0), axis))

    # ------------------------------------------------------------------ THE MAST (lure) AND THE BULB (esca)
    mast_pts = [(0, MAST_BASE[1] + 0.05, MAST_BASE[2] - 0.08), (0, -2.15, 1.30), (0, -2.85, 1.58), (0, -3.55, 1.48),
                (0, -4.00, 1.22), (0, ESCA[1], ESCA[2] + 0.17)]
    mast = f.tube_along("lure", mast_pts, [0.085, 0.07, 0.06, 0.055, 0.05, 0.045], segments=8)
    f.ensure_slots(mast, SLOTS)
    f.paint(mast, H)
    parts.append(mast)
    for name, (cy, cz), r in (("collar.0", (MAST_BASE[1] + 0.02, MAST_BASE[2] - 0.02), 0.15), ("collar.1", (-2.85, 1.58), 0.095)):
        col = f.lathe(name, [(r * 0.7, -0.06), (r, -0.03), (r, 0.03), (r * 0.7, 0.06)], segments=12, axis=(0, 1, 0),
                      origin=(0, cy, cz))
        f.ensure_slots(col, SLOTS)
        f.paint(col, D)
        parts.append(col)
    bulb = f.sphere("esca", 0.21, center=ESCA, segments=16, rings=10, scale=(1.0, 1.0, 1.1))
    f.ensure_slots(bulb, SLOTS)
    f.paint(bulb, J)
    parts.append(bulb)
    cap = f.lathe("esca.cap", [(0.0, 0.0), (0.12, 0.02), (0.15, 0.08), (0.10, 0.12)], segments=12, axis=(0, 0, -1),
                  origin=(ESCA[0], ESCA[1], ESCA[2] + 0.30))
    f.ensure_slots(cap, SLOTS)
    f.paint(cap, H)
    parts.append(cap)

    # ------------------------------------------------------------------ DORSAL COMB, STABILISERS, TAIL, ENGINES
    for k, (y, h) in enumerate(((-0.95, 0.34), (-0.25, 0.28), (0.45, 0.21))):
        base = 0.62 - 0.08 * k
        outline = [(y - 0.24, base - 0.08), (y + 0.30, base - 0.08), (y + 0.34, base + h * 0.3), (y + 0.30, base + h),
                   (y + 0.14, base + h * 0.85), (y - 0.12, base + h * 0.25)]
        fin = f.plate("comb.%d" % k, outline, 0.05, normal=(1, 0, 0), right=(0, 1, 0))
        f.ensure_slots(fin, SLOTS)
        f.paint(fin, D)
        f.paint(fin, H, lambda c, n, i, base=base: c.z < base + 0.02)
        f.bevel(fin, width=0.01, segments=1, angle_deg=30)
        f.apply_modifiers(fin)
        parts.append(fin)

    for tag, sx in (("L", 1), ("R", -1)):
        rx, ry, rz = FIN_ROOT
        outline = [(-0.20, -0.25), (0.15, -0.30), (0.55, 0.05), (0.62, 0.42), (0.40, 0.55), (0.0, 0.30)]
        # in the fin's plane: u outward (+X for the left), v back (+Y); a slight anhedral
        pts = [(sx * (rx + u * 0.95 + 0.0), ry + v * 1.1, rz - 0.12 * u) for (v, u) in outline]
        bm = bmesh.new()
        top = [bm.verts.new((x, y, z + 0.03)) for (x, y, z) in pts]
        bot = [bm.verts.new((x, y, z - 0.03)) for (x, y, z) in pts]
        bm.faces.new(top)
        bm.faces.new(list(reversed(bot)))
        n = len(pts)
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new([top[i], bot[i], bot[j], top[j]])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        fin = f.new_mesh_object("fin." + tag, bm, smooth=False)
        f.ensure_slots(fin, SLOTS)
        f.paint(fin, H)
        f.paint(fin, D, lambda c, n, i, sx=sx: abs(c.x) > 1.05)               # the stabiliser's tip
        f.bevel(fin, width=0.01, segments=1, angle_deg=30)
        f.apply_modifiers(fin)
        parts.append(fin)

    boom = loft("tail", [(2.18, 0.20, 0.19, 0.19, 0.02), (2.55, 0.17, 0.16, 0.16, 0.02), (2.95, 0.12, 0.12, 0.12, 0.02),
                         (3.05, 0.09, 0.09, 0.09, 0.02)], n=16)
    f.paint(boom, D, lambda c, n, k: 2.45 < c.y < 2.62)
    parts.append(boom)
    for lobe, sz in (("up", 1), ("down", -1)):
        outline = [(2.90, 0.0), (3.10, 0.06 * sz), (3.62, 0.62 * sz), (3.82, 0.66 * sz), (3.62, 0.34 * sz), (3.30, 0.05 * sz)]
        if sz < 0:
            outline = list(reversed(outline))
        cf = f.plate("caudal.%s" % lobe, [(y, z + 0.02) for (y, z) in outline], 0.05, normal=(1, 0, 0), right=(0, 1, 0))
        f.ensure_slots(cf, SLOTS)
        f.paint(cf, D)
        f.paint(cf, H, lambda c, n, i: c.y < 3.2)
        f.bevel(cf, width=0.01, segments=1, angle_deg=30)
        f.apply_modifiers(cf)
        parts.append(cf)

    for tag, sx in (("L", 1), ("R", -1)):
        y0 = JET_Y - 0.95
        nac = f.lathe("nacelle." + tag, [(0.04, 0.0), (0.11, 0.12), (0.15, 0.32), (0.155, 0.50), (0.155, 0.60), (0.155, 0.72),
                                         (0.168, 0.88), (0.162, 0.93), (0.11, 0.93), (0.09, 0.88)],
                      segments=14, axis=(0, 1, 0), origin=(sx * JET_X, y0, JET_Z))
        f.ensure_slots(nac, SLOTS)
        f.paint(nac, H)
        f.paint(nac, J, lambda c, n, k: c.y > y0 + 0.89)
        f.paint(nac, D, lambda c, n, k: y0 + 0.62 < c.y < y0 + 0.70)
        f.subsurf(nac, 1)
        f.apply_modifiers(nac)
        parts.append(nac)

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
    ctx.log("angler joined: %d verts, %d tris (n-gons triangulated: %d)" % (len(mesh.data.vertices), f.tri_count(mesh), len(big)))

    # masks BEFORE skinning (skinning folds the sub-part groups into their bones)
    ms = morphs.MaskSet(mesh)
    hull_m = ms.mask("hull", "throat", "comb.0", "comb.1", "comb.2", "nacelle.L", "nacelle.R")
    jaw_m = ms.mask("jaw.B")
    bulb_m = ms.mask("esca", "esca.cap")
    mast_m = ms.mask("lure", "collar.0", "collar.1", "esca", "esca.cap")
    fins = {"L": ms.mask("fin.L"), "R": ms.mask("fin.R")}
    tail_m = ms.mask("tail", "caudal.up", "caudal.down")
    caudal_m = ms.mask("caudal.up", "caudal.down")

    # ------------------------------------------------------------------ RIG
    B = rig.BoneSpec
    bones = [B("hull", (0, 0, 0), (0, -0.5, 0)),
             B("jaw.B", JAW_HINGE, (0, JAW_HINGE[1] - 0.6, JAW_HINGE[2]), parent="hull"),
             B("lure", MAST_BASE, (0, -2.15, 1.30), parent="hull"),
             B("esca", ESCA, (ESCA[0], ESCA[1], ESCA[2] - 0.3), parent="lure"),
             B("fin.L", FIN_ROOT, (FIN_ROOT[0] + 0.4, FIN_ROOT[1] + 0.2, FIN_ROOT[2]), parent="hull"),
             B("fin.R", (-FIN_ROOT[0], FIN_ROOT[1], FIN_ROOT[2]), (-FIN_ROOT[0] - 0.4, FIN_ROOT[1] + 0.2, FIN_ROOT[2]), parent="hull"),
             B("tail", TAIL_ROOT, (0, TAIL_ROOT[1] + 0.5, TAIL_ROOT[2]), parent="hull"),
             B("tail.2", CAUDAL_ROOT, (0, CAUDAL_ROOT[1] + 0.6, CAUDAL_ROOT[2]), parent="tail")]
    arm = rig.build_armature(ARMATURE, bones)
    g2b = {"throat": "hull", "collar.0": "lure", "collar.1": "lure", "esca.cap": "esca",
           "comb.0": "hull", "comb.1": "hull", "comb.2": "hull", "nacelle.L": "hull", "nacelle.R": "hull",
           "caudal.up": "tail.2", "caudal.down": "tail.2"}
    for name, _, _ in teeth:
        g2b[name] = "jaw.B" if ".l." in name else "hull"
    rig.skin_rigid(mesh, arm, group_to_bone=g2b, default_bone="hull")
    bone_names = [b.name for b in arm.data.bones]

    # ------------------------------------------------------------------ MORPHS (4-12% of the ~8.9 diagonal)
    tooth_grow = [morphs.stretch_along(axis, base, 2.3, ms.mask(name)) for (name, base, axis) in teeth]
    shapes = {
        "Charge": [  # the bite: the intake vanes grow into fangs, the jaw juts
            *tooth_grow,
            morphs.stretch_along((0, -1, 0), JAW_HINGE, 1.12, [max(a, b) for a, b in zip(jaw_m, ms.mask(*[t[0] for t in teeth if ".l." in t[0]]))]),
        ],
        "Mass": [  # the lure and the volume: the bulb swells, the hull fattens about its axis
            morphs.scale_about(2.0, ESCA, bulb_m),
            morphs.scale_about(1.38, (0, 0, 0), [max(a, b) for a, b in zip(hull_m, jaw_m)], axes=(1, 0, 1)),
        ],
        "Space": [  # reach: the mast reaches further forward, the stabilisers span out
            morphs.stretch_along((0, -1, 0), MAST_BASE, 1.28, mast_m),
            *[morphs.stretch_along((sx, 0, 0), (sx * FIN_ROOT[0], 0, 0), 1.45, fins[t]) for t, sx in (("L", 1), ("R", -1))],
        ],
        "Time": [  # rate: the stabilisers and the caudal sweep back, the tail lengthens
            *[morphs.sweep((sx, 0, 0), (sx * FIN_ROOT[0], 0, 0), (0, 0.65, 0), fins[t]) for t, sx in (("L", 1), ("R", -1))],
            morphs.stretch_along((0, 1, 0), TAIL_ROOT, 1.22, tail_m),
            morphs.sweep((0, 0, 1), (0, 0, 0), (0, 0.35, 0), caudal_m),
        ],
    }
    for name in ("Charge", "Mass", "Space", "Time"):
        kb, mx, frac, moved = morphs.add_shape(mesh, name, shapes[name])
        ctx.log("shape %s: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))

    # ------------------------------------------------------------------ DOCUMENTATION POSES (AnglerAnimation drives the bones)
    # About the WORLD axes of the Blender frame: +X rotation = the front DOWN; -Z rotation turns a forward part to the
    # pilot's RIGHT (-X) and a rearward part to the LEFT (+X); -Y rotation lifts a +X (left) tip.
    poses = {
        "lure": {"jaw.B": (24, 0, 0), "lure": (10, 0, 0)},
        "turnright": {"tail": (0, 0, -20), "tail.2": (0, 0, -12), "fin.L": (0, -8, 0), "fin.R": (0, -8, 0)},
        "turnleft": {"tail": (0, 0, 20), "tail.2": (0, 0, 12), "fin.L": (0, 8, 0), "fin.R": (0, 8, 0)},
        "dive": {"fin.L": (0, -22, 0), "fin.R": (0, 22, 0), "tail": (-8, 0, 0)},
    }
    names = ("lure", "turnright", "turnleft", "dive")
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
                "lure": [("jaw.B", "z", -1, 0.08), ("esca", "z", -1, 0.15)],
                "turnright": [("tail.2", "x", +1, 0.15)],
                "turnleft": [("tail.2", "x", -1, 0.15)],
                "dive": [("fin.L", "z", +1, 0.035), ("fin.R", "z", +1, 0.035)],
            },
            "shapes": ["Charge", "Mass", "Space", "Time"],
            "slots": list(SLOTS),
            # its own envelope (an anglerfish's bow, not the Stoat stand-in's 0.9-wide body; module doc)
            "size_ref_blender": ((-1.30, -4.35, -0.80), (1.30, 3.85, 1.65)), "size_tol": (0.08, 0.05, 0.08),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.04,
        },
    }
