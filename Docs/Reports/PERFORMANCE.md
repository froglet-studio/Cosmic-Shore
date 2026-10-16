# Performance — where it stands (report for the core four)

**From:** the performance sessions (branch `perf/performance-optimization`, merged into `Ys-bleeding-edge`), 2026-10-15.
**Record:** `Docs/PERFORMANCE_OPTIMIZATION.md` (the plan, the lever table, the measured-dead list, the method).
**Resolve loop:** `/perf-hunt`. **Daily check:** `.github/workflows/perf-daily.yml`.

## 1. In one minute

- **The target** (Yash, 2026-10-14): 100,000 prisms at 60+ fps on a phone — at least twice today's performance.
- **The prisms are not the cost.** 103k instanced prisms render at 122 fps in the lab; the scene with the MOST prisms
  (S4 Scurry, 69k) is the fastest scenario. What costs the frame is what grows beside them: spindles and snow
  (~40k transparent renderers in a grown Lattice), fauna body-sync and AI continuations (S5 Wildlife Liberation,
  19.8 fps with only 13k prisms), a ~10 ms fixed floor, and a 15.9 ms collider-LOD sweep spike.
- **The first regression the system caught (2026-10-15):** the lava lamp fell from 100+ to 19-30 fps. An Amoebius A/B of two
  commits named three new per-frame costs (the Charge crystal arcs, the first-login guide's scene searches, the spotlight's
  text measuring); Yash's Profiler confirmed the arcs at 21 ms of a 36 ms frame. Fixed the same day and on `bleeding-edge`
  (#1098). The Unity after-number is still owed (T-20261015-7edd).
- **Every `Prism` draws through entities** (audit, record §1.2): the config is on, nothing opts out, the only gate is the
  device probe. What is NOT a prism and still has a GameObject renderer each is the spindles (~40k in a grown Lattice,
  SRP-batchable already) and the snow shards: levers L1 and L6, Garrett's ecology area.
- **No phone has ever been measured.** The device gate may drop every prism to the 12.9x slower legacy path on a
  phone without compute/Vulkan. That is the first thing the phone plan measures (§3.6 of the record).
- **Measuring is automatic, and there is no other editor UI.** In Play mode the editor captures by itself, once per
  scene, after the scene has run long enough (S2 8 min, Skim Race 90 s, S5 2 min, any other scene 90 s;
  `PerfAutoCapture`), and writes burst + diag + prof + renderers + cells + console + cameras as ONE JSON; after Play
  Unity asks "Send to Claude?" and Send pushes it; the daily sweep judges it against its baseline and files a team task when something got
  worse. Ask any Claude session "performance status" or "what should I test" for the rest.

## 1a. Waiting on Garrett (Yash, 2026-10-15: "tell this to Garrett, this has to [reach him] next time he works")

Four performance items need Garrett, because each sits in his code or is his design call. A Claude session shows them to
him on its first prompt that touches crystals, ecology, flora, spindles, projectiles, explosions, the Squirrel or
Regatta (`.claude/hooks/team_tasks_nudge.py`, "tasks waiting on a person").

1. **Pick the Charge light's sleep size** (T-20261015-a5a5). The crystal arcs cost 21 ms of a 36 ms lava-lamp frame in
   Yash's Profiler; they now sleep when a crystal is under 24 px across on screen. Fly up to a crystal, see where the
   light wakes, and set `sleepBelowScreenRadiusPixels` in the profile and its generator.
2. **Pool the AOE explosions** (T-20261015-fafa, lever L13). Every omni collect instantiates the Squirrel's ring
   explosion and destroys it after; it is the whole of the impactor's 5.6 KB/frame in the Amoebius Skim Race run.
3. **Decide the spindles** (T-20261015-641c, lever L1). Every prism is already on entities; the spindles are not prisms
   and are the last big per-object renderer cost (~40k in a grown Lattice, +5.3 ms measured). Entities, or one mesh
   per plant, keeping the wither fade.
4. **Switch on the daily performance check** (T-20261015-f106). GitHub runs the scheduled workflow only from the
   default branch; it is merged into `bleeding-edge` now, so it needs one manual run to confirm its artifacts.

## 2. How a number reaches this report

1. Someone plays with Code Optimization = Release; the editor captures by itself after the scene's time (labelled by
   the scene's scenario: S2, SkimRace, S5; the badge counts down).
2. The editor shares the file: after Play stops, Unity asks "Send to Claude?" (a new capture asks on its own) and Send pushes it to the person's `bugledger/<user>` branch.
3. `perf_hunt.py sweep` (daily, or any session) reads every branch, judges validity first (Debug, Deep Profile, a frame
   cap, Burst off, a second camera: not a number), compares with `Docs/perf/baselines.json`, and writes the block below.
4. A regression becomes a `Docs/TEAM_TASKS.md` task (area Performance); `/perf-hunt` fixes it with a before/after.

## 3. Open measurements (who can take them)

See the Performance tasks on `Docs/TEAM_TASKS.md`: the S2 Lattice capture, the Skim Race Release capture (the Burst
check the 10-08 fix still owes, and the `PoolRefill.Squirrel Prism` ~9 KB/frame lead), and the S5 capture (the L8
go/no-go). Yash has offered to take editor tests.

<!-- perf_hunt:begin (generated by Tools/Build/perf_hunt.py; do not edit) -->

_Last sweep: 2026-10-15T23:25:35Z (`python3 Tools/Build/perf_hunt.py sweep`). Baselines: `Docs/perf/baselines.json` (editor captures), `Docs/perf/amoebius_baselines.json` (Amoebius markers)._

**Static gates:** all OK

| Gate | Result |
|---|---|
| mathf params alloc | OK - mathf-params-alloc check: OK (2098 files) |
| burst MathF (swarm pose) | OK - burst pose gate: OK |
| burst MathF (substrate) | OK - burst substrate gate: OK |
| console logging | OK - console-logging check: 0 PROBLEM(S) (2074 files) |
| per-frame allocation scan | OK - 78 finding(s) (update 6, yield-new-in-loop 63, yield-null loops 9) - was 78 |

**Editor captures sent to Claude from the editor:** 3 on 539 branch(es)

| Scene / label | By | When | CPU busy ms | p95 ms | GC KB/f | Prisms | Verdict |
|---|---|---|---:|---:|---:|---:|---|
| MinigameSkimRace\|S_SkimRace_I2 | yskha | 2026-10-08 06:35:23 | 13.12 | 19.87 | 30.22 | 9568 | invalid: not compared - ERROR  0.32 ms/frame of Burst jobs ran as MANAGED code (ShellContactQueryJob 0.23 ms, PrismRender.TransformFlush 0.06 ms, LOD.Sweep 0.03 ms) - the Burst fix is not in effect; read the 'burst' section |
| MinigameSkimRace\|prisma | local | 2026-10-09 13:02:22 | 16.67 | 16.67 | 48.80 | 0 | invalid: not compared - ERROR  Burst is disabled - every job runs managed |
| Menu_Main\|S1test | local | 2026-10-14 17:11:54 | 16.67 | 16.67 | 73.90 | 0 | invalid: not compared - ERROR  Burst is disabled - every job runs managed; ERROR  the burst probe job ran MANAGED - the game's jobs are not Burst-compiled; see the burst section |

**Amoebius marker run (allocations are the gate; milliseconds are watched):**

- `SkimRace|I2|3p`: REGRESSION - VesselImpactor.AcceptImpactee allocates 3.831 -> 5.578 KB/frame (+46%); PoolRefill.FastGrowPrism allocates 0.526 -> 0.7 KB/frame (+33%); new allocating marker PoolRefill.PrismImplosion: 0.122 KB/frame; SkimRace.Driver.TrackMpc 0.5364 -> 0.7046 ms/frame (+31%) - .NET time, confirm in Unity
  - top game allocators (KB/frame): `PoolRefill.Squirrel Prism` 8.90, `VesselImpactor.AcceptImpactee` 5.58, `PoolActivate.Squirrel Prism` 0.72, `PoolRefill.FastGrowPrism` 0.70
  - engine shim, not the game (`Docs/perf/amoebius_known.json`): CrystalDepthPrisms.LateUpdate 3.00 KB/frame - MaterialPropertyBlock.SetVectorArray(Vector4[]) clones the array on every call in Amoebius
  - engine shim, not the game (`Docs/perf/amoebius_known.json`): CrystalChargeArcs.LateUpdate 0.05 KB/frame - Mesh.SetVertices(List)/SetUVs(List<Vector4>)/SetTriangles(List) copy their lists into fres

**Team tasks filed this sweep:** T-20261015-d7f8, T-20261015-c8e2, T-20261015-c7db

<!-- perf_hunt:end -->
