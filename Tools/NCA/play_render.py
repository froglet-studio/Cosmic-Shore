"""Render a predator pass (results/<x>/predator.json, written by play_swarm.publish) as a PNG strip per
body plan: the swarm seen side-on to the predator's path, the predator a circle moving left to right.
Colour = element (Charge gold, Mass red, Space blue, Time green). A slab of +-depth around the path is
shown, so a gap opening around the predator is visible instead of hidden behind the rest of the body.

    python Tools/NCA/play_render.py Tools/NCA/results/play/predator.json Tools/NCA/results/play/predator_pass.png
"""
import base64
import json
import sys

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402
import numpy as np  # noqa: E402

COL = np.array([[0.95, 0.75, 0.1], [0.85, 0.2, 0.2], [0.2, 0.45, 0.95], [0.2, 0.75, 0.3]])


def unpack(d, scale):
    a = np.frombuffer(base64.b64decode(d["b64"]), "<i2").reshape(d["shape"]).astype(np.float32)
    return a / np.array(scale, np.float32)


def main(src, dst, cols=6, depth=6.0):
    P = json.load(open(src))
    kinds = [k for k in ("mass", "space", "charge", "time") if k in P]
    fig, axes = plt.subplots(len(kinds), cols, figsize=(cols * 2.6, len(kinds) * 2.7))
    for r, k in enumerate(kinds):
        fr = unpack(P[k], P["scale"]); n = P[k]["n"]; tr = P[k]["predator"]
        on = [i for i, t in enumerate(tr) if t is not None]
        if len(on) < 2:
            continue
        p0, p1 = np.array(tr[on[0]][:3]), np.array(tr[on[-1]][:3])
        ax_u = (p1 - p0) / np.linalg.norm(p1 - p0)
        ref = np.array([0, 0, 1.0]) if abs(ax_u[2]) < 0.9 else np.array([1.0, 0, 0])
        ax_v = np.cross(ax_u, ref); ax_v /= np.linalg.norm(ax_v)
        ax_w = np.cross(ax_u, ax_v)
        mid = (p0 + p1) / 2
        # frames: just before, four during, just after, regrown
        picks = [max(0, on[0] - 1)] + [on[int(j)] for j in np.linspace(len(on) * 0.3, len(on) * 0.75, cols - 3)] + \
                [min(len(fr) - 1, on[-1] + 2), len(fr) - 1]
        for c, i in enumerate(picks[:cols]):
            ax = axes[r, c]
            x = fr[i, :n[i]]
            rel = x[:, :3] - mid
            sl = np.abs(rel @ ax_w) < depth
            ax.scatter(rel[sl] @ ax_u, rel[sl] @ ax_v, s=6, c=COL[x[sl, 3].astype(int).clip(0, 3)], linewidths=0)
            if tr[i] is not None:
                q = np.array(tr[i][:3]) - mid
                ax.add_patch(plt.Circle((q @ ax_u, q @ ax_v), tr[i][3], fill=False, color="k", lw=1.2))
            ax.set_xlim(-45, 45); ax.set_ylim(-45, 45); ax.set_aspect("equal"); ax.set_xticks([]); ax.set_yticks([])
            lab = "before" if c == 0 else ("regrown" if i == len(fr) - 1 else ("after" if tr[i] is None else "pass"))
            ax.set_title(f"{k} {lab} n={n[i]}", fontsize=8)
    fig.tight_layout()
    fig.savefig(dst, dpi=90)
    print("wrote", dst)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
