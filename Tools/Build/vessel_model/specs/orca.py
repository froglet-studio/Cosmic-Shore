"""
orca - a NEW hull with no artist file: the Orca, the 4-seat crew vessel (one helm, three gunner turrets;
Assets/_Scripts/Controller/Vessel/Crew/ORCA_CREW.md). Today Orca.prefab draws the Sparrow's mesh plus three
procedural octahedron pods on box struts (OrcaCrewView). Garrett, 2026-10-21: "it is time to use the vessel
modeling tools and really step up the vessel". A spaceship inspired by an orca, never the animal (SKILL.md 0).

THE READ, from the chase camera (the Sparrow's followOffset z -50: the ~46-unit span fills ~45% of a 16:9 frame):
a broad, deep, black gunmetal hull with ONE tall triangular dorsal pylon carrying a long railgun, two long paddle
outriggers carrying a gun pod at each tip, and black flukes with two glowing engine mouths between them; the
orca's white becomes the domain saddle, flank sweeps and belly, the eye patch becomes the glass.

Clade -> ship (every part answers "what is this on the ship?"):
  broad deep body       -> the armoured hull (bone `hull`): a superellipse loft, blunt melon nose, three panel seams
  eye patch             -> sunk sensor glass on each side of the head; the blowhole is the helm's canopy, dark-framed
  white chin / belly    -> the domain belly plate (sunk, framed)
  white flank sweep     -> a sunk domain plate per flank rising toward the tail; the grey saddle behind the dorsal fin
                           is a domain saddle plate
  tall dorsal fin       -> the DORSAL PYLON (bone `fin`): a raked airfoil blade with a domain rank band, topped by a
                           collar pedestal carrying the SNIPER turret
  pectoral fins         -> the OUTRIGGERS (bones `wing.L` / `wing.R`): broad swept paddles with sunk domain panels,
                           each ending in a nacelle (domain band, running light, small ember mouth aft) whose pedestal
                           carries a turret: FAST GUN on the port (pilot's left) tip, BOMB on the starboard tip
  tail stock            -> the peduncle (bone `tail`) behind a domain-trimmed joint collar
  flukes                -> the ENGINE SECTION (bone `tail.2`): two broad swept fluke planes, domain under and a domain
                           trailing-edge trim on top, carrying the two main engines (`jet.L` / `jet.R`, each with a
                           `.mouth` child at the nozzle lip, length axis aft, skinning the bell)

THE TURRETS (each one bespoke to its weapon, every turret at rest aims straight forward, +Z Unity):
  each = a domain-banded BASE RING + yoke plates (yaw bone) | trunnion pins, housing, gun, scope (pitch bone) |
         muzzle part (muzzle bone, at the barrel tip) | a missile tube on the outboard end of the trunnion pin
         (pitch bone) whose mouth collar skins to the missile bone. Every leaf bone skins geometry, so Unity's
         optimizeBones cannot strip it.
  SNIPER   a railgun: a long thin barrel (13 u, 3x the 4.3 u housing) between two rails, five domain coil rings,
           a muzzle brake with an ember bore, a scope tube with a glass lens on a mount block on top
  FAST GUN a "calligraphy pen": a tapered pen-barrel housing with a domain band, a six-barrel rotary cluster
           (ember bores) through a clamp ring and a muzzle ring
  BOMB     a mortar: a boxy cradle and a short fat drum with a domain waist band, a flared wide mouth lip and an
           ember throat

FRAME (preset "zup", FLEET_CONVENTION 1.2): modelled in the Blender frame nose -Y, up +Z, pilot's left +X;
Unity (x, y, z) = (-bx, bz, -by), i.e. Blender (bx, by, bz) = (-ux, -uz, uy). Nested under the hull root with an
identity local transform and scale 1, the nose lands on Unity +Z and up on +Y (Thresher / Grouper precedent).
The turret pivots are OrcaCrewCore.Mounts[s] * hullRadius 9 in Unity hull space, kept EXACT:
  SniperYaw  Unity (0, 16.2, -1.8)     = Blender (0, 1.8, 16.2)
  FastGunYaw Unity (-21.6, 3.15, -4.5) = Blender (21.6, 4.5, 3.15)   (port = pilot's left = Blender +X, .L side)
  BombYaw    Unity (21.6, 3.15, -4.5)  = Blender (-21.6, 4.5, 3.15)  (starboard, .R side)
Every turret bone points along the nose (Blender -Y) with its Z axis up (roll 0 on a -Y bone gives X = -X world,
Z = +Z world), so yaw is about the bone's up and pitch about its right in Blender.

Contract (FLEET_CONVENTION 1-4 for a NEW vessel, with the brief's names):
  file      Assets/_Models/Vessel Models/Orca.fbx (new guid), preset "zup"
  nodes     mesh `Orca`, armature `OrcaArmature` (the brief's names, so the nested model resolves unambiguously
            beside the hidden Sparrow model; the convention's generic `mesh` / `rig` would collide by name); root
            bone `hull`
  slots     0 Domain, 1 Body, 2 Window, 3 Engine
  shapes    Charge (weapon): the three turret housings, cradle and scope grow 2x along the gun / 1.15x across, the
                             barrels, coils, muzzle parts, drum and missile tubes swell; no tip moves
            Mass (volume):   the hull, peduncle, nacelles and engine cases swell, the flukes, fin and outriggers thicken
            Space (reach):   the railgun barrel telescopes 1.35x (at 100% the sniper tip moves 6.1 u forward,
                             the fast gun's 1.7 u, the bomb lip 1.0 u; the muzzle bones mark the REST tips),
                             the fast gun's barrels 1.35x, the mortar drum 1.3x; the flukes span 1.25x; the outrigger
                             nacelles telescope fore and aft about the turret pivot
            Time (speed):    the nose stretches and sharpens, the flukes and the outriggers sweep back, the nacelle
                             tails stretch aft
            No morph moves a turret pivot (each morph is centred on it) or a jet .mouth bone.
  takes     none drive it (code poses the turret bones by name; the Sparrow's Animator stays on the hidden
            Sparrow bones). Four documentation poses for the sheet and the studio, each direction-proved by
            pose_expect: pitchup (flukes curl down), yawright (flukes swing to the outside), aimright (every turret
            yaws 40 deg right), aimup (every turret pitches 25 deg up).
"""

import math
from _common import box_uv

NAME = "Orca"
OUTPUT = "Assets/_Models/Vessel Models/Orca.fbx"

ARMATURE = "OrcaArmature"
MESH_NODE = "Orca"
SLOTS = ("Domain", "Body", "Window", "Engine")
D, H, W, J = 0, 1, 2, 3

# ---------------------------------------------------------------- layout (Blender frame: nose -Y, up +Z, left +X)
SNIPER_P = (0.0, 1.8, 16.2)
FAST_P = (21.6, 4.5, 3.15)
BOMB_P = (-21.6, 4.5, 3.15)

NOSE_Y = -16.0
BODY_END_Y = 9.6
PED_Y0, PED_LEN = 9.0, 4.0
# (y, half-width, half-height top, half-height bottom, centre lift): a conical melon head, the back rising to
# the pylon and falling to the tail stock, a fuller flatter belly (the orca's side profile)
BODY_STATIONS = [
    (NOSE_Y, 0.90, 0.70, 0.60, -0.90),
    (-15.5, 2.05, 1.60, 1.35, -0.80),
    (-14.5, 3.20, 2.50, 2.15, -0.66),
    (-12.8, 4.20, 3.35, 2.90, -0.48),
    (-10.4, 5.00, 4.05, 3.50, -0.24),
    (-7.4, 5.50, 4.60, 3.80, 0.00),
    (-3.6, 5.60, 5.00, 3.85, 0.12),
    (0.0, 5.30, 4.80, 3.60, 0.25),
    (3.4, 4.50, 4.00, 3.00, 0.38),
    (6.4, 3.35, 3.05, 2.25, 0.44),
    (BODY_END_Y, 2.10, 2.15, 1.60, 0.48),
]
N_TOP, N_BOT = 2.35, 2.6

FIN_ROOT = (0.0, -6.0, 3.8)     # leading edge at the root
FIN_SPAN, FIN_CR, FIN_CT, FIN_SWEEP = 11.4, 10.5, 3.6, 6.0

WING_ROOT_X, WING_ROOT_Y, WING_ROOT_Z = 4.45, -9.0, -2.2
WING_CR, WING_CT = 7.6, 4.4
WING_BULGE, WING_LE_BULGE = 2.2, 1.0   # the paddle: chord and leading edge bulge mid-span
NAC_R, NAC_Z = 1.40, 1.80
NAC_Y0, NAC_LEN = -0.2, 9.0

FLUKE_ROOT = (0.0, 12.0, 0.50)
FLUKE_SPAN, FLUKE_CR, FLUKE_CT, FLUKE_SWEEP = 9.6, 4.8, 1.6, 4.6
JET_X, JET_Y0, JET_Z, JET_R = 2.2, 11.5, 0.60, 1.25

# wing geometry derived so the tip sits in the nacelle under the turret pivot
_TIP_X, _TIP_Z = 21.2, NAC_Z
_RUN = _TIP_X - WING_ROOT_X
_RISE = _TIP_Z - WING_ROOT_Z
WING_DIHEDRAL = math.degrees(math.atan2(_RISE, _RUN))
WING_SPAN = math.hypot(_RUN, _RISE)
WING_SWEEP = (FAST_P[1] - WING_CT / 2) - WING_ROOT_Y

# per-turret layout: housing half-width, trunnion height above the pivot, ring scale, missile side (+1 = +X)
TURRET_K = 1.35   # every turret is built at unit dims and scaled about its pivot (the pivot never moves)
TURRETS = {
    "Sniper": dict(P=SNIPER_P, hw=0.85, th=0.95, ring=0.85, side=1, pid=100),
    "FastGun": dict(P=FAST_P, hw=0.95, th=1.25, ring=1.0, side=1, pid=200),
    "Bomb": dict(P=BOMB_P, hw=1.10, th=1.70, ring=1.15, side=-1, pid=300),
}
# pid offsets inside a turret
P_RING, P_YOKE, P_PIN, P_HOUSE, P_GUN, P_BAND, P_MUZ, P_SCOPE, P_POD, P_PODMOUTH, P_RAIL = range(1, 12)


def _lerp_station(y, k):
    """Station parameter k at y, Catmull-Rom through the stations (a smooth hull, no kinks at the stations)."""
    st = BODY_STATIONS
    if y <= st[0][0]:
        return st[0][k]
    if y >= st[-1][0]:
        return st[-1][k]
    for j in range(len(st) - 1):
        if y <= st[j + 1][0]:
            break
    p0, p1, p2, p3 = st[max(0, j - 1)], st[j], st[j + 1], st[min(len(st) - 1, j + 2)]
    t = (y - p1[0]) / (p2[0] - p1[0])
    # non-uniform spacing: tangents by finite differences in y, Hermite form
    def slope(a, b):
        return (b[k] - a[k]) / max(1e-6, b[0] - a[0])
    h = p2[0] - p1[0]
    m1 = slope(p0, p2) if p0 is not p1 else slope(p1, p2)
    m2 = slope(p1, p3) if p3 is not p2 else slope(p1, p2)
    t2, t3 = t * t, t * t * t
    return ((2 * t3 - 3 * t2 + 1) * p1[k] + (t3 - 2 * t2 + t) * h * m1 + (-2 * t3 + 3 * t2) * p2[k] + (t3 - t2) * h * m2)


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
        return o

    def lathe_fwd(name, prof, origin, seg=16):
        """A lathe whose profile distance runs FORWARD (toward the nose, -Y) from `origin`."""
        return f.lathe(name, prof, segments=seg, axis=(0, -1, 0), origin=origin)

    def lathe_aft(name, prof, origin, seg=16):
        return f.lathe(name, prof, segments=seg, axis=(0, 1, 0), origin=origin)

    def lathe_up(name, prof, origin, seg=24):
        return f.lathe(name, prof, segments=seg, axis=(0, 0, 1), origin=origin)

    parts = []          # one object per BONE
    info = {}           # numbers the morphs and the rig need

    # ================================================================== HULL
    hull_parts = []
    length = BODY_END_Y - NOSE_Y
    # resample the stations densely (smooth, and fine enough that the domain pattern follows its curves)
    ys = [NOSE_Y, NOSE_Y + 0.12, NOSE_Y + 0.35]
    y = NOSE_Y + 0.75
    while y < BODY_END_Y - 0.2:
        ys.append(round(y, 3))
        y += 0.55 if y < NOSE_Y + 2.8 else 1.05
    ys.append(BODY_END_Y)
    st = [((y - NOSE_Y) / length, half_w(y), _lerp_station(y, 2), _lerp_station(y, 3), N_TOP, N_BOT, _lerp_station(y, 4))
          for y in ys]
    body = f.fuselage("body", length, nose=(0, NOSE_Y, 0.0), axis=(0, 1, 0), segments=36, stations=st)
    f.ensure_slots(body, SLOTS)
    f.paint(body, H)

    def flank_mid(y):
        return -2.6 + 0.42 * (y - 0.4)

    def flank_half(y):
        t = (y - 0.4) / 9.0
        return 1.5 * max(0.0, math.sin(math.pi * min(max(t, 0.0), 1.0))) ** 0.6

    # the orca's white: a flank sweep per side rising toward the tail, the saddle behind the pylon (black between
    # them), the chin and belly
    # three panel seams round the hull (dark grooves; never through a plate)
    for yseam in (-12.4, -7.0, -1.2):
        yc = min(ys, key=lambda v: abs(v - yseam))       # a seam is one ring of faces: the one starting at the nearest station
        nxt = ys[ys.index(yc) + 1]
        mid, half = (yc + nxt) / 2, (nxt - yc) / 2
        f.inset_faces(body, lambda c, n, i, mid=mid, half=half: i == H and abs(c.y - mid) < half * 0.6, 0.06, depth=0.06)
    hull_parts.append(piece(body, None, 1))

    # the orca's white -> CONFORMAL DOMAIN ARMOUR PLATES on the hull (a plate's outline is a smooth curve on the
    # surface, not a stair of painted faces): a flank sweep per side rising toward the tail, the saddle behind the
    # pylon (black hull between them), the chin-and-belly plate
    def surf(y, th):
        hw, dz = half_w(y), _lerp_station(y, 4)
        c, s_ = math.cos(th), math.sin(th)
        hh, nn = (_lerp_station(y, 2), N_TOP) if s_ >= 0 else (_lerp_station(y, 3), N_BOT)
        return Vector((hw * math.copysign(abs(c) ** (2.0 / nn), c), y, dz + hh * math.copysign(abs(s_) ** (2.0 / nn), s_)))

    def surf_n(y, th):
        e = 1e-3
        du = surf(y + e, th) - surf(y - e, th)
        dv = surf(y, th + e) - surf(y, th - e)
        nrm = dv.cross(du)
        return nrm.normalized() if nrm.length > 1e-9 else Vector((0, 0, 1))

    def conformal_plate(name, y0, y1, band, nu, nv, out=0.07, inn=0.22):
        """A plate lying on the hull between y0..y1 whose angular extent at y is band(y) -> (th_lo, th_hi); its
        outline is pinched to a point where the band closes. Closed solid: outer skin, inner skin, walls."""
        bm = bmesh.new()
        grid_o, grid_i = [], []
        for a in range(nu + 1):
            y = y0 + (y1 - y0) * a / nu
            lo, hi = band(y)
            ro, ri = [], []
            for b in range(nv + 1):
                th = lo + (hi - lo) * b / nv
                p0, nn = surf(y, th), surf_n(y, th)
                ro.append(bm.verts.new(p0 + nn * out))
                ri.append(bm.verts.new(p0 - nn * inn))
            grid_o.append(ro)
            grid_i.append(ri)
        for a in range(nu):
            for b in range(nv):
                bm.faces.new((grid_o[a][b], grid_o[a + 1][b], grid_o[a + 1][b + 1], grid_o[a][b + 1]))
                bm.faces.new((grid_i[a][b], grid_i[a][b + 1], grid_i[a + 1][b + 1], grid_i[a + 1][b]))
        for a in range(nu):   # the two long walls
            bm.faces.new((grid_o[a][0], grid_i[a][0], grid_i[a + 1][0], grid_o[a + 1][0]))
            bm.faces.new((grid_o[a][nv], grid_o[a + 1][nv], grid_i[a + 1][nv], grid_i[a][nv]))
        for b in range(nv):   # the two end walls
            bm.faces.new((grid_o[0][b], grid_o[0][b + 1], grid_i[0][b + 1], grid_i[0][b]))
            bm.faces.new((grid_o[nu][b], grid_i[nu][b], grid_i[nu][b + 1], grid_o[nu][b + 1]))
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        return f.new_mesh_object(name, bm)

    def lens(y, y0, y1, p=0.6):
        t = (y - y0) / (y1 - y0)
        return max(0.0, math.sin(math.pi * min(max(t, 0.0), 1.0))) ** p

    for sx in (1, -1):
        def flank(y, sx=sx):
            mid = -0.78 + 1.30 * ((y - 0.2) / 9.3) ** 1.15
            hwid = 0.04 + 0.40 * lens(y, 0.2, 9.5, 0.55)
            lo, hi = mid - hwid, mid + hwid
            return (lo, hi) if sx > 0 else (math.pi - hi, math.pi - lo)
        pl = conformal_plate("flankplate", 0.2, 9.5, flank, 20, 5)
        hull_parts.append(piece(pl, D, 4))
    sad = conformal_plate("saddle", 5.0, 9.2, lambda y: (math.pi / 2 - 0.02 - 0.42 * lens(y, 5.0, 9.2, 0.5),
                                                         math.pi / 2 + 0.02 + 0.42 * lens(y, 5.0, 9.2, 0.5)), 12, 6)
    hull_parts.append(piece(sad, D, 4))

    def belly(y):
        w = 0.05 + 0.62 * lens(y, -15.4, 1.6, 0.35) * (1.0 - 0.35 * morphs.smoothstep(-11.0, -6.0, y))
        return (1.5 * math.pi - w, 1.5 * math.pi + w)
    bel = conformal_plate("belly", -15.4, 1.6, belly, 20, 5)
    hull_parts.append(piece(bel, D, 4))

    # the eye patch -> a long tilted sensor lens on each side of the head, in a dark bezel, pressed into the hull
    def surf_x(y, z):
        hw, dz = half_w(y), _lerp_station(y, 4)
        hh = _lerp_station(y, 2) if z >= dz else _lerp_station(y, 3)
        nn = N_TOP if z >= dz else N_BOT
        v = min(0.999, abs(z - dz) / hh)
        return hw * (1 - v ** nn) ** (1.0 / nn)
    ey, ez = -10.6, 1.0
    for sx in (1, -1):
        ex = surf_x(ey, ez)
        rot = Matrix.Rotation(math.radians(-sx * 13), 4, "Z") @ Matrix.Rotation(math.radians(17), 4, "X") \
            @ Matrix.Rotation(math.radians(-sx * 24), 4, "Y")
        for nm, sc, inset, slot in (("eyebezel", (0.34, 3.15, 1.12), 0.18, H), ("eyelens", (0.30, 2.80, 0.90), 0.07, W)):
            e = f.sphere(nm, 1.0, center=(0, 0, 0), segments=14, rings=7, scale=sc)
            f.transform_mesh(e, Matrix.Translation(Vector((sx * (ex - inset), ey, ez))) @ rot)
            hull_parts.append(piece(e, slot, 2))
    # the helm's canopy where the blowhole is: glass in a dark frame
    cy = -11.4
    frame = f.sphere("canopyframe", 1.0, center=(0, cy + 0.1, top_z(cy) - 0.55), segments=16, rings=8, scale=(1.42, 3.15, 0.90))
    hull_parts.append(piece(frame, H, 2))
    can = f.sphere("canopy", 1.0, center=(0, cy, top_z(cy) - 0.38), segments=16, rings=8, scale=(1.20, 2.80, 0.85))
    hull_parts.append(piece(can, W, 2))
    # a dorsal spine rail from the canopy to the pylon: a dark structural ridge
    rail_pts = [(0.0, y, top_z(y) - 0.18) for y in (-9.0, -8.0, -6.9, -5.8)]
    rail = f.tube_along("spine", rail_pts, [0.22, 0.38, 0.48, 0.50], segments=10)
    hull_parts.append(piece(rail, H, 3))
    # the outrigger roots: hinge fairings on the lower flanks
    for sx in (1, -1):
        fr = f.sphere("fairing", 1.0, center=(sx * (WING_ROOT_X + 0.05), WING_ROOT_Y + 3.6, WING_ROOT_Z + 0.05),
                      segments=14, rings=7, scale=(0.90, 3.8, 0.75))
        hull_parts.append(piece(fr, H, 3))
    parts.append(bone_part(hull_parts, "hull"))

    # ================================================================== DORSAL PYLON (tall dorsal fin)
    fin_parts = []
    blade = f.wing("pylon", root=FIN_ROOT, span=FIN_SPAN, chord_root=FIN_CR, chord_tip=FIN_CT, sweep=FIN_SWEEP,
                   forward=(0, -1, 0), up=(-1, 0, 0), thickness=0.165, sections=7, segments=22, tip_round=False,
                   taper_power=0.85)
    f.ensure_slots(blade, SLOTS)
    f.paint(blade, H)
    # a domain rank band high on the pylon (both faces), and a thin domain leading-edge trim below it
    f.paint(blade, D, lambda c, n, i: 11.45 < c.z < 12.95)

    def fin_te(c, n, i):
        t = max(0.0, (c.z - FIN_ROOT[2]) / FIN_SPAN) ** 0.85
        le = FIN_ROOT[1] + FIN_SWEEP * t
        return 5.2 < c.z < 11.3 and (c.y - le) / (FIN_CR + (FIN_CT - FIN_CR) * t) > 0.86
    f.paint(blade, D, fin_te)     # a domain trim line up the pylon's trailing edge: the fin reads from behind
    blade = piece(blade, None, 10)
    f.sharpen_by_angle(blade, 38)
    fin_parts.append(blade)
    P = Vector(SNIPER_P)
    ped = lathe_up("pedestal", [(0.0, -2.2), (1.15, -2.2), (1.35, -1.45), (1.80, -0.95), (1.80, -0.62), (1.5, -0.50), (0.0, -0.50)],
                   origin=P, seg=24)
    f.transform_mesh(ped, Matrix.Translation(P) @ Matrix.Diagonal((1.0, 1.32, 1.0, 1.0)) @ Matrix.Translation(-P))
    f.ensure_slots(ped, SLOTS)
    f.paint(ped, H)
    f.paint(ped, D, lambda c, n, i: abs(n.z) < 0.3 and c.z > P.z - 0.85)
    fin_parts.append(piece(ped, None, 11, bevel=0.02))
    parts.append(bone_part(fin_parts, "fin"))

    # ================================================================== OUTRIGGERS (pectoral fins) + nacelles
    span_dir = {}
    cosd = math.cos(math.radians(WING_DIHEDRAL))

    def paddle_chord(t):
        return WING_CR + (WING_CT - WING_CR) * t + WING_BULGE * math.sin(math.pi * t) ** 1.3

    def paddle_le(t):
        return WING_ROOT_Y + WING_SWEEP * t - WING_LE_BULGE * math.sin(math.pi * t)

    def paddle(name, sx, n_sec=7):
        """The pectoral paddle: airfoil sections whose chord swells mid-span and whose leading edge bows forward."""
        sts = []
        for k in range(n_sec + 1):
            t = k / n_sec
            ch = paddle_chord(t)
            le = Vector((sx * (WING_ROOT_X + _RUN * t), paddle_le(t), WING_ROOT_Z + _RISE * t))
            prof = f.airfoil(chord=ch, thickness=0.105 - 0.02 * t, camber=0.02, segments=16)
            sts.append(f.Station(le, prof, right=(0, 1, 0), up=(0, 0, 1)))
        return f.loft(name, sts)
    for tg, sx, tp in (("L", 1, FAST_P), ("R", -1, BOMB_P)):
        wp = []
        root = Vector((sx * WING_ROOT_X, WING_ROOT_Y, WING_ROOT_Z))
        wg = paddle("outrigger", sx)
        f.ensure_slots(wg, SLOTS)
        f.paint(wg, H)

        def wing_panel(c, n, i, sx=sx):
            t = (sx * c.x - WING_ROOT_X) / _RUN
            if not (0.27 < t < 0.73) or n.z < 0.3:
                return False
            frac = (c.y - paddle_le(t)) / paddle_chord(t)
            return 0.22 < frac < 0.62
        f.inset_faces(wg, wing_panel, 0.10, depth=0.04, slot=D)
        wp.append(piece(wg, None, 20 if sx > 0 else 25))
        f.sharpen_by_angle(wp[-1], 38)
        d = math.radians(WING_DIHEDRAL)
        span_dir[sx] = Vector((sx * math.cos(d), 0.0, math.sin(d)))
        # the nacelle the turret stands on
        nx = tp[0]
        q = NAC_R / 1.15
        prof = [(0.0, 0.0), (0.45 * q, 0.08), (0.85 * q, 0.45), (NAC_R, 1.15), (NAC_R, 1.55), (NAC_R, 2.05), (NAC_R, NAC_LEN - 1.7),
                (1.0 * q, NAC_LEN - 0.8), (0.80 * q, NAC_LEN), (0.62 * q, NAC_LEN), (0.55 * q, NAC_LEN - 0.25), (0.0, NAC_LEN - 0.25)]
        nac = lathe_aft("nacelle", prof, origin=(nx, NAC_Y0, NAC_Z), seg=16)
        f.ensure_slots(nac, SLOTS)
        f.paint(nac, H)
        f.paint(nac, D, lambda c, n, i: NAC_Y0 + 1.55 < c.y < NAC_Y0 + 2.05)
        f.paint(nac, J, lambda c, n, i, nx=nx: c.y > NAC_Y0 + NAC_LEN - 0.3 and math.hypot(c.x - nx, c.z - NAC_Z) < 0.62 * NAC_R / 1.15)
        f.paint(nac, H, lambda c, n, i: c.y < NAC_Y0 + 0.3)
        wp.append(piece(nac, None, 21 if sx > 0 else 26))
        f.sharpen_by_angle(wp[-1], 35)
        # the turret pedestal on the nacelle's back
        TP = Vector(tp)
        pd = lathe_up("ringseat", [(0.0, -1.0), (1.75, -1.0), (1.95, -0.66), (1.80, -0.48), (0.0, -0.48)], origin=TP, seg=24)
        wp.append(piece(pd, H, 22 if sx > 0 else 27, bevel=0.02))
        # a running light on the nacelle's outboard flank (domain, never lime)
        lt = f.sphere("runlight", 0.16, center=(nx + sx * (NAC_R - 0.02), NAC_Y0 + 1.8, NAC_Z + 0.15), segments=10, rings=6,
                      scale=(1.0, 2.2, 1.0))
        wp.append(piece(lt, D, 23 if sx > 0 else 28))
        parts.append(bone_part(wp, "wing." + tg))

    # ================================================================== PEDUNCLE (tail)
    tp_ = []
    ped = f.fuselage("peduncle", PED_LEN, nose=(0, PED_Y0, 0.48), axis=(0, 1, 0), segments=24,
                     stations=[(0.0, 2.20, 2.20, 1.66, 2.4, 2.4, 0.0), (0.5, 1.55, 1.75, 1.20, 2.4, 2.4, 0.02),
                               (1.0, 1.05, 1.10, 0.85, 2.4, 2.4, 0.03)])
    tp_.append(piece(ped, H, 30))
    col = lathe_aft("collar", [(0.0, 0.0), (2.28, 0.0), (2.44, 0.12), (2.44, 0.40), (2.28, 0.52), (0.0, 0.52)],
                    origin=(0, PED_Y0 - 0.05, 0.48), seg=24)
    f.transform_mesh(col, Matrix.Translation(Vector((0, 0, 0.48))) @ Matrix.Diagonal((1.0, 1.0, 0.86, 1.0))
                     @ Matrix.Translation(Vector((0, 0, -0.48))))
    f.ensure_slots(col, SLOTS)
    f.paint(col, H)
    f.paint(col, D, lambda c, n, i: abs(n.y) < 0.3)
    tp_.append(piece(col, None, 31))
    parts.append(bone_part(tp_, "tail"))

    # ================================================================== FLUKES (tail.2) - the engine section
    fl = []
    for sx in (1, -1):
        fk = f.wing("fluke", root=FLUKE_ROOT, span=FLUKE_SPAN, chord_root=FLUKE_CR, chord_tip=FLUKE_CT, sweep=FLUKE_SWEEP,
                    dihedral_deg=0.0, thickness=0.14, sections=6, segments=20, side=sx, tip_round=True)
        f.ensure_slots(fk, SLOTS)
        f.paint(fk, H)
        f.paint(fk, D, lambda c, n, i: n.z < -0.5)

        def te_trim(c, n, i, sx=sx):
            t = min(1.0, max(0.0, sx * c.x / FLUKE_SPAN))
            le = FLUKE_ROOT[1] + FLUKE_SWEEP * t
            ch = FLUKE_CR + (FLUKE_CT - FLUKE_CR) * t
            return n.z > 0.5 and (c.y - le) / ch > 0.80 and sx * c.x > 2.75
        f.paint(fk, D, te_trim)
        fl.append(piece(fk, None, 35))
    parts.append(bone_part(fl, "tail.2"))

    # main engines: a case on each fluke root (jet.L / jet.R) and its bell (jet.X.mouth)
    for tg, sx in (("L", 1), ("R", -1)):
        x = sx * JET_X
        R = JET_R
        shell = lathe_aft("jetcase", [(0.0, 0.0), (0.47 * R, 0.08), (0.86 * R, 0.6), (R, 1.45), (R, 2.9), (0.0, 2.9)],
                          origin=(x, JET_Y0, JET_Z), seg=20)
        f.ensure_slots(shell, SLOTS)
        f.paint(shell, H)
        f.paint(shell, D, lambda c, n, i: JET_Y0 + 1.75 < c.y < JET_Y0 + 2.05)
        parts.append(bone_part([piece(shell, None, 40 if sx > 0 else 41)], "jet." + tg))
        bell = lathe_aft("jetbell", [(0.0, 0.0), (0.97 * R, 0.0), (1.07 * R, 0.2), (1.12 * R, 0.55), (0.86 * R, 0.55), (0.74 * R, 0.36),
                                     (0.0, 0.36)], origin=(x, JET_Y0 + 2.7, JET_Z), seg=20)
        f.ensure_slots(bell, SLOTS)
        f.paint(bell, H)
        f.paint(bell, J, lambda c, n, i, x=x: c.y > JET_Y0 + 2.9 and math.hypot(c.x - x, c.z - JET_Z) < 0.83 * JET_R)
        parts.append(bone_part([piece(bell, None, 42 if sx > 0 else 43)], "jet." + tg + ".mouth"))
    info["jet_mouth"] = {tg: (sx * JET_X, JET_Y0 + 3.25, JET_Z) for tg, sx in (("L", 1), ("R", -1))}

    # ================================================================== TURRETS
    def missile_tube(name_root, T, x, z, length, pid0):
        """A missile tube on the outboard end of the trunnion pin; returns (pod parts, mouth part, mouth point)."""
        y0 = T.y + length * 0.42
        pod = lathe_fwd(name_root + "pod", [(0.0, 0.0), (0.26, 0.04), (0.36, 0.30), (0.36, length - 0.35), (0.0, length - 0.35)],
                        origin=(x, y0, z), seg=14)
        f.ensure_slots(pod, SLOTS)
        f.paint(pod, H)
        f.paint(pod, D, lambda c, n, i, y0=y0: y0 - 0.95 < c.y < y0 - 0.65)
        pod = piece(pod, None, pid0 + P_POD)
        mouth = lathe_fwd(name_root + "mouth", [(0.0, 0.0), (0.40, 0.0), (0.45, 0.08), (0.45, 0.42), (0.27, 0.42), (0.27, 0.34), (0.0, 0.34)],
                          origin=(x, y0 - (length - 0.40), z), seg=14)
        f.ensure_slots(mouth, SLOTS)
        f.paint(mouth, H)
        f.paint(mouth, J, lambda c, n, i, x=x, z=z: n.y < -0.9 and math.hypot(c.x - x, c.z - z) < 0.26)
        mouth = piece(mouth, None, pid0 + P_PODMOUTH)
        return pod, mouth, Vector((x, y0 - (length - 0.40) - 0.42, z))

    def base_and_yoke(t, P, T, hw, ring, pid0):
        k = ring
        rg = lathe_up(t + "ring", [(0.0, -0.55 * k), (1.35 * k, -0.55 * k), (1.55 * k, -0.35 * k), (1.55 * k, 0.0),
                                   (1.42 * k, 0.18 * k), (0.0, 0.18 * k)], origin=P, seg=20)
        f.ensure_slots(rg, SLOTS)
        f.paint(rg, H)
        f.paint(rg, D, lambda c, n, i: abs(n.z) < 0.3 and c.z < P.z - 0.03)
        out = [piece(rg, None, pid0 + P_RING, bevel=0.015)]
        plate_x = hw + 0.20
        top = T.z + 0.42
        bot = P.z + 0.10 * k
        for sx in (1, -1):
            pl = f.box(t + "yoke", (0.22, 1.15, top - bot), center=(P.x + sx * plate_x, P.y + 0.05, (top + bot) / 2))
            out.append(piece(pl, H, pid0 + P_YOKE, bevel=0.04))
        return out, plate_x

    def trunnion(t, T, plate_x, side, pod_x, pid0):
        x_neg = -(plate_x + 0.24) if side > 0 else -pod_x
        x_pos = pod_x if side > 0 else (plate_x + 0.24)
        pin = f.lathe(t + "pin", [(0.0, 0.0), (0.26, 0.0), (0.30, 0.06), (0.30, x_pos - x_neg - 0.06), (0.26, x_pos - x_neg), (0.0, x_pos - x_neg)],
                      segments=12, axis=(1, 0, 0), origin=(T.x + x_neg, T.y, T.z))
        return piece(pin, H, pid0 + P_PIN)

    turret = {}
    for t, cfg in TURRETS.items():
        P = Vector(cfg["P"])
        T = P + Vector((0, 0, cfg["th"]))
        hw, side, pid0 = cfg["hw"], cfg["side"], cfg["pid"]
        yaw_parts, plate_x = base_and_yoke(t, P, T, hw, cfg["ring"], pid0)
        pod_x = plate_x + 0.60
        pitch, muz = [], []
        pitch.append(trunnion(t, T, plate_x, side, pod_x, pid0))

        if t == "Sniper":
            hs = f.fuselage("snhouse", 4.3, nose=(T.x, T.y - 2.1, T.z + 0.05), axis=(0, 1, 0), segments=16,
                            stations=[(0.0, 0.42, 0.34, 0.30, 3.0, 3.0, 0.0), (0.12, 0.74, 0.62, 0.52, 3.2, 3.0, 0.0),
                                      (0.5, 0.85, 0.75, 0.60, 3.4, 3.0, 0.02), (0.86, 0.80, 0.70, 0.55, 3.2, 3.0, 0.0),
                                      (1.0, 0.55, 0.46, 0.40, 3.0, 3.0, 0.0)])
            pitch.append(piece(hs, H, pid0 + P_HOUSE))
            BL = 13.0
            bo = Vector((T.x, T.y - 1.9, T.z + 0.05))
            info["sniper_barrel"] = (bo.copy(), BL)
            br = lathe_fwd("snbarrel", [(0.0, 0.0), (0.44, 0.0), (0.44, 0.5), (0.31, 0.78), (0.27, 0.92), (0.27, BL - 0.9), (0.0, BL - 0.9)],
                           origin=bo, seg=12)
            pitch.append(piece(br, H, pid0 + P_GUN))
            for sx in (1, -1):
                rl = f.box("snrail", (0.12, BL - 2.6, 0.36), center=(bo.x + sx * 0.30, bo.y - (1.3 + (BL - 2.6) / 2), bo.z))
                pitch.append(piece(rl, H, pid0 + P_RAIL, bevel=0.02))
            for dd in (2.6, 4.6, 6.6, 8.6, 10.6):
                co = lathe_fwd("sncoil", [(0.0, 0.0), (0.42, 0.0), (0.54, 0.08), (0.54, 0.36), (0.42, 0.44), (0.0, 0.44)],
                               origin=bo + Vector((0, -dd, 0)), seg=10)
                pitch.append(piece(co, D, pid0 + P_BAND))
            bk = lathe_fwd("snbrake", [(0.0, 0.0), (0.36, 0.0), (0.42, 0.10), (0.42, 1.0), (0.24, 1.0), (0.24, 0.92), (0.0, 0.92)],
                           origin=bo + Vector((0, -(BL - 1.0), 0)), seg=14)
            f.ensure_slots(bk, SLOTS)
            f.paint(bk, H)
            f.paint(bk, J, lambda c, n, i, bo=bo: n.y < -0.9 and math.hypot(c.x - bo.x, c.z - bo.z) < 0.23)
            muz.append(piece(bk, None, pid0 + P_MUZ))
            # vent slots along the brake's flanks
            for k in range(3):
                vs = f.box("snvent", (0.88, 0.10, 0.12), center=(bo.x, bo.y - (BL - 0.75) - 0.2 * k, bo.z))
                muz.append(piece(vs, H, pid0 + P_MUZ))
            tip = bo + Vector((0, -BL, 0))
            # scope on a mount block
            mb = f.box("snmount", (0.42, 1.1, 0.62), center=(T.x, T.y - 0.25, T.z + 0.95))
            pitch.append(piece(mb, H, pid0 + P_SCOPE, bevel=0.04))
            so = (T.x, T.y + 1.6, T.z + 1.40)
            sc = lathe_fwd("snscope", [(0.0, 0.0), (0.24, 0.0), (0.31, 0.14), (0.31, 2.55), (0.39, 2.68), (0.39, 3.2), (0.0, 3.2)],
                           origin=so, seg=14)
            f.ensure_slots(sc, SLOTS)
            f.paint(sc, H)
            f.inset_faces(sc, lambda c, n, i: n.y < -0.9, 0.06, depth=0.05, slot=W)
            pitch.append(piece(sc, None, pid0 + P_SCOPE))
            pod, mouth, mm = missile_tube("sn", T, T.x + side * pod_x, T.z, 3.6, pid0)

        elif t == "FastGun":
            ho = (T.x, T.y + 2.2, T.z)
            hs = lathe_fwd("fghouse", [(0.0, 0.0), (0.45, 0.05), (0.80, 0.35), (0.95, 0.8), (0.95, 1.5), (0.95, 1.75),
                                       (0.95, 2.2), (0.88, 2.5), (0.62, 3.4), (0.50, 3.8), (0.0, 3.8)], origin=ho, seg=16)
            f.ensure_slots(hs, SLOTS)
            f.paint(hs, H)
            f.paint(hs, D, lambda c, n, i: ho[1] - 1.72 < c.y < ho[1] - 1.53)
            pitch.append(piece(hs, None, pid0 + P_HOUSE))
            hub = lathe_fwd("fghub", [(0.0, 0.0), (0.64, 0.0), (0.64, 0.32), (0.0, 0.32)], origin=(T.x, T.y - 1.5, T.z), seg=18)
            pitch.append(piece(hub, H, pid0 + P_BAND, bevel=0.02))
            bo = Vector((T.x, T.y - 1.7, T.z))
            BL = 3.5
            info["fast_barrel"] = (bo.copy(), BL)
            for k in range(6):
                a = 2 * math.pi * k / 6 + math.pi / 6
                b = lathe_fwd("fgbarrel", [(0.0, 0.0), (0.12, 0.0), (0.12, BL), (0.0, BL)],
                              origin=(bo.x + 0.40 * math.cos(a), bo.y, bo.z + 0.40 * math.sin(a)), seg=8)
                f.ensure_slots(b, SLOTS)
                f.paint(b, H)
                f.paint(b, J, lambda c, n, i: n.y < -0.9)
                pitch.append(piece(b, None, pid0 + P_GUN))
            cl = lathe_fwd("fgclamp", [(0.0, 0.0), (0.60, 0.0), (0.66, 0.08), (0.66, 0.24), (0.60, 0.32), (0.0, 0.32)],
                           origin=bo + Vector((0, -2.1, 0)), seg=18)
            pitch.append(piece(cl, H, pid0 + P_BAND))
            mr = lathe_fwd("fgmuzzle", [(0.0, 0.0), (0.56, 0.0), (0.62, 0.06), (0.62, 0.16), (0.56, 0.22), (0.0, 0.22)],
                           origin=bo + Vector((0, -(BL - 0.42), 0)), seg=18)
            muz.append(piece(mr, D, pid0 + P_MUZ))
            tip = bo + Vector((0, -BL, 0))
            pod, mouth, mm = missile_tube("fg", T, T.x + side * pod_x, T.z, 3.4, pid0)

        else:  # Bomb
            cr = f.box("bmcradle", (2.2, 2.1, 1.7), center=(T.x, T.y + 0.75, T.z - 0.05))
            pitch.append(piece(cr, H, pid0 + P_HOUSE, bevel=0.14))
            do = Vector((T.x, T.y - 0.1, T.z))
            info["bomb_drum"] = do.copy()
            dr = lathe_fwd("bmdrum", [(0.0, 0.0), (1.0, 0.0), (1.18, 0.12), (1.18, 0.90), (1.29, 0.95), (1.29, 1.30), (1.18, 1.35),
                                      (1.18, 1.92), (1.26, 2.0), (0.95, 2.0), (0.90, 1.78), (0.0, 1.78)], origin=do, seg=20)
            f.ensure_slots(dr, SLOTS)
            f.paint(dr, H)
            f.paint(dr, D, lambda c, n, i, do=do: do.y - 1.30 < c.y < do.y - 0.95 and math.hypot(c.x - do.x, c.z - do.z) > 1.22)
            f.paint(dr, J, lambda c, n, i, do=do: c.y < do.y - 1.70 and math.hypot(c.x - do.x, c.z - do.z) < 0.97)
            pitch.append(piece(dr, None, pid0 + P_GUN))
            lo = do + Vector((0, -1.9, 0))
            lip = f.lathe("bmlip", [(1.0, 0.0), (1.30, 0.0), (1.56, 0.42), (1.56, 0.60), (1.16, 0.60), (1.0, 0.34), (1.0, 0.0)],
                          segments=20, axis=(0, -1, 0), origin=lo, cap=False)
            f.ensure_slots(lip, SLOTS)
            f.paint(lip, H)
            f.paint(lip, J, lambda c, n, i, lo=lo: math.hypot(c.x - lo.x, c.z - lo.z) < 1.12 and c.y < lo.y - 0.05)
            muz.append(piece(lip, None, pid0 + P_MUZ))
            tip = lo + Vector((0, -0.60, 0))
            pod, mouth, mm = missile_tube("bm", T, T.x + side * pod_x, T.z - 0.25, 3.0, pid0)

        pitch.append(pod)
        # scale the whole turret about its pivot: the pivot stays exact, everything else grows to read at 250 px
        K = TURRET_K
        M = Matrix.Translation(P) @ Matrix.Scale(K, 4) @ Matrix.Translation(-P)
        for o in yaw_parts + pitch + muz + [mouth]:
            f.transform_mesh(o, M)

        def sc(v, P=P, K=K):
            return P + (Vector(v) - P) * K
        pod_c = sc((T.x + side * pod_x, T.y, T.z - (0.25 if t == "Bomb" else 0.0)))
        T, tip, mm = sc(T), sc(tip), sc(mm)
        for key in ("sniper_barrel", "fast_barrel"):
            if key in info and info[key][0] is not None and not info.get(key + "_k"):
                info[key] = (sc(info[key][0]), info[key][1] * K)
                info[key + "_k"] = True
        if t == "Bomb":
            info["bomb_drum"] = sc(info["bomb_drum"])
        turret[t] = dict(P=P, T=T, tip=tip, missile=mm, plate_x=plate_x, pod_x=pod_x, pod_c=pod_c)
        parts.append(bone_part(yaw_parts, t + "Yaw"))
        parts.append(bone_part(pitch, t + "Pitch"))
        parts.append(bone_part(muz, t + "Muzzle"))
        parts.append(bone_part([mouth], t + "Missile"))

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
    f.sharpen_by_angle(mesh, 38)
    box_uv(mesh)
    ctx.log("orca joined: %d verts, %d tris (n-gons triangulated: %d)" % (len(mesh.data.vertices), f.tri_count(mesh), len(big)))

    # ================================================================== RIG
    B = rig.BoneSpec
    fwd = Vector((0, -1, 0))
    bones = [B("hull", (0, 0, 0), (0, -1.6, 0))]
    bones.append(B("fin", (0, -0.4, 2.8), (0, 1.8, 14.4), parent="hull"))
    wing_root_mid = {}
    for tg, sx in (("L", 1), ("R", -1)):
        h = Vector((sx * WING_ROOT_X, WING_ROOT_Y + WING_CR * 0.45, WING_ROOT_Z + 0.1))
        wing_root_mid[sx] = h
        bones.append(B("wing." + tg, h, h + span_dir[sx] * (WING_SPAN * 0.9), parent="hull"))
    bones.append(B("tail", (0, PED_Y0 + 0.3, 0.48), (0, PED_Y0 + 3.4, 0.5), parent="hull"))
    bones.append(B("tail.2", (0, FLUKE_ROOT[1] + 0.5, FLUKE_ROOT[2]), (0, FLUKE_ROOT[1] + 3.5, FLUKE_ROOT[2]), parent="tail"))
    for tg, sx in (("L", 1), ("R", -1)):
        x = sx * JET_X
        bones.append(B("jet." + tg, (x, JET_Y0 + 0.3, JET_Z), (x, JET_Y0 + 2.6, JET_Z), parent="tail.2"))
        m = Vector(info["jet_mouth"][tg])
        bones.append(B("jet." + tg + ".mouth", m, m + Vector((0, 0.8, 0)), parent="jet." + tg))
    host = {"Sniper": "fin", "FastGun": "wing.L", "Bomb": "wing.R"}
    for t, tt in turret.items():
        bones.append(B(t + "Yaw", tt["P"], tt["P"] + fwd * 1.2, parent=host[t]))
        bones.append(B(t + "Pitch", tt["T"], tt["T"] + fwd * 1.2, parent=t + "Yaw"))
        bones.append(B(t + "Muzzle", tt["tip"], tt["tip"] + fwd * 0.8, parent=t + "Pitch"))
        bones.append(B(t + "Missile", tt["missile"], tt["missile"] + fwd * 0.8, parent=t + "Pitch"))
    arm = rig.build_armature(ARMATURE, bones)
    rig.skin_rigid(mesh, arm, default_bone="hull")
    bone_names = [b.name for b in arm.data.bones]
    for t in turret:
        for suffix in ("Yaw", "Pitch", "Muzzle", "Missile"):
            b = arm.data.bones[t + suffix]
            ml = b.matrix_local
            ctx.log("bone %-15s head %s  X %s  Y %s  Z %s" % (
                b.name, tuple(round(v, 3) for v in b.head_local),
                tuple(round(v, 2) for v in ml.col[0][:3]), tuple(round(v, 2) for v in ml.col[1][:3]), tuple(round(v, 2) for v in ml.col[2][:3])))

    # ================================================================== MORPHS
    me = mesh.data
    nv = len(me.vertices)
    pid = [0.0] * nv
    me.attributes["pid"].data.foreach_get("value", pid)

    def pm(*ids):
        s = set(float(i) for i in ids)
        return [1.0 if p in s else 0.0 for p in pid]

    Z0 = 0.15

    def custom(mask, fn):
        def g(i, co, n):
            if mask[i] <= 0:
                return Vector((0, 0, 0))
            return fn(co, n) * mask[i]
        return g

    def radial(mask, cx, cz, factor):
        """Scale across a fore-aft axis through (cx, cz): radius only, length kept."""
        return custom(mask, lambda co, n: Vector(((co.x - cx) * (factor - 1), 0, (co.z - cz) * (factor - 1))))

    def scale_xyz(mask, c, sx, sy, sz):
        return custom(mask, lambda co, n: Vector(((co.x - c.x) * (sx - 1), (co.y - c.y) * (sy - 1), (co.z - c.z) * (sz - 1))))

    def ring_xy(mask, c, factor):
        return custom(mask, lambda co, n: Vector(((co.x - c.x) * (factor - 1), (co.y - c.y) * (factor - 1), 0)))

    body_m = pm(1, 4)
    hullish = pm(1, 2, 3, 4)
    charge, mass, space, timee = [], [], [], []
    for t, cfg in TURRETS.items():
        tt, p0 = turret[t], cfg["pid"]
        T, P = tt["T"], tt["P"]
        # CHARGE: the weapon grows; housings along the gun, every bore swells, no tip moves
        charge.append(scale_xyz(pm(p0 + P_HOUSE, p0 + P_SCOPE), T, 1.15, 2.0, 1.75))
        charge.append(radial(pm(p0 + P_PIN), T.x, T.z, 1.5))
        charge.append(ring_xy(pm(p0 + P_RING), P, 1.30))
        pod_c = tt["pod_c"]
        charge.append(radial(pm(p0 + P_POD, p0 + P_PODMOUTH), pod_c.x, pod_c.z, 1.45))
        if t == "Sniper":
            bo, BL = info["sniper_barrel"]
            charge.append(radial(pm(p0 + P_GUN, p0 + P_MUZ, p0 + P_RAIL), bo.x, bo.z, 1.6))
            charge.append(radial(pm(p0 + P_BAND), bo.x, bo.z, 1.75))
            space.append(morphs.stretch_along((0, -1, 0), tuple(bo), 1.35, pm(p0 + P_GUN, p0 + P_MUZ, p0 + P_RAIL, p0 + P_BAND)))
        elif t == "FastGun":
            bo, BL = info["fast_barrel"]
            charge.append(radial(pm(p0 + P_GUN, p0 + P_BAND, p0 + P_MUZ), bo.x, bo.z, 1.55))
            space.append(morphs.stretch_along((0, -1, 0), tuple(bo), 1.35, pm(p0 + P_GUN, p0 + P_MUZ)))
            space.append(morphs.stretch_along((0, -1, 0), tuple(bo), 1.35, pm(p0 + P_BAND), ramp=(1.0, 1.4)))
        else:
            do = info["bomb_drum"]
            charge.append(radial(pm(p0 + P_GUN), do.x, do.z, 1.40))
            charge.append(radial(pm(p0 + P_MUZ), do.x, do.z, 1.50))
            space.append(morphs.stretch_along((0, -1, 0), tuple(do), 1.30, pm(p0 + P_GUN, p0 + P_MUZ), ramp=(0.0, 0.6)))

    # MASS: volume - the hull fattens about its axis, everything structural thickens
    mass.append(custom(hullish, lambda co, n: Vector((co.x * 0.38, 0, (co.z - Z0) * 0.38))))
    mass.append(custom(body_m, lambda co, n: n * 0.45))
    mass.append(radial(pm(30, 31), 0.0, 0.48, 1.38))
    mass.append(custom(pm(35), lambda co, n: Vector((0, 0, (co.z - FLUKE_ROOT[2]) * 0.9))))
    mass.append(custom(pm(10), lambda co, n: Vector((co.x * 0.7, 0, 0))))
    for sx, wid, nid, tp in ((1, 20, 21, FAST_P), (-1, 25, 26, BOMB_P)):
        r = Vector((sx * WING_ROOT_X, WING_ROOT_Y, WING_ROOT_Z))

        def wing_thick(co, n, r=r, sx=sx):
            t = (sx * co.x - WING_ROOT_X) / max(1e-6, WING_SPAN * cosd)
            zmid = r.z + t * (WING_SPAN * math.sin(math.radians(WING_DIHEDRAL))) + 0.02
            return Vector((0, 0, (co.z - zmid) * 0.75))
        mass.append(custom(pm(wid), wing_thick))
        # the nacelle widens but never rises into the turret's base ring
        mass.append(custom(pm(nid), lambda co, n, tp=tp: Vector(((co.x - tp[0]) * 0.36, 0, min(0.0, co.z - NAC_Z) * 0.36))))
    for sx, sid in ((1, 40), (-1, 41)):
        mass.append(radial(pm(sid), sx * JET_X, JET_Z, 1.25))

    # SPACE: reach - the guns telescope (above), the flukes span wider, the outrigger nacelles telescope about the pivot
    space.append(custom(pm(35), lambda co, n: Vector((co.x * 0.25, 0, 0))))
    for nid, tp in ((21, FAST_P), (26, BOMB_P)):
        space.append(custom(pm(nid), lambda co, n, tp=tp: Vector((0, (co.y - tp[1]) * 0.32, 0))))

    # TIME: speed - the nose stretches and sharpens, flukes and outriggers sweep back, nacelle tails stretch aft
    def nose(co, n):
        d = -7.0 - co.y
        if d <= 0:
            return Vector((0, 0, 0))
        s = morphs.smoothstep(0.0, 9.0, d)
        return Vector((-co.x * 0.28 * s, -d * 0.32 * s, -(co.z - 0.0) * 0.22 * s))
    timee.append(custom(hullish, nose))
    timee.append(custom(pm(35), lambda co, n: Vector((0, abs(co.x) * 0.50, 0))))
    for sx, wid in ((1, 20), (-1, 25)):
        def bow(co, n, sx=sx):
            t = min(1.0, max(0.0, (sx * co.x - WING_ROOT_X) / (WING_SPAN * cosd)))
            return Vector((0, 2.6 * math.sin(math.pi * t) ** 1.2, 0))
        timee.append(custom(pm(wid), bow))
    for nid, tp in ((21, FAST_P), (26, BOMB_P)):
        timee.append(custom(pm(nid), lambda co, n, tp=tp: Vector((0, max(0.0, co.y - tp[1]) * 0.28, 0))))

    shapes = {"Charge": charge, "Mass": mass, "Space": space, "Time": timee}
    for name in ("Charge", "Mass", "Space", "Time"):
        kb, mx, frac, moved = morphs.add_shape(mesh, name, shapes[name])
        ctx.log("shape %s: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))
    if "pid" in me.attributes:
        me.attributes.remove(me.attributes["pid"])

    # ================================================================== DOCUMENTATION POSES (code drives the bones)
    # World axes of the Blender frame: -X turns the nose UP (a point ahead of a hinge rises), -Z turns it to the pilot's
    # RIGHT (LESSONS 34), so a tail point swings to +X (the outside of a right turn).
    yaws = {t + "Yaw": (0, 0, -40) for t in turret}
    pitches = {t + "Pitch": (-25, 0, 0) for t in turret}
    poses = {
        "pitchup": {"tail": (-10, 0, 0), "tail.2": (-16, 0, 0)},
        "yawright": {"tail": (0, 0, -12), "tail.2": (0, 0, -16)},
        "aimright": yaws,
        "aimup": pitches,
    }
    names = ("pitchup", "yawright", "aimright", "aimup")
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
                "pitchup": [("tail.2", "z", -1, 0.5)],
                "yawright": [("tail.2", "x", +1, 0.5)],
                "aimright": [("SniperPitch", "x", -1, 1.0), ("FastGunPitch", "x", -1, 0.3), ("BombPitch", "x", -1, 0.3)],
                "aimup": [("SniperPitch", "z", +1, 0.5), ("FastGunPitch", "z", +1, 0.2), ("BombPitch", "z", +1, 0.2)],
            },
            "shapes": ["Charge", "Mass", "Space", "Time"],
            "slots": list(SLOTS),
            # the three turrets are different weapons (and the missile tubes ride outboard), so the hull is not mirror-symmetric
            "symmetric": False,
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.03,
        },
    }
