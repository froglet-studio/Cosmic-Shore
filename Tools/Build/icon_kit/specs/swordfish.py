"""The Swordfish's ability-row icons (Resources/ElementalAbilityMaps/Swordfish.asset), its own since 2026-11-12.

The Swordfish split off the Rhino on 2026-11-07 with the whole sword kit, and flew the Rhino's HUD and its katana cards
until now. Since 2026-11-08 its sword is the fish's BILL (SwordfishBill.asset, author_swordfish_bill.py): flat, tapering
to a point, its hilt in the hull's snout; at rest it lies straight ahead, one trigger swings it out flat to that side
(yaw 90), both triggers stand it straight up (raise 90, the energize stance; ShieldSwipeActionExecutor.BillChannels).
The camera sits behind the hull, so every card here is the pilot's own view: the hull nose-on from behind (its real
mesh, Swordfish.fbx pitch -90 yaw 90: the round body, the crest up, the pectorals swept out) and the bill out of its
snout - foreshortened when it points ahead, full length when it is out to the side or raised.

  Charge  Energize     the bill follows the real sword (the HUD turns it with the side swing, lengthens it as it
                       rises); white dark, danger lit. Charge scales the petals a sword hit takes; Charge 5 pockets them.
  Mass    Afterimage   the bill swinging past two prism copies of itself in your colour, while they are laid.
  Space   Blade Reach  the bill laid flat at its live length (24 -> 40), its tip in a super-shielded prism, shattered
                       once Shield Breaker (Space 5) is active.
  Time    Quickdraw    the sword's RATE (2026-11-12): the bill snapped out to the side with its swing arc; the plate
                       is the swing's recovery draining away (Time shortens it); Flurry (Time 5) opens the swing back
                       the other way - no recovery at all.
  Omni    Surge        the omni crystal is absorbed and eight bills stand round the hull.

Before: the Rhino's cards (Rhino_Katana, Rhino_Afterimage, Rhino_BladeReach, Rhino_RampSpool), which still drew a
katana and a Rhino. The picks below are the recommended option of each sheet (Garrett 2026-11-07: build the
recommended option with full polish and he reviews the roster at the end).
"""
import importlib as _importlib
import math

import numpy as np

import toy_portraits
from canvas import GX, GY, band, empty, fill, rotate

HULL = "Swordfish"
ICON_DIR = "Assets/_Graphics/Icons/AbilityIcons/Swordfish/"
RHINO_DIR = "Assets/_Graphics/Icons/AbilityIcons/Rhino/"
FBX = "Assets/_Models/Vessel Models/Swordfish.fbx"

_rh = _importlib.import_module("specs.rhino")


def _clear_of(mask, *others, px=2.5):
    keep = mask.copy()
    for o in others:
        keep &= ~toy_portraits.grown(o, px)
    return keep


# ---- the hull, nose-on from behind (the pilot's camera) ---------------------------------------------------------
def hull_back(cx=64.0, cy=100.0, box=56.0, dilate=1.6):
    """The Swordfish from behind, crest up: Swordfish.fbx at pitch -90 yaw 90 (measured on the mesh grid), fitted to
    `box` px across the pectoral tips. The thin pectorals are thickened a little so they hold at 40 px."""
    from mesh_silhouette import silhouette
    return silhouette(FBX, pitch=-90.0, yaw=90.0, box=box, cx=cx, cy=cy, dilate_px=dilate)


# ---- the bill ----------------------------------------------------------------------------------------------------
BILL_W = 15.0        # the bill's width at its root (px): far wider than the real 15:1 so it holds at 40 px
BILL_LEN = 88.0      # snout to point at full length (px)


def bill(angle=0.0, length=BILL_LEN, hilt=(64.0, 98.0), w=BILL_W, root=0.0):
    """The bill standing off `hilt`, turned `angle` deg clockwise: the bill's own taper (half-width (1 - t)^0.72,
    author_swordfish_bill.profile), drawn from `root` (a fraction of the length hidden in the snout) to the point."""
    hx, hy = hilt
    side_l, side_r = [], []
    steps = 28
    for i in range(steps + 1):
        t = root + (1.0 - root) * i / steps
        hw = w / 2 * (1.0 - t) ** 0.72 * (1.0 + 0.10 * max(0.0, 1.0 - t / 0.06))
        y = -length * t
        side_l.append((hx - hw, hy + y))
        side_r.append((hx + hw, hy + y))
    pts = side_l + side_r[::-1]
    return fill(rotate(pts, angle, hx, hy))


def prism_bill(angle, length=BILL_LEN, hilt=(64.0, 98.0), pitch=13.0, seam=3.0):
    """An AFTERIMAGE: the bill cut across into prisms (the sword's copies are prisms in its shape and pose)."""
    hx, hy = hilt
    m = bill(angle, length, hilt, w=BILL_W * 1.2)
    a = math.radians(angle)
    along = (GX - hx) * math.sin(a) - (GY - hy) * math.cos(a)
    return m & ((along % pitch) >= seam)


# ---- Charge: Energize, the bill follows the real sword -----------------------------------------------------------
CH_HILT = (64.0, 104.0)      # the snout, low in the card: the bill stands off it
BILL_SIDE_CARD_DEG = 55.0    # the view's billSideCardDegrees: a full side swing (90 real) turns the card bill 55 deg
BILL_REST01 = 0.65           # the view's billRestLength01: pointing ahead, the bill is foreshortened to this


def bill_pose(side, raise01):
    """What the HUD draws for a side swing (-1..1) and a raise (0..1). Mirrors SwordfishVesselHUDView.BillPose."""
    length01 = BILL_REST01 + (1.0 - BILL_REST01) * max(abs(side), raise01)
    return bill(side * BILL_SIDE_CARD_DEG, BILL_LEN * length01, CH_HILT)


def socket(hilt, angle=0.0):
    """The snout the bill is seated in, seen from behind: a short flat wedge across the hilt, wider than the bill's
    root and square at the back - the jaw the bill runs on from (the snout is as wide as the bill's root)."""
    hx, hy = hilt
    pts = [(hx - 13.0, hy + 9.0), (hx + 13.0, hy + 9.0), (hx + 9.0, hy - 2.0), (hx - 9.0, hy - 2.0)]
    return fill(rotate(pts, angle, hx, hy))


def ch_socket():
    return socket(CH_HILT)


def ch_bill():
    """The PART: the bill at full length, upright on the hilt; the view turns and shortens it about the hilt."""
    return _clear_of(bill(0.0, BILL_LEN, CH_HILT), socket(CH_HILT), px=2.0)


def energize():
    return ch_socket() | _clear_of(bill_pose(0.0, 1.0), socket(CH_HILT), px=2.0)


# (name, side, raise, lit, plate = EnergizeGauge01)
BILL_STATES = [("at rest: pointing ahead", 0.0, 0.0, False, 0.0),
               ("RT: out flat to the right", 1.0, 0.0, False, 0.0),
               ("LT: out flat to the left", -1.0, 0.0, False, 0.0),
               ("half pull, right", 0.5, 0.0, False, 0.0),
               ("both triggers: raised, charging", 0.0, 1.0, False, 0.55),
               ("lit (still raised)", 0.0, 1.0, True, 1.0),
               ("lit, out to the right", 1.0, 0.0, True, 0.7),
               ("cooling down", 0.0, 0.0, False, 0.0)]


def _bill_states():
    parts, states, gauge = {"socket": ch_socket}, [], {}
    for k, (n, side, rise, lit, g) in enumerate(BILL_STATES):
        parts[f"b{k}"] = (lambda side=side, rise=rise: _clear_of(bill_pose(side, rise), socket(CH_HILT), px=2.0))
        states.append((n, [("socket", "white"), (f"b{k}", "danger" if lit else "white")]))
        gauge[n] = (g, None)
    return parts, states, gauge


_BP, _BS, _BG = _bill_states()

# ---- Mass: Afterimage --------------------------------------------------------------------------------------------
AF_HILT = (40.0, 112.0)
AF_BLADE, AF_NEAR, AF_FAR = 38.0, 12.0, -14.0     # the bill swinging right past where it was
AF_NEAR_ALPHA, AF_FAR_ALPHA = 0.8, 0.45


def af_blade():
    return bill(AF_BLADE, 100.0, AF_HILT, w=14.0)


def af_near():
    return _clear_of(prism_bill(AF_NEAR, 92.0, AF_HILT), af_blade(), px=2.0)


def af_far():
    return _clear_of(prism_bill(AF_FAR, 84.0, AF_HILT), af_blade(), af_near(), px=2.0)


def afterimage():
    return af_blade() | af_near() | af_far()


AF_LIT = [("far", "domain", AF_FAR_ALPHA), ("near", "domain", AF_NEAR_ALPHA), ("blade", "white")]

# ---- Space: Blade Reach (the Rhino's layout, the bill for the katana) --------------------------------------------
REACH_HILT = _rh.REACH_HILT
REACH_ANGLE = _rh.REACH_ANGLE
REACH_MAX, REACH_REST = _rh.REACH_MAX, _rh.REACH_REST
PART_HILT = (64.0, 100.0)          # the parts are drawn upright on this hilt (the view's reachPartHilt)


def reach_len(space01):
    return REACH_REST + (REACH_MAX - REACH_REST) * space01


def flat_bill(length):
    """The bill laid flat across the card at `length` px from the hilt (its root sits in the snout socket)."""
    return bill(REACH_ANGLE, length, REACH_HILT, w=15.0)


def reach_socket(hilt=REACH_HILT, angle=REACH_ANGLE):
    """The snout the bill is seated in: a short flat wedge, square at the back (the hilt)."""
    hx, hy = hilt
    pts = [(hx - 10.5, hy + 6.0), (hx + 10.5, hy + 6.0), (hx + 8.0, hy - 6.0), (hx - 8.0, hy - 6.0)]
    return fill(rotate(pts, angle, hx, hy))


PRISM_ALONG = -_rh.PRISM_BITE + _rh.PRISM_R * 0.42   # the view's reachPrismAlong (13.12); the bill is straight, so
                                                      # reachPrismSidePerLength is 0 (the katana's curve set -0.0913)


def prism_centre(length):
    a = math.radians(REACH_ANGLE)
    d = length + PRISM_ALONG
    return REACH_HILT[0] + math.sin(a) * d, REACH_HILT[1] - math.cos(a) * d


def _reach_prism(length, broken):
    cx, cy = prism_centre(length)
    star = _rh.stella(cx, cy, _rh.PRISM_R * (0.74 if broken else 1.0), broken=broken)
    return _clear_of(star, flat_bill(length), reach_socket(), _rh.reach_posts(), px=_rh.KNOCKOUT)


def blade_reach(space01=0.4, broken=False):
    L = reach_len(space01)
    b = flat_bill(L) | reach_socket()
    return [(_reach_prism(L, broken), 1.0), (b, 1.0), (_rh.reach_posts(), 0.6)]


def reach_part_hilt():
    return reach_socket(PART_HILT, 0.0)


def reach_part_blade():
    """The bill at full reach, upright on the part hilt; the view scales it along itself to the live length."""
    return bill(0.0, REACH_MAX, PART_HILT, w=15.0)


# ---- Time: Quickdraw -----------------------------------------------------------------------------------------------
QD_HILT = (64.0, 108.0)
QD_ANGLE = 50.0               # the bill snapped out to the right
QD_LEN = 72.0
QD_ARC_R = 60.0               # the swing's path: a swoosh trailing the point
QD_ARC_W = 6.0


def qd_bill():
    return _clear_of(bill(QD_ANGLE, QD_LEN, QD_HILT), socket(QD_HILT), px=2.0)


def qd_socket():
    return socket(QD_HILT)


def _swoosh(a_from, a_to, w_max=9.0, steps=14):
    """A swoosh trailing the bill's point: an arc round the snout at the point's radius, full width (w_max) where the
    bill is (`a_to`) and tapering to nothing behind it (`a_from`) - the fleet's one motion mark, bent to the swing."""
    out = empty()
    for i in range(steps):
        t0, t1 = i / steps, (i + 1) / steps
        a0 = a_from + (a_to - a_from) * t0
        a1 = a_from + (a_to - a_from) * t1
        out |= band(QD_HILT[0], QD_HILT[1], QD_ARC_R, a0, a1 + (0.6 if a_to > a_from else -0.6), w=max(1.0, w_max * t1))
    return out


def qd_arc():
    """The swing just made: from straight ahead round to the bill (canvas.band: up is -90, clockwise positive)."""
    return _clear_of(_swoosh(-96.0, -90.0 + QD_ANGLE - 8.0), bill(QD_ANGLE, QD_LEN, QD_HILT), px=3.0)


def qd_flurry():
    """Flurry (Time 5): the way back is open at once - the same swoosh mirrored, the other side of ahead."""
    return _swoosh(-84.0, -90.0 - QD_ANGLE + 8.0)


def quickdraw():
    return qd_socket() | qd_bill() | qd_arc() | qd_flurry()


# ---- Omni: Surge -------------------------------------------------------------------------------------------------
def surge():
    """The omni crystal absorbed and eight bills standing round the hull, 45 degrees apart round the nose axis - seen
    from behind, a ring of blades round the body (SurgeRingSwordCount 8)."""
    core = hull_back(64.0, 64.0, 50.0, dilate=2.6)
    out = core
    hx, hy = 64.0, 64.0
    for k in range(8):
        a = k * 45.0
        ra = math.radians(a)      # each bill stands off the body along its own line
        off = 21.0
        b = bill(a, 33.0, (hx + math.sin(ra) * off, hy - math.cos(ra) * off), w=11.0)
        out |= _clear_of(b, core, px=2.5)
    return out


# ---- the icons the kit writes ------------------------------------------------------------------------------------
PART_EXEMPT = {"at40": "a part: the HUD draws the card whole", "components": "a part", "gap": "a part",
               "coverage": "a part", "box_fill": "a part", "in_box": "a part", "thin": "a part", "specks": "a part"}
COMPOSITE = {"components": "the bill, its snout and its parts", "specks": "the prism seams",
             "gap": "the seams between snout and bill", "thin": "the bill is slim", "in_box": "the bill runs the card",
             "coverage": "a bill is slim (the katana it replaces was exempt for the same reason)",
             "box_fill": "the bill reaches corner to corner; its length is the measure"}

ICONS = [
    dict(name="Swordfish_Energize", element="Charge", draw=energize, exempt=COMPOSITE,
         act="Energize: the bill raised off its snout, seen from behind (composite; the HUD turns it live)"),
    dict(name="Swordfish_BillSocket", element="Charge", draw=ch_socket, subdir="Parts", family=False, exempt=PART_EXEMPT,
         act="Energize: the snout the bill stands off (white; never turns)"),
    dict(name="Swordfish_Bill", element="Charge", draw=ch_bill, subdir="Parts", family=False, exempt=PART_EXEMPT,
         act="Energize: the bill at full length, upright on the hilt; the view turns and shortens it"),

    dict(name="Swordfish_Afterimage", element="Mass", draw=afterimage, exempt=COMPOSITE,
         act="Afterimage: the bill swinging past two prism copies of itself (composite; the HUD draws the parts)"),
    dict(name="Swordfish_AfterimageBlade", element="Mass", draw=af_blade, subdir="Parts", family=False,
         exempt=PART_EXEMPT, act="Afterimage: the bill (white)"),
    dict(name="Swordfish_AfterimageNear", element="Mass", draw=af_near, subdir="Parts", family=False,
         exempt=PART_EXEMPT, act="Afterimage: the nearer prism copy (domain colour)"),
    dict(name="Swordfish_AfterimageFar", element="Mass", draw=af_far, subdir="Parts", family=False,
         exempt=PART_EXEMPT, act="Afterimage: the further prism copy (domain colour, fainter)"),

    dict(name="Swordfish_BladeReach", element="Space", draw=blade_reach, exempt=COMPOSITE,
         act="Blade Reach: the bill at its live length, its tip in a super-shielded prism (composite)"),
    dict(name="Swordfish_ReachHilt", element="Space", draw=reach_part_hilt, subdir="Parts", family=False,
         exempt=PART_EXEMPT, act="Blade Reach: the snout socket, upright on the part hilt"),
    dict(name="Swordfish_ReachBlade", element="Space", draw=reach_part_blade, subdir="Parts", family=False,
         exempt=PART_EXEMPT, act="Blade Reach: the bill at full reach, upright; the view scales it to the live length"),
    dict(name="Swordfish_ReachPrism", element="Space", draw=_rh.reach_part_prism, subdir="Parts", family=False,
         exempt=PART_EXEMPT, act="Blade Reach: the super-shielded prism (flat stella octangula), centred"),
    dict(name="Swordfish_ReachPrismBroken", element="Space", draw=_rh.reach_part_prism_broken, subdir="Parts",
         family=False, exempt=PART_EXEMPT, act="Blade Reach: the prism shattered along its facets (Shield Breaker)"),
    dict(name="Swordfish_ReachPosts", element="Space", draw=_rh.reach_posts, subdir="Parts", family=False,
         exempt=PART_EXEMPT, act="Blade Reach: the posts at the rest length and the full reach"),

    dict(name="Swordfish_Quickdraw", element="Time", draw=quickdraw, exempt=COMPOSITE,
         act="Quickdraw: the bill snapped out of the snout along its swing arc; Flurry opens the arc both ways (composite)"),
    dict(name="Swordfish_QuickdrawSocket", element="Time", draw=qd_socket, subdir="Parts", family=False,
         exempt=PART_EXEMPT, act="Quickdraw: the snout the bill swings from (white)"),
    dict(name="Swordfish_QuickdrawBill", element="Time", draw=qd_bill, subdir="Parts", family=False,
         exempt=PART_EXEMPT, act="Quickdraw: the bill out to the right (white)"),
    dict(name="Swordfish_QuickdrawArc", element="Time", draw=qd_arc, subdir="Parts", family=False,
         exempt=PART_EXEMPT, act="Quickdraw: the swing's arc (white; brighter as Time speeds the sword)"),
    dict(name="Swordfish_QuickdrawFlurry", element="Time", draw=qd_flurry, subdir="Parts", family=False,
         exempt=PART_EXEMPT, act="Flurry: the arc back the other way (white; Time 5)"),

    dict(name="Swordfish_Surge", element="Omni", draw=surge, subdir="Core", family=False,
         exempt={"components": "the hull and eight bills", "specks": "the hull's fins", "gap": "the bills stand off the hull",
                 "thin": "the bills are slim", "in_box": "the ring of bills fills the card"},
         act="The OMNI crystal card: the Surge - eight bills standing round the hull (one white sprite)"),
]

_SURGE_STATES = [("an omni crystal collected", [("i", "white")])]
_QD_STATES = [("Time 0: ready", [("socket", "white"), ("arc", "white", 0.55), ("bill", "white")]),
              ("just swung (recovery draining)", [("socket", "white"), ("arc", "white", 0.55), ("bill", "white")]),
              ("Time 10: quick", [("socket", "white"), ("arc", "white"), ("bill", "white")]),
              ("Time 5: Flurry", [("socket", "white"), ("arc", "white"), ("flurry", "white"), ("bill", "white")])]
_QD_GAUGE = {"Time 0: ready": (0.0, None), "just swung (recovery draining)": (0.6, None),
             "Time 10: quick": (0.0, None), "Time 5: Flurry": (0.0, None)}
_REACH_PARTS = dict(p=lambda: _reach_prism(reach_len(0.4), False),
                    q=lambda: _reach_prism(reach_len(1.0), True),
                    b0=lambda: flat_bill(reach_len(0.0)) | reach_socket(),
                    b4=lambda: flat_bill(reach_len(0.4)) | reach_socket(),
                    b1=lambda: flat_bill(reach_len(1.0)) | reach_socket(),
                    t=_rh.reach_posts,
                    p0=lambda: _reach_prism(reach_len(0.0), False))

CARDS = [
    dict(name="energize", element="Charge", before=RHINO_DIR + "Rhino_Katana.png",
         title="Energize: the bill follows the real sword out of the hull's snout (from behind): foreshortened ahead, "
               "out flat to a side on one trigger, raised on both; white dark, danger lit",
         parts=_BP, states=_BS, gauge=_BG),
    dict(name="afterimage", element="Mass", before=RHINO_DIR + "Rhino_Afterimage.png",
         title="Afterimage: the bill (white) swinging past two prism copies of itself in your colour while they are laid",
         parts=dict(blade=af_blade, near=af_near, far=af_far),
         states=[("idle: no afterimages", [("blade", "white")]), ("laying (lit)", AF_LIT)]),
    dict(name="bladereach", element="Space", before=RHINO_DIR + "Rhino_BladeReach.png",
         title="Blade Reach: the bill at its live length (24 -> 40), its tip binding in a super-shielded prism; "
               "shattered once Shield Breaker (Space 5) is active",
         parts=_REACH_PARTS,
         states=[("Space 0", [("p0", "shielded"), ("b0", "white"), ("t", "white", 0.6)]),
                 ("Space 4", [("p", "shielded"), ("b4", "white"), ("t", "white", 0.6)]),
                 ("Space 10: Shield Breaker", [("q", "shielded"), ("b1", "white"), ("t", "white", 0.6)])]),
    dict(name="quickdraw", element="Time", before=RHINO_DIR + "Rhino_RampSpool.png", gauge=_QD_GAUGE,
         title="Quickdraw: the bill snapped out along its swing; the plate is the swing's recovery draining away (Time "
               "shortens it); Flurry (Time 5) opens the swing back the other way - no recovery",
         parts=dict(socket=qd_socket, bill=qd_bill, arc=qd_arc, flurry=qd_flurry), states=_QD_STATES),
    dict(name="omni", element="Omni", title="The OMNI crystal card: the Surge - eight bills round the hull for 8 s",
         parts=dict(i=surge), states=_SURGE_STATES),
]

LOCKUP = dict(
    Charge=dict(parts=dict(socket=ch_socket, b=lambda: _clear_of(bill_pose(0.6, 0.0), socket(CH_HILT), px=2.0)), state=[("socket", "white"), ("b", "white")], gauge=0.0),
    Mass=dict(parts=dict(blade=af_blade, near=af_near, far=af_far), state=AF_LIT),
    Space=dict(parts=_REACH_PARTS, state=[("p", "shielded"), ("b4", "white"), ("t", "white", 0.6)]),
    Time=dict(parts=dict(socket=qd_socket, bill=qd_bill, arc=qd_arc), state=_QD_STATES[2][1], gauge=0.0),
    Omni=dict(parts=dict(i=surge), state=[("i", "white")]),
)

# ---- the HUD: SwordfishHUDVariant (a copy of the Rhino's, 2026-11-12) and SwordfishVesselHUDView ------------------
VARIANT = "Assets/_Prefabs/UI Elements/VesselHUD/SwordfishHUDVariant.prefab"
VIEW = "5161366343835098938"   # SwordfishVesselHUDView on the variant
SLOT = dict(Charge="7720000000000003015", Mass="7720000000000003025", Space="7720000000000003035",
            Time="7720000000000003045")

REBIND = [
    # Charge: the snout (white, a prefab part) and the bill the view generates on a pivot at the hilt. The omni card's
    # art rides this step (the view's omniAbilitySprite).
    dict(file=VARIANT, pair=dict(container=SLOT["Charge"], view=VIEW, after="highlights",
                                 children=[("BillSocket", "Swordfish_BillSocket", "billSocketIcon")],
                                 fields={"billSprite": ("icon", "Swordfish_Bill"),
                                         "omniAbilitySprite": ("icon", "Swordfish_Surge")})),
    # Mass: two prism copies (domain colour, shown while laid) under the bill (white).
    dict(file=VARIANT, pair=dict(container=SLOT["Mass"], view=VIEW, after="highlights",
                                 children=[("AfterimageFar", "Swordfish_AfterimageFar", "afterimageFarIcon"),
                                           ("AfterimageNear", "Swordfish_AfterimageNear", "afterimageNearIcon"),
                                           ("AfterimageBlade", "Swordfish_AfterimageBlade", "afterimageBladeIcon")])),
    # Space: the view lays the five parts out live (no prefab children).
    dict(file=VARIANT, pair=dict(container=SLOT["Space"], view=VIEW, after="highlights", children=[],
                                 fields={"reachHiltSprite": ("icon", "Swordfish_ReachHilt"),
                                         "reachBladeSprite": ("icon", "Swordfish_ReachBlade"),
                                         "reachPrismSprite": ("icon", "Swordfish_ReachPrism"),
                                         "reachPrismBrokenSprite": ("icon", "Swordfish_ReachPrismBroken"),
                                         "reachPostsSprite": ("icon", "Swordfish_ReachPosts")})),
    # Time: the swooshes under the bill and its snout. The plate keeps its gauge (TimeGauge): the swing's recovery.
    dict(file=VARIANT, pair=dict(container=SLOT["Time"], view=VIEW, after="highlights",
                                 children=[("QuickdrawFlurry", "Swordfish_QuickdrawFlurry", "quickdrawFlurryIcon"),
                                           ("QuickdrawArc", "Swordfish_QuickdrawArc", "quickdrawArcIcon"),
                                           ("QuickdrawBill", "Swordfish_QuickdrawBill", "quickdrawBillIcon"),
                                           ("QuickdrawSocket", "Swordfish_QuickdrawSocket", "quickdrawSocketIcon")])),
]
