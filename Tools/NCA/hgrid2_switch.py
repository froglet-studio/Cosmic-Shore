"""Quick switch tester: for one config, the given transitions (plan->target element), seed 7 (and
--seeds), with the per-term breakdown of the final body against the target plan.
    python Tools/NCA/hgrid2_switch.py "cfg k=v,..." "time:1,mass:3" [--seeds=7,108] [--evo]"""
import sys, os, time, torch
import swarm_nca as sn, swarm_eval as se, hgrid2_diag as hd
torch.set_num_threads(int(os.environ.get("NT", "1")))
cfgs, trs = sys.argv[1], sys.argv[2]
seeds = (7,); evo = False
for a in sys.argv[3:]:
    if a.startswith("--seeds="): seeds = tuple(int(x) for x in a.split("=")[1].split(","))
    if a == "--evo": evo = True
kw = dict(kv.split("=") for kv in cfgs.split(",") if kv)
if evo:
    import hgrid2_evo as M
else:
    import hgrid2_model as M
T = sn.load_targets()
t0 = time.time(); out = []
for tr in trs.split(","):
    k, e = tr.split(":"); e = int(e); to = sn.PLAN_OF[e]
    for sd in seeds:
        m = M.make(**kw)
        sw, gen = se._grow(m, [k], T, 240, sd)
        if not se.cull_to(sw, 0, e, gen):
            out.append(f"{k}->{to} n/a"); continue
        for _ in range(240): sw = m(sw, gen)
        x = sn.decode(sw, 0); row = se._score_row(sw, 0, T, sn.LossCfg())
        terms = {v: round(sn.swarm_loss(x, T[to], L)[1]["sink"], 1) for v, L in hd.VARIANTS.items()}
        mk = sw.active[0] & sw.hatched[0]
        out.append(f"{k}->{to} s{sd} {'PASS' if se._passes(row, to, int(mk.sum())) else 'fail'} {terms} n{int(mk.sum())} mix{torch.bincount(sw.elem[0][mk],minlength=4).tolist()} want{T[to].mix} dom{torch.bincount(sw.dom[0][mk],minlength=3).tolist()} wantdom{T[to].slot_mix} dmap{sw.dmap[0].tolist()} best_other {min(v for kk,v in row.items() if kk!=to)}")
print(cfgs, f"{time.time()-t0:.0f}s"); print("\n".join("   " + o for o in out), flush=True)
