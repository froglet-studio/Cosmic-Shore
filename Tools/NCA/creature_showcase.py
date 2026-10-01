"""Scripted lives of the creature (one per body plan), recorded two ways:

  results/creature/rollout.json   swarm_nca.pack format (15-column units every EVERY steps, so the
                                  existing viewer can play it) plus extra keys per plan: `vessel`
                                  (per frame [x, y, z, radius] or null), `startle` / `flags` (per frame
                                  base64 int8 per unit), `events`, `crystal_track`.
  results/creature/showcase.html  a self-contained canvas page: orbit, scrub, read the events.

Each life: grow from the 16-tadpole seed; a vessel flies straight through (the school parts ahead of
it, the pufferfish inflates and spikes, the jellyfish jets away); the pilot circles slowly (the
dragonfly's Time units mob it); the pilot RAMS through (tadpoles inside die, each leaving a lime
crystal that drifts to the nearby ship and is collected); the wound heals from its edges; predators
eat the majority element (swarm_eval.cull_to), the body SHIVERS (the tell) and re-forms as the new
majority's plan.

    python Tools/NCA/creature_showcase.py [--set key=value ...]
"""
import argparse
import base64
import json
import math
import os
import sys

import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402
import swarm_eval as se  # noqa: E402
import creature_model as cm  # noqa: E402

EVERY = 4
NAMES = {"mass": "Whale (Mass)", "space": "Jellyfish (Space)", "charge": "Pufferfish (Charge)", "time": "Dragonfly (Time)"}
# the switch each life shows: element -> the plan it becomes
SHOW_SWITCH = {"mass": 2, "space": 0, "charge": 3, "time": 1}
OUT = os.path.join(HERE, "results", "creature")


def body(sw):
    al = sw.active[0] & sw.hatched[0]
    p = sw.pos[0][al]
    c = p.mean(0)
    return al, c.numpy(), float(((p - c) ** 2).sum(-1).mean().sqrt())


@torch.no_grad()
def life(kind, cfg=None, seed=3, body_kind="evo"):
    T = sn.load_targets()
    if body_kind == "field":
        import creature_field as cf
        model = cf.CreatureField(**(cfg or {}))
    else:
        model = cm.CreatureRule(**(cfg or {}))
    gen = sn.make_gen(seed)
    sw = sn.seed_swarm([T[kind]], model.world, gen)
    units, ns, show, vessel, flags, events, crystals = [], [], [], [], [], [], []
    live_cr = []           # [x, y, z, elem, born, vx, vy, vz]
    collected = [0]
    rammed = [0]
    t = [0]
    ship = [None]

    def rec():
        if t[0] % EVERY:
            return
        x = sn.decode(sw, 0)
        vis = x["hatched"]
        tier = x["tier"].argmax(1)
        u = torch.cat([x["p"], x["elem"][:, None].float(), x["dom"][:, None].float(), x["h"], tier[:, None].float(),
                       x["f"], x["sp"], x["w"][:, None]], 1)[vis]
        units.append(u.numpy()); ns.append(int(vis.sum()))
        idx = x["idx"][vis]
        fl = getattr(model, "flags", None)
        if fl is None:
            st = np.zeros(len(idx)); dg = np.zeros(len(idx)); mb = np.zeros(len(idx)); tl = 0.0
        else:
            st = fl["startle"][0][idx].numpy(); dg = fl["danger"][0][idx].numpy(); mb = fl["mob"][0][idx].numpy()
            tl = float(fl["tell"][0]) if torch.is_tensor(fl["tell"]) else 0.0
        age = np.minimum(sw.s[0][idx][:, 0].numpy() * 0 + 30, 30)
        flags.append(dict(st=np.round(st * 100).astype(np.int8), dg=dg.astype(np.int8), mb=mb.astype(np.int8), tell=round(tl, 2)))
        vessel.append(None if ship[0] is None else [round(float(v), 2) for v in ship[0][0]] + [round(float(ship[0][1]), 2)])
        crystals.append([[round(c[0], 1), round(c[1], 1), round(c[2], 1), int(c[3])] for c in live_cr])

    def crystal_step():
        # dead tadpoles' crystals: drift slowly; near a ship they are drawn to it and collected
        keep = []
        for c in live_cr:
            p = np.array(c[:3]); v = np.array(c[5:8]) * 0.92
            if ship[0] is not None:
                sc, sr = np.asarray(ship[0][0]), ship[0][1]
                d = sc - p; dist = np.linalg.norm(d)
                if dist < sr * 0.8:
                    collected[0] += 1
                    continue
                if dist < 4.0 * sr:
                    v = v + d / max(dist, 1e-3) * 0.35 * (1 - dist / (4.0 * sr) + 0.3)
            p = p + v
            keep.append([*p.tolist(), c[3], c[4], *v.tolist()])
        live_cr[:] = keep

    def step(n, path=None, kill=False):
        nonlocal sw
        for i in range(n):
            al, c, rms = body(sw)
            pr = path(i, c, rms) if path else None
            ship[0] = pr
            model.vessels = [] if pr is None else [pr]
            if pr is not None and kill:
                pc = torch.as_tensor(pr[0], dtype=torch.float32)
                ins = al & ((sw.pos[0] - pc).norm(dim=-1) < pr[1])
                for j in ins.nonzero().squeeze(1).tolist():
                    p = sw.pos[0, j].numpy()
                    live_cr.append([*p.tolist(), int(sw.elem[0, j]), t[0], *(np.random.default_rng(j).normal(size=3) * 0.3).tolist()])
                sw.active[0, ins] = False; sw.hatched[0, ins] = False; sw.s[0, ins] = 0.0
                rammed[0] += int(ins.sum())
            rec()
            before = sw.active[0] & sw.hatched[0]
            p0, e0 = sw.pos[0].clone(), sw.elem[0].clone()
            sw = model(sw, gen)
            died = before & ~sw.active[0]          # its own death (the rule's death channel): a crystal
            for j in died.nonzero().squeeze(1).tolist():
                live_cr.append([*p0[j].tolist(), int(e0[j]), t[0], 0.0, 0.0, 0.0])
            crystal_step()
            t[0] += 1
        ship[0] = None
        model.vessels = []

    events.append([0, "seed: 16 tadpoles, element mix of the " + NAMES[kind].split()[0].lower()])
    step(200)
    al, c, rms = body(sw)
    events.append([t[0], f"grown: {int(al.sum())} tadpoles"])
    # 1. fly-by
    d = np.array([0.8, 0.25, 0.55]); d /= np.linalg.norm(d)
    rad = 0.6 * rms
    start = c - d * (2.5 * rms + rad); span = int(2 * (2.5 * rms + rad) / 3.0)
    events.append([t[0], "a vessel flies straight through - the school parts ahead of it"])
    step(span, lambda i, c_, r_: (start + d * 3.0 * i, rad, d * 3.0))
    step(50)
    # 2. circling pilot
    al, c, rms = body(sw)
    R = 1.6 * rms
    events.append([t[0], "the pilot circles slowly" + (" - Time units mob the ship" if kind == "time" else "")])

    def circ(i, c_, r_):
        a = 0.8 / R * i
        return (c + R * np.array([math.cos(a), 0.15 * math.sin(2 * a), math.sin(a)]), 0.4 * rms,
                0.8 * np.array([-math.sin(a), 0.0, math.cos(a)]))
    step(110, circ)
    step(40)
    # 2b. cruise alongside: the whale follows, the dragonfly zips along, the others let it go
    al, c, rms = body(sw)
    side = np.array([0.0, 0.0, 1.0]) * 1.5 * rms
    events.append([t[0], "the pilot cruises past alongside" + {"mass": " - the whale turns and follows", "time": " - the dragonfly tags along"}.get(kind, "")])
    step(90, lambda i, c_, r_: (c + side + np.array([1.6, 0, 0]) * (i - 45), 0.35 * rms, np.array([1.6, 0.0, 0.0])))
    step(40)
    # 3. ram: tadpoles inside the ship die and leave crystals the ship then collects
    al, c, rms = body(sw)
    d2 = np.array([-0.6, -0.15, 0.78]); d2 /= np.linalg.norm(d2)
    rad = 0.55 * rms
    start = c - d2 * (2.2 * rms + rad); span = int(2 * (2.2 * rms + rad) / 2.0)
    n0 = int(al.sum())
    events.append([t[0], "RAM: the vessel rams through at speed - every tadpole it hits dies and leaves a crystal"])
    step(span, lambda i, c_, r_: (start + d2 * 2.0 * i, rad, d2 * 2.0), kill=True)
    events.append([t[0], f"{rammed[0]} tadpoles killed; the ship turns back for the crystals; the wound fills from its edges"])
    end = start + d2 * 2.0 * span

    def back(i, c_, r_):
        p = end - d2 * 0.9 * i
        return (p, rad, -d2 * 0.9)
    step(40, back)
    step(110)
    # 4. predators eat the majority: the body shivers (the tell) and re-forms as the new plan
    to = SHOW_SWITCH[kind]
    al, c, rms = body(sw)
    n0 = int(al.sum())
    se.cull_to(sw, 0, to, gen)
    events.append([t[0], f"predators eat until {sn.ELEMENTS[to]} is the majority ({n0 - int(body(sw)[0].sum())} eaten) - "
                         f"the body shivers, then re-forms as the {NAMES[sn.PLAN_OF[to]].split()[0].lower()}"])
    step(300)
    x = sn.decode(sw, 0)
    row = {k: round(sn.swarm_loss(x, T[k], sn.LossCfg())[1]["sink"], 2) for k in sn.KINDS}
    events.append([t[0], f"end: closest plan {min(row, key=row.get)} ({row})"])
    nmax = max(ns + [1])
    arr = np.zeros((len(units), nmax, 15), np.float32)
    for i, f in enumerate(units):
        arr[i, :len(f)] = f
    return dict(frames=arr, n=ns, crystals=[], switched_at=None, vessel=vessel, flags=flags, events=events,
                crystal_track=crystals, collected=collected[0], end_row=row)


def pack_extra(data):
    out = sn.pack({k: dict(frames=v["frames"], n=v["n"], crystals=v["crystals"], switched_at=v["switched_at"]) for k, v in data.items()},
                  steps=EVERY)
    for k, v in data.items():
        F = len(v["flags"]); N = v["frames"].shape[1]
        fl = np.zeros((F, N, 3), np.int8)
        for i, f in enumerate(v["flags"]):
            m = len(f["st"])
            fl[i, :m, 0] = f["st"]; fl[i, :m, 1] = f["dg"]; fl[i, :m, 2] = f["mb"]
        out[k].update(every=EVERY, vessel=v["vessel"], flags=dict(shape=list(fl.shape), b64=base64.b64encode(fl.tobytes()).decode(),
                                                                  cols=["startle_x100", "danger", "mob"]),
                      tell=[f["tell"] for f in v["flags"]], events=v["events"], crystal_track=v["crystal_track"],
                      collected=v["collected"], end_row=v["end_row"])
    out["note"] = "creature lives: pack() frames every `every` steps + vessel track, per-unit flags, crystal positions"
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--set", action="append", default=[])
    ap.add_argument("--seed", type=int, default=3)
    ap.add_argument("--body", default="evo", choices=["evo", "field"])
    a = ap.parse_args()
    suf = "" if a.body == "evo" else "_" + a.body
    torch.set_num_threads(4)
    cfg = {kv.split("=", 1)[0]: json.loads(kv.split("=", 1)[1]) for kv in a.set}
    os.makedirs(OUT, exist_ok=True)
    data = {}
    for k in sn.KINDS:
        data[k] = life(k, cfg, a.seed, a.body)
        print(k, data[k]["frames"].shape, "collected", data[k]["collected"], data[k]["events"][-1][1], flush=True)
    packed = pack_extra(data)
    json.dump(packed, open(os.path.join(OUT, f"rollout{suf}.json"), "w"))
    page = open(os.path.join(HERE, "creature_showcase.html.tpl")).read()
    payload = {}
    for k in sn.KINDS:
        # the page needs 9 of the 15 packed columns: x y z elem dom tier facing(3)
        a = np.frombuffer(base64.b64decode(packed[k]["b64"]), "<i2").reshape(packed[k]["shape"])[..., [0, 1, 2, 3, 4, 8, 9, 10, 11]]
        payload[k] = {kk: packed[k][kk] for kk in ("n", "vessel", "flags", "tell", "events", "crystal_track", "every")}
        payload[k].update(shape=list(a.shape), b64=base64.b64encode(np.ascontiguousarray(a).tobytes()).decode())
    for k in sn.KINDS:
        payload[k]["name"] = NAMES[k]
    open(os.path.join(OUT, f"showcase{suf}.html"), "w").write(page.replace("/*DATA*/", json.dumps(payload)).replace("/*SCALE*/", json.dumps(packed["scale"])))
    print("wrote", OUT)


if __name__ == "__main__":
    main()
