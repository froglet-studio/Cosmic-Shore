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
import numpy as np
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


# ---- Pulsefire Cannons (Space): THE SPRAY over the segmented heat gauge (Garrett 2026-10-27: "option E2 looks great").
# Measured first: the guns' RANGE is fixed (Space no longer reaches them, SPARROW_SPRAY_ACCURACY.md Round 7) and the
# live quantity is HEAT, 0..30 s in six even phases - the cone holds at 0 deg, opens to 1.5, holds, blows out to 7.5,
# holds, collapses to 37.5. Garrett: "the icon we had before the current had a way to show that there were different
# heat buckets that had different spread. if you could convey the realtime spread value and which level they are on
# that would be best"; then "lets try to get E using more shots and the whole space to define the angle"; and of the
# marks across the plate "i like the ease of reading the lines as the boundaries between levels but i don't like how
# it visually dominates the whole space". So: the PLATE is the heat, its fill cut into one bar per level reached and
# the levels still ahead as dim ticks at the plate's edges (card.py gauge_marks_style="segments";
# SparrowHUDView.PaintGunHeatMarks); the ICON is ten rounds in five ranks from a muzzle at the card's left edge, each
# on its own ray inside the LIVE cone - one stream when cold, fanned to the card's whole height in the collapse. Every
# rank puts one round on a cone edge, so the angle is drawn, not guessed. The real angles are tiny until the
# blow-out, so the view maps each phase's real cap onto a readable card angle (SparrowHUDView.SprayCardAngle, the
# knots below); the controller pushes the real half-angle every frame. The view GENERATES the ten round Images under
# the card's icon (a transparent container) from one round sprite and lays them out on their rays - the layout below
# is serialized on the view (gunSprayMuzzle, gunSprayRounds) and must match.
import math

# (label, heat fill 0..1, real half-angle deg) - each phase's representative point, for the states sheet
HEAT_STATES = [("cold: pin-accurate", 0.10, 0.0),
               ("fightable plateau: 1.5 deg", 0.42, 1.5),
               ("area denial: 7.5 deg", 0.75, 7.5),
               ("collapse: 37.5 deg", 1.00, 37.5)]
GUN_MARKS = [1 / 6, 2 / 6, 3 / 6, 4 / 6, 5 / 6]   # CollectPhaseJoins on the shipped profile: six even phases
SPRAY_MUZZLE = (2.0, 64.0)                        # icon px; SparrowHUDView.gunSprayMuzzle
SPRAY_RANKS = [(20, (1.0, -0.4)), (42, (-1.0, 0.3)), (64, (1.0, -0.55)), (86, (-1.0, 0.6)), (110, (1.0, -1.0))]
SPRAY_CAPS_REAL = (1.5, 7.5, 37.5)                # the shipped profile's three caps (the view reads them live)
SPRAY_CAPS_CARD = (8.0, 19.0, 30.0)               # SparrowHUDView.gunSprayCardAngles


def spray_card_angle(real):
    """The C# SprayCardAngle: piecewise-linear from (0, cap1, cap2, cap3) to (0, card1, card2, card3), clamped."""
    xs, ys = (0.0,) + SPRAY_CAPS_REAL, (0.0,) + SPRAY_CAPS_CARD
    if real >= xs[-1]:
        return ys[-1]
    for i in range(1, len(xs)):
        if real <= xs[i]:
            t = (real - xs[i - 1]) / (xs[i] - xs[i - 1])
            return ys[i - 1] + t * (ys[i] - ys[i - 1])
    return 0.0


def bullet_round(cx, cy, a=0.0):
    return fill(rotate([(cx - 7.5, cy - 4.5), (cx + 2.5, cy - 4.5), (cx + 7.5, cy), (cx + 2.5, cy + 4.5), (cx - 7.5, cy + 4.5)], -a, cx, cy))


def prism_round(cx, cy, a=0.0):
    """A stance round: a bare prism, front-above, centred on (cx, cy) like the bullet it replaces."""
    return block(cx - 2.5, cy + 2.5, w=11, h=8, dp=4)   # a prism flies square-on: the view never turns it


def spray(half, shape=bullet_round):
    x0, y0 = SPRAY_MUZZLE
    m = empty()
    for d, ks in SPRAY_RANKS:
        for k in ks:
            a = half * k
            r = math.radians(a)
            m |= shape(x0 + d * math.cos(r), y0 - d * math.sin(r), a)
    return m


def cannons():
    """The card's sprite (sheets, any single-image surface): the cold spray, one stream."""
    return spray(0.0)


def prism_rounds():
    """weaponModeIcons[1]: the cold spray in the stance, prism rounds."""
    return spray(0.0, prism_round)


def gun_spray_round():
    """The part the view clones ten times: one bullet round at the sprite's centre."""
    return bullet_round(64, 64)


def gun_spray_prism_round():
    """The stance's round part: one bare prism at the sprite's centre."""
    return prism_round(64, 64)


# The L5 state (Piercing Bullets): a hostile prism on the centre line, the rounds through it and out the far side.
PIERCE_PRISM = dict(cx=58, cy=70, w=10, h=36, dp=6)


def pierce_prism():
    """The Space 5 part: a hostile prism on the centre line the rounds fly through (the view tints it danger, 0.6)."""
    return block(**PIERCE_PRISM)


# ---- Skyburst Rockets (Charge) OPTIONS, 2026-10-27. Measured on the HUD first: the card is a LADDER of five rack
# sprites (0..4 rockets held, SetMissilesFromAmmo01) over the plate gauge filling toward the NEXT rocket
# (SetMissileCharge) - two readings of one tank (FireGunActionExecutor: Ammo01, a base rocket costs ShotCost01 =
# 0.25, a heavy one from the stance costs two). The skill says a ladder is a how-much drawn as shapes, so every
# candidate moves the COUNT onto the plate in the language Garrett just approved on the Space card: the plate is the
# TANK, cut into one bar per rocket - full bars are rockets held, the bar filling is the next one. The icon spends its
# shapes on the act: a rocket that goes off NEAR its target in two blasts (the prism-tearing burst and the wider
# warhead), stocked by DESTROYING mass. Empty rack: the icon is a ghost (the promise); a rocket held: solid.
# One CARDS entry per candidate; the losers are deleted once Garrett picks.
import math as _m
from motifs import burst
from canvas import ring as _ring, band as _band

RACK_MARKS = [0.25, 0.5, 0.75]          # ShotCost01 multiples: a join per rocket
RACK_STATES = [("empty: earning the first", 0.15, 0.6),
               ("2 held, charging the third", 0.62, 1.0),
               ("rack full: 4 held", 1.0, 1.0)]


def rocket_at(cx, cy, h, heading=45.0):
    """The rack's rocket (nose, body, two swept fins), turned to fly at `heading` degrees (0 = right, 90 = up)."""
    w = h * 0.2
    pts = [(cx, cy - h / 2), (cx + w / 2, cy - h / 2 + h * 0.26), (cx + w / 2, cy + h * 0.2),
           (cx + w * 1.0, cy + h / 2), (cx + w * 0.4, cy + h * 0.34), (cx - w * 0.4, cy + h * 0.34),
           (cx - w * 1.0, cy + h / 2), (cx - w / 2, cy + h * 0.2), (cx - w / 2, cy - h / 2 + h * 0.26)]
    return fill(rotate(pts, 90.0 - heading, cx, cy))


# K - THE ROCKET: one rocket climbing at 45 degrees, big - the weapon itself.
def skyburst_k():
    return rocket_at(64, 64, 104, 45)


# L - THE SKYBURST: a rocket lower-left, its nose into the double detonation upper-right - the jagged prism-tearing
# burst inside the wider warhead ring (an open arc: the fuze goes off NEAR, not on contact).
def skyburst_l():
    m = rocket_at(36, 92, 58, 45)
    m |= burst(84, 44, 24, spikes=9, inner=0.5, rot=10) & ~disc_(84, 44, 8)
    m |= _band(84, 44, 38, 200, 470, w=7)
    return m


def disc_(cx, cy, r):
    from canvas import disc
    return disc(cx, cy, r)


# M - THE HARVEST: the burst among prisms - it tears the hostile ones (danger) apart, flung outward; at Charge 5 your
# own (domain) prisms inside it are spared. Teaches what it acts on and what changes.
M_PRISMS = [(30, 30, -30), (100, 26, 20), (24, 96, -10), (104, 100, 30)]


def skyburst_m_burst():
    return burst(64, 64, 34, spikes=10, inner=0.5, rot=8) & ~disc_(64, 64, 11)


def skyburst_m_prisms():
    from motifs import prism
    m = empty()
    for x, y, _a in M_PRISMS:
        m |= prism(x, y, 13)
    return m


ICONS = [
    dict(name="Sparrow_TurretStance", element="Mass", act="Turret Stance: the stopped Sparrow firing swelling rounds from both pods (composite)", draw=turret_stance,
         exempt={"gap": "Garrett 2026-10-26 - the real mesh", "components": "a hull and four rounds", "specks": "the blocks' seam slivers"}),
    dict(name="Sparrow_TurretStanceHull", element="Mass", act="Turret Stance: the hull part (stopped, fans spread)", draw=turret_stance_hull, subdir="Parts", family=False,
         exempt={"gap": "the real mesh", "box_fill": "a part: the hull leaves room for the rounds", "in_box": "a part"}),
    dict(name="Sparrow_TurretStanceRounds", element="Mass", act="Turret Stance: the rounds part, bare prisms (ghost while flying, solid in the stance)", draw=turret_stance_rounds, subdir="Parts", family=False,
         exempt={"coverage": "a part", "box_fill": "a part", "in_box": "a part", "components": "four rounds", "specks": "seam slivers", "gap": "seams", "thin": "seams", "at40": "the seams close at 40 px, the blocks stay"}),
    dict(name="Sparrow_TurretStanceRoundsShielded", element="Mass", act="Turret Stance: the rounds part at Mass 5 (octahedrons, the shielded colour)", draw=turret_stance_rounds_shielded, subdir="Parts", family=False,
         exempt={"coverage": "a part", "box_fill": "a part", "in_box": "a part", "components": "four rounds"}),
    dict(name="Sparrow_Cannons", element="Space", act="Pulsefire Cannons: the cold spray, one stream (composite; the HUD lays the rounds out live)", draw=cannons,
         exempt={"coverage": "a line of rounds: the cold spray", "components": "five rounds", "at40": "the rounds hold at 40 px"}),
    dict(name="Sparrow_GunSprayRound", element="Space", act="Pulsefire Cannons: one round, the part the view clones onto the live cone", draw=gun_spray_round, subdir="Parts", family=False,
         exempt={"coverage": "a part", "box_fill": "a part", "in_box": "a part", "at40": "a part"}),
    dict(name="Sparrow_GunSprayPrismRound", element="Space", act="Pulsefire Cannons: one prism round, the stance's part", draw=gun_spray_prism_round, subdir="Parts", family=False,
         exempt={"coverage": "a part", "box_fill": "a part", "in_box": "a part", "at40": "a part", "specks": "seam slivers", "thin": "seams", "gap": "seams", "components": "the seams cut it"}),
    dict(name="Sparrow_GunSprayPierce", element="Space", act="Pulsefire Cannons at Space 5: the prism the rounds pierce", draw=pierce_prism, subdir="Parts", family=False,
         exempt={"coverage": "a part", "box_fill": "a part", "in_box": "a part", "specks": "seam slivers", "thin": "seams", "gap": "seams", "components": "the seams cut it"}),
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
    dict(name="Sparrow_PrismRounds", element="Space", act="stance: the cold spray in prism rounds", draw=prism_rounds, subdir="Modes", family=False,
         exempt={"coverage": "a line of rounds", "components": "five rounds", "specks": "seam slivers", "thin": "seams", "gap": "seams", "at40": "seams"}),
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
    # The Space card: the icon Image becomes a transparent container; the view GENERATES the ten rounds and the
    # Space 5 prism under it from these sprites (no prefab children: the layout is the view's serialized numbers).
    dict(file=VARIANT, pair=dict(container="4990196459091587280", view="7983934951929746178", after="stanceRoundsShieldedSprite",
                                 children=[],
                                 fields={"gunSprayRoundSprite": ("icon", "Sparrow_GunSprayRound"),
                                         "gunSprayPrismRoundSprite": ("icon", "Sparrow_GunSprayPrismRound"),
                                         "gunSprayPierceSprite": ("icon", "Sparrow_GunSprayPierce")})),
]


# The hull's other cards as the HUD draws them today (states.py options sheets show a candidate beside them).
LOCKUP = dict(
    Charge=dict(parts=dict(rack=lambda: rockets(4)), state=[("rack", "white")]),
    Mass=dict(parts=dict(hull=turret_stance_hull, rounds=turret_stance_rounds), state=[("rounds", "domain", 0.6), ("hull", "domain")]),
    Space=dict(parts=dict(guns=cannons), state=[("guns", "domain")], gauge=0.10, gauge_marks=GUN_MARKS),
    Time=dict(parts=dict(hull=afterburner_hull_rest, jets=afterburner_jets_rest), state=[("jets", "blue"), ("hull", "domain")]),
)

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

    dict(name="cannons", element="Space", before="Sparrow_Cannons", before_rev="9f748f9ae", before_gauge=True, before_gauge_marks_style="lines",
         gauge_marks=GUN_MARKS, gauge_marks_style="segments",
         title="Pulsefire Cannons: ten rounds on their rays inside the LIVE cone; the heat fill is one bar per level reached, the levels ahead dim edge ticks",
         parts={**{f"s{i}": (lambda r=r: spray(spray_card_angle(r))) for i, (_n, _g, r) in enumerate(HEAT_STATES)},
                "stance": lambda: spray(spray_card_angle(7.5), prism_round), "prism": pierce_prism, "pierce": cannons},
         states=[(n, [(f"s{i}", "domain")]) for i, (n, _g, _r) in enumerate(HEAT_STATES)]
                + [("turret stance at 7.5 deg: prism rounds", [("stance", "domain")]),
                   ("Space 5: rounds pierce prisms", [("prism", "danger", 0.6), ("pierce", "domain")])],
         gauge={**{n: (g, None) for n, g, _r in HEAT_STATES}, "turret stance at 7.5 deg: prism rounds": (0.75, None),
                "Space 5: rounds pierce prisms": (0.10, None)}),
    # ---- Skyburst Rockets OPTIONS (2026-10-27): the plate is the tank in one bar per rocket; the icon is the act
    dict(name="skyburst_option_k", group="skyburst", lockup_state=1, element="Charge", before="Assets/_Graphics/Icons/AbilityIcons/Sparrow/Ladder/Sparrow_Rockets_2.png", before_gauge_marks=[],
         before_gauge_value=0.48, gauge_marks=RACK_MARKS, gauge_marks_style="segments",
         title="K - THE ROCKET: one big rocket climbing; a ghost while the rack is empty. The plate: one bar per rocket",
         parts=dict(r=skyburst_k),
         states=[(n, [("r", "domain", a)]) for n, _g, a in RACK_STATES],
         gauge={n: (g, None) for n, g, _a in RACK_STATES}),
    dict(name="skyburst_option_l", group="skyburst", lockup_state=1, element="Charge", before="Assets/_Graphics/Icons/AbilityIcons/Sparrow/Ladder/Sparrow_Rockets_2.png", before_gauge_marks=[],
         before_gauge_value=0.48, gauge_marks=RACK_MARKS, gauge_marks_style="segments",
         title="L - THE SKYBURST: the rocket into its double blast (burst + the wider warhead arc); a ghost while empty",
         parts=dict(r=skyburst_l),
         states=[(n, [("r", "domain", a)]) for n, _g, a in RACK_STATES],
         gauge={n: (g, None) for n, g, _a in RACK_STATES}),
    dict(name="skyburst_option_m", group="skyburst", lockup_state=1, element="Charge", before="Assets/_Graphics/Icons/AbilityIcons/Sparrow/Ladder/Sparrow_Rockets_2.png", before_gauge_marks=[],
         before_gauge_value=0.48, gauge_marks=RACK_MARKS, gauge_marks_style="segments",
         title="M - THE HARVEST: the burst tearing hostile prisms apart (they stock the rack); at Charge 5 your own are spared",
         parts=dict(b=skyburst_m_burst, p=skyburst_m_prisms),
         states=[(n, [("p", "danger"), ("b", "domain", a)]) for n, _g, a in RACK_STATES]
                + [("Charge 5: your prisms spared", [("p", "domain"), ("b", "domain")])],
         gauge={**{n: (g, None) for n, g, _a in RACK_STATES}, "Charge 5: your prisms spared": (1.0, None)}),
]
