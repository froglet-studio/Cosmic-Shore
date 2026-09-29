"""Growing Neural Cellular Automata — a faithful PyTorch port of Mordvintsev et al.,
"Growing Neural Cellular Automata", Distill 2020 (https://distill.pub/2020/growing-ca/).

This is a REPRODUCTION, not a variation. Every number below is the one the paper's
reference Colab uses; where PyTorch and TensorFlow differ the difference is named.

    python3 Tools/NCA/growing_nca.py train --experiment regenerating --target lizard
    python3 Tools/NCA/growing_nca.py figures --run Tools/NCA/runs/lizard_regenerating

The three experiments of the paper:
  growing       — train from the seed every step. Grows the shape, then is free to
                  explode or decay past the training horizon (the paper's Fig. "Experiment 1").
  persistent    — sample from a POOL of previous final states, replace the worst with a
                  seed. The pattern becomes an ATTRACTOR: it grows and then holds.
  regenerating  — the pool plus damage: the 3 best-of-8 pool samples get a random circle
                  erased before each step. The pattern learns to repair itself.

Model (per cell, 16 channels: RGBA + 12 hidden, shared weights everywhere):
  perceive  = [identity, Sobel_x, Sobel_y] depthwise over all 16 channels -> 48
  update    = 1x1 conv 48->128, ReLU, 1x1 conv 128->16 (final layer ZERO-initialised,
              so the untrained CA is the identity map)
  stochastic= each cell applies its update with p = 0.5 per step (no global clock)
  alive     = a cell is alive iff max alpha over its 3x3 neighbourhood > 0.1, tested
              BEFORE and AFTER the update; dead cells are zeroed.
"""
from __future__ import annotations

import argparse
import json
import math
import os
import time
from dataclasses import dataclass, asdict

import numpy as np
import torch
import torch.nn as nn
import torch.nn.functional as F
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
NOTO_FONT = "/usr/share/fonts/truetype/noto/NotoColorEmoji.ttf"

EMOJI = {
    # The paper's gallery (Fig. 1 + the Colab's picker).
    "lizard": "\U0001F98E",
    "butterfly": "\U0001F98B",
    "mushroom": "\U0001F344",
    "cactus": "\U0001F335",
    "hibiscus": "\U0001F33A",
    "herb": "\U0001F33F",
    "seedling": "\U0001F331",
    "blossom": "\U0001F338",
}


# ---------------------------------------------------------------- target ---

def load_emoji(name: str, max_size: int = 40) -> np.ndarray:
    """Premultiplied RGBA float32 [H, W, 4] in [0, 1].

    The paper fetches noto-emoji/png/128/emoji_uXXXX.png and thumbnails it to 40px.
    GitHub is not reachable from every build machine, so this renders the same artwork
    from the Noto Color Emoji font (the PNGs are generated from it), centres it on the
    same 128x128 canvas, and thumbnails it exactly as the Colab does.
    A path to any RGBA image is also accepted.
    """
    if os.path.isfile(name):
        img = Image.open(name).convert("RGBA")
    else:
        ch = EMOJI[name]
        font = ImageFont.truetype(NOTO_FONT, 109, layout_engine=ImageFont.Layout.BASIC)
        big = Image.new("RGBA", (160, 160), (0, 0, 0, 0))
        ImageDraw.Draw(big).text((0, 0), ch, font=font, embedded_color=True)
        big = big.crop(big.getbbox())
        img = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
        img.paste(big, ((128 - big.width) // 2, (128 - big.height) // 2))
    img.thumbnail((max_size, max_size), Image.LANCZOS)  # Colab: PIL.Image.ANTIALIAS
    a = np.asarray(img, dtype=np.float32) / 255.0
    a[..., :3] *= a[..., 3:]  # premultiply
    return a


# ----------------------------------------------------------------- model ---

class CAModel(nn.Module):
    """State layout is CHANNELS-LAST: x is [B, H, W, C]. Weights are the paper's two 1x1
    convolutions stored as matrices (w1 [hidden, 3C], w2 [C, hidden]).

    Sparse, but EXACT: a cell that is dead before the update is zeroed by the alive mask
    whatever its update was, so the MLP is evaluated only on pre-alive cells and scattered
    back. `dense_reference_step` is the literal paper formulation and the self-test holds
    the two equal."""

    def __init__(self, channel_n: int = 16, hidden: int = 128, fire_rate: float = 0.5):
        super().__init__()
        self.channel_n = channel_n
        self.hidden = hidden
        self.fire_rate = fire_rate
        self.w1 = nn.Parameter(torch.empty(hidden, channel_n * 3))
        self.b1 = nn.Parameter(torch.zeros(hidden))
        self.w2 = nn.Parameter(torch.zeros(channel_n, hidden))  # zero init: untrained CA = identity
        self.b2 = nn.Parameter(torch.zeros(channel_n))
        # Keras Conv2D default is glorot_uniform (fan_in 48, fan_out 128) + zero bias.
        nn.init.xavier_uniform_(self.w1)

    @staticmethod
    def perceive(x: torch.Tensor, angle: float = 0.0) -> torch.Tensor:
        """[B,H,W,C] -> [B,H,W,3C], feature index 3*c + k for k in (identity, sobel_x, sobel_y).
        Sobel = outer([1,2,1],[-1,0,1]) / 8, zero padded ('SAME') — the grid is not toroidal."""
        p = F.pad(x, (0, 0, 1, 1, 1, 1))
        tl, t, tr = p[:, :-2, :-2], p[:, :-2, 1:-1], p[:, :-2, 2:]
        l, r = p[:, 1:-1, :-2], p[:, 1:-1, 2:]
        bl, b, br = p[:, 2:, :-2], p[:, 2:, 1:-1], p[:, 2:, 2:]
        sx = ((tr + 2 * r + br) - (tl + 2 * l + bl)) / 8.0
        sy = ((bl + 2 * b + br) - (tl + 2 * t + tr)) / 8.0
        if angle:
            c, s = math.cos(angle), math.sin(angle)
            sx, sy = c * sx - s * sy, s * sx + c * sy
        return torch.stack([x, sx, sy], -1).flatten(-2)

    @staticmethod
    def alive(x: torch.Tensor) -> torch.Tensor:
        """[B,H,W,1] bool: max alpha over the 3x3 neighbourhood > 0.1."""
        a = x[..., 3].unsqueeze(1)
        return (F.max_pool2d(a, 3, stride=1, padding=1) > 0.1).squeeze(1).unsqueeze(-1)

    def forward(self, x, fire_rate=None, angle=0.0, step_size=1.0, fire_mask=None):
        B, H, W, C = x.shape
        pre = self.alive(x)
        y = self.perceive(x, angle).reshape(-1, 3 * C)
        rate = self.fire_rate if fire_rate is None else fire_rate
        fire = fire_mask if fire_mask is not None else torch.rand(B, H, W, 1, device=x.device) <= rate
        # Only cells that are alive AND fire can change; everything else contributes 0.
        idx = (pre & fire).reshape(-1).nonzero().squeeze(1)
        h = torch.relu(F.linear(y.index_select(0, idx), self.w1, self.b1))
        d = F.linear(h, self.w2, self.b2) * step_size
        dx = torch.zeros(B * H * W, C, dtype=x.dtype, device=x.device).index_copy(0, idx, d)
        x = x + dx.view(B, H, W, C)
        post = self.alive(x)
        return x * (pre & post).to(x.dtype)

    def dense_reference_step(self, x, fire_mask):
        """The paper's literal formulation (dense update everywhere), for the self-test."""
        pre = self.alive(x)
        y = self.perceive(x)
        dx = F.linear(torch.relu(F.linear(y, self.w1, self.b1)), self.w2, self.b2)
        x = x + dx * fire_mask.to(x.dtype)
        return x * (pre & self.alive(x)).to(x.dtype)


def make_seed(n, h, w, c=16):
    x = torch.zeros(n, h, w, c)
    x[:, h // 2, w // 2, 3:] = 1.0
    return x


def circle_damage(n, h, w, rng: np.random.Generator) -> torch.Tensor:
    """1 - circle mask, exactly the Colab's make_circle_masks."""
    x = np.linspace(-1.0, 1.0, w)[None, None, :]
    y = np.linspace(-1.0, 1.0, h)[None, :, None]
    center = rng.random((2, n, 1, 1)) - 0.5
    r = rng.random((n, 1, 1)) * 0.3 + 0.1
    xx, yy = (x - center[0]) / r, (y - center[1]) / r
    mask = (xx * xx + yy * yy < 1.0).astype(np.float32)
    return torch.from_numpy(1.0 - mask)[..., None]


# --------------------------------------------------------------- training ---

@dataclass
class Config:
    target: str = "lizard"
    experiment: str = "regenerating"   # growing | persistent | regenerating
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

    @property
    def use_pool(self):
        return self.experiment in ("persistent", "regenerating")

    @property
    def damage(self):
        return self.damage_n if self.experiment == "regenerating" else 0


def to_rgba(x):
    return x[..., :4]


def to_rgb_image(x: torch.Tensor) -> np.ndarray:
    """Premultiplied state -> RGB over white, as the Colab's to_rgb: 1 - a + rgb."""
    rgba = to_rgba(x).clamp(0, 1)
    rgb = 1.0 - rgba[..., 3:4] + rgba[..., :3]
    return rgb.clamp(0, 1).detach().cpu().numpy()


def train(cfg: Config, out_dir: str):
    os.makedirs(out_dir, exist_ok=True)
    if cfg.threads:
        torch.set_num_threads(cfg.threads)
    torch.manual_seed(cfg.seed)
    rng = np.random.default_rng(cfg.seed)

    target = load_emoji(cfg.target, cfg.target_size)
    p = cfg.target_padding
    target = np.pad(target, ((p, p), (p, p), (0, 0)))
    h, w = target.shape[:2]
    Image.fromarray((np.clip(1 - target[..., 3:] + target[..., :3], 0, 1) * 255).astype(np.uint8)).save(
        os.path.join(out_dir, "target.png"))
    tgt = torch.from_numpy(target)[None]  # [1, H, W, 4]

    ca = CAModel(cfg.channel_n, cfg.hidden, cfg.fire_rate)
    # Colab: Adam(PiecewiseConstantDecay([2000], [2e-3, 2e-4])). Keras eps=1e-7.
    opt = torch.optim.Adam(ca.parameters(), lr=cfg.lr, eps=1e-7)
    sched = torch.optim.lr_scheduler.MultiStepLR(opt, [cfg.lr_drop_step], 0.1)

    seed = make_seed(1, h, w, cfg.channel_n)
    pool = seed.repeat(cfg.pool_size, 1, 1, 1) if cfg.use_pool else None

    def per_sample_loss(x):
        return ((to_rgba(x) - tgt) ** 2).mean(dim=(1, 2, 3))

    log = []
    t0 = time.time()
    with open(os.path.join(out_dir, "config.json"), "w") as f:
        json.dump(asdict(cfg), f, indent=2)

    for step in range(cfg.steps + 1):
        if cfg.use_pool:
            idx = rng.choice(cfg.pool_size, cfg.batch_size, replace=False)
            x0 = pool[idx].clone()
            with torch.no_grad():
                order = torch.argsort(per_sample_loss(x0), descending=True)
            x0, idx = x0[order], idx[order.numpy()]
            x0[:1] = seed  # the worst sample is replaced by a fresh seed
            if cfg.damage:
                x0[-cfg.damage:] *= circle_damage(cfg.damage, h, w, rng)
        else:
            x0 = seed.repeat(cfg.batch_size, 1, 1, 1)

        iter_n = int(rng.integers(cfg.min_iter, cfg.max_iter + 1))  # tf.random.uniform([], 64, 96, int32)
        x = x0
        for _ in range(iter_n):
            x = ca(x)
        loss = per_sample_loss(x).mean()

        opt.zero_grad(set_to_none=True)
        loss.backward()
        # Per-variable gradient normalisation — the paper's key stabiliser.
        for prm in ca.parameters():
            if prm.grad is not None:
                prm.grad /= prm.grad.norm() + 1e-8
        opt.step()
        sched.step()

        if cfg.use_pool:
            pool[idx] = x.detach()

        L = float(loss)
        log.append(L)
        if step % 50 == 0:
            dt = time.time() - t0
            print(f"[{cfg.target}/{cfg.experiment}] step {step:5d}  loss {L:.5f}  "
                  f"log10 {math.log10(L):+.3f}  {dt/(step+1):.2f}s/it  elapsed {dt/60:.1f}m", flush=True)
        if step % 500 == 0 or step == cfg.steps:
            torch.save(ca.state_dict(), os.path.join(out_dir, "model.pt"))
            np.save(os.path.join(out_dir, "loss.npy"), np.array(log))
            if cfg.use_pool:
                save_grid(x.detach(), os.path.join(out_dir, f"batch_{step:05d}.png"))

    export_weights_json(ca, os.path.join(out_dir, "weights.json"), cfg)
    return ca


def save_grid(x, path, scale=4):
    imgs = to_rgb_image(x)
    row = np.concatenate(list(imgs), axis=1)
    im = Image.fromarray((row * 255).astype(np.uint8))
    im.resize((im.width * scale, im.height * scale), Image.NEAREST).save(path)


def export_weights_json(ca: CAModel, path: str, cfg: Config):
    """Flat weights for the in-browser / in-engine runner."""
    w1 = ca.w1.detach().numpy()  # [128, 48]
    w2 = ca.w2.detach().numpy()  # [16, 128]
    data = {
        "channel_n": cfg.channel_n, "hidden": cfg.hidden, "fire_rate": cfg.fire_rate,
        "perception_order": "per-channel [identity, sobel_x, sobel_y] -> index 3*c+k",
        "w1": w1.round(6).tolist(), "b1": ca.b1.detach().numpy().round(6).tolist(),
        "w2": w2.round(6).tolist(), "b2": ca.b2.detach().numpy().round(6).tolist(),
        "target": cfg.target, "experiment": cfg.experiment,
        "grid": cfg.target_size + 2 * cfg.target_padding,
    }
    with open(path, "w") as f:
        json.dump(data, f)


# ---------------------------------------------------------------- figures ---

def load_run(run_dir):
    with open(os.path.join(run_dir, "config.json")) as f:
        raw = json.load(f)
    cfg = Config(**raw)
    ca = CAModel(cfg.channel_n, cfg.hidden, cfg.fire_rate)
    ca.load_state_dict(torch.load(os.path.join(run_dir, "model.pt")))
    ca.eval()
    return cfg, ca


@torch.no_grad()
def rollout(ca, x, n, record=(), angle=0.0):
    frames = {}
    for i in range(n + 1):
        if i in record:
            frames[i] = x.clone()
        if i < n:
            x = ca(x, angle=angle)
    return x, frames


@torch.no_grad()
def figures(run_dir, seed=1):
    torch.manual_seed(seed)
    cfg, ca = load_run(run_dir)
    size = cfg.target_size + 2 * cfg.target_padding
    fig_dir = os.path.join(run_dir, "figures")
    os.makedirs(fig_dir, exist_ok=True)
    target = torch.from_numpy(np.pad(load_emoji(cfg.target, cfg.target_size),
                                     ((cfg.target_padding,) * 2, (cfg.target_padding,) * 2, (0, 0))))[None]

    def err(x):
        return float(((to_rgba(x) - target) ** 2).mean())

    # 1. Growth strip + long-horizon stability (the paper's "what happens past step 96").
    marks = [0, 10, 20, 30, 40, 50, 60, 72, 96, 200, 500, 1000, 2000, 4000]
    x = make_seed(1, size, size, cfg.channel_n)
    curve = []
    frames = {}
    for i in range(max(marks) + 1):
        if i in marks:
            frames[i] = x.clone()
        curve.append(err(x))
        x = ca(x)
    strip = torch.cat([frames[m] for m in marks])
    save_grid(strip, os.path.join(fig_dir, "growth_strip.png"), scale=3)
    np.save(os.path.join(fig_dir, "stability_curve.npy"), np.array(curve))

    # 2. Animated growth (GIF).
    x = make_seed(1, size, size, cfg.channel_n)
    gif = []
    for i in range(400):
        if i < 120 or i % 4 == 0:
            gif.append(to_rgb_image(x)[0])
        x = ca(x)
    write_gif(gif, os.path.join(fig_dir, "growth.gif"))

    # 3. Regeneration: grow 200 steps, then cut, as in the paper's damage figure.
    x = make_seed(1, size, size, cfg.channel_n)
    for _ in range(200):
        x = ca(x)
    cuts = []
    H = W = size
    for kind in ("left", "top", "bottom_right", "circle"):
        y = x.clone()
        if kind == "left":
            y[:, :, : W // 2] = 0
        elif kind == "top":
            y[:, : H // 2] = 0
        elif kind == "bottom_right":
            y[:, H // 2:, W // 2:] = 0
        else:
            y *= circle_damage(1, H, W, np.random.default_rng(seed))
        seq = [y.clone()]
        rec = []
        for i in range(300):
            y = ca(y)
            rec.append(err(y))
            if i + 1 in (10, 25, 50, 100, 200, 300):
                seq.append(y.clone())
        cuts.append((kind, torch.cat(seq), rec))
    rows = [np.concatenate(list(to_rgb_image(s)), axis=1) for _, s, _ in cuts]
    im = Image.fromarray((np.concatenate(rows, axis=0) * 255).astype(np.uint8))
    im.resize((im.width * 3, im.height * 3), Image.NEAREST).save(os.path.join(fig_dir, "regeneration.png"))

    # 4. Rotation: the perception kernel rotated by 45 deg grows a rotated pattern
    #    (the paper's "Rotating the perceptive field" section).
    rot = []
    for deg in (0, 45, 90, 135):
        x = make_seed(1, size, size, cfg.channel_n)
        for _ in range(200):
            x = ca(x, angle=math.radians(deg))
        rot.append(x)
    save_grid(torch.cat(rot), os.path.join(fig_dir, "rotation.png"), scale=3)

    summary = {
        "error_at": {str(m): curve[m] for m in marks},
        "regeneration_error_after_300": {k: rec[-1] for k, _, rec in cuts},
        "regeneration_error_after_50": {k: rec[49] for k, _, rec in cuts},
    }
    with open(os.path.join(fig_dir, "summary.json"), "w") as f:
        json.dump(summary, f, indent=2)
    print(json.dumps(summary, indent=2))


def write_gif(frames, path, scale=4, ms=33):
    ims = []
    for f in frames:
        im = Image.fromarray((f * 255).astype(np.uint8))
        ims.append(im.resize((im.width * scale, im.height * scale), Image.NEAREST))
    ims[0].save(path, save_all=True, append_images=ims[1:], duration=ms, loop=0)


# -------------------------------------------------------------------- cli ---

def selftest():
    """The sparse update must equal the paper's dense formulation, value AND gradient,
    and a rotation of 0 must be the identity on perception."""
    torch.manual_seed(0)
    ca = CAModel()
    nn.init.normal_(ca.w2, std=0.05)
    x = torch.rand(3, 20, 20, 16)
    x[..., 3] = (x[..., 3] > 0.7).float() * x[..., 3]  # patchy life, some dead regions
    fire = torch.rand(3, 20, 20, 1) <= 0.5
    a = ca(x, fire_mask=fire)
    b = ca.dense_reference_step(x, fire)
    d = float((a - b).abs().max())
    ga = torch.autograd.grad(a.square().sum(), ca.w1)[0]
    gb = torch.autograd.grad(ca.dense_reference_step(x, fire).square().sum(), ca.w1)[0]
    dg = float((ga - gb).abs().max() / gb.abs().max())
    assert d < 1e-5, f"sparse != dense: {d}"
    assert dg < 1e-4, f"sparse grad != dense grad: {dg}"
    # Negative control: a sparse step that skipped the DEAD-cell zeroing would differ.
    leak = x + (F.linear(torch.relu(F.linear(ca.perceive(x), ca.w1, ca.b1)), ca.w2, ca.b2) * fire)
    assert float((leak - b).abs().max()) > 1e-3, "negative control did not fire"
    assert float((ca.perceive(x, 0.0) - ca.perceive(x, 2 * math.pi)).abs().max()) < 1e-5
    # Sobel orientation: a ramp increasing to the right has positive sobel_x, zero sobel_y.
    ramp = torch.arange(8.0).view(1, 1, 8, 1).expand(1, 8, 8, 16)
    pr = ca.perceive(ramp)[0, 4, 4].view(16, 3)
    assert abs(float(pr[0, 1]) - 1.0) < 1e-6 and abs(float(pr[0, 2])) < 1e-6, pr[0]
    print(f"selftest OK  value diff {d:.2e}  grad rel diff {dg:.2e}")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    t = sub.add_parser("train")
    for k, v in asdict(Config()).items():
        t.add_argument(f"--{k.replace('_', '-')}", type=type(v), default=v)
    t.add_argument("--out", default=None)
    fg = sub.add_parser("figures")
    fg.add_argument("--run", required=True)
    b = sub.add_parser("bench")
    b.add_argument("--threads", type=int, default=0)
    sub.add_parser("selftest")
    args = ap.parse_args()

    if args.cmd == "train":
        kw = {k: getattr(args, k) for k in asdict(Config())}
        cfg = Config(**kw)
        out = args.out or os.path.join(HERE, "runs", f"{cfg.target}_{cfg.experiment}")
        train(cfg, out)
        figures(out)
    elif args.cmd == "figures":
        figures(args.run)
    elif args.cmd == "bench":
        if args.threads:
            torch.set_num_threads(args.threads)
        ca = CAModel()
        nn.init.normal_(ca.w2, std=0.01)
        opt = torch.optim.Adam(ca.parameters(), 2e-3)
        # A realistic trained-pool batch: the lizard's own footprint is alive.
        t = np.pad(load_emoji("lizard"), ((16, 16), (16, 16), (0, 0)))
        x0 = torch.zeros(8, 72, 72, 16)
        x0[..., :4] = torch.from_numpy(t)
        x0[..., 4:] = x0[..., 3:4] * 0.5
        print(f"alive fraction {float(CAModel.alive(x0).float().mean()):.3f}")
        for rep in range(3):
            t0 = time.time()
            x = x0
            for _ in range(80):
                x = ca(x)
            loss = x[..., :4].pow(2).mean()
            opt.zero_grad(); loss.backward(); opt.step()
            print(f"threads={torch.get_num_threads()} 80 steps fwd+bwd: {time.time()-t0:.2f}s")
    elif args.cmd == "selftest":
        selftest()

if __name__ == "__main__":
    main()
