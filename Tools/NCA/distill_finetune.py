"""distill stage 3: fine-tune the distilled Student on swarm_nca.swarm_loss (short-horizon BPTT), keeping
the behaviour-cloning loss as an anchor.

Start states come from the STUDENT's own rollouts (grow 240, fair cull to a random element, 240 more),
snapshotted at random times, each with a STICKY label: the seeded plan before the cull, the new
majority's plan after it. From a snapshot the student runs K steps with gradients through its velocity
and look heads (laying / molting stay sampled, i.e. non-differentiable events), and the end state is
scored by swarm_loss against the label, plus the overflow penalty (w_over) and the body floor
(min_body) from round 1, plus W_BC x the BC loss on a batch of teacher-labelled rows.

    python Tools/NCA/distill_finetune.py --init runs/distill/dag/student.pt --run runs/distill/ft
"""
from __future__ import annotations

import argparse
import json
import os
import sys
import time
from dataclasses import replace

import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402
import swarm_eval as se  # noqa: E402
import distill_student as ds  # noqa: E402
import distill_train as dtr  # noqa: E402


@torch.no_grad()
def snapshots(st, seed, B=8, grow=240, after=240, per=3):
    """Student rollouts; returns a list of (Swarm with batch 1, wanted plan kind)."""
    rng = np.random.default_rng(seed)
    gen = sn.make_gen(seed)
    T = sn.load_targets()
    kinds = [sn.KINDS[b % 4] for b in range(B)]
    culls = [None if rng.random() < 0.2 else int(rng.integers(0, 4)) for _ in range(B)]
    sw = sn.seed_swarm([T[k] for k in kinds], st.world, gen)
    want = list(kinds)
    times = sorted(set(int(x) for x in rng.integers(40, grow + after - 30, size=per * 2)))
    out = []
    for t in range(grow + after):
        if t == grow:
            for b in range(B):
                if culls[b] is not None and se.cull_to(sw, b, culls[b], gen):
                    want[b] = sn.PLAN_OF[culls[b]]
        if t in times:
            for b in range(B):
                if int((sw.active[b] & sw.hatched[b]).sum()) >= 8:
                    out.append((sw.index(torch.tensor([b])), want[b]))
        sw = st(sw, gen)
    return out


def main():
    torch.set_num_threads(4)
    ap = argparse.ArgumentParser()
    ap.add_argument("--init", required=True)
    ap.add_argument("--run", default=os.path.join(HERE, "runs", "distill", "ft"))
    ap.add_argument("--data", default="")
    ap.add_argument("--iters", type=int, default=300)
    ap.add_argument("--K", type=int, default=24)
    ap.add_argument("--B", type=int, default=6)
    ap.add_argument("--lr", type=float, default=2e-4)
    ap.add_argument("--w_bc", type=float, default=1.0)
    ap.add_argument("--eval_every", type=int, default=100)
    a = ap.parse_args()
    os.makedirs(a.run, exist_ok=True)
    logf = open(os.path.join(a.run, "log.txt"), "a")

    def log(*x):
        s = " ".join(str(v) for v in x)
        print(s, flush=True); logf.write(s + "\n"); logf.flush()

    st = dtr.load(a.init)
    st.train()
    data = torch.load(a.data or os.path.join(os.path.dirname(a.init), "data.pt"))
    L = sn.LossCfg(w_over=0.05, over_band=12.0, min_body=32, w_body=20.0)
    T = sn.load_targets()
    opt = torch.optim.Adam(st.parameters(), lr=a.lr)
    pool = []
    hist = []
    t0 = time.time()
    for it in range(a.iters):
        if not pool:
            st.eval()
            pool = snapshots(st, seed=1000 + it)
            np.random.default_rng(it).shuffle(pool)
            st.train()
        batch = [pool.pop() for _ in range(min(a.B, len(pool)))]
        sw = sn.Swarm.cat([b for b, _ in batch])
        gen = sn.make_gen(it)
        for _ in range(a.K):
            sw = st.step(sw, gen)
        lsum, infos = 0.0, []
        for b, (_, k) in enumerate(batch):
            x = sn.decode(sw, b)
            l, info = sn.swarm_loss(x, T[k], L)
            lsum = lsum + l / len(batch); infos.append(info["sink"])
        sel = torch.randint(len(data["f"]), (4096,))
        bc, parts = dtr.losses(st, {kk: v[sel] for kk, v in data.items()}, dtr.W_LOSS)
        loss = lsum + a.w_bc * bc
        opt.zero_grad(); loss.backward()
        torch.nn.utils.clip_grad_norm_(st.parameters(), 1.0)
        opt.step()
        if it % 10 == 0:
            log(f"it {it}: sink {np.mean(infos):.2f} bc {float(bc):.4f} ({time.time() - t0:.0f}s)")
        if (it + 1) % a.eval_every == 0:
            st.eval()
            dtr.save(st, os.path.join(a.run, "student.pt"))
            res = dtr.quick_eval(st, log)
            hist.append(dict(it=it + 1, passed=res["passed"], feasible=res["feasible"], res=res))
            json.dump(hist, open(os.path.join(a.run, "hist.json"), "w"), indent=1)
            dtr.save(st, os.path.join(a.run, f"student_{it + 1:04d}.pt"))
            st.train()


if __name__ == "__main__":
    main()
