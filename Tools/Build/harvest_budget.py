#!/usr/bin/env python3
"""
The Field - Harvest's arena (GameModes.Harvest = 88, the Thresher's SPACE game) - as a MIRROR of
SpawnableHarvest.cs, not an estimate. author_harvest_assets.py imports it for the census, the laid volume and the
PhaseThresholds, and the stills are drawn from it, so the picture, the numbers and the build cannot drift apart.

  * every draw is Hash01 of an integer key (jackstraw_budget's port, reused), never the shared System.Random;
  * every per-unit count is a field or a const;
  * the only count-affecting rejection is deterministic geometry: a sheaf whose reach enters the centre clearing
    or comes closer to another than the spacing re-draws, at most SHEAF_TRIES times, then is skipped.

The C# computes in float32 and this in float64, so a draw within a few millionths of a unit of a rejection boundary
could resolve differently. The generator asserts every accepted sheaf clears every boundary by MIN_MARGIN.

Run it to print the ladder:   python3 Tools/Build/harvest_budget.py
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from jackstraw_budget import hash01, i32, unit_from, in_ball, fibonacci, add, cross, norm, dot  # noqa: E402

# ── Constants: must match SpawnableHarvest.cs (the generator greps for each) ──
BLOCK = 6.0
SEGMENT = 20.0
SECTION = 3.0
STALK_AIR = 20.0
SHEAF_TRIES = 48
FLUSH = 0.995
SHEAF_KEY_BASE = 3_000_000
DEFAULT_SEED = 8888

MIN_MARGIN = 1e-3

DOMAIN_IDS = {"jade": 1, "ruby": 2, "blue": 3, "gold": 4}
PLAYABLE = ["jade", "ruby", "gold"]


def key(seed, slot, attempt, draw):
    return i32(seed * 7919 + SHEAF_KEY_BASE + (slot * SHEAF_TRIES + attempt) * 4 + draw)


def domain_for(pattern, slot):
    if pattern == 1:
        return PLAYABLE[(slot // 4) % 3] if slot % 4 == 0 else "blue"
    if pattern == 2:
        return "blue" if slot % 4 == 3 else PLAYABLE[slot % 4]
    return "blue"


def stalk_root(p):
    return p["coreRadius"] + BLOCK


def reach(p):
    return stalk_root(p) + p["stalkPrisms"] * SEGMENT


def spacing(p):
    return max(2.0 * reach(p) + STALK_AIR, 2.0 * p["coreRadius"] + p["coreGap"])


def place(p):
    """The sheaves' centres, axes and domains, and the least margin past any rejection boundary."""
    seed = p["seed"] or DEFAULT_SEED
    rch, gap = reach(p), spacing(p)
    min_centre = p["centreClearingRadius"] + rch
    sheaves, margins = [], []
    for slot in range(p["sheafCount"]):
        for t in range(SHEAF_TRIES):
            c = in_ball(key(seed, slot, t, 0), p["fieldRadius"] - rch)
            m = math.sqrt(dot(c, c)) - min_centre
            if m < 0:
                continue
            ok = True
            for s in sheaves:
                d = math.sqrt(dot((c[0] - s["c"][0], c[1] - s["c"][1], c[2] - s["c"][2]),
                                  (c[0] - s["c"][0], c[1] - s["c"][1], c[2] - s["c"][2]))) - gap
                if d < 0:
                    ok = False
                    break
                m = min(m, d)
            if not ok:
                continue
            margins.append(m)
            sheaves.append(dict(slot=slot, c=c, axis=unit_from(key(seed, slot, t, 1)),
                                domain=domain_for(p["colourPattern"], slot)))
            break
    return sheaves, (min(margins) if margins else float("inf"))


def frame(axis):
    helper = (0.0, 0.0, 1.0) if abs(axis[2]) < 0.9 else (1.0, 0.0, 0.0)
    y = norm(cross(axis, helper))
    z = cross(axis, y)
    return axis, y, z


def in_frame(f, x, y, z):
    return (x[0] * f[0] + y[0] * f[1] + z[0] * f[2],
            x[1] * f[0] + y[1] * f[1] + z[1] * f[2],
            x[2] * f[0] + y[2] * f[1] + z[2] * f[2])


def min_spacing(n, radius):
    """Least distance between two of n points on a Fibonacci sphere of this radius (for the overlap guards)."""
    pts = [fibonacci(k, n) for k in range(n)]
    best = float("inf")
    for i in range(n):
        for j in range(i + 1, n):
            d = (pts[i][0] - pts[j][0], pts[i][1] - pts[j][1], pts[i][2] - pts[j][2])
            best = min(best, math.sqrt(dot(d, d)) * radius)
    return best


def build(p):
    """Lay the field for one prefab's fields. Returns pieces (for drawing), the sheaves and the census.

    p: seed, fieldRadius, centreClearingRadius, sheafCount, coreGap, coreRadius, coreBlocks, shieldedCores,
       stalkCount, stalkPrisms, dangerStalkEveryNth, colourPattern.
    """
    sheaves, margin = place(p)
    prisms = []   # (centre, axis, size, kind, domain, part)
    root = stalk_root(p)
    block = (BLOCK, BLOCK, BLOCK)
    stalk = (SECTION, SECTION, SEGMENT * FLUSH)
    core_kind = "shielded" if p["shieldedCores"] else "plain"
    n_danger = p["dangerStalkEveryNth"]
    for s in sheaves:
        x, y, z = frame(s["axis"])
        for k in range(p["coreBlocks"]):
            d = in_frame(fibonacci(k, p["coreBlocks"]), x, y, z)
            prisms.append((add(s["c"], d, p["coreRadius"]), d, block, core_kind, s["domain"], "core"))
        for k in range(p["stalkCount"]):
            d = in_frame(fibonacci(k, p["stalkCount"]), x, y, z)
            kind = "danger" if n_danger > 0 and k % n_danger == n_danger - 1 else "plain"
            for j in range(p["stalkPrisms"]):
                prisms.append((add(s["c"], d, root + SEGMENT * (j + 0.5)), d, stalk, kind, s["domain"], "stalk"))

    per_core = p["coreBlocks"]
    per_sheaf = p["coreBlocks"] + p["stalkCount"] * p["stalkPrisms"]
    by_domain = {k: sum(1 for s in sheaves if s["domain"] == k) for k in DOMAIN_IDS}
    census = {
        "prisms": len(prisms),
        "core": sum(1 for q in prisms if q[5] == "core"),
        "stalk": sum(1 for q in prisms if q[5] == "stalk"),
        "plain": sum(1 for q in prisms if q[3] == "plain"),
        "shielded": sum(1 for q in prisms if q[3] == "shielded"),
        "danger": sum(1 for q in prisms if q[3] == "danger"),
        "volume": sum(q[2][0] * q[2][1] * q[2][2] for q in prisms),
        "sheaves": len(sheaves), "skippedSheaves": p["sheafCount"] - len(sheaves),
        "perCore": per_core, "perSheaf": per_sheaf, "byDomain": by_domain,
        "minMargin": margin, "reach": reach(p), "spacing": spacing(p),
        # The overlap guards: neighbouring core cubes, and neighbouring stalk roots.
        "coreNeighbour": min_spacing(p["coreBlocks"], p["coreRadius"]),
        "stalkRootNeighbour": min_spacing(p["stalkCount"], root) if p["stalkCount"] > 1 else float("inf"),
    }
    return {"prisms": prisms, "sheaves": sheaves, "census": census}


# ── The ladder: one SpawnableHarvest prefab per intensity (plan A, picked by Shombith 2026-11-10) ──
# spawnRing is the ServerPlayerVesselInitializer's per-intensity ring floor: just outside the field, so every pilot
# starts in open water facing it. The membrane is the standard 1,200 u cell at every rung (the Boneyard's).
RUNGS = [
    dict(i=1, label="Stubble", seed=8801, fieldRadius=800, centreClearingRadius=200, sheafCount=8, coreGap=180,
         coreRadius=64, coreBlocks=160, shieldedCores=False, stalkCount=40, stalkPrisms=6, dangerStalkEveryNth=0,
         colourPattern=0, spawnRing=880,
         blurb="eight big neutral sheaves, dense with stalks - every sheaf scores for everyone"),
    dict(i=2, label="Sheaves", seed=8802, fieldRadius=880, centreClearingRadius=200, sheafCount=12, coreGap=180,
         coreRadius=58, coreBlocks=140, shieldedCores=True, stalkCount=24, stalkPrisms=6, dangerStalkEveryNth=0,
         colourPattern=1, spawnRing=960,
         blurb="twelve sheaves with shielded cores; one in four wears a domain colour"),
    dict(i=3, label="Stooks", seed=8803, fieldRadius=940, centreClearingRadius=200, sheafCount=16, coreGap=180,
         coreRadius=52, coreBlocks=110, shieldedCores=True, stalkCount=16, stalkPrisms=5, dangerStalkEveryNth=0,
         colourPattern=2, spawnRing=1020,
         blurb="sixteen sheaves, a quarter each Jade, Ruby, Gold and neutral, with sparser stalks"),
    dict(i=4, label="Granary", seed=8804, fieldRadius=1000, centreClearingRadius=200, sheafCount=22, coreGap=180,
         coreRadius=46, coreBlocks=90, shieldedCores=True, stalkCount=12, stalkPrisms=5, dangerStalkEveryNth=4,
         colourPattern=2, spawnRing=1080,
         blurb="twenty-two small sheaves, every fourth stalk danger, one crystal for the lobby"),
]


if __name__ == "__main__":
    for r in RUNGS:
        c = build(r)["census"]
        print(f"I{r['i']} {r['label']:8} prisms {c['prisms']:5}  core {c['core']:5} stalk {c['stalk']:5}"
              f"  shielded {c['shielded']:5} danger {c['danger']:4}  sheaves {c['sheaves']}/{r['sheafCount']}"
              f"  per core {c['perCore']} per sheaf {c['perSheaf']}  by domain {c['byDomain']}"
              f"  margin {c['minMargin']:.3f}  core nbr {c['coreNeighbour']:.1f} root nbr {c['stalkRootNeighbour']:.1f}"
              f"  reach {c['reach']:.0f} spacing {c['spacing']:.0f}")
