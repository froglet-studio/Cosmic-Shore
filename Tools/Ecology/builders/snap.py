"""Quick 3-view PNG of a species' structure (for the log and for judging 'more interesting')."""
import numpy as np
import matplotlib; matplotlib.use("Agg")
import matplotlib.pyplot as plt


def snap(path, panels, title="", slab=None):
    """panels: list of (label, points (n,3), colors (n,3) or None, centre)"""
    fig, ax = plt.subplots(len(panels), 3, figsize=(9, 3 * len(panels)), squeeze=False)
    for r, (lab, P, C, c0) in enumerate(panels):
        P = np.asarray(P) - c0 if len(P) else np.zeros((0, 3))
        for j, (a, b) in enumerate(((0, 1), (0, 2), (1, 2))):
            x = ax[r, j]; x.set_facecolor("#0b0d14")
            Q = P
            if slab is not None and len(P):
                o = 3 - a - b; Q = P[np.abs(P[:, o]) < slab]
            if len(Q):
                P_ = Q
                x.scatter(P_[:, a], P_[:, b], s=6, c="#ff8c40" if C is None or slab is not None else C, marker="s", linewidths=0)
            if False:
                x.scatter(P[:, a], P[:, b], s=6, c=C if C is not None else "#ff8c40", marker="s", linewidths=0)
            x.set_aspect("equal"); x.set_xticks([]); x.set_yticks([])
            x.set_title(f"{lab}  {'xyz'[a]}{'xyz'[b]}{' slab' if slab else ''}", fontsize=8)
    fig.suptitle(title, fontsize=10); fig.tight_layout()
    fig.savefig(path, dpi=90, facecolor="white"); plt.close(fig)


def snap3d(path, panels, title="", elev=18, azims=(30, 120)):
    """panels: (label, points, colours). One 3D view per azimuth, per panel."""
    from mpl_toolkits.mplot3d import Axes3D  # noqa: F401
    fig = plt.figure(figsize=(4.2 * len(azims), 4.0 * len(panels)))
    for r, (lab, P, C) in enumerate(panels):
        P = np.asarray(P)
        for j, az in enumerate(azims):
            ax = fig.add_subplot(len(panels), len(azims), r * len(azims) + j + 1, projection="3d")
            if len(P):
                ax.scatter(P[:, 0], P[:, 1], P[:, 2], c=C, s=14, marker="s", depthshade=True, linewidths=0)
                m = np.abs(P).max(); ax.set_xlim(-m, m); ax.set_ylim(-m, m); ax.set_zlim(-m, m)
            ax.view_init(elev, az); ax.set_axis_off(); ax.set_title(lab, fontsize=8)
    fig.suptitle(title, fontsize=10); fig.tight_layout(); fig.savefig(path, dpi=80); plt.close(fig)
