"""The Sparrow's ability-row icons (Resources/ElementalAbilityMaps/Sparrow.asset), angular, one object each.

  Charge  Skyburst Rockets   the rack: four rockets on a rail, as a LADDER of five sprites (0..4 held) the view
                             steps through (SparrowHUDView.SetMissilesFromAmmo01); a spent slot is a ghost, so
                             the count reads as "n of 4" and the card's own sprite is the full rack
                             (before: three 32 px rockets, then a three-rung ladder with a dark decagon PLATE
                             baked into the art, sitting on the lockup's own plate)
  Mass    Turret Stance      the hull planted on a base, a prism leaving its nose and swelling as it flies
                             (before: a generic "swap" arrow pair)
  Space   Pulsefire Cannons  the volley: three bullets in a diagonal line; the weapon-mode PAIR the view swaps
                             (SetWeaponMode): bullets while moving, prism rounds in the stance
                             (before: a 32 px bullet, and a FLAME for the stance)
  Time    Afterburner        the hull at full burn: the chevron with a long jagged plume behind it (before: the
                             fleet's borrowed boost icon; the roll ring the view draws round it is its own Image)

Ladder and weapon-mode sprites live in Ladder/ and Modes/; the ladder is family=False (rungs are variants).
"""
from canvas import fill, rect, rotate
from motifs import hull, prism

HULL = "Sparrow"
VARIANT = "Assets/_Prefabs/UI Elements/VesselHUD/SparrowHUDVariant.prefab"
ART = "Assets/_Graphics/Design Assests/HUD UI/"


def rocket(cx, cy, h):
    """A rocket, nose up: a pointed nose, a straight body, two swept fins. h = overall height."""
    w = h * 0.2
    return fill([(cx, cy - h / 2), (cx + w / 2, cy - h / 2 + h * 0.26), (cx + w / 2, cy + h * 0.2),
                 (cx + w * 1.0, cy + h / 2), (cx + w * 0.4, cy + h * 0.34), (cx - w * 0.4, cy + h * 0.34),
                 (cx - w * 1.0, cy + h / 2), (cx - w / 2, cy + h * 0.2), (cx - w / 2, cy - h / 2 + h * 0.26)])


def rockets(n, total=4):
    """The rack: `n` solid rockets and `total - n` ghosts on one rail, the row centred."""
    pitch = 26
    x0 = 64 - pitch * (total - 1) / 2
    rail = rect(12, 108, 116, 118)
    solid, ghost = rail, rect(0, 0, 0, 0)
    for i in range(total):
        r = rocket(x0 + i * pitch, 56, 92)
        if i < n:
            solid |= r
        else:
            ghost |= r
    return [(solid, 1.0), (ghost, 0.6)]   # a ghost at exactly 0.5 sits on the gates' threshold and vanishes at 40 px


def turret_stance():
    base = rect(30, 108, 98, 118)
    body = hull(64, 90, 30, heading=-90)
    small = prism(64, 52, 12)
    big = prism(64, 20, 15) & rect(0, 6, 128, 128)
    return base | body | small | big


def _diagonal(shape_at):
    m = rect(0, 0, 0, 0)
    for x, y, s in ((30, 98, 0.8), (64, 64, 1.0), (98, 30, 1.2)):
        m |= shape_at(x, y, s)
    return m


def cannons():
    def bullet(x, y, s):
        pts = [(x + px * s, y + py * s) for px, py in ((0, -22), (10, -11), (10, 20), (-10, 20), (-10, -11))]
        return fill(rotate(pts, 45, x, y))
    return _diagonal(bullet)


def prism_rounds():
    return _diagonal(lambda x, y, s: prism(x, y, 20 * s))


def afterburner():
    body = hull(94, 64, 28, heading=0)
    plume = fill([(68, 44), (40, 30), (50, 50), (8, 40), (34, 60), (4, 64), (34, 68), (8, 88), (50, 78), (40, 98), (68, 84)])
    return body | plume


ICONS = [
    dict(name="Sparrow_TurretStance", element="Mass", act="Turret Stance", draw=turret_stance, before=ART + "New_Sparrow/swap weapon  icon.png"),
    dict(name="Sparrow_Cannons", element="Space", act="Pulsefire Cannons", draw=cannons, before=ART + "New_Sparrow/bullet icon.png"),
    dict(name="Sparrow_Afterburner", element="Time", act="Afterburner", draw=afterburner, before=ART + "New_Sparrow/boost icon.png"),
    dict(name="Sparrow_Rockets_4", element="Charge", act="Skyburst Rockets: 4 of 4", draw=lambda: rockets(4), subdir="Ladder", family=False, before=ART + "Missiles/missileGauge_2.png"),
    dict(name="Sparrow_Rockets_3", element="Charge", act="rack: 3 of 4", draw=lambda: rockets(3), subdir="Ladder", family=False),
    dict(name="Sparrow_Rockets_2", element="Charge", act="rack: 2 of 4", draw=lambda: rockets(2), subdir="Ladder", family=False, before=ART + "Missiles/missileGauge_1.png"),
    dict(name="Sparrow_Rockets_1", element="Charge", act="rack: 1 of 4", draw=lambda: rockets(1), subdir="Ladder", family=False),
    dict(name="Sparrow_Rockets_0", element="Charge", act="rack: 0 of 4", draw=lambda: rockets(0), subdir="Ladder", family=False, before=ART + "Missiles/missileGauge_0.png"),
    dict(name="Sparrow_PrismRounds", element="Space", act="stance: prism rounds", draw=prism_rounds, subdir="Modes", family=False, before=ART + "New_Sparrow/fire icon.png"),
]

REBIND = [
    dict(file=VARIANT, old="7aededbf59d8deb4fb340521261c6efa", icon="Sparrow_Rockets_4", expect=1),      # missiles icon (the card)
    dict(file=VARIANT, old="4a5259289f0bdff46a3c22300b3933d7", icon="Sparrow_TurretStance", expect=1),   # swap weapon icon
    dict(file=VARIANT, old="6da9cfe8d216bd84695e3b4c71bfa533", icon="Sparrow_Cannons", expect=2),        # bullet icon: the card AND weaponModeIcons[0]
    dict(file=VARIANT, old="50344d4701b0890479c9a02018960d9b", icon="Sparrow_PrismRounds", expect=1),    # fire icon: weaponModeIcons[1]
    dict(file=VARIANT, old="f0197e0a2ce38654ea3d882fff64967a", icon="Sparrow_Afterburner", expect=2),    # boost icon: the Time card and the legacy BoostIcon image
    dict(file=VARIANT, list="missileIcons", icons=[f"Sparrow_Rockets_{i}" for i in range(5)]),
]
