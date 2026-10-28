"""The Grizzly's ability-row icons (Resources/ElementalAbilityMaps/Grizzly.asset), angular, one object each.

  Charge  Dig In          the Grizzly (its REAL mesh, side-on, in its own `dig` take: the anchor raked forward, the
                          claws down) planted on a ground plate, the anchor's tines sunk into it - stop, dig in,
                          reload (before: SmashIcon-PLACEHOLDER.png, a fist)
  Mass    Weapon Systems  the rotary drum the Y button turns: three chambers (explosives, sniper, plasma claw) round a
                          hub, the live one under the pointer notch - cycle your weapons
                          (before: ZapIcon-PLACEHOLDER.png, a lightning bolt)
  Space   Charged Cannon  the shell leaving its muzzle blast: an angular round, nose up and to the right, its driving
                          band proud of the body, the blast it was charged with behind it - hold, release, boom
                          (before: CannonIcon-PLACEHOLDER.png)
  Time    Rush            the Grizzly (its REAL mesh, side-on) leaning nose-down into the charge, one motion bar
                          behind it - the burst of momentum (before: BoostIcon-PLACEHOLDER.png, three chevrons).
                          Round 1 drew it from above with a ghost: the plan view read as a beetle, not a bear

The hull is Grizzly.fbx (2026-10-27, Docs/VesselModels/Grizzly.md). Its HUD is the GrizzlyHUD canvas embedded in
Grizzly.prefab (no <Hull>HUDVariant yet), whose four AbilityIconBinding entries draw the placeholders: REBIND swaps
each placeholder's guid there for the icon's, once per file. CannonIcon-PLACEHOLDER.png is also a ModeControlsLibrary
icon (another screen's): left to its owner. Single white sprites, not tinted parts: GrizzlyHUDView is jtgjones's,
in flight on grizzly-v3, so the view is not touched here.
"""
import numpy as np

import mesh_silhouette
from canvas import disc, fill, ngon, rect, rotate
from motifs import burst

HULL = "Grizzly"
FBX = "Assets/_Models/Vessel Models/Grizzly.fbx"
PREFAB = "Assets/_Prefabs/Spacevessels/Grizzly.prefab"
PH = "Assets/_Graphics/{PLACEHOLDERS}/Icons/"


def _dilate(mask, px):
    """Grow a mask by `px` icon px (a square structuring element on the supersampled grid)."""
    from canvas import SS
    r = int(round(px * SS))
    out = mask.copy()
    for dx in range(-r, r + 1, max(1, r // 4)):
        for dy in range(-r, r + 1, max(1, r // 4)):
            out |= np.roll(np.roll(mask, dy, axis=0), dx, axis=1)
    return out


GROUND_TOP = 90      # 7 px under the engine pods; the plunged anchor's tines reach 10 px into the plate


def dig_in():
    """The hull side-on (pitch -90: the hump up, the nose right; +90 looks from below) in its `dig` take, standing
    on a ground plate. Its anchor tines reach below the plate's top: the plate is cut 6 px round them, so they read
    as SUNK into it, not welded to it."""
    hull = mesh_silhouette.silhouette(FBX, pitch=-90, yaw=0, box=114, cx=64, cy=62, dilate_px=1.4, take="rig|dig")
    from canvas import GY
    below = hull & (GY > GROUND_TOP)
    plate = rect(6, GROUND_TOP, 122, 108)
    plate &= ~_dilate(below, 6)
    return hull | plate


def weapon_systems():
    """A twelve-sided drum; three round chambers on a ring at 120 degrees; a square hub; the pointer: a notch cut in
    the rim over the top chamber, the one that fires."""
    drum = fill(ngon(12, 54, 64, 66, rot=15))
    hub = fill(ngon(4, 10, 64, 66, rot=45))
    chambers = None
    for a in (-90, 30, 150):
        import math
        x, y = 64 + 29 * math.cos(math.radians(a)), 66 + 29 * math.sin(math.radians(a))
        c = disc(x, y, 15)
        chambers = c if chambers is None else chambers | c
    notch = fill([(56, 6), (72, 6), (64, 18)])
    return drum & ~chambers & ~hub & ~notch


def charged_cannon():
    """The round: a solid body with a two-step ogive nose and a driving band standing proud near its base (a shell,
    not a rocket: no fins), turned 45 degrees up and to the right; the muzzle blast it leaves behind its base."""
    H = 28
    body = [(0, -H / 2), (6, -H / 2), (6, -H / 2 - 4), (16, -H / 2 - 4), (16, -H / 2), (46, -H / 2), (62, -H / 2 + 7),
            (76, 0), (62, H / 2 - 7), (46, H / 2), (16, H / 2), (16, H / 2 + 4), (6, H / 2 + 4), (6, H / 2), (0, H / 2)]

    def place(pts):
        moved = [(x + 46, y + 64) for x, y in pts]
        return [(x + 8, y - 8) for x, y in rotate(moved, -45, 64, 64)]
    shell = fill(place(body))
    blast = burst(42, 86, 30, spikes=9, inner=0.5, rot=10)
    gap = _dilate(shell, 6)
    return shell | (blast & ~gap)


def rush():
    """The hull side-on, leaning nose-down into the charge (rolled 12 degrees), driven forward: two chamfered exhaust
    bars trailing straight back from its two rows of haunch engines, leaned with it (two at most: the fleet's motion
    mark). The rows are measured off the UNROLLED mesh's rear edge and the bars turned with the hull about the same
    centre (canvas.rotate and the silhouette's roll share one convention), so each bar sits on its engine row."""
    lean, box, cx, cy = 12, 80, 80, 62
    flat = mesh_silhouette.silhouette(FBX, pitch=-90, yaw=0, box=box, cx=cx, cy=cy, dilate_px=1.4)
    hull = mesh_silhouette.silhouette(FBX, pitch=-90, yaw=0, box=box, cx=cx, cy=cy, dilate_px=1.4, roll=lean)
    from canvas import GX, GY
    x_rear = float(GX[flat].min())
    ys = np.unique(np.round(GY[flat & (GX < x_rear + 3)]).astype(int))
    rows = sorted(np.split(ys, np.where(np.diff(ys) > 2)[0] + 1), key=len, reverse=True)[:2]
    bars = None
    for r in rows:
        yc, w = float(r.mean()), 11.0
        x1 = x_rear - 8
        x0 = 9 + w * 0.6                     # the bar's tail stays inside the optical box
        pts = [(x0, yc - w / 2), (x1, yc - w / 2), (x1 - w * 0.6, yc + w / 2), (x0 - w * 0.6, yc + w / 2)]
        bar = fill(rotate(pts, lean, cx, cy))
        bars = bar if bars is None else bars | bar
    return hull | (bars & ~_dilate(hull, 6))


ICONS = [
    dict(name="Grizzly_DigIn", element="Charge", act="Dig In", draw=dig_in, before=PH + "SmashIcon-PLACEHOLDER.png",
         exempt={"gap": "the hull's real mesh: the pods, ears and claw blades float across even gaps by design "
                        "(vessel-model SKILL 0.5), as on the Squirrel's joust"}),
    dict(name="Grizzly_WeaponSystems", element="Mass", act="Weapon Systems", draw=weapon_systems,
         before=PH + "ZapIcon-PLACEHOLDER.png"),
    dict(name="Grizzly_ChargedCannon", element="Space", act="Charged Cannon", draw=charged_cannon,
         before=PH + "CannonIcon-PLACEHOLDER.png"),
    dict(name="Grizzly_Rush", element="Time", act="Rush", draw=rush, before=PH + "BoostIcon-PLACEHOLDER.png",
         exempt={"gap": "the hull's real mesh: the pods, ears and claw blades float across even gaps by design "
                        "(vessel-model SKILL 0.5)"}),
]

REBIND = [
    dict(file=PREFAB, old="377abf4d2118f4f488b8bff68e4f713c", icon="Grizzly_DigIn", expect=1),
    dict(file=PREFAB, old="a59bb0ac607ec7747838b0af208c6568", icon="Grizzly_WeaponSystems", expect=1),
    dict(file=PREFAB, old="9414cd56ce4e0404fb8693afb41a9729", icon="Grizzly_ChargedCannon", expect=1),
    dict(file=PREFAB, old="138fe9e88ccdae140ab320fd38ff30fe", icon="Grizzly_Rush", expect=1),
]
