"""Per-term loss breakdown (pos / +elem / +dom / full) of grown swarms, the census and timing.

    python Tools/NCA/sort_diag.py [params.json] [--seed 7] [--steps 240]
"""
import argparse, dataclasses as dc, json, time
import numpy as np, torch
import swarm_nca as sn
import sort_model as sm

VARIANTS = lambda L: {"pos": dc.replace(L, w_h=0, w_tier=0, w_face=0, w_sp=0, w_dom=0, w_elem=0),
                      "+elem": dc.replace(L, w_h=0, w_tier=0, w_face=0, w_sp=0, w_dom=0),
                      "+dom": dc.replace(L, w_h=0, w_tier=0, w_face=0, w_sp=0), "full": L}


@torch.no_grad()
def breakdown(model, seed=7, steps=240, kinds=sn.KINDS, log=print):
    T = sn.load_targets(); L = sn.LossCfg(); out = {}
    for k in kinds:
        gen = sn.make_gen(seed); sw = sn.seed_swarm([T[k]], model.world, gen)
        t0 = time.time()
        for _ in range(steps):
            sw = model(sw, gen)
        x = sn.decode(sw, 0)
        row = {v: round(sn.swarm_loss(x, T[k], LL)[1]["sink"], 2) for v, LL in VARIANTS(L).items()}
        row["n"] = int((sw.active[0] & sw.hatched[0]).sum()); row["sec"] = round(time.time() - t0, 1)
        row["el"] = sn.census(sw, 0, T[k])
        out[k] = row
        log(f"{k:7s} {row}")
    return out


if __name__ == "__main__":
    ap = argparse.ArgumentParser(); ap.add_argument("params", nargs="?", default="")
    ap.add_argument("--seed", type=int, default=7); ap.add_argument("--steps", type=int, default=240)
    ap.add_argument("--set", default="", help="k=v,k=v overrides")
    a = ap.parse_args()
    torch.set_num_threads(4)
    m = sm.load(a.params) if a.params else sm.SortSwarm()
    if a.set:
        d = dc.asdict(m.cfg)
        for kv in a.set.split(","):
            k_, v_ = kv.split("="); d[k_] = type(d[k_])(float(v_)) if not isinstance(d[k_], tuple) else d[k_]
        d["vmax"] = tuple(d["vmax"]); m = sm.SortSwarm(sm.SortCfg(**d))
    breakdown(m, a.seed, a.steps)


@torch.no_grad()
def switch_breakdown(model, k, e, seed=7, steps=240, log=print):
    import swarm_eval as se
    T = sn.load_targets(); L = sn.LossCfg()
    gen = sn.make_gen(seed); sw = sn.seed_swarm([T[k]], model.world, gen)
    for _ in range(steps):
        sw = model(sw, gen)
    pre = sn.census(sw, 0, T[k])
    if not se.cull_to(sw, 0, e, gen):
        log("n/a"); return None
    post = sn.census(sw, 0, T[k])
    for _ in range(steps):
        sw = model(sw, gen)
    to = sn.PLAN_OF[e]; x = sn.decode(sw, 0)
    row = {v: round(sn.swarm_loss(x, T[to], LL)[1]["sink"], 2) for v, LL in VARIANTS(L).items()}
    c = sn.census(sw, 0, T[to])
    log(f"{k}->{to} {row} el {c['elements']} vs {c['target_elements']} dom {c['domains']} vs {T[to].slot_mix} | after cull el {post['elements']} dom {post['domains']}")
    return row
