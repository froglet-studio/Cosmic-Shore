"""STAGE 2 of the Regatta balance plan: the Regatta circuit that levels the field.

Regatta seats every hull on one course, and on today's course the fastest AI finishes long before the slowest. This job
searches ONE intensity's circuit settings (RegattaCourse.ForIntensity: the corner profile, the mouth, the floors, the
spread, the lateral swing, the gate presentation, plus which seed lays it) for the course on which the roster's AIs
finish closest together:

    loss = ln(slowest AI's race / fastest AI's race)                    the DISPARITY, 0 = a dead heat
         + 0.5 per (gate, lane) whose rail no longer passes the mouth    RegattaCourseTests' coverage rule
         + 2 x how far the mean race moved outside [0.8, 1.25] x today's (in ln)   a race stays a race
         + 2 x how far the hardest corner fell under 0.85 x today's (in ln)        an intensity keeps its character

The circuit is the shipped generator (course_port.py, gated against the C# harness); each hull races it with the model
named in `roster` (race_models.py), with the drive numbers stage 1 promoted (or today's shipped ones).

WHEN TO RUN IT FOR REAL: after stage 1 converges and is promoted, so the roster's drives are the trained ones (the
plan, Docs/AI_SYSTEM/REGATTA_BALANCE_PLAN.md). Until then its jobs are `blocked`; a run before that measures today's
AIs, which is still a useful number but not the one the plan promotes.

args:
    intensity  1-4
    roster     {hull: {"model": race_models model, "drive": {asset field: value} | "asset": repo path}}
    limit      seconds before a race counts as not finished
"""
import math
import os

import course_port as cp
import race_models as rm

VERSION = "1"
PATHS = ["Tools/Build/", "Assets/Resources/AIBoostPolicies/", "Assets/Resources/RaceLines/",
         "Assets/_Prefabs/Spacevessels/", "Assets/_SO_Assets/", "Assets/Resources/"]
SHIP_SEED = cp.REGATTA_DEFAULT_SEED
_CTX = {}


def inputs(args):
    out = ["Tools/Build/rhino_drive_sim.py", "Tools/Build/manta_drive_sim.py", "Tools/Build/dolphin_drive_sim.py",
           "Tools/Build/regatta_line_sim.py", "Tools/Build/regatta_balance.py", "Tools/Build/situation_features.py",
           "Assets/_Scripts/Controller/Arcade/Regatta/RegattaCourse.cs",
           "Assets/_Scripts/Controller/Arcade/Headlong/HeadlongCircuit.cs"]
    for h in (args.get("roster") or {}).values():
        if h.get("asset"):
            out.append(h["asset"])
    return out


def settings_from(params, intensity):
    """The job's parameters -> a RegattaCourse.ForIntensity-shaped settings dict and the base seed."""
    s = cp.regatta_settings(intensity)
    angles = [max(0.0, float(params.get(f"corner{k}", s["CornerProfile"][k]))) for k in range(8)]
    total = sum(angles) or 1.0
    s["CornerProfile"] = [a * 360.0 / total for a in angles]
    for key in ("RingRadius", "CornerFloorRadius", "AngularSpread", "LateralPerturbation", "AxisJitterDegrees",
                "MaxPresentDegrees", "RadialSwing"):
        if key in params:
            s[key] = float(params[key])
    base_seed = SHIP_SEED + 104729 * int(params.get("seed", 0))
    return s, base_seed


def _drives(root, roster):
    out = {}
    for hull, h in roster.items():
        d = dict(h.get("drive") or {})
        if h.get("asset"):
            d = dict(rm.read_asset_fields(root, h["asset"]), **d)
        out[hull] = rm.to_sim(h["model"], d) if h["model"] in rm.DRIVE_FIELDS else d
    return out


def race_all(course, intensity, roster, drives, limit):
    times, raw = {}, {}
    for hull, h in roster.items():
        c = rm.balance_course(course) if h["model"] == "balance" else course
        if h["model"] == "balance":
            c = dict(c, gates=course["gates"])
        t, r = rm.race(hull, h["model"], c, intensity, drive=drives[hull], steering="centre", limit=limit)
        times[hull], raw[hull] = t, r
    return times, raw


def setup(ctx):
    a = ctx["args"]
    i = int(a["intensity"])
    _CTX["drives"] = _drives(ctx["root"], a["roster"])
    ship = cp.regatta_course(i)
    times, _ = race_all(ship, i, a["roster"], _CTX["drives"], float(a.get("limit", 400)))
    _CTX["ship_mean"] = sum(times.values()) / len(times)
    _CTX["ship_hardest"] = max(ship["turnDegrees"])


def baseline(args):
    s = cp.regatta_settings(int(args["intensity"]))
    p = {f"corner{k}": float(v) for k, v in enumerate(s["CornerProfile"])}
    p.update({k: float(s[k]) for k in ("RingRadius", "CornerFloorRadius", "AngularSpread", "LateralPerturbation",
                                       "AxisJitterDegrees", "MaxPresentDegrees", "RadialSwing")})
    p["seed"] = 0
    return p


def evaluate(params, ctx):
    a = ctx["args"]
    if "drives" not in _CTX:
        setup(ctx)
    i = int(a["intensity"])
    s, base_seed = settings_from(params, i)
    course = cp.course(s, cp.seed_for_intensity(base_seed, i))
    times, raw = race_all(course, i, a["roster"], _CTX["drives"], float(a.get("limit", 400)))
    fastest, slowest = min(times.values()), max(times.values())
    disparity = math.log(slowest / fastest)
    cover = rm.rail_coverage(course)
    mean = sum(times.values()) / len(times)
    drift = abs(math.log(mean / _CTX["ship_mean"]))
    hardest = max(course["turnDegrees"])
    soft = max(0.0, math.log(0.85 * _CTX["ship_hardest"] / max(hardest, 1e-6)))
    loss = disparity + 0.5 * cover + 2.0 * max(0.0, drift - math.log(1.25)) + 2.0 * soft
    return {"loss": loss, "metrics": {
        "disparity": round(disparity, 4), "spread_x": round(slowest / fastest, 3),
        "seconds": {h: round(t, 1) for h, t in times.items()},
        "dnf": [h for h, r in raw.items() if r.get("time") is None], "rail_fails": cover,
        "mean_vs_shipped": round(mean / _CTX["ship_mean"], 3), "hardest_turn": round(hardest, 1),
        "seed": cp.seed_for_intensity(base_seed, i), "settings": s,
        "limits": {h: rm.MODEL_LIMITS[r["model"]] for h, r in a["roster"].items()}}}


def self_test():
    """Today's I2 course scores its own disparity with no penalty (every rail passes, the race is today's length)."""
    import sys
    here = os.path.dirname(os.path.abspath(__file__))
    root = os.path.dirname(os.path.dirname(os.path.dirname(here)))
    sys.path.insert(0, os.path.join(root, "Tools", "Build"))
    roster = {"Rhino": {"model": "rhino_drive", "asset": "Assets/Resources/AIBoostPolicies/RhinoAIBoostPolicy_Regatta.asset"},
              "Urchin": {"model": "rail", "drive": {"speed": 300}}}
    ctx = {"root": root, "args": {"intensity": 2, "roster": roster}}
    _CTX.clear()
    r = evaluate(baseline(ctx["args"]), ctx)
    bad = []
    m = r["metrics"]
    if m["rail_fails"] or abs(r["loss"] - m["disparity"]) > 1e-3:
        bad.append(f"regatta_course: today's I2 course is penalised ({m})")
    _CTX.clear()
    return bad
