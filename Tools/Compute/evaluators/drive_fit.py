"""STAGE 1 of the Regatta balance plan: a hull's AI DRIVE that races its own Time game well at every intensity.

The space is the drive's own asset fields (RampBoostAIPolicySO, MantaSoarAITuningSO, ChargeBoostAIPolicySO, by the
names in race_models.DRIVE_FIELDS); the loss is how long the AI takes to race the mode's circuit at each intensity,
on several course seeds when the mode deals a new course every match:

    loss = mean over (intensity, seed) of ln(race seconds)           a DNF is priced (race_models.DNF_*)
         + stay_near_shipped x mean((field - shipped) / range)^2       never wander for nothing

ln() makes every intensity count the same (a 10% gain at I1 is worth a 10% gain at I4) and "flies well on ALL four"
is a hard number: `metrics.worst_vs_shipped` is the worst intensity's time against the shipped drive's.

Where this sits next to the learners: the hourly AI learning sweep (Docs/AI_SYSTEM/AUTO_LEARNING.md) learns a drive
from how PEOPLE fly a mode, and that stays the primary signal wherever people play. This job fills what nobody plays
(an intensity with no races, a mode with no learner) and tells the AI owner where the person-learned numbers race
slower in the model than a searched drive. Its result is a PROPOSAL for the drive's owner (Yash for the AI),
written into the `<asset>_<Mode>` variant only with their go-ahead (/regatta-balance stage 1).

args:
    hull         "Rhino" | "Manta" | "Dolphin"
    model        race_models model: rhino_drive | manta_drive | dolphin_drive
    mode         "Regatta" | "Headlong" | "Redline" (which circuit generator, which steering)
    intensities  [1, 2, 3, 4]
    seeds        course seeds per intensity for modes that deal a new course each match (Regatta's is fixed)
    shipped      the asset to read the shipped values from (repo-relative), else the space's `init`
    limit        seconds before a race counts as not finished
"""
import math
import os

import course_port as cp
import race_models as rm

VERSION = "1"
PATHS = ["Tools/Build/", "Assets/Resources/AIBoostPolicies/", "Assets/Resources/RaceLines/",
         "Assets/_Prefabs/Spacevessels/"]
_CTX = {}


def inputs(args):
    sims = {"rhino_drive": "rhino_drive_sim.py", "manta_drive": "manta_drive_sim.py", "dolphin_drive": "dolphin_drive_sim.py"}
    out = ["Tools/Build/" + sims.get(args.get("model"), ""), "Tools/Build/situation_features.py"]
    if args.get("shipped"):
        out.append(args["shipped"])
    return out


def _courses(args):
    mode = args.get("mode", "Regatta")
    out = []
    for i in args.get("intensities", [1, 2, 3, 4]):
        if mode == "Regatta":
            out.append((i, 0, cp.regatta_course(i, rails=False)))
            continue
        settings = cp.headlong_settings(i) if mode == "Headlong" else cp.redline_settings(i)
        for s in args.get("seeds", [1, 2, 3]):
            out.append((i, s, cp.course(settings, cp.seed_for_intensity(20260915 + 104729 * int(s), i), rails=False)))
    return out


def setup(ctx):
    _CTX["courses"] = _courses(ctx["args"])
    _CTX["shipped"] = rm.read_asset_fields(ctx["root"], ctx["args"]["shipped"]) if ctx["args"].get("shipped") else {}


def baseline(args):
    return None   # the worker scores the space's `init` values, which jobs set to the shipped asset's numbers


def evaluate(params, ctx):
    a = ctx["args"]
    if "courses" not in _CTX:
        setup(ctx)
    model = a["model"]
    drive = rm.to_sim(model, params)
    steering = "centre" if a.get("mode", "Regatta") == "Regatta" else "axis"
    limit = float(a.get("limit", 400))
    logs, per_i, dnf = [], {}, 0
    for (i, seed, course) in _CTX["courses"]:
        t, raw = rm.race(a["hull"], model, course, i, drive=drive, steering=steering, limit=limit)
        logs.append(math.log(t))
        per_i.setdefault(i, []).append(t)
        dnf += raw["time"] is None
    loss = sum(logs) / len(logs)
    shipped = _CTX.get("shipped") or {}
    w = float(a.get("stay_near_shipped", 0.02))
    if w and shipped:
        dims = {d["name"]: d for d in a.get("_space", [])}
        pen = []
        for k, v in params.items():
            if k in shipped and k in dims:
                span = dims[k]["hi"] - dims[k]["lo"]
                pen.append(((v - shipped[k]) / span) ** 2)
        if pen:
            loss += w * sum(pen) / len(pen)
    return {"loss": loss, "metrics": {
        "seconds_by_intensity": {str(i): round(sum(v) / len(v), 2) for i, v in per_i.items()},
        "dnf": dnf, "races": len(logs), "model": model, "limits": rm.MODEL_LIMITS[model]}}


def self_test():
    """The shipped Rhino Regatta drive races all four shipped courses (the evaluator reproduces the sim)."""
    import sys
    here = os.path.dirname(os.path.abspath(__file__))
    root = os.path.dirname(os.path.dirname(os.path.dirname(here)))
    sys.path.insert(0, os.path.join(root, "Tools", "Build"))
    ctx = {"root": root, "args": {"hull": "Rhino", "model": "rhino_drive", "mode": "Regatta",
                                  "shipped": "Assets/Resources/AIBoostPolicies/RhinoAIBoostPolicy_Regatta.asset"}}
    _CTX.clear()
    setup(ctx)
    r = evaluate(dict(_CTX["shipped"]), ctx)
    bad = []
    if r["metrics"]["dnf"]:
        bad.append(f"drive_fit: the shipped Rhino drive did not finish {r['metrics']}")
    _CTX.clear()
    return bad
