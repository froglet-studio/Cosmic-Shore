# Unified Systems — Audit

**Date:** 2026-07-17 · **Branch:** `claude/unified-systems-refactor-apze59` (== bleeding-edge `1f558502`)
**Companion doc:** `Docs/CODEBASE_OUTLINE.md` (branch `claude/game-codebase-outline-yqkz47`) — the whole-game
atlas whose per-area observations seeded this audit's hypotheses.

> **This is the evidence base.** The working documents are `GARRETT.md` (decision sheet D1-D21 +
> owner items), `YASH.md` (systems engineering programs Y0-Y8), and `SHOMBITH.md` (UI
> consolidation + tooling S0-S5). The zero-risk deletion wave described in §4 Wave 1 has been
> partially executed on this branch (commit `16cc36e8`) — re-verify remaining claims at the
> moment of change; line numbers drift.

**What this is.** A measured inventory of the codebase's *unification debt*, in three classes:

- **(A) Vestigial cleanup** — a unification already happened and won, but the losing generation's
  corpse is still in the tree (dead classes, unwired prefab components, `[Obsolete]` assets still
  executing, commented-out bodies kept "as reference").
- **(B) Unify opportunities** — two (or more) parallel systems still do the same job today; one
  should absorb the other.
- **(C) Unenforced patterns** — a contract held across a *class* of things (the 11 vessels, the
  domain minigames, the input strategies, "all cross-system events are SOAP") that exists only by
  convention/comment, so members drift or silently ship incomplete.

This is the same shape the Elemental Ability system just solved (`Docs/ElementalAbilitySystem/`):
the quantitative layer became a *fleet contract* (`ElementalAbilityMapSO` per vessel, display that
"literally cannot ship without" existing via `SilhouetteController.CreateDefaultElementBars`). This
audit finds everywhere else that same move is either half-done or still waiting.

**Method + confidence.** Every finding was produced by one auditing agent and then independently
**adversarially verified** by a second agent attempting to refute it against the live tree. Wiring
claims were measured, not assumed: each suspect class's `.cs.meta` guid was grepped across all
`*.prefab` / `*.unity` / `*.asset` files, and code references were grepped repo-wide.
"**fully dead**" below means *zero content references AND zero live code references*. Of 67
findings, 53 verified as stated and 14 were **adjusted** (corrections incorporated below); **zero
were refuted**. Verify at the moment of fixing anyway — line numbers drift.

**Locked designs are respected.** Nothing here proposes decay/timers, client domain writes, lazy
Relay, or SOAP violations — several findings *restore* locked designs where drift crept in.

---

## 0. Executive summary

The codebase has completed (or nearly completed) at least **eight major unifications** — R_ vessel
actions, `ScoringRuleSO` domain scoring, `GenericPoolManager` pooling, UGS auth/data over PlayFab,
FMOD SFX, `ElementalBarsView`, `EventDrivenStatsProvider`, the networked spawn pipeline — and in
every single case the losing system was left in the tree. Roughly **80+ fully-dead classes**,
**~23 scene-less `SO_ArcadeGame` assets**, a **68k-LOC inert SDK**, and a dozen half-wired
"successor built but never mounted" systems remain. Worse, some vestiges are not inert: the audit
found **live bugs caused directly by unfinished unifications** (§0.1).

Three unifications are genuinely *unfinished* and already have declared target states in team
docs: **one always-networked scoring path** (`Docs/ScoringSystem/REFACTOR.md` R1), **one spawn
pipeline** (`Player.cs:124` — "No offline single-player: every session is a Relay host"), and
**one audio pipeline** (`AudioSystem.cs:33` header prescribes the music-on-FMOD migration).

The highest-leverage *new* work is **enforcement**: fleet-contract and convention tests (the
`EnumIntegrityTests` precedent) that turn each convention into a failing test, so none of this
regrows.

### 0.1 Live bugs caused by unification debt (fix-with-cleanup)

| Bug | Root cause | Evidence |
|---|---|---|
| Joust results can mis-sort after game end | Legacy `NetworkScoreTracker` still enabled in the migrated Joust scene, `golfRules:0` on a golf mode, fires a **second** `SortRoundStats` + `InvokeWinnerCalculated` 500 ms after the controller's authoritative RPC (masked only by `EndGameSequencer._isRunning`) | `MinigameJoust_Gameplay.unity:10613,10618`, `NetworkScoreTracker.cs:37-47`, `EndGameSequencer.cs:110` |
| Invert-Y / invert-throttle silently ignored on touch — the mobile-first platform | Input pipeline copy-pasted per strategy; Touch's copy omits the inversion block | `TouchInputStrategy.cs:286-304` (no `InvertYEnabled` hits), vs `GamepadInputStrategy.cs:182-202` |
| Daily challenge play/claim NREs; faction missions, hangar training, arcade-explore launches dead | `Arcade` singleton attached to nothing (`Instance` null) and PlayFab `CatalogManager`/`DailyRewardHandler` prefabs unplaced, but 5 live UI paths still call them | `Arcade.cs:20` (guid 0 content hits), `FactionMissionModal.cs:25`, `HangarTrainingModal.cs:175`, `DailyChallengeSystem.cs:205,243` |
| Quest track shows only mode 0; intensity unlocks fall back to defaults; quest toasts never fire | `GameModeProgressionService` exists **only** in `_Prefabs/MIgration_Prefabs (DELETE LATER)/PlayerDataService.prefab`, referenced by nothing — `Instance` is permanently null while 8+ shipping UI files null-guard around it | `GameModeProgressionService.cs:20`, `QuestTrackView.cs:180-181` |
| Store and Port (leaderboards) screens dead-render | PlayFab teardown stopped halfway: manager prefabs unplaced, static events never fire, but screens still gate on them (UGS replacement `UGSStatsManager` exists) | `StoreScreen.cs:83,100`, `LeaderboardsMenu.cs:47,67` |
| ProfileModal random-name flow hangs a coroutine | Live `WaitUntil` on a disabled PlayFab `GetTitleData` call | `ProfileModal.cs:193-195`, `AuthenticationManager.cs:58-80` |
| Continuous vessel SFX play at slider² volume | Four FMOD emitter controllers hand-copy per-instance slider math on top of `AudioSystem`'s bus-level slider | `AudioSystem.cs:683,700-707`, `DriftAudioController.cs:549` etc. |
| Settings → "Run Benchmark" button loads an unloadable scene | `BenchmarkStressTest.unity` absent from EditorBuildSettings; scene also carries the wrong controller vs. docs | `BenchmarkSceneLauncher.cs:20,40`, `EditorBuildSettings.asset` |
| 5 Arcade UI cards launch games whose scenes don't exist | `ArcadeGames.asset` still lists BlockBandit, Darts, MazeRunner, Rampage, SlipNStride | `_SO_Assets/Games/GameLists/ArcadeGames.asset` |
| Squirrel align-toggle & shard-toggle abilities silently no-op | Align: legacy component undispatchable + R_ asset never wired. Shard: `ShardFieldBus` bodies commented out, zero registered listeners | `Squirrel.prefab:3336`, `ShardFieldBus.cs:17,22`, `ShardToggleActionExecutor.cs:50` |
| Sparrow full-auto block prisms never recycle when destroyed | `BlockProjectileFactory.ReturnBlock` is a commented-out stub; the one live release call site is a no-op (one-way consumption is the conserved-mass norm — the bug is only the *destroyed-prism* recycle path) | `BlockProjectileFactory.cs:50-62`, `DomainCheckProjectilePrismHitEffectSO.cs:68` |
| Loadout/squad/training progress don't roam across devices | Live systems write `DataAccessor` local files while their finished CloudSave mirror repos load every session with zero consumers | `LoadoutSystem.cs:30,108`, `UGSDataService.cs:141-152` |

---

## 1. Class A — Vestigial cleanup (the unification happened; delete the corpse)

### 1.1 Vessel actions: the legacy `ShipAction` generation is undispatchable — **CONFIRMED**

The R_ generation (`ShipActionSO` config + `ShipActionExecutorBase`) won completely. The only
dispatch path is `VesselController` → `VesselStatus.ActionHandler` (`R_VesselActionHandler`),
which handles `ShipActionSO` exclusively (`VesselController.cs:202`); the legacy dispatch
overloads (`VesselHelper.cs:70-111`) have **zero callers** and nothing ever calls
`ShipAction.Initialize`.

- **~26 legacy scripts are fully dead** (0 content, 0 code refs): ChargeBoost, ConsumeBoost,
  Overheating, Drift, DriftTrail, GrowTrail, GrowSkimmer, GrowActionBase, ZoomOut,
  DeployTeamCrystal, ShardToggle, SeedWall, CloakSeedWall, ToggleStationaryMode, ApplyRotation,
  ChangeRotationSpeed, DisableTrail, SyncActionWrapper, StopGunsAction,
  ToggleProjectileActionWrapper, and the stubs SeedAssemblerMono, ZoomGrowRateDistributeAction,
  AssembledArchBurstAction ("TODO This class will be deleted"), etc.
- **17 legacy components remain attached but inert**, and *only* on the five prefabs absent from
  `Vessel Prefab Container.asset` (Urchin/Grizzly/Termite/Falcon/Shrike — unreachable through the
  spawn pipeline), plus one `ToggleAlignAction` on the shipping Squirrel (`Squirrel.prefab:3336`).
  All 11 prefabs already carry `R_VesselActionHandler`, so stripping orphans nothing.
- **Caveat:** the unshipped vessels' unique behaviors (drones, gyro, spin, energize, ghost,
  detach) exist *only* in legacy form — port to R_ SO+executor before productizing those vessels,
  or accept losing the reference implementations. `IScaleProvider` is consumed by live R_
  executors and must be re-homed, not deleted.

**Do:** delete the ~26 dead scripts + the `ShipHelper` legacy overloads; strip inert components
from the five prefabs; decide align-toggle (wire `ToggleAlignAction.asset` into Squirrel's R_
mapping or delete all three artifacts). **Effort M.**

### 1.2 The dead second elemental binder — **CONFIRMED**

Two parallel `ElementalFloat` reflection binders exist. `ElementalShipComponent.BindElementalFloats`
(`ElementalVesselComponent.cs:14-30`) is live (Skimmer). `ElementalFloatBinder`
(`VesselActions/ElementalFloatBinder.cs`) is fully dead **and stale** — its sole call site is
commented out (`VesselActionSO.cs:12`), and it reflects `GetProperty("Ship")` against the renamed
`Vessel` property, so even re-enabling it would silently no-op. Already confirmed as the dead
SO-side layer in `Docs/ElementalAbilitySystem/AUDIT.md` §1.2; the fix direction (executor-side
live reads) is already the shipped Phase-1 pattern. **Do:** delete. **Effort S.**

### 1.3 Scoring: dead generation ×3 — **CONFIRMED**

- `CompositeScoring.cs`, `ScoreData.cs` (100 % commented), `BaseScoringMode.cs` +
  `CompositeScoringMode.cs` (closed two-class island): **fully dead — delete all four**.
- **10 of 17 `ScoringModes` values appear in zero content**; values 3–6 construct strategies that
  `throw NotImplementedException` from inspector data (`BaseScoreTracker.cs:127-130`) — a
  designer-reachable landmine. Delete the three throwing stubs + enum values 3–6; fold the six
  never-authored working strategies (8,9,10,12,13,14) into the unified-path migration decision (§2.1).
- **Six turn monitors have zero content instances**, four of them behavioral stubs (instant-end or
  never-end): CellControl, Distance, ResourceAccumulation, ShipCollision (file
  `VesselCollisionTurnMonitor.cs`), VolumeCreated, VolumeDestroyed. Delete; rebuild any wanted end
  condition as a `ScoringRuleSO`-delegated monitor. **Effort S each.**

### 1.4 The deprecated `MiniGame` loop is content-dead but survives as a static config bag — **ADJUSTED**

`MiniGame` (489 lines, mostly commented), `CellularBrawlMiniGame`, `ProtectMissionGame`: zero
content wiring each. The *only* live dependency (verified correction: `Arcade.cs`'s writes are all
inside comment blocks) is `ArcadeExploreView.cs:152-159` reading/writing `MiniGame` **statics** as
a launch-config mailbox — and two of the four statics it reads are *never written*, so that launch
path always passes intensity 1 / players 1 regardless of UI. The in-code TODO already says it:
"Remove statics from MiniGame, use SOAP Data Container" (`ArcadeExploreView.cs:147`).
**Do:** route `ArcadeExploreView` through `GameDataSO`, then delete all three classes (unblocks
§1.3's commented-referenced monitors too). **Effort S–M.**

### 1.5 Impact effects vestiges — **CONFIRMED (one adjustment)**

- `PrismImpactor` / `MineImpactor` effect arrays are private **without** `[SerializeField]` —
  unassignable from content, never assigned in code; both `AcceptImpactee` bodies are provably
  dead (reactions fire from the striking side's `*DataContainerSO`). Three abstract effect types
  exist solely to type the dead arrays (`VesselMineEffectSO`, `ExplosionMineEffectSO`,
  `SkimmerProjectileEffectSO` — zero concrete subclasses). **Strip + delete.**
- `[Obsolete] SkimmerFXPrismEffectSO` still **executes on the shipping Squirrel** (menu default
  vessel) — wired in `SquirrelSkimmerImpactorDataContainer.asset:22` *alongside* its own
  replacement (`SkimmerForcefieldCracklePrismEffect` at `data[4]`). The Dolphin hit is inert stale
  YAML (override targets a removed serialized field). Finish the migration or drop `[Obsolete]`.
- **Seven static events raised with zero subscribers** (pure dead broadcast): `OnPrismCollision`,
  `OnDangerBlockSpawned`, legacy `FireGunAction.OnShotFired`, both `OnVolleyFired`s,
  `ShapeSign.OnShapeSelected`, `FreestyleSign.OnFreestyleSelected`. Delete with their Invoke sites.
- `VesselCollider`/`IVesselCollider`: fully dead, and its deprecation comment points at
  `R_ImpactCollider` — a type that **does not exist** (successor is `ImpactCollider`). Delete.
- `ShipHUD` (`VesselHUD.cs`, "TODO: remove") is reachable only through the unshipped Termite
  prefab chain, yet three subscribers keep the `onShipHUDInitialized` channel alive
  (`MiniGameHUD.cs:296`, `MenuMiniGameHUD.cs:102`, `GameCanvas.cs:35`). Delete the raiser, the
  subscriptions, and (if no raiser is planned) the `ShipHUDData` SOAP family.
- Dead effect leaves: `SkimmerScaleTrailAndCameraPrismEffectSO` (zero instances);
  `VesselDecoyByCrystalEffectSO` (empty `Execute`, 33-line commented body, orphaned Manta asset) —
  a commented-out implementation is not a backlog. **Effort S each.**

### 1.6 UI dead mass — **CONFIRMED (adjustments noted)**

- **Notification System (banner): zero senders.** Complete 7-file receive pipeline + channel +
  settings assets, `NotificationAPI.Notify` has zero callers — yet the presenter prefab is
  **nested inside 5 of 11 vessel prefabs** (not Sparrow — more fleet drift). Its role is already
  served by `ToastNotificationAPI`. Delete the family + the per-vessel nestings.
- **`GameCanvas.cs`:** on zero prefabs/scenes (the shipping `GameCanvas.prefab` does *not* carry
  it); dependency tree is the dead `MiniGame` generation. Delete with §1.4.
- **`ResourceDisplay`/`ResourceButton`:** zero code callers, zero UnityEvent/anim-event callers;
  inert components still run `Start()` on live HUD variants (Sparrow ×3, Squirrel, Serpent, and
  the shared base `VesselHUDPrefab` ×4, which propagates into shipping HUDs). 3 orphan prefabs
  (EnergyDisplay, BoostDisplay, ItemDisplay); "Wall Button" is nested in the *live*
  SerpentHUDVariant (inert but not an orphan). Delete + strip.
- **Dead HUD scaffolding:** empty `IMinigameHUDController`/`IMinigameHUDView`,
  `MinigameHUDContainer`, unwired `HexRaceHUDView`, and the whole
  `_Prefabs/MIgration_Prefabs (DELETE LATER)/` folder (9 prefabs, zero external refs — but first
  rescue `GameModeProgressionService` off it, §1.9).
- **`ElementPipsView`/`ElementPipsConfigSO`:** the dead predecessor of `ElementalBarsView`, with
  orphaned config asset. Delete (do **not** confuse with the live picture-in-picture `Pip.cs`,
  wired in 8 prefabs). **Effort S–M each.**

### 1.7 Controllers/spawn dead mass — **CONFIRMED (adjustments noted)**

- Five zero-wired classes in the *current* generation: `WildlifeBlitzMiniGame`,
  `SinglePlayerSlipnStrideController`, `SandboxBenchmarkController`, `VolumeTestController`,
  `VolumeTestPlayerSpawnerAdapter`. The benchmark one is a doc/content contradiction:
  `Docs/SettingsSystem/ARCHITECTURE.md` and `BenchmarkSceneLauncher.cs:14` both name it as the
  benchmark scene's controller, but `BenchmarkStressTest.unity` actually carries
  `SinglePlayerWildlifeBlitzController` — and the scene isn't in the build, so the live Settings
  button is broken either way. Decide once; delete or wire.
- **Content rot in the launch pipeline:** 23 of 36 `_SO_Assets/Games/*.asset` reference scenes
  absent from the entire Assets tree; `ArcadeGames.asset` still shows 5 dead cards in the UI;
  the mode-32 co-op blitz stack is dead end-to-end (controller on nothing; its scene contains a
  `MultiplayerCellularDuelController` clone leftover; asset in no game list);
  `PreviousAllGames.asset` (18 entries) is referenced by nothing. Prune in one pass; mark orphaned
  `GameModes` IDs retired-do-not-reuse. Also: CLAUDE.md's multiplayer file inventory is stale —
  `DomainAssigner.cs` and `NetworkStatsManager.cs` no longer exist anywhere.

### 1.8 Input/camera/audio/dialogue vestiges — **CONFIRMED (one deliberate-retention flag)**

- **`KeyboardMouseInputStrategy` is fully dead**; the live twin `KeyboardInputStrategy` is
  misfiled at the **`Assets/` root** (violates the documented structure). Delete the dead one
  (port mouse-look deliberately if wanted); move the live file into `Controller/IO/`.
- **`CameraSettingsApplier`:** 0 refs ever (verified across git history) and its job is duplicated
  by `CameraManager` — **but** the superseding commit deliberately documented retaining it for
  cameras `CameraManager` doesn't own (`Docs/SettingsSystem/ARCHITECTURE.md` §Camera consumers).
  Removing it is a design-doc decision, not routine cleanup.
- **Wwise is a husk; CLAUDE.md is wrong ×3.** `Assets/Wwise/` contains zero non-meta files; zero
  `AkSoundEngine` refs; the real middleware is FMOD (`Assets/Plugins/FMOD`, 7 first-party
  scripts). Delete the husk; fix CLAUDE.md lines 127/167/209.
- **The whole dialogue runtime (`System/Runtime/`) is built but never mounted:** no scene/prefab
  carries `DialogueManager`/resolver/views; the event channel has **no asset instance and no
  raiser**; FTUE doesn't use it. Team decision: mount it or archive it. Unconditionally delete
  `DialogueUIController` (dead pre-refactor duplicate of `MainMenuDialogueView`, ~85 % overlap),
  `IDialogueService` (zero implementors), `DialogueEditorRuntimeTester` (commented corpse).
- **AudioSystem/Jukebox dead tissue (~150 lines):** `PlayNextMusicClip`, both fade/crossfade paths
  (which contain a real bug — music faded by `SFXVolume` — inside unreachable code),
  `SetMixerSFXVolume`, `PlaySFXEvent(ref,pos)`/`PlaySFXEventAttached`, `MusicSource1/2`,
  `Jukebox.OnDeathExplosionCompletion`/`PlaySong`; the mixer "mute" writes linear values into a
  dB-domain param (works only because Jukebox stops the sources). Safe precursor slice of §2.6.

### 1.9 Persistence/backend vestiges — **CONFIRMED**

- **PlayFab: 26 first-party files (3,351 LOC) + 68,413-LOC SDK, all inert** ("[PLAYFAB DISABLED]"
  early-returns), with a **~20-file live blast radius** outside the tree (Store, Port
  leaderboards, purchase/reward cards, HangarCaptainsView, DailyChallenge, ProfileModal, XpHandler,
  tests, editor tools). 5 of 7 CORE prefabs unplaced (`Instance` permanently null);
  `AuthenticationManager.prefab` alone still placed in `Authentication.unity`; `CaptainManager`
  still in `Bootstrap.unity` **and** AppManager DI. This cleanup is a *bug fix* (§0.1), not
  hygiene. One excision unit: tree + SDK + tests + dependent-UI ports to UGS equivalents.
- **`GameModeProgressionService` never instantiated** — the NEW progression chain's brain exists
  only on the orphaned DELETE-LATER prefab; `ParticipationXpAwarder` (its sibling) is wired
  directly in Bootstrap, showing the intended pattern. **Wire it into Bootstrap, then delete the
  folder.** (Smallest fix with the biggest player-visible payoff in this audit.)
- **Legacy quest chain half-dead:** `QuestSystem`/`UserJourneySystem`/`UserActionTrigger` fully
  dead (delete, incl. the never-consumed `SO_TrainingGame.SO_QuestChain` field);
  `UserActionSystem` + `CallToActionSystem`/`CallToActionTarget` are **live and load-bearing**
  (analytics funnel + CTA dots in Menu_Main, GameCard, 3 Hangar prefabs) — keep or SOAP-ify, don't
  delete.
- **Captain XP:** `CaptainProgressRepository`/`CloudData` finished but never registered in
  `UGSDataService` (absent from fields/factory/init/reset), while the dead PlayFab
  `XpHandler`/`CaptainManager` path stays DI-wired and consumed by live hangar UI. Collapse to one.
- **`LoginEventBus` + `TestLoginUI`:** fully dead PlayFab-era debug harness; fold into the PlayFab
  removal.
- **`TrailBlockBufferManager`** (superseded by `GenericPoolManager` — the pooling unification
  *did* complete; 7 wired subclasses measured) + **`ExplodableProjectile`** (100 % commented):
  delete. **`Hangar`** singleton: dead + orphaned CORE prefab: delete with its commented refs.
- **`FlowField` + `WarpField`:** parallel copy-paste SO vector-field systems — and **both are
  runtime-dead** (WarpField zero content; FlowField's sole presence is one un-read component in
  Menu_Main; consumers are unattached editor gizmos). Delete both families (13 scripts + 8 assets),
  or keep exactly one generalized base if a field fundamental is imminent. Bundled discovery:
  `_Scripts/Game/Environment/CapsuleMembrane.cs` is **live code** (consumed by `Cell.cs:63`)
  misfiled in the "vestigial, no C#" `_Scripts/Game/` tree — relocate it and fix the CLAUDE.md note.
- **Vessel animation:** `DolphinAnimation`, `SparrowAnimationController`,
  `SingleStickAnimationController` fully dead, while the *live* classes carry retired ship names
  (`BufoAnimation`=Grizzly, `RiptideAnimation`=Dolphin) and a typo'd `MantaAnimationContoller`
  wired into **six** prefabs. Delete the dead; rename-in-place (guid-preserving) the live.
- **Scoreboard stats providers — three generations:** per-mode HexRace/Joust/CrystalCapture
  providers fully dead; `UniversalStatsProvider` + editor + `IStatExposable` + 3 authored
  `StatModuleSO` assets are a **stillborn rival framework** (never wired anywhere);
  `EventDrivenStatsProvider` is what ships. Declare the winner, delete the other two generations,
  port WildlifeBlitz onto it.

---

## 2. Class B — Unify opportunities (two systems, one job, both running)

### 2.1 Scoring: the legacy per-player pipeline still runs *inside* the migrated scenes — **CONFIRMED, top priority**

The declared target (`Docs/ScoringSystem/REFACTOR.md`: "One unified, always-networked scoring
path… Clean out legacy, don't duplicate it") is half-landed. Measured today:

- **Joust and Crystal Capture scenes carry an enabled legacy `NetworkScoreTracker`** on the same
  GameObject as their migrated controllers — firing a duplicate results pass 500 ms after the
  authoritative `SyncFinalScores`/`SyncJoustResults` RPC, with Joust's tracker mis-configured
  `golfRules:0` (§0.1). HexRace's legacy tracker survives deliberately as the mid-turn elapsed-time
  HUD feeder + UGS reporter (its winner path is a no-op).
- The legacy pipeline is still **primary** for the non-migrated modes: CellularDuel (SP+MP),
  WildlifeBlitz (SP + co-op), MultiplayerFreestyle, 2v2CoOpVsAI.
- Removal caveat: in Joust/CC the legacy `TimePlayed`/`CrystalsCollected` strategies are what feed
  the mid-turn centerline score display — replace with a rule-driven feeder before deleting.
- R1 (`IsMultiplayerMode`) is ~half done: UI forks already gone, **2 behavioral reads left**
  (`MultiplayerSetup.cs:84` session gate, `HostConnectionService.cs:1860` presence) + RPC
  round-trip + 2 diagnostic reads + 7 writes. The ARCHITECTURE.md §8 fork map is **stale** (lists
  removed sites, misses 3 current ones) — refresh before the R1 design discussion.
- Also verified still-open: R6 legacy per-player HUD path in `MultiplayerHUD`, R10-D per-peer
  re-sort in the domain controllers' ClientRpcs, and the `BuildReveal` contract forcing every new
  rule to author a dead reveal payload (only `.Header` is consumed).
- **`DomainColorPaletteSO` regressed after R5 declared it deletable:** six Tournament/Connecting
  UI classes re-adopted it (TournamentSceneView *prefers* it over the theme), wired in 6 scenes +
  4 prefabs, plus 5 orphaned refs in SilhouetteConfig assets, plus inline hard-coded palettes in
  `ToyFactory`/`AstroLeagueBall`/`AstroLeagueArena` (AstroLeague's settings SO is missing gold
  entirely). Re-unify onto `ThemeManagerData.GetDomainUIColor`/`SO_ColorSet`, then delete.

**Sequence:** (1) de-wire the legacy trackers from Joust/CC with a rule-driven score feeder;
(2) migrate the four legacy-primary modes onto `ScoringRuleSO`; (3) delete the `BaseScoreTracker`
family + `ScoringModes` + strategies; (4) finish R1 off the corrected fork map. **Effort L,
sequenced with §2.2.**

### 2.2 One spawn pipeline + one controller spine — **CONFIRMED, the biggest remaining fork**

Two full player+vessel pipelines coexist, but the non-networked one survives in **exactly two
shipping scenes** (MinigameCellularDuel, MinigameWildlifeBlitz — plus the out-of-build benchmark
scene): `MiniGamePlayerSpawnerAdapter → PlayerSpawner → VesselSpawner →
InitializeForSinglePlayerMode` duplicates prefab-resolve/instantiate/DI-inject, pair-init,
identity resolution, and theming against the networked path everything else uses. The direction is
already declared in three places (`Player.cs:124` "No offline single-player: every session is a
Relay host", HexRace docs' single-scene model, ScoringSystem "solo = host + AI").

The controller trees fork the same way: `MultiplayerMiniGameControllerBase` re-implements the
whole lifecycle as an `Execute*`/`Sync*_ClientRpc` spine, so the "template method" base is only
genuinely the template for the SP branch — with measured drift (`MiniGameControllerBase.EndGame`
never calls `CalculateDomainStats`; the MP path does — SP games silently lack domain aggregation).
`MultiplayerCellularDuelController` already exists as the networked twin of the SP duel controller.

**Do:** port the two SP modes to the HexRace model (controller + `ServerPlayerVesselInitializerWithAI`),
then delete `PlayerSpawner`, `VesselSpawner`, `PlayerSpawnerAdapterBase`,
`MiniGamePlayerSpawnerAdapter`, the spawner prefab, `InitializeForSinglePlayerMode`, and the
SP controller branch; collapse the base so one server path owns the lifecycle. **Effort L; do
§2.1 and §2.2 as one program** — they touch the same scenes and sync paths.

### 2.3 `MenuMiniGameHUD` vs `MiniGameHUD` — **CONFIRMED**

The menu HUD hand-duplicates three behaviors (ShipHUD reparent loop, `DomainVolumeIndicator`
attach dance, local-HUD show/hide), kept in sync **only by cross-referencing comments** in each
file; a third dead copy sits in `GameCanvas.cs`. CLAUDE.md's "full MiniGameHUD can replace this
later" plan has no code representation (no shared base; `MiniGameHUD` hard-requires
`MiniGameHUDView` + game-flow events absent in the menu). **Do:** extract shared helpers or a slim
base so the documented migration path becomes real. **Effort M.**

### 2.4 Toast/notification plumbing ×4 — **CONFIRMED**

Honest verdict from measurement: the three *live* systems (ToastSystem in-HUD chat stack — 2
raisers; ToastNotification swipe stack — 11 raisers; GameEventFeed domain-colored kill feed — 4
raisers) have genuinely distinct visual roles; **do not force one surface**. The real duplication
is infrastructural: 4 payload structs, 4 channels in 2 incompatible styles (ToastChannel is a
plain C# event on a SO — the banned pattern), 3 near-identical settings SOs, 4 independent
queue/pool/DOTween implementations. Notification System (the fourth) has **zero senders** — delete
(§1.6). **Do:** one shared settings shape + one pooled item/tween core + one channel style (SOAP).
**Effort M.**

### 2.5 Effect-SO copy-paste families — **CONFIRMED**

The matrix already has the right pattern (`EffectsSO/Helpers`: `PrismEffectHelper`,
`ExplosionHelper`, `ResourceChangeSpec`, `HapticSpec`); three families ignored it:
(1) the element→"ElementXReceived" SFX switch, byte-identical ×3 (+1 variant);
(2) the all-element decaying buff/debuff pulse with per-vessel static cooldown dictionary,
duplicated ×2 — each copy an independent slow leak (never evicts destroyed `ResourceSystem`s);
(3) the victim input-mute debuff trio ×3; plus 4 spin effects re-deriving impact direction.
**Do:** `ElementSfxHelper`, `ElementalPulseSpec` (one cooldown map with eviction), `InputMuteSpec`,
optional `SpinHelper`. **Effort M.**

### 2.6 One audio pipeline — **CONFIRMED (judgment: split is not load-bearing)**

FMOD already runs in every scene; only content work keeps music on Unity `AudioSource`s
(`SO_Song` clips). The class header itself prescribes the migration; the split currently forces
`sfxBusPath` onto the FMOD **master** bus, which (a) blocks an independent music bus and (b) is
half of the slider² bug (§0.1) — four emitter controllers hand-copy per-instance slider math on
top of it (`FloraAmbientAudioController`'s copy is on a fully dead component). **Do:** dead-member
slice first (§1.8), then: FMOD music events + `bus:/Music`, convert Jukebox + the two
`PlaySFXClip` callers, delete the AudioSource/AudioMixer path, point SFX at `bus:/SFX`, and strip
per-instance slider math from the emitters. **Effort M.**

### 2.7 Per-mode UGS stats reporting — **CONFIRMED**

Every new mode currently needs: a scene-placed `*StatsReporter` (Joust/CC ~85 % identical; HexRace
does it a third way inside its tracker), a new `UGSStatsManager.Report<Mode>Stats` method (4
parallel), a clone `<Mode>PlayerStatsProfile` (Hex/Joust byte-identical modulo field name), a
`PlayerStatsProfile` field, and `LogControlWindow` special cases. The variation is fully
config-describable (metric, better-direction, winner rule — all already encoded by the mode's
`ScoringRuleSO`/golf flag). **Do:** one generic reporter + one `ReportModeResult(mode, intensity,
value)` API + one keyed best-value dictionary with CloudSave migration. **Effort M.**

### 2.8 Second `PrismType→pool` dispatcher — **ADJUSTED**

`BlockProjectileFactory` (live on Sparrow) duplicates `PrismFactory`'s dispatch. Correction from
verification: Get-without-Release is the documented conserved-mass norm, **not** a defect; the
genuine gap is that the *destroyed-prism recycle path* never works (`ReturnBlock` stub;
`BlockProjectilePoolManager` never wires `OnReturnToPool` the way `InteractivePrismPoolManager`
does). **Do:** fold into the `PrismFactory` channel, or implement provenance-stamped release.
**Effort M.**

### 2.9 Cloud persistence: finish or delete the mirrors — **CONFIRMED**

Four repos (Squad, Loadout, DailyChallenge, TrainingProgress) are constructed + awaited + flushed
**every sign-in with zero consumers**, while the live systems write `DataAccessor` local files
(Favorites has no mirror at all) — so exactly the state players expect to roam silently doesn't.
DailyChallenge is the worst case: **three backends for one domain** (PlayerPrefs live, PlayFab
dead, UGS built-but-idle) plus NRE paths (§0.1). **Do per domain:** finish the migration
(HangarRepo/VesselUnlockSystem is the worked example) with one-time local import, or delete the
repo + model + key. Don't keep both. **Effort M.**

### 2.10 Camera: pick the end state — **CONFIRMED (medium confidence on the unify option)**

What shipped is permanent coexistence, not a migration: gameplay follow is the hand-rolled
`CustomCameraController` (zero Cinemachine code in it, contra the migration doc); the menu is
Cinemachine, and the seam is paid for in live code (runtime bridge vCams +
`CinemachineMatchTargetOrientation` existing purely to hide the handoff pop). The abstraction is
fiction: `ICameraController` has one implementor and ~10 down-casts to the concrete class;
`ICameraConfigurator` has zero polymorphic call sites. **Do (decide once):** Option A — port the
follow behavior into Cinemachine and delete the seam machinery (L); Option B — declare coexistence
final, rewrite the doc, and either widen or delete the interfaces (S). Today's code pays for both
end states. **Effort S–L by option.**

---

## 3. Class C — Unenforced patterns (contracts held only by convention)

### 3.1 The fleet contract matrix — the audit's centerpiece — **ADJUSTED (counts verified)**

Measured per-vessel obligation coverage across the 11 `VesselClassType` members:

| Obligation | Coverage | Resolution mechanism (all unenforced) |
|---|---|---|
| Vessel prefab | 11/11 | `VesselPrefabContainer` maps only 6; other 5 only in `DefaultNetworkPrefabs` |
| `SO_Vessel` asset | 11/11 | 2 misfiled in `_SO_Assets/_TEMP/` (Shrike's has zero refs) |
| `ElementalAbilityMapSO` | 6/11 | `Resources.Load` by enum name; missing ⇒ **silent** null (`Multiplier()==1`) |
| `CameraSettingsSO` | 6/11 | serialized field on `VesselCameraCustomizer` |
| HUD view+controller+variant | 6/11 | component presence, runtime `LogWarning` only |
| Telemetry | 2/11 direct + 4 degraded | `VesselTelemetryBootstrapper` Awake switch, warns "stat SO refs will be null"; 5 have nothing |
| `elementBars` wiring | 2/11 (Sparrow, Squirrel; Rhino's field is null) | optional inspector field (auto-create fallback exists) |

Framing caveat from verification: ability maps and elementBars are *documented* opt-in rollouts —
"nothing enforces" is still true, but those two are by-design optional tiers. The rest are
accidents. **No editor validation, test, or manifest asserts any of this**; every gap is silent
until a vessel spawns.

**Do:** a fleet-contract edit-mode test / validation window that iterates `VesselClassType` and
asserts each obligation per tier (playable vs planned, flagged on `SO_Vessel`) — the exact move
that made the elemental display un-shippable-without. Relocate the `_TEMP` class SOs. **Effort M,
very high leverage — this is the enforcement pattern every other §3 item reuses.**

### 3.2 Per-vessel HUD MVC — **CONFIRMED**

6/11 wired; 5 prefabs ship `vesselHUDController: {fileID: 0}` (runtime warning only). The field is
a raw `MonoBehaviour` + runtime is-cast even though the project's own `[RequireInterface]` is used
**15 lines above** for `_shipInstance` (`VesselStatus.cs:46` vs `:60-69`). Two controllers misfiled
under `R_VesselActions/Data Containers/` (namespace drift too); Sparrow drops the `Vessel` infix.
**Do:** `[RequireInterface(typeof(IVesselHUDController))]`, move + rename the misfiled pair, add
the prefab-walking edit-mode test (fold into §3.1's). **Effort S–M.**

### 3.3 Domain-game end-game protocol — **CONFIRMED**

Five controllers (HexRace, Joust, CrystalCapture, NucleusRush, AstroLeague) hand-copy the same
convention: `HasEndGame=>false`, winner detection in `OnTurnEndedCustom`, parallel-array snapshot,
~35-line `Sync*_ClientRpc` ending in the canonical six-step tail (WinnerName → SortRoundStats →
CalculateDomainStats → SetResults → InvokeWinnerCalculated → InvokeMiniGameEnd). Enforced only by
comments; a sixth mode can forget any step (double game-end, desynced panels, stale results). The
domain base even shadows the countdown RPC verbatim. **Do:** hoist into
`MultiplayerDomainGamesController` — seal `HasEndGame`, one protected
`SyncFinalResults(winnerDomain, winnerName, metric[])` template, one shared ClientRpc tail; modes
supply only metric extraction (mostly already on `ScoringRuleSO`). **Effort M.**

### 3.4 Rule-delegated turn monitors — **ADJUSTED**

`NetworkCrystalCollisionTurnMonitor`, `NetworkJoustCollisionTurnMonitor`, and
`NucleusRushWaveTurnMonitor` are three hand-copies of the same trio (target resolve/publish;
`IsServer && rule.IsObjectiveReached`; `rule.Remaining` display) **including hand-copied B15
leak-safe `_subscribedStats` bookkeeping — a correctness-critical convention enforced by
comments**. (Correction: the NetworkVariable target-sync leg is only duplicated between Crystal
and NucleusRush; Joust's per-peer constant resolve is deliberate per its R10 comment.) The
non-network Crystal/Joust bases have zero content instances — dead branches kept as base classes.
**Do:** one `ObjectiveTurnMonitor` base owning the trio + the B15 lifecycle by type; collapse the
dead bases. **Effort M.**

### 3.5 Input strategy pipeline — **CONFIRMED (contains the live invert bug)**

`IInputStrategy` demands only lifecycle + `ProcessInput`; the four load-bearing behaviors
(XSum/YSum reparameterization ×5 copies, invert post-calc ×4-of-5 — **Touch missing it, live
bug**, §0.1 — the ~30-line speed/direction hysteresis ×5, the 6-block drift-composite edge logic
×3 + a divergent touch re-derivation) are copy-pasted per strategy. The MultiMouse stack is live
shipping code (constructed unconditionally), so `DualMouseInputStrategy` is a fourth live copy.
**Do:** template-method `BaseInputStrategy` — strategies override only `ReadDevice(...)`; base
owns reparameterize + inversion + effects + composites (~250 duplicated lines deleted, touch
invert fixed by construction). **Effort M.**

### 3.6 SOAP mandate vs ~60 static events — **CONFIRMED**

Measured under `Controller/` + `System/` (PlayFab excluded): ~60 `static event`s. Drift symptoms:
9 raised with zero subscribers (§1.5); **split-brain channels** — legacy actions and R_ executors
each declare `OnShotFired`/`OnVolleyFired`, telemetry hears only the executor side; **the same
concept on two channel technologies** — `OmniCrystalImpactor` raises SOAP
`ScriptableEventCrystalStats` while `ElementalCrystalImpactor` raises a static `Action<string>`
with 3 cross-system subscribers; `GameSetting`'s 9 statics (each a bespoke delegate + 7-line
PlayerPrefs ritual, subscribers in 10 files, one un-unsubscribable lambda block in analytics);
`DisplayGraphicsSettings` (7), `AccessibilitySettings` (5), `PauseSystem` (static class +
`Time.timeScale`, 20+ call sites) — already acknowledged as a deferred violation in
`BOOTSTRAP_AUDIT.md:44-45`. `PrismTeamManager.onPrismStolen` is the worked SOAP precedent.
**Do:** delete the 9 dead; unify crystal collection onto one channel; migrate `GameSetting` to a
`GameSettingsDataSO` (mirroring `HostConnectionDataSO`); add the enforcement test (no new
`static event` under `Controller/`). **Effort M–L, slice-able.**

### 3.7 Singleton vs DI dual-path — **CONFIRMED**

Same services reachable both ways, consumers split: `CameraManager` — DI-registered, **48
`.Instance` uses / 0 `[Inject]`** (the DI registration is effectively dead); `AudioSystem` — 23
`.Instance` alongside the CLAUDE.md prefer-`[Inject]` guidance; `GameSetting` — half-and-half;
`CaptainManager` — both patterns on one dead class. 28 classes extend `Singleton*`; the
self-creating prism-manager family (`PrismSpatialIndex` etc.) is a deliberate, working pattern —
whitelist it. **Do:** per service, pick one path and delete the other; enforcement test (no new
`: Singleton` outside the whitelist). **Effort M.**

### 3.8 Vessel/Ship naming skew — **ADJUSTED (no missing-script risk)**

14 `Ship*` type declarations remain under `Controller/Vessel`; 11 `*Vessel*.cs` files declare
`Ship*` types; 5 mismatched MonoBehaviours are attached in content. Verification correction: on
Unity 6 a single-class file needn't match its filename — nothing is at deserialization risk, and
the repo proves it (`R_ShipElementStatsHandler` dereferenced on every vessel init). The debt is
consistency/grep-ability + doc drift (CLAUDE.md already cites the *file* name
`R_VesselElementStatsHandler` as if it were the type). **Do:** rename types in place (guid-safe),
then the file==type edit-mode test. **Effort M, low risk.**

---

## 4. Sequencing recommendation

**Wave 0 — bug-fix-grade wiring (days):** wire `GameModeProgressionService` into Bootstrap; de-wire
the legacy `NetworkScoreTracker` from Joust/CC (with rule-driven score feeder); fix touch
invert-Y/throttle (or take §3.5 whole); decide+fix the daily-challenge/`Arcade.Instance` NRE
paths; prune the 5 launch-fail arcade cards.

**Wave 1 — pure deletions (low risk, high signal):** §1.2, §1.3, §1.5's dead items, §1.6, §1.7,
§1.8's dead items, §1.9's fully-dead items (TrailBlockBufferManager, Hangar, LoginEventBus,
Flow/WarpField, dead animations, dead stats providers, ElementPips). Each is a small PR of
verified-dead files. Delete the Wwise husk + fix CLAUDE.md (FMOD, stale file inventories,
`_Scripts/Game` note).

**Wave 2 — enforcement (the multiplier):** the fleet-contract edit-mode test (§3.1 + §3.2), the
file==type test (§3.8), the no-new-static-event / no-new-Singleton greps (§3.6/§3.7). Land these
before the big migrations so the migrations can't regress.

**Wave 3 — the two declared migrations, as one program:** unified scoring path (§2.1) + unified
spawn/controller spine (§2.2), then the `BaseScoreTracker`/`ScoringModes` family deletion and
`IsMultiplayerMode` retirement. This is the largest item and already has team-doc mandates.

**Wave 4 — consolidations:** domain end-game protocol hoist (§3.3) + turn-monitor base (§3.4);
input pipeline template (§3.5 if not done in Wave 0); effect helpers (§2.5); toast plumbing
(§2.4); HUD dedup (§2.3); stats reporting (§2.7); domain-color re-unification (§2.1 bullet);
audio migration (§2.6); persistence mirrors (§2.9); PlayFab excision (§1.9 — L, its own program).

**Wave 5 — decisions needing an owner:** camera end state (§2.10); dialogue runtime mount-or-archive
(§1.8); `CameraSettingsApplier` retention (§1.8); unshipped-vessel legacy actions port-or-drop
(§1.1 caveat).

---

## 5. Cross-reference: where this audit touches existing backlogs

| Existing doc | Relation |
|---|---|
| `Docs/ScoringSystem/REFACTOR.md` R1/R5/R6/R10 | §2.1 verifies status; **R5 regressed** (DomainColorPaletteSO returned); §8 fork map stale — refresh before R1 |
| `Docs/ScoringSystem/BUGS.md` B15 | §3.4 proposes enforcing the leak fix by type instead of comment |
| `Docs/ElementalAbilitySystem/AUDIT.md` §1.2 | §1.2 (dead binder) is its confirmed finding; delete now |
| `Docs/SettingsSystem/ARCHITECTURE.md` | §1.7 benchmark contradiction; §1.8 CameraSettingsApplier deliberate retention |
| `Docs/CameraMigrationReview.md` | §2.10 — doc describes a migration that became coexistence |
| `_Scripts/System/Bootstrap/BOOTSTRAP_AUDIT.md:44-45` | §3.6 — settings static events already flagged as deferred violation |
| CLAUDE.md | Corrections owed: Wwise→FMOD ×3, `_Scripts/Game` "no C#" (CapsuleMembrane), DomainAssigner/NetworkStatsManager file rows, HexRaceHUD.cs row, mode-32 scene table, "R_VesselElementStatsHandler" type name |
