#!/usr/bin/env python3
"""
The Field - Harvest's arena (GameModes.Harvest = 88, the Thresher's SPACE game) - as a MIRROR of
SpawnableHarvest.cs, not an estimate. author_harvest_assets.py imports it for the census, the laid volume and the
PhaseThresholds, and the stills are drawn from it, so the picture, the numbers and the build cannot drift apart.

  * every draw is Hash01 of an integer key (jackstraw_budget's port, reused), never the shared System.Random;
  * every per-unit count is a field or a const;
  * the only count-affecting rejections are deterministic geometry: a piece that enters the clearing, leaves the
    field or comes too close to another re-draws, at most TRIES times, then is skipped.

The C# computes in float32 and this in float64, so a draw within a few millionths of a unit of a rejection boundary
could resolve differently. The generator asserts every accepted piece clears its boundaries by MIN_MARGIN, and checks
the census against the shipped C#'s own count in the layout bake (Tools/Build/prism_layout_bakes.json).

Run it to print the ladder:   python3 Tools/Build/harvest_budget.py
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from jackstraw_budget import (hash01, i32, unit_from, in_ball, fibonacci, add, sub, cross, norm, dot,  # noqa: E402
                              segment_segment_sqr)

# ── Constants: must match SpawnableHarvest.cs (the generator greps for each) ──
BLOCK = 6.0
BALE_PITCH = 6.5
SEGMENT = 20.0
SECTION = 3.0
STALK_AIR = 20.0
PIECE_AIR = 24.0
STRAW_GAP = 7.5
TRIES = 48
FLUSH = 0.995
SHEAF_KEY_BASE, CLOUD_KEY_BASE, STRAW_KEY_BASE, BALE_KEY_BASE = 3_000_000, 4_000_000, 5_000_000, 6_000_000
OPEN_KEY_BASE = 7_000_000
DEFAULT_SEED = 8888
SQRT3 = 1.7320508

MIN_MARGIN = 1e-3

DOMAIN_IDS = {"jade": 1, "ruby": 2, "blue": 3, "gold": 4}
PLAYABLE = ["jade", "ruby", "gold"]


def key(seed, family, slot, attempt, draw):
    return i32(seed * 7919 + family + (slot * TRIES + attempt) * 4 + draw)


def domain_for(pattern, slot):
    if pattern == 1:
        return PLAYABLE[(slot // 4) % 3] if slot % 4 == 0 else "blue"
    if pattern == 2:
        return "blue" if slot % 4 == 3 else PLAYABLE[slot % 4]
    return "blue"


def bale_domain_for(pattern, slot):
    return domain_for(2, slot) if pattern == 2 else "blue"


def stalk_root(p):
    return p["coreRadius"] + BLOCK


def reach(p):
    return stalk_root(p) + p["stalkPrisms"] * SEGMENT


def spacing(p):
    return max(2.0 * reach(p) + STALK_AIR, 2.0 * p["coreRadius"] + p["coreGap"])


def bale_reach(p):
    return (0.5 * (p["baleSize"] - 1) * BALE_PITCH + 0.5 * BLOCK) * SQRT3


def mag(v):
    return math.sqrt(dot(v, v))


class Margin:
    """The least distance past any rejection boundary an ACCEPTED piece has (the float32 guard)."""
    def __init__(self):
        self.v = float("inf")

    def see(self, d):
        self.v = min(self.v, d)


def clear_of(p, pt, r, sheaves, clouds, bales, margin=None):
    worst = float("inf")
    for s in sheaves:
        d = mag(sub(s["c"], pt)) - (s["reach"] + r + PIECE_AIR)
        if d < 0:
            return False
        worst = min(worst, d)
    for c in clouds:
        d = mag(sub(c, pt)) - (p["chaffCloudRadius"] + r + PIECE_AIR)
        if d < 0:
            return False
        worst = min(worst, d)
    for b in bales:
        d = mag(sub(b["c"], pt)) - (bale_reach(p) + r + PIECE_AIR)
        if d < 0:
            return False
        worst = min(worst, d)
    if margin is not None:
        margin.see(worst)
    return True


def place_sheaves(p, margin):
    seed = p["seed"] or DEFAULT_SEED
    rch, gap = reach(p), spacing(p)
    min_centre = p["centreClearingRadius"] + rch
    sheaves = []
    for slot in range(p["sheafCount"]):
        for t in range(TRIES):
            c = in_ball(key(seed, SHEAF_KEY_BASE, slot, t, 0), p["fieldRadius"] - rch)
            m = mag(c) - min_centre
            if m < 0:
                continue
            ok = True
            for s in sheaves:
                d = mag(sub(c, s["c"])) - gap
                if d < 0:
                    ok = False
                    break
                m = min(m, d)
            if not ok:
                continue
            margin.see(m)
            sheaves.append(dict(slot=slot, c=c, axis=unit_from(key(seed, SHEAF_KEY_BASE, slot, t, 1)),
                                domain=domain_for(p["colourPattern"], slot), reach=rch))
            break
    return sheaves


def place_clouds(p, sheaves, margin):
    seed = p["seed"] or DEFAULT_SEED
    r = p["chaffCloudRadius"]
    clouds = []
    for slot in range(p["chaffClouds"]):
        for t in range(TRIES):
            c = in_ball(key(seed, CLOUD_KEY_BASE, slot, t, 0), p["fieldRadius"] - r)
            m = mag(c) - (p["centreClearingRadius"] + r + PIECE_AIR)
            if m < 0:
                continue
            mm = Margin()
            if not clear_of(p, c, r, sheaves, clouds, [], mm):
                continue
            margin.see(min(m, mm.v))
            clouds.append(c)
            break
    return clouds


def place_bales(p, sheaves, clouds, margin):
    seed = p["seed"] or DEFAULT_SEED
    r = bale_reach(p)
    bales = []
    for slot in range(p["baleCount"]):
        for t in range(TRIES):
            c = in_ball(key(seed, BALE_KEY_BASE, slot, t, 0), p["fieldRadius"] - r)
            m = mag(c) - (p["centreClearingRadius"] + r + PIECE_AIR)
            if m < 0:
                continue
            mm = Margin()
            if not clear_of(p, c, r, sheaves, clouds, bales, mm):
                continue
            margin.see(min(m, mm.v))
            bales.append(dict(c=c, axis=unit_from(key(seed, BALE_KEY_BASE, slot, t, 1)),
                              domain=bale_domain_for(p["colourPattern"], slot)))
            break
    return bales


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
            best = min(best, mag(sub(pts[i], pts[j])) * radius)
    return best


def build(p):
    """Lay the field for one prefab's fields. Returns pieces (for drawing), the sheaves and the census.

    p: seed, fieldRadius, centreClearingRadius, sheafCount, coreGap, coreRadius, coreBlocks, shieldedCores,
       stalkCount, stalkPrisms, wheelStalks, dangerStalkEveryNth, chaffClouds, chaffCloudRadius, strawsPerCloud,
       baleCount, baleSize, colourPattern.
    """
    seed = p["seed"] or DEFAULT_SEED
    margin = Margin()
    sheaves = place_sheaves(p, margin)
    clouds = place_clouds(p, sheaves, margin)
    bales = place_bales(p, sheaves, clouds, margin)

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
            if p["wheelStalks"]:
                a = 2.0 * math.pi * k / p["stalkCount"]
                d = add((y[0] * math.cos(a), y[1] * math.cos(a), y[2] * math.cos(a)), z, math.sin(a))
            else:
                d = in_frame(fibonacci(k, p["stalkCount"]), x, y, z)
            kind = "danger" if n_danger > 0 and k % n_danger == n_danger - 1 else "plain"
            for j in range(p["stalkPrisms"]):
                prisms.append((add(s["c"], d, root + SEGMENT * (j + 0.5)), d, stalk, kind, s["domain"], "stalk"))

    straws = skipped_straws = 0
    g2 = STRAW_GAP * STRAW_GAP
    for ci, c in enumerate(clouds):
        segs = []
        for s_i in range(p["strawsPerCloud"]):
            slot = ci * p["strawsPerCloud"] + s_i
            for t in range(TRIES):
                n_pr = 1 if hash01(key(seed, STRAW_KEY_BASE, slot, t, 2)) < 0.5 else 2
                half = n_pr * SEGMENT * 0.5
                mid = add(c, in_ball(key(seed, STRAW_KEY_BASE, slot, t, 0), p["chaffCloudRadius"] - half))
                axis = unit_from(key(seed, STRAW_KEY_BASE, slot, t, 1))
                a, b = add(mid, axis, -half), add(mid, axis, half)
                worst = float("inf")
                ok = True
                for sa, sb in segs:
                    d2 = segment_segment_sqr(a, b, sa, sb)
                    if d2 < g2:
                        ok = False
                        break
                    worst = min(worst, math.sqrt(d2) - STRAW_GAP)
                if not ok:
                    continue
                margin.see(worst)
                segs.append((a, b))
                for j in range(n_pr):
                    prisms.append((add(a, axis, SEGMENT * (j + 0.5)), axis, stalk, "plain", "blue", "chaff"))
                straws += 1
                break
            else:
                skipped_straws += 1

    o = 0.5 * (p["baleSize"] - 1)
    for b in bales:
        x, y, z = frame(b["axis"])
        for i in range(p["baleSize"]):
            for j in range(p["baleSize"]):
                for k in range(p["baleSize"]):
                    off = add(add((x[0] * (i - o), x[1] * (i - o), x[2] * (i - o)), y, j - o), z, k - o)
                    prisms.append((add(b["c"], off, BALE_PITCH), b["axis"], block, "plain", b["domain"], "bale"))

    per_core = p["coreBlocks"]
    per_sheaf = p["coreBlocks"] + p["stalkCount"] * p["stalkPrisms"]
    per_bale = p["baleSize"] ** 3
    own = {k: 0 for k in DOMAIN_IDS}
    for q in prisms:
        own[q[4]] += 1
    census = {
        "prisms": len(prisms),
        "core": sum(1 for q in prisms if q[5] == "core"),
        "stalk": sum(1 for q in prisms if q[5] == "stalk"),
        "chaff": sum(1 for q in prisms if q[5] == "chaff"),
        "bale": sum(1 for q in prisms if q[5] == "bale"),
        "plain": sum(1 for q in prisms if q[3] == "plain"),
        "shielded": sum(1 for q in prisms if q[3] == "shielded"),
        "danger": sum(1 for q in prisms if q[3] == "danger"),
        "volume": sum(q[2][0] * q[2][1] * q[2][2] for q in prisms),
        "sheaves": len(sheaves), "skippedSheaves": p["sheafCount"] - len(sheaves),
        "clouds": len(clouds), "skippedClouds": p["chaffClouds"] - len(clouds),
        "bales": len(bales), "skippedBales": p["baleCount"] - len(bales),
        "straws": straws, "skippedStraws": skipped_straws,
        "perCore": per_core, "perSheaf": per_sheaf, "perBale": per_bale,
        "prismsByDomain": own,
        "byDomain": {k: sum(1 for s in sheaves if s["domain"] == k) for k in DOMAIN_IDS},
        "minMargin": margin.v, "reach": reach(p), "spacing": spacing(p), "baleReach": bale_reach(p),
        # The overlap guards: neighbouring core cubes, neighbouring stalk roots, bale blocks.
        "coreNeighbour": min_spacing(p["coreBlocks"], p["coreRadius"]),
        "stalkRootNeighbour": (2 * root * math.sin(math.pi / p["stalkCount"]) if p["wheelStalks"]
                               else min_spacing(p["stalkCount"], root)) if p["stalkCount"] > 1 else float("inf"),
    }
    return {"prisms": prisms, "sheaves": sheaves, "clouds": clouds, "bales": bales, "census": census}


# ── The ladder: one SpawnableHarvest prefab per intensity ──
# Plan A (sheaves), picked 2026-11-10; the fill (wheels, chaff, bales) asked for the same day ("its too empty right now").
# spawnRing is the ServerPlayerVesselInitializer's per-intensity ring floor: just outside the field, so every pilot
# starts in open water facing it. The membrane is the standard 1,200 u cell at every rung (the Boneyard's).
RUNGS = [
    dict(i=1, label="Stubble", seed=8801, fieldRadius=820, centreClearingRadius=200, sheafCount=10, coreGap=180,
         coreRadius=60, coreBlocks=150, shieldedCores=False, stalkCount=36, stalkPrisms=6, wheelStalks=True,
         dangerStalkEveryNth=0, chaffClouds=8, chaffCloudRadius=110, strawsPerCloud=24, baleCount=4, baleSize=3,
         colourPattern=0, spawnRing=900,
         blurb="ten big neutral wheel sheaves, drifts of chaff and four bales - everything scores for everyone"),
    dict(i=2, label="Sheaves", seed=8802, fieldRadius=880, centreClearingRadius=200, sheafCount=12, coreGap=180,
         coreRadius=52, coreBlocks=120, shieldedCores=True, stalkCount=30, stalkPrisms=6, wheelStalks=True,
         dangerStalkEveryNth=0, chaffClouds=10, chaffCloudRadius=110, strawsPerCloud=24, baleCount=6, baleSize=3,
         colourPattern=1, spawnRing=960,
         blurb="twelve wheel sheaves with shielded cores, chaff and six neutral bales; one sheaf in four wears a domain colour"),
    dict(i=3, label="Stooks", seed=8803, fieldRadius=940, centreClearingRadius=200, sheafCount=16, coreGap=180,
         coreRadius=46, coreBlocks=100, shieldedCores=True, stalkCount=28, stalkPrisms=5, wheelStalks=True,
         dangerStalkEveryNth=0, chaffClouds=12, chaffCloudRadius=100, strawsPerCloud=22, baleCount=8, baleSize=3,
         colourPattern=2, spawnRing=1020,
         blurb="sixteen wheel sheaves and eight bales, a quarter each Jade, Ruby, Gold and neutral, with Space crystals "
               "for every team in the match"),
    dict(i=4, label="Granary", seed=8804, fieldRadius=1000, centreClearingRadius=200, sheafCount=20, coreGap=180,
         coreRadius=42, coreBlocks=80, shieldedCores=True, stalkCount=24, stalkPrisms=5, wheelStalks=True,
         dangerStalkEveryNth=4, chaffClouds=12, chaffCloudRadius=100, strawsPerCloud=20, baleCount=8, baleSize=3,
         colourPattern=2, spawnRing=1080,
         blurb="twenty small wheel sheaves with danger stalks, chaff and bales, and live elemental creatures that "
               "graze the field and hunt each other"),
]


if __name__ == "__main__":
    for r in RUNGS:
        c = build(r)["census"]
        print(f"I{r['i']} {r['label']:8} prisms {c['prisms']:5}  core {c['core']:4} stalk {c['stalk']:4} chaff {c['chaff']:4}"
              f" bale {c['bale']:3}  sheaves {c['sheaves']}/{r['sheafCount']} clouds {c['clouds']}/{r['chaffClouds']}"
              f" bales {c['bales']}/{r['baleCount']} straws {c['straws']} (skip {c['skippedStraws']})"
              f"  by domain {c['prismsByDomain']}  margin {c['minMargin']:.3f}"
              f"  core nbr {c['coreNeighbour']:.1f} root nbr {c['stalkRootNeighbour']:.1f}")
