"""HGRID learned morphogen: a 3D NCA on the swarm's grid that must PRODUCE the field the boids follow.

The upper level of the hierarchy. Each swarm step:
  1. the live tadpoles SPLAT into the grid: per-element density (4) + total (1); the grid also hears
     the swarm-level element mix (4) and headcount, as a game Cell tracks its fauna;
  2. the NCA updates its state (63 channels: the 47-channel field the boid reads + 16 hidden):
     fixed depthwise perception (identity, Sobel x/y/z, Laplacian) -> 1x1 MLP -> residual update,
     stochastic per-cell firing;
  3. the boids (hgrid_boid.FieldBoid, designed) read the field's first 47 channels.

The NCA is never told the plan. It must integrate the element mix into a decision and grow that
plan's morphogen (where each (element, slot) class should be, what it should look like) around the
swarm's centroid - and when the mix changes (members eaten), dissolve one creature's field and grow
the next one's. Supervision is the designed OracleField on the very states the NCA's own boids
visit (DAgger: the driver switches from the oracle to the NCA as training goes), so the model learns
on its own distribution. The grid snaps to whole cells and its state rolls with the swarm.

    python Tools/NCA/hgrid_nca.py train --run runs/hgrid_nca --hours 3
    python Tools/NCA/hgrid_eval.py learned --ckpt runs/hgrid_nca/latest.pt --probe
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

HERE = os.path.dirname(os.path.abspath(__file__))
HID = 16
CH = hc.FIELD_C + HID
IN = 10


def _kernels():
    ident = torch.zeros(3, 3, 3); ident[1, 1, 1] = 1
    d = torch.tensor([-1.0, 0.0, 1.0]); s = torch.tensor([1.0, 2.0, 1.0])
    sx = d[:, None, None] * s[None, :, None] * s[None, None, :] / 32
    sy = s[:, None, None] * d[None, :, None] * s[None, None, :] / 32
    sz = s[:, None, None] * s[None, :, None] * d[None, None, :] / 32
    lap = torch.ones(3, 3, 3) / 26; lap[1, 1, 1] = -1
    return torch.stack([ident, sx, sy, sz, lap])          # [5,3,3,3]


class GridNCA(nn.Module):
    def __init__(self, hidden=96, fire=0.75):
        super().__init__()
        self.fire = fire
        k = _kernels()
        self.register_buffer("kern", k[:, None].repeat(CH, 1, 1, 1, 1).view(5 * CH, 1, 3, 3, 3))
        self.c1 = nn.Conv3d(5 * CH + IN, hidden, 1)
        self.c2 = nn.Conv3d(hidden, CH, 1)
        nn.init.zeros_(self.c2.weight); nn.init.zeros_(self.c2.bias)

    def forward(self, h, inp, gen=None):
        p = F.conv3d(F.pad(h, (1, 1, 1, 1, 1, 1)), self.kern, groups=CH)   # zero boundary (outside = nothing)
        dh = self.c2(F.relu(self.c1(torch.cat([p, inp], 1))))
        if self.fire < 1:
            m = (torch.rand(h.shape[0], 1, *h.shape[2:], generator=gen) < self.fire).to(h.dtype)
            dh = dh * m
        return h + dh


def shift_grid(g, d):
    """Roll grid state g [K,G,G,G] by integer cells d (3,), filling with zeros (new space is empty)."""
    G = g.shape[-1]
    out = torch.zeros_like(g)
    src = [slice(max(0, int(x)), G + min(0, int(x))) for x in d]
    dst = [slice(max(0, -int(x)), G - max(0, int(x))) for x in d]
    out[:, dst[0], dst[1], dst[2]] = g[:, src[0], src[1], src[2]]
    return out


class LearnedField:
    """field_fn for FieldBoid. Keeps the NCA state on the swarm (sw.grid) and its frame centre on
    sw.gcen (so a drifting swarm rolls its morphogen with it). In training, `self.oracle` labels each
    step and `self.beta` is the probability that the NCA (not the oracle) drives a sample."""

    def __init__(self, nca: GridNCA, cfg: hb.BoidCfg, targets):
        self.nca, self.cfg = nca, cfg
        self.oracle = hb.OracleField(targets, cfg)
        self.beta = 1.0
        self.collect = None          # a list to append (pred, label) pairs to (training)
        self.gen = None

    def inputs(self, sw, frame, live):
        oh = F.one_hot(sw.elem, 4).float()
        dens = hc.blur(hc.splat(frame, sw.pos.detach(), oh, live), 1)               # [B,4,G^3]
        tot = dens.sum(1, keepdim=True)
        n = live.float().sum(1).clamp(min=1)
        mix = (oh * live[..., None].float()).sum(1) / n[:, None]
        glob = torch.cat([mix, (n / 100)[:, None]], 1)[:, :, None, None, None].expand(-1, -1, *dens.shape[2:])
        return torch.cat([dens, tot, glob], 1)

    def __call__(self, sw, centres, live):
        cfg = self.cfg
        B = sw.B
        G = cfg.G
        if sw.grid is None or sw.grid.shape[0] != B or bool((sw.clock == 0).any()):
            fresh = sw.grid is None or sw.grid.shape[0] != B
            g0 = torch.zeros(B, CH, G, G, G) if fresh else sw.grid.clone()
            if not fresh:
                g0[sw.clock == 0] = 0
            sw.grid = g0
            sw.gcen = centres.clone()
        if sw.gcen is None or sw.gcen.shape[0] != B:
            sw.gcen = centres.clone()
        # roll the state when the (quantised) centre moved
        dcell = torch.round((centres - sw.gcen) / cfg.cell).long()
        if bool((dcell != 0).any()):
            sw.grid = torch.stack([shift_grid(sw.grid[b], dcell[b]) if bool((dcell[b] != 0).any()) else sw.grid[b]
                                   for b in range(B)])
            sw.gcen = centres.clone()
        frame = hc.GridFrame(centres, G, cfg.cell)
        inp = self.inputs(sw, frame, live)
        h = self.nca(sw.grid, inp, self.gen)
        h = h.clamp(-50, 50)
        sw.grid = h
        pred = torch.cat([F.relu(h[:, :hc.NCLS]), h[:, hc.NCLS:hc.FIELD_C]], 1)
        if self.collect is None and self.beta >= 1.0:
            return pred
        lab = self.oracle(sw, centres, live).detach()
        if self.collect is not None:
            self.collect.append((pred, lab, h))
        if self.beta >= 1.0:
            return pred
        use = (torch.rand(B, generator=self.gen) < self.beta).float()[:, None, None, None, None]
        return use * pred.detach() + (1 - use) * lab


def field_loss(pred, lab, h, w_attr=0.5, w_over=0.1):
    """Relative squared error per sample (the field is mostly empty space, so a plain mean would
    reward predicting nothing): density classes and attribute channels normalised separately."""
    def rel(p, l):
        num = ((p - l) ** 2).flatten(1).sum(1)
        den = (l ** 2).flatten(1).sum(1) + 1e-3
        return (num / den).mean()
    d = rel(pred[:, :hc.NCLS], lab[:, :hc.NCLS])
    a = rel(pred[:, hc.NCLS:], lab[:, hc.NCLS:])
    over = F.relu(h.abs() - 8).mean()
    return d + w_attr * a + w_over * over, float(d.detach()), float(a.detach())


# ----------------------------------------------------------------- train ---

@dataclass
class NcaCfg:
    run: str = os.path.join(HERE, "runs", "hgrid_nca")
    hours: float = 3.0
    batch: int = 4
    window: int = 16             # truncated BPTT window (swarm steps)
    grow_min: int = 120
    grow_max: int = 240
    after: int = 200             # steps after the cull
    p_cull: float = 0.85         # an episode loses its majority mid-way (else: a vessel strike or nothing)
    p_strike: float = 0.5
    lr: float = 1e-3
    hidden: int = 96
    beta0: float = 0.0           # DAgger: chance the NCA drives a sample, ramped beta0 -> beta1 over ramp_eps
    beta1: float = 0.8
    ramp_eps: int = 60
    seed: int = 0


def load_model(path, cfg=None):
    st = torch.load(path, weights_only=False)
    cfg = cfg or hb.BoidCfg(**st.get("boid", {}))
    nca = GridNCA(hidden=st.get("hidden", 96))
    nca.load_state_dict(st["nca"])
    nca.eval()
    targets = sn.load_targets()
    lf = LearnedField(nca, cfg, targets)
    lf.beta = 1.0
    m = hb.FieldBoid(sn.World(), cfg, lf, targets)
    m.nca = nca
    return m


def train(nc: NcaCfg, bc: hb.BoidCfg):
    import swarm_probe
    os.makedirs(nc.run, exist_ok=True)
    torch.manual_seed(nc.seed)
    gen = sn.make_gen(nc.seed)
    targets = sn.load_targets()
    nca = GridNCA(hidden=nc.hidden)
    opt = torch.optim.Adam(nca.parameters(), lr=nc.lr)
    ck = os.path.join(nc.run, "latest.pt")
    ep0 = 0
    if os.path.exists(ck):
        st = torch.load(ck, weights_only=False)
        nca.load_state_dict(st["nca"]); opt.load_state_dict(st["opt"]); ep0 = st["ep"]
        print("resumed at episode", ep0)
    lf = LearnedField(nca, bc, targets)
    lf.gen = gen
    boid = hb.FieldBoid(sn.World(), bc, lf, targets)
    json.dump(dict(nca=asdict(nc), boid=asdict(bc)), open(os.path.join(nc.run, "config.json"), "w"), indent=1)
    log = open(os.path.join(nc.run, "log.jsonl"), "a")
    t_end = time.time() + nc.hours * 3600
    ep = ep0
    while time.time() < t_end:
        t0 = time.time()
        lf.beta = nc.beta0 + (nc.beta1 - nc.beta0) * min(1.0, ep / max(1, nc.ramp_eps))
        kinds = [sn.KINDS[i % 4] for i in range(nc.batch)]
        sw = sn.seed_swarm([targets[k] for k in kinds], boid.world, gen)
        T1 = int(torch.randint(nc.grow_min, nc.grow_max + 1, (1,), generator=gen))
        total = T1 + nc.after
        lf.collect = []
        stats = dict(d=[], a=[])
        events = []
        for t in range(total):
            if t == T1:
                for b in range(sw.B):
                    u = float(torch.rand((), generator=gen))
                    if u < nc.p_cull:
                        r = sn.lose_majority(sw, b, gen, mode="excess")
                        events.append(("cull", r))
                    elif u < nc.p_cull + (1 - nc.p_cull) * nc.p_strike:
                        one = sw.index(torch.tensor([b]))
                        swarm_probe.strike(one, gen=gen)
                        for a in ("active", "hatched", "s"):
                            getattr(sw, a)[b] = getattr(one, a)[0]
                        events.append(("strike", None))
            sw = boid.step(sw, gen, train=True)
            if len(lf.collect) >= nc.window or t == total - 1:
                loss = 0
                for pred, lab, h in lf.collect:
                    l, dd, aa = field_loss(pred, lab, h)
                    loss = loss + l
                    stats["d"].append(dd); stats["a"].append(aa)
                loss = loss / len(lf.collect)
                opt.zero_grad(); loss.backward()
                for p in nca.parameters():
                    if p.grad is not None:
                        p.grad /= (p.grad.norm() + 1e-8)
                opt.step()
                lf.collect = []
                sw.grid = sw.grid.detach()
        lf.collect = None
        ep += 1
        rec = dict(ep=ep, beta=round(lf.beta, 2), T1=T1, d=round(sum(stats["d"]) / len(stats["d"]), 4),
                   a=round(sum(stats["a"]) / len(stats["a"]), 4), sec=round(time.time() - t0, 1),
                   n=[int((sw.active[b] & sw.hatched[b]).sum()) for b in range(sw.B)],
                   plan=[sn.KINDS[int(g)][:2] for g in sw.gplan])
        log.write(json.dumps(rec) + "\n"); log.flush()
        print(json.dumps(rec), flush=True)
        if ep % 5 == 0:
            torch.save(dict(nca=nca.state_dict(), opt=opt.state_dict(), ep=ep, hidden=nc.hidden, boid=asdict(bc)), ck)
        if ep % 20 == 0:
            torch.save(dict(nca=nca.state_dict(), ep=ep, hidden=nc.hidden, boid=asdict(bc)), os.path.join(nc.run, f"nca_{ep:04d}.pt"))
    torch.save(dict(nca=nca.state_dict(), opt=opt.state_dict(), ep=ep, hidden=nc.hidden, boid=asdict(bc)), ck)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=["train"])
    ap.add_argument("--set", action="append")
    ap.add_argument("--boid", action="append")
    a = ap.parse_args()
    torch.set_num_threads(4)
    nc = NcaCfg()
    for kv in a.set or []:
        k, v = kv.split("=", 1); t = type(getattr(nc, k)); setattr(nc, k, t(float(v)) if t in (int, float) else v)
    bc = hb.BoidCfg()
    for kv in a.boid or []:
        k, v = kv.split("=", 1); t = type(getattr(bc, k)); setattr(bc, k, t(float(v)) if t in (int, float) else v)
    if not os.path.isabs(nc.run):
        nc.run = os.path.join(HERE, nc.run)
    train(nc, bc)


if __name__ == "__main__":
    main()
