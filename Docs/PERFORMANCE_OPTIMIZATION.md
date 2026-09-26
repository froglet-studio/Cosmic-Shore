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
| L1 | **Spindles off GameObject renderers** — draw them through Entities Graphics like prisms, or merge a plant's spindles into one mesh | ~40k enabled spindle renderers vs 4.6k visible (Lattice); render-job wait 14 ms; hide test suggests ~4 ms (confounded, §4.3) | **Still true, and wider.** `Spindle` is still one GameObject `Renderer` per limb; the +121 lines are sway constants handed to health prisms (`PrismSway`) and a `ResolveRenderedObject` fallback. The new Mandelbulb/Borromean families (Arboretum) grow one spindle per bond (`MandelbulbFlora._limb`), and some creatures carry spindles | Large, structural. Must keep the per-spindle wither fade (continuity law) |
| L2 | **Recycle flora prisms + spindles** instead of minting, and route plant **births** through the budgeted spawn queue | `Instantiate` 3.6 ms / 720 µs per growth step; birth spike ~1.2 ms (`archive §0.8`, §4 Tier 0a) | **Still true, sharper.** Every growth path takes its prism from `EnvironmentPrismPool.Get`, but that pool only **mints** during play: a consumed or exploded prism is `Destroy`ed and never returns. Spindles are a raw `Instantiate` (`LifeForm.AddSpindle`, `AssembledFlora.cs:571`, `PhyllotacticFlora.cs:466`, `BranchingFlora.cs:153`). Recycling can only cover churn (grazing + regrowth), not net growth | Medium. Ecology change — `/ecology` protocol |
| L3 | **Find the gameplay GC** and the multi-MB spike frames | 154.5 KB/frame in Lattice, all under `UpdateScene` | **Unknown** — a Lattice-only number; re-measure per scenario | Unknown until attributed |
| L4 | **Pure-entity prisms** (no GameObject at all for bulk mass) | 28,544 disabled prism GameObjects exist; `Docs/PRISM_ECS_MIGRATION.md` Checkpoint D | **Still true**: Checkpoint D is designed, not built | Large, structural, post-launch unless a scenario demands it |
| L5 | Cheap, known-wrong code, no capture needed | `archive §4 Tier 1–2` | **All still present**, lines moved: `HijackController` AIs re-plan on one frame (`nextRetarget = 0f`, `:217`/`:272`); `AstroLeagueBall._shieldPoppedThisVisit` is a `List` scanned by `Contains` (`:1214`, `:1236`); `PrismSpatialIndex`'s four `Schedule(_highWaterMark).Complete()` scans (`:2211`, `:2363`, `:2448`, `:2520`) with no "anything shielded?" early-out; `ConnectingPanelController.Update` rebuilds strings every frame (`:174–193`); `Boid.cs:627` allocating `OverlapSphere` | Small each |
| L6 | Snow shard count (`SnowChanger.shardDistance` 120 → 200 = 4,189 → 905 GameObjects) | Renderer census | **Still true** (`SnowChanger.prefab` 120) | Small; a visual-density call for a human |
| L7 | Memory: music `DecompressOnLoad` (182 MB resident), texture streaming off | `Docs/MEMORY_AUDIT.md` | **Still true** (`cosmic shore chill time 3.wav` `loadType: 0`, `quality: 1`) | Load time + RAM, not frame time |
| L8 | **Batch the creature body-prism re-sync** — one pass after all creatures move: `TransformAccessArray` read in Burst, one index pass, one bulk `LocalToWorld` write | §1.0 S5: `LightFauna.Update` 13.1 ms avg self, 374 calls | Code read at `070874533`: `Fauna.NotifyBodyPrismsMoved` → per prism `Prism.NotifyPositionChanged` (index + shell + `EntityManager.SetComponentData`) | Medium. Behaviour-neutral; must keep the mover contract's same-frame visibility |
| L9 | **The S5 gunfight continuations** — AI full-auto fire + per-round flight steps | §1.0 S5: 20.2 ms avg, 123 ms in the spike frame | Contents unknown; markers in `aaa1517fe` | Unknown until the marker capture |
| L10 | **The behaviour-tick neighbour scan** | §1.0 S5: 12.7 ms avg, ~0.85 ms per tick | Goal query is cached, so the cost is the `QuerySphere` + per-candidate loop | Must keep the ONE diet predicate (`Fauna.IsPreyForMe`) — no Burst copy of the rule |
| L11 | **Collider-LOD tick hitch** — slice `RunSweep`'s transitions across frames | §1.0 S4: `LOD.Sweep` 15.9 ms on one frame in five | `PrismColliderLodManager` restores are unbudgeted by design (safety); which half costs is unknown | Small–medium; must never delay a restore near a focus |

**New since the base, not a lever yet:** every prism render prototype gained four `float3` sway
overrides (`PrismSwaySpan*`/`Axis`/`Timing`, 48 B per entity) for the health-prism sway
(`ECOSYSTEM.md §47`). That is more per-instance data to upload per prism; nothing measured says it
matters. Watch the GPU row in S2/S6.

### 3.4 Step 3 — a performance gate

CI runs no Unity job (`UNITY_RUNNER_LABEL` unset), so nothing stops a regression. The pieces
exist (`-csmbench` dev-build self-runner, `BenchmarkComparison`, `MetricDeltaTests`). Once the
scenario set exists, a nightly `-csmbench` on S1–S6 with a budget per scenario is the gate.

### 3.5 Do NOT redo these — measured dead

| Hypothesis | Why it is dead | Evidence |
|---|---|---|
| Prism draw calls are the cost | A draw costs 0.042 µs CPU; all draw submission is ~0.2 ms | `archive §0.11`, `§0.11.6` |
| Re-key `PrismRenderService.GetPrototype` by mesh + material | Would move ~10% of draws; prisms are 23 draws | `§0.11`, `§0.11.4` |
| Chunk spatial locality | 320× locality change moved draws 0.05% | `§0.11.1` |
| Convert `SnowMaterial` / `SpindleMaterial` to opaque | Snow's surface is baked in its graph; spindles would pop when withering | `§0.11.5` |
| GC comes from Entities Graphics / prisms / the HUD / coroutines | Legacy path allocates more; empty scene allocates more; hidden overlay same; arithmetic 100× short | `§0.11`–`§0.11.3`, `§0.9` |
| The stress cloud inflated the boot-world numbers | Reproduced without it | `§0.11.4` |

---

## 4. How to measure — simply

The last month's biggest waste was not a wrong fix. It was measurements that looked like
answers and weren't: single-frame HUD reads, arms taken in different worlds, an unfocused
editor window, and screenshots re-typed by hand.

### 4.1 One question → one tool

| Question | Tool | Output |
|---|---|---|
| **What is costing the frame?** | **Unity Profiler**, Hierarchy + Timeline, Deep Profile **off** | Names the caller. **Always start here.** |
| **Where does the GC come from?** | Profiler Hierarchy, sort by **GC Alloc** — or `prof <label>` (`topGc` ranks by SELF allocation) | Names the caller |
| **The Hierarchy as text** | `prof <label> [frames] [root=…] [sort=…]` (§4.5) | `prof_*.json`: averaged tree, top self, top GC, threads, typical + spike frame |
| **What is issuing draws?** | Frame Debugger | Names the shader / object |
| **Did my change help?** | `freeze on` + `ab "<new>" "<old>" 20 6` in the **same state**; or two `diag <label> 15` reports | A delta with an error bar |
| **Which system costs what, in a BUILD?** | `diag <label> 15` in a Development build — it times ~23 named markers per frame with `ProfilerRecorder`, no Profiler attached (§4.5) | `markers` block: avg / p50 / p95 / max ms per system |
| **Is it GPU-bound?** | HUD `Bound` row, after `fps uncap` | |
| **Prism-only questions** | Lab scene `PrismGridExplosionTest` (`grid`, `lab mix …`) | Fixed population, no Bootstrap |
| **The real number** | A Development build, run the §4.7 way, `diag` per scenario | The only number to judge against the target |
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
    collider LOD, debris).
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
