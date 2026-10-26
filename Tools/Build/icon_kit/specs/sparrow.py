"""The Sparrow's ability-row icons (Resources/ElementalAbilityMaps/Sparrow.asset), angular, one object each.

  Charge  Skyburst Rockets   the rack: four rockets on a rail, as a LADDER of five sprites (0..4 held) the view
                             steps through (SparrowHUDView.SetMissilesFromAmmo01); a spent slot is a ghost, so
                             the count reads as "n of 4" and the card's own sprite is the full rack
                             (before: three 32 px rockets, then a three-rung ladder with a dark decagon PLATE
                             baked into the art, sitting on the lockup's own plate)
  Mass    Turret Stance      the Sparrow from above (fans spread: it has STOPPED), firing PRISM rounds from both
                             gun pods at their real spacing - a round, then the same round swollen (Mass is the
                             substance). Live parts: the rounds are a GHOST while flying, solid in the stance
                             (the stance event), and at Mass 5 they become octahedrons in the domain's shielded
                             colour (the upgrade hook) - Garrett 2026-10-26: "b is good". (before: round 2's hull
                             planted on a base with a prism leaving its nose; before that a "swap" arrow pair)
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
from canvas import empty, fill, rect, rotate
from motifs import block, hull, prism

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
# end here - the jets stream from under the blades, two plumes close together on the centreline, in the HUD's BLUE
# (ElementalBarsConfigSO.BlueColor, the view tints the part): "give them a blue tint".
AB_HULL = dict(box=90, cx=76.0, cy=64.0)
AB_JET_Y = (64 - 7, 64 + 7)             # "bring the jets together" (Garrett 2026-10-26): a 6 px gap at the engines
AB_JET_X0 = 32                       # where the plumes leave the hull's tail end
AB_JET_LEN = dict(rest=14, boost=26)  # a plume's length: idling, and at full burn - nearly double ("lengthen them when using the boost"); the tip stays inside the optical box


def sparrow(take=None, box=112, cx=64.0, cy=64.0):
    return mesh_silhouette.silhouette(FBX, box=box, cx=cx, cy=cy, take=take, **VIEW_PLAN)


def plume(x0, y, length, w0=8, w1=3):
    """One exhaust plume trailing LEFT from (x0, y): a bar tapering from w0 at the engine to w1 at its tip, with the
    tip cut square and the engine end chamfered - the style's angular language, no flame curls."""
    x1 = x0 - length
    c = 2.5
    return fill([(x0 - c, y - w0 / 2), (x0, y - w0 / 2 + c), (x0, y + w0 / 2 - c), (x0 - c, y + w0 / 2),
                 (x1, y + w1 / 2), (x1, y - w1 / 2)])


def jets(state):
    m = plume(AB_JET_X0, AB_JET_Y[0], AB_JET_LEN[state])
    return m | plume(AB_JET_X0, AB_JET_Y[1], AB_JET_LEN[state])


# ---- Turret Stance (Mass): the stopped hull and the rounds from both pods
TS_HULL = dict(box=80, cx=46.0, cy=64.0)
TS_POD_Y = (45, 83)                 # the gun pods at x +-3.0 of the 6.25 half-width, at this box: +-19 px
TS_ROUND = ((92, dict(w=8, h=5, dp=3), 6), (109, dict(w=13, h=9, dp=4), 10))   # (cx, block, octahedron s): a round, then swollen


def turret_stance_hull():
    """The hull part: the Sparrow stopped, fans spread (the rest pose)."""
    return sparrow(None, **TS_HULL)


def turret_stance_rounds():
    """The rounds part: bare prisms (blocks) from both pods, the second swollen."""
    m = empty()
    for y in TS_POD_Y:
        for cx, b, _s in TS_ROUND:
            m |= block(cx, y, **b)
    return m


def turret_stance_rounds_shielded():
    """The rounds at Mass 5: octahedrons - a shielded prism reads as a diamond (the Squirrel's drift trail)."""
    m = empty()
    for y in TS_POD_Y:
        for cx, _b, s_ in TS_ROUND:
            m |= prism(cx, y, s_)
    return m


def turret_stance():
    """The card's sprite (sheets, any single-image surface): in the stance, bare rounds flying."""
    return turret_stance_hull() | turret_stance_rounds()


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
    dict(name="Sparrow_TurretStance", element="Mass", act="Turret Stance: the stopped Sparrow firing swelling rounds from both pods (composite)", draw=turret_stance,
         exempt={"gap": "Garrett 2026-10-26 - the real mesh", "components": "a hull and four rounds", "specks": "the blocks' seam slivers"}),
    dict(name="Sparrow_TurretStanceHull", element="Mass", act="Turret Stance: the hull part (stopped, fans spread)", draw=turret_stance_hull, subdir="Parts", family=False,
         exempt={"gap": "the real mesh", "box_fill": "a part: the hull leaves room for the rounds", "in_box": "a part"}),
    dict(name="Sparrow_TurretStanceRounds", element="Mass", act="Turret Stance: the rounds part, bare prisms (ghost while flying, solid in the stance)", draw=turret_stance_rounds, subdir="Parts", family=False,
         exempt={"coverage": "a part", "box_fill": "a part", "in_box": "a part", "components": "four rounds", "specks": "seam slivers", "gap": "seams", "thin": "seams", "at40": "the seams close at 40 px, the blocks stay"}),
    dict(name="Sparrow_TurretStanceRoundsShielded", element="Mass", act="Turret Stance: the rounds part at Mass 5 (octahedrons, the shielded colour)", draw=turret_stance_rounds_shielded, subdir="Parts", family=False,
         exempt={"coverage": "a part", "box_fill": "a part", "in_box": "a part", "components": "four rounds"}),
    dict(name="Sparrow_Cannons", element="Space", act="Pulsefire Cannons", draw=cannons, before=ART + "New_Sparrow/bullet icon.png"),
    dict(name="Sparrow_Afterburner", element="Time", act="Afterburner: the Sparrow folded at full burn (composite: the boost hull + jets)", draw=afterburner,
         exempt={"components": "a hull and two plumes", "gap": "the real mesh"}),
    dict(name="Sparrow_AfterburnerRest", element="Time", act="Afterburner at rest (composite: fans spread, idling jets)", draw=afterburner_rest, subdir="Parts", family=False,
         exempt={"gap": "Garrett 2026-10-26 - the real mesh; the spread fans' plates are the fidelity he asked for", "components": "a hull and two plumes"}),
    dict(name="Sparrow_AfterburnerHullRest", element="Time", act="Afterburner: the hull part at rest", draw=afterburner_hull_rest, subdir="Parts", family=False,
         exempt={"gap": "the real mesh", "box_fill": "a part: the hull leaves room for its jets", "in_box": "a part"}),
    dict(name="Sparrow_AfterburnerHullBoost", element="Time", act="Afterburner: the hull part boosting (SpeedUp take)", draw=afterburner_hull_boost, subdir="Parts", family=False,
         exempt={"gap": "the real mesh", "box_fill": "a part", "in_box": "a part", "components": "the nose blades slide forward in the take"}),
    dict(name="Sparrow_AfterburnerJetsRest", element="Time", act="Afterburner: the jets part, idling (the HUD blue)", draw=afterburner_jets_rest, subdir="Parts", family=False,
         exempt={"coverage": "a part", "box_fill": "a part", "in_box": "a part", "components": "two plumes", "thin": "the plumes taper", "at40": "a part"}),
    dict(name="Sparrow_AfterburnerJetsBoost", element="Time", act="Afterburner: the jets part at full burn (the HUD blue)", draw=afterburner_jets_boost, subdir="Parts", family=False,
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
    # (the swap-weapon placeholder 4a5259... that Sparrow_TurretStance replaced in round 2 is gone from the prefab: the Mass
    #  card's Image is a container now, its sprite cleared by the pair step below, so there is no reference to keep)
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
    # The Mass card: the icon Image becomes a container over the rounds part (ghost / solid / shielded, swapped and
    # faded by the view) and the hull part (domain colour).
    dict(file=VARIANT, pair=dict(container="6611959372928416812", view="7983934951929746178", after="afterburnerJetsBoostSprite",
                                 children=[("StanceRounds", "Sparrow_TurretStanceRounds", "stanceRoundsIcon"),
                                           ("StanceHull", "Sparrow_TurretStanceHull", "stanceHullIcon")],
                                 fields={"stanceRoundsSprite": ("icon", "Sparrow_TurretStanceRounds"),
                                         "stanceRoundsShieldedSprite": ("icon", "Sparrow_TurretStanceRoundsShielded")})),
]


# The cards' STATES sheets (states.py): Docs/Icons/sheets/sparrow_<name>.png, drawn on every author.py run.
CARDS = [
    dict(name="afterburner", element="Time", before="Sparrow_Afterburner", before_rev="f9e5f73e3~1",
         title="Afterburner: the Sparrow folds its fans and its jets lengthen while the boost is held; no meter on the plate",
         parts=dict(hull_rest=afterburner_hull_rest, hull_boost=afterburner_hull_boost, jets_rest=afterburner_jets_rest, jets_boost=afterburner_jets_boost),
         states=[("rest: fans spread, idling jets", [("jets_rest", "blue"), ("hull_rest", "domain")]),
                 ("boosting: folded, full burn", [("jets_boost", "blue"), ("hull_boost", "domain")])]),
    dict(name="turret_stance", element="Mass", before="Sparrow_TurretStance", before_rev="386d7cb1e~1",
         title="Turret Stance: the stopped Sparrow firing swelling rounds from both pods - a ghost while flying, solid in the stance, shielded octahedrons at Mass 5",
         parts=dict(hull=turret_stance_hull, rounds=turret_stance_rounds, rounds_shielded=turret_stance_rounds_shielded),
         states=[("flying: stance off (ghost)", [("rounds", "domain", 0.6), ("hull", "domain")]),
                 ("in the stance", [("rounds", "domain"), ("hull", "domain")]),
                 ("in the stance at Mass 5", [("rounds_shielded", "shielded"), ("hull", "domain")])]),
]
