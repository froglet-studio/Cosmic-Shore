#!/usr/bin/env python3
"""Draw the showcase cell's snapshot (Docs/SWARM_FAUNA.md §26) as two orthogonal views with a legend.

    python3 render.py <snapshot.json> <out.png>

The snapshot is what `showcase.exe ... all <snap.json>` writes at t = 300 s for seed 1: swarm members (element, tier,
lineage), substrate agents, plant hearts, the threat grove (traps, slots, tubes, sclerotia), the fortress (workers,
walls), the thief nest and its hoard, the wearers (hearts and the prisms they wear), each pilot's wake and path.
Headless (matplotlib Agg). A reader tool: it writes one image and nothing else.
"""
import json
import sys

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402
from matplotlib.lines import Line2D  # noqa: E402
from matplotlib.patches import Circle  # noqa: E402

BG, INK, DIM = "#0d1117", "#e6edf3", "#30363d"
ELEMENT = {0: ("charge", "#f2c94c"), 1: ("mass", "#bb6bd9"), 2: ("space", "#2d9cdb"), 3: ("time", "#27ae60")}
SUBSTRATE = {"pack": "#ff7b72", "locust": "#d29922", "lurker": "#a5d6ff"}
PILOT = {"careless": "#ffa657", "skilled": "#7ee787", "raider": "#ff6ec7"}


def pts(rows, i, j):
    return [r[i] for r in rows], [r[j] for r in rows]


def view(ax, s, i, j, title, centre=None, half=None):
    ax.set_facecolor(BG)
    R = s["membrane"]
    ax.add_patch(Circle((0, 0), R, fill=False, ec="#58a6ff", lw=1.2, alpha=0.6))
    ax.add_patch(Circle((0, 0), s["nucleus"], fill=True, fc="#21262d", ec="#8b949e", lw=0.8))
    for b in s["bands"]:
        for r in (b["inner"], b["outer"]):
            ax.add_patch(Circle((0, 0), r, fill=False, ec=DIM, lw=0.6, ls="--"))
    # wake first (underneath everything)
    for k, (kind, col) in enumerate(PILOT.items()):
        tr = [t for t in s.get("trail", []) if t[3] == k]
        if tr:
            ax.scatter(*pts(tr, i, j), s=1.2, c=col, alpha=0.25, lw=0)
    g = s.get("grove")
    if g:
        if g.get("tubes"):
            ax.scatter(*pts(g["tubes"], i, j), s=3, c="#f0883e", alpha=0.45, lw=0)
        if g.get("slots"):
            ax.scatter(*pts(g["slots"], i, j), s=4, c="#3fb950", alpha=0.7, lw=0)
        if g.get("traps"):
            ax.scatter(*pts(g["traps"], i, j), s=36, marker="^", facecolors="none", edgecolors="#3fb950", lw=1.0)
        if g.get("sclerotia"):
            ax.scatter(*pts(g["sclerotia"], i, j), s=46, marker="D", c="#f0883e", edgecolors=INK, lw=0.5)
    plants = s.get("plants", [])
    for e, (name, col) in ELEMENT.items():
        p = [pl["at"] for pl in plants if pl["element"] == e]
        if p:
            ax.scatter(*pts(p, i, j), s=60, marker="*", c=col, edgecolors=INK, lw=0.4, alpha=0.9)
    sw = s.get("swarm", [])
    for e, (name, col) in ELEMENT.items():
        m = [r for r in sw if r[4] == e and r[5] != 1]
        if m:
            ax.scatter(*pts(m, i, j), s=7, c=col, alpha=0.8, lw=0)
    danger = [r for r in sw if r[5] == 1]
    if danger:
        ax.scatter(*pts(danger, i, j), s=9, c="#ff4d4d", alpha=0.9, lw=0)
    for key, col in SUBSTRATE.items():
        a = [r for r in s.get("substrate", []) if r[3] == key]
        if a:
            ax.scatter(*pts(a, i, j), s=10, c=col, marker="o", edgecolors=BG, lw=0.3)
    b = s.get("builders", {})
    if b.get("walls"):
        ax.scatter(*pts(b["walls"], i, j), s=4, marker="s", c="#c9d1d9", alpha=0.85, lw=0)
    if b.get("workers"):
        ax.scatter(*pts(b["workers"], i, j), s=8, c="#79c0ff", lw=0)
    if b.get("hoard"):
        ax.scatter(*pts(b["hoard"], i, j), s=8, marker="s", c="#e3b341", lw=0)
    if b.get("thieves"):
        ax.scatter(*pts(b["thieves"], i, j), s=12, marker="v", c="#e3b341", edgecolors=BG, lw=0.3)
    if b.get("nest"):
        n = b["nest"]
        ax.scatter([n[i]], [n[j]], s=90, marker="h", facecolors="none", edgecolors="#e3b341", lw=1.2)
    if b.get("worn"):
        ax.scatter(*pts(b["worn"], i, j), s=4, marker="s", c="#d2a8ff", alpha=0.8, lw=0)
    if b.get("wearer_hearts"):
        ax.scatter(*pts(b["wearer_hearts"], i, j), s=16, marker="o", c="#d2a8ff", edgecolors=INK, lw=0.4)
    for p in s.get("pilots", []):
        col = PILOT.get(p["kind"], INK)
        path = p.get("path", [])
        if path:
            ax.plot(*pts(path[-60:], i, j), c=col, lw=0.8, alpha=0.7)
        ax.scatter([p["at"][i]], [p["at"][j]], s=70, marker="P", c=col, edgecolors=INK, lw=0.6, zorder=5)
    lim = R * 1.04 if half is None else half
    cx, cy = (0.0, 0.0) if centre is None else centre
    ax.set_xlim(cx - lim, cx + lim)
    ax.set_ylim(cy - lim, cy + lim)
    ax.set_aspect("equal")
    axes = "xyz"
    ax.set_xlabel(f"{axes[i]} (u)", color=INK)
    ax.set_ylabel(f"{axes[j]} (u)", color=INK)
    ax.set_title(title, color=INK, fontsize=12)
    ax.tick_params(colors="#8b949e", labelsize=8)
    for sp in ax.spines.values():
        sp.set_color(DIM)


def legend_handles():
    H = []

    def dot(label, col, marker="o", size=7, face=True):
        H.append(Line2D([], [], ls="", marker=marker, ms=size, mfc=col if face else "none", mec=col, label=label))

    for e, (name, col) in ELEMENT.items():
        dot(f"swarm member - {name} region", col, size=5)
    dot("swarm member showing its DANGER plate", "#ff4d4d", size=5)
    for e, (name, col) in ELEMENT.items():
        dot(f"plant heart ({name})", col, "*", 10)
    for key, col in SUBSTRATE.items():
        dot(f"substrate {key}", col, size=6)
    dot("fortress wall prism", "#c9d1d9", "s", 5)
    dot("fortress worker", "#79c0ff", size=5)
    dot("thief nest", "#e3b341", "h", 9, face=False)
    dot("thief / hoard prism", "#e3b341", "v", 6)
    dot("wearer heart", "#d2a8ff", size=6)
    dot("prism worn by a wearer", "#d2a8ff", "s", 4)
    dot("snap trap (and its slots)", "#3fb950", "^", 8, face=False)
    dot("physarum tube / sclerotium", "#f0883e", "D", 6)
    for kind, col in PILOT.items():
        dot(f"pilot: {kind} (+ wake, path)", col, "P", 9)
    H.append(Line2D([], [], color="#58a6ff", lw=1.2, label="membrane (1200 u)"))
    H.append(Line2D([], [], color=DIM, lw=1, ls="--", label="swarm bands"))
    return H


def main():
    s = json.load(open(sys.argv[1]))
    out = sys.argv[2]
    fig = plt.figure(figsize=(28, 9.6), facecolor=BG)
    gs = fig.add_gridspec(1, 4, width_ratios=[1, 1, 1, 0.42], wspace=0.14)
    view(fig.add_subplot(gs[0]), s, 0, 2, "top view (x-z)")
    view(fig.add_subplot(gs[1]), s, 0, 1, "side view (x-y)")
    # close-up (top view) on the busiest place: the centroid of the swarm members, else of everything drawn
    sw = s.get("swarm", []) or [[0, 0, 0]]
    cx = sum(r[0] for r in sw) / len(sw)
    cz = sum(r[2] for r in sw) / len(sw)
    view(fig.add_subplot(gs[2]), s, 0, 2, f"close-up, top view, 600 u across at ({cx:.0f}, {cz:.0f})", (cx, cz), 300.0)
    lax = fig.add_subplot(gs[3])
    lax.axis("off")
    leg = lax.legend(handles=legend_handles(), loc="center left", frameon=False, fontsize=9.5, labelcolor=INK)
    for t in leg.get_texts():
        t.set_color(INK)
    n_sw = len(s.get("swarm", []))
    n_sub = len(s.get("substrate", []))
    b = s.get("builders", {})
    fig.suptitle(f"The Swarm cell, all together - t = {s['t']:.0f} s (seed 1): {n_sw} swarm members, {n_sub} substrate agents, "
                 f"{len(b.get('walls', []))} wall prisms, {len(b.get('wearer_hearts', []))} wearer heart(s) wearing {len(b.get('worn', []))} prisms",
                 color=INK, fontsize=14, y=0.97)
    fig.savefig(out, dpi=110, facecolor=BG, bbox_inches="tight")
    print(f"rendered {out}")


if __name__ == "__main__":
    main()
