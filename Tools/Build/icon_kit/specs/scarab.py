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


A = Scale(k=116 / (2 * REACH), ox=64.0)                      # the whole volume, true scale (hull ~11 px)
B = Scale(k=116 / (2 * PLATE_R), ox=22.0, behind=False)      # the front half, true scale (hull ~14 px)
C = Scale(k=116 / (2 * REACH), ox=64.0, hull_boost=2.0)      # A with the hull drawn 2x so it survives on the HUD


def thrown(cx, cy, s=9.0):
    return prism(cx, cy, s)


def shatter(cx, cy, s=9.0, spread=3.5):
    """A prism broken into its four quarters, each pushed out from the centre."""
    m = empty()
    for qx, qy in ((1, -1), (1, 1), (-1, 1), (-1, -1)):
        tri = [(cx, cy), (cx + qx * s * 0.8, cy), (cx, cy + qy * s)]
        m |= fill([(px + qx * spread, py + qy * spread) for px, py in tri])
    return m


def _parts(L, prisms, lone):
    """prisms: where the claimed prisms land (inside the volume, near its front); lone: the shielded one."""
    return dict(hull=L.hull, plate=L.plate, mirror=L.mirror, edges=L.edges,
                prisms=lambda: _clear_of(prisms(), L.plate()),
                shielded=lambda: thrown(*lone), shatter=lambda: shatter(*lone))


def _card(name, title, parts):
    states = [
        ("ready", [("edges", "white", 0.35), ("mirror", "white", 0.45), ("hull", "domain"), ("plate", "white")]),
        ("punching (lit): prisms thrown", [("edges", "white", 0.35), ("mirror", "white", 0.45), ("prisms", "blue"),
                                           ("hull", "domain"), ("plate", "white")]),
        ("recharging", [("edges", "white", 0.2), ("mirror", "white", 0.2), ("hull", "domain"), ("plate", "white", 0.3)]),
        ("below Charge 5: a shielded prism only sheds", [("edges", "white", 0.35), ("mirror", "white", 0.45),
                                                         ("shielded", "blue"), ("hull", "domain"), ("plate", "white")]),
        ("Charge 5, Cavitation Shear: it shatters", [("edges", "white", 0.35), ("mirror", "white", 0.45),
                                                     ("shatter", "shielded"), ("hull", "domain"), ("plate", "white")]),
    ]
    return dict(name=name, group="cavitation", lockup_state=1, element="Charge",
                before=ICON_DIR + "Scarab_CavitationBlast-PLACEHOLDER.png", title=title, parts=parts, states=states)


CARDS += [
    _card("charge_option_a",
          "A - TRUE SCALE, WHOLE VOLUME: the punch's real reach (54 ahead, mirrored 54 behind, 90 across) around a "
          "true-size Scarab; the front plate solid, its mirror faint",
          _parts(A, lambda: thrown(104, 34) | thrown(104, 94) | thrown(88, 64), (104, 64))),
    _card("charge_option_b",
          "B - TRUE SCALE, FRONT HALF: the Scarab at the left, the plate 54 ahead and 90 across - a bigger Scarab, "
          "the mirror half off the card",
          _parts(B, lambda: thrown(98, 30) | thrown(98, 98) | thrown(76, 64), (98, 64))),
    _card("charge_option_c",
          "C - A WITH THE HULL 2x: the volume true, the Scarab drawn twice its size so it still reads on the HUD",
          _parts(C, lambda: thrown(104, 34) | thrown(104, 94) | thrown(92, 64), (104, 64))),
]
