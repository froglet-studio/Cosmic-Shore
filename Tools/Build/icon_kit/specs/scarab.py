"""The Scarab's ability-row icons (Resources/ElementalAbilityMaps/Scarab.asset).

  Charge  Cavitation Blast  the mantis-shrimp punch that rides the committed right-stick dash: a swept PLATE (a disc
                            across the dash, its face normal the dash) sweeping from the hull, MIRRORED through the hull
                            so it reaches as far behind as ahead; everything it claims - prisms, fauna, a rival's
                            elements - is thrown the way the punch travelled, so mass BEHIND is dragged forward through
                            the ship. Charge shortens its cooldown (x1 -> x0.5); Charge 5, Cavitation Shear, destroys
                            SHIELDED prisms outright instead of only shedding their shields (ScarabCavitationBlast.cs).
  Mass    Switch            a ring the balls thread; Mass grows its radius
  Space   Ball Forge        fly through an omni crystal to mint a ball; Space grows it (x1 -> x4)
  Time    Throttle          analog right-trigger thrust along the nose; Time raises its top speed; Time 5 Snap Dash

Before: four PLACEHOLDER sprites (author_hull_icon_placeholders.py), three of them tinted as gauges by ScarabHUDView
(the blast ready / spent in two colours, the switch by charge count, the forge filling / ready). One card at a time
with Garrett, options in the hull's whole lockup.

The hull is the Scarab's own mesh from straight above, nose RIGHT (pitch 0, yaw 0; the horn and mandibles at +x - the
grid on 2026-10-28), flying across the card (LESSONS 55: the act runs across, clear of the plate's marks).
"""
import mesh_silhouette
from canvas import N, chamfered_rect, disc, empty, fill, rotate
from motifs import prism

import numpy as np
from PIL import Image

HULL = "Scarab"
ICON_DIR = "Assets/_Graphics/Icons/AbilityIcons/Scarab/"
FBX = "Assets/_Models/Vessel Models/Scarab.fbx"
VIEW_PLAN = dict(pitch=0, yaw=0, dilate_px=0.8)


def _png_mask(rel):
    import os
    root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))
    a = Image.open(os.path.join(root, rel)).convert("RGBA").split()[3].resize((N, N), Image.LANCZOS)
    return np.asarray(a) > 127


def placeholder(name):
    return lambda: _png_mask(ICON_DIR + f"Scarab_{name}-PLACEHOLDER.png")


def scarab(box=60, cx=64.0, cy=64.0):
    return mesh_silhouette.silhouette(FBX, box=box, cx=cx, cy=cy, **VIEW_PLAN)


def _clear_of(mask, *others, px=2.5):
    import toy_portraits
    keep = mask.copy()
    for o in others:
        keep &= ~toy_portraits.grown(o, px)
    return keep


ICONS = []
LOCKUP = dict(
    Charge=dict(parts=dict(i=placeholder("CavitationBlast")), state=[("i", "white")]),
    Mass=dict(parts=dict(i=placeholder("Switch")), state=[("i", "white")]),
    Space=dict(parts=dict(i=placeholder("BallForge")), state=[("i", "white")]),
    Time=dict(parts=dict(i=placeholder("Throttle")), state=[("i", "white")]),
)
CARDS = []
REBIND = []


# ---- Charge: Cavitation Blast options, TO SCALE (2026-10-28) ------------------------------------------------------
# Garrett: "show the relative size of the volume affected relative to the scarab". Measured from the prefabs:
#   hull radius r = 4.5 (ScarabHull's SphereCollider - MeasureHullRadius takes the largest hull collider; scale 1),
#   plate radius R = radiusPerVesselRadius 10 x r = 45 (Scarab.prefab), length = lengthPerRadius 1.2 x R = 54 ahead,
#   mirrored (mirrorAboutStartPlane 1, AOEScarabCavitation.prefab) = 54 behind: the swept volume seen from above is a
#   rectangle 2.4R = 108 long by 2R = 90 wide, centred on the hull. The mesh (hull_view, file frame) is 10.6 long by
#   9.7 wide - the punch is ~10 Scarabs long. The plate is edge-on from above: a bar across the dash.
# Colours: the hull is yours (domain), the plate white (the punch), the volume's edges white and faint, the prisms it
# throws the HUD blue; a shielded prism the shielded colour, shattered at Charge 5, only shed below it.
CY = 64.0
HULL_R = 4.5
PLATE_R = 10 * HULL_R                 # 45
REACH = 1.2 * PLATE_R                 # 54 each way
HULL_EXTENT = 10.58                   # the mesh's longest side (nose axis), what silhouette() fits to `box`
HULL_NOSE_OFFSET = 0.47               # the mesh bbox centre sits this far ahead of the hull's origin
EDGE_W = 2.0                          # the volume's edge lines
PLATE_W = 4.0


def bar(x0, x1, y0, y1):
    return fill([(x0, y0), (x1, y0), (x1, y1), (x0, y1)])


class Scale:
    """One to-scale layout: k icon px per metre, the hull's origin at (ox, CY)."""

    def __init__(self, k, ox, hull_boost=1.0, behind=True):
        self.k, self.ox, self.boost, self.behind = k, ox, hull_boost, behind

    def x(self, m):
        return self.ox + m * self.k

    def hull(self):
        return scarab(box=HULL_EXTENT * self.k * self.boost, cx=self.x(HULL_NOSE_OFFSET * self.boost), cy=CY)

    def half(self):
        return PLATE_R * self.k

    def plate(self):
        x = self.x(REACH)
        return bar(x - PLATE_W, x, CY - self.half(), CY + self.half())

    def mirror(self):
        x = self.x(-REACH)
        return bar(x, x + PLATE_W, CY - self.half(), CY + self.half()) if self.behind else empty()

    def edges(self):
        x0 = self.x(-REACH) + (PLATE_W if self.behind else -self.k * 8)
        x1 = self.x(REACH) - PLATE_W - 1.5
        h = self.half()
        return _clear_of(bar(x0, x1, CY - h, CY - h + EDGE_W) | bar(x0, x1, CY + h - EDGE_W, CY + h), self.hull())

    def field(self, holes=empty):
        """The swept volume as ONE flat shape (Garrett: no box, flat): from the mirror end (or the card's edge) to a
        gap short of the plate, the hull knocked out of it with a margin, and what it claims cut out as NEGATIVE
        space - the prisms are holes in the punch."""
        x0 = self.x(-REACH) if self.behind else 0.0
        x1 = self.x(REACH) - PLATE_W - 2.5
        h = self.half()
        return _clear_of(bar(x0, x1, CY - h, CY + h), self.hull(), px=2.5) & ~holes()


# Round 3 (Garrett: "a is good but ... more cohesive with the rest of our polished icons"). What the polished cards
# share (sheets rhino.png, sparrow.png): ONE bold white mass in the middle of the card, the parts separated by
# knock-out gaps rather than outlines, no grey fills (grey means "not ready" - the Sparrow's spent rockets), the
# small things chunky enough to survive at 40 px. So A becomes a solid WHITE volume, still to scale, with the plate
# its front edge behind a knock-out groove, the hull in domain colour in its own knock-out, and what the punch
# shatters cut out as negative space. Three treatments of that mass:
L = Scale(k=92 / (2 * REACH), ox=60.0)      # the volume 92 long (14..106), 77 across; true-scale hull ~9 px
GROOVE = 3.0


def _slab():
    """A1: the swept cylinder from above, its corners chamfered like the fleet's plates."""
    x0, x1, h = L.x(-REACH), L.x(REACH) - PLATE_W - GROOVE, L.half()
    return chamfered_rect(x0, CY - h, x1, CY + h, 7.0)


def _bands():
    """A2: the slab as the plate's WAKE - horizontal strokes, the gaps the water the plate pushed through
    (the Sparrow's jets and rounds are drawn this way); full length, the volume's true extent."""
    x0, x1, h = L.x(-REACH), L.x(REACH) - PLATE_W - GROOVE, L.half()
    m, n, gap = empty(), 7, 3.0
    bh = (2 * h - (n - 1) * gap) / n
    for i in range(n):
        y0 = CY - h + i * (bh + gap)
        m |= fill([(x0, y0), (x1, y0), (x1, y0 + bh), (x0, y0 + bh)])
    return m


def _bubble():
    """A3: the cavitation bubble - the slab with its rear end rounded off (the mirror half fading into the wake),
    the front square against the plate."""
    x0, x1, h = L.x(-REACH), L.x(REACH) - PLATE_W - GROOVE, L.half()
    r = h * 0.55
    m = fill([(x0 + r, CY - h), (x1, CY - h), (x1, CY + h), (x0 + r, CY + h)])
    m |= fill([(x0, CY - h + r), (x0 + r + 1, CY - h + r), (x0 + r + 1, CY + h - r), (x0, CY + h - r)])
    m |= disc(x0 + r, CY - h + r, r) | disc(x0 + r, CY + h - r, r)
    return m


def plate():
    x = L.x(REACH)
    return bar(x - PLATE_W, x, CY - L.half(), CY + L.half())


def whole(cx, cy, s=7.0):
    """An untouched prism: outside the volume, the punch never reached it."""
    return prism(cx, cy, s)


def shards(cx, cy, s=12.0):
    """A prism the punch claimed, broken and flung the way the punch travels (+x): three slivers, each a wedge
    pointing forward, fanned in a chevron with the middle one furthest ahead - the break reads as a direction,
    where a burst of four quarters reads as an explosion in place."""
    m = empty()
    for dx, dy in ((-4.0, -8.0), (4.0, 0.0), (-4.0, 8.0)):
        x, y = cx + dx, cy + dy
        m |= fill([(x - s * 0.6, y - 3.2), (x + s * 0.7, y), (x - s * 0.6, y + 3.2)])
    return m


INSIDE = lambda: shards(72, 42) | shards(72, 86)
LONE = (78, 64)
OUTSIDE = lambda: whole(120, 64) | whole(26, 13) | whole(98, 115)


def _parts(shape):
    def field(holes=empty):
        return _clear_of(shape(), L.hull(), px=2.5) & ~holes()
    return dict(hull=L.hull, plate=plate, whole=lambda: _clear_of(OUTSIDE(), plate()),
                field=field, field_shatter=lambda: field(INSIDE),
                field_shed=lambda: field(lambda: prism(*LONE, 10.0)),
                field_shear=lambda: field(lambda: shards(*LONE)))


def _card(name, title, parts):
    top = [("whole", "blue"), ("hull", "domain"), ("plate", "white")]
    states = [
        ("ready", [("field", "white")] + top),
        ("punching (lit): what it holds shatters", [("field_shatter", "white")] + top),
        ("recharging", [("field", "white", 0.3), ("whole", "blue"), ("hull", "domain"), ("plate", "white", 0.3)]),
        ("below Charge 5: a shielded prism only sheds", [("field_shed", "white")] + top),
        ("Charge 5, Cavitation Shear: it shatters", [("field_shear", "white")] + top),
    ]
    return dict(name=name, group="cavitation", lockup_state=1, element="Charge",
                before=ICON_DIR + "Scarab_CavitationBlast-PLACEHOLDER.png", title=title, parts=parts, states=states)


CARDS += [
    _card("charge_option_a1", "A1 - THE SLAB: A as one solid white mass with chamfered corners, the plate behind a "
          "groove, shattered prisms cut out; recharging greys like the Sparrow's spent rockets", _parts(_slab)),
    _card("charge_option_a2", "A2 - THE WAKE: the same volume as horizontal strokes (the plate's wake), the shards "
          "cut through them", _parts(_bands)),
    _card("charge_option_a3", "A3 - THE BUBBLE: the slab with its rear rounded - the cavitation bubble, square only "
          "against the plate", _parts(_bubble)),
]
