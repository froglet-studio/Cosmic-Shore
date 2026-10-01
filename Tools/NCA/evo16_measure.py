"""evo16: measure a model on the 16-transition yardstick (swarm_eval) at several seeds, in parallel.

    python Tools/NCA/evo16_measure.py --model evo:Tools/NCA/results/evo/genome.npy --seeds 7,23,41,59,77,95,113 \
        --out runs/evo16/measure_evo.json

One process per seed (1 torch thread each, 4 at a time). Writes the per-seed swarm_eval result and a
per-transition pass-rate table (fraction of seeds whose majority-of-3 passed, and the mean sample rate).
`--model evo16:<genome.npy>` loads the evo16 model (evo16_model.Evo16Rule).
"""
import argparse
import json
import os
import sys
import time
from evo16_pool import pinned_pool

os.environ.setdefault("OMP_NUM_THREADS", "1")
os.environ.setdefault("MKL_NUM_THREADS", "1")
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)


def load(spec):
    import swarm_eval as se
    kind, path = spec.split(":", 1)
    if kind == "evo16":
        import numpy as np
        import evo16_model as e16
        return e16.Evo16Rule(np.load(path))
    return se.load_model(spec)


def _one(args):
    spec, seed, samples = args
    import torch
    torch.set_num_threads(1)
    import swarm_eval as se
    m = load(spec)
    return seed, se.evaluate(m, seed=seed, samples=samples, full=True, log=lambda *a: None)


def table(results):
    """results: {seed: eval} -> {transition: dict(pass=fraction of seeds ok, rate=mean sample rate)}"""
    import swarm_nca as sn
    keys = [f"{k}->{k}" for k in sn.KINDS]
    for r in results.values():
        for t in r["switch"]:
            if t not in keys:
                keys.append(t)
    out = {}
    for t in keys:
        oks, rates, na = [], [], 0
        for r in results.values():
            a, b = t.split("->")
            v = r["own"][a] if a == b else r["switch"].get(t)
            if v is None:
                na += 1; continue
            oks.append(v["ok"]); rates.append(v["rate"])
        out[t] = dict(pass_=round(sum(oks) / max(1, len(oks)), 2), rate=round(sum(rates) / max(1, len(rates)), 2), na=na)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", required=True)
    ap.add_argument("--seeds", default="7,23,41,59,77,95,113")
    ap.add_argument("--samples", type=int, default=3)
    ap.add_argument("--out", required=True)
    a = ap.parse_args()
    seeds = [int(s) for s in a.seeds.split(",")]
    t0 = time.time()
    with pinned_pool(min(4, len(seeds))) as pool:
        res = dict(pool.map(_one, [(a.model, s, a.samples) for s in seeds]))
    tab = table(res)
    summ = {s: f"{r['passed']}/{r['feasible']}" for s, r in res.items()}
    tot = sum(r["passed"] for r in res.values()); feas = sum(r["feasible"] for r in res.values())
    print(json.dumps(summ))
    for t, v in tab.items():
        print(f"  {t:16s} pass {v['pass_']:.2f}  rate {v['rate']:.2f}  n/a {v['na']}")
    print(f"TOTAL {tot}/{feas} ({tot / max(1, feas):.3f})  [{time.time() - t0:.0f}s]")
    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    json.dump(dict(model=a.model, seeds=seeds, per_seed=summ, total=tot, feasible=feas, table=tab,
                   results={str(k): v for k, v in res.items()}), open(a.out, "w"), indent=1)


if __name__ == "__main__":
    main()
