"""Train the pure emergent swimmer (emergent_model.EmergentRule) on ONE animated target.

BPTT through the simulation (positions, states AND the chemical water), Adam with per-tensor
normalised gradients (as swarm_nca.train). A persistent pool of swarms (the NCA pool trick) so the
rule learns to persist and keep pulsing, not just to reach a body once.

Curriculum (a stage per run, set by flags):
  static : the loss is the divergence to the best-matching FRAME of the cycle (swarm_nca.swarm_loss
           with frames=None): grow and hold a body, any phase.
  anim   : `window` checkpoints `period` steps apart are matched to consecutive frames of the cycle
           at the best starting phase (swarm_nca.anim_loss, k0=None: no clock is given to the rule
           or the loss). One cycle = 8 frames x period steps.
Every stage: death is penalised (LossCfg.w_survive on the death pull, plus w_death on the realised
death count's straight-through pull), state overflow is penalised (w_over), and pool samples are
STRUCK (swarm_probe-style sphere cut) with probability p_strike so healing is learned.

    python Tools/NCA/emergent_train.py --run runs/em_a --stage static --steps 400 --init-solo
"""
from __future__ import annotations

import argparse
import json
import os
import time
from dataclasses import asdict, dataclass, replace

import torch

import emergent_model as em
import swarm_nca as sn

HERE = os.path.dirname(os.path.abspath(__file__))
SOLO = os.path.join(HERE, "results", "solo_space", "rule_latest.pt")


@dataclass
class Cfg:
    run: str = os.path.join(HERE, "runs", "em")
    plan: str = "space"
    stage: str = "static"
    steps: int = 400
    lr: float = 3e-4
    lr_end: float = 1e-4
    batch: int = 6
    pool: int = 24
    seed_every: int = 3
    pre_min: int = 8
    pre_max: int = 40
    bptt: int = 32           # static stage
    period: int = 8          # anim stage: steps per target frame
    window: int = 6          # anim stage: checkpoints per rollout
    w_speed: float = 1.0
    w_over: float = 1.0
    w_survive: float = 60.0
    p_strike: float = 0.08
    replace_above: float = 60.0
    replace_n: int = 1000    # pool hygiene: an overgrown sample (death is penalised, so it can never shrink) is reseeded
    init: str = ""           # an emergent rule .pt to resume weights from (new run)
    init_solo: int = 0       # warm start from the solo_space specialist, census folded to a constant
    seed: int = 0
    snap_every: int = 25
    log_every: int = 1
    threads: int = 4


GLOB_CONST = [0.88, 25 / 88, 2 / 88, 54 / 88, 7 / 88]     # the plan's own headcount/100 and mix, as a CONSTANT


def strike_b(sw, b, gen, frac=1.0):
    """swarm_probe.strike for sample b, in place."""
    m = sw.active[b] & sw.hatched[b]
    p = sw.pos[b][m]
    if len(p) < 24:
        return 0
    c = p.mean(0)
    rms = ((p - c) ** 2).sum(-1).mean().sqrt()
    d = torch.randn(3, generator=gen); d = d / d.norm().clamp(min=1e-6)
    hit = m & (((sw.pos[b] - (c + frac * rms * d)) ** 2).sum(-1) < (frac * rms) ** 2)
    sw.active[b, hit] = False; sw.hatched[b, hit] = False; sw.s[b, hit] = 0.0
    return int(hit.sum())


def seed(T, world, gen, k=1):
    return em.ESwarm.of(sn.seed_swarm([T] * k, world, gen))


def over_term(x, band):
    return torch.relu(x["s"].abs() - band).sum(-1).mean() if len(x["s"]) else x["s"].sum()


def train(cfg: Cfg):
    torch.set_num_threads(cfg.threads)
    os.makedirs(cfg.run, exist_ok=True)
    T = sn.load_targets()[cfg.plan]
    L = sn.LossCfg(w_over=cfg.w_over, w_survive=cfg.w_survive)
    world = sn.World(learned_lay=1)
    torch.manual_seed(cfg.seed)
    gen = sn.make_gen(cfg.seed)
    rule = em.EmergentRule(world)
    if cfg.init_solo:
        rule.warm_from(SOLO, GLOB_CONST)
    if cfg.init:
        rule.load_state_dict(em.load_rule(cfg.init).state_dict())
    opt = torch.optim.Adam(rule.parameters(), lr=cfg.lr)
    gamma = (cfg.lr_end / cfg.lr) ** (1 / max(1, cfg.steps))
    sched = torch.optim.lr_scheduler.ExponentialLR(opt, gamma)
    pool = seed(T, world, gen, cfg.pool)
    start = 0
    ck = os.path.join(cfg.run, "latest.pt")
    if os.path.exists(ck):
        st = torch.load(ck, weights_only=False)
        rule.load_state_dict(st["rule"]); opt.load_state_dict(st["opt"]); sched.load_state_dict(st["sched"])
        pool = st["pool"]; start = st["step"]; gen.set_state(st["gen"])
        print(f"resumed at {start}", flush=True)
    json.dump(asdict(cfg), open(os.path.join(cfg.run, "config.json"), "w"), indent=1)
    log = open(os.path.join(cfg.run, "log.jsonl"), "a")
    for step in range(start, cfg.steps):
        t0 = time.time()
        idx = torch.randperm(cfg.pool, generator=gen)[:cfg.batch]
        sw = pool.index(idx)
        if step % cfg.seed_every == 0:
            sw = em.ESwarm.cat([seed(T, world, gen), sw.index(torch.arange(1, cfg.batch))])
        struck = 0
        for b in range(1, cfg.batch):
            if float(torch.rand((), generator=gen)) < cfg.p_strike:
                struck += strike_b(sw, b, gen) > 0
        pre = int(torch.randint(cfg.pre_min, cfg.pre_max + 1, (1,), generator=gen))
        with torch.no_grad():
            for _ in range(pre):
                sw = rule(sw, gen)
        sw = sw.detach()
        losses, infos = [], []
        if cfg.stage == "static":
            for _ in range(cfg.bptt):
                sw = rule(sw, gen)
            for b in range(cfg.batch):
                x = sn.decode(sw, b)
                l, info = sn.swarm_loss(x, T, L)
                losses.append(l); infos.append(info)
        else:
            cks = []
            for i in range(cfg.window * cfg.period):
                sw = rule(sw, gen)
                if (i + 1) % cfg.period == 0:
                    cks.append(sw)
            for b in range(cfg.batch):
                xs = [sn.decode(c, b) for c in cks]
                l, info = sn.anim_loss(xs, T, L, cfg.period, cfg.w_speed, None)
                ov = torch.stack([over_term(x, L.over_band) for x in xs]).mean()
                l = l + cfg.w_over * ov
                info["over"] = float(ov.detach())
                losses.append(l); infos.append(info)
        ok = [bool(torch.isfinite(l)) for l in losses]
        opt.zero_grad()
        nonfinite = 0
        if any(ok):
            loss = torch.stack([l for l, g in zip(losses, ok) if g]).mean()
            loss.backward()
            grads = [p.grad for p in rule.parameters() if p.grad is not None]
            if all(bool(torch.isfinite(g).all()) for g in grads):
                for g in grads:
                    g /= (g.norm() + 1e-8)
                opt.step()
            else:
                nonfinite += 1
        else:
            loss = torch.zeros(())
        sched.step()
        sw = sw.detach()
        for b in range(cfg.batch):
            bad = (not ok[b]) or int(sw.active[b].sum()) == 0 or infos[b]["sink"] > cfg.replace_above \
                or int((sw.active[b] & sw.hatched[b]).sum()) > cfg.replace_n \
                or not bool(torch.isfinite(sw.s[b]).all())
            if bad:
                fresh = seed(T, world, gen)
                for a in em.ESwarm.FIELDS:
                    getattr(sw, a)[b] = getattr(fresh, a)[0]
        for j, pi in enumerate(idx.tolist()):
            for a in em.ESwarm.FIELDS:
                getattr(pool, a)[pi] = getattr(sw, a)[j]
        dt = time.time() - t0
        if step % cfg.log_every == 0:
            ns = [int((sw.active[b] & sw.hatched[b]).sum()) for b in range(cfg.batch)]
            D, lam = rule.chem_rates()
            rec = dict(step=step, loss=round(float(loss), 3), sec=round(dt, 1), pre=pre, struck=struck, nf=nonfinite,
                       sink=[round(i["sink"], 2) for i in infos], n=ns, frame=[i["frame"] for i in infos],
                       deaths=int(sw.deaths.sum()), surv=round(sum(i.get("survive", 0) for i in infos), 4),
                       over=round(sum(i.get("over", 0) for i in infos), 3), smax=round(float(sw.s.abs().amax()), 1),
                       speed=infos[0].get("speed"), D=[round(float(v), 3) for v in D], lam=[round(float(v), 3) for v in lam],
                       fmax=round(float(sw.fld.amax()), 2), lr=round(sched.get_last_lr()[0], 6))
            log.write(json.dumps(rec) + "\n"); log.flush()
            print(f"{step:5d} {float(loss):7.3f} {dt:4.1f}s sink {' '.join(f'{v:5.1f}' for v in rec['sink'])} "
                  f"n {rec['n']} d{rec['deaths']} s{rec['smax']} f{rec['fmax']}", flush=True)
        if (step + 1) % cfg.snap_every == 0 or step == cfg.steps - 1:
            torch.save(dict(rule=rule.state_dict(), opt=opt.state_dict(), sched=sched.state_dict(), pool=pool,
                            step=step + 1, gen=gen.get_state()), ck + ".tmp")
            os.replace(ck + ".tmp", ck)
            em.save_rule(rule, os.path.join(cfg.run, f"rule_{step + 1:05d}.pt"), dict(step=step + 1, stage=cfg.stage))
    return rule


def main():
    ap = argparse.ArgumentParser()
    for f_, v in asdict(Cfg()).items():
        ap.add_argument("--" + f_.replace("_", "-"), type=type(v), default=v)
    a = ap.parse_args()
    train(Cfg(**{k: getattr(a, k) for k in asdict(Cfg())}))


if __name__ == "__main__":
    main()
