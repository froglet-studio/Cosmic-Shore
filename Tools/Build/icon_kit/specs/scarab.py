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
from canvas import N, empty, fill, rotate
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


A = Scale(k=96 / (2 * REACH), ox=62.0)                       # the whole volume, true scale, a margin outside it
B = Scale(k=84 / (2 * PLATE_R), ox=18.0, behind=False)      # the front half, true scale (hull ~10 px)
C = Scale(k=96 / (2 * REACH), ox=62.0, hull_boost=2.0)      # A with the hull drawn 2x so it survives on the HUD


def whole(cx, cy, s=7.0):
    """An untouched prism: outside the volume, the punch never reached it."""
    return prism(cx, cy, s)


def shards(cx, cy, s=8.0):
    """A prism the punch claimed, broken and flung the way the punch travels (+x): three slivers, each a wedge
    pointing forward, fanned in a chevron with the middle one furthest ahead - the break reads as a direction,
    where a burst of four quarters reads as an explosion in place."""
    m = empty()
    for dx, dy in ((-3.0, -5.5), (3.0, 0.0), (-3.0, 5.5)):
        x, y = cx + dx, cy + dy
        m |= fill([(x - s * 0.6, y - 2.2), (x + s * 0.7, y), (x - s * 0.6, y + 2.2)])
    return m


def _parts(L, outside, inside, lone):
    """outside: whole prisms beyond the volume; inside: the prisms it shatters; lone: the shielded one inside."""
    return dict(hull=L.hull, plate=L.plate, mirror=L.mirror, edges=L.edges,
                whole=lambda: _clear_of(outside(), L.plate(), L.edges()),
                prisms=lambda: _clear_of(inside(), L.plate(), L.hull()),
                shielded=lambda: prism(*lone, 8.0), shatter=lambda: shards(*lone))


def _card(name, title, parts):
    vol = [("edges", "white", 0.35), ("mirror", "white", 0.45)]
    top = [("hull", "domain"), ("plate", "white")]
    states = [
        ("ready", vol + [("whole", "blue")] + top),
        ("punching (lit): what it holds shatters", vol + [("whole", "blue"), ("prisms", "blue")] + top),
        ("recharging", [("edges", "white", 0.2), ("mirror", "white", 0.2), ("whole", "blue"), ("hull", "domain"),
                        ("plate", "white", 0.3)]),
        ("below Charge 5: a shielded prism only sheds", vol + [("whole", "blue"), ("shielded", "blue")] + top),
        ("Charge 5, Cavitation Shear: it shatters", vol + [("whole", "blue"), ("shatter", "shielded")] + top),
    ]
    return dict(name=name, group="cavitation", lockup_state=1, element="Charge",
                before=ICON_DIR + "Scarab_CavitationBlast-PLACEHOLDER.png", title=title, parts=parts, states=states)


_OUT = lambda: whole(121, 64) | whole(30, 11) | whole(96, 117)      # beyond the plate, above and below the volume
CARDS += [
    _card("charge_option_a",
          "A - TRUE SCALE, WHOLE VOLUME: whole prisms outside the punch's reach, shattered ones inside flung forward; "
          "the Scarab true size (54 ahead, mirrored 54 behind, 90 across)",
          _parts(A, _OUT, lambda: shards(80, 42) | shards(80, 86) | shards(28, 64), (82, 64))),
    _card("charge_option_b",
          "B - TRUE SCALE, FRONT HALF: the Scarab at the left, the plate 54 ahead; shattered prisms inside, whole ones "
          "past the plate and outside its edges",
          _parts(B, lambda: whole(110, 40) | whole(116, 88) | whole(56, 12),
                 lambda: shards(48, 42) | shards(56, 86), (52, 64))),
    _card("charge_option_c",
          "C - A WITH THE HULL 2x: the volume true, the Scarab drawn twice its size so it still reads on the HUD",
          _parts(C, _OUT, lambda: shards(80, 42) | shards(80, 86) | shards(28, 64), (84, 64))),
]
