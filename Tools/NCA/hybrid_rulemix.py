"""Zero-training chimeras: the whale and the jelly are two separate rules, but both were fine-tuned
from the same 3D lizard, so their weights sit in one basin. This asks what grows when a body runs
BOTH rules: (a) a weight blend (1-t) whale + t jelly everywhere, (b) a spatial rule field, whale
update at the head end, jelly update at the tail end, a smooth ramp between.

    python3 Tools/NCA/hybrid_rulemix.py --out runs/rulemix
"""
import argparse
import os
import sys

import numpy as np
import torch
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from nca3d import CA3D, render  # noqa: E402
from hybrid3d import GRID, SEED_AT  # noqa: E402


def load(name):
    ca = CA3D(16, 128)
    ca.load_state_dict(torch.load(os.path.join(HERE, f"results/{name}3d_swim/model.pt"), map_location="cpu"))
    return ca


def blend(a, b, t):
    ca = CA3D(16, 128)
    sa, sb = a.state_dict(), b.state_dict()
    ca.load_state_dict({k: (1 - t) * sa[k] + t * sb[k] for k in sa})
    return ca


def seed():
    x = torch.zeros((1,) + GRID + (16,))
    x[0, SEED_AT[0], SEED_AT[1], SEED_AT[2], 3:] = 1
    return x


@torch.no_grad()
def field_step(cas, gs, x, fire):
    """One step where cell update = sum_i g_i(x) * update_i; alive masking as CA3D."""
    pre = CA3D.alive(x)
    y = CA3D.perceive(x)
    dx = sum(g * torch.nn.functional.linear(torch.relu(torch.nn.functional.linear(y, c.w1, c.b1)), c.w2, c.b2)
             for c, g in zip(cas, gs))
    x = x + dx * fire
    return x * (pre & CA3D.alive(x)).float()


@torch.no_grad()
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=os.path.join(HERE, "runs/rulemix"))
    ap.add_argument("--steps", type=int, default=400)
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    torch.set_num_threads(1)
    W, J = load("whale"), load("jelly")
    times = [60, 120, 200, 300, 400]
    rows = []
    for t in (0.0, 0.25, 0.5, 0.75, 1.0):
        torch.manual_seed(1)
        ca, x, ims = blend(W, J, t), seed(), []
        for s in range(1, a.steps + 1):
            x = ca(x)
            if s in times:
                ims.append(render(x[0], px=128))
        rows.append(np.concatenate(ims, 1)); print("blend", t, flush=True)
    # spatial: ramp along W (the whale's swim axis)
    D, H, Wd = GRID
    for lo, hi, tag in ((14, 30, "ramp"), (21, 23, "sharp")):
        g = torch.clamp((torch.arange(Wd).float() - lo) / max(1, hi - lo), 0, 1).view(1, 1, 1, Wd, 1)
        torch.manual_seed(1)
        x, ims = seed(), []
        for s in range(1, a.steps + 1):
            fire = (torch.rand(x.shape[:-1] + (1,)) <= 0.5).float()
            x = field_step([W, J], [1 - g, g], x, fire)
            if s in times:
                ims.append(render(x[0], px=128))
        rows.append(np.concatenate(ims, 1)); print("field", tag, flush=True)
    Image.fromarray((np.concatenate(rows, 0) * 255).astype(np.uint8)).save(os.path.join(a.out, "rulemix.png"))


if __name__ == "__main__":
    main()
