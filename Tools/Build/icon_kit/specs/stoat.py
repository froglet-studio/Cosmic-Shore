"""The Stoat's ability row - the orbit-only kit (Yash, 2026-11-02; R_VesselActions/STOAT.md §6) - through the REAL hull.

The roster pass (2026-11-07). The shipped row still drew the retired kit (the Mass/Time mode switch, the field dipole, the
pathfinder); these draw what the Stoat flies today, in parts `StoatHUDView.RefreshParts` paints. `Stoat.fbx` is a long thin
hull, so it is dilated (lesson 82) and drawn side-on where its legs read (Ripple, Heavy Launch) and from above on the orbit.
Picks made by the session on Garrett's go-ahead to choose:

  Charge  Ripple            the pair let go - the attractor (solid) and repulsor (ring) meeting - throws a space-time
                            ripple (white arcs) at a fauna (danger), anti vessel and fauna only; Siphon (Charge 5): the
                            stripped petals coming back. The plate stays the ripple a let-go would throw.
  Mass    Heavy Launch      the Stoat (domain) flung out of the orbit, side-on and leaping, its kick (white) behind it and
                            its wake (domain prisms) laid along the line; Armoured Wake (Mass 5): the wake rimmed white.
  Space   Catch Orbit       the pair - the attractor (solid) and repulsor (ring) - with the Stoat caught on the orbit round
                            the attractor; Accretion (Space 5): a rival prism (danger) falling into the attractor.
  Time    Timed Slingshot   the orbit round the attractor with the 90-degree window (a thick arc); orbiting, the Stoat
                            rides the orbit and the window lights in it; let go, it flies off the tangent; Foresight (Time 5):
                            a rival's predicted path (danger, dashed).
  Omni    Fulmen            an omni crystal's branching thunderbolt (one white sprite).
"""
import importlib
import math

import mesh_silhouette
import toy_portraits
from canvas import band, disc, empty, fill, ring, stroke
from motifs import prism_halves

HULL = "Stoat"
FBX = "Assets/_Models/Vessel Models/Stoat.fbx"
ICON_DIR = "Assets/_Graphics/Icons/AbilityIcons/Stoat/"
_S = {}


def stoat(box, cx, cy, roll=0.0, dil=2.6, side=False):
    """The hull from above (or side-on, legs down), nose right, turned `roll` clockwise, dilated so the stick has mass."""
    k = (box, cx, cy, roll, dil, side)
    if k not in _S:
        _S[k] = mesh_silhouette.silhouette(FBX, pitch=-90 if side else 0, yaw=0, box=box, cx=cx, cy=cy, roll=roll,
                                           dilate_px=dil)
    return _S[k].copy()


def grown(m, px):
    return toy_portraits.grown(m, px)


# ---- Charge: Ripple ---------------------------------------------------------------------------------------------------------
RC = (40, 64)


def r_stoat():
    return stoat(72, 40, 104, dil=2.4, side=True)


def r_pair():
    return disc(RC[0] - 7, RC[1], 9) | ring(RC[0] + 9, RC[1], 7, w=4.5)


def r_rings():
    out = empty()
    for r, w in ((24, 6), (38, 5.5), (52, 5)):
        out |= band(RC[0], RC[1], r, -50, 50, w=w)
    return out


def r_fauna():
    return toy_portraits.fish(104, 64, 15)


def r_siphon():
    out = empty()
    for i in range(4):
        t = (i + 0.5) / 4
        out |= disc(96 - t * 30, 76 + t * 22, 3.2)
    return out


# ---- Mass: Heavy Launch -----------------------------------------------------------------------------------------------------
_LA = math.radians(-35)


def h_stoat():
    return stoat(84, 80, 40, roll=-35, dil=2.6, side=True)


def h_kick():
    ux, uy = math.cos(_LA), math.sin(_LA)
    px, py = -uy, ux
    out = empty()
    for off in (-9, 9):
        x0, y0 = 46 + px * off, 66 + py * off
        out |= stroke([(x0 - ux * 16, y0 - uy * 16), (x0, y0)], w=5)
    return out & ~grown(h_stoat(), 3)


def h_wake():
    out = empty()
    for i, (x, y) in enumerate(((34, 84), (22, 98), (12, 112))):
        out |= prism_halves(x, y, 8.5 - i, gap=1.6)
    return out


def h_shield():
    p = h_wake()
    return grown(p, 4.0) & ~grown(p, 2.2)


# ---- Space: Catch Orbit -----------------------------------------------------------------------------------------------------
AT, RP, OR = (54, 74), (111, 74), 36


def c_stoat():
    return stoat(70, AT[0] + 2, AT[1] - OR, dil=2.8)


def c_attractor():
    return disc(*AT, 14)


def c_repulsor():
    return ring(*RP, 10, w=5.5)


def c_orbit():
    return band(AT[0], AT[1], OR, -150, 160, w=5.5) & ~grown(c_stoat(), 3.5)


def c_prey():
    return prism_halves(14, 74, 8, gap=1.4)


def c_pull():
    return stroke([(25, 74), (34, 74)], w=4)


# ---- Time: Timed Slingshot --------------------------------------------------------------------------------------------------
TA, TR = (46, 82), 36


def t_attractor():
    return disc(*TA, 14)


def t_stoat_orbit():
    a = math.radians(-35)
    return stoat(70, TA[0] + TR * math.cos(a), TA[1] + TR * math.sin(a), roll=55, dil=3.0)


def t_orbit():
    return band(TA[0], TA[1], TR, 90, 360, w=6) & ~grown(t_stoat_orbit(), 3)


def t_window():
    return band(TA[0], TA[1], TR, -110, -70, w=13)


def t_stoat_gone():
    return stoat(70, 88, TA[1] - TR, dil=3.0)


def t_speed():
    out = empty()
    for dy in (-8, 8):
        out |= stroke([(46, TA[1] - TR + dy), (54, TA[1] - TR + dy)], w=4)
    return out


def t_foresight():
    pts = [(84 + 6 * i, 120 - 6 * i - 0.5 * i * i) for i in range(7)]
    out = empty()
    for a, b in zip(pts[::2], pts[1::2]):
        out |= stroke([a, b], w=4)
    return out


# ---- Omni: Fulmen -----------------------------------------------------------------------------------------------------------
_scarab = importlib.import_module("specs.scarab")


def fulmen():
    """The bolt: one jagged, tapering zigzag from the crystal, with a fork off its middle."""
    crystal = _scarab.omni_crystal(32, 30, 48)
    bolt = fill([(52, 44), (70, 46), (60, 66), (82, 66), (64, 92), (74, 92), (50, 122), (56, 98), (46, 98),
                 (54, 76), (36, 76)])
    fork = fill([(66, 80), (98, 78), (112, 100), (92, 88), (70, 90)])
    return crystal | ((bolt | fork) & ~grown(crystal, 3))


PART_EXEMPT = {"at40": "a part: the HUD draws the card whole", "components": "a part", "gap": "a part",
               "coverage": "a part", "box_fill": "a part", "in_box": "a part", "thin": "a part", "specks": "a part"}

RIPPLE_PARTS = [("RippleStoat", "Stoat_RippleStoat", "rippleStoatIcon", r_stoat, "the Stoat side-on (domain)"),
                ("RipplePair", "Stoat_RipplePair", "ripplePairIcon", r_pair,
                 "the pair meeting: attractor and repulsor (white)"),
                ("RippleRings", "Stoat_RippleRings", "rippleRingsIcon", r_rings,
                 "the ripple (white; faint with no pair held)"),
                ("RippleFauna", "Stoat_RippleFauna", "rippleFaunaIcon", r_fauna, "a fauna it reaches (danger)"),
                ("RippleSiphon", "Stoat_RippleSiphon", "rippleSiphonIcon", r_siphon,
                 "Siphon: the stripped petals coming back (danger; Charge 5)")]
LAUNCH_PARTS = [("LaunchStoat", "Stoat_LaunchStoat", "launchStoatIcon", h_stoat, "the Stoat leaping (domain)"),
                ("LaunchKick", "Stoat_LaunchKick", "launchKickIcon", h_kick, "the launch's kick (white; launched)"),
                ("LaunchWake", "Stoat_LaunchWake", "launchWakeIcon", h_wake, "its wake (domain; faint at rest)"),
                ("LaunchShield", "Stoat_LaunchShield", "launchShieldIcon", h_shield,
                 "Armoured Wake: the wake rimmed (white; Mass 5)")]
ORBIT_PARTS = [("OrbitStoat", "Stoat_OrbitStoat", "orbitStoatIcon", c_stoat, "the Stoat on the orbit (domain)"),
               ("OrbitAttractor", "Stoat_OrbitAttractor", "orbitAttractorIcon", c_attractor,
                "the attractor (white; faint with no pair)"),
               ("OrbitRepulsor", "Stoat_OrbitRepulsor", "orbitRepulsorIcon", c_repulsor,
                "the repulsor (white; faint with no pair)"),
               ("OrbitRing", "Stoat_OrbitRing", "orbitRingIcon", c_orbit, "the orbit (white; caught)"),
               ("OrbitPrey", "Stoat_OrbitPrey", "orbitPreyIcon", c_prey, "Accretion: a rival prism (danger; Space 5)"),
               ("OrbitPull", "Stoat_OrbitPull", "orbitPullIcon", c_pull,
                "Accretion: its fall into the attractor (white; Space 5)")]
SLING_PARTS = [("SlingAttractor", "Stoat_SlingAttractor", "slingAttractorIcon", t_attractor, "the attractor (white)"),
               ("SlingOrbit", "Stoat_SlingOrbit", "slingOrbitIcon", t_orbit, "the orbit (white)"),
               ("SlingWindow", "Stoat_SlingWindow", "slingWindowIcon", t_window,
                "the 90-degree window (white; lit inside it)"),
               ("SlingStoatOrbit", "Stoat_SlingStoatOrbit", "slingStoatOrbitIcon", t_stoat_orbit,
                "the Stoat riding the orbit (domain)"),
               ("SlingStoatGone", "Stoat_SlingStoatGone", "slingStoatGoneIcon", t_stoat_gone,
                "the Stoat let go on the tangent (domain)"),
               ("SlingSpeed", "Stoat_SlingSpeed", "slingSpeedIcon", t_speed, "the slingshot's speed (white; just let go)"),
               ("SlingForesight", "Stoat_SlingForesight", "slingForesightIcon", t_foresight,
                "Foresight: a rival's predicted path (danger; Time 5)")]

ICONS = []
for element, parts in (("Charge", RIPPLE_PARTS), ("Mass", LAUNCH_PARTS), ("Space", ORBIT_PARTS), ("Time", SLING_PARTS)):
    ICONS += [dict(name=i, element=element, act=act, draw=draw, subdir="Parts", family=False, exempt=PART_EXEMPT)
              for _n, i, _f, draw, act in parts]
ICONS += [dict(name="Stoat_Fulmen", element="Omni", subdir="Core", family=False, draw=fulmen,
               act="The OMNI crystal card: Fulmen - the omni crystal's branching thunderbolt (one white sprite)",
               exempt={"components": "the crystal's plates, projected from its own mesh",
                       "specks": "the omni crystal's small triangle plates (as Scarab_BallForge)",
                       "gap": "the omni crystal's plate seams, small beside the bolt"})]

_parts = lambda lst: {n: d for n, _i, _f, d, _a in lst}   # noqa: E731
LET_GO = [("RippleStoat", "domain"), ("RipplePair", "white"), ("RippleRings", "white"), ("RippleFauna", "danger")]
LAUNCHED = [("LaunchStoat", "domain"), ("LaunchKick", "white"), ("LaunchWake", "domain")]
CAUGHT = [("OrbitStoat", "domain"), ("OrbitAttractor", "white"), ("OrbitRepulsor", "white"), ("OrbitRing", "white")]
GONE = [("SlingAttractor", "white", 0.35), ("SlingOrbit", "white", 0.35), ("SlingWindow", "white", 0.35),
        ("SlingStoatGone", "domain"), ("SlingSpeed", "white")]

CARDS = [
    dict(name="ripple", element="Charge", before=ICON_DIR + "Stoat_Ripple.png", before_gauge=True,
         gauge={"no pair held": (0.0, None), "a pair let go": (0.7, None), "Charge 5: Siphon": (0.7, None)},
         title="Ripple (Charge): the pair let go throws a space-time ripple (white) at a fauna (danger); Siphon brings the "
               "stripped petals back; the plate is the ripple a let-go would throw",
         parts=_parts(RIPPLE_PARTS),
         states=[("no pair held", [("RippleStoat", "domain"), ("RipplePair", "white", 0.35),
                                   ("RippleRings", "white", 0.35), ("RippleFauna", "danger")]),
                 ("a pair let go", LET_GO),
                 ("Charge 5: Siphon", LET_GO + [("RippleSiphon", "danger")])],
         lockup_state=1),
    dict(name="launch", element="Mass", before=ICON_DIR + "Stoat_Modes.png",
         title="Heavy Launch (Mass): the Stoat flung out of the orbit, its kick behind it and its wake laid along the "
               "line; Armoured Wake rims the wake",
         parts=_parts(LAUNCH_PARTS),
         states=[("at rest", [("LaunchStoat", "domain"), ("LaunchWake", "domain", 0.35)]),
                 ("launched", LAUNCHED),
                 ("Mass 5: Armoured Wake", LAUNCHED + [("LaunchShield", "white")])],
         lockup_state=1),
    dict(name="orbit", element="Space", before=ICON_DIR + "Stoat_ModeTime.png", before_gauge=True,
         gauge={"no pair": (0.0, None), "caught on the orbit": (0.0, None), "Space 5: Accretion": (0.5, None)},
         title="Catch Orbit (Space): the attractor and repulsor with the Stoat caught on the orbit; Accretion pulls a "
               "rival prism in; the plate is Accretion's growth",
         parts=_parts(ORBIT_PARTS),
         states=[("no pair", [("OrbitStoat", "domain"), ("OrbitAttractor", "white", 0.35), ("OrbitRepulsor", "white", 0.35)]),
                 ("caught on the orbit", CAUGHT),
                 ("Space 5: Accretion", CAUGHT + [("OrbitPrey", "danger"), ("OrbitPull", "white")])],
         lockup_state=1),
    dict(name="sling", element="Time", before=ICON_DIR + "Stoat_Pathfinder.png", before_gauge=True,
         gauge={"orbiting": (0.5, None), "in the window": (1.0, None), "let go": (0.0, None),
                "Time 5: Foresight": (0.0, None)},
         title="Timed Slingshot (Time): the Stoat rides the orbit to the 90-degree window, then flies off the tangent; "
               "Foresight shows a rival's predicted path; the plate is the progress to the window",
         parts=_parts(SLING_PARTS),
         states=[("orbiting", [("SlingAttractor", "white"), ("SlingOrbit", "white"), ("SlingWindow", "white", 0.35),
                               ("SlingStoatOrbit", "domain")]),
                 ("in the window", [("SlingAttractor", "white"), ("SlingOrbit", "white"), ("SlingWindow", "white"),
                                    ("SlingStoatOrbit", "domain")]),
                 ("let go", GONE),
                 ("Time 5: Foresight", GONE + [("SlingForesight", "danger")])],
         lockup_state=2),
    dict(name="omni", element="Omni", title="The OMNI crystal card: Fulmen",
         parts=dict(i=fulmen), states=[("an omni crystal collected", [("i", "white")])]),
]
LOCKUP = dict(
    Charge=dict(parts=_parts(RIPPLE_PARTS), state=LET_GO, gauge=0.7),
    Mass=dict(parts=_parts(LAUNCH_PARTS), state=LAUNCHED),
    Space=dict(parts=_parts(ORBIT_PARTS), state=CAUGHT),
    Time=dict(parts=_parts(SLING_PARTS), state=GONE),
    Omni=dict(parts=dict(i=fulmen), state=[("i", "white")]),
)

VARIANT = "Assets/_Prefabs/UI Elements/VesselHUD/StoatHUDVariant.prefab"
VIEW = "5875684227236902712"            # StoatHUDView on the variant (the Thresher clone's fileIDs)
SLOT = dict(Charge="7700000000000001105", Mass="7700000000000001115", Space="7700000000000001125",
            Time="7700000000000001135")
REBIND = [
    dict(file=VARIANT, pair=dict(container=SLOT[el], view=VIEW, after="colorBlendRate",
                                 children=[(n, i, f) for n, i, f, _d, _a in parts],
                                 **({"fields": {"omniAbilitySprite": ("icon", "Stoat_Fulmen")}} if el == "Time" else {})))
    for el, parts in (("Charge", RIPPLE_PARTS), ("Mass", LAUNCH_PARTS), ("Space", ORBIT_PARTS), ("Time", SLING_PARTS))
]
