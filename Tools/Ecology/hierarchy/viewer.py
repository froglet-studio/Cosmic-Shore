"""Record a hierarchical run around pilots and build a self-contained viewer of expand / collapse.

    python -m hierarchy.viewer [--T 90] [--pilots 2] [--out results/hierarchy_viewer.html]

What you see:
  dim green/red puffs   IMPOSTORS: each region's macro population drawn as a few representatives, each sized by
                        the headcount it stands for (drawn volume = bodies it stands for)
  bright points         AGENTS: individuals simulated near a pilot; a fresh one is drawn sliding out of the
                        representative it came from (emerge), a newborn grows from nothing (bloom)
  white diamonds        pilots (with a forward cone of what they can SEE)
  faint boxes           HOT regions (expanded)
  floor tint            flora per region (the shared field)
Frames are packed binary (int16 positions, base64) and agents are matched by id between frames and lerped, so the
playback is smooth at a 5 fps record rate. Drawn in the shared 3D viewer (common/viewer.py): cameras 1-4, click a
body to follow it, space pauses; I/G/B/L toggle impostors/agents/hot boxes/flora.
    python -m hierarchy.viewer --retemplate      # re-render results/hierarchy_viewer.html from its recorded run
"""
from __future__ import annotations

import argparse
import base64
import json
import os

import numpy as np


OUT = os.path.join(os.path.dirname(__file__), "results")


def b64(a):
    return base64.b64encode(np.ascontiguousarray(a).tobytes()).decode()


def record(T=60.0, pilots=2, seed=11, every=4, imp_every=5, n_herb=45000, n_pred=3000, P=None):
    from .params import Params                  # the sim needs scipy; --retemplate does not
    from .sim import HierSim, Pilot
    P = P or Params()
    sim = HierSim(P, seed=seed)
    sim.populate(n_herb, n_pred)
    for i in range(pilots):
        p = sim.add_pilot(Pilot.wanderer(speed=110.0 + 40 * i))
        p.turn = 0.8
    q = 32767.0 / sim.W.R
    frames = []
    stats = []
    pop_metric = []                 # drawn body volume near each pilot, per frame (continuity check)
    for i in range(int(T / P.dt_micro)):
        sim.step()
        if i % every:
            continue
        out = {}
        sim.render(out)
        A = sim.A
        ag = out.get("agents")
        n = A.n
        f = dict(t=round(sim.t, 2))
        if ag is not None and n:
            f["aid"] = b64(A.view("id").astype(np.uint32))
            f["apos"] = b64(np.round(ag["pos"] * q).astype(np.int16))
            f["asp"] = b64(A.view("sp").astype(np.uint8))
            f["asz"] = b64(np.round(np.asarray(ag["size"]) * 10).astype(np.uint8))
        if len(frames) % imp_every == 0:        # impostors drift slowly: keyframe them
            imp = []
            for k, name in enumerate(("impostor_herb", "impostor_pred")):
                b = out.get(name)
                if b is None:
                    imp.append(("", "")); continue
                imp.append((b64(np.round(b["pos"] * q).astype(np.int16)),
                            b64(np.clip(np.round(np.asarray(b["size"]) * 4), 0, 65535).astype(np.uint16))))
            f["imp"] = imp
        f["pil"] = [np.round(p.pos, 1).tolist() + np.round(p.vel / max(np.linalg.norm(p.vel), 1e-9), 3).tolist()
                    for p in sim.arena.pilots]
        f["hot"] = b64(np.packbits(sim.hot))
        fl = (sim.W.F.sum(1) / max(sim.W.vox_ok.sum(1).max(), 1) / P.flora_cap * 255)
        f["flora"] = b64(np.clip(fl, 0, 255).astype(np.uint8))
        frames.append(f)
        s = sim.summary()
        stats.append(dict(t=round(sim.t, 1), herb=s["herb"]["count"], pred=s["pred"]["count"], agents=int(A.n),
                          hot=int(sim.hot.sum()), expand=int(sim.events["expand"]), absorb=int(sim.events["absorb"])))
        # continuity metric: drawn volume (size^3) within 450 u of each pilot, agents + impostors
        row = []
        for p in sim.arena.pilots:
            tot = 0.0
            for b in out.values():
                d = np.linalg.norm(np.asarray(b["pos"]) - p.pos, axis=1)
                tot += float((np.asarray(b["size"]) ** 3 * (d < 450)).sum())
            row.append(tot)
        pop_metric.append(row)
    meta = dict(R=sim.W.R, L=P.L, centers=np.round(sim.W.centers, 1).tolist(), q=q, every_s=every * P.dt_micro,
                stats=stats, events=dict((k, float(v)) for k, v in sim.events.items()))
    return meta, frames, np.array(pop_metric)


PANEL = os.path.join(os.path.dirname(__file__), "viewer_panel.html")


def render(meta, frames, label="hierarchy run"):
    """The shared 3D viewer (common/viewer.py: lit bodies, fog, the vessel + trail, HUD, minimap, cameras 1-4,
    click-to-follow) with the hierarchy's levels drawn by hooks (viewer_panel.html). Frames stay packed binary."""
    import sys
    sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
    from common.viewer import TPL as VTPL
    run = dict(fmt="hier", label=label, meta=meta, frames=frames)
    panel = open(PANEL, encoding="utf-8").read()
    return (VTPL.replace('<div id="info"></div>', panel, 1).replace("__TITLE__", "Hierarchical Ecology Viewer")
            .replace("__DATA__", json.dumps([run], separators=(",", ":"))))


def build(out, meta, frames):
    with open(out, "w", encoding="utf-8") as fh:
        fh.write(render(meta, frames))
    return os.path.getsize(out)


def extract(path):
    """(meta, frames) recorded in a viewer built by ANY version of this file (old: const D = {...}; new: RUNS)."""
    import re
    s = open(path, encoding="utf-8").read()
    m = re.search(r"const D = (\{.*?\});\n", s, re.S)
    if m:
        d = json.loads(m.group(1))
        return d["meta"], d["frames"]
    r = json.loads(re.search(r"const RUNS = (\[.*?\]);\n", s, re.S).group(1))[0]
    return r["meta"], r["frames"]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--T", type=float, default=60.0)
    ap.add_argument("--pilots", type=int, default=2)
    ap.add_argument("--out", default=os.path.join(OUT, "hierarchy_viewer.html"))
    ap.add_argument("--retemplate", action="store_true", help="re-render --out from the run already recorded in it")
    a = ap.parse_args()
    os.makedirs(OUT, exist_ok=True)
    if a.retemplate:
        meta, frames = extract(a.out)
        print(build(a.out, meta, frames), "bytes ->", a.out)
        return
    meta, frames, popm = record(a.T, a.pilots)
    meta["drawn_volume_near_pilot"] = popm[:, 0].round(1).tolist()
    print(build(a.out, meta, frames), "bytes ->", a.out)
    d = np.abs(np.diff(popm, axis=0)) / np.maximum(popm[:-1], 1)
    print("drawn-volume near pilot: max frame-to-frame relative change", float(d.max()), "p99", float(np.quantile(d, 0.99)))


if __name__ == "__main__":
    main()
