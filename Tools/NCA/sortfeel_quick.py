"""Quick screen: own-plan loss (1 sample, seed 7) + swarm_feel planar/jerk/osc/stuck for SortFeel variants.
    python sortfeel_quick.py 'well_dead=1.0,wander=0.05' ...
"""
import json, sys, os, time
import torch
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import swarm_nca as sn, swarm_eval as se, swarm_feel as sf, sortfeel_model as fm
BASE = os.path.join(HERE, "results/sort/params.json")

def parse(spec):
    kw = {}
    for p in filter(None, spec.split(",")):
        k, v = p.split("="); kw[k] = float(v)
    return kw

@torch.no_grad()
def quick(kw, seed=7):
    m = fm.from_sort(BASE, **kw)
    T = sn.load_targets(); L = sn.LossCfg(); own = {}
    for k in sn.KINDS:
        m.mem = {}
        gen = sn.make_gen(seed); sw = sn.seed_swarm([T[k]], m.world, gen)
        for _ in range(240): sw = m(sw, gen)
        own[k] = se._score_row(sw, 0, T, L)[k]
    m.mem = {}
    f = sf.feel(m, seed=seed)
    ok, ch = sf.in_band(f)
    return dict(own=own, planar={k: (f[k]["planar_frac"], f[k]["planar_plan"]) for k in sn.KINDS},
                pex=round(sf.planar_excess(f), 3), mean={q: f["mean"][q] for q in ("speed", "jerk_rel", "osc", "stuck", "coherence", "jitter")},
                band=ok, checks=ch)

if __name__ == "__main__":
    torch.set_num_threads(4)
    for spec in sys.argv[1:]:
        t = time.time(); r = quick(parse(spec))
        print(spec, json.dumps(r), f"{time.time()-t:.0f}s", flush=True)
