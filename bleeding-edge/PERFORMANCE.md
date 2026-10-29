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
them ourselves"); what was done is below, so Garrett only needs to look, not build. `team_tasks.py list --area Performance`
lists the open ones (tickets name nobody since 2026-10-23; the `team_tasks_nudge.py` hook that showed them on a first
matching prompt was switched off that day and deleted on 2026-10-25).

1. **Charge light sleep size: done** (T-20261015-a5a5 closed). Kept at 24 px radius; between 24 and 64 px the arcs now
   update every other frame (`halfRateBelowScreenRadiusPixels`). Garrett may still retune either number in the profile.
2. **AOE explosions: pooled, and the real allocator fixed** (T-20261015-fafa closed). The Squirrel's ring spawner is
   reused instead of instantiated. Measuring it showed the cost was the crystal-to-ring morph rebuilding the whole
   crystal mesh each collect; that is cached now. Skim Race impactor allocation 5.53 -> 3.41 KB/frame in Amoebius, back
   under its baseline. The damage blasts are not pooled (their hit ledgers assume a fresh instance).
3. **Spindles: an LOD is built, waiting on Unity** (2026-10-20, Garrett's ask, `cece/gallant-keller-6mr1u4`,
   `Docs/SPINDLE_LOD.md`). The 10-17 size LOD hid nothing because it measured a limb's bounding sphere; a spindle is
   a wire and goes sub-pixel by THICKNESS. Three tiers (the untouched braid; the braid converging into one thread;
   the thread alone, drawn instanced while still). Needs a first look in Unity and an S2 A/B with the profile's
   `lodEnabled` on and off. Snow (lever L6) is drawn instanced now, ~4.2k renderers fewer, same density.
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

_Last sweep: 2026-10-29T11:46:28Z (`python3 Tools/Build/perf_hunt.py sweep`). Baselines: `Docs/perf/baselines.json` (editor captures), `Docs/perf/amoebius_baselines.json` (Amoebius markers)._

**Static gates:** 1 FAILING

| Gate | Result |
|---|---|
| mathf params alloc | FAIL - mathf-params-alloc check: 1 FAILURE(S) |
| burst MathF (swarm pose) | OK - burst pose gate: OK |
| burst MathF (substrate) | OK - burst substrate gate: OK |
| console logging | OK - console-logging check: 0 PROBLEM(S) (2439 files) |
| per-frame allocation scan | OK - 41 finding(s) (update 1, yield-new-in-loop 32, yield-null loops 8) - was 78 |

**Editor captures sent to Claude from the editor:** 141 on 572 branch(es)

| Scene / label | By | When | CPU busy ms | p95 ms | GC KB/f | Prisms | Verdict |
|---|---|---|---:|---:|---:|---:|---|
| MinigameSkimRace\|S_SkimRace_I2 | yskha | 2026-10-08 06:35:23 | 13.12 | 19.87 | 30.22 | 9568 | invalid: not compared - ERROR  0.32 ms/frame of Burst jobs ran as MANAGED code (ShellContactQueryJob 0.23 ms, PrismRender.TransformFlush 0.06 ms, LOD.Sweep 0.03 ms) - the Burst fix is not in effect; read the 'burst' section |
| MinigameRegatta\|MinigameRegatta | belac | 2026-10-16 11:07:58 | 8.69 | 21.26 | 47.02 | 26945 | within baseline |
| MinigameRegatta\|MinigameRegatta | belac | 2026-10-16 13:10:56 | 7.38 | 14.56 | 32.78 | 21841 | REGRESSION: marker Inl_UniversalRenderTotal 0.9318 -> 1.3824 ms/frame (+48%); marker UniTaskLoopRunnerPreLateUpdate 0.0007 -> 0.3666 ms/frame (+52271%) |
| MinigameGrizzlyCharge\|MinigameGrizzlyCharge | mrglo | 2026-10-16 14:51:10 | 6.48 | 19.19 | 84.01 | 10830 | within baseline |
| MinigameRegatta\|MinigameRegatta | belac | 2026-10-16 15:13:50 | 2.15 | 9.13 | 89.49 | 2480 | REGRESSION: gcKB 47.01902389526367 -> 89.48501586914062 (+90%) |
| MinigameGrizzlyCharge\|MinigameGrizzlyCharge | mrglo | 2026-10-16 15:42:28 | 6.01 | 18.97 | 96.07 | 12793 | REGRESSION: marker LateBehaviourUpdate 0.3973 -> 0.6018 ms/frame (+51%) |
| Menu_Main\|Garland | mrglo | 2026-10-16 16:35:36 | 3.85 | 18.77 | 117.82 | 16882 | within baseline |
| MinigameGrizzlyCharge\|MinigameGrizzlyCharge | mrglo | 2026-10-17 06:15:18 | 7.38 | 19.17 | 92.31 | 11442 | within baseline |
| MinigameGrizzlyCharge\|MinigameGrizzlyCharge | mrglo | 2026-10-17 06:25:43 | 5.92 | 18.69 | 107.79 | 11966 | REGRESSION: gcKB 84.00566864013672 -> 107.78675079345703 (+28%) |
| MinigameGrizzlyCharge\|MinigameGrizzlyCharge | mrglo | 2026-10-17 07:09:38 | 6.05 | 19.34 | 93.89 | 12409 | within baseline |
| Menu_Main\|Garland | yskha | 2026-10-18 00:41:07 | 13.77 | 67.50 | 31.37 | 15555 | within baseline |
| Menu_Main\|Garland | yskha | 2026-10-18 03:19:01 | 6.39 | 11.46 | 23.30 | 12628 | improved |
| Menu_Main\|Garland | yskha | 2026-10-18 09:03:27 | 7.54 | 10.88 | 18.33 | 15326 | improved |
| Menu_Main\|Garland | belac | 2026-10-19 10:20:15 | 9.98 | 14.78 | 16.04 | 10920 | within baseline |
| MinigameSwitchback\|MinigameSwitchback | belac | 2026-10-19 10:25:46 | 4.48 | 11.04 | 55.60 | 7814 | within baseline |
| Menu_Main\|Garland | belac | 2026-10-19 10:59:37 | 9.01 | 11.45 | 15.97 | 9013 | improved |
| Menu_Main\|Garland | belac | 2026-10-19 14:21:51 | 9.21 | 12.39 | 18.53 | 21272 | improved |
| Menu_Main\|Garland | belac | 2026-10-20 13:42:29 | 8.69 | 11.39 | 16.52 | 16696 | improved |
| Menu_Main\|Garland | owner | 2026-10-20 15:40:21 | 10.34 | 22.80 | 46.97 | 14022 | within baseline |
| MinigameBloomrush\|MinigameBloomrush | owner | 2026-10-20 15:47:52 | 130.14 | 201.88 | 123.44 | 7460 | within baseline |
| MinigameBloomrush\|MinigameBloomrush | owner | 2026-10-20 16:02:48 | 220.80 | 304.67 | 190.07 | 3739 | REGRESSION: cpuBusyMs 130.13706970214844 -> 220.8030242919922 (+70%); p95Ms 201.87890625 -> 304.6697998046875 (+51%); gcKB 123.43673706054688 -> 190.07241821289062 (+54%); marker PlayerLoop 126.5328 -> 211.6232 ms/frame (+67%); marker CoroutinesDelayedCalls 84.1366 -> 170.3202 ms/frame (+102%); marker BehaviourUpdate 5.541 -> 7.8674 ms/frame (+42%); marker Physics.Simulate 1.4195 -> 2.5316 ms/frame (+78%); marker UGUI.Rendering.UpdateBatches 1.0869 -> 1.6051 ms/frame (+48%); marker LightFauna.Hunt 0.016 -> 0.858 ms/frame (+5262%) |
| MinigameBreakwater\|MinigameBreakwater | owner | 2026-10-20 16:21:58 | 7.45 | 23.01 | 48.00 | 6640 | within baseline |
| MinigameWaystation\|MinigameWaystation | belac | 2026-10-20 16:43:41 | 4.04 | 11.30 | 47.96 | 1060 | within baseline |
| Menu_Main\|Garland | owner | 2026-10-20 16:51:27 | 22.63 | 31.11 | 25.73 | 29438 | REGRESSION: cpuBusyMs 10.338772773742676 -> 22.62531852722168 (+119%); p95Ms 22.80489730834961 -> 31.10999870300293 (+36%); marker PlayerLoop 12.32 -> 22.5357 ms/frame (+83%); marker UGUI.Rendering.UpdateBatches 3.5317 -> 6.3619 ms/frame (+80%); marker BehaviourUpdate 1.7693 -> 3.9895 ms/frame (+125%); marker Inl_UniversalRenderTotal 2.0078 -> 3.3711 ms/frame (+68%); marker Animators.Update 0.3682 -> 0.6994 ms/frame (+90%); marker CoroutinesDelayedCalls 0.1594 -> 0.6503 ms/frame (+308%); marker LightFauna.Tick.PrismScan 0.0977 -> 0.3934 ms/frame (+303%); marker Physics.Simulate 0.0989 -> 0.3538 ms/frame (+258%) |
| MinigameRegatta\|MinigameRegatta | owner | 2026-10-20 17:15:02 | 8.44 | 28.95 | 38.87 | 3535 | within baseline |
| MinigameRegatta\|MinigameRegatta | owner | 2026-10-20 17:33:05 | 11.90 | 30.06 | 23.24 | 2474 | REGRESSION: cpuBusyMs 8.438409805297852 -> 11.8966703414917 (+41%); marker Inl_UniversalRenderTotal 1.3349 -> 2.2512 ms/frame (+69%); marker BehaviourUpdate 1.322 -> 2.0793 ms/frame (+57%); marker UGUI.Rendering.UpdateBatches 0.4972 -> 0.7372 ms/frame (+48%) |
| MinigameRegatta\|MinigameRegatta | owner | 2026-10-20 17:38:22 | 26.55 | 54.18 | 136.38 | 2563 | REGRESSION: cpuBusyMs 8.438409805297852 -> 26.546234130859375 (+215%); p95Ms 28.951602935791016 -> 54.175899505615234 (+87%); gcKB 38.86630630493164 -> 136.3773956298828 (+251%); marker PlayerLoop 13.8741 -> 30.6687 ms/frame (+121%); marker Inl_UniversalRenderTotal 1.3349 -> 2.5698 ms/frame (+93%); marker CoroutinesDelayedCalls 0.1523 -> 0.955 ms/frame (+527%); marker LateBehaviourUpdate 0.4564 -> 0.8877 ms/frame (+95%) |
| MinigameWaystation\|MinigameWaystation | belac | 2026-10-21 15:31:05 | 3.92 | 10.76 | 50.72 | 1159 | within baseline |
| Menu_Main\|Garland | belac | 2026-10-22 15:46:15 | 9.91 | 13.18 | 16.96 | 17299 | REGRESSION: marker BehaviourUpdate 1.5177 -> 2.0018 ms/frame (+32%) |
| MinigameWrasseRun\|MinigameWrasseRun | owner | 2026-10-22 23:06:22 | 18.92 | 22.65 | 21.31 | 2151 | within baseline |
| MinigameThresherTime\|MinigameThresherTime | owner | 2026-10-22 23:28:43 | 20.48 | 39.18 | 27.32 | 7005 | within baseline |
| Menu_Main\|Garland | owner | 2026-10-22 23:39:13 | 30.48 | 47.67 | 32.74 | 13026 | REGRESSION: cpuBusyMs 10.338772773742676 -> 30.480070114135742 (+195%); p95Ms 22.80489730834961 -> 47.67159652709961 (+109%); marker PlayerLoop 12.32 -> 29.2043 ms/frame (+137%); marker Inl_UniversalRenderTotal 2.0078 -> 9.5487 ms/frame (+376%); marker UGUI.Rendering.UpdateBatches 3.5317 -> 6.5698 ms/frame (+86%); marker BehaviourUpdate 1.7693 -> 4.7321 ms/frame (+167%); marker LateBehaviourUpdate 0.8621 -> 1.6474 ms/frame (+91%) |
| MinigameWreckingBall\|MinigameWreckingBall | owner | 2026-10-22 23:43:30 | 31.62 | 67.52 | 30.91 | 11350 | within baseline |
| Menu_Main\|MinigameRegatta | owner | 2026-10-22 23:56:25 | 38.89 | 66.08 | 409.84 | 4948 | within baseline |
| MinigameRegatta\|MinigameRegatta | owner | 2026-10-23 00:24:45 | 3.02 | 23.03 | 61.11 | 3042 | REGRESSION: gcKB 38.86630630493164 -> 61.10776138305664 (+57%) |
| MinigameHijack\|MinigameHijack | owner | 2026-10-23 13:50:32 | 16.24 | 29.73 | 18.93 | 4791 | within baseline |
| MinigameDustup\|MinigameDustup | owner | 2026-10-23 13:56:52 | 16.52 | 26.85 | 22.06 | 10895 | within baseline |
| MinigameTapestry\|MinigameTapestry | owner | 2026-10-23 14:02:10 | 10.80 | 30.73 | 23.88 | 1095 | within baseline |
| MinigameBroadside\|MinigameBroadside | owner | 2026-10-23 14:13:05 | 22.81 | 42.91 | 31.70 | 14402 | within baseline |
| Menu_Main\|Garland | owner | 2026-10-23 19:42:57 | 26.56 | 42.69 | 24.08 | 15703 | REGRESSION: cpuBusyMs 10.338772773742676 -> 26.555517196655273 (+157%); p95Ms 22.80489730834961 -> 42.69499588012695 (+87%); marker PlayerLoop 12.32 -> 26.4649 ms/frame (+115%); marker UGUI.Rendering.UpdateBatches 3.5317 -> 8.6272 ms/frame (+144%); marker BehaviourUpdate 1.7693 -> 4.157 ms/frame (+135%); marker Inl_UniversalRenderTotal 2.0078 -> 3.5404 ms/frame (+76%); marker LateBehaviourUpdate 0.8621 -> 2.0219 ms/frame (+135%); marker Animators.Update 0.3682 -> 0.6937 ms/frame (+88%); marker CoroutinesDelayedCalls 0.1594 -> 0.5622 ms/frame (+253%); marker Physics.Simulate 0.0989 -> 0.3397 ms/frame (+243%) |
| Menu_Main\|S2-4min | yskha | 2026-10-25 06:57:08 | 15.20 | 21.84 | 42.36 | 11951 | REGRESSION: cpuBusyMs 11.583251953125 -> 15.202925682067871 (+31%); p95Ms 15.22900390625 -> 21.844799041748047 (+43%); gcKB 20.374347686767578 -> 42.35520935058594 (+108%); marker PlayerLoop 11.5601 -> 15.1657 ms/frame (+31%); marker UGUI.Rendering.UpdateBatches 4.1482 -> 5.6229 ms/frame (+36%) |
| Menu_Main\|Garland | yskha | 2026-10-25 07:01:07 | 16.32 | 24.04 | 42.59 | 16280 | REGRESSION: cpuBusyMs 13.767739295959473 -> 16.319618225097656 (+19%); gcKB 31.3652400970459 -> 42.59490203857422 (+36%); marker UGUI.Rendering.UpdateBatches 0.8188 -> 5.7573 ms/frame (+603%); marker CoroutinesDelayedCalls 0.2016 -> 0.5166 ms/frame (+156%); marker LightFauna.Tick.PrismScan 0.1389 -> 0.4233 ms/frame (+205%) |
| MinigameScurryMultiplayer_Gameplay\|MinigameScurryMultiplayer_Gameplay | yskha | 2026-10-25 07:03:38 | 11.23 | 31.82 | 45.88 | 2619 | adopted as the baseline |
| MinigameSkimRace\|SkimRace | yskha | 2026-10-25 07:16:29 | 11.33 | 19.21 | 51.85 | 9520 | adopted as the baseline |
| MinigameWarpline\|MinigameWarpline | yskha | 2026-10-25 07:25:54 | 13.78 | 22.53 | 55.17 | 8532 | within baseline |
| Menu_Main\|S2-4min | yskha | 2026-10-25 08:19:05 | 11.58 | 15.23 | 20.37 | 10748 | within baseline |
| Menu_Main\|Garland | yskha | 2026-10-25 08:23:05 | 12.83 | 25.26 | 20.53 | 15251 | REGRESSION: marker UGUI.Rendering.UpdateBatches 0.8188 -> 4.563 ms/frame (+457%) |
| MinigameGrizzlyCharge\|MinigameGrizzlyCharge | mrglo | 2026-10-25 15:19:30 | 8.76 | 23.27 | 88.84 | 12344 | REGRESSION: cpuBusyMs 6.47861385345459 -> 8.757814407348633 (+35%); p95Ms 19.186302185058594 -> 23.271499633789062 (+21%); marker LateBehaviourUpdate 0.3973 -> 2.44 ms/frame (+514%) |
| Menu_Main\|S2-4min | owner | 2026-10-25 18:18:12 | 17.64 | 25.08 | 19.72 | 12285 | adopted as the baseline |
| MinigameSkimRace\|SkimRace | owner | 2026-10-25 18:21:41 | 14.90 | 33.35 | 29.54 | 10128 | adopted as the baseline |
| Menu_Main\|S2-4min | yskha | 2026-10-25 18:37:50 | 10.88 | 13.88 | 20.06 | 11912 | within baseline |
| Menu_Main\|Garland | yskha | 2026-10-25 18:41:50 | 11.03 | 14.44 | 19.30 | 15522 | REGRESSION: marker UGUI.Rendering.UpdateBatches 0.8188 -> 4.1744 ms/frame (+410%) |
| Menu_Main\|S2-4min | yskha | 2026-10-25 21:50:16 | 16.83 | 31.09 | 39.59 | 10546 | REGRESSION: cpuBusyMs 11.583251953125 -> 16.829206466674805 (+45%); p95Ms 15.22900390625 -> 31.093502044677734 (+104%); gcKB 20.374347686767578 -> 39.58917999267578 (+94%); marker PlayerLoop 11.5601 -> 16.7356 ms/frame (+45%); marker UGUI.Rendering.UpdateBatches 4.1482 -> 5.653 ms/frame (+36%); marker Inl_UniversalRenderTotal 1.5724 -> 3.4127 ms/frame (+117%); marker LateBehaviourUpdate 0.7454 -> 1.8441 ms/frame (+147%) |
| MinigameRegatta\|MinigameRegatta | yskha | 2026-10-25 21:53:24 | 11.55 | 14.69 | 93.24 | 3188 | within baseline |
| Menu_Main\|S2-4min | yskha | 2026-10-25 22:28:54 | 12.66 | 16.65 | 19.94 | 11213 | REGRESSION: marker LateBehaviourUpdate 0.7454 -> 1.2037 ms/frame (+61%) |
| Menu_Main\|Garland | yskha | 2026-10-25 22:32:54 | 11.75 | 15.94 | 18.97 | 14207 | REGRESSION: marker UGUI.Rendering.UpdateBatches 0.8188 -> 4.2108 ms/frame (+414%) |
| Menu_Main\|S2-4min | yskha | 2026-10-25 22:41:52 | 11.60 | 17.01 | 18.99 | 12286 | within baseline |
| Menu_Main\|Garland | yskha | 2026-10-25 22:45:52 | 14.52 | 22.43 | 20.96 | 16470 | REGRESSION: marker UGUI.Rendering.UpdateBatches 0.8188 -> 4.8871 ms/frame (+497%) |
| MinigameRegatta\|MinigameRegatta | yskha | 2026-10-25 22:48:56 | 7.66 | 12.99 | 21.60 | 6469 | REGRESSION: marker LateBehaviourUpdate 1.2338 -> 2.0717 ms/frame (+68%); marker BlackHole.Tick 0.8936 -> 1.6136 ms/frame (+81%); marker BlackHole.Field 0.8637 -> 1.5805 ms/frame (+83%) |
| Menu_Main\|S2-4min | yskha | 2026-10-25 23:05:02 | 12.59 | 21.89 | 17.57 | 5386 | REGRESSION: p95Ms 15.22900390625 -> 21.891199111938477 (+44%); marker UGUI.Rendering.UpdateBatches 4.1482 -> 6.6938 ms/frame (+61%); marker LateBehaviourUpdate 0.7454 -> 2.7279 ms/frame (+266%) |
| Menu_Main\|S2-4min | yskha | 2026-10-26 00:01:25 | 12.05 | 15.53 | 19.46 | 15079 | within baseline |
| MinigameRegatta\|MinigameRegatta | yskha | 2026-10-26 00:13:52 | 12.72 | 18.50 | 25.59 | 12943 | REGRESSION: p95Ms 14.685699462890625 -> 18.504301071166992 (+26%); marker BehaviourUpdate 1.2319 -> 5.0397 ms/frame (+309%); marker UGUI.Rendering.UpdateBatches 0.3546 -> 1.0722 ms/frame (+202%) |
| Menu_Main\|S2-4min | yskha | 2026-10-26 00:27:27 | 14.25 | 22.04 | 23.82 | 11966 | REGRESSION: cpuBusyMs 11.583251953125 -> 14.254095077514648 (+23%); p95Ms 15.22900390625 -> 22.043701171875 (+45%); marker UGUI.Rendering.UpdateBatches 4.1482 -> 6.2493 ms/frame (+51%); marker LateBehaviourUpdate 0.7454 -> 2.485 ms/frame (+233%) |
| MinigameSwitchback\|MinigameSwitchback | yskha | 2026-10-26 00:29:49 | 12.07 | 26.02 | 43.35 | 4191 | within baseline |
| Menu_Main\|S2-4min | yskha | 2026-10-26 00:44:28 | 15.48 | 31.18 | 38.94 | 11693 | REGRESSION: cpuBusyMs 11.583251953125 -> 15.477563858032227 (+34%); p95Ms 15.22900390625 -> 31.177898406982422 (+105%); gcKB 20.374347686767578 -> 38.94325637817383 (+91%); marker PlayerLoop 11.5601 -> 15.4247 ms/frame (+33%); marker Inl_UniversalRenderTotal 1.5724 -> 3.1402 ms/frame (+100%) |
| Menu_Main\|Garland | yskha | 2026-10-26 00:48:30 | 16.05 | 34.67 | 39.15 | 15581 | REGRESSION: cpuBusyMs 13.767739295959473 -> 16.049108505249023 (+17%); marker UGUI.Rendering.UpdateBatches 0.8188 -> 4.7363 ms/frame (+478%) |
| MinigameSwitchback\|MinigameSwitchback | yskha | 2026-10-26 01:40:04 | 10.27 | 20.49 | 79.47 | 4575 | REGRESSION: gcKB 43.353912353515625 -> 79.47058868408203 (+83%) |
| Menu_Main\|S2-4min | yskha | 2026-10-26 02:10:35 | 14.48 | 22.85 | 32.67 | 14367 | REGRESSION: cpuBusyMs 11.583251953125 -> 14.47546100616455 (+25%); p95Ms 15.22900390625 -> 22.850305557250977 (+50%); gcKB 20.374347686767578 -> 32.66828536987305 (+60%); marker Inl_UniversalRenderTotal 1.5724 -> 2.6968 ms/frame (+72%) |
| Menu_Main\|S2-4min | yskha | 2026-10-26 02:23:36 | 14.74 | 19.80 | 20.42 | 11287 | REGRESSION: cpuBusyMs 11.583251953125 -> 14.7449312210083 (+27%); p95Ms 15.22900390625 -> 19.803499221801758 (+30%); marker LateBehaviourUpdate 0.7454 -> 2.6696 ms/frame (+258%); marker BlackHole.Tick 0.0099 -> 2.0564 ms/frame (+20672%); marker BlackHole.Field 0.0025 -> 2.0219 ms/frame (+80776%) |
| Menu_Main\|Garland | yskha | 2026-10-26 02:27:37 | 12.22 | 18.79 | 19.63 | 15578 | REGRESSION: marker UGUI.Rendering.UpdateBatches 0.8188 -> 4.6481 ms/frame (+468%) |
| Menu_Main\|S2-4min | yskha | 2026-10-26 02:52:49 | 12.34 | 15.47 | 19.89 | 9334 | within baseline |
| MinigameScarabScramble\|MinigameScarabScramble | yskha | 2026-10-26 02:58:00 | 6.41 | 12.06 | 26.47 | 6623 | adopted as the baseline |
| Menu_Main\|Garland | yskha | 2026-10-26 03:17:26 | 12.26 | 16.30 | 19.79 | 17286 | REGRESSION: marker UGUI.Rendering.UpdateBatches 0.8188 -> 4.3157 ms/frame (+427%) |
| MinigameBreakwater\|MinigameBreakwater | yskha | 2026-10-26 03:22:45 | 7.71 | 12.31 | 20.31 | 7599 | adopted as the baseline |
| Menu_Main\|S2-4min | owner | 2026-10-26 12:00:55 | 22.15 | 26.83 | 20.12 | 11992 | REGRESSION: cpuBusyMs 17.642911911010742 -> 22.15468406677246 (+26%); marker Inl_UniversalRenderTotal 1.7527 -> 2.9132 ms/frame (+66%) |
| Menu_Main\|S2-4min | yskha | 2026-10-26 21:37:34 | 13.65 | 26.35 | 19.82 | 12358 | REGRESSION: cpuBusyMs 11.583251953125 -> 13.653483390808105 (+18%); p95Ms 15.22900390625 -> 26.35099983215332 (+73%); marker LateBehaviourUpdate 0.7454 -> 2.1829 ms/frame (+193%); marker BlackHole.Tick 0.0099 -> 1.178 ms/frame (+11799%); marker BlackHole.Field 0.0025 -> 1.1446 ms/frame (+45684%) |
| MinigameSwitchback\|MinigameSwitchback | yskha | 2026-10-26 22:00:20 | 6.64 | 12.06 | 24.41 | 5371 | improved |
| MinigameBreakwater\|MinigameBreakwater | yskha | 2026-10-26 22:04:09 | 6.55 | 12.31 | 23.75 | 5746 | within baseline |
| Menu_Main\|S2-4min | yskha | 2026-10-26 22:09:18 | 13.22 | 27.66 | 20.75 | 12839 | REGRESSION: p95Ms 15.22900390625 -> 27.657100677490234 (+82%); marker Inl_UniversalRenderTotal 1.5724 -> 2.7345 ms/frame (+74%); marker LateBehaviourUpdate 0.7454 -> 1.2941 ms/frame (+74%) |
| Menu_Main\|Garland | yskha | 2026-10-26 22:13:19 | 13.55 | 29.21 | 22.02 | 17047 | REGRESSION: marker UGUI.Rendering.UpdateBatches 0.8188 -> 4.3947 ms/frame (+437%) |
| Menu_Main\|S2-4min | yskha | 2026-10-26 23:47:50 | 15.38 | 23.99 | 28.01 | 12503 | REGRESSION: cpuBusyMs 11.583251953125 -> 15.380742073059082 (+33%); p95Ms 15.22900390625 -> 23.990602493286133 (+58%); gcKB 20.374347686767578 -> 28.013641357421875 (+37%); marker PlayerLoop 11.5601 -> 15.3519 ms/frame (+33%); marker BehaviourUpdate 2.2361 -> 4.256 ms/frame (+90%); marker LateBehaviourUpdate 0.7454 -> 3.2429 ms/frame (+335%); marker Inl_UniversalRenderTotal 1.5724 -> 2.2735 ms/frame (+45%); marker BlackHole.Tick 0.0099 -> 1.9889 ms/frame (+19990%); marker BlackHole.Field 0.0025 -> 1.947 ms/frame (+77780%) |
| Menu_Main\|Garland | yskha | 2026-10-26 23:51:49 | 16.55 | 27.40 | 29.49 | 17794 | REGRESSION: cpuBusyMs 13.767739295959473 -> 16.548059463500977 (+20%); marker BehaviourUpdate 2.5217 -> 3.8504 ms/frame (+53%); marker UGUI.Rendering.UpdateBatches 0.8188 -> 1.5038 ms/frame (+84%); marker CoroutinesDelayedCalls 0.2016 -> 0.5976 ms/frame (+196%); marker LightFauna.Tick.PrismScan 0.1389 -> 0.4634 ms/frame (+234%) |
| MinigameWarpline\|MinigameWarpline | yskha | 2026-10-27 00:18:28 | 9.93 | 14.99 | 22.14 | 7129 | improved |
| MinigameWarpline\|MinigameWarpline | yskha | 2026-10-27 00:30:25 | 13.25 | 20.10 | 29.19 | 7302 | improved |
| MinigameWarpline\|MinigameWarpline | yskha | 2026-10-27 01:22:42 | 9.26 | 12.65 | 42.51 | 374 | improved |
| Menu_Main\|S2-4min | yskha | 2026-10-27 01:39:06 | 8.16 | 11.19 | 18.86 | 10657 | improved |
| Menu_Main\|S2-4min | yskha | 2026-10-27 02:21:56 | 8.99 | 20.57 | 18.01 | 11222 | REGRESSION: p95Ms 15.22900390625 -> 20.57009506225586 (+35%); marker LateBehaviourUpdate 0.7454 -> 1.6822 ms/frame (+126%) |
| MinigameBreakwater\|MinigameBreakwater | yskha | 2026-10-27 03:18:12 | 7.51 | 11.78 | 20.62 | 6684 | within baseline |
| Menu_Main\|S2-4min | belac | 2026-10-27 16:26:36 | 6.26 | 11.72 | 24.42 | 10867 | adopted as the baseline |
| Menu_Main\|Garland | belac | 2026-10-27 16:30:36 | 6.63 | 11.56 | 23.64 | 17900 | REGRESSION: gcKB 16.043190002441406 -> 23.63743019104004 (+47%) |
| Menu_Main\|S2-4min | yskha | 2026-10-27 17:55:11 | 11.12 | 21.83 | 18.99 | 11054 | REGRESSION: p95Ms 15.22900390625 -> 21.82779884338379 (+43%); marker LateBehaviourUpdate 0.7454 -> 2.5545 ms/frame (+243%) |
| Menu_Main\|S2-4min | yskha | 2026-10-27 18:05:20 | 10.29 | 14.74 | 21.64 | 9902 | REGRESSION: marker LateBehaviourUpdate 0.7454 -> 2.9039 ms/frame (+290%) |
| Menu_Main\|Garland | yskha | 2026-10-27 18:09:20 | 8.40 | 11.82 | 17.70 | 14620 | REGRESSION: marker UGUI.Rendering.UpdateBatches 0.8188 -> 1.3959 ms/frame (+70%) |
| MinigameBreakwater\|MinigameBreakwater | yskha | 2026-10-27 18:28:55 | 6.31 | 11.48 | 25.99 | 6322 | REGRESSION: gcKB 20.30805015563965 -> 25.985919952392578 (+28%) |
| Menu_Main\|S2-4min | yskha | 2026-10-27 18:33:29 | 11.52 | 14.60 | 18.36 | 12386 | REGRESSION: marker Inl_UniversalRenderTotal 1.5724 -> 3.005 ms/frame (+91%) |
| Menu_Main\|Garland | yskha | 2026-10-27 18:37:29 | 11.10 | 14.51 | 18.65 | 16613 | REGRESSION: marker UGUI.Rendering.UpdateBatches 0.8188 -> 3.0899 ms/frame (+277%) |
| Menu_Main\|S2-4min | yskha | 2026-10-27 19:50:36 | 11.12 | 14.70 | 19.06 | 12073 | REGRESSION: marker LateBehaviourUpdate 0.7454 -> 2.4511 ms/frame (+229%) |
| Menu_Main\|Garland | yskha | 2026-10-27 19:54:36 | 11.43 | 15.09 | 18.47 | 15933 | REGRESSION: marker UGUI.Rendering.UpdateBatches 0.8188 -> 1.452 ms/frame (+77%) |
| Menu_Main\|S2-4min | yskha | 2026-10-27 22:32:19 | 11.55 | 23.39 | 18.97 | 12199 | REGRESSION: p95Ms 15.22900390625 -> 23.38840103149414 (+54%); marker LateBehaviourUpdate 0.7454 -> 3.3009 ms/frame (+343%) |
| Menu_Main\|S2-4min | yskha | 2026-10-28 00:02:13 | 7.38 | 10.39 | 17.70 | 11165 | improved |
| MinigameSkimRace\|SkimRace | yskha | 2026-10-28 00:05:38 | 12.15 | 19.01 | 26.74 | 12583 | REGRESSION: marker BehaviourUpdate 2.8945 -> 5.2361 ms/frame (+81%); marker SkimRace.Pilot.Decide 0.0883 -> 2.1497 ms/frame (+2335%); marker SkimRace.Pilot.FillObstacles 0.4598 -> 1.0159 ms/frame (+121%); marker SkimRace.Driver.GuardMass 0.0807 -> 0.304 ms/frame (+277%) |
| Menu_Main\|S2-4min | yskha | 2026-10-28 00:19:35 | 9.85 | 29.67 | 56.97 | 12347 | REGRESSION: p95Ms 15.22900390625 -> 29.666900634765625 (+95%); gcKB 20.374347686767578 -> 56.96968078613281 (+180%); marker LateBehaviourUpdate 0.7454 -> 1.8978 ms/frame (+155%) |
| Menu_Main\|Garland | yskha | 2026-10-28 00:23:34 | 11.84 | 23.98 | 18.94 | 17621 | REGRESSION: marker UGUI.Rendering.UpdateBatches 0.8188 -> 1.1751 ms/frame (+44%) |
| Menu_Main\|S2-4min | yskha | 2026-10-28 00:52:10 | 9.81 | 15.61 | 22.73 | 12400 | REGRESSION: marker LateBehaviourUpdate 0.7454 -> 1.4968 ms/frame (+101%) |
| Menu_Main\|S2-4min | yskha | 2026-10-28 01:05:46 | 9.60 | 14.46 | 22.53 | 12496 | REGRESSION: marker LateBehaviourUpdate 0.7454 -> 1.6495 ms/frame (+121%) |
| Menu_Main\|Garland | yskha | 2026-10-28 01:09:47 | 9.96 | 22.86 | 22.07 | 17680 | REGRESSION: marker UGUI.Rendering.UpdateBatches 0.8188 -> 1.4486 ms/frame (+77%) |
| Menu_Main\|S2-4min | yskha | 2026-10-28 01:31:11 | 9.82 | 13.70 | 18.91 | 12083 | REGRESSION: marker LateBehaviourUpdate 0.7454 -> 1.7333 ms/frame (+133%) |
| Menu_Main\|S2-4min | yskha | 2026-10-28 01:44:03 | 9.76 | 27.88 | 322.94 | 12379 | REGRESSION: p95Ms 15.22900390625 -> 27.883298873901367 (+83%); gcKB 20.374347686767578 -> 322.9427795410156 (+1485%); marker LateBehaviourUpdate 0.7454 -> 1.2521 ms/frame (+68%) |
| Menu_Main\|Garland | yskha | 2026-10-28 01:48:00 | 10.03 | 14.03 | 18.63 | 17250 | REGRESSION: marker UGUI.Rendering.UpdateBatches 0.8188 -> 1.4288 ms/frame (+74%) |
| Menu_Main\|S2-4min | yskha | 2026-10-28 02:32:49 | 9.31 | 13.61 | 18.42 | 12801 | REGRESSION: marker LateBehaviourUpdate 0.7454 -> 1.1175 ms/frame (+50%) |
| Menu_Main\|S2-4min | yskha | 2026-10-28 07:24:45 | 10.05 | 22.88 | 37.45 | 12772 | REGRESSION: p95Ms 15.22900390625 -> 22.882299423217773 (+50%); gcKB 20.374347686767578 -> 37.45463180541992 (+84%); marker LateBehaviourUpdate 0.7454 -> 1.2405 ms/frame (+66%) |
| Menu_Main\|Garland | yskha | 2026-10-28 07:28:43 | 9.44 | 21.93 | 31.36 | 17602 | REGRESSION: marker UGUI.Rendering.UpdateBatches 0.8188 -> 1.3209 ms/frame (+61%) |
| Menu_Main\|S2-4min | yskha | 2026-10-28 07:37:28 | 10.70 | 32.33 | 55.03 | 12523 | REGRESSION: p95Ms 15.22900390625 -> 32.333396911621094 (+112%); gcKB 20.374347686767578 -> 55.02894592285156 (+170%); marker Inl_UniversalRenderTotal 1.5724 -> 2.0604 ms/frame (+31%); marker LateBehaviourUpdate 0.7454 -> 1.7074 ms/frame (+129%) |
| Menu_Main\|Garland | yskha | 2026-10-28 07:41:28 | 14.00 | 25.17 | 37.76 | 17042 | REGRESSION: marker UGUI.Rendering.UpdateBatches 0.8188 -> 1.355 ms/frame (+65%) |
| Menu_Main\|S2-4min | yskha | 2026-10-28 08:37:53 | 13.76 | 27.82 | 47.47 | 12219 | REGRESSION: cpuBusyMs 11.583251953125 -> 13.76067066192627 (+19%); p95Ms 15.22900390625 -> 27.821598052978516 (+83%); gcKB 20.374347686767578 -> 47.47294616699219 (+133%); marker BehaviourUpdate 2.2361 -> 5.3751 ms/frame (+140%); marker LateBehaviourUpdate 0.7454 -> 1.6373 ms/frame (+120%) |
| Menu_Main\|S2-4min | yskha | 2026-10-28 08:46:07 | 13.65 | 26.83 | 38.40 | 12057 | REGRESSION: cpuBusyMs 11.583251953125 -> 13.651314735412598 (+18%); p95Ms 15.22900390625 -> 26.828004837036133 (+76%); gcKB 20.374347686767578 -> 38.396087646484375 (+88%); marker BehaviourUpdate 2.2361 -> 5.2191 ms/frame (+133%); marker LateBehaviourUpdate 0.7454 -> 2.4876 ms/frame (+234%) |
| Menu_Main\|Garland | yskha | 2026-10-28 08:50:09 | 22.51 | 43.99 | 50.38 | 15922 | REGRESSION: cpuBusyMs 13.767739295959473 -> 22.50657081604004 (+63%); gcKB 31.3652400970459 -> 50.37604522705078 (+61%); marker PlayerLoop 13.7399 -> 22.3503 ms/frame (+63%); marker BehaviourUpdate 2.5217 -> 13.7052 ms/frame (+443%); marker UGUI.Rendering.UpdateBatches 0.8188 -> 1.6195 ms/frame (+98%) |
| Menu_Main\|S2-4min | yskha | 2026-10-28 09:06:31 | 10.52 | 22.81 | 36.57 | 12389 | REGRESSION: p95Ms 15.22900390625 -> 22.813804626464844 (+50%); gcKB 20.374347686767578 -> 36.574249267578125 (+80%); marker BehaviourUpdate 2.2361 -> 3.3485 ms/frame (+50%); marker LateBehaviourUpdate 0.7454 -> 1.0723 ms/frame (+44%) |
| Menu_Main\|Garland | yskha | 2026-10-28 09:10:31 | 13.06 | 26.20 | 46.25 | 16247 | REGRESSION: gcKB 31.3652400970459 -> 46.24597930908203 (+47%); marker BehaviourUpdate 2.5217 -> 4.6707 ms/frame (+85%); marker UGUI.Rendering.UpdateBatches 0.8188 -> 1.6551 ms/frame (+102%) |
| Menu_Main\|S2-4min | belac | 2026-10-28 17:30:54 | 5.74 | 11.62 | 31.04 | 9968 | REGRESSION: gcKB 24.42466163635254 -> 31.04372787475586 (+27%) |
| Menu_Main\|Garland | belac | 2026-10-28 17:34:54 | 6.59 | 11.68 | 21.86 | 11629 | REGRESSION: gcKB 16.043190002441406 -> 21.857921600341797 (+36%) |
| Menu_Main\|S2-4min | yskha | 2026-10-28 22:42:11 | 16.26 | 25.46 | 31.98 | 12041 | REGRESSION: cpuBusyMs 11.583251953125 -> 16.260852813720703 (+40%); p95Ms 15.22900390625 -> 25.456697463989258 (+67%); gcKB 20.374347686767578 -> 31.975481033325195 (+57%); marker PlayerLoop 11.5601 -> 16.1602 ms/frame (+40%); marker BehaviourUpdate 2.2361 -> 6.0564 ms/frame (+171%); marker Inl_UniversalRenderTotal 1.5724 -> 2.3894 ms/frame (+52%); marker LateBehaviourUpdate 0.7454 -> 1.3586 ms/frame (+82%) |
| Menu_Main\|Garland | yskha | 2026-10-28 22:46:12 | 24.38 | 33.75 | 23.93 | 16078 | REGRESSION: cpuBusyMs 13.767739295959473 -> 24.377403259277344 (+77%); marker PlayerLoop 13.7399 -> 24.2445 ms/frame (+76%); marker BehaviourUpdate 2.5217 -> 8.0155 ms/frame (+218%); marker UGUI.Rendering.UpdateBatches 0.8188 -> 1.648 ms/frame (+101%) |
| Menu_Main\|S2-4min | yskha | 2026-10-28 23:51:41 | 11.81 | 29.20 | 79.06 | 13139 | REGRESSION: p95Ms 15.22900390625 -> 29.197696685791016 (+92%); gcKB 20.374347686767578 -> 79.05918884277344 (+288%); marker BehaviourUpdate 2.2361 -> 3.9839 ms/frame (+78%); marker LateBehaviourUpdate 0.7454 -> 1.034 ms/frame (+39%) |
| Menu_Main\|Garland | yskha | 2026-10-28 23:55:40 | 13.14 | 37.67 | 110.29 | 18801 | REGRESSION: gcKB 31.3652400970459 -> 110.29055786132812 (+252%); marker BehaviourUpdate 2.5217 -> 4.7825 ms/frame (+90%); marker UGUI.Rendering.UpdateBatches 0.8188 -> 1.6199 ms/frame (+98%) |
| Menu_Main\|S2-4min | yskha | 2026-10-29 00:28:44 | 9.72 | 27.07 | 53.94 | 13561 | REGRESSION: p95Ms 15.22900390625 -> 27.074901580810547 (+78%); gcKB 20.374347686767578 -> 53.9354248046875 (+165%) |
| Menu_Main\|S2-4min | yskha | 2026-10-29 00:33:49 | 13.33 | 29.07 | 60.64 | 13624 | REGRESSION: cpuBusyMs 11.583251953125 -> 13.33448600769043 (+15%); p95Ms 15.22900390625 -> 29.073001861572266 (+91%); gcKB 20.374347686767578 -> 60.63871765136719 (+198%); marker BehaviourUpdate 2.2361 -> 4.4892 ms/frame (+101%); marker Inl_UniversalRenderTotal 1.5724 -> 2.1904 ms/frame (+39%); marker LateBehaviourUpdate 0.7454 -> 1.4174 ms/frame (+90%) |
| Menu_Main\|S2-4min | yskha | 2026-10-29 01:42:01 | 14.38 | 29.13 | 20.28 | 11121 | REGRESSION: cpuBusyMs 11.583251953125 -> 14.378622055053711 (+24%); p95Ms 15.22900390625 -> 29.126699447631836 (+91%); marker BehaviourUpdate 2.2361 -> 5.5945 ms/frame (+150%); marker LateBehaviourUpdate 0.7454 -> 2.1681 ms/frame (+191%) |
| Menu_Main\|Garland | yskha | 2026-10-29 01:46:08 | 15.41 | 30.91 | 21.44 | 13219 | REGRESSION: marker BehaviourUpdate 2.5217 -> 3.5172 ms/frame (+39%); marker UGUI.Rendering.UpdateBatches 0.8188 -> 1.4159 ms/frame (+73%) |
| Menu_Main\|S2-4min | yskha | 2026-10-29 02:34:20 | 10.83 | 27.22 | 43.41 | 11967 | REGRESSION: p95Ms 15.22900390625 -> 27.219097137451172 (+79%); gcKB 20.374347686767578 -> 43.41159439086914 (+113%); marker BehaviourUpdate 2.2361 -> 2.9535 ms/frame (+32%); marker LateBehaviourUpdate 0.7454 -> 1.5558 ms/frame (+109%) |
| Menu_Main\|Garland | yskha | 2026-10-29 02:38:21 | 11.17 | 15.80 | 21.80 | 16974 | REGRESSION: marker UGUI.Rendering.UpdateBatches 0.8188 -> 1.6615 ms/frame (+103%) |
| MinigameSwitchback\|MinigameSwitchback | yskha | 2026-10-29 05:11:16 | 8.76 | 14.01 | 42.13 | 4145 | REGRESSION: marker BehaviourUpdate 1.5892 -> 2.1265 ms/frame (+34%) |
| Menu_Main\|S2-4min | yskha | 2026-10-29 05:16:22 | 14.63 | 45.53 | 70.71 | 12669 | REGRESSION: cpuBusyMs 11.583251953125 -> 14.634629249572754 (+26%); p95Ms 15.22900390625 -> 45.52839279174805 (+199%); gcKB 20.374347686767578 -> 70.71208190917969 (+247%); marker BehaviourUpdate 2.2361 -> 4.0547 ms/frame (+81%); marker Inl_UniversalRenderTotal 1.5724 -> 2.9009 ms/frame (+84%); marker LateBehaviourUpdate 0.7454 -> 2.0333 ms/frame (+173%) |
| Menu_Main\|Garland | yskha | 2026-10-29 05:20:24 | 15.64 | 37.86 | 74.27 | 15496 | REGRESSION: gcKB 31.3652400970459 -> 74.26766967773438 (+137%); marker BehaviourUpdate 2.5217 -> 3.8926 ms/frame (+54%); marker UGUI.Rendering.UpdateBatches 0.8188 -> 1.8819 ms/frame (+130%); marker CoroutinesDelayedCalls 0.2016 -> 1.0789 ms/frame (+435%); marker LightFauna.Tick.PrismScan 0.1389 -> 0.952 ms/frame (+585%); marker LightFauna.Feed 0.0227 -> 0.3204 ms/frame (+1311%) |
| Menu_Main\|S2-4min | yskha | 2026-10-29 06:31:21 | 11.41 | 17.67 | 59.71 | 10579 | REGRESSION: p95Ms 15.22900390625 -> 17.671903610229492 (+16%); gcKB 20.374347686767578 -> 59.7143669128418 (+193%); marker BehaviourUpdate 2.2361 -> 3.5823 ms/frame (+60%); marker Inl_UniversalRenderTotal 1.5724 -> 2.242 ms/frame (+43%); marker LateBehaviourUpdate 0.7454 -> 1.2983 ms/frame (+74%) |
| Menu_Main\|S2-4min | yskha | 2026-10-29 06:44:40 | 12.25 | 39.20 | 413.60 | 12438 | REGRESSION: p95Ms 15.22900390625 -> 39.201900482177734 (+157%); gcKB 20.374347686767578 -> 413.5957336425781 (+1930%); marker BehaviourUpdate 2.2361 -> 3.0211 ms/frame (+35%); marker LateBehaviourUpdate 0.7454 -> 1.811 ms/frame (+143%) |
| Menu_Main\|Garland | yskha | 2026-10-29 06:48:40 | 12.07 | 40.92 | 398.60 | 14679 | REGRESSION: gcKB 31.3652400970459 -> 398.603759765625 (+1171%); marker UGUI.Rendering.UpdateBatches 0.8188 -> 1.171 ms/frame (+43%); marker CoroutinesDelayedCalls 0.2016 -> 0.8445 ms/frame (+319%); marker LightFauna.Tick.PrismScan 0.1389 -> 0.7454 ms/frame (+437%); marker LightFauna.Feed 0.0227 -> 0.2624 ms/frame (+1056%) |
| MinigameSwitchback\|MinigameSwitchback | yskha | 2026-10-29 07:03:23 | 8.43 | 25.69 | 311.02 | 4018 | REGRESSION: gcKB 43.353912353515625 -> 311.0187683105469 (+617%); marker Fauna.BodySync 0.2983 -> 0.5079 ms/frame (+70%) |
| Menu_Main\|S2-4min | yskha | 2026-10-29 07:30:45 | 11.89 | 36.71 | 382.65 | 12786 | REGRESSION: p95Ms 15.22900390625 -> 36.70940017700195 (+141%); gcKB 20.374347686767578 -> 382.6472473144531 (+1778%); marker BehaviourUpdate 2.2361 -> 2.9451 ms/frame (+32%); marker Inl_UniversalRenderTotal 1.5724 -> 2.1237 ms/frame (+35%) |
| Menu_Main\|Garland | yskha | 2026-10-29 07:34:44 | 12.11 | 30.86 | 325.25 | 15094 | REGRESSION: gcKB 31.3652400970459 -> 325.25054931640625 (+937%); marker UGUI.Rendering.UpdateBatches 0.8188 -> 1.078 ms/frame (+32%); marker CoroutinesDelayedCalls 0.2016 -> 0.4233 ms/frame (+110%); marker LightFauna.Feed 0.0227 -> 0.2435 ms/frame (+973%) |
| MinigameSwitchback\|MinigameSwitchback | yskha | 2026-10-29 08:15:59 | 9.69 | 37.18 | 347.32 | 3800 | REGRESSION: p95Ms 26.01799964904785 -> 37.175899505615234 (+43%); gcKB 43.353912353515625 -> 347.32421875 (+701%); marker BehaviourUpdate 1.5892 -> 2.1488 ms/frame (+35%); marker Fauna.BodySync 0.2983 -> 0.5015 ms/frame (+68%) |

<!-- perf_hunt:end -->
