"""Targeted check: the three switches INTO the dragonfly + its own plan, seed 23, 3 samples, for given overrides."""
import json, sys
from multiprocessing import Pool
def run(args):
    kw, k, r = args
    import torch; torch.set_num_threads(1)
    import swarm_nca as sn, swarm_eval as se, sortfeel_model as fm
    m = fm.from_sort("results/sortfeel/params.json", **kw)
    T = sn.load_targets(); L = sn.LossCfg()
    sw, gen = se._grow(m, [k], T, 240, 23 + 101 * r)
    if k == "time":
        row = se._score_row(sw, 0, T, L); return k, r, row["time"], se._passes(row, "time", se._alive(sw, 0))
    se.cull_to(sw, 0, 3, gen)
    for _ in range(240): sw = m(sw, gen)
    row = se._score_row(sw, 0, T, L); return k, r, row["time"], se._passes(row, "time", se._alive(sw, 0))
if __name__ == "__main__":
    kw = json.loads(sys.argv[1])
    with Pool(4) as p: R = p.map(run, [(kw, k, r) for k in ("mass", "space", "charge", "time") for r in range(3)])
    out = {}
    for k, r, l, ok in R: out.setdefault(k, []).append((round(l, 2), ok))
    print(sys.argv[1], json.dumps({k: (round(sum(x[0] for x in v) / 3, 2), sum(x[1] for x in v)) for k, v in out.items()}))
