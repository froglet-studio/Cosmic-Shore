"""distill: the teacher as a LABELER. The `field` model (field_swarm.FieldSwarm, unchanged) is run on a
SHADOW of whatever swarm is being simulated - the student's own swarm during DAgger - and its decisions
are read off as per-tadpole action labels:

  v (its new velocity), look (its new facing/prism/tier/spindle channels), startle, the swarm's committed
  plan, the lay events (which parent laid, in which direction, which element) and molt starts (who, into what).

The shadow keeps the teacher's private per-tadpole memory (slot HOME, startle, birth age) across steps
while positions / elements / molts are re-synced from the simulated swarm each step, so the teacher
reacts to the STUDENT's state (DAgger) rather than to its own trajectory.
"""
from __future__ import annotations

import numpy as np
import torch

import swarm_nca as sn
import field_swarm as fs
import distill_student as ds

C = sn.C


class RecordingField(fs.FieldSwarm):
    """FieldSwarm that records lay events and molt starts made inside _step."""

    def __init__(self, cfg=None, world=None):
        super().__init__(cfg, world)
        self.lays = []      # (b, parent, dir[3], elem)
        self.molts = []     # (b, j, e_to)

    def _lay(self, sw, b, plan, perm, gen):
        before = sw.active[b].clone()
        pos0 = sw.pos[b].clone(); S0 = sw.s[b].clone()
        super()._lay(sw, b, plan, perm, gen)
        new = (sw.active[b] & ~before).nonzero().squeeze(1).tolist()
        if not new:
            return
        cand = (before & sw.hatched[b]).nonzero().squeeze(1)
        for j in new:
            same = cand[(sw.elem[b, cand] == sw.elem[b, j]) & (sw.dom[b, cand] == sw.dom[b, j])]
            if len(same) == 0:
                continue
            dpos = (sw.pos[b, j] - pos0[same]).norm(dim=-1)
            dlook = (S0[same][:, 1:12] - sw.s[b, j, 1:12]).abs().sum(-1)
            p = int(same[torch.argmin((dpos - self.world.r_bud).abs() * 10 + dlook)])
            d = sw.pos[b, j] - pos0[p]
            self.lays.append((b, p, (d / d.norm().clamp(min=1e-6)).numpy(), int(sw.elem[b, j])))

    def _molt(self, sw, b, idx, plan, counts, gen):
        before = sw.s[b][:, fs.MOLT].clone()
        super()._molt(sw, b, idx, plan, counts, gen)
        st = ((before <= 0) & (sw.s[b][:, fs.MOLT] > 0)).nonzero().squeeze(1).tolist()
        for j in st:
            self.molts.append((b, j, int(sw.s[b, j, fs.MOLT_TO])))


def plan_logits(plan_elem: int):
    return 6.0 * (torch.nn.functional.one_hot(torch.tensor(plan_elem), 4).float() - 0.25)


class Labeler:
    def __init__(self, cfg: fs.FieldCfg | None = None, world=None):
        self.T = RecordingField(cfg or fs.FieldCfg(assign="greedy"), world)
        self.tsw = None
        self.prev_live = None
        self.gen = sn.make_gen(12345)

    def reset(self):
        self.tsw = None; self.prev_live = None; self.T.mem = {}

    @torch.no_grad()
    def label(self, sw: sn.Swarm, predators=()):
        """predators: a list (every sample sees it) or {b: list}."""
        """Teacher's action for every live tadpole of sw (in the student's channel layout)."""
        B, N, _ = sw.pos.shape
        n = B * N
        live = sw.active & sw.hatched
        if self.tsw is None or self.tsw.B != B:
            self.tsw = sw.clone(); self.tsw.s = torch.zeros(B, N, C); self.prev_live = torch.zeros_like(live)
        t = self.tsw
        new = live & ~self.prev_live
        S = t.s
        S[new] = 0.0
        S[~live] = 0.0
        S[:, :, fs.VEL] = sw.s[:, :, ds.VEL]
        S[:, :, fs.MOLT] = sw.s[:, :, ds.MOLT]
        S[:, :, fs.MOLT_TO] = sw.s[:, :, ds.MOLT_TO]
        S[:, :, 1:12] = sw.s[:, :, 1:12]
        S[:, :, sn.A] = live.float(); S[:, :, sn.DIE] = -12.0
        t.pos = sw.pos.clone(); t.elem = sw.elem.clone(); t.dom = sw.dom.clone()
        t.active = live.clone(); t.hatched = live.clone(); t.clock = sw.clock.clone(); t.since = sw.since.clone()
        t2 = t.clone()
        self.T.lays, self.T.molts = [], []
        for b in range(B):
            self.T.predators = list(predators.get(b, [])) if isinstance(predators, dict) else list(predators)
            if int(t2.clock[b]) == 0 or b not in self.T.mem:
                self.T._reset(b, t2)
            self.T._step(t2, b, self.gen)
        # persist the teacher's private memory for the tadpoles that were live
        for ch in (fs.HOME, fs.STARTLE, fs.BIRTH):
            S[:, :, ch] = torch.where(live, t2.s[:, :, ch], torch.zeros_like(S[:, :, ch]))
        self.prev_live = live.clone()
        act = dict(
            v=t2.s[:, :, fs.VEL].reshape(n, 3).clone(),
            look=t2.s[:, :, 1:12].reshape(n, 11).clone(),
            stl=t2.s[:, :, fs.STARTLE].reshape(n).clone(),
            lay_par=torch.zeros(n, dtype=torch.bool), lay_dir=torch.zeros(n, 3), lay_el=sw.elem.reshape(n).clone(),
            molt_start=torch.zeros(n, dtype=torch.bool), molt_to=torch.zeros(n, dtype=torch.long),
            plan=torch.tensor([sn.MAJOR[self.T.mem[b]["plan"]] for b in range(B)]),
        )
        act["pb"] = torch.stack([plan_logits(int(p)) for p in act["plan"]])[:, None, :].expand(B, N, 4).reshape(n, 4).clone()
        for (b, p, d, e) in self.T.lays:
            i = b * N + p
            if act["lay_par"][i]:
                continue                           # one egg per parent per step in the student's mechanics
            act["lay_par"][i] = True; act["lay_dir"][i] = torch.as_tensor(d); act["lay_el"][i] = e
        for (b, j, e) in self.T.molts:
            act["molt_start"][b * N + j] = True; act["molt_to"][b * N + j] = e
        act["live"] = live.reshape(n)
        return act
