"""Experiment A: does a one-node extension operator buy judge score per byte, on the best existing genomes?

For each base genome (best of the fronts), make variants whose sky (out slot 4) is replaced by a small ext subgraph,
tune ONLY the variable subgraph's bytes with a (1+L) evolution strategy under the judge (mean of InceptionV3 and
MobileNetV2 KID, 1.0 = Amoebius held-out), and compare against a CONTROL that tunes the original sky subgraph with the
same budget. Reports judge, stat distance and zbytes (pruned) per arm.

  python3 -m ops_ext.tools.skyswap OUT_DIR [--evals 32] [--bases N] [--arms a,b,c] [--shard i/k]
"""
import argparse
import json
import os
import random
import sys
import time

import numpy as np

SHARE = "/mnt/project-files/amoebius-genome"


def load_bases(n):
    from agenome.genome import Genome
    rows = []
    for f in ["runs/main/pareto.json", "runs/islandB/pareto.json", "runs/islandA/pareto.json"]:
        try:
            for c in json.load(open(os.path.join(SHARE, f))):
                rows.append((c.get("judge", 9.0) if "judge" in c else 5 + c.get("dist", 9), f.split("/")[1], c["genome"]))
        except Exception:
            pass
    rows.sort(key=lambda r: r[0])
    bases = []
    seen = set()
    for j, src, gj in rows:
        g = Genome.from_json(gj)
        k = g.to_bytes()
        if k in seen:
            continue
        seen.add(k)
        bases.append((f"{src}_{len(bases)}", g))
        if len(bases) >= n - 2:
            break
    for p in ["seeds/amoebius_like/minimal__tuned.json", "seeds/amoebius_like/orange_plane_feedback__tuned.json"]:
        try:
            bases.append((os.path.basename(p)[:-5], Genome.load(os.path.join(SHARE, p))))
        except Exception as e:
            print("skip", p, e)
    return bases


def sky_nodes(g):
    """Indices of the T subgraph feeding the root's sky slot."""
    r = g.root()
    s = g.nodes[r].inputs[4]
    if s is None:
        return set()
    from ops_ext.bake import t_ancestors
    return t_ancestors(g, s)


ARMS = {
    # name: list of (op, input refs relative to this list, params) ; the last node becomes the sky
    "control": None,
    "warpfbm": [("tex.warpfbm", [], {})],
    "spectral": [("tex.spectral", [], {})],
    "spectral_palette": [("tex.spectral", [], {}), ("tex.palette", [0], {})],
    "warpfbm_flame": [("tex.warpfbm", [], {}), ("tex.flame", [], {"bright": 60}), ("tex.blend", [0, 1], {"mode": 0, "amount": 120})],
    "spectral_eigen": [("tex.spectral", [], {}), ("tex.eigen", [], {"dom": 220, "render": 60}), ("tex.blend", [0, 1], {"mode": 0, "amount": 60})],
    "orig_plus_flame": "plus:tex.flame",
    "orig_fftfilter": "filter:tex.fftfilter",
    "agents_ribbon": "afilter:ag.ribbon",
    "agents_flow": "afilter:ag.flow",
    "agents_curl": "afilter:ag.curl",
    "agents_fourier_ribbon": "afilter:ag.fourier+ag.ribbon",
    "sky_palette": "filter:tex.palette",
    "sky_galaxy": "filter:tex.galaxy",
    "sky_lines": "filter:tex.lines",
    "post_bandgain": "pfilter:post.bandgain",
    "cam_kick": "cfilter:cam.kick",
    "lines_bandgain_kick": "filter:tex.lines|pfilter:post.bandgain|cfilter:cam.kick",
}
SLOT = {"afilter": 1, "cfilter": 2, "pfilter": 3}


def make_arm(g, arm):
    """Return (genome, tunable node indices)."""
    from agenome.genome import Genome, Node, _insert
    from agenome.ops import OPS
    g = g.copy().ensure_root()
    spec = ARMS[arm]
    r = g.root()
    if spec is None:
        return g, sorted(sky_nodes(g))
    if isinstance(spec, str) and "|" in spec:  # several edits in sequence; tune the union of their nodes
        idx_all = []
        for k, part in enumerate(spec.split("|")):
            ARMS["_tmp"] = part
            g, idx = make_arm(g, "_tmp")
            if g is None:
                return None, []
            # every insert lands just before the root, so earlier indices stay valid
            idx_all = idx_all + idx
        del ARMS["_tmp"]
        return g, sorted(set(idx_all))
    if isinstance(spec, str) and spec.split(":")[0] in SLOT:
        slot = SLOT[spec.split(":")[0]]
        a = g.nodes[r].inputs[slot]
        if a is None and slot == 1:
            return None, []
        idx = []
        prev = a
        for op in spec.split(":")[1].split("+"):
            o = OPS[op]
            node = Node(op, [prev] + [None] * (len(o.inputs) - 1), [p.default for p in o.params])
            _insert(g, r, node)
            prev = r
            idx.append(r)
            r += 1
        g.nodes[r].inputs[slot] = prev
        return g.repair(), idx
    if isinstance(spec, str):
        kind, op = spec.split(":")
        s = g.nodes[r].inputs[4]
        if s is None:
            return None, []
        o = OPS[op]
        new = Node(op, [None] * len(o.inputs), [p.default for p in o.params])
        _insert(g, r, new)
        r += 1
        idx = [r - 1]
        if kind == "filter":
            g.nodes[r - 1].inputs[0] = s
            g.nodes[r].inputs[4] = r - 1
        else:  # plus: blend(original sky, op) additively
            o2 = OPS["tex.blend"]
            b = Node("tex.blend", [s, r - 1], [0, 90])
            _insert(g, r, b)
            r += 1
            g.nodes[r].inputs[4] = r - 1
            idx.append(r - 1)
        return g.repair(), idx
    base = r
    for k, (op, ins, ps) in enumerate(spec):
        o = OPS[op]
        prm = [p.default for p in o.params]
        names = [p.name for p in o.params]
        for kk, v in ps.items():
            prm[names.index(kk)] = v
        node = Node(op, [base + j for j in ins] + [None] * (len(o.inputs) - len(ins)), prm)
        _insert(g, base + k, node)
    r = g.root()
    g.nodes[r].inputs[4] = base + len(spec) - 1
    g = g.repair()
    # prune the dead original sky; recompute tunable indices by op identity after pruning
    tun_ops = len(spec)
    g2 = g.copy()
    keep = sorted(g2.live())
    new_idx = [keep.index(base + k) for k in range(tun_ops) if (base + k) in keep]
    g2.prune()
    return g2, new_idx


class Scorer:
    def __init__(self):
        from agenome.render import Renderer
        from agenome.judge import Judge
        from agenome.features import TargetStats
        self.R = Renderer()
        self.Js = [Judge(n, "native", 12) for n in ("inception", "mobilenet")]
        self.ST = TargetStats.load(os.path.join(SHARE, "fitness/target_stats_reels_train.npz"))
        self.n = 0

    def __call__(self, g):
        from agenome.features import window_features
        self.n += 1
        try:
            fr = self.R.render(g, 60)
        except Exception as e:
            return {"judge": 99.0, "err": str(e)[:200]}
        sel = fr[np.linspace(0, 59, 12).round().astype(int)]
        js = [J.raw(sel)["judge"] for J in self.Js]
        st = self.ST.distance(window_features(fr, sub=2))["total"]
        jv = float(max(js)) if os.environ.get("JUDGE_V", "3") == "3" else float(np.mean(js))  # v3 = worse net
        return {"judge": jv, "judges": [round(x, 3) for x in js], "stat": round(float(st), 4),
                "zbytes": g.size(), "nodes": len(g.nodes), "frames": fr}


def tune(g, idx, score, evals, rng, lam=4):
    best = g.copy()
    bs = score(best)
    hist = [bs["judge"]]
    sigma = 24.0
    used = 1
    while used < evals:
        kids = []
        for _ in range(lam):
            c = best.copy()
            for _ in range(1 + int(rng.expovariate(0.8))):
                i = rng.choice(idx)
                if not c.nodes[i].params:
                    continue
                j = rng.randrange(len(c.nodes[i].params))
                if rng.random() < 0.15:
                    c.nodes[i].params[j] = rng.randrange(256)
                else:
                    c.nodes[i].params[j] = int(max(0, min(255, round(c.nodes[i].params[j] + rng.gauss(0, sigma)))))
            kids.append(c.repair())
        improved = False
        for c in kids:
            s = score(c)
            used += 1
            if s["judge"] < bs["judge"]:
                best, bs, improved = c, s, True
        sigma = min(64, sigma * 1.3) if improved else max(6, sigma * 0.85)
        hist.append(bs["judge"])
    return best, bs, hist


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("out")
    ap.add_argument("--evals", type=int, default=32)
    ap.add_argument("--bases", type=int, default=5)
    ap.add_argument("--arms", default=",".join(ARMS))
    ap.add_argument("--shard", default="0/1")
    a = ap.parse_args(argv)
    import ops_ext
    ops_ext.install()
    os.makedirs(a.out, exist_ok=True)
    si, sk = map(int, a.shard.split("/"))
    bases = load_bases(a.bases)
    jobs = [(bn, bg, arm) for bn, bg in bases for arm in a.arms.split(",")]
    jobs = jobs[si::sk]
    S = Scorer()
    out_path = os.path.join(a.out, f"results_{si}.jsonl")
    done = set()
    if os.path.exists(out_path):
        for ln in open(out_path):
            d = json.loads(ln)
            done.add((d["base"], d["arm"]))
    from PIL import Image
    for bn, bg, arm in jobs:
        if (bn, arm) in done:
            continue
        g, idx = make_arm(bg, arm)
        if g is None or not idx:
            print("skip", bn, arm, flush=True)
            continue
        t0 = time.time()
        rng = random.Random(hash((bn, arm)) & 0xFFFF)
        s0 = S(g)
        best, bs, hist = tune(g, idx, S, a.evals, rng)
        rec = {"base": bn, "arm": arm, "judge0": round(s0["judge"], 4), "zbytes0": s0.get("zbytes"),
               "judge": round(bs["judge"], 4), "judges": bs.get("judges"), "stat": bs.get("stat"),
               "zbytes": bs.get("zbytes"), "nodes": bs.get("nodes"), "hist": [round(h, 3) for h in hist],
               "sec": round(time.time() - t0, 1), "genome": best.to_json()}
        with open(out_path, "a") as f:
            f.write(json.dumps(rec) + "\n")
        if "frames" in bs:
            fr = bs["frames"]
            Image.fromarray(np.concatenate([fr[i] for i in (0, 20, 40, 59)], 1)).save(
                os.path.join(a.out, f"{bn}__{arm}.png"))
        print(f"{bn:28s} {arm:18s} judge {s0['judge']:.3f} -> {bs['judge']:.3f}  zbytes {s0.get('zbytes')} -> "
              f"{bs.get('zbytes')}  {time.time() - t0:.0f}s", flush=True)


if __name__ == "__main__":
    main()
