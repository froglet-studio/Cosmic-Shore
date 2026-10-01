"""HGRID hybrid: the LEARNED per-tadpole rule (G2) keeps motion, look, hatching and death; the grid
layer takes over only what G2 could not learn - the element MIX (laying + egg element) and, optionally,
shedding surplus classes.

Why: G2 grows every plan correctly but fails three of four switches because after the cull its
element mix drifts (laying is blind to what the body needs). Here the swarm's grid field says how many
of each (element, slot) class the current plan wants; a class with room breeds, a class without
does not, and a share of births take the most-wanted element. No retraining: G2's weights as shipped.

    python Tools/NCA/hgrid_hybrid.py [--rule results/swarm_coevo_g2/rule.pt] [--set p_cross=0.25]
"""
from __future__ import annotations

import argparse
import json

import torch

import swarm_nca as sn
import hgrid_core as hc
import hgrid_boid as hb


class HybridRule:
    def __init__(self, rule: sn.SwarmRule, cfg: hb.BoidCfg, shed=1, g2_lay=0):
        self.rule, self.cfg, self.shed, self.g2_lay = rule, cfg, shed, g2_lay
        self.world = rule.world
        self.targets = sn.load_targets()
        self.oracle = hb.OracleField(self.targets, cfg)
        self.boid = hb.FieldBoid(self.world, cfg, self.oracle, self.targets)

    def __call__(self, sw, gen=None):
        cfg = self.cfg
        hsw = hb.HSwarm.lift(sw)
        gplan, dmap = hsw.gplan.clone(), hsw.dmap.clone()
        if bool((hsw.clock == 0).all()):
            gplan[:] = -1
        out = self.rule(sw, gen, bud=bool(self.g2_lay))
        out = hb.HSwarm.lift(out)
        out.gplan, out.dmap = gplan, dmap
        B, N, _ = out.pos.shape
        live = out.active & out.hatched
        lf = live.float()
        centres = (out.pos * lf[..., None]).sum(1) / lf.sum(1).clamp(min=1)[:, None]
        centres = torch.round(centres / cfg.cell) * cfg.cell
        hb.decide_plan(out, live, cfg, self.targets)
        frame = hc.GridFrame(centres, cfg.G, cfg.cell)
        D = self.oracle(out, centres, live)
        Dc = D[:, :hc.NCLS].reshape(B, 4, 3, *D.shape[2:])
        inv = torch.zeros(B, 3, dtype=torch.long)
        for b in range(B):
            for s in range(3):
                inv[b, out.dmap[b, s]] = s
        Dd = torch.stack([Dc[b][:, inv[b]] for b in range(B)]).reshape(B, 12, *D.shape[2:])
        cls = out.elem * 3 + out.dom
        A = hc.blur(hc.splat(frame, out.pos, torch.nn.functional.one_hot(cls, 12).float(), live), 1)
        Def = Dd - A
        gd = hc.sample(frame, hc.grad(frame, Def).flatten(1, 2), out.pos).reshape(B, N, 12, 3)
        d_own = torch.gather(hc.sample(frame, Def, out.pos), 2, cls[..., None])[..., 0]
        want = Dd.flatten(2).sum(-1)
        have = torch.zeros(B, 12).scatter_add(1, cls, lf)
        if self.shed:
            surplus = have > (1 + cfg.starve_tol) * want + 1.0
            sur_i = torch.gather(surplus, 1, cls) & live & (d_own < cfg.starve_local)
            # hunger rides on a hidden channel (25) so the rule's own death channel is left alone
            h = out.s[..., 25]
            h = torch.where(sur_i, h + cfg.starve_rate, (h - cfg.starve_rate).clamp(min=0))
            died = live & (h > 1.0)
            out.s[..., 25] = torch.where(died, torch.zeros_like(h), h)
            out.deaths = out.deaths + died.sum(1)
            out.active = out.active & ~died; out.hatched = out.hatched & ~died
            out.s = out.s * (~died)[..., None].float()
            live = live & ~died
            have = torch.zeros(B, 12).scatter_add(1, cls, live.float())
        self.boid._lay(out, live, have, want, gd, gen)
        # hand the eggs to the learned rule: alpha starts at 0 and the rule hatches them (as in G2)
        return out


def main():
    import hgrid_eval, swarm_probe
    ap = argparse.ArgumentParser()
    ap.add_argument("--rule", default="results/swarm_coevo_g2/rule.pt")
    ap.add_argument("--set", action="append")
    ap.add_argument("--shed", type=int, default=1)
    ap.add_argument("--g2-lay", type=int, default=0)
    ap.add_argument("--probe", action="store_true")
    ap.add_argument("--out", default="")
    ap.add_argument("--threads", type=int, default=4)
    a = ap.parse_args()
    torch.set_num_threads(a.threads)
    cfg = hgrid_eval.parse_sets(hb.BoidCfg(), a.set)
    model = HybridRule(sn.load_rule(a.rule), cfg, a.shed, a.g2_lay)
    data, summary = hgrid_eval.score(model)
    hgrid_eval.report(summary)
    pr = swarm_probe.probe(model) if a.probe else None
    if pr:
        print(json.dumps(pr))
    if a.out:
        hgrid_eval.publish(a.out, model, data, summary, pr, dict(kind="hybrid_g2", rule=a.rule, cfg=hb.asdict(cfg), shed=a.shed))


if __name__ == "__main__":
    main()
