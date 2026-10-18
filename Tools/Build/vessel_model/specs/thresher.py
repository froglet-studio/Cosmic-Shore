"""
thresher - a hull with NO artist file: it replaces the procedural placeholder (ThresherHullBuilder /
ThresherHullForm: a faceted shark, two materials, built at Awake) with a model built to the fleet
convention and the fleet's look (Garrett, 2026-10-18: "don't forget they are spaceships inspired by
earth clades").

THE READ, from the chase camera (ThresherCameraSettingsSO followOffset (0, 8, -55): about 17 px per unit
at 1080p, so the 8.1-unit hull is ~140 px long and 60 px across): a sleek predator hull towing a heavy
ball, whose signature is the thresher shark's upper tail lobe - as long as the body, a whip - here a
three-jointed boom with a domain-coloured blade, beating side to side and cracking when the ball smashes.

Clade -> ship (every part answers "what is this on the ship?"):
  shark body          -> a lofted gunmetal predator hull, a dark spine between two sunk domain saddle plates
                         and a domain trim line along each flank (the shark's countershading boundary)
  snout               -> a pointed sensor ram with a cyan sensor tip
  big eye             -> sensor glass in a dark bezel, sunk into each cheek; a low cockpit canopy on the back
  gill slits          -> five glowing intake louvres per side (Engine slot)
  dorsal fin          -> the dorsal stabiliser blade (Domain) on the spine
  pectoral fins       -> swept, drooped wings on hinge fairings (bones wing.L / wing.R) with sunk domain
                         panels and lime nav lights at the tips
  pelvic fins         -> two engine nacelles hugging the rear flanks; their glowing mouths sit where the
                         prefab's two VesselJets already are (Unity (+-0.32, -0.08, -2.05) on ThresherHull)
  second dorsal / anal-> small stabilisers (Body)
  caudal peduncle     -> the tail boom root (bone `tail`) behind a mechanical joint collar
  lower caudal lobe   -> a short ventral skeg
  upper caudal lobe   -> the WHIP: three hinged boom segments (tail.2-4), a dark spar along the leading edge,
                         a domain blade trailing it, a knuckle at every joint, a lime light at the tip
  (the chain)         -> a tow shackle (bone `hitch`): a domain ring held by a clevis just past the WHIP'S TIP,
                         so the chain begins at the tip of the tail (Garrett, 2026-10-18: "the thresher cord should
                         begin at the tip of the tail"); it rides the whip's swing and every morph's tip

Contract (FLEET_CONVENTION sections 1-4 for a NEW vessel):
  file      Assets/_Models/Vessel Models/Thresher.fbx (new guid), preset "zup"
  nodes     mesh `mesh`, armature `rig`; root bone `hull`
  bones     hull; wing.L / wing.R (the placeholder's pectoral hinges, PectoralRoot at t=0.33);
            tail (peduncle hinge, PeduncleZ) -> tail.2 -> tail.3 -> tail.4 (the scythe's three segment hinges,
            LobePoint(k/3)); hitch (child of tail.4, just past LobePoint(1), the whip tip). Placeholder (Unity) ->
            Blender (-x, -z, y):
              wing.L   Unity (-0.4746, -0.2198, 0.748)   = Blender (+0.4746, -0.748, -0.2198)
              tail     Unity (0, 0, -2.2)                = Blender (0, 2.2, 0)
              tail.2   Unity (0, 0.02, -2.7) (the fork)  = Blender (0, 2.7, 0.02)
              (the placeholder's HitchCenter, Unity (0, -0.2516, -2.425), is no longer the tow point)
  size      the placeholder's within ~6%: 8.1 snout to whip tip, 3.5 across the wings (the 1.6 x 1.4 x 5.2
            body box, the 2.2 x 1.9 x 5.8 hitbox, the jets and the 55-unit camera were tuned to it)
  slots     0 Domain, 1 Body, 2 Window, 3 Engine
  shapes    Charge, Mass, Space, Time - the placeholder's meanings at the convention's 4-12% travel:
            Charge = the weapon: the dorsal blade stands tall and the whip's blade widens;
            Mass   = volume: the hull, nacelles and peduncle fatten and the tow shackle swells (Mass Mode);
            Space  = reach: the whip's last segment telescopes out and the wings reach along their span;
            Time   = rate: the wings sweep back and spread, the dorsal and the whip rake back
  takes     none drive it (ThresherAnimation poses the bones in code); four documentation poses for the
            sheet and the studio: beatleft, beatright (the swim), curl (planted, toward a ball on the right),
            flare (the wings brake down)
"""

import math
from _common import box_uv

NAME = "Thresher"
OUTPUT = "Assets/_Models/Vessel Models/Thresher.fbx"

ARMATURE = "rig"
MESH_NODE = "mesh"
SLOTS = ("Domain", "Body", "Window", "Engine")
D, H, W, J = 0, 1, 2, 3

# The placeholder's layout (ThresherHullForm.Defaults), in the Blender frame: nose -Y, up +Z, pilot's left +X.
BODY_NOSE_Y = -2.288          # NoseZ + 2% of the body length, the snout apex
PEDUNCLE_Y = 2.2              # PeduncleZ: the tail hinge
FORK = (0.0, 2.7, 0.02)       # ForkPoint: the scythe's root hinge
PECTORAL_ROOT = (0.4746, -0.748, -0.2198)
PECTORAL_SPAN, PECTORAL_DROOP, PECTORAL_SWEEP = 1.35, 18.0, 0.75
LOBE_LENGTH, LOBE_RISE, LOBE_CURL, LOBE_CHORD, LOBE_BLADE, LOBE_SEGMENTS = 3.6, 24.0, 22.0, 0.62, 0.4, 3
JET_X, JET_Z, JET_Y = 0.32, -0.08, 2.05   # the prefab's VesselJets (Unity (+-0.32, -0.08, -2.05))

# body girth (half-width) along Y; heights are a flattened ellipse-ish superellipse
BODY_STATIONS = [  # (y, half-width, dy)
    (BODY_NOSE_Y, 0.03, -0.05), (-2.20, 0.12, -0.045), (-2.05, 0.23, -0.035), (-1.85, 0.32, -0.025),
    (-1.60, 0.43, -0.015), (-1.32, 0.53, 0.0), (-1.00, 0.59, 0.0), (-0.66, 0.62, 0.0), (-0.20, 0.60, 0.0),
    (0.22, 0.55, 0.0), (0.66, 0.47, 0.0), (1.10, 0.38, 0.0), (1.50, 0.30, 0.0), (1.85, 0.24, 0.0),
    (PEDUNCLE_Y, 0.19, 0.0),
]
FLATTEN = 0.86


def lobe_dir(u):
    phi = math.radians(LOBE_RISE + LOBE_CURL * u * u)
    return (0.0, math.cos(phi), math.sin(phi))


def lobe_point(u):
    """ThresherHullForm.LobePoint in the Blender frame (the same 48-step integration)."""
    u = max(0.0, min(1.0, u))
    steps = 48
    p = list(FORK)
    du = 1.0 / steps
    for i in range(steps):
        a = i * du
        if a >= u:
            break
        step = min(du, u - a)
        d = lobe_dir(a + step * 0.5)
        for c in range(3):
            p[c] += d[c] * LOBE_LENGTH * step
    return tuple(p)


def lobe_chord_dir(u):
    """Perpendicular to the lobe in the YZ plane, toward the trailing (lower, rear) side."""
    d = lobe_dir(u)
    return (0.0, d[2], -d[1])


def lobe_chord(u):
    return LOBE_CHORD * (1.0 + (0.06 - 1.0) * (u ** 0.85))


def girth(y):
    st = BODY_STATIONS
    if y <= st[0][0]:
        return st[0][1]
    for (y0, w0, _), (y1, w1, _) in zip(st, st[1:]):
        if y <= y1:
            return w0 + (w1 - w0) * (y - y0) / (y1 - y0)
    return st[-1][1]


def build(ctx):
    import bpy
    import bmesh
    from mathutils import Vector, Matrix
    f, rig, morphs, takes = ctx.forms, ctx.rig, ctx.morphs, ctx.takes
    parts = []

    def tag(obj, pid, lu=None, lc=None):
        """Per-vertex part id (and, on the whip, its lobe parameter and chord fraction), for the morph masks."""
        me = obj.data
        a = me.attributes.get("pid") or me.attributes.new("pid", "FLOAT", "POINT")
        a.data.foreach_set("value", [float(pid)] * len(me.vertices))
        for nm, val in (("lu", lu), ("lc", lc)):
            b = me.attributes.get(nm) or me.attributes.new(nm, "FLOAT", "POINT")
            vals = val if isinstance(val, list) else [float(val or 0.0)] * len(me.vertices)
            b.data.foreach_set("value", vals)
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

    # ------------------------------------------------------------------ THE HULL (shark body -> predator hull)
    length = PEDUNCLE_Y - BODY_NOSE_Y
    st = []
    for (y, w, dy) in BODY_STATIONS:
        t = (y - BODY_NOSE_Y) / length
        st.append((t, w, w * FLATTEN, w * FLATTEN * 0.92, 2.6, 2.3, dy))
    body = f.fuselage("body", length, nose=(0, BODY_NOSE_Y, 0.0), axis=(0, 1, 0), segments=24, stations=st)
    f.ensure_slots(body, SLOTS)
    f.paint(body, H)
    stations_y = [y for (y, _, _) in BODY_STATIONS]

    def row_of(c):
        """Which of the loft's 24 face rows (15 degrees each, row 0 just above the +X equator, counter-clockwise
        seen from the nose) a face centre sits in: the superellipse parameter recovered from the centre."""
        w = max(girth(c.y), 1e-4)
        dy = 0.0
        for (y0, _, d0), (y1, _, d1) in zip(BODY_STATIONS, BODY_STATIONS[1:]):
            if y0 <= c.y <= y1:
                dy = d0 + (d1 - d0) * (c.y - y0) / (y1 - y0)
        top = c.z - dy >= 0
        h = w * FLATTEN * (1.0 if top else 0.92)
        n = 2.6 if top else 2.3
        ux, uz = c.x / w, (c.z - dy) / h
        a = math.atan2(math.copysign(abs(uz) ** (n / 2), uz), math.copysign(abs(ux) ** (n / 2), ux))
        return int(math.floor((math.degrees(a) % 360.0) / 15.0))
    # the flank trim line (the countershading boundary): the face row just above the widest line, snout to tail
    f.paint(body, D, lambda c, n, i: row_of(c) in (0, 11) and -1.86 < c.y)
    # two domain saddle plates either side of the dark spine (what the chase camera sees of the back)
    # (the station row 0.22..0.66 stays dark: a seam that splits each saddle into a fore and an aft plate)
    seam = lambda c: 0.22 < c.y < 0.66
    f.paint(body, D, lambda c, n, i: row_of(c) in (3, 8) and -0.66 < c.y < 1.85 and not seam(c))
    f.paint(body, D, lambda c, n, i: row_of(c) in (4, 7) and -1.32 < c.y < 1.5 and not seam(c))
    f.subsurf(body, 1)
    f.apply_modifiers(body)
    # sink the saddles: a dark frame round each and the plate pushed into the hull
    f.inset_faces(body, lambda c, n, i: i == D and n.z > 0.3, 0.022, depth=0.016, slot=D)
    hull_parts.append(piece(body, None, 1))

    # snout sensor tip
    tip = f.cone("sensor", 0.07, 0.16, axis=(0, -1, 0), origin=(0, BODY_NOSE_Y + 0.12, -0.05), segments=10)
    hull_parts.append(piece(tip, W, 2))

    # sensor eyes: cyan glass in a dark bezel, sunk into each cheek (the thresher's huge eye)
    for sx in (1, -1):
        y0 = -1.62
        gw = girth(y0)
        cx = sx * gw * 0.80
        cz = gw * FLATTEN * 0.40
        bez = f.lathe("bezel", [(0.0, -0.05), (0.17, -0.05), (0.19, 0.0), (0.16, 0.035), (0.0, 0.035)], segments=16,
                      axis=(sx * 0.82, 0.0, 0.57), origin=(cx, y0, cz))
        f.transform_mesh(bez, Matrix.Translation(Vector((cx, y0, cz))) @ Matrix.Diagonal((1.0, 1.25, 1.0, 1.0))
                         @ Matrix.Translation(Vector((-cx, -y0, -cz))))
        hull_parts.append(piece(bez, H, 2))
        eye = f.sphere("eye", 0.135, center=(cx + sx * 0.035, y0, cz + 0.025), segments=14, rings=8, scale=(0.7, 1.35, 0.9))
        hull_parts.append(piece(eye, W, 2))

    # cockpit canopy: a long low glass teardrop on the back, behind the eyes, ahead of the dorsal
    can = f.sphere("canopy", 1.0, center=(0, -1.05, girth(-1.05) * FLATTEN * 0.86), segments=18, rings=10,
                   scale=(0.20, 0.55, 0.17))
    hull_parts.append(piece(can, W, 3))
    frame = f.sphere("canopyframe", 1.0, center=(0, -1.02, girth(-1.02) * FLATTEN * 0.80), segments=18, rings=10,
                     scale=(0.25, 0.64, 0.18))
    hull_parts.append(piece(frame, H, 3))

    # gill slits -> five glowing intake louvres per side, raked back, half sunk into the flank
    for sx in (1, -1):
        for k in range(5):
            y = -1.30 + k * 0.115
            gx = girth(y) * 0.985
            lv = f.box("louvre", (0.07, 0.035, 0.40), center=(0, 0, 0))
            f.transform_mesh(lv, Matrix.Translation(Vector((sx * gx, y, 0.02))) @ Matrix.Rotation(math.radians(-14), 4, "X"))
            hull_parts.append(piece(lv, J, 4, bevel=0.008))
        housing = f.box("gillplate", (0.03, 0.64, 0.48), center=(0, 0, 0))
        f.transform_mesh(housing, Matrix.Translation(Vector((sx * (girth(-1.07) * 0.955), -1.07, 0.02))))
        hull_parts.append(piece(housing, H, 4, bevel=0.012))

    # the shark's mouth -> a chin intake under the snout: a dark scoop with a glowing throat
    chin = f.box("chin", (0.46, 0.30, 0.10), center=(0, -1.62, -girth(-1.62) * FLATTEN * 0.92 + 0.03))
    hull_parts.append(piece(chin, H, 2, bevel=0.012))
    throat = f.box("throat", (0.36, 0.06, 0.05), center=(0, -1.775, -girth(-1.62) * FLATTEN * 0.92 + 0.02))
    hull_parts.append(piece(throat, J, 2))

    # the dorsal stabiliser: a swept blade standing on the spine (domain), its root sunk in the back
    top = girth(-0.88) * FLATTEN * 0.88
    dorsal = f.wing("dorsal", root=(0, -0.88, top - 0.06), span=0.98, chord_root=1.05, chord_tip=0.22, sweep=0.98,
                    forward=(0, -1, 0), up=(-1, 0, 0), thickness=0.10, sections=5, segments=14, tip_round=True)
    f.ensure_slots(dorsal, SLOTS)
    f.paint(dorsal, D)
    # a dark leading-edge spar: the front fifth of the chord at every height (by chord fraction, so the edge is clean)
    def dorsal_spar(c, n, i):
        t = max(0.0, min(1.0, (c.z - (top - 0.06)) / 0.98))
        le = -0.88 + 0.98 * t
        return (c.y - le) / (1.05 + (0.22 - 1.05) * t) < 0.2
    f.paint(dorsal, H, dorsal_spar)
    hull_parts.append(piece(dorsal, None, 5, bevel=0.006))

    # second dorsal and anal stabilisers (Body)
    d2 = f.wing("dorsal2", root=(0, 1.32, girth(1.32) * FLATTEN * 0.85 - 0.04), span=0.26, chord_root=0.34, chord_tip=0.1,
                sweep=0.22, forward=(0, -1, 0), up=(-1, 0, 0), thickness=0.12, sections=2, segments=10)
    hull_parts.append(piece(d2, H, 6, bevel=0.004))
    an = f.wing("anal", root=(0, 1.40, -girth(1.40) * FLATTEN * 0.80 + 0.04), span=0.22, chord_root=0.30, chord_tip=0.1,
                sweep=0.2, forward=(0, -1, 0), up=(1, 0, 0), thickness=0.12, sections=2, segments=10)
    hull_parts.append(piece(an, H, 6, bevel=0.004))

    # wing hinge fairings on the flanks (the wings grow out of them)
    for sx in (1, -1):
        fr = f.sphere("fairing", 1.0, center=(sx * (PECTORAL_ROOT[0] + 0.02), PECTORAL_ROOT[1] + 0.05, PECTORAL_ROOT[2] + 0.02),
                      segments=14, rings=8, scale=(0.20, 0.50, 0.17))
        hull_parts.append(piece(fr, H, 15))

    # pelvic fins -> engine nacelles hugging the rear flanks; the mouths at the prefab's jets
    for sx in (1, -1):
        prof = [(0.0, 0.0), (0.07, 0.04), (0.13, 0.22), (0.165, 0.50), (0.17, 0.72), (0.155, 0.82), (0.150, 0.84),
                (0.118, 0.84), (0.105, 0.76), (0.0, 0.76)]
        nac = f.lathe("nacelle", prof, segments=16, axis=(0, 1, 0), origin=(sx * JET_X, JET_Y - 0.84, JET_Z))
        f.ensure_slots(nac, SLOTS)
        f.paint(nac, H)
        f.paint(nac, J, lambda c, n, i, sx=sx: c.y > JET_Y - 0.11 and math.hypot(c.x - sx * JET_X, c.z - JET_Z) < 0.125)
        f.paint(nac, D, lambda c, n, i: JET_Y - 0.84 + 0.30 < c.y < JET_Y - 0.84 + 0.40)
        hull_parts.append(piece(nac, None, 8, sharp=35))
        # the pelvic pylon under each nacelle
        py = f.wing("pylon", root=(sx * (JET_X + 0.02), JET_Y - 0.55, JET_Z - 0.10), span=0.26, chord_root=0.40, chord_tip=0.16,
                    sweep=0.18, dihedral_deg=-55, forward=(0, -1, 0), thickness=0.12, sections=2, segments=10, side=sx)
        hull_parts.append(piece(py, H, 8, bevel=0.004))

    hull = bone_part(hull_parts, "hull")
    parts.append(hull)

    # ------------------------------------------------------------------ WINGS (pectoral fins -> swept wings)
    span_dir = {}
    for tag_, sx in (("L", 1), ("R", -1)):
        root = Vector((sx * PECTORAL_ROOT[0], PECTORAL_ROOT[1] - 0.315, PECTORAL_ROOT[2]))
        wg = f.wing("wingskin", root=tuple(root), span=PECTORAL_SPAN, chord_root=0.95, chord_tip=0.30,
                    sweep=PECTORAL_SPAN * PECTORAL_SWEEP, dihedral_deg=-PECTORAL_DROOP, thickness=0.11, camber=0.02,
                    sections=6, segments=18, side=sx)
        f.ensure_slots(wg, SLOTS)
        f.paint(wg, H)
        # a sunk domain panel on the upper skin, mid-span, framed in the dark structure
        cosd = math.cos(math.radians(PECTORAL_DROOP))

        def wing_panel(c, n, i, sx=sx, root=root, cosd=cosd):
            t = (sx * c.x - PECTORAL_ROOT[0]) / (PECTORAL_SPAN * cosd)
            if not (0.17 < t < 0.83) or n.z < 0.2:
                return False
            le = root.y + PECTORAL_SPAN * PECTORAL_SWEEP * t
            frac = (c.y - le) / (0.95 + (0.30 - 0.95) * t)
            return 0.12 < frac < 0.76
        f.inset_faces(wg, wing_panel, 0.02, depth=0.012, slot=D)
        wg = piece(wg, None, 7, sharp=38)
        d = math.radians(PECTORAL_DROOP)
        sd = Vector((sx * math.cos(d), 0.0, -math.sin(d)))
        span_dir[sx] = sd
        tipc = root + sd * PECTORAL_SPAN + Vector((0, PECTORAL_SPAN * PECTORAL_SWEEP + 0.06, 0.0))
        light = f.sphere("navlight", 0.055, center=tuple(tipc + Vector((0, 0.03, 0.0))), segments=10, rings=6, scale=(1.0, 2.2, 1.0))
        light = piece(light, J, 16)
        parts.append(bone_part([wg, light], "wing." + tag_))

    # ------------------------------------------------------------------ TAIL BASE (peduncle -> tail boom root)
    tb = []
    r0 = girth(PEDUNCLE_Y)
    ped = f.fuselage("peduncle", FORK[1] - PEDUNCLE_Y + 0.12, nose=(0, PEDUNCLE_Y - 0.1, 0.0), axis=(0, 1, 0), segments=16,
                     stations=[(0.0, r0 * 1.02, r0 * FLATTEN, r0 * FLATTEN, 2.4, 2.4, 0.0),
                               (0.55, r0 * 0.80, r0 * 0.78 * FLATTEN, r0 * 0.78 * FLATTEN, 2.4, 2.4, 0.01),
                               (1.0, r0 * 0.62, r0 * 0.66 * FLATTEN, r0 * 0.60 * FLATTEN, 2.4, 2.4, 0.02)])
    tb.append(piece(ped, H, 9, sharp=40))
    collar = f.lathe("collar", [(0.0, -0.06), (r0 * 1.12, -0.06), (r0 * 1.18, -0.02), (r0 * 1.18, 0.03), (r0 * 1.10, 0.07), (0.0, 0.07)],
                     segments=16, axis=(0, 1, 0), origin=(0, PEDUNCLE_Y + 0.02, 0.0))
    f.ensure_slots(collar, SLOTS)
    f.paint(collar, H)
    f.paint(collar, D, lambda c, n, i: abs(c.y - PEDUNCLE_Y - 0.025) < 0.035 and math.hypot(c.x, c.z) > r0 * 1.05)
    tb.append(piece(collar, None, 9))
    # the lower lobe -> a ventral skeg under the fork, behind the shackle
    sk = f.wing("skeg", root=(0, FORK[1] - 0.05, FORK[2] - 0.04), span=0.62, chord_root=0.40, chord_tip=0.10, sweep=0.55,
                forward=(0, -1, 0), up=(1, 0, 0), thickness=0.12, sections=3, segments=10)
    tb.append(piece(sk, H, 10, bevel=0.004))
    parts.append(bone_part(tb, "tail"))

    # ------------------------------------------------------------------ THE SHACKLE (where the chain leaves)
    # just past the whip's tip, in the whip's own plane: the chain begins at the tip of the tail
    hp = []
    ring_r, tube = 0.20, 0.055
    tip_p = Vector(lobe_point(1.0))
    # it hangs back and a little down off the tip like a towing eye, so it trails the whip without standing above it
    tip_d = Vector((0.0, math.cos(math.radians(30)), -math.sin(math.radians(30))))
    HITCH = tuple(tip_p + tip_d * (ring_r + 0.03))
    near = Vector(HITCH) - tip_d * ring_r            # the ring's side the clevis grips
    bm = bmesh.new()
    segs, sides = 16, 8
    grid = []
    for i in range(segs):
        a = 2 * math.pi * i / segs
        radial = Vector((0, math.cos(a), math.sin(a)))
        row = []
        for j in range(sides):
            b = 2 * math.pi * j / sides
            p = Vector(HITCH) + radial * (ring_r + tube * math.cos(b)) + Vector((1, 0, 0)) * (tube * math.sin(b))
            row.append(bm.verts.new(p))
        grid.append(row)
    for i in range(segs):
        for j in range(sides):
            bm.faces.new((grid[i][j], grid[(i + 1) % segs][j], grid[(i + 1) % segs][(j + 1) % sides], grid[i][(j + 1) % sides]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ring = f.new_mesh_object("ring", bm, smooth=True)
    hp.append(piece(ring, D, 11))
    for sx in (1, -1):   # the clevis cheeks: from the peduncle's belly down past the ring's pin
        mid = (tip_p + near) * 0.5
        ch = f.box("clevis", (0.035, 0.20, 0.20), center=(sx * 0.085, mid.y, mid.z))
        hp.append(piece(ch, H, 11, bevel=0.008))
    pin = f.lathe("pin", [(0.0, -0.13), (0.05, -0.13), (0.05, 0.13), (0.0, 0.13)], segments=10, axis=(1, 0, 0),
                  origin=(0, near.y, near.z))
    hp.append(piece(pin, H, 11))
    parts.append(bone_part(hp, "hitch"))

    # ------------------------------------------------------------------ THE WHIP (upper caudal lobe -> three-jointed boom)
    def whip_segment(k):
        u0, u1 = k / LOBE_SEGMENTS, (k + 1) / LOBE_SEGMENTS
        steps = 5
        nprof = 14
        bm = bmesh.new()
        rings = []
        lus, lcs = [], []
        for s in range(steps + 1):
            u = u0 + (u1 - u0) * s / steps
            L = Vector(lobe_point(u))
            C = Vector(lobe_chord_dir(u))
            ch = lobe_chord(u)
            th = 0.10 * (1.0 - 0.6 * u) + 0.02
            row = []
            # a lens section: a round spar at the leading edge, tapering to a knife at the trailing edge
            for j in range(nprof):
                a = 2 * math.pi * j / nprof
                cc = 0.5 * (1 - math.cos(a))           # 0 at the leading edge, 1 at the trailing edge
                half = th * 0.5 * (math.sqrt(max(cc, 0.0)) * (1.0 - cc) ** 1.1 * 2.2 + 0.04 * (1 - cc))
                side = 1.0 if math.sin(a) >= 0 else -1.0
                p = L + C * (cc * ch) + Vector((side * half, 0, 0))
                row.append(bm.verts.new(p))
                lus.append(u)
                lcs.append(cc)
            rings.append(row)
        for s in range(steps):
            for j in range(nprof):
                jn = (j + 1) % nprof
                fc = bm.faces.new((rings[s][j], rings[s + 1][j], rings[s + 1][jn], rings[s][jn]))
        cap0 = bm.faces.new(list(reversed(rings[0])))
        cap1 = bm.faces.new(rings[-1])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.verts.ensure_lookup_table()
        seg = f.new_mesh_object("blade", bm, smooth=True)
        f.ensure_slots(seg, SLOTS)
        f.paint(seg, H)
        split = 1.0 - LOBE_BLADE

        def is_blade(c, n, i):
            # chord fraction of the face centre: project onto the nearest station's chord
            best, bu = 1e9, u0
            for q in range(25):
                uu = u0 + (u1 - u0) * q / 24
                dd = (c - Vector(lobe_point(uu))).length
                if dd < best:
                    best, bu = dd, uu
            Lb, Cb = Vector(lobe_point(bu)), Vector(lobe_chord_dir(bu))
            return (c - Lb).dot(Cb) / max(lobe_chord(bu), 1e-4) > split - 0.013
        f.paint(seg, D, is_blade)
        f.sharpen_by_angle(seg, 50)
        tag(seg, 12, lu=lus, lc=lcs)
        objs = [seg]
        # the knuckle at this segment's root hinge: a collar along the chord, hiding the joint as it turns
        L0, C0 = Vector(lobe_point(u0)), Vector(lobe_chord_dir(u0))
        ch0 = lobe_chord(u0)
        kn = f.lathe("knuckle", [(0.0, -0.06), (0.07 * (1 - 0.45 * u0), -0.06), (0.085 * (1 - 0.45 * u0), 0.0),
                                 (0.06 * (1 - 0.45 * u0), ch0 * 0.55), (0.0, ch0 * 0.62)],
                     segments=10, axis=tuple(C0), origin=tuple(L0))
        objs.append(piece(kn, H, 13))
        if k == LOBE_SEGMENTS - 1:
            tipp = Vector(lobe_point(1.0))
            tl = f.sphere("whiplight", 0.06, center=tuple(tipp), segments=10, rings=6, scale=(1.0, 1.0, 1.0))
            objs.append(piece(tl, J, 14))
        name = "tail.%d" % (k + 2)
        return bone_part(objs, name)

    for k in range(LOBE_SEGMENTS):
        parts.append(whip_segment(k))

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
    f.sharpen_by_angle(mesh, 36)
    box_uv(mesh)
    ctx.log("thresher joined: %d verts, %d tris (n-gons triangulated: %d)" % (len(mesh.data.vertices), f.tri_count(mesh), len(big)))

    # ------------------------------------------------------------------ RIG
    B = rig.BoneSpec
    bones = [B("hull", (0, 0, 0), (0, -1.6, 0))]
    for tag_, sx in (("L", 1), ("R", -1)):
        h = Vector((sx * PECTORAL_ROOT[0], PECTORAL_ROOT[1], PECTORAL_ROOT[2]))
        bones.append(B("wing." + tag_, h, h + span_dir[sx] * 1.2, parent="hull"))
    bones.append(B("tail", (0, PEDUNCLE_Y, 0), FORK, parent="hull"))
    prev = "tail"
    for k in range(LOBE_SEGMENTS):
        u0, u1 = k / LOBE_SEGMENTS, (k + 1) / LOBE_SEGMENTS
        nm = "tail.%d" % (k + 2)
        bones.append(B(nm, lobe_point(u0), lobe_point(u1), parent=prev))
        prev = nm
    bones.append(B("hitch", HITCH, tuple(Vector(HITCH) + tip_d * 0.3), parent="tail.%d" % (LOBE_SEGMENTS + 1)))
    arm = rig.build_armature(ARMATURE, bones)
    rig.skin_rigid(mesh, arm, default_bone="hull")
    bone_names = [b.name for b in arm.data.bones]

    # ------------------------------------------------------------------ MORPHS (4-12% of the ~9.2 diagonal)
    me = mesh.data
    nv = len(me.vertices)

    def attr(nm):
        out = [0.0] * nv
        if nm in me.attributes:
            me.attributes[nm].data.foreach_get("value", out)
        return out
    pid, lu, lc = attr("pid"), attr("lu"), attr("lc")

    def pmask(*ids):
        s = set(float(i) for i in ids)
        return [1.0 if p in s else 0.0 for p in pid]
    ms = morphs.MaskSet(mesh)
    wing = {1: ms.mask("wing.L"), -1: ms.mask("wing.R")}
    whip = [ms.mask("tail.%d" % (k + 2)) for k in range(LOBE_SEGMENTS)]
    whip_all = ms.mask(*("tail.%d" % (k + 2) for k in range(LOBE_SEGMENTS)))
    dorsal = pmask(5)
    body_like = [m * h for m, h in zip(pmask(1, 2, 3, 4, 6, 15), ms.mask("hull"))]
    nacelle = pmask(8)
    shackle = ms.mask("hitch")
    peduncle = ms.mask("tail")
    blade = [w * (1.0 if p == 12.0 else 0.0) for w, p in zip(whip_all, pid)]
    top0 = girth(-0.88) * FLATTEN * 0.88 - 0.06

    def widen_blade(amount):
        def fn(i, co, n):
            if blade[i] <= 0 or lc[i] <= 0.35:
                return Vector((0, 0, 0))
            k = (lc[i] - 0.35) / 0.65
            return Vector(lobe_chord_dir(lu[i])) * (amount * lobe_chord(lu[i]) * k)
        return fn

    def raise_dorsal(factor):
        def fn(i, co, n):
            if dorsal[i] <= 0:
                return Vector((0, 0, 0))
            h = max(0.0, co.z - top0)
            return Vector((0, 0.35 * h * (factor - 1), h * (factor - 1)))
        return fn

    def about_axis_x_z(factor, mask, z0=0.0):
        def fn(i, co, n):
            if mask[i] <= 0:
                return Vector((0, 0, 0))
            return Vector((co.x * (factor - 1), 0, (co.z - z0) * (factor - 1))) * mask[i]
        return fn

    def ride_out(mask, sx, amount):
        return morphs.translate((sx * amount, 0, 0), mask)

    tip_u0 = (LOBE_SEGMENTS - 1) / LOBE_SEGMENTS
    tip_root = lobe_point(tip_u0)
    tip_axis = Vector(lobe_point(1.0)) - Vector(tip_root)
    tip_len = tip_axis.length
    shapes = {
        "Charge": [  # the weapon: the dorsal blade stands tall and the whip's blade widens
            raise_dorsal(1.55),
            widen_blade(0.55),
        ],
        "Mass": [  # volume: the hull, nacelles and peduncle fatten, the tow shackle swells (Mass Mode), the wings ride out
            about_axis_x_z(1.60, body_like),
            about_axis_x_z(1.52, nacelle, z0=0.0),
            morphs.translate((0, 0, 0.23), dorsal),
            morphs.scale_about(1.35, (0, PEDUNCLE_Y, 0), peduncle, axes=(1, 0, 1)),
            morphs.scale_about(1.9, HITCH, shackle),
            ride_out(wing[1], 1, 0.25), ride_out(wing[-1], -1, 0.25),
        ],
        "Space": [  # reach: the whip's last segment telescopes out, the wings reach along their span
            morphs.stretch_along(tuple(tip_axis.normalized()), tip_root, 1.0 + 0.70, whip[-1], ramp=(0.08, 0.3)),
            morphs.translate(tuple(tip_axis.normalized() * (0.70 * tip_len)), shackle),   # the shackle stays on the tip
            *[morphs.stretch_along(tuple(span_dir[sx]), (sx * PECTORAL_ROOT[0], PECTORAL_ROOT[1], PECTORAL_ROOT[2]), 1.18, wing[sx],
                                   ramp=(0.2, 0.5)) for sx in (1, -1)],
        ],
        "Time": [  # rate: the wings sweep back and spread, the dorsal and the whip rake back
            *[morphs.sweep((sx, 0, 0), (sx * PECTORAL_ROOT[0], 0, 0), (0, 0.42, 0), wing[sx]) for sx in (1, -1)],
            *[morphs.stretch_along((sx, 0, 0), (sx * PECTORAL_ROOT[0], 0, 0), 1.12, wing[sx]) for sx in (1, -1)],
            morphs.sweep((0, 0, 1), (0, 0, top0), (0, 0.42, 0), dorsal),
            morphs.sweep((0, 0, 1), FORK, (0, 0.30, -0.0), whip_all),
            morphs.sweep((0, 0, 1), FORK, (0, 0.30, -0.0), shackle),
        ],
    }
    for name in ("Charge", "Mass", "Space", "Time"):
        kb, mx, frac, moved = morphs.add_shape(mesh, name, shapes[name])
        ctx.log("shape %s: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))
    for a in ("pid", "lu", "lc"):
        if a in me.attributes:
            me.attributes.remove(me.attributes[a])

    # ------------------------------------------------------------------ DOCUMENTATION POSES (code drives the bones)
    # World axes of the Blender frame: +Z yaw. A +Z turn carries a point behind the hull (+Y) toward -X, the
    # pilot's RIGHT; +Y turns the +X (left) wing's tip DOWN.
    def chain(per_joint):
        return {"tail": (0, 0, per_joint[0]), "tail.2": (0, 0, per_joint[1]), "tail.3": (0, 0, per_joint[2]), "tail.4": (0, 0, per_joint[3])}
    poses = {
        "beatleft": chain((-8, -10, -13, -16)),
        "beatright": chain((8, 10, 13, 16)),
        "curl": chain((14, 14, 16, 22)),       # planted, the whip curls toward a ball on the right
        "flare": {"wing.L": (0, 40, 0), "wing.R": (0, -40, 0)},
    }
    names = ("beatleft", "beatright", "curl", "flare")
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
                "beatleft": [("tail.4", "x", +1, 1.0), ("tail.3", "x", +1, 0.5)],
                "beatright": [("tail.4", "x", -1, 1.0), ("tail.3", "x", -1, 0.5)],
                "curl": [("tail.4", "x", -1, 1.5)],
                "flare": [("wing.L", "z", -1, 0.25), ("wing.R", "z", -1, 0.25)],
            },
            "shapes": ["Charge", "Mass", "Space", "Time"],
            "slots": list(SLOTS),
            # the placeholder's envelope (Blender frame), which the gameplay and camera were tuned to
            "size_ref_blender": ((-1.76, -2.29, -0.64), (1.76, 5.77, 1.90)),
            # y and z carry the tow shackle hung off the whip tip (the chain begins at the tip of the tail, 2026-10-18)
            "size_tol": (0.08, 0.09, 0.14),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.04,
        },
    }
