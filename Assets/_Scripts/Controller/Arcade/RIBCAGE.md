# Ribcage — Technical Documentation

## Overview

Ribcage is the **Rhino-only cage-breaking race**. A hollow sphere of SHIELDED prism
bone pens the cell's brood; domains race to smash their way out, and the team in
front wears the swarm's colours.

- **The cage is the arena and the objective.** ~2,700 shielded prisms in sixteen
  meridian ribs, seven latitude hoops, a woven cross-lattice and two polar crowns.
  Every bar takes **two hits** — the first sheds the shield, the second shatters it —
  unless the hit *devastates*, which is the mode's whole skill surface.
- **Fauna cannot touch the cage, by construction.** Shielded mass is not food for any
  herbivore (`Docs/ECOSYSTEM.md` §16.2) and — since this branch — is not a fauna
  steering target either. So the race can neither be eaten out from under the players
  nor stall with a swarm parked on bars it cannot chew. **Nothing in the cage is
  super-shielded**: that tier is fully invulnerable (`Prism.Damage` returns early), so
  one such bar would be permanently unbreakable mass and enough of them would put the
  target out of reach.
- **The leader IS the cell's controlling domain.** That single publication is the
  whole fauna hook. Fauna already spawn in exactly one colour — the cell's controlling
  colour (the locked no-domain-asymmetry invariant) — and herbivores in a
  nucleus-less cell already eat **opposing-domain** mass. So the brood hatches wearing
  the leader's colours and hunts every trailing team's trails. **There is no
  "target the loser" code anywhere; the diet rule was always this.** When the lead
  changes hands the override re-colours the live swarm and its diet flips with it.
- **Two release rungs.** 25% of the target releases the grazer swarm and floors the
  cell at Restless; 50% adds the predator and floors it at Frenzy. Aggression,
  steering, danger-immunity and speed all fall out of the existing
  `CellPhase → CellAggressionLevel` mapping.

**Key architectural facts:**

- **Scene**: `Assets/_Scenes/Multiplayer Scenes/MinigameRibcage.unity` (single unified
  scene, cloned from Rampage's skeleton — no separate singleplayer variant; solo play
  is a party of one + AI backfill)
- **GameMode enum**: `GameModes.Ribcage = 39`
- **Controller**: `RibcageController : MultiplayerDomainGamesController` — structural
  sibling of `RampageController` (1 round / 1 turn, `HasEndGame=false`, server winner
  detection in `OnTurnEndedCustom`, snapshot `SyncFinalScores_ClientRpc`), plus the
  fauna ladder
- **Scoring**: `RibcageScoringRuleSO` (`metric = ScoringMetric.PrismsDestroyed`,
  golf-timed like HexRace/Scurry/Rampage) — winning-domain players `Score = finish
  time`, losers the `GolfScoreSentinels` remaining sentinel (displayed "N Bars Left")
- **Turn monitor**: `RibcagePrismTurnMonitor` — resolves the cage target from
  `EndConditionOverridesSO.GetRibcagePrismTarget()` (default **600**, FrogletTools ▸
  Game Modes ▸ End Game Conditions — never a per-scene field), syncs it via
  NetworkVariable → `GameDataSO.PrismTargetCount`
- **Domains**: `MinDomainsAllowed = 2` (like Joust — a cage race with everyone on one
  team has no one to feed the swarm), `MaxDomainsAllowed = 3`; players **2–4** with AI
  backfill
- **Vessels**: **Rhino only** (`ArcadeGameRibcage.Vessels` has one entry). The mode is
  built around the ram and the two-hit shielded bar.
- **Config**: `_SO_Assets/Games/ArcadeGameRibcage.asset` (registered in
  `GameLists/OrganicRematchGames.asset`, `ProgressionConfig.alwaysUnlockedModes`)

## The destruction → score pipeline (zero bespoke tracking)

Identical to Rampage's — the stat was already plumbed platform-wide:

```
Rhino ram / ability shatters a bar (2nd hit; 1st sheds the shield)
  └─ Prism.Damage → SetupDestruction → onTrailBlockDestroyed.Raise(PrismStats{…})
              ▼
StatsManager.PrismDestroyed                        [server-only via _allowRecord]
  └─ victim non-roster (the cage) OR another domain's trail → HostilePrismsDestroyed++
              ▼
ScoringMetrics.Read(stats, PrismsDestroyed) → SumByDomain
  ├─ MultiplayerDomainGamesController.SyncDomainSumsRoutine → HUD domain panels
  ├─ RibcagePrismTurnMonitor.CheckForEndOfTurn → rule.IsObjectiveReached  [server]
  ├─ RibcageController.SampleLadder → leader + release tier                [server]
  └─ ElementalComebackSystem (source PrismsDestroyed) → trailing-team buff
              │  turn end
              ▼
RibcageController.OnTurnEndedCustom → AssignScores → SyncFinalScores_ClientRpc
```

Your own and your teammates' trails never score (the roster domain check filters
them), so there is no lay-and-smash farming loop.

## The fauna ladder (zero bespoke ecology)

The controller publishes **two facts** to the arena cell and lets the existing ecology
draw every consequence. It contains no fauna targeting code at all.

```
RibcageController.SampleLadder            [server, every ladderSampleSeconds = 0.5s]
  │  leader = active domain with the highest HostilePrismsDestroyed sum
  │  progress = leaderSum / GameDataSO.PrismTargetCount
  │
  ├─ PublishLeader_ClientRpc  → Cell.SetModeControlOverride(leader)   [EVERY peer]
  │     ├─ Cell.DominantDomain now returns the leader
  │     │     └─ Cell.ControllingDomain → RandomLifeSpawner spawns the wave in
  │     │        the leader's colour  (no-domain-asymmetry: ONE colour, the
  │     │        controller's — unchanged)
  │     ├─ Cell.IsPreyForHerbivore (no nucleus zone) → preyDomain != faunaDomain
  │     │     └─ the swarm eats every TRAILING team's trails. That is the whole
  │     │        "fauna hunt the losers" feature: the legacy diet rule, unmodified.
  │     └─ live swarm re-coloured via Fauna.SetTeam, so a lead change flips the
  │        targets of the creatures already in the air, not just the next wave
  │
  └─ PublishRelease_ClientRpc → Cell.FaunaReleaseTier + Cell.ModePhaseFloor  [EVERY peer]
        tier -1  (sealed, < 25%) : no species may seed; no phase floor
        tier  0  (>= 25%)        : Ribcage Tadpole (ReleaseTier 0) seeds;
                                   floor = Restless → CellAggressionLevel.Level1
                                   (steer at the opposing-colour centroid = the
                                   trailing teams' trails)
        tier  1  (>= 50%)        : Ribcage Shark (ReleaseTier 1) joins;
                                   floor = Frenzy → Level2 (any-colour steering,
                                   friendly avoidance off, danger-immune, faster)
```

Both publications are ClientRpcs because **fauna are client-local** (no NetworkObject),
so every peer must run its own gate. The control pin would also replicate through
`CellNetworkSync` on its own 0.5s mirror; the RPC just makes the swarm change colour on
the event rather than on the next tick.

`ApplyRelease` calls `Cell.RestartSpawnerForMode()` when a tier opens, so the fauna
spawn clock realigns to the **release moment** — otherwise the gate opens mid-period
and the brood can take a full `BaseFaunaSpawnTime` to appear, which reads as the reward
simply not arriving. The profile authors no flora, so the "restart re-runs the initial
flora batch" caveat on that method does not apply here.

### Why the leader gets *helped* rather than punished

The swarm is a snowball, and the counterweight is already in the box: destruction feeds
`ElementalComebackSystem` (`ScoreDifferenceSource.PrismsDestroyed`), so the further
ahead the leader gets the stronger the trailing teams' all-element buffs become. Pull
too far ahead and you are fighting buffed Rhinos while your own swarm chews mass that
no longer scores for you. `ComebackRatePerScoreDeficit` is **0.03** (vs Rampage's 0.01
at a 2000 target) so a ~300-bar deficit against a 600 target reaches the same buff
ceiling a ~10-crystal deficit does in Scurry.

## Ecology configuration

`_SO_Assets/Cell Configs/Ribcage Cell/`:

- **Ribcage Cell Config** — Blob-class membrane/cytoplasm, `EnvironmentPrefab` =
  `SpawnableRibcage.prefab`. **NO `NucleusPrefab`, and that is load-bearing:** a
  nucleus control zone switches herbivores to the spatial "eat anything outside the
  nucleus" diet (`Cell.IsPreyForHerbivore`), which would point the swarm at *every*
  team including the leader's and break the entire hook. Ribcage needs the legacy
  opposing-domain diet.
  `PhaseThresholds` ride the measured cage baseline + the standard Blob deltas
  (`Docs/ECOSYSTEM.md` §18): Restless 3421/3221 (volume 1,080,580/1,077,380),
  Frenzy 6321/5721 (volume 1,126,980/1,117,380). The cell therefore boots **Calm**,
  and since destruction only *lowers* volume the ladder never climbs on its own — the
  mode's phase floor is the only thing that raises it, which is exactly the intent.
- **Ribcage Spawn Profile** — **no flora** (the cage is the arena; flora would add
  unshielded mass that fauna erode and that dilutes the cage as the scoring target).
  `InitialFaunaSpawnWaitTime 0` (the release tier is the gate, not a clock),
  `BaseFaunaSpawnTime 15`, `FaunaFoodFloor 0`. Herbivore ring: 3 points at radius
  **180** and predator ring 2 points at **220** — both **inside** the 300-unit cage, so
  the brood hatches within the ribs and pours out through the bars the players break.
- **Ribcage Tadpole Fauna Config Data** (`ReleaseTier 0`) — the grazer swarm, 6 per
  seed, cap 14.
- **Ribcage Shark Fauna Config Data** (`ReleaseTier 1`) — the 50% predator, cap 3.

Both species are Blob-lineage clones (same prefabs, element palettes and level spread),
so they drop elemental crystals on death like every other lifeform — skimmable
powerups mid-match.

## The cage

`SpawnableRibcage : CellEnvironmentSpawnableBase`, radius **300**, seed 39,
deterministic per seed like every cell environment. Analytic budget
(`Tools/Build/ribcage_budget.py`; confirm with FrogletTools ▸ Ecology ▸ Measure Cell
Environment Baselines):

| structure | count | vol/prism | volume | detail |
|---|---:|---:|---:|---|
| meridian ribs | 1776 | 431.3 | 766,004 | 16 ribs × 111 |
| latitude hoops | 509 | 431.3 | 219,536 | lats 0, ±26, ±52, ±74 |
| cross-lattice | 288 | 131.8 | 37,955 | 16 pairs × 6 bands × 3 |
| joints | 112 | 327.5 | 36,683 | 16 × 7 crossings |
| polar crowns | 36 | 255.6 | 9,201 | 2 × 18 at lat ±84 |
| **TOTAL** | **2,721** | | **1,069,380** | |

The rib-to-rib gap at the equator is ~118u, so this is a **ribcage, not a prison
grille**: you fly between the bones freely. Sealing the sphere to vessel-tight spacing
would cost ~6k prisms of always-on collider for no gameplay — the goal is to smash the
structure, never to be locked inside it.

**Collider-budget impact.** ~2,721 box colliders for the cage. Shielded prisms keep the
authored **BoxCollider trigger** (`PrismOctahedronShield` changes the LOOK only — a
convex-mesh collider is invisible to one skimmer family or the other), so a shielded
bar costs exactly what a plain prism costs and the octahedron look is free. That is
~1.8× the masterplan's ≤1500 per-cell target and **~3.7× *under* Rampage's deliberate
10,000-prism arena gate**, with no flora in the cell and no new physics queries
anywhere — fauna senses ride `PrismSpatialIndex`, scoring rides the StatsManager SOAP
channel, and the AI aims analytically (below). Destruction actively removes colliders
as the match runs. Watch the collider/prism telemetry (DiagnosticsHUD / Benchmark tool)
on device; `RibCount` / `BarStep` are the two numbers to turn down.

## AI cage-breakers

Deliberately **not** Rampage's `Cell.GetExplosionTarget` density-grid mass hunt: the
cage is shielded and shielded mass is now kept out of the targeting grids, so the grids
here hold only player trails and would send every AI chasing vessels instead of
breaking out. The shell is an analytic sphere, so `RibcageController.ArmCageBreakers`
walks a golden-angle spiral over it — successive targets far apart, deterministic,
never repeating a spot — refreshed every `aiRetargetSeconds` (2s). Each AI is phased
onto its own arc so a full lobby spreads around the sphere instead of queueing at one
rib. Ramming *through* the aimed point is what breaks the bars.

## End condition

Authored ONLY through **FrogletTools ▸ Game Modes ▸ End Game Conditions**
(`EndConditionOverridesSO.ribcagePrismTarget`, 0 = default **600**). The 25%/50%
release thresholds are *fractions of this same number* (read from
`GameDataSO.PrismTargetCount`), so moving the target moves the whole escalation ladder
with it and the two can never drift. Live/Build split + build auto-restore work like
every other mode.

At the default 600 with three domains neck-and-neck the worst case is 1,800 bars
destroyed of 2,721 — the cage survives every match as a broken ruin rather than
vanishing mid-race.

## Shared-code touchpoints (added for this mode)

| Site | Change |
|---|---|
| `GameModes` | `Ribcage = 39` |
| `GameToastSituation` | `RibcageBroodReleased = 50`, `RibcagePackReleased = 51`, `RibcageLeaderChanged = 52` |
| `Cell` | `SetModeControlOverride` (+ live-swarm re-colour), `ModePhaseFloor`, `FaunaReleaseTier`, `NotifyBlockShieldStateChanged`, shielded mass excluded from the targeting grids |
| `PrismSpatialIndex` | `ForwardShieldChangeToCell` |
| `PrismStateManager` | `SyncAOERegistryShieldState` also re-files the prism in its cell's grids |
| `RandomLifeSpawner` | staged-release gate (`faunaCfg.ReleaseTier <= host.FaunaReleaseTier`) |
| `FaunaConfigurationSO` | `ReleaseTier` (default 0 — no shipped biome changes) |
| `EndConditionOverridesSO` (+ window + asset) | `ribcagePrismTarget` live/build/getter, default 600 |
| `ElementalComebackSystem` | `GameModes.Ribcage` default-source case |

### The one cross-mode behaviour change: shielded mass leaves the targeting grids

`Cell.AddBlock`'s own comment already stated the rule — *"fauna must never be led to
mass they cannot eat"* — and applied it only to nucleus-interior mass. `Docs/ECOSYSTEM.md`
§16.2 then removed shielded prisms from every herbivore's **diet**, but they stayed in
the **grids**, so density centroids kept steering swarms onto mass the creatures had
just been told they could not eat. That is the residue behind §16.3's Skim Race stall,
and it is fatal to a mode whose arena *is* a shielded structure.

This branch finishes the rule: shielded prisms are excluded from the targeting grids at
`AddBlock`, and `NotifyBlockShieldStateChanged` (called from the single funnel every
shield transition already passes through) re-files a prism when a shield engages or is
shed. It strictly *reduces* grid work and adds no query.

**It affects two other modes and both are improvements** — Skim Race's super-shielded
track and Astro League's super-shielded edge lining no longer pull fauna steering.
Verify both in-editor (below) rather than assuming.

## Assets

| Asset | Path |
|---|---|
| Arcade game config | `_SO_Assets/Games/ArcadeGameRibcage.asset` |
| Scoring rule | `_SO_Assets/Scoring Rules/RibcageScoringRule.asset` |
| Cell config | `_SO_Assets/Cell Configs/Ribcage Cell/Ribcage Cell Config.asset` |
| Spawn profile | `_SO_Assets/Cell Configs/Ribcage Cell/Ribcage Spawn Profile.asset` |
| Fauna species | `…/Ribcage Tadpole Fauna Config Data.asset`, `…/Ribcage Shark Fauna Config Data.asset` |
| Cage prefab | `_Prefabs/Spawnables/SpawnableRibcage.prefab` |
| Scene | `_Scenes/Multiplayer Scenes/MinigameRibcage.unity` (in `EditorBuildSettings`) |
| End conditions | `Assets/Resources/EndConditionOverrides.asset` (`ribcagePrismTarget`) |

Every asset above is authored by `Tools/Build/author_ribcage_assets.py` — deterministic
GUIDs, idempotent, validates before writing. **Re-tune there and re-run** rather than
hand-editing the YAML, so the generator stays the source of truth.
`Tools/Build/ribcage_budget.py` is the cage's analytic budget model; keep it in sync
with `SpawnableRibcage.cs` when the geometry changes.

## In-editor verification (authored headless — NOT yet run)

1. **Open** `MinigameRibcage.unity`. Every script reference resolves (no "Missing
   (Mono Script)"), the controller's inspector shows `rule` = RibcageScoringRule,
   `arenaCell` = the scene Cell, and the release fractions 0.25 / 0.5.
2. **Cage builds.** Enter play mode solo (party of one + 3 AI backfill). The connecting
   screen should hold until the cage is fully laid and grown
   (`PrismTrailBuilder.PollArenaReady`), then reveal a complete ribcage — **nothing
   should pop in after the countdown** (continuity of existence). If the cell's
   deferred build starts after the connecting screen releases, an `EnvironmentLoadVeil`
   takes over instead; either path is correct, but confirm which one you see.
3. **Baseline confirm.** FrogletTools ▸ Ecology ▸ Measure Cell Environment Baselines
   should report **2,721 prisms / ~1,069,380 volume** for the Ribcage cell. If it
   disagrees, the generator and `ribcage_budget.py` have drifted — fix both.
4. **Bars are two-hit.** Ram a rib: first contact sheds the shield (octahedron
   disengages), second shatters it and the HUD sum increments by one.
5. **No fauna before 25%.** Nothing hatches while the cage is sealed.
6. **25% release.** At 150 bars (default target 600) the brood toast fires, tadpoles
   hatch **inside** the cage wearing the **leading domain's** colour, and they leave to
   graze the *trailing* domains' trails — never the leader's, never the cage.
7. **50% release.** At 300 bars the pack toast fires and a shark joins; the cell reads
   Frenzy on the DiagnosticsHUD.
8. **Lead change flips the swarm.** Let a second domain take the lead — the *live*
   creatures should re-colour and switch which trails they eat.
9. **Win + scoreboard.** First domain to 600 ends the turn; winners show a time,
   losers "N Bars Left". Replay (scene reload) re-seals the cage and resets the ladder.
10. **Regression — the grid change.** Play **Skim Race** (intensity 3) and **Astro
    League**: fauna should behave normally and should no longer park against the
    super-shielded track / edge lining.
11. **Collider telemetry** on device via DiagnosticsHUD / the Benchmark tool; if the
    cage is too heavy, lower `RibCount` (16) or raise `BarStep` (17) in
    `SpawnableRibcage.cs` and re-run both Python tools.

## Known limitations / follow-ups

- **Toast copy is unauthored.** The three `GameToastSituation` values exist but no
  `GameToastConfigSO` authors a definition for them, so they are silently skipped
  (which is how a mode opts out). Author a `GameToastConfig_Ribcage.asset` with
  `{0}`=domain, `{1}`=bars smashed, `{2}`=target to make them visible.
- **No objective-arrow provider**: like Rampage and Brood Rush,
  `MiniGameHUD.CreateObjectiveProviderForGameMode` has no Ribcage case — the cage
  surrounds you, so there is no single point to aim at.
- **No UGS stats reporter yet** (a "most bars smashed" leaderboard is a clean
  follow-up), and no dedicated end-game controller — the shared scoreboard handles it.
- **Target 600 is a first guess.** Nobody has measured how fast a Rhino clears
  two-hit bars. It is one editor field; expect to tune it on the first playtest, and
  the release rungs follow it automatically.
- **`Cell.OpposingVolume` still counts shielded mass** as the fauna prey signal, so a
  shielded structure satisfies `FaunaFoodFloor` without being food. Ribcage sidesteps
  it (`FaunaFoodFloor 0` — the release tier is the real gate), but the honest fix is to
  net shielded volume out of that signal. Left alone deliberately: it is the population
  bound for every biome, so it deserves its own change and its own verification.
