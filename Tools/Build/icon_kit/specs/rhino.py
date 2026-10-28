"""The Rhino's ability-row icons (Resources/ElementalAbilityMaps/Rhino.asset), angular, one object each.

  Charge  Energize     hold both triggers to LIGHT the blade (the cooldown is its only limit); Charge scales how
                       many petals a sword hit takes from a rival, and at Charge 5 the sword POCKETS them instead
                       of knocking them loose as crystals (RHINO_ENERGY_SWORD.md, 2026-10-26)
  Mass    Afterimage   the unlit blade keeps leaving prisms in its own shape and pose for 5 s
  Space   Blade Reach  Space alone grows the blade, 24 -> 40; at Space 5 an energized blade pops super-shields
  Time    Ramp Spool   how fast the ramp winds the Rhino up to top speed; at Time 5 the Momentum ward

Before: four white-square PLACEHOLDER sprites. One card at a time with Garrett, options in the hull's whole lockup.

Charge is THE KATANA (Garrett 2026-10-28, round 3 option F). The blade follows the real sword: in the game it is the
ForceFieldSkimmer's capsule (mesh 10208, local scale (1.5, 24, 4.8)) standing off the nose (mount (0, -1, 20.7), raised
20 deg); the triggers swipe it (yaw + roll, up to 90 deg each way, RT - LT) and chop it down (pitch, up to 65 deg,
RT + LT). Rounds 1-2 drew it as a lozenge and a straight sword in the domain colour - "make it look more sword like and
not team colored. when lit make it the danger color. the idea is to follow the actual sword", then "more like a
katana." The HUD turns the card's katana with the live swipe (half the real angle), MIRRORS it so the edge leads the
swing ("it should flip the sword as well as rotate it so it is swiping with the sharp part"), and cross-fades it to
the straight blade seen from behind as both triggers chop ("the both triggers chop should remove the curve so it
looks like it is from behind"). White while dark, danger while lit.
"""
import math

import numpy as np
from PIL import Image

from canvas import GX, GY, N, empty, fill, rect, rotate

HULL = "Rhino"
ICON_DIR = "Assets/_Graphics/Icons/AbilityIcons/Rhino/"


def _png_mask(rel):
    """A committed sprite's alpha as a kit mask (the lockup's siblings while they are still placeholders)."""
    import os
    root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))
    a = Image.open(os.path.join(root, rel)).convert("RGBA").split()[3].resize((N, N), Image.LANCZOS)
    return np.asarray(a) > 127


def placeholder(name):
    return lambda: _png_mask(ICON_DIR + f"Rhino_{name}-PLACEHOLDER.png")


_BEFORE = dict(before=ICON_DIR + "Rhino_SwordEnergy-PLACEHOLDER.png", before_rev="90a5b7cf5", before_gauge_value=0.55)


# ---- the katana -------------------------------------------------------------------------------------------------
# A long shallow curve (sori) bending back toward the spine, one cutting edge on the convex side (the LEFT, in the
# sprite), the angled kissaki point (the edge turns up at the yokote and meets the spine), a small tsuba where the
# blade leaves the nose and a long tsuka. The pivot is the hilt: the view rotates, mirrors and foreshortens about it.
KATANA_HILT = (60.0, 100.0)  # the pivot, in card px (the view's katanaHilt): high enough for the long grip below it
KATANA_LEN = 92              # habaki to point (px)
KATANA_W = 12                # the blade's depth
KATANA_SORI = 14             # how far the point sits off the hilt's straight line (px): the curve
CARD_SWIPE = 0.5             # the card turns half the sword's real swipe (90 deg real -> 45), so it stays in the card
REAL_SWIPE = 90.0            # RhinoShieldSwipeConfig.swipeYawDegrees: a full single-trigger pull
REAL_CHOP = 65.0             # RhinoShieldSwipeConfig.chopPitchDegrees: both triggers fully pulled


def katana(angle=0.0, chop=0.0, tsuba=True, tsuka=True, length=KATANA_LEN, w=KATANA_W, sori=KATANA_SORI, hilt=KATANA_HILT, mirror=False):
    """The katana standing off the hilt: turned `angle` deg clockwise (the swipe, already scaled to the card),
    foreshortened by cos(`chop`), and `mirror`ed about the hilt so its edge (drawn on the left) leads a swipe right.
    `sori=0` is the blade seen from behind, as the chop shows it."""
    hx, hy = hilt
    fx = -1.0 if mirror else 1.0
    L = length * math.cos(math.radians(chop))
    k = min(0.17, 15.0 / max(L, 1.0))          # the kissaki's share of the blade
    steps = 24

    def centre(t):                              # the curve: off the line by sori * t^2, bending to the spine (+x)
        return sori * t * t * (L / length), -L * t

    def normal(t):                              # the unit vector toward the spine side at t
        dx, dy = 2 * sori * t * (L / length), -L
        n = math.hypot(dx, dy)
        return -dy / n, dx / n

    spine, edge = [], []
    for i in range(steps + 1):
        t = (1 - k) * i / steps
        cx, cy = centre(t)
        nx, ny = normal(t)
        hw = w / 2 * (1 - 0.25 * t)              # a slight taper toward the point
        spine.append((cx + nx * hw, cy + ny * hw))
        edge.append((cx - nx * hw, cy - ny * hw))
    # the kissaki: the spine runs on and dips to the point; the edge turns up at the yokote and runs straight to it
    tx, ty = centre(1.0)
    nx, ny = normal(1.0)
    hw = w / 2 * 0.75
    point = (tx + nx * hw * 0.6, ty + ny * hw * 0.6)
    sx, sy = centre(1 - k * 0.45)
    snx, sny = normal(1 - k * 0.45)
    spine.append((sx + snx * hw, sy + sny * hw))
    pts = spine + [point] + edge[::-1]
    m = fill(rotate([(hx + fx * x, hy + y) for x, y in pts], angle, hx, hy))
    if tsuba:                                   # the guard: a flat octagon across the blade's root
        g = [(-w * 0.95, 1), (-w * 0.7, -1.5), (w * 0.7, -1.5), (w * 0.95, 1), (w * 0.95, 5), (w * 0.7, 7.5),
             (-w * 0.7, 7.5), (-w * 0.95, 5)]
        m |= fill(rotate([(hx + x, hy + y) for x, y in g], angle, hx, hy))
    if tsuka:                                   # the grip: long, a touch narrower than the blade, its end cut square
        grip = [(-w * 0.42, 7), (w * 0.42, 7), (w * 0.38, 25), (-w * 0.38, 25)]
        m |= fill(rotate([(hx + x, hy + y) for x, y in grip], angle, hx, hy))
    return m


def katana_curved():
    return katana()


def katana_chop():
    return katana(sori=0.0)


CHOP_STRAIGHT01 = 0.75       # the view's katanaChopStraight01: the chop at which the blade shows from behind (sum 1.5 of
                             # 2, RhinoShieldSwipeConfig.stanceSumThreshold - the both-triggers energize stance)
CHOP_LENGTH01 = 0.8          # the view's katanaChopLength01: the blade's length on the card at a full chop (it tips away)


def katana_pose(swipe, chop01, facing=1.0):
    """What the HUD draws for a swipe (-1..1, RT - LT) and a chop (0..1): turned, mirrored when the swipe is to the
    right (at rest it keeps the side it last faced), shortened as it chops, and the straight blade from behind once
    both triggers are in. Mirrors RhinoVesselHUDView.KatanaPose so the sheet shows the HUD's states."""
    mirror = swipe > 0 or (swipe == 0 and facing < 0)
    ang = swipe * REAL_SWIPE * CARD_SWIPE
    length01 = 1.0 + (CHOP_LENGTH01 - 1.0) * chop01
    chop = math.degrees(math.acos(length01))
    straight = chop01 >= CHOP_STRAIGHT01
    return katana(ang, chop, mirror=mirror, sori=0.0 if straight else KATANA_SORI)


# (name, swipe -1..1, chop 0..1, lit, plate = EnergizeGauge01)
KATANA_STATES = [("at rest", 0.0, 0.0, False, 0.0),
                 ("RT: swiped right, edge leading", 1.0, 0.5, False, 0.0),
                 ("LT: swiped left, edge leading", -1.0, 0.5, False, 0.0),
                 ("half pull, right", 0.5, 0.25, False, 0.0),
                 ("both triggers: the chop, from behind", 0.0, 1.0, False, 0.55),
                 ("lit (still chopping)", 0.0, 1.0, True, 1.0),
                 ("lit, swiped right", 1.0, 0.5, True, 0.7),
                 ("cooling down", 0.0, 0.0, False, 0.0)]


def _katana_states():
    parts, states, gauge = {}, [], {}
    for k, (n, swipe, chop01, lit, g) in enumerate(KATANA_STATES):
        parts[f"k{k}"] = (lambda swipe=swipe, chop01=chop01: katana_pose(swipe, chop01))
        states.append((n, [(f"k{k}", "danger" if lit else "white")]))
        gauge[n] = (g, None)
    return parts, states, gauge


_KP, _KS, _KG = _katana_states()

PART_EXEMPT = {"coverage": "a part: the katana, slim by design", "box_fill": "a part", "in_box": "a part",
               "thin": "the katana is slim: Garrett 2026-10-28 'more like a katana'"}

ICONS = [
    dict(name="Rhino_Katana", element="Charge", act="Energize: the sword the Rhino swings, a katana at rest (composite; the HUD turns it live)",
         draw=katana_curved, exempt={"coverage": "a katana is slim (Garrett 2026-10-28)", "thin": "the katana is slim",
                                     "box_fill": "the blade stands on its hilt, off-centre by its curve",
                                     "in_box": "the blade runs the card's height, as the sword stands off the nose"}),
    dict(name="Rhino_KatanaCurved", element="Charge", act="Energize: the curved katana, the part the view turns and mirrors about the hilt",
         draw=katana_curved, subdir="Parts", family=False, exempt=PART_EXEMPT),
    dict(name="Rhino_KatanaChop", element="Charge", act="Energize: the katana seen from behind (no curve), the part the chop fades to",
         draw=katana_chop, subdir="Parts", family=False, exempt=PART_EXEMPT),
]

# The hull's other cards as the HUD draws them today (states.py options sheets show a candidate beside them).
LOCKUP = dict(
    Charge=dict(parts=dict(k=katana_curved), state=[("k", "white")]),
    Mass=dict(parts=dict(i=placeholder("Afterimage")), state=[("i", "white")]),
    Space=dict(parts=dict(i=placeholder("BladeReach")), state=[("i", "white")]),
    Time=dict(parts=dict(i=placeholder("RampSpool")), state=[("i", "white")], gauge=0.55),
)

CARDS = [
    dict(name="energize", element="Charge", **_BEFORE,
         title="Energize: the katana turns with the live swipe (half the real angle), flips so the edge leads, shows the straight blade seen from behind once both triggers chop; white dark, danger lit",
         parts=_KP, states=_KS, gauge=_KG),
]

VARIANT = "Assets/_Prefabs/UI Elements/VesselHUD/RhinoHUDVariant.prefab"
VIEW = "5161366343835098938"   # RhinoVesselHUDView on the variant

REBIND = [
    # The Charge card: the icon Image becomes a transparent container; the view GENERATES the katana under it (a pivot
    # at the hilt holding the curved blade and the straight one) from the two parts. The plate keeps its energize gauge.
    dict(file=VARIANT, pair=dict(container="7720000000000003015", view=VIEW, after="highlights", children=[],
                                 fields={"katanaCurvedSprite": ("icon", "Rhino_KatanaCurved"),
                                         "katanaChopSprite": ("icon", "Rhino_KatanaChop")})),
]


# ---- Mass: Afterimage options (2026-10-28) ------------------------------------------------------------------------
# The unlit blade keeps leaving PRISMS in its own shape and pose for 5 s (RHINO_SWORD_AFTERIMAGE.md): copies of the
# sword, built of prisms, hanging where it swung. A prism copy is the katana's blade cut across into blocks.
PRISM_PITCH = 13.0   # one prism's length along the blade (px)
PRISM_SEAM = 3.0     # the seam between prisms (px)


def prism_katana(angle, hilt=KATANA_HILT, length=KATANA_LEN, mirror=False):
    """The blade alone (no tsuba, no grip) at `angle`, cut across into prisms: an afterimage. Scaled with `length`
    (a copy further away is smaller), a touch wider than the sword so the blocks read."""
    hx, hy = hilt
    k = length / KATANA_LEN
    m = katana(angle, tsuba=False, tsuka=False, hilt=hilt, length=length, w=KATANA_W * 1.15 * k,
               sori=KATANA_SORI * k, mirror=mirror)
    a = math.radians(angle)
    along = (GX - hx) * math.sin(a) - (GY - hy) * math.cos(a)     # distance out along the blade from the hilt
    return m & ((along % (PRISM_PITCH * k)) >= PRISM_SEAM * max(k, 0.7))


# Mass is THE TRAIL (Garrett 2026-10-28, option H of K / H / I / J): the sword (white, as on the Charge card) swinging
# right past two prism copies of itself in the pilot's domain colour - they are the pilot's prisms - the copy nearer it
# brighter. Garrett on H: "the after image is flipped the wrong way": the copies are the sword where it WAS in the same
# swing, so they are mirrored with it, the edge leading right.
AFTER_BLADE_ANGLE = 20.0
AFTER_NEAR_ANGLE = -15.0
AFTER_FAR_ANGLE = -45.0
AFTER_NEAR_ALPHA = 0.8      # the view's afterimageNearAlpha
AFTER_FAR_ALPHA = 0.45      # the view's afterimageFarAlpha


def afterimage_blade():
    return katana(AFTER_BLADE_ANGLE, mirror=True)


def afterimage_near():
    return prism_katana(AFTER_NEAR_ANGLE, mirror=True)


def afterimage_far():
    return prism_katana(AFTER_FAR_ANGLE, mirror=True)


def afterimage():
    return afterimage_blade() | afterimage_near() | afterimage_far()


AFTER_LIT = [("far", "domain", AFTER_FAR_ALPHA), ("near", "domain", AFTER_NEAR_ALPHA), ("blade", "white")]
ICONS += [
    dict(name="Rhino_Afterimage", element="Mass", act="Afterimage: the sword swinging past two prism copies of itself (composite; the HUD draws the parts)",
         draw=afterimage, exempt={"components": "the sword and its prism copies", "specks": "the prism seams",
                                  "gap": "the prism seams", "thin": "the katana is slim", "in_box": "the sword runs the card's height"}),
    dict(name="Rhino_AfterimageBlade", element="Mass", act="Afterimage: the sword part (white)", draw=afterimage_blade,
         subdir="Parts", family=False, exempt=PART_EXEMPT),
    dict(name="Rhino_AfterimageNear", element="Mass", act="Afterimage: the nearer prism copy (domain colour)", draw=afterimage_near,
         subdir="Parts", family=False, exempt=dict(PART_EXEMPT, components="a copy built of prisms", specks="the prism seams", gap="the prism seams")),
    dict(name="Rhino_AfterimageFar", element="Mass", act="Afterimage: the further prism copy (domain colour, fainter)", draw=afterimage_far,
         subdir="Parts", family=False, exempt=dict(PART_EXEMPT, components="a copy built of prisms", specks="the prism seams", gap="the prism seams")),
]
LOCKUP["Mass"] = dict(parts=dict(blade=afterimage_blade, near=afterimage_near, far=afterimage_far), state=AFTER_LIT)

CARDS += [dict(name="afterimage", element="Mass", before=ICON_DIR + "Rhino_Afterimage-PLACEHOLDER.png", before_rev="80877373e",
               title="Afterimage: the sword (white) swinging past two prism copies of itself in your colour, shown while the copies are being laid",
               parts=dict(blade=afterimage_blade, near=afterimage_near, far=afterimage_far),
               states=[("idle: no afterimages", [("blade", "white")]),
                       ("laying (lit)", AFTER_LIT)])]

REBIND += [
    # The Mass card: the icon Image becomes a transparent container over three parts - the two prism copies (domain
    # colour, shown only while the afterimages are being laid) and the sword (white).
    dict(file=VARIANT, pair=dict(container="7720000000000003025", view=VIEW, after="highlights",
                                 children=[("AfterimageFar", "Rhino_AfterimageFar", "afterimageFarIcon"),
                                           ("AfterimageNear", "Rhino_AfterimageNear", "afterimageNearIcon"),
                                           ("AfterimageBlade", "Rhino_AfterimageBlade", "afterimageBladeIcon")])),
]


# ---- Space: Blade Reach (2026-10-28) -------------------------------------------------------------------------------
# Space alone sets the blade's length: 24 at rest -> 40 at Space 10 (ShieldSkimmerScaleDriver.UpdateLength, the
# Skimmer's live Space scale). Space 5 is SHIELD BREAKER: below it the blade BINDS on a super-shielded prism (it sticks,
# the turn drops to a third, the prism survives), energized or not; from Space 5 an energized blade pops it.
# Round 1 (N / O / P): Garrett asked what N's plate fill showed - the Space level, which the length already shows, and
# every vessel has an L5, so a mark for it was inconsistent - then "show it hitting a supershielded prism and it breaking
# above 5". So: the katana laid FLAT (apart from the upright Charge card) at the live length, its tip in a super-shielded
# prism that stays whole below Space 5 and is shattered from Space 5. No plate meter.
REACH_HILT = (20.0, 112.0)
REACH_ANGLE = 52.0                # the blade's lean, clockwise from upright: it reaches across the card's diagonal
REACH_MAX = 68.0                  # the blade at Space 10 (40 u)
REACH_REST = REACH_MAX * 24 / 40  # at rest (24 u)
PRISM_BITE = 2.0                  # how far the tip sits inside the prism's near point


def reach_len(space01):
    return REACH_REST + (REACH_MAX - REACH_REST) * space01


def flat_katana(length, w=15.0):
    return katana(REACH_ANGLE, hilt=REACH_HILT, length=length, w=w, sori=KATANA_SORI * length / KATANA_LEN, mirror=True)


# The super-shielded prism, as the game builds it (StellatedOctahedronMeshGenerator): the stella octangula, two
# tetrahedra through each other - eight spikes, tips at the cube corners, each spike three faces on a face of the
# inscribed octahedron. Garrett 2026-10-28: "that does not look like a stelated octahedron". Drawn from the real
# geometry (regular: a = b = c), seen straight down a body diagonal - the flat six-pointed star with its facets cut by
# seams, ONE colour ("we need a flatter design in order to play well with the others"; a shaded three-quarter view was
# round 3). Shattered: the eight spikes flung out along their own corners, as the game's
# 24-face shatter + explode-out.
STELLA_YAW, STELLA_PITCH = 45.0, -35.26   # down a body diagonal: the flat six-point star, the row's flat language
STELLA_LIGHT = (-0.55, 0.65, -0.5)       # toward the upper left, toward the viewer (view space, y up)
STELLA_SEAM = 1.5                        # px the facets are pulled in from their edges (a seam of ~3 px)


def _stella_faces(explode=0.0):
    """The 24 outer triangles in model space, (tip, base0, base1) per face, each spike pushed out by `explode`."""
    faces = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            for sz in (-1, 1):
                tip = np.array([sx, sy, sz], float)
                base = [np.array([sx, 0, 0], float), np.array([0, sy, 0], float), np.array([0, 0, sz], float)]
                push = tip / np.linalg.norm(tip) * explode
                for i in range(3):
                    faces.append([tip + push, base[i] + push, base[(i + 1) % 3] + push])
    return faces


def _view(p):
    y, x = math.radians(STELLA_YAW), math.radians(STELLA_PITCH)
    cy, sy, cx, sx = math.cos(y), math.sin(y), math.cos(x), math.sin(x)
    X, Y, Z = p
    X, Z = X * cy + Z * sy, -X * sy + Z * cy           # yaw about up
    Y, Z = Y * cx - Z * sx, Y * sx + Z * cx            # pitch about right
    return np.array([X, Y, Z])


def stella_tones(cx, cy, r, explode=0.0):
    """Three masks (light, mid, dark): the visible faces of the stella octangula centred (cx, cy), tips r px out."""
    light = np.array(STELLA_LIGHT) / np.linalg.norm(STELLA_LIGHT)
    k = r / math.sqrt(3)
    tris = []
    for f in _stella_faces(explode):
        v = [_view(p) for p in f]
        n = np.cross(v[1] - v[0], v[2] - v[0])
        n /= np.linalg.norm(n)
        centre = sum(v) / 3
        if np.dot(n, centre) < 0:                        # orient outward
            n = -n
        if n[2] >= 0:                                    # facing away (the viewer looks down +z)
            continue
        lit = float(np.dot(n, light))
        pts = [(cx + p[0] * k, cy - p[1] * k) for p in v]
        tris.append((centre[2], lit, pts))
    tris.sort(key=lambda t: -t[0])                       # far first
    ranked = sorted(t[1] for t in tris)                   # the tones split the visible faces in thirds by light
    cut_dark, cut_mid = ranked[len(ranked) // 3], ranked[2 * len(ranked) // 3]
    tones = [empty(), empty(), empty()]
    for _, lit, pts in tris:
        mx = sum(x for x, _ in pts) / 3
        my = sum(y for _, y in pts) / 3
        inset = []
        for x, y in pts:
            d = math.hypot(x - mx, y - my) or 1.0
            inset.append((x - (x - mx) / d * STELLA_SEAM, y - (y - my) / d * STELLA_SEAM))
        full = fill(pts)
        for t in tones:
            t &= ~full                                    # a nearer face covers what is behind it
        tone = 0 if lit >= cut_mid else 1 if lit >= cut_dark else 2
        tones[tone] |= fill(inset)
    return tones


PRISM_R = 36.0


def _reach_at(d_along, d_side=0.0):
    """A point d_along the blade's line from the hilt, d_side toward its upper-left side."""
    hx, hy = REACH_HILT
    a = math.radians(REACH_ANGLE)
    return (hx + math.sin(a) * d_along - math.cos(a) * d_side, hy - math.cos(a) * d_along - math.sin(a) * d_side)


def prism_centre(length):
    return _reach_at(length - PRISM_BITE + PRISM_R * 0.42, -KATANA_SORI * length / KATANA_LEN * 0.6)


def stella(cx, cy, r, explode=0.0):
    t = stella_tones(cx, cy, r, explode)
    return t[0] | t[1] | t[2]


def shielded_prism(length):
    cx, cy = prism_centre(length)
    return stella(cx, cy, PRISM_R)


def shattered_prism(length):
    cx, cy = prism_centre(length)
    return stella(cx, cy, PRISM_R * 0.72, explode=1.1)


def reach_posts():
    """O (Garrett's pick of N / O / P): two posts over the blade's line, at its rest length and its full reach, so the
    live tip reads against how far it can grow."""
    m = empty()
    for L in (REACH_REST, REACH_MAX):
        x, y = _reach_at(L, 30.0)
        m |= fill(rotate([(x - 2.4, y - 8), (x + 2.4, y - 8), (x + 2.4, y + 8), (x - 2.4, y + 8)], REACH_ANGLE - 90, x, y))
    return m


# (name, Space 0..1, broken, lit)
REACH_STATES = [("Space 0: short, bound in the shield", 0.0, False, False),
                ("Space 4: longer, still bound", 0.4, False, False),
                ("Space 4, energized: still bound", 0.4, False, True),
                ("Space 5+: Shield Breaker - it pops", 0.5, True, False),
                ("Space 10, energized", 1.0, True, True)]


def reach_card():
    parts, states = {}, []
    for k, (n, s01, broken, lit) in enumerate(REACH_STATES):
        L = reach_len(s01)
        parts[f"b{k}"] = (lambda L=L: flat_katana(L))
        parts[f"p{k}"] = (lambda L=L, broken=broken: shattered_prism(L) if broken else shielded_prism(L))
        parts["posts"] = reach_posts
        states.append((n, [(f"p{k}", "shielded")] + [(f"b{k}", "danger" if lit else "white"), ("posts", "white", 0.6)]))
    return dict(name="bladereach_round2", group="bladereach2", lockup_state=3, element="Space",
                before=ICON_DIR + "Rhino_BladeReach-PLACEHOLDER.png",
                title="Blade Reach: the katana flat at its live length between posts at its rest and full reach (O), its tip in a super-shielded prism - bound below Space 5, shattered from Space 5; no plate meter",
                parts=parts, states=states)


CARDS += [reach_card()]
