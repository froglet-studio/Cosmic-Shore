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
  the editor sends it to Claude by itself (no question); the daily sweep judges it against its baseline and files a team task when something got
  worse. Ask any Claude session "performance status" or "what should I test" for the rest.

## 1a. Waiting on Garrett (Yash, 2026-10-15: "tell this to Garrett, this has to [reach him] next time he works")

Four performance items were waiting on Garrett. On 2026-10-17 Yash took the first three over ("Garrett is busy, we do
them ourselves"); what was done is below, so Garrett only needs to look, not build. A Claude session still shows him the
open ones on its first prompt that touches crystals, ecology, flora, spindles, projectiles, explosions, the Squirrel or
Regatta (`.claude/hooks/team_tasks_nudge.py`, "tasks waiting on a person").

1. **Charge light sleep size: done** (T-20261015-a5a5 closed). Kept at 24 px radius; between 24 and 64 px the arcs now
   update every other frame (`halfRateBelowScreenRadiusPixels`). Garrett may still retune either number in the profile.
2. **AOE explosions: pooled, and the real allocator fixed** (T-20261015-fafa closed). The Squirrel's ring spawner is
   reused instead of instantiated. Measuring it showed the cost was the crystal-to-ring morph rebuilding the whole
   crystal mesh each collect; that is cached now. Skim Race impactor allocation 5.53 -> 3.41 KB/frame in Amoebius, back
   under its baseline. The damage blasts are not pooled (their hit ledgers assume a fresh instance).
3. **Spindles: nothing to build yet** (T-20261015-641c closed). A grown Lattice holds ~2.9k spindles today, not ~40k,
   and none are under a pixel, so a size LOD hides nothing. A Unity S2 capture (T-20261015-56dd) shows whether a
   bigger change is still worth it. Snow (lever L6) is drawn instanced now, ~4.2k renderers fewer, same density.
4. **Switch on the daily performance check** (T-20261015-f106). GitHub runs the scheduled workflow only from the
   default branch; it is merged into `bleeding-edge` now, so it needs one manual run to confirm its artifacts.

## 2. How a number reaches this report

1. Someone plays with Code Optimization = Release; the editor captures by itself after the scene's time (labelled by
   the scene's scenario: S2, SkimRace, S5; the badge counts down).
2. The editor shares the file: after Play stops, the editor sends it by itself, with no question, to the person's `bugledger/<user>` branch.
3. `perf_hunt.py sweep` (daily, or any session) reads every branch, judges validity first (Debug, Deep Profile, a frame
   cap, Burst off, a second camera: not a number), compares with `Docs/perf/baselines.json`, and writes the block below.
4. A regression becomes a `Docs/TEAM_TASKS.md` task (area Performance); `/perf-hunt` fixes it with a before/after.

## 3. Open measurements (who can take them)

See the Performance tasks on `Docs/TEAM_TASKS.md`: the S2 Lattice capture, the Skim Race Release capture (the Burst
check the 10-08 fix still owes, and the `PoolRefill.Squirrel Prism` ~9 KB/frame lead), and the S5 capture (the L8
go/no-go). Yash has offered to take editor tests.

<!-- perf_hunt:begin (generated by Tools/Build/perf_hunt.py; do not edit) -->

_Last sweep: 2026-10-17T08:48:58Z (`python3 Tools/Build/perf_hunt.py sweep`). Baselines: `Docs/perf/baselines.json` (editor captures), `Docs/perf/amoebius_baselines.json` (Amoebius markers)._

**Static gates:** all OK

| Gate | Result |
|---|---|
| mathf params alloc | OK - mathf-params-alloc check: OK (2104 files) |
| burst MathF (swarm pose) | OK - burst pose gate: OK |
| burst MathF (substrate) | OK - burst substrate gate: OK |
| console logging | OK - console-logging check: 0 PROBLEM(S) (2080 files) |
| per-frame allocation scan | OK - 45 finding(s) (update 0, yield-new-in-loop 36, yield-null loops 9) - was 78 |

**Editor captures sent to Claude from the editor:** 9 on 545 branch(es)

| Scene / label | By | When | CPU busy ms | p95 ms | GC KB/f | Prisms | Verdict |
|---|---|---|---:|---:|---:|---:|---|
| MinigameSkimRace\|S_SkimRace_I2 | yskha | 2026-10-08 06:35:23 | 13.12 | 19.87 | 30.22 | 9568 | invalid: not compared - ERROR  0.32 ms/frame of Burst jobs ran as MANAGED code (ShellContactQueryJob 0.23 ms, PrismRender.TransformFlush 0.06 ms, LOD.Sweep 0.03 ms) - the Burst fix is not in effect; read the 'burst' section |
| MinigameSkimRace\|prisma | local | 2026-10-09 13:02:22 | 16.67 | 16.67 | 48.80 | 0 | invalid: not compared - ERROR  Burst is disabled - every job runs managed |
| Menu_Main\|S1test | local | 2026-10-14 17:11:54 | 16.67 | 16.67 | 73.90 | 0 | invalid: not compared - ERROR  Burst is disabled - every job runs managed; ERROR  the burst probe job ran MANAGED - the game's jobs are not Burst-compiled; see the burst section |
| MinigameRegatta\|MinigameRegatta | belac | 2026-10-16 11:07:58 | 8.69 | 21.26 | 47.02 | 26945 | within baseline |
| MinigameRegatta\|MinigameRegatta | belac | 2026-10-16 13:10:56 | 7.38 | 14.56 | 32.78 | 21841 | REGRESSION: marker Inl_UniversalRenderTotal 0.9318 -> 1.3824 ms/frame (+48%); marker UniTaskLoopRunnerPreLateUpdate 0.0007 -> 0.3666 ms/frame (+52271%) |
| MinigameGrizzlyCharge\|MinigameGrizzlyCharge | mrglo | 2026-10-16 14:51:10 | 6.48 | 19.19 | 84.01 | 10830 | within baseline |
| MinigameRegatta\|MinigameRegatta | belac | 2026-10-16 15:13:50 | 2.15 | 9.13 | 89.49 | 2480 | REGRESSION: gcKB 47.01902389526367 -> 89.48501586914062 (+90%) |
| MinigameGrizzlyCharge\|MinigameGrizzlyCharge | mrglo | 2026-10-16 15:42:28 | 6.01 | 18.97 | 96.07 | 12793 | REGRESSION: marker LateBehaviourUpdate 0.3973 -> 0.6018 ms/frame (+51%) |
| Menu_Main\|Garland | mrglo | 2026-10-16 16:35:36 | 3.85 | 18.77 | 117.82 | 16882 | within baseline |

**Amoebius marker run (allocations are the gate; milliseconds are watched):**

- `SkimRace|I2|3p`: within baseline
  - top game allocators (KB/frame): `PoolRefill.Squirrel Prism` 8.91, `VesselImpactor.AcceptImpactee` 3.41, `PoolActivate.Squirrel Prism` 0.73, `PoolRefill.FastGrowPrism` 0.69
  - engine shim, not the game (`Docs/perf/amoebius_known.json`): CrystalDepthPrisms.LateUpdate 3.00 KB/frame - MaterialPropertyBlock.SetVectorArray(Vector4[]) clones the array on every call in Amoebius
  - engine shim, not the game (`Docs/perf/amoebius_known.json`): CrystalChargeArcs.LateUpdate 0.05 KB/frame - Mesh.SetVertices(List)/SetUVs(List<Vector4>)/SetTriangles(List) copy their lists into fres

**Team tasks filed this sweep:** T-20261017-1809, T-20261017-2129, T-20261017-19f0

<!-- perf_hunt:end -->
