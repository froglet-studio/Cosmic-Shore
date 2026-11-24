# Switchback — static prism budget (2026-11-02)

Goal: more room for **static prism obstacles around the path** (grids, mazes, math shapes), with **no fauna near the path**.
All per-prism numbers below are **ESTIMATES** extrapolated from one capture (2026-11-01 17:39 IST, editor, frame cap @75 on so
frame time is *not* a valid measurement): `prismPath ON · ents=4558 meshes=7 mats=14`, CPU 9.7 ms, GPU 2.86 ms, 460 draws,
154 SetPass. No before/after soak was run (the headless Unity project was busy with another soak) — **untested**.

## What a static prism costs today

| Area | Today | Est. cost / +1,000 static prisms |
|---|---|---|
| Rendering | Entities Graphics companion entity, instanced (7 meshes / 14 mats), shadows OFF (`ShadowCastingMode.Off`, no receive) | CPU ~0.03 ms (`UpdateOldEntitiesGraphicsChunksJob` 0.15 ms @ 4,558). Draw calls ~flat (batched). GPU: vertex + overdraw, no distance culling — depends on size/screen cover, unmeasured |
| Colliders / physics | `BoxCollider` per prism, but `PrismColliderLodManager` keeps only those within 200 u of a pilot live (Burst sweep 0.25 s, ~0.0075 ms) | Sweep ~+0.002 ms; physics only scales with prisms **within 200 u** (`ColliderCap` 300–400) |
| Lifecycle | No per-prism Update; shared Driver drains a budgeted create queue | Load-time only (~spread over frames); per-prism GameObject memory (~1–2 KB + entity) |
| Events | None per frame for static prisms | ~0 |
| Spatial index | Burst `PrismSpatialIndex` | AI prism-sense / fauna queries grow with **local density** near pilots, not total count |
| Shadows / LOD | Shadows already off; no LOD | — |

**Blockers (in order):** (1) collider count inside 200 u of pilots — dense obstacles right on the path hit `ColliderCap`;
(2) load-time creation + per-prism GameObject memory (mobile); (3) AI/spatial query density near the path;
(4) GPU fill with no distance culling (big shapes). Fauna near the path (`Fauna.BodySync` 0.42–0.49 ms, animators
0.44 ms, 56 fauna / 231 skinned renderers) is removed by the corridor below.

## What changed

- **Fauna path corridor** (`FaunaCorridor`, `Cell.SetFaunaPathCorridor`): a capsule-chain round the gate path. Fauna are not
  born in it, their goals are pushed out of it (via `Cell.ClampToFaunaContainment`, so every spawner/steerer honours it),
  herbivores don't eat prisms in it, LightFauna schools spawn outside it. Diet + steering, never a wall. Fauna away
  from the path are unchanged. Radius per intensity: `SwitchbackController.faunaCorridorRadiusByIntensity`
  (default 220 u for I1–I4; 0 disables). Cleared on despawn.
- ProfilerMarkers: `Fauna.Corridor.PushOut`, `Switchback.Features.Spawn`.

## Measured (2026-11-02, headless soak, CPU only)

Unity 6000.3.17f1 editor, `-batchmode -nographics`, `SwitchbackSoakLauncher -sbSoak 4:4:1` (I4, fixed seed, 3 AI),
`-sbSoakFps 1000`, race-end quit; frame stats after an 8 s warm-up. **No GPU** — rendering/fill cost is not measured here;
a GPU capture on a real device/PC is still needed. Stress prisms: 6 u Plain prisms placed by an out-of-repo probe through
`featuresPrefab` (walls/lattices/mazes), none within 120 u of the path, ≤ 80 within 200 u.

| Run (ms) | p50 | p95 | p99 | avg | max | GC KB/frame | fauna near path |
|---|---|---|---|---|---|---|---|
| Baseline (today's perf fixes + corridor reverted) | 3.16 | 6.74 | 16.46 | 5.57 | 2,172 | 207 | 50.7 |
| Latest (9a130d9f7) | 2.16 | 3.24 | 10.19 | 5.62 | 9,837 | 349 | 10.5 |
| Latest + 1,500 static prisms (80 within 200 u) | 2.11 | 2.96 | 7.36 | 5.42 | 2,559 | 361 | 10.1 |
| Latest + 3,000 static prisms (80 within 200 u) | 2.58 | 4.26 | 14.26 | 5.50 | 10,946 | 260 | 14.2 |

- **+1,500 at I4 is within noise** of latest on CPU. **+3,000** costs ~+0.4 ms p50 / +1 ms p95; part of that is more
  fauna (19 vs 12 total; `Fauna.Light.Update` 0.02 → 0.07 ms, `Animators` 0.13 → 0.25 ms), so it is not pure prism cost.
- avg is dominated by a few multi-second hitch frames (max column) inside `BehaviourUpdate` that no named marker owns —
  open item, not prism-related (present without stress).
- GC/frame includes the probe's own allocations; compare runs, not absolutes.

## Budget per intensity (I4 row measured on CPU; I1–I3 still estimates)

Headroom = extra static prisms on top of today's `TotalCap`, keeping colliders near the path within `ColliderCap`.

| Intensity | TotalCap today | ColliderCap (200 u) | Moving | Suggested extra static obstacles | Of which within 200 u of path |
|---|---|---|---|---|---|
| I1 | 1800 | 300 | 0 | +3,000 | ≤ 150 |
| I2 | 2400 | 340 | 40 | +2,500 | ≤ 120 |
| I3 | 3000 | 370 | 100 | +2,000 | ≤ 100 |
| I4 | 3400 | 400 | 140 | +1,500 | ≤ 80 |

Rationale: +1,000 static prisms ≈ +0.03 ms CPU (est.); the binding limit is colliders/AI density near the path and
mobile memory/load time, not per-frame render cost. Keep obstacles ≥ the corridor's *visual* edge so pilots see them
but most of their prisms sit beyond 200 u of the racing line; the `LayCapacity` 4096 budget is per-trail and separate.

## The I4 gauntlet (2026-11-03): the I4 row spent, on the path

Yash asked for 1.5x the I4 objects (`SWITCHBACK.md` "The I4 gauntlet"). `TotalCap(4)` 3400 -> 5100 (+1,700, a little
over the +1,500 measured above), and unlike the probe's 80-within-200-u stress prisms these sit ON the path: tunnels,
plugs, seals and a denser field, so `ColliderCap(4)` rose 400 -> 600 (Breakwater I1's shipped worst is 568) and the
fixed seed 45_504 reaches it. Movers 28 / 140 -> 42 / 210. Measured headless only (layout counts; course average 4,933
prisms against 3,231). **To verify:** the I4 soak above (3 AI, fixed seed) with the gauntlet - p50 / p95 / p99, live
colliders, `Switchback.BlastRun.TickMovers`, load time - against the "Latest" row. If 600 near the path costs too much,
lower `ColliderCap(4)` first: the layout then thins the field evenly (hashed cell order), and a gate's tunnels are laid before its field.

## Skipped
- Distance culling / LOD for prism entities — changes visuals.
- GameObject-less static prism path (entity-only, no collider GO) — biggest memory win but large and risky; next step if
  memory/load becomes the limit.

## To verify
Headless soak before/after with +N static prisms per intensity: `Fauna.BodySync`, `UpdateOldEntitiesGraphicsChunksJob`,
`LOD.Sweep/Drain`, live collider count, load time, memory.
