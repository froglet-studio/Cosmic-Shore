"""STAGE 3 of the Regatta balance plan: the SMALLEST elemental tuning that closes the gap the course could not.

One Time level per hull (the one element that reaches a hull's speed: Soar on the Manta, the boost on the Sparrow and
the Serpent, the throttle ceiling on the Scarab, the charge rate on the Dolphin, the Stoat's pair; the Rhino's ramp,
the Urchin's grind and the Squirrel's skim answer to none - regatta_balance.py's TUNABLE) - ONE value per hull for
every intensity, because it becomes that hull's new elemental BASELINE for mobility, not a per-match handicap:

    loss = mean over intensities of ln(slowest / fastest)       the gap left, every hull at its level
         + minimality x mean |level| / 10                       the least tuning that does it

Model: regatta_balance.py's lap model on the course each intensity actually races - today's circuit, or stage 2's
promoted circuit when `courses` carries its settings. It is a COMPETENT-PILOT model (the only one in which the
elements reach speed today); the race models that fly the AI do not take an element level yet. Re-pointing this at the
AI models is the upgrade the scout lists ("stage 3 at AI fidelity").

args:
    hulls       the roster (default: regatta_balance.HULLS)
    courses     {"1": settings, ...} RegattaCourse.ForIntensity-shaped (from stage 2), else today's circuits
    seeds       {"1": base seed, ...} stage 2's seeds (default: RegattaCourse.DefaultSeed)
    minimality  the weight on |level| (default 0.02)
"""
import math
import os

import course_port as cp
import race_models as rm

VERSION = "1"
PATHS = ["Tools/Build/", "Assets/_Prefabs/Spacevessels/", "Assets/_SO_Assets/", "Assets/Resources/"]
_CTX = {}


def inputs(args):
    return ["Tools/Build/regatta_balance.py", "Assets/_Scripts/Controller/Arcade/Regatta/RegattaCourse.cs"]


def setup(ctx):
    import regatta_balance as RB
    a = ctx["args"]
    c = RB.read_constants()
    hulls = a.get("hulls") or list(RB.HULLS)
    table = {}
    for i in (1, 2, 3, 4):
        settings = (a.get("courses") or {}).get(str(i))
        seed = int((a.get("seeds") or {}).get(str(i), cp.REGATTA_DEFAULT_SEED))
        course = cp.regatta_course(i, settings=settings, base_seed=seed)
        bc = rm.balance_course(course)
        for h in hulls:
            levels = range(RB.MIN_LEVEL, RB.MAX_LEVEL + 1) if h in RB.TUNABLE else [0]
            for lv in levels:
                table[(h, i, lv)] = RB.lap_time(h, RB.MODELS[h](c, lv), bc) * RB.LAPS
    _CTX.update(table=table, hulls=hulls, tunable=[h for h in hulls if h in RB.TUNABLE], lo=RB.MIN_LEVEL, hi=RB.MAX_LEVEL)


def baseline(args):
    return {}   # every hull at rest (level 0): the space's init


def evaluate(params, ctx):
    if "table" not in _CTX:
        setup(ctx)
    a = ctx["args"]
    T = _CTX["table"]
    levels = {h: int(round(params.get(f"time_{h}", 0))) if h in _CTX["tunable"] else 0 for h in _CTX["hulls"]}
    spreads, worst = {}, {}
    for i in (1, 2, 3, 4):
        times = {h: T[(h, i, levels[h])] for h in _CTX["hulls"]}
        spreads[str(i)] = round(max(times.values()) / min(times.values()), 4)
        worst[str(i)] = [min(times, key=times.get), max(times, key=times.get)]
    gap = sum(math.log(s) for s in spreads.values()) / 4
    tuning = sum(abs(v) for v in levels.values()) / (10.0 * max(1, len(_CTX["tunable"])))
    loss = gap + float(a.get("minimality", 0.02)) * tuning
    return {"loss": loss, "metrics": {"spread_x": spreads, "fastest_slowest": worst, "levels": levels,
                                      "mean_abs_level": round(tuning * 10, 2), "limits": rm.MODEL_LIMITS["balance"]}}


def self_test():
    """At rest the model reproduces regatta_balance.solve()'s rest spread on today's courses."""
    import sys
    here = os.path.dirname(os.path.abspath(__file__))
    root = os.path.dirname(os.path.dirname(os.path.dirname(here)))
    sys.path.insert(0, os.path.join(root, "Tools", "Build"))
    import regatta_balance as RB
    _CTX.clear()
    ctx = {"root": root, "args": {}}
    r = evaluate({}, ctx)
    ref = RB.solve()
    bad = []
    for i in (1, 2, 3, 4):
        if abs(r["metrics"]["spread_x"][str(i)] - ref[i]["restSpread"]) > 0.01:
            bad.append(f"regatta_elements: I{i} rest spread {r['metrics']['spread_x'][str(i)]} vs regatta_balance {ref[i]['restSpread']:.4f}")
    _CTX.clear()
    return bad
