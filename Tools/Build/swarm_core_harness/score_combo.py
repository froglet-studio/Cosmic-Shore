#!/usr/bin/env python3
"""Score the SHIPPED grid core in its round-5 form (SwarmGridCore.cs = the research's `combo`: hgrid2 made
LOSSLESS) with the research's UNCHANGED scorer, on the research's own 16-transition yardstick.

    python3 Tools/Build/swarm_core_harness/score_combo.py [--seeds 7,23,41] [--samples 3]
            [--modes research16,research8,game16,game8,legacy] [--no-ref] [--out results.json]

  1. builds and runs the C# harness in `yardstick` mode: for each mode, seed, plan and sample it grows the
     swarm 240 steps from the mode's seed and records it (the OWN-plan test), then for every other element
     regrows the identical swarm, culls it with swarm_eval.cull_to's rule (ported) and runs 240 steps (the
     12 SWITCH tests). Every record is the full tadpole state.
  2. scores every record with swarm_nca.decode + swarm_nca.swarm_loss (default LossCfg, unchanged) against all
     four targets and applies swarm_eval._passes (alive >= 32, within the loss-8 bar, strictly closest to the
     wanted plan) - a test passes on a majority of its samples, exactly as swarm_eval.evaluate counts.
  3. runs the Python combo itself (results/combo/g16/params.json and results/combo/params.json = C8) through
     swarm_eval.evaluate at the same seeds as the reference, plus scorecard.lossless_and_cost.
  4. swarm_feel.metrics on a 64-step window of each grown own-plan body (C# modes).

Modes: research16 / research8 = the C# core as the research runs combo (three domain slots, free laying, fixed
frame, membrane 80, the research seed of 16); game16 / game8 = what ships (one domain, funded laying from a full
stomach, oriented, lock 30, animated molts, the game's 96 seed), scored against the ONE-DOMAIN plan (score_grid's
one_domain); legacy = the pre-round-5 game core (finding 17's debris), for contrast.
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
import score_grid as sg  # noqa: E402  (build_swarm, one_domain, research_dir)

KINDS = sg.KINDS


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--seeds", default="7,23,41")
    ap.add_argument("--samples", type=int, default=3)
    ap.add_argument("--modes", default="research16,research8,game16,game8,legacy")
    ap.add_argument("--nca", default="")
    ap.add_argument("--no-ref", action="store_true")
    ap.add_argument("--ref-only", action="store_true")
    ap.add_argument("--out", default="")
    a = ap.parse_args()
    seeds = [int(x) for x in a.seeds.split(",")]
    nca = sg.research_dir(a.nca)
    sys.path.insert(0, nca)
    import torch
    torch.set_num_threads(1)
    import swarm_nca as sn
    import swarm_eval as se
    import swarm_feel
    targets = sn.load_targets(os.path.join(nca, "results", "swarm_targets"))
    targets1 = {k: sg.one_domain(sn, T) for k, T in targets.items()}
    L = sn.LossCfg()
    res = {"seeds": seeds, "samples": a.samples, "csharp": {}, "python": {}, "feel": {}}

    if not a.ref_only:
        exp = os.path.join(tempfile.mkdtemp(prefix="swarm_yard_"), "yard.json")
        t0 = time.time()
        subprocess.run(["bash", os.path.join(HERE, "run.sh"), "yardstick", sg.PLANS, exp, ",".join(map(str, seeds)),
                        str(a.samples), a.modes], check=True)
        dump = json.load(open(exp))
        print(f"\nC# yardstick export: {len(dump['runs'])} records in {time.time() - t0:.0f}s; self-inflicted deaths {dump['selfDeaths']}")
        res["selfDeaths"] = dump["selfDeaths"]
        tests = {}
        for r in dump["runs"]:
            key = (r["mode"], r["seed"], r["kind"], r["want"])
            if r.get("na"):
                tests.setdefault(key, [])
                continue
            sw = sg.build_swarm(sn, torch, r["units"])
            x = sn.decode(sw, 0)
            T = targets if r["mode"].startswith("research") else targets1
            row = {k: round(sn.swarm_loss(x, T[k], L)[1]["sink"], 2) for k in KINDS}
            n = int(sw.hatched[0].sum())
            ok = se._passes(row, r["want"], n)
            tests.setdefault(key, []).append(dict(ok=ok, row=row, n=n, stray=r["stray"], molts=r["molts"]))
            if r["tag"] == "own" and "win" in r:
                P = torch.tensor(r["win"], dtype=torch.float32)
                if P.ndim == 3 and P.shape[1] >= 8:
                    m = swarm_feel.metrics(P, torch.tensor(r["winElem"]), r["kind"] if r["mode"].startswith("research") else None)
                    res["feel"].setdefault("csharp_" + r["mode"], {}).setdefault(r["kind"], []).append(m)
        for (mode, seed, k, want), rs in tests.items():
            d = res["csharp"].setdefault(mode, {}).setdefault(str(seed), {"tests": {}})
            if not rs:
                d["tests"][f"{k}->{want}"] = dict(na=True)
                continue
            oks = [q["ok"] for q in rs]
            d["tests"][f"{k}->{want}"] = dict(ok=sum(oks) * 2 > len(rs), rate=round(sum(oks) / len(rs), 2),
                                             loss=round(statistics.mean(q["row"][want] for q in rs), 2),
                                             n=round(statistics.mean(q["n"] for q in rs)),
                                             stray=round(statistics.mean(q["stray"] for q in rs), 3))

    if not a.no_ref:
        import combo_model as cm
        import scorecard as scd
        for label, path in (("combo_g16", os.path.join(nca, "results", "combo", "g16", "params.json")),
                            ("combo_g8", os.path.join(nca, "results", "combo", "params.json"))):
            model = cm.load(path)
            for s in seeds:
                t0 = time.time()
                ev = se.evaluate(model, seed=s, samples=a.samples, full=True, log=lambda *_: None)
                tt = {}
                for k in KINDS:
                    o = ev["own"][k]; tt[f"{k}->{k}"] = dict(ok=o["ok"], rate=o["rate"], loss=o["cross"][k], n=o["n"])
                for key, v in ev["switch"].items():
                    k, to = key.split("->")
                    tt[f"{k}->{to}"] = dict(ok=v["ok"], rate=v["rate"], loss=v["cross"][to], n=v["n"])
                for key in ev["na"]:
                    tt[key] = dict(na=True)
                res["python"].setdefault(label, {})[str(s)] = {"tests": tt, "passed": ev["passed"], "feasible": ev["feasible"]}
                print(f"  Python {label} seed {s}: {ev['passed']}/{ev['feasible']} ({time.time() - t0:.0f}s)", flush=True)
            lc = scd.lossless_and_cost(model, seed=seeds[0])
            res["python"][label]["lossless"] = lc
            print(f"  Python {label}: deaths {lc['deaths']}  {lc['ms_per_step']} ms/step @ {lc['grown_n']}", flush=True)

    # ── report ──
    def summarise(table):
        out = {}
        for s, d in table.items():
            if not isinstance(d, dict) or "tests" not in d:
                continue
            t = d["tests"]
            feas = [v for v in t.values() if not v.get("na")]
            own = [t.get(f"{k}->{k}", {}) for k in KINDS]
            out[s] = dict(passed=sum(v["ok"] for v in feas), feasible=len(feas),
                          own=[v.get("loss") for v in own], own_ok=sum(bool(v.get("ok")) for v in own),
                          fails=sorted(f"{k}({v['loss']})" for k, v in t.items() if not v.get("na") and not v["ok"]))
        return out

    print("\nYARDSTICK (swarm_eval: 4 own + 12 switches, majority of samples, loss-8 bar)")
    rows = [("Python " + k, v) for k, v in res["python"].items()] + [("C# " + k, v) for k, v in res["csharp"].items()]
    for label, table in rows:
        for s, v in summarise(table).items():
            own = " / ".join(f"{x:.2f}" if x is not None else "-" for x in v["own"])
            print(f"  {label:<18} seed {s:>3}: {v['passed']:2d}/{v['feasible']}  own {own}  fails {v['fails']}")
    print("\nFEEL (swarm_feel.metrics, mean over plans and seeds)")
    keys = ["speed", "jerk_rel", "planar_frac", "coherence", "jitter", "phase", "stuck", "osc"]
    for label, d in res["feel"].items():
        allm = [m for ms in d.values() for m in ms if "speed" in m]
        if allm:
            print(f"  {label:<18} " + "  ".join(f"{q} {statistics.mean(m[q] for m in allm):.3f}" for q in keys))
    if a.out:
        json.dump(res, open(a.out, "w"), indent=1)
        print(f"\nwrote {a.out}")


if __name__ == "__main__":
    main()
