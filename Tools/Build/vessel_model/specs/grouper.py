"""
grouper - a NEW hull with no artist file: the Wrasse's carrier, the deep-bodied ship the Wrasse Run
pilots (Assets/_Scripts/Controller/Arcade/WRASSERUN.md), drawn today by the Sparrow's model nested in
Wrasse.prefab. Built to the fleet convention and the fleet's look (Garrett, 2026-10-18: "don't forget they
are spaceships inspired by earth clades"; 2026-10-20: "give the wrasse and grouper proper 3d vessels with
animations").

THE READ, from the chase camera (the Sparrow's followOffset z -50, about 19 px per unit at 1080p, so the
12-unit hull is ~230 px long): a big-mouthed, deep-bodied reef predator - a heavy armoured hull with a ram
scoop for a mouth, a sensor crest standing along its back, two stubby thruster wings and a rounded vectoring
vane for a tail.

Clade -> ship (every part answers "what is this on the ship?"):
  deep body           -> a tall, laterally pressed gunmetal hull; two sunk domain plates per flank split by a
                         dark lateral seam (the lateral line), domain saddles either side of the crest rail
  huge mouth          -> the RAM SCOOP: an oval intake ring on the face with a glowing throat, over a hinged
                         lower mandible (bone `jaw`) that juts past the lip (the grouper's underbite) with a
                         domain lip trim
  eye                 -> the canopy: a glass bubble on the forehead in a dark frame, and a sensor eye in a bezel
                         high on each cheek
  gill cover          -> an armoured operculum plate per side with four glowing vent louvres behind it
  spiny dorsal        -> the SENSOR / HEAT-SINK CREST (bone `dorsal`): a rail on the spine carrying nine domain
                         fins with dark leading spars, tallest at the front, and a soft rear vane
  pectoral fins       -> stubby wings (bones `fin.L` / `fin.R`) with sunk domain panels and a THRUSTER POD at
                         each tip (Engine mouth aft, domain band, lime nav light)
  pelvic / anal fins  -> two ventral skegs and an anal stabiliser (Body)
  caudal peduncle     -> the tail boom (bone `tail.1`) behind a joint collar, flanked by the two main engines
  rounded caudal fin  -> the VECTORING VANE (bone `tail.2`, child of tail.1): a fan with a sunk domain panel
                         and two vector nozzles at its root

Contract (FLEET_CONVENTION sections 1-4 for a NEW vessel):
  file      Assets/_Models/Vessel Models/Grouper.fbx (guid e8c932601ef7e35b1f3017557298424c), preset "zup"
  nodes     mesh `mesh`, armature `rig`; root bone `hull`
  bones     hull; jaw; fin.L / fin.R; dorsal; tail.1 -> tail.2. Rigid skinning, one bone per part.
  size      the Sparrow's drawn hull, which Wrasse.prefab draws today (SparrowModel1.fbx with its 0.2034 node
            scale: 12.50 wide, 12.23 long, 1.78 tall in Unity units, Docs/VesselModels/Sparrow.md 1.3), within
            10% in length and span; the height is the clade's (a deep body), declared in size_tol
  slots     0 Domain, 1 Body, 2 Window, 3 Engine
  shapes    Charge (weapon): the crest stands tall and the ram scoop juts and drops open;
            Mass (volume):   the hull, the thruster pods and the jaw swell;
            Space (reach):   the wings reach along their span, the pods ride out, the tail boom telescopes;
            Time (speed):    the wings sweep back, the crest and the vane rake back, the vane spreads
  takes     none drive it yet (code poses the bones by name); four documentation poses for the sheet and the
            studio: gape (the jaw drops open), beatleft / beatright (the tail vectors), flare (the wings brake)
"""

import math
from _common import box_uv

NAME = "Grouper"
OUTPUT = "Assets/_Models/Vessel Models/Grouper.fbx"

ARMATURE = "rig"
MESH_NODE = "mesh"
SLOTS = ("Domain", "Body", "Window", "Engine")
D, H, W, J = 0, 1, 2, 3

# The layout, in the Blender frame: nose -Y, up +Z, pilot's left +X.
FACE_Y = -5.90                 # the upper lip / the scoop's face
TAIL_Y = 2.50                  # where the body ends and the boom begins
JAW_HINGE = (0.0, -4.30, -0.62)
FIN_ROOT = (1.50, -2.05, -0.55)
FIN_SPAN, FIN_DIHEDRAL, FIN_SWEEP = 3.65, 6.0, 1.05
POD_R, POD_LEN = 0.50, 2.70
BOOM = (0.0, 2.30, 0.10)        # tail.1 head (the peduncle hinge)
VANE = (0.0, 4.05, 0.10)        # tail.2 head (the vane's hinge)
CREST_Y0, CREST_Y1 = -3.20, 0.90
ENGINE_X, ENGINE_Z, ENGINE_Y = 0.78, -0.30, 2.62

# (y, half-width, half-height top, half-height bottom, centre lift)
BODY_STATIONS = [
    (FACE_Y, 0.62, 0.52, 0.40, -0.06),
    (-5.72, 0.98, 0.86, 0.62, 0.00),
    (-5.30, 1.27, 1.20, 0.88, 0.05),
    (-4.70, 1.52, 1.55, 1.18, 0.10),
    (-3.90, 1.71, 1.88, 1.46, 0.12),
    (-2.90, 1.81, 2.06, 1.64, 0.12),
    (-1.80, 1.83, 2.10, 1.70, 0.10),
    (-0.60, 1.73, 2.00, 1.62, 0.08),
    (0.50, 1.50, 1.74, 1.40, 0.08),
    (1.40, 1.13, 1.33, 1.04, 0.09),
    (2.10, 0.78, 0.92, 0.72, 0.10),
    (TAIL_Y, 0.60, 0.70, 0.56, 0.10),
]
N_TOP, N_BOT = 2.3, 2.7


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

    # ------------------------------------------------------------------ THE HULL (deep body -> armoured hull)
    length = TAIL_Y - FACE_Y
    st = [((y - FACE_Y) / length, w, ht, hb, N_TOP, N_BOT, dz) for (y, w, ht, hb, dz) in BODY_STATIONS]
    body = f.fuselage("body", length, nose=(0, FACE_Y, 0.0), axis=(0, 1, 0), segments=32, stations=st)
    f.ensure_slots(body, SLOTS)
    f.paint(body, H)

    def flank_band(c):
        """Height of a face centre as a fraction of the local half-height (0 at the waterline, 1 at the top)."""
        mid = _lerp_station(c.y, 4)
        return (c.z - mid) / max(_lerp_station(c.y, 2), 1e-3)

    # two domain plates per flank, split by a dark lateral seam (the lateral line) and a dark frame
    fore = lambda c: -3.55 < c.y < -1.25
    aft = lambda c: -0.85 < c.y < 1.45
    f.paint(body, D, lambda c, n, i: abs(n.x) > 0.55 and (fore(c) or aft(c)) and -0.35 < flank_band(c) < 0.62)
    # the crest saddles: either side of the dark spine rail, what the chase camera sees of the back
    f.paint(body, D, lambda c, n, i: n.z > 0.5 and 0.38 < abs(c.x) < 1.15 and CREST_Y0 + 0.2 < c.y < CREST_Y1 - 0.1)
    f.subsurf(body, 1)
    f.apply_modifiers(body)
    # sink every domain plate a hair into the hull inside a dark frame
    f.inset_faces(body, lambda c, n, i: i == D, 0.05, depth=0.03, slot=D)
    hull_parts.append(piece(body, None, 1))

    # the RAM SCOOP on the face: a thick dark lip ring round a glowing, recessed throat
    sc_c = (0.0, FACE_Y + 0.02, -0.05)
    lip = f.lathe("scooplip", [(0.0, -0.32), (0.58, -0.32), (0.74, -0.18), (0.76, 0.05), (0.66, 0.30), (0.0, 0.30)],
                  segments=28, axis=(0, 1, 0), origin=sc_c)
    f.transform_mesh(lip, Matrix.Translation(Vector(sc_c)) @ Matrix.Diagonal((1.25, 1.0, 0.82, 1.0))
                     @ Matrix.Translation(-Vector(sc_c)))
    f.ensure_slots(lip, SLOTS)
    f.paint(lip, H)
    f.inset_faces(lip, lambda c, n, i: n.y < -0.85 and math.hypot(c.x / 1.25, (c.z + 0.05) / 0.82) < 0.52, 0.06, depth=0.24, slot=J)
    hull_parts.append(piece(lip, None, 2))
    # intake vanes across the throat (dark), so it reads as a scoop not a lamp
    for k in (-1, 0, 1):
        vn = f.box("scoopvane", (0.05, 0.10, 0.62), center=(k * 0.26, FACE_Y + 0.08, -0.05))
        hull_parts.append(piece(vn, H, 2, bevel=0.008))

    # the canopy (the eye): a glass bubble on the forehead in a dark frame
    can_y = -4.05
    can = f.sphere("canopy", 1.0, center=(0, can_y, top_z(can_y) - 0.30), segments=24, rings=12, scale=(0.62, 1.20, 0.46))
    hull_parts.append(piece(can, W, 3))
    frame = f.sphere("canopyframe", 1.0, center=(0, can_y + 0.05, top_z(can_y) - 0.40), segments=24, rings=12,
                     scale=(0.74, 1.40, 0.46))
    hull_parts.append(piece(frame, H, 3))
    # the cheek sensor eyes: cyan glass in a dark bezel, high on the head
    for sx in (1, -1):
        y0 = -4.75
        cx = sx * half_w(y0) * 0.80
        cz = _lerp_station(y0, 4) + _lerp_station(y0, 2) * 0.48
        axis = Vector((sx * 0.85, -0.2, 0.48)).normalized()
        bez = f.lathe("bezel", [(0.0, -0.08), (0.26, -0.08), (0.29, 0.0), (0.24, 0.05), (0.0, 0.05)], segments=18,
                      axis=tuple(axis), origin=(cx, y0, cz))
        hull_parts.append(piece(bez, H, 3))
        eye = f.sphere("eye", 0.19, center=tuple(Vector((cx, y0, cz)) + axis * 0.06), segments=16, rings=8)
        hull_parts.append(piece(eye, W, 3))

    # the gill cover -> an armoured operculum plate and four glowing vent louvres behind it
    for sx in (1, -1):
        y0 = -3.85
        hw = half_w(y0)
        op = f.sphere("operculum", 1.0, center=(sx * (hw * 0.86), y0, _lerp_station(y0, 4) - 0.15), segments=20, rings=10,
                      scale=(0.32, 0.70, 1.15))
        hull_parts.append(piece(op, H, 4, bevel=0.0))
        for k in range(4):
            y = -3.05 + k * 0.20
            gx = half_w(y) * 0.975
            lv = f.box("louvre", (0.10, 0.06, 1.10), center=(0, 0, 0))
            f.transform_mesh(lv, Matrix.Translation(Vector((sx * gx, y, _lerp_station(y, 4) - 0.10)))
                             @ Matrix.Rotation(math.radians(-10), 4, "X"))
            hull_parts.append(piece(lv, J, 4, bevel=0.01))

    # the crest rail on the spine (the crest itself is its own bone)
    rail_pts = []
    for k in range(9):
        y = CREST_Y0 - 0.25 + (CREST_Y1 + 1.2 - CREST_Y0) * k / 8
        rail_pts.append((0.0, y, top_z(y) - 0.10))
    rail = f.tube_along("rail", rail_pts, [0.30] * len(rail_pts), segments=10)
    hull_parts.append(piece(rail, H, 5, bevel=0.0))

    # pelvic fins -> two ventral skegs; the anal fin -> a ventral stabiliser
    for sx in (1, -1):
        sk = f.wing("skeg", root=(sx * 0.55, -1.75, bot_z(-1.75) + 0.20), span=0.95, chord_root=1.05, chord_tip=0.35,
                    sweep=0.65, dihedral_deg=-62, forward=(0, -1, 0), thickness=0.12, sections=3, segments=12, side=sx)
        hull_parts.append(piece(sk, H, 6, bevel=0.006))
    an = f.wing("anal", root=(0, 0.55, bot_z(0.55) + 0.18), span=0.80, chord_root=1.30, chord_tip=0.40, sweep=0.80,
                forward=(0, -1, 0), up=(1, 0, 0), thickness=0.11, sections=3, segments=12)
    f.ensure_slots(an, SLOTS)
    f.paint(an, H)
    f.paint(an, D, lambda c, n, i: c.z < bot_z(0.55) - 0.45)
    hull_parts.append(piece(an, None, 6, bevel=0.006))
    # the wing roots: hinge fairings on the flanks
    for sx in (1, -1):
        fr = f.sphere("fairing", 1.0, center=(sx * (FIN_ROOT[0] + 0.05), FIN_ROOT[1] + 0.95, FIN_ROOT[2] + 0.02),
                      segments=16, rings=8, scale=(0.30, 1.20, 0.30))
        hull_parts.append(piece(fr, H, 15))

    # the two main engines flank the peduncle: nacelles with glowing mouths aft
    for sx in (1, -1):
        prof = [(0.0, 0.0), (0.16, 0.10), (0.30, 0.55), (0.38, 1.20), (0.40, 1.80), (0.37, 2.05), (0.36, 2.10),
                (0.28, 2.10), (0.25, 1.95), (0.0, 1.95)]
        nac = f.lathe("nacelle", prof, segments=18, axis=(0, 1, 0), origin=(sx * ENGINE_X, ENGINE_Y - 2.10, ENGINE_Z))
        f.ensure_slots(nac, SLOTS)
        f.paint(nac, H)
        f.paint(nac, J, lambda c, n, i, sx=sx: c.y > ENGINE_Y - 0.20 and math.hypot(c.x - sx * ENGINE_X, c.z - ENGINE_Z) < 0.30)
        f.paint(nac, D, lambda c, n, i: ENGINE_Y - 0.75 < c.y < ENGINE_Y - 0.55)
        hull_parts.append(piece(nac, None, 8, sharp=35))

    hull = bone_part(hull_parts, "hull")
    parts.append(hull)

    # ------------------------------------------------------------------ THE JAW (lower mandible -> scoop mandible)
    jy0, jy1 = JAW_HINGE[1], -6.48
    jl = jy0 - jy1
    jst = [
        (0.0, 0.40, 0.14, 0.20, 2.4, 2.4, -0.62),
        (0.12, 0.92, 0.20, 0.32, 2.4, 2.6, -0.66),
        (0.45, 1.12, 0.22, 0.38, 2.4, 2.8, -0.78),
        (0.80, 1.00, 0.22, 0.32, 2.4, 2.8, -0.74),
        (0.95, 0.78, 0.16, 0.22, 2.4, 2.6, -0.58),
        (1.0, 0.40, 0.08, 0.10, 2.4, 2.4, -0.52),
    ]
    jaw = f.fuselage("mandible", jl, nose=(0, jy1, 0.0), axis=(0, 1, 0), segments=24,
                     stations=[(1.0 - t, w, ht, hb, nt, nb, dz) for (t, w, ht, hb, nt, nb, dz) in reversed(jst)])
    f.ensure_slots(jaw, SLOTS)
    f.paint(jaw, H)
    # the lip trim: a domain band round the jutting front of the mandible
    f.paint(jaw, D, lambda c, n, i: c.y < jy1 + 0.55 and c.z > -0.78)
    jaw = piece(jaw, None, 20, sharp=38)
    # a glowing intake slot under the lip
    js = f.box("jawslot", (1.00, 0.10, 0.08), center=(0, jy1 + 0.42, -0.50))
    js = piece(js, J, 20, bevel=0.01)
    parts.append(bone_part([jaw, js], "jaw"))

    # ------------------------------------------------------------------ THE CREST (spiny dorsal -> sensor / heat-sink crest)
    cp = []
    n_spines = 9
    spine_h = []
    for k in range(n_spines):
        y = CREST_Y0 + (CREST_Y1 - CREST_Y0) * k / (n_spines - 1)
        h = 1.35 - 0.55 * (k / (n_spines - 1)) ** 1.2
        spine_h.append((y, h))
        z0 = top_z(y) - 0.12
        # chords overlap the 0.51 spacing, so the nine fins read as one serrated crest, not a row of needles
        fin = f.wing("spine", root=(0, y - 0.10, z0), span=h, chord_root=0.66, chord_tip=0.16, sweep=0.30,
                     forward=(0, -1, 0), up=(-1, 0, 0), thickness=0.13, sections=3, segments=12)
        f.ensure_slots(fin, SLOTS)
        f.paint(fin, D)
        # the dark leading spar: the front third of the chord at every height
        def spar(c, n, i, y=y, z0=z0, h=h):
            t = max(0.0, min(1.0, (c.z - z0) / h))
            le = y - 0.10 + 0.30 * t
            return (c.y - le) / (0.66 + (0.16 - 0.66) * t) < 0.24
        f.paint(fin, H, spar)
        # the glowing sensor tip on every other spine
        cp.append(piece(fin, None, 21, bevel=0.006))
        if k % 2 == 0:
            tipc = (0.0, y - 0.10 + 0.30 + 0.03, z0 + h * 0.97)
            lt = f.sphere("crestlight", 0.065, center=tipc, segments=10, rings=6)
            cp.append(piece(lt, J, 21))
    # the soft rear vane: a low swept blade behind the spines
    ry = CREST_Y1 + 0.25
    soft = f.wing("softdorsal", root=(0, ry, top_z(ry) - 0.14), span=0.80, chord_root=1.55, chord_tip=0.50, sweep=0.95,
                  forward=(0, -1, 0), up=(-1, 0, 0), thickness=0.10, sections=3, segments=12)
    f.ensure_slots(soft, SLOTS)
    f.paint(soft, H)
    f.paint(soft, D, lambda c, n, i: c.z > top_z(ry) + 0.30)
    cp.append(piece(soft, None, 22, bevel=0.006))
    parts.append(bone_part(cp, "dorsal"))

    # ------------------------------------------------------------------ WINGS (pectoral fins -> stubby wings + thruster pods)
    span_dir, pod_c = {}, {}
    for tag_, sx in (("L", 1), ("R", -1)):
        root = Vector((sx * FIN_ROOT[0], FIN_ROOT[1], FIN_ROOT[2]))
        wg = f.wing("wingskin", root=tuple(root), span=FIN_SPAN, chord_root=2.25, chord_tip=1.45, sweep=FIN_SWEEP,
                    dihedral_deg=FIN_DIHEDRAL, thickness=0.12, camber=0.02, sections=6, segments=20, side=sx, tip_round=False)
        f.ensure_slots(wg, SLOTS)
        f.paint(wg, H)
        cosd = math.cos(math.radians(FIN_DIHEDRAL))

        def wing_panel(c, n, i, sx=sx, root=root, cosd=cosd):
            t = (sx * c.x - FIN_ROOT[0]) / (FIN_SPAN * cosd)
            if not (0.12 < t < 0.80) or n.z < 0.2:
                return False
            le = root.y + FIN_SWEEP * t
            frac = (c.y - le) / (2.25 + (1.45 - 2.25) * t)
            return 0.14 < frac < 0.78
        f.inset_faces(wg, wing_panel, 0.04, depth=0.02, slot=D)
        wg = piece(wg, None, 7, sharp=38)
        d = math.radians(FIN_DIHEDRAL)
        sd = Vector((sx * math.cos(d), 0.0, math.sin(d)))
        span_dir[sx] = sd
        tip_le = root + sd * FIN_SPAN + Vector((0, FIN_SWEEP, 0))
        pc = tip_le + Vector((sx * (POD_R * 0.55), -0.45, 0.02))
        pod_c[sx] = pc
        # the thruster pod: a nacelle along the wing tip, intake dark, a domain band, the mouth glowing aft
        y_front = pc.y
        prof = [(0.0, 0.0), (0.22, 0.10), (0.40, 0.40), (POD_R, 0.90), (POD_R, 2.05), (POD_R * 0.86, 2.55),
                (POD_R * 0.80, POD_LEN), (POD_R * 0.58, POD_LEN), (POD_R * 0.50, POD_LEN - 0.20), (0.0, POD_LEN - 0.20)]
        pod = f.lathe("pod", prof, segments=20, axis=(0, 1, 0), origin=(pc.x, y_front, pc.z))
        f.ensure_slots(pod, SLOTS)
        f.paint(pod, H)
        f.paint(pod, D, lambda c, n, i, y_front=y_front: y_front + 1.05 < c.y < y_front + 1.55)
        f.paint(pod, J, lambda c, n, i, y_front=y_front, pc=pc: c.y > y_front + POD_LEN - 0.25
                and math.hypot(c.x - pc.x, c.z - pc.z) < POD_R * 0.62)
        pod = piece(pod, None, 16, sharp=35)
        light = f.sphere("navlight", 0.09, center=(pc.x + sx * POD_R * 0.92, y_front + 0.95, pc.z), segments=10, rings=6,
                         scale=(1.0, 2.0, 1.0))
        light = piece(light, J, 17)
        parts.append(bone_part([wg, pod, light], "fin." + tag_))

    # ------------------------------------------------------------------ TAIL BOOM (peduncle -> tail.1)
    tb = []
    r0w, r0h = half_w(TAIL_Y), _lerp_station(TAIL_Y, 2)
    bl = VANE[1] - BOOM[1] + 0.15
    boom = f.fuselage("boom", bl, nose=(0, BOOM[1], BOOM[2]), axis=(0, 1, 0), segments=20,
                      stations=[(0.0, r0w * 1.04, r0h * 1.02, r0h * 0.98, 2.4, 2.4, 0.0),
                                (0.5, r0w * 0.78, r0h * 0.80, r0h * 0.76, 2.4, 2.4, 0.0),
                                (1.0, r0w * 0.58, r0h * 0.66, r0h * 0.62, 2.4, 2.4, 0.0)])
    tb.append(piece(boom, H, 9, sharp=40))
    collar = f.lathe("collar", [(0.0, -0.08), (0.80, -0.08), (0.86, -0.02), (0.86, 0.06), (0.78, 0.12), (0.0, 0.12)],
                     segments=20, axis=(0, 1, 0), origin=(0, TAIL_Y + 0.02, BOOM[2]))
    f.transform_mesh(collar, Matrix.Translation(Vector((0, 0, BOOM[2]))) @ Matrix.Diagonal((0.88, 1.0, 1.0, 1.0))
                     @ Matrix.Translation(Vector((0, 0, -BOOM[2]))))
    f.ensure_slots(collar, SLOTS)
    f.paint(collar, H)
    f.paint(collar, D, lambda c, n, i: abs(c.y - TAIL_Y - 0.04) < 0.05)
    tb.append(piece(collar, None, 9))
    parts.append(bone_part(tb, "tail.1"))

    # ------------------------------------------------------------------ VECTORING VANE (rounded caudal -> tail.2)
    vp = []
    cxv, czv, R = 0.40, 0.0, 1.55
    outline = [(0.0, -0.42), (0.0, 0.42), (0.40, 1.10)]
    for k in range(13):
        a = math.radians(58 - 112 * k / 12)
        outline.append((cxv + R * math.cos(a), czv + R * math.sin(a) * 1.05))
    outline.append((0.40, -1.05))
    # plate(): outline x along `right` (+Y), y along normal x right (+Z); reverse for counter-clockwise
    vane = f.plate("vane", list(reversed(outline)), 0.16, normal=(1, 0, 0), origin=(0, VANE[1] - 0.05, VANE[2]), right=(0, 1, 0))
    f.ensure_slots(vane, SLOTS)
    f.paint(vane, H)
    f.bevel(vane, width=0.03, segments=1, angle_deg=30)
    f.apply_modifiers(vane)
    f.inset_faces(vane, lambda c, n, i: abs(n.x) > 0.9, 0.16, depth=0.03, slot=D)
    vp.append(piece(vane, None, 10, sharp=40))
    # two vector nozzles at the vane's root, the boom's thrust turned by the vane
    hub = f.lathe("hub", [(0.0, -0.05), (0.42, -0.05), (0.50, 0.20), (0.44, 0.45), (0.0, 0.45)], segments=18,
                  axis=(0, 1, 0), origin=(0, VANE[1] - 0.18, VANE[2]))
    vp.append(piece(hub, H, 10))
    for (ox, oz) in ((0.0, 0.66), (0.0, -0.62)):
        nz = f.lathe("vnozzle", [(0.0, 0.0), (0.16, 0.0), (0.20, 0.30), (0.18, 0.42), (0.11, 0.42), (0.11, 0.36), (0.0, 0.36)],
                     segments=12, axis=(0, 1, 0), origin=(ox, VANE[1] - 0.05, VANE[2] + oz))
        f.ensure_slots(nz, SLOTS)
        f.paint(nz, H)
        f.paint(nz, J, lambda c, n, i: n.y > 0.8)
        vp.append(piece(nz, None, 10))
    parts.append(bone_part(vp, "tail.2"))

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
    ctx.log("grouper joined: %d verts, %d tris (n-gons triangulated: %d)" % (len(mesh.data.vertices), f.tri_count(mesh), len(big)))

    # ------------------------------------------------------------------ RIG
    B = rig.BoneSpec
    bones = [B("hull", (0, 0, 0), (0, -1.6, 0))]
    bones.append(B("jaw", JAW_HINGE, (0, -6.2, -0.60), parent="hull"))
    for tag_, sx in (("L", 1), ("R", -1)):
        h = Vector((sx * FIN_ROOT[0], FIN_ROOT[1], FIN_ROOT[2]))
        bones.append(B("fin." + tag_, h, h + span_dir[sx] * FIN_SPAN, parent="hull"))
    bones.append(B("dorsal", (0, CREST_Y0, top_z(CREST_Y0) - 0.12), (0, CREST_Y1 + 0.6, top_z(CREST_Y1) - 0.12), parent="hull"))
    bones.append(B("tail.1", BOOM, VANE, parent="hull"))
    bones.append(B("tail.2", VANE, (0, VANE[1] + 1.5, VANE[2]), parent="tail.1"))
    arm = rig.build_armature(ARMATURE, bones)
    rig.skin_rigid(mesh, arm, default_bone="hull")
    bone_names = [b.name for b in arm.data.bones]

    # ------------------------------------------------------------------ MORPHS (4-12% of the ~18 diagonal)
    me = mesh.data
    nv = len(me.vertices)
    pid = [0.0] * nv
    me.attributes["pid"].data.foreach_get("value", pid)

    def pmask(*ids):
        s = set(float(i) for i in ids)
        return [1.0 if p in s else 0.0 for p in pid]
    ms = morphs.MaskSet(mesh)
    fin = {1: ms.mask("fin.L"), -1: ms.mask("fin.R")}
    pods = {sx: [m * p for m, p in zip(fin[sx], pmask(16, 17))] for sx in (1, -1)}
    crest = ms.mask("dorsal")
    spines = pmask(21)
    jawm = ms.mask("jaw")
    hullm = ms.mask("hull")
    boom = ms.mask("tail.1")
    vane = ms.mask("tail.2")
    mid_z = 0.10

    def about_axis(factor, mask, z0=mid_z, cx=0.0):
        def fn(i, co, n):
            if mask[i] <= 0:
                return Vector((0, 0, 0))
            return Vector(((co.x - cx) * (factor - 1), 0, (co.z - z0) * (factor - 1))) * mask[i]
        return fn

    def raise_crest(factor):
        def fn(i, co, n):
            if spines[i] <= 0:
                return Vector((0, 0, 0))
            h = max(0.0, co.z - (top_z(co.y) - 0.12))
            return Vector((0, 0.20 * h * (factor - 1), h * (factor - 1)))
        return fn

    def pod_swell(factor, sx):
        c = pod_c[sx]
        def fn(i, co, n):
            if pods[sx][i] <= 0:
                return Vector((0, 0, 0))
            return Vector(((co.x - c.x) * (factor - 1), 0, (co.z - c.z) * (factor - 1)))
        return fn

    def jaw_jut(fwd, drop):
        def fn(i, co, n):
            if jawm[i] <= 0:
                return Vector((0, 0, 0))
            t = max(0.0, min(1.0, (JAW_HINGE[1] - co.y) / (JAW_HINGE[1] + 6.48)))
            return Vector((0, -fwd * t, -drop * t))
        return fn

    def tail_back(dist):
        return morphs.translate((0, dist, 0), [max(a, b) for a, b in zip(boom, vane)])

    shapes = {
        "Charge": [  # the weapon: the crest stands tall, the ram scoop juts and drops open
            raise_crest(1.60),
            jaw_jut(0.55, 0.40),
        ],
        "Mass": [  # volume: the hull, the pods and the jaw swell
            about_axis(1.32, hullm),
            about_axis(1.28, jawm),
            morphs.translate((0, 0, 0.55), crest),
            pod_swell(1.45, 1), pod_swell(1.45, -1),
            morphs.translate((0.50, 0, 0), fin[1]), morphs.translate((-0.50, 0, 0), fin[-1]),
            about_axis(1.25, boom),
        ],
        "Space": [  # reach: the wings reach along their span, the pods ride out, the tail boom telescopes
            *[morphs.stretch_along(tuple(span_dir[sx]), (sx * FIN_ROOT[0], FIN_ROOT[1], FIN_ROOT[2]), 1.30,
                                   [w * (1 - p) for w, p in zip(fin[sx], pods[sx])], ramp=(0.1, 0.5)) for sx in (1, -1)],
            *[morphs.translate(tuple(span_dir[sx] * (0.30 * FIN_SPAN)), pods[sx]) for sx in (1, -1)],
            morphs.stretch_along((0, 1, 0), BOOM, 1.45, boom),
            tail_back(0.45 * (VANE[1] - BOOM[1])),
        ],
        "Time": [  # speed: the wings sweep back, the crest and vane rake back, the vane spreads
            *[morphs.sweep((sx, 0, 0), (sx * FIN_ROOT[0], 0, 0), (0, 0.38, 0), fin[sx]) for sx in (1, -1)],
            morphs.sweep((0, 0, 1), (0, 0, top_z(-1.0) - 0.12), (0, 0.70, 0), crest),
            morphs.scale_about(1.30, VANE, vane, axes=(1, 1.15, 1.30)),
        ],
    }
    for name in ("Charge", "Mass", "Space", "Time"):
        kb, mx, frac, moved = morphs.add_shape(mesh, name, shapes[name])
        ctx.log("shape %s: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))
    if "pid" in me.attributes:
        me.attributes.remove(me.attributes["pid"])

    # ------------------------------------------------------------------ DOCUMENTATION POSES (code drives the bones)
    # World axes of the Blender frame: +X pitch (a point ahead of the hinge swings DOWN), +Z yaw (a point behind
    # the hull swings toward -X, the pilot's RIGHT), +Y turns the +X (left) wing's tip DOWN.
    poses = {
        "gape": {"jaw": (24, 0, 0)},
        "beatleft": {"tail.1": (0, 0, -12), "tail.2": (0, 0, -20)},
        "beatright": {"tail.1": (0, 0, 12), "tail.2": (0, 0, 20)},
        "flare": {"fin.L": (0, 35, 0), "fin.R": (0, -35, 0)},
    }
    names = ("gape", "beatleft", "beatright", "flare")
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
                "gape": [("jaw", "z", -1, 0.5)],
                "beatleft": [("tail.2", "x", +1, 0.5)],
                "beatright": [("tail.2", "x", -1, 0.5)],
                "flare": [("fin.L", "z", -1, 0.5), ("fin.R", "z", -1, 0.5)],
            },
            "shapes": ["Charge", "Mass", "Space", "Time"],
            "slots": list(SLOTS),
            # the Sparrow as Wrasse.prefab draws it (Blender frame of SparrowModel1.fbx, its 0.2034 node scale applied):
            # length and span held to 10%; the height is the clade's deep body (~3x the Sparrow's flat 1.78), declared
            "size_ref_blender": ((-6.250, -4.664, -0.818), (6.250, 7.565, 0.958)),
            "size_tol": (0.10, 0.10, 2.5),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.04,
        },
    }
