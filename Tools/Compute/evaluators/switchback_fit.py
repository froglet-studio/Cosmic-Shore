"""The Dolphin's universal Time AI: ONE Switchback (Blast Run) charge drive scored on all four intensities at once, grown
from every Switchback drive genome the game ships (Docs/AI_SYSTEM/AI_PLAN.md step time-arcade; Yash, 2026-11-02: keep
learning from people, the learned files are the search's seeds, not its result).

Why: the game carries four Dolphin drives for Switchback - the mode-wide `DolphinAIBoostPolicy_Switchback` (flown at
I3, the intensity with no file of its own) and the I1, I2 and I4 files the AI learning sweep wrote from Yash's races
(Docs/AI_SYSTEM/AUTO_LEARNING.md section 7) - each fitted to one intensity's races. The plan wants one AI per vessel and
element that races every intensity well, so this searches one drive whose loss is the four intensities together, and
its first generation is the four genomes and mutations of each.

The model is the Blast Run simulator (Tools/Build/switchback_sim_harness/Sim.cs): the SHIPPED course, Blast Run layout,
blast planner and prism avoidance compiled out of Assets/ against the Skim Race harness's Unity shim, raced by a
kinematic Dolphin and the AI's executor ported from SwitchbackBlastAI / GateRaceController / AIPilot /
ChargeBoostAIPolicySO, with the game's drift, AI nose lag (the level's row of SwitchbackController.aiNoseFollowByIntensity),
the AI nose lead (AIPilot.NoseLeadGain, off unless the job's `nose_lead` switches it on) and measured prism costs. Nothing
is copied: the evaluator builds the harness from the job's ref (run.sh's build, in Python so it runs on Windows too) and
parses `races`' lines.

    per race       time / cruise          if it finished    (cruise = the gate-to-gate path at the Dolphin's cruise, 68 u/s)
                   cut / cruise + 2 x the gates it missed / gates      if not (a DNF always scores worse than any finish)
    per intensity  mean over races + 0.5 x the worst race
    loss           mean over the intensities + 0.5 x the worst intensity        (no intensity is given up for the others)
                 + stay_near_shipped x mean((field - genome's) / range)^2       never wander for nothing

A point is a GENOME (which shipped asset lies underneath: a `genome` choice in the space) plus the searched drive
fields over it, by the asset's own names. The space is the drive fields the simulator models; its bounds are the
asset's [Range] / [Min] limits, narrowed where a value past them only switches the drive off.

Races: `seeds` fixed course seeds from `seedbase` per intensity plus the game's tester seed (45_500 + intensity), at
the difficulty `levels` (default Hard, 3). Switchback deals a new course every match, so several fixed courses are
raced; fixed courses can be over-fit, so promotion re-races the winner against today's genomes on FRESH seeds.

NOT modelled (metrics.limits): the editor (real races ran ~1.6x the model's times in the 2026-11-02 soak, so judge the
ranking, not the seconds); rival Dolphins and their blasts; prisms bouncing (every hit destroys, as in the game); the
crystal pickup's exact shape (12 u); roll; frame hitches.

args:
    intensities        Switchback intensities raced together (default 1-4; `intensity` alone races one)
    levels             difficulty levels raced (default [3], Hard; each level flies its own nose lag row)
    seeds              extra fixed course seeds per intensity per evaluation (fixed: same params, same loss)
    seedbase           the first extra seed
    max_seconds        a race is cut here (a DNF)
    policy             repo path of the anchor genome (the mode-wide drive): the space's `init`, the board's baseline
    genomes            repo paths of every genome to grow from and choose between (default: the policy alone)
    stay_near_shipped  weight of the pull toward the chosen genome's values (0 turns it off)
    nose_lead          the AI nose lead (AIPilot.NoseLeadGain; Tools/Build/ai_nose_lead.py is the Python twin): a gain
                       stated at the fleet lag, or {"gain", "max_extra_deg"}; absent = off, today's steering (every
                       shipped asset flies 0, so the races are the ones before the knob existed)
"""
import glob
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys

import compute_lib as L

VERSION = "2"      # 2: the AI nose lead knob (Sim.cs noseLead / noseLeadExtra)
HARNESS = "Tools/Build/switchback_sim_harness/"
A = "Assets/_Scripts/Controller/Arcade/"
SOURCES = ["Tools/Build/skimrace_sim_harness/UnityShim.cs", HARNESS + "ShimExtra.cs", HARNESS + "Sim.cs",
           A + "Switchback/SwitchbackCourse.cs", A + "Switchback/SwitchbackBlastRun.cs", A + "Switchback/SwitchbackFeatures.cs",
           A + "Switchback/SwitchbackBlastPlanner.cs", A + "Switchback/SwitchbackPrismAvoidance.cs",
           A + "Racing/RaceCourseGeometry.cs", A + "Breakwater/BreakwaterStationBuilder.cs", A + "Breakwater/BreakwaterCourse.cs",
           "Assets/_Scripts/Data/Enums/PrismKind.cs"]                                     # run.sh's files.rsp, in order
POL = "Assets/Resources/AIBoostPolicies/"
GENERAL = POL + "DolphinAIBoostPolicy_Switchback.asset"
DOLPHIN_GENOMES = [GENERAL] + [POL + f"DolphinAIBoostPolicy_Switchback_I{i}.asset" for i in (1, 2, 4)]
# The drive fields the simulator models (Sim.cs `drive.<field>=`), with the game's limits. alignDegrees is [Range(0, 90)]
# and spendAtCharge01 [Range(0.05, 1)] in the code; the others are [Min] and bounded here where more only switches the
# drive off (a hold longer than any straight, a pulse no discharge can gain).
FIELDS = {"alignDegrees": (0.0, 90.0), "engageRunwaySeconds": (0.0, 4.0), "spendAtCharge01": (0.05, 1.0),
          "peakMarginUnits": (0.0, 30.0), "minDischargeSeconds": (0.0, 2.0), "minPulseGainUnits": (0.0, 120.0),
          "maxHoldOffSeconds": (0.5, 8.0)}
PATHS = [HARNESS, "Tools/Build/skimrace_sim_harness/UnityShim.cs", A + "Switchback/", A + "Racing/RaceCourseGeometry.cs",
         A + "Breakwater/", "Assets/_Scripts/Data/Enums/PrismKind.cs"] + DOLPHIN_GENOMES
LIMITS = ("offline sim of the shipped Switchback Blast Run course, layout, planner and avoidance with a kinematic Dolphin "
          "(SWITCHBACK.md 'Offline: the Blast Run simulator'); one AI, no rivals; real races ran ~1.6x its times; "
          "fixed seeds - promotion re-races fresh ones")
REQUEST_ARGS = {"intensity": {"choices": [1, 2, 3, 4]}, "seeds": {"lo": 1, "hi": 24}, "levels": {"subset": [1, 2, 3, 4]}}
COMBINE_WORST = 0.5     # loss = mean over intensities + this x the worst one (the plan's composite)
NOSE_LEAD_KEY = "nose_lead"         # ai_nose_lead.CONFIG_KEY: the job arg every offline race model reads
NOSE_LEAD_EXTRA_DEG = 40.0          # AIPilot.DefaultNoseLeadMaxExtraDegrees (ai_nose_lead.DEFAULT_MAX_EXTRA_DEG)
# evaluate({}, self_test's ctx): the mode-wide drive at I1 Hard on courses 45501 and 7000, measured 2026-11-07 (Linux x64,
# dotnet 8.0.133) before the nose lead knob existed. Without `nose_lead` the loss is this to 1e-6, or the model moved.
SELF_TEST_LOSS = 0.5557729803892291
FIXED_SEED = 45_500     # SwitchbackBlastRun.FixedSeed(intensity) = 45_500 + intensity, the tester's course
_CTX = {}
_RACE = re.compile(r"^RACE (\d) (\d) (-?\d+) (FIN|DNF) ([0-9.]+) (\d+)/(\d+) ([0-9.]+)")


def inputs(args):
    return SOURCES + sorted(set(genomes(args)))


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


def course_seeds(args, intensity):
    n, base = int(args.get("seeds", 3)), int(args.get("seedbase", 7000))
    return [FIXED_SEED + intensity] + [base + k for k in range(n)]


def nose_lead_argv(args):
    """The AI nose lead the job switches on, as the sim's argv (`noseLead=<gain> noseLeadExtra=<deg>`: Sim.cs
    Opts.NoseLeadGain / NoseLeadMaxExtraDegrees), nothing when it is off. `nose_lead` is the gain stated at the fleet lag
    (AIPilot.NoseLeadGain) or {"gain", "max_extra_deg"}, the forms ai_nose_lead.NoseLead.from_config reads for every
    offline model; absent, 0 or a bool = off, today's steering, so an evaluation without it is the one before the knob."""
    cfg = args.get(NOSE_LEAD_KEY)
    if cfg is None or isinstance(cfg, bool):
        return []
    if isinstance(cfg, dict):
        gain, extra = float(cfg.get("gain") or 0.0), cfg.get("max_extra_deg")
        extra = NOSE_LEAD_EXTRA_DEG if extra is None else float(extra)
    elif isinstance(cfg, (int, float)):
        gain, extra = float(cfg), NOSE_LEAD_EXTRA_DEG
    else:
        raise ValueError(f"{NOSE_LEAD_KEY}: a gain or {{'gain', 'max_extra_deg'}}, not {cfg!r}")
    return [f"noseLead={gain:g}", f"noseLeadExtra={extra:g}"] if gain > 0 else []


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
    """sim.dll under the cache, keyed by every source's bytes: a new revision builds anew."""
    h = hashlib.sha1(VERSION.encode())
    for rel in SOURCES:
        with open(os.path.join(root, rel), "rb") as fh:
            h.update(rel.encode() + b"\0" + fh.read())
    out = os.path.join(L.cache_dir(), "switchback_sim", h.hexdigest()[:16])
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
                        "-nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632,CS0108,CS1587,CS0219", "-target:exe",
                        "-main:Program", "-out:" + os.path.join(tmp, "sim.dll"), "@" + os.path.join(tmp, "build.rsp")],
                       capture_output=True, text=True, **L.NO_WINDOW)
    if p.returncode:
        raise RuntimeError("the Switchback sim did not compile: " + (p.stdout + p.stderr)[-1500:])
    with open(os.path.join(tmp, "sim.runtimeconfig.json"), "w", encoding="utf-8") as fh:
        json.dump({"runtimeOptions": {"tfm": "net%s.0" % ver.split(".")[0],
                                      "framework": {"name": "Microsoft.NETCore.App", "version": ver}}}, fh)
    with open(os.path.join(tmp, "info.json"), "w", encoding="utf-8") as fh:
        json.dump({"dotnet": dotnet}, fh)
    try:
        os.replace(tmp, out)                # another pool process may have won the race: theirs is identical
    except OSError:
        shutil.rmtree(tmp, ignore_errors=True)
    return out


def _sim(folder, dotnet, args):
    env = dict(os.environ, DOTNET_PROCESSOR_COUNT="1", DOTNET_CLI_TELEMETRY_OPTOUT="1")
    p = subprocess.run([dotnet, os.path.join(folder, "sim.dll")] + [str(a) for a in args],
                       capture_output=True, text=True, cwd=folder, env=env, **L.NO_WINDOW)
    if p.returncode:
        raise RuntimeError(f"sim {args[:3]} exited {p.returncode}: " + (p.stdout + p.stderr)[-800:])
    return p.stdout


# ── the genomes: the assets' values ───────────────────────────────────────────

def read_policy(root, rel):
    """The asset's searched drive fields (the ones Sim.cs models), as floats."""
    out = {}
    with open(os.path.join(root, rel), encoding="utf-8") as fh:
        body = fh.read().split("m_EditorClassIdentifier:", 1)[-1]
    for k, v in re.findall(r"^  (\w+): (-?[0-9.]+(?:e-?\d+)?)\s*$", body, re.M):
        if k in FIELDS:
            out[k] = float(v)
    return out


def space_for(root, policy_rel=GENERAL, others=()):
    """Every modelled drive field with the anchor's value as `init`; with more genomes, a `genome` choice first."""
    shipped = read_policy(root, policy_rel)
    out = [{"name": k, "lo": lo, "hi": hi, "init": min(hi, max(lo, shipped[k]))}
           for k, (lo, hi) in FIELDS.items() if k in shipped]
    names = [genome_name(g) for g in [policy_rel] + [g for g in others if g != policy_rel]]
    if len(names) > 1:
        out.insert(0, {"name": "genome", "type": "choice", "choices": names, "init": names[0]})
    return out


def seeds(args, root):
    """The first generation's genomes: each asset in `genomes` as it ships (clamped into the space). Read from the
    assets, never retyped."""
    gs = genomes(args)
    if len(gs) < 2:
        return []
    out = []
    for g in gs:
        p = {k: min(FIELDS[k][1], max(FIELDS[k][0], v)) for k, v in read_policy(root, g).items()}
        p["genome"] = genome_name(g)
        out.append(p)
    return out


# ── the evaluator contract ─────────────────────────────────────────────────────

def setup(ctx):
    a, root = ctx["args"], ctx["root"]
    folder = build(root)
    with open(os.path.join(folder, "info.json"), encoding="utf-8") as fh:
        info = json.load(fh)
    _CTX.update(folder=folder, dotnet=info["dotnet"], root=root,
                genomes={genome_name(g): read_policy(root, g) for g in genomes(a)},
                anchor=genome_name(genomes(a)[0]))


def baseline(args):
    return None   # the worker scores the space's `init` values, which the job sets to the shipped asset's numbers


def race(params, ctx, intensity, level):
    """Race the chosen genome with `params` over it on one intensity's courses at one level; returns per-race rows."""
    a = ctx["args"]
    if _CTX.get("root") != ctx["root"]:
        setup(ctx)
    params = dict(params)
    drive = dict(_CTX["genomes"][params.pop("genome", _CTX["anchor"])], **params)
    kv = [f"drive.{k}={round(float(v), 6)}" for k, v in sorted(drive.items()) if k in FIELDS]
    ss = course_seeds(a, intensity)
    out = _sim(_CTX["folder"], _CTX["dotnet"], ["races", f"i={intensity}", f"level={level}",
                                                f"seeds={','.join(str(s) for s in ss)}",
                                                f"maxSeconds={float(a.get('max_seconds', 420)):g}"] + kv + nose_lead_argv(a))
    rows = []
    for line in out.splitlines():
        m = _RACE.match(line.strip())
        if m:
            rows.append({"finished": m.group(4) == "FIN", "time": float(m.group(5)), "gates": int(m.group(6)),
                         "of": int(m.group(7)), "cruise": float(m.group(8)), "seed": int(m.group(3))})
    if len(rows) != len(ss):
        raise RuntimeError(f"expected {len(ss)} race lines from the sim, read {len(rows)}")
    return rows


def score(rows, cut):
    v = [r["time"] / r["cruise"] if r["finished"] else cut / r["cruise"] + 2.0 * (r["of"] - r["gates"]) / max(1, r["of"])
         for r in rows]
    return sum(v) / len(v) + 0.5 * max(v)


def evaluate(params, ctx):
    a = ctx["args"]
    if _CTX.get("root") != ctx["root"]:
        setup(ctx)
    cut = float(a.get("max_seconds", 420))
    med = lambda xs: sorted(xs)[len(xs) // 2] if xs else None
    scores, metrics, every_finishes = {}, {}, True
    for i in intensities(a):
        rows = [r for lvl in a.get("levels", [3]) for r in race(params, ctx, i, int(lvl))]
        fin = [r for r in rows if r["finished"]]
        every_finishes &= len(fin) == len(rows)
        scores[i] = score(rows, cut)
        metrics[f"I{i}"] = {"score": round(scores[i], 4), "finished": f"{len(fin)}/{len(rows)}",
                            "median_s": med([r["time"] for r in fin])}
    s = list(scores.values())
    combined = sum(s) / len(s) + (COMBINE_WORST * max(s) if len(s) > 1 else 0.0)
    loss = combined
    w = float(a.get("stay_near_shipped", 0.01))
    base = _CTX["genomes"][params.get("genome", _CTX["anchor"])]
    pen = [((v - base[k]) / (FIELDS[k][1] - FIELDS[k][0])) ** 2 for k, v in params.items() if k in FIELDS and k in base]
    if w and pen:
        loss += w * sum(pen) / len(pen)
    metrics.update(score=round(combined, 4), genome=params.get("genome", _CTX["anchor"]),
                   worst=f"I{max(scores, key=scores.get)}", limits=LIMITS)
    return {"loss": loss, "valid": every_finishes, "metrics": metrics}


def self_test():
    """The shipped mode-wide drive finishes the tester course at I1 (the simulator's own result) at the loss measured
    before the nose lead knob (SELF_TEST_LOSS: the knob is off by default), a drive that never discharges (spend at full
    charge, no align cone) scores worse (the negative control), the same params give the same loss, the genome choice
    really changes what races (the I4 file underneath is not the mode-wide one), and the seeds are the four shipped
    genomes themselves. The nose lead: its argv forms; at 2.5 on I1's slow-nose levels (Easy and Medium, the nose at
    1.9 / 2.2 per second, where the lag costs most: 0.34 / 0.27 of loss against no lag on these courses, 0.13 / 0.09 at
    Hard / Legendary) it finishes as often as no lead and the composite improves, gain 1 (judged from the command, no
    lead past the aim) is the weaker control, with no nose lag it changes nothing (the effective gain is 0), and the
    harness's own parity check against AIPilot / ai_nose_lead.py passes. Measured 2026-11-07: Easy + Medium 0.7292 ->
    0.7100 at 2.5 (0.7225 at gain 1); on these two courses Easy alone and Hard alone are worse at 2.5, so the gain for
    Switchback's own rate ladder stays a tuning question for the AI owner - this holds the mechanism, not a tuning."""
    here = os.path.dirname(os.path.abspath(__file__))
    root = os.path.dirname(os.path.dirname(os.path.dirname(here)))
    try:
        _dotnet()
    except RuntimeError as e:
        print(f"  switchback_fit: skipped ({e})")
        return []
    ctx = {"root": root, "args": {"intensities": [1], "seeds": 1, "policy": GENERAL, "genomes": DOLPHIN_GENOMES}}
    _CTX.clear()
    bad = []
    try:
        good = evaluate({}, ctx)
        broken = evaluate({"spendAtCharge01": 1.0, "alignDegrees": 0.0}, ctx)
        again = evaluate({}, ctx)
        other = evaluate({"genome": "DolphinAIBoostPolicy_Switchback_I4"}, ctx)
        if good["metrics"]["I1"]["finished"] != "2/2":
            bad.append(f"switchback_fit: the shipped drive did not finish I1: {good['metrics']}")
        if abs(good["loss"] - SELF_TEST_LOSS) > 1e-6:
            bad.append(f"switchback_fit: the shipped drive's I1 loss moved from {SELF_TEST_LOSS} to {good['loss']!r} "
                       "(the model changed: re-measure SELF_TEST_LOSS, or a knob is on by default)")
        if not broken["loss"] > good["loss"]:
            bad.append(f"switchback_fit: a drive that never discharges did not score worse ({broken['loss']:.3f} vs {good['loss']:.3f})")
        if again["loss"] != good["loss"]:
            bad.append("switchback_fit: the same params gave a different loss (the sim is not deterministic)")
        if other["loss"] == good["loss"]:
            bad.append("switchback_fit: the genome choice changed nothing (the I4 genome raced as the mode-wide one)")
        sp = space_for(root, GENERAL, DOLPHIN_GENOMES[1:])
        if sp[0].get("name") != "genome" or len(sp) != 1 + len(FIELDS) or any(
                d.get("type") != "choice" and not (d["lo"] <= d["init"] <= d["hi"]) for d in sp):
            bad.append(f"switchback_fit: the genomes' space is wrong ({len(sp)} fields)")
        sd = seeds(ctx["args"], root)
        if [x["genome"] for x in sd] != [genome_name(g) for g in DOLPHIN_GENOMES] or sd[1] == sd[0]:
            bad.append("switchback_fit: the seeds are not the four shipped genomes")
        # the AI nose lead: the argv forms (absent / 0 / a bool / gain 0 = off, nothing appended)
        for cfg, want in (({}, []), ({"nose_lead": 0}, []), ({"nose_lead": False}, []), ({"nose_lead": {"gain": 0}}, []),
                          ({"nose_lead": 2.5}, ["noseLead=2.5", "noseLeadExtra=40"]),
                          ({"nose_lead": {"gain": 3}}, ["noseLead=3", "noseLeadExtra=40"]),
                          ({"nose_lead": {"gain": 2, "max_extra_deg": 20}}, ["noseLead=2", "noseLeadExtra=20"])):
            if nose_lead_argv(cfg) != want:
                bad.append(f"switchback_fit: nose_lead_argv({cfg}) = {nose_lead_argv(cfg)}, wanted {want}")
        try:
            nose_lead_argv({"nose_lead": "2.5"})
            bad.append("switchback_fit: nose_lead_argv accepted a string")
        except ValueError:
            pass
        # ...at 2.5 on the slow-nose levels it finishes as often and the composite improves; gain 1 is the weaker control
        slow = dict(ctx["args"], levels=[1, 2])
        off = evaluate({}, {"root": root, "args": slow})
        lead = evaluate({}, {"root": root, "args": dict(slow, nose_lead=2.5)})
        weaker = evaluate({}, {"root": root, "args": dict(slow, nose_lead=1.0)})
        fin = lambda r: int(r["metrics"]["I1"]["finished"].split("/")[0])
        if fin(lead) < fin(off) or not lead["loss"] < off["loss"]:
            bad.append(f"switchback_fit: nose lead 2.5 did not help I1 Easy + Medium (loss {lead['loss']:.4f} vs {off['loss']:.4f}, "
                       f"finished {lead['metrics']['I1']['finished']} vs {off['metrics']['I1']['finished']})")
        if not weaker["loss"] > lead["loss"]:
            bad.append(f"switchback_fit: gain 1 (no lead past the aim) was not weaker than 2.5 ({weaker['loss']:.4f} vs {lead['loss']:.4f})")
        # ...with no nose lag there is nothing to lead: the harness's noseLag=0 races are the same, line for line
        argv = ["races", "i=1", "level=1", "seeds=45501,7000", "maxSeconds=420", "noseLag=0"]
        argv += [f"drive.{k}={round(float(v), 6)}" for k, v in sorted(_CTX["genomes"][_CTX["anchor"]].items()) if k in FIELDS]
        if _sim(_CTX["folder"], _CTX["dotnet"], argv) != _sim(_CTX["folder"], _CTX["dotnet"], argv + nose_lead_argv({"nose_lead": 2.5})):
            bad.append("switchback_fit: the nose lead changed a race flown with no nose lag (its effective gain must be 0)")
        # ...and the harness's parity check: NoseLeadDirection / EffectiveNoseLeadGain on ai_nose_lead.py's self-test values
        out = _sim(_CTX["folder"], _CTX["dotnet"], ["selftest"])
        if "switchback_sim self-test: ok" not in out:
            bad.append("switchback_fit: the harness self-test (nose-lead parity included) failed: " + out.strip()[-300:])
    except Exception as e:
        bad.append(f"switchback_fit: {type(e).__name__}: {e}")
    _CTX.clear()
    return bad
