#!/usr/bin/env python3
"""Regatta courses by the hundred, and the EQUATIONS that connect them (Plan step: time-arena-course; Regatta flywheel
round 1, worker W3).

Garrett (2026-11-16): "really mutate the heck out the course and find tons of different solutions in a vast space. then
we want to find the equations that connect those solutions together so we can find more quickly. then we will shuffle
these and have all kinds of fun races for players." One command does the whole chain:

    search    MAP-Elites (quality-diversity) over the course GENOME (the shared contract: circuit + features + placements;
              W2's operators replace ours when they land). The archive keeps the best course in every cell of what a
              player FEELS: which hull wins it, how much of the lap is ridable rail, how sharp its hardest corner is, how
              many kinds of feature it uses. So it holds many different good courses, not one optimum.
    judge     every course a candidate is raced by every hull, alone (Tools/Compute/evaluators/regatta_judge.py, backend
              "engine" = the game's own code in Amoebius once W1 lands; "model" = regatta_judge_model.py until then).
    loss      Tools/Compute/evaluators/regatta_loss.py: the spread, race noise, length band, finishes, guards; the pool's
              cancellation.
    equations a SURROGATE fitted to every race the judge has run: each hull's ln(seconds) as a ridge regression on
              inspectable course features (lap length, corners, mouths, ridable / neutral / gap shares, detours,
              alternate rings, crystals, placements by kind). It is checked on races it never saw; only a surrogate that
              predicts held-out races is used, and what it learned is written in plain words (EQUATIONS.md: "a tenth
              more neutral rail costs the Urchin ~x s and the Manta ~y s").
    screen    once trusted, the surrogate screens SCREEN x as many offspring as the judge can afford, and the judge
              races only the ones it predicts fill an empty cell or beat a cell's incumbent (plus a random share, so
              the surrogate keeps being tested where it is unsure). That is "find more quickly": `compare` runs the same
              judge budget with and without the screen and reports both archives.
    pool      Tools/Build/regatta_pool_pick.py's picker over the archive: every course passes on its own, the pool's
              per-hull means within 1.10x, no hull fastest everywhere, the courses as different as possible. pool.json
              carries each picked course's GENOME, ready for the course builder (author_regatta_assets.py, round 2).

Every number on the model backend is a MODEL's (the measured roster), and the files say so.

    python3 Tools/Build/regatta_qd.py run --budget 1500 --out DIR [--backend model|engine] [--no-surrogate]
    python3 Tools/Build/regatta_qd.py compare --budget 800 --out DIR       # the same budget, screened and not
    python3 Tools/Build/regatta_qd.py equations DIR [DIR ...]               # refit and rewrite EQUATIONS.md
    python3 Tools/Build/regatta_qd.py engine-pass --n 40 --out DIR          # designed courses in the game's code + model
    python3 Tools/Build/regatta_qd.py engine-report DIR --out DIR           # rewrite its ENGINE_VS_MODEL / EQUATIONS
    python3 Tools/Build/regatta_qd.py shard --shard K --of N --backend engine --budget 1000 --out ROOT/shard_K/
    python3 Tools/Build/regatta_qd.py merge ROOT --out ROOT/merged          # every shard -> archive, equations, pool
    python3 Tools/Build/regatta_qd.py --self-test

A READER tool: it writes only into --out.
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
ROOT = os.path.dirname(os.path.dirname(HERE))
EVAL = os.path.join(ROOT, "Tools", "Compute", "evaluators")
for _p in (HERE, EVAL):
    if _p not in sys.path:
        sys.path.insert(0, _p)

import regatta_judge_model as jm   # noqa: E402
import regatta_loss as L           # noqa: E402

HULLS = list(jm.HULLS)
REPORT_ONLY = ("Scarab",)           # races and is reported, not scored (it stalls in half its engine races, W1 2026-11-16)
SCORED = [h for h in HULLS if h not in REPORT_ONLY]


def set_hulls(hulls):
    """Race and score only these hulls (an engine run races the trusted roster only)."""
    global HULLS, SCORED
    HULLS = [h for h in jm.HULLS if h in hulls] + [h for h in hulls if h not in jm.HULLS]
    SCORED = [h for h in HULLS if h not in REPORT_ONLY]
SCREEN = 8                          # offspring the surrogate screens per judged one
EXPLORE = 0.25                      # share of each judged batch taken at random from the screened offspring
TRUST_R2 = 0.7                      # mean held-out R^2 over the scored hulls before the surrogate may screen
CANCEL_BAR = 1.10

# ── the genome: ranges and operators (circuit + features; W2's catalogue adds placements) ─────────────────────────

CIRCUIT = {"RingRadius": (60.0, 130.0), "CornerFloorRadius": (80.0, 320.0), "AngularSpread": (0.5, 6.0),
           "LateralPerturbation": (60.0, 400.0), "AxisJitterDegrees": (0.0, 50.0), "MaxPresentDegrees": (40.0, 85.0),
           "RadialSwing": (0.2, 0.6)}
CORNER = (0.0, 150.0)
LEGS = 8
MAX_SECTIONS = 12
MAX_ALTS = 8
DETOUR = (0.0, 150.0)
GAP_U = (16.0, 240.0)
ALT_OFFSET = (20.0, 420.0)
ALT_SCALE = (0.6, 1.4)
SEED_STRIDE = 104729


def _clamp(v, lo, hi):
    return lo if v < lo else hi if v > hi else v


def _wrap32(v):
    v &= 0xFFFFFFFF
    return v - (1 << 32) if v >= (1 << 31) else v


def _new_seed(rng):
    return _wrap32(jm._mr().MEASURED_SEED + SEED_STRIDE * rng.randrange(4096))


def _rand_section(rng, leg=None):
    leg = rng.randrange(LEGS) if leg is None else leg
    kind = rng.choice((0, 1, 2, 3, 3))
    if kind == 3:      # a gap: a length in u at a place on the leg; the leg's arc turns it into shares in repair()
        mid = rng.uniform(0.2, 0.8)
        return {"Leg": leg, "From": mid, "To": mid, "Kind": 3, "Neutral": False, "Detour": 0.0, "DetourBearing": 0.0,
                "GapU": rng.uniform(*GAP_U)}
    lo = rng.uniform(0.0, 0.8)
    return {"Leg": leg, "From": lo, "To": min(1.0, lo + rng.uniform(0.1, 0.6)), "Kind": kind,
            "Neutral": rng.random() < 0.5, "Detour": rng.uniform(*DETOUR) if rng.random() < 0.35 else 0.0,
            "DetourBearing": rng.uniform(0.0, 360.0)}


def _rand_alt(rng, ring=None):
    return {"Ring": rng.randrange(LEGS) if ring is None else ring, "Offset": rng.uniform(*ALT_OFFSET),
            "Bearing": rng.uniform(0.0, 360.0), "RadiusScale": rng.uniform(*ALT_SCALE)}


def random_genome(rng):
    g = jm.today_genome()
    c = g["circuit"]
    c["CornerProfile"] = [rng.uniform(*CORNER) for _ in range(LEGS)]
    for k, (lo, hi) in CIRCUIT.items():
        c[k] = rng.uniform(lo, hi)
    g["seed"] = _new_seed(rng)
    g["features"]["Sections"] = [_rand_section(rng) for _ in range(rng.choice((0, 1, 2, 3, 4, 6)))]
    g["features"]["AlternateGates"] = [_rand_alt(rng) for _ in range(rng.choice((0, 0, 1, 2, 3)))]
    g["features"]["Crystals"] = {"PerLap": rng.choice((0, 0, 0, 1, 2, 4)), "Phase": rng.random()}
    return repair(g)


def mutate(g, rng, strength=1.0):
    g = copy.deepcopy(g)
    c, f = g["circuit"], g["features"]
    ops = ["circuit", "circuit", "corner", "add_section", "remove_section", "tweak_section", "add_alt", "remove_alt",
           "tweak_alt", "crystals", "reseed"]
    for _ in range(rng.choice((1, 1, 2, 3))):
        op = rng.choice(ops)
        if op == "circuit":
            k = rng.choice(list(CIRCUIT))
            lo, hi = CIRCUIT[k]
            c[k] = c[k] + rng.gauss(0.0, 0.15 * strength * (hi - lo))
        elif op == "corner":
            i = rng.randrange(LEGS)
            prof = list(c["CornerProfile"])
            prof[i] = prof[i] + rng.gauss(0.0, 25.0 * strength)
            c["CornerProfile"] = prof
        elif op == "add_section" and len(f["Sections"]) < MAX_SECTIONS:
            f["Sections"].append(_rand_section(rng))
        elif op == "remove_section" and f["Sections"]:
            f["Sections"].pop(rng.randrange(len(f["Sections"])))
        elif op == "tweak_section" and f["Sections"]:
            s = rng.choice(f["Sections"])
            what = rng.choice(("span", "kind", "neutral", "detour", "leg"))
            if what == "span":
                d = rng.gauss(0.0, 0.1 * strength)
                s["From"], s["To"] = s["From"] + d, s["To"] + d + rng.gauss(0.0, 0.05)
                if s["Kind"] == 3:
                    s["GapU"] = s.get("GapU", 40.0) * math.exp(rng.gauss(0.0, 0.3))
            elif what == "kind":
                s["Kind"] = rng.choice((0, 1, 2, 3))
                if s["Kind"] == 3:
                    s.setdefault("GapU", rng.uniform(*GAP_U))
                    s["Neutral"], s["Detour"] = False, 0.0
            elif what == "neutral" and s["Kind"] != 3:
                s["Neutral"] = not s["Neutral"]
            elif what == "detour" and s["Kind"] != 3:
                s["Detour"] = s["Detour"] + rng.gauss(0.0, 40.0)
                s["DetourBearing"] = (s["DetourBearing"] + rng.gauss(0.0, 60.0)) % 360.0
            elif what == "leg":
                s["Leg"] = rng.randrange(LEGS)
        elif op == "add_alt" and len(f["AlternateGates"]) < MAX_ALTS:
            f["AlternateGates"].append(_rand_alt(rng))
        elif op == "remove_alt" and f["AlternateGates"]:
            f["AlternateGates"].pop(rng.randrange(len(f["AlternateGates"])))
        elif op == "tweak_alt" and f["AlternateGates"]:
            a = rng.choice(f["AlternateGates"])
            a["Offset"] += rng.gauss(0.0, 60.0 * strength)
            a["Bearing"] = (a["Bearing"] + rng.gauss(0.0, 45.0)) % 360.0
            a["RadiusScale"] += rng.gauss(0.0, 0.1)
        elif op == "crystals":
            f["Crystals"] = {"PerLap": rng.choice((0, 1, 2, 3, 4)), "Phase": rng.random()}
        elif op == "reseed":
            g["seed"] = _new_seed(rng)
    return repair(g)


def crossover(a, b, rng):
    """Circuit numbers uniform; features by leg (a leg's sections and its ring's alternate come from one parent)."""
    g = copy.deepcopy(a)
    for k in CIRCUIT:
        if rng.random() < 0.5:
            g["circuit"][k] = b["circuit"][k]
    if rng.random() < 0.5:
        g["circuit"]["CornerProfile"] = list(b["circuit"]["CornerProfile"])
    if rng.random() < 0.5:
        g["seed"] = b["seed"]
    take_b = [rng.random() < 0.5 for _ in range(LEGS)]
    pick = lambda leg: b if (leg >= 0 and take_b[leg]) else a
    g["features"]["Sections"] = [copy.deepcopy(s) for p in (a, b) for s in p["features"]["Sections"]
                                 if pick(int(s["Leg"])) is p]
    g["features"]["AlternateGates"] = [copy.deepcopy(x) for p in (a, b) for x in p["features"]["AlternateGates"]
                                       if pick(int(x["Ring"])) is p]
    return repair(g)


def repair(g):
    """Clamp every gene to its legal range so a mutated genome stays a course the game can lay: corners renormalised to
    360, sections ordered inside their leg, gaps clear of every ring, one alternate per ring, at most 4 line crystals.
    Whether the generator then FINDS a circuit, and whether every rail threads every mouth, is the judge's guard."""
    c, f = g["circuit"], g["features"]
    prof = [_clamp(float(x), *CORNER) for x in c["CornerProfile"]]
    tot = sum(prof) or 1.0
    c["CornerProfile"] = [x * 360.0 / tot for x in prof]
    for k, (lo, hi) in CIRCUIT.items():
        c[k] = _clamp(float(c[k]), lo, hi)
    secs = []
    for s in f["Sections"][:MAX_SECTIONS]:
        s["Leg"] = int(_clamp(int(s["Leg"]), -1, LEGS - 1))
        s["Kind"] = int(_clamp(int(s["Kind"]), 0, 3))
        if s["Kind"] == 3:
            s["GapU"] = _clamp(float(s.get("GapU", 40.0)), *GAP_U)
            mid = _clamp(0.5 * (float(s["From"]) + float(s["To"])), 0.1, 0.9)
            s["From"] = s["To"] = mid          # the span is set from GapU and the leg's arc when the course is laid
            s["Neutral"], s["Detour"], s["DetourBearing"] = False, 0.0, 0.0
            if s["Leg"] < 0:
                s["Leg"] = 0
        else:
            lo, hi = sorted((_clamp(float(s["From"]), 0.0, 1.0), _clamp(float(s["To"]), 0.0, 1.0)))
            if hi - lo < 0.02:
                continue
            s["From"], s["To"] = round(lo, 4), round(hi, 4)
            s["Detour"] = round(_clamp(float(s["Detour"]), *DETOUR), 1)
            if s["Detour"] < 1.0:
                s["Detour"] = 0.0
            s["DetourBearing"] = round(float(s["DetourBearing"]) % 360.0, 1)
            s["Neutral"] = bool(s["Neutral"])
        secs.append(s)
    f["Sections"] = secs
    alts, seen = [], set()
    for a in f["AlternateGates"]:
        a["Ring"] = int(_clamp(int(a["Ring"]), 0, LEGS - 1))
        if a["Ring"] in seen:
            continue
        seen.add(a["Ring"])
        a["Offset"] = round(_clamp(float(a["Offset"]), *ALT_OFFSET), 1)
        a["Bearing"] = round(float(a["Bearing"]) % 360.0, 1)
        a["RadiusScale"] = round(_clamp(float(a["RadiusScale"]), *ALT_SCALE), 3)
        alts.append(a)
    f["AlternateGates"] = alts[:MAX_ALTS]
    cr = f.get("Crystals") or {}
    f["Crystals"] = {"PerLap": int(_clamp(int(cr.get("PerLap", 0)), 0, 4)), "Phase": round(float(cr.get("Phase", 0.0)) % 1.0, 4)}
    return g


# W2's genome module (Tools/Compute/evaluators/regatta_genome.py: every segment spawner, flora and placement kind) takes
# over the operators the moment it exists; ours stay as the circuit + features fallback.
try:
    import regatta_genome as _w2      # noqa: E402
except ImportError:
    _w2 = None


def ops():
    """(random_genome, mutate, crossover, repair) - W2's when it has landed, else this module's."""
    if _w2 is None:
        return random_genome, mutate, crossover, repair
    rep = lambda g: (lambda out: out[0] if isinstance(out, tuple) else out)(_w2.repair(g))
    prior = player_prior()

    def mutate_(g, rng, strength=1.0):
        if prior is not None:
            try:      # W2's genes from play: half the added or moved placements where people beat the AIs
                return rep(_w2.mutate(g, rng, strength, prior=prior))
            except TypeError:
                pass
        return rep(_w2.mutate(g, rng, strength))

    def random_(rng):
        if prior is not None and rng.random() < 0.5:      # half the fresh courses are made from play
            try:
                import regatta_player_prior as pp
                return rep(pp.seed_genomes(prior, rng, rng.randint(1, 4), 1)[0])
            except (ImportError, AttributeError):
                pass
        return rep(_w2.random_genome(rng))      # any intensity: its circuit row and seed are genes too
    return random_, mutate_, (lambda a, b, rng: rep(_w2.crossover(a, b, rng))), rep


_PRIOR = []


def player_prior():
    """W2's player prior (Tools/Compute/regatta/player_prior.json, built from human Regatta flights), or None."""
    if not _PRIOR:
        try:
            import regatta_player_prior as pp
            _PRIOR.append(json.load(open(pp.PRIOR, encoding="utf-8")))
        except (ImportError, OSError, ValueError, AttributeError):
            _PRIOR.append(None)
    return _PRIOR[0]


def game_genome(g, leg_arc):
    """The genome as the game reads it: a gap's GapU (u) turned into the From/To share of its leg (RegattaCourseFeatures
    has no length field), clear of the rings, and the search-only keys dropped."""
    out = copy.deepcopy(g)
    secs = []
    for s in out["features"]["Sections"]:
        if s["Kind"] == 3 and "GapU" in s:
            arc = leg_arc[s["Leg"]] if 0 <= s["Leg"] < len(leg_arc) else 0.0
            if arc <= 1.0:
                continue
            half = min(0.5 * s["GapU"] / arc, 0.4)
            mid = _clamp(s["From"], 0.05 + half, 0.95 - half)
            s["From"], s["To"] = round(mid - half, 4), round(mid + half, 4)
        s.pop("GapU", None)
        secs.append(s)
    out["features"]["Sections"] = secs
    return out


def lay_genome(g):
    """(the game genome, the laid course). Laying twice: once plain for the legs' arcs (which a gap's length needs)."""
    import regatta_features as rf
    plain = copy.deepcopy(g)
    plain["features"] = rf.empty()
    arcs = jm.lay(plain)["legArc"]
    gg = game_genome(g, arcs)
    return gg, jm.lay(gg)


# ── course features: the surrogate's inputs and the archive's descriptors ─────────────────────────────────────────

FEATURES = ("rail_prisms", "lap_u", "lane_over_spine", "max_turn", "mean_turn", "rms_turn", "hard_corners", "min_mouth",
            "corner_floor", "ridable", "neutral", "gap_share", "plain", "shielded", "gap_count", "free_stretches",
            "detour_u", "alternates", "alt_offset", "crystals")


def _corner_floor(g):
    c = g.get("circuit") or {}
    if "CornerFloorRadius" in c:
        return float(c["CornerFloorRadius"])
    import course_port as cp
    return float(cp.regatta_settings(int(g.get("intensity", 1)))["CornerFloorRadius"])


def course_features(g, course):
    """The inspectable numbers a course is described by (FEATURES); W2's placements add `kind:<id>` counts."""
    import regatta_features as rf
    turns = [float(t) for t in course["turnDegrees"]]
    seg = course["segments"][0]
    share = seg["share"]
    fd = rf.descriptors(course)
    alts = course["alternates"]
    x = {"lap_u": float(course["spineLength"]),
         "lane_over_spine": sum(course["laneLengths"]) / len(course["laneLengths"]) / float(course["spineLength"]),
         "max_turn": max(turns), "mean_turn": sum(turns) / len(turns),
         "rms_turn": math.sqrt(sum(t * t for t in turns) / len(turns)), "hard_corners": sum(1 for t in turns if t > 60.0),
         "min_mouth": min(float(gt["r"]) for gt in course["gates"]), "corner_floor": _corner_floor(g),
         "ridable": float(fd["ridable_share"]), "neutral": sum(v for k, v in share.items() if k.isupper()),
         "gap_share": float(share.get("_", 0.0)), "plain": float(share.get("p", 0.0) + share.get("P", 0.0)),
         "shielded": float(share.get("h", 0.0) + share.get("H", 0.0)), "gap_count": float(fd["gap_count"]),
         "free_stretches": float(seg["free_stretches"]), "detour_u": float(fd["detour_u"]),
         "alternates": float(len(alts)),
         "alt_offset": sum(a["Offset"] for a in g["features"]["AlternateGates"]) / max(1, len(alts)) if alts else 0.0,
         "crystals": float(fd["crystals"]),
         "rail_prisms": float(sum(len(lane) for lane in course["lanes"]))}
    x["prisms"] = x["rail_prisms"]
    if _w2 is not None and hasattr(_w2, "descriptors"):
        for k, v in (_w2.descriptors(g) or {}).items():
            if isinstance(v, (int, float)) and not isinstance(v, bool):
                x["w2:" + k] = float(v)
            elif isinstance(v, dict):
                x.update({f"w2:{k}:{kk}": float(vv) for kk, vv in v.items() if isinstance(vv, (int, float))})
        x["prisms"] = x["rail_prisms"] + x.get("w2:est_prisms", 0.0)    # the catalogue's estimate (the carve lays fewer)
    else:
        for p in g.get("placements") or ():
            k = "kind:" + str(p.get("kind"))
            x[k] = x.get(k, 0.0) + 1.0
    return add_families(x)


def kinds_used(g, x):
    f = g["features"]
    used = {"neutral" if x["neutral"] > 0.01 else None, "gap" if x["gap_count"] else None,
            "detour" if x["detour_u"] > 1.0 else None, "plain/shielded" if x["plain"] + x["shielded"] > 0.01 else None,
            "alternate" if f["AlternateGates"] else None, "crystals" if x["crystals"] else None}
    used |= {p.get("kind") for p in g.get("placements") or ()}
    used.discard(None)
    return sorted(used)


def cell_of(leader, x, nkinds):
    """The archive cell: who wins it, how ridable, how sharp, how varied (0-1, 2-4, 5+ kinds of feature or placement)."""
    rid = 0 if x["ridable"] < 0.6 else 1 if x["ridable"] < 0.9 else 2
    sharp = 0 if x["max_turn"] < 70 else 1 if x["max_turn"] < 110 else 2
    if "w2:placements" in x:     # W2's genome: how heavy the course is (prisms laid against the budget), so lean courses
        n = x.get("prisms", 0.0) / L.PRISM_BUDGET               # keep cells of their own (performance, Garrett 11-16)
        var = 0 if n <= 0.5 else 1 if n <= 1.0 else 2
    else:
        var = 0 if nkinds <= 1 else 1 if nkinds <= 4 else 2
    return f"{leader}|r{rid}|s{sharp}|k{var}"


# ── evaluating a genome (runs in the worker processes) ───────────────────────────────────────────────────────────

_REF = {}


def today():
    """Today's course as a genome: W2's empty genome when its module exists, else the measured-on circuit."""
    return _w2.empty() if _w2 is not None else jm.today_genome()


def _ref_mean(backend):
    if backend not in _REF:
        g = today()
        r = _judge(g, jm.lay(g), backend)
        times = L.alone_times(r, SCORED)
        _REF[backend] = sum(sum(v) / len(v) for v in times.values()) / len(times)
    return _REF[backend]


# The hulls the engine judge flies reliably (W1's baseline 2026-11-16, after the Stoat and Dolphin AI fixes #2037 / #2038,
# #2042; alone, I1, 4 seeds): Rhino 35.4, Stoat 46.1, Urchin 51.0, Squirrel 80.1, Manta 82.3, Sparrow 90.5, Serpent 107.0,
# Dolphin 131.0 - a 3.7x field. The Scarab stalls in half its races and waits. "mixed" races these in the engine and
# prices any other hull with the model; every seat carries the backend that timed it.
ENGINE_TRUSTED = tuple(h for h in os.environ.get(
    "REGATTA_ENGINE_HULLS", "Dolphin,Manta,Rhino,Serpent,Sparrow,Squirrel,Stoat,Urchin").split(",") if h)
# The judge seeds each race at turn start, so (seed, race) is deterministic; the search races one seed per course (8
# hulls x 2 seeds is ~4.5 min a course on 4 cores) and the pool's candidates are re-raced on more seeds for the noise.
# The start condition every hull races under: "card" (the arcade card's own starting elements, today) or "L5" (every
# hull at Time level 5). Garrett, 2026-11-16: search and balance Regatta under both. One condition per shard, so each
# condition is its own archive; merge reports each.
START = os.environ.get("REGATTA_START", "card")

# Stage 1 of a gated engine course: the hulls most often fastest (Rhino, Stoat) and slowest (Serpent, Dolphin) on the
# first engine courses; four is one round on four cores, so a course stopped here costs half.
STAGE1 = tuple(h for h in os.environ.get("REGATTA_STAGE1", "Rhino,Stoat,Serpent,Dolphin").split(",") if h)
ENGINE_SEEDS = tuple(int(x) for x in os.environ.get("REGATTA_ENGINE_SEEDS", "1").split(","))


def _rj():
    """W1's judge (Tools/Compute/evaluators/regatta_judge.py, or REGATTA_JUDGE_DIR while it is on its branch)."""
    d = os.environ.get("REGATTA_JUDGE_DIR")
    if d and d not in sys.path:
        sys.path.insert(0, d)
    import regatta_judge as rj
    return rj


def _judge(g, course, backend, seeds=(0,), hulls=None):
    """RESULT for every hull (or `hulls`), alone. backend: "model" | "engine" (every hull in the game's code) | "mixed"
    (ENGINE_TRUSTED in the engine, the rest the model). Each seat row is tagged with the backend that timed it."""
    seats = [{"hull": h, "ai": "shipped"} for h in (hulls or HULLS)]
    if backend == "model":
        r = jm.judge_course(course, seats, list(seeds))
        for race in r["races"]:
            for s in race["seats"]:
                s["backend"] = "model"
        return r
    eng = seats if backend == "engine" else [s for s in seats if s["hull"] in ENGINE_TRUSTED]
    if START == "card":
        r = _rj().judge(g, eng, list(seeds), backend="engine")
    else:       # Garrett 2026-11-16: balance both with and without every hull starting at Time L5
        try:
            r = _rj().judge(g, eng, list(seeds), backend="engine", start=START)
        except TypeError:
            raise SystemExit(f"this regatta_judge.py has no start condition: --start {START} needs W1's start field")
    races = []
    for race in r["races"]:
        for s in race.get("seats", []):
            races.append({"seed": race.get("seed", 0), "field": False, "seats": [dict(s, backend="engine")]})
    got = {(x["seed"], x["seats"][0]["hull"]) for x in races}
    for seed in seeds:     # an engine seat with no race (a crash, a timeout) is a DNF, never silently a model time
        for s in eng:
            if (seed, s["hull"]) not in got:
                races.append({"seed": seed, "field": False, "seats": [{"hull": s["hull"], "finish_s": None,
                                                                       "dnf_reason": "no engine race", "backend": "engine"}]})
    if backend == "mixed":
        m = jm.judge_course(course, [s for s in seats if s["hull"] not in ENGINE_TRUSTED], list(seeds))
        for race in m["races"]:
            for s in race["seats"]:
                races.append({"seed": race["seed"], "field": False, "seats": [dict(s, backend="model")]})
    return {"races": races, "backend": backend, "engine_commit": r.get("engine_commit"), "wall_s": r.get("wall_s"),
            "notes": r.get("notes", [])}


def featurize(g):
    """Lay a genome and describe it, without racing: (game genome, x, guard fails) or (None, error, None)."""
    import regatta_features as rf
    try:
        gg, course = lay_genome(g)
    except Exception as e:      # the generator found no legal circuit for these numbers
        return None, f"{type(e).__name__}: {e}", None
    return gg, course_features(gg, course), rf.guard_fails(course)


def evaluate(job):
    """One judge call: (search genome, backend, seeds[, gate]) -> the record the archive and the surrogate learn from.
    With a `gate` (a spread) on the engine, the course is raced in two stages: first STAGE1 (the hulls that are most
    often the fastest and the slowest: one round of the 4 cores), and the rest only when those four are already inside
    the gate. A course stopped at stage 1 is kept as a PARTIAL record: its four times still train the surrogate."""
    g, backend, seeds = job[:3]
    gate = job[3] if len(job) > 3 else None
    import regatta_features as rf
    try:
        gg, course = lay_genome(g)
    except Exception as e:
        return {"genome": g, "valid": False, "error": f"{type(e).__name__}: {e}"}
    x = course_features(gg, course)
    guard = rf.guard_fails(course)
    if guard and backend != "model":
        # A rail through the nucleus or the membrane, or one missing a mouth, is a course defect (the 140 u detours the
        # first engine pass raced made the Serpent DNF): it is never worth an engine race. Recorded, not raced.
        return {"genome": g, "game_genome": gg, "valid": False, "x": x, "guard_fails": guard,
                "error": f"{guard} guard fails: not raced"}
    stage1 = [h for h in STAGE1 if h in HULLS]
    if gate is not None and backend != "model" and len(stage1) >= 2 and len(HULLS) > len(stage1):
        r1 = _judge(gg, course, backend, seeds, hulls=stage1)
        t1 = {h: v for h, v in L.alone_times(r1).items()}
        done = {h: sum(v) / len(v) for h, v in t1.items() if v and all(t is not None for t in v)}
        lb = max(done.values()) / min(done.values()) if len(done) >= 2 else float("inf")
        if lb > gate or len(done) < len(stage1):
            tb = {st["hull"]: st.get("backend", backend) for race in r1["races"] for st in race["seats"]}
            return {"genome": g, "game_genome": gg, "valid": False, "partial": True, "x": x,
                    "seconds": {h: done.get(h) for h in t1}, "timed_by": tb, "stage1_spread": round(lb, 4),
                    "kinds": kinds_used(gg, x), "error": (f"stage 1: {', '.join(h for h in stage1 if h not in done)} did not finish: stopped"
                              if len(done) < len(stage1) else f"stage 1 spread {lb:.2f}x > gate {gate:.2f}x: stopped"),
                    "backend": backend, "engine_commit": r1.get("engine_commit")}
        r2 = _judge(gg, course, backend, seeds, hulls=[h for h in HULLS if h not in stage1])
        r = dict(r2, races=r1["races"] + r2["races"])
    else:
        r = _judge(gg, course, backend, seeds)
    loss = L.course_loss(r, SCORED, _ref_mean(backend), guard_fails=guard, prisms=x.get("prisms"))
    times = L.alone_times(r)
    secs = {h: (None if any(t is None for t in v) else sum(v) / len(v)) for h, v in times.items()}
    kinds = kinds_used(gg, x)
    leader = SCORED.index(loss["fastest"]) if loss.get("fastest") else -1
    timed_by = {}
    for race in r["races"]:
        for st in race["seats"]:
            timed_by[st["hull"]] = st.get("backend", r.get("backend", backend))
    return {"genome": g, "game_genome": gg, "valid": True, "x": x, "seconds": secs, "loss": loss, "kinds": kinds,
            "timed_by": timed_by,
            "cell": cell_of(leader, x, len(kinds)), "backend": r.get("backend", backend),
            "engine_commit": r.get("engine_commit"), "wall_s": r.get("wall_s")}


# ── the surrogate: ridge regression of each hull's ln(seconds) on the course features ───────────────────────────

def _np():
    import numpy as np
    return np


class Surrogate:
    """ln(t_h) = b0_h + sum_j b_hj * z_j   with z the standardised FEATURES (+ any placement kinds seen), one ridge per hull,
    the penalty chosen by 5-fold cross-validation. Linear in ln time, so each coefficient is a MULTIPLIER on a hull's time:
    the equation is readable as "+1 sd of this feature x exp(b) on that hull"."""

    ALPHAS = (0.01, 0.1, 0.3, 1.0, 3.0, 10.0, 30.0, 100.0)

    def __init__(self, names):
        self.names = list(names)
        self.alpha, self.mu, self.sd, self.coef, self.icpt = None, None, None, {}, {}
        self.held_out = {}

    @staticmethod
    def _xy(records, names, hull):
        rows = [(r["x"], r["seconds"].get(hull)) for r in records if usable(r) and r["seconds"].get(hull)]
        X = [[float(x.get(n, 0.0)) for n in names] for x, _ in rows]
        y = [math.log(t) for _, t in rows]
        return X, y

    def _fit_one(self, X, y, alpha):
        np = _np()
        Z = (X - self.mu) / self.sd
        A = Z.T @ Z + alpha * np.eye(Z.shape[1])
        b = np.linalg.solve(A, Z.T @ (y - y.mean()))
        return b, y.mean()

    def fit(self, records, hulls=HULLS, alpha=None):
        np = _np()
        allx = np.array([[float(r["x"].get(n, 0.0)) for n in self.names] for r in records if usable(r)])
        self.mu = allx.mean(axis=0)
        self.sd = allx.std(axis=0)
        self.sd[self.sd < 1e-9] = 1.0
        if alpha is None:
            best = None
            for a in self.ALPHAS:
                err = 0.0
                for h in hulls:
                    X, y = map(np.array, self._xy(records, self.names, h))
                    folds = np.arange(len(y)) % 5
                    for k in range(5):
                        tr, te = folds != k, folds == k
                        if te.sum() == 0 or tr.sum() < 3:
                            continue
                        b, c = self._fit_one(X[tr], y[tr], a)
                        err += float(((((X[te] - self.mu) / self.sd) @ b + c - y[te]) ** 2).sum())
                if best is None or err < best[0]:
                    best = (err, a)
            alpha = best[1]
        self.alpha = alpha
        for h in hulls:
            X, y = map(np.array, self._xy(records, self.names, h))
            if len(y) < 3:
                continue
            self.coef[h], self.icpt[h] = self._fit_one(X, y, alpha)
        return self

    def predict(self, x):
        """{hull: seconds} for one feature dict."""
        np = _np()
        z = (np.array([float(x.get(n, 0.0)) for n in self.names]) - self.mu) / self.sd
        return {h: math.exp(float(z @ b + self.icpt[h])) for h, b in self.coef.items()}

    def score(self, records, hulls=SCORED):
        """Held-out accuracy on records it was not fitted on: R^2 of ln time and the mean absolute error in seconds per
        hull, the R^2 of ln(spread), and how often it names the right leader."""
        np = _np()
        out = {}
        for h in hulls:
            rows = [(r["x"], r["seconds"][h]) for r in records if usable(r) and r["seconds"].get(h) and h in self.coef]
            if len(rows) < 3:
                continue
            p = np.array([math.log(self.predict(x)[h]) for x, _ in rows])
            y = np.array([math.log(t) for _, t in rows])
            ss = float(((y - y.mean()) ** 2).sum())
            out[h] = {"r2": round(1.0 - float(((p - y) ** 2).sum()) / ss, 3) if ss > 1e-12 else None,
                      "mae_s": round(float(np.abs(np.exp(p) - np.exp(y)).mean()), 2), "n": len(rows)}
        sp, so, right = [], [], 0
        rs = [r for r in records if r.get("valid") and all(r["seconds"].get(h) for h in hulls)]
        for r in rs:
            pr = self.predict(r["x"])
            ps = {h: pr[h] for h in hulls}
            ts = {h: r["seconds"][h] for h in hulls}
            sp.append(math.log(max(ps.values()) / min(ps.values())))
            so.append(math.log(max(ts.values()) / min(ts.values())))
            right += min(ps, key=ps.get) == min(ts, key=ts.get)
        r2s = [v["r2"] for v in out.values() if v["r2"] is not None]
        spread_r2 = None
        if len(rs) >= 3:
            sp, so = np.array(sp), np.array(so)
            ss = float(((so - so.mean()) ** 2).sum())
            spread_r2 = round(1.0 - float(((sp - so) ** 2).sum()) / ss, 3) if ss > 1e-12 else None
        self.held_out = {"per_hull": out, "mean_r2": round(sum(r2s) / len(r2s), 3) if r2s else None,
                         "spread_r2": spread_r2, "leader_right": round(right / len(rs), 3) if rs else None, "n": len(rs)}
        return self.held_out

    def trusted(self):
        return bool(self.held_out.get("mean_r2") is not None and self.held_out["mean_r2"] >= TRUST_R2)

    def to_json(self):
        return {"features": self.names, "alpha": self.alpha, "mean": [float(v) for v in self.mu],
                "sd": [float(v) for v in self.sd], "intercept": {h: float(v) for h, v in self.icpt.items()},
                "coef": {h: [float(v) for v in b] for h, b in self.coef.items()}, "held_out": self.held_out,
                "form": "ln(seconds_h) = intercept_h + sum_j coef_hj * (x_j - mean_j) / sd_j"}


def usable(r):
    """A record the surrogate may learn from: fully raced, or stopped after stage 1 (its raced hulls' times)."""
    return bool(r.get("valid") or (r.get("partial") and r.get("x")))


def engine_only(records):
    """The records with only their ENGINE-timed seconds (a mixed run's model seats dropped): what the surrogate learns
    the game from."""
    out = []
    for r in records:
        if not r.get("valid"):
            continue
        tb = r.get("timed_by") or {}
        secs = {h: t for h, t in r["seconds"].items() if tb.get(h) == "engine"}
        if secs:
            out.append(dict(r, seconds=secs))
    return out


PER_KIND = ("w2:count_by_kind:", "w2:mean_scale_by_kind:", "kind:")

# W2's 52 placement kinds pooled by what a pilot meets (W3's grouping, 2026-11-16): the surrogate learns one input per
# family until there are enough courses for one per kind. The rail features (neutral, gaps, plain, shielded, detours)
# are their own family already: FEATURES.
FAMILIES = {
    "walls": ("Wall", "Panes", "Shatterplane", "Flatline", "Breakwater", "Standoff", "DartBoard", "ShapeSign"),
    "surfaces": ("Gyroid", "SchwarzPSurface", "Spherene", "Helicoid", "Ellipsoid", "Geode", "Caldera", "Pumpkin", "Swell"),
    "rings": ("FiveRings", "LinkedRings", "CliffordTorus", "HopfFibration", "TorusKnot", "Ourobor", "Orrery", "Corona",
              "Garland", "Twistbands"),
    "strands": ("Helix", "Skein", "Jackstraw", "SingleTrailBlock", "Comet", "Zephyr", "Harvest", "RaceTrack",
                "WaypointTrack", "RegattaRails", "SwitchbackFeatures", "Switchyard"),
    "growth": ("LSystem", "Yggdra", "Kelpwood", "Hesperides", "Ribcage", "Boneyard", "Flora"),
    "structures": ("Atlantis", "Batman", "Daedala", "WildlifeCage", "ShapeBase", "Crystal"),
}
_FAMILY_OF = {k: f for f, ks in FAMILIES.items() for k in ks}


def add_families(x):
    """x with one count per placement family ("fam:<family>"), from its per-kind counts. Idempotent, so records judged
    before the families existed are featurised the same way."""
    fam = {f"fam:{f}": 0.0 for f in FAMILIES}
    fam["fam:other"] = 0.0
    for k, v in x.items():
        for pre in ("w2:count_by_kind:", "kind:"):
            if k.startswith(pre):
                fam["fam:" + _FAMILY_OF.get(k[len(pre):], "other")] += float(v)
    x.update(fam)
    return x


def feature_names(records, per_kind=None):
    """The surrogate's inputs: FEATURES plus W2's aggregate descriptors. One input per placement kind (about 60 of them)
    is only added once there are 10 courses per input, or ridge fits the noise (held-out R^2 -0.64 on 22 courses,
    2026-11-16). Inputs that never vary are dropped."""
    rs = [r for r in records if usable(r)]
    for r in rs:
        add_families(r["x"])
    extra = sorted({k for r in rs for k in r["x"] if k not in FEATURES})
    agg = [k for k in extra if not k.startswith(PER_KIND)]
    kinds = [k for k in extra if k.startswith(PER_KIND)]
    names = list(FEATURES) + agg
    if per_kind or (per_kind is None and len(rs) >= 10 * (len(names) + len(kinds))):
        names += kinds
    return [n for n in names if len({float(r["x"].get(n, 0.0)) for r in rs}) > 1]


def fit_trusted(records, seed=0, min_n=30, folds=5):
    """Held-out accuracy by K-fold cross-validation (every course is predicted once by a fit that never saw it, so a
    few dozen courses still give a stable score), then one fit on all of them. Two scores: each hull's ln time, and its
    STANDING (ln time minus the field's mean ln time on that course), which is what a pool cancels. None with too few
    records."""
    valid = [r for r in records if usable(r)]
    if len(valid) < min_n:
        return None
    np = _np()
    rng = random.Random(seed)
    idx = list(range(len(valid)))
    rng.shuffle(idx)
    names = feature_names(valid)
    hulls = [h for h in HULLS if sum(1 for r in valid if r["seconds"].get(h)) >= 3]
    scored = [h for h in SCORED if h in hulls]
    full = Surrogate(names).fit(valid, hulls=hulls)
    pred = {}                                    # record index -> {hull: predicted seconds}, out of fold
    for k in range(folds):
        te = [i for j, i in enumerate(idx) if j % folds == k]
        tr = [i for j, i in enumerate(idx) if j % folds != k]
        if not te or len(tr) < 3:
            continue
        sk = Surrogate(names).fit([valid[i] for i in tr], hulls=hulls, alpha=full.alpha)
        for i in te:
            pred[i] = sk.predict(valid[i]["x"])
    per = {}
    for h in scored:
        rows = [(math.log(pred[i][h]), math.log(valid[i]["seconds"][h])) for i in pred
                if valid[i]["seconds"].get(h) and h in pred[i]]
        if len(rows) < 3:
            continue
        p, y = np.array([a for a, _ in rows]), np.array([b for _, b in rows])
        ss = float(((y - y.mean()) ** 2).sum())
        per[h] = {"r2": round(1.0 - float(((p - y) ** 2).sum()) / ss, 3) if ss > 1e-12 else None,
                  "mae_s": round(float(np.abs(np.exp(p) - np.exp(y)).mean()), 2), "n": len(rows)}
    fulls = [i for i in pred if all(valid[i]["seconds"].get(h) for h in scored) and all(h in pred[i] for h in scored)]
    stand, sp, so, right = {h: ([], []) for h in scored}, [], [], 0
    for i in fulls:
        lp = {h: math.log(pred[i][h]) for h in scored}
        ly = {h: math.log(valid[i]["seconds"][h]) for h in scored}
        mp, my = sum(lp.values()) / len(lp), sum(ly.values()) / len(ly)
        for h in scored:
            stand[h][0].append(lp[h] - mp)
            stand[h][1].append(ly[h] - my)
        sp.append(max(lp.values()) - min(lp.values()))
        so.append(max(ly.values()) - min(ly.values()))
        right += min(lp, key=lp.get) == min(ly, key=ly.get)

    def r2(p, y):
        p, y = np.array(p), np.array(y)
        ss = float(((y - y.mean()) ** 2).sum()) if len(y) >= 3 else 0.0
        return round(1.0 - float(((p - y) ** 2).sum()) / ss, 3) if ss > 1e-12 else None

    standing = {h: r2(*stand[h]) for h in scored}
    r2s = [v["r2"] for v in per.values() if v["r2"] is not None]
    st2 = [v for v in standing.values() if v is not None]
    full.held_out = {"per_hull": per, "mean_r2": round(sum(r2s) / len(r2s), 3) if r2s else None,
                     "standing_r2": standing, "mean_standing_r2": round(sum(st2) / len(st2), 3) if st2 else None,
                     "spread_r2": r2(sp, so), "leader_right": round(right / len(fulls), 3) if fulls else None,
                     "n": len(fulls), "folds": folds, "courses": len(valid)}
    return full


# ── the plain-words report: what the equations say ───────────────────────────────────────────────────────────────

STEPS = {"lap_u": (500.0, "500 u more lap"), "lane_over_spine": (0.05, "lanes 5% longer than the spine (more braid / detour)"),
         "max_turn": (20.0, "a hardest corner 20 deg sharper"), "mean_turn": (10.0, "corners 10 deg sharper on average"),
         "rms_turn": (10.0, "10 deg more rms corner"), "hard_corners": (1.0, "one more corner over 60 deg"),
         "min_mouth": (-20.0, "the tightest ring mouth 20 u smaller"), "corner_floor": (-50.0, "a corner floor 50 u tighter"),
         "ridable": (-0.1, "a tenth less ridable rail"), "neutral": (0.1, "a tenth of the lap in no team's colour"),
         "gap_share": (0.05, "5% of the lap as rail gaps"), "plain": (0.1, "a tenth of the lap as plain (cuttable) rail"),
         "shielded": (0.1, "a tenth of the lap as shielded (not super-shielded) rail"), "gap_count": (1.0, "one more rail gap"),
         "free_stretches": (1.0, "one more stretch off the rail"), "detour_u": (100.0, "100 u more rail detour"),
         "alternates": (1.0, "one more alternate ring"), "alt_offset": (100.0, "alternate rings 100 u further out"),
         "crystals": (1.0, "one more crystal on the line"),
         **{f"fam:{f}": (1.0, f"one more placement from the {f} family") for f in list(FAMILIES) + ["other"]}}


def reference_seconds(recs):
    """Each hull's median time over the courses it finished: the reference the equations' seconds are read at. (A
    stage-1 record has no time for the hulls it never raced, so no single record is the reference.)"""
    ref = {}
    for h in HULLS:
        ts = sorted(r["seconds"][h] for r in recs if r.get("valid") and r["seconds"].get(h) is not None)
        if ts:
            ref[h] = ts[len(ts) // 2]
    return ref


def effects(s, ref_seconds):
    """{feature: {hull: seconds}}: what one STEPS step of each feature does to each hull's race at the reference course
    (the model is linear in ln time, so the step multiplies the hull's time by exp(b * step / sd))."""
    out = {}
    for j, n in enumerate(s.names):
        step = STEPS.get(n, (1.0, f"one more {n.split(':', 1)[-1]}"))[0]
        out[n] = {h: round(ref_seconds[h] * (math.exp(float(b[j]) * step / float(s.sd[j])) - 1.0), 2)
                  for h, b in s.coef.items() if h in ref_seconds}
    return out


def _say(h, v):
    if abs(v) < 0.5:
        return f"barely moves the {h} ({v:+.1f} s)"
    return f"{'slows' if v > 0 else 'speeds up'} the {h} by {abs(v):.1f} s"


def equations_md(s, ref_seconds, records, backend):
    hulls = [h for h in HULLS if h in s.coef]
    scored = [h for h in SCORED if h in s.coef]
    eff = effects(s, ref_seconds)
    ho = s.held_out
    n = sum(1 for r in records if r.get("valid"))
    lines = [f"# What the Regatta course search learned ({time.strftime('%Y-%m-%d')})", "",
             "Plan step: `time-arena-course`. Regatta flywheel round 1, worker W3. Written by `Tools/Build/regatta_qd.py`.", "",
             (f"**Every number here is a MODEL's** (backend `model`: the measured roster, each hull anchored at its "
              f"2026-11-03 editor race and moved by the offline models). The equations are only as true as the judge: "
              f"re-fit them on the engine judge's races before acting on them." if backend == "model" else
              f"**Judge: the game's own code** (Amoebius headless, every hull's shipped AI, alone), for "
              f"{', '.join(hulls)} only; {n} courses."), "",
             "## The equation", "",
             "For every hull h: `ln(seconds_h) = c_h + sum over features of b_hf x (feature - mean) / sd`, a ridge "
             f"regression (penalty {s.alpha}) fitted on {n} judged courses. Each b is a multiplier on that hull's time, so "
             "the table below is simply what one step of each feature adds to each hull's race.", "",
             "## Can it be trusted? (courses it never saw)", "",
             f"Mean R^2 of ln(time) over the scored hulls: **{ho.get('mean_r2')}** (it may screen the search at >= "
             f"{TRUST_R2}); R^2 of the course's ln(spread): **{ho.get('spread_r2')}**; it names the right fastest hull on "
             f"**{ho.get('leader_right')}** of {ho.get('n')} held-out courses.", "",
             f"Each course is predicted by a fit that never saw it ({ho.get('folds', 1)}-fold cross-validation over "
             f"{ho.get('courses', n)} courses). STANDING is a hull's ln time minus the field's mean on that course: the "
             f"quantity a pool cancels, so its R^2 is the one that says whether the equations can steer a pool (mean "
             f"**{ho.get('mean_standing_r2')}**).", "",
             "| hull | held-out R^2, time | R^2, standing | mean error (s) |", "|---|---|---|---|"]
    for h, v in sorted(ho.get("per_hull", {}).items()):
        lines.append(f"| {h} | {v['r2']} | {(ho.get('standing_r2') or {}).get(h)} | {v['mae_s']} |")
    lines += ["", "## What each feature does to each hull (seconds, against today's course)", "",
              "| change | " + " | ".join(hulls) + " |", "|---|" + "---|" * len(hulls)]
    order = sorted(eff, key=lambda k: -max([abs(v) for v in eff[k].values()] or [0]))
    label = lambda k: STEPS.get(k, (1.0, f"one more {k.split(':', 1)[-1]}"))[1]
    for k in order:
        lines.append(f"| {label(k)} | " + " | ".join(f"{eff[k].get(h, 0):+.1f}" for h in hulls) + " |")
    lines += ["", "## In plain words", ""]
    for k in order[:8]:
        e = {h: v for h, v in eff[k].items() if h in scored}
        if not e:
            continue
        ranked = sorted(e.items(), key=lambda kv: kv[1])
        hi, lo = ranked[-1], ranked[0]
        lines.append(f"- **{label(k).capitalize()}** {_say(*hi)} and {_say(*lo)}"
                     + (f"; the {', '.join(h for h, _ in ranked[1:-1])} sit between." if len(ranked) > 2 else ".")
                     + (" That gap is a balancing lever." if hi[1] - lo[1] >= 3.0 else ""))
    present = [h for h in scored if h in ref_seconds and ref_seconds[h]]
    if len(present) >= 2:
        fast = min(present, key=lambda h: ref_seconds[h])
        rest = [h for h in present if h != fast]
        gap = lambda k: eff[k].get(fast, 0) - sum(eff[k].get(h, 0) for h in rest) / len(rest)
        lines += ["", f"## The levers against today's leader ({fast})", "",
                  f"Ranked by how much more one step slows the {fast} than the average of the others:", ""]
        for k in sorted(order, key=lambda k: -gap(k))[:5]:
            lines.append(f"- {label(k)}: the {fast} {eff[k].get(fast, 0):+.1f} s, the others "
                         f"{sum(eff[k].get(h, 0) for h in rest) / len(rest):+.1f} s on average")
    return "\n".join(lines) + "\n"


# ── MAP-Elites, with or without the surrogate screen ─────────────────────────────────────────────────────────────

def _pool(workers):
    import multiprocessing as mp
    return mp.get_context("fork").Pool(workers) if workers > 1 else None


def _map(pool, fn, items):
    return pool.map(fn, items, chunksize=1) if pool else [fn(i) for i in items]


def qd_score(archive, cap=1.0):
    """Sum over filled cells of max(0, cap - loss): more cells and better cells both raise it."""
    return round(sum(max(0.0, cap - r["loss"]["loss"]) for r in archive.values()), 3)


def insert(archive, r):
    if not r.get("valid"):
        return False
    cur = archive.get(r["cell"])
    if cur is None or r["loss"]["loss"] < cur["loss"]["loss"]:
        archive[r["cell"]] = r
        return True
    return False


def predicted(s, x, guard):
    """The surrogate's guess at a course's loss and leader (the spread and length terms; guards known exactly)."""
    p = s.predict(x)
    sc = {h: p[h] for h in SCORED if h in p}
    lead = min(sc, key=sc.get)
    loss = math.log(max(sc.values()) / min(sc.values())) + L.GUARD_WEIGHT * guard
    return loss, lead


def run(budget, out, backend="model", surrogate=True, seed=1, batch=16, init=64, workers=None, log=print):
    """MAP-Elites with `budget` judge calls; with `surrogate`, a trusted surrogate screens SCREEN x batch offspring per
    generation. Writes records.jsonl, archive.json, surrogate.json, EQUATIONS.md, pool.json and summary.json into `out`."""
    os.makedirs(out, exist_ok=True)
    rng = random.Random(seed)
    rand_g, mut, cross, _ = ops()
    workers = workers or max(1, (os.cpu_count() or 2))
    pool = _pool(workers)
    t0 = time.time()
    archive, records, trace = {}, [], []
    s = None
    screened_calls = 0
    live = None
    try:
        seeds = (0,) if backend == "model" else ENGINE_SEEDS
        jobs = [(today(), backend, seeds)] + [(rand_g(rng), backend, seeds) for _ in range(min(init, budget) - 1)]
        live = open(os.path.join(out, "records.live.jsonl"), "w", encoding="utf-8")
        for r in (pool.imap(evaluate, jobs) if pool else map(evaluate, jobs)):
            records.append(r)
            insert(archive, r)
            live.write(json.dumps(r) + "\n")
            live.flush()
            if backend != "model":
                log(f"[{backend}] {len(records)}/{budget}: " + (", ".join(f"{h} {t:.0f}" for h, t in sorted(
                    r["seconds"].items(), key=lambda kv: kv[1] or 1e9) if t) + f"  spread {r['loss']['spread_x']}x"
                    if r.get("valid") else r.get("error", "invalid")))
        while len(records) < budget:
            elites = list(archive.values())
            n = min(batch, budget - len(records))
            if surrogate and len(records) >= 60 and (s is None or len(records) % (4 * batch) < batch):
                s = fit_trusted(records, seed=len(records))
            use = surrogate and s is not None and s.trusted()
            k = n * (SCREEN if use else 1)
            kids = []
            for _ in range(k):
                if elites and rng.random() < 0.9:
                    a = rng.choice(elites)["genome"]
                    child = cross(a, rng.choice(elites)["genome"], rng) if rng.random() < 0.3 else a
                    kids.append(mut(child, rng))
                else:
                    kids.append(rand_g(rng))
            if use:
                feats = _map(pool, featurize, kids)
                scored_kids = []
                for kid, (gg, x, guard) in zip(kids, feats):
                    if gg is None:
                        continue
                    ploss, lead = predicted(s, x, guard)
                    cell = cell_of(SCORED.index(lead), x, len(kinds_used(gg, x)))
                    cur = archive.get(cell)
                    gain = (1.0 + max(0.0, 1.0 - ploss)) if cur is None else cur["loss"]["loss"] - ploss
                    scored_kids.append((gain, kid))
                scored_kids.sort(key=lambda t: -t[0])
                n_explore = int(round(EXPLORE * n))
                chosen = [kid for _, kid in scored_kids[:n - n_explore]]
                rest = [kid for _, kid in scored_kids[n - n_explore:]]
                chosen += rng.sample(rest, min(n_explore, len(rest)))
                screened_calls += len(chosen)
            else:
                chosen = kids
            for r in _map(pool, evaluate, [(g, backend, seeds) for g in chosen]):
                records.append(r)
                insert(archive, r)
                live.write(json.dumps(r) + "\n")
                live.flush()
            passing = sum(1 for r in archive.values() if r["loss"]["passes"])
            trace.append({"judged": len(records), "cells": len(archive), "passing_cells": passing,
                          "qd": qd_score(archive), "screen": bool(use),
                          "surrogate_r2": s.held_out.get("mean_r2") if s else None, "t": round(time.time() - t0, 1)})
            if len(trace) % 5 == 0 or len(records) >= budget or backend != "model":
                log(f"[{backend}{' +screen' if surrogate else ''}] judged {len(records)}/{budget}: {len(archive)} cells, "
                    f"{passing} passing, QD {trace[-1]['qd']}, surrogate R2 {trace[-1]['surrogate_r2']}, "
                    f"{trace[-1]['t']} s")
    finally:
        if pool:
            pool.close()
        if live:
            live.close()
    s = fit_trusted(records, seed=seed) or s
    ref = next(r for r in records if r.get("valid"))["seconds"]
    with open(os.path.join(out, "records.jsonl"), "w", encoding="utf-8") as fh:
        for r in records:
            fh.write(json.dumps(r) + "\n")
    json.dump({"backend": backend, "cells": archive, "model": backend == "model"}, open(os.path.join(out, "archive.json"), "w"),
              indent=1)
    if s:
        json.dump(s.to_json(), open(os.path.join(out, "surrogate.json"), "w"), indent=1)
        open(os.path.join(out, "EQUATIONS.md"), "w").write(equations_md(s, ref, records, backend))
    open(os.path.join(out, "SEARCH.md"), "w").write(search_report(records, backend, s))
    pool_report = pick_pool(list(archive.values()) + top_passing(records, 60), out, backend)
    summary = {"backend": backend, "surrogate": surrogate, "budget": budget, "seed": seed, "judged": len(records),
               "valid": sum(1 for r in records if r.get("valid")), "cells": len(archive),
               "passing_cells": sum(1 for r in archive.values() if r["loss"]["passes"]), "qd": qd_score(archive),
               "best_spread": min((r["loss"]["spread_x"] for r in records if r.get("valid") and r["loss"].get("spread_x")),
                                  default=None),
               "leaders_in_archive": sorted({r["loss"]["fastest"] for r in archive.values() if r["loss"].get("fastest")}),
               "today": {"seconds": ref, "loss": records[0]["loss"]}, "surrogate_held_out": s.held_out if s else None,
               "screened_judge_calls": screened_calls, "pool_verdict": pool_report.get("verdict"),
               "pool_ratio": (pool_report.get("pool_loss") or {}).get("ratio"), "wall_s": round(time.time() - t0, 1),
               "trace": trace, "model": "every number a MODEL (the measured roster)" if backend == "model" else backend}
    json.dump(summary, open(os.path.join(out, "summary.json"), "w"), indent=1)
    return summary


def top_passing(records, n):
    ok = [r for r in records if r.get("valid") and r["loss"]["passes"]]
    return sorted(ok, key=lambda r: r["loss"]["loss"])[:n]


def pick_pool(cands, out, backend, min_k=4, max_k=8):
    """regatta_pool_pick.pick over the candidates, with each pool entry's GENOME attached; pool.json in `out`."""
    import regatta_pool_pick as pp
    uniq, seen = [], set()
    for r in cands:
        key = json.dumps(r["game_genome"], sort_keys=True)
        if key not in seen:
            seen.add(key)
            uniq.append(r)
    pc = []
    for i, r in enumerate(uniq):
        x, lo = r["x"], r["loss"]
        pc.append({"params": {"id": float(i)}, "source": f"{backend} record {i} cell {r['cell']}",
                   "descriptors": {"sharpness": x["max_turn"], "mean_turn": x["mean_turn"], "ridable": x["ridable"],
                                   "race_length": lo.get("race_length", 1.0), "mouth": x["min_mouth"],
                                   "gaps": x["gap_count"], "crystals": x["crystals"]},
                   "metrics": {"seconds": {h: v for h, v in r["seconds"].items() if v}, "chaos": lo.get("chaos", 0.0),
                               "rail_fails": lo.get("guard_fails", 0), "race_length": lo.get("race_length", 1.0),
                               "dnf": lo.get("dnf", []), "scored": SCORED, "generator_seed": r["game_genome"]["seed"],
                               "settings": r["game_genome"].get("circuit") or {}}})
    a = argparse.Namespace(pass_spread=L.PASS_SPREAD, pass_chaos=L.PASS_CHAOS, cancel=CANCEL_BAR, min=min_k, max=max_k,
                           restarts=12, seed=1, scored=SCORED)
    rep = pp.pick(pc, a) if len(pc) >= min_k else {"pool": [], "verdict": f"NO pool: only {len(pc)} candidates"}
    chosen = rep.get("pool") or ((rep.get("closest") or {}).get("pool") or [])
    for e in chosen:
        r = uniq[int(e["params"]["id"])]
        e["genome"] = r["game_genome"]
        e["kinds"] = r["kinds"]
        e["cell"] = r["cell"]
    if chosen:
        rep["pool_loss"] = L.pool_loss([uniq[int(e["params"]["id"])]["loss"] for e in chosen], SCORED)
    rep["genome_version"] = jm.GENOME_VERSION
    rep["backend"] = backend
    rep["how_to_ship"] = ("each entry's `genome` is the shared-contract course genome; the course builder "
                          "(RegattaCourseGenome, W2) lays it, and author_regatta_assets.py turns a pool into rails "
                          "prefabs + cell configs + Regatta Course Pool entries (round 2).")
    json.dump(rep, open(os.path.join(out, "pool.json"), "w"), indent=1)
    return rep


# ── shards: many cloud threads search at once, one writer per file, one merge ──────────────────────────────────

def read_jsonl(path):
    """Every complete line of an append-only JSONL file (a half-written last line on a network mount is skipped)."""
    out = []
    try:
        with open(path, encoding="utf-8") as fh:
            for line in fh:
                try:
                    out.append(json.loads(line))
                except ValueError:
                    pass
    except FileNotFoundError:
        pass
    return out


def genome_key(g):
    """One string per distinct course (the genome without its free-text notes)."""
    return json.dumps({k: v for k, v in g.items() if k != "notes"}, sort_keys=True)


def dedupe(recs):
    """Each distinct course once: the race of a course judged twice (a restarted runner, two runners drawing the same
    seed course) is the same deterministic race, so the first full record wins."""
    out, at = [], {}
    for r in recs:
        key = genome_key(r["genome"]) if r.get("genome") else None
        if key is None:
            out.append(r)
        elif key not in at:
            at[key] = len(out)
            out.append(r)
        elif not out[at[key]].get("valid") and r.get("valid"):
            out[at[key]] = r
    return out


def shard_files(root):
    """Every shard's records file under `root` (root/shard_K/records.jsonl)."""
    if not os.path.isdir(root):
        return []
    return sorted(os.path.join(root, d, "records.jsonl") for d in os.listdir(root)
                  if d.startswith("shard_") and os.path.isfile(os.path.join(root, d, "records.jsonl")))


def shard(k, n, out, backend="engine", budget=1000, peers_every=5, refit_every=10, log=print):
    """One search runner. Writes ONLY out/records.jsonl (append-only, one line per judged course; it resumes from that
    file). Reads every sibling shard_* (read-only) so the elites, the surrogate and the gate are the whole team's.
    Its own seed stream (shard K of N). Steady state, one course at a time (the engine already fills the cores):
    parent = a random elite of the merged archive, mutated or crossed (W2's operators); with a trusted surrogate,
    SCREEN candidates are laid and the best predicted one is raced (a quarter of the time a random one, so the surrogate
    keeps being tested). The gate is the median full-course spread so far: a course whose stage-1 four are already
    wider is stopped there (half the cost) and kept as a partial record."""
    os.makedirs(out, exist_ok=True)
    root = os.path.dirname(os.path.abspath(out.rstrip("/")))
    mine = os.path.join(out, "records.jsonl")
    rand_g, mut, cross, _ = ops()
    seeds = (0,) if backend == "model" else ENGINE_SEEDS
    own = read_jsonl(mine)
    # The stream depends on how far this shard got and on its start condition, so a restarted runner (or an L5 runner
    # with the same K) never replays courses already raced.
    rng = random.Random(f"{k}/{n}/{START}/{len(own)}")
    peers, s, t0 = [], None, time.time()
    log(f"[shard {k}/{n}] {len(own)} courses already in {mine}")
    while len(own) < budget:
        if len(own) % peers_every == 0:
            peers = [r for f in shard_files(root) if os.path.abspath(f) != os.path.abspath(mine) for r in read_jsonl(f)
                     if r.get("start", "card") == START]      # another condition's courses are another archive
        every = own + peers
        archive = {}
        for r in every:
            insert(archive, r)
        if len([r for r in every if usable(r)]) >= 60 and (s is None or len(own) % refit_every == 0):
            s = fit_trusted(every, seed=len(every))
        full = sorted(r["loss"]["spread_x"] for r in every if r.get("valid") and r["loss"].get("spread_x"))
        gate = full[len(full) // 2] if len(full) >= 10 and backend != "model" else None
        if k == 0 and not every:
            g = today()
        elif len(archive) < 12 or rng.random() < 0.15:
            g = rand_g(rng)
        else:
            elites = list(archive.values())
            def child():
                a = rng.choice(elites)["genome"]
                return mut(cross(a, rng.choice(elites)["genome"], rng) if rng.random() < 0.3 else a, rng)
            if s is not None and s.trusted() and rng.random() > EXPLORE:
                best = None
                for c in (child() for _ in range(SCREEN)):
                    gg, x, guard = featurize(c)
                    if gg is None or guard:
                        continue
                    ploss, lead = predicted(s, x, guard)
                    cell = cell_of(SCORED.index(lead) if lead in SCORED else -1, x, len(kinds_used(gg, x)))
                    cur = archive.get(cell)
                    gain = (1.0 + max(0.0, 1.0 - ploss)) if cur is None else cur["loss"]["loss"] - ploss
                    if best is None or gain > best[0]:
                        best = (gain, c)
                g = best[1] if best else child()
            else:
                g = child()
        seen = {genome_key(r["genome"]) for r in every if r.get("genome")}
        for _ in range(8):                       # a course already raced is mutated until it is new
            if genome_key(g) not in seen:
                break
            g = mut(g, rng)
        r = evaluate((g, backend, seeds, gate))
        r.update(start=START, shard=k, at=time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()))
        with open(mine, "a", encoding="utf-8") as fh:
            fh.write(json.dumps(r) + "\n")
        own.append(r)
        msg = (f"spread {r['loss']['spread_x']}x" if r.get("valid") else r.get("error", "invalid"))
        log(f"[shard {k}/{n}] {len(own)}/{budget} (+{len(peers)} from peers, {len(archive)} cells, gate "
            f"{gate and round(gate, 2)}, surrogate {s and s.held_out.get('mean_r2')}): {msg}  {time.time() - t0:.0f} s")
    return own


def merge(root, out=None, log=print):
    """Every start condition in the shards gets its own merged folder (root/merged/<start>/) and summary."""
    out = out or os.path.join(root, "merged")
    recs = [r for f in shard_files(root) for r in read_jsonl(f)]
    starts = sorted({r.get("start", "card") for r in recs}) or ["card"]
    summaries = {}
    for st in starts:
        sub = [r for r in recs if r.get("start", "card") == st]
        summaries[st] = merge_records(sub, os.path.join(out, st), root, log=log)
    json.dump(summaries, open(os.path.join(out, "summary.json"), "w"), indent=1)
    return summaries


def merge_records(recs, out, root, log=print):
    """Fold one start condition's shard records into one archive, surrogate, report and pool in `out`. Reads the shards
    only; the one writer of the merged files is whoever runs this."""
    os.makedirs(out, exist_ok=True)
    if not recs:
        return {"courses": 0}
    n_raw = len(recs)
    recs = dedupe(recs)
    if len(recs) < n_raw:
        log(f"[merge] {n_raw - len(recs)} repeated courses folded ({len(recs)} distinct)")
    set_hulls(sorted({h for r in recs if r.get("valid") for h in r["seconds"]}) or HULLS)
    backend = next((r.get("backend") for r in recs if r.get("valid")), "engine")
    archive = {}
    for r in recs:
        insert(archive, r)
    s = fit_trusted(recs, min_n=20)
    json.dump({"backend": backend, "cells": archive, "shards": shard_files(root)},
              open(os.path.join(out, "archive.json"), "w"), indent=1)
    open(os.path.join(out, "SEARCH.md"), "w").write(search_report(recs, backend, s))
    if s:
        json.dump(s.to_json(), open(os.path.join(out, "surrogate.json"), "w"), indent=1)
        ref = reference_seconds(recs)
        open(os.path.join(out, "EQUATIONS.md"), "w").write(equations_md(s, ref, recs, backend))
    rep = pick_pool(list(archive.values()) + top_passing(recs, 60), out, backend)
    summary = {"courses": len(recs), "raced_full": sum(1 for r in recs if r.get("valid")),
               "stopped_stage1": sum(1 for r in recs if r.get("partial")), "cells": len(archive),
               "shards": len(shard_files(root)), "surrogate": s.held_out if s else None,
               "pool_verdict": rep.get("verdict")}
    json.dump(summary, open(os.path.join(out, "summary.json"), "w"), indent=1)
    log(json.dumps(summary, indent=1))
    return summary


# ── the search report: how far the course alone closes the field, and who stays an outlier ──────────────────────

def _pct(xs, q):
    xs = sorted(xs)
    return xs[min(len(xs) - 1, int(round(q * (len(xs) - 1))))] if xs else None


def search_report(records, backend, surrogate=None):
    """SEARCH.md: the spread distribution, each hull's standing across every raced course (its time over the field's
    geometric mean), how often it wins and trails, the best courses, and (with a surrogate) which kinds move which hull.
    A hull that is slowest, or fastest, on nearly every course is the loop's step-5 signal: the course cannot fix it."""
    ok = [r for r in records if r.get("valid") and r["loss"].get("spread_x")]
    if not ok:
        return "# Regatta search\n\nNo course finished.\n"
    # Every scored hull that finished on most raced courses (a stage-1 record carries only four of them).
    hulls = [h for h in SCORED if sum(1 for r in ok if r["seconds"].get(h)) >= 0.5 * len(ok)]
    full = [r for r in ok if all(r["seconds"].get(h) for h in hulls)] or ok
    ok = [r for r in ok if all(r["seconds"].get(h) for h in hulls)] or ok
    spreads = [r["loss"]["spread_x"] for r in ok]
    def plain(r):     # today's course: no placements, flora, features or circuit overrides
        g = r.get("genome") or {}
        f = g.get("features") or {}
        return not (g.get("placements") or g.get("flora") or f.get("Sections") or f.get("AlternateGates")
                    or (f.get("Crystals") or {}).get("PerLap") or g.get("circuit")) and int(g.get("intensity", 1)) == 1
    today = next((r for r in ok if plain(r)), ok[0])
    lines = [f"# Regatta course search ({time.strftime('%Y-%m-%d')}, backend `{backend}`)", "",
             f"{len(records)} genomes judged, {len(ok)} raced to a result, {len(records) - len(ok)} not raced or not "
             f"finished (guard fails, DNFs). Hulls scored: {', '.join(hulls)}.", "",
             "## How far the course alone closes the field", "",
             f"Today's course: **{today['loss']['spread_x']:.2f}x** (slowest / fastest). Over the searched courses: best "
             f"**{min(spreads):.2f}x**, 10th percentile {_pct(spreads, 0.1):.2f}x, median {_pct(spreads, 0.5):.2f}x, "
             f"90th {_pct(spreads, 0.9):.2f}x. Passing (<= {L.PASS_SPREAD}x and every guard): "
             f"{sum(1 for r in ok if r['loss']['passes'])}.", "",
             "## Each hull across every course", "",
             "Standing = the hull's time over the geometric mean of the field on that course (1.00 = mid-field).", "",
             "| hull | best standing | median | worst | fastest on | slowest on | today |", "|---|---|---|---|---|---|---|"]
    for h in hulls:
        st = []
        for r in full:
            gm = math.exp(sum(math.log(r["seconds"][x]) for x in hulls) / len(hulls))
            st.append(r["seconds"][h] / gm)
        first = sum(1 for r in full if min(hulls, key=lambda x: r["seconds"][x]) == h)
        last = sum(1 for r in full if max(hulls, key=lambda x: r["seconds"][x]) == h)
        gm0 = math.exp(sum(math.log(today["seconds"][x]) for x in hulls) / len(hulls))
        lines.append(f"| {h} | {min(st):.2f} | {_pct(st, 0.5):.2f} | {max(st):.2f} | {first}/{len(full)} | "
                     f"{last}/{len(full)} | {today['seconds'][h] / gm0:.2f} |")
    outl = []
    for h in hulls:
        st = [r["seconds"][h] / math.exp(sum(math.log(r["seconds"][x]) for x in hulls) / len(hulls)) for r in full]
        if min(st) > 1.15:
            outl.append(f"the **{h}** is slower than mid-field on every course (best {min(st):.2f})")
        elif max(st) < 0.87:
            outl.append(f"the **{h}** is faster than mid-field on every course (worst {max(st):.2f})")
    lines += ["", "## Outliers the course cannot fix (step 5: the vessel or its AI)", ""]
    lines += [f"- {o}." for o in outl] or ["- none: every hull is mid-field or better on some course."]
    lines += ["", "## The best courses", "", "| spread | fastest | slowest | kinds |", "|---|---|---|---|"]
    for r in sorted(ok, key=lambda r: r["loss"]["loss"])[:10]:
        lines.append(f"| {r['loss']['spread_x']:.2f}x | {r['loss']['fastest']} | {r['loss']['slowest']} | "
                     f"{', '.join(r.get('kinds') or [])} |")
    return "\n".join(lines) + "\n"


# ── the engine pass: a designed set of courses raced in the game's own code ───────────────────────────────────────

def design(n, seed=1):
    """`n` features-only genomes on today's circuit and seed (the engine reads no circuit until W2's RegattaCourseGenome
    lands): today's course, each feature kind alone at two strengths (so each one's effect can be read), then random
    mixes. Every one is a course the model and the engine can both race."""
    rng = random.Random(seed)
    base = jm.today_genome()

    def sec(leg, lo, hi, kind=0, neutral=False, detour=0.0, bearing=0.0, gap=None):
        s = {"Leg": leg, "From": lo, "To": hi, "Kind": kind, "Neutral": neutral, "Detour": detour, "DetourBearing": bearing}
        if gap:
            s["GapU"] = gap
        return s

    def with_(sections=(), alts=(), crystals=0):
        g = copy.deepcopy(base)
        g["features"]["Sections"] = [dict(x) for x in sections]
        g["features"]["AlternateGates"] = [dict(x) for x in alts]
        g["features"]["Crystals"] = {"PerLap": crystals, "Phase": 0.3}
        return repair(g)

    out = [("today", base)]
    for share in (0.15, 0.4):
        out.append((f"neutral {share}", with_([sec(-1, 0.5 - share / 2, 0.5 + share / 2, neutral=True)])))
    for legs in (2, 6):
        out.append((f"gaps x{legs}", with_([sec(k * 8 // legs, 0.5, 0.5, kind=3, gap=80.0) for k in range(legs)])))
    for d in (60.0, 140.0):
        out.append((f"detour {d:.0f}u x4", with_([sec(k, 0.3, 0.7, detour=d, bearing=90.0 * k) for k in (0, 2, 4, 6)])))
    for kind, name in ((2, "plain"), (1, "shielded")):
        out.append((f"{name} 0.5", with_([sec(-1, 0.25, 0.75, kind=kind)])))
    for k in (2, 5):
        out.append((f"alternates x{k}", with_(alts=[{"Ring": r, "Offset": 160.0, "Bearing": 45.0 * r, "RadiusScale": 1.0}
                                                   for r in range(k)])))
    for c in (2, 4):
        out.append((f"crystals {c}", with_(crystals=c)))
    while len(out) < n:
        g = random_genome(rng)
        g["circuit"], g["seed"] = copy.deepcopy(base["circuit"]), base["seed"]
        if not g["features"]["Sections"]:       # alternates and crystals alone barely move these hulls: no race wasted
            continue
        out.append((f"mix {len(out)}", repair(g)))
    return out[:n]


def engine_pass(n, out, backend="mixed", seed=1, log=print):
    """Race `n` designed courses with `backend` and the model side by side; records.jsonl holds both timings, and
    ENGINE_VS_MODEL.md says, per feature, whether the model's price agrees with the game's."""
    os.makedirs(out, exist_ok=True)
    rows = []
    path = os.path.join(out, "records.jsonl")
    done = {}
    if os.path.exists(path):          # resumable: an engine pass is slow
        for line in open(path, encoding="utf-8"):
            r = json.loads(line)
            done[r["name"]] = r
    with open(path, "a", encoding="utf-8") as fh:
        for name, g in design(n, seed):
            if name in done:
                rows.append(done[name])
                continue
            t0 = time.time()
            r = evaluate((g, backend, ENGINE_SEEDS))
            if not r.get("valid"):
                r.setdefault("seconds", {})
            gg, course = lay_genome(g)
            m = jm.judge_course(course, [{"hull": h} for h in HULLS], [1])
            r["model_seconds"] = {s["hull"]: s["finish_s"] for s in m["races"][0]["seats"]}
            r["name"] = name
            rows.append(r)
            fh.write(json.dumps(r) + "\n")
            fh.flush()
            log(f"[engine pass] {len(rows)}/{n} {name}: " + ", ".join(
                f"{h} {r['seconds'].get(h)} (model {r['model_seconds'].get(h)})" for h in ENGINE_TRUSTED)
                + f"  {time.time() - t0:.0f} s")
    return engine_report(rows, out)


def engine_report(rows, out):
    """ENGINE_VS_MODEL.md, and (enough engine courses) surrogate.json + EQUATIONS.md fitted on the engine's races only."""
    open(os.path.join(out, "ENGINE_VS_MODEL.md"), "w").write(engine_vs_model_md(rows))
    eng = engine_only(rows)
    s = fit_trusted(eng, min_n=20) if eng else None
    if s:
        today = next(r for r in rows if r["name"] == "today")["seconds"]
        json.dump(s.to_json(), open(os.path.join(out, "surrogate.json"), "w"), indent=1)
        open(os.path.join(out, "EQUATIONS.md"), "w").write(equations_md(s, today, eng, "engine"))
    return rows


def engine_vs_model_md(rows):
    today = next(r for r in rows if r["name"] == "today")
    tb = today.get("timed_by") or {}
    eng = [h for h in HULLS if tb.get(h) == "engine"]
    lines = ["# Regatta course features: the game's own code against the model", "",
             f"Each course raced once per hull, alone, Legendary AI, I1 circuit and seed (the engine reads no circuit "
             f"yet). Engine = Amoebius headless (W1's judge) for {', '.join(eng)}, the hulls whose port "
             f"flight matches the editor; model = regatta_judge_model. Change = seconds against today's course.", "",
             "| course | " + " | ".join(f"{h} game | {h} model" for h in eng) + " |",
             "|---|" + "---|---|" * len(eng)]

    def d(r, h, key):
        a, b = r[key].get(h), today[key].get(h)
        return "DNF" if a is None else f"{a:.1f} ({a - b:+.1f})" if b else f"{a:.1f}"
    for r in rows:
        lines.append(f"| {r['name']} | " + " | ".join(f"{d(r, h, 'seconds')} | {d(r, h, 'model_seconds')}"
                                                      for h in eng) + " |")
    agree, n = 0, 0
    for r in rows:
        for h in eng:
            a, b = r["seconds"].get(h), r["model_seconds"].get(h)
            a0, b0 = today["seconds"].get(h), today["model_seconds"].get(h)
            if None in (a, b, a0, b0) or r is today:
                continue
            da, db = a - a0, b - b0
            n += 1
            agree += (abs(da) < 2 and abs(db) < 2) or (da * db > 0 and 0.5 <= (da / db if db else 9) <= 2)
    lines += ["", f"The model gets the sign and the size (within 2x, or both under 2 s) of **{agree} of {n}** changes."]
    return "\n".join(lines) + "\n"


# ── commands ─────────────────────────────────────────────────────────────────────────────────────────────────────

def compare(budget, out, backend="model", seed=1, log=print):
    a = run(budget, os.path.join(out, "plain"), backend, surrogate=False, seed=seed, log=log)
    b = run(budget, os.path.join(out, "screened"), backend, surrogate=True, seed=seed, log=log)
    keys = ("judged", "cells", "passing_cells", "qd", "best_spread", "pool_ratio", "wall_s")
    rep = {k: {"plain": a[k], "screened": b[k]} for k in keys}
    rep["surrogate_held_out"] = b["surrogate_held_out"]
    json.dump(rep, open(os.path.join(out, "compare.json"), "w"), indent=1)
    return rep


def self_test():
    bad = []
    rng = random.Random(7)
    # Operators: today's genome survives repair unchanged; random and mutated genomes stay in range; crossover by leg.
    today = jm.today_genome()
    r0 = repair(copy.deepcopy(today))
    if r0["features"] != today["features"] or abs(sum(r0["circuit"]["CornerProfile"]) - 360.0) > 1e-6:
        bad.append("regatta_qd: repair changes today's genome")
    gg, course = lay_genome(today)
    import regatta_features as rf
    if course["spineLength"] != jm.lay(today)["spineLength"] or not rf.is_empty(gg["features"]):
        bad.append("regatta_qd: the empty genome does not lay today's course")
    laid = 0
    for _ in range(12):
        g = mutate(random_genome(rng), rng)
        for k, (lo, hi) in CIRCUIT.items():
            if not lo <= g["circuit"][k] <= hi:
                bad.append(f"regatta_qd: a mutated genome has {k}={g['circuit'][k]} outside [{lo}, {hi}]")
        if featurize(g)[0] is not None:
            laid += 1
    if laid < 8:
        bad.append(f"regatta_qd: only {laid} of 12 random genomes lay a course")
    # The surrogate recovers a planted linear law, and (negative control) a law of pure noise is NOT trusted.
    recs = []
    for i in range(200):
        x = {n: rng.random() for n in FEATURES}
        recs.append({"valid": True, "x": x, "seconds": {h: 60.0 * math.exp(0.3 * x["neutral"] * (2.0 if h == "Urchin" else 0.1)
                                                                         + 0.1 * x["max_turn"] + rng.gauss(0, 0.002))
                                                        for h in HULLS}})
    s = fit_trusted(recs)
    if not (s and s.trusted()):
        bad.append(f"regatta_qd: the surrogate does not recover a planted law ({s and s.held_out})")
    else:
        e = effects(s, {h: 60.0 for h in HULLS})
        if not e["neutral"]["Urchin"] > 5 * abs(e["neutral"]["Manta"]):
            bad.append(f"regatta_qd: the planted 'neutral slows the Urchin' law is not what the equations report ({e['neutral']})")
    noise = [dict(r, seconds={h: 60.0 * math.exp(rng.gauss(0, 0.2)) for h in HULLS}) for r in recs]
    sn = fit_trusted(noise)
    if sn and sn.trusted():
        bad.append(f"regatta_qd: a surrogate fitted to noise is trusted ({sn.held_out})")
    # A tiny end-to-end run on the model: an archive, a pool file, a summary.
    import tempfile
    with tempfile.TemporaryDirectory() as d:
        summ = run(24, d, surrogate=False, batch=8, init=16, workers=2, log=lambda *_: None)
        if summ["cells"] < 3 or not os.path.exists(os.path.join(d, "pool.json")):
            bad.append(f"regatta_qd: a 24-call run fills {summ['cells']} cells / writes no pool.json")
    return bad


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("cmd", nargs="?", choices=("run", "compare", "equations", "engine-pass", "engine-report", "report",
                                              "shard", "merge"))
    ap.add_argument("--shard", type=int, default=0, help="shard: this runner's index K (0-based)")
    ap.add_argument("--of", type=int, default=1, help="shard: how many runners N")
    ap.add_argument("--start", default=None, choices=("card", "rest", "L5"),
                    help="the start condition every hull races under (default card, or REGATTA_START)")
    ap.add_argument("dirs", nargs="*")
    ap.add_argument("--budget", type=int, default=1500)
    ap.add_argument("--out", default=None, help="output folder (merge: default ROOT/merged; else regatta_qd_out)")
    ap.add_argument("--backend", default="model", choices=("model", "engine", "mixed"))
    ap.add_argument("--n", type=int, default=40, help="engine-pass: how many designed courses")
    ap.add_argument("--no-surrogate", action="store_true")
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--batch", type=int, default=16)
    ap.add_argument("--workers", type=int, default=None)
    ap.add_argument("--hulls", default=None, help="comma list raced and scored (default: every hull on the model, the "
                    "trusted engine roster ENGINE_TRUSTED on engine / mixed)")
    ap.add_argument("--init", type=int, default=64)
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args()
    if a.hulls:
        set_hulls([h for h in a.hulls.split(",") if h])
    elif a.backend == "engine":
        set_hulls(list(ENGINE_TRUSTED))
    if a.workers is None and a.backend != "model":
        a.workers = 1            # the engine judge already runs one process per hull in parallel
    if a.start:
        global START
        START = a.start
    if a.cmd != "merge" and a.out is None:
        a.out = "regatta_qd_out"
    if a.self_test or not a.cmd:
        problems = self_test()
        print("\n".join(problems) or "regatta_qd --self-test: ok")
        sys.exit(1 if problems else 0)
    if a.cmd == "run":
        s = run(a.budget, a.out, a.backend, not a.no_surrogate, a.seed, a.batch, init=a.init, workers=a.workers)
        print(json.dumps({k: v for k, v in s.items() if k != "trace"}, indent=1))
    elif a.cmd == "engine-pass":
        engine_pass(a.n, a.out, a.backend if a.backend != "model" else "mixed", a.seed)
    elif a.cmd == "shard":
        shard(a.shard, a.of, a.out, a.backend, a.budget)
    elif a.cmd == "merge":
        merge(a.dirs[0], a.out)
    elif a.cmd == "report":
        recs = []
        for d in a.dirs:
            f = os.path.join(d, "records.jsonl")
            f = f if os.path.exists(f) else os.path.join(d, "records.live.jsonl")
            recs += [json.loads(l) for l in open(f, encoding="utf-8")]
        backend = next((r.get("backend") for r in recs if r.get("valid")), a.backend)
        hulls = sorted({h for r in recs if r.get("valid") for h in r["seconds"]})
        set_hulls(hulls)
        os.makedirs(a.out, exist_ok=True)
        md = search_report(recs, backend)
        open(os.path.join(a.out, "SEARCH.md"), "w").write(md)
        s = fit_trusted([r for r in recs if r.get("valid")], min_n=20)
        if s:
            ref = reference_seconds(recs)
            open(os.path.join(a.out, "EQUATIONS.md"), "w").write(equations_md(s, ref, recs, backend))
        print(md)
    elif a.cmd == "engine-report":
        rows = [json.loads(l) for d in a.dirs for l in open(os.path.join(d, "records.jsonl"), encoding="utf-8")]
        engine_report(rows, a.out)
        print(open(os.path.join(a.out, "ENGINE_VS_MODEL.md")).read())
    elif a.cmd == "compare":
        print(json.dumps(compare(a.budget, a.out, a.backend, a.seed), indent=1))
    else:
        recs = [json.loads(l) for d in a.dirs for l in open(os.path.join(d, "records.jsonl"), encoding="utf-8")]
        s = fit_trusted(recs)
        ref = reference_seconds(recs)
        backend = next(r for r in recs if r.get("valid")).get("backend", "model")
        md = equations_md(s, ref, recs, backend)
        open(os.path.join(a.out, "EQUATIONS.md") if a.out else "EQUATIONS.md", "w").write(md)
        print(md)


if __name__ == "__main__":
    main()
