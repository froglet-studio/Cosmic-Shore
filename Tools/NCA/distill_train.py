"""distill: behaviour cloning + DAgger of the field teacher into the local Student.

    python Tools/NCA/distill_train.py bc     --run runs/distill/bc      # stage 1: teacher-driven data, supervised
    python Tools/NCA/distill_train.py dagger --run runs/distill/dag --init runs/distill/bc/student.pt
    python Tools/NCA/distill_train.py eval   --ckpt runs/distill/dag/student.pt

Data: batches of episodes. Each episode grows one plan from a seed for 240 steps, then (usually) culls
to a random target element with swarm_eval.cull_to (the fair cull), sometimes strikes the body
(swarm_probe.strike), sometimes flies a ship through it, and runs 240 more steps. At every step the
teacher (distill_teacher.Labeler: FieldSwarm on a shadow of THIS swarm) labels every live tadpole;
the student's local features at that state are stored with the labels. In DAgger the swarm is advanced
by the student (per-sample coin: teacher with prob beta), so the data covers the student's own mistakes.
"""
from __future__ import annotations

import argparse
import json
import math
import os
import sys
import time

import numpy as np
import torch
import torch.nn.functional as F

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402
import swarm_eval as se  # noqa: E402
import swarm_probe  # noqa: E402
import distill_student as ds  # noqa: E402
import distill_teacher as dt  # noqa: E402

KEEP_NEG = 0.3


def episode_plan(rng, B):
    """Per sample: (kind, cull element or None, strike step or None, predator pass start or None)."""
    eps = []
    for b in range(B):
        k = sn.KINDS[b % 4]
        r = rng.random()
        cull = None if r < 0.15 else int(rng.integers(0, 4))
        strike = int(rng.integers(150, 420)) if rng.random() < 0.3 else None
        pred = int(rng.integers(120, 440)) if rng.random() < 0.3 else None
        eps.append((k, cull, strike, pred))
    return eps


def _pred_path(sw, b, rng):
    al = (sw.active[b] & sw.hatched[b])
    p = sw.pos[b][al].numpy()
    c = p.mean(0); rms = float(np.sqrt(((p - c) ** 2).sum(-1).mean()))
    rad = 0.6 * rms
    d = rng.normal(size=3); d /= np.linalg.norm(d)
    speed = 3.0
    start = c - d * (rms * 2.5 + rad)
    span = int(2 * (rms * 2.5 + rad) / speed)
    return dict(start=start, d=d, speed=speed, rad=rad, span=span)


@torch.no_grad()
def collect(student: ds.Student, beta: float, B: int, seed: int, grow=240, after=240, keep_neg=KEEP_NEG, log=print):
    """One batch of B episodes. Returns a dict of tensors (features + labels)."""
    rng = np.random.default_rng(seed)
    gen = sn.make_gen(seed)
    T = sn.load_targets()
    eps = episode_plan(rng, B)
    sw = sn.seed_swarm([T[e[0]] for e in eps], student.world, gen)
    lab = dt.Labeler()
    rows = {k: [] for k in ("f", "v", "look", "lay", "lay_w", "dir", "molt_ok", "molt", "molt_w", "mto", "pb", "stl", "plan_mask")}
    preds = {}
    zerr = []
    for t in range(grow + after):
        if t == grow:
            for b, (k, cull, _, _) in enumerate(eps):
                if cull is not None:
                    se.cull_to(sw, b, cull, gen)
        for b, (k, _, strike, pred) in enumerate(eps):
            if strike is not None and t == strike:
                one = sw.index(torch.tensor([b]))
                swarm_probe.strike(one, gen=gen)
                sw.active[b], sw.hatched[b], sw.s[b] = one.active[0], one.hatched[0], one.s[0]
            if pred is not None and t == pred:
                preds[b] = _pred_path(sw, b, rng)
        pl = {}
        for b, P in list(preds.items()):
            tt = t - eps[b][3]
            if tt >= P["span"]:
                del preds[b]; continue
            pc = P["start"] + P["d"] * P["speed"] * tt
            pl[b] = [(pc, P["rad"], P["d"] * P["speed"])]
        act = lab.label(sw, pl)
        student.predators = pl
        mix = torch.from_numpy(rng.random(B) < beta)
        sw2 = student.step(sw, gen, action=act, mix=mix)
        f = student._last_feats
        live = act["live"]
        # labels
        s_old = sw.s.reshape(-1, ds.C)
        idx = live.nonzero().squeeze(1)
        laypos = act["lay_par"][idx]
        keep = laypos | act["molt_start"][idx] | (torch.rand(len(idx), generator=gen) < keep_neg)
        idx = idx[keep]
        lp = act["lay_par"][idx].float()
        rows["f"].append(f[idx])
        rows["v"].append(act["v"][idx])
        rows["look"].append((act["look"][idx] - s_old[idx][:, ds.LOOK]) / ds.LOOK_SCALE)
        rows["lay"].append(lp)
        rows["lay_w"].append(torch.where(lp > 0, torch.ones_like(lp), torch.full_like(lp, 1 / keep_neg)))
        rows["dir"].append(act["lay_dir"][idx])
        mok = (s_old[idx, ds.MOLT] <= 0).float()
        ms = act["molt_start"][idx].float()
        rows["molt_ok"].append(mok)
        rows["molt"].append(ms)
        rows["molt_w"].append(torch.where(ms > 0, torch.ones_like(ms), torch.full_like(ms, 1 / keep_neg)))
        rows["mto"].append(act["molt_to"][idx])
        bidx = idx // sw.pos.shape[1]
        rows["pb"].append(act["plan"][bidx])
        rows["stl"].append(act["stl"][idx])
        rows["plan_mask"].append(torch.ones(len(idx)))
        # consensus quality (diagnostic): student's centre estimate vs the teacher's anchor
        if t % 40 == 39:
            Zs = sw2.s[:, :, ds.Z]
            for b in range(B):
                m = sw2.active[b] & sw2.hatched[b]
                if b in lab.T.mem and int(m.sum()):
                    an = torch.as_tensor(lab.T.mem[b]["anchor"])
                    zerr.append(float((Zs[b][m] - an).norm(dim=-1).mean()))
        sw = sw2
    data = {k: torch.cat(v) for k, v in rows.items()}
    fin = []
    for b in range(B):
        m = sw.active[b] & sw.hatched[b]
        fin.append((eps[b][0], eps[b][1], int(m.sum()), torch.bincount(sw.elem[b][m], minlength=4).tolist()))
    data["_meta"] = dict(zerr=float(np.mean(zerr)) if zerr else None, fin=fin)
    return data


def merge(dsets, cap):
    keys = [k for k in dsets[0] if not k.startswith("_")]
    out = {k: torch.cat([d[k] for d in dsets]) for k in keys}
    n = len(out["f"])
    if n > cap:
        sel = torch.randperm(n)[:cap]
        out = {k: v[sel] for k, v in out.items()}
    return out


def losses(student, batch, w):
    out = student.head(batch["f"])
    l_v = F.smooth_l1_loss(out[:, 0:3], batch["v"], beta=0.2)
    l_look = F.smooth_l1_loss(out[:, 3:14], batch["look"], beta=0.1)
    l_lay = (F.binary_cross_entropy_with_logits(out[:, 14], batch["lay"], reduction="none") * batch["lay_w"]).sum() / batch["lay_w"].sum()
    pos = batch["lay"] > 0
    l_dir = (1 - F.cosine_similarity(out[pos][:, 15:18], batch["dir"][pos], dim=-1)).mean() if pos.any() else out.sum() * 0
    mok = batch["molt_ok"] > 0
    l_molt = ((F.binary_cross_entropy_with_logits(out[:, 22], batch["molt"], reduction="none") * batch["molt_w"])[mok].sum()
              / batch["molt_w"][mok].sum().clamp(min=1))
    ms = batch["molt"] > 0
    l_mto = F.cross_entropy(out[ms][:, 23:27], batch["mto"][ms]) if ms.any() else out.sum() * 0
    l_pb = F.cross_entropy(out[:, 27:31], batch["pb"])
    l_stl = F.mse_loss(out[:, 31], batch["stl"])
    parts = dict(v=l_v, look=l_look, lay=l_lay, dir=l_dir, molt=l_molt, mto=l_mto, pb=l_pb, stl=l_stl)
    tot = sum(w.get(k, 1.0) * v for k, v in parts.items())
    return tot, {k: float(v) for k, v in parts.items()}


W_LOSS = dict(v=4.0, look=1.0, lay=2.0, dir=0.5, molt=2.0, mto=0.5, pb=0.5, stl=1.0)


def fit(student, data, epochs, lr=2e-3, bs=4096, log=print, wd=1e-5):
    n = len(data["f"])
    if float(student.sd.sum()) == student.nf:      # first fit: feature normalisation
        with torch.no_grad():
            student.mu.copy_(data["f"].mean(0)); student.sd.copy_(data["f"].std(0).clamp(min=1e-3))
    opt = torch.optim.AdamW(student.parameters(), lr=lr, weight_decay=wd)
    steps = epochs * max(1, n // bs)
    sch = torch.optim.lr_scheduler.OneCycleLR(opt, max_lr=lr, total_steps=steps, pct_start=0.1)
    it = 0
    for ep in range(epochs):
        perm = torch.randperm(n)
        acc = {}
        for i in range(0, n - bs + 1, bs):
            sel = perm[i:i + bs]
            batch = {k: v[sel] for k, v in data.items()}
            loss, parts = losses(student, batch, W_LOSS)
            opt.zero_grad(); loss.backward(); torch.nn.utils.clip_grad_norm_(student.parameters(), 1.0); opt.step()
            if it < steps - 1:
                sch.step()
            it += 1
            for k, v in parts.items():
                acc[k] = acc.get(k, 0) + v
        nb = max(1, n // bs)
        log(f"  epoch {ep}: " + " ".join(f"{k}={v / nb:.4f}" for k, v in acc.items()))


def save(student, path, extra=None):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    torch.save(dict(state=student.state_dict(), cfg=student.cfg.__dict__, extra=extra or {}), path)


def load(path):
    d = torch.load(path, weights_only=False)
    st = ds.Student(cfg=ds.StudentCfg(**d["cfg"]))
    st.load_state_dict(d["state"], strict=False)
    return st.eval()


def quick_eval(student, log=print, full=True, samples=3):
    if os.environ.get("DISTILL_LIGHT_EVAL"):
        full, samples = False, 1
    res = se.evaluate(student, full=full, samples=samples, log=log)
    log(se.matrix(res))
    log(f"PASSED {res['passed']}/{res['feasible']}  own {res['own_passed']} std {res['std_passed']} rest {res['rest_passed']}  {res['seconds']}s")
    return res


def main():
    torch.set_num_threads(4)
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=["bc", "dagger", "eval"])
    ap.add_argument("--run", default=os.path.join(HERE, "runs", "distill", "bc"))
    ap.add_argument("--init", default="")
    ap.add_argument("--ckpt", default="")
    ap.add_argument("--batches", type=int, default=6)
    ap.add_argument("--B", type=int, default=8)
    ap.add_argument("--epochs", type=int, default=8)
    ap.add_argument("--iters", type=int, default=6)
    ap.add_argument("--cap", type=int, default=3_000_000)
    ap.add_argument("--hidden", type=int, default=256)
    ap.add_argument("--seed", type=int, default=0)
    ap.add_argument("--plan_mode", default="")
    ap.add_argument("--genome", type=int, default=0)
    a = ap.parse_args()
    os.makedirs(a.run, exist_ok=True)
    logf = open(os.path.join(a.run, "log.txt"), "a")

    def log(*x):
        s = " ".join(str(v) for v in x)
        print(s, flush=True); logf.write(s + "\n"); logf.flush()

    if a.cmd == "eval":
        st = load(a.ckpt)
        res = quick_eval(st, log)
        json.dump(res, open(os.path.join(a.run, "eval16.json"), "w"), indent=1)
        return
    st = load(a.init) if a.init else ds.Student(cfg=ds.StudentCfg(hidden=a.hidden, genome=a.genome, plan_mode=a.plan_mode or "learned"))
    if a.plan_mode:
        st.cfg.plan_mode = a.plan_mode
    hist = json.load(open(os.path.join(a.run, "hist.json"))) if os.path.exists(os.path.join(a.run, "hist.json")) else []
    dpath = os.path.join(a.run, "data.pt")
    if a.cmd == "bc":
        t0 = time.time()
        dsets = []
        for i in range(a.batches):
            d = collect(st, 1.0, a.B, seed=a.seed * 1000 + i)
            log(f"collect {i}: {len(d['f'])} rows, z-err {d['_meta']['zerr']:.2f}, lay+ {int(d['lay'].sum())} molt+ {int(d['molt'].sum())} ({time.time() - t0:.0f}s)")
            dsets.append(d)
        data = merge(dsets, a.cap)
        torch.save(data, dpath)
        fit(st, data, a.epochs, log=log)
        save(st, os.path.join(a.run, "student.pt"))
        res = quick_eval(st, log)
        hist.append(dict(stage="bc", passed=res["passed"], feasible=res["feasible"], res=res))
        json.dump(hist, open(os.path.join(a.run, "hist.json"), "w"), indent=1)
        return
    # DAgger
    data = torch.load(dpath) if os.path.exists(dpath) else (torch.load(os.path.join(os.path.dirname(a.init), "data.pt")) if a.init else None)
    start = len([h for h in hist if h["stage"].startswith("dagger")])
    for it in range(start, a.iters):
        beta = max(0.0, 0.5 * (1 - it / max(1, a.iters - 1)))
        t0 = time.time()
        new = []
        for i in range(a.batches):
            d = collect(st, beta, a.B, seed=a.seed * 1000 + 100 * (it + 1) + i)
            new.append(d)
            log(f"dagger {it} collect {i} beta={beta:.2f}: {len(d['f'])} rows z-err {d['_meta']['zerr']:.2f} fin {d['_meta']['fin'][:4]} ({time.time() - t0:.0f}s)")
        data = merge(([data] if data is not None else []) + new, a.cap)
        torch.save(data, dpath)
        fit(st, data, a.epochs, log=log, lr=1e-3)
        save(st, os.path.join(a.run, f"student_{it:02d}.pt"))
        save(st, os.path.join(a.run, "student.pt"))
        res = quick_eval(st, log)
        hist.append(dict(stage=f"dagger{it}", beta=beta, passed=res["passed"], feasible=res["feasible"], res=res))
        json.dump(hist, open(os.path.join(a.run, "hist.json"), "w"), indent=1)


if __name__ == "__main__":
    main()
