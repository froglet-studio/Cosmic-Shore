# Performance — State, History & Plan

**Read this before any performance work.** It is deliberately short: where the game stands,
what was learned since the effort started, what is left, and how to measure without fooling
yourself. The raw evidence behind every claim — captures, tables, root-cause write-ups — is in
the frozen log, **`Docs/archive/PERFORMANCE_LOG_2026.md`**, and code comments cite its section
numbers (`§0.8`, `§0.11.6`, `Task 10`), which stay valid there.

| | |
|---|---|
| **Rewritten** | 2026-09-22, on branch `claude/bold-fermi-54nlts` @ `561700737` |
| **Merged** | bleeding-edge `ee2ad320f` merged in on 2026-09-23 (`c2e7a7c46`) — 1,390 files, **including a new boot world** (§1.1); then `899f0baba` the same day (`070874533`), for the fix to an every-frame console error |
| **Plan revised** | 2026-09-23 on the merged tree @ `5c6439e68` (§3 — every lever re-checked in code) |
| **Every number below** | an **Editor** number, none a ship number. **§1.0 is the merged tree** (game code `070874533`, measured 2026-09-24 → 26). Everything else is the PRE-merge tree (base `1f160508f`) unless stated. |

---

## 1. Where we are

**The prism renderer is solved. The cost has moved to everything built AROUND prisms.**

The instanced (Entities Graphics / BRG) prism path draws **103,823 prisms at 4.8 ms of CPU**,
**12.9× cheaper** than the old one-GameObject-per-prism path (61.9 ms), and in the boot world
**8,436 prisms render in 23 opaque draw calls**. Draw calls, materials and prism count are not
where the frame goes any more.

### 1.0 The six scenarios on the merged tree (2026-09-24 → 26)

Game code at **`070874533`** (the merged tree). The measuring tools were at `bfcc16117`–`18bbd2445`
and add only diagnostics: the `freeze` hold does nothing unless a measurement switches it on.
Every row is a `diag <label> 15` average. None is a HUD reading.

| # | Scenario | Prism ents | Enabled renderers | FPS | Frame ms | **CPU busy ms** | GPU ms | p99 ms | Draws | GC KB/f |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| S1 | Garland, 4 min | 17,854 | 7,150 | 55.1\* | 18.2\* | **14.3** | 12.4 | 25.9 | 4,772 | 24.5 |
| S2 | Lattice, 8 min | 42,076 | 55,323 | 38.6 | 25.9 | **21.4** | 6.2 | 41.1 | 10,797 | 52.9 |
| S3 | Rampage **intensity 4** (labelled `S3_Rampage1`) | 18,843 | 29,568 | 58.7 | 17.0 | **13.9** | 4.9 | 25.3 | 7,008 | 24.7 |
| S3 | Rampage **intensity 1** | 1,216 | 66 | — | — | — | — | — | — | — |
| S4 | Scurry intensity 4 (Atlantis) | 69,094 | 4,444 | 74.8 | 13.4 | **10.6** | 3.3 | 21.1 | 1,539 | 24.6 |
| S5 | **Wildlife Liberation** | 13,419 | 6,095 | 19.8 | 50.5 | **46.0** | 5.9 | **163.3** | 2,013 | 48.3 |
| S6 | Arboretum, 8 min | 22,182 | 7,335 | 64.8 | 15.4 | **12.5** | 3.5 | 19.5 | 2,991 | 22.1 |

\* S1 ran with VSync on (`frameCapVSync 1`), so its FPS and frame time are capped. Its CPU busy
figure is the one to use. An earlier S4 run (72,583 entities) agrees within 3%.

**The S3 intensity-1 run is invalid, and it is a GAME BUG, not a timing mistake.** It had 1,216
prism entities and 66 enabled renderers. The tester reports (2026-09-26) that **Rampage at
intensity 1 spawns no cell items at all**, just the vessels flying. Crystals do seem to spawn:
the renderer list holds 24 renderers on the four Mass crystal materials, i.e. six crystals of
four shells each, which matches intensity 1's crystal rule. So flora and fauna are what is
missing.

Code read at `aaa1517fe`; none of these explains it:
- The config order in `MinigameRampage.unity` is 1, 2, 3, 4.
- All four configs and spawn profiles resolve, including `Nucleus500.prefab`.
- The spawn profiles differ from intensity 4 only in the three scale fields.
- The `freeze` hold is released on every scene change.
- The planting band cannot collapse, since a 490u nucleus is inside the 0.76 × membrane band.

**It has now happened twice, not once.** The first S3 run (`diag`, 2026-09-25 06:22, labelled
`S3_Rampage1`) was read at the time as intensity 4, because its 9,862 prism entities matched intensity
4's seeded forest (9,830). That match was a coincidence. The run has **no spindle renderer at all**
(115 enabled renderers, none on a `SpindleMaterial_Phase*`), and every Rampage species grows spindles, so
there was no forest. Its prisms were the Dolphins' own trails after 445 s. The real intensity-4 run
(2026-09-25 23:37) has ~21,000 spindle renderers. So two launches that were most likely intensity 1
(the first one is not confirmed) both grew no flora and no fauna.

**Why fauna are missing too, once flora are.** In an IntensityWise cell the fauna loop seeds only while
`Cell.FaunaSpawningEnabled`, i.e. while the cell holds ENVIRONMENT volume (trail + flora). Trails count,
so fauna should still seed off the Dolphins' trails. That they did not either points at the spawner itself
(never started, or its coroutines dead) rather than at planting alone.

**The next measurement is `cells S3_Rampage1`** (new at `CellStateReport.cs`). It dumps every cell's
bootstrap state, running spawner, phase, volumes and per-species counts against their caps to JSON,
and names the first fingerprint it finds. Take it 30 s into an intensity-1 launch, with the Unity
console's error count visible. S3 stays unmeasured until the bug is fixed.

**Against the target, only S5 misses.** The target is 60 fps in a Development build with no
frame over 50 ms, and the editor runs about 2–3× slower than a build.
- **S5** spends 46.0 ms of CPU and has a 163 ms p99 in the editor.
- **S2** is next at 21.4 ms (p99 41 ms).
- **S1, S3 i4, S4 and S6** spend 10.6–14.3 ms.

S5 is not GPU-bound (5.9 ms). **It is main-thread script.**

**Where S5 goes.** `prof S5_Wildlife`, 180 frames. The Profiler's overhead is included, so this
table RANKS. Its PlayerLoop averages 73.9 ms against the diag's 46.0 ms of CPU busy.

| Block | Avg self ms | Median frame | Spike frame | What it is |
|---|---:|---:|---:|---|
| UniTask continuations at `PreLateUpdate` | **20.2** (in 74% of frames) | — | **123** | The AI Sparrows' full-auto loop (`FullAutoActionExecutor`) and every round's flight step (`Projectile.MoveProjectileAsync`). The pool activations, prism hits and vessel sweeps under it are instrumented, and together they are ~2% of it. **This is the p99** |
| `LightFauna.Update` | **13.1** (374 calls) | 13.4 | 15.1 | Each creature, every frame, moves and then re-syncs every body prism: spatial index, shell, render matrix. 323 of the 374 are QuadFish |
| `LightFauna.UpdateBehaviorCoroutine` | **12.7** (in 54% of frames) | **29.9** (35 ticks) | 28.2 | The behaviour tick: goal, vessel overlap, prism-neighbour scan. **~0.85 ms per tick.** The goal query is cached (`BlockDensityGrid.FindDensestRegion`), so the cost is the scan |
| `QuadFishSwimDriver.Update` | 1.6 | | | Fin strokes |
| Camera rendering | 4.8 | | | Not the problem |

All three script blocks are uninstrumented managed code, so the Profiler shows only their outer
edge. **`aaa1517fe` adds markers inside them:**
- `Fauna.BodySync`
- `LightFauna.Tick.Goal`, `LightFauna.Tick.Vessels`, `LightFauna.Tick.PrismScan`
- `LightFauna.Feed`, `LightFauna.Hunt`
- `Projectile.Growth`, `Projectile.SweepVessels`, `Projectile.SweepPrisms`, `Projectile.Fuze`
- `FullAuto.Fire`

One more `prof S5_Wildlife` splits each block.

**The other findings:**

- **Spindles, S2:** `ab "renderers hide *Spindle" "renderers show" 20 6` with the ecology frozen.
  - CPU busy **+5.3 ms ±0.4**, GPU +1.5 ±0.3, draws +8,570.
  - That is for ~50,300 spindle renderers, so **~0.105 ms of CPU per 1,000**.
- **Spindles, S6:** the same command gave **+0.3 ±0.4**, which is noise (draws +176).
- Both runs warned that prism entities drifted 12% / 29% between arms. The interleaved rounds
  balance that out, but it is why the capped world is the clean one.
- **S2's spike frame** (45.9 ms PlayerLoop):
  - `PhyllotacticFlora.GrowCoroutine` took 15.8 ms, 5.0 ms of it `Instantiate` for 26 growth steps.
  - `ShieldRegenCoroutine` took 5.2 ms.
- **S4's spike frame** (27.6 ms): `LOD.Sweep` took **15.9 ms**. That is the collider-LOD tick over
  ~69k prisms. It runs on 20% of frames (every 0.25 s) and averages only 0.64 ms, so it is a 4 Hz
  frame-pacing hitch, not an average cost.
- **GC** is 20–53 KB/frame in every scenario. **The pre-merge 154.5 KB/frame is not reproduced.**
  - S5's largest allocator is `DiagnosticsHUD.Update` itself, at 8.3 KB/f while it samples.
    Discount it.
  - Next are S5's `PreLateUpdate` continuations: 4.6 KB/f over ~59 allocations.
- **`prof` fix (`ded51b6ad`):** `GfxTask_ReadValue` is now counted as a wait. Before, the D3D12
  task worker read ~100% busy in every capture, exactly the frame time in each.

### 1.0.1 The pre-merge Lattice frame (history)

Where it does go — one Profiler frame of the Lattice boot world (main thread 57.6 ms, Editor,
Deep Profile off, `archive §0.11.6`):

| Block | ms | What it is |
|---|---:|---|
| **Camera rendering** (`RenderPlayModeViewCameras`) | **24.1** | … of which **14.2 ms is the main thread idle**, waiting for worker jobs (culling, render-queue extraction, graphics jobs). All draw submission is only **~0.2 ms** |
| **`UpdateScene`** (game code + engine per-frame work) | **28.4** | |
| ↳ `AssembledFlora.Update` (plant growth) | 6.2 | of which `Instantiate` 3.6 ms — ~720 µs per growth step (a prism + a spindle) |
| ↳ rest of `ScriptRunBehaviourUpdate` | 5.3 | |
| ↳ everything outside `ScriptRunBehaviourUpdate` | **~17** | **not expanded in that capture — unattributed** |
| `EditorLoop` | 4.7 | Free in a player build |

Four things were open before the merge. §1.0 supersedes their priority. Lead 1 is now measured
(+5.3 ms in a grown Lattice), and lead 4's 154.5 KB/frame is not reproduced:

1. **~14 ms of render-job waiting.** Best lead: the boot world had **45,197 enabled renderers
   and only 4,599 visible**, and **~40,000 of them were flora spindles** (the branch geometry
   under each lattice prism). One hide test hints spindles cost **~4 ms** — but it compared two
   different world sizes, so it is **not established** (§4.3).
2. **~17 ms of unattributed `UpdateScene`.** Those rows were not expanded in the 09-22 capture
   (an older capture put coroutines at ~2.8 ms of it, `archive §0.8`).
3. **Growth instantiation.** Every growth step and every plant birth `Instantiate`s GameObjects.
4. **Gameplay GC: 154.5 KB/frame** in Menu_Main, inside `UpdateScene`, caller unknown — plus
   single frames allocating up to **7.2 MB** that line up with 108–128 ms hitches.

### 1.1 ⚠ The merge changed the boot world — re-baseline before anything else

PR #892 (merged 2026-09-20) made **Garland** the boot world (`SpawnableGarland`,
`Docs/ECOSYSTEM.md §48`): an authored cell of **4,259 prisms, ~8,147 when
mature, 37 heart colliders**, composed for the far menu camera. **Lattice** — the cell every
boot-world number in this doc and the archive describes — is now **opt-in through the Cell
Selector**.

So the default home screen is probably far cheaper than anything measured here, and the
"boot world" problem is now a "heaviest opt-in cell" problem. The findings still hold as facts
about Lattice and about GameObjects hung off prisms; **their priority does not** until the
scenario set (§3.1) is re-measured on the merged tree. The merge also changed `Spindle.cs`
(+121: sway constants shared with health prisms), `PrismRenderService.cs` (+44) and
`Flora.cs` (+80) — the exact code under lead #1.

**Confirmed in code on the merged tree** (§3.1): Garland is the only `BootDefault` config. The
merge also brought the **Arboretum** (Cell Selector, ~55k prisms at maturity, one spindle per
bond), which is why the scenario set gains an S6.

---

### 1.2 Which prisms draw through entities (audit, 2026-10-15, Yash: "make sure all our prisms are using entities")

**Every `Prism` is on the instanced path, on every device that passes the probe.** Read from the tree, not from memory:

- `Assets/Resources/PrismRenderConfig.asset` has `useInstancedRendering: 1`, so `PrismRenderService.Enabled` is true
  outside an A/B (`prismpath`). `Prism.EnsureRenderEntity` declines only for a prism with no MeshRenderer/MeshFilter, no
  mesh, no material, or no ECS world; every subclass (`HealthPrism`, the fauna body prisms, the trail prisms, the flora
  prisms) inherits it. `SetExoticVisualActive(true)` - the hand-back to a GameObject renderer - has **no caller** in the
  project (the shields moved to a GPU morph on a shared mesh, `PRISM_ANIMATION.md` §5 B4).
- The one gate is the device: `EntitiesGraphicsSupportProbe` (SRP + compute shaders + the `SparseUploader` kernels). A
  device that fails it draws every prism through its MeshRenderer, the 12.9x path - that is §3.6 step 0's phone question,
  still unmeasured. In flight: `claude/webgl-prism-animations-kg5qx7` adds a MeshRenderer stamp path (`PrismMeshRendererStamps`)
  so the clock-material animations survive there (WebGL first); it is for devices that FAIL the probe and changes nothing
  where Entities Graphics runs.

**What is drawn by a GameObject renderer in the lava lamp, and is not a `Prism`:**

| Thing | How it draws today | Batches? | Lever |
|---|---|---|---|
| **Spindles** (the limb under every lattice prism; ~40k enabled in a grown Lattice, ~4.6k visible) | one `Renderer` per limb (`Spindle.cs`) | yes: `sharedMaterial` + shared phase variants, fade through ONE scratch MaterialPropertyBlock, no per-renderer block - SRP-batchable already | **L1** (entities, or one mesh per plant). Structural, ecology LOCKED, Garrett's area |
| **Snow shards** (`SnowChanger`, 4,189 GameObjects at `shardDistance` 120) | one renderer each | yes | **L6** (120 -> 200 = 905 objects; a visual-density call) |
| Crystal Mass columns (`CrystalDepthPrisms`) | ONE MeshRenderer per crystal, every column baked into one mesh | n/a | none needed |
| Swarm / substrate members | their own GPU-instanced draw (`SwarmMemberRenderer`; an SM 3.5 fallback where vertex buffers are missing) | n/a | none needed |
| Charge arcs (`CrystalChargeArcs`) | one dynamic mesh per crystal, asleep under 24 px on screen since 10-15 | n/a | T-20261015-a5a5 (Garrett picks the size) |

So "all prisms on entities" holds; what is left on GameObject renderers is the spindles and the snow, and that is L1 and L6 in
§3.3, unchanged in rank. The code-level practices the lava lamp's per-frame paths are held to (no `renderer.material`, no
per-frame `FindObjects*`, no closure per contact) are the gates in §3.4 plus the measured items in §2's 10-15 rows.

## 2. What we did, since day 1

| When | What happened | Outcome |
|---|---|---|
| 07-08 | **De-spike pass** (PR #573): growth pass sliced under a budget, GC moved behind the splash, cached waits | Menu **~6 → ~50 fps**; SkimRace spike train gone |
| 07-09 | Flora grow-tick paced; prism render entities made from prototypes + batched visibility; collider-LOD classification in Burst (5.5 → 0.7 ms); async pool refills; fauna consume pacing | All shipped; regression list in `archive §1` |
| 07-14/15 | "10 ms `DomainVolumeIndicator` spike" was really the cell volume sum → one Burst job off the main thread; collider-LOD hysteresis | Script tick **15.2 → 1.7 ms** at ~7k prisms. Found **Burst was OFF in the editor** the whole time (every job ~20× slow) |
| 07-17 | Async pool refills broke Sparrow guns (un-injected projectiles) | Fixed; `OnInstanceCreated` hook |
| 08-02 | **Clock-material law**: CPU prism animation managers deleted, animation is GPU-clocked (`Docs/PRISM_ANIMATION.md`); prism deaths became batched pure entities | A 2,408-death frame had cost **1,863 ms** in one `List.Remove`; gone |
| 08-03 | macOS "3 fps" → editor diagnostic toggles, not the Mac | `archive §0.2` |
| 08-06 | Pause-panel prewarm; smoother game → menu return | First-pause hitch gone |
| 08-15 | Shield morphs moved to the GPU; last CPU prism ticker deleted | |
| 08-18 | **FMOD**: a looping event fired as a one-shot leaked one voice per boost | `RuntimeManager.Update` **693 ms → normal** |
| 08-20/21 | Editor stalls: Enter Play Mode Options on, 52 static resets, Soap patched, FMOD Live Update off, reload guard | Play-mode entry and the "Running managed callbacks" hang fixed |
| 08-25 | Prism death path measured: **6.87 µs per death** (was a stale 430 µs estimate); death pooling retired | `Docs/PRISM_EXPLOSION_BENCHMARK.md` |
| 09-02 | FMOD emitters stop outside their max distance | |
| 09-09 | Entities Graphics device gate (an Intel Iris laptop crashed before frame 1) | `Docs/PRISM_ECS_MIGRATION.md §8` |
| 09-10 | **First measurement of the Lattice boot world**: CPU-bound; a periodic spike = one flora **birth** (~1.2 ms of `Instantiate`) | `archive §0.8` |
| 09-14 | Record recovered from an abandoned branch; 1,080 allocating coroutines fixed; GPU-garbage guard on the HUD; `prismpath` A/B switch | |
| 09-15/16 | Standalone lab scene (`PrismGridExplosionTest`, no Bootstrap); **A/B: instanced path 12.9× cheaper than legacy** | `archive §0.10` |
| 09-18/19 | Lab tests on mixed populations | Material interleaving explains **6.4%** of boot-world draws; chunk locality **0%**; GC is **not** the render path |
| 09-20 | Empty-scene baseline; boot world at 1/4/8 min; **Frame Debugger** | ~90% of draws are **not** prisms; prisms = 23 opaque draws; the rest is transparent snow / spindles / crystals. Empty scene's PlayerLoop allocates **304 B** — the HUD's 41.8 KB was editor-side |
| 09-21 | Refused two proposed material conversions after reading the graphs | Spindles fade by alpha — opaque would make withering pop (continuity law) |
| 09-22 | **Frame attributed** in the Profiler: draws ~0.2 ms, render-job waiting 14 ms. 15 dead SOAP listeners stripped from 8 prism prefabs. `renderers` census + `renderers hide/show` | ~40k of 45k enabled renderers are spindles; hide test confounded (§4.3) |
| 09-23 | Merged bleeding-edge (new boot world: Garland); plan re-derived on the merged tree; target + six scenarios confirmed. `freeze` and `ab` console commands | The confounded spindle test can now be re-run in one state (§4.5) |
| 09-25 | First spindle `ab` hid 5 of ~68k renderers (the lattice spindles wear their own materials); `renderers hide *text` added | An accidental A/A: ±3.9 ms CPU noise at 3 × 10 s in the menu, so the re-run is 6 × 20 s (§4.5) |
| 09-25 | S1–S6 `diag`s and the spindle `ab` in S2 + S6; `prof` console command (the Profiler Hierarchy as JSON) | Spindles cost ~+5.3 ms CPU in a grown Lattice; S5 Wildlife Liberation is the worst scenario (50.5 ms) and is not yet attributed — `prof` is how |
| 09-26 | **Measurement moved to the industry-standard method (§4.7).** `diag` gained per-system marker timings via `ProfilerRecorder` (works in a Development build, no Profiler attached), p50/p95 frame time, the run environment, and the label in its file name. The **Profiler → JSON exporter** covers a connected build or a `.data` file. `diag`/`ab` timestamps are now culture-invariant. **Rampage intensity 1 spawns no flora or fauna**, a game bug, open | Every number from here on says whether it came from the Editor or a build |
| 09-26 | `prof` on S2, S4, S5 (and an invalid S3 i1). §1.0 recorded, §3.3 re-ranked. Markers inside S5's three script blocks (`aaa1517fe`); `prof` counts `GfxTask_ReadValue` as a wait (`ded51b6ad`) | **Only S5 misses the target.** It is main-thread script: gunfight continuations 20.2 ms (the p99), creature `Update` 13.1, behaviour tick 12.7. Pick: **L8**, gated on one more `prof S5` |
| 10-05 | Merged `Bug_Hunt`. Skim Race AI pilot: exact laid-mass broadphase, bounded track-shell search, precomputed course segments (`SKIM_RACE_AI.md` §8.0e); `SkimRace.Pilot.*` markers in `diag`; `PrismTimerManager` per-owner lists pooled | Simulator decide cost per AI seat per frame I1 0.108 -> 0.049 ms, I2 0.563 -> 0.303, I4 0.184 -> 0.079, race output byte-identical. Does **not** explain the ~127 ms hand-played Skim Race frame (`SKIM_RACE_AI.md` §8.0c) - that needs a `diag` in the editor |
| 10-06 | First editor `diag` + `prof` of Skim Race I2 with 2 AI: the AI was the largest cost (Decide 7.2 + FillObstacles 2.1 of ~24 ms PlayerLoop; 840 allocations a frame). Fixed exactly: no-alloc 3-value Min/Max (+ gate), planner inner loops in floats, cheaper obstacle gather, pooled skim beams, prism `Awake` GC (`SKIM_RACE_AI.md` §8.0f) | Simulator on Mono in DOUBLE precision (the editor's float mode, `SKIMRACE_RUNTIME=mono`), per seat per frame: I2 2.805 -> 1.986 ms, I4 0.845 -> 0.575, decisions identical (§8.0g: the first rewrite was only identical in single precision; the editor's tests caught it). Editor re-measure owed, in Release code optimization |
| 10-06 | Fast-forwarded to `Ys-bleeding-edge` (64 commits: Skim Race team plan / difficulty / general policy, device tiers + MobileLow render/content tiers, touch controls, drift haptics, party fixes) and audited every new per-frame path with this effort's tools: gates, `scan_perframe_allocations.py`, a pattern scan of every added line, the simulator's AI cost before/after, and a read of each new `Update`. Two fixes: `R_VesselActionHandler`'s editor/dev diagnostics built a string per input press at the call site (presses arrive every few frames on every vessel, AI included); `Trail.RemoveOldest` re-cast to `ushort` (a merge regression of BH-4.8). `diag` now also times `SkimRace.Driver.TrackMpc` / `.GuardMass` | AI cost unchanged by the new features (Mono double precision, per seat per frame): I1 0.252 -> 0.251 ms, I2 2.074 -> 2.036, I4 0.621 -> 0.615 (.NET +2-7%, measured while the engine build ran - noise). Everything else neutral or a win: boost event only on change, rate-limited skim tick, `RaceTrailCap` (MobileLow) returns retired trail prisms to their pool, membrane instance cap on low tiers |
| 10-07 | Editor re-measure of Skim Race I2 with 2 AI on `perf/performance-optimization` (`SKIM_RACE_AI.md` §8.0h). The `prof` caught three Burst jobs running as managed code (Burst still compiling after the branch switch: `ShellContact.Query` 1.32 ms vs 0.08). `prof` now flags managed job time, `diag` times `ShellContact.Query` and records the Editor's Code Optimization | **28.3 -> 18.0 ms (35 -> 55 fps)**, p95 39.4 -> 26.9, GC 78 -> 27 KB/frame. Like-for-like AI cost (Decide + FillObstacles) 9.3 -> 4.3 ms; the rest of the drop is a smaller race (5717 vs 8362 prisms) and systems not touched here. What is left in the AI is the 20 Hz track planner's bursts (2.9 of 3.35 ms, ~4 ms per seat per re-plan); every lever there changes timing and waits on a decision |
| 10-07 | Skim Race AI seats stagger their track-planner re-plans (`SkimRaceReplanGate`, `TrackMpcStaggerSeats`; `SKIM_RACE_AI.md` §8.0i), the user's pick of §8.0h's options. One re-plan per frame; a seat whose frame is taken waits one frame, never two | Simulator, Mono double precision (the editor's mode), I2 with 2 seats: AI frame cost p90 11.0 -> 6.8 ms, p99 15.0 -> 9.6, same average; coincident re-plans 89% -> 0%. Racing unchanged within noise over 400 races per arm (+0.6 s per seat, SE 0.4) |
| 10-07 | Editor check of the stagger (`SKIM_RACE_AI.md` §8.0j): `TrackMpc` p95 10.55 -> 5.28 ms, `Decide` p95 11.13 -> 7.13, spike frame 1 re-plan instead of 2. The new `prof` check found that **none of the game's own Burst jobs are Burst-compiled in the editor since the `Ys-bleeding-edge` merge**: workers run `ShellContactQueryJob` managed at 0.6-0.97 ms instead of `(Burst)` 0.013 ms; `CellVolumeSumJob`, `FindDensestRegionJob`, the LOD sweep and the transform flush run managed too; package jobs still compile | ~1.9 ms a frame of main thread in Skim Race (`ShellContact.Query` 1.53 vs 0.08 ms) plus worker time, and every mode that runs these jobs. Cause not visible from the repository (same Burst 1.8.29, Burst on, no settings or attribute change): needs the editor's Burst messages |
| 10-07 | Compared with `claude/confident-pascal-w76l2o` @ `41e27e9d2`. It already contains this branch up to the stagger (`e8fc01dcd`) plus 37 commits: retuned AI policies (v5-i1, v3-i2, v2-i4, v3-general), its own fixed-grid re-plan stagger (dropped in favour of `SkimRaceReplanGate`), the objective refactor, Regatta, Hijack ownership replication, toasts, Urchin AI/HUD, fleet boost policies and the party/netcode request policy. `SkimRaceDriver.cs` is identical | AI cost the same within noise (simulator, Mono double precision, 2 seats, 18 ms frames, 6 races): per seat I2 1.67 vs 1.61 ms, I1 0.150 vs 0.155, I4 0.46 vs 0.51 (`MassGuardSeconds` +3-6%). No new per-frame allocations (`scan_perframe_allocations.py` identical once line numbers are dropped); the new `Update`s are 0.5 s toast polls and Hijack's 0.1 s ownership flush; no package, project-setting or Burst-job change, so the managed-Burst regression (§8.0j) is on both |
| 10-08 | Burst follow-up (`SKIM_RACE_AI.md` §8.0j): the Burst Inspector compiles all eight flagged jobs, and four `prof`s with Synchronous Compilation on still show the game's jobs managed (1.3-1.9 ms a frame on the main thread). So it is neither a compile error nor a compile still in flight. New `burst` console command (Burst's switches, an attached debugger, the Code Optimization, and a probe job that reports how it really ran); `prof` reports now carry the run environment, and `diag`'s gains the job compiler switch and an attached debugger | Cause still open; the `burst` line decides between a runtime switch, a debugger and Burst not reaching Assembly-CSharp |
| 10-08 | **Cause found and fixed** (`SKIM_RACE_AI.md` §8.0k). `Editor.log`: `` Unable to find internal function `System.MathF::Sqrt` `` (and Acos, Pow, Exp, Sin, Cos), then `Burst is disabled for` the game's jobs, "due to a failure to resolve one or more `extern` methods". In Unity's Mono those `MathF` members are InternalCalls Burst cannot link. `SubstrateAgentJob` and `SwarmPoseJob` (from `bleeding-edge`, 10-05) called them, and Burst builds every Assembly-CSharp job into one library, so every game job ran managed. Both kernels now call `(float)System.Math` helpers (Burst intrinsics); both text gates (which had recommended `MathF`) fail on any other `MathF` member and name exactly the six on the old sources. `burst` now prints Burst's refusal from the log. First the cache-clean (`Library/BurstCache`) fixed a separate Release hang at "Building arena 42%" (stale compiled jobs) | Release baseline with the jobs still managed: 65.8 fps, frame avg 15.2 / p95 19.9 ms, CPU busy 13.1, `Decide` 2.35 / p95 4.83, `ShellContact.Query` 0.39, managed jobs 0.32 ms of main thread. Expected: ~0.3 ms back in Skim Race (`ShellContact.Query` to ~0.1) plus worker time; more in ecology modes. Harnesses: substrate group K 100% bit-identical (206,100 agent-steps) once its reference uses the same primitives; swarm, ecology-LOD, showcase-cell pass. **Not yet measured in Unity** |
| 10-08 | Merged `Ys-bleeding-edge` (`280c0475f`, which carries `bleeding-edge` `afd66621d`) and re-checked this effort's work on the result. 8 conflicts: the Skim Race pilot/objective/deployment take theirs (the team plan moved into the objective) and keep the seat-stagger `ReplanGate`; the rest are doc or identical-fix conflicts. None of the simulator's AI files, the policy assets, the Burst job files' math, the perf tools or `SkimRaceDriver` changed in the merge. New on their side and checked for cost: `Prism.ApplyColorShade` (a float compare per material sync unless a gyroid shades the prism), a held-input map in `R_VesselActionHandler`, `SubstrateSiege` (managed tick, not Burst), and their fix to complete the swarm pose job before its inputs are disposed | All C# gates, both Burst MathF gates, the substrate (K 100% bit-identical) and swarm harnesses, both glue type-checks (the swarm one after adding `EventStopTrigger` to its FMOD stub: red on `Ys-bleeding-edge` too), `burst` tests, unity_refcompile player and player-dev OK (editor: the 4 old ones only), Froglet Engine tests 1596/1596 and 352/352. The port's live compile has 9 errors from upstream files (Nca creature, nested gyroid, swarm audio, wormhole, Grizzly claw: engine gaps; `Ys-bleeding-edge` has the same 9 plus 19 this branch fixed). Then merged its next 7 commits (`9096b5482`: party size enforced by the session, the net session record, edit-mode suites on Prisma), no conflicts. That harness (`Tools/Build/prisma_edit_mode_tests`) runs this effort's own suites with no Editor, on Prisma's Unity runtime: SkimRaceAITests 37, ProfilerCaptureTests 42, BurstProbeTests 4, MarkerBudget 22, ABComparison 32, FrameBoundness 20, RendererCensus 13, SkimRaceCourseQuery 5, **175/175 pass**. Its default suites: 559/569, the 10 failures being exactly the pre-existing ones `REVIEW_INVITE_AND_RESILIENCE.md` F8 lists. Unity test steps unchanged (`SKIM_RACE_AI.md` §8.0k) |
| 10-09 | **Per-system profiling without Unity.** Amoebius's (the engine under `Port/`, renamed from Prisma 2026-10-09) `ProfilerMarker` was a no-op, so every `diag` marker read 0 there. The engine now records each marker the game times (time, calls and **bytes allocated** per frame, loop thread), and the game's unchanged `diag` reports real numbers in Amoebius's Debug player (`--do console diag`). Also closed the 9 live-compile gaps that kept the game from building in Amoebius. One command runs a match and writes the markers: `python3 Port/tools/amoebius_markers_run.py` (`Port/docs/ARCHITECTURE.md` §11.3b) | Skim Race I2, 3 seats (2 AI), 1800 frames, fully-optimised JIT: the ranking matches the 10-08 Unity `diag` (`Pilot.Update` 0.49, `Decide` 0.45, `TrackMpc` 0.40, `PoolRefill.Squirrel Prism` 0.07, `ShellContact.Query` 0.06 ms/frame). **New lead: `PoolRefill.Squirrel Prism` allocates ~9 KB/frame**, about a third of the 30 KB/frame in Unity, and `PoolActivate` ~0.7 KB; `LightFauna.Feed` ~1.2 KB when fauna are in the cell. Hypothesis until a Unity `prof` (sort GC) confirms it. Repeatability: allocations ~1%, ms 5-10% between identical runs |
| 10-14 | Merged `bleeding-edge` `d0ee1c45d` (668 commits; Prisma → **Amoebius**; the Vessel Studio; UGS-only online play; the FrogletTools menu reorganised) and re-checked this effort on the result. Upstream had closed five of the nine engine gaps this branch filled on 10-09 (`attachedRigidbody`, `OverlapBoxNonAlloc`, `EventStopTrigger`, `CopyTexture`, `CullMode`/`RenderQueue`) its own way; those took upstream's version and this branch's duplicates were deleted. The marker collector, `capture`, the Capture Performance window and the reader are not upstream and stay. **None of §3.6's five owed tests was done upstream**, and the perf record itself had no upstream change. Upstream's `Docs/QA/DEV_TASKS.md` DT-003 lists the 35 red edit-mode tests, which covers the `RaceRankToastDriver` and `SparrowCombatTier` failures this branch's Amoebius run of the suite showed (559/569, pre-existing). `Tools/Build/prisma_markers_run.py` is now `amoebius_markers_run.py` | Amoebius Debug build, engine tests, `unity_refcompile player-dev` (RESULT OK) all green on the merged tree; `/verify-unity` still unavailable |
| 10-15 | Merged this effort into **`Ys-bleeding-edge`** (the owner's working branch; it already carried `bleeding-edge` `d9b143d38`), so the capture tooling runs there. Amoebius did not build on that tree: upstream `41a53b61c` (browser Toybox boot) made `OfflineModeService` use Netcode's `SinglePlayerTransport`, which the engine lacked - a stand-in was added. `capture` now records **every enabled camera**: a second camera (the Third Eye window, a preview, a Scene view) renders the world again, and the reader warns when it sees more than one, so "fewer frames with Third Eye open" is a measured fact, not a guess | Amoebius Debug build + a real `capture` on the Ys tree (cameras: `1 enabled`), engine tests 1979/1979, `unity_refcompile player-dev` OK, gates clean |
| 10-15 | **The perf-hunt system** (§3.4): `perf_hunt.py` (gates, collect every shared capture from every branch, compare with baselines, the Amoebius allocation gate, the sweep that writes `Docs/Reports/PERFORMANCE.md` and files team tasks), `/perf-hunt` (the resolve loop and the checklist every C# change is held to), `.github/workflows/perf-daily.yml` (once a day from `bleeding-edge`), the Performance area in `Docs/Reports/areas.json`. Rides the Bug Hunt check-in's shared perf JSONs (Yash's `BugHuntReports`) exactly as `/bug-ledger` rides its issues. First run: gates green except as recorded; 68 scan findings recorded as the count to beat; the first Amoebius Skim Race allocation baseline committed | Before this nothing checked performance on a schedule: `unity-ci` compiles weekly and `amoebius-parity-ci` smokes the engine on push, neither measures a frame. **First lesson, same day:** the baseline's top allocator, `CrystalChargeArcs.LateUpdate` at ~708 KB/frame (85× the next), is the ENGINE's: Amoebius's `Mesh.Set*(List)` copies every call because its renderer detects change by array identity, while the game's code is allocation-free and Unity's `SetVertices(List)` allocates nothing. So an Amoebius allocation is the game's only when the engine API it calls does not copy; `Docs/perf/amoebius_known.json` lists the known engine copies with evidence, the gate reports them apart, and the engine fix is a task for Shombith on the board |
| 10-15 | **First shared editor run judged by the system.** Yash's Bug Hunt check-in published the 10-08 Skim Race I2 `diag` + `prof` (`BugLedger/shared/reports/yskha/perf/`); the sweep pairs loose diag/prof files into a bundle and judged it: **refused as a baseline** - 0.32 ms/frame of jobs ran MANAGED (`ShellContactQueryJob`, `PrismRender.TransformFlush`, `LOD.Sweep`: the pre-fix run) under a 120 fps cap. Its numbers are the starting numbers already on record (65.8 fps, 13.1 ms CPU busy, 30.2 KB GC/frame, `Decide` 2.35 ms, `TrackMpc` 1.45, 9,568 prisms, instanced path ON). The first Release capture taken AFTER the Burst fix becomes the Skim Race baseline | The pipe works end to end with nothing transcribed: editor → shared report → sweep → verdict |
| 10-15 | **No extra editor UI** (Yash: "automated, as clean as possible; only in Claude when someone asks; a small indicator at editor start"). The Capture Performance window is deleted. The editor has ONE surface, the Scene view's **Bug Hunt & Performance badge** (`BugHuntPerfBadge`, Docs/DIAGNOSTICS.md): bug counts, gates, regressions, captures owed, the next step, and Send to Claude / Capture / Release buttons shown only when they apply. The one dialog is the Bug Ledger's after-play "Send to Claude?" (another session, same day). Every sweep now writes `Docs/perf/status.json`; `perf_hunt.py status` prints it, and the `health_status.py` hook hands it to any Claude session asked for the status or what to test. Proof: `unity_refcompile --config editor` 0 errors in changed files; `perf_hunt.py --self-test`, `health_status.py --self-test`; in-editor test is T-20261015-7d29 |
| 10-15 | **Lava lamp regression found and fixed (Yash: "19-30 fps at the start of lava lamp, used to be 100+").** Amoebius, headless, Menu_Main freestyle, same protocol both sides (fresh profile, 600 warm-up frames, 1,200 measured, tiered JIT off), with a temporary per-script timer in the loop: `e27ca677a` (10-15 01:25) **3.35 ms/frame** -> HEAD `1f8ad4553` **8.5 ms/frame** (frames over 33 ms: 15 -> 68 per 1,800). Three new per-frame costs carried the whole gap: (1) `CrystalChargeArcs` (the Charge light, Garrett's crystals, merged 10-13/14) 2.5-3.2 ms: it rebuilt an arc mesh for every visible crystal every frame, and the lava lamp sees every crystal 150-1,200 units away, **1-11 px in radius on a 1080p screen**, with bolts 1-2 % of that wide; (2) the first-login guide's step finder (`QuestGuideToMicrogameNode`) 0.83 ms: two `FindObjectsByType` scene searches twice a second, forever; (3) `MenuSpotlight` 0.9 ms: a TMP `GetPreferredValues` every frame and a scene-wide Button search every second. Fixes: the arcs sleep on their unlit faces below `sleepBelowScreenRadiusPixels` (24 px, wake at 1.25x; `CrystalChargeArcsProfileSO`, its generator updated); the step finder reads `ModePreviewSession.Current` and searches only while the Arcade grid is unresolved; the spotlight caches its caption size and searches every 5 s (reachability still every 1 s). **After: 3.45 ms/frame** (arcs off the list, step finder 248 -> 1 ms per 300 frames, spotlight 0.93 -> 0.31 ms). Amoebius milliseconds are .NET, not Mono: **Unity confirmed it the same day** (Yash's Profiler, lava lamp on `bleeding-edge`, before the fix): a 36.5 ms frame with `CrystalChargeArcs.LateUpdate` at **21.1 ms self, 57.9 %, 27 calls, 2.6 KB GC** per frame, and the chart's 1.4 s plateaus above 33 ms are the flux beat (charge + strike + hold + fade) with the 0.9 s gaps between them back under 16 ms. So the Unity cost is ~7x the Amoebius one (Mono, 27 crystals in view) and the rest of that frame is ~15 ms (~65 fps). The 2.6 KB also confirms the 684 KB/frame Amoebius reading is the engine's Mesh.Set*(List) copy (`amoebius_known.json`), not the game. The after-measurement is a Release `capture S2` (T-20261015-7edd). | Amoebius A/B per script; `unity_refcompile player-dev` OK; ported tests 11/11; generator `--check` OK |
| 10-15 | **Re-checked on the merged tree** (Yash: "go through bleeding-edge and recheck the performance test"): `bleeding-edge` `a2dbc6787` (#1098 carries the lava-lamp fix) and `Ys-bleeding-edge` both fast-forwarded into the performance branch; `perf_hunt.py sweep --write --tasks`: gates all OK, 78 scan findings (unchanged), 0 capture regressions, still no valid baseline (no Release capture has been sent); `perf_hunt.py amoebius`: Skim Race I2 **REGRESSION** against the 10-09 baseline - `VesselImpactor.AcceptImpactee` 3.83 -> 5.58 KB/frame (+46 %), `PoolRefill.FastGrowPrism` 0.53 -> 0.70, a new `PoolRefill.PrismImplosion` 0.12; watch `SkimRace.Driver.TrackMpc` +31 % ms (.NET time). The tree between the two runs is the Regatta Squirrel AI (it now skims its own rail: more hull contacts, more consumed prisms, more refills), so the pool rows are consumption, lever L2; the impactor row is attributed below. | `sweep`, `amoebius` on `a2dbc6787` |
| 10-15 | **Prism-on-entities audit** (§1.2): every `Prism` is instanced (config on, no exotic-visual caller, device probe the only gate); the GameObject renderers left in the lava lamp are the spindles (SRP-batchable already) and the snow - L1 and L6, unchanged. | read from the tree |
| 10-15 | **The impactor allocation attributed.** The 14 `RunEffectIsolated(() => e.Execute(..), e)` lambdas (a closure per effect per contact) were removed first (`ReportEffectThrow`, same once-per-effect report) - measured NEUTRAL: 5.58 -> 5.58 KB/frame, so the closure was never the allocator. Nested markers then split `VesselImpactor.AcceptImpactee`: hull-prism contact, SFX, suppress check and every prism effect sum to < 0.1 KB/frame; **`SquirrelVesselExplosionByCrystalEffect` is 6.0 of the 6.2 KB/frame** - one un-pooled `Instantiate` of the AOE ring prefab plus its ring of prisms per omni collect, at 0.01 collects/frame. Lever **L13**, task T-20261015-fafa. The regression against the 10-09 baseline stands (not adopted: a worse number is never the new normal); it is the Regatta Squirrel collecting more crystals, and the pool is the fix. | Amoebius markers run x3 (`amoebius_markers_run.py --no-build`), per-effect nested markers (temporary, not committed) |
| 10-15 | **Style-B UI: the ambient layer sleeps while hidden** (`claude/new-session-76hnyc`, `Docs/MENU_REDESIGN_B.md` §24). The menu redesign shipped ~70 endless `KitAmbient` / `KitSpark` loops in Menu_Main (Ark 10, the three configure views 16, ~3 per quest card, the 12 intensity nodes), almost all under closed modals (active at CanvasGroup alpha 0) or on screens the strip had slid outside its RectMask2D, plus two on GameCanvas that ran for the whole match; and `SpectatorOverlay` re-wrote its group and bar position every frame while hidden. Each write moves a RectTransform or a CanvasGroup on a canvas of thousands of graphics, which re-batches that canvas every frame. Fix: `KitSightline` (product of the parent CanvasGroups, cached; a non-self-masked renderer's RectMask2D cull) - a loop does nothing while hidden; a Breathe modulates its owner's alpha (a hidden part stays hidden); the spectator overlay stops writing once settled. **Not measured in Unity** (no editor in the session; Amoebius cannot model uGUI's native re-batch): reasoning from the structure only, so the before/after is owed to the next editor capture. The bigger share of the reported menu drop is the `CrystalChargeArcs` regression above, merged into the branch the same day. | `unity_refcompile` player + editor; `KitMotionTests` +6 |
| 10-15 | **Re-run on `Ys-bleeding-edge` `a4651c844`** (Yash: "benchmarks again on Ys-bleeding-edge"). No new Unity capture on any branch (`bugledger/yskha` still holds only the 10-08 Skim Race diag + prof), so still no valid editor baseline. Sweep: gates OK. Amoebius Skim Race I2: the same three allocation regressions, unchanged to the byte (impactor 5.57 KB/frame = the un-pooled AOE ring, L13 / T-20261015-fafa; the auto-filed duplicate T-20261015-d7f8 closed onto it; the two pool-refill rows stay as tasks T-20261015-c8e2 / -c7db, consumption by the Regatta Squirrel, lever L2). Lava lamp in Amoebius: **1.95 / 1.86 ms per frame** (two runs), `CrystalChargeArcs.LateUpdate` 0.005 ms (asleep, as fixed). Every marker, untouched ones included (`ShellContact.Query` 0.10 -> 0.047 ms), read about half its earlier value, so the drop from 3.45 ms is mostly the machine being less loaded than earlier: timings compare only within one sitting; allocations compare across sittings. | `sweep --write --tasks`, `amoebius`, menu run x2 |
| 10-16 | **Generic pass over the latest data** (Yash: "re-diagnose the latest performance datas for any generic obvious optimizations"). Inputs: the Amoebius lava-lamp run with a per-script timer and, new, per-coroutine allocation accounting (temporary, not committed); the Skim Race marker run; the per-frame allocation scan; Yash's Unity Profiler shot. (1) **The scan's Update list had 6 findings; 3 were false** (expression-bodied `LateUpdate() => ...` read the next method's body: the Butterfly / Scarab / Stoat hull builders' one-time `HideLegacyModel`). The scanner now reads `=>` bodies and honours a `// perframe-ok: <reason>` mark (used on the throttled warp-field refresh and the PhotoBooth tool). The one real one, **`GunTransformer.Update`** (Falcon, Shrike), called `GetComponentsInChildren<Transform>()` every frame; it now fills one reused list (same children, no array). Update list: 6 -> 0. (2) **`WaitCache`** (new, `Utility/`): shared `WaitForSeconds` for coroutine timers - `Seconds` (exact key, timing unchanged) at 21 sites (AI pilot, assemblers, Fauna and Worm behaviour ticks, trail builder, resource / charge-boost / full-auto / drift / charged-gun ticks) and `About` (rounded to 10 ms) at 6 randomised cadences (Boid, LightFauna, the three life spawners). Loop findings 63 -> 36 (the rest are UI / dialogue / one-shot). **Measured neutral in the lava lamp** (coroutine phase 66.2 -> 66.2 KB/frame: only a handful of creatures tick per frame there); it scales with creature count (S5), so it is good practice, not a measured win. (3) Where the lava lamp's coroutine bytes really go: `Prism.CreateBlockCoroutine` ~3 KB per completion (in `FinishCreation`: SOAP raise ~0.43 KB per raise, spatial bind ~0.31 KB, the rest unmarked) and creature spawning while it runs; `LOD.Sweep`'s 39 KB per sweep is the engine's NativeList stand-in (the game's path is native-only). Unity's own number for the whole lava-lamp frame was 5.1 KB GC, half of it the Charge arcs now asleep, so nothing else large is left there. Remaining items stay where they are: L13 (AOE pool, Garrett), L2 (pool refills), L1 (spindles, Garrett). | Amoebius A/B x2 (`wc_before` / `wc_after`), per-coroutine bytes, scan, `unity_refcompile player-dev` OK |
| 10-16 | **The first real shared Unity capture, read** - `bugledger/mrglo` (committed by jtgjones), Grizzly Charge, RTX 3070, Release, Burst ON in the probe job: CPU busy **6.5 ms**, GPU 6.5 ms, capped at 60 by vsync, 10.8k prism entities instanced, 2,773 draws. Three findings, fixed: (1) **two PipCameras (640x320) enabled** - the retired picture-in-picture camera rendering the world again into a texture nothing shows, while the render thread was 80 % busy (15 ms). Every vessel prefab overrides the PipCamera inactive except the **Grizzly**, whose `Pip` also had `pipCamera` unwired, so its default-off was a no-op for every Grizzly (player and AI). `Pip.Awake` now finds its own `PipCamera` child when the reference is empty. (2) **`VesselAnimation.Update` 0.39 KB every frame** = `BufoAnimation` (the Grizzly) building `new List<Transform>{...}` per frame; `DolphinAnimation` had the same; both are explicit calls now (same parts, same order). (3) **`TurnMonitorController.Update`** ran LINQ `Any` every frame of every mode (0.04 KB/frame): a plain loop, same short-circuit. The reader now names an allocation's owner instead of printing `GC.Alloc` (`perf_capture_report.py`). Not fixed, recorded: diag's 84 KB/frame GC is editor-side (the Profiler's own player-loop rows sum to ~3 KB/frame); 4,189 enabled snow renderers here too (lever L6); the later diag's `LateBehaviourUpdate` +0.2 ms is at the threshold floor with 3 cameras on, not acted on. | the capture JSON via `perf_capture_report.py`; `unity_refcompile` player-dev OK, editor 0 errors in changed files; an Amoebius Skim Race match end to end |
| 10-17 | **Garrett's four items, done by Yash's session** (Yash: "Garrett is busy. We do the explosion pool, crystal sleep size and the spindles and also snow shards"). (1) **Explosion pool (L13):** `AOEExplosionPool`, opt-in per class (`AOEExplosion.Poolable`; every self-retire now calls `Retire()`, which parks a pooled blast and destroys any other; the authored duration is restored per detonation). On for the Squirrel's ring spawner (`AOEBlockSpawner`), whose ring container is now made once (`SpawnableRings`). Measured: 1 miss, 7 reuses over 8 collects, and the Instantiate left is 0.012 KB/frame, so **the shell was never the allocator**. Temporary nested markers (reverted) put 5.22 of `AcceptImpactee`'s 5.53 KB/frame in `CrystalMorphMeshBuilder.TryBuild`, the Squirrel's crystal-to-ring morph (~1.3 MB per collect). It now caches each source mesh's unshared arrays (`EmitCache`) and reuses its target scratch: **`AcceptImpactee` 5.53 -> 3.41 KB/frame**, under the 3.83 baseline, so that regression is cleared. The 3.1 KB/frame left is Amoebius's `Mesh.Set*` copying arrays (Unity copies to native). (2) **Crystal sleep size (T-20261015-a5a5):** kept at 24 px radius (a bolt core is ~0.3 px there); new `halfRateBelowScreenRadiusPixels` 64 (profile, asset, generator): between 24 and 64 px the arcs rebuild on alternate frames, staggered per crystal. (3) **Snow (L6):** density kept, drawn instanced (`SnowChanger`, `shard.prefab` only): ~4.2k GameObjects and renderers gone; submit 0.001 ms/frame in Amoebius. (4) **Spindles (L1):** a sub-pixel render LOD was built, measured and removed: 0 hidden of 2,898 spindles in an 8-minute Lattice (selected through the Cell Selector; the lamp boots Garland), 0 of 3,287 in Garland. The ~40k spindle count behind L1 is stale; a Unity S2 census decides whether L1 is still worth building. | Amoebius Skim Race x4 (`pool1`, `trace1`, `trace2`, `perf_hunt amoebius`), lava lamp x5 incl. an 8-minute Lattice; `unity_refcompile` player-dev OK, editor 0 errors in changed files; gates OK |
| 10-17 | **The two pool-refill "regressions" (Yash: "now fix the pool refill regressions too").** Method: re-run the BASELINE commit (`574e0d39a`, a worktree, same harness) beside the current tree. It measures `PoolRefill.FastGrowPrism` **0.766**, `PoolRefill.PrismImplosion` **0.157** and `AcceptImpactee` 5.60 KB/frame today, so the 10-15 baseline (0.526 / none / 3.83) does not reproduce at its own commit: neither row was a code regression. (1) **`PrismImplosion` was real waste, fixed:** `PrismManagers.prefab` topped the implosion pool up to 160 objects after boot, though death implosions are batched entity debris since D4 and only the Sparrow's ReverseSuction `Get()`s it (its class doc already said "sized for Grow bursts", prewarm 12). `bufferSizeTarget` 160 -> 24: **0.157 -> 0.000 KB/frame**. Same prefab: the explosion pool, which gameplay never `Get()`s (its doc: "Do not prewarm"), prewarmed **1,500** objects synchronously at boot; now 0, maintenance off. (2) **`FastGrowPrism` is ring mass, not a regression:** its refill tracks boost-prism activations one for one at ~20 KB per prism in .NET (24 KB at the baseline commit), i.e. each prism a boost ring lays stays in the world as mass and the pool mints its replacement; KB/frame moves with how many rings the AIs lay. The Skim Race Amoebius baseline was re-adopted from a run that repeats to 0.3% (`poolfix2`: 0.693; `AcceptImpactee` 3.41). Note: one run on the same build played differently (fewer crystal hits, 2.91 KB/frame), so a marker that scales with events can move without any code change. (3) **A false S2 baseline, caught:** the sweep's first pass adopted mrglo's 10-16 Menu_Main capture as S2, but its cells line says Garland (the auto-capture labels any long Menu_Main play S2). `perf_hunt.py` now keys a Menu_Main capture by the cell it measured unless that is the scenario's (`SCENARIO_CELLS`, self-tested): it is `Menu_Main|Garland`, and S2 still waits for a Lattice capture. Its census: 7,098 renderers on, **4,189 of them snow** (gone with L6) and ~2.7k spindles, matching Amoebius. | Amoebius Skim Race: `base_skim` (baseline commit), `trace2`, `poolfix`, `poolfix2`; `perf_hunt.py --self-test`; `sweep --write --tasks` |
| 10-17 | **The Stoat slowdown after 8-10 minutes (Yash: "frames dropped to maybe 30, 40").** Yash's capture (`bugledger/yskha`, Menu_Main Garland, Stoat, Release, RTX 4070 Ti): 48.8 fps, p95 67.5 ms, 47 spikes in 15 s, the Profiler's spike frame **88 ms of self time in `::Driver.LateUpdate`**, which ten nested classes share. Two taints: the Third Eye camera was on, and vsync capped at 120. mrglo's Garland capture without a Stoat has no such row. (1) Each `Driver.LateUpdate` now has its own marker (`BlackHoleRegistry.LateUpdate`, `WormholeView`, `WarpFieldVesselScaler`, `PrismLit.Flush`, `PrismCradle.Flush`, `VesselSpeedTunnel`, `VesselPlacementView`, `VesselRearView`), and the black-hole tick has nested ones (`BlackHole.Pairs / GravityField(.Admit/.Prune/.Verdicts) / VesselPull / Waves / Warp / LensCameras`), all in `diag`'s defaults. (2) Amoebius Warpline (two Stoats): hole and pair counts stay flat (0-6 holes, at most one pair), but the tick's cost grows. Temporary markers named the cause: **`FindObjectsByType<VesselStatus>`** walks every loaded object, and the session keeps adding objects. It cost 1.8 ms a call in the first minute and **20 ms (worst 36 ms in the pull, 61 ms in the Stoat's ripple) after 30,000 frames**. The black-hole pull (1 s), the Stoat's ripple (each annihilation), the Stoat's rival scan and the warp-field scaler (0.25 s) all paid it on a timer. (3) **Fixed:** `VesselStatus.Live`, a registry kept in OnEnable/OnDisable. All four callers read it. | Same late window: `BlackHoleRegistry.LateUpdate` max **36.0 -> 6.0 ms**, 14.0 -> **0.58 KB/frame**; `BlackHole.VesselPull` max **35.9 -> 0.10 ms**; ripple search gone. What's left: `GravityField.Admit` grows with the trail mass near the holes (0.02 -> 1.5 ms a call at 10 Hz, worst 5.9 ms). Needs a Unity re-capture with the Third Eye closed |
| 10-20 | **Spindle LOD (L1), Garrett: "the main driver is performance... smooth is the requirement... improving their look from afar is the nice-to-have".** Measured the mesh first: every flora branch draws ONE braid (`bonita.fbx` BezierCurve.001: three triangular strands round a straight centreline, 432 tris, girth 1.40); a strand is one pixel at ~280 units and SpindleGraph alpha-clips the braid away by ~1,400 while paying every vertex. So the metric is THICKNESS (girths), not the bounding sphere the 10-17 attempt used. Built `SpindleLodSystem` (Burst pass over a read-only TransformAccessArray, hysteresis bands placed where both tiers draw the same picture, budgeted swaps, still far limbs instanced), two graphs generated from SpindleGraph (which is untouched) and `SpindleLod.hlsl` (strands converge, lace closes, width held at 1.25 px with coverage alpha, far fade). Verified: Unity reference compile player + editor, Amoebius build, clang harness on the shipped HLSL (6 mutants caught), generator self-tests; Amoebius Garland 1,800 frames: 865 of 910 limbs instanced, counters exact. Not yet: the Unity import of the graphs, the look, the frame-time A/B (`Docs/SPINDLE_LOD.md` §5) | reference compile x3, Amoebius smoke x3, `verify_spindle_lod.py` |
| 10-20 | **Spindle LOD round 2 (Garrett's play test + "lean in").** (1) Play bug: near and far tiers shown at once around the vessel in the Lattice - the system tiered by `Camera.main`, Bootstrap's static menu camera in flight; now `CameraManager.ViewCamera`. (2) OVERT style (default): tiers in LOCKSTEP with the prisms' squared-distance depth ramp (near → mid where the ramp first opens, mid → far at its over-distance), the braid's strands fan open root-anchored as far as danger prisms' faces travel (15.2 u) with a travelling wave; built as the shared `IncorporealDepth.hlsl` the shards, switches, membrane and nucleus can adopt. Far tier = the 144-tri mid braid (instanced); culling bounds grow by the opening. Amoebius Garland: 886 of 914 limbs instanced. Previewed offline from the shipped shader code (`render_spindle_lod_preview.py`) | reference compile x2, Amoebius smoke, `verify_spindle_lod.py` (12 mutants) |
| 10-20 | **Spindle LOD round 3 (Garrett: "70% for charge, 20% for everything else ... the onset is very abrupt ... the lowest performance cost that still signals its minimal presence").** Per-element strength on the LOD materials (Charge 0.7, others 0.2). The far side now RECEDES in the shared `IncorporealDepth.hlsl` (smooth in distance from norm 2.25 to 16: 1.7% of the effect per 1% of distance against the near ramp's 3.2%) while the strands converge, and the geometry steps down with it: braid 144 → braid 72 → a 12-triangle thread (was 24), all instanced while still, culled past ~2,640 u. Amoebius Garland: 910 limbs, 890 instanced (24 braid / 799 low / 87 thread) | reference compile, Amoebius smoke, `verify_spindle_lod.py` (16 mutants), previews at 0.7 and 0.2 |
| 10-21 | **Spindle LOD round 4 (Garrett: "grade them in over twice the distance and cut both the charge and normal max amplitude values in half").** Strength Charge 0.35 / others 0.1 (a tip travels ~5.3 / ~1.5 u at the peak). The recede's span doubles in distance (`recedeEndNorm` 16 → 42.25: ~474 → ~2,056 u; onset 1.3% of the effect per 1% of distance, was 1.7%), the thread fades 46 → 100 norm (~2,145 → ~3,160 u), culled past ~3,300 u. Cost: the 72-tri braid now covers out to ~2,140 u where the 12-tri thread took over at ~1,320 - Amoebius Garland: 910 limbs, 889 instanced, all far ones in the low braid (24 braid / 886 low / 0 thread; round 3 had 87 threads). Instanced, so the difference is vertex work only; the frame-time A/B is still owed (`Docs/SPINDLE_LOD.md` §5) | reference compile x2, Amoebius smoke, `verify_spindle_lod.py` (16 mutants), previews at 0.35 and 0.1 |
| 10-21 | **Spindle depth beyond the braid (Garrett: "apply this to all spindles including fauna like sharks ... replace the switch rings with a spindle material that has this effect").** Creatures' own spindle-material models (shark, brittlestar, worm, quadfish) wear `SpindleDepthGraph` (bloom by position alone: no tearing at seams, capped so the wave never folds it); switch rings draw on it in the facets form. Cost: no draw added - each body and ring keeps its one renderer; the depth vertex stage adds a few ALU ops per vertex (a sin per vertex, and for bodies a 4-step Newton inverse with sin/cos per fragment for the pulse). No LOD meshes or batching for bodies (few, and they move). Amoebius Garland: 49 bodies registered beside the 910 limbs (889 instanced, unchanged), same 38 distinct log messages as before. Not measured in Unity | reference compile x2, Amoebius smoke, `verify_spindle_lod.py` (24 mutants), previews of every creature and the ring |
| 10-21 | **Spindle LOD round 5 (Garrett: "settling back from 474 to circa 700 but still use the 144, then 72 till circa 1000, then 12 till 1500 and gone after that ... cut the opening at peak in half ... apply to all spindles").** Recede 474 -> 700 on the 144-tri braid (`recedeEndNorm` 4.9); 72-tri braid to ~1,000 (`overtThreadStartNorm` 10); 12-tri thread to ~1,500, fading 1,250 -> 1,500 and culled at ~1,564 (was ~3,300). Strength Charge 0.175 / others 0.05 / rings 0.05. Creature bodies fade out on the thread's band. Cost: the cull distance more than halves, so the instanced thread population past 1,564 u is gone; the 144-tri braid now runs to ~730 u (was 474). Amoebius Garland, last of six samples: 910 limbs - braid 168 / low 298 / thread 444, 889 instanced, 49 bodies, none culled from the menu camera (round 4: braid 24 / low 886 / thread 0), same 38 distinct log messages. Not measured in Unity | reference compile x2, Amoebius smoke, `verify_spindle_lod.py` (26 mutants), previews at 0.175 / 0.05, shark and ring |
| 10-21 | **Switch rings as a synthetic wire (Garrett: "a synthetic spindle geometry that is more like a wire ... synthetic constructs up close, but spindles from afar").** The ring is now a machined three-strand cable (5,184 tris near, 1,728 settled, 576 thread; first cut 7,776, trimmed to a 24-turn lay) on `SpindleWireGraph`, tiered by `SpindleLodSystem` (a synthetic mesh set, never culled). Cost: was one 144-tri torus per ring; now 5,184 within ~730 u of a ring and 576 beyond ~1,000 (rings spin, so they never batch). A course of 40 gates all within 730 u would be ~207k tris - watch a race capture. Not measured | reference compile x2, Amoebius build/smoke, `verify_spindle_lod.py` (30 mutants), generator `--self-test` (8), wire preview |
| 10-24 | **The Menu_Main slowdown since 10-19 (Yash: "find the main menu slowdown").** Every Menu_Main capture since 10-19 (Caleb x6, Andrew x4; RTX 3070-class editors, Release) is slower than the 10-16 Garland baseline: CPU busy 3.8 -> 9-30 ms. The Profiler tree names one row: **`ClipperRegistry.Cull` 3.1-7.7 ms/frame (was 0.26)**, under `Canvas.SendWillRenderCanvases` (8.2 ms in Andrew's 10-23 capture). It started with Shombith's style-B menu (#1153, merged 10-19): **RectMask2D components across scenes and prefabs went 18 -> 59** in that window (QuestCard 0 -> 3, Settings 11, Ark 5...). Menu screens are never deactivated (closed modals sit at alpha 0, freestyle hides the whole screens group by alpha), so every mask stays registered, and any graphic that MOVES under one is re-clipped every frame. `Docs/MENU_REDESIGN_B.md` §24 (10-15) already made the kit's own loops sleep while hidden (`KitSightline`); its Unity before/after was owed, and these captures are that after. One unguarded mover found: **`QuestTrackView.UpdateParallax` rescaled every quest card every frame**, visible or not, and a transform assignment counts as a move even at the same value, so each card's three masks re-clipped every frame on the hidden Profile screen. Fixed: the parallax sleeps while `KitSightline` says the track is hidden, and writes a card only when its value changes. Also seen, separate: `SpindleLod.Drain` (Garrett's spindle LOD, 10-20) up to 14 ms in a spike frame. | Not measured in Unity (no editor in the session; Amoebius does not model uGUI clipping). The after-capture is owed: Menu_Main in Release past the 4-minute auto-capture, judged on `ClipperRegistry.Cull` |
| 10-26 | **The Menu_Main slowdown, round 2: masks that sleep while nothing under them can be seen (Shombith: "when the game loads into the main menu the slider starts to lag").** The 10-25/26 captures (Yash x8, after the 10-24 QuestTrackView fix) still show `ClipperRegistry.Cull` 3.4-4.0 ms/frame (0.26-0.31 on 10-16/18), the largest self-time row in the menu; with `BehaviourUpdate` ~0.9 ms it is most of why the screen slide stutters. The 10-24 reading ("a graphic that MOVES under a mask is re-clipped") was half of it: every enabled RectMask2D runs its pass every frame and calls `Cull` on EVERY MaskableGraphic under it whether or not anything moved. Menu_Main has ~1,500 graphics under masks that nobody can see at rest: the closed Arcade window's grid alone is 545 (plus one art-band mask per card), Arena 70, Mission 29, Toy Box 22, and every off-screen hub screen sat under the strip's single runtime mask (~770). Fixed: **`KitRectMask2D`** (`Assets/_Scripts/UI/Kit/`), a RectMask2D that skips its pass while a CanvasGroup above it has the effective alpha at zero or its rect lies wholly off the root canvas, after one full pass that culls everything under it. The menu generators author it for every mask (`rect_mask()` in `menu_b_motion.py` / `author_menu_arcade.py`, the Hangar's two viewports): 47 masks in 24 prefabs after the 2026-10-26 bleeding-edge merge (re-count: `git grep -c` the `KitRectMask2D` guid), a guid swap and nothing else. `ScreenSwitcher` gives each strip screen its own kit mask spanning its slot (negative padding = the safe-area insets, so bleeds still draw), so a screen slid off the strip sleeps too. Expected: `ClipperRegistry.Cull` back near 0.3 ms at rest on Home. Amoebius gained the API it lacked (`RectMask2D : UIBehaviour` with `PerformClipping` and `rectTransform`, `Rect.Overlaps(rect, allowInverse)`, unary `Vector4` minus). | Not measured in Unity (no editor in the session; Amoebius does not model uGUI clipping). The before is the 10-25/26 captures; the after is owed: Menu_Main in Release past the 4-minute auto-capture, judged on `ClipperRegistry.Cull` and `UGUI.Rendering.UpdateBatches` |
| 10-24 | **Bloomrush at 3.5 fps (Andrew's two 10-20 captures, RTX 4050 laptop, Release): one prism coroutine per waiting prism.** CPU busy 130-221 ms; the Profiler tree: `Prism.CreateBlockCoroutine` resumed **56,014 / 57,636 times a frame**, `CoroutinesDelayedCalls` 139 ms self (Unity's scheduler waking them) + 27 ms inside them. Every laid prism waited for its creation completion as its own coroutine (`yield return null` until the 6-per-frame budget had room), and Bloomrush's arena - Rampage's cactus forest, 480 plants, ~56k prisms waiting at the 90 s capture in both matches - is grown during play, not under the load gate, so tens of thousands wait at once. **Fixed (T-20261021-a2b3, handed over by Garrett):** a prism that finds no budget joins one FIFO queue per budget class, drained by one driver in Update (`Prism.CreationQueue`, marker `Prism.CreationQueue.Drain`); budgets, order, `CompleteCreationImmediately` and the destroyed / inactive / reused-life cases are unchanged (an entry from a dead life is dropped without spending budget). Not changed, Garrett's call: at 6 a frame the forest still takes ~2.6 min to finish appearing. | Not measured in Unity (no editor). Amoebius cannot build the game today (9 engine gaps, T-20261024-ae82), so no Amoebius A/B either. Expected: the 166 ms/frame of coroutine work goes to ~0 (one drain of <= 12 completions) |
| 10-24 | **Garrett's two remaining Cell Selector swap tasks (handed over 10-21).** (1) **T-20261021-a743:** `Cell.ReleaseRetiredWorld` released the suctioned world at a fixed 500 prisms a frame - 1.1-1.4 s for 4,259 Garland prisms in both Orrery captures, because the frames were slow, not the work. Now a 12 ms time slice per frame for pool releases, keeping the 500 cap only for deferred `Destroy`s (their cost lands at the end of the frame, where a clock cannot see it). (2) **T-20261021-e916:** the "no companion render entity to stamp (cellSwapSuction)" prisms are ones whose creation had not completed yet (a render entity is made only when a prism first shows), i.e. invisible; snapping them was no pop, but one could COMPLETE during the suction and pop in at full size inside the shrinking world. `StampSuctionToward` now cancels an incomplete prism's pending creation (`Prism.CancelPendingCreation`, counted as Load Time Insights "Suction: prisms not created yet") and keeps the warning for a visible prism with no entity, composing its detail line only for the one warning that is logged (`PrismClockDiagnostics.WillWarnNoRenderEntity`). | Not measured in Unity (no editor). The next Orrery swap capture with Load Time Insights armed shows the release span and both suction counters |
| 10-25 | **`SpindleLod.Drain` spikes (Garrett's spindle LOD, #1369; seen in the 10-24 menu row).** Andrew's 10-23 Menu_Main capture (RTX 3070-class editor, Release): Drain runs in 16 % of frames at ~3.4 ms a call, worst **14.7 ms** (`maxTotalMs`); the swaps are budgeted by COUNT only (`maxTransitionsPerFrame` 384), and each is several renderer writes (mesh, material, bounds, enabled), ~38 µs in that editor. Now also by TIME: `SpindleLodProfileSO.maxTransitionMsPerFrame` (3 ms; the clock is read every 16 swaps); the remainder drains next frame, which the profile already documents as invisible (both sides of a swap draw the same picture over the hysteresis band). Total work is unchanged; the spike is spread. | Not measured in Unity (no editor). Expected: `SpindleLod.Drain` max per frame <= ~3 ms in the next Menu_Main capture, average unchanged; SpindleLodTests 17/17 on the engine runner |
| 10-25 | **Spindle LOD in Wrecking Ball: `SpindleLod.Drain` 11.4 + `SpindleLod.Draw` 9.3 ms/frame** (Andrew's 10-22 capture, RTX 4050 laptop, Release; CPU busy 31.6 ms; 20,306 limbs, 18,893 of the far ones instanced). The drain was busy almost every frame (0.9 calls/frame), and every limb that joined or left an instanced batch marked the WHOLE batch dirty, so `Rebuild` re-wrote every matrix and box of the batch - thousands of limbs, managed - on every frame the drain worked. Now incremental: a join writes its own slot and grows the box, a leave swaps the last matrix into its slot and keeps the box (it only over-covers: a little culling, never a missing limb), and a full `Rebuild` runs only for a new batch or once half the batch has left (`FarBatch.leftSinceRebuild`). With the drain's 3 ms time cap (row above), the two together should fall from ~20.7 to ~3-4 ms/frame there. Not understood yet: why Wrecking Ball's drain never empties (a tick's pending list is refilled every 0.2 s) - worth a look by the spindle LOD's owner if the after-capture still shows the drain at its cap every frame. | Not measured in Unity (no editor). refcompile player-dev OK; SpindleLodTests 17/17 on the engine runner (none covers the batch bookkeeping). The next Wrecking Ball capture shows both markers |
| 10-25 | **`CrystalChargeArcs.LateUpdate` (the Charge / omni crystal light): 0.83 ms/frame, 2.4 KB/frame and ~33 `GC.Alloc` a frame in Wrecking Ball** (30 crystals; Broadside 0.82 ms, 14 crystals; Andrew's 10-22/23 captures, Release), worst frame 21 ms. Two costs, both removed with identical output: (1) `ChargeSeamWalker.Advance` built a `new[] { SiteA, SiteB }` plus three capturing lambdas (`TrueForAll` / `Exists` / `RemoveAll`) for every seam struck, on every Charge crystal, seen or not - now plain loops (`AvoidCircling`), same options in the same order, so the same random pick; (2) the seam walk's petal glow ran `sqrt` + `Exp` for every petal x lit point x back point - a pair past d^2 = ln 100 adds under 0.01 and can neither light the petal nor raise its glow, so it is skipped before both. Both rewrites matched the originals on 200,000 random cases each (a scratch Python model). | Not measured in Unity (no editor). refcompile player-dev OK; `CrystalChargeArcsTests` cannot run on the engine runner (it uses `AssetDatabase.LoadAllAssetsAtPath`). Expected: the marker's `GC.Alloc` child gone and its ms down in the next Wrecking Ball / Broadside capture |
| 10-25 | **`KiteNavigator.Update` (Garrett's Wrasse kite): 1.67 ms/frame SELF in Wrasse Run** (Andrew's 10-22 capture, two kites, x2 calls) - the largest unexplained per-frame cost left in the 10-22/23 captures, with no marker inside to split it. Not fixed blind: its stages now carry markers (`KiteNavigator.Board`, `.Motes`, `.ShadowPrisms`, `.ShadowRivals`, `.ShadowCreatures`, `.Steer`, `.Fly`, `.Effects`, and `.Draw` in LateUpdate). Suspect, unmeasured: `ShadowPrisms` runs a board-wide `PrismSpatialIndex.QuerySphere` per kite per frame and walks every result. Also measured and left: `Fauna.BodySync` (~14 us per creature, 0.3-0.8 ms/frame across modes) - its per-prism work is a few transform reads and the index/render contract, so a trim saves ~0.1-0.3 ms for a change to the locked prism contract. | Markers only, no behaviour change. refcompile player-dev OK; KiteNavigator Core/Sling/Juice tests pass on the engine runner; KiteNavigatorConfigTests 6/8 with and without this change (2 Wrasse-prefab checks fail on the runner either way). The next Wrasse Run capture names the stage |
| 10-26 | **`BlackHole.Field` (the Stoat dipole's gravity field, T-20261017-9990's phase): 2.47 ms/frame average, 5 ms worst, all of it Field SELF time** (Yash's Menu_Main capture 2026-10-26 02:27 IST, Release, RTX 4070 Ti; 1.03 ms in the 02:23 capture, 0.82 ms in Regatta 10-25 22:48). Its child markers (Admit, Prune, Verdicts) are near zero, so the time sits in the unmarked rest of `BlackHoleGravityField.Tick`: the job wait, the main-thread box-contact pass (up to 256 bodies, each sweeping the index and reading every neighbour's transform-backed box), the batch index write and `ApplyContacts`. Not fixed blind: those four stages now carry markers (`BlackHole.GravityField.Jobs` / `.BoxContacts` / `.IndexWrite` / `.Contacts`), in the diag's default list. An Amoebius Warpline run (I2, 3 seats, 1,800 frames) does not reproduce it (Field 0.009 ms: the AI Stoats put few bodies into the field). Suspect, unmeasured: `BoxContacts` (bodies packed in a hole make it bodies x neighbours). | Markers only, no behaviour change. refcompile player-dev OK; MarkerBudgetTests 22/22 on the engine runner. The next Menu_Main capture with a dipole names the stage |
| 10-26 | **The Squirrel prism pool runs dry: `PoolMiss.Squirrel Prism`**, a synchronous `Instantiate` in the frame that lays the prism. Unity: Thresher Time 2.2 misses/frame, **1.71 ms/frame** (Andrew, 10-22); Regatta 1.7/frame, 0.38 ms (Yash, 10-26). Amoebius Skim Race I2 had none at the 10-17 baseline and 5.6-7.5 KB/frame after: bisected over Ys-bleeding-edge's first-parent history to `142ff7da00` (M5, class names match their files), confirmed 3/3 runs each side - it made the element stats handler and 11 other components LOAD in Amoebius (it binds by file name; Unity always loaded them), so the engine began laying prisms at Unity's rate. Not a game regression: the old Amoebius baseline was measured without them. The cause in both engines is the pool's refill ceiling: `PrismManagers.prefab`'s Squirrel pool refilled at most 40 prisms/s into a 120-prism buffer while trails lay faster. Now `maxInstantiateRate` 160, `bufferSizeTarget` 240, `maxAddsPerFrame` 8 (a 30 fps frame still keeps up). Prisms laid as trail are not returned, so the instantiation count is gameplay's; this moves it from the lay frame to the buffered async refill. | Amoebius Skim Race I2 x1 A/B: `PoolMiss.Squirrel Prism` 7.54 -> **0** KB/frame, `PoolRefill` 9.72 -> 15.92, pool total 17.25 -> 15.92 KB/frame. Unity re-capture owed (Thresher Time, Regatta) |
| 10-26 | **`StoatPathfinderExecutor.Update` (the Stoat's path line): 1.06-1.80 ms/frame self in Yash's Regatta captures** (10-25/26, Release, RTX 4070 Ti), unsplit. Split with markers (`StoatPathfinder.Predict` / `.Dots` / `.Foresight`) and measured in Amoebius Warpline (I2, 3 seats): `Predict` (every flying Stoat, AI included: the path decides the warp boost) ~0.26 ms per Stoat; `Dots` 138 KB/frame, the engine's ranged `Mesh.Set*` copy (`amoebius_known.json`, not the game's). `StoatDipoleMath.PredictPath`'s loop check compared each new point with EVERY earlier one (~80,000 distance checks for a 400-point path); now it skips 16-point blocks whose bounds lie beyond the loop margin, walking blocks in order so the first hit is the same point. Identical output: 6,000 random paths (field, jumps, portal open/closed, 200+ loops) matched the original point for point (a scratch equivalence test, not committed); StoatDipoleTests 23/23. The rest of `Predict` is the field integration (up to 96 substeps per 3 u step near a sink), which sets the path's accuracy and is unchanged. | Amoebius Warpline x2 each: `Predict` 0.72 / 0.65 -> 0.57 / 0.56 ms/frame (-15..21 %). Unity re-capture owed (Regatta with a Stoat) |
| 10-26 | **Re-ranked on Yash's four newest captures** (Menu_Main x2, Scarab Scramble, Breakwater; Release, RTX 4070 Ti): CPU busy 12.3 / 12.3 / 6.4 / 7.7 ms, nothing new above 0.6 ms apart from the items already open (menu UI clipping, the black-hole field, the Wrasse kite - each waiting on a capture or its owner). Breakwater's rounds (~139 in flight) cost `Projectile.SweepPrisms` 0.57 + `.Growth` 0.34 + `.SweepVessels` 0.20 ms/frame, about 8 us per round per frame: the lever is batching the flight step, a rework of the shared projectile loop (Garrett's guns), not taken blind. A local reorder of the sweep was tried and measured dead (§3.5). | `perf_capture_report.py` x4; Amoebius Breakwater A/B x2 |
| 10-26 | **The half-second jerk in the editor (Yash: "I am getting frame drops and sudden jerks ... I should at least maintain 120"): the Third Eye's pilot scan.** Six of Yash's 10-26 captures (Menu_Main x4, Switchback x2; Release, RTX 4070 Ti, i7-8700K) freeze **85-130 ms every 0.51 s** (29-30 spike frames in 15 s, p99 100-147 ms) while CPU busy stays 13-16 ms; the Profiler puts the time in `EditorLoop` (max 90-134 ms; 3.7 ms in the clean sessions) and "Unaccounted time between Profiler.SessionMetadata and PlayerLoop" (85-92 ms), i.e. on the main thread but outside the game's frame. Five of the six list the `Third Eye Camera`; the sixth (21:37) lists one camera because the window was docked behind a tab (its camera turns off while hidden, its scan does not); none of the clean captures has it open. `ThirdEyePilots.Scan` ran every 0.5 s and called **`FindObjectsByType<SkimRacePilot>` and `FindObjectsByType<VesselStatus>`**, the walk over every loaded object the 10-17 row measured at 20-61 ms a call once a session has run a while (and fixed in the game with `VesselStatus.Live`). Fixed: the scan reads `VesselStatus.Live` and each hull's own `SkimRacePilot` (installed on the vessel's GameObject), so it costs a few vessels, not the scene. `perf_capture_report.py` now warns "editor stall" when `EditorLoop` exceeds 25 ms in a frame, because the camera list cannot see a hidden window. Separately, from the same captures: (1) `ClipperRegistry.Cull` 3.5 ms in the 21:37 Menu_Main capture is the BEFORE of the mask fix above (it reached `Ys-bleeding-edge` at 16:43 UTC, the capture was 16:07 UTC). (2) `BlackHole.Field` is now in Yash's menu (1.1-2.0 ms average since 02:23, `BlackHole.GravityField.BoxContacts` p95 2.5 ms, max 4 ms) whenever the menu hull makes holes; the box pass is Garrett's prism-motion ladder (exact boxes up to `BoxMaxMovers` 256 on the main thread), not changed here. (3) The Regatta 1.77 s / 30 MB frame (10-25 21:53) is the scene change after the race ("Invalid transition None -> GameOver"), not a race frame. (4) The Warpline 100 ms frame (10-25) is `ControllerButtonPress.Start` x2 on the end-of-match reveal, 96 ms SELF with no child marker (editor type load / JIT is the likely cause; not reproduced). | Not measured in Unity (no editor in the session). Expected: the next Menu_Main capture with the Third Eye open shows no 0.5 s spike train, `EditorLoop` max a few ms; reference compile (editor config) |
| 10-29 | **Destroyed-prism recycling: the idle lava lamp no longer grows without bound (Yash: "I want lavalamp to run infinitely like a screensaver ... without lagging or eating more memory"; recycling approved by Yash for dead prisms after their death effect).** A destroyed prism (`Prism.SetupDestruction`: collider off, render hidden, `MarkDestroyed`) stayed an ACTIVE GameObject in its trail forever - its GameObject, render-entity slot, spatial-index slot and `Trail.TrailList` entry all kept, so an AI lava lamp laying and shooting for an hour climbed in GameObject count, `PrismSpatialIndex` high-water mark and trail length until it hitched and ran out of memory. Now `PrismRecycler` (`Controller/Vessel/PrismRecycler.cs`, marker `PrismRecycler.Drain`) queues every death and, `PrismExplosion.DefaultDuration` (7.5 s) later, returns the corpse to its pool, at most `MaxPerFrame` (64) entries a frame: **vessel-wake prisms** (pooled, still members of a trail that opted in with `Trail.RecycleDestroyedMembers` - only `VesselPrismController`'s two trails do) leave the trail in one batched compaction per trail per frame (`Trail.RemoveMembers`, survivors re-indexed, an index-stamped `prismProperties.Index` restamped, `Trail.OnMembersRemoved` for `TrailFollower`) and go back through `ReturnToPool`; **natural-death skeletons** (`HealthPrism.LeaveAsSkeleton(..., recycleWhenDestroyed: true)` from `LifeForm`/`Fauna`, stamped `Prism.RecyclesWhenDestroyed`) go back through `EnvironmentPrismPool.TryRelease`. Every entry carries the death's `Prism.DestroyEpoch`, so a Restore, a reuse, a second death or an owner's own return drops it. **Not recycled** (an owner holds those corpses and owns their exit): Ark hull/wake, Hijack yards and every builder-laid environment trail, flora/fauna body prisms (`LifeForm.Die`), prisms a recorded front-remover already took (Wanderway, Arkway, RaceTrailCap, Ark wake), ability-plucked skeletons (Meerkat swallow, Jacks' net), every trail that did not opt in; and while any Urchin exists (`GunVesselTransformer` restores dead prisms it rides over) ALL vessel-wake recycling is held - entries wait, nothing is lost. This is not a cap or a TTL: only mass that is already destroyed leaves. Also: `Trail.LookAhead` gained a fill-your-own-list overload and `LookAheadShared` (the trail's reused buffer; Skimmer's boosters and `SkimmerAlignPrismEffectSO` use it, the nudge-shard markers keep their own list), an empty trail no longer throws in it, and the vessel lay's `TrailList.IndexOf` (O(n) per lay) is `GetBlockIndex`. | Not measured in Unity (no editor or compiler in the session; not even a reference compile). Owed: lava lamp idle 30+ min with the F7 console / Profiler - GameObject count, `PrismSpatialIndex` HighWaterMark, `EnvironmentPrismPool.IssuedCount`/`ParkedCount`, `PrismRecycler.PendingCount`/`RecycledCount` and frame time should plateau; Ark, Arkway, Wanderway, Skim Race/Joust (MobileLow cap) and an Urchin ride unchanged (`Docs/UNITY_VERIFICATION_CHECKLIST.md`) |
| 10-29 | **The Menu_Main slowdown, round 3 (#1485): the text under the sleeping masks.** Captures after the round-2 mask fix (10-26 23:47 IST on) have `ClipperRegistry.Cull` back at 0.08-0.56 ms (from 3.1-7.7 between 10-19 and 10-26; one 2.2 ms outlier on 10-27), but `Canvas.SendWillRenderCanvases` SELF still reads ~1.06-1.09 ms (0.54-0.65 total at the 10-16/18 baseline) and `UGUI.Rendering.UpdateBatches` 1.5-2.2 ms (0.6-0.9). Inference, not yet attributed by a marker: most of that self time is `TMP_UpdateManager.DoRebuilds`, which calls `InternalUpdate` (a `lossyScale` read and the env-map matrix) on every enabled TextMeshProUGUI each frame, and Menu_Main keeps ~1,300-2,500 of them enabled under screens and windows that are hidden by alpha or slid off the strip. Fixed: a **sleeping `KitRectMask2D` parks the text under it** (`isTextObjectScaleStatic = true`, which unregisters it from TMP's per-frame update, the same thing TMP does for a disabled Canvas) and gives it back when it wakes, is disabled or destroyed; text authored static is left alone, and an inner mask that is still asleep re-parks its own text when an outer one wakes. `DailyRewardCard`'s countdown formats its string once a second instead of every frame. New markers: `KitRectMask2D.Clip` (its call count = masks awake), `KitRectMask2D.ParkText` / `.UnparkText`. No YAML or look change. | Not measured in Unity (no editor in the session). refcompile player + editor: error set identical to the baseline (after adding RectMask2D `OnDisable`/`OnDestroy` = family to `depublicize_overrides.txt`); `KitRectMask2DTests` (EditMode, 4) written, to be run in the Editor. Owed: one Menu_Main Release capture judged on `ClipperRegistry.Cull`, `SendWillRenderCanvases` self and `UpdateBatches` |

### 2.1 How the picture changed

- **July:** "the game stutters" → the fix was pacing — slice hot passes across frames, move
  sums into Burst jobs. It worked; the menu went from 6 to 50 fps.
- **August:** "prisms are too expensive" → prisms stopped being GameObjects that animate on the
  CPU and became GPU-clocked, instanced entities. That also worked, and was later measured at
  12.9×.
- **September:** "the boot world is slow at 30–55k prisms" → four hypotheses about prisms and
  draw calls were each **measured dead**, and the cost turned out to be the GameObjects hung
  *off* the prisms (spindles, snow shards), plant growth instantiating GameObjects, and code
  nobody has attributed yet.
- **Now:** bleeding-edge swapped the boot world for a cell designed to be cheap. The next
  question is not "why is the boot world slow" but "**which real scenarios miss the target, and
  what do they spend it on**".

---

## 3. The plan — what is left, in order

Revise the plan before running tests. Tests exist to answer a question on this list.
**This section was re-derived on the merged tree (`5c6439e68`, 2026-09-23)** by reading the code
each row names, not from memory. The target and all six scenarios were **confirmed by the human on
2026-09-23**, and all six were measured on 2026-09-24 → 26 (§1.0). S3 intensity 1 is still owed.

### 3.1 Step 0 — the tree, the boot world, the target, the scenarios

1. **Merged** 2026-09-23 (`c2e7a7c46`), then bleeding-edge `899f0baba` on top (`070874533`) — it
   renames the LIT peer arrays, which stops `_PrismSightPeerApex … exceeds previous array size`
   being logged **every frame**, and per-frame console output would distort every capture. All 15
   `Tools/Build/check_*.py` gates pass; every merged `.cs` parses under Release / Development /
   Editor defines. **Compiled clean in the editor at `bfcc16117`** (2026-09-23, which also
   carries the `freeze`/`ab` tools). The first compile of `62681c830` was reported as "Errors"
   with no text; a scan of every name this branch's own code calls found nothing missing, and
   the next compile — on the re-merged tree — came back clean, so the earlier report is
   recorded as not reproduced rather than as diagnosed.
2. **The boot world, confirmed in code.** Menu_Main's Cell is `CellTypeChoiceOptions.EnvironmentFree`
   (2) over 12 configs. `Cell.ResolveBootIndex` takes the first config with `BootDefault`, and
   **`Garland Cell Config` is the only asset in the project that sets it** (index 10). Lattice sits
   at index 0 and is now only the rule-2 fallback, reached through the Cell Selector.

   | World | Prisms | Heart colliders | Grows? |
   |---|---:|---:|---|
   | **Garland** (boot) | 4,259 authored → ~8,147 mature | 37 at cap | 4 phyllotactic species, caps 3–7 plants; no lattice species, so no population-driven growth (`ECOSYSTEM.md §48.4`) |
   | Lattice (opt-in) | ~24k → ~55k over its first minutes (measured pre-merge) | up to 1,080 | yes — reproduction until caps |
   | **Arboretum** (opt-in, **new since the base**) | 54,935 at maturity | 20 | 20 hard-capped plants, then **stops** (`ECOSYSTEM.md §57`) |

3. **Target (confirmed 2026-09-23):** **60 fps in a Development build on the team's reference PC** — a 16.7 ms
   frame, with no frame over 50 ms in steady play. The editor inflates frame time ~2–3×, so an
   editor number is for *comparing* and a build number is for *judging*.
4. **Scenario set (confirmed 2026-09-23, all six)** — the same set every time, so numbers compare across weeks:

   | # | Scenario | Wait | Why it is on the list |
   |---|---|---|---|
   | S1 | Menu_Main, **Garland** (default) | 4 min | What every player sees first |
   | S2 | Menu_Main, **Lattice** via Cell Selector | 8 min | Heaviest *growing* world; every number so far describes it; the only one with plant **births** (L2) |
   | S3 | **Rampage intensity 1** | 3 min | Heaviest arcade cell (5.0× flora, ~49k prisms); **never profiled** |
   | S4 | **Scurry intensity 4** (Atlantis) | 3 min | Heaviest authored environment (~69k prisms) |
   | S5 | **Wildlife Liberation** | 3 min | Heaviest fauna (~1.2k creatures); QuadFish, Clawfish and worm segments carry spindles too |
   | S6 | Menu_Main, **Arboretum** via Cell Selector | 8 min | **Added:** new since the base, the largest spindle world (one spindle per bond on ~55k prisms), and hard-capped — the one heavy world that **stops changing**, so it is where an A/B is cleanest |

   The draft list had five. S6 is the one change, for the reason in its row. SkimRace with 3
   players (the original "single-digit fps" report) stays off the list unless it still
   reproduces after the merge.

### 3.2 Step 1 — attribute each scenario once (no A/B needed)

One Profiler frame per scenario, **Hierarchy view expanded to the leaves**, plus the
**Timeline view** for the render block. This names the cost directly. Record the top 10 rows
per scenario in §1. Two questions this answers that no A/B can:

- **What are the worker jobs the main thread waits on?** Timeline shows them by name during
  the `Idle`. If they are `CullScriptable` / render-queue jobs, lead #1 (renderer count) is
  confirmed without any hide test.
- **What is the other ~17 ms of `UpdateScene`?** Expand `ScriptRunDelayedDynamicFrameRate`
  (coroutines), `ScriptRunBehaviourLateUpdate`, `DirectorUpdate`, physics.

Also sort the Hierarchy by **GC Alloc** once per scenario: that names the 154.5 KB/frame caller.
Then `diag <scenario> 15` for averages. **One pass, one tree, one SHA.**

Since 2026-09-25 the Hierarchy half of this is one command, `prof <scenario>` (§4.5): it reads
180 recorded frames and writes the averaged tree, the top self-time rows, the top allocators, the
worker/render-thread busy vs wait, and the typical and spike frames as JSON — no screenshots. The
Timeline view is still the only way to see WHICH worker job the main thread is waiting on.

### 3.3 Step 2 — the levers we already know about

**Re-ranked 2026-09-26 on the §1.0 numbers (game code `070874533`).** Only S5 misses the target,
so the top of the list is what S5 spends its time on. L8–L11 are new; each is described in the
table below.

| Rank | Lever | Scenario | Evidence (§1.0) | Status |
|---|---|---|---|---|
| 1 | **L8** creature body re-sync, batched | S5 | `LightFauna.Update` 13.1 ms avg self | **The pick** (below) |
| 2 | **L9** the S5 gunfight continuations | S5 | 20.2 ms avg; **123 ms in the spike frame, i.e. the p99** | Needs the marker capture first |
| 3 | **L10** the behaviour-tick neighbour scan | S5 | 12.7 ms avg; 29.9 in the median frame | Needs the marker capture first |
| 4 | L1 spindles | S2 (inside target); S3 i1 (unmeasured) | S2 **+5.3 ms ±0.4**; S6 noise | Waits on the S3 i1 re-run |
| 5 | L2 growth instantiation | S2 spike frames | `GrowCoroutine` 15.8 ms in one frame, 5.0 of it `Instantiate` | Spikes, not the average |
| 6 | **L11** collider-LOD tick hitch | S4 | `LOD.Sweep` 15.9 ms every 0.25 s | Pacing, not the average |
| 7 | L3 gameplay GC | all | 20–53 KB/f; the pre-merge 154.5 is not reproduced | Demoted |
| — | L4–L7 | | Unchanged | |

**Why L1 waits on S3 i1.** Rampage at intensity 4 already runs 29,568 enabled renderers.
Intensity 1 grows 5× the plants. At S2's measured slope, that projects to **~15 ms** of spindle
cost in the heaviest arcade cell. That is a projection, not a measurement, and L1 goes back to
the top only if the re-run confirms it.

**The pick: L8 — batch the creature body-prism re-sync.**
- **What it replaces.** Every moving creature, every frame, walks its own body prisms (the mover
  contract, `Fauna.NotifyBodyPrismsMoved`). For each prism it:
  - reads the transform,
  - moves its entry in `PrismSpatialIndex`,
  - refreshes its shell,
  - writes its render matrix through `EntityManager.SetComponentData`.

  That is thousands of prisms a frame in S5, each several native calls.
- **What it becomes:** one pass after every creature has moved.
  - A `TransformAccessArray` read inside a Burst job.
  - One index pass.
  - One bulk matrix write.
- **Why it is safe.** It does not change behaviour: the same positions land in the same frame.
  It must run after the last creature's `Update` and before the behaviour-tick coroutines, which
  read the index. It touches no ecology rule. It is platform-wide: every creature in every cell,
  plus every other mover on the same contract.
- **Why it beats L9 and L10 today.** It is the only one of the three whose content is known from
  the code alone. L10's fix is also boxed in: it must keep the ONE diet predicate
  (`Fauna.IsPreyForMe`), so it cannot be a Burst copy of the rule.
- **Expected saving: ~5 ms of CPU busy in S5** (range 4–7).
  - 13.1 ms profiled × the 0.62 Profiler-to-diag ratio ≈ 8 ms for all of `LightFauna.Update`.
  - The re-sync is the larger part of that; movement, feeding and hunting stay.
- **Go / no-go before building.** In the marker capture, `Fauna.BodySync` must average **≥ 8 ms**.
  If `FullAuto.Fire`, a `Projectile.*` marker or `LightFauna.Tick.PrismScan` is bigger, the pick
  moves to that one.
- **Proof.** The lever ships with a console switch. In S5, after 3 min, run `freeze on`, then
  `ab "bodysync batch" "bodysync legacy" 20 6`. Expect CPU busy **B − A ≈ +5 ms (+4 to +7)**,
  well outside ±0.4.

Rows L1–L7 were ranked by the evidence we had **before** the merge. Each needs Step 1 to confirm it is big in a
scenario that **misses the target**; the ranking is redone from those numbers once §1 holds
them. "Merged tree" says whether the code the row names is still there.

| # | Lever | Evidence (pre-merge) | Merged tree (`5c6439e68`) | Size |
|---|---|---|---|---|
| L1 | **Spindles off GameObject renderers** — draw them through Entities Graphics like prisms, or merge a plant's spindles into one mesh | ~40k enabled spindle renderers vs 4.6k visible (Lattice); render-job wait 14 ms; hide test suggests ~4 ms (confounded, §4.3) | **Still true, and wider.** `Spindle` is still one GameObject `Renderer` per limb; the +121 lines are sway constants handed to health prisms (`PrismSway`) and a `ResolveRenderedObject` fallback. The new Mandelbulb/Borromean families (Arboretum) grow one spindle per bond (`MandelbulbFlora._limb`), and some creatures carry spindles **Re-measured 10-17 (Amoebius, Yash's session): the premise is stale.** The real Lattice (Cell Selector > Lattice, the lava lamp boots Garland since `ECOSYSTEM.md` §48.9) holds **2,898 spindles at 8 minutes** (28,200 frames), Garland 3,287 at 8 minutes, against ~40k in the 09-25 Unity capture. A size-on-screen render LOD (hide a limb under 0.75 px radius, continuity-safe) was built and measured: it hid **0 of 2,898** in the Lattice and 0 of 1,963 / 3,287 in Garland (limbs are 2-16 px), so it was removed rather than shipped. Hiding anything bigger is a visible pop. Before any structural move (Entities Graphics or one mesh per plant), a Unity S2 capture's `renderers` census has to show the spindle count is still large (T-20261015-56dd). **2026-10-20, built at Garrett's ask (`cece/gallant-keller-6mr1u4`, `Docs/SPINDLE_LOD.md`):** the 10-17 LOD hid nothing because it measured a limb's bounding SPHERE (a 15-unit wire is 2-16 px "big" while its 0.3-unit strands are sub-pixel); the new one measures THICKNESS in girths. Three tiers (the untouched braid; the braid at every third ring converging into one thread on the GPU; the thread alone, 24 tris), and a STILL far limb leaves GameObject rendering for one `RenderMeshInstanced` call per material. Amoebius Garland: 865 of 910 limbs instanced. Unity A/B (S2, `lodEnabled` on/off) pending | Large, structural. Must keep the per-spindle wither fade (continuity law) — kept: a fading limb leaves the batch |
| L2 | **Recycle flora prisms + spindles** instead of minting, and route plant **births** through the budgeted spawn queue | `Instantiate` 3.6 ms / 720 µs per growth step; birth spike ~1.2 ms (`archive §0.8`, §4 Tier 0a) | **Still true, sharper.** Every growth path takes its prism from `EnvironmentPrismPool.Get`, but that pool only **mints** during play: a consumed or exploded prism is `Destroy`ed and never returns. *(2026-10-29: a destroyed natural-death skeleton now returns via `PrismRecycler` after its death effect, row 10-29 of §2; destroyed flora/fauna body prisms still leave with `LifeForm.Die` and are the remaining L2 churn.)* Spindles are a raw `Instantiate` (`LifeForm.AddSpindle`, `AssembledFlora.cs:571`, `PhyllotacticFlora.cs:466`, `BranchingFlora.cs:153`). Recycling can only cover churn (grazing + regrowth), not net growth | Medium. Ecology change — `/ecology` protocol |
| L3 | **Find the gameplay GC** and the multi-MB spike frames | 154.5 KB/frame in Lattice, all under `UpdateScene` | **Unknown** — a Lattice-only number; re-measure per scenario | Unknown until attributed |
| L4 | **Pure-entity prisms** (no GameObject at all for bulk mass) | 28,544 disabled prism GameObjects exist; `Docs/PRISM_ECS_MIGRATION.md` Checkpoint D | **Still true**: Checkpoint D is designed, not built | Large, structural, post-launch unless a scenario demands it |
| L5 | Cheap, known-wrong code, no capture needed | `archive §4 Tier 1–2` | **All still present**, lines moved: `HijackController` AIs re-plan on one frame (`nextRetarget = 0f`, `:217`/`:272`); `AstroLeagueBall._shieldPoppedThisVisit` is a `List` scanned by `Contains` (`:1214`, `:1236`); `PrismSpatialIndex`'s four `Schedule(_highWaterMark).Complete()` scans (`:2211`, `:2363`, `:2448`, `:2520`) with no "anything shielded?" early-out; `ConnectingPanelController.Update` rebuilds strings every frame (`:174–193`); `Boid.cs:627` allocating `OverlapSphere` | Small each |
| L6 | Snow shards: ~4,189 GameObjects with a renderer each (`SnowChanger.shardDistance` 120) | Renderer census; 4,189 enabled SnowMaterial renderers, the largest family in the first shared Unity capture (10-16) | **Done 10-17, density kept.** Instead of a density cut (a look call), `SnowChanger` draws a single-mesh shard prefab (`shard.prefab`) as instanced draws from arrays (`Graphics.RenderMeshInstanced`, in calls of 1023): the same positions, LookAt and scale law, no GameObjects, no Instantiates at cell boot. Any other shard prefab (children, extra components) keeps the GameObject path. Caveat: transparent instances are not depth-sorted inside one draw. Amoebius lava lamp: the submit is 0.001 ms/frame | Go/no-go: a Unity capture's renderer census without the snow family, and a look at overlapping shards |
| L7 | Memory: music `DecompressOnLoad` (182 MB resident), texture streaming off | `Docs/MEMORY_AUDIT.md` | **Still true** (`cosmic shore chill time 3.wav` `loadType: 0`, `quality: 1`) | Load time + RAM, not frame time |
| L8 | **Batch the creature body-prism re-sync** — one pass after all creatures move: `TransformAccessArray` read in Burst, one index pass, one bulk `LocalToWorld` write | §1.0 S5: `LightFauna.Update` 13.1 ms avg self, 374 calls | Code read at `070874533`: `Fauna.NotifyBodyPrismsMoved` → per prism `Prism.NotifyPositionChanged` (index + shell + `EntityManager.SetComponentData`) | Medium. Behaviour-neutral; must keep the mover contract's same-frame visibility |
| L9 | **The S5 gunfight continuations** — AI full-auto fire + per-round flight steps | §1.0 S5: 20.2 ms avg, 123 ms in the spike frame | Contents unknown; markers in `aaa1517fe` | Unknown until the marker capture |
| L10 | **The behaviour-tick neighbour scan** | §1.0 S5: 12.7 ms avg, ~0.85 ms per tick | Goal query is cached, so the cost is the `QuerySphere` + per-candidate loop | Must keep the ONE diet predicate (`Fauna.IsPreyForMe`) — no Burst copy of the rule |
| L11 | **Collider-LOD tick hitch** — slice `RunSweep`'s transitions across frames | §1.0 S4: `LOD.Sweep` 15.9 ms on one frame in five | `PrismColliderLodManager` restores are unbudgeted by design (safety); which half costs is unknown | Small–medium; must never delay a restore near a focus |
| L13 | **Pool the AOE explosions** — `ExplosionHelper.SpawnAllAndDetonate` is `Object.Instantiate(prefab)` per detonation and `AOEExplosion` Destroys itself after | Amoebius Skim Race I2, 10-15: `VesselImpactor.AcceptImpactee` 5.6 KB/frame, attributed then to `SquirrelVesselExplosionByCrystalEffect` as a whole | **Done 10-17 (Yash's session, for Garrett), and the attribution was wrong.** `AOEExplosionPool` (opt-in per class, `AOEExplosion.Poolable`, `Retire()` instead of `Destroy`) now reuses the Squirrel's ring spawner: 1 miss and 7 reuses over 8 collects, and the one Instantiate left is 0.012 KB/frame, so **the shell was never the allocator**. Nested markers found the real one: `CrystalMorphMeshBuilder.TryBuild` (the crystal-to-ring morph, run from `BoostRingBuilder.RingLaid`), 5.22 of the 5.53 KB/frame, ~1.3 MB per collect, re-expanding the whole crystal mesh into seven per-corner arrays every build. It now caches the source-only arrays per mesh (`EmitCache`, like the panel analysis) and reuses its target scratch: 5.22 -> 3.11 KB/frame, `AcceptImpactee` 5.53 -> 3.41. The 3.1 left is Amoebius's own `Mesh.Set*` copying each array (`Port/.../Rendering/Mesh.cs:249-290`); Unity copies to native memory, so in Unity a collect now allocates only the morph's small lists and the `Mesh` object. The damage blasts are NOT pooled: their `ExplosionImpactor` keeps per-blast hit ledgers and friendly-fire overrides that assume a fresh instance | Go/no-go: a Unity Release Skim Race capture shows no `Instantiate` and little GC on a collect |
| L14 | **The Bloomrush teardown frame** (B36, `Docs/PartySystem/BUGS.md`) - leaving a grown Bloomrush for the menu destroys every laid prism in one Single-load frame; a frame of ~10 s drops the Relay connection | Owner's 20 Oct editor: 3 drops, each ~3 s after the Bloomrush scene unloaded; capture 10-20 16:02 (before the 10-24 creation queue): 220.8 ms CPU busy, 57,638 `Prism.CreateBlockCoroutine` alive, 3,739 prism entities, 4.3 GB managed. **Amoebius, 2026-10-25 (`Ys-bleeding-edge`, after the queue):** Bloomrush I1, 2 seats, headless, 5,400 frames (90 s of game time): **26,680 prisms still waiting in `Prism.CreationQueue`** (each already a GameObject); the switch to Menu_Main is **one frame of 1.28-1.50 s** (0.83 s after 600 frames). A sampling trace (`dotnet-trace`, `dotnet-sampled-thread-time`) of that frame: 720 ms the engine parsing Menu_Main's files, 838 ms in GC polls, 419 ms destroying objects (engine code), **game code only `GenericPoolManager.ReleaseAllActive` 56 ms**; no game `OnDestroy` is O(n^2) (`Prism.OnDestroy` -> `PrismSpatialIndex.Unregister` is O(1)) | Amoebius cannot measure Unity's native destroy and asset unload, which is where the frame most likely goes; the game's managed teardown is small. The 10-24 queue removed 57k coroutines that a destroy also had to stop, so the freeze may already be much shorter. **Next: measure in Unity** - a Load Time Insights capture of Bloomrush -> Menu_Main after 90 s, on `Ys-bleeding-edge`. Since 2026-10-25 it records itself in any online play (`loadKind: "menuReturn"`) and reaches `bugledger/<user>` after Play; `net_logs.py show` lists it beside the test. Then, by what it shows: (a) fewer objects to destroy - the forest's build rate is Garrett's open call (T-20261024-3084); (b) destroy the waiting prisms in slices behind the already-opaque return splash before the Single load. Not built: no Unity number yet | Go/no-go: the capture's longest frame on the return. Over ~5 s -> (b); the activation frame alone -> (a) |

**New since the base, not a lever yet:** every prism render prototype gained four `float3` sway
overrides (`PrismSwaySpan*`/`Axis`/`Timing`, 48 B per entity) for the health-prism sway
(`ECOSYSTEM.md §47`). That is more per-instance data to upload per prism; nothing measured says it
matters. Watch the GPU row in S2/S6.

### 3.4 Step 3 — the performance gate (built 2026-10-15: the perf-hunt system)

CI still runs no Unity job (`UNITY_RUNNER_LABEL` unset), so the gate is built from what runs without one,
and the editor's numbers reach it through the after-play "Send to Claude" (`bugledger/<user>` branches). Three pieces, one script
(`Tools/Build/perf_hunt.py`, `--self-test`), one skill (`/perf-hunt`), one workflow:

| Piece | What | When |
|---|---|---|
| **`perf_hunt.py gates`** | the static allocation gates (`check_mathf_params_alloc`, the two Burst MathF gates, console logging) and `scan_perframe_allocations.py`'s counts against the recorded counts (`Docs/perf/perf_hunt_state.json`): a new per-frame allocation site is a regression the day it lands | every sweep |
| **`perf_hunt.py collect` / `compare`** | every `capture_*.json` an editor shared (`BugLedger/shared/reports/<user>/perf/`, every origin branch, the way `bug_ledger.py collect` reads issues), judged by `perf_capture_report.py` (validity first) and compared with `Docs/perf/baselines.json` per `scene\|label`: CPU busy +15 % (≥ 0.5 ms), p95 +15 %, GC +25 % (≥ 2 KB/f), a marker +30 % (≥ 0.2 ms), or a prism-path change | every sweep |
| **`perf_hunt.py amoebius`** | `amoebius_markers_run.py` for a mode; **allocations per marker vs `Docs/perf/amoebius_baselines.json`** (+10 %, ≥ 0.05 KB/f; they repeat to ~1 %, so this is a same-day gate); milliseconds only watched (+25 %) | daily, and before/after a fix |
| **`perf_hunt.py sweep --write --tasks`** | gates + collect (+ `--amoebius`) → the generated block of `Docs/Reports/PERFORMANCE.md`, and one `Docs/TEAM_TASKS.md` task (area Performance) per NEW regression | daily, any session |
| **`.github/workflows/perf-daily.yml`** | 05:40 UTC on `bleeding-edge`: the sweep (strict: red on a gate or a baseline count), and the Amoebius Skim Race run against the committed allocation baseline; artifacts `perf-sweep`, `perf-amoebius` | every day |
| **`/perf-hunt`** | the resolve loop: validity → rank by measured ms and KB → pick by §3.3 / never §3.5 → fix against the checklist → prove before/after → `/bug-closeout` + `--adopt` the new baseline | a scheduled session, or anyone |

The first measurement of a key becomes its baseline (bootstrap); a baseline is only ever replaced after a
confirmed improvement (`--adopt`), never to make a run green. The `-csmbench` dev-build self-runner stays the
build-side instrument for the day a Unity runner exists.

### 3.5 Do NOT redo these — measured dead

| Hypothesis | Why it is dead | Evidence |
|---|---|---|
| Prism draw calls are the cost | A draw costs 0.042 µs CPU; all draw submission is ~0.2 ms | `archive §0.11`, `§0.11.6` |
| Re-key `PrismRenderService.GetPrototype` by mesh + material | Would move ~10% of draws; prisms are 23 draws | `§0.11`, `§0.11.4` |
| Chunk spatial locality | 320× locality change moved draws 0.05% | `§0.11.1` |
| Convert `SnowMaterial` / `SpindleMaterial` to opaque | Snow's surface is baked in its graph; spindles would pop when withering | `§0.11.5` |
| GC comes from Entities Graphics / prisms / the HUD / coroutines | Legacy path allocates more; empty scene allocates more; hidden overlay same; arithmetic 100× short | `§0.11`–`§0.11.3`, `§0.9` |
| The stress cloud inflated the boot-world numbers | Reproduced without it | `§0.11.4` |
| Reorder `Projectile.SweepPrismsAlong` so the `PrismImpactor` lookup runs only after the contact test | Neutral: Amoebius Breakwater I2 (~71 rounds/frame) `Projectile.SweepPrisms` 0.178 -> 0.177 ms/frame on an interleaved A/B (an earlier single pair read -12%, but the unchanged `FullAuto.Fire` moved -18% in it: noise). The cost is per round, not per candidate lookup; the real lever is batching every round's flight step (one job, not ~140 UniTask steps) | 2026-10-26, §2 |

### 3.6 The 2× plan: 100,000 prisms at 60+ fps on a phone (set 2026-10-14)

**The ask.** The owner saw ~40k prisms under 60 fps and wants 100k at 60+ on a phone: at least
twice today's performance. This section states what the record already proves, what it does not,
and the order of work. Every number below is from §1.0, the archive or §3.3; nothing here is new
measurement.

**Why 40k prisms read under 60 fps — the prisms are not the cost.**

| Fact | Evidence |
|---|---|
| 103,823 prisms on the instanced path: **122 fps, 4.8 ms CPU, 2.1 ms GPU** (lab, desktop editor) | archive §0.10 |
| S4 Scurry i4 with **69,094** prisms: **74.8 fps**, 10.6 ms CPU — the most prisms of any scenario and the fastest | §1.0 |
| S2 Lattice with 42,076 prisms: 38.6 fps, 21.4 ms CPU — but 89–93% of its draws are **not prisms** (2,401 transparent draws: ~40k spindle renderers, 4,189 snow objects, crystals); prisms are 23 opaque draws | §1.0, archive §0.11.4–5 |
| S5 Wildlife Liberation with only 13,419 prisms: **19.8 fps**, 46 ms CPU — all script: UniTask continuations 20.2 ms, `LightFauna.Update` 13.1 ms, behaviour tick 12.7 ms | §1.0 |
| Marginal cost per prism in a real world: 0.24–0.43 µs/frame on top of a **fixed ~10 ms** that is there at 0 prisms | archive §0.11.4 |
| Collider-LOD sweep spikes **15.9 ms every 0.25 s** at ~69k prisms | L11 |

So a scene "with 40k prisms" is slow because of what grows alongside them (spindles, snow, fauna,
AI, the sweep) and a 10 ms floor, not because of the prism draw path, which is already near its
limit. Doubling prism count on the instanced path costs ~2–4 ms; doubling the rest costs the frame.

**What the record does NOT contain: a single phone measurement.** Every row above is a desktop
editor. The only phone data are two anecdotes (`PLATFORM_UNIFICATION.md` §40–66): a 4 GB Samsung at
~4 fps, an iPhone at 30–40 fps. Nobody has recorded which **prism path** a phone takes. The device
gate (`EntitiesGraphicsSupportProbe`, `PRISM_ECS_MIGRATION.md` §8) drops every prism to the legacy
MeshRenderer path — the **12.9× costlier** one — when compute shaders or the `SparseUploader`
kernels are missing; `PRISM_ECS_MIGRATION.md` says BRG needs Vulkan/Metal and the Android build is
Auto [Vulkan, GLES3]. A phone that lands on GLES3 may be running the path the lab measured at 14 fps
for 100k prisms. That one unknown could be the whole "phone is slow" story, and it is a 10-minute test.

**Three ceilings 100k on a phone must clear, in the order they bite:**

1. **Which path** — legacy vs instanced is 12.9×. Nothing else matters until this is known per device.
2. **Memory** — each prism is still a GameObject with 7 MonoBehaviours and a collider (~2–4 KB) plus a
   ~375 B render entity; 103k prisms read 1.8 GB managed in the editor (`MEMORY_AUDIT.md`). A MobileLow
   phone has < 5 GB total. 100k GameObject prisms will not fit; **L4 pure-entity prisms** is structural,
   not optional, for this target.
3. **GPU** — 100k × 24 tris = 2.4M prism tris/frame, 2.1 ms on a desktop GPU. A mid phone GPU is
   ~10–20× slower: 20–40 ms. Fine on MobileHigh at 71% render scale, doubtful on MobileLow. And the
   non-prism transparent load (spindles/snow overdraw) hits tile-based phone GPUs far harder than a desktop.

**Definition of "2×" (so it can be passed or failed).** Two numbers, both on the same phone, same
scenario, Release, `diag` 15 s, §4.7 protocol:
- **A.** CPU busy ms at the §1.0 scenario set (S2 and S4 are the prism-heavy ones) — halve it.
- **B.** The largest prism count that holds 60 fps p95 in S4 (the fastest prism-heavy scene) — double it,
  on each tier device.

**Order of work.** Each step ends in a measurement; the next step is chosen from it, not from this list.

| Step | What | Owner | Proof |
|---|---|---|---|
| **0** | **Phone baseline.** Development build (Android Vulkan, Android GLES3 forced, iOS). On each: F7, `diag phone 15` in S2 and S4, `prisms 50000` then `diag`, `renderers`, and the HUD's prism path line. Pull `persistentDataPath/CosmicShore Diagnostics/*.json`. Two devices minimum: one MobileLow (Adreno 610 / Mali-G52 class), one MobileHigh | human + tool | `diag` JSONs committed under `Docs/perf/phone/`; `prismPath` field says `instanced` or `legacy` per device |
| **1** | **Guarantee the instanced path on phone.** If step 0 shows `legacy` anywhere: decide GLES3 (drop it, or make the probe fall back to a cheap non-BRG instanced path) — the "biggest open product decision" in `PRISM_ECS_MIGRATION.md` §417. If `instanced` everywhere, this step is free | code | re-run step 0: every device `instanced` |
| **2** | **Non-prism render load** (phone GPU): L1 spindles onto the instanced path keeping the alpha fade; L6 snow distance; cap transparent draws per tier. S2 is the scene | code | S2 `diag`: transparent draws and GPU ms halved on the MobileLow device |
| **3** | **Script floor**: L8 fauna body-sync (gated on `Fauna.BodySync` ≥ 8 ms in S5 — Amoebius reads 0.06 ms in Skim Race, so measure S5 first), L9 gunfight continuations, L11 slice the collider sweep, L5 spatial-index early-out, L3 GC (`PoolRefill` ~9 KB/frame lead) | code | S5 and S4 `diag`: CPU busy ms; no 15 ms sweep spike at 69k |
| **4** | **L4 pure-entity prisms** — the structural change for 100k on a phone: no GameObject per prism, collider only within the 200 m LOD radius, data in chunks. Largest step; design exists (Checkpoint D) | code | 100k `prisms` cloud on the MobileHigh device: memory and fps recorded; S4 at 2× its prism count |
| **5** | **Gate.** Nightly `-csmbench` on S1–S6 per tier (§3.4) plus the Amoebius marker run as the no-editor allocation gate (§2, 10-09) | tooling | a regression fails a run |

**How a test is run now (2026-10-14).** One command in the HUD console, `capture <label> 15`, or the
automatic capture: since 2026-10-15 the editor runs it by itself in Play, once per scene, after the scene's time
(`PerfAutoCapture`; S2 480 s, Skim Race 90 s, S5 120 s from `perf_hunt.py SCENARIOS`, other scenes 90 s; skipped in
Debug code optimization). There is no Capture button; after Play the editor sends the file to Claude by itself (no question), or paste it.
The agent reads it with `Tools/Build/perf_capture_report.py`, which refuses a Debug-optimization, Deep-Profile
or managed-job capture before it ranks anything. **First editor test asked for: S2 Lattice** - Menu_Main,
Cell Selector → Lattice, let it grow 8 minutes, `capture S2 15`. It is the owner's "40k prisms under 60 fps"
scene, and the bundle also answers the Burst question (`burst` + the Profiler's managed-job rows).

**Tests owed before any lever is built** (this is the list the owner asked for):
1. Phone `diag` S2 + S4 + `prisms 50000` on a MobileLow and a MobileHigh device, Vulkan and GLES3 — the baseline and the path answer.
2. Unity Release `burst` + `diag` + `prof` in Skim Race I2 — confirms the 10-08 Burst fix (still unmeasured) and the `PoolRefill` allocation lead.
3. `prof` of S5 for `Fauna.BodySync` average — the L8 go/no-go.
4. Memory reading in a build (not editor) at 50k and 100k prisms — sizes L4.
5. S3 Rampage intensity 1 — still owed from §1.0.

**What this plan refuses.** Re-keying prototypes, chunk locality, opaque snow/spindles, "prism draw
calls are the cost" — all measured dead (§3.5). And no lever is built on a desktop number when the
target is a phone: step 0 comes first.

---

## 4. How to measure — simply

The last month's biggest waste was not a wrong fix. It was measurements that looked like
answers and weren't: single-frame HUD reads, arms taken in different worlds, an unfocused
editor window, and screenshots re-typed by hand.

### 4.1 One question → one tool

| Question | Tool | Output |
|---|---|---|
| **Everything, once, with nothing to transcribe** | Just play: the editor captures by itself (`PerfAutoCapture`; or `capture <label> 15` in the console), it goes to Claude by itself after Play, or paste the file (§4.6) | One JSON: burst + diag + prof + renderers + cells + console; `perf_capture_report.py` reads it |
| **What is costing the frame?** | **Unity Profiler**, Hierarchy + Timeline, Deep Profile **off** | Names the caller. **Always start here.** |
| **Where does the GC come from?** | Profiler Hierarchy, sort by **GC Alloc** — or `prof <label>` (`topGc` ranks by SELF allocation) | Names the caller |
| **The Hierarchy as text** | `prof <label> [frames] [root=…] [sort=…]` (§4.5) | `prof_*.json`: averaged tree, top self, top GC, threads, typical + spike frame |
| **What is issuing draws?** | Frame Debugger | Names the shader / object |
| **Did my change help?** | `freeze on` + `ab "<new>" "<old>" 20 6` in the **same state**; or two `diag <label> 15` reports | A delta with an error bar |
| **Which system costs what, in a BUILD?** | `diag <label> 15` in a Development build — it times ~23 named markers per frame with `ProfilerRecorder`, no Profiler attached (§4.5) | `markers` block: avg / p50 / p95 / max ms per system |
| **Is it GPU-bound?** | HUD `Bound` row, after `fps uncap` | |
| **Prism-only questions** | Lab scene `PrismGridExplosionTest` (`grid`, `lab mix …`) | Fixed population, no Bootstrap |
| **The real number** | A Development build, run the §4.7 way, `diag` per scenario | The only number to judge against the target |
| **Which system allocates, with no Unity?** | `python3 Port/tools/amoebius_markers_run.py --mode <Mode>` (Amoebius, headless) | Per-marker ms and **KB/frame**; allocations carry over to Unity, milliseconds do not (no Burst, .NET JIT) |
| **Did my change help (allocations, scaling)?** | Two `amoebius_markers_run.py` runs, then `--compare A B` | Per-marker delta; confirm a timing win in a Unity Release `diag` |
| **The Hierarchy of a BUILD** | Profiler attached to the build → **FrogletTools ▸ Performance ▸ Export Profiler Frames to JSON** | The same `prof_*.json` as the in-Editor command |

### 4.2 Pre-flight — every time

1. **Burst ON** (`Jobs ▸ Burst ▸ Enable Compilation`). A job row named
   `ExecuteJobFunction.Invoke() [Invoke]` means Burst is off and every job runs ~20× slow.
2. **Deep Profile OFF.** It inflates the frame ~10× and changes what you measure.
3. **Click inside the Game view** so it has focus. An unfocused editor throttles and the HUD
   shows `Stalled — N ms unattributed`; Frame Time and FPS are then meaningless (CPU busy is
   still valid).
4. **`fps uncap`** in the HUD console, or a vsync-capped frame hides every change smaller than
   its idle time.
5. **Write the commit SHA next to every number.** A measurement describes one tree.

### 4.2a When the EDITOR is slow, not the game (Yash, 2026-10-15: Regatta at 10 fps)

On the same machine (RTX 4070 Ti), Skim Race I2 with 2 AI captured at **66 fps** (CPU busy 13.1 ms, GPU 3.7 ms,
Release, 2026-10-08). A Regatta session at **10 fps** is therefore the editor's setup, not the GPU. Check these in
order (the cost estimates are from this record and `Docs/SKIM_RACE_AI.md` §14.4):

| # | Check | Why it costs |
|---|---|---|
| 1 | **Code Optimization = Release** (bug icon, bottom right; the badge's Release button). Unity starts in Debug unless Preferences > General > Code Optimization On Startup says Release. | About 5x on the AI planner and on every game C# path, URP's own C# included |
| 2 | **Profiler: Deep Profile off, target Play Mode, recording off while just testing.** The badge's Capture button opens the Profiler for `prof`. | Deep profile is about 10x; editor profiling adds the windows' cost |
| 3 | **No visible Scene view while playing** (a tab behind the Game view is fine) | It renders the world again every Play frame |
| 4 | **The AI Diagnose panel (Third Eye) closed** *(until 2026-11-01 the Third Eye was an editor window with a second render; it now drives the main camera, so only its overlays and trails cost)* | Overlay lines, trails, labels |
| 5 | **Game view no larger than 1440p**; Burst on; Game view focused | GPU pixels; managed jobs (10-20x); an unfocused editor throttles |

**To find out which it is, measure, do not guess:** play two sessions past the auto-capture time (90 s for a scene with no
scenario), once with the AI Diagnose panel closed and once with it open, each goes to Claude by itself after Play.
A capture now records the editor's own state (`editor`: Scene views open, Game view size, deep profiling, editor
profiling) beside the camera list, and `perf_capture_report.py` flags each one.

**When the editor cannot go faster, test in slow motion:** HUD console (F7) `timescale 0.25`. At 10 fps a hull
at 300 u/s moves 30 u per frame and overshoots its line, so AI (and hand) testing at a low frame rate is not
representative (`Docs/SKIM_RACE_AI.md` §8.0c). At 0.25x each frame steps what 40 fps does. `timescale off` resets
it; a scene change releases it. A development player build is the other honest frame rate (the HUD console,
`capture` and the AI Diagnose panel work there).

### 4.3 The same-state rule (why the spindle test failed)

An A/B is only valid if **the only difference between the two arms is the thing under test**.
The Lattice world grows: the spindle test's arm A had **24,243** prisms and arm B **49,116**,
because they came from different moments. CPU busy read 19.1 ms vs 20.3 ms, and that cannot
be split between "twice the world" and "46,248 spindle renderers hidden". (Against the
separately measured growth curve — 20.3 ms at 31k, 25.9 ms at 55k — arm B would have been
~24.5 ms with spindles, so hiding them *may* be worth ~4 ms. That is an estimate across
sessions, not a measurement.)

Three ways to satisfy the rule, simplest first:

1. **Attribute instead of A/B.** If the Profiler names it, no A/B is needed (§3.2).
2. **Measure a world that does not change** — the lab scene, or an authored cell (Garland,
   Atlantis) at a fixed time after load.
3. **Freeze and interleave** a growing world — both are now one command each (§4.5):
   `freeze on` stops the growth, and `ab "<A>" "<B>" 10 3` interleaves the arms, pairs them by
   round and prints the delta with an error bar. Freezing removes drift; interleaving cancels
   whatever drift is left (exactly over an even number of rounds, to a third over three).

### 4.4 Send text, not screenshots

- **Benchmark window → Runtime Capture → Copy error log** puts stats, spikes and the script
  methods behind each spike on the clipboard as text.
- **`diag <label> <seconds>`** writes `Documents/CosmicShore Diagnostics/diag_<scene>_<label>_*.json`
  and a `.txt`. It holds:
  - averages: `avgGcKbPerFrame`, `avgDraws`, CPU/GPU, `prismPath`;
  - the frame's **p50 / p95 / p99**;
  - the renderer census, taken after sampling;
  - since 2026-09-26, **per-system timings** (`markers`);
  - the **run environment** (`environment`): Editor or build, Burst, Profiler, focus, resolution,
    quality, GPU/CPU.
- **`prof <label>`** is the text export of the Profiler Hierarchy that Unity does not have
  (§4.5). Screenshots are now only needed for the Timeline view.

### 4.5 The same-state tools (added 2026-09-23)

- **`freeze on` / `freeze off`** — raises `Cell.DiagnosticProductionHold` on every cell. It is
  production gating only, the same class of gate Frenzy already is: `FloraGrowingEnabled` (and so
  planting, plant reproduction and lattice colony births) goes false, `FaunaSpawningEnabled` goes
  false, `IsFaunaAtCap` / `IsFloraAtCap` read full, and the two producers that ask neither —
  `RandomLifeSpawner`'s fauna seeder and the worm colony's growth tick — check the hold directly.
  **It removes nothing and runs no timer**: grazing, predation and starvation keep working, so a
  frozen world can only lose mass; fauna aggression is untouched because the phase is untouched;
  and every producer turns its cycle whether or not it produced, so `freeze off` resumes growth at
  the ordinary rate instead of hatching what was held. It is settable only in the Editor and
  Development builds, and a scene change releases it (with a warning) so a hold can never freeze
  the next scenario. Collider impact: none. `diag` reports now record whether it was on.
  Not covered, deliberately: the player's own trail, and the Lifeform Matrix toy's explicit
  releases (a player's act, not production).
- **`ab "<A>" "<B>" [seconds] [rounds]`** — runs the two console commands as the arms of one
  comparison: rounds are counterbalanced (A B | B A | A B …), each command is followed by a 3 s
  settle, a renderer census and a 0.5 s gap, then `seconds` of recording. It prints **one line** —
  `ab: CPU busy B-A = -3.8 ms ±0.4 · GPU … · frame … · draws … · GC/f … (3 rounds, frozen: yes)` —
  and saves every recording in one `ab_*.json` + `.txt`. `±` is the standard error of the
  per-round differences (rounds are the replicates; frames inside one recording are not
  independent), and a delta inside two of them is marked `(noise)`. Loud warnings: world not
  frozen; the SAME arm's prism-entity or enabled-renderer count moving >5% (between arms these
  may differ — `renderers hide` changes the renderer count by design, `prismpath` the entity
  count); a recording whose frame was capped, idle or stalled. The world is always left in arm
  **B**'s state, so put the restoring command second. The statistics are pure
  (`ABComparison.cs`) and tested, including two negative controls: pure drift reads as a real
  effect under a fixed A-then-B order and as zero under the counterbalanced one, and a renderer
  count that differs between arms by design raises no warning.
- **A family's test must hide the FAMILY — check the count before trusting the delta.** The
  first `ab "renderers hide Spindle" "renderers show" 10 3` in a grown Lattice cell (2026-09-25,
  on `73e99421b`, ~52k prism entities, ~68k enabled renderers) hid **5** renderers: since §47.7
  the lattice species' spindles wear `GyroidSpindleMaterial` / `AssemblySpindleMaterial` /
  `QuasicrystalSpindleMaterial`, which the prefix `Spindle` does not match. Both arms were the same
  world, and the tool correctly called every delta `(noise)` — which makes the run an accidental
  A/A test worth keeping: in the menu, with the autopilot camera moving through the forest, 3 × 10 s
  gives **±3.9 ms** standard error on CPU busy (draws swung 4.3k–19.3k inside one condition). So
  the spindle test is `ab "renderers hide *Spindle" "renderers show" 20 6`: a leading `*` makes the
  match a substring, and six 20 s rounds are what it takes to see a few milliseconds through that
  noise. Run `renderers hide *Spindle` by hand first and confirm it hides thousands, then
  `renderers show`.
- **`prof [label] [frames] [root=<name>] [sort=total|self|gc|calls] [min=<ms>] [mingc=<KB>]
  [depth=<n>] [top=<n>]`** (added 2026-09-25, Editor only) — the Profiler Hierarchy as JSON. It
  switches Record on, waits for `frames` new frames (default 180, 10–600), switches Record off so
  the ring buffer cannot roll, reads every frame back and restores Record to how it found it. The
  report holds: the main-thread tree **averaged over every captured frame** (a sample absent from a
  frame counts as 0 there, so a 1-in-10 spike does not read as a steady cost — `presentPct` says
  how often it ran and `maxTotalMs` how bad it got); `topSelf`, self time summed by NAME across
  every path; `topGc`, ranked by **self** allocation — the Profiler's GC column is inclusive, so
  ranking it names `PlayerLoop` rather than the caller; `threads`, busy vs wait for every other
  thread (sampled every 6th frame), which is where a main-thread `Idle` is explained. A sample
  counts as a wait when it is `Idle`, a `WaitFor…`, a `Semaphore.Wait…` or `GfxTask_ReadValue`. That
  last one is the D3D12 task worker blocked on its next command. It has no "Wait" in its name, and
  until `ded51b6ad` it showed that thread as 100% busy for the whole frame. The report also holds two
  whole frames, the **typical** (median) and the **spike** (slowest). Those two are picked by
  `PlayerLoop` time, not the whole frame, because in the Editor the slowest whole frame is usually
  an Editor repaint — a test proves the whole-frame pick would choose it. Editor-only rows
  (`EditorLoop`, `EditorOnly*`) are flagged `editorOnly` and left out of the one-line console
  summary. `root=UpdateScene` starts the tree at that sample; `min`/`mingc` drop rows under BOTH
  thresholds; `sort` orders siblings. Keep Deep Profile **off** — the report says when it was on,
  because Deep Profile times every managed call and inflates script cost several-fold. Leave the
  Profiler window open on the Hierarchy view; the Record button is driven for you.
- **Per-system timings in `diag`** (added 2026-09-26; `MarkerBudget` + `MarkerBudgetRecorder`) —
  every `diag` run times a fixed list of named markers with `ProfilerRecorder`.
  - **What is timed.** Unity's frame phases (`PlayerLoop`, `BehaviourUpdate`,
    `CoroutinesDelayedCalls`, `LateBehaviourUpdate`, UniTask `PreLateUpdate`, physics, animators,
    UI, the URP render total) and this project's hot-path markers (creatures, gunfight,
    collider LOD, debris, and the Skim Race AI pilot: `SkimRace.Pilot.Decide`,
    `SkimRace.Pilot.FillObstacles`, both paid by every AI seat every frame).
  - **What each row says.** Main-thread ms per frame: average, median, p95 and max, over EVERY
    frame of the run (absent = 0), plus how often the marker ran and its calls per frame.
  - **It needs no Profiler, so it works in a Development build.** That is the point: it is how a
    per-system budget is tracked where the target is judged.
  - **Adding markers:** `diag S5 15 m=Name.One,Name.Two`.
  - **A name the build does not have** reports `found: false`, so a renamed marker is visible
    rather than silently absent.
  - Names are resolved through `ProfilerRecorderHandle.GetAvailable`, never a guessed category:
    a recorder keyed on the wrong category reads zero without complaint.
  - The first 3 frames of every `diag` are discarded, and the run's clock and the recorders both
    restart after them. The command's own frame and the handle enumeration land there.
  - Stated limit: each recorder keeps its latest 250 frames per second of run and says
    `truncated` past that.
- **Export Profiler Frames to JSON** (FrogletTools ▸ Performance, added 2026-09-26) writes
  whatever the Profiler window holds as the same `prof_*.json`. That covers:
  - a Development build with the Profiler attached;
  - a `.data` recording loaded into the Profiler;
  - Play mode.

  It shares the finishing and saving step with `prof`
  (`ProfilerFrameReader.Finish` / `Save`), so the two cannot write different reports from the
  same frames.
- **A measurement toggle is not a shipped lever.** `renderers hide` switches renderers off in
  one frame — fine for asking what they cost, never acceptable as the fix. If lever L1 ships, a
  spindle leaves the culling population by FADING (continuity of existence), not by a toggle.

### 4.6 HUD console reference (F7 → console)

| Command | Does |
|---|---|
| `fps uncap` / `fps restore` | Remove / restore the vsync + target-frame-rate cap |
| `diag [label] [seconds] [m=A,B]` | Timed, tagged recording → JSON + TXT, with per-system marker timings, p50/p95/p99 and the run environment. `m=` adds markers to the default list |
| `renderers` | Census: enabled / disabled / visible renderers, by type, top 8 materials |
| `renderers hide <prefix>` / `renderers hide *<text>` / `renderers show` | Switch off every renderer whose material name starts with `<prefix>` — or, with a leading `*`, CONTAINS `<text>` — then exactly those back on. Use `*Spindle` for the spindle family: the lattice species wear `GyroidSpindleMaterial`, `AssemblySpindleMaterial` and `QuasicrystalSpindleMaterial`, which the prefix `Spindle` misses |
| `prismpath on\|off\|auto` | Instanced vs legacy prism rendering, live |
| `prisms <n>` / `prisms off` | Render-only stress cloud of `n` prism entities |
| `grid …` / `lab …` / `bench` | Lab scene only: real prism lattice, mixed populations, explosion benchmark |
| `freeze on` / `freeze off` | Hold ecology production in every cell (§4.5); released on scene change |
| `ab "<A>" "<B>" [seconds] [rounds]` | Counterbalanced A/B of two commands → one line of paired deltas + `ab_*.json`; `ab stop` cancels (§4.5) |
| `burst` | Are this project's Burst jobs running as Burst? Prints Burst's switches (`enabled`, `compilation`, `synchronous`, the job system's `jobCompiler`, `jobsDebugger`), an attached debugger and the Code Optimization, then runs a probe job in Assembly-CSharp (`Run()` and `Schedule()`) that reports `BURST` or `MANAGED` from how it actually executed, then Burst's own refusal from the editor log (jobs disabled, the reason, unresolved InternalCalls), since the last domain reload (`SKIM_RACE_AI.md` §8.0j, §8.0k) |
| **`capture [label] [seconds]`** | **The whole measurement in one file** (added 2026-10-14): runs `burst`, `diag`, `prof` (Editor), `renderers` and `cells` in sequence, collects the console's warnings/errors meanwhile, lists every enabled camera (an extra one - the Third Eye window, a preview - renders the world again and the reader warns), and writes `capture_<scene>_<label>_*.json` with the diag and prof JSON embedded verbatim. `capture copy` puts the file's text on the clipboard. **Capture** on the Scene view's Bug Hunt & Performance badge (2026-10-15, which replaced the Capture Performance window) is the same with a button, labels the capture by the scene's scenario and opens the Profiler for `prof`; after Play the editor sends the file by itself. `Tools/Build/perf_capture_report.py <file>` prints the judgement (validity first: Debug optimization, Deep Profile, frame cap, Burst, managed job time); two files = a before/after |
| `prof [label] [frames] [root=…] [sort=…] [min=…] [mingc=…] [depth=…] [top=…]` | Editor only: record + read back Profiler frames → averaged tree, top self time, top allocators, thread busy/wait, typical + spike frame in `prof_*.json`; `prof stop` cancels (§4.5). For a BUILD, use FrogletTools ▸ Performance ▸ Export Profiler Frames to JSON |

### 4.7 The test protocol (industry standard, adopted 2026-09-26)

This is how professional game teams measure performance, and how this project measures from
now on. Each rule is here because breaking it has produced a wrong number in this effort.

1. **Budget first.** The target is a **16.7 ms** frame (60 fps) with no frame over 50 ms. Plan
   to spend **≤ 14 ms** on average. The 2.7 ms of headroom is what absorbs a hitch, a hotter
   machine, a busier match. Per-system budgets are set once the build numbers exist (step 3).
   `diag`'s `markers` block is where they will be checked.
2. **Judge in a Development build on the reference PC. The Editor is for finding and ranking,
   never for judging.** The Editor adds its own loop (4–19 ms here) and runs scripts as
   debuggable Mono. The shipped game is a different program. The measurement build:
   - Development Build **ON**, Script Debugging **OFF**, Deep Profiling Support **OFF**;
   - Autoconnect Profiler **OFF** for `diag` runs, ON only for a Profiler session;
   - the **same scripting backend and quality level as the shipped game**;
   - one fixed window size (1920×1080).

   The project already sets **IL2CPP** for Standalone, and **Frame Timing Stats** is ON; `diag`'s
   CPU/GPU split needs the latter in a build.
3. **Control the conditions.** Plugged in, High Performance power plan, other apps closed,
   window focused, `fps uncap`. `diag` now RECORDS all of these in `environment`, so a number
   taken under the wrong conditions is visible afterwards.
4. **Warm up, then measure.** Load the scenario, wait its stated time (§3.1), then measure. The
   first seconds hold shader compiles, pool fills and growth; `diag` discards its first frames
   for the same reason.
5. **Repeat.**
   - Three `diag` runs per scenario, reloading the scenario between them. Quote the **median of
     the three**.
   - If the three differ by more than ~10%, the scenario is not stable. Find out why before
     quoting it.
6. **Quote the right statistic.**
   - **p50 (median) frame** for "how fast": the average is pulled up by hitches.
   - **p95 / p99** for "how smooth".
   - **CPU busy** for how much work the CPU did.
   - FPS alone hides all three.
7. **Attribute before you fix.**
   - Profile only the scenario that misses the target.
   - Hierarchy: `prof` in the Editor, or the exporter for a build.
   - Your own code gets named markers, then `diag` times them in the build.
   - Deep Profile finds WHERE, never HOW MUCH.
8. **Change one thing, prove it in the same state.**
   - `freeze on`, then `ab "<new>" "<old>" 20 6`.
   - Quote the delta with its ± standard error. A delta inside twice its error is noise.
9. **Record everything with its commit SHA** in §1. Keep the raw JSON files; a summary without
   its data cannot be re-checked.
10. **Guard what you won.** A nightly benchmark of S1–S6 with a budget per scenario (§3.4) stops
    a regression from shipping silently.

---

## 5. Rules this effort paid for

The locked conventions (slice + per-frame budget + atomic publish; Burst over packed arrays;
`/ecology` for anything ecological; profile first) are in `archive §2` and still hold. On top:

1. **A measurement names a number, never a cause.** Three lab tests and six reports narrowed
   the draw-call question; one Frame Debugger capture answered it. Reach for the tool that
   names the caller before building an experiment that infers one.
2. **A live HUD row is a sample.** 197.9 KB/frame (one frame) was really ~15.5 KB (average).
   Conclusions need `diag` averages.
3. **A derived verdict must only name the cause it can establish.** The HUD said "GPU-bound" on
   an idle GPU, "CPU-bound" on a 77%-idle frame, and "Capped" with no cap. Each is now fixed.
4. **A/B arms must share their state** (§4.3).
5. **A measurement describes one tree.** Stamp the SHA; re-take after a merge.
6. **Check the editor before blaming the code.** Burst off (~20×), Deep Profile (~10×), an
   unfocused window, FMOD Live Update — each has produced a phantom bottleneck here.
7. **A surface type belongs to the shader graph unless it allows material override — and opaque
   is only free for geometry that never fades.**

---

## 6. Which doc covers what

| Doc | Covers | State |
|---|---|---|
| **this doc** | State, history, plan, method | Current |
| `Docs/archive/PERFORMANCE_LOG_2026.md` | Every capture and root cause, 07-08 → 09-22 | Frozen evidence |
| `Docs/MEMORY_AUDIT.md` | What the game holds in RAM + on disk | Open (L7) |
| `Docs/PRISM_ECS_MIGRATION.md` | The instanced prism path; pure-entity plan (L4); device gate | Architecture |
| `Docs/PRISM_ANIMATION.md` | The clock-material law (LOCKED) | Architecture |
| `Docs/SPATIAL_INDEX.md` | `PrismSpatialIndex`, the one spatial index of prisms | Architecture |
| `Docs/PRISM_EXPLOSION_BENCHMARK.md` | The prism-death benchmark (`bench`) + its 08-25 results | Tool + results |
| `Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_TOOL.md` | Benchmark window, DiagnosticsHUD, `-csmbench`, Load Time Insights | Tool guide |
| `Docs/prompts/PERFORMANCE_NEXT_SESSION_PROMPT.md` | The prompt that starts the next perf session | Handoff |

Deleted 2026-09-22 (recover with `git log --diff-filter=D -- <path>`):
`Docs/PERFORMANCE_CAPTURE_RECIPES.md` (method folded into §4),
`Assets/_Scripts/Game/Prisms/PRISM_PERFORMANCE_AUDIT.md` (pre-ECS audit of an architecture that
no longer exists), `BENCHMARK_ARCHITECTURE.md` and `BENCHMARK_TEST_PROCEDURE.md` (duplicated
`BENCHMARK_TOOL.md`).
