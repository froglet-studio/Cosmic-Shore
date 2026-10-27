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


# ---- Pulsefire Cannons (Space) OPTIONS, round 2 (2026-10-27). Measured first: the guns' RANGE is fixed (Space no
# longer reaches them, SPARROW_SPRAY_ACCURACY.md Round 7) and the live quantity is HEAT, 0..30 s in six even phases -
# the cone holds at 0 deg, opens to 1.5, holds, blows out to 7.5, holds, collapses to 37.5 - and range falls with the
# cone to a quarter (Round 8, GunSpreadMath.RangeFactor). The card today: a white bullet sprite over the generated heat
# gauge with a mark at each phase join (SetGunHeat, SetGunHeatPhaseJoins; passed marks bright, ahead dim).
# Garrett 2026-10-27: "the icon we had before the current had a way to show that there were different heat buckets
# that had different spread. if you could convey the realtime spread value and which level they are on that would be
# best." So: the PLATE keeps the heat and its phase marks (which level), and the ICON draws the live cone (how wide,
# how far). The real angles are tiny until the blow-out, so the card maps each phase's real cap onto a readable one
# (a pure function beside the curve; the controller pushes the real half-angle every frame).
import math
from canvas import stroke

GUN_MUZZLE = (14.0, 64.0)
GUN_LEN = 104.0                      # full range, in icon px along the centre line
# (label, heat fill 0..1, real half-angle deg, card half-angle deg, range kept) - the phases' representative points
HEAT_STATES = [("cold: pin-accurate", 0.10, 0.0, 0.0, 1.00),
               ("fightable plateau: 1.5 deg", 0.42, 1.5, 10.0, 0.97),
               ("area denial: 7.5 deg", 0.75, 7.5, 20.0, 0.85),
               ("collapse: 37.5 deg", 1.00, 37.5, 34.0, 0.25)]
GUN_MARKS = [1 / 6, 2 / 6, 3 / 6, 4 / 6, 5 / 6]   # CollectPhaseJoins on the shipped profile: six even phases


def ray_rounds(angle, length, n=4, shape=None, start=14):
    """`n` cannon rounds along a ray from the muzzle at `angle` degrees (up is negative y), out to `length`."""
    shape = shape or (lambda cx, cy: [(cx - 10, cy - 5.5), (cx + 4, cy - 5.5), (cx + 10, cy), (cx + 4, cy + 5.5), (cx - 10, cy + 5.5)])
    x0, y0 = GUN_MUZZLE
    m = empty()
    a = math.radians(angle)
    for i in range(n):
        d = start + (length - 10 - start) * i / max(1, n - 1)
        cx, cy = x0 + d * math.cos(a), y0 - d * math.sin(a)
        m |= fill(rotate(shape(cx, cy), -angle, cx, cy))
    return m


def _len(kept):
    return GUN_LEN * max(0.5, kept)      # the collapse keeps a quarter; the card floors it so the rounds stay legible


# Option E - THE SPRAY: four rounds from one muzzle, each on its own ray inside the live cone, alternating sides.
# Cold, every ray is the centre line and the card is ONE stream; heat swings the rounds out to the live angle (the
# view places the four round Images on their rays) and the range shortens with the cone.
SPRAY_SIDES = (0.85, -0.85, 1.0, -1.0)    # where each round sits across the cone, nearest first


def fan(half, kept):
    L = _len(kept)
    x0, y0 = GUN_MUZZLE
    m = empty()
    for i, k in enumerate(SPRAY_SIDES):
        d = 18 + (L - 28) * i / (len(SPRAY_SIDES) - 1)
        a = half * k
        r = math.radians(a)
        cx, cy = x0 + d * math.cos(r), y0 - d * math.sin(r)
        m |= fill(rotate([(cx - 10, cy - 5.5), (cx + 4, cy - 5.5), (cx + 10, cy), (cx + 4, cy + 5.5), (cx - 10, cy + 5.5)], -a, cx, cy))
    return m


# Option F - THE CONE: the centre stream solid, and the cone itself behind it as a ghost sector (a Radial filled
# Image: fillAmount = 2 x the live angle / 360, scaled by the range) with its edges hard.
def cone_sector(half, kept):
    if half <= 0:
        return empty()
    import canvas as C
    x0, y0 = GUN_MUZZLE
    L = _len(kept)
    dx, dy = C.GX - x0, -(C.GY - y0)
    r = (dx ** 2 + dy ** 2) ** 0.5
    ang = abs(np.degrees(np.arctan2(dy, dx)))
    return (r <= L) & (r >= 10) & (ang <= half)


def cone_edges(half, kept):
    if half <= 0:
        return empty()
    x0, y0 = GUN_MUZZLE
    L = _len(kept)
    m = empty()
    for a in (half, -half):
        r = math.radians(a)
        m |= stroke([(x0 + 10 * math.cos(r), y0 - 10 * math.sin(r)), (x0 + L * math.cos(r), y0 - L * math.sin(r))], 6)
    return m


def cone_centre(kept):
    return ray_rounds(0, _len(kept))


# The L5 state (Piercing Bullets): a hostile prism on the centre line, the rounds through it and out the far side.
PIERCE_PRISM = dict(cx=62, cy=70, w=12, h=40, dp=6)


def pierce_prism():
    return block(**PIERCE_PRISM)


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


# The hull's other cards as the HUD draws them today (states.py options sheets show a candidate beside them).
LOCKUP = dict(
    Charge=dict(parts=dict(rack=lambda: rockets(4)), state=[("rack", "white")]),
    Mass=dict(parts=dict(hull=turret_stance_hull, rounds=turret_stance_rounds), state=[("rounds", "domain", 0.6), ("hull", "domain")]),
    Space=dict(parts=dict(guns=cannons), state=[("guns", "white")]),
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

    # ---- Pulsefire Cannons OPTIONS round 2 (2026-10-27): the live cone + the heat phase; the old card first
    dict(name="cannons_option_e", group="cannons", lockup_state=1, element="Space", before="Sparrow_Cannons", before_gauge_value=0.42,
         gauge_marks=GUN_MARKS,
         title="Option E - THE SPRAY: four rounds, each on its own ray inside the live cone - a line when cold, a scatter when hot; the plate is the heat, a mark per phase",
         parts={**{f"s{i}": (lambda h=h, k=k: fan(h, k)) for i, (_n, _g, _r, h, k) in enumerate(HEAT_STATES)},
                "prism": pierce_prism, "pierce": lambda: fan(0, 1.0)},
         states=[(n, [(f"s{i}", "domain")]) for i, (n, _g, _r, _h, _k) in enumerate(HEAT_STATES)]
                + [("Space 5: rounds pierce prisms", [("prism", "danger", 0.6), ("pierce", "domain")])],
         gauge={**{n: (g, None) for n, g, _r, _h, _k in HEAT_STATES}, "Space 5: rounds pierce prisms": (0.10, None)}),
    dict(name="cannons_option_f", group="cannons", lockup_state=1, element="Space", before="Sparrow_Cannons", before_gauge_value=0.42,
         gauge_marks=GUN_MARKS,
         title="Option F - THE CONE: the centre stream over the live cone (a ghost sector with hard edges); the plate is the heat, a mark per phase",
         parts={**{f"c{i}": (lambda h=h, k=k: [(cone_centre(k) | cone_edges(h, k), 1.0), (cone_sector(h, k) & ~cone_centre(k), 0.35)])
                   for i, (_n, _g, _r, h, k) in enumerate(HEAT_STATES)},
                "prism": pierce_prism, "pierce": lambda: cone_centre(1.0)},
         states=[(n, [(f"c{i}", "domain")]) for i, (n, _g, _r, _h, _k) in enumerate(HEAT_STATES)]
                + [("Space 5: rounds pierce prisms", [("prism", "danger", 0.6), ("pierce", "domain")])],
         gauge={**{n: (g, None) for n, g, _r, _h, _k in HEAT_STATES}, "Space 5: rounds pierce prisms": (0.10, None)}),
]
