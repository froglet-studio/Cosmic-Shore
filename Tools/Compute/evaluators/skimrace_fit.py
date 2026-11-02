"""The Squirrel's universal Time AI: ONE Skim Race policy scored on all four intensities at once, grown from every
genome that exists (Docs/AI_SYSTEM/AI_PLAN.md step time-arcade, Garrett 2026-11-02).

Why: the game carries three Squirrel genomes for Skim Race - the general policy (`SkimRaceAIConfig`, flown at I3, at
I4 since its own file went stale, and anywhere a map changed) and the I1 and I2 files - each fitted to one track. The
plan wants one AI per vessel and element that races every intensity well, so the next round searches one policy whose
loss is the four tracks' scores together, and its first generation is the three genomes and mutations of each.

The model is the SHIPPED pilot core (Assets/_Scripts/Controller/AI/SkimRace/*.cs) compiled against the harness's Unity
shim (Tools/Build/skimrace_sim_harness/Sim.cs), racing the scene's own tracks. Nothing is copied: the evaluator builds
the harness from the job's ref (run.sh's build, in Python so it runs on Windows too) and parses `eval`'s race lines.

    per track = tuneall's own score (Sim.cs TuneAll.Score):
                  per race  time / ideal                     if it finished
                            cut / ideal + 2 x missed share    if not (a DNF always scores worse than any finish)
                  mean over races + 0.5 x the worst race
    loss      = mean over the intensities + 0.5 x the worst intensity     (no track is given up for the others)
              + stay_near_shipped x mean((field - genome's) / range)^2     never wander for nothing

A point is a GENOME (which shipped asset lies underneath: a `genome` choice in the space, so a genome's fields the
search does not move stay its own) plus the searched fields over it. Races: `seeds` fixed seeds from `seedbase` per
intensity, 2 AI seats, 16/28/50 ms frames +-50%, contacts on the 0.04 s fixed step (skimrace_retune.PHYSICS), raced
to 3x ideal (at least the intensity's limit), cut 60 s later - tuneall's conditions. The space is the pursuit fields
any genome's author states (tuneall `only=stated`), inside the harness's own Ranges: a re-fit of the numbers, never a
control switched on or off (a genome's own switches come with the genome).

NOT modelled (metrics.limits): the editor (the sim is calibrated against it, Docs/SKIM_RACE_AI.md 6-8, and the editor is
the measurement); human opponents; more than 2 seats; frame hitches beyond the jitter. Fixed seeds can be over-fit:
promotion re-races the winner against today's genomes on FRESH races (seedbase 99000), the retune's own gate.

args:
    intensities        the Skim Race intensities whose scene tracks are raced together (default 1-4; `intensity`
                       alone races one, for a manual run)
    seeds              races per intensity per evaluation (fixed: same params, same loss)
    seedbase           the first seed
    policy             repo path of the anchor genome (the general policy): the space's `init`, the board's baseline
    genomes            repo paths of every genome to grow from and choose between (default: the policy alone)
    stay_near_shipped  weight of the pull toward the chosen genome's values (0 turns it off)
"""
import glob
import hashlib
import json
import math
import os
import re
import shutil
import subprocess
import sys

import compute_lib as L

VERSION = "2"
HARNESS = "Tools/Build/skimrace_sim_harness/"
SR = "Assets/_Scripts/Controller/AI/SkimRace/"
SOURCES = [HARNESS + "UnityShim.cs", HARNESS + "Sim.cs"] + [SR + n + ".cs" for n in (
    "SkimRaceAIConfigSO", "SkimRaceCourse", "SkimRaceObservation", "SkimRaceDriver", "SkimRaceShell", "SkimRacePlanner",
    "SkimRaceObstacle", "SkimRaceHandicap", "SkimRaceTrackFingerprint", "SkimRaceTargetTracker", "SkimRaceTeamAssignment",
    "SkimRaceReplanGate")] + ["Assets/_Scripts/Utility/Foundation/MathfNoAlloc.cs"]       # run.sh's files.rsp, in order
SCENE = "Assets/_Scenes/Multiplayer Scenes/MinigameSkimRace.unity"
FINGERPRINT = "Tools/Build/skimrace_track_fingerprint.py"
AUTHOR = "Tools/Build/author_skimrace_ai_config.py"     # which fields each genome's author states (the space)
GENERAL = "Assets/Resources/SkimRaceAIConfig.asset"
PATHS = [HARNESS, SR, "Assets/_Scripts/Utility/Foundation/MathfNoAlloc.cs", SCENE, FINGERPRINT, AUTHOR,
         "Assets/Resources/SkimRaceAIConfig.asset", "Assets/Resources/SkimRaceAIConfig_I1.asset",
         "Assets/Resources/SkimRaceAIConfig_I2.asset", "Assets/Resources/SkimRaceAIConfig_I4.asset"]
PHYSICS = ["ph.Seats=2", "ph.Dt=0.028", "ph.DtJitter=0.5", "ph.PhysicsStep=0.04", "dts=0.016,0.028,0.05"]
EXTRA_TIME = 60.0       # Sim.cs Physics.ExtraTime: a race is cut at limit + this
LIMITS = ("offline sim of the shipped Skim Race pilot core on the scene's track, calibrated against the editor "
          "(Docs/SKIM_RACE_AI.md 6-8); 2 AI seats, no people; fixed seeds - promotion re-races fresh ones")
REQUEST_ARGS = {"intensity": {"choices": [1, 2, 3, 4]}, "seeds": {"lo": 2, "hi": 40}}
COMBINE_WORST = 0.5     # loss = mean over intensities + this x the worst one (tuneall's own shape, one level up)
_CTX = {}
_RACE = re.compile(r"^\s*(FIN|DNF) t=([0-9.]+) (\d+)/(\d+) .*seats=\[([^\]]*)\]")


def inputs(args):
    return SOURCES + [SCENE, FINGERPRINT, AUTHOR] + sorted(set(genomes(args)))


def intensities(args):
    if args.get("intensities"):
        return [int(i) for i in args["intensities"]]
    return [int(args["intensity"])] if args.get("intensity") else [1, 2, 3, 4]


def genomes(args):
    """Every genome's repo path, the anchor first."""
    anchor = args.get("policy", GENERAL)
    return [anchor] + [g for g in args.get("genomes") or [] if g != anchor]


def genome_name(rel):
    return os.path.splitext(os.path.basename(rel))[0]


# ── the harness, built once per machine per source revision ───────────────────

def _dotnet():
    exe = shutil.which("dotnet")
    roots = [os.environ.get("DOTNET_ROOT"), os.path.join(os.path.expanduser("~"), ".dotnet"),
             os.path.dirname(os.path.realpath(exe)) if exe else None]
    for r in [r for r in roots if r]:
        csc = sorted(glob.glob(os.path.join(r, "sdk", "*", "Roslyn", "bincore", "csc.dll")))
        refs = sorted(glob.glob(os.path.join(r, "packs", "Microsoft.NETCore.App.Ref", "*", "ref", "net*")))
        shared = os.path.join(r, "shared", "Microsoft.NETCore.App")
        if csc and refs and os.path.isdir(shared):
            dn = os.path.join(r, "dotnet.exe" if os.name == "nt" else "dotnet")
            vers = sorted(os.listdir(shared), key=lambda s: [int(x) for x in re.findall(r"\d+", s)[:3]])
            return (dn if os.path.exists(dn) else exe), csc[-1], refs[-1], vers[-1]
    raise RuntimeError("no .NET SDK with Roslyn found (needs a dotnet 8+ SDK; DOTNET_ROOT or ~/.dotnet)")


def build(root):
    """sim.dll + track.txt + info.json under the cache, keyed by every input's bytes: a new revision builds anew."""
    h = hashlib.sha1(VERSION.encode())
    for rel in SOURCES + [SCENE, FINGERPRINT]:
        with open(os.path.join(root, rel), "rb") as fh:
            h.update(rel.encode() + b"\0" + fh.read())
    out = os.path.join(L.cache_dir(), "skimrace_sim", h.hexdigest()[:16])
    if os.path.exists(os.path.join(out, "info.json")):
        return out
    dotnet, csc, refdir, ver = _dotnet()
    tmp = f"{out}.tmp{os.getpid()}"
    os.makedirs(tmp, exist_ok=True)
    with open(os.path.join(tmp, "build.rsp"), "w", encoding="utf-8") as fh:
        for d in sorted(glob.glob(os.path.join(refdir, "*.dll"))):
            fh.write(f'-r:"{d}"\n')
        for rel in SOURCES:
            fh.write(f'"{os.path.join(root, rel)}"\n')
    p = subprocess.run([dotnet, csc, "-nologo", "-langversion:latest", "-nostdlib", "-noconfig", "-optimize+",
                        "-nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632,CS0108,CS1587", "-target:exe",
                        "-main:Program", "-out:" + os.path.join(tmp, "sim.dll"), "@" + os.path.join(tmp, "build.rsp")],
                       capture_output=True, text=True, **L.NO_WINDOW)
    if p.returncode:
        raise RuntimeError("the Skim Race sim did not compile: " + (p.stdout + p.stderr)[-1500:])
    with open(os.path.join(tmp, "sim.runtimeconfig.json"), "w", encoding="utf-8") as fh:
        json.dump({"runtimeOptions": {"tfm": "net%s.0" % ver.split(".")[0],
                                      "framework": {"name": "Microsoft.NETCore.App", "version": ver}}}, fh)
    p = subprocess.run([sys.executable, os.path.join(root, FINGERPRINT), "--scene", os.path.join(root, SCENE),
                        "--emit-track", os.path.join(tmp, "track.txt")], capture_output=True, text=True, **L.NO_WINDOW)
    if p.returncode:
        raise RuntimeError("the track reader failed: " + (p.stdout + p.stderr)[-800:])
    info = {"dotnet": dotnet, "fingerprints": {}, "tracks": {}}
    for line in _sim(tmp, dotnet, ["fingerprint"]).splitlines():
        m = re.match(r"I(\d+): (\w+)", line.strip())
        if m:
            info["fingerprints"][m.group(1)] = m.group(2)
    for i in info["fingerprints"]:          # tuneall with no steps prints each track's ideal and race limit, nothing else
        m = re.search(r"I\d+: ideal ([0-9.]+) s, raced to ([0-9.]+) s", _sim(tmp, dotnet, ["tuneall", i, "1", "0", "final=0"]))
        if m:
            info["tracks"][i] = {"ideal": float(m.group(1)), "limit": float(m.group(2))}
    with open(os.path.join(tmp, "info.json"), "w", encoding="utf-8") as fh:
        json.dump(info, fh, indent=1)
    try:
        os.replace(tmp, out)                # another pool process may have won the race: theirs is identical
    except OSError:
        shutil.rmtree(tmp, ignore_errors=True)
    return out


def _sim(folder, dotnet, args):
    # One core per evaluation: the worker runs one evaluation per core, and the sim's Parallel.For would oversubscribe.
    env = dict(os.environ, DOTNET_PROCESSOR_COUNT="1", DOTNET_CLI_TELEMETRY_OPTOUT="1")
    p = subprocess.run([dotnet, os.path.join(folder, "sim.dll"), os.path.join(folder, "track.txt")] + [str(a) for a in args],
                       capture_output=True, text=True, cwd=folder, env=env, **L.NO_WINDOW)
    if p.returncode:
        raise RuntimeError(f"sim {args[:3]} exited {p.returncode}: " + (p.stdout + p.stderr)[-800:])
    return p.stdout


# ── the policy: the asset's values, and the space tuneall would search ─────────

def read_policy(root, rel):
    """The asset's numeric and bool fields as the sim's Field=value strings take them (bools as 0/1)."""
    out = {}
    with open(os.path.join(root, rel), encoding="utf-8") as fh:
        body = fh.read().split("m_EditorClassIdentifier:", 1)[-1]
    for k, v in re.findall(r"^  (\w+): (-?[0-9.]+(?:e-?\d+)?)\s*$", body, re.M):
        out[k] = float(v)
    return out


def ranges(root):
    """Sim.cs's own Ranges table and PursuitTunables list (the bounds every retune has searched within)."""
    with open(os.path.join(root, HARNESS, "Sim.cs"), encoding="utf-8") as fh:
        src = fh.read()
    table = src[src.index("Dictionary<string, (float lo, float hi)> Ranges"):]
    table = table[:table.index("};")]
    rng = {k: (float(lo.rstrip("f")), float(hi.rstrip("f")))
           for k, lo, hi in re.findall(r'\["(\w+)"\]\s*=\s*\(\s*(-?[0-9.]+f?)\s*,\s*(-?[0-9.]+f?)\s*\)', table)}
    pursuit = src[src.index("static readonly string[] PursuitTunables"):]
    pursuit = re.findall(r'"(\w+)"', pursuit[:pursuit.index("};")])
    return rng, pursuit


def int_fields(root):
    with open(os.path.join(root, SR, "SkimRaceAIConfigSO.cs"), encoding="utf-8") as fh:
        return set(re.findall(r"public int (\w+)", fh.read()))


def space_for(root, policy_rel, others=()):
    """tuneall only=stated: the pursuit fields the policy's AUTHOR states, whose shipped value sits inside its range,
    with `init` the policy's own value. With `others` (more genomes), also every field any of THEIR authors state,
    and a `genome` choice first: which asset lies underneath (init: this policy)."""
    shipped = read_policy(root, policy_rel)
    rng, pursuit = ranges(root)
    ints = int_fields(root)
    stated = _stated(root, policy_rel)
    for g in others:
        stated |= _stated(root, g)
    out = []
    for k in pursuit:
        if k in stated and k in shipped and k in rng and rng[k][0] <= shipped[k] <= rng[k][1]:
            d = {"name": k, "lo": rng[k][0], "hi": rng[k][1], "init": shipped[k]}
            if k in ints:
                d.update(type="int", lo=int(rng[k][0]), hi=int(rng[k][1]), init=int(shipped[k]))
            out.append(d)
    names = [genome_name(g) for g in [policy_rel] + [g for g in others if g != policy_rel]]
    if len(names) > 1:
        out.insert(0, {"name": "genome", "type": "choice", "choices": names, "init": names[0]})
    return out


def seeds(args, root):
    """The first generation's genomes: each asset in `genomes` as it ships, on the space's fields (clamped into the
    harness's ranges). Read from the assets, never retyped."""
    gs = genomes(args)
    if len(gs) < 2:
        return []
    rng, _ = ranges(root)
    out = []
    for g in gs:
        p = {k: min(rng[k][1], max(rng[k][0], v)) for k, v in read_policy(root, g).items() if k in rng}
        p["genome"] = genome_name(g)
        out.append(p)
    return out


def _stated(root, policy_rel):
    """The fields author_skimrace_ai_config.py states for this asset (its POLICIES entry)."""
    import importlib.util
    spec = importlib.util.spec_from_file_location("_author_skimrace", os.path.join(root, "Tools", "Build", "author_skimrace_ai_config.py"))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return set(mod.POLICIES.get(os.path.splitext(os.path.basename(policy_rel))[0], {}))


# ── the evaluator contract ─────────────────────────────────────────────────────

def setup(ctx):
    a, root = ctx["args"], ctx["root"]
    folder = build(root)
    with open(os.path.join(folder, "info.json"), encoding="utf-8") as fh:
        info = json.load(fh)
    want = [str(i) for i in intensities(a)]
    missing = [i for i in want if i not in info["tracks"]]
    if missing:
        raise RuntimeError(f"the scene has no Skim Race intensity {missing} (tracks: {sorted(info['tracks'])})")
    _CTX.update(folder=folder, dotnet=info["dotnet"], tracks={i: info["tracks"][i] for i in want},
                fingerprints={i: info["fingerprints"].get(i) for i in want},
                genomes={genome_name(g): read_policy(root, g) for g in genomes(a)},
                anchor=genome_name(genomes(a)[0]), ints=int_fields(root), root=root)


def baseline(args):
    return None   # the worker scores the space's `init` values, which the job sets to the shipped asset's numbers


def race(params, ctx, intensity, seeds=None, seedbase=None):
    """Race the chosen genome with `params` over it on one intensity's track; returns per-race rows and tuneall's
    score."""
    a = ctx["args"]
    if _CTX.get("root") != ctx["root"]:
        setup(ctx)
    params = dict(params)
    policy = dict(_CTX["genomes"][params.pop("genome", _CTX["anchor"])], **params)
    kv = [f"{k}={int(round(v)) if k in _CTX['ints'] else (round(v, 6) if isinstance(v, float) else v)}"
          for k, v in sorted(policy.items())]
    track = _CTX["tracks"][str(intensity)]
    ideal, limit = track["ideal"], track["limit"]
    n = int(seeds or a.get("seeds", 8))
    out = _sim(_CTX["folder"], _CTX["dotnet"], ["eval", int(intensity), n] + kv + [
        f"limit={limit:g}", f"seedbase={int(seedbase if seedbase is not None else a.get('seedbase', 5000))}"] + PHYSICS)
    rows = []
    for line in out.splitlines():
        m = _RACE.match(line)
        if m:
            seats = [float(x) for x in m.group(5).split()]
            rows.append({"finished": m.group(1) == "FIN", "time": float(m.group(2)), "collected": int(m.group(3)),
                         "required": int(m.group(4)), "winner": min(seats) if seats else float(m.group(2))})
    if len(rows) != n:
        raise RuntimeError(f"expected {n} race lines from the sim, read {len(rows)}")
    cut = limit + EXTRA_TIME
    v = [r["time"] / ideal if r["finished"] else cut / ideal + 2.0 * (r["required"] - r["collected"]) / max(1, r["required"])
         for r in rows]
    return rows, sum(v) / len(v) + 0.5 * max(v)


def evaluate(params, ctx):
    a = ctx["args"]
    if _CTX.get("root") != ctx["root"]:
        setup(ctx)
    med = lambda xs: sorted(xs)[len(xs) // 2] if xs else None
    scores, metrics, every_finishes = {}, {}, True
    for i in intensities(a):
        rows, score = race(params, ctx, i)
        fin = [r for r in rows if r["finished"]]
        every_finishes &= bool(fin)
        scores[i] = score
        winner = med([r["winner"] for r in fin])
        ideal = _CTX["tracks"][str(i)]["ideal"]
        metrics[f"I{i}"] = {"score": round(score, 4), "finished": f"{len(fin)}/{len(rows)}", "winner_median_s": winner,
                            "winner_x_ideal": round(winner / ideal, 3) if winner else None, "ideal_s": ideal,
                            "fingerprint": _CTX["fingerprints"][str(i)]}
    s = list(scores.values())
    combined = sum(s) / len(s) + (COMBINE_WORST * max(s) if len(s) > 1 else 0.0)
    loss = combined
    w = float(a.get("stay_near_shipped", 0.01))
    base = _CTX["genomes"][params.get("genome", _CTX["anchor"])]
    dims = {d["name"]: d for d in a.get("_space", []) if d.get("type", "float") != "choice"}
    pen = [((v - base[k]) / (dims[k]["hi"] - dims[k]["lo"])) ** 2
           for k, v in params.items() if k in dims and k in base and dims[k]["hi"] > dims[k]["lo"]]
    if w and pen:
        loss += w * sum(pen) / len(pen)
    metrics.update(score=round(combined, 4), genome=params.get("genome", _CTX["anchor"]),
                   worst=f"I{max(scores, key=scores.get)}", limits=LIMITS)
    return {"loss": loss, "valid": every_finishes, "metrics": metrics}


def self_test():
    """The shipped general policy finishes today's I1 (the sim reproduces its published result: 20/20 on fresh races,
    Docs/SKIM_RACE_AI.md 6.12), a broken policy (no throttle, no lookahead) scores worse (the negative control), the
    genome choice really changes what races (the I2 genome underneath is not the general one), and the seeds are the
    shipped genomes themselves."""
    here = os.path.dirname(os.path.abspath(__file__))
    root = os.path.dirname(os.path.dirname(os.path.dirname(here)))
    try:
        _dotnet()
    except RuntimeError as e:
        print(f"  skimrace_fit: skipped ({e})")
        return []
    gs = [GENERAL, "Assets/Resources/SkimRaceAIConfig_I1.asset", "Assets/Resources/SkimRaceAIConfig_I2.asset"]
    ctx = {"root": root, "args": {"intensities": [1], "seeds": 3, "policy": GENERAL, "genomes": gs}}
    _CTX.clear()
    bad = []
    try:
        good = evaluate({}, ctx)
        broken = evaluate({"MinThrottle": 0.0, "RecoveryThrottle": 0.0, "LookaheadMax": 30.0, "LookaheadMin": 5.0,
                           "StickGainPerDegree": 0.005}, ctx)
        again = evaluate({}, ctx)
        other = evaluate({"genome": "SkimRaceAIConfig_I2"}, ctx)
        if good["metrics"]["I1"]["finished"] != "3/3":
            bad.append(f"skimrace_fit: the shipped general policy did not finish I1: {good['metrics']}")
        if not broken["loss"] > good["loss"]:
            bad.append(f"skimrace_fit: a broken policy did not score worse ({broken['loss']:.3f} vs {good['loss']:.3f})")
        if again["loss"] != good["loss"]:
            bad.append("skimrace_fit: the same params gave a different loss (the sim is not deterministic)")
        if other["loss"] == good["loss"]:
            bad.append("skimrace_fit: the genome choice changed nothing (the I2 genome raced as the general one)")
        sp = space_for(root, GENERAL, gs[1:])
        if sp[0].get("name") != "genome" or len(sp) < 20 or any(
                d.get("type") != "choice" and not (d["lo"] <= d["init"] <= d["hi"]) for d in sp):
            bad.append(f"skimrace_fit: the genomes' space is wrong ({len(sp)} fields)")
        sd = seeds(ctx["args"], root)
        if [x["genome"] for x in sd] != [genome_name(g) for g in gs] or sd[1] == sd[0]:
            bad.append("skimrace_fit: the seeds are not the three shipped genomes")
    except Exception as e:
        bad.append(f"skimrace_fit: {type(e).__name__}: {e}")
    _CTX.clear()
    return bad
