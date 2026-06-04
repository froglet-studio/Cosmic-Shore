# Density Partitioning — Handoff (mid-iteration)

**Branch:** `claude/audit-density-partitioning-2EvgR`
**Head commit:** `e7e4e56` (push branch to see latest)
**Companion docs:** `Docs/DENSITY_PARTITIONING_AUDIT.md` (full history + benchmark + temporal sim findings)

This document is the fast-start brief for whoever picks the work up. The branch
covers density partitioning (Phase 1 + Phase 2 landed), flora behavior fixes,
trail-mass cell registration, and a HUD gauge that visualizes the ecology in
real time. Two issues remain open at this handoff — both centered on the menu
cell never reaching `Quiet` phase, which means the spawn-cycle ring stays
static and no periodic fauna spawn in Menu_Main freestyle.

---

## 1. What's landed on this branch (in shipping order)

| Commit (head→base of this work) | What it does |
|---|---|
| `e7e4e56` | Spawn-cycle ring on the HUD hex; switched domain colors to `TrailHighlightColor`; aggressive host-button face hide |
| `a8ecae8` | Theme colors via `gameData.ThemeManagerData.ColorSet`; readout reparented to canvas root |
| `5ff3340` | Hexagonal gauge geometry — pointy-top, 1/3 per domain, radial fill |
| `b8039a0` | Zero-authoring indicator (self-constructs wedges + readout) |
| `1a9768a` | Diagnostic readout (`J:n R:n G:n` / `total/rabid Phase` / `±rate/s`); widened Blob phase bands |
| `962ce8a` | First indicator pass (circle Radial360, since replaced) |
| `52e4f4a` | Flora regrowth + dispersed planting + Frozen phase gate on BranchingFlora |
| `809572d` | Trail prisms register with cell density grids (the missing CellControlManager-deprecation step) |
| `9df956f` | Production grid Phase 2: 75m adaptive voxels, voxel mean-shift, result caching |
| `2bfeb89` | Merge of bleeding-edge (61 commits) into this branch — no conflicts |
| `c058663` | Production grid Phase 1: sized to cell, smoothing + sub-voxel interp, HealthBlockTracker ordering |
| `a9aca25` | Density partition benchmark + audit doc |

Everything before `c058663` is benchmark/audit tooling. From `c058663` forward
is production code. The branch is pushed and conflict-free with bleeding-edge.

---

## 2. What's working

1. **Phase 1 (density grid) — `c058663`.** `BlockDensityGrid` is sized to the
   owning cell, runs smoothing + sub-voxel interp, and `HealthBlockTracker.Add`
   has the correct domain ordering (§2.3.1 in the audit). Geometric benchmark
   confirms ~28m median error at cell scale vs ~100m for the shipped argmax.

2. **Phase 2 (density grid) — `9df956f`.** Voxel size is a physical constant
   (75m, half the smoothing kernel — Nyquist), resolution adapts per cell to
   [9, 33] points/axis. `FindDensestRegionJob` adds 5-iteration voxel mean-shift.
   `BlockDensityGrid` has a result cache (dirty flag + 0.25s min recompute) so
   N fauna querying the same grid trigger at most 4 job runs/sec instead of N.
   Counts are now `ushort` (was `byte`; overflowed at 256+ on hot voxels).

3. **Trail-prism cell registration — `809572d`.** The single biggest behavior
   fix on this branch. The deprecated `CellControlManager` registration in
   `Prism.cs` was commented out and never replaced, so the cell's per-domain
   grids contained ONLY flora — fauna's anti-domain "densest" query was always
   surgically targeting flora because flora positions were the only signal in
   the grid. Now `Prism.RegisterWithCell()` / `UnregisterFromCell()` wire into
   the full pooled lifecycle (spawn, explode, restore, pool return, steal,
   destroy) via the new static `Cell.FindCellContaining(position)` registry.

4. **Flora behavior — `52e4f4a`.** Both `AssembledFlora` and `BranchingFlora`
   now: (a) use a LIVE prism budget instead of a lifetime spawn counter — a
   grazed flora regrows; (b) re-sprout branches when all active branches are
   exhausted or consumed (random-sampled survivors); (c) respect
   `Cell.FloraGrowingEnabled` (the previously orphaned phase gate);
   (d) plant within `0.6 × MembraneRadius` instead of huddling at 75-200m
   around the crystal. Also: `CellLifeSpawnerBase.PickRandomDomain` returns
   only `{Jade, Ruby, Gold}` — never `Blue` (Blue flora counted as opposing
   mass for every anti-domain query, pulling every school to the same place).

5. **HUD volume gauge.** Hexagonal pause-button face restored per spec:
   pointy-top hexagon, Jade top / Ruby lower-left / Gold lower-right, each
   sector always full angular width with radial fill toward the centre as
   that domain's mass approaches `Cell.RabidEnterThreshold`. Centre hexagon
   tinted by the dominant domain. Colors from
   `gameData.ThemeManagerData.ColorSet.TrailHighlightColor`.

6. **Validation tooling — `a9aca25` and on.** Geometric benchmark
   (`DensityPartitionBenchmarkRunner`) and temporal ecology sim
   (`DensityPartitionTemporalSimRunner`) both ship with a `ProductionV2`
   variant that mirrors exactly what BlockDensityGrid now does. Re-running
   either tool re-validates production behavior after future changes.

---

## 3. What's broken / open

### 3.1 Spawn-cycle ring is static; no fauna spawn in Menu_Main freestyle

**Symptom:** the HUD's outer spawn-cycle ring never advances; flora visibly
grow but no fauna appear.

**Actual root cause (the IntensityWise hypothesis below was wrong).** The
shared `Cell.prefab` — used by Menu_Main **and 12 other scenes** — has
`cellTypeChoiceOptions = Random`, so every scene runs `RandomLifeSpawner`,
**not** `IntensityWiseLifeSpawner`. Nothing in the project sets `IntensityWise`
(`grep "cellTypeChoiceOptions: 1"` → zero hits). So the whole phase-gated
ecology + ring telemetry the audit built lived on `IntensityWiseLifeSpawner`,
which no scene actually runs. Two independent consequences in Menu_Main:

1. **No fauna.** `RandomLifeSpawner` gated fauna on
   `GetControllingVolume(gameData) > FaunaSpawnVolumeThreshold` — the
   *controlling team's scored `VolumeRemaining`*, which is ~0 in Menu_Main
   (no game is scored there). Flora spawned because their gate is the
   opposite direction (`volume < FloraSpawnVolumeCeiling`). The phase
   threshold (`Quiet = 100`) was never on this path, so lowering it does
   nothing.
2. **Dead ring.** `RandomLifeSpawner` never called `RecordFaunaSpawn()` —
   only `IntensityWiseLifeSpawner` did — so `_lastFaunaSpawnTime` stayed
   `-1` and `FaunaSpawnCycleFraction` returned 0 forever.

**Fix applied — retrofit `RandomLifeSpawner` onto the phase model** (chosen
over flipping cells to `IntensityWise` so no prefab/scene rewiring is needed;
the spawner every scene already runs becomes the one phase-driven model):

- Flora plant on `host.FloraPlantingEnabled` (Phase < Settled) instead of the
  scored-volume ceiling.
- Fauna spawn on `host.FaunaSpawningEnabled` (Phase >= Quiet) instead of the
  scored-volume threshold.
- `host.RecordFaunaSpawn()` is seeded before the continuous loop and called
  after each spawn; the interval is aggression-scaled (`ScaleFaunaInterval`)
  so `Cell.CurrentFaunaSpawnPeriod` — what the ring reads — matches the real
  cadence.

Because flora now plant up to Settled (300) and every flora/trail prism
increments `LiveBlockCount` via `Cell.AddBlock`, the menu cell should climb
through Quiet (100) on its own — so lowering `QuietEnter` is no longer
required to get the first fauna. If the readout's `total` stalls below 100,
*then* lower `QuietEnter`/`QuietExit` (keep exit < enter).

**Validate (one run):** play Menu_Main freestyle, read the indicator text:
`total` should climb (flora), cross 100 → `Phase` reaches `Quiet` → fauna
appear and the ring starts sweeping; `±rate/s` shows the grow/consume swing.
There should be no `[NO CFG]` tag (the menu cell does get Blob Cell Config).

> **In-game note:** the flora gate also changed in gameplay scenes (now
> Phase < Settled, not scored-volume). Watch flora density when testing a
> gameplay scene — if flora vanish too early under heavy trail, raise that
> biome's `SettledEnter` rather than reverting the gate.

### 3.2 In-game ecology validation still pending

The temporal sim (`DensityPartitionTemporalSimRunner`) showed
ProductionV2 (75m voxels + voxel mean-shift) eats more than MID/OLD in
the sim's 300s window — but the sim's swarm model is approximate (orbit
sphere + sub-goal resampling vs production's actual boid separation).
The audit §7.4 notes this: only in-game observation can confirm
oscillation behavior. The diagnostic readout under the HUD makes this
observation easy — watch the `±rate/s` line over several minutes of play:

- Steady `+N/s` while flora grow → climbing toward Frozen
- `-N/s` after fauna swarm forms → consumption flipping the cycle
- Near-zero at Frozen → pinned (the static-ecology failure mode)

This is blocked on §3.1 — fauna have to actually spawn before we can
observe consumption.

### 3.3 Audit doc lag

`Docs/DENSITY_PARTITIONING_AUDIT.md §7.3` lists Phase 2 as landed (correct)
but doesn't yet cover the ecology-side fixes (`52e4f4a`, `809572d`) or
the HUD work. Add a §8 covering those, or fold the HUD work into a new
`Docs/ECOLOGY_HUD.md` and link it from §7.

---

## 4. File-by-file map of what's on this branch

### Production code (modified)

| File | Why |
|---|---|
| `Assets/_Scripts/Controller/Managers/BlockDensityGrid.cs` | Phase 1+2 grid: adaptive resolution, smoothing + interp + voxel mean-shift, result cache, ushort counts. |
| `Assets/_Scripts/Controller/Environment/Cell.cs` | Membrane-sized grid setup, `FindCellContaining` / `FindNearestActiveCell` registry, `trackedBlocks` as `Dictionary<Prism,Domains>` (domain snapshot for stable Remove), `NotifyBlockDomainChanged`, `GetDomainBlockCount`, `RabidEnterThreshold`, `HasConfigAssigned`, fauna spawn cycle telemetry (`RecordFaunaSpawn`, `CurrentFaunaSpawnPeriod`, `FaunaSpawnCycleFraction`). |
| `Assets/_Scripts/Controller/Vessel/Prism.cs` | `RegisterWithCell` / `UnregisterFromCell` wired into the full pooled lifecycle; `HandleTeamChangedForCell` re-files on steal; `OnDisable`/`OnDestroy` cleanup. |
| `Assets/_Scripts/Controller/Environment/FloraAndFauna/HealthBlockTracker.cs` | Domain ordering fix (`hp.ChangeTeam(domain)` before `cell.AddBlock`). |
| `Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs` | Live budget, branch reseeding (random-sampled survivors), `FloraGrowingEnabled` gate, dispersed `Plant()`. |
| `Assets/_Scripts/Controller/Environment/FloraAndFauna/BranchingFlora.cs` | Same set as AssembledFlora. |
| `Assets/_Scripts/Controller/Environment/FloraAndFauna/Flora.cs` | `plantRadiusCellFraction` + `ResolvePlantRadius` shared base. |
| `Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs` | `PickRandomDomain` returns `{Jade,Ruby,Gold}` only — never Blue. |
| `Assets/_Scripts/Controller/Environment/IntensityWiseLifeSpawner.cs` | `host.RecordFaunaSpawn()` seeded before loop start and called after each `TrySpawnFauna`. |

### Production assets (modified)

| File | Why |
|---|---|
| `Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Cell Config.asset` | Phase bands widened (Quiet 100/50, Settled 300/180, Restless 500/300, Frozen 700/420, Rabid 900/540) so oscillation is visible. **Suspect**: Quiet=100 too high for Menu_Main — see §3.1. |
| `Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Cell Spawn Profile.asset` | `BaseFaunaSpawnTime: 30 → 12` so predator population builds within a session. |

### New UI

| File | Why |
|---|---|
| `Assets/_Scripts/UI/DomainVolumeIndicator.cs` | Controller. Resolves the cell + colors + dominant + spawn cycle, lerps the targets, pushes state to the graphic. Zero-authoring: auto-attaches via `MenuMiniGameHUD`, creates a full-rect child graphic and a canvas-root diagnostic readout. |
| `Assets/_Scripts/UI/DomainVolumeHexGraphic.cs` | `MaskableGraphic` (the `ObjectiveArrowGraphic` idiom). Renders: faint outer boundary hex, three domain bands (radial fill per sector), centre hex tinted by dominant, spawn-cycle annulus ring outside the hex sweeping clockwise from the top. |
| `Assets/_Scripts/UI/MenuMiniGameHUD.cs` | `EnsureDomainVolumeIndicator()` self-attaches the indicator to the volume/pause button and hands over the injected `GameDataSO`. |

### Tooling (added/modified)

| File | Why |
|---|---|
| `Assets/_Scripts/Utility/Tools/DensityPartitionBenchmark/*` | Geometric benchmark, temporal sim, algorithms — both tools ship a `ProductionV2` variant that mirrors current production exactly. |
| `Docs/DENSITY_PARTITIONING_AUDIT.md` | First-principles audit, benchmark findings, temporal sim findings. |

---

## 5. How to validate from a clean clone

```
git fetch origin claude/audit-density-partitioning-2EvgR
git checkout claude/audit-density-partitioning-2EvgR
# Unity → open Menu_Main → enter freestyle (drag the menu crystal)
```

You should see:

1. The pause button is now a hexagonal volume gauge (Jade top / Ruby lower-left
   / Gold lower-right) with a thin ring around the outside.
2. A diagnostic readout below the button: `J:n R:n G:n` per-domain counts,
   `total/rabid Phase` (with `[NO CFG]` tag if the cell never initialized),
   and `±rate/s` net prism rate.
3. Flora visibly growing and regrowing across the cell.
4. **Expected to be broken (§3.1):** the spawn-cycle ring never advances, no
   fauna spawn.

Run the geometric benchmark at any time via FrogletTools → Density tab to
re-confirm the grid's accuracy hasn't regressed.

---

## 6. Recommended next steps (priority order)

1. **Diagnose §3.1.** Lower `Blob Cell Config.QuietEnter` to 30, re-test
   Menu_Main, see if fauna spawn and the ring starts moving. If yes, pick a
   tuning that hits Quiet within ~30s of menu entry. If no, the bug is in
   the cycle-tracking code path itself.
2. **Validate the ecology emergent loop** once fauna spawn. Watch the
   `±rate/s` readout for primary oscillation (flora vs fauna swings).
3. **Document §3.3.** Audit doc §8 covering ecology + HUD.
4. **Decide on PR shape.** The whole branch is ready in principle —
   c058663 (Phase 1) and 9df956f (Phase 2) are tested and validated,
   809572d is the single biggest correctness fix on the branch, and the
   ecology fixes are pure improvements. Either PR the whole branch into
   bleeding-edge as one feature drop, or split: density grid +
   trail-mass registration first, ecology + HUD second.

---

## 7. Useful context for the new thread

- **Three fundamentals doing most of the work:** Mass (per-domain prism counts),
  Cells (where Mass is computed), Flora & Fauna (consumers of Mass that read the
  density grid). Audit §7.3 and CLAUDE.md "Design Philosophy" cover this.
- **The static spatial registry on `Cell`** (`ActiveCells` + `FindCellContaining`)
  is the canonical "which cell contains this position" lookup. Use it from any
  pooled-prefab-spawned object that needs to know its cell at runtime — Prism
  is the first user, but the same pattern fits future projectiles / abilities.
- **`gameData.ThemeManagerData.ColorSet`** is the canonical UI domain-color
  source (`MultiplayerHUD`, this HUD). `TrailHighlightColor` is the bright
  identity hue — `OutsideBlockColor` is the dim outer block shell and reads
  as near-black/wrong for several domains.
- **The Frozen phase is the hysteresis backbone of the primary cycle.**
  `Cell.FloraGrowingEnabled = phase < Frozen` shuts flora off; if consumption
  brings the count below `FrozenExit`, flora resume. The wider the hysteresis
  band, the bigger the visible swing in count and in the volume gauge.
- **Stop hooking dead `CellControlManager` code.** The audit has the
  trail-mass registration replacement; future work should use
  `Cell.AddBlock` / `RemoveBlock` directly (via `FindCellContaining`)
  rather than reviving any `CellControlManager`-style singleton.
