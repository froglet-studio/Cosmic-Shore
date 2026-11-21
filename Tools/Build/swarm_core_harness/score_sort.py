#!/usr/bin/env python3
"""Score the SHIPPED sort core (SwarmSortCore.cs) with the research's UNCHANGED scorer, beside the
other two in-game cores.

    python3 Tools/Build/swarm_core_harness/score_sort.py [--seeds 7,23,41,108,209] [--no-ref] [--out x.json]

What it does:
  1. builds and runs the C# harness in export mode (run.sh export): every plan grown 240 steps from
     the research's seed (16 tadpoles, randn x 2) in RESEARCH mode (SwarmSortCore exactly as
     sort_model runs it: domain regions, free laying, instant molts, frame-0 code, fixed frame) and in
     GAME mode (one region, funded laying from a full stomach, animated wells, oriented body, molts of
     10 steps, seeded with the game's 96); plus the GRID core's game mode and the FIELD core, for the
     feel table;
  2. decodes every exported tadpole through swarm_nca.decode and scores it with swarm_nca.swarm_loss
     (default LossCfg) against all four targets - the cross row whose diagonal is the own-plan loss;
  3. runs the Python sort itself (results/sort/params.json) at the same seeds as the reference;
  4. computes swarm_feel.metrics on a 64-step window of each grown body;
  5. reports the LOSSLESS count (members that vanished without being killed - scorecard.py's count)
     for every C# run, and for the Python sort (scorecard.lossless_and_cost) unless --no-ref.

GAME mode is scored against the ONE-DOMAIN version of each plan (every unit's slot set to 0), as
score_grid.py does: the game's one-colour law has no domain regions. The research code is read from
the research branch (git archive of Tools/NCA from origin/cece/gifted-curie-x2cpd0) unless --nca
points at a checkout. Needs torch + numpy.
"""
import argparse
import json
import os
import statistics
import subprocess
import sys
import tempfile
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from score_grid import research_dir, build_swarm, one_domain, row, KINDS, PLANS  # noqa: E402


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--seeds", default="7,23,41,108,209")
    ap.add_argument("--nca", default="")
    ap.add_argument("--no-ref", action="store_true", help="skip the Python sort reference run")
    ap.add_argument("--out", default="", help="write the full results JSON here")
    ap.add_argument("--modes", default="sortresearch,sortgame,game,field")
    a = ap.parse_args()
    seeds = [int(x) for x in a.seeds.split(",")]

    nca = research_dir(a.nca)
    sys.path.insert(0, nca)
    import torch
    torch.set_num_threads(4)
    import swarm_nca as sn
    import swarm_feel
    targets = sn.load_targets(os.path.join(nca, "results", "swarm_targets"))
    targets1 = {k: one_domain(sn, T) for k, T in targets.items()}
    L = sn.LossCfg()

    exp = os.path.join(tempfile.mkdtemp(prefix="swarm_export_"), "states.json")
    t0 = time.time()
    subprocess.run(["bash", os.path.join(HERE, "run.sh"), "export", PLANS, exp, ",".join(map(str, seeds)), "240", "64", a.modes], check=True)
    runs = json.load(open(exp))["runs"]
    print(f"\nC# export: {len(runs)} runs in {time.time() - t0:.0f}s")

    label = {"sortresearch": "C# sort research", "sortgame": "C# sort game (1-domain)", "game": "C# grid game (1-domain)",
             "field": "C# field game"}
    res = {"seeds": seeds, "csharp": {}, "python": {}, "feel": {}, "lossless": {}}
    for r in runs:
        mode = r["mode"]
        P = torch.tensor(r["win"], dtype=torch.float32)
        if P.ndim == 3 and P.shape[1] >= 8:
            m = swarm_feel.metrics(P, torch.tensor(r["winElem"]), r["kind"])
            res["feel"].setdefault(label[mode], {}).setdefault(r["kind"], []).append(m)
        if mode == "field":
            continue
        if "selfDeaths" in r:
            res["lossless"].setdefault(label[mode], []).append(r["selfDeaths"])
        sw = build_swarm(sn, torch, r["units"])
        x = sn.decode(sw, 0)
        T = targets if mode == "sortresearch" else targets1
        res["csharp"].setdefault(label[mode], {}).setdefault(r["kind"], {})[r["seed"]] = dict(
            cross=row(sn, x, T, L), n=int(sw.hatched[0].sum()), ms=r["ms"])

    if not a.no_ref:
        import sort_model
        model = sort_model.load(os.path.join(nca, "results", "sort", "params.json"))
        model.world = sn.World()
        t0 = time.time()
        for seed in seeds:
            for k in KINDS:
                gen = sn.make_gen(seed)
                model.mem = {}
                sw = sn.seed_swarm([targets[k]], model.world, gen)
                for _ in range(240):
                    sw = model(sw, gen)
                tt = time.time()
                sw2 = sw.clone()
                for _ in range(32):
                    sw2 = model(sw2, gen)
                ms = (time.time() - tt) * 1000 / 32
                x = sn.decode(sw, 0)
                res["python"].setdefault(k, {})[seed] = dict(cross=row(sn, x, targets, L),
                                                           n=int((sw.active[0] & sw.hatched[0]).sum()), ms=round(ms, 2))
                P, alive, el = [], (sw.active[0] & sw.hatched[0]).clone(), sw.elem[0].clone()
                for _ in range(65):
                    P.append(sw.pos[0].clone()); sw = model(sw, gen)
                    alive &= (sw.active[0] & sw.hatched[0]) & (sw.elem[0] == el)
                m = swarm_feel.metrics(torch.stack(P)[:, alive], el[alive], k)
                res["feel"].setdefault("python sort", {}).setdefault(k, []).append(m)
        print(f"Python sort reference: {len(seeds) * 4} runs in {time.time() - t0:.0f}s")
        try:
            import scorecard
            model.mem = {}
            res["python_lossless"] = scorecard.lossless_and_cost(model, seed=seeds[0])
        except Exception as ex:   # the scorecard needs swarm_eval; report rather than fail the comparison
            res["python_lossless"] = {"error": repr(ex)}

    # ── report ──
    def own(table, k):
        return [table[k][s]["cross"][k] for s in seeds if s in table.get(k, {})]

    def strict(table, k):
        out = []
        for s in seeds:
            c = table[k][s]["cross"]; n = table[k][s]["n"]
            out.append(n >= sn.MIN_TEST_BODY and c[k] <= sn.MAX_TEST_LOSS and c[k] < min(v for q, v in c.items() if q != k))
        return out

    print("\nOWN-PLAN LOSS (swarm_loss sink, default LossCfg; bar 8)  per seed " + ",".join(map(str, seeds)))
    tables = [("python sort", res["python"])] + [(lab, res["csharp"].get(lab, {})) for lab in
                                                  ("C# sort research", "C# sort game (1-domain)", "C# grid game (1-domain)")]
    for lab, table in tables:
        if not table:
            continue
        for k in KINDS:
            v = own(table, k)
            ok = strict(table, k)
            ns = [table[k][s]["n"] for s in seeds]
            ms = [table[k][s]["ms"] for s in seeds]
            print(f"  {lab:<24} {k:<6} " + " ".join(f"{x:5.2f}" for x in v) +
                  f"   mean {statistics.mean(v):5.2f}  pass {sum(ok)}/{len(ok)}  n~{round(statistics.mean(ns))}  {statistics.mean(ms):.3f} ms/step")
    print("\nFEEL (swarm_feel.metrics, mean over plans and seeds)")
    keys = ["speed", "jerk_rel", "planar_frac", "coherence", "jitter", "phase", "stuck", "osc"]
    for lab, d in res["feel"].items():
        allm = [m for ms in d.values() for m in ms if "speed" in m]
        if allm:
            mean = {q: statistics.mean(m[q] for m in allm) for q in keys}
            # the research's ORGANIC band (swarm_feel.BAND, calibrated on the lead's review); planar
            # excess is the worst plan's mean planar_frac above the plan's own
            pe = max(max(0.0, statistics.mean(m["planar_frac"] for m in ms if "speed" in m) - ms[0].get("planar_plan", 0.0))
                     for ms in d.values() if any("speed" in m for m in ms))
            b = swarm_feel.BAND
            checks = dict(jerk_rel=b["jerk_rel"][0] <= mean["jerk_rel"] <= b["jerk_rel"][1], osc=mean["osc"] <= b["osc_max"],
                          stuck=mean["stuck"] <= b["stuck_max"], planar=pe <= b["planar_excess_max"])
            verdict = "ORGANIC" if all(checks.values()) else "out (" + ", ".join(k for k, v in checks.items() if not v) + ")"
            res.setdefault("organic", {})[lab] = dict(checks=checks, planar_excess=round(pe, 3))
            print(f"  {lab:<24} " + "  ".join(f"{q} {mean[q]:.3f}" for q in keys) + f"  planar_excess {pe:.3f}  -> {verdict}")
    print("\nLOSSLESS (members that vanished without being killed, per run)")
    for lab, v in res["lossless"].items():
        print(f"  {lab:<24} {sum(v)} over {len(v)} runs")
    if "python_lossless" in res:
        pl = res["python_lossless"]
        print(f"  {'python sort (scorecard)':<24} " + (f"deaths {pl.get('deaths')}  lossless {pl.get('lossless')}" if "error" not in pl else pl["error"]))
    if a.out:
        def keyify(o):
            if isinstance(o, dict):
                return {str(k): keyify(v) for k, v in o.items()}
            if isinstance(o, list):
                return [keyify(v) for v in o]
            return o
        json.dump(keyify(res), open(a.out, "w"), indent=1)
        print(f"\nwrote {a.out}")


if __name__ == "__main__":
    main()
