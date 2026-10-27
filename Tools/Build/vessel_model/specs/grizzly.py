"""
grizzly - a NEW hull with no artist file: it replaces the generic wedge the Grizzly has flown since it was made
(`Vessel_Wedge_Scene (4).fbx`: eight static part meshes, no armature, no element shapes, a jet pair wearing Lime,
and a file shared with `TimeDandruff` and `ExplodableProjectile`, so it is left untouched) with a model built to the
fleet convention and the fleet's look. Garrett, 2026-10-27: "make a new much more awesome model for the Grizzly."
A spaceship inspired by a grizzly bear (Ursus arctos horribilis), never the animal.

THE READ: the Grizzly's camera sits straight BEHIND it (GrizzlyCameraSettingsSO followOffset (0, 0, -60), 30-100
dynamic), so the hull is mostly seen from the rear: it must say "bear" from behind and above by its outline alone -
the shoulder HUMP rising over everything, four heavy haunch engines below it, forelimbs reaching out ahead with their
claws, round ears over a long snout from the side. It is the fleet's brawler and artillery piece (charged cannon,
sniper scope, trigger bombs, plasma claws, Rush, Dig In), so it reads heavy and armoured: thick plates, short gaps.

Clade -> ship (every part answers "what is this on the ship?"):
  shoulder hump        -> the dorsal REACTOR SHELL (bone `hump`, which follows the prefab's `TopWing`): a thick armour
                          dome floating over the shoulders, a domain saddle down its ridge, panel seams on its flanks
  head                 -> the COMMAND MODULE (bone `head`), floating ahead of the torso across a neck gap: a raised
                          brow over a long snout. The brow carries the sunk cockpit canopy; the eyes are two small
                          sensor windows at the stop
  snout                -> the CANNON BARREL: the charged shot and the sniper leave along it; a domain blaze runs down
                          its ridge and its tip is the ember-lit bore
  jaw                  -> a hinged lower MANDIBLE (bone `jaw`) under the barrel, two fang-like heat sinks at its lip
  round ears           -> two SENSOR DISHES (bones `ear.L` / `ear.R`): a domain rim round a glass lens
  forelimbs            -> shoulder PAULDRONS (bones `shoulder.L` / `shoulder.R`), curved armour floating on the torso's
                          widest point, and the CLAW GAUNTLETS (bones `claw.L` / `claw.R`) ahead and below them: an
                          armoured paw with a domain knuckle plate and four hooked claw blades whose hot tips glow
                          (Engine: danger) - where the plasma claws come from
  hind legs / haunches -> the four THRUSTER PODS (bones `jet.T.L`, `jet.T.R`, `jet.B.L`, `jet.B.R`, which follow the
                          prefab's ThrusterTopLeft ... ThrusterBottomRight): armoured nacelles with intake rings, a domain
                          collar and an ember mouth exactly where the prefab's four VesselJets sit
  belly / digging      -> the DIG-IN ANCHOR (bone `keel`, which follows the prefab's `BottomWing`): a three-tined spade
                          floating under the torso
  rump                 -> the torso's stern: a domain rim round a sunk ember reactor port between the four engines

A HOVERING ASSEMBLY (vessel-model section 0.5): the torso (`hull`) plus 14 parts, each its own closed shell on its own
bone, floating clear; the gate is `assembly_report.py Grizzly.fbx --floating <FLOATING>`.

Contract (FLEET_CONVENTION sections 1-4 for a NEW vessel):
  file      Assets/_Models/Vessel Models/Grizzly.fbx (new guid), preset "zup"
  frame     Blender: nose -Y, up +Z, pilot's left +X.  Unity (x, y, z) = Blender (-x, z, -y).
  nodes     mesh `mesh`, armature `rig`; root bone `hull`
  bones     hull; head -> jaw, ear.L, ear.R; hump; keel; shoulder.L/.R; claw.L/.R; jet.T.L/.T.R/.B.L/.B.R.
            Rigid skinning, one bone per part.
  anchors   the prefab's wedge parts all pivot at the hull's origin (Grizzly/OrientationHandle/Ship_Wedge_Body, the
            fins at Unity z -1); its four VesselJets sit at Unity (+-2.725, +-1.461, -3.81) = Blender (-+2.725,
            3.81, +-1.461), so each pod's mouth is there; the cannon's Gun at Unity (0, 0, 3.5) is inside the barrel.
  size      the wedge's envelope (Unity x +-3.5, y +-5.5 with its two tall fins, z -3.8..7.3): the length is kept to
            5 % (snout tip to the jet mouths, 10.6 against 11.1); it is wider (the claws) and lower (no tall fins),
            declared in size_tol. The prefab's colliders are left as they are.
  slots     0 Domain, 1 Body, 2 Window, 3 Engine
  shapes    Charge (weapon): the claw blades lengthen and the gauntlets swell and reach forward;
            Mass (volume):   the torso and the hump bulk out, the pauldrons, gauntlets and pods ride out with them;
            Space (reach):   the barrel telescopes forward (the sniper's reach), the ear dishes rise;
            Time (rate):     the four thruster pods stretch aft and their bells flare, the hump sweeps back
  takes     rest (first); roar (the jaw drops, the head lifts); swipe (the right claw rakes down and in);
            dig (the anchor rakes forward, both claws dig down). Single-frame poses, no blend-shape curves.
            BufoAnimation drives the hull in code; these document the parts' hinges for the sheet and the studio.
"""

import math
from _common import box_uv

NAME = "Grizzly"
OUTPUT = "Assets/_Models/Vessel Models/Grizzly.fbx"

ARMATURE = "rig"
MESH_NODE = "mesh"
SLOTS = ("Domain", "Body", "Window", "Engine")
D, H, W, J = 0, 1, 2, 3

# ---------------------------------------------------------------- layout (Blender frame: nose -Y, up +Z, left +X)
# the torso: (y, half-width, half-height top, half-height bottom, centre lift)
TORSO = [
    (-2.30, 1.20, 0.98, 0.92, 0.04),
    (-2.05, 1.70, 1.22, 1.08, 0.04),
    (-1.55, 1.98, 1.36, 1.18, 0.02),
    (-0.80, 2.00, 1.30, 1.16, 0.00),
    (0.00, 1.84, 1.16, 1.08, -0.02),
    (0.80, 1.55, 1.04, 0.98, -0.04),
    (1.50, 1.20, 0.84, 0.78, -0.04),
    (2.10, 0.74, 0.50, 0.48, -0.04),
]
TORSO_NT, TORSO_NB = 3.0, 3.2

# the head module (y, half-width, top, bottom, lift), snout tip first
HEAD = [
    (-6.80, 0.52, 0.46, 0.40, -0.14),        # a deep, blunt muzzle, not a long flat one
    (-6.66, 0.68, 0.64, 0.54, -0.12),
    (-6.20, 0.80, 0.74, 0.62, -0.08),
    (-5.55, 0.92, 0.84, 0.66, -0.02),
    (-5.00, 1.24, 1.16, 0.80, 0.18),        # the stop: the brow rises steeply off the snout
    (-4.35, 1.56, 1.42, 0.92, 0.34),
    (-3.50, 1.60, 1.36, 0.94, 0.32),
    (-2.65, 1.24, 1.04, 0.86, 0.22),
]
HEAD_NT, HEAD_NB = 2.6, 3.0
NECK_GAP = HEAD[-1][0] - TORSO[0][0]       # -0.30: the head floats 0.30 ahead of the torso

JET_X, JET_Z = 2.725, 1.461                # the prefab's VesselJets: Unity (+-2.725, +-1.461, -3.81)
JET_MOUTH_Y = 3.81
JET_FRONT_Y = -0.20
JET_R = 0.68                               # the bell; the haunch ahead of it is wider

GAUNTLET = (2.95, -3.55, -0.55)             # the paw's centre (left side; the right mirrors it)
HUMP_BAND = (50.0, 130.0)                  # degrees round the torso (from +X, counter-clockwise in x-z)
PAULDRON_BAND = (-42.0, 44.0)
PAULDRON_CAP = 30.0                         # the plate below, the domain cap above (a hairline seam between)


def catmull_1d(tab, y, k):
    """Catmull-Rom through the table's column k at y (clamped), so lofted outlines carry no creases at the knots."""
    ys = [r[0] for r in tab]
    if y <= ys[0]:
        return tab[0][k]
    if y >= ys[-1]:
        return tab[-1][k]
    i = max(j for j in range(len(ys) - 1) if ys[j] <= y)
    p1, p2 = tab[i][k], tab[i + 1][k]
    p0 = tab[i - 1][k] if i > 0 else 2 * p1 - p2
    p3 = tab[i + 2][k] if i + 2 < len(tab) else 2 * p2 - p1
    t = (y - ys[i]) / (ys[i + 1] - ys[i])
    t2, t3 = t * t, t * t * t
    return 0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3)


def outline(tab, y):
    return tuple(catmull_1d(tab, y, k) for k in (1, 2, 3, 4))


def se_pt(w, ht, hb, a, nt, nb):
    """The superellipse point at angle a (radians, from +X counter-clockwise in x-z), centred on the origin."""
    c, s = math.cos(a), math.sin(a)
    hh, nn = (ht, nt) if s >= 0 else (hb, nb)
    return (w * math.copysign(abs(c) ** (2.0 / nn), c), hh * math.copysign(abs(s) ** (2.0 / nn), s))


def body_pt(tab, y, a, nt, nb):
    w, ht, hb, dz = outline(tab, y)
    x, z = se_pt(w, ht, hb, a, nt, nb)
    return x, z + dz


def body_normal(tab, y, a, nt, nb):
    """The outline's outward normal in x-z at angle a (numerically; the outline runs counter-clockwise)."""
    e = 1e-3
    x0, z0 = body_pt(tab, y, a - e, nt, nb)
    x1, z1 = body_pt(tab, y, a + e, nt, nb)
    tx, tz = x1 - x0, z1 - z0
    ln = math.hypot(tx, tz) or 1.0
    return tz / ln, -tx / ln


def torso_top(y, x=0.0):
    w, ht, hb, dz = outline(TORSO, y)
    u = min(0.999, abs(x) / w)
    return dz + ht * (1 - u ** TORSO_NT) ** (1.0 / TORSO_NT)


def torso_bottom(y):
    w, ht, hb, dz = outline(TORSO, y)
    return dz - hb


def head_bottom(y):
    w, ht, hb, dz = outline(HEAD, y)
    return dz - hb


def head_top(y, x=0.0):
    w, ht, hb, dz = outline(HEAD, y)
    u = min(0.999, abs(x) / w)
    return dz + ht * (1 - u ** HEAD_NT) ** (1.0 / HEAD_NT)


def hump_height(y):
    """The reactor shell's thickness at its ridge along y: a rounded mound peaking over the withers (y ~ -1.1)."""
    u = max(0.0, min(1.0, (y + 2.20) / 2.95))
    return 0.22 + 1.30 * math.sin(math.pi * u ** 0.72) ** 0.8


def build(ctx):
    import bpy  # noqa: F401
    import bmesh
    from mathutils import Vector, Matrix
    f, rig, morphs, takes = ctx.forms, ctx.rig, ctx.morphs, ctx.takes
    parts = []

    def tag(obj, pid):
        me = obj.data
        a = me.attributes.get("pid") or me.attributes.new("pid", "FLOAT", "POINT")
        a.data.foreach_set("value", [float(pid)] * len(me.vertices))
        return obj

    def piece(obj, slot, pid, bevel=0.0, angle=30):
        f.ensure_slots(obj, SLOTS)
        if slot is not None:
            f.paint(obj, slot)
        if bevel > 0:
            f.bevel(obj, width=bevel, segments=1, angle_deg=angle)
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
        ctx.log("part %-11s %6d tris" % (name, f.tri_count(o)))
        return o

    def offset_shell(name, tab, nt, nb, y0, y1, a0, a1, gap, thick, n=16, stations=16, cx=0.0, cz=0.0):
        """An armour shell floating `gap` off a body's surface over the band a0..a1 (degrees), y0..y1, `thick(u, v)`
        thick, where u runs 0..1 along y and v 0..1 across the band: its inner face follows the body, so the gap is
        even everywhere. A domain trim built this way is a CONFORMAL plate: its edges run along the body's own lines,
        never the stair a face-painted band leaves (the Orca's lesson)."""
        sts = []
        for k in range(stations + 1):
            u = k / stations
            y = y0 + (y1 - y0) * u
            inner, outer = [], []
            for i in range(n + 1):
                v = i / n
                a = math.radians(a0 + (a1 - a0) * v)
                px, pz = body_pt(tab, y, a, nt, nb)
                nx, nz = body_normal(tab, y, a, nt, nb)
                t = thick(u, v)
                inner.append((px + nx * gap, pz + nz * gap))
                outer.append((px + nx * (gap + t), pz + nz * (gap + t)))
            sts.append(f.Station((cx, y, cz), outer + list(reversed(inner)), right=(1, 0, 0), up=(0, 0, 1)))
        return f.loft(name, sts)

    # ================================================================== THE TORSO (hull)
    y0t, y1t = TORSO[0][0], TORSO[-1][0]
    tl = y1t - y0t
    dense = []
    for k in range(20):
        y = y0t + tl * k / 19
        w, ht, hb, dz = outline(TORSO, y)
        dense.append(((y - y0t) / tl, w, ht, hb, TORSO_NT, TORSO_NB, dz))
    torso = f.fuselage("torso", tl, nose=(0, y0t, 0), axis=(0, 1, 0), segments=24, stations=dense)
    f.ensure_slots(torso, SLOTS)
    f.paint(torso, H)
    # the stern: a domain rim round a sunk ember reactor port
    f.paint(torso, D, lambda c, n, i: c.y > 1.95 and n.y > 0.6)
    f.inset_faces(torso, lambda c, n, i: c.y > 2.05 and n.y > 0.6, 0.20, depth=0.12, slot=H)
    f.inset_faces(torso, lambda c, n, i: c.y > 1.90 and n.y > 0.6 and i == H, 0.24, depth=0.06, slot=J)
    # panel seams on the flanks (three plates a side)
    for ya, yb in ((-1.45, -0.75), (-0.55, 0.35), (0.55, 1.35)):
        f.inset_faces(torso, lambda c, n, i, ya=ya, yb=yb: abs(n.x) > 0.75 and ya < c.y < yb and c.z - outline(TORSO, c.y)[3] > -0.16,
                      0.05, depth=0.035)
    piece(torso, None, 1, bevel=0.03, angle=40)
    bone_part([torso], "hull")

    # ================================================================== THE REACTOR SHELL (hump)
    def hump_thick(u, v):
        y = -2.20 + 2.95 * u
        bump = math.sin(math.pi * v) ** 1.4
        return 0.16 + (hump_height(y) - 0.16) * bump
    hump = offset_shell("hump", TORSO, TORSO_NT, TORSO_NB, -2.20, 0.75, HUMP_BAND[0], HUMP_BAND[1], 0.12, hump_thick,
                        n=10, stations=13)
    f.ensure_slots(hump, SLOTS)
    f.paint(hump, H)
    # the domain saddle: the ridge, seen from behind and above
    f.paint(hump, D, lambda c, n, i: abs(c.x) < 0.52 and n.z > 0.25)
    # vent seams down each flank of the dome
    for ya, yb in ((-1.70, -1.20), (-0.95, -0.45)):
        f.inset_faces(hump, lambda c, n, i, ya=ya, yb=yb: 0.62 < abs(c.x) < 1.30 and ya < c.y < yb and n.z > 0.1,
                      0.04, depth=0.04)
    piece(hump, None, 2, bevel=0.03, angle=40)
    bone_part([hump], "hump")

    # ================================================================== THE COMMAND MODULE (head) and its mandible (jaw)
    y0h, y1h = HEAD[0][0], HEAD[-1][0]
    hl = y1h - y0h
    hd = []
    for k in range(18):
        y = y0h + hl * k / 17
        w, ht, hb, dz = outline(HEAD, y)
        hd.append(((y - y0h) / hl, w, ht, hb, HEAD_NT, HEAD_NB, dz))
    head = f.fuselage("head", hl, nose=(0, y0h, 0), axis=(0, 1, 0), segments=24, stations=hd)
    f.ensure_slots(head, SLOTS)
    f.paint(head, H)
    # the cockpit canopy on the brow, sunk in its frame
    f.inset_faces(head, lambda c, n, i: -4.62 < c.y < -3.15 and n.z > 0.62 and abs(c.x) < 0.95, 0.07, depth=0.07, slot=W)
    # the eyes: two small sensor windows at the stop, facing forward and out
    f.inset_faces(head, lambda c, n, i: -5.20 < c.y < -4.62 and n.y < -0.25 and abs(n.x) > 0.30 and 0.30 < c.z - outline(HEAD, c.y)[3] < 0.85,
                  0.035, depth=0.05, slot=W)
    # the bore: the snout tip is the cannon's muzzle
    f.inset_faces(head, lambda c, n, i: c.y < -6.75 and n.y < -0.8, 0.14, depth=0.16, slot=J)
    # the back of the module: a domain rim (seen across the neck gap)
    f.paint(head, D, lambda c, n, i: c.y > -2.70 and n.y > 0.7)
    piece(head, None, 10, bevel=0.025, angle=40)
    # the blaze down the barrel's ridge: a conformal domain plate on a hairline seam
    blaze = offset_shell("blaze", HEAD, HEAD_NT, HEAD_NB, -6.60, -5.12, 72.0, 108.0, 0.015, lambda u, v: 0.05, n=8, stations=10)
    head = bone_part([head, piece(blaze, D, 12)], "head")
    from mathutils.kdtree import KDTree
    head_kd = KDTree(len(head.data.vertices))
    for v in head.data.vertices:
        head_kd.insert(v.co, v.index)
    head_kd.balance()

    def lift_clear(obj, clearance, step=0.02):
        """Raise a part straight up until no vertex of it lies within `clearance` of the head's: a dish tilted toward
        the brow meets it at its rim, which no single offset predicts. Returns the lift."""
        lift = 0.0
        while min(head_kd.find(v.co)[2] for v in obj.data.vertices) < clearance:
            for v in obj.data.vertices:
                v.co.z += step
            lift += step
        obj.data.update()
        return lift

    # the mandible: a lofted plate following the head's underside, a gap below it
    JAW_GAP = 0.10
    jaw_y0, jaw_y1 = -6.55, -4.15
    jst = []
    for k in range(10):
        y = jaw_y0 + (jaw_y1 - jaw_y0) * k / 9
        u = k / 9
        w = outline(HEAD, y)[0] * (0.78 - 0.10 * u)
        top = head_bottom(y) - JAW_GAP
        th = 0.20 + 0.10 * math.sin(math.pi * u)
        prof = f.superellipse(w, 0.001, n=3.0, segments=20, h_bottom=th, n_bottom=2.6)
        # the plate's top must follow the head's (rounded) underside: lift each point by the head's curvature
        prof2 = []
        for (x, z) in prof:
            hw = outline(HEAD, y)[0]
            uu = min(0.999, abs(x) / hw)
            curv = outline(HEAD, y)[2] * (1 - (1 - uu ** HEAD_NB) ** (1.0 / HEAD_NB))
            prof2.append((x, z + curv * 0.95))
        jst.append(f.Station((0, y, top), prof2, right=(1, 0, 0), up=(0, 0, 1)))
    jaw = f.loft("jaw", jst)
    f.ensure_slots(jaw, SLOTS)
    f.paint(jaw, H)
    jaw_parts = [piece(jaw, None, 11, bevel=0.02, angle=40)]
    for sx in (1, -1):
        tip_y = -6.38
        base = Vector((sx * 0.36, tip_y, head_bottom(tip_y) - JAW_GAP - 0.02))
        fang = f.cone("fang", 0.075, 0.30, axis=(0, 0, -1), origin=tuple(base), segments=10)
        f.ensure_slots(fang, SLOTS)
        f.paint(fang, H)
        jaw_parts.append(piece(fang, None, 11))
    bone_part(jaw_parts, "jaw")
    JAW_HINGE = (0.0, jaw_y1 + 0.05, head_bottom(jaw_y1) - JAW_GAP - 0.10)

    # ================================================================== SENSOR DISHES (ear.L / ear.R)
    ears = {}
    for side_, sx in (("L", 1), ("R", -1)):
        ey = -3.10
        ex = sx * 1.12
        ax = Vector((sx * 0.30, -0.48, 0.82)).normalized()
        base = Vector((ex, ey, head_top(ey, ex) + 0.10)) + ax * 0.10
        dish = f.lathe("dish", [(0.0, -0.16), (0.30, -0.16), (0.48, -0.10), (0.57, 0.0), (0.58, 0.08), (0.50, 0.13),
                                (0.38, 0.08), (0.20, 0.04), (0.0, 0.035)],
                       segments=16, axis=tuple(ax), origin=tuple(base))
        f.ensure_slots(dish, SLOTS)
        f.paint(dish, H)
        f.paint(dish, D, lambda c, n, i, base=base, ax=ax: (c - base).dot(ax) > 0.07 and ((c - base) - ax * (c - base).dot(ax)).length > 0.45)
        f.paint(dish, W, lambda c, n, i, base=base, ax=ax: (c - base).dot(ax) > 0.0 and ((c - base) - ax * (c - base).dot(ax)).length < 0.42)
        piece(dish, None, 20, bevel=0.015, angle=40)
        lift = lift_clear(dish, 0.10)
        base = base + Vector((0, 0, lift))
        ears[side_] = (base, ax)
        bone_part([dish], "ear." + side_)

    # ================================================================== PAULDRONS (shoulder.L / shoulder.R)
    def pauldron_thick(u, v):
        """Thickest across the plate's middle, back to the cap's 0.24 at its top edge (v = 1)."""
        return 0.24 + 0.30 * math.sin(math.pi * min(1.0, 0.55 + 0.45 * v) * 1.0) ** 0.8 * math.sin(math.pi * min(1.0, u * 1.10)) ** 0.5

    for side_, sx in (("L", 1), ("R", -1)):
        lo_, mid_, hi_ = PAULDRON_BAND[0], PAULDRON_CAP, PAULDRON_BAND[1]
        band = (lo_, mid_) if sx > 0 else (180.0 - mid_, 180.0 - lo_)
        cap = (mid_ + 3.0, hi_) if sx > 0 else (180.0 - hi_, 180.0 - mid_ - 3.0)
        pa = offset_shell("pauldron", TORSO, TORSO_NT, TORSO_NB, -2.05, -0.50, band[0], band[1], 0.13,
                          lambda u, v: pauldron_thick(u, v if sx > 0 else 1.0 - v), n=10, stations=10)
        f.ensure_slots(pa, SLOTS)
        f.paint(pa, H)
        f.inset_faces(pa, lambda c, n, i: -1.55 < c.y < -0.95 and -0.30 < c.z < 0.35 and sx * n.x > 0.6, 0.05, depth=0.04)
        # the domain edge along the plate's top: its own conformal plate across a hairline seam
        pc = offset_shell("pauldron_cap", TORSO, TORSO_NT, TORSO_NB, -1.98, -0.57, cap[0], cap[1], 0.13,
                          lambda u, v: 0.24, n=3, stations=8)
        bone_part([piece(pa, None, 30, bevel=0.025, angle=40), piece(pc, D, 31)], "shoulder." + side_)

    # ================================================================== CLAW GAUNTLETS (claw.L / claw.R)
    claw_tips, claw_dirs = {}, {}
    for side_, sx in (("L", 1), ("R", -1)):
        gx, gy, gz = GAUNTLET[0] * sx, GAUNTLET[1], GAUNTLET[2]
        gp = []
        # the paw: an armoured block, rounded, its front lower than its back
        pl = 1.95
        paw_tab = []
        for k in range(13):
            u = k / 12
            w = 0.74 * (0.82 + 0.22 * math.sin(math.pi * min(1.0, u * 1.2)))
            ht = 0.50 + 0.16 * math.sin(math.pi * u)
            paw_tab.append((gy - pl * 0.55 + pl * u, w, ht, 0.44, 0.10 * (u - 0.5)))
        pst = [((y - paw_tab[0][0]) / pl, w, ht, hb, 3.2, 3.6, dz) for (y, w, ht, hb, dz) in paw_tab]
        paw = f.fuselage("paw", pl, nose=(gx, gy - pl * 0.55, gz), axis=(0, 1, 0), segments=14, stations=pst)
        f.ensure_slots(paw, SLOTS)
        f.paint(paw, H)
        f.inset_faces(paw, lambda c, n, i, gy=gy: n.z > 0.55 and gy + 0.30 < c.y < gy + 0.75, 0.05, depth=0.04)
        gp.append(piece(paw, None, 40, bevel=0.025, angle=40))
        # the domain knuckle plate on the paw's back: conformal, on a hairline seam
        kn = offset_shell("knuckle", paw_tab, 3.2, 3.6, paw_tab[0][0] + 0.16, gy + 0.10, 52.0, 128.0, 0.02,
                          lambda u, v: 0.07, n=6, stations=6, cx=gx, cz=gz)
        gp.append(piece(kn, D, 42))
        # four hooked claw blades from the paw's front, fanned, curving down
        front_y = gy - pl * 0.55
        tips = []
        for j in range(4):
            off = (j - 1.5) * 0.36
            root = Vector((gx + off, front_y + 0.25, gz + 0.06))
            fan = math.radians(sx * (j - 1.5) * 6.0)
            dirv = Vector((math.sin(fan), -math.cos(fan), 0.0))
            L = 1.40 - 0.12 * abs(j - 1.5)
            pts = []
            for s in range(9):
                t = s / 8
                p = root + dirv * (L * t) + Vector((0, 0, -0.58 * L * t ** 2.2))
                pts.append(tuple(p))
            radii = [0.16 * (1.0 - 0.88 * (s / 8) ** 1.1) for s in range(9)]
            blade = f.tube_along("blade", pts, radii, segments=6)
            f.ensure_slots(blade, SLOTS)
            f.paint(blade, H)
            f.paint(blade, J, lambda c, n, i, root=root, dirv=dirv, L=L: (c - root).dot(dirv) > 0.62 * L)
            gp.append(piece(blade, None, 41))
            tips.append(Vector(pts[-1]))
        claw_tips[side_] = tips
        claw_dirs[side_] = (Vector((gx, front_y, gz)), Vector((0, -1, -0.35)).normalized())
        bone_part(gp, "claw." + side_)

    # ================================================================== THRUSTER PODS (jet.T.L ... jet.B.R)
    pod_len = JET_MOUTH_Y - JET_FRONT_Y
    r = JET_R
    L = pod_len
    pod_prof = [(0.0, 0.0), (0.36, 0.02), (0.60, 0.10), (0.74, 0.26), (0.80, 0.46),     # the rounded front (the hip)
                (0.80, 0.60), (0.75, 0.66), (0.75, 0.74), (0.81, 0.80), (0.81, 1.36),     # an intake groove, the thigh
                (0.77, 1.44), (0.72, 2.40), (0.68, 2.48),                                  # tapering to the hock
                (0.72, 2.54), (0.72, 2.98), (0.66, 3.04),                                  # the domain collar
                (0.62, 3.10), (0.62, L - 0.32), (r, L - 0.24), (r, L),                     # the bell and its lip
                (r * 0.80, L), (r * 0.72, L - 0.12), (r * 0.64, L - 0.30),                 # the dark bezel, inside
                (r * 0.34, L - 0.30), (0.0, L - 0.04)]                                     # the throat round a spike
    for vert, sz in (("T", 1), ("B", -1)):
        for side_, sx in (("L", 1), ("R", -1)):
            cx, cz = sx * JET_X, sz * JET_Z
            pod = f.lathe("pod", pod_prof, segments=16, axis=(0, 1, 0), origin=(cx, JET_FRONT_Y, cz))
            f.ensure_slots(pod, SLOTS)
            f.paint(pod, H)
            f.paint(pod, D, lambda c, n, i, cx=cx, cz=cz: JET_FRONT_Y + 2.56 < c.y < JET_FRONT_Y + 2.96 and math.hypot(c.x - cx, c.z - cz) > 0.69)
            # the throat: the annulus at the bottom of the bezel, round the dark spike
            f.paint(pod, J, lambda c, n, i, cx=cx, cz=cz: c.y > JET_MOUTH_Y - 0.36 and 0.12 < math.hypot(c.x - cx, c.z - cz) < r * 0.62)
            pp = [piece(pod, None, 50, bevel=0.0)]
            # a haunch armour cowl over each pod's outboard side
            cowl_a = math.atan2(sz * 0.55, sx * 0.84)
            def cowl_thick(u, v):
                return 0.12 + 0.08 * math.sin(math.pi * v) * math.sin(math.pi * u) ** 0.4
            cst = []
            for k in range(7):
                u = k / 6
                y = JET_FRONT_Y + 0.92 + 1.40 * u
                inner, outer = [], []
                for i in range(10):
                    v = i / 9
                    a = cowl_a + math.radians(-58 + 116 * v)
                    nx, nz = math.cos(a), math.sin(a)
                    rr = 0.81 + 0.08
                    t = cowl_thick(u, v)
                    inner.append((cx + nx * rr, cz + nz * rr))
                    outer.append((cx + nx * (rr + t), cz + nz * (rr + t)))
                cst.append(f.Station((0, y, 0), outer + list(reversed(inner)), right=(1, 0, 0), up=(0, 0, 1)))
            cowl = f.loft("cowl", cst)
            f.ensure_slots(cowl, SLOTS)
            f.paint(cowl, H)
            pp.append(piece(cowl, None, 51))
            bone_part(pp, "jet.%s.%s" % (vert, side_))

    # ================================================================== THE DIG-IN ANCHOR (keel)
    ky0, ky1 = -1.55, 1.05
    top = torso_bottom(-0.25) - 0.16
    tines = []
    span = ky1 - ky0
    outline_pts = [(ky1, 0.0), (ky0, 0.0)]
    # the spade's lower edge: three tines raking forward
    for j, (yc, depth) in enumerate(((-1.20, 1.55), (-0.30, 1.75), (0.60, 1.35))):
        outline_pts += [(yc - 0.35, -0.55), (yc - 0.10, -depth), (yc + 0.05, -depth + 0.12), (yc + 0.32, -0.60)]
    pts2 = [(y, z) for (y, z) in outline_pts]
    # plate() wants the outline in its (right, up) frame: right = +Y (along the hull), up = +Z
    keel = f.plate("keel", [(y, z) for (y, z) in pts2], 0.32, normal=(1, 0, 0), origin=(0, 0, top), right=(0, 1, 0))
    f.ensure_slots(keel, SLOTS)
    f.paint(keel, H)
    f.paint(keel, D, lambda c, n, i: c.z > top - 0.28 and abs(n.x) > 0.7)
    f.paint(keel, J, lambda c, n, i: c.z < top - 1.42 and abs(n.x) > 0.7)
    bone_part([piece(keel, None, 60, bevel=0.03, angle=30)], "keel")

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
    ctx.log("grizzly joined: %d verts, %d tris (n-gons triangulated: %d)" % (len(mesh.data.vertices), f.tri_count(mesh), len(big)))

    # ================================================================== RIG
    B = rig.BoneSpec
    bones = [B("hull", (0, 0, 0), (0, -1.0, 0)),
             B("head", (0, y1h, 0.16), (0, -5.0, 0.16), parent="hull"),
             B("jaw", JAW_HINGE, (0, jaw_y0, head_bottom(jaw_y0) - 0.25), parent="head")]
    for side_ in ("L", "R"):
        base, ax = ears[side_]
        bones.append(B("ear." + side_, tuple(base), tuple(base + ax * 0.45), parent="head"))
    bones.append(B("hump", (0, -0.75, torso_top(-0.75) + 0.12), (0, -0.75, torso_top(-0.75) + 1.6), parent="hull"))
    bones.append(B("keel", (0, -0.25, top), (0, -0.25, top - 1.6), parent="hull"))
    for side_, sx in (("L", 1), ("R", -1)):
        bones.append(B("shoulder." + side_, (sx * 1.70, -1.30, 0.35), (sx * 2.55, -1.30, 0.55), parent="hull"))
    for side_, sx in (("L", 1), ("R", -1)):
        root, d = claw_dirs[side_]
        hinge = Vector((sx * GAUNTLET[0], GAUNTLET[1] + 0.70, GAUNTLET[2] + 0.10))   # the wrist, at the paw's back
        bones.append(B("claw." + side_, tuple(hinge), tuple(root + d * 0.9), parent="hull"))
    for vert, sz in (("T", 1), ("B", -1)):
        for side_, sx in (("L", 1), ("R", -1)):
            bones.append(B("jet.%s.%s" % (vert, side_), (sx * JET_X, JET_FRONT_Y, sz * JET_Z), (sx * JET_X, JET_MOUTH_Y, sz * JET_Z), parent="hull"))
    arm = rig.build_armature(ARMATURE, bones)
    rig.skin_rigid(mesh, arm, default_bone="hull")
    bone_names = [b.name for b in arm.data.bones]

    # ================================================================== MORPHS (4-12 % of the ~15 diagonal)
    me = mesh.data
    nv = len(me.vertices)
    pid = [0.0] * nv
    me.attributes["pid"].data.foreach_get("value", pid)

    def pmask(*ids):
        s = set(float(i) for i in ids)
        return [1.0 if p in s else 0.0 for p in pid]
    ms = morphs.MaskSet(mesh)
    m = {n: ms.mask(n) for n in bone_names}

    def field_move(field, mask, anchor, extra=(0, 0, 0)):
        """Translate a part rigidly by `field` at its anchor, so a part rides the body it floats off."""
        dv = field(Vector(anchor)) + Vector(extra)

        def fn(i, co, n):
            return dv * mask[i] if mask[i] > 0 else Vector((0, 0, 0))
        return fn

    def scale_field(centre, s):
        c, s = Vector(centre), Vector(s)

        def field(co):
            d = co - c
            return Vector((d.x * (s.x - 1), d.y * (s.y - 1), d.z * (s.z - 1)))
        return field

    def apply_field(field, mask):
        def fn(i, co, n):
            return field(co) * mask[i] if mask[i] > 0 else Vector((0, 0, 0))
        return fn

    # Mass: one bulk field for the torso and the hump; everything that floats off them rides it
    mass_f = scale_field((0, -0.4, -0.1), (1.22, 1.06, 1.24))
    hump_f = scale_field((0, -0.9, 0.9), (1.22, 1.10, 1.38))
    mass_rides = [field_move(mass_f, m["shoulder.%s" % s], (sx * 2.1, -1.3, 0.3)) for s, sx in (("L", 1), ("R", -1))]
    mass_rides += [field_move(mass_f, m["claw.%s" % s], (sx * 2.6, -2.6, -0.6)) for s, sx in (("L", 1), ("R", -1))]
    mass_rides += [field_move(mass_f, m["jet.%s.%s" % (v, s)], (sx * 2.0, 1.0, sz * 1.1))
                   for v, sz in (("T", 1), ("B", -1)) for s, sx in (("L", 1), ("R", -1))]
    mass_rides += [field_move(mass_f, m["keel"], (0, -0.25, top))]
    # Charge: the claw blades lengthen, the paws swell and reach forward
    charge = []
    for s, sx in (("L", 1), ("R", -1)):
        root, d = claw_dirs[s]
        blades = [a * b for a, b in zip(m["claw." + s], pmask(41))]
        paws = [a * b for a, b in zip(m["claw." + s], pmask(40, 42))]
        charge.append(morphs.stretch_along((0, -1, -0.30), tuple(root + Vector((0, 0.15, 0))), 1.55, blades))
        charge.append(apply_field(scale_field((sx * GAUNTLET[0], GAUNTLET[1], GAUNTLET[2]), (1.22, 1.15, 1.28)), paws))
        charge.append(field_move(lambda co: Vector((sx * 0.12, -0.42, -0.06)), m["claw." + s], (0, 0, 0)))
    # Space: the barrel telescopes forward (the head ahead of the brow, the jaw with it); the dishes rise
    snout = [max(a, b) for a, b in zip(m["head"], m["jaw"])]
    space = [morphs.stretch_along((0, -1, 0), (0, -5.0, 0), 1.55, snout, ramp=(0.0, 0.5))]
    for s in ("L", "R"):
        base, ax = ears[s]
        space.append(field_move(lambda co, ax=ax: ax * 0.38, m["ear." + s], tuple(base)))
    # Time: the pods stretch aft and their bells flare; the hump sweeps back
    time_ = []
    for v, sz in (("T", 1), ("B", -1)):
        for s, sx in (("L", 1), ("R", -1)):
            mk = m["jet.%s.%s" % (v, s)]
            time_.append(morphs.stretch_along((0, 1, 0), (sx * JET_X, JET_FRONT_Y + 1.2, sz * JET_Z), 1.38, mk, ramp=(0.0, 0.5)))

            def flare(i, co, n, mk=mk, cx=sx * JET_X, cz=sz * JET_Z):
                if mk[i] <= 0 or co.y < JET_MOUTH_Y - 0.9:
                    return Vector((0, 0, 0))
                k = (co.y - (JET_MOUTH_Y - 0.9)) / 0.9
                return Vector(((co.x - cx) * 0.30 * k, 0, (co.z - cz) * 0.30 * k))
            time_.append(flare)

    def hump_sweep(i, co, n):
        if m["hump"][i] <= 0:
            return Vector((0, 0, 0))
        h = max(0.0, co.z - 1.2)
        return Vector((0, 0.55 * h, -0.10 * h))
    time_.append(hump_sweep)

    shapes = {
        "Charge": charge,
        "Mass": [apply_field(mass_f, m["hull"]), apply_field(hump_f, m["hump"]), *mass_rides],
        "Space": space,
        "Time": time_,
    }
    for name in ("Charge", "Mass", "Space", "Time"):
        kb, mx, frac, moved = morphs.add_shape(mesh, name, shapes[name])
        ctx.log("shape %s: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))
    if "pid" in me.attributes:
        me.attributes.remove(me.attributes["pid"])

    # ================================================================== TAKES (code drives the hull; these are the hinges)
    # world degrees in the Blender frame: +X turns a point AHEAD of its pivot DOWN; +Z carries a point ahead toward +X.
    poses = {
        "roar": {"jaw": (28, 0, 0), "head": (-8, 0, 0), "ear.L": (-20, 0, 0), "ear.R": (-20, 0, 0)},
        "swipe": {"claw.R": (30, 0, 34), "shoulder.R": (0, 12, 0)},
        "dig": {"keel": (-32, 0, 0), "claw.L": (34, 0, 0), "claw.R": (34, 0, 0)},
    }
    names = ("roar", "swipe", "dig")
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
                "roar": [("jaw", "z", -1, 0.10)],
                "swipe": [("claw.R", "x", +1, 0.10)],
                "dig": [("keel", "y", -1, 0.10), ("claw.L", "z", -1, 0.10)],
            },
            "shapes": ["Charge", "Mass", "Space", "Time"],
            "slots": list(SLOTS),
            # the wedge's envelope in the Blender frame (x +-3.5, nose 7.26 .. jet mouths 3.81, the fins' 5.5 tall):
            # the length is held to 5 %, the width (the claws) and the height (no tall fins) are the clade's
            "size_ref_blender": ((-3.49, -7.26, -5.50), (3.49, 3.81, 5.50)),
            "size_tol": (0.45, 0.05, 0.50),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.03,
        },
    }


FLOATING = ("head,jaw,ear.L,ear.R,hump,keel,shoulder.L,shoulder.R,claw.L,claw.R,"
            "jet.T.L,jet.T.R,jet.B.L,jet.B.R")
