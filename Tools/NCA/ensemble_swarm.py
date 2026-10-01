"""Specialist ensemble: four single-plan learned rules (results/solo_<plan>/rule_latest.pt, each G2
fine-tuned on ONE body plan), switched per swarm by its living majority element with a dwell.

A process family of its own: every specialist is accurate on its own plan and an attractor on the
others (it grows its own creature from foreign seeds), so instead of averaging them into one rule
(task-vector hybrids, swarm_hybrid.py) the swarm runs whichever specialist its composition names.
All four share G2's architecture and state channels, so a swap mid-life keeps every tadpole's state.

    python Tools/NCA/swarm_eval.py --model ensemble:Tools/NCA/results   (spec = folder holding solo_*)
"""
import os
import torch

import swarm_nca as sn


class EnsembleRule:
    def __init__(self, rules, dwell=12):
        self.rules = rules                       # plan name -> SwarmRule
        self.world = next(iter(rules.values())).world
        self.dwell = dwell
        self.cur = None; self.cand = None; self.cnt = None

    def _majority(self, sw):
        out = []
        for b in range(sw.pos.shape[0]):
            out.append(sn.majority_plan(sw, b, None))
        return out

    def __call__(self, sw, gen):
        B = sw.pos.shape[0]
        if self.cur is None or len(self.cur) != B or int(sw.clock.max()) == 0:
            maj = self._majority(sw)
            self.cur = [m or sn.KINDS[0] for m in maj]; self.cand = list(self.cur); self.cnt = [0] * B
        maj = self._majority(sw)
        for b in range(B):
            m = maj[b] or self.cur[b]
            if m == self.cur[b]:
                self.cand[b], self.cnt[b] = m, 0
            elif m == self.cand[b]:
                self.cnt[b] += 1
                if self.cnt[b] >= self.dwell:
                    self.cur[b], self.cnt[b] = m, 0
            else:
                self.cand[b], self.cnt[b] = m, 1
        parts = []
        for b in range(B):
            sub = sn.Swarm.cat([sw.index(torch.tensor([b]))])
            parts.append(self.rules[self.cur[b]](sub, gen))
        return sn.Swarm.cat(parts)


def load(folder, dwell=12):
    return EnsembleRule({k: sn.load_rule(os.path.join(folder, f"solo_{k}", "rule_latest.pt")) for k in sn.KINDS}, dwell)
