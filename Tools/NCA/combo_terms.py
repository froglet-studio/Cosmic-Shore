"""Per-term breakdown of a combo candidate's own-plan loss (the round's diagnosis, runs/diag_terms.py, reproduced)."""
import sys, json, dataclasses as dc, torch, swarm_nca as sn, combo_eval
torch.set_num_threads(1)
T = sn.load_targets(); base = sn.LossCfg()
V = {"pos only": dc.replace(base, w_h=0, w_tier=0, w_face=0, w_sp=0, w_dom=0, w_elem=0),
     "+ element": dc.replace(base, w_h=0, w_tier=0, w_face=0, w_sp=0, w_dom=0),
     "+ domain": dc.replace(base, w_h=0, w_tier=0, w_face=0, w_sp=0), "full": base}
out = {}
for k in sn.KINDS:
    m, _ = combo_eval.build(sys.argv[2:])
    gen = sn.make_gen(7); sw = sn.seed_swarm([T[k]], m.world, gen)
    for _ in range(240): sw = m(sw, gen)
    x = sn.decode(sw, 0)
    out[k] = {n: round(sn.swarm_loss(x, T[k], L)[1]["sink"], 2) for n, L in V.items()}
    out[k]["n"] = int((sw.active[0] & sw.hatched[0]).sum())
    print(k, out[k], flush=True)
json.dump(out, open(sys.argv[1], "w"), indent=1)
