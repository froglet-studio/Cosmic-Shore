"""Past the box: genes drawn from an interval 3x as wide as zoo_model.GENES (centred on it, negatives clamped
to 0, ints rounded, dwell >= 1), to find where the yardstick starts to fail. -> results/zoo/boundary_far.json"""
import json, os, sys, time
from concurrent.futures import ProcessPoolExecutor
import numpy as np
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import zoo_model as zm
from zoo_boundary import _one


def far(rng):
    g = {}
    for k, (lo, hi, kind) in zm.GENES.items():
        if kind == "b":
            g[k] = int(rng.random() < 0.5); continue
        v = lo + (rng.random() * 3 - 1) * (hi - lo)
        v = max(v, 0.0)
        g[k] = int(round(v)) if kind == "i" else round(v, 4)
    g["dwell"] = max(1, g["dwell"]); g["molt_steps"] = max(1, g["molt_steps"]); g["morph_steps"] = max(2, g["morph_steps"])
    g["lay_max"] = max(1, g["lay_max"]); g["jitter_tau"] = max(1.0, g["jitter_tau"]); g["vscale"] = max(0.05, g["vscale"])
    g["sense"] = max(0.1, g["sense"]); g["breathe_period"] = max(4.0, g["breathe_period"]); g["lay_rate"] = max(0.002, g["lay_rate"])
    return g


if __name__ == "__main__":
    n = int(sys.argv[1]) if len(sys.argv) > 1 else 40
    rng = np.random.default_rng(7)
    gs = [far(rng) for _ in range(n)]
    t0 = time.time()
    with ProcessPoolExecutor(4) as ex:
        res = list(ex.map(_one, gs))
    ok = sum(r["ok"] for r in res)
    fails = {}
    for r in res:
        for f in r["fails"]:
            fails[f] = fails.get(f, 0) + 1
    # per-gene: how far outside the box were the failing ones?
    U = np.array([[(r["g"][k] - zm.GENES[k][0]) / (zm.GENES[k][1] - zm.GENES[k][0]) for k in zm.NAMES] for r in res])
    y = np.array([r["ok"] for r in res])
    sep = {}
    if 0 < y.sum() < len(y):
        d = U[y].mean(0) - U[~y].mean(0)
        sep = {zm.NAMES[i]: round(float(d[i]), 2) for i in np.argsort(-np.abs(d))[:10]}
    out = dict(n=n, ok=ok, rate=round(ok / n, 3), fails=fails, gene_separation=sep, seconds=round(time.time() - t0), runs=res)
    json.dump(out, open(os.path.join(HERE, "results", "zoo", "boundary_far.json"), "w"), indent=1)
    print(f"{ok}/{n} pass; failing tests {fails}; separating genes (pass-mean minus fail-mean, unit) {sep}")
