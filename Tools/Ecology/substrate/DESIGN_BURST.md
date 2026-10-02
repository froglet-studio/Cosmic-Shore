# Substrate → Unity: Burst `IJobParallelFor` and GPU compute designs

The Python substrate (`core.py`) is written in the shape the port needs: struct-of-arrays state, a fixed
tick, one neighbour hash per tick, a rotating 1/k re-steer slice, and a per-agent kernel with no
allocation and no cross-agent writes (`kernels_nb.fused_step` is that kernel, compiled by numba). This
file maps it onto Unity. Measured Python/numba costs are in `bench.json` and `Tools/Ecology/DISCOVERIES.md`
§ Substrate.

## 1. State (one `NativeArray` per field, capacity-sized, never resized in play)

| array | type | notes |
|---|---|---|
| `pos`, `vel`, `intentDir` | `float3` | `intentDir` is the coasting intent (written only by the 1/k slice) |
| `intentSpeed`, `hunger`, `fear`, `curious`, `aggr`, `attach`, `phase`, `qTarget`, `stock`, `grow`, `starveT`, `biteCool` | `float` | drives + bookkeeping, 4 B each |
| `wanderSeed` | `float3` | |
| `role`, `slot` | `ushort` | caste / ring slot / body slot, fixed at birth |
| `flags` | `byte` | alive, dying |
| `freedTick` | `int` | pool-slot reuse guard (an index never changes identity inside a tick) |

~100 B per agent → 100k agents = 10 MB. Species parameters are two `Regime` structs (solitary,
gregarious; 19 floats each) and a 13-float species block in a `NativeArray<SpeciesParams>` indexed by a
per-agent species byte, so ONE job set runs every species in a cell.

Fields: per cell, 5+ channels × `40³` `float` = 1.3 MB, plus 3-float gradients for the 4 steering channels.

## 2. The job chain (one tick; nothing on the main thread but `Schedule`)

| # | job | type | work | notes |
|---|---|---|---|---|
| 1 | `HashKeysJob` | `IJobParallelFor` | N | `cellKey = pack(floor((pos+R)/h))` |
| 2 | `SortByKeyJob` | `IJob` (radix) or `NativeParallelMultiHashMap` | N | counting sort over occupied keys; Python uses open addressing |
| 3 | `CellMomentsJob` | `IJobParallelFor` over cells | N | count, Σpos, Σvel, Σphase per occupied cell (8 floats) |
| 4 | `FieldDepositJob` | `IJobParallelFor` + per-thread grids, then reduce | N | trail / alarm / threat; or `Interlocked` on int-quantized grid |
| 5 | `FieldBlurJob` ×3 | `IJobParallelFor` over lines | 3 × G³ | separable `[a,1-2a,a]`, decay folded into the last pass; gradient pass every 2 ticks |
| 6 | `AgentStepJob` | `IJobParallelFor`, batch 64 | N (+ N/k heavy) | **= `kernels_nb.fused_step`**: drives → if `(i+tick)%k==0`: 27-cell moment read, quorum target, paint interest/danger over D dirs, soft-argmax → intent; integrate all |
| 7 | `EatQueryJob` | `IJobParallelFor` over the slice | N/k | nearest edible prism via `PrismSpatialIndex` (game) — already Burst |
| 8 | `WorldResolveJob` | `IJob` (single thread) | events only | conflicts (two eaters, one prism), consume/lay, births/deaths into free slots. Event lists are tiny; this is the only serial job |
| 9 | `RenderPackJob` | `IJobParallelFor` | N | pos + size + colour (+ intent tint) into a `GraphicsBuffer`-mapped `NativeArray` |

Dependencies: 1→2→3→6, 4→5→6 (fields read by 6 are LAST tick's, so 4–5 can run in parallel with 1–3),
6→7→8→9. Schedule from `Update`, `Complete()` in the next frame's `Update` (one frame of latency, invisible at
a 10 Hz sim tick interpolated at render rate), so the main thread only pays scheduling.

**Expected main-thread cost: ≈ 0.02–0.05 ms per tick** (9 `Schedule` calls + one `Complete` + one
`GraphicsBuffer.SetData`/`LockBufferForWrite` for the render buffer). Everything else is on worker threads.

## 3. Worker-thread budget (projected from the numba fused kernel)

numba compiles to LLVM like Burst; numba `prange` on this 4-core container ≈ Burst on 4 workers. Measured
(`bench.json`, fused backend, locust params = every term active):

| N | k | fused kernel (4 thr) | whole step incl. Python-side world |
|---|---|---|---|
| 10k | 8 | ~1.4 ms | ~6.8 ms |
| 100k | 8 | ~11 ms | ~31 ms |
| 100k | 1 | ~41 ms | ~81 ms |

The "whole step" column still runs world bookkeeping, deposits and field sampling in numpy; in a Burst port
those are jobs 4, 5, 7, 8 and cost what their kernels cost. A 10 Hz sim tick for **10k agents is ~1.5–3 ms of
worker time per tick (~0.3 ms per rendered frame at 60 fps)**; 100k is ~15–20 ms per tick, i.e. one worker
core's worth at 10 Hz — affordable on PC, too much for mobile, which should cap a cell around 20–30k.

## 4. Rendering: no GameObjects

One `Graphics.RenderMeshInstanced` / `RenderMeshIndirect` per species mesh, reading a `GraphicsBuffer`
written once per sim tick by job 9. The vertex shader interpolates `prevPos→pos` by `(t - tickStart)/tickDt`
(the reason pool slots must never change identity inside a tick — §1 `freedTick`), and draws
`size × grow` so grow-in and wither-out are GPU-side (continuity of existence for free). Colour = lerp of
the two regimes' colours by `phase`, tinted by `intent` (the telegraph). Same shape as the swarm round-7
port on `cece/swarm-fauna-game` (Docs/SWARM_FAUNA.md §14).

Colliders: **zero per agent.** Contacts (bite, pilot ram, eat) are distance tests inside jobs 6–8. A
species that needs to be SHOT runs its agents through `PrismSpatialIndex` as health prisms only when
assembled into a body (the body's slots are the prisms), which is the existing worm/whale budget.

## 5. GPU compute alternative

Jobs 1–6 and 9 map 1:1 onto compute kernels (`BitonicSort` or a counting sort with `InterlockedAdd` for the
hash; the agent kernel is `[numthreads(64,1,1)]`). Then the render buffer never leaves the GPU and the
main-thread cost is one `Dispatch` chain. The catch is the world: eating and the pilot contact need
CPU-side truth (conserved mass, the scoring RPCs), so jobs 7–8 would need an `AsyncGPUReadback` of an
event list one frame late. Recommendation: **Burst first** (it keeps conserved mass and gameplay
authority on the CPU, where `PrismSpatialIndex` already lives); move only the agent kernel + render pack
to compute if a profile shows the worker budget is the bottleneck.

## 6. Networking

Fauna are per-peer today (`CellNetworkSync`). The substrate is deterministic given (seed, tick, inputs),
but floating-point determinism across machines is not guaranteed by Burst unless
`FloatMode.Deterministic`; the cheaper path is the existing one — the server owns the world events
(eat/die/bite), peers run the swarm visually and reconcile phase/body state from a tiny replicated
summary (per species: count, mean phase, body centre/heading, assembling flag).
