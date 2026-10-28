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


# ---- Charge: Cavitation Blast options (2026-10-28) -----------------------------------------------------------------
# The plate seen from above is EDGE-ON: a bar across the dash. Prisms are the fleet's corner-on diamond (VOCABULARY).
# Colours: the hull is yours (domain), the plate white (the punch itself), the prisms it throws the HUD blue
# (untouched mass), a shielded prism the shielded colour; at Charge 5 that prism is shattered, below it only shed.
CY = 64.0
PLATE_H = 104.0          # the disc's diameter on the card (it is ~10 hull radii across in the game - drawn as a bar
                        # that dwarfs the hull)
PLATE_W = 9.0


def plate_bar(x, h=PLATE_H, w=PLATE_W, cy=CY):
    return fill([(x - w / 2, cy - h / 2), (x + w / 2, cy - h / 2), (x + w / 2, cy + h / 2), (x - w / 2, cy + h / 2)])


def plate_lens(x, h=PLATE_H, depth=12.0, w=7.0, cy=CY):
    """B: the plate's front bowed forward - the cavitation bubble's face, still a bar of one weight."""
    out, inn = [], []
    for k in range(17):
        t = -1 + 2 * k / 16
        y = cy + t * h / 2
        out.append((x + depth * (1 - t * t), y))
        inn.append((x + depth * (1 - t * t) - w, y))
    return fill(out + inn[::-1])


def sweep_band(x0, x1, h=PLATE_H, cy=CY):
    """The volume the plate sweeps (a ghost): from x0 to x1, the plate's height."""
    return fill([(x0, cy - h / 2), (x1, cy - h / 2), (x1, cy + h / 2), (x0, cy + h / 2)])


def thrown(cx, cy, s=11.0, lean=0.0):
    return prism(cx, cy, s) if not lean else fill(rotate([(cx, cy - s), (cx + s * 0.8, cy), (cx, cy + s), (cx - s * 0.8, cy)], lean, cx, cy))


def shatter(cx, cy, s=11.0, spread=4.0):
    """A prism broken into its four quarters, each pushed out from the centre."""
    m = empty()
    for qx, qy in ((1, -1), (1, 1), (-1, 1), (-1, -1)):
        tri = [(cx, cy), (cx + qx * s * 0.8, cy), (cx, cy + qy * s)]
        m |= fill([(px + qx * spread, py + qy * spread) for px, py in tri])
    return m


# A: THE SLAP - the hull centred; the plate at the front of its sweep, the sweep a ghost through the hull and as far
# behind (the mirror); two prisms, one ahead and one BEHIND, both thrown right.
A_HULL = dict(box=66, cx=50.0, cy=CY)
A_PLATE_X = 94.0


def a_hull():
    return scarab(**A_HULL)


def a_plate():
    return _clear_of(plate_bar(A_PLATE_X), a_hull())


def a_sweep():
    return _clear_of(sweep_band(16.0, A_PLATE_X - PLATE_W / 2), a_hull(), plate_bar(A_PLATE_X))


def a_prisms():
    return thrown(112, 30) | thrown(112, 98)


def a_shielded():
    return thrown(112, 30)


def a_shatter():
    return shatter(112, 30)


# B: THE PUNCH - the hull at the left, the bowed plate ahead of its nose, three prisms flung on past it.
B_HULL = dict(box=62, cx=36.0, cy=CY)
B_PLATE_X = 72.0


def b_hull():
    return scarab(**B_HULL)


def b_plate():
    return _clear_of(plate_lens(B_PLATE_X), b_hull())


def b_prisms():
    return thrown(104, 28) | thrown(110, 64) | thrown(104, 100)


def b_shielded():
    return thrown(110, 64)


def b_shatter():
    return shatter(110, 64)


# C: THE DRAG - A without the prism ahead: one prism behind the hull pulled THROUGH it to the front, its old place a
# ghost - the mirror half is what makes this punch the Scarab's.
def c_ghost_behind():
    return _clear_of(thrown(12, 64), a_hull())


def c_prism_front():
    return thrown(112, 64)


def c_shatter():
    return shatter(112, 64)


def _card(name, title, parts, prisms_key, shielded_key, shatter_key, extra=()):
    base = [(k, c, *a) for k, c, *a in extra]
    states = [
        ("ready", base + [("hull", "domain"), ("plate", "white")]),
        ("punching (lit): prisms thrown", base + [("sweep", "white", 0.3), (prisms_key, "blue"), ("hull", "domain"), ("plate", "white")]),
        ("recharging", base + [("hull", "domain"), ("plate", "white", 0.3)]),
        ("below Charge 5: a shielded prism only sheds", base + [("sweep", "white", 0.3), (shielded_key, "blue"), ("hull", "domain"), ("plate", "white")]),
        ("Charge 5, Cavitation Shear: it shatters", base + [("sweep", "white", 0.3), (shatter_key, "shielded"), ("hull", "domain"), ("plate", "white")]),
    ]
    if "sweep" not in parts:
        states = [(n, [l for l in L if l[0] != "sweep"]) for n, L in states]
    return dict(name=name, group="cavitation", lockup_state=1, element="Charge",
                before=ICON_DIR + "Scarab_CavitationBlast-PLACEHOLDER.png", title=title, parts=parts, states=states)


CARDS += [
    _card("charge_option_a",
          "A - THE SLAP: the Scarab from above, the plate edge-on ahead of the nose, as tall as the punch is wide; the prisms it throws on past it",
          dict(hull=a_hull, plate=a_plate, prisms=a_prisms, shielded=a_shielded, shatter=a_shatter),
          "prisms", "shielded", "shatter"),
    _card("charge_option_b",
          "B - THE PUNCH: the Scarab at the left, the plate bowed forward off its nose, three prisms flung on past it",
          dict(hull=b_hull, plate=b_plate, prisms=b_prisms, shielded=b_shielded, shatter=b_shatter),
          "prisms", "shielded", "shatter"),
    _card("charge_option_c",
          "C - THE DRAG: A with one prism, pulled from BEHIND the hull through it to the front (its old place a ghost) - the mirror half is this punch's own",
          dict(hull=a_hull, plate=a_plate, prisms=c_prism_front, ghost=c_ghost_behind,
               shielded=c_prism_front, shatter=c_shatter),
          "prisms", "shielded", "shatter"),
]
CARDS[-1]["states"][1][1].insert(0, ("ghost", "blue", 0.4))
