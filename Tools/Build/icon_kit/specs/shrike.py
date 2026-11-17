"""The Shrike's ability row (R_VesselActions/SHRIKE.md §3-4), redrawn through the REAL hull in parts the HUD drives.

The roster pass (2026-11-07): every card draws the Shrike from `Shrike.fbx` from above (nose right), the way it reads at a
glance - swept fins, hooked prow, long boom. Picks made by the session on Garrett's go-ahead to choose:

  Charge  Targeting Array  the Shrike (domain) painting a rival prism (danger) inside a reticle (white; a ring with four
                           ticks - never the rhombus, which the Time petal owns); painted and locked, the reticle goes
                           solid and two missiles (white) come in. The missile COUNT stays generated under the card.
  Mass    Nest             the Shrike docked in a cup of super-shielded blocks (white squares with a negative-space X),
                           its way out open; a nest flora carrying its missile stands on the cup once one is built.
  Space   Void Lance       the Shrike's beam (white) breaking the prisms in its line (danger, split); Void Pierce
                           (Space 5): a super-shielded block on the line, whole at rest and cut by the beam while it fires.
  Time    Void Sling       the tether (white) from the Shrike to its anchor block; slinging, it lays its thick blocks
                           (domain) behind it.
  Omni    the plain omni crystal (a crystal resets the sling and feeds the nests; one white sprite).

The selected mode's gauge no longer turns the CTA lime: it turns the pilot's domain (the lime is never an "on" colour).
Before: Shrike_TargetingArray / _Nest / _VoidLance / _VoidSling (author_shrike_icons.py), kept as the slot sprites' files.
"""
import importlib
import math

import mesh_silhouette
import toy_portraits
from canvas import empty, fill, rect, ring, stroke
from motifs import prism_halves, prism_square_x

HULL = "Shrike"
FBX = "Assets/_Models/Vessel Models/Shrike.fbx"
ICON_DIR = "Assets/_Graphics/Icons/AbilityIcons/Shrike/"
_S = {}


def shrike(box, cx, cy, roll=0.0):
    """The hull from above, nose right, turned `roll` clockwise."""
    k = (box, cx, cy, roll)
    if k not in _S:
        _S[k] = mesh_silhouette.silhouette(FBX, pitch=0, yaw=0, box=box, cx=cx, cy=cy, roll=roll, dilate_px=1.0)
    return _S[k].copy()


def grown(m, px):
    return toy_portraits.grown(m, px)


def dart(x, y, deg, length=14, w=6):
    """A missile: a notched dart centred on (x, y), pointing along `deg` (clockwise from +x)."""
    a = math.radians(deg)
    ux, uy = math.cos(a), math.sin(a)
    px, py = -uy, ux
    return fill([(x + ux * length / 2, y + uy * length / 2),
                 (x - ux * length / 2 + px * w / 2, y - uy * length / 2 + py * w / 2),
                 (x - ux * length / 3, y - uy * length / 3),
                 (x - ux * length / 2 - px * w / 2, y - uy * length / 2 - py * w / 2)])


def square(x, y, s):
    return fill([(x - s, y - s), (x + s, y - s), (x + s, y + s), (x - s, y + s)])


# ---- Charge: Targeting Array ------------------------------------------------------------------------------------------------
TGT = (90, 38)


def a_shrike():
    return shrike(64, 34, 92, roll=-40)


def a_reticle():
    r = ring(*TGT, 22, w=5)
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        r |= stroke([(TGT[0] + dx * 16, TGT[1] + dy * 16), (TGT[0] + dx * 30, TGT[1] + dy * 30)], w=5)
    return r


def a_target():
    return prism_halves(*TGT, 12)


def a_missiles():
    return (dart(50, 30, 0, 22, 11) | dart(92, 100, -90, 22, 11)) & ~grown(a_reticle(), 2.5)


def a_salvo():
    """Charge 5 Salvo: a second pair of missiles behind the first (every flora launches twice as often)."""
    return (dart(28, 30, 0, 18, 9) | dart(112, 104, -90, 18, 9)) & ~grown(a_reticle() | a_missiles(), 3.0)


# ---- Mass: Nest -------------------------------------------------------------------------------------------------------------
N_C, N_R = (58, 78), 38


def n_shrike():
    return shrike(54, N_C[0] + 4, N_C[1])


def n_cup():
    out = empty()
    for k in range(9):
        a = math.radians(70 + k * 27.5)
        out |= prism_square_x(N_C[0] + N_R * math.cos(a), N_C[1] + N_R * math.sin(a), 8.0, cut=2.2)
    return out


def n_flora():
    x = N_C[0] - 14
    return stroke([(x, N_C[1] - N_R - 4), (x, 22)], w=4.5) | dart(x, 15, -90, 20, 11)


def n_armed():
    """Mass 5 Armed Nests: the left-behind flora's second missile, beside the first."""
    x = N_C[0] - 14
    return dart(x + 16, 22, -90, 20, 11) & ~grown(n_flora(), 2.5)


# ---- Space: Void Lance ------------------------------------------------------------------------------------------------------
L_Y = 64
L_PR = ((78, 18),)


def l_shrike():
    return shrike(62, 31, L_Y)


def l_beam():
    return rect(60, L_Y - 4.5, 122, L_Y + 4.5)


def l_prisms():
    out = empty()
    for x, s in L_PR:
        out |= prism_halves(x, L_Y, s, gap=2)
    return out


def l_split():
    out = empty()
    for x, s in L_PR:
        out |= prism_halves(x, L_Y, s, gap=15)
    return out & ~grown(l_beam(), 2.5)


def l_pierce_whole():
    return prism_square_x(108, L_Y, 11.5, cut=3.0)


def l_pierce_cut():
    return l_pierce_whole() & ~grown(l_beam(), 2.2)


# ---- Time: Void Sling -------------------------------------------------------------------------------------------------------
ANCHOR = (104, 26)


def s_shrike():
    return shrike(52, 56, 70, roll=-45)


def s_anchor():
    return square(*ANCHOR, 12)


def s_tether():
    return stroke([(72, 54), (ANCHOR[0] - 14, ANCHOR[1] + 14)], w=4)


def s_blocks():
    out = empty()
    for i, (x, y) in enumerate(((36, 92), (20, 108))):
        out |= square(x, y, 9 - i * 1.5)
    return out


def s_whip():
    """Time 5 Whipcrack: two speed marks either side of the tether (the sling lands in 0.6 s)."""
    (x0, y0), (x1, y1) = (72, 54), (ANCHOR[0] - 14, ANCHOR[1] + 14)
    dx, dy = x1 - x0, y1 - y0
    m = math.hypot(dx, dy)
    ux, uy, nx, ny = dx / m, dy / m, -dy / m, dx / m
    out = empty()
    for side in (-1, 1):
        cx, cy = x0 + ux * m * 0.45 + nx * 11 * side, y0 + uy * m * 0.45 + ny * 11 * side
        out |= stroke([(cx - ux * 9, cy - uy * 9), (cx + ux * 9, cy + uy * 9)], w=5)
    return out & ~grown(s_tether() | s_anchor() | s_shrike(), 2.5)


# ---- Omni -------------------------------------------------------------------------------------------------------------------
_scarab = importlib.import_module("specs.scarab")


def omni_crystal_card():
    return _scarab.omni_crystal(64, 64, 104)


PART_EXEMPT = {"at40": "a part: the HUD draws the card whole", "components": "a part", "gap": "a part",
               "coverage": "a part", "box_fill": "a part", "in_box": "a part", "thin": "a part", "specks": "a part"}

ARRAY_PARTS = [("ArrayShrike", "Shrike_ArrayShrike", "arrayShrikeIcon", a_shrike, "the Shrike painting (domain)"),
               ("ArrayReticle", "Shrike_ArrayReticle", "arrayReticleIcon", a_reticle,
                "the reticle on the target (white; faint until painted)"),
               ("ArrayTarget", "Shrike_ArrayTarget", "arrayTargetIcon", a_target, "the rival prism painted (danger)"),
               ("ArrayMissiles", "Shrike_ArrayMissiles", "arrayMissilesIcon", a_missiles,
                "the flora's missiles coming in (white; painted with a missile ready)"),
               ("ArraySalvo", "Shrike_ArraySalvo", "arraySalvoIcon", a_salvo,
                "Salvo: a second pair of missiles (white; Charge 5, painted)")]
NEST_PARTS = [("NestShrike", "Shrike_NestShrike", "nestShrikeIcon", n_shrike, "the Shrike docked (domain)"),
              ("NestCup", "Shrike_NestCup", "nestCupIcon", n_cup, "the nest's super-shielded cup (white; faint undocked)"),
              ("NestFlora", "Shrike_NestFlora", "nestFloraIcon", n_flora, "a nest flora with its missile (white)"),
              ("NestArmed", "Shrike_NestArmed", "nestArmedIcon", n_armed,
               "Armed Nests: the flora's second missile (white; Mass 5, a nest built)")]
LANCE_PARTS = [("LanceShrike", "Shrike_LanceShrike", "lanceShrikeIcon", l_shrike, "the Shrike (domain)"),
               ("LanceBeam", "Shrike_LanceBeam", "lanceBeamIcon", l_beam, "the Void Lance (white; firing)"),
               ("LancePrisms", "Shrike_LancePrisms", "lancePrismsIcon", l_prisms, "the prisms in its line (danger)"),
               ("LanceSplit", "Shrike_LanceSplit", "lanceSplitIcon", l_split, "those prisms broken (danger; firing)"),
               ("LancePierce", "Shrike_LancePierce", "lancePierceIcon", l_pierce_whole,
                "Void Pierce: a super-shielded block on the line (white; Space 5)"),
               ("LancePierceCut", "Shrike_LancePierceCut", "lancePierceCutIcon", l_pierce_cut,
                "Void Pierce: the block cut by the beam (white; Space 5, firing)")]
SLING_PARTS = [("SlingShrike", "Shrike_SlingShrike", "slingShrikeIcon", s_shrike, "the Shrike (domain)"),
               ("SlingAnchor", "Shrike_SlingAnchor", "slingAnchorIcon", s_anchor, "the anchor block (white)"),
               ("SlingTether", "Shrike_SlingTether", "slingTetherIcon", s_tether, "the tether (white; faint until shot)"),
               ("SlingBlocks", "Shrike_SlingBlocks", "slingBlocksIcon", s_blocks, "the blocks it lays slinging (domain)"),
               ("SlingWhip", "Shrike_SlingWhip", "slingWhipIcon", s_whip,
                "Whipcrack: speed marks along the tether (white; Time 5)")]

ICONS = []
for element, parts in (("Charge", ARRAY_PARTS), ("Mass", NEST_PARTS), ("Space", LANCE_PARTS), ("Time", SLING_PARTS)):
    ICONS += [dict(name=i, element=element, act=act, draw=draw, subdir="Parts", family=False, exempt=PART_EXEMPT)
              for _n, i, _f, draw, act in parts]
ICONS += [dict(name="Shrike_OmniCrystal", element="Omni", subdir="Core", family=False, draw=omni_crystal_card,
               act="The OMNI crystal card: the omni crystal (it resets the sling and feeds the nests; one white sprite)",
               exempt={"components": "the crystal's plates, projected from its own mesh",
                       "specks": "the omni crystal's small triangle plates (as Scarab_BallForge)"})]

_parts = lambda lst: {n: d for n, _i, _f, d, _a in lst}   # noqa: E731
PAINTED = [("ArrayShrike", "domain"), ("ArrayReticle", "white"), ("ArrayTarget", "danger"), ("ArrayMissiles", "white")]
DOCKED = [("NestShrike", "domain"), ("NestCup", "white")]
FIRING = [("LanceShrike", "domain"), ("LanceBeam", "white"), ("LanceSplit", "danger")]
SLINGING = [("SlingShrike", "domain"), ("SlingAnchor", "white"), ("SlingTether", "white"), ("SlingBlocks", "domain")]

CARDS = [
    dict(name="array", element="Charge", before=ICON_DIR + "Shrike_TargetingArray.png", before_gauge=True,
         gauge={"idle": (0.0, None), "painting, missiles ready": (1.0, None)},
         title="Targeting Array (Charge): the Shrike (domain) paints a rival prism (danger); locked, the reticle goes solid "
               "and the flora's missiles come in; the count stays under the card",
         parts=_parts(ARRAY_PARTS),
         states=[("idle", [("ArrayShrike", "domain"), ("ArrayReticle", "white", 0.35), ("ArrayTarget", "danger")]),
                 ("painting, missiles ready", PAINTED),
                 ("Charge 5: Salvo", PAINTED + [("ArraySalvo", "white")])],
         lockup_state=1),
    dict(name="nest", element="Mass", before=ICON_DIR + "Shrike_Nest.png", before_gauge=True,
         gauge={"flying": (1.0, None), "docked": (1.0, None), "a nest built": (0.2, None)},
         title="Nest (Mass): the Shrike docked in a cup of super-shielded blocks, its way out open; a nest flora with "
               "its missile once one is built; the plate is the nest cooldown",
         parts=_parts(NEST_PARTS),
         states=[("flying", [("NestShrike", "domain"), ("NestCup", "white", 0.35)]),
                 ("docked", DOCKED),
                 ("a nest built", DOCKED + [("NestFlora", "white")]),
                 ("Mass 5: Armed Nests", DOCKED + [("NestFlora", "white"), ("NestArmed", "white")])],
         lockup_state=1),
    dict(name="lance", element="Space", before=ICON_DIR + "Shrike_VoidLance.png", before_gauge=True,
         gauge={"ready": (1.0, None), "firing": (0.3, None), "Space 5: Void Pierce": (0.3, None)},
         title="Void Lance (Space): the beam (white) breaks the prisms in its line (danger); Void Pierce cuts a "
               "super-shielded block too; the plate is the cooldown",
         parts=_parts(LANCE_PARTS),
         states=[("ready", [("LanceShrike", "domain"), ("LancePrisms", "danger")]),
                 ("firing", FIRING),
                 ("Space 5: Void Pierce", FIRING + [("LancePierceCut", "white")])],
         lockup_state=1),
    dict(name="sling", element="Time", before=ICON_DIR + "Shrike_VoidSling.png", before_gauge=True,
         gauge={"ready": (1.0, None), "slinging": (0.2, None)},
         title="Void Sling (Time): the tether (white) from the Shrike to its anchor; slinging, it lays its blocks "
               "(domain) behind it; the plate is the cooldown",
         parts=_parts(SLING_PARTS),
         states=[("ready", [("SlingShrike", "domain"), ("SlingAnchor", "white"), ("SlingTether", "white", 0.35)]),
                 ("slinging", SLINGING),
                 ("Time 5: Whipcrack", SLINGING + [("SlingWhip", "white")])],
         lockup_state=1),
    dict(name="omni", element="Omni", title="The OMNI crystal card: the omni crystal",
         parts=dict(i=omni_crystal_card), states=[("an omni crystal collected", [("i", "white")])]),
]
LOCKUP = dict(
    Charge=dict(parts=_parts(ARRAY_PARTS), state=PAINTED, gauge=1.0),
    Mass=dict(parts=_parts(NEST_PARTS), state=DOCKED, gauge=1.0),
    Space=dict(parts=_parts(LANCE_PARTS), state=FIRING, gauge=0.3),
    Time=dict(parts=_parts(SLING_PARTS), state=SLINGING, gauge=0.2),
    Omni=dict(parts=dict(i=omni_crystal_card), state=[("i", "white")]),
)

VARIANT = "Assets/_Prefabs/UI Elements/VesselHUD/ShrikeHUDVariant.prefab"
VIEW = "5875684227236902712"            # ShrikeHUDView on the variant (the Brittle Star clone's fileIDs)
SLOT = dict(Charge="7700000000000001105", Mass="7700000000000001115", Space="7700000000000001125",
            Time="7700000000000001135")
REBIND = [
    dict(file=VARIANT, pair=dict(container=SLOT[el], view=VIEW, after="colorBlendRate",
                                 children=[(n, i, f) for n, i, f, _d, _a in parts],
                                 **({"fields": {"omniAbilitySprite": ("icon", "Shrike_OmniCrystal")}} if el == "Time" else {})))
    for el, parts in (("Charge", ARRAY_PARTS), ("Mass", NEST_PARTS), ("Space", LANCE_PARTS), ("Time", SLING_PARTS))
]
