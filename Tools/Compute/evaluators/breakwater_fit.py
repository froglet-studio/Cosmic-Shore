"""The Sparrow's universal Time AI: ONE afterburner-and-rocket drive (HoldBoostAIPolicySO) scored on all four Breakwater
intensities at once, grown from every Sparrow drive genome the game ships (Docs/AI_SYSTEM/AI_PLAN.md step time-arcade;
Breakwater is the Sparrow's Time game, MINIGAME_TRAINING_PLAN.md section 6).

Why: the game carries two Sparrow drives - the base `SparrowAIBoostPolicy` every mode flies (bands set for a
straight-to-a-crystal autopilot: on a race line it lets go of the afterburner every 0.6 s, which is why the AI Sparrow is
the slowest hull in Regatta, Tools/Build/sparrow_regatta_sim.py) and `SparrowAIBoostPolicy_Breakwater` (wide bands and
the rocket learned from Yash's shots, 2026-10-26). The plan wants one AI per vessel and element that races every
intensity well; this searches it, its first generation the two genomes and mutations of each.

The model is Tools/Build/breakwater_sparrow_sim.py: the course mirror (breakwater_arena.generate, BreakwaterCourse's
walk bit for bit), the 29 crossings, the Regatta steering BreakwaterController flies, AIPilot's stick law and nose lag,
the measured hull (cruise 35, afterburner 135 u/s, easing 1.4 / s), HoldBoostAIPolicySO's hold and rocket line for line,
and the station's eye, Plain collar, Danger weave and the rocket's hole.

    per race       seconds for the 29 crossings; a DNF is the cut plus 15 s per crossing left (worse than any finish)
    per intensity  mean over its courses + 0.5 x the worst course
    loss           ln of: mean over the intensities + 0.5 x the worst intensity      (the AI plan's composite)
                 + stay_near_shipped x mean((field - the nearest genome's) / range)^2       never wander for nothing

The space is HoldBoostAIPolicySO's fields by the asset's own names, bounded by their [Range] / [Min] (narrowed where a
value past them only switches the drive off).

NOT modelled (metrics.limits): the editor; rivals; the shoals and the dish's plates; the danger prism's element debuff;
the guns' ammo refill is an assumption (breakwater_sparrow_sim.REFILL_PER_STATION). Fixed course seeds can be
over-fit: promotion re-races the winner against both genomes on FRESH seeds (the job's `promote`).

args:
    intensities        Breakwater intensities raced together (default 1-4)
    seeds              course seeds per intensity (fixed: same params, same loss)
    seedbase           the first course seed
    max_seconds        a race is cut here (a DNF)
    genomes            repo paths of every genome to grow from (default: the base drive and the Breakwater variant)
    candidates         hand candidates added to the first generation (CANDIDATES, e.g. ["held"])
    stay_near_shipped  weight of the pull toward the nearest genome's values (0 turns it off)
    nose_lead          the AI nose lead (AIPilot.NoseLeadGain, Tools/Build/ai_nose_lead.py): a gain stated at the fleet
                       lag, or {"gain", "max_extra_deg"}; absent or 0 = OFF, today's steering (the numbers above)
"""
import math
import os
import re
import sys

VERSION = "2"   # 2: the Plain collar slows x0.5 (the asset), the held-afterburner candidate seeds the search
BASE = "Assets/_SO_Assets/AI Boost Policies/SparrowAIBoostPolicy.asset"
VARIANT = "Assets/Resources/AIBoostPolicies/SparrowAIBoostPolicy_Breakwater.asset"
GENOMES = [VARIANT, BASE]
FIELDS = {"alignDegrees": (0.0, 90.0), "releaseAlignDegrees": (0.0, 120.0), "stickBand": (0.0, 1.0),
          "releaseStickBand": (0.0, 1.0), "engageRunwaySeconds": (0.0, 4.0), "releaseRunwaySeconds": (0.0, 3.0),
          "minHoldSeconds": (0.0, 3.0), "minRestSeconds": (0.0, 2.0), "rocketMaxRange": (1.0, 700.0),
          "rocketMinRange": (0.0, 300.0), "rocketAimDegrees": (0.0, 45.0), "rocketCooldownSeconds": (0.1, 3.0)}
PATHS = ["Tools/Build/breakwater_sparrow_sim.py", "Tools/Build/sparrow_regatta_sim.py", "Tools/Build/breakwater_arena.py",
         "Tools/Build/regatta_line_sim.py", "Tools/Build/rhino_drive_sim.py", "Tools/Build/scarab_drive_sim.py",
         "Tools/Build/situation_features.py", "Tools/Build/ai_nose_lead.py", BASE, VARIANT]
LIMITS = ("offline model of Breakwater (Tools/Build/breakwater_sparrow_sim.py): the mirrored course walk, the Regatta "
          "steering, a measured kinematic Sparrow, the shipped drive's hold and rocket, the station's eye / collar / "
          "danger weave / rocket hole; one AI, no rivals; no danger debuff; the ammo refill is assumed; fixed seeds - "
          "promotion re-races fresh ones")
REQUEST_ARGS = {"intensities": {"subset": [1, 2, 3, 4]}, "seeds": {"lo": 1, "hi": 12}}
REQUEST_ARGS["nose_lead"] = {"lo": 0.0, "hi": 6.0}   # the AI nose lead a manual run may switch on (0 = off)
COMBINE_WORST = 0.5
_CTX = {}


def _sim():
    if "sim" not in _CTX:
        here = os.path.dirname(os.path.abspath(__file__))
        sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(here)), "Build"))
        import breakwater_sparrow_sim as S
        _CTX["sim"] = S
    return _CTX["sim"]


def inputs(args):
    return list(PATHS)


def genomes(args):
    return list(args.get("genomes") or GENOMES)


def read_policy(root, rel):
    """The drive fields of a shipped HoldBoostAIPolicySO asset (the space's names), and whether it carries a rocket."""
    out = {}
    text = open(os.path.join(root, rel), encoding="utf-8").read()
    for k in FIELDS:
        m = re.search(r"^\s{2}" + k + r":\s*(-?[0-9.]+(?:[eE][-+]?\d+)?)\s*$", text, re.M)
        if m:
            out[k] = float(m.group(1))
    rocket = re.search(r"^\s{2}clearWayRocket:\s*\{fileID:\s*(\d+)", text, re.M)
    return out, bool(rocket and rocket.group(1) != "0")


# Hand candidates grown from beside the genomes (args.candidates). "held": the first genome with the afterburner never
# let go (people hold it 80-99% of a Breakwater race). Version 1's two restarts both settled on a release 1.4 s before
# each station (loss 5.690) while this point scores 5.613 on the same courses: the sep-CMA-ES at sigma0 0.2 from the
# shipped genomes does not cross the valley of extra weave clips between them.
CANDIDATES = {"held": {"alignDegrees": 90.0, "releaseAlignDegrees": 120.0, "stickBand": 1.0, "releaseStickBand": 1.0,
                       "engageRunwaySeconds": 0.0, "releaseRunwaySeconds": 0.0}}


def seeds(args, root):
    """The first generation's genomes: each asset in `genomes` as it ships, clamped into the space (read from the
    assets, never retyped), then each named hand candidate laid over the first genome."""
    out = []
    for g in genomes(args):
        p, _ = read_policy(root, g)
        out.append({k: min(FIELDS[k][1], max(FIELDS[k][0], p.get(k, sum(FIELDS[k]) / 2))) for k in FIELDS})
    for name in args.get("candidates", []):
        out.append(dict(out[0], **CANDIDATES[name]))
    return out


def courses(args):
    S = _sim()
    base = int(args.get("seedbase", 7100))
    n = int(args.get("seeds", 3))
    return [(i, s, S.course(i, s)) for i in args.get("intensities", [1, 2, 3, 4]) for s in range(base, base + n)]


def setup(ctx):
    a, root = ctx["args"], ctx["root"]
    _CTX["courses"] = courses(a)
    _CTX["genomes"] = [read_policy(root, g)[0] for g in genomes(a)]


def baseline(args):
    return None   # the worker scores the space's `init` values, which the job sets to the variant's numbers


def score(params, ctx, fresh=None):
    """Race every course; returns (composite seconds, metrics)."""
    S = _sim()
    a = ctx["args"]
    cut = float(a.get("max_seconds", 300))
    p = dict(params, rocket=True)
    for k, (lo, hi) in FIELDS.items():
        if k not in p:
            p[k] = (_CTX["genomes"][0] if _CTX.get("genomes") else {}).get(k, (lo + hi) / 2)
    per_i, met = {}, {}
    for (i, s, c) in (fresh or _CTX["courses"]):
        r = S.race(c, p, T=cut, nose_lead=a.get("nose_lead"))
        per_i.setdefault(i, []).append(S.priced(r, cut))
        m = met.setdefault(f"I{i}", {"finished": 0, "races": 0, "danger": 0, "collar": 0, "misses": 0, "rockets": 0,
                                     "boosted": 0.0})
        m["races"] += 1; m["finished"] += r["time"] is not None
        for k in ("danger", "collar", "misses", "rockets"):
            m[k] += r[k]
        m["boosted"] += r["boosted"]
    by_i = {}
    for i, v in per_i.items():
        by_i[i] = sum(v) / len(v) + COMBINE_WORST * max(v)
        m = met[f"I{i}"]
        m["seconds"] = round(sum(v) / len(v), 1)
        m["boosted"] = round(m["boosted"] / m["races"], 2)
        m["finished"] = f"{m['finished']}/{m['races']}"
    return S.composite(by_i, COMBINE_WORST), met


def evaluate(params, ctx):
    if "courses" not in _CTX:
        setup(ctx)
    comp, met = score(params, ctx)
    loss = math.log(comp)
    w = float(ctx["args"].get("stay_near_shipped", 0.01))
    if w and _CTX.get("genomes"):
        pens = []
        for g in _CTX["genomes"]:
            d = [((params[k] - g[k]) / (FIELDS[k][1] - FIELDS[k][0])) ** 2 for k in FIELDS if k in params and k in g]
            if d:
                pens.append(sum(d) / len(d))
        if pens:
            loss += w * min(pens)
    met.update(composite_seconds=round(comp, 2), limits=LIMITS)
    return {"loss": loss, "metrics": met}


def self_test():
    """Both genomes race and finish I1; the Breakwater variant beats the base drive (its stutter); the same params give
    the same loss; a drive that never boosts (an align cone of 0) scores worse (the negative control); the seeds are the
    shipped genomes."""
    here = os.path.dirname(os.path.abspath(__file__))
    root = os.path.dirname(os.path.dirname(os.path.dirname(here)))
    ctx = {"root": root, "args": {"intensities": [1], "seeds": 1, "seedbase": 7100}}
    _CTX.clear()
    bad = []
    try:
        sd = seeds(ctx["args"], root)
        variant, base = sd
        good, plain = evaluate(variant, ctx), evaluate(base, ctx)
        again = evaluate(variant, ctx)
        never = evaluate(dict(variant, alignDegrees=0.0, releaseAlignDegrees=0.0), ctx)
        if good["metrics"]["I1"]["finished"] != "1/1":
            bad.append(f"breakwater_fit: the Breakwater drive did not finish I1: {good['metrics']}")
        if not good["loss"] < plain["loss"]:
            bad.append(f"breakwater_fit: the variant did not beat the base drive ({good['loss']:.3f} vs {plain['loss']:.3f})")
        if again["loss"] != good["loss"]:
            bad.append("breakwater_fit: the same params gave a different loss")
        if not never["loss"] > good["loss"]:
            bad.append("breakwater_fit: a drive that never boosts did not score worse")
        if variant == base or abs(variant["releaseRunwaySeconds"] - 1.25) > 1e-6:
            bad.append(f"breakwater_fit: the seeds are not the shipped genomes ({sd})")
    except Exception as e:
        bad.append(f"breakwater_fit: {type(e).__name__}: {e}")
    _CTX.clear()
    return bad


if __name__ == "__main__":
    b = self_test()
    print("breakwater_fit self-test: " + ("ok" if not b else "FAILED " + repr(b)))
    sys.exit(1 if b else 0)
