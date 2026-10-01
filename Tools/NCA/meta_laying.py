"""Control for the meta direction: can SELECTIVE LAYING ALONE (no metamorphosis - element stays fixed
at birth) steer a swarm's composition? Designed homeostat on egg element: every egg laid this step
takes the element most UNDER the live majority's plan mix (greedy, counts updated per egg). Domain
still breeds true. Same frozen rule, same yardstick.

    NCA_THREADS=1 python Tools/NCA/meta_laying.py [--rule results/swarm_coevo_g2/rule.pt]
"""
import argparse, os, sys
import torch
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import swarm_nca as sn
import meta_swarm as ms


class LayHomeostat(ms.MetaRule):
    def lay(self, sw, gi, gj, gen=None):
        before = sw.active.clone()
        super().lay(sw, gi, gj, gen)
        new = sw.active & ~before
        for b in range(sw.B):
            ids = new[b].nonzero().squeeze(1)
            if len(ids) == 0:
                continue
            m = sw.active[b] & sw.hatched[b]
            cnt = torch.bincount(sw.elem[b][m], minlength=4).float()
            plan = sn.PLAN_OF[int(cnt.argmax())]
            mix = torch.tensor(self.targets[plan].mix, dtype=torch.float)
            for i in ids.tolist():
                want = mix / mix.sum() * (cnt.sum() + 1)
                e = int((cnt - want).argmin())
                sw.elem[b, i] = e; cnt[e] += 1


def main():
    ap = argparse.ArgumentParser(); ap.add_argument("--rule", default=os.path.join(HERE, "results", "swarm_coevo_g2", "rule.pt"))
    a = ap.parse_args()
    st = torch.load(a.rule, weights_only=False)
    w = ms.MetaWorld(learned_lay=1, p_meta=0.0)
    r = LayHomeostat(w, hidden=st["hidden"])
    if st["rule"]["w1"].shape[1] == r.F: r.load_state_dict(st["rule"])
    else: r.load_g2(st["rule"])
    r.targets = sn.load_targets()
    summary, res = ms.evaluate(r)
    ms.print_eval(summary, res)


if __name__ == "__main__":
    main()
