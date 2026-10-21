"""
marlin - a NEW hull with no artist file: the marlin of Marlin and Jacks (vessel class 22,
Assets/_Scripts/Controller/Vessel/R_VesselActions/MARLIN_JACKS.md), which flew the Rhino's drawn rig until now.
Built to the fleet convention and the fleet's look (Garrett, 2026-10-18: "don't forget they are spaceships
inspired by earth clades"; 2026-10-21: "lets give it some models and get it ready to ship").

THE READ, from the Rhino's chase camera: a long, fast billfish - a spear out front, a tall sail standing on its
back, sickle fins and a crescent tail, with the striped marlin's bars down its flanks in the team colour.

Clade -> ship (every part answers "what is this on the ship?"):
  bill                -> the SPEAR: a long tapering ram ahead of the nose (the Rhino's ramp boost rides it), a domain
                         collar at its root
  torpedo body        -> a deep gunmetal hull, widest just behind the head, tapering to a slim tail boom
  vertical bars       -> the DOMAIN BARS: eight sunk bands down each flank (the striped marlin's), framed in the hull
  sail (first dorsal) -> the SAIL (bone `sail`): a tall plate on the back, highest at its front, with a sunk domain panel.
                         Marlins raise it to herd bait and fold it to sprint; the code raises it on the call and the
                         boost and the Time morph folds it
  eye                 -> a sensor eye in a bezel on each cheek, a glass canopy on the head, three glowing gill vents
  pectoral fins       -> long sickle fins (bones `fin.L` / `fin.R`), domain tips; they follow the Rhino's front
                         plates, which the hull's puppetry turns
  engines             -> two nacelles flanking the rear body, glowing mouths aft
  caudal keels        -> two small keels on the tail boom (bone `tail.1`)
  lunate tail         -> the CRESCENT VANE (bone `tail.2`, child of tail.1), its horns in the domain

Contract (FLEET_CONVENTION sections 1-4 for a NEW vessel):
  file      Assets/_Models/Vessel Models/Marlin.fbx (guid 6d2b8e4f0a9c4e17b3f5a8c1d2e7b904), preset "zup"
  nodes     mesh `mesh`, armature `rig`; root bone `hull`
  bones     hull; sail; fin.L / fin.R; tail.1 -> tail.2. Rigid skinning, one bone per part.
  size      the Rhino's drawn hull it replaces (rhino_shapekey_with_animations.fbx: 25.2 wide, 18.0 long, 15.3 tall in
            the Blender frame, Docs/VesselModels/Rhino.md), nested where the rig was (under `Rhino_Test (1)` at local
            z -1.5545), so the Rhino's five hull colliders still enclose it. A marlin is long, not wide: 23 units bill to
            tail, 14 across the fins, 9.5 tall with the sail (declared in size_ref_blender)
  slots     0 Domain, 1 Body, 2 Window, 3 Engine
  shapes    Charge (weapon): the spear lengthens and the sail stands taller;
            Mass (volume):   the hull deepens and the engines swell;
            Space (reach):   the fins reach along their span and the crescent spreads;
            Time (speed):    the sail folds down and back, the fins sweep, the crescent rakes
  takes     none drive it (code poses the bones by name, MarlinCrewBodies); four documentation poses for the sheet:
            beatleft / beatright (the tail), sailup (the sail raised), flare (the fins brake)
"""

import math
from _common import box_uv

NAME = "Marlin"
OUTPUT = "Assets/_Models/Vessel Models/Marlin.fbx"

ARMATURE = "rig"
MESH_NODE = "mesh"
SLOTS = ("Domain", "Body", "Window", "Engine")
D, H, W, J = 0, 1, 2, 3

# The layout, in the Blender frame: nose -Y, up +Z, pilot's left +X.
NOSE_Y = -7.60                  # where the body's head ends and the spear begins
BILL_TIP = -12.40
BODY_END = 6.00                 # where the body ends and the tail boom begins
TAIL1 = (0.0, 5.60, 0.10)       # the peduncle hinge
TAIL2 = (0.0, 7.40, 0.10)       # the crescent's hinge
TAIL_END = 10.70
FIN_ROOT = (1.55, -3.30, -0.95)
FIN_SPAN, FIN_DIHEDRAL, FIN_SWEEP = 4.70, -14.0, 2.60
SAIL_Y0, SAIL_Y1 = -4.30, 2.70
SAIL_H = 3.60
ENGINE_X, ENGINE_Z, ENGINE_Y0, ENGINE_LEN = 1.42, -0.62, 0.90, 4.70

# (y, half-width, half-height top, half-height bottom, centre lift)
BODY_STATIONS = [
    (NOSE_Y, 0.36, 0.34, 0.30, 0.10),
    (-7.10, 0.80, 0.82, 0.66, 0.14),
    (-6.20, 1.28, 1.40, 1.10, 0.20),
    (-4.80, 1.72, 1.95, 1.52, 0.22),
    (-2.80, 1.95, 2.24, 1.74, 0.22),
    (-0.80, 1.90, 2.16, 1.70, 0.20),
    (1.20, 1.66, 1.88, 1.48, 0.17),
    (3.20, 1.24, 1.40, 1.10, 0.14),
    (4.80, 0.82, 0.92, 0.74, 0.11),
    (BODY_END, 0.50, 0.56, 0.46, 0.10),
]


def _lerp_station(y, k):
    st = BODY_STATIONS
    if y <= st[0][0]:
        return st[0][k]
    for a, b in zip(st, st[1:]):
        if y <= b[0]:
            t = (y - a[0]) / (b[0] - a[0])
            return a[k] + (b[k] - a[k]) * t
    return st[-1][k]


def half_w(y):
    return _lerp_station(y, 1)


def top_z(y):
    return _lerp_station(y, 4) + _lerp_station(y, 2)


def bot_z(y):
    return _lerp_station(y, 4) - _lerp_station(y, 3)


def build(ctx):
    import bpy  # noqa: F401
    import bmesh
    from mathutils import Vector, Matrix
    f, rig, morphs, takes = ctx.forms, ctx.rig, ctx.morphs, ctx.takes
    parts = []

    def tag(obj, pid):
        me = obj.data
        a = me.attributes.get("pid") or me.attributes.new("pid", "FLOAT", "POINT")
        a.data.foreach_set("value", [float(pid)] * len(me.vertices))
        return obj

    def piece(obj, slot, pid, bevel=0.0):
        f.ensure_slots(obj, SLOTS)
        if slot is not None:
            f.paint(obj, slot)
        if bevel > 0:
            f.bevel(obj, width=bevel, segments=1, angle_deg=30)
            f.apply_modifiers(obj)
        tag(obj, pid)
        return obj

    def bone_part(objs, name):
        if len(objs) == 1:
            o = objs[0]
            o.name = name
        else:
            o = f.join(objs, name, SLOTS)
        for vg in list(o.vertex_groups):
            o.vertex_groups.remove(vg)
        f.tag_group(o, name)
        return o

    def vplate(name, outline, thickness, origin):
        """A plate in the body's mid plane (x = 0): outline points are (along +Y from origin, up +Z), given clockwise."""
        return f.plate(name, list(reversed(outline)), thickness, normal=(1, 0, 0), origin=origin, right=(0, 1, 0))

    head = []

    # ------------------------------------------------------------------ THE BODY
    length = BODY_END - NOSE_Y
    dense = []
    for k in range(37):
        t = k / 36
        y = NOSE_Y + t * length
        dense.append((t, half_w(y), _lerp_station(y, 2), _lerp_station(y, 3), 2.4, 2.6, _lerp_station(y, 4)))
    body = f.fuselage("body", length, nose=(0, NOSE_Y, 0.0), axis=(0, 1, 0), segments=28, stations=dense)
    f.ensure_slots(body, SLOTS)
    f.paint(body, H)

    def band(c):
        mid = _lerp_station(c.y, 4)
        hh = _lerp_station(c.y, 2) if c.z >= mid else _lerp_station(c.y, 3)
        return (c.z - mid) / max(hh, 1e-3)
    # the striped marlin's bars: eight bands down each flank, upper body to just below the midline
    bars = [(-3.6 + 0.98 * k, 0.22 + 0.02 * k) for k in range(8)]
    bar = lambda c, n, i: abs(n.x) > 0.45 and -0.45 < band(c) < 0.80 and any(abs(c.y - y) < w for y, w in bars)
    f.paint(body, D, bar)
    f.inset_faces(body, lambda c, n, i: i == D, 0.03, depth=0.03, slot=D)
    head.append(piece(body, None, 1))

    # the spear: a long tapering ram, a domain collar at its root
    bill_len = NOSE_Y - BILL_TIP + 0.25
    bill = f.lathe("bill", [(0.0, 0.0), (0.05, 0.10), (0.11, 0.9), (0.19, 2.2), (0.28, 3.6), (0.36, 4.4),
                            (0.40, bill_len), (0.0, bill_len)],
                   segments=14, axis=(0, 1, 0), origin=(0, BILL_TIP, 0.16))
    f.ensure_slots(bill, SLOTS)
    f.paint(bill, H)
    f.paint(bill, D, lambda c, n, i: NOSE_Y - 0.55 < c.y < NOSE_Y - 0.15)
    head.append(piece(bill, None, 2))
    # the lower jaw: a short beak under the spear's root
    jaw = f.lathe("jaw", [(0.0, 0.0), (0.10, 0.15), (0.24, 0.9), (0.32, 1.5), (0.0, 1.5)], segments=12,
                  axis=(0, 1, 0), origin=(0, NOSE_Y - 1.10, -0.18))
    head.append(piece(jaw, H, 3))

    # the eyes: a sensor in a bezel on each cheek; the gill vents behind them
    for sx in (1, -1):
        y0 = -6.45
        cx = sx * half_w(y0) * 0.86
        cz = _lerp_station(y0, 4) + 0.18 * _lerp_station(y0, 2)
        axis = Vector((sx * 0.95, -0.28, 0.12)).normalized()
        bez = f.lathe("bezel", [(0.0, -0.06), (0.28, -0.06), (0.31, 0.0), (0.26, 0.05), (0.0, 0.05)], segments=16,
                      axis=tuple(axis), origin=(cx, y0, cz))
        head.append(piece(bez, H, 4))
        eye = f.sphere("eye", 0.20, center=tuple(Vector((cx, y0, cz)) + axis * 0.06), segments=14, rings=8)
        head.append(piece(eye, W, 4))
        for k in range(3):
            y = -5.55 + k * 0.30
            gx = half_w(y) * 0.96
            lv = f.box("gill", (0.10, 0.10, 0.85), center=(0, 0, 0))
            f.transform_mesh(lv, Matrix.Translation(Vector((sx * gx, y, _lerp_station(y, 4) + 0.05 * _lerp_station(y, 2))))
                             @ Matrix.Rotation(math.radians(sx * 16), 4, "Y") @ Matrix.Rotation(math.radians(-14), 4, "X"))
            head.append(piece(lv, J, 4, bevel=0.012))
    # the cockpit canopy on the head, ahead of the sail
    cy = -5.55
    can = f.sphere("canopy", 1.0, center=(0, cy, top_z(cy) - 0.20), segments=20, rings=10, scale=(0.62, 1.30, 0.46))
    head.append(piece(can, W, 5))
    frame = f.sphere("canopyframe", 1.0, center=(0, cy + 0.08, top_z(cy) - 0.30), segments=20, rings=10,
                     scale=(0.72, 1.48, 0.46))
    head.append(piece(frame, H, 5))
    # pectoral hinge fairings
    for sx in (1, -1):
        fr = f.sphere("fairing", 1.0, center=(sx * (FIN_ROOT[0] + 0.05), FIN_ROOT[1] + 0.65, FIN_ROOT[2]),
                      segments=12, rings=6, scale=(0.26, 0.85, 0.24))
        head.append(piece(fr, H, 6))

    # the engines: two nacelles flanking the rear body, mouths glowing aft
    for sx in (1, -1):
        x = sx * ENGINE_X
        y_end = ENGINE_Y0 + ENGINE_LEN
        prof = [(0.0, 0.0), (0.22, 0.10), (0.46, 0.65), (0.55, 2.2), (0.52, 3.9), (0.47, ENGINE_LEN - 0.05),
                (0.34, ENGINE_LEN - 0.05), (0.31, ENGINE_LEN - 0.25), (0.0, ENGINE_LEN - 0.25)]
        nac = f.lathe("engine", prof, segments=16, axis=(0, 1, 0), origin=(x, ENGINE_Y0, ENGINE_Z))
        f.ensure_slots(nac, SLOTS)
        f.paint(nac, H)
        f.paint(nac, D, lambda c, n, i, x=x: ENGINE_Y0 + 2.0 < c.y < ENGINE_Y0 + 2.35)
        f.paint(nac, J, lambda c, n, i, x=x, y_end=y_end: c.y > y_end - 0.32 and math.hypot(c.x - x, c.z - ENGINE_Z) < 0.36)
        head.append(piece(nac, None, 7))

    # the second dorsal and the anal finlet (small, near the tail)
    d2 = vplate("dorsal2", [(0.0, -0.3), (0.0, 0.0), (0.35, 0.55), (0.95, 0.42), (1.30, 0.0), (1.30, -0.3)], 0.08,
                (0, 4.30, top_z(4.30)))
    head.append(piece(d2, H, 8, bevel=0.01))
    an = vplate("anal", [(0.0, 0.3), (1.40, 0.3), (1.40, 0.0), (0.95, -0.55), (0.30, -0.70), (0.0, 0.0)], 0.08,
                (0, 3.30, bot_z(3.30)))
    head.append(piece(an, H, 8, bevel=0.01))
    parts.append(bone_part(head, "hull"))

    # ------------------------------------------------------------------ THE SAIL (bone `sail`)
    sroot_z = top_z(-1.0) - 0.30
    slen = SAIL_Y1 - SAIL_Y0
    upper = [(slen * 0.03, SAIL_H * 0.75), (slen * 0.07, SAIL_H * 0.97), (slen * 0.15, SAIL_H), (slen * 0.30, SAIL_H * 0.86),
             (slen * 0.48, SAIL_H * 0.64), (slen * 0.66, SAIL_H * 0.46), (slen * 0.84, SAIL_H * 0.30), (slen, SAIL_H * 0.14)]
    outline = [(0.0, -0.25), (0.0, 0.0)] + upper + [(slen, -0.25)]
    sail = vplate("sail", outline, 0.10, (0, SAIL_Y0, sroot_z))
    f.ensure_slots(sail, SLOTS)
    f.paint(sail, H)
    f.inset_faces(sail, lambda c, n, i: abs(n.x) > 0.9, 0.16, depth=0.025, slot=D)
    parts.append(bone_part([piece(sail, None, 9)], "sail"))

    # ------------------------------------------------------------------ PECTORAL FINS (fin.L / fin.R): long sickles
    span_dir = {}
    for tag_, sx in (("L", 1), ("R", -1)):
        root = Vector((sx * FIN_ROOT[0], FIN_ROOT[1], FIN_ROOT[2]))
        wg = f.wing("fin", root=tuple(root), span=FIN_SPAN, chord_root=2.10, chord_tip=0.55, sweep=FIN_SWEEP,
                    dihedral_deg=FIN_DIHEDRAL, thickness=0.08, sections=6, segments=16, side=sx, taper_power=0.8)
        f.ensure_slots(wg, SLOTS)
        f.paint(wg, H)
        f.paint(wg, D, lambda c, n, i, sx=sx: sx * c.x > FIN_ROOT[0] + FIN_SPAN * 0.66)
        d = math.radians(FIN_DIHEDRAL)
        span_dir[sx] = Vector((sx * math.cos(d), 0.0, math.sin(d)))
        parts.append(bone_part([piece(wg, None, 10, bevel=0.006)], "fin." + tag_))

    # ------------------------------------------------------------------ THE TAIL BOOM AND KEELS (tail.1)
    tb = []
    boom = f.lathe("boom", [(0.0, 0.0), (0.50, 0.0), (0.46, 0.8), (0.36, 1.6), (0.30, 2.05), (0.0, 2.05)], segments=16,
                   axis=(0, 1, 0), origin=(0, BODY_END - 0.30, TAIL1[2]))
    tb.append(piece(boom, H, 11))
    collar = f.lathe("collar", [(0.0, 0.0), (0.58, 0.0), (0.60, 0.18), (0.56, 0.32), (0.0, 0.32)], segments=16,
                     axis=(0, 1, 0), origin=(0, BODY_END - 0.40, TAIL1[2]))
    tb.append(piece(collar, D, 11))
    for sx in (1, -1):
        kl = f.plate("keel", [(0.0, -0.05), (0.0, 0.05), (1.4, 0.0)][::-1], 0.10, normal=(0, 0, 1),
                     origin=(sx * 0.32, BODY_END + 0.30, TAIL1[2]), right=(0, 1, 0))
        # a keel is a low wedge sticking out of each side of the boom
        for v in kl.data.vertices:
            v.co.x += sx * 0.36 * max(0.0, 1.0 - abs(v.co.y - (BODY_END + 1.0)) / 0.9)
        tb.append(piece(kl, H, 11))
    parts.append(bone_part(tb, "tail.1"))

    # ------------------------------------------------------------------ THE CRESCENT VANE (tail.2)
    flen = TAIL_END - TAIL2[1]
    lead = [(flen * 0.10, 0.70), (flen * 0.30, 1.85), (flen * 0.55, 3.05), (flen * 0.80, 4.00), (flen, 4.55)]
    trail = [(flen * 0.80, 3.55), (flen * 0.60, 2.45), (flen * 0.46, 1.25), (flen * 0.40, 0.0)]
    cres = ([(0.0, -0.32), (0.0, 0.32)] + lead + trail + [(x, -z) for (x, z) in reversed(trail[:-1])]
            + [(x, -z) for (x, z) in reversed(lead)])
    vane = vplate("crescent", cres, 0.12, (0, TAIL2[1] - 0.15, TAIL2[2]))
    f.ensure_slots(vane, SLOTS)
    f.paint(vane, H)
    # the crescent's horns carry the domain (an inset on this concave outline folds over itself)
    f.paint(vane, D, lambda c, n, i: abs(c.z - TAIL2[2]) > 2.6)
    hub = f.lathe("hub", [(0.0, -0.05), (0.34, -0.05), (0.38, 0.25), (0.30, 0.55), (0.0, 0.60)], segments=14,
                  axis=(0, 1, 0), origin=(0, TAIL2[1] - 0.25, TAIL2[2]))
    parts.append(bone_part([piece(vane, None, 12), piece(hub, H, 12)], "tail.2"))

    # ------------------------------------------------------------------ JOIN, n-gons out
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
    f.sharpen_by_angle(mesh, 40)
    box_uv(mesh)
    ctx.log("marlin joined: %d verts, %d tris (n-gons triangulated: %d)" % (len(mesh.data.vertices), f.tri_count(mesh), len(big)))

    # ------------------------------------------------------------------ RIG
    B = rig.BoneSpec
    sail_head = (0, SAIL_Y0 + 0.3, sroot_z)
    bones = [B("hull", (0, 0, 0), (0, -2.0, 0)),
             B("sail", sail_head, (0, SAIL_Y0 + 0.3, sroot_z + SAIL_H), parent="hull"),
             B("tail.1", TAIL1, TAIL2, parent="hull"),
             B("tail.2", TAIL2, (0, TAIL_END, TAIL2[2]), parent="tail.1")]
    for tag_, sx in (("L", 1), ("R", -1)):
        h = Vector((sx * FIN_ROOT[0], FIN_ROOT[1], FIN_ROOT[2]))
        bones.append(B("fin." + tag_, h, h + span_dir[sx] * FIN_SPAN, parent="hull"))
    arm = rig.build_armature(ARMATURE, bones)
    rig.skin_rigid(mesh, arm, default_bone="hull")
    bone_names = [b.name for b in arm.data.bones]
    me = mesh.data

    # ------------------------------------------------------------------ MORPHS (4-12% of the ~25 diagonal)
    nv = len(me.vertices)
    pid = [0.0] * nv
    me.attributes["pid"].data.foreach_get("value", pid)

    def pmask(*ids):
        s = set(float(i) for i in ids)
        return [1.0 if p in s else 0.0 for p in pid]
    ms = morphs.MaskSet(mesh)
    fin = {1: ms.mask("fin.L"), -1: ms.mask("fin.R")}
    sailm = ms.mask("sail")
    vanem = ms.mask("tail.2")
    bodym = pmask(1, 3, 4, 5, 8)
    billm = pmask(2)
    engines = pmask(7)

    def deepen(factor, mask):
        def fn(i, co, n):
            if mask[i] <= 0:
                return Vector((0, 0, 0))
            mid = _lerp_station(co.y, 4)
            return Vector((co.x * (factor - 1) * 0.7, 0, (co.z - mid) * (factor - 1)))
        return fn

    def fold_sail(down, back):
        def fn(i, co, n):
            if sailm[i] <= 0:
                return Vector((0, 0, 0))
            h = max(0.0, co.z - sroot_z)
            return Vector((0, back * h, -down * h))
        return fn

    shapes = {
        "Charge": [  # the weapon: the spear lengthens, the sail stands taller
            morphs.stretch_along((0, -1, 0), (0, NOSE_Y, 0), 1.45, billm),
            morphs.scale_about(1.40, sail_head, sailm, axes=(0, 0.3, 1)),
        ],
        "Mass": [  # volume: the hull deepens, the engines swell
            deepen(1.45, bodym),
            morphs.scale_about(1.45, (ENGINE_X, ENGINE_Y0 + 2.4, ENGINE_Z), [e if 1 else 0 for e in engines], axes=(1, 0.6, 1)),
            *[morphs.translate((sx * 0.6, 0, 0), fin[sx]) for sx in (1, -1)],
        ],
        "Space": [  # reach: the fins reach along their span, the crescent spreads
            *[morphs.stretch_along(tuple(span_dir[sx]), (sx * FIN_ROOT[0], FIN_ROOT[1], FIN_ROOT[2]), 1.45, fin[sx],
                                   ramp=(0.2, 1.5)) for sx in (1, -1)],
            morphs.scale_about(1.40, TAIL2, vanem, axes=(0.3, 0.6, 1)),
        ],
        "Time": [  # speed: the sail folds down and back, the fins sweep, the crescent rakes
            fold_sail(0.55, 0.45),
            *[morphs.sweep((sx, 0, 0), (sx * FIN_ROOT[0], 0, 0), (0, 0.45, 0), fin[sx]) for sx in (1, -1)],
            morphs.sweep((0, 0, 1), (0, 0, TAIL2[2]), (0, 0.22, 0), [m if 1 else 0 for m in vanem]),
            morphs.sweep((0, 0, -1), (0, 0, TAIL2[2]), (0, 0.22, 0), [m if 1 else 0 for m in vanem]),
        ],
    }
    for name in ("Charge", "Mass", "Space", "Time"):
        kb, mx, frac, moved = morphs.add_shape(mesh, name, shapes[name])
        ctx.log("shape %s: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))
    if "pid" in me.attributes:
        me.attributes.remove(me.attributes["pid"])

    # ------------------------------------------------------------------ DOCUMENTATION POSES (code drives the bones)
    # +Z yaw carries a point behind the hull toward -X (the pilot's RIGHT); +Y turns the +X (left) fin's tip DOWN;
    # -X pitch folds the sail's top aft (+Y).
    poses = {
        "beatleft": {"tail.1": (0, 0, -8), "tail.2": (0, 0, -16)},
        "beatright": {"tail.1": (0, 0, 8), "tail.2": (0, 0, 16)},
        "sailup": {"sail": (12, 0, 0)},
        "flare": {"fin.L": (0, 35, 0), "fin.R": (0, -35, 0)},
    }
    names = ("beatleft", "beatright", "sailup", "flare")
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
                "beatleft": [("tail.2", "x", +1, 0.4)],
                "beatright": [("tail.2", "x", -1, 0.4)],
                "sailup": [("sail", "y", -1, 0.3)],
                "flare": [("fin.L", "z", -1, 0.3), ("fin.R", "z", -1, 0.3)],
            },
            "shapes": ["Charge", "Mass", "Space", "Time"],
            "slots": list(SLOTS),
            # the brief's envelope: 23 units spear to tail (held to 6%); span and height the clade's (declared)
            "size_ref_blender": ((-6.3, -12.4, -4.6), (6.3, 10.7, 6.2)),
            "size_tol": (0.15, 0.06, 0.15),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.04,
        },
    }
