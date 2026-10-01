"""Hybrid test: an existing LEARNED rule moves and lays, and only the `field` composition controller is
added on top - plan by element ratios with time hysteresis, and molting surplus elements into the plan's
deficit ones. Question: is the learned rules' switch failure (the element mix drifts after the cull) fixed
by composition control alone?

    python Tools/NCA/field_hybrid.py   # -> Tools/NCA/results/field/hybrid.json
"""
import json
import math
import os
import sys

import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402

ELEM_KIND = {sn.MAJOR[k]: k for k in sn.KINDS}


class Hybrid:
    def __init__(self, rule, dwell=12, molt_rate=0.03, molt_steps=10, mix_of=None):
        self.rule, self.world = rule, rule.world
        self.dwell, self.molt_rate, self.molt_steps = dwell, molt_rate, molt_steps
        self.T = sn.load_targets()
        self.mem = {}

    def eval(self):
        self.rule.eval(); return self

    @torch.no_grad()
    def __call__(self, sw, gen=None, **kw):
        sw = self.rule(sw, gen)
        for b in range(sw.B):
            if int(sw.clock[b]) <= 1 or b not in self.mem:
                al = sw.active[b] & sw.hatched[b]
                c = torch.bincount(sw.elem[b][al], minlength=4)
                k = ELEM_KIND[int(c.argmax())]
                self.mem[b] = dict(plan=k, cand=k, cand_n=0, molt={})
            self._compose(sw, b)
        return sw

    def _compose(self, sw, b):
        m = self.mem[b]
        al = (sw.active[b] & sw.hatched[b]).numpy()
        elem = sw.elem[b].numpy()
        # finish / drop molts
        for i in list(m["molt"]):
            if not al[i]:
                del m["molt"][i]; continue
            to, prog = m["molt"][i]
            prog += 1.0 / self.molt_steps
            if prog >= 1:
                elem[i] = to; del m["molt"][i]
            else:
                m["molt"][i] = (to, prog)
        idx = np.nonzero(al)[0]
        if not len(idx):
            return
        eff = elem.copy()
        for i, (to, _) in m["molt"].items():
            eff[i] = to
        counts = np.bincount(eff[idx], minlength=4)
        cur = sn.MAJOR[m["plan"]]; top = int(np.argmax(counts)); maj = cur if counts[cur] == counts[top] else top
        if maj != cur:
            if m["cand"] == ELEM_KIND[maj]:
                m["cand_n"] += 1
            else:
                m["cand"], m["cand_n"] = ELEM_KIND[maj], 1
            if m["cand_n"] < self.dwell:
                return
            m["plan"], m["molt"] = m["cand"], {}
        else:
            m["cand"], m["cand_n"] = m["plan"], 0
        mix = np.array(self.T[m["plan"]].mix, float)
        goal = mix / mix.sum() * len(idx)
        surplus = counts - goal
        rng = np.random.default_rng(int(sw.clock[b]))
        for _ in range(max(1, int(math.ceil(self.molt_rate * len(idx))))):
            ef, et = int(np.argmax(surplus)), int(np.argmin(surplus))
            if surplus[ef] < 1 or surplus[et] > -1:
                break
            cand = [i for i in idx if eff[i] == ef and i not in m["molt"]]
            if not cand:
                break
            i = int(rng.choice(cand))
            m["molt"][i] = (et, 0.0); eff[i] = et
            surplus[ef] -= 1; surplus[et] += 1


def main():
    torch.set_num_threads(4)
    out = {}
    for tag in ("g2", "f1", "e8"):
        path = os.path.join(HERE, "results", f"swarm_coevo_{tag}", "rule.pt")
        si = int(json.load(open(os.path.join(HERE, "results", f"swarm_coevo_{tag}", "summary.json"))).get("scale_inv", 0))
        L = sn.LossCfg(scale_inv=si)
        row = {}
        for name, model in (("learned", sn.load_rule(path)), ("learned+composition", Hybrid(sn.load_rule(path)))):
            model.eval()
            _, s = sn.rollout(model, 240, L=L)
            p, close = sn.tests_passed(s)
            print(f"== {tag} {name}: {p}/8")
            sn.print_cross(s); sn.print_switch(s)
            row[name] = dict(passed=p, close=round(close, 2),
                             own={k: s["cross"][k][k] for k in sn.KINDS},
                             switch={k: dict(to=s["switch"][k]["to"], best=min(s["switch"][k]["cross"], key=s["switch"][k]["cross"].get),
                                             n=s["switch"][k]["census"]["n"], el=s["switch"][k]["census"]["elements"]) for k in sn.KINDS})
        out[tag] = row
    json.dump(out, open(os.path.join(HERE, "results", "field", "hybrid.json"), "w"), indent=1)
    for tag, row in out.items():
        print(tag, {n: r["passed"] for n, r in row.items()})


if __name__ == "__main__":
    main()
