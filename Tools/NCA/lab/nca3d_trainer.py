#!/usr/bin/env python3
"""
nca3d_trainer.py -- 3D Growing Neural Cellular Automata trainer (flora prong, NCA Ecology Lab).

Garrett's trainer from the nca_ecology_lab Drive folder (2026-10-18), moved into the repo so Froglet Compute can run it
as a COMMAND job. The model, targets, loss, metrics and GIF are his, unchanged. What was added is only the contract a
Compute command job needs (Docs/Compute/README.md, Tools/Compute/worker.py command_session):

  * a CLI: `--config "job nca3d | target sphere | iters 4000"` takes the Notion row's Config verbatim, `--out`, `--resume`
  * status.json, rewritten as it trains: {step, steps, phase, best_iou, loss}; Compute reads progress = step / steps and
    the score from `loss` (1 - best_iou, lower is better, as the board expects)
  * checkpoints (ckpt.pt, state.json) every --ckpt-every iterations and on SIGINT / SIGTERM / SIGBREAK, then exit 0:
    the worker interrupts a run when the person comes back or its session ends (worker.py, the SIGINT after
    presence.back()), and another session - on this computer or, through the results branch, another - resumes it
  * the training pool is kept locally only (pool_local.pt, fp16): at 256 x 16 x 24^3 it is ~110 MB, over a results
    branch's cap, so a run relayed to another computer reseeds its pool and says so in status.json
  * device: cuda, then Apple mps, then cpu; --require-gpu makes a run on a machine without one fail at once with a
    reason in result.json instead of spending a day on the CPU

The runner's entry point `run(config: dict) -> dict` is kept, so Garrett's standalone runner still works unchanged.

    python3 nca3d_trainer.py --config "job nca3d | target sphere | iters 4000" --out runs/a1
    python3 nca3d_trainer.py --config "..." --out runs/a1 --resume      # after an interruption
    python3 nca3d_trainer.py                                            # tiny smoke config
"""
from __future__ import annotations

import argparse
import json
import math
import os
import signal
import sys
import time
from dataclasses import dataclass, asdict, fields

import numpy as np

try:
    import torch
    import torch.nn as nn
    import torch.nn.functional as F
except Exception as e:  # pragma: no cover - torch required at runtime on the GPU host
    raise SystemExit(
        "PyTorch is required. Install with CUDA from https://pytorch.org "
        f"(import failed: {e})"
    )

try:
    import imageio.v2 as imageio
except Exception:
    imageio = None


# --------------------------------------------------------------------------- #
# Procedural targets: RGBA volumes in [0,1], shape (4, S, S, S)               #
# --------------------------------------------------------------------------- #
def make_target(name: str, size: int) -> np.ndarray:
    s = size
    lin = np.linspace(-1.0, 1.0, s)
    z, y, x = np.meshgrid(lin, lin, lin, indexing="ij")
    if name == "sphere":
        occ = (x * x + y * y + z * z) < 0.6 ** 2
    elif name == "shell":
        r = np.sqrt(x * x + y * y + z * z)
        occ = (r < 0.75) & (r > 0.55)
    elif name == "gyroid":  # 2D-ish sheet prismscape
        k = 3.0 * math.pi
        g = np.sin(k * x) * np.cos(k * y) + np.sin(k * y) * np.cos(k * z) + np.sin(k * z) * np.cos(k * x)
        occ = np.abs(g) < 0.30
    elif name == "octa":  # octahedron
        occ = (np.abs(x) + np.abs(y) + np.abs(z)) < 0.8
    else:
        raise ValueError(f"unknown target '{name}'")
    occ = occ.astype(np.float32)
    # Simple position-based color so RGB carries a learnable signal.
    rgb = np.stack([(x + 1) / 2, (y + 1) / 2, (z + 1) / 2], 0).astype(np.float32) * occ
    return np.concatenate([rgb, occ[None]], 0)  # (4, S, S, S)


# --------------------------------------------------------------------------- #
# Model                                                                       #
# --------------------------------------------------------------------------- #
def _perception_kernels() -> torch.Tensor:
    ident = np.zeros((3, 3, 3), np.float32); ident[1, 1, 1] = 1.0
    sx = np.zeros((3, 3, 3), np.float32); sx[1, 1, 0] = -1; sx[1, 1, 2] = 1
    sy = np.zeros((3, 3, 3), np.float32); sy[1, 0, 1] = -1; sy[1, 2, 1] = 1
    sz = np.zeros((3, 3, 3), np.float32); sz[0, 1, 1] = -1; sz[2, 1, 1] = 1
    lap = -6.0 * ident.copy()
    lap[1, 1, 0] = lap[1, 1, 2] = lap[1, 0, 1] = lap[1, 2, 1] = lap[0, 1, 1] = lap[2, 1, 1] = 1.0
    return torch.tensor(np.stack([ident, sx, sy, sz, lap], 0))  # (5,3,3,3)


class NCA3D(nn.Module):
    def __init__(self, channels: int = 16, hidden: int = 128, fire_rate: float = 0.5):
        super().__init__()
        self.C = channels
        self.fire_rate = fire_rate
        k = _perception_kernels()                       # (5,3,3,3)
        w = k[:, None].repeat(channels, 1, 1, 1, 1)     # (5C,1,3,3,3) depthwise
        self.register_buffer("perc_w", w)
        self.n_perc = 5
        self.w1 = nn.Conv3d(channels * self.n_perc, hidden, 1)
        self.w2 = nn.Conv3d(hidden, channels, 1)
        nn.init.zeros_(self.w2.weight)
        nn.init.zeros_(self.w2.bias)

    def perceive(self, x: torch.Tensor) -> torch.Tensor:
        return F.conv3d(x, self.perc_w, padding=1, groups=self.C)

    def alive_mask(self, x: torch.Tensor) -> torch.Tensor:
        a = x[:, 3:4]
        return F.max_pool3d(a, 3, stride=1, padding=1) > 0.1

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        pre = self.alive_mask(x)
        y = self.perceive(x)
        y = self.w2(F.relu(self.w1(y)))
        if self.fire_rate < 1.0:
            mask = (torch.rand_like(x[:, :1]) <= self.fire_rate).float()
            y = y * mask
        x = x + y
        post = self.alive_mask(x)
        return x * (pre & post).float()


def seed_state(batch: int, channels: int, size: int, device) -> torch.Tensor:
    x = torch.zeros(batch, channels, size, size, size, device=device)
    c = size // 2
    x[:, 3:, c, c, c] = 1.0  # alpha + hidden = 1 at the center cell
    return x


def damage(x: torch.Tensor, rng: np.random.Generator) -> torch.Tensor:
    b, c, s, _, _ = x.shape
    lin = torch.linspace(-1, 1, s, device=x.device)
    z, y, xx = torch.meshgrid(lin, lin, lin, indexing="ij")
    cx, cy, cz = (rng.uniform(-0.5, 0.5, 3)).tolist()
    r = float(rng.uniform(0.2, 0.4))
    keep = ((xx - cx) ** 2 + (y - cy) ** 2 + (z - cz) ** 2) > r * r
    return x * keep.float()[None, None]


# --------------------------------------------------------------------------- #
# Metrics + rendering                                                         #
# --------------------------------------------------------------------------- #
def iou(state: torch.Tensor, target: torch.Tensor, thr: float = 0.1) -> float:
    a = (state[:, 3] > thr)
    t = (target[3] > 0.5)[None]
    inter = (a & t).sum().item()
    union = (a | t).sum().item()
    return inter / union if union else 0.0


def to_rgb_image(state: torch.Tensor) -> np.ndarray:
    # max-intensity projection along depth of RGB*alpha, returns HxWx3 uint8
    s = state[0]
    rgb = s[:3].clamp(0, 1)
    a = s[3:4].clamp(0, 1)
    vol = (rgb * a)
    img = vol.max(dim=1).values  # project along D -> (3,H,W)
    img = img.permute(1, 2, 0).detach().cpu().numpy()
    return (img * 255).astype(np.uint8)


def render_growth_gif(model: NCA3D, size: int, steps: int, path: str, device, every: int = 4):
    if imageio is None:
        return None
    model.eval()
    with torch.no_grad():
        x = seed_state(1, model.C, size, device)
        frames = []
        for i in range(steps):
            x = model(x)
            if i % every == 0:
                frames.append(to_rgb_image(x))
        frames.append(to_rgb_image(x))
    imageio.mimsave(path, frames, duration=0.08)
    model.train()
    return path


# --------------------------------------------------------------------------- #
# Config                                                                      #
# --------------------------------------------------------------------------- #
@dataclass
class NCAConfig:
    target: str = "sphere"
    size: int = 24
    channels: int = 16
    hidden: int = 128
    fire_rate: float = 0.5
    pool_size: int = 256
    batch: int = 8
    steps_min: int = 48
    steps_max: int = 72
    iters: int = 2000
    lr: float = 2e-3
    damage_prob: float = 0.0  # set >0 (e.g. 0.5) to train regeneration (A2)
    damage_after: int = 500
    seed: int = 0
    out_dir: str = "runs/nca_smoke"


_TYPES = {f.name: f.type for f in fields(NCAConfig)}


def parse_config(config_str: str) -> dict:
    """The runner's parser (runner.py parse_config), so a Notion row's Config means the same thing in both:
    "job nca3d | target sphere | iters 4000" -> {'job':'nca3d', 'target':'sphere', 'iters':4000}."""
    cfg: dict = {}
    for part in (config_str or "").split("|"):
        part = part.strip()
        if not part:
            continue
        if ":" in part and " " not in part.split(":", 1)[0]:
            key, _, val = part.partition(":")
        else:
            key, _, val = part.partition(" ")
        key, val = key.strip().rstrip(":"), val.strip()
        if val == "":
            cfg[key] = True
            continue
        try:
            cfg[key] = int(val)
        except ValueError:
            try:
                cfg[key] = float(val)
            except ValueError:
                cfg[key] = val
    return cfg


def trainer_params(cfg: dict) -> tuple[dict, list[str]]:
    """The keys the trainer understands, coerced as jobs.py does; and the ones it dropped (shown, never silent)."""
    p, dropped = {}, []
    for k, v in cfg.items():
        if k in ("job", "out_dir"):
            continue
        t = _TYPES.get(k)
        if t in ("int", int) and isinstance(v, int) and not isinstance(v, bool):
            p[k] = v
        elif t in ("float", float) and isinstance(v, (int, float)) and not isinstance(v, bool):
            p[k] = float(v)
        elif t in ("str", str) and isinstance(v, str):
            p[k] = v
        else:
            dropped.append(k)
    return p, dropped


def pick_device(want: str = "auto") -> str:
    if want != "auto":
        return want
    if torch.cuda.is_available():
        return "cuda"
    mps = getattr(torch.backends, "mps", None)
    if mps is not None and mps.is_available():
        return "mps"
    return "cpu"


# --------------------------------------------------------------------------- #
# Checkpoints and the interrupt                                               #
# --------------------------------------------------------------------------- #
class _Stop:
    """SIGINT (Linux/macOS worker), SIGBREAK (the Windows worker's CTRL_BREAK_EVENT) or SIGTERM: finish the iteration,
    checkpoint, exit 0 - an interrupted run is not a failed one (worker.py reads a non-zero exit as 'failed')."""
    requested = False

    @classmethod
    def install(cls):
        for name in ("SIGINT", "SIGTERM", "SIGBREAK"):
            sig = getattr(signal, name, None)
            if sig is not None:
                try:
                    signal.signal(sig, lambda *_: setattr(cls, "requested", True))
                except (ValueError, OSError):
                    pass        # not the main thread (the runner's run()): the runner owns its own signals


def _write_json(path, data):
    tmp = path + ".tmp"
    with open(tmp, "w") as f:
        json.dump(data, f, indent=1)
    os.replace(tmp, path)


def _status(out_dir, **kw):
    _write_json(os.path.join(out_dir, "status.json"), dict(kw, updated=int(time.time())))


def _save_ckpt(out_dir, it, model, opt, sched, pool, rng, best_iou, history, cfg):
    torch.save({"iter": it, "model": model.state_dict(), "opt": opt.state_dict(), "sched": sched.state_dict(),
                "rng": rng.bit_generator.state, "torch_rng": torch.get_rng_state(), "best_iou": best_iou,
                "history": history, "config": asdict(cfg)}, os.path.join(out_dir, "ckpt.pt.tmp"))
    os.replace(os.path.join(out_dir, "ckpt.pt.tmp"), os.path.join(out_dir, "ckpt.pt"))
    torch.save(pool.detach().to("cpu", torch.float16), os.path.join(out_dir, "pool_local.pt.tmp"))
    os.replace(os.path.join(out_dir, "pool_local.pt.tmp"), os.path.join(out_dir, "pool_local.pt"))
    _write_json(os.path.join(out_dir, "state.json"), {"iter": it, "best_iou": best_iou, "config": asdict(cfg)})


# --------------------------------------------------------------------------- #
# Training                                                                    #
# --------------------------------------------------------------------------- #
def run(config: dict, resume: bool = False, device_pref: str = "auto", ckpt_every: int = 100,
        threads: int = 0) -> dict:
    cfg = NCAConfig(**{**asdict(NCAConfig()), **(config or {})})
    os.makedirs(cfg.out_dir, exist_ok=True)
    if threads:
        torch.set_num_threads(threads)
    device = pick_device(device_pref)
    torch.manual_seed(cfg.seed)
    rng = np.random.default_rng(cfg.seed)

    target = torch.tensor(make_target(cfg.target, cfg.size), device=device)  # (4,S,S,S)
    model = NCA3D(cfg.channels, cfg.hidden, cfg.fire_rate).to(device)
    opt = torch.optim.Adam(model.parameters(), lr=cfg.lr)
    sched = torch.optim.lr_scheduler.StepLR(opt, step_size=max(1, cfg.iters // 2), gamma=0.3)

    pool = seed_state(cfg.pool_size, cfg.channels, cfg.size, device)
    history: list = []
    best_iou = 0.0
    start = 0
    note = ""
    ckpt_path = os.path.join(cfg.out_dir, "ckpt.pt")
    if resume and os.path.isfile(ckpt_path):
        ck = torch.load(ckpt_path, map_location=device, weights_only=False)
        same = lambda c: {k: v for k, v in c.items() if k != "out_dir"}     # a relayed run lives in another folder
        if same(ck["config"]) != same(asdict(cfg)):
            raise SystemExit(f"--resume: {ckpt_path} was trained with a different config; use a new --out")
        model.load_state_dict(ck["model"])
        opt.load_state_dict(ck["opt"])
        sched.load_state_dict(ck["sched"])
        rng.bit_generator.state = ck["rng"]
        torch.set_rng_state(ck["torch_rng"])
        best_iou, history, start = ck["best_iou"], ck["history"], ck["iter"] + 1
        pool_path = os.path.join(cfg.out_dir, "pool_local.pt")
        if os.path.isfile(pool_path):
            pool = torch.load(pool_path, map_location=device).float()
        else:
            note = "pool reseeded (relayed from another computer: the pool stays on the machine that trained it)"
        print(json.dumps({"resumed_at": start, "note": note}), flush=True)

    t0 = time.time() - (history[-1]["secs"] if history else 0.0)
    _Stop.install()
    status = dict(step=start, steps=cfg.iters, phase="training", device=device, best_iou=round(best_iou, 4),
                  loss=round(1.0 - best_iou, 4), note=note)
    _status(cfg.out_dir, **status)

    for it in range(start, cfg.iters):
        idx = rng.choice(cfg.pool_size, cfg.batch, replace=False)
        x = pool[idx].clone()
        # reseed the worst sample in the batch to keep the pool anchored
        with torch.no_grad():
            losses0 = ((x[:, :4] - target[None]) ** 2).mean(dim=(1, 2, 3, 4))
            x[int(losses0.argmax())] = seed_state(1, cfg.channels, cfg.size, device)[0]
            if cfg.damage_prob > 0 and it > cfg.damage_after:
                for b in range(cfg.batch):
                    if rng.random() < cfg.damage_prob:
                        x[b: b + 1] = damage(x[b: b + 1], rng)

        n_steps = int(rng.integers(cfg.steps_min, cfg.steps_max + 1))
        for _ in range(n_steps):
            x = model(x)

        loss = ((x[:, :4] - target[None]) ** 2).mean()
        opt.zero_grad()
        loss.backward()
        for p in model.parameters():  # normalize grads (NCA stability trick)
            if p.grad is not None:
                p.grad /= (p.grad.norm() + 1e-8)
        opt.step()
        sched.step()

        with torch.no_grad():
            pool[idx] = x.detach()

        if it % 50 == 0 or it == cfg.iters - 1:
            cur_iou = iou(x, target)
            best_iou = max(best_iou, cur_iou)
            rec = dict(iter=it, loss=round(float(loss), 6), iou=round(cur_iou, 4),
                       secs=round(time.time() - t0, 1))
            history.append(rec)
            print(json.dumps(rec), flush=True)
        status.update(step=it + 1, best_iou=round(best_iou, 4), loss=round(1.0 - best_iou, 4))
        if (it + 1) % 10 == 0:
            _status(cfg.out_dir, **status)
        if _Stop.requested or (it + 1) % ckpt_every == 0:
            _save_ckpt(cfg.out_dir, it, model, opt, sched, pool, rng, best_iou, history, cfg)
            if _Stop.requested:
                _status(cfg.out_dir, **dict(status, phase="interrupted"))
                print(json.dumps({"interrupted_at": it + 1, "checkpoint": ckpt_path}), flush=True)
                return {"interrupted": True, "step": it + 1}

    # 2x-horizon stability (A1): run well past training horizon, measure drift
    _status(cfg.out_dir, **dict(status, step=cfg.iters, phase="stability"))
    model.eval()
    with torch.no_grad():
        x = seed_state(1, cfg.channels, cfg.size, device)
        for _ in range(cfg.steps_max):
            x = model(x)
        mid = x.clone()
        for _ in range(cfg.steps_max * 2):
            x = model(x)
        drift = float(((x[:, :4] - mid[:, :4]) ** 2).mean())
        stab_iou = iou(x, target)

    gif = render_growth_gif(model, cfg.size, cfg.steps_max * 2,
                            os.path.join(cfg.out_dir, "growth.gif"), device)
    ckpt = os.path.join(cfg.out_dir, "model.pt")
    torch.save({"state_dict": model.state_dict(), "config": asdict(cfg)}, ckpt)

    metrics = dict(
        target=cfg.target, channels=cfg.channels, hidden=cfg.hidden,
        params=sum(p.numel() for p in model.parameters()),
        final_loss=history[-1]["loss"] if history else None, best_iou=round(best_iou, 4),
        stability_drift=round(drift, 6), stability_iou=round(stab_iou, 4),
        wall_secs=round(time.time() - t0, 1), device=device,
    )
    result = dict(metrics=metrics, artifacts={"gif": gif, "checkpoint": ckpt},
                  history=history, config=asdict(cfg))
    with open(os.path.join(cfg.out_dir, "result.json"), "w") as f:
        json.dump(result, f, indent=2)
    _status(cfg.out_dir, **dict(status, step=cfg.iters, phase="done", best_iou=metrics["best_iou"],
                                stability_iou=metrics["stability_iou"], loss=round(1.0 - best_iou, 4)))
    print("RESULT", json.dumps(metrics), flush=True)
    return result


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--config", default="", help="a Notion row's Config, verbatim: 'job nca3d | target sphere | iters 4000'")
    ap.add_argument("--out", default="runs/nca_smoke")
    ap.add_argument("--resume", action="store_true", help="continue from --out's checkpoint")
    ap.add_argument("--device", default="auto", choices=("auto", "cuda", "mps", "cpu"))
    ap.add_argument("--require-gpu", action="store_true", help="fail at once (exit 3) when there is no cuda or mps")
    ap.add_argument("--threads", type=int, default=0)
    ap.add_argument("--ckpt-every", type=int, default=100)
    ap.add_argument("--set", action="append", default=[], metavar="KEY=VALUE", help="override one config key")
    a = ap.parse_args(argv)
    os.makedirs(a.out, exist_ok=True)
    if not a.config and not a.set:     # the smoke config, as before
        a.config = "job nca3d | target sphere | size 20 | channels 12 | hidden 96 | iters 200 | batch 6 | pool_size 128"
    cfg = parse_config(a.config)
    for kv in a.set:
        k, _, v = kv.partition("=")
        cfg.update(parse_config(f"{k} {v}"))
    if cfg.get("job", "nca3d") != "nca3d":
        raise SystemExit(f"this trainer runs job nca3d, not {cfg['job']!r}")
    params, dropped = trainer_params(cfg)
    if dropped:
        print(json.dumps({"dropped_config_keys": dropped}), flush=True)
    if a.require_gpu and pick_device(a.device) == "cpu":
        _write_json(os.path.join(a.out, "result.json"), {"reason": "no GPU (cuda or mps) on this computer"})
        return 3
    params["out_dir"] = a.out
    run(params, resume=a.resume, device_pref=a.device, ckpt_every=a.ckpt_every, threads=a.threads)
    return 0


if __name__ == "__main__":
    sys.exit(main())
