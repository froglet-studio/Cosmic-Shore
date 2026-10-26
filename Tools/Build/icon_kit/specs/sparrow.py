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
  Time    Afterburner        the Sparrow's OWN mesh, from above, nose right: skinned into its authored SpeedUp take
                             (both fans folded into swept wings, the nose blades slid forward - mesh_pose.py) as the
                             card's sprite, and at rest (fans spread) as the part the HUD shows until the boost is
                             held - a live SWAP in the domain colour (Garrett 2026-10-26: "time first, build the
                             folded pose"; "make the vessel pointing to the right"; "A is the best"). The plate
                             shows NO meter: the roll pip that filled it is unbound ("lets not show that that is
                             armed because its cooldown is so low"). (before: round 2's chevron with a plume; before
                             that the fleet's borrowed boost icon)

Ladder and weapon-mode sprites live in Ladder/ and Modes/; the ladder is family=False (rungs are variants).
"""
import mesh_silhouette
from canvas import fill, rect, rotate
from motifs import hull, prism

HULL = "Sparrow"
VARIANT = "Assets/_Prefabs/UI Elements/VesselHUD/SparrowHUDVariant.prefab"
ART = "Assets/_Graphics/Design Assests/HUD UI/"
FBX = "Assets/_Models/Vessel Models/SparrowModel1.fbx"       # the live model (Docs/VesselModels/Sparrow.md §1)
BOOST_TAKE = "Sparrow_Armature+Mesh|SpeedUp_SparrowAnim"      # the fold (dossier §8.5: fans 36 -> 165 deg, blades forward)
# Plan view, no mirror: the pointed fuselage end lands on the RIGHT. (The dossier records that the artist modelled
# the hull tail-first - in flight the blade end leads - but the card shows the ship the way a ship reads.)
VIEW_PLAN = dict(pitch=0, yaw=0, dilate_px=1.0, mirror=False)


# The hull sits right of centre so its JETS have room behind it (Garrett 2026-10-26: "lets show some jets coming out
# of the sparrow so the default state communicates motion and doesn't just look like the vessel silhouette"). The
# engine faces sit at the blade end of the mesh (the dossier: z +2.7, behind the six nose blades), which is the LEFT
# end here - the jets stream from under the blades, two plumes at the engines' x (+-2.3 of the 6.25 half-width).
AB_HULL = dict(box=94, cx=74.0, cy=64.0)
AB_JET_Y = (64 - 14, 64 + 14)
AB_JET_X0 = 30                       # where the plumes leave the hull's tail end
AB_JET_LEN = dict(rest=20, boost=24)  # a plume's length: idling, and at full burn (the tip stays inside the optical box)


def sparrow(take=None, box=112, cx=64.0, cy=64.0):
    return mesh_silhouette.silhouette(FBX, box=box, cx=cx, cy=cy, take=take, **VIEW_PLAN)


def plume(x0, y, length, w0=10, w1=4):
    """One exhaust plume trailing LEFT from (x0, y): a bar tapering from w0 at the engine to w1 at its tip, with the
    tip cut square and the engine end chamfered - the style's angular language, no flame curls."""
    x1 = x0 - length
    c = 2.5
    return fill([(x0 - c, y - w0 / 2), (x0, y - w0 / 2 + c), (x0, y + w0 / 2 - c), (x0 - c, y + w0 / 2),
                 (x1, y + w1 / 2), (x1, y - w1 / 2)])


def jets(state):
    m = plume(AB_JET_X0, AB_JET_Y[0], AB_JET_LEN[state])
    return m | plume(AB_JET_X0, AB_JET_Y[1], AB_JET_LEN[state])


def afterburner_hull_rest():
    """The hull part at rest: fans spread, as it sits until the boost is held."""
    return sparrow(None, **AB_HULL)


def afterburner_hull_boost():
    """The hull part boosting: the SpeedUp take, fans folded."""
    return sparrow(BOOST_TAKE, **AB_HULL)


def afterburner_jets_rest():
    return jets("rest")


def afterburner_jets_boost():
    return jets("boost")


def afterburner():
    """The card's sprite (sheets, any single-image surface): the Sparrow folded at full burn."""
    return afterburner_hull_boost() | afterburner_jets_boost()


def afterburner_rest():
    """The rest composite, for the sheets."""
    return afterburner_hull_rest() | afterburner_jets_rest()


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


ICONS = [
    dict(name="Sparrow_TurretStance", element="Mass", act="Turret Stance", draw=turret_stance, before=ART + "New_Sparrow/swap weapon  icon.png"),
    dict(name="Sparrow_Cannons", element="Space", act="Pulsefire Cannons", draw=cannons, before=ART + "New_Sparrow/bullet icon.png"),
    dict(name="Sparrow_Afterburner", element="Time", act="Afterburner: the Sparrow folded at full burn (composite: the boost hull + jets)", draw=afterburner,
         exempt={"components": "a hull and two plumes", "gap": "the real mesh"}),
    dict(name="Sparrow_AfterburnerRest", element="Time", act="Afterburner at rest (composite: fans spread, idling jets)", draw=afterburner_rest, subdir="Parts", family=False,
         exempt={"gap": "Garrett 2026-10-26 - the real mesh; the spread fans' plates are the fidelity he asked for", "components": "a hull and two plumes"}),
    dict(name="Sparrow_AfterburnerHullRest", element="Time", act="Afterburner: the hull part at rest", draw=afterburner_hull_rest, subdir="Parts", family=False,
         exempt={"gap": "the real mesh", "box_fill": "a part: the hull leaves room for its jets", "in_box": "a part"}),
    dict(name="Sparrow_AfterburnerHullBoost", element="Time", act="Afterburner: the hull part boosting (SpeedUp take)", draw=afterburner_hull_boost, subdir="Parts", family=False,
         exempt={"gap": "the real mesh", "box_fill": "a part", "in_box": "a part", "components": "the nose blades slide forward in the take"}),
    dict(name="Sparrow_AfterburnerJetsRest", element="Time", act="Afterburner: the jets part, idling (white)", draw=afterburner_jets_rest, subdir="Parts", family=False,
         exempt={"coverage": "a part", "box_fill": "a part", "in_box": "a part", "components": "two plumes", "thin": "the plumes taper", "at40": "a part"}),
    dict(name="Sparrow_AfterburnerJetsBoost", element="Time", act="Afterburner: the jets part at full burn (white)", draw=afterburner_jets_boost, subdir="Parts", family=False,
         exempt={"coverage": "a part", "box_fill": "a part", "in_box": "a part", "components": "two plumes", "thin": "the plumes taper", "at40": "a part"}),
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
    # The Time card: the icon Image becomes a transparent container over the hull part the view tints with the
    # domain colour and swaps between the rest and boost sprites; the roll pip is unbound from the card's gauge
    # and its Image switched off; the pip's retired view fields are dropped from the block.
    dict(file=VARIANT, pair=dict(container="3258612490553136050", view="7983934951929746178", after="weaponModeIcon",
                                 children=[("AfterburnerJets", "Sparrow_AfterburnerJetsRest", "afterburnerJetsIcon"),
                                           ("AfterburnerHull", "Sparrow_AfterburnerHullRest", "afterburnerHullIcon")],
                                 binding=dict(element=4, gauge=None),
                                 fields={"afterburnerRestSprite": ("icon", "Sparrow_AfterburnerHullRest"),
                                         "afterburnerBoostSprite": ("icon", "Sparrow_AfterburnerHullBoost"),
                                         "afterburnerJetsRestSprite": ("icon", "Sparrow_AfterburnerJetsRest"),
                                         "afterburnerJetsBoostSprite": ("icon", "Sparrow_AfterburnerJetsBoost"),
                                         "rollChargeIndicator": None, "rollArmedColor": None, "rollSpentColor": None,
                                         "rollChargeTweenDuration": None, "rollSpendPunchScale": None})),
    dict(file=VARIANT, deactivate="6877628874251343197"),
]
