"""The Angler's ability-row icons (Resources/ElementalAbilityMaps/Angler.asset, R_VesselActions/ANGLER.md).

  Charge  Erase / Lock-on     LT, held: a narrow beam from the nose unpaints prisms. Each time it comes onto a rival vessel
                              it LOCKS and SPENDS one creature: the school's creature nearest that rival leaves the school
                              (the count drops by one) and hunts it until it dies, each bite a Strike that takes petals.
                              Every hunted hull wears a ring on the pilot's screen. Plate: the share of the school hunting
                              (3 hunters = full), lime while LT erases. No level-5 upgrade.
  Mass    Lure, Tame & Grow   A, held: the esca lights, the hull stops, wild herbivores in the light tame and join the school
                              (the count on this card); plants in the light grow 3x. L5 Steal Fauna.
  Space   Paint               RT, held: a beam paints prisms lit; half the school balls up ahead; let go and that half eats the
                              newest lit prisms. L5 Paint Shields.
  Time    Dash to the School  B: the hull dashes 75% of the way to one group's centre in 0.35 s; Time speeds the fauna x1 -> x2.

Before (2026-10-31): the round-1 PIL set (author_angler_icons.py), round caps - Command (a hull, a beam, a reticle, two
fish), Leash (a mast and a sun over a plant), Lure (a beam cone of fish and prisms), Tame (chevrons to three fish).
The audit (ABILITY_ICON_SKILL_PLAN.md §2) has Command as "does not read". One card at a time with Garrett, options in
the hull's whole lockup, the Charge card first (the worst).

Real geometry:
  the hull     Angler.fbx (the mesh's nose is +Y as _polys reads it) from the SIDE (pitch -90, yaw 0: nose RIGHT, mast up) - the
               anglerfish read: the broad bow, the jaw, the mast arching forward to the esca bulb (ANGLER.md §9).
  a creature   the QuadFish (QuadFish.prefab, the school's commonest; omniFauna and the "QuadFish don't always listen"
               rounds): a SpaceCrystal core (scale 2) and four HealthBlock prisms 0.186 x 0.8 x 0.2 at (+-0.908, +-0.738, 0),
               each turned 51.3 deg about x - drawn as its codex portrait reads it, the core with four fins in an X. (From
               the side the pairs overlap into a ball with two loose fins, which read as an S on the first draft.)
  the rival    any vessel: the Serpent card's plain dart, in danger.
  the lock     the ring every hunted hull wears on the pilot's screen (§1e).
"""
import math

import mesh_silhouette
from canvas import N, disc, empty, fill, ring, stroke

import numpy as np
from PIL import Image

HULL = "Angler"
ICON_DIR = "Assets/_Graphics/Icons/AbilityIcons/Angler/"
FBX = "Assets/_Models/Vessel Models/Angler.fbx"
VIEW_SIDE = dict(pitch=-90, yaw=0, dilate_px=1.0)   # -90: upright (90 draws it keel up)


def _png_mask(rel):
    import os
    root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))
    a = Image.open(os.path.join(root, rel)).convert("RGBA").split()[3].resize((N, N), Image.LANCZOS)
    return np.asarray(a) > 127


def shipped(name):
    return lambda: _png_mask(ICON_DIR + f"Angler_{name}.png")


def angler(box, cx, cy):
    return mesh_silhouette.silhouette(FBX, box=box, cx=cx, cy=cy, **VIEW_SIDE)


def _clear_of(mask, *others, px=2.5):
    import toy_portraits
    keep = mask.copy()
    for o in others:
        keep &= ~toy_portraits.grown(o, px)
    return keep


ICONS = []
# The row as the HUD draws it today: four white sprites, each over its plate gauge (AnglerHUDView: one gauge per card).
LOCKUP = dict(
    Charge=dict(parts=dict(i=shipped("Command")), state=[("i", "white")]),
    Mass=dict(parts=dict(i=shipped("Leash")), state=[("i", "white")]),
    Space=dict(parts=dict(i=shipped("Lure")), state=[("i", "white")]),
    Time=dict(parts=dict(i=shipped("Tame")), state=[("i", "white")], gauge=1.0),
)
CARDS = []
REBIND = []


# ---- the QuadFish from the side, nose right -------------------------------------------------------------------------
QF_CORE_R = 0.5                 # the SpaceCrystal core, prefab units (codex render: the ball about as wide as a fin is long)
QF_FIN = (0.8, 0.2)             # HealthBlock length (local y) and thickness (local z), the prefab's scale
QF_FIN_Y = 0.738                # each fin's centre above / below the core
QF_TILT = math.degrees(2 * math.asin(0.43271863))     # 51.3 deg about x: both fins swept back


def quadfish(cx, cy, s, flip=False):
    """The QuadFish as its codex portrait reads (fauna_quadfish.png): the crystal core and its four HealthBlock fins in
    an X round it, at the prefab's corner offsets (+-0.908, +-0.738), each fin laid along its corner's diagonal (the
    portrait's three-quarter look; a true front view stands them upright and reads as an H). s px per prefab unit;
    the fins are drawn 0.3 thick (the prefab's 0.186, thickened for the 60 px card)."""
    out = disc(cx, cy, QF_CORE_R * s)
    L, W = QF_FIN[0] * s / 2, 0.3 * s / 2
    for sx in (1, -1):
        for sy in (1, -1):
            fx, fy = cx + sx * 0.908 * s, cy + sy * QF_FIN_Y * s
            n = math.hypot(0.908, QF_FIN_Y)
            ux, uy = sx * 0.908 / n, sy * QF_FIN_Y / n
            vx, vy = -uy, ux
            out |= fill([(fx + ux * L + vx * W, fy + uy * L + vy * W), (fx + ux * L - vx * W, fy + uy * L - vy * W),
                         (fx - ux * L - vx * W, fy - uy * L - vy * W), (fx - ux * L + vx * W, fy - uy * L + vy * W)])
    return out


def dart(x, y, s=1.0):
    """A rival (any vessel): the Serpent card's plain dart, nose right."""
    return fill([(x + 11 * s, y), (x - 8 * s, y - 8 * s), (x - 3 * s, y), (x - 8 * s, y + 8 * s)])


def lock_ring(x, y, r=17.0, w=4.0):
    """The ring a hunted hull wears on the pilot's screen, cut into four arcs so it reads as a lock, not a halo."""
    from canvas import band
    out = empty()
    for a in (0, 90, 180, 270):
        out |= band(x, y, r, a + 14, a + 76, w=w)
    return out


# ---- Charge: Erase / Lock-on ----------------------------------------------------------------------------------------
# The act: a lock SPENDS one creature, which leaves the school and hunts the rival. Who: your school (domain); what it
# acts on: a rival (danger), locked (the ring); what changes: one creature is out hunting (white, the act), the plate
# counts the hunters. The act runs left to right, the hunter between the school and its prey.
RIVAL = (102.0, 64.0)


def rival():
    return dart(*RIVAL, s=1.9)


def ring_at_rival():
    return lock_ring(RIVAL[0] - 3.0, RIVAL[1], r=24.0, w=5.0)


# A: THE HUNT. The Angler (domain) at the left, its one spent creature (white) out on the course to the locked rival.
A_HULL = dict(box=62.0, cx=32.0, cy=64.0)


def a_hull():
    return angler(**A_HULL)


def a_hunter():
    return quadfish(60.0, 64.0, 13.0)


# B: THE SPEND. The school (two creatures, domain) at the left; the third has broken off (white) toward the locked rival.
def b_school():
    return quadfish(16.0, 32.0, 10.0) | quadfish(16.0, 96.0, 10.0)


def b_hunter():
    return quadfish(52.0, 64.0, 14.0)


def b_wake():
    """The line the spent creature left the school along: two short chamfered dashes behind it."""
    out = empty()
    for x0, x1 in ((22.0, 27.0), (30.0, 34.0)):
        out |= stroke([(x0, 64.0), (x1, 64.0)], w=5.0)
    return out


# C: THE BITE. Close up: the creature (white) at the locked rival's tail, a petal torn off behind (the Strike's price).
def c_hunter():
    return quadfish(34.0, 64.0, 21.0)


def c_rival():
    return dart(96.0, 58.0, s=2.3)


def c_ring():
    return lock_ring(93.0, 58.0, r=28.0, w=6.0)


def c_petal():
    import toy_portraits
    return _clear_of(toy_portraits.petal("Charge", (80.0, 104.0), 135.0, 26.0), c_ring(), px=2.5)


# D: THE ERASER. The Angler's narrow LT beam runs across: a prism it unpainted (blue, whole) under it, the rival at its
# end locked, and the one creature it spent on the way.
D_HULL = dict(box=52.0, cx=27.0, cy=64.0)


def d_hull():
    return angler(**D_HULL)


def d_beam():
    x0 = D_HULL["cx"] + D_HULL["box"] / 2
    return _clear_of(stroke([(x0 - 2, 62.0), (RIVAL[0] - 28.0, 62.0)], w=4.5), d_prism(), d_hunter(), px=2.5)


def d_prism():
    from motifs import prism
    return prism(62.0, 94.0, 15.0)


def d_hunter():
    return quadfish(64.0, 34.0, 9.5)


GAUGE = {"rest": (0.0, None), "LT erasing": (0.0, (150, 235, 41, 255)),
         "1 hunter out": (1 / 3, None), "3 hunters out": (1.0, None)}


def _states(parts_rest, parts_lock):
    return [("rest", parts_rest), ("LT erasing", parts_rest), ("1 hunter out", parts_lock), ("3 hunters out", parts_lock)]


BEFORE = ICON_DIR + "Angler_Command.png"
CARDS += [
    dict(name="lockon_a", group="lockon", element="Charge", before=BEFORE,
         title="A  the hunt: the Angler (domain) side-on, its spent QuadFish (white) out on the course to the locked rival (danger)",
         parts=dict(hull=a_hull, hunter=a_hunter, rival=rival, ring=ring_at_rival),
         states=_states([("hull", "domain"), ("rival", "danger", 0.6)],
                        [("hull", "domain"), ("hunter", "white"), ("rival", "danger"), ("ring", "white")]),
         lockup_state=2, gauge=GAUGE, gauge_marks_style="segments"),
    dict(name="lockon_b", group="lockon", element="Charge", before=BEFORE,
         title="B  the spend: two of your school (domain) behind, the third broken off (white) toward the locked rival (danger)",
         parts=dict(school=b_school, hunter=b_hunter, wake=b_wake, rival=rival, ring=ring_at_rival),
         states=_states([("school", "domain"), ("hunter", "domain"), ("rival", "danger", 0.6)],
                        [("school", "domain"), ("wake", "white", 0.6), ("hunter", "white"), ("rival", "danger"), ("ring", "white")]),
         lockup_state=2, gauge=GAUGE, gauge_marks_style="segments"),
    dict(name="lockon_c", group="lockon", element="Charge", before=BEFORE,
         title="C  the bite: close up, the QuadFish (white) at the locked rival (danger), a Charge petal torn off behind",
         parts=dict(hunter=c_hunter, rival=c_rival, ring=c_ring, petal=c_petal),
         states=_states([("hunter", "domain", 0.6), ("rival", "danger", 0.6)],
                        [("hunter", "white"), ("rival", "danger"), ("ring", "white"), ("petal", "danger")]),
         lockup_state=2, gauge=GAUGE, gauge_marks_style="segments"),
    dict(name="lockon_d", group="lockon", element="Charge", before=BEFORE,
         title="D  the eraser: the Angler's LT beam across an unpainted prism (blue) to the locked rival; the creature it spent",
         parts=dict(hull=d_hull, beam=d_beam, prism=d_prism, hunter=d_hunter, rival=rival, ring=ring_at_rival),
         states=_states([("hull", "domain"), ("prism", "blue"), ("rival", "danger", 0.6)],
                        [("hull", "domain"), ("beam", "white"), ("prism", "blue"), ("hunter", "white"), ("rival", "danger"),
                         ("ring", "white")]),
         lockup_state=2, gauge=GAUGE, gauge_marks_style="segments"),
]


# ---- Charge in the game: B, the spend (Garrett picked B, 2026-10-31: "B is fine") -----------------------------------
# Five parts in the Charge slot's container, tinted and shown by AnglerHUDView.SetLockOn: the school (domain) always;
# the third creature in the domain colour while it is still in the school, white once a lock has spent it (any hunter
# out), with its wake (white, 60%) and the lock ring (white); the rival danger, at 60% until it is hunted. The plate
# keeps its gauge (the hunters out, 3 = full; lime while LT erases).
LOCKON_PARTS = [("LockSchool", "Angler_LockSchool", "lockSchoolIcon", b_school, "the two creatures left in the school (domain)"),
                ("LockWake", "Angler_LockWake", "lockWakeIcon", b_wake, "the spent creature's wake (white, 60%; hunters out)"),
                ("LockHunter", "Angler_LockHunter", "lockHunterIcon", b_hunter, "the third creature (domain; white once spent)"),
                ("LockRival", "Angler_LockRival", "lockRivalIcon", rival, "the rival (danger; 60% until it is hunted)"),
                ("LockRing", "Angler_LockRing", "lockRingIcon", ring_at_rival, "the lock ring a hunted hull wears (white; hunters out)")]
PART_EXEMPT = {"at40": "a part: the HUD draws the card whole", "components": "a part", "gap": "a part",
               "coverage": "a part", "box_fill": "a part", "in_box": "a part", "thin": "a part", "specks": "a part"}
ICONS += [dict(name=i, element="Charge", act="Erase / Lock-on: " + act, draw=draw, subdir="Parts", family=False,
               exempt=PART_EXEMPT) for _n, i, _f, draw, act in LOCKON_PARTS]
for c in CARDS:
    if c["name"] == "lockon_b":
        CARDS.append(dict(c, name="lockon", group=None,
                          title="Erase / Lock-on (Charge, picked B): your school (domain), the third creature spent "
                                "(white) toward the locked rival (danger)"))
        break
CARDS[:] = [c for c in CARDS if c.get("group") != "lockon"]   # the losers' sheets go once the owner picks
LOCKUP["Charge"] = dict(parts=dict(s=b_school, h=b_hunter, r=rival),
                        state=[("s", "domain"), ("h", "domain"), ("r", "danger", 0.6)])

VARIANT = "Assets/_Prefabs/UI Elements/VesselHUD/AnglerHUDVariant.prefab"
VIEW = "5875684227236902712"            # AnglerHUDView on the variant
CHARGE_ICON = "7700000000000001105"     # the Charge slot's icon Image (was Angler_Command)
REBIND += [
    dict(file=VARIANT, pair=dict(container=CHARGE_ICON, view=VIEW, after="colorBlendRate",
                                 children=[(n, i, f) for n, i, f, _d, _a in LOCKON_PARTS])),
]


# ---- Mass: Lure, Tame & Grow -----------------------------------------------------------------------------------------
# The mechanic (AnglerConfig.asset, ANGLER.md 1d): A held lights the esca and stops the hull; the light sits lureAhead 14 u
# ahead of the nose; wild herbivores within lureRadius 70 u turn to it, and one held within tameRadius 22 u for tameTime
# 2.5 s joins the school (the count on this card). Plants in the light grow 3x. Mass 5 STEAL FAUNA: another Angler's
# creatures can be lured and kept. Plate: the furthest-along taming (lime while luring). The hull is 6.5 u long (8.15 x
# the prefab's 0.8), a QuadFish about 5 u across (its 2.03 scale). WHO: the Angler (domain), its esca (white, lit);
# WHAT: a wild creature (blue) drawn in and tamed (domain); at Mass 5 a rival Angler's creature (danger) taken.
# Keep the bottom-right corner light: the HUD writes the school count there (AnglerHUDView.MakeCount).
ESCA_FILE = (0.0, 4.33, 0.92)           # the esca bulb, the mesh's furthest-forward vertex (file frame)


def esca_at(box, cx, cy):
    return mesh_silhouette.project_point(FBX, ESCA_FILE, pitch=VIEW_SIDE["pitch"], yaw=VIEW_SIDE["yaw"], box=box, cx=cx, cy=cy)


# A: TO SCALE. The tame light (r 22 u) as one white disc round the lure point, the true-size Angler (domain) knocked out
# at its edge 14 u behind the centre, a creature in the light, a wild one outside it heading in. 1.8 px per u.
A_PX = 1.8
A_LIGHT = (72.0, 60.0)


def ma_hull_box():
    return dict(box=8.15 * 0.8 * A_PX, cx=A_LIGHT[0] - 14 * A_PX - 3.25 * A_PX, cy=A_LIGHT[1])


def ma_hull():
    return angler(**ma_hull_box())


def ma_fish_in():
    return quadfish(A_LIGHT[0] + 6.0, A_LIGHT[1], 2.5 * A_PX * 1.6)


def ma_fish_out():
    return quadfish(118.0, 100.0, 2.5 * A_PX * 1.6)


def ma_light():
    return _clear_of(disc(*A_LIGHT, 22 * A_PX), ma_hull(), ma_fish_in(), px=2.5)


# B: THE ANGLERFISH, close up (not to scale): the hull side-on (domain), its esca lit (white), one creature (blue,
# then domain when tamed) under the light, nose to the bulb.
B_HULL = dict(box=80.0, cx=40.0, cy=78.0)


def mb_hull():
    return angler(**B_HULL)


def mb_bulb(lit=True):
    x, y = esca_at(**B_HULL)
    return disc(x, y, 7.5 if lit else 4.5)


def mb_glow():
    x, y = esca_at(**B_HULL)
    return _clear_of(ring(x, y, 13.0, w=3.5), mb_hull(), px=2.5)


def mb_fish():
    x, y = esca_at(**B_HULL)
    return quadfish(x + 22.0, y + 2.0, 10.0)


# C: THE LIGHT, close up: the same hull, the light as a white disc hanging from the bulb, the creature inside it knocked
# out in its own colour.
def mc_light():
    x, y = esca_at(**B_HULL)
    return _clear_of(disc(x + 14.0, y + 6.0, 24.0), mb_hull(), mc_fish(), px=2.5)


def mc_fish():
    x, y = esca_at(**B_HULL)
    return quadfish(x + 16.0, y + 8.0, 9.0)


def mc_hull():
    return _clear_of(mb_hull(), disc(*esca_at(**B_HULL), 0.1), px=0)


MASS_GAUGE = {"rest": (0.0, None), "luring": (0.6, (150, 235, 41, 255)), "tamed": (0.0, (150, 235, 41, 255)),
              "Mass 5: steal": (0.5, (150, 235, 41, 255))}
MASS_BEFORE = ICON_DIR + "Angler_Leash.png"
CARDS += [
    dict(name="lure_a", group="lure", element="Mass", before=MASS_BEFORE,
         title="A  to scale: the tame light (r 22 u) as a white disc, the true-size Angler (domain) at its edge, a creature in the light",
         parts=dict(hull=ma_hull, light=ma_light, fin=ma_fish_in, fout=ma_fish_out),
         states=[("rest", [("hull", "domain"), ("fin", "blue"), ("fout", "blue", 0.6)]),
                 ("luring", [("light", "white"), ("hull", "domain"), ("fin", "blue"), ("fout", "blue", 0.6)]),
                 ("tamed", [("light", "white"), ("hull", "domain"), ("fin", "domain"), ("fout", "blue", 0.6)]),
                 ("Mass 5: steal", [("light", "white"), ("hull", "domain"), ("fin", "danger"), ("fout", "danger", 0.6)])],
         lockup_state=1, gauge=MASS_GAUGE, gauge_marks_style="segments"),
    dict(name="lure_b", group="lure", element="Mass", before=MASS_BEFORE,
         title="B  the anglerfish (close up, not to scale): the Angler (domain), its esca lit (white), a creature drawn under the light",
         parts=dict(hull=mb_hull, dark=lambda: mb_bulb(False), bulb=mb_bulb, glow=mb_glow, fish=mb_fish),
         states=[("rest", [("hull", "domain"), ("fish", "blue", 0.6)]),
                 ("luring", [("hull", "domain"), ("glow", "white", 0.6), ("bulb", "white"), ("fish", "blue")]),
                 ("tamed", [("hull", "domain"), ("glow", "white", 0.6), ("bulb", "white"), ("fish", "domain")]),
                 ("Mass 5: steal", [("hull", "domain"), ("glow", "white", 0.6), ("bulb", "white"), ("fish", "danger")])],
         lockup_state=1, gauge=MASS_GAUGE, gauge_marks_style="segments"),
    dict(name="lure_c", group="lure", element="Mass", before=MASS_BEFORE,
         title="C  the light (close up): the Angler (domain), a white pool of light under its esca, the creature inside it",
         parts=dict(hull=mb_hull, light=mc_light, fish=mc_fish),
         states=[("rest", [("hull", "domain"), ("fish", "blue", 0.6)]),
                 ("luring", [("hull", "domain"), ("light", "white"), ("fish", "blue")]),
                 ("tamed", [("hull", "domain"), ("light", "white"), ("fish", "domain")]),
                 ("Mass 5: steal", [("hull", "domain"), ("light", "white"), ("fish", "danger")])],
         lockup_state=1, gauge=MASS_GAUGE, gauge_marks_style="segments"),
]


# ---- Mass in the game: B, the anglerfish (Garrett picked B, 2026-10-31: "B is fine") ---------------------------------
# Four parts in the Mass slot's container, driven by AnglerHUDView.SetLure: the hull (domain) always; the lit bulb and
# its glow (white, 60%) while the lure is held; the creature drawn to the bulb - blue (60% at rest, full in the light),
# danger at Mass 5 (Steal Fauna: another Angler's creature), the domain colour for a beat when one joins the school.
LURE_PARTS = [("LureHull", "Angler_LureHull", "lureHullIcon", mb_hull, "the Angler side-on (domain)"),
              ("LureGlow", "Angler_LureGlow", "lureGlowIcon", mb_glow, "the esca's glow (white, 60%; while luring)"),
              ("LureBulb", "Angler_LureBulb", "lureBulbIcon", mb_bulb, "the lit esca bulb (white; while luring)"),
              ("LureFish", "Angler_LureFish", "lureFishIcon", mb_fish,
               "the creature drawn in (blue; danger at Mass 5; domain as it joins)")]
ICONS += [dict(name=i, element="Mass", act="Lure, Tame & Grow: " + act, draw=draw, subdir="Parts", family=False,
               exempt=PART_EXEMPT) for _n, i, _f, draw, act in LURE_PARTS]
for c in CARDS:
    if c["name"] == "lure_b":
        CARDS.append(dict(c, name="lure", group=None,
                          title="Lure, Tame & Grow (Mass, picked B): the Angler (domain), its esca lit (white), a creature "
                                "drawn to the bulb (blue; danger at Mass 5; yours as it joins)"))
        break
CARDS[:] = [c for c in CARDS if c.get("group") != "lure"]
LOCKUP["Mass"] = dict(parts=dict(h=mb_hull, f=mb_fish), state=[("h", "domain"), ("f", "blue", 0.6)])
MASS_ICON = "7700000000000001115"       # the Mass slot's icon Image (was Angler_Leash); the fauna count is its child
REBIND += [
    dict(file=VARIANT, pair=dict(container=MASS_ICON, view=VIEW, after="lockWakeAlpha",
                                 children=[(n, i, f) for n, i, f, _d, _a in LURE_PARTS])),
]


# ---- Space: Paint -----------------------------------------------------------------------------------------------------
# The mechanic (ANGLER.md 1, 1a, 1c, 1j, 1m): RT held, a beam from the nose (3200 u, never narrower than 6 u) paints
# prisms LIT (their colour lifted 75% toward white) and every creature prefers them; half the school balls up ahead of
# the nose. Let go: that half flies at the NEWEST lit prisms and eats them, one creature per prism; the next throw sends
# the other half. Space 5 PAINT SHIELDS: shielded prisms paint too and the squad cracks the shield off. Plate: the
# picked half's share out on an order (lime while RT paints with something painted). WHO: your squad (domain); WHAT: the
# prisms you painted (lit, white) - the untouched blue, the eaten gone (negative space).
from motifs import prism, prism_lit


def _prisms(xs, y, s):
    out = empty()
    for x in xs:
        out |= prism(x, y, s)
    return out


# A: THE BRUSH. The Angler (domain) at the left, its paint beam (white) across a row of prisms: the ones under it lit
# (white), one past its end still blue; the squad balled up ahead of the nose.
SA_HULL = dict(box=58.0, cx=29.0, cy=56.0)
SA_ROW = (72.0, 94.0, 116.0)


def sa_hull():
    return angler(**SA_HULL)


def sa_beam():
    x, y = esca_at(**SA_HULL)
    return _clear_of(stroke([(x + 3, y), (104.0, y)], w=4.5), sa_lit(), sa_ball(), px=2.5)


def sa_lit():
    x, y = esca_at(**SA_HULL)
    return _prisms(SA_ROW[:2], y, 14.0)


def sa_wild():
    x, y = esca_at(**SA_HULL)
    return prism(SA_ROW[2], y, 14.0)


def sa_ball():
    return quadfish(44.0, 96.0, 10.0) | quadfish(70.0, 96.0, 10.0)


# B: THE THROW. Three of your squad (domain) flying right at the lit prisms (white); the first already eaten (its
# prism cut out of the row as negative space), one blue prism left alone.
SB_ROW = (86.0, 112.0)


def sb_squad():
    return quadfish(18.0, 34.0, 11.0) | quadfish(18.0, 94.0, 11.0) | quadfish(50.0, 64.0, 11.0)


def sb_lit():
    return _prisms(SB_ROW, 64.0, 16.0)


def sb_wild():
    return prism(100.0, 104.0, 11.0)


def sb_eat():
    """The front creature at its prism: a bite out of the first lit prism."""
    return _clear_of(prism(SB_ROW[0], 64.0, 16.0), disc(72.0, 64.0, 10.0), px=0.5) | prism(SB_ROW[1], 64.0, 16.0)


# C: THE LIT PRISM. One big painted prism (white, its lit face cut) and the creature that is eating it (domain) at its
# corner; at Space 5 the prism is shielded (shielded colour) and its shield cracks off in two halves.
def sc_prism():
    return _clear_of(prism_lit(76.0, 64.0, 40.0), sc_fish(), px=2.5)


def sc_fish():
    return quadfish(30.0, 64.0, 15.0)


def sc_shield_halves():
    from canvas import shift
    p = prism(76.0, 64.0, 40.0) & ~prism(76.0, 64.0, 32.0)
    top = p & fill([(0, 0), (128, 0), (128, 60), (0, 68)])
    bot = p & ~fill([(0, 0), (128, 0), (128, 60), (0, 68)])
    return _clear_of(shift(top, 3.0, -4.0) | shift(bot, 3.0, 4.0), sc_fish(), px=2.5)


def sc_core():
    return _clear_of(prism_lit(76.0, 64.0, 28.0), sc_fish(), px=2.5)


SPACE_GAUGE = {"rest": (0.0, None), "painting": (0.0, (150, 235, 41, 255)), "thrown": (0.5, None),
               "Space 5: shields": (0.5, None)}
SPACE_BEFORE = ICON_DIR + "Angler_Lure.png"
CARDS += [
    dict(name="paint_a", group="paint", element="Space", before=SPACE_BEFORE,
         title="A  the brush: the Angler's beam (white) across prisms it lit (white), one past it still blue; the squad balled up ahead",
         parts=dict(hull=sa_hull, beam=sa_beam, lit=sa_lit, wild=sa_wild, ball=sa_ball),
         states=[("rest", [("hull", "domain"), ("lit", "blue"), ("wild", "blue"), ("ball", "domain", 0.6)]),
                 ("painting", [("hull", "domain"), ("beam", "white"), ("lit", "white"), ("wild", "blue"), ("ball", "domain")]),
                 ("thrown", [("hull", "domain"), ("lit", "white"), ("wild", "blue"), ("ball", "domain", 0.6)]),
                 ("Space 5: shields", [("hull", "domain"), ("beam", "white"), ("lit", "shielded"), ("wild", "blue"), ("ball", "domain")])],
         lockup_state=1, gauge=SPACE_GAUGE, gauge_marks_style="segments"),
    dict(name="paint_b", group="paint", element="Space", before=SPACE_BEFORE,
         title="B  the throw: your squad (domain) flying at the lit prisms (white), the first bitten into; the unpainted one left blue",
         parts=dict(squad=sb_squad, lit=sb_lit, eat=sb_eat, wild=sb_wild),
         states=[("rest", [("squad", "domain", 0.6), ("lit", "blue"), ("wild", "blue")]),
                 ("painting", [("squad", "domain"), ("lit", "white"), ("wild", "blue")]),
                 ("thrown", [("squad", "domain"), ("eat", "white"), ("lit", "white", 0.0), ("wild", "blue")]),
                 ("Space 5: shields", [("squad", "domain"), ("lit", "shielded"), ("wild", "blue")])],
         lockup_state=2, gauge=SPACE_GAUGE, gauge_marks_style="segments"),
    dict(name="paint_c", group="paint", element="Space", before=SPACE_BEFORE,
         title="C  the lit prism: one painted prism (white) and the creature eating it (domain); at Space 5 its shield cracks off",
         parts=dict(prism=sc_prism, fish=sc_fish, shell=sc_shield_halves, core=sc_core),
         states=[("rest", [("prism", "blue"), ("fish", "domain", 0.6)]),
                 ("painting", [("prism", "white"), ("fish", "domain")]),
                 ("thrown", [("prism", "white"), ("fish", "domain")]),
                 ("Space 5: shields", [("shell", "shielded"), ("core", "white"), ("fish", "domain")])],
         lockup_state=1, gauge=SPACE_GAUGE, gauge_marks_style="segments"),
]
