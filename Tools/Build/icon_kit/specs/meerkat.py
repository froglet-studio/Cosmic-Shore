"""The Meerkat's ability-row icons (R_VesselActions/MEERKAT.md section 10; map Resources/ElementalAbilityMaps/Meerkat.asset),
round 1 (2026-11-03): its first row of its own. Until then it wore the Stoat's HUD and four Stoat placeholders (Ripple,
Modes, ModeTime, Pathfinder), which named nothing it does. Single white sprites; the HOW-MUCH of each card is the plate
gauge MeerkatHUDView already fills (sentinels standing, portal prisms standing, the reticle's growth, the ride left).

  Charge  Sentinel Mode  Y toggles it. Each new soliton leaves a SENTINEL at its way in: an afterimage of the hull that
                         stands guard, zaps rivals in its guard (Charge sets the petals a zap knocks out) and shields your
                         prisms there. Pool 3 (L5 Six Sentinels: 6). ONE object: the hull standing upright, a meerkat on
                         sentry, on the rim of the mouth it guards, its guard an arc over it.
  Mass    Exit Portal    passive: when a soliton forms, six small prisms of your domain on a hexagon round its way out,
                         at 1.5 x the exit's radius; Mass sets their size (L5 Danger Portal: danger prisms). ONE object:
                         the mouth face-on, ringed by its six prisms.
  Space   Soliton        RT forms the wormhole the reticle shows: one oval arc, wide mouths and a narrow throat (its
                         surface area is the Space level), a standing tunnel anyone may ride; it swallows prisms at its
                         way in and fires them out of its way out (L5 Steal). ONE object: the wormhole itself, an arch
                         that pinches to its throat and flares to two open mouths.
  Time    Faster Ride    passive: you ride every soliton faster, yours or anyone's (Time grows the throat's rush; L5 Boost
                         Out: 1.5 x the exit speed). ONE object: the hull shooting out of a mouth, one chamfered bar behind.

Real geometry:
  the hull     the Meerkat's FBX (nose +x in the side view, pitch -90 puts its back up). Until the Meerkat's own hull lands
               (/vessel-model, 2026-11-03) this is Stoat.fbx, the model the Meerkat flies today; HULL_FBX switches with it.
  a prism      a square with a negative-space X (motifs.prism_square_x; two close triangles read as a rhombus at 40 px
               on the Exit Portal, roster pass 2026-11-07), never the rhombus (Garrett 2026-11-01).
  a mouth      an ellipse ring: face-on on the Exit Portal, edge-on where the hull flies into or stands over it.
"""
import math
import os

import mesh_silhouette
from canvas import GX, GY, SS, band, disc, empty, fill, ngon, ring, rotate
from motifs import prism_halves, prism_square_x

HULL = "Meerkat"
ICON_DIR = "Assets/_Graphics/Icons/AbilityIcons/Stoat/"   # the placeholders the Meerkat wore (the sheet's BEFORE)
_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))
_OWN = "Assets/_Models/Vessel Models/Meerkat.fbx"
HULL_FBX = _OWN if os.path.exists(os.path.join(_ROOT, _OWN)) else "Assets/_Models/Vessel Models/Stoat.fbx"
SIDE = dict(pitch=-90, yaw=0, dilate_px=2.6)               # side on, back up, nose right


def _grown(mask, px):
    """Grow a mask by `px` icon px (the clearance a secondary part keeps from the focal one)."""
    r = int(round(px * SS))
    out = mask.copy()
    for dx in range(-r, r + 1, max(1, r // 4)):
        for dy in range(-r, r + 1, max(1, r // 4)):
            out |= _roll(mask, dx, dy)
    return out


def _roll(mask, dx, dy):
    import numpy as np
    return np.roll(np.roll(mask, dy, axis=0), dx, axis=1)


def _ellipse(cx, cy, rx, ry, rot=0.0, n=96):
    pts = [(cx + rx * math.cos(2 * math.pi * i / n), cy + ry * math.sin(2 * math.pi * i / n)) for i in range(n)]
    return fill(rotate(pts, rot, cx, cy))


def _mouth(cx, cy, rx, ry, w, rot=0.0):
    """A mouth: an ellipse ring `w` px thick, its hole open."""
    return _ellipse(cx, cy, rx, ry, rot) & ~_ellipse(cx, cy, rx - w, ry - w * ry / rx, rot)


# ---- Space: the Soliton ----------------------------------------------------------------------------------------------
# An arch (the oval arc bending round its middle, SolitonTunnelShapeMath.Conic) whose wall pinches to the throat and flares
# to a mouth at each end; each foot plunges into its mouth, an open ring seen edge on, so it reads as a tunnel from one
# hole in space to another, not a horseshoe.
ARCH = ((33.0, 94.0), (64.0, -34.0), (95.0, 94.0))       # quadratic Bezier: way in, control, way out
THROAT_W, MOUTH_W = 13.0, 30.0
MOUTH_RX, MOUTH_RY, MOUTH_RIM = 19.0, 7.5, 6.5


def _bez(t):
    (x0, y0), (x1, y1), (x2, y2) = ARCH
    u = 1 - t
    p = (u * u * x0 + 2 * u * t * x1 + t * t * x2, u * u * y0 + 2 * u * t * y1 + t * t * y2)
    d = (2 * u * (x1 - x0) + 2 * t * (x2 - x1), 2 * u * (y1 - y0) + 2 * t * (y2 - y1))
    m = math.hypot(*d) or 1.0
    return p, (d[0] / m, d[1] / m)


def soliton():
    left, right = [], []
    steps = 64
    for i in range(steps + 1):
        t = i / steps
        (x, y), (tx, ty) = _bez(t)
        w = THROAT_W + (MOUTH_W - THROAT_W) * (abs(2 * t - 1) ** 2.2)
        nx, ny = -ty, tx
        left.append((x + nx * w / 2, y + ny * w / 2))
        right.append((x - nx * w / 2, y - ny * w / 2))
    body = fill(left + right[::-1])
    out = empty()
    for t in (0.0, 1.0):
        (x, y), _ = _bez(t)
        rim = _mouth(x, y, MOUTH_RX, MOUTH_RY, MOUTH_RIM)
        front = rim & (GY > y)                              # the near lip, over the tube
        body &= ~(_ellipse(x, y, MOUTH_RX, MOUTH_RY) & (GY > y - 1)) & ~_grown(front, 2.5)
        out |= rim
    return out | body


# ---- Mass: the Exit Portal -------------------------------------------------------------------------------------------
PORTAL_R, PRISM_S = 44.0, 9.5


def exit_portal():
    out = ring(64, 64, 19, w=11)
    for x, y in ngon(6, PORTAL_R, 64, 64, rot=-90):
        out |= prism_square_x(x, y, PRISM_S)
    return out


# ---- Charge: Sentinel Mode -------------------------------------------------------------------------------------------
def _sentinel_hull():
    # side on, rolled nose-up: the hull standing on its tail like a meerkat on sentry
    return mesh_silhouette.silhouette(HULL_FBX, box=86, cx=64, cy=62, roll=-90, **SIDE)


def sentinel_mode():
    hull = _sentinel_hull()
    rim = _mouth(64, 106, 40, 11, 7)
    guard = band(64, 62, 52, 200, 340, w=8)
    keep = _grown(hull, 5)
    return hull | (rim & ~keep) | (guard & ~keep)


# ---- Time: Faster Ride -----------------------------------------------------------------------------------------------
RIDE_LEAN = -16


def faster_ride():
    """The hull leaning up and out of the way out, a mouth edge on just behind its tail, and ONE chamfered bar (the fleet's
    motion mark) along the line it left by, cut clear of both."""
    hull = mesh_silhouette.silhouette(HULL_FBX, box=86, cx=76, cy=60, roll=RIDE_LEAN, **SIDE)
    x_tail = float(GX[hull].min())
    y_tail = float(GY[hull & (GX < x_tail + 6)].mean())
    mx = max(22.0, x_tail - 16)
    mouth = _mouth(mx, y_tail + 4, 12, 32, 10, rot=RIDE_LEAN)
    bar = fill(rotate([(mx + 14, y_tail - 6), (x_tail + 20, y_tail - 6), (x_tail + 14, y_tail + 6), (mx + 8, y_tail + 6)],
                      RIDE_LEAN, mx, y_tail))
    keep = _grown(hull, 4)
    return hull | (mouth & ~keep) | (bar & ~keep & ~_grown(mouth, 4))


# ---- the OMNI crystal card: the Soliton Network ----------------------------------------------------------------------
# Any omni crystal the Meerkat takes grows a Soliton Network round it (R_VesselActions/MEERKAT.md section 7a; sized by Mass,
# half the cell to the whole cell): solitons forking from where it took the crystal. The card is
# VesselHUDView.omniAbilitySprite, ONE white sprite (the lockup tints nothing on the omni card). ONE object: a mouth at
# the heart, three tunnels forking out of it, each pinched to its throat and opening on a mouth of its own.
NET_ARMS = (-90.0, 30.0, 150.0)
NET_R = 45.0


def _tunnel(a, b, bend, w0, w1):
    """A tunnel from a to b: a quadratic arc bowed `bend` px off the chord, `w1` wide at both ends and `w0` at its throat."""
    (ax, ay), (bx, by) = a, b
    mx, my = (ax + bx) / 2, (ay + by) / 2
    dx, dy = bx - ax, by - ay
    m = math.hypot(dx, dy) or 1.0
    c = (mx - dy / m * bend, my + dx / m * bend)
    left, right = [], []
    for i in range(41):
        t = i / 40
        u = 1 - t
        x = u * u * ax + 2 * u * t * c[0] + t * t * bx
        y = u * u * ay + 2 * u * t * c[1] + t * t * by
        tx = 2 * u * (c[0] - ax) + 2 * t * (bx - c[0])
        ty = 2 * u * (c[1] - ay) + 2 * t * (by - c[1])
        tm = math.hypot(tx, ty) or 1.0
        w = w0 + (w1 - w0) * abs(2 * t - 1) ** 2
        left.append((x - ty / tm * w / 2, y + tx / tm * w / 2))
        right.append((x + ty / tm * w / 2, y - tx / tm * w / 2))
    return fill(left + right[::-1])


def soliton_network():
    hub = ring(64, 64, 12, w=9)
    tips = [(64 + NET_R * math.cos(math.radians(a)), 64 + NET_R * math.sin(math.radians(a))) for a in NET_ARMS]
    mouths = empty()
    for x, y in tips:
        mouths |= ring(x, y, 10, w=7)
    tubes = empty()
    for x, y in tips:
        tubes |= _tunnel((64, 64), (x, y), 10.0, 11.0, 22.0)
    rims = hub | mouths
    holes = disc(64, 64, 7.5)
    for x, y in tips:
        holes |= disc(x, y, 6.5)
    return rims | (tubes & ~_grown(rims, 2.5) & ~holes)


# ---- level 5 (the roster audit, 2026-11-13): each the binding's upgradedSprite, swapped in when the upgrade unlocks ------
def six_sentinels():
    """Charge 5 SIX SENTINELS: the pool doubles - a rank of sentinels on one wide mouth, under one guard."""
    lead = mesh_silhouette.silhouette(HULL_FBX, box=78, cx=64, cy=64, roll=-90, **SIDE)
    side = empty()
    for x in (26, 102):
        side |= mesh_silhouette.silhouette(HULL_FBX, box=62, cx=x, cy=72, roll=-90, **SIDE)
    rim = _mouth(64, 108, 54, 11, 7)
    guard = band(64, 70, 58, 205, 335, w=7)
    keep = _grown(lead | side, 4)
    return lead | side | (rim & ~keep) | (guard & ~keep)


def danger_portal():
    """Mass 5 DANGER PORTAL: the way out ringed by six DANGER prisms - spiked, they hurt whoever flies through them."""
    import motifs
    out = ring(64, 64, 19, w=11)
    for x, y in ngon(6, PORTAL_R, 64, 64, rot=-90):
        out |= motifs.burst(x, y, 12.5, spikes=6, inner=0.5, rot=-90)
    return out


def steal():
    """Space 5 STEAL: a rival's prism (hollow) swallowed at the way in comes out of the way out as ours (solid)."""
    import motifs
    from canvas import shift, stroke
    arch = shift(soliton(), 0, 10) & (GY < 116)
    s = 12.0
    rival = stroke([(20 - s, 22 - s), (20 + s, 22 - s), (20 + s, 22 + s), (20 - s, 22 + s)], w=5.0, closed=True)
    ours = prism_square_x(108, 22, s)
    head = motifs.arrowhead((20.0, 56.0), (0.0, 1.0), 12.0) | motifs.arrowhead((108.0, 46.0), (0.0, -1.0), 12.0)
    cut = _grown(rival | ours | head, 3)
    return (arch & ~cut) | rival | ours | head


def boost_out():
    """Time 5 BOOST OUT: the hull thrown out of the way out - two long bars flanking its wake, where the plain ride leaves one short one."""
    hull = mesh_silhouette.silhouette(HULL_FBX, box=78, cx=84, cy=58, roll=RIDE_LEAN, **SIDE)
    x_tail = float(GX[hull].min())
    y_tail = float(GY[hull & (GX < x_tail + 6)].mean())
    mx = 20.0
    mouth = _mouth(mx, y_tail + 6, 11, 30, 9, rot=RIDE_LEAN)
    bars = empty()
    for dy, x0, x1 in ((-15, mx + 18, x_tail + 26), (15, mx + 18, x_tail + 26)):
        y = y_tail + dy
        bars |= fill(rotate([(x0 + 4, y - 4.5), (x1, y - 4.5), (x1 - 4, y + 4.5), (x0, y + 4.5)],
                            RIDE_LEAN, x_tail, y_tail))
    keep = _grown(hull, 4)
    return hull | (mouth & ~keep) | (bars & ~keep & ~_grown(mouth, 3))


_GAP = "the hull's real mesh: its fins and pods float across even gaps by design (vessel-model SKILL 0.5)"
ICONS = [
    dict(name="Meerkat_SentinelMode", element="Charge", act="Sentinel Mode: the hull on sentry over the mouth it guards",
         draw=sentinel_mode, before=ICON_DIR + "Stoat_Ripple.png", exempt={"gap": _GAP, "components": _GAP}),
    dict(name="Meerkat_ExitPortal", element="Mass", act="Exit Portal: the way out, ringed by its six prisms",
         draw=exit_portal, before=ICON_DIR + "Stoat_Modes.png",
         exempt={"components": "the six prisms ARE the act (six on a hexagon, Garrett's kit), each two close triangles",
                 "gap": "the cut between the halves is the shape"}),
    dict(name="Meerkat_Soliton", element="Space", act="Soliton: the wormhole, an arch from mouth to throat to mouth",
         draw=soliton, before=ICON_DIR + "Stoat_ModeTime.png"),
    dict(name="Meerkat_FasterRide", element="Time", act="Faster Ride: the hull shooting out of a mouth",
         draw=faster_ride, before=ICON_DIR + "Stoat_Pathfinder.png", exempt={"gap": _GAP, "components": _GAP}),
]
ICONS += [
    dict(name="Meerkat_SixSentinels", element="Charge", act="Charge 5 Six Sentinels: the pool of sentinels doubles to six",
         draw=six_sentinels, exempt={"gap": _GAP, "components": "six sentinels stand apart: the pool IS the act"}),
    dict(name="Meerkat_DangerPortal", element="Mass", act="Mass 5 Danger Portal: the exit's six prisms are danger prisms",
         draw=danger_portal, exempt={"components": "the six danger prisms ARE the act (six on a hexagon)"}),
    dict(name="Meerkat_Steal", element="Space", act="Space 5 Steal: a rival's prism swallowed comes out as ours",
         draw=steal, exempt={"components": "the prism in, the arch, the prism out: the steal's three parts"}),
    dict(name="Meerkat_BoostOut", element="Time", act="Time 5 Boost Out: thrown out of every soliton at 1.5 x the exit speed",
         draw=boost_out, exempt={"gap": _GAP, "components": _GAP}),
]
ICONS += [dict(name="Meerkat_SolitonNetwork", element="Omni", family=False, draw=soliton_network,
               act="The OMNI crystal card: the Soliton Network, three solitons forking from a mouth (one white sprite)",
               exempt={"components": "a mouth at the heart and three tunnels to three mouths: the network's parts",
                       "gap": "each tunnel stands clear of the mouths it joins"})]
REBIND = []   # author_meerkat_assets.py binds these in MeerkatHUDVariant (meerkat_icon_ids.py: the same guids)
