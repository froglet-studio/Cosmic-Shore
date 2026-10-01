"""Side-by-side ms/step, same process, 1 thread, alternating: sort vs sortfeel on grown bodies."""
import json, time, torch, swarm_nca as sn, sort_model as sm, sortfeel_model as fm
torch.set_num_threads(1)
T = sn.load_targets(); out = {}
for k in sn.KINDS:
    row = {}
    for name, mk in (("sort", lambda: sm.load("results/sort/params.json")), ("sortfeel", lambda: fm.load("results/sortfeel/params.json"))):
        m = mk(); gen = sn.make_gen(7); sw = sn.seed_swarm([T[k]], m.world, gen)
        for _ in range(240): sw = m(sw, gen)
        t = time.perf_counter()
        for _ in range(40): sw = m(sw, gen)
        row[name] = round(1000 * (time.perf_counter() - t) / 40, 2)
    out[k] = row; print(k, row, flush=True)
out["mean"] = {n: round(sum(out[k][n] for k in sn.KINDS) / 4, 2) for n in ("sort", "sortfeel")}
print(out["mean"]); json.dump(out, open("results/sortfeel/timing.json", "w"), indent=1)
