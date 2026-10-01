"""Snapshot of grown bodies vs their plans (three projections each), coloured by element (C M S T),
marker by domain. python Tools/NCA/hgrid2_render.py --out results/hgrid2/bodies.png"""
import argparse, torch, matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import swarm_nca as sn, hgrid2_eval as h2
COL = ["#f2c14e", "#4e79a7", "#9c6ade", "#59a14f"]   # Charge Mass Space Time
MK = ["o", "^", "s"]
ap = argparse.ArgumentParser(); ap.add_argument("--out", required=True); ap.add_argument("--evo", action="store_true")
a = ap.parse_args(); torch.set_num_threads(4)
kw = dict(h2.BEST)
if a.evo:
    import hgrid2_evo as M; kw.update(h2.EVO)
else:
    import hgrid2_model as M
T = sn.load_targets()
fig, ax = plt.subplots(4, 4, figsize=(16, 16))
for r, k in enumerate(sn.KINDS):
    m = M.make(**kw); gen = sn.make_gen(7); sw = sn.seed_swarm([T[k]], m.world, gen)
    for _ in range(240): sw = m(sw, gen)
    x = sn.decode(sw, 0); L = sn.swarm_loss(x, T[k], sn.LossCfg())[1]
    p = (x["p"] - x["p"].mean(0)).numpy(); e = x["elem"].numpy(); d = x["dom"].numpy()
    fr = T[k].frames[L["frame"]]; tp = (fr["p"] - fr["p"].mean(0)).numpy(); te = fr["elem"].numpy(); ts = fr["slot"].numpy()
    for c, (i, j, nm) in enumerate([(0, 1, "x-y"), (0, 2, "x-z")]):
        for pts, el, dm, col in [(tp, te, ts, 2 * c), (p, e, d, 2 * c + 1)]:
            A = ax[r, col]
            for dd in range(3):
                q = dm == dd
                A.scatter(pts[q, i], pts[q, j], c=[COL[v] for v in el[q]], marker=MK[dd], s=22, edgecolors="k", linewidths=0.2)
            A.set_aspect("equal"); A.set_title(f"{k} {'plan' if col % 2 == 0 else 'grown (loss %.2f)' % L['sink']} {nm}", fontsize=10)
fig.suptitle("hgrid2" + (" evo body + grid" if a.evo else "") + " - colour = element (Charge yellow, Mass blue, Space purple, Time green), marker = domain", fontsize=12)
fig.tight_layout(); fig.savefig(a.out, dpi=70)
print("saved", a.out)
