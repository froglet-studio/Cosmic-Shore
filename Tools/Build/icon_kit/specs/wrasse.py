"""The Wrasse's ability-row icons (Resources/ElementalAbilityMaps/Wrasse.asset, Crew/KITE_NAVIGATOR.md), its own since
2026-11-13.

The Wrasse (the Grouper hull and its navigator's kite) flew the Sparrow's HUD, so its cards drew the Sparrow's parts.
Every Wrasse ability is a LOOP the kite closes round motes, so every card is the kite at the knot of its lasso, with what
the loop makes inside it. The level-5 upgrade is a second sprite on the same card (VesselHUDView.upgradedSprite).

  Charge  Kite Ram          the kite striking a rival hull, petals knocked loose. Charge 5 CRYSTAL STEAL: the petals
                            fly back to the kite.
  Mass    Mass Cluster      the lasso round a cluster of prisms. Mass 5 SHIELDED CLUSTERS: a shell round the cluster.
  Space   Space Blast       the lasso round a blast. Space 5 SHIELD BREAKER: the blast splitting a shielded prism.
  Time    Saved Boosts      the lasso round banked boost chevrons. Time 5 TAILWIND: each chevron stretched long.

The kite is WrasseKite.fbx from above. No option round (Garrett 2026-11-07: build the recommended option with full
polish; he reviews the roster at the end).
"""
import math

import mesh_silhouette
import motifs
import toy_portraits
from canvas import fill, ngon, ring, rotate

HULL = "Wrasse"
ICON_DIR = "Assets/_Graphics/Icons/AbilityIcons/Wrasse/"
FBX = "Assets/_Models/Vessel Models/WrasseKite.fbx"

LOOP_C = (64.0, 72.0)   # the lasso's centre
LOOP_R = 44.0
KITE_BOX = 60.0


def _clear_of(mask, *others, px=3.0):
    keep = mask.copy()
    for o in others:
        keep &= ~toy_portraits.grown(o, px)
    return keep


def kite(cx, cy, box=KITE_BOX, heading=0.0):
    """The kite seen side on (its fan tail, body and sensor needle read best that way), nose along `heading` (deg
    clockwise, 0 = right); thickened so the slender craft holds at 40 px."""
    return mesh_silhouette.silhouette(FBX, pitch=90.0, yaw=0.0, roll=heading, box=box, cx=cx, cy=cy, dilate_px=2.6,
                                      close_px=2.0)


def lasso(w=8.0):
    """The loop the kite closes: a ring, and the kite on top of it flying on round (to the right)."""
    k = kite(LOOP_C[0], LOOP_C[1] - LOOP_R - 2, KITE_BOX)
    loop = ring(LOOP_C[0], LOOP_C[1], LOOP_R, w=w)
    return k | _clear_of(loop, k, px=3.5), k


# ---- Charge: Kite Ram / Crystal Steal --------------------------------------------------------------------------
def _petal(cx, cy, s, rot=0.0):
    return fill(rotate([(cx, cy - s), (cx + s * 0.55, cy), (cx, cy + s), (cx - s * 0.55, cy)], rot, cx, cy))


def kite_ram():
    k = kite(44, 44, 74, heading=38)
    rival = motifs.hull(96, 94, 26, heading=200)
    petals = _petal(104, 54, 13, 20) | _petal(70, 108, 13, -30)
    return k | rival | _clear_of(petals, rival, k, px=3)


def crystal_steal():
    k = kite(42, 42, 80, heading=38)
    rival = motifs.hull(100, 100, 22, heading=200)
    p1, p2 = _petal(100, 50, 10, 20), _petal(56, 100, 10, -30)
    a1 = motifs.arrowhead((80.0, 54.0), (-1.0, 0.25), 15.0)
    a2 = motifs.arrowhead((54.0, 80.0), (-0.25, -1.0), 15.0)
    return k | rival | p1 | p2 | _clear_of(a1 | a2, k, px=3)


# ---- Mass: Mass Cluster / Shielded Clusters ----------------------------------------------------------------------
def _cluster(cx, cy, s=13.0, spread=1.25):
    d = s * spread
    return motifs.prism(cx - d, cy + d * 0.6, s) | motifs.prism(cx + d, cy + d * 0.6, s) | motifs.prism(cx, cy - d * 0.75, s)


def mass_cluster():
    l, k = lasso()
    return l | _cluster(LOOP_C[0], LOOP_C[1] + 2, 15.0)


def shielded_clusters():
    l, k = lasso()
    c = (LOOP_C[0], LOOP_C[1] + 2)
    shell = fill(ngon(6, 32.0, c[0], c[1], rot=30.0)) & ~fill(ngon(6, 25.0, c[0], c[1], rot=30.0))
    return l | _cluster(c[0], c[1] + 1, 10.0, 1.3) | shell


# ---- Space: Space Blast / Shield Breaker -------------------------------------------------------------------------
def space_blast():
    l, k = lasso()
    return l | motifs.burst(LOOP_C[0], LOOP_C[1], 30.0, spikes=8, inner=0.5)


def shield_breaker():
    """A shielded prism split down the middle by the blast, its halves thrown apart."""
    l, k = lasso()
    cx, cy = LOOP_C
    sz, w = 28.0, 28.0 * 0.8
    halves = (fill([(cx - 9, cy - sz + 6), (cx - 9, cy + sz + 6), (cx - 9 - w, cy + 6)]) |
              fill([(cx + 9, cy - sz - 6), (cx + 9, cy + sz - 6), (cx + 9 + w, cy - 6)]))
    crack = fill([(cx - 3, cy - 30), (cx + 3, cy - 30), (cx + 6, cy - 8), (cx - 2, cy + 2), (cx + 4, cy + 30), (cx - 4, cy + 30),
                  (cx - 8, cy + 2), (cx, cy - 8)])
    return l | (halves & ~toy_portraits.grown(crack, 1.5))


# ---- Time: Saved Boosts / Tailwind -------------------------------------------------------------------------------
def _chevron(x, y, w=16.0, h=30.0, t=9.0):
    """A boost: one filled chevron pointing right, its tip at x + w / 2."""
    return fill([(x - w / 2, y - h / 2), (x - w / 2 + t, y - h / 2), (x + w / 2, y), (x - w / 2 + t, y + h / 2),
                 (x - w / 2, y + h / 2), (x + w / 2 - t, y)])


def saved_boosts():
    l, k = lasso()
    cy = LOOP_C[1]
    return l | _chevron(44, cy, 18, 34) | _chevron(64, cy, 18, 34) | _chevron(84, cy, 18, 34)


def tailwind():
    """One boost drawn long: a chevron at the head of a long bar."""
    l, k = lasso()
    cy = LOOP_C[1]
    head = _chevron(86, cy, 22, 40, 11)
    bar = fill([(30, cy - 6), (78, cy - 6), (84, cy), (78, cy + 6), (30, cy + 6)])
    return l | head | _clear_of(bar, head, px=3)


ICONS = [
    dict(name="Wrasse_KiteRam", element="Charge", act="Kite Ram", draw=kite_ram),
    dict(name="Wrasse_CrystalSteal", element="Charge", act="Charge 5 Crystal Steal", draw=crystal_steal),
    dict(name="Wrasse_MassCluster", element="Mass", act="Mass Cluster", draw=mass_cluster,
         exempt={"components": "a cluster of separate prisms by design"}),
    dict(name="Wrasse_ShieldedClusters", element="Mass", act="Mass 5 Shielded Clusters", draw=shielded_clusters,
         exempt={"components": "a cluster of separate prisms inside its shell by design"}),
    dict(name="Wrasse_SpaceBlast", element="Space", act="Space Blast", draw=space_blast),
    dict(name="Wrasse_ShieldBreaker", element="Space", act="Space 5 Shield Breaker", draw=shield_breaker),
    dict(name="Wrasse_SavedBoosts", element="Time", act="Saved Boosts", draw=saved_boosts,
         exempt={"components": "three banked boosts, one chevron each, by design"}),
    dict(name="Wrasse_Tailwind", element="Time", act="Time 5 Tailwind", draw=tailwind),
]
