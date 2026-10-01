"""Upper-bound experiment for the meta direction: does switching become easy once composition is
CONTROLLED? A designed (not learned) metamorphosis controller on the frozen G2 rule: every step,
tadpoles of the element most OVER the current majority's plan mix start metamorphosing (same
12-step, 8%-cap, slowed process as MetaRule) into the element most UNDER it. The plan is read from
the live majority, never from a label - so it is a legal, local-ish swarm rule (it needs only the
swarm's element census, which the game Cell already tracks).

    python Tools/NCA/meta_oracle.py [--cap 0.08] [--out results/meta_oracle]
"""
import argparse, json, os, sys
import torch
import torch.nn.functional as F
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import swarm_nca as sn
import meta_swarm as ms


class OracleMeta(ms.MetaRule):
    def metamorph(self, s, elem, bw, live, gen):
        W = self.world
        B, N = elem.shape; n = B * N
        live = live.reshape(n)
        prog = s[:, ms.PROG].detach()
        running = live & (prog > 0)
        prog2 = torch.where(running, prog + 1.0 / W.meta_steps, prog)
        done = running & (prog2 >= 1.0 - 1e-6)
        e = elem.reshape(n).clone()
        tg = (s[:, ms.TGT].detach().round().long() - 1).clamp(0, 3)
        e = torch.where(done, tg, e)
        start = torch.zeros(n, dtype=torch.bool); chosen = torch.zeros(n, dtype=torch.long)
        lv = live.view(B, N)
        for b in range(B):
            m = lv[b]; nb = int(m.sum())
            if nb == 0: continue
            eb = e.view(B, N)[b]
            cnt = torch.bincount(eb[m], minlength=4).float()
            # pending conversions count as already done
            pend = (running & ~done).view(B, N)[b]
            cnt = cnt - torch.bincount(eb[pend], minlength=4).float() + torch.bincount(tg.view(B, N)[b][pend], minlength=4).float()
            plan = sn.PLAN_OF[int(cnt.argmax())]
            mix = torch.tensor(self.targets[plan].mix, dtype=torch.float); mix = mix / mix.sum() * nb
            gap = cnt - mix                                   # + over, - under
            busy = int(((prog2 > 0) & ~done).view(B, N)[b].sum())
            room = int(W.meta_cap * nb) - busy
            k = min(room, int(gap.clamp(min=0).max().floor()) if gap.max() >= 1 else 0)
            if k <= 0: continue
            src, dst = int(gap.argmax()), int(gap.argmin())
            ids = (m & (eb == src) & (prog2.view(B, N)[b] <= 0)).nonzero().squeeze(1)
            ids = ids[torch.randperm(len(ids), generator=gen)[:k]]
            start[b * N + ids] = True; chosen[b * N + ids] = dst
        new_prog = torch.where(done, torch.zeros_like(prog2), torch.where(start, torch.full_like(prog2, 1.0 / W.meta_steps), prog2))
        new_tgt = torch.where(start, (chosen + 1).to(s.dtype), torch.where(done, torch.zeros_like(prog), s[:, ms.TGT].detach()))
        lf = live.to(s.dtype)
        s = torch.cat([s[:, :ms.PROG], (new_prog * lf)[:, None], (new_tgt * lf)[:, None], torch.zeros(n, 1), s[:, ms.CARRY + 1:]], 1)
        if self.events is not None and B == 1:
            self.events.append((int(start.sum()), int(done.sum()), int((new_prog > 0).sum())))
        return s, e.view(B, N), bw


def main():
    ap = argparse.ArgumentParser(); ap.add_argument("--cap", type=float, default=0.08); ap.add_argument("--out", default="")
    ap.add_argument("--rule", default=os.path.join(HERE, "results", "swarm_coevo_g2", "rule.pt"))
    a = ap.parse_args(); torch.set_num_threads(int(os.environ.get("NCA_THREADS", 4)))
    w = ms.MetaWorld(learned_lay=1, p_meta=1.0, meta_cap=a.cap)
    st = torch.load(a.rule, weights_only=False)
    r = OracleMeta(w, hidden=st["hidden"])
    if st["rule"]["w1"].shape[1] == r.F: r.load_state_dict(st["rule"])
    else: r.load_g2(st["rule"])
    r.targets = sn.load_targets()
    summary, res = ms.evaluate(r, a.out or None, meta_tag="meta_oracle", note=f"designed conformity metamorph, cap {a.cap}, on {os.path.relpath(a.rule, HERE)}")
    ms.print_eval(summary, res)


if __name__ == "__main__":
    main()
