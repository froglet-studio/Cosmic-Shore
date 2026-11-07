"""The Thresher's universal Time AI: ONE sling genome (ThresherSlingAIPolicySO's fields) scored on all four Thresher
Time intensities at once, grown from every Thresher sling genome that exists (Docs/AI_SYSTEM/AI_PLAN.md step
time-arcade). A MODEL's score: Tools/Build/thresher_time_sim.py races it, never the game.

Why: Thresher Time is the Thresher's own Time game. Its AI's sling (plant, wind, aimed launch) was learned once from
people's races on the RINGS (ThresherAIBoostPolicy_ThresherTime, 2026-11-02); since 2026-11-04 every gate is a NEST to
break, and THRESHERTIME.md section 0 says the genome has to be learned again on the new mode. This job is that search,
run by hand while Froglet Compute is paused (Shombith, 2026-11-06).

    per race       time / cruise-out     if it finished    (cruise-out = the race's gate-to-gate path at 125 u/s, the
                                                             Thresher's full-throttle cruise)
                   limit / cruise-out + 2 x the gates it missed / gates      if not (a DNF always scores worse)
    per intensity  mean over races + 0.5 x the worst race
    loss           mean over the intensities + 0.5 x the worst intensity        (no intensity is given up for the others)
                 + stay_near_shipped x mean((field - nearest genome's) / range)^2

The space is the policy asset's own field names, so a result is written into ThresherAIBoostPolicy_ThresherTime
(AIBoostPolicySO.ForMode) without a rename, by the learner's writer (Tools/Build/learn_drives_from_flights.py), never
by hand. The two switches (windWhilePlanted, aimedLaunch) are not searched: they stay as the genome being grown has them
(`aimed` overrides). The mode's own AI and course are whatever the simulator mirrors from the job's ref.

NOT modelled (metrics.limits): thresher_time_sim.LIMITS.

args:
    hull, mode         "Thresher", "ThresherTime": read by the scout, not by the evaluator
    intensities        Thresher Time intensities raced together (default 1-4)
    seeds              fixed course seeds per intensity (default 3)
    seedbase           the first of them (default 1)
    limit              seconds before a race counts as not finished (default 600)
    nests              race the nests (default true; false = the rings, the mode before 2026-11-04)
    aimed              1 / 0 to force the aimed launch on / off (default: the grown genome's own)
    genomes            repo paths of every Thresher sling genome to grow from (a missing file is skipped)
    stay_near_shipped  weight of the pull toward the nearest genome (0 turns it off)
    nose_lead          the AI nose lead (AIPilot.NoseLeadGain, Tools/Build/ai_nose_lead.py): a gain stated at the fleet
                       lag, or {"gain", "max_extra_deg"}; absent or 0 = OFF, today's steering (the numbers above)
    nose_rate          the AI nose's follow rate per second (VesselTransformer.AINoseFollowPerSecond); absent = the
                       fleet lag (1.5) the model flies today
"""
import importlib.util
import os

VERSION = "1"   # 1 (2026-11-06): the nests of 2026-11-04, the chain solver probe, the towed ball's spring
SIM = "Tools/Build/thresher_time_sim.py"
PROBE = "Tools/Build/thresher_time_sim/plant_probe.json"
POL = "Assets/Resources/AIBoostPolicies/"
GENOMES = [POL + "ThresherAIBoostPolicy_ThresherTime.asset", "Assets/_SO_Assets/AI Boost Policies/ThresherAIBoostPolicy.asset"]
FIELDS = {"plantMinTurnDegrees": (0.0, 180.0), "plantMinDistance": (0.0, 600.0), "slingIntervalSeconds": (0.0, 8.0),
          "releaseConeDegrees": (1.0, 90.0), "maxPlantSeconds": (0.2, 6.0), "aimedHoldSeconds": (0.0, 4.0),
          "aimToleranceDegrees": (0.5, 45.0)}
SWITCHES = ("windWhilePlanted", "aimedLaunch")
PATHS = [SIM, PROBE, POL, "Tools/Build/manta_redline_sim.py",
         "Assets/_Scripts/Controller/AI/Boost/ThresherSlingAIPolicySO.cs",
         "Assets/_Scripts/Controller/Arcade/ThresherTime/", "Assets/_Scenes/Multiplayer Scenes/MinigameThresherTime.unity",
         "Assets/_SO_Assets/VesselActions/Thresher/ThresherConfig.asset", "Assets/_Prefabs/Spacevessels/Thresher.prefab"]
REQUEST_ARGS = {"intensities": {"subset": [1, 2, 3, 4]}, "seeds": {"lo": 1, "hi": 12}, "limit": {"lo": 200, "hi": 1200}}
REQUEST_ARGS["nose_lead"] = {"lo": 0.0, "hi": 6.0}   # the AI nose lead a manual run may switch on (0 = off)
COMBINE_WORST = 0.5
CRUISE = 125.0            # ThresherTimeCourse.CruiseSpeed (15 + 110)
_CTX = {}


def inputs(args):
    return [SIM, PROBE, "Tools/Build/ai_nose_lead.py", "Assets/_Scripts/Controller/AI/Boost/ThresherSlingAIPolicySO.cs",
            "Assets/_Scripts/Controller/Arcade/ThresherTime/ThresherTimeCourse.cs",
            "Assets/_Scripts/Controller/Arcade/ThresherTime/ThresherTimeNest.cs",
            "Assets/_Scripts/Controller/Arcade/ThresherTime/ThresherTimeController.cs",
            "Assets/_Scenes/Multiplayer Scenes/MinigameThresherTime.unity",
            "Assets/_SO_Assets/VesselActions/Thresher/ThresherConfig.asset",
            "Assets/_Prefabs/Spacevessels/Thresher.prefab"] + list(genomes(args))


def intensities(args):
    return [int(i) for i in args.get("intensities") or [1, 2, 3, 4]]


def genomes(args):
    return list(args.get("genomes") or GENOMES)


def course_seeds(args):
    n, base = int(args.get("seeds", 3)), int(args.get("seedbase", 1))
    return [base + k for k in range(n)]


def _load_sim(root):
    spec = importlib.util.spec_from_file_location("thresher_time_sim_job", os.path.join(root, SIM))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def _sim(root):
    if _CTX.get("sim_root") != root:
        _CTX["sim"] = _load_sim(root)
        _CTX["sim_root"] = root
    return _CTX["sim"]


# ── the genomes ─────────────────────────────────────────────────────────────────────────────────────────────────

def every_genome(args, root):
    """(name, full genome) for every genome file that exists, the mode's own first."""
    sim = _sim(root)
    out = []
    for g in genomes(args):
        f = sim.read_genome(g) if os.path.isfile(os.path.join(root, g)) else None
        if f:
            out.append((os.path.splitext(os.path.basename(g))[0], f))
    return out


def clamp(fields):
    return {k: min(FIELDS[k][1], max(FIELDS[k][0], float(v))) for k, v in fields.items() if k in FIELDS}


def shipped(args, root):
    every = every_genome(args, root)
    return every[0][1] if every else dict(_sim(root).CODE_DEFAULTS)


def space_for(root, args=None):
    s = shipped(args or {}, root)
    return [{"name": k, "lo": lo, "hi": hi, "init": s[k]} for k, (lo, hi) in FIELDS.items()]


def seeds(args, root):
    """The first generation: every genome as it ships (read from the assets, never retyped)."""
    return [clamp(f) for _, f in every_genome(args, root)]


# ── the evaluator contract ─────────────────────────────────────────────────────────────────────────────────────

def _key(ctx):
    a = ctx["args"]
    return (ctx["root"], tuple(intensities(a)), tuple(course_seeds(a)), tuple(genomes(a)), bool(a.get("nests", True)))


def setup(ctx):
    a, root = ctx["args"], ctx["root"]
    sim = _sim(root)
    courses = {}
    for i in intensities(a):
        for s in course_seeds(a):
            c = sim.course_for(i, s)
            path = sum(c["legLengths"]) * sim.SHIPPED["laps"]
            courses.setdefault(i, []).append((s, c, path / CRUISE))
    _CTX.update(key=_key(ctx), courses=courses, genomes=every_genome(a, root), shipped=shipped(a, root))


def baseline(args):
    return None   # the worker scores the space's `init`, the mode's shipped genome


def score(rows, limit):
    v = [r["time"] / r["flat"] if r["time"] is not None else limit / r["flat"] + 2.0 * (r["of"] - r["gates"]) / max(1, r["of"])
         for r in rows]
    return sum(v) / len(v) + COMBINE_WORST * max(v)


def genome_of(params, args=None):
    """The full genome a params dict names: the grown genome's switches, the searched fields clamped."""
    g = dict(_CTX["shipped"])
    g.update(clamp({k: params.get(k, g[k]) for k in FIELDS}))
    if args and args.get("aimed") is not None:
        g["aimedLaunch"] = 1.0 if int(args["aimed"]) else 0.0
    return g


def evaluate(params, ctx):
    a = ctx["args"]
    if _CTX.get("key") != _key(ctx):
        setup(ctx)
    sim = _CTX["sim"]
    g = genome_of(params, a)
    limit = float(a.get("limit", 600))
    nests = bool(a.get("nests", True))
    med = lambda xs: round(sorted(xs)[len(xs) // 2], 1) if xs else None
    scores, metrics, every_finishes = {}, {}, True
    for i, races in _CTX["courses"].items():
        rows = []
        for s, c, flat in races:
            r = sim.race(c, i, genome=g, nests=nests, limit=limit, nose_rate=a.get("nose_rate"), nose_lead=a.get("nose_lead"))
            r["flat"] = flat
            rows.append(r)
        fin = [r["time"] for r in rows if r["time"] is not None]
        every_finishes &= len(fin) == len(rows)
        scores[i] = score(rows, limit)
        metrics[f"I{i}"] = {"score": round(scores[i], 4), "finished": f"{len(fin)}/{len(rows)}", "median_s": med(fin),
                            "gates": [r["gates"] for r in rows], "launches": sum(r["launches"] for r in rows),
                            "nest_breaks": sum(r["nest_breaks"] for r in rows)}
    s = list(scores.values())
    combined = sum(s) / len(s) + (COMBINE_WORST * max(s) if len(s) > 1 else 0.0)
    loss = combined
    w = float(a.get("stay_near_shipped", 0.01))
    if w:
        span = lambda k: FIELDS[k][1] - FIELDS[k][0]
        near = min(sum(((g[k] - h[k]) / span(k)) ** 2 for k in FIELDS) / len(FIELDS) for _, h in _CTX["genomes"])
        loss += w * near
    metrics.update(score=round(combined, 4), worst=f"I{max(scores, key=scores.get)}", limits=sim.LIMITS)
    return {"loss": loss, "valid": every_finishes, "metrics": metrics}


def self_test():
    """The seeds are the mode's genome and the base genome, read from the repo, the mode's first; the space's init is
    the mode's genome; the same params give the same loss; other course seeds race other courses; a genome that never
    plants (plantMinTurnDegrees 180: the cruise-only control) scores differently from the shipped one; an unbreakable
    nest scores worse than the shipped genome on the same courses (the negative control: credit needs an open nest)."""
    here = os.path.dirname(os.path.abspath(__file__))
    root = os.path.dirname(os.path.dirname(os.path.dirname(here)))
    ctx = {"root": root, "args": {"intensities": [1], "seeds": 1, "limit": 200}}
    _CTX.clear()
    bad = []
    try:
        good = evaluate({}, ctx)
        again = evaluate({}, ctx)
        moved = evaluate({}, {"root": root, "args": dict(ctx["args"], seedbase=50)})
        never = evaluate({"plantMinTurnDegrees": 180.0}, ctx)
        if again["loss"] != good["loss"]:
            bad.append("threshertime_fit: the same params gave a different loss (the sim is not deterministic)")
        if moved["loss"] == good["loss"]:
            bad.append("threshertime_fit: other course seeds raced the same courses (a stale course cache)")
        if never["loss"] == good["loss"]:
            bad.append("threshertime_fit: a genome that never slings scored exactly as the shipped one (the genome is ignored)")
        sim = _CTX["sim"]
        c = _CTX["courses"][1][0][1]
        blocked = sim.race(c, 1, genome=genome_of({}), limit=200, breakable=False)
        if blocked["gates"] != 0:
            bad.append("threshertime_fit: a gate was credited with its nest unbroken")
        sd = seeds(ctx["args"], root)
        if len(sd) < 2 or abs(sd[0]["aimedHoldSeconds"] - 1.54) > 1e-6 or sd[1] == sd[0]:
            bad.append(f"threshertime_fit: the seeds are not the mode's genome and the base genome ({sd})")
        if abs(space_for(root)[0]["init"] - 6.691) > 1e-6:
            bad.append("threshertime_fit: the space's init is not the mode's shipped genome")
    except Exception as e:
        import traceback
        bad.append(f"threshertime_fit: {type(e).__name__}: {e}\n{traceback.format_exc()}")
    _CTX.clear()
    return bad


if __name__ == "__main__":
    import sys
    out = self_test()
    print("\n".join(out) or "threshertime_fit self-test: ok")
    sys.exit(1 if out else 0)
