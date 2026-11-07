"""The Stoat's universal Time AI: ONE set of autopilot rows (StoatSlingConfig's ai* rows, autopilotHold01 and the
trampoline aim's scale) scored on all four Slingshot intensities at once, grown from every Stoat genome that exists
(Docs/AI_SYSTEM/AI_PLAN.md step time-arcade; MINIGAME_TRAINING_PLAN.md sections 8 and 11).

Why: Slingshot is the Stoat's own Time game, and its kit has been the TRAMPOLINE since 2026-11-04 (Yash: a press lays
a white hole's soft shell ahead, a graze bounces you with the kick and the warp kept as momentum; the shell halved to
30 u with a hard core on 2026-11-07). The AI's rows were chosen by hand on stoat_orbit_sim.py's exploration of the
trampoline, which differs from the shipped C# in the bounce's kick, the pass and the predicted line
(Tools/Build/slingshot_stoat_sim.py's docstring). The editor's AI Stoats on the 60 u shell did not finish Slingshot I3
in 283 s (Yash's race of 2026-11-07 02:08, both rivals 11 and 13 rings of 16); the model of the same course, ported from
the C#, finishes it in 233-262 s with 12-21 misses.

The model is Tools/Build/slingshot_stoat_sim.py (StoatSlingExecutor's AutopilotTrampoline / LayTrampoline /
StepTrampoline / Bounce, StoatSlingMath's trampoline, the flight clock, the gate steering of GateRaceController with
AICommitFromApproachPoint, AIPilot's stick, nose lag and orbit break-off). Every kit number is read from the job's ref
(StoatSlingConfig, StoatDipoleConfig, the scene's approach numbers, StoatSlingMath's hard core): change the kit and a new
epoch starts.

    per race       time / cruise          if it finished    (cruise = the race's gate-to-gate path at the Stoat's 60 u/s)
                   limit / cruise + 2 x the rings it missed / rings      if not (a DNF always scores worse than a finish)
    per intensity  mean over races + 0.5 x the worst race
    loss           mean over the intensities + 0.5 x the worst intensity        (no intensity is given up for the others)
                 + stay_near_shipped x min over genomes of mean((field - genome's) / range)^2

The space is the asset's own field names (a result is written into StoatSlingConfig_Slingshot, AIModeTuning.ForPilot,
without a rename), bounded by the SO's [Range] / [Min] and narrowed where a value past them only switches the AI off.
aiWarpMinDistance is searched only where the C# at the job's ref presses for the warp on the trampoline
(StoatSlingExecutor.AutopilotTrampoline reading AiWarpMinDistance, as the orbit's AutopilotSling did); elsewhere it does
nothing and is held at the genome's value. aiReleaseConeDegrees, aiCatchGiveUpSeconds and aiHoldForWindowDegrees are the
orbit's: the trampoline AI never reads them.

Races: `seeds` fixed course seeds per intensity from `seedbase`, each from a seat of the equatorial spawn ring
(Slingshot deals a new course every match; promotion re-races the winner on FRESH seeds).

NOT modelled (metrics.limits): slingshot_stoat_sim.LIMITS.

args:
    hull, mode         "Stoat", "Slingshot": read by the scout, not by the evaluator
    intensities        Slingshot intensities raced together (default 1-4)
    seeds              fixed course seeds per intensity (default 6)
    seedbase           the first of them (default 0)
    limit              seconds before a race counts as not finished (default 300: two laps at cruise run 140-150 s)
    dt                 the model's step (default 1/60, the frame the C# assumes)
    warp_press         "auto" (read the C# at the ref), true or false
    genomes            repo paths of every Stoat sling genome to grow from (a missing file is skipped)
    grow               false: the first generation is the space's init alone (an independent restart)
    stay_near_shipped  weight of the pull toward the nearest genome (0 turns it off)
"""
import importlib.util
import os
import re

VERSION = "1"
SIM = "Tools/Build/slingshot_stoat_sim.py"
EXECUTOR = "Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/StoatSlingExecutor.cs"
POL = "Assets/Resources/AIBoostPolicies/"
HULL = "Assets/_SO_Assets/VesselActions/Stoat/StoatSlingConfig.asset"
GENOMES = [POL + "StoatSlingConfig_Slingshot.asset", HULL, POL + "StoatSlingConfig_Regatta.asset",
           POL + "StoatSlingConfig_Warpline.asset"]
# the searched rows: (lo, hi). aiSlingMinTurnDegrees 0 switches the autopilot off (AutopilotTrampoline returns), so its
# floor is 10; the interval past 8 s and the distances past the course's longest leg change nothing.
FIELDS = {"aiSlingMinTurnDegrees": (10.0, 180.0), "aiSlingIntervalSeconds": (0.5, 8.0), "aiSlingMinDistance": (0.0, 800.0),
          "trampolineAiTurnScale": (40.0, 300.0), "autopilotHold01": (0.0, 1.0), "aiWarpMinDistance": (0.0, 1500.0)}
REQUEST_ARGS = {"intensities": {"subset": [1, 2, 3, 4]}, "seeds": {"lo": 1, "hi": 16}, "limit": {"lo": 200, "hi": 900}}
COMBINE_WORST = 0.5
_CTX = {}


def _sim_paths():
    return [SIM, "Tools/Build/stoat_orbit_sim.py", "Tools/Build/regatta_line_sim.py", "Tools/Build/rhino_drive_sim.py",
            "Tools/Build/scarab_drive_sim.py", "Tools/Build/situation_features.py", "Tools/Compute/evaluators/course_port.py"]


def _load_sim(root):
    import sys
    build = os.path.join(root, "Tools", "Build")
    if build not in sys.path:
        sys.path.insert(0, build)
    spec = importlib.util.spec_from_file_location("slingshot_stoat_sim_job", os.path.join(root, SIM))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


PATHS = [SIM, "Tools/Build/stoat_orbit_sim.py", "Tools/Build/regatta_line_sim.py", "Tools/Build/rhino_drive_sim.py",
         "Tools/Build/scarab_drive_sim.py", "Tools/Build/situation_features.py", EXECUTOR,
         "Assets/_Scripts/Controller/Vessel/R_VesselActions/StoatSlingMath.cs",
         "Assets/_Scripts/Controller/Vessel/R_VesselActions/Data Containers/StoatSlingConfigSO.cs",
         "Assets/_SO_Assets/VesselActions/Stoat/", POL, "Assets/_Scenes/Multiplayer Scenes/MinigameSlingshot.unity"]


def inputs(args):
    return _sim_paths() + [EXECUTOR, "Assets/_Scripts/Controller/Vessel/R_VesselActions/StoatSlingMath.cs",
                           "Assets/_Scripts/Controller/Vessel/R_VesselActions/Data Containers/StoatSlingConfigSO.cs",
                           HULL, "Assets/_SO_Assets/VesselActions/Stoat/StoatDipoleConfig.asset",
                           "Assets/_Scenes/Multiplayer Scenes/MinigameSlingshot.unity"] + genomes(args)


def intensities(args):
    return [int(i) for i in args.get("intensities") or [1, 2, 3, 4]]


def genomes(args):
    return list(args.get("genomes") or GENOMES)


def course_seeds(args):
    n, base = int(args.get("seeds", 6)), int(args.get("seedbase", 0))
    return [base + k for k in range(n)]


def warp_press(args, root):
    """Whether the C# at `root` presses for the warp alone on the trampoline (AutopilotTrampoline reads AiWarpMinDistance)."""
    w = args.get("warp_press", "auto")
    if w != "auto":
        return bool(w)
    path = os.path.join(root, EXECUTOR)
    if not os.path.isfile(path):
        return False
    with open(path, encoding="utf-8") as fh:
        src = fh.read()
    m = re.search(r"void AutopilotTrampoline\(.*?\n        }\n", src, re.S)
    return bool(m and "AiWarpMinDistance" in m.group(0))


def space(args, root):
    """The searched fields: aiWarpMinDistance only where the C# reads it on the trampoline."""
    return [k for k in FIELDS if k != "aiWarpMinDistance" or warp_press(args, root)]


# ── the genomes ─────────────────────────────────────────────────────────────────────────────────────────────────

def every_genome(args, root, sim=None):
    """(name, rows) for every genome file that exists, the Slingshot copy first (what an AI Stoat flies there today)."""
    sim = sim or _load_sim(root)
    out = []
    for g in genomes(args):
        if os.path.isfile(os.path.join(root, g)):
            out.append((os.path.splitext(os.path.basename(g))[0], sim.ai_rows(root, g)))
    return out


def clamp(rows):
    return {k: min(FIELDS[k][1], max(FIELDS[k][0], float(v))) for k, v in rows.items() if k in FIELDS and v is not None}


def space_for(root, args=None):
    args = args or {}
    sim = _load_sim(root)
    g = every_genome(args, root, sim)
    init = clamp(g[0][1]) if g else {}
    return [{"name": k, "lo": FIELDS[k][0], "hi": FIELDS[k][1], "init": init.get(k)} for k in space(args, root)]


def seeds(args, root):
    """The first generation: every genome as it ships (read from the assets, never retyped). grow=false: none, so the
    search starts from the space's init alone (an independent restart)."""
    if args.get("grow") is False:
        return []
    keep = set(space(args, root))
    return [{k: v for k, v in clamp(rows).items() if k in keep} for _, rows in every_genome(args, root)]


# ── the evaluator contract ─────────────────────────────────────────────────────────────────────────────────────

def _key(ctx):
    a = ctx["args"]
    return (ctx["root"], tuple(intensities(a)), tuple(course_seeds(a)), tuple(genomes(a)), str(a.get("warp_press", "auto")))


def setup(ctx):
    a, root = ctx["args"], ctx["root"]
    sim = _load_sim(root)
    k = dict(sim.kit(root), warp_press=warp_press(a, root))
    gs = every_genome(a, root, sim)
    courses = {}
    for i in intensities(a):
        for s in course_seeds(a):
            gates = sim.course(i, 20260000 + 97 * s + i)
            start = sim.spawn(s)
            path = sim.norm(sim.sub(gates[0]["p"], start[0])) + sim.S.LAPS * sum(
                sim.norm(sim.sub(gates[(j + 1) % len(gates)]["p"], gates[j]["p"])) for j in range(len(gates)))
            courses.setdefault(i, []).append((s, gates, start, path / sim.S.CRUISE))
    _CTX.clear()
    _CTX.update(key=_key(ctx), sim=sim, kit=k, courses=courses, genomes=gs, base=dict(gs[0][1]) if gs else sim.ai_rows(root))


def baseline(args):
    return None   # the worker scores the space's `init`: the Slingshot copy's rows as they ship


def score(rows, limit):
    v = [r["time"] / r["cruise"] if r["time"] is not None else limit / r["cruise"] + 2.0 * (r["of"] - r["rings"]) / max(1, r["of"])
         for r in rows]
    return sum(v) / len(v) + COMBINE_WORST * max(v)


def evaluate(params, ctx):
    a = ctx["args"]
    if _CTX.get("key") != _key(ctx):
        setup(ctx)
    sim, k = _CTX["sim"], _CTX["kit"]
    ai = dict(_CTX["base"])
    ai.update(clamp({kk: params[kk] for kk in FIELDS if kk in params}))
    limit, dt = float(a.get("limit", 300)), float(a.get("dt", 1 / 60))
    med = lambda xs: round(sorted(xs)[len(xs) // 2], 1) if xs else None
    scores, metrics, every_finishes = {}, {}, True
    for i, races in _CTX["courses"].items():
        rows = []
        for s, gates, start, cruise in races:
            r = sim.race(gates, k, ai, T=limit, dt=dt, start=start)
            r["cruise"] = cruise
            rows.append(r)
        fin = [r["time"] for r in rows if r["time"] is not None]
        every_finishes &= len(fin) == len(rows)
        scores[i] = score(rows, limit)
        metrics[f"I{i}"] = {"score": round(scores[i], 4), "finished": f"{len(fin)}/{len(rows)}", "median_s": med(fin),
                            "misses": sum(r["misses"] for r in rows), "bounces": round(sum(r["bounces"] for r in rows) / len(rows), 1),
                            "presses": round(sum(r["presses"] for r in rows) / len(rows), 1),
                            "cruise_s": round(sum(r["cruise"] for r in rows) / len(rows), 1)}
    s = list(scores.values())
    combined = sum(s) / len(s) + (COMBINE_WORST * max(s) if len(s) > 1 else 0.0)
    loss = combined
    w = float(a.get("stay_near_shipped", 0.01))
    if w and _CTX["genomes"]:
        span = lambda f: FIELDS[f][1] - FIELDS[f][0]
        keep = [f for f in FIELDS if f in params]
        if keep:
            near = min(sum(((ai[f] - clamp(g).get(f, ai[f])) / span(f)) ** 2 for f in keep) / len(keep)
                       for _, g in _CTX["genomes"])
            loss += w * near
    metrics.update(score=round(combined, 4), worst=f"I{max(scores, key=scores.get)}", warp_press=k["warp_press"],
                   limits=sim.LIMITS)
    return {"loss": loss, "valid": every_finishes, "metrics": metrics}


def self_test():
    """The shipped rows score a finite loss on I2; an AI that never presses (aiSlingMinTurnDegrees at its floor is still a
    press rule, so the control holds the interval at 8 s and the distance past every leg) cruises and finishes; the same
    params give the same loss; other course seeds race other courses; the seeds are read from the repo's genomes; the
    warp press is detected from the C#, and forcing it off holds aiWarpMinDistance out of the space."""
    here = os.path.dirname(os.path.abspath(__file__))
    root = os.path.dirname(os.path.dirname(os.path.dirname(here)))
    ctx = {"root": root, "args": {"intensities": [2], "seeds": 2}}
    _CTX.clear()
    bad = []
    try:
        good = evaluate({}, ctx)
        lazy = evaluate({"aiSlingMinDistance": 800.0, "aiSlingIntervalSeconds": 8.0, "aiWarpMinDistance": 0.0}, ctx)
        moved = evaluate({}, {"root": root, "args": dict(ctx["args"], seedbase=50)})
        again = evaluate({}, ctx)
        if not good["loss"] < float("inf"):
            bad.append(f"slingshot_fit: the shipped rows scored {good['loss']}")
        if lazy["metrics"]["I2"]["finished"] != "2/2" or lazy["metrics"]["I2"]["presses"] > 2:
            bad.append(f"slingshot_fit: a Stoat that hardly presses should cruise and finish: {lazy['metrics']['I2']}")
        if moved["loss"] == good["loss"]:
            bad.append("slingshot_fit: other course seeds raced the same courses (a stale course cache)")
        if again["loss"] != good["loss"]:
            bad.append("slingshot_fit: the same params gave a different loss (the sim is not deterministic)")
        sd = seeds(ctx["args"], root)
        if not sd or any(not (FIELDS[f][0] <= v <= FIELDS[f][1]) for s_ in sd for f, v in s_.items()):
            bad.append(f"slingshot_fit: the seeds are not the repo's genomes inside the space ({sd})")
        if seeds(dict(ctx["args"], grow=False), root):
            bad.append("slingshot_fit: grow=false should start from the init alone")
        if "aiWarpMinDistance" in space({"warp_press": False}, root):
            bad.append("slingshot_fit: aiWarpMinDistance is searched where the C# does not read it")
        if not set(space({"warp_press": True}, root)) == set(FIELDS):
            bad.append("slingshot_fit: the full space is not the six rows")
        init = {d["name"]: d["init"] for d in space_for(root)}
        if init.get("aiSlingMinTurnDegrees") is None:
            bad.append(f"slingshot_fit: the space's init is not read from StoatSlingConfig_Slingshot ({init})")
    except Exception as e:
        bad.append(f"slingshot_fit: {type(e).__name__}: {e}")
    _CTX.clear()
    return bad
