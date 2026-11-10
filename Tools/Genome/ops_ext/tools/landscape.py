"""Experiment C: how rich is the landscape near a scaffold, with and without the extension ops?

Fix a minimal Amoebius scaffold (agents + trail + chase camera + post, i.e. minimal__tuned without its sky), then
sample random sky subgraphs of 1-3 nodes drawn either from the core texture ops or from core + ext, render, judge.
Reports per arm: the judge distribution (quantiles, fraction under thresholds), bytes, failures, render time.
A better operator set should put more random draws close to Amoebius at the same byte budget.

  python3 -m ops_ext.tools.landscape OUT_DIR --n 120 --arm core|ext [--shard i/k]
"""
import argparse
import json
import os
import random
import time

import numpy as np

SHARE = "/mnt/project-files/amoebius-genome"


def random_sky(g, rng, pool, k):
    """Append k random T nodes (each wired to earlier T nodes at random) and point the sky at the last."""
    from agenome.genome import Node, _insert
    from agenome.ops import OPS
    r = g.root()
    first = r
    for j in range(k):
        o = rng.choice(pool)
        ins = []
        for s in o.inputs:
            cands = list(range(first, r + j)) if j else []
            ins.append(rng.choice(cands) if cands else None)
        node = Node(o.name, ins, [rng.randrange(256) for _ in o.params])
        _insert(g, r + j, node)
    g.nodes[g.root()].inputs[4] = g.root() - 1
    return g.repair()


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("out")
    ap.add_argument("--n", type=int, default=120)
    ap.add_argument("--arm", default="ext")
    ap.add_argument("--shard", default="0/1")
    ap.add_argument("--seed", type=int, default=1)
    a = ap.parse_args(argv)
    import ops_ext
    ops_ext.install()
    from agenome.genome import Genome
    from agenome.ops import ops_of_type
    from ops_ext.registry import EXT
    from ops_ext.tools.skyswap import Scorer
    os.makedirs(a.out, exist_ok=True)
    si, sk = map(int, a.shard.split("/"))
    base = Genome.load(os.path.join(SHARE, "seeds/amoebius_like/minimal__tuned.json")).ensure_root()
    T = ops_of_type("T")
    pool = [o for o in T if o.name not in EXT] if a.arm == "core" else T
    S = Scorer()
    rng = random.Random(a.seed * 1000 + si)
    path = os.path.join(a.out, f"{a.arm}_{si}.jsonl")
    for t in range(si, a.n, sk):
        g = random_sky(base.copy(), rng, pool, rng.choice([1, 2, 2, 3]))
        g.prune()
        t0 = time.time()
        s = S(g)
        rec = {"arm": a.arm, "i": t, "judge": round(s["judge"], 4), "stat": s.get("stat"), "zbytes": g.size(),
               "ops": [n.op for n in g.nodes if n.op.startswith("tex.")], "sec": round(time.time() - t0, 2),
               "err": s.get("err"), "genome": g.to_json()}
        with open(path, "a") as f:
            f.write(json.dumps(rec) + "\n")
        print(t, rec["judge"], rec["zbytes"], rec["ops"], rec["sec"], flush=True)


if __name__ == "__main__":
    main()
