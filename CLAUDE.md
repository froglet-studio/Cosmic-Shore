# CLAUDE.md — Cosmic Shore / Froglet Inc.

## Prime Directive

You are expected to work autonomously and persistently. Complete the entire task before stopping. Do not pause to ask for confirmation, approval, or clarification unless you are genuinely blocked on ambiguous requirements. If you encounter an error, debug and fix it yourself — attempt at least 3 different approaches before reporting the issue. Do not checkpoint, summarize progress, or ask "should I continue?" mid-task. Continue until all steps are done or you hit a hard wall.

When a task spans multiple files or systems, complete ALL of them in a single pass. Do not stop after the first file and ask if you should proceed to the next.

## Ecosystem Design Principles (LOCKED — read before any ecology change)

The cell ecosystem (flora/fauna/cells/crystals) is a **platform fundamental** on the path to
credible **artificial life**. North star + roadmap: `Docs/ECOSYSTEM_MASTERPLAN.md`. Mechanics
log: `Docs/ECOSYSTEM.md`. These invariants are **locked** — do not relitigate or re-derive them.
They are a direct application of "Favor Emergent Systems / Don't cheat emergence" (below) and —
not by accident — they are also what makes the system credible as artificial life (a scripted
outcome is optimization, not life). Use the `/ecology` skill for any change here.

- **Continuity of existence — nothing pops in or out (PLATFORM-WIDE LAW, all of Cosmic Shore).**
  Nothing may *instantly* appear or disappear. Every entity — prisms, crystals, flora, fauna,
  vessels, projectiles, even UI — must **grow / bloom / fade / suction / wither / evaporate** into
  and out of existence over a visible transition. A bare `Instantiate`-then-show or `Destroy` of
  anything the player can see is a bug. Spawns animate in (scale-from-zero / bloom); deaths animate
  out (wither from the extremities inward, suction toward a point, or fade). This is *why*
  starvation withers and mass is conserved — it is the same law applied to the ecosystem. It is not
  ecology-specific: respect it everywhere.
- **No imposed death.** No decay, lifespan, or fixed-period despawn timers. Populations are
  bounded by **consumption + starvation**, never an imposed clock. (Repeatedly rejected.)
- **No domain asymmetry.** Fauna spawn in **one color — the cell's controlling color**. Never
  cross-domain / prey-weighted / per-domain-biased spawning. The herbivore DIET is spatial in
  nucleus cells (see "Volume is the spine" below): outside the nucleus they graze **any**
  domain's mass voraciously; inside they eat **nothing**. Cells without a nucleus keep the
  legacy opposing-mass diet. **Shielded and super-shielded mass is never food, in any cell** —
  `Prism.Consume` is a no-op on super-shielded mass and only sheds the shield on shielded mass,
  so targeting one is a feed-hold the creature can never finish. Every herbivore edibility
  predicate routes through `Fauna.IsShieldedMass`; do not write a grazer that tests shield state
  itself. Shielded mass is likewise **not a steering target** — it is excluded from the cell's
  targeting grids (`Cell.AddBlock`, re-filed on any shield transition by
  `Cell.NotifyBlockShieldStateChanged`), because "fauna must never be led to mass they cannot
  eat" is one rule, not two. (`Docs/ECOSYSTEM.md §16`, `§22`.)
  A mode may redefine what "controls" a cell — Brood Rush makes it the nucleus claim (Ribcage
  pinned it to the race leader until its fauna were removed; `Cell.SetModeControlOverride`
  survives as the platform capability) — but the spawn colour is still
  exactly ONE colour, the controller's, and that setter also re-colours the LIVE swarm so a
  cell can never hold two fauna colours at once. A mode may also PEN a cell's fauna
  (`Cell.FaunaContainmentRadius`): outside the pen nothing is prey and every goal is
  clamped back in — a spatial diet + steering rule, never a wall, and never a cull.
  A biome's STARTING release state is authored data (`SpawnProfileSO.InitialFaunaReleaseTier`),
  not a runtime call — a runtime-only gate races the cell's own bootstrap and loses.
- **Starvation = wither-to-crystal.** A starving (or predated) creature withers from its extremity
  spindles inward — a shark's fins / a brittlestar's arms evaporate *before* the core body
  (farthest-from-centre first, emergent from geometry) — and leaves a collectible elemental crystal.
  It **does not vanish** (the continuity law above). **Mass is conserved** (the "self-sustaining
  economy" that makes the system NASA-credible). Sealed into `Fauna.Die` so no fauna can bypass it.
- **Volume is the spine.** Phase, dominant domain, prey, HUD all key off per-domain **VOLUME**
  (`Cell.LiveVolume`), not prism count. Count is a rare frenzy/perf backstop only.
  **Node control is the NUCLEUS**: in a cell with a nucleus, `DominantDomain` reads only the
  per-domain ENVIRONMENT volume laid **inside the nucleus** (the territorial claim — a fauna
  sanctuary players contest with abilities + out-laying volume); everything **outside** is the
  voraciously-grazed feeding ground and never sways control. The fauna spawner ticks a fixed
  **30s** wave clock (`BaseFaunaSpawnTime`), spawning each wave in the controlling color and
  raising `CellRuntimeDataSO.OnFaunaWaveSpawned` — the heartbeat Brood Rush scores on. See
  `Docs/ECOSYSTEM.md §13` + `_Scripts/Controller/Arcade/NUCLEUSRUSH.md`.
- **Every lifeform drops one elemental crystal** (Charge/Mass/Space/Time) as a powerup on death,
  enforced by `LifeFormCrystal`. It must not be possible to make a lifeform that violates this.
- **Territorial permanence.** Take a cell, leave, it stays yours — the claim fauna cannot touch.
  In nucleus cells the permanent claim is the **nucleus interior** (fauna never consume it);
  exterior canopy/trail is deliberately contested churn (voracious any-domain grazing). In
  nucleus-less cells the legacy rule stands: fauna eat only opposing mass, so the dominant
  canopy is never culled. Oscillation lives in the fauna churn *under* that constraint.
- **Endogenous selection only.** When evolution lands, fitness is **survival itself**
  (starvation/predation/reproduction cost), never a designer-scored fitness function — the line
  between artificial life and a mere optimizer, identical to "don't cheat emergence."
- **Collider budget is a hard gate.** No ecology feature ships without stating its active-collider
  impact; respect the per-cell budget (collider-LOD by phase + Burst density-grid fauna queries,
  not `Physics.OverlapSphere`). See `Docs/ECOSYSTEM_MASTERPLAN.md §4`.
- **The Cell owns the environment — minigames don't build parallel systems.** When a mode needs
  ecology, wire the standard **Cell** (`CellConfigDataSO` + `SpawnProfileSO`) and configure it; do
  **not** ship a mode-local duplicate of something the Cell already owns. The Cell's `MembranePrefab`
  is the playfield-boundary read, its `CytoplasmPrefab` (a `SnowChanger`) is the drifting
  atmosphere/motes, its `NucleusPrefab` is the core marker, its `SpawnProfile` is the population, and
  its `PhaseThresholds` are the phase/aggression ladder — a bespoke arena edge cage, plankton
  particle system, per-mode spawner, or mode-local culler is the same class of mistake as cheating
  emergence. A mode owns only its **gameplay-bearing** structure (physics walls a ball must bounce
  off, goal portals, a midfield ring). Tune the ladder in **volume** — modes whose vessel lays
  low-volume prisms (Squirrel trail ≈ 3.1 vol each, ~⅕ the nominal 16) must author explicit
  `*EnterVolume`/`*ExitVolume` (else the ×16 count-derivation sets the ladder ~5× too high and fauna
  never hunt) and lower `SpawnProfile.FaunaFoodFloor` so herbivores seed against the thinner prey.
  Full table + rationale: `Docs/ECOSYSTEM_MASTERPLAN.md §5.1`.
  **Corollary — never hand-place a membrane/nucleus/cytoplasm in a scene.** The Cell instantiates
  each of them itself in `SpawnVisuals` from the config, and *only* that instance is tracked: every
  nucleus consumer (`NucleusWorldRadius`, `RefreshNucleusControlRadius`, `IsInsideNucleus`,
  `SetNucleusWorldRadius`) reads the Cell's private `nucleus` field, and the cleanup/swap paths read
  `membrane`/`nucleus`/`spawnedCytoplasm`. A scene-placed copy is therefore a *pure* duplicate — it
  renders on top of the real one and no bookkeeping can see it (three scenes shipped a coincident
  `Nucleus.prefab` this way). Same rule inside `Cell` itself: every spawn in `SpawnVisuals` plus
  `SpawnCytoplasm` is guarded on its own field, because a repeat `Initialize` pass overwrote the
  field and orphaned an untracked membrane/nucleus/`SnowChanger` that no cleanup path could reach.
  **Anything placing objects relative to the core during the SPAWN CHAIN must read
  `Cell.ExpectedNucleusWorldRadius`, not `NucleusWorldRadius`, and resolve the cell with
  `Cell.FindByRuntimeData` rather than `CellRuntimeDataSO.Cell`** — `Cell.Initialize` runs on
  `OnInitializeGame` behind `InitDelayMs` (1000 ms) while vessels spawn at `preSpawnDelayMs`
  (200 ms) and AI at `OnNetworkSpawn`, so both the field and the radius are still empty then. That
  race shipped once: the player spawn ring silently fell back and put everyone 70u from the centre,
  inside the nucleus. **To change a
  Cell-owned visual's size, author a new `CellConfigDataSO` pointing at a resized prefab** (Scurry's
  `Scurry Cell Config` → `HalfNucleus.prefab`) — do not place, scale, or duplicate one in a scene.
  Guarded by **FrogletTools > Ecology > Audit Cell-Owned Visuals**, which also sweeps the dead
  `Cell` overrides scenes accumulate (72 of them across 12 scenes on the day it was written).
  Note a scene backdrop is NOT this: `SkyboxModel` (`MembraneBase`/`BigMembraneVariant`) is a
  different asset from any config's `MembranePrefab` and is the only geometry in the tool scenes.
- **A world you load is opt-in, and swapping one is ACTIVE removal — not decay.** An authored
  `EnvironmentPrefab` costs a multi-second veiled build, so a scene may boot
  `CellTypeChoiceOptions.EnvironmentFree` (the first config with no environment — Menu_Main does)
  and let the heavy worlds be chosen on demand. The one runtime entry point is
  `Cell.RequestCellSwap` (the freestyle **Cell Selector** toy): it **suctions** the old world away
  and **blooms** the new one in behind the standard `EnvironmentLoadVeil` — continuity of existence
  holds at both ends — and it removes mass only because a player flew into a station and asked, the
  same explicit, active event class as a scene load. **Do not** turn this into anything that runs on
  its own: no auto-rotate, no idle re-roll, no "the cell has been up too long" reset. That would be
  the timed culler §0 rejects, wearing a new costume. Detail: `Docs/ECOSYSTEM.md §19`.

**Protocol:** (1) restate which invariants the change touches + confirm none are violated;
(2) confirm at genuine forks (AskUserQuestion); (3) implement surgically, config-driven; (4) state
the collider-budget impact + exact in-editor verification. The `/ecology` skill encodes this.

## About This Project

Cosmic Shore is a multigenre space game ("the party game for pilots") developed by Froglet Inc., a Delaware C-corp based in Grand Rapids, MI. Different vessel classes embody gameplay from different genres to connect players across demographics.

### Vessel Classes

The game features 11 vessel class types (defined in `Assets/_Scripts/Data/Enums/VesselClassType.cs`):

| Vessel | ID | Genre / Role |
|---|---|---|
| **Manta** | 1 | Feature-complete playable vessel |
| **Dolphin** | 2 | Feature-complete playable vessel |
| **Rhino** | 3 | Feature-complete playable vessel |
| **Urchin** | 4 | Playable vessel (AI in progress) |
| **Grizzly** | 5 | Playable vessel (AI in progress) |
| **Squirrel** | 6 | Racing/drift — vaporwave arcade racer, tube-riding along player-generated trails (F-Zero / Redout feel) |
| **Serpent** | 7 | Playable vessel with dedicated HUD |
| **Termite** | 8 | Planned |
| **Falcon** | 9 | Planned |
| **Shrike** | 10 | Planned |
| **Sparrow** | 11 | Shooter — arcade space combat with guns and missiles |

Meta values: `Any (-1)`, `Random (0)`

**Use the `/vessel` skill for ANY vessel-class work** — new vessels, abilities/executors,
elemental ability maps + level-5 upgrades, HUD rows/hints/gauges, petal bars, hull morphs/rig
swaps, impact/skimmer containers. It loads the fleet-wide vessel contract, the audit tools, and
the per-subsystem checklists so the requirements are not re-derived per vessel.

### Team Domains

Team ownership is tracked via the `Domains` enum: `Jade (1)`, `Ruby (2)`, `Blue (3)`, `Gold (4)`. **Blue is the "no team / not yet picked / neutral entity" sentinel** and is never present in `GameDataSO.ActiveDomains` (the playable set is `{Jade, Ruby, Gold}`, indices 0..2). Code that previously used `Domains.None` or `Domains.Unassigned` (both removed) now uses `Domains.Blue` for the same "no specific team" semantic — neutral mines, uncommitted crystals, the wildcard "any team" density-grid bucket, and players who haven't yet picked a domain.

Cross-client domain sync is driven entirely by `Player.NetDomain` (server-write `NetworkVariable<Domains>`). Its replication callback `Player.OnNetDomainChanged` propagates every change to:

1. The local `Player.Domain` mirror (read by `IVesselStatus.Domain` and many UI consumers).
2. `RoundStats.Domain` — a local mirror kept in sync on EVERY peer (its `n_Domain` NetworkVariable was retired, see `Docs/ScoringSystem/BUGS.md` B10) — keeps scoreboards, end-game controllers, and `GameToastAPI` colorers live across modal re-picks, `NormalizeUnassignedHumans` rerolls, and shape-mode `SetDomain`.
3. The vessel materials via `ShipHelper.SetShipProperties(_vesselThemeManagerData, Vessel)` — the theme reference is stashed onto `Player` by `ClientPlayerVesselInitializer.InitializePair`/`ReInitializePair` at vessel spawn/swap.

Do not snapshot domain at component-creation time. Either subscribe to `Player.NetDomain.OnValueChanged` directly or read the live `Player.Domain` mirror each time you need it. `RoundStats.Domain` is also live (after Phase 5) so end-game UIs can keep using it.

**Never write domain state from client code.** `NetDomain` is Server-write (clients request picks via `Player.RequestSetDomain_ServerRpc`), and the `Player.Domain` / `RoundStats.Domain` mirrors sync ONLY from `NetDomain` (`InitializeForMultiplayerMode` + `OnNetDomainChanged`) — a local overwrite desyncs that machine until the next NetDomain delta (`Docs/ScoringSystem/BUGS.md` B10/B11). The menu's Jade reset is server-side in `MenuServerPlayerVesselInitializer.OnPlayerReadyToSpawnAsync` (the deleted client-local `ApplyMenuDomain` was the root of `Docs/PartySystem/BUGS.md` B9). `ShipHelper.SetShipProperties` is init-aware: it swaps the material references and, once `VesselCustomization.Initialize` has painted the hull, also re-applies the mesh material — so a replicated domain change fully re-themes the vessel with no extra calls.

`ServerPlayerVesselInitializerWithAI.GetBalancedDomain` ties break by `ActiveDomains` enum order (Jade → Ruby → Gold), not RNG, so identical inputs produce identical AI distributions across machines without needing a shared seed.

### Tech Stack

- **Engine**: Unity 6+ with URP (Universal Render Pipeline) — `com.unity.render-pipelines.universal` 17.0.4
- **Language**: C# with UniTask (`com.cysharp.unitask`) for async
- **Architecture**: ScriptableObject-driven config separation + SOAP (Scriptable Object Architecture Pattern) for cross-system communication
- **Networking**: Unity Netcode for GameObjects (`com.unity.netcode.gameobjects` 2.5.0)
- **Camera**: Cinemachine 3.1.2 with per-vessel `CameraSettingsSO` assets
- **VFX**: VFX Graph 17.0.4, custom HLSL shaders, Shader Graph
- **Input**: Unity Input System 1.14.2 with strategy pattern (`IInputStrategy` → platform-specific implementations)
- **Audio**: FMOD Studio integration (`Assets/Plugins/FMOD`)
- **Haptics**: NiceVibrations for mobile/gamepad haptics. **Two everyday feels**, both local-human-pilot-only (skim-pulse reward + prism-punish thud), plus **one rare alert shake** fenced to match-changing events (only Ribcage's two progress-milestone rungs today); everything else is silent. See `Docs/HAPTICS.md`.
- **Animation**: Timeline 1.8.9, DOTween for procedural animation
- **DI**: Reflex (`com.gustavopsantos.reflex` 14.1.0) for dependency injection
- **Performance**: Unity Jobs + Burst Compiler, Adaptive Performance 5.1.6, DOTS Entities 1.4.2 (installed, incremental adoption)
- **Backend**: PlayFab SDK (legacy, inert), Unity Gaming Services (Analytics, CloudSave, Leaderboards, Multiplayer, Purchasing 4.12.2, Ads 4.12.0)
- **Testing**: Unity Test Framework 1.6.0 (NUnit-based)
- **Target**: Mobile-first with PC/console expansion

## Project Structure

```
Assets/
├── _Scripts/                  # All first-party code (~1,100 C# files)
│   ├── Controller/            # Gameplay systems (~536 files)
│   │   ├── Vessel/            # Vessel core: VesselStatus, Prism, Trail, VesselPrismController, VesselActions/, R_VesselActions/
│   │   ├── Environment/       # Cells, crystals, flora/fauna, flow fields, warp fields, spawning
│   │   ├── ImpactEffects/     # Impactors (11 types) + Effect SOs (20+ types)
│   │   ├── Arcade/            # Mini-game controllers, scoring, turn monitors
│   │   ├── Projectiles/       # Projectile systems, guns, mines, AOE effects
│   │   ├── Managers/          # PrismStateManager, PrismTimerManager, PrismSpatialIndex, ThemeManager
│   │   ├── IO/                # Input strategies (Keyboard, Gamepad, Touch)
│   │   ├── Animation/         # Per-vessel animation controllers
│   │   ├── Camera/            # CustomCameraController, CameraSettingsSO, ICameraController
│   │   ├── Multiplayer/       # Netcode: ServerPlayerVesselInitializer (+ WithAI, Menu variants), ClientPlayerVesselInitializer, MultiplayerSetup, MenuCrystalClickHandler, DomainAssigner, NetworkStatsManager
│   │   ├── Player/            # Player (NetworkBehaviour), IPlayer, RoundStats
│   │   ├── Prisms/            # PrismFactory
│   │   ├── Assemblers/        # Gyroid/wall assembly systems
│   │   ├── Party/             # HostConnectionService, PartyInviteController, FriendsInitializer
│   │   ├── AI/                # AIPilot, AIGunner
│   │   ├── FX/                # Visual effects controllers
│   │   ├── ECS/               # DOTS entity components
│   │   ├── XP/                # Experience point controllers
│   │   └── Settings/          # Runtime settings
│   ├── System/                # Application-level systems (~126 files)
│   │   ├── Bootstrap/         # BootstrapConfigSO, SceneTransitionManager, ApplicationLifecycleManager
│   │   ├── Playfab/           # PlayFab integration (Auth, Economy, Groups, PlayerData, PlayStream)
│   │   ├── Instrumentation/   # AnalyticsServiceFacade (UGS Analytics, single writer)
│   │   ├── Runtime/           # Dialogue runtime (DialogueManager, models, views, helpers)
│   │   ├── RewindSystem/      # Rewind/replay functionality
│   │   ├── Audio/             # FMOD audio management (AudioSystem, Jukebox)
│   │   ├── LoadOut/           # Vessel loadout configuration
│   │   ├── CallToAction/      # Promotional/CTA system
│   │   ├── Squads/            # Squad management
│   │   ├── Quest/             # Quest system
│   │   ├── UserAction/        # User action tracking
│   │   ├── UserJourney/       # Funnel analytics
│   │   ├── Favorites/         # Favorites system
│   │   ├── Ads/               # Ad integration
│   │   └── Architectures/     # Shared architectural base classes
│   ├── UI/                    # Game & app UI (~188 files)
│   │   ├── Controller/        # VesselHUD controllers (Manta, Rhino, Serpent, Sparrow)
│   │   ├── View/              # VesselHUD views (all vessel types + Minigame, Multiplayer)
│   │   ├── Interfaces/        # IVesselHUDController, IVesselHUDView, IMinigameHUDController, IScreen
│   │   ├── Elements/          # Reusable UI components (NavLink, NavGroup, ProfileDisplayWidget, etc.)
│   │   ├── Views/             # Screen/view implementations (VesselSelection, Profile)
│   │   ├── Modals/            # Modal dialogs (Settings, Profile, PurchaseConfirmation)
│   │   ├── Screens/           # Screen containers
│   │   ├── ToastSystem/       # ToastService, ToastChannel, ToastAnimation
│   │   ├── Notification System/ # Push notification UI
│   │   ├── GameToastSystem/   # In-game toast feed (situation SOs, per-mode configs, idle hints)
│   │   ├── FX/                # UI visual effects
│   │   └── Animations/        # UI animations
│   ├── Data/                  # Models & enums (~29 files)
│   │   ├── Enums/             # VesselClassType, Domains, ResourceType, ShipActions, InputEvents, etc.
│   │   └── Structs/           # DailyChallenge, GameplayReward, TrainingGameProgress
│   ├── ScriptableObjects/     # SO definitions & SOAP types (~70 files)
│   │   ├── SOAP/              # Custom SOAP types (16 subdirectories)
│   │   └── SO_*.cs            # Game data SOs (Captain, Vessel, Game, ArcadeGame, Element, etc.)
│   ├── Utility/               # Effects, PoolsAndBuffers, DataContainers, DataPersistence, ClassExtensions
│   ├── DialogueSystem/        # Dialogue editor tools, animation, SO assets
│   ├── Editor/                # Editor tools (CopyTool, shader inspectors, scene utilities)
│   ├── Tests/                 # Edit-mode unit tests
│   ├── Integrations/          # PlayFab SDK integration
│   └── SSUScripts/            # Specialized subsystem scripts
├── _SO_Assets/                # ScriptableObject asset instances (48+ subdirectories)
├── _Prefabs/                  # CORE, Cameras, Characters, Environment, Pools, Projectile, Spaceships, Trails, UI Elements
├── _Scenes/                   # Game scenes organized by type
├── _Graphics/, _Models/, _Audio/, _Animations/
├── FTUE/                      # First-Time User Experience / Tutorial system
├── Plugins/                   # Obvious.Soap, Demigiant (DOTween), NativeShare, etc.
├── PlayFabSDK/                # Backend SDK (legacy)
├── NiceVibrations/            # Haptic feedback
└── SerializeInterface/        # Custom [RequireInterface] attribute support
```

Note: A vestigial `_Scripts/Game/` directory exists containing mostly non-code assets (compute shaders, input action mappings, material files, and the `PRISM_PERFORMANCE_AUDIT.md`) plus two live scripts pending relocation (`Game/Environment/CapsuleMembrane.cs` + `CapsuleMembraneAnimationSO.cs`, consumed by `Cell`). All other C# code has been reorganized into the directories listed above.

### Assembly Definitions

All first-party gameplay code compiles in Unity's default assembly (no runtime `.asmdef` files). Only test assemblies have explicit assembly definitions:

| Assembly | Scope |
|---|---|
| `CosmicShore.Bootstrap.Tests` | Bootstrap unit tests |
| `CosmicShore.Multiplayer.Tests` | Multiplayer unit tests |
| `CosmicShore.PlayFabTests` | PlayFab integration tests |
| `CosmicShore.Tests.EditMode` | General edit-mode tests |

Third-party assemblies: `Obvious.Soap`, `PlayFab`, `Lofelt.NiceVibrations`, `NativeShare.Runtime`

### Scenes & Game Modes

Full scene inventory, `GameModes` enum reference, controller hierarchy, and launch
pipeline: `Docs/SCENES.md`. The always-true rules:

- **No single-player scenes**: solo play is a multiplayer game whose party is one host
  (eager Relay session + AI backfill via `ServerPlayerVesselInitializerWithAI`). The
  single-player controller branch and non-networked spawn path were deleted 2026-07-20.
- **GameModes IDs are never reused** (`Assets/_Scripts/Data/Enums/GameModes.cs`): retired
  IDs stay annotated do-not-reuse (7 and 31 are skipped; highest is `Benchmark(40)`).
  `Tournament(36)` is the session-level meta (player-facing name "Maelstrom"); freestyle
  lives ONLY in Menu_Main as the lava lamp - `Freestyle(7)` and `MultiplayerFreestyle(28)`
  are retired and must not be reintroduced. **Exception — `Rampage(2)`**: the legacy solo
  ID was deliberately *repurposed* as a live multiplayer party game (the destruction race,
  Scurry's destructive analog; see `_Scripts/Controller/Arcade/RAMPAGE.md`). It is the one
  reused ID; do not treat mode 2 as retired. `Ribcage(39)` (display name "Peel the
  Cage") is the Rhino-only layered-cage destruction race - first domain to destroy the
  hostile-prism target (2000, the same metric as Rampage) wins, and intensity is how
  many rinds you peel (see `_Scripts/Controller/Arcade/RIBCAGE.md`). `Benchmark(40)` is
  NOT an arcade mode - it is the Settings > Run Benchmark stress-test context (no card,
  no scoring, endless), set by `BenchmarkSceneLauncher` so mode-keyed consumers resolve
  honestly instead of borrowing a retired id.
- **Controller skeleton**: `MiniGameControllerBase` → `MultiplayerMiniGameControllerBase`
  → `MultiplayerDomainGamesController` → per-mode controllers (server-authoritative
  turn/round/game flow via ClientRpc), incl. `RampageController` + `RibcageController`
  (both prisms-destroyed scoring).
- **Launch pipeline**: `SO_ArcadeGame` (static config) → `ArcadeGameConfigureModal` →
  `GameDataSO` (SOAP runtime state) → `SceneLoader.LaunchGame()` (host-driven Netcode
  scene load) → scene-placed controller; config syncs to clients in
  `MultiplayerMiniGameControllerBase.OnNetworkSpawn()`.

### Documentation Index

| Document | Location | Content |
|---|---|---|
| `CLAUDE.md` | Project root | Architecture, patterns, systems reference |
| `SCENES.md` | `Docs/` | Complete scene inventory, game modes, launch pipeline |
| `HAPTICS.md` | `Docs/` | The two-feel haptics policy (skim-pulse reward + prism-punish thud), the one-clip priority/rate-limit gate, runtime `.haptic`+GamepadRumble clip generation, local-pilot gating, and in-editor verification. **Read before adding or re-enabling any haptic.** |
| `THREADING.md` | `Docs/` | UniTask / SyncContext threading rules, `.AsMainThread()` contract, `MainThreadDispatcher`, canary, history |
| `PALETTE.md` | `Docs/` | The domain colour set (`SO_ColorSet`): which asset is live, what `_DarkColor`/`_BrightColor` actually are (prism **base face** vs **fresnel rim** — "Outside/Inside" is a misnomer), the **linear-HDR colour-space rule** (Rec.709/CIELAB apply directly; scaling a pair changes brightness but NOT contrast), and the measured shielded-tier contract (ΔL\* 29.34 across all domains). **Read before editing any `*BlockColor` field.** |
| `CONDITIONAL_COMPILATION.md` | `Docs/` | `#if UNITY_EDITOR` / `DEVELOPMENT_BUILD` rules, the two safe guard patterns, and the `Tools/Build/check_conditional_compilation.py` CI gate. **Read before writing ANY script that uses a compilation guard or the `UnityEditor` namespace** — this class of mistake has broken the automated build repeatedly and is invisible in the Editor. |
| `SPATIAL_INDEX.md` | `Docs/` | `PrismSpatialIndex` — THE canonical spatial index of prism mass (Burst AOE queries, growth occupancy reservations, bucket grid). **Read before adding any spatial query against prisms.** |
| `PRISM_ANIMATION.md` | `Docs/` | **The clock-material law (LOCKED, STRICT — no legacy fallback)**: no prism may need multiframe CPU updates to animate — animation = pool-pull + one initial-conditions stamp + GPU runs the course off the shader clock + one scheduled end-state swap; colliders and gameplay state go FINAL at the start. There is NO CPU animation tier to fall back to: an unwired graph fails LOUD (`PrismClockDiagnostics`) and the visual snaps until the §4.4 wiring lands (in-editor checklist: `Docs/PRISM_CLOCK_WIRING_CHECKLIST.md`; validator: FrogletTools > Ecology > Prism Animation). Full audit of every prism update path+ migration tracker. §4.7 documents the ONE sanctioned shape for view-dependent prism visuals — a GLOBAL uniform published once per frame, never a per-prism write — and states the camera↔vessel occlusion corridor as a **PLATFORM LAW** with the four layers that make it un-authorable to skip. **Read before touching any prism visual, animation, or state transition.** |
| `PERFORMANCE_OPTIMIZATION.md` | `Docs/` | Frame-cost optimization log + prioritized backlog: shipped de-spike commits (do-not-regress list), the locked slice + per-frame budget + atomic publish fix pattern, instrumentation inventory (markers, DiagnosticsHUD, telemetry), per-task root-cause analyses with verified file/line refs, standing verification protocol. **Read before any perf work.** |
| `PartySystem/` | `Docs/` | Party (Relay) layer: `ARCHITECTURE.md` (locked design, investigation Q&A, error-handling matrix, exit criteria), `REFACTOR.md` (active backlog + deferred items + per-commit protocol), `BUGS.md`, `TESTS.md`, `TODOS.md`. EAGER per-user Relay session is the locked design. |
| `PresenceSystem/` | `Docs/` | Presence-lobby (discovery) layer: `ARCHITECTURE.md`, `REFACTOR.md`, `BUGS.md`, `TESTS.md`, `TODOS.md`. Lobby-only UGS session, coexists with NetworkManager. |
| `NetworkDiagnostics/` | `Docs/` | NetDiag overlay: `ARCHITECTURE.md` (NetworkMonitor + `NetworkDiagnostics` helper, classification rules), `TESTS.md` (Tests A-E), `TODOS.md`. |
| `ScoringSystem/` | `Docs/` | Scoring system (in-game score HUD + final scoreboard): `ARCHITECTURE.md` (shared data layer, event dispatch, per-mode override table, target = one unified networked scoring path), `REFACTOR.md` (sequenced backlog + ground rules: SOAP/observer/SOLID/DRY/KISS; `IsMultiplayerMode` retired 2026-07-20), `BUGS.md`, `TESTS.md`. |
| `TournamentSystem/` | `Docs/` | Tournament mode (`GameModes.Tournament = 36`): `ARCHITECTURE.md` — session-level meta chaining the three domain minigames (HexRace → Joust → Crystal Capture) via sequential `Single` loads; network-free standings folded from the synced `GameDataSO.Results` by the persistent `TournamentController`; host-only Continue→hub→Summary end-game flow (summary-vs-hub keyed off the authoritative `IsShuffleComplete`, race-to-6); `TournamentDataSO` data + file index. |
| `ToySystem/` | `Docs/` | Freestyle **Toy** system (the new `Toy` fundamental): `ARCHITECTURE.md` — world-space interactive stations the local vessel flies into (no score, no end condition), placed near the Cell membrane in Menu_Main. Toys are either a `MatrixToy` (ONE station that unfolds into a matrix of choices out along the outward radial and folds away on the next pass — cell selector, painting gallery, vessel changer) or a shared `SwapToySetCoordinator<T>` "flip-set" for small universes (each toy is the option it switches you to; the used one flips to your previous option — the domain changer) — Vessel Changer (mini ship models via `VesselModelBuilder`, reuses `RequestSwap` + restores freestyle control), Domain Changer (two toys tinted the domains you're not, `RequestSetDomain_ServerRpc`), and the "Connect the Dots" Painting toy — a gallery of painting stations (`PaintingToyDefinitionSO` → one `PaintingToy` per `PaintingDefinitionSO`), each running a multi-stroke, multi-domain `PaintingRunner`: per-stroke start gates recolour the trail via `RequestSetDomain_ServerRpc`, pen-up between strokes via `VesselPrismController.SetSpawnerPaused`, shared trail-toy shape language (cones = trail-on pointing at the next point — also worn by the Domain Changer; jacks = stroke-end trail-off; both in the domain prism material), stroke progress AND per-prism drawing state resume across vessel swaps/game modes/sessions (`PaintingProgressStore` + `PaintingPrismStore`, saved prisms regrow via the PrismFactory channel), completion SHARE/REPAINT gates with a self-contained WebGL share export (`PaintingShareExporter` + NativeShare), a 16-painting gallery (on-ramp Star → Rainbow → Saturn → Taj Mahal, then 12 grandiose non-planar constructions — Torus Knot, Buckyball, Double Helix, Nautilus, Lotus, Rose, Spiral Galaxy, Phoenix, Almighty Mountain, Starry Night, Lion's Head, Peacock — composed from `PaintingStrokeToolkit`: deterministic curves + a divergence-free curl "3D-impressionist" field; stroke order is computed at runtime by `OrderForFlightContinuity` — each stroke starts near the previous stroke's end, domain-contiguous, curvier strokes deferred on near-ties) — plus the **Wanderway microscene conveyor** (`ConveyorToy` + `WanderwayRun` + `WanderwayReturnToy` + `MicrosceneConveyor` + `Microscene` + `MicroscenePatterns` + `MicroscenePatternsGrand` + `MicroscenePainter`): a toy you fly into to LEAVE for a wander — the run reverts the host cell to its bare environment-free (Blob) config through `Cell.RequestCellSwap`, then streams a speed-scaled field of procedurally-varied microscenes ahead of your flight path anywhere you fly, recycling the scene farthest behind into a fresh arrangement ahead — a *closed* system that transports a fixed stock of conserved prisms. **Grand scale (shipped 2026-08-02):** the belt's whole stock — `poolSize × prismBudgetPerScene`, **20 × 1500 = 30,000 prisms**, the same order as an authored cell environment — is built ONCE on the first pass through the toy, behind the same `EnvironmentLoadVeil` + arena-ready gate the Cell Selector uses for a world swap (`MicrosceneConveyor.PrimeAsync` → `PrismTrailBuilder.LayBudgetedAsync`); after that it never instantiates again. 48 recipes in two families: the **classic forty** (gate runs, tunnels, orchards, menageries, shingled domes, torus knots, Möbius rails, banked ribbon chicanes, spine×motif "Medley" composers, …), hand-tuned at `MicroscenePatterns.DesignRadius` and scaled bodily (POSITIONS only — never prism scales, so a bigger belt does not inflate per-prism volume into the host cell's phase ladder), and the **grand eight** (`MicroscenePatternsGrand`: Cathedral, World Tree, Orrery, Sunken City, Leviathan, Geode Vault, Aurora Veil, Hypersphere), which take the scene radius as their basis and multiply their part counts with the budget — borrowing the construction idioms of the freestyle six cell environments. The recycle is fully clock-driven (`Docs/PRISM_ANIMATION.md` §5 C8): collapse = one grow-clock re-stamp per prism, transport = hide + ONE container transform write (legitimate only because the off-screen removal gate proves it unseen), bloom = the standard creation stamps — the old per-frame container scale + per-prism spatial/entity re-sync (~180k writes per recycle at this scale) is deleted. **The run (shipped 2026-08-02)** makes the wander a place you go to and come back from: your trail becomes a **rolling tether** — a ribbon of exactly `tetherPrisms` (100) that follows you, the tail withering and RECYCLING into the pool the head lays from — with the **return station riding that tail**, so the way home is always one tether-length behind you. That recycle is the **one authorized exception** to *Mass is conserved* (explicit sign-off, 2026-08-03): it buys a truly infinite runner at fixed memory, and it is fenced to a live run — `WanderwayRun.RollTether` is the sole caller of `Trail.RemoveOldest`, `VesselPrismController` grew no cap field, and outside a run the law holds in full. Continuity of existence is NOT waived: a retiring prism withers on the grow clock and only then returns to the pool. Full record: `Docs/ECOSYSTEM.md` §0 — do not generalise it, do not revert it. Three exits call the same `WanderwayRun.End`: that station, another pass through the toy, and the **overview button** / gamepad Start (which drop freestyle — the run watches `ToyContext.IsFreestyleActive` for the edge, so no new wiring). Ending a run stops the belt, clears the pen, and repositions the vessel home via `IVessel.SetPose`; the belt's scenes and the Blob cell stay (restoring a world is the Cell Selector's job). It paints every scene structurally from the full domain triad (per-structure rainbows, gradients, pinwheels) with danger/shielded/supershielded prisms as capped palette tools, lays skimmable elemental crystals, and releases flora/fauna into the containing cell as ordinary citizens. `ToyboxSO` registry + deferred unlock-state hook; `ToyboxController` self-wires (Resources/default fallback); `FrogletTools > Scene Setup > Setup Freestyle Toybox` authors assets + wires the scene. **Second pass (shipped):** `VesselModelBuilder` hull-filters the skimmer sphere + paints an opaque domain-tinted preview material (all six ships render, not just Rhino); `Toy` re-arms only after the vessel flies clear + the flipped toy re-grows slowly (can't switch you back before you escape); a vessel swap keeps your domain (`ReInitializePair` re-syncs `Player.Domain` from `NetDomain` before repaint) and inherits pose + speed (`IVessel.SetInitialSpeed`) and re-shows the HUD (`OnPlayerPairInitialized`); mini ships recolour on any domain change (`SwapToySetCoordinator.OnTick`); gamepad **Start** exits freestyle and `EventSystem.sendNavigationEvents` is off in freestyle so the pad stops double-driving the UI. **Cell Selector pass (shipped):** the freestyle six cost an `EnvironmentLoadVeil` hold on EVERY entry to Menu_Main (boot and every return from an arcade game), so the Cell now boots `CellTypeChoiceOptions.EnvironmentFree` (the first config with no `EnvironmentPrefab` — Blob: no build, no veil) and the six heavy worlds become OPT-IN through `CellSelectorToy` + `CellSelectorToyDefinitionSO`: fly the toy and a matrix of mini-cells blooms outward (the Lifeform Matrix pattern, now sharing `ToyMatrixStation`), each slot a bare genuine SCALE MODEL of the world it creates (no cage, no orb — the model speaks for itself) — `CellMiniatureBuilder` strides the generator's own output (`GetTrailData` + the new `CellEnvironmentSpawnableBase.CachedLays` for per-prism domain) into one mesh with a submesh per domain, spawning NO prisms, streamed one per frame and released after sampling; fly a mini-cell and `Cell.RequestCellSwap` suctions the old world away, drains it 500 prisms/frame, and grows the chosen one back behind the standard veil — picking the cell you are already in IS the freestyle reset (it also retires the pooled trail mass). The toy authors no cell list: it reads `Cell.AvailableConfigs`. `BACKLOG.md` tracks per-toy follow-up (own branches) + known limitations. |
| `ShuffleSystem/` | `Docs/` | **"Maelstrom" is the player-facing display name of Tournament mode** (the docs folder keeps the legacy "Shuffle" name) — the `ArcadeGameTournament.asset` card carries `DisplayName = "Maelstrom"`. It is **not** a separate mode: code/data/enum stay **Tournament** (`GameModes.Tournament = 36`); the scene file was renamed to `Maelstrom.unity` in the v2 rework. `ARCHITECTURE.md` is a **pointer** to `TournamentSystem/ARCHITECTURE.md`; the former Shuffle-specific behavior deltas (randomized lineup, per-domain `{2,1,0}` scoring + crystal-wallet credit, race-to-6) are now **shipped**. |
| `ElementalAbilitySystem/` | `Docs/` | Vessel elemental-ability contract: `ARCHITECTURE.md` (4 abilities × 4 elements × 4 upgrades; §7 four-icon row + control hints), `FLEET_MAPS.md` (per-vessel map status + un-approved proposals), `AUDIT.md` (dated evidence, CONFIRMED/REPORTED labels), `BACKLOG.md` (sequenced plan). Per-ability deep docs live beside the code in `_Scripts/Controller/Vessel/R_VesselActions/*.md`. Work here routes through the `/vessel` skill. |
| `CameraMigrationReview.md` | `Docs/` | Camera system migration tracking |
| `BOOTSTRAP_AUDIT.md` | `_Scripts/System/Bootstrap/` | Bootstrap scene audit, execution order, DI registration |
| `HEXRACE.md` | `_Scripts/Controller/Arcade/` | HexRace game mode technical reference |
| `RAMPAGE.md` | `_Scripts/Controller/Arcade/` | Rampage game mode technical reference (multiplayer destruction race) |
| `CRYSTAL_CAPTURE.md` | `_Scripts/Controller/Arcade/` | Crystal Capture game mode technical reference |
| `JOUST.md` | `_Scripts/Controller/Arcade/` | Joust game mode technical reference |
| `ASTROLEAGUE.md` | `_Scripts/Controller/Arcade/` | Astro League game mode technical reference |
| `RIBCAGE.md` | `_Scripts/Controller/Arcade/` | Ribcage / "Peel the Cage" technical reference (Rhino-only cage-breaking race; the layered-orange intensity model, the open-weave generator, the shielded-mass targeting-grid rule, and the record of the removed fauna ladder) |
| `PRISM_PERFORMANCE_AUDIT.md` | `_Scripts/Game/Prisms/` | Prism system performance analysis (vestigial location) |
| `UNIT_TESTING_GUIDE.md` | `_Scripts/Tests/` | Unit testing guidelines and inventory |
| `BENCHMARK_TOOL.md` | `_Scripts/Utility/PerformanceBenchmark/` | Performance Benchmark tool guide (tabs, score/hints, sweep, Load Time Insights, customization) |
| `TOOLING.md` | `Docs/` | **The editor-tooling convention.** One menu root (`FrogletTools/`), one auto-discovering board (Froglet Master Tool), one shared palette, and — for any tool that WRITES assets — the ship contract: record what you wrote, draw `FrogletToolShipPanel` (Validate & Push / Retire Tool), because a tool's output is the deliverable and it lands in the working tree, not the branch. **Read before adding ANY `[MenuItem]`** — a tool outside `FrogletTools/` is flagged as non-conforming by the board itself. |
| `GAMECANVAS.md` | `Docs/` | GameCanvas as one source of truth: the two forked prefabs, the 1,734 identical-in-every-scene overrides that masked the prefab, the ~20 that are genuinely per-mode, the dangling cross-prefab refs, the code fixes that removed per-scene wiring, and the in-editor unification steps. **Read before touching any game-mode scene's canvas.** |
| `GIT_RULES.md` | Project root | Git commit conventions |
| `BOOTSTRAP_AUTH_FLOW.md` | `Docs/` | Bootstrap → Authentication → Menu_Main full flow: scene-by-scene diagrams, `ApplicationStateMachine`, auth SOAP data flow, key-file tables, auth patterns |
| `MULTIPLAYER_SPAWNING.md` | `Docs/` | Netcode component reference, player/vessel spawn chains (menu, game, party join, freestyle flight), Player NetworkVariables, player-count & AI-backfill pipeline, team balancing |
| `PARTY_SOCIAL.md` | `Docs/` | Party/invite lobby + friend system reference: services, SOAP types, facade API, presence, UI components, SO assets, patterns |
| `HEXRACE_SUMMARY.md` | `Docs/` | Condensed HexRace reference (canonical deep-dive: `_Scripts/Controller/Arcade/HEXRACE.md`) |
| `MENU_NAVIGATION.md` | `Docs/` | Menu_Main screen navigation: `ScreenSwitcher`, `IScreen`, screen inventory, reusable UI components |
| `LAVALAMP.md` | `Docs/` | Lava-lamp / menu-freestyle merge: Game UI hierarchy, HUD lifecycle, vessel selection, phased shape-drawing/scoring rollout |
| `ELEMENTAL_BARS.md` | `Docs/` | Elemental bars petal-flower HUD: level→colour math, `ElementalBarsConfigSO` single source of truth, per-vessel rollout |
| `ECOSYSTEM.md` + `ECOSYSTEM_MASTERPLAN.md` | `Docs/` | Ecosystem mechanics log + north-star roadmap (the LOCKED invariants above summarize these) |
| `NUCLEUSRUSH.md` | `_Scripts/Controller/Arcade/` | Brood Rush (nucleus-control fauna-wave race) technical reference |
| `README.md` | `Docs/` | Party / Presence / NetDiag docs index + shared conventions and locked designs |

## Architecture Patterns

Follow these established patterns. Do not introduce alternative architectures without discussion.

### ScriptableObject Config Separation

All tunable gameplay parameters live in ScriptableObjects, not in MonoBehaviours. MonoBehaviours reference SO configs at runtime. Example pattern:

- `SkimmerAlignPrismEffectSO` (config) → referenced by the vessel's prism controller system
- `VesselExplosionByCrystalEffectSO` (config) → defines explosion parameters for crystal impacts
- `CameraSettingsSO` (config) → per-vessel camera follow/zoom settings
- `BootstrapConfigSO` (config) → bootstrap scene flow settings (target framerate, splash duration, timeouts)
- Use `[CreateAssetMenu]` with organized menu paths: `ScriptableObjects/Impact Effects/[Category]/[Name]`

### SOAP — Scriptable Object Architecture Pattern (Primary Architecture)

This project uses the **SOAP asset** (Obvious.Soap v2.7.0, installed at `Assets/Plugins/Obvious/Soap/`) as the backbone for modular, event-driven, and data-container-based architecture. **Use SOAP whenever possible** for cross-system communication and shared state — do not introduce singletons, static events, or direct references between systems when a SOAP variable or event can do the job.

**Fail-loud policy**: Do not add if-null guards on `ScriptableEvent` serialized fields. Missing references should produce immediate, obvious errors rather than silent failures.

#### Core SOAP Primitives

- **`ScriptableVariable<T>`** — Persistent data containers that live as assets. Any system can read/write to them without knowing about other consumers. Use these for shared state (player health, score, vessel class, authentication data, etc.).
- **`ScriptableEvent<T>` / `ScriptableEventNoParam`** — Decoupled event channels. Raise events from any system; listeners subscribe via inspector-wired `EventListener` components or code. Use these for one-to-many notifications (game over, boost changed, crystal collected, etc.).
- **`EventListener<T>`** — MonoBehaviour that subscribes to a `ScriptableEvent` and exposes `UnityEvent` responses in the inspector. Preferred for UI and scene-bound reactions.

#### When to Use SOAP

| Scenario | SOAP Solution |
|---|---|
| Sharing state between unrelated systems | `ScriptableVariable<T>` asset |
| Broadcasting an event to multiple listeners | `ScriptableEvent<T>` asset |
| UI needs to react to gameplay changes | `EventListener<T>` on the UI GameObject |
| New system needs data from another system | Reference the existing `ScriptableVariable` — do not add a direct dependency |
| Request/response pattern between systems | `GenericEventChannelWithReturnSO<T, Y>` (custom extension at `Assets/_Scripts/ScriptableObjects/SOAP/ScriptableEventWithReturn/`) |

#### Creating New SOAP Types

Custom SOAP types live in `Assets/_Scripts/ScriptableObjects/SOAP/` organized by data type. When you need a new type:

1. Create a folder: `Assets/_Scripts/ScriptableObjects/SOAP/Scriptable[TypeName]/`
2. Create the variable class: `[TypeName]Variable : ScriptableVariable<[TypeName]>`
3. Create the event class: `ScriptableEvent[TypeName] : ScriptableEvent<[TypeName]>`
4. Create the listener class: `EventListener[TypeName] : EventListenerGeneric<[TypeName]>`
5. Use namespace `CosmicShore.ScriptableObjects` for all custom SOAP types

Existing custom SOAP types (16 subdirectories): `AbilityStats`, `ApplicationState` (`ApplicationStateData` + `ApplicationStateDataVariable` + `ScriptableEventApplicationState` — written by `ApplicationStateMachine`), `AuthenticationData` (+ `NetworkMonitorData`), `ClassType` (VesselClassType + VesselImpactor + debuff events), `CrystalStats`, `FriendData` (`FriendData` struct + `FriendPresenceActivity` `[DataContract]` + `ScriptableEventFriendData` + `ScriptableListFriendData` + `EventListenerFriendData` — relationship & presence data for UGS Friends integration, written by `FriendsServiceFacade`), `GameplaySFX` (gameplay sound effect category events for decoupled audio), `InputEvents`, `PartyData` (PartyInviteData, PartyPlayerData + list variant), `PipData`, `PrismStats`, `Quaternion`, `VesselHUDData`, `Transform`, and `ScriptableEventWithReturn` (generic return channel + `PrismEventChannelWithReturnSO`). Also contains `VesselPrefabContainer.cs` for vessel-class-to-prefab mapping.

#### SOAP Anti-Patterns

- **Do not** use singletons or static events for cross-system communication — use `ScriptableEvent` instead
- **Do not** add direct MonoBehaviour-to-MonoBehaviour references for data sharing — use `ScriptableVariable` instead
- **Do not** use `FindObjectOfType` or service locators to get shared data — wire a `ScriptableVariable` in the inspector
- **Do not** create C# events or `Action` delegates on MonoBehaviours for things that multiple unrelated systems need to observe — use `ScriptableEvent`
- **Do not** duplicate SOAP types — check `Assets/_Scripts/ScriptableObjects/SOAP/` for existing types before creating new ones
- **Do not** put gameplay logic inside ScriptableVariable/ScriptableEvent classes — they are data containers and channels, not controllers
- **Do not** add if-null guards on ScriptableEvent serialize fields — fail loud on missing references

### Threading & Main-Thread Affinity

See `Docs/THREADING.md` for the full reference. The short version:

UGS SDK (`Unity.Services.*`) and Netcode methods return `System.Threading.Tasks.Task` whose
continuations complete on the .NET ThreadPool. From the ThreadPool, any `UnityEngine.Object`
access throws `EnsureRunningOnMainThread`, and any `Obvious.Soap` `ScriptableEvent.Raise()` runs
its listeners **inline on the off-thread**, surfacing the same crash one level deeper.

**The contract:** every `await` of a UGS / Netcode `Task` uses `.AsMainThread()`:

```csharp
ActiveSession = await MultiplayerService.Instance.CreateSessionAsync(opts).AsMainThread();
```

`.AsMainThread()` (in `Assets/_Scripts/Utility/ClassExtensions/UniTaskExtensions.cs`) awaits
the original task and then awaits `MainThreadDispatcher.SwitchToMainThreadAsync()`, which
marshals onto Unity's captured `SynchronizationContext`. Four overloads cover
`Task`, `Task<T>`, `UniTask`, `UniTask<T>`.

**Why UniTask's own primitives don't work on this version (`com.cysharp.unitask@86b6e6a2e286`):**

UniTask 2.x intentionally bypasses `SynchronizationContext` and `ExecutionContext`:

> *"UniTask always works like `Task.ConfigureAwait(false)` and is not guaranteed that the thread
> before awaiting may match the thread after awaiting."* — UniTask docs.

Consequence:

- `UniTask.SwitchToMainThread()` — awaiter's `IsCompleted` reports `true` from ThreadPool →
  continuation runs **inline** on ThreadPool. Switch is a no-op. ([Cysharp/UniTask#319](https://github.com/Cysharp/UniTask/issues/319), [#151](https://github.com/Cysharp/UniTask/issues/151))
- `UniTask.Yield(PlayerLoopTiming.Update)` — yields, but the resumption is *not* guaranteed on
  main thread because UniTask's `ContinuationQueue` doesn't capture the SyncContext.
  ([Cysharp/UniTask#561](https://github.com/Cysharp/UniTask/discussions/561) — exact symptom we hit.)

Neither primitive is a reliable main-thread switch on this version. The `MainThreadDispatcher` +
`.AsMainThread()` boundary helper bypasses UniTask's bypass by using Unity's own
`SynchronizationContext`, which IS properly main-thread-bound.

**The canary** lives in `SceneTransitionManager.SetFadeImmediate`
(`Assets/_Scripts/System/Bootstrap/SceneTransitionManager.cs`). It reads
`MainThreadDispatcher.IsOnMainThread` and logs `Debug.LogError` with the call stack if a future
UGS call site forgets `.AsMainThread()`. Both the canary and the helper share one main-thread-ID
source — no risk of divergent capture sites.

**When to use which primitive:**

| Situation | Use |
|---|---|
| `await` a UGS / Netcode / cross-thread `Task` | `.AsMainThread()` |
| `await` a `UniTask` you wrote that internally awaits UGS with `.AsMainThread()` | nothing extra at the caller |
| Need main thread without a Task to attach to (e.g., top of a `catch` block) | `await MainThreadDispatcher.SwitchToMainThreadAsync()` |
| Yield one frame for PlayerLoop processing (NOT thread marshaling) | `await UniTask.Yield(PlayerLoopTiming.Update)` — fine for sequencing, not for affinity |
| Assert main thread (debug) | `MainThreadDispatcher.IsOnMainThread` |

The three remaining `Yield(PlayerLoopTiming.Update)` calls in
`Controller/Party/PartyInviteController.cs` (in catch / recovery blocks) are intentional —
they are "wait for the next PlayerLoop tick before handling this exception" semantics, not
threading.

**Anti-patterns to avoid:**

- **Do not** add `await UniTask.SwitchToMainThread()` or `await UniTask.Yield(PlayerLoopTiming.Update)` as a thread-marshaling fix — neither works on this UniTask version. Use `.AsMainThread()`.
- **Do not** raise a SOAP `ScriptableEvent` from a UGS / Netcode callback continuation without ensuring the continuation has resumed on the main thread first — SOAP raises invoke listeners inline.
- **Do not** touch a `UnityEngine.Object` (incl. `== null` checks) in a `Task` continuation without `.AsMainThread()` upstream.
- **Do not** capture `Thread.CurrentThread.ManagedThreadId` in random places to make per-class main-thread checks — read `MainThreadDispatcher.IsOnMainThread` instead, single source of truth.

### Bootstrap, Authentication & App State

Full flow (scene-by-scene execution diagrams, `ApplicationStateMachine` graph, SOAP data
flow, key-file tables): `Docs/BOOTSTRAP_AUTH_FLOW.md`. The rules that must hold:

- `AppManager` is the Reflex DI root and bootstrap orchestrator (`[DefaultExecutionOrder(-100)]`,
  `IInstaller`); all persistent services/SO assets register in `InstallBindings()`.
- **Single writer**: only `AuthenticationServiceFacade` writes `AuthenticationData`; only
  `ApplicationStateMachine` writes `ApplicationStateDataVariable`; scene controllers and
  UI read state and subscribe to SOAP events - they never mutate directly.
- All auth async uses UniTask + `CancellationToken` with linked-CTS timeouts (no polling
  loops, no raw `Task.Delay`); disable buttons during async ops instead of boolean
  guards; get facades via `[Inject]`, never by creating controller GameObjects.
- `SceneLoader` (DontDestroyOnLoad, Bootstrap) owns game launch / restart /
  return-to-menu via code-subscribed SOAP events; scene names come from `SceneNameListSO`.

### Dependency Injection (Reflex)

The project uses Reflex DI with `AppManager` as the root `IInstaller`. All persistent services and shared assets are registered in `AppManager.InstallBindings()`:

**SO asset registration** (`RegisterValue`): `SceneNameListSO`, `GameDataSO`, `AuthenticationDataVariable`, `NetworkMonitorDataVariable`, `FriendsDataSO`, `HostConnectionDataSO`, `ApplicationLifecycleEventsContainerSO`, `ApplicationStateDataVariable`. These are project-level assets wired via inspector on AppManager.

**MonoBehaviour singleton registration** (`RegisterFactory`, Lazy): `GameSetting`, `AudioSystem`, `PlayerDataService`, `UGSStatsManager`, `CaptainManager`, `IAPManager`, `SceneLoader`, `ThemeManager`, `CameraManager`, `PostProcessingManager`, `StatsManager`, `SceneTransitionManager`. These use a lazy factory that prefers the serialized reference and falls back to a scene search at first injection time.

**Pure C# singleton registration** (`RegisterFactory`, Lazy): `AuthenticationServiceFacade`, `NetworkMonitor`, `FriendsServiceFacade`, `ApplicationStateMachine`.

#### DI Patterns to Follow

- **Use `[Inject]` for shared assets**: `GameDataSO`, `SceneNameListSO`, and other DI-registered assets should be accessed via `[Inject]`, not `[SerializeField]`. This eliminates manual inspector wiring and serialization drift.
- **Injection timing**: `[Inject]` fields are populated after `Awake()` but before `Start()`. Access injected fields in `Start()` or later — never in `Awake()`. If you need to subscribe to events in `OnEnable()`, use a deferred pattern: attempt in `OnEnable()`, retry with duplicate guards in `Start()`. The same hazard applies to runtime creation: `AddComponent<T>()` runs the new component's `Awake`/`OnEnable` INLINE, before the caller's next line can assign any field — so a factory that assigns dependencies after `AddComponent` must also explicitly complete the deferred subscription (reference: `ElementalComebackSystem.EnsureExists` + `TrySubscribeToGameEvents`).
- **ContainerScope per scene**: Each scene that uses `[Inject]` must have a Reflex `ContainerScope` component (via the `ContainerScope.prefab` in `_Prefabs/CORE/`). The Bootstrap scene's scope is the root; other scenes get child scopes.

### Input Strategy Pattern

Platform-agnostic input via `Assets/_Scripts/Controller/IO/`:

- `IInputStrategy` — interface for all input handlers
- `BaseInputStrategy` — shared logic
- `KeyboardMouseInputStrategy`, `GamepadInputStrategy`, `TouchInputStrategy` — platform-specific implementations
- `InputController` — manages active strategy and input state
- `IInputStatus` / `InputStatus` — input state container
- Input strategies are swappable per platform/context at runtime
- **Only the local human's `InputController` runs.** `Player.InitializeForMultiplayerMode`
  sets `InputController.enabled = IsLocalUser` (the earliest point AI-ness is reliable —
  `NetIsAI` is written by the AI spawner *after* `Spawn()`, so `OnNetworkSpawn` cannot gate
  this). AI pilots write `InputStatus` directly; remote vessels replicate theirs.
- **Gamepad triggers are rest-calibrated.** Triggers do not universally rest at zero (worn
  springs drift upward; some DirectInput-style pads rest mid-range by design), and a rest
  value above the deadzone reads as "permanently held" — the press edge never fires and
  trigger-bound actions (e.g. the Squirrel's drift) go dead. `GamepadInputStrategy`
  min-latches each trigger's observed resting baseline (re-latched on `Gamepad.current`
  device change) and remaps `[rest..1] → [0..1]` before edge detection and the
  `InputStatus` analog writes. Do not compare raw trigger reads against an absolute
  threshold anywhere else.

### Impact Effects Architecture

The collision/impact system (`Assets/_Scripts/Controller/ImpactEffects/`) uses a matrix of impactors and effect SOs:

**Impactor types** (all extend `ImpactorBase`): `VesselImpactor`, `NetworkVesselImpactor`, `PrismImpactor`, `ProjectileImpactor`, `SkimmerImpactor`, `MineImpactor`, `ExplosionImpactor`, `CrystalImpactor`, `ElementalCrystalImpactor`, `OmniCrystalImpactor`, `TeamCrystalImpactor`

**Effect SO pattern**: `[Impactor][Target]EffectSO` — e.g., `VesselExplosionByCrystalEffectSO`, `SkimmerAlignPrismEffectSO`, `SparrowDebuffByRhinoDangerPrismEffectSO`. Per-vessel effect asset instances exist for each vessel class. Organized into subdirectories: `Vessel Crystal Effects/`, `Vessel Prism Effects/`, `Vessel Explosion Effects/`, `Vessel Projectile Effects/`, `Vessel Skimmer Effects/`, `Skimmer Prism Effects/`, `Projectile Crystal Effects/`, `Projectile Prism Effects/`, `Projectile Mine Effects/`, `Projectile End Effects/`.

Key interfaces: `IImpactor` / `IImpactCollider`

**A vessel and its own skimmer never impact each other.** `SkimmerImpactor` and `VesselImpactor` carry mirrored self-guards on their vessel<->skimmer dispatch — required because the Rhino's sword capsule permanently overlaps its own hull, which otherwise ran the full victim-effect chain against the pilot (muting their own `RightStickAction` via `VesselDamageBySkimmerEffect`, impact-SFX spam). Skimmer-vs-own-PRISM handling is separate and stays flag-controlled (`Skimmer.AffectSelf`). See `_Scripts/Controller/Vessel/R_VesselActions/RHINO_SHIELD_SWIPE.md`.

**A skimmer only skims if `VesselStatus` points AT it — and the failure is silent.**
`VesselController.Initialize` initializes **only** `VesselStatus.NearFieldSkimmer` /
`FarFieldSkimmer`, and `SkimmerImpactor` drops every contact while `skimmer.IsInitialized` is
false. So a vessel can carry a perfectly wired skimmer — trigger sphere, kinematic rigidbody,
`ImpactCollider`, effect container, layer 7 — and skim **nothing at all**, with no error
anywhere, because the reference points at a different (or disabled) skimmer object. The Dolphin
shipped that way for its whole life: an active `EnergySkimmer` doing the physics and a disabled
legacy nested `Skimmer.prefab` holding the reference. **Audit it, don't infer it from feel:**
`FrogletTools > Vessels > Audit Vessel Skimmers` checks assignment, active state up the whole
ancestor chain, the components the trigger path needs, and whether the container holds any
prism effects — asset-only, no play mode. *(Serpent currently fails it.)* Note that a skim's
three feedback signals are each individually invisible — the haptic is a **no-op on desktop**,
the beam VFX only draws when the skimmed prism authors a `ParticleEffect`, and a gauge that
moves a tenth of its range per skim reads as nothing — so "I feel no skimming" is not evidence
about the wiring in either direction. The forcefield crackle needs **three** pieces to be
present or `SkimmerForcefieldCracklePrismEffectSO.Execute` returns silently: the effect in the
container, a `ForcefieldCrackleController` on the impactor's own GameObject, and an overlay
`MeshRenderer` assigned to it (vessels whose skimmer IS `Skimmer.prefab` get the last two free;
standalone skimmer objects do not). Detail:
`_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md` §5.

**Danger prisms are not safe to their own domain (locked design).** `IsDangerous` effects apply to every vessel that touches the prism, regardless of domain — friendly fire included (the fire-trail action literally sets `IsDangerous` from a `FriendlyFire` flag). Danger-prism effect SOs must not gate on domain. **Danger is mutually exclusive with BOTH shield tiers**: `PrismStateManager.MakeDangerous` clears `IsShielded` AND `IsSuperShielded` (and disengages the shield visuals), just as `ActivateSuperShield` clears `IsDangerous` — a danger prism carrying a stale super-shield flag is invulnerable and kills any AOE explosion that touches it. `Prism.ResetState` also clears `IsSuperShielded` on pool reuse (no spawner requests super-shield pre-`Initialize`; it is always engaged post-spawn). This is what makes danger trails a risk/reward surface: the Squirrel's own overheat trail grants 10x skim energy (`SkimmerBoostPrismEffect.dangerEnergyMultiplier`, gated behind the skimming vessel's Charge level-5 "Live Wire" upgrade — below it danger skims pay base energy) but slams its owner on contact — volume-independent full-stop slow at the danger max (`VesselChangeSpeedByPrismEffectSO`: `maxSlowStrength * dangerSlowMultiplier`), all-element decaying debuff for 4s (`VesselElementalDebuffByDangerPrismEffectSO`), and boost reset.

**AOE blast impulse — `Inertia` only reaches the screen with a ceiling of its own.** Every
explosion entry point (`ExplosionImpactor.ProcessBatchFrame` / `ProcessBatchConeFrame` /
`DrainPendingBatchFrame` → `PrismSpatialIndex.ProcessExplosionFrame` / `ProcessExplosionConeFrame`
/ `DrainPendingExplosionDamage`) takes ONE `ExplosionImpulse`
(`_Scripts/Controller/Projectiles/ExplosionImpulse.cs`) instead of a loose `(speed, inertia)` pair,
because debris speed is `min(Speed * Inertia, ceiling)` and the ceiling is the third number that
cannot travel separately. With no ceiling of its own a blast falls back to
`PrismExplosion.prefab`'s authored `maxSpeed` (**33.33 u/s**) — a guard sized for the legacy
`impactVector / volume` gain, not a physical bound — and **every** AOE magnitude sits far above it
(the Dolphin cone's wavefront is `height / (duration * 4)` ≈ 222 u/s, 6.7x over), so every blast
saturates to the same speed and `Inertia` is dead tuning. `AOEExplosion.proportionalDebris` opts a
blast onto the true-velocity contract `PrismEffectHelper.DamageProportional` already defines: the
vector IS the debris velocity (`speed * debrisRestitution * Inertia`) and the blast passes a matching
ceiling. Off by default; **on** for `AOEConicExplosion.prefab` (the Dolphin crystal blast) at
`debrisRestitution 1/3 x Inertia 1.8 = 0.6`. Debris speed and **shatter rate are one number** on this
contract (`PrismExplosion.TriggerExplosion` re-reads `Speed` off the clamped velocity when an
override is supplied — otherwise raising the ceiling finishes the shatter in a frame while the debris
crawls), so `Inertia` scales both together; do not split them. Both prism paths carry the ceiling —
the Burst resolve and the Physics-trigger fallback (`ExecuteCommonPrismCommands`) — so a blast throws
mass at the same speed with or without the spatial index. Detail: `Docs/SPATIAL_INDEX.md` § "Impulse".

**Forcefield Crackle (Skimmer)**: `SkimmerForcefieldCracklePrismEffectSO` (at `_Scripts/Controller/ImpactEffects/EffectsSO/Skimmer Prism Effects/`) is a shader-driven alternative to `SkimmerFXPrismEffectSO` that visualizes the Skimmer's invisible sphere collider on prism impacts. It computes the impact point via `Collider.ClosestPoint` between the prism box and skimmer sphere, projects it onto the sphere surface, and forwards the event (position + duration + intensity + radius) to a `ForcefieldCrackleController` MonoBehaviour on the vessel (`_Scripts/Controller/Vessel/ForcefieldCrackleController.cs`). The controller owns all visual parameters (colors, arc density/sharpness, ring thickness, ripple speed, fresnel) as serialized fields and feeds a ring buffer of up to 16 simultaneous impacts to the shader via MaterialPropertyBlock arrays each frame. `[ExecuteAlways]` allows edit-mode preview via `ForcefieldCrackleControllerEditor` (at `_Scripts/Editor/`). The shader's custom-function HLSL file `ForcefieldCrackle.hlsl` (at `Assets/Materials/Graphs/`) uses FBM-based electrical arcs with expanding wavefronts on a geodesic distance metric so arcs follow the sphere's curvature. All three code files use the `CosmicShore.Gameplay` namespace.

### Multiplayer / Netcode & Player Spawning

Component reference, full spawn chains (menu, game, party join, freestyle flight),
`Player` NetworkVariable tables, and the player-count/AI-backfill pipeline:
`Docs/MULTIPLAYER_SPAWNING.md`. Load-bearing rules:

- Vessel spawning is ONE unified Netcode+SOAP pipeline for menu and game
  (`ServerPlayerVesselInitializer` → `ClientPlayerVesselInitializer`; menu adds autopilot
  via `MenuServerPlayerVesselInitializer`, game scenes pre-spawn AI via
  `ServerPlayerVesselInitializerWithAI`). Never add a parallel spawn path.
- **AI players/vessels spawn server-owned with `destroyWithScene: false`** (same-tick
  scene-load batching would destroy them on clients as they spawn) - so every cleanup
  path must explicitly despawn AI (`ExecuteSceneReloadReplay`,
  `SceneLoader.ClearPlayerVesselReferences`, disconnect shutdown).
- Track processed players by `NetworkObjectId`, never `OwnerClientId` - AI shares the
  host's OwnerClientId. Locality (`Player.IsLocalUser`) is reliable only after pair-init
  sets `IsInitializedAsAI` (the AI spawner writes `NetIsAI` AFTER `Spawn()`).
- The spawner never shuts down the NetworkManager - the eager Relay session persists
  across all scene transitions; teardown is explicit party-leave
  (`PartyInviteController`) or transport failure (`MultiplayerSetup.OnTransportFailure`).
- `SceneLoader` guards `if (nm.IsListening && !nm.IsServer) return` before any local
  scene load (MPPM shared-SOAP double-load protection).
- AI team assignment is deterministic (`GetBalancedDomain`: lowest total → fewest humans
  → enum order Jade→Ruby→Gold) - identical results on every machine, no shared seed.
- Menu domain reset (Jade) happens ONLY server-side in
  `MenuServerPlayerVesselInitializer.OnPlayerReadyToSpawnAsync`; a runtime vessel swap
  keeps the player's current `NetDomain` and inherits pose + speed.
- **`IsLocalUser` vs `IsLocalPilot`.** `IsLocalUser` (= `IsMultiplayerOwner`) is the
  networked path's "locally-owned, non-AI player" and requires `IsSpawned`. `IsLocalPilot`
  is broader by exactly one case - a non-AI Player whose NetworkObject is not spawned.
  **Anything that must hold in EVERY game mode binds on `IsLocalPilot`**, so no spawn path
  can slip a human past a platform system; the prism occlusion corridor is the reference
  case (`Docs/PRISM_ANIMATION.md` §4.7).

### Party / Invite / Friends (social layer)

Two UGS sessions layer here: a **presence lobby** (lobby-only, no Relay, ≤100 players -
discovery + invite property exchange) and a **party session** (Relay-backed, ≤4 - actual
gameplay networking); both coexist with the active NetworkManager.

**Local authoritative state is never answered from a remote-published mirror (LOCKED).**
"How big is MY party / is X in it?" is answered by `IPartyRoster` (implemented by
`HostConnectionDataSO`, fed by the party **session**) - local, authoritative, zero latency.
"How big is THEIR party?" is answered by `PartyPlayerData.AdvertisedPartyMemberCount` /
`AdvertisedPartyMaxSlots` - presence-**lobby** properties, a hint, poll latency. Answering
the first with the second's data is what let three players in ONE party render 2/4, 1/4 and
3/4 simultaneously: with N members that is N independently-published scalars and nothing
that reconciles them, so **no polling cadence can ever fix it** (which is why several
cadence/push/jitter passes did not). The `Advertised` prefix and the `IPartyRoster` surface
exist to make the mistake unspellable - do not remove either as redundant. Full analysis:
`Docs/PresenceSystem/BUGS.md` B15. Roster changes broadcast on the coalesced
`HostConnectionDataSO.OnPartyRosterChanged` (one raise per settled mutation) - listen to
that for repaints, not to the per-member joined/left events.

Single writers:
`HostConnectionService` → `HostConnectionDataSO`; `FriendsServiceFacade` → `FriendsDataSO`
(UI reads SOAP lists/events, never calls UGS directly; presence updates go through
`FriendsInitializer` only; invites are per-player lobby properties so no host privilege is
needed). Service/UI/SO-asset inventories + facade API: `Docs/PARTY_SOCIAL.md`; spawn
chains for party join + menu freestyle flight: `Docs/MULTIPLAYER_SPAWNING.md`.

#### Party / Presence / NetDiag docs — start at `Docs/README.md`

Full engineering docs for these subsystems live under `Docs/` (the index +
shared conventions are in `Docs/README.md`). Route by task:

| If your task… | Read first |
|---|---|
| Touches `HostConnectionService` / `PartySessionService` / `NetworkTransitionService` / `PartyInviteController` | `Docs/PartySystem/ARCHITECTURE.md` (+ `BUGS.md`) |
| Touches `PresenceLobbyService` / `LobbyPropertyWriter` / `LobbyRefreshScheduler` / `InviteService` / `AcceptanceSignalService` | `Docs/PresenceSystem/ARCHITECTURE.md` |
| Classifies / logs a party·lobby·session·transition catch failure | `Docs/NetworkDiagnostics/ARCHITECTURE.md` |
| Run the MPPM regression before a commit | `Docs/PartySystem/TESTS.md` (S-series) + `Docs/PresenceSystem/TESTS.md` (P-series) |
| Validate the NetDiag overlay itself | `Docs/NetworkDiagnostics/TESTS.md` (Tests A–E) |
| Log / triage a bug | `Docs/PartySystem/BUGS.md` (B2/B3/B5/B7) · `Docs/PresenceSystem/BUGS.md` (B1/B4/B6) |
| Pick up refactor work | `Docs/PartySystem/REFACTOR.md` · `Docs/PresenceSystem/REFACTOR.md` |
| Read what was already tried (session history) | `Docs/PartySystem/MPPM_SESSION_LOG.md` |

**Locked design (do not relitigate):** EAGER per-user Relay — every player
hosts their own Relay-backed party session on entering `Menu_Main`. **Do not
reintroduce LAZY / on-first-invite creation** (the shutdown-and-recreate
cascade it caused is the root of every recurring party-invite bug). Full rule
+ rationale: `Docs/README.md` § "Locked design" and
`Docs/PartySystem/ARCHITECTURE.md` § "Locked design" / "Unbreakable exit
criteria".

**Threading prerequisite (shipped):** the UGS / Netcode `Task` continuation → SOAP
off-thread → `EnsureRunningOnMainThread` cascade is resolved by `MainThreadDispatcher`
+ `.AsMainThread()` at every UGS / Netcode `await`. See `Docs/THREADING.md`.
**Do not** introduce `UniTask.SwitchToMainThread()` or
`UniTask.Yield(PlayerLoopTiming.Update)` as a thread-marshaling fix — both have
been tried and proven unreliable on this UniTask version.

### Domain Game Modes (HexRace / Joust / Crystal Capture / Astro League / Brood Rush)

Per-mode technical references live next to the controllers
(`_Scripts/Controller/Arcade/HEXRACE.md`, `JOUST.md`, `CRYSTAL_CAPTURE.md`, `RAMPAGE.md`,
`ASTROLEAGUE.md`, `NUCLEUSRUSH.md`; condensed HexRace notes in
`Docs/HEXRACE_SUMMARY.md`). Cross-mode rules:

- **Domain-aggregated scoring**: modes end on a per-domain sum via the mode's
  `ScoringRuleSO.IsObjectiveReached` (`ScoringMetrics.SumByDomain`) - at most three
  scores ever exist (Jade/Ruby/Gold); teammates contribute to one total. The comeback
  system (`ElementalComebackSystem`, REQUIRED in every party game, auto-created by
  `MultiplayerMiniGameControllerBase.EnsureExists`) keys off domain aggregates too.
- **Replay is a full network scene reload** (`UseSceneReloadForReplay = true`) for all
  shipped modes - flora/fauna/environment don't reset in place.
- **Server-authoritative winners**: detection runs in `OnTurnEndedCustom()` on the
  server; results broadcast via the shared `SyncFinalResults` template.
- **End-game/win-condition COUNTS** are authored ONLY through FrogletTools > Game Modes >
  End Game Conditions (`Resources/EndConditionOverrides.asset`) - never per-scene
  inspector fields.

### FTUE (First-Time User Experience)

Tutorial system at `Assets/FTUE/` (25 C# files) using adapter pattern with clean interface separation:

- **Interfaces**: `IFlowController`, `ITutorialExecutor`, `ITutorialStepHandler`, `ITutorialUIView`, `IAnimator`, `IOutroHandler`, `ITutorialStepExecutor`
- **Adapters**: `TutorialExecutorAdapter`, `FTUEIntroAnimatorAdapter`, `TutorialUIViewAdapter`
- **Data models**: `TutorialStep`, `TutorialPhase`, `TutorialSection`, `TutorialSequenceSet`, `TutorialStepPayload`, `TutorialStepType`, `FTUEProgress`
- **Drivers**: `FTUEIntroAnimator`, `TutorialFlowController`
- **Step handlers**: `FreestylePromptHandler`, `IntroWelcomeHandler`, `LockModesExceptFreestyleHandler`, `OpenArcadeMenuHandler`
- **UI**: `TutorialUIView`, `InGameTutorialFlowView`
- **Events**: `FTUEEventManager` (SOAP-based event broadcasting)

### Dialogue System

Custom dialogue system spanning two locations:

- **Editor & assets**: `Assets/_Scripts/DialogueSystem/` — animation controllers, shader graphs (SpriteAnimation, UI_NoiseDissolve), SO dialogue data assets, prefab
- **Runtime code**: `Assets/_Scripts/System/Runtime/` — `DialogueManager`, `DialogueEventChannel`, `DialogueUIAnimator`, `DialogueViewResolver`, `DialogueAudioBatchLinker`
- **Models**: `Assets/_Scripts/System/Runtime/Models/` — `DialogueLine`, `DialogueSet`, `DialogueSetLibrary`, `DialogueSpeaker`, `DialogueVisuals`, `DialogueModeType`, `IDialogueService`, `IDialogueView`, `IDialogueViewResolver`
- **Views**: `InGameRadioDialogueView`, `MainMenuDialogueView`, `RewardDialogueView`
- **Editor tools**: `DialogueEditorWindow`, `DialogueLineDrawer` (in `_Scripts/Editor/`)

### AI Opponent System

Runtime-configurable AI opponents at `Assets/_Scripts/Controller/AI/`:
- `AIPilot` controls AI vessel behavior
- `AIGunner` controls AI targeting/shooting
- AI profiles configured via `SO_AIProfileList` (`MainAIProfileList.asset`)
- AI profiles used for score cards and multiplayer backfill
- Configurable AI ship selection and behavior at runtime

**AI pilot lifecycle (do not regress):**

- `VesselController.ToggleAIPilot(bool)` is the single choke point for AI control of a
  vessel — it also stops any `AICinematicBehavior` flourish (the `EndGameSequencer` starts
  its behavior *after* enabling the pilot, so the end-game flourish still works). Never
  call `AIPilot.StartAIPilot`/`StopAIPilot` or start a cinematic around this seam.
- **A human-controlled turn never starts with autopilot on**: `Player.StartPlayer`'s human
  branch calls `ToggleAIPilot(false)` (symmetric with the AI branch). A leaked `AutoPilotEnabled`
  blocks every button action in `R_VesselActionHandler` while `AIPilot.Update` fights the
  player's input — the root of the "AI drifting conflicts with my drifting" class of bug.
- `AIPilot` and `AICinematicBehavior` keep their `enabled` flag mirrored to their active
  state: no `Update()` dispatch on vessels they aren't driving. `StartAIPilot` is
  idempotent; `StopAIPilot` uses `StopAllCoroutines` (a `StopCoroutine(new enumerator)`
  never stops the running coroutine).

### Menu Screen Navigation (Menu_Main)

`ScreenSwitcher` slides side-by-side panels and discovers `IScreen` implementors
automatically (`OnScreenEnter`/`OnScreenExit`) - never hard-wire screen references.
Reuse `ProfileDisplayWidget`, `NavLink`/`NavGroup`, `ModalWindowManager`; cache component
lookups; pair every subscribe with an unsubscribe; prefer `[Inject] AudioSystem` for new
code. Screen inventory + component reference: `Docs/MENU_NAVIGATION.md`.

### Lava-Lamp Mode (Menu Freestyle)

**"Lava lamp" and "freestyle" are the same thing** - one system, two names (viewed from
the menu vs player-controlled). BOTH standalone freestyle games are retired and must not
be reintroduced; party members fly the lava lamp together in Menu_Main. **Mass is
conserved in the menu too**: the lava-lamp vessel IS the freestyle gameplay vessel - no
trail caps, prism TTLs, or idle cullers (a menu ring-buffer cap shipped once and was
reverted; see Design Philosophy). Manage menu-idle prism growth with fauna cleanup or by
pausing the spawner. Freestyle input ownership (gamepad vs UI `sendNavigationEvents`),
HUD-after-swap, Game UI hierarchy, and the phased HUD/shape/scoring rollout:
`Docs/LAVALAMP.md` + `Docs/ToySystem/ARCHITECTURE.md`.

### Elemental Bars (per-vessel buff/debuff display)

Every vessel conveys elemental buffs/debuffs through `ElementalBarsView` - a 5-petal
"flower" per element driven by `ElementalBarsController` (null-safe, opt-in rollout per
vessel; named `SilhouetteController` until the vessel silhouette / trail-display HUD
element it also drove was removed). **All shared look/feel lives in `ElementalBarsConfigSO`**
(`Resources/ElementalBarsConfig.asset`) - never per-vessel SerializeFields. Petals are
pure-white silhouettes multiply-tinted at runtime - **never hue-shift**. Petal math,
level→colour table, juice, wiring tool, and perf notes: `Docs/ELEMENTAL_BARS.md`.

**The maintained-mechanism law (LOCKED).** No sustained/held mechanism may HOLD an element
above integer level **10** — the 10..15 overcharge band belongs to **transients only**, and
everything in it drains back to (at most) 10: temporary effects decay to zero, crystal-earned
base overcharge bleeds down (`RecoverBaseLevels`), the domain fauna buff's held layer fills
only to 10 with over-ceiling increases converted to draining spikes, and the comeback bonus
fills toward 10 and never past it. The player always gets to *feel* a reward above 10, and the
drain always restores the headroom to feel the next one. Enforced in `ResourceSystem`
(`SustainedCeiling`, `CompositeEffectiveLevel`); mechanics log: `Docs/ECOSYSTEM.md §15`.

### Elemental Hull Morphs (the vessel model is an element display)

The vessel's own hull conveys its element levels: vessel models carry **blend shapes on their
skinned meshes labeled by element name** (`charge` / `mass` / `space` / `time`, case-insensitive —
authored into the FBX), and `VesselAnimation` (base class, runs on every vessel) discovers them **by
name** at `Initialize` and glides each between its extremes as the effective element level moves
through the **[0, 10] progression band** — the deficit band [-5, 0) holds the level-0 silhouette,
the overcharge band (10, 15] holds the level-10 authored extreme (the same effective level the HUD
flowers read, so hull and flowers always agree). Transitions are DOTween glides, never snaps —
continuity of existence applies to the vessel's own body.

- **Single source of feel — `VesselElementalMorphConfigSO`** (`_Scripts/ScriptableObjects/`, asset
  at `Resources/VesselElementalMorphConfig.asset`): morph duration + ease, plus the pure helpers
  (`NormalizedMorphWeight`, `TryResolveElement` — both edit-mode tested in
  `VesselElementalMorphTests`). Spec changes go in the asset, never per-vessel fields.
- **Opt-in by art, zero wiring.** A vessel morphs the moment its model ships element-labeled shape
  keys — no per-prefab flags (the old `UseShapeKeys` bool + hardcoded shape indices are retired).
  Non-element art shapes (jaws, tendrils) are untouched; a name mentioning two elements is ambiguous
  and ignored. The shape's authored extreme is read from its last frame weight, so 0-100 and custom
  frame weights both work.
- **Fleet status**: audit with **FrogletTools > Vessels > Audit Vessel Elemental Morphs** (asset-only,
  no play mode, uses the exact runtime discovery). Manta/Termite/Falcon/Shrike (Manta meshes),
  Sparrow, Serpent, and Squirrel ship labeled shapes; Dolphin/Urchin/Rhino prefabs still wire
  shape-less test/placeholder meshes and need the rig swap below; Grizzly has no labeled shapes yet.
- **The Squirrel's FBX is a spliced hybrid of two historical exports — do not re-export over it
  blindly.** The 2024-10-29 export (`aa5046d41`, "add squirrel with shapekeys") carried
  `Time/Mass/Space/Charge` but its takes were broken; the 2024-11-15 re-export (`dc2c8ea54`,
  "fixed squirrel animations") repaired 2,622 of 3,483 bone curves across all 9 takes **and
  silently dropped all four shape keys** — which also silently killed the elemental morph surface.
  The shipped file is the fixed export with the four shape-key subtrees grafted back at the FBX
  binary level (valid because both exports share byte-identical topology and vertex drift ≤2e-6;
  verified by byte-level structural diff: base objects untouched, takes byte-identical to the fixed
  export, shapes byte-identical to the shape-key export, and **zero blend-shape animation curves**
  — the donor's constant-zero residue curves were deliberately left out). Same path + GUID; the
  mesh fileID is a name-hash shared by both exports, and the `.meta` pins each clip's take name to
  an explicit internalID matching `SquirrelAnimatorController 1`'s motion references — so the
  nested prefab instance, the animator clips, and the blend-space puppetry
  (`MantaAnimationContoller` → Animator floats `Pitch/Yaw/Roll/Throttle`) all keep binding. Any
  future Squirrel re-export must carry BOTH the fixed takes and the four element shape keys.
- **Morph weights are written in `LateUpdate`, which is load-bearing.** Unity's Animator writes
  bound curves every frame during the animation update — after `Update`, where tweens run — so an
  export that carries even constant-zero blend-shape curves would stomp script-set weights every
  frame. Tweens therefore drive a cached weight and `VesselAnimation.LateUpdate` is the single
  writer to the renderers, making the element level authoritative over any stray animation curve on
  any vessel (the current Squirrel takes are clean, but the defense is deliberate). Do not
  "simplify" the tween to write the renderer directly.
- **Animated parts resolve BY NAME too** (`VesselAnimation.ResolvePart`, `ResolveParts()` hook):
  an authored inspector reference always wins, and an empty one is looked up among the model's
  descendants by candidate name — current rig bone first, legacy part name as fallback. This is
  what makes an art swap cheap: the stale references come back null and the rig's bones bind
  themselves. Unbound parts are reported (`ReportUnresolvedParts`) and degrade to "that limb
  doesn't move", never a per-frame `NullReferenceException`.

#### The rigged-model swap (Dolphin / Urchin / Rhino)

These three are the fleet's only vessels whose art cannot morph, and it is **not** a wiring
oversight — their prefabs wire fundamentally different models. `Dolphin_Test.fbx` is 17 separate
static part meshes, `Urchan_Test.fbx` 14, and Rhino wires `Vessel_Placeholder_1.fbx` (a literal
placeholder); none carries a single blend shape. Their `*_shapekey_with_animations.fbx` rigs are
one skinned mesh on an armature **plus** the four element shapes — and each rig was authored FOR
that vessel's script: the dolphin rig's `jetT/jetm/jetB × .l/.r` + `jaw.u`/`jaw.b` are exactly
`RiptideAnimation`'s six thrusters and two jaws; the rhino rig's `wing1.*`/`jet.*` are
`RhinoAnimation`'s wings and engines (its `wing2.*` back wings host colliders, nothing drives them);
the urchin rig's `gunM.*`/`jetT.*`/`jetB.*` are `UrchinAnimation`'s guns and jets. The three scripts
name those bones as their primary resolution candidates, so the **code half of the port is done**.

**Rest poses are the reason a rig needs more than a name swap.** Puppetry drives a part *toward* an
absolute local rotation, which silently assumes it rests at identity — true of part-per-mesh art
placed by translation alone, false of a rig, where the bone's rest angle is what fans the engines
out (`wing1.l` rests at ~42°, `jet.l` at ~115°, `gunM.l` at ~90°). So `VesselAnimation` gained
`CaptureRestRotations` / `RotatePartFromRest` / rest-aware `ResetAnimation`: parts are driven
**relative to the pose they were authored in**. Identity-rest art is unaffected; rigged art holds
its shape. Two Dolphin bugs surfaced from the same root and are fixed: `RiptideAnimation` re-homed
its drift parts onto `Chassis` every non-drifting frame (a no-op on the old art, where they were
already its children — on the rig it would have permanently flattened the armature onto `fuse` and
collapsed the six jets onto one point; it now restores each part's **own** captured parent), and its
`InitialRotations` list was indexed two slots out of step with `animationTransforms`, so each engine
animated around a neighbour's rest pose. **That second fix changes the Dolphin's current look** — its
six engine cases rest at 26–169° and were being dragged toward identity.

The prefab half is a **hands-on editor pass**, not an automated one: a `SkinnedMeshRenderer`'s bone
list, bindposes, bounds and imported mesh IDs are owned by Unity's FBX importer, collider volumes
were authored against the old silhouettes and must be re-fitted by eye, and every legacy part
carries its `MeshRenderer` alongside its collider — so moving one onto a bone without retiring its
renderer welds the placeholder ship to the new skeleton. Run **FrogletTools > Vessels > Plan Vessel
Rig Swap** (report only, never writes): it prints, per vessel, which gameplay object belongs on
which bone, which objects have **no mapped bone** and would go dark when the old model is disabled
(Rhino's `ForceFieldSkimmer` parents to the legacy root), the rig's element shapes, and the ship-
geometry re-point. The printed procedure ends by clearing the animation's part fields — leave them
**empty** so they resolve to bones — and re-running the morph audit.
- **Seeding**: `VesselAnimation` snaps to live levels at `Initialize` (the live initial emit is
  `ResourceSystem.Start`), and `ResourceSystem.InitializeElementLevels` now emits
  `OnElementLevelChange` (deduped) so a mid-session re-seed repaints every consumer — hull morphs,
  HUD flowers, and ability unlock state alike. Note `SetResourceLevels` currently has **no live
  caller** (its historical MiniGame turn-reset and Hangar call sites are commented out); the emit
  future-proofs any revived re-seed path.

### The Four-Icon Ability Row (LOCKED structure — every vessel HUD)

Every vessel HUD shows **exactly four ability icons in the lower right — one per ability** — and the
order is not a layout preference, it is the element contract made visible:

> **The icons run charge → mass → space → time, left to right — the same order as the element
> flowers above them.** Each icon sits under the element that upgrades that ability (per the vessel's
> `ElementalAbilityMapSO`), so "which flower do I fill to upgrade this?" is answered by position alone.

`VesselHUDView.AbilityDisplayOrder` is the single source of that order — `VesselHUDController`'s
upgrade-seeding loop and `ElementalBarsView`'s flower layout read the same array. `OnValidate` keeps
the `abilityIcons` list sorted into it; `VesselHUDView.ValidateAbilityIconRow()` (editor-only, called
once from `VesselHUDController.Initialize`) warns on the wrong icon count, an out-of-order binding, or
a layout whose left-to-right order contradicts the bindings.

**The upgrade signal** (element hits its unlock level, default 5 — the all-petals-white flower):
`R_VesselElementalAbilityHandler.OnUpgradeStateChanged` → `VesselHUDController` →
`VesselHUDView.SetAbilityUpgraded`. Three independent layers, so the signal survives any per-vessel
presentation: (1) **authored sprite swap** (`AbilityIconBinding.upgradedSprite`, restored on re-lock —
authored art only, never runtime-generated); (2) the **element badge** — that element's petal in the
level-5 white from `ElementalBarsConfigSO`, blooming in / withering out per the continuity law, and a
*child* of the icon so per-frame icon repaints can never stomp it; (3) an optional **tint + persistent
scale bump** with a one-shot unlock punch.

- **Icons that are live gameplay gauges** (cooldown fill, heat tint, drift lean, impact flash) set
  `tintIconOnUpgrade = false` — never overload a gauge colour with upgrade meaning — and their view
  **must** override `SetAbilityUpgraded` to re-anchor its captured rest scales to
  `AbilityIconRestScale(element)`, or its own tweens settle back to the pre-upgrade scale and wipe the
  bump. `SquirrelVesselHUDView` is the reference implementation.
- **Fleet status** (audit it yourself: **FrogletTools > Vessels > Audit Vessel Ability Rows**, which
  reports every vessel's compliance against this contract from assets alone, no play mode):

  | vessel | map | icons | order | uniform | hints |
  |---|---|---|---|---|---|
  | Squirrel | complete | 4/4 | ✅ | ✅ | ✅ bound |
  | Sparrow | complete | 4/4 | ✅ | ✅ | ⚠ no switcher on its HUD |
  | Dolphin | complete | 4/4 | ✅ | ✅ | ⚠ no switcher on its HUD |
  | Manta | 3/4 named, 0/4 upgrades | 0/4 | — | — | n/a |
  | Rhino | 1/4 named, 0/4 upgrades | 0/4 | — | — | n/a |
  | Serpent | 1/4 named, 0/4 upgrades | 0/4 | — | — | n/a |

  The Dolphin deliberately runs with **both** `tintIconOnUpgrade` and `showUpgradeBadge` off —
  all four of its icons are live gauges, so the persistent scale bump is its only upgrade
  signal, which is why nothing in `DolphinVesselHUDView` writes an icon transform per event.
  Mechanics: `_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md`.

  Manta / Rhino / Serpent are blocked on **design, not wiring**: their
  `ElementalAbilityMapSO` entries are still `(open design slot)` with `Input = 0` and no
  `UpgradeLabel`, and their HUDs have 0–2 lower-right icons rather than four. Author the map
  (`Docs/ElementalAbilitySystem/FLEET_MAPS.md` §2 holds the un-approved proposals) and the icons
  before wiring — do not invent an element→ability mapping to satisfy the audit.
- Full reference: `Docs/ElementalAbilitySystem/ARCHITECTURE.md` §7.1. The `/vessel` skill
  encodes this contract (plus the rest of the per-vessel checklist) — use it for any vessel work.

**Control hints attach to the ability, never to a position.** The `(LT)`/`(RT)` glyphs are bound to
an ability and their placement is *derived*: `hint.binding` (the physical control) →
`InputHintBindingMap` → `InputEvents` → the ability bound to that input (`ElementalAbilityMapSO`,
falling back to a shared action asset via `R_VesselActionHandler.CollectBoundActions` when a vessel's
touch and gamepad maps use different events) → `VesselHUDView.TryGetAbilityIcon`.
`InputDeviceIconSetSwitcher.BindHintsToAbilities` runs this once from `VesselHUDController.Initialize`
and re-anchors each hint onto its icon — **without reparenting**, so the Xbox/PS/keyboard set
switching still works. Reassign an ability to a different input event, or move an icon in the row,
and the label follows on its own. Editor warnings flag both a hint that labels nothing and an
input-bound ability with no hint. Do NOT hand-position control glyphs against a HUD layout — that is
the brittleness this replaced. See `Docs/ElementalAbilitySystem/ARCHITECTURE.md` §7.2.

### Namespace Convention

All game code lives under `CosmicShore.*` with 8 primary namespaces:

- `CosmicShore.Core` — foundational systems: PlayFab integration, authentication, bootstrap, rewind, FTUE, dialogue runtime
- `CosmicShore.Gameplay` — all gameplay controllers: vessel, input, multiplayer, camera, impact effects, arcade, projectiles, environment, player, AI
- `CosmicShore.Data` — enums (VesselClassType, Domains, ResourceType, ShipActions, InputEvents, etc.) and data structs
- `CosmicShore.ScriptableObjects` — SO definitions (SO_Captain, SO_Vessel, SO_Game, etc.) and all custom SOAP types
- `CosmicShore.UI` — all UI: vessel HUD controllers/views, modals, screens, toast system, scoreboards, elements
- `CosmicShore.Utility` — utilities: Effects, PoolsAndBuffers, DataContainers, DataPersistence, ClassExtensions, interactive SSU components
- `CosmicShore.Editor` — editor tools: dialogue editor, shader inspectors, copy tools, scene utilities
- `CosmicShore.Tests` — edit-mode unit tests

### Key Systems & Classes

| System | Key Classes | Location |
|---|---|---|
| Vessel core | `VesselStatus` (extends `NetworkBehaviour`), `VesselTransformer`, `VesselController`, `VesselPrismController` | `_Scripts/Controller/Vessel/` |
| Vessel actions | `VesselActionSO` (base config), `VesselActionExecutorBase`, `ActionExecutorRegistry` + 40+ action SOs | `_Scripts/Controller/Vessel/R_VesselActions/`, `VesselActions/` |
| Prism lifecycle | `Prism`, `PrismFactory`, `Trail`, `TrailFollower` | `_Scripts/Controller/Vessel/`, `_Scripts/Controller/Prisms/` |
| Prism occlusion corridor (**PLATFORM LAW**) | Prisms between the player's camera and their ship go see-through so the ship is never hidden — **not a feature a vessel or mode may choose**; it must not be possible to author one in which it is off (the retired `ClearPrisms` was per-vessel opt-in on 3 of 11 vessels and had been silently dead on all three). Four layers make that structural: (1) the fade lives in the prism SHADER GRAPHS themselves (`PrismOcclusionFade` spliced into `SurfaceDescription.Alpha` on **every graph a live prism can render with** — BlockGraph + ExplodingBlockGraph), so new prisms inherit it; (2) the target binds in `VesselController.Initialize` under `IPlayer.IsLocalPilot` — the one method every vessel must call on every spawn path — so there is no per-vessel or per-scene wiring to forget; (3) `PrismOcclusionDiagnostics` screams once per material from `Prism.SyncRenderMaterial` if a prism can't fade; (4) `PrismOcclusionCoverageTests` + FrogletTools > Ecology > Prism Animation > **Validate Occlusion Corridor** fail on new content authored outside it. `PrismOcclusionCorridor` publishes just 2 `Shader.SetGlobalVector` per frame (vessel position + (outer, inner, coreAlpha)); the camera end is read on the GPU from `_WorldSpaceCameraPos`. The corridor is a **CONE** — a point at the lens, widening to the sphere that circumscribes the hull and ending at the vessel's plane (no caps, and the base graded on the same shell thickness as the sides so the whole boundary is seamless), the minimal volume that can occlude the ship (the old constant-radius capsule was an artefact of the retired `ClearPrisms` CapsuleCollider and massively over-cleared near the camera; tapering makes the cleared region a constant ANGULAR size). It is **ship-sized**: the radii are multiples of the vessel's OWN circumscribing radius, measured hull-only (skimmers excluded) and rotation-invariantly at bind — outer edge on that circle, fully-clear core at a quarter of it — so a new vessel of any size is correctly scaled with nothing authored. **Zero per-prism CPU**, no extra draw calls, corridor prisms stay in the OPAQUE queue (screen-door dither into `SurfaceDescription.AlphaClipThreshold`; kernel selected by `PRISM_OCCLUSION_KERNEL` — 2 = screen-space Worley cells, organic flecking, current (its CDF-fitted `smoothstep` remap is load-bearing: raw F1 measures 0.140 coverage error, remapped 0.0048); 1 = corridor-relative spiral, an iris anchored to the corridor; 0 = interleaved gradient noise, a dissolve anchored to the screen — only these three of the twelve candidates hold coverage fidelity over a short band; `PRISM_OCCLUSION_MORPH_RATE` slowly evolves the pattern off `_Time.y` at zero CPU — Worley's cells orbit, the spiral's phase drifts, IGN can't morph because a hash has no continuity to move). Tuning: `PrismOcclusionConfigSO` (`Resources/PrismOcclusionConfig`). The ONE sanctioned hold is `SetSuppressed`, used only by the manual replay camera. See `Docs/PRISM_ANIMATION.md` §4.7 | `_Scripts/Utility/`, `_Scripts/ScriptableObjects/`, `_Scripts/Controller/Vessel/`, `_Graphics/Materials/Graphs/` |
| Prism performance | `PrismStateManager`, `PrismTimerManager`, `BlockDensityGrid` (the CPU animation managers — `PrismScaleManager`/`MaterialStateManager`/`AdaptiveAnimationManager` — were deleted under the clock-material law; see `Docs/PRISM_ANIMATION.md`) + `PrismDebris` (batched pure-entity death VFX for **both** death visuals: a frame's prism deaths spawn as ONE `em.Instantiate(prototype, N)` batch per family — explosions AND fauna-consumption suctions — with full-duration clock animation and sweep-based batch retirement. A live explosion costs zero per-frame CPU; a live suction costs ONE `float3` (its convergence target MOVES — every implosion comes from `Prism.Consume` and every call site passes a live creature Transform — so the §1 exception rides a per-record refresh with a CPU-mirrored culling envelope). The per-death path is split by five `Prism.Destroy.*` markers. **The pooled `PrismExplosion`/`PrismImplosion` GameObjects are NOT a working visual fallback** — under strict clock mode an explosion with no render entity draws nothing and an implosion draws a static block, both loudly, by design; their live job is being the CONFIG source (mesh/material/layer/clamp band/duration) the batch reads off the pool prefab. Retiring them is tracked as `Docs/PRISM_ANIMATION.md` D4/§4.6.1 — a refactor, not a deletion) | `_Scripts/Controller/Managers/`, `_Scripts/Utility/Effects/` |
| Cell environments | `CellEnvironmentSpawnableBase` (shared deterministic lay/stream/noise contract) + `SpawnableAtlantis` (Scurry intensity 4, ~69k prisms) + the freestyle seven `SpawnableYggdra`/`Daedala`/`Orrery`/`Zephyr`/`Caldera`/`Geode`/`Ourobor` (~34-41k each, rolled by Menu_Main's Cell via `CellConfigDataSO.EnvironmentPrefab`). Two are built AROUND the nucleus and lay **nothing inside the node-control radius** (an authored environment in there pre-awards node control): **Caldera** — four inward-aimed volcanic massifs in tetrahedral symmetry, no ground plane (`Docs/ECOSYSTEM.md` §18.1) — and **Ourobor** — three interlocked ULTRAWIDE Möbius bands of rolling countryside with a cityscape on BOTH faces, so stalagmites become stalactites and no global "up" survives a lap (`§18.2`). Alongside them, **`SpawnableHesperides`** — the GARDEN cell, the one environment whose world is the **planting**: ~12k authored prisms of architecture (terraces, pergolas, trellises, aqueduct, hanging baskets, super-shielded orchard gate, danger brambles) that `Sow`s ~560 `FloraPlantingSite`s — each tagged with its ground kind (`FloraSiteKind`: Bed/Climb/Basket/Water/Ledge) — which the Cell hands to its ordinary flora spawner (`Cell.TryTakePlantingSite(cfg.PreferredSites, …)` → `Flora.SetPlantPositionOverride(pos, up)`), so a mature Hesperides reaches Yggdra's ~33k prisms by GROWTH — living, grazeable `PhyllotacticFlora` in eight forms (Arbor/Rosette/Frond/Coral/Spire/Tendril/Reed/Lantern) plus gyroid + Schwarz P topiary — not by lay. One growth model, forms are parameters; prisms are shaped by ROLE (stem spans its segment, leaf spans its reach and attaches to the stalk) with depth taper, per-prism jitter, cupped alternating whorls, gravity droop and spiral twist. See `Docs/ECOSYSTEM.md` §21. `EnvironmentLoadVeil` (gate-less scenes defer past boot then hold a connecting-style veil), `CellEnvironmentBaselineMeasurer` (FrogletTools > Ecology > Measure Cell Environment Baselines - PhaseThresholds must ride each measured baseline; see `Docs/ECOSYSTEM.md` §18) | `_Scripts/Controller/Environment/Spawning/`, `_Scripts/Controller/Environment/MiniGameObjects/`, `_Scripts/Editor/` |
| Prism spatial index | `PrismSpatialIndex` (formerly `PrismAOERegistry`) — THE canonical spatial index of all live prism mass: Burst AOE damage queries + growth occupancy (`TryReserve` claim-before-spawn closes the disabled-collider spawn race) + bucket hash grid. One registration lifecycle (`Register`/`MarkDestroyed`/`MarkRestored`/`Unregister`/`UpdatePosition`), multiple query views. Do not build parallel spatial stores or query prisms via physics — see `Docs/SPATIAL_INDEX.md` | `_Scripts/Controller/Managers/` || Shield octahedra | `PrismOctahedronShield` (the SHIELDED state's octahedron: per-face bloom engage + shatter-overlay disengage, mass scales with volume; the COLLIDER stays the authored primitive box TRIGGER — the octahedron is a look-only change, because a convex-mesh trigger is invisible to trigger-skimmers and a convex-mesh solid is invisible to solid swipes, whereas the primitive box trigger is seen by both, exactly like an unshielded prism; shape-precise shielded collision is SHIPPED as the spatial-index shell tier: `PrismShellContactManager` + `PrismSpatialIndex.CollectShellContacts` + `ShieldShellMath` run an exact Burst narrowphase — sphere/capsule/OBB probes vs the octahedron and vs the stella as the NON-CONVEX union of its two tetrahedra (spike-tip grazes hit, inter-spike gaps inside the bounding box do not) — dispatching through the same AcceptImpactee effect chain while Skimmer/VesselImpactor suppress box-trigger dispatch for shell-owned pairs; see Docs/SPATIAL_INDEX.md § Shell view), `PrismStellatedOctahedronShield` (the SUPER-SHIELDED state's stellated octahedron / Stella Octangula — the Skim Race track look; engaged by `PrismStateManager.ActivateSuperShield` with the OPAQUE team material, reversed by `DeactivateShields`), testers, `OctahedronMeshGenerator` / `StellatedOctahedronMeshGenerator` (`PopulateMesh*` + `GetSharedShieldMesh` quantized-geometry caches). **Both integrate with the instanced prism render path via the `SetExoticVisualActive` / `SetRenderMeshOverride` handoff — see the anti-pattern below on why a bare MeshFilter swap renders nothing** | `_Scripts/Controller/Vessel/`, `_Scripts/Utility/` |
| Impact effects | `ImpactorBase` + 11 impactor types, 20+ Effect SO types | `_Scripts/Controller/ImpactEffects/` |
| Swing kinematics | `SkimmerSwingKinematics` (rigid-body velocity of any point on a skimmer that MOVES relative to its vessel — the Rhino's sword: `v = v_vessel + omega_vessel x r + R * v_rel`, every rate differentiated in the VESSEL's frame so translation/teleports can't leak in; `ClosestBladePoint`/`NormalizedAlongBlade` recover WHICH part of the blade a contact landed on, hilt/tip derived from the pivot, never authored) + `SkimmerSwingKinematicsConfigSO`; composed into impacts by `PrismEffectHelper.ContactVelocity` so a destroyed prism gets the velocity of the part that hit it (a tip strike, not the hull). Skimmers without the component collapse to the previous `Course * Speed` exactly. The magnitude survives to the screen via `PrismEffectHelper.DamageProportional`, which hands the debris velocity over **as final** — `Prism.Explode` passes it through untouched (the supplied `DebrisSpeedLimit` marks it) instead of applying the legacy `/ prismProperties.volume`. **That divide is dead code**: `SetupDestruction` disables the scale animator before reading the volume, `GetCurrentVolume()` returns 0 once disabled, so `Max(0,1)` pins the divisor to exactly 1 for every prism — the legacy gain is just `inertia`. Never pre-multiply by volume expecting it to cancel; the leftover is a straight volume multiplier that damps small prisms (a Rhino trail sliver is ~0.75) and pins large ones to the ceiling. Opt-in per effect (`proportionalDebris`) — on for the sword AND the hull (`VesselDamagePrismEffectSO`), since every vessel's `Inertia` is 1 and the legacy hull formula therefore landed under the clamp's FLOOR, making every ram produce an identical 30 u/s; with both proportional a hull hit and a parked-sword hit at the same velocity now impart the same magnitude. Debris ships at **1/3** the physical read via one tuning group that must move together — `restitution` + `debrisSpeedLimit` on the three damage SOs, `debrisRestitution` + `Inertia` on `AOEExplosion` (the **AOE blasts** joined the group; see below), and `minSpeed`/`maxSpeed` on `PrismExplosion.prefab` (the band also carries the clamp-bound legacy paths, so the retune is uniform). On the three damage SOs `inertia` is NOT the lever — proportional paths ignore it and legacy paths are saturated — but on an AOE blast running `proportionalDebris` it IS the single lever: the blast supplies its OWN ceiling, so `Inertia` scales throw AND shatter linearly, and `debrisRestitution x Inertia = 1` holds the pre-existing shatter rate. `restitution` also drives the shatter rate, so shatter violence tracks impact force. A parked sword must add exactly zero, so elongation (ambient shield scaling, +15/-5 u/s at the tip) defaults off, `restDeadbandSpeed` zeroes sub-threshold residue (which rectifies upward, `|v+n|>|v|`), and `AngularVelocity` reads the angle off the quaternion's vector part via `atan2` — `ToAngleAxis`/`acos` returns exactly zero below ~0.01 deg/frame in float32 and drops slow vessel rotation. See `_Scripts/Controller/Vessel/R_VesselActions/RHINO_SHIELD_SWIPE.md` § "Swing velocity model" | `_Scripts/Controller/Vessel/`, `_Scripts/Controller/ImpactEffects/EffectsSO/Helpers/` |
| Forcefield crackle | `SkimmerForcefieldCracklePrismEffectSO` (computes impact points via `Collider.ClosestPoint`), `ForcefieldCrackleController` (`[ExecuteAlways]`, 16-impact ring buffer + MaterialPropertyBlock arrays, owns all visual params), `ForcefieldCrackle.hlsl` (FBM electrical arcs on geodesic sphere), `ForcefieldCrackleControllerEditor` (edit-mode preview) | `_Scripts/Controller/ImpactEffects/EffectsSO/Skimmer Prism Effects/`, `_Scripts/Controller/Vessel/`, `Assets/Materials/Graphs/`, `_Scripts/Editor/` |
| Camera | `CustomCameraController`, `VesselCameraCustomizer`, `CameraSettingsSO`, `ICameraController`, `ICameraConfigurator` | `_Scripts/Controller/Camera/` |
| Vessel HUD | `IVesselHUDController`, `IVesselHUDView`, per-vessel controllers & views (Sparrow, Squirrel, Serpent, Manta, Rhino, Dolphin) | `_Scripts/UI/Controller/`, `_Scripts/UI/View/`, `_Scripts/UI/Interfaces/` |
| Elemental bars | `ElementalBarsView` (5-petal flower per element), `ElementalBarsConfigSO` (shared colour/sprite/juice spec), `ElementalBarsController` (per-vessel driver), `ElementalPetalBarWirer` (editor setup) | `_Scripts/UI/View/`, `_Scripts/ScriptableObjects/`, `_Scripts/Controller/Vessel/`, `_Scripts/Editor/` |
| Arcade games | `MiniGameControllerBase`, `MultiplayerMiniGameControllerBase`, `MultiplayerDomainGamesController`, `ScoringRuleSO` family | `_Scripts/Controller/Arcade/` |
| Resource system | `ResourceSystem`, `R_VesselActionHandler`, `R_VesselElementStatsHandler` | `_Scripts/Controller/Vessel/` |
| Object pooling | `GenericPoolManager` (Unity `ObjectPool<T>` with async buffer maintenance) | `_Scripts/Utility/PoolsAndBuffers/` |
| Player system | `Player` (NetworkBehaviour, `IPlayer`), `RoundStats` | `_Scripts/Controller/Player/` |
| Cell-relative spawn ring | `CellSpawnFormation` (pure math: N players on a sphere around the cell, all facing it — 4 tetrahedral, 3 equilateral triangle, 2 antipodal, 5+ Fibonacci), driven by `ServerPlayerVesselInitializer.arrangeSpawnPointsAroundCell` at `Cell.ExpectedNucleusWorldRadius + spawnDistanceOutsideNucleus`. Opt-in per scene (on for Crystal Capture). Tests: `CellSpawnFormationTests` | `_Scripts/Utility/`, `_Scripts/Controller/Multiplayer/` |
| Menu navigation | `ScreenSwitcher`, `IScreen`, `ModalWindowManager`, `ProfileDisplayWidget`, `NavLink`/`NavGroup` | `_Scripts/UI/`, `_Scripts/UI/Interfaces/`, `_Scripts/UI/Elements/`, `_Scripts/UI/Modals/` |
| Freestyle toys | `Toy` (base world-trigger; bloom, local-user + freestyle gating, exit-gated re-arm), `MatrixToy` (the one-toy-opens-into-many base: a pass unfolds a matrix of choices out along the outward radial, another folds it away — shared by the cell selector, painting gallery, and vessel changer), `SwapToy` + `SwapToySetCoordinator<T>` (a small set of toys showing "the options you're not on", each flips to your previous option on use — the domain changer), `VesselChangerToy` (one toy opening into a matrix of mini ship models via `VesselModelBuilder`, reuses `RequestSwap` + restores freestyle control after swap), `DomainChangerToySet` (two toys tinted the domains you're not, `RequestSetDomain_ServerRpc`), `PaintingGalleryToy` + `PaintingToy` + `PaintingRunner` (one toy opening into a matrix of painting stations; multi-stroke multi-domain connect-the-dots: domain gates, pen-up, cone/jack stroke markers in prism material, resumable progress that survives folding the gallery away) + `PaintingDefinitionSO`/`PaintingPresetLibrary`/`PaintingStrokeToolkit` (stroke data + 16 grandiose 3D presets + the curl-field stroke library + Star/Rainbow/Saturn/Taj Mahal generators; runtime flight-continuity stroke ordering via `OrderForFlightContinuity`) + `PaintingProgressStore`/`PaintingPrismStore` (local JSON progress + per-prism drawing state, regrown on return) + `PaintingShareExporter` (self-contained WebGL HTML → NativeShare), `ConveyorToy` + `MicrosceneConveyor` + `Microscene` + `MicroscenePatterns` + `MicroscenePatternsGrand` + `MicroscenePainter` (Wanderway: on/off toggle streaming a speed-scaled field of procedurally-varied microscenes — 48 recipes: the classic forty incl. spine×motif Medley composers, plus the monument-scale grand eight — ahead of the vessel, structurally painted across the full domain triad with capped danger/shield accents; a closed conveyor of a 30k-prism conserved stock built once behind an `EnvironmentLoadVeil` + skimmable crystals + cell-released lifeforms), `CellSelectorToy` + `CellSelectorToyDefinitionSO` (the world picker AND the freestyle reset: a matrix of bare `CellMiniatureBuilder` scale models over `Cell.AvailableConfigs`, sampled from the generator's real output with no prisms spawned; selection routes through `Cell.RequestCellSwap`), `ToyMatrixStation` (shared fly-through choice station), `ToyboxController` (places toys near the membrane), `ToyboxSO`/`ToyDefinitionSO` (registry + deferred unlock state), `ToyboxSetupTool` (editor) | `_Scripts/Controller/Toys/`, `_Scripts/ScriptableObjects/Toys/`, `_Scripts/Editor/` |
| Menu screens | `HomeScreen`, `ArcadeScreen`, `StoreScreen`, `HangarScreen`, `LeaderboardsMenu`, `EpisodeScreen` | `_Scripts/UI/Screens/` |
| UI | Elements, FX, Modals, Screens, Views + `ToastService` / `ToastChannel` (menu) + in-game toast feed (`GameToastAPI`, `GameToastController`, `GameToastView`, per-mode `GameToastConfigSO` — see `_Scripts/UI/GameToastSystem/GAME_TOASTS.md`) | `_Scripts/UI/` |
| Telemetry | `VesselTelemetryBootstrapper`, `VesselTelemetry` (abstract) + per-vessel subclasses, `VesselStatsCloudData` | `_Scripts/Controller/Vessel/` |
| Analytics | `AnalyticsServiceFacade` (UGS Analytics, single writer; consent/age-gated), `UGSStatsManager` (leaderboards) | `_Scripts/System/Instrumentation/`, `_Scripts/UI/` |
| Bootstrap / DI | `AppManager` (orchestrator + IInstaller), `BootstrapConfigSO`, `SceneTransitionManager`, `ApplicationLifecycleManager`, `ApplicationLifecycleEventsContainerSO` | `_Scripts/System/`, `_Scripts/System/Bootstrap/`, `_Scripts/ScriptableObjects/` |
| Threading / Main-thread affinity | `MainThreadDispatcher` (captures Unity's `SynchronizationContext` at `BeforeSceneLoad`, exposes `IsOnMainThread` + `SwitchToMainThreadAsync()`), `UniTaskExtensions.AsMainThread<T>()` (boundary helper for UGS / Netcode `Task` awaits), `SceneTransitionManager.SetFadeImmediate` (canary that fires if a UGS continuation reaches it off-thread) | `_Scripts/Utility/`, `_Scripts/Utility/ClassExtensions/`, `_Scripts/System/Bootstrap/`. See `Docs/THREADING.md`. |
| App state machine | `ApplicationStateMachine` (single-writer phase tracker), `ApplicationStateData` / `ApplicationStateDataVariable` (SOAP state), `ApplicationState` enum | `_Scripts/System/`, `_Scripts/ScriptableObjects/SOAP/ScriptableApplicationState/`, `_Scripts/Data/Enums/` |
| Scene management | `SceneLoader` (MonoBehaviour, DontDestroyOnLoad in Bootstrap, game launch + restart + return-to-menu, SOAP code subscriptions), `SceneNameListSO` (centralized scene names, DI-registered) | `_Scripts/System/`, `_Scripts/Utility/DataContainers/` |
| Authentication | `AuthenticationServiceFacade` (facade/writer), `AuthenticationController` (MonoBehaviour adapter), `AuthenticationSceneController` (scene UI), `SplashToAuthFlow` (splash routing), `AuthenticationData` / `AuthenticationDataVariable` (SOAP state) | `_Scripts/System/`, `_Scripts/ScriptableObjects/SOAP/ScriptableAuthenticationData/` |
| Friends | `FriendsServiceFacade` (facade/single-writer for UGS Friends SDK), `FriendsInitializer` (MonoBehaviour bridge + presence), `FriendsDataSO` (SOAP container: 4 lists + 4 events), `FriendData`/`FriendPresenceActivity` (SOAP data types) | `_Scripts/System/`, `_Scripts/Controller/Party/`, `_Scripts/Utility/DataContainers/`, `_Scripts/ScriptableObjects/SOAP/ScriptableFriendData/` |
| Friends UI | `FriendsListPanel` (combined Online + Requests, no tabs), `OnlineInfoEntry` (online row = invite/cancel/kick button), `RequestInfoEntry` (accept/decline; friend-request + party-invite) | `_Scripts/UI/Elements/` |
| Player data | `PlayerDataService` (cloud profile, XP, rewards), `PlayerProfileData` | `_Scripts/UI/Views/` |
| Network monitoring | `NetworkMonitor` (polling), `NetworkMonitorData` / `NetworkMonitorDataVariable` (SOAP events) | `_Scripts/System/`, `_Scripts/ScriptableObjects/SOAP/ScriptableAuthenticationData/` |
| Multiplayer | `MultiplayerSetup` (NetworkManager lifecycle + UGS sessions), `ServerPlayerVesselInitializer` (base spawner), `ClientPlayerVesselInitializer` (pair initializer + RPCs), `ServerPlayerVesselInitializerWithAI` (AI pre-spawner), `MenuServerPlayerVesselInitializer` (menu autopilot), `MenuCrystalClickHandler` (play-from-menu), `DomainAssigner` (team pool) | `_Scripts/Controller/Multiplayer/` |
| Party / Invite | `HostConnectionService` (presence lobby + party sessions, single-writer to `HostConnectionDataSO`), `PartyInviteController` (Netcode host↔client transitions), `FriendsInitializer` (Friends service bridge) | `_Scripts/Controller/Party/` |
| Party UI | `ArcadeLobbyList` (4-slot party panel; per-slot kick ✕ for host) + `FriendInfoSlot` (single slot), `FriendsListPanel` (Online + Requests), `OnlineInfoEntry` (online row = invite button; "IN YOUR PARTY" + cancel-✕/kick states), `RequestInfoEntry` (accept/decline), `PartyInviteNotificationPanel` (bottom-left global invite popup) | `_Scripts/UI/Elements/` (`PartyInviteNotificationPanel` in `_Scripts/UI/Screens/`) |
| Menu scene controller | `MainMenuController` (sub-state machine: None→Initializing→Ready→LaunchingGame), `MainMenuState` enum | `_Scripts/System/`, `_Scripts/Data/Enums/` |
| Audio | `AudioSystem` (DI singleton), `ScriptableEventGameplaySFX` / `EventListenerGameplaySFX` (decoupled gameplay SFX via SOAP) | `_Scripts/System/Audio/`, `_Scripts/ScriptableObjects/SOAP/ScriptableGameplaySFX/` |
| App systems | Favorites, LoadOut, Quest, Rewind, Squads, UserAction, UserJourney, Xp, Ads, IAP, DailyChallenge, TrainingGameProgress | `_Scripts/System/` |
| ScriptableObjects | `SO_Vessel`, `SO_Captain`, `SO_Game`, `SO_ArcadeGame`, `SO_Element`, `SO_Mission`, etc. | `_Scripts/ScriptableObjects/` |

### Async Pattern

- Prefer UniTask over coroutines for new code
- For ScriptableObjects that need async: use a `CoroutineRunner` singleton proxy or async/await with cancellation tokens
- Always include `CancellationToken` for anything non-trivial — UniTask respects play mode lifecycle better than raw `Task`
- Bootstrap uses `UniTaskVoid` with `CancellationTokenSource` for the async startup sequence
- Prefer SOAP event channels (`ScriptableEvent`) over `UniTask.WaitUntil` polling for waiting on state changes from other systems. Subscribe to the relevant event and react when it fires, rather than polling a condition every frame
- **Every `await` of a UGS / Netcode `Task` uses `.AsMainThread()`** — see the "Threading & Main-Thread Affinity" section above and `Docs/THREADING.md`. UniTask's own `SwitchToMainThread()` and `Yield(PlayerLoopTiming.Update)` are unreliable on this UniTask version and must not be used as thread-marshaling primitives.

### Anti-Patterns to Avoid

- `FindObjectOfType` / `GameObject.Find` in hot paths
- `Instantiate`/`Destroy` in gameplay loops — use object pooling
- Excessive `GetComponent` calls — cache references
- Mixed coroutine/async patterns in the same system
- Singletons, static events, or direct references for cross-system communication — use SOAP `ScriptableVariable` and `ScriptableEvent` instead
- C# `event Action` / delegates on MonoBehaviours for broadcast patterns — use SOAP `ScriptableEvent` channels
- `renderer.material` (clones material) — use `renderer.sharedMaterial` + MaterialPropertyBlock instead
- Swapping a prism's `MeshFilter` mesh (or its `MeshRenderer` materials) directly to restyle it — **prisms draw through the instanced companion entity (`PrismRenderService`), so a GameObject-local swap renders NOTHING**: the companion keeps drawing the plain box while your new mesh sits on a renderer that isn't drawing (exactly how the stellated super-shield first shipped invisible). Any per-prism visual override must hand rendering across explicitly: `Prism.SetExoticVisualActive(true)` while showing per-prism-unique geometry (engage morphs, shatter overlays), then `Prism.SetRenderMeshOverride(sharedMesh)` + `SetExoticVisualActive(false)` once the geometry settles to something shareable (fetch it from the quantized-geometry caches — `OctahedronMeshGenerator.GetSharedShieldMesh` / `StellatedOctahedronMeshGenerator.GetSharedShieldMesh` — so same-size prisms batch as ONE mesh instead of a per-prism draw-call storm), and `ClearRenderMeshOverride()` + `SetExoticVisualActive(false)` on the way back (including pool-return `OnDisable`). `PrismOctahedronShield` and `PrismStellatedOctahedronShield` are the reference implementations. **Two corollaries an exotic visual must respect** (`Docs/PRISM_ANIMATION.md` §4.5, learned the hard way from §3.8 #10): (1) taking over *rendering* must never suppress companion-entity *creation* — clock stamps are one-shot, so a prism with no entity at the instant it is stamped loses that animation permanently; entity existence and entity visibility are separate concerns, and the transient morph mesh must never be registered with Entities Graphics (it mints a `BatchMeshID` per prism) — read the batchable geometry from `Prism.EffectiveRenderMesh()`/`SyncRenderMesh()`; (2) a visual state applied while `!Prism.IsCreationComplete` is part of the prism's BIRTH, not a transition on live mass — it snaps (`PrismStateManager.IsBirthTransition`), because the grow-in bloom already carries continuity of existence and a morph there is invisible by construction while costing draw calls, per-frame mesh rebuilds, and one SFX per prism laid
- **Any multiframe CPU update that animates a prism** — per-frame/per-tick writes of a prism's transform scale, colors, shader parameters, positions, or morph meshes to play out a visual transition (coroutines, DOTween, UniTask loops, manager passes, per-frame `SetPropertyBlock`/`SetComponentData`). **The clock-material law (`Docs/PRISM_ANIMATION.md`, LOCKED)**: prism animation is a pool-pull whose material accepts initial conditions, ONE stamp of those conditions (start time, rate/duration, endpoints — per-instance Hybrid-Per-Instance properties), the GPU runs the course off the shader clock with zero further CPU writes, and ONE scheduled swap to the end-state prism at the analytically-known end frame (`PrismTimerManager`-class scheduler, never per-frame progress polling). Colliders and gameplay state (spatial index, volume, state flags) go to their FINAL values at the START of the animation — only photons animate. Interruptions re-stamp (current value is analytic). **STRICT: there is no legacy fallback tier** — never reintroduce a CPU animation path "just until the shader is wired"; an unwired graph fails loud (`PrismClockDiagnostics`) and snaps, which is the intended forcing function. If a visual seems impossible to express as `f(clock, initial conditions)`, that's a design discussion (live gameplay data vs. animation — see the doc), not a license for a per-frame loop
- Per-object coroutines at scale — use centralized timer/manager systems (see Prism Performance Audit)
- **Guarding `using UnityEngine;` (or any using an unguarded declaration needs) behind `#if UNITY_EDITOR` / `#if DEVELOPMENT_BUILD`.** A guard must cover a self-consistent unit: if the class declaration is outside the guard, everything it depends on must be too. `#if UNITY_EDITOR\nusing UnityEngine;\n#endif` above an unguarded `class Foo : MonoBehaviour` compiles fine in the Editor and in Development builds, then fails the **Release** player build with `CS0246: 'MonoBehaviour' could not be found` — which is the automated build, not yours. Likewise, never touch the `UnityEditor` namespace outside `#if UNITY_EDITOR` in a file that isn't under an `Editor/` folder. Run `python3 Tools/Build/check_conditional_compilation.py` (~1s, no Unity needed) before committing any guarded script. Full rules + the two safe patterns: `Docs/CONDITIONAL_COMPILATION.md`
- New spatial queries against prisms via `Physics.OverlapSphere` / `Physics.CheckBox`, or building a new grid/registry/octree over prisms — `PrismSpatialIndex` is THE canonical spatial index of prism mass (occupancy, AOE, proximity). Physics queries are also structurally blind to fresh prisms (colliders disabled for the first 0.6s after spawn). Add new query shapes to `PrismSpatialIndex` instead — see `Docs/SPATIAL_INDEX.md`
- `await UniTask.SwitchToMainThread()` or `await UniTask.Yield(PlayerLoopTiming.Update)` as a thread-marshaling fix — they don't reliably switch threads on this UniTask version. Use `.AsMainThread()` (see `Docs/THREADING.md`)
- Raising a SOAP `ScriptableEvent` from a UGS / Netcode `Task` continuation without ensuring the continuation has resumed on the main thread first — SOAP `Raise()` invokes listeners inline, so off-thread raises crash any listener that touches Unity state
- Touching a `UnityEngine.Object` (incl. `== null` checks routing through `op_Equality`) in a `Task` continuation without `.AsMainThread()` upstream — throws `EnsureRunningOnMainThread`
- Caching a UGS singleton `*.Instance` (e.g. `MultiplayerService.Instance`) in a service **constructor** — lazy DI singletons are constructed during Bootstrap DI resolution, *before* `UnityServices.InitializeAsync()` completes, so `*.Instance` is null at construction and gets pinned null forever. Instead expose a private property that resolves at use time: `private IMultiplayerService _multiplayerService => MultiplayerService.Instance;` — always reads the live `Instance` at the call site (see `PartySessionService` / `PresenceLobbyService`)
- Subscribing to per-`RoundStats` C# stat events (`OnScoreChanged`, `OnAnyStatChanged`, `OnCrystalsCollectedChanged`, …) with cleanup gated on `OnMiniGameTurnEnd`, or unsubscribing by iterating `gameData.RoundStatsList` — `RoundStats` lives on the **persistent** Player NetworkObject (survives every scene transition), a mid-turn scene exit never fires the turn-end cleanup, and `SceneLoader.LoadSceneAsync` clears the roster lists via `ResetRuntimeData()` BEFORE the old scene's objects are destroyed, so list-based unsubscribe loops detach nothing. The leaked delegates fire inside the next game's stat-setter raise chains and can silently kill the game-end flow (`Docs/ScoringSystem/BUGS.md` B15). Instead: track the stats you actually subscribed to and detach from that record in `OnDestroy` (see `NetworkCrystalCollisionTurnMonitor` / `MultiplayerHUD`); `Player.PrepareForNewScene` / `InitializeForMultiplayerMode` purge any stragglers via `RoundStats.ClearEventSubscriptions()` at every scene entry

## Shader & Visual Development

### HLSL / Shader Graph

- Custom Function nodes use HLSL files stored in a consistent location
- Function signatures must follow Shader Graph conventions (proper `_float` suffix usage, sampler declarations)
- Blend shapes are converted to textures for shader-driven animation (no controller scripts — animation is entirely GPU-driven for performance)
- Edge detection, prism rendering, Shepard tone effects, and speed trail scaling are active shader systems
- Procedural HyperSea skybox shader with Andromeda galaxy, domain-warped nebulae, and configurable star density

### Performance Standards

- Use `Unity.Profiling.ProfilerMarker` with `using (marker.Auto())` for profiling, not manual `Begin`/`EndSample`
- Watch for `Gfx.WaitForPresentOnGfxThread` bottlenecks — usually indicates GPU sync issues, not CPU
- Static batching, object pooling, and draw call management are always priorities
- Test with profiler before and after optimization changes — don't assume improvement
- GPU instancing enabled on all prism and VFX materials
- Prism scale/material/effect animation is GPU-clock-driven (the clock-material law, `Docs/PRISM_ANIMATION.md`) — the former CPU Jobs+Burst animation managers are deleted
- Burst-compiled spatial queries replace Physics-based AOE prism damage (`PrismSpatialIndex` — see `Docs/SPATIAL_INDEX.md`)
- Cache-line-aware data layouts with hot/cold splitting and bit-packed flags (`PrismSpatialData` / `PrismDamageData` in `PrismSpatialIndex`)
- Growth occupancy checks use `PrismSpatialIndex.TryReserve` (claim-before-spawn), never `Physics.CheckBox` — prism colliders are disabled for the first 0.6s after spawn, so physics queries are structurally blind to fresh prisms

### Prism System Performance

The prism system is the most performance-critical gameplay system. See `Assets/_Scripts/Game/Prisms/PRISM_PERFORMANCE_AUDIT.md` for the full audit (note: audit doc remains in the vestigial `Game/` directory). Key facts:

- Each prism is a full GameObject with 5-6 MonoBehaviours + BoxCollider + MeshRenderer
- At 2,000 prisms: ~12,000 MonoBehaviour instances + 2,000 colliders
- Scale and material animation are already Jobs + Burst optimized
- Main bottlenecks: explosion/implosion VFX (per-object UniTask), physics colliders, material instancing leaks
- Active optimization: `PrismTimerManager`, per-frame explosion VFX cap, `EventListenerBase` GC elimination

## Testing

### Test Infrastructure

- **Framework**: Unity Test Framework 1.6.0 (NUnit-based)
- **Edit-mode tests**: `Assets/_Scripts/Tests/EditMode/` — 17 test files covering enums, data SOs, geometry utils, party data, resource collection, disposable groups, camera settings, etc.
- **Bootstrap tests**: `Assets/_Scripts/System/Bootstrap/Tests/` — `AppManagerBootstrapTests` (file: `BootstrapControllerTests.cs`), `BootstrapConfigSOTests`, `SceneTransitionManagerTests`, `ApplicationLifecycleManagerTests`, `ApplicationStateMachineTests`, `SceneFlowIntegrationTests`
- **Multiplayer tests**: `Assets/_Scripts/Controller/Multiplayer/Tests/` — `DomainAssignerTests`
- **PlayFab tests**: `Assets/_Scripts/System/Playfab/PlayFabTests/` — `PlayFabCatalogTests`
- **SOAP framework tests**: `Assets/Plugins/Obvious/Soap/Core/Editor/Tests/`
- **Test scenes**: `Assets/_Scenes/TestInput/`, `Assets/_Scenes/Game_TestDesign/`

### Build & CI

No automated CI/CD pipeline is currently configured. Builds are manual. Build profiles live in `Assets/Settings/Build Profiles/`.

## Editor Tooling (LOCKED convention — read `Docs/TOOLING.md` before adding any `[MenuItem]`)

**Every first-party editor tool lives under ONE menu root, `FrogletTools/`, and appears
automatically in `FrogletTools > Froglet Master Tool`.** The `Tools/Cosmic Shore/…` and
`Cosmic Shore/…` roots were retired — do not reintroduce them, and do not add a tool under
`Tools/`, `Window/`, or a new root of your own.

- **Discovery is automatic, never registered.** `FrogletToolRegistry` reflects over `[MenuItem]`
  attributes; a tool shows up on the board the moment its path starts with `FrogletTools/` and it
  compiles. There is no manifest to update. That prefix is also the only filter, so third-party
  package menus (PlayFab, FMOD, Soap, Quick Scene Pro) are never picked up and are left where
  their vendors put them.
- **The board is a card grid**: one collapsible colour-coded section per category, one card per
  tool (title, description, five-dot importance), most important first, flowing into as many
  columns as the window is wide enough for.
- **`[FrogletTool(category, Importance, Description)]`** on the same static method as the
  `[MenuItem]` controls the section, the ranking (1–5, which is also the dot rating on the card)
  and the blurb. It is optional — omit it and the registry infers a category from the path/type
  name and uses importance 3. The attribute compiles into the **editor** assembly, so only files
  under an `Editor/` folder can use it; a runtime-assembly tool behind `#if UNITY_EDITOR` still
  appears, just with inferred metadata.
- **Draw through `FrogletEditorPalette`** (banner, `ColorButton`, `StatusPill`, `DrawCard`,
  accent stripes, semantic Ok/Warn/Error/Info colours, light-skin adaptation) so every Froglet
  window reads as one product. Do not hand-roll `GUI.color` juggling in a new window — extend the
  palette instead.
- **Prefab drift is a first-class check.** `PrefabInstanceSceneScanner` reads prefab-instance
  overrides straight out of scene YAML (fast, read-only, no scenes opened) and
  `PrefabDriftFixer` performs every write through `PrefabUtility` on a properly loaded scene.
  Use these rather than opening scenes to interrogate `PrefabUtility`, and never hand-edit scene
  or prefab YAML to "apply" an override. **FrogletTools > Ecology > Audit Cell-Owned Visuals**
  rides the same scanner for the Cell's half of this: it reports scene-placed membrane/nucleus/
  cytoplasm instances that duplicate what the scene's Cell already spawns, and Cell overrides whose
  `propertyPath` names a field the script no longer has (Unity never prunes an unresolvable
  modification, so retired fields linger for years pointing at guids no asset carries).
- **Editor-tool config belongs in a ScriptableObject**, not a hard-coded list in the window
  (`GameModePrefabKitSO` is the reference) — same config-separation rule as gameplay.
- **A tool's OUTPUT is the deliverable; the tool is scaffolding.** A wirer/setup/migration tool
  writes a scene, prefab or SO into the human's **working tree**, while the branch carries only
  the tool — so the tool merges and its data does not, and the feature is broken on every other
  machine with nothing in the diff to explain it. Any tool that writes assets therefore
  `FrogletToolChangeLedger.Record(ToolName, path)`s in the same block that writes each one and
  draws `FrogletToolShipPanel.Draw(Ship, this)`: **Validate & Push** (saves, validates, stages
  ONLY that tool's recorded paths — never `-A` — commits, pushes; protected branches refused) and
  **Retire Tool** (deletes the one-off + scratch assets, refusing while its output is still
  unpushed, so retirement can't strand it). The catch-all is **FrogletTools > Build > Pending Tool
  Changes**, which also lists dirty files no tool claimed. Contract:
  `Docs/TOOLING.md` § "Tool output is a deliverable". Agent-side gate: the `/ship-tools` skill,
  and `/ship` §2.5 — which `/ship-quick` and `/ship-deep` inherit and **no mode may skip**. A
  READER tool (audit/report only) needs none of this; say so in its doc comment.

## Shared prefabs are single sources of truth (see `Docs/GAMECANVAS.md`)

`GameCanvas.prefab` is the in-game UI surface for every mode; the same rules apply to any prefab
shared across scenes.

- **A scene override always beats the prefab.** Overrides parked in a scene are why editing the
  prefab stopped changing anything — six game-mode scenes each carried ~1,770 unapplied overrides,
  1,734 of them byte-identical. If a change should apply to every mode, **Apply to Prefab**.
- **A variant, never a copy.** If a mode needs a different canvas, use **Create ▸ Prefab Variant**.
  `GameCanvas-HexRace.prefab` is a hard copy, which severed propagation and left 8 references
  dangling into the other prefab asset.
- **Genuinely per-mode values go in config or code**, not a scene override: an SO keyed by
  `GameModes`, or a runtime resolve. There is exactly one `MiniGameControllerBase` per gameplay
  scene, so the canvas finds it itself (`MiniGameHUD.EnsureReadyButtonWiring`,
  `Scoreboard.ResolveGameController`) — an explicit inspector assignment still wins.
- **Never bind a UnityEvent to a concrete controller subclass.** `OnReadyClicked` is public on
  `MiniGameControllerBase`; naming `HexRaceController` in the inspector creates a per-scene
  override for no gain.
- **Run `FrogletTools > Game Modes > Game Mode Prefab Kit` ▸ Validate before committing a scene**
  that contains a shared prefab.

## Code Style

- Clean, maintainable C# — favor readability over cleverness
- Use `[Header("Section Name")]` and `[Tooltip("...")]` attributes generously on serialized fields
- Use `[SerializeField]` with private fields, not public fields
- Pattern match where it improves clarity: `effects is { Length: > 0 }`
- Use `TryGetComponent` over `GetComponent` + null check
- Prefer expression-bodied members for simple accessors: `public Transform Transform => transform;`
- Anti-spam / cooldown patterns belong in the SO config, not hardcoded
- Always assign static numeric values to enum members to prevent Unity serialization drift
- Commit messages follow conventional commits: `type(scope): summary` (see `GIT_RULES.md`)

## Debugging Methodology

When investigating issues, follow this systematic approach:

1. Reproduce the issue consistently
2. Add `ProfilerMarker`s to isolate the hot path
3. Check the call stack in Timeline view for self-time
4. Narrow to the specific derived class (base class profiling often hides the real culprit)
5. Fix, profile again, confirm improvement with data

Do not guess at performance problems. Profile first.

## Communication Preferences

- Be direct and technical. Skip preamble and motivational framing.
- When presenting solutions, lead with the code, then explain if needed.
- If you need to make a judgment call between two valid approaches, pick the one that's simpler to maintain and mention the tradeoff briefly.
- When refactoring, preserve the existing naming conventions and folder structure unless explicitly asked to reorganize.
- For shader work: always specify which render pipeline stage and what Shader Graph node types are involved.
- Don't repeat back what I just told you. Acknowledge briefly and move to the solution.

## What Claude Code Should Never Do

- Stop to ask "would you like me to continue?" after completing one of several related files
- Introduce new packages or dependencies without flagging it first
- Restructure folder organization or namespaces without explicit instruction
- Use `Debug.Log` as a fix — it's a diagnostic tool, not a solution
- Write a tooling, diagnostics, benchmark, or debug-overlay script that uses `#if UNITY_EDITOR` / `#if DEVELOPMENT_BUILD` without reading `Docs/CONDITIONAL_COMPILATION.md` and running `python3 Tools/Build/check_conditional_compilation.py` first. "It compiles in the Editor" proves nothing here — the Editor always defines `UNITY_EDITOR`, so this whole bug class is invisible until the Release build fails
- Leave TODO comments as a substitute for completing the work
- Generate code that compiles but ignores the established architecture patterns above
- Add if-null guards on SOAP ScriptableEvent serialized fields — fail loud
- Use `renderer.material` when `renderer.sharedMaterial` + MaterialPropertyBlock works

## Design Philosophy: Favor Emergent Systems Over Bespoke Solutions

Cosmic Shore aims to be built on a small, carefully curated set of
**fundamentals** whose interactions produce a large number of desirable
emergent outcomes. When solving a problem, maintain active awareness of
these fundamentals and prefer solutions that work *through* them rather than
*around* them.

### The fundamentals (working list)

Use the canonical term, not a casual synonym. This list is the team's current
best understanding and will be refined over time — propose additions or
corrections through the process below rather than silently inventing new
ones.

- **Domain** — team/affiliation identity attached to mass, vessels, and
  structures. Sometimes referred to casually as "color"; the canonical term
  is *domain*.
- **Mass** — the produced/consumed quantity that drives scoring, fueling,
  and cell control. **Mass is conserved: it has no passive decay.** A prism
  (the concrete unit of mass), once created, is only ever removed by an
  *active* force — a vessel using an ability, or fauna eating it. There is no
  aging, lifespan, timed culler, or growth/decay oscillator anywhere in the
  mass pipeline. Population homeostasis is the job of the **food web** (fauna
  consume mass; fauna starve when prey is scarce), never of artificial decay.
  A large accumulation of prisms is therefore a *valid* state, not a bug to
  auto-correct: it persists until an active force consumes it, and when the
  fauna that would eat it can't reach prey, the correction surfaces as fauna
  starving — not as prisms vanishing. This holds in **every scene the
  simulation runs in** — including Menu_Main's lava-lamp/freestyle, where the
  autopilot vessel *is* the gameplay vessel. There is no "cosmetic" or
  "menu-only" exemption. See "Universality" and "Don't cheat emergence" below
  and `Docs/ECOSYSTEM.md`.
- **Cells** (with `CellType`) — the regions of play that are the unit of
  territorial control. Casual language sometimes calls these "biomes"; the
  canonical term is *cell*.
- **Elementals** — the single system that governs **all** buffing and
  debuffing across vessels and their environment. If a buff or debuff isn't
  expressed through elementals, that's a smell.
- **Prisms / Prismscapes** — the geometric primitive of player-generated
  structure. Trails are the 1-dimensional case of a prismscape; higher-
  dimensional prism constructions are planned and should reuse this
  primitive rather than introducing parallel structure types. Prisms *are*
  conserved mass (see **Mass**): only active forces — vessel abilities and
  fauna consumption — remove a prism. Whether a prism is a lifeform's health-
  prism or vessel-spawned makes no difference to this rule.
- **Flora & Fauna** — populations that live on and respond to the
  fundamentals above (e.g. fauna attraction to prisms, flora growth on
  cells).
- **Vessels** — the player/AI actors whose class-specific abilities compose
  with the fundamentals above.
- **Toys** — interactive world-space stations the player's **Vessel** flies into,
  surfaced in the Menu_Main lava-lamp/freestyle "toybox". A toy has **no score and
  no end condition** — something to play with indefinitely (toys are to freestyle
  what party games are to the rest of Cosmic Shore). Added at the prompter's request;
  it earns its place by composing with the others rather than bypassing them: the
  vessel-changer cycles **Vessel**, the domain-changer cycles **Domain** (server-RPC,
  never a client write), the painting/"connect the dots" toy lays a conserved **Mass**
  prism pattern, and the **Wanderway conveyor** streams **Prisms/Mass** (a fixed stock
  it *transports* — suction-out → bloom-in — never creates or destroys), **Crystals**
  (skimmable elemental pickups), and **Flora & Fauna** (released into the containing
  **Cell** as ordinary citizens) into an endless field ahead of the vessel, and the
  **cell-selector** picks the **Cell** itself (a matrix of mini-cells over the Cell's
  *own* config rotation — the toy never authors a parallel list — routed through the
  one `Cell.RequestCellSwap` entry point; choosing the cell you are already in is the
  freestyle reset). Toys are placed relative to the **Cell** membrane (read, not
  duplicated). A toy imposes no decay/timer/win-lose, so it stays inside *Mass is
  conserved* + *don't cheat emergence* — a cell swap removes mass only because a
  player flew into a station and asked for a new world, the same **active**, explicit
  event class as a scene load, never a clock. Unlock *conditions* are deferred; the
  toybox registry + per-toy unlock state live in `ToyboxSO`.
  See `Docs/ToySystem/ARCHITECTURE.md` and `Docs/ECOSYSTEM.md §19`.

### Process for curating fundamentals

The goal is an *exhaustive, minimal* set of fundamentals — expressive enough
to solve every problem through composition, small enough that the team can
hold the whole set in their head. Every fundamental costs mental overhead
for everyone who touches the codebase, so adding one must be a deliberate
act, not a side-effect of a feature ticket.

Before treating something as a fundamental (or before proposing a new one),
run this check:

1. **Name it precisely.** Use the canonical term. If no canonical term
   exists, propose one explicitly and get it agreed before using it.
2. **Show its reach.** A fundamental earns its place by being load-bearing
   for many features. Enumerate at least three distinct features or
   behaviors that depend on it; if you can't, it's probably not fundamental.
3. **Show how it composes.** Describe how it interacts with each existing
   fundamental. Emergence comes from the cross-products between
   fundamentals, so a system that doesn't meaningfully combine with the
   others is a bespoke feature wearing a fundamental's costume.
4. **Prefer extension over addition.** If a proposed fundamental is a
   special case of, or expressible through, an existing one, extend or
   rename the existing one instead.
5. **Budget the weight.** A new fundamental must be *very* useful to justify
   the weight it adds to the set. Flag any proposed addition to the
   prompter and get explicit agreement before committing to it.

### Order of preference

When addressing a task, try these approaches in order and stop at the first
one that fits:

1. **Use an existing fundamental.** Can the goal be achieved by composing
   behaviors the current fundamentals already produce?
2. **Tune parameters.** Can it be achieved by adjusting the parameters,
   weights, or configuration of an existing fundamental?
3. **Extend a fundamental.** Can it be achieved by adding a small, general
   capability to an existing fundamental that other features could also
   benefit from?
4. **Propose a new fundamental.** Only after the steps above have been
   rejected for clear reasons, *and* after running the curation process
   above with explicit prompter sign-off.
5. **Add a bespoke solution.** Last resort, and only when a new fundamental
   would be unjustified weight.

Three similar lines is better than a premature abstraction, but a bespoke
feature that duplicates or bypasses an existing fundamental is worse than
either.

### Don't "cheat" emergence without asking

A "cheat" is any solution that directly hard-codes the desired outcome
instead of letting it arise from the interaction of the fundamentals.
Cheats are tempting because they are shorter and more predictable, but they
erode the systems that make the game's behavior rich and surprising, and
they tend to accumulate special cases.

If the most direct path to a goal would require reaching past the
fundamentals and using privileged information or a shortcut to explicitly
produce the outcome, **stop and ask the prompter for explicit permission
before doing so.** Describe the emergent alternative you considered and why
you were tempted to bypass it, so the prompter can make an informed call.

**Example.** Suppose the task is to balance the ecosystem by creating fauna
that are attracted to prisms. The emergent approach is to place prisms and
configure fauna attraction parameters (working through the Flora & Fauna
and Prism fundamentals), then let the fauna find them. A cheat would be to
use the known planted locations of the fauna to directly place or steer
things so the balance is achieved by construction. Before taking that
shortcut — for instance, before reading fauna placement data and acting on
it to short-circuit the attraction behavior — ask the prompter whether they
want the cheat or the emergent solution.

**Example (resolved): prism decay is a cheat — mass is conserved.** Cells fill
with the dominant domain's flora and "freeze solid": fauna only eat *opposing*
mass, so the leader's flora have no predator and the prism count never falls.
The tempting fix is **passive prism decay** — prisms age and die on a timer (or
a cell-level reaper culls N per tick) so the count drops on its own and flora
resume growing through the phase hysteresis. **That is a cheat** — a timed
culler is just the flora regrowth-pulse inverted, a hard-coded oscillator
reaching past the fundamentals to manufacture the breathing we want to *emerge*.
The decided answer (do not relitigate): **prisms are conserved; the only sinks
are active — vessel abilities and fauna consumption.** The down-force on a
dominant accumulation is the **food web**: opposing-domain fauna graze it down,
or, when no fauna can reach edible prey, the population crashes via starvation.
A large accumulation that nothing is eating is a *valid* equilibrium, not a
defect to auto-correct. If a future cell "freezes," fix it by giving an active
force a reason/ability to consume that mass (or by tuning fauna diet, reach, and
spawning) — never by adding decay. The flora regrowth pulse that currently
exists is the growth-side counterpart of this same cheat and is flagged for
retirement, not extension. See `Docs/ECOSYSTEM.md`.

**Example (resolved & reverted): the menu trail cap is a cheat — no "cosmetic"
exemptions.** The Menu_Main autopilot vessel lays prisms indefinitely, so a
perf-motivated commit added a per-trail ring-buffer cap (`maxTrailBlocks` /
`Trail.RemoveOldest`, commit `64d8f0c8`) that silently recycled the oldest
trail prism on every new spawn, rationalized as "cosmetic, menu-only —
gameplay unaffected." That rationale was false by construction: the lava lamp
*is* freestyle (one system, two names — see "Lava-Lamp Mode"), so the same
capped vessel is the one the player flies, and the cap followed them into
freestyle flight as an age-based trail limit — exactly the passive-removal
cheat §0 of `Docs/ECOSYSTEM.md` rejects. The commit was reverted. The decided
answer (do not relitigate): **there is no context in which trail caps, prism
TTLs, or idle cullers are acceptable.** If prism accumulation in the menu (or
anywhere) is a perf problem, solve it with the universal systems: **fauna
cleanup** (cleanup is one of the fauna's jobs — foragers consume trail mass
through the food web) or **pause/throttle the spawner** (not creating mass is
allowed; aging it out is not). **One authorized exception exists** — the
Wanderway rolling tether, granted by explicit sign-off to make that toy a truly
infinite runner at fixed memory, fenced to a live `WanderwayRun` and recorded in
`Docs/ECOSYSTEM.md` §0. It is an exception *because it was asked for*, not a
precedent: the protocol still stands, and the next one needs its own sign-off.

### Universality — one HyperSea, one rule set

The fundamentals are universal. The HyperSea has rules and **everything in it
follows them** — game scenes, Menu_Main's lava-lamp/freestyle, tools and test
scenes alike. Do not create context-specific exemptions ("it's only the menu,"
"it's just cosmetic," "it's a perf special case"). Every carve-out creeps
confusion into best practices about when the rules apply, and carve-outs are
precisely how rejected cheats re-enter the codebase — both resolved examples
above came back wearing a special-circumstance costume.

When a context creates pressure (performance, pacing, visuals), solve it with
the universal systems that already exist — fauna have many jobs and cleanup is
one of them; spawners can pause; abilities can consume — never with a bespoke
mechanism that exists only in that context. Build systems once, use them
everywhere. If a universal system genuinely can't serve the context, that is a
fundamentals discussion (see the curation process above), not a license for a
local workaround.

### When in doubt

Name the fundamentals involved, describe how each candidate solution
interacts with them, and prefer the solution that leaves the fundamentals
intact and more expressive for future features.
