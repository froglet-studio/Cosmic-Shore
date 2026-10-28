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


def stella_facets(cx, cy, r):
    """The flat star's facets: (mask, centroid) per visible face, each already cut back by the faces in front of it
    and inset by the seam - the separation the game's shatter breaks along."""
    k = r / math.sqrt(3)
    tris = []
    for f in _stella_faces():
        v = [_view(p) for p in f]
        n = np.cross(v[1] - v[0], v[2] - v[0])
        centre = sum(v) / 3
        if np.dot(n, centre) < 0:
            n = -n
        if n[2] >= 0:
            continue
        tris.append((centre[2], [(cx + p[0] * k, cy - p[1] * k) for p in v]))
    tris.sort(key=lambda t: -t[0])
    facets = []
    for _, pts in tris:
        full = fill(pts)
        facets = [(m & ~full, c) for m, c in facets]
        mx = sum(x for x, _ in pts) / 3
        my = sum(y for _, y in pts) / 3
        inset = []
        for x, y in pts:
            d = math.hypot(x - mx, y - my) or 1.0
            inset.append((x - (x - mx) / d * STELLA_SEAM, y - (y - my) / d * STELLA_SEAM))
        facets.append((fill(inset), (mx, my)))
    return [(m, c) for m, c in facets if m.any()]


def _shift(mask, dx, dy):
    """A mask moved by (dx, dy) icon px, edges filled empty."""
    sx, sy = int(round(dx * N / 128)), int(round(dy * N / 128))
    out = np.zeros_like(mask)
    h, w = mask.shape
    out[max(0, sy):min(h, h + sy), max(0, sx):min(w, w + sx)] = mask[max(0, -sy):min(h, h - sy), max(0, -sx):min(w, w - sx)]
    return out


STELLA_BURST = 0.7   # how far each facet flies, as a fraction of its distance from the centre


def stella(cx, cy, r, broken=False):
    """The flat super-shield. Broken: every facet flies straight out from the centre along its own centroid, so the
    star comes apart along its real faces (the game's 24-face shatter + explode-out), each piece whole."""
    m = empty()
    for f, (mx, my) in stella_facets(cx, cy, r):
        m |= _shift(f, (mx - cx) * STELLA_BURST, (my - cy) * STELLA_BURST) if broken else f
    return m


KNOCKOUT = 2.5        # px the prism stands clear of the sword and the posts


def _clear(mask, length):
    from toy_portraits import grown
    return mask & ~grown(flat_katana(length) | reach_posts(), KNOCKOUT)


def shielded_prism(length):
    cx, cy = prism_centre(length)
    return _clear(stella(cx, cy, PRISM_R), length)


def shattered_prism(length):
    cx, cy = prism_centre(length)
    return _clear(stella(cx, cy, PRISM_R * 0.74, broken=True), length)


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
    return dict(name="bladereach", element="Space",
                before=ICON_DIR + "Rhino_BladeReach-PLACEHOLDER.png", before_rev="3367d4308",
                title="Blade Reach: the katana at its live length between posts at its rest and full reach (O), its tip in a super-shielded prism - bound below Space 5, shattered from Space 5; no plate meter",
                parts=parts, states=states)


# ---- the Space card's PARTS (Garrett 2026-10-28, round 5: "excellent"). The view lays them out live:
# a pivot at the hilt turned REACH_ANGLE holds the hilt part and the blade, the blade SCALED along itself to the live
# length (Skimmer.ElementalScale01: 24 at rest -> 40 at Space 10); the prism is placed at the live tip, whole or broken
# (Shield Breaker active); the posts are fixed. The upright parts stand on PART_HILT so the long grip fits in the sprite.
PART_HILT = (64.0, 100.0)
PRISM_ALONG = PRISM_R * 0.42 - PRISM_BITE              # the prism's centre past the tip, along the blade (px)
PRISM_SIDE_PER_LENGTH = -KATANA_SORI * 0.6 / KATANA_LEN  # and to its side, per px of blade (the curve's lean)


def reach_part_hilt():
    full = katana(0.0, hilt=PART_HILT, length=REACH_MAX, w=15.0, sori=KATANA_SORI * REACH_MAX / KATANA_LEN, mirror=True)
    return full & ~reach_part_blade()


def reach_part_blade():
    return katana(0.0, hilt=PART_HILT, length=REACH_MAX, w=15.0, sori=KATANA_SORI * REACH_MAX / KATANA_LEN,
                  tsuba=False, tsuka=False, mirror=True)


def _prism_part(broken):
    """The prism centred on the sprite, cut clear of the blade as it enters (baked at Space 5's length: the blade
    always meets the prism the same way, only the distance changes)."""
    L0 = reach_len(0.5)
    cx, cy = prism_centre(L0)
    m = shattered_prism(L0) if broken else shielded_prism(L0)
    return _shift(m, 64.0 - cx, 64.0 - cy)


def reach_part_prism():
    return _prism_part(False)


def reach_part_prism_broken():
    return _prism_part(True)


def blade_reach():
    """The composite at Space 4 (bound): the card's fallback and the fleet sheet's tile."""
    L = reach_len(0.4)
    return flat_katana(L) | shielded_prism(L) | reach_posts()


REACH_PART_EXEMPT = dict(PART_EXEMPT, components="a part of several pieces", specks="the facet seams", gap="the facet seams")
ICONS += [
    dict(name="Rhino_BladeReach", element="Space", act="Blade Reach: the katana at its live length, its tip in a super-shielded prism (composite; the HUD lays the parts out)",
         draw=blade_reach, exempt={"components": "the sword, the prism's facets and the posts", "specks": "the facet seams",
                                   "gap": "the facet seams", "thin": "the katana is slim", "in_box": "the sword runs corner to corner"}),
    dict(name="Rhino_ReachHilt", element="Space", act="Blade Reach: the tsuba and grip, upright on the part hilt", draw=reach_part_hilt,
         subdir="Parts", family=False, exempt=REACH_PART_EXEMPT),
    dict(name="Rhino_ReachBlade", element="Space", act="Blade Reach: the blade at full reach, upright; the view scales it to the live length",
         draw=reach_part_blade, subdir="Parts", family=False, exempt=REACH_PART_EXEMPT),
    dict(name="Rhino_ReachPrism", element="Space", act="Blade Reach: the super-shielded prism (flat stella octangula), centred", draw=reach_part_prism,
         subdir="Parts", family=False, exempt=REACH_PART_EXEMPT),
    dict(name="Rhino_ReachPrismBroken", element="Space", act="Blade Reach: the prism shattered along its facets (Shield Breaker), centred",
         draw=reach_part_prism_broken, subdir="Parts", family=False, exempt=REACH_PART_EXEMPT),
    dict(name="Rhino_ReachPosts", element="Space", act="Blade Reach: the posts at the rest length and the full reach, in card space",
         draw=reach_posts, subdir="Parts", family=False, exempt=REACH_PART_EXEMPT),
]
LOCKUP["Space"] = dict(parts=dict(p=lambda: shielded_prism(reach_len(0.4)), b=lambda: flat_katana(reach_len(0.4)), t=reach_posts),
                       state=[("p", "shielded"), ("b", "white"), ("t", "white", 0.6)])

CARDS += [reach_card()]
REBIND += [
    # The Space card: the icon Image becomes a transparent container; the view generates the layout under it from
    # the five parts (no prefab children: the layout is the view's serialized numbers).
    dict(file=VARIANT, pair=dict(container="7720000000000003035", view=VIEW, after="highlights", children=[],
                                 fields={"reachHiltSprite": ("icon", "Rhino_ReachHilt"),
                                         "reachBladeSprite": ("icon", "Rhino_ReachBlade"),
                                         "reachPrismSprite": ("icon", "Rhino_ReachPrism"),
                                         "reachPrismBrokenSprite": ("icon", "Rhino_ReachPrismBroken"),
                                         "reachPostsSprite": ("icon", "Rhino_ReachPosts")})),
]


# ---- Time: Ramp Spool options (2026-10-28) -------------------------------------------------------------------------
# Hold full throttle dead straight and the ramp winds the Rhino up at a CONSTANT rate, cruise 50 -> top 840 u/s; Time
# scales the rate (220 -> 550 u/s per s, floored 110), not the top. Time 5 is MOMENTUM: above 70% of top speed (588 u/s)
# danger prisms cannot drain the Rhino (VesselElementalImmunity, WhileAboveTopSpeedFraction; RHINO_RAMP_BOOST.md).
# Round 1 (R / T / U: the flat katana with streaks, chevrons, a ramp) - Garrett: "this ability is about the rhino and
# its invulnerability to danger prisms so it should have a rhino silhouette and a tick on the meter like you did with
# the sparrow that shows the barrier come up to protect against the danger prism". So: the Rhino seen from above, nose
# up (the game's own HUD silhouette, Assets/_Graphics/Silhouettes/Rhino: RhinoBody + two RhinoWing, laid out at the
# hull's measured pod offsets), the plate gauge = speed / top speed with ONE tick at 70%, and above it the barrier up
# between the nose and a danger prism.
SIL_DIR = "Assets/_Graphics/Silhouettes/Rhino/"
SIL_PX_PER_U = 10.0                # RhinoBody: 56 px across the 5.6 u fuselage
SIL_POD_X = 5.43                   # the pods' centres off the centreline (u, Docs/VesselModels/Rhino.md 5.2)
SIL_POD_DROP = (6.43 + 4.47)       # nose tip (z +6.43) to the pods' centres (z -4.47), u
SIL_WING_POD_Y = 77.0              # the pod's centre down the RhinoWing sprite (px)
MOMENTUM01 = 0.7                   # the tick: Momentum's 70% of top speed
TIME_MARKS = [MOMENTUM01]


def _sil(name):
    import os
    root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))
    return Image.open(os.path.join(root, SIL_DIR + name + ".png")).convert("RGBA").split()[3]


def _rhino_composite():
    """The top view at the silhouette's own scale (10 px/u): the body, the two wing plates through their pods."""
    body, wing = _sil("RhinoBody"), _sil("RhinoWing")
    dx = SIL_POD_X * SIL_PX_PER_U
    top = SIL_POD_DROP * SIL_PX_PER_U - SIL_WING_POD_Y          # the wings' top edge below the nose
    W = int(2 * dx + wing.width) + 2
    H = int(max(body.height, top + wing.height)) + 2
    a = Image.new("L", (W, H), 0)
    cx = W / 2
    a.paste(body, (int(round(cx - body.width / 2)), 0), body)
    for sx in (-1, 1):
        a.paste(wing, (int(round(cx + sx * dx - wing.width / 2)), int(round(top))), wing)
    return a


def rhino_top(cx=64.0, nose_y=40.0, height=88.0):
    """The Rhino from above, nose up at (cx, nose_y), `height` card px from nose to the back plates' ends."""
    a = _rhino_composite()
    k = height / a.height
    w, h = a.width * k, a.height * k
    full = Image.new("L", (N, N), 0)
    s = N / 128.0
    im = a.resize((max(1, int(round(w * s))), max(1, int(round(h * s)))), Image.LANCZOS)
    full.paste(im, (int(round((cx - w / 2) * s)), int(round(nose_y * s))))
    return np.asarray(full) > 127


NOSE_Y = 38.0


def danger_prism(cx, cy, angle=0.0, l=30.0, w=11.0):
    """A plain prism (the trail's block), long across the Rhino's path, as the danger prisms lie in a rival's trail."""
    return fill(rotate([(cx - l / 2, cy - w / 2), (cx + l / 2, cy - w / 2), (cx + l / 2, cy + w / 2), (cx - l / 2, cy + w / 2)],
                       angle, cx, cy))


def barrier_arc(cy=NOSE_Y - 8.0, r=40.0, half=50.0, t=6.0):
    """The ward: a bow of light standing ahead of the nose, curving back around the shoulders."""
    cx, oy = 64.0, cy + r
    out, inn = [], []
    for k in range(25):
        a = math.radians(-90 - half + 2 * half * k / 24)
        out.append((cx + r * math.cos(a), oy + r * math.sin(a)))
        inn.append((cx + (r - t) * math.cos(a), oy + (r - t) * math.sin(a)))
    return fill(out + inn[::-1])


def barrier_wedge(cy=NOSE_Y - 10.0, half_w=36.0, depth=16.0, t=6.0):
    """W: the ward as a plough's blade - a chevron ahead of the nose (the bulldozer that ignores mass)."""
    sh = t / math.cos(math.atan2(depth, half_w))
    return fill([(64 - half_w, cy + depth), (64, cy), (64 + half_w, cy + depth),
                 (64 + half_w, cy + depth + t), (64, cy + sh), (64 - half_w, cy + depth + t)])


def _clear_of(mask, *others, px=3.0):
    import toy_portraits
    keep = mask.copy()
    for o in others:
        keep &= ~toy_portraits.grown(o, px)
    return keep


# (name, speed / top, boosting, upgraded (Time 5), straight)
RAMP_STATES = [("cruising: no ramp", 0.06, False, True, True),
               ("winding up (lit)", 0.45, True, True, True),
               ("past 70%, Time below 5: no ward", 0.85, True, False, True),
               ("past 70% at Time 5: Momentum holds", 0.85, True, True, True),
               ("bent line: the ramp grades down", 0.6, True, True, False)]
_GAUGE_DIM = (120, 120, 140, 255)


def _ramp_option(name, title, ward, prism_ahead, prism_glance):
    hull = lambda: rhino_top(nose_y=NOSE_Y)
    parts = dict(hull=hull, ward=lambda: _clear_of(ward(), hull()),
                 ahead=lambda: _clear_of(prism_ahead(), hull()),
                 glance=lambda: _clear_of(prism_glance(), ward(), hull()))
    states, gauge = [], {}
    for n, s, boosting, up, straight in RAMP_STATES:
        warded = up and s >= MOMENTUM01
        L = [("glance", "danger"), ("ward", "white")] if warded else [("ahead", "danger")]
        states.append((n, L + [("hull", "domain")]))
        gauge[n] = (s, None if straight else _GAUGE_DIM)
    return dict(name=name, group="rampspool4", lockup_state=3, element="Time",
                before=ICON_DIR + "Rhino_RampSpool-PLACEHOLDER.png", before_rev="899c25973", before_gauge_value=0.55,
                gauge_marks=TIME_MARKS, gauge_marks_style="segments", title=title, parts=parts, states=states, gauge=gauge)


# Round 3: "lets have the rhino travel left to right so it doesn't cause visual confusion between the impact event and
# the threshold" - nose up put the ward and the prism at the TOP of the card, right under the plate's fill line and
# its 70% ticks. The Rhino now flies nose RIGHT, the ward and the prism ahead of it on the right.
SIDE_CY = 66.0
SIDE_NOSE_X = 74.0
SIDE_LEN = 70.0


def rhino_right(nose_x=SIDE_NOSE_X, cy=SIDE_CY, length=SIDE_LEN):
    """The Rhino from above flying RIGHT: the top view turned a quarter clockwise, nose at (nose_x, cy)."""
    a = _rhino_composite().rotate(-90, expand=True)          # nose up -> nose right
    k = length / a.width
    w, h = a.width * k, a.height * k
    full = Image.new("L", (N, N), 0)
    s = N / 128.0
    im = a.resize((max(1, int(round(w * s))), max(1, int(round(h * s)))), Image.LANCZOS)
    full.paste(im, (int(round((nose_x - w) * s)), int(round((cy - h / 2) * s))))
    return np.asarray(full) > 127


def ward_right(x=SIDE_NOSE_X + 8.0, cy=SIDE_CY, half_h=34.0, depth=15.0, t=6.0):
    """The plough's chevron ahead of the nose, pointing the way the Rhino flies."""
    sh = t / math.cos(math.atan2(depth, half_h))
    return fill([(x, cy - half_h), (x + depth, cy), (x, cy + half_h),
                 (x - t, cy + half_h), (x + depth - sh, cy), (x - t, cy - half_h)])


def _side_option():
    hull = rhino_right
    ahead = lambda: danger_prism(114, SIDE_CY, angle=90)
    glance = lambda: danger_prism(110, SIDE_CY + 36, angle=-55)
    parts = dict(hull=hull, ward=lambda: _clear_of(ward_right(), hull()),
                 ahead=lambda: _clear_of(ahead(), hull()),
                 glance=lambda: _clear_of(glance(), ward_right(), hull()))
    c = _ramp_option("time_option_w2",
                     "W2 - THE PLOUGH, flying right: past the 70% mark (edge ticks) at Time 5 the chevron ward stands ahead of the nose and the danger prism glances away",
                     ward_right, ahead, glance)
    c["parts"] = parts
    return c


CARDS += [_side_option()]
