"""Cost curve, measured in ONE quiet process (1 thread): ms/step of a grown swarm per plan, for hgrid2 (no cache)
and the combo at grid sizes 16 / 12 / 8 with the exact plan-field cache."""
import json, time, sys, torch, swarm_nca as sn, combo_eval
torch.set_num_threads(1)
C = ["lay_cap=1.0", "ratio=2", "orphan_proxy=1", "transfer2=1"]
VARS = {"hgrid2 (starve, no cache)": ["molt=0", "cache=0"], "combo G16 no cache": C + ["cache=0"],
        "combo G16": C, "combo G12": C + ["G=12", "cell=8"], "combo G8": C + ["G=8", "cell=12"]}
T = sn.load_targets(); out = {}
for name, sets in VARS.items():
    row = {}
    for k in sn.KINDS:
        m, _ = combo_eval.build(sets)
        gen = sn.make_gen(7); sw = sn.seed_swarm([T[k]], m.world, gen)
        for _ in range(240): sw = m(sw, gen)
        t = time.perf_counter()
        for _ in range(64): sw = m(sw, gen)
        row[k] = round((time.perf_counter() - t) / 64 * 1000, 2)
    row["mean"] = round(sum(row[k] for k in sn.KINDS) / 4, 2)
    out[name] = row; print(name, row, flush=True)
json.dump(out, open(sys.argv[1], "w"), indent=1)
