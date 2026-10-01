"""Two more player-facing measurements for the creature.

  switch_trace  the 12 fair-cull switches (swarm_eval.cull_to) scored every 10 steps: how many steps
                after the cull until the new majority's plan is closest (switch latency), and how long
                the body visibly SHIVERS (the tell) before that - is the tell a readable warning, or
                does it end before the body has re-formed?
  escort        a ship cruises past alongside the grown body (1.5 RMS radii off, 1.6 voxels/step, not
                heading at it) for 90 steps: how far the body's centroid travels WITH the ship
                (shell on vs off), i.e. does the whale follow you.

    python Tools/NCA/creature_extra.py [--out Tools/NCA/results/creature/extra.json]
"""
import argparse
import json
import os
import sys

import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402
import swarm_eval as se  # noqa: E402
import creature_model as cm  # noqa: E402
import creature_probe as cp  # noqa: E402


@torch.no_grad()
def switch_trace(seed=7, steps=240, after=240, L=None):
    L = L or sn.LossCfg()
    T = sn.load_targets()
    out = {}
    for k in sn.KINDS:
        for e in range(4):
            to = sn.PLAN_OF[e]
            if to == k:
                continue
            m = cm.CreatureRule()
            gen = sn.make_gen(seed)
            sw = sn.seed_swarm([T[k]], m.world, gen)
            for _ in range(steps):
                sw = m(sw, gen)
            if not se.cull_to(sw, 0, e, gen):
                out[f"{k}->{to}"] = None
                continue
            latency, tell_steps, trace = None, 0, []
            for t in range(after + 1):
                if t % 10 == 0:
                    x = sn.decode(sw, 0)
                    row = {k2: sn.swarm_loss(x, T[k2], L)[1]["sink"] for k2 in sn.KINDS}
                    best = min(row, key=row.get)
                    trace.append(best[:2])
                    if latency is None and best == to:
                        latency = t
                if t < after:
                    sw = m(sw, gen)
                    if float(m._st["tell"][0]) > 0:
                        tell_steps += 1
            out[f"{k}->{to}"] = dict(latency=latency, tell_steps=tell_steps, final=trace[-1], trace="".join(c[0] for c in trace))
            print(k, "->", to, out[f"{k}->{to}"], flush=True)
    return out


@torch.no_grad()
def _make(body_kind, **cfg):
    if body_kind == "field":
        import creature_field as cf
        return cf.CreatureField(**cfg)
    return cm.CreatureRule()


def escort(seed=5, steps=240, body_kind="evo"):
    import copy
    T = sn.load_targets()
    out = {}
    for k in sn.KINDS:
        m = _make(body_kind)
        gen = sn.make_gen(seed)
        sw = sn.seed_swarm([T[k]], m.world, gen)
        for _ in range(steps):
            sw = m(sw, gen)
        res = {}
        for on in (True, False):
            mm = _make(body_kind); mm._st = copy.deepcopy(m._st)
            if hasattr(m, "mem"):
                mm.mem = copy.deepcopy(m.mem)
            mm.shell = on
            s2 = sw.clone(); g2 = sn.make_gen(seed + 9)
            _, c0, rms = cp.body(s2)
            c0 = c0.numpy(); dvec = np.array([1.0, 0.0, 0.0]); side = np.array([0.0, 0.0, 1.5 * rms])
            for i in range(90):
                pc = c0 + side + dvec * 1.6 * (i - 45)
                mm.vessels = [(pc, 0.35 * rms, dvec * 1.6)]
                s2 = mm(s2, g2)
            mm.vessels = []
            _, c1, _ = cp.body(s2)
            res["on" if on else "off"] = round(float((c1.numpy() - c0) @ dvec), 2)
        res["ship_travel"] = 144.0
        out[k] = res
        print(k, res, flush=True)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default="")
    ap.add_argument("--gradual", action="store_true")
    ap.add_argument("--field-escort", action="store_true")
    ap.add_argument("--gradual-field", action="store_true")
    ap.add_argument("--eater", action="store_true")
    a = ap.parse_args()
    if a.gradual or a.gradual_field or a.eater:
        return
    if "--field-escort" in sys.argv:
        json.dump(escort(body_kind="field"), open(os.path.join(HERE, "runs", "creature", "field_escort.json"), "w"), indent=1)
        return
    torch.set_num_threads(4)
    res = dict(escort=escort(), switch=switch_trace())
    if a.out:
        json.dump(res, open(a.out, "w"), indent=1)


if __name__ == "__main__":
    main()


@torch.no_grad()
def eater(seed=7, steps=240, rate=1, max_steps=400, body_kind="field", L=None, lead=0.0, **cfg):
    """Gradual predation WITH the eater present: a ship hovers 0.8 RMS radii off the body (so the swarm is afraid)
    and eats `rate` majority tadpoles per step (nearest to it first) until another element leads, then leaves.
    Reports flip time and whether the new plan is closest 150 steps later."""
    L = L or sn.LossCfg()
    T = sn.load_targets()
    out = {}
    for k in sn.KINDS:
        m = _make(body_kind, **cfg)
        gen = sn.make_gen(seed)
        sw = sn.seed_swarm([T[k]], m.world, gen)
        for _ in range(steps):
            sw = m(sw, gen)
        maj0 = sn.MAJOR[k]
        flip = warn = None
        eaten = 0
        for t in range(max_steps):
            al = sw.active[0] & sw.hatched[0]
            cnt = torch.bincount(sw.elem[0][al], minlength=4)
            if flip is None and int(cnt.argmax()) != maj0:
                flip = t
            top2 = cnt.sort(descending=True).values
            feeding = flip is None or (int(cnt.argmax()) != maj0 and int(top2[0] - cnt[maj0]) < max(2, lead * float(cnt.sum())))
            if feeding and t < max_steps - 160:
                p = sw.pos[0][al]; c = p.mean(0); rms = float(((p - c) ** 2).sum(-1).mean().sqrt())
                ship = (c + torch.tensor([0.8 * rms, 0.0, 0.0])).numpy()
                m.vessels = [(ship, 0.4 * rms, np.zeros(3))]
                idx = (al & (sw.elem[0] == maj0)).nonzero().squeeze(1)
                dd = (sw.pos[0][idx] - torch.as_tensor(ship, dtype=torch.float32)).norm(dim=-1)
                kill = idx[dd.argsort()[:rate]]
                sw.active[0, kill] = False; sw.hatched[0, kill] = False; sw.s[0, kill] = 0.0
                eaten += len(kill)
            else:
                m.vessels = []
            sw = m(sw, gen)
            if warn is None and m.flags is not None and float(torch.as_tensor(m.flags["tell"]).reshape(-1)[0]) > 0.05:
                warn = t
            if flip is not None and t >= flip + 150:
                break
        m.vessels = []
        x = sn.decode(sw, 0)
        row = {k2: round(sn.swarm_loss(x, T[k2], L)[1]["sink"], 2) for k2 in sn.KINDS}
        al = sw.active[0] & sw.hatched[0]
        out[k] = dict(flip_at=flip, eaten=eaten, warn_at=warn, closest_after=min(row, key=row.get), row=row,
                      majority_after=sn.ELEMENTS[int(torch.bincount(sw.elem[0][al], minlength=4).argmax())], n_after=int(al.sum()))
        print("eater", body_kind, cfg, k, {kk: v for kk, v in out[k].items() if kk != "row"}, flush=True)
    return out


@torch.no_grad()
def gradual(seed=7, steps=240, rate=1, every=2, max_steps=400, L=None, body_kind="evo"):
    """Predators eat `rate` tadpoles of the majority element every `every` steps until another element
    leads (then stop). Reports when the pre-tell started, when the majority flipped, and whether the
    new plan is closest 120 steps later: is there a readable WARNING before the switch?"""
    L = L or sn.LossCfg()
    T = sn.load_targets()
    out = {}
    for k in sn.KINDS:
        m = _make(body_kind)
        gen = sn.make_gen(seed)
        sw = sn.seed_swarm([T[k]], m.world, gen)
        for _ in range(steps):
            sw = m(sw, gen)
        maj0 = sn.MAJOR[k]
        warn = flip = None
        for t in range(max_steps):
            al = sw.active[0] & sw.hatched[0]
            cnt = torch.bincount(sw.elem[0][al], minlength=4)
            if flip is None and int(cnt.argmax()) != maj0:
                flip = t
            if flip is None and t % every == 0:
                idx = (al & (sw.elem[0] == maj0)).nonzero().squeeze(1)
                kill = idx[torch.randperm(len(idx), generator=gen)[:rate]]
                sw.active[0, kill] = False; sw.hatched[0, kill] = False; sw.s[0, kill] = 0.0
            sw = m(sw, gen)
            if warn is None and float(m.flags["tell"][0]) > 0.05:
                warn = t
            if flip is not None and t >= flip + 120:
                break
        x = sn.decode(sw, 0)
        row = {k2: round(sn.swarm_loss(x, T[k2], L)[1]["sink"], 2) for k2 in sn.KINDS}
        out[k] = dict(warn_at=warn, flip_at=flip, warning_steps=None if (warn is None or flip is None) else flip - warn,
                      closest_after=min(row, key=row.get), new_majority=None if flip is None else sn.ELEMENTS[int(torch.bincount(
                          sw.elem[0][sw.active[0] & sw.hatched[0]], minlength=4).argmax())])
        print("gradual", k, out[k], flush=True)
    return out


if __name__ == "__main__" and "--eater" in sys.argv:
    torch.set_num_threads(4)
    res = {}
    for fear in (0.0, 4.0):
        for r in (1, 2):
            res[f"fear{fear}_rate{r}"] = eater(rate=r, fear=fear)
    for r in (1, 2):
        res[f"fear4.0_rate{r}_lead0.1"] = eater(rate=r, fear=4.0, lead=0.1)
    json.dump(res, open(os.path.join(HERE, "runs", "creature", "field_eater.json"), "w"), indent=1)
elif __name__ == "__main__" and "--gradual-field" in sys.argv:
    torch.set_num_threads(4)
    json.dump({f"rate{r}": gradual(rate=r, every=1, body_kind="field") for r in (1, 2, 4)},
              open(os.path.join(HERE, "runs", "creature", "field_gradual.json"), "w"), indent=1)
elif __name__ == "__main__" and "--gradual" in sys.argv:
    torch.set_num_threads(4)
    json.dump({f"rate{r}": gradual(rate=r, every=1) for r in (2, 4)}, open(os.path.join(HERE, "runs", "creature", "gradual.json"), "w"), indent=1)
