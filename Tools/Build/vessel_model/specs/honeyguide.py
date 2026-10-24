"""
honeyguide - a NEW hull with no artist file: the Honeyguide and Ratel, the two-seat crew vessel
(Assets/_Scripts/Controller/Vessel/Crew/HONEYGUIDE.md), drawn until now by the Sparrow's model its prefab was
cloned from. Built to the fleet convention and the fleet's look (Garrett, 2026-10-18: "don't forget they are
spaceships inspired by earth clades"; 2026-10-21: "Get everything including models etc. Together so we are
ready to /ship").

THE READ, from the chase camera (the Sparrow's followOffset, about 19 px per unit at 1080p, so the 12-unit hull
is ~230 px long): a small, sharp-winged scout bird of a ship - a stout body with a stubby beak, long pointed
swept wings, a fanned tail with pale outer vanes, and a hexagonal turret on its back where the second seat (the
Ratel, the honey badger the honeyguide leads) plays the Dynamo.

Clade -> ship (every part answers "what is this on the ship?"):
  honeyguide body     -> a stout, rounded gunmetal fuselage; a domain saddle across the shoulders and a domain
                         chin band, both sunk in dark frames
  stubby beak         -> the SENSOR BEAK (bone `beak`): a short faceted cone with a glowing sensor tip, on a
                         collar at the face
  eye                 -> the canopy: a glass bubble on the forehead in a dark frame (the Honeyguide's seat)
  the Ratel           -> the RATEL TURRET (bone `ratel`): a low hexagonal turret behind the canopy, six glowing
                         hex cells on its roof (the Dynamo's board), a domain rim - the second seat
  long pointed wings  -> swept wings (bones `wing.L` / `wing.R`) with a sunk domain panel and three overlapping
                         primary plates at the tip; a lime nav light at each tip
  fanned tail, white
  outer feathers      -> the TAIL FAN (bone `tail`): five overlapping vanes on a hub, the two outer vanes domain
                         (the white outer feathers), the inner three dark
  legs / vent         -> a ventral keel (Body)
  (the ship)          -> two engine nacelles under the tail root with glowing mouths aft

Contract (FLEET_CONVENTION sections 1-4 for a NEW vessel):
  file      Assets/_Models/Vessel Models/Honeyguide.fbx (guid 4f1c2a9e7b3d4e5f8a6b0c1d2e3f4a5b), preset "zup"
  nodes     mesh `mesh`, armature `rig`; root bone `hull`
  bones     hull; beak; ratel; wing.L / wing.R; tail. Rigid skinning, one bone per part.
  assembly  a HOVERING ASSEMBLY (vessel-model section 0.5): beak, turret, wings and tail fan each float clear of the
            hull, 1.3-1.9% of the diagonal; gate `assembly_report.py Honeyguide.fbx --floating beak,ratel,wing.L,wing.R,tail`
  size      the Sparrow's drawn hull, which Honeyguide.prefab drew until now (SparrowModel1.fbx with its 0.2034 node
            scale: 12.50 wide, 12.23 long, 1.78 tall in Unity units, Docs/VesselModels/Sparrow.md 1.3), within
            10% in length and span; the height is the clade's (a stout body and the turret), declared in size_tol
  slots     0 Domain, 1 Body, 2 Window, 3 Engine
  shapes    Charge (weapon): the sensor beak juts into a lance and the turret's cells rise;
            Mass (volume):   the body, the turret and the nacelles swell;
            Space (reach):   the wings reach along their span, the tail fan lengthens;
            Time (speed):    the wings sweep back and the tail fan closes to a dart
  takes     none drive it (HoneyguideBody poses the bones by name); four documentation poses for the sheet:
            bankleft / bankright (the wings bank), flare (the wings rise to brake), fan (the tail lifts)
"""

import math
from _common import box_uv

NAME = "Honeyguide"
OUTPUT = "Assets/_Models/Vessel Models/Honeyguide.fbx"

ARMATURE = "rig"
MESH_NODE = "mesh"
SLOTS = ("Domain", "Body", "Window", "Engine")
D, H, W, J = 0, 1, 2, 3

# The layout, in the Blender frame: nose -Y, up +Z, pilot's left +X. The Sparrow's drawn hull spans
# x -6.25..6.25 and y -4.66 (nose) .. 7.57 (tail).
FACE_Y = -3.70                  # where the beak's collar meets the face
BEAK_GAP = 0.30                 # the beak floats this far ahead of the face collar
BEAK_TIP_Y = -4.62 - BEAK_GAP
TAIL_Y = 3.10                   # where the body ends and the tail hub begins
# a HOVERING ASSEMBLY (vessel-model section 0.5, Garrett 2026-10-21): every bone part floats clear of the hull and of
# each other, about 1.5-2% of the diagonal - the wings clear the hinge fairings, the beak the face collar, the turret
# the back, the tail fan the engine mouths
WING_ROOT = (1.55, -1.70, 0.12)
WING_SPAN, WING_DIHEDRAL, WING_SWEEP = 4.90, 4.0, 3.10
WING_CHORD_ROOT, WING_CHORD_TIP = 2.70, 0.60
TAIL_HUB = (0.0, 3.95, 0.18)    # the tail bone's head, clear of the engine mouths (3.55)
TAIL_LEN = 3.80
RATEL_C = (0.0, -0.55)          # the turret's centre (y) and ...
RATEL_R = 0.78                  # ... its hex radius
RATEL_LIFT = 0.36               # the turret hovers this far above the back
ENGINE_X, ENGINE_Z, ENGINE_Y = 0.52, -0.42, 3.55

# (y, half-width, half-height top, half-height bottom, centre lift)
BODY_STATIONS = [
    (FACE_Y, 0.42, 0.40, 0.30, 0.04),
    (-3.35, 0.70, 0.66, 0.50, 0.06),
    (-2.80, 0.92, 0.86, 0.64, 0.08),
    (-2.00, 1.08, 0.98, 0.74, 0.08),
    (-1.00, 1.14, 1.02, 0.78, 0.06),
    (0.10, 1.08, 0.96, 0.74, 0.04),
    (1.20, 0.90, 0.80, 0.64, 0.04),
    (2.20, 0.64, 0.58, 0.48, 0.08),
    (TAIL_Y, 0.40, 0.36, 0.30, 0.14),
]
N_TOP, N_BOT = 2.2, 2.5


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
        """Per-vertex part id for the morph masks."""
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
        """Join sub-pieces into one part named after the bone that skins it."""
        if len(objs) == 1:
            o = objs[0]
            o.name = name
        else:
            o = f.join(objs, name, SLOTS)
        for vg in list(o.vertex_groups):
            o.vertex_groups.remove(vg)
        f.tag_group(o, name)
        return o

    hull_parts = []

    # ------------------------------------------------------------------ THE HULL (stout body -> gunmetal fuselage)
    length = TAIL_Y - FACE_Y
    st = [((y - FACE_Y) / length, w, ht, hb, N_TOP, N_BOT, dz) for (y, w, ht, hb, dz) in BODY_STATIONS]
    body = f.fuselage("body", length, nose=(0, FACE_Y, 0.0), axis=(0, 1, 0), segments=32, stations=st)
    f.ensure_slots(body, SLOTS)
    f.paint(body, H)

    def band(c):
        """Height of a face centre as a fraction of the local half-height (0 at the waterline, 1 at the top)."""
        return (c.z - _lerp_station(c.y, 4)) / max(_lerp_station(c.y, 2), 1e-3)

    # the shoulder saddle: domain across the back either side of the turret, what the chase camera sees
    f.paint(body, D, lambda c, n, i: n.z > 0.35 and -2.35 < c.y < 1.35 and abs(c.x) > RATEL_R + 0.12
            and math.hypot(c.x, c.y - RATEL_C[1]) > RATEL_R + 0.18)
    # the chin band: the honeyguide's pale throat, as a domain band under the face
    f.paint(body, D, lambda c, n, i: -3.30 < c.y < -2.55 and band(c) < -0.15 and n.z < -0.2)
    # the flank stripe running back to the tail: the bird's streaked flank, a sunk domain line
    f.paint(body, D, lambda c, n, i: abs(n.x) > 0.6 and 0.25 < c.y < 2.35 and -0.05 < band(c) < 0.22)
    f.subsurf(body, 1)
    f.apply_modifiers(body)
    f.inset_faces(body, lambda c, n, i: i == D, 0.04, depth=0.025, slot=D)
    hull_parts.append(piece(body, None, 1))

    # the canopy (the eye): a glass bubble on the forehead in a dark frame - the Honeyguide's seat
    can_y = -2.55
    can = f.sphere("canopy", 1.0, center=(0, can_y, top_z(can_y) - 0.22), segments=24, rings=12, scale=(0.50, 1.05, 0.38))
    hull_parts.append(piece(can, W, 3))
    frame = f.sphere("canopyframe", 1.0, center=(0, can_y + 0.06, top_z(can_y) - 0.30), segments=24, rings=12,
                     scale=(0.60, 1.22, 0.38))
    hull_parts.append(piece(frame, H, 3))

    # the face collar the beak sits in
    collar = f.lathe("facecollar", [(0.0, -0.06), (0.36, -0.06), (0.44, 0.02), (0.44, 0.12), (0.36, 0.18), (0.0, 0.18)],
                     segments=20, axis=(0, 1, 0), origin=(0, FACE_Y - 0.04, 0.04))
    f.ensure_slots(collar, SLOTS)
    f.paint(collar, H)
    f.paint(collar, D, lambda c, n, i: abs(c.y - (FACE_Y + 0.03)) < 0.05)
    hull_parts.append(piece(collar, None, 2))

    # the ventral keel (the legs / vent): a low swept blade under the belly
    kl = f.wing("keel", root=(0, -1.20, bot_z(-1.2) + 0.16), span=0.55, chord_root=2.10, chord_tip=0.70, sweep=1.10,
                forward=(0, -1, 0), up=(1, 0, 0), thickness=0.12, sections=3, segments=12)
    hull_parts.append(piece(kl, H, 6, bevel=0.006))

    # the wing roots: hinge fairings on the flanks
    for sx in (1, -1):
        fr = f.sphere("fairing", 1.0, center=(sx * 0.97, WING_ROOT[1] + 1.15, WING_ROOT[2]),
                      segments=16, rings=8, scale=(0.26, 1.35, 0.26))
        hull_parts.append(piece(fr, H, 15))

    # the two engines under the tail root: nacelles with glowing mouths aft and a domain band
    for sx in (1, -1):
        prof = [(0.0, 0.0), (0.14, 0.08), (0.26, 0.45), (0.32, 1.05), (0.33, 1.55), (0.30, 1.78), (0.29, 1.82),
                (0.22, 1.82), (0.20, 1.70), (0.0, 1.70)]
        nac = f.lathe("nacelle", prof, segments=18, axis=(0, 1, 0), origin=(sx * ENGINE_X, ENGINE_Y - 1.82, ENGINE_Z))
        f.ensure_slots(nac, SLOTS)
        f.paint(nac, H)
        f.paint(nac, J, lambda c, n, i, sx=sx: c.y > ENGINE_Y - 0.16 and math.hypot(c.x - sx * ENGINE_X, c.z - ENGINE_Z) < 0.24)
        f.paint(nac, D, lambda c, n, i: ENGINE_Y - 0.62 < c.y < ENGINE_Y - 0.46)
        hull_parts.append(piece(nac, None, 8))
        # the pylon tying the nacelle to the belly
        py = f.box("pylon", (0.12, 1.10, 0.36), center=(sx * ENGINE_X * 0.8, ENGINE_Y - 0.95, ENGINE_Z + 0.26))
        hull_parts.append(piece(py, H, 8, bevel=0.01))

    hull = bone_part(hull_parts, "hull")
    parts.append(hull)

    # ------------------------------------------------------------------ THE BEAK (stubby beak -> sensor beak)
    bk = []
    beak_len = FACE_Y - BEAK_GAP - BEAK_TIP_Y
    beak = f.lathe("beak", [(0.0, 0.0), (0.07, 0.04), (0.18, 0.30), (0.30, 0.70), (0.36, beak_len - 0.05),
                            (0.0, beak_len - 0.05)], segments=8, axis=(0, 1, 0), origin=(0, BEAK_TIP_Y, 0.02))
    f.ensure_slots(beak, SLOTS)
    f.paint(beak, H)
    # the upper mandible's ridge in domain: a thin line along the top of the beak
    f.paint(beak, D, lambda c, n, i: n.z > 0.8 and c.y > BEAK_TIP_Y + 0.25)
    bk.append(piece(beak, None, 20))
    tip = f.sphere("sensortip", 0.075, center=(0, BEAK_TIP_Y + 0.04, 0.02), segments=10, rings=6)
    bk.append(piece(tip, J, 20))
    parts.append(bone_part(bk, "beak"))

    # ------------------------------------------------------------------ THE RATEL (the second seat -> hex turret)
    rp = []
    rc = Vector((0.0, RATEL_C[1], top_z(RATEL_C[1]) + RATEL_LIFT))
    hexo = [(RATEL_R * math.cos(math.radians(30 + 60 * k)), RATEL_R * math.sin(math.radians(30 + 60 * k))) for k in range(6)]
    base = f.plate("ratelbase", hexo, 0.42, normal=(0, 0, 1), origin=tuple(rc + Vector((0, 0, 0.12))), right=(1, 0, 0))
    f.ensure_slots(base, SLOTS)
    f.paint(base, H)
    f.paint(base, D, lambda c, n, i: abs(n.z) < 0.5)            # the domain rim: the turret's six walls
    f.bevel(base, width=0.03, segments=1, angle_deg=30)
    f.apply_modifiers(base)
    rp.append(piece(base, None, 30))
    roof_z = rc.z + 0.12 + 0.21
    # the Dynamo's board on the roof: six hex cells round a centre cell, each a glowing Engine lens in a dark socket
    cells = [(0.0, 0.0)] + [(0.40 * math.cos(math.radians(60 * k)), 0.40 * math.sin(math.radians(60 * k))) for k in range(6)]
    for (cx, cy) in cells:
        cell = [(cx + 0.17 * math.cos(math.radians(30 + 60 * k)), cy + 0.17 * math.sin(math.radians(30 + 60 * k))) for k in range(6)]
        lens = f.plate("ratelcell", cell, 0.06, normal=(0, 0, 1), origin=(0, rc.y, roof_z + 0.02), right=(1, 0, 0))
        rp.append(piece(lens, J, 31))
    # a low glass hood over the board's back half (the Ratel's own window)
    hood = f.sphere("ratelhood", 1.0, center=(0, rc.y + 0.32, roof_z), segments=16, rings=8, scale=(0.44, 0.30, 0.20))
    rp.append(piece(hood, W, 32))
    parts.append(bone_part(rp, "ratel"))

    # ------------------------------------------------------------------ WINGS (long pointed wings -> swept wings)
    span_dir, tip_le = {}, {}
    for tag_, sx in (("L", 1), ("R", -1)):
        root = Vector((sx * WING_ROOT[0], WING_ROOT[1], WING_ROOT[2]))
        wg = f.wing("wingskin", root=tuple(root), span=WING_SPAN, chord_root=WING_CHORD_ROOT, chord_tip=WING_CHORD_TIP,
                    sweep=WING_SWEEP, dihedral_deg=WING_DIHEDRAL, thickness=0.10, camber=0.02, sections=7, segments=20,
                    side=sx, tip_round=True, taper_power=0.9)
        f.ensure_slots(wg, SLOTS)
        f.paint(wg, H)
        cosd = math.cos(math.radians(WING_DIHEDRAL))

        def wing_panel(c, n, i, sx=sx, root=root, cosd=cosd):
            t = (sx * c.x - WING_ROOT[0]) / (WING_SPAN * cosd)
            if not (0.10 < t < 0.62) or n.z < 0.2:
                return False
            le = root.y + WING_SWEEP * (t ** 0.9)
            frac = (c.y - le) / (WING_CHORD_ROOT + (WING_CHORD_TIP - WING_CHORD_ROOT) * (t ** 0.9))
            return 0.12 < frac < 0.70
        f.inset_faces(wg, wing_panel, 0.04, depth=0.02, slot=D)
        wparts = [piece(wg, None, 7)]
        d = math.radians(WING_DIHEDRAL)
        sd = Vector((sx * math.cos(d), 0.0, math.sin(d)))
        span_dir[sx] = sd
        tip_le[sx] = root + sd * WING_SPAN + Vector((0, WING_SWEEP, 0))
        # the primaries: three overlapping plates along the outer trailing edge, longest outboard (the pointed tip)
        for k, (t, ln, wd) in enumerate(((0.52, 1.05, 0.70), (0.66, 1.15, 0.62), (0.80, 1.25, 0.52))):
            le = root + sd * (WING_SPAN * t) + Vector((0, WING_SWEEP * (t ** 0.9), 0))
            chord = WING_CHORD_ROOT + (WING_CHORD_TIP - WING_CHORD_ROOT) * (t ** 0.9)
            te = le + Vector((0, chord * 0.45, 0.012 * (k + 1)))
            outline = [(0.0, -wd / 2), (ln * 0.75, -wd / 2), (ln, 0.0), (ln * 0.75, wd / 2), (0.0, wd / 2)]
            pl = f.plate("primary", list(reversed(outline)), 0.05, normal=(0, 0, 1), origin=tuple(te),
                         right=tuple(Vector((sx * 0.75, 1.0, 0.0)).normalized()))
            pl = piece(pl, D if k == 2 else H, 9, bevel=0.006)
            wparts.append(pl)
        light = f.sphere("navlight", 0.07, center=tuple(tip_le[sx] + Vector((sx * 0.02, 0.10, 0.0))), segments=10, rings=6,
                         scale=(1.0, 2.0, 1.0))
        wparts.append(piece(light, J, 17))
        parts.append(bone_part(wparts, "wing." + tag_))

    # ------------------------------------------------------------------ THE TAIL FAN (fanned tail -> five vanes)
    tp = []
    hub = f.lathe("tailhub", [(0.0, -0.10), (0.36, -0.10), (0.42, 0.20), (0.34, 0.62), (0.0, 0.62)], segments=18,
                  axis=(0, 1, 0), origin=TAIL_HUB)
    f.ensure_slots(hub, SLOTS)
    f.paint(hub, H)
    f.paint(hub, D, lambda c, n, i: abs(c.y - TAIL_HUB[1] - 0.18) < 0.06)
    tp.append(piece(hub, None, 10))
    # broad vanes whose chords overlap their spacing, so the five read as ONE fanned tail, not a fishbone
    for k, ang in enumerate((-20, -10, 0, 10, 20)):
        outer = abs(ang) == 20
        ln = TAIL_LEN * (0.97 if outer else 1.0 - 0.04 * abs(ang) / 10)
        wd = 1.05
        # a vane: rounded at the tip, root tucked inside the hub; laid flat (normal +Z), splayed by `ang` about Z
        outline = [(0.0, -wd * 0.32), (ln * 0.70, -wd / 2), (ln * 0.93, -wd * 0.36), (ln, 0.0), (ln * 0.93, wd * 0.36),
                   (ln * 0.70, wd / 2), (0.0, wd * 0.32)]
        a = math.radians(ang)
        dirv = Vector((math.sin(a), math.cos(a), 0.0))
        vane = f.plate("vane", list(reversed(outline)), 0.07, normal=(0, 0, 1),
                       origin=(TAIL_HUB[0], TAIL_HUB[1] + 0.25, TAIL_HUB[2] + 0.015 * (2 - abs(k - 2))), right=tuple(dirv))
        f.ensure_slots(vane, SLOTS)
        f.paint(vane, D if outer else H)
        if not outer:
            # a domain tip on the dark vanes, so the fan's edge reads as one pale crescent from behind
            f.paint(vane, D, lambda c, n, i, dirv=dirv, ln=ln: (c - Vector(TAIL_HUB)).dot(dirv) > ln * 0.86)
        f.bevel(vane, width=0.012, segments=1, angle_deg=30)
        f.apply_modifiers(vane)
        tp.append(piece(vane, None, 11))
    parts.append(bone_part(tp, "tail"))

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
    f.sharpen_by_angle(mesh, 38)
    box_uv(mesh)
    ctx.log("honeyguide joined: %d verts, %d tris (n-gons triangulated: %d)" % (len(mesh.data.vertices), f.tri_count(mesh), len(big)))

    # ------------------------------------------------------------------ RIG
    B = rig.BoneSpec
    bones = [B("hull", (0, 0, 0), (0, -1.6, 0))]
    bones.append(B("beak", (0, FACE_Y - BEAK_GAP, 0.02), (0, BEAK_TIP_Y, 0.02), parent="hull"))
    bones.append(B("ratel", tuple(rc), tuple(rc + Vector((0, 0, 0.6))), parent="hull"))
    for tag_, sx in (("L", 1), ("R", -1)):
        h = Vector((sx * WING_ROOT[0], WING_ROOT[1] + 0.9, WING_ROOT[2]))
        bones.append(B("wing." + tag_, h, h + span_dir[sx] * WING_SPAN, parent="hull"))
    bones.append(B("tail", TAIL_HUB, (0, TAIL_HUB[1] + TAIL_LEN, TAIL_HUB[2]), parent="hull"))
    arm = rig.build_armature(ARMATURE, bones)
    rig.skin_rigid(mesh, arm, default_bone="hull")
    bone_names = [b.name for b in arm.data.bones]

    # ------------------------------------------------------------------ MORPHS (4-12% of the ~17.5 diagonal)
    me = mesh.data
    nv = len(me.vertices)
    pid = [0.0] * nv
    me.attributes["pid"].data.foreach_get("value", pid)

    def pmask(*ids):
        s = set(float(i) for i in ids)
        return [1.0 if p in s else 0.0 for p in pid]
    ms = morphs.MaskSet(mesh)
    wing = {1: ms.mask("wing.L"), -1: ms.mask("wing.R")}
    beakm = ms.mask("beak")
    ratel = ms.mask("ratel")
    cellm = pmask(31)
    hullm = ms.mask("hull")
    nacm = pmask(8)
    tail = ms.mask("tail")
    mid_z = 0.06

    def about_axis(factor, mask, z0=mid_z):
        def fn(i, co, n):
            if mask[i] <= 0:
                return Vector((0, 0, 0))
            return Vector((co.x * (factor - 1), 0, (co.z - z0) * (factor - 1))) * mask[i]
        return fn

    shapes = {
        "Charge": [  # the weapon: the sensor beak juts into a lance, the turret's cells rise
            morphs.stretch_along((0, -1, 0), (0, FACE_Y - BEAK_GAP, 0.02), 2.6, beakm),
            morphs.translate((0, 0, 0.42), cellm),
        ],
        "Mass": [  # volume: the body, the turret and the nacelles swell
            about_axis(1.38, [h * (1 - c) for h, c in zip(hullm, nacm)]),
            morphs.scale_about(1.45, tuple(rc), ratel),
            morphs.translate((0, 0, 0.62), ratel),
            *[morphs.scale_about(1.45, (sx * ENGINE_X, ENGINE_Y - 0.9, ENGINE_Z), [m * (1 if (sx > 0) == (x > 0) else 0)
                                  for m, x in zip(nacm, [v.co.x for v in me.vertices])], axes=(1, 1, 1)) for sx in (1, -1)],
            morphs.translate((0.42, 0, 0), wing[1]), morphs.translate((-0.42, 0, 0), wing[-1]),
        ],
        "Space": [  # reach: the wings reach along their span, the tail fan lengthens
            *[morphs.stretch_along(tuple(span_dir[sx]), (sx * WING_ROOT[0], WING_ROOT[1], WING_ROOT[2]), 1.28, wing[sx],
                                   ramp=(0.1, 0.5)) for sx in (1, -1)],
            morphs.stretch_along((0, 1, 0), TAIL_HUB, 1.30, tail),
        ],
        "Time": [  # speed: the wings sweep back, the tail fan closes to a dart
            *[morphs.sweep((sx, 0, 0), (sx * WING_ROOT[0], 0, 0), (0, 0.42, 0), wing[sx]) for sx in (1, -1)],
            morphs.scale_about(0.45, TAIL_HUB, tail, axes=(1.0, -0.218, 0.0)),   # x0.45 across, x1.12 long
        ],
    }
    for name in ("Charge", "Mass", "Space", "Time"):
        kb, mx, frac, moved = morphs.add_shape(mesh, name, shapes[name])
        ctx.log("shape %s: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))
    if "pid" in me.attributes:
        me.attributes.remove(me.attributes["pid"])

    # ------------------------------------------------------------------ DOCUMENTATION POSES (code drives the bones)
    # World axes of the Blender frame: +X pitch (a point ahead of the hinge swings DOWN, behind it UP), +Z yaw,
    # +Y turns the +X (left) wing's tip DOWN.
    poses = {
        "bankleft": {"wing.L": (0, 22, 0), "wing.R": (0, 22, 0)},
        "bankright": {"wing.L": (0, -22, 0), "wing.R": (0, -22, 0)},
        "flare": {"wing.L": (0, -30, 0), "wing.R": (0, 30, 0)},
        "fan": {"tail": (16, 0, 0)},
    }
    names = ("bankleft", "bankright", "flare", "fan")
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
                "bankleft": [("wing.L", "z", -1, 0.5), ("wing.R", "z", +1, 0.5)],
                "bankright": [("wing.L", "z", +1, 0.5), ("wing.R", "z", -1, 0.5)],
                "flare": [("wing.L", "z", +1, 0.5), ("wing.R", "z", +1, 0.5)],
                "fan": [("tail", "z", +1, 0.3)],
            },
            "shapes": ["Charge", "Mass", "Space", "Time"],
            "slots": list(SLOTS),
            # the Sparrow as Honeyguide.prefab drew it (Blender frame of SparrowModel1.fbx, its 0.2034 node scale
            # applied): length and span held to 10%; the height is the clade's stout body and the turret, declared
            "size_ref_blender": ((-6.250, -4.664, -0.818), (6.250, 7.565, 0.958)),
            "size_tol": (0.10, 0.10, 1.6),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.04,
        },
    }
