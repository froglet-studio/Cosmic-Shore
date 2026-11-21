"""The Manta's universal Time AI: ONE Soar drive (MantaSoarAITuningSO's two stick bands) scored on all four Redline
intensities at once, grown from every Manta Time genome that exists (Docs/AI_SYSTEM/AI_PLAN.md step time-arcade).

Why: Redline is the Manta's own Time game (AI_PLAN.md section 2). The job before this one (`drive-manta-redline`,
drive_fit on manta_drive_sim.py) raced Regatta's point-mass model: full throttle, Regatta's steering and no nose lag.
On Regatta's own courses that model is about 3x faster than the AI Mantas in the editor's recorded races, and
Redline flies none of the three. This one races the Redline simulator (Tools/Build/manta_redline_sim.py), which
ports what an AI Manta does in MinigameRedline: the shipped course, the gate aim, AIPilot's stick, skill throttle and
orbit break-off, the Soar triggers, and the hull's turn law with the AI nose lag. On Regatta's I1 course it laps in
28 s against the editor's Hard AI Mantas' 23-28 s (2026-11-03).

    per race       time / flat-out     if it finished    (flat-out = the course's gate-to-gate path at the full Soar, 720 u/s)
                   limit / flat-out + 2 x the rings it missed / rings      if not (a DNF always scores worse than any finish)
    per intensity  mean over races + 0.5 x the worst race
    loss           mean over the intensities + 0.5 x the worst intensity        (no intensity is given up for the others)
                 + stay_near_shipped x mean((field - nearest genome's) / range)^2

The space is the drive's own asset fields by their names (BoostStickBand, YawsteryStickBand; [Range(0, 1)] on the SO),
so a result is written into MantaSoarAITuning_Redline (MantaSoarAITuningSO.LoadForMode) without a rename. The mode's
own AI (steering, throttle floor, whether it steers for Time crystals) and its course (the Soar-only ladder, the Time
crystal field, five laps) are whatever the simulator mirrors from the job's ref: change Redline and the simulator
together, and a new epoch starts.

Races: `seeds` fixed course seeds per intensity from `seedbase` (Redline deals a new course every match, so several are
raced; fixed courses can be over-fit, so promotion re-races the winner against today's genomes on FRESH seeds).

NOT modelled (metrics.limits): manta_redline_sim.LIMITS.

args:
    hull, mode         "Manta", "Redline": read by the scout (a job for this pair is queued), not by the evaluator
    intensities        Redline intensities raced together (default 1-4)
    seeds              fixed course seeds per intensity (default 4)
    seedbase           the first of them (default 1)
    limit              seconds before a race counts as not finished (default 900: five laps at I4 run 540 s)
    genomes            repo paths of every Manta Time genome to grow from (MantaSoarAITuning_* assets; a missing file
                       is skipped, so the Redline file the AI learning sweep will write joins on its own)
    stay_near_shipped  weight of the pull toward the nearest genome (0 turns it off)
    nose_lead          the AI nose lead (AIPilot.NoseLeadGain, Tools/Build/ai_nose_lead.py): a gain stated at the fleet
                       lag, or {"gain", "max_extra_deg"}; absent or 0 = OFF, today's steering (the numbers above)
    nose_rate          the AI nose's follow rate per second (VesselTransformer.AINoseFollowPerSecond); absent = the
                       fleet lag (1.5) the model flies today
                       the roll law while the lead is on rides in nose_lead as {"gain", "max_extra_deg", "roll"}
                       ("zero" = the game's law, no roll while leading; "led" / "unled" are measured what-ifs)
"""
import importlib.util
import os
import re

VERSION = "2"   # 2 (2026-11-04): five laps, the Soar-only ladder, the Time crystal field
SIM = "Tools/Build/manta_redline_sim.py"
EXECUTOR = "Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/MantaAnalogTurnBoostExecutor.cs"
PREFAB = "Assets/_Prefabs/Spacevessels/Manta.prefab"
POL = "Assets/Resources/AIBoostPolicies/"
GENOMES = [POL + "MantaSoarAITuning_Redline.asset", POL + "MantaSoarAITuning_Regatta.asset"]
FIELDS = {"BoostStickBand": (0.0, 1.0), "YawsteryStickBand": (0.0, 1.0)}
CODE = {"BoostStickBand": "aiBoostStickBand", "YawsteryStickBand": "aiYawsteryStickBand"}   # the executor's own fields
LAPS_SOURCE = "Assets/_Scripts/Controller/Arcade/Redline/RedlineCourseSource.cs"   # the sim reads DefaultLaps here
PATHS = [SIM, EXECUTOR, PREFAB, POL, LAPS_SOURCE]
REQUEST_ARGS = {"intensities": {"subset": [1, 2, 3, 4]}, "seeds": {"lo": 1, "hi": 12}, "limit": {"lo": 300, "hi": 1500}}
REQUEST_ARGS["nose_lead"] = {"lo": 0.0, "hi": 6.0}   # the AI nose lead a manual run may switch on (0 = off)
COMBINE_WORST = 0.5
FLAT_OUT = 720.0          # RedlineCourse.MantaTopSpeed
_CTX = {}


def inputs(args):
    return [SIM, EXECUTOR, PREFAB, LAPS_SOURCE, "Tools/Build/ai_nose_lead.py"] + [g for g in genomes(args)]


def intensities(args):
    return [int(i) for i in args.get("intensities") or [1, 2, 3, 4]]


def genomes(args):
    return list(args.get("genomes") or GENOMES)


def course_seeds(args):
    n, base = int(args.get("seeds", 4)), int(args.get("seedbase", 1))
    return [base + k for k in range(n)]


# ── the genomes ─────────────────────────────────────────────────────────────────────────────────────────────────

def _fields(text, names):
    out = {}
    for name, key in names.items():
        m = re.search(r"^\s+" + re.escape(key) + r":\s*(-?[0-9.]+(?:e-?\d+)?)\s*$", text, re.M)
        if m:
            out[name] = float(m.group(1))
    return out


def shipped(root):
    """What an AI Manta flies with no MantaSoarAITuning_<mode> file: the executor's bands as Manta.prefab stores them,
    else the C# field initialisers (REDLINE.md section 6: not stored on the prefab until it is next saved)."""
    with open(os.path.join(root, PREFAB), encoding="utf-8") as fh:
        out = _fields(fh.read(), CODE)
    with open(os.path.join(root, EXECUTOR), encoding="utf-8") as fh:
        src = fh.read()
    for name, key in CODE.items():
        if name not in out:
            m = re.search(r"\b" + key + r"\s*=\s*(-?[0-9.]+)f?\s*;", src)
            if m:
                out[name] = float(m.group(1))
    return out


def read_genome(root, rel):
    path = os.path.join(root, rel)
    if not os.path.isfile(path):
        return None
    with open(path, encoding="utf-8") as fh:
        got = _fields(fh.read(), {k: k for k in FIELDS})
    return got if len(got) == len(FIELDS) else None


def every_genome(args, root):
    """(name, fields) for the shipped executor bands and every genome file that exists, the shipped one first."""
    out = [("MantaAnalogTurnBoostExecutor", shipped(root))]
    for g in genomes(args):
        f = read_genome(root, g)
        if f:
            out.append((os.path.splitext(os.path.basename(g))[0], f))
    return out


def clamp(fields):
    return {k: min(FIELDS[k][1], max(FIELDS[k][0], v)) for k, v in fields.items() if k in FIELDS}


def space_for(root):
    s = shipped(root)
    return [{"name": k, "lo": lo, "hi": hi, "init": s[k]} for k, (lo, hi) in FIELDS.items()]


def seeds(args, root):
    """The first generation: every genome as it ships (read from the assets and the executor, never retyped)."""
    return [clamp(f) for _, f in every_genome(args, root)]


# ── the evaluator contract ─────────────────────────────────────────────────────────────────────────────────────

def _load_sim(root):
    spec = importlib.util.spec_from_file_location("manta_redline_sim_job", os.path.join(root, SIM))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def _key(ctx):
    """What the cached courses and genomes depend on: a manual run may change the intensities and course seeds."""
    a = ctx["args"]
    return (ctx["root"], tuple(intensities(a)), tuple(course_seeds(a)), tuple(genomes(a)))


def setup(ctx):
    import course_port as cp
    a, root = ctx["args"], ctx["root"]
    sim = _load_sim(root)
    courses = {}
    for i in intensities(a):
        for s in course_seeds(a):
            c = cp.course(cp.redline_settings(i), cp.seed_for_intensity(20260915 + 104729 * s, i), rails=False)
            path = sum(c["legLengths"]) * sim.LAPS      # the race's laps (RedlineCourseSource.DefaultLaps)
            courses.setdefault(i, []).append((s, c, path / FLAT_OUT))
    _CTX.clear()
    _CTX.update(key=_key(ctx), sim=sim, courses=courses, genomes=every_genome(a, root), shipped=shipped(root))


def baseline(args):
    return None   # the worker scores the space's `init`, the shipped executor bands


def score(rows, limit):
    v = [r["time"] / r["flat"] if r["time"] is not None else limit / r["flat"] + 2.0 * (r["of"] - r["rings"]) / max(1, r["of"])
         for r in rows]
    return sum(v) / len(v) + COMBINE_WORST * max(v)


def evaluate(params, ctx):
    a = ctx["args"]
    if _CTX.get("key") != _key(ctx):
        setup(ctx)
    sim = _CTX["sim"]
    p = clamp({k: float(params.get(k, v)) for k, v in _CTX["shipped"].items()})
    bands = (p["BoostStickBand"], p["YawsteryStickBand"])
    limit = float(a.get("limit", 900))
    med = lambda xs: round(sorted(xs)[len(xs) // 2], 1) if xs else None
    scores, metrics, every_finishes = {}, {}, True
    for i, races in _CTX["courses"].items():
        rows = []
        for s, c, flat in races:
            r = sim.race(c, i, bands=bands, limit=limit, nose_rate=a.get("nose_rate"), nose_lead=a.get("nose_lead"))
            r["flat"] = flat
            rows.append(r)
        fin = [r["time"] for r in rows if r["time"] is not None]
        every_finishes &= len(fin) == len(rows)
        scores[i] = score(rows, limit)
        metrics[f"I{i}"] = {"score": round(scores[i], 4), "finished": f"{len(fin)}/{len(rows)}", "median_s": med(fin),
                            "soar": round(sum(r["soar"] for r in rows) / len(rows), 3),
                            "misses": sum(r["misses"] for r in rows)}
    s = list(scores.values())
    combined = sum(s) / len(s) + (COMBINE_WORST * max(s) if len(s) > 1 else 0.0)
    loss = combined
    w = float(a.get("stay_near_shipped", 0.01))
    if w:
        span = lambda k: FIELDS[k][1] - FIELDS[k][0]
        near = min(sum(((p[k] - g[k]) / span(k)) ** 2 for k in FIELDS if k in g) / len(FIELDS)
                   for _, g in _CTX["genomes"] if g)
        loss += w * near
    metrics.update(score=round(combined, 4), worst=f"I{max(scores, key=scores.get)}", limits=sim.LIMITS)
    return {"loss": loss, "valid": every_finishes, "metrics": metrics}


def self_test():
    """The shipped bands finish I1 (the simulator's own result); a Manta that never Soars scores worse (the negative
    control); the same params give the same loss; the seeds are the shipped bands and the Regatta file, read from the
    repo; the space's init is the shipped bands."""
    here = os.path.dirname(os.path.abspath(__file__))
    root = os.path.dirname(os.path.dirname(os.path.dirname(here)))
    ctx = {"root": root, "args": {"intensities": [1], "seeds": 2}}
    _CTX.clear()
    bad = []
    try:
        good = evaluate({}, ctx)
        never = evaluate({"BoostStickBand": 0.0}, ctx)
        moved = evaluate({}, {"root": root, "args": dict(ctx["args"], seedbase=50)})
        again = evaluate({}, ctx)
        if moved["loss"] == good["loss"]:
            bad.append("redline_fit: other course seeds raced the same courses (a stale course cache)")
        if good["metrics"]["I1"]["finished"] != "2/2":
            bad.append(f"redline_fit: the shipped bands did not finish I1: {good['metrics']['I1']}")
        if not never["loss"] > good["loss"]:
            bad.append(f"redline_fit: a Manta that never Soars did not score worse ({never['loss']:.3f} vs {good['loss']:.3f})")
        if again["loss"] != good["loss"]:
            bad.append("redline_fit: the same params gave a different loss (the sim is not deterministic)")
        sd = seeds(ctx["args"], root)
        if len(sd) < 2 or sd[0] != {"BoostStickBand": 0.35, "YawsteryStickBand": 0.75} or sd[1] == sd[0]:
            bad.append(f"redline_fit: the seeds are not the shipped bands and the Regatta genome ({sd})")
        if [d["init"] for d in space_for(root)] != [0.35, 0.75]:
            bad.append("redline_fit: the space's init is not the shipped executor bands")
    except Exception as e:
        bad.append(f"redline_fit: {type(e).__name__}: {e}")
    _CTX.clear()
    return bad
