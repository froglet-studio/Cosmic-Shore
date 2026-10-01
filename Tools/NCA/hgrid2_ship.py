"""HGRID2 direction (d): a player's ship WRITES into the swarm's grid, and the swarm reacts through the
same field it grows by. No new behaviour rule per creature: one extra grid channel.

  threat T   a channel on the swarm's grid. Each step T <- decay x T + splat(ship position) (a Gaussian
             blob of radius ~ship_r), so a moving ship leaves a fading WAKE.
  reaction   every live tadpole adds  k_flee x vmax x T(x) x (-grad T / |grad T|): it is pushed out of the
             ship's path and out of the wake while it lasts. The body's own deficit field is untouched,
             so the hole the ship cuts is REFILLED by the same morphogen once the wake fades - the
             swarm parts around the ship and closes behind it, like a school around a diver.
  nothing dies: the ship only displaces (a vessel strike that kills is swarm_probe's job).

    python Tools/NCA/hgrid2_ship.py [--evo] [--out results/hgrid2/ship.json]
Measures, per plan: how many tadpoles the ship touched (within 2 voxels of it), the body's divergence before
/ during / after, and the steps until it is back within 10% of the pre-pass divergence.
"""
from __future__ import annotations

import argparse, json, math, time
import torch

import swarm_nca as sn
import hgrid_core as hc
import hgrid2_eval as h2


class ShipField:
    def __init__(self, model, G=16, cell=6.0, decay=0.9, ship_r=5.0, k_flee=1.5, speed=2.5):
        self.model, self.world = model, model.world
        self.G, self.cell, self.decay, self.ship_r, self.k_flee, self.speed = G, cell, decay, ship_r, k_flee, speed
        self.T = None; self.ship = None; self.vel = None

    def launch(self, sw, gen):
        """A pass straight through the body's centroid from 40 voxels out, along a random direction."""
        m = sw.active[0] & sw.hatched[0]
        c = sw.pos[0][m].mean(0)
        d = torch.randn(3, generator=gen); d = d / d.norm()
        self.ship = c - 40 * d; self.vel = d * self.speed; self.T = None; self.cen = c

    def __call__(self, sw, gen=None):
        out = self.model(sw, gen)
        if self.ship is None:
            return out
        frame = hc.GridFrame(self.cen[None], self.G, self.cell)
        blob = torch.zeros(1, 1, 7, 7, 7)
        pts = self.ship[None, None] + torch.zeros(1, 1, 3)
        S = hc.splat(frame, pts, torch.ones(1, 1, 1))
        S = hc.blur(S, max(1, int(round(self.ship_r / self.cell * 2))))
        S = S / S.max().clamp(min=1e-6)
        self.T = S if self.T is None else torch.maximum(self.decay * self.T, S)
        live = out.active & out.hatched
        g = hc.grad(frame, self.T)[:, 0]                                   # [1,3,G,G,G]
        smp = hc.sample(frame, torch.cat([self.T, g], 1), out.pos)         # [1,N,4]
        t, gv = smp[..., 0], smp[..., 1:]
        # close to the ship itself, flee straight away from it (the grid gradient is coarse at the centre)
        away = out.pos - self.ship
        dist = away.norm(dim=-1, keepdim=True)
        dirn = torch.where(dist < 2 * self.ship_r, away / dist.clamp(min=1e-3), -gv / gv.norm(dim=-1, keepdim=True).clamp(min=1e-6))
        vmax = torch.tensor(self.world.vmax)[out.elem][..., None]
        push = self.k_flee * vmax * t[..., None].clamp(0, 1) * dirn
        out.pos = out.pos + push * live[..., None].float()
        self.ship = self.ship + self.vel
        self.last_touch = int(((out.pos[0] - self.ship).norm(dim=-1) < 2.0)[live[0]].sum())
        return out


@torch.no_grad()
def run(model, kinds=sn.KINDS, seed=11, grow=240, after=200):
    T = sn.load_targets(); L = sn.LossCfg(); res = {}
    for k in kinds:
        gen = sn.make_gen(seed)
        sh = ShipField(model)
        sw = sn.seed_swarm([T[k]], model.world, gen)
        for _ in range(grow):
            sw = sh(sw, gen)
        score = lambda: round(sn.swarm_loss(sn.decode(sw, 0), T[k], L)[1]["sink"], 2)
        r = dict(before=score(), n=int((sw.active[0] & sw.hatched[0]).sum()))
        sh.launch(sw, gen)
        touch, worst, curve = 0, 0.0, []
        for t in range(after):
            sw = sh(sw, gen)
            touch += sh.last_touch
            if t % 10 == 9:
                v = score(); curve.append(v); worst = max(worst, v)
        r.update(worst=worst, after=curve[-1], touched=touch, n_after=int((sw.active[0] & sw.hatched[0]).sum()), curve=curve)
        back = [i for i, v in enumerate(curve) if i * 10 > 40 / sh.speed and v <= 1.1 * r["before"] + 0.3]
        r["recovered_at_step"] = (back[0] + 1) * 10 if back else None
        res[k] = r
        print(k, {q: r[q] for q in r if q != "curve"}, flush=True)
    return res


if __name__ == "__main__":
    ap = argparse.ArgumentParser(); ap.add_argument("--evo", action="store_true"); ap.add_argument("--out", default="")
    a = ap.parse_args(); torch.set_num_threads(2)
    if a.evo:
        import hgrid2_evo; kw = dict(h2.BEST); kw.update(h2.EVO); model = hgrid2_evo.make(**kw)
    else:
        import hgrid2_model; model = hgrid2_model.make(**h2.BEST)
    res = run(model)
    if a.out:
        json.dump(res, open(a.out, "w"), indent=1)
