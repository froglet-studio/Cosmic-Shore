#!/usr/bin/env python3
"""Train a vessel's Time AI in its OWN Time game, in the real game (Plan step: time-arcade).

Every candidate is raced by the shipped AIPilot in Amoebius, headless, through the race judge (regatta_judge.judge with
`mode`): the game's own course, rings, prisms, drift, boost and AI code. Nothing is modelled. A candidate is the AI's
numbers for that game, set IN MEMORY for its run (`overrides` on the drive assets, `controller_overrides` on the
mode's scene controller), so candidates race side by side and nothing is written until a winner is promoted.

    one AI per vessel per element (Docs/AI_SYSTEM/AI_PLAN.md): ONE drive, written to every intensity's file, scored
    on I1-I4 together. The first generation is every shipped genome (the mode file and its _I<n> files) as it ships.

Loss (lower is better), per race:  finish / reference(intensity)       when it finished
                                   2 + share of gates missed            when it did not (a DNF is worse than any finish)
    per intensity  mean over races + 0.5 x the worst race
    total          mean over intensities + 0.5 x the worst intensity  (no rung is given up for the others)
reference(intensity) is the shipped AI's mean on the same courses (the baseline row), so 1.0 = today's AI.

    python3 Tools/Compute/evaluators/time_ai_search.py --hull Dolphin --baseline
    python3 Tools/Compute/evaluators/time_ai_search.py --hull Dolphin --search --generations 6
    python3 Tools/Compute/evaluators/time_ai_search.py --hull Dolphin --confirm best.json --seeds 101,102,...

Courses: the judge forces course seed = race seed (CourseSeedOverride.Forced), so a seed list is a fixed set of
courses. Search on a few; CONFIRM a winner on fresh ones (24+ flights, Port/docs/AI_TRAINING.md) before promoting.
"""
import argparse
import concurrent.futures
import json
import math
import os
import re
import sys
import tempfile
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))
import regatta_judge as RJ  # noqa: E402
from optimizers import SepCMAES, Space  # noqa: E402

ROOT = RJ.ROOT
POL = "Assets/Resources/AIBoostPolicies/"

# Per vessel: its Time game, the drive assets per intensity (AIBoostPolicySO.ForMode: <name>_<Mode>_I<n>, else the mode
# file), the searched drive fields with the asset's limits, and the scene-controller fields searched with them.
GAMES = {
    "Dolphin": {
        "mode": "Switchback",
        "assets": {1: POL + "DolphinAIBoostPolicy_Switchback_I1.asset", 2: POL + "DolphinAIBoostPolicy_Switchback_I2.asset",
                   3: POL + "DolphinAIBoostPolicy_Switchback.asset", 4: POL + "DolphinAIBoostPolicy_Switchback_I4.asset"},
        # ChargeBoostAIPolicySO, bounds as Tools/Compute/evaluators/switchback_fit.py FIELDS (+ the stick band)
        "fields": {"alignDegrees": (0.0, 90.0), "stickBand": (0.0, 1.0), "engageRunwaySeconds": (0.0, 4.0),
                   "spendAtCharge01": (0.05, 1.0), "peakMarginUnits": (0.0, 30.0), "minDischargeSeconds": (0.0, 2.0),
                   "minPulseGainUnits": (0.0, 120.0), "maxHoldOffSeconds": (0.5, 8.0)},
        # SwitchbackController.aiNoseLead row 0 is the Dolphin's (code default 2.5; the scene does not override it)
        "controller": {"aiNoseLead.0.Gain": (0.0, 3.0, 2.5)},
    },
    "Serpent": {
        "mode": "SerpentTime",
        "assets": {i: POL + "SerpentAIBoostPolicy_SerpentTime.asset" for i in (1, 2, 3, 4)},
        # PelletBoostAIPolicySO over AIBoostPolicySO's bands
        "fields": {"alignDegrees": (0.0, 90.0), "releaseAlignDegrees": (0.0, 120.0), "stickBand": (0.0, 1.0),
                   "releaseStickBand": (0.0, 1.0), "engageRunwaySeconds": (0.0, 6.0), "releaseRunwaySeconds": (0.0, 3.0),
                   "maxConcurrentBurns": (1, 6), "minSecondsBetweenPellets": (0.0, 4.0), "reservePellets": (0, 4)},
        "ints": ("maxConcurrentBurns", "reservePellets"),
        # SerpentTimeController.aiNoseLead row 0 is the Serpent's (code default 1.0)
        "controller": {"aiNoseLead.0.Gain": (0.0, 3.0, 1.0)},
    },
    "Sparrow": {
        "mode": "Breakwater",
        "assets": {i: POL + "SparrowAIBoostPolicy_Breakwater.asset" for i in (1, 2, 3, 4)},
        # HoldBoostAIPolicySO over AIBoostPolicySO's bands (the rocket's numbers are left as they ship)
        "fields": {"alignDegrees": (0.0, 90.0), "releaseAlignDegrees": (0.0, 120.0), "stickBand": (0.0, 1.0),
                   "releaseStickBand": (0.0, 1.0), "engageRunwaySeconds": (0.0, 4.0), "releaseRunwaySeconds": (0.0, 3.0),
                   "minHoldSeconds": (0.0, 3.0), "minRestSeconds": (0.0, 3.0)},
        # BreakwaterController.aiNoseLead row 0 is the Sparrow's (code default 2.0)
        "controller": {"aiNoseLead.0.Gain": (0.0, 3.0, 2.0)},
    },
}


def read_fields(rel, names):
    body = open(os.path.join(ROOT, rel), encoding="utf-8").read().split("m_EditorClassIdentifier:", 1)[-1]
    return {k: float(v) for k, v in re.findall(r"^  (\w+): (-?[0-9.]+(?:e-?\d+)?)\s*$", body, re.M) if k in names}


def space_for(g):
    ints = g.get("ints", ())
    dims = [{"name": k, "lo": lo, "hi": hi, **({"type": "int"} if k in ints else {})} for k, (lo, hi) in g["fields"].items()]
    dims += [{"name": "ctl:" + k, "lo": lo, "hi": hi, "init": init} for k, (lo, hi, init) in g["controller"].items()]
    return Space(dims)


def shipped_genomes(g):
    """Every drive the game ships for this vessel and game, with today's controller values: the first generation."""
    out = []
    for rel in sorted(set(g["assets"].values())):
        p = read_fields(rel, g["fields"])
        p.update({k: int(v) for k, v in p.items() if k in g.get("ints", ())})
        p.update({"ctl:" + k: init for k, (_, _, init) in g["controller"].items()})
        p["_from"] = os.path.basename(rel)
        out.append(p)
    return out


def overrides_for(g, params):
    """None = the shipped AI (each intensity its own file). Otherwise ONE drive on every intensity's file."""
    if params is None:
        return None, None
    fields = {k: params[k] for k in g["fields"] if k in params}
    ov = [{"asset": rel, "fields": fields} for rel in sorted(set(g["assets"].values()))]
    ctl = {k[4:]: v for k, v in params.items() if k.startswith("ctl:")}
    return ov, ctl or None


def race_one(hull, g, params, intensity, seeds, limit_s, tries=3):
    """One process: this candidate at this intensity over the seeds. A player that died (killed for memory, a crash)
    is not the AI's fault and is raced again rather than scored as a DNF."""
    ov, ctl = overrides_for(g, params)
    for attempt in range(tries):
        wd = tempfile.mkdtemp(prefix=f"tas-{hull}-{intensity}-")
        r = RJ.judge(None, [hull], seeds, limit_s=limit_s, mode=g["mode"], intensity=intensity, overrides=ov,
                     controller_overrides=ctl, workers=1, workdir=wd)
        died = any("player exited" in n for n in r["notes"]) or len(r["races"]) < len(seeds)
        if not died:
            break
        print(f"retry {hull} I{intensity} (attempt {attempt + 1}): {[n for n in r['notes'] if 'exited' in n]}", flush=True)
    rows = []
    for race in r["races"]:
        s = race["seats"][0] if race.get("seats") else {}
        rows.append({"seed": race.get("seed"), "course_seed": race.get("course_seed"), "finish_s": s.get("finish_s"),
                     "gates": s.get("gates", 0), "gates_total": race.get("gates_total") or 1, "outcome": race.get("outcome"),
                     "prism_hits": s.get("prism_hits")})
    if len(rows) < len(seeds):
        rows += [{"seed": sd, "finish_s": None, "gates": 0, "gates_total": 1, "outcome": "no-result"} for sd in seeds[len(rows):]]
    return {"intensity": intensity, "races": rows, "notes": r["notes"]}


def loss(per_intensity, ref):
    def race_loss(x, i):
        if x["finish_s"] is not None:
            return x["finish_s"] / ref[i]
        return 2.0 + (1.0 - x["gates"] / max(1, x["gates_total"]))
    lis = []
    for res in per_intensity:
        i = res["intensity"]
        ls = [race_loss(x, i) for x in res["races"]]
        lis.append(sum(ls) / len(ls) + 0.5 * max(ls))
    return sum(lis) / len(lis) + 0.5 * max(lis), lis


def evaluate_many(hull, g, cands, intensities, seeds, limit_s, pool):
    """cands: [params|None]; every (candidate, intensity) is one process, all in one pool."""
    jobs = [(c, i) for c in range(len(cands)) for i in intensities]
    futs = {pool.submit(race_one, hull, g, cands[c], i, seeds, limit_s): (c, i) for c, i in jobs}
    out = [[None] * len(intensities) for _ in cands]
    for f in concurrent.futures.as_completed(futs):
        c, i = futs[f]
        out[c][intensities.index(i)] = f.result()
    return out


def summarise(per_intensity):
    return {r["intensity"]: [x["finish_s"] for x in r["races"]] for r in per_intensity}


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--hull", required=True, choices=sorted(GAMES))
    ap.add_argument("--baseline", action="store_true", help="race the shipped AI and write the reference")
    ap.add_argument("--search", action="store_true")
    ap.add_argument("--confirm", help="JSON file of params (or a list of them) to race against the shipped AI")
    ap.add_argument("--generations", type=int, default=6)
    ap.add_argument("--popsize", type=int, default=8)
    ap.add_argument("--seeds", default="1,2")
    ap.add_argument("--intensities", default="1,2,3,4")
    ap.add_argument("--limit", type=float, default=400)
    ap.add_argument("--workers", type=int, default=os.cpu_count() or 4)
    ap.add_argument("--out", default=None, help="folder for the log and results (default a temp folder)")
    a = ap.parse_args()
    g = GAMES[a.hull]
    seeds = [int(s) for s in a.seeds.split(",") if s]
    its = [int(s) for s in a.intensities.split(",") if s]
    out = a.out or tempfile.mkdtemp(prefix=f"time-ai-{a.hull}-")
    os.makedirs(out, exist_ok=True)
    log = open(os.path.join(out, "log.jsonl"), "a")
    RJ.build()
    pool = concurrent.futures.ThreadPoolExecutor(a.workers)
    t0 = time.time()

    def emit(rec):
        rec["t"] = round(time.time() - t0, 1)
        log.write(json.dumps(rec) + "\n")
        log.flush()
        print(json.dumps({k: v for k, v in rec.items() if k != "per_intensity"}), flush=True)

    ref_path = os.path.join(out, f"reference_{'_'.join(map(str, seeds))}.json")
    if a.baseline or not os.path.exists(ref_path):
        base = evaluate_many(a.hull, g, [None], its, seeds, a.limit, pool)[0]
        ref = {}
        for r in base:
            done = [x["finish_s"] for x in r["races"] if x["finish_s"] is not None]
            ref[r["intensity"]] = sum(done) / len(done) if done else a.limit
        json.dump({"ref": ref, "per_intensity": base}, open(ref_path, "w"), indent=1)
        emit({"kind": "baseline", "seeds": seeds, "ref": ref, "times": summarise(base)})
    ref = {int(k): v for k, v in json.load(open(ref_path))["ref"].items()}

    if a.confirm:
        cands = json.load(open(a.confirm))
        cands = cands if isinstance(cands, list) else [cands]
        res = evaluate_many(a.hull, g, [None] + cands, its, seeds, a.limit, pool)
        for k, r in enumerate(res):
            l, lis = loss(r, ref)
            emit({"kind": "confirm", "candidate": "shipped" if k == 0 else k, "loss": round(l, 4),
                  "per_rung": [round(x, 3) for x in lis], "times": summarise(r), "per_intensity": r,
                  "params": None if k == 0 else cands[k - 1]})
        return

    if a.search:
        space = space_for(g)
        es = SepCMAES(space, seed=1, popsize=a.popsize, sigma0=0.2)
        es.seed([space.encode(p) for p in shipped_genomes(g)])
        best = None
        for gen in range(a.generations):
            us = es.ask()
            cands = [space.decode(u) for u in us]
            res = evaluate_many(a.hull, g, cands, its, seeds, a.limit, pool)
            losses = []
            for p, r in zip(cands, res):
                l, lis = loss(r, ref)
                losses.append(l)
                emit({"kind": "eval", "gen": gen, "loss": round(l, 4), "per_rung": [round(x, 3) for x in lis],
                      "times": summarise(r), "params": p, "per_intensity": r})
                if best is None or l < best[0]:
                    best = (l, p)
            es.tell(us, losses)
            emit({"kind": "gen", "gen": gen, "best_loss": round(best[0], 4), "best": best[1],
                  "gen_losses": sorted(round(x, 3) for x in losses)})
        json.dump(best[1], open(os.path.join(out, "best.json"), "w"), indent=1)
        print("best", best, "->", os.path.join(out, "best.json"))


if __name__ == "__main__":
    main()
