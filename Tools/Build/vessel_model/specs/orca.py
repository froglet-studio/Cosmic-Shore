"""
orca - a NEW hull with no artist file: the Orca, the 4-seat crew vessel (one helm, three gunner turrets;
Assets/_Scripts/Controller/Vessel/Crew/ORCA_CREW.md). Today Orca.prefab draws the Sparrow's mesh plus three
procedural octahedron pods on box struts (OrcaCrewView). Garrett, 2026-10-21: "it is time to use the vessel
modeling tools and really step up the vessel". A spaceship inspired by an orca, never the animal (SKILL.md 0).

A HOVERING ASSEMBLY (Garrett, 2026-10-21, after flying round 1: "the original vessels are based largely around the
idea of free floating parts that are associated and animated together but clearly distinct parts of a hovering
assembly ... this will also help keep the guns associated with the vessel but not have to worry about mating planar
surfaces"). Measured on the artist hulls: the Manta's wings float 3.4-6.5% of the diagonal clear, the Dolphin's 5.7%,
the Sparrow's tail and wing pieces 0.1-1.1%. So every major part here floats on its own bone with a designed gap of
~1.5-2.2% (GAP 1.15 u, the dorsal blade 1.6 u), facing edges parallel (the outrigger plates' end faces are x = const
planes, the blade's root edge follows the hull's back line), nothing mated, no struts, no root fillets.

THE READ, from the chase camera (the Sparrow's followOffset z -50): a broad black gunmetal core with a tall dorsal blade
hovering over its back and a railgun hovering over the blade's tip, a chain of three floating paddle plates stepping
out to a gun pod on each side, and two floating engine pods with glowing mouths between two floating fluke blades; the
orca's white is the domain saddle, flank sweeps and belly, the eye patch is the glass.

Clade -> ship (every part answers "what is this on the ship?"):
  broad deep body       -> the CORE (bone `hull`): a superellipse loft with a conical melon nose, three panel seams and
                           the tail stock behind a domain-trimmed collar
  eye patch             -> a long tilted sensor lens on each side of the head in a dark bezel; the blowhole is the helm's
                           canopy, dark-framed, with a spine rail behind it
  white flank / saddle  -> conformal domain armour plates on the core: a flank sweep per side, the saddle behind the
  / chin and belly         dorsal blade, the chin-and-belly plate (smooth outlines, not painted face stairs)
  tall dorsal fin       -> the DORSAL BLADE (bone `fin`), hovering 1.6 u over the back: a raked airfoil with a domain
                           rank band, a domain trailing-edge trim and a domain tip cap facing the SNIPER, which hovers
                           ~1.45 u above the flat tip at its exact pivot
  pectoral fins         -> the OUTRIGGER CHAIN (bones `wing.L.1..3` / `wing.R.1..3`): three floating paddle plates per
                           side, each with a sunk domain panel, stepping out and up to the turret POD (`pod.L` / `pod.R`:
                           a nacelle with a domain band, running light and small ember mouth aft) over which the FAST GUN
                           (port, pilot's left) or the BOMB (starboard) hovers ~1.0-1.1 u at its exact pivot
  flukes                -> the ENGINE SECTION, floating behind the tail stock: two engine pods (`engine.L` / `engine.R`,
                           each bell skinned to its `jet.X.mouth` child: length axis aft, the plume host) between two
                           fluke blades (`fluke.L` / `fluke.R`), domain under and a domain trailing-edge trim on top

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
  file      Assets/_Models/Vessel Models/Orca.fbx (guid 9b34bfdd14c8462c8bc138b3938820a2), preset "zup"
  nodes     mesh `Orca`, armature `OrcaArmature` (the brief's names, so the nested model resolves unambiguously
            beside the hidden Sparrow model; the convention's generic `mesh` / `rig` would collide by name); root
            bone `hull`
  bones     every floating part on its own bone, head at the part's centre, parented to `hull`: fin, wing.L.1..3,
            wing.R.1..3, pod.L, pod.R, fluke.L, fluke.R, engine.L, engine.R (-> jet.L.mouth / jet.R.mouth). The three
            turret yaw bones are parented to `hull` too (code sets their pose in the hull's frame, so no floating
            part carries them): <Seat>Yaw -> <Seat>Pitch -> <Seat>Muzzle, <Seat>Missile. Rigid skin throughout.
  slots     0 Domain, 1 Body, 2 Window, 3 Engine
  shapes    Charge (weapon): the three turret housings, cradle and scope grow 2x along the gun / 1.15x across, the
                             barrels, coils, muzzle parts, drum and missile tubes swell; no tip moves
            Mass (volume):   the core fattens (a deeper belly; the back barely rises, so the blade's hover survives), the
                             blade, plates, pods, engines and flukes thicken, the outrigger chain rides out with the flank
            Space (reach):   the railgun barrel telescopes 1.35x (at 100% the sniper tip moves 6.1 u forward, the fast
                             gun's 1.7 u, the bomb lip 1.0 u; the muzzle bones mark the REST tips), the fast gun's barrels
                             1.35x, the mortar drum 1.3x; the flukes reach out from their roots; the pods telescope
            Time (speed):    the nose stretches and sharpens, the flukes and the outrigger chain sweep back, the pod tails
                             stretch aft
            No morph moves a turret pivot (each morph is centred on it) or a jet .mouth bone.
  takes     none drive it (code poses the turret bones by name; the Sparrow's Animator stays on the hidden
            Sparrow bones). Four documentation poses for the sheet and the studio, each direction-proved by
            pose_expect: pitchup (the flukes and engines tip and drop), yawright (they swing to the outside of the
            turn), aimright (every turret yaws 40 deg right), aimup (every turret pitches 25 deg up).
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
PED_Y0, PED_LEN = 9.0, 3.6
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

# THE HOVERING ASSEMBLY (Garrett, 2026-10-21: "the original vessels are based largely around the idea of free
# floating parts that are associated and animated together but clearly distinct parts of a hovering assembly"):
# every major part floats clear of its neighbours by a designed gap, its facing edges parallel. 1% of the ~66 u
# diagonal is 0.66 u.
GAP = 1.15                        # the standard gap between floating parts (~1.75% of the diagonal)
FIN_GAP = 1.6                     # the dorsal blade's hover above the hull's back (~2.4%)
FIN_LE_Y, FIN_CR, FIN_CT, FIN_SWEEP = -6.0, 10.5, 3.6, 6.0
FIN_TIP_Z = 14.1                  # flat tip; the sniper's base ring hovers ~1.45 u above it (pivot z 16.2)

WING_X0, WING_X1 = 6.6, 19.05     # the outrigger chain's inner and outer edges (hull flank ~5.3; pod side 20.2)
WING_Z0 = -1.6                    # chain height at the hull; it rises to the pod's axis
WING_LE0, WING_CR, WING_CT = -8.0, 7.6, 4.4
WING_BULGE, WING_LE_BULGE = 2.2, 1.0   # the paddle: chord and leading edge bulge mid-span
N_PLATES = 3
NAC_R, NAC_Z = 1.40, -0.10        # the turret pods; their tops sit ~1.0-1.1 u under the turret base rings
NAC_Y0, NAC_LEN = -0.2, 9.0
WING_Z1 = NAC_Z
WING_SWEEP = (FAST_P[1] - WING_CT / 2) - WING_LE0
_PLATE_W = (WING_X1 - WING_X0 - (N_PLATES - 1) * GAP) / N_PLATES
PLATES = [(WING_X0 + k * (_PLATE_W + GAP), WING_X0 + k * (_PLATE_W + GAP) + _PLATE_W) for k in range(N_PLATES)]

ENG_X, ENG_Y0, ENG_Z, ENG_R = 1.90, 13.6, 0.50, 1.15     # the engine pods float behind the tail stock (ends y 12.6)
FLUKE_X0, FLUKE_LE_Y, FLUKE_Z = ENG_X + ENG_R + GAP, 13.2, 0.50
FLUKE_SPAN, FLUKE_CR, FLUKE_CT, FLUKE_SWEEP = 7.4, 4.2, 1.4, 4.4

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
    # a dorsal spine rail from the canopy back toward the dorsal blade: a dark structural ridge
    rail_pts = [(0.0, y, top_z(y) - 0.18) for y in (-10.0, -9.2, -8.3, -7.4)]
    rail = f.tube_along("spine", rail_pts, [0.22, 0.38, 0.46, 0.40], segments=10)
    hull_parts.append(piece(rail, H, 3))
    # the tail stock is part of the core: a peduncle behind a domain-trimmed joint collar
    ped = f.fuselage("peduncle", PED_LEN, nose=(0, PED_Y0, 0.48), axis=(0, 1, 0), segments=24,
                     stations=[(0.0, 2.20, 2.20, 1.66, 2.4, 2.4, 0.0), (0.5, 1.55, 1.75, 1.20, 2.4, 2.4, 0.02),
                               (1.0, 1.05, 1.10, 0.85, 2.4, 2.4, 0.03)])
    hull_parts.append(piece(ped, H, 30))
    col = lathe_aft("collar", [(0.0, 0.0), (2.28, 0.0), (2.44, 0.12), (2.44, 0.40), (2.28, 0.52), (0.0, 0.52)],
                    origin=(0, PED_Y0 - 0.05, 0.48), seg=24)
    f.transform_mesh(col, Matrix.Translation(Vector((0, 0, 0.48))) @ Matrix.Diagonal((1.0, 1.0, 0.86, 1.0))
                     @ Matrix.Translation(Vector((0, 0, -0.48))))
    f.ensure_slots(col, SLOTS)
    f.paint(col, H)
    f.paint(col, D, lambda c, n, i: abs(n.y) < 0.3)
    hull_parts.append(piece(col, None, 31))
    parts.append(bone_part(hull_parts, "hull"))

    centre = {}          # bone name -> the part's centre (bone heads sit at part centres)

    def note_centre(obj, name):
        lo, hi = f.bounds(obj)
        centre[name] = (lo + hi) / 2
        return obj

    # ================================================================== DORSAL BLADE (tall dorsal fin), hovering
    # Its root edge follows the hull's back at FIN_GAP (parallel edges), its tip is flat under the sniper's ring.
    root_z = top_z(FIN_LE_Y) + FIN_GAP
    fin_span = FIN_TIP_Z - root_z
    blade = f.wing("dorsal", root=(0.0, FIN_LE_Y, root_z), span=fin_span, chord_root=FIN_CR, chord_tip=FIN_CT,
                   sweep=FIN_SWEEP, forward=(0, -1, 0), up=(-1, 0, 0), thickness=0.165, sections=7, segments=22,
                   tip_round=False, taper_power=0.85)
    for v in blade.data.vertices:          # shear the root down onto the hull's back line, fading out by the tip
        t = max(0.0, min(1.0, (v.co.z - root_z) / fin_span))
        v.co.z += (top_z(v.co.y) + FIN_GAP - root_z) * (1.0 - t) ** 1.6
    blade.data.update()
    f.ensure_slots(blade, SLOTS)
    f.paint(blade, H)
    f.paint(blade, D, lambda c, n, i: 11.25 < c.z < 12.75)          # the rank band

    def fin_te(c, n, i):
        t = max(0.0, (c.z - root_z) / fin_span) ** 0.85
        le = FIN_LE_Y + FIN_SWEEP * t
        return 7.3 < c.z < 11.1 and (c.y - le) / (FIN_CR + (FIN_CT - FIN_CR) * t) > 0.86
    f.paint(blade, D, fin_te)     # a domain trim line up the trailing edge: the blade reads from behind
    f.paint(blade, D, lambda c, n, i: n.z > 0.9 and c.z > FIN_TIP_Z - 0.05)   # a domain tip cap facing the turret
    blade = piece(blade, None, 10)
    f.sharpen_by_angle(blade, 38)
    parts.append(note_centre(bone_part([blade], "fin"), "fin"))
    info["fin_root_z"], info["fin_span"] = root_z, fin_span

    # ================================================================== OUTRIGGERS: a chain of floating paddle plates
    def chain_t(x):
        return (abs(x) - WING_X0) / (WING_X1 - WING_X0)

    def paddle_chord(t):
        return WING_CR + (WING_CT - WING_CR) * t + WING_BULGE * math.sin(math.pi * t) ** 1.3

    def paddle_le(t):
        return WING_LE0 + WING_SWEEP * t - WING_LE_BULGE * math.sin(math.pi * t)

    def plate_z(t):
        return WING_Z0 + (WING_Z1 - WING_Z0) * t

    def paddle_plate(name, sx, x0, x1, n_sec=3):
        """One plate of the chain: airfoil sections between x0 and x1; its end faces are x = const planes, so each
        gap is bounded by two parallel faces."""
        sts = []
        for k in range(n_sec + 1):
            x = x0 + (x1 - x0) * k / n_sec
            t = chain_t(x)
            ch = paddle_chord(t)
            le = Vector((sx * x, paddle_le(t), plate_z(t)))
            prof = f.airfoil(chord=ch, thickness=0.105 - 0.02 * t, camber=0.02, segments=16)
            sts.append(f.Station(le, prof, right=(0, 1, 0), up=(0, 0, 1)))
        return f.loft(name, sts)

    span_dir = {}
    dih = math.atan2(WING_Z1 - WING_Z0, WING_X1 - WING_X0)
    for tg, sx, tp in (("L", 1, FAST_P), ("R", -1, BOMB_P)):
        span_dir[sx] = Vector((sx * math.cos(dih), 0.0, math.sin(dih)))
        for k, (x0, x1) in enumerate(PLATES):
            pl = paddle_plate("plate", sx, x0, x1)
            f.ensure_slots(pl, SLOTS)
            f.paint(pl, H)
            m = 0.11 * (x1 - x0)

            def panel(c, n, i, sx=sx, x0=x0, x1=x1, m=m):
                if not (x0 + m < sx * c.x < x1 - m) or n.z < 0.3:
                    return False
                t = chain_t(c.x)
                return 0.13 < (c.y - paddle_le(t)) / paddle_chord(t) < 0.74
            f.inset_faces(pl, panel, 0.10, depth=0.04, slot=D)
            pl = piece(pl, None, (20 if sx > 0 else 25) + k)
            f.sharpen_by_angle(pl, 38)
            name = "wing.%s.%d" % (tg, k + 1)
            parts.append(note_centre(bone_part([pl], name), name))
        # the turret's pod: a nacelle hovering under the turret (domain band, running light, small ember mouth aft)
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
        nac = piece(nac, None, 23 if sx > 0 else 28)
        f.sharpen_by_angle(nac, 35)
        lt = f.sphere("runlight", 0.16, center=(nx + sx * (NAC_R - 0.02), NAC_Y0 + 1.8, NAC_Z + 0.15), segments=10, rings=6,
                      scale=(1.0, 2.2, 1.0))
        lt = piece(lt, D, 24 if sx > 0 else 29)
        parts.append(bone_part([nac, lt], "pod." + tg))
        centre["pod." + tg] = Vector((nx, NAC_Y0 + NAC_LEN / 2, NAC_Z))

    # ================================================================== ENGINE SECTION: floating engine pods + fluke blades
    for tg, sx in (("L", 1), ("R", -1)):
        x = sx * ENG_X
        R = ENG_R
        case = lathe_aft("engcase", [(0.0, 0.0), (0.47 * R, 0.08), (0.86 * R, 0.6), (R, 1.45), (R, 2.9), (0.0, 2.9)],
                         origin=(x, ENG_Y0, ENG_Z), seg=20)
        f.ensure_slots(case, SLOTS)
        f.paint(case, H)
        f.paint(case, D, lambda c, n, i: ENG_Y0 + 1.75 < c.y < ENG_Y0 + 2.05)
        parts.append(bone_part([piece(case, None, 40 if sx > 0 else 41)], "engine." + tg))
        centre["engine." + tg] = Vector((x, ENG_Y0 + 1.45, ENG_Z))
        bell = lathe_aft("engbell", [(0.0, 0.0), (0.97 * R, 0.0), (1.07 * R, 0.2), (1.12 * R, 0.55), (0.86 * R, 0.55), (0.74 * R, 0.36),
                                     (0.0, 0.36)], origin=(x, ENG_Y0 + 2.7, ENG_Z), seg=20)
        f.ensure_slots(bell, SLOTS)
        f.paint(bell, H)
        f.paint(bell, J, lambda c, n, i, x=x: c.y > ENG_Y0 + 2.9 and math.hypot(c.x - x, c.z - ENG_Z) < 0.83 * ENG_R)
        parts.append(bone_part([piece(bell, None, 42 if sx > 0 else 43)], "jet." + tg + ".mouth"))
        fk = f.wing("fluke", root=(sx * FLUKE_X0, FLUKE_LE_Y, FLUKE_Z), span=FLUKE_SPAN, chord_root=FLUKE_CR, chord_tip=FLUKE_CT,
                    sweep=FLUKE_SWEEP, dihedral_deg=0.0, thickness=0.14, sections=6, segments=20, side=sx, tip_round=True)
        f.ensure_slots(fk, SLOTS)
        f.paint(fk, H)
        f.paint(fk, D, lambda c, n, i: n.z < -0.5)

        def te_trim(c, n, i, sx=sx):
            t = min(1.0, max(0.0, (sx * c.x - FLUKE_X0) / FLUKE_SPAN))
            le = FLUKE_LE_Y + FLUKE_SWEEP * t
            ch = FLUKE_CR + (FLUKE_CT - FLUKE_CR) * t
            return n.z > 0.5 and (c.y - le) / ch > 0.78 and t > 0.12
        f.paint(fk, D, te_trim)
        fk = piece(fk, None, 35 if sx > 0 else 36)
        parts.append(note_centre(bone_part([fk], "fluke." + tg), "fluke." + tg))
    info["jet_mouth"] = {tg: (sx * ENG_X, ENG_Y0 + 3.25, ENG_Z) for tg, sx in (("L", 1), ("R", -1))}

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
    # every floating part on its own bone, its head at the part's centre; the turret yaw bones hang off the hull
    # (code sets their pose in the hull's frame, so no part of the chain carries them)
    bones.append(B("fin", centre["fin"], centre["fin"] + Vector((0, 0, 2.0)), parent="hull"))
    for tg, sx in (("L", 1), ("R", -1)):
        for k in range(N_PLATES):
            nm = "wing.%s.%d" % (tg, k + 1)
            bones.append(B(nm, centre[nm], centre[nm] + span_dir[sx] * 1.5, parent="hull"))
        bones.append(B("pod." + tg, centre["pod." + tg], centre["pod." + tg] + fwd * 1.5, parent="hull"))
        bones.append(B("fluke." + tg, centre["fluke." + tg], centre["fluke." + tg] + Vector((sx * 1.5, 0, 0)), parent="hull"))
        bones.append(B("engine." + tg, centre["engine." + tg], centre["engine." + tg] + Vector((0, 1.2, 0)), parent="hull"))
        m = Vector(info["jet_mouth"][tg])
        bones.append(B("jet." + tg + ".mouth", m, m + Vector((0, 0.8, 0)), parent="engine." + tg))
    for t, tt in turret.items():
        bones.append(B(t + "Yaw", tt["P"], tt["P"] + fwd * 1.2, parent="hull"))
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

    body_m = pm(1, 4, 30)
    hullish = pm(1, 2, 3, 4, 30, 31)
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

    # MASS: volume - the core fattens about its axis (deeper belly, the back barely rises, so the dorsal blade's hover
    # survives), every floating part thickens, and the outrigger chain rides out with the flank so its gaps stay open
    def core(co, n):
        dz = co.z - Z0
        return Vector((co.x * 0.20, 0, dz * (0.12 if dz > 0 else 0.62)))
    mass.append(custom(hullish, core))
    mass.append(custom(body_m, lambda co, n: n * 0.25))
    mass.append(custom(pm(10), lambda co, n: Vector((co.x * 0.7, 0, 0))))
    for sx, base, nid in ((1, 20, 23), (-1, 25, 28)):
        for k in range(N_PLATES):
            shift = (1.2, 0.8, 0.4)[k]

            def plate_mass(co, n, sx=sx, shift=shift):
                zmid = plate_z(chain_t(co.x)) + 0.03
                return Vector((sx * shift, 0, (co.z - zmid) * 0.8))
            mass.append(custom(pm(base + k), plate_mass))
        mass.append(custom(pm(nid), lambda co, n, sx=sx: Vector(((co.x - sx * abs(FAST_P[0])) * 0.36, 0, min(0.0, co.z - NAC_Z) * 0.36))))
    for sx, cid, fid in ((1, 40, 35), (-1, 41, 36)):
        mass.append(radial(pm(cid), sx * ENG_X, ENG_Z, 1.25))
        mass.append(custom(pm(fid), lambda co, n: Vector((0, 0, (co.z - FLUKE_Z) * 0.9))))

    # SPACE: reach - the guns telescope (above), the flukes reach out from their roots, the pods telescope about the pivot
    for sx, fid, nid, tp in ((1, 35, 23, FAST_P), (-1, 36, 28, BOMB_P)):
        space.append(custom(pm(fid), lambda co, n, sx=sx: Vector(((co.x - sx * FLUKE_X0) * 0.30, 0, 0))))
        space.append(custom(pm(nid), lambda co, n, tp=tp: Vector((0, (co.y - tp[1]) * 0.32, 0))))

    # TIME: speed - the nose stretches and sharpens, the flukes and the outrigger chain sweep back, pod tails stretch aft
    def nose(co, n):
        d = -7.0 - co.y
        if d <= 0:
            return Vector((0, 0, 0))
        s = morphs.smoothstep(0.0, 9.0, d)
        return Vector((-co.x * 0.28 * s, -d * 0.32 * s, -(co.z - 0.0) * 0.22 * s))
    timee.append(custom(hullish, nose))
    for sx, fid, base, nid, tp in ((1, 35, 20, 23, FAST_P), (-1, 36, 25, 28, BOMB_P)):
        timee.append(custom(pm(fid), lambda co, n, sx=sx: Vector((0, (sx * co.x - FLUKE_X0) * 0.50, 0))))

        def bow(co, n):
            t = min(1.0, max(0.0, chain_t(co.x)))
            return Vector((0, 2.6 * math.sin(math.pi * t) ** 1.2, 0))
        timee.append(custom(pm(base, base + 1, base + 2), bow))
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
        # the engine section is a hovering assembly: its parts both turn and move (they hinge on nothing)
        "pitchup": {"fluke.L": {"rot": (-14, 0, 0), "loc": (0, 0, -0.7)}, "fluke.R": {"rot": (-14, 0, 0), "loc": (0, 0, -0.7)},
                    "engine.L": {"rot": (-8, 0, 0), "loc": (0, 0, -0.35)}, "engine.R": {"rot": (-8, 0, 0), "loc": (0, 0, -0.35)}},
        "yawright": {"fluke.L": {"rot": (0, 0, -12), "loc": (0.9, 0, 0)}, "fluke.R": {"rot": (0, 0, -12), "loc": (0.9, 0, 0)},
                     "engine.L": {"rot": (0, 0, -8), "loc": (0.6, 0, 0)}, "engine.R": {"rot": (0, 0, -8), "loc": (0.6, 0, 0)}},
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
                "pitchup": [("fluke.L", "z", -1, 0.4), ("fluke.R", "z", -1, 0.4)],
                "yawright": [("fluke.L", "x", +1, 0.5), ("fluke.R", "x", +1, 0.5)],
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
