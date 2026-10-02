"""Retune an existing creature toward a target emotion: CMA-ES started AT its parameters with a small step,
so the result stays close to the original (the 'nudge this species toward dread' use case). Reports the
parameter distance moved and which parameters moved most. -> results/retune.json"""
import json, os, sys
from multiprocessing import Pool
import numpy as np
import search
from critter import PARAMS, decode
from sim import HERE

src, target = sys.argv[1], sys.argv[2]
th0 = np.array(json.load(open(os.path.join(HERE, "results", "search.json")))[src]["cma"]["theta"])
with Pool(4) as pool:
    (bf, bx), hist = search.cma(target, 10, 12, pool, seed=3, start=th0, sigma0=0.12)
    val = search._eval((bx, target, search.VAL_SEEDS, search.VAL_VIEWERS))
moved = sorted(((abs(bx[i] - th0[i]), PARAMS[i][0], round(float(th0[i]), 2), round(float(bx[i]), 2)) for i in range(len(th0))), reverse=True)[:6]
res = dict(source=src, target=target, fitness=round(bf, 3), val_fitness=round(val[0], 3), val_hit=round(val[1], 3),
           l2_move=round(float(np.linalg.norm(bx - th0)), 3), moved=[(n, a, b) for _, n, a, b in moved], history=hist,
           params={k: (round(v, 2) if isinstance(v, float) else v) for k, v in decode(bx).items()})
p = os.path.join(HERE, "results", "retune.json"); allr = json.load(open(p)) if os.path.exists(p) else {}
allr[f"{src}->{target}"] = res; json.dump(allr, open(p, "w"), indent=1)
print(json.dumps({k: v for k, v in res.items() if k not in ("history", "params")}))
