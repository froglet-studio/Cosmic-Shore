"""distill: long-horizon body-frame test. Grow one plan for N steps (no cull) and print, every 80 steps,
headcount, element mix, RMS radius, the centre estimate's mean error and spread, and the own-plan divergence.
A body frame is good enough when these match the oracle_z=1 run (true centroid; NOT local, diagnostic only).

    python Tools/NCA/distill_longrun.py runs/distill/gdag/student_07_zm0.pt space oracle_z=1
"""
import os, sys
import torch
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import swarm_nca as sn, distill_train as dtr, distill_student as ds

torch.set_num_threads(int(os.environ.get("DISTILL_THREADS", "1")))
st = dtr.load(sys.argv[1]); T = sn.load_targets()
kinds = [a for a in sys.argv[2:] if a in sn.KINDS] or ["space", "mass"]
for kv in [a for a in sys.argv[2:] if "=" in a]:
    k, v = kv.split("="); setattr(st.cfg, k, type(getattr(st.cfg, k))(v))
steps = 480
for k in kinds:
    g = sn.make_gen(7); sw = sn.seed_swarm([T[k]], st.world, g)
    for i in range(steps + 1):
        if i % 80 == 0:
            m = sw.active[0] & sw.hatched[0]; p = sw.pos[0][m]; c = p.mean(0); z = sw.s[0][m][:, ds.Z]
            print(k, i, int(m.sum()), torch.bincount(sw.elem[0][m], minlength=4).tolist(),
                  "rms %.1f zerr %.1f zspread %.1f" % (((p - c) ** 2).sum(-1).mean().sqrt(), (z - c).norm(dim=-1).mean(), (z - z.mean(0)).norm(dim=-1).mean()),
                  "own", round(sn.swarm_loss(sn.decode(sw, 0), T[k], sn.LossCfg())[1]["sink"], 1), flush=True)
        sw = st(sw, g)
