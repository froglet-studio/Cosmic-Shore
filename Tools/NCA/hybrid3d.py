"""One 3D NCA, three animals: lizard, whale and jellyfish in a single rule, with the form carried
by the cells themselves, so hybrids are emergent rather than designed.

Why this shape. The lizard, whale and jelly in results/ are three separate rules. Nothing can be
"between" them: a cell runs one network or the other. Here one network (the same 16-channel,
128-hidden cell as nca3d.py, so nca_creature.js and the game kernel run it unchanged) learns all
three. Which animal a body becomes is written in three of its own state channels, the GENOME
(channels 13, 14, 15 = lizard, whale, jelly, one-hot in the seed). The genome is ordinary state:
every cell reads its neighbours' genome through perception and writes its own, so the rule has to
learn to keep it, spread it into new tissue and regrow it after a cut. A small loss holds the
genome to its code inside a pure animal; NOTHING is trained on mixtures. So when a whale meets
jelly tissue (a graft, a blended seed, two seeds side by side, a wound refilled from both sides)
the rule is in a state it has never seen, and what it does there (one form invades, a stable
chimera, a border that wanders, two heads) is the rule's own answer.

    python3 Tools/NCA/hybrid3d.py bench
    OMP_NUM_THREADS=1 nohup python3 Tools/NCA/hybrid3d.py train --out /mnt/project-files/hybrid3d/h1 \
        --init3d Tools/NCA/results/lizard3d_swim/model.pt --genome-w 0.1 > h1.log 2>&1 &
    python3 Tools/NCA/hybrid3d.py train --out /mnt/project-files/hybrid3d/h1 --resume   # after a restart

Targets: the frames the separate runs trained on (lizard results/lizard3d_swim, whale and jelly
from their runs), placed in one 22x44x44 grid so that each run's seed cell lands on the common
seed cell (11, 22, 22). Axes: D = up for all three (the lizard lies flat, its depth axis is up).
"""
from __future__ import annotations

import argparse
import json
import math
import os
import sys
import time
from dataclasses import dataclass, asdict

import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from nca3d import CA3D, frame_mse, rollout_checkpointed, sphere_damage, render  # noqa: E402
from PIL import Image  # noqa: E402

FORMS = ("lizard", "whale", "jelly")
GENOME = (13, 14, 15)                    # state channels holding the one-hot form code
GRID = (22, 44, 44)
SEED_AT = (11, 22, 22)
# (frames.npy, the seed cell of that run's own grid = its grid centre)
SOURCES = {
    "lizard": "results/lizard3d_swim/frames.npy",
    "whale": "results/whale3d_swim/frames.npy",
    "jelly": "results/jelly3d_swim/frames.npy",
}
FALLBACK = "/mnt/project-files/overnight/nca-ckpt/{}/frames.npy"


def load_form_frames():
    """-> float32 [F, K, D, H, W, 4] in the common grid."""
    out = []
    for f in FORMS:
        p = os.path.join(HERE, SOURCES[f])
        if not os.path.isfile(p):
            p = FALLBACK.format(f)
        a = np.load(p).astype(np.float32)
        K, d, h, w, _ = a.shape
        off = [SEED_AT[i] - (d, h, w)[i] // 2 for i in range(3)]
        g = np.zeros((K,) + GRID + (4,), np.float32)
        g[:, off[0]:off[0] + d, off[1]:off[1] + h, off[2]:off[2] + w] = a
        assert abs(g[..., 3].sum() - a[..., 3].sum()) < 1e-3, f"{f} does not fit the common grid"
        out.append(g)
    return np.stack(out)


def make_seed(form_code, C=16):
    """form_code: [n, 3] genome rows (one-hot for a pure animal, anything for a hybrid)."""
    code = torch.as_tensor(form_code, dtype=torch.float32)
    x = torch.zeros((len(code),) + GRID + (C,))
    x[:, SEED_AT[0], SEED_AT[1], SEED_AT[2], 3:] = 1.0
    x[:, SEED_AT[0], SEED_AT[1], SEED_AT[2], list(GENOME)] = code
    return x


def genome_readout(x):
    """[B,D,H,W,C] -> [B, 3]: mean genome over cells with alpha > 0.1 (which animal the body 'is')."""
    m = (x[..., 3] > 0.1).float()
    g = x[..., list(GENOME)]
    return (g * m[..., None]).flatten(1, 3).sum(1) / m.flatten(1).sum(1).clamp(min=1)[:, None]


@dataclass
class ConfigH:
    init3d: str = ""             # warm start (a 16/128 nca3d model.pt)
    genome_w: float = 0.1        # weight of the genome-holding loss (0 = genome lives only in the seed)
    per_form: int = 2            # batch = per_form x 3 forms
    damage_per_form: int = 1
    pool_per_form: int = 128
    frames: int = 8
    period: int = 8
    window: int = 5
    min_seed_check: int = 64
    clock_steps: int = 1000
    clock_min_iter: int = 96
    clock_max_iter: int = 128
    lr: float = 2e-3
    lr_drop_step: int = 5000
    steps: int = 8000
    min_iter: int = 64
    max_iter: int = 96
    fire_rate: float = 0.5
    blowup_factor: float = 20.0
    seed: int = 0
    threads: int = 1
    pool_cache: str = ""        # local file (NOT the shared folder: 0.5 GB) the pool is saved to every 100 pool steps
    device: str = "cpu"         # cuda on a GPU box (the run's files are device-free: load with map_location)


def train(cfg: ConfigH, out_dir: str, resume: bool = False):
    os.makedirs(out_dir, exist_ok=True)
    start = 0
    if resume:
        cfg = ConfigH(**{**asdict(cfg), **json.load(open(os.path.join(out_dir, "config.json"))), "threads": cfg.threads, "pool_cache": cfg.pool_cache})
        start = json.load(open(os.path.join(out_dir, "state.json")))["step"]
    else:
        with open(os.path.join(out_dir, "config.json"), "w") as f:
            json.dump(asdict(cfg), f, indent=2)
    torch.set_num_threads(cfg.threads)
    dev = torch.device(cfg.device)
    torch.set_default_device(dev)
    torch.manual_seed(cfg.seed + start)
    rng = np.random.default_rng(cfg.seed + start)
    fr = torch.from_numpy(load_form_frames()).to(dev)                # [F, K, D, H, W, 4]
    if not os.path.isfile(os.path.join(out_dir, "frames.npy")):
        np.save(os.path.join(out_dir, "frames.npy"), fr.cpu().numpy().astype(np.float16))
    NF, K = fr.shape[:2]
    ca = CA3D(16, 128, cfg.fire_rate)
    if resume:
        ca.load_state_dict(torch.load(os.path.join(out_dir, "model.pt"), map_location=dev))
    elif cfg.init3d:
        ca.load_state_dict(torch.load(cfg.init3d, map_location=dev))
    opt = torch.optim.Adam(ca.parameters(), lr=cfg.lr, eps=1e-7)
    sched = torch.optim.lr_scheduler.MultiStepLR(opt, [cfg.lr_drop_step], 0.1)
    eye = torch.eye(NF)
    seeds = make_seed(eye.cpu()).to(dev)                                           # [F, ...]
    P = cfg.pool_per_form
    pool = seeds.repeat_interleave(P, 0)                             # [F*P, ...], form = index // P
    J, Pd = cfg.window, cfg.period
    shift = torch.arange(J)
    log, rollbacks, good = [], 0, None
    t0 = time.time()
    if resume:
        rollbacks = json.load(open(os.path.join(out_dir, "state.json"))).get("rollbacks", 0)
        if os.path.isfile(os.path.join(out_dir, "opt.pt")):
            opt.load_state_dict(torch.load(os.path.join(out_dir, "opt.pt"), map_location=dev))
        for _ in range(start):
            sched.step()
        log = list(np.load(os.path.join(out_dir, "loss.npy")))[:start + 1]
        if start >= cfg.clock_steps and cfg.pool_cache and os.path.isfile(cfg.pool_cache):
            pool = torch.load(cfg.pool_cache, map_location=dev).float()
            print(f"[hyb] pool loaded from {cfg.pool_cache}", flush=True)
        elif start >= cfg.clock_steps:                               # no cached pool: regrow it
            with torch.no_grad():
                for b in range(0, NF * P, 32):
                    n = min(32, NF * P - b)
                    x = seeds[(torch.arange(b, b + n) // P)]
                    for _ in range(int(rng.integers(150, 400))):
                        x = ca(x)
                    pool[b:b + n] = x
        print(f"[hyb] resumed at step {start} (lr {opt.param_groups[0]['lr']:.1e}, pool {(time.time() - t0) / 60:.1f}m)", flush=True)
        t0 = time.time()

    for step in range(start + 1 if resume else 0, cfg.steps + 1):
        clock = step < cfg.clock_steps
        form = torch.arange(NF).repeat_interleave(cfg.per_form)      # [B]
        B = len(form)
        if clock:
            idx = None
            x0 = seeds[form].clone()
            n = int(rng.integers(cfg.clock_min_iter, cfg.clock_max_iter + 1))
        else:
            idx = np.concatenate([f * P + rng.choice(P, cfg.per_form, replace=False) for f in range(NF)])
            x0 = pool[idx].clone()
            with torch.no_grad():
                for f in range(NF):                                  # per form: worst first; worst -> seed
                    sl = slice(f * cfg.per_form, (f + 1) * cfg.per_form)
                    e = frame_mse(x0[sl], fr[f]).min(1).values
                    o = torch.argsort(e, descending=True).cpu().numpy() + f * cfg.per_form
                    x0[sl], idx[sl] = x0[o].clone(), idx[o]
                    x0[f * cfg.per_form] = seeds[f]
                    if cfg.damage_per_form:
                        d = cfg.damage_per_form
                        x0[(f + 1) * cfg.per_form - d:(f + 1) * cfg.per_form] *= sphere_damage(d, *GRID, rng).to(dev)
            n = int(rng.integers(cfg.min_iter, cfg.max_iter + 1))

        checks = [n - (J - 1 - j) * Pd for j in range(J)]
        x, snaps = rollout_checkpointed(ca, x0, n, set(checks))
        born = torch.tensor([(c // Pd) % K for c in checks])
        sv = torch.tensor([float(c >= cfg.min_seed_check) for c in checks])
        kk = (torch.arange(K)[:, None] + shift[None]) % K
        losses, img = [], []
        for f in range(NF):
            sl = slice(f * cfg.per_form, (f + 1) * cfg.per_form)
            err = torch.stack([frame_mse(s[sl], fr[f]) for s in snaps], 1)          # [b, J, K]
            seed_cost = (err[:, shift, born] * sv).sum(-1) / sv.sum().clamp(min=1)
            if clock:
                lf = seed_cost
            else:
                cost = err[:, shift[None, :].expand(K, J), kk].mean(-1)
                free = cost[torch.arange(cost.shape[0]), cost.detach().argmin(1)]
                lf = torch.cat([seed_cost[:1], free[1:]])
            losses.append(lf)
        shape_loss = torch.cat(losses).mean()
        gl = torch.zeros(())
        if cfg.genome_w:
            for s in snaps:
                m = (s[..., 3:4] > 0.1).float()
                tgt = eye[form].view(B, 1, 1, 1, NF)
                gl = gl + (((s[..., list(GENOME)] - tgt) ** 2) * m).sum() / m.sum().clamp(min=1) / NF
            gl = gl / len(snaps)
        loss = shape_loss + cfg.genome_w * gl * 0.01                 # genome err ~1 vs shape err ~1e-2

        L = float(loss.detach())
        recent = [v for v in log[-50:] if math.isfinite(v)]
        med = float(np.median(recent)) if len(recent) >= 10 else float("inf")
        extinct = not bool((x.detach()[..., 3] > 0.1).any())   # the whole batch died (one cut-out sample is reseeded below)
        if not math.isfinite(L) or L > cfg.blowup_factor * med or extinct:
            rollbacks += 1
            if good is not None:
                ca.load_state_dict(good[0]); opt.load_state_dict(good[1])
            print(f"[hyb] step {step}: loss {L:.4g} (median {med:.4g}) extinct={extinct} -> rollback {rollbacks}", flush=True)
            log.append(med if math.isfinite(med) else (log[-1] if log else 1.0))
            sched.step()
            continue
        opt.zero_grad(set_to_none=True)
        loss.backward()
        for prm in ca.parameters():
            if prm.grad is not None:
                prm.grad /= prm.grad.norm() + 1e-8
        opt.step()
        sched.step()
        xd = x.detach()
        with torch.no_grad():
            bad = ~torch.isfinite(xd).flatten(1).all(1) | (xd.abs().flatten(1).amax(1) > 50) | \
                  ~(xd[..., 3] > 0.1).flatten(1).any(1)
            if bool(bad.any()):
                xd = xd.clone(); xd[bad] = seeds[form[bad]]
        if clock:
            if step == cfg.clock_steps - 1:
                for f in range(NF):
                    pick = rng.integers(0, cfg.per_form, P) + f * cfg.per_form
                    pool[f * P:(f + 1) * P] = xd[pick]
        else:
            pool[idx] = xd
            if cfg.pool_cache and step % 100 == 0:
                torch.save(pool.half().cpu(), cfg.pool_cache + ".tmp"); os.replace(cfg.pool_cache + ".tmp", cfg.pool_cache)
        log.append(L)
        if step % 25 == 0:
            good = ({k: v.clone() for k, v in ca.state_dict().items()}, __import__("copy").deepcopy(opt.state_dict()), step)
            dt = time.time() - t0
            spi = dt / (step - start + 1)
            per = [float(l.mean()) for l in losses]
            gr = genome_readout(xd).cpu().numpy().round(2).tolist()
            json.dump({"step": step, "steps": cfg.steps, "phase": "clock" if clock else "pool", "loss": L,
                       "shape": {f: round(p, 5) for f, p in zip(FORMS, per)}, "genome_loss": float(gl),
                       "s_per_it": round(spi, 2), "eta_h": round((cfg.steps - step) * spi / 3600, 2),
                       "rollbacks": rollbacks, "pid": os.getpid(), "updated": time.strftime("%Y-%m-%d %H:%M:%S")},
                      open(os.path.join(out_dir, "status.json"), "w"), indent=1)
            print(f"[hyb] {'clock' if clock else 'pool '} {step:5d} loss {L:.5f} " +
                  " ".join(f"{f[0]}={p:.4f}" for f, p in zip(FORMS, per)) + f" g={float(gl):.3f} {spi:.2f}s/it", flush=True)
        if step % 25 == 0 or step == cfg.steps:
            torch.save({k: v.cpu() for k, v in ca.state_dict().items()}, os.path.join(out_dir, "model.tmp")); os.replace(os.path.join(out_dir, "model.tmp"), os.path.join(out_dir, "model.pt"))
            torch.save(opt.state_dict(), os.path.join(out_dir, "opt.tmp")); os.replace(os.path.join(out_dir, "opt.tmp"), os.path.join(out_dir, "opt.pt"))
            np.save(os.path.join(out_dir, "loss.npy"), np.array(log))
            json.dump({"step": step, "rollbacks": rollbacks}, open(os.path.join(out_dir, "state.json"), "w"))
        if step % 250 == 0 or step == cfg.steps:
            ims = [render(xd[i].cpu(), px=96) for i in range(B)]
            Image.fromarray((np.concatenate(ims, 1) * 255).astype(np.uint8)).save(os.path.join(out_dir, f"batch_{step:05d}.png"))
            if step % 1000 == 0:
                torch.save({k: v.cpu() for k, v in ca.state_dict().items()}, os.path.join(out_dir, f"model_{step:05d}.pt"))
    return ca


def bench(threads=1, iters=40):
    torch.set_num_threads(threads)
    ca = CA3D(16, 128)
    ca.load_state_dict(torch.load(os.path.join(HERE, "results/lizard3d_swim/model.pt")))
    fr = load_form_frames()
    print("frames", fr.shape, "alive/form", (fr[:, 0, ..., 3] > 0.1).sum((1, 2, 3)))
    x = make_seed(torch.eye(3).repeat_interleave(2, 0))
    with torch.no_grad():
        for _ in range(60):
            x = ca(x)
    x.requires_grad_(False)
    t = time.time()
    y, s = rollout_checkpointed(ca, x, iters, {iters})
    s[0][..., :4].pow(2).mean().backward()
    print(f"{threads} thr: {(time.time() - t) / iters * 80:.2f} s per 80-step training iteration (batch 6)")


def export(model, out, grid=(24, 56, 72), note=""):
    """weights.json in nca3d's format (nca_creature.js / build_nca_creature.py read it), on the probe/viewer grid."""
    sd = torch.load(model, map_location="cpu")
    data = {"channel_n": 16, "hidden": 128, "fire_rate": 0.5,
            **{k: sd[k].numpy().round(6).tolist() for k in ("w1", "b1", "w2", "b2")},
            "D": grid[0], "H": grid[1], "W": grid[2], "frames": 8, "period": 8,
            "perception_order": "per-channel [identity, sobel_x, sobel_y, sobel_z] -> index 4*c+k",
            "target": "lizard + whale + jelly in one rule (3D), form = genome channels 13-15 " + note,
            "genome": {"channels": list(GENOME), "forms": list(FORMS)},
            "experiment": "hybrid3d", "axes": "D=up (world y), H=world z, W=world x"}
    with open(out, "w") as f:
        json.dump(data, f)


def main():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    e = sub.add_parser("export"); e.add_argument("--model", required=True); e.add_argument("--out", required=True); e.add_argument("--note", default="")
    b = sub.add_parser("bench"); b.add_argument("--threads", type=int, default=1)
    t = sub.add_parser("train")
    t.add_argument("--out", required=True)
    t.add_argument("--resume", action="store_true")
    for k, v in asdict(ConfigH()).items():
        t.add_argument(f"--{k.replace('_', '-')}", type=type(v), default=v)
    a = ap.parse_args()
    if a.cmd == "bench":
        bench(a.threads)
    elif a.cmd == "export":
        export(a.model, a.out, note=a.note)
    else:
        kw = {k: getattr(a, k) for k in asdict(ConfigH())}
        train(ConfigH(**kw), a.out, a.resume)


if __name__ == "__main__":
    main()
