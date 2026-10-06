"""The yardstick for species 3 (the pure emergent swimmer): ONE animated target, scored on seven axes.

  SHAPE      swarm_nca's divergence (default LossCfg) between the grown swarm and the target frame at the
             swarm's best-matching phase, averaged over the cycle. Two readings:
               strict  - ONE phase line for the whole window: frame(t) = k0 + t / tpf (mod 8), with the start
                         k0 and the tempo tpf (steps per frame) fitted, and the cycle no longer than the
                         window. A body that holds one pose scores what holding that pose costs over a cycle.
                         THIS is the headline.
               lenient - each checkpoint against its own best frame (it ignores whether the body animates).
             Also: `static` = the best single frame held for the whole window (what a body that never moves
             would score against the cycle) and `anim_gain` = static - strict (> 0: the animation is real).
             Bars: the four-plan loss-8 bar, and posinfo2's own-plan losses 1.6-2.7.
  TEMPO      steps per animation cycle (8 x the fitted tpf), per seed, and its spread across seeds; also the
             slope of the unwrapped per-checkpoint best frame (model-free cross-check, particle_nca style).
  GROWTH     from the 16-tadpole seed with no script: headcount and shape loss over time, steps to 90% of the
             final headcount.
  REGROWTH   swarm_probe.strike (a sphere of one RMS radius, ~a third removed), then 160 steps: before / cut /
             recovered (best-frame loss as swarm_probe scores it), heal fraction, steps to heal half / 90%.
  LOSSLESS   self-inflicted deaths per 1k tadpole-steps (scorecard.py's definition: a slot alive and hatched at t
             and inactive at t+1, or a rise of the deaths counter), over grow + window + regrow (the strike
             itself excluded). LOSSLESS = 0.
  PERFORMANT ms/step for one grown swarm, batch 1, 1 thread.
  EMERGENT   swarm_feel.metrics on the grown body + the organic band (swarm_feel.BAND) for this plan alone,
             next to results/hgrid2/feel_calibration.json and results/posinfo2/feel.json; locality declared.

    python Tools/NCA/emergent_eval.py --rule Tools/NCA/results/emergent/rule.pt --seeds 7,23,41,1000 \
        --out Tools/NCA/results/emergent/eval.json
"""
from __future__ import annotations

import argparse
import json
import math
import os
import time

import numpy as np
import torch

import emergent_model as em
import swarm_feel as sf
import swarm_nca as sn
import swarm_probe as sp

HERE = os.path.dirname(os.path.abspath(__file__))
FOUR_PLAN_BAR = sn.MAX_TEST_LOSS
POSINFO2_OWN = [1.65, 2.72]


def _live(sw):
    return sw.active[0] & sw.hatched[0]


class Counter:
    """Steps a model, counting self-inflicted deaths (scorecard's definition) and wall time."""

    def __init__(self, model):
        self.model, self.deaths, self.tsteps, self.sec, self.steps = model, 0, 0, 0.0, 0

    @torch.no_grad()
    def __call__(self, sw, gen):
        before, d0 = _live(sw).clone(), sw.deaths.clone()
        t = time.perf_counter()
        sw = self.model(sw, gen)
        self.sec += time.perf_counter() - t
        self.steps += 1
        self.tsteps += int(before.sum())
        gone = int((before & ~sw.active[0]).sum())
        self.deaths += max(gone, int((sw.deaths - d0).clamp(min=0).sum()))
        return sw


def frame_table(x, T, L):
    """Divergence of one decoded swarm to every frame of T: [K]."""
    if float(x["w"].sum()) < 1e-3:
        return np.full(len(T.frames), 100.0)
    return np.array([sn.swarm_loss(x, T, L, frames=[k])[1]["sink"] for k in range(len(T.frames))])


def fit_phase(D, every, max_cycle):
    """D [n_ck, K]: best constant-tempo phase line. Returns (mean loss, tpf, k0)."""
    n, K = D.shape
    t = np.arange(n) * every
    best = (1e9, None, None)
    for tpf in np.arange(2.0, max_cycle / K + 1e-9, 0.125):
        for k0 in np.arange(0, K, 0.125):
            fr = np.floor(k0 + t / tpf + 0.5).astype(int) % K
            v = D[np.arange(n), fr].mean()
            if v < best[0]:
                best = (float(v), float(tpf), float(k0))
    return best


def phase_slope(D, every):
    K = D.shape[1]
    ph = np.unwrap(D.argmin(1) * 2 * np.pi / K) * K / (2 * np.pi)
    s = np.polyfit(np.arange(len(ph)) * every, ph, 1)[0]
    return float(K / s) if abs(s) > 1e-6 else float("inf")


@torch.no_grad()
def run_seed(model, plan="space", seed=7, grow=300, window=192, every=4, regrow=160, L=None):
    L = L or sn.LossCfg()
    T = sn.load_targets()[plan]
    gen = sn.make_gen(seed)
    cnt = Counter(model)
    sw = sn.seed_swarm([T], model.world, gen)
    growth = []
    for t in range(grow):
        sw = cnt(sw, gen)
        if (t + 1) % 20 == 0:
            growth.append(dict(step=t + 1, n=int(_live(sw).sum()),
                               loss=round(sn.swarm_loss(sn.decode(sw, 0), T, L)[1]["sink"], 2)))
    # ---- the cycle window
    D, P, alive, elem = [], [], _live(sw).clone(), sw.elem[0].clone()
    for t in range(window + 1):
        if t % every == 0:
            D.append(frame_table(sn.decode(sw, 0), T, L))
        P.append(sw.pos[0].clone())
        if t < window:
            sw = cnt(sw, gen)
            alive &= _live(sw) & (sw.elem[0] == elem)
    D = np.stack(D)
    strict, tpf, k0 = fit_phase(D, every, window)
    static = float(D.mean(0).min())
    lenient = float(D.min(1).mean())
    feel = sf.metrics(torch.stack(P)[:, alive], elem[alive], plan)
    n_grown = int(_live(sw).sum())
    # ---- cost at the grown size: 32 steps, 1 thread (the caller sets threads)
    c2 = Counter(model)
    sw2 = sw.clone()
    for _ in range(32):
        sw2 = c2(sw2, gen)
    ms = 1000 * c2.sec / c2.steps
    # ---- regrowth after a vessel strike
    score = lambda s_: round(sn.swarm_loss(sn.decode(s_, 0), T, L)[1]["sink"], 2)
    before = score(sw)
    n_before = int(_live(sw).sum())
    killed = sp.strike(sw, gen=gen)
    cut = score(sw)
    traj = []
    for t in range(regrow):
        sw = cnt(sw, gen)
        if (t + 1) % 10 == 0:
            traj.append((t + 1, score(sw), int(_live(sw).sum())))
    rec = traj[-1][1]
    span = cut - before
    heal = (cut - rec) / span if span > 1e-6 else None

    def t_heal(frac):
        if span <= 1e-6:
            return 0
        for st, v, _ in traj:
            if (cut - v) / span >= frac:
                return st
        return None
    nf = [g["n"] for g in growth]
    t90 = next((g["step"] for g in growth if g["n"] >= 0.9 * nf[-1]), None)
    return dict(
        seed=seed, n_grown=n_grown,
        shape=dict(strict=round(strict, 3), lenient=round(lenient, 3), static=round(static, 3),
                   anim_gain=round(static - strict, 3), frame_table_mean=[round(float(v), 2) for v in D.mean(0)]),
        tempo=dict(cycle_steps=round(8 * tpf, 1), steps_per_frame=tpf, k0=k0, slope_cycle=round(phase_slope(D, every), 1)),
        growth=dict(curve=growth, steps_to_90pct=t90, final_n=nf[-1], final_loss=growth[-1]["loss"]),
        regrowth=dict(before=before, n_before=n_before, killed=killed, cut=cut, recovered=rec, n_after=traj[-1][2],
                      heal=None if heal is None else round(heal, 3), steps_to_half=t_heal(0.5), steps_to_90pct=t_heal(0.9),
                      traj=traj),
        lossless=dict(deaths=cnt.deaths, tadpole_steps=cnt.tsteps,
                      per_1k=round(1000 * cnt.deaths / max(1, cnt.tsteps), 4)),
        ms_per_step=round(ms, 3), feel=feel)


def band(feel, plan="space"):
    b = sf.BAND
    pe = max(0.0, feel.get("planar_frac", 0) - feel.get("planar_plan", 0))
    checks = dict(jerk_rel=b["jerk_rel"][0] <= feel["jerk_rel"] <= b["jerk_rel"][1], osc=feel["osc"] <= b["osc_max"],
                  stuck=feel["stuck"] <= b["stuck_max"], planar=pe <= b["planar_excess_max"])
    return all(checks.values()), checks, round(pe, 3)


def evaluate(model, seeds=(7, 23, 41, 1000), plan="space", locality="local", log=print, **kw):
    torch.set_num_threads(1)
    per = {}
    for s in seeds:
        t0 = time.time()
        per[str(s)] = r = run_seed(model, plan, s, **kw)
        log(f"seed {s}: shape strict {r['shape']['strict']} (lenient {r['shape']['lenient']}, static {r['shape']['static']}) "
            f"cycle {r['tempo']['cycle_steps']} n {r['n_grown']} heal {r['regrowth']['heal']} deaths {r['lossless']['deaths']} "
            f"{r['ms_per_step']} ms  ({time.time() - t0:.0f}s)")
    sv = lambda f: [f(per[str(s)]) for s in seeds]
    strict = sv(lambda r: r["shape"]["strict"])
    cyc = sv(lambda r: r["tempo"]["cycle_steps"])
    deaths = sum(sv(lambda r: r["lossless"]["deaths"])); ts = sum(sv(lambda r: r["lossless"]["tadpole_steps"]))
    keys = ["speed", "jerk", "jerk_rel", "planar_frac", "planar_plan", "coherence", "jitter", "phase", "stuck", "osc"]
    fm = {k: round(float(np.mean(sv(lambda r: r["feel"].get(k, 0)))), 3) for k in keys}
    ok, checks, pe = band(fm, plan)
    heal = [h for h in sv(lambda r: r["regrowth"]["heal"]) if h is not None]
    out = dict(
        plan=plan, target=sn.load_targets()[plan].name, seeds=list(seeds), locality=locality,
        shape=dict(strict_mean=round(float(np.mean(strict)), 3), strict_per_seed=strict,
                   lenient_mean=round(float(np.mean(sv(lambda r: r["shape"]["lenient"]))), 3),
                   static_mean=round(float(np.mean(sv(lambda r: r["shape"]["static"]))), 3),
                   anim_gain_mean=round(float(np.mean(sv(lambda r: r["shape"]["anim_gain"]))), 3),
                   four_plan_bar=FOUR_PLAN_BAR, under_bar=all(v <= FOUR_PLAN_BAR for v in strict),
                   posinfo2_own_range=POSINFO2_OWN),
        tempo=dict(cycle_steps=cyc, mean=round(float(np.mean(cyc)), 1), cv=round(float(np.std(cyc) / max(1e-6, np.mean(cyc))), 3),
                   trained_cycle=64),
        growth=dict(final_n=sv(lambda r: r["growth"]["final_n"]), steps_to_90pct=sv(lambda r: r["growth"]["steps_to_90pct"]),
                    target_n=sn.load_targets()[plan].n),
        regrowth=dict(heal=sv(lambda r: r["regrowth"]["heal"]), heal_mean=round(float(np.mean(heal)), 3) if heal else None,
                      steps_to_half=sv(lambda r: r["regrowth"]["steps_to_half"]),
                      steps_to_90pct=sv(lambda r: r["regrowth"]["steps_to_90pct"]),
                      before=sv(lambda r: r["regrowth"]["before"]), cut=sv(lambda r: r["regrowth"]["cut"]),
                      recovered=sv(lambda r: r["regrowth"]["recovered"])),
        lossless=dict(deaths=deaths, tadpole_steps=ts, per_1k=round(1000 * deaths / max(1, ts), 4), lossless=deaths == 0),
        performant=dict(ms_per_step=round(float(np.mean(sv(lambda r: r["ms_per_step"]))), 3),
                        grown_n=round(float(np.mean(sv(lambda r: r["n_grown"]))), 1), threads=1),
        emergent=dict(in_band=ok, checks=checks, planar_excess=pe, mean=fm, locality=locality),
        per_seed=per)
    out["headline"] = (f"shape {out['shape']['strict_mean']} (bar {FOUR_PLAN_BAR}, posinfo2 own {POSINFO2_OWN[0]}-{POSINFO2_OWN[1]}; "
                       f"static {out['shape']['static_mean']}) | cycle {out['tempo']['mean']} steps cv {out['tempo']['cv']} | "
                       f"grown n {out['growth']['final_n']} | heal {out['regrowth']['heal_mean']} | "
                       f"deaths {out['lossless']['per_1k']}/1k | {out['performant']['ms_per_step']} ms | organic {ok} | {locality}")
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--rule", required=True)
    ap.add_argument("--seeds", default="7,23,41,1000")
    ap.add_argument("--locality", default="local")
    ap.add_argument("--grow", type=int, default=300)
    ap.add_argument("--out", default="")
    a = ap.parse_args()
    model = em.load_rule(a.rule)
    model.eval()
    res = evaluate(model, [int(s) for s in a.seeds.split(",")], locality=a.locality, grow=a.grow)
    res["rule"] = os.path.relpath(a.rule, HERE)
    print(res["headline"])
    if a.out:
        json.dump(res, open(a.out, "w"), indent=1)


if __name__ == "__main__":
    main()
