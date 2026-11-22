#!/usr/bin/env python3
"""The WIDE Regatta course genome and its search (Garrett, 2026-11-21; Plan step: time-arena-course).

    "I want each one i try to be as different as the yggdra cell is to the ourobor cell. no more bland tripple domain
    tracks of supershielded prisms ... the only constraint is just make each ring visible from the sight lines that got
    from one ring to another ... the size of the rings, their position and orientation, every prism in the cell (its
    state orientation, size, position, etc), the size of the cell, its nucleus, its flora, its fauna. everything."

Why the earlier searches (regatta_qd.py, archives under regatta-flywheel/archive*) all came back looking alike: every
genome they could write kept the three braided super-shielded rails (1,812 prisms, one lane per team domain) on the
same 8-ring circuit generator round the nucleus, and a world placement was thinned to 3,000 prisms and carved out of
the line. The search could only decorate one track. This genome (RegattaCourseGenome version 2) removes all three:

    rings   eight free rings drawn from a closed curve through the shell (genes: winding 0/1/2, radius and its swing,
            latitude and longitude waves, the curve's orientation, each ring's mouth and tilt);
    rails   off by default (a gene can lay them, in any rail kind);
    base    a WHOLE world from any cell (Yggdra, Ourobor, Atlantis, Caldera ... or a giant shape) at full density in the
            cell's own frame: its scale, centre, turn, density, the share of prisms kept, every prism's STATE (a weighted
            mix of plain, shielded, super-shielded and danger), colour (native, one domain, or a mix), size, turn
            jitter and a smooth warp field bending the whole layout;
    extras  up to five more spawner placements on the line, each with the same per-prism genes, some across the line;
    sight   the one hard rule: flying a leg into ring i, ring i+1 stands within 60 degrees of the heading and a tube to
            it is carved clear of every prism (RegattaCourseGenome.SightCone / SightClearance, checked here before a
            genome is ever raced).

The genotype lives in the genome's "wide" key (the game ignores it); render() writes the phenotype the game lays.

The search is MAP-Elites over three descriptors that force real difference: the BASE world (one niche per world), the
SUPER-SHIELDED share of the course's prisms (four bins: none, a little, some, mostly) and the course SHAPE (winding x
vertical extent). Each niche keeps its most balanced course, so a niche can never be won by the seed design: the
shipped track (a rails-on, no-base course) does not even belong to most niches.

    python3 Tools/Compute/evaluators/regatta_wide.py --self-test
    python3 Tools/Compute/evaluators/regatta_wide.py sample --n 5 --out /tmp/w           # genomes only
    python3 Tools/Compute/evaluators/regatta_wide.py shard --out DIR [--budget N] [--seed S]
    python3 Tools/Compute/evaluators/regatta_wide.py merge --out DIR SHARD_DIR...
    python3 Tools/Compute/evaluators/regatta_wide.py pick --out DIR [--k 8]               # the most different set
"""
import argparse
import copy
import json
import math
import os
import random
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import regatta_genome as rg  # noqa: E402

RINGS = 8
NUCLEUS = 391.9 * 1.1
MEMBRANE = 1150.0
SIGHT_CONE = 60.0          # RegattaCourseGenome.SightCone
SIGHT_FROM = 0.6           # RegattaCourseGenome.SightFrom
MAX_RING_TILT = 65.0       # RegattaCourseGenome.MaxRingTilt
RING_RADIUS = (40.0, 180.0)

# The balanced Regatta field (night of 2026-11-19 report: 1.076x card, 1.072x L5 on the shipped shuffle).
FIELD = ["Urchin", "Scarab", "Sparrow", "Dolphin", "Squirrel", "Thresher", "BrittleStar", "Rhino", "Swordfish",
         "Goby", "Shrike"]
# Stage A screens a course on the four hulls that spread most on new ground (the rail rider, the fastest free flyer,
# the line-splitter and the skimmer) at one seed and the card start; only a course inside STAGE_A_BAR races the field.
STAGE_A = ["Urchin", "Scarab", "Dolphin", "Squirrel"]
STAGE_A_BAR = 1.8
MIN_WORLD_PRISMS = 2000             # a course must lay at least this many placement prisms (a world, not an empty cell)
SS_BINS = (0.0, 0.05, 0.35, 0.7)     # super-shielded share: none (< 5%), a little, some, mostly


def _catalogue():
    return rg.catalogue()


def world_kinds():
    return sorted(k["id"] for k in _catalogue()["usable"] if k["world"])


# Giant shapes that can stand in for a world (a cell-sized gyroid is a world of its own).
SHAPE_BASES = ["Gyroid", "SchwarzPSurface", "HopfFibration", "Spherene", "TorusKnot", "Helicoid", "CliffordTorus",
               "LSystem", "LinkedRings"]
EXTRA_KINDS = ["Gyroid", "SchwarzPSurface", "HopfFibration", "Spherene", "TorusKnot", "Helicoid", "CliffordTorus",
               "LSystem", "LinkedRings", "FiveRings", "Wall", "Batman", "Kelpwood", "Standoff", "Corona", "Jackstraw",
               "Flatline", "Ribcage", "Skein", "Shatterplane"]


_LIFE = None


def lifeform_cells():
    """(every cell config with a spawn profile, {world kind: its own cell's config}), read from the repo: a course may
    live with any cell's flora and fauna, and a world most often with its own."""
    global _LIFE
    if _LIFE is None:
        import glob
        import re
        root = os.path.join(rg.REPO, "Assets", "_SO_Assets", "Cell Configs")
        guid2prefab = {}
        for m in glob.glob(os.path.join(rg.REPO, "Assets", "_Prefabs", "Spawnables", "*.prefab.meta")):
            g = re.search(r"guid: (\w+)", open(m, encoding="utf-8", errors="ignore").read())
            if g:
                guid2prefab[g.group(1)] = os.path.basename(m)[:-len(".prefab.meta")]
        life, own = [], {}
        for f in sorted(glob.glob(os.path.join(root, "*", "*.asset"))):
            txt = open(f, encoding="utf-8", errors="ignore").read()
            sp = re.search(r"SpawnProfile: \{fileID: \d+, guid: (\w+)", txt)
            rel = os.path.relpath(f, root)[:-len(".asset")].replace(os.sep, "/")
            if not sp or "Regatta" in rel:
                continue
            life.append(rel)
            ep = re.search(r"EnvironmentPrefab: \{fileID: -?\d+, guid: (\w+)", txt)
            pf = guid2prefab.get(ep.group(1)) if ep else None
            if pf and pf.startswith("Spawnable"):
                own.setdefault(pf[len("Spawnable"):].rstrip("0123456789"), rel)
        _LIFE = (life, own)
    return _LIFE


def random_cell(rng, base_kind):
    life, own = lifeform_cells()
    r = rng.random()
    lifeforms = own.get(base_kind) if r < 0.5 and own.get(base_kind) else rng.choice(life) if r < 0.85 else ""
    return {"nucleus": "None" if rng.random() < 0.45 else "Standard",
            "shell": rng.choice([0.0, 0.0, rng.uniform(700.0, 1150.0)]), "lifeforms": lifeforms}


def base_choices():
    # No bare course: a course is a whole world with the loop through it (Garrett 2026-11-21, "a course that doesn't
    # have that at all"); every world kind and shape base, never the empty cell that leaves only rails or extras.
    return world_kinds() + SHAPE_BASES


# ── Vector helpers (plain floats; the game's sight check is in float32, the margin here is a degree) ──────────────

def _add(a, b): return (a[0] + b[0], a[1] + b[1], a[2] + b[2])
def _sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def _mul(a, s): return (a[0] * s, a[1] * s, a[2] * s)
def _dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def _cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
def _len(a): return math.sqrt(_dot(a, a))


def _norm(a, fb=(0.0, 0.0, 1.0)):
    m = _len(a)
    return _mul(a, 1.0 / m) if m > 1e-9 else fb


def _angle(a, b):
    c = _dot(_norm(a), _norm(b))
    return math.degrees(math.acos(max(-1.0, min(1.0, c))))


def _rot(v, yaw, pitch, roll):
    """Unity's Quaternion.Euler(pitch, yaw, roll) * v (Z, then X, then Y)."""
    x, y, z = v
    r = math.radians(roll)
    x, y = x * math.cos(r) - y * math.sin(r), x * math.sin(r) + y * math.cos(r)
    p = math.radians(pitch)
    y, z = y * math.cos(p) - z * math.sin(p), y * math.sin(p) + z * math.cos(p)
    w = math.radians(yaw)
    x, z = x * math.cos(w) + z * math.sin(w), -x * math.sin(w) + z * math.cos(w)
    return (x, y, z)


# ── The genotype ──────────────────────────────────────────────────────────────────────────────────────────────────

def _mix(rng, ss_target=None):
    """Weights of Plain, Shielded, SuperShielded, Danger. ss_target picks the super-shielded share's bin."""
    w = [rng.random() ** 2 for _ in range(4)]
    if ss_target is None:
        ss_target = rng.choice([0, 0, 1, 2, 3])
    lo = SS_BINS[ss_target]
    hi = SS_BINS[ss_target + 1] if ss_target + 1 < len(SS_BINS) else 1.0
    rest = sum(w[i] for i in (0, 1, 3)) or 1.0
    ss = rng.uniform(lo, hi) if ss_target else 0.0
    out = [w[0] / rest * (1 - ss), w[1] / rest * (1 - ss), ss, w[3] / rest * (1 - ss)]
    return [round(x, 4) for x in out]


def random_loop(rng):
    winding = rng.choice([0, 1, 1, 1, 2])
    return {
        "winding": winding,
        "R0": rng.uniform(600.0, 1000.0),
        "rAmp": rng.uniform(0.0, 220.0), "rK": rng.choice([1, 2, 3]), "rPh": rng.uniform(0, 2 * math.pi),
        "lat": [rng.uniform(-55, 55), rng.uniform(-35, 35), rng.uniform(-20, 20)],
        "latPh": [rng.uniform(0, 2 * math.pi) for _ in range(3)],
        "lon": [rng.uniform(-30, 30), rng.uniform(-15, 15)],
        "lonPh": [rng.uniform(0, 2 * math.pi) for _ in range(2)],
        # A winding-0 loop (not round the nucleus) spans this many degrees of the shell.
        "span": rng.uniform(70.0, 150.0), "eight": rng.random() < 0.35,
        "rot": [rng.uniform(0, 360), rng.uniform(-90, 90), rng.uniform(0, 360)],
        "tJit": [rng.uniform(-0.25, 0.25) for _ in range(RINGS)],
    }


def random_base(rng, kind=None, ss=None):
    kind = kind or rng.choice(base_choices())
    if kind == "none":
        return {"kind": "none"}
    k = _catalogue()["by_id"][kind]
    native = max(40.0, float(k["native_radius"]))
    world = k["world"]
    # The racing shell is 431-1150 u from the centre (nucleus to membrane): a world authored round its own cell's
    # centre is scaled so it FILLS that shell (its 90% radius out near the membrane); the part inside the nucleus is
    # carved. Measured 2026-11-21: Yggdra at its native 349 u laid 231 of 39,194 prisms (the rest inside the nucleus).
    scale = rng.uniform(650.0, 1150.0)
    domain = rng.choice(["Native", "Native", "Mix", "Jade", "Ruby", "Gold", "Blue"])
    return {
        "kind": kind, "scale": min(1150.0, scale),
        "at": [rng.uniform(-150, 150), rng.uniform(-150, 150), rng.uniform(-150, 150)] if world else [0.0, 0.0, 0.0],
        "rot": [rng.uniform(0, 360), rng.uniform(0, 360), rng.uniform(0, 360)],
        "density": rng.uniform(0.4, 1.6) if world else 1.0,
        "keep": rng.choice([1.0, 1.0, rng.uniform(0.15, 1.0)]),
        "mix": _mix(rng, ss), "domain": domain, "dmix": [round(rng.random(), 3) for _ in range(4)],
        # Scaling a world up grows its prisms with it (by about scale / native radius); the size gene brings them back
        # down toward the native size or leaves them big.
        "size": min(3.0, max(0.3, native / scale * math.exp(rng.uniform(math.log(0.6), math.log(3.0))))),
        "jitter": rng.choice([0.0, 0.0, rng.uniform(5, 90)]),
        "warp": rng.choice([0.0, 0.0, rng.uniform(10, 180)]), "wl": rng.uniform(150, 1200),
        "seed": rng.randrange(1, 2 ** 31 - 1),
        # Across the line: the world stands ON the racing line (only the ring mouths and the sight tubes stay clear),
        # so it is terrain every pilot meets, not scenery beside a cleared tube. Measured 2026-11-21: with the line
        # tube carved, Dolphin, Scarab and Urchin flew a Yggdra and an Ourobor lap to the same hundredth.
        "cross_line": rng.random() < 0.6,
        "params": rg.random_params(rng, k, 0.6) if k["params"] else {},
    }


def random_extra(rng, kind=None, ss=None):
    kind = kind or rng.choice(EXTRA_KINDS)
    k = _catalogue()["by_id"][kind]
    return {
        "kind": kind, "leg": rng.uniform(0, RINGS), "radius": rng.uniform(0, 260), "bearing": rng.uniform(0, 360),
        "along": rng.uniform(-120, 120),
        "scale": math.exp(rng.uniform(math.log(max(20.0, k["scale_lo"])), math.log(320.0))),
        "rot": [rng.uniform(0, 360), rng.uniform(0, 360), rng.uniform(0, 360)],
        "mix": _mix(rng, ss), "domain": rng.choice(["Native", "Mix", "Jade", "Ruby", "Gold", "Blue"]),
        "dmix": [round(rng.random(), 3) for _ in range(4)],
        "size": math.exp(rng.uniform(math.log(0.5), math.log(2.5))), "jitter": rng.choice([0.0, rng.uniform(5, 60)]),
        "keep": 1.0, "warp": 0.0, "wl": 400.0, "cross_line": rng.random() < 0.4,
        "seed": rng.randrange(1, 2 ** 31 - 1), "params": rg.random_params(rng, k, 0.5) if k["params"] else {},
    }


def random_genes(rng, base=None, ss=None):
    genes = _random_genes(rng, base, ss)
    genes["cell"] = random_cell(rng, genes["base"]["kind"])
    # Without a nucleus the loop may cut through the centre: a smaller radius is a different course, not a shrunk one.
    if genes["cell"]["nucleus"] == "None" and rng.random() < 0.5:
        genes["loop"]["R0"] = rng.uniform(250.0, 700.0)
    return genes


def random_stretch(rng):
    a = rng.uniform(0.0, 0.8)
    return {"leg": rng.randrange(RINGS), "from": round(a, 3), "to": round(min(1.0, a + rng.uniform(0.15, 1.0)), 3),
            "kind": rng.choice([1, 1, 2]), "neutral": rng.random() < 0.25,
            "detour": round(rng.choice([0.0, 0.0, rng.uniform(10.0, 80.0)]), 2)}


def rail_coverage(genes):
    """The share of the lap's legs that carry rail (0 with rails off)."""
    if genes.get("rails") != "partial":
        return 0.0
    cov = [[] for _ in range(RINGS)]
    for st in genes.get("stretches", []):
        cov[st["leg"] % RINGS].append((st["from"], st["to"]))
    tot = 0.0
    for spans in cov:
        end = 0.0
        for a, b in sorted(spans):
            a = max(a, end)
            if b > a:
                tot += b - a
                end = b
    return tot / RINGS


def _random_genes(rng, base=None, ss=None):
    return {
        "loop": random_loop(rng),
        "rings": {"radius": [rng.uniform(*RING_RADIUS) for _ in range(RINGS)],
                  "tilt": [[rng.uniform(-30, 30), rng.uniform(-30, 30)] for _ in range(RINGS)]},
        "base": random_base(rng, base, ss),
        "extras": [random_extra(rng, None, ss) for _ in range(rng.choice([0, 0, 1, 2, 3, 5]))],
        "cell": None,
        # 2026-11-21: every confirmed full-braid ("on") course still looked like the old tracks; "on" is no longer
        # drawn and railKind is kept for old genomes' schema only.
        # Rails are off or PARTIAL (Garrett 2026-11-22: "there is an optimum amount of railing and part of learning the
        # course is to find out what that is"): a few stretches of shielded or plain rail on chosen legs, the rest a gap.
        # Never the full braid that made every old course look alike.
        "rails": "off" if rng.random() < 0.4 else "partial",
        "stretches": [random_stretch(rng) for _ in range(rng.randint(1, 6))],
        "railKind": rng.choice([1, 2, 3]),      # Shielded, Plain, Gap (never all super-shielded)
        "sight": rng.uniform(8.0, 30.0), "line": rng.uniform(14.0, 70.0),
        "crystals": rng.choice([0, 0, 1, 2, 3, 4]), "crystalPhase": rng.random(),
        "seed": rng.randrange(1, 2 ** 31 - 1),
    }


# ── Rendering the genotype into the game's genome ─────────────────────────────────────────────────────────────────

def _loop_point(L, t):
    """The closed curve's point at t in [0, 2 pi), cell-local."""
    lat = sum(a * math.sin((k + 1) * t + ph) for k, (a, ph) in enumerate(zip(L["lat"], L["latPh"])))
    r = L["R0"] + L["rAmp"] * math.sin(L["rK"] * t + L["rPh"])
    if L["winding"] == 0:
        # A loop on one side of the nucleus: a circle (or a figure eight) on the shell round a pole.
        span = math.radians(L["span"]) * 0.5
        x = math.sin(t) * span
        y = (math.sin(2 * t) if L["eight"] else math.cos(t)) * span * 0.75 + math.radians(lat) * 0.3
        d = _norm((x, y, 1.0))
    else:
        lon = L["winding"] * t + sum(math.radians(b) * math.sin((k + 1) * t + ph)
                                     for k, (b, ph) in enumerate(zip(L["lon"], L["lonPh"])))
        la = math.radians(lat)
        d = (math.cos(la) * math.sin(lon), math.sin(la), math.cos(la) * math.cos(lon))
    p = _mul(d, r)
    return _rot(p, *L["rot"])


def shell_of(genes):
    c = genes.get("cell") or {}
    inner = 40.0 if c.get("nucleus") == "None" else NUCLEUS
    outer = min(MEMBRANE, max(650.0, c["shell"])) if c.get("shell") else MEMBRANE
    return inner, outer


def render_rings(genes):
    L = genes["loop"]
    inner, outer = shell_of(genes)
    ts = [2 * math.pi * (i + 0.5 * genes["loop"]["tJit"][i]) / RINGS for i in range(RINGS)]
    pts = [_loop_point(L, t) for t in ts]
    rings = []
    for i in range(RINGS):
        rad = max(RING_RADIUS[0], min(RING_RADIUS[1], genes["rings"]["radius"][i]))
        p = pts[i]
        dist = _len(p)
        lo, hi = inner + rad + 20.0, max(inner + rad + 20.0, outer - rad - 20.0)
        if dist < lo or dist > hi:
            p = _mul(p, max(lo, min(hi, dist)) / max(dist, 1e-6))
        pts[i] = p
    for i in range(RINGS):
        rad = max(RING_RADIUS[0], min(RING_RADIUS[1], genes["rings"]["radius"][i]))
        inbound = _norm(_sub(pts[i], pts[i - 1]))
        outbound = _norm(_sub(pts[(i + 1) % RINGS], pts[i]), inbound)
        axis = _norm(_add(inbound, outbound), inbound)
        side = _norm(_cross(axis, _norm(pts[i])), (1.0, 0.0, 0.0))
        up = _cross(side, axis)
        ta, tb = (math.radians(x) for x in genes["rings"]["tilt"][i])
        axis = _norm(_add(_add(_mul(axis, math.cos(ta) * math.cos(tb)), _mul(side, math.sin(ta))),
                          _mul(up, math.sin(tb) * math.cos(ta))))
        rings.append({"pos": [round(c, 3) for c in pts[i]], "axis": [round(c, 5) for c in axis], "radius": round(rad, 3)})
    return rings


def _v2_fields(src, pd):
    if src.get("domain") == "Mix":
        pd["domain"], pd["domain_mix"] = "Mix", list(src["dmix"])
    else:
        pd["domain"] = src.get("domain", "Native")
    if src.get("mix") is not None:
        pd["prism"], pd["prism_mix"] = "Mix", list(src["mix"])
    if abs(src.get("size", 1.0) - 1.0) > 1e-6:
        pd["prism_size"] = round(src["size"], 4)
    if src.get("jitter", 0) > 0:
        pd["jitter"] = round(src["jitter"], 3)
    if src.get("keep", 1.0) < 1.0:
        pd["keep"] = round(src["keep"], 4)
    if src.get("warp", 0) > 0:
        pd["warp"], pd["warp_wavelength"] = round(src["warp"], 3), round(src["wl"], 3)


def render(genes):
    """The game's genome (RegattaCourseGenome version 2 JSON) for a genotype; the genotype rides along in "wide"."""
    sections = []
    if genes["rails"] == "partial":
        # The whole lap a gap, then each stretch laid over it (a later section wins where they overlap).
        sections.append({"Leg": -1, "From": 0.0, "To": 1.0, "Kind": 3, "Neutral": False, "Detour": 0.0,
                         "DetourBearing": 0.0})
        for st in genes["stretches"]:
            sections.append({"Leg": int(st["leg"]), "From": float(st["from"]), "To": float(st["to"]),
                             "Kind": int(st["kind"]), "Neutral": bool(st["neutral"]), "Detour": float(st["detour"]),
                             "DetourBearing": 0.0})
    elif genes["rails"] == "on":
        sections.append({"Leg": -1, "From": 0.0, "To": 1.0, "Kind": int(genes["railKind"]), "Neutral": False,
                         "Detour": 0.0, "DetourBearing": 0.0})
    g = {"version": 2, "intensity": 1, "seed": int(genes["seed"]),
         "features": {"Sections": sections, "AlternateGates": [],
                      "Crystals": {"PerLap": int(genes["crystals"]), "Phase": float(genes["crystalPhase"])}},
         "line_clearance": round(genes["line"], 3), "placements": [], "rings": render_rings(genes),
         "sight_clearance": round(genes["sight"], 3)}
    if genes["rails"] == "off":
        g["rails_off"] = True
    c = genes.get("cell") or {}
    if c.get("nucleus") == "None" or c.get("shell") or c.get("lifeforms"):
        g["cell"] = {"nucleus": c.get("nucleus", "Standard"), "shell_radius": round(float(c.get("shell") or 0.0), 3),
                     "lifeforms": c.get("lifeforms") or ""}
    b = genes["base"]
    if b["kind"] != "none":
        pd = {"kind": b["kind"], "leg": 0.0, "offset": [0.0, 0.0, 0.0], "scale": round(b["scale"], 3),
              "yaw": round(b["rot"][0], 3), "pitch": round(b["rot"][1], 3), "roll": round(b["rot"][2], 3),
              "seed": int(b["seed"]), "density": round(b["density"], 4), "cross_line": bool(b.get("cross_line")),
              "cross_rail": True,
              "params": dict(b.get("params") or {}), "base": True, "at": [round(x, 3) for x in b["at"]]}
        _v2_fields(b, pd)
        g["placements"].append(pd)
    for e in genes["extras"]:
        pd = {"kind": e["kind"], "leg": round(e["leg"], 4), "offset": [round(e["radius"], 3), round(e["bearing"], 3),
                                                                     round(e["along"], 3)],
              "scale": round(e["scale"], 3), "yaw": round(e["rot"][0], 3), "pitch": round(e["rot"][1], 3),
              "roll": round(e["rot"][2], 3), "seed": int(e["seed"]), "density": 1.0,
              "cross_line": bool(e["cross_line"]), "cross_rail": True, "params": dict(e.get("params") or {})}
        _v2_fields(e, pd)
        g["placements"].append(pd)
    g["wide"] = genes
    return g


# ── The sight line rule and the shell, checked before a course is raced ──────────────────────────────────────────

def _gates(genome):
    return [{"p": tuple(rg.cp.f(x) for x in r["pos"]), "a": tuple(rg.cp.f(x) for x in _norm(tuple(r["axis"]))),
             "r": rg.cp.f(r["radius"])} for r in genome["rings"]]


def _repaired_axes(genome):
    """RegattaCourseGenome.RepairRings' axis rule (within MAX_RING_TILT of the bisector)."""
    rings = genome["rings"]
    out = []
    for i, r in enumerate(rings):
        p = tuple(r["pos"])
        inbound = _norm(_sub(p, tuple(rings[i - 1]["pos"])))
        outbound = _norm(_sub(tuple(rings[(i + 1) % RINGS]["pos"]), p), inbound)
        bis = _norm(_add(inbound, outbound), inbound)
        axis = _norm(tuple(r["axis"]), bis)
        off = _angle(bis, axis)
        if off > MAX_RING_TILT:
            t = MAX_RING_TILT / off
            axis = _norm(_add(_mul(bis, 1 - t), _mul(axis, t)))   # close enough to slerp for a check
        out.append(dict(r, axis=list(axis)))
    return dict(genome, rings=out)


def check(genome):
    """(ok, facts): the sight cone at every ring, and the racing line kept inside the shell (out of the nucleus)."""
    gs = _gates(_repaired_axes(genome))
    worst, bad = 0.0, 0
    for i in range(RINGS):
        point, tangent, _ = rg.spine_frame(gs, (i - 1) % RINGS + SIGHT_FROM)
        to = tuple(float(x) for x in gs[(i + 1) % RINGS]["p"])
        a = _angle(tuple(float(x) for x in tangent), _sub(to, tuple(float(x) for x in point)))
        worst = max(worst, a)
        bad += a > SIGHT_CONE - 1.0
    lo, hi, length = 1e9, 0.0, 0.0
    prev = None
    for s in range(RINGS * 16):
        point, _, _ = rg.spine_frame(gs, s / 16.0)
        p = tuple(float(x) for x in point)
        d = _len(p)
        lo, hi = min(lo, d), max(hi, d)
        if prev is not None:
            length += _len(_sub(p, prev))
        prev = p
    ys = [r["pos"][1] for r in genome["rings"]]
    facts = {"sight_bad": bad, "sight_worst": round(worst, 1), "spine_min": round(lo, 1), "spine_max": round(hi, 1),
             "lap_u": round(length, 0), "vertical_u": round(max(ys) - min(ys), 0)}
    inner, outer = shell_of(genome["wide"]) if "wide" in genome else (NUCLEUS, MEMBRANE)
    ok = bad == 0 and lo > inner + 30.0 and hi < outer - 30.0
    return ok, facts


def random_legal(rng, base=None, ss=None, tries=400):
    for _ in range(tries):
        genes = random_genes(rng, base, ss)
        g = render(genes)
        ok, facts = check(g)
        if ok:
            return g, facts
    raise RuntimeError("no legal course in %d draws" % tries)


# ── Descriptors: what makes two courses different ─────────────────────────────────────────────────────────────────

def ss_share(genes):
    """The expected super-shielded share of the course's PLACED prisms (rails counted when on). A native-state base
    is read from what the world lays by default (most world cells mix shielded and super-shielded: counted at 0.5)."""
    cat = _catalogue()["by_id"]
    total, ss = 0.0, 0.0

    def add(src, n):
        nonlocal total, ss
        mix = src.get("mix")
        total += n
        ss += n * (mix[2] if mix else 0.5)

    b = genes["base"]
    if b["kind"] != "none":
        k = cat[b["kind"]]
        add(b, min(45000.0, k["native_prisms"] * (b["density"] if k["world"] else 1.0) * b.get("keep", 1.0)))
    for e in genes["extras"]:
        add(e, min(3000.0, cat[e["kind"]]["native_prisms"]))
    if genes["rails"] == "on" and genes["railKind"] != 3:
        total += 1812.0
    total += 1812.0 * rail_coverage(genes)      # partial rails are shielded or plain, never super-shielded
    return ss / total if total else 0.0


def descriptor(genes, facts):
    share = ss_share(genes)
    ss_bin = max(i for i, lo in enumerate(SS_BINS) if share >= lo)
    shape = f"w{genes['loop']['winding']}{'e' if genes['loop']['winding'] == 0 and genes['loop']['eight'] else ''}"
    tall = "tall" if facts["vertical_u"] > 900 else "flat"
    nuc = "open" if (genes.get("cell") or {}).get("nucleus") == "None" else "core"
    cov = rail_coverage(genes)
    rails = "r0" if cov <= 0 else "r1" if cov < 0.25 else "r2"
    return f"{genes['base']['kind']}|ss{ss_bin}|{shape}-{tall}-{nuc}|{rails}"


# ── Variation ─────────────────────────────────────────────────────────────────────────────────────────────────────

def _jiggle(rng, v, sd, lo, hi):
    return max(lo, min(hi, v + rng.gauss(0, sd)))


def mutate(genes, rng):
    g = copy.deepcopy(genes)
    if g["rails"] == "on":
        g["rails"] = "off"      # an older full-braid elite's children are railless
    g.setdefault("stretches", [random_stretch(rng) for _ in range(rng.randint(1, 4))])
    if g["base"]["kind"] == "none":
        g["base"] = random_base(rng, rng.choice(base_choices()), None)
    ops = rng.randint(1, 4)
    for _ in range(ops):
        op = rng.choice(["loop", "loop", "ring", "ring", "base", "base", "basekind", "mix", "extra", "rails", "rails", "dials",
                         "cell", "cross"])
        L = g["loop"]
        if op == "loop":
            key = rng.choice(["R0", "rAmp", "lat", "lon", "rot", "tJit", "span", "phase"])
            if key == "R0": L["R0"] = _jiggle(rng, L["R0"], 60, 600, 1000)
            elif key == "rAmp": L["rAmp"] = _jiggle(rng, L["rAmp"], 40, 0, 220)
            elif key == "lat": i = rng.randrange(3); L["lat"][i] = _jiggle(rng, L["lat"][i], 10, -55, 55)
            elif key == "lon": i = rng.randrange(2); L["lon"][i] = _jiggle(rng, L["lon"][i], 6, -30, 30)
            elif key == "rot": i = rng.randrange(3); L["rot"][i] += rng.gauss(0, 20)
            elif key == "tJit": i = rng.randrange(RINGS); L["tJit"][i] = _jiggle(rng, L["tJit"][i], 0.08, -0.25, 0.25)
            elif key == "span": L["span"] = _jiggle(rng, L["span"], 15, 70, 150)
            else: i = rng.randrange(3); L["latPh"][i] += rng.gauss(0, 0.5)
        elif op == "ring":
            i = rng.randrange(RINGS)
            g["rings"]["radius"][i] = _jiggle(rng, g["rings"]["radius"][i], 20, *RING_RADIUS)
            j = rng.randrange(2)
            g["rings"]["tilt"][i][j] = _jiggle(rng, g["rings"]["tilt"][i][j], 10, -40, 40)
        elif op == "base" and g["base"]["kind"] != "none":
            b = g["base"]
            key = rng.choice(["scale", "at", "rot", "density", "keep", "size", "jitter", "warp", "seed"])
            if key == "scale": b["scale"] = _jiggle(rng, b["scale"], 80, 500, 1150)
            elif key == "at": i = rng.randrange(3); b["at"][i] = _jiggle(rng, b["at"][i], 40, -300, 300)
            elif key == "rot": i = rng.randrange(3); b["rot"][i] += rng.gauss(0, 25)
            elif key == "density": b["density"] = _jiggle(rng, b["density"], 0.2, 0.1, 2.0)
            elif key == "keep": b["keep"] = _jiggle(rng, b.get("keep", 1.0), 0.15, 0.05, 1.0)
            elif key == "size": b["size"] = _jiggle(rng, b["size"], 0.2, 0.3, 3.0)
            elif key == "jitter": b["jitter"] = _jiggle(rng, b["jitter"], 15, 0, 180)
            elif key == "warp": b["warp"] = _jiggle(rng, b["warp"], 30, 0, 250)
            else: b["seed"] = rng.randrange(1, 2 ** 31 - 1)
        elif op == "basekind":
            # Keep the state mix's bin, change the world: a jump across niches.
            ss = max(i for i, lo in enumerate(SS_BINS) if ss_share(g) >= lo)
            g["base"] = random_base(rng, None, ss)
            if g.get("cell") and rng.random() < 0.5:
                g["cell"]["lifeforms"] = lifeform_cells()[1].get(g["base"]["kind"], g["cell"]["lifeforms"])
        elif op == "mix":
            target = rng.choice([g["base"]] + g["extras"]) if g["extras"] else g["base"]
            if target.get("kind") != "none":
                target["mix"] = _mix(rng)
                target["domain"] = rng.choice(["Native", "Mix", "Jade", "Ruby", "Gold", "Blue"])
        elif op == "extra":
            if g["extras"] and rng.random() < 0.4:
                g["extras"].pop(rng.randrange(len(g["extras"])))
            elif len(g["extras"]) < 5:
                g["extras"].append(random_extra(rng))
            elif g["extras"]:
                e = rng.choice(g["extras"])
                e["leg"] = (e["leg"] + rng.gauss(0, 0.4)) % RINGS
                e["radius"] = _jiggle(rng, e["radius"], 40, 0, 300)
        elif op == "cell":
            c = g.get("cell") or random_cell(rng, g["base"]["kind"])
            key = rng.choice(["nucleus", "shell", "lifeforms"])
            fresh = random_cell(rng, g["base"]["kind"])
            c[key] = fresh[key]
            g["cell"] = c
        elif op == "cross" and g["base"]["kind"] != "none":
            g["base"]["cross_line"] = not g["base"].get("cross_line")
        elif op == "rails":
            st, r = g["stretches"], rng.random()
            if g["rails"] == "off" or r < 0.1:
                g["rails"] = "partial" if g["rails"] == "off" else "off"
            elif r < 0.3 and len(st) < 8:
                st.append(random_stretch(rng))
            elif r < 0.45 and len(st) > 1:
                st.pop(rng.randrange(len(st)))
            else:
                x = rng.choice(st)
                key = rng.choice(["leg", "from", "to", "kind", "neutral", "detour"])
                if key == "leg": x["leg"] = rng.randrange(RINGS)
                elif key == "from": x["from"] = round(_jiggle(rng, x["from"], 0.15, 0.0, x["to"] - 0.05), 3)
                elif key == "to": x["to"] = round(_jiggle(rng, x["to"], 0.15, x["from"] + 0.05, 1.0), 3)
                elif key == "kind": x["kind"] = 1 if x["kind"] == 2 else 2
                elif key == "neutral": x["neutral"] = not x["neutral"]
                else: x["detour"] = round(_jiggle(rng, x["detour"], 15, 0.0, 80.0), 2)
        else:
            g["sight"] = _jiggle(rng, g["sight"], 4, 8, 30)
            g["line"] = _jiggle(rng, g["line"], 8, 14, 70)
            g["crystals"] = max(0, min(4, g["crystals"] + rng.choice([-1, 1])))
    return g


# ── Judging ───────────────────────────────────────────────────────────────────────────────────────────────────────

def _judge():
    import regatta_judge
    return regatta_judge


def judge_retry(genome, hulls, seeds, limit_s, start):
    """regatta_judge.judge, with every hull that came back with NO race (its game process timed out while booting, a
    slow machine; 2026-11-21 on a busy PC whole fields came back empty) judged once more. A hull still missing is a
    hole, named in the result's "holes"; a DNF in a race that ran is a real DNF."""
    rj = _judge()
    r = rj.judge(genome, hulls, list(seeds), limit_s=limit_s, start=start)
    flown = {s["hull"] for race in r["races"] for s in race.get("seats", [])}
    missing = [h for h in hulls if h not in flown]
    if missing:
        again = rj.judge(genome, missing, list(seeds), limit_s=limit_s, start=start)
        r["races"].extend(again["races"])
        r["notes"] = sorted(set(r.get("notes", []) + again.get("notes", [])))
        flown |= {s["hull"] for race in again["races"] for s in race.get("seats", [])}
    r["holes"] = [h for h in hulls if h not in flown]
    return r


def imbalance(result, hulls):
    rj = _judge()
    by = {}
    for race in result["races"]:
        for s in race.get("seats", []):
            by.setdefault(s["hull"], []).append(s.get("finish_s"))
    scores = {h: rj.balance_score({"c": by.get(h, [])}) for h in hulls}
    have = [v for v in scores.values() if v]
    dnf = sum(1 for race in result["races"] for s in race.get("seats", []) if s.get("finish_s") is None)
    ratio = max(have) / min(have) if len(have) == len(hulls) else None
    return ratio, {h: (round(v, 2) if v else None) for h, v in scores.items()}, dnf


def evaluate(genome, seeds_a=(1,), seeds_b=(1,), limit_s=300):
    """Stage A (four hulls, one seed, card start), then the field at both starts. Returns the record's score part."""
    rj = _judge()
    rec = {}
    ra = judge_retry(genome, STAGE_A, seeds_a, limit_s, "card")
    a, times_a, dnf_a = imbalance(ra, STAGE_A)
    rec["stage_a"] = {"imbalance": a, "times": times_a, "dnf": dnf_a, "notes": ra.get("notes", [])[:6],
                      "holes": ra["holes"]}
    if ra["holes"]:
        rec["score"], rec["stage"], rec["error"] = None, "A", f"judge holes {ra['holes']}"
        return rec
    prisms = next((race.get("course_placement_prisms") for race in ra["races"] if race.get("course_placement_prisms") is not None), None)
    rec["prisms"] = prisms
    if prisms is not None and prisms < MIN_WORLD_PRISMS:
        # A world the carves ate (2026-11-21: a confirmed LSystem course laid 0 base prisms) is an empty cell.
        rec["score"], rec["stage"], rec["error"] = None, "A", f"thin world: {prisms} prisms laid"
        return rec
    if dnf_a > 1:
        # Two of the four probe hulls stuck is a course the field does not fly, however even its times look.
        rec["score"], rec["stage"], rec["error"] = None, "A", f"stage A: {dnf_a} of {len(STAGE_A)} did not finish"
        return rec
    if a is None or a > STAGE_A_BAR:
        # A stage-A failure ranks below every stage-B course of the same spread.
        rec["score"] = None if a is None else 2.0 * field_score({"card": {"times": times_a}})
        rec["stage"] = "A"
        return rec
    worst, out = 0.0, {}
    for start in ("card", "L5"):
        r = judge_retry(genome, FIELD, seeds_b, limit_s, start)
        x, times, dnf = imbalance(r, FIELD)
        out[start] = {"imbalance": x, "times": times, "dnf": dnf, "holes": r["holes"], "notes": r.get("notes", [])[:8]}
        if r["holes"]:
            rec.update(field=out, score=None, stage="B", error=f"judge holes {r['holes']} at {start}")
            return rec
        worst = max(worst, x if x else 99.0)
    rec["field"] = out
    rec["imbalance"] = worst
    rec["score"] = field_score(out)
    rec["stage"] = "B"
    return rec


# ── The archive (MAP-Elites) ──────────────────────────────────────────────────────────────────────────────────────

def _load(path):
    recs = []
    if os.path.exists(path):
        with open(path) as f:
            for line in f:
                line = line.strip()
                if line:
                    try:
                        recs.append(json.loads(line))
                    except ValueError:
                        pass
    return recs


def spread_stats(times):
    """(max/min, the tightest 9-hull window, exp of the RMS log distance from the median) of one start's times; a DNF
    counts as the 400 s limit."""
    v = sorted(x if x else 400.0 for x in times.values())
    med = v[len(v) // 2]
    rms = math.sqrt(sum(math.log(x / med) ** 2 for x in v) / len(v))
    # The window is over FINISHERS: nine DNFs at the 400 s limit are not a 1.00x window (2026-11-22, every partial-
    # rail course scored 1.00 with 9 of 11 hulls stuck).
    fin = [x for x in v if x < 400.0]
    k = min(9, len(v))
    nine = min(fin[i + k - 1] / fin[i] for i in range(len(fin) - k + 1)) if len(fin) >= k else 99.0
    return v[-1] / v[0], nine, math.exp(rms)


def field_score(field):
    """The search's balance score for a field result (worse of the two starts): the RMS log spread squared times the
    full spread's root. Max/min alone is set by the one or two worst hulls on a course (2026-11-21: courses with nine
    hulls inside 1.22x scored 3.6x for a slow Shrike), so it hid the courses closest to balance; the RMS rewards the
    whole field coming together and the root keeps an outlier from being free. Selection for Garrett still reads the
    true max/min."""
    worst = 0.0
    for v in field.values():
        # A start with no finisher at all is a judge that did not race (a crashed or starved game process), never a
        # perfectly balanced course: 2026-11-21 a PC shard's empty field scored 1.00.
        if not any(v["times"].values()):
            return None
        full, _, rms = spread_stats(v["times"])
        worst = max(worst, rms ** 2 * math.sqrt(full))
    return worst


def rescore(r):
    genes = (r.get("genome") or {}).get("wide") or {}
    if genes.get("rails") == "on" or (genes.get("base") or {}).get("kind") == "none" or \
            (r.get("prisms") is not None and r["prisms"] < MIN_WORLD_PRISMS) or \
            ((r.get("stage_a") or {}).get("dnf") or 0) > 1:
        return dict(r, score=None)
    if r.get("niche") and r["niche"].count("|") == 2:
        r = dict(r, niche=r["niche"] + "|r0")    # written before the rail-coverage axis: railless
    if r.get("stage") == "B" and r.get("field"):
        # A hull with no time at all (None; a DNF is 400) never raced: a hole, in records written before holes were named.
        holes = any(v.get("holes") or any(t is None for t in v["times"].values()) for v in r["field"].values())
        r = dict(r, score=None if holes or r.get("error") else field_score(r["field"]))
    elif r.get("stage") == "A" and (r.get("stage_a") or {}).get("times") and r.get("score") is not None:
        r = dict(r, score=2.0 * (field_score({"card": {"times": r["stage_a"]["times"]}}) or 99.0))
    return r


def elites(records):
    best = {}
    for r in map(rescore, records):
        if r.get("score") is None:
            continue
        d = r["niche"]
        if d not in best or r["score"] < best[d]["score"]:
            best[d] = r
    return best


def shard(out, budget, seed, init=None):
    os.makedirs(out, exist_ok=True)
    path = os.path.join(out, "records.jsonl")
    rng = random.Random(seed)
    records = _load(path)
    bases = base_choices()
    n_init = init if init is not None else len(bases) * 2
    done = len(records)
    print(f"[wide] shard {out}: {done} records, budget {budget}", flush=True)
    while done < budget:
        arch = elites(records)
        if done < n_init or not arch or rng.random() < 0.25:
            # Fill the map: a base and a super-shielded bin the archive has least of.
            counts = {}
            for d in arch:
                counts[d.split("|")[0]] = counts.get(d.split("|")[0], 0) + 1
            base = min(bases, key=lambda b: (counts.get(b, 0), rng.random())) if done >= n_init else bases[done % len(bases)]
            g, facts = random_legal(rng, base=base, ss=rng.choice([0, 0, 1, 2, 3]))
            origin = "random"
        else:
            # Mutate an elite, weighted toward the balanced ones but from every niche.
            pool = sorted(arch.values(), key=lambda r: r["score"])
            parent = pool[min(len(pool) - 1, int(abs(rng.gauss(0, len(pool) / 3))))]
            for _ in range(60):
                genes = mutate(parent["genome"]["wide"], rng)
                g = render(genes)
                ok, facts = check(g)
                if ok:
                    break
            else:
                continue
            origin = "mutant:" + parent["id"]
        rid = f"{os.path.basename(out.rstrip('/'))}-{done:04d}"
        t0 = time.time()
        try:
            sc = evaluate(g)
        except Exception as e:   # a crashed judge is a record too, so the search does not retry it forever
            sc = {"score": None, "error": f"{type(e).__name__}: {e}"[:400]}
        rec = dict(id=rid, origin=origin, niche=descriptor(g["wide"], facts), facts=facts, ss_share=round(ss_share(g["wide"]), 3),
                   genome=g, wall_s=round(time.time() - t0, 1), at=time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
                   **sc)
        with open(path, "a") as f:
            f.write(json.dumps(rec) + "\n")
        records.append(rec)
        done += 1
        print(f"[wide] {rid} {rec['niche']} stage {rec.get('stage')} score {rec.get('score')} "
              f"({rec['wall_s']} s) {rec.get('error', '')}", flush=True)


# ── Picking the set Garrett plays: balanced AND far apart ─────────────────────────────────────────────────────────

def distance(a, b):
    """How different two courses are: a different world counts most, then the state mix, the shape, the size."""
    ga, gb = a["genome"]["wide"], b["genome"]["wide"]
    d = 0.0
    d += 3.0 if ga["base"]["kind"] != gb["base"]["kind"] else 0.0
    d += 2.0 * abs(a["ss_share"] - b["ss_share"])
    d += 1.0 if ga["loop"]["winding"] != gb["loop"]["winding"] else 0.0
    d += abs(a["facts"]["vertical_u"] - b["facts"]["vertical_u"]) / 1500.0
    d += abs(a["facts"]["lap_u"] - b["facts"]["lap_u"]) / 5000.0
    d += 2.0 * abs(rail_coverage(ga) - rail_coverage(gb))
    return d


def pick(records, k=8, bar=1.25):
    arch = [r for r in elites(records).values() if r.get("stage") == "B"]
    arch.sort(key=lambda r: r["score"])
    good = [r for r in arch if r["score"] <= bar] or arch[:k * 2]
    chosen = []
    if good:
        chosen.append(good[0])
    while len(chosen) < k and len(chosen) < len(good):
        nxt = max((r for r in good if r not in chosen),
                  key=lambda r: min(distance(r, c) for c in chosen) - 2.0 * (r["score"] - 1.0))
        chosen.append(nxt)
    return chosen


def nine_window(r):
    return max(spread_stats(r["field"][s]["times"])[1] for s in ("card", "L5"))


def confirm(records, out, per_base=1, top=8, seeds=(2, 3), limit_s=300):
    """Re-race the most balanced stage-B course of each world (by its tightest nine-hull window, the seats a match
    fills) on two more seeds at both starts and fold in the screen's seed 1: a one-seed screen is noisy (2026-11-18:
    1.05-1.10x screens came out 1.15-1.48x on three seeds). The `top` worlds go first."""
    rj = _judge()
    path = os.path.join(out, "confirmed.jsonl")
    done = {r["id"] for r in _load(path)}
    by_base = {}
    cands = [r for r in map(rescore, records) if r.get("stage") == "B" and r.get("score") is not None]
    for r in sorted(cands, key=nine_window):
        if len(by_base) >= top and r["niche"].split("|")[0] not in by_base:
            continue
        key = r["niche"].split("|")[0]
        by_base.setdefault(key, [])
        if len(by_base[key]) < per_base:
            by_base[key].append(r)
    todo = [r for rs in by_base.values() for r in rs if r["id"] not in done]
    print(f"[wide] confirming {len(todo)} courses on seeds {list(seeds)}", flush=True)
    for r in todo:
        res = {}
        worst = 0.0
        for start in ("card", "L5"):
            jr = judge_retry(r["genome"], FIELD, seeds, limit_s, start)
            # The screen's seed-1 race joins the two new seeds.
            for h, t in r["field"][start]["times"].items():
                jr["races"].append({"seats": [{"hull": h, "finish_s": t}]})
            x, times, dnf = imbalance(jr, FIELD)
            res[start] = {"imbalance": x, "times": times, "dnf": dnf}
            worst = max(worst, x if x else 99.0)
            prisms = next((race.get("course_placement_prisms") for race in jr["races"]), None)
        c = dict(id=r["id"], niche=r["niche"], screen=r["score"], confirmed=worst, seeds=[1] + list(seeds), field=res,
                 nine=max(spread_stats(res[s]["times"])[1] for s in res),
                 prisms=prisms, genome=r["genome"], ss_share=r["ss_share"], facts=r["facts"])
        with open(path, "a") as f:
            f.write(json.dumps(c) + "\n")
        print(f"[wide] confirmed {r['id']} {r['niche']} nine {nine_window(r):.3f} -> {c['nine']:.3f}, "
              f"all eleven {worst:.3f}", flush=True)


def playtest_pool(confirmed, k=8):
    """A shuffle pool (Tools/Build/regatta_shuffle_pool.json shape) of the k most different confirmed courses, for a
    PLAYTEST branch: Regatta deals only these. Ref1-Ref4 go to the four most balanced (the generator needs them)."""
    for c in confirmed:
        c["score"] = c["confirmed"]
        c["stage"] = "B"
    chosen = pick(confirmed, k, bar=99.0)
    by_balance = sorted(chosen, key=lambda c: c["confirmed"])
    entries = []
    for i, c in enumerate(chosen):
        w = c["genome"]["wide"]
        cell = w.get("cell") or {}
        desc = (f"{w['base']['kind']} world, super-shielded {100 * c['ss_share']:.0f}%, "
                f"{'no rails' if w['rails'] == 'off' else f'rails on {rail_coverage(w):.0%} of the lap'}, {'no nucleus' if cell.get('nucleus') == 'None' else 'nucleus'}, "
                f"lifeforms {cell.get('lifeforms') or 'none'}, lap {c['facts']['lap_u']:.0f} u")
        spread = {st: round(v["imbalance"], 3) if v["imbalance"] else None for st, v in c["field"].items()}
        e = {"label": f"Wild{i + 1}", "genome": {k2: v for k2, v in c["genome"].items() if k2 != "wide"},
             "prisms": int(c.get("prisms") or 0) + 200, "candidate": c["id"],
             "notes": f"Wide search {c['id']}: {desc}. Field spread {spread} (3 seeds, real-game judge).",
             "judged": {st: v["times"] for st, v in c["field"].items()}}
        if c in by_balance[:4]:
            e["ref"] = f"Ref{by_balance.index(c) + 1}"
        entries.append(e)
    return {"picked_by": "Tools/Compute/evaluators/regatta_wide.py playtest (most different, then most balanced)",
            "imbalance": {st: max(c["field"][st]["imbalance"] or 99 for c in chosen) for st in ("card", "L5")},
            "refs_by": "the four most balanced wide-search courses (playtest branch only)", "entries": entries}


def self_test():
    rng = random.Random(7)
    seen = set()
    for i, base in enumerate(base_choices()):
        g, facts = random_legal(rng, base=base)
        assert g["version"] == 2 and len(g["rings"]) == RINGS, g
        assert check(g)[0]
        seen.add(descriptor(g["wide"], facts))
        m = mutate(g["wide"], rng)
        json.dumps(render(m))
    # The no-super-shielded bin is reachable for every base.
    g, facts = random_legal(rng, base="Yggdra", ss=0)
    assert ss_share(g["wide"]) < SS_BINS[1], ss_share(g["wide"])
    # Negative control: a lap that turns back on itself breaks the sight rule.
    bad = copy.deepcopy(g)
    for j in range(RINGS):
        bad["rings"][j]["pos"] = list(_mul(tuple(bad["rings"][j]["pos"]), 1.0 if j % 2 else -1.0))
    assert not check(bad)[0], "a zig-zag lap passed the sight check"
    print(f"regatta_wide self-test ok: {len(base_choices())} bases, {len(seen)} niches sampled")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("cmd", nargs="?", default="self-test")
    ap.add_argument("--self-test", action="store_true")
    ap.add_argument("--out")
    ap.add_argument("--n", type=int, default=5)
    ap.add_argument("--k", type=int, default=8)
    ap.add_argument("--budget", type=int, default=400)
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--base")
    ap.add_argument("dirs", nargs="*")
    a = ap.parse_args()
    if a.self_test or a.cmd == "self-test":
        return self_test()
    if a.cmd == "sample":
        os.makedirs(a.out, exist_ok=True)
        rng = random.Random(a.seed)
        for i in range(a.n):
            g, facts = random_legal(rng, base=a.base)
            with open(os.path.join(a.out, f"wide_{i}.json"), "w") as f:
                json.dump(g, f, indent=1)
            print(i, descriptor(g["wide"], facts), facts)
        return
    if a.cmd == "shard":
        return shard(a.out, a.budget, a.seed)
    if a.cmd == "confirm":
        recs = []
        for d in a.dirs:
            recs += _load(os.path.join(d, "records.jsonl"))
        os.makedirs(a.out, exist_ok=True)
        return confirm(recs, a.out)
    if a.cmd == "playtest":
        conf = []
        for d in a.dirs:
            conf += _load(os.path.join(d, "confirmed.jsonl"))
        pool = playtest_pool(conf, a.k)
        with open(a.out, "w") as f:
            json.dump(pool, f, indent=1)
        for e in pool["entries"]:
            print(e["label"], e.get("ref", ""), e["notes"])
        return
    if a.cmd in ("merge", "pick"):
        recs = []
        for d in a.dirs or [a.out]:
            recs += _load(os.path.join(d, "records.jsonl"))
        if a.cmd == "merge":
            os.makedirs(a.out, exist_ok=True)
            arch = elites(recs)
            with open(os.path.join(a.out, "archive.json"), "w") as f:
                json.dump(sorted(arch.values(), key=lambda r: r["score"]), f)
            print(f"{len(recs)} records, {len(arch)} niches filled")
            for r in sorted(arch.values(), key=lambda r: r["score"])[:20]:
                print(f"  {r['score']:.3f} {r['niche']} {r['id']}")
            return
        for r in pick(recs, a.k):
            print(f"{r['score']:.3f} {r['niche']} {r['id']}")
        return
    ap.error(f"unknown command {a.cmd}")


if __name__ == "__main__":
    main()
