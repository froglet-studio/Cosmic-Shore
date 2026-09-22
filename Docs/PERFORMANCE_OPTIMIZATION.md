# Performance — State, History & Plan

**Read this before any performance work.** It is deliberately short: where the game stands,
what was learned since the effort started, what is left, and how to measure without fooling
yourself. The raw evidence behind every claim — captures, tables, root-cause write-ups — is in
the frozen log, **`Docs/archive/PERFORMANCE_LOG_2026.md`**, and code comments cite its section
numbers (`§0.8`, `§0.11.6`, `Task 10`), which stay valid there.

| | |
|---|---|
| **Rewritten** | 2026-09-22, on branch `claude/bold-fermi-54nlts` @ `561700737` |
| **That branch's base** | `1f160508f` (bleeding-edge, 2026-09-14) |
| **bleeding-edge now** | `ee2ad320f` — **1,390 files ahead of that base, and it changed the boot world** (§1.1) |
| **Every number below** | an **Editor** number on this branch's tree, unless stated. None is a ship number. |

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

### 1.1 ⚠ bleeding-edge changed the boot world — re-baseline before anything else

PR #892 (merged 2026-09-20) made **Garland** the boot world (`SpawnableGarland`,
`Docs/ECOSYSTEM.md §48` on bleeding-edge): an authored cell of **4,259 prisms, ~8,147 when
mature, 37 heart colliders**, composed for the far menu camera. **Lattice** — the cell every
boot-world number in this doc and the archive describes — is now **opt-in through the Cell
Selector**.

So the default home screen is probably far cheaper than anything measured here, and the
"boot world" problem is now a "heaviest opt-in cell" problem. The findings still hold as facts
about Lattice and about GameObjects hung off prisms; **their priority does not** until the
scenario set (§3.1) is re-measured on the merged tree. bleeding-edge also changed `Spindle.cs`
(+121: sway constants shared with health prisms), `PrismRenderService.cs` (+44) and
`Flora.cs` (+80) — the exact code under lead #1.

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

### 3.1 Step 0 — merge, then set a target and a scenario set

1. **Merge bleeding-edge into the perf branch** and get it compiling. Nothing measured on the
   old base describes what players run.
2. **Write down the target.** Proposal: **60 fps in a Development build on the team's reference
   PC**, with no frame over 50 ms in steady play. (The editor inflates frame time ~2–3×; an
   editor number is for *comparing*, a build number is for *judging*.)
3. **Fix the scenario set** — the same five every time, so numbers are comparable across weeks:

   | # | Scenario | Why it is on the list |
   |---|---|---|
   | S1 | Menu_Main, **Garland** (default), 4 min | What every player sees first |
   | S2 | Menu_Main, **Lattice** via Cell Selector, 8 min | Heaviest opt-in world; everything measured so far |
   | S3 | **Rampage intensity 1** | Heaviest arcade cell (~49k flora prisms); **never profiled** |
   | S4 | **Scurry intensity 4** (Atlantis, ~69k prisms) | Heaviest authored environment |
   | S5 | **Wildlife Liberation** | Heaviest fauna (~1.2k creatures) |

   (SkimRace with 3 players was the original "single-digit fps" report; add it as S6 if it
   still reproduces after the merge.)

### 3.2 Step 1 — attribute each scenario once (no A/B needed)

One Profiler frame per scenario, **Hierarchy view expanded to the leaves**, plus the
**Timeline view** for the render block. This names the cost directly. Record the top 10 rows
per scenario here. Two questions this answers that no A/B can:

- **What are the worker jobs the main thread waits on?** Timeline shows them by name during
  the `Idle`. If they are `CullScriptable` / render-queue jobs, lead #1 (renderer count) is
  confirmed without any hide test.
- **What is the other ~17 ms of `UpdateScene`?** Expand `ScriptRunDelayedDynamicFrameRate`
  (coroutines), `ScriptRunBehaviourLateUpdate`, `DirectorUpdate`, physics.

Also sort the Hierarchy by **GC Alloc** once per scenario: that names the 154.5 KB/frame caller.

### 3.3 Step 2 — the levers we already know about

Ranked by evidence. Each needs Step 1 to confirm it is big in a scenario that misses the target.

| # | Lever | Evidence | Size |
|---|---|---|---|
| L1 | **Spindles off GameObject renderers** — draw them through Entities Graphics like prisms, or merge a plant's spindles into one mesh | ~40k enabled spindle renderers vs 4.6k visible; render-job wait 14 ms; hide test suggests ~4 ms | Large, structural. Must keep the per-spindle wither fade (continuity law) |
| L2 | **Pool prism + spindle pairs** for growth, and route plant **births** through the budgeted spawn queue | `Instantiate` 3.6 ms / 720 µs per growth step; birth spike ~1.2 ms (`archive §0.8`, §4 Tier 0a) | Medium. Ecology change — `/ecology` protocol |
| L3 | **Find the 154.5 KB/frame gameplay GC** and the multi-MB spike frames | Profiler, `EditorLoop` 0 B, all under `UpdateScene` | Unknown until attributed |
| L4 | **Pure-entity prisms** (no GameObject at all for bulk mass) | 28,544 disabled prism GameObjects still exist; `Docs/PRISM_ECS_MIGRATION.md` Checkpoint D (designed, not built) | Large, structural, post-launch unless a scenario demands it |
| L5 | Cheap, known-wrong code, no capture needed | `HijackController` AIs all re-plan on the same frame (`nextRetarget = 0f`); `AstroLeagueBall` `List.Contains` in a hot loop + zero markers; 4 full-population sync scans in `PrismSpatialIndex` with no "anything shielded?" early-out; `ConnectingPanelController` rebuilds strings every frame; `Boid.cs:627` allocating `OverlapSphere` | Small each; list in `archive §4 Tier 1–2` |
| L6 | Snow shard count (`SnowChanger.shardDistance` 120 → 200 = 4,189 → 905 GameObjects) | Renderer census | Small; a visual-density call for a human |
| L7 | Memory: music `DecompressOnLoad` (182 MB resident), texture streaming off | `Docs/MEMORY_AUDIT.md` | Load time + RAM, not frame time |

### 3.4 Step 3 — a performance gate

CI runs no Unity job (`UNITY_RUNNER_LABEL` unset), so nothing stops a regression. The pieces
exist (`-csmbench` dev-build self-runner, `BenchmarkComparison`, `MetricDeltaTests`). Once the
scenario set exists, a nightly `-csmbench` on S1–S5 with a budget per scenario is the gate.

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
3. **Freeze or interleave** a growing world: pause growth first, or take A → B → A → B in quick
   succession and compare B against the average of the As.

### 4.4 Send text, not screenshots

- **Benchmark window → Runtime Capture → Copy error log** puts stats, spikes and the script
  methods behind each spike on the clipboard as text.
- **`diag <label> <seconds>`** writes `Documents/CosmicShore Diagnostics/diag_*.json` and a
  `.txt` with averages (`avgGcKbPerFrame`, `avgDraws`, CPU/GPU, `prismPath`, and the renderer
  census taken after sampling).
- A Profiler Hierarchy screenshot is fine: it is a tree, and there is no text export.

### 4.5 Tooling the next session should add (small)

- **`freeze on|off`** — hold the world still for an A/B: pause flora growth / reproduction and
  fauna spawning. It pauses *production*, which the ecology rules allow ("not creating mass is
  allowed"). It must remove nothing.
- **`ab "<cmd A>" "<cmd B>" <seconds> <rounds>`** — run A and B alternately, average each, print
  **one line** such as `CPU busy B−A = −3.8 ms ±0.4 (3 rounds)`. This replaces the human timing
  the arms by hand.

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
