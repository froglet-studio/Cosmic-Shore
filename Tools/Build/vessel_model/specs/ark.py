"""
ark - a NEW hull with no artist file: the ARK, a large mothership (Garrett, 2026-10-27: "a large mothership that is
decorated with prisms, shielded prisms, danger prisms, supershielded prisms. these are the ark's health and they will
regenerate into socketed locations that should look like the prism belongs there. base the superstructure off half an
arc clam shell. the ark should have locations for an inactive visible omni crystal that thematically powers the vessel
and a payload that players can grab and use as a weapon that the ark will regenerate").
Round 2 (same day): "make this into a detailed and polished ship. from nose to jets it will be half the radius of a
nucleus, so when players fly next to it they will need to see a detailed mothership. there should be a cockpit
windshield at the smooth part where all the corrugations converge."
A spaceship inspired by a scallop (Pectinidae): ONE valve of the shell, never the animal.

SCALE. A standard nucleus is 392 units in radius (Nucleus.prefab at scale 400, SliceArenaGeometryTests), so the ark is
196 units from the nose (the core's rim, just past the shell's lip) to the jet mouths, about 18 Squirrels. Detail is sized for a vessel flying alongside.

THE READ: from a passing vessel it must say "a clam-shell mothership crowned with prisms": a fan of ribbed, shingled
hull plates radiating from a smooth domed bridge, a prism lying in every groove between the ribs as if the groove was
cut for it, a ring of danger prisms below the lip, a crystal hovering in a reactor cage over the engines, and one long
lance lying in the centre groove with its head past the lip.

Clade -> ship:
  radial ribs + riblets    -> EIGHT SHELL PLATES (bones `shell.0` .. `shell.7`), each a crowned rib with three riblets,
                              split into three SHINGLED armour tiles (the scallop's growth rings: each tile rises toward
                              its outer edge, so every seam is a step), sunken panels in each tile, a domain band on
                              the lip and a domain stripe down the outer tiles' crown, running lights at the lip corners
  the grooves              -> PRISM SOCKETS: every groove carries cradles cut to the prism (a U channel: floor, domain
                              side rails, end stops, two Engine-slot regen pads, a pedestal off the core). A socketed
                              prism's top lies flush with the rib edges, so it reads as part of the shell
  the umbo (smooth beak    -> the BRIDGE DOME (bone `umbo`): a smooth dome where every rib converges, with a seven-pane
  where ribs converge)        wraparound COCKPIT WINDSHIELD on its forward face (domain sill and brow), portholes round
                              its flanks, a domain rim, a sensor mast with a beacon and a dish on its crown
  the auricles (ears)      -> two HANGAR WINGS beside the hinge (bones `auricle.L` / `auricle.R`): ribbed flanges with a
                              lit hangar mouth on each outer end, a manoeuvring thruster aft and a comm mast
  the mantle / body        -> the CORE (root `hull`): a lens hull under the plates with a chamfered wall carrying a
                              domain band, a row of portholes and an observation gallery at the prow, ventral panels and
                              two sensor domes underneath
  (keel)                   -> a ventral KEEL FIN (bone `keel`) floating under the core, domain stripe, running lights
  the hinge ligament       -> the ENGINE BANK behind the hinge: three floating nacelles (bones `engine.L` / `engine.C` /
                              `engine.R`) with intake lips, domain bands, cooling fins and glowing bell nozzles
  (no clade part)          -> the OMNI REACTOR over the engines: the dormant omni crystal (bone `omni`, Window slot =
                              inactive glass) floating in a gimballed cage (bone `omni.ring`: two rings, four
                              containment pylons, a glowing base collar). It thematically powers the ark
  (no clade part)          -> the PAYLOAD (bone `payload`): a lance lying in the centre groove on three forked clamps
                              (bone `payload.dock`, regen pads), its glowing head past the lip. Players grab it; the
                              ark regrows it in its clamps

SOCKETS. One bone per socket, `socket.<tier>.<nn>`, head at the socketed prism's CENTRE, tail along the prism's length
(toward the lip for groove sockets, along the lip for rim sockets), bone Z (roll) = the surface's up. Each cradle mesh
is skinned to its socket bone, so Unity's bone optimisation keeps it. The socket prism is PRISM_W x PRISM_H x PRISM_L
(across x up x along): spawn the prism at the bone, scaled to that, and it sits in the groove.
  supershielded  the two innermost groove rows, nearest the bridge (14)
  shielded       the two middle rows (12)
  prism          the two outer rows (12)
  danger         below the lip, two per plate (16)
The payload's regen point is the `payload` bone's rest relative to `payload.dock`.

A HOVERING ASSEMBLY (vessel-model section 0.5): `assembly_report.py Ark.fbx --floating
shell.0,...,shell.7,umbo,auricle.L,auricle.R,omni.ring,omni,payload,engine.L,engine.C,engine.R,keel`.

Contract (FLEET_CONVENTION sections 1-4 for a NEW body):
  file      Assets/_Models/Vessel Models/Ark.fbx (guid 3a7c5e1f9b2d4c60a8e4f1b7d3c9e205), preset "zup"
  frame     Blender: nose -Y, up +Z, pilot's left +X. Unity (x, y, z) = Blender (-x, z, -y): NOSE = Unity +Z.
            The hinge sits at Blender (0, HY, 0): the origin is on the centre line, HY ahead of it.
  nodes     mesh `mesh` (identity, metres), armature `rig`; root bone `hull`
  slots     0 Domain, 1 Body, 2 Window, 3 Engine
  shapes    Charge (weapon): the lance lengthens (the omni crystal is always one size);
            Mass (volume):   the belly deepens (the keel rides it) and the nacelles fatten;
            Space (reach):   the hangar wings slide outward and the masts telescope;
            Time (rate):     the nacelles stretch back
            None moves a plate, a cradle or a socket bone, so a socketed prism always sits in its cradle.
  takes     rest (first); clap (the shell lifts its lip about the hinge); release (the lance slides forward out of
            its clamps). Single-frame poses, no blend-shape curves.
"""

import math

NAME = "Ark"
OUTPUT = "Assets/_Models/Vessel Models/Ark.fbx"

ARMATURE = "rig"
MESH_NODE = "mesh"
SLOTS = ("Domain", "Body", "Window", "Engine")
D, H, W, J = 0, 1, 2, 3

# ---------------------------------------------------------------- layout (Blender frame: nose -Y, up +Z, left +X)
NOSE_TO_JETS = 196.0      # half a standard nucleus radius (392)
HY = 74.0                 # the hinge (umbo centre) on the centre line; the fan opens toward -Y
R = 118.0                 # the plates' outer radius (the lip)
THETA = 62.0              # the fan's half angle, degrees
NPLATE = 8                # plates; NPLATE - 1 grooves (one on the centre line: the payload's)
GROOVE = 8.4              # constant groove width between plates
RHO0 = 46.0               # where the plates begin (inside: the bridge dome)
TILES = ((RHO0, 71.0), (71.6, 95.6), (96.2, R))   # shingled tiles per plate; 0.6 seams
SHINGLE = 0.7             # each tile's rise toward its outer edge
Z0, HDOME = 6.0, 24.0     # lip height, the dome's rise
CROWN, RIBLET = 1.6, 0.55
PLATE_T = 1.8
CORE_DROP = 6.5           # core top below the dome surface
RC = R + 10.0             # the core's radius in the fan (a margin past the lip for the rim sockets): the NOSE
MARGIN_Z = Z0 - 0.6 - CORE_DROP

UMBO_R, UMBO_R_AFT = 38.0, 20.0
UMBO_LIFT = 9.5

# the socketed prism (across, up, along)
PRISM_W, PRISM_H, PRISM_L = 4.0, 1.6, 9.0
SOCKET_ROWS = (("supershielded", 53.5), ("supershielded", 64.5), ("shielded", 78.0), ("shielded", 89.5),
               ("prism", 102.0), ("prism", 112.5))
DANGER_RHO = R + 5.0

# the payload lance along the centre groove
PAY_RHO0, PAY_RHO1 = 72.0, 128.0
PAY_R = 1.8
LANCE_GAP = 1.4          # the lance hovers this far over its cradle's prongs

# the engine bank and reactor
ENG_Y0 = HY + 35.5                          # nacelle fronts, clear of the core's tail
ENG_LEN = NOSE_TO_JETS - RC - (ENG_Y0 - HY)  # the jets end NOSE_TO_JETS behind the nose (the core's rim)
ENGINES = (("engine.L", 24.0, 2.0, 7.5), ("engine.C", 0.0, -2.0, 9.0), ("engine.R", -24.0, 2.0, 7.5))
REACTOR_Y = HY + 38.0
COLLAR_Z = 15.0


def _ang(dx, dy):
    """Fan angle (degrees) of a point dx across, dy ahead of the hinge: 0 straight ahead, +90 to +X."""
    return math.degrees(math.atan2(dx, dy))


def dome(x, y):
    """The valve's top surface height at (x, y): highest at the hinge, falling to the lip and toward the fan's flanks,
    and falling fast behind the hinge."""
    dx, dy = x, HY - y
    rho = math.hypot(dx, dy)
    th = _ang(dx, dy) if rho > 1e-6 else 0.0
    u = min(rho / R, 1.15)
    # the flank fall-off fades in away from the hinge, so the bridge dome's crown is smooth (no crease where the
    # angle term jumps at the centre); from the plates outward (rho >= 40) it is the full fall-off
    rise = HDOME * (1.0 - u ** 2.1) * (1.0 - 0.38 * min(1.0, (th / THETA) ** 2) * _smooth(0.0, 40.0, rho))
    if dy < 0:
        rise *= math.exp(-(dy / 24.0) ** 2)
    return Z0 + max(rise, -1.0)


def fan_dir(phi_deg):
    p = math.radians(phi_deg)
    return (math.sin(p), -math.cos(p))


def fan_perp(phi_deg):
    """Unit vector across the fan toward increasing phi."""
    p = math.radians(phi_deg)
    return (math.cos(p), math.sin(p))


def at(phi, rho):
    d = fan_dir(phi)
    return rho * d[0], HY + rho * d[1]


PHI = [-THETA + k * (2 * THETA / NPLATE) for k in range(NPLATE + 1)]   # plate edges; PHI[1:-1] are the grooves
GROOVES = PHI[1:-1]


def tile_lift(rho):
    for a, b in TILES:
        if a - 0.31 <= rho <= b + 0.31:
            return SHINGLE * min(max((rho - a) / (b - a), 0.0), 1.0)
    return 0.0


def _smooth(a, b, x):
    t = min(max((x - a) / (b - a), 0.0), 1.0)
    return t * t * (3 - 2 * t)


def core_rmax(phi):
    a = abs(phi)
    if a <= THETA + 3:
        return RC
    if a <= 78.0:
        return 34.0 + (RC - 34.0) * (1 - _smooth(THETA + 3, 78.0, a))
    return 34.0 - 2.0 * _smooth(78.0, 180.0, a)


def core_top(x, y):
    return max(dome(x, y), Z0 - 0.6) - CORE_DROP


def core_bot(x, y):
    rho = math.hypot(x, HY - y)
    phi = _ang(x, HY - y) if rho > 1e-6 else 0.0
    q = min(rho / core_rmax(phi), 1.0)
    return -9.0 - 10.0 * (1 - q * q) ** 0.8


def umbo_rmax(phi):
    a = abs(phi)
    if a <= 80.0:
        return UMBO_R
    return UMBO_R_AFT + (UMBO_R - UMBO_R_AFT) * 0.5 * (1 + math.cos(math.pi * (a - 80.0) / 100.0))


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

    # ================================================================== SHELL PLATES (shell.0 .. shell.7)
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
            # domain: the lip band and a stripe down the crown of the outer tiles; the lip cap
            for t, row in enumerate(grid):
                for s in range(NS):
                    if ti == 2 and t >= NRT - 2:
                        row[s].material_index = D
                    elif ti >= 1 and s in (NS // 2 - 1, NS // 2):
                        row[s].material_index = D
            if ti == 2:
                for fc in caps[1]:
                    fc.material_index = D
            # sunken panels: the tile's middle, either side of the crown stripe
            r_lo, r_hi = (2, NRT - 3) if ti < 2 else (2, NRT - 4)
            inset(bm, [grid[t][s] for t in range(r_lo, r_hi) for s in range(3, NS // 2 - 2)], 0.35, 0.3)
            inset(bm, [grid[t][s] for t in range(r_lo, r_hi) for s in range(NS // 2 + 2, NS - 3)], 0.35, 0.3)
            tiles.append(piece(finish_bm("tile", bm), None, 10 + k, bevel=0.12, angle=35))
        # running lights at the lip corners
        for (dd, nn, off, side) in ((da, na, oa, 1), (db, nb, ob, -1)):
            rho = R - 1.4
            p = Vector((rho * dd[0] + side * (off + 1.2) * nn[0], HY + rho * dd[1] + side * (off + 1.2) * nn[1], 0))
            p.z = dome(p.x, p.y) + SHINGLE + 0.5
            lt = f.sphere("light", 0.55, center=tuple(p), segments=10, rings=6)
            tiles.append(piece(lt, J, 10 + k))
        bone_part(tiles, "shell.%d" % k)

    # ================================================================== THE BRIDGE DOME (umbo) + COCKPIT WINDSHIELD
    UPH = [-180.0 + 2.5 * j for j in range(144)]
    UTS = [0.10, 0.20, 0.30, 0.40, 0.50, 0.58, 0.64, 0.72, 0.80, 0.88, 0.93, 0.97, 0.995]

    def umbo_top(x, y):
        rho = math.hypot(x, HY - y)
        phi = _ang(x, HY - y) if rho > 1e-6 else 0.0
        q = min(rho / umbo_rmax(phi), 1.0)
        return dome(x, y) + 1.2 + UMBO_LIFT * (1 - q ** 5)

    def umbo_bot(x, y):
        return dome(x, y) - 2.6
    ubm, ub = polar_bm(UPH, UTS, umbo_rmax, umbo_top, umbo_bot, (0.5,))
    band = {t: i for i, t in enumerate(UTS[:-1])}       # top band index by its inner ring

    def cols(lo, hi):
        return [j for j, p in enumerate(UPH) if lo <= p + 1.25 <= hi]
    front = cols(-45.0, 45.0)
    for j in front:
        ub[band[0.58]][j].material_index = D           # brow
        ub[band[0.88]][j].material_index = D           # sill
    for j in range(len(UPH)):
        ub[band[0.97]][j].material_index = D           # rim band
    # seven panes of four columns, one-column mullions, three rows tall
    base = cols(-42.5, -40.0)[0]
    for pane in range(7):
        cs = [base + pane * 5 + c for c in range(4)]
        inset(ubm, [ub[band[t]][j] for t in (0.64, 0.72, 0.80) for j in cs], 0.3, 0.6, W)
    # hull panels on the dome's crown: 30-degree sectors (the forward one is the windshield's), one-column gutters
    for s0 in range(12):
        lo = -180.0 + 30.0 * s0
        if lo + 30.0 > -50.0 and lo < 50.0:
            continue
        cs = cols(lo + 2.5, lo + 27.5)
        inset(ubm, [ub[band[t]][j] for t in (0.20, 0.30, 0.40) for j in cs], 0.35, 0.3)
    # portholes round the flanks
    for j, p in enumerate(UPH):
        c = p + 1.25
        if 52.0 <= abs(c) <= 150.0 and j % 2 == 0:
            inset(ubm, [ub[band[0.93]][j]], 0.22, 0.35, W)
    umbo = piece(finish_bm("umbo", ubm), None, 20, bevel=0.1, angle=35)
    peak = Vector((0.0, HY - 4.0, umbo_top(0.0, HY - 4.0)))
    mast = f.tube_along("mast", [tuple(peak - Vector((0, 0, 0.8))), tuple(peak + Vector((0, 0, 4.0))), tuple(peak + Vector((0, 0, 9.0)))],
                        [0.75, 0.55, 0.35], segments=10)
    beacon = f.sphere("beacon", 0.9, center=tuple(peak + Vector((0, 0, 9.6))), segments=12, rings=8)
    dish_o = peak + Vector((0, 2.2, 3.0))
    dish = f.lathe("dish", [(0.0, 0.0), (1.2, 0.15), (2.4, 0.55), (2.8, 0.95), (2.5, 0.95), (1.1, 0.45), (0.0, 0.3)], segments=16,
                   axis=(0, -0.5, 0.86), origin=tuple(dish_o))
    umbo_mast = [piece(mast, H, 21), piece(beacon, J, 21), piece(dish, H, 21)]
    bone_part([umbo] + umbo_mast, "umbo")

    # ================================================================== HANGAR WINGS (auricle.L / auricle.R)
    ear_info = {}
    for side_, sx in (("L", 1), ("R", -1)):
        x0, x1 = 46.0, 88.0
        rings = []
        NX, NY = 14, 20
        for i in range(NX + 1):
            a = i / NX
            ax = x0 + (x1 - x0) * a
            yf = HY - 12.0 + 14.0 * a ** 1.3
            yb = HY + 16.0 - 2.0 * a
            zt = 15.0 - 4.0 * a
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
        for row in grid:
            for j in range(2):
                row[j].material_index = D                # the leading edge
        inset(bm, [grid[i][j] for i in range(2, NX - 2) for j in range(4, NY - 3)], 0.4, 0.3)
        inset(bm, caps[1], 0.9, 3.5, W)                 # the hangar mouth on the outer end (a lit force field)
        wing = piece(finish_bm("auricle", bm), None, 30 + (sx > 0), bevel=0.1, angle=35)
        ex = sx * (x1 - 9.0)
        ey = HY + 16.0 - 2.0 * ((abs(ex) - x0) / (x1 - x0))
        thr = f.lathe("thruster", [(0.0, -0.4), (1.6, -0.4), (2.0, 1.2), (2.3, 3.2), (1.8, 3.4), (1.2, 2.6), (0.0, 2.8)], segments=14,
                      axis=(0, 1, 0), origin=(ex, ey, 9.0))
        f.ensure_slots(thr, SLOTS)
        f.paint(thr, H)
        f.paint(thr, J, lambda c, n, i, ex=ex, ey=ey: c.y > ey + 2.0 and math.hypot(c.x - ex, c.z - 9.0) < 1.4)
        cm_base = Vector((sx * 66.0, HY + 6.0, 15.0 - 4.0 * 20.0 / 42.0 + 1.0))
        cm = f.tube_along("cmast", [tuple(cm_base - Vector((0, 0, 0.6))), tuple(cm_base + Vector((0, 0, 3.0))), tuple(cm_base + Vector((0, 0, 7.0)))],
                          [0.6, 0.45, 0.3], segments=10)
        cdish = f.lathe("cdish", [(0.0, 0.0), (1.0, 0.12), (2.0, 0.45), (2.3, 0.8), (2.0, 0.8), (0.9, 0.38), (0.0, 0.25)], segments=14,
                        axis=(sx * 0.6, 0.2, 0.77), origin=tuple(cm_base + Vector((sx * 1.2, 0, 5.0))))
        ctip = f.sphere("ctip", 0.5, center=tuple(cm_base + Vector((0, 0, 7.5))), segments=10, rings=6)
        bone_part([wing, piece(thr, None, 32), piece(cm, H, 33), piece(cdish, H, 33), piece(ctip, J, 33)], "auricle." + side_)
        ear_info[sx] = (Vector((sx * x0, HY, 12.0)), cm_base)

    # ================================================================== CORE (hull): the lens under the plates
    CPH = sorted(set([-180.0 + 2.5 * j for j in range(144)] +
                     [s * (60.0 + 1.25 * j) for j in range(1, 16) for s in (1, -1)]))
    CTS = [0.10, 0.22, 0.34, 0.46, 0.58, 0.68, 0.76, 0.83, 0.89, 0.94, 0.975, 0.992]
    CWALL = (0.06, 0.30, 0.55, 0.80, 0.94)
    cbm, cb = polar_bm(CPH, CTS, core_rmax, core_top, core_bot, CWALL)
    nt = len(CTS)
    # loops: top rings 0..nt-1, wall nt..nt+4, bottom rings; band k joins loop k and k+1
    W_BAND, W_PORT = nt, nt + 1
    nb_first = nt + len(CWALL) + 1                    # first bottom band (outermost)
    for j in range(len(CPH)):
        cb[W_BAND][j].material_index = D
        cb[nb_first + 1][j].material_index = D       # a domain ring on the belly
    ports, gallery = [], []
    for j, p in enumerate(CPH):
        c = 0.5 * (p + (CPH[j + 1] if j + 1 < len(CPH) else 180.0))
        if abs(c) < 9.0:
            gallery.append(cb[W_PORT][j])
        elif (abs(c) <= 60.0 or 100.0 <= abs(c) <= 170.0) and j % 2 == 0:
            ports.append(cb[W_PORT][j])
    inset(cbm, gallery, 0.55, 0.5, W)
    for fc in ports:
        inset(cbm, [fc], 0.5, 0.4, W)
    # ventral panels: six sectors of the belly, gutters between
    for s0 in range(6):
        lo, hi = -60.0 + 20.0 * s0, -60.0 + 20.0 * (s0 + 1)
        cs = [j for j, p in enumerate(CPH) if lo + 1.0 < p + 0.6 < hi - 1.6]
        inset(cbm, [cb[k][j] for k in range(nb_first + 2, nb_first + 6) for j in cs], 0.5, 0.45)
    core = piece(finish_bm("core", cbm), None, 1, bevel=0.1, angle=35)
    hull_parts = [core]
    for sx in (1, -1):
        cx, cy = sx * 30.0, HY - 62.0
        dz = core_bot(cx, cy)
        dm = f.sphere("dome", 3.2, center=(cx, cy, dz + 0.9), segments=18, rings=10)
        hull_parts.append(piece(dm, W, 2))
    # belly running lights, just inside the domain ring
    for a in range(-60, 61, 15):
        x, y = at(float(a), 0.955 * core_rmax(float(a)))
        hull_parts.append(piece(f.sphere("blight", 0.75, center=(x, y, core_bot(x, y) - 0.2), segments=10, rings=6), J, 3))

    # ================================================================== SOCKETS: cradles cut to the prism
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
    dphi = 2 * THETA / NPLATE
    for k in range(NPLATE):
        for off in (-0.25, 0.25):
            pm = 0.5 * (PHI[k] + PHI[k + 1]) + off * dphi
            x, y = at(pm, DANGER_RHO)
            n = fan_perp(pm)
            centre = Vector((x, y, MARGIN_Z + 0.9 + PRISM_H / 2 + 0.3))
            nm = "socket.danger.%02d" % count["danger"]
            count["danger"] += 1
            cradle(nm, centre, Vector((n[0], n[1], 0)), Vector((0, 0, 1)), pid_s)
            pid_s += 1
    ctx.log("sockets: %s (%d)" % (count, sum(count.values())))

    # ================================================================== PAYLOAD (the lance) + its dock
    def gpt(rho):
        x, y = at(0.0, rho)
        return Vector((x, y, dome(x, y) + tile_lift(rho) - PAY_R + 0.7))
    p_rear = gpt(PAY_RHO0)
    p_lip = gpt(R - 6.0)
    pax = (p_lip - p_rear).normalized()
    plen = PAY_RHO1 - PAY_RHO0
    pay_mid, p_tip, dock_head = kit.lance(p_rear, pax, plen, core_top, PAY_R, LANCE_GAP)

    # ================================================================== ENGINE BANK (behind the hinge)
    eng = {}
    for nm, ex, ez, rr in ENGINES:
        o = Vector((ex, ENG_Y0, ez))
        kit.nacelle(nm, o, (0, 1, 0), ENG_LEN, rr)
        eng[nm] = (o, ENG_LEN, rr)

    # ================================================================== OMNI REACTOR over the engines
    collar_o = Vector((0.0, REACTOR_Y, COLLAR_Z))
    OMNI = kit.reactor(collar_o)

    # ================================================================== KEEL FIN under the core
    krings = []
    KY0, KY1 = HY - 92.0, HY + 22.0

    def keel_depth(a):
        return 9.0 * math.sin(math.pi * min(1.0, a * 1.15)) ** 0.6 + 0.6
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
        if 6 <= i <= 16:
            row[0].material_index = D
            row[3].material_index = D
    keel = piece(finish_bm("keel", kbm), None, 70, bevel=0.06, angle=35)
    kl = []
    for i in (5, 11, 17):
        a = i / 24
        y = KY0 + (KY1 - KY0) * a
        zt = core_bot(0.0, y) - 1.6 - keel_depth(a)
        kl.append(piece(f.sphere("klight", 0.7, center=(0.0, y, zt + 0.2), segments=10, rings=6), J, 71))
    bone_part([keel] + kl, "keel")

    bone_part(hull_parts, "hull")

    # ================================================================== JOIN, n-gons out
    mesh = kit.join(MESH_NODE)
    ctx.log("ark joined: %d verts, %d tris" % (len(mesh.data.vertices), f.tri_count(mesh)))

    # ================================================================== RIG
    B = rig.BoneSpec
    HINGE = Vector((0, HY, dome(0, HY)))
    bones = [B("hull", (0, 0, 0), (0, -20.0, 0))]
    for k in range(NPLATE):
        pm = 0.5 * (PHI[k] + PHI[k + 1])
        x, y = at(pm, R)
        bones.append(B("shell.%d" % k, tuple(HINGE), (x, y, dome(x, y)), parent="hull"))
    bones.append(B("umbo", tuple(HINGE), tuple(HINGE + Vector((0, -UMBO_R, 0))), parent="hull"))
    for sx, s in ((1, "L"), (-1, "R")):
        root, cm = ear_info[sx]
        bones.append(B("auricle." + s, tuple(root), tuple(root + Vector((sx * 42.0, 0, 0))), parent="hull"))
    bones.append(B("omni.ring", tuple(collar_o), tuple(OMNI), parent="hull"))
    bones.append(B("omni", tuple(OMNI), tuple(OMNI + Vector((0, 0, 10.0))), parent="omni.ring"))
    bones.append(B("payload.dock", tuple(dock_head), tuple(dock_head + pax * 6.0), parent="hull"))
    bones.append(B("payload", tuple(pay_mid), tuple(p_tip), parent="payload.dock"))
    for nm, (o, ln, rr) in eng.items():
        bones.append(B(nm, tuple(o), tuple(o + Vector((0, ln, 0))), parent="hull"))
    bones.append(B("keel", (0, KY0, core_bot(0, KY0) - 2.0), (0, KY1, core_bot(0, KY1) - 2.0), parent="hull"))
    bones += kit.socket_bone_specs(B)
    arm = rig.build_armature(ARMATURE, bones)
    kit.roll_sockets(arm)                              # the socket's Z = the prism's up
    rig.skin_rigid(mesh, arm, default_bone="hull")
    bone_names = [b.name for b in arm.data.bones]

    # ================================================================== MORPHS (3-15 % of the diagonal)
    me = mesh.data
    ms = morphs.MaskSet(mesh)
    m = {n: ms.mask(n) for n in bone_names}

    def belly(i, co, n):
        # the core's underside deepens; its top (and so every pedestal) stays
        if m["hull"][i] <= 0 or co.z > -1.5:
            return Vector((0, 0, 0))
        return Vector((0, 0, (co.z + 1.5) * 0.6 * _smooth(-1.5, -9.0, co.z)))

    def keel_ride(i, co, n):
        if m["keel"][i] <= 0:
            return Vector((0, 0, 0))
        zb = core_bot(co.x, co.y)
        return Vector((0, 0, (zb + 1.5) * 0.6))

    def fatten(i, co, n):
        for e, (o, ln, rr) in eng.items():
            if m[e][i] > 0:
                return Vector((co.x - o.x, 0, co.z - o.z)) * 0.28
        return Vector((0, 0, 0))

    def slide(mask, dv):
        dv = Vector(dv)

        def fn(i, co, n):
            return dv * mask[i] if mask[i] > 0 else Vector((0, 0, 0))
        return fn
    cmast = {sx: ear_info[sx][1] for sx in (1, -1)}

    def telescope(i, co, n):
        # the masts (above their bases) stretch up 1.6x
        for sx, side in ((1, "auricle.L"), (-1, "auricle.R")):
            if m[side][i] > 0 and abs(co.x - cmast[sx].x) < 3.6 and co.z > cmast[sx].z + 0.2 and abs(co.y - cmast[sx].y) < 3.5:
                return Vector((0, 0, (co.z - cmast[sx].z) * 0.6))
        if m["umbo"][i] > 0 and abs(co.x) < 3.5 and co.z > peak.z + 0.2 and abs(co.y - peak.y) < 5.0:
            return Vector((0, 0, (co.z - peak.z) * 0.6))
        return Vector((0, 0, 0))

    shapes = {
        # The omni crystal is always one size (Garrett, 2026-10-28): Charge lengthens the lance and leaves it alone.
        "Charge": [morphs.stretch_along(tuple(pax), tuple(p_rear), 1.38, m["payload"])],
        "Mass": [belly, keel_ride, fatten],
        "Space": [slide(m["auricle.L"], (11.0, 0, 0)), slide(m["auricle.R"], (-11.0, 0, 0)), telescope],
        "Time": [morphs.stretch_along((0, 1, 0), tuple(o), 1.55, m[e]) for e, (o, ln, rr) in eng.items()],
    }
    for name in ("Charge", "Mass", "Space", "Time"):
        kb, mx, frac, moved = morphs.add_shape(mesh, name, shapes[name])
        ctx.log("shape %s: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))
    if "pid" in me.attributes:
        me.attributes.remove(me.attributes["pid"])

    # ================================================================== TAKES
    # world degrees in the Blender frame: -X lifts a point ahead of the pivot (the lip rises about the hinge)
    poses = {
        "clap": {"shell.%d" % k: (-10, 0, 0) for k in range(NPLATE)},
        "release": {"payload": {"rot": (0, 0, 0), "loc": tuple(pax * 20.0)}},
    }
    names = ("clap", "release")
    for p in names:
        takes.pose_take(arm, p, poses[p])
    takes.clear_pose(arm)

    lo, hi = f.bounds(mesh)
    nose_y, jets_y = HY - RC, ENG_Y0 + ENG_LEN
    ctx.log("bounds (blender) %s .. %s; nose to jets %.1f (want %.1f), lance tip %.1f past the nose" % (
        tuple(round(v, 1) for v in lo), tuple(round(v, 1) for v in hi), jets_y - nose_y, NOSE_TO_JETS, nose_y - lo[1]))
    assert abs((jets_y - nose_y) - NOSE_TO_JETS) < 0.02 * NOSE_TO_JETS, "nose to jets drifted off half a nucleus radius"
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
                "release": [("payload", "y", -1, 10.0)],
            },
            "shapes": ["Charge", "Mass", "Space", "Time"],
            "slots": list(SLOTS),
            "size_ref_blender": (tuple(lo), tuple(hi)),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.03,
        },
    }
