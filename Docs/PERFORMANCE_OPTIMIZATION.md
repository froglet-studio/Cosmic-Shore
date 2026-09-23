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
| **Every number below** | an **Editor** number taken on the PRE-merge tree (base `1f160508f`), unless stated. None is a ship number, and none has been re-taken since the merge. |

---

## 1. Where we are

**The prism renderer is solved. The cost has moved to everything built AROUND prisms.**

The instanced (Entities Graphics / BRG) prism path draws **103,823 prisms at 4.8 ms of CPU**,
**12.9× cheaper** than the old one-GameObject-per-prism path (61.9 ms), and in the boot world
**8,436 prisms render in 23 opaque draw calls**. Draw calls, materials and prism count are not
where the frame goes any more.

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

Four things are open, and nothing else is proven to matter yet:

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
2026-09-23**; nothing in §1 has been re-measured yet.

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

### 3.3 Step 2 — the levers we already know about

Ranked by the evidence we had **before** the merge. Each needs Step 1 to confirm it is big in a
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
| **Where does the GC come from?** | Profiler Hierarchy, sort by **GC Alloc** | Names the caller |
| **What is issuing draws?** | Frame Debugger | Names the shader / object |
| **Did my change help?** | Two `diag <label> 15` reports of the **same state**, or Benchmark window **Compare** | Averages, not samples |
| **Is it GPU-bound?** | HUD `Bound` row, after `fps uncap` | |
| **Prism-only questions** | Lab scene `PrismGridExplosionTest` (`grid`, `lab mix …`) | Fixed population, no Bootstrap |
| **The real number** | Development build, `-csmbench` | The only number to judge against the target |

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
- **`diag <label> <seconds>`** writes `Documents/CosmicShore Diagnostics/diag_*.json` and a
  `.txt` with averages (`avgGcKbPerFrame`, `avgDraws`, CPU/GPU, `prismPath`, and the renderer
  census taken after sampling).
- A Profiler Hierarchy screenshot is fine: it is a tree, and there is no text export.

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
- **A measurement toggle is not a shipped lever.** `renderers hide` switches renderers off in
  one frame — fine for asking what they cost, never acceptable as the fix. If lever L1 ships, a
  spindle leaves the culling population by FADING (continuity of existence), not by a toggle.

### 4.6 HUD console reference (F7 → console)

| Command | Does |
|---|---|
| `fps uncap` / `fps restore` | Remove / restore the vsync + target-frame-rate cap |
| `diag [label] [seconds]` | Timed, tagged recording → JSON + TXT |
| `renderers` | Census: enabled / disabled / visible renderers, by type, top 8 materials |
| `renderers hide <prefix>` / `renderers show` | Switch off every renderer on a material named `<prefix>*`, then exactly those back on |
| `prismpath on\|off\|auto` | Instanced vs legacy prism rendering, live |
| `prisms <n>` / `prisms off` | Render-only stress cloud of `n` prism entities |
| `grid …` / `lab …` / `bench` | Lab scene only: real prism lattice, mixed populations, explosion benchmark |
| `freeze on` / `freeze off` | Hold ecology production in every cell (§4.5); released on scene change |
| `ab "<A>" "<B>" [seconds] [rounds]` | Counterbalanced A/B of two commands → one line of paired deltas + `ab_*.json`; `ab stop` cancels (§4.5) |

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
