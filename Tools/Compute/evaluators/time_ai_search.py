#!/usr/bin/env python3
"""Train a vessel's Time AI in its OWN Time game, in the real game (Plan step: time-arcade).

Every candidate is raced by the shipped AIPilot in Amoebius, headless, through the race judge (regatta_judge.judge with
`mode`): the game's own course, rings, prisms, drift, boost and AI code. Nothing is modelled. A candidate is the AI's
numbers for that game, set IN MEMORY for its run (`overrides` on the drive assets, `controller_overrides` on the
mode's scene controller, `crystal_genes` for RaceAICrystalGenesSO), so candidates race side by side and nothing is
written until a winner is promoted.

    one AI per vessel per element (Docs/AI_SYSTEM/AI_PLAN.md): ONE drive, written to every intensity's file, scored on
    every CONDITION together. A condition is an intensity and a starting element table: the card's own (I1 at rest,
    I2-I4 at the game's element 5 since #2055) and the same rungs at Time 10, because a hull's Time sets its speed and
    an AI trained at one speed breaks at another (FINDINGS.md 13: the Scarab, 216 u/s in Scarab Rally, loops at 324 u/s
    in Regatta). W2's TrainingCourseOverlay genomes widen the courses (`--genome`, once it ships).

Two phases (Garrett, 2026-11-16: "constrain the space to find a good seed quickly then switch to a pareto scheme to
expand and contract complexity"):
    1 constrained  sep-CMA-ES over the drive fields and the nose lead, from every shipped genome; crystal gene at 0.
    2 pareto       a non-dominated archive over (loss, complexity). Complexity = genes moved off the phase-1 seed (a
                   crystal budget above 0 counts). A child EXPANDS (frees one held gene, the crystal gene included),
                   CONTRACTS (puts one moved gene back on the seed) or MUTATES its moved genes.

Loss (lower is better), per race:  finish / reference(condition)       when it finished
                                   2 + share of gates missed            when it did not (a DNF is worse than any finish)
    per condition  mean over races + 0.5 x the worst race
    total          mean over conditions + 0.5 x the worst condition  (no rung is given up for the others)
reference(condition) is the shipped AI's mean on the same courses, so 1.0 = today's AI.

    python3 Tools/Compute/evaluators/time_ai_search.py --hull Scarab --search --generations 4 --pareto 4
    python3 Tools/Compute/evaluators/time_ai_search.py --hull Scarab --confirm best.json --seeds 101,102,103

A player killed for memory is raced again, never scored as a DNF. One player process takes ~2.5 GB: --workers 4 at
most on a 16 GB machine. Confirm a winner on fresh courses (24+ flights, Port/docs/AI_TRAINING.md) before promoting.
"""
import argparse
import concurrent.futures
import json
import os
import random
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
SO = "Assets/_SO_Assets/AI Boost Policies/"
CRYSTAL = "crystal"            # the RaceAICrystalGenesSO gene: detour budget, world units
CRYSTAL_RANGE = (0.0, 300.0)

# Per vessel: its Time game, the drive assets per intensity (AIBoostPolicySO.ForMode: <name>_<Mode>_I<n>, else the mode
# file, else the hull's own), the searched drive fields with the asset's limits, and the nose lead: the mode's
# aiNoseLead table with this hull's row ("rows" are the other hulls' rows, kept as they ship).
GAMES = {
    "Dolphin": {
        "mode": "Switchback",
        "assets": {1: POL + "DolphinAIBoostPolicy_Switchback_I1.asset", 2: POL + "DolphinAIBoostPolicy_Switchback_I2.asset",
                   3: POL + "DolphinAIBoostPolicy_Switchback.asset", 4: POL + "DolphinAIBoostPolicy_Switchback_I4.asset"},
        # ChargeBoostAIPolicySO, bounds as Tools/Compute/evaluators/switchback_fit.py FIELDS (+ the stick band)
        "fields": {"alignDegrees": (0.0, 90.0), "stickBand": (0.0, 1.0), "engageRunwaySeconds": (0.0, 4.0),
                   "spendAtCharge01": (0.05, 1.0), "peakMarginUnits": (0.0, 30.0), "minDischargeSeconds": (0.0, 2.0),
                   "minPulseGainUnits": (0.0, 120.0), "maxHoldOffSeconds": (0.5, 8.0)},
        "lead": 2.5, "rows": [],                       # SwitchbackController.aiNoseLead: Dolphin 2.5
    },
    "Serpent": {
        "mode": "SerpentTime",
        "assets": {i: POL + "SerpentAIBoostPolicy_SerpentTime.asset" for i in (1, 2, 3, 4)},
        "fields": {"alignDegrees": (0.0, 90.0), "releaseAlignDegrees": (0.0, 120.0), "stickBand": (0.0, 1.0),
                   "releaseStickBand": (0.0, 1.0), "engageRunwaySeconds": (0.0, 6.0), "releaseRunwaySeconds": (0.0, 3.0),
                   "maxConcurrentBurns": (1, 6), "minSecondsBetweenPellets": (0.0, 4.0), "reservePellets": (0, 4)},
        "ints": ("maxConcurrentBurns", "reservePellets"),
        "lead": 1.0, "rows": [],                       # SerpentTimeController.aiNoseLead: Serpent 1.0
    },
    "Sparrow": {
        "mode": "Breakwater",
        "assets": {i: POL + "SparrowAIBoostPolicy_Breakwater.asset" for i in (1, 2, 3, 4)},
        "fields": {"alignDegrees": (0.0, 90.0), "releaseAlignDegrees": (0.0, 120.0), "stickBand": (0.0, 1.0),
                   "releaseStickBand": (0.0, 1.0), "engageRunwaySeconds": (0.0, 4.0), "releaseRunwaySeconds": (0.0, 3.0),
                   "minHoldSeconds": (0.0, 3.0), "minRestSeconds": (0.0, 3.0)},
        "lead": 1.0, "rows": [],                       # BreakwaterController.aiNoseLead: Sparrow 1.0 (#2054)
    },
    "Scarab": {
        "mode": "ScarabRally",
        # The hull's own drive (no Scarab Rally variant): what it flies in Regatta too, so a win here carries over.
        "assets": {i: SO + "ScarabAIBoostPolicy.asset" for i in (1, 2, 3, 4)},
        "fields": {"alignDegrees": (0.0, 90.0), "releaseAlignDegrees": (0.0, 120.0), "stickBand": (0.0, 1.0),
                   "releaseStickBand": (0.0, 1.0), "engageRunwaySeconds": (0.0, 4.0), "releaseRunwaySeconds": (0.0, 3.0),
                   "dashCooldownSeconds": (0.2, 6.0)},
        # Scarab Rally inherits Switchback's table, which has only the Dolphin's row: the Scarab flies with NO lead
        # there and 2.5 in Regatta (RaceLines/Scarab_Regatta). The search gives it a row.
        "lead": 0.0, "rows": [{"Hull": "Dolphin", "Gain": 2.5, "MaxExtraDegrees": 40}],
    },
}
LEAD_RANGE = (0.0, 3.0)
LEAD = "noseLead"


def wide_genome(hull, w, seed):
    """A curriculum course for this hull's game (W2's TrainingCourseOverlay): the game's own gates with genome placements
    laid round them; w=0 is the game's own course, w=1 the full genome."""
    import random
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import regatta_genome as RG
    return RG.training_genome(random.Random(seed), w, for_mode=GAMES[hull]["mode"])


def conditions_for(a):
    """[(name, intensity, start, genome)]: the card's start at each rung; Time 10 at the --fast rungs; Time 0 (rest) at
    the --rest rungs; and --wide courses (W2's curriculum, I2) so the drive does not overfit the game's own track."""
    out = [(f"I{i}", i, None, None) for i in a.intensities]
    out += [(f"I{i}@T10", i, {a.hull: {"Time": 10}}, None) for i in a.fast]
    out += [(f"I{i}@T0", i, "rest", None) for i in a.rest]
    out += [(f"W{k}@I2", 2, None, wide_genome(a.hull, a.wide_w, 900 + k)) for k in range(a.wide)]
    return out


def read_fields(rel, names):
    body = open(os.path.join(ROOT, rel), encoding="utf-8").read().split("m_EditorClassIdentifier:", 1)[-1]
    return {k: float(v) for k, v in re.findall(r"^  (\w+): (-?[0-9.]+(?:e-?\d+)?)\s*$", body, re.M) if k in names}


def dims_for(g):
    ints = g.get("ints", ())
    dims = [{"name": k, "lo": lo, "hi": hi, **({"type": "int"} if k in ints else {})} for k, (lo, hi) in g["fields"].items()]
    dims.append({"name": LEAD, "lo": LEAD_RANGE[0], "hi": LEAD_RANGE[1]})
    return dims


def shipped_genomes(g):
    """Every drive the game ships for this vessel and game, with today's nose lead and no crystal detour."""
    out = []
    for rel in sorted(set(g["assets"].values())):
        p = read_fields(rel, g["fields"])
        p.update({k: int(v) for k, v in p.items() if k in g.get("ints", ())})
        p[LEAD] = g["lead"]
        p[CRYSTAL] = 0.0
        p["_from"] = os.path.basename(rel)
        out.append(p)
    return out


def spec_for(hull, g, params):
    """None = the shipped AI. Otherwise ONE drive on every intensity's file, this hull's nose lead row and its crystal
    gene, all in memory."""
    if params is None:
        return {}
    fields = {k: params[k] for k in g["fields"] if k in params}
    ov = [{"asset": rel, "fields": fields} for rel in sorted(set(g["assets"].values()))]
    rows = list(g["rows"]) + [{"Hull": hull, "Gain": float(params.get(LEAD, g["lead"])), "MaxExtraDegrees": 40}]
    out = {"overrides": ov, "controller_overrides": {"aiNoseLead": rows}}
    if params.get(CRYSTAL, 0) > 0:
        out["crystal_genes"] = {hull: float(params[CRYSTAL])}
    return out


def race_one(hull, g, params, cond, seeds, limit_s, tries=3):
    """One process: this candidate in one condition over the seeds. A player that died (killed for memory, a crash)
    is not the AI's fault and is raced again rather than scored as a DNF."""
    name, intensity, start, genome = cond
    extra = spec_for(hull, g, params)
    for attempt in range(tries):
        wd = tempfile.mkdtemp(prefix=f"tas-{hull}-{name.replace('@', '-')}-")
        r = RJ.judge(genome, [hull], seeds, limit_s=limit_s, mode=g["mode"], intensity=intensity, workers=1, workdir=wd,
                     start=start, **extra)
        died = any("player exited" in n for n in r["notes"]) or len(r["races"]) < len(seeds)
        if not died:
            break
        print(f"retry {hull} {name} (attempt {attempt + 1}): {[n for n in r['notes'] if 'exited' in n]}", flush=True)
    rows = []
    for race in r["races"]:
        s = race["seats"][0] if race.get("seats") else {}
        rows.append({"seed": race.get("seed"), "course_seed": race.get("course_seed"), "finish_s": s.get("finish_s"),
                     "gates": s.get("gates", 0), "gates_total": race.get("gates_total") or 1, "outcome": race.get("outcome"),
                     "prism_hits": s.get("prism_hits"), "start_levels": s.get("start_levels")})
    if len(rows) < len(seeds):
        rows += [{"seed": sd, "finish_s": None, "gates": 0, "gates_total": 1, "outcome": "no-result"} for sd in seeds[len(rows):]]
    return {"condition": name, "races": rows, "notes": r["notes"]}


def loss(per_cond, ref):
    def race_loss(x, c):
        if x["finish_s"] is not None:
            return x["finish_s"] / ref[c]
        return 2.0 + (1.0 - x["gates"] / max(1, x["gates_total"]))
    lis = []
    for res in per_cond:
        ls = [race_loss(x, res["condition"]) for x in res["races"]]
        lis.append(sum(ls) / len(ls) + 0.5 * max(ls))
    return sum(lis) / len(lis) + 0.5 * max(lis), lis


def evaluate_many(hull, g, cands, conds, seeds, limit_s, pool):
    """cands: [params|None]; every (candidate, condition) is one process, all in one pool."""
    jobs = [(c, k) for c in range(len(cands)) for k in range(len(conds))]
    futs = {pool.submit(race_one, hull, g, cands[c], conds[k], seeds, limit_s): (c, k) for c, k in jobs}
    out = [[None] * len(conds) for _ in cands]
    for f in concurrent.futures.as_completed(futs):
        c, k = futs[f]
        out[c][k] = f.result()
    return out


def summarise(per_cond):
    return {r["condition"]: [x["finish_s"] for x in r["races"]] for r in per_cond}


def complexity(p, seed, dims):
    """Genes moved off the seed by more than 5% of their range, plus the crystal gene when it is on."""
    moved = sum(1 for d in dims if abs(p[d["name"]] - seed[d["name"]]) > 0.05 * (d["hi"] - d["lo"]))
    return moved + (1 if p.get(CRYSTAL, 0) > 0 else 0)


def pareto_front(archive):
    """Non-dominated (loss, complexity) entries."""
    front = []
    for e in archive:
        if not any((o["loss"] <= e["loss"] and o["cx"] <= e["cx"]) and (o["loss"] < e["loss"] or o["cx"] < e["cx"])
                   for o in archive):
            front.append(e)
    return sorted(front, key=lambda e: e["cx"])


def child(parent, seed, dims, rng):
    """EXPAND a held gene, CONTRACT a moved one, or MUTATE the moved ones."""
    p = {k: v for k, v in parent.items() if not k.startswith("_")}
    names = [d["name"] for d in dims] + [CRYSTAL]
    rng_of = {d["name"]: (d["lo"], d["hi"]) for d in dims}
    rng_of[CRYSTAL] = CRYSTAL_RANGE
    moved = [n for n in names if abs(p[n] - seed[n]) > 0.05 * (rng_of[n][1] - rng_of[n][0]) or (n == CRYSTAL and p[n] > 0)]
    held = [n for n in names if n not in moved]
    op = rng.choice(["expand"] * 2 + ["contract"] * (1 if moved else 0) + ["mutate"] * (2 if moved else 0))
    if op == "expand" and held:
        n = rng.choice(held)
        lo, hi = rng_of[n]
        p[n] = min(hi, max(lo, p[n] + rng.gauss(0, 0.25) * (hi - lo))) if n != CRYSTAL else rng.uniform(40, hi)
    elif op == "contract" and moved:
        n = rng.choice(moved)
        p[n] = seed[n]
    else:
        for n in moved:
            lo, hi = rng_of[n]
            p[n] = min(hi, max(lo, p[n] + rng.gauss(0, 0.08) * (hi - lo)))
    for d in dims:
        if d.get("type") == "int":
            p[d["name"]] = int(round(p[d["name"]]))
    p["_op"] = op
    return p


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--hull", required=True, choices=sorted(GAMES))
    ap.add_argument("--search", action="store_true")
    ap.add_argument("--confirm", help="JSON file of params (or a list of them) to race against the shipped AI")
    ap.add_argument("--generations", type=int, default=4, help="phase 1 (constrained) generations")
    ap.add_argument("--pareto", type=int, default=4, help="phase 2 (Pareto) generations")
    ap.add_argument("--popsize", type=int, default=6)
    ap.add_argument("--seeds", default="11,12")
    ap.add_argument("--intensities", default="1,2,3,4")
    ap.add_argument("--fast", default="1,4", help="rungs also raced at Time 10 (empty: none)")
    ap.add_argument("--rest", default="", help="rungs also raced from Time 0 (empty: none)")
    ap.add_argument("--wide", type=int, default=0, help="how many W2 curriculum courses (raced at I2)")
    ap.add_argument("--wide-w", type=float, default=0.5, help="curriculum weight of the wide courses (0-1)")
    ap.add_argument("--limit", type=float, default=400)
    ap.add_argument("--workers", type=int, default=4)
    ap.add_argument("--out", default=None)
    a = ap.parse_args()
    a.intensities = [int(s) for s in a.intensities.split(",") if s]
    a.fast = [int(s) for s in a.fast.split(",") if s]
    a.rest = [int(s) for s in a.rest.split(",") if s]
    g = GAMES[a.hull]
    seeds = [int(s) for s in a.seeds.split(",") if s]
    conds = conditions_for(a)
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
        print(json.dumps({k: v for k, v in rec.items() if k != "per_cond"}), flush=True)

    ref_path = os.path.join(out, f"reference_{'_'.join(map(str, seeds))}_{'_'.join(c[0] for c in conds)}.json")
    if not os.path.exists(ref_path):
        base = evaluate_many(a.hull, g, [None], conds, seeds, a.limit, pool)[0]
        ref = {}
        for r in base:
            done = [x["finish_s"] for x in r["races"] if x["finish_s"] is not None]
            ref[r["condition"]] = sum(done) / len(done) if done else a.limit
        json.dump({"ref": ref, "per_cond": base}, open(ref_path, "w"), indent=1)
        emit({"kind": "baseline", "seeds": seeds, "ref": ref, "times": summarise(base)})
    ref = json.load(open(ref_path))["ref"]

    def score(cands, kind, gen):
        res = evaluate_many(a.hull, g, cands, conds, seeds, a.limit, pool)
        recs = []
        for p, r in zip(cands, res):
            l, lis = loss(r, ref)
            rec = {"kind": kind, "gen": gen, "loss": round(l, 4), "per_cond": r,
                   "per_condition": dict(zip([c[0] for c in conds], [round(x, 3) for x in lis])),
                   "times": summarise(r), "params": p}
            recs.append(rec)
            emit(rec)
        return recs

    if a.confirm:
        cands = json.load(open(a.confirm))
        cands = cands if isinstance(cands, list) else [cands]
        score([None] + cands, "confirm", 0)
        return

    if not a.search:
        return
    dims = dims_for(g)
    space = Space(dims)
    es = SepCMAES(space, seed=1, popsize=a.popsize, sigma0=0.2)
    shipped = shipped_genomes(g)
    es.seed([space.encode(p) for p in shipped])
    archive = []
    for gen in range(a.generations):
        us = es.ask()
        cands = [dict(space.decode(u), **{CRYSTAL: 0.0}) for u in us]
        recs = score(cands, "phase1", gen)
        es.tell(us, [r["loss"] for r in recs])
        archive += recs
        best = min(archive, key=lambda r: r["loss"])
        emit({"kind": "gen", "phase": 1, "gen": gen, "best_loss": best["loss"], "best": best["params"]})

    # Phase 2: the best constrained genome is the seed; complexity is measured from it.
    seed = {k: v for k, v in min(archive, key=lambda r: r["loss"])["params"].items() if not k.startswith("_")}
    for r in archive:
        r["cx"] = complexity(r["params"], seed, dims)
    rng = random.Random(7)
    for gen in range(a.pareto):
        front = pareto_front(archive)
        cands = [child(rng.choice(front)["params"], seed, dims, rng) for _ in range(a.popsize)]
        recs = score(cands, "pareto", gen)
        for r in recs:
            r["cx"] = complexity(r["params"], seed, dims)
        archive += recs
        front = pareto_front(archive)
        emit({"kind": "front", "phase": 2, "gen": gen,
              "front": [{"loss": e["loss"], "cx": e["cx"], "params": e["params"]} for e in front]})
    front = pareto_front(archive)
    json.dump([{"loss": e["loss"], "cx": e["cx"], "params": e["params"]} for e in front],
              open(os.path.join(out, "front.json"), "w"), indent=1)
    best = min(archive, key=lambda r: r["loss"])
    json.dump(best["params"], open(os.path.join(out, "best.json"), "w"), indent=1)
    print("best", best["loss"], best["params"], "->", out)


if __name__ == "__main__":
    main()
