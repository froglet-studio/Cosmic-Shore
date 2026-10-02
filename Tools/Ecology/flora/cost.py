"""Cost model for the five threat flora: measured numpy timings (scaling evidence, NOT game cost) plus explicit
GPU-compute and Burst estimates from operation and byte counts. Every assumption is a named constant.

    python cost.py            -> results/cost.json

The GPU estimate is bandwidth-bound arithmetic (these kernels are memory-bound: a few flops per byte):
    t = bytes_moved / BW + launches * LAUNCH
The Burst estimate is per-element ns from what Burst sustains on this kind of SoA loop, divided over the job
workers. Both are ESTIMATES; the measured column is real but measures numpy, which is 20-100x slower than Burst.
"""
import json, os, sys, time
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

BW = {"desktop (RTX 3060, 360 GB/s)": 360e9, "laptop iGPU (Iris Xe, ~60 GB/s)": 60e9, "mobile (Adreno 650, ~44 GB/s)": 44e9}
EFF = 0.35          # achieved fraction of peak bandwidth for 3D stencils / scattered texture reads (conservative)
LAUNCH = 8e-6       # per compute dispatch, s
BURST_WORKERS = 4
NS = dict(agent_step=45.0, voxel_stencil=1.6, voxel_simple=0.5, prism_test=6.0)   # Burst ns per element


def physarum_gpu(n_agents, G, field_hz=60.0):
    vox = G ** 3
    b_agents = n_agents * (5 * 32 + 24 + 24 + 8)                 # 5 sensor fetches (~32 B each, cache-amortised), pos/dir rw, atomic deposit
    b_field = vox * 4 * (3 * 2 + 3 + 2)                          # separable blur 3 passes (r+w), evaporate+EMA fused (r T,S w S), threshold r
    b_waves = vox * 1 * 8 * (12.0 / field_hz)                    # GH on int8 at ~12 Hz (wave tick), 6-neighbour reads
    launches = 6
    return {k: round(((b_agents + b_field + b_waves) / (bw * EFF) + launches * LAUNCH) * 1e3, 3) for k, bw in BW.items()}


def coral_gpu(G, substeps=2):
    vox = G ** 3
    b = substeps * vox * 4 * (2 * 7 + 2 * 2 + 4)                 # u,v 7-point Laplacians + reaction update
    return {k: round((b / (bw * EFF) + 3 * substeps * LAUNCH) * 1e3, 3) for k, bw in BW.items()}


def burst_ms(n_agents=0, G=0, prisms=0, substeps=1, rate_hz=60.0):
    ns = n_agents * NS["agent_step"] + (G ** 3) * NS["voxel_stencil"] * 3 * substeps + prisms * NS["prism_test"]
    return round(ns * 1e-6 / BURST_WORKERS * rate_hz / 60.0, 3)


def measure(spec, params, steps=60):
    from harness import FloraArena, resolve
    from common.arena import Pilot
    ar = FloraArena(seed=7); ar.grove_mass(1600, clumps=20)
    sp = resolve(spec)(ar, params); ar.species = sp
    p = ar.add_pilot(Pilot.wanderer()); p.prev = p.pos.copy()
    t = time.perf_counter()
    for _ in range(steps):
        sp.step(ar, 0.1); ar.step(0.1)
    return round((time.perf_counter() - t) / steps * 1e3, 2)


if __name__ == "__main__":
    out = dict(assumptions=dict(BW=list(BW), EFF=EFF, LAUNCH_us=LAUNCH * 1e6, BURST_WORKERS=BURST_WORKERS, NS=NS))
    out["physarum_gpu_ms"] = {f"{n//1000}k agents, {G}^3": physarum_gpu(n, G) for n, G in
                              ((16000, 56), (32000, 64), (100000, 128), (100000, 96))}
    out["physarum_burst_ms_per_frame"] = {f"{n//1000}k agents, {G}^3 @{hz}Hz": burst_ms(n, G, rate_hz=hz) for n, G, hz in
                                          ((16000, 56, 60), (32000, 64, 20), (100000, 128, 10), (16000, 48, 10))}
    out["coral_gpu_ms"] = {f"{G}^3": coral_gpu(G) for G in (48, 64, 96, 128)}
    out["coral_burst_ms_per_frame"] = {f"{G}^3 @{hz}Hz": burst_ms(G=G, substeps=2, rate_hz=hz) for G, hz in ((48, 60), (48, 10), (64, 10))}
    out["agent_species_burst_ms"] = {"snaptrap 60 traps x 27 prisms": burst_ms(prisms=60 * 27),
                                     "spores 140 pods + 2000 spores": burst_ms(n_agents=2000, prisms=140 * 8),
                                     "walker 24 x 34 prisms": burst_ms(prisms=24 * 34)}
    out["numpy_ms_per_step_measured"] = {
        "snaptrap": measure("snaptrap:SnapTrap", {}), "spores": measure("spores:SporeBurster", {}),
        "walker": measure("walker:Walker", {}),
        "coral 48^3": measure("coral:Coral", {"Dv": 0.02, "Du": 0.3, "rate": 40, "k": 0.01}),
        "physarum 16k/56^3": measure("physarum:Physarum", {"warmup": 0}),
    }
    p = os.path.join(os.path.dirname(os.path.abspath(__file__)), "results", "cost.json")
    json.dump(out, open(p, "w"), indent=1); print(json.dumps(out, indent=1))
