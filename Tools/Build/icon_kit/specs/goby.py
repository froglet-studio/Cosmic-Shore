"""The Goby & Pistol's ability row - both seats - through the REAL hulls (`Goby.fbx`, `Pistol.fbx`).

The roster pass (2026-11-07). The seats' cards were placeholders written by `author_goby_hud.py` from their phone
buttons' motifs (brackets, arrows, a pac-man): every card now draws the hull that flies it. The kit owns the PNGs now;
`author_goby_hud.py` keeps their guids (its `guid_for`) and stops writing them. Single white sprites, swapped per seat
by `GobyPistolHUDView.SetSeat` (no tinted parts: the seat swap is a sprite swap). Picks made by the session on
Garrett's go-ahead to choose (`.claude/skills/duo-vessel/references/designs/goby-and-pistol.md` §11):

  card     the Goby (from above, nose right)               the Pistol (three-quarter side-on, the claw forward)
  Charge   Lock: the Goby's nose on a rival hull held in   Snap: the claw shut (take `snap`), the vortex ring leaving
           four brackets                                   it and a fainter one behind
  Mass     Perch: the Goby over the den its perch starts   Den: the Pistol resting by the den it built, on the ground
  Space    Undertow: the plane (a bar), mass beside it     Ghost: the Pistol (faint) passing through a wall of prisms
           solid where it has been pulled in, a ghost
           where it was
  Time     Dart: the Goby, its ghost where it charged,     Dive: the Pistol nose-down over a hill (Tiny Wings)
           and the block its dart stops on
  Omni     the omni crystal (one white sprite; the HUD had none)

Prisms are `prism_square_x` (a square with a negative-space X): never a rhombus (the Time petal owns that shape).
"""
import importlib
import math

import numpy as np

import mesh_silhouette
from canvas import SS, empty, fill, rect, stroke
from motifs import prism_square_x

HULL = "Goby"
GOBY = "Assets/_Models/Vessel Models/Goby.fbx"
PISTOL = "Assets/_Models/Vessel Models/Pistol.fbx"
RIVAL = "Assets/_Models/Vessel Models/SquirrelVessel_CosmicShoresTest1.fbx"
ICON_DIR = "Assets/_Graphics/Icons/AbilityIcons/Goby/"
HULL_EXEMPT = {"gap": "the hull's real mesh: fins, legs and antennae float across even gaps by design (vessel-model SKILL 0.5)",
               "components": "the hull's real mesh: the Pistol's legs and antennae are separate pieces",
               "specks": "the hull's real mesh: leg tips and fin ends"}

PRISM_EXEMPT = {"gap": "prism_square_x's negative-space X (the fleet's prism; the Thresher and Stoat dens)",
                "components": "a den or a pull is many separate prisms"}


def goby(box, cx, cy, roll=0.0):
    """The Goby from just above (pitch -15: its fins read), nose right, turned `roll` clockwise."""
    return mesh_silhouette.silhouette(GOBY, pitch=-15, yaw=0, box=box, cx=cx, cy=cy, roll=roll, dilate_px=1.8)


def pistol(box, cx, cy, roll=0.0, take=None, pitch=-30, dil=1.6):
    """The Pistol three-quarter side-on (pitch -30: its claw, legs and antennae all read), nose right."""
    return mesh_silhouette.silhouette(PISTOL, pitch=pitch, yaw=0, box=box, cx=cx, cy=cy, roll=roll, dilate_px=dil,
                                      take=take)


def grow(mask, px):
    from PIL import Image, ImageFilter
    r = int(px * SS) * 2 + 1
    return np.asarray(Image.fromarray((mask * 255).astype("uint8")).filter(ImageFilter.MaxFilter(r))) > 127


def fit(art, k=0.84):
    """Scale a composition drawn on the whole 128 canvas into the 104 px optical box (about the centre)."""
    from PIL import Image
    def one(m):
        n = m.shape[0]
        s = int(round(n * k))
        im = Image.fromarray((m * 255).astype("uint8")).resize((s, s), Image.LANCZOS)
        out = Image.new("L", (n, n), 0)
        out.paste(im, ((n - s) // 2, (n - s) // 2))
        return np.asarray(out) > 127
    if isinstance(art, list):
        return [(one(m), a) for m, a in art]
    return one(art)


def ellipse_ring(cx, cy, rx, ry, w):
    t = np.linspace(0, 2 * math.pi, 72)
    return stroke([(cx + rx * math.cos(a), cy + ry * math.sin(a)) for a in t], w=w, closed=True)


def fitted(f):
    def g():
        return fit(f())
    g.__name__ = f.__name__
    return g


# ---- the Goby's seat ---------------------------------------------------------------------------------------------

@fitted
def goby_lock():
    hull = goby(60, 30, 96, roll=-40)
    rival = mesh_silhouette.silhouette(RIVAL, pitch=-15, yaw=0, box=56, cx=82, cy=46, roll=-40, dilate_px=2.0)
    brackets = empty()
    for sx in (-1, 1):
        for sy in (-1, 1):
            cx, cy = 82 + sx * 40, 46 + sy * 40
            brackets |= stroke([(cx, cy - sy * 15), (cx, cy), (cx - sx * 15, cy)], w=7)
    return hull | brackets | rival


@fitted
def goby_perch():
    den = empty()
    for x, y in ((28, 110), (52, 110), (76, 110), (100, 110), (40, 88), (64, 88), (88, 88)):
        den |= prism_square_x(x, y, 10)
    hull = goby(108, 64, 44)
    return (hull & ~grow(den, 2)) | den


@fitted
def goby_undertow():
    plane = rect(58, 2, 70, 126)
    pulled = prism_square_x(40, 26, 12) | prism_square_x(40, 64, 12) | prism_square_x(88, 64, 12) | prism_square_x(88, 102, 12)
    was = prism_square_x(13, 26, 12) | prism_square_x(13, 64, 12) | prism_square_x(115, 64, 12) | prism_square_x(115, 102, 12)
    return [(plane | pulled, 1.0), (was, 0.4)]


@fitted
def goby_dart():
    hull = goby(80, 70, 64)
    ghost = goby(64, 22, 64) & ~grow(hull, 2)
    block = prism_square_x(120, 64, 12)
    return [(hull | block, 1.0), (ghost, 0.5)]


# ---- the Pistol's seat -------------------------------------------------------------------------------------------

@fitted
def pistol_snap():
    hull = pistol(86, 46, 66, take="rig|snap")
    ring = ellipse_ring(106, 62, 10, 27, 7)
    trail = ellipse_ring(88, 62, 5.5, 16, 5) & ~grow(hull, 2)
    return [(hull | ring, 1.0), (trail, 0.45)]


@fitted
def pistol_den():
    den = empty()
    for x, y in ((76, 104), (96, 104), (116, 104), (86, 84), (106, 84), (96, 64)):
        den |= prism_square_x(x, y, 9.5)
    hull = pistol(84, 44, 52, dil=2.4)
    ground = rect(4, 116, 124, 123)
    return (hull & ~grow(den, 2)) | den | ground


@fitted
def pistol_ghost():
    wall = empty()
    for y in (16, 38, 60, 82, 104):
        wall |= prism_square_x(70, y, 11.5)
    hull = pistol(108, 60, 64, dil=2.6)
    return [(wall & ~grow(hull, 2.5), 1.0), (hull, 0.7)]


@fitted
def pistol_dive():
    xs = np.linspace(0, 128, 200)
    top = [(x, 84 - 30 * math.cos((x - 14) / 128 * 2 * math.pi)) for x in xs]
    hill = fill(top + [(128, 128), (0, 128)])
    hull = pistol(80, 54, 62, roll=34)
    return (hill & ~grow(hull, 3)) | hull


# ---- level 5 (the fleet pass, 2026-11-13): each the seat's upgraded icon, swapped in by GobyPistolHUDView when the
# card's element reaches its level-5 upgrade, on the seat whose act the upgrade changes (the other seat keeps its card)

def _petal(cx, cy, s, rot=0.0):
    from canvas import rotate
    return fill(rotate([(cx, cy - s), (cx + s * 0.55, cy), (cx, cy + s), (cx - s * 0.55, cy)], rot, cx, cy))


def _shell(cx, cy, r, w=5.0):
    """A shielded prism's shell: a hexagonal band round it (the Wrasse's Shielded Clusters shell)."""
    from canvas import ngon
    return fill(ngon(6, r, cx, cy, rot=30.0)) & ~fill(ngon(6, r - w, cx, cy, rot=30.0))


@fitted
def pistol_vortex_steal():
    """Charge 5 VORTEX STEAL: the ring closed on a rival hull, and the petals it took coming back to the Pistol."""
    import motifs
    hull = pistol(70, 34, 40, take="rig|snap")
    rival = mesh_silhouette.silhouette(RIVAL, pitch=-15, yaw=0, box=40, cx=96, cy=92, roll=-40, dilate_px=2.0)
    ring = ellipse_ring(96, 92, 30, 30, 7)
    petals = _petal(76, 62, 11, 45) | _petal(52, 82, 11, 45)
    heads = motifs.arrowhead((60.0, 52.0), (-0.7, -0.7), 13.0) | motifs.arrowhead((36.0, 72.0), (-0.7, -0.7), 13.0)
    return hull | rival | (ring & ~grow(rival, 3)) | ((petals | heads) & ~grow(hull | ring, 2.5))


@fitted
def pistol_fortress():
    """Mass 5 FORTRESS: the den grows shielded and super-shielded prisms - shells round the den's top courses."""
    den, shells = empty(), empty()
    for x, y in ((76, 104), (96, 104), (116, 104), (86, 84), (106, 84)):
        den |= prism_square_x(x, y, 9.5)
    top = prism_square_x(96, 60, 9.5)
    shells |= _shell(96, 60, 22, 8)
    hull = pistol(84, 40, 52, dil=2.4)
    ground = rect(4, 116, 124, 123)
    return (hull & ~grow(den | top | shells, 2)) | den | top | shells | ground


@fitted
def goby_deep_undertow():
    """Space 5 DEEP UNDERTOW: shielded prisms are pulled in too - the pulled mass beside the plane wears shells."""
    plane = rect(58, 2, 70, 126)
    pulled = prism_square_x(36, 34, 11) | prism_square_x(92, 64, 11) | prism_square_x(36, 96, 11)
    shells = _shell(92, 64, 24, 8)
    was = prism_square_x(10, 64, 10) | prism_square_x(118, 30, 10) | prism_square_x(118, 98, 10)
    return [(plane | pulled | (shells & ~grow(plane, 2)), 1.0), (was, 0.4)]


@fitted
def goby_phase_dart():
    """Time 5 PHASE DART: the dart passes through prisms - the Goby out the far side of a wall, its ghost behind."""
    wall = empty()
    for y in (22, 50, 78, 106):
        wall |= prism_square_x(58, y, 11)
    hull = goby(54, 98, 64)
    ghost = goby(46, 24, 64) & ~grow(wall | hull, 2)
    return [(hull | (wall & ~grow(hull, 2.5)), 1.0), (ghost, 0.5)]


def omni_crystal_card():
    return importlib.import_module("specs.scarab").omni_crystal(64, 64, 104)


ICONS = [
    dict(name="Goby_Lock", element="Charge", act="Lock (the Goby): its nose on a rival held in four brackets",
         draw=goby_lock, before=ICON_DIR + "Goby_Lock.png", exempt=HULL_EXEMPT),
    dict(name="Goby_Perch", element="Mass", act="Perches (the Goby): the Goby over the den its perch starts",
         draw=goby_perch, before=ICON_DIR + "Goby_Perch.png", exempt=HULL_EXEMPT),
    dict(name="Goby_Undertow", element="Space", act="Undertow (passive): mass beside the plane pulled into it",
         draw=goby_undertow, before=ICON_DIR + "Goby_Undertow.png", exempt=PRISM_EXEMPT),
    dict(name="Goby_Dart", element="Time", act="Dart (the Goby): from its ghost to the block it stops on",
         draw=goby_dart, before=ICON_DIR + "Goby_Dart.png", exempt=HULL_EXEMPT),
    dict(name="Pistol_Snap", element="Charge", act="Snap (the Pistol): the claw shut, the vortex ring leaving it",
         draw=pistol_snap, before=ICON_DIR + "Pistol_Snap.png", exempt=HULL_EXEMPT, family=False),
    dict(name="Pistol_Den", element="Mass", act="Den (the Pistol): resting by the den its energy built",
         draw=pistol_den, before=ICON_DIR + "Pistol_Den.png", exempt=HULL_EXEMPT, family=False),
    dict(name="Pistol_Ghost", element="Space", act="Ghost (the Pistol): passing through a wall of prisms",
         draw=pistol_ghost, before=ICON_DIR + "Pistol_Ghost.png", exempt=HULL_EXEMPT, family=False),
    dict(name="Pistol_Dive", element="Time", act="The dive (the Pistol): nose-down over a hill",
         draw=pistol_dive, before=ICON_DIR + "Pistol_Dive.png", exempt=HULL_EXEMPT, family=False),
    dict(name="Pistol_VortexSteal", element="Charge", act="Charge 5 Vortex Steal: a ring's hit steals the petals it takes",
         draw=pistol_vortex_steal, exempt=HULL_EXEMPT, family=False),
    dict(name="Pistol_Fortress", element="Mass", act="Mass 5 Fortress: the den grows shielded and super-shielded prisms",
         draw=pistol_fortress, exempt=HULL_EXEMPT, family=False),
    dict(name="Goby_DeepUndertow", element="Space", act="Space 5 Deep Undertow: shielded prisms are pulled in too",
         draw=goby_deep_undertow, exempt=PRISM_EXEMPT, family=False),
    dict(name="Goby_PhaseDart", element="Time", act="Time 5 Phase Dart: the dart passes through prisms",
         draw=goby_phase_dart, exempt=HULL_EXEMPT, family=False),
    dict(name="Goby_OmniCrystal", element="Omni", subdir="Core", family=False, draw=omni_crystal_card,
         act="The OMNI crystal card: the omni crystal (one white sprite)",
         exempt={"components": "the crystal's plates, projected from its own mesh",
                 "specks": "the omni crystal's small triangle plates (as Scarab_BallForge)"}),
]
