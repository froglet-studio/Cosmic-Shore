"""Per-term diagnosis (runs/diag_terms.py pattern) for any swarm model: grow each plan 240 steps
and re-score its own plan with the loss terms switched on one at a time:
  pos | +elem | +dom | full (adds the visuals: prism h, Charge tier, facing, spindle).

    python Tools/NCA/hgrid2_diag.py oracle            # round-1 hgrid oracle
    python Tools/NCA/hgrid2_diag.py hgrid2 [k=v ...]  # this round's model (hgrid2_model.make)
"""
import sys, time, dataclasses as dc
import torch
import swarm_nca as sn

BASE = sn.LossCfg()
VARIANTS = {"pos": dc.replace(BASE, w_h=0, w_tier=0, w_face=0, w_sp=0, w_dom=0, w_elem=0),
            "+elem": dc.replace(BASE, w_h=0, w_tier=0, w_face=0, w_sp=0, w_dom=0),
            "+dom": dc.replace(BASE, w_h=0, w_tier=0, w_face=0, w_sp=0),
            "full": BASE}


def grow(model, k, seed=7, steps=240):
    T = sn.load_targets()
    gen = sn.make_gen(seed)
    sw = sn.seed_swarm([T[k]], model.world, gen)
    for _ in range(steps):
        sw = model(sw, gen)
    return sw


def diag(model, seeds=(7,), kinds=sn.KINDS, log=print):
    T = sn.load_targets()
    res = {}
    for k in kinds:
        rows = []
        for sd in seeds:
            sw = grow(model, k, sd)
            x = sn.decode(sw, 0)
            rows.append({v: sn.swarm_loss(x, T[k], L)[1]["sink"] for v, L in VARIANTS.items()} | {"n": int((sw.active[0] & sw.hatched[0]).sum())})
        r = {v: round(sum(q[v] for q in rows) / len(rows), 2) for v in rows[0]}
        res[k] = r
        log(f"{k:7s} " + "  ".join(f"{v}: {r[v]:6.2f}" for v in r), flush=True) if log is print else log(k, r)
    return res


if __name__ == "__main__":
    torch.set_num_threads(4)
    kind = sys.argv[1]
    sets = sys.argv[2:]
    if kind == "oracle":
        import hgrid_boid as hb
        model = hb.make_oracle()
    else:
        import hgrid2_model as hm
        model = hm.make(**{a.split("=")[0]: float(a.split("=")[1]) for a in sets})
    t0 = time.time()
    diag(model)
    print(f"{time.time()-t0:.0f}s")
