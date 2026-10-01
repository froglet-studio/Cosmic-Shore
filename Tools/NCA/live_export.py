"""LIVE swarm: data export + fidelity scoring for the browser port (swarm_live.js).

  python live_export.py targets          -> results/live/targets.json (the four plans, as load_targets sees them)
  python live_export.py score <states>   -> score states exported by `node swarm_live.js fidelity` with the
                                           UNCHANGED Python scorer (swarm_nca.decode + swarm_loss, default LossCfg)
  python live_export.py pyref <n>        -> the Python hgrid oracle's own distribution (n seeds) for comparison

The JS port reads targets.json (node) or the same object embedded in the gallery page (browser). Nothing
here edits swarm_nca.py / hgrid_*.py; it only imports them.
"""
from __future__ import annotations

import json
import os
import sys
import time

import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402

torch.set_num_threads(int(os.environ.get("LIVE_THREADS", "2")))

OUT = os.path.join(HERE, "results", "live")
OUT2 = os.path.join(HERE, "results", "live2")
R4 = lambda a: [round(float(v), 4) for v in np.asarray(a).reshape(-1)]


def targets_json():
    T = sn.load_targets()
    out = {}
    for k, t in T.items():
        frames = []
        for fr in t.frames:
            c = fr["p"] - fr["p"].mean(0)          # PlanFields stores frames centred on their own centroid
            frames.append(dict(p=R4(c), e=fr["elem"].tolist(), sl=fr["slot"].tolist(), ti=fr["tier"].tolist(),
                               h=R4(fr["h"]), f=R4(fr["f"]), sp=R4(fr["sp"])))
        out[k] = dict(name=t.name, n=t.n, mix=list(map(int, t.mix)), slot_mix=list(map(int, t.slot_mix)), frames=frames)
    return out


def evo_json():
    """The evolved rule (results/evo: G2 MLP + CMA-ES genome) with the genome's output gain already folded
    into w3/b3, exactly as evo_model.EvoRule builds it. Weights as base64 little-endian float32."""
    import base64
    import math
    import evo_model as em
    g = np.load(os.path.join(HERE, "results", "evo", "genome.npy"))
    m = em.EvoRule(g)
    G = m.genome
    b64 = lambda t: base64.b64encode(t.detach().float().contiguous().numpy().astype("<f4").tobytes()).decode()
    w = {k: dict(shape=list(v.shape), b64=b64(v)) for k, v in m.state_dict().items()}
    gv = lambda n: em.gene(G, n)
    sw = lambda n: gv(n) > 0
    rate = (0.05 * np.tanh(G[em.SLICES["swirl"]])).tolist()
    return dict(
        weights=w, hidden=m.hidden, fire_rate=m.fire_rate, world={k: (list(v) if isinstance(v, tuple) else v) for k, v in m.world.__dict__.items()},
        lay=dict(on=sw("sw_lay"), k=gv("k_lay"), b=gv("b_lay")),
        egg=dict(on=sw("sw_egg"), p=1 / (1 + math.exp(-gv("p_egg"))), beta=gv("beta_egg")),
        lock=dict(on=sw("sw_lock"), margin=0.25 / (1 + math.exp(-gv("lock")))),
        D=m.D.tolist(), swirl=dict(on=sw("sw_swirl") and float(np.abs(rate).max()) > 1e-4, rate=rate),
        axes=em._AXES.tolist())


def sort_json():
    """The sort model (results/sort/params.json): its config plus every plan's positional-information code
    (sort_model.PlanCode - the k-means wells are built once with their own fixed seed, so the JS port reads
    them rather than re-deriving them). Wells per (element, slot): weight, mean, inverse covariance, logdet;
    the type's look as a full 32-channel state (field_swarm.invert_state)."""
    import sort_model as sm
    m = sm.load(os.path.join(HERE, "results", "sort", "params.json"))
    cfg = asdict_(m.cfg)
    codes = {}
    for k, c in m.codes.items():
        wells = {}
        for (e, s_), (w, mu, inv, ld) in c.wells.items():
            wells[f"{e},{s_}"] = dict(w=R6(w), mu=R6(mu), inv=R6(inv), ld=R6(ld), state=R6(c.state[(e, s_)]),
                                      next=[int(x) for x in c.next[(e, s_)]])
        codes[k] = dict(n=int(c.n), nslots=int(c.nslots), counts=c.counts.tolist(), axis=R6(c.axis), wells=wells)
    from field_swarm import invert_state
    fallback = [R6(invert_state(e, [1, 1, 1] if e != 2 else [2, .3, .3], 0, [0, 0, 1], [.3, 0])) for e in range(4)]
    return dict(cfg=cfg, codes=codes, fallback=fallback)


def asdict_(cfg):
    from dataclasses import asdict
    return {k: (list(v) if isinstance(v, tuple) else v) for k, v in asdict(cfg).items()}


R6 = lambda a: [round(float(v), 6) for v in np.asarray(a).reshape(-1)]


def swarm_from_states(st, N=None):
    """One exported JS state -> a 1-sample swarm_nca.Swarm (only the channels decode() reads matter)."""
    n = len(st["elem"])
    N = N or max(n, 1)
    pos = torch.zeros(1, N, 3); s = torch.zeros(1, N, sn.C)
    elem = torch.zeros(1, N, dtype=torch.long); dom = torch.zeros(1, N, dtype=torch.long)
    active = torch.zeros(1, N, dtype=torch.bool); hatched = torch.zeros(1, N, dtype=torch.bool)
    pos[0, :n] = torch.tensor(st["pos"], dtype=torch.float32).view(n, 3)
    S = torch.tensor(st["s"], dtype=torch.float32).view(n, -1)       # A | FAC 3 | PR 3 | TI 3 | SP 2 | DIE
    s[0, :n, sn.A] = S[:, 0]; s[0, :n, sn.FAC] = S[:, 1:4]; s[0, :n, sn.PR] = S[:, 4:7]
    s[0, :n, sn.TI] = S[:, 7:10]; s[0, :n, sn.SP] = S[:, 10:12]; s[0, :n, sn.DIE] = S[:, 12]
    elem[0, :n] = torch.tensor(st["elem"]); dom[0, :n] = torch.tensor(st["dom"])
    active[0, :n] = torch.tensor(st["active"], dtype=torch.bool); hatched[0, :n] = torch.tensor(st["hatched"], dtype=torch.bool)
    return sn.Swarm(pos, s, elem, dom, active, hatched, torch.zeros(1, dtype=torch.long))


def score_swarm(sw, T, L):
    x = sn.decode(sw, 0)
    row = {k: round(sn.swarm_loss(x, T[k], L)[1]["sink"], 2) for k in sn.KINDS}
    live = sw.active[0] & sw.hatched[0]
    mix = [int(((sw.elem[0] == e) & live).sum()) for e in range(4)]
    return row, int(live.sum()), mix


def score(path):
    T = sn.load_targets(); L = sn.LossCfg()
    data = json.load(open(path))
    res = []
    for st in data["states"]:
        row, n, mix = score_swarm(swarm_from_states(st), T, L)
        res.append(dict(kind=st["kind"], seed=st["seed"], step=st["step"], tag=st.get("tag", "own"), want=st.get("want", st["kind"]),
                        cross=row, n=n, mix=mix))
    return res


@torch.no_grad()
def pyref(nseeds, steps=240, kind="grid", switch=False):
    """The Python model grown from swarm_nca.seed_swarm, nseeds per plan: the hgrid oracle
    (results/hgrid/oracle meta.cfg), or kind="evo" the evolved rule (results/evo genome)."""
    if kind == "evo":
        import evo_model as em
        model = em.EvoRule(np.load(os.path.join(HERE, "results", "evo", "genome.npy")))
    elif kind.startswith("hgrid2"):
        import hgrid2_model as hm
        cfg = json.load(open(os.path.join(HERE, "results", "hgrid2", "params.json")))["cfg"]
        if kind == "hgrid2_g12":
            cfg["G"] = 12
        model = hm.Boid2(sn.World(), hm.Cfg(**cfg))
    elif kind == "sort":
        import sort_model as sm
        model = sm.load(os.path.join(HERE, "results", "sort", "params.json"))
    else:
        import hgrid_boid as hb
        cfg = hb.BoidCfg(**json.load(open(os.path.join(HERE, "results", "hgrid", "oracle", "summary.json")))["meta"]["cfg"])
        model = hb.make_oracle(cfg)
    T = sn.load_targets(); L = sn.LossCfg()
    res = []
    for k in sn.KINDS:
        for seed in range(nseeds):
            gen = sn.make_gen(1000 + seed)
            torch.manual_seed(1000 + seed)
            sw = sn.seed_swarm([T[k]], model.world, gen)
            t0 = time.time()
            for _ in range(steps):
                sw = model(sw, gen)
            row, n, mix = score_swarm(sw, T, L)
            res.append(dict(kind=k, seed=seed, step=steps, tag="own", want=k, cross=row, n=n, mix=mix, sec=round(time.time() - t0, 1)))
            print(k, seed, row[k], n, mix, flush=True)
            if switch:
                want = sn.lose_majority(sw, 0, gen, to=sn.SWITCH_TO[k])
                for _ in range(steps):
                    sw = model(sw, gen)
                if want:
                    row, n, mix = score_swarm(sw, T, L)
                    res.append(dict(kind=k, seed=seed, step=2 * steps, tag="switch", want=want, cross=row, n=n, mix=mix))
                    print("  switch", want, row[want], n, mix, flush=True)
    return res


def table():
    """Markdown fidelity table: Python model vs its JS port, own plan and after the yardstick cull,
    loss to the wanted plan (mean +- sd [min, max]) and the share under the loss-8 bar."""
    def load(p):
        p = os.path.join(OUT, p)
        return json.load(open(p)) if os.path.isfile(p) else []
    if os.environ.get("LIVE_TABLE") == "2":
        L2 = lambda p: json.load(open(os.path.join(OUT2, p))) if os.path.isfile(os.path.join(OUT2, p)) else []
        pairs = [(m, "own", L2(f"pyref_{m}_switch.json"), L2(f"fidelity_{m}_js.json")) for m in ("hgrid2", "sort")]
        if os.path.isfile(os.path.join(OUT2, "fidelity_hgrid2_g12_js.json")):
            pairs.append(("hgrid2 G=12", "own", L2("pyref_hgrid2_g12_switch.json"), L2("fidelity_hgrid2_g12_js.json")))
        return _table(pairs)
    pairs = [("grid", "own", load("pyref.json") + [r for r in load("pyref_grid_switch.json") if r.get("tag") == "switch"],
              load("fidelity_grid_js.json") + [r for r in load("fidelity_grid_switch_js.json") if r.get("tag") == "switch"]),
             ("evo", "own", load("pyref_evo_switch.json"), load("fidelity_evo_js.json"))]
    return _table(pairs)


def _table(pairs):
    out = ["| model | test | Python: loss to wanted plan | n | JS port: loss | n | <=8 Py / JS |", "|---|---|---|---|---|---|---|"]
    st = lambda v: f"{np.mean(v):.1f} +- {np.std(v):.1f}, median {np.median(v):.1f} [{np.min(v):.1f}, {np.max(v):.1f}]" if len(v) else "-"
    for name, _, py, js in pairs:
        for tag in ("own", "switch"):
            for k in sn.KINDS:
                a = [r["cross"][r.get("want", k)] for r in py if r["kind"] == k and r.get("tag", "own") == tag]
                b = [r["cross"][r.get("want", k)] for r in js if r["kind"] == k and r.get("tag", "own") == tag]
                if not a and not b:
                    continue
                lab = k if tag == "own" else f"{k} -> {sn.PLAN_OF[sn.SWITCH_TO[k]]}"
                sh = lambda v: f"{np.mean(np.array(v) <= 8):.2f}" if len(v) else "-"
                out.append(f"| {name} | {lab} | {st(a)} | {len(a)} | {st(b)} | {len(b)} | {sh(a)} / {sh(b)} |")
    return "\n".join(out)


def main():
    cmd = sys.argv[1] if len(sys.argv) > 1 else "targets"
    os.makedirs(OUT, exist_ok=True)
    if cmd == "targets":
        p = os.path.join(OUT, "targets.json")
        json.dump(targets_json(), open(p, "w"), separators=(",", ":"))
        print("wrote", p, os.path.getsize(p) // 1024, "KB")
    elif cmd == "evo":
        p = os.path.join(OUT, "evo_rule.json")
        json.dump(evo_json(), open(p, "w"), separators=(",", ":"))
        print("wrote", p, os.path.getsize(p) // 1024, "KB")
    elif cmd == "sort":
        os.makedirs(OUT2, exist_ok=True)
        p = os.path.join(OUT2, "sort_code.json")
        json.dump(sort_json(), open(p, "w"), separators=(",", ":"))
        print("wrote", p, os.path.getsize(p) // 1024, "KB")
    elif cmd == "table":
        print(table())
    elif cmd == "score":
        res = score(sys.argv[2])
        out = sys.argv[3] if len(sys.argv) > 3 else sys.argv[2].replace(".json", "_scored.json")
        json.dump(res, open(out, "w"), indent=1)
        for r in res:
            print(r["kind"], r["tag"], r["seed"], r["step"], "want", r["want"], r["cross"][r["want"]], r["n"], r["mix"])
    elif cmd == "pyref":
        n = int(sys.argv[2]) if len(sys.argv) > 2 else 6
        kind = sys.argv[3] if len(sys.argv) > 3 else "grid"
        sw_ = len(sys.argv) > 4 and sys.argv[4] == "switch"
        res = pyref(n, kind=kind, switch=sw_)
        out = OUT2 if kind in ("hgrid2", "hgrid2_g12", "sort") else OUT
        os.makedirs(out, exist_ok=True)
        json.dump(res, open(os.path.join(out, f"pyref_{kind}{'_switch' if sw_ else ''}.json"), "w"), indent=1)


if __name__ == "__main__":
    main()
