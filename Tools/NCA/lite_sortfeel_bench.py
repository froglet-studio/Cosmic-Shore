"""lite_sortfeel bench: per-phase profile (ms/step) on grown bodies, and B-swarm scaling.
python lite_sortfeel_bench.py "<query>" [steps] [--batch]  -> prints JSON."""
import json, sys, time, torch
import swarm_eval as se, swarm_nca as sn
torch.set_num_threads(1)

def grown(model, kinds, seed=7, steps=240):
    gen = sn.make_gen(seed)
    sw = sn.seed_swarm([sn.load_targets()[k] for k in kinds], model.world, gen)
    for _ in range(steps):
        sw = model(sw, gen)
    return sw, gen

def phases(q, steps=150):
    m = se.load_model("lite_sortfeel:results/sortfeel/params.json?" + q)
    tot = 0.0; n = 0; live = 0
    for k in sn.KINDS:
        sw, gen = grown(m, [k])
        m.profile = True
        t0 = time.perf_counter()
        for _ in range(steps):
            sw = m(sw, gen)
        tot += time.perf_counter() - t0; n += steps; live += int((sw.active & sw.hatched).sum())
        m.profile = False
    out = dict(query=q, ms_per_step=round(1000 * tot / n, 3), mean_live=live / 4,
               us_per_tadpole_step=round(1e6 * tot / n / (live / 4), 2),
               phases_ms={k: round(1000 * v / n, 3) for k, v in sorted(m.prof.items(), key=lambda x: -x[1])},
               ops_per_step={k: round(v / n, 1) for k, v in m.ops.items()})
    return out

def batch(q, Bs=(1, 4, 16, 64), steps=30):
    m = se.load_model("lite_sortfeel:results/sortfeel/params.json?" + q)
    res = {}
    for B in Bs:
        kinds = [sn.KINDS[i % 4] for i in range(B)]
        sw, gen = grown(m, kinds, steps=120)
        t0 = time.perf_counter()
        for _ in range(steps):
            sw = m(sw, gen)
        dt = time.perf_counter() - t0
        live = int((sw.active & sw.hatched).sum())
        res[B] = dict(ms_per_swarm_step=round(1000 * dt / steps / B, 3), us_per_tadpole_step=round(1e6 * dt / steps / live, 2))
    return res

if __name__ == "__main__":
    q = sys.argv[1]; st = int(sys.argv[2]) if len(sys.argv) > 2 else 150
    o = phases(q, st)
    if "--batch" in sys.argv:
        o["batch"] = batch(q)
    print(json.dumps(o))
