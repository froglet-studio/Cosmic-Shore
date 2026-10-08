"""HGRID end-to-end: G2's learned rule CONDITIONED on grid samples, fine-tuned with swarm_loss.

Variant A of the direction. The grid layer (hgrid_boid: designed morphogen + composition control) is
in the loop exactly as in hgrid_hybrid; the change is that every tadpole now also SAMPLES the grid -
its class's deficit there, the all-class deficit and wanted density, the gradient of its class
deficit, and the plan's flow at its cell (9 channels) - as extra inputs to the learned rule. Those
input columns start at zero weight, so step 0 IS the hybrid (7/8); training is the same G2 recipe
(sticky labels, switches, overflow penalty, body floor) on swarm_nca.swarm_loss.

    python Tools/NCA/hgrid_e2e.py train --run runs/hgrid_e2e --hours 2
    python Tools/NCA/hgrid_e2e.py eval --ckpt runs/hgrid_e2e/rule_e2e.pt [--probe] [--out ...]
"""
from __future__ import annotations

import argparse
import json
import os
import time
from dataclasses import asdict

import torch
import torch.nn as nn
import torch.nn.functional as F

import swarm_nca as sn
import hgrid_core as hc
import hgrid_boid as hb

HERE = os.path.dirname(os.path.abspath(__file__))
K = 9


class GridRule(sn.SwarmRule):
    """SwarmRule with K extra inputs (grid samples) appended to its perception, before the population
    signals. `self._extra` [B*N, K] must be set before each forward."""

    def __init__(self, world, hidden=192):
        super().__init__(world, hidden)
        self.P = self.F - self.G                       # perception width
        self.F = self.F + K
        self.w1 = nn.Parameter(torch.zeros(hidden, self.F))
        self._extra = None

    def load_g2(self, sd):
        sd = dict(sd)
        w = sd["w1"]
        new = torch.zeros(w.shape[0], self.F)
        new[:, :self.P] = w[:, :self.P]
        new[:, self.P + K:] = w[:, self.P:]
        sd["w1"] = new
        self.load_state_dict(sd)

    def perceive(self, pos, x, dom, gi, gj, n):
        return torch.cat([super().perceive(pos, x, dom, gi, gj, n), self._extra], 1)


class E2EModel:
    """model(sw, gen): grid layer -> features -> learned rule step (no designed laying) -> grid
    composition (shed + lay). Gradients flow through the rule; grid features are inputs (detached)."""

    def __init__(self, rule: GridRule, cfg: hb.BoidCfg, shed=1):
        self.rule, self.cfg, self.shed = rule, cfg, shed
        self.world = rule.world
        self.targets = sn.load_targets()
        self.oracle = (hb.WaveField if cfg.wave else hb.OracleField)(self.targets, cfg)
        self.boid = hb.FieldBoid(self.world, cfg, self.oracle, self.targets)

    def _grid(self, sw, live):
        cfg = self.cfg
        B, N, _ = sw.pos.shape
        lf = live.float()
        centres = (sw.pos.detach() * lf[..., None]).sum(1) / lf.sum(1).clamp(min=1)[:, None]
        centres = torch.round(centres / cfg.cell) * cfg.cell
        hb.decide_plan(sw, live, cfg, self.targets)
        frame = hc.GridFrame(centres, cfg.G, cfg.cell)
        D = self.oracle(sw, centres, live)
        Dc = D[:, :hc.NCLS].reshape(B, 4, 3, *D.shape[2:])
        inv = torch.zeros(B, 3, dtype=torch.long)
        for b in range(B):
            for s in range(3):
                inv[b, sw.dmap[b, s]] = s
        Dd = torch.stack([Dc[b][:, inv[b]] for b in range(B)]).reshape(B, 12, *D.shape[2:])
        cls = sw.elem * 3 + sw.dom
        A = hc.blur(hc.splat(frame, sw.pos.detach(), F.one_hot(cls, 12).float(), live), 1)
        Def = Dd - A
        tot = torch.cat([Def.sum(1, keepdim=True), Dd.sum(1, keepdim=True)], 1)
        ch = torch.cat([Def, tot, hc.grad(frame, Def).flatten(1, 2)], 1)          # 12 + 2 + 36
        if D.shape[1] > hc.FIELD_C:
            ch = torch.cat([ch, D[:, hc.FIELD_C:], D[:, :hc.NCLS]], 1)             # + 12 flow + 12 dens
        smp = hc.sample(frame, ch, sw.pos.detach())
        d_own = torch.gather(smp[..., :12], 2, cls[..., None])[..., 0]
        g = smp[..., 14:50].reshape(B, N, 12, 3)
        g_own = torch.gather(g, 2, cls[..., None, None].expand(B, N, 1, 3))[:, :, 0]
        fe = torch.zeros(B, N, 3)
        if D.shape[1] > hc.FIELD_C:
            fl = smp[..., 50:62].view(B, N, 4, 3)
            de = smp[..., 62:74].view(B, N, 4, 3).sum(-1)
            fe = torch.gather(fl, 2, sw.elem[..., None, None].expand(B, N, 1, 3))[:, :, 0] \
                / torch.gather(de, 2, sw.elem[..., None]).clamp(min=0.2)
        extra = torch.cat([d_own[..., None], smp[..., 12:14], 4 * g_own, 4 * fe], -1)   # [B,N,9]
        want = Dd.flatten(2).sum(-1)
        wf = getattr(self.oracle, "want_field", None)
        if wf is not None:                       # wave: breeding budget = the decided plan's whole template
            Wc = wf[:, :hc.NCLS].reshape(B, 4, 3, -1).sum(-1)
            want = torch.stack([Wc[b][:, inv[b]] for b in range(B)]).reshape(B, 12)
        return extra, g, d_own, want, cls

    def __call__(self, sw, gen=None, fire=None):
        cfg = self.cfg
        hsw = hb.HSwarm.lift(sw)
        if bool((hsw.clock == 0).all()):
            hsw.gplan[:] = -1
        live = hsw.active & hsw.hatched
        extra, g, d_own, want, cls = self._grid(hsw, live)
        B, N = hsw.pos.shape[:2]
        self.rule._extra = extra.detach().reshape(B * N, K).clamp(-10, 10)
        out = hb.HSwarm.lift(self.rule(sw, gen, bud=False, fire=fire))
        out.gplan, out.dmap = hsw.gplan.clone(), hsw.dmap.clone()
        out.grid, out.gcen = hsw.grid, hsw.gcen          # the wave's per-cell plan state rides along
        with torch.no_grad():
            live = out.active & out.hatched
            lf = live.float()
            have = torch.zeros(B, 12).scatter_add(1, cls, lf)
            if self.shed:
                surplus = have > (1 + cfg.starve_tol) * want + 1.0
                sur_i = torch.gather(surplus, 1, cls) & live & (d_own < cfg.starve_local)
                h = out.s[..., 25].detach()
                h = torch.where(sur_i, h + cfg.starve_rate, (h - cfg.starve_rate).clamp(min=0))
                died = live & (h > 1.0)
                out.deaths = out.deaths + died.sum(1)
                out.active = out.active & ~died; out.hatched = out.hatched & ~died
                live = live & ~died
                have = torch.zeros(B, 12).scatter_add(1, cls, live.float())
            hcol = torch.where(died, torch.zeros_like(h), h) if self.shed else None
        if self.shed:
            s = out.s.clone()
            s[..., 25] = hcol
            out.s = s * live[..., None].float() + s * (out.active & ~out.hatched)[..., None].float()
        self.boid._lay(out, live, have, want, g, gen)
        return out


def make(rule_path="results/swarm_coevo_g2/rule.pt", cfg=None):
    cfg = cfg or hb.BoidCfg()
    st = torch.load(os.path.join(HERE, rule_path) if not os.path.isabs(rule_path) else rule_path, weights_only=False)
    w = st["world"]; w["vmax"] = tuple(w["vmax"])
    world = sn.World(**w)
    rule = GridRule(world, hidden=st["hidden"])
    if st["rule"]["w1"].shape[1] == rule.F:
        rule.load_state_dict(st["rule"])
    else:
        rule.load_g2(st["rule"])
    return E2EModel(rule, cfg)


# ------------------------------------------------------------------ train ---

def train(run, hours, init, cfg_b: hb.BoidCfg, lr=1.5e-4, per_kind=2, pool=12, bptt=24, roll_min=48, roll_max=80,
          p_switch=0.3, seed=0, eval_every=150):
    import hgrid_eval
    os.makedirs(run, exist_ok=True)
    torch.manual_seed(seed)
    gen = sn.make_gen(seed)
    targets = sn.load_targets()
    model = make(init, cfg_b)
    rule = model.rule
    L = sn.LossCfg(w_over=1.0, min_body=76, w_body=20)
    opt = torch.optim.Adam(rule.parameters(), lr=lr)
    ck = os.path.join(run, "latest.pt")
    step0 = 0
    pools = None
    if os.path.exists(ck):
        st = torch.load(ck, weights_only=False)
        rule.load_state_dict(st["rule"]); opt.load_state_dict(st["opt"]); step0 = st["step"]; pools = st["pool"]
        print("resumed", step0)
    if pools is None:                       # pre-grown pools (no grad), each sample at its own clock
        pools = {}
        for k in sn.KINDS:
            sw = sn.seed_swarm([targets[k]] * pool, model.world, gen)
            with torch.no_grad():
                for _ in range(120):
                    sw = model(sw, gen)
            pools[k] = hb.HSwarm.lift(sw)
    log = open(os.path.join(run, "log.jsonl"), "a")
    best = -1
    bf = os.path.join(run, "best.json")
    if os.path.exists(bf):
        best = json.load(open(bf))["tests"]
    t_end = time.time() + hours * 3600
    step = step0
    while time.time() < t_end:
        t0 = time.time()
        batch, picks = [], {}
        for k in sn.KINDS:
            idx = torch.randperm(pool, generator=gen)[:per_kind]
            picks[k] = idx
            sub = pools[k].index(idx)
            if step % 5 == 0:
                fresh = hb.HSwarm.lift(sn.seed_swarm([targets[k]], model.world, gen))
                sub = hb.HSwarm.cat([fresh, sub.index(torch.arange(1, per_kind))])
            for j in range(per_kind):
                if int(sub.since[j]) >= 240 and float(torch.rand((), generator=gen)) < p_switch:
                    new = sn.lose_majority(sub, j, gen, mode="excess")
                    if new is not None:
                        sub.plan[j] = sn.KINDS.index(new); sub.since[j] = 0
            batch.append(sub)
        sw = hb.HSwarm.cat(batch)
        T = int(torch.randint(roll_min, roll_max + 1, (1,), generator=gen))
        with torch.no_grad():
            for _ in range(T - bptt):
                sw = model(sw, gen)
        sw = sw.detach()
        for _ in range(bptt):
            sw = model(sw, gen)
        losses, infos = [], []
        for b in range(sw.B):
            k = sn.KINDS[int(sw.plan[b])]                 # sticky label (seed / last cull)
            l, info = sn.swarm_loss(sn.decode(sw, b), targets[k], L)
            losses.append(l); infos.append(info)
        ok = [bool(torch.isfinite(l)) for l in losses]
        loss = torch.stack([l for l, o in zip(losses, ok) if o]).sum() / per_kind if any(ok) else torch.zeros(())
        opt.zero_grad()
        if any(ok):
            loss.backward()
            for p in rule.parameters():
                if p.grad is not None:
                    p.grad = torch.nan_to_num(p.grad) / (p.grad.norm() + 1e-8)
            opt.step()
        sw = sw.detach()
        for i, k in enumerate(sn.KINDS):
            for j, pi in enumerate(picks[k].tolist()):
                b = i * per_kind + j
                src = sw.index(torch.tensor([b]))
                if int((src.active & src.hatched).sum()) == 0 or not ok[b] or infos[b]["sink"] > 120:
                    src = hb.HSwarm.lift(sn.seed_swarm([targets[k]], model.world, gen))
                for a in sn.Swarm.FIELDS + ("gplan", "dmap"):
                    getattr(pools[k], a)[pi] = getattr(src, a)[0]
        rec = dict(step=step, loss=round(float(loss), 3), T=T, sec=round(time.time() - t0, 1),
                   sink=[round(i_["sink"], 1) for i_ in infos], n=[int(i_["n"]) for i_ in infos],
                   plan=[sn.KINDS[int(p)][:2] for p in sw.plan])
        log.write(json.dumps(rec) + "\n"); log.flush()
        if step % 10 == 0:
            print(json.dumps(rec), flush=True)
        step += 1
        if step % 25 == 0:
            torch.save(dict(rule=rule.state_dict(), opt=opt.state_dict(), step=step, pool=pools), ck)
        if step % eval_every == 0:
            torch.save(dict(rule=rule.state_dict(), world=asdict(model.world), hidden=rule.hidden, step=step),
                       os.path.join(run, f"rule_e2e_{step:05d}.pt"))
            _, summ = hgrid_eval.score(model)
            tp = summ["tests_passed"]
            print(f"EVAL step {step}: {tp}/8 close {summ['close']}", flush=True)
            log.write(json.dumps(dict(eval=step, tests=tp, close=summ["close"])) + "\n"); log.flush()
            if tp > best or (tp == best and summ["close"] < json.load(open(bf))["close"]):
                best = tp
                json.dump(dict(tests=tp, close=summ["close"], step=step), open(bf, "w"))
                torch.save(dict(rule=rule.state_dict(), world=asdict(model.world), hidden=rule.hidden, step=step),
                           os.path.join(run, "rule_e2e.pt"))
    torch.save(dict(rule=rule.state_dict(), opt=opt.state_dict(), step=step, pool=pools), ck)


def main():
    import hgrid_eval, swarm_probe
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=["train", "eval"])
    ap.add_argument("--run", default="runs/hgrid_e2e")
    ap.add_argument("--hours", type=float, default=2.0)
    ap.add_argument("--init", default="results/swarm_coevo_g2/rule.pt")
    ap.add_argument("--ckpt", default="")
    ap.add_argument("--probe", action="store_true")
    ap.add_argument("--out", default="")
    ap.add_argument("--threads", type=int, default=4)
    ap.add_argument("--set", action="append")
    a = ap.parse_args()
    torch.set_num_threads(a.threads)
    cfg = hgrid_eval.parse_sets(hb.BoidCfg(), a.set)
    run = a.run if os.path.isabs(a.run) else os.path.join(HERE, a.run)
    if a.cmd == "train":
        train(run, a.hours, a.init, cfg)
        return
    model = make(a.ckpt or a.init, cfg)
    data, summary = hgrid_eval.score(model)
    hgrid_eval.report(summary)
    pr = swarm_probe.probe(model) if a.probe else None
    if pr:
        print(json.dumps(pr))
    if a.out:
        hgrid_eval.publish(a.out, model, data, summary, pr, dict(kind="e2e", ckpt=a.ckpt, cfg=hb.asdict(cfg)))
        torch.save(torch.load(a.ckpt, weights_only=False), os.path.join(a.out, "rule_e2e.pt"))


if __name__ == "__main__":
    main()
