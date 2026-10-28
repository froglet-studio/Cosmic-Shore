"""
ark_reverse - the Ark's SECOND CONCEPT: the clam valve facing the other way (Garrett, 2026-10-27: "this is one concept.
now lets explore an alternative where the arc is facing the other way. get re-inspired from the arc clam shell. and have
it face the opposite direction."). `ark.py` (Ark.fbx) is the first concept: hinge aft, fan opening forward to a lip at
the nose. Here the HINGE IS THE PROW and the fan opens AFT, so the shell is a swept delta with the bridge at its point.

Round 3 asked for jets with clearance to manoeuvre, the game's own shield shapes as the sockets the hull is designed
around, danger prisms where the Ark wants to be dangerous, a hangar the escorts launch from, a body that plays like a
level. Round 4 asked for menace. Round 5 (same day): "this is still a bubbly pokemon. think hierarchical and
heterarchical. look at the dolphin and the details of its contours. imagine that is two or three hierarchies and a
couple heterarchies. this ark should have 5 hierarchies that have 2 to 5 heterarchies each. it should look sophisticated
like it belongs on the cover of a sci fi novel not like megaknight turned into a toy spaceship." Round 6: "now apply
these 5 levels to more of the components. the engines alone should not read low poly. each should have the feel of
these five levels." So every component repeats the hierarchy inside itself (the jet, its gimbal ring, the lance, the
reactor, the keel fin, the side pods, the sensor masts, the hangar doors): see each builder's docstring, and the
table in Docs/VesselModels/ArkReverse.md.

THE FORM LANGUAGE (round 5). The Dolphin's lesson: its masses are crisp faceted prisms (a chamfered tube in stepped
sections, flat jaw blades, floating swept wings, a thruster cluster), each carries its own secondary forms (collars,
steps, a ventral fin, scalloped notches, knuckles), over those run parallel surface systems (contour traces that turn
in 45-degree jogs and end in rings, chevron groups, dot rows, round cut-outs), and then micro detail. Nothing round for
its own sake, nothing a spike, nothing stacked on a pedestal. The silhouette is LOW and LONG: a scallop is a jet-driven
bivalve, so the Ark is a closed clam whose two valves gape only at the stern, and its jets fire out of that gape. Five
levels of scale, each with two to five parallel systems (heterarchies):

  L1 silhouette   (100-230)  the SHELL FAN of ridged rib blades | the DECK, the lower valve: a cranked-arrow plate whose
                             swept flanks rise as bulwarks under the shell's edges, so the valves meet in a shadow seam |
                             the SPINE, a faceted keel hull from prow to stern | the UMBO, the stepped citadel over the
                             hinge where the ribs converge | the JAWS, two flat blades reaching forward either side of
                             the hangar mouth
  L2 assemblies   (30-80)    rib blades (shoulders, a raised crest, shingled tiles) | the umbo's tiers (boss, citadel,
                             bridge with its visor band) | spine sections stepped by collars | the bulwarks with their
                             walkways | the jet bank in the stern gape, each jet in an octagonal gimbal ring
  L3 components   (8-25)     prism sockets (clamped stellations, the brood of octahedra, gills, the blades set into the
                             leading edges and the jaws, the stern grille between the jets) | the reactor well with the
                             omni crystal | the keel fin and sponsons | the hangar mouth and the curving tunnel | the
                             sensor array
  L4 surface      (1-8)      inset panels | contour traces with jogs | vent grilles | port rows | groove channels
  L5 micro        (0.2-1.5)  trace terminals | running lights | dot clusters

THE PRISMS ARE THE GAME'S OWN SHAPES (OctahedronMeshGenerator, StellatedOctahedronMeshGenerator: 3x the half-extents),
on cubic prisms, so the shields are the REGULAR octahedron and stellated octahedron; every shield socket is the shield's
negative (a boolean hole plus a key cradle cut to it):
  supershielded  stellations sunk where grooves cross the growth seams (alternating seams groove by groove), held by four
                 faceted clamp arms with glowing pads. They are the firewalls between the groove cells
  shielded       octahedra, the brood in the mantle cavity under the plates, on glowing cups
  prism          gills: slabs standing in the grooves, raked aft, on rails
  danger         where the Ark wants to be dangerous, each in a slot cut to it: blades set into the swept leading edges
                 (the edge that rams), into the jaws' inner edges (the mouth), and a grille between the jets in the
                 stern gape

THE LEVEL. The shell floats over the deck: the MANTLE CAVITY is a pillared hall, closed at the sides by the bulwarks and
open at the stern gape (fly in between the jets) and through the grooves. The HANGAR is in the spine's prow (clam-jaw
doors, three escort pads, arched frames) between the jaws, and a TUNNEL runs from its back wall down the spine and
curves out through the keel; the bulwarks' tops are walkways along the leading edges.

ENGINES AND WINGS THAT MANOEUVRE: each jet pivots in its octagonal gimbal ring in the gape; the flight takes vector the
thrust (fleet convention), and the wings move with them: the jaws (the scallop's auricles, its "wings") pitch as canards,
and the outer shell plates lift as spoilers on the inside of a roll or a yaw.
`pose_clearance.py --parts "engine.*,payload,hangar.door.*,shell.*,auricle.*" --takes all` proves nothing touches.

A HOVERING ASSEMBLY: `assembly_report.py ArkReverse.fbx --floating shell.0,...,shell.7,umbo,auricle.L,auricle.R,
omni.ring,omni,payload,engine.0,...,engine.7,engine.0.ring,...,engine.7.ring,keel,hangar.door.T,hangar.door.B`.

Contract (FLEET_CONVENTION sections 1-4 for a NEW body):
  file      Assets/_Models/Vessel Models/ArkReverse.fbx (guid 5c1e8a3f7d2b4e9a9f6b2c4d8e1a7f30), preset "zup"
  frame     Blender: nose -Y, up +Z, pilot's left +X. Unity (x, y, z) = Blender (-x, z, -y): NOSE = Unity +Z.
            The hinge sits at Blender (0, HY, 0); the fan opens toward +Y (aft).
  nodes     mesh `mesh` (identity, metres), armature `rig`; root bone `hull`; `umbo` is the citadel, `auricle.L/R` the
            jaws (the scallop's ears), `keel` the keel fin
  slots     0 Domain, 1 Body, 2 Window, 3 Engine
  sockets   `socket.<tier>.<nn>` (head at the prism's centre, tail along it, Z = its up; parent `hull`, or the jaw for a
            jaw's blade); `hangar.slot.0..2`, where the three escorts sit (head on the pad, +3 up, tail toward the door)
  shapes    Charge: the lance lengthens aft and the omni crystal swells; Mass: the keel deepens and the jets fatten;
            Space: the jaws' knuckles reach and the sensor masts rise; Time: the jets stretch. None moves a plate, a
            cradle, a socket bone or a hole.
  takes     rest (first); pitchup, pitchdown, yawleft, yawright, rollleft, rollright, speedup, speeddown (the jets
            manoeuvring; the jaws pitch with pitch and roll, the outer plates lift as spoilers with roll and yaw);
            release (the lance slides aft out of its cradles); hangaropen (the clam-jaw doors open).
"""

import math
import os
from _ark_kit import SLOTS, D, H, W, J

NAME = "ArkReverse"
OUTPUT = "Assets/_Models/Vessel Models/ArkReverse.fbx"

ARMATURE = "rig"
MESH_NODE = "mesh"

# ---------------------------------------------------------------- layout (Blender frame: nose -Y, up +Z, left +X)
NOSE_TO_JETS = 196.0      # half a standard nucleus radius (392)
NOSE_Y = -98.0            # the jaws' knuckles are the nose; the jets end at NOSE_Y + NOSE_TO_JETS
HY = -44.0                # the hinge
DECK_NOSE = 50.0          # the deck's prow tip, ahead of the hinge
R = 128.0                 # the ribs' outer radius (the trailing lip)
THETA = 50.0              # the fan's half angle, degrees
NPLATE = 8
GROOVE = 8.4
RHO0 = 52.0               # where the ribs begin (inside: the umbo)
TILES = ((RHO0, 77.0), (77.6, 102.6), (103.2, R))
SEAM1 = 0.5 * (TILES[0][1] + TILES[1][0])          # the growth seams: the stellations' firewall lines, staggered
SEAM2 = 0.5 * (TILES[1][1] + TILES[2][0])          # groove by groove so they never read as a row
SHINGLE = 0.7
Z0, HDOME = 4.0, 14.0
PLATE_T = 1.8
# a rib's cross-section: edge chamfer, a flat shoulder, a ramp up to a narrow raised crest (planar facets, no pillow)
U_COLS = (0.0, 0.06, 0.14, 0.22, 0.32, 0.39, 0.46, 0.5, 0.54, 0.61, 0.68, 0.78, 0.86, 0.94, 1.0)
SHOULDER, CREST = 0.6, 2.0
MANTLE = 18.0             # the gape at the lip, where the jets sit
CORE_DROP = PLATE_T + MANTLE
CAVITY_MAX = -2.0         # the cavity floor never rises above this
DECK_T = 4.5              # the deck (lower valve) is a plate, not a lens
RC = R + 2.0
FLANK = 6.0
BULWARK_GAP = 1.8         # the shadow seam between a bulwark's top and the shell's edge above it
BULWARK_RHO = (54.0, 57.0)
CRANK = 6.0               # the leading edge runs from the lip corner (THETA + FLANK) to the shoulder (THETA + FLANK + CRANK)
SHOULDER_RHO = 52.0
PROW = ((40.0, HY - 12.0), (26.0, HY - 36.0), (8.0, HY - DECK_NOSE), (0.0, HY - DECK_NOSE))   # the +X half, aft to the tip
PROW_FALL = 0.08          # the deck's top falls this much per unit ahead of the hinge

# the umbo: a boss, a citadel and a bridge, stepped over the hinge; the reactor well behind the bridge
UMBO_RMAX, UMBO_INSET, UMBO_GAP = 49.0, 4.0, 1.6
UMBO_Z, UMBO_FALL = 20.0, 0.26
TIER_H = (3.5, 7.0)
TIER2_FR, BRIDGE_FR = 0.46, 0.225       # (between the riser rings: the risers slope)
WELL_RHO, WELL_R = 31.0, 14.5

# the spine: (y, half width, bottom z); its top meets the deck. Stepped sections, collars at the steps
SPINE = ((-88.0, 17.0, -24.0), (-86.6, 19.0, -25.6), (-56.0, 19.0, -25.6), (-55.2, 20.6, -26.6), (-53.2, 20.6, -26.6),
         (-52.4, 17.0, -24.4), (20.0, 17.0, -24.4), (20.8, 18.4, -25.4), (22.8, 18.4, -25.4), (23.6, 15.0, -24.0),
         (80.0, 7.0, -22.0))

SLAB = (3.6, 1.6, 9.0)
OCTA_A, STELLA_A = 1.6, 2.2
OCTA_S, STELLA_S = 3.0 * OCTA_A, 3.0 * STELLA_A
GILLS = (56.0, 121.0, 4.6)
GILL_CLEAR = 10.5
GILL_DEPTH = 8.7
GILL_RAKE = 0.27
BROOD_ROWS = (88.0, 112.0)
SOCKET_CLEAR = 1.3
SLOT_CLEAR = (3.85, 6.3, 3.0)   # a danger blade's slot: half across, half along, depth (1.3 clear of its cradle)
EDGE_ROWS = (0.14, 0.36, 0.58, 0.80)     # the leading-edge blades, as fractions from the lip corner to the shoulder
GRILLE_RHO = 125.0

PAY_RHO0, PAY_RHO1 = 100.0, 140.0
PAY_R = 1.8
LANCE_GAP = 1.4

JET_R = 3.6
RING_RHO = 125.0          # the gimbal rings sit in the gape just inside the lip
RING_S = 5.0              # the pivot, behind each jet's intake
ENG_RHO0 = RING_RHO - RING_S
RING_CLEAR = 2.1
YAW, PITCH = 14.0, 12.0

HANGAR_HW = 14.0
HANGAR_Z0, HANGAR_Z1 = -23.0, -12.0
HANGAR_BACK = -44.0
DOOR_GAP = 1.6
JAW_Z, JAW_T = -17.5, 4.5
JAW_IN = 21.2             # the jaws' inner (biting) edge, 2.2 clear of the spine
JAW_SLOTS = (-67.0, -81.0)


def _ang(dx, dy):
    """Fan angle (degrees) of a point dx across, dy aft of the hinge: 0 straight aft, +90 to +X."""
    return math.degrees(math.atan2(dx, dy))


def _smooth(a, b, x):
    t = min(max((x - a) / (b - a), 0.0), 1.0)
    return t * t * (3 - 2 * t)


def dome(x, y):
    """The valve's top surface: highest at the hinge, falling aft to the trailing lip and toward the swept flanks, and
    falling fast ahead of the hinge."""
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


def crest(u):
    """The rib's cross-section height over its edge line at u (0..1 across): planar facets."""
    v = min(u, 1.0 - u)
    if v <= 0.06:
        return SHOULDER * v / 0.06
    if v <= 0.32:
        return SHOULDER
    if v <= 0.46:
        return SHOULDER + (CREST - SHOULDER) * (v - 0.32) / 0.14
    return CREST


def outline_pts():
    """The deck's plan outline ahead of the lip, +X half: the lip corner, the swept leading edge to the shoulder, the
    prow's chamfers to the tip."""
    return [at(THETA + FLANK, RC), at(THETA + FLANK + CRANK, SHOULDER_RHO)] + list(PROW)


def _ray_seg(phi, a, b):
    """Distance from the hinge along fan direction phi to the segment a-b, or None."""
    d = fan_dir(phi)
    ex, ey = b[0] - a[0], b[1] - a[1]
    den = d[0] * ey - d[1] * ex
    if abs(den) < 1e-12:
        return None
    ax, ay = a[0], a[1] - HY
    t = (ax * ey - ay * ex) / den
    s = (ax * d[1] - ay * d[0]) / den
    return t if t > 0 and -1e-9 <= s <= 1 + 1e-9 else None


def deck_r(phi):
    """The deck's outline radius at fan angle phi: the lip arc, then the straight leading edge and the prow."""
    a = abs(((phi + 180.0) % 360.0) - 180.0)
    if a <= THETA + FLANK:
        return RC
    pts = outline_pts()
    for p, q in zip(pts, pts[1:]):
        t = _ray_seg(a, p, q)
        if t is not None:
            return t
    return DECK_NOSE


def floor_z(x, y):
    """The cavity floor, and the deck's top under the umbo and the prow (flat, falling gently toward the tip)."""
    flat = CAVITY_MAX - PROW_FALL * max(0.0, HY - y)
    if y <= HY:
        return flat
    rho, _ = polar(x, y)
    fan = min(dome(x, y) - CORE_DROP, CAVITY_MAX)
    return flat + (fan - flat) * _smooth(50.0, 54.0, rho)   # (flat under the whole umbo, so it floats evenly)


def bulwark(rho):
    """A bulwark's top at rho: a shadow seam under the shell's outer edge."""
    x, y = at(THETA, rho)
    return dome(x, y) - PLATE_T - BULWARK_GAP


def core_top(x, y):
    """What stands on the deck: the cavity floor inside the fan, a bulwark's walkway under and outside the shell's edge
    (aft of the umbo). Supports stand on this; the deck's own top is `floor_z`."""
    rho, phi = polar(x, y)
    fl = floor_z(x, y)
    w = _smooth(THETA - 0.9, THETA - 0.3, abs(phi)) * _smooth(BULWARK_RHO[0], BULWARK_RHO[1], rho)
    return fl + (bulwark(rho) - fl) * w


def deck_bot(x, y):
    return floor_z(x, y) - DECK_T


def umbo_r(phi):
    return min(UMBO_RMAX, deck_r(phi) - UMBO_INSET)


def umbo_top(x, y):
    """The umbo's stepped top: a boss falling toward the prow and the flanks, a citadel tier and the bridge tier."""
    rho, phi = polar(x, y)
    fr = rho / umbo_r(phi)
    base = UMBO_Z - UMBO_FALL * max(0.0, HY - y) - 0.07 * abs(x)
    return base + (TIER_H[1] if fr < BRIDGE_FR else (TIER_H[0] if fr < TIER2_FR else 0.0))


def umbo_bot(x, y):
    return floor_z(x, y) + UMBO_GAP


def eng_len():
    c = math.cos(math.radians(min(abs(m) for m in MIDS)))
    return (NOSE_Y + NOSE_TO_JETS - HY) / c - ENG_RHO0


def jets_y():
    return HY + (ENG_RHO0 + eng_len()) * math.cos(math.radians(min(abs(m) for m in MIDS)))


def spine_at(y):
    """The spine's half width and bottom at y (stations interpolated), and its top: into the deck."""
    for a, b in zip(SPINE, SPINE[1:]):
        if a[0] <= y <= b[0]:
            t = (y - a[0]) / (b[0] - a[0])
            return a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t, deck_bot(0.0, y) + 1.0
    s = SPINE[0] if y < SPINE[0][0] else SPINE[-1]
    return s[1], s[2], deck_bot(0.0, y) + 1.0


def jaw_outline():
    """The +X jaw's plan outline (x, y): a flat blade from its root beside the spine to a chamfered knuckle at the nose,
    a stepped notch in its outer edge (the Dolphin's scalloped jaw, in plan)."""
    return [(JAW_IN, -58.0), (31.0, -58.0), (33.5, -61.0), (33.5, -70.0), (31.7, -72.0), (31.7, -80.0), (32.6, -82.0),
            (30.6, -88.0), (25.4, -95.0), (19.6, NOSE_Y), (16.4, -97.6), (15.6, -95.6), (JAW_IN - 1.0, -90.2),
            (JAW_IN, -89.0)]


def _cols(grid, *forced, gap=1.2):
    """Polar columns: the forced ones (corners, channel edges) and the grid's, minus any grid column within `gap`
    degrees of a forced one (a sliver column is a face an inset turns inside out)."""
    fs = set(round(p, 4) for g in forced for p in g)
    keep = [p for p in grid if all(abs(((p - q + 180.0) % 360.0) - 180.0) >= gap for q in fs)]
    return sorted(set(round(p, 4) for p in keep) | fs)


def build(ctx):
    from mathutils import Vector, Matrix
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
        M, ac, al, upn = kit.frame_matrix(centre, along, up)
        r = math.radians(deg)
        al2 = (al * math.cos(r) + ac * math.sin(r)).normalized()
        return al2.cross(upn).normalized(), al2, upn

    def strip(name, pts, w, h, slot, pid, lift=0.04):
        """A thin raised strip along a polyline on a surface (a contour trace, a seam line, a stripe)."""
        prof = [(-w / 2, -h * 0.5), (w / 2, -h * 0.5), (w / 2, h * 0.5), (-w / 2, h * 0.5)]
        st = f.spine_loft(name, [tuple(p + Vector((0, 0, lift + h * 0.5))) for p in pts], [prof] * len(pts))
        f.set_smooth(st, False)
        return piece(st, slot, pid)

    def disc(name, centre, normal, r, h, slot, pid, segments=8, ring=False):
        prof = [(0.0, 0.0), (r, 0.0), (r, h), (0.0, h)] if not ring else [(r * 0.55, 0.0), (r, 0.0), (r, h), (r * 0.55, h)]
        d = f.lathe(name, prof, segments=segments, axis=tuple(normal), origin=tuple(centre), cap=not ring)
        f.set_smooth(d, False)
        return piece(d, slot, pid)

    def slab_slot(centre, along, up, size=None):
        """The slot a danger blade sits in, cut from the part that holds it: by default its cradle's envelope grown 1.3
        all round (a FLOATING part's slot must clear the cradle by the assembly gap); `size` = (across, along, below,
        above) for a tight slit in the hull."""
        M, ac, al, upn = kit.frame_matrix(centre, along, up)
        if size is None:
            b = f.box("slot", (2 * SLOT_CLEAR[0], 2 * SLOT_CLEAR[1], 2 * SLOT_CLEAR[2]))
        else:
            b = f.box("slot", (size[0], size[1], size[2] + size[3]), center=(0.0, 0.0, 0.5 * (size[3] - size[2])))
        f.transform_mesh(b, M)
        f.ensure_slots(b, SLOTS)
        f.paint(b, H)
        return b

    def rib_pt(k, u, rho, lift=None):
        pa, pb = PHI[k], PHI[k + 1]
        oa = GROOVE / 2 if k > 0 else 0.0
        ob = GROOVE / 2 if k < NPLATE - 1 else 0.0
        da, db, na, nb = fan_dir(pa), fan_dir(pb), fan_perp(pa), fan_perp(pb)
        ea = Vector((rho * da[0] + oa * na[0], HY + rho * da[1] + oa * na[1], 0))
        eb = Vector((rho * db[0] - ob * nb[0], HY + rho * db[1] - ob * nb[1], 0))
        p = ea.lerp(eb, u)
        p.z = dome(p.x, p.y) + (tile_lift(rho) if lift is None else lift) + crest(u)
        return p

    def colmid(phis, j):
        return 0.5 * (phis[j] + (phis[j + 1] if j + 1 < len(phis) else phis[0] + 360.0))

    def wrap(p):
        return ((p + 180.0) % 360.0) - 180.0

    # ------------------------------------------------------------------ the component kit: every part in five levels
    # A component carries the ship's hierarchy in miniature (L1 its silhouette, L2 its assemblies, L3 its components,
    # L4 its surface, L5 its micro detail), so no part reads as a low-poly primitive. Round parts are lathed with enough
    # segments to read round (the join smooths under 40 degrees and keeps every step crisp).
    import bmesh as _bmesh

    def lathe_ob(name, prof, axis, origin, segs, slot=H, cap=True):
        ob = f.lathe(name, prof, segments=segs, axis=tuple(axis), origin=tuple(origin), cap=cap)
        f.ensure_slots(ob, SLOTS)
        f.paint(ob, slot)
        return ob

    def inset_where(ob, pred, thickness, depth, slot=None):
        """Inset every face of `ob` whose (centre, normal) passes `pred`, one by one: panel seams, vent slots."""
        bm = _bmesh.new()
        bm.from_mesh(ob.data)
        bm.normal_update()
        faces = sorted([fc for fc in bm.faces if pred(fc.calc_center_median(), fc.normal)],
                       key=lambda fc: tuple(round(x, 3) for x in fc.calc_center_median()))
        for fc in faces:
            kit.inset(bm, [fc], thickness, depth, slot)
        bm.to_mesh(ob.data)
        bm.free()
        ob.data.update()

    def ring_loft(name, centre, axis, u, v, R, sec, n=8, rot=math.pi / 8):
        """A closed polygonal ring (n flats, a flat at the top) with a mitred cross-section `sec` of (radial, axial)
        offsets. Returns (bm, faces) with faces[k * len(sec) + i] = flat k (centred at 360/n*(k+1) degrees), section
        edge i."""
        bm = _bmesh.new()
        sc = 1.0 / math.cos(math.pi / n)
        loops = []
        for a in range(n):
            ang = 2 * math.pi * a / n + rot
            rd = u * math.cos(ang) + v * math.sin(ang)
            loops.append([bm.verts.new(centre + rd * (R + x * sc) + axis * y) for (x, y) in sec])
        faces = []
        for a in range(n):
            A, Bl = loops[a], loops[(a + 1) % n]
            for i in range(len(sec)):
                j = (i + 1) % len(sec)
                fc = bm.faces.new((A[i], A[j], Bl[j], Bl[i]))
                fc.material_index = H
                faces.append(fc)
        _bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.normal_update()
        return bm, faces
    # a chamfered square section, (radial, axial): edges 0 inner, 1 inner-aft chamfer, 2 aft, 3 outer-aft chamfer,
    # 4 outer, 5 outer-front chamfer, 6 front, 7 inner-front chamfer
    def chamfer_sec(hr, ha, c):
        return [(-hr, -ha + c), (-hr, ha - c), (-hr + c, ha), (hr - c, ha), (hr, ha - c), (hr, -ha + c), (hr - c, -ha), (-hr + c, -ha)]

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
        c = Vector((x, y, dome(x, y) - 2.6))
        d = fan_dir(g)
        fr = rotated_frame(c, Vector((d[0], d[1], 0.0)), up, 45.0)
        st = (sname("supershielded"), c, fr, "hull", "groove")
        stellas.append(st)
        groove_stella[g] = st

    # ================================================================== L1 THE SHELL FAN: ridged rib blades
    NB, NRT = 4, 12
    NS = len(U_COLS) - 1
    for k in range(NPLATE):
        pa, pb = PHI[k], PHI[k + 1]
        left_vents = (k % 2 == 0)
        tiles = []
        for ti, (r0, r1) in enumerate(TILES):
            rings = []
            for t in range(NRT + 1):
                rho = r0 + (r1 - r0) * t / NRT
                lift = SHINGLE * t / NRT
                top = [rib_pt(k, u, rho, lift) for u in U_COLS]
                bot = []
                for s in range(NB + 1):
                    p = rib_pt(k, 1 - s / NB, rho, lift)
                    bot.append(Vector((p.x, p.y, dome(p.x, p.y) + lift - PLATE_T)))
                rings.append(top + bot)
            bm, grid, caps = loft_bm(rings, zipper=(NS + 1, NB + 1))
            for fc in caps[1]:
                fc.material_index = H if ti == 2 else D        # thin neon seams at the steps; the lip's edge is dark
            r_lo, r_hi = 2, NRT - 3
            L_SH, R_SH = (1, 2, 3), (10, 11, 12)               # the shoulders' facets
            # L4 heterarchy per tile: panels | vent grilles | panels with chevrons - and the sides alternate rib by rib
            if ti == 1:
                vent_side, panel_side = (L_SH, R_SH) if left_vents else (R_SH, L_SH)
                for t in range(r_lo, r_hi + 1, 2):
                    inset(bm, [grid[t][vent_side[1]]], 0.14, 0.4)
                inset(bm, [grid[t][s] for t in range(r_lo, r_hi) for s in panel_side], 0.32, 0.25)
            else:
                for side in (L_SH, R_SH):
                    inset(bm, [grid[t][s] for t in range(r_lo, r_hi) for s in side], 0.32, 0.25)
            tile = piece(finish_bm("tile", bm), None, 10 + k, bevel=0.08, angle=30)
            f.set_smooth(tile, False)
            tiles.append(tile)
        # L4 a contour trace down the crest, jogging at 45 degrees onto a shoulder near the lip and ending in a ring
        side_u = 0.24 if left_vents else 0.76
        pts = [rib_pt(k, 0.5, rho) for rho in [56.0 + 2.0 * i for i in range(31)]]
        jog = [rib_pt(k, 0.5 + (side_u - 0.5) * s / 4, 116.0 + 0.9 * s) for s in range(1, 5)]
        tail = [rib_pt(k, side_u, rho) for rho in (120.5, 122.5, 124.0)]
        trace = pts + jog + tail
        tiles.append(strip("trace", trace, 0.45, 0.12, D, 10 + k))
        endp = tail[-1]
        tiles.append(disc("term", endp + Vector((0, 0, 0.02)), Vector((0, 0, 1)), 1.0, 0.16, D, 10 + k, ring=True))
        # L4 chevrons near the lip, on the other shoulder; L5 a dot row near the root
        ch_u = 1 - side_u
        for i in range(4):
            rho = 113.0 + 1.6 * i
            a_, b_ = rib_pt(k, ch_u - 0.06, rho), rib_pt(k, ch_u + 0.06, rho + 1.1)
            tiles.append(strip("chev", [a_, b_], 0.5, 0.1, D, 10 + k))
        for i in range(3):
            p = rib_pt(k, 0.39 if left_vents else 0.61, 60.0 + 2.0 * i)
            tiles.append(disc("dot", p, surf_normal(p.x, p.y), 0.45, 0.12, W, 10 + k, segments=8))
        for u_l in (0.1, 0.9):                                  # L5 running lights at the lip corners
            p = rib_pt(k, u_l, R - 1.2)
            tiles.append(piece(f.sphere("light", 0.5, center=tuple(p + Vector((0, 0, 0.4))), segments=8, rings=6), J, 10 + k))
        plate = f.join(tiles, "plate", SLOTS)
        cutters = []
        for g in (pa, pb):
            if g in groove_stella:
                _, c, fr, _, _ = groove_stella[g]
                cutters.append(kit.shield_cutter("stella", c, fr, STELLA_S, SOCKET_CLEAR))
        kit.subtract(plate, cutters)
        # L3 the clamp arms that hold the stellations either side (faceted brackets, glowing pads)
        clamps = []
        for g, side in ((pa, 1), (pb, -1)):
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
                arm_ = [root, P(sa * 7.0, 7.0, 6.4), P(sa * 4.3, 4.3, 9.0)]
                bar = f.tube_along("clamp", [tuple(p) for p in arm_], [0.75, 0.7, 0.6], segments=4)
                f.set_smooth(bar, False)
                clamps.append(piece(bar, H, 10 + k))
                clamps.append(piece(f.box("pad", (1.4, 1.4, 0.5), center=tuple(arm_[-1] - upg * 0.4)), J, 10 + k))
        bone_part([plate] + clamps, "shell.%d" % k)

    # ================================================================== L1 THE UMBO: boss, citadel, bridge, reactor well
    prow_phis = [polar(*p)[1] for p in outline_pts()[1:]]
    UPH = _cols([-180.0 + 3.0 * j for j in range(120)],
                [g + s * 1.0 for g in GROOVES for s in (1, -1)],
                [s * p for p in prow_phis + [THETA + FLANK] for s in (1, -1)])
    UPH = [p for p in UPH if -180.0 <= p < 180.0]
    UTS = [0.04, 0.10, 0.16, 0.20, 0.25, 0.30, 0.35, 0.40, 0.44, 0.48, 0.54, 0.60, 0.66, 0.72, 0.76]

    def top_rs(p):
        """The skirt's top edge: steep facing the ribs (they converge into it), battered toward the prow and the flanks."""
        return 0.78 + 0.18 * (1.0 - _smooth(60.0, 90.0, abs(wrap(p))))
    UWALL = tuple((fz, (lambda p, g=g: top_rs(p) + (1.0 - top_rs(p)) * g))
                  for fz, g in ((0.0, 0.0), (0.035, 0.09), (0.10, 0.09), (0.11, 0.18), (0.58, 0.68), (0.59, 0.77), (1.0, 1.0)))
    ubm, ub = polar_bm(UPH, UTS, umbo_r, umbo_top, umbo_bot, UWALL)
    nu = len(UTS)
    B_BRIDGE, B_TIER2, U_CHAMFER, U_SLIT, U_GLACIS = 3, 8, nu, nu + 1, nu + 3

    def cw(j, fr):
        """A column's width at fraction fr of the umbo's radius: an inset must leave its face a real face."""
        b = UPH[j + 1] if j + 1 < len(UPH) else UPH[0] + 360.0
        return math.radians(b - UPH[j]) * fr * umbo_r(colmid(UPH, j))
    runs, cur = [], []
    for j in range(len(UPH)):                            # the bridge's sloped riser in panes about 2.6 wide
        cur.append(j)
        if sum(cw(i, BRIDGE_FR) for i in cur) >= 2.6:
            runs.append(cur)
            cur = []
    if cur:
        runs[-1] += cur
    for i, run in enumerate(runs):
        c_ = abs(wrap(colmid(UPH, run[len(run) // 2])))
        if c_ > 112.0:
            inset(ubm, [ub[B_BRIDGE][j] for j in run], 0.16, 0.4, W)   # the visor: canted panes round the forward riser
        elif i % 2 == 0:
            inset(ubm, [ub[B_BRIDGE][j] for j in run], 0.2, 0.2)
    for j in range(len(UPH)):
        c_ = abs(wrap(colmid(UPH, j)))
        ub[U_CHAMFER][j].material_index = D                 # the citadel's neon edge
        if cw(j, TIER2_FR) > 1.4 and c_ > 95.0 and j % 3 == 0:
            inset(ubm, [ub[B_TIER2][j]], 0.45, 0.3, W)       # slits in the citadel's forward riser
        if cw(j, 1.0) > 1.8:
            if j % 2 == 0 and c_ > 40.0:
                inset(ubm, [ub[U_SLIT][j]], 0.3, 0.25, W)    # the port slit band under the edge
            if j % 2 == 1:
                inset(ubm, [ub[U_GLACIS][j]], 0.45, 0.3)     # the glacis's armour plates
    chan = [j for j in range(len(UPH)) if any(abs(wrap(colmid(UPH, j)) - g) < 1.0 for g in GROOVES)]
    for j in chan:                                       # L4 the grooves run on as channels across the boss
        inset(ubm, [ub[b][j] for b in range(9, 14)], 0.12, 0.6)
    edges = sorted(set([-180.0, 180.0] + list(GROOVES) + [s * a for a in range(50, 180, 20) for s in (1, -1)]))
    for lo, hi in zip(edges, edges[1:]):                 # L4 the boss's and the citadel's plating, sector by sector
        cs = [j for j in range(len(UPH)) if lo + 1.6 < wrap(colmid(UPH, j)) < hi - 1.6 and cw(j, 1.0) / umbo_r(colmid(UPH, j)) > math.radians(1.9)]
        if len(cs) > 1:
            inset(ubm, [ub[b][j] for b in range(10, 13) for j in cs], 0.3, 0.25)
            inset(ubm, [ub[b][j] for b in range(5, 8) for j in cs], 0.15, 0.2)
    umbo = piece(finish_bm("umbo", ubm), None, 20, bevel=0.08, angle=30)
    f.set_smooth(umbo, False)
    wx, wy = at(0.0, WELL_RHO)
    oct_ = [(WELL_R * math.cos(math.radians(22.5 + 45 * a)), WELL_R * math.sin(math.radians(22.5 + 45 * a))) for a in range(8)]
    well = f.plate("well", oct_, 60.0, normal=(0, 0, 1), origin=(wx, wy, 20.0), right=(1, 0, 0))
    import bmesh
    wbm = bmesh.new()
    wbm.from_mesh(well.data)
    bmesh.ops.recalc_face_normals(wbm, faces=wbm.faces)  # a cutter's faces must point out
    wbm.to_mesh(well.data)
    wbm.free()
    ax_, ay_ = at(0.0, 0.5 * (WELL_RHO + WELL_R + UMBO_RMAX) + 2.0)
    notch = f.box("notch", (9.0, 14.0, 40.0), center=(ax_, ay_, 38.0))      # the crystal seen from aft, over the ribs
    for c_ in (well, notch):
        f.ensure_slots(c_, SLOTS)
        f.paint(c_, H)
    kit.subtract(umbo, [well])                           # one at a time: the two overlap, and a joined cutter that
    kit.subtract(umbo, [notch])                          # intersects itself is not a solid to the EXACT solver
    umbo_parts = [umbo]
    flat_r = WELL_R * math.cos(math.radians(22.5))
    for a in range(8):                                   # glowing ribs down the well's walls
        if a == 2:
            continue                                     # (the notch faces aft, +Y)
        ang = math.radians(45.0 * a)
        px, py = wx + math.cos(ang) * (flat_r - 0.1), wy + math.sin(ang) * (flat_r - 0.1)
        z_lo, z_hi = umbo_bot(px, py) + 3.0, umbo_top(px + math.cos(ang) * 0.6, py + math.sin(ang) * 0.6) - 1.5
        bx = f.box("wrib", (0.5, 1.0, z_hi - z_lo), center=(0, 0, 0))
        f.transform_mesh(bx, Matrix.Translation(Vector((px, py, 0.5 * (z_lo + z_hi)))) @ Matrix.Rotation(ang, 4, "Z"))
        umbo_parts.append(piece(bx, D, 21))
    top_z = umbo_top(0.0, HY)
    # L3 the sensor array on the bridge, each mast in five levels: L1 a tapering mast; L2 its stepped base and a collar;
    # L3 two yards with end pods, an emitter dish on the tallest; L4 the collar's band; L5 a tip light, dots on the base
    Zu = Vector((0, 0, 1))
    for (dx, dy, hgt) in ((0.0, 3.0, 6.0), (3.0, -2.0, 4.5), (-3.0, -2.0, 3.5)):
        b_ = Vector((dx, HY + dy, top_z - 0.2))
        umbo_parts.append(piece(lathe_ob("mbase", [(0.0, 0.0), (1.25, 0.0), (1.25, 0.35), (0.95, 0.55), (0.95, 0.8), (0.0, 0.8)], Zu, b_, 16), None, 21, bevel=0.03))
        mast = lathe_ob("mast", [(0.0, 0.0), (0.42, 0.0), (0.42, hgt * 0.45), (0.55, hgt * 0.47), (0.55, hgt * 0.52), (0.32, hgt * 0.54),
                                 (0.26, hgt), (0.0, hgt)], Zu, b_ + Zu * 0.8, 12)
        f.paint(mast, D, lambda c, n, i, b_=b_, hgt=hgt: b_.z + 0.8 + hgt * 0.475 < c.z < b_.z + 0.8 + hgt * 0.515 and math.hypot(c.x - b_.x, c.y - b_.y) > 0.5)
        umbo_parts.append(piece(mast, None, 21))
        for frac, span in ((0.62, 1.6), (0.86, 1.1)):
            zc_ = b_.z + 0.8 + hgt * frac
            umbo_parts.append(piece(f.tube_along("yard", [(b_.x - span, b_.y, zc_), (b_.x + span, b_.y, zc_)], [0.12, 0.12], segments=8), H, 21))
            for sx in (1, -1):
                umbo_parts.append(piece(lathe_ob("ypod", [(0.0, 0.0), (0.22, 0.1), (0.22, 0.5), (0.0, 0.6)], Vector((sx, 0, 0)),
                                                 Vector((b_.x + sx * span, b_.y, zc_)), 8), None, 21))
        if hgt > 5.0:
            dish = lathe_ob("dish", [(0.0, 0.0), (0.25, 0.05), (1.1, 0.45), (1.15, 0.6), (0.0, 0.25)], Vector((0, -1, 0)),
                            Vector((b_.x, b_.y - 0.35, b_.z + 0.8 + hgt * 0.74)), 12)
            f.paint(dish, W, lambda c, n, i, b_=b_: n.y < -0.3 and math.hypot(c.x - b_.x, c.z - (b_.z + 0.8 + hgt * 0.74)) < 0.5)   # its lens
            umbo_parts.append(piece(dish, None, 21))
        umbo_parts.append(piece(f.sphere("mlight", 0.3, center=tuple(b_ + Zu * (0.8 + hgt + 0.25)), segments=8, rings=6), J, 21))
        for k_ in range(3):
            a = math.radians(-60.0 + 60.0 * k_)
            umbo_parts.append(disc("mdot", b_ + Vector((math.sin(a) * 1.1, -math.cos(a) * 1.1, 0.35)), Vector((math.sin(a), -math.cos(a), 0.6)).normalized(), 0.12, 0.06, W, 21, segments=6))
    for sx in (1, -1):                                   # L4 a contour trace round each side of the boss, a jog, a ring
        path = []
        for i in range(24):
            p = sx * (62.0 + 4.0 * i)
            x, y = at(p, 0.78 * umbo_r(p))
            path.append(Vector((x, y, umbo_top(x, y))))
        for s in range(1, 4):
            p = sx * (156.0 + 2.0 * s)
            x, y = at(p, (0.78 - 0.03 * s) * umbo_r(p))
            path.append(Vector((x, y, umbo_top(x, y))))
        umbo_parts.append(strip("utrace", path, 0.45, 0.12, D, 22))
        umbo_parts.append(disc("uterm", path[-1] + Vector((0, 0, 0.1)), Vector((0, 0, 1)), 0.9, 0.16, D, 22, ring=True))
        for i in range(3):                               # L5 a dot row on the citadel's flank
            x, y = at(sx * (100.0 + 3.0 * i), 0.45 * umbo_r(sx * 100.0))
            umbo_parts.append(disc("udot", Vector((x, y, umbo_top(x, y))), Vector((0, 0, 1)), 0.42, 0.12, W, 22))
    bone_part(umbo_parts, "umbo")

    # ================================================================== L1 THE JAWS (auricle.L / auricle.R)
    jaw_info = {}
    jaw_socket = {}
    for side_, sx in (("L", 1), ("R", -1)):
        outline = [(sx * x, y) for (x, y) in jaw_outline()]
        if sx < 0:
            outline = list(reversed(outline))
        jaw = f.plate("jaw", outline, JAW_T, normal=(0, 0, 1), origin=(0.0, 0.0, JAW_Z), right=(1, 0, 0))
        f.ensure_slots(jaw, SLOTS)
        f.paint(jaw, H)
        f.bevel(jaw, width=0.25, segments=1, angle_deg=30)
        f.apply_modifiers(jaw)
        cut = [f.lathe("hole", [(0.0, -4.0), (r_, -4.0), (r_, 4.0), (0.0, 4.0)], segments=10, axis=(0, 0, 1),
                       origin=(sx * hx, hy, JAW_Z)) for (hx, hy, r_) in ((21.4, -94.4, 1.3), (25.6, -88.8, 1.0), (27.8, -85.0, 0.6))]
        cut += [f.lathe("bite", [(0.0, -4.0), (1.2, -4.0), (1.2, 4.0), (0.0, 4.0)], segments=10, axis=(0, 0, 1),
                        origin=(sx * 33.5, by, JAW_Z)) for by in (-62.8, -65.6, -68.4)]
        for h_ in cut:
            f.ensure_slots(h_, SLOTS)
            f.paint(h_, D)
        for yc in JAW_SLOTS:                             # the danger blades set into the biting edge: the mouth
            nm = sname("danger")
            c = Vector((sx * JAW_IN, yc, JAW_Z))
            kit.cradle(nm, c, Vector((0, -1, 0)), Vector((0, 0, 1)), 300 + len(jaw_socket), SLAB, core_top, ped_len=0.3)
            jaw_socket[nm] = "auricle." + side_
            cut.append(slab_slot(c, Vector((0, -1, 0)), Vector((0, 0, 1))))
        kit.subtract(jaw, cut)
        f.set_smooth(jaw, False)
        jparts = [piece(jaw, None, 30 + (sx > 0))]
        ztop = JAW_Z + JAW_T / 2
        tr = [Vector((sx * x, y, ztop)) for (x, y) in ((31.6, -59.6), (31.6, -69.6), (29.8, -71.4), (29.8, -80.6),
                                                       (30.6, -82.4), (29.0, -86.4), (27.15, -87.7))]
        jparts.append(strip("jtrace", tr, 0.45, 0.12, D, 33))
        # the trace ends in a ring round the knuckle's cut-out, the Dolphin's ringed hole
        jparts.append(disc("jterm", Vector((sx * 25.6, -88.8, ztop + 0.02)), Vector((0, 0, 1)), 1.9, 0.16, D, 33, ring=True))
        for i in range(3):
            jparts.append(disc("jdot", Vector((sx * (25.0 + 1.6 * i), -60.4, ztop)), Vector((0, 0, 1)), 0.42, 0.12, W, 33))
        bone_part(jparts, "auricle." + side_)
        jaw_info[sx] = Vector((sx * 27.0, -58.0, JAW_Z))

    # ================================================================== L1 THE DECK (the lower valve) + L2 its bulwarks
    q0, q1 = outline_pts()[0], outline_pts()[1]
    edge_cols = [polar(q0[0] + (q1[0] - q0[0]) * s / 24, q0[1] + (q1[1] - q0[1]) * s / 24)[1] for s in range(25)]
    CPH = _cols([-180.0 + 2.5 * j for j in range(144)],
                [s * (THETA + d_) for d_ in (-0.3, 0.3, 1.5, 3.0, 4.5) for s in (1, -1)],
                [s * p for p in edge_cols + prow_phis for s in (1, -1)], gap=1.0)
    CPH = [p for p in CPH if -180.0 <= p < 180.0]
    CTS = [0.08, 0.16, 0.24, 0.30, 0.354, 0.385, 0.415, 0.44, 0.50, 0.58, 0.66, 0.74, 0.82, 0.89, 0.94, 0.965]
    CWALL = ((0.0, 0.975), (0.05, 0.985), (0.30, 0.985), (0.31, 1.0), (0.70, 1.0), (0.80, 0.99), (0.95, 0.975))
    cbm, cb = polar_bm(CPH, CTS, deck_r, floor_z, deck_bot, CWALL)
    nt = len(CTS)
    W_CHAMFER, W_UPPER, W_LOWER = nt, nt + 1, nt + 3
    nb_first = nt + len(CWALL)
    for j in range(len(CPH)):
        cb[W_CHAMFER][j].material_index = D              # the deck edge's neon line
    for j in range(len(CPH)):                            # L4 port rows in the deck's outer band, vent slits above
        if abs(colmid(CPH, j)) <= THETA + FLANK and j % 3 == 0:
            inset(cbm, [cb[W_LOWER][j]], 0.45, 0.3, W)
        elif j % 7 == 1 and abs(colmid(CPH, j)) > THETA + FLANK + CRANK:
            inset(cbm, [cb[W_UPPER][j]], 0.25, 0.3)
    for s0 in range(NPLATE):                             # the cavity floor's plating, sector by sector under the ribs
        lo, hi = PHI[s0], PHI[s0 + 1]
        cs = [j for j, p in enumerate(CPH) if lo + 1.0 < p + 0.6 < hi - 1.6]
        inset(cbm, [cb[k][j] for k in range(8, 11) for j in cs], 0.5, 0.25)
        inset(cbm, [cb[k][j] for k in range(11, 13) for j in cs], 0.5, 0.25)
        inset(cbm, [cb[k][j] for k in range(nb_first + 2, nb_first + 6) for j in cs], 0.5, 0.35)   # the underside's
    deck = piece(finish_bm("deck", cbm), None, 1, bevel=0.08, angle=30)
    f.set_smooth(deck, False)

    # L2 THE BULWARKS: a wedge on the deck from under the shell's outer edge to the swept leading edge, its top a
    # walkway a shadow seam below the shell, its face stepped (a vent band, then the main face the blades are set into)
    def edge_at(rho):
        """The +X leading edge's point at distance rho from the hinge, and its fraction from the lip corner."""
        ex, ey = q1[0] - q0[0], q1[1] - q0[1]
        ax, ay = q0[0], q0[1] - HY
        A, B, C = ex * ex + ey * ey, 2 * (ax * ex + ay * ey), ax * ax + ay * ay - rho * rho
        t = (-B - math.sqrt(max(B * B - 4 * A * C, 0.0))) / (2 * A)
        return Vector((q0[0] + ex * t, q0[1] + ey * t, 0.0)), t
    BW_RHO = [BULWARK_RHO[0] + (RC - BULWARK_RHO[0]) * i / 38 for i in range(39)]
    BW_STEP = 3.4                                        # the glacis leans out this far from under the vent band
    hull_parts = [deck]
    blade_s = []
    for side in (1, -1):
        rings = []
        for rho in BW_RHO:
            ix, iy = at(side * (THETA - 0.6), rho)
            inner = Vector((ix, iy, 0.0))
            outer, _ = edge_at(rho)
            outer.x *= side
            lat = (outer - inner).normalized()
            top = bulwark(rho)
            bot = min(floor_z(inner.x, inner.y), floor_z(outer.x, outer.y)) - 0.6

            def P(p, z):
                return Vector((p.x, p.y, z))
            rings.append([P(inner, bot), P(inner, top), P(outer - lat * 4.4, top), P(outer - lat * 3.6, top - 0.9),
                          P(outer - lat * 4.0, top - 1.5), P(outer - lat * 4.0, top - 3.2), P(outer - lat * BW_STEP, top - 3.8),
                          P(outer, bot)])
        bbm, bg, bcap = loft_bm(rings)
        for t, row in enumerate(bg):
            row[2].material_index = D                    # the walkway's neon edge
        for t in range(1, len(bg) - 1):
            if t % 4 == 1:
                inset(bbm, [bg[t][4]], 0.4, 0.3, W)      # L4 port slits under the edge
            if t % 2 == 0:
                inset(bbm, [bg[t][1]], 0.3, 0.15)        # the walkway's deck plates
            if t % 2 == 1:
                inset(bbm, [bg[t][6]], 0.5, 0.3)         # the glacis's armour plates
        bw = piece(finish_bm("bulwark", bbm), None, 1, bevel=0.08, angle=30)
        f.set_smooth(bw, False)
        e_ = Vector((side * (q1[0] - q0[0]), q1[1] - q0[1], 0.0)).normalized()
        outn = e_.cross(Vector((0, 0, 1)))
        if outn.x * side < 0:
            outn = -outn
        cuts = []
        for s_ in EDGE_ROWS:                             # the danger blades set into the swept leading edge
            p = Vector((side * (q0[0] + (q1[0] - q0[0]) * s_), q0[1] + (q1[1] - q0[1]) * s_, 0.0))
            rho = polar(p.x, p.y)[0]
            zt = bulwark(rho) - 3.8
            zb = min(floor_z(p.x, p.y), floor_z(p.x - outn.x * 12.0, p.y - outn.y * 12.0)) - 0.6
            c = Vector((p.x, p.y, 0.5 * (zt + zb))) - outn * (0.5 * BW_STEP)   # on the glacis, halfway down
            # a blade lies FLAT, its edge outboard: from the side it is a glowing line along the armour, a cutting edge
            kit.cradle(sname("danger"), c, e_, Vector((0, 0, 1)), 200 + len(blade_s), SLAB, core_top, ped_len=0.3)
            blade_s.append(s_)
            cuts.append(slab_slot(c, e_, Vector((0, 0, 1)), size=(7.2, 10.6, 2.0, 1.3)))
        kit.subtract(bw, cuts)
        hull_parts.append(bw)
    for g in GROOVES + [-THETA, THETA]:                  # L3 the underside's radial ribs
        pts = []
        for i in range(20):
            rho = 36.0 + (deck_r(g) * 0.95 - 36.0) * i / 19
            x, y = at(g, rho)
            pts.append(Vector((x, y, deck_bot(x, y) - 0.35)))
        hull_parts.append(strip("urib", pts, 1.6, 0.7, H, 3, lift=-0.7))
    for side in (1, -1):                                 # L5 running lights along the walkways
        for i in range(7):
            rho = 62.0 + 10.5 * i
            x, y = at(side * (THETA + 0.5 * FLANK), rho)
            hull_parts.append(piece(f.sphere("wlight", 0.4, center=(x, y, bulwark(rho) + 0.3), segments=8, rings=6), J, 3))

    # ================================================================== L1 THE SPINE: a faceted keel hull, stepped
    rings = []
    ys = sorted(set([s[0] for s in SPINE] + [SPINE[0][0] + (SPINE[-1][0] - SPINE[0][0]) * i / 40 for i in range(41)]))
    for y in ys:
        hw, zb, zt = spine_at(y)
        c_ = 0.2 * min(hw, zt - zb)
        oct_ = [(-hw + c_, zt), (hw - c_, zt), (hw, zt - c_), (hw, zb + c_), (hw - c_, zb), (-hw + c_, zb), (-hw, zb + c_), (-hw, zt - c_)]
        rings.append([Vector((x, y, z)) for (x, z) in oct_])
    sbm, sg, scap = loft_bm(rings)
    collar_rows = [i for i, (a, b) in enumerate(zip(ys, ys[1:])) if (-55.3 < a < -53.1) or (20.7 < a < 22.7)]
    for i in collar_rows:
        for j in range(8):
            sg[i][j].material_index = D                  # the collars' bands
    for i, (a, b) in enumerate(zip(ys, ys[1:])):         # L4 panels and port rows on the spine's flanks
        if -50.0 < a < 18.0 and b - a > 2.0:
            for j in (2, 6):                             # (faces 2 and 6 are the flanks; 3 and 7 are chamfers)
                inset(sbm, [sg[i][j]], 0.6, 0.3)
    spine = piece(finish_bm("spine", sbm), None, 2, bevel=0.08, angle=30)
    f.set_smooth(spine, False)
    # the hangar in its prow and the tunnel from the hangar's back wall, curving down out through the keel line
    bay = f.box("bay", (2 * HANGAR_HW, HANGAR_BACK - (SPINE[0][0] - 12.0), HANGAR_Z1 - HANGAR_Z0),
                center=(0.0, 0.5 * (HANGAR_BACK + SPINE[0][0] - 12.0), 0.5 * (HANGAR_Z0 + HANGAR_Z1)))
    f.ensure_slots(bay, SLOTS)
    f.paint(bay, H)
    f.paint(bay, J, lambda c_, n_, i: n_.y > 0.9)
    tun_ctrl = [Vector((0.0, HANGAR_BACK - 1.0, -17.5)), Vector((0.0, -14.0, -17.5)), Vector((0.0, 8.0, -17.5)),
                Vector((0.0, 26.0, -19.0)), Vector((0.0, 38.0, -23.0)), Vector((0.0, 48.0, -30.0))]
    tunnel = []
    for i in range(len(tun_ctrl) - 1):
        p0, p1, p2, p3 = tun_ctrl[max(i - 1, 0)], tun_ctrl[i], tun_ctrl[i + 1], tun_ctrl[min(i + 2, len(tun_ctrl) - 1)]
        for s in range(6):
            t = s / 6.0
            tunnel.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    tunnel.append(tun_ctrl[-1])
    tcut = f.tube_along("tunnel", [tuple(p) for p in tunnel], [4.4] * len(tunnel), segments=8)
    f.ensure_slots(tcut, SLOTS)
    f.paint(tcut, H)
    kit.subtract(spine, [bay])
    kit.subtract(spine, [tcut])                          # (the tunnel opens out of the bay's back wall)
    hull_parts.append(spine)
    for i in range(3, len(tunnel) - 2, 3):               # the tunnel's octagonal frames and lights
        tgt = (tunnel[i + 1] - tunnel[i - 1]).normalized()
        u = tgt.cross(Vector((0, 0, 1))).normalized()
        v = u.cross(tgt).normalized()
        ring = [tunnel[i] + (u * math.cos(2 * math.pi * a / 8 + math.pi / 8) + v * math.sin(2 * math.pi * a / 8 + math.pi / 8)) * 4.5 for a in range(9)]
        hull_parts.append(piece(f.tube_along("tframe", [tuple(p) for p in ring], [0.45] * 9, segments=4, cap=False), D, 5))
        hull_parts.append(piece(f.sphere("tlight", 0.4, center=tuple(tunnel[i] + v * 4.0), segments=8, rings=6), J, 5))
    front_y = SPINE[0][0]
    for z in (HANGAR_Z1 + 0.55, HANGAR_Z0 - 0.55):      # the mouth's thin neon frame
        hull_parts.append(piece(f.box("hframe", (2 * HANGAR_HW + 2.2, 0.5, 0.45), center=(0.0, front_y - 0.2, z)), D, 6))
    for sx in (1, -1):
        hull_parts.append(piece(f.box("hjamb", (0.45, 0.5, HANGAR_Z1 - HANGAR_Z0 + 1.55), center=(sx * (HANGAR_HW + 0.875), front_y - 0.2,
                                                                                                    0.5 * (HANGAR_Z0 + HANGAR_Z1))), D, 6))
    for i in range(6):
        y = HANGAR_BACK - 4.0 - 6.4 * i
        arch = [(-HANGAR_HW + 0.3, y, HANGAR_Z0), (-HANGAR_HW + 0.3, y, HANGAR_Z1 - 0.3), (HANGAR_HW - 0.3, y, HANGAR_Z1 - 0.3),
                (HANGAR_HW - 0.3, y, HANGAR_Z0)]
        hull_parts.append(piece(f.tube_along("arch", arch, [0.5] * 4, segments=4), H, 7))
        for sx in (1, -1):
            hull_parts.append(piece(f.box("flight", (0.8, 2.0, 0.2), center=(sx * 6.5, y - 3.2, HANGAR_Z0 + 0.1)), J, 7))
    for sx in (1, -1):                                   # L4 a port row along the spine's forward flanks
        for i in range(9):
            y = -84.0 + 3.2 * i
            hw, zb, zt = spine_at(y)
            hull_parts.append(piece(f.box("sport", (0.3, 1.6, 0.6), center=(sx * (hw + 0.05), y, zt - 4.6)), W, 4))
    # L3 the side pods on the spine's flanks, each in five levels: L1 a faceted pod; L2 its collar, the recessed vent
    # bay and the exhaust; L3 the grille's fins, a sensor blister with its lens; L4 panel seams, a trace with a jog and
    # a ring; L5 bolt heads on the collar, a light at the nose
    for sx in (1, -1):
        for (y0, y1) in ((-40.0, -20.0), (-8.0, 12.0)):
            hw, zb, zt = spine_at(0.5 * (y0 + y1))
            zc = zb + 0.45 * (zt - zb)
            cx, ym = sx * (hw + 1.4), 0.5 * (y0 + y1)
            ST = ((y0, 0.55), (y0 + 1.4, 0.9), (y0 + 3.0, 1.0), (ym - 0.7, 1.0), (ym - 0.6, 1.09), (ym + 0.6, 1.09), (ym + 0.7, 1.0),
                  (y1 - 3.2, 1.0), (y1 - 1.4, 0.86), (y1, 0.6))

            def octa(y, k, w=2.2, h=2.6):
                w, h = w * k, h * k
                c = 0.35 * min(w, h)
                return [Vector((cx + sx * dx, y, zc + dz)) for (dx, dz) in
                        ((w, h - c), (w - c, h), (-w + c, h), (-w, h - c), (-w, -h + c), (-w + c, -h), (w - c, -h), (w, -h + c))]
            pbm, pg, pcap = loft_bm([octa(y, k) for y, k in ST])
            for j in range(8):
                pg[4][j].material_index = D                  # L2 the collar's band
            for t in (2, 6):
                for j in (1, 5):
                    inset(pbm, [pg[t][j]], 0.3, 0.12)        # L4 panel seams top and bottom
            inset(pbm, [pg[2][7]], 0.45, 0.35)               # L2 the vent bay in the outer flank, ahead of the collar
            inset(pbm, [pg[6][7]], 0.5, 0.15)
            pod = piece(finish_bm("pod", pbm), None, 4, bevel=0.05, angle=30)
            hull_parts.append(pod)
            xo = cx + sx * (2.2 - 0.35)
            for i in range(6):                               # L3 the grille's fins in the bay
                hull_parts.append(piece(f.box("fin", (0.5, 0.3, 3.0), center=(xo, y0 + 4.0 + 1.25 * i, zc)), H, 4))
            ex = lathe_ob("exhaust", [(1.1, 0.0), (1.1, 0.5), (0.95, 0.8), (0.75, 0.8), (0.6, 0.4), (0.0, 0.3)], Vector((0, 1, 0)),
                          Vector((cx, y1 - 0.3, zc)), 16)    # L2 the exhaust
            f.paint(ex, J, lambda c, n, i: n.y < -0.2 or math.hypot(c.x - cx, c.z - zc) < 0.7)
            hull_parts.append(piece(ex, None, 4))
            bl = lathe_ob("blister", [(0.0, 0.0), (1.1, 0.0), (1.0, 0.5), (0.6, 0.95), (0.0, 1.05)], Vector((0, 0, 1)),
                          Vector((cx - sx * 0.3, y0 + 5.0, zc + 2.6 - 0.05)), 6)
            hull_parts.append(piece(bl, None, 4, bevel=0.03))
            hull_parts.append(disc("lens", Vector((cx - sx * 0.3, y0 + 4.15, zc + 3.05)), Vector((0, -1, 0.35)).normalized(), 0.35, 0.08, W, 4))
            tr = [Vector((cx - sx * 0.6, y, zc + 2.6)) for y in (y0 + 7.0, ym - 1.8)] + [Vector((cx + sx * 0.4, ym - 0.8, zc + 2.6))]
            hull_parts.append(strip("ptrace", tr, 0.3, 0.08, D, 4))            # L4 the trace on the pod's back
            hull_parts.append(disc("pterm", tr[-1] + Vector((0, 0.5, 0.12)), Vector((0, 0, 1)), 0.38, 0.08, D, 4, ring=True))
            for dz in (-1.6, -0.5, 0.6, 1.7):                # L5 bolt heads on the collar's outer face
                hull_parts.append(disc("pbolt", Vector((cx + sx * 2.4, ym, zc + dz)), Vector((sx, 0, 0)), 0.15, 0.09, H, 4, segments=6))
            hull_parts.append(piece(f.sphere("plight", 0.3, center=(cx, y0 - 0.15, zc), segments=8, rings=6), J, 4))   # L5 the nose light
    # the lift pylons in the cavity (dark, plain: they hold the hall up, they are not ornaments)
    for pm in MIDS:
        x, y = at(pm, 66.0)
        top = Vector((x, y, dome(x, y) + tile_lift(66.0) - PLATE_T - SOCKET_CLEAR - 0.8))
        hull_parts += kit.column(top, core_top(x, y) - 0.3, 1.6, 8, band=False)
        cap = f.lathe("lcap", [(0.0, 0.0), (2.0, 0.0), (1.4, 0.4), (0.0, 0.4)], segments=8, axis=(0, 0, 1), origin=tuple(top))
        hull_parts.append(piece(cap, J, 8))
    # the whimsy: pairs of blue eyes peering out of the cavity's dark
    for pm in (-18.75, 0.0, 18.75):
        x, y = at(pm, 58.5)
        base_ = Vector((x, y, core_top(x, y) + 2.2))
        acr = Vector((*fan_perp(pm), 0.0))
        for s_ in (1, -1):
            e = base_ + acr * (1.5 * s_)
            hull_parts.append(piece(f.sphere("eye", 1.0, center=tuple(e), segments=10, rings=8), W, 4))
            hull_parts.append(piece(f.box("eyepost", (0.8, 0.8, 1.6), center=tuple(e - Vector((0, 0, 1.6)))), H, 4))

    # ================================================================== L3 SOCKETS
    pid_s = 100
    for (nm, c, fr, par, where) in stellas:
        kit.shield_socket(nm, "stella", c, fr, STELLA_S, pid_s, core_top(c.x, c.y) - 0.3, SOCKET_CLEAR)
        pid_s += 1
    for k, pm in enumerate(MIDS):                      # the brood
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
    for g in GROOVES:                                  # the gills, standing in the grooves on rails
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
            spine_pts, profs = [], []
            for i in range(9):
                rho = r0 + (r1 - r0) * i / 8
                x, y = at(g, rho)
                spine_pts.append(Vector((x, y, rail_top(x, y) - 0.7)))
                profs.append([(-1.0, -0.7), (1.0, -0.7), (1.0, 0.7), (-1.0, 0.7)])
            rail = f.spine_loft("rail", [tuple(p) for p in spine_pts], profs)
            f.set_smooth(rail, False)
            hull_parts.append(piece(rail, H, 9))
            for rho in sorted({r0 + 1.2, r1 - 1.2}):
                x, y = at(g, rho)
                hull_parts += kit.column(Vector((x, y, rail_top(x, y) - 1.4)), core_top(x, y) - 0.3, 1.0, 9, band=False)
    for g in GROOVES:                                  # the grille between the jets in the stern gape
        if abs(g) < 1e-6:
            continue
        x, y = at(g, GRILLE_RHO)
        tang = Vector((*fan_perp(g), 0.0))
        zc = 0.5 * (core_top(x, y) + dome(x, y) + tile_lift(GRILLE_RHO) - PLATE_T)
        kit.cradle(sname("danger"), Vector((x, y, zc)), Vector((0, 0, 1)), tang, pid_s, SLAB, core_top, column=True, band=False)
        pid_s += 1
    ctx.log("sockets: %s (%d)" % (count, sum(count.values())))

    # the three escorts' launch pads, the clam-jaw doors in the hangar mouth
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
    door_y = front_y + 1.6
    door_z = {"T": (0.5 * (HANGAR_Z0 + HANGAR_Z1) + DOOR_GAP / 2, HANGAR_Z1 - DOOR_GAP),
              "B": (HANGAR_Z0 + DOOR_GAP, 0.5 * (HANGAR_Z0 + HANGAR_Z1) - DOOR_GAP / 2)}
    door_hinge = {}
    for key, (z0, z1) in door_z.items():
        xs = [-(HANGAR_HW - DOOR_GAP) + 2 * (HANGAR_HW - DOOR_GAP) * i / 8 for i in range(9)]
        rings = [[Vector((x, door_y, z1)), Vector((x, door_y, z0)), Vector((x, door_y + 1.0, z0)), Vector((x, door_y + 1.0, z1))] for x in xs]
        bm, grid, caps = loft_bm(rings)
        for row in grid:
            row[1 if key == "T" else 3].material_index = J
        for t in (1, 3, 5, 7):
            inset(bm, [grid[t][0]], 0.35, 0.2)
        leaf = piece(finish_bm("door", bm), None, 90, bevel=0.08, angle=30)
        f.set_smooth(leaf, False)
        dparts = [leaf]
        zh = z1 - 0.45 if key == "T" else z0 + 0.45     # L3 the hinge knuckles along the hinge edge
        for xk in (-8.4, 0.0, 8.4):
            kn = lathe_ob("knuckle", [(0.0, 0.0), (0.55, 0.0), (0.55, 2.4), (0.0, 2.4)], Vector((1, 0, 0)), Vector((xk - 1.2, door_y + 0.5, zh)), 16)
            f.paint(kn, D, lambda c, n, i, xk=xk: abs(c.x - xk) > 1.15)
            dparts.append(piece(kn, None, 91))
        zl = z0 + 0.6 if key == "T" else z1 - 0.6       # L5 lights at the meeting edge
        for xl in (-11.4, 11.4):
            dparts.append(piece(f.sphere("dlight", 0.22, center=(xl, door_y - 0.02, zl), segments=8, rings=6), J, 91))
        bone_part(dparts, "hangar.door." + key)
        door_hinge[key] = Vector((0.0, door_y, z1 if key == "T" else z0))

    # ================================================================== PAYLOAD: the lance in the centre groove
    def gpt(rho):
        x, y = at(0.0, rho)
        return Vector((x, y, dome(x, y) + tile_lift(rho) - PAY_R + 0.7))
    p_rear = gpt(PAY_RHO0)
    pax = (gpt(R - 2.0) - p_rear).normalized()
    plen = PAY_RHO1 - PAY_RHO0

    def lance(p_rear, pax, plen, pay_r, gap, fracs):
        """The payload in five levels: L1 a shaft and a spear head; L2 pommel, grip, two collars, the socket, the head's
        glowing core between two side blades; L3 four tail vanes, a three-turn energy coil, the cross blades; L4 grip
        rings, shaft seams, a contour trace with a jog and a ring; L5 bolt circles and the pommel's light. Bone parts
        `payload` and `payload.dock` (the hover cradles, as the kit's). Returns (pay_mid, p_tip, dock_head)."""
        ac0 = pax.cross(Vector((0, 0, 1))).normalized()
        up0 = ac0.cross(pax).normalized()

        def S(c):
            return (c - p_rear).dot(pax)
        hs = plen - 12.0                                 # the head's socket
        sr = pay_r - 0.2
        prof = [(0.0, 0.0), (0.9, 0.0), (1.5, 0.4), (1.5, 1.4), (1.2, 1.7), (1.2, 1.9), (1.35, 2.1), (1.35, 7.0), (1.2, 7.2),
                (2.0, 7.6), (2.1, 8.0), (2.1, 9.6), (2.0, 10.0), (sr, 10.4), (sr, 19.0), (1.95, 19.3), (1.95, 21.4), (sr, 21.7),
                (sr, hs - 2.4), (1.5, hs - 1.9), (2.2, hs - 1.3), (2.3, hs - 0.9), (2.3, hs + 0.2), (1.4, hs + 0.6), (0.0, hs + 0.6)]
        shaft = lathe_ob("shaft", prof, pax, p_rear, 24)
        f.paint(shaft, J, lambda c, n, i: S(c) < 0.05)                                   # the pommel's glowing end
        f.paint(shaft, D, lambda c, n, i: 8.05 < S(c) < 9.55 or 19.35 < S(c) < 21.35)     # the collars' bands
        inset_where(shaft, lambda c, n: 10.5 < S(c) < 18.9, 0.06, 0.03)                  # L4 seams along the shaft
        parts = [piece(shaft, None, 40, bevel=0.04)]
        for i in range(6):                                                               # L4 the grip's rings
            s0 = 2.5 + 0.8 * i
            parts.append(piece(lathe_ob("grip", [(1.3, s0), (1.48, s0), (1.48, s0 + 0.3), (1.3, s0 + 0.3)], pax, p_rear, 24, cap=False), None, 40))
        for i in range(3):                                                               # L3 the energy coil
            s0 = hs - 6.2 + 1.1 * i
            parts.append(piece(lathe_ob("coil", [(sr - 0.05, s0), (sr + 0.42, s0), (sr + 0.42, s0 + 0.45), (sr - 0.05, s0 + 0.45)], pax, p_rear, 24, slot=D, cap=False), None, 41))
        head = f.lathe("head", [(0.0, -0.3), (1.2, 0.0), (2.2, 3.4), (1.4, 8.6), (0.0, 11.4)], segments=4,
                       axis=tuple(pax), origin=tuple(p_rear + pax * (hs + 0.6)), up=(0, 0, 1))
        parts.append(piece(head, J, 41))                                                 # L2 the head's glowing core
        for sx in (1, -1):                                                               # L2/L3 the side blades, edged in domain
            bl = f.plate("blade", [(0.4, 1.1), (3.6, 3.3), (8.4, 2.0), (10.6, 0.5), (8.2, 1.3), (3.8, 2.3), (1.2, 1.4)], 0.42,
                         normal=tuple(up0), origin=tuple(p_rear + pax * (hs + 0.6)), right=tuple(pax))
            if sx < 0:
                f.transform_mesh(bl, Matrix.Translation(p_rear + pax * (hs + 0.6)) @ Matrix.Rotation(math.pi, 4, tuple(pax)) @
                                 Matrix.Translation(-(p_rear + pax * (hs + 0.6))))
            f.ensure_slots(bl, SLOTS)
            f.paint(bl, H)
            f.paint(bl, D, lambda c, n, i: abs(n.dot(up0)) < 0.5)
            parts.append(piece(bl, None, 42, bevel=0.03))
        for adeg in (45.0, 135.0, 225.0, 315.0):                                         # L3 the tail vanes
            rd = ac0 * math.cos(math.radians(adeg)) + up0 * math.sin(math.radians(adeg))
            fin = f.plate("fin", [(0.6, sr - 0.1), (6.4, sr - 0.1), (5.4, sr + 1.6), (1.4, sr + 1.8)], 0.32,
                          normal=tuple(pax.cross(rd)), origin=tuple(p_rear), right=tuple(pax))
            f.ensure_slots(fin, SLOTS)
            f.paint(fin, H)
            f.paint(fin, D, lambda c, n, i, rd=rd: (c - p_rear).dot(rd) > sr + 1.1 and abs(n.dot(pax.cross(rd))) < 0.5)
            parts.append(piece(fin, None, 42, bevel=0.03))
            for k in range(3):                                                           # L4 chevrons on each vane
                c0 = p_rear + pax * (2.2 + 1.1 * k) + rd * (sr + 0.9)
                ch = f.box("chev", (0.5, 0.18, 0.36), center=(0, 0, 0))
                M, _, _, _ = kit.frame_matrix(c0, pax, pax.cross(rd))
                f.transform_mesh(ch, M)
                parts.append(piece(ch, D, 42))
        tr = [p_rear + pax * s_ + up0 * (sr + 0.02) for s_ in (10.7, 14.0, 17.2)] + \
             [p_rear + pax * 18.0 + (up0 * math.cos(0.5) + ac0 * math.sin(0.5)) * (sr + 0.02)]
        parts.append(strip("ptrace", tr, 0.3, 0.08, D, 43))                              # L4 the trace, jogging off the top
        parts.append(disc("pterm", tr[-1], (up0 * math.cos(0.5) + ac0 * math.sin(0.5)), 0.45, 0.1, D, 43, ring=True))
        for s_, rr_ in ((8.8, 2.1), (hs - 0.35, 2.3)):                                   # L5 bolt circles
            for i in range(10):
                a = 2 * math.pi * (i + 0.5) / 10
                rd = ac0 * math.cos(a) + up0 * math.sin(a)
                parts.append(disc("pbolt", p_rear + pax * s_ + rd * rr_, rd, 0.14, 0.09, H, 43, segments=6))
        bone_part(parts, "payload")
        dock = []
        for frac in fracs:
            c = p_rear + pax * (plen * frac)
            M, ac, al, upn = kit.frame_matrix(c, pax, Vector((0, 0, 1)))
            ptop = -2.2 - gap                            # the lance hovers `gap` over the prongs (its widest collar)
            for sx in (1, -1):
                dock.append(kit.boxm("prong", (0.6, 2.4, 2.0), (sx * 2.0, 0, ptop - 1.0), M, D, 43, bevel=0.08))
            dock.append(kit.boxm("fbase", (5.2, 2.8, 0.7), (0, 0, ptop - 2.35), M, H, 43, bevel=0.1))
            dock.append(kit.boxm("fpad", (2.6, 2.0, 0.14), (0, 0, ptop - 1.93), M, J, 43))
            bottom = c + upn * (ptop - 2.7)
            ped_h = max(0.8, bottom.z - core_top(bottom.x, bottom.y) + 0.3)
            ped = f.lathe("fped", [(0.0, 0.0), (1.0, 0.0), (1.0, ped_h - 0.6), (1.4, ped_h - 0.3), (1.4, ped_h), (0.0, ped_h)],
                          segments=12, axis=(0, 0, 1), origin=tuple(bottom - Vector((0, 0, ped_h - 0.15))))
            dock.append(piece(ped, H, 43))
        bone_part(dock, "payload.dock")
        pay_mid = p_rear + pax * (plen * 0.5)
        return pay_mid, p_rear + pax * plen, pay_mid - Vector((0, 0, 2.2 + gap + 2.0))
    pay_mid, p_tip, dock_head = lance(p_rear, pax, plen, PAY_R, LANCE_GAP, fracs=(0.20, 0.30))

    # ================================================================== L2 THE JET BANK in the stern gape, in gimbal rings
    def jet(name, o, axis, ln, rr):
        """A jet in five levels of its own. L1: a cowled intake, a waist through the gimbal ring, a core, a nozzle.
        L2: the cowl's lip, two collars, the nozzle's iris. L3: ten stator vanes round a spinner, twelve iris petals with
        six actuators, four strakes, two conduits on clamps. L4: panel seams, side vent slots, a contour trace with a
        45-degree jog ending in a ring. L5: bolt heads on the collars, running lights, a dot row. Nothing within 2.4 of
        the ring's plane is wider than the waist, so every pitch clears the ring."""
        u = axis.cross(Vector((0, 0, 1))).normalized()
        v = u.cross(axis).normalized()

        def RD(adeg):
            a = math.radians(adeg)
            return u * math.cos(a) + v * math.sin(a)

        def P(s_, r_, adeg):
            return o + axis * s_ + RD(adeg) * r_

        def S(c):
            return (c - o).dot(axis)

        def RR(c):
            d = c - o
            return (d - axis * d.dot(axis)).length

        def ANG(c):
            d = c - o
            return math.degrees(math.atan2(d.dot(v), d.dot(u))) % 360.0

        def near(adeg, angles, tol):
            return any(abs(((adeg - b + 180.0) % 360.0) - 180.0) < tol for b in angles)
        R1, R2, RCO = rr * 1.24, rr * 1.25, rr * 1.31          # the cowl, the core, the collars
        STRAKES, PIPES, JACKS = (0.0, 180.0, 240.0, 300.0), (62.0, 118.0), tuple(15.0 + 60.0 * i for i in range(6))
        prof = [(0.0, 1.8), (rr * 0.83, 1.2), (rr * 0.85, 0.6), (rr * 0.99, 0.2), (rr * 1.1, 0.0), (rr * 1.21, 0.2),
                (R1, 0.8), (R1, 1.6), (rr, 2.2), (rr, 7.6), (rr * 1.14, 8.0), (R2, 8.4),
                (R2, 10.6), (RCO, 10.8), (RCO, 11.8), (R2, 12.0), (R2, 14.4), (RCO, 14.6), (RCO, 15.4), (R2, 15.6),
                (R2, 16.6), (rr * 1.14, 17.4), (rr * 1.08, 17.6), (rr * 1.08, 18.4),
                (rr * 1.22, ln - 0.25), (rr * 1.25, ln), (rr * 1.15, ln), (rr * 1.04, ln - 1.4), (rr * 0.92, ln - 3.0),
                (rr * 0.66, ln - 3.6), (rr * 0.44, ln - 3.2), (rr * 0.38, ln - 1.7), (rr * 0.24, ln - 0.7), (0.0, ln - 0.5)]
        body = lathe_ob("jet", prof, axis, o, 32)
        f.paint(body, J, lambda c, n, i: S(c) < 1.75 and RR(c) < rr * 0.8)                        # L2 the intake's core

        def collar_band(c):
            return (10.75 < S(c) < 11.85 or 14.55 < S(c) < 15.45) and RR(c) > rr * 1.28
        f.paint(body, D, lambda c, n, i: collar_band(c))                                             # L2 the collars

        def liner(c, n):
            d = c - o
            radial = d - axis * d.dot(axis)
            return S(c) > ln - 3.9 and (radial.length < rr * 0.7 or (n.dot(radial.normalized()) < -0.05 and S(c) < ln - 1.0))
        f.paint(body, J, lambda c, n, i: liner(c, n))                                # the glow deep in the bell (its lip ring dark), the plug
        inset_where(body, lambda c, n: 12.1 < S(c) < 14.3 and RR(c) > rr * 1.2, 0.07, 0.04)          # L4 panel seams
        inset_where(body, lambda c, n: 8.5 < S(c) < 10.5 and RR(c) > rr * 1.2 and near(ANG(c), (0.0, 180.0), 36.0), 0.1, 0.14)   # vents
        parts = [piece(body, None, 50, bevel=0.04)]
        parts.append(piece(lathe_ob("spinner", [(0.0, 0.35), (0.55, 0.45), (0.95, 0.9), (0.95, 1.7), (0.0, 1.75)], axis, o, 16), None, 51))
        for i in range(10):                                                                          # L3 stator vanes
            rd = RD(18.0 + 36.0 * i)
            vane = f.plate("vane", [(0.85, 0.85), (1.55, 0.85), (1.4, rr * 0.83), (0.8, rr * 0.83)], 0.16,
                           normal=tuple(axis.cross(rd)), origin=tuple(o), right=tuple(axis))
            parts.append(piece(vane, H, 51))
        sN0, sN1 = 18.5, ln - 0.3
        for i in range(12):                                                                          # L3 the iris petals
            rd = RD(15.0 + 30.0 * i)
            p0 = o + axis * sN0 + rd * (rr * 1.08 + 0.2)
            p1 = o + axis * sN1 + rd * (rr * 1.22 + 0.2)
            rt = (p1 - p0).normalized()
            td = axis.cross(rd).normalized()
            L = (p1 - p0).length
            pet = f.plate("petal", [(-L / 2, -1.0), (L / 2, -1.25), (L / 2, 1.25), (-L / 2, 1.0)], 0.26,
                          normal=tuple(rt.cross(td)), origin=tuple(0.5 * (p0 + p1)), right=tuple(rt))
            parts.append(piece(pet, H, 52, bevel=0.03))
        for adeg in JACKS:                                                                           # L3 the iris actuators
            rd = RD(adeg)
            a0 = o + axis * 15.0 + rd * (RCO + 0.32)
            a1 = o + axis * (0.5 * (sN0 + sN1)) + rd * (rr * 1.15 + 0.52)
            mid = a0.lerp(a1, 0.55)
            parts.append(piece(f.tube_along("jack", [tuple(a0), tuple(mid)], [0.28, 0.28], segments=8), H, 53))
            parts.append(piece(f.tube_along("rod", [tuple(mid), tuple(a1)], [0.15, 0.15], segments=8), D, 53))
        for adeg in STRAKES:                                                                         # L3 strakes, edged in domain
            rd = RD(adeg)
            nrm = axis.cross(rd)
            st = f.plate("strake", [(12.1, R2 - 0.1), (16.3, R2 - 0.1), (15.7, R2 + 0.85), (12.7, R2 + 0.95)], 0.3,
                         normal=tuple(nrm), origin=tuple(o), right=tuple(axis))
            f.ensure_slots(st, SLOTS)
            f.paint(st, H)
            f.paint(st, D, lambda c, n, i, nrm=nrm: RR(c) > R2 + 0.8 and abs(n.dot(nrm)) < 0.5)
            parts.append(piece(st, None, 54, bevel=0.03))
        for adeg in PIPES:                                                                           # L3 conduits on clamps
            parts.append(piece(f.tube_along("pipe", [tuple(P(8.7, R2 + 0.5, adeg)), tuple(P(16.3, R2 + 0.5, adeg))], [0.22, 0.22], segments=8), H, 55))
            for s_ in (9.6, 13.2, 16.0):
                M, _, _, _ = kit.frame_matrix(P(s_, R2 + 0.3, adeg), axis, RD(adeg))
                parts.append(kit.boxm("clamp", (0.6, 0.45, 0.7), (0, 0, 0), M, H, 55))
        top = [P(s_, r_, 90.0) for s_, r_ in ((8.7, R2 + 0.07), (10.5, R2 + 0.07), (10.75, RCO + 0.07), (11.85, RCO + 0.07),
                                              (12.1, R2 + 0.07), (13.2, R2 + 0.07))]
        jog = [P(13.85, R2 + 0.07, 99.0)]
        parts.append(strip("jtrace", top + jog, 0.3, 0.08, D, 56))                                  # L4 the contour trace
        parts.append(disc("jterm", P(14.15, R2 + 0.03, 99.0), RD(99.0), 0.32, 0.1, D, 56, ring=True))
        for i, s_ in enumerate((12.4, 12.9, 13.4)):                                                  # L5 a dot row
            parts.append(disc("jdot", P(s_, R2 + 0.03, 106.0), RD(106.0), 0.16, 0.08, W, 56, segments=8))
        for s_, skip in ((11.3, STRAKES + PIPES + (90.0,)), (15.0, STRAKES + PIPES + JACKS)):        # L5 bolt heads
            for i in range(12):
                adeg = 30.0 * i
                if not near(adeg, skip, 9.0):
                    parts.append(disc("jbolt", P(s_, RCO, adeg), RD(adeg), 0.15, 0.1, H, 57, segments=6))
        for adeg in (0.0, 180.0):                                                                    # L5 running lights
            parts.append(piece(f.sphere("jlight", 0.22, center=tuple(P(1.2, R1 + 0.12, adeg)), segments=8, rings=6), J, 57))
        bone_part(parts, name)

    def gimbal(name, piv, axis, ring_r):
        """The gimbal ring in five levels. L1: the octagonal ring, a flat at the top. L2: its two trunnion bearings and
        the yaw pivot plates. L3: four yaw jacks on its diagonal flats. L4: panel seams on its outer face, neon chamfer
        lines aft. L5: bolt circles on the bearings, a light on each cap."""
        u = axis.cross(Vector((0, 0, 1))).normalized()
        v = Vector((0, 0, 1))
        flat = ring_r * math.cos(math.radians(22.5))
        sec = chamfer_sec(0.7, 0.7, 0.28)
        bm, fl = ring_loft("gring", piv, axis, u, v, ring_r, sec)
        ns = len(sec)
        for k in range(8):
            fl[k * ns + 3].material_index = D                                                       # L4 the neon chamfer aft
            kit.inset(bm, [fl[k * ns + 4]], 0.16, 0.07)                                              # L4 the outer face's panels
        parts = [piece(finish_bm("gring", bm), None, 58, bevel=0.03)]
        for s_ in (1, -1):                                                                           # L2 the trunnion bearings
            ax_ = u * s_
            org = piv + ax_ * (flat + 0.6)
            br = lathe_ob("bearing", [(0.0, 0.0), (1.55, 0.0), (1.55, 0.35), (1.2, 0.55), (1.2, 1.25), (0.95, 1.4), (0.95, 1.7), (0.0, 1.7)],
                          ax_, org, 24)                  # (short: the grille's bars stand beside it)
            f.paint(br, D, lambda c, n, i, org=org, ax_=ax_: 1.28 < (c - org).dot(ax_) < 1.37)
            parts.append(piece(br, None, 59, bevel=0.03))
            w_ = ax_.cross(v).normalized()
            for i in range(8):                                                                       # L5 its bolt circle
                a = 2 * math.pi * (i + 0.5) / 8
                parts.append(disc("gbolt", org + ax_ * 0.35 + (v * math.cos(a) + w_ * math.sin(a)) * 1.38, ax_, 0.13, 0.09, H, 59, segments=6))
            parts.append(piece(f.sphere("glight", 0.22, center=tuple(org + ax_ * 1.78), segments=8, rings=6), J, 59))
        for s_ in (1, -1):                                                                           # L2 the yaw pivot plates
            c0 = piv + v * s_ * (flat + 0.7)
            parts.append(disc("yplate", c0, v * s_, 1.1, 0.32, H, 60, segments=16))
            parts.append(disc("ycap", c0 + v * s_ * 0.32, v * s_, 0.5, 0.06, D, 60, segments=16))
        for adeg in (45.0, 135.0, 225.0, 315.0):                                                     # L3 the yaw jacks
            rd = u * math.cos(math.radians(adeg)) + v * math.sin(math.radians(adeg))
            t = axis.cross(rd).normalized()
            c0 = piv + rd * (flat + 0.7 + 0.42)
            parts.append(piece(f.tube_along("yjack", [tuple(c0 - t * 1.3), tuple(c0 + t * 0.3)], [0.42, 0.42], segments=12), H, 61))
            parts.append(piece(f.tube_along("yrod", [tuple(c0 + t * 0.3), tuple(c0 + t * 1.5)], [0.18, 0.18], segments=8), D, 61))
        bone_part(parts, name + ".ring")
    ln = eng_len()
    eng = {}
    ring_r = (JET_R + RING_CLEAR + 0.7) / math.cos(math.radians(22.5))
    for k, pm in enumerate(MIDS):
        d = fan_dir(pm)
        axis = Vector((d[0], d[1], 0.0))
        xr, yr = at(pm, RING_RHO)
        zc = 0.5 * (dome(xr, yr) + tile_lift(RING_RHO) - PLATE_T + core_top(xr, yr))
        piv = Vector((xr, yr, zc))
        o = piv - axis * RING_S
        nm = "engine.%d" % k
        jet(nm, o, axis, ln, JET_R)
        gimbal(nm, piv, axis, ring_r)
        eng[nm] = (o, axis, ln, piv)

    # ================================================================== L3 THE OMNI REACTOR in the umbo's well
    # L1 the crystal in its cage; L2 a stepped collar, an octagonal hoop, four struts; L3 four emitters aimed at the
    # crystal, the struts' knuckles; L4 the collar's panel seams and neon band; L5 its bolt circle, the hoop's lights.
    collar_o = Vector((wx, wy, floor_z(wx, wy) + 1.5))
    Zv = Vector((0, 0, 1))
    collar = lathe_ob("collar", [(0.0, 0.0), (6.2, 0.0), (7.2, 0.6), (7.2, 1.5), (6.4, 1.9), (6.4, 2.6), (5.6, 3.0), (3.4, 3.2), (0.0, 3.2)],
                      Zv, collar_o, 32)
    f.paint(collar, J, lambda c, n, i: n.z > 0.8 and c.z > collar_o.z + 3.15)
    f.paint(collar, D, lambda c, n, i: collar_o.z + 1.6 < c.z < collar_o.z + 1.8 and n.z > 0.2)
    inset_where(collar, lambda c, n: collar_o.z + 0.65 < c.z < collar_o.z + 1.45 and abs(n.z) < 0.3, 0.12, 0.08)
    OMNI = collar_o + Vector((0, 0, 15.0))
    rparts = [piece(collar, None, 60, bevel=0.04)]
    for i in range(12):
        a = 2 * math.pi * (i + 0.5) / 12
        rparts.append(disc("rbolt", collar_o + Vector((4.6 * math.cos(a), 4.6 * math.sin(a), 3.08)), Zv, 0.18, 0.1, H, 60, segments=6))
    hbm, hfl = ring_loft("hoop", OMNI, Zv, Vector((1, 0, 0)), Vector((0, 1, 0)), 10.0, chamfer_sec(0.55, 0.55, 0.2))
    for k in range(8):
        hfl[k * 8 + 0].material_index = D                                                            # the hoop glows inward
    rparts.append(piece(finish_bm("hoop", hbm), None, 61, bevel=0.03))
    for a in range(4):                                   # four struts from the collar's rim up to the hoop, knuckled
        ang = 2 * math.pi * a / 4 + math.pi / 8
        rd = Vector((math.cos(ang), math.sin(ang), 0.0))
        p0, p1 = collar_o + rd * 6.4 + Vector((0, 0, 1.8)), OMNI + rd * 9.6 - Vector((0, 0, 0.6))
        km = p0.lerp(p1, 0.45)
        rparts.append(piece(f.tube_along("strut", [tuple(p0), tuple(km)], [0.55, 0.5], segments=8), H, 61))
        rparts.append(piece(f.tube_along("strut", [tuple(km), tuple(p1)], [0.38, 0.34], segments=8), H, 61))
        rparts.append(piece(f.sphere("knuckle", 0.7, center=tuple(km), segments=12, rings=8), D, 61))
        ea = ang + math.pi / 4                                                                       # L3 an emitter between struts
        er = Vector((math.cos(ea), math.sin(ea), 0.0))
        em = lathe_ob("emitter", [(0.0, 0.0), (0.75, 0.0), (0.75, 0.7), (0.5, 1.3), (0.32, 1.75), (0.0, 1.85)], -er, OMNI + er * 9.6, 16)
        f.paint(em, J, lambda c, n, i, er=er: (OMNI + er * 9.6 - c).dot(er) > 1.45)
        rparts.append(piece(em, None, 62))
        rparts.append(piece(f.sphere("hlight", 0.28, center=tuple(OMNI + er * 10.0 + Vector((0, 0, 0.75))), segments=8, rings=6), J, 62))
    bone_part(rparts, "omni.ring")
    crys = f.lathe("omni", [(0.0, -10.0), (5.2, -1.6), (6.0, 0.0), (5.2, 1.6), (0.0, 10.0)], segments=6,
                   axis=(0, 0, 1), origin=tuple(OMNI))
    f.set_smooth(crys, False)
    bone_part([piece(crys, W, 63)], "omni")

    # ================================================================== L3 THE KEEL FIN under the spine's mid section
    # L1 the swept blade; L2 its root fairing and the sensor pod along its deepest edge; L3 three stiffener ribs;
    # L4 panels between the ribs and the neon band; L5 lights fore and aft.
    KY0, KY1 = -42.0, 22.0
    krings = []
    NK = 24

    def keel_top(y):
        return spine_at(y)[1] - 1.6

    def keel_depth(a):
        return 9.0 * _smooth(0.0, 0.35, a) * (1.0 - 0.55 * _smooth(0.7, 1.0, a)) + 0.6
    for i in range(NK + 1):
        a = i / NK
        y = KY0 + (KY1 - KY0) * a
        zt = keel_top(y)
        dd = keel_depth(a)
        hw = 1.2
        fd = min(0.8, 0.4 * dd)                          # the root fairing's flare
        krings.append([Vector((hw * 1.3, y, zt)), Vector((hw, y, zt - fd)), Vector((hw, y, zt - dd * 0.55)), Vector((hw * 0.55, y, zt - dd * 0.62)),
                       Vector((0.0, y, zt - dd)), Vector((-hw * 0.55, y, zt - dd * 0.62)), Vector((-hw, y, zt - dd * 0.55)),
                       Vector((-hw, y, zt - fd)), Vector((-hw * 1.3, y, zt))])
    kbm, kg, kc = loft_bm(krings)
    RIBS = (7, 12, 17)
    for i, row in enumerate(kg):
        if 6 <= i <= 17:
            row[2].material_index = D
            row[5].material_index = D
    for i in range(3, NK - 3):                                                                       # L4 panels between the ribs
        if i not in RIBS and i - 1 not in RIBS and i % 2 == 1:
            for j in (1, 6):
                inset(kbm, [kg[i][j]], 0.12, 0.08)
    keel_parts = [piece(finish_bm("keel", kbm), None, 70, bevel=0.06, angle=30)]
    for i in RIBS:                                                                                   # L3 the stiffener ribs
        a = (i + 0.5) / NK
        y = KY0 + (KY1 - KY0) * a
        zt, dd = keel_top(y), keel_depth(a)
        rib = f.plate("krib", [(-1.75, zt - 0.4), (1.75, zt - 0.4), (1.0, zt - dd * 0.7), (-1.0, zt - dd * 0.7)], 0.5,
                      normal=(0, -1, 0), origin=(0.0, y, 0.0), right=(1, 0, 0))
        keel_parts.append(piece(rib, H, 71, bevel=0.04))
    a0, a1 = 0.36, 0.7                                                                               # L2 the sensor pod
    yp0, yp1 = KY0 + (KY1 - KY0) * a0, KY0 + (KY1 - KY0) * a1
    zp = min(keel_top(y) - keel_depth((y - KY0) / (KY1 - KY0)) for y in (yp0, yp1, 0.5 * (yp0 + yp1))) + 0.25
    pod = lathe_ob("kpod", [(0.0, 0.0), (0.6, 0.6), (0.95, 2.0), (0.95, 4.0), (1.08, 4.15), (1.08, 4.55), (0.95, 4.7),
                            (0.95, yp1 - yp0 - 3.0), (0.7, yp1 - yp0 - 1.0), (0.0, yp1 - yp0)],
                   Vector((0, 1, 0)), Vector((0.0, yp0, zp)), 16)
    f.paint(pod, D, lambda c, n, i: yp0 + 4.2 < c.y < yp0 + 4.5 and math.hypot(c.x, c.z - zp) > 1.0)
    keel_parts.append(piece(pod, None, 72, bevel=0.03))
    for y in (yp0 - 0.4, yp1 + 0.4):                                                                 # L5 lights fore and aft
        keel_parts.append(piece(f.sphere("klight", 0.3, center=(0.0, y, zp), segments=8, rings=6), J, 72))
    bone_part(keel_parts, "keel")

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
    bones.append(B("umbo", (0.0, HY, top_z), (0.0, HY - 20.0, top_z), parent="hull"))
    for sx, s in ((1, "L"), (-1, "R")):
        root = jaw_info[sx]
        bones.append(B("auricle." + s, tuple(root), tuple(root + Vector((0, -34.0, 0))), parent="hull"))
    bones.append(B("omni.ring", tuple(collar_o), tuple(OMNI), parent="hull"))
    bones.append(B("omni", tuple(OMNI), tuple(OMNI + Vector((0, 0, 10.0))), parent="omni.ring"))
    bones.append(B("payload.dock", tuple(dock_head), tuple(dock_head + pax * 6.0), parent="hull"))
    bones.append(B("payload", tuple(pay_mid), tuple(p_tip), parent="payload.dock"))
    for nm, (o, axis, ln_, piv) in eng.items():
        bones.append(B(nm + ".ring", tuple(piv), tuple(piv + Vector((0, 0, 4.0))), parent="hull"))
        bones.append(B(nm, tuple(piv), tuple(piv + axis * (ln_ - RING_S)), parent=nm + ".ring"))
    bones.append(B("keel", (0, KY0, keel_top(KY0)), (0, KY1, keel_top(KY1)), parent="hull"))
    bones.append(B("hangar.door.T", tuple(door_hinge["T"]), tuple(door_hinge["T"] - Vector((0, 0, 4.0))), parent="hull"))
    bones.append(B("hangar.door.B", tuple(door_hinge["B"]), tuple(door_hinge["B"] + Vector((0, 0, 4.0))), parent="hull"))
    for i, p in enumerate(slots):
        bones.append(B("hangar.slot.%d" % i, tuple(p), tuple(p + Vector((0, -6.0, 0))), parent="hull"))
    for b in kit.socket_bone_specs(B):
        if b.name in jaw_socket:
            b.parent = jaw_socket[b.name]
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
        return Vector((0, 0, min(co.z - keel_top(co.y), 0.0) * 1.2))

    def fatten(i, co, n):
        for e, (o, axis, ln_, piv) in eng.items():
            if m[e][i] > 0:
                d = co - o
                return (d - axis * d.dot(axis)) * 0.2
        return Vector((0, 0, 0))

    def reach(i, co, n):
        for sx, side in ((1, "auricle.L"), (-1, "auricle.R")):
            if m[side][i] > 0:
                if co.y < -90.0:                       # past the blades: the knuckle reaches out and forward
                    return Vector((sx * 0.6, -1.0, 0.0)) * (-90.0 - co.y) * 1.4
                return Vector((0, 0, 0))
        if m["umbo"][i] > 0 and co.z > top_z + 0.5:   # the sensor array rises
            return Vector((0, 0, (co.z - top_z - 0.5) * 1.2))
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
    right = [e for e in jets if eng[e][0].x < 0]
    # THE WINGS move with the jets. The jaws are the scallop's auricles (its "wings"): they pitch as canards, together
    # for pitch and against each other for roll (a canard's tip up lifts its side). The outer shell plates are spoilers:
    # a plate twists about its own centreline so its OUTER edge lifts on the inside of a roll or a yaw (a raised spoiler
    # drops that side and drags it round). They only ever lift: the bulwark is a shadow seam (1.8) below the outer edge.
    def spoiler(k, deg):
        b = arm.data.bones["shell.%d" % k]
        ax_ = (b.tail_local - b.head_local).normalized()
        sgn = 1.0 if MIDS[k] > 0 else -1.0
        outward = Vector((*fan_perp(MIDS[k]), 0.0)) * sgn
        a = math.radians(deg)
        if (Matrix.Rotation(a, 3, ax_) @ outward).z < 0:
            a = -a
        return tuple(math.degrees(v) for v in Matrix.Rotation(a, 3, ax_).to_euler("XYZ"))
    SPOIL = {0: 6.0, 1: 4.5, NPLATE - 1: 6.0, NPLATE - 2: 4.5}
    right_sp = {"shell.%d" % k: spoiler(k, SPOIL[k]) for k in (0, 1)}
    left_sp = {"shell.%d" % k: spoiler(k, SPOIL[k]) for k in (NPLATE - 1, NPLATE - 2)}
    JAWP = 7.0                                           # a canard's tip up = -X (+X turns a point AHEAD of the pivot down)
    poses = {
        "pitchup": {**{e: (PITCH, 0, 0) for e in jets}, "auricle.L": (-JAWP, 0, 0), "auricle.R": (-JAWP, 0, 0)},
        "pitchdown": {**{e: (-PITCH, 0, 0) for e in jets}, "auricle.L": (JAWP, 0, 0), "auricle.R": (JAWP, 0, 0)},
        "yawright": {**{e + ".ring": (0, 0, YAW) for e in jets}, "shell.0": right_sp["shell.0"]},
        "yawleft": {**{e + ".ring": (0, 0, -YAW) for e in jets}, "shell.%d" % (NPLATE - 1): left_sp["shell.%d" % (NPLATE - 1)]},
        "rollright": {**{e: ((PITCH if e in right else -PITCH), 0, 0) for e in jets}, **right_sp,
                      "auricle.L": (-JAWP, 0, 0), "auricle.R": (JAWP, 0, 0)},
        "rollleft": {**{e: ((-PITCH if e in right else PITCH), 0, 0) for e in jets}, **left_sp,
                     "auricle.L": (JAWP, 0, 0), "auricle.R": (-JAWP, 0, 0)},
        "speedup": {e + ".ring": (0, 0, 0.35 * MIDS[int(e.split(".")[1])]) for e in jets},
        "speeddown": {e + ".ring": (0, 0, -0.25 * MIDS[int(e.split(".")[1])]) for e in jets},
        "release": {"payload": {"rot": (0, 0, 0), "loc": tuple(pax * 20.0)}},
        "hangaropen": {"hangar.door.T": (-75.0, 0, 0), "hangar.door.B": (70.0, 0, 0)},
    }
    names = ("pitchup", "pitchdown", "yawleft", "yawright", "rollleft", "rollright", "speedup", "speeddown", "release", "hangaropen")
    for p in names:
        takes.pose_take(arm, p, poses[p])
    takes.clear_pose(arm)

    # ================================================================== THE RIG TABLE (the game reads it: Ark, ArkModel)
    # Unity binds the Ark's hull to the SOCKETS and puppets its jets and wings from the TAKES. Both are written here, in
    # Unity's model space (Unity x, y, z = Blender -x, z, -y; a reflection, so a rotation R becomes M R M^T), from the
    # same numbers the takes were built from - one source, and pose_clearance has proved every one of them. The file
    # lands beside the FBX: `ArkReverse.rig.json` (a TextAsset in Unity).
    Mb = Matrix(((-1.0, 0.0, 0.0), (0.0, 0.0, 1.0), (0.0, -1.0, 0.0)))

    def U(v):
        return [round(-v[0], 4), round(v[2], 4), round(-v[1], 4)]

    def Uq(rot_deg):
        q = (Mb @ Euler([math.radians(a) for a in rot_deg], "XYZ").to_matrix() @ Mb.transposed()).to_quaternion()
        return [round(q.x, 6), round(q.y, 6), round(q.z, 6), round(q.w, 6)]
    from mathutils import Euler
    TIER = {"supershielded": ("SuperShielded", [2 * STELLA_A] * 3), "shielded": ("Shielded", [2 * OCTA_A] * 3),
            "prism": ("Plain", list(SLAB)), "danger": ("Danger", list(SLAB))}
    table = {"model": NAME, "frame": "Unity model space: x right (pilot's right), y up, z nose; units = Blender metres",
             "noseToJets": NOSE_TO_JETS, "sockets": [], "bones": []}
    for nm_, head, tail, up in kit.socket_bones:
        tier = nm_.split(".")[1]
        fwd = (tail - head).normalized()
        upn = (up - fwd * up.dot(fwd)).normalized()
        kind, size = TIER[tier]
        table["sockets"].append({"name": nm_, "tier": tier, "kind": kind, "position": U(head), "forward": U(fwd),
                                 "up": U(upn), "size": [round(v, 4) for v in size], "bone": jaw_socket.get(nm_, "hull")})
    INPUTS = (("pitchUp", "pitchup"), ("pitchDown", "pitchdown"), ("yawRight", "yawright"), ("yawLeft", "yawleft"),
              ("rollRight", "rollright"), ("rollLeft", "rollleft"), ("speedUp", "speedup"), ("speedDown", "speeddown"))
    moved = {}
    for field, take in INPUTS:
        for b_, spec_ in poses[take].items():
            rot = spec_.get("rot", (0, 0, 0)) if isinstance(spec_, dict) else spec_
            moved.setdefault(b_, {})[field] = Uq(rot)
    for b_ in sorted(moved):
        entry = {"bone": b_, "head": U(arm.data.bones[b_].head_local)}
        entry.update(moved[b_])
        table["bones"].append(entry)
    import json
    rig_path = os.path.join(ctx.out_dir, NAME + ".rig.json")
    with open(rig_path, "w") as fh:
        json.dump(table, fh, indent=1, sort_keys=True)
        fh.write("\n")
    ctx.log("rig table: %d sockets, %d puppeted bones -> %s" % (len(table["sockets"]), len(table["bones"]), os.path.basename(rig_path)))

    lo, hi = f.bounds(mesh)
    ctx.log("bounds (blender) %s .. %s; nose to jets %.1f (want %.1f), jets %.1f long" % (
        tuple(round(v, 1) for v in lo), tuple(round(v, 1) for v in hi), jets_y() - lo[1], NOSE_TO_JETS, ln))
    assert abs(lo[1] - NOSE_Y) < 0.5, "something reaches ahead of the jaws' knuckles: the nose moved"
    assert abs((jets_y() - lo[1]) - NOSE_TO_JETS) < 0.02 * NOSE_TO_JETS, "nose to jets drifted off half a nucleus radius"
    return {
        "armature": arm, "mesh": mesh,
        "spec": {
            "preset": "zup", "armature": ARMATURE, "mesh_node": MESH_NODE,
            "bones": bone_names,
            "takes": [ARMATURE + "|" + p for p in names],
            "pose_takes": [ARMATURE + "|" + p for p in names],
            "sheet_poses": ("pitchup", "yawright", "rollright", "hangaropen"),
            "pose_expect": {
                "pitchup": [("engine.3", "z", +1, 0.6), ("auricle.L", "z", +1, 0.6), ("auricle.R", "z", +1, 0.6)],
                "pitchdown": [("engine.3", "z", -1, 0.6), ("auricle.L", "z", -1, 0.6), ("auricle.R", "z", -1, 0.6)],
                "yawright": [("engine.3", "x", -1, 0.6)],
                "yawleft": [("engine.3", "x", +1, 0.6)],
                "rollright": [("engine.0", "z", +1, 0.6), ("engine.7", "z", -1, 0.6), ("auricle.L", "z", +1, 0.6), ("auricle.R", "z", -1, 0.6)],
                "rollleft": [("engine.0", "z", -1, 0.6), ("engine.7", "z", +1, 0.6), ("auricle.L", "z", -1, 0.6), ("auricle.R", "z", +1, 0.6)],
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
