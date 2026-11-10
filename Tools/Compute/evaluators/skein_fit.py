"""The Urchin's universal Time AI: ONE UrchinAutopilotConfigSO (the rail decisions an AI Urchin makes: when to ride,
reverse, slip off, launch, re-latch and lay a Track) scored on all four Skein intensities at once
(Docs/AI_SYSTEM/AI_PLAN.md step time-arcade).

Why: Skein is the Urchin's own Time game (AI_PLAN.md section 2) and nobody had a model of it. This races the Skein
simulator (Tools/Build/skein_urchin_sim.py), which ports what an AI Urchin does in MinigameSkein: the shipped cable
(exported from SkeinCourse.cs by Tools/Build/skein_course_harness, the prefab seeds and the seed ladder), the gate aim,
SkeinController.TryOverrideAim's rail lead, AIPilot's stick and skill throttle, the free hull's turn law and nose lag,
the ride (TrailFollower and GunVesselTransformer.Slide: own colour 300 u/s, a rival's 20 u/s, the launch kick and its
bleed, the re-latch grace), the latch (a trigger sampled every fixed step), the Track Projector and the
UrchinAutopilotDriver / UrchinRailAssessment decisions line for line. Every number is a MODEL: no Unity editor ran.

    per race       time / ideal      if it finished    (ideal = the rings' polyline at the own-colour ride, 300 u/s)
                   limit / ideal + 2 x the rings it missed / rings      if not (a DNF always scores worse than a finish)
    per intensity  mean over races + 0.5 x the worst race
    loss           mean over the intensities + 0.5 x the worst intensity        (no intensity is given up for the others)
                 + stay_near_shipped x mean((field - shipped) / range)^2

The space is UrchinAutopilotConfigSO's own fields by their names, inside its [Min] / [Range] attributes, so a result
is written into Assets/Resources/UrchinAutopilotConfig.asset without a rename. RailLeadDistance is left out: Skein
rides with SkeinController.aiRailLeadDistance (260), not the config's. NOTE the asset is SHARED by Skein, Regatta and
Hijack (UrchinAutopilotConfigSO.LoadDefault; there is no per-mode file), so a promotion changes the Urchin in all
three: promotion re-races the winner in Regatta's model before it is proposed.

The mode's own AI is an arg, so its what-ifs are searched without editing the game: `steering` "shipped" is Skein as
it ships (the gate aim orbits the approach point, aiCommitFromApproachPoint off), "regatta" is the commit-from-approach
aim Regatta and Switchback use; `seek` 1 adds a mode-side seek for the rail that threads the next ring (the
RegattaController.TryOwnLaneAhead pattern). Both are proposals to the mode's owner, never part of the genome.

NOT modelled (metrics.limits): skein_urchin_sim.LIMITS.

args:
    hull, mode         "Urchin", "Skein": read by the scout, not by the evaluator
    intensities        Skein intensities raced together (default 1-4)
    slots              start angles on the spawn ring, degrees (default 0, 120, 240): the start decides a lot
    holdout            extra cables per intensity from skein_holdout.json (default 0; promotion uses 2)
    limit              seconds before a race counts as not finished (default 600)
    steering, seek     the mode's aim ("shipped" / "regatta") and its rail seek (0 / 1); defaults: Skein as it ships
    stay_near_shipped  weight of the pull toward the shipped genome (0 turns it off)
    nose_lead          the AI nose lead (AIPilot.NoseLeadGain, Tools/Build/ai_nose_lead.py): a gain stated at the fleet
                       lag, or {"gain", "max_extra_deg"}; absent or 0 = OFF, today's steering (the numbers above)
    nose_rate          the AI nose's follow rate per second (VesselTransformer.AINoseFollowPerSecond); absent = the
                       fleet lag (1.5) the model flies today
"""
import importlib.util
import os
import re

VERSION = "1"
SIM = "Tools/Build/skein_urchin_sim.py"
CONFIG_CS = "Assets/_Scripts/Controller/AI/Urchin/UrchinAutopilotConfigSO.cs"
CONFIG_ASSET = "Assets/Resources/UrchinAutopilotConfig.asset"
COURSES = "Tools/Build/skein_course_harness/skein_courses.json"
HOLDOUT = "Tools/Build/skein_course_harness/skein_holdout.json"
PATHS = [SIM, CONFIG_CS, CONFIG_ASSET, COURSES, HOLDOUT, "Assets/_Scripts/Controller/Arcade/Skein/", "Tools/Build/ai_nose_lead.py"]
# Search ranges: the SO's own [Min] / [Range] floors and ceilings; where the SO has only a [Min], a ceiling a few
# times the shipped value (nothing in the driver reads past it).
FIELDS = {
    "AssessIntervalSeconds": (0.05, 1.0),
    "ScanArc": (50.0, 4000.0),
    "CaptureFraction": (0.1, 1.0),
    "ClosingFraction": (0.1, 0.95),
    "LaunchPreferArc": (0.0, 800.0),
    "DryCrawlArc": (0.0, 400.0),
    "MaxDryCrawlSeconds": (0.5, 12.0),
    "ReverseLeadTurnRadii": (1.0, 6.0),
    "ParkedSpeed": (0.5, 40.0),
    "ParkedSeconds": (0.5, 12.0),
    "MinSlipIntervalSeconds": (0.25, 6.0),
    "SpikeIntervalSeconds": (0.1, 6.0),
    "SpikeMinAmmo": (0.0, 1.0),
    "TrackAimDegrees": (1.0, 45.0),
    "TrackMinRangeInLengths": (1.0, 12.0),
    "TrackAxisAlignment": (0.0, 1.0),
}
REQUEST_ARGS = {"intensities": {"subset": [1, 2, 3, 4]}, "holdout": {"lo": 0, "hi": 2}, "limit": {"lo": 300, "hi": 1500}}
COMBINE_WORST = 0.5
_CTX = {}


def intensities(args):
    return [int(i) for i in args.get("intensities") or [1, 2, 3, 4]]


def slots(args):
    return [float(s) for s in (args.get("slots") if args.get("slots") is not None else [0, 120, 240])]


def inputs(args):
    return [SIM, CONFIG_CS, CONFIG_ASSET, COURSES] + ([HOLDOUT] if int(args.get("holdout", 0)) else [])


# ── the genome ─────────────────────────────────────────────────────────────────────────────────────────────────

def shipped(root):
    """What an AI Urchin flies: Resources/UrchinAutopilotConfig.asset over the C# initialisers (LoadDefault)."""
    with open(os.path.join(root, CONFIG_CS), encoding="utf-8") as fh:
        src = fh.read()
    out = {}
    for name in FIELDS:
        m = re.search(r"public float " + name + r"\s*=\s*(-?[0-9.]+)f?\s*;", src)
        if m:
            out[name] = float(m.group(1))
    path = os.path.join(root, CONFIG_ASSET)
    if os.path.isfile(path):
        with open(path, encoding="utf-8") as fh:
            for line in fh:
                m = re.match(r"^\s{2}(\w+):\s*(-?[0-9.]+(?:[eE][-+]?\d+)?)\s*$", line)
                if m and m.group(1) in FIELDS:
                    out[m.group(1)] = float(m.group(2))
    return out


def clamp(fields):
    return {k: min(FIELDS[k][1], max(FIELDS[k][0], v)) for k, v in fields.items() if k in FIELDS}


def space_for(root):
    s = shipped(root)
    return [{"name": k, "lo": lo, "hi": hi, "init": s[k]} for k, (lo, hi) in FIELDS.items()]


def seeds(args, root):
    """The first generation: the genome as it ships (read from the asset and the SO, never retyped). It is the only
    Urchin genome that exists; a learned one joins here when the AI learning sweep writes it."""
    return [clamp(shipped(root))]


# ── the evaluator contract ─────────────────────────────────────────────────────────────────────────────────────

def _load_sim(root):
    spec = importlib.util.spec_from_file_location("skein_urchin_sim_job", os.path.join(root, SIM))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def _key(ctx):
    a = ctx["args"]
    return (ctx["root"], tuple(intensities(a)), int(a.get("holdout", 0)))


def setup(ctx):
    a, root = ctx["args"], ctx["root"]
    sim = _load_sim(root)
    courses = {}
    for i in intensities(a):
        cs = [sim.course_for(i)] + [sim.course_for(i, "holdout", h) for h in range(int(a.get("holdout", 0)))]
        courses[i] = [(c, sim.ideal_seconds(c)) for c in cs]
    _CTX.clear()
    _CTX.update(key=_key(ctx), sim=sim, courses=courses, shipped=clamp(shipped(root)))


def baseline(args):
    return None   # the worker scores the space's `init`, the shipped genome


def score(rows, limit):
    v = [r["time"] / r["ideal"] if r["time"] is not None else limit / r["ideal"] + 2.0 * (r["of"] - r["rings"]) / max(1, r["of"])
         for r in rows]
    return sum(v) / len(v) + COMBINE_WORST * max(v)


def evaluate(params, ctx):
    a = ctx["args"]
    if _CTX.get("key") != _key(ctx):
        setup(ctx)
    sim = _CTX["sim"]
    p = clamp({k: float(params.get(k, v)) for k, v in _CTX["shipped"].items()})
    limit = float(a.get("limit", 600))
    steering = a.get("steering", "shipped")
    seek = bool(int(a.get("seek", 0)))
    med = lambda xs: round(sorted(xs)[len(xs) // 2], 1) if xs else None
    scores, metrics, every_finishes = {}, {}, True
    for i, races in _CTX["courses"].items():
        rows = []
        for c, ideal in races:
            for s in slots(a):
                r = sim.race(c, p, limit=limit, start_slot=s, steering=steering, seek=seek, nose_rate=a.get("nose_rate"),
                             nose_lead=a.get("nose_lead"))
                r["ideal"] = ideal
                rows.append(r)
        fin = [r["time"] for r in rows if r["time"] is not None]
        every_finishes &= len(fin) == len(rows)
        scores[i] = score(rows, limit)
        metrics[f"I{i}"] = {"score": round(scores[i], 4), "finished": f"{len(fin)}/{len(rows)}", "median_s": med(fin),
                            "rings": round(sum(r["rings"] for r in rows) / len(rows), 1),
                            "ride": round(sum(r["ride_share"] for r in rows) / len(rows), 3),
                            "tracks": sum(r["tracks"] for r in rows), "slips": sum(r["slips"] for r in rows)}
    s = list(scores.values())
    combined = sum(s) / len(s) + (COMBINE_WORST * max(s) if len(s) > 1 else 0.0)
    loss = combined
    w = float(a.get("stay_near_shipped", 0.01))
    if w:
        sh = _CTX["shipped"]
        loss += w * sum(((p[k] - sh[k]) / (FIELDS[k][1] - FIELDS[k][0])) ** 2 for k in FIELDS) / len(FIELDS)
    metrics.update(score=round(combined, 4), worst=f"I{max(scores, key=scores.get)}", limits=sim.LIMITS)
    return {"loss": loss, "valid": every_finishes, "metrics": metrics}


def self_test():
    """The shipped genome finishes I1 from every slot; an Urchin that slips off every rail the moment it latches
    (ParkedSpeed at its ceiling, ParkedSeconds at its floor: every ride reads as parked) scores worse (the negative
    control); the same params give the same loss; the seed and the space's init are the shipped asset's values."""
    here = os.path.dirname(os.path.abspath(__file__))
    root = os.path.dirname(os.path.dirname(os.path.dirname(here)))
    ctx = {"root": root, "args": {"intensities": [1]}}
    _CTX.clear()
    bad = []
    try:
        good = evaluate({}, ctx)
        parked = evaluate({"ParkedSpeed": FIELDS["ParkedSpeed"][1], "ParkedSeconds": FIELDS["ParkedSeconds"][0]}, ctx)
        again = evaluate({}, ctx)
        if good["metrics"]["I1"]["finished"] != f"{len(slots(ctx['args']))}/{len(slots(ctx['args']))}":
            bad.append(f"skein_fit: the shipped genome did not finish I1 from every slot: {good['metrics']['I1']}")
        if not parked["loss"] > good["loss"]:
            bad.append(f"skein_fit: an Urchin that never rides did not score worse ({parked['loss']:.3f} vs {good['loss']:.3f})")
        if again["loss"] != good["loss"]:
            bad.append("skein_fit: the same params gave a different loss (the sim is not deterministic)")
        sd = seeds(ctx["args"], root)
        if len(sd) != 1 or sd[0].get("CaptureFraction") != 0.8 or sd[0].get("MaxDryCrawlSeconds") != 4.0:
            bad.append(f"skein_fit: the seed is not the shipped UrchinAutopilotConfig ({sd})")
        init = {d["name"]: d["init"] for d in space_for(root)}
        if init != sd[0] or set(init) != set(FIELDS):
            bad.append("skein_fit: the space's init is not the shipped genome")
        if "RailLeadDistance" in FIELDS:
            bad.append("skein_fit: RailLeadDistance is in the space, but Skein rides with aiRailLeadDistance")
    except Exception as e:
        bad.append(f"skein_fit: {type(e).__name__}: {e}")
    _CTX.clear()
    return bad


if __name__ == "__main__":
    import sys
    if "--self-test" in sys.argv:
        b = self_test()
        for x in b:
            print("FAIL", x)
        print("skein_fit self-test:", "ok" if not b else f"{len(b)} failure(s)")
        sys.exit(1 if b else 0)
