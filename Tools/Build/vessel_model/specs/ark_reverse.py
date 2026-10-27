"""
ark_reverse - the Ark's SECOND CONCEPT: the clam valve facing the other way (Garrett, 2026-10-27: "this is one concept.
now lets explore an alternative where the arc is facing the other way. get re-inspired from the arc clam shell. and have
it face the opposite direction."). `ark.py` (Ark.fbx) is the first concept: hinge aft, fan opening forward to a lip at
the nose. Here the HINGE IS THE PROW and the fan opens AFT, so the shell is a swept delta with the bridge at its point.

Round 3 (same day) asked for engines with clearance to manoeuvre, the game's own shield shapes as sockets the hull is
designed around (stellated octahedra for supershielded prisms, octahedra for shielded), danger prisms where the Ark
wants to be dangerous, a hangar the escorts launch from, and a body that plays like a level: "a kaiju protected by three
vessels and its own regeneration ... somewhere interesting when you are small and on its surface or moving through its
body channels, ducts, and curving passageways."
Round 4 (same day): "it needs to look less like a darthvader panel meets a child's toy. this should be a menacing,
intimidating mothership. more torn like cyberpunk. you can maintain a touch of whimsy but the current placement of the
prisms look like it has stars and buttons all over it. it needs some more wickedness."

THE PRISMS ARE THE GAME'S OWN SHAPES. A shielded prism settles into an octahedron with its vertices at 3x the prism's
half-extents (OctahedronMeshGenerator, CIRCUMSCRIBING_SCALE 3); a supershielded prism into the stella octangula with its
spike tips at the cube corners 3x out (StellatedOctahedronMeshGenerator). The Ark's shield sockets hold CUBIC prisms, so
their shields are the REGULAR octahedron and the REGULAR stellated octahedron; every socket is designed around the
shield, not the box. Round 4 takes them off the surface (no more stars and buttons) and into the body:
  supershielded  stellations CLUTCHED and SUNK: each set through a star-cut hole where a groove crosses a growth seam
                 (the inner seam and the middle one, alternating groove by groove, so they never form a row), only its
                 upper spikes rising out of the socket, gripped by four hooked talons glowing at the tips, an X-cradle
                 under it cut to its tetrahedron edges; one in each pincer, two in the prow's cheeks. They are the
                 firewalls between the groove cells
  shielded       octahedra in the dark of the MANTLE CAVITY, a brood under the plates on round cups, seen through the
                 stern maw, the grooves and the rips in the armour
  prism          GILLS: slabs standing in the grooves, raked aft, on rails under the rib edges (the scallop's gill
                 lamellae): from above a row of thin glowing fins, never a row of tiles
  danger         TEETH: fangs hanging from the plates' lip and rising from the core's rim (the stern maw into the
                 cavity), saw-blades raked back along the swept leading edges, fangs at the hangar's jaw
Every shield socket is the prism's negative: the hole is the shield solid grown by a clearance and subtracted, the cradle
under it a key block with the same solid subtracted (its walls glowing), so it holds the prism without touching.

MENACE (round 4). Torn armour (the groove and leading edges ripped in steps, five plates torn open over exposed girders,
their edges glowing hot), hanging cables, barbs along the rim, a serrated keel, faceted octagonal jets and gimbals and an
octagonal reactor cage, forward PINCERS (the ears) flanking the bridge with teeth along their inner edges, and a face:
two slit eyes (the cockpit's glass), each a wedge starting as a point by the nose and opening outboard, under armoured
brows that jut over them and meet in a V; a crown of raked spikes; the hangar's glowing maw and fangs below. Domain colour is kept to thin neon
seams; the rim's windows are sparse red slits. The whimsy: blue eyes peering out of the cavity's dark, and the crystal.

THE LEVEL. The shell floats MANTLE above the core: the MANTLE CAVITY between them is a pillared hall you fly into
through the stern maw or the open front flanks, past the lift pylons, the gills' rails, the brood's cups and the
stellations' X-cradles. Through the body: the HANGAR under the bridge (clam-jaw doors, three launch pads for the three
escorts, arched frames), two curving DUCTS from the flanks to the stern, the PROMENADE recessed round the core's rim, the
belly's radial ribs, the parapets along the leading edges.

ENGINES THAT MANOEUVRE. Each jet pivots RING_S behind its intake, inside an octagonal ring with clearance on every side;
the takes vector the thrust the way the manoeuvre needs (fleet convention: pitchup = nose rises, yawright = nose to the
pilot's right, rollright = the right side drops): pitchup / pitchdown tilt every nozzle, yawleft / yawright swing every
ring, rollleft / rollright tilt the sides opposite ways, speedup draws the splayed jets in, speeddown fans them out.
`pose_clearance.py --parts "engine.*,payload,hangar.door.*" --takes all` proves nothing touches in any take.

A HOVERING ASSEMBLY: `assembly_report.py ArkReverse.fbx --floating shell.0,...,shell.7,umbo,auricle.L,auricle.R,
omni.ring,omni,payload,engine.0,...,engine.7,engine.0.ring,...,engine.7.ring,keel,hangar.door.T,hangar.door.B`.

Contract (FLEET_CONVENTION sections 1-4 for a NEW body):
  file      Assets/_Models/Vessel Models/ArkReverse.fbx (guid 5c1e8a3f7d2b4e9a9f6b2c4d8e1a7f30), preset "zup"
  frame     Blender: nose -Y, up +Z, pilot's left +X. Unity (x, y, z) = Blender (-x, z, -y): NOSE = Unity +Z.
            The hinge sits at Blender (0, HY, 0), near the nose; the fan opens toward +Y (aft).
  nodes     mesh `mesh` (identity, metres), armature `rig`; root bone `hull`
  slots     0 Domain, 1 Body, 2 Window, 3 Engine
  sockets   `socket.<tier>.<nn>` (head at the prism's centre, tail along it, Z = its up; parent `hull`, or the mandible
            for a pincer's stellation); `hangar.slot.0..2`, where the three escorts sit (head on the pad, +3 up, tail
            toward the door)
  shapes    Charge: the lance lengthens aft and the omni crystal swells; Mass: the keel deepens and the jets fatten;
            Space: the pincers' tips reach and the crown spikes rise; Time: the jets stretch. None moves a plate, a
            cradle, a socket bone or a hole.
  takes     rest (first); pitchup, pitchdown, yawleft, yawright, rollleft, rollright, speedup, speeddown (the jets
            manoeuvring); release (the lance slides aft out of its cradles); hangaropen (the clam-jaw doors open).
            Single-frame poses, no blend-shape curves.
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
SEAM1 = 0.5 * (TILES[0][1] + TILES[1][0])          # the growth seams: the stellations' firewall lines, staggered
SEAM2 = 0.5 * (TILES[1][1] + TILES[2][0])          # groove by groove so they never read as a row
SHINGLE = 0.7
Z0, HDOME = 6.0, 22.0
CROWN, RIBLET = 1.6, 0.55
PLATE_T = 1.8
MANTLE = 14.0             # the flyable cavity between the plates' undersides and the core
CORE_DROP = PLATE_T + MANTLE
RC = R + 2.0              # the core's radius in the fan: the jets hang off its trailing rim
FLANK = 6.0               # degrees of core margin beyond the outer plates: the leading-edge parapets
PARAPET_H = 11.3          # the parapets rise to 4.5 under the plate level, closing the cavity's flanks aft of rho 80
CHIN = 9.0                # the prow's belly drops this much more: room for the hangar and its lower jaw

UMBO_R = 34.0
UMBO_LIFT = 11.0
RIDGE = 1.1               # the shell's corrugations, climbing onto the bridge dome
RIDGE_PITCH = 360.0 / 28.0

# prisms: slabs are (across, up, along); shields are cubic prisms of half-extent a, so their shields are regular
SLAB = (3.6, 1.6, 9.0)
GILLS = (56.0, 121.0, 4.6)  # the slabs stand in the grooves as raked gill fins: first, last, pitch (rho)
GILL_CLEAR = 10.5         # no gill within this of a stellation (its spikes reach 9.33 along the groove)
GILL_DEPTH = 4.8          # the gills' rails run this far under the rib edges
GILL_RAKE = 0.27          # the gills lean aft
OCTA_A = 1.6
STELLA_A = 2.2
OCTA_S, STELLA_S = 3.0 * OCTA_A, 3.0 * STELLA_A    # the game's CIRCUMSCRIBING_SCALE
BROOD_ROWS = (88.0, 112.0)                         # the octahedra in the cavity, under each plate's centreline
SOCKET_CLEAR = 1.3        # every socket's clearance round its prism (the assembly gate's 0.4 % of the diagonal)
FANG_RHO_UP, FANG_RHO_LOW = 125.0, 127.0
BLADE_ROWS = (86.0, 100.0, 114.0, 126.0)
RIPS = ((1, 90.0, 3.4, 3), (2, 90.0, 3.0, 5), (6, 74.0, 2.3, 7), (4, 114.0, 4.2, 11), (5, 118.0, 3.8, 13))
# torn plates: (plate, rho, radius, seed), each clear of the talons and over a brood octahedron where one hangs

PAY_RHO0, PAY_RHO1 = 114.0, 156.0
PAY_R = 1.8
LANCE_GAP = 1.4

ENG_RHO0 = R + 4.0
ENG_R = 7.5
ENG_Z = 12.0              # above the plates' lip, so the stern maw under the jets stays open
RING_S = 6.0              # the gimbal pivot, this far behind each intake (ahead of the nacelle's band)
RING_CLEAR = 2.3
YAW, PITCH = 14.0, 12.0
REACTOR_RHO = 43.0

HANGAR_HW = 18.0          # the hangar mouth: half width, floor and roof, depth back to y
HANGAR_Z0, HANGAR_Z1 = -25.0, -11.2
HANGAR_BACK = HY + 8.0
DOOR_GAP = 1.6
MANDIBLE = (44.0, 70.0)   # the mandibles' root and tip x: forward pincers either side of the bridge


def _ang(dx, dy):
    """Fan angle (degrees) of a point dx across, dy aft of the hinge: 0 straight aft, +90 to +X."""
    return math.degrees(math.atan2(dx, dy))


def _smooth(a, b, x):
    t = min(max((x - a) / (b - a), 0.0), 1.0)
    return t * t * (3 - 2 * t)


def _hash(*v):
    """A repeatable pseudo-random number in [0, 1) from integers: the torn shapes never change between builds."""
    h = 0
    for x in v:
        h = (h * 1103515245 + 12345 + int(x) * 2654435761) & 0x7FFFFFFF
    return (h % 10007) / 10007.0


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


def polar(x, y):
    rho = math.hypot(x, y - HY)
    return rho, (_ang(x, y - HY) if rho > 1e-6 else 0.0)


PHI = [-THETA + k * (2 * THETA / NPLATE) for k in range(NPLATE + 1)]
GROOVES = PHI[1:-1]
MIDS = [0.5 * (a + b) for a, b in zip(PHI, PHI[1:])]


def tile_lift(rho):
    for a, b in TILES:
        if a - 0.31 <= rho <= b + 0.31:
            return SHINGLE * min(max((rho - a) / (b - a), 0.0), 1.0)
    return 0.0


def tear(rho, k, side):
    """How far a plate's edge is torn back at rho: stepped, irregular, repeatable; 0 .. ~1.6. Only ever widens a
    groove, so every socket's clearance holds."""
    v = 1.3 * math.sin(rho * 0.41 + k * 1.7 + side * 0.9) + 0.8 * math.sin(rho * 1.13 + k * 2.9 - side * 1.4)
    return round(max(0.0, v - 0.7) * 2.0) / 2.0 * 0.8


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
    """The mantle cavity's floor, MANTLE under the plates' undersides; outside the plates, aft of rho 80, it rises
    into the leading-edge parapets."""
    rho, phi = polar(x, y)
    base = max(dome(x, y), Z0 - 0.6) - CORE_DROP
    return base + PARAPET_H * _smooth(THETA - 0.5, THETA + 1.5, abs(phi)) * _smooth(74.0, 84.0, rho)


def core_bot(x, y):
    """The belly: the lower valve, with radial ribs on the shell's pitch, deeper at the prow (the hangar's chin)."""
    rho, phi = polar(x, y)
    q = min(rho / core_rmax(phi), 1.0)
    ribs = 0.9 * math.cos(math.radians(phi) * 360.0 / 12.5) * _smooth(0.35, 0.85, q)
    return -24.0 - 8.0 * (1 - q * q) ** 0.8 - CHIN * _smooth(100.0, 150.0, abs(phi)) + ribs


def umbo_rmax(phi):
    return UMBO_R


def umbo_top(x, y):
    """The bridge dome: smooth at its crown, the shell's corrugations climbing its flanks and back (never its face)."""
    rho, phi = polar(x, y)
    q = min(rho / UMBO_R, 1.0)
    from_front = 180.0 - abs(phi)
    ridge = RIDGE * (0.5 + 0.5 * math.cos(math.radians(phi) * 360.0 / RIDGE_PITCH)) ** 2 * _smooth(0.45, 0.9, q) * _smooth(56.0, 66.0, from_front)
    return dome(x, y) + 1.2 + UMBO_LIFT * (1 - q ** 5) + ridge


def eye_q(ff):
    """A slit eye's lower and upper edges (fractions of the dome's radius) at an angle `ff` from straight ahead: a
    wedge that starts as a point low by the nose and opens wider and higher outboard - slanted, the scowl."""
    t = min(max((ff - 5.0) / 33.0, 0.0), 1.0)
    return 0.86 - 0.05 * t, 0.85 - 0.14 * t ** 0.8


def eng_len():
    """The jets' length, so the centre jets' mouths end NOSE_TO_JETS behind the nose."""
    c = math.cos(math.radians(min(abs(m) for m in MIDS)))
    return (NOSE_TO_JETS - NOSE_R) / c - ENG_RHO0


def jets_y():
    """The aftmost jet mouth (the two centre jets)."""
    return HY + (ENG_RHO0 + eng_len()) * math.cos(math.radians(min(abs(m) for m in MIDS)))


def arc_y(x, r):
    """The y of the prow's arc (radius r about the hinge) at x."""
    return HY - math.sqrt(max(r * r - x * x, 0.0))


def mandible(a):
    """The mandible's section at a (0 root .. 1 tip): x, leading y, trailing y, top z, thickness. A heavy pincer swept
    forward to a point no further forward than the nose."""
    x = MANDIBLE[0] + (MANDIBLE[1] - MANDIBLE[0]) * a
    return x, HY - 12.0 - 26.0 * a ** 1.2, HY + 12.0 - 46.0 * a ** 0.85, 13.0 - 3.0 * a, 8.0 - 5.0 * a


def build(ctx):
    from mathutils import Vector
    from _ark_kit import Kit
    f, rig, morphs, takes = ctx.forms, ctx.rig, ctx.morphs, ctx.takes
    kit = Kit(ctx, SLOTS)
    piece, bone_part, loft_bm, inset, finish_bm = kit.piece, kit.bone_part, kit.loft_bm, kit.inset, kit.finish_bm

    def polar_bm(phis, ts, rmax, ztop, zbot, wall, slot=H):
        return kit.polar_bm(phis, ts, rmax, ztop, zbot, wall, at, (0.0, HY), slot)

    def surf_normal(x, y, fn=dome):
        e = 0.25
        gx = (fn(x + e, y) - fn(x - e, y)) / (2 * e)
        gy = (fn(x, y + e) - fn(x, y - e)) / (2 * e)
        return Vector((-gx, -gy, 1.0)).normalized()

    def rotated_frame(centre, along, up, deg):
        """(across, along, up) of a socket whose prism is turned `deg` about its up from `along`."""
        M, ac, al, upn = kit.frame_matrix(centre, along, up)
        r = math.radians(deg)
        al2 = (al * math.cos(r) + ac * math.sin(r)).normalized()
        return al2.cross(upn).normalized(), al2, upn

    def spike(name, base, direction, length, r, slot, pid, segments=6):
        d = Vector(direction).normalized()
        sp = f.lathe(name, [(0.0, -0.3), (r, -0.3), (r * 0.8, length * 0.35), (0.0, length)], segments=segments,
                     axis=tuple(d), origin=tuple(base))
        f.set_smooth(sp, False)
        return piece(sp, slot, pid)

    # ------------------------------------------------------------------ socket placements first (the holes need them)
    stellas = []        # (name, centre, frame, parent, where)
    count = {"supershielded": 0, "shielded": 0, "prism": 0, "danger": 0}

    def sname(tier):
        n = "socket.%s.%02d" % (tier, count[tier])
        count[tier] += 1
        return n
    groove_stella = {}
    seam_of = {g: (SEAM2 if i % 2 == 0 else SEAM1) for i, g in enumerate(GROOVES)}
    for g in GROOVES:
        x, y = at(g, seam_of[g])
        up = surf_normal(x, y)
        c = Vector((x, y, dome(x, y) - 2.6))           # sunk: only its upper spikes rise out of the socket
        d = fan_dir(g)
        fr = rotated_frame(c, Vector((d[0], d[1], 0.0)), up, 45.0)
        st = (sname("supershielded"), c, fr, "hull", "groove")
        stellas.append(st)
        groove_stella[g] = st

    # ================================================================== SHELL PLATES (shell.0 .. shell.7): torn armour
    NS, NB, NRT = 28, 4, 12
    for k in range(NPLATE):
        pa, pb = PHI[k], PHI[k + 1]
        da, db, na, nb = fan_dir(pa), fan_dir(pb), fan_perp(pa), fan_perp(pb)

        def edge_off(rho, side):
            if side < 0:                               # the plate's pa edge
                return (GROOVE / 2 + tear(rho, k, -1)) if k > 0 else 1.2 * tear(rho, k, -1)
            return (GROOVE / 2 + tear(rho, k, 1)) if k < NPLATE - 1 else 1.2 * tear(rho, k, 1)
        tiles = []
        for ti, (r0, r1) in enumerate(TILES):
            rings = []
            for t in range(NRT + 1):
                rho = r0 + (r1 - r0) * t / NRT
                oa, ob = edge_off(rho, -1), edge_off(rho, 1)
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
            # thin neon seams at the shingle steps; the trailing lip's edge glows
            for fc in caps[1]:
                fc.material_index = J if ti == 2 else D
            r_lo, r_hi = (2, NRT - 3) if ti < 2 else (2, NRT - 4)
            inset(bm, [grid[t][s] for t in range(r_lo, r_hi) for s in range(3, NS // 2 - 2)], 0.35, 0.3)
            inset(bm, [grid[t][s] for t in range(r_lo, r_hi) for s in range(NS // 2 + 2, NS - 3)], 0.35, 0.3)
            tiles.append(piece(finish_bm("tile", bm), None, 10 + k, bevel=0.12, angle=35))
        plate = f.join(tiles, "plate", SLOTS)
        # the holes: the stellations in the grooves either side; the rips torn in this plate (edges glowing hot)
        cutters = []
        for g in (pa, pb):
            if g in groove_stella:
                _, c, fr, _, _ = groove_stella[g]
                cutters.append(kit.shield_cutter("stella", c, fr, STELLA_S, SOCKET_CLEAR))
        girders = []
        for (rk, rrho, rrad, seed) in RIPS:
            if rk != k:
                continue
            x, y = at(MIDS[k], rrho)
            up = surf_normal(x, y)
            cen = Vector((x, y, dome(x, y) + tile_lift(rrho)))
            radial = Vector((*fan_dir(MIDS[k]), 0.0))
            outline = []
            for i in range(14):
                a = 2 * math.pi * (i + 0.3 * _hash(seed, i)) / 14
                r = rrad * ((1.0 if i % 2 == 0 else 0.55) * (0.75 + 0.35 * _hash(seed, i, 7)))
                outline.append((r * math.cos(a), r * math.sin(a)))
            rip = f.plate("rip", outline, 10.0, normal=tuple(up), origin=tuple(cen), right=tuple(radial))
            f.ensure_slots(rip, SLOTS)
            f.paint(rip, J)
            cutters.append(rip)
            across = up.cross(radial).normalized()
            for o_ in (-0.3, 0.35):                    # the exposed ribs: girders left spanning the rip
                gb = f.box("girder", (0.7, 2.4 * rrad, 0.8), center=(0, 0, 0))
                M, ac, al, upn = kit.frame_matrix(cen + radial * (o_ * rrad) - up * 0.9, across, up)
                f.transform_mesh(gb, M)
                girders.append(piece(gb, H, 10 + k))
            for o_ in (-0.55, 0.6):                    # torn cables hanging from the girders
                top_ = cen + radial * (-0.3 * rrad) + across * (o_ * rrad) - up * 1.2
                hang = [top_, top_ - up * 1.0 + radial * 0.3, top_ - up * 2.0 + radial * 0.2, top_ - up * 2.6 - radial * 0.2]
                girders.append(piece(f.tube_along("cable", [tuple(p) for p in hang], [0.28, 0.26, 0.2, 0.08], segments=6), H, 10 + k))
        # the talons that clutch the stellations either side (rising from this plate, hooking over the spikes)
        talons = []
        for g, side in ((pa, 1), (pb, -1)):           # this plate lies on the +across side of pa, the -across side of pb
            if g not in groove_stella:
                continue
            _, c, fr, _, _ = groove_stella[g]
            dg = Vector((*fan_dir(g), 0.0))
            pg = Vector((*fan_perp(g), 0.0))
            upg = fr[2]
            for sa in (1, -1):
                def P(a_, c_, z_):
                    return c + dg * a_ + pg * (side * c_) + upg * z_
                root = P(sa * 7.4, 7.4, 0.0)
                root.z = dome(root.x, root.y) + 0.9
                path = [root, P(sa * 6.8, 6.8, 5.6), P(sa * 5.3, 5.3, 9.1), P(sa * 3.5, 3.5, 10.1), P(sa * 2.4, 2.4, 8.7)]
                tal = f.tube_along("talon", [tuple(p) for p in path], [1.25, 1.0, 0.8, 0.55, 0.12], segments=6)
                f.ensure_slots(tal, SLOTS)
                f.paint(tal, H)
                tip = path[-1]
                f.paint(tal, J, lambda c_, n_, i_, tip=tip: (c_ - tip).length < 2.1)
                f.set_smooth(tal, False)
                talons.append(piece(tal, None, 10 + k))
        kit.subtract(plate, cutters)
        bone_part([plate] + girders + talons, "shell.%d" % k)

    # ================================================================== THE BRIDGE DOME: a scowl, a crown of spikes
    UPH = [2.5 * j for j in range(144)]                # 0 = aft; straight ahead is 180
    UTS = [0.10, 0.20, 0.30, 0.40, 0.50, 0.58, 0.64, 0.68, 0.72, 0.76, 0.80, 0.84, 0.88, 0.93, 0.97, 0.995]

    def umbo_bot(x, y):
        return dome(x, y) - 2.6
    ubm, ub = polar_bm(UPH, UTS, umbo_rmax, umbo_top, umbo_bot, (0.5,))
    band = {t: i for i, t in enumerate(UTS[:-1])}

    def from_front_of(j):
        return abs(UPH[j] + 1.25 - 180.0)
    for j in range(len(UPH)):
        ub[band[0.97]][j].material_index = D           # a thin neon rim
    for s0 in range(12):
        lo = 30.0 * s0
        if lo + 30.0 > 130.0 and lo < 230.0:
            continue
        cs = [j for j, p in enumerate(UPH) if lo + 2.5 <= p + 1.25 <= lo + 27.5]
        inset(ubm, [ub[band[t]][j] for t in (0.20, 0.30, 0.40) for j in cs], 0.35, 0.3)
    for j in range(len(UPH)):                          # sparse red slits round the flanks
        ff = from_front_of(j)
        if 60.0 <= ff <= 150.0 and j % 4 == 0:
            inset(ubm, [ub[band[0.93]][j]], 0.35, 0.4, J)
    umbo = piece(finish_bm("umbo", ubm), None, 20, bevel=0.1, angle=35)
    crown = [umbo]

    def dome_pt(ff, q, side, lift):
        x, y = at(side * (180.0 - ff), q * UMBO_R)
        return Vector((x, y, umbo_top(x, y))) + surf_normal(x, y, umbo_top) * lift
    # the face: two slit eyes (the cockpit's glass), each a wedge set into the dome, and over each an armoured brow
    # that juts out over the eye's top edge; the brows meet in a V over the nose
    for side in (1, -1):
        rings = []
        for i in range(11):
            ff = 5.0 + 33.0 * i / 10
            qb, qt = eye_q(ff)
            rings.append([dome_pt(ff, qt, side, 0.25), dome_pt(ff, qb, side, 0.25), dome_pt(ff, qb, side, -0.8), dome_pt(ff, qt, side, -0.8)])
        bm, _, _ = loft_bm(rings, slot=W)
        eye = piece(finish_bm("eye", bm), None, 22)
        f.set_smooth(eye, False)
        rings = []
        for i in range(13):
            ff = 0.8 + 43.0 * i / 12
            qb, qt = eye_q(ff)
            ql = qt + 0.012
            qu = ql - 0.09
            rings.append([dome_pt(ff, qu, side, -0.4), dome_pt(ff, qu, side, 0.7), dome_pt(ff, ql, side, 1.9), dome_pt(ff, ql, side, 1.1)])
        bm, _, _ = loft_bm(rings)
        brow = piece(finish_bm("brow", bm), H, 22, bevel=0.06, angle=35)
        f.set_smooth(brow, False)
        crown += [eye, brow]
    peak = Vector((0.0, HY + 3.0, umbo_top(0.0, HY + 3.0)))
    for (dx, dy, ln_, r_) in ((0.0, 3.0, 15.0, 1.5), (5.5, 1.0, 10.5, 1.2), (-5.5, 1.0, 10.5, 1.2), (10.0, -3.5, 7.0, 0.9),
                              (-10.0, -3.5, 7.0, 0.9), (3.0, 9.0, 6.0, 0.8), (-3.0, 9.0, 6.0, 0.8)):
        x, y = dx, HY + dy
        base_ = Vector((x, y, umbo_top(x, y) - 0.4))
        dirn = Vector((dx * 0.03, 0.42, 1.0))         # raked back
        crown.append(spike("spike", base_, dirn, ln_, r_, H, 21))
    tall = peak + Vector((0.0, 0.42, 1.0)).normalized() * 15.0 + Vector((0, 0, -0.3))
    crown.append(piece(f.sphere("beacon", 0.8, center=tuple(tall), segments=10, rings=6), J, 21))
    bone_part(crown, "umbo")

    # ================================================================== MANDIBLES (auricle.L / auricle.R)
    ear_info = {}
    for side_, sx in (("L", 1), ("R", -1)):
        NX, NY = 16, 20
        rings = []
        for i in range(NX + 1):
            a = i / NX
            ax, yf, yb, zt, th = mandible(a)
            yb = max(yb, yf + 1.2)
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
            row[0].material_index = D                    # a neon leading edge
        inset(bm, [grid[i][j] for i in range(9, NX - 3) for j in range(4, NY - 3)], 0.4, 0.3)
        inset(bm, [grid[i][n - 1] for i in range(1, 9)], 0.5, 2.2, W)          # the launch slot, low in the jaw
        wing = piece(finish_bm("mandible", bm), None, 30 + (sx > 0), bevel=0.1, angle=35)
        f.set_smooth(wing, False)
        a = 0.25
        ax, yf, yb, zt, th = mandible(a)
        c = Vector((sx * ax, 0.5 * (yf + yb), zt - th / 2 + 0.45))
        fr = rotated_frame(c, Vector((0, 1, 0)), Vector((0, 0, 1)), 45.0)
        kit.subtract(wing, [kit.shield_cutter("stella", c, fr, STELLA_S, SOCKET_CLEAR)])
        stellas.append((sname("supershielded"), c, fr, "auricle." + side_, "mandible"))
        a_t = 0.12
        tx, tyf, tyb, tzt, tth = mandible(a_t)
        thr = f.lathe("thruster", [(0.0, -0.4), (1.6, -0.4), (2.0, 1.2), (2.3, 3.2), (1.8, 3.4), (1.2, 2.6), (0.0, 2.8)], segments=8,
                      axis=(0, 1, 0), origin=(sx * tx, tyb - 0.4, tzt - tth * 0.5))
        f.ensure_slots(thr, SLOTS)
        f.paint(thr, H)
        f.paint(thr, J, lambda c_, n_, i, o=Vector((sx * tx, tyb - 0.4, tzt - tth * 0.5)): c_.y > o.y + 2.0 and math.hypot(c_.x - o.x, c_.z - o.z) < 1.4)
        f.set_smooth(thr, False)
        barbs = []
        for a_b, ln_b in ((0.55, 5.0), (0.7, 4.0)):
            bx, byf, byb, bzt, bth = mandible(a_b)
            barbs.append(spike("barb", Vector((sx * bx, 0.5 * (byf + byb), bzt + 0.6)), (sx * 0.3, -0.5, 1.0), ln_b, 0.8, H, 33))
        for a_b in (0.35, 0.5, 0.65, 0.8):              # the pincer's teeth, along its inner (trailing) edge
            bx, byf, byb, bzt, bth = mandible(a_b)
            barbs.append(spike("tooth", Vector((sx * bx, byb - 0.4, bzt - bth * 0.5)), (-sx * 0.75, 0.6, -0.1), 4.2 - 2.0 * a_b, 0.9, H, 33, segments=4))
        bone_part([wing, piece(thr, None, 32)] + barbs, "auricle." + side_)
        ear_info[sx] = (Vector((sx * MANDIBLE[0], HY, 10.0)), Vector((sx * mandible(0.55)[0], 0, 0)))

    # ================================================================== CORE (hull): cavity floor, parapets, belly
    CPH = sorted(set([-180.0 + 2.5 * j for j in range(144)] +
                     [s * (THETA - 1.0 + 0.5 * j) for j in range(1, 7) for s in (1, -1)] +
                     [s * (THETA + FLANK + 1.0 * j) for j in range(1, 16) for s in (1, -1)]))
    CTS = [0.10, 0.22, 0.34, 0.46, 0.54, 0.58, 0.62, 0.66, 0.72, 0.78, 0.84, 0.89, 0.94, 0.975, 0.992]
    CWALL = (0.06, 0.30, 0.55, 0.80, 0.94)
    cbm, cb = polar_bm(CPH, CTS, core_rmax, core_top, core_bot, CWALL)
    nt = len(CTS)
    W_PORT, W_LOW = nt + 1, nt + 2
    nb_first = nt + len(CWALL) + 1

    def ccentre(j):
        return 0.5 * (CPH[j] + (CPH[j + 1] if j + 1 < len(CPH) else 180.0))
    for j in range(len(CPH)):
        cb[nt - 1][j].material_index = D               # the rim's edge: a thin neon line
    # the PROMENADE: half the rim wall recessed into a gallery channel all round (not across the hangar's prow), lit by
    # sparse red slits
    prom_cols = [j for j in range(len(CPH)) if abs(ccentre(j)) < 150.0]
    inner = inset(cbm, [cb[k][j] for k in (W_PORT, W_LOW) for j in prom_cols], 0.6, 2.4)
    for i, j in enumerate(prom_cols):
        if j % 4 == 0 and abs(ccentre(j)) < 145.0:
            inset(cbm, [inner[i]], 0.7, 0.35, J)
    for s0 in range(6):
        lo, hi = -60.0 + 20.0 * s0, -60.0 + 20.0 * (s0 + 1)
        cs = [j for j, p in enumerate(CPH) if lo + 1.0 < p + 0.6 < hi - 1.6]
        inset(cbm, [cb[k][j] for k in range(nb_first + 2, nb_first + 6) for j in cs], 0.5, 0.45)
    core = piece(finish_bm("core", cbm), None, 1, bevel=0.1, angle=35)
    hull_parts = []

    # the HANGAR: a bay cut into the prow under the bridge
    bay = f.box("bay", (2 * HANGAR_HW, (HANGAR_BACK - (HY - NOSE_R - 12.0)), HANGAR_Z1 - HANGAR_Z0),
                center=(0.0, 0.5 * (HANGAR_BACK + HY - NOSE_R - 12.0), 0.5 * (HANGAR_Z0 + HANGAR_Z1)))
    f.ensure_slots(bay, SLOTS)
    f.paint(bay, H)
    f.paint(bay, J, lambda c_, n_, i: n_.y > 0.9)           # the cutter's aft face becomes the bay's back wall: it glows
    # the DUCTS: two curving passages from the flank walls to the stern, under the cavity floor
    ducts = []
    for sx in (1, -1):
        ctrl = [(68.0, 78.0), (62.0, 72.0), (52.0, 80.0), (40.0, 96.0), (30.0, 112.0), (24.0, 138.0)]
        pts = [Vector((*at(sx * p, r), -17.0)) for p, r in ctrl]
        path = []
        for i in range(len(pts) - 1):
            p0, p1, p2, p3 = pts[max(i - 1, 0)], pts[i], pts[i + 1], pts[min(i + 2, len(pts) - 1)]
            for s in range(8):
                t = s / 8.0
                t2, t3 = t * t, t * t * t
                path.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
        path.append(pts[-1])
        ducts.append(path)
    # the cheek stellations beside the hangar's jaw, set into the prow's wall (deep cavities)
    for sx in (1, -1):
        x = sx * 33.0
        y = arc_y(x, NOSE_R)
        nrm = Vector((x, y - HY, 0.0)).normalized()
        c = Vector((x, y, -17.5)) + nrm * 0.4
        tang = Vector((0, 0, 1)).cross(nrm).normalized()
        fr = (tang.cross(nrm).normalized(), tang, nrm)              # across (vertical), along (the wall's tangent), up (out)
        stellas.append((sname("supershielded"), c, fr, "hull", "cheek"))
    cutters = [bay] + [f.tube_along("duct", [tuple(p) for p in d], [5.5] * len(d), segments=16) for d in ducts]
    for c_ in cutters[1:]:
        f.ensure_slots(c_, SLOTS)
        f.paint(c_, H)
    cutters += [kit.shield_cutter("stella", c, fr, STELLA_S, 2.6) for (_, c, fr, par, where) in stellas if where == "cheek"]
    kit.subtract(core, cutters)
    hull_parts.append(core)
    # duct frames: octagonal rings round each passage, a red light on each
    for d in ducts:
        for i in range(4, len(d) - 3, 4):
            tgt = (d[i + 1] - d[i - 1]).normalized()
            u = tgt.cross(Vector((0, 0, 1))).normalized()
            v = u.cross(tgt).normalized()
            ring = [d[i] + (u * math.cos(2 * math.pi * a / 8) + v * math.sin(2 * math.pi * a / 8)) * 5.6 for a in range(9)]
            hull_parts.append(piece(f.tube_along("dframe", [tuple(p) for p in ring], [0.55] * 9, segments=4, cap=False), H, 5))
            hull_parts.append(piece(f.sphere("dlight", 0.5, center=tuple(d[i] + v * 5.0), segments=8, rings=6), J, 5))
    # hangar: a glowing maw frame, arches, floor lights, the back bulkhead's frame
    frame = []
    for z in (HANGAR_Z1 + 0.75, HANGAR_Z0 - 0.75):
        pts = [Vector((x, arc_y(x, NOSE_R + 0.3), z)) for x in [-(HANGAR_HW + 1.4) + (2 * HANGAR_HW + 2.8) * i / 16 for i in range(17)]]
        frame.append(piece(f.tube_along("hframe", [tuple(p) for p in pts], [0.7] * len(pts), segments=6), J, 6))
    for sx in (1, -1):
        x = sx * (HANGAR_HW + 0.7)
        y = arc_y(x, NOSE_R + 0.3)
        frame.append(piece(f.tube_along("hjamb", [(x, y, HANGAR_Z0 - 0.75), (x, y, HANGAR_Z1 + 0.75)], [0.7, 0.7], segments=6), J, 6))
    hull_parts += frame
    for i in range(6):
        y = HANGAR_BACK - 4.0 - 6.4 * i
        arch = [(-HANGAR_HW + 0.3, y, HANGAR_Z0), (-HANGAR_HW + 0.3, y, HANGAR_Z1 - 0.3), (HANGAR_HW - 0.3, y, HANGAR_Z1 - 0.3),
                (HANGAR_HW - 0.3, y, HANGAR_Z0)]
        hull_parts.append(piece(f.tube_along("arch", arch, [0.55] * 4, segments=6), H, 7))
        for sx in (1, -1):
            hull_parts.append(piece(f.box("flight", (0.8, 2.0, 0.2), center=(sx * 7.0, y - 3.2, HANGAR_Z0 + 0.1)), J, 7))
    bh = [(-HANGAR_HW + 2.0, HANGAR_BACK - 0.3, HANGAR_Z0 + 1.5), (-HANGAR_HW + 2.0, HANGAR_BACK - 0.3, HANGAR_Z1 - 1.5),
          (HANGAR_HW - 2.0, HANGAR_BACK - 0.3, HANGAR_Z1 - 1.5), (HANGAR_HW - 2.0, HANGAR_BACK - 0.3, HANGAR_Z0 + 1.5),
          (-HANGAR_HW + 2.0, HANGAR_BACK - 0.3, HANGAR_Z0 + 1.5)]
    hull_parts.append(piece(f.tube_along("bulkhead", bh, [0.6] * 5, segments=6, cap=False), D, 7))
    # lift pylons in the mantle cavity, under each plate's tip, and the cables slung between them
    pylon_feet = []
    for pm in MIDS:
        x, y = at(pm, 66.0)
        top = Vector((x, y, dome(x, y) + tile_lift(66.0) - PLATE_T - SOCKET_CLEAR - 0.8))
        hull_parts += kit.column(top, core_top(x, y) - 0.3, 1.6, 8)
        cap = f.lathe("lcap", [(0.0, 0.0), (2.0, 0.0), (1.4, 0.4), (0.0, 0.4)], segments=8, axis=(0, 0, 1), origin=tuple(top))
        hull_parts.append(piece(cap, J, 8))
        pylon_feet.append(Vector((x, y, core_top(x, y) + 2.6)))
    for a_, b_ in zip(pylon_feet, pylon_feet[1:]):
        pts = [a_.lerp(b_, t / 12.0) - Vector((0, 0, 1.9 * math.sin(math.pi * t / 12.0))) for t in range(13)]
        hull_parts.append(piece(f.tube_along("cable", [tuple(p) for p in pts], [0.32] * 13, segments=6), H, 8))
    # barbs along the rim, raked down and out (not over the ducts' mouths)
    for a_ in range(-48, 49, 8):
        if abs(abs(a_) - 24) < 6:
            continue
        rr_ = core_rmax(float(a_))
        x, y = at(float(a_), rr_ - 0.4)
        zt_, zb_ = core_top(x, y), core_bot(x, y)
        out = Vector((*fan_dir(float(a_)), 0.0))
        hull_parts.append(spike("rimbarb", Vector((x, y, zt_ + 0.62 * (zb_ - zt_))), out - Vector((0, 0, 0.45)), 6.0, 1.3, H, 3, segments=4))
    # sensor domes and belly lights
    for sx in (1, -1):
        cx, cy = sx * 28.0, HY + 70.0
        hull_parts.append(piece(f.sphere("dome", 3.2, center=(cx, cy, core_bot(cx, cy) + 0.9), segments=12, rings=8), W, 2))
    for a in range(-45, 46, 15):
        x, y = at(float(a), 0.955 * core_rmax(float(a)))
        hull_parts.append(piece(f.sphere("blight", 0.75, center=(x, y, core_bot(x, y) - 0.2), segments=8, rings=6), J, 3))
    # the whimsy: pairs of blue eyes peering out of the cavity's dark, each under a slanted brow
    for pm in (-18.75, 0.0, 18.75):
        x, y = at(pm, 58.5)
        base_ = Vector((x, y, core_top(x, y) + 2.2))
        aft = Vector((*fan_dir(pm), 0.0))
        acr = Vector((*fan_perp(pm), 0.0))
        for s_ in (1, -1):
            e = base_ + acr * (1.5 * s_)
            hull_parts.append(piece(f.sphere("eye", 1.0, center=tuple(e), segments=12, rings=8), W, 4))
            brow = f.box("brow", (0.5, 2.0, 0.45), center=(0, 0, 0))
            M, ac, al, upn = kit.frame_matrix(e + Vector((0, 0, 1.25)) + aft * 0.2, acr * s_ + Vector((0, 0, -0.35)), Vector((0, 0, 1)))
            f.transform_mesh(brow, M)
            hull_parts.append(piece(brow, H, 4))
            hull_parts.append(piece(f.box("eyepost", (0.8, 0.8, 1.6), center=tuple(e - Vector((0, 0, 1.6)))), H, 4))

    # ================================================================== SOCKETS
    pid_s = 100
    for (nm, c, fr, par, where) in stellas:
        support = core_top(c.x, c.y) - 0.3 if where == "groove" else None
        kit.shield_socket(nm, "stella", c, fr, STELLA_S, pid_s, support, SOCKET_CLEAR)
        pid_s += 1
    for k, pm in enumerate(MIDS):                      # the brood: octahedra in the cavity, under each plate
        for rho in BROOD_ROWS:
            x, y = at(pm, rho)
            c = Vector((x, y, dome(x, y) + tile_lift(rho) - PLATE_T - SOCKET_CLEAR - OCTA_S - 0.9))
            d = fan_dir(pm)
            fr = rotated_frame(c, Vector((d[0], d[1], 0.0)), Vector((0, 0, 1)), 45.0)
            kit.shield_socket(sname("shielded"), "octa", c, fr, OCTA_S, pid_s, core_top(x, y) - 0.3, SOCKET_CLEAR)
            pid_s += 1

    def rail_top(x, y):
        rho, _ = polar(x, y)
        return dome(x, y) + tile_lift(rho) - GILL_DEPTH
    for g in GROOVES:                                  # the gills: slabs standing in the grooves, raked aft, on rails
        d = fan_dir(g)
        out = Vector((d[0], d[1], 0.0))
        rake = (Vector((0, 0, 1)) + out * GILL_RAKE).normalized()
        rows, rho = [], GILLS[0]
        while rho <= GILLS[1] + 1e-6:
            if abs(rho - seam_of[g]) >= GILL_CLEAR and not (abs(g) < 1e-6 and rho > PAY_RHO0 - 6.0):
                rows.append(rho)
            rho += GILLS[2]
        runs = []
        for r_ in rows:
            if runs and r_ - runs[-1][-1] < GILLS[2] * 1.5:
                runs[-1].append(r_)
            else:
                runs.append([r_])
        for run in runs:
            for r_ in run:
                x, y = at(g, r_)
                c = Vector((x, y, rail_top(x, y))) + rake * (SLAB[2] / 2 + 0.55)
                kit.cradle(sname("prism"), c, rake, out, pid_s, SLAB, core_top, ped_len=0.3)
                pid_s += 1
            r0, r1 = run[0] - 2.6, run[-1] + 2.6
            spine, profs = [], []
            for i in range(9):
                rho = r0 + (r1 - r0) * i / 8
                x, y = at(g, rho)
                spine.append(Vector((x, y, rail_top(x, y) - 0.7)))
                profs.append([(-1.0, -0.7), (1.0, -0.7), (1.0, 0.7), (-1.0, 0.7)])
            rail = f.spine_loft("rail", [tuple(p) for p in spine], profs)
            f.set_smooth(rail, False)
            hull_parts.append(piece(rail, H, 9))
            for rho in sorted({r0 + 1.2, r1 - 1.2}):
                x, y = at(g, rho)
                hull_parts += kit.column(Vector((x, y, rail_top(x, y) - 1.4)), core_top(x, y) - 0.3, 1.0, 9)
    for pm in MIDS:                                    # the stern maw: fangs hanging from the plates' lip
        x, y = at(pm, FANG_RHO_UP)
        out = Vector((*fan_dir(pm), 0.0))
        under = dome(x, y) + tile_lift(FANG_RHO_UP) - PLATE_T
        c = Vector((x, y, under - SOCKET_CLEAR - SLAB[2] / 2 - 0.65))
        kit.cradle(sname("danger"), c, Vector((0, 0, -1)), out, pid_s, SLAB, core_top, ped_len=0.6)
        pid_s += 1
    for g in GROOVES:                                  # ... and rising from the core's rim between them
        if abs(g) < 1e-6:
            continue
        x, y = at(g, FANG_RHO_LOW)
        out = Vector((*fan_dir(g), 0.0))
        c = Vector((x, y, core_top(x, y) + SLAB[2] / 2 + 0.55))
        kit.cradle(sname("danger"), c, Vector((0, 0, 1)), out, pid_s, SLAB, core_top, ped_len=0.6)
        pid_s += 1
    for side in (1, -1):                               # saw-blades raked back along the swept leading edges
        phi = side * (THETA + 3.75)
        outn = Vector((*fan_perp(phi), 0.0)) * side
        for rho in BLADE_ROWS:
            x, y = at(phi, rho)
            aft = Vector((*fan_dir(phi), 0.0))
            rake = (Vector((0, 0, 1)) + aft * 0.4).normalized()
            c = Vector((x, y, core_top(x, y))) + rake * (SLAB[2] / 2 + 0.55)
            kit.cradle(sname("danger"), c, rake, outn, pid_s, SLAB, core_top, ped_len=0.6)
            pid_s += 1
    for sx in (1, -1):                                 # the hangar's jaw: fangs in the sill, fangs at the jambs
        x = sx * 8.5
        y = arc_y(x, NOSE_R)
        nrm = Vector((x, y - HY, 0.0)).normalized()
        tang = Vector((0, 0, 1)).cross(nrm).normalized()
        c = Vector((x, y, HANGAR_Z0 - 4.6)) + nrm * (0.3 + 0.6 + SLAB[1] / 2)
        kit.cradle(sname("danger"), c, tang, nrm, pid_s, SLAB, core_top, ped_len=1.2)
        pid_s += 1
        x = sx * (HANGAR_HW + 4.2)
        y = arc_y(x, NOSE_R)
        nrm = Vector((x, y - HY, 0.0)).normalized()
        c = Vector((x, y, 0.5 * (HANGAR_Z0 + HANGAR_Z1))) + nrm * (0.3 + 0.6 + SLAB[1] / 2)
        kit.cradle(sname("danger"), c, Vector((0, 0, 1)), nrm, pid_s, SLAB, core_top, ped_len=1.2)
        pid_s += 1
    ctx.log("sockets: %s (%d)" % (count, sum(count.values())))

    # the three escorts' launch pads in the hangar
    slots = []
    for i, y in enumerate((HANGAR_BACK - 8.0, HANGAR_BACK - 20.0, HANGAR_BACK - 32.0)):
        pad = f.lathe("pad", [(0.0, 0.0), (4.2, 0.0), (4.2, 0.18), (3.6, 0.3), (0.0, 0.3)], segments=6, axis=(0, 0, 1),
                      origin=(0.0, y, HANGAR_Z0 + 0.02))
        f.ensure_slots(pad, SLOTS)
        f.paint(pad, H)
        f.paint(pad, J, lambda c_, n_, i_: n_.z > 0.9)
        f.set_smooth(pad, False)
        bone_part([piece(pad, None, 9)], "hangar.slot.%d" % i)
        slots.append(Vector((0.0, y, HANGAR_Z0 + 3.0)))

    # the clam-jaw doors in the hangar mouth: FLAT leaves (a curved leaf turning about a straight hinge sweeps its ends
    # into the other leaf), set back where the mouth's walls meet the prow's arc, so the prow's curve frames them
    door_y = arc_y(HANGAR_HW, NOSE_R) + 0.4
    door_z = {"T": (0.5 * (HANGAR_Z0 + HANGAR_Z1) + DOOR_GAP / 2, HANGAR_Z1 - DOOR_GAP),
              "B": (HANGAR_Z0 + DOOR_GAP, 0.5 * (HANGAR_Z0 + HANGAR_Z1) - DOOR_GAP / 2)}
    door_hinge = {}
    for key, (z0, z1) in door_z.items():
        xs = [-(HANGAR_HW - DOOR_GAP) + 2 * (HANGAR_HW - DOOR_GAP) * i / 16 for i in range(17)]
        rings = []
        for x in xs:
            rings.append([Vector((x, door_y, z1)), Vector((x, door_y, z0)), Vector((x, door_y + 1.0, z0)), Vector((x, door_y + 1.0, z1))])
        bm, grid, caps = loft_bm(rings)
        edge = 1 if key == "T" else 3
        for row in grid:
            row[edge].material_index = J               # the jaws' edges glow
        leaf = piece(finish_bm("door", bm), None, 90, bevel=0.08, angle=35)
        f.set_smooth(leaf, False)
        bone_part([leaf], "hangar.door." + key)
        # hinged on the OUTER face's edge: turning about the mid-thickness dipped the lower leaf's outer edge toward the
        # bay floor as it opened (pose_clearance found it 1.1 off the floor)
        door_hinge[key] = Vector((0.0, door_y, z1 if key == "T" else z0))

    # ================================================================== PAYLOAD: the stinger lance in the centre groove
    def gpt(rho):
        x, y = at(0.0, rho)
        return Vector((x, y, dome(x, y) + tile_lift(rho) - PAY_R + 0.7))
    p_rear = gpt(PAY_RHO0)
    pax = (gpt(R - 2.0) - p_rear).normalized()
    plen = PAY_RHO1 - PAY_RHO0
    pay_mid, p_tip, dock_head = kit.lance(p_rear, pax, plen, core_top, PAY_R, LANCE_GAP, fracs=(0.20, 0.30))

    # ================================================================== JETS in octagonal gimbal rings
    ln = eng_len()
    eng = {}
    ring_r = (ENG_R + RING_CLEAR + 0.9) / math.cos(math.radians(22.5))   # the octagon's flats clear the jet
    for k, pm in enumerate(MIDS):
        d = fan_dir(pm)
        o = Vector((ENG_RHO0 * d[0], HY + ENG_RHO0 * d[1], ENG_Z))
        axis = Vector((d[0], d[1], 0.0))
        nm = "engine.%d" % k
        kit.nacelle(nm, o, axis, ln, ENG_R, segments=8)
        piv = o + axis * RING_S
        u = axis.cross(Vector((0, 0, 1))).normalized()
        v = Vector((0, 0, 1))
        ring = [piv + (u * math.cos(2 * math.pi * a / 8) + v * math.sin(2 * math.pi * a / 8)) * ring_r for a in range(9)]
        rparts = [piece(f.tube_along("gring", [tuple(p) for p in ring], [0.9] * 9, segments=4, cap=False), H, 55)]
        for s_ in (1, -1):                              # the trunnion bosses the jet pitches about
            boss = f.lathe("boss", [(0.0, 0.0), (1.5, 0.0), (1.5, 1.6), (1.0, 2.2), (0.0, 2.2)], segments=6,
                           axis=tuple(u * s_), origin=tuple(piv + u * s_ * (ring_r - 0.3)))
            f.set_smooth(boss, False)
            rparts.append(piece(boss, D, 56))
        rparts.append(piece(f.sphere("rlight", 0.55, center=tuple(piv + v * (ring_r + 0.9)), segments=8, rings=6), J, 56))
        bone_part(rparts, nm + ".ring")
        eng[nm] = (o, axis, ln, piv)

    # ================================================================== OMNI REACTOR in the court behind the bridge
    rx, ry = at(0.0, REACTOR_RHO)
    ux, uy = at(0.0, UMBO_R)
    collar_o = Vector((rx, ry, umbo_top(ux, uy) + 1.8))
    OMNI = kit.reactor(collar_o, pylon_r=16.5, pylon_drop=6.0, pylon_len=17.0, ring_segments=8)

    # ================================================================== KEEL: a serrated blade under the core
    krings = []
    KY0, KY1 = HY - 30.0, HY + 112.0
    NK = 72

    def keel_depth(a):
        base_ = 9.0 * math.sin(math.pi * min(1.0, (1 - a) * 1.15)) ** 0.6 + 0.6
        saw = (a * 11.0) % 1.0                         # eleven teeth, raked aft
        return base_ * (0.72 + 0.5 * saw)
    for i in range(NK + 1):
        a = i / NK
        y = KY0 + (KY1 - KY0) * a
        ztop = core_bot(0.0, y) - 1.6
        depth = keel_depth(a)
        hw = 1.3 * (0.4 + 0.6 * math.sin(math.pi * a) ** 0.5)
        krings.append([Vector((hw, y, ztop)), Vector((hw * 0.7, y, ztop - depth * 0.7)), Vector((0.0, y, ztop - depth)),
                       Vector((-hw * 0.7, y, ztop - depth * 0.7)), Vector((-hw, y, ztop))])
    kbm, kg, kc = loft_bm(krings)
    for i, row in enumerate(kg):
        if 20 <= i <= 54:
            row[1].material_index = J if i % 6 == 0 else H
            row[2].material_index = J if i % 6 == 0 else H
    keel = piece(finish_bm("keel", kbm), None, 70, bevel=0.06, angle=35)
    f.set_smooth(keel, False)
    bone_part([keel], "keel")

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
        root, _ = ear_info[sx]
        bones.append(B("auricle." + s, tuple(root), tuple(root + Vector((sx * 38.0, -30.0, 0))), parent="hull"))
    bones.append(B("omni.ring", tuple(collar_o), tuple(OMNI), parent="hull"))
    bones.append(B("omni", tuple(OMNI), tuple(OMNI + Vector((0, 0, 10.0))), parent="omni.ring"))
    bones.append(B("payload.dock", tuple(dock_head), tuple(dock_head + pax * 6.0), parent="hull"))
    bones.append(B("payload", tuple(pay_mid), tuple(p_tip), parent="payload.dock"))
    for nm, (o, axis, ln_, piv) in eng.items():
        bones.append(B(nm + ".ring", tuple(piv), tuple(piv + Vector((0, 0, 4.0))), parent="hull"))
        bones.append(B(nm, tuple(piv), tuple(piv + axis * (ln_ - RING_S)), parent=nm + ".ring"))
    bones.append(B("keel", (0, KY0, core_bot(0, KY0) - 2.0), (0, KY1, core_bot(0, KY1) - 2.0), parent="hull"))
    bones.append(B("hangar.door.T", tuple(door_hinge["T"]), tuple(door_hinge["T"] - Vector((0, 0, 4.0))), parent="hull"))
    bones.append(B("hangar.door.B", tuple(door_hinge["B"]), tuple(door_hinge["B"] + Vector((0, 0, 4.0))), parent="hull"))
    for i, p in enumerate(slots):
        bones.append(B("hangar.slot.%d" % i, tuple(p), tuple(p + Vector((0, -6.0, 0))), parent="hull"))
    mandible_socket = {nm: par for (nm, c, fr, par, where) in stellas if where == "mandible"}
    for b in kit.socket_bone_specs(B):
        if b.name in mandible_socket:
            b.parent = mandible_socket[b.name]
        bones.append(b)
    arm = rig.build_armature(ARMATURE, bones)
    kit.roll_sockets(arm)
    rig.skin_rigid(mesh, arm, default_bone="hull")
    bone_names = [b.name for b in arm.data.bones]

    # ================================================================== MORPHS (3-15 % of the diagonal)
    me = mesh.data
    ms = morphs.MaskSet(mesh)
    m = {n_: ms.mask(n_) for n_ in bone_names}

    def keel_deepen(i, co, n):
        if m["keel"][i] <= 0:
            return Vector((0, 0, 0))
        ztop = core_bot(0.0, co.y) - 1.6
        return Vector((0, 0, min(co.z - ztop, 0.0) * 1.1))

    def fatten(i, co, n):
        for e, (o, axis, ln_, piv) in eng.items():
            if m[e][i] > 0:
                d = co - o
                return (d - axis * d.dot(axis)) * 0.2     # stays inside its gimbal ring
        return Vector((0, 0, 0))

    tip_dir = (Vector((MANDIBLE[1] - MANDIBLE[0], mandible(1.0)[1] - mandible(0.0)[1], 0.0))).normalized()

    def reach(i, co, n):
        for sx, side in ((1, "auricle.L"), (-1, "auricle.R")):
            if m[side][i] > 0:
                if abs(co.x) > 62.0:                   # past the stellation: the pincer's tip reaches
                    return Vector((sx * tip_dir.x, tip_dir.y, 0.0)) * (abs(co.x) - 62.0) * 1.5
                return Vector((0, 0, 0))
        if m["umbo"][i] > 0 and co.z > peak.z + 1.0 and (Vector((co.x, co.y, 0)) - Vector((0, peak.y, 0))).length < 14.0:
            return Vector((0, 0, (co.z - peak.z - 1.0) * 0.55))   # the crown's spikes rise
        return Vector((0, 0, 0))

    shapes = {
        "Charge": [morphs.stretch_along(tuple(pax), tuple(p_rear), 1.35, m["payload"]),
                   morphs.scale_about(1.35, tuple(OMNI), m["omni"])],
        "Mass": [keel_deepen, fatten],
        "Space": [reach],
        "Time": [morphs.stretch_along(tuple(axis), tuple(o), 1.5, m[e]) for e, (o, axis, ln_, piv) in eng.items()],
    }
    for name in ("Charge", "Mass", "Space", "Time"):
        kb, mx, frac, moved = morphs.add_shape(mesh, name, shapes[name])
        ctx.log("shape %s: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))
    if "pid" in me.attributes:
        me.attributes.remove(me.attributes["pid"])

    # ================================================================== TAKES: the jets manoeuvre, the lance, the doors
    # world degrees in the Blender frame: +X turns a point aft of the pivot UP (a nozzle tilts up); +Z swings it toward
    # -X, the pilot's right. Thrust pushes the stern opposite the exhaust, so the nose turns toward where the nozzles
    # point: pitchup tilts every nozzle up, yawright swings them right, rollright lifts the right side's nozzles.
    jets = sorted(eng)
    right = [e for e in jets if eng[e][0].x < 0]       # the pilot's right is Blender -X
    poses = {
        "pitchup": {e: (PITCH, 0, 0) for e in jets},
        "pitchdown": {e: (-PITCH, 0, 0) for e in jets},
        "yawright": {e + ".ring": (0, 0, YAW) for e in jets},
        "yawleft": {e + ".ring": (0, 0, -YAW) for e in jets},
        "rollright": {e: ((PITCH if e in right else -PITCH), 0, 0) for e in jets},
        "rollleft": {e: ((-PITCH if e in right else PITCH), 0, 0) for e in jets},
        "speedup": {e + ".ring": (0, 0, 0.35 * MIDS[int(e.split(".")[1])]) for e in jets},
        "speeddown": {e + ".ring": (0, 0, -0.25 * MIDS[int(e.split(".")[1])]) for e in jets},
        "release": {"payload": {"rot": (0, 0, 0), "loc": tuple(pax * 20.0)}},
        "hangaropen": {"hangar.door.T": (-75.0, 0, 0), "hangar.door.B": (70.0, 0, 0)},
    }
    names = ("pitchup", "pitchdown", "yawleft", "yawright", "rollleft", "rollright", "speedup", "speeddown", "release", "hangaropen")
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
            "sheet_poses": ("pitchup", "yawright", "rollright", "hangaropen"),
            "pose_expect": {
                "pitchup": [("engine.3", "z", +1, 0.6)],
                "pitchdown": [("engine.3", "z", -1, 0.6)],
                "yawright": [("engine.3", "x", -1, 0.6)],
                "yawleft": [("engine.3", "x", +1, 0.6)],
                "rollright": [("engine.0", "z", +1, 0.6), ("engine.7", "z", -1, 0.6)],
                "rollleft": [("engine.0", "z", -1, 0.6), ("engine.7", "z", +1, 0.6)],
                "speedup": [("engine.0", "x", +1, 0.3), ("engine.7", "x", -1, 0.3)],
                "speeddown": [("engine.0", "x", -1, 0.3), ("engine.7", "x", +1, 0.3)],
                "release": [("payload", "y", +1, 10.0)],
                "hangaropen": [("hangar.door.T", "y", -1, 1.0), ("hangar.door.B", "y", -1, 1.0)],
            },
            "shapes": ["Charge", "Mass", "Space", "Time"],
            "slots": list(SLOTS),
            "size_ref_blender": (tuple(lo), tuple(hi)),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.03,
        },
    }
