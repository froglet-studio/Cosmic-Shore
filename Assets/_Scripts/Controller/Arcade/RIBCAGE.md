# Ribcage — "Peel the Cage" — Technical Documentation

> **Naming.** `GameModes.Ribcage = 39` is the code/data/enum identity and does not change. **"Peel
> the Cage"** is the player-facing `DisplayName` on `ArcadeGameRibcage.asset` — the same
> display-name-only split Tournament/"Maelstrom" uses. Do not rename the enum, the controller, the
> scene, or this file to chase the display name.

## Overview

Ribcage is the **Rhino-only cage race**. Domains race to be first to **destroy 2,000 hostile
prisms**. The arena is a **layered orange** of prism bone — concentric hollow shells you scrape
your way through — and the bone *is* the score, so breaking out and winning are the same act.

**One axis.** Destruction is the race: `HostilePrismsDestroyed`, the same platform stat Rampage
runs on and the same 2,000 target. Scoring mass is everything that is not your own team's laid
trail — the cage (environment mass, non-roster owner ⇒ hostile whatever colour it wears) and rival
trails. Your own and your teammates' trails never score, so there is no lay-and-smash farming loop.

**Intensity is how many rinds you have to peel.** One shell at intensity 1, four at intensity 4,
added *inward* from a fixed outer radius. This is not a controller feature: the Cell picks one
`CellConfigDataSO` per intensity (`CellTypeChoiceOptions.IntensityWise`), each pointing at a
`SpawnableRibcage` prefab variant with a different `shellCount`. See "Intensity" below.

- **The cage is the arena and the objective.** 5,471 / 9,902 / 13,316 / 15,690 prisms at one
  through four shells, outer radius **360**, shells at **360 / 280 / 200 / 120**. Each shell is
  twenty-six meridian ribs, thirteen latitude hoops, a woven cross-lattice, joints at every
  crossing, and two polar crowns.
- **The weave is deliberately OPEN.** The grille opening at the outer shell is ~**87u × 82u**
  (squareness 1.07) — you fly between the bones freely, and the gaps are what let you *see* the
  next rind waiting behind this one. Successive shells are rotated by a fraction of a rib spacing
  (`ShellLonOffsets = {0, 0.5, 0.25, 0.75}`), so no two of the four align and there is no free
  radial corridor straight through to the core.
- **Every bar is a ONE-hit prism.** All plain (`PrismKind.Plain`) except the danger traps. Nothing
  is `Shielded` and nothing is `SuperShielded` — a super-shielded prism is fully invulnerable
  (`Prism.Damage` returns early), so one in the cage would be permanently unbreakable mass and
  enough of them could put the target out of reach. Do not "upgrade" the bars.
- **182–484 of the bars are DANGER traps.** Now that the bars are plain, a danger bar is neither
  harder nor softer than its neighbours — it is **pure downside**. What it costs is contact: the
  standard danger-prism punishment (volume-independent full-stop slow, a 4 s all-element debuff,
  boost reset). So "just ram everything" stops being the answer; you have to read the bar before
  you commit.
- **No fauna.** The cell authors no species (see "The fauna removal" below).

**Key architectural facts:**

- **Scene**: `Assets/_Scenes/Multiplayer Scenes/MinigameRibcage.unity` (single unified scene,
  cloned from Rampage's skeleton — no separate singleplayer variant; solo play is a party of one
  + AI backfill)
- **GameMode enum**: `GameModes.Ribcage = 39`
- **Controller**: `RibcageController : MultiplayerDomainGamesController` — structural sibling of
  `RampageController` (1 round / 1 turn, `HasEndGame=false`, server winner detection in
  `OnTurnEndedCustom`, snapshot `SyncFinalScores_ClientRpc`), plus progress milestones and the AI
  cage-breakers
- **Scoring**: `RibcageScoringRuleSO` (`metric = ScoringMetric.PrismsDestroyed`; golf-timed like
  HexRace/Scurry/Rampage) — winning-domain players `Score = finish time`, losers the
  `GolfScoreSentinels` sentinel (displayed "N Bars Left")
- **Turn monitor**: `RibcagePrismTurnMonitor` — resolves the destruction target from
  `EndConditionOverridesSO.GetRibcagePrismTarget()` (default **2000**, FrogletTools ▸ Game Modes ▸
  End Game Conditions — never a per-scene field), syncs it via NetworkVariable →
  `GameDataSO.PrismTargetCount`
- **Domains**: `MinDomainsAllowed = 2` (like Joust), `MaxDomainsAllowed = 3`; players **2–4** with
  AI backfill
- **Vessels**: **Rhino only** (`ArcadeGameRibcage.Vessels` has one entry). `SO_ArcadeGame.Vessels`
  used to be only the UI's list of CHOICES — nothing validated the selection at launch, so a vessel
  picked in an earlier game persisted into a mode that does not allow it (a Dolphin flew Ribcage
  while its AI opponents correctly spawned Rhinos). `GameDataSO.SyncFromArcadeGame` now clamps
  `selectedVesselClass` into the game's allowed set, so a single-vessel mode cannot be entered in
  the wrong hull by ANY route — modal, rematch, or the Tournament chain.
- **Config**: `_SO_Assets/Games/ArcadeGameRibcage.asset` (registered in
  `GameLists/OrganicRematchGames.asset`, `ProgressionConfig.alwaysUnlockedModes`)

## The pipeline (zero bespoke tracking)

The stat was already plumbed platform-wide (Rampage runs on it); the mode only picks it and reads
it twice — once to end the turn, once to drive the milestones.

```
Rhino shatters a bar (one hit - plain prism)
  └─ Prism.Damage → SetupDestruction → onTrailBlockDestroyed.Raise(PrismStats{…})
              ▼
StatsManager.PrismDestroyed → HostilePrismsDestroyed++   (cage mass is non-roster ⇒ hostile;
                                                          your own team's trail is filtered out)
              ▼
ScoringMetrics.Read(stats, PrismsDestroyed) → SumByDomain
  ├─ MultiplayerDomainGamesController.SyncDomainSumsRoutine → HUD domain panels
  ├─ RibcagePrismTurnMonitor.CheckForEndOfTurn → rule.IsObjectiveReached   [server]
  ├─ RibcageController.SampleProgress → leader + milestone rungs           [server]
  └─ ElementalComebackSystem (source PrismsDestroyed) → trailing-team buff
              │  turn end
              ▼
RibcageController.OnTurnEndedCustom → AssignScores → SyncFinalScores_ClientRpc
```

## Intensity — the layered orange

The platform already has exactly one way for a cell to vary by intensity, and this mode uses it
rather than inventing a second:

```
Cell.AssignConfig                                     [Cell.cs]
  CellTypeChoiceOptions.IntensityWise
    → index = Clamp(gameData.SelectedIntensity - 1, 0, CellConfigs.Count - 1)
    → CellConfigs[index]   (Ribcage Cell Config 1..4, in that order)
        → EnvironmentPrefab = SpawnableRibcage{1..4}.prefab
             → SpawnableRibcage.shellCount = 1..4
        → PhaseThresholds   = THAT intensity's own measured baseline
```

Three properties of this arrangement are load-bearing:

1. **Shells are added INWARD**; the outer radius never moves. So `SpawnableRibcage.ShellRadius`
   stays a single constant, the AI's aim point needs no per-intensity case, the player spawn ring
   is unchanged, and the arena's outer silhouette is identical at every intensity.
2. **Each intensity needs its OWN `CellConfigDataSO`** because `PhaseThresholds` must ride its own
   baseline — a four-rind cage starts at ~15.7k prisms and a one-rind cage at ~5.5k, so one shared
   threshold block would put three of the four arenas in the wrong phase from frame one.
3. **`shellCount` is inside `BuildParameterHash`**, so the four variants share one script without
   sharing a generation cache.

Re-tune by editing `ribcage_budget.py` (geometry) and re-running `author_ribcage_assets.py`, which
imports the model directly — the thresholds cannot drift from the geometry behind a stale constant.

## The cage

`SpawnableRibcage : CellEnvironmentSpawnableBase`, outer radius **360**, shell gap **80**, seed 39,
deterministic per seed like every cell environment. Analytic budget
(`Tools/Build/ribcage_budget.py`; confirm with FrogletTools ▸ Ecology ▸ Measure Cell Environment
Baselines):

| shell | radius | ribs | hoops | lattice | joints | crowns | danger | total | volume |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 | 360 | 3,458 | 1,171 | 468 | 338 | 36 | 182 | **5,471** | 2,178,110 |
| 1 | 280 | 2,678 | 911 | 468 | 338 | 36 | 140 | **4,431** | 1,729,549 |
| 2 | 200 | 1,924 | 648 | 468 | 338 | 36 | 101 | **3,414** | 1,290,908 |
| 3 | 120 | 1,144 | 388 | 468 | 338 | 36 | 60 | **2,374** | 842,347 |

| intensity | shells | prisms | volume | danger |
|---|---:|---:|---:|---:|
| 1 | 1 | **5,471** | 2,178,110 | 182 |
| 2 | 2 | **9,902** | 3,907,660 | 323 |
| 3 | 3 | **13,316** | 5,198,568 | 424 |
| 4 | 4 | **15,690** | 6,040,915 | 484 |

Rib and hoop COUNTS are constant per shell while prism counts scale with circumference, so the
openings tighten toward the core — which is what an orange's inner layers actually do, and it means
the last rind is the hardest to slip through rather than the easiest.

**Collider-budget impact — read this before tuning anything else.** One box collider per prism, so
the cage *is* the collider count: 5,471 at intensity 1 rising to 15,690 at intensity 4, plus
nothing else (no fauna, no flora in this cell).

- Intensity 1 is **~3.6× the masterplan's ≤1,500 per-cell target** but comfortably **under
  Rampage's deliberate 10,000-prism arena gate** — and it is a **2.7× improvement** on the previous
  single dense shell (14,977).
- Intensity 4 is **~10×** the masterplan target and **~1.5× Rampage's gate**, i.e. about what the
  old single shell cost. That cost is now opt-in per match rather than paid by everyone.

The open weave is what bought this: the same prism budget spread over four readable rinds instead
of one solid sphere. Mitigations are the standing ones (collider-LOD by phase, no new physics
queries anywhere — scoring rides the StatsManager SOAP channel and the AI aims analytically).
**Measure on device before tuning.** The cheapest dial is `shellCount` (drop the top intensity),
then `RibCount`, then `HoopCount`, then `BarStep`. Re-run **both** Python tools after any change.

## Progress milestones

At a quarter and a half of the win target, the **leading** domain crosses a rung:
`RibcageController.SampleProgress` (server, every `progressSampleSeconds` = 0.5 s) →
`AnnounceMilestone_ClientRpc` → a `GameToastSituation` post plus `HapticController.PlayAlert()` on
every peer (~1.2 s of hard rattling — the game's **third** haptic feel, and the only thing that
fires it; see `Docs/HAPTICS.md`).

These are **pure feedback — they change no game state**, so a missed or late sample costs a toast,
never a rule. Rungs ride the leader's *own* progress rather than a cross-domain total so they land
at a fixed point in the race rather than at a point a busy lobby reaches several times faster. A
lead change after the first milestone posts `RibcageLeaderChanged`.

Toast copy is still unauthored, so **right now the shake IS the milestone feedback**.

## Spawning outside the cage

Players start on the computed cell spawn ring (`CellSpawnFormation` — symmetric, all facing the
cell), NOT on authored transforms: the donor scene's four points sat at ±50, deep inside the cage,
so everyone started penned in.

The ring normally measures off the cell's nucleus radius, and Ribcage's cell deliberately has none
— so it would have collapsed to the cell centre, i.e. the same bug.
`ServerPlayerVesselInitializer.spawnRingRadiusFloor` (default 0 = every existing scene unchanged)
gives the ring a floor for exactly this case: a cell whose "core" is a structure rather than a
nucleus. Ribcage authors **576** — outside the 360u outer shell, well inside the 1200u membrane,
and far enough back to see the whole cage and line up a charge.

## AI cage-breakers

**Every AI station is OUTSIDE the shell. That is the whole fix.** `AIPilot` has no arrive-and-stop
behaviour — it steers at `_targetPosition` forever and simply flies through on arrival — so *any*
target inside the cage becomes a point the AI loops around from within. That is what "the AI just
stays inside" was, twice: first when stations sat *on* the shell, then again when a two-waypoint
approach/punch cycle put the punch waypoint at 0.55×R.

Now there is one station per strike, always at **1.3×R**. Stations walk a golden-angle spiral, so
successive stations are ~137° apart and the **chord between them passes close to the centre** — a
full crossing of the cage, which is what shatters bars, and with four rinds a crossing now cuts
through every layer. The loitering happens outside; the damage happens on the transit. Each AI is
phased onto its own arc so a full lobby spreads around the sphere.

**Every 4th strike is a RAID** on `Cell.GetExplosionTarget` — the densest mass hostile to its
domain, i.e. opponents' trails. The raid beat is offset by seat so the AIs never all raid at once.

**Invariant for anyone re-tuning this:** `AiStationStandoff` must stay **> 1**. A value ≤ 1 puts the
station on or inside the bone and the AI moves in permanently.

## End condition

Authored ONLY through **FrogletTools ▸ Game Modes ▸ End Game Conditions**
(`EndConditionOverridesSO.ribcagePrismTarget`, 0 = default **2000**) — the number of hostile prisms
a domain must DESTROY to win, the same target Rampage races to. Live/Build split + build
auto-restore work like every other mode. The milestone rungs are fractions of it (0.25 / 0.5).

> **⚠ Pacing flag — 2,000 is inherited, not measured for this arena.** It was set when every bar was
> a two-hit shielded prism in a 14,977-prism cage: ~4,000 hits, ~13% of the bone. The bars are now
> one-hit and intensity 1 is 5,471 prisms, so the same target is ~2,000 hits and **~37% of the whole
> arena** — roughly four times faster, against a much larger share of the cage. Expect intensity 1
> to finish quickly and to leave the arena visibly stripped. This has not been playtested. It is one
> editor field, and the milestones follow it automatically; a per-intensity target would need a
> small change to `RibcagePrismTurnMonitor`.

## The fauna removal (2026-08)

The mode used to run a **fauna ladder**: a brood was penned inside the cage, the cell's controlling
domain was pinned to the race leader (`Cell.SetModeControlOverride`) so the brood hatched in the
leader's colours, and the untouched legacy herbivore diet (eat opposing-domain mass) turned it loose
on every trailing team. The same two rungs opened the pen and added a predator. Fauna were **removed
from the level on request**, so:

| removed | kept |
|---|---|
| The five fauna config assets and the spawn profile's `SupportedFaunas` | Every platform capability the ladder was built on |
| `RibcageController.ApplyStage`, `PublishLeader_ClientRpc`, `PublishRelease_ClientRpc`, the stage constants | `Cell.SetModeControlOverride` / `ModePhaseFloor` / `FaunaReleaseTier` / `FaunaContainmentRadius` / `ContainmentIntruderFrenzy` / `HasPreyInsideFaunaContainment` |
| `SpawnableRibcage.ContainmentRadius` | `SpawnProfileSO.InitialFaunaReleaseTier`, `FaunaConfigurationSO.ReleaseTier`, the batched fauna seeding, the shielded-grid fix |

The kept items are general, documented platform capabilities with no Ribcage dependency — several
now have **no caller**, which is accepted for the same reason `ScoringMetric.PrismsRemaining` is
kept: churning a shared, serialized surface twice costs more than an unused-but-documented API.
Restoring the brood is a **data** change (author the species and list them on the spawn profile)
plus re-adding the ladder from git history.

## Shared-code touchpoints (added for this mode)

| Site | Change |
|---|---|
| `GameModes` | `Ribcage = 39` |
| `GameToastSituation` | `RibcageQuarterPeeled = 50`, `RibcageHalfPeeled = 51`, `RibcageLeaderChanged = 52` (50/51 were the fauna-release rungs; renamed, not retired — no `GameToastConfigSO` authors them yet, so nothing serialized pointed at the old names) |
| `Cell` | `SetModeControlOverride` (+ live-swarm re-colour), `ModePhaseFloor`, `FaunaReleaseTier`, `FaunaContainmentRadius` / `IsInsideFaunaContainment` / `ClampToFaunaContainment`, `ContainmentIntruderFrenzy` + `HasPreyInsideFaunaContainment`, `NotifyBlockShieldStateChanged`, shielded mass excluded from the targeting grids, release tier seeded from the spawn profile at config-assign |
| `HapticController` | `PlayAlert()` — the third feel, gate extended (`s_alertBusyUntil` outranks skim + punish) per `Docs/HAPTICS.md` |
| `Fauna` | `Goal` becomes a PROPERTY so containment clamps at the one point every writer passes through |
| `GameDataSO` | `SyncFromArcadeGame` clamps `selectedVesselClass` into `SO_ArcadeGame.Vessels` — enforces every restricted-vessel mode on every launch path |
| `ServerPlayerVesselInitializer` | `spawnRingRadiusFloor` — lets the computed spawn ring serve a cell whose core is a STRUCTURE rather than a nucleus |
| `SpawnProfileSO` | `InitialFaunaReleaseTier` — the biome's START tier, seeded before any spawner can tick |
| `IntensityWiseLifeSpawner` / `RandomLifeSpawner` | honour `ReleaseTier`; fauna seeding batched across frames |
| `PrismSpatialIndex` / `PrismStateManager` | `ForwardShieldChangeToCell`; shield transitions re-file the prism in its cell's grids |
| `EndConditionOverridesSO` (+ window + asset) | `ribcagePrismTarget` live/build/getter, default 2000 |
| `ElementalComebackSystem` | `GameModes.Ribcage` shares Rampage's `ScoreDifferenceSource.PrismsDestroyed` case |
| `ScoringMetric` / `ScoringMetrics.Read` | `PrismsRemaining = 6` → `stats.PrismsRemaining` — added for a standing-mass variant of this mode, kept as an available-but-unused metric |

### The one cross-mode behaviour change: shielded mass leaves the targeting grids

`Cell.AddBlock`'s own comment already stated the rule — *"fauna must never be led to mass they
cannot eat"* — and applied it only to nucleus-interior mass. `Docs/ECOSYSTEM.md` §16.2 then removed
shielded prisms from every herbivore's **diet**, but they stayed in the **grids**, so density
centroids kept steering swarms onto mass the creatures had just been told they could not eat. That
is the residue behind §16.3's Skim Race stall.

This branch finishes the rule: shielded prisms are excluded from the targeting grids at `AddBlock`,
and `NotifyBlockShieldStateChanged` (called from the single funnel every shield transition already
passes through) re-files a prism when a shield engages or is shed. It strictly *reduces* grid work
and adds no query.

**It affects two other modes and both are improvements** — Skim Race's super-shielded track and
Astro League's super-shielded edge lining no longer pull fauna steering. Verify both in-editor
(below) rather than assuming. Note this change now has **no bearing on Ribcage itself**, whose cage
is no longer shielded and which has no fauna — it is kept because it is a genuine platform fix.

## Assets

| Asset | Path |
|---|---|
| Arcade game config | `_SO_Assets/Games/ArcadeGameRibcage.asset` |
| Scoring rule | `_SO_Assets/Scoring Rules/RibcageScoringRule.asset` |
| Cell configs (4) | `_SO_Assets/Cell Configs/Ribcage Cell/Ribcage Cell Config {1..4}.asset` |
| Spawn profile | `_SO_Assets/Cell Configs/Ribcage Cell/Ribcage Spawn Profile.asset` |
| Cage prefabs (4) | `_Prefabs/Spawnables/SpawnableRibcage{1..4}.prefab` |
| Scene | `_Scenes/Multiplayer Scenes/MinigameRibcage.unity` (in `EditorBuildSettings`) |
| End conditions | `Assets/Resources/EndConditionOverrides.asset` (`ribcagePrismTarget`) |

Every asset above is authored by `Tools/Build/author_ribcage_assets.py` — deterministic GUIDs,
idempotent, validates before writing. **Re-tune there and re-run** rather than hand-editing the
YAML. `Tools/Build/ribcage_budget.py` is the cage's analytic budget model and the generator
**imports** it, so geometry and PhaseThresholds cannot drift apart.

## In-editor verification (authored headless — NOT yet run)

1. **Open** `MinigameRibcage.unity`. Every script reference resolves (no "Missing (Mono Script)"),
   the controller's inspector shows `rule` = RibcageScoringRule and the milestone fractions
   0.25 / 0.5, and the **Cell shows four configs with Cell Type Choice = Intensity Wise**.
2. **Intensity picks the layers.** Launch at intensity 1 → one shell. Relaunch at intensity 4 →
   four nested shells at 360 / 280 / 200 / 120. This is the headline check: if every intensity looks
   the same, the Cell is not on `IntensityWise` or the configs are listed out of order.
3. **Gaps are back.** The outer weave should read as an open ribcage (~87u × 82u openings), not a
   solid sphere — you should be able to fly between the bones without touching them, and see the
   next rind through the gaps.
4. **No free corridor.** Line up on the centre from outside and fly straight in: the shells are
   phase-offset, so you should meet bone rather than thread all four layers untouched.
5. **Baseline confirm.** FrogletTools ▸ Ecology ▸ Measure Cell Environment Baselines should report
   **5,471 / 9,902 / 13,316 / 15,690** prisms for intensities 1–4. If it disagrees, the generator and
   `ribcage_budget.py` have drifted — fix both.
6. **Bars are ONE hit.** Ram a rib: it shatters on first contact, no shield to shed, no octahedron.
   Then find a **danger** bar (distinct material): also one hit, but it full-stops you, debuffs all
   four elements for 4 s and resets boost.
7. **No fauna.** Nothing should hatch, at any intensity, at any point in the match.
8. **Rhino only.** Pick a different vessel in an earlier game, then launch Ribcage — you should spawn
   a Rhino anyway, with a `clamping selected vessel` line in the log.
9. **Spawn outside.** All players start on a ring ~576u out, facing the cage, with the whole cage
   visible ahead — nobody starts inside it.
10. **Smashing scores; laying does not.** The HUD domain sum should rise as you break bars and not at
    all from laying trail. Shatter one of your OWN team's trail prisms — the sum must not move; a
    rival's trail must.
11. **Milestones.** When the leading domain reaches **500** destroyed the device should shake hard
    for ~1.2 s; again at **1,000**. Nothing else should change.
12. **Win + scoreboard.** First domain to **2,000 destroyed** ends the turn; winners show a time,
    losers "N Bars Left". Replay (scene reload) resets the milestones.
13. **Pacing.** Time intensity 1 end to end — see the pacing flag under "End condition". If it
    finishes in well under a minute, lower the target.
14. **AI stays outside.** Watch an AI Rhino for a minute: it should orbit outside, cross the cage on
    transits, and only be inside briefly. If it settles inside, `AiStationStandoff` has been set ≤ 1.
15. **Regression — the grid change.** Play **Skim Race** (intensity 3) and **Astro League**: fauna
    should behave normally and should no longer park against the super-shielded track / edge lining.
16. **Collider telemetry** on device via DiagnosticsHUD / the Benchmark tool, at intensity 4 (the
    worst case, 15,690).

## Known limitations / follow-ups

- **Toast copy is unauthored.** The three `GameToastSituation` values exist but no
  `GameToastConfigSO` authors a definition for them, so they are silently skipped (which is how a
  mode opts out). Author a `GameToastConfig_Ribcage.asset` with `{0}`=domain, `{1}`=bars smashed,
  `{2}`=target to make them visible.
- **The 2,000 target is unmeasured for this arena** — see the pacing flag above. Most likely thing to
  need a change after the first playtest.
- **No objective-arrow provider**: like Rampage, `MiniGameHUD.CreateObjectiveProviderForGameMode` has
  no Ribcage case — the cage surrounds you, so there is no single point to aim at.
- **No UGS stats reporter yet** (a "most bars smashed" leaderboard is a clean follow-up), and no
  dedicated end-game controller — the shared scoreboard handles it.
- **Danger bars are a first pass.** One in 19 rib prisms, evenly spread by a deterministic index walk
  with a per-shell phase offset. If they read as noise rather than as traps, cluster them instead
  (whole trap *segments* of a rib) — one constant, `SpawnableRibcage.DangerEveryNthRibPrism`.
- **Several kept platform APIs now have no caller** (the fauna-containment family). Documented above
  as a deliberate trade, not an oversight.
- **`Cell.OpposingVolume` still counts shielded mass** as the fauna prey signal. Ribcage no longer
  touches this, but the honest fix is to net shielded volume out of that signal. Left alone
  deliberately: it is the population bound for every biome, so it deserves its own change and its own
  verification.
