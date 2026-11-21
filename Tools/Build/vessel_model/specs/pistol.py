"""
pistol - a NEW hull with no artist file: the PISTOL, the second seat of the Goby and Pistol crew vessel
(VesselClassType 18, .claude/skills/duo-vessel/references/designs/goby-and-pistol.md). Until now
GobyPistolDuo.BuildView drew the Pistol as an orange capsule (Garrett, 2026-10-24: "now give the pistol a model").
A spaceship inspired by a pistol shrimp (Alpheidae), never the animal.

THE READ: the Pistol seat films it SIDE-ON (a near-orthographic 28-degree lens about 29 units across, so the shrimp is
~1/17 of the screen) and the Goby's chase camera sees it from behind and above. Side-on it must say "pistol shrimp"
from the silhouette: a humped carapace, ONE outsized claw held forward with its hammer cocked, long whips arcing
back over the body, a segmented tail ending in a fan, legs underneath.

Clade -> ship:
  snapping claw (major chela) -> the CAVITATION CANNON (bone `gun`): a swollen barrel housing on an arm link, a fixed
                                 lower finger and an Engine-slot muzzle (the bubble's bore) between the fingers,
                                 domain barrel bands; its DACTYL is the hinged hammer (bone `claw`), cocked open at
                                 rest - code snaps it shut (take `snap`)
  small claw (minor chela)    -> a slender manipulator (bone `pincer`)
  carapace                    -> a core hull under overlapping hard plates (a dorsal shield and two side shields,
                                 hairline seams between them), a domain stripe sunk in the dorsal ridge, domain trim on
                                 the side shields' lower edges, a short prow (rostrum)
  transparent eye hoods       -> the front of the core is glass (Window): the hood the near-blind shrimp sees through
  eye stalks                  -> two sensor masts on top of the head (bones `eye.L` / `eye.R`), glass tips on domain
                                 collars
  antennae                    -> long sensor whips (bones `antenna.L` / `antenna.R`) arcing up and back over the body,
                                 radius ramped down and capped by a bead (no needles), domain whips on dark peduncles
  walking legs                -> three pairs of landing struts (bones `leg.<1-3>.L/.R`), each on a thruster foot that
                                 glows (Engine) underneath
  abdomen                     -> a segmented boom of six telescoping rings with domain trailing rims, three on
                                 `tail.1` and three on `tail.2`
  tail fan (telson, uropods)  -> the VECTORING FAN: a nozzle hub with an Engine mouth and the telson (bone `fan`),
                                 and two stacked uropod blades per side (bones `fan.L` / `fan.R`), domain outer edges

A HOVERING ASSEMBLY (vessel-model section 0.5): every part above floats clear of the core and of its neighbours as
its own closed shell on its own bone; the gate is
`assembly_report.py Pistol.fbx --floating gun,claw,pincer,eye.L,eye.R,antenna.L,antenna.R,leg.1.L,...,fan.R`.

Contract (FLEET_CONVENTION sections 1-4 for a NEW body):
  file      Assets/_Models/Vessel Models/Pistol.fbx (guid 7d2e9b4c1a6f4e83b5c0d8a2f6e1b937), preset "zup"
  frame     Blender: nose (claw tips) -Y, up +Z, pilot's left +X.  Unity (x, y, z) = Blender (-x, z, -y):
            NOSE / TRAVEL = Unity +Z, UP = Unity +Y, pilot's RIGHT = Unity +X. The big claw is on the pilot's RIGHT
            (Unity +X), the small claw on the left. Posed with LookRotation(travel, planeUp) - travel along the frame's
            R and up along its Up - the model's +X faces the Pistol camera (it sits on -M looking +M), so the camera
            sees the claw side. Origin: the carapace's centre line, between the legs (the capsule's centre).
  nodes     mesh `mesh` (identity, metres), armature `rig` (the zup preset's R(0,0,180) S 1); root bone `hull`
  bones     hull; gun -> claw; pincer; eye.L / eye.R; antenna.L / antenna.R; leg.1.L .. leg.3.R; tail.1 -> tail.2 ->
            fan -> fan.L / fan.R. Rigid skinning, one bone per part.
  size      1.65 long claw tip to fan tip (y -0.85 .. +0.80; the brief asked ~1.7, held to 5%), 0.56 wide, 0.70 tall;
            width and height are the clade's (declared in size_tol). The capsule it replaces was 2.72 x 1.7.
  slots     0 Domain, 1 Body, 2 Window, 3 Engine
  shapes    Charge (weapon): the cannon's barrel swells and its fingers reach, the hammer rides up with it;
            Mass (volume):   the carapace bulks out, the tail rings thicken, the struts splay to carry it;
            Space (reach):   the sensor whips reach further, the eye masts extend;
            Time (rate):     the tail fan spreads and its blades and telson lengthen
  takes     rest (first); snap (the hammer shut, +75 deg about the hinge, which is parallel to the model's X axis);
            dive (body tucked: the tail curls under, the legs fold back); glide (the fan spreads, the tail lifts).
            Single-frame poses, no blend-shape curves.
"""

import math
from _common import box_uv

NAME = "Pistol"
OUTPUT = "Assets/_Models/Vessel Models/Pistol.fbx"

ARMATURE = "rig"
MESH_NODE = "mesh"
SLOTS = ("Domain", "Body", "Window", "Engine")
D, H, W, J = 0, 1, 2, 3

# ---------------------------------------------------------------- layout (Blender frame: nose -Y, up +Z, left +X)
# the carapace core: (y, half-width, half-height top, half-height bottom, centre lift)
CARAPACE = [
    (-0.420, 0.022, 0.020, 0.018, 0.030),
    (-0.390, 0.055, 0.055, 0.045, 0.030),
    (-0.340, 0.090, 0.098, 0.075, 0.024),
    (-0.260, 0.115, 0.135, 0.098, 0.016),
    (-0.150, 0.124, 0.152, 0.108, 0.010),
    (-0.040, 0.120, 0.150, 0.104, 0.006),
    (0.020, 0.110, 0.138, 0.096, 0.004),
    (0.050, 0.096, 0.118, 0.084, 0.002),
]
PLATE_IN, PLATE_OUT = 0.003, 0.016          # the shields' offsets off the core

# the abdomen: six rings, three per bone, gaps between the bones
RINGS_1 = [(0.085, 0.180), (0.184, 0.279), (0.283, 0.372)]
RINGS_2 = [(0.402, 0.468), (0.472, 0.534), (0.538, 0.590)]
ABD_Y0, ABD_Y1 = 0.085, 0.590

# the cannon (pilot's RIGHT = Blender -X)
GUN_X, GUN_Z = -0.190, -0.075
PALM_Y0, PALM_Y1 = -0.400, -0.712           # rear, front (muzzle face)
HINGE = (-0.190, -0.615, 0.062)              # the hammer's knuckle centre: the `claw` bone's head
COCK_DEG = 75.0                              # the hammer's rest opening; `snap` shuts it all the way
SNAP_DEG = COCK_DEG                           # one constant, so the snap shuts it exactly
GUN_ANCHOR = (-0.110, -0.300, -0.145)        # where the arm link leaves the body (the `gun` bone's head)
PINCER_ANCHOR = (0.100, -0.290, -0.145)

EYE_BASE = (0.050, -0.300, 0.182)
EYE_AXIS = (0.35, -0.55, 0.76)
ANT_BASE = (0.060, -0.440, 0.000)
LEG_Y = (-0.215, -0.120, -0.025)
LEG_FOOT_DY = (0.000, 0.040, 0.080)          # the feet fan back, so the struts read as legs side-on
LEG_BASE_X, LEG_BASE_Z = 0.060, -0.140
HUB_Y = 0.618                                # the fan's hub, on the boom's end (tilted down with it)


def _lerp_tab(tab, y, k):
    if y <= tab[0][0]:
        return tab[0][k]
    for a, b in zip(tab, tab[1:]):
        if y <= b[0]:
            t = (y - a[0]) / (b[0] - a[0])
            return a[k] + (b[k] - a[k]) * t
    return tab[-1][k]


def abd(y):
    """The abdomen's outline at y: (half-width, top, bottom, centre z). It tapers, arches a little over its first
    rings and bends down to the fan - the shrimp's hunched back."""
    u = max(0.0, (y - ABD_Y0) / (ABD_Y1 - ABD_Y0))
    uc = min(1.0, u)
    w = 0.092 + (0.046 - 0.092) * uc
    ht = 0.110 + (0.050 - 0.110) * uc ** 0.9
    hb = 0.072 + (0.042 - 0.072) * uc
    dz = 0.045 * math.sin(math.pi * min(u, 0.6) / 1.2) - 0.13 * u ** 2.0
    return w, ht, hb, dz


def abd_tilt():
    """The boom's slope at its end, in degrees below horizontal: the fan leaves along it."""
    e = 1e-3
    d = (abd(ABD_Y1 + e)[3] - abd(ABD_Y1 - e)[3]) / (2 * e)
    return math.degrees(math.atan(-d))


FAN_Z = abd(HUB_Y)[3]
URO_ROOT = (0.070, HUB_Y + 0.046, FAN_Z + 0.002)  # before the tilt; the fan is built level and turned down


def catmull(pts, per=6):
    """A smooth polyline through control points (Catmull-Rom), `per` samples per span."""
    from mathutils import Vector
    P = [Vector(p) for p in pts]
    P = [P[0] + (P[0] - P[1])] + P + [P[-1] + (P[-1] - P[-2])]
    out = []
    for i in range(1, len(P) - 2):
        p0, p1, p2, p3 = P[i - 1], P[i], P[i + 1], P[i + 2]
        for s in range(per):
            t = s / per
            t2, t3 = t * t, t * t * t
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
    out.append(P[-2])
    return out


def build(ctx):
    import bpy  # noqa: F401
    import bmesh
    from mathutils import Vector, Matrix
    f, rig, morphs, takes = ctx.forms, ctx.rig, ctx.morphs, ctx.takes
    parts = []
    PID = {}

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
        o = objs[0] if len(objs) == 1 else f.join(objs, name, SLOTS)
        o.name = name
        for vg in list(o.vertex_groups):
            o.vertex_groups.remove(vg)
        f.tag_group(o, name)
        parts.append(o)
        return o

    def se_pt(w, ht, hb, a, nt=2.4, nb=2.6):
        c, s = math.cos(a), math.sin(a)
        hh, nn = (ht, nt) if s >= 0 else (hb, nb)
        return (w * math.copysign(abs(c) ** (2.0 / nn), c), hh * math.copysign(abs(s) ** (2.0 / nn), s))

    def shell(name, tab, y0, y1, a0, a1, off_in, off_out, n=14, stations=12, nt=2.4, nb=2.6):
        """A curved armour plate: the band of the core's outline between angles a0..a1 (degrees, from +X
        counter-clockwise in x-z), offset off_in..off_out outside the core, lofted from y0 to y1."""
        sts = []
        for k in range(stations + 1):
            y = y0 + (y1 - y0) * k / stations
            w, ht, hb, dz = (_lerp_tab(tab, y, 1), _lerp_tab(tab, y, 2), _lerp_tab(tab, y, 3), _lerp_tab(tab, y, 4))
            outer, inner = [], []
            for i in range(n + 1):
                a = math.radians(a0 + (a1 - a0) * i / n)
                ox, oz = se_pt(w + off_out, ht + off_out, hb + off_out, a, nt, nb)
                ix, iz = se_pt(w + off_in, ht + off_in, hb + off_in, a, nt, nb)
                outer.append((ox, oz))
                inner.append((ix, iz))
            prof = outer + list(reversed(inner))
            sts.append(f.Station((0, y, dz), prof, right=(1, 0, 0), up=(0, 0, 1)))
        return f.loft(name, sts)

    # ================================================================== THE CORE (hull)
    core_parts = []
    y0c, y1c = CARAPACE[0][0], CARAPACE[-1][0]
    length = y1c - y0c
    dense = []
    for k in range(25):
        y = y0c + length * k / 24
        dense.append(((y - y0c) / length, _lerp_tab(CARAPACE, y, 1), _lerp_tab(CARAPACE, y, 2), _lerp_tab(CARAPACE, y, 3),
                      2.4, 2.6, _lerp_tab(CARAPACE, y, 4)))
    core = f.fuselage("core", length, nose=(0, y0c, 0), axis=(0, 1, 0), segments=28, stations=dense)
    f.ensure_slots(core, SLOTS)
    f.paint(core, H)
    # the transparent eye hood: the core's front, ahead of the dorsal shield
    f.paint(core, W, lambda c, n, i: c.y < -0.330 and c.z > _lerp_tab(CARAPACE, c.y, 4) - 0.010 and n.y < 0.2)
    core_parts.append(piece(core, None, 1))
    # the dorsal shield (top arc) and the two side shields; hairline seams between them show the dark core
    dorsal = shell("dorsal", CARAPACE, -0.318, 0.050, 34, 146, PLATE_IN, PLATE_OUT)
    f.ensure_slots(dorsal, SLOTS)
    f.paint(dorsal, H)
    f.paint(dorsal, D, lambda c, n, i: n.z > 0.95 and -0.28 < c.y < 0.02)
    core_parts.append(piece(dorsal, None, 2))
    for sx in (1, -1):
        a0, a1 = (-52, 29) if sx > 0 else (151, 232)
        side = shell("side", CARAPACE, -0.300, 0.040, a0, a1, PLATE_IN, PLATE_OUT, n=12)
        f.ensure_slots(side, SLOTS)
        f.paint(side, H)
        f.paint(side, D, lambda c, n, i, sx=sx: sx * n.x > 0.2 and c.z < _lerp_tab(CARAPACE, c.y, 4) - 0.35 * _lerp_tab(CARAPACE, c.y, 3)
                and -0.28 < c.y < 0.03)
        core_parts.append(piece(side, None, 2))
    # the prow (rostrum): a short blunt spike over the hood
    ros = f.lathe("rostrum", [(0.0, 0.0), (0.008, 0.006), (0.016, 0.040), (0.018, 0.070), (0.0, 0.074)], segments=10,
                  axis=(0, 1, 0), origin=(0, -0.500, 0.046))
    core_parts.append(piece(ros, H, 3))
    bone_part(core_parts, "hull")

    # ================================================================== THE CANNON (gun) and its hammer (claw)
    gp = []
    link = f.tube_along("link", [GUN_ANCHOR, (-0.150, -0.350, -0.130), (-0.178, -0.382, -0.106)], [0.024, 0.026, 0.022], segments=12)
    gp.append(piece(link, H, 10, bevel=0.0))
    # the barrel housing: a swollen palm, a little compressed, nose on -Y
    palm_st = [(-0.400, 0.032, 0.036, 0.036, -0.005), (-0.420, 0.050, 0.062, 0.060, 0.0), (-0.470, 0.062, 0.084, 0.082, 0.0),
               (-0.560, 0.066, 0.092, 0.088, 0.0), (-0.640, 0.062, 0.088, 0.084, 0.0), (-0.690, 0.052, 0.072, 0.068, -0.004),
               (-0.712, 0.040, 0.052, 0.050, -0.008)]
    plen = PALM_Y0 - PALM_Y1
    palm = f.fuselage("palm", plen, nose=(GUN_X, PALM_Y1, GUN_Z), axis=(0, 1, 0), segments=24,
                      stations=[((y - PALM_Y1) / plen, _lerp_tab(palm_st[::-1], y, 1), _lerp_tab(palm_st[::-1], y, 2),
                                 _lerp_tab(palm_st[::-1], y, 3), 3.0, 2.8, _lerp_tab(palm_st[::-1], y, 4))
                                for y in [PALM_Y1 + plen * k / 18 for k in range(19)]])   # dense, so the bands paint
    f.ensure_slots(palm, SLOTS)
    f.paint(palm, H)
    f.paint(palm, D, lambda c, n, i: (-0.505 < c.y < -0.462 or -0.592 < c.y < -0.575) and abs(n.y) < 0.6)
    f.paint(palm, J, lambda c, n, i: c.y < PALM_Y1 + 0.006 and n.y < -0.5)   # the muzzle: the bubble's bore
    gp.append(piece(palm, None, 11))
    # the fixed lower finger (pollex), from the barrel's lower lip forward
    pol = f.spine_loft("pollex", [(GUN_X, -0.702, GUN_Z - 0.030), (GUN_X, -0.775, GUN_Z - 0.022), (GUN_X, -0.848, GUN_Z + 0.004)],
                       [f.superellipse(0.030, 0.026, n=2.4, segments=14), f.superellipse(0.025, 0.021, n=2.4, segments=14),
                        f.superellipse(0.014, 0.012, n=2.4, segments=14)])
    f.ensure_slots(pol, SLOTS)
    f.paint(pol, H)
    gp.append(piece(pol, None, 12))
    bone_part(gp, "gun")

    # the hammer (dactyl): built shut, then cocked open about the knuckle
    hx, hy, hz = HINGE
    spine = [(hx, hy, hz), (hx, -0.660, hz - 0.004), (hx, -0.720, hz - 0.018), (hx, -0.785, hz - 0.045), (hx, -0.832, hz - 0.082)]
    profs = [f.superellipse(0.036, 0.034, n=2.8, segments=16), f.superellipse(0.038, 0.034, n=2.8, segments=16),
             f.superellipse(0.032, 0.030, n=2.8, segments=16), f.superellipse(0.024, 0.022, n=2.8, segments=16),
             f.superellipse(0.014, 0.013, n=2.8, segments=16)]
    dac = f.spine_loft("dactyl", spine, profs)
    f.ensure_slots(dac, SLOTS)
    f.paint(dac, H)
    f.paint(dac, D, lambda c, n, i: n.z > 0.15 and c.y < hy - 0.025)
    knuckle = f.sphere("knuckle", 0.030, center=HINGE, segments=14, rings=8, scale=(1.15, 1.0, 1.0))
    f.ensure_slots(knuckle, SLOTS)
    f.paint(knuckle, H)
    # the plunger under the hammer's root: it drives into the barrel's socket on the snap
    plunger = f.sphere("plunger", 0.017, center=(hx, -0.668, hz - 0.036), segments=10, rings=6, scale=(1.0, 1.3, 1.0))
    f.ensure_slots(plunger, SLOTS)
    f.paint(plunger, H)
    claw_parts = [piece(dac, None, 13), piece(knuckle, None, 13), piece(plunger, None, 13)]
    cock = Matrix.Translation(Vector(HINGE)) @ Matrix.Rotation(math.radians(-COCK_DEG), 4, "X") @ Matrix.Translation(-Vector(HINGE))
    for o in claw_parts:
        f.transform_mesh(o, cock)
    tip_shut = Vector(spine[-1])
    tip_cocked = cock @ tip_shut
    bone_part(claw_parts, "claw")

    # ================================================================== THE SMALL CLAW (pincer, pilot's left)
    pp = []
    plink = f.tube_along("plink", [PINCER_ANCHOR, (0.122, -0.335, -0.136), (0.138, -0.368, -0.122)], [0.018, 0.019, 0.016], segments=10)
    pp.append(piece(plink, H, 20))
    pax = Vector((0.03, -1.0, 0.06)).normalized()
    pal = f.lathe("ppalm", [(0.0, 0.0), (0.020, 0.004), (0.030, 0.035), (0.031, 0.100), (0.024, 0.140), (0.0, 0.150)], segments=14,
                  axis=tuple(pax), origin=(0.142, -0.378, -0.118))
    f.ensure_slots(pal, SLOTS)
    f.paint(pal, H)
    pp.append(piece(pal, None, 21))
    p_front = Vector((0.142, -0.378, -0.118)) + pax * 0.150
    for dz in (0.013, -0.013):
        a = p_front + pax * 0.004 + Vector((0, 0, dz * 0.6))
        b = a + pax * 0.055 + Vector((0, 0, dz * 0.5))
        cpt = b + pax * 0.045 - Vector((0, 0, dz * 0.7))
        fg = f.tube_along("finger", [tuple(a), tuple(b), tuple(cpt)], [0.010, 0.009, 0.007], segments=8)
        f.ensure_slots(fg, SLOTS)
        f.paint(fg, H)
        f.paint(fg, D, lambda c, n, i, b=b: (c - b).dot(pax) > 0.005)
        pp.append(piece(fg, None, 22))
    bone_part(pp, "pincer")

    # ================================================================== EYE MASTS (eye.L / eye.R)
    eax = {}
    for side_, sx in (("L", 1), ("R", -1)):
        base = Vector((sx * EYE_BASE[0], EYE_BASE[1], EYE_BASE[2]))
        ax = Vector((sx * EYE_AXIS[0], EYE_AXIS[1], EYE_AXIS[2])).normalized()
        eax[sx] = (base, ax)
        mast = f.lathe("mast", [(0.0, 0.0), (0.019, 0.002), (0.022, 0.016), (0.014, 0.022), (0.012, 0.060), (0.0, 0.062)],
                       segments=12, axis=tuple(ax), origin=tuple(base))
        f.ensure_slots(mast, SLOTS)
        f.paint(mast, H)
        f.paint(mast, D, lambda c, n, i, base=base, ax=ax: (c - base).dot(ax) < 0.020)
        glass = f.sphere("glass", 0.028, center=tuple(base + ax * 0.092), segments=14, rings=8)
        bone_part([piece(mast, None, 30), piece(glass, W, 31)], "eye." + side_)

    # ================================================================== SENSOR WHIPS (antenna.L / antenna.R)
    ant_ctrl = {}
    for side_, sx in (("L", 1), ("R", -1)):
        base = Vector((sx * ANT_BASE[0], ANT_BASE[1], ANT_BASE[2]))
        ped_end = Vector((sx * 0.070, -0.510, 0.032))
        ped = f.tube_along("peduncle", [tuple(base), tuple((base + ped_end) * 0.5), tuple(ped_end)], [0.020, 0.021, 0.017], segments=12)
        f.ensure_slots(ped, SLOTS)
        f.paint(ped, H)
        ctrl = [ped_end + (ped_end - base).normalized() * 0.010, (sx * 0.082, -0.575, 0.095), (sx * 0.100, -0.590, 0.195),
                (sx * 0.130, -0.525, 0.292), (sx * 0.170, -0.390, 0.352), (sx * 0.210, -0.200, 0.382),
                (sx * 0.240, 0.020, 0.372), (sx * 0.258, 0.230, 0.332), (sx * 0.268, 0.410, 0.272)]
        path = catmull([tuple(p) for p in ctrl], per=5)
        nr = len(path)
        radii = [0.0125 - 0.0055 * (k / (nr - 1)) ** 0.8 for k in range(nr)]
        whip = f.tube_along("whip", [tuple(p) for p in path], radii, segments=8)
        f.ensure_slots(whip, SLOTS)
        f.paint(whip, D)
        bead = f.sphere("bead", 0.011, center=tuple(path[-1] + (path[-1] - path[-2]).normalized() * 0.006), segments=10, rings=6)
        f.ensure_slots(bead, SLOTS)
        f.paint(bead, H)
        ant_ctrl[sx] = (base, path)
        bone_part([piece(ped, None, 40), piece(whip, None, 41), piece(bead, None, 41)], "antenna." + side_)

    # ================================================================== LANDING STRUTS (leg.<n>.L/.R)
    legs = {}
    for k, ly in enumerate(LEG_Y, start=1):
        for side_, sx in (("L", 1), ("R", -1)):
            base = Vector((sx * LEG_BASE_X, ly, LEG_BASE_Z))
            knee = Vector((sx * 0.155, ly - 0.035, -0.166))
            foot = Vector((sx * 0.185, ly + LEG_FOOT_DY[k - 1], -0.262))
            strut = f.tube_along("strut", [tuple(base), tuple((base + knee) * 0.5), tuple(knee), tuple((knee + foot) * 0.5), tuple(foot)],
                                 [0.015, 0.016, 0.015, 0.013, 0.012], segments=10)
            f.ensure_slots(strut, SLOTS)
            f.paint(strut, H)
            pod = f.lathe("pod", [(0.0, 0.0), (0.017, 0.003), (0.022, 0.016), (0.021, 0.036), (0.016, 0.040), (0.0, 0.040)],
                          segments=12, axis=(0, 0, -1), origin=tuple(foot - Vector((0, 0, 0.006))))
            f.ensure_slots(pod, SLOTS)
            f.paint(pod, H)
            f.paint(pod, J, lambda c, n, i: n.z < -0.6)
            nm = "leg.%d.%s" % (k, side_)
            legs[nm] = (base, foot, sx)
            bone_part([piece(strut, None, 50), piece(pod, None, 51)], nm)

    # ================================================================== THE SEGMENTED BOOM (tail.1, tail.2)
    def ring(y0, y1, pid):
        """One abdominal ring on the boom's curved spine (sections square to it), its front a step smaller than the
        ring ahead's back so the rings read as telescoping plates, a domain rim on its trailing top, and a pleuron
        plate hanging down each side (the shrimp's scalloped lower edge, side-on)."""
        ys = [y0, y0 + 0.22 * (y1 - y0), y0 + 0.6 * (y1 - y0), y1]
        sc = [0.90, 1.0, 1.0, 0.985]
        spine_, profs_ = [], []
        for y, s in zip(ys, sc):
            w, ht, hb, dz = abd(y)
            spine_.append((0, y, dz))
            profs_.append(f.superellipse(w * s, ht * s, n=2.3, segments=24, h_bottom=hb * s, n_bottom=3.0))
        o = f.spine_loft("ring", spine_, profs_)
        f.ensure_slots(o, SLOTS)
        f.paint(o, H)
        f.paint(o, D, lambda c, n, i: c.y > y1 - 0.13 * (y1 - y0) and abs(n.y) < 0.75 and c.z > abd(c.y)[3])
        out = [piece(o, None, pid)]
        yc = 0.5 * (y0 + y1)
        w, ht, hb, dz = abd(yc)
        L = y1 - y0
        slope = (abd(y1)[3] - abd(y0)[3]) / L
        ca, sa = 1.0 / math.sqrt(1 + slope * slope), slope / math.sqrt(1 + slope * slope)
        pts = [(-0.46 * L, 0.0), (0.46 * L, 0.0), (0.44 * L, -hb * 0.9), (0.30 * L, -hb - 0.026), (-0.30 * L, -hb - 0.020),
               (-0.46 * L, -hb * 0.85)]
        pts = [(a * ca - b * sa, a * sa + b * ca) for (a, b) in pts]   # turned to the boom's slope (y, z)
        for sx in (1, -1):
            outline = pts if sx > 0 else [(-a, b) for (a, b) in reversed(pts)]
            pl = f.plate("pleuron", outline, 0.010, normal=(sx, 0, 0), origin=(sx * (w + 0.0095), yc, dz),
                         right=(0, sx, 0))
            f.ensure_slots(pl, SLOTS)
            f.paint(pl, H)
            out.append(piece(pl, None, pid, bevel=0.002))
        return out
    bone_part([o for (a, b) in RINGS_1 for o in ring(a, b, 60)], "tail.1")
    bone_part([o for (a, b) in RINGS_2 for o in ring(a, b, 61)], "tail.2")

    # ================================================================== THE VECTORING FAN (fan, fan.L, fan.R)
    fz = FAN_Z
    tilt = abd_tilt()
    hubp = Vector((0, HUB_Y, fz))
    TILT = Matrix.Translation(hubp) @ Matrix.Rotation(math.radians(-tilt), 4, "X") @ Matrix.Translation(-hubp)
    TILT3 = TILT.to_3x3()
    ctx.log("fan tilt: %.1f deg below level (the boom's end)" % tilt)
    hub = f.lathe("hub", [(0.0, 0.0), (0.026, 0.0), (0.034, 0.016), (0.036, 0.040), (0.033, 0.052), (0.026, 0.054), (0.0, 0.054)],
                  segments=16, axis=(0, 1, 0), origin=(0, HUB_Y, fz))
    f.ensure_slots(hub, SLOTS)
    f.paint(hub, H)
    f.paint(hub, J, lambda c, n, i: n.y > 0.6)
    tel_y0 = HUB_Y + 0.062
    tel = f.plate("telson", [(-0.022, 0.0), (0.022, 0.0), (0.030, 0.050), (0.024, 0.115), (0.012, 0.136), (-0.012, 0.136),
                             (-0.024, 0.115), (-0.030, 0.050)], 0.016, normal=(0, -math.sin(math.radians(6)), math.cos(math.radians(6))),
                  origin=(0, tel_y0, fz - 0.004), right=(1, 0, 0))
    f.ensure_slots(tel, SLOTS)
    f.paint(tel, H)
    f.paint(tel, D, lambda c, n, i: c.y > tel_y0 + 0.105)
    fan_parts = [piece(hub, None, 70), piece(tel, None, 71, bevel=0.003)]
    for o in fan_parts:
        f.transform_mesh(o, TILT)
    bone_part(fan_parts, "fan")
    tel_root, tel_tip = TILT @ Vector((0, tel_y0, fz)), TILT @ Vector((0, tel_y0 + 0.136, fz))

    uro_dir, uro_root = {}, {}
    for side_, sx in (("L", 1), ("R", -1)):
        root = Vector((sx * URO_ROOT[0], URO_ROOT[1], URO_ROOT[2]))
        blades = []
        for yaw, dzb, ln in ((24.0, 0.0, 0.155), (40.0, 0.016, 0.165)):
            yr = math.radians(yaw)
            dih = math.radians(18.0)
            d = Vector((sx * math.sin(yr) * math.cos(dih), math.cos(yr) * math.cos(dih), math.sin(dih) * math.sin(yr))).normalized()
            nrm = Vector((-sx * math.sin(dih) * 0.6, 0.0, 1.0)).normalized()
            right = (d - nrm * d.dot(nrm)).normalized()
            outline = [(0.0, -0.020), (0.045, -0.028), (ln * 0.90, -0.022), (ln, 0.0), (ln * 0.90, 0.026), (0.045, 0.030), (0.0, 0.020)]
            if sx < 0:
                outline = [(x, -y) for (x, y) in reversed(outline)]
            bl = f.plate("uropod", outline, 0.012, normal=tuple(nrm), origin=tuple(root + Vector((0, 0, dzb))), right=tuple(right))
            f.ensure_slots(bl, SLOTS)
            f.paint(bl, H)
            f.paint(bl, D, lambda c, n, i, root=root, d=d, ln=ln: (c - root).dot(d) > ln * 0.70)
            blades.append(piece(bl, None, 72, bevel=0.003))
            uro_dir[sx] = (TILT3 @ d).normalized()
        for o in blades:
            f.transform_mesh(o, TILT)
        uro_root[sx] = TILT @ root
        bone_part(blades, "fan." + side_)

    # ================================================================== JOIN, n-gons out
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
    ctx.log("pistol joined: %d verts, %d tris (n-gons triangulated: %d)" % (len(mesh.data.vertices), f.tri_count(mesh), len(big)))

    # ================================================================== RIG
    B = rig.BoneSpec
    bones = [B("hull", (0, 0, 0), (0, -0.30, 0)),
             B("gun", GUN_ANCHOR, (GUN_X, PALM_Y1, GUN_Z), parent="hull"),
             B("claw", HINGE, tuple(tip_cocked), parent="gun"),
             B("pincer", PINCER_ANCHOR, tuple(p_front), parent="hull")]
    for side_, sx in (("L", 1), ("R", -1)):
        base, ax = eax[sx]
        bones.append(B("eye." + side_, tuple(base), tuple(base + ax * 0.092), parent="hull"))
    for side_, sx in (("L", 1), ("R", -1)):
        base, path = ant_ctrl[sx]
        bones.append(B("antenna." + side_, tuple(base), tuple(path[min(4, len(path) - 1)]), parent="hull"))
    for nm, (base, foot, sx) in legs.items():
        bones.append(B(nm, tuple(base), tuple(foot), parent="hull"))
    bones += [B("tail.1", (0, RINGS_1[0][0], abd(RINGS_1[0][0])[3]), (0, RINGS_1[-1][1], abd(RINGS_1[-1][1])[3]), parent="hull"),
              B("tail.2", (0, RINGS_2[0][0], abd(RINGS_2[0][0])[3]), (0, RINGS_2[-1][1], abd(RINGS_2[-1][1])[3]), parent="tail.1"),
              B("fan", tuple(hubp), tuple(tel_tip), parent="tail.2")]
    for side_, sx in (("L", 1), ("R", -1)):
        bones.append(B("fan." + side_, tuple(uro_root[sx]), tuple(uro_root[sx] + uro_dir[sx] * 0.15), parent="fan"))
    arm = rig.build_armature(ARMATURE, bones)
    rig.skin_rigid(mesh, arm, default_bone="hull")
    bone_names = [b.name for b in arm.data.bones]

    # ================================================================== MORPHS (3-15 % of the ~1.9 diagonal)
    me = mesh.data
    nv = len(me.vertices)
    pid = [0.0] * nv
    me.attributes["pid"].data.foreach_get("value", pid)

    def pmask(*ids):
        s = set(float(i) for i in ids)
        return [1.0 if p in s else 0.0 for p in pid]
    ms = morphs.MaskSet(mesh)
    m = {n: ms.mask(n) for n in bone_names}

    def field_move(field, mask, extra=Vector((0, 0, 0)), anchor=None):
        """Translate a part rigidly by `field` evaluated at its anchor (so a part rides the hull it floats off)."""
        dv = field(Vector(anchor)) + extra

        def fn(i, co, n):
            return dv * mask[i] if mask[i] > 0 else Vector((0, 0, 0))
        return fn

    def rot_about(axis, pivot, deg, mask):
        R = Matrix.Rotation(math.radians(deg), 3, Vector(axis))
        p = Vector(pivot)

        def fn(i, co, n):
            if mask[i] <= 0:
                return Vector((0, 0, 0))
            return (R @ (co - p) + p - co) * mask[i]
        return fn

    # Mass: the hull's own scale field
    MC, MS = Vector((0, -0.17, 0.02)), Vector((1.45, 1.10, 1.45))

    def hull_field(co):
        d = co - MC
        return Vector((d.x * (MS.x - 1), d.y * (MS.y - 1), d.z * (MS.z - 1)))
    hullm = m["hull"]

    def hull_swell(i, co, n):
        return hull_field(co) if hullm[i] > 0 else Vector((0, 0, 0))

    tailm = [max(a, b) for a, b in zip(m["tail.1"], m["tail.2"])]

    def tail_swell(i, co, n):
        if tailm[i] <= 0:
            return Vector((0, 0, 0))
        dz = abd(co.y)[3]
        return Vector((co.x * 0.30, 0, (co.z - dz) * 0.30))

    # Charge: the barrel swells about its rear, the hammer rides its knuckle's displacement
    CP, CS = Vector((GUN_X, PALM_Y0, GUN_Z)), Vector((1.35, 1.25, 1.40))

    def gun_field(co):
        d = co - CP
        return Vector((d.x * (CS.x - 1), d.y * (CS.y - 1), d.z * (CS.z - 1)))
    barrel = pmask(11, 12)

    def gun_swell(i, co, n):
        return gun_field(co) if barrel[i] > 0 else Vector((0, 0, 0))

    shapes = {
        "Charge": [gun_swell, field_move(gun_field, m["claw"], anchor=HINGE)],
        "Mass": [hull_swell, tail_swell,
                 *[field_move(hull_field, m[nm], anchor=bn) for nm, bn in
                   (("eye.L", eax[1][0]), ("eye.R", eax[-1][0]), ("antenna.L", ant_ctrl[1][0]), ("antenna.R", ant_ctrl[-1][0]),
                    ("gun", GUN_ANCHOR), ("claw", GUN_ANCHOR), ("pincer", PINCER_ANCHOR))],
                 *[field_move(hull_field, m[nm], extra=Vector((sx * 0.045, 0, -0.030)), anchor=base) for nm, (base, foot, sx) in legs.items()]],
        "Space": [*[morphs.scale_about(1.20, tuple(ant_ctrl[sx][0]), m["antenna." + s]) for s, sx in (("L", 1), ("R", -1))],
                  *[morphs.stretch_along(tuple(eax[sx][1]), tuple(eax[sx][0]), 1.75, m["eye." + s]) for s, sx in (("L", 1), ("R", -1))]],
        "Time": [*[rot_about(tuple(TILT3 @ Vector((0, 0, 1))), tuple(uro_root[sx]), -sx * 26.0, m["fan." + s]) for s, sx in (("L", 1), ("R", -1))],
                 *[morphs.stretch_along(tuple(uro_dir[sx]), tuple(uro_root[sx]), 1.30, m["fan." + s]) for s, sx in (("L", 1), ("R", -1))],
                 morphs.stretch_along(tuple((tel_tip - tel_root).normalized()), tuple(tel_root), 1.35, pmask(71))],
    }
    for name in ("Charge", "Mass", "Space", "Time"):
        kb, mx, frac, moved = morphs.add_shape(mesh, name, shapes[name])
        ctx.log("shape %s: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))
    if "pid" in me.attributes:
        me.attributes.remove(me.attributes["pid"])

    # ================================================================== TAKES (code drives the bones; these are the poses)
    # world degrees in the Blender frame: +X turns a point ahead and above DOWN (the hammer shuts), a point behind DOWN
    # needs -X (the tail curls under); +Z carries a point behind toward -X.
    legs_back = {nm: (40, 0, 0) for nm in legs}
    poses = {
        "snap": {"claw": (SNAP_DEG, 0, 0)},
        "dive": {"tail.1": (-22, 0, 0), "tail.2": (-30, 0, 0), "fan": (-28, 0, 0), **legs_back,
                 "gun": (-8, 0, 0), "pincer": (-8, 0, 0)},
        "glide": {"tail.1": (6, 0, 0), "tail.2": (6, 0, 0), "fan.L": (0, 0, -24), "fan.R": (0, 0, 24),
                  **{nm: (25, 0, 0) for nm in legs}},
    }
    names = ("snap", "dive", "glide")
    for p in names:
        takes.pose_take(arm, p, poses[p])
    takes.clear_pose(arm)

    lo, hi = f.bounds(mesh)
    ctx.log("bounds (blender) %s .. %s" % (tuple(round(v, 3) for v in lo), tuple(round(v, 3) for v in hi)))
    ctx.log("claw tip: cocked %s, shut %s" % (tuple(round(v, 3) for v in tip_cocked), tuple(round(v, 3) for v in tip_shut)))
    return {
        "armature": arm, "mesh": mesh,
        "spec": {
            "preset": "zup", "armature": ARMATURE, "mesh_node": MESH_NODE,
            "bones": bone_names,
            "takes": [ARMATURE + "|" + p for p in names],
            "pose_takes": [ARMATURE + "|" + p for p in names],
            "sheet_poses": names,
            "pose_expect": {
                "snap": [("claw", "z", -1, 0.04)],
                "dive": [("fan", "z", -1, 0.08)],
                "glide": [("fan.L", "x", +1, 0.008), ("fan.R", "x", -1, 0.008)],
            },
            "shapes": ["Charge", "Mass", "Space", "Time"],
            "slots": list(SLOTS),
            # the brief's envelope: 1.70 claw tip to fan tip (held to 5%), width and height the clade's (declared)
            "size_ref_blender": ((-0.28, -0.85, -0.32), (0.28, 0.85, 0.40)),
            "size_tol": (0.5, 0.05, 0.5),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.03,
        },
    }
