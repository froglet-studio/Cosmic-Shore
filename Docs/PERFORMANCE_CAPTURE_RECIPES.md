# Performance Capture Recipes

The four captures that unblock the backlog in `PERFORMANCE_OPTIMIZATION.md` §4.
Written 2026-09-09 against `78a95259f` (`origin/Ys-merge-2026-09-07`).

**Why these four, and why now.** Nothing has been measured since 2026-07-15,
and *that* session's own first TODO — the Burst-ON verification capture — was
never taken. So every number in the log predates enabling Burst, and none has
been re-confirmed at the current population. The backlog's ordering is a
hypothesis until Capture A lands.

**Take Capture A first, and read one row before anything else.** If the boot
world is **GPU-bound** at 8 minutes, all of §4 Tier 1 is the wrong lever and
the answer is overdraw/shader work. The 2026-07-15 session suspected exactly
that (capture #4: 2.16M verts, transparent-prism overdraw) and never confirmed
it. One HUD row settles it.

---

## Pre-flight (every capture)

Get this wrong and the numbers are fiction.

| Setting | Value | Why |
|---|---|---|
| `Jobs ▸ Burst ▸ Enable Compilation` | **ON** | It was OFF for the whole 2026-07-15 session, silently running every job as managed IL at ~20× cost. |
| `Jobs ▸ Burst ▸ Safety Checks` | Off | |
| `Jobs ▸ Jobs Debugger` | Off | |
| Leak detection | Off | |
| **Deep Profile** | **OFF** | Inflates the frame ~10× and changes what you are measuring. |
| Console window | Closed | |
| Profiler | Standalone process | |

**The tell that Burst is actually on:** a job row reading
`ExecuteJobFunction.Invoke() [Invoke]` in the Hierarchy view is executing
managed. Burst-compiled rows show the job type name. Check this before
trusting a single number.

**Ground truth is a development build.** The standalone profiler only moves the
profiler UI out of process — the game still runs in the editor, so `EditorLoop`
(~4.4 ms) and `Profiler.FlushCounters` (~2.6 ms) persist. Where a row is
ambiguous, take the dev build.

**DiagnosticsHUD** is the fastest read and installs itself in every scene
(`[RuntimeInitializeOnLoadMethod]` + `DontDestroyOnLoad` — no scene wiring):

- **F7** — toggle the panel
- **F6** — advanced sections (Memory, Prism Path)
- **F5** — run diagnostic / reset

---

## Capture A — Menu_Main boot world *(take this first)*

**What it tests:** the claim §4 Tier 1 rests on. The **Lattice cell is the boot
world** (`CellConfigs[0]`, `Docs/ECOSYSTEM.md:6253`) and reaches **~63,360 prism
colliders + 1,080 always-on heart colliders at cap** (`:6172`, `:6236`) — but
the cost *accrues*: the cell opens with eight plants and climbs over ~7 minutes.
The doc's central contract, *every per-frame system must be O(near/active), not
O(population)*, has never been tested at that density.

**Before you start:** add `ECOSIM_PROBE` to Scripting Define Symbols. It is not
set in any `scriptingDefineSymbols` entry today, so `EcosystemPerfProbe`'s
`[ECOSIM]` line — the live collider count — is unreadable without it. Remove it
afterwards.

**Method.** Boot to Menu_Main. Sit still. Sample at **1, 4 and 8 minutes** so
the trend is visible, not just the endpoint.

**Read, in this order:**

1. **`Bound`** (F7 → F6) — the CPU/GPU verdict from `FrameBoundness.Classify`.
   *This is the row that decides the backlog.*
2. `Frame Time`, FPS.
3. `ShellContact.Query` — §4 Tier 1 #1's four sync scans. Expect this to grow
   with total population rather than with nearby shielded mass; that is the
   finding.
4. `LOD.Sweep` / `LOD.Drain`.
5. `Cell.VolumeSum`, `Cell.VolumeSum.Snapshot`.
6. `Prism.Create.*` (Visibility / SOAPRaise / SpatialBind) — fixed and confirmed
   at 11–25k, never re-measured at 63k.
7. `PrismDebris.*`.
8. Draw calls / batches / verts, GC per frame.
9. The `[ECOSIM]` line's live collider count.

**Expected if Burst is genuinely on:** `LodClassifyJob` ~0.1–0.3 ms;
`CellVolumeSumJob` ~0.15 ms on a *worker* row (it should not appear in the
main-thread hierarchy at all).

---

## Capture B — BenchmarkStressTest *(the regression baseline)*

**Launch:** Settings ▸ Run Benchmark. `BenchmarkSceneLauncher.LaunchBenchmark`
sets scene `BenchmarkStressTest`, `GameModes.WildlifeBlitz`,
`IsMultiplayerMode = false`, vessel Squirrel, and
`ConfigurePlayerCounts(1 + aiCount, 1)` where `aiCount` comes from the display
settings' AI crowd size.

**Two things to know about this scene, both re-derived on the merged tree:**

- It lives at `Assets/_Scenes/Singleplayer Scenes/BenchmarkStressTest.unity` and
  wires `SinglePlayerWildlifeBlitzController`. A duplicate copy at the old
  Multiplayer path (wiring the retired `SandboxBenchmarkController`) was deleted
  on 2026-09-09 — it shared a GUID with this one, so Unity picked between them
  arbitrarily. If you see two, the fix did not land.
- `BenchmarkSceneLauncher`'s class docstring still names
  `SandboxBenchmarkController`. It is stale; that class now has zero scene
  references. Harmless for the capture — **every benchmark tool self-installs**
  — but do not go looking for it in the scene.

**Headless variant:** a dev build with `-csmbench` runs the same pass and writes
JSON, with cross-commit diffing via `BenchmarkComparison` / `MetricDeltaTests`.

**This run becomes the regression baseline.** Record the commit SHA with it, or
the diffing has nothing to compare against.

---

## Capture C — one live mode

Pick by what you want to learn:

- **Rampage** or **Wildlife Liberation** — documented "very heavy collider
  budget". The straightforward density read.
- **Scarab Scramble** — the one that exercises §4 Tier 1 #4.
  `AstroLeagueBall.ProcessPrismInteractions` runs up to 8 `QuerySphere` calls
  per ball per tick on every peer, and the cell allows 4 loose balls
  (`cellBallLimit`). The file carries **zero markers**, so this needs
  instrumentation before it will show up as anything but unattributed
  main-thread time.
- **Hijack** — the only mode with a phase-locked AI census spike (§4 Tier 1 #5).
  Watch for a periodic ~3 s hitch rather than a steady cost; `nextRetarget`
  starts at `0f` for every AI, so they all re-census on the same frame.

---

## Capture D — load time

`LoadInsights` is hooked on every launch (call sites throughout
`ServerPlayerVesselInitializer`, measuring `ScriptedDelay`, `Netcode`, vessel
instantiate/inject/spawn) and, per the docs, **its output has never been read**.

FrogletTools ▸ Performance Benchmark ▸ **Load Time Insights**.

The scripted delays are the interesting rows — `preSpawnDelayMs` and
`postSpawnDelayMs` are 200 ms each, per player, by design.

---

## Recording results

Put numbers back into `PERFORMANCE_OPTIMIZATION.md`:

- A capture that **confirms** a backlog item → note the measured cost on that
  item in §4 and start the fix.
- A capture that **refutes** one → say so explicitly in §4 and re-order. A
  finding that survives only because nobody measured it is worse than no
  finding.
- Either way, add a §6 changelog row with the date, the commit, and what the
  capture actually showed — including the `Bound` verdict, which is the one
  number every future session needs first.
