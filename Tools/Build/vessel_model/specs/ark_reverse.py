"""
ark_reverse - the Ark's SECOND CONCEPT: the clam valve facing the other way (Garrett, 2026-10-27: "this is one concept.
now lets explore an alternative where the arc is facing the other way. get re-inspired from the arc clam shell. and have
it face the opposite direction."). `ark.py` (Ark.fbx) is the first concept: hinge aft, fan opening forward to a lip at
the nose. Here the HINGE IS THE PROW and the fan opens AFT, so the shell becomes a swept delta with the bridge at its
point. Same brief as the first concept: prisms, shielded, danger and supershielded prisms as the ark's health,
regenerating into sockets that look like they belong; a visible inactive omni crystal; a payload players grab; 196 from
nose to jets (half a standard nucleus radius); a cockpit windshield where the corrugations converge.

Re-inspired from the scallop, beyond the turn-around:
  the umbo (where ribs converge) -> the BRIDGE DOME is the PROW (bone `umbo`): its seven-pane cockpit windshield looks
                                    straight ahead, portholes round its flanks, a sensor mast and dish on its crown
  the auricles (ears)            -> forward CANARD HANGARS (bones `auricle.L` / `auricle.R`) flanking the bridge, a long
                                    lit LAUNCH SLOT in each leading edge (the fighters leave forward), a thruster aft
  the radial ribs                -> EIGHT SHELL PLATES fanning aft (bones `shell.0` .. `shell.7`): riblets, three
                                    shingled tiles, sunken panels, domain lip band and crown stripe, lip running lights
  the rib ends at the margin     -> EIGHT JETS across the trailing lip, one floating behind each rib and splayed with
                                    the fan (bones `engine.0` .. `engine.7`): the corrugations end in thrust
  the mantle's row of blue eyes  -> MANTLE EYES: a blue sensor eye peeking from the end of each groove between the jets
  the swept valve edges          -> the delta's LEADING EDGES, lined with DANGER prisms on the core's flank margin
  (a pearl in the clam)          -> the OMNI REACTOR in the court behind the bridge where every rib converges: the
                                    dormant omni crystal (bone `omni`, Window = inactive glass) in its gimballed cage
                                    (bone `omni.ring`), a beacon the chase camera sees over the whole fan
  (a stinger)                    -> the PAYLOAD lance lies in the centre groove pointing aft, its glowing head between
                                    the two centre jets (bones `payload`, `payload.dock`); players grab it from behind
  the mantle / body              -> the CORE (root `hull`), the KEEL fin (bone `keel`), as on the first concept

SOCKETS (the kit's cradle, as on the first concept: bone `socket.<tier>.<nn>`, head at the prism's centre, tail along
it, Z = the prism's up; prism PRISM_W x PRISM_H x PRISM_L):
  supershielded  the two innermost groove rows, nearest the reactor (14)
  shielded       the two middle rows (13: the centre groove keeps its first shielded row)
  prism          the two outer rows, by the jets (12)
  danger         along the two swept leading edges, four per side (8)

A HOVERING ASSEMBLY: `assembly_report.py ArkReverse.fbx --floating shell.0,...,shell.7,umbo,auricle.L,auricle.R,omni.ring,
omni,payload,engine.0,...,engine.7,keel`.

Contract (FLEET_CONVENTION sections 1-4 for a NEW body):
  file      Assets/_Models/Vessel Models/ArkReverse.fbx (guid 5c1e8a3f7d2b4e9a9f6b2c4d8e1a7f30), preset "zup"
  frame     Blender: nose -Y, up +Z, pilot's left +X. Unity (x, y, z) = Blender (-x, z, -y): NOSE = Unity +Z.
            The hinge sits at Blender (0, HY, 0), near the nose; the fan opens toward +Y (aft).
  nodes     mesh `mesh` (identity, metres), armature `rig`; root bone `hull`
  slots     0 Domain, 1 Body, 2 Window, 3 Engine
  shapes    Charge: the lance lengthens aft and the omni crystal swells; Mass: the belly deepens (the keel rides it) and
            the jets fatten; Space: the canard hangars slide outward and the masts telescope; Time: the jets stretch.
            None moves a plate, a cradle or a socket bone.
  takes     rest (first); clap (the shell lifts its trailing lip about the hinge); release (the lance slides aft out of
            its cradles). Single-frame poses, no blend-shape curves.
"""

import math
from _ark_kit import SLOTS, D, H, W, J

NAME = "ArkReverse"
OUTPUT = "Assets/_Models/Vessel Models/ArkReverse.fbx"

ARMATURE = "rig"
MESH_NODE = "mesh"

# ---------------------------------------------------------------- layout (Blender frame: nose -Y, up +Z, left +X)
NOSE_TO_JETS = 196.0      # half a standard nucleus radius (392)
NOSE_R = 38.0             # the core's radius ahead of the hinge: the nose is at y = HY - NOSE_R
HY = -98.0 + NOSE_R       # the hinge (umbo centre), so the nose sits at -98 and the jets end near +98
R = 128.0                 # the plates' outer radius (the trailing lip)
THETA = 50.0              # the fan's half angle, degrees
NPLATE = 8
GROOVE = 8.4
RHO0 = 52.0               # where the plates begin (inside: the bridge and the reactor court)
TILES = ((RHO0, 77.0), (77.6, 102.6), (103.2, R))
SHINGLE = 0.7
Z0, HDOME = 6.0, 22.0
CROWN, RIBLET = 1.6, 0.55
PLATE_T = 1.8
CORE_DROP = 6.5
RC = R + 2.0              # the core's radius in the fan: the jets hang off its trailing rim
FLANK = 6.0               # degrees of core margin beyond the outer plates: the danger prisms' leading-edge shelf

UMBO_R = 34.0
UMBO_LIFT = 11.0

PRISM_W, PRISM_H, PRISM_L = 4.0, 1.6, 9.0
SOCKET_ROWS = (("supershielded", 58.0), ("supershielded", 70.0), ("shielded", 84.0), ("shielded", 96.0),
               ("prism", 108.5), ("prism", 120.5))
DANGER_ROWS = (72.0, 88.0, 104.0, 120.0)

PAY_RHO0, PAY_RHO1 = 92.0, 154.0
PAY_R = 1.8
LANCE_GAP = 1.4

ENG_RHO0 = R + 4.0
ENG_R = 7.5
ENG_Z = 7.0
REACTOR_RHO = 43.0


def _ang(dx, dy):
    """Fan angle (degrees) of a point dx across, dy aft of the hinge: 0 straight aft, +90 to +X."""
    return math.degrees(math.atan2(dx, dy))


def _smooth(a, b, x):
    t = min(max((x - a) / (b - a), 0.0), 1.0)
    return t * t * (3 - 2 * t)


def dome(x, y):
    """The valve's top surface: highest at the hinge (the prow), falling aft to the trailing lip and toward the swept
    flanks, and falling fast ahead of the hinge (the bridge dome sits on that)."""
    dx, dy = x, y - HY
    rho = math.hypot(dx, dy)
    th = _ang(dx, dy) if rho > 1e-6 else 0.0
    u = min(rho / R, 1.15)
    rise = HDOME * (1.0 - u ** 2.1) * (1.0 - 0.38 * min(1.0, (th / THETA) ** 2) * _smooth(0.0, 40.0, rho))
    if dy < 0:
        rise *= math.exp(-(dy / 22.0) ** 2)
    return Z0 + max(rise, -1.0)


def fan_dir(phi_deg):
    p = math.radians(phi_deg)
    return (math.sin(p), math.cos(p))


def fan_perp(phi_deg):
    """Unit vector across the fan toward increasing phi."""
    p = math.radians(phi_deg)
    return (math.cos(p), -math.sin(p))


def at(phi, rho):
    d = fan_dir(phi)
    return rho * d[0], HY + rho * d[1]


PHI = [-THETA + k * (2 * THETA / NPLATE) for k in range(NPLATE + 1)]
GROOVES = PHI[1:-1]
MIDS = [0.5 * (a + b) for a, b in zip(PHI, PHI[1:])]


def tile_lift(rho):
    for a, b in TILES:
        if a - 0.31 <= rho <= b + 0.31:
            return SHINGLE * min(max((rho - a) / (b - a), 0.0), 1.0)
    return 0.0


def core_rmax(phi):
    a = abs(phi)
    if a > 180.0:
        a = 360.0 - a
    if a <= THETA + FLANK:
        return RC
    if a <= THETA + FLANK + 16.0:
        return NOSE_R + (RC - NOSE_R) * (1 - _smooth(THETA + FLANK, THETA + FLANK + 16.0, a))
    return NOSE_R


def core_top(x, y):
    return max(dome(x, y), Z0 - 0.6) - CORE_DROP


def core_bot(x, y):
    rho = math.hypot(x, y - HY)
    phi = _ang(x, y - HY) if rho > 1e-6 else 0.0
    q = min(rho / core_rmax(phi), 1.0)
    return -9.0 - 10.0 * (1 - q * q) ** 0.8


def umbo_rmax(phi):
    return UMBO_R


def jets_y():
    """The aftmost jet mouth (the two centre jets)."""
    return HY + (ENG_RHO0 + eng_len()) * math.cos(math.radians(min(abs(m) for m in MIDS)))


def eng_len():
    """The jets' length, so the centre jets' mouths end NOSE_TO_JETS behind the nose."""
    c = math.cos(math.radians(min(abs(m) for m in MIDS)))
    return (NOSE_TO_JETS - NOSE_R) / c - ENG_RHO0


def build(ctx):
    from mathutils import Vector
    from _ark_kit import Kit
    f, rig, morphs, takes = ctx.forms, ctx.rig, ctx.morphs, ctx.takes
    kit = Kit(ctx, SLOTS)
    piece, bone_part, loft_bm, inset, finish_bm = kit.piece, kit.bone_part, kit.loft_bm, kit.inset, kit.finish_bm

    def polar_bm(phis, ts, rmax, ztop, zbot, wall, slot=H):
        return kit.polar_bm(phis, ts, rmax, ztop, zbot, wall, at, (0.0, HY), slot)

    def surf_normal(x, y):
        e = 0.25
        gx = (dome(x + e, y) - dome(x - e, y)) / (2 * e)
        gy = (dome(x, y + e) - dome(x, y - e)) / (2 * e)
        return Vector((-gx, -gy, 1.0)).normalized()

    # ================================================================== SHELL PLATES (shell.0 .. shell.7), fanning aft
    NS, NB, NRT = 28, 4, 12
    for k in range(NPLATE):
        pa, pb = PHI[k], PHI[k + 1]
        oa = GROOVE / 2 if k > 0 else 0.0
        ob = GROOVE / 2 if k < NPLATE - 1 else 0.0
        da, db, na, nb = fan_dir(pa), fan_dir(pb), fan_perp(pa), fan_perp(pb)
        tiles = []
        for ti, (r0, r1) in enumerate(TILES):
            rings = []
            for t in range(NRT + 1):
                rho = r0 + (r1 - r0) * t / NRT
                ea = Vector((rho * da[0] + oa * na[0], HY + rho * da[1] + oa * na[1], 0))
                eb = Vector((rho * db[0] - ob * nb[0], HY + rho * db[1] - ob * nb[1], 0))
                lift = SHINGLE * t / NRT
                top, bot = [], []
                for s in range(NS + 1):
                    u = s / NS
                    p = ea.lerp(eb, u)
                    sn = math.sin(math.pi * u)
                    z = dome(p.x, p.y) + lift + CROWN * sn ** 0.7 + RIBLET * (0.5 - 0.5 * math.cos(6 * math.pi * u)) * sn ** 0.5
                    top.append(Vector((p.x, p.y, z)))
                for s in range(NB + 1):
                    p = ea.lerp(eb, 1 - s / NB)
                    bot.append(Vector((p.x, p.y, dome(p.x, p.y) + lift - PLATE_T)))
                rings.append(top + bot)
            bm, grid, caps = loft_bm(rings, zipper=(NS + 1, NB + 1))
            for t, row in enumerate(grid):
                for s in range(NS):
                    if ti == 2 and t >= NRT - 2:
                        row[s].material_index = D
                    elif ti >= 1 and s in (NS // 2 - 1, NS // 2):
                        row[s].material_index = D
            if ti == 2:
                for fc in caps[1]:
                    fc.material_index = D
            r_lo, r_hi = (2, NRT - 3) if ti < 2 else (2, NRT - 4)
            inset(bm, [grid[t][s] for t in range(r_lo, r_hi) for s in range(3, NS // 2 - 2)], 0.35, 0.3)
            inset(bm, [grid[t][s] for t in range(r_lo, r_hi) for s in range(NS // 2 + 2, NS - 3)], 0.35, 0.3)
            tiles.append(piece(finish_bm("tile", bm), None, 10 + k, bevel=0.12, angle=35))
        for (dd, nn, off, side) in ((da, na, oa, 1), (db, nb, ob, -1)):
            rho = R - 1.4
            p = Vector((rho * dd[0] + side * (off + 1.2) * nn[0], HY + rho * dd[1] + side * (off + 1.2) * nn[1], 0))
            p.z = dome(p.x, p.y) + SHINGLE + 0.5
            tiles.append(piece(f.sphere("light", 0.55, center=tuple(p), segments=10, rings=6), J, 10 + k))
        bone_part(tiles, "shell.%d" % k)

    # ================================================================== THE BRIDGE DOME at the prow + COCKPIT WINDSHIELD
    UPH = [2.5 * j for j in range(144)]                # 0 = aft; the windshield is centred on 180 (straight ahead)
    UTS = [0.10, 0.20, 0.30, 0.40, 0.50, 0.58, 0.64, 0.72, 0.80, 0.88, 0.93, 0.97, 0.995]

    def umbo_top(x, y):
        q = min(math.hypot(x, y - HY) / UMBO_R, 1.0)
        return dome(x, y) + 1.2 + UMBO_LIFT * (1 - q ** 5)

    def umbo_bot(x, y):
        return dome(x, y) - 2.6
    ubm, ub = polar_bm(UPH, UTS, umbo_rmax, umbo_top, umbo_bot, (0.5,))
    band = {t: i for i, t in enumerate(UTS[:-1])}

    def cols(lo, hi):
        return [j for j, p in enumerate(UPH) if lo <= p + 1.25 <= hi]
    for j in cols(135.0, 225.0):
        ub[band[0.58]][j].material_index = D           # brow
        ub[band[0.88]][j].material_index = D           # sill
    for j in range(len(UPH)):
        ub[band[0.97]][j].material_index = D           # rim band
    base = cols(137.5, 140.0)[0]
    for pane in range(7):
        cs = [base + pane * 5 + c for c in range(4)]
        inset(ubm, [ub[band[t]][j] for t in (0.64, 0.72, 0.80) for j in cs], 0.3, 0.6, W)
    for s0 in range(12):
        lo = 30.0 * s0
        if lo + 30.0 > 130.0 and lo < 230.0:
            continue
        cs = cols(lo + 2.5, lo + 27.5)
        inset(ubm, [ub[band[t]][j] for t in (0.20, 0.30, 0.40) for j in cs], 0.35, 0.3)
    for j, p in enumerate(UPH):
        c = abs(p + 1.25 - 180.0)
        if 52.0 <= c <= 150.0 and j % 2 == 0:
            inset(ubm, [ub[band[0.93]][j]], 0.22, 0.35, W)
    umbo = piece(finish_bm("umbo", ubm), None, 20, bevel=0.1, angle=35)
    peak = Vector((0.0, HY + 3.0, umbo_top(0.0, HY + 3.0)))
    mast = f.tube_along("mast", [tuple(peak - Vector((0, 0, 0.8))), tuple(peak + Vector((0, 0, 4.0))), tuple(peak + Vector((0, 0, 9.0)))],
                        [0.75, 0.55, 0.35], segments=10)
    beacon = f.sphere("beacon", 0.9, center=tuple(peak + Vector((0, 0, 9.6))), segments=12, rings=8)
    dish = f.lathe("dish", [(0.0, 0.0), (1.2, 0.15), (2.4, 0.55), (2.8, 0.95), (2.5, 0.95), (1.1, 0.45), (0.0, 0.3)], segments=16,
                   axis=(0, -0.5, 0.86), origin=tuple(peak + Vector((0, 2.2, 3.0))))
    bone_part([umbo, piece(mast, H, 21), piece(beacon, J, 21), piece(dish, H, 21)], "umbo")

    # ================================================================== CANARD HANGARS (auricle.L / auricle.R)
    ear_info = {}
    for side_, sx in (("L", 1), ("R", -1)):
        x0, x1 = 46.0, 86.0
        NX, NY = 14, 20
        rings = []
        for i in range(NX + 1):
            a = i / NX
            ax = x0 + (x1 - x0) * a
            yf = HY - 10.0 + 12.0 * a ** 1.3
            yb = HY + 14.0 - 2.0 * a
            zt = 13.0 - 4.0 * a
            th = 6.0 - 1.2 * a
            top = []
            for j in range(NY + 1):
                v = j / NY
                sn = math.sin(math.pi * v)
                z = zt + 0.9 * sn ** 0.5 + 0.45 * (0.5 - 0.5 * math.cos(6 * math.pi * v)) * sn ** 0.5
                top.append(Vector((sx * ax, yf + (yb - yf) * v, z)))
            bot = [Vector((sx * ax, yf + (yb - yf) * (1 - s / 4), zt - th)) for s in range(5)]
            rings.append(top + bot)
        bm, grid, caps = loft_bm(rings)
        n = NY + 1 + 5
        for row in grid:
            for j in range(2):
                row[j].material_index = D                # the leading edge
        inset(bm, [grid[i][j] for i in range(2, NX - 2) for j in range(4, NY - 3)], 0.4, 0.3)
        inset(bm, [grid[i][n - 1] for i in range(1, NX - 1)], 0.6, 2.5, W)     # the forward launch slot
        inset(bm, caps[1], 0.5, 0.4)
        wing = piece(finish_bm("auricle", bm), None, 30 + (sx > 0), bevel=0.1, angle=35)
        ex = sx * (x1 - 8.0)
        ey = HY + 14.0 - 2.0 * ((abs(ex) - x0) / (x1 - x0))
        thr = f.lathe("thruster", [(0.0, -0.4), (1.6, -0.4), (2.0, 1.2), (2.3, 3.2), (1.8, 3.4), (1.2, 2.6), (0.0, 2.8)], segments=14,
                      axis=(0, 1, 0), origin=(ex, ey, 8.0))
        f.ensure_slots(thr, SLOTS)
        f.paint(thr, H)
        f.paint(thr, J, lambda c, n, i, ex=ex, ey=ey: c.y > ey + 2.0 and math.hypot(c.x - ex, c.z - 8.0) < 1.4)
        cm_base = Vector((sx * 64.0, HY + 6.0, 13.0 - 4.0 * 18.0 / 40.0 + 1.0))
        cm = f.tube_along("cmast", [tuple(cm_base - Vector((0, 0, 0.6))), tuple(cm_base + Vector((0, 0, 3.0))), tuple(cm_base + Vector((0, 0, 7.0)))],
                          [0.6, 0.45, 0.3], segments=10)
        cdish = f.lathe("cdish", [(0.0, 0.0), (1.0, 0.12), (2.0, 0.45), (2.3, 0.8), (2.0, 0.8), (0.9, 0.38), (0.0, 0.25)], segments=14,
                        axis=(sx * 0.6, -0.2, 0.77), origin=tuple(cm_base + Vector((sx * 1.2, 0, 5.0))))
        ctip = f.sphere("ctip", 0.5, center=tuple(cm_base + Vector((0, 0, 7.5))), segments=10, rings=6)
        bone_part([wing, piece(thr, None, 32), piece(cm, H, 33), piece(cdish, H, 33), piece(ctip, J, 33)], "auricle." + side_)
        ear_info[sx] = (Vector((sx * x0, HY, 10.0)), cm_base)

    # ================================================================== CORE (hull)
    CPH = sorted(set([-180.0 + 2.5 * j for j in range(144)] +
                     [s * (THETA + FLANK + 1.0 * j) for j in range(1, 16) for s in (1, -1)]))
    CTS = [0.10, 0.22, 0.34, 0.46, 0.58, 0.68, 0.76, 0.83, 0.89, 0.94, 0.975, 0.992]
    CWALL = (0.06, 0.30, 0.55, 0.80, 0.94)
    cbm, cb = polar_bm(CPH, CTS, core_rmax, core_top, core_bot, CWALL)
    nt = len(CTS)
    W_BAND, W_PORT = nt, nt + 1
    nb_first = nt + len(CWALL) + 1
    for j in range(len(CPH)):
        cb[W_BAND][j].material_index = D
        cb[nb_first + 1][j].material_index = D
    ports, gallery = [], []
    for j, p in enumerate(CPH):
        c = 0.5 * (p + (CPH[j + 1] if j + 1 < len(CPH) else 180.0))
        if abs(c) > 168.0:
            gallery.append(cb[W_PORT][j])               # the observation gallery under the bridge, at the nose
        elif (abs(c) <= THETA + FLANK - 1.0 or 85.0 <= abs(c) <= 160.0) and j % 2 == 0:
            ports.append(cb[W_PORT][j])
    inset(cbm, gallery, 0.55, 0.5, W)
    for fc in ports:
        inset(cbm, [fc], 0.5, 0.4, W)
    for s0 in range(6):
        lo, hi = -60.0 + 20.0 * s0, -60.0 + 20.0 * (s0 + 1)
        cs = [j for j, p in enumerate(CPH) if lo + 1.0 < p + 0.6 < hi - 1.6]
        inset(cbm, [cb[k][j] for k in range(nb_first + 2, nb_first + 6) for j in cs], 0.5, 0.45)
    hull_parts = [piece(finish_bm("core", cbm), None, 1, bevel=0.1, angle=35)]
    for sx in (1, -1):
        cx, cy = sx * 28.0, HY + 70.0
        hull_parts.append(piece(f.sphere("dome", 3.2, center=(cx, cy, core_bot(cx, cy) + 0.9), segments=18, rings=10), W, 2))
    for a in range(-45, 46, 15):
        x, y = at(float(a), 0.955 * core_rmax(float(a)))
        hull_parts.append(piece(f.sphere("blight", 0.75, center=(x, y, core_bot(x, y) - 0.2), segments=10, rings=6), J, 3))
    # the mantle eyes: a blue sensor eye at the end of every groove (but the lance's), between the jets
    for g in GROOVES:
        if abs(g) < 1e-6:
            continue
        x, y = at(g, R + 1.0)
        ez = dome(x, y) - 0.4
        stalk_b = core_top(x, y) - 0.3
        stalk = f.lathe("stalk", [(0.0, 0.0), (1.1, 0.0), (0.8, 1.0), (0.8, ez - stalk_b - 2.4), (1.4, ez - stalk_b - 1.6),
                                  (1.4, ez - stalk_b - 1.2), (0.0, ez - stalk_b - 1.2)], segments=12,
                        axis=(0, 0, 1), origin=(x, y, stalk_b))
        hull_parts.append(piece(stalk, D, 4))
        hull_parts.append(piece(f.sphere("eye", 1.7, center=(x, y, ez), segments=16, rings=10), W, 4))

    # ================================================================== SOCKETS
    def cradle(name, centre, along, up, pid):
        kit.cradle(name, centre, along, up, pid, (PRISM_W, PRISM_H, PRISM_L), core_top)

    count = {"supershielded": 0, "shielded": 0, "prism": 0, "danger": 0}
    pid_s = 100
    for g in GROOVES:
        for tier, rho in SOCKET_ROWS:
            if abs(g) < 1e-6 and rho + PRISM_L / 2 > PAY_RHO0 - 1.0:
                continue                              # the centre groove's outer rows carry the lance
            x, y = at(g, rho)
            upn = surf_normal(x, y)
            top = Vector((x, y, dome(x, y) + tile_lift(rho)))
            x2, y2 = at(g, rho + 1.0)
            along = Vector((x2, y2, dome(x2, y2) + tile_lift(rho + 1.0))) - top
            nm = "socket.%s.%02d" % (tier, count[tier])
            count[tier] += 1
            cradle(nm, top - upn * (PRISM_H / 2), along, upn, pid_s)
            pid_s += 1
    for side in (1, -1):
        phi = side * (THETA + 0.56 * FLANK)
        for rho in DANGER_ROWS:
            x, y = at(phi, rho)
            upn = surf_normal(x, y)
            base = Vector((x, y, core_top(x, y)))
            x2, y2 = at(phi, rho + 1.0)
            along = Vector((x2, y2, core_top(x2, y2))) - base
            centre = base + upn * (0.9 + PRISM_H / 2 + 0.3)
            nm = "socket.danger.%02d" % count["danger"]
            count["danger"] += 1
            cradle(nm, centre, along, upn, pid_s)
            pid_s += 1
    ctx.log("sockets: %s (%d)" % (count, sum(count.values())))

    # ================================================================== PAYLOAD: the stinger lance in the centre groove
    def gpt(rho):
        x, y = at(0.0, rho)
        return Vector((x, y, dome(x, y) + tile_lift(rho) - PAY_R + 0.7))
    p_rear = gpt(PAY_RHO0)
    pax = (gpt(R - 6.0) - p_rear).normalized()
    plen = PAY_RHO1 - PAY_RHO0
    pay_mid, p_tip, dock_head = kit.lance(p_rear, pax, plen, core_top, PAY_R, LANCE_GAP, fracs=(0.12, 0.36, 0.56))

    # ================================================================== JETS across the trailing lip (engine.0 .. engine.7)
    ln = eng_len()
    eng = {}
    for k, pm in enumerate(MIDS):
        d = fan_dir(pm)
        o = Vector((ENG_RHO0 * d[0], HY + ENG_RHO0 * d[1], ENG_Z))
        axis = Vector((d[0], d[1], 0.0))
        nm = "engine.%d" % k
        kit.nacelle(nm, o, axis, ln, ENG_R)
        eng[nm] = (o, axis, ln)

    # ================================================================== OMNI REACTOR in the court behind the bridge
    # the court between the bridge (rho < UMBO_R) and the plate tips (rho > RHO0) is too short for the collar, so the
    # whole reactor hovers clear above both: its collar floats over the bridge dome's rim height
    rx, ry = at(0.0, REACTOR_RHO)
    ux, uy = at(0.0, UMBO_R)
    collar_o = Vector((rx, ry, umbo_top(ux, uy) + 1.8))
    OMNI = kit.reactor(collar_o, pylon_r=16.5, pylon_drop=6.0, pylon_len=17.0)

    # ================================================================== KEEL FIN under the core
    krings = []
    KY0, KY1 = HY - 26.0, HY + 112.0

    def keel_depth(a):
        return 9.0 * math.sin(math.pi * min(1.0, (1 - a) * 1.15)) ** 0.6 + 0.6
    for i in range(25):
        a = i / 24
        y = KY0 + (KY1 - KY0) * a
        ztop = core_bot(0.0, y) - 1.6
        depth = keel_depth(a)
        hw = 1.3 * (0.4 + 0.6 * math.sin(math.pi * a) ** 0.5)
        krings.append([Vector((hw, y, ztop)), Vector((hw * 0.7, y, ztop - depth * 0.7)), Vector((0.0, y, ztop - depth)),
                       Vector((-hw * 0.7, y, ztop - depth * 0.7)), Vector((-hw, y, ztop))])
    kbm, kg, kc = loft_bm(krings)
    for i, row in enumerate(kg):
        if 8 <= i <= 18:
            row[0].material_index = D
            row[3].material_index = D
    keel = piece(finish_bm("keel", kbm), None, 70, bevel=0.06, angle=35)
    kl = []
    for i in (7, 13, 19):
        a = i / 24
        y = KY0 + (KY1 - KY0) * a
        zt = core_bot(0.0, y) - 1.6 - keel_depth(a)
        kl.append(piece(f.sphere("klight", 0.7, center=(0.0, y, zt + 0.2), segments=10, rings=6), J, 71))
    bone_part([keel] + kl, "keel")

    bone_part(hull_parts, "hull")

    # ================================================================== JOIN
    mesh = kit.join(MESH_NODE)
    ctx.log("ark reverse joined: %d verts, %d tris" % (len(mesh.data.vertices), f.tri_count(mesh)))

    # ================================================================== RIG
    B = rig.BoneSpec
    HINGE = Vector((0, HY, dome(0, HY)))
    bones = [B("hull", (0, 0, 0), (0, -20.0, 0))]
    for k, pm in enumerate(MIDS):
        x, y = at(pm, R)
        bones.append(B("shell.%d" % k, tuple(HINGE), (x, y, dome(x, y)), parent="hull"))
    bones.append(B("umbo", tuple(HINGE), tuple(HINGE + Vector((0, -UMBO_R, 0))), parent="hull"))
    for sx, s in ((1, "L"), (-1, "R")):
        root, cm = ear_info[sx]
        bones.append(B("auricle." + s, tuple(root), tuple(root + Vector((sx * 40.0, 0, 0))), parent="hull"))
    bones.append(B("omni.ring", tuple(collar_o), tuple(OMNI), parent="hull"))
    bones.append(B("omni", tuple(OMNI), tuple(OMNI + Vector((0, 0, 10.0))), parent="omni.ring"))
    bones.append(B("payload.dock", tuple(dock_head), tuple(dock_head + pax * 6.0), parent="hull"))
    bones.append(B("payload", tuple(pay_mid), tuple(p_tip), parent="payload.dock"))
    for nm, (o, axis, ln_) in eng.items():
        bones.append(B(nm, tuple(o), tuple(o + axis * ln_), parent="hull"))
    bones.append(B("keel", (0, KY0, core_bot(0, KY0) - 2.0), (0, KY1, core_bot(0, KY1) - 2.0), parent="hull"))
    bones += kit.socket_bone_specs(B)
    arm = rig.build_armature(ARMATURE, bones)
    kit.roll_sockets(arm)
    rig.skin_rigid(mesh, arm, default_bone="hull")
    bone_names = [b.name for b in arm.data.bones]

    # ================================================================== MORPHS (3-15 % of the diagonal)
    me = mesh.data
    ms = morphs.MaskSet(mesh)
    m = {n_: ms.mask(n_) for n_ in bone_names}

    def belly(i, co, n):
        if m["hull"][i] <= 0 or co.z > -1.5:
            return Vector((0, 0, 0))
        return Vector((0, 0, (co.z + 1.5) * 0.6 * _smooth(-1.5, -9.0, co.z)))

    def keel_ride(i, co, n):
        if m["keel"][i] <= 0:
            return Vector((0, 0, 0))
        return Vector((0, 0, (core_bot(co.x, co.y) + 1.5) * 0.6))

    def fatten(i, co, n):
        for e, (o, axis, ln_) in eng.items():
            if m[e][i] > 0:
                d = co - o
                return (d - axis * d.dot(axis)) * 0.28
        return Vector((0, 0, 0))

    def slide(mask, dv):
        dv = Vector(dv)

        def fn(i, co, n):
            return dv * mask[i] if mask[i] > 0 else Vector((0, 0, 0))
        return fn
    cmast = {sx: ear_info[sx][1] for sx in (1, -1)}

    def telescope(i, co, n):
        for sx, side in ((1, "auricle.L"), (-1, "auricle.R")):
            if m[side][i] > 0 and abs(co.x - cmast[sx].x) < 3.6 and co.z > cmast[sx].z + 0.2 and abs(co.y - cmast[sx].y) < 3.5:
                return Vector((0, 0, (co.z - cmast[sx].z) * 0.6))
        if m["umbo"][i] > 0 and abs(co.x) < 3.5 and co.z > peak.z + 0.2 and abs(co.y - peak.y) < 5.0:
            return Vector((0, 0, (co.z - peak.z) * 0.6))
        return Vector((0, 0, 0))

    shapes = {
        "Charge": [morphs.stretch_along(tuple(pax), tuple(p_rear), 1.35, m["payload"]),
                   morphs.scale_about(1.35, tuple(OMNI), m["omni"])],
        "Mass": [belly, keel_ride, fatten],
        "Space": [slide(m["auricle.L"], (11.0, 0, 0)), slide(m["auricle.R"], (-11.0, 0, 0)), telescope],
        "Time": [morphs.stretch_along(tuple(axis), tuple(o), 1.5, m[e]) for e, (o, axis, ln_) in eng.items()],
    }
    for name in ("Charge", "Mass", "Space", "Time"):
        kb, mx, frac, moved = morphs.add_shape(mesh, name, shapes[name])
        ctx.log("shape %s: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))
    if "pid" in me.attributes:
        me.attributes.remove(me.attributes["pid"])

    # ================================================================== TAKES
    # world degrees in the Blender frame: +X lifts a point behind the pivot (the trailing lip rises about the hinge)
    poses = {
        "clap": {"shell.%d" % k: (10, 0, 0) for k in range(NPLATE)},
        "release": {"payload": {"rot": (0, 0, 0), "loc": tuple(pax * 20.0)}},
    }
    names = ("clap", "release")
    for p in names:
        takes.pose_take(arm, p, poses[p])
    takes.clear_pose(arm)

    lo, hi = f.bounds(mesh)
    nose_y = HY - NOSE_R
    ctx.log("bounds (blender) %s .. %s; nose to jets %.1f (want %.1f), jets %.1f long" % (
        tuple(round(v, 1) for v in lo), tuple(round(v, 1) for v in hi), jets_y() - nose_y, NOSE_TO_JETS, ln))
    assert abs((jets_y() - nose_y) - NOSE_TO_JETS) < 0.02 * NOSE_TO_JETS, "nose to jets drifted off half a nucleus radius"
    return {
        "armature": arm, "mesh": mesh,
        "spec": {
            "preset": "zup", "armature": ARMATURE, "mesh_node": MESH_NODE,
            "bones": bone_names,
            "takes": [ARMATURE + "|" + p for p in names],
            "pose_takes": [ARMATURE + "|" + p for p in names],
            "sheet_poses": names,
            "pose_expect": {
                "clap": [("shell.3", "z", +1, 3.0)],
                "release": [("payload", "y", +1, 10.0)],
            },
            "shapes": ["Charge", "Mass", "Space", "Time"],
            "slots": list(SLOTS),
            "size_ref_blender": (tuple(lo), tuple(hi)),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.03,
        },
    }
