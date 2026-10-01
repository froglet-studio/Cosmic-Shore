"""Interaction probe for the creature: what a player sees when a VESSEL (a moving sphere) meets it.

Every plan is grown 240 steps from the same seed, then four scripted encounters run on clones of that
grown swarm, each twice - shell ON (the creature) and shell OFF (react=False: the bare learned evo
body, which ignores the ship) - so every number has an inert baseline:

  flyby   the ship flies straight through the centroid at 3 voxels/step (2-4x a tadpole's top speed),
          radius 0.6 x the body's RMS radius. Kills nothing.
  ram     the same pass, but every tadpole inside the ship is killed (a ram). Dead tadpoles leave
          crystals; the body must heal.
  circle  the ship circles the body at 1.6 RMS radii, 0.8 voxels/step (a loitering pilot), 120 steps.
  chase   the ship chases the centroid at 1.2 voxels/step and hovers 0.8 RMS radii off it, 120 steps.

Metrics (per encounter):
  latency      steps from first contact (a live tadpole within sense range of the ship) until the
               tadpoles in that range move at >= 1.5x their pre-contact speed (centroid motion removed).
  touched      share of the school that was ever inside the ship (lower = the school parted).
  scatter      peak over the encounter of the mean displacement of each surviving tadpole from its
               pre-encounter place in the body frame, in RMS radii (how far the swarm scatters).
  reform       steps after the ship leaves until the own-plan score is back within 1.2x(+0.5) of before.
  readable     share of scored frames DURING the encounter at which the body is still strictly closest
               to its own plan (is it still a whale while you fly through it?).
  worst/after  own-plan divergence at its worst during, and 120 steps after.
  heal (ram)   (cut - recovered) / (cut - before), as swarm_probe.

    python Tools/NCA/creature_probe.py [--out Tools/NCA/results/creature/interaction.json]
"""
import argparse
import json
import math
import os
import sys
import time

import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402
import creature_model as cm  # noqa: E402

SENSE = 3.0


def body(sw):
    al = sw.active[0] & sw.hatched[0]
    p = sw.pos[0][al]
    c = p.mean(0)
    return al, c, float(((p - c) ** 2).sum(-1).mean().sqrt())


def score_row(sw, T, L):
    x = sn.decode(sw, 0)
    return {k: sn.swarm_loss(x, T[k], L)[1]["sink"] for k in sn.KINDS}


def encounter(model, sw, gen, kind, path, steps, kill=False, after=90, T=None, L=None, every=10):
    """path(t, centroid, rms) -> (centre np3, radius, velocity np3) or None. Returns metrics."""
    T = T or sn.load_targets(); L = L or sn.LossCfg()
    sw = sw.clone()
    al0, c0, rms0 = body(sw)
    rel0 = (sw.pos[0] - c0).clone()
    before = score_row(sw, T, L)[kind]
    n0 = int(al0.sum())
    touched = torch.zeros(sw.pos.shape[1], dtype=torch.bool)
    ever = al0.clone()
    contact, latency, base_speed = None, None, None
    prev_rel = rel0.clone()
    scatter, worst, readable, nread = 0.0, before, 0, 0
    crystals = 0
    left = None
    trace = []
    for t in range(steps + after):
        al, c, rms = body(sw)
        pr = path(t, c.numpy(), rms) if t < steps else None
        model.vessels = [] if pr is None else [pr]
        if pr is not None:
            pc = torch.as_tensor(pr[0], dtype=torch.float32)
            dd = (sw.pos[0] - pc).norm(dim=-1)
            ins = al & (dd < pr[1])
            touched |= ins
            if kill and bool(ins.any()):
                sw.active[0, ins] = False; sw.hatched[0, ins] = False; sw.s[0, ins] = 0.0
                crystals += int(ins.sum())
            near = al & (dd < SENSE * pr[1])
            if contact is None and bool(near.any()):
                contact = t
        rel = sw.pos[0] - sw.pos[0][al].mean(0)
        spd = (rel - prev_rel).norm(dim=-1)
        if pr is not None:
            near = al & ((sw.pos[0] - torch.as_tensor(pr[0], dtype=torch.float32)).norm(dim=-1) < SENSE * pr[1])
            if contact is None:
                base_speed = float(spd[al].mean())
            elif latency is None and bool(near.any()) and base_speed is not None and t > contact:
                if float(spd[near].mean()) >= 1.5 * max(base_speed, 0.05):
                    latency = t - contact
        if t == steps:
            left = t
        prev_rel = rel.clone()
        sw = model(sw, gen)
        keep = al0 & sw.active[0] & sw.hatched[0]
        if bool(keep.any()):
            al_, c_, _ = body(sw)
            disp = ((sw.pos[0] - c_) - rel0)[keep].norm(dim=-1).mean() / rms0
            if t < steps:
                scatter = max(scatter, float(disp))
        if t % every == 0:
            row = score_row(sw, T, L)
            trace.append(round(row[kind], 2))
            if t < steps:
                worst = max(worst, row[kind])
                nread += 1
                readable += int(row[kind] < min(v for k, v in row.items() if k != kind) - 1e-6)
    model.vessels = []
    after_row = score_row(sw, T, L)
    reform = None
    for i, s_ in enumerate(trace):
        if i * every >= steps and s_ <= before * 1.2 + 0.5:
            reform = i * every - steps
            break
    out = dict(before=round(before, 2), worst=round(worst, 2), after=round(after_row[kind], 2),
               touched=round(float(touched.sum()) / max(n0, 1), 3), scatter=round(scatter, 3),
               latency=latency, reform=reform, readable=round(readable / max(nread, 1), 2),
               n_after=int((sw.active[0] & sw.hatched[0]).sum()), still_own=min(after_row, key=after_row.get) == kind)
    if kill:
        out["killed"] = crystals
        cut = max(trace[: max(1, steps // every + 1)])
        span = cut - before
        out["cut"] = cut
        out["heal"] = round((cut - after_row[kind]) / span, 3) if span > 1e-6 else None
        out["trace"] = trace
    return out, sw


def flyby_path(seed, speed=3.0, frac=0.6):
    rng = np.random.default_rng(seed)
    d = rng.normal(size=3); d /= np.linalg.norm(d)
    st = {}

    def path(t, c, rms):
        if not st:
            rad = frac * rms
            st.update(rad=rad, start=c - d * (rms * 2.5 + rad), span=int(2 * (rms * 2.5 + rad) / speed))
        if t >= st["span"]:
            return None
        return (st["start"] + d * speed * t, st["rad"], d * speed)
    return path, st


def circle_path(seed, speed=0.8, frac=0.5, orbit=1.6):
    st = {}

    def path(t, c, rms):
        if not st:
            st.update(c=c.copy(), R=orbit * rms, rad=frac * rms)
        R = st["R"]; w = speed / R
        a = w * t
        p = st["c"] + R * np.array([math.cos(a), 0.15 * math.sin(2 * a), math.sin(a)])
        v = speed * np.array([-math.sin(a), 0.3 * math.cos(2 * a) * 0, math.cos(a)])
        return (p, st["rad"], v)
    return path, st


def chase_path(seed, speed=1.2, frac=0.5, hover=0.8):
    rng = np.random.default_rng(seed + 5)
    d = rng.normal(size=3); d /= np.linalg.norm(d)
    st = {}

    def path(t, c, rms):
        if not st:
            st.update(p=c + d * 3.0 * rms, rad=frac * rms)
        to = c - st["p"]; dist = np.linalg.norm(to)
        stop = hover * rms
        v = np.zeros(3) if dist <= stop else to / dist * min(speed, dist - stop)
        st["p"] = st["p"] + v
        return (st["p"].copy(), st["rad"], v)
    return path, st


def make_model(body, cfg):
    if body == "field":
        import creature_field as cf
        return cf.CreatureField(**(cfg or {}))
    return cm.CreatureRule(**(cfg or {}))


def _one(args):
    k, seed, steps, cfg, body = args
    torch.set_num_threads(1)
    return k, run_kind(k, seed, steps, cfg, body)


def run(seed=5, steps=240, cfg=None, kinds=sn.KINDS, log=print, body="evo"):
    from multiprocessing import Pool
    t0 = time.time()
    with Pool(len(kinds)) as pool:
        out = dict(pool.map(_one, [(k, seed, steps, cfg, body) for k in kinds]))
    for k in kinds:
        res = out[k]
        log(f"{k}: ({time.time() - t0:.0f}s)")
        for n in ("flyby", "ram", "circle", "chase"):
            a, b = res[n], res[n + "_inert"]
            log(f"   {n:6s} react/inert touched {a['touched']}/{b['touched']} scatter {a['scatter']}/{b['scatter']} lat {a['latency']}/{b['latency']} "
                f"worst {a['worst']}/{b['worst']} after {a['after']}/{b['after']} reform {a['reform']}/{b['reform']} read {a['readable']}/{b['readable']}"
                + (f" heal {a.get('heal')}/{b.get('heal')}" if 'killed' in a else ''))
    return out


@torch.no_grad()
def run_kind(k, seed=5, steps=240, cfg=None, body_kind="evo"):
    import copy
    T = sn.load_targets(); L = sn.LossCfg()
    if True:
        model = make_model(body_kind, cfg)
        gen = sn.make_gen(seed)
        sw = sn.seed_swarm([T[k]], model.world, gen)
        for _ in range(steps):
            sw = model(sw, gen)
        res = {}
        for name, mk, n, kill in (("flyby", flyby_path, None, False), ("ram", flyby_path, None, True),
                                  ("circle", circle_path, 120, False), ("chase", chase_path, 120, False)):
            for react in (True, False):
                model.react = react; model.shell = react
                path, st = mk(seed)
                path(0, body(sw)[1].numpy(), body(sw)[2])
                nsteps = n if n is not None else st["span"]
                path, st = mk(seed)
                state = copy.deepcopy(model._st)
                mem = copy.deepcopy(getattr(model, "mem", None))
                g2 = sn.make_gen(seed + 77)
                r, _ = encounter(model, sw, g2, k, path, nsteps, kill=kill, T=T, L=L)
                model._st = state
                if mem is not None:
                    model.mem = mem
                res[f"{name}{'' if react else '_inert'}"] = r
            model.react = True; model.shell = True
    return res


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--seed", type=int, default=5)
    ap.add_argument("--set", action="append", default=[])
    ap.add_argument("--out", default="")
    ap.add_argument("--body", default="evo", choices=["evo", "field"])
    a = ap.parse_args()
    cfg = {kv.split("=", 1)[0]: json.loads(kv.split("=", 1)[1]) for kv in a.set}
    res = run(a.seed, cfg=cfg, body=a.body)
    if a.out:
        json.dump(res, open(a.out, "w"), indent=1)


if __name__ == "__main__":
    main()
