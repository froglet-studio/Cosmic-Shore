"""HGRID vote: a small learned 3D NCA that holds the PLAN DECISION, cell by cell; the shapes it chooses
between are designed templates (the four plans' rasterised targets, hgrid_core.PlanFields).

Per cell the NCA keeps 4 plan logits + 8 hidden channels. It hears the splatted swarm (per-element
density, total) and the swarm-level element mix + headcount. The field the boids follow is the
mixture  sum_k softmax(logits)_k(x) * template_k(x),  so a decision can be partial and spatial: when a
swarm is eaten, the new plan can sweep across the body as a wave instead of the whole creature
flipping in one frame. Hysteresis is the NCA's own business (its state persists between steps).

Supervision: per occupied cell, cross-entropy toward the STICKY intended plan - the seed's plan, changed
only when the swarm is eaten (the new majority's plan at that moment) - on the states the NCA-driven
boids visit (DAgger). A first version labelled with the CURRENT majority and collapsed: a wrong field
breeds the wrong element, the majority follows, and the label agrees with the mistake.

    python Tools/NCA/hgrid_vote.py train --set run=runs/hgrid_vote --set hours=1.5
    python Tools/NCA/hgrid_vote.py eval --ckpt runs/hgrid_vote/latest.pt [--probe] [--out ...]
"""
from __future__ import annotations

import argparse
import json
import os
import time
from dataclasses import dataclass, asdict

import torch
import torch.nn as nn
import torch.nn.functional as F

import swarm_nca as sn
import hgrid_core as hc
import hgrid_boid as hb
from hgrid_nca import _kernels, shift_grid

HERE = os.path.dirname(os.path.abspath(__file__))
VH = 8
VCH = 4 + VH
VIN = 10


class VoteNCA(nn.Module):
    def __init__(self, hidden=48, fire=0.75, leak=0.02):
        super().__init__()
        self.fire, self.leak = fire, leak
        k = _kernels()
        self.register_buffer("kern", k[:, None].repeat(VCH, 1, 1, 1, 1).view(5 * VCH, 1, 3, 3, 3))
        self.c1 = nn.Conv3d(5 * VCH + VIN, hidden, 1)
        self.c2 = nn.Conv3d(hidden, VCH, 1)
        nn.init.zeros_(self.c2.weight); nn.init.zeros_(self.c2.bias)

    def forward(self, h, inp, gen=None):
        p = F.conv3d(F.pad(h, (1, 1, 1, 1, 1, 1)), self.kern, groups=VCH)
        dh = self.c2(F.relu(self.c1(torch.cat([p, inp], 1))))
        if self.fire < 1:
            dh = dh * (torch.rand(h.shape[0], 1, *h.shape[2:], generator=gen) < self.fire).to(h.dtype)
        h = h + dh - self.leak * h
        return 8 * torch.tanh(h / 8)


class VoteField:
    def __init__(self, nca: VoteNCA, cfg: hb.BoidCfg, targets):
        self.nca, self.cfg, self.targets = nca, cfg, targets
        self.pf = hc.PlanFields(targets, cfg.G, cfg.cell, period=cfg.period, flow=bool(cfg.flow))
        self.beta = 1.0
        self.collect = None
        self.gen = None
        self.last_w = None

    def templates(self, sw, centres):
        B = sw.B
        outs = []
        for k in sn.KINDS:
            fr = [int(c // self.cfg.period) % len(self.targets[k].frames) if self.cfg.animate else 0 for c in sw.clock]
            outs.append(self.pf.field([k] * B, fr, centres))
        return torch.stack(outs, 1)                                   # [B,4,K,G,G,G]

    def __call__(self, sw, centres, live):
        cfg, B, G = self.cfg, sw.B, self.cfg.G
        if sw.grid is None or sw.grid.shape[0] != B or sw.grid.shape[1] != VCH:
            sw.grid = torch.zeros(B, VCH, G, G, G); sw.gcen = centres.clone()
        elif bool((sw.clock == 0).any()):
            sw.grid = sw.grid.clone(); sw.grid[sw.clock == 0] = 0
        if sw.gcen is None or sw.gcen.shape[0] != B:
            sw.gcen = centres.clone()
        dcell = torch.round((centres - sw.gcen) / cfg.cell).long()
        if bool((dcell != 0).any()):
            sw.grid = torch.stack([shift_grid(sw.grid[b], dcell[b]) if bool((dcell[b] != 0).any()) else sw.grid[b]
                                   for b in range(B)])
            sw.gcen = centres.clone()
        frame = hc.GridFrame(centres, G, cfg.cell)
        oh = F.one_hot(sw.elem, 4).float()
        dens = hc.blur(hc.splat(frame, sw.pos.detach(), oh, live), 1)
        n = live.float().sum(1).clamp(min=1)
        mix = (oh * live[..., None].float()).sum(1) / n[:, None]
        glob = torch.cat([mix, (n / 100)[:, None]], 1)[:, :, None, None, None].expand(-1, -1, G, G, G)
        inp = torch.cat([dens, dens.sum(1, keepdim=True), glob], 1)
        h = self.nca(sw.grid, inp, self.gen)
        sw.grid = h
        w = torch.softmax(h[:, :4], 1)                                # [B,4,G,G,G]
        self.last_w = w
        T = self.templates(sw, centres)
        field = (w[:, :, None] * T).sum(1)
        if self.collect is not None:
            occ = (T[:, :, :hc.NCLS].sum(2).amax(1) + dens.sum(1)).detach() > 0.05      # cells that matter
            self.collect.append((h[:, :4], sw.plan.clone(), occ))
        if self.collect is not None:
            field = field.detach()        # train the decision through its CE only; boids carry no graph
        if self.beta >= 1.0:
            return field
        lab = T[torch.arange(B), sw.plan]
        use = (torch.rand(B, generator=self.gen) < self.beta).float()[:, None, None, None, None]
        return use * field.detach() + (1 - use) * lab


@dataclass
class VoteCfg:
    run: str = os.path.join(HERE, "runs", "hgrid_vote")
    hours: float = 1.5
    batch: int = 4
    window: int = 16
    grow_min: int = 100
    grow_max: int = 240
    after: int = 200
    lr: float = 1e-3
    hidden: int = 48
    beta0: float = 0.2
    beta1: float = 1.0
    ramp_eps: int = 30
    seed: int = 0


def load_model(path, cfg=None):
    st = torch.load(path, weights_only=False)
    cfg = cfg or hb.BoidCfg(**st.get("boid", {}))
    nca = VoteNCA(hidden=st.get("hidden", 48)); nca.load_state_dict(st["nca"]); nca.eval()
    targets = sn.load_targets()
    vf = VoteField(nca, cfg, targets)
    m = hb.FieldBoid(sn.World(), cfg, vf, targets)
    m.nca = nca
    return m


def train(vc: VoteCfg, bc: hb.BoidCfg):
    import swarm_probe
    os.makedirs(vc.run, exist_ok=True)
    torch.manual_seed(vc.seed)
    gen = sn.make_gen(vc.seed)
    targets = sn.load_targets()
    nca = VoteNCA(hidden=vc.hidden)
    opt = torch.optim.Adam(nca.parameters(), lr=vc.lr)
    ck = os.path.join(vc.run, "latest.pt")
    ep = 0
    if os.path.exists(ck):
        st = torch.load(ck, weights_only=False)
        nca.load_state_dict(st["nca"]); opt.load_state_dict(st["opt"]); ep = st["ep"]
    vf = VoteField(nca, bc, targets); vf.gen = gen
    boid = hb.FieldBoid(sn.World(), bc, vf, targets)
    json.dump(dict(vote=asdict(vc), boid=asdict(bc)), open(os.path.join(vc.run, "config.json"), "w"), indent=1)
    log = open(os.path.join(vc.run, "log.jsonl"), "a")
    t_end = time.time() + vc.hours * 3600
    while time.time() < t_end:
        t0 = time.time()
        vf.beta = vc.beta0 + (vc.beta1 - vc.beta0) * min(1.0, ep / max(1, vc.ramp_eps))
        kinds = [sn.KINDS[i % 4] for i in range(vc.batch)]
        sw = sn.seed_swarm([targets[k] for k in kinds], boid.world, gen)
        T1 = int(torch.randint(vc.grow_min, vc.grow_max + 1, (1,), generator=gen))
        total = T1 + vc.after
        vf.collect = []
        ce_l, acc_l = [], []
        for t in range(total):
            if t == T1:
                for b in range(sw.B):
                    u = float(torch.rand((), generator=gen))
                    if u < 0.85:
                        r = sn.lose_majority(sw, b, gen, mode="excess")
                        if r is not None:          # the label: the plan the swarm SHOULD take now (sticky)
                            sw.plan[b] = sn.KINDS.index(r)
                    elif u < 0.93:
                        one = sw.index(torch.tensor([b])); swarm_probe.strike(one, gen=gen)
                        for a in ("active", "hatched", "s"):
                            getattr(sw, a)[b] = getattr(one, a)[0]
            sw = boid.step(sw, gen, train=True)
            if len(vf.collect) >= vc.window or t == total - 1:
                loss = 0
                for logit, gp, occ in vf.collect:
                    lab = gp.clamp(min=0)[:, None, None, None].expand(-1, *occ.shape[1:])
                    ce = F.cross_entropy(logit, lab, reduction="none")
                    m = occ.float()
                    loss = loss + (ce * m).sum() / m.sum().clamp(min=1)
                    acc = ((logit.argmax(1) == lab).float() * m).sum() / m.sum().clamp(min=1)
                    ce_l.append(float((ce * m).sum() / m.sum().clamp(min=1))); acc_l.append(float(acc))
                loss = loss / len(vf.collect)
                opt.zero_grad(); loss.backward()
                for p in nca.parameters():
                    if p.grad is not None:
                        p.grad /= (p.grad.norm() + 1e-8)
                opt.step()
                vf.collect = []
                sw.grid = sw.grid.detach()
        vf.collect = None
        ep += 1
        rec = dict(ep=ep, beta=round(vf.beta, 2), T1=T1, ce=round(sum(ce_l) / len(ce_l), 4),
                   acc=round(sum(acc_l) / len(acc_l), 4), acc_end=round(sum(acc_l[-40:]) / len(acc_l[-40:]), 4),
                   sec=round(time.time() - t0, 1), n=[int((sw.active[b] & sw.hatched[b]).sum()) for b in range(sw.B)],
                   plan=[sn.KINDS[int(g)][:2] for g in sw.plan], maj=[sn.KINDS[int(g)][:2] for g in sw.gplan])
        log.write(json.dumps(rec) + "\n"); log.flush(); print(json.dumps(rec), flush=True)
        if ep % 5 == 0:
            torch.save(dict(nca=nca.state_dict(), opt=opt.state_dict(), ep=ep, hidden=vc.hidden, boid=asdict(bc)), ck)
    torch.save(dict(nca=nca.state_dict(), opt=opt.state_dict(), ep=ep, hidden=vc.hidden, boid=asdict(bc)), ck)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=["train", "eval"])
    ap.add_argument("--set", action="append")
    ap.add_argument("--boid", action="append")
    ap.add_argument("--ckpt", default="")
    ap.add_argument("--probe", action="store_true")
    ap.add_argument("--out", default="")
    ap.add_argument("--threads", type=int, default=4)
    a = ap.parse_args()
    torch.set_num_threads(a.threads)
    import hgrid_eval
    bc = hgrid_eval.parse_sets(hb.BoidCfg(), a.boid)
    if a.cmd == "train":
        vc = hgrid_eval.parse_sets(VoteCfg(), a.set)
        if not os.path.isabs(vc.run):
            vc.run = os.path.join(HERE, vc.run)
        train(vc, bc)
    else:
        import swarm_probe
        model = load_model(a.ckpt, bc)
        data, summary = hgrid_eval.score(model)
        hgrid_eval.report(summary)
        pr = swarm_probe.probe(model) if a.probe else None
        if pr:
            print(json.dumps(pr))
        if a.out:
            hgrid_eval.publish(a.out, model, data, summary, pr, dict(kind="vote", ckpt=a.ckpt, cfg=hb.asdict(bc)))
            torch.save(torch.load(a.ckpt, weights_only=False)["nca"], os.path.join(a.out, "vote_nca.pt"))


if __name__ == "__main__":
    main()
