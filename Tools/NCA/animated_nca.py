"""Animated Neural CA — the Growing-NCA model, trained to reproduce a LOOP instead of a
still image. Same cell, same 8,336 parameters, same perception, same stochastic update
and alive mask as growing_nca.py; only the target and the loss change.

    python3 Tools/NCA/animated_nca.py target --out swim.gif           # preview the loop
    python3 Tools/NCA/animated_nca.py train                           # lizard swim, 8 frames
    python3 Tools/NCA/animated_nca.py train --gif path/to/anim.gif    # any animated GIF
    python3 Tools/NCA/animated_nca.py figures --run Tools/NCA/runs/lizard_swim

The problem an animation adds is TIME. A static target is a fixed point; an animation is a
limit cycle, and the cells have no clock: each one fires at random half the time and sees
only its 3x3 neighbours. So the loss must demand that the pattern ADVANCE without telling
it where in the loop it is:

  * the rollout is checked at J instants, P steps apart, ending at the last step;
  * checkpoint j is compared with frame (k0 + j) mod K;
  * k0 is whichever start frame fits the whole window best (chosen per sample, no gradient).

So each sample picks its own phase, but must then move forward one frame per P steps. A
frozen image cannot satisfy that, because consecutive frames differ. A pool sample keeps
whatever phase it had, which is how the loop survives from one training step to the next.

A seed has not grown yet at the early checkpoints, so for the seed sample only checkpoints
at step >= min_seed_check count (the paper's own horizon is 64-96 steps).
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
from PIL import Image, ImageSequence

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from growing_nca import (CAModel, circle_damage, emoji_canvas, make_seed, save_grid,  # noqa: E402
                         to_rgb_image, to_target, write_gif)


# --------------------------------------------------------------- targets ---

def _bilinear(img: np.ndarray, sx: np.ndarray, sy: np.ndarray) -> np.ndarray:
    """Sample img [H, W, C] at float coords (zero outside)."""
    H, W = img.shape[:2]
    x0, y0 = np.floor(sx).astype(int), np.floor(sy).astype(int)
    fx, fy = (sx - x0)[..., None], (sy - y0)[..., None]
    out = np.zeros(sx.shape + (img.shape[2],), np.float32)
    for dy, wy in ((0, 1 - fy), (1, fy)):
        for dx, wx in ((0, 1 - fx), (1, fx)):
            xx, yy = x0 + dx, y0 + dy
            ok = (xx >= 0) & (xx < W) & (yy >= 0) & (yy < H)
            v = np.zeros_like(out)
            v[ok] = img[yy[ok], xx[ok]]
            out += v * wx * wy
    return out


def swim_frames(name="lizard", frames=8, amp=6.0, wavelength=0.9, max_size=40):
    """A travelling body wave (how a lizard or fish swims), generated from the emoji itself.

    The body axis is the alpha-weighted principal axis. The tail is the end with less mass
    in its last quarter. Every pixel is displaced along the body NORMAL by
        amp * env(s) * sin(2 pi (s / wavelength - k / frames)),   s = 0 head .. 1 tail,
    env(s) = s^1.3, so the head holds still and the tail swings widest. `amp` is in pixels of
    the 128px source (~amp/3.2 at the 40px target). One loop = one wavelength of travel.
    """
    src = emoji_canvas(name)
    a = np.asarray(src, np.float32) / 255.0
    pm = a.copy()
    pm[..., :3] *= pm[..., 3:]  # warp in premultiplied space (no dark fringes)
    H, W = a.shape[:2]
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    w = a[..., 3]
    m = w.sum()
    cx, cy = (w * xx).sum() / m, (w * yy).sum() / m
    cov = np.cov(np.stack([(xx - cx).ravel(), (yy - cy).ravel()]), aweights=w.ravel())
    ev, evec = np.linalg.eigh(cov)
    u = evec[:, 1]                      # major axis
    s = (xx - cx) * u[0] + (yy - cy) * u[1]
    body = w > 0.5
    lo, hi = s[body].min(), s[body].max()
    q = (hi - lo) * 0.25
    if w[s > hi - q].sum() > w[s < lo + q].sum():  # the tail is the THINNER end
        u, s, lo, hi = -u, -s, -hi, -lo
    n = np.array([-u[1], u[0]])
    sn = np.clip((s - lo) / (hi - lo), 0, 1)
    env = sn ** 1.3
    out = []
    for k in range(frames):
        d = amp * env * np.sin(2 * np.pi * (sn / wavelength - k / frames))
        f = _bilinear(pm, xx - d * n[0], yy - d * n[1])
        alpha = np.clip(f[..., 3:], 0, 1)
        straight = np.where(alpha > 1e-4, f[..., :3] / np.maximum(alpha, 1e-4), 0)
        im = Image.fromarray((np.concatenate([straight, alpha], -1).clip(0, 1) * 255).round().astype(np.uint8), "RGBA")
        out.append(to_target(im, max_size))
    return np.stack(out)


def gif_frames(path, max_size=40, max_frames=16):
    """Frames of any animated GIF/WebP/APNG, each thumbnailed + premultiplied like the paper's
    target. Longer animations are subsampled evenly to max_frames."""
    im = Image.open(path)
    fr = [f.convert("RGBA") for f in ImageSequence.Iterator(im)]
    if len(fr) > max_frames:
        fr = [fr[int(i)] for i in np.linspace(0, len(fr) - 1, max_frames)]
    side = max(max(f.width, f.height) for f in fr)
    out = []
    for f in fr:
        c = Image.new("RGBA", (side, side), (0, 0, 0, 0))
        c.paste(f, ((side - f.width) // 2, (side - f.height) // 2))
        out.append(to_target(c, max_size))
    return np.stack(out)


# --------------------------------------------------------------- training ---

@dataclass
class AnimConfig:
    target: str = "lizard"
    gif: str = ""
    frames: int = 8
    amp: float = 6.0
    wavelength: float = 0.9
    period: int = 8            # CA steps per animation frame
    window: int = 5            # checkpoints per rollout (window spans (window-1)*period steps)
    min_seed_check: int = 64
    channel_n: int = 16
    hidden: int = 128
    target_size: int = 40
    target_padding: int = 16
    batch_size: int = 8
    pool_size: int = 1024
    fire_rate: float = 0.5
    lr: float = 2e-3
    lr_drop_step: int = 2000
    steps: int = 8000
    min_iter: int = 64
    max_iter: int = 96
    damage_n: int = 3
    seed: int = 0
    threads: int = 0


def build_frames(cfg: AnimConfig) -> np.ndarray:
    fr = gif_frames(cfg.gif, cfg.target_size, cfg.frames) if cfg.gif else \
        swim_frames(cfg.target, cfg.frames, cfg.amp, cfg.wavelength, cfg.target_size)
    p = cfg.target_padding
    return np.pad(fr, ((0, 0), (p, p), (p, p), (0, 0)))


def frame_mse(x, frames):
    """[B,H,W,C] x [K,H,W,4] -> [B,K] mse of RGBA against every frame."""
    return ((x[:, None, ..., :4] - frames[None]) ** 2).mean(dim=(2, 3, 4))


def train(cfg: AnimConfig, out_dir: str):
    os.makedirs(out_dir, exist_ok=True)
    if cfg.threads:
        torch.set_num_threads(cfg.threads)
    torch.manual_seed(cfg.seed)
    rng = np.random.default_rng(cfg.seed)
    fr_np = build_frames(cfg)
    K, H, W = fr_np.shape[:3]
    frames = torch.from_numpy(fr_np)
    np.save(os.path.join(out_dir, "frames.npy"), fr_np)
    write_gif([np.clip(1 - f[..., 3:] + f[..., :3], 0, 1) for f in fr_np],
              os.path.join(out_dir, "target.gif"), ms=cfg.period * 33)
    with open(os.path.join(out_dir, "config.json"), "w") as f:
        json.dump(asdict(cfg), f, indent=2)

    ca = CAModel(cfg.channel_n, cfg.hidden, cfg.fire_rate)
    opt = torch.optim.Adam(ca.parameters(), lr=cfg.lr, eps=1e-7)
    sched = torch.optim.lr_scheduler.MultiStepLR(opt, [cfg.lr_drop_step], 0.1)
    seed = make_seed(1, H, W, cfg.channel_n)
    pool = seed.repeat(cfg.pool_size, 1, 1, 1)
    J, P = cfg.window, cfg.period
    shift = torch.arange(J)
    log, t0 = [], time.time()

    for step in range(cfg.steps + 1):
        idx = rng.choice(cfg.pool_size, cfg.batch_size, replace=False)
        x0 = pool[idx].clone()
        with torch.no_grad():
            order = torch.argsort(frame_mse(x0, frames).min(1).values, descending=True)
        x0, idx = x0[order], idx[order.numpy()]
        x0[:1] = seed
        if cfg.damage_n:
            x0[-cfg.damage_n:] *= circle_damage(cfg.damage_n, H, W, rng)

        n = int(rng.integers(cfg.min_iter, cfg.max_iter + 1))
        checks = [n - (J - 1 - j) * P for j in range(J)]
        x, snaps = x0, []
        for i in range(1, n + 1):
            x = ca(x)
            if i in checks:
                snaps.append(x)
        S = torch.stack(snaps, 1)                                   # [B, J, H, W, C]
        err = frame_mse(S.flatten(0, 1), frames).view(len(idx), J, K)  # [B, J, K]
        valid = torch.ones(len(idx), J)
        valid[0] = torch.tensor([float(c >= cfg.min_seed_check) for c in checks])
        # cost[b, k0] = mean over valid j of err[b, j, (k0 + j) % K]
        kk = (torch.arange(K)[:, None] + shift[None]) % K             # [K, J]
        gathered = err[:, shift[None, :].expand(K, J), kk]            # [B, K, J]
        cost = (gathered * valid[:, None]).sum(-1) / valid.sum(-1, keepdim=True)
        k0 = cost.detach().argmin(1)
        loss = cost[torch.arange(len(idx)), k0].mean()

        opt.zero_grad(set_to_none=True)
        loss.backward()
        for prm in ca.parameters():
            if prm.grad is not None:
                prm.grad /= prm.grad.norm() + 1e-8
        opt.step()
        sched.step()
        pool[idx] = x.detach()

        L = float(loss)
        log.append(L)
        if step % 50 == 0:
            dt = time.time() - t0
            print(f"[anim {cfg.gif or cfg.target}] step {step:5d}  loss {L:.5f}  log10 {math.log10(L):+.3f}  "
                  f"{dt/(step+1):.2f}s/it  elapsed {dt/60:.1f}m", flush=True)
        if step % 500 == 0 or step == cfg.steps:
            torch.save(ca.state_dict(), os.path.join(out_dir, "model.pt"))
            np.save(os.path.join(out_dir, "loss.npy"), np.array(log))
            save_grid(x.detach(), os.path.join(out_dir, f"batch_{step:05d}.png"))

    export(ca, cfg, fr_np, os.path.join(out_dir, "weights.json"))
    return ca


def export(ca, cfg, fr_np, path):
    data = {
        "channel_n": cfg.channel_n, "hidden": cfg.hidden, "fire_rate": cfg.fire_rate,
        "w1": ca.w1.detach().numpy().round(6).tolist(), "b1": ca.b1.detach().numpy().round(6).tolist(),
        "w2": ca.w2.detach().numpy().round(6).tolist(), "b2": ca.b2.detach().numpy().round(6).tolist(),
        "grid": fr_np.shape[1], "frames": fr_np.shape[0], "period": cfg.period,
        "target": cfg.gif or f"{cfg.target} swim", "experiment": "animated",
    }
    with open(path, "w") as f:
        json.dump(data, f)


# ---------------------------------------------------------------- figures ---

def load_anim(run_dir):
    cfg = AnimConfig(**json.load(open(os.path.join(run_dir, "config.json"))))
    ca = CAModel(cfg.channel_n, cfg.hidden, cfg.fire_rate)
    ca.load_state_dict(torch.load(os.path.join(run_dir, "model.pt")))
    return cfg, ca, torch.from_numpy(np.load(os.path.join(run_dir, "frames.npy")))


@torch.no_grad()
def figures(run_dir, seed=1, horizon=3000):
    torch.manual_seed(seed)
    cfg, ca, frames = load_anim(run_dir)
    K, H, W = frames.shape[:3]
    P = cfg.period
    fig = os.path.join(run_dir, "figures")
    os.makedirs(fig, exist_ok=True)

    # 1. Long rollout: which frame does the pattern match at every step, and how well?
    x = make_seed(1, H, W, cfg.channel_n)
    table, states = [], {}
    for t in range(horizon + 1):
        table.append(frame_mse(x, frames)[0].numpy())
        if 200 <= t < 200 + 3 * K * P:
            states[t] = x.clone()
        x = ca(x)
    table = np.stack(table)                      # [T, K]
    best, berr = table.argmin(1), table.min(1)
    # Unwrap the frame index into a continuous phase, fit frames/step after growth.
    ph = np.unwrap(best * 2 * np.pi / K) * K / (2 * np.pi)
    tt = np.arange(horizon + 1)
    sl = slice(200, None)
    slope = np.polyfit(tt[sl], ph[sl], 1)[0]
    # Static baseline: the single best still image (mean of frames) against the loop.
    mean_frame = frames.mean(0, keepdim=True)
    static_err = float(((mean_frame - frames) ** 2).mean())
    frame_gap = float(np.mean([float(((frames[i] - frames[(i + 1) % K]) ** 2).mean()) for i in range(K)]))
    # Phase-locked error: after growth, compare with the frame the CLOCK predicts.
    fit = np.polyfit(tt[sl], ph[sl], 1)
    pred = np.round(np.polyval(fit, tt)).astype(int) % K

    # 2. GIF: the automaton beside the target, target driven by the automaton's own phase.
    gif = []
    x = make_seed(1, H, W, cfg.channel_n)
    for t in range(200 + 6 * K * P):
        if t < 120 or t % 2 == 0:
            e = frame_mse(x, frames)[0]
            k = int(e.argmin())
            tgt = np.clip(1 - frames[k][..., 3:] + frames[k][..., :3], 0, 1).numpy()
            gif.append(np.concatenate([to_rgb_image(x)[0], np.ones((H, 2, 3)), tgt], 1))
        x = ca(x)
    write_gif(gif, os.path.join(fig, "loop.gif"), scale=1, ms=33)  # native pixels; the page scales it

    # 3. One loop, every P steps (top) above the target frame it best matches (bottom).
    ts = sorted(states)[::P][:K]
    top = np.concatenate([to_rgb_image(states[t])[0] for t in ts], 1)
    bot = np.concatenate([np.clip(1 - frames[best[t]][..., 3:] + frames[best[t]][..., :3], 0, 1).numpy()
                          for t in ts], 1)
    im = Image.fromarray((np.concatenate([top, np.ones((2, top.shape[1], 3)), bot], 0) * 255).astype(np.uint8))
    im.resize((im.width * 3, im.height * 3), Image.NEAREST).save(os.path.join(fig, "loop_strip.png"))

    # 4. Damage while moving: cut the tail half at step 400; does it regrow AND keep swimming?
    x = make_seed(1, H, W, cfg.channel_n)
    for _ in range(400):
        x = ca(x)
    x[:, H // 2:, W // 2:] = 0
    rec, rbest, seq = [], [], [x.clone()]
    for i in range(600):
        x = ca(x)
        e = frame_mse(x, frames)[0]
        rec.append(float(e.min()))
        rbest.append(int(e.argmin()))
        if i + 1 in (20, 50, 100, 200, 400, 600):
            seq.append(x.clone())
    save_grid(torch.cat(seq), os.path.join(fig, "damage.png"), scale=3)
    rph = np.unwrap(np.array(rbest[300:]) * 2 * np.pi / K) * K / (2 * np.pi)
    regen_slope = float(np.polyfit(np.arange(len(rph)), rph, 1)[0])

    summary = {
        "frames": K, "period_target_steps_per_frame": P,
        "measured_steps_per_frame": float(1 / slope) if abs(slope) > 1e-6 else None,
        "best_frame_error_mean_after_200": float(berr[200:].mean()),
        "best_frame_error_at": {str(t): float(berr[t]) for t in (60, 96, 200, 1000, horizon)},
        "clock_predicted_frame_error_mean": float(table[np.arange(200, horizon + 1), pred[200:]].mean()),
        "static_best_image_error": static_err,
        "mean_consecutive_frame_difference": frame_gap,
        "distinct_frames_visited_after_200": int(len(set(best[200:].tolist()))),
        "after_damage_error_at_600": rec[-1],
        "after_damage_steps_per_frame": float(1 / regen_slope) if abs(regen_slope) > 1e-6 else None,
    }
    np.save(os.path.join(fig, "phase.npy"), np.stack([best, berr]))
    with open(os.path.join(fig, "summary.json"), "w") as f:
        json.dump(summary, f, indent=2)
    print(json.dumps(summary, indent=2))


# -------------------------------------------------------------------- cli ---

TARGET_KEYS = ("target", "gif", "frames", "amp", "wavelength", "period", "target_size", "target_padding")

def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    tg = sub.add_parser("target")
    tg.add_argument("--out", required=True)
    t = sub.add_parser("train")
    t.add_argument("--out", default=None)
    for k, v in asdict(AnimConfig()).items():
        for p in ((t, tg) if k in TARGET_KEYS else (t,)):
            p.add_argument(f"--{k.replace('_', '-')}", type=type(v), default=v)
    fg = sub.add_parser("figures")
    fg.add_argument("--run", required=True)
    args = ap.parse_args()

    if args.cmd == "target":
        cfg = AnimConfig(**{k: getattr(args, k) for k in TARGET_KEYS})
        fr = build_frames(cfg)
        write_gif([np.clip(1 - f[..., 3:] + f[..., :3], 0, 1) for f in fr], args.out, scale=6, ms=cfg.period * 33)
        diffs = [float(((fr[i] - fr[(i + 1) % len(fr)]) ** 2).mean()) for i in range(len(fr))]
        print(f"{len(fr)} frames, consecutive-frame MSE {np.mean(diffs):.2e} (min {min(diffs):.2e})")
    elif args.cmd == "train":
        cfg = AnimConfig(**{k: getattr(args, k) for k in asdict(AnimConfig())})
        name = os.path.splitext(os.path.basename(cfg.gif))[0] if cfg.gif else f"{cfg.target}_swim"
        out = args.out or os.path.join(HERE, "runs", name)
        train(cfg, out)
        figures(out)
    elif args.cmd == "figures":
        figures(args.run)


if __name__ == "__main__":
    main()
