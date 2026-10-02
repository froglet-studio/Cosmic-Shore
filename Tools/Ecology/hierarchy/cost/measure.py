"""Cost of the hierarchy at game scale: a 1200-u cell, 50k+ macro individuals, 1-4 pilots.

For each pilot count it runs the Python reference for T seconds and records how many agents are expanded (mean,
p95, max), how many regions are hot, and the Python wall time per component; then it projects a compiled
(Burst-shaped) cost from the C kernels' measured ns/agent and ns/cohort (kernels.c, compiled and run here).

    python -m hierarchy.cost.measure [--T 60]
"""
from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import tempfile

import numpy as np

from ..params import Params
from ..sim import HierSim, Pilot

HERE = os.path.dirname(__file__)
OUT = os.path.join(HERE, "..", "results")


def kernel_costs():
    exe = os.path.join(tempfile.gettempdir(), "eco_kernels")
    subprocess.run(["cc", "-O2", "-march=native", "-o", exe, os.path.join(HERE, "kernels.c"), "-lm"], check=True)
    txt = subprocess.run([exe], capture_output=True, text=True, check=True).stdout
    micro = {int(a): float(b) for a, b in re.findall(r"agents\s+(\d+)\s+[\d.]+ ms/tick\s+([\d.]+) ns/agent", txt)}
    macro_ms = float(re.search(r"([\d.]+) ms per macro step", txt).group(1))
    return micro, macro_ms, txt


def run(npilots, T=60.0, seed=13, n_herb=50000, n_pred=4000):
    P = Params()
    sim = HierSim(P, seed=seed)
    sim.populate(n_herb, n_pred)
    for i in range(npilots):
        p = sim.add_pilot(Pilot.wanderer(speed=120.0 + 25 * i)); p.turn = 1.2
    agents, hot = [], []
    for _ in range(int(T / P.dt_micro)):
        sim.step()
        agents.append(sim.A.n); hot.append(int(sim.hot.sum()))
    tm = sim.timing
    st = tm["steps"]; ms = max(tm["macro_steps"], 1)
    a = np.array(agents[int(10 / P.dt_micro):])
    s = sim.summary()
    return dict(pilots=npilots, individuals=s["herb"]["count"] + s["pred"]["count"],
                agents_mean=float(a.mean()), agents_p95=float(np.quantile(a, 0.95)), agents_max=int(a.max()),
                hot_mean=float(np.mean(hot)), regions=int(sim.W.nreg),
                py_micro_ms_per_tick=1e3 * tm["micro"] / st, py_macro_ms_per_macro_step=1e3 * tm["macro"] / ms,
                py_lod_ms_per_macro_step=1e3 * tm["lod"] / ms, py_flora_ms_per_macro_step=1e3 * tm["flora"] / ms,
                events={k: float(v) for k, v in sim.events.items()})


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--T", type=float, default=60.0)
    a = ap.parse_args()
    micro_ns, macro_ms, txt = kernel_costs()
    ks = sorted(micro_ns)
    rows = []
    for n in (1, 2, 4):
        r = run(n, a.T)
        # compiled projection: interpolate ns/agent at the p95 agent count
        ns = float(np.interp(r["agents_p95"], ks, [micro_ns[k] for k in ks]))
        r["c_micro_ms_per_tick_p95"] = r["agents_p95"] * ns * 1e-6
        r["c_micro_ms_per_60fps_frame_at_10hz"] = r["c_micro_ms_per_tick_p95"] * 10 / 60
        r["c_macro_ms_per_step"] = macro_ms
        r["c_macro_ms_per_60fps_frame_at_1hz"] = macro_ms / 60
        rows.append(r)
        print(json.dumps({k: (round(v, 3) if isinstance(v, float) else v) for k, v in r.items() if k != "events"}))
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, "cost.json"), "w") as fh:
        json.dump(dict(kernels=txt, rows=rows), fh, indent=1)
    print(txt)


if __name__ == "__main__":
    main()
