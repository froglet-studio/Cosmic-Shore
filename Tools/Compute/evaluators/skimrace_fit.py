"""The Squirrel's Skim Race AI on ONE intensity's track: the policy `skimrace_retune.py` would fit, searched by every
machine instead of one.

Why (measured 2026-11-01 on bleeding-edge 25709d4c64): the scene's I3 and I4 maps no longer match what the shipped
policies were tuned on. I3 has no file of its own and flies the general policy (`skimrace-v3-general`), whose I3
track then had a 65.6 s ideal; today's I3 is 32.8 s ideal and the general policy races it in ~90 s (2.7x ideal). I4's
file records fingerprint 227b9055 and the scene's is efdff5b2, so the deployment falls back to the general policy there
too (Docs/SKIM_RACE_AI.md section 11). The retune is the cure the doc names; a single retune is 4 seeds x 16
cross-entropy steps on one machine (~45 min). This job is the same search, shared.

The model is the SHIPPED pilot core (Assets/_Scripts/Controller/AI/SkimRace/*.cs) compiled against the harness's Unity
shim (Tools/Build/skimrace_sim_harness/Sim.cs), racing the scene's own track. Nothing is copied: the evaluator builds
the harness from the job's ref (run.sh's build, in Python so it runs on Windows too) and parses `eval`'s race lines.

    loss = tuneall's own score on this track (Sim.cs TuneAll.Score):
             per race  time / ideal                     if it finished
                       cut / ideal + 2 x missed share    if not (a DNF always scores worse than any finish)
             mean over races + 0.5 x the worst race
         + stay_near_shipped x mean((field - shipped) / range)^2     never wander for nothing

Races: `seeds` fixed seeds from `seedbase`, 2 AI seats, 16/28/50 ms frames +-50%, contacts on the 0.04 s fixed step
(skimrace_retune.PHYSICS), raced to 3x ideal (at least the intensity's limit), cut 60 s later - tuneall's conditions.
The space is the policy's stated pursuit fields inside the harness's own Ranges (tuneall `only=stated`): a re-fit of
the numbers the policy uses, never a control switched on or off.

NOT modelled (metrics.limits): the editor (the sim is calibrated against it, Docs/SKIM_RACE_AI.md 6-8, and the editor is
the measurement); human opponents; more than 2 seats; frame hitches beyond the jitter. Fixed seeds can be over-fit:
promotion re-races the winner against the general policy on FRESH races (seedbase 99000), the retune's own gate.

args:
    intensity          the Skim Race intensity (1-4) whose scene track is raced
    seeds              races per evaluation (fixed: same params, same loss)
    seedbase           the first seed
    policy             repo path of the asset whose values are the baseline (the one the game flies there today)
    stay_near_shipped  weight of the pull toward the shipped values (0 turns it off)
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

VERSION = "1"
HARNESS = "Tools/Build/skimrace_sim_harness/"
SR = "Assets/_Scripts/Controller/AI/SkimRace/"
SOURCES = [HARNESS + "UnityShim.cs", HARNESS + "Sim.cs"] + [SR + n + ".cs" for n in (
    "SkimRaceAIConfigSO", "SkimRaceCourse", "SkimRaceObservation", "SkimRaceDriver", "SkimRaceShell", "SkimRacePlanner",
    "SkimRaceObstacle", "SkimRaceHandicap", "SkimRaceTrackFingerprint", "SkimRaceTargetTracker", "SkimRaceTeamAssignment",
    "SkimRaceReplanGate")] + ["Assets/_Scripts/Utility/Foundation/MathfNoAlloc.cs"]       # run.sh's files.rsp, in order
SCENE = "Assets/_Scenes/Multiplayer Scenes/MinigameSkimRace.unity"
FINGERPRINT = "Tools/Build/skimrace_track_fingerprint.py"
PATHS = [HARNESS, SR, "Assets/_Scripts/Utility/Foundation/MathfNoAlloc.cs", SCENE, FINGERPRINT,
         "Assets/Resources/SkimRaceAIConfig.asset", "Assets/Resources/SkimRaceAIConfig_I1.asset",
         "Assets/Resources/SkimRaceAIConfig_I2.asset", "Assets/Resources/SkimRaceAIConfig_I4.asset"]
PHYSICS = ["ph.Seats=2", "ph.Dt=0.028", "ph.DtJitter=0.5", "ph.PhysicsStep=0.04", "dts=0.016,0.028,0.05"]
EXTRA_TIME = 60.0       # Sim.cs Physics.ExtraTime: a race is cut at limit + this
LIMITS = ("offline sim of the shipped Skim Race pilot core on the scene's track, calibrated against the editor "
          "(Docs/SKIM_RACE_AI.md 6-8); 2 AI seats, no people; fixed seeds - promotion re-races fresh ones")
REQUEST_ARGS = {"intensity": {"choices": [1, 2, 3, 4]}, "seeds": {"lo": 2, "hi": 40}}
_CTX = {}
_RACE = re.compile(r"^\s*(FIN|DNF) t=([0-9.]+) (\d+)/(\d+) .*seats=\[([^\]]*)\]")


def inputs(args):
    return SOURCES + [SCENE, FINGERPRINT, args.get("policy", "Assets/Resources/SkimRaceAIConfig.asset")]


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


def space_for(root, policy_rel):
    """tuneall only=stated: the pursuit fields the policy's AUTHOR states, whose shipped value sits inside its range."""
    shipped = read_policy(root, policy_rel)
    rng, pursuit = ranges(root)
    ints = int_fields(root)
    stated = _stated(root, policy_rel)
    out = []
    for k in pursuit:
        if k in stated and k in shipped and k in rng and rng[k][0] <= shipped[k] <= rng[k][1]:
            d = {"name": k, "lo": rng[k][0], "hi": rng[k][1], "init": shipped[k]}
            if k in ints:
                d.update(type="int", lo=int(rng[k][0]), hi=int(rng[k][1]), init=int(shipped[k]))
            out.append(d)
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
    i = str(int(a.get("intensity", 3)))
    if i not in info["tracks"]:
        raise RuntimeError(f"the scene has no Skim Race intensity {i} (tracks: {sorted(info['tracks'])})")
    policy = a.get("policy", "Assets/Resources/SkimRaceAIConfig.asset")
    _CTX.update(folder=folder, dotnet=info["dotnet"], track=info["tracks"][i], fingerprint=info["fingerprints"].get(i),
                shipped=read_policy(root, policy), ints=int_fields(root), root=root)


def baseline(args):
    return None   # the worker scores the space's `init` values, which the job sets to the shipped asset's numbers


def race(params, ctx, seeds=None, seedbase=None):
    """Race the shipped policy with `params` over it; returns per-race rows and tuneall's score."""
    a = ctx["args"]
    if _CTX.get("root") != ctx["root"]:
        setup(ctx)
    policy = dict(_CTX["shipped"], **params)
    kv = [f"{k}={int(round(v)) if k in _CTX['ints'] else (round(v, 6) if isinstance(v, float) else v)}"
          for k, v in sorted(policy.items())]
    ideal, limit = _CTX["track"]["ideal"], _CTX["track"]["limit"]
    n = int(seeds or a.get("seeds", 8))
    out = _sim(_CTX["folder"], _CTX["dotnet"], ["eval", int(a.get("intensity", 3)), n] + kv + [
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
    rows, score = race(params, ctx)
    loss = score
    w = float(a.get("stay_near_shipped", 0.01))
    dims = {d["name"]: d for d in a.get("_space", [])}
    pen = [((v - _CTX["shipped"][k]) / (dims[k]["hi"] - dims[k]["lo"])) ** 2
           for k, v in params.items() if k in dims and k in _CTX["shipped"] and dims[k]["hi"] > dims[k]["lo"]]
    if w and pen:
        loss += w * sum(pen) / len(pen)
    fin = [r for r in rows if r["finished"]]
    med = lambda xs: sorted(xs)[len(xs) // 2] if xs else None
    return {"loss": loss, "valid": bool(fin), "metrics": {
        "score": round(score, 4), "finished": f"{len(fin)}/{len(rows)}",
        "winner_median_s": med([r["winner"] for r in fin]), "race_median_s": med([r["time"] for r in fin]),
        "ideal_s": _CTX["track"]["ideal"], "fingerprint": _CTX["fingerprint"], "limits": LIMITS}}


def self_test():
    """The shipped general policy finishes today's I1 (the sim reproduces its published result: 20/20 on fresh races,
    Docs/SKIM_RACE_AI.md 6.12), and a broken policy (no throttle, no lookahead) scores worse: the negative control."""
    here = os.path.dirname(os.path.abspath(__file__))
    root = os.path.dirname(os.path.dirname(os.path.dirname(here)))
    try:
        _dotnet()
    except RuntimeError as e:
        print(f"  skimrace_fit: skipped ({e})")
        return []
    ctx = {"root": root, "args": {"intensity": 1, "seeds": 3, "policy": "Assets/Resources/SkimRaceAIConfig.asset"}}
    _CTX.clear()
    bad = []
    try:
        good = evaluate({}, ctx)
        broken = evaluate({"MinThrottle": 0.0, "RecoveryThrottle": 0.0, "LookaheadMax": 30.0, "LookaheadMin": 5.0,
                           "StickGainPerDegree": 0.005}, ctx)
        again = evaluate({}, ctx)
        if good["metrics"]["finished"] != "3/3":
            bad.append(f"skimrace_fit: the shipped general policy did not finish I1: {good['metrics']}")
        if not broken["loss"] > good["loss"]:
            bad.append(f"skimrace_fit: a broken policy did not score worse ({broken['loss']:.3f} vs {good['loss']:.3f})")
        if again["loss"] != good["loss"]:
            bad.append("skimrace_fit: the same params gave a different loss (the sim is not deterministic)")
        sp = space_for(root, "Assets/Resources/SkimRaceAIConfig.asset")
        if len(sp) < 20 or any(not (d["lo"] <= d["init"] <= d["hi"]) for d in sp):
            bad.append(f"skimrace_fit: the general policy's space is wrong ({len(sp)} fields)")
    except Exception as e:
        bad.append(f"skimrace_fit: {type(e).__name__}: {e}")
    _CTX.clear()
    return bad
