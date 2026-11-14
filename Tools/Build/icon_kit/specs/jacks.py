"""The Jacks' ability row (R_VesselActions/JACKS.md §1-2, round 10-11) through the REAL hull (`Jack.fbx`, side-on).

The roster pass (2026-11-07). `author_jacks_icons.py`'s glyphs (2026-10-21) still drew the kit before round 10: Breeding,
a Herding Ring and Herd Speed. Round 10 made every control a card - Mass is The Call, Space is Wide, Time is Decoys -
and these draw that kit. Single white sprites bound by guid in `Jacks.prefab` (`author_jacks_assets.py`
ICON_SPRITES); the files were renamed to the cards (`Jacks_TheCall`, `Jacks_Wide`, `Jacks_Decoys`) with their metas,
so the guids hold. Picks made by the session on Garrett's go-ahead to choose:

  Charge  Danger Share  kept: `author_jacks_icons.py`'s fish struck through by a bolt still names the card (a spiked
                        creature stinging a rival hull was drawn and read worse at 40 px)
  Mass    The Call      the two jacks (above and below, facing in) and the herd called into a knot between them
  Space   Wide          the view eased out: the whole cell's corner brackets round the herding view's (faint), the
                        herd inside
  Time    Decoys        a jack and its decoy (a ghost of the jack) up and ahead of it: the decoy a trigger lays, and
                        flies the hull to

A herd creature is a lens body with a forked tail (the Jacks' fauna); a jack is the real mesh, so a creature never
passes for a jack.
"""
import math

import numpy as np

import mesh_silhouette
from canvas import SS, empty, fill, rotate, stroke

HULL = "Jacks"
JACK = "Assets/_Models/Vessel Models/Jack.fbx"
ICON_DIR = "Assets/_Graphics/Icons/AbilityIcons/Jacks/"
APART = {"components": "a herd is many separate creatures, and the two jacks stand apart (the act is the gathering)",
         "gap": "the jack's real mesh: its fins float clear of the body (vessel-model SKILL 0.5)"}


def jack(box, cx, cy, mirror=False):
    """The jack side-on, nose right (mirror: nose left)."""
    return mesh_silhouette.silhouette(JACK, pitch=-90, yaw=0, box=box, cx=cx, cy=cy, mirror=mirror, dilate_px=1.6)


def grow(mask, px):
    from PIL import Image, ImageFilter
    r = int(px * SS) * 2 + 1
    return np.asarray(Image.fromarray((mask * 255).astype("uint8")).filter(ImageFilter.MaxFilter(r))) > 127


def creature(cx, cy, length, height, ang=0.0):
    """A herd creature: a lens body nosed right and a forked tail, turned `ang` degrees clockwise."""
    n, x0 = 20, cx - length * 0.22
    top = [(x0 + length * 0.72 * i / n - length * 0.28, cy - height / 2 * math.sin(math.pi * i / n)) for i in range(n + 1)]
    bot = [(x0 + length * 0.72 * i / n - length * 0.28, cy + height / 2 * math.sin(math.pi * i / n)) for i in range(n, -1, -1)]
    tx = x0 - length * 0.28
    tail = [(tx + 2, cy), (tx - length * 0.26, cy - height * 0.5), (tx - length * 0.17, cy), (tx - length * 0.26, cy + height * 0.5)]
    return fill(rotate(top + bot, ang, cx, cy)) | fill(rotate(tail, ang, cx, cy))


def the_call():
    a, b = jack(52, 38, 22), jack(52, 90, 106, mirror=True)
    herd = empty()
    for x, y in ((50, 54), (78, 64), (52, 76)):
        herd |= creature(x, y, 34, 18)
    return a | b | (herd & ~grow(a | b, 3))


def wide():
    full, view = empty(), empty()
    for sx in (-1, 1):
        for sy in (-1, 1):
            cx, cy = 64 + sx * 48, 64 + sy * 48
            full |= stroke([(cx, cy - sy * 22), (cx, cy), (cx - sx * 22, cy)], w=11)
            cx, cy = 64 + sx * 28, 64 + sy * 28
            view |= stroke([(cx, cy - sy * 11), (cx, cy), (cx - sx * 11, cy)], w=6)
    herd = creature(54, 54, 30, 17) | creature(76, 65, 30, 17) | creature(54, 77, 30, 17)
    return [(full | herd, 1.0), (view & ~grow(herd, 2), 0.45)]


def decoys():
    j = jack(78, 52, 76)
    decoy = jack(78, 76, 48) & ~grow(j, 2.5)
    return [(j, 1.0), (decoy, 0.55)]


# ---- level 5 (the vessel pass, 2026-11-13): each the binding's upgradedSprite, swapped in when the upgrade unlocks --
def attack():
    """Charge 5 ATTACK: the herd turns on a rival - three creatures converging, noses on a rival hull (the fleet hull)."""
    import motifs
    tx, ty = 98.0, 36.0
    rival = motifs.hull(tx, ty, 24, heading=-90)
    herd = empty()
    for x, y in ((62, 70), (88, 84), (44, 54)):
        herd |= creature(x, y, 40, 22, ang=math.degrees(math.atan2(ty - y, tx - x)))
    return rival | (herd & ~grow(rival, 4))


def mass_clusters():
    """Mass 5 MASS CLUSTERS: a herd creature laying a cluster of prisms behind it."""
    import motifs
    c = creature(84, 40, 64, 34, ang=0)
    pile = empty()
    for x, y in ((26, 84), (54, 84), (40, 106), (68, 106)):
        pile |= motifs.prism(x, y, 16)
    return c | (pile & ~grow(c, 3))


def eat_shields():
    """Space 5 EAT SHIELDS: a creature biting into a shielded prism, the shield's ring broken where it bites."""
    import motifs
    from canvas import band
    c = creature(56, 64, 50, 28, ang=0)
    shield = band(88, 64, 28, -140, 140, w=7)
    core = motifs.prism(90, 64, 20)
    return c | ((shield | core) & ~grow(c, 4))


def second_decoy():
    """Time 5 SECOND DECOY: a jack and TWO decoys (ghosts of the jack), one for each trigger."""
    j = jack(68, 46, 84)
    d1 = jack(54, 34, 34) & ~grow(j, 2.5)
    d2 = jack(54, 94, 34) & ~grow(j | d1, 2.5)
    return [(j, 1.0), (d1 | d2, 0.55)]


ICONS = [
    dict(name="Jacks_TheCall", element="Mass", act="The Call: the herd called to the point between the jacks",
         draw=the_call, exempt=APART),
    dict(name="Jacks_Wide", element="Space", act="Wide: the view eased out to the whole cell", draw=wide, exempt=APART),
    dict(name="Jacks_Decoys", element="Time", act="Decoys: the decoy a trigger lays, and flies the jacks to",
         draw=decoys, exempt=APART),
    dict(name="Jacks_Attack", element="Charge", act="Charge 5 Attack: the herd goes for rivals and bites them",
         draw=attack, exempt=APART),
    dict(name="Jacks_MassClusters", element="Mass", act="Mass 5 Mass Clusters: a creature lays a cluster of our prisms",
         draw=mass_clusters, exempt=APART),
    dict(name="Jacks_EatShields", element="Space", act="Space 5 Eat Shields: the herd eats shielded prisms",
         draw=eat_shields, exempt=APART),
    dict(name="Jacks_SecondDecoy", element="Time", act="Time 5 Second Decoy: a decoy for each trigger",
         draw=second_decoy, exempt=APART),
]
