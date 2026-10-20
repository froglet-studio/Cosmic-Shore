"""
wrassekite - a NEW body with no artist file: the Wrasse's kite, the small craft the navigator slings round
the Grouper on its invisible rope and threads through the Wrasse Run's rings
(Assets/_Scripts/Controller/Vessel/Crew/KITE_NAVIGATOR.md, Assets/_Scripts/Controller/Arcade/WRASSERUN.md).
Built to the fleet convention and the fleet's look (Garrett, 2026-10-18: "don't forget they are spaceships
inspired by earth clades"; 2026-10-20: "give the wrasse and grouper proper 3d vessels with animations").

THE READ: seen at the scale of the kite's 160-unit board, a slim dart about 16 units long with ONE bold
domain stripe from snout to tail - the bluestreak cleaner wrasse's signature - that bends as it is slung.

Clade -> ship (every part answers "what is this on the ship?"):
  slender body        -> a long, narrow gunmetal hull lofted on a flexible spine (spine.1 -> spine.2), weights
                         blended along it so the whole craft swims as it whips round the rope
  lateral stripe      -> the DOMAIN STRIPE: one band along each flank, snout to tail, widening aft as the
                         bluestreak's does, sunk in a dark frame
  pointed snout       -> the SENSOR PROBE: a needle mast with a glowing sensor tip ahead of the nose
  eye                 -> a sensor eye in a bezel set IN the stripe on each cheek, and a small cockpit canopy
  long dorsal / anal  -> two long low CONTROL VANES along the back and the belly, a domain trim on their edge
  pectoral fins       -> two small control vanes on hinges (bones fin.L / fin.R)
  small fan tail      -> the VECTORING FAN (bone `tail`), with a sunk domain panel, behind two small engines
                         whose glowing mouths flank the peduncle

Contract (FLEET_CONVENTION sections 1-4 for a NEW vessel):
  file      Assets/_Models/Vessel Models/WrasseKite.fbx (guid c56064d56acc061a124729744a5966fa), preset "zup"
  nodes     mesh `mesh`, armature `rig`; root bone `hull`
  bones     hull; spine.1 -> spine.2 -> tail; fin.L / fin.R. The body and its two long vanes are weighted
            along the spine (hull / spine.1 / spine.2 / tail); every other part is rigid on one bone.
  size      16 units snout tip to fan tip, centred on its own origin (y -8 .. +8 in the Blender frame)
  slots     0 Domain, 1 Body, 2 Window, 3 Engine
  shapes    Charge (weapon): the sensor probe extends and the dorsal vane stands up;
            Mass (volume):   the body and its vanes deepen;
            Space (reach):   the pectoral vanes reach out and the fan spreads;
            Time (speed):    the vanes sweep back, the body sleeks longer and the fan stretches
  takes     none drive it yet (code poses the bones by name); three documentation poses for the sheet and the
            studio: beatleft / beatright (the spine swims), flare (the pectoral vanes brake)
"""

import math
from _common import box_uv

NAME = "WrasseKite"
OUTPUT = "Assets/_Models/Vessel Models/WrasseKite.fbx"

ARMATURE = "rig"
MESH_NODE = "mesh"
SLOTS = ("Domain", "Body", "Window", "Engine")
D, H, W, J = 0, 1, 2, 3

# The layout, in the Blender frame: nose -Y, up +Z, pilot's left +X.
NOSE_Y = -7.00                  # the body's snout; the probe runs on to PROBE_TIP
PROBE_TIP = -7.98
PEDUNCLE_Y = 5.70
SPINE1, SPINE2, TAIL = (0, -1.0, 0), (0, 2.4, 0), (0, 5.5, 0)
FAN_END = 8.0
FIN_ROOT = (0.50, -3.40, -0.22)
FIN_SPAN, FIN_DIHEDRAL, FIN_SWEEP = 0.95, -14.0, 0.42
ENGINE_X, ENGINE_Z = 0.34, -0.02
# where the spine weights peak (hull is full ahead of the first knot, tail full behind the last)
KNOTS = (("hull", -1.6), ("spine.1", 0.7), ("spine.2", 3.9), ("tail", 6.1))

# (y, half-width, half-height top, half-height bottom, centre lift)
BODY_STATIONS = [
    (NOSE_Y, 0.06, 0.06, 0.05, 0.00),
    (-6.60, 0.20, 0.20, 0.16, 0.00),
    (-6.00, 0.36, 0.38, 0.30, 0.02),
    (-5.00, 0.52, 0.58, 0.46, 0.04),
    (-3.80, 0.60, 0.70, 0.56, 0.05),
    (-2.00, 0.63, 0.74, 0.60, 0.05),
    (0.00, 0.60, 0.71, 0.57, 0.05),
    (2.00, 0.52, 0.61, 0.50, 0.04),
    (3.80, 0.38, 0.45, 0.36, 0.03),
    (5.00, 0.25, 0.29, 0.24, 0.02),
    (PEDUNCLE_Y, 0.18, 0.21, 0.17, 0.02),
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


def spine_weights(y):
    """Hat functions over KNOTS: the share of each spine bone at a station."""
    if y <= KNOTS[0][1]:
        return {KNOTS[0][0]: 1.0}
    for (na, ya), (nb, yb) in zip(KNOTS, KNOTS[1:]):
        if y <= yb:
            t = (y - ya) / (yb - ya)
            t = t * t * (3 - 2 * t)
            return {na: 1.0 - t, nb: t}
    return {KNOTS[-1][0]: 1.0}


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

    def piece(obj, slot, pid, bevel=0.0, sharp=40.0):
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

    head, flex = [], []   # rigid on hull / weighted along the spine

    # ------------------------------------------------------------------ THE BODY (slender hull on a flexible spine)
    length = PEDUNCLE_Y - NOSE_Y
    st = [((y - NOSE_Y) / length, w, ht, hb, 2.4, 2.6, dz) for (y, w, ht, hb, dz) in BODY_STATIONS]
    # many stations along the length so the spine weights bend it smoothly
    dense = []
    for k in range(41):
        t = k / 40
        y = NOSE_Y + t * length
        dense.append((t, half_w(y), _lerp_station(y, 2), _lerp_station(y, 3), 2.4, 2.6, _lerp_station(y, 4)))
    dense[0] = st[0]
    body = f.fuselage("body", length, nose=(0, NOSE_Y, 0.0), axis=(0, 1, 0), segments=24, stations=dense)
    f.ensure_slots(body, SLOTS)
    f.paint(body, H)

    def band(c):
        mid = _lerp_station(c.y, 4)
        hh = _lerp_station(c.y, 2) if c.z >= mid else _lerp_station(c.y, 3)
        return (c.z - mid) / max(hh, 1e-3)
    # the bluestreak: one band per flank, snout to tail, widening aft
    stripe = lambda c, n, i: abs(n.x) > 0.35 and NOSE_Y + 0.35 < c.y < PEDUNCLE_Y - 0.05 and \
        abs(band(c) - 0.05) < 0.24 + 0.22 * (c.y - NOSE_Y) / length
    f.paint(body, D, stripe)
    f.inset_faces(body, lambda c, n, i: i == D, 0.025, depth=0.02, slot=D)
    flex.append(piece(body, None, 1))

    # the long control vanes: dorsal along the back, anal along the belly (domain trim on the edge)
    def vane(name, y0, y1, height, up_sign, pid):
        z_at = top_z if up_sign > 0 else bot_z
        root_z = z_at(y0) - up_sign * 0.06
        vn = f.wing(name, root=(0, y0, root_z), span=height, chord_root=y1 - y0, chord_tip=(y1 - y0) * 0.82,
                    sweep=(y1 - y0) * 0.16, forward=(0, -1, 0), up=(-up_sign, 0, 0), thickness=0.022, sections=3,
                    segments=60, tip_round=False)
        # the root follows the body's taper, and the vane's height ramps in from its front and eases off aft
        # (a long low fin, not a plank)
        for v in vn.data.vertices:
            u = max(0.0, min(1.0, (v.co.y - y0) / (y1 - y0)))
            prof = min(1.0, (u / 0.28) ** 0.7) * (1.0 - 0.35 * max(0.0, (u - 0.75) / 0.25))
            hgt = (v.co.z - root_z) * up_sign
            v.co.z = z_at(v.co.y) - up_sign * 0.06 + up_sign * max(0.0, hgt) * max(prof, 0.08)
        f.ensure_slots(vn, SLOTS)
        f.paint(vn, H)
        f.paint(vn, D, lambda c, n, i: up_sign * (c.z - z_at(c.y)) > height * 0.66)
        return piece(vn, None, pid, bevel=0.004)
    flex.append(vane("dorsal", -3.7, 4.4, 0.58, 1, 2))
    flex.append(vane("anal", -0.2, 4.4, 0.48, -1, 3))

    # the sensor probe: a needle mast ahead of the snout with a glowing tip, a dark collar at its root
    probe = f.lathe("probe", [(0.0, 0.0), (0.035, 0.0), (0.045, 0.60), (0.08, 0.86), (0.11, 0.98), (0.0, 0.98)],
                    segments=12, axis=(0, 1, 0), origin=(0, PROBE_TIP + 0.10, 0.0))
    head.append(piece(probe, H, 4))
    ptip = f.sphere("probetip", 0.075, center=(0, PROBE_TIP + 0.06, 0.0), segments=10, rings=6, scale=(1.0, 1.6, 1.0))
    head.append(piece(ptip, W, 4))
    # the eye: a sensor in a bezel set in the stripe on each cheek
    for sx in (1, -1):
        y0 = -5.75
        cx = sx * half_w(y0) * 0.88
        cz = _lerp_station(y0, 4) + 0.05 * _lerp_station(y0, 2)
        axis = Vector((sx * 0.95, -0.25, 0.15)).normalized()
        bez = f.lathe("bezel", [(0.0, -0.05), (0.15, -0.05), (0.17, 0.0), (0.14, 0.03), (0.0, 0.03)], segments=14,
                      axis=tuple(axis), origin=(cx, y0, cz))
        head.append(piece(bez, H, 5))
        eye = f.sphere("eye", 0.105, center=tuple(Vector((cx, y0, cz)) + axis * 0.04), segments=12, rings=6)
        head.append(piece(eye, W, 5))
    # the gill vents: three glowing slots per side behind the eye, above the stripe
    for sx in (1, -1):
        for k in range(3):
            y = -5.05 + k * 0.17
            gx = half_w(y) * 0.955
            lv = f.box("gill", (0.06, 0.06, 0.34), center=(0, 0, 0))
            f.transform_mesh(lv, Matrix.Translation(Vector((sx * gx, y, _lerp_station(y, 4) + 0.36 * _lerp_station(y, 2))))
                             @ Matrix.Rotation(math.radians(sx * 18), 4, "Y") @ Matrix.Rotation(math.radians(-12), 4, "X"))
            head.append(piece(lv, J, 5, bevel=0.008))
    # the cockpit canopy: a small glass teardrop on the back, behind the eyes, ahead of the dorsal vane
    cy = -4.8
    can = f.sphere("canopy", 1.0, center=(0, cy, top_z(cy) - 0.12), segments=18, rings=10, scale=(0.30, 0.85, 0.24))
    head.append(piece(can, W, 6))
    frame = f.sphere("canopyframe", 1.0, center=(0, cy + 0.04, top_z(cy) - 0.17), segments=18, rings=10,
                     scale=(0.36, 1.0, 0.24))
    head.append(piece(frame, H, 6))
    for sx in (1, -1):   # pectoral hinge fairings
        fr = f.sphere("fairing", 1.0, center=(sx * (FIN_ROOT[0] + 0.02), FIN_ROOT[1] + 0.30, FIN_ROOT[2]),
                      segments=12, rings=6, scale=(0.13, 0.42, 0.13))
        head.append(piece(fr, H, 7))
    parts.append(bone_part(head, "hull"))

    # the two small engines flank the peduncle (rigid on spine.2), mouths glowing aft
    eng = []
    for sx in (1, -1):
        y_end = PEDUNCLE_Y - 0.08
        prof = [(0.0, 0.0), (0.08, 0.06), (0.16, 0.35), (0.19, 0.85), (0.18, 1.10), (0.17, 1.15), (0.13, 1.15), (0.12, 1.05),
                (0.0, 1.05)]
        x = sx * (ENGINE_X)
        nac = f.lathe("engine", prof, segments=14, axis=(0, 1, 0), origin=(x, y_end - 1.15, ENGINE_Z))
        f.ensure_slots(nac, SLOTS)
        f.paint(nac, H)
        f.paint(nac, J, lambda c, n, i, x=x, y_end=y_end: c.y > y_end - 0.13 and math.hypot(c.x - x, c.z - ENGINE_Z) < 0.16)
        eng.append(piece(nac, None, 8, sharp=35))
    parts.append(bone_part(eng, "spine.2"))

    # ------------------------------------------------------------------ PECTORAL VANES (fin.L / fin.R)
    span_dir = {}
    for tag_, sx in (("L", 1), ("R", -1)):
        root = Vector((sx * FIN_ROOT[0], FIN_ROOT[1], FIN_ROOT[2]))
        wg = f.wing("vane", root=tuple(root), span=FIN_SPAN, chord_root=0.80, chord_tip=0.36, sweep=FIN_SWEEP,
                    dihedral_deg=FIN_DIHEDRAL, thickness=0.09, sections=4, segments=14, side=sx)
        f.ensure_slots(wg, SLOTS)
        f.paint(wg, H)
        f.paint(wg, D, lambda c, n, i, sx=sx: sx * c.x > FIN_ROOT[0] + FIN_SPAN * 0.62)
        d = math.radians(FIN_DIHEDRAL)
        span_dir[sx] = Vector((sx * math.cos(d), 0.0, math.sin(d)))
        parts.append(bone_part([piece(wg, None, 9, bevel=0.004)], "fin." + tag_))

    # ------------------------------------------------------------------ THE VECTORING FAN (tail)
    fp = []
    root_y = PEDUNCLE_Y - 0.25
    flen = FAN_END - root_y
    # the leading edges flare from the narrow root to a broad, gently rounded trailing edge
    upper = [(flen * (0.12 + 0.76 * u), 0.16 + 0.88 * math.sin(u * math.pi / 2)) for u in (k / 10 for k in range(1, 11))]
    trailing = [(flen - 0.12 + 0.12 * math.cos(a), 1.04 * math.sin(a))
                for a in (math.radians(90 - 180 * k / 8) for k in range(1, 8))]
    lower = [(x, -z) for (x, z) in reversed(upper)]
    outline = [(0.0, -0.16), (0.0, 0.16)] + upper + trailing + lower
    fan = f.plate("fan", list(reversed(outline)), 0.09, normal=(1, 0, 0), origin=(0, root_y, 0.02), right=(0, 1, 0))
    f.ensure_slots(fan, SLOTS)
    f.paint(fan, H)
    f.bevel(fan, width=0.02, segments=1, angle_deg=30)
    f.apply_modifiers(fan)
    f.inset_faces(fan, lambda c, n, i: abs(n.x) > 0.9, 0.12, depth=0.02, slot=D)
    fp.append(piece(fan, None, 10, sharp=40))
    hub = f.lathe("hub", [(0.0, -0.05), (0.20, -0.05), (0.24, 0.15), (0.20, 0.35), (0.0, 0.40)], segments=14,
                  axis=(0, 1, 0), origin=(0, root_y - 0.05, 0.02))
    fp.append(piece(hub, H, 10))
    parts.append(bone_part(fp, "tail"))

    # the flexing body and vanes: one part, weighted along the spine after the bind
    parts.append(bone_part(flex, "flex"))

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
    ctx.log("wrasse kite joined: %d verts, %d tris (n-gons triangulated: %d)" % (len(mesh.data.vertices), f.tri_count(mesh), len(big)))

    # ------------------------------------------------------------------ RIG
    B = rig.BoneSpec
    bones = [B("hull", (0, 0, 0), (0, -1.6, 0)),
             B("spine.1", SPINE1, SPINE2, parent="hull"),
             B("spine.2", SPINE2, TAIL, parent="spine.1"),
             B("tail", TAIL, (0, FAN_END, 0), parent="spine.2")]
    for tag_, sx in (("L", 1), ("R", -1)):
        h = Vector((sx * FIN_ROOT[0], FIN_ROOT[1], FIN_ROOT[2]))
        bones.append(B("fin." + tag_, h, h + span_dir[sx] * FIN_SPAN, parent="hull"))
    arm = rig.build_armature(ARMATURE, bones)
    me = mesh.data
    flex_idx = [v.index for v in me.vertices if any(mesh.vertex_groups[g.group].name == "flex" for g in v.groups)]
    rig.skin_rigid(mesh, arm, default_bone="hull")   # "flex" folds into hull here; the blend below replaces it
    groups = {n: mesh.vertex_groups.get(n) or mesh.vertex_groups.new(name=n) for n, _ in KNOTS}
    for i in flex_idx:
        for g in groups.values():
            g.remove([i])
        for n, w in spine_weights(me.vertices[i].co.y).items():
            if w > 1e-4:
                groups[n].add([i], w, "REPLACE")
    bone_names = [b.name for b in arm.data.bones]

    # ------------------------------------------------------------------ MORPHS (4-12% of the ~16.4 diagonal)
    nv = len(me.vertices)
    pid = [0.0] * nv
    me.attributes["pid"].data.foreach_get("value", pid)

    def pmask(*ids):
        s = set(float(i) for i in ids)
        return [1.0 if p in s else 0.0 for p in pid]
    ms = morphs.MaskSet(mesh)
    fin = {1: ms.mask("fin.L"), -1: ms.mask("fin.R")}
    fanm = ms.mask("tail")
    bodym = pmask(1, 2, 3)
    dorsal = pmask(2)
    vanes = pmask(2, 3)
    probe = pmask(4)
    engines = pmask(8)

    def deepen(factor, mask):
        def fn(i, co, n):
            if mask[i] <= 0:
                return Vector((0, 0, 0))
            mid = _lerp_station(co.y, 4)
            return Vector((co.x * (factor - 1) * 0.6, 0, (co.z - mid) * (factor - 1)))
        return fn

    def raise_vane(factor, mask, up_sign):
        z_at = top_z if up_sign > 0 else bot_z

        def fn(i, co, n):
            if mask[i] <= 0:
                return Vector((0, 0, 0))
            h = max(0.0, up_sign * (co.z - z_at(co.y)))
            return Vector((0, 0, up_sign * h * (factor - 1)))
        return fn

    def sweep_vanes(lean):
        def fn(i, co, n):
            if vanes[i] <= 0:
                return Vector((0, 0, 0))
            up = 1 if dorsal[i] > 0 else -1
            z_at = top_z if up > 0 else bot_z
            h = max(0.0, up * (co.z - z_at(co.y)))
            return Vector((0, lean * h, 0))
        return fn

    shapes = {
        "Charge": [  # the weapon: the sensor probe extends, the dorsal vane stands up
            morphs.translate((0, -0.95, 0), probe),
            raise_vane(1.9, dorsal, 1),
        ],
        "Mass": [  # volume: the body and its vanes deepen
            deepen(1.55, bodym),
            *[morphs.translate((sx * 0.25, 0, 0), fin[sx]) for sx in (1, -1)],
            morphs.scale_about(1.35, (0, PEDUNCLE_Y - 0.6, ENGINE_Z), engines),
        ],
        "Space": [  # reach: the pectoral vanes reach out, the fan spreads, the vanes stand taller
            *[morphs.stretch_along(tuple(span_dir[sx]), (sx * FIN_ROOT[0], FIN_ROOT[1], FIN_ROOT[2]), 1.75, fin[sx],
                                   ramp=(0.1, 0.6)) for sx in (1, -1)],
            morphs.scale_about(1.65, (0, root_y, 0.02), fanm, axes=(1, 1, 1)),
            raise_vane(1.35, pmask(3), -1),
        ],
        "Time": [  # speed: the vanes sweep back, the body sleeks longer, the fan stretches
            sweep_vanes(1.4),
            *[morphs.sweep((sx, 0, 0), (sx * FIN_ROOT[0], 0, 0), (0, 0.65, 0), fin[sx]) for sx in (1, -1)],
            morphs.stretch_along((0, 1, 0), (0, 0, 0), 1.06, [max(a, b) for a, b in zip(bodym, engines)]),
            morphs.translate((0, 0.06 * PEDUNCLE_Y, 0), fanm),
            morphs.stretch_along((0, 1, 0), (0, root_y, 0), 1.25, fanm),
        ],
    }
    for name in ("Charge", "Mass", "Space", "Time"):
        kb, mx, frac, moved = morphs.add_shape(mesh, name, shapes[name])
        ctx.log("shape %s: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))
    if "pid" in me.attributes:
        me.attributes.remove(me.attributes["pid"])

    # ------------------------------------------------------------------ DOCUMENTATION POSES (code drives the bones)
    # +Z yaw carries a point behind the hull toward -X (the pilot's RIGHT); +Y turns the +X (left) vane's tip DOWN.
    poses = {
        "beatleft": {"spine.1": (0, 0, -7), "spine.2": (0, 0, -11), "tail": (0, 0, -16)},
        "beatright": {"spine.1": (0, 0, 7), "spine.2": (0, 0, 11), "tail": (0, 0, 16)},
        "flare": {"fin.L": (0, 40, 0), "fin.R": (0, -40, 0)},
    }
    names = ("beatleft", "beatright", "flare")
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
                "beatleft": [("tail", "x", +1, 0.5)],
                "beatright": [("tail", "x", -1, 0.5)],
                "flare": [("fin.L", "z", -1, 0.2), ("fin.R", "z", -1, 0.2)],
            },
            "shapes": ["Charge", "Mass", "Space", "Time"],
            "slots": list(SLOTS),
            # the brief's envelope: 16 units snout to tail (held to 6%), width and height free (declared)
            "size_ref_blender": ((-1.6, -8.0, -1.2), (1.6, 8.0, 1.4)),
            "size_tol": (0.6, 0.06, 0.6),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.04,
        },
    }
