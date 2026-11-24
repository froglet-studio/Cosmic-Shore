"""The Serpent's universal Time AI: ONE pellet drive (PelletBoostAIPolicySO's decision fields) scored on all four
Serpentine intensities at once, grown from every Serpent Time genome that exists (Docs/AI_SYSTEM/AI_PLAN.md step
time-arcade; Serpentine is GameModes.SerpentTime, the Serpent's own Time game).

Why: no model of the Serpent's race existed. The AI's pellets are its whole speed verb: the tank holds four, each burns
6 s, and since 2026-10-31 nothing refills them on a clock (only the Serpent's own wall eating its own trail does), so
WHEN the four are lit, and how many at once, is the decision a race turns on. This races the Serpentine simulator
(Tools/Build/serpent_time_sim.py): the shipped course (held to the C# by serpent_time_course_harness), the Regatta
steering with the scene's distances, AIPilot's single stick and orbit break-off, the hull's turn law and AI nose lag,
the pellet executor and PelletBoostAIPolicySO.Driver. The dirty race is OFF (no SerpentineGate shapes, shots, walls or
cloak): serpent_time_sim.MODEL_LIMITS.

    per race       time / flat-out     if it finished    (flat-out = the race's gate-to-gate path at a full stack, 460 u/s)
                   limit / flat-out + 2 x the rings it missed / rings      if not (a DNF always scores worse than any finish)
    per intensity  mean over races + 0.5 x the worst race
    loss           mean over the intensities + 0.5 x the worst intensity        (no intensity is given up for the others)
                 + stay_near_shipped x mean((field - nearest genome's) / range)^2

The space is the driver's own asset fields by their names (alignDegrees, stickBand, engageRunwaySeconds from
AIBoostPolicySO; maxConcurrentBurns, minSecondsBetweenPellets, reservePellets from PelletBoostAIPolicySO), so a result
is written into SerpentAIBoostPolicy_SerpentTime (AIBoostPolicySO.ForMode) without a rename. The release fields
(releaseAlignDegrees, releaseStickBand, releaseRunwaySeconds) are not in it: a lit pellet cannot be put out, and the
pellet driver never reads them. The vessel's numbers (the tank, the burn, the multiplier) are read, never searched: a
game mode never changes the vessel (CLAUDE.md, LOCKED).

Races: `seeds` fixed course seeds per intensity from `seedbase` (Serpentine deals a new course every match, so several
are raced; promotion re-races the winner on FRESH seeds).

args:
    hull, mode         "Serpent", "SerpentTime": read by the scout, not by the evaluator
    intensities        intensities raced together (default 1-4)
    seeds              fixed course seeds per intensity (default 4)
    seedbase           the first of them (default 1)
    limit              seconds before a race counts as not finished (default 600)
    genomes            repo paths of every Serpent Time genome to grow from (PelletBoostAIPolicySO assets; a missing file
                       is skipped)
    stay_near_shipped  weight of the pull toward the nearest genome (0 turns it off)
    nose_lead          the AI nose lead (AIPilot.NoseLeadGain, Tools/Build/ai_nose_lead.py): a gain stated at the fleet
                       lag, or {"gain", "max_extra_deg"}; absent or 0 = OFF, today's steering (the numbers above)
"""
import importlib.util
import os

VERSION = "1"
SIM = "Tools/Build/serpent_time_sim.py"
GENOMES = ["Assets/_SO_Assets/AI Boost Policies/SerpentAIBoostPolicy.asset",
           "Assets/Resources/AIBoostPolicies/SerpentAIBoostPolicy_SerpentTime.asset"]
FIELDS = {"alignDegrees": (0.0, 90.0), "stickBand": (0.0, 1.0), "engageRunwaySeconds": (0.0, 6.0),
          "maxConcurrentBurns": (1, 4), "minSecondsBetweenPellets": (0.0, 8.0), "reservePellets": (0, 3)}
INTS = ("maxConcurrentBurns", "reservePellets")
PATHS = [SIM, "Assets/_Scripts/Controller/Arcade/SerpentTime/", "Assets/_Scripts/Controller/AI/Boost/PelletBoostAIPolicySO.cs",
         "Assets/_Prefabs/Spacevessels/Serpent.prefab", "Assets/_SO_Assets/VesselActions/Serpent/ConsumeBoostAction.asset"]
REQUEST_ARGS = {"intensities": {"subset": [1, 2, 3, 4]}, "seeds": {"lo": 1, "hi": 12}, "limit": {"lo": 300, "hi": 1200}}
REQUEST_ARGS["nose_lead"] = {"lo": 0.0, "hi": 6.0}   # the AI nose lead a manual run may switch on (0 = off)
COMBINE_WORST = 0.5
_CTX = {}


def _sim_inputs():
    return [SIM, "Tools/Build/manta_redline_sim.py", "Tools/Build/ai_nose_lead.py", "Tools/Compute/evaluators/course_port.py",
            "Assets/_Scripts/Controller/Arcade/SerpentTime/SerpentTimeCourse.cs",
            "Assets/_Scripts/Controller/Arcade/SerpentTime/SerpentTimeCourseSource.cs",
            "Assets/_Scripts/Controller/Arcade/SerpentTime/SerpentTimeController.cs",
            "Assets/_Scenes/Multiplayer Scenes/MinigameSerpentTime.unity",
            "Assets/_Prefabs/Spacevessels/Serpent.prefab", "Assets/_SO_Assets/VesselActions/Serpent/ConsumeBoostAction.asset"]


def inputs(args):
    return _sim_inputs() + genomes(args)


def intensities(args):
    return [int(i) for i in args.get("intensities") or [1, 2, 3, 4]]


def genomes(args):
    return list(args.get("genomes") or GENOMES)


def course_seeds(args):
    n, base = int(args.get("seeds", 4)), int(args.get("seedbase", 1))
    return [base + k for k in range(n)]


def _load_sim(root):
    spec = importlib.util.spec_from_file_location("serpent_time_sim_job", os.path.join(root, SIM))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def clamp(fields):
    out = {}
    for k, (lo, hi) in FIELDS.items():
        if k in fields:
            v = min(hi, max(lo, float(fields[k])))
            out[k] = int(round(v)) if k in INTS else v
    return out


def every_genome(args, root):
    """(name, fields) for every genome file that exists, the hull's own policy first."""
    sim = _load_sim(root)
    out = []
    for g in genomes(args):
        f = sim.read_genome(g)
        if f:
            out.append((os.path.splitext(os.path.basename(g))[0], clamp(f)))
    return out


def space_for(root):
    sim = _load_sim(root)
    s = sim.read_genome(GENOMES[0])
    return [dict({"name": k, "lo": lo, "hi": hi, "init": clamp(s)[k]}, **({"type": "int"} if k in INTS else {}))
            for k, (lo, hi) in FIELDS.items()]


def seeds(args, root):
    """The first generation: every genome as it ships (read from the assets, never retyped)."""
    return [f for _, f in every_genome(args, root)]


def _key(ctx):
    a = ctx["args"]
    return (ctx["root"], tuple(intensities(a)), tuple(course_seeds(a)), tuple(genomes(a)))


def setup(ctx):
    a, root = ctx["args"], ctx["root"]
    sim = _load_sim(root)
    courses = {}
    for i in intensities(a):
        for s in course_seeds(a):
            c = sim.course_for(i, s)
            courses.setdefault(i, []).append((s, c, sim.flat_out(c)))
    _CTX.clear()
    _CTX.update(key=_key(ctx), sim=sim, courses=courses, genomes=every_genome(a, root),
                shipped=clamp(sim.read_genome(GENOMES[0])))


def baseline(args):
    return None   # the worker scores the space's `init`, the hull's own SerpentAIBoostPolicy


def score(rows, limit):
    v = [r["time"] / r["flat"] if r["time"] is not None else limit / r["flat"] + 2.0 * (r["of"] - r["rings"]) / max(1, r["of"])
         for r in rows]
    return sum(v) / len(v) + COMBINE_WORST * max(v)


def evaluate(params, ctx):
    a = ctx["args"]
    if _CTX.get("key") != _key(ctx):
        setup(ctx)
    sim = _CTX["sim"]
    p = clamp(dict(_CTX["shipped"], **{k: params[k] for k in FIELDS if k in params}))
    limit = float(a.get("limit", 600))
    med = lambda xs: round(sorted(xs)[len(xs) // 2], 1) if xs else None
    scores, metrics, every_finishes = {}, {}, True
    for i, races in _CTX["courses"].items():
        rows = []
        for s, c, flat in races:
            r = sim.race(c, p, limit=limit, nose_lead=a.get("nose_lead"))
            r["flat"] = flat
            rows.append(r)
        fin = [r["time"] for r in rows if r["time"] is not None]
        every_finishes &= len(fin) == len(rows)
        scores[i] = score(rows, limit)
        metrics[f"I{i}"] = {"score": round(scores[i], 4), "finished": f"{len(fin)}/{len(rows)}", "median_s": med(fin),
                            "mean_s": round(sum(fin) / len(fin), 1) if fin else None,
                            "misses": sum(r["misses"] for r in rows), "lit": round(sum(r["lit"] for r in rows) / len(rows), 2)}
    s = list(scores.values())
    combined = sum(s) / len(s) + (COMBINE_WORST * max(s) if len(s) > 1 else 0.0)
    loss = combined
    w = float(a.get("stay_near_shipped", 0.01))
    if w and _CTX["genomes"]:
        span = lambda k: FIELDS[k][1] - FIELDS[k][0]
        near = min(sum(((p[k] - g[k]) / span(k)) ** 2 for k in FIELDS if k in g) / len(FIELDS) for _, g in _CTX["genomes"])
        loss += w * near
    metrics.update(score=round(combined, 4), worst=f"I{max(scores, key=scores.get)}", params=p, limits=sim.MODEL_LIMITS)
    return {"loss": loss, "valid": every_finishes, "metrics": metrics}


def self_test():
    """The shipped policy finishes I1 and I4; a Serpent that never lights a pellet scores worse (the negative control);
    the same params give the same loss; other course seeds race other courses; the seeds are the two shipped genomes,
    read from the repo; the space's init is the hull's own policy."""
    here = os.path.dirname(os.path.abspath(__file__))
    root = os.path.dirname(os.path.dirname(os.path.dirname(here)))
    ctx = {"root": root, "args": {"intensities": [1, 4], "seeds": 1}}
    _CTX.clear()
    bad = []
    try:
        good = evaluate({}, ctx)
        never = evaluate({"reservePellets": 3, "maxConcurrentBurns": 1}, ctx)
        moved = evaluate({}, {"root": root, "args": dict(ctx["args"], seedbase=50)})
        again = evaluate({}, ctx)
        if moved["loss"] == good["loss"]:
            bad.append("serpent_fit: other course seeds raced the same courses (a stale course cache)")
        if good["metrics"]["I1"]["finished"] != "1/1" or good["metrics"]["I4"]["finished"] != "1/1":
            bad.append(f"serpent_fit: the shipped policy did not finish I1 / I4: {good['metrics']}")
        if not never["loss"] > good["loss"]:
            bad.append(f"serpent_fit: a Serpent that keeps three pellets back did not score worse ({never['loss']:.3f} vs {good['loss']:.3f})")
        if again["loss"] != good["loss"]:
            bad.append("serpent_fit: the same params gave a different loss (the sim is not deterministic)")
        sd = seeds(ctx["args"], root)
        if len(sd) != 2 or sd[0]["alignDegrees"] != 12.0 or sd[1]["alignDegrees"] != 34.29:
            bad.append(f"serpent_fit: the seeds are not SerpentAIBoostPolicy and its _SerpentTime variant ({sd})")
        if [d["init"] for d in space_for(root)][:3] != [12.0, 0.35, 1.5]:
            bad.append("serpent_fit: the space's init is not SerpentAIBoostPolicy's fields")
    except Exception as e:
        bad.append(f"serpent_fit: {type(e).__name__}: {e}")
    _CTX.clear()
    return bad


if __name__ == "__main__":
    import sys
    problems = self_test()
    print("serpent_fit self-test: " + ("ok" if not problems else "FAILED\n  " + "\n  ".join(problems)))
    sys.exit(1 if problems else 0)
