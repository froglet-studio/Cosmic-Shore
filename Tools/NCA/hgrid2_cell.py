"""HGRID2 direction (d): a CELL-scale morphogen herding several swarms at once.

Each swarm keeps its own body (hgrid2_model: plan, fine layer, migrants, composition). On top, the Cell
owns one coarse field over the whole cell (here 24^3 cells of 8 voxels, centred on the cell):

  shared density   every live tadpole of every swarm splats into one channel. A swarm feels the OTHERS'
                   density (total minus its own) and its members are pushed down that gradient: bodies
                   keep apart without any swarm knowing about another.
  home channel     a per-swarm "come here" bump the Cell writes (e.g. a territory, a nucleus orbit). The
                   whole swarm drifts toward it (a uniform translation of its members, so the body is
                   carried, not deformed). Moving the homes HERDS the swarms.

Nothing here changes a creature's rule; the Cell writes two fields. Measured: per-swarm own-plan
divergence while being herded, body overlap (share of tadpoles within 2.5 voxels of another swarm's),
and how closely each centroid follows its moving home.

    python Tools/NCA/hgrid2_cell.py [--out results/hgrid2/cell.json]
"""
from __future__ import annotations

import argparse, json, math
import torch

import swarm_nca as sn
import hgrid_core as hc
import hgrid2_model as hm
import hgrid2_eval as h2


@torch.no_grad()
def run(kinds=("mass", "space", "charge"), steps=600, G=24, cell=8.0, k_sep=1.2, k_home=0.25, home_r=36.0,
        orbit=0.004, seed=5, log=print, anchor_step=0.3, cfg=None, mode="anchor"):
    T = sn.load_targets(); L = sn.LossCfg()
    model = hm.make(**dict(h2.BEST, **(cfg or {})))
    W = model.world
    gen = sn.make_gen(seed)
    sw = sn.seed_swarm([T[k] for k in kinds], W, gen)
    B = len(kinds)
    ang0 = torch.arange(B) * (2 * math.pi / B)
    homes = lambda t: torch.stack([home_r * torch.cos(ang0 + orbit * t), home_r * torch.sin(ang0 + orbit * t), torch.zeros(B)], 1)
    sw.pos = sw.pos + homes(0)[:, None, :] * sw.active[..., None].float()
    frame = hc.GridFrame(torch.zeros(1, 3), G, cell)
    hist = []
    anchor = homes(0).clone()
    for t in range(steps):
        if mode == "anchor":
            # the Cell writes WHERE each body should be: its morphogen centre walks toward the home at a bounded
            # rate, and the body swims after its own field (fine layer + migrants)
            lf0 = (sw.active & sw.hatched).float()
            c0 = (sw.pos * lf0[..., None]).sum(1) / lf0.sum(1).clamp(min=1)[:, None]
            dlt = homes(t) - anchor
            anchor = anchor + dlt * (anchor_step / dlt.norm(dim=-1, keepdim=True).clamp(min=anchor_step))
            # never let the anchor run away from the body it carries (it leads by at most 6 voxels)
            lead = anchor - c0
            anchor = c0 + lead * (6.0 / lead.norm(dim=-1, keepdim=True).clamp(min=6.0))
            model.anchor = anchor
        sw = model(sw, gen)
        live = sw.active & sw.hatched
        lf = live.float()
        # the Cell's shared density field, one channel per swarm (dims [1, B, G^3])
        P = sw.pos.reshape(1, -1, 3)
        V = torch.nn.functional.one_hot(torch.arange(B).repeat_interleave(sw.pos.shape[1]), B).float()[None]
        D = hc.blur(hc.splat(frame, P, V, live.reshape(1, -1)), 1)
        others = D.sum(1, keepdim=True) - D                                     # [1,B,...]
        g = hc.grad(frame, others)[0]                                           # [B,3,G,G,G]
        gs = hc.sample(frame, g.reshape(1, B * 3, G, G, G), P).reshape(1, -1, B, 3)[0]   # [B*N, B, 3]
        own = torch.arange(B).repeat_interleave(sw.pos.shape[1])
        push = -gs[torch.arange(len(own)), own].reshape(B, -1, 3)
        vmax = torch.tensor(W.vmax)[sw.elem][..., None]
        pn = push.norm(dim=-1, keepdim=True)
        push = k_sep * vmax * push / pn.clamp(min=1e-6) * (pn / (pn + 0.05))
        cen = (sw.pos * lf[..., None]).sum(1) / lf.sum(1).clamp(min=1)[:, None]
        drift = homes(t + 1) - cen
        dn = drift.norm(dim=-1, keepdim=True)
        if mode == "anchor":
            drift = drift * 0
        drift = k_home * drift / dn.clamp(min=1e-6) * torch.minimum(dn, torch.ones_like(dn) * 2.0) / 2.0
        sw.pos = sw.pos + (push + drift[:, None, :]) * lf[..., None]
        if t % 50 == 49:
            row = {}
            for b, k in enumerate(kinds):
                row[k] = dict(loss=round(sn.swarm_loss(sn.decode(sw, b), T[k], L)[1]["sink"], 2), n=int(live[b].sum()),
                              home_err=round(float((cen[b] - homes(t + 1)[b]).norm()), 1))
            allp = torch.cat([sw.pos[b][live[b]] for b in range(B)])
            tag = torch.cat([torch.full((int(live[b].sum()),), b) for b in range(B)])
            d = torch.cdist(allp, allp); d[tag[:, None] == tag[None]] = 1e9
            row["overlap"] = round(float((d.min(1).values < 2.5).float().mean()), 3)
            row["t"] = t + 1
            hist.append(row); log(row)
    return hist


if __name__ == "__main__":
    ap = argparse.ArgumentParser(); ap.add_argument("--out", default=""); ap.add_argument("--k_sep", type=float, default=1.2)
    a = ap.parse_args(); torch.set_num_threads(4)
    ap2 = a
    h = run(k_sep=a.k_sep, log=lambda r: print(json.dumps(r), flush=True))
    if a.out:
        json.dump(h, open(a.out, "w"), indent=1)
