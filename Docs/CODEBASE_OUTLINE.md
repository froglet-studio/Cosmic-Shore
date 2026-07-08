# Cosmic Shore — Codebase Atlas

_An exhaustive, system-by-system outline of the entire Cosmic Shore game codebase (~1,387 first-party C# files under `Assets/_Scripts`). Generated 2026-07-08._

> Each top-level section below maps to a subsystem of the game. Component bullets name the concrete class/file and its role. For locked design rules and deep dives, see `CLAUDE.md` and `Docs/`.

## Contents

**1. Orientation**

- [MAP · Architecture Overview & System Map](#architecture-overview--system-map)

**2. Vessels**

- [S01 · Vessel Core, Status & Prisms](#vessel-core-status--prisms)
- [S02 · Vessel Actions System](#vessel-actions-system)

**3. World & Ecosystem**

- [S03 · Environment — Cells, Flora & Fauna, Crystals](#environment--cells-flora--fauna-crystals)
- [S04 · Environment — Spawning & MiniGame Objects](#environment--spawning--minigame-objects)

**4. Combat, Effects & Toys**

- [S05 · Impact Effects System](#impact-effects-system)
- [S11 · Projectiles & Freestyle Toys](#projectiles--freestyle-toys)

**5. Game Modes & Scoring**

- [S06 · Arcade — MiniGame Controllers & Modes](#arcade--minigame-controllers--modes)
- [S07 · Arcade — Scoring & Turn Monitors](#arcade--scoring--turn-monitors)

**6. Engine, Netcode & Client**

- [S08 · Prism Performance — Managers, Spatial Index, Assemblers, ECS](#prism-performance--managers-spatial-index-assemblers-ecs)
- [S09 · Multiplayer, Player & Party/Presence Netcode](#multiplayer-player--partypresence-netcode)
- [S10 · Input, Camera, Animation, FX & AI](#input-camera-animation-fx--ai)

**7. Application & Backend**

- [S12 · System — Bootstrap, DI, App State, Auth, Scene](#system--bootstrap-di-app-state-auth-scene)
- [S13 · System — Backend, Cloud, Progression & App Features](#system--backend-cloud-progression--app-features)
- [S14 · System — Dialogue Runtime, Rewind & Audio](#system--dialogue-runtime-rewind--audio)

**8. User Interface**

- [S15 · UI — Vessel HUDs, Screens, Modals & Interfaces](#ui--vessel-huds-screens-modals--interfaces)
- [S16 · UI — Menu Views, Widgets & Player Data](#ui--menu-views-widgets--player-data)
- [S17 · UI — Reusable Elements, Buttons, Hangar Cards & Animations](#ui--reusable-elements-buttons-hangar-cards--animations)
- [S18 · UI — Toasts, Notifications & Game Event Feed](#ui--toasts-notifications--game-event-feed)

**9. Data, Config & SOAP**

- [S19 · Data Model & ScriptableObject Definitions](#data-model--scriptableobject-definitions)
- [S20 · SOAP — Scriptable Object Architecture Pattern Types](#soap--scriptable-object-architecture-pattern-types)

**10. Utilities, Tooling & Tests**

- [S21 · Utility — Data Containers, Network, Effects, Pooling, Extensions](#utility--data-containers-network-effects-pooling-extensions)
- [S22 · Utility — Performance Benchmark, Tools, Recording & Misc](#utility--performance-benchmark-tools-recording--misc)
- [S23 · Editor Tools, Tests & Legacy Code](#editor-tools-tests--legacy-code)

---

## Architecture Overview & System Map

Cosmic Shore is a multigenre space game — **"the party game for pilots"** — developed by **Froglet Inc.** (a Delaware C-corp based in Grand Rapids, MI). Its organizing premise is that a single shared world, the **HyperSea**, is flown by many different **vessel classes**, each embodying the feel of a different game genre (racing, shooter, brawler, sandbox) so that players across demographics can meet in one place. The game is built on a small set of universal **fundamentals** whose interactions are meant to produce rich emergent behavior rather than a pile of bespoke mode-specific features, and one hard law runs through all of it: **nothing pops in or out** — everything grows, blooms, withers, or fades over a visible transition, and mass is conserved.

### What the Game Is

**Vessel classes** (`Assets/_Scripts/Data/Enums/VesselClassType.cs`) — 11 classes, each a genre lens:

| Vessel | ID | Genre / Role |
|---|---|---|
| **Manta** | 1 | Feature-complete playable vessel |
| **Dolphin** | 2 | Feature-complete playable vessel |
| **Rhino** | 3 | Feature-complete playable vessel |
| **Urchin** | 4 | Playable vessel (AI in progress) |
| **Grizzly** | 5 | Playable vessel (AI in progress) |
| **Squirrel** | 6 | Racing / drift — vaporwave arcade racer, tube-riding player-generated trails (F-Zero / Redout feel) |
| **Serpent** | 7 | Playable vessel with dedicated HUD |
| **Termite** | 8 | Planned |
| **Falcon** | 9 | Planned |
| **Shrike** | 10 | Planned |
| **Sparrow** | 11 | Shooter — arcade space combat with guns and missiles |

Meta sentinels: `Any (-1)`, `Random (0)`.

**Domains** (`Data/Enums/Domains.cs`) — team/affiliation identity attached to mass, vessels, and structures: `Jade (1)`, `Ruby (2)`, `Blue (3)`, `Gold (4)`. The playable set is `{Jade, Ruby, Gold}`; **Blue is the neutral "no team / not yet picked" sentinel** (never present in `GameDataSO.ActiveDomains`) and replaced the removed `Domains.None`/`Unassigned`. All cross-client team state flows from the server-authoritative `Player.NetDomain` NetworkVariable — clients never write domain directly.

### Tech Stack

| Concern | Technology |
|---|---|
| Engine / render | **Unity 6+** with **URP** 17.0.4 |
| Language / async | **C#** with **UniTask** (`Cysharp.UniTask`) |
| Networking | **Unity Netcode for GameObjects** 2.5.0 (server-authoritative), UGS Relay/Multiplayer sessions |
| DI | **Reflex** 14.1.0 (`AppManager` as root `IInstaller`) |
| Cross-system architecture | **SOAP** — Scriptable Object Architecture Pattern (Obvious.Soap 2.7.0) |
| Camera | **Cinemachine** 3.1.2 with per-vessel `CameraSettingsSO` |
| VFX / shaders | **VFX Graph** 17.0.4, Shader Graph, custom HLSL |
| Performance | **Unity Jobs + Burst**, Adaptive Performance, **DOTS Entities** 1.4.2 (incremental) |
| Input | **Unity Input System** 1.14.2 via strategy pattern (`IInputStrategy`) |
| Audio / haptics | **Wwise**; NiceVibrations |
| Animation | Timeline 1.8.9, DOTween |
| Backend | **Unity Gaming Services** (Auth, CloudSave, Leaderboards, Friends, Analytics, Multiplayer, Purchasing, Ads); PlayFab SDK (legacy, inert) |
| Testing | Unity Test Framework 1.6.0 (NUnit) |
| Target | Mobile-first, PC/console expansion |

### Architectural Layers & Cross-Cutting Patterns

The codebase is organized around a handful of load-bearing patterns applied uniformly:

- **SOAP as the primary architecture.** Cross-system communication and shared state go through `ScriptableVariable<T>` assets (shared data), `ScriptableEvent<T>` / `ScriptableEventNoParam` (decoupled one-to-many channels), and inspector-wired `EventListener<T>` components — **not** singletons, static events, or direct MonoBehaviour references. Custom SOAP types live under `_Scripts/ScriptableObjects/SOAP/`. Policy is **fail-loud**: no if-null guards on `ScriptableEvent` fields.
- **ScriptableObject config separation.** All tunable gameplay parameters live in SO config assets (`CameraSettingsSO`, `BootstrapConfigSO`, effect SOs, `ElementalBarsConfigSO`, …); MonoBehaviours reference them at runtime and hold no magic numbers. Specs live in one asset so they can't drift between prefabs.
- **Reflex DI root.** `AppManager` (`[DefaultExecutionOrder(-100)]`, `IInstaller`) registers all persistent managers and shared SO assets in `InstallBindings()`. Systems consume shared assets via `[Inject]` (populated after `Awake`, before `Start`); each scene carries a `ContainerScope`.
- **Single-writer / multi-reader facades.** Each backend domain has exactly one writer into a SOAP container that everyone else reads: `AuthenticationServiceFacade` → `AuthenticationDataVariable`, `ApplicationStateMachine` → `ApplicationStateDataVariable`, `FriendsServiceFacade` → `FriendsDataSO`, `HostConnectionService` → `HostConnectionDataSO`.
- **Main-thread affinity.** Every `await` of a UGS/Netcode `Task` uses **`.AsMainThread()`** (`UniTaskExtensions`), which marshals continuations back onto Unity's captured `SynchronizationContext` via `MainThreadDispatcher`. UniTask's own `SwitchToMainThread()` / `Yield(PlayerLoopTiming.Update)` are unreliable on this version and are banned as marshaling primitives. A canary in `SceneTransitionManager.SetFadeImmediate` fires if a continuation reaches it off-thread. See `Docs/THREADING.md`.
- **Server-authoritative Netcode.** All multiplayer flow (turns, rounds, winner detection, domain assignment, vessel spawning) is server-driven via ClientRpc/ServerRpc; the EAGER per-user Relay model means every player hosts their own party session from `Menu_Main` onward.

### High-Level Runtime Flow

```
Bootstrap (build 0) ── Authentication (1) ── Menu_Main (2, lava-lamp/freestyle) ── Game Scene
     AppManager          UGS sign-in           MainMenuController                MiniGameController
     DI + platform       host start            autopilot vessel + party         turn/round/game loop
```

The top-level phase is tracked by **`ApplicationStateMachine`** (single writer to a SOAP variable, table-validated transitions):

```
None → Bootstrapping → Authenticating → MainMenu → LoadingGame → InGame → GameOver
Special: Paused · Disconnected → MainMenu|Authenticating · ShuttingDown (terminal)
```

**Game launch pipeline** (four data layers into a scene-placed controller):

```
SO_ArcadeGame (static config: mode, scene, captains, min/max players+intensity, scoring)
   → ArcadeGameConfigSO (ephemeral UI state: chosen game + intensity + count + vessel)
   → GameDataSO (shared SOAP runtime state + all SOAP events; ConfigurePlayerCounts → AI backfill)
   → gameData.InvokeGameLaunch() → OnLaunchGame → SceneLoader.LaunchGame() (host-driven Netcode load)
   → MiniGameControllerBase subclass drives rounds → turns → countdown → gameplay → end
```

Menu_Main doubles as the **lava lamp / freestyle** experience (one system, two names): the autopilot background vessel *is* the playable gameplay vessel, so the universal rules — conserved mass, no trail caps — apply there with no "menu-only" exemption. The full scene inventory, game-mode table (37 modes), and controller hierarchy are in `Docs/SCENES.md`.

### The Design-Philosophy Fundamentals

The game is deliberately built from a **minimal, exhaustive set of fundamentals** — prefer composing these over adding bespoke systems; never "cheat" an outcome that should emerge from their interaction:

- **Domain** — team/affiliation identity attached to mass, vessels, and structures ("color" is the casual synonym).
- **Mass** — the produced/consumed quantity driving scoring, fueling, and cell control. **Conserved: no passive decay** — a prism is removed only by an active force (a vessel ability or fauna eating it); no lifespan, timer, or culler anywhere.
- **Cells** (`CellType`) — the regions of play that are the unit of territorial control (casually "biomes").
- **Elementals** — the single system governing *all* buffing/debuffing across vessels and environment (Charge / Mass / Space / Time).
- **Prisms / Prismscapes** — the geometric primitive of player-generated structure; trails are the 1-D case. Prisms *are* conserved mass.
- **Flora & Fauna** — populations that live on and respond to the fundamentals (fauna attraction to prisms, starvation-driven population bounds, wither-to-crystal on death).
- **Vessels** — the player/AI actors whose class-specific abilities compose with everything above.
- **Toys** — interactive world-space stations the vessel flies into in the Menu_Main toybox; no score, no end condition — they compose the other fundamentals (vessel/domain changers, painting, the Wanderway conveyor) rather than bypassing them.

### `Assets/_Scripts` Directory Map

~1,387 first-party C# files across 10 top-level directories (all under the `CosmicShore.*` namespace family). Gameplay code compiles into Unity's default assembly; only test assemblies carry `.asmdef` files.

| Directory | `.cs` | Role |
|---|---:|---|
| **Controller/** | 658 | All gameplay systems — `Vessel/`, `Environment/` (cells, flora/fauna, crystals, spawning), `Arcade/` (mode controllers + turn monitors), `ImpactEffects/`, `Projectiles/`, `Managers/` (prism perf), `Multiplayer/`, `Player/`, `Party/`, `AI/`, `Camera/`, `IO/`, `Prisms/`, `Toys/`, `FX/`, `ECS/`, `Assemblers/` |
| **UI/** | 227 | Vessel HUD controllers/views, screens, modals, menu navigation (`ScreenSwitcher`/`IScreen`), scoreboards, toast + notification systems, reusable elements |
| **Utility/** | 151 | `DataContainers/` (`GameDataSO`, `SceneNameListSO`), `PoolsAndBuffers/`, `ClassExtensions/` (`.AsMainThread()`), `Effects/`, `DataPersistence/`, threading (`MainThreadDispatcher`), `PerformanceBenchmark/` |
| **System/** | 135 | App-level systems — `Bootstrap/` (`AppManager`, `SceneTransitionManager`), auth/state facades, `Runtime/` (dialogue runtime), `Instrumentation/` (analytics), `RewindSystem/`, `Audio/`, `LoadOut/`, `Quest/`, `Xp/`, `Ads/`, `Squads/`, `UserJourney/` |
| **ScriptableObjects/** | 102 | SO definitions (`SO_Vessel`, `SO_Captain`, `SO_ArcadeGame`, `SO_Element`, …) and all custom SOAP types under `SOAP/` |
| **Data/** | 41 | Enums (`VesselClassType`, `Domains`, `GameModes`, `ApplicationState`, `MainMenuState`, `ResourceType`, …) and data structs |
| **Editor/** | 39 | Editor tooling — copy tools, shader inspectors, scene/setup utilities, validators |
| **Tests/** | 31 | Edit-mode NUnit tests (enums, data SOs, geometry, party data, microscene patterns, …) |
| **Game/** | 3 | Vestigial — non-code assets plus `PRISM_PERFORMANCE_AUDIT.md`; all C# has moved out |
| **DialogueSystem/**, **Integrations/**, **SSUScripts/** | 0 | Asset-only trees (dialogue SO/shader assets, PlayFab SDK integration); their runtime code lives under `System/Runtime/` and `System/Playfab/` |

The sections that follow drill into each of these systems in turn.

---

## Vessel Core, Status & Prisms

This is the runtime heart of every playable/AI actor in Cosmic Shore. A vessel prefab is a `VesselController` (`NetworkBehaviour`, implements `IVessel`) sitting on the same GameObject as a `VesselStatus` (`MonoBehaviour`, implements `IVesselStatus`) — the controller owns network identity and the initialization chain, while `VesselStatus` is the mutable state bag and lazy component registry (`GetOrAdd`) that every other subsystem reads through the `IVesselStatus` interface. Around this pair hang the movement transformers, the prism/trail spawning-and-conservation machinery (prisms **are** the game's conserved "mass" fundamental), the skimmer + prism-shield geometry, the elemental resource system that drives all buffs/debuffs, the silhouette/pip HUD visualizers, per-vessel telemetry, and networked owner→client kinematic replication. Movement is `transform.position`-driven (no rigidbodies) with owner-authoritative NetworkVariables; input flows through a SOAP-decoupled action handler.

### Core vessel identity, status & lifecycle
The `IVessel`/`IVesselStatus` pair splits network behavior (`VesselController`) from state + component access (`VesselStatus`). `VesselController.Initialize(player)` is the single wiring point that boots every subsystem, sets theme materials, and fires `OnInitialized`.

- **IVessel** — vessel-controller interface (extends `ITransform`): network identity (`IsNetworkOwner`/`IsNetworkClient`, `PlayerNetId`/`VesselNetId`/`OwnerClientNetId`), `OnInitialized`/`OnBeforeDestroyed` events, and the full control surface (`Initialize`, `PerformShipControllerActions`, `Teleport`, `SetResourceLevels`, material/trail setters, `ToggleAIPilot`, `SetPose`, `SetInitialSpeed`, `ChangePlayer`, slowed-transform registration). `Assets/_Scripts/Controller/Vessel/IVessel.cs`
- **IVesselStatus** — the mutable state/registry interface: booleans (`IsBoosting`, `IsDrifting`, `IsOverheating`, `IsSlowed`, `IsStationary`, `IsAttached`, `GunsActive`, `IsTranslationRestricted`…), `Speed`/`Course`/`blockRotation`, `Player`/`Domain`/`PlayerName` (live-read, never snapshotted), lazy accessors for every attached component (ResourceSystem, VesselTransformer, AIPilot, Skimmers, Silhouette, HUD controller, ActionHandler, ElementalStatsHandler), and default-implemented `InputController`/`InputStatus`/`IsLocalUser`. `IVesselStatus.cs`
- **VesselController** — `NetworkBehaviour`+`IVessel`; owns four owner-write NetworkVariables (`n_Speed`, `n_Course`, `n_BlockRotation`, `n_IsTranslationRestricted`) replicated every `Update` on the owner and mirrored into `VesselStatus` via `OnValueChanged` on clients; drives the giant `Initialize()` boot chain, `ChangePlayer` (AI/client/local re-wiring), `SetPose_ClientRpc`/`SetInitialSpeed_ClientRpc` (menu vessel-swap continuity), `DestroyVessel` (despawn vs Destroy), and `[ServerRpc]`/`[ClientRpc]` add/remove of self to `GameDataSO.SlowedShipTransforms`. Registers with `gameData.Vessels`, raises `InvokeVesselNetworkSpawned`. `VesselController.cs`
- **VesselStatus** — `MonoBehaviour`+`IVesselStatus`; `[RequireComponent]` manifest of the standard vessel stack; `[RequireInterface(IVessel)]` back-reference; lazy `GetOrAdd` accessors for all subsystems; registers/unregisters the vessel `transform` as a focus with `PrismColliderLodManager` (proximity collider-LOD) on enable/disable; `ResetForPlay()` zeroes combat/boost state and resets resources, transformer, prism spawner, animation. `VesselStatus.cs`
- **VesselSpawner** — single-player (non-networked) vessel instantiator; resolves `Random`/`Any` to a concrete `VesselClassType`, pulls the prefab from `VesselPrefabContainer`, `Instantiate` + Reflex `GameObjectInjector.InjectRecursive`. `VesselSpawner.cs`
- **ShipHelper** (`VesselHelper.cs`) — static helper: input/resource action-map initialization into dictionaries, start/stop action dispatch, `Teleport`, `ApplyShipMaterial` (MeshRenderer slot 1 / SkinnedMeshRenderer slot 0), and the central **`SetShipProperties(themeManagerData, vessel)`** that paints hull/silhouette/AOE/skimmer/trail per current domain and re-applies mesh material if already painted.
- **VesselCustomization** — collects `_shipGeometries` into `VesselStatus.ShipGeometries`, applies `ShipMaterial` on init, and `RefreshShipMaterial()` re-paints on domain-change-after-init. `VesselCustomization.cs`
- **VesselTrailCustomization** — rebuilds each child `TrailRenderer.colorGradient` from domain highlight/core colors while preserving prefab alpha keys; used by `VesselController.SetTrailColors`. `VesselTrailCustomization.cs`
- **VesselCameraCustomizer** — `ElementalShipComponent`+`ICameraConfigurator`; applies per-vessel `CameraSettingsSO` (dynamic/orthographic/follow-offset overrides) to the active `ICameraController`, raises `OnInitializePlayerCamera` (`ScriptableEventTransform`), and `RetargetAndApply` on player change. `VesselCameraCustomizer.cs`
- **VesselCollider** / **IVesselCollider** — deprecated (superseded by `R_ImpactCollider`) thin `[RequireInterface(IVessel)]` back-reference holder. `VesselCollider.cs`
- **ShipHUD** (`VesselHUD.cs`) — legacy/near-dead bridge (self-documented "TODO remove"); finds a child `MiniGameHUD` and raises `ScriptableEventShipHUDData onShipHUDInitialized`.
- **Pip** — on `Start` raises `ScriptableEventPipData` (active/mirrored) driven by `AutoPilotEnabled`, toggles an optional pip camera. `Pip.cs`
- **DomainColorPaletteSO** — `ScriptableObject` fallback color table (jade/ruby/gold/blue/danger) with `Get(Domains)`; blue = the neutral sentinel. `DomainColorPaletteSO.cs`

### Movement & transformers
`VesselTransformer` is the base flight model — per-frame rotate (pitch/yaw/roll from `IInputStatus`) then throttle-lerp + course + `transform.position +=`, plus analog drift blending, throttle/velocity modifier lists, and boost decay. Subclasses swap the control scheme.

- **VesselTransformer** — base: `RotateShip`/`MoveShip`, `ThrottleScaler`/`Pitch/Yaw/RollScaler`, `ElementalFloat ThrottleScalerMultiplier`, `BeginDrift`/`EndDrift` + analog-drift interpolation (`_frameTriggerSum`, `DriftDamping`), `ModifyThrottle`/`ModifyVelocity` decaying modifier lists (`ShipThrottleModifier`/`ShipVelocityModifier`) that set `IsSlowed` and flare the engine, `DecayBoost` raising `ScriptableEventBoostChanged`, `SetPose`/`SetInitialSpeed`/`SpinShip`/`FlatSpinShip`/`ApplyRotation`, `ToggleActive`, `ResetTransformer`. `VesselTransformer.cs`
- **SingleStickVesselTransformer** — one-stick scheme using `EasedLeftJoystickPosition`; recomputes course from `transform.forward`; sets `IsSingleStickControls`. `SingleStickVesselTransformer.cs`
- **GunVesselTransformer** — trail-attached "ride the trail" mode via `BlockscapeFollower`; `Slide`/`SlideActions` recharge ammo (`ResourceSystem`), `FinalBlockSlideEffects` grows/steals the attached prism; adjusts close-camera distance on attach. `ProjectileScale`/`BlockScale`/`growthAmount ElementalFloat`. `GunVesselTransformer.cs`
- **CommandVesselTransformer** — lerps toward `InputStatus.ThreeDPosition` (command-stick 3D cursor). `CommandVesselTransformer.cs`
- **GunTransformer** — arranges child gun barrels radially around a `gunFocus` from `RightNormalizedJoystickPosition` (aim spread). `GunTransformer.cs`
- **DriftJet** — orients a jet effect toward `Course` while drifting, or side-facing otherwise. `DriftJet.cs`
- **BlockscapeFollower** — surface-crawler that walks a vessel across a prism's box faces (edge-crossing normal recomputation), terrain-aware speed by friendly/hostile/destroyed prism domain; used by `GunVesselTransformer`. `BlockscapeFollower.cs`

### Prisms, trails & conserved mass
Prisms are the concrete unit of conserved mass. `VesselPrismController` runs the async spawn loop that lays trail prisms; `Prism` is the pooled lifecycle object registered in `PrismSpatialIndex`; `Trail` is the ordered list with look-ahead/projection used for trail-riding.

- **Prism** — pooled `MonoBehaviour` (requires `MaterialPropertyAnimator`, `PrismScaleAnimator`, `PrismTeamManager`, `PrismStateManager`); `PrismProperties`, growth/volume via scale animator (`Volume`/`CurrentVolume`/`TargetScale`/`Grow`), `Damage`/`Consume` (idempotent, shield-aware, `byCreature` flag) → `Explode`/`Implode` via `OnBlockImpactedEventChannel`, `SetupDestruction`, `Restore`, `Steal`/`ChangeTeam`, shield/danger/transparency state forwarding, LOD collider culling (`SetColliderCulledByLod`), and full `PrismSpatialIndex` registration lifecycle (`Register`/`MarkDestroyed`/`MarkRestored`/`Unregister`/`UpdatePosition`) plus cell density-grid domain forwarding; raises `ScriptableEventPrismStats` created/destroyed/restored channels; `IsEnvironmentOwned` distinguishes track/shape prisms from player trail. `Prism.cs`
- **VesselPrismController** — async (`UniTaskVoid`) spawn loop laying prisms into `Trail`/`Trail2` at speed-scaled wavelength via `PrismEventChannelWithReturnSO`; `BaseScale`×`XScaler/YScaler/ZScaler`, gap/offset, `EnableDangerMode`/`DisableDangerMode` (overheat danger prisms + material blend), `SetNormalizedXScale`/`SetDotProduct`, `StartSpawn`/`StopSpawn`/`ClearTrails`, events `OnBlockCreated`/`OnBlockSpawned` + static `OnDangerBlockCreated`; virtual `ApplyBoost*` hooks. `VesselPrismController.cs`
- **SparrowPrismController** — overrides `ApplyBoostScale`/`ApplyBoostGap`/`ApplyBoostSpawnDelay` so non-boosting Sparrow lays larger, wider-gapped, less-frequent prisms. `SparrowPrismController.cs`
- **Trail** — `[Serializable]` ordered `List<Prism>` + index dictionary; `Add`/`Clear`/`GetBlock`/`GetBlockIndex`, `LookAhead` and `Project` (distance projection with ping-pong `IndexSafetyCheck`), optional `isLoop`, holds a `TrailRenderer`. `Trail.cs`
- **TrailFollower** — attaches a vessel to a trail and moves it along via `Trail.Project`, terrain-aware block speed (`FriendlyTerrainSpeed`/`HostileTerrainSpeed`/`DestroyedTerrainSpeed`), calls `GunVesselTransformer.FinalBlockSlideEffects` on block change; defines the `TrailFollowerDirection` enum. `TrailFollower.cs`
- **TrailViewer** — makes trail blocks near the follower transparent and draws a `LineRenderer` guide down the trail. `TrailViewer.cs`
- **TrailScaleModulator** — lerps `VesselPrismController.X/Y/ZScaler` toward a `TrailScaleProfileSO` target (`Apply`/`Revert`). `TrailScaleModulator.cs`
- **TrailScaleProfileSO** — `ScriptableObject`: absolute-or-multiplier `scaleXYZ` + apply/revert lerp seconds. `TrailScaleProfileSO.cs`
- **ClearPrisms** — capsule trigger between camera and vessel that sets nearby prisms transparent (`_Alpha` via shared `MaterialPropertyBlock`, distance-to-line falloff) so they don't occlude the ship. `ClearPrisms.cs`
- **ScoutTrailPrismScaler** — probes `PrismSpatialIndex.IsAnyPrismWithin` at an adaptive radius (open vs confined space) and injects `SetNormalizedXScale` for scout-style open-space reward; config-driven. `TrailPassives/ScoutTrailPrismScaler.cs`
- **ScoutTrailPrismScalerConfig** — `ScriptableObject`: radius min/max/start, grow/shrink rates, `scaleByRadius` curve, smoothing, update interval, with `OnValidate` clamping. `TrailPassives/ScoutTrailPrismScalerConfig.cs`

### Skimmer & prism octahedron shields
The skimmer is the vessel's sphere sensor that vacuums crystals and boosts prism interactions; the octahedron shield family swaps a prism between its box state and a circumscribing (stellated) octahedron with per-face bloom/shatter and mass scaling.

- **Skimmer** — `ElementalShipComponent`; sphere-collider "skim" component, `VaccumAmount`/`vacuumCrystal`/`affectSelf`, `ElementalFloat Scale`, `ExecuteImpactOnShip` (raises `ScriptableEventString onSkimmerShipImpact`) / `ExecuteImpactOnPrism` (spawns `NudgeShard` boosters ahead on the trail via `Trail.LookAhead`), `ResizeForSeconds`, gaussian tube visualization. Near-field & far-field instances referenced by `VesselStatus`. `Skimmer.cs`
- **PrismOctahedronShield** — box↔circumscribing-octahedron transition via `OctahedronMeshGenerator`; per-face bloom `Engage`, shatter-overlay `Disengage` (lazily-created child mesh), BoxCollider↔convex MeshCollider swap, density-based mass scaling (4.5× box), branchless `IsPointInsideShield`; `[ContextMenu]` toggles. `PrismOctahedronShield.cs`
- **PrismOctahedronShieldTester** — standalone Input-System Space/auto-toggle harness (no full Prism lifecycle). `PrismOctahedronShieldTester.cs`
- **PrismStellatedOctahedronShield** — stella-octangula "super-shield" variant (24 outer faces, 13.5× box mass) via `StellatedOctahedronMeshGenerator`, 4-linear-form tetrahedral `IsPointInsideShield`. `PrismStellatedOctahedronShield.cs`
- **PrismStellatedOctahedronShieldTester** — Space/auto-toggle harness for the stellated shield. `PrismStellatedOctahedronShieldTester.cs`
- **ForcefieldCrackleController** — `[ExecuteAlways]`; 16-impact ring buffer fed by `SkimmerForcefieldCracklePrismEffectSO`, pushes impact positions/params + all visual params (arc density/sharpness, ring, ripple, fresnel) to `ForcefieldCrackle.hlsl` via `MaterialPropertyBlock` each frame; `AddImpact`/`ClearAllImpacts`. `ForcefieldCrackleController.cs`

### Resource & elemental system
`ResourceSystem` holds both the per-vessel fuel `Resource` list and the four elemental levels (Charge/Mass/Space/Time) that drive **all** buffs/debuffs; `ElementalFloat` fields auto-bind to element levels so tunables scale with elemental state.

- **ResourceSystem** — `ElementalShipComponent`; fuel `Resources` (gain coroutine, `ChangeResourceAmount`/`SetResourceAmount`/`Reset`, `OnResourceChanged`) plus elemental levels: base `ElementalLevels` dict, temporary decaying `ElementalEffect` modifiers, `ApplyElementalEffect(element, magnitude, duration)` (symmetric buff/debuff, permanent if duration≤0), `GetLevel`(-5..15)/`GetNormalizedLevel`/`AdjustLevel`/`SetElementLevel`/`IncrementLevel`, passive `RecoverBaseLevels` drift to the [0,10] resting band, inspector test-harness overrides, cached `ChargeLevel/MassLevel/SpaceLevel/TimeLevel`, and the `OnElementLevelChange` event (fires only on integer-level change). `ResourceSystem.cs`
- **Resource** — `[Serializable]` fuel unit: name, gain rate, clamped `CurrentAmount` (0..1) with `OnResourceChange`. `Resource.cs`
- **ElementalFloat** — `[Serializable]` float that binds `Name`+`Vessel` and auto-recomputes `Value = LerpUnclamped(Min, Max, level/10)` on `OnElementLevelChange`; the primitive for elementally-scaled tunables. `ElementalFloat.cs`
- **ElementalShipComponent** — base `MonoBehaviour` whose `BindElementalFloats(IVessel)` reflects over all `ElementalFloat` fields to name+bind them (base of `Skimmer`, `ResourceSystem`, `VesselCameraCustomizer`). `ElementalVesselComponent.cs`
- **R_ShipElementStatsHandler** (`R_VesselElementStatsHandler.cs`) — registry of `ElementStat` (statName→Element) bindings; `BindElementalFloat` dedupes and adds.
- **R_VesselActionHandler** — `NetworkBehaviour`; maps input/resource events → `ShipActionSO` lists (with per-device touch/gamepad overrides), executes via `ActionExecutorRegistry`, mirrors button press/release through `[ServerRpc]`/`[ClientRpc]` (with `PerformanceBenchmark.NetMarkers`), input-mute cooldowns (`MuteInput`), raises `ScriptableEventAbilityStats`/`ScriptableEventInputEventBlock`; defines `InputEventShipActionMapping`/`ResourceEventShipActionMapping` structs. (Action *SOs* live in the excluded subfolders.) `R_VesselActionHandler.cs`

### HUD visualizers — silhouette, pips, overheat, slow
Per-vessel HUD widgets that read live vessel state. The silhouette renders a conveyor of trail segments + energy jaws + elemental bars; pips render 4 elemental columns.

- **SilhouetteController** — driver: subscribes to `VesselPrismController.OnBlockCreated`/`OnBlockSpawned`, drift-altitude, resource changes, and (Manta) flower-explosion; forwards to `SilhouetteView`, seeds/updates `ElementalBarsView` from `ResourceSystem.OnElementLevelChange`; `[Inject] GameDataSO` for theme colors. `SilhouetteController.cs`
- **SilhouetteView** — the UI renderer: energy jaws, 2D silhouette rotation, pooled trail-segment conveyor (`BuildPoolIfNeeded`/`ApplyHeadAndConveyor`), domain/danger tinting, danger sprite swap, Manta flower overlay. `SilhouetteView.cs`
- **SilhouetteConfigSO** — `ScriptableObject` layout/flow/smoothing/multiplier + danger-visual config (single source of look/feel). `SilhouetteConfigSO.cs`
- **ElementPipsView** — builds 4 element columns × N pips (levels −5..+15) with zero-line marker; `SetLevel`/`Rebuild` from `ElementPipsConfigSO`. `ElementPipsView.cs`
- **ElementPipsConfigSO** — `ScriptableObject` pip layout/colors/per-element sprites with `OnChanged`/`OnValidate` live rebuild. `ElementPipsConfigSO.cs`
- **OverheatTrailVisualBridge** — bridges `OverheatingActionExecutor` events to `SilhouetteController.SetDangerVisual`. `OverheatTrailVisualBridge.cs`
- **SlowShipViewer** (`SlowVesselViewer.cs`) — listens to `ScriptableEventExplosionDebuffApplied[]` and draws a `LineRenderer` to the nearest slowed victim.

### Telemetry & stats
Per-vessel telemetry accumulates flight/combat stats over a turn and broadcasts them through `VesselStatEventSO` SOAP-style channels that the scoreboard provider caches; lifetime totals persist to Cloud Save.

- **VesselTelemetry** — abstract base tracking longest drift / max boost / prisms damaged; subscribes to `GameDataSO` turn events + `VesselDamagePrismEffectSO`; `RegisterStat`, extension hooks (`OnTurnStartedExtended`, etc.), `InjectGameData`. `VesselTelemetry.cs`
- **DefaultVesselTelemetry** — base-only telemetry for vessels without custom stats. `DefaultVesselTelemetry.cs`
- **SparrowVesselTelemetry** — adds prism-blocks-shot / skyburst-missiles / danger-blocks (subscribes to `FullAutoBlockShootActionExecutor`, `FireGunActionExecutor`, `VesselPrismController.OnDangerBlockCreated`). `SparrowVesselTelemetry.cs`
- **SquirrelVesselTelemetry** — adds clean-crystal streak / jousts-won / prisms-stolen (subscribes to `ScriptableEventCrystalStats`, joust event, `SkimmerStealPrismEffectSO`). `SquirrelVesselTelemetry.cs`
- **VesselTelemetryBootstrapper** — `[DefaultExecutionOrder(-100)]`; runtime-adds the correct telemetry subclass per `VesselClassType` and injects `gameData`, then self-destructs. `VesselTelemetryBootstrapper.cs`
- **VesselStatEventSO** — `ScriptableObject` single-stat channel with display metadata (label/icon/format), `Raise(value)`/`CurrentValue`/`FormattedValue`/`Reset`. `VesselStatEventSO.cs`
- **EventDrivenStatsProvider** — `ScoreboardStatsProvider` that subscribes to explicit `VesselStatEventSO` list (or discovers them from the local vessel's telemetry), caches latest values, exposes `GetStats()` for the end-game scoreboard. `EventDrivenStatsProvider.cs`
- **VesselStatsCloudData** — `[Serializable]` Cloud-Save container (`VesselStatsCloudData` → per-vessel `VesselLifetimeStats` with common stats + custom `Counters`). `VesselStatsCloudData.cs`

### Network client caches
Static registries of all spawned networked instances, keyed by NetworkObjectId, used by initializers/team lookups.

- **NetworkClientCache&lt;T&gt;** — generic base caching all active `T : NetworkBehaviour` via `NetcodeHooks` spawn/despawn; `ActiveInstances`, `OwnInstance`, `OnNewInstanceAdded`, `GetInstanceByClientId`. `NetworkClientCache.cs`
- **NetworkPlayerClientCache** — `NetworkClientCache<Player>`; adds `GetPlayerByTeam(Domains)`. `NetworkPlayerClientCache.cs`
- **NetworkVesselClientCache** — `NetworkClientCache<VesselController>`. `NetworkVesselClientCache.cs`

### Animation & audio (subfolders)
Procedural jet visuals plus per-vessel FMOD engine audio gated to the local player.

- **ParametricJetEffect** — drives a `ParticleSystem` + jet material (power/width/length, afterburner, mach diamonds, heat distortion) from public params. `Animation/ParametricJetEffect.cs`
- **ProceduralJetMesh** — generates and UV-scrolls a tapered ribbon jet mesh. `Animation/ProceduralJetMesh.cs`
- **ShipAudioController** — `[DisallowMultipleComponent]`; drives the FMOD "space ship engine main" loop from transform-measured velocity + pitch/yaw tilt + drift + elemental levels (`ResourceSystem`), with additional engine layers, listener-vs-ship attach routing, ownership-gated creation (`onlyAudibleToController`), and SFX-slider volume tie-in. `Audio/ShipAudioController.cs`
- **ShipStudioListenerGate** — `[RequireComponent(StudioListener)]`; enables the FMOD `StudioListener` only on the local user's vessel (deferred until `Player` resolves), keeping exactly one active listener. `Audio/ShipStudioListenerGate.cs`

### Boids
- **BoidController** — `BoidManager` subclass spawning queen/mound `Boid` drones for a vessel, raising `onMoundDroneSpawned`/`onQueenDroneSpawned` (`ScriptableEventInt`) and `TransferDrone` between pools. `BoidController.cs`

### Interactions & patterns
- **SOAP channels (raised):** `ScriptableEventBoostChanged` (transformer boost decay), `ScriptableEventPrismStats` (prism created/destroyed/restored), `PrismEventChannelWithReturnSO` (prism spawn + block-impact request/return), `ScriptableEventString` (skimmer-ship impact), `ScriptableEventPipData`/`ScriptableEventShipHUDData`/`ScriptableEventTransform` (HUD/camera init), `ScriptableEventAbilityStats`/`ScriptableEventInputEventBlock` (action handler), `ScriptableEventInt` (boids), and `VesselStatEventSO`/`VesselDamagePrismEffectSO`/`SkimmerStealPrismEffectSO`/`ScriptableEventCrystalStats` (telemetry). **Listened:** `GameDataSO.OnMiniGameTurnStarted/End/OnClientReady/OnResetForReplay`, `ResourceSystem.OnElementLevelChange/OnResourceChanged`, `VesselPrismController.OnBlockCreated/OnBlockSpawned/OnDangerBlockCreated`, overheat/explosion-debuff events.
- **NetworkVariables:** owner-write `n_Speed`/`n_Course`/`n_BlockRotation`/`n_IsTranslationRestricted` on `VesselController`, replicated per-frame and mirrored into `VesselStatus`; input mirrored via `R_VesselActionHandler` Server/Client RPCs; both wrap `PerformanceBenchmark.NetMarkers` for the hot serialize/RPC paths. Domain is read live from `Player.Domain`, never snapshotted (per CLAUDE.md).
- **DI:** Reflex `[Inject] GameDataSO`/`Container` on `VesselSpawner`, `SilhouetteController`, `VesselTelemetry`(via bootstrapper), `EventDrivenStatsProvider`; vessels are DI-injected recursively at spawn.
- **Spatial index / Burst:** `Prism` is the write path into `PrismSpatialIndex` (the canonical prism-mass index — AOE Burst queries, growth occupancy, cell density grids); `ScoutTrailPrismScaler` and `Skimmer` are read paths. `PrismColliderLodManager` uses `VesselStatus` as its collider-LOD focus.
- **Key data flows:** `VesselController.Initialize` boots the whole stack and calls `ShipHelper.SetShipProperties`; input → `R_VesselActionHandler` → `ShipActionSO` executors mutate `VesselStatus`/`ResourceSystem`; `VesselTransformer` reads `IInputStatus`+`VesselStatus` to move the transform and write the NetworkVariables; `VesselPrismController` lays conserved-mass `Prism`s into `Trail`; resource/elemental state fans out to `ElementalFloat` tunables, `SilhouetteController`/`ElementPipsView` HUD, `ShipAudioController` FMOD params, and telemetry.

---

## Vessel Actions System

The Vessel Actions System turns raw input events (and resource-threshold events) into gameplay behaviors — boost, drift, fire, grow trails, deploy crystals, cloak, zoom, etc. It exists in **two parallel generations** that both live under these two folders. The **legacy generation** (`VesselActions/`) is a family of `ShipAction` MonoBehaviour components physically attached to the vessel prefab. The **current "R_" generation** (`R_VesselActions/`) splits each ability into a stateless `ShipActionSO` config asset plus a scene `ShipActionExecutorBase` MonoBehaviour that owns runtime state, wired together at runtime by `R_VesselActionHandler`. Note the pervasive naming skew: files are named `*Vessel*` but the C# types are almost all named `*Ship*` (e.g. `VesselActionSO.cs` declares `ShipActionSO`). Both generations compose with the **Elementals** fundamental via `ElementalFloat` fields whose values track a vessel's per-element resource level, so any tunable number can be buffed/debuffed by elemental crystals with zero action-specific code.

### Core abstractions & dispatch (R_ generation)
The R_ system is a config→executor pattern: an input event maps to a list of `ShipActionSO` assets; each asset's `StartAction`/`StopAction` either mutates `IVesselStatus` directly or looks up its paired executor from the per-vessel `ActionExecutorRegistry` and calls into it. Executors hold all mutable/async state so the SO assets stay shareable and stateless (context is passed in on every call).

- **ShipActionSO** — abstract `ScriptableObject` action config; `Initialize(IVesselStatus)` + abstract `StartAction(ActionExecutorRegistry, IVesselStatus)` / `StopAction(...)`. `Controller/Vessel/R_VesselActions/Data Containers/VesselActionSO.cs`.
- **ShipActionExecutorBase** — abstract `MonoBehaviour` base for runtime executors; virtual `Initialize(IVesselStatus)`. `R_VesselActions/Executors/VesselActionExecutorBase.cs`.
- **ActionExecutorRegistry** — `MonoBehaviour` holding a serialized `List<ShipActionExecutorBase>` indexed by `Type`; `InitializeAll(status)` seeds every executor and stashes the live `VesselStatus`; `Get<T>()` returns the executor (falls back to `GetComponentInChildren`). Injects `AudioSystem` (Reflex) and exposes it to SOs. `R_VesselActions/Executors/ActionExecutorRegistry.cs`.
- **R_VesselActionHandler** — `NetworkBehaviour` driver (in `Controller/Vessel/`, the system's entry point). Holds `InputEvents→List<ShipActionSO>` maps plus Touch/Gamepad override maps and a `ResourceEvents` map; subscribes to `ScriptableEventInputEvents` (`OnButtonPressed`/`OnButtonReleased`); server-authoritatively replays actions on every peer via `SendButtonPressed_ServerRpc`→`_ClientRpc`; raises `ScriptableEventAbilityStats onAbilityExecuted` (duration) and `ScriptableEventInputEventBlock`; per-input **mute** window (`MuteInput`) with a UniTask end-notifier; skips when `AutoPilotEnabled`. Declares mapping structs **InputEventShipActionMapping** and **ResourceEventShipActionMapping**.
- **VesselHelper.InitializeShipControlActions / InitializeClassResourceActions / DestroyRuntimeActions** — static helpers the handler calls to flatten the inspector mapping lists into dictionaries and call `asset.Initialize(vesselStatus)` on each SO (assets used directly, not cloned). `Controller/Vessel/VesselHelper.cs` (referenced, just outside these folders).

### Core abstractions (legacy generation)
Legacy actions are `MonoBehaviour`s on the vessel; the old handler path calls `StartAction()`/`StopAction()` with no arguments (state lives on the component).

- **ShipAction** — abstract base extending `ElementalShipComponent`; caches `IVessel`/`IVesselStatus`/`ResourceSystem`, binds its `ElementalFloat` fields in `Initialize(IVessel)`, exposes `IsInitialized`, abstract `StartAction()`/`StopAction()`. `VesselActions/VesselAction.cs`.
- **ElementalShipComponent** — `MonoBehaviour` base (in `Controller/Vessel/ElementalVesselComponent.cs`); `BindElementalFloats(IVessel)` reflects over `ElementalFloat` fields and stamps each with a name + vessel so it subscribes to level changes. (Just outside the folders but is the legacy base.)
- **IScaleProvider** — `{ MinScale, CurrentScale }` interface implemented by grow executors/actions so camera-zoom followers can track a driven scale. `VesselActions/IScaleProvider.cs`.
- **ElementalFloatBinder** — static reflection helper that clones an object's `ElementalFloat` fields and binds name+ship (parallel to `ElementalShipComponent`, used for SO-side binding). `VesselActions/ElementalFloatBinder.cs`.
- **SyncActionWrapper** — `MonoBehaviour` glue: reads a lead action's `IScaleProvider` and pushes it into a `ZoomOutAction` via `AddProvider`. `VesselActions/SyncActionWrapper.cs`.

### R_ actions — movement & boost
- **BoostActionSO** — sets `IsBoosting=true`/`IsStationary=false` on start, clears on stop; plays `BoostActivate` SFX. `Data Containers/BoostActionSO.cs`.
- **ChargeBoostActionSO** + **ChargeBoostActionExecutor** — hold-to-charge / release-to-discharge boost. SO holds max multiplier, charge/discharge times, tick cadence, resource slot, recharge cooldown. Executor runs UniTask charge/discharge loops writing a normalized resource and `ChargedBoostCharge`/`BoostMultiplier`/`IsBoosting`; exposes charge/discharge C# events; resets on `OnMiniGameTurnEnd`. `Data Containers/ChargeBoostActionSO.cs`, `Executors/ChargeBoostActionExecutor.cs`.
- **ConsumeBoostActionSO** + **ConsumeBoostActionExecutor** — magazine of boost charges (1–4) with reload. SO: `ElementalFloat boostMultiplier`, duration, maxCharges, reload timings, optional resource cost. Executor stacks time-boxed boosts (multiplicative `4^stacks`), raises `ScriptableEventBoostChanged` (payload carries `VesselStatus`+`SourceDomain` so remote vessels don't hijack a HUD), auto-reloads when empty, exposes charge/reload/boost C# events. `Data Containers/ConsumeBoostActionSO.cs`, `Executors/ConsumeBoostActionExecutor.cs`.
- **ModifyVelocityActionSO** — one-shot forward `ModifyVelocity` impulse over a duration; plays `SpeedBurst` SFX. `Data Containers/ModifyVelocityActionSO.cs`.
- **ApplyRotationActionSO** — applies a fixed rotation about any of pitch/yaw/roll axes via `VesselTransformer.ApplyRotation`. `Data Containers/ApplyRotationActionSO.cs`.
- **YawsteryActionSO** + **YawsteryActionExecutor** — hold-to-yaw steering with ramp-in/out, speed coupling (`speedExp`), optional lock-to-angle, and mid-hold direction swap. Executor runs a UniTask intensity loop applying per-frame yaw through `VesselTransformer`; exposes start/end/intensity events. `Data Containers/YawsteryActionSO.cs`, `Executors/YawsteryActionExecutor.cs`.

### R_ actions — drift
- **DriftActionSO** — begins/ends a drift on `VesselTransformer` (`Mult`, damping, `isSharpDrifting`), sets `IsDrifting`, plays drift SFX; raises `OnDriftingStarted`/`OnDoubleDriftingStarted`/`OnDriftEnded` (`ScriptableEventNoParam`) only for the local owner (physics/SFX still replicate). `Data Containers/DriftActionSO.cs`.
- **DriftTrailActionSO** + **DriftTrailActionExecutor** — while held, a UniTask loop feeds the `Course·forward` dot product into `VesselPrismController.SetDotProduct` (drift-altitude trail banking) and fires `OnChangeDriftAltitude`; resets to 1 on end/turn-end. `Data Containers/DriftTrailActionSO.cs`, `Executors/DriftTrailActionExecutor.cs`.

### R_ actions — trail, prism & stationary/seed
- **GrowTrailActionSO** + **GrowTrailActionExecutor** — grows/shrinks trail prism scalers on weighted X/Y/Z/Gap axes between `min` and `ElementalFloat maxSize`; executor is an `IScaleProvider` driving `VesselPrismController.XScaler/YScaler/ZScaler/Gap` via a UniTask loop. `Data Containers/GrowTrailActionSO.cs`, `Executors/GrowTrailActionExecutor.cs`.
- **GrowSkimmerActionSO** + **GrowSkimmerActionExecutor** — grows the skimmer transform to `ElementalFloat maxSize` (coroutine), optional boost-while-growing, then ramps back; `IScaleProvider`, raises `OnScaleChanged`; SO has `ApplyMaxSizeDebuff` UniTask. `Data Containers/GrowSkimmerActionSO.cs`, `Executors/GrowSkimmerActionExecutor.cs`.
- **SeedWallActionSO** + **SeedAssemblerActionExecutor** — plant the latest trail prism as a wall/gyroid assembler seed. SO: resource cost math (`ComputeCost`), `AssemblerKind{Wall,Gyroid}`, `ShieldMode{None,Shield,SuperShield}`, bonding depth. Executor validates/consumes resource, shields the seed, attaches a `WallAssembler`, `BeginBonding()`; exposes seed/bond events and a stable API reused by cloak & stationary mode. `Data Containers/SeedWallActionSO.cs`, `Executors/SeedAssemblerActionExecutor.cs`.
- **ToggleTranslationModeActionSO** + **ToggleTranslationModeActionExecutor** — toggles `IsTranslationRestricted` (Serpent/Sparrow stationary modes); in Serpent mode also seeds a wall + stops trail spawn, restores on toggle-off; authority-gated for Netcode; raises `ScriptableEventBool stationaryModeChanged`. `Data Containers/ToggleTranslationModeActionSO.cs`, `Executors/ToggleTranslationModeActionExecutor.cs`.
- **OverheatingActionSO** + **OverheatingActionExecutor** — wraps another `ShipActionSO`; builds "heat" in a resource while the wrapped action runs, and on max heat forces overheat: enables danger-prism mode on `VesselPrismController` (`EnableDangerMode` with material/scale/lerp), holds for a duration, then decays heat. SO exposes heat rates, `ElementalFloat heatDecayRate`, danger material/scale. `Data Containers/OverheatingActionSO.cs`, `Executors/OverheatingActionExecutor.cs`.
- **FireTrailBlockActionSO** + **FireTrailBlockActionExecutor** — repeat-fires shielded/dangerous trail-block prisms forward from a muzzle at a fire rate; SO carries rate/scale/speed/lifetime + shielded/friendlyFire flags. (Both files live in `VesselActions/` despite being R_-pattern types.) `VesselActions/FireTrailBlockActionSO.cs`, `VesselActions/FireTrailBlockActionExecutor.cs`.
- **CloakSeedWallActionExecutor** — cloaks the ship (bakes a ghost mesh, swaps to a ghost material or hides renderer) and turns existing + newly-spawned trail prisms transparent for a cooldown while seeding a wall via `SeedAssemblerActionExecutor`; restores everything on end. Consumes **CloakSeedWallActionSO** (config lives in `UI/View/`, out of these folders). `Executors/CloakSeedWallActionExecutor.cs`.

### R_ actions — weapons & projectiles
- **FireGunActionSO** + **FireGunActionExecutor** — single-shot gun fire: consumes ammo resource, fires via a `Gun` from a world-space muzzle anchor with inherited velocity. SO: ammo index/cost, projectile scale/speed/energy, `ElementalFloat projectileTime`. Executor exposes `Ammo01`, `OnGunFired`/`OnAmmoChanged`, and a static `OnShotFired(playerName)`. `Data Containers/FireGunActionSO.cs`, `Executors/FireGunActionExecutor.cs`.
- **FullAutoActionSO** + **FullAutoActionExecutor** — hold-to-fire automatic gun loop across multiple muzzles at `firingRate`, consuming ammo per volley; `ElementalFloat speedValue`, `FiringPatterns`, energy. Static `OnVolleyFired`. `Data Containers/FullAutoActionSO.cs`, `Executors/FullAutoActionExecutor.cs`.
- **FullAutoBlockShootActionSO** + **FullAutoBlockShootActionExecutor** — auto-fires domain-colored **block prisms** from a `BlockProjectileFactory`, moves+anchors them a random distance, manages colliders/rigidbody and deferred visual reveal. SO: fire rate, block speed/scale, stop-distance range, rotation offset, `PrismType`, collider-disable flag. Static `OnBlockShot`. `Data Containers/FullAutoBlockShootActionSO.cs`, `Executors/FullAutoBlockShootActionExecutor.cs`.
- **SparrowModeSwitchingFireSO** — composite: while held, delegates to `stationaryFire` or `normalFire` sub-SO based on `IsTranslationRestricted`, and hot-swaps between them on `ScriptableEventBool stationaryModeChanged`. `Data Containers/SparrowModeSwitchingFireSO.cs`.

### R_ actions — crystal & elemental/shard
- **DeployTeamCrystalActionSO** + **DeployTeamCrystalActionExecutor** — press spawns a faded "ghost" `Crystal` in front of the vessel; release detaches + activates it as a team crystal in the vessel's `Domain`. SO: forward offset, fade value, ray mask, cooldown. `Data Containers/DeployTeamCrystalActionSO.cs`, `Executors/DeployTeamCrystalActionExecutor.cs`.
- **ShardToggleActionSO** + **ShardToggleActionExecutor** — toggle: point cell "shards" (a `SnowChanger` field) at the densest opposing-mass region (`Cell.GetExplosionTarget(domain)` via `CellRuntimeDataSO`) through a `ShardFieldBus`, then restore. `Data Containers/ShardToggleActionSO.cs`, `Executors/ShardToggleActionExecutor.cs`.

### R_ actions — camera & skimmer scaling helpers
- **ZoomOutActionSO** + **ZoomOutActionExecutor** — while held, drives camera distance to track a grow provider's scale (`Trail` or `Skimmer` via `IScaleProvider`), expanding/retracting with far-clip management and adaptive-zoom suspend; local-camera-target gated. `Data Containers/ZoomOutActionSO.cs`, `Executors/ZoomOutActionExecutor.cs` (`[DefaultExecutionOrder(-1000)]`).
- **CameraZoomFollowScaleProvider** — standalone `MonoBehaviour` (`[DefaultExecutionOrder(-900)]`) that continuously maps an `IScaleProvider`'s scale ratio to camera distance in `LateUpdate` (Fixed/FollowScale modes). `Executors/CameraZoomFollowScaleProvider.cs`.
- **ShieldSkimmerScaleConfigSO** + **ShieldSkimmerScaleDriver** — data + driver that scale the skimmer transform from a normalized "shield" resource with prism-growth ticks, decay, and a crystal-triggered max-size hold; config exposes speeds/caps and an `ApplyMaxSizeDebuff` UniTask. `Executors/ShieldSkimmerScaleConfigSO.cs`, `Executors/ShieldSkimmerScaleDriver.cs`.

### R_ actions — toggles
- **ToggleAlignActionSO** — sets `AlignmentEnabled=false` on start, `true` on stop; optional initial-state seed in `Initialize`. `VesselActions/ToggleAlignActionSO.cs` (R_-pattern SO living in the legacy folder).

### Misplaced HUD controllers (in the SO folder, not actions)
- **DolphinVesselHUDController** — `VesselHUDController` subclass mapping an energy resource to a Dolphin charge bar; sits in `Data Containers/` but is HUD code. `Data Containers/DolphinVesselHUDController.cs`.
- **SquirrelVesselHUDController** — `VesselHUDController` subclass wiring boost/drift/joust/crystal SOAP events to `SquirrelVesselHUDView` (domain-colored boost bar, drift/danger flashes); local-owner gated. `Data Containers/SquirrelVesselHUDController.cs`.

### Legacy `ShipAction` components — movement & rotation
- **ApplyRotationAction** — fixed pitch/yaw/roll rotation via `VesselTransformer` (only applies pitch as written). `VesselActions/ApplyRotationAction.cs`.
- **ChangeRotationSpeedAction** — multiplies/divides `Pitch/Yaw/RollScaler` while held. `VesselActions/ChangeRotationSpeedAction.cs`.
- **SpinAroundAction** — triggers a 180° `FlatSpinShip`. `VesselActions/SpinAroundAction.cs`.
- **DriftAction** — scales rotation scalers ×1.5 and sets `IsDrifting`; plays drift SFX (injects `AudioSystem`). `VesselActions/DriftAction.cs`.

### Legacy `ShipAction` components — boost
- **BoostAction** — sets `IsBoosting`/`IsStationary`. `VesselActions/BoostAction.cs`.
- **ChargeBoostAction** — coroutine charge/discharge boost mirroring `ChargeBoostActionSO`; writes `BoostMultiplier`/`ChargedBoostCharge`, exposes charge/discharge C# events. `VesselActions/ChargeBoostAction.cs`.
- **ConsumeBoostAction** — magazine boost with additive stacking + reload coroutines; `ElementalFloat boostMultiplier`. `VesselActions/ConsumeBoostAction.cs`.

### Legacy `ShipAction` components — grow & zoom
- **GrowActionBase** — shared grow/shrink of a `target` transform between `MinSize` and `ElementalFloat maxSize` in `LateUpdate`; implements `IScaleProvider`. `VesselActions/GrowActionBase.cs`.
- **GrowSkimmerAction** — empty subclass of `GrowActionBase`. `VesselActions/GrowSkimmerAction.cs`.
- **GrowTrailAction** — `GrowActionBase` variant driving `VesselPrismController` X/Y/Z/Gap scalers by weight, auto-selecting a scaling dimension. `VesselActions/GrowTrailAction.cs`.
- **ZoomOutAction** — `LateUpdate` camera-distance follower off an injected `IScaleProvider` (`AddProvider`), with directional zoom multipliers and smoothing. `VesselActions/ZoomOutAction.cs`.
- **ZoomGrowRateDistributeAction** — vestigial/stubbed shared-rate distributor (`ElementalFloat sharedRate`, logic commented out). `VesselActions/ZoomGrowRateDistributeAction.cs`.

### Legacy `ShipAction` components — trail, prism, stationary & seed
- **DisableTrailAction** — `StopSpawn` on start, `StartSpawn` on stop. `VesselActions/DisableTrailAction.cs`.
- **DriftTrailAction** — coroutine feeding drift-altitude dot product into `VesselPrismController.SetDotProduct`; `OnChangeDriftAltitude` event. `VesselActions/DriftTrailAction.cs`.
- **SeedWallAction** — consumes a resource, super-shields the latest trail prism and attaches a `GyroidAssembler` that starts bonding. `VesselActions/SeedWallAction.cs`.
- **SeedAssemblerConfigurator** — plain `MonoBehaviour` seed/bond helper (add assembler prefab to latest trail block, `StartSeed`/`BeginBonding`/`StopSeed[Completely]`) reused by cloak/stationary actions. `VesselActions/SeedAssemblerConfigurator.cs`.
- **SeedAssemblerMono** — near-empty `ShipAction` holding an `Assembler` reference (stub). `VesselActions/SeedAssemblerMono.cs`.
- **ToggleStationaryModeAction** — toggles `IsStationary`; Serpent mode seeds a wall + stops trail spawn, else just toggles spawn. `VesselActions/ToggleStationaryModeAction.cs`.
- **ToggleTurretModeAction** — subclass adding ×2 resource gain rate while stationary. `VesselActions/ToggleTurretModeAction.cs`.
- **OverheatingAction** — legacy heat-build/overheat/decay wrapping another `ShipAction` (coroutine version of the R_ executor). `VesselActions/OverheatingAction.cs`.
- **CloakSeedWallAction** — legacy cloak+seed with ghost-mesh spawning, material-alpha fades, protected-block bookkeeping, cooldown. `VesselActions/CloakSeedWallAction.cs`.
- **DetachAction** — clears `IsAttached`/`AttachedPrism`. `VesselActions/DetachAction.cs`.
- **ToggleAlignAction** — sets `AlignmentEnabled` false/true. `VesselActions/ToggleAlignAction.cs`.
- **ToggleGyroAction** — routes to `InputController.OnToggleGyro`. `VesselActions/ToggleGyroAction.cs`.
- **StopGunsAction** — sets `GunsActive=false`. `VesselActions/StopGunsAction.cs`.
- **GhostAction** — temporarily disables ship-geometry colliders for `maxDuration` (intangibility). `VesselActions/GhostAction.cs`.
- **AssembledArchBurstAction** — one-shot procedural lattice/arch generator (Bézier surface, tri/hex edges, layered rods); marked for deletion. `VesselActions/AssembledArchBurstAction.cs`.

### Legacy `ShipAction` components — weapons & projectiles (mostly WIP)
- **FireGunAction** — single-shot gun fire consuming ammo; `ElementalFloat ProjectileTime`; `OnGunFired` + static `OnShotFired`. `VesselActions/FireGunAction.cs`.
- **ChargedFireGunAction** — WIP charge-up gun that gains energy then fires a scaled projectile / detonates live ones. `VesselActions/ChargedFireGunAction.cs`.
- **FireBarrageAction** — WIP multi-gun barrage (clones a gun template onto children); firing commented out. `VesselActions/FireBarrageAction.cs`.
- **FullAutoAction** — hold-to-fire loop across gun transforms; `ElementalFloat speed`; `OnFullAutoStarted/Stopped` + static `OnVolleyFired`. `VesselActions/FullAutoAction.cs`.
- **DetonateProjectilesAction** — calls `gun.DetonateProjectile()`. `VesselActions/DetonateProjectilesAction.cs`.
- **EnergizeAction** — temporarily raises energy/speed/projectile-time on a list of `FireGunAction`s, restoring on stop. `VesselActions/EnergizeAction.cs`.
- **ToggleProjectileActionWrapper** — flips a wrapped `FullAutoAction`'s energy/projectile-time between two presets. `VesselActions/ToggleProjectileActionWrapper.cs`.

### Legacy `ShipAction` components — drones
- **DeployDronesAction** — `boidController.TransferDrone(false)`. `VesselActions/DeployDronesAction.cs`.
- **RecallDronesAction** — `boidController.TransferDrone(true)`. `VesselActions/RecallDronesAction.cs`.
- **MoundDronesShipAction** — spawns N drones toward a cell crystal (via `CellRuntimeDataSO`). File `VesselActions/MoundDronesVesselAction.cs`.
- **QueenDronesShipAction** — spawns N "queen" drones from the ship. File `VesselActions/QueenDronesVesselAction.cs`.

### Legacy `ShipAction` components — crystal & shard
- **DeployTeamCrystalAction** — cooldown-gated ghost `Crystal` that follows the ship (coroutine) then activates in the vessel's `Domain`. `VesselActions/DeployTeamCrystalAction.cs`.
- **ShardToggleAction** — toggles cell shards toward a `Domain`'s densest region via `ShardFieldBus`/`CellRuntimeDataSO`. `VesselActions/ShardToggleAction.cs`.
- **ShardFieldBus** — `ScriptableObject` event bus registering `SnowChanger` listeners with `BroadcastPointAtPosition`/`BroadcastRestoreToCrystal` (broadcast bodies currently commented out). `VesselActions/ShardFieldBus.cs`.

### Interactions & patterns
- **Input pipeline & networking.** `R_VesselActionHandler` (a `NetworkBehaviour`) is the sole bridge from input to actions: it listens on `ScriptableEventInputEvents` SOAP channels, and on the owner it round-trips through `ServerRpc→ClientRpc` so every peer replays the same `StartAction`/`StopAction`, making abilities deterministic across clients. Device-specific override maps (Touch/Gamepad) select action lists off `IVesselStatus.InputStatus.ActiveInputDevice`. It emits `ScriptableEventAbilityStats` (ability durations, consumed by telemetry/HUD) and `ScriptableEventInputEventBlock` (mute windows).
- **Config→executor contract.** SOs are shared, effectively stateless assets; runtime/async state (UniTask loops, coroutines, cooldowns, magazines) lives in the scene `ShipActionExecutorBase` components found via the type-keyed `ActionExecutorRegistry`. Executors resolve `AudioSystem` through Reflex `[Inject]` and universally self-reset on the `OnMiniGameTurnEnd` (`ScriptableEventNoParam`) to avoid leaking boosts/heat/fire across turns.
- **Elementals composition.** Any tunable magnitude typed as `ElementalFloat` (boost multipliers, projectile time, max grow/skimmer size, heat decay) auto-binds to the vessel's `ResourceSystem.OnElementLevelChange` (via `ElementalShipComponent.BindElementalFloats` for legacy components and `ElementalFloatBinder`/`ShipActionSO` for SOs) and `LerpUnclamped`s between Min/Max — so elemental crystals buff/debuff abilities with no per-action code, including comeback-system levels outside 0–10.
- **State surface.** Actions mostly communicate by writing shared `IVesselStatus` flags (`IsBoosting`, `BoostMultiplier`, `IsDrifting`, `IsStationary`, `IsTranslationRestricted`, `IsOverheating`, `AlignmentEnabled`, `GunsActive`, `IsAttached`) and by driving `VesselTransformer` (rotation/velocity/drift), `VesselPrismController` (trail scalers, spawn on/off, danger mode, dot-product banking), `ResourceSystem` (ammo/heat/shield/charge pools), and `Gun`/`BlockProjectileFactory`.
- **Cross-system SOAP outputs.** Boost UI is driven by `ScriptableEventBoostChanged` (payload carries originating `VesselStatus`/`SourceDomain` to prevent remote-vessel HUD hijack); drift UI by `ScriptableEventNoParam` drift channels; `ScriptableEventBool stationaryModeChanged` couples translation-mode toggles across the mode-switching fire, overheat, and stationary executors. Static C# `OnShotFired`/`OnVolleyFired`/`OnBlockShot(playerName)` feed scoring/telemetry.
- **Scale-provider chain.** `GrowTrailActionExecutor`, `GrowSkimmerActionExecutor`, `GrowActionBase`, and `ShieldSkimmerScaleDriver` publish `IScaleProvider`; `ZoomOutAction(SO)`/`CameraZoomFollowScaleProvider`/`SyncActionWrapper` consume it to bind camera distance to trail/skimmer scale through `CameraManager`/`ICameraController`.
- **Ecology alignment.** Trail/prism actions honor the conserved-mass law — they seed, shield, grow, or reroute prisms (`SeedAssemblerActionExecutor`, danger-mode overheat, block-shoot) and deploy collectible `Crystal`s in the vessel's `Domain`, never adding decay timers to prisms themselves.
- **Migration caveat.** The two generations overlap (e.g. `ChargeBoost`, `ConsumeBoost`, `Overheating`, `FireGun`, `FullAuto`, `Drift`, `GrowTrail/Skimmer`, `ZoomOut`, `DeployTeamCrystal`, `ShardToggle`, `SeedWall` each exist twice); the R_ config→executor path is the current one. File/type naming is inconsistent (`*Vessel*.cs` files declaring `*Ship*` types), and a few files are misfiled (`FireTrailBlockActionSO`/`ToggleAlignActionSO` are R_-pattern SOs under `VesselActions/`; the two `*VesselHUDController` files are HUD code under the SO `Data Containers/` folder). Several legacy weapon/drone actions are explicitly WIP or stubbed (`ChargedFireGunAction`, `FireBarrageAction`, `SeedAssemblerMono`, `ZoomGrowRateDistributeAction`, `AssembledArchBurstAction`).

---

## Environment — Cells, Flora & Fauna, Crystals

This is the living "HyperSea" ecosystem layer: **Cells** are spherical regions of territorial play that own a membrane/nucleus/cytoplasm and host a self-sustaining food web of **flora** (plant lifeforms that plant + grow prisms) and **fauna** (boid-like animals that graze opposing-domain mass, reproduce, starve, and wither-to-crystal on death). Everything keys off per-domain **volume** ("volume is the spine") accumulated in the Cell, which drives a three-rung phase/aggression ladder (Calm → Restless → Frenzy). Every lifeform conserves its mass into a collectible elemental **crystal** on death. The area also contains the crystal spawn/replication managers, the drifting cytoplasm "snow", scalar flow/warp field definitions, and ambient skybox/star effects. It composes with the rest of the game through SOAP events on the shared `CellRuntimeDataSO`/`GameDataSO`, Netcode replication, Reflex DI, and the Burst-backed `PrismSpatialIndex`/density grids. All types are in namespace `CosmicShore.Gameplay`.

### Cell core & phase ladder
The `Cell` MonoBehaviour is the unit of territorial control: it instantiates membrane/nucleus/cytoplasm visuals from a `CellConfigDataSO`, tracks every prism's per-domain volume + count, computes its phase locally each half-second, and runs one of two life spawners. A static registry lets pooled prisms find their containing cell.

- **Cell** — the central region controller (`Cell.cs`, 1089 lines): owns membrane/nucleus visuals, per-domain `BlockCountDensityGrid`/`BlockVolumeDensityGrid`, `massTracked`/`trackedBlocks` bookkeeping, `LiveVolume`/`DominantDomain`/`OpposingVolume` (the volume spine), phase compute via `CellPhaseRules.Compute`, derived gates (`FloraGrowingEnabled`/`FloraPlantingEnabled`/`FaunaSpawningEnabled`/`AggressionLevel`/`ControllingDomain`), `AddBlock`/`RemoveBlock`/`NotifyBlockDomainChanged`, live-fauna registry (`RegisterLiveFauna`/`GetLiveHerbivoreCount`), density-grid goal queries (`GetExplosionTarget`/`GetDensestRegionAnyDomain`), nucleus-boundary reshaping for modes (`SetNucleusWorldRadius`/`SetNucleusMesh`), a `SenseRadius` override for large arenas, fauna-spawn-cycle telemetry, and the static `ActiveCells` registry (`FindCellContaining`/`FindNearestActiveCell`). Injects `GameDataSO`; reads/writes `CellRuntimeDataSO`.
- **CellNetworkSync** — optional server-authoritative replication of `Cell.Phase` + `DominantDomain` + live block count via three `NetworkVariable`s (`NetworkBehaviour`, `[RequireComponent(typeof(Cell))]`); server mirrors, clients overwrite local compute via `Cell.ApplyAuthoritativePhaseAndDomain` (`CellNetworkSync.cs`).

### Cell life spawners
Two interchangeable spawners (chosen by the Cell's `CellTypeChoiceOptions`) run coroutine loops off a `SpawnProfileSO` to seed flora and fauna, gated on the Cell's phase/volume and the "no domain asymmetry" invariant (flora seed all three domains, fauna spawn in the controlling color).

- **ICellLifeSpawner** — spawner contract: `Start(host, config, runtime, gameData)` / `Stop(host)` (`ICellLifeSpawner.cs`).
- **CellLifeSpawnerBase** — abstract base (`CellLifeSpawnerBase.cs`): coroutine tracking, `Validate`, weighted/probabilistic roll helpers (`PickWeighted`/`AllowSpawn`), domain picking (`PickRandomDomain` — playable domains only, never Blue), and the canonical spawn methods `SpawnFlora`/`SpawnFauna`/`SpawnFaunaWithDomain`/`RegisterSpawned` (static so the freestyle conveyor reuses them), plus `RunSpawnLoop`/`RunThresholdLoop`.
- **RandomLifeSpawner** — prey-linked seeder (`RandomLifeSpawner.cs`): per-flora and per-fauna type loops; fauna loop is a *seeder not driver* — tops species up to `PopulationSize` when a prey/food floor is met (`FaunaReproductionRules.SeedSpawnCount`/`PreyAvailable`), spawning the swarm on the densest mass concentration; reproduction (`Fauna.TryReproduce`) drives population above the floor.
- **IntensityWiseLifeSpawner** — intensity/aggression-scaled spawner (`IntensityWiseLifeSpawner.cs`): initial-batch + continuous flora/fauna loops, fauna interval scaled by `CellAggressionLevel` (`FaunaSpawnIntervalByAggression`), lineage-binds spawned fauna, loud warnings on empty/misconfigured `SupportedFaunas`.

### Cell modifiers
Serialized per-config hooks that mutate a Cell at init (`Cell.ApplyModifiers` calls each `Apply`).

- **CellModifier** — abstract MonoBehaviour base with `Apply(Cell)` (`CellModifiers/CellModifier.cs`).
- **ExtraOmniCrystals** — modifier meant to spawn N additional omni-crystals (`additionalCrystals`); body is currently stubbed/commented out (`CellModifiers/ExtraOmniCrystals.cs`).

### Lifeform infrastructure (health / spindle / mass conservation)
Shared skeleton for all lifeforms: health-prism and spindle trackers (extracted for SRP), the withering spindle visual, the health-prism mass unit, and the invariant enforcer that guarantees a death powerup.

- **ILifeFormEntity** — common contract (`Domain`, `GetGameObject()`, `Initialize(Cell)`), extends `ITeamAssignable` (`FloraAndFauna/ILifeFormEntity.cs`).
- **LifeForm** — abstract base for health/spindle lifeforms (primarily Flora) (`FloraAndFauna/LifeForm.cs`): composition of `HealthBlockTracker` + `SpindleTracker`, maturity/lethality tracking, shield-regen, `Die`/`DieCoroutine`, turn-end drain of live blocks to the Cell, `SetTeam`; raises `onLifeFormCreated`/`onLifeFormDestroyed` (`ScriptableEventInt`) and the static `OnLifeFormDeath`. Injects `AudioSystem`/`GameDataSO`.
- **HealthBlockTracker** — plain-C# tracker of a lifeform's `HealthPrism`s (`FloraAndFauna/HealthBlockTracker.cs`): `ChangeTeam`-before-`Cell.AddBlock` ordering (prevents phantom-count desync), forwards live/dead refs to `Cell.Add/RemoveBlock`, maturity + `DamageAll` + `CleanupDeadRefs`.
- **SpindleTracker** — plain-C# tracker of `Spindle`s for a lifeform: add/remove, `Instantiate`, `IsEmpty`, `ForceWitherAll` (`FloraAndFauna/SpindleTracker.cs`).
- **Spindle** — the withering visual node/branch (`FloraAndFauna/Spindle.cs`): holds child health-blocks + child spindles, condense/evaporate shader animations (`_DeathAnimation`), `ForceWither` (recursive extremity-first collapse), `CheckForLife`, permanent-wither latch, scene-unload-safe teardown.
- **HealthPrism** — the prism that *is* a lifeform's health/mass (`HealthPrism.cs`, extends `Prism`): binds to its `LifeForm` + parent `Spindle`, overrides `Explode`/`Implode` to deregister from spindle and notify the lifeform, flora-specific destruction SFX, `Reparent`.
- **LifeFormCrystal** — static invariant enforcer (`FloraAndFauna/LifeFormCrystal.cs`): `EnsureElementalCrystal` guarantees every lifeform carries exactly one Charge/Mass/Space/Time crystal (authored → reused, non-elemental → element randomized, missing → provisioned from `ElementalCrystalSetSO`), logging loudly on the misconfig fallback.

### Flora
Plant lifeforms that plant themselves dispersed across the cell (fraction of membrane radius) and grow on a periodic cycle, gated by `Cell.FloraGrowingEnabled` (steady until Frenzy, then frozen with hysteresis — no growth cap, no decay).

- **Flora** — abstract plant base over `LifeForm` (`FloraAndFauna/Flora.cs`): grow/plant cycle (`GrowCoroutine`, `growPeriod`/`PlantPeriod`/`stunDuration`), `ResolvePlantRadius` (membrane-fraction dispersal), leaf-size on health-blocks; abstract `Grow()`/`Plant()`.
- **BranchingFlora** — recursive branching tree flora (`FloraAndFauna/BranchingFlora.cs`): trunk/branch/leaf spawning with growth chance + branch angles + depth, live-prism budget (`maxTotalSpawnedObjects`) so grazed flora regrow, reawakening reseed, optional crystaltropic goal, no-leaf failsafe auto-die, secondary-spawn planting.
- **AssembledFlora** — flora whose growth pattern is driven by an `Assembler` (`FloraAndFauna/AssembledFlora.cs`, also declares `GrowthInfo` + `AssemblerFactory`): gyroid/Schwarz-P assembler programming, live-prism budget + branch reawakening/reseed (`ReseedBranches`), dangerous-prism marking, crystal growth on grow. Collaborates with `Assembler`/`GyroidAssembler`/`SchwarzPAssembler` (Assemblers, out of scope).

### Fauna
Animal lifeforms and their managers. `Fauna` is the sealed-death base: it owns diet (herbivore/predator), predation immunity, starvation clock, reproduction, lineage registration, aggression-scaled goal resolution, and the mass-conserving `Die` chokepoint. Concrete creatures move each frame and consume prisms via the `PrismSpatialIndex`; managers spawn/steer sub-creatures.

- **Fauna** — abstract base for animal lifeforms + managers (`FloraAndFauna/Fauna.cs`): `FaunaDiet` selector, `IsPredationImmune`/`IsStarving`/`NotifyFed`, reproduction (`AssignLineage`/`TryReproduce`/`SpawnOffspring` via `FaunaConfigurationSO` + `FaunaReproductionRules`), aggression-tiered `ResolveGoal` (crystal → opposing centroid → any centroid) with per-instance orbit offset, sealed `Die` (drops crystal, then virtual `OnDeath`), `Predated`/`IsAlivePrey` idempotent predation, body-prism mover contract (`CacheBodyPrisms`/`NotifyBodyPrismsMoved`), shared static `OverlapScratch`/`PrismScratch`/`NonPrismOverlapMask`. Injects `GameDataSO`.
- **LightFauna** — lightweight boid-like grazer/predator (brittlestar/shark) (`FloraAndFauna/LightFauna.cs`): separation/goal steering, phase-driven goal swap, predator prey-seeking (`TryFindNearestPreyFauna`), herbivore prism grazing via `PrismSpatialIndex.QuerySphere`, aggression cadence/consume-radius/speed multipliers, danger-immunity in Frenzy, starvation death, and extremity-first `WitherCoroutine` (continuity rule). Uses `LightFaunaDataSO`.
- **LightFaunaManager** — spawns/maintains a `LightFauna` group (`FloraAndFauna/LightFaunaManager.cs`, extends `Fauna`): phase-gated `MaybeSpawnGroup`, prism-load-scaled `ComputeBatchSize`, formation layout, replenish-on-death; subscribes to `CellRuntimeDataSO.OnPhaseChanged`. Uses `LightFaunaManagerDataSO`.
- **LightFaunaDataSO** — `ScriptableObject` config for `LightFauna` detection/behavior/speed/wither (`FloraAndFauna/LightFaunaDataSO.cs`).
- **LightFaunaManagerDataSO** — `ScriptableObject` config for spawn count/radius, per-100-prism population scaling, formation spread (`FloraAndFauna/LightFaunaManagerDataSO.cs`).
- **Boid** — flocking creature (tadpole forager / mound drone) (`FloraAndFauna/Boid.cs`, extends `Fauna`; declares `BoidCollisionEffects` enum): separation/alignment/cohesion + block attraction via spatial index, forager-vs-drone diet (any-domain graze + starve vs opposing-domain damage), hunt-speed dash toward densest region, Attach/Explode collision effects, mound-building coroutine (`AddToMoundCoroutine`/`NewBlock`), fade-out death.
- **BoidManager** — spawns a `Boid` swarm and registers their trail (`FloraAndFauna/BoidManager.cs`, extends `Fauna`).
- **BoidSimulationController** — GPU compute-shader boid simulation (`FloraAndFauna/BoidSimulationController.cs`, declares `Entity` struct): dispatches `boidSimulationShader` over boid+block entities with per-team volume weights read from `GameDataSO.GetTeamVolumes`, double-buffered compute buffers. (Class name is `BoidSImulationController`.)
- **Worm** — segmented worm creature (`FloraAndFauna/Worm.cs`, MonoBehaviour): head/middle/tail segment list, follow/turn motion (mostly disabled), `AddSegment`/`RemoveSegment`/`SplitWorm`.
- **BodySegmentFauna** — one worm segment (`FloraAndFauna/BodySegmentFauna.cs`, extends `Fauna`): head/tail flags, `OnDeath` splits/updates the parent `Worm`.
- **WormManager** — spawns/grows/steers a `Worm` group toward the cell's opposing-color centroid (`FloraAndFauna/WormManager.cs`, extends `Fauna`).
- **QuadFish** — placeholder fauna type (empty `Fauna` subclass) (`FloraAndFauna/QuadFish.cs`).
- **Bone** — skeletal node with parent/children + lerp animation, used by segmented/rigged creatures (`FloraAndFauna/Bone.cs`).
- **SpawnableCord** — procedural physical "cord" of prisms + a `LineRenderer` with spring/momentum propagation (`FloraAndFauna/SpawnableCord.cs`, extends `SpawnableBase`; declares `Cord` struct).

### Crystals — elemental pickups & spawning
Crystals are the collectible fuel/score/elemental pickups. `Crystal` (a `CellItem`) handles collection, explosion into a spent-crystal impact, domain re-theming, growth, and activation-on-lifeform-death. Manager subclasses own anchor-based spawn/respawn, with a Netcode-authoritative variant.

- **Crystal** — the crystal entity (`FlowField/Crystal.cs`, extends `CellItem`; declares `CrystalModelData`): `crystalProperties`, per-domain material re-theming (`ChangeDomain`/`LerpCrystalMaterialCoroutine`), `Explode`/`ExplodeParams`/`NotifyManagerToExplodeCrystal`, `Respawn`/`DestroyCrystal`, `ActivateCrystal` (reparents to cell so a dying lifeform's crystal survives), `GrowCrystal`/`Vacuum`/`MoveToNewPos`, static `Active` registry, `CanBeCollected`. Injects `AudioSystem`.
- **CrystalManager** — abstract `NetworkBehaviour` spawn base (`FlowField/CrystalManager.cs`; declares `CrystalPositionSet`): anchor lists by intensity, batch spawn (one anchor/batch) + per-crystal respawn progression, min-distance enforcement, `CrystalCountMode` (fixed vs player-count+extra), `SpawnWithDomain`, `ResetSpawnState`; abstract `RespawnCrystal`/`ExplodeCrystal`. Injects `GameDataSO`; reads `CellRuntimeDataSO`.
- **LocalCrystalManager** — single-player/local implementation (`FlowField/LocalCrystalManager.cs`): spawns on turn-start/client-ready, respawn = local reposition, turn-end destroy.
- **NetworkCrystalManager** — server-authoritative implementation (`FlowField/NetworkCrystalManager.cs`; declares `CrystalSlotData` + `NetworkExplodeParams`): atomic position+domain `NetworkList<CrystalSlotData>`, late-joiner sync, server RPC respawn/explode, replay reset.
- **SpaceCrystalAnimator** — blendshape idle-morph + one-shot collect animation for space-element crystals (`Crystals/SpaceCrystalAnimator.cs`).
- **CrystalProperties** — serializable struct of a crystal's fuel/score/tail/speed/`Element`/value + `IsElemental` (`CrystalProperties.cs`).
- **PrismProperties** — serializable per-prism state container (position, volume, speed-debuff, trail linkage, shield/danger/transparent flags, `TimeCreated`, layer) carried by prisms including fauna/flora health-prisms (`PrismProperties.cs`).

### Cytoplasm — drifting atmosphere & motes
The cell's atmospheric "snow" and interactive nudge shards that fill the membrane interior.

- **SnowChanger** — the `CytoplasmPrefab` driver (`Cytoplasm/SnowChanger.cs`): uniformly scatters "snow" shards through the membrane sphere and reorients/rescales them toward the crystal (or an axis) on `CellRuntimeDataSO.OnCellItemsUpdated`; sized from Cell nucleus/membrane radii. Instantiated + `Initialize`d by `Cell.SpawnCytoplasm`.
- **NudgeShard** — trigger volume that nudges a passing vessel's velocity and (for Squirrel) grants energy + steals its prisms (`Cytoplasm/NudgeShard.cs`). Injects `AudioSystem`.
- **NudgeShardPoolManager** — `GenericPoolManager<NudgeShard>` object pool (`NudgeShardPoolManager.cs`).

### Flow fields
`ScriptableObject`-defined vector fields (direction × magnitude) sampled per node position, with a MonoBehaviour bridge and an editor gizmo visualizer. Distinct from the FlowField-folder crystal system above.

- **FlowFieldSO** — base flow field (`FlowField/FlowFieldSO.cs`): `fieldThickness/Width/Height/Max`, virtual `FlowVector(node)` (elliptical default).
- **EllipticalFlow / GaussianFlow / OvalFlow / PolarFlow / ZeroFlow** — five `FlowFieldSO` subclasses, each overriding `FlowVector` with a distinct closed-form field (elliptical sinusoid, twin-gaussian, oval racetrack, polar-swirl, and null); `GaussianFlow` adds a `sigma` field (`FlowField/{Elliptical,Gaussian,Oval,Polar,Zero}Flow.cs`).
- **FlowFieldData** — MonoBehaviour bridge caching an `FlowFieldSO`'s params and forwarding `FlowVector` (`FlowField/FlowFieldData.cs`).
- **FlowFieldView** — editor `OnDrawGizmosSelected` lattice visualizer of the flow field via instantiated shard nodes (`FlowField/FlowFieldView.cs`).

### Warp fields
Parallel `ScriptableObject` system to flow fields, producing a "hybrid" vector (gradient-aligned direction, scalar-field magnitude) for space-warping effects.

- **WarpFieldSO** — base warp field with `fieldThickness/Width/Height/Max` + virtual `HybridVector(node)` (`WarpField/WarpFieldSO.cs`).
- **TardisWarp** — sigmoid/atan radial warp with `minRadius` (`WarpField/TardisWarp.cs`).
- **ZeroWarp** — null warp (`WarpField/ZeroWarp.cs`).
- **WarpFieldData** — MonoBehaviour bridge forwarding `HybridVector` (`WarpField/WarpFieldData.cs`).
- **WarpFieldView** — editor gizmo lattice visualizer for warp fields (`WarpField/WarpFieldView.cs`).

### Ambient sky & diagnostics
- **SkyboxRotation** — slowly rotates the skybox geobox each frame (`SkyboxRotation.cs`).
- **StarChanger** — tints/positions a star material's color + muton position from crystal position and fuel level (`green→blue→red`) (`StarChanger.cs`).
- **EcosystemPerfProbe** — read-only perf telemetry that logs parseable `[ECOSIM] prisms=… volume=… colliders=… fauna=… phase=… fps=…` lines by summing `Cell.ActiveCellsSnapshot`, for offline tuner recalibration; optional on-screen readout, `ECOSIM_PROBE`-gated auto-spawn (`EcosystemPerfProbe.cs`).

### Interactions & patterns
- **Volume spine + phase ladder (locked invariant):** `Cell.LiveVolume`/`GetDomainVolume`/`OpposingVolume` are recomputed from live `Prism.CurrentVolume` on a 0.25s cadence and feed `CellPhaseRules.Compute` (with a prism-count Frenzy backstop) against a `CellPhaseThresholds` table resolved from `CellConfigDataSO.PhaseThresholds`. Phase projects to `FloraGrowingEnabled` (steady-until-Frenzy, hysteresis, no decay) and `CellAggressionLevel` (fauna goal/cadence/speed tiers) — the "conserved mass / food-web homeostasis" rules from `Docs/ECOSYSTEM.md`.
- **SOAP channels:** the Cell and lifeforms are wired through the shared `CellRuntimeDataSO` (`OnCellItemsUpdated`, `OnResetForReplay`, `OnPhaseChanged`, `OnCrystalSpawned`, crystal/cell-item lists, `Cell` back-reference) and `GameDataSO` (`OnInitializeGame`, `OnShowGameEndScreen`, `OnMiniGameTurnStarted/End`, `OnClientReady`, `OnResetForReplay`, `OnPlayerAdded`, controlling-team/volume queries); lifeforms raise `ScriptableEventInt` create/destroy and static `LifeForm.OnLifeFormDeath`. Fail-loud (no null-guards on event refs) per project policy.
- **NetworkVariables / Netcode:** `CellNetworkSync` replicates phase+dominant-domain+block-count; `NetworkCrystalManager` replicates crystal position+domain atomically via `NetworkList<CrystalSlotData>` with server RPC respawn/explode. Flora/fauna spawning is intentionally per-client non-deterministic, reconciled on top by the server phase mirror.
- **DI (Reflex):** `[Inject] GameDataSO` (Cell, Fauna, LifeForm, CrystalManager, BoidSimulationController), `[Inject] AudioSystem` (LifeForm, Crystal, NudgeShard).
- **Burst / spatial systems:** fauna sense/graze through `PrismSpatialIndex.QuerySphere` (prisms) + masked `Physics.OverlapSphereNonAlloc` (vessels), and Cell targeting uses per-domain `BlockCountDensityGrid`/`BlockVolumeDensityGrid` (`GetExplosionTarget`/`GetDensestRegionAnyDomain`); fauna bodies are moving `HealthPrism` mass kept honest in the index via `NotifyBodyPrismsMoved`.
- **Key data flows:** Cell → spawner → `SpawnFlora`/`SpawnFaunaWithDomain` (config-driven from `SpawnProfileSO`/`FloraConfigurationSO`/`FaunaConfigurationSO`); prisms register into Cell volume/count grids via `HealthBlockTracker` → `Cell.AddBlock`; fauna feed (`NotifyFed`) → `TryReproduce` (population driver) or starve → sealed `Fauna.Die`/`OnDeath` → `LifeFormCrystal` elemental drop + extremity-first wither (continuity + mass-conservation invariants). External collaborators outside this scope: `Prism`/`Trail`/`PrismSpatialIndex` (Vessel), `CellItem` (MiniGameObjects), `CellConfigDataSO`/`CellRuntimeDataSO`/`SpawnProfileSO`/`Fauna/FloraConfigurationSO`/`CellPhaseThresholds`/`FaunaReproductionRules` (Utility/DataContainers), `CellPhase`/`CellAggressionLevel`/`FaunaDiet` (Data/Enums), `ElementalCrystalSetSO`/`ThemeManagerDataContainerSO` (ScriptableObjects), `Assembler`/`GyroidAssembler` (Assemblers).

---

## Environment — Spawning & MiniGame Objects

This area is the game's procedural structure factory: the composable pattern-generation framework that turns math (curves, surfaces, fractals, L-systems) into laid-down prism trails, crystals, flora, and fauna, plus the mini-game objects that drive Shape Drawing mode and the procedural race/course tracks. Everything is built on one primitive — `SpawnableBase` emits `SpawnPoint[]` (pose + scale) which `PrismTrailBuilder` converts into live `Prism` trails (conserved mass, colored by `Domains`, themed by `PrismKind`). A tree of `SpawnableBase` nodes (internal "generator" nodes position children; leaf nodes instantiate prefabs) is cached by parameter hash. `SegmentSpawner` assembles a mini-game's playfield from weighted/guaranteed/intensity-mapped spawnables; `ShapeDrawingManager` orchestrates the fly-a-shape-through-crystals minigame. Two Burst-adjacent per-prism runtime animators live here too.

### Core spawn framework (`Controller/Environment/Spawning/`)

The composable, cacheable backbone. A `SpawnableBase` node is either an internal node (positions its `children` list at each generated point, normalizing scales) or a leaf (instantiates `leafPrefab` — auto-detecting `Prism` prefabs for trail management). Results are cached by `GetParameterHash()` and regenerated only on change.

- **SpawnableBase** — abstract MonoBehaviour base for all spatial pattern generation + object spawning; subclasses override `GeneratePoints()` (single trail) or `GenerateTrailData()` (multi-trail) plus `GetParameterHash()`; provides seeded `System.Random`, cache, tree recursion, `Spawn(intensity)`, and `SpawnPrismTrail` helpers. `Controller/Environment/Spawning/SpawnableBase.cs`
- **SpawnPoint** — immutable `struct` holding position/rotation/scale for one spawned object, plus safe `LookRotation` helpers. `Spawning/SpawnPoint.cs`
- **SpawnTrailData** — `[Serializable]` class: one trail's `SpawnPoint[]` + `IsLoop` + `Domain`; multiple instances represent multi-trail structures. `Spawning/SpawnTrailData.cs`
- **PrismTrailBuilder** — THE canonical "lay a prism into a trail" primitive (`LayOne`: Instantiate → ChangeTeam → ownerID → pose → TargetScale → Trail → Initialize → apply kind → `trail.Add`); three lay modes `LaySync` / `LayGradual` (coroutine) / `LayBatched` (UniTask, N-per-frame). Also defines the `PrismLay` readonly struct (SpawnPoint + Domain + PrismKind). `Spawning/PrismTrailBuilder.cs`
- **PrismKinds** — static applier of `PrismKind` state to a live `Prism` via the state-machine shield path (`Apply` additive / `Clear` / `Retheme` reversible); documents the collider-budget rule (Plain/Danger = BoxCollider, Shielded/SuperShielded = always-on convex MeshCollider). `Spawning/PrismKinds.cs`
- **PrismGeometry** — pure engine-light geometry library (no `UnityEngine.Random`, no MonoBehaviour): scalar/scale-palette helpers (`StrandScale`, `PlateScale`, `SlabScale`, `PillarScale`, `BoulderScale`, `RailScale`, `BeamScale`, `MoteScale`, `ShardScale`, `ChunkScale`, `TrunkScale`) and primitive emitters (`AddHoop`, `AddArch`, `AddVortex`, `AddCorridor`, `AddGrid3D`, `AddTorusRing`, `AddPillars`, `AddFan`, `AddScatter`, `AddWaveSheet`) shared by the freestyle microscene recipes and available to the Generators. `Spawning/PrismGeometry.cs`
- **PrismKind** *(enum, lives in `Data/Enums/PrismKind.cs` but is core to this system)* — Plain(0)/Danger(1)/Shielded(2)/SuperShielded(3); the gameplay "state" themed onto a prism, orthogonal to its `Domains` color.

### Procedural point generators (`Controller/Environment/Spawning/Generators/`)

18 lightweight `SpawnableBase` subclasses that each implement `GeneratePoints()` + `GetParameterHash()`. Designed as reusable internal/leaf nodes (`ConcentricLayersGenerator` etc. emit normalized 0–1 scales for nesting) — they position children or lay prisms along a shape.

- **AtOriginGenerator** — a single point at the origin with no rotation. `Generators/AtOriginGenerator.cs`
- **StraightLineGenerator** — points along the Z axis with `RotationMode.Random`/`Constant`. `Generators/StraightLineGenerator.cs`
- **KinkyLineGenerator** — a path with a random rotation applied to the forward vector each step. `Generators/KinkyLineGenerator.cs`
- **BranchingLineGenerator** — a kinky line that recursively branches into tree structures (branch probability, angle, depth, total-point caps). `Generators/BranchingLineGenerator.cs`
- **ConcentricLayersGenerator** — concentric layers of decreasing scale at the origin, built for nesting children. `Generators/ConcentricLayersGenerator.cs`
- **CubicGenerator** — random positions on a cubic voxel grid. `Generators/CubicGenerator.cs`
- **MazeGridGenerator** — random positions on a 3D grid. `Generators/MazeGridGenerator.cs`
- **SavedMazeGenerator** — points loaded from pre-saved `MazeData` ScriptableObjects, indexed by intensity. `Generators/SavedMazeGenerator.cs`
- **HoneycombGridGenerator** — points on a hexagonal honeycomb grid. `Generators/HoneycombGridGenerator.cs`
- **HexRingGenerator** — points in a cylindrical hex layout. `Generators/HexRingGenerator.cs`
- **CurvyTubeGenerator** — points along a sinusoidal tube path. `Generators/CurvyTubeGenerator.cs`
- **KinkyTubeGenerator** — a tube path with periodic random direction jitter. `Generators/KinkyTubeGenerator.cs`
- **CylinderSurfaceGenerator** — points along a cylinder surface with random angle tilt. `Generators/CylinderSurfaceGenerator.cs`
- **SphereSurfaceGenerator** — points on a sphere surface, angular spread controlled by difficulty angle. `Generators/SphereSurfaceGenerator.cs`
- **SphereUniformGenerator** — points uniformly distributed inside a sphere. `Generators/SphereUniformGenerator.cs`
- **ToroidSurfaceGenerator** — points on a toroidal surface. `Generators/ToroidSurfaceGenerator.cs`
- **SpiralTowerGenerator** — points along an ascending spiral/helix. `Generators/SpiralTowerGenerator.cs`
- **HilbertCurveGenerator** — points along a 3D Hilbert curve via an L-system (A/B/C/D rules interpreted with turtle rotations). `Generators/HilbertCurveGenerator.cs`

### Geometry & structure spawnables (`Controller/Environment/MiniGameObjects/`, `SpawnableBase` subclasses)

Mathematical curves/surfaces/fractals laid down as prism cages and rails; most scale with intensity. Each overrides `GeneratePoints()`/`GenerateTrailData()` and lays prisms via `SpawnPrismTrail`. `SpawnableHelix`/`SpawnableZigzag` add a local `NextFloat` randomizer.

- **SpawnableHelix** — randomized double-order helix (first/second-order radius), ~150 blocks. `SpawnableHelix.cs`
- **SpawnableZigzag** — a randomized triangular-wave zigzag line. `SpawnableZigzag.cs`
- **SpawnableTube** — a straight cylindrical tube (radius × length × segments) of prisms. `SpawnableTube.cs`
- **SpawnableCylinder** — stacked rings forming a cylinder (multi-trail per ring). `SpawnableCylinder.cs`
- **SpawnableComet** — a comet head + swept tail built from rings. `SpawnableComet.cs`
- **SpawnableWall** — a Width×Height grid wall of prisms (dimensions shrink with intensity), optional embedded `Crystal`. `SpawnableWall.cs`
- **SpawnableEllipsoid** — three orthogonal rings tracing an ellipsoid; base class for smear/pumpkin variants; safe prism-scale accessor for internal-node mode. `SpawnableEllipsoid.cs`
- **SpawnableCardioidSmear** — extends `SpawnableEllipsoid`; 12 cardioid curves smeared around the axis. `SpawnableCardioidSmear.cs`
- **SpawnablePumpkin** — extends `SpawnableEllipsoid`; ribbed pumpkin of stacked curves (defaults `Domains.Gold`). `SpawnablePumpkin.cs`
- **SpawnableDartBoard** — concentric rings of increasing block count (green/red prism alternation) forming a dartboard. `SpawnableDartBoard.cs`
- **SpawnableFiveRings** — five interlocking rings (Olympic-style), radius/count scale with intensity. `SpawnableFiveRings.cs`
- **SpawnableLinkedRings** — many great circles at Fibonacci-distributed orientations → armillary-sphere cage. `SpawnableLinkedRings.cs`
- **SpawnableSpherene** — geodesic polyhedron (subdivided icosahedron) with prisms along every edge → Buckminster-Fuller dome/fullerene lattice. `SpawnableSpherene.cs`
- **SpawnableTorusKnot** — a (p,q)-torus knot with multiple offset strands → braided rails. `SpawnableTorusKnot.cs`
- **SpawnableHelicoid** — the helicoid minimal surface (spiral ramp); multiple interleaved helicoids for layered ramps. `SpawnableHelicoid.cs`
- **SpawnableSchwarzPSurface** — the Schwarz-P triply-periodic minimal surface (cos x + cos y + cos z = 0) → 3D tunnel labyrinth. `SpawnableSchwarzPSurface.cs`
- **SpawnableHopfFibration** — prisms along fibers of the Hopf fibration S³→S² → nested interlocking tori. `SpawnableHopfFibration.cs`
- **SpawnableCliffordTorus** — flat Clifford torus in S³ stereographically projected to R³ (Dupin cyclide surface). `SpawnableCliffordTorus.cs`
- **SpawnableGyroid** — recursively grown gyroid lattice (`GyroidBlockType` seed, expansion-site toggles, spatial-grid overlap dedup). `SpawnableGyroid.cs`
- **SpawnableLSystem** — L-system fractal generator with presets (`Custom`, `BasicTree`, `Tree3D`, `HilbertCurve3D`, `KochSnowflake3D`, `SphericalSpiral`, `FractalCoral`, `CrystalStructure`). `SpawnableLSystem.cs`
- **SpawnableBaseballCurve** — the two baseball-seam curves as prism trails. `SpawnableBaseballCurve.cs`
- **SpawnableBatman** — the Batman-logo equation plotted as prism trails (piecewise wing/head/bottom functions). `SpawnableBatman.cs`
- **SpawnableSingleTrailBlock** — a single prism block at the origin (configurable `blockScale`). `SpawnableSingleTrailBlock.cs`

### Shape-drawing shapes (`SpawnableShapeBase` subclasses)

`SpawnableShapeBase` extends `SpawnableBase` to draw a recognizable 2D outline (XY plane) of prisms, gradually revealed one prism at a time (`spawnInterval`) via `PrismTrailBuilder.LayGradual`, and attaches a `Rigidbody` + `SphereCollider` + `ShapeCollisionTrigger` (auto-sized to bounds, enabled only after the reveal finishes) carrying a `ShapeDefinition`. Block count/size scale with intensity (`GetScaledBlockCount`, `GetIntensitySizeMultiplier`). 10 shapes, one per `ShapePreset`:

- **SpawnableShapeBase** — abstract base: gradual reveal, intensity scaling, trigger attachment, bounding-radius calc. `SpawnableShapeBase.cs`
- **SpawnableCircle / SpawnableStar / SpawnableHeart / SpawnableLightning / SpawnableSmiley / SpawnableSpiral / SpawnableDiamond / SpawnableInfinity / SpawnableArrow / SpawnableWave** — each emits its named outline as prism trail points (Circle is a closed loop; Smiley/Lightning use per-segment pen-up gaps). Files `SpawnableCircle.cs`, `SpawnableStar.cs`, `SpawnableHeart.cs`, `SpawnableLightning.cs`, `SpawnableSmiley.cs`, `SpawnableSpiral.cs`, `SpawnableDiamond.cs`, `SpawnableInfinity.cs`, `SpawnableArrow.cs`, `SpawnableWave.cs`.

### Shape Drawing Mode (fly-a-shape minigame)

A "connect the crystals to draw a shape" mode: fly through waypoint crystals in order (with per-segment pen-up trail gating), scored on time + path accuracy, ending in a top-down reveal cinematic. Waypoints are authored in local space (~200-unit box) on a `ShapeDefinition` SO or auto-generated from a preset.

- **ShapeDrawingManager** — MonoBehaviour orchestrator; `StartShapeSequence(def, origin)` → preview cinematic → `BeginDrawing` → spawns waypoint crystals sequentially (listening to `ShapeDrawingCrystalManager.OnWaypointCrystalHit`) → samples player path for accuracy → `FinishShape` computes `ShapeScoreData` → `RevealSequence` camera pan → prism-keeping shrink animation. Draws guide/ghost `LineRenderer`s, spawns a directional `SnowChanger`, swaps crystal managers, and raises SOAP `onShapeGameModeStarted`/`onReturnShapePrismsEvent` (both `ScriptableEventNoParam`) plus `UnityEvent`s (`OnShapeCompleted`, `OnFreestyleResumed`, `OnScoreCalculated`, `OnRevealStarted`, `OnPreviewComplete`). `ShapeDrawingManager.cs`
- **ShapeDefinition** — `ScriptableObject` (CreateAssetMenu): shape name/description/thumbnail, `waypoints` + `trailEnabledPerSegment` (pen-up mask), player start pose, reveal-camera params, par time, and runtime preset auto-generation (`ShapePreset` → `GenerateCircle/Star/Heart/Lightning/Smiley/Spiral/Diamond/Infinity/Arrow/Wave`); world-space waypoint helpers. `ShapeDefinition.cs`
- **ShapePreset** *(enum)* — None, Circle, Star, Heart, Lightning, Smiley, Spiral, Diamond, Infinity, Arrow, Wave. (in `ShapeDefinition.cs`)
- **ShapeDrawingCrystalManager** — extends `LocalCrystalManager`; overrides `ExplodeCrystal`/`RespawnCrystal` so hit crystals fire `OnWaypointCrystalHit` (C# `Action<int>`) instead of respawning; `SpawnAtPosition`/`DestroyAllCrystals` helpers for exact waypoint placement. `ShapeDrawingCrystalManager.cs`
- **ShapeScoreData** — immutable `struct`: shape name, elapsed/par time, accuracy %, derived 1–5 star rating (time 40% + accuracy 60%). `ShapeScoreData.cs`
- **ShapeScoreDisplay** — MonoBehaviour that renders `ShapeScoreData` (name/time/accuracy/stars) on the reveal screen; wired to `OnScoreCalculated`. `ShapeScoreDisplay.cs`
- **ShapeDefinitionEditor** — `[CustomEditor]` adding preset-generation buttons to the `ShapeDefinition` inspector (editor-only). `ShapeDefinitionEditor.cs`

### Signs & mode-select triggers

World-space collider gates the vessel flies through to pick a mode; each disables itself on trigger and exposes a `ResetTrigger`. They fire decoupled static event buses.

- **ShapeSign** — editor-placed trigger sign carrying a `ShapeDefinition`; on vessel `OnTriggerEnter` raises `ShapeSignEvents.RaiseShapeSelected(def, worldPos, domain)`. Defines the static **ShapeSignEvents** bus (`OnShapeSelected` event). `ShapeSign.cs`
- **ShapeCollisionTrigger** — runtime-attached (by `SpawnableShapeBase`) trigger; gated by `SetReady(true)` after gradual spawn; on collision raises `ShapeSignEvents`. `ShapeCollisionTrigger.cs`
- **SpawnableShapeSign** — plain MonoBehaviour that instantiates a `ShapeSign` prefab and injects a `ShapeDefinition` (plugs signs into `SegmentSpawner` at low weight). `SpawnableShapeSign.cs`
- **FreestyleSign** — trigger sign that raises `FreestyleSignEvents.RaiseFreestyleSelected()` to start standard segment-spawner gameplay; defines the static **FreestyleSignEvents** bus. `FreestyleSign.cs`
- **ModeSelectTrigger** — trigger with an optional `ShapeToLoad`; fires `UnityEvent<ShapeDefinition> OnModeSelected` (null = freestyle). `ModeSelectTrigger.cs`

### Segment & track spawners

Higher-level assemblers that lay out multiple spawnables into a playfield or a full race course; consumed by mini-game controllers (HexRace, Slip-n-Stride).

- **SegmentSpawner** — MonoBehaviour that builds a mini-game's segment field from `weightedSegments` (weighted-random per slot), `guaranteedSpawnables` (always spawned), and `spawnableByIntensity` (intensity-mapped override); cycles segment domains across active player domains; `Seed`-deterministic; lays segments along a line/sphere; `[Inject] GameDataSO` and auto-resets on `gameData.OnResetForReplay` (unless `ExternalResetControl`); has a diagnostic `superShieldTrackPrisms` mode using `PrismStellatedOctahedronShield`; migrates legacy serialized fields. `SegmentSpawner.cs`
- **SpawnableWaypointTrack** — `SpawnableBase` closed-loop track through per-intensity `CrystalPositionSet` waypoints; linear or Catmull-Rom spline segments, uniform `prismSpacing`, larger waypoint markers; exposes preview/query API (`GetPreviewBlocks`, `GetInterpolatedPositions`, `GetClosestPointOnTrack`, `EstimateTrackLength`) + editor gizmos. `SpawnableWaypointTrack.cs`
- **SpawnableRaceTrack** — `SpawnableBase` procedural seeded race track (target lap time × speed → length; complexity, corkscrew, banking, feature/checkpoint counts, start/finish + checkpoint domains). `SpawnableRaceTrack.cs`
- **SpawnableDriftCourse** — `SpawnableBase` long segmented drift course (~2000 blocks in randomized segments) for drift/racing modes. `SpawnableDriftCourse.cs`

### Content leaf spawnables & threat spawner

Leaf nodes that instantiate non-prism gameplay content, plus a positional threat placer.

- **SpawnableCrystal** — instantiates a single `Crystal` at the container origin, colored via `ChangeDomain`. `SpawnableCrystal.cs`
- **SpawnableFlora** — instantiates a single `Flora` instance at the origin. `SpawnableFlora.cs`
- **ThreatSpawner** — places a `Threat` around a `NodeCenter` per `SpawnMode` (ConcentratedInvasion, RandomSurfaceScatter, LocalizedAmbush, PathBasedDeployment, SphereInterdiction). `ThreatSpawner.cs`
- **CellItem** — abstract base for cell-placed buff/debuff items (`ItemType` enum: None/Buff/Debuff; `ownDomain`, `Id`, `Initialize`). `CellItem.cs`
- **HilbertCurveLSystemPositioning** — standalone MonoBehaviour that generates 3D Hilbert-curve positions/rotations via an L-system turtle (`GetPositions`/`GetRotations`) for external positioning use. `HilbertCurveLSystemPositioning.cs`

### Prism runtime animators (`Controller/Environment/Prisms/`)

Per-prism MonoBehaviours that register with the centralized manager singletons (batched, not per-object Update loops) to animate a spawned prism's look/size.

- **MaterialPropertyAnimator** — animates a prism's `_BrightColor`/`_DarkColor`/`_Spread` shader props via `MaterialPropertyBlock`; registers with `MaterialStateManager`; resolves team materials from `ThemeManagerDataContainerSO`; `UpdateMaterial`/`SetTransparency`/`MarkMaterialsDirty`. `Prisms/MaterialPropertyAnimator.cs`
- **PrismScaleAnimator** — `[RequireComponent(Prism)]`; grow-from-zero scale animation with min/max clamps, registers with `PrismScaleManager`; `Grow`, `SetTargetScale`, `BeginGrowthAnimation`, volume tracking; on scale-complete raises SOAP `ScriptableEventPrismStats onPrismVolumeModified` and triggers shield-if-largest. `Prisms/PrismScaleAnimator.cs`

### Interactions & patterns

- **One prism-lay contract:** every builder in this area (static spawnables, shape shapes, and the freestyle microscene conveyor) funnels through `PrismTrailBuilder.LayOne`, so the Instantiate→theme→scale→Initialize sequence lives in exactly one place; `PrismKinds` is the single applier of `PrismKind` shield/danger state and keeps the AOE registry consistent.
- **Conserved mass / continuity:** spawnables lay `Prism`s into `Trail`s (no TTLs or cullers); `PrismScaleAnimator` blooms them from scale-zero (continuity-of-existence law), and its volume deltas feed the ecosystem via the `onPrismVolumeModified` SOAP event.
- **SOAP channels:** `SegmentSpawner` injects `GameDataSO` (DI/Reflex) and listens to `gameData.OnResetForReplay`; `ShapeDrawingManager` raises `ScriptableEventNoParam` (`onShapeGameModeStarted`, `onReturnShapePrismsEvent`) consumed by pooled shape prisms, plus `ScriptableEventPrismStats` for volume; signs use lightweight static event buses (`ShapeSignEvents`, `FreestyleSignEvents`) and `UnityEvent`s to stay decoupled from controllers.
- **Determinism & caching:** `SpawnableBase` caches by `GetParameterHash()` and seeds an instance-local `System.Random`; `SegmentSpawner`/`SpawnableRaceTrack`/`SpawnableWaypointTrack` are seed-driven so all networked clients (e.g. HexRace's `_netTrackSeed`) build identical tracks. `PrismGeometry` is deliberately `UnityEngine.Random`-free for the same reason.
- **Consumers:** `SegmentSpawner`/`SpawnableWaypointTrack` are driven by `HexRaceController` and `SinglePlayerSlipnStrideController`; shape-drawing objects feed the (Menu freestyle) Shape Drawing flow; domains are assigned from live player `Domains` and never from client-authored state.
- **Collider budget:** `PrismKind`/`PrismKinds` document that Shielded/SuperShielded swap to always-on convex MeshColliders (LOD cannot reclaim them) — the palette keeps them rare; `SegmentSpawner.superShieldTrackPrisms` is a diagnostic-only path.
- **Editor tooling:** `SpawnableWaypointTrack` (`GetPreviewBlocks`, gizmos), `ShapeDefinitionEditor` (preset buttons), and `SegmentSpawner.EnumerateSpawnables` support in-editor preview without running the runtime prism lifecycle.

---

## Impact Effects System

The Impact Effects System is Cosmic Shore's collision-to-behavior matrix. Every collidable game entity (vessel, prism, projectile, mine, AOE explosion, crystal) carries an **Impactor** MonoBehaviour, and every collider that should trigger reactions carries an **ImpactCollider** pointing back at its impactor. When two triggers overlap, `ImpactorBase.OnTriggerEnter` resolves the other party's `IImpactor` and dispatches to a `switch` on impactee type; the "active" impactor then looks up a list of `[Impactor][Target]EffectSO` ScriptableObjects (wired through per-impactor `...DataContainerSO` assets) and calls `Execute(...)` on each. This keeps collision *reactions* fully data-driven and composable: an impactor is just a router, and each behavior (damage, steal, boost, haptics, explosion, debuff, shield, spin, VFX) is a standalone SO asset that can be swapped, reordered, or reused across vessels without code changes. Crystal collection and explosion damage add a networked/Burst fast-path on top of the same abstraction.

### Core contracts & base types
The two interfaces and the abstract base define the whole dispatch protocol; `ImpactCollider` is the single concrete `IImpactCollider` so Unity's typed-lookup fast path can be used per trigger-enter.

- **IImpactor** — collision-agent contract: `ITransform` + `Domains OwnDomain` (team of the thing that collided). `Assets/_Scripts/Controller/ImpactEffects/Impactors/IImpactor.cs`
- **IImpactCollider** — collider-side contract exposing its owning `IImpactor`. Same file.
- **ImpactorBase** — abstract `MonoBehaviour` base for all impactors: injects Reflex `Container`, exposes `Transform`/`OwnDomain`, `OnTriggerEnter` resolves the concrete `ImpactCollider` (deliberately *not* `GetComponent<IImpactCollider>` — a ~26% self-time perf fix noted in-comment) and calls abstract `AcceptImpactee(IImpactor)`; `DoesEffectExist(...)` guards empty effect arrays. `Impactors/ImpactorBase.cs`
- **ImpactCollider** — sole `IImpactCollider` impl; serialized `[RequireInterface(typeof(IImpactor))]` object reference, plus `internal SetImpactor(...)` for runtime-built colliders (e.g. conveyor-toy pickups). `ImpactCollider.cs`
- **ImpactProperties** — `[Serializable]` struct of bonus/penalty payload fields (fuel/score/tailLength/speed bonuses+penalties, `HapticType`, `Prism`). `ImpactProperties.cs`
- **ImpactEffectSO** — abstract `ScriptableObject` root of the whole effect matrix; provides `IsVesselAllowedToImpact(guestType, allowedTypes[])` gating helper. `EffectsSO/ImpactEffectSO.cs`

### Impactors (collision agents)
Each concrete impactor wraps one entity type, exposes its `OwnDomain`, and in `AcceptImpactee` routes per impactee type into the effect lists it owns (or into secondary behavior on the wrapped entity).

- **VesselImpactor** — the player/AI vessel's impactor (`[RequireComponent(IVessel, NetworkVesselImpactor)]`); routes prism hits (with HexRace track-vs-trail SFX branching), omni/elemental crystal collection (owner-authoritative via `NetworkVesselImpactor`, else local), and skimmer hits into `VesselImpactorDataContainerSO` lists; splits elemental-crystal effects per `Element`. `Impactors/VesselImpactor.cs`
- **NetworkVesselImpactor** — `NetworkBehaviour` sidecar giving crystal collection server authority: `ExecuteOnHit{Omni,Elemental}Crystal` → ServerRpc → ClientRpc → replays the collect on every client. `Impactors/NetworkVesselImpactor.cs`
- **PrismImpactor** — wraps a `Prism`; `OwnDomain => Prism.Domain`; its own per-impactee effect arrays are private/unassigned (vestigial — prism reactions actually fire from the vessel/projectile/skimmer/explosion side). `Impactors/PrismImpactor.cs`
- **ProjectileImpactor** — wraps a `Projectile`; honors `Projectile.DisallowImpactOnVessel/Prism` domain rules, routes ship/prism/mine hits into `ProjectileImpactorDataContainerSO`, and exposes `ExecuteEndEffects()` (passes itself as impactee) for end-of-life detonation. `Impactors/ProjectileImpactor.cs`
- **SkimmerImpactor** — wraps a `Skimmer`; owns skim runtime state (`_skimStartTimes`, `CombinedWeight`, `SqrSweetSpot`), vacuums crystals on `OnTriggerStay`, and routes vessel/prism/elemental-crystal hits into `SkimmerImpactorDataContainerSO`; also fires the Skimmer's secondary `ExecuteImpactOnShip/Prism`. `Impactors/SkimmerImpactor.cs`
- **ExplosionImpactor** — wraps `AOEExplosion` (`[RequireComponent(AOEExplosion)]`); dual-path: legacy Physics `OnTriggerEnter` for vessels, and **Burst batch AOE** for prisms via `PrismSpatialIndex.ProcessExplosionFrame` (`BeginBatchProcessing`/`ProcessBatchFrame`/`EndBatchProcessing`), with `affectSelf/destructive/devastating/shielding` flags, super-shield invulnerability, `ForceLegacyPhysics` A/B toggle and `ProfilerMarker`s. `Impactors/ExplosionImpactor.cs`
- **MineImpactor** — wraps a `Mine`; `OwnDomain => Domains.Blue`; routes ship/projectile/explosion hits (its effect arrays are private/unassigned — vestigial). `Impactors/MineImpactor.cs`

### Crystal impactors & collection data
Crystals share an abstract base and specialize collection semantics (domain gating, elemental vs omni, move-to-vessel animation).

- **CrystalImpactor** — abstract `ImpactorBase` for crystals (`[RequireComponent(Crystal)]`); `OwnDomain => Crystal.ownDomain`. `Impactors/CrystalImpactor.cs`
- **OmniCrystalImpactor** — collectible by any domain; on vessel touch raises `ScriptableEventCrystalStats OnCrystalCollected`, runs `VesselCrystalEffectSO[]`, explodes/respawns; guards network-client (server-authoritative) and manager-less local mints (conveyor toy); `WaitForImpact` dedups multi-collider hits. `Impactors/OmniCrystalImpactor.cs`
- **TeamCrystalImpactor** — `OmniCrystalImpactor` subclass overriding `IsDomainMatching` to strict same-domain collection. `Impactors/TeamCrystalImpactor.cs`
- **ElementalCrystalImpactor** — collected only by a `SkimmerImpactor`; runs `SkimmerCrystalEffectSO[]`, then flies the crystal to the vessel (`MoveToVesselThenPlaySpaceCollect`, smoothstep lerp) and plays the Space-collect blendshape before destroying; exposes `HasBeenCollected`, static `OnCrystalCollected`, and `internal SetCollectionEffects(...)` for runtime spawns. `Impactors/ElementalCrystalImpactor.cs`
- **CrystalImpactData** — `INetworkSerializable` struct (`Element`, `SpeedBuffAmount`, `IsAlive`) with `FromCrystal(...)` factory; the payload shipped across the crystal-collection RPCs (Burst-marked serialize/deserialize). `Impactors/CrystalImpactData.cs`

### Impactor data containers
Per-impactor `ScriptableObject` registries that hold the ordered effect lists, so the impactor code is generic and the effect wiring is authored in the inspector. (`CreateAssetMenu` under `ScriptableObjects/Impact Effects/…`.)

- **VesselImpactorDataContainerSO** — vessel's lists: `VesselPrismEffects`, `VesselCrystalEffects` (omni) + per-element `Vessel{Mass,Charge,Space,Time}CrystalEffects`, `VesselSkimmerEffects`. `Containers/VesselImpactorDataContainerSO.cs`
- **ProjectileImpactorDataContainerSO** — `ProjectileShipEffects`, `ProjectilePrismEffects`, `ProjectileMineEffect`, `ProjectileEndEffects`. `Containers/ProjectileImpactorDataContainerSO.cs`
- **SkimmerImpactorDataContainerSO** — `VesselSkimmerEffects`, `SkimmerPrismEffects`, `SkimmerCrystalEffects`. `Containers/SkimmerImpactorDataContainerSO.cs`
- **ExplosionImpactorDataContainerSO** — `vesselExplosionEffects`, `explosionPrismEffects`. `Containers/ExplosionImpactorDataContainerSO.cs`

### Effect SO abstract base types (the dispatch matrix)
Each abstract type fixes one `(impactor, impactee)` signature for `Execute(...)`; concrete effects subclass exactly one. Fifteen bases span the impactor×target grid.

- **VesselPrismEffectSO** — `Execute(VesselImpactor, PrismImpactor)`.
- **VesselCrystalEffectSO** — `Execute(VesselImpactor, CrystalImpactData)`.
- **VesselSkimmerEffectsSO** — `Execute(VesselImpactor, SkimmerImpactor)`.
- **VesselExplosionEffectSO** — `Execute(VesselImpactor, ExplosionImpactor)`.
- **VesselProjectileEffectSO** — `Execute(VesselImpactor, ProjectileImpactor)`; adds serialized `vesselTypesToImpact[]` gate.
- **VesselMineEffectSO** — `Execute(VesselImpactor, MineImpactor)` (no concrete impls today).
- **SkimmerPrismEffectSO** — `Execute(SkimmerImpactor, PrismImpactor)`.
- **SkimmerCrystalEffectSO** — `Execute(SkimmerImpactor, CrystalImpactor)`.
- **SkimmerProjectileEffectSO** — `Execute(SkimmerImpactor, ProjectileImpactor)` (no concrete impls today).
- **ProjectilePrismEffectSO** — `Execute(ProjectileImpactor, PrismImpactor)`.
- **ProjectileCrystalEffectSO** — `Execute(ProjectileImpactor, CrystalImpactor)`.
- **ProjectileMineEffectSO** — `Execute(ProjectileImpactor, MineImpactor)`.
- **ProjectileEndEffectSO** — `Execute(ProjectileImpactor, ImpactorBase)` (end-of-life self-effect).
- **ExplosionPrismEffectSO** — `Execute(ExplosionImpactor, PrismImpactor)`.
- **ExplosionMineEffectSO** — `Execute(ExplosionImpactor, MineImpactor)` (no concrete impls today).
- All in `EffectsSO/Abstract Effect Types/`.

### Effect helpers & shared specs
Static helpers and serializable value structs factor out the repeated logic (damage/steal formulas, explosion spawning, resource writes, haptics, speed-scaled skim VFX) so effect SOs stay tiny.

- **PrismEffectHelper** — static `Damage(status, prismImpactor, inertia, course, speed)` / `Damage(..., velocity)` and `Steal(...)` — the canonical prism damage/steal formulas (`course*speed*inertia`). `EffectsSO/Helpers/PrismEffectHelper.cs`
- **ExplosionHelper** — static `CreateExplosion(...)` overloads (vessel + projectile) that build `AOEExplosion.InitializeStruct`, DI-inject the instance, and detonate; scales explosion size from a vessel resource. `EffectsSO/Helpers/ExplosionHelper.cs`
- **ResourceChangeSpec** — `[Serializable]` struct (`resourceIndex`, `resourceAmount`, `overrideAmount`) with `ApplyTo(ResourceSystem)` — set-or-add a resource. `EffectsSO/Helpers/ResourceChangeSpec.cs`
- **HapticSpec** — `[Serializable]` `HapticType` wrapper; `PlayIfManual(status)` skips haptics under autopilot. `EffectsSO/Helpers/HapticSpec.cs`
- **SkimFxRunner** — internal `UniTaskVoid RunAsync(...)` that spawns/stretches a prism skim particle each frame, lifetime scaled by vessel speed, auto-cancels on prism destroy. `EffectsSO/Helpers/SkimFxRunner.cs`

### Vessel–Prism effects (13)
Reactions when a vessel body hits a prism; several are danger-prism aware (locked design: **danger prisms are friendly-fire — no domain gating**, they slow/debuff their own domain too).

- **VesselDamagePrismEffectSO** — deals `course*speed*inertia` prism damage (optional override course/speed); fires static `OnVesselDamagedPrism`. `.../Vessel Prism Effects/VesselDamagePrismEffectSO.cs`
- **VesselChangeSpeedByPrismEffectSO** — volume-scaled slow up to `maxSlowStrength`; skips own trail unless dangerous; **danger prisms → volume-independent full stop (`maxSlowStrength * dangerSlowMultiplier`)** with longer recovery. `.../VesselChangeSpeedByPrismEffectSO.cs`
- **VesselElementalDebuffByDangerPrismEffectSO** — on a danger prism, applies a decaying negative `ApplyElementalEffect` to all four elements (per-vessel cooldown); no domain gate. `.../VesselElementalDebuffByDangerPrismEffectSO.cs`
- **VesselFeelDangerByPrismEffectSO** — danger prism → `ModifyThrottle(speedDebuffAmount, duration)`. `.../VesselFeelDangerByPrismEffectSO.cs`
- **SparrowDebuffByRhinoDangerPrismEffectSO** — danger prism mutes an input (`MuteInput`+`StopShipControllerActions`) and raises `ScriptableEventVesselImpactor` + `ScriptableEventExplosionDebuffApplied`. `.../SparrowDebuffByRhinoDangerPrismEffectSO.cs`
- **VesselResetBoostPrismEffectSO** — clears boost to base multiplier, raises `ScriptableEventBoostChanged`, `ScriptableEventString onSkimmerShipCollision`, static `OnPrismCollision` (streak reset). `.../VesselResetBoostPrismEffectSO.cs`
- **VesselChangeResourceByPrismEffectSO** — halves a chosen energy resource on hit. `.../VesselChangeResourceByPrismEffectSO.cs`
- **VesselStealPrismEffectSO** — steals the prism for the vessel's domain via `PrismEffectHelper.Steal`. `.../VesselStealPrismEffectSO.cs`
- **VesselAttachPrismEffectSO** — sets `IsAttached`/`AttachedPrism` (trail-riding). `.../VesselAttachPrismEffectSO.cs`
- **VesselBounceByPrismEffectSO** — reflects heading off the prism normal + shoves velocity away. `.../VesselBounceByPrismEffectSO.cs`
- **VesselDeviationByPrismEffectSO** — lateral (randomizable) bounce/gentle-spin, respects `IsTranslationRestricted`. `.../VesselDeviationByPrismEffectSO.cs`
- **VesselFXPrismEffectSO** — spawns the speed-scaled skim particle via `SkimFxRunner`. `.../VesselFXPrismEffectSO.cs`
- **VesselHapticsByPrismEffectSO** — plays a `HapticSpec` (manual-only). `.../VesselHapticsByPrismEffectSO.cs`

### Vessel–Crystal effects (9)
Reactions when a vessel/skimmer collects a crystal; keyed off `CrystalImpactData.Element` by the container.

- **VesselIncrementLevelByCrystalEffectSO** — `ResourceSystem.IncrementLevel(element)` + local per-element SFX. `.../Vessel Crystal Effects/VesselIncrementLevelByCrystalEffectSO.cs`
- **VesselAdjustLevelByCrystalEffectSO** — `AdjustLevel(element, ±N)` + local SFX. `.../VesselAdjustLevelByCrystalEffectSO.cs`
- **VesselChangeResourceByCrystalEffectSO** — applies a `ResourceChangeSpec`. `.../VesselChangeResourceByCrystalEffectSO.cs`
- **VesselModifyThrotleByCrystalEffectSO** — `ModifyThrottle(data.SpeedBuffAmount, duration)`. `.../VesselModifyThrotleByCrystalEffectSO.cs`
- **VesselSetShieldByCrystalEffectSO** — sets a shield resource to full. `.../VesselSetShieldByCrystalEffectSO.cs`
- **VesselExplosionByCrystalEffectSO** — spawns an AOE on crystal collect (resource-scaled, anti-spam cooldown); raises per-vessel events (`ScriptableEventVesselImpactor` Rhino/Squirrel, static `OnMantaFlowerExplosion`). `.../VesselExplosionByCrystalEffectSO.cs`
- **VesselHapticsByCrystalEffectSO** — `HapticSpec` on collect. `.../VesselHapticsByCrystalEffectSO.cs`
- **VesselCollisionReporterSO** — raises `ScriptableEventString onSkimmerShipCollision` (stats reporting). `.../VesselCollisionReporterSO.cs`
- **VesselDecoyByCrystalEffectSO** — currently a no-op (mine-decoy spawn body commented out; `minePrefab` retained). `.../VesselDecoyByCrystalEffectSO.cs`

### Vessel–Explosion effects (1)
- **VesselChangeSpeedByExplosionEffectSO** — an AOE that catches a vessel mutes an input for N seconds and raises `ScriptableEventExplosionDebuffApplied` + `ScriptableEventVesselImpactor`. `.../Vessel Explosion Effects/VesselChangeSpeedByExplosionEffectSO.cs`

### Vessel–Projectile effects (5)
Reactions when a projectile strikes a vessel; most gate on `vesselTypesToImpact[]`.

- **VesselChangeSpeedByProjectileEffectSO** — `ModifyThrottle(amount, duration)`. `.../Vessel Projectile Effects/VesselChangeSpeedByProjectileEffectSO.cs`
- **VesselSpinByProjectileEffectSO** — spins the ship along the impact vector (type-gated). `.../VesselSpinByProjectileEffectSO.cs`
- **VesselGentleSpinByProjectileEffectSO** — randomized-sign gentle yaw spin. `.../VesselGentleSpinByProjectileEffectSO.cs`
- **VesselChangeSkimmerSizeByProjectileEffectSO** — applies a temporary max-skimmer-size debuff via `ShieldSkimmerScaleConfigSO` (type-gated). `.../VesselChangeSkimmerSizeByProjectileEffectSO.cs`
- **VesselSpinBySkyBurstProjectileEffectSO** — gentle-or-hard spin the victim, then detonates the SkyBurst projectile through `ProjectileDetonatorSO`. `.../VesselSpinBySkyBurstProjectileEffectSO.cs`

### Vessel–Skimmer effects (9 effects + 1 config)
Reactions when a vessel body crosses another vessel's skimmer sphere — the core of Joust scoring and Rhino/Squirrel skimmer abilities.

- **VesselExplosionBySkimmerEffectSO** — Joust point: faster vessel overtaking a slower **opponent** scores (`ScriptableEventString OnJoustCollision`), spawns AOE, `GameFeedAPI.PostJoust`, plays Joust SFX; teammates score nothing. `.../Vessel Skimmer Effects/VesselExplosionBySkimmerEffectSO.cs`
- **VesselOvertakeBySkimmerEffectSO** — Squirrel overtake: overtaken **ally** buffed / **opponent** debuffed (decaying `ApplyElementalEffect` on all four elements) + haptics + per-element buff SFX. `.../VesselOvertakeBySkimmerEffectSO.cs`
- **VesselDamageBySkimmerEffectSO** — Rhino skimmer mutes a victim input and raises `ScriptableEventSkimmerDebuffApplied`. `.../VesselDamageBySkimmerEffectSO.cs`
- **VesselDangerBlockFormationBySkimmerEffectSO** — Rhino skimmer spawns an `AOEDangerHemisphereBlocks` formation aimed at the cell crystal (uses `CellRuntimeDataSO`); static `OnDangerBlockSpawned`. `.../VesselDangerBlockFormationBySkimmerEffectSO.cs`
- **VesselAssembledArchBurstBySkimmerEffectSO** — procedurally spawns a Bézier-surface lattice of rod prisms in front of the impactee (tri/hex tessellation, Perlin jitter, layered). `.../VesselAssembledArchBurstBySkimmerEffectSO.cs`
- **VesselShrinkSkimmerEffectSO** — temporarily shrinks the hit skimmer (coroutine lerp + restore, per-attacker cooldown; writes `Skimmer.Scale` via reflection). `.../VesselShrinkSkimmerEffectSO.cs`
- **VesselSpinBySkimmerEffectSO** — signed yaw spin of the victim (+ optional lateral shove), sign derived from attacker/victim facing. `.../VesselSpinBySkimmerEffectSO.cs`
- **VesselHapticsBySkimmerEffectSO** — `HapticSpec` on skimmer contact. `.../VesselHapticsBySkimmerEffectSO.cs`
- **VesselPrismSpawnerCooldownBySkimmerEffectSO** — stops the vessel's trail spawner for N seconds then restarts. `.../VesselPrismSpawnerCooldownBySkimmerEffectSO.cs`
- **DangerHemisphereConfigSO** — config asset (not an effect) for the danger-hemisphere burst: timing, ray count/blocks, radius/spread, growth curve, `PrismEventChannelWithReturnSO`, shield/danger flags, theme container. `.../Vessel Skimmer Effects/DangerHemisphereConfigSO.cs`

### Skimmer–Prism effects (18) + blend helper
Reactions while a skimmer sphere sweeps prisms — alignment/boost/steal/damage plus the shader-driven forcefield visualization. `CombinedWeight`/`SqrSweetSpot` from `SkimmerImpactor` feed the analog ones.

- **SkimmerForcefieldCracklePrismEffectSO** — computes the sphere-surface impact point via `Collider.ClosestPoint`, feeds a `ForcefieldCrackleController` (position+duration+intensity+radius) driving the electrical-arc shader; optional particle burst. Replaces `SkimmerFXPrismEffectSO`. `.../Skimmer Prism Effects/SkimmerForcefieldCracklePrismEffectSO.cs`
- **SkimmerFXPrismEffectSO** — `[Obsolete]` legacy skim particle via `SkimFxRunner` (kept for asset compat). `.../SkimmerFXPrismEffectSO.cs`
- **SkimmerBoostPrismEffectSO** — accrues boost per hit up to a shared max; **danger prism grants `dangerEnergyMultiplier` (10×) energy** (the risk/reward danger-trail); raises `ScriptableEventBoostChanged`. `.../SkimmerBoostPrismEffect.cs`
- **SkimmerAlignPrismEffectSO** — tube-riding: looks ahead along the trail and gently aligns the skimmer's forward/up (Squirrel drift alignment), scaled by `CombinedWeight`. `.../SkimmerAlignPrismEffectSO.cs`
- **SkimmerDamagePrismEffectSO** — standard `course*speed*inertia` prism damage. `.../SkimmerDamagePrismEffectSO.cs`
- **RhinoSkimmerDamagePrismEffectSO** — super-shield prism → bounce the Rhino back (reflect/reverse + gentle spin); otherwise normal damage. `.../RhinoSkimmerDamagePrismEffectSO.cs`
- **SkimmerStealPrismEffectSO** — steals the prism; static `OnSkimmerStolenPrism`. `.../SkimmerStealPrismEffectSO.cs`
- **SkimmerOverchargeCollectPrismEffectSO** — accumulates unique enemy-prism hits (shield/deshield own/enemy prisms), fires overcharge-ready events, then `ConfirmOvercharge` blows up collected prisms over time via recursive raycast destruction; cooldown. `.../SkimmerOverchargeCollectPrismEffectSO.cs`
- **SkimmerAddShieldByPrismEffectSO** — grows a shield resource per enemy-prism skim (scale-unit math, capped). `.../SkimmerAddShieldByPrismEffectSO.cs`
- **SkimmerChangeResourceByPrismEffectSO** — applies a `ResourceChangeSpec`. `.../SkimmerChangeResourceByPrismEffectSO.cs`
- **SkimmerVisualizeDistancePrismEffectSO** — writes normalized skimmer-to-prism distance into a resource (HUD viz). `.../SkimmerVisualizeDistancePrismEffectSO.cs`
- **SkimmerModifyThrotleByPrismEffectSO** — sets boost multiplier from `CombinedWeight`. `.../SkimmerModifyThrotleByPrismEffectSO.cs`
- **SkimmerScaleGapByPrismEffectSO** — lerps trail `Gap` toward minimum by `CombinedWeight`. `.../SkimmerScaleGapByPrismEffectSO.cs`
- **SkimmerScalePitchAndYawPrismEffectSO** — scales pitch/yaw responsiveness by `CombinedWeight`. `.../SkimmerScalePitchAndYawPrismEffectSO.cs`
- **SkimmerScaleTrailPrismEffectSO** — sets normalized trail X-scale from skimmer-prism distance (class `SkimmerScaleTrailPrismEffectSO`; file `SkimmerScaleTrailPrismEffectSO.cs`). `.../SkimmerScaleTrailPrismEffectSO.cs`
- **SkimmerScaleTrailAndCameraPrismEffectSO** — older normalized trail X-scale variant (camera hook commented out). `.../SkimmerScaleTrailAndCameraPrismEffectSO.cs`
- **SkimmerHapticsByPrismEffectSO** — `HapticSpec` on skim. `.../SkimmerHapticsByPrismEffectSO.cs`
- **SkimmerScaleHapticWithDistanceByPrismSO** — continuous haptic scaled by `CombinedWeight` (manual-only). `.../SkimmerScaleHapticWithDistanceByPrismSO.cs`
- **MaterialBlendUtility** — static reusable renderer material/property blend-over-time helper (coroutine host via injected `BlendRunner`, MPB fallback for cross-shader); used by the overcharge effect. `.../Skimmer Prism Effects/MaterialBlendUtility.cs`

### Skimmer–Crystal effects (3)
Elemental-crystal collection reactions (live in the Skimmer Prism Effects folder but subclass `SkimmerCrystalEffectSO`).

- **SkimmerAdjustElementLevelByCrystalEffectSO** — grants the crystal's own element a level boost scaled by crystal world-scale (capped, `ComputeLevelGain` is pure/testable) + local per-element SFX. `.../SkimmerAdjustElementLevelByCrystalEffectSO.cs`
- **SkimmerSetShieldByCrystalEffectSO** — sets a shield resource to full on crystal skim. `.../SkimmerSetShieldByCrystalEffectSO.cs`
- **SkimmerSFXByCrystalEffectSO** — plays a positional `GameplaySFXCategory` (default `CrystalSkim`) at the skimmer. `.../SkimmerSFXByCrystalEffectSO.cs`

### Projectile–Prism / Crystal / Mine / End effects (9)
Reactions driven by projectiles hitting prisms, crystals, mines, or reaching end-of-life; several route through the detonation service.

- **ProjectileDamagePrismEffectSO** — velocity×inertia prism damage. `.../Projectile Prism Effects/ProjectileDamagePrismEffectSO.cs`
- **ProjectileStealPrismEffectSO** — steals the prism for the shooter. `.../ProjectileStealPrismEffectSO.cs`
- **DomainCheckProjectilePrismHitEffectSO** — block-projectile logic: friendly pass-through vs. damage enemy prism, optionally also destroy the shooter's own prism (returns it to `BlockProjectileFactory`), then `ExecuteEndEffects()`. `.../DomainCheckProjectilePrismHitEffectSO.cs`
- **SkyBurstProjectileDamagePrismEffectSO** — damages the prism, then stops-at-contact and detonates after a delay via `ProjectileDetonatorSO`. `.../SkyBurstProjectileDamagePrismEffectSO.cs`
- **ProjectileExplosionByCrystalEffectSO** — spawns an AOE (charge-scaled) when a projectile hits a crystal. `.../Projectile Crystal Effects/ProjectileExplosionByCrystalEffectSO.cs`
- **ProjectileExplodeMineSO** — nullifies a mine's delayed explosion, passing projectile velocity. `.../Projectile Mine Effects/ProjectileExplodeMineSO.cs`
- **SkyBurstProjectileExplodeMineSO** — nullifies the mine and detonates the projectile via the detonator service. `.../SkyBurstProjectileExplodeMineSO.cs`
- **DetonateEndEffectSO** — projectile end-of-life: spawn AOE (charge-scaled, face-velocity) and return to pool via `ProjectileDetonatorSO`. `.../Projectile End Effects/DetonateEndEffectSO.cs`
- **DetonateSparrowProjectileEndEffectSO** — simplest end effect: `projectile.ReturnToFactory()`. `.../DetonateSparrowProjectileEndEffectSO.cs`

### Projectile detonation service
- **ProjectileDetonatorSO** — shared `ScriptableObject` service (`ScriptableObjects/Services/Projectile Detonator`) with a `Request` struct; `Detonate` → async `DetonateAsync` stops motion, disables collider, optional explode-delay, faces exit velocity, charge-lerps AOE scale, instantiates+DI-injects+initializes+detonates each `AOEExplosion` prefab, then returns the projectile to its factory. Used by the SkyBurst and Detonate end/prism/mine effects. `EffectsSO/ProjectileDetonatorSO.cs`

### Interactions & patterns
- **Dispatch topology.** Colliders → `ImpactCollider` → `ImpactorBase.OnTriggerEnter` → `AcceptImpactee` `switch` → per-pair `[Impactor][Target]EffectSO.Execute`. The *active* impactor (vessel/projectile/skimmer/explosion) owns the effect lists via its `...DataContainerSO`; `PrismImpactor`/`MineImpactor` carry vestigial private effect arrays, so prism/mine reactions fire from the striking side.
- **SOAP channels.** Effects raise decoupled events rather than call systems directly: `ScriptableEventBoostChanged` (boost), `ScriptableEventString` (`OnJoustCollision`, `onSkimmerShipCollision` stats), `ScriptableEventVesselImpactor` / `ScriptableEventExplosionDebuffApplied` / `ScriptableEventSkimmerDebuffApplied` (HUD debuff feedback), `ScriptableEventCrystalStats` (`OnCrystalCollected`), `PrismEventChannelWithReturnSO` (danger-hemisphere prism spawns). A handful of legacy static `event Action`s remain (`OnVesselDamagedPrism`, `OnSkimmerStolenPrism`, `OnMantaFlowerExplosion`, `OnPrismCollision`, `OnDangerBlockSpawned`, `ElementalCrystalImpactor.OnCrystalCollected`). Audio uses `AudioSystem.PlayGameplaySFX(GameplaySFXCategory, position)` and `GameFeedAPI.PostJoust`.
- **NetworkVariables / Netcode.** Crystal collection is server-authoritative: `NetworkVesselImpactor` bounces `CrystalImpactData` through ServerRpc→ClientRpc; `OmniCrystalImpactor` early-outs on non-server clients; AOE damage carries the striking vessel's `Domain`/`Player.Name` for scoring attribution (anonymous explosions use `Domains.Blue`/"🔥GuyFawkes🔥").
- **Burst / spatial index.** `ExplosionImpactor` bypasses Physics for prisms entirely, driving AOE damage through `PrismSpatialIndex.ProcessExplosionFrame` each frame (Burst), honoring super-shield invulnerability, with `ProfilerMarker`s and a `ForceLegacyPhysics` A/B path; the `TrailBlocks` layer is skipped in `OnTriggerEnter` while batching.
- **DI (Reflex).** `ImpactorBase` injects the `Container`; explosion spawns (`ExplosionHelper`, `ProjectileDetonatorSO`, danger-hemisphere) call `GameObjectInjector.InjectRecursive` on freshly instantiated AOE objects so their own effects/systems resolve.
- **Locked designs surfaced here.** Danger prisms are friendly-fire (no domain gating anywhere: `VesselChangeSpeedByPrismEffectSO`, `VesselElementalDebuffByDangerPrismEffectSO`, `VesselFeelDangerByPrismEffectSO`, `SparrowDebuffByRhinoDangerPrismEffectSO`) and multiply skim energy 10× (`SkimmerBoostPrismEffectSO`); the forcefield-crackle effect visualizes the invisible skimmer sphere and supersedes the obsolete skim-FX effect; per-effect anti-spam cooldowns live in the SO config (per-`VesselImpactor`/`ResourceSystem`/`Skimmer` dictionaries), and haptics always route through `HapticSpec.PlayIfManual` to stay silent under autopilot.

---

## Arcade — MiniGame Controllers & Modes

This is the game-mode layer that turns a launched scene into a playable match. Every mode is a **Template Method** state machine rooted in `MiniGameControllerBase` (a `NetworkBehaviour`): it drives the fixed lifecycle **game → rounds → turns → countdown → gameplay → turn-end → round-end → game-end**, exposing `virtual`/`abstract` hooks (`OnCountdownTimerEnded`, `SetupNewTurn`, `OnTurnEndedCustom`, `OnRoundEndedCustom`, `EndGame`, `RequestReplay`) that each concrete controller overrides. Two abstract branches split the tree: `SinglePlayerMiniGameControllerBase` (local, event-driven via `GameDataSO` SOAP) and `MultiplayerMiniGameControllerBase` (server-authoritative, every lifecycle beat mirrored to clients via `[ClientRpc]`). All shared runtime state (players, per-player `RoundStats`, scoring rule, winner) lives on the injected `GameDataSO`; the controllers never own it. Turn-end conditions are detected by pluggable `TurnMonitor`s, scoring by `ScoringRuleSO`/`BaseScoring` strategies, and end-of-turn winner attribution by per-mode server logic that snapshots `RoundStatsList` into a `SyncFinalScores_ClientRpc`. The domain minigames (HexRace, Joust, Crystal Capture, Astro League) all inherit `MultiplayerDomainGamesController` and share a per-domain aggregated scoring model; Tournament ("Maelstrom") chains three of them.

### Lifecycle core (template method)
The skeleton and the two authority branches. `EndTurn`/`EndRound`/`EndGame` are the spine; subclasses hook the `*Custom` overrides and the `bool` policy properties (`HasEndGame`, `ShouldResetPlayersOnTurnEnd`, `ShowEndGameSequence`, `UseGolfRules`, `UseSceneReloadForReplay`).

- **MiniGameControllerBase** — abstract `NetworkBehaviour` base; defines the round/turn/countdown/end template, `numberOfRounds`/`numberOfTurnsPerRound` config, `[Inject] GameDataSO`, `countdownTimer`, `_onToggleReadyButton` (`ScriptableEventBool`), and abstract `OnCountdownTimerEnded`/`RequestReplay`. `Controller/Arcade/MiniGameControllerBase.cs`
- **SinglePlayerMiniGameControllerBase** — abstract local branch; `Start()` wires `GameDataSO.OnMiniGameTurnEnd`→`EndTurn` + `OnResetForReplay`, calls `InitializeGame`/`InvokeClientReady`/`SetupNewRound`; `OnCountdownTimerEnded`→`SetPlayersActive`+`StartTurn`; `RequestReplay` resets locally + snaps camera. `Controller/Arcade/SinglePlayerMiniGameControllerBase.cs`
- **MultiplayerMiniGameControllerBase** — abstract server-authoritative branch; `[Inject] SceneTransitionManager`; `OnNetworkSpawn` server-subscribes turn/session events + `SyncGameConfigToClients_ClientRpc`; `InitializeAfterDelay` (1000 ms) → `InitializeGame`/`InvokeSessionStarted`/`SetupNewRound`; every turn/round/game transition wrapped in `SyncTurnEnd`/`SyncRoundEnd`/`SyncGameEnd`/`ShowReadyButton` ClientRpcs; `EnsureLocalHumanCanMove`; replay via `ResetForReplay_ClientRpc` or full-scene reload (`ExecuteSceneReloadReplay` despawns AI + vessels, `ResetRuntimeData`, Netcode `LoadScene`). `Controller/Arcade/MultiplayerMiniGameControllerBase.cs`
- **MultiplayerDomainGamesController** — base for the four domain minigames; three `NetworkVariable<int>` domain-score sums replicated server→all (throttled 0.1 s coroutine over `ScoringMetrics.SumByDomain`) and mirrored into `GameDataSO`; human ready-up (`OnReadyClicked_ServerRpc` counts `ConnectedClientsIds`), `GameFeedAPI` "Ready"/"disconnected" posts, delayed `EndGame`. `Controller/Arcade/MultiplayerDomainGamesController.cs`
- **CountdownTimer** — `MonoBehaviour` 3-2-1 countdown; DOTween sprite/scale/fade sequence with `HUDAnimationSettingsSO`, invokes an `Action onComplete` (the controller's `OnCountdownTimerEnded`). `Controller/Arcade/CountdownTimer.cs`
- **MiniGame** — DEPRECATED pre-`MiniGameControllerBase` abstract `MonoBehaviour` game loop (static `OnMiniGameStart`/`OnMiniGameEnd` events, `PauseSystem` hooks, elimination bookkeeping); mostly commented-out, retained only as base of `CellularBrawlMiniGame`/`ProtectMissionGame`. `Controller/Arcade/MiniGame.cs`

### Single-player mode controllers
Concrete `SinglePlayerMiniGameControllerBase` subclasses; each customizes turn setup and replay.

- **SinglePlayerCellularDuelController** — 2-player duel; `ShouldResetPlayersOnTurnEnd=true`, swaps vessels (`gameData.SwapVessels`) on turn end and replay. `Controller/Arcade/SinglePlayerCellularDuelController.cs`
- **SinglePlayerWildlifeBlitzController** — blitz mode; drives `SinglePlayerWildlifeBlitzScoreTracker` + `SingleplayerWildlifeBlitzTurnMonitor` + `TimeBasedTurnMonitor`, resets score/environment on replay, reads `CellRuntimeDataSO`. `Controller/Arcade/SinglePlayerWildlifeBlitzController.cs`
- **WildlifeBlitzMiniGame** — minimal single-player blitz variant (Start→Play→End→Scoreboard), only overrides `SetupNewTurn`. `Controller/Arcade/WildlifeBlitzMiniGame.cs`
- **SinglePlayerSlipnStrideController** — Slip'n'Stride procedural trail-course; spawns a `SegmentSpawner` course (+ optional `SpawnableHelix`) with intensity-scaled segment count/length and per-turn or once environment reset. `Controller/Arcade/SinglePlayerSlipnStrideController.cs`
- **SandboxBenchmarkController** — endless free-flight stress-test controller (`HasEndGame=false`, `ShowEndGameSequence=false`); no `TurnMonitor`, auto-starts the countdown so the human + AI Squirrels fly indefinitely while spawners ramp load. `Controller/Arcade/SandboxBenchmarkController.cs`
- **VolumeTestController** — empty placeholder `MonoBehaviour` (test scaffold, no logic). `Controller/Arcade/VolumeTestController.cs`

### Multiplayer domain-game controllers
Concrete `MultiplayerDomainGamesController` subclasses. All four set `numberOfRounds=1`, publish a `ScoringRuleSO` to `GameDataSO.ScoringRule`, use `HasEndGame=false` + `UseSceneReloadForReplay=true`, and end the game server-side in `OnTurnEndedCustom` → per-mode `SyncFinalScores_ClientRpc` (snapshots `RoundStatsList`, sets `WinnerName`/`WinnerDomain`, `InvokeWinnerCalculated`+`InvokeMiniGameEnd`).

- **HexRaceController** — crystal-collection race; deterministic track from a server-generated seed replicated via `_netTrackSeed` NetworkVariable (immediate/`OnValueChanged`/poll-fallback paths) into `SegmentSpawner`+`SpawnableHelix` (intensity-scaled); `UseGolfRules=true`, winner = first domain whose crystal sum hits target, scores = finish time / loser deficit sentinel. `Controller/Arcade/HexRaceController.cs`
- **MultiplayerJoustController** — jousting collisions; `UseGolfRules=true`, winning domain = highest summed `JoustCollisions`, winner+teammates score elapsed time, losers a flat sentinel; `SyncJoustResults_ClientRpc`. `Controller/Arcade/MultiplayerJoustController.cs`
- **MultiplayerCrystalCaptureController** — crystal-capture points mode (`UseGolfRules=false`); Score = personal crystal count, domain aggregation decides standing; `OnResetForReplayCustom` zeroes crystals/score. `Controller/Arcade/MultiplayerCrystalCaptureController.cs`
- **MultiplayerCellularDuelController** — 2-player cell duel; swaps vessel network-ownership (`ChangeOwnership`) + `gameData.SwapVessels` between rounds and on replay. `Controller/Arcade/MultiplayerCellularDuelController.cs`
- **AstroLeagueController** — see the dedicated Astro League subsystem below (the largest concrete controller).

### Other multiplayer controllers
- **MultiplayerFreestyleController** — networked sandbox; per-player activation (`OnCountdownTimerEnded_ServerRpc`→`SetNewPlayerActive`), `SetNonOwnerPlayersActiveInNewClient` on client-ready; no scoring/end condition. `Controller/Arcade/MultiplayerFreestyleController.cs`
- **MultiplayerWildlifeBlitzMiniGame** — co-op blitz; own ready-sync (`OnReadyClicked_ServerRpc` counts humans), broadcasts round setup via ClientRpc. `Controller/Arcade/MultiplayerWildlifeBlitzMiniGame.cs`

### Legacy / mission controllers (`MiniGame` base)
- **CellularBrawlMiniGame** — thin `MiniGame` subclass (only calls `base.Start`); brawl variant on the deprecated loop. `Controller/Arcade/CellularBrawlMiniGame.cs`
- **ProtectMissionGame** — mission mode on the legacy `MiniGame`; weighted threat-wave spawner (`ThreatSpawner`, `SO_Mission.PotentialThreats`, difficulty/intensity scaling, fauna-only limit past a team-volume threshold), squadmate/hostile-AI setup. `Controller/Arcade/Missions/ProtectMissionGame.cs`

### Tournament / Maelstrom
"Maelstrom" is the player-facing name of `GameModes.Tournament`. A persistent pure-C# brain chains three domain minigames via sequential `Single` scene loads; standings are network-free (every peer folds the synced `GameDataSO.Results` identically), the host drives a randomized lineup and race-to-N.

- **TournamentController** — eager DI singleton (`static Instance`) alive from bootstrap; subscribes `OnMiniGameEnd` + `SceneManager.sceneLoaded`; folds per-round results into `TournamentDataSO` standings/history, decides lobby-vs-hub-vs-summary-vs-restart on the Maelstrom scene load, draws a random `(mode, intensity∈[1..ceiling])` and launches via `GameDataSO.SyncFromArcadeGame`+`InvokeGameLaunch`; exposes `MinLoadSplashDwellSeconds` for the between-game standings splash. `Controller/Arcade/Tournament/TournamentController.cs`
- **TournamentStateMachine** / **TournamentPhase** — table-driven phase tracker (`Idle→Lobby→InGame→Complete→Summary`) with validated transitions + `OnPhaseChanged`; each peer runs its own, kept in lock-step by deterministic scene/result signals. `Controller/Arcade/Tournament/TournamentStateMachine.cs`
- **TournamentLobbyNetwork** — scene-placed `NetworkBehaviour`; host-authoritative ready-up + countdown for the lobby/hub (`_deadline`/`_readyCount`/`_totalPlayers` NetworkVariables, 30 s auto-start snapping to 5 s once all ready), fires `TournamentController.BeginNextRound` at deadline. `Controller/Arcade/Tournament/TournamentLobbyNetwork.cs`
- **TournamentSceneView** — data-driven `MonoBehaviour` view for the Maelstrom scene; two panels (active lobby/hub with round-history cards + countdown, and the results summary with domain ranking + per-player cards), reads `TournamentDataSO` + `DomainColorPaletteSO`, host-only Play Again / Main Menu, DOTween juice. `Controller/Arcade/Tournament/TournamentSceneView.cs`

### Astro League (hypersea soccer)
A full server-simulated soccer match on the domain-games stack: two domains slam a billiard-physics ball through the opposing goal, with kickoffs, celebrations, golden-goal overtime, and per-intensity court shapes morphed onto the Cell nucleus.

- **AstroLeagueController** — match director extending `MultiplayerDomainGamesController`; owns the `PreMatch→Kickoff→Live→Celebration→Overtime→Finished` phase machine, replicates match config (`n_IntensityScale`/`n_BoundaryShape`/`n_GoalTarget`/`n_CentralGoal` NetworkVariables), attributes goals to the last non-defending striker (`GoalsScored` on `RoundStats`), drives AI strikers via `AIPilot.SetExternalTargetProvider` (billiard approach thinking), kickoff parking, announcer ClientRpcs, and the shared server-authoritative end-game. `Controller/Arcade/AstroLeague/AstroLeagueController.cs`
- **AstroLeagueBall** — server-authoritative billiard ball `NetworkBehaviour`; server rigidbody simulation replicated via `n_Position`/`n_Velocity`/`n_AngularVelocity`/`n_Frozen`/`n_Hidden`/`n_LastHitDomain`, clients dead-reckon; momentum-conserving elastic vessel strikes + anti-clip depenetration, per-tick `PrismSpatialIndex.QuerySphere` prism resolution (shield own / eat+slow opposing), zero-friction coast, boundary reflect, impact juice (flash/particles/shake/haptics/hitstop), prism-fresnel icosphere visuals. `Controller/Arcade/AstroLeague/AstroLeagueBall.cs`
- **AstroLeagueArena** — runtime-built stadium `MonoBehaviour` (deterministic per-peer); constructs the `AstroLeagueBoundary`, goal-portal rings + midfield ring (LineRenderers), scales everything with match intensity, and hands the boundary to the ball; delegates all atmosphere/boundary visuals to the standard Cell (membrane/cytoplasm/nucleus). `Controller/Arcade/AstroLeague/AstroLeagueArena.cs`
- **AstroLeagueBoundary** / **AstroLeagueBoundaryShape** — immutable court-geometry helper + shape enum (Sphere, Box, Octagonal/Hexagonal Prism, BeveledBox, Octahedron, Cylinder, NotchedRing); convex-polytope face-plane containment (`Contain` — banks off flat walls), analytic sphere/cylinder/torus branches, and `BuildVisualMesh` (convex hull / barrel / notched torus) that morphs the cell nucleus so the visible cage IS the wall the ball hits. `Controller/Arcade/AstroLeague/AstroLeagueBoundary.cs`
- **AstroLeagueGoal** — server-authoritative goal-line detector `MonoBehaviour`; FixedUpdate plane-crossing poll (leading-edge for solid-backed goals, center for pass-through central goals) within the mouth radius, teleport-guarded, reports clean crossings to `AstroLeagueController.HandleGoalServer`. `Controller/Arcade/AstroLeague/AstroLeagueGoal.cs`
- **AstroLeagueMatchMonitor** — server match clock as a `TurnMonitor`; counts down regulation time, pushes "M:SS"/"OT" via the shared display channel, pausable for kickoffs/celebrations, raises `OnClockExpired`, ends the turn only on controller `ForceEnd`. `Controller/Arcade/AstroLeague/AstroLeagueMatchMonitor.cs`
- **AstroLeagueScoringRuleSO** — `ScoringRuleSO` (points, metric=Goals); mercy-rule `IsObjectiveReached`, Score=personal goal tally, ranked results + WON/LOST-BY-N reveal. `Controller/Arcade/AstroLeague/AstroLeagueScoringRuleSO.cs`
- **AstroLeagueSettingsSO** — single designer config `ScriptableObject`; match rules (duration/goal limit/overtime), per-intensity boundary-shape + central-goal arrays + scale ramp, ball physics (mass/bounce/drag/spin/mesh), strike/recoil/kickoff pacing, AI striker tuning, and all juice constants (hitstop/shake/flash/particles/wall-juice gating). `Controller/Arcade/AstroLeague/AstroLeagueSettingsSO.cs`

### Objective providers
`IObjectiveProvider` implementations feeding the HUD objective indicator; each returns the current target `Transform`.

- **HexRaceObjectiveProvider** — closest live `Crystal` in the local player's own domain; event-driven cache (`ElementalCrystalImpactor.OnCrystalCollected`), iterates the `Crystal.Active` registry (no scene scan), `ProfilerMarker`-instrumented. `Controller/Arcade/HexRaceObjectiveProvider.cs`
- **JoustObjectiveProvider** — closest other player's vessel from `GameDataSO.Players`. `Controller/Arcade/JoustObjectiveProvider.cs`
- **AstroLeagueObjectiveProvider** — the ball (cached `AstroLeagueBall`, hidden while `IsHidden`). `Controller/Arcade/AstroLeagueObjectiveProvider.cs`

### Turn monitors (turn-end detection)
`Controller/Arcade/TurnMonitors/`. `TurnMonitorController` polls a list each frame; each `TurnMonitor` reports `CheckForEndOfTurn()`. Network variants gate on `IsServer` and delegate the end condition to `GameDataSO.ScoringRule.IsObjectiveReached` (per-domain aggregate) while syncing the target + remaining readout.

- **TurnMonitorController** — `NetworkBehaviour` that owns a `List<TurnMonitor>`, starts/stops them on `OnMiniGameTurnStarted`/`OnMiniGameTurnEnd`, and raises `InvokeGameTurnConditionsMet` when any monitor triggers (works single- and multiplayer via network-or-`OnEnable` subscription). `Controller/Arcade/TurnMonitorController.cs`
- **TurnMonitor** — abstract `NetworkBehaviour` base; `_updateInterval` UniTask loop, `StartMonitor`/`StopMonitor`/`Pause`/`Resume`/`ResetMonitor`, abstract `CheckForEndOfTurn`, `onUpdateTurnMonitorDisplay` (`ScriptableEventString`) HUD channel. `Controller/Arcade/TurnMonitors/TurnMonitor.cs`
- **CrystalCollisionTurnMonitor** / **NetworkCrystalCollisionTurnMonitor** — crystal-count end condition (target from `EndConditionOverridesSO`→waypoints→39); network variant syncs target via `_netCrystalCollisions` NetworkVariable→`GameDataSO.CrystalTargetCount`, subscribes every player's `OnCrystalsCollectedChanged`, ends on the rule's per-domain objective. `Controller/Arcade/TurnMonitors/CrystalCollisionTurnMonitor.cs`, `NetworkCrystalCollisionTurnMonitor.cs`
- **JoustCollisionTurnMonitor** / **NetworkJoustCollisionTurnMonitor** — joust-count end condition (`EndConditionOverridesSO`→default 3, published to `JoustTargetCount`); network variant owns collision sync RPCs (`ReportCollision_ServerRpc`/`SyncCollision_ClientRpc`) and ends on the rule's per-domain objective. `Controller/Arcade/TurnMonitors/JoustCollisionTurnMonitor.cs`, `NetworkJoustCollisionTurnMonitor.cs`
- **TimeBasedTurnMonitor** / **NetworkTimeBasedTurnMonitor** — fixed-duration timer (`ElapsedTime`/`TimeRemaining`); network variant broadcasts the readout via `UpdateTimerUI_ClientRpc`. `Controller/Arcade/TurnMonitors/TimeBasedTurnMonitor.cs`, `NetworkTimeBasedTurnMonitor.cs`
- **SingleplayerWildlifeBlitzTurnMonitor** — ends when local score reaches the cell type's `CellEndGameScore`; raises score-target + lifeform-counter events, exposes `DidPlayerWin`. `Controller/Arcade/SingleplayerWildlifeBlitzTurnMonitor.cs` (top-level)
- **AllLifeFormsDestroyedTurnMonitor** — ends when the current cell's lifeform count hits 0 (`CellRuntimeDataSO`), updates a counter event. `Controller/Arcade/TurnMonitors/AllLifeFormsDestroyedTurnMonitor.cs`
- **CellControlTurnMonitor** — ends when the local player's domain controls the cell by volume (`GetControllingTeamStatsBasedOnVolumeRemaining`). `Controller/Arcade/TurnMonitors/CellControlTurnMonitor.cs`
- **VolumeCreatedTurnMonitor** — ends when any player's `VolumeCreated` exceeds a threshold. `Controller/Arcade/TurnMonitors/VolumeCreatedTurnMonitor.cs`
- **VolumeDestroyedTurnMonitor** — ends when the local player's `TotalVolumeDestroyed > 0`. `Controller/Arcade/TurnMonitors/VolumeDestroyedTurnMonitor.cs`
- **DistanceTurnMonitor** — ends after a distance traveled (largely stubbed against speed). `Controller/Arcade/TurnMonitors/DistanceTurnMonitor.cs`
- **ShipCollisionTurnMonitor** (`VesselCollisionTurnMonitor.cs`) — skimmer/ship-collision end condition (currently stubbed to `true`). `Controller/Arcade/TurnMonitors/VesselCollisionTurnMonitor.cs`
- **ResourceAccumulationTurnMonitor** — ends when a resource reaches a percent of max (stubbed pending `ResourceSystem` wiring). `Controller/Arcade/TurnMonitors/ResourceAccumulationTurnMonitor.cs`

### Scoring rules & result model
`Controller/Arcade/Scoring/` (+ top-level helpers). Per-mode stateless strategy assets consumed by the domain controllers, turn monitors, HUD, scoreboard and reveal.

- **ScoringRuleSO** — abstract per-mode strategy `ScriptableObject`; `metric` (`ScoringMetric`) + `golfRules`, `LiveMetric`/`Remaining`/`ResolveWinner` over `ScoringMetrics.SumByDomain`, abstract `IsObjectiveReached`/`AssignScores`/`BuildResults`/`BuildReveal`, `DomainDelta` helper. `Controller/Arcade/Scoring/ScoringRuleSO.cs`
- **HexRaceScoringRuleSO** — golf-timed crystals; winner=finish time, losers encode team crystals-left sentinel, RACE TIME / CRYSTALS LEFT reveal. `Controller/Arcade/Scoring/HexRaceScoringRuleSO.cs`
- **JoustScoringRuleSO** — golf-timed jousts; winner domain scores time, losers flat sentinel, WON/LOST-BY-N-JOUSTS reveal. `Controller/Arcade/Scoring/JoustScoringRuleSO.cs`
- **CrystalCaptureScoringRuleSO** — points; Score=crystal count, highest sum wins, WON/LOST-BY-N-CRYSTALS reveal. `Controller/Arcade/Scoring/CrystalCaptureScoringRuleSO.cs`
- **ScoringMetrics** — static reader/summer: `Read(IRoundStats, ScoringMetric)` + `SumByDomain` (the one metric-parameterized aggregator every rule uses). `Controller/Arcade/Scoring/ScoringMetrics.cs`
- **ScoreResultBuilder** — static ranked-`ScoreResult` assembler (`Row` struct, `Build`/`BuildRanked` stable sort, shared `FormatTime`). `Controller/Arcade/ScoreResultBuilder.cs`
- **ScoreReveal** — readonly-struct end-game cinematic payload (header/label/value/format-as-time). `Controller/Arcade/Scoring/ScoreReveal.cs`
- **GolfScoreSentinels** — static single source of truth for loser/DNF score sentinels (`DnfThreshold`, `JoustLoserScore`, HexRace encode/decode/is-finish helpers). `Controller/Arcade/GolfScoreSentinels.cs`

### Score trackers & composite-scoring modes
The pre-rule (still-used for single-player/blitz) scoring pipeline: a tracker builds `BaseScoring` strategies from `ScoringConfig[]`, each subscribing to a `RoundStats` stat event and writing `Score`.

- **IScoreTracker** — `CalculateTotalScore(playerName)` interface. `Controller/Arcade/IScoreTracker.cs`
- **BaseScoreTracker** — abstract `NetworkBehaviour` tracker; `ScoringConfig[]`→`BaseScoring[]` factory (`CreateScoring` switch over `ScoringModes`), subscribes/unsubscribes per turn, sums scores, sorts + `InvokeWinnerCalculated`. `Controller/Arcade/BaseScoreTracker.cs`
- **ScoreTracker** — offline/single-player tracker (`OnEnable`/`OnDisable` subscription). `Controller/Arcade/ScoreTracker.cs`
- **NetworkScoreTracker** — server-only tracker; computes winner then `SendRoundStats_ClientRpc` after a 500 ms delay. `Controller/Arcade/NetworkScoreTracker.cs`
- **HexRaceScoreTracker** — tracks local elapsed race time into `LocalRoundStats.Score`, encodes loser score via `GolfScoreSentinels`, reports `VesselTelemetry`/HexRace stats to `UGSStatsManager`, exposes stats (`IStatExposable`). `Controller/Arcade/HexRaceScoreTracker.cs`
- **SinglePlayerWildlifeBlitzScoreTracker** — blitz tracker; adds score on `LifeForm.OnLifeFormDeath` + `ElementalCrystalImpactor.OnCrystalCollected`, win time vs sentinel, reports blitz + telemetry stats to UGS. `Controller/Arcade/SinglePlayerWildlifeBlitzScoreTracker.cs`
- **ScoringModes** — enum of 17 scoring modes (VolumeCreated, CrystalsCollected, PrismsCreated, LifeFormsKilled, ElementalCrystalsCollectedBlitz, …). `Controller/Arcade/ScoringModes.cs`
- **BaseScoring** — abstract per-metric scoring strategy (`Score`, `scoreMultiplier`, `Subscribe`/`Unsubscribe`). `Controller/Arcade/Scoring/BaseScoring.cs`
- **BaseScoring subclasses** — the concrete metric strategies wired by `BaseScoreTracker.CreateScoring`: top-level **PrismsCreatedScoring**, **HostilePrismsDestroyedScoring**, **FriendlyPrismsDestroyedScoring** (`Controller/Arcade/*Scoring.cs`); and in `Scoring/`: **VolumeCreatedScoring**, **HostileVolumeDestroyedScoring**, **FriendlyVolumeDestroyedScoring**, **TimePlayedScoring**, **TurnsPlayedScoring**, **TeamVolumeDifferenceScoring**, **CrystalsCollectedScoring** (`CrystalType` All/Omni/Elemental, size-scaling), **LifeFormsKilledScoring**, **ElementalCrystalsCollectedBlitzScoring**, **VolumeAndBlocksStolenScoring** (throws `NotImplementedException` — stub). Each subscribes to a `RoundStats` change event (or a static impactor/lifeform event) and sets `Score = stat × multiplier`.
- **BaseScoringMode** / **CompositeScoringMode** — older serializable additive-scoring strategy base + composite that sums child modes (`CalculateScore`/`EndTurnScore`). `Controller/Arcade/Scoring/BaseScoringMode.cs`, `CompositeScoringMode.cs`
- **CompositeScoring** / **ScoreData** — fully commented-out / DEPRECATED legacy scoring aggregator + score-data holder (present but inert). `Controller/Arcade/Scoring/CompositeScoring.cs`, `Controller/Arcade/ScoreData.cs`

### End-game stats providers, reporters & comeback
Scoreboard stat surfacing (subclasses of UI `ScoreboardStatsProvider`), UGS reporters (react to `OnMiniGameEnd`), and the elemental comeback buffer.

- **HexRaceStatsProvider** / **MultiplayerJoustStatsProvider** / **MultiplayerCrystalCaptureStatsProvider** — build the per-mode scoreboard `StatData` lists (streak/drift/jousts/boost/crystals) from the tracker/`RoundStats`+`VesselTelemetry`. `Controller/Arcade/HexRaceStatsProvider.cs`, `MultiplayerJoustStatsProvider.cs`, `MultiplayerCrystalCaptureStatsProvider.cs`
- **CrystalCaptureStatsReporter** / **JoustStatsReporter** — `MonoBehaviour`s that report the local winner's stats + `VesselTelemetry` to `UGSStatsManager` on `OnMiniGameEnd`. `Controller/Arcade/CrystalCaptureStatsReporter.cs`, `JoustStatsReporter.cs`
- **WildlifeBlitzEndGameStatsTracker** — compiles final blitz stats (win time vs 999 sentinel) on `OnMiniGameEnd`, exposes display tuple. `Controller/Arcade/Scoring/WildlifeBlitzEndGameStatsTracker.cs`
- **ElementalComebackSystem** — applies elemental buffs to losing players scaled to their **domain** deficit; `SO_ElementalComebackProfile` per-vessel/element weights, `ScoreDifferenceSource` (Score/CrystalsCollected/Goals), per-element comeback audio, operates only in the 0.0–1.5 normalized range. `Controller/Arcade/ElementalComebackSystem.cs`

### Interactions & patterns
- **`GameDataSO` is the hub.** Every controller is `[Inject]`-ed the shared `GameDataSO` and communicates through its SOAP channels (`OnMiniGameTurnStarted/End`, `OnMiniGameRoundStarted/End`, `OnMiniGameEnd`, `OnResetForReplay`, `OnClientReady`, `OnSessionStarted`, `InvokeWinnerCalculated`) and mutable state (`RoundStatsList`/`LocalRoundStats`, `Players`/`Vessels`, `ScoringRule`, `WinnerName`/`WinnerDomain`, `Results`, `CrystalTargetCount`/`JoustTargetCount`/`GoalTargetCount`, `Selected*`, `RequestedAIBackfillCount`/`RequestedDomainCount`, `IsTournamentMode`). Controllers never store gameplay state themselves.
- **Server authority.** All multiplayer flow is server-driven `[ClientRpc]`/`[ServerRpc]` mirroring (turn/round/game transitions, ready-up, config sync, final-score snapshots), with `NetworkVariable`s for continuously-replicated values (domain score sums, HexRace track seed, Astro League ball state + match config, tournament ready/deadline). Turn-end authority is delegated to `ScoringRuleSO.IsObjectiveReached` over per-domain metric sums so AI + human teammates finish together.
- **DI + injected services.** Reflex `[Inject]` supplies `GameDataSO`, `SceneTransitionManager`, `UGSStatsManager`, `AudioSystem`; `TournamentController` is an eager DI singleton bridged to scene code via `static Instance`. `CameraManager.Instance`/`AudioSystem.Instance` are used from lifecycle hooks.
- **Turn/scoring plug-ins.** `TurnMonitorController`→`TurnMonitor` detect turn end and raise `InvokeGameTurnConditionsMet`; `ScoringRuleSO`/`BaseScoring` compute scores; end-game counts come from `EndConditionOverridesSO` (the End Game Conditions tool), never per-scene fields.
- **Cross-system reach.** Astro League consumes `PrismSpatialIndex` (Burst sphere queries) for ball↔prism resolution, morphs the standard **Cell** nucleus for its court, and steers AI via `AIPilot.SetExternalTargetProvider`; Tournament chains modes through `SceneLoader`/`GameDataSO.InvokeGameLaunch`; UGS reporters and `GameFeedAPI` posts fan out to leaderboards and the in-game feed; `ElementalComebackSystem` feeds the elementals fundamental (`ResourceSystem`).

---

## Arcade — Scoring & Turn Monitors

This area decides **how a turn ends**, **who won**, **what score each player gets**, and **how those results replicate to every client**. Two generations of code coexist. The **legacy per-player event pipeline** (`BaseScoreTracker` + `BaseScoring` strategies keyed off a `ScoringModes` enum, plus a family of `TurnMonitor` MonoBehaviours) drives single-player and older modes: each strategy subscribes to `IRoundStats` change events and writes `RoundStats.Score`. The **current domain-aggregated pipeline** (`ScoringRuleSO` strategy assets + `ScoringMetrics.SumByDomain` + network turn monitors that delegate their end condition to `ScoringRule.IsObjectiveReached`) drives the three networked domain modes (HexRace, Joust, Crystal Capture) and Astro League: the turn ends when any active domain's summed metric hits a target, the server computes final scores/results, and a `_ClientRpc` snapshots them to every peer. All scoring is server-authoritative in networked modes; golf-rule modes encode "loser/DNF" scores with `GolfScoreSentinels`.

### Per-mode scoring-rule strategy SOs (current pipeline)

Stateless `ScriptableObject` strategies — one asset per mode, published to `GameDataSO.ScoringRule`. Every shared consumer (network turn monitor end-condition, in-game HUD, scoreboard, end-game reveal) asks the rule instead of forking per-mode. A rule is a pure function of the `GameDataSO` passed in (shared singletons, no per-game fields).

- **ScoringRuleSO** — abstract base: holds the `ScoringMetric` + `golfRules` flag; provides `LiveMetric`, `Remaining` (target − domain sum), `ResolveWinner` (highest domain sum, Jade→Ruby→Gold tie-break), `DomainDelta` (winner-vs-best-loser gap for the reveal); abstract `IsObjectiveReached(out winner)`, `AssignScores`, `BuildResults`, `BuildReveal`. `Assets/_Scripts/Controller/Arcade/Scoring/ScoringRuleSO.cs`
- **HexRaceScoringRuleSO** — golf-timed crystal race; first domain whose crystal sum ≥ target (`CrystalTargetCount>0 ? configured : 39`) wins; winners score finish time, losers get `GolfScoreSentinels.EncodeHexRaceLoserScore(remaining)`. `.../Scoring/HexRaceScoringRuleSO.cs`
- **JoustScoringRuleSO** — golf-timed jousting; target = `JoustTargetCount`; winning domain scores elapsed time, losers a flat `JoustLoserScore` sentinel; deficits are the domain gap. `.../Scoring/JoustScoringRuleSO.cs`
- **CrystalCaptureScoringRuleSO** — points (not golf); target = `CrystalTargetCount`; each player's `Score` = their crystal count; shares the crystal turn monitor with HexRace. `.../Scoring/CrystalCaptureScoringRuleSO.cs`
- **AstroLeagueScoringRuleSO** — points; metric = `GoalsScored`; `IsObjectiveReached` is the mercy rule (`GoalTargetCount`), full-time/golden-goal decided by the controller; each player's `Score` = personal goals. (In sibling `Controller/Arcade/AstroLeague/`, a peer of the three above.) `.../AstroLeague/AstroLeagueScoringRuleSO.cs`

### Scoring value model & result assembly

Shared value types + helpers that keep the number players watch identical to the number that ends the game, and produce one ranked list every end-game surface reads.

- **ScoringMetric** (enum) — the single per-player stat a rule aggregates: `Crystals(0)`, `OmniCrystals(1)`, `ElementalCrystals(2)`, `Jousts(3)`, `Goals(4)`. `Assets/_Scripts/Data/Enums/ScoringMetric.cs`
- **ScoringMetrics** (static) — `Read(stats, metric)` reads one player's metric; `SumByDomain(gameData, metric, domain)` sums it across every player on a domain (THE aggregator every rule/turn-monitor uses). `.../Scoring/ScoringMetrics.cs`
- **ScoreResult** (readonly struct) — one ranked row: `Rank`, `Name`, `Domain`, `Score`, `ScoreText` (mode-formatted primary), `Secondary`. The single "who placed where" record for scoreboard/cinematic/reward. `Assets/_Scripts/Data/Structs/ScoreResult.cs`
- **ScoreResultBuilder** (static) — `Build(rows, golfRules)` (sorts + 1-based ranks), `BuildRanked(orderedRows)` (rank-only when caller pre-sorted), `FormatTime(seconds)` shared mm:ss:cs; nested `Row` input struct. `Assets/_Scripts/Controller/Arcade/ScoreResultBuilder.cs`
- **ScoreReveal** (readonly struct) — local player's end-game cinematic payload: `Header` ("VICTORY"/"DEFEAT"), `Label`, `Value`, `FormatAsTime`; produced by the rule's `BuildReveal` so reveal and scoreboard can't disagree. `.../Scoring/ScoreReveal.cs`
- **GolfScoreSentinels** (static) — single source of truth for loser/DNF sentinels in time-based golf modes: `DnfThreshold=10000`, `HexRaceLoserBase`, `JoustLoserScore=99999`; `EncodeHexRaceLoserScore`/`DecodeHexRaceCrystalsLeft`, `IsHexRaceLoserScore`, `IsJoustLoserScore`, `IsFinishTime`. `Assets/_Scripts/Controller/Arcade/GolfScoreSentinels.cs`

### Score trackers (legacy per-player pipeline)

`NetworkBehaviour` components that instantiate `BaseScoring` strategies from a serialized `ScoringConfig[]`, subscribe them on turn start, and recompute per-player totals. Wire to `GameDataSO` SOAP turn/game lifecycle events. On game end they sort round stats + calculate domain stats + raise `InvokeWinnerCalculated`.

- **IScoreTracker** — one-method interface (`CalculateTotalScore(playerName)`) strategies call to recompute a player's total. `Assets/_Scripts/Controller/Arcade/IScoreTracker.cs`
- **BaseScoreTracker** — abstract `NetworkBehaviour : IScoreTracker`; subscribes to `OnInitializeGame`/`OnMiniGameTurnStarted`/`OnMiniGameTurnEnd`/`OnMiniGameEnd`/`OnClickToMainMenu`; `InitializeScoringMode` builds `scoringArray` via `CreateScoring(mode,multiplier)` switch over `ScoringModes`; `CalculateTotalScore` sums every strategy's `Score` into `RoundStats.Score`; `SortAndInvokeResults`; `GetScoring<T>()`; nested `ScoringConfig` struct (`Mode` + `Multiplier`). `Assets/_Scripts/Controller/Arcade/BaseScoreTracker.cs`
- **ScoreTracker** — offline/single-player concrete; subscribes on `OnEnable`, sorts+invokes locally. `.../ScoreTracker.cs`
- **NetworkScoreTracker** — server-only concrete; wires events in `OnNetworkSpawn` (server), on game end delays 500ms then `SendRoundStats_ClientRpc` → every client runs `SortAndInvokeResults`. `.../NetworkScoreTracker.cs`
- **HexRaceScoreTracker** — `BaseScoreTracker, IStatExposable`; per-frame writes local elapsed time into `LocalRoundStats.Score`, on `OnMiniGameTurnEnd` computes win/loss + encodes loser sentinel and reports UGS HexRace + vessel telemetry (winner only); winner detection itself is the controller's job; `GetExposedStats()` feeds the scoreboard stats provider. `.../HexRaceScoreTracker.cs`
- **SinglePlayerWildlifeBlitzScoreTracker** — blitz scoring; `Start/StopTracking` subscribes to static `LifeForm.OnLifeFormDeath` + `ElementalCrystalImpactor.OnCrystalCollected`, adds to `LocalRoundStats.Score`; `ResetScores` on `OnResetForReplay`; `CalculateWinnerAndInvokeEvent` sets win-time vs `999f`, reports UGS blitz + telemetry. `.../SinglePlayerWildlifeBlitzScoreTracker.cs`
- **ScoringModes** (enum) — 17 legacy strategy ids: `HostileVolumeDestroyed(0)`, `VolumeCreated(1)`, `TimePlayed(2)`, `TurnsPlayed(3)`, `VolumeStolen(4)`, `BlocksStolen(5)`, `TeamVolumeDifference(6)`, `CrystalsCollected(7)`, `OmniCrystalsCollected(8)`, `ElementalCrystalsCollected(9)`, `CrystalsCollectedScaleWithSize(10)`, `FriendlyVolumeDestroyed(11)`, `PrismsCreated(12)`, `HostilePrismsDestroyed(13)`, `FriendlyPrismsDestroyed(14)`, `LifeFormsKilled(15)`, `ElementalCrystalsCollectedBlitz(16)`. `Assets/_Scripts/Controller/Arcade/ScoringModes.cs`

### BaseScoring strategies (legacy per-player)

`[Serializable]` strategies constructed by `BaseScoreTracker.CreateScoring`. Each `Subscribe`/`Unsubscribe` to per-`IRoundStats` change events (or static gameplay events) and writes its own `Score`, then calls `ScoreTracker.CalculateTotalScore`.

- **BaseScoring** — abstract base: `Score`, `scoreMultiplier` (default 145.65), `GameData`, `ScoreTracker`, `TryGetRoundStats`. `.../Scoring/BaseScoring.cs`
- **CrystalsCollectedScoring** — subscribes `OnCrystalsCollectedChanged`; nested `CrystalType {All,Omni,Elemental}` + `scaleWithSize`; tracks its own `_subscribedStats` list so a mid-turn scene exit can't leak the handler onto the next game's persistent RoundStats (BUGS.md B15). `.../Scoring/CrystalsCollectedScoring.cs`
- **VolumeCreatedScoring** — `OnVolumeCreatedChanged` → `VolumeCreated × mult`. `.../Scoring/VolumeCreatedScoring.cs`
- **HostileVolumeDestroyedScoring** — `OnHostileVolumeDestroyedChanged`, reward. `.../Scoring/HostileVolumeDestroyedScoring.cs`
- **FriendlyVolumeDestroyedScoring** — `OnFriendlyVolumeDestroyedChanged`, penalty (negative). `.../Scoring/FriendlyVolumeDestroyedScoring.cs`
- **PrismsCreatedScoring** (`internal`) — `OnBlocksCreatedChanged` → `BlocksCreated × mult`. `Assets/_Scripts/Controller/Arcade/PrismsCreatedScoring.cs`
- **HostilePrismsDestroyedScoring** (`internal`) — `OnHostilePrismsDestroyedChanged`, reward. `.../HostilePrismsDestroyedScoring.cs`
- **FriendlyPrismsDestroyedScoring** (`internal`) — `OnFriendlyPrismsDestroyedChanged`. `.../FriendlyPrismsDestroyedScoring.cs`
- **TimePlayedScoring** — UniTask loop adding `dt × mult` to every player's `Score` on an interval; uses `NetworkManager.ServerTime` when networked, else `Time.timeAsDouble`. `.../Scoring/TimePlayedScoring.cs`
- **LifeFormsKilledScoring** — static `LifeForm.OnLifeFormDeath` → `+mult` per kill; exposes `ScorePerKill`, `GetTotalLifeFormsKilled`. `.../Scoring/LifeFormsKilledScoring.cs`
- **ElementalCrystalsCollectedBlitzScoring** — static `ElementalCrystalImpactor.OnCrystalCollected` → `+mult`; exposes `GetScoreMultiplier`, `GetTotalCrystalsCollected`. `.../Scoring/ElementalCrystalsCollectedBlitzScoring.cs`
- **TeamVolumeDifferenceScoring** — legacy; `Subscribe/Unsubscribe` throw `NotImplementedException` (scoring body commented out). `.../Scoring/TeamVolumeDifferenceScoring.cs`
- **TurnsPlayedScoring** — legacy (elimination, deprecated by comment); throws `NotImplementedException`. `.../Scoring/TurnsPlayedScoring.cs`
- **VolumeAndBlocksStolenScoring** — legacy (volume/blocks stolen via `trackBlocks`); throws `NotImplementedException`. `.../Scoring/VolumeAndBlocksStolenScoring.cs`

Deprecated / dead in this family:
- **BaseScoringMode** + **CompositeScoringMode** — older `CalculateScore/EndTurnScore` composite abstraction, unused by the trackers. `.../Scoring/BaseScoringMode.cs`, `.../Scoring/CompositeScoringMode.cs`
- **CompositeScoring** — entirely commented out. `.../Scoring/CompositeScoring.cs`
- **ScoreData** — entirely commented out (former round-stats container). `Assets/_Scripts/Controller/Arcade/ScoreData.cs`

### Turn monitors

`TurnMonitor` is a `NetworkBehaviour` polled each frame (`Update` → `CheckForEndOfTurn`) plus an async `RestrictedUpdate` tick loop; `TurnMonitorController` owns the list and raises the end-of-turn SOAP event. Networked monitors delegate the actual end condition to `gameData.ScoringRule.IsObjectiveReached` (server only) and mirror targets/remaining via NetworkVariables.

- **TurnMonitor** — abstract base: `_updateInterval`, `gameData`, `onUpdateTurnMonitorDisplay` (`ScriptableEventString`); `StartMonitor`/`StopMonitor`/`Pause`/`Resume`/`ResetMonitor`; abstract `CheckForEndOfTurn`; virtual `RestrictedUpdate`/`OnTurnEnded`/`ResetState`; UniTask `RunLoopAsync`. `.../TurnMonitors/TurnMonitor.cs`
- **TurnMonitorController** — `NetworkBehaviour` owning `List<TurnMonitor>`; subscribes `OnMiniGameTurnStarted`→StartMonitors / `OnMiniGameTurnEnd`→StopMonitors (network in `OnNetworkSpawn`, single-player fallback in `OnEnable`, idempotent `-=`/`+=`); each frame if any monitor's `CheckForEndOfTurn` → latches off + `gameData.InvokeGameTurnConditionsMet()`. `Assets/_Scripts/Controller/Arcade/TurnMonitorController.cs`
- **TimeBasedTurnMonitor** — ends at `elapsedTime ≥ duration`; ticks elapsed + raises countdown display; exposes `ElapsedTime`/`Duration`/`TimeRemaining`. `.../TurnMonitors/TimeBasedTurnMonitor.cs`
- **NetworkTimeBasedTurnMonitor** — replicates the timer display string via `UpdateTimerUI_ClientRpc`. `.../TurnMonitors/NetworkTimeBasedTurnMonitor.cs`
- **CrystalCollisionTurnMonitor** — resolves target from `EndConditionOverridesSO` (Tools > End Game Conditions) → waypoints×laps → 39 (never `[SerializeField]`); ends when local `CrystalsCollected ≥ target`; `GetRemainingCrystalsCountToCollect`; subscribes local `OnCrystalsCollectedChanged`. `.../TurnMonitors/CrystalCollisionTurnMonitor.cs`
- **NetworkCrystalCollisionTurnMonitor** — syncs resolved target via `_netCrystalCollisions` NetworkVariable → `gameData.CrystalTargetCount`; subscribes every player's crystal event (domain-sum HUD) tracked in `_subscribedStats`; `CheckForEndOfTurn` (server) = `ScoringRule.IsObjectiveReached`; remaining UI = local player's domain deficit. Used by HexRace **and** Crystal Capture. `.../TurnMonitors/NetworkCrystalCollisionTurnMonitor.cs`
- **JoustCollisionTurnMonitor** — resolves joust target from `EndConditionOverridesSO` (default 3) → `gameData.JoustTargetCount`; ends when local `JoustCollisions ≥ collisionsNeeded`; exposes `CollisionsNeeded`. `.../TurnMonitors/JoustCollisionTurnMonitor.cs`
- **NetworkJoustCollisionTurnMonitor** — all peers subscribe every player's joust event (`_subscribedStats`); client→`ReportCollision_ServerRpc`, server→`SyncCollision_ClientRpc` (higher-count-wins, anti-recursion); `CheckForEndOfTurn` (server) = `ScoringRule.IsObjectiveReached`; remaining UI = domain deficit. `.../TurnMonitors/NetworkJoustCollisionTurnMonitor.cs`
- **AllLifeFormsDestroyedTurnMonitor** — ends when a cell's lifeform count ≤ 0 (`CellRuntimeDataSO`); raises a lifeform-counter string event. `.../TurnMonitors/AllLifeFormsDestroyedTurnMonitor.cs`
- **SingleplayerWildlifeBlitzTurnMonitor** — ends when `LocalRoundStats.Score ≥ CellEndGameScore` (from `CellRuntimeDataSO.Config`); sets `DidPlayerWin`; raises score-target + lifeform-counter events. `Assets/_Scripts/Controller/Arcade/SingleplayerWildlifeBlitzTurnMonitor.cs`
- **CellControlTurnMonitor** — ends when local player's domain controls the cell by volume (`GetControllingTeamStatsBasedOnVolumeRemaining`). `.../TurnMonitors/CellControlTurnMonitor.cs`
- **VolumeCreatedTurnMonitor** — ends when any player's `VolumeCreated > amount`; raises remaining-volume string. `.../TurnMonitors/VolumeCreatedTurnMonitor.cs`
- **VolumeDestroyedTurnMonitor** — ends when local player's `TotalVolumeDestroyed > 0` (elimination-style, mostly stubbed). `.../TurnMonitors/VolumeDestroyedTurnMonitor.cs`
- **DistanceTurnMonitor** — distance-traveled target (speed source commented out — legacy/stub). `.../TurnMonitors/DistanceTurnMonitor.cs`
- **ResourceAccumulationTurnMonitor** — resource-percent target; body TODO, currently returns `true`. `.../TurnMonitors/ResourceAccumulationTurnMonitor.cs`
- **ShipCollisionTurnMonitor** — vessel-collision target; largely commented out, returns `true` (legacy stub). File: `.../TurnMonitors/VesselCollisionTurnMonitor.cs`

### Comeback (elemental rubber-banding)

- **ElementalComebackSystem** — `MonoBehaviour` that buffs losing **domains** each `updateInterval` by writing elemental levels (0.0–1.5 normalized) via `ResourceSystem.SetElementLevel`, scaled by a per-vessel `SO_ElementalComebackProfile`. Reads deficits as domain aggregates: `ScoreDifferenceSource {Score, CrystalsCollected, Goals}` → `SumCrystalsCollectedByDomain` / `ScoringMetrics.SumByDomain`; per-element comeback SFX on the local player with a cooldown; snapshots baselines on turn start, inactive between turns. `Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs`

### End-game stats providers, reporters & objective providers

Scoreboard stat rows, UGS stat reporters, and the objective-indicator target suppliers. Providers/reporters read `GameDataSO.RoundStatsList` + local vessel telemetry on `OnMiniGameEnd`.

- **ScoreboardStatsProvider** (abstract base) + **StatData** struct — `GetStats()` → labeled/iconed scoreboard rows. `Assets/_Scripts/UI/ScoreboardStatsProvider.cs`
- **IStatExposable** — `GetExposedStats()` dictionary (implemented by `HexRaceScoreTracker`). `Assets/_Scripts/UI/IStatExposable.cs`
- **HexRaceStatsProvider** — pulls best streak / longest drift / jousts won / max boost from `HexRaceScoreTracker.GetExposedStats`. `Assets/_Scripts/Controller/Arcade/HexRaceStatsProvider.cs`
- **MultiplayerJoustStatsProvider** — jousts won + race time (if won, via `JoustCollisionTurnMonitor.CollisionsNeeded`) + drift/boost telemetry. `.../MultiplayerJoustStatsProvider.cs`
- **MultiplayerCrystalCaptureStatsProvider** — crystals collected + omni + drift/boost telemetry. `.../MultiplayerCrystalCaptureStatsProvider.cs`
- **JoustStatsReporter** — on `OnMiniGameEnd`, if local player is `WinnerName`, reports UGS joust stats + vessel telemetry. `.../JoustStatsReporter.cs`
- **CrystalCaptureStatsReporter** — on `OnMiniGameEnd`, if local player is rank-0 after sort, reports UGS crystal-capture stats + telemetry. `.../CrystalCaptureStatsReporter.cs`
- **WildlifeBlitzEndGameStatsTracker** — on `OnMiniGameEnd`, folds blitz win/time into `LocalRoundStats.Score` (win-time vs `999f`) and exposes `GetDisplayStats()` (kills/time/win). `.../Scoring/WildlifeBlitzEndGameStatsTracker.cs`
- **IObjectiveProvider** — `TryGetObjective(out Transform)` supplies the objective-indicator target per mode. `Assets/_Scripts/UI/Interfaces/IObjectiveProvider.cs`
- **HexRaceObjectiveProvider** — nearest live `Crystal` in the local player's own domain; event-driven cache over `Crystal.Active` (no scene scans), profiled. `.../HexRaceObjectiveProvider.cs`
- **JoustObjectiveProvider** — nearest other player's vessel from `gameData.Players`. `.../JoustObjectiveProvider.cs`
- **AstroLeagueObjectiveProvider** — the `AstroLeagueBall` (cached, hidden while `IsHidden`). `.../AstroLeagueObjectiveProvider.cs`

### Interactions & patterns

- **How a turn ends.** `TurnMonitorController` polls its monitors each frame; the first `CheckForEndOfTurn()==true` raises `GameDataSO.InvokeGameTurnConditionsMet()` (→ `OnMiniGameTurnEnd`). Networked domain monitors gate `CheckForEndOfTurn` on `IsServer` and delegate the actual condition to `gameData.ScoringRule.IsObjectiveReached(out winner)` — a **per-domain** sum (`ScoringMetrics.SumByDomain`) reaching the mode's target, so human + AI teammates finish together. End-game counts come only from `EndConditionOverridesSO` (`Resources/EndConditionOverrides`, the Tools > End Game Conditions window), never per-scene `[SerializeField]`.
- **How scores/winner are computed & synced (current pipeline).** In the domain controllers (`MultiplayerDomainGamesController` → `HexRaceController` / `MultiplayerJoustController` / `MultiplayerCrystalCaptureController`), the server's `OnTurnEndedCustom` calls `rule.ResolveWinner` / `rule.IsObjectiveReached`, then `rule.AssignScores(gameData, winnerDomain, finishTime)`, `gameData.SortRoundStats` + `CalculateDomainStats`, and snapshots per-player `{Name,Score,Domain,metric}` arrays + `WinnerName`/`WinnerDomain` into a `[ClientRpc]` (`SyncFinalScores_ClientRpc` / `SyncJoustResults_ClientRpc`). Every client applies the snapshot, sets `WinnerName`/`WinnerDomain`, re-sorts, `gameData.SetResults(rule.BuildResults(gameData))`, then `InvokeWinnerCalculated()` + `InvokeMiniGameEnd()`. These modes set `HasEndGame=false` + override `SetupNewRound` (guarded by `_raceEnded`/`_finalResultsSent`) to suppress the base flow's duplicate end + Ready button; replay is a full network scene reload (`UseSceneReloadForReplay=true`). The **in-game** per-domain HUD boxes are kept exact across clients by `MultiplayerDomainGamesController`'s three `n_DomainSum{0,1,2}` server-write NetworkVariables (recomputed every 0.1s via `ScoringMetrics.SumByDomain`, mirrored into `gameData.SetDomainMetricSum`).
- **How scores sync (legacy pipeline).** `NetworkScoreTracker` (server) delays 500ms then `SendRoundStats_ClientRpc` so every peer runs `SortAndInvokeResults` (`SortRoundStats` + `CalculateDomainStats` + `InvokeWinnerCalculated`); `ScoreTracker` does the same locally offline.
- **SOAP channels.** Everything hangs off `GameDataSO` events: `OnInitializeGame`, `OnMiniGameTurnStarted`, `OnMiniGameTurnEnd`, `OnMiniGameEnd`, `OnResetForReplay`, plus `OnClickToMainMenu`; monitors emit `ScriptableEventString`/`ScriptableEventInt` display events; blitz uses static gameplay events (`LifeForm.OnLifeFormDeath`, `ElementalCrystalImpactor.OnCrystalCollected`).
- **Shared state on `GameDataSO`.** `ScoringRule`, `RoundStatsList`/`IRoundStats.Score`, `CrystalTargetCount`, `JoustTargetCount`, `GoalTargetCount`, `WinnerName`, `WinnerDomain`, `Results`, `ActiveDomains`/`RequestedDomainCount`, `SumCrystalsCollectedByDomain`, `SetDomainMetricSum` — the shared read model for HUD, scoreboard, reveal, comeback, and UGS reporting.
- **NetworkVariables.** `_netCrystalCollisions` (crystal target), joust collision-sync RPCs, `n_DomainSum{0..2}` (live domain HUD sums), `NetworkTimeBasedTurnMonitor` display RPC.
- **DI / lifecycle.** `[Inject] GameDataSO` + `[Inject] UGSStatsManager` throughout (Reflex); trackers/monitors are `NetworkBehaviour`s gating on `IsServer`; unsubscription is done off privately-tracked `_subscribedStats` lists (never `RoundStatsList`) plus `OnDestroy` safety nets to avoid leaking handlers onto persistent Player `RoundStats` across scene loads (BUGS.md B15). Golf modes rank ascending; loser scores are `GolfScoreSentinels` so DNF always sorts behind any real finish time.

---

## Prism Performance — Managers, Spatial Index, Assemblers, ECS

This area is the performance backbone of the prism/mass system — the game's most performance-critical gameplay surface, where a single cell can hold thousands of prisms, each nominally a GameObject with 5-6 MonoBehaviours + a BoxCollider + a MeshRenderer. It replaces the naive "one coroutine / one physics query / one collider per prism" model with three cross-cutting strategies: (1) **centralized batched managers** that pull per-prism animation/timer work off individual objects and drive it through Unity **Jobs + Burst** over cache-line-packed native arrays; (2) **`PrismSpatialIndex`**, THE canonical spatial index of all live prism mass (Burst AOE sphere scans, a bucket hash grid for neighbourhood queries, growth-occupancy reservations, and the coarse cell-density view), which supersedes `Physics.OverlapSphere`/`CheckBox` against prisms; and (3) **collider-LOD** that keeps only prisms near a vessel/projectile physically collidable. Layered on top are the **assemblers** (procedural crystalline growth — gyroid, herringbone wall, Schwarz-P minimal surface — that call `TryReserve` at the grow decision), the **density grid** (Burst mean-shift "densest region" search that feeds fauna targeting), the **theme/material** pipeline, and an installed-but-dormant **DOTS/ECS** component set staged for a future full migration.

### PrismSpatialIndex — the canonical prism spatial index
A `Singleton` holding a hot/cold split of every registered prism: a 16-byte `PrismSpatialData` array (position + packed flags, exactly 4 per cache line) scanned by Burst, an 8-byte `PrismDamageData` cold array read only for hit prisms on the main thread, a managed `Prism[]`, a managed `Cell[]` (coarse density-view binding), a `NativeParallelMultiHashMap` bucket grid, and a managed reservation dictionary. One registration lifecycle feeds four query views (AOE damage, occupancy/reservations, neighbourhood, cell density). File: `Controller/Managers/PrismSpatialIndex.cs`.

- **PrismSpatialIndex** — `Singleton<PrismSpatialIndex>`; owns registration (`Register`/`Unregister`/`MarkDestroyed`/`MarkRestored`/`UpdatePosition`/`UpdateShieldState`/`UpdateDomain`/`UpdateVolume`), occupancy (`TryReserve`, `IsPositionOccupied`, `ReleaseReservation`), neighbourhood queries (`QuerySphere`, `IsAnyPrismWithin`, `CopyLivePrisms`), cell-density forwarding (`ForwardDomainChangeToCell`), and per-frame AOE (`ProcessExplosionFrame`); auto-creates via `EnsureInstance()`; grows native arrays by power-of-two `EnsureCapacity`; disposes native collections in `OnDestroy`.
- **PrismFlags** — static bit-flag constants packed into one status byte (`IsActive`, `Destroyed`, `IsShielded`, `IsSuperShielded`) plus the Burst early-exit `JobSkipMask`/`JobPassValue`.
- **PrismSpatialData** — 16-byte HOT struct (`float3 Position` + `byte Flags` + 3 pad bytes); the only data the Burst job touches.
- **PrismDamageData** — 8-byte COLD struct (`float Volume` + `int Domain`); read on the main thread only for prisms that pass the spatial filter.
- **AOESpatialQueryJob** — `[BurstCompile] IJobParallelFor` that scans `PrismSpatialData`, does the single-byte active/not-destroyed check and a squared-distance test against the sphere, and appends hit indices to a `NativeList<int>.ParallelWriter`.
- **Reservation / bucket internals** — `TryReserve` claims a quantized site (`ReservationQuantum` 4m, `ReservationTtlSeconds` 5m safety TTL, lazy `PruneExpiredReservations`) at the grow decision to close the spawn race that physics can't (colliders disabled for `Prism.waitTime` ≈ 0.6s after spawn); `Register` consumes the matching reservation by proximity; `BucketKey`/`AddToBucket`/`RemoveFromBucket` maintain an 8m (`BucketSizeMeters`) hash grid, with `BucketWalkCostsMoreThanLinearScan` switching wide queries to a linear hot-array scan; `MAX_NEW_HITS_PER_FRAME` (48) spreads AOE damage across frames; `RegisterSynthetic`/`ClearAll` support the AOE benchmark; `ProfilerMarker`s `AOE.ProcessExplosion`/`AOE.BurstJob.Schedule`/`AOE.ResolveDamage`.
- **Cell-density view** — `BindCell`/`UnbindCell` file each prism into its containing `Cell`'s per-domain grids (fauna bodies bind as VOLUME-only mass, gated by a `HealthPrism`+`Fauna` check), keeping the coarse (`Cell.LiveVolume`/count) and fine (occupancy/AOE) views fed by one stream so they cannot diverge.

### PrismColliderLodManager — proximity collider LOD
A `Singleton` that culls prism BoxColliders far from anything that physically touches prisms (vessels, in-flight projectiles), so active-collider count is bounded by an LOD radius rather than the population; rides the same auto-created GameObject as `PrismSpatialIndex`. File: `Controller/Managers/PrismColliderLodManager.cs`.

- **PrismColliderLodManager** — `Singleton<PrismColliderLodManager>`; foci self-register via static `RegisterFocus`/`UnregisterFocus`; each `tickIntervalSeconds` (0.25s) `Sweep()` unions per-focus `PrismSpatialIndex.QuerySphere` neighbourhoods (`lodRadiusMeters`, min 50, default 200) and toggles every live prism's collider via `Prism.SetColliderCulledByLod`; restores all colliders when disabled or focus-less (never blanket-culls); exposes `LastNearCount`/`LastLiveCount` telemetry; `lodEnabled` master kill-switch.

### Batched animation managers (Jobs + Burst)
An abstract adaptive base plus three concrete managers move per-prism scale, material, and VFX animation off individual coroutines/UniTask loops into batched Burst jobs over `NativeArray` data, with a dynamic frame-skip that scales update cadence under load. Files under `Controller/Managers/`.

- **AdaptiveAnimationManager<TManager, TAnimator, TAnimationData>** — abstract `Singleton` base; tracks registered/active animator sets, owns the `NativeArray<TAnimationData>` (power-of-two `EnsureCapacity`), and drives dynamic frame-skipping (`BASE_FRAME_INTERVAL` 1 → `MAX_FRAME_INTERVAL` 12) off a 60-sample frame-time history and performance-pressure curve; `BATCH_SIZE` 128; abstract `ProcessAnimationFrame`/`IsAnimatorActive`/`IsAnimatorValid`; exposes `RegisteredAnimatorCount`/`ActiveAnimatorCount` for the perf benchmark. `Controller/Managers/AdaptiveAnimationManager.cs`.
- **PrismScaleManager** — batches prism grow/scale lerps; builds a job-input array aligned 1:1 with a `scalingAnimators` list, schedules `UpdateScalesJob`, applies results, and fires `ExecuteOnScaleComplete` on completed animators; `OnBlockStartScaling`/`OnBlockStopScaling` entry points. `Controller/Managers/PrismScaleManager.cs`.
  - **ScaleAnimationData** — job struct (currentScale/targetScale/growthRate).
  - **UpdateScalesJob** — `[BurstCompile] IJobParallelFor` clamped lerp toward target scale with squared-distance completion threshold.
- **MaterialStateManager** — batches prism material colour/spread transitions (`_BrightColor`/`_DarkColor`/`_Spread`) via one shared `MaterialPropertyBlock`; smoothstep-lerps bright/dark/spread and invokes each animator's `OnAnimationComplete`. `Controller/Managers/MaterialStateManager.cs`.
  - **MaterialAnimationData** — job struct (start/target bright & dark `float4`, start/target spread `float3`, progress/duration, `animatorIndex`).
  - **UpdateAnimationsJob** — `[BurstCompile] IJobParallelFor` advancing progress by `deltaTime/duration`.
- **PrismEffectsManager** — `Singleton` batching explosion/implosion VFX (replaces per-instance UniTask loops); registers `PrismExplosion`/`PrismImplosion`, schedules `UpdateExplosionsJob`/`UpdateImplosionsJob`, writes shader props (`_ExplosionAmount`/`_Opacity`/`_State`/`_Location`) through a shared MPB, refreshes moving implosion sinks (`RefreshConvergence`), and runs an editor/dev-only throttled `FindObjectsByType` "zombie VFX" safety audit; `ProfilerMarker`s `Prism.ProcessExplosions`/`Prism.ProcessImplosions`. `Controller/Managers/PrismEffectsManager.cs`.
  - **ExplosionJobData / ImplosionJobData** — VFX job structs (position/velocity/elapsed/duration; implosion adds grow-delay + progress + completion flag).
  - **UpdateExplosionsJob / UpdateImplosionsJob** — `[BurstCompile] IJobParallelFor` position/opacity and grow/implode progress integration.
- **PrismTimerManager** — `Singleton` replacing per-prism shield coroutines with one flat timer list checked each `Update`; `ScheduleShieldDeactivation`/`CancelTimers`, `EnsureInstance()` auto-create; documented as the stepping stone toward the ECS `ShieldTimer`. Internal `TimerAction` enum + `TimerEntry` struct. `Controller/Managers/PrismTimerManager.cs`.

### Prism per-instance state, team, theming & factory
Per-prism `MonoBehaviour`s that resolve their config from a shared `ThemeManagerDataContainerSO`, drive shield/danger state (delegating timing to `PrismTimerManager` and syncing flags into `PrismSpatialIndex`), handle domain "steal", and spawn prisms/VFX from pools.

- **PrismStateManager** — per-prism `MonoBehaviour`; owns `BlockState` (Normal/Shielded/SuperShielded/Dangerous); `MakeDangerous`/`ActivateShield(duration?)`/`ActivateSuperShield`/`DeactivateShields`; auto-adds a `PrismOctahedronShield` in `Awake` and engages/disengages it; swaps team materials via the theme container; `SyncAOERegistryShieldState()` pushes flag changes into `PrismSpatialIndex.UpdateShieldState`; plays `GameplaySFXCategory.ShieldActivate/Deactivate` SOAP SFX. Defines the **BlockState** enum. `Controller/Managers/PrismStateManager.cs`.
- **PrismTeamManager** — per-prism `MonoBehaviour` tracking `Domain` (default `Domains.Blue`); `SetInitialTeam`/`ChangeTeam`/`Steal(playerName, domain, superSteal)` (respects super-shield invulnerability and shield decay); re-themes on team change; raises the `ScriptableEventPrismStats onPrismStolen` SOAP event; exposes `event Action<Domains,Domains> OnTeamChanged`. `Controller/Managers/PrismTeamManager.cs`.
- **ThemeManager** — scene `MonoBehaviour`; in `Awake` clones the base `SO_MaterialSet` into per-domain (Jade/Ruby/Gold/Blue) material sets from the `SO_ColorSet`, populates `ThemeManagerDataContainerSO.TeamMaterialSets`, and hands the color set to the static `GameFeedAPI.ColorSet`. `Controller/Managers/ThemeManager.cs`.
- **ThemeManagerDataContainerSO** — `[CreateAssetMenu]` `ScriptableObject` data container holding `BaseMaterialSet` + `SO_ColorSet` and the runtime `Dictionary<Domains, SO_MaterialSet>`; accessor family `GetTeam{Block,TransparentBlock,Crystal,Spike,Shielded,TransparentShielded,Dangerous,TransparentDangerous,SuperShielded,TransparentSuperShielded}Material(...)`, `GetDomainUIColor`, `SetBackgroundColor`. `Controller/Managers/ThemeManagerDataContainerSO.cs`.
- **PrismFactory** — pool-driven spawner `MonoBehaviour`; subscribes to the `PrismEventChannelWithReturnSO` request/response SOAP channel and dispatches on **PrismType** (Dolphin/Serpent/Sparrow/Manta/Squirrel/Rhino/Interactive/Explosion/Implosion/Grow) to per-vessel `InteractivePrismPoolManager`s and the `PrismExplosion`/`PrismImplosion` pools; enforces per-frame VFX spawn caps (`MaxExplosion/ImplosionVFXPerFrame` 64); tints via `MaterialPropertyBlock`; self-unsubscribing grow callback. Defines the **PrismType** enum. `Controller/Prisms/PrismFactory.cs`.

### Density grid — Burst "densest region" (fauna targeting)
A plain C# per-cell, per-domain grid of block counts, searched by a Burst job for the densest region so fauna swarms steer toward remaining mass; the grid is fed by the spatial index's cell-density view. File: `Controller/Managers/BlockDensityGrid.cs`.

- **BlockDensityGrid** — base grid: sizes resolution per cell from physical constants (`SmoothingRadiusMeters` 150, `TargetVoxelSizeMeters` 75, clamped 9–33 points/axis), owns the `NativeArray<ushort>` counts + two float scratch buffers + result arrays, caches the last answer (dirty flag + `MinRecomputeIntervalSeconds` 0.25s staleness bound), and runs the job in `FindDensestRegion()`; `GetDensityAtPosition`, `LastResultDensity`, coordinate↔index mapping, `Init`/`Dispose`.
- **FindDensestRegionJob** — `[BurstCompile] IJob`: separable 3D box filter → argmax → sub-voxel parabolic interpolation → mean-shift refinement (`MeanShiftIterations` 5) over raw counts so the target tracks surviving mass as fauna hollow out a cluster's core.
- **BlockCountDensityGrid** — concrete grid overriding `AddBlock`/`RemoveBlock` with saturating/underflow-guarded `ushort` voxel counts and dirty-marking.
- **BlockVolumeDensityGrid** — declared volume-weighted variant (currently an empty `BlockDensityGrid` subclass).

### Assemblers — procedural crystalline growth
Prism-hosted `MonoBehaviour`s that grow lattices one prism at a time, deciding each new site through `PrismSpatialIndex.TryReserve` (claim-before-spawn, replacing the collider-blind `Physics.CheckBox`) and recruiting/steering neighbouring prisms via `QuerySphere`. Files under `Controller/Assemblers/`.

- **Assembler** — abstract base `MonoBehaviour`: `Prism`/`Spindle`/`Depth`, `IsFullyBonded()`, `GetGrowthInfo()`, `SeedBonding()`/`StartBonding()`/`StopBonding()`. `Assembler.cs`.
- **GyroidAssembler** — grows the gyroid lattice from a baked bond-mate table; four corner bond sites, mate search via `QuerySphere` (registered prisms) + a "Mound"-layer `OverlapSphereNonAlloc` (unregistered mound blocks), `ConvertBlock` recruits/steals candidates, `TryReserve`-gated `GetGrowthInfo`, and `NotifyPositionChanged` on steered blocks to keep the index honest; defines **GyroidGrowthInfo** (`GrowthInfo` subclass carrying `BlockType`) and the **GyroidBlockType** enum (12 tile types AB…EsD). `GyroidAssembler.cs`.
- **GyroidBondMate** — runtime struct: a mate `GyroidAssembler` + substrate/bondee corner sites + delta pose + block type + tail flag. `GyroidBondMate.cs`.
- **GyroidBondMateData** — baked-table struct (same fields minus the live mate reference). `GyroidBondMateData.cs`.
- **GyroidBondMateDataContainer** — static class holding `BondMateDataMap`, the `(GyroidBlockType, CornerSiteType) → GyroidBondMateData` lookup of the gyroid's non-Euclidean tile geometry (per-corner delta position/up/forward). `GyroidBondMateDataContainer.cs`.
- **CornerSiteType** — enum (TopLeft/TopRight/BottomLeft/BottomRight/None) naming the four bond corners. `CornerSiteType.cs`.
- **WallAssembler** — grows a herringbone wall; four `SiteType` sites, own/opponent pull & rotate speeds, `QuerySphere`-based mate search with `colliderTheshold` gating, snap/shield/danger/steal on bond, `TryReserve`-gated `GetGrowthInfo`, `NotifyPositionChanged` on steered mates, and recursive `StopAssembly`; defines the nested **BondMate** struct and **SiteType** enum. `WallAssembler.cs`.
- **SchwarzPAssembler** — grows the Schwarz-P minimal surface (`cos x+cos y+cos z=0`) analytically: each tile's four tangent-plane neighbours are computed on the fly (step → Newton-project onto the zero level set → orient to the gradient), with a shared per-flora **SchwarzPSurfaceFrame** doing param↔world mapping + weak (alive-only) occupancy, and cross-structure occupancy via `TryReserve`; defines **SchwarzPGrowthInfo** (`GrowthInfo` subclass carrying frame/param position/heading). `SchwarzPAssembler.cs`.

### DOTS / ECS component scaffolding (dormant)
Installed-but-not-yet-active DOTS `IComponentData` mirrors of the MonoBehaviour prism systems, staged for the future migration from "GameObject + 5-6 MonoBehaviours" to "entity + components." File: `Controller/ECS/Components/PrismComponents.cs`.

- **PrismData** — core prism state (transform, target scale, growth rate, domain, volume, time-created, bit-flags) mirroring `Prism` + `PrismProperties`.
- **ScaleAnimation** — `IEnableableComponent` scale-animation state (replaces `PrismScaleAnimator` + `PrismScaleManager`).
- **MaterialAnimation** — `IEnableableComponent` colour/spread transition state (replaces `MaterialPropertyAnimator` + `MaterialStateManager`).
- **ShieldTimer** — `IEnableableComponent` shield-deactivation timer (replaces `PrismTimerManager` coroutine timing).
- **ExplosionEffect / ImplosionEffect** — VFX entity state (replace `PrismExplosion`/`PrismImplosion` + `PrismEffectsManager`).

### Other managers in `Controller/Managers` (non-prism)
Several unrelated managers live in the same folder and are in scope; they do not touch the prism pipeline.

- **CameraManager** — `Singleton` orchestrating player/death/end/main-menu cameras (Cinemachine + `CustomCameraController`); reacts to `_onReturnToMainMenu`/`_onInitializePlayerCamera` SOAP events and `DisplayGraphicsSettings` FOV/AA changes; `SetupGamePlayCameras`/`SetupEndCameraFollow`/`SnapPlayerCameraToTarget`/camera-activation API. `Controller/Managers/CameraManager.cs`.
- **PostProcessingManager** — `Singleton` swapping a URP `Volume`'s orthographic/perspective `VolumeProfile`. `Controller/Managers/PostProcessingManager.cs`.
- **StatsManager** — server-gated (`NetcodeHooks`) round-stats recorder writing into `GameDataSO` round stats (`CrystalCollected`, `Prism{Created,Destroyed,Restored,Stolen,VolumeModified}`, `ExecuteJoust/SkimmerShipCollision`, `RegisterAbilityExecuted`, `Lifeform{Created,Destroyed}` into `CellRuntimeDataSO`); defines the **CellStats**, **CrystalStats**, **PrismStats**, **AbilityStats** structs. `Controller/Managers/StatsManager.cs`.
- **Arcade** — `SingletonPersistent` (namespace `CosmicShore.Core`) game-launch entry point; builds mode→SO lookups and `LaunchMission`/`LaunchArcadeGame`/`LaunchTrainingGame` configure `GameDataSO` and raise `InvokeGameLaunch()` (much legacy logic commented out). `Controller/Managers/Arcade.cs`.
- **Elements** — static registry loading `SO_Element` assets from `Resources/Element SOs` with `Get(Element)`. `Controller/Managers/Elements.cs`.
- **Hangar** — `Singleton` marked DEPRECATED; near-empty, retains `LocalPlayerVessel` and stubbed setters (most body commented out). `Controller/Managers/Hangar.cs`.

### Interactions & patterns
- **Single canonical spatial store.** `PrismSpatialIndex` is fed by `Prism`'s lifecycle (`Register`/`MarkDestroyed`/`MarkRestored`/`Unregister`/`UpdatePosition`) and read by AOE damage (`ExplosionImpactor` → `ProcessExplosionFrame`), collider-LOD (`PrismColliderLodManager` via `QuerySphere`/`CopyLivePrisms`), assembler mate-finding + occupancy (`QuerySphere` + `TryReserve`), fauna senses, and the per-cell `BlockDensityGrid`s (via `BindCell`/`Cell.AddBlock`). New spatial queries against prisms must add a view here, never use `Physics.OverlapSphere`/`CheckBox` (physics is structurally blind to prisms during the 0.6s post-spawn collider-disabled window).
- **Jobs + Burst everywhere hot.** Five Burst jobs (`AOESpatialQueryJob`, `UpdateScalesJob`, `UpdateAnimationsJob`, `UpdateExplosionsJob`/`UpdateImplosionsJob`, `FindDensestRegionJob`) operate on `NativeArray`s with hot/cold cache-line-aware layouts; the `AdaptiveAnimationManager` base adds dynamic frame-skip (1×–12×) under load; all native collections are `Allocator.Persistent` with explicit disposal.
- **TryReserve claim-before-spawn** closes the concurrent-growth race that colliders couldn't: assemblers claim a site synchronously at the grow decision; the claim is consumed when the spawned prism registers (by proximity) or lapses after a 5s TTL.
- **SOAP channels & NetworkVariables.** `PrismFactory` uses the `PrismEventChannelWithReturnSO` request/return channel; `PrismTeamManager` raises `ScriptableEventPrismStats onPrismStolen`; `PrismStateManager` raises `GameplaySFXCategory` audio events; `StatsManager` writes `GameDataSO`/`CellRuntimeDataSO` and gates on `NetcodeHooks.IsServer`; `CameraManager` consumes `ScriptableEventNoParam`/`ScriptableEventTransform` events. Shielded/domain state changes are mirrored into the spatial index flags (`UpdateShieldState`/`UpdateDomain`) so AOE and occupancy stay consistent.
- **Config separation & theming.** All per-domain look lives in `ThemeManagerDataContainerSO` (built once by `ThemeManager`); every prism state/team component reads material sets from that one asset, so appearance can't drift between prefabs.
- **DOTS on-ramp.** `PrismComponents` mirrors the MonoBehaviour managers 1:1 (`ScaleAnimation`↔scale manager, `MaterialAnimation`↔material manager, `ShieldTimer`↔timer manager, `Explosion/ImplosionEffect`↔effects manager), documenting the intended incremental migration target.

---

## Multiplayer, Player & Party/Presence Netcode

This area is the networking spine of Cosmic Shore: it spawns and initializes networked `Player` + vessel pairs, keeps team (Domain) identity replicated and consistent across every peer, and implements the eager-Relay party/presence social layer that lets players discover each other in `Menu_Main` and fly together. Everything runs on Unity Netcode for GameObjects (2.5.0) over UGS Multiplayer sessions with Relay transport, coordinated through SOAP events on `GameDataSO` / `HostConnectionDataSO` and Reflex DI. The design is **multiplayer-first**: there is no offline single-player path — every session is a Relay host (solo or party), so menu autopilot vessels spawn through the exact same Netcode pipeline as gameplay vessels. All types live in `namespace CosmicShore.Gameplay`. (Note: `DomainAssigner` and `NetworkStatsManager` are named in CLAUDE.md but no longer exist as files — domain balancing now lives in `ServerPlayerVesselInitializerWithAI.GetBalancedDomain`, and network health monitoring lives in `System/NetworkMonitor.cs`, outside this scope.)

### Player core & identity (`Controller/Player/`)

The `Player` NetworkBehaviour is the persistent per-client actor (survives Netcode scene loads, `DestroyWithScene=false`); its six NetworkVariables are the replication surface for vessel selection, team, name, avatar, AI flag, and linked-vessel id. It implements `IPlayer` and owns the local input/round-stats components.

- **Player** — `NetworkBehaviour, IPlayer`; the networked player object. `Controller/Player/Player.cs`. NetworkVariables: `NetDefaultVesselType` (VesselClassType, Owner-write), `NetDomain` (Domains, **Server-write** — the single authoritative team source), `NetName` (FixedString128, Owner-write, 3-tier fallback), `NetVesselId` (ulong, Server-write), `NetIsAI` (bool, Server-write), `NetAvatarId` (int, Owner-write). Key API: `RequestSetDomain_ServerRpc` (validated against `GameDataSO.IsActiveDomain`/`RequestedDomainCount`), `SetDomain`, `InitializeForSinglePlayerMode`/`InitializeForMultiplayerMode`, `PrepareForNewScene` (re-inits persistent Player for a new scene + purges stale `RoundStats` subscriptions), `StartPlayer`/`ResetForPlay`/`DestroyPlayer`, `ChangeVessel`. Raises `gameData.OnPlayerNetworkSpawnedUlong` (immediately on clients; server defers until name+vessel-type replicate via `TryRaiseDeferredSpawnEvent`). `OnNetDomainChanged` mirrors domain to local `Domain` + `RoundStats.Domain` on every peer and repaints the vessel via `ShipHelper.SetShipProperties` (using stashed `_vesselThemeManagerData`). Identity resolution order: `PlayerDataService.CurrentProfile` → `GameDataSO.LocalPlayerDisplayName` → UGS `PlayerName` (suffix-stripped). Ownership predicates: `IsLocalUser`/`IsMultiplayerOwner` (`IsOwner && !IsInitializedAsAI`), `IsNetworkClient`.
- **IPlayer** — player abstraction (extends `ITransform`); exposes Domain/Name/AvatarId/Vessel/InputController/InputStatus/RoundStats + ownership flags, plus the nested serializable `IPlayer.InitializeData` (vesselClass, PlayerName, AvatarId, IsAI, AllowSpawning). `Controller/Player/IPlayer.cs`.
- **PlayerSpawner** — MonoBehaviour; single-player (non-networked) spawn of player prefab + vessel via `VesselSpawner`, DI-injects both, calls `InitializeForSinglePlayerMode`. `Controller/Player/PlayerSpawner.cs`.
- **PlayerSpawnerAdapterBase** — abstract base for single-player adapters; `[Inject] GameDataSO`, holds `PlayerSpawner` + `InitializeData[]` + spawn transforms; `AddSpawnPosesToGameData`, `SpawnDefaultPlayersAndAddToGameData`, `SpawnCustomPlayerAndAddToGameData`. `Controller/Player/PlayerSpawnerAdapterBase.cs`.
- **MiniGamePlayerSpawnerAdapter** — subclass; on `GameDataSO.OnInitializeGame` spawns the human (resolves name/avatar from `PlayerDataService`) then default AI. `Controller/Player/MiniGamePlayerSpawnerAdapter.cs`.
- **VolumeTestPlayerSpawnerAdapter** — minimal subclass for the volume-test scene (init game → add spawn poses → spawn defaults → set active). `Controller/Player/VolumeTestPlayerSpawnerAdapter.cs`.
- **VesselSpawner** — MonoBehaviour (in `Controller/Vessel/`, but the single-player spawn dependency); resolves `Random`/`Any` to a concrete class, instantiates from `VesselPrefabContainer`, `GameObjectInjector.InjectRecursive`, returns `IVessel`. `Controller/Vessel/VesselSpawner.cs`.

### Server-side vessel spawn pipeline (`Controller/Multiplayer/`)

The server owns vessel spawning: it listens for `Player` spawn SOAP events, waits for NetworkVariables to replicate, instantiates + DI-injects + spawns the vessel NetworkObject with client ownership, then delegates pair init and notifies clients via RPC. Uses `NetcodeHooks` (composition) rather than direct `NetworkBehaviour` inheritance.

- **ServerPlayerVesselInitializer** — `MonoBehaviour` + `[RequireComponent(NetcodeHooks)]`; base server spawner. `Controller/Multiplayer/ServerPlayerVesselInitializer.cs`. `[Inject] GameDataSO`, `Container`; serialized `ClientPlayerVesselInitializer`, `VesselPrefabContainer` (exposed read-only), spawn points, `preSpawnDelayMs`/`postSpawnDelayMs` (200ms each), virtual `DestroyVesselWithScene` (true). Tracks processed players by `NetworkObjectId` (AI share the host's OwnerClientId). Subscribes to `OnPlayerNetworkSpawnedUlong`; `ProcessPreExistingPlayers` catches Players spawned before it loaded (host Player from Auth scene) and persistent Players from `NetworkManager.ConnectedClients`. `HandlePlayerNetworkSpawnedAsync` waits/retries for readiness then calls virtual `OnPlayerReadyToSpawnAsync` → `SpawnVesselForPlayer` (`SpawnWithOwnership`) → `clientPlayerVesselInitializer.InitializePlayerAndVessel` → `NotifyClients`. Implements the client-pull `HandleRosterRequest`/`SendFullRosterToClient`. Never shuts down the NetworkManager on despawn (eager-Relay persists).
- **ServerPlayerVesselInitializerWithAI** — extends the base to pre-spawn server-owned AI. `Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs`. `[Inject] SO_GameList`, `TournamentDataSO`; serialized `aiPlayerPrefab`, `aiInitializeDatas`, `SO_AIProfileList`. `SpawnAIs` runs **before** `base.OnNetworkSpawn` subscribes (so AI spawn events are harmlessly missed), spawns AI players+vessels with `destroyWithScene:false` (survives the client scene-sync batch destroy), marks them processed, picks vessel via `PickAIVesselType`/`SO_GameList`, seeds/reuses `TournamentDataSO.TournamentAINames`, configures `AIPilot.ConfigureForGameMode` (skill = intensity×0.25, seek-players in Joust). Static `GetBalancedDomain` (deterministic 3-tier tie-break: lowest total → fewest humans → enum order), `BuildActiveDomains` (contiguous `ActiveDomains[0..DC-1]` slice), `NormalizeUnassignedHumans` (reassigns humans off the active set).
- **ClientPlayerVesselInitializer** — `NetworkBehaviour`; common player-vessel pair init on both server and client. `Controller/Multiplayer/ClientPlayerVesselInitializer.cs`. Serialized `ThemeManagerDataContainerSO`; `[Inject] GameDataSO`, `Container`. Server path: `InitializePlayerAndVessel` (direct). Client path RPCs: `InitializeAllPlayersAndVessels_ClientRpc` (new client, all pairs, fires ClientReady), `InitializeNewPlayerAndVessel_ClientRpc` (existing clients, one pair), `ReplaceVesselForPlayer_ClientRpc` (swap). Queues unresolved pairs; resolves reactively on `OnPlayerNetworkSpawnedUlong` + `OnVesselNetworkSpawned` (zero WaitUntil). `InitializePair` → `player.InitializeForMultiplayerMode`, `vessel.Initialize`, `ShipHelper.SetShipProperties`, stashes theme on Player, `gameData.AddPlayer`, `InvokePlayerPairInitialized`, `InvokeClientReady` (local). `ReInitializePair` handles swaps (re-syncs `Player.Domain` from `NetDomain` so the swapped hull keeps the chosen color). Client-pull "unbreakable join": `RequestRosterFromHost_ServerRpc` + `RosterPullRetryLoop` (4 attempts × 1.5s) heal dropped one-shot pushes; `RequestVesselSwap_ServerRpc` forwards swaps; `ReRegisterPersistentPlayers` re-adds scene-surviving Players. Server-side callbacks `OnSwapRequested`/`OnRosterRequested`.
- **NetcodeHooks** — referenced adapter that surfaces `OnNetworkSpawnHook`/`OnNetworkDespawnHook` so the spawners can be plain MonoBehaviours (in `Utility/Network/`, outside scope).

### Menu multiplayer, freestyle & arcade config (`Controller/Multiplayer/`)

The menu reuses the same spawn pipeline with an autopilot overlay, a network-authoritative domain reset, and runtime vessel swaps; a separate NetworkBehaviour relays the arcade configure modal between host and clients.

- **MenuServerPlayerVesselInitializer** — extends `ServerPlayerVesselInitializer` for `Menu_Main`. `Controller/Multiplayer/MenuServerPlayerVesselInitializer.cs`. Serialized `menuVesselDomain` (Jade); `DestroyVesselWithScene=false`. `OnPlayerReadyToSpawnAsync` override: resets a human's `NetDomain` to Jade server-side **before** base spawns/paints (the ONLY menu domain reset), then `ActivateAutopilot` (`StartPlayer`, `ToggleAIPilot(true)`, `SetPause(true)`). Owns runtime vessel swaps: `RequestSwap` (host-direct or client `RequestVesselSwap_ServerRpc`), `SwapVesselAsync` (snapshot speed → despawn old → spawn new → `ReplaceVesselForPlayer` → `SetPose`/`SetInitialSpeed` → reactivate autopilot → `NotifyClientsOfSwap` via `ReplaceVesselForPlayer_ClientRpc`), `IsSwapping` guard.
- **MenuCrystalClickHandler** — MonoBehaviour toggling menu (autopilot + menu UI) ↔ freestyle (player control + game UI) for `GameDataSO.LocalPlayer` only. `Controller/Multiplayer/MenuCrystalClickHandler.cs`. `[Inject] MenuFreestyleEventsContainerSO`; serialized menu/freestyle `CanvasGroup[]`, fade/camera durations, `MainMenuCameraController`, `lockInputDuringEnterTransition`. `ToggleTransition` → `TransitionToFreestyle`/`TransitionToMenu` raising the SOAP transition-bracket events; `IsMultiplayerSession` guard skips `Time.timeScale` changes when remote clients exist (would freeze other vessels); `IsInFreestyle` state; parallel UI-fade + camera-blend.
- **MenuVesselSelectionPanelController** — network-aware vessel picker for freestyle. `Controller/Multiplayer/MenuVesselSelectionPanelController.cs`. Reuses `VesselSelectionPanelUI`/`ShipCardView`; delegates the swap to `MenuServerPlayerVesselInitializer.RequestSwap`; `Open` autopilots the vessel while browsing; `OnResumeButtonClicked` requests the swap and `RestoreFreestyleAfterSwapAsync` (polls `IsSwapping`, `restoreFreestyleDelayMs=600`); routes domain picks through `DomainSelectionPanel.OnDomainSelected` → `Player.RequestSetDomain_ServerRpc`; auto-closes on `OnMenuStateTransitionStart`.
- **ArcadeConfigSyncManager** — `NetworkBehaviour` relaying the arcade-configure modal host→clients. `Controller/Multiplayer/ArcadeConfigSyncManager.cs`. `[Inject] GameDataSO`, `SO_GameList`. `CommitConfiguration` (server, single-shot `_isCommitted` guard) writes `gameData.RequestedDomainCount`, resets every human's `NetDomain` to Jade, and broadcasts `OpenConfigOnClients_ClientRpc`. Ready-up system: `ConfirmLocalPlayerReady` → `ConfirmReady_ServerRpc` → `HandlePlayerReady` counts ready clients, `SyncReadyCount_ClientRpc`/`AllPlayersReady_ClientRpc`. C# events (`OnConfigOpenedOnClient`, `OnConfigClosedOnClient`, `OnPlayerReadyCountChanged`, `OnAllPlayersReady`, `OnScreenChangedOnClient`) drive the modal; `NotifyScreenChanged`/`ChangeScreenOnClients_ClientRpc` keep screen navigation in sync; `FindGameByMode` helper.

### NetworkManager & UGS session lifecycle (`Controller/Multiplayer/MultiplayerSetup.cs`)

Bridges authentication → Netcode host lifecycle and matchmaking. Runs on a scene GameObject; the NetworkManager itself lives in Bootstrap (DontDestroyOnLoad).

- **MultiplayerSetup** — MonoBehaviour. `[Inject] GameDataSO`, `AuthenticationDataVariable`. On `OnSignedIn` (or already-signed-in) → `EnsureHostStarted` (wires `ConnectionApprovalCallback`/`OnClientDisconnectCallback`/`OnTransportFailure`, `_hostStartInProgress` guard, MPPM per-clone port assignment; actual Relay host start is delegated to `HostConnectionService`). For multiplayer games, `ExecuteMultiplayerSetup`: reuse handed-off `gameData.ActiveSession` if present, else shut down local host and `QuerySessions` (filtered by GameMode `String1` + maxPlayers `String2`) → `TryJoinFirstAvailable`/`JoinSessionByIdAsync` or `StartSessionAsHost` (`CreateSessionAsync().WithRelayNetwork()`), all with rate-limit (429) exponential-backoff retries. `OnConnectionApprovalCallback` auto-creates player objects. `OnClientDisconnect`: host reconciles roster via `HostConnectionService.ReconcilePartyMembersNow`; client routes host-loss to `PartyInviteController.HandleHostLossAsync`. `OnTransportFailure` → same self-rescue. `GetPlayerProperties`/`GetSessionProperties` build the UGS property dicts.

### Party lifecycle facade & state machine (`Controller/Party/`)

`HostConnectionService` is the single-writer facade over the whole party lifecycle — presence lobby, Relay party session, invite payloads, and member list — delegating mechanics to injected services and exposing state through a validated state machine and `HostConnectionDataSO` SOAP events. **Eager-Relay is the locked design**: every authenticated player creates their own solo Relay-backed party session on entering `Menu_Main` (`EnsureInitializedAsync` → `EnsurePartySessionAsync`).

- **HostConnectionService** — `MonoBehaviour, IPartyStateQuery`; DontDestroyOnLoad singleton (`Instance`), single writer to `HostConnectionDataSO`. `Controller/Party/HostConnectionService.cs` (~2078 lines). Serialized `AuthenticationDataVariable`, `HostConnectionDataSO`, `ScriptableEventNoParam bootStatusRetryRequestedEvent`, `presenceLobbyMaxPlayers=100`, `refreshIntervalSeconds=1.5`. `[Inject]` all extracted services + `PlayerDataService` + `GameDataSO`. Public API: `SendInviteAsync` (writes `invite_payloads` with the real session id), `CancelInviteAsync`, `AcceptInviteAsync` (three-phase: publish acceptance signal → leave own Relay → `JoinByIdAsync` the inviter's), `DeclineInviteAsync`, `LeavePartyAsync`, `KickPartyMemberAsync`, `ForceRefreshNow`, `ReconcilePartyMembersNow`, `EnsurePartySessionAsync` (idempotent create-or-no-op; `IsHostingParty` fast-path + `SessionCreationMutex` double-check), `LeavePartySessionAsync`, `ClearPartySessionRef`, `ClearJoinedPartyAsync`. `Update` drives the refresh loop (`_scheduler.ShouldFireNow` → `RefreshAsync`) gated on lobby-mutex, rate-limit backoff, and menu-scene. `RefreshAsync` diffs online players, detects incoming invites (`TryFindIncomingInvite`/`TryRaiseIncomingInvite`, `_lastFiredInvite` dedup), scans for acceptance signals, syncs party members, expires outgoing invites, and periodically converges the presence lobby. `IPartyStateQuery`: `CurrentState`, `ActivePartySessionId`, `PartyMemberCount`; plus `LastPendingInvite`, `PartySession`, `StateMachine`, `OutgoingInviteTargets`. Internal helper `IsBenignLobbyPatcherError` (reflected by tests) swallows benign SDK `ArgumentOutOfRangeException` from `LobbyPatcher`.
- **PartyStateMachine** — pure C# single-writer state validator; static `LegalTransitions` table, `TryTransition` (→`Disconnected` always allowed), `OnStateChanged`, `IsIn`. `Controller/Party/StateMachine/PartyStateMachine.cs`.
- **PartyState** — enum of the 7 lifecycle phases: `Disconnected`, `InPresenceLobby` (transient), `Inviting`, `JoiningParty`, `HostingParty` (transient), `InParty` (baseline), `Reconnecting`. `Controller/Party/StateMachine/PartyState.cs`.
- **IPartyStateQuery** — read-only view (`CurrentState`, `ActivePartySessionId`, `PartyMemberCount`) used by `FriendsInitializer`/UI to avoid coupling to the concrete service. `Controller/Party/Interfaces/IPartyStateQuery.cs`.

### Party invite orchestration & presence (`Controller/Party/`)

Thin orchestrators sequence the Netcode host↔client handoffs and bridge presence into the Friends system; the low-level Netcode mechanics are delegated to `NetworkTransitionService`.

- **PartyInviteController** — DontDestroyOnLoad singleton MonoBehaviour; sequences accept/decline/leave/recovery. `Controller/Party/PartyInviteController.cs`. Serialized `HostConnectionDataSO`, `ToastChannel`, timeouts; `[Inject] GameDataSO`, `SceneTransitionManager`, `SceneLoader`, `SceneNameListSO`, `INetworkTransitionService`. `AcceptInviteAsync` (shutdown NM → clear stale refs → `HostConnectionService.AcceptInviteAsync` → wait client-connect → gate on `OnClientReady` via `WaitForClientReadyAsync` → raise `OnPartyJoinCompleted`, else `BounceToSoloMenuAsync`), `DeclineInviteAsync`, `LeavePartyAndReturnToMenuAsync` (mirror cold-boot: destroy vessel → leave session → shutdown NM → reload Menu_Main → `EnsurePartySessionAsync`), `HandleHostLossAsync` (host-loss/transport-failure self-rescue, `_transitioning` idempotency — field reflected by tests), `TransitionToPartyHostAsync` (now a no-op under eager-Relay), `RecoverFromFailedTransitionAsync`. All awaits of UGS/Netcode Tasks use `.AsMainThread()`; failures log `NetworkDiagnostics.ClassifyException`.
- **FriendsInitializer** — MonoBehaviour bridge; initializes `FriendsServiceFacade` on `OnSignedIn`, manages presence across scene transitions. `Controller/Party/FriendsInitializer.cs`. Serialized `AuthenticationDataVariable`, `FriendsDataSO`, `HostConnectionDataSO`; `[Inject] FriendsServiceFacade`; reads `IPartyStateQuery` (= `HostConnectionService.Instance`). `SetPresenceInMenu`/`SetPresenceInParty`/`SetPresenceInGame`/`SetPresenceOffline`; wires `OnPartyMemberJoined`/`OnPartyMemberLeft` to flip presence between "In Menu" and "In Party".

### Party services & interfaces (`Controller/Party/Services/`, `Controller/Party/Interfaces/`)

`HostConnectionService` was decomposed (Phases 3–12) into single-responsibility pure-C# services (DI-registered, main-thread only), each behind an interface where a seam matters. None may reach outside its lane (e.g. lobby services never touch NetworkManager, session service never touches NetworkManager).

- **PresenceLobbyService** / **IPresenceLobbyService** — the lobby-only UGS discovery session (no Relay, ≤100 players). Join-or-create with simultaneous-create race healing (`ConvergeToCanonicalAsync` picks the lexicographically-smallest session id), refresh, `SavePropertiesAsync`, `ForceReset`, `BuildLocalPlayerProperties` (8 keys always present). Resolves `MultiplayerService.Instance` at use-time (never cached in ctor). `Services/PresenceLobbyService.cs`, `Interfaces/IPresenceLobbyService.cs`.
- **PartySessionService** / **IPartySessionService** — the Relay-backed party session (`ActiveSession` backed by `GameDataSO.ActiveSession`). `CreateAsync`/`JoinByIdAsync` (host-conflict + rate-limit + transient-SessionException retries), `LeaveAsync`, `RefreshAsync`, `ClearSession`, `PlayerLeaving` re-broadcast, `CreatedAtUnscaledTime` grace timestamp. Never touches NetworkManager. `Services/PartySessionService.cs`, `Interfaces/IPartySessionService.cs`.
- **NetworkTransitionService** / **INetworkTransitionService** — NetworkManager lifecycle for transitions: `ShutdownAsync` (strong `IsFullyReset` gate), `WaitForClientConnectionAsync`, `WaitForSceneSyncAsync` (Single-mode load), `ClearStaleReferences` (`GameDataSO.ResetRuntimeDataForPartyJoin`). Fail-soft (log + return false on timeout) + diagnostics. `Services/NetworkTransitionService.cs`, `Interfaces/INetworkTransitionService.cs`.
- **InviteService** / **IInviteService** — outgoing invite payload build/track/serialize/parse. Payload format `targetId|senderId|sessionId|displayName|avatarId`, `\n`-joined; `PENDING` sentinel + `UpdatePayloadsWithRealSessionId`; `AddOrRefresh`/`Remove`/`RefreshTimeout`/`SerializeAll`/`RemoveExpired`; internal static `ParseLine`. `Services/InviteService.cs`, `Interfaces/IInviteService.cs`.
- **AcceptanceSignalService** — stateless orchestrator of the PENDING-sentinel three-phase acceptance handshake done entirely via lobby player-properties: `ScanForSignals` (host detects `accepted_invite`), `RepublishWithRealIdAsync` (host swaps PENDING→real id), `PublishSignalAsync` (recipient writes acceptance), `WaitForRealSessionIdAsync` (recipient polls, ~400ms, 7s budget). `Services/AcceptanceSignalService.cs` (no interface).
- **PartyMemberService** / **IPartyMemberService** — owns the `HostConnectionDataSO.PartyMembers` SOAP list: `SeedLocalPlayer`, `SyncFromSession` (diff → `OnPartyMemberJoined`/`OnPartyMemberLeft` via the event bus), `ClearSilent`, `ClearWithEvents`, `ReadMemberData`. Reads `ISession.Players` only; never calls UGS SDK or NM. `Services/PartyMemberService.cs`, `Interfaces/IPartyMemberService.cs`.
- **LobbyPropertyWriter** — the mutex+refresh+set+save-with-retry write pattern; owns `LobbyMutex` and `SessionCreationMutex` (SemaphoreSlim), `WriteAsync`, `SaveWithRetryAsync` (429 + "Index was out of range" retries). `Services/LobbyPropertyWriter.cs` (no interface).
- **LobbyRefreshScheduler** — the presence-poll timer: `ShouldFireNow` (per-tick gate), `Boost` (0.75s interval for 15s after invite events), `Reset`, `ResetDeferred`, `IsBoosted`. `Services/LobbyRefreshScheduler.cs` (no interface).
- **SoapPartyEventBus** — the sole caller of `HostConnectionDataSO.OnXxx.Raise(...)`: `RaiseHostConnectionEstablished`/`Lost`, `RaiseInviteSent`/`Received`/`Resolved`, `RaisePartyMemberJoined`/`Left`/`Kicked`, `RaisePartyJoinCompleted` — each null-guarded + logged. `Services/SoapPartyEventBus.cs` (no interface).

### Tests (`Controller/Multiplayer/Tests/`)

- **ServerPlayerVesselInitializerWithAITests** — pure-static NUnit coverage of `BuildActiveDomains`, `GameDataSO.IsActiveDomain`, and the 3-tier `GetBalancedDomain` tie-break (including the worked "solo Jade host, DC=3, 3 AI → Ruby/Gold/Ruby" case). `Controller/Multiplayer/Tests/ServerPlayerVesselInitializerWithAITests.cs`.
- **PartyAcceptFlowPlayModeTests** — reflection tests of `HostConnectionService.IsBenignLobbyPatcherError` (direct/wrapped AOORE benign, unrelated AOORE / other types not benign); the two-NetworkManager happy-path accept tests are documented as a manual MPPM smoke procedure (TODO). `Controller/Multiplayer/Tests/PartyAcceptFlowPlayModeTests.cs`.

### Interactions & patterns

- **SOAP is the spawn nervous system.** `GameDataSO` events drive everything: `OnPlayerNetworkSpawnedUlong` (raised by `Player.OnNetworkSpawn`, consumed by server initializers), `OnVesselNetworkSpawned`, `OnPlayerPairInitialized`, `OnClientReady` (loading-screen clear + party-join gate), `OnInitializeGame`, `OnSessionStarted`/`OnSessionEnded`. Party state flows out through `HostConnectionDataSO` events (invites, member join/left/kicked, join-completed, connection established/lost) and reactive lists (`OnlinePlayers`, `PartyMembers`) — raised exclusively by `SoapPartyEventBus`. Freestyle transitions use `MenuFreestyleEventsContainerSO`.
- **NetDomain is the one authoritative team channel.** Only the server writes `Player.NetDomain` (clients request via `RequestSetDomain_ServerRpc`); `OnNetDomainChanged` fans out to the local `Player.Domain` mirror, `RoundStats.Domain` on every peer, and a vessel repaint. The menu's Jade reset (`MenuServerPlayerVesselInitializer`) and the arcade-modal reset (`ArcadeConfigSyncManager`) are server-side; AI domains come from `GetBalancedDomain`. No client code writes domain state.
- **Eager per-user Relay (locked).** Each player owns a solo Relay party session from menu entry; accept = leave own session then join the inviter's; the presence lobby (lobby-only UGS) and party session (Relay) coexist with an always-listening NetworkManager, and the network is torn down only by explicit leave/quit/transport-failure — never by a vessel spawner's despawn.
- **DI & lifetime.** `GameDataSO`, `HostConnectionDataSO`, and the pure-C# party services are Reflex-injected; Netcode-instantiated objects (host Player, replicated vessels) bypass Reflex, so `ClientPlayerVesselInitializer.InjectVesselDependencies` DI-injects client-side vessel instances and `Player` falls back to `PlayerDataService.Instance`. `HostConnectionService`/`PartyInviteController`/`FriendsInitializer` share one DontDestroyOnLoad GameObject.
- **Threading contract.** Every UGS/Netcode `Task` await uses `.AsMainThread()`; SOAP raises and `UnityEngine.Object` access never happen off-thread. Rate-limit (429), host-conflict, and transient-SessionException failures are absorbed with exponential-backoff retries; catches log `NetworkDiagnostics.ClassifyException` + snapshot.
- **Persistent-object hazards handled.** `Player` and `RoundStats` survive scene loads, so `PrepareForNewScene`/`InitializeForMultiplayerMode` purge stale per-stat event subscriptions, and both server and client initializers re-register persistent Players cleared by `GameDataSO.ResetRuntimeData`. The client-pull roster loop + reactive pending-pair resolution make joins converge without WaitUntil polling even if a one-shot bootstrap RPC drops.

---

## Input, Camera, Animation, FX & AI

This area covers everything between the raw device and the vessel it drives, plus the presentation layers that respond to that motion. Input uses a **strategy pattern**: `InputController` (a `MonoBehaviour` on each locally-controlled vessel) picks one `IInputStrategy` per frame based on the connected hardware and calls `ProcessInput()`, which writes normalized flight values (`XSum/YSum/XDiff/YDiff`, eased joystick vectors, trigger analogs) and raises discrete `InputEvents` SOAP events into a shared `IInputStatus`. `InputStatus` is a `NetworkBehaviour` that transparently mirrors those values over `NetworkVariable`s so remote peers can replay a vessel's control state. The `AIPilot` writes into the *same* `IInputStatus` fields, so AI and human vessels share one downstream pipeline. Camera consumes that motion (a hand-rolled `CustomCameraController` for gameplay follow, plus a Cinemachine-based menu camera system), the per-vessel `VesselAnimation` subclasses puppet ship geometry from the input values, FX controllers translate vessel state into FMOD audio, and Settings owns the device-local graphics/display store and the cloud-roaming audio/input prefs.

### Input — Strategy Contract & Status Container
The strategy interface plus the shared mutable state every strategy and the AI write into; `InputStatus` is the network-replicated concrete implementation.
- **IInputStrategy** — contract for a platform input handler: `Initialize`, `ProcessInput`, `SetPortrait`, activate/deactivate/pause/resume hooks, invert-Y/throttle toggles. `Assets/_Scripts/Controller/IO/IInputStrategy.cs`
- **BaseInputStrategy** — abstract base: holds the `IInputStatus`, the shared cosine `Ease()` curve (input `[-2,2]`→`[-1,1]`), and `ResetInput()` that zeroes all flight fields. `Assets/_Scripts/Controller/IO/BaseInputStrategy.cs`
- **IInputStatus** — interface for the per-vessel input state bag: scalar flight axes, booleans (`Idle`, `Paused`, gyro/invert flags, `CommandStickControls`), joystick home/clamped/normalized/eased vectors, `ActiveInputDevice`, the `OnButtonPressed`/`OnButtonReleased` `ScriptableEventInputEvents` channels, `OnToggleInputPaused` C# event, `GetGyroRotation()`, `ResetForReplay()`. `Assets/_Scripts/Controller/IO/IInputStatus.cs`
- **InputStatus** — `NetworkBehaviour` implementation. Every field is backed by a paired `NetworkVariable<T>` (owner-write/everyone-read) *and* a local fallback; getters/setters switch on `IsSpawned` so it works un-spawned (single-player/menu) and replicates when networked. Owns the two `ScriptableEventInputEvents` serialized channels, propagates `n_paused` changes to `OnToggleInputPaused`, and implements `ResetForReplay()`. `Assets/_Scripts/Controller/IO/InputStatus.cs`
- **IInputProvider** — narrow read-only view (`XSum/YSum/XDiff/YDiff`, eased sticks, `OneTouchLeft`, `SingleTouchValue`) for consumers that only need to read flight input. `Assets/_Scripts/Controller/IO/IInputProvider.cs`
- **InputController** — `MonoBehaviour` that owns the strategy set. `Awake` `GetOrAdd`s an `InputStatus`; `Initialize()` builds the four strategies + `MultiMouseService` + `DeviceOrientationHandler`, syncs invert settings from `GameSetting`, and picks a strategy. `Update()` ticks multi-mouse, re-selects the strategy each frame (`SelectStrategy`: Gamepad → Touch on handheld → engaged DualMouse → Keyboard), runs `ProcessInput()`, and drives orientation. Public API: `SetPortrait`, `SetIdle`, `SetPause` (raises pause/resume on the strategy), `OnToggleGyro`, `GetGyroRotation`, static `UsingGamepad()`. Handles Windows Esc→fullscreen and the dual-mouse "both-LMB engage / Esc disengage" gesture. `[Inject] GameSetting`. `Assets/_Scripts/Controller/IO/InputController.cs`

### Input — Platform Strategies
Four concrete strategies convert their device into the unified `IInputStatus` shape (identical `XSum/YSum/XDiff/YDiff` math) and raise the same directional/speed `InputEvents`.
- **GamepadInputStrategy** — sticks → flight axes; face buttons → `Button1/2/3Action`; right shoulder → `FlipAction`; analog triggers (0.05 deadzone) feed `LeftTriggerAnalog/RightTriggerAnalog` and generate the full `OnlyLeft/OnlyRight/BothSticks` drift-trigger composite events; applies invert-Y/throttle post-calc; raises `FullSpeed/MinimumSpeedStraightAction`. `Assets/_Scripts/Controller/IO/GamepadInputStrategy.cs`
- **TouchInputStrategy** — dual-thumb virtual joysticks over EnhancedTouch; handles 1/2/3+ touch counts, a custom touch-tuned `Ease()` (mostly-linear with cubic deadzone), single-thumb **drift** transitions (2→1 finger lift raises `OnlyLeft/OnlyRightStickAction` and pins throttle to 1), `CommandStickControls` node-tap mode, and `IdleAction` on no-touch. `Assets/_Scripts/Controller/IO/TouchInputStrategy.cs`
- **KeyboardMouseInputStrategy** — in-scope W/S-throttle + A/D-roll + mouse-look strategy; locks/hides cursor, maps mouse/keys to stick-equivalents, raises stick/button/speed events. NOTE: `InputController` actually instantiates `KeyboardInputStrategy` (a separate, smoothed-virtual-stick class at the repo-root `Assets/KeyboardInputStrategy.cs`, out of scope); this in-scope `KeyboardMouseInputStrategy` is a parallel/legacy variant not wired into the controller. `Assets/_Scripts/Controller/IO/KeyboardMouseInputStrategy.cs`
- **DualMouseInputStrategy** — `sealed`; desktop two-mice symmetric flight (each mouse = a virtual stick with exponential recenter). LMB-left→`Button1`, LMB-right→`Button2`, MMB→`Button3`, RMB-per-mouse→left/right drift triggers with the same `OnlyLeft/OnlyRight/Both` composites; releases triggers on deactivate. Consumes per-device deltas from `MultiMouseService`. `Assets/_Scripts/Controller/IO/DualMouseInputStrategy.cs`

### Input — Multi-Mouse Provider Stack
Platform abstraction that separates two physical mice on desktop so `DualMouseInputStrategy` gets independent deltas/buttons.
- **IMultiMouseDevice / IMultiMouseProvider** — per-device (`ConsumeDelta`, button states, `Tick`) and provider (`IsAvailable`, `DeviceCount`, `GetDevice`, `Tick/Refresh/Shutdown`) interfaces. `Assets/_Scripts/Controller/IO/MultiMouse/IMultiMouseDevice.cs`
- **MultiMouseService** — chooses the best provider: prefers Unity's `Mouse.all` when it already separates mice (macOS/Linux), else falls back to Win32 Raw Input on Windows; exposes `HasTwoMice`, `Tick`, `GetDevice`. `Assets/_Scripts/Controller/IO/MultiMouse/MultiMouseService.cs`
- **UnityMultiMouseProvider** — `sealed`; wraps `InputSystem.devices` `Mouse` instances, accumulating deltas and edge-detecting buttons per frame; hot-plug re-enumerates while <2 mice. `Assets/_Scripts/Controller/IO/MultiMouse/UnityMultiMouseProvider.cs`
- **Win32RawInputMultiMouseProvider** — `sealed`, `#if UNITY_STANDALONE_WIN`; spins a background thread owning a message-only window registered for `WM_INPUT`, marshals `RAWMOUSE` per-`HANDLE` deltas/buttons under a lock, and snapshots them into per-frame `FrameView` device objects on `Tick`. Full P/Invoke of `user32`/`kernel32`. `Assets/_Scripts/Controller/IO/MultiMouse/Win32RawInputMultiMouseProvider.cs`

### Input — Orientation, Phone-Flip & Haptics
Mobile sensor handling and the shared haptic dispatcher.
- **DeviceOrientationHandler** — plain class driven by `InputController`; forces landscape, reads the accelerometer to raise `FlipAction` on phone flip, and manages the `AttitudeSensor` gyro (async stabilization coroutine, `GyroToUnity` conversion, `GetAttitudeRotation()`) toggled by `OnToggleGyro`. `Assets/_Scripts/Controller/IO/DeviceOrientationHandler.cs`
- **PhoneFlipDetector** — `MonoBehaviour` that watches `Input.acceleration.y` and fires the static `onPhoneFlip(bool)` C# event on landscape-left/right flips. `Assets/_Scripts/Controller/IO/PhoneFlipDetector.cs`
- **FlipUIScreen** — subscribes to `PhoneFlipDetector.onPhoneFlip` and re-anchors a `RectTransform` corner on flip. `Assets/_Scripts/Controller/IO/FlipUIScreen.cs`
- **FlipUI** — subscribes to the same event and rotates its transform 180° on flip. `Assets/_Scripts/Controller/IO/FlipUI.cs`
- **HapticController** — `MonoBehaviour` + static API; `PlayHaptic(HapticType)` and `PlayConstant(...)` route through NiceVibrations, gated on `GameSetting.HapticsEnabled/HapticsLevel`. Defines the **HapticType** enum (`None, ButtonPress, PrismCollision, ShipCollision, CrystalCollision, MineCollision`) mapped to NiceVibrations presets. `[Inject] GameSetting`. `Assets/_Scripts/Controller/IO/HapticController.cs`

### Input — Gamepad UI Helpers & Action Asset
Gamepad support for menu UI, and the generated Input System binding class.
- **ControllerButtonPress** — `[RequireComponent(Button)]`; invokes a UI button's `onClick` when a configured `GamepadButton` is pressed while a matching `MenuScreens`/`ModalWindows` context is active; swaps the prompt sprite between DualShock (△○✕□) and Xbox (YBAX) glyphs. `Assets/_Scripts/Controller/IO/ControllerButtonPress.cs`
- **ControllerDropdown** — `[RequireComponent(TMP_Dropdown)]`; D-pad up/down cycles a TMP dropdown's value. `Assets/_Scripts/Controller/IO/ControllerDropdown.cs`
- **InputActionsAsset** — auto-generated Unity Input System wrapper (`InputActionCodeGenerator` 1.14.2) exposing the project's `.inputactions` maps/actions/schemes. `Assets/_Scripts/Controller/IO/_Input Mapping/InputActionsAsset.cs`

### Camera — Gameplay Follow Camera
The hand-rolled follow camera used by all gameplay scenes and menu freestyle.
- **ICameraController** — contract: `ApplySettings`, `SetFollowTarget`, `Activate/Deactivate`, camera-distance get/set, `NeutralOffsetZ`, `ZoomSmoothTime`, `AdaptiveZoomEnabled`. `Assets/_Scripts/Controller/Camera/ICameraController.cs`
- **ICameraConfigurator** — one-method `Configure(ICameraController)` hook for per-vessel camera configuration. `Assets/_Scripts/Controller/Camera/ICameraConfigurator.cs`
- **CustomCameraController** — `[RequireComponent(Camera)]` implementation. `LateUpdate` follows a target with continuous **lateral-dominance-blended** `SmoothDamp` position + `Slerp` rotation (fixes agile-vessel jitter), a teleport guard for kickoff/respawn snaps, Perlin-noise camera `Shake(intensity,duration)`, `SnapToTarget`, distance/offset get-set, and orthographic toggle. Reads `CameraMode` from `CameraSettingsSO` (fixed vs dynamic vs adaptive-zoom). `Assets/_Scripts/Controller/Camera/CustomCameraController.cs`
- **CameraSettingsSO** — `[CreateAssetMenu]` per-vessel config: `CameraMode` (**CameraMode** enum: `FixedCamera/DynamicCamera/Orthographic`), follow offset, dynamic min/max distance, follow/rotation smooth times, `disableSmoothing`, near/far clip, adaptive-zoom enable + max distance, orthographic size. `Assets/_Scripts/Controller/Camera/CameraSettingsSO.cs`
- **CameraSettingsSOEditor** — `#if UNITY_EDITOR` custom inspector that shows only the fields relevant to the selected `CameraMode`. `Assets/_Scripts/Controller/Camera/CameraSettingsSOEditor.cs`
- **CameraSettingsApplier** — `[RequireComponent(Camera)]` drop-on that applies the player's FOV + post-process AA (FXAA/SMAA/TAA) from `DisplayGraphicsSettings` and keeps them live via its `OnFieldOfViewChanged`/`OnAnySettingChanged` events. `Assets/_Scripts/Controller/Camera/CameraSettingsApplier.cs`

### Camera — Menu / Cinemachine
Cinemachine-blended camera for Menu_Main's lava-lamp autopilot ↔ freestyle transitions.
- **MainMenuCameraController** — orchestrates menu↔freestyle camera blends. Defines the **MenuCameraMode** enum (`CrystalOrbit/VesselFollow/VesselChaseTight/VesselTopDownPan`), builds/reuses runtime vCams (`CM Freestyle Bridge`, `CM Menu Vessel Follow`) on `CameraManager`, and blends A→B (menu vCam → bridge → hand off to `CustomCameraController`) and back via `CinemachineBrain` priority + `DefaultBlend`/FOV-punch overrides. Listens to SOAP `OnClientReady`, `MenuFreestyleEventsContainerSO.OnGameStateTransitionStart/OnMenuStateTransitionStart`, `CellRuntimeDataSO.OnCrystalSpawned`; supports randomized mode-switching via a UniTask loop. `[Inject] MenuFreestyleEventsContainerSO, GameDataSO`. `Assets/_Scripts/Controller/Camera/MainMenuCameraController.cs`
- **CinemachineMatchTargetOrientation** — `CinemachineExtension` (Aim stage) that orients a vCam to `LookRotation(target - camPos, target.up)` — matching `CustomCameraController.SnapToTarget` so the bridge→PlayerCam handoff has zero rotation discontinuity; optional rotation damping. `Assets/_Scripts/Controller/Camera/CinemachineMatchTargetOrientation.cs`
- **VCamRecorderController** — dev tool; after a delay points a `CinemachineVirtualCameraBase` at the player's vessel transform (recording studio). `Assets/_Scripts/Controller/Camera/VCamRecorderController.cs`

(Referenced but out of this scope: **CameraManager** at `Controller/Managers/CameraManager.cs` owns the vCam hierarchy and `SetupGamePlayCameras`/`SetMainMenuCameraActive`; **VesselCameraCustomizer** at `Controller/Vessel/` supplies the per-vessel `CameraSettingsSO`.)

### Animation — Base + Per-Vessel Puppetry
`VesselAnimation` is the abstract driver; each vessel subclass maps pitch/yaw/roll/throttle onto its geometry (either transform rotation or an `Animator`). All read `IInputStatus` each frame and honor `Idle`/single-stick modes.
- **VesselAnimation** — abstract `MonoBehaviour` base. `Update()` dispatches `Idle()` or `PerformShipPuppetry(pitch,yaw,roll,throttle)` from `IInputStatus`; provides `RotatePart`/`ResetAnimation`/`Brake` helpers, shape-key blend-shape driving from `ResourceSystem.OnElementLevelChange` (**Element**→blend index), and engine/body flare material hooks. Subclasses implement `AssignTransforms` + `PerformShipPuppetry`. `Assets/_Scripts/Controller/Animation/VesselAnimation.cs`
- **MantaAnimation** — transform puppetry for fuselage/two wings/four thrusters using small/medium/big scalers. `Assets/_Scripts/Controller/Animation/MantaAnimation.cs`
- **DolphinAnimation** — transform puppetry for fuselage/wings/tail chain with brake-driven wing splay. `Assets/_Scripts/Controller/Animation/DolphinAnimation.cs`
- **RhinoAnimation** — transform puppetry for fuselage/wings/engines with throttle-driven wing rotation. `Assets/_Scripts/Controller/Animation/RhinoAnimation.cs`
- **UrchinAnimation** — transform puppetry for body/guns/jets; scales jet parts up when detached and spins the body when attached (`VesselStatus.IsAttached`). `Assets/_Scripts/Controller/Animation/UrchinAnimation.cs`
- **BufoAnimation** — transform puppetry for fuselage/wings/thrusters; overrides `RotatePart` to swap axes in portrait mode. `Assets/_Scripts/Controller/Animation/BufoAnimation.cs`
- **RiptideAnimation** — Squirrel/racing puppetry: reparents wings/thrusters to a `DriftHandle` while drifting (`VesselStatus.IsDrifting`), animates jaw open-angle from a resource (`ResourceSystem.Resources[JawResourceIndex].OnResourceChange`). `Assets/_Scripts/Controller/Animation/RiptideAnimation.cs`
- **SparrowAnimationController** — `Animator`-driven; lerps and pushes `Pitch/Yaw/Roll/Throttle` floats and a `Boost` bool. `Assets/_Scripts/Controller/Animation/SparrowAnimationController.cs`
- **MantaAnimationContoller** *(sic)* — alternate `Animator`-driven Manta puppetry with optional boost bool and 2× exaggerated params. `Assets/_Scripts/Controller/Animation/MantaAnimationContoller.cs`
- **SingleStickAnimationController** — `Animator`-driven puppetry for single-stick vessels, sets a `Blend`=1 param. `Assets/_Scripts/Controller/Animation/SingleStickAnimationController.cs`
- **RotateAroundOrigin** — spins a transform's position around world origin (menu follow-target orbiter, used by `MainMenuCameraController`). `Assets/_Scripts/Controller/Animation/RotateAroundOrigin.cs`
- **Pan** — starts a continuous transform spin on gamepad right-shoulder press (recording/photo tool). `Assets/_Scripts/Controller/Animation/Pan.cs`

### FX — FMOD Audio Controllers
Vessel/flora audio driven by state, all gated on `GameSetting.SFXLevel`/`SFXEnabled`, local-user, and vessel class.
- **DriftAudioController** — `[DisallowMultipleComponent]`; runs a looping FMOD drift event while `VesselStatus.IsDrifting`, driving a `Drift Amount` parameter (0 = single trigger, 1 = both, from `IInputStatus` trigger analogs), with an Idle/Active/Releasing phase machine (**DriftPhase**/**AttachMode** nested enums), a release one-shot, and vessel-class gating (Squirrel). `Assets/_Scripts/Controller/FX/DriftAudioController.cs`
- **ProximityBoostAudioController** — `[DisallowMultipleComponent]`; listens to the `ScriptableEventBoostChanged` SOAP channel and fires a per-skim tick one-shot + a looping "speed surge" event whose amount parameter tracks the normalized `BoostMultiplier` (base/max from shared SOAP `ScriptableVariable<float>`s). Local-user + vessel-class gated. `Assets/_Scripts/Controller/FX/ProximityBoostAudioController.cs`
- **FloraAmbientAudioController** — `[DisallowMultipleComponent]`; one looping spatialized FMOD ambient bed per living flora, created on enable and stopped/released (fade-out) on disable/destroy so nothing lingers after a flora withers. `Assets/_Scripts/Controller/FX/FloraAmbientAudioController.cs`
- **FMODOneShotVolumeHelper** — static helper: `PlaySFXOneShot`/`PlaySFXOneShotAttached` reproduce FMOD's PlayOneShot create/start/release with a `setVolume()` in between so one-shots honor the SFX slider (short-circuits at volume ≤ 0). `Assets/_Scripts/Controller/FX/FMODOneShotVolumeHelper.cs`

### FX — VFX / Particle Property Modifiers
Small drop-on tweakers for authored effects.
- **VFXPropertyModifier** — on `Start`, sets a named float/int/vector3/texture property on a `UnityEngine.VFX.VisualEffect` and reinitializes it. `Assets/_Scripts/Controller/FX/VFXPropertyModifier.cs`
- **ParticleEffectModifier** — on `Start`, overrides one named `ParticleSystem.main` property (lifetime/speed/size/color/gravity/sim-speed/max-particles). `Assets/_Scripts/Controller/FX/ParticleEffectModifier.cs`

### AI — Pilot & Gunner
The AI pilot writes into the same `IInputStatus` that human strategies do, so all downstream flight/animation/camera code is device-agnostic.
- **AIPilot** — `MonoBehaviour` autopilot. Steers toward a target (nearest domain-appropriate `CellItem` crystal via `OnCellItemsUpdated` SOAP, or nearest enemy player in `seekPlayers`/Joust mode, or an external `Func<Vector3>` steering hook for Astro League) by computing a cross-product turn and writing `XSum/YSum/YDiff/XDiff` (or the single-stick eased vector) into `IInputStatus`. Skill-lerped throttle/aggressiveness/avoidance, optional `ram`/`drift` behaviors, ability coroutines that `StartAction`/`StopAction` `ShipActionSO`s through an `ActionExecutorRegistry`. `ConfigureForGameMode(gameData, seekPlayers, skill)` set at spawn. Defines nested **AIAbility** class + **Corner**/**AvoidanceBehavior** helpers. `[Inject] GameDataSO`. `Assets/_Scripts/Controller/AI/AIPilot.cs`
- **AIGunner** — thin `MonoBehaviour` stub holding a `Gun`, gun mount, and `Domains domain` (`[FormerlySerializedAs("Team")]`); wiring largely commented out. `Assets/_Scripts/Controller/AI/AIGunner.cs`

### Settings — Audio / Input Prefs (Cloud-Roaming)
- **GameSetting** — `SingletonPersistent<GameSetting>`; the cloud-roaming settings store for music/SFX/haptics enable+level, invert-Y, invert-throttle, and joystick-visuals. PlayerPrefs-backed (**PlayerPrefKeys** enum) with a full set of static change events (`OnChangeSFXLevel`, `OnChangeInvertYEnabledStatus`, …) that `InputController`, `DriftAudioController`, `HapticController`, etc. subscribe to. Layers UGS Cloud Save on top of local prefs (`ApplyCloudSettings`/`SyncToCloud`). `[Inject] UGSDataService`. `Assets/_Scripts/Controller/Settings/GameSetting.cs`

### Settings — Graphics / Display / Performance (Device-Local)
- **GraphicsSettingsData** — `[Serializable]` plain container for all device-local settings: display mode/resolution/refresh/vsync/target-fps/FOV, quality preset/AA/render-scale/upscaling/texture-quality, and CPU knobs (adaptive-perf, ecosystem density, physics detail, AI crowd size, VFX density). `Clone()` via `MemberwiseClone`. `Assets/_Scripts/Controller/Settings/GraphicsSettingsData.cs`
- **DisplayGraphicsSettings** — `SingletonPersistent<>`; PlayerPrefs-only store (does **not** cloud-sync — per-device). `RuntimeInitializeOnLoadMethod` self-instantiates, seeds from `SettingsAutoDetector` on first run, loads+applies otherwise. Typed setters persist + apply + raise per-consumer change events (`OnFieldOfViewChanged`, `OnEcosystemDensityChanged`, `OnVfxDensityChanged`, `OnAnySettingChanged`, …); bulk ops `ApplyAutoDetect`/`ApplySnapshot`/`ResetToDefaults`. `Assets/_Scripts/Controller/Settings/DisplayGraphicsSettings.cs`
- **GraphicsSettingsApplier** — stateless bridge; the only code that pushes a `GraphicsSettingsData` onto `QualitySettings`/`Screen`/the URP asset/`Camera` (`ApplyAll/ApplyDisplay/ApplyQuality/ApplyFrameRate`, plus reusable `ApplyCameraAntiAliasing` for newly-spawned cameras). `Assets/_Scripts/Controller/Settings/GraphicsSettingsApplier.cs`
- **SettingsAutoDetector** — static `SystemInfo` heuristic: `CapabilityScore()` (0–7, CPU-core-weighted), `RecommendPreset()`, and `RecommendSettings()` producing a full snapshot (native display, refresh-aware fps cap, core-scaled CPU knobs, GPU-tier AA). `Assets/_Scripts/Controller/Settings/SettingsAutoDetector.cs`
- **BenchmarkSceneLauncher** — `[Inject] GameDataSO`; wires the Settings "Run Benchmark" button to launch `BenchmarkStressTest` through the standard `GameDataSO`→`OnLaunchGame`→`SceneLoader` path (single-player sandbox, AI count from `DisplayGraphicsSettings.AiCrowdSize`). `Assets/_Scripts/Controller/Settings/BenchmarkSceneLauncher.cs`
- **AccessibilitySettings** — static PlayerPrefs store for colorblind mode, subtitles + scale, reduce-flashing (photosensitivity), and camera-shake intensity, each with a change event for future consumers. `Assets/_Scripts/Controller/Settings/AccessibilitySettings.cs`

(The setting enums — `DisplayModeSetting`, `VSyncSetting`, `QualityPresetSetting`, `AntiAliasingSetting`, `UpscalingSetting`, `AdaptivePerformanceSetting`, `EcosystemDensitySetting`, `PhysicsDetailSetting`, `ColorblindModeSetting` — live in `CosmicShore.Data`, out of this scope.)

### Interactions & patterns
- **One input surface, many producers.** Human strategies and `AIPilot` both write the identical `IInputStatus` fields, so `VesselAnimation`, the vessel flight/prism controllers, and camera all consume input without knowing whether a human, a remote peer, or the AI produced it. `InputStatus` being a `NetworkBehaviour` means owner writes replicate to every peer via `NetworkVariable`s (read-everyone/write-owner) — that is how remote vessels animate correctly.
- **SOAP everywhere for events.** Discrete input is broadcast via `ScriptableEventInputEvents` (`OnButtonPressed/Released` with the **InputEvents** enum) rather than direct calls; camera menu transitions key off `MenuFreestyleEventsContainerSO`, `GameDataSO.OnClientReady`, and `CellRuntimeDataSO.OnCrystalSpawned`; boost audio listens on `ScriptableEventBoostChanged`; AI crystal seeking listens on an `OnCellItemsUpdated` `ScriptableEventNoParam`. `BenchmarkSceneLauncher` reuses the `GameDataSO`→`OnLaunchGame` launch pipeline rather than a bespoke loader.
- **DI (Reflex).** `[Inject]` supplies `GameSetting` (`InputController`, `HapticController`), `GameDataSO` (`AIPilot`, `BenchmarkSceneLauncher`, `MainMenuCameraController`), `MenuFreestyleEventsContainerSO` (`MainMenuCameraController`), and `UGSDataService` (`GameSetting`). `InputController` falls back to `FindFirstObjectByType` because it's often `GetOrAdd`ed dynamically (no DI pass).
- **Settings fan-out via static C# events.** `GameSetting` (cloud-roaming audio/input) and `DisplayGraphicsSettings`/`AccessibilitySettings` (device-local) both broadcast change events that camera (`CameraSettingsApplier`), audio (drift/boost/flora controllers), and input (`InputController` invert sync) subscribe to — the single writers, multi-reader pattern the rest of the codebase uses. `GraphicsSettingsApplier` is the sole toucher of engine graphics state (Config Separation).
- **Cinemachine + hand-rolled camera coexistence.** Gameplay uses `CustomCameraController` (`ICameraController`); the menu blends between Cinemachine vCams and hands off to that same controller, with `CinemachineMatchTargetOrientation` guaranteeing pose continuity across the handoff. `CameraManager` (out of scope) owns the vCam hierarchy both reference.
- **Continuity-of-existence law.** FMOD audio controllers deliberately fade/release on disable (`ALLOWFADEOUT`) so voices don't pop out when flora wither or drift ends — mirroring the platform-wide "nothing pops in/out" rule.

Notable discrepancy found while reading: `InputController` instantiates `KeyboardInputStrategy`, which is defined at the repo root `Assets/KeyboardInputStrategy.cs` (outside this scope), while the in-scope `Controller/IO/KeyboardMouseInputStrategy.cs` (class `KeyboardMouseInputStrategy`) is a parallel keyboard/mouse strategy not referenced by the controller.

---

## Projectiles & Freestyle Toys

Two loosely-related gameplay areas. **Projectiles** (`Controller/Projectiles/`) are the pooled, non-networked ordnance vessels fire — guns that launch cosine-decelerated bolts, timed mines, and a family of Area-of-Effect "explosions" that either damage prism mass through a Burst spatial-index batch path or *build* prism structures (rings, flowers, hemispheres, radial bursts) via the canonical prism-spawn event channel. **Freestyle Toys** (`Controller/Toys/` + `ScriptableObjects/Toys/`) implement the **Toy** fundamental: score-less, end-less world-space stations the local vessel flies into during Menu_Main freestyle to swap ship, change domain, paint a shape, or summon the Wanderway microscene conveyor. Both areas obey the continuity-of-existence law (bloom-in / suction-out, conserved mass) and route mutations through existing fundamentals rather than duplicating them.

### Projectile flight core & pooling

A `Projectile` is a pooled `MonoBehaviour` that moves itself along a velocity vector with a cosine ease-out over a fixed `projectileTime`, then fires its end-effects and returns to its factory; energy tier picks which pool it comes from.

- **Projectile** — pooled flying bolt; `MonoBehaviour`, `[Inject] AudioSystem`. Async `MoveProjectileAsync` (UniTask, `PreLateUpdate`) advances position with a `Cos` decay factor, spike-fade opacity, and optional detach-on-launch; `ProjectileImpactor.ExecuteEndEffects()` runs at end of flight. Registers itself as a `PrismColliderLodManager` focus while in flight so prism colliders along its path wake up. Domain-aware friendly-fire gating (`DisallowImpactOnPrism`/`DisallowImpactOnVessel`). `Controller/Projectiles/Projectile.cs`
- **ProjectileType** (enum) — `Normal`, `Energized`, `SuperEnergized` pool selector. `Controller/Projectiles/ProjectileFactory.cs`
- **ProjectileFactory** — `MonoBehaviour` mapping `ProjectileType → ProjectilePoolManager` (inspector `PoolEntry` list); `GetProjectile(energy,…)` derives the tier from energy and returns/`SetType`s a pooled instance; `ReturnProjectile` releases it. `Controller/Projectiles/ProjectileFactory.cs`
- **ProjectilePoolManager** — `GenericPoolManager<Projectile>` wrapper with a double-release guard. `Controller/Projectiles/ProjectilePoolManager.cs`
- **ExplodableProjectile** — fully commented-out/**deprecated** legacy subclass (kept only as a `/* */` reference; superseded by `Projectile` + effect SOs). `Controller/Projectiles/ExplodableProjectile.cs`

### Guns

`Gun` is the firing driver: it pulls projectiles from a `ProjectileFactory`, sets velocity/scale, and launches them in a chosen pattern with a cooldown. `LoadedGun` is a self-firing preset used as an end-effect (e.g. a projectile that spawns more projectiles).

- **Gun** — `MonoBehaviour` firing controller. `FireGun(...)` with cooldown; `FireSingle` (forward or custom-direction bolt inheriting vessel velocity) and `FireSpherical` (tetrahedral 4-vertex pattern at `energy==0`, else a golden-angle Fibonacci sphere of `2*(energy+3)` bolts). `StopProjectile`/`DetonateProjectile` act on the last-fired bolt; `Initialize(IVesselStatus)` captures owning domain. `Controller/Projectiles/Gun.cs`
- **FiringPatterns** (enum) — `Default`, `Spherical`. `Controller/Projectiles/Gun.cs`
- **LoadedGun** — `Gun` subclass with serialized `speed`/`projectileTime`/`firingPattern`/`energy`; its parameterless `FireGun()` fires a preconfigured spherical burst from `transform.parent`, ignoring cooldown and detaching after spawn. `Controller/Projectiles/LoadedGun.cs`

### Mines

- **Mine** — `MonoBehaviour`, `[Inject] AudioSystem`. Coroutine `ExplodeCountdown` detonates after `explodeAfterSeconds`; `NullifyDelayedExplosion(velocity)` forces an immediate impact-driven explosion. On explode it disables its collider, plays SFX, and instantiates a `SpentMinePrefab` per `MineModelData` model, handing each an `Impact.HandleImpact`. `isplayer` swaps to a neutral blue material. `Controller/Projectiles/Mine.cs`
- **MineModelData** (`[Serializable]`) — per-model material set (default / exploding / inactive). `Controller/Projectiles/Mine.cs`

### AOE explosions & prism-building bursts

`AOEExplosion` is the shared base for everything that blooms outward from a point. The plain explosion scales a trigger sphere over `ExplosionDuration` and does damage; a batch-processing path excludes the `TrailBlocks` layer from PhysX and drives damage through a Burst spatial-index job (`ExplosionImpactor.ProcessBatchFrame`) instead of thousands of `OnTriggerEnter` pairs. Subclasses either reshape the explosion (cone) or repurpose the bloom to *spawn prism structures* via the `PrismEventChannelWithReturnSO` channel (conserved-mass, grow-from-zero).

- **AOEExplosion** — base bloom; extends `ElementalShipComponent`, `[Inject] GameDataSO`, `[RequireComponent(MeshRenderer)]`. `Initialize(InitializeStruct)` + `Detonate()` run `ExplodeAsync` (UniTask): scale-lerp with `Sin` ease, per-instance opacity via `MaterialPropertyBlock`, Burst batch AOE damage with `ApplyPrismExclusion`/`RestorePrismExclusion` around the collider. Subscribes to `gameData.OnMiniGameTurnEnd` (cancel) and `OnResetForReplay` (destroy). Nested `InitializeStruct` (domain, vessel, material, maxScale, pose) and `CalculateImpactVector`. `Controller/Projectiles/AOEExplosion.cs`
- **AOEConicExplosion** — cone-shaped variant; builds an `AOEContainer`, scales a cone mesh (`MaxScaleVector = (scale,scale,height)`), recomputes the `SphereCollider.radius` per frame, clones the vessel's `AOEConicExplosionMaterial`, `Blue` domain falls back to the vessel's domain. `Controller/Projectiles/AOEConicExplosion.cs`
- **AOEBlockCreation** — `AOEExplosion` that *lays prisms* instead of damaging: spawns `ringCount` rings of `blockCount` interactive prisms via `_prismSpawnEvent.RaiseEvent(PrismEventData)`, tracks them in `Trail`s for reset cleanup, optional `shielded`. Base for the structural bursts. `Controller/Projectiles/AOEBlockCreation.cs`
- **AOEBlockSpawner** — `AOEBlockCreation` that delegates to a serialized `SpawnableBase` (`spawnable.Spawn(...)`) `repetitions` times along the vessel course. `Controller/Projectiles/AOEBlockSpawner.cs`
- **AOEFlowerCreation** — `AOEBlockCreation` that seeds a symmetric branching "flower" of prisms off the vessel's last two trail blocks over `TunnelAmount` iterations (recursive `CreateBranches`). `Controller/Projectiles/AOEFlowerCreation.cs`
- **AOERadialBlocks** — `AOEConicExplosion` that first runs the conic visual, then spawns `numberOfRays × blocksPerRay` prisms on spread rays with a `scaleCurve` falloff and manual grow-to-scale. `Controller/Projectiles/AOERadialBlocks.cs`
- **AOEDangerHemisphereBlocks** — `sealed AOEExplosion` driven by a `DangerHemisphereConfigSO`; spawns forward-hemisphere-limited rays of prisms, then `MakeDangerousAsync` flags each `IsDangerous`/`IsShielded`, applies the danger material, and grows it in. `Controller/Projectiles/AOEDangerHemisphereBlocks.cs`

### Spawnables (structure prefabs)

`SpawnableBase` (out of scope, in `Controller/Environment/Spawning/`) subclasses that directly `Instantiate` prism prefabs into a named structure; used by `AOEBlockSpawner` and shape modes.

- **SpawnableFlower** — recursive branching flower of prisms seeded off two source blocks (`depth`-limited `CreateBranches`), domain-tinted, each block `Instantiate`d into a container `Trail`. `Controller/Projectiles/SpawnableFlower.cs`
- **SpawnableRings** — `ringCount` rings of `prismsPerRing` prisms at `ringRadius`/`ringSpacing`, intensity-scaled tip angle, optional `MakeDangerous`/`ActivateShield`. `Controller/Projectiles/SpawnableRings.cs`

### Block-projectile pooling & legacy buffer

- **BlockProjectileFactory** — `MonoBehaviour` mapping `PrismType → BlockProjectilePoolManager` for prism-as-projectile spawning (`GetBlock`; `ReturnBlock` is currently stubbed/commented). `Controller/Projectiles/BlockProjectileFactory.cs`
- **BlockProjectilePoolManager** — `GenericPoolManager<Prism>` that detaches the pooled prism from its parent on `Get`. `Controller/Projectiles/BlockProjectilePoolManager.cs`
- **TrailBlockBufferManager** — **deprecated** `Singleton<TrailBlockBufferManager>` that pre-instantiates and maintains per-domain `Queue<Prism>` buffers with an adaptive instantiate-rate coroutine (superseded by `PrismFactory` event channels). `Controller/Projectiles/TrailBlockBufferManager.cs`

### Toy fundamental — base, context & toybox

A **Toy** is a first-class fundamental: a world-space station (trigger collider + procedural sphere/label visuals) that the *local* player's vessel flies into during freestyle. Toys have no score, no end condition, and no decay — they bloom in, fire once per pass, and re-arm only after the vessel physically flies clear (exit-gated). The `ToyboxController` places the player's unlocked toys around the cell membrane at runtime, handing each a shared `ToyContext` in lieu of Reflex injection.

- **Toy** — abstract base; `MonoBehaviour`, `[RequireComponent(Collider)]`. Owns bloom-in (`BloomIn` smoothstep scale-from-zero), the slow `Rebloom`/`regrowDuration` re-grow used on flip, `Disarm`, an `Update` exit-gate (`LocalVesselOutsideTrigger` with `exitRadiusMultiplier` hysteresis, robust across swap despawn/respawn), local-user + `Context.IsFreestyleActive()` gating in `OnTriggerEnter`, and the abstract `OnActivated(IVesselStatus)`. `Controller/Toys/Toy.cs`
- **ToyContext** (plain class) — shared runtime deps passed to every toy: `GameDataSO`, `MenuServerPlayerVesselInitializer`, `VesselPrefabContainer`, and the `Func<bool> IsFreestyleActive` predicate. `Controller/Toys/ToyContext.cs`
- **ToyPlacement** (readonly struct) — where/how big a toy sits: position, look-target, body radius, trigger radius. `Controller/Toys/ToyContext.cs`
- **ToyFactory** (static) — procedural visual builder: `CreateRoot`/`CreateBareRoot` (trigger `SphereCollider` root), `AddSphereBody` (tinted URP-Unlit sphere), `AddLabel` (world-space `TextMeshPro`). Zero prefab authoring. `Controller/Toys/ToyFactory.cs`
- **ToyboxController** — `MonoBehaviour`, `[Inject] GameDataSO`, `[Inject] MenuFreestyleEventsContainerSO`. Self-wires: resolves a `ToyboxSO` (serialized → `Resources/Toybox` → code-built default of the four built-in toys), finds the active `Cell` membrane (or a fallback ring), and `def.Spawn`s each unlocked toy around the ring on `OnClientReady`. Tracks freestyle-active from the freestyle SOAP events. `Controller/Toys/ToyboxController.cs`
- **ToyDefinitionSO** — abstract `ScriptableObject` config: id/displayName/description, `unlockedByDefault`, `placementAngleDegrees`, `accentColor`, abstract `Spawn(parent, placement, context)`, `SetRuntimeMetadata`. `ScriptableObjects/Toys/ToyDefinitionSO.cs`
- **ToyboxSO** — `[CreateAssetMenu]` registry of `ToyDefinitionSO`s + id→bool unlock map (`IsUnlocked`, `UnlockedToys`, `SetToyUnlocked`, `AddToy`, `OnToyboxChanged`); unlock *conditions* deferred (everything ships unlocked). `ScriptableObjects/Toys/ToyboxSO.cs`

### Swap-toy sets (Vessel Changer & Domain Changer)

A `SwapToySetCoordinator<T>` manages a *set* of toys that always shows "the options you are not currently on"; flying through one applies the change and flips that toy to the option you just left. Both changers route through server-authoritative pipelines, never client-local writes.

- **SwapToy** — `Toy` that just raises `event Action<SwapToy> Activated` on activation; the coordinator owns option state. `Controller/Toys/SwapToy.cs`
- **SwapToySetCoordinator&lt;T&gt;** — abstract `MonoBehaviour` set manager: polls current option each frame, reconciles slots to `universe \ {current}` (offering `prev` first so the used toy flips), lays slots on an arc, `Disarm`s all siblings on any activation. Abstract hooks `InitialUniverse`/`TryGetCurrent`/`IsValid`/`Apply`/`ConfigureVisual`/`LabelFor` + `OnTick`. `Controller/Toys/SwapToySetCoordinator.cs`
- **VesselChangerToySet** — `SwapToySetCoordinator<VesselClassType>`; each slot is a mini ship model, `Apply` calls `MenuServerPlayerVesselInitializer.RequestSwap` then restores freestyle control after a delay (fixes lost-control-after-swap), `OnTick` recolours all mini ships on domain change. `Controller/Toys/VesselChangerToySet.cs`
- **VesselModelBuilder** (static) — builds a display-only hull model from a ship *prefab asset* (never instantiates the gameplay prefab): filters to hull meshes (drops skimmer sphere / trails / jets / vfx), paints one opaque self-lit domain-tinted preview material, normalizes to target radius. `Controller/Toys/VesselModelBuilder.cs`
- **DomainChangerToySet** — `SwapToySetCoordinator<Domains>`; one toy per team colour you're not on (Jade/Ruby/Gold minus current), `Apply` calls `Player.RequestSetDomain_ServerRpc`, tints from `ThemeManagerData.GetDomainUIColor`. `Controller/Toys/DomainChangerToySet.cs`
- **VesselChangerToyDefinitionSO** — `[CreateAssetMenu]` def; optional `vesselCollection`, spawns a `VesselChangerToySet`. `ScriptableObjects/Toys/VesselChangerToyDefinitionSO.cs`
- **DomainChangerToyDefinitionSO** — `[CreateAssetMenu]` def; spawns a `DomainChangerToySet`. `ScriptableObjects/Toys/DomainChangerToyDefinitionSO.cs`

### Painting toy ("Fly by Numbers")

- **PaintingToy** — `Toy` that on activation places a shape plane ahead of the vessel and starts a `MenuShapePainter`; `Configure(shape, scale, reach, offset)`. `Controller/Toys/PaintingToy.cs`
- **MenuShapePainter** — self-contained `MonoBehaviour` runner: reads a `ShapeDefinition`'s waypoints, draws a ghost `LineRenderer` outline + a live guide line + a lit waypoint marker, advances as the vessel flies near each point in order (the vessel's own trail paints it), `event Completed`, fade-and-destroy. No Cell/crystal-manager/scoring/HUD — runs anywhere. `Controller/Toys/MenuShapePainter.cs`
- **PaintingToyDefinitionSO** — `[CreateAssetMenu]` def; serialized `ShapeDefinition shape`, scale/reach/offset, `SetRuntimeShape` (default toybox uses an auto-generated `ShapePreset.Star`). `ScriptableObjects/Toys/PaintingToyDefinitionSO.cs`

### Wanderway — microscene conveyor

The conveyor toy toggles a belt that keeps a field of procedurally-varied "microscenes" (gate runs, tunnels, orchards, meadows, menageries…) blooming ahead of the vessel's flight path. It is a *closed* system: a fixed pool of scenes transports a fixed stock of conserved prisms (suction-out → relocate → re-pose → bloom-in), lays skimmable elemental/omni crystals, and releases flora/fauna into the containing cell as ordinary citizens. Everything advances only with player motion — no score, no timer.

- **ConveyorToy** — `Toy` toggle; first pass starts a sibling `MicrosceneConveyor` (built under the toybox root, not the toy, so bloom-scale never scales laid mass), later passes `StopBelt`/`Resume`; `ShowState` flips body/label to read on/off. `Configure(ConveyorConfig)`. `Controller/Toys/ConveyorToy.cs`
- **MicrosceneConveyor** — the belt runner `MonoBehaviour`. Speed-scaled geometry (spacing/lookahead grow with vessel speed); each `0.25s` tick scans a forward cone around the live course and either **near-fills** a scene directly ahead or **extends** off the frontmost scene's tip, placing a new `Microscene` while the pool is under `PoolSize` else recycling the farthest-behind one. Shuffle-bag recipe selection (`NextPlan`), live playable-domain read each draw (never snapshots domain), per-arrival derived `System.Random` for reproducibility, dormant while not freestyle. `Begin`/`Resume`/`StopBelt`, `IsRunning`. `Controller/Toys/MicrosceneConveyor.cs`
- **ConveyorConfig** (plain class) — belt tunables (prism/omni prefabs, crystal effects, palette, pool size, prism budget, radii, spacing, lookahead, recycle/transition timing, turn-break degrees, seed). `Controller/Toys/MicrosceneConveyor.cs`
- **Microscene** — one belt slot `MonoBehaviour`: a `Trail` of prisms + crystal pickups laid by a plan. `PopulateAsync` (batched grow-in via `PrismTrailBuilder.LayBatched`), `RecycleAsync` (suction→relocate→`RearrangeInto`→bloom, re-theming each prism's domain+kind reversibly via `PrismKinds`), crystal minting (`MintElementalCrystal`/`MintOmniCrystal` with runtime `ElementalCrystalImpactor`+`ImpactCollider` wiring and `FadeIn`), `ReleaseLifeforms` into the host `Cell` through `CellLifeSpawnerBase` respecting population caps + `FaunaReproductionRules`, and `NotifyPrismPositions` (spatial-index movers contract). `Busy`/`Anchor`/`PendingAnchor`. `Controller/Toys/Microscene.cs`
- **MicroscenePlan** — two-layer plan: geometry (`PrismPoints`/`CrystalPoints`) vs themed output (`Prisms` as `PrismLay`, `Crystals` as `CrystalDrop`) + `FloraCount`/`FaunaCount`. `Controller/Toys/MicroscenePlan.cs`
  - **CrystalKind** (enum) — `Elemental` / `Omni`. **CrystalDrop** (readonly struct) — local position + kind. `Controller/Toys/MicroscenePlan.cs`
- **MicroscenePalette** (`[Serializable]`) — theming config kept separate from geometry: domain-scheme weights (Mono/Banded/Accent/NeutralVein) + accent/blue chances, prism-kind weights + caps (danger/shielded/supershielded), scale-mood, omni-crystal chance, live `PlayableDomains`; `Default`. `Controller/Toys/MicroscenePalette.cs`
- **MicroscenePatterns** (static) — `RecipeCount = 28` pure geometry generators (Gate Run, Helix Weave, Tunnel, Slalom, Starburst, Orchard, Meadow, Menagerie, Polygon Gates, Serpent Ribbon, Colonnade, Orbitals, Canyon, Lattice, Comet Tail, Spiral Ramp, Archway, Vortex, Slot Corridor, Cube Field, Torus Gate, Pillar Hall, Turbine, Asteroid Field, Rolling Plains, Grove, Aviary, Preserve). Each re-rolls its params per plan and is fitted to exactly `prismBudget` points (`FitToBudget`/`ClampCrystals`); `ApplyTheming` assigns per-prism domain + `PrismKind` + scale mood + crystal mix; `IsLifeformRecipe` marks the six that release flora/fauna. Builds geometry via the shared `PrismGeometry` vocabulary. `Controller/Toys/MicroscenePatterns.cs`
- **ConveyorToyDefinitionSO** — `[CreateAssetMenu]` def; authors all `ConveyorConfig` fields (prism/omni prefabs, crystal effects, palette, pool/budget/spacing/lookahead/turn-break/seed, lifeform toggle), `BuildConfig`, `SetRuntimePrismPrefab`. `ScriptableObjects/Toys/ConveyorToyDefinitionSO.cs`

### Interactions & patterns

- **Prism spawning goes through the fundamental, not around it.** All AOE structure-builders and microscenes create mass via the `PrismEventChannelWithReturnSO` return-channel (`PrismEventData` → `PrismFactory`) or the shared `PrismTrailBuilder`/`PrismGeometry`/`PrismKinds`/`SpawnPoint`/`PrismLay` primitives (in `Controller/Environment/Spawning/`) — never bespoke instantiation of conserved mass. Microscene recycling is *transport* (suction→re-pose→bloom), conserving mass; fauna grazing is the only sink.
- **Burst spatial index.** `AOEExplosion` damage runs through `ExplosionImpactor.ProcessBatchFrame` (a Burst job over cache-packed prism data in `PrismSpatialIndex`), excluding the `TrailBlocks` layer from PhysX; `Microscene.NotifyPrismPositions` honours the index's movers contract; projectiles register as `PrismColliderLodManager` focuses.
- **SOAP channels.** AOE effects subscribe to `GameDataSO.OnMiniGameTurnEnd` (cancel) and `OnResetForReplay` (destroy); `ToyboxController` subscribes to `GameDataSO.OnClientReady` and `MenuFreestyleEventsContainerSO` transition events; audio fires through `AudioSystem.PlayGameplaySFX(GameplaySFXCategory…)`.
- **DI & context.** Projectile/Mine use Reflex `[Inject] AudioSystem`; `AOEExplosion` injects `GameDataSO`. Runtime-built toys are *not* injected — `ToyboxController` resolves deps and hands them down through `ToyContext`.
- **Networking is reused, never rebuilt.** Toys mutate networked state only through server-authoritative paths: vessel swap via `MenuServerPlayerVesselInitializer.RequestSwap`, domain via `Player.RequestSetDomain_ServerRpc` → `Player.NetDomain` replication. Toys read the live `LocalPlayer.Domain`/`VesselStatus.VesselType` mirrors each frame and never snapshot domain.
- **Continuity + Toy fundamental.** Every spawn blooms/grows from zero and every removal suctions/withers; toys gate on `IsLocalUser` + freestyle-active, re-arm exit-gated, and impose no decay/score/end — composing with Vessel, Domain, Prisms/Mass, Crystals, and Flora/Fauna rather than duplicating them. `Cell` membrane is read for placement and lifeform release, not re-implemented.

---

## System — Bootstrap, DI, App State, Auth, Scene

This area is the application backbone: it boots the app in the Bootstrap scene, wires every persistent service through Reflex dependency injection, tracks the top-level application phase as a single-writer state machine, drives the bootstrap → authentication → main-menu execution flow, and owns scene loading/fade transitions, UGS authentication, the Friends service, network reachability monitoring, and the analytics pipeline. Cross-system state is shared exclusively through SOAP `ScriptableVariable`/`ScriptableEvent` assets (single writer, many readers) rather than singletons or static events; the few remaining static events (`ApplicationLifecycleManager`, `PauseSystem`, `LoginEventBus`) are legacy or OS-lifecycle bridges. Async work uses UniTask with `CancellationToken`s tied to component lifecycle, and every UGS/Netcode `Task await` is marshaled back to the main thread with `.AsMainThread()`.

### Bootstrap & Reflex DI Root
`AppManager` is the single orchestrator and DI installer that runs first (`[DefaultExecutionOrder(-100)]`) in the Bootstrap scene, establishes the `DontDestroyOnLoad` root, configures the platform, registers every service/asset, kicks off auth + network monitoring, and loads the Authentication scene. `BootstrapConfigSO` supplies its tunables. A static safety net auto-creates the bootstrap flow objects when the scene lacks them.
- **AppManager** — top-level MonoBehaviour orchestrator + Reflex `IInstaller`; `Awake` sets `DontDestroyOnLoad`/configures platform/early-resolves managers, `Start` transitions app state to `Bootstrapping`, configures `GameDataSO`, starts `NetworkMonitor` + auth, and runs `RunBootstrapAsync` (min-splash hold → `Authenticating` → load Authentication scene); `InstallBindings` registers all DI (see below); has static `OnBootstrapComplete`/`OnBootstrapFailed` events + `HasBootstrapped`. `_Scripts/System/AppManager.cs`
- **BootstrapConfigSO** — `ScriptableObject` config (`ScriptableObjects/Core/BootstrapConfig`) exposing `ServiceInitTimeoutSeconds`, `MinimumSplashDuration`, `TargetFrameRate`, `PreventScreenSleep`, `VSyncCount`, `VerboseLogging`. `_Scripts/System/Bootstrap/BootstrapConfigSO.cs`

DI registration performed by `AppManager.InstallBindings(ContainerBuilder)`:
- **SO assets via `RegisterValue`** (fail-loud if unassigned): `SceneNameListSO`, `GameDataSO`, `AuthenticationDataVariable`, `NetworkMonitorDataVariable`, `FriendsDataSO`, `HostConnectionDataSO`, `SO_GameList`, `TournamentDataSO`, `MenuFreestyleEventsContainerSO`, `ApplicationLifecycleEventsContainerSO`, `ApplicationStateDataVariable`.
- **MonoBehaviour singletons via lazy `RegisterFactory`** (serialized ref → deferred scene search → `EnsurePersistent`): `GameSetting`, `AudioSystem`, `PlayerDataService`, `UGSStatsManager`, `CaptainManager`, `IAPManager`, `SceneLoader`, `ThemeManager`, `CameraManager`, `PostProcessingManager`, `StatsManager`, `SceneTransitionManager`, `MultiplayerSetup`, `UGSDataService`, `PrismFactory`.
- **Pure-C# lazy singletons**: `AuthenticationServiceFacade`, `NetworkMonitor`, `FriendsServiceFacade`, `ApplicationStateMachine`, `AnalyticsServiceFacade`, `TournamentController`, plus the party stack (`LobbyPropertyWriter`, `SoapPartyEventBus`, `LobbyRefreshScheduler`, `InviteService`, `AcceptanceSignalService`, and interface-typed `IPresenceLobbyService`/`PresenceLobbyService`, `IPartySessionService`/`PartySessionService`, `IPartyMemberService`/`PartyMemberService`, `INetworkTransitionService`/`NetworkTransitionService`).
- **`[Inject]`-forced-eager services** (constructed at bootstrap so their subscriptions exist from app start): `AnalyticsServiceFacade`, `TournamentController`, alongside the injected `AuthenticationServiceFacade`/`FriendsServiceFacade`/`NetworkMonitor`/`ApplicationStateMachine`.

### Application State Machine
`ApplicationStateMachine` is the single writer of the top-level `ApplicationState`, validated against a table-driven transition graph and published through SOAP so any system can read or subscribe. It self-subscribes to gameplay and OS-lifecycle events for automatic phase changes.
- **ApplicationStateMachine** — pure C# DI singleton; writes `ApplicationStateDataVariable` (`State`/`PreviousState`, raises `OnStateChanged`); `TransitionTo` enforces `ValidTransitions` with special handling for `ShuttingDown` (always), `Paused` (any→Paused→previous via `_stateBeforePause`), and `Disconnected` (any active state); auto-wires `GameDataSO.OnSessionStarted`→`InGame`, `OnMiniGameEnd`→`GameOver`, `ApplicationLifecycleManager.OnAppPaused`→pause/restore, `OnAppQuitting`→`ShuttingDown`, `NetworkMonitorData.OnNetworkLost`→`Disconnected`. `_Scripts/System/ApplicationStateMachine.cs`
- **ApplicationState (enum)** — `None(0)`, `Bootstrapping(1)`, `Authenticating(2)`, `MainMenu(3)`, `LoadingGame(4)`, `InGame(5)`, `GameOver(6)`, `Paused(7)`, `Disconnected(8)`, `ShuttingDown(9)` (defined in `Data/Enums/ApplicationState.cs`, driven exclusively by this machine).

### Menu Scene Controller (Menu_Main sub-state machine)
`MainMenuController` owns a per-scene sub-state machine that runs while `ApplicationState` stays `MainMenu`, configuring the autopilot-vessel display and reacting to readiness/freestyle/launch SOAP events.
- **MainMenuController** — scene MonoBehaviour on Menu_Main's Game object; `[Inject]`s `MenuFreestyleEventsContainerSO`/`GameDataSO`/`AnalyticsServiceFacade`; `Start` configures menu game data (vessel class, intensity, spawn origins, pushes `menuVesselClass` onto the host's owner-writable `Player.NetDefaultVesselType`) and calls `gameData.InitializeGame()`; table-driven `MainMenuState` transitions fire the public `OnStateChanged` event; subscribes to `OnClientReady`→`HandleMenuReady` (Ready + local autopilot + `analytics.RecordMenuReady()`), `OnLaunchGame`→`LaunchingGame`, `OnPlayerPairInitialized`→activate non-local vessels' autopilot, and freestyle enter/exit events. Never writes domain (server-authoritative). `_Scripts/System/MainMenuController.cs`
- **MainMenuState (enum)** — `None(0)`, `Initializing(1)`, `Ready(2)`, `LaunchingGame(3)`, `Freestyle(4)` (defined in `Data/Enums/MainMenuState.cs`).

### Scene Flow & Transitions
Scene loading is split between `SceneTransitionManager` (fade overlay + raw load) and `SceneLoader` (game-launch/return/session-end orchestration, network-aware). `SplashToAuthFlow` routes the splash scene into auth, and `ApplicationLifecycleManager` bridges OS lifecycle to both static and SOAP events.
- **SceneTransitionManager** — persistent MonoBehaviour (`[DefaultExecutionOrder(-50)]`); adopts the Bootstrap splash `CanvasGroup` (`_splashOverlay`) or auto-builds a programmatic full-screen overlay; `LoadSceneAsync` (fade-out→`SceneManager.LoadSceneAsync`→settle→fade-in with null-op + synchronous fallbacks), `LoadNetworkSceneAsync` (server-authoritative via `NetworkManager.SceneManager`), and manual `FadeToBlack`/`FadeFromBlack`/`SetFadeImmediate`; `SetFadeImmediate` is the **threading canary** — it logs an error and bails if called off the main thread. Fires `OnSceneLoadComplete`. `_Scripts/System/Bootstrap/SceneTransitionManager.cs`
- **SceneLoader** — persistent MonoBehaviour DI singleton; subscribes in code to `GameDataSO.OnLaunchGame`→`LaunchGame`, `_onClickToMainMenuButton`→`ReturnToMainMenu`, `_onActiveSessionEnd`→`HandleActiveSessionEnd`; `LaunchGame`/`ReturnToMainMenu` transition app state, drive the splash overlay + arm `FadeFromSplashOnReady` on `OnClientReady`, and defer the actual `NetworkManager.SceneManager.LoadScene` to the server (MPPM/connected-client guard `IsListening && !IsServer`); `ClearPlayerVesselReferences` explicitly despawns AI players/vessels before scene reload; `ArmSplashFadeOnNextClientReady` is a public re-arm hook (also called by `PartyInviteController`); honors Tournament between-game splash dwell. `_Scripts/System/SceneLoader.cs`
- **SplashToAuthFlow** — splash-scene MonoBehaviour; `[Inject]`s `AuthenticationDataVariable`/`SceneNameListSO`/`SceneTransitionManager`; after the splash duration waits (with timeout) for in-flight auth to settle, then always routes to the Authentication scene (even when already signed in, because network host start + Netcode Menu_Main load happen there). `_Scripts/System/SplashToAuthFlow.cs`
- **ApplicationLifecycleManager** — persistent MonoBehaviour; `[Inject]`s `ApplicationLifecycleEventsContainerSO`; forwards Unity `OnApplicationPause`/`Focus`/`Quit` + `SceneManager.sceneLoaded`/`sceneUnloaded` to **both** static C# events (`OnAppPaused`, `OnAppFocusChanged`, `OnAppQuitting`, `OnSceneLoaded`, `OnSceneUnloading`) and the SOAP container events; exposes static `IsQuitting`; resets statics on domain reload. `_Scripts/System/Bootstrap/ApplicationLifecycleManager.cs`

### Authentication (UGS, single-writer facade)
Authentication uses Unity Gaming Services exclusively. `AuthenticationServiceFacade` is the sole writer to the SOAP auth state; a scene controller and a thin MonoBehaviour adapter read/subscribe.
- **AuthenticationServiceFacade** — pure C# DI singleton, single writer to `AuthenticationDataVariable`; `StartAuthentication` (fire-and-forget init + anonymous sign-in), `EnsureInitializedAsync` (coalesced `UnityServices.InitializeAsync` + MPPM profile switch + one-time UGS event wiring), `EnsureSignedInAnonymouslyAsync`, `TrySignInCachedAsync` (silent cached-session restore), `SignOut`, `ResetStartupState`; centralizes state + raises `OnSignedIn`/`OnSignInFailed`/`OnSignedOut` SOAP events; provider/link stubs (Google/Apple/Facebook/Steam/UnityPlayerAccount) return completed tasks. `_Scripts/System/AuthenticationServiceFacade.cs`
- **AuthenticationSceneController** — Authentication-scene MonoBehaviour orchestrating the UI flow; `[Inject]`s the facade, `AuthenticationDataVariable`, `PlayerDataService`, `SceneNameListSO`, `SceneTransitionManager`, `ApplicationStateMachine`, `HostConnectionDataSO`; races `RunAuthFlowCoreAsync` (already-signed-in → cached → guest-login panel or auto sign-in) against a hard safety timeout; post-auth waits for `PlayerDataService.IsInitialized`, optionally shows username setup (`UpdatePlayerNameAsync`), then `NavigateToMainMenu` → `LoadMainMenuNetworkedAsync` which waits for a live Relay session (`WaitForRelayReadyAsync` on `HostConnectionDataSO.OnHostConnectionEstablished` + `NetworkManager.IsListening`, up to 3 retries + manual retry surface) before `NetworkManager.SceneManager.LoadScene(Menu_Main)`; drives a `ScriptableEventBootStatusRequest` for status/retry UI. `_Scripts/System/AuthenticationSceneController.cs`
- **AuthenticationController** — thin MonoBehaviour adapter (`_Scripts/System/Systems/Authentication/`); inspector `autoSignInAnonymously`; `[Inject]`s the facade + `AuthenticationDataVariable`; delegates `EnsureSignedInAnonymouslyAsync`/`TrySignInCachedAsync`/`SignOut` and exposes `IsSignedIn`/`PlayerId`, with a standalone fallback when no facade is injected; provider/link stubs mirror the facade.

### Friends (UGS Friends SDK, single-writer facade)
- **FriendsServiceFacade** — pure C# DI singleton, single writer to `FriendsDataSO`; `InitializeAsync` (post-auth: `FriendsService.InitializeAsync().AsMainThread()`, wire `RelationshipAdded`/`RelationshipDeleted`/`PresenceUpdated`, `SyncAllRelationships`, raise `OnFriendsServiceReady`, then presence); public API `SendFriendRequestByName/ByIdAsync`, `Accept/Decline/CancelFriendRequestAsync`, `RemoveFriendAsync`, `Block/UnblockPlayerAsync`, `SetPresence/SetAvailabilityAsync`, `RefreshAsync`, `IsFriend`/`IsBlocked`; every mutation re-syncs the four SOAP lists; maps UGS `Relationship`→`FriendData` (strips the `#XXXX` discriminator, maps availability to int) and raises `OnFriendAdded`/`OnFriendRemoved`/`OnFriendRequestReceived`; `HandleSignedOut` resets state. `_Scripts/System/FriendsServiceFacade.cs`

### Network Monitoring
- **NetworkMonitor** — plain C# DI singleton wrapping `Application.internetReachability`; `StartMonitoring`/`StopMonitoring` run a cancelable UniTask poll loop (default 5s) that writes `NetworkMonitorData.IsOnline`/`LastTransitionUnscaledTime` and raises `OnNetworkLost`/`OnNetworkFound` on edge transitions; initialized/started by `AppManager` (which also seeds `CosmicShore.Utility.NetworkDiagnostics`). `_Scripts/System/NetworkMonitor.cs`

### Analytics / Instrumentation
- **AnalyticsServiceFacade** — pure C# DI singleton, single writer for all UGS Analytics custom events; opt-in consent + COPPA age gate stored in `PlayerPrefs` (`ConsentDecided`/`ConsentGranted`/`AgeEligible`/`NeedsPrivacyFlow`, `SetConsent`/`SetAgeEligible`/`SubmitBirthYear`/`RequestDataDeletion`); starts collection only when signed-in + connected + age-eligible + consented + UGS initialized; funnels every event through `RecordEvent`, flushes on pause/quit; subscribes to a wide SOAP + static-event surface (auth sign-in, network found/lost, `OnMiniGameTurnStarted`/`OnMiniGameEnd`, app pause/quit, ads, freestyle enter, friends/party events, `GameSetting` changes, `FavoriteSystem`, `UGSCloudSaveProvider.OnSaveFailed`, `UserActionSystem`) and exposes typed `Record*` calls (menu ready, mode/intensity unlock, crystals earned/spent/blocked, vessel unlock, quest, share, first-launch, session-ended, repeated-fail). `_Scripts/System/Instrumentation/AnalyticsServiceFacade.cs`
- **UGSKeys** — static class: single source of truth for all UGS Cloud Save keys and the ~30 analytics event-name constants consumed by the facade. `_Scripts/System/UGSKeys.cs`

### Misc app-root systems
Additional systems living at the `System/` root that participate in the app shell.
- **PauseSystem** — static pause toggle (`TogglePauseGame` sets `Time.timeScale`, exposes `Paused` + static `OnGamePaused`/`OnGameResumed`); used by `SceneLoader` on scene transitions. `_Scripts/System/PauseSystem.cs`
- **IAPManager** — MonoBehaviour singleton for real-money "support"/episode purchases via hosted web checkout (`Application.OpenURL`); config-driven by `SO_IAPConfig` + per-episode `SO_EpisodeData`; `InitiateEpisodePurchase`/`InitiateSupportPurchase`/`ConfirmPendingPurchase` with `OnCheckoutOpened`/`OnReturnedFromCheckout`/`OnPurchaseComplete` events; DI-registered as a manager singleton. `_Scripts/System/IAPManager.cs`
- **DailyChallengeSystem** — `SingletonPersistent<DailyChallengeSystem>`; deterministic daily game selection (date-seeded RNG over `Arcade.TrainingGames`), ticket balance + tiered reward ladder tracked in `PlayerPrefs` (legacy, backend-bound TODO), `PlayDailyChallenge`/`ReportScore`/`ClaimReward`; bridges legacy `PlayFab PlayerDataController.OnGettingPlayerData`. `_Scripts/System/DailyChallengeSystem.cs`
- **TrainingGameProgressSystem** — static class persisting per-`GameModes` `TrainingGameProgress` to disk via `DataAccessor`; `ReportProgress`/`SatisfyIntensityTier`/`ClaimIntensityTierReward`/`GetGameProgress` gate intensity-tier unlocks and reward claims. `_Scripts/System/TrainingGameProgressSystem.cs`
- **SnsShare** — MonoBehaviour screenshot-and-share via `NativeShare`; `[Inject]`s `AnalyticsServiceFacade` (`RecordShareTriggered`) + `GameDataSO`. `_Scripts/System/SnsShare.cs`
- **ScriptableEventNotificationPayload** — SOAP `ScriptableEvent<NotificationPayload>` asset type (`ScriptableObjects/Events/NotificationPayload`). `_Scripts/System/ScriptableEventNotificationPayload.cs`

### Legacy / EventBus / Helpers
Older or inert helpers kept in scope.
- **LoginEventBus** — legacy static event bus (`Dictionary<LoginType, UnityEvent>`, `Subscribe`/`Unsubscribe`/`Publish`, `IDisposable`); `LoginType` enum `Success`/`Fail`/`Other`. Superseded by the SOAP auth path. `_Scripts/System/Architectures/EventBus/LoginEventBus.cs`
- **TestLoginUI** — debug `OnGUI` login window driving the legacy PlayFab `AuthenticationManager` via `LoginEventBus`. `_Scripts/System/Architectures/EventBus/TestLoginUI.cs`
- **DialogueEditorRuntimeTester** — fully commented-out/vestigial stub (only a `using`); no active type. `_Scripts/System/Helpers/DialogueEditorRuntimeTester.cs`

### Tests (edit-mode, `CosmicShore.Core` namespace)
`_Scripts/System/Bootstrap/Tests/` (Unity Test Framework / NUnit):
- **AppManagerBootstrapTests** (file `BootstrapControllerTests.cs`, 12 tests), **ApplicationStateMachineTests** (26), **BootstrapConfigSOTests** (9), **SceneTransitionManagerTests** (11), **ApplicationLifecycleManagerTests** (11), **SceneFlowIntegrationTests** (17) — cover config defaults, the state-transition table + pause/disconnect/shutdown rules, overlay/fade behavior, lifecycle event dispatch, and the end-to-end bootstrap→auth→menu scene flow.

### Interactions & patterns
- **Single-writer SOAP**: each shared state has exactly one writer facade/machine and many readers — `AuthenticationServiceFacade`→`AuthenticationDataVariable`, `ApplicationStateMachine`→`ApplicationStateDataVariable`, `FriendsServiceFacade`→`FriendsDataSO`, `NetworkMonitor`→`NetworkMonitorDataVariable`, `AnalyticsServiceFacade` (read-only consumer of most), plus `HostConnectionDataSO`/`GameDataSO`/`MenuFreestyleEventsContainerSO`/`ApplicationLifecycleEventsContainerSO`/`TournamentDataSO` shared through DI. Consumers subscribe to `ScriptableEvent.OnRaised` or read `.Value`; missing SOAP refs fail loud (no null-guards).
- **Reflex DI**: `AppManager` is the root `IInstaller`; SO assets use `RegisterValue`, scene managers + pure services use lazy `RegisterFactory` so registration never fails before the object exists. `[Inject]` fields populate between `Awake` and `Start`.
- **Execution-order + bootstrap flow**: `AppManager(-100)` → `SceneTransitionManager(-50)` → services; the canonical runtime path is Bootstrap (`Bootstrapping`) → Authentication scene (`Authenticating`, UGS sign-in + host start) → Netcode-loaded Menu_Main (`MainMenu` + `MainMenuController` sub-states) → `LoadingGame`→`InGame`→`GameOver` driven by `GameDataSO` events via `SceneLoader`.
- **Netcode coupling**: scene loads for launch/return are server-authoritative (`NetworkManager.SceneManager.LoadScene`) with client-defer guards for MPPM; `AuthenticationSceneController` gates the Menu_Main load on a confirmed Relay/host state via `HostConnectionDataSO`; `MainMenuController` writes the host's owner-writable `Player.NetDefaultVesselType` but never domain.
- **Threading**: every UGS/Netcode await uses `.AsMainThread()`; `SceneTransitionManager.SetFadeImmediate` is the main-thread canary; UniTask + linked-CTS timeouts (not polling) are the standard async idiom throughout auth/scene flow.
- **OS lifecycle bridge**: `ApplicationLifecycleManager` fans pause/focus/quit/scene events into both static C# events and SOAP, feeding `ApplicationStateMachine` (pause/shutdown) and `AnalyticsServiceFacade` (session-end/flush).

---

## System — Backend, Cloud, Progression & App Features

This area is the game's **persistence, backend, and meta-progression layer**: it saves and loads all durable player state, drives which game modes/intensities/vessels are unlocked, and hosts the app-feature systems (loadouts, squads, quests, favorites, ads, calls-to-action, funnel analytics). The live persistence backbone is **UGS Cloud Save** behind a SOLID facade (`UGSDataService` → per-domain `ICloudDataRepository` → `ICloudSaveProvider`), with each domain serialized as a plain `[Serializable]` JSON model under a key in `UGSKeys`. A large **PlayFab** integration (auth, economy/catalog, groups, PlayStream analytics, leaderboards, cloud scripts, player data) still exists but is **deprecated and inert** — every PlayFab entry point early-returns from its `Start`/`Awake`/`OnEnable` with a `[PLAYFAB DISABLED]` note, its responsibilities migrated to UGS (auth → `AuthenticationServiceFacade`, player data → `PlayerDataService`, leaderboards → `UGSStatsManager`, analytics → `AnalyticsServiceFacade`). Progression/loadout/squad/favorites systems layer on top, some persisting locally via `DataAccessor` and some via the cloud repositories.

### Cloud Data Layer — UGS Cloud Save facade (`System/CloudData/`)
Single-writer facade + one repository per data domain. `UGSDataService` (DI-registered lazy MonoBehaviour singleton, also exposes a static `Instance` for non-DI callers) creates every repository, loads them all after sign-in, and re-syncs vessel unlock state onto `SO_Vessel` assets. Repositories derive from `CloudDataRepository<T>` which owns a debounced save loop (never drops a dirty change; provider retries with backoff); the provider is the only class that touches the UGS SDK. All under namespace `CosmicShore.Core`.

- **UGSDataService** — facade orchestrating init/flush/reset and typed read/write access to every domain; subscribes to auth `OnSignedIn`, calls `SyncHangarToVessels()` to restore `SO_Vessel.isLocked`; `System/CloudData/UGSDataService.cs`.
- **IUGSDataService** — facade contract: `IsInitialized`, `OnInitialized`, read-only accessors for Profile/Stats/VesselStats/Progression/Hangar/Episodes/Settings, `InitializeAsync`/`FlushAllAsync`/`ResetAllDataAsync`; `System/CloudData/Interfaces/IUGSDataService.cs`.
- **ICloudDataRepository / ICloudDataReader / ICloudDataWriter** — segregated repo interfaces (read `Data`/`IsLoaded`; write `IsDirty`/`MarkDirty`/`SaveAsync`; full repo adds `CloudKey`, `OnDataChanged`, `LoadAsync`); `System/CloudData/Interfaces/ICloudDataRepository.cs`.
- **ICloudSaveProvider** — backend abstraction (`IsAvailable`, `LoadAsync<T>`, `SaveAsync<T>`) decoupling services from concrete UGS calls; `System/CloudData/Interfaces/ICloudSaveProvider.cs`.
- **UGSCloudSaveProvider** — concrete UGS Cloud Save impl: Newtonsoft (de)serialization with legacy-string fallback, retry backoff `{2s,4s,8s}`, per-key failed-state dedup, `static event OnSaveFailed` (AnalyticsServiceFacade emits `cloud_save_failed`), main-thread marshal for toast/analytics on failure; `System/CloudData/Providers/UGSCloudSaveProvider.cs`.
- **CloudDataRepository\<T\>** — abstract base: default-instance data, `OnAfterLoad` null-collection fixup hook, `DebouncedSaveLoop`, `ResetAsync`; `System/CloudData/Repositories/CloudDataRepository.cs`.

Repositories (one per key; each just sets `CloudKey` and null-guards collections in `OnAfterLoad`):
- **PlayerProfileRepository** — `player_profile` → `PlayerProfileData` (display name, avatar, crystals, reward IDs).
- **PlayerStatsRepository** — `PLAYER_STATS_PROFILE` → `PlayerStatsProfile` (per-mode high scores; seeds Blitz/MultiHex/Joust/CrystalCapture sub-profiles), 2s debounce.
- **VesselStatsRepository** — `VESSEL_STATS` → `VesselStatsCloudData` (per-vessel lifetime telemetry), 2s debounce.
- **GameProgressionRepository** — `GAME_MODE_PROGRESSION` → `GameModeProgressionData` (quest-chain unlock state).
- **HangarRepository** — `HANGAR_DATA` → `HangarCloudData` (vessel unlocks + preferences).
- **EpisodeProgressRepository** — `EPISODE_PROGRESS` → `EpisodeProgressCloudData`.
- **PlayerSettingsRepository** — `PLAYER_SETTINGS` → `PlayerSettingsCloudData` (roaming settings).
- **DailyChallengeRepository** — `DAILY_CHALLENGE` → `DailyChallengeCloudData` (replaces PlayerPrefs storage).
- **TrainingProgressRepository** — `TRAINING_PROGRESS` → `TrainingProgressCloudData` (replaces local-file training progress).
- **SquadRepository** — `SQUAD_DATA` → `SquadCloudData`.
- **LoadoutRepository** — `LOADOUT_DATA` → `LoadoutCloudData`.
- **CaptainProgressRepository** — `CAPTAIN_PROGRESS` → `CaptainProgressCloudData`; **defined but not currently instantiated/wired by `UGSDataService`** (the created set is the 11 above); `System/CloudData/Repositories/CaptainProgressRepository.cs`.

Cloud data models (plain `[Serializable]` JSON DTOs with helper methods; `System/CloudData/Models/`):
- **PlayerSettingsCloudData** — music/SFX/haptics enables+levels, invert-Y/throttle, joystick visuals.
- **HangarCloudData** (+ **VesselPreference**) — `UnlockedVessels`, per-vessel `LastUsedTicks`/`Favorited`, `SelectedVessel`; unlock/lock/pref helpers.
- **EpisodeProgressCloudData** (+ **EpisodeState**) — unlocked/completed episode lists + per-episode missions/best-score/stars; `ReportMissionCompleted`.
- **DailyChallengeCloudData** (+ **RewardTierState**) — challenge date, ticket balance/refill, mode/intensity/high-score, 3 reward tiers; new-day/ticket-refill/report-score/satisfy/claim logic.
- **TrainingProgressCloudData** (+ **TrainingGameState**, **TrainingTierState**) — per-mode current intensity + 4 satisfy/claim tiers with auto-advance.
- **CaptainProgressCloudData** (+ **CaptainState**) — per-captain XP/level/unlocked/encountered/upgrade-count with encounter/unlock/add-XP helpers (intended PlayFab-XP replacement).
- **SquadCloudData** — leader + 2 rogues as `VesselClassType`+`Element` pairs, `Initialized` flag (cloud mirror of local `Squad`).
- **LoadoutCloudData** (+ **LoadoutEntry**, **GameLoadoutEntry**) — player loadout slots + per-game last-used configs + active index (cloud mirror of local `Loadout`).
- **GameModeProgressionData** — lives in `System/Progression/` (see below) but is this layer's model for `GAME_MODE_PROGRESSION`.

Supporting registry (just outside the folder, foundational to this layer):
- **UGSKeys** — single source of truth for all Cloud Save keys **and** analytics event-name constants (play_again, game_started/completed, mode/intensity_unlocked, crystals_earned/spent, friend/party events, quest_completed, cloud_save_failed, etc.); `System/UGSKeys.cs`.

### Game-Mode Progression & Quest Chain (`System/Progression/`)
The live progression system: a linear quest chain gates game-mode unlocks and per-mode intensity tiers (1→4). Data persists through `UGSDataService.ProgressionRepo`; rules are designer-tunable via `SO_ProgressionConfig` (with a code-default fallback). Evaluated at every `GameDataSO.OnMiniGameEnd`.

- **GameModeProgressionData** — cloud model: `UnlockedModes`, `CompletedQuests`, `BestStats`, `MaxUnlockedIntensity`, `IntensityPlayCounts` (keyed `"Mode:intensity"`) plus query/mutate helpers; `System/Progression/GameModeProgressionData.cs`.
- **GameModeProgressionService** — DontDestroyOnLoad singleton (`[Inject] UGSDataService`, `AnalyticsServiceFacade`; serialized `SO_GameModeQuestList`, `SO_ProgressionConfig`, `GameDataSO`). Determines mode/intensity unlock state, evaluates quest targets and intensity-tier unlocks per game end, claims quests to unlock the next mode, exposes hangar-unlock gate; events `OnProgressionChanged`, `OnQuestCompleted`, `OnIntensityUnlocked`; debug/reset API; `System/Progression/GameModeProgressionService.cs`.
- **ParticipationXpAwarder** — MonoBehaviour that awards flat participation XP (from `SO_ProgressionConfig.participationXpPerGame` or local `xpPerGame`) to the local player once per game via `PlayerDataService.AddXP`, driven by `GameDataSO.OnSessionStarted`/`OnMiniGameEnd`; `System/Progression/ParticipationXpAwarder.cs`.
- **SO_ProgressionConfig** (referenced; lives in `ScriptableObjects/`) — always-unlocked modes, first-quest-free, intensity floor/cap, full-intensity modes, participation XP, vessel-hangar quest DisplayName.

### Vessel Unlock / Hangar (`System/VesselUnlock/`)
- **VesselUnlockSystem** — static hangar API: unlock/lock `SO_Vessel` at runtime and persist to `UGSDataService.HangarRepo`; `TryPurchaseVessel` spends crystals via `PlayerDataService.TrySpendCrystals`; `GetCurrencyBalance`, `ResetAllUnlocks`; `event OnUnlockStateChanged`; `System/VesselUnlock/VesselUnlockSystem.cs`.

### Captain XP (legacy, PlayFab-backed) (`System/Xp/`)
- **XpHandler** + **XpData** struct — static per-vessel-class captain XP store (Space/Time/Charge/Mass), encountered-captain tracking, PlayFab player-data read/write via `PlayerDataController`; superseded by `CaptainProgressCloudData` and effectively inert with PlayFab disabled; `System/Xp/XpHandler.cs`.

### LoadOut (`System/LoadOut/`)
Local-file-persisted game launch configuration (via `DataAccessor`), mirrored to cloud by `LoadoutCloudData`/`LoadoutRepository`.
- **Loadout** (struct) — launch config: intensity, player count, `VesselClassType`, `GameModes`, multiplayer flag, `Initialized` computed; `System/LoadOut/Loadout.cs`.
- **ArcadeGameLoadout** (struct) — a `GameModes` + `Loadout` pairing for per-game last-used config; `System/LoadOut/ArcadeGameLoadout.cs`.
- **LoadoutSystem** — static manager over `loadouts.data` (player slots) + `game_loadouts.data` (per-game), active-index selection, get/set/save; `System/LoadOut/LoadoutSystem.cs`.

### Squads (`System/Squads/`)
Local-file-persisted 3-captain squad (leader + 2 rogues), mirrored to cloud by `SquadCloudData`/`SquadRepository`.
- **Squad** (struct) — leader/rogue1/rogue2 as `VesselClassType`+`Element`; constructed from three `SO_Captain`; `System/Squads/Squad.cs`.
- **SquadSystem** — static store over `squad.data`: load/save, default-squad seeding, `SquadLeader`/`RogueOne`/`RogueTwo` resolved against an externally-set `CaptainList`, setters by class/element or `SO_Captain`; `System/Squads/SquadSystem.cs`.

### Legacy Quest / Objective Engine (`System/Quest/`, `System/UserJourney/`, `System/UserAction/`, `System/CallToAction/`)
A separate, event-driven quest/objective engine (distinct from the new `GameModeProgressionService`): user actions fire events → quests advance → completed quests chain into the next and drive call-to-action UI indicators. `QuestSystem`/`UserJourneySystem`/`UserActionSystem`/`CallToActionSystem` are `SingletonPersistent`.
- **Quest** — serializable quest model: title/description/shard value, completion `UserAction` + count/quantity, rewards (`RewardItemID`, `VirtualItem`, crystal list), progress flags, `CallToAction`, `OnQuestCompleted` callback; `System/Quest/Quest.cs`.
- **QuestSystem** — active-quest registry keyed by action label; subscribes to `UserActionSystem.OnUserActionCompleted`, completes quests, grants rewards, records `AnalyticsServiceFacade.RecordQuestCompleted`, registers each quest's `CallToAction`; `System/Quest/QuestSystem.cs`.
- **UserJourneySystem** — drives a linear `SO_QuestChain`: adds the next quest to `QuestSystem` as each `Quest.OnQuestCompleted` fires; `System/UserJourney/UserJourneySystem.cs`.
- **UserAction** — action payload (`UserActionType`, value, label) + `GetGameplayUserActionLabel(mode,vessel,intensity)`; `System/UserAction/UserAction.cs`.
- **UserActionSystem** — broadcast hub: `CompleteAction` raises `OnUserActionCompleted`; `System/UserAction/UserActionSystem.cs`.
- **UserActionTrigger** — MonoBehaviour that fires a `UserAction` (inspector type/value/label) from UnityEvents/buttons; `System/UserAction/UserActionTrigger.cs`.
- **CallToAction** — serializable CTA: target ID, completion `UserActionType`, dependency-target list; `System/CallToAction/CallToAction.cs`.
- **CallToActionSystem** — CTA registry: activate/dismiss target callbacks, dependency counters, resolves CTAs on matching `UserActionSystem.OnUserActionCompleted`; `System/CallToAction/CallToActionSystem.cs`.
- **CallToActionTarget** — MonoBehaviour that registers a `CallToActionTargetType` and shows/hides an `ActiveIndicator` GameObject; `System/CallToAction/CallToActionTarget.cs`.

### Favorites (`System/Favorites/`)
- **FavoriteSystem** — static toggle store for favorited `GameModes` over `game_favorites.data` (`DataAccessor`); `IsFavorited`/`ToggleFavorite`; `event OnFavoriteChanged(mode,isNow)` (AnalyticsServiceFacade subscribes → `minigame_favorited`); `System/Favorites/FavoriteSystem.cs`.

### Ads (`System/Ads/`)
- **AdsSystem** — MonoBehaviour implementing Unity Ads `IUnityAdsInitializationListener`/`IUnityAdsLoadListener`/`IUnityAdsShowListener`; per-platform game/ad-unit IDs, dev-skip flag, `Initialize`/`LoadAd`/`ShowAd`; static events (`AdInitializationComplete/Failed`, `AdLoaded`, `AdFailedToLoad`, `AdShowClick/Start/Complete/Failure`) — AnalyticsServiceFacade subscribes to `AdLoaded` for `ad_impression`; `System/Ads/AdsSystem.cs`.

### PlayFab Integration — DEPRECATED / INERT (`System/Playfab/`)
Full legacy PlayFab SDK integration. Every runtime entry point is disabled (`Start`/`Awake`/`OnEnable` early-return with `[PLAYFAB DISABLED]`); kept as reference/pending removal. Responsibilities migrated to UGS. Namespace `CosmicShore.Core`.

Authentication (`Authentication/`):
- **AuthenticationManager** — `SingletonPersistent`; anonymous/device/custom-ID + email login/register, unlinking, random-name lists, `OnLoginSuccess/Error/RegisterSuccess`; `Awake` disabled.
- **AuthenticationView** — MonoBehaviour login/register/display-name UI; `Start` disabled.
- **PlayFabAccount** — account model (ID, device unique ID, `AuthContext`, `IsHost`).
- **AuthMethods** — enum (Default/Anonymous/PlayFabLogin/EmailLogin/Register).

Economy (`Economy/`):
- **CatalogManager** — `SingletonPersistent` (`[Inject] CaptainManager`); PlayFab Economy catalog/inventory: recursive catalog load, grant/purchase items, elemental-crystal & currency balance, daily-challenge tickets; events `OnLoadCatalogSuccess`/`OnLoadInventory`/`OnInventoryChange`/`OnCurrencyBalanceChange`; `Start` disabled.
- **CaptainManager** — `SingletonPersistent`, DI-registered lazy singleton (per AppManager); captain encounter/unlock/level/XP over catalog inventory + `XpHandler`; `OnEnable` disabled.
- **CatalogBundleHandler** — static bundle search/purchase/pay flow.
- **DailyRewardHandler** — `SingletonPersistent`; cloud-script daily reward/challenge claim + bundle grant; `Start` disabled.
- **Inventory** — serializable local inventory (crystals/captains/upgrades/ship classes/games/tickets/all) with disk save/load + contains-checks.
- **StoreShelve** — serializable catalog cache dictionaries by content type + ticket references.
- **VirtualItem** — catalog/inventory item model (ID, name, content type, `ItemPrice` list, tags, amount).
- **ItemPrice** — price model (item ID, amount, unit amount).

Groups (`Groups/`):
- **GroupController** — `SingletonPersistent`; PlayFab Groups create/delete/get; `Start` disabled.
- **GroupModel** — group name + `EntityKey`.

PlayStream / analytics / leaderboards (`PlayStream/`):
- **AnalyticsController** — `SingletonPersistent`; PlayFab user-data get/set/delete, read-only data, PlayStream event writes; `Start` disabled.
- **LeaderboardManager** — `SingletonPersistent`; online/offline gameplay + daily-challenge stat reporting and leaderboard/friend-leaderboard fetch with local caching; `LeaderboardEntry` struct; `Start` disabled (→ UGS `UGSStatsManager`).
- **EventsModel** — PlayStream `EventContents` + custom-tags wrapper.

Player data (`PlayerData/`):
- **PlayerDataController** — `SingletonPersistent`; PlayFab profile load/display-name/avatar + user-data get/update; events `OnProfileLoaded`/`OnPlayerDisplayNameUpdated`/`OnPlayerAvatarUpdated`/`OnGettingPlayerData`; `Start` disabled (→ UGS `PlayerDataService`).
- **PlayerProfile** — profile model (display name, avatar URL, `ProfileIconId`, email, default name).
- **PlayerSession** — remember-me / login-GUID over PlayerPrefs.
- **PlayerEvent** — PlayStream event payload.
- **CaptainInstanceData** — struct (captainId + upgradeLevel).

Cloud scripts & utility (`CloudScripts/`, `Utility/`, `PlayFabTests/`):
- **CloudScriptRunner** + **FunctionProperties** — generic Azure/CloudScript `ExecuteFunction` wrapper.
- **ModelConversionService** — PlayFab catalog/inventory item ↔ `VirtualItem`/`ItemPrice` conversion.
- **PlayFabUtility** — `SingletonPersistent`; server time + shared `HandleErrorReport`/`GettingPlayFabErrors`.
- **PlayFabCatalogTests** — trivial NUnit placeholder test (`CosmicShore.PlayFabTests` assembly).

### Interactions & patterns
- **SOLID cloud persistence**: `UGSDataService` (facade) → `ICloudDataRepository<T>` (one per domain, `CloudDataRepository<T>` base) → `ICloudSaveProvider` (`UGSCloudSaveProvider`). Domains are added by adding a model + repository + `UGSKeys` constant, never by editing existing repos. Debounced auto-save + backoff retry means a mutation just calls `MarkDirty()`.
- **Auth-driven init**: `UGSDataService` subscribes to `AuthenticationDataVariable.OnSignedIn` (SOAP), loads all repos, then pushes hangar unlocks onto `SO_Vessel` assets; `GameModeProgressionService` waits on `UGSDataService.OnInitialized`.
- **Game-end SOAP fan-out**: `GameDataSO.OnMiniGameEnd`/`OnSessionStarted` drive both `GameModeProgressionService` (quest/intensity unlocks) and `ParticipationXpAwarder` (XP), each reading `RoundStatsList`/`LocalPlayer`/`SelectedIntensity` off `GameDataSO`.
- **Cross-service links**: unlocks read/write crystals through `PlayerDataService` (`TrySpendCrystals`, `AddXP`, `GetCrystalBalance`); `PlayerDataService.HandleProfileChanged` caches into `GameDataSO`. Progression/vessel-unlock/hangar all key off `SO_Vessel`, `SO_GameModeQuestList`/`SO_GameModeQuestData`, `SO_ProgressionConfig`, `GameModes`, `VesselClassType`, `Element`, `Domains`.
- **Analytics is decoupled**: systems expose plain C#/`static` events (`FavoriteSystem.OnFavoriteChanged`, `AdsSystem.AdLoaded`, `UGSCloudSaveProvider.OnSaveFailed`) or call `AnalyticsServiceFacade.Record*`; the facade (single UGS Analytics writer) subscribes, mapping to `UGSKeys` event names.
- **DI**: `UGSDataService` and `CaptainManager` are DI-registered lazy singletons (Reflex `[Inject]`); `GameModeProgressionService`, `QuestSystem`, `UserActionSystem`, `CallToActionSystem`, `UserJourneySystem` inject `AnalyticsServiceFacade`/`UGSDataService` and otherwise use `Instance`/`SingletonPersistent`. No NetworkBehaviours, NetworkVariables, or Burst jobs in this area — it is all app-level backend/meta state.
- **Two persistence backends coexist**: newer domains use UGS Cloud Save repositories; older systems (`LoadoutSystem`, `SquadSystem`, `FavoriteSystem`, legacy `Inventory`/`LeaderboardManager`) use local files via `DataAccessor`, with cloud mirrors (`LoadoutCloudData`, `SquadCloudData`) available for migration. `CaptainProgressRepository`/`CaptainProgressCloudData` exist as the intended replacement for the PlayFab `XpHandler`/`CaptainManager` XP system but are not yet wired into `UGSDataService`.
- **Legacy dead weight**: the entire `System/Playfab/` tree plus `System/Xp/XpHandler.cs` are inert (PlayFab disabled) — safe to describe as reference-only, superseded by UGS auth/data/leaderboards/analytics; two distinct quest systems exist (new `GameModeProgressionService` chain vs. legacy `Quest`/`QuestSystem`+`UserJourneySystem`+CTA engine).

---

## System — Dialogue Runtime, Rewind & Audio

This area bundles three loosely related runtime systems that live under `Assets/_Scripts/System/`. The **Dialogue Runtime** is an event-channel-driven, view-resolved conversation player: a `DialogueManager` coroutine walks the lines of a `DialogueSet` ScriptableObject and delegates presentation to one of several `IDialogueView` implementations chosen per channel (main menu, in-game radio, reward). The **Rewind System** is a self-contained time-rewind framework: per-`FixedUpdate` snapshots of transform/active-state into fixed-size circular buffers, driven by a singleton manager that can instantly rewind or scrub-preview. The **Audio System** is a `[DefaultExecutionOrder(-1)]` DI singleton fronting two parallel pipelines — legacy Unity `AudioSource` music (with crossfade, fed by `Jukebox`) and preferred FMOD one-shot SFX (categorized menu + gameplay events), the latter governed by the in-game SFX slider via an FMOD bus and a per-instance volume helper. (Note: the middleware in use is FMOD, not Wwise.)

### Dialogue — controller & event channel
The manager is a scene `MonoBehaviour` that listens on a ScriptableObject event channel; a raise-by-id call resolves a `DialogueSet` from the library, picks a view, and runs a per-line coroutine that waits on each view's completion callback before advancing and finally hiding.
- **DialogueManager** — sealed `MonoBehaviour`; subscribes to `DialogueEventChannel.OnDialogueRequested`, looks up sets via `DialogueSetLibrary`, resolves a view via `DialogueViewResolver`, optionally toggles a game/dialogue canvas, and drives the `RunSequence` coroutine (`ShowDialogueSet` → per-line `ShowLine` → `Hide`); exposes `IsPlaying`. `System/Runtime/Controller/DialogueManager.cs`
- **DialogueEventChannel** — `ScriptableObject` event channel (`CreateAssetMenu`) exposing `event Action<string> OnDialogueRequested` and a validated `Raise(setId)`; the decoupled entry point any gameplay code uses to request dialogue by id. `System/Runtime/Events/DialogueEventChannel.cs`
- **IDialogueService** — interface contract for a dialogue service: `PlayDialogueById`, `PlayDialogueSet`, `IsPlaying` (matches `DialogueManager`'s public surface). `System/Runtime/Models/IDialogueService.cs`

### Dialogue — data models (ScriptableObjects & enums)
Conversations are authored as SO assets: a library holds many sets, each set holds ordered lines plus portraits, mode, channel, and optional reward payload.
- **DialogueSetLibrary** — `ScriptableObject` (`CreateAssetMenu`) holding `List<DialogueSet>` with `GetSetById(id)` lookup. `System/Runtime/Models/DialogueSetLibrary.cs`
- **DialogueSet** — `ScriptableObject` (`CreateAssetMenu`) with `setId`, `DialogueModeType mode`, `DialogueChannel channel`, two speaker portrait `Sprite`s, `List<DialogueLine> lines`, and optional `RewardData`; also declares the `DialogueChannel` (MainMenu/InGameRadio/Reward) and `DialogueSide` (Auto/Left/Right) enums. `System/Runtime/Models/DialogueSet.cs`
- **DialogueLine** — `[Serializable]` line: `DialogueSpeaker`, `speakerName`, `TextArea` `text`, `AudioClip voiceClip`, `DialogueSide side`, `displayTime`, `isInGameMonologue`. `System/Runtime/Models/DialogueLine.cs`
- **DialogueSpeaker** — enum `None/Speaker1/Speaker2`. `System/Runtime/Models/DialogueSpeaker.cs`
- **DialogueModeType** — enum `Monologue/Dialogue/Reward`. `System/Runtime/Models/DialogueModeType.cs`
- **RewardData** — `[Serializable]` reward payload (`RewardType`, value, `Sprite`, description, `RewardRarity`, condition, unlock trigger, custom script); also declares `RewardType` (Item/Currency/XP/Unlock) and `RewardRarity` (Common/Rare/Epic/Legendary) enums. `System/Runtime/Models/RewardData.cs`

### Dialogue — view abstraction & resolver
A small strategy pattern: the resolver maps a set's `channel` to one concrete view; each view implements a three-method presentation contract.
- **IDialogueView** — presentation contract: `ShowDialogueSet(set)`, `ShowLine(set, line, onLineComplete)`, `Hide(onHidden)`. `System/Runtime/Models/IDialogueView.cs`
- **IDialogueViewResolver** — contract `IDialogueView ResolveView(DialogueSet set)`. `System/Runtime/Models/IDialogueViewResolver.cs`
- **DialogueViewResolver** — sealed `MonoBehaviour : IDialogueViewResolver`; holds serialized references to the three views and switches on `set.channel` (defaults to main-menu view). `System/Runtime/Helpers/DialogueViewResolver.cs`

### Dialogue — concrete views
Three `MonoBehaviour : IDialogueView` presenters, each with its own typewriter routine (`WaitForSecondsRealtime`) and next/skip button wiring; they differ in layout and animation strategy.
- **MainMenuDialogueView** — sealed; instantiates a `DialogueUIPrefabRefs` prefab, plays Animator pop-in/out clips, supports both monologue (single portrait) and left/right two-speaker dialogue with side resolution from `DialogueSide`/`DialogueSpeaker`, wires next/skip buttons, hides via `MonologuePopIn`/`DialoguePopIn`. `System/Runtime/View/MainMenuDialogueView.cs`
- **InGameRadioDialogueView** — sealed; compact radio-style single-panel widget (captain icon + name + body), optional `CanvasGroup` fade, click-anywhere/auto-advance options, typewriter with configurable char delay. `System/Runtime/View/InGameRadioDialogueView.cs`
- **RewardDialogueView** — sealed; emphasizes a reward panel populated from `DialogueSet.rewardData` (image/title/description/rarity), optional body typewriter, single continue button; used at mission/FTUE end. `System/Runtime/View/RewardDialogueView.cs`
- **DialogueUIController** — legacy/alternative `MonoBehaviour` presenter (not an `IDialogueView`): instantiates the prefab and drives `ShowMonologue`/`ShowDialogue` with Animator pop animations, typewriter, and `WaitingForNextPressed` gating; overlaps functionally with `MainMenuDialogueView`. `System/Runtime/View/DialogueUIController.cs`

### Dialogue — prefab references, animation & helpers
Shared prefab wiring and stateless helpers used by the views.
- **DialogueUIPrefabRefs** — `MonoBehaviour` holding all TMP/Image/RectTransform/Button references for the dialogue prefab (monologue root, left/right speakers, reward panel, next/skip buttons) plus `OnAnimInComplete`/`OnAnimOutComplete` `Action`s fired by Animation Events. `System/Runtime/References/DialogueUIPrefabRefs.cs`
- **DialogueUIAnimator** — static DOTween helper: `AnimateSpeakerIn`/`AnimateSpeakerOut` (anchored-position tweens with easing + callbacks) and a `Hide` utility. `System/Runtime/Helpers/DialogueUIAnimator.cs`
- **DialogueVisuals** — static color helper: `GetColorForSpeaker` and `GetModeColor`. `System/Runtime/Models/DialogueVisuals.cs`
- **DialogueAudioBatchLinker** — `#if UNITY_EDITOR` static editor utility to scan a set's lines for missing `voiceClip`s and mark the asset dirty (name-based matching is stubbed). `System/Runtime/Helpers/DialogueAudioBatchLinker.cs`
- **SplitterGUILayout** — `#if UNITY_EDITOR` static IMGUI helper drawing a draggable vertical/horizontal splitter handle for editor windows (outside the dialogue runtime proper). `System/Runtime/Helpers/SplitterGUILayout.cs`

### Rewind System
A drop-in time-rewind framework. A singleton manager collects all `RewindBase` objects at `Awake`, records their state each `FixedUpdate` into per-property circular buffers (capped at `TrackSeconds`), and can either instant-rewind or enter a scrub-preview mode.
- **RewindSystem** — `MonoBehaviour` singleton (`Instance`) and manager: serialized `TrackSeconds` (default 12), tracks `AvailableSeconds`/`IsRewound`/`TrackingEnabled`; `InstantRewindTimeBySeconds`, `StartRewindTimeBySeconds`/`SetTimeSecondsInRewind`/`StopRewindTimeBySeconds` (scrub preview), `RestartTracking`; `FixedUpdate` calls `Track()` or `Rewind()` on every registered object; exposes static `Action<float> BuffersRestore`. `System/RewindSystem/RewindSystem.cs`
- **RewindBase** — abstract `MonoBehaviour` base: owns `CircularBuffer<bool>` (active state) and `CircularBuffer<TransformValues>` (transform); protected `TrackObjectActiveState`/`RestoreObjectActiveState`, `TrackTransform`/`RestoreTransform` (plus stubbed Animator/Audio regions); abstract `Track()`/`Rewind(seconds)` and an `Init()` hook. `System/RewindSystem/RewindBase.cs`
- **GenericRewind** — concrete `RewindBase` with `[SerializeField]` toggles `trackObjectActiveState`/`trackTransform`; routes `Track()`/`Rewind()` to the base helpers. `System/RewindSystem/GenericRewind.cs`
- **CircularBuffer<T>** — generic fixed-capacity ring buffer sized to `1 / Time.fixedDeltaTime` records/sec; `WriteLastValue`, `ReadLastValue`, `ReadFromBuffer(seconds)` with modular index math for time-offset reads. `System/RewindSystem/CircularBuffer.cs`
- **TransformValues** — `[Serializable]` position/rotation/scale snapshot struct-like class. `System/RewindSystem/TransformValues.cs`
- **OptionalParticleSettings** — `[Serializable]` struct wrapping an `enabled` bool, used with a custom drawer to gate optional particle tracking (currently commented out in `GenericRewind`). `System/RewindSystem/OptionalParticleSettings.cs`
- **OptionalPropertyDrawer** — `#if UNITY_EDITOR` `[CustomPropertyDrawer(typeof(OptionalParticleSettings))]` drawing the value field with an enable checkbox on the right. `System/RewindSystem/RewindDrawer.cs`
- **SaveLoad** — static persistence helper using `BinaryFormatter` to save/load a `List<TransformValues>` to `Application.persistentDataPath/savedGames.gd`. `System/RewindSystem/SaveLoad.cs`

### Audio — central service (DI singleton)
`AudioSystem` is the single audio service, force-early via `[DefaultExecutionOrder(-1)]`, with a static `Instance` plus Reflex `[Inject] GameSetting`. It runs two pipelines side by side and subscribes to `GameSetting` static events to keep volumes/mutes in sync.
- **AudioSystem** — `MonoBehaviour` service. Legacy Unity `AudioSource` **music** path: dual `musicSource1/2` with `PlayMusicClip`, `PlayNextMusicClip`, `PlayMusicClipWithFade`, `PlayMusicClipWithCrossFade`, `StopAllSongs`, `IsMusicSourcePlaying`, plus `AudioMixer` volume setters. Preferred **FMOD SFX** path: inspector-wired `EventReference` per category, dictionaries `MenuAudioEvents`/`GameplaySFXEvents`, `PlayMenuAudio`, `PlayGameplaySFX` (2D + spatialized overloads), and low-level `PlaySFXEvent`/`PlaySFXEventAttached`; resolves an FMOD SFX **bus** (`sfxBusPath`, default `bus:/`) whose volume+mute follow `GameSetting.SFXLevel`/`SFXEnabled`, with per-category volume scaling (BlockDestroy/Explosion) and a sliding-window throttle for BlockDestroy bursts; legacy `PlaySFXClip(AudioClip)` remains for old callers. `System/Audio/AudioSystem.cs`
- **MenuAudioCategory** — enum of 12 UI/menu SFX categories (OptionClick, OpenView, SwitchView, CloseView, SmallReward, BigReward, Upgrade, Denied, Confirmed, LetsGo, SwitchScreen, RedeemTicket). `System/Audio/AudioSystem.cs`
- **GameplaySFXCategory** — enum of 37 in-game SFX categories (BlockDestroy, Shield Activate/Deactivate, MineExplode, ProjectileLaunch, CrystalCollect, VesselImpact, GameEnd, ScoreReveal, Pause Open/Close, GunFire, BoostActivate, Explosion, CreatureDeath, Drift Start/End, EnergyGain, SpeedBurst, CrystalSkim, Joust Scored/Received, four Element*Received, four Comeback*, four JoustBuff*, TrackImpact, FloraCollision, CreatureBlockHit). `System/Audio/AudioSystem.cs`

### Audio — music playlist (Jukebox)
The music pipeline's driver: a persistent singleton that builds a playlist from song SOs and pushes clips into `AudioSystem`'s legacy music sources.
- **Jukebox** — `SingletonPersistent<Jukebox>` with Reflex `[Inject] AudioSystem`; builds a `Dictionary<string, Song>` from `SO_Song[]` (+ an `onDeathSong`), plays random/sequential/specific songs, auto-advances when music stops, and reacts to `GameSetting.OnChangeMusicEnabledStatus`. `System/Audio/Jukebox.cs`
- **Song** — plain runtime wrapper around an `SO_Song` (title/description/author/`AudioClip Clip`). `System/Audio/Song.cs`

### Audio — supporting FMOD & SOAP integration (referenced from outside scope)
The FMOD one-shot volume plumbing and the SOAP event bridge that lets designer-wired listeners fire gameplay SFX by category.
- **FMODOneShotVolumeHelper** — static helper (`CosmicShore.Gameplay.Audio`) reproducing FMOD's create/start/release one-shot sequence with a `setVolume()` in between so one-shots honor the SFX slider; `PlaySFXOneShot(worldPosition)` and `PlaySFXOneShotAttached(gameObject)`, short-circuiting at `volume <= 0`. `Controller/FX/FMODOneShotVolumeHelper.cs`
- **ScriptableEventGameplaySFX** — Obvious SOAP `ScriptableEvent<GameplaySFXCategory>` asset; a category-typed event channel raised by gameplay to request an SFX. `ScriptableObjects/SOAP/ScriptableGameplaySFX/ScriptableEventGameplaySFX.cs`
- **EventListenerGameplaySFX** — SOAP `EventListenerGeneric<GameplaySFXCategory>` component; inspector-wired `EventResponse[]` mapping a raised category to a `UnityEvent<GameplaySFXCategory>` (typically `AudioSystem.PlayGameplaySFX`). `ScriptableObjects/SOAP/ScriptableGameplaySFX/EventListenerGameplaySFX.cs`

### Interactions & patterns
- **Dialogue is fully decoupled via a SO event channel.** Any gameplay/UI code (FTUE, menus, mission flow) calls `DialogueEventChannel.Raise(setId)`; `DialogueManager` is the only listener and fans out to channel-specific `IDialogueView`s through `DialogueViewResolver`. Data lives entirely in SO assets (`DialogueSetLibrary` → `DialogueSet` → `DialogueLine`/`RewardData`), so conversations are authored without code.
- **Audio uses Reflex DI + a static Instance.** `AudioSystem` and `Jukebox` both `[Inject]` their dependencies (`GameSetting`, `AudioSystem`) but fall back to `FindFirstObjectByType` when auto-created by `AppManager.EnsureService` before the container is built. `AudioSystem` runs at execution order -1 so it's ready before other systems.
- **Two audio pipelines, one slider.** Music is legacy Unity `AudioSource` (mixer bus `MusicVolume`, driven by `Jukebox`); SFX is FMOD, where the whole SFX bank obeys `GameSetting.SFXLevel/SFXEnabled` via the FMOD bus, and one-shots additionally flow through `FMODOneShotVolumeHelper` (per-instance volume) to avoid double-attenuation. `GameSetting`'s static change events (`OnChangeMusicLevel`, `OnChangeSFXLevel`, `OnChangeMusicEnabledStatus`, `OnChangeSFXEnabledStatus`) are the sync channel.
- **Gameplay SFX are raised through SOAP, not called directly.** Gameplay code raises `ScriptableEventGameplaySFX` with a `GameplaySFXCategory`; an `EventListenerGameplaySFX` in the scene routes that to `AudioSystem.PlayGameplaySFX`, which looks up the wired FMOD `EventReference`, applies per-category volume scaling/throttling, and plays it (2D or spatialized). Continuous emitters (drift, proximity boost) use FMOD directly via sibling FX controllers.
- **Rewind is a standalone subsystem** with its own singleton (`RewindSystem.Instance`) that discovers `RewindBase` objects at `Awake` and records/restores state through generic `CircularBuffer`s each `FixedUpdate`; it does not depend on the dialogue or audio systems and is currently self-contained (Animator/Audio tracking hooks are stubbed).

---

## UI — Vessel HUDs, Screens, Modals & Interfaces

This is the entire player- and app-facing UI layer under `Assets/_Scripts/UI/`. It covers per-vessel combat HUDs (an MVC controller/view pair per vessel), the shared minigame HUD family that hosts them (`MiniGameHUD` and its menu/multiplayer/mode subclasses), end-game scoreboards and player/tournament cards, the universal domain-volume gauge and off-screen objective indicator, the whole Menu_Main navigation shell (`ScreenSwitcher` + `IScreen` screens), all modal dialogs (`ModalWindowManager` family), plus stats-reporting glue (UGS leaderboards, stat providers/modules). Almost everything is a `MonoBehaviour` driven by SOAP `ScriptableEvent` channels off `GameDataSO`, per-vessel action executors, and the `HostConnectionDataSO`/`AuthenticationDataVariable`/`FriendsDataSO` containers; runtime data (`GameDataSO`, `AudioSystem`, `PlayerDataService`, etc.) arrives via Reflex `[Inject]`. Everything lives in `CosmicShore.UI` except `UGSStatsManager` (`CosmicShore.Core`).

### Interfaces & HUD contracts (`UI/Interfaces/`)
The MVC and screen-lifecycle contracts the rest of the area implements.
- **IVesselHUDController** — per-vessel HUD controller contract: `Initialize(IVesselStatus)`, `Subscribe/UnsubscribeFromEvents`, `ShowHUD/HideHUD`, `SetBlockPrefab`. `UI/Interfaces/IVesselHUDController.cs`
- **IVesselHUDView** — empty marker interface for vessel HUD views. `UI/Interfaces/IVesselHUDView.cs`
- **IMiniGameHUDController** / **IMiniGameHUDView** — near-empty placeholder contracts for the minigame HUD MVC (extension points). `UI/Interfaces/IMinigameHUDController.cs`, `IMinigameHUDView.cs`
- **IScreen** — menu-screen lifecycle (`OnScreenEnter`/`OnScreenExit`) called by `ScreenSwitcher`. `UI/Interfaces/IScreen.cs`
- **IObjectiveProvider** — supplies the world-space `Transform` an `ObjectiveIndicator` points at (`TryGetObjective`); implemented per game mode. `UI/Interfaces/IObjectiveProvider.cs`
- **IShipCatalog** — (file `IVesselCatalog.cs`) enumerates `VesselClassType`s. `UI/IVesselCatalog.cs`
- **IStatExposable** — `GetExposedStats()` dictionary contract consumed by `UniversalStatsProvider`. `UI/IStatExposable.cs`

### Per-vessel HUD MVC (`UI/Controller/` + `UI/View/`)
Each playable vessel has a controller (subscribes to that vessel's action executors / effect SOs) plus a view (holds the Images/TMP and tween juice). All controllers extend `VesselHUDController`; all views extend `VesselHUDView`. Controllers gate on `IsLocalUser && !IsInitializedAsAI` so only the local human's HUD reacts.
- **VesselHUDController** — base controller: caches `R_VesselActionHandler`, resolves a `VesselHUDView`, toggles per-input highlight Images via `Actions.OnInputEventStarted/Stopped`, forwards `SetBlockPrefab` to view + legacy `SilhouetteController`. `UI/Controller/VesselHUDController.cs`
- **VesselHUDView** — abstract base view: `HighlightBinding` list (InputEvent→Image), DOTween `Show/Hide` fade via `HUDAnimationSettingsSO`, `TrailBlockPrefab`. `UI/View/VesselHUDView.cs`
- **MantaVesselHUDController** / **MantaVesselHUDView** — skimmer overcharge counter: listens to `SkimmerOverchargeCollectPrismEffectSO` events, drives a radial fill + prism-count text, fires `ToastChannel` countdown/"OVERCHARGED!" toasts. `UI/Controller/MantaVesselHUDController.cs`, `UI/View/MantaVesselHUDView.cs`
- **RhinoVesselHUDController** / **RhinoVesselHUDView** — skimmer-scale icon, crystal-explosion slow counter (dedup `HashSet` of slowed vessels), line-icon flash, skimmer-debuff timer; reads `ShieldSkimmerScaleDriver` + `ScriptableEventVesselImpactor`/`ScriptableEventSkimmerDebuffApplied` SOAP channels. `UI/Controller/RhinoVesselHUDController.cs`, `UI/View/RhinoVesselHUDView.cs`
- **SerpentVesselHUDController** / **SerpentVesselHUDView** — seed-wall shield sprite (0–4 from a resource index) + 4 boost "pips" driven by `ConsumeBoostActionExecutor` snapshot/consume events, with per-pip fill coroutines. `UI/Controller/SerpentVesselHUDController.cs`, `UI/View/SerpentVesselHUDView.cs`
- **SparrowHUDController** / **SparrowHUDView** — missile-ammo icon stages, boost/heat fill from `OverheatingActionExecutor` (`Heat01`), weapon-mode (stationary) icon from `ScriptableEventBool`, and blocked-input red-pulse highlights from `ScriptableEventInputEventBlock`; reads `FireGunActionExecutor.OnAmmoChanged`. `UI/Controller/SparrowHUDController.cs`, `UI/View/SparrowHUDView.cs`
- **DolphinVesselHUDView** — charge-boost sprite stepped from a normalized 0–1 charge (no dedicated controller). `UI/View/DolphinVesselHUDView.cs`
- **SquirrelVesselHUDView** — the richest view (no controller): boost fill tinted by domain/source color with crystal-surge flash, drift icon rotation/tint/double-drift juice, danger-ring, shield/crystal punch juice — the Squirrel HUD view fed by `SquirrelVesselHUDView` juice calls (routed from `SquirrelVesselHUDView`'s gameplay hooks). `UI/View/SquirrelVesselHUDView.cs`
- **MinigameHUDContainer** — empty placeholder MonoBehaviour. `UI/Controller/MinigameHUDContainer.cs`
- **TrailPool** — plain C# helper (not a MonoBehaviour) that builds a UI pool of trail-block rows for a HUD trail preview, with explicit pixel-layout or legacy world→UI math, drift-yaw smoothing, driven by a `VesselPrismController`. `UI/Controller/TrailPool.cs`

**Vessel-HUD SOAP payloads / helper SOs (`UI/Controller/`, `UI/View/`):**
- **InputEventBlockPayload** + **ScriptableEventInputEventBlock** — struct (Input/Started/Ended/TotalSeconds) + its custom `ScriptableObject` event (Sparrow blocked-input highlights). `UI/Controller/`
- **BoostChangedPayload** + **ScriptableEventBoostChanged** — shared global boost channel payload (multiplier, max, source domain, source `IVesselStatus` for self-filtering). `UI/View/`
- **ControllerButtonIconReferences** — active/inactive sprite swap with fade for a controller-glyph Image. `UI/View/ControllerButtonIconReferences.cs`
- **CloakSeedWallActionSO** — a `ShipActionSO` (cloak + seed-wall action config, ghost/prism-cloak materials) that lives under `UI/View/` but is a vessel-action SO delegating to `CloakSeedWallActionExecutor`. `UI/View/CloakSeedWallActionSO.cs`

### Elemental bars (`UI/View/ElementalBarsView.cs`)
The shared four-element "flower" buff/debuff widget every vessel HUD can host.
- **ElementalBarsView** — renders 4 elements × 5 rotated petal Images; distributes an integer level [-5,15] round-robin into per-petal tick colors via `ElementalBarsConfigSO`, animates buff scale-pop / debuff shake+flash, and exposes `SetLevel`, `RefreshAllBars`, `JuiceCrystalCollected/JuiceJoust/JuiceDrift*` and scale APIs; zero-authoring auto-builds flowers + loads petal sprites from Resources. Driven by `SilhouetteController` on each vessel. `UI/View/ElementalBarsView.cs`

### Minigame HUD family (`UI/MiniGameHUD.cs` + subclasses, `UI/View/MinigameHUDView.cs`)
The in-game HUD root that hosts score, timers, ready button, connecting flow, pre-game cinematic, objective indicator, domain-volume gauge, and dynamic player/AI score entries; reparents vessel HUDs into itself via `onShipHUDInitialized`. `MonoBehaviour`s wired through `GameDataSO` SOAP events, injected `GameDataSO`+Reflex `Container`.
- **MiniGameHUD** — base HUD: subscribes to `OnClientReady`/`OnMiniGameTurnStarted`/`OnMiniGameTurnEnd`/reset; runs the connecting-panel→pre-game-cinematic→ready-button unlock sequence (UniTask), builds local + AI `PlayerScoreEntry` cards, resolves domain colors from `ThemeManagerData`, auto-creates the mode-appropriate `IObjectiveProvider` + `ObjectiveIndicator` and the `DomainVolumeIndicator` on the pause button, handles drone counters/silhouette/pip/ShipHUD reparent events; detaches stat handlers in `OnDestroy` (B15). `UI/MiniGameHUD.cs`
- **MiniGameHUDView** — the view: score/left/right/round-time/lifeform TMP, ready button, connecting panel + its typewriter/dots animators, `PlayerScoreContainer`+`PlayerScoreEntry` prefab, DOTween view/connecting fades via `HUDAnimationSettingsSO`. `UI/View/MinigameHUDView.cs`
- **MultiplayerHUD** — HexRace/Joust/Crystal-Capture HUD: per-domain score panels (ally-left / opposing-right `DomainScorePanel`s) when `MultiplayerHUDView.HasDomainPanelWiring`, else legacy per-player cards; reads server-synced `GetDomainMetricSum`, rebuilds on a `Player.Domain` layout signature change, subscribes to `OnAnyStatChanged`/`OnPlayerAdded`/`OnDomainMetricSumsChanged`. `UI/MultiplayerHUD.cs`
- **MultiplayerHUDView** — extends `MiniGameHUDView` with ally/opposing domain containers + `DomainScorePanel` prefab wiring + `ClearDomainPanels`. `UI/View/MultiplayerHUDView.cs`
- **MenuMiniGameHUD** — Menu_Main freestyle HUD: Volume/Pause button → `MenuCrystalClickHandler.ToggleTransition` (and gamepad-Start), reparents vessel HUDs, re-shows local HUD on `OnPlayerPairInitialized` after a swap, self-attaches `DomainVolumeIndicator`, instantiates the PauseMenu prefab; subscribes to `MenuFreestyleEventsContainerSO` transitions. `UI/MenuMiniGameHUD.cs`
- **WildlifeBlitzHUD** — extends `MiniGameHUD`; shows *remaining* score to a target, wires `onSetScoreTarget`/`onScoreChanged`/`onLifeFormCounterUpdated` blitz SOAP events. `UI/WildlifeBlitzHUD.cs`
- **HexRaceHUDView** — empty `MiniGameHUDView` subclass, HexRace extension point. `UI/HexRaceHUDView.cs`
- **GameCanvas** — legacy game-canvas root (HUD ref, ship-button panel, awards/XP/crystal displays, quest-complete handler, `onShipHUDInitialized` reparent). `UI/GameCanvas.cs`
- **ConnectingPanelController** — in-game "CONNECTING TO SHORE…" pre-cinematic panel: own camera, animated dots, mode+intensity text, Maelstrom domain-rank list; `MiniGameHUD` awaits its `ShowAsync`. `UI/ConnectingPanelController.cs`

### Off-screen objective indicator (`UI/ObjectiveIndicator.cs`, `UI/ObjectiveArrowGraphic.cs`)
Edge-of-screen arrow pointing at a mode-specific objective, with a runtime-built sub-canvas for cheap redraws.
- **ObjectiveIndicator** — clamps an icon to the parent-rect edge in the objective's screen direction, rotates + shows distance, uses `IObjectiveProvider`; `CreateRuntime` builds a self-contained sub-canvas + `ObjectiveArrowGraphic`; ProfilerMarker-instrumented `LateUpdate`. `UI/ObjectiveIndicator.cs`
- **ObjectiveArrowGraphic** — procedural `MaskableGraphic` chevron (three layered concave hexagons, lime palette) with alpha/scale pulse and no sprite dependency. `UI/ObjectiveArrowGraphic.cs`

### Domain-volume gauge & team-volume UI (`UI/DomainVolumeIndicator.cs`, `UI/DomainVolumeHexGraphic.cs`, `UI/VolumeUI.cs`, …)
The universal hex pause-button gauge showing per-domain live volume vs the cell's phase ladder, plus the older radial team-volume fill.
- **DomainVolumeIndicator** — samples the local player's `Cell` (`GetDomainVolume`, `FrenzyEnterVolume`, `ResolvedThresholds`, fauna spawn cycle), lerps per-domain fills, resolves domain colors from `ThemeManagerData.ColorSet`, self-constructs a `DomainVolumeHexGraphic` and hides the host button face; `SetGameData` for the AddComponent path. `UI/DomainVolumeIndicator.cs`
- **DomainVolumeHexGraphic** — procedural `MaskableGraphic` pointy-top hexagon split into 3 domain sectors filling radially inward, concentric phase-threshold rings, dominant-domain center hex, and an outer spawn-cycle arc; rebuilds only on meaningful `SetState` deltas. `UI/DomainVolumeHexGraphic.cs`
- **VolumeUI** — material-driven 4-radius team-volume fill Image, reset on `OnResetForReplay`. `UI/VolumeUI.cs`
- **LocalVolumeUIController** — single-player: polls `GameDataSO.GetTeamVolumes()` every 0.5s into `VolumeUI` between turn start/end. `UI/LocalVolumeUIController.cs`
- **NetworkVolumeUIController** — `NetworkBehaviour` server→client sync of team volumes via ClientRpc, with late-joiner ServerRpc request. `UI/NetworkVolumeUIController.cs`
- **CurrentScore** — TMP showing Jade-minus-Ruby volume from sorted round stats (injected `GameDataSO`). `UI/CurrentScore.cs`

### End-game scoreboards & player cards (`UI/Scoreboard.cs`, `UI/PlayerScoreCard.cs`, …)
The results screen: one card per player/domain, victory banner, crystal rewards, host/client lobby buttons, tournament Continue.
- **Scoreboard** — base end-game board: shown on `OnShowGameEndScreen`, builds `PlayerScoreCard`s from `GameDataSO.Results` (or sorted `RoundStatsList`), sets domain victory banner, awards crystals (winner-flat or tournament `{2,1,0}` placement), configures host (Play Again/Main Menu/Continue) vs client (Leave Lobby) buttons, dynamic stat rows from a `ScoreboardStatsProvider`; overridable `SortPlayers`/`FormatPlayerScore`/`FormatSecondaryStat`. `UI/Scoreboard.cs`
- **DuelForCellScoreboard** — `Scoreboard` subclass: descending points sort, integer score format. `UI/DuelForCellScoreboard.cs`
- **CoOpScoreBoard** — `Scoreboard` subclass with an opponent-score TMP field. `UI/CoOpScoreBoard.cs`
- **PlayerScoreCard** — one results row: avatar/name/formatted score, domain background tint, "+N" crystal reward + secondary-stat panels (shared `DataPanels` root auto-shown), entrance + counter-roll animations via `CardEntranceAnimator`/`ScoreNumberAnimator`. `UI/PlayerScoreCard.cs`
- **EndShapeDetailHUD** — freestyle shape-drawing results panel (name/time/par/accuracy/star rating from `ShapeScoreData`, screenshot/exit events). `UI/EndShapeDetailHUD.cs`
- **StatRowUI** — icon+label+value row used in the scoreboard stats container. `UI/StatRowUI.cs`

### Stats providers, modules, profiles & leaderboards (`UI/*StatsProvider.cs`, `UI/*PlayerStatsProfile.cs`, `UI/UGSStatsManager.cs`, …)
Bridges game trackers → scoreboard stat rows and → UGS cloud/leaderboards.
- **ScoreboardStatsProvider** — abstract `GetStats()→List<StatData>`; **StatData** struct (label/value/icon). `UI/ScoreboardStatsProvider.cs`
- **UniversalStatsProvider** + **StatBinding** — binds `IStatExposable` tracker keys to `StatModuleSO`s and formats them; has custom editor. `UI/UniversalStatsProvider.cs`
- **WildlifeBlitzStatsProvider** — pulls lifeforms-killed + crystals from a `SinglePlayerWildlifeBlitzScoreTracker`. `UI/WildlifeBlitzStatsProvider.cs`
- **StatModuleSO** (+ `ValueFormatType` enum) — `ScriptableObject` defining a stat's label/icon/format/binding path. `UI/StatModuleSO.cs`
- **UGSStatsManager** (`CosmicShore.Core`, DI singleton) — evaluates/reports per-mode high scores & vessel telemetry, submits to UGS Leaderboards via `LeaderboardConfigSO`, delegates persistence to `UGSDataService` repos; `TrackPlayAgain`. `UI/UGSStatsManager.cs`
- **LeaderboardConfigSO** — `(GameMode,Intensity)→leaderboardId` map + active-mode list, cached; has custom editor + `ActiveGameModesWindow`. `UI/LeaderboardConfigSO.cs`
- **PlayerStatsProfile** — serializable aggregate of the four per-mode profiles. `UI/PlayerStatsProfile.cs`
- **WildlifeBlitzPlayerStatsProfile / CrystalCapturePlayerStatsProfile** (high-score dicts, higher-better) and **HexRacePlayerStatsProfile / JoustPlayerStatsProfile** (best-time dicts, lower-better) — per-mode best-score records keyed `Mode_Intensity`. `UI/*PlayerStatsProfile.cs`

### Tournament / Maelstrom UI cards (`UI/Tournament*.cs`)
Cards for the Maelstrom (Tournament) results & round-scroll, all tinting to `DomainColorPaletteSO`.
- **TournamentRoundCard** — one round: header (index/mode/winning domain) + a `TournamentPlayerCard` per player ordered by cumulative total; `Setup`/`SetupPreview`. `UI/TournamentRoundCard.cs`
- **TournamentPlayerCard** — one player's round row (avatar, name, round score, domain total). `UI/TournamentPlayerCard.cs`
- **TournamentSummaryPlayerCard** — summary-screen player row (total score) with staggered pop entrance. `UI/TournamentSummaryPlayerCard.cs`
- **TournamentDomainScoreView** — one domain standings row (place/name/points, domain-tinted, "(You)" badge). `UI/TournamentDomainScoreView.cs`

### Duel-for-Cell stats UI (`UI/DuelCellStatsRoundUIController.cs`, `UI/DuellCellStatsRowUIController.cs`)
The detailed two-player, two-round prism/volume stat table for cellular-duel.
- **DuelCellStatsRoundUIController** — subscribes to both players' `OnAnyStatChanged`, snapshots round-1 to compute round-2 deltas, toggles the panel with gamepad d-pad up; defines the `StatsRowData` struct. `UI/DuelCellStatsRoundUIController.cs`
- **DuellCellStatsRowUIController** — renders one `StatsRowData` row (12 prism/volume TMP fields + score), `CleanupUI`. `UI/DuellCellStatsRowUIController.cs`

### Vessel selection (`UI/VesselSelectionPanelController.cs`, `UI/VesselSelectionPanelUI.cs`, `UI/VesselCardView.cs`, `UI/VesselSelection.cs`, `UI/VesselButtonPanel.cs`)
The single-player/menu in-game vessel-swap panel (network-aware variant lives in `Controller/Multiplayer`).
- **VesselSelectionPanelController** — collects `ShipCardView` cards, snapshots the current vessel (pose/course/AI/active), spawns a replacement via `VesselSpawner`, transfers state, restores player control with a yield delay; pushes selection into `GameDataSO`. `UI/VesselSelectionPanelController.cs`
- **VesselSelectionPanelUI** — pure show/hide via `CanvasGroup` + exposes the card container. `UI/VesselSelectionPanelUI.cs`
- **ShipCardView** (file `VesselCardView.cs`) — one placed vessel card: number/SO/`VesselClassType`, click event, selected marker + active/inactive icon. `UI/VesselCardView.cs`
- **ShipButtonPanel** (file `VesselButtonPanel.cs`) — fades a group of button Images in/out. `UI/VesselButtonPanel.cs`
- **ShipSelection** (file `VesselSelection.cs`) — near-stub TMP-dropdown vessel picker (Hangar wiring commented out). `UI/VesselSelection.cs`

### Misc in-game HUD widgets (`UI/PipUI.cs`, `UI/ResourceDisplay.cs`, `UI/ThumbCursor.cs`, …)
- **PipUI** — picture-in-picture toggle (small/large scale + position, mirrored). `UI/PipUI.cs`
- **ResourceDisplay** — multi-mode resource gauge (legacy fuel images / slider fill / sprite swap) with fill-up/down animations; has inline custom editor. `UI/ResourceDisplay.cs`
- **ResourceButton** — bucketed gauge-level sprite from a 0–1 charge. `UI/ResourceButton.cs`
- **ThumbCursor** / **ThumbPerimeter** — touch-joystick cursor + perimeter visuals from `IInputStatus` (both currently self-disable / suspended). `UI/ThumbCursor.cs`, `UI/ThumbPerimeter.cs`
- **Minimap** — orbit camera around the active `Cell` following the vessel. `UI/Minimap.cs`
- **PauseButton** — toggles `PauseSystem`. `UI/PauseButton.cs`
- **PauseMenu** — in-game pause panel: resume/replay/main-menu (host-only gated), music/invert toggles, opens settings modal, host/client `NetworkManager` guards, plays pause SFX. `UI/PauseMenu.cs`
- **TestMiniGameEvents** — debug logger for round-start/end SOAP events. `UI/TestMiniGameEvents.cs`

### Menu navigation shell — ScreenSwitcher (`UI/ScreenSwitcher.cs`)
The horizontal sliding-panel navigator for Menu_Main, plus the modal stack.
- **ScreenSwitcher** — maps `MenuScreens` (STORE/ARK/HOME/PORT/HANGAR/PROFILE) enum to panel `RectTransform`s, slides one viewport-width per index (aspect-safe), caches `IScreen` components and fires enter/exit, manages the `ModalWindows` stack + `PlayerPrefs` return-state, gamepad trigger/button nav, host-only ARK gating, and freestyle mode (hides nav/screens via CanvasGroup, disables `EventSystem.sendNavigationEvents`, closes modals) on `MenuFreestyleEventsContainerSO` events; injects `HostConnectionDataSO`. `UI/ScreenSwitcher.cs`

### Menu screens (`UI/Screens/`)
Screen containers under the switcher; several implement `IScreen`.
- **HomeScreen** — home panel; player name from injected `PlayerDataService.OnProfileChanged`, first-launch flow (disabled). `UI/Screens/HomeScreen.cs`
- **ArcadeScreen** — Arcade panel toggling Explore/Loadout sub-views via CanvasGroups + audio. `UI/Screens/ArcadeScreen.cs`
- **StoreScreen** (extends `View`) — captain/game purchase cards, crystal/ticket balances animated, daily-challenge ticket card; driven by `CatalogManager` events + injected `CaptainManager`. `UI/Screens/StoreScreen.cs`
- **HangarScreen** (`IScreen`) — vessel grid + detail views (new flow) with staggered fade-in, unlock-state refresh, legacy overview/abilities/training fallback. `UI/Screens/HangarScreen.cs`
- **LeaderboardsMenu** (`IScreen`) — game/vessel selection + high-score list from `LeaderboardManager`; per-row highlight of the local player. `UI/Screens/LeaderboardsMenu.cs`
- **EpisodeScreen** — episode cards from `SO_EpisodeList`, cloud completion/unlock via injected `UGSDataService`, purchasable episodes wired to `IAPManager`. `UI/Screens/EpisodeScreen.cs`
- **BootStatusBroadcaster** — translates auth/party/game-data boot SOAP events into `BootStatusRequest` raises (connecting/joining/creating/host-ready/connection-lost), suppressing retry during launch/party transitions. `UI/Screens/BootStatusBroadcaster.cs`
- **BootStatusPanel** — pure view for the splash status text + retry button driven by `ScriptableEventBootStatusRequest` in / retry-requested out; fails loud on missing wiring. `UI/Screens/BootStatusPanel.cs`
- **PartyInviteNotificationPanel** — bottom-left invite popup: subscribes to `HostConnectionDataSO.OnInviteReceived`/`OnInviteResolved`, avatar+name+Accept/Decline delegating to `PartyInviteController`, 3s auto-hide, latest-wins. `UI/Screens/PartyInviteNotificationPanel.cs`

### Modals (`UI/Modals/`)
Overlay dialogs; most extend `ModalWindowManager` (open/close animation + modal-stack integration).
- **ModalWindowManager** — base modal: `ModalWindowIn/Out` with Animator + CanvasGroup, `PushModal/PopModal` on `ScreenSwitcher`, gamepad-B close, external-deactivation recovery, open/close audio. `UI/Modals/ModalWindowManager.cs`
- **ArcadeGameConfigureModal** — the large game-launch config modal: game meta, intensity buttons, player-count + domain-count `IntStepper`s, Screen-1→Screen-2 domain/vessel selection with per-player `DomainAvatarChip`s, ready-up sync via `ArcadeConfigSyncManager` (host/client modes), `SyncAllGameDataForLaunch` → `GameDataSO.ConfigurePlayerCounts`/`SyncFromArcadeGame` → `InvokeGameLaunch`; `ShouldLocalPlayerLaunch` decides launch authority. `UI/Modals/ArcadeGameConfigureModal.cs`
- **ArcadeGameConfigSO** — ephemeral UI selection state (game/intensity/player/domain/ship/domain) with `ResetState`. `UI/Modals/ArcadeGameConfigSO.cs`
- **ScriptableEventArcadeGameConfig** — SOAP event carrying that config. `UI/Modals/ScriptableEventArcadeGameConfig.cs`
- **GameSettingsPanelController** — self-wiring 4-tab options panel (General/Display/Performance/Other): populates dropdowns from enums, binds ON/OFF button pairs + sliders, routes to `DisplayGraphicsSettings`/`GameSetting`/`AccessibilitySettings`/`AnalyticsServiceFacade`, context-locks perf settings to the main menu (injects `ApplicationStateDataVariable`). `UI/Modals/GameSettingsPanelController.cs`
- **SettingsModal** — thin `ModalWindowManager` forwarding toggles/sliders to injected `GameSetting`. `UI/Modals/SettingsModal.cs`
- **SettingsTabBar** — self-wiring tab bar (button/content/underline/label scale). `UI/Modals/SettingsTabBar.cs`
- **ProfileModal** — display-name edit (UGS `PlayerDataService` + `AuthenticationService` sync), avatar sprite, random-name typewriter, legacy PlayFab email login/link scaffolding. `UI/Modals/ProfileModal.cs`
- **PurchaseConfirmationModal** — captain/upgrade/ticket purchase confirm with animated crystal/ticket balances + icon emitter. `UI/Modals/PurchaseConfirmationModal.cs`
- **DailyChallengeModal** — daily-challenge countdown + ticket balance, launches via `DailyChallengeSystem`. `UI/Modals/DailyChallengeModal.cs`
- **FactionMissionModal** — faction-mission launcher (intensity + `Arcade.LaunchMission`). `UI/Modals/FactionMissionModal.cs`
- **HangarTrainingModal** — training-game selection, intensity buttons w/ reward tiers via `TrainingGameProgressSystem`, preview clip, `Arcade.LaunchTrainingGame`. `UI/Modals/HangarTrainingModal.cs`
- **AppInitializationModal** — Menu_Main init overlay polling UGS auth (with offline timeout) before revealing nav/menu. `UI/Modals/AppInitializationModal.cs`
- **SceneTransitionModal** — animator "door" open/close bool driver. `UI/Modals/SceneTransitionModal.cs`

### Shared UI utilities, animation config & editors
Cross-cutting helpers and inspector tooling.
- **HUDAnimationSettingsSO** — `ScriptableObject` bundling all HUD/scoreboard/card/countdown tween timings + easings (shared by HUD, cards, vessel HUD). `UI/HUDAnimationSettingsSO.cs`
- **MenuAudio** — plays a `MenuAudioCategory` via injected `AudioSystem`. `UI/MenuAudio.cs`
- **WidescreenLayoutAdapter** — pillarboxes a full-screen rect beyond a max aspect. `UI/WidescreenLayoutAdapter.cs`
- **InfiniteScroll** — vertically looping/snapping scroll list (duplicates items above/below). `UI/InfiniteScroll.cs`
- **TooltipHandler** — pointer-driven show/hide tooltip. `UI/TooltipHandler.cs`
- **ButtonPanel** — repositions buttons between normal/bottom-edge layouts. `UI/ButtonPanel.cs`
- **VersionDisplay** — writes `Application.version` to a TMP label. `UI/VersionDisplay.cs`
- **ActiveGameModesWindow** — `EditorWindow` to pick active game modes on `LeaderboardConfigSO`. `UI/ActiveGameModesWindow.cs`
- **BenchmarkSceneHud** — in-scene benchmark overlay (live FPS/1%-low, quick graphics toggles via `DisplayGraphicsSettings`, `PerformanceBenchmarkRunner`, exit to menu). `UI/BenchmarkSceneHud.cs`
- **LeaderboardConfigSOEditor** / **UniversalStatsProviderEditor** / (inline **ResourceDisplayEditor**) — custom inspectors for leaderboard mappings, stat-binding reorderable list, and resource-display mode fields. `UI/LeaderboardConfigSOEditor.cs`, `UI/UniversalStatsProviderEditor.cs`, `UI/ResourceDisplay.cs`

### Interactions & patterns
- **SOAP-driven, injected data.** Nearly every HUD/scoreboard reads `GameDataSO` (via `[Inject]` or serialized) and subscribes to its `ScriptableEvent` channels — `OnClientReady`, `OnMiniGameTurnStarted/End`, `OnMiniGameRoundStarted/End`, `OnShowGameEndScreen`, `OnResetForReplay`, `OnLaunchGame`, `OnPlayerAdded`, `OnDomainMetricSumsChanged`, `OnPlayerPairInitialized`, `OnClientReady`. Vessel HUDs instead subscribe to per-vessel **action executors** and **Effect SOs** (`SkimmerOverchargeCollectPrismEffectSO`, `OverheatingActionExecutor`, `ConsumeBoostActionExecutor`, `ScriptableEventVesselImpactor`, etc.). Menu/party/boot UI reads `HostConnectionDataSO`, `AuthenticationDataVariable`, `FriendsDataSO`, `MenuFreestyleEventsContainerSO`.
- **NetworkVariables / server authority.** `MultiplayerHUD` renders from server-synced `GetDomainMetricSum` (never client re-sums), and attributes domains from the authoritative `Player.Domain` (NetDomain mirror), not `RoundStats.Domain`. `Scoreboard`/`PauseMenu` gate Play Again/Main Menu/Continue on `NetworkManager.IsServer`; `NetworkVolumeUIController` is itself a `NetworkBehaviour` syncing volumes via ClientRpc/ServerRpc.
- **DI.** Reflex `[Inject]` supplies `GameDataSO`, `AudioSystem`, `PlayerDataService`, `GameSetting`, `CaptainManager`, `UGSDataService`, `HostConnectionDataSO`, `ApplicationStateDataVariable`, and the Reflex `Container` (used to `GameObjectInjector.InjectRecursive` runtime-created objective providers / pause menus).
- **Domain color single source of truth.** All domain tinting resolves through `GameDataSO.ThemeManagerData` (`GetDomainUIColor` / `ColorSet.TrailHighlightColor`) or a `DomainColorPaletteSO`, matching vessels/prisms — no per-widget palettes.
- **Vessel-HUD reparenting.** Vessel prefabs raise `onShipHUDInitialized` (`ScriptableEventShipHUDData`); `MiniGameHUD`/`MenuMiniGameHUD`/`GameCanvas` reparent those HUD children under the shared game canvas so per-vessel HUDs render as siblings.
- **Leak-safe stat subscriptions.** HUDs track exactly the `IRoundStats` they subscribed to and detach in `OnDestroy` (not by iterating the roster), because `RoundStats` live on the persistent Player NetworkObjects and survive scene transitions (ScoringSystem B15).
- **Procedural, batch-friendly graphics.** `ObjectiveArrowGraphic` and `DomainVolumeHexGraphic` are custom `MaskableGraphic`s built in `OnPopulateMesh` with delta-gated rebuilds and (for the indicator) an isolating sub-canvas — no textures/fonts, minimal Canvas batch churn on the shared HUD.

---

## UI — Menu Views, Widgets & Player Data

This area covers the out-of-game menu **View** layer (Arcade/Hangar/Port/Profile/XP/Quest/DailyChallenge/FactionMission screens and their widgets), the client-side **player profile domain service** that owns display name / avatar / crystal / XP state and reconciles it with UGS cloud saves, the network-aware **vessel/ship selection** views, the **pre-game cinematic** camera flythrough, and the **privacy/consent** first-run + settings UI. The dominant pattern is a small abstract `View` base whose subclasses bind one or a list of `ScriptableObject` "models" and re-`UpdateView()` on demand, an index selector held in a shared SOAP `ScriptableVariable<int>`, and event-driven refreshes from domain services (`PlayerDataService`, `GameModeProgressionService`, `CaptainManager`) rather than polling. Data flows: SOs → `View.AssignModel(s)`/`Select` → `UpdateView()`; and `PlayerDataService.OnProfileChanged` / static `OnCrystalBalanceChanged` / `OnXPChanged` → widget refresh. All types are in the `CosmicShore.UI` namespace unless noted.

### View base & selection framework
The abstract base every menu detail-panel extends: it holds a `List<ScriptableObject>` model set plus a currently-`SelectedModel`, uses a serialized SOAP `ScriptableVariable<int>` (`shipClassTypeVariable`) as the shared "which index is selected" source of truth, and forces subclasses to implement rendering in `UpdateView()`.
- **View** — abstract `MonoBehaviour` base for model-driven menu panels; `AssignModel`/`AssignModels` (fail-loud-warn on null/empty), `Select(int)` writes the index into `shipClassTypeVariable` and re-renders, `UpdateView()` is abstract; serialized `NavGroup navGroup` + `ScriptableVariable<int> shipClassTypeVariable`. `UI/Views/View.cs`.

### Player profile & data service
The client-side profile domain layer. `PlayerDataService` is a `DontDestroyOnLoad` singleton (also DI-injectable) and the single owner of the live profile; it merges cloud data on top of a local default, persists via `UGSDataService.ProfileRepo`, and fans changes out through one instance event plus two static currency/XP events. The profile widgets and modals subscribe to those events and never mutate profile state directly except through the service's public API.
- **PlayerProfileData** — `[Serializable]` plain-data profile record: `userId`, `displayName`, `avatarId`, `crystalBalance`, `xp`, `List<string> unlockedRewardIds`, `long firstSeenUtc` (install cohort stamp). `UI/Views/PlayerProfileData.cs`.
- **PlayerDataService** — `MonoBehaviour` singleton + domain service; `[Inject] UGSDataService`, `[Inject] AnalyticsServiceFacade`, serialized `SO_ProfileIconList` + `GameDataSO`. Creates a local default profile in `Awake`, merges cloud profile (union-merges unlocked rewards) once `UGSDataService` is ready, stamps `firstSeenUtc`. Public API: `SetAvatarId`/`SetDisplayName` (immediate + debounced save), `GetCrystalBalance`/`AddCrystals`/`TrySpendCrystals` (records analytics), `GetXP`/`AddXP`, `UnlockReward`/`IsRewardUnlocked`, `GetAvatarSprite`, `RefreshProfileVisuals`. Events: instance `Action<PlayerProfileData> OnProfileChanged`, static `Action<int> OnCrystalBalanceChanged` and `OnXPChanged`. Mirrors name/avatar into `GameDataSO.LocalPlayerDisplayName`/`LocalPlayerAvatarId`. Editor-only `ApplyPendingDebugCrystals` drains the Froglet Toolbox queue. `UI/Views/PlayerDataService.cs`.
- **ProfileScreen** — `MonoBehaviour` menu profile panel; reads the `PlayerDataService.Instance` singleton (not `[Inject]`, to dodge OnEnable/inject timing), subscribes `OnProfileChanged` → refreshes `displayNameText` + `avatarImage` via `GetAvatarSprite`. `UI/Views/ProfileScreen.cs`.
- **ArcadeProfileWidget** — top-left avatar + username widget on the arcade/home screen; `[Inject] PlayerDataService`, serialized `SO_ProfileIconList` + `ProfileIconSelectView`. Inline username edit (3–25 chars) via `SetDisplayName`, avatar click opens `ProfileIconSelectView.OpenAvatar()`, refreshes on `OnProfileChanged`. `UI/Views/ArcadeProfileWidget.cs`.
- **ProfileIconSelectView** — `ModalWindowManager` two-tab (avatar grid / display-name) profile modal; `[Inject] PlayerDataService`, `GameDataSO`, and Reflex `Container` (to `GameObjectInjector.InjectRecursive` runtime-instantiated icon buttons). Builds a grid of `ProfileIconSelectButton` from `SO_ProfileIconList`, `SelectIcon` → `dataService.SetAvatarId`, name save → `SetDisplayName`. Also declares **enum ProfileModalTab** (`Avatar=0`, `DisplayName=1`). `UI/Views/ProfileIconSelectView.cs`.
- **AvatarButtonView** — reusable avatar button; `Initialize(Sprite, isSelected, Action onClick)` wires the `Button` and toggles a `selectedHighlight`. `UI/Views/AvatarButtonView.cs`.

### Arcade / game-launch views
The Arcade ("ARK") screen's two entry surfaces: a grid explorer that populates game cards and opens the configure modal, and a legacy 4-slot loadout editor that syncs directly into `GameDataSO` and launches.
- **ArcadeExploreView** — `MonoBehaviour`; `[Inject] SO_GameList`, serialized `ArcadeGameConfigureModal`, `ArcadeDPadNav`, `DailyChallengeCard`, `VesselClassTypeVariable`. Populates/sorts `GameCard`s (favorites first, then alphabetical), gates each via `GameModeProgressionService.IsGameModeUnlocked` (`SetLocked`), reacts to `CatalogManager.OnLoadInventory` + `GameModeProgressionService.OnProgressionChanged`, wires `CallToActionTarget`. `SelectGame` opens the configure modal; `PlaySelectedGame` saves loadout + calls `Arcade.Instance.LaunchArcadeGame`; `ToggleFavorite` via `FavoriteSystem`. `UI/Views/ArcadeExploreView.cs`.
- **ArcadeLoadoutView** — `MonoBehaviour` legacy 4-card loadout editor; `[Inject] AudioSystem`, `SO_GameList`, `GameDataSO`, serialized `SO_VesselList`, `LoadoutCard`s, and player-count/intensity `Image[]` selectors. Reads/writes `LoadoutSystem` loadouts, filters available vessels per game (skips `IsLocked`), clamps player count to `Min/MaxPlayersAllowed`; `OnClickPlayButton` → `gameData.SyncFromArcadeGame` + `gameData.InvokeGameLaunch()`. `UI/Views/ArcadeLoadoutView.cs`.

### Hangar views
The Hangar screen detail panels: a modern tabbed vessel detail/unlock view plus older `View`-subclass panels for vessel selection, per-ability display, and captain upgrades. `HangarOverviewView` is a stub kept for serialized-reference compatibility.
- **HangarVesselDetailView** — `MonoBehaviour` tabbed vessel detail (General + up to 4 ability tabs); `[Inject] AnalyticsServiceFacade`. `SetVessel(SO_Vessel)` paints name/description/abilities; unlock flow shows a spend-crystals panel, `VesselUnlockSystem.TryPurchaseVessel` gated on `GameModeProgressionService.IsVesselHangarUnlocked` (toast if locked), records `RecordVesselUnlocked`. Subscribes `VesselUnlockSystem.OnUnlockStateChanged` + `PlayerDataService.OnCrystalBalanceChanged`; exposes `OnBackPressed`. `UI/Views/HangarVesselDetailView.cs`.
- **HangarVesselSelectionView** — `View`; renders selected `SO_Vessel` (name/description/preview/locked state, Train button, `UnlockMessagePanel`) and pushes `gameplayParameter1..3` into a `HangarGameplayParameterDisplayGroup`. `UI/Views/HangarVesselSelectionView.cs`.
- **HangarAbilitiesView** — `View`; renders selected `SO_VesselAbility` (class name, ability name/description, instantiates `PreviewClip` RawImage, Train vs GoToStore button by `Vessel.IsLocked`). `UI/Views/HangarAbilitiesView.cs`.
- **HangarCaptainsView** — `View`; `[Inject] CaptainManager`. Populates a captain selection strip (`CaptainUpgradeSelectionCard`), renders selected `SO_Captain` details + upgrade requirements (XP via `CaptainManager`, crystals via `CatalogManager.GetCrystalBalance`), drives purchase through `PurchaseConfirmationModal` → `CatalogManager.PurchaseCaptainUpgrade`; encounter/lock/upgrade states, `MenuAudio` feedback. Subscribes `CaptainManager.OnLoadCaptainData`. `UI/Views/HangarCaptainsView.cs`.
- **HangarOverviewView** — `View` empty stub (no-op `UpdateView`) retained for `HangarScreen` serialized references. `UI/Views/HangarOverviewView.cs`.

### Vessel / ship selection views
Type-driven ship picker used in menus (note the file↔class name mismatch: `VesselSelection*.cs` files define `ShipSelection*` types). Selection is keyed off `GameDataSO.selectedVesselClass` (a SOAP vessel-class variable) and index, rather than raw list position.
- **ShipSelectionView** — `View` (file `UI/Views/VesselSelectionView.cs`); type-indexed slots (`ShipSelectionSlot[]`) bound to a `shipsCatalog` `SO_Vessel` list; normalizes `gameData.selectedVesselClass`/`VesselClassSelectedIndex` on enable (default Dolphin), blocks locked vessels, plays `MenuAudio`, exposes `delegate SelectionCallback OnSelect`.
- **ShipSelectionItemView** — `MonoBehaviour` single slot (file `UI/Views/VesselSelectionItemView.cs`); `Configure(SO_Vessel, isSelected, onClick)` swaps active/inactive icon + name + button, `Clear()` hides.
- **ShipSelectionSlot** — `[Serializable] struct` pairing a `VesselClassType` with its `ShipSelectionItemView`. `UI/Views/ShipSelectionSlot.cs`.

### Port / Squad views
The Port screen's squad-builder family. Most of this is inactive/stubbed since captains were removed from vessels, but the wiring remains.
- **PortSquadView** — `View`; squad leader + two rogue captain slots via `SquadMemberCard`, `ShowCaptainSelectModal(int)` opens the configure modal, `AssignCaptain` writes through `SquadSystem` (`SetSquadLeader`/`SetRogueOne`/`SetRogueTwo` + `SaveSquad`). Currently a stub (captain system removed). `UI/Views/PortSquadView.cs`.
- **PortSquadMemberConfigureView** — `ModalWindowManager`; composes a `ShipSelectionView` + `PortSquadCaptainSelectionView` + `SquadMemberCard`, cross-wires their `OnSelect` callbacks, `ConfirmCaptain` commits to `PortSquadView.AssignCaptain`. `UI/Views/PortSquadMemberConfigureView.cs`.
- **PortSquadCaptainSelectionView** — `View`; row-based captain list with selected/unselected row tint, `delegate SelectionCallback OnSelect`, `IsPlayer` toggles description-vs-flavor text; captain-list currently cleared (inactive). `UI/Views/PortSquadCaptainSelectionView.cs`.
- **PortFactionView** — `View` stub; `UpdateView()` throws `NotImplementedException`. `UI/Views/PortFactionView.cs`.

### Progression track views
Horizontally-scrolling milestone tracks driven by animation (DOTween) and progression/profile services. Both spawn card prefabs and animate a slider.
- **XPTrackView** — `MonoBehaviour`; `[Inject] PlayerDataService`, serialized `SO_XPTrackData`. Spawns milestone reward items + level labels, animates an XP `Slider` with DOTween, refreshes lock/unlock state; subscribes `PlayerDataService.OnProfileChanged` and reads `GetXP()`. `UI/Views/XPTrackView.cs`.
- **QuestTrackView** — `MonoBehaviour`; serialized `SO_GameModeQuestList`, spawns `QuestItemCard`s + description labels, drives a main + ghost `Slider`, scroll-snap, parallax depth scaling, and a choreographed DOTween "claim fanfare" that calls `GameModeProgressionService.ClaimQuestAndUnlockNext`. Card state (`QuestItemState`) derived from `GameModeProgressionService` unlock/complete checks; subscribes `OnProgressionChanged`. `UI/Views/QuestTrackView.cs`.

### Daily challenge & faction mission views
`View`-subclass detail panels for the daily-challenge and faction-mission cards, plus the daily-challenge leaderboard.
- **DailyChallengeGameView** — `View`; renders `SO_TrainingGame` (title, instantiated `PreviewClip`, intensity/ship sprites via `SO_VesselList`), three reward tiers (`GameplayRewardButton`) and a vertical progress indicator driven by `DailyChallengeSystem.Instance.RewardState` (satisfied/claimed per tier). `UI/Views/DailyChallengeGameView.cs`.
- **DailyChallengeLeaderboardView** — `View`; fetches `LeaderboardManager.DailyChallengeStatisticName` scores, populates a fixed `HighScoresContainer` (position/avatar via `SO_ProfileIconList`/`GetProfileIconByID`, name, score), highlights the local player (`AuthenticationManager.PlayFabAccount.ID`); subscribes `PlayerDataController.OnProfileLoaded`. `UI/Views/DailyChallengeLeaderboardView.cs`.
- **FactionMissionGameView** — `View`; renders `SO_Mission` description + instantiated `PreviewClip`. `UI/Views/FactionMissionGameView.cs`.

### Pre-game cinematic
Camera flythrough shown before a match's Ready prompt, driven imperatively by `MiniGameHUD`.
- **PreGameCinematicController** — `MonoBehaviour`; `Play(lookAtCenter, playerTarget)` disables the active `CustomCameraController` (via `CameraManager.Instance.GetActiveController`) and coroutine-flies the camera through serialized `waypoints` (or an auto-generated orbit path), then smoothly transitions behind the player vessel and re-enables/snaps the player camera. Skippable via serialized skip `Button`/`CanvasGroup` or gamepad South button; raises `event Action OnCinematicFinished`; `SetupSkipButton` allows runtime skip-button injection. `UI/PreGameCinematic/PreGameCinematicController.cs`.

### Privacy & consent
COPPA age gate + opt-in analytics consent, and the settings-screen equivalent. Both are thin controllers over `AnalyticsServiceFacade` (`[Inject]`); collection stays off unless an age-eligible player explicitly accepts.
- **PrivacyConsentController** — `MonoBehaviour` first-run overlay; a neutral birth-year age gate (or two-button fallback) then an accept/decline consent step, gated on `_analytics.NeedsPrivacyFlow`/`AgeChecked`. Calls `SubmitBirthYear`/`SetAgeEligible`/`SetConsent`, opens the privacy-policy URL, raises `event Action OnPrivacyFlowCompleted`. `UI/Privacy/PrivacyConsentController.cs`.
- **AnalyticsPrivacySettingsController** — `MonoBehaviour` settings panel; a consent `Toggle` (reflects `ConsentGranted`, interactable only when `AgeEligible`), a "Delete my data" button (`RequestDataDeletion`, revokes consent), and a privacy-policy link button. `UI/Privacy/AnalyticsPrivacySettingsController.cs`.

### Model / editor
- **MiniGameHUDViewInspector** — a `CustomEditor` for `MiniGameHUDView` that draws a color-coded, sectioned inspector (Common Elements / Bottom Buttons / Button Events). Entirely disabled behind `#if false` (the file's `CosmicShore.UI` namespace block is empty); effectively dormant editor scaffolding. `UI/Model/MinigameHUDInspector.cs`.

### Interactions & patterns
- **SOAP / shared state.** Selection state lives in SOAP `ScriptableVariable`s injected into `View` (`shipClassTypeVariable`) and `GameDataSO` (`selectedVesselClass`, `VesselClassSelectedIndex`); the vessel picker and loadout views write these and call `GameDataSO.SyncFromArcadeGame` + `InvokeGameLaunch()` to hand off to the scene-load pipeline. `ArcadeExploreView` uses a `VesselClassTypeVariable` and `MiniGame.*` statics.
- **Domain-service events (not polling).** Profile UI reacts to `PlayerDataService.OnProfileChanged` and the static `OnCrystalBalanceChanged`/`OnXPChanged`; progression UI reacts to `GameModeProgressionService.OnProgressionChanged`; hangar/captain UI reacts to `CaptainManager.OnLoadCaptainData` and `VesselUnlockSystem.OnUnlockStateChanged`; explore reacts to `CatalogManager.OnLoadInventory`. This is the SOAP-preferred observer style over `WaitUntil` polling.
- **DI (Reflex).** Views pull services via `[Inject]` (`PlayerDataService`, `AnalyticsServiceFacade`, `AudioSystem`, `SO_GameList`, `GameDataSO`, `CaptainManager`, `UGSDataService`). `ProfileIconSelectView` additionally injects the Reflex `Container` to `GameObjectInjector.InjectRecursive` its runtime-instantiated icon buttons (Reflex does not auto-inject `Instantiate`d prefabs). `ProfileScreen` deliberately uses the `PlayerDataService.Instance` singleton instead of `[Inject]` to avoid the OnEnable-before-inject timing gap.
- **Cloud persistence.** `PlayerDataService` merges/writes through `UGSDataService.ProfileRepo` (debounced `MarkDirty` + immediate `SaveAsync` for deliberate actions like avatar change), and mirrors `displayName`/`avatarId` into `GameDataSO` so the networked `Player.NetName`/`NetAvatarId` replication path can pick them up in-game.
- **No NetworkBehaviours / Burst here.** This slice is pure client menu/UI: no `NetworkVariable`s, no Burst jobs. Animation is DOTween (`XPTrackView`, `QuestTrackView`) or hand-rolled coroutine lerps (`PreGameCinematicController`), which cooperates with `CameraManager`/`CustomCameraController` for the flythrough. `PreGameCinematicController` is invoked by `MiniGameHUD` and is the only in-scene (gameplay) member of this set.
- **Base-class conventions.** Detail panels extend `View` (model + index + `UpdateView`); modals extend `ModalWindowManager` (`ProfileIconSelectView`, `PortSquadMemberConfigureView`). Several `View`s are intentional stubs (`HangarOverviewView`, `PortFactionView`, and the captain-dependent Port squad views) reflecting the removal of the captain-on-vessel system, kept only to satisfy serialized inspector references.

---

## UI — Reusable Elements, Buttons, Hangar Cards & Animations

This area holds the shared, prefab-attachable `MonoBehaviour` widgets that the Menu_Main screens, arcade/configure modals, in-game HUDs, and end-game scoreboards compose out of. Everything here is view-layer glue: components read shared state from SOAP `ScriptableVariable`/`ScriptableList` assets and DI-injected services (`AudioSystem`, `PlayerDataService`, `GameDataSO`, `CaptainManager`, `HostConnectionService`, `FriendsServiceFacade`), react to their C#/SOAP change events, and drive TMP text, `Image` sprites/tints, `Button` interactability, and DOTween/coroutine juice. None are `NetworkBehaviour`s or `ScriptableObject`s — networked writes are always requested through the domain layer (e.g. `Player.RequestSetDomain_ServerRpc`). Files live under `UI/Elements`, `UI/Elements/Buttons`, `UI/Elements/Hangar`, `UI/Animations`, and `UI/FX`, all in the `CosmicShore.UI` namespace.

### Tab / nav navigation

Crossfade-based selection among sibling links; `NavGroup` discovers `NavLink` children and drives per-link active/inactive visual state.

- **NavLink** — a selectable tab: crossfades parallel active/inactive `Image`+`TMP_Text` lists over `crossfadeDuration`, optionally resizes its `RectTransform`, plays a `SwitchView` menu sound on click, and delegates activation to its owning `NavGroup`; references a `View` to show/select. `UI/Elements/NavLink.cs`
- **NavGroup** — container that enumerates child `NavLink`s (via `Initialize()`), assigns indices, and on `ActivateLink` toggles either the linked `View` GameObjects (`SelectView` mode) or calls `view.Select(index)` (`UpdateView` mode); force-rebuilds a parent `HorizontalLayoutGroup` on dynamic resizes. Declares the `NavGroupType` enum. `UI/Elements/NavGroup.cs`

### Profile & avatar widgets

Small display widgets bound to the live UGS profile through `PlayerDataService` (DI-injected or `.Instance` singleton) and its `OnProfileChanged`/`OnCrystalBalanceChanged` events.

- **ProfileDisplayWidget** — shows a player's display name + avatar; `[Inject]`s `PlayerDataService`, subscribes to `OnProfileChanged`, and resolves the avatar sprite via `GetAvatarSprite(avatarId)`. `UI/Elements/ProfileDisplayWidget.cs`
- **ProfileImage** — `[RequireComponent(Image)]` avatar that binds to `PlayerDataService.Instance.OnProfileChanged` (with an OnEnable/Start retry for bootstrap ordering) and falls back to `SO_ProfileIconList` icon 0 when the service is unavailable. `UI/Elements/ProfileImage.cs`
- **ProfileIconSelectButton** — one avatar option in the profile-icon picker; holds a `ProfileIcon`, plays an `OptionClick` sound (`[Inject] AudioSystem`, null-guarded), toggles a border on select, and reports selection to its `ProfileIconSelectView`. `UI/Elements/ProfileIconSelectButton.cs`

### Party & friends rows

The Menu_Main social panels. All data flows through `HostConnectionDataSO` (presence lobby / party state) and `FriendsDataSO` (relationships) SOAP containers; mutations route through `HostConnectionService`, `PartyInviteController`, and the `[Inject] FriendsServiceFacade`. Rows animate in with a CanvasGroup fade and punch-scale on press.

- **FriendsListPanel** — controller for the combined Online + Requests panel; spawns `OnlineInfoEntry`/`RequestInfoEntry` rows from `HostConnectionDataSO.OnlinePlayers` + `FriendsDataSO.IncomingRequests` + pending `PartyInviteData`, subscribes to the lobby/invite/party-member SOAP events, resolves remote party status into `OnlineInfoEntry.Status`, and drives send/cancel/kick/accept/decline invite+friend actions; plays a sound on invite received and auto-opens itself. `UI/Elements/FriendsListPanel.cs`
- **OnlineInfoEntry** — a single online-player row: avatar, name, status label (ONLINE / IN PARTY / IN A MATCH / PARTY FULL / IN YOUR PARTY), an Invite button and a dual-role Cancel/Kick (✕) button, with a pulsing yellow "PENDING REQUEST" tint, entry fade-in, invite-press punch, and a shared anti-spam `TryBeginAction` cooldown; declares the `Status` enum. `UI/Elements/OnlineInfoEntry.cs`
- **RequestInfoEntry** — a Requests-section row handling both incoming friend requests and party invites (`Kind` enum); Accept/Decline buttons, per-row expiry timer that colour-shifts toward "expiring soon" and auto-declines, CanvasGroup fade-in and button punch-scale. `UI/Elements/RequestInfoEntry.cs`
- **ArcadeLobbyList** — the 4-slot party panel inside the arcade modal; slot 0 is always the local player, remaining slots render `HostConnectionDataSO.PartyMembers`, empty slots expose a "+" that opens `FriendsListPanel`; shows an "N Players Online" counter, a host-only per-slot kick, a Leave-Party button (`HostConnectionService.LeavePartyAsync`), a two-pass clear-then-populate ordering, and shared-slot-reference wiring diagnostics. `UI/Elements/ArcadeLobbyList.cs`
- **FriendInfoSlot** — one slot of `ArcadeLobbyList`: LocalPlayer / Occupied / Empty states, `SetAsLocalPlayer`/`SetPlayer`/`ClearSlot`, `BindAddButton`/`BindKickButton` wiring, host-only ✕ kick with optimistic disable, GameObject-level (not just `Image.enabled`) toggling of avatar/name children, and `DisplayNameTextGO`/`AvatarIconGO` accessors for the shared-reference guard. `UI/Elements/FriendInfoSlot.cs`

### Domain (team) selection & score panels

Domain UI reads the `Domains` enum (Jade/Ruby/Gold, Blue = neutral) and per-domain theme colours (`DomainColorSet`); selection is forwarded upward and applied server-side, never written from the client.

- **DomainSelectionPanel** — three-button (Jade/Ruby/Gold) selector; toggles per-domain selection indicators and raises `event Action<Domains> OnDomainSelected` (consumer calls `Player.RequestSetDomain_ServerRpc`). `UI/Elements/DomainSelectionPanel.cs`
- **DomainInfoData** — per-domain tile in the configure modal; owns selected/unselected background sprite + label colour, exposes its `Button` and the `avatarStrip` transform the modal reparents chips into, and self-labels from the serialized `Domains` value (with an editor `OnValidate` deferred write). `UI/Elements/DomainInfoData.cs`
- **DomainAvatarChip** — one pooled avatar slot inside a domain tile's strip; `Set(sprite, isLocal)` shows the avatar + optional local-player outline ring, `Hide()` deactivates it (lifecycle owned by the modal, never self-instantiated). `UI/Elements/DomainAvatarChip.cs`
- **DomainScorePanel** — `[RequireComponent(CanvasGroup)]` in-game HUD card for one domain (used by `MultiplayerHUD` for HexRace/Joust/CrystalCapture); animates the team's summed objective via a composed `ScoreNumberAnimator`, tints background indicator + accent strip from a `DomainColorSet` (legacy single-colour overload also present), and spawns per-teammate avatar icons from a `PlayerScoreEntry` prefab. `UI/Elements/DomainScorePanel.cs`
- **TeamScorecard** — end-game scoreboard card for one team: team name/score, domain-coloured header background, and up to two `PlayerScoreEntry` rows via `Populate(...)`; declares the `PlayerDisplayData` struct. `UI/Elements/TeamScorecard.cs`
- **PlayerScoreEntry** — `[RequireComponent(CanvasGroup)]` lightweight per-player in-game score row (avatar, name, live score, domain indicator) used by `MiniGameHUD`/`MultiplayerHUD`; delegates score animation to `ScoreNumberAnimator` and entrance to `CardEntranceAnimator`. `UI/Elements/PlayerScoreEntry.cs`
- **ScoreNumberAnimator** — plain-C# helper (no MonoBehaviour) that drives a score `TMP_Text` with DOTween counter-roll + punch-scale + gain/loss colour-flash, parameterised by `HUDAnimationSettingsSO`; composed by `PlayerScoreEntry`, `PlayerScoreCard`, and `DomainScorePanel` to keep the animation logic in one place. `UI/Elements/ScoreNumberAnimator.cs`

### Steppers & discrete selectors

Numeric and discrete pickers feeding the arcade configure modal / game launch (`IntVariable` SOAP vars, `GameDataSO.ConfigurePlayerCounts`).

- **IntStepper** — generic +/- integer stepper clamped to `[min,max]`; `Initialize`/`SetValue`/`SetInteractable`, `Value` getter, and `event Action<int> OnValueChanged`; reused for player-count and domain-count. `UI/Elements/IntStepper.cs`
- **PlayerCountButton** — legacy fixed player-count button (meeple + border sprite pairs), holds `Count`, raises `event SelectDelegate OnSelect`, and toggles selected/active sprite+colour states; references an `IntVariable selectedPlayerCount`. `UI/Elements/PlayerCountButton.cs`
- **IntensitySelectButton** — 1–4 intensity picker with selected/unselected sprite pairs per level, active/inactive/locked colour states, writes to an `IntVariable selectedIntensityCount`, and raises `OnSelect`/`OnLockedSelect` (locked buttons stay clickable to trigger an unlock prompt). `UI/Elements/IntensitySelectButton.cs`

### Toggles & settings widgets

Small settings controls persisting to `PlayerPrefs` via `GameSetting.PlayerPrefKeys`.

- **SwitchToggle** — animated toggle handle; on `Toggle.onValueChanged` slides the handle `RectTransform` and plays an `OptionClick` sound (`[Inject] AudioSystem`). `UI/Elements/SwitchToggle.cs`
- **ToggleSynchronizer** — reads a `PlayerPrefKeys` int at Start and shows/hides paired On/Off GameObjects to reflect the stored boolean. `UI/Elements/ToggleSynchronizer.cs`
- **SliderInitializer** — loads/saves a `Slider` value under a `PlayerPrefKeys` float key (`UpdateSliderValue` on change). `UI/Elements/SliderInitializer.cs`
- **InputFieldUpdater** — mirrors a `TMP_InputField`'s value into a `TMP_Text` display on change (utility/demo binder). `UI/Elements/InputFieldUpdater.cs`
- **FavoriteIcon** — swaps active/inactive star sprites off a `Favorited` bool property (simple presentational toggle). `UI/Elements/FavoriteIcon.cs`

### Arcade / mode cards

Cards rendering `SO_ArcadeGame` / `Loadout` data from `SO_GameList` / `SO_VesselList`; favouriting and CTA routing go through `FavoriteSystem` and `FTUEEventManager`.

- **GameCard** — arcade game-mode tile: pulls the `SO_ArcadeGame` for its `GameModes` value, sets title/background/favorite-star, `ToggleFavorite()` (via `FavoriteSystem` + `AudioSystem`), `OnCardClicked` raises the CTA through `FTUEEventManager.RaiseCTAClicked`, and `SetLocked()` greys + disables the card. `UI/Elements/GameCard.cs`
- **LoadoutCard** — saved-loadout tile showing game background, ship silhouette, and player-count/intensity sprites (or a "+" empty state); select/deselect border+title colouring, reports selection to its `ArcadeLoadoutView`. `UI/Elements/LoadoutCard.cs`
- **QuickPlayButton** — one-tap HexRace launch; `[Inject]`s `GameDataSO`/`SO_GameList`/`HostConnectionDataSO`, sizes player count to the party (via `NetworkManager` or `HostConnectionDataSO.PartyMembers`), configures game data, assigns the local player a random domain (`RequestSetDomain_ServerRpc`), plays a `LetsGo` sound, and calls `gameData.InvokeGameLaunch()`. `UI/Elements/QuickPlayButton.cs`
- **DailyChallengeCard** — placeholder daily-challenge tile, hard-disabled ("COMING SOON"). `UI/Elements/DailyChallengeCard.cs`
- **DailyChallengePlayButton** — placeholder daily-challenge play button, disabled (no-op `Play`). `UI/Elements/DailyChallengePlayButton.cs`

### Quest, squad & misc cards

- **QuestItemCard** — quest-track tile driven by `SO_GameModeQuestData`; auto-resolves child references, renders Locked/Unlocked/ReadyToClaim/Claimed states (`QuestItemState` enum, declared here), a pulsing "active frontier" glow border (blue in-progress / green ready), DOTween unlock and claim scale-bounce animations, and `BindClaimAction`. `UI/Elements/QuestItemCard.cs`
- **SquadMemberCard** — captain/ship squad slot from an `SO_Captain` (captain image/name + `SO_Vessel.SquadImage`); largely inert since the captain-on-vessel system was removed. `UI/Elements/SquadMemberCard.cs`
- **RewardedAdsButton** — wires a button's onClick to `AdsSystem.ShowAd`. `UI/Elements/RewardedAdsButton.cs`

### Purchase / reward buttons (`UI/Elements/Buttons`)

A `PurchaseCard` hierarchy backing the store, plus reward-claim buttons; all interact with `CatalogManager` (inventory, currency, `PurchaseItem`), the `PurchaseConfirmationModal`, and `IconEmitter` juice.

- **PurchaseCard** — abstract base: holds the `PurchaseConfirmationModal` + `VirtualItem`, declares abstract `Purchase()`/`SetVirtualItem()` and virtual `OnClickBuy()` (opens the modal). `UI/Elements/Buttons/PurchaseCard.cs`
- **PurchaseItemCard** — abstract mid-tier: price/name/description/image labels, price/unavailable/purchased button+background swapping, `CatalogManager.OnCurrencyBalanceChange` affordability updates, `Purchase()` → `CatalogManager.PurchaseItem`, and DOTween-free coroutine card-flip on purchase; abstract `PurchaseLimitReached()`. `UI/Elements/Buttons/PurchaseItemCard.cs`
- **PurchaseCaptainCard** — captain variant; resolves the `SO_Captain` via `[Inject] CaptainManager`, `PurchaseLimitReached` = already-owned. `UI/Elements/Buttons/PurchaseCaptainCard.cs`
- **PurchaseGameCard** — arcade-game variant (renders `SO_ArcadeGame`, owned-check via `CatalogManager.Inventory.ContainsGame`). `UI/Elements/Buttons/PurchaseGameCard.cs`
- **PurchaseGameplayTicketCard** — daily-challenge-ticket variant; capped at `MaxDailyChallengeTicketBalance`, custom purchase + post-modal balance refresh. `UI/Elements/Buttons/PurchaseGameplayTicketCard.cs`
- **DailyRewardCard** — daily-reward claim card extending `PurchaseCard`; a Free/Ad/Clock `ButtonMode` state machine (PlayerPrefs-dated), live countdown to UTC midnight, `DailyRewardHandler.Claim()`, ad-watch reward path (`AdsSystem`), a 3D Y-axis card-flip coroutine between modes, and `IconEmitter` bursts. `UI/Elements/Buttons/DailyRewardCard.cs`
- **GameplayRewardButton** — tiered end-of-game reward button (`RewardButtonType` enum: DailyChallenge / Intensity); Claim/NotEarned/Collected button states, `DailyChallengeSystem`/`TrainingGameProgressSystem` claim routing, `IconEmitter` burst, and an X-axis card-flip reveal coroutine; holds a `GameplayReward`. `UI/Elements/Buttons/GameplayRewardButton.cs`
- **CaptainUpgradeSelectionCard** — captain-upgrade selector; `[Inject] CaptainManager` to fetch a `Captain`, shows level + element icon (`SO_Element.GetIcon(level, selected)`), selected border swap, reports index to `HangarCaptainsView`. `UI/Elements/Buttons/CaptainUpgradeSelectionCard.cs`

### Hangar cards (`UI/Elements/Hangar`)

Vessel-hangar detail widgets reading `SO_Vessel` / `SO_VesselAbility` / `SO_TrainingGame` and the `VesselUnlockSystem`.

- **HangarVesselGridCard** — `[RequireComponent(CanvasGroup)]` vessel grid tile with `IPointerEnter/Exit` DOTween hover-scale, icon/name/lock overlay from `SO_Vessel.IsLocked`, `SetAlpha`/`SetNameVisible`, and click → `HangarScreen.SelectVesselForDetail`. `UI/Elements/Hangar/HangarVesselGridCard.cs`
- **HangarVesselSelectNavLink** (class `HangarShipSelectNavLink`) — `NavLink` subclass for the vessel nav strip; active/inactive background+lock sprites and size, `AssignShipClass`/`AssignIndex`, click → `HangarScreen.SelectShip`; overrides `SetActive`. `UI/Elements/Hangar/HangarVesselSelectNavLink.cs`
- **HangarAbilityCard** — renders one `SO_VesselAbility` (icon/name/description). `UI/Elements/Hangar/HangarAbilityCard.cs`
- **HangarGameplayParameterDisplay** — a labelled slider readout for one `GameplayParameter` (left/right labels + thumb position from `Value`). `UI/Elements/Hangar/HangarGameplayParameterDisplay.cs`
- **HangarGameplayParameterDisplayGroup** — assigns a `List<GameplayParameter>` across a list of `HangarGameplayParameterDisplay`s (count-mismatch warns). `UI/Elements/Hangar/HangarGameplayParameterDisplayGroup.cs`
- **HangarTrainingGameButton** — training-game selector tile; active/inactive element-icon pair (`SO_Element.GetFullIcon`) + border, click → `HangarTrainingModal.SelectGame`. `UI/Elements/Hangar/HangarTrainingGameButton.cs`
- **CrystalCurrencyDisplay** — live crystal-balance label; subscribes to `PlayerDataService.OnCrystalBalanceChanged` + `VesselUnlockSystem.OnUnlockStateChanged` and reads `VesselUnlockSystem.GetCurrencyBalance()`. `UI/Elements/Hangar/CrystalCurrencyDisplay.cs`

### Shared card animation helpers

- **CardEntranceAnimator** — stateless static helper returning a DOTween `Sequence` for a staggered scale+fade entrance, parameterised by `HUDAnimationSettingsSO`; used by `PlayerScoreEntry` and `PlayerScoreCard`. `UI/Elements/CardEntranceAnimator.cs`
- **ConnectingPanel** — picks a random background sprite on enable (pre-game connecting screen). `UI/Elements/ConnectingPanel.cs`

### Text & loading animations (`UI/Animations`)

Text-flourish animators for splash/connecting states.

- **ConnectingDotsAnimator** — coroutine-driven trailing-dots loop after a `BaseText` string (`WaitForSecondsRealtime`, so runs while paused). `UI/Animations/ConnectingDotsAnimator.cs`
- **EllipsisTextLooper** — `[RequireComponent(TMP_Text)]` DOTween `Sequence` cycling 0–3 ellipsis dots with optional fade flicker, `SetUpdate(true)` (timescale-independent). `UI/Animations/EllipsisTextLooper.cs`
- **DoTweenTypewriterAnimator** — UniTask character-by-character reveal of a baked `fullText` with a `CancellationToken` (`PlayIn`/`ClearInstant`). `UI/Animations/DoTweenTypewriterAnimator.cs`

### Visual FX (`UI/FX`)

Lightweight procedural UI juice.

- **IconEmitter** — emits N `ImageTemplate` clones along quadratic-Bézier arcs from a `Source` to a `Target` `Image` with grow/shrink size phases, tail fade, source-shrink and target-pulse coroutines, and optional audio (`EmissionMode` enum: RandomAngle/Sweep/Scatter); the currency/reward "coins fly to wallet" effect. `UI/FX/IconEmitter.cs`
- **Pulse** — sine-wave alpha pulse on an `Image` (`angularFrequency`, `alphaFloor`, unscaled time). `UI/FX/Pulse.cs`
- **JustRotate** — constant per-frame `transform.Rotate` about a configurable axis/speed (toggleable). `UI/FX/JustRotate.cs`

### Interactions & patterns

- **SOAP data containers as the read source.** Party/friends widgets bind to `HostConnectionDataSO` (`OnlinePlayers`/`PartyMembers` `ScriptableList`s + `OnInviteReceived`/`OnInviteResolved`/`OnPartyMemberJoined/Left/Kicked` events) and `FriendsDataSO` (`IncomingRequests`), subscribing `OnItemAdded`/`OnItemRemoved`/`OnCleared`/`OnRaised` in OnEnable and unsubscribing in OnDisable. Selectors write to SOAP `IntVariable`s (`selectedPlayerCount`, `selectedIntensityCount`) that the configure modal later commits into `GameDataSO`.
- **DI (Reflex) for services.** `[Inject]` pulls `AudioSystem` (menu SFX via `MenuAudioCategory`), `PlayerDataService`, `CaptainManager`, `GameDataSO`, `SO_GameList`, `HostConnectionDataSO`, and `FriendsServiceFacade`; components accessed before injection (bootstrap ordering) fall back to `.Instance` singletons with retry (`ProfileImage`, `ArcadeLobbyList`). Injected fields are read in `Start()`/OnEnable, never `Awake()`.
- **Server-authoritative writes only.** Domain UI (`DomainSelectionPanel`, `QuickPlayButton`) never mutates `Player.NetDomain` directly — it forwards selections and the consumer calls `Player.RequestSetDomain_ServerRpc`; game launch goes through `GameDataSO.ConfigurePlayerCounts` + `InvokeGameLaunch`, and party mutations through `HostConnectionService`/`PartyInviteController`. There are no `NetworkBehaviour`s or `NetworkVariable`s in this area.
- **Shared animation infrastructure.** `ScoreNumberAnimator` and `CardEntranceAnimator` centralise the score/card DOTween juice (parameterised by the shared `HUDAnimationSettingsSO`), composed by `PlayerScoreEntry`, `PlayerScoreCard`, `DomainScorePanel`, and `TeamScorecard`; tweens are `SetUpdate(true)`/unscaled so they run while the menu is paused and are killed in `OnDestroy`/`OnDisable`.
- **Store/reward pipeline.** The `PurchaseCard` → `PurchaseItemCard` hierarchy plus `DailyRewardCard`/`GameplayRewardButton` drive `CatalogManager` (currency/inventory/`PurchaseItem`), the `PurchaseConfirmationModal`, and `IconEmitter` currency-flight FX; card-flip reveals are hand-rolled `Time.unscaledDeltaTime` rotation coroutines.
- **No SOAP null-guards; fail-loud diagnostics.** Consistent with project rules, panels log loud errors on missing scene wiring (`FriendsListPanel.ValidateSceneWiring`, `ArcadeLobbyList.WarnOnSharedSlotReferences`, `NavLink`/`HangarGameplayParameterDisplayGroup` count-mismatch warnings) rather than silently degrading.

---

## UI — Toasts, Notifications & Game Event Feed

This area is the game's collection of transient, non-blocking on-screen message surfaces. There are **four independent notification families**, each built from the same repeating recipe: an immutable payload type, a decoupled channel (either a plain C# event on a `ScriptableObject` or an Obvious.Soap `ScriptableEvent<T>`), a settings `ScriptableObject` for look/feel, a presenter/manager MonoBehaviour that pools+animates items with DOTween, and (usually) a static façade API that auto-loads its channel from `Resources/`. The families differ in placement and intent: **ToastSystem** is a chat-style stack for in-HUD gameplay callouts (countdowns, prefixes); **ToastNotification** is a Material-style swipe-dismissable stack for app/system messages; **Notification System** is a single top-anchored header/title slide-in card; and **GameEventFeed** is an in-game kill-feed of domain-colored gameplay events (joins, ready, disconnects, jousts). None of these use Netcode directly — network-originated events are posted locally on each client through the static APIs.

### ToastSystem — chat-style HUD toast stack

A pooled, queued "chat feed" that pushes new lines in at the bottom and lets older lines slide upward via a `VerticalLayoutGroup`. Driven by a plain C# `event` on a `ScriptableObject` channel (not a SOAP event), it is inspector-wired into vessel HUDs for gameplay callouts.

- **ToastAnimation** — enum of entrance styles: `ChatSubtleSlide` (default), `Pop`, `Fade`. `Assets/_Scripts/UI/ToastSystem/ToastAnimation.cs`
- **ChatToastRequest** — immutable `readonly struct` payload: `Prefix`, `Postfix`, `Duration`, `Animation`, `Icon`, `Accent`, plus `PostfixCountdownFrom` / `PostfixCountdownFormat` for per-second countdown lines. `Assets/_Scripts/UI/ToastSystem/ChatToastRequest.cs`
- **ToastChannel** — `ScriptableObject` (`[CreateAssetMenu]`) channel exposing `event Action<ChatToastRequest, Action> OnChatToast` and high-level raisers `ShowPrefix` / `ShowPrefixPostfix` / `ShowCountdown` (the second `Action` arg is an optional on-done callback, e.g. confirm-overcharge). `Assets/_Scripts/UI/ToastSystem/ToastChannel.cs`
- **ToastService** — `MonoBehaviour` on a HUD chat container; subscribes to `channel.OnChatToast`, maintains a request `Queue`, an `_active` list capped by `maxConcurrent` (5), and a `Stack` object pool of views; mirrors the channel's `ShowPrefix`/`ShowPrefixPostfix`/`ShowCountdown` helpers for direct local calls. `Assets/_Scripts/UI/ToastSystem/ToastService.cs`
- **ToastItemView** — `MonoBehaviour` per-line view; `Play()` sets prefix/postfix/icon/accent, runs a coroutine that plays the in-tween, ticks the postfix countdown once per second (firing `onDoneExternal` at zero), waits `Duration`, then plays the out-tween and reclaims itself to the pool; DOTween-based `PlayIn`/`PlayOut`, `ForceHide`. `Assets/_Scripts/UI/ToastSystem/ToastItemView.cs`
- **ToastRequest** — fully commented-out legacy struct (dead file, retained for reference). `Assets/_Scripts/UI/ToastSystem/ToastRequest.cs`

*Consumers:* `MantaVesselHUDController` (overcharge `ShowCountdown` + `ShowPrefix`), `PartyInviteController`.

### ToastNotification — swipe-dismissable system toast stack

A persistent-singleton stack of left-edge toasts for app/system messages, decoupled via a SOAP `ScriptableEvent<string>`. Layout (stacking, clipping) is fully owned by the container's `VerticalLayoutGroup`/`RectMask2D`; items never touch their own anchors.

- **ToastNotificationSettingsSO** — `ScriptableObject` config: slide/fade durations & eases, `autoRemoveDelay`, `swipeDismissThreshold`, layout margins/spacing, `maxVisible` (3), `maxQueue` (10), `useUnscaledTime`. `Assets/_Scripts/UI/ToastNotification/ToastNotificationSettingsSO.cs`
- **ToastNotificationChannel** — SOAP `ScriptableEvent<string>` (`[CreateAssetMenu]`); the decoupled string-message channel. `Assets/_Scripts/UI/ToastNotification/ToastNotificationChannel.cs`
- **ToastNotificationItem** — `MonoBehaviour` implementing `IBeginDragHandler`/`IDragHandler`/`IEndDragHandler`; `Show(message, settings)` fades in and schedules auto-dismiss; supports horizontal swipe-to-dismiss with alpha feedback; raises `event Action<ToastNotificationItem> OnDismissed`; `DismissImmediate`/`AutoDismiss`. `Assets/_Scripts/UI/ToastNotification/ToastNotificationItem.cs`
- **ToastNotificationManager** — `SingletonPersistent<ToastNotificationManager>` (DontDestroyOnLoad); subscribes `channel.OnRaised → Show(string)`, manages an `_activeToasts` list, `_pendingQueue`, and item `_pool`, enforcing `maxVisible`/`maxQueue`; assignable `Container`; builds a default toast prefab at runtime if none wired. `Assets/_Scripts/UI/ToastNotification/ToastNotificationManager.cs`
- **ToastNotificationAPI** — static façade `Show(string)`; lazily auto-creates the manager singleton, reflection-wires the settings + channel loaded from `Resources/` (`ToastNotificationSettings`, `Channels/ToastNotificationChannel`), and locates the `"ToastNotificationContainer"` including inactive scene objects. `Assets/_Scripts/UI/ToastNotification/ToastNotificationAPI.cs`

*Consumers:* `ArcadeGameConfigureModal`, `UGSCloudSaveProvider` (save-fail), `HangarVesselDetailView` (locked hangar), `FriendsListPanel` (friend/party feedback), `EndGameSequencer` (quest-complete / intensity-unlocked).

### Notification System — top-anchored header/title card

A single reused slide-in "banner" card (header line + title line) for prominent one-off announcements, decoupled via a SOAP `ScriptableEvent<NotificationPayload>`. One `NotificationView` instance is instantiated once and re-animated per queued payload.

- **NotificationPayload** — `[Serializable] struct` with `Header` + `Title`. `Assets/_Scripts/UI/Notification System/Payload/NotificationPayload.cs`
- **ScriptableEventNotificationPayload** — SOAP `ScriptableEvent<NotificationPayload>` channel (defined out-of-folder at `Assets/_Scripts/System/ScriptableEventNotificationPayload.cs`, namespace `CosmicShore.Core`; loaded from `Resources/Channels/NotificationChannel`).
- **NotificationAPI** — static façade `Notify(header, title)` / `Notify(payload)`; lazily loads the channel from `Resources/` and `Raise`s the payload. `Assets/_Scripts/UI/Notification System/Payload/NotificationAPI.cs`
- **NotificationSettingsSO** — `ScriptableObject` config: in/out `SlideDirection`, padding, in/hold/out durations, eases, alpha fade, optional scale, `maxQueue`, `useUnscaledTime`. Also declares the **SlideDirection** enum (`FromRight`/`FromLeft`/`FromTop`/`FromBottom`). `Assets/_Scripts/UI/Notification System/Payload/NotificationSettingsSO.cs`
- **NotificationPresenter** — `MonoBehaviour`; subscribes `channel.OnRaised → Enqueue`, runs a coroutine queue, lazily instantiates one `NotificationView`, and per payload builds a DOTween in→hold→out sequence (anchored-pos slide + optional fade/scale) between on-screen and computed off-screen positions. `Assets/_Scripts/UI/Notification System/Payload/NotificationPresenter.cs`
- **NotificationView** — `MonoBehaviour` binding target holding refs (`container`, `canvasGroup`, `headerText`, `titleText`, `fitHelper`); `Bind(payload)` sets the two text lines. `Assets/_Scripts/UI/Notification System/Payload/NotificationView.cs`
- **CanvasFitHelper** — `MonoBehaviour` utility; `GetOffscreenPos(showPos, dir, padding)` returns the off-screen anchored position that fully hides the rect in a given slide direction. `Assets/_Scripts/UI/Notification System/Payload/CanvasFitHelper.cs`

*Consumers:* no in-code callers of `NotificationAPI.Notify` currently — the presenter is scene/inspector-wired and the API is reserved for header/title announcements.

### GameEventFeed — in-game domain-colored event feed

An in-HUD "kill-feed" that streams gameplay events (player joined/ready/disconnected, joust hits) as domain-tinted lines, oldest-destroyed-first. Decoupled via a SOAP `ScriptableEvent<GameFeedPayload>` and additionally reactive to `GameDataSO` player events; it builds its own ScrollRect/Viewport/Content hierarchy at runtime.

- **GameFeedType** — enum: `Generic`, `PlayerJoined`, `PlayerReady`, `PlayerDisconnected`, `JoustHit`. `Assets/_Scripts/UI/GameEventFeed/GameFeedPayload.cs`
- **GameFeedPayload** — `[Serializable] struct`: `Message`, `Domain`, `Type`. `Assets/_Scripts/UI/GameEventFeed/GameFeedPayload.cs`
- **ScriptableEventGameFeedPayload** — SOAP `ScriptableEvent<GameFeedPayload>` (`[CreateAssetMenu]`); the feed channel, loaded from `Resources/Channels/GameFeedChannel`. `Assets/_Scripts/UI/GameEventFeed/ScriptableEventGameFeedPayload.cs`
- **GameFeedSettingsSO** — `ScriptableObject` config: `slideInDuration`/`slideInOffset`/`slideInEase`, `holdDuration`, `fadeOutDuration`, `maxVisibleEntries` (6), `useUnscaledTime`. `Assets/_Scripts/UI/GameEventFeed/GameFeedSettingsSO.cs`
- **GameFeedAPI** — static façade `Post(message, domain, type)` and `PostJoust(attacker, atkDomain, target, tgtDomain)` (composes a rich-text color-tagged joust line); holds a static `SO_ColorSet ColorSet` (assigned once by `ThemeManager`, single source of truth "R5") and `GetDomainColor(domain)` with white fallback. `Assets/_Scripts/UI/GameEventFeed/GameFeedAPI.cs`
- **GameFeedEntry** — `MonoBehaviour` per-line view; `Setup(message, color, isRichText)` and `AnimateIn(settings)` (X-only slide + fade in → hold → fade out → self-destroy, so the layout group owns Y); static `CreateEntry(parent)` builds an entry programmatically when no prefab is wired. `Assets/_Scripts/UI/GameEventFeed/GameFeedEntry.cs`
- **GameEventFeed** — `MonoBehaviour`; `[Inject] GameDataSO gameData` (Reflex DI); subscribes to `feedChannel.OnRaised → OnFeedEvent` and `gameData.OnPlayerAdded → OnPlayerAdded`; programmatically constructs the ScrollRect/Viewport/Content scroll structure (or reuses a wired container), spawns entries destroying the oldest past `maxVisibleEntries`, colors non-rich lines via `gameData.ThemeManagerData.GetDomainUIColor(domain)`; `ClearFeed()`. `Assets/_Scripts/UI/GameEventFeed/GameEventFeed.cs`

*Consumers:* `MultiplayerDomainGamesController` (`PlayerReady`, `PlayerDisconnected`), `VesselExplosionBySkimmerEffectSO` (`PostJoust` on skimmer joust), `ThemeManager` (assigns `GameFeedAPI.ColorSet`).

### Interactions & patterns

- **Four parallel surfaces, one recipe.** Each family = immutable payload + channel + settings SO + presenter/manager + optional static façade. Static façades (`ToastNotificationAPI`, `NotificationAPI`, `GameFeedAPI`) lazily `Resources.Load` their channel from `Resources/Channels/…` (and settings/prefab), so any system can post a message with no scene reference or DI — the "fire-and-forget from anywhere" pattern.
- **Two channel styles.** ToastSystem uses a plain C# `event Action` on a `ScriptableObject` (`ToastChannel`), inspector-wired directly into HUDs; the other three use Obvious.Soap `ScriptableEvent<T>` (`ScriptableEvent<string>`, `<NotificationPayload>`, `<GameFeedPayload>`) subscribed via `OnRaised`. This is the SOAP decoupling described in the project's architecture — publishers never reference presenters.
- **DI + domain theming.** `GameEventFeed` is the only component using Reflex `[Inject] GameDataSO`; it, `GameFeedAPI`, and the feed derive line colors from the shared `SO_ColorSet` / `ThemeManagerData.GetDomainUIColor` (same palette the vessels/prisms use), keeping `Domains`-colored UI consistent. `ThemeManager` pushes that color set into the static `GameFeedAPI.ColorSet` at scene theming time.
- **`GameDataSO` reactivity.** Beyond its own channel, the feed listens to `GameDataSO.OnPlayerAdded(name, domain)` to auto-post "joined" lines — coupling the notification UI to the player-spawn pipeline.
- **No NetworkVariables / Netcode here.** Network events (ready, disconnect, joust) are computed server/authoritatively elsewhere and surfaced locally through the static `Post`/`Show` calls on each client; these UI classes hold no `NetworkBehaviour`, RPCs, or `NetworkVariable`s. No Burst jobs either — everything is DOTween-animated UI.
- **Lifecycle & pooling.** ToastSystem and ToastNotification pool their item views (Queue/Stack + active list) and cap concurrency (`maxConcurrent`/`maxVisible`) with overflow queues; GameEventFeed destroys-oldest instead of pooling. All settings default to `useUnscaledTime` so messages animate through `Time.timeScale == 0` (pause menus). `ToastNotificationManager` is a DontDestroyOnLoad `SingletonPersistent`, and its API re-locates the `"ToastNotificationContainer"` (including inactive) after scene changes. Fail-soft logging routes through `CSDebug` rather than throwing.

---

## Data Model & ScriptableObject Definitions

This area is the game's serialization backbone: the enums that name every fundamental (domains, elements, vessels, cell phases, game modes), the value-type structs that move small payloads across SOAP channels and cloud saves, the `RoundStats` networked per-player stat record, and the large family of `ScriptableObject` config assets (`SO_*`) that hold all designer-tunable gameplay, progression, cosmetic, and monetization data. Nearly everything lives in the `CosmicShore.Data` namespace (enums/structs) or `CosmicShore.ScriptableObjects` namespace (SO configs); a handful of vessel-facing SOs sit in the global namespace. A recurring convention throughout: **every enum member is assigned an explicit static numeric value** to prevent Unity serialization drift from silently rewiring scene NetworkVariables and SOAP asset references. Note that several enum/struct files are named for a newer concept than the type they still declare (e.g. `VesselActions.cs` declares `ShipActions`); those mismatches are called out below.

### Core identity enums (fundamentals)

These name the load-bearing fundamentals every other system keys off. All in `Data/Enums/`.

- **VesselClassType** — the 11 playable vessel classes plus meta sentinels; `Any(-1)`, `Random(0)`, `Manta(1)`, `Dolphin(2)`, `Rhino(3)`, `Urchin(4)`, `Grizzly(5)`, `Squirrel(6)`, `Serpent(7)`, `Termite(8)`, `Falcon(9)`, `Shrike(10)`, `Sparrow(11)`. `VesselClassType.cs`
- **Domains** — team/affiliation identity attached to mass/vessels; `Jade(1)`, `Ruby(2)`, `Blue(3)`, `Gold(4)`. Blue is the "no team / not yet picked / neutral" sentinel, never in `ActiveDomains`. `Domains.cs`
- **Element** — the four elementals plus sentinels; `None(0)`, `Charge(1)`, `Mass(2)`, `Space(3)`, `Time(4)`, `Omni(5)`. `Element.cs`
- **ResourceType** — how a resource gauge is treated; `Gauge(0)`, `Item(1)`. `ResourceType.cs`
- **PrismKind** — a spawned prism's gameplay theme, orthogonal to its Domain colour; `Plain(0)`, `Danger(1)`, `Shielded(2)`, `SuperShielded(3)`. Doc comment notes shielded kinds swap to always-on convex MeshColliders (collider-budget cost). `PrismKind.cs`

### Ecosystem enums

Encode the locked 3-phase cell ecology; each carries a long design comment tying it to `Docs/ECOSYSTEM.md`. In `Data/Enums/`.

- **CellPhase** — a cell's ecological state along its live-prism-count axis; `None(0)`, `Calm(1)`, `Restless(2)`, `Frenzy(3)`. Maps 1:1 onto aggression bands. `CellPhase.cs`
- **CellAggressionLevel** — fauna aggression band indexing the per-level multiplier arrays; `Level0(0)`, `Level1(1)`, `Level2(2)`. `CellAggressionLevel.cs`
- **FaunaDiet** — the predator/herbivore split seam; `Herbivore(0)` (eats opposing prism mass), `Predator(1)` (eats herbivore fauna). `FaunaDiet.cs`

### Application & menu lifecycle enums

Drive the top-level state machines. In `Data/Enums/`.

- **ApplicationState** — top-level app phases written only by `ApplicationStateMachine`; `None(0)`, `Bootstrapping(1)`, `Authenticating(2)`, `MainMenu(3)`, `LoadingGame(4)`, `InGame(5)`, `GameOver(6)`, `Paused(7)`, `Disconnected(8)`, `ShuttingDown(9)`. `ApplicationState.cs`
- **MainMenuState** — Menu_Main sub-states driven by `MainMenuController`; `None(0)`, `Initializing(1)`, `Ready(2)`, `LaunchingGame(3)`, `Freestyle(4)`. `MainMenuState.cs`
- **BootStatusMode** — render mode for the boot/loading status surface, carried by `BootStatusRequest`; `Hide(0)`, `Status(1)`, `Retry(2)`. `BootStatusMode.cs`

### Game mode & scoring enums

- **GameModes** — the 37-mode catalog with explicit IDs (7 and 31 reserved/skipped): `Random(0)`, single-player `Elimination(1)`…`ProtectMission(27)`, multiplayer `MultiplayerFreestyle(28)`, `MultiplayerCellularDuel(29)`, `Multiplayer2v2CoOpVsAI(30)`, `MultiplayerWildlifeBlitzGame(32)`, `HexRace(33)`, `MultiplayerJoust(34)`, `MultiplayerCrystalCapture(35)`, `Tournament(36)` (meta), `AstroLeague(37)`. `GameModes.cs`
- **ScoringMetric** — the single per-player stat a scoring rule aggregates by Domain; `Crystals(0)`, `OmniCrystals(1)`, `ElementalCrystals(2)`, `Jousts(3)`, `Goals(4)`. `ScoringMetric.cs`

### Input enums

- **InputEvents** — the abstract fly/action input events fed by input strategies; `FullSpeedStraightAction(0)`…`BothSticksAction(13)` (14 members incl. left/right stick, flip, idle, min-speed, three buttons, node/self tap). `InputEvents.cs`
- **InputDeviceType** — active input device; `Touch(0)`, `Gamepad(1)`, `Keyboard(2)`, `DualMouse(3)`. Declared in the **global namespace** (no `CosmicShore.Data`). `InputDeviceType.cs`
- **ShipActions** — 20 vessel ability/action IDs (`Boost(1)`, `Invulnerability(2)`, `ToggleCamera(3)`, `GrowSkimmer(7)`, `Drift(16)`, `SpeedTubes(17)`, `MachCone(19)`, `ExplosiveAcorn(20)`, …). Declared in file `VesselActions.cs`.

### Impact / effect classification enums

Small tag enums selecting which effect fires on a given collision or camera event; several are legacy with overlapping numeric values. In `Data/Enums/`.

- **ImpactEffects** — legacy master impact list with **deliberately overlapping duplicate values** (e.g. `FillCharge=1`/`DrainHalfAmmo=1`) and a large commented-out redesign sketch. `ImpactEffects.cs`
- **CrystalImpactEffects** — crystal-collision effects; `FillCharge(1)`, `DrainAmmo(2)`, `Boost(5)`, `AreaOfEffectExplosion(6)`, `StealCrystal(12)`, `AdjustLevel(14)`, … `CrystalImpactEffects.cs`
- **TrailBlockImpactEffects** — 19 prism-collision effects; `PlayHaptics(0)`…`Redirect(18)` (incl. `GainResourceByVolume(6)`, `Shield(11)`, `Fire(13)`, `Bounce(14)`, `Explode(15)`, `FeelDanger(17)`). `TrailBlockImpactEffects.cs`
- **ShipImpactEffects** — vessel-collision effects; `TrailSpawnerCooldown(0)`, `PlayHaptics(1)`, `SpinAround(2)`, `Knockback(3)`, `Stun(4)`, `Charm(5)`, `AreaOfEffectExplosion(6)`. Declared in file `VesselImpactEffects.cs`.
- **SkimmerStayEffects** — continuous skimmer-in-contact effects; `ChangeResource(1)`, `FX(3)`, `Boost(4)`, `ScaleTrailAndCamera(5)`, `Align(6)`, `VizualizeDistance(7)`, `ScalePitchAndYaw(8)`, `ScaleHapticWithDistance(9)`, `ScaleGap(10)`. `SkimmerStayEffects.cs`
- **ShipCameraOverrides** — per-action camera overrides; `CloseCam(3)`, `FarCam(4)`, `ChangeFollowTarget(5)`, `SetFollowTarget(6)`, `Orthgraphic(7)`, `SetFixedFollowOffset(8)`. Declared in file `PassiveAbilities.cs`.
- **ResourceEvents** — resource-threshold notifications; `AboveThreeQuartersAmmo(0)`, `AboveHalfAmmo(1)`. `ResourceEvents.cs`

### Graphics & settings enums

All nine live in one file `GraphicsSettingsEnums.cs`, each with a designer/perf rationale comment.

- **DisplayModeSetting** — `Fullscreen(0)`, `Borderless(1)`, `Windowed(2)`.
- **VSyncSetting** — `Off(0)`, `On(1)`, `Half(2)`.
- **QualityPresetSetting** — `Custom(-1)`, `VeryLow(0)`…`Ultra(5)`.
- **AntiAliasingSetting** — `Off(0)`, `FXAA(1)`, `SMAA(2)`, `MSAA2x(3)`, `MSAA4x(4)`, `MSAA8x(5)`, `TAA(6)`.
- **UpscalingSetting** — `Auto(0)`, `Linear(1)`, `FSR(2)`, `STP(3)`.
- **EcosystemDensitySetting** — CPU-side spawn/sim density (conserved-mass-safe); `Sparse(0)`, `Normal(1)`, `Lush(2)`.
- **PhysicsDetailSetting** — active-collider fidelity; `Low(0)`, `High(1)`.
- **AdaptivePerformanceSetting** — `AdaptiveAnimationManager` aggressiveness; `Off(0)`, `Balanced(1)`, `Aggressive(2)`.
- **ColorblindModeSetting** — gameplay-critical domain-colour correction; `Off(0)`, `Protanopia(1)`, `Deuteranopia(2)`, `Tritanopia(3)`.

### Progression / analytics enums

- **CaptainLevel** — Playfab-backed captain upgrade tier; `Upgrade0(0)`…`Upgrade5(5)`. `CaptainLevel.cs`
- **CallToActionTargetType** — CTA deep-link targets bucketed by 100s (`None(-1)`, arcade 100s, store 200s, hangar 300s, per-mode play targets 400s up to `PlayGameCurvatious(436)`). `CallToActionTargetType.cs`
- **UserActionType** — analytics user-action IDs, same 100s bucketing (`None(-1)`, `ViewArcadeMenu(100)`, `PlayGame(400)`, …). `UserActionType.cs`

### Round stats data model

The per-player stat record that scoring, HUDs, and scoreboards read. Lives (despite the folder) in `Data/Enums/`.

- **IRoundStats** — interface declaring ~35 stat properties (prism counts, volumes, crystal counts/values, joust/goal/skimmer collisions, per-ability active times, `Name`, `Domain`, `Score`) plus a matching `OnXChanged` event per stat and a default `Cleanup()` that zeroes them. `IRoundStats.cs`
- **DomainStats** — tiny struct pairing a `Domains` with a `float Score`. Same file. `IRoundStats.cs`
- **RoundStats** — `NetworkBehaviour` implementing `IRoundStats`; lives on the persistent Player NetworkObject. Each stat is a local field mirrored by a **Server-write `NetworkVariable<T>`** (`n_Score`, `n_VolumeCreated`, `n_CrystalsCollected`, … ~30 of them); setters write local-first then push to the NetVar on the server, and `OnNetworkSpawn` seeds locals + wires `OnValueChanged` callbacks that re-raise the C# events. `Domain` is deliberately **not** a NetworkVariable (mirrored from `Player.NetDomain`). Exposes `ClearEventSubscriptions()` (severs leaked cross-scene subscriptions, per BUGS.md B15) and `InvokeOnJoustCollisionChanged()`. `RoundStats.cs`

### Value-type structs

Small `[Serializable]` payloads for SOAP channels, cloud saves, and modifiers. Split between `Data/Structs/` and (two of them) `Data/Enums/`.

- **ResourceCollection** — `Mass`/`Charge`/`Space`/`Time` float bundle for initial elemental levels. In `Data/Enums/ResourceCollection.cs`.
- **ShipThrottleModifier** — `initialValue`/`duration`/`elapsedTime` throttle tween state. Declared in file `Data/Enums/VesselThrottleModifier.cs`.
- **ShipVelocityModifier** — `Vector3 initialValue`/`duration`/`elapsedTime` velocity tween state. Declared in file `Data/Enums/VesselVelocityModifier.cs`.
- **BootStatusRequest** — `{ BootStatusMode Mode, string Text }`, carried on the `Event_BootStatusRequest` SOAP channel. `Data/Structs/BootStatusRequest.cs`
- **DailyChallenge** — `{ int Intensity, GameModes GameMode }`. `Data/Structs/DailyChallenge.cs`
- **DailyChallengeRewardState** — three-tier satisfied/claimed flags + `HighScore`. `Data/Structs/DailyChallengeRewardState.cs`
- **GameplayReward** — `{ int ScoreRequirement, int Value, Element Element, GameModes GameMode }`. `Data/Structs/GameplayReward.cs`
- **ScoreResult** — readonly ranked result row (`Rank`, `Name`, `Domain`, `Score`, mode-formatted `ScoreText`, optional `Secondary`); the single source of truth every end-game surface reads (ScoringSystem R10). `Data/Structs/ScoreResult.cs`
- **TrainingGameProgress** / **TrainingGameTier** — 4-tier training progression with `SatisfyTier`/`ClaimTier`/`IsTierClaimed`/`IsTierSatisfied`; each tier is a `{ bool Satisfied, bool Claimed }` struct. `Data/Structs/TrainingGameProgress.cs`

### Game & mode config SOs

`SO_Game` is the shared base for every playable-mode card. All in `ScriptableObjects/`.

- **SO_Game** — base mode config: `GameModes Mode`, `IsMultiplayer`, `DisplayName`, `Description`, active/inactive icons, `CardBackground`, `VideoPlayer PreviewClip`, `GolfScoring`, `SceneName`. `SO_Game.cs`
- **SO_ArcadeGame** — `SO_Game` subclass adding the arcade card's ranges: `List<SO_Vessel> Vessels`, `Min/MaxPlayersAllowed`, `Min/MaxDomainsAllowed` (team-count gate), `Min/MaxIntensity`, and `CallToActionTargetType`/`ViewUserAction`/`PlayUserAction`. `SO_ArcadeGame.cs`
- **SO_Mission** — `SO_Game` subclass for missions: `Min/MaxDifficulty` + `Threat[] PotentialThreats`. File also declares **SpawnMode** enum (`ConcentratedInvasion`, `RandomSurfaceScatter`, `LocalizedAmbush`, `PathBasedDeployment`, `SphereInterdiction`) and the **Threat** class (`threatName`, `threatLevel`, `weight`, `threatPrefab`, `spawnMode`, virtual `Spawn` assigning team via `ITeamAssignable`), with a commented-out `Boss` subclass. `SO_Mission.cs`
- **SO_GameList** — `List<SO_ArcadeGame> Games`. `SO_GameList.cs`
- **SO_MissionList** — `List<SO_Mission> Games`. `SO_MissionList.cs`

### Vessel & captain config SOs

- **SO_Vessel** — the master vessel definition (global namespace): identity (`VesselClassType Class`, name, description), element config (`PrimaryElement`, `SO_Element`, `ResourceCollection InitialResourceLevels`), a large visuals block (icons, preview/squad/trail/card sprites), `List<SO_VesselAbility> Abilities`, `List<SO_ArcadeGame> Games`, `List<SO_TrainingGame> TrainingGames`, three `GameplayParameter` axes, and unlock config (`isLocked`, `UnlockCost`, runtime `Unlock()`/`Lock()`). File also declares the **GameplayParameter** struct (`LeftHandLabel`/`RightHandLabel`/`Value`). `SO_Vessel.cs`
- **SO_VesselAbility** — one vessel ability's display data (name, description, active/inactive icons, `VideoPlayer PreviewClip`, runtime-only `Vessel` backlink). Global namespace. `SO_VesselAbility.cs`
- **SO_VesselList** — `List<SO_Vessel> VesselList` with `TryGetVesselByClass(...)`. Global namespace. `SO_VesselList.cs`
- **SO_Captain** — captain profile: name/description/AI-behavior/flavor text, portrait + headshot + active/inactive icon sprites, linked `SO_Vessel`, `PrimaryElement` + `SO_Element`, `ResourceCollection InitialResourceLevels`. `SO_Captain.cs`
- **SO_CaptainList** — `List<SO_Captain> CaptainList`. `SO_CaptainList.cs`

### Elements, comeback & AI config SOs

- **SO_Element** — UI representation of an `Element` with level-indexed active/inactive icon lists (`GetIcon(level 0-5, active)`, `GetFullIcon`). `SO_Element.cs`
- **SO_ElementalComebackProfile** — per-minigame elemental comeback tuning; a list of **VesselComebackConfig** structs (per-`VesselClassType` Mass/Charge/Space/Time buff weights + initial `[-5,15]` element levels, with `GetWeight`/`GetInitialLevel`) plus a `defaultConfig` fallback via `GetConfig(vesselClass)`. `SO_ElementalComebackProfile.cs`
- **SO_AIProfileList** — `List<AIProfile>` with `PickRandom(count)` and `FindByName`. File also declares the **AIProfile** struct (`Name`, `Sprite AvatarSprite`). `SO_AIProfileList.cs`
- **SO_ProfileIconList** — `List<ProfileIcon>` avatar registry; declares the **ProfileIcon** struct (`Name`, `Id`, `Sprite IconSprite`). `SO_ProfileIconList.cs`

### Progression, quest & XP config SOs

- **SO_GameModeQuestData** — one mode-unlock quest: identity (`GameMode`, `DisplayName`, `Description`, `Icon`), unlock condition (`QuestTargetType TargetType`, `TargetValue`), an intensity-unlock block (per-tier stat targets, plays-to-unlock counts, goal descriptions), `Order`, `IsPlaceholder`, and a non-serialized runtime `IsCompleted`. File also declares the **QuestTargetType** enum (`CrystalsCollected(0)`, `RaceTimeUnder(1)`, `JoustsWon(2)`, `ScoreAbove(3)`, `SurvivalTime(4)`, `WinMatch(5)`, `IntensityUnlocked(6)`, `Placeholder(99)`). `SO_GameModeQuestData.cs`
- **SO_GameModeQuestList** — ordered `List<SO_GameModeQuestData> Quests` (index 0 unlocked from start). `SO_GameModeQuestList.cs`
- **SO_ProgressionConfig** — designer knobs for the unlock system: `alwaysUnlockedModes` (default Tournament), `firstQuestAlwaysUnlocked`, `defaultMaxIntensity`/`maxIntensity`, `fullIntensityModes`, `participationXpPerGame`, `vesselHangarQuestDisplayName`, with `IsAlwaysUnlocked`/`HasFullIntensity` helpers. `SO_ProgressionConfig.cs`
- **SO_QuestChain** — `List<Quest> Quests` (the `Quest` type lives in `CosmicShore.Core`, outside this scope). `SO_QuestChain.cs`
- **SO_XPTrackData** — the XP milestone track (`xpPerMilestone`, `List<XPMilestone> milestones`) with index/progress/normalized/reward/newly-unlocked helper math. File also declares the **XPMilestone** class (wraps an `SO_XPTrackReward`). `SO_XPTrackData.cs`
- **SO_XPTrackReward** — one milestone reward (`rewardId`, `rewardName`, `icon`, `unlockDescription`, `unlockType`, `unlockReferenceId`). `SO_XPTrackReward.cs`
- **SO_TrainingGame** — wraps an `SO_ArcadeGame` with two `SO_Element`s, an `SO_Vessel`, `DailyChallengeIntensity`, seven `GameplayReward`s (three daily-challenge tiers + four intensity tiers), and an `SO_QuestChain`. `SO_TrainingGame.cs`
- **SO_TrainingGameList** — `List<SO_TrainingGame> Games`. `SO_TrainingGameList.cs`

### Episodes & monetization SOs

- **SO_EpisodeData** — one episode card: `episodeId`, `title`, `description`, `cardImage`, `episodeNumber`, `amount` string, `isAvailable`, plus web-checkout fields `priceUsd` and per-episode `checkoutUrl`. `SO_EpisodeData.cs`
- **SO_EpisodeList** — `List<SO_EpisodeData> episodes`. `SO_EpisodeList.cs`
- **SO_IAPConfig** — web-checkout config (Steam/PC, no store SDK): `checkoutBaseUrl`, `supportUrl`, `currencySymbol`, `defaultBuyLabel`, `openInExternalBrowser`, with `BuildCheckoutUrl(productId, price, overrideUrl)` (token substitution) and `FormatPrice`. `SO_IAPConfig.cs`

### Color, material, audio & flora SOs

- **SO_Color_Palette** — a small named palette (two team colours ×2, UI, trail) keyed by `PaletteUUID`. Declared in file `SO_ColorPalette.cs`.
- **SO_ColorSet** — the full per-domain colour authority: a `DomainColorSet` per domain (Jade/Ruby/Gold/Blue) + one `EnvironmentColorSet`, with `TryGetColorSetByDomain` and `GetDomainUIColor` (returns the domain's `TrailHighlightColor`). File also declares **DomainColorSet** (~19 HDR ship/block/shield/AOE/spike/skimmer/crystal/trail colours) and **EnvironmentColorSet** (sky/light/dark/CTA/danger). `SO_ColorSet.cs`
- **SO_MaterialSet** — the shared material bundle: ship, block variants (transparent/shielded/super-shielded/dangerous), crystal materials, AOE/spike/skimmer materials, and a silhouette prefab. `SO_MaterialSet.cs`
- **SO_Song** — one audio track (`AudioClip Clip`, `Decription`, `Author`). `SO_Song.cs`
- **FloraCollection** — `List<Flora> prefabs` with `GetRandomPrefab()`. Declared in file `SO_FloraCollection.cs`.

### Toy definition SOs

Config for the freestyle "toybox" — world-space stations the local vessel flies into, with no score/end condition. In `ScriptableObjects/Toys/`.

- **ToyDefinitionSO** — abstract base: identity (`id` unlock key, `displayName`, `description`), `unlockedByDefault`, `placementAngleDegrees`, `accentColor`, plus the abstract `Spawn(parent, ToyPlacement, ToyContext)` each subclass overrides and an internal `SetRuntimeMetadata` for code-built defaults. `ToyDefinitionSO.cs`
- **ToyboxSO** — the toy registry + per-toy unlock state (`List<ToyDefinitionSO> toys`, an id→bool `_unlocked` map, `OnToyboxChanged` event); `IsUnlocked`, `UnlockedToys()`, `SetToyUnlocked(id, bool)` (deferred-persistence hook), `AddToy`. `ToyboxSO.cs`
- **PaintingToyDefinitionSO** — "fly by numbers" painting toy; carries a `ShapeDefinition shape`, `shapeScale`, `reachThreshold`, `originForwardOffset`; spawns a `PaintingToy` driving a self-contained `MenuShapePainter`. `PaintingToyDefinitionSO.cs`
- **VesselChangerToyDefinitionSO** — cycles the local vessel class; optional `VesselClassType[] vesselCollection`; spawns a `VesselChangerToySet` reusing the networked `RequestSwap` pipeline. `VesselChangerToyDefinitionSO.cs`
- **DomainChangerToyDefinitionSO** — cycles the local domain via server-authoritative `RequestSetDomain_ServerRpc`; spawns a `DomainChangerToySet`. `DomainChangerToyDefinitionSO.cs`
- **ConveyorToyDefinitionSO** — the "Wanderway" microscene conveyor; a deep content/belt config (prism prefab, optional `SkimmerCrystalEffectSO[]` + omni-crystal prefab, `MicroscenePalette`, `lifeformScenes` flag, and belt tuning: pool size, per-scene prism budget, radii/spacing/lookahead, recycle/transition timing, turn-break angle, seed). `BuildConfig()` packs these into a `ConveyorConfig` consumed by the spawned `ConveyorToy`. `ConveyorToyDefinitionSO.cs`

### Interactions & patterns

- **SOAP channels**: These types are the payloads of SOAP events/variables elsewhere — `BootStatusRequest` (Event_BootStatusRequest), `ApplicationState`/`MainMenuState` (written by `ApplicationStateMachine`/`MainMenuController` into SOAP state variables), `GameplayReward`/`DailyChallenge*` (rewards flow), and `ToyboxSO.OnToyboxChanged` (a plain C# event the toy UI/persistence observe).
- **NetworkVariables**: `RoundStats` is the primary networked data model in scope — ~30 Server-write `NetworkVariable<T>` fields mirrored to local fields, with `Domain` intentionally excluded (sourced from `Player.NetDomain`). `ScoreResult`/`DomainStats` are the deterministic, already-synced end-game reductions built identically on each client.
- **DI / config separation**: The `SO_*` assets are the concrete embodiment of the project's ScriptableObject config-separation rule — `SO_GameList`/`SO_VesselList`/`SO_CaptainList`/`SO_AIProfileList`/`ToyboxSO` are DI-injected or Resources-loaded registries queried by launch, spawning, AI-backfill, and menu systems; `SO_ProgressionConfig`/`SO_GameModeQuestList` externalize formerly-hardcoded unlock rules; `SO_ElementalComebackProfile` and `SO_ColorSet`/`SO_MaterialSet` feed gameplay/theme tuning.
- **Scoring spine**: `ScoringMetric` + `IRoundStats`/`RoundStats` + `ScoreResult` form the unified per-domain scoring path (a mode's `ScoringRuleSO` picks one `ScoringMetric`, aggregates the matching `RoundStats` field by `Domain`, and emits `ScoreResult` rows).
- **Ecosystem coupling**: `CellPhase`/`CellAggressionLevel`/`FaunaDiet`/`PrismKind` are the locked ecology invariants; `EcosystemDensitySetting` and `PhysicsDetailSetting` (graphics enums) are the only sanctioned production/collider levers, deliberately never expressing decay (conserved-mass rule).
- **File-vs-type naming drift**: several files are named for a newer concept than the type they declare — `VesselActions.cs`→`ShipActions`, `VesselImpactEffects.cs`→`ShipImpactEffects`, `PassiveAbilities.cs`→`ShipCameraOverrides`, `VesselThrottleModifier.cs`/`VesselVelocityModifier.cs`→`ShipThrottleModifier`/`ShipVelocityModifier`, `SO_ColorPalette.cs`→`SO_Color_Palette`, `SO_FloraCollection.cs`→`FloraCollection`; and `InputDeviceType` is the one enum outside the `CosmicShore.Data` namespace.

---

## SOAP — Scriptable Object Architecture Pattern Types

This area holds Cosmic Shore's **custom SOAP types** — the project-specific extensions of the Obvious.Soap asset (`Assets/Plugins/Obvious/Soap/`) that form the backbone of cross-system communication. SOAP replaces singletons, static events, and direct MonoBehaviour references with three decoupled primitives that live as `.asset` files: a `ScriptableVariable<T>` (shared, observable state container), a `ScriptableEvent<T>` (one-to-many broadcast channel), and an `EventListener<T>` (inspector-wirable MonoBehaviour that maps a channel to `UnityEvent` responses). Every custom type here follows the project convention of a **Variable / Event / Listener triple** per payload `T` (some payloads need only a subset), plus a small number of `ScriptableList<T>` reactive collections and a bespoke **return-channel** extension for request/response. Assets are authored via `[CreateAssetMenu]` with a fixed naming scheme — `Variable_<T>`, `Event_<T>`, `List_<T>` under `ScriptableObjects/…` menu paths. All types live in namespace `CosmicShore.ScriptableObjects`. The house rule (CLAUDE.md) is **fail-loud**: no if-null guards on serialized `ScriptableEvent` fields, and payload types are pure data — no gameplay logic in variables or events.

### The SOAP primitive pattern (framework base classes)

Every file in this area derives from Obvious.Soap generics; the custom classes are almost always empty subclasses whose only job is to fix `T` so Unity can serialize a concrete asset/component. The recurring shapes:

- **`ScriptableVariable<T>`** — persistent, asset-backed state with an `OnValueChanged` event. Custom subclasses named `<T>Variable`, authored via `[CreateAssetMenu(fileName="Variable_"+nameof(T), …)]`. Reference types don't auto-fire `OnValueChanged` on member mutation, so those carry embedded `ScriptableEvent`s instead (see `ApplicationStateData`, `AuthenticationData`).
- **`ScriptableEvent<T>`** — decoupled broadcast channel; `Raise(T)` invokes listeners **inline**. Custom subclasses named `ScriptableEvent<T>`, authored via `[CreateAssetMenu(fileName="Event_"+nameof(T), …)]`.
- **`ScriptableEventNoParam`** — parameterless framework channel, referenced (not subclassed) as fields inside payload models for lifecycle pings (`OnSignedIn`, `OnNetworkLost`, etc.).
- **`EventListenerGeneric<T>` / `EventResponse<T>`** — the MonoBehaviour half: a serialized `EventResponse[]` array, each entry pairing one `ScriptableEvent<T>` to a `UnityEvent<T>` response wired in the inspector. Custom subclasses named `EventListener<T>` with `[AddComponentMenu("Soap/EventListeners/…")]`, plus a nested `[Serializable] EventResponse` and a nested `UnityEvent<T>` type.
- **`ScriptableList<T>`** — asset-backed reactive collection with add/remove/clear events. Custom subclasses named `ScriptableList<T>`, authored via `[CreateAssetMenu(fileName="List_"+nameof(T), …)]`.

### Application, bootstrap & session state

Reference-type SOAP variables written by a single owning service and read/observed widely. Because they are classes, state-change notification rides an embedded `ScriptableEventNoParam`/`ScriptableEvent`, not `OnValueChanged`.

- **`ApplicationStateData`** — model holding `State` + `PreviousState` (the `ApplicationState` enum) and an embedded `ScriptableEventApplicationState OnStateChanged`; written exclusively by `ApplicationStateMachine`. `ScriptableObjects/SOAP/ScriptableApplicationState/`
- **`ApplicationStateDataVariable`** — `ScriptableVariable<ApplicationStateData>` asset (the shared app-phase state).
- **`ScriptableEventApplicationState`** — `ScriptableEvent<ApplicationState>` broadcast of the new phase on every valid transition.
- **`EventListenerApplicationState`** — inspector listener for `ApplicationState` (nested `ApplicationStateUnityEvent`).
- **`AuthenticationData`** — model with `PlayerId`, `IsSignedIn`, an `AuthState` enum (`NotInitialized→…→SignedIn|Failed`), a `StringVariable UserName`, and three embedded `ScriptableEventNoParam`s: `OnSignedIn`, `OnSignedOut`, `OnSignInFailed`. Sole writer is `AuthenticationServiceFacade`. `ScriptableObjects/SOAP/ScriptableAuthenticationData/`
- **`AuthenticationDataVariable`** — `ScriptableVariable<AuthenticationData>` asset.
- **`NetworkMonitorData`** — model with `refreshInterval`, `IsOnline`, `LastTransitionUnscaledTime`, and embedded `OnNetworkFound` / `OnNetworkLost` `ScriptableEventNoParam`s; written by `NetworkMonitor`, read by `NetworkDiagnostics`. (Same folder as AuthenticationData.)
- **`NetworkMonitorDataVariable`** — `ScriptableVariable<NetworkMonitorData>` asset.
- **`BootStatusRequest` channel** — drives the boot/loading status surface. `ScriptableEventBootStatusRequest` (`ScriptableEvent<BootStatusRequest>`) + `EventListenerBootStatusRequest`; the `BootStatusRequest` payload struct (`Mode` + `Text`) lives in `Data/Structs/`, and `BootStatusPanel` is the sole subscriber. `ScriptableObjects/SOAP/ScriptableBootStatus/`

### Ecosystem — cell phase

Value-type variable for the cell's ecological state axis, driving fauna aggression and HUD.

- **`CellPhaseVariable`** — `ScriptableVariable<CellPhase>` (enum `None/Calm/Restless/Frenzy`, defined in `Data/Enums/`). `ScriptableObjects/SOAP/ScriptableCellPhase/`
- **`ScriptableEventCellPhase`** — `ScriptableEvent<CellPhase>` phase-change broadcast.
- **`EventListenerCellPhase`** — inspector listener (nested `CellPhaseUnityEvent`).

### Vessel class & impact/debuff channels

Vessel-class identity plus the elemental debuff-application events fired by the impact-effect system. Note the folder is `ScriptableClassType/` but the class-type triple keeps legacy "Ship" names.

- **`VesselClassTypeVariable`** — `ScriptableVariable<VesselClassType>` (enum from `Data/Enums/`). `ScriptableObjects/SOAP/ScriptableClassType/`
- **`ScriptableEventShipClassType`** — `ScriptableEvent<VesselClassType>` (asset filename `Event_VesselClassType`).
- **`EventListenerShipClassType`** — inspector listener for `VesselClassType` (nested `ShipClassTypeUnityEvent`).
- **`ScriptableEventVesselImpactor`** — `ScriptableEvent<VesselImpactor>` broadcasting a vessel impactor reference (payload `VesselImpactor : ImpactorBase`).
- **`ScriptableEventExplosionDebuffApplied`** — `ScriptableEvent<ExplosionDebuffPayload>`; declares the `[Serializable] struct ExplosionDebuffPayload { IVessel Vessel; float Duration; }` inline.
- **`ScriptableEventSkimmerDebuffApplied`** — `ScriptableEvent<SkimmerDebuffPayload>`; declares `struct SkimmerDebuffPayload { IVesselStatus Attacker; IVesselStatus Victim; float Duration; }` inline.

### Gameplay stat channels (scoring / telemetry)

Event+Listener pairs (no variable) carrying per-event stat structs from `StatsManager` (namespace `CosmicShore.Gameplay`) to scoring, HUD, and telemetry consumers.

- **`ScriptableEventAbilityStats`** + **`EventListenerAbilityStats`** — `AbilityStats { string PlayerName; InputEvents ControlType; float Duration; }`. `ScriptableObjects/SOAP/ScriptableAbilityStats/`
- **`ScriptableEventCrystalStats`** + **`EventListenerCrystalStats`** — `CrystalStats { string PlayerName; Element Element; float Value; }`. `ScriptableObjects/SOAP/ScriptableCrystalStats/`
- **`ScriptableEventPrismStats`** + **`EventListenerPrismStats`** — `PrismStats { string OwnName; float Volume; string AttackerName; }`. `ScriptableObjects/SOAP/ScriptablePrismStats/`

### Party & Friends social data

Immutable identity/invite structs used both as SOAP event payloads and as `ScriptableList` element types. Structs override `Equals`/`GetHashCode` on `PlayerId` only, so list dedup survives mutable presence fields. Written by `HostConnectionService` (party) and `FriendsServiceFacade` (friends).

- **`PartyInviteData`** — struct `{ HostPlayerId, PartySessionId, HostDisplayName, HostAvatarId }`; immutable invite payload. `ScriptableObjects/SOAP/ScriptablePartyData/`
- **`ScriptableEventPartyInviteData`** + **`EventListenerPartyInviteData`** — invite-notification channel + listener.
- **`PartyPlayerData`** — struct `{ PlayerId, DisplayName, AvatarId, PartyMemberCount, PartyMaxSlots, MatchName }`; player identity + advertised party state (equality by `PlayerId`).
- **`ScriptableEventPartyPlayerData`** + **`EventListenerPartyPlayerData`** — party-member-change channel + listener.
- **`ScriptableListPartyPlayerData`** — `ScriptableList<PartyPlayerData>` backing `OnlinePlayers` / `PartyMembers`.
- **`FriendData`** — struct `{ PlayerId, DisplayName, Availability (int, maps UGS Availability enum), ActivityStatus }` with an `IsOnline` helper; immutable friend snapshot (equality by `PlayerId`). `ScriptableObjects/SOAP/ScriptableFriendData/`
- **`ScriptableEventFriendData`** + **`EventListenerFriendData`** — friend added/removed channel + listener.
- **`ScriptableListFriendData`** — `ScriptableList<FriendData>` backing `Friends`, `IncomingRequests`, `OutgoingRequests`, `BlockedPlayers`.
- **`FriendPresenceActivity`** — `[Preserve][DataContract]` rich-presence payload for the UGS Friends SDK (`Status`, `Scene`, `VesselClass`, `PartySessionId`, `PartyMemberCount`, `PartyMaxSlots`, `MatchName`), serialized over the wire alongside the availability enum. Not a SOAP type itself; the serializable companion to `FriendData`.

### Vessel HUD, rendering & geometry channels

Event+Listener pairs (mostly no variable) for HUD reparenting, silhouette/PiP rendering, and generic Unity-value broadcasts. The `ScriptableVesselHUDData/` folder keeps legacy "ShipHUDData" type names.

- **`ShipHUDData`** — struct `{ MiniGameHUD ShipHUD; }` (references `CosmicShore.UI`). `ScriptableObjects/SOAP/ScriptableVesselHUDData/`
- **`ScriptableEventShipHUDData`** + **`EventListenerShipHUDData`** — vessel-HUD-initialized channel (drives HUD reparenting into the menu "Game UI" via `MiniGameHUD.OnShipHUDInitialized`).
- **`PipData`** — struct `{ bool IsActive; bool IsMirrored; }` (picture-in-picture toggle). `ScriptableObjects/SOAP/ScriptablePipData/`
- **`ScriptableEventPipData`** + **`EventListenerPipData`** — PiP channel + listener.
- **`SilhouetteData`** — struct `{ SilhouetteController Sender; bool IsSilhouetteActive; bool IsTrailDisplayActive; List<GameObject> Silhouettes; }`. `ScriptableObjects/SOAP/ScriptableSilhouetteData/`
- **`ScriptableEventSilhouetteData`** + **`EventListenerSilhouetteData`** — silhouette-render channel + listener.
- **`ScriptableEventTransform`** + **`EventListenerTransform`** — generic `ScriptableEvent<UnityEngine.Transform>` broadcast (e.g., follow-target hand-off). `ScriptableObjects/SOAP/ScriptableTransform/`
- **`ScriptableEventQuaternion`** + **`EventListenerQuaternion`** — generic `ScriptableEvent<Quaternion>` broadcast. `ScriptableObjects/SOAP/ScriptableQuaternion/`
- **`ScriptableEventUlong`** — generic `ScriptableEvent<ulong>` (no matching listener/variable; used for `OwnerClientId`-style player-spawn pings such as `OnPlayerNetworkSpawnedUlong`). `ScriptableObjects/SOAP/ScriptableUlong/`

### Input & audio channels

Decoupled category enums broadcast to input handlers and the audio system.

- **`ScriptableEventInputEvents`** + **`EventListenerInputEvents`** — `ScriptableEvent<InputEvents>` (the `InputEvents` control-gesture enum in `Data/Enums/`). `ScriptableObjects/SOAP/ScriptableInputEvents/`
- **`ScriptableEventGameplaySFX`** + **`EventListenerGameplaySFX`** — `ScriptableEvent<GameplaySFXCategory>` (enum from `System/Audio/AudioSystem.cs`: `BlockDestroy`, `ShieldActivate/Deactivate`, `MineExplode`, `ProjectileLaunch`, `CrystalCollect`, `VesselImpact`, `GameEnd`, `ScoreReveal`, `PauseOpen/Close`, `GunFire`, `BoostActivate`, `Explosion`, `CreatureDeath`, `DriftStart`, …), letting gameplay raise sound categories without referencing the audio system. `ScriptableObjects/SOAP/ScriptableGameplaySFX/`

### Request/response return channels (custom SOAP extension)

Standard SOAP events are fire-and-forget; this bespoke extension adds a **synchronous return value**, used where a raiser needs a result back (e.g. spawning a prism and getting the spawned object).

- **`GenericEventChannelWithReturnSO<T, Y>`** — abstract `ScriptableObject` exposing `event Func<T, Y> OnEventReturn` and `Y RaiseEvent(T item)` (returns `default(Y)` when no listener). The generic request/response channel base. `ScriptableObjects/SOAP/ScriptableEventWithReturn/`
- **`PrismEventChannelWithReturnSO`** — concrete `GenericEventChannelWithReturnSO<PrismEventData, PrismReturnEventData>`. Declares `PrismEventData` (mutable class: `Domains ownDomain` (formerly `OwnTeam`), `Rotation`, `SpawnPosition`, `Scale`, `Velocity`, `Volume`, `PrismType`, `TargetTransform`, `Action OnGrowCompleted`) and `PrismReturnEventData { GameObject SpawnedObject; }` — the prism-spawn request/response channel used by `PrismFactory`.

### Data-container SO

Not a SOAP variable/event, but colocated in this folder as an inspector-wired lookup asset.

- **`VesselPrefabContainer`** — `ScriptableObject` holding a `Transform[] _shipPrefabs`; `TryGetShipPrefab(VesselClassType, out Transform)` scans prefabs by their `IVesselStatus.VesselType` and fails loud (`CSDebug.LogError`) if none match. The vessel-class→prefab map consumed by `ServerPlayerVesselInitializer`/`VesselSpawner`. `ScriptableObjects/SOAP/VesselPrefabContainer.cs`

### Interactions & patterns

- **DI registration:** the reference-type SOAP variables here (`AuthenticationDataVariable`, `NetworkMonitorDataVariable`, `ApplicationStateDataVariable`, plus `GameDataSO`, `FriendsDataSO`, `HostConnectionDataSO`) are registered in `AppManager.InstallBindings()` via `RegisterValue` and injected with `[Inject]`, so systems read shared state without direct references.
- **Single-writer discipline:** each variable has exactly one writer service (`AuthenticationServiceFacade`, `ApplicationStateMachine`, `NetworkMonitor`, `HostConnectionService`, `FriendsServiceFacade`) and many observers — the same pattern the rest of the game follows.
- **Reference-type notification quirk:** models like `ApplicationStateData`/`AuthenticationData` embed `ScriptableEventNoParam`/`ScriptableEvent` fields because `ScriptableVariable<T>.OnValueChanged` does not fire on member mutation of a class payload.
- **Inline `Raise()` = threading hazard:** SOAP `Raise()` invokes listeners synchronously on the calling thread, so payloads originating from UGS/Netcode `Task` continuations must be marshaled to the main thread (`.AsMainThread()`) before raise — see `Docs/THREADING.md`.
- **Payload location split:** most payload structs/enums live outside this folder (`Data/Enums/`, `Data/Structs/`, `Controller/Managers/StatsManager.cs`, `System/Audio/AudioSystem.cs`, `Controller/ImpactEffects/`) and are wrapped here into channels; a few payloads (`ExplosionDebuffPayload`, `SkimmerDebuffPayload`, `PrismEventData`/`PrismReturnEventData`, `PipData`, `SilhouetteData`, `ShipHUDData`, `PartyInviteData`, `PartyPlayerData`, `FriendData`, `FriendPresenceActivity`) are declared in-place.
- **Naming drift to watch:** folder `ScriptableClassType/` → types `…ShipClassType`; folder `ScriptableVesselHUDData/` → type `ShipHUDData`; `PrismEventData.ownDomain` has `[FormerlySerializedAs("OwnTeam")]` — all legacy "Ship"/"Team" holdovers preserved for serialization stability.
- **Downstream reach:** these channels feed nearly every subsystem — Party/Presence/Friends UI (`ScriptableList*`, party/friend events), scoring & telemetry (stat events), HUD (`ShipHUDData`, `PipData`, `SilhouetteData`), audio (`GameplaySFX`), ecology (`CellPhase`), bootstrap/auth/network lifecycle (state variables), player spawning (`ScriptableEventUlong`, `VesselPrefabContainer`), and prism spawning (`PrismEventChannelWithReturnSO`).

---

## Utility — Data Containers, Network, Effects, Pooling, Extensions

This area is the shared substrate the rest of the game is wired to: the `ScriptableObject` data containers that hold cross-system runtime state and the SOAP event channels that decouple producers from consumers (the "single asset every system references" pattern), the ecology/cell configuration SOs, the Netcode helper components and serialization glue, the object-pooling base + prism VFX effects, and the async/threading/transform extension methods. Almost everything here is either a `ScriptableObject` asset consumed via `[Inject]`/inspector wiring, a small `MonoBehaviour` component, or a static helper class. The two biggest hubs are `GameDataSO` (the central minigame runtime state + ~20 SOAP events, the spine of the launch/spawn/scoring/end-game flow) and `GenericPoolManager<T>` (the pooling base used by all prism VFX and interactive prisms).

### Central runtime data containers (SOAP-backed ScriptableObjects)

The core `ScriptableObject` singletons that hold shared runtime state and expose SOAP `ScriptableEvent` channels; a single asset of each is registered in DI (`AppManager`) or wired into the relevant service, and every system reads/subscribes rather than referencing each other directly.

- **GameDataSO** — the central minigame runtime state + event bus that links `MiniGameController`, `SceneLoader`, `MultiplayerSetup`, turn monitors, scoring, and spawning; `Assets/_Scripts/Utility/DataContainers/GameDataSO.cs`. Holds ~20 `ScriptableEvent*` channels (`OnLaunchGame`, `OnSceneTransition`, `OnSessionStarted`, `OnInitializeGame`, `OnMiniGameRoundStarted`, `OnClientReady`, `OnMiniGameTurnStarted`/`End`, `OnMiniGameRoundEnd`, `OnMiniGameEnd`, `OnWinnerCalculated`, `OnResetForReplay`, `OnSessionEnded`, `OnPlayerNetworkSpawnedUlong`, `OnVesselNetworkSpawned`, `OnPlayerPairInitialized`, `OnShowGameEndScreen`) plus C# events `OnPlayerAdded`/`OnDomainMetricSumsChanged`; config/state fields (`SceneName`, `GameMode`, `IsMultiplayerMode`, `IsTournamentMode`, `RequestedAIBackfillCount`, `RequestedDomainCount`, `IsGolfRules`); rosters (`Players`, `Vessels`, `RoundStatsList`, `DomainStatsList`, `SpawnPoses`, `LocalPlayer`); server-authoritative results (`WinnerName`, `WinnerDomain`, `Results`, `CrystalTargetCount`, `JoustTargetCount`, `GoalTargetCount`, `ScoringRule`, per-domain metric sums); and the domain-balancing statics (`ActiveDomains = {Jade,Ruby,Gold}`, `IsActiveDomain`, `BuildHumanCounts`). Owns the roster dedup/prune logic (name-keyed `AddPlayer` with stale-`RoundStats` shadow replacement), spawn-pose assignment, sort/aggregation helpers (`SortRoundStats`, `CalculateDomainStats`, `SumCrystalsCollectedByDomain`, `GetTeamVolumes`), and the layered reset methods (`ResetRuntimeData`, `ResetRuntimeDataForPartyJoin`, `ResetRuntimeDataForReplay`, `ResetAllData`).
- **SceneNameListSO** — centralized scene-name registry (Bootstrap / Authentication / Menu_Main / Multiplayer), DI-injected to replace hardcoded scene strings; `Assets/_Scripts/Utility/DataContainers/SceneNameListSO.cs`.
- **HostConnectionDataSO** — SOAP container for the presence-lobby + party system; holds `OnlinePlayers`/`PartyMembers` reactive lists, connection/party/invite events (`OnHostConnectionEstablished`/`Lost`, `OnPartyMemberJoined`/`Left`/`Kicked`, `OnInviteReceived`/`Sent`/`Resolved`, `OnPartyJoinCompleted`), local-player identity, `IsPartyHost`/`IsPresenceLobbyHost` flags, `MaxPartySlots`, and `RemovePartyMember`; single-writer is `HostConnectionService`. `Assets/_Scripts/Utility/DataContainers/HostConnectionDataSO.cs`.
- **FriendsDataSO** — SOAP container for the UGS Friends system; four `ScriptableListFriendData` lists (`Friends`, `IncomingRequests`, `OutgoingRequests`, `BlockedPlayers`), events (`OnFriendAdded`/`Removed`, `OnFriendRequestReceived`/`Sent`, `OnFriendsServiceReady`), and computed counts (`FriendCount`, `OnlineFriendCount`); single-writer is `FriendsServiceFacade`. `Assets/_Scripts/Utility/DataContainers/FriendsDataSO.cs`.
- **BenchmarkDataSO** — SOAP container for the performance-benchmark runner; lifecycle events (`OnBenchmarkStarted`, `OnSamplingStarted`, `OnBenchmarkCompleted`, `OnBenchmarkStopped`, `OnProgressUpdated` — the last three carry `BenchmarkStateData`) plus runtime state fields. `Assets/_Scripts/Utility/DataContainers/BenchmarkDataSO.cs`.

### Cell / ecology configuration & runtime SOs

The data-driven ecosystem: authored config SOs describe a cell's membrane/atmosphere/spawn population and its volume-driven phase ladder, and a runtime SO holds live per-cell state. These are the "Cell owns the environment" assets referenced by the locked ecology invariants.

- **CellConfigDataSO** — per-cell authored config: shell name/icon/difficulty, visual prefabs (`MembranePrefab`, `NucleusPrefab`, `CytoplasmPrefab` as a `SnowChanger`), `CellModifier` list, the `SpawnProfileSO`, an optional mass-sensing radius override, and the `CellPhaseThresholds`. `Assets/_Scripts/Utility/DataContainers/CellConfigDataSO.cs`.
- **CellPhaseThresholds** (+ **CellPhaseRules**) — `[Serializable]` struct of per-biome up/down thresholds driving the Calm→Restless→Frenzy phase ladder on live per-domain prism **volume** (the spine), with a legacy prism-**count** backstop for perf; `NominalPrismVolume=16f` anchor, `WithDerivedVolumeScale()` migration, `IsAllZero`/`Default`. `CellPhaseRules.Compute()` is the static hysteresis state machine that resolves the new phase. `Assets/_Scripts/Utility/DataContainers/CellPhaseThresholds.cs`.
- **CellRuntimeDataSO** — live per-cell runtime state SO: `Config` reference, `CellStatsList` dictionary, `Cell`/`CellItems`/`Crystals` lists, crystal add/remove/query helpers (`TryGetLocalCrystal` by domain→Blue→first, `TryGetCrystalById`), `WriteCellRuntimeStats` (server phase/dominant-domain write raising `OnPhaseChanged`), and `ResetRuntimeData` (destroys crystals). Wires SOAP events `OnResetForReplay`, `OnCrystalSpawned`, `OnCellItemsUpdated`, `OnPhaseChanged` (`ScriptableEventCellPhase`). `Assets/_Scripts/Utility/DataContainers/CellRuntimeDataSO.cs`.
- **SpawnProfileSO** — the cell's population profile: flora batch timing + `FloraSpawnVolumeCeiling` + `SupportedFloras`, fauna batch timing + `FaunaFoodFloor` (prey floor in nominal prisms) + `FaunaSpawnVolumeThreshold`/`BaseFaunaSpawnTime` + `SupportedFaunas`. Carries deprecated-inert `FloraExcludeLocalDomain`/`FaunaExcludeLocalDomain` flags kept only for legacy deserialization. `Assets/_Scripts/Utility/DataContainers/SpawnProfileSO.cs`.
- **FloraConfigurationSO** — one flora species entry: `Flora` prefab, spawn probability, initial count, optional plant-period override. `Assets/_Scripts/Utility/DataContainers/FloraConfigurationSO.cs`.
- **FaunaConfigurationSO** — one fauna species entry: `Fauna` prefab, `PopulationSize` seed floor, and the reproduction driver params (`FeedsPerOffspring`, `OffspringPerBirth`, `ReproductionCooldownSeconds`, `MaxLivePopulation` perf cap). `Assets/_Scripts/Utility/DataContainers/FaunaConfigurationSO.cs`.
- **FaunaReproductionRules** — engine-free static decision rules for the prey-linked population pipeline (unit-testable Lotka–Volterra gating): `ShouldBirth`, `SeedSpawnCount`, and `PreyAvailable` (the shared prey-availability gate used by both cell spawners and the freestyle conveyor). `Assets/_Scripts/Utility/DataContainers/FaunaReproductionRules.cs`.

### Tournament data

The single-source-of-truth SO for a Maelstrom/Tournament session plus its display formatter; standings are reduced locally on every peer from the already-synced `GameDataSO.Results` (no extra networking).

- **TournamentDataSO** — authored lineup (`GameQueue`, `ModeCard`, `LobbySceneName`) + scoring table (`PointsByPlace`, `WinTarget`/`EffectiveWinTarget`, `MaxGames`) + runtime standings; SOAP events `OnTournamentStarted`/`OnGameResultRecorded`/`OnStandingsChanged`/`OnTournamentCompleted`. Core logic: `RecordResults` (folds a game's per-player results into per-domain standings + history), `GetDomainPlacementOrder`, `CrystalsForDomain`, `BuildSortedStandings`, `IsShuffleComplete`, `ResolveWinTarget`, `ResetRuntime`. `Assets/_Scripts/Utility/DataContainers/Tournament/TournamentDataSO.cs`.
  - **TournamentDomainStanding** — one team's cumulative standing (`TotalPoints`, per-game `Placements`, `BestPlacement`).
  - **TournamentPlayerSnapshot** — captured per-player finish (name/domain/avatar/AI/rank/score text) surviving the per-scene reset for the hub/summary.
  - **TournamentRoundRecord** — one completed round (mode name, intensity, ranked player snapshots, per-domain `DomainOrder`, `WinningDomain`).
- **TournamentStandingsFormatter** — pure static formatters turning `TournamentDataSO` into display strings so the loading splash and the results screen can't drift: `FormatRunning`, `FormatConnecting`, `FormatFinal` (with `(You)` tagging + ordinal helper). `Assets/_Scripts/Utility/DataContainers/Tournament/TournamentStandingsFormatter.cs`.

### Generic data SOs & plain data structs

Small reusable data holders — a generic typed variable SO with a change event, its concrete instances, and value structs used by UI/scoring.

- **GenericDataSO\<T\>** — abstract typed `ScriptableObject` holding one `Value` with an `OnValueChanged` C# event and an implicit `T` conversion; the base for the concrete data SOs below. `Assets/_Scripts/Utility/DataContainers/GenericDataSO.cs`.
- **IntDataSO / StringDataSO / SpriteDataSO / TagDataSO** — concrete `GenericDataSO<T>` instances for `int`, `string`, `Sprite`, and `TagSO`; each a `[CreateAssetMenu]` asset. `Assets/_Scripts/Utility/DataContainers/{IntDataSO,StringDataSO,SpriteDataSO,TagDataSO}.cs`.
- **VesselDisplayData** — `[Serializable]` struct of vessel display info (playerName, vesselType, ranking, domain, score) for scoreboards. `Assets/_Scripts/Utility/DataContainers/VesselDisplayData.cs`.
- **WildlifeBlitzStats** — struct of Wildlife Blitz end-screen stats (didWin, elapsedTime, lifeFormsKilled, crystalsCollected). `Assets/_Scripts/Utility/DataContainers/WildlifeBlitzStats.cs`.

### End-game & AI cinematic behaviour

Two `MonoBehaviour`s that drive the end-of-game reveal: a coordinator listening on `GameDataSO`, and the dormant AI flourish it uses to keep the local vessel flying.

- **EndGameSequencer** — `MonoBehaviour` on the `EndGameStatsPanel` prefab that, on `GameDataSO.OnWinnerCalculated`, hands the local vessel a random AI flourish, plays GameEnd SFX, reveals a randomized win/lose toast (`EndGameMessageSetSO`), holds for `revealDuration`, then raises `OnShowGameEndScreen`; also re-homes quest-complete / intensity-unlocked toasts from `GameModeProgressionService`. `Assets/_Scripts/Utility/DataContainers/EndGameSequencer.cs`.
- **AICinematicBehavior** (+ **AICinematicBehaviorType** enum) — `MonoBehaviour` giving a vessel scripted flourishes (MoveForward/Loop/Drift/Spiral implemented; BarrelRoll/FlyBy/HoverSpin placeholders) by driving `IVesselStatus.InputStatus`; currently only driven by `EndGameSequencer`. `Assets/_Scripts/Utility/DataContainers/AICinematicBehavior.cs`.

### Netcode utilities

Netcode-for-GameObjects helper components, serialization structs, and small network glue used across the multiplayer stack.

- **NetcodeHooks** — `NetworkBehaviour` exposing `OnNetworkSpawnHook`/`OnNetworkDespawnHook` C# events so non-`NetworkBehaviour` classes (e.g. `ServerPlayerVesselInitializer`) can react to spawn/despawn without inheriting. `Assets/_Scripts/Utility/Network/NetcodeHooks.cs`.
- **ClientNetworkTransform** — `NetworkTransform` subclass overriding `OnIsServerAuthoritative()→false` for owner/client-authoritative transform sync. `Assets/_Scripts/Utility/Network/ClientNetworkTransform.cs`.
- **ClientNetworkAnimator** — `NetworkAnimator` subclass with `OnIsServerAuthoritative()→false` for owner-authoritative animation sync. `Assets/_Scripts/Utility/Network/ClientNetworkAnimator.cs`.
- **CustomNetworkVariable\<T\>** — thin `NetworkVariable<T>` subclass; body (a `ForceNotify`/`NotifyObservers` experiment) is fully commented out — currently an inert placeholder. `Assets/_Scripts/Utility/Network/CustomNetworkVariable.cs`.
- **FixedPlayerName** — `INetworkSerializable` wrapper over `FixedString32Bytes` (single place to change name max-size) with implicit string conversions; serialize/deserialize wrapped in `NetMarkers` profiler markers. `Assets/_Scripts/Utility/Network/FixedPlayerName.cs`.
- **NetworkGUID** (struct `NetworkGuid` + **NetworkGuidExtensions**) — `INetworkSerializeByMemcpy` two-`ulong` GUID with `ToNetworkGuid`/`ToGuid` conversion extensions. `Assets/_Scripts/Utility/Network/NetworkGUID.cs`.
- **NetworkObjectSpawner** — `MonoBehaviour` that, on server after a scene load completes, instantiates + `Spawn`s a `NetworkObject` prefab at its transform (moved to the correct scene, `destroyWithScene:true`) then self-destroys. `Assets/_Scripts/Utility/Network/NetworkObjectSpawner.cs`.
- **ServerAdditiveSceneLoader** — server-only `NetworkBehaviour` trigger volume that additively loads a scene while tagged player-owned `NetworkObject`s are inside and unloads it (after a delay) when all leave; tracks a `SceneState` machine and cleans up on client disconnect. `Assets/_Scripts/Utility/Network/ServerAdditiveSceneLoader.cs`.
- **IPFinder** — static helper returning the local `192.168.*` IPv4 address. `Assets/_Scripts/Utility/Network/IPFinder.cs`.

### Class extension methods

Static extension helpers for async/threading, Netcode, transforms, GameObjects, and debug logging.

- **UniTaskExtensions** — the `.AsMainThread()` boundary helper (four overloads over `Task`/`Task<T>`/`UniTask`/`UniTask<T>`) that awaits a UGS/Netcode task then marshals back to Unity's main thread via `MainThreadDispatcher`, plus `WaitOneFrame`. This is the documented fix for the off-thread SOAP/`UnityEngine.Object` crash cascade. `Assets/_Scripts/Utility/ClassExtensions/UniTaskExtensions.cs`.
- **MainThreadDispatcher** *(counterpart in `Assets/_Scripts/Utility/MainThreadDispatcher.cs`, just outside the subfolder)* — captures Unity's `SynchronizationContext` at `BeforeSceneLoad`; exposes `IsOnMainThread` and `SwitchToMainThreadAsync()` (the reliable main-thread switch `.AsMainThread()` delegates to).
- **NetcodeExtensions** — `ulong.IsLocalClient()` comparing against `NetworkManager.Singleton.LocalClientId`. `Assets/_Scripts/Utility/ClassExtensions/NetcodeExtensions.cs`.
- **TransformExtensions** — `SetFullProperties`, `ToGlobal`, and the `ResizeForSeconds` UniTask animation (per-transform CTS dictionary, external-token linking, `CancelResize`). `Assets/_Scripts/Utility/ClassExtensions/TransformExtensions.cs`.
- **GameObjectExtension** — `GetOrAdd<T>`, `OrNull<T>`, `DestroyChildren`/`EnableChildren`/`DisableChildren`, `IsLayer`, `TryGetInterface<TInterface>`. `Assets/_Scripts/Utility/ClassExtensions/GameObjectExtension.cs`.
- **DebugExtensions** — `LogWithClassMethod`/`LogWarning…`/`LogError…` and color-wrapped `LogColored`/`LogWarningColored`/`LogErrorColored` (all routing through `CSDebug`). `Assets/_Scripts/Utility/ClassExtensions/DebugExtensions.cs`.

### Object pooling

The generic pooling base and its concrete managers; all prism VFX and interactive prisms pool through it.

- **GenericPoolManager\<T\>** — abstract `MonoBehaviour` wrapping Unity's `ObjectPool<T>` with prewarm, an active-object tracking `HashSet`, a Burst-friendly async **buffer-maintenance loop** (adaptive instantiate rate, `maxAddsPerFrame`), graceful batched `ReleaseAllActiveAsync`/synchronous `ReleaseAllActive` (to avoid network-heartbeat stalls on scene transitions), and overridable Create/Get/Release callbacks. Abstract `Get`/`Release` implemented by subclasses. `Assets/_Scripts/Utility/PoolsAndBuffers/GenericPoolManager.cs`.
- **PrismExplosionPoolManager** / **PrismImplosionPoolManager** / **InteractivePrismPoolManager** — concrete `GenericPoolManager` subclasses for explosion VFX, implosion/grow VFX, and interactive `Prism`s; each releases-all on `activeSceneChanged` (and, for prisms, on SOAP `OnResetForReplay`/`OnSceneTransition`), and auto-subscribes/unsubscribes the pooled object's `OnReturnToPool` callback in `Get`/`Release`. `Assets/_Scripts/Utility/Effects/{PrismExplosionPoolManager,PrismImplosionPoolManager,InteractivePrismPoolManager}.cs`.

### Visual effects (Effects/)

Pooled prism VFX driven by the centralized Burst `PrismEffectsManager`, plus small standalone shader/UI effect `MonoBehaviour`s.

- **PrismExplosion** — pooled prism-destruction VFX; stores explosion state (velocity/speed/elapsed) and registers with `PrismEffectsManager` for batched Burst animation, using a `MaterialPropertyBlock` (shader props `_Velocity`/`_ExplosionAmount`/`_Opacity`); renderer stays disabled until the manager applies the first animated frame; `OnReturnToPool` callback + `OnEffectComplete`. `Assets/_Scripts/Utility/Effects/PrismExplosion.cs`.
- **PrismImplosion** — pooled prism suction/grow VFX (shader `_State`/`_Location`); tracks a live `_convergenceTransform` so the sink follows moving fauna (`RefreshConvergence`), supports `StartImplosion`/`StartGrow`, and has a wall-clock `Update` watchdog that force-completes leaked instances. `Assets/_Scripts/Utility/Effects/PrismImplosion.cs`.
- **Impact** — coroutine-driven shader impact ripple that animates `_velocity`/`_Opacity`/position over `maxDistance` then destroys itself + its material (`_player`/`_red` variant selection). `Assets/_Scripts/Utility/Effects/Impact.cs`.
- **FadeIn** — coroutine that ramps a renderer material's `_opacity` from 0→1 (clones the material on start). `Assets/_Scripts/Utility/Effects/FadeIn.cs`.
- **FlickerHighScore** — coroutine toggling among four high-score `Image`s at randomized intervals for a flicker look. `Assets/_Scripts/Utility/Effects/FlickerHighScore.cs`.
- **FlickerUIEffect** — coroutine flickering a single `Image` on/off at randomized on/off intervals. `Assets/_Scripts/Utility/Effects/FlickerUIEffect.cs`.

### Interactive / SSU shader-driven components

Sprite-shader "SSU" components (2D wind/squish/parallax) plus their shared base — self-contained material-driven visual interactivity.

- **InstancerSSU** *(Internal/)* — `[DisallowMultipleComponent]` base holding the shared `runtimeMaterial` reference for SSU subtypes. `Assets/_Scripts/Utility/Internal/InstancerSSU.cs`.
- **InteractiveWindSSU** — `InstancerSSU` subclass bending a wind sprite in response to 2D trigger colliders entering its box (direction/target-bend math, stay-bent vs temporary, hyper-performance material swap, wiggle desync); includes a `DefaultCollider` helper. `Assets/_Scripts/Utility/Interactive/InteractiveWindSSU.cs`.
- **InteractiveSquishSSU** — squishes a sprite (`_SquishFade`) on 2D trigger enter/stay, lerping back out after a duration. `Assets/_Scripts/Utility/Interactive/InteractiveSquishSSU.cs`.
- **WindManagerSSU** — pushes global wind shader params (`WindTime`, `WindNoiseScale`, `WindMinIntensity`/`WindMaxIntensity`) via `Shader.SetGlobalFloat`, only when values change. `Assets/_Scripts/Utility/Interactive/WindManagerSSU.cs`.
- **WindParallaxSSU** — seeds a renderer material's `_WindXPosition` from the object's starting X for parallax. `Assets/_Scripts/Utility/Interactive/WindParallaxSSU.cs`.

### Data persistence

Low-level JSON save/load to disk and a serializable dictionary helper.

- **DataAccessor** — static JSON save/load/flush to `Application.persistentDataPath` (Newtonsoft, `FileShare.ReadWrite` for MPPM-safe concurrent access, self-healing delete on deserialize failure). `Assets/_Scripts/Utility/DataPersistence/DataAccessor.cs`.
- **SerializableDictionary\<TKey,TValue\>** — `Dictionary` subclass implementing `ISerializationCallbackReceiver` to round-trip via parallel key/value lists for JSON/inspector serialization. `Assets/_Scripts/Utility/DataPersistence/SerializableDictionary.cs`.

### Custom SOAP type — BenchmarkStateData

A custom SOAP triad (data struct + event + listener) following the project's SOAP-type convention, used by the performance-benchmark system.

- **BenchmarkStateData** — immutable `[Serializable]` snapshot struct (label, scene, git hash, progress, frames, avg/p99 frame times, report path) with value equality keyed on report path. `Assets/_Scripts/Utility/SOAP/ScriptableBenchmarkData/BenchmarkStateData.cs`.
- **ScriptableEventBenchmarkStateData** — `ScriptableEvent<BenchmarkStateData>` channel asset. `Assets/_Scripts/Utility/SOAP/ScriptableBenchmarkData/ScriptableEventBenchmarkStateData.cs`.
- **EventListenerBenchmarkStateData** — inspector-wirable `EventListenerGeneric<BenchmarkStateData>` with `UnityEvent` responses. `Assets/_Scripts/Utility/SOAP/ScriptableBenchmarkData/EventListenerBenchmarkStateData.cs`.

### Interactions & patterns

- **SOAP as the spine.** `GameDataSO`, `HostConnectionDataSO`, `FriendsDataSO`, `CellRuntimeDataSO`, `TournamentDataSO`, and `BenchmarkDataSO` are the shared-state hubs: producers write, consumers subscribe to `ScriptableEvent*` channels. `GameDataSO`'s event set drives the entire launch → spawn → turn/round → end-game → replay lifecycle; the single-writer discipline (`AuthenticationServiceFacade`, `HostConnectionService`, `FriendsServiceFacade`) applies to the party/friends containers.
- **NetworkVariable / server-authority.** These containers themselves hold no `NetworkVariable`s (they're `ScriptableObject`s); network authority is mirrored *into* them — turn monitors write `CrystalTargetCount`/`JoustTargetCount`/`GoalTargetCount`, controllers write `WinnerName`/`WinnerDomain`/`Results`/`ScoringRule` and the per-domain metric sums via ClientRpc/NetworkVariable callbacks. The Netcode utilities (`NetcodeHooks`, `Client*Transform/Animator`, `FixedPlayerName`, `NetworkGuid`, `ServerAdditiveSceneLoader`, `NetworkObjectSpawner`) are the low-level glue the multiplayer stack composes.
- **DI wiring.** `SceneNameListSO`, `GameDataSO`, `HostConnectionDataSO`, `FriendsDataSO` (and the benchmark/tournament SOs where used) are registered in `AppManager.InstallBindings()` and consumed via `[Inject]`.
- **Threading boundary.** `UniTaskExtensions.AsMainThread()` + `MainThreadDispatcher` is the enforced marshaling boundary for every UGS/Netcode `await` — the fix that keeps off-thread continuations from crashing SOAP raises and `UnityEngine.Object` access.
- **Pooling + Burst.** `GenericPoolManager<T>` is the pooling substrate for all prism VFX; the pooled `PrismExplosion`/`PrismImplosion` objects don't self-animate — they register with the centralized Burst-driven `PrismEffectsManager` and are animated in batches, honoring the continuity-of-existence (bloom-in/suction-out) law.
- **Ecology data flow.** `CellConfigDataSO` → `SpawnProfileSO` → `Flora/FaunaConfigurationSO` describe the population; `CellPhaseThresholds`/`CellPhaseRules` compute the volume-driven phase ladder; `FaunaReproductionRules` gates reproduction/seeding; `CellRuntimeDataSO` holds the live per-cell state and raises `OnPhaseChanged`. Volume (not count) is the spine, consistent with the locked ecosystem invariants.

---

## Utility — Performance Benchmark, Tools, Recording & Misc

This area is the game's **developer tooling and diagnostics layer** — everything used to measure, tune, debug, record, and report on Cosmic Shore rather than to play it. Its centerpiece is a self-contained **Performance Benchmark framework**: an allocation-free end-of-frame collector that captures per-frame FPS / CPU-GPU split / render / memory / netcode / gameplay-load samples, folds them into a JSON `BenchmarkReport` with computed statistics, a rule-based hint engine, an A–F grade and a 0-100 score, and a four-tab editor window (Runtime Capture / Sweep / History / Compare) plus in-build overlays. Around it sit a family of one-off benchmarks (AOE spatial-query, density-partition), the "Froglet Toolbox" editor console, and a scattering of recording, screenshot, email/bug-report, and deprecated staging utilities. Cross-system decoupling is via SOAP; the one gameplay reach-in (object counts) is isolated in a single sampler. All heavy/editor-only code is compiled out of release with `#if UNITY_EDITOR || DEVELOPMENT_BUILD` guards.

### Performance Benchmark — runtime capture core
The runtime collector samples once per `WaitForEndOfFrame` (so render/GPU numbers are settled), filling a pre-allocated flat `FrameSnapshot[]` in place; all formatting/scoring/report assembly happens once when capture ends. It reads Unity `ProfilerRecorder`/`ProfilerCounterValue`/`FrameTimingManager` stats and measures its own per-frame allocation as a zero-alloc self-check.

- **PerformanceBenchmarkRunner** — `MonoBehaviour` collector; warmup→sample→analyze state machine (`Idle/WarmingUp/Sampling/Done`), fixed-duration or free-form ("record until stopped") capture, spike detection, custom profiler counters, optional `BenchmarkDataSO` mirroring; exposes `IsRunning`/`Progress`/`LastReport`/`Spikes`/`AutoSave`. `Utility/PerformanceBenchmark/PerformanceBenchmarkRunner.cs`
- **BenchmarkConfigSO** — `ScriptableObject` config (menu `ScriptableObjects/Tools/Benchmark Config`): warmup/sample durations, per-category capture toggles (rendering/memory/physics/game-load/netcode), output folder + label. `Utility/PerformanceBenchmark/BenchmarkConfigSO.cs`
- **FrameSnapshot** — `[Serializable] struct`; one frame's data (frame time, fps, CPU/GPU ms, draw/batch/setpass/tri/vert counts, memory bytes, active rigidbodies, netcode ms/RPCs/NetVars/bytes, gameplay counts). `Utility/PerformanceBenchmark/FrameSnapshot.cs`
- **BenchmarkStatistics** — `[Serializable]` aggregate computed from snapshots: percentiles (p1/p5/p50/p95/p99), std-dev, CPU/GPU means, render/memory/physics/netcode/game-load averages+peaks, and a least-squares memory-leak slope (bytes/frame). Static `Compute()`. `Utility/PerformanceBenchmark/BenchmarkStatistics.cs`
- **BenchmarkReport** — `[Serializable]` full run: schema version, `SourceInfo` (Editor/DevBuild origin), git commit/branch, full environment/device capture, snapshots, statistics, spikes, analysis, and optional sweep `errors`/`marks`. JSON save/load; shells out to `git` for commit info. Includes `ReportOrigin` enum + `SourceInfo`/`SweepError`/`SweepMark` types. `Utility/PerformanceBenchmark/BenchmarkReport.cs`
- **BenchmarkAnalysis** — static scoring + rule-based hint engine; blends fps/p99/stability/GC into a 0-100 score, computes a bound verdict, evaluates `HintRule`s, and ships `DefaultRules()` grounded in the project's documented anti-patterns. Defines `HintSeverity`, `MarkerSample`, `SpikeEntry`, `BenchmarkHint`, `HintRuleType`, `HintRule`, `BenchmarkAnalysisResult`. `Utility/PerformanceBenchmark/BenchmarkAnalysis.cs`
- **BenchmarkHintRulesSO** — `ScriptableObject` (menu `ScriptableObjects/Tools/Benchmark Hint Rules`) holding a customizable `List<HintRule>`; `Resolve()` falls back to defaults, `Reset To Defaults` context menu. `Utility/PerformanceBenchmark/BenchmarkHintRulesSO.cs`
- **BenchmarkGrade** — static A–F health grade from statistics (avg fps / p99 / std-dev thresholds) with explanation string; shared by window and sweep. `Utility/PerformanceBenchmark/BenchmarkGrade.cs`
- **BenchmarkComparison** — static `BenchmarkComparer.Compare()` produces per-metric `MetricDelta`s (absolute/percent/verdict, higher-is-worse aware) plus improved/regressed/neutral tally and a boxed ASCII `FormatAsText()`; defines `MetricDelta` (+`Verdict` enum) and `ComparisonResult`. `Utility/PerformanceBenchmark/BenchmarkComparison.cs`
- **BenchmarkHistory** — static on-disk report index (`benchmark_index.json`): add/tag/query-by-scene/get-latest, rebuild-from-folder, remove, and a text `GetTrendSummary()`; nested `IndexEntry`. `Utility/PerformanceBenchmark/BenchmarkHistory.cs`
- **FrameBoundness** — static single-source CPU-vs-GPU classifier (`Classify`, `BusyCpuMs`, `TargetFpsCap`, `IsAtCap`) with `Unknown/CpuBound/GpuBound/Balanced` constants; shared by analysis + both overlays. `Utility/PerformanceBenchmark/FrameBoundness.cs`
- **GameLoadSampler** — static, allocation-free reader of gameplay object counts (`GameLoadMetrics` struct: prisms/explosions/implosions/vessels/players) from `PrismScaleManager`/`PrismEffectsManager`/`GameDataSO`; the one isolated reach-in from tool to gameplay. `Utility/PerformanceBenchmark/GameLoadSampler.cs`
- **NetMarkers** — static NGO profiler markers (`CSM.Net.Tick/Serialize/Deserialize/SpawnDespawn/RpcDispatch`) + per-frame counters (RPCs sent, NetVars dirty, bytes sent) with `CountRpc`/`CountNetVarDirty`/`AddBytesSent` helpers; placed in netcode hot paths, read by the runner. `Utility/PerformanceBenchmark/NetMarkers.cs`
- **BenchmarkBuildAutoRunner** — `MonoBehaviour` self-running dev-build benchmark gated on `-csmbench` launch arg (dev/editor only); reuses the runner, writes a DevBuild-origin report to `persistentDataPath/PerfRuns/`. `Utility/PerformanceBenchmark/BenchmarkBuildAutoRunner.cs`

### Performance Benchmark — sweep & long-capture tools
Batch and hands-free capture paths built on the same runner.

- **BenchmarkSweepRunner** — `DontDestroyOnLoad` `MonoBehaviour` that loads each scene in Build Settings, benchmarks it (or does a fast "errors-only" scan), tallies logged errors/exceptions/asserts per scene, and builds a combined graded summary; nested `SweepEntry`, static `StartSweep`/`Instance`. `Utility/PerformanceBenchmark/BenchmarkSweepRunner.cs`
- **ManualSweepSession** — `DontDestroyOnLoad` companion for the Sweep tab's "play and mark" mode: captures errors + F8 timestamped `SweepMark`s with a smoothed fps, folds them into a report via `FillReport()`; static `StartSession`/`Stop`. `Utility/PerformanceBenchmark/ManualSweepSession.cs`
- **ProfilerCsvLogger** — standalone `MonoBehaviour` that samples a fixed `ProfilerRecorder` set per frame to a CSV under `persistentDataPath/ProfilerCaptures/` plus an end-of-run summary `.txt` (worst-frame list, percentiles, configurable script-marker columns with `Category:Marker` syntax); static `StartCapture`/`StopCapture`, safety-flush, auto-start option. `Utility/PerformanceBenchmark/ProfilerCsvLogger.cs`
- **SpikeAnalyzer** — `#if UNITY_EDITOR` bridge to `ProfilerDriver`; walks a profiler frame's hierarchy to rank top self-time markers (`TryGetTopMarkers`), filtering editor/engine "noise" and flagging script samples; exposes `LastFrameIndex`/`FirstFrameIndex`/`SetProfilerEnabled`. Used to enrich spikes off the game thread. `Utility/PerformanceBenchmark/SpikeAnalyzer.cs`

### Performance Benchmark — live HUD overlays
Independent of a benchmark run; drop-in on-screen readouts.

- **BenchmarkHUDOverlay** — lightweight IMGUI overlay (F9 toggle) showing fps/frame-time (ring-buffered), CPU-GPU split + bound verdict, draw/setpass/tris/GC, memory, and optional game load; allocation-free hot path, throttled text rebuild. `Utility/PerformanceBenchmark/BenchmarkHUDOverlay.cs`
- **DiagnosticsHUD** — `#if UNITY_EDITOR || DEVELOPMENT_BUILD` auto-spawning uGUI overlay (F7 toggle / F6 advanced / F5 record); simple + advanced modes (CPU thread breakdown, render, memory vs device RAM, network RTT/NetVars/RPCs/bytes, OS region/UTC), and a "Run Diagnostic" that records a timed spike capture to `Documents/CosmicShore Diagnostics/` as JSON + readable `.txt`; nested `DiagSpike`/`DiagReport`. Builds its own Canvas/EventSystem. `Utility/PerformanceBenchmark/DiagnosticsHUD.cs`

### Performance Benchmark — editor window & automation
- **PerformanceBenchmarkWindow** — `EditorWindow` (`FrogletTools/Performance Benchmark`) with four tabs — **Runtime Capture** (Collect: configure + start-on-play, live results, hints, spike breakdown with editor-side off-thread enrichment, save to history), **Sweep** (manual play-session + automatic multi-scene sweep, error/mark lists), **History** (indexed runs, tag editing), **Compare** (baseline-vs-current delta table). Caches last runs to disk to survive the play-mode domain reload. `Utility/PerformanceBenchmark/Editor/PerformanceBenchmarkWindow.cs`
- **BenchmarkAutoStart** — `[InitializeOnLoad]` static bridge for "start capture on Play": stashes config/rules/game-data GUIDs in `SessionState` across the domain reload, enables profiler + frame-timing, optionally opens a chosen scene while suppressing the `SceneBootstrapper` Bootstrap redirect, then spawns/configures the runner on `EnteredPlayMode`. `Utility/PerformanceBenchmark/Editor/BenchmarkAutoStart.cs`
- **EditorUIStyles** — static shared pastel palette + lazy IMGUI factories (section headers, badges, stat rows, score bars, grade/severity/score color mappings) for the window's tabs. `Utility/PerformanceBenchmark/Editor/EditorUIStyles.cs`

### Performance Benchmark — SOAP data layer (referenced from adjacent folders)
The decoupling layer the runner writes to and consumers read; lives just outside the PerformanceBenchmark folder.

- **BenchmarkDataSO** — SOAP `ScriptableObject` container: lifecycle events (`OnBenchmarkStarted`/`OnSamplingStarted`/`OnBenchmarkCompleted`/`OnBenchmarkStopped`), `OnProgressUpdated`, and runtime-state fields (IsRunning/IsSampling/Progress/FramesCaptured/…). `Utility/DataContainers/BenchmarkDataSO.cs`
- **BenchmarkStateData** — immutable `[Serializable] struct` progress/result payload (label, scene, git hash, progress, avg fps/frame-time, p99, report path); equality by report path. `Utility/SOAP/ScriptableBenchmarkData/BenchmarkStateData.cs`
- **ScriptableEventBenchmarkStateData** / **EventListenerBenchmarkStateData** — the SOAP event channel + inspector listener for `BenchmarkStateData`. `Utility/SOAP/ScriptableBenchmarkData/`

### Performance Benchmark — edit-mode tests
NUnit edit-mode fixtures covering the runtime-safe (non-`MonoBehaviour`) pieces, all under `Utility/PerformanceBenchmark/Tests/Editor/`.

- **BenchmarkAnalysisTests, BenchmarkComparerTests, BenchmarkConfigSOTests, BenchmarkHistoryTests, BenchmarkReportTests, BenchmarkStatisticsTests, FrameBoundnessTests, MetricDeltaTests** — 8 fixtures verifying scoring/hints, comparison verdicts, config accessors, history index round-trips, report JSON, statistics/percentiles, bound classification, and metric-delta math.

### Tools — Froglet Toolbox & menu
The team's editor console and menu entry points.

- **FrogletTools** — `#if UNITY_EDITOR` `[InitializeOnLoad]` static menu host: legacy `FrogletTools/…` menu items (logging levels, scene shortcuts) and the AOE benchmark overlay/run entries; loads log prefs on load. `Utility/Tools/FrogletTools.cs`
- **LogControlWindow** — `EditorWindow` "Froglet Toolbox" (`FrogletTools/Toolbox`), a themed multi-tab console: **Scenes**, **Tools** (create/multiplayer-bootstrap toggle/utilities), **Logging** (`CSDebug` level toggles), **Density** (reflection-decoupled launcher for the density-partition runner + temporal sim, scene create/open, report copy), **Quest** (progression/intensity debug), **Vessels** (lock/unlock), **Crystals** (award/set balance, edit-mode pending), **UGS Data** (live cloud-data inspector). Persists log prefs; drives `PlayerDataService`/`GameModeProgressionService`/`UGSDataService`/`VesselUnlockSystem`. `Utility/Tools/LogControlWindow.cs`
- **AudioTester** — near-empty `MonoBehaviour` stub (grabs an `AudioSource`, empty click handler). `Utility/Tools/AudioTester.cs`

### Tools — AOE spatial-query benchmark
Isolates the Burst `PrismSpatialIndex` explosion path (synthetic prisms, no colliders/GameObjects).

- **AOEBenchmarkRunner** — `#if DEVELOPMENT_BUILD || UNITY_EDITOR` `MonoBehaviour`; registers N synthetic prisms, runs simulated growing-radius explosion frames through `ProcessExplosionFrame`, times them with `Stopwatch`, and prints a boxed report (avg/min/max/total ms, hits, speedup); nested `BenchmarkResult`. Auto-runs on `Start`. `Utility/Tools/AOEBenchmarkRunner.cs`
- **AOEBenchmarkOverlay** — `#if DEVELOPMENT_BUILD || UNITY_EDITOR` draggable IMGUI overlay (F9); reads `AOE.*`/`Prism.*` profiler markers via `ProfilerRecorder`, shows last-frame + rolling averages, and A/B-toggles `ExplosionImpactor.ForceLegacyPhysics` (Physics vs Burst) live; static `ToggleOverlay`. `Utility/Tools/AOEBenchmarkOverlay.cs`

### Tools — Density Partition Benchmark
Edit-mode harness grading density-search algorithms against a deterministic ground truth, plus a temporal ecology sim (namespace `CosmicShore.Utility.Tools.DensityPartitionBenchmark`). See `Docs/DENSITY_PARTITIONING_AUDIT.md`.

- **DensityPartitionBenchmarkScenario** — synthetic input definitions: `BenchmarkPrism` struct, `ScenarioKind`/`ScenarioShape` enums, and `[Serializable] BenchmarkScenario` (deterministic seeded `Build()` into a prism list; distribution shapes incl. domain-segregated clusters + a staleness-bug reproduction). `Utility/Tools/DensityPartitionBenchmark/DensityPartitionBenchmarkScenario.cs`
- **DensityPartitionBenchmarkAlgorithms** — the candidate search matrix as one parameterized `Search` (grid size, mass-weight, box smoothing, sub-voxel parabolic interp, mean-shift) plus a 64³ ground-truth scan; `BenchmarkResult`/`SearchOptions` structs. Variants: GridArgmax17 / GridSmoothed17 / GridSmoothedInterp17 / GridMassSmoothedInterp17 / GridMassSmoothedInterp32. `Utility/Tools/DensityPartitionBenchmark/DensityPartitionBenchmarkAlgorithms.cs`
- **DensityPartitionBenchmarkRunner** — `[ExecuteAlways] MonoBehaviour` driver; runs every scenario×algorithm, caches ground truth, renders/dumps the report to `lastReport`, auto-resets stale serialized scenarios on config-version bump; `RunAllAndDump()`/`ResetToDefaults()`. `Utility/Tools/DensityPartitionBenchmark/DensityPartitionBenchmarkRunner.cs`
- **DensityPartitionBenchmarkReport** — static diff-friendly text report builder (`QueryRow` per anti-Jade/Ruby/Gold/all query: distance error, mass-found %, median/worst ms), peaked-vs-diffuse median buckets. `Utility/Tools/DensityPartitionBenchmark/DensityPartitionBenchmarkReport.cs`
- **DensityPartitionTemporalSimRunner** — `[ExecuteAlways] MonoBehaviour` edit-mode ecology simulator running the flora-growth / fauna-consumption / cell-phase loop over time to compare grid strategies (shipped ±500m box vs cell-sized vs ProductionV2) for bounded outer-shell mass; also an ecology tuning bench; `RunComparison()`. `Utility/Tools/DensityPartitionBenchmark/DensityPartitionTemporalSimRunner.cs`
- **DensityPartitionBenchmarkRunnerEditor** / **DensityPartitionTemporalSimRunnerEditor** — `#if UNITY_EDITOR` custom inspectors adding Run/Reset/Copy-to-clipboard buttons + help boxes. `Utility/Tools/DensityPartitionBenchmark/Editor/`

### Recording Studio helpers
Editor-only Timeline transform-recording plus a free-cam.

- **DataHolder** — `#if UNITY_EDITOR` `MonoBehaviour` config container (in `AnimationRecorderData.cs`) for the recorder: `PlayableDirector`, `TimelineAsset`, assets path, tracked `Animator[]`, recording delay, name salt. `Utility/Recording/AnimationRecorderData.cs`
- **AnimationRecorder** — `#if UNITY_EDITOR` `[InitializeOnLoad] EditorWindow` that samples tracked objects' transforms and saves them as animation clips into a Timeline (serialized-property field-name constants for the `DataHolder`). `Utility/Recording/AnimationRecorderWindow.cs`
- **FancyCamController** — `MonoBehaviour` free-fly / target-orbit camera for capture: keyboard translation, mouse/gamepad rotation, orbit + roll around a target with trigger/scroll zoom. `Utility/Recording/FancyCamController.cs`
- **DefaultTimeline.playable** — default Timeline asset used by the recorder (non-code). `Utility/Recording/DefaultTimeline.playable`

### Screenshots
- **CaptureScreenShot** — `MonoBehaviour` wrapper over `ScreenCapture`: editor `C`-key supersized screenshot, plus `CaptureScreenShotToDisk`/`…AsTexture`/`…IntoRenderTexture` API. `Utility/ScreenShots/CaptureScreenShot.cs`

### Reporting & Email
Player-facing bug report → native email share.

- **SendBugReport** — `MonoBehaviour` bug-report popup controller; gates the send button on filled subject/contents, builds a `ShareByEmail` to the Zendesk support address. `Utility/Reporting/SendBugReport.cs`
- **ShareByEmail** — plain C# helper wrapping `NativeShare` for email (recipient/subject/text, optional attachment, result callback). `Utility/Email/ShareByEmail.cs`
- **EmailValidator** — static regex email-format validator. `Utility/Email/EmailValidator.cs`

### ChoppingBlock — deprecated staging
Isolated dead/reference code kept out of the way.

- **AndroidIAPExample** — legacy PlayFab + Unity Purchasing `IDetailedStoreListener` sample (login, catalog search, grant/purchase flow); reference/example, not wired in. `Utility/ChoppingBlock/AndroidIAPExample.cs`
- **TestHarnessOctreeDensitySearch** — `MonoBehaviour` test harness that prints per-team `Cell.GetExplosionTarget` density-search results for the "TestHarnessOctree" scene of one pumpkin per color. `Utility/ChoppingBlock/TestHarnessOctreeDensitySearch.cs`

### MainThreadDispatcher (Utility root)
- **MainThreadDispatcher** — static reliable Unity-main-thread marshaller: captures Unity's `SynchronizationContext` + thread id at `BeforeSceneLoad`, exposes `IsOnMainThread` and an awaitable `SwitchToMainThreadAsync()` (`Post` onto the captured context). The load-bearing primitive behind `.AsMainThread()` — deliberately used because UniTask's own switch primitives are unreliable on this version (see `Docs/THREADING.md`). `Utility/MainThreadDispatcher.cs`

### Interactions & patterns
- **SOAP over singletons.** The benchmark runner mirrors lifecycle/progress into `BenchmarkDataSO` via `ScriptableEventBenchmarkStateData`/`ScriptableEventNoParam` so overlays/CI can react without direct references; all consumers poll the runner or listen to events. Editor tools instead read live services (`PlayerDataService`, `GameModeProgressionService`, `UGSDataService`, `VesselUnlockSystem`).
- **Unity Profiling as the data source.** `ProfilerRecorder`, `ProfilerCounterValue`, `ProfilerMarker`, and `FrameTimingManager` feed every collector; `NetMarkers` are placed in the gameplay/netcode hot paths and read here, and `SpikeAnalyzer` walks `ProfilerDriver` frames editor-side. `FrameBoundness` + `BenchmarkGrade` are the single sources of truth shared between analysis, overlays, and sweep.
- **Isolated gameplay coupling.** `GameLoadSampler` is the one deliberate reach-in — it reads counts from `PrismScaleManager`/`PrismEffectsManager`/`GameDataSO`; the AOE and density tools go deeper (`PrismSpatialIndex` Burst path, `Cell.countGrids`/`GetExplosionTarget`) but stay behind synthetic inputs so they run outside the production lifecycle (density benchmark is edit-mode-safe via `[ExecuteAlways]` + reflection decoupling in the Toolbox).
- **Domain-reload resilience.** Editor automation stashes asset GUIDs in `SessionState` and caches last runs to `persistentDataPath` JSON so results survive entering/leaving Play Mode; reports carry git commit/branch + device/source info for cross-commit `BenchmarkComparer` comparisons, indexed by `BenchmarkHistory`.
- **Release-stripping & threading.** Overlays, dev auto-runner, AOE tools, and all `Editor/` code compile out of release builds via `#if UNITY_EDITOR || DEVELOPMENT_BUILD`; `MainThreadDispatcher` (root) underpins the whole codebase's `.AsMainThread()` UGS/Netcode continuation contract. Bug-report/email flows out through `NativeShare`; `AndroidIAPExample` and the octree harness are quarantined in `ChoppingBlock` as deprecated/reference code.

---

## Editor Tools, Tests & Legacy Code

This area collects everything that supports development rather than shipping runtime: Unity **editor tooling** (custom inspectors, property drawers, `EditorWindow` panels, one-click setup/validation tools, shader GUIs, the dialogue authoring window, and play-mode/scene bootstrap infrastructure), the **edit-mode unit-test suite** (31 NUnit fixtures that lock enum values, data-struct layouts, SOAP containers, pure ecology/tournament rules, and party plumbing), a small amount of **still-live code parked in the vestigial `Game/` tree** (the capsule-membrane renderer + its baked preset SO, and the source-generated input actions), and **asset-only folders** (`SSUScripts`, `DialogueSystem`, `Integrations`). Almost all editor code lives in the `CosmicShore.Editor` namespace and compiles into Unity's implicit editor assembly; only `CosmicShore.PlayFabTests` carries its own `.asmdef`. Two menu roots are used: `Tools/Cosmic Shore/*` (first-party) and `FrogletTools/Legacy/*` (older/experimental utilities). Many of the SSU-prefixed editors are the editor half of the third-party "Sprite Shaders Ultimate" package, whose runtime components live under `CosmicShore.Utility`.

### Editor — one-click setup & authoring tools
Static classes that author SO assets / wire scene objects / mutate prefabs in bulk from a `[MenuItem]`, so complex wiring isn't done by hand. Most are idempotent and print a summary dialog.

- **ElementalPetalBarWirer** — `Tools/Cosmic Shore/Wire Elemental Petal Bars`; assigns the shared `ElementalBarsConfigSO` (`Resources/ElementalBarsConfig`) to a selected `ElementalBarsView`, creates the 4 element bars and a square `*_Flower` `petalRoot` container per element. `Editor/ElementalPetalBarWirer.cs`
- **ToyboxSetupTool** — `Tools/Cosmic Shore/Setup Freestyle Toybox`; authors the four toy-definition SOs (`PaintingToyDefinitionSO`, `VesselChangerToyDefinitionSO`, `DomainChangerToyDefinitionSO`, `ConveyorToyDefinitionSO`) under `_SO_Assets/Toys/`, creates/loads `ToyboxSO` at `Resources/Toybox.asset`, registers the toys, and adds a `ToyboxController` to Menu_Main (on the `MenuCrystalClickHandler` host). Auto-fills unset content refs (prism/crystal/effect prefabs). `Editor/ToyboxSetupTool.cs`
- **EndConditionOverridesWindow** — `Tools/Cosmic Shore/End Game Conditions`; the single editor for how each domain mode ends (HexRace/Crystal-Capture crystal counts, Joust count, Maelstrom win target). Reads/writes the one `EndConditionOverridesSO` at `Resources/EndConditionOverrides.asset` (auto-created), with a "Live vs Build baseline" split and a "Set Build Values" snapshot. `Editor/EndConditionOverridesWindow.cs`
- **EndConditionBuildRestore** — `IPreprocessBuildWithReport` (callbackOrder 0); before a build, if `autoRestoreBuildValuesBeforeBuild` is on, copies the Build baseline onto the Live end-game counts so a test config never ships. `Editor/EndConditionBuildRestore.cs`
- **LifeFormCrystalValidator** — `Tools/Cosmic Shore/Validate Lifeform Crystals`; author-time enforcement of the "every lifeform drops exactly one elemental crystal" invariant — scans all prefabs carrying `LifeForm`/`LightFauna` and warns on missing / non-elemental / multiple `Crystal`s. `Editor/LifeFormCrystalValidator.cs`
- **ProfileAvatarBinder** — `Tools/Cosmic Shore/Profile/Bind Selected Image To Local Avatar`; adds a `ProfileImage` (and an `Image` if needed) to selected GameObjects so a UI slot live-binds the local player's avatar. Has a validate handler gating on selection. `Editor/ProfileAvatarBinder.cs`
- **CreateBenchmarkSceneTool** — `Tools/Cosmic Shore/Create Benchmark Scene`; clones `MinigameWildlifeBlitz.unity` → `BenchmarkStressTest.unity`, registers it in Build Settings, and prints a manual finish-wiring checklist (endless controller, Squirrel vessels, high-density spawn profile, benchmark HUD). `Editor/CreateBenchmarkSceneTool.cs`
- **StripCrystalAudioSourceTool** — `Tools/Cosmic Shore/Strip Crystal AudioSources`; one-shot migration that removes dead `AudioSource`s from every `Crystal` prefab under `_Prefabs/Environment/` (audio now goes through `AudioSystem.PlayGameplaySFX`). `Editor/StripCrystalAudioSourceTool.cs`
- **ForceReserializeScriptableObjects** — `FrogletTools/Legacy/Force Re-Serialize All ScriptableObjects`; dirties + saves every `t:ScriptableObject` to normalize on-disk serialization. `Editor/ForceReserializeScriptableObjects.cs`

### Editor — custom inspectors & property drawers
`[CustomEditor]` / `[CustomPropertyDrawer]` types that augment specific components with bake buttons, test harnesses, or conditional field display.

- **CapsuleMembraneEditor** — inspector for `CapsuleMembrane` (in the vestigial `Game/` tree); adds a "Bake Animation Preset" button that precomputes the membrane wobble into a `CapsuleMembraneAnimationSO`, plus a status box flagging a missing / stale / up-to-date bake via `BakeSignature.Matches`. `Editor/CapsuleMembraneEditor.cs`
- **ForcefieldCrackleControllerEditor** — inspector for `ForcefieldCrackleController`; edit-mode test harness that injects impacts (per-axis direction popup, "Add Test Impact", "Add 6 Impacts", "Clear All") via `AddImpact`/`ClearAllImpacts` to preview the skimmer forcefield arcs without playing. `Editor/ForcefieldCrackleControllerEditor.cs`
- **ElementalFloatDrawer** — `[CustomPropertyDrawer(typeof(ElementalFloat))]`; shows Min/Max/Element only when `Enabled` is true, with dynamic `GetPropertyHeight`. `Editor/ElementalFloatDrawer.cs`
- **ElementalFloatEditor** — `FrogletTools/Legacy/ElementalFloat Editor` window; reflection-walks the selected GameObject hierarchy to surface and edit every `ElementalFloat` field across all components. `Editor/ElementalFloatEditor.cs`
- **VesselEditor** — fully commented-out legacy `ShipEditor` `[CustomEditor(typeof(Vessel))]` skeleton (dead file kept for reference; drove field visibility off `ShowIfAttribute`/`crystalImpactEffects`). `Editor/VesselEditor.cs`
- **SSU component inspectors** — validation-focused inspectors for the "Sprite Shaders Ultimate" runtime helpers, each drawing help boxes and red/green requirement checks: **ImageSSUEditor** (`ImageSSU`, requires RectTransform + Image + UI_Graphic shader space), **MaterialInstancerSSUEditor** (`MaterialInstancerSSU`, requires Renderer/Graphic), **SpriteSheetSSUEditor** (`SpriteSheetSSU`, requires Image/SpriteRenderer), **UnscaledTimeSSUEditor** (`UnscaledTimeSSU`), **ShaderFaderSSUEditor** (`ShaderFaderSSU`; largest — filters serialized fields by `getChildObjects`/`automaticFading`, "Copy From" harvests float/vector/color shader props into `FloatFaderSSU`/`VectorFaderSSU`, live preview slider). `Editor/ImageSSUEditor.cs`, `MaterialInstancerSSUEditor.cs`, `SpriteSheetSSUEditor.cs`, `UnscaledTimeSSUEditor.cs`, `ShaderFaderSSUEditor.cs`
- **Interactive wind inspectors** — **InteractiveWindSSUEditor** (`InteractiveWindSSU`; large collapsible Setup/Troubleshooting/Information hint sections, auto-fixes layer=2 and adds a trigger `BoxCollider2D`, hyper-performance-mode fields), **WindManagerSSUEditor** (`WindManagerSSU`; base inspector + "one active WindManager" info box), **WindParallaxSSUEditor** (`WindParallaxSSU`; info box only). `Editor/Interactive/Editor/InteractiveWindSSUEditor.cs`, `WindManagerSSUEditor.cs`, `WindParallaxSSUEditor.cs`

### Editor — shader GUIs & SSU support
Custom `ShaderGUI` implementations and their backing hint data, all belonging to the "Sprite Shaders Ultimate" package.

- **SSUShaderGUI** — the large (~2,000-line) master `ShaderGUI` for SSU shaders: shader-space switching, per-property enable/hide/toggle logic, category lines, rainbow box colors, performance/texture-sample readout, and status warnings; loads a `Dictionary<string,ShaderHintSSU>` of hint assets to annotate properties. `Editor/SSUShaderGUI.cs`
- **ShaderHintSSU** — `ScriptableObject` (menu `ScriptableObjects/Shader/SSU Shader Hint (ignore this)`) holding a shader description, per-property `HintText` list, space hint, requirement flags (full-rect mesh, sprite-sheet fix, instancing, tiling), and perf metadata; consumed by `SSUShaderGUI`. Backing assets: **65 `.asset` hint files** under `SSUScripts/Editor/Resources/SSU/Hints/` (Add Color, Hologram, Dissolve variants, Recolor RGB/Palette/RGBYCP, UV Distort/Rotate/Scale/Scroll, Wind, etc.). `Editor/ShaderHintSSU.cs`
- **CodingHelper** — utility `EditorWindow` (`ShowUtility`) opened from a shader-property label; generates copy-to-clipboard C# snippets (`SetColor`/`SetVector`/`SetTexture`/`SetFloat`) and a SpriteRenderer-vs-UI-Image example for driving that property at runtime. `Editor/CodingHelper.cs`
- **ShapeFxPackUI** — standalone `ShaderGUI` for the third-party "Shapes FX Pack" material (MatCap switch, diffuse/outline/displacement/panner/target-mode sections). `Editor/ShapeFxPackUI.cs`

### Editor — dialogue authoring
- **DialogueEditorWindow** — `FrogletTools/Legacy/Dialogue Editor`; 3-panel `EditorWindow` (guarded by `#if !LINUX_BUILD`) that lists `DialogueSet` assets under `DialogueSystem/SO`, edits set id/mode, a `ReorderableList` of `DialogueLine`s (or a `RewardData` form for Reward mode), portrait pickers, per-set colors persisted in `EditorPrefs`, and a live preview panel. `Editor/DialogueEditorWindow.cs`
- **DialogueLineDrawer** — despite its name, an empty `MonoBehaviour` stub (Start/Update only), not a drawer. `Editor/DialogueLineDrawer.cs`
- **DialogueSetEditorView** — fully commented-out static helper (dead code) that once drew a set's line list + button bar. `Editor/DialogueSetEditorView.cs`
- **DialogueSystem/ (asset-only, in scope)** — no C# lives here; the folder holds authoring assets: `DialogueSet_01/02`, `DIalogue1/2`, `Library.asset`, `New_Dialogues/{ActivationDL, ftue_intro_01}`, the `DialogueUIPrefab` + Animator controller + pop-in/out anims, and the `SpriteAnimation` / `UI_NoiseDissolve` shader graphs. (Runtime dialogue code lives under `System/Runtime`, outside this scope.)

### Editor — play-mode & scene bootstrap infrastructure
`[InitializeOnLoad]` singletons that hook `EditorApplication` events to make play-mode behave for a networked, SO-heavy project.

- **SceneBootstrapper** — forces the Bootstrap scene (build index 0) to load when entering Play Mode (needed so `NetworkManager`/`NetworkObject` `GlobalObjectIdHash`es exist in the AssetDatabase); caches/restores the previous scene on exit; suppresses false "dirty" flags caused by framework `OnValidate` noise (Cinemachine/URP/Netcode) across domain reloads and play-exit. Exposes the `LoadBootstrapSceneOnPlay` toggle (menu items under `FrogletTools/Legacy/TestingMultiplayer/*`) that the benchmark tool flips. `Editor/SceneBootstrapper.cs`
- **PlayModeSOProtector** — snapshots every `.asset` under `_SO_Assets/` into `SessionState` on `ExitingEditMode` and writes the original bytes back (deferred via `delayCall`, then force-reimports) on `EnteredEditMode`, so play-mode mutations to SOAP variables and plain SOs (`GameDataSO`, `CellRuntimeDataSO`, …) never persist to disk. `Editor/PlayModeSOProtector.cs`

### Editor — diagnostics, asset-finding & misc utilities
`EditorWindow`/menu tools for inspecting a scene or project.

- **FindAssetByGUID** — `FrogletTools/Legacy/Find Asset by GUID`; three modes: find asset by GUID, find GameObject by file ID (current or all build scenes), find sub-asset by GUID+file ID; plus helpers to read the GUID/file-ID of the current selection. `Editor/FindAssetByGUID.cs`
- **SceneObjectCounter** — `Tools/Scene Object Counter`; tallies loaded objects by type name (excludes persistent assets). `Editor/SceneObjectCounter.cs`
- **TextureMemoryUseWindow** (`TextureMemoryUsageWindow`) — `Tools/Texture Memory Usage`; lists all loaded `Texture2D`s by estimated bytes (format→bpp). `Editor/TextureMemoryUseWindow.cs`
- **RuntimeTextureMemoryUsageWindow** — `Tools/Runtime Texture Memory Usage`; same estimate but scoped to textures actually referenced by scene renderers, UI `Image`/`RawImage`, terrains, lightmaps, and the skybox. `Editor/RuntimeTextureMemoryUsageWindow.cs`
- **ProfilerCsvLoggerMenu** — `Tools/Cosmic Shore/Profiler CSV Logger/{Start Capture, Stop Capture & Save, Open Capture Folder}`; editor wrapper around the runtime `ProfilerCsvLogger` for per-frame CSV capture during Play Mode. `Editor/ProfilerCsvLoggerMenu.cs`
- **ComponentCopierWindow** — `FrogletTools/Legacy/Component Copier`; copies selected components (minus Transform) from one GameObject to another via `ComponentUtility`, pasting values into existing or adding new. `Editor/CopyTool/ComponentCopierWindow.cs`
- **TriangleWindowMeshGenerator** — `Tools/Triangle Window Mesh Generator`; builds an inward-facing procedural cube mesh at a chosen size. `Editor/TriangleWindowMeshGenerator.cs`
- **PlayFabProductGenerator** — `EditorWindow` for generating PlayFab Economy catalog items (captains/upgrades) from `SO_Vessel`/`SO_Captain`; **entirely commented-out / disabled** (menu item and all logic behind block comments, secret key from env var). `Editor/PlayfabProductGenerator.cs`

### Edit-mode tests (`Tests/EditMode/` — 31 fixtures)
NUnit `[TestFixture]`s in namespace `CosmicShore.Tests` (~450+ tests per `UNIT_TESTING_GUIDE.md`). No `.asmdef` sits in this folder — the fixtures are picked up by the project's predefined editor test assembly (the `CosmicShore.Bootstrap.Tests` / `Multiplayer.Tests` / `Tests.EditMode` names in CLAUDE.md have **no `.asmdef` on disk**; the only physical test assembly nearby is `CosmicShore.PlayFabTests.asmdef`, outside this folder). Reference: `Tests/UNIT_TESTING_GUIDE.md`.

- **Enum serialization-drift guards** (lock integer↔name so reordering can't silently corrupt every serialized field):
  - **EnumIntegrityTests** — `VesselClassType`, `Domains`, `GameModes`, `Element`, `ShipActions`, `ResourceType`. `Tests/EditMode/EnumIntegrityTests.cs`
  - **EnumIntegrityExtendedTests** — `CaptainLevel` (→ PlayFab IAP IDs), `CSLogLevel`, `InputEvents`, impact-effect enums, `UserActionType`, `CallToActionTargetType`, etc. `EnumIntegrityExtendedTests.cs`
  - **EcologyEnumIntegrityTests** — `FaunaDiet`, `CellPhase`, `CellAggressionLevel` (serialized on fauna/cell prefabs). `EcologyEnumIntegrityTests.cs`
- **Data structs & SOAP/data containers** (field-order, equality, event-firing, reset contracts):
  - **ResourceCollectionTests** (`ResourceCollection` Mass/Charge/Space/Time) · **XpDataTests** (`XpData`) · **ShipModifierTests** (`ShipThrottleModifier`/`ShipVelocityModifier`) · **TrainingGameProgressTests** (`TrainingGameProgress` tiers 1-4) · **PartyPlayerDataTests** (equality by PlayerId) · **PartyInviteDataTests** (invite payload) · **GenericDataSOTests** (`IntDataSO`/`StringDataSO` `OnValueChanged`) · **RuntimeCollectionSOTests** (`RuntimeCollectionSO<T>` ItemAdded/Removed) · **CameraSettingsSOTests** (per-vessel defaults) · **HostConnectionDataSOTests** (`HasOpenSlots`, `ResetRuntimeData` host flags) · **GameDataSOTests** (largest data fixture — reset, sorting, domain stats, volume, turns, winner calc) · **IRoundStatsCleanupTests** (all 30+ stats zeroed on `Cleanup()`) · **DisposableGroupTests** (composite dispose). Files under `Tests/EditMode/`.
- **Utility / math / logging**: **GeometryUtilsTests** (distance/clamp) · **GameObjectExtensionTests** (`GetOrAdd`, `OrNull`, child mgmt, `TryGetInterface`, `IsLayer`) · **CSDebugTests** (log-level preset↔flag mapping). 
- **Pure ecology / spatial rules** (pin the emergent invariants without a scene):
  - **CellPhaseRulesTests** — `CellPhaseRules.Compute` volume-hysteresis phase ladder (Calm→Restless→Frenzy). `CellPhaseRulesTests.cs`
  - **FaunaReproductionRulesTests** — `FaunaReproductionRules` (feeds→offspring, floor reseed) = Lotka–Volterra coupling. `FaunaReproductionRulesTests.cs`
  - **MicroscenePatternsTests** — `MicroscenePatterns.Plan` for the Wanderway conveyor: budget-exactness (mass conservation), determinism (instance-local `System.Random` only), crystal/lifeform/collider-budget bounds. `MicroscenePatternsTests.cs`
  - **SkimmerAdjustElementLevelByCrystalEffectTests** — crystal scale→element-level mapping + elemental gating. `SkimmerAdjustElementLevelByCrystalEffectTests.cs`
  - **PrismSpatialIndexTests** — `PrismSpatialIndex` query views (`QuerySphere`, `IsAnyPrismWithin`), lifecycle (`MarkDestroyed`/`MarkRestored`/`Unregister`/`UpdatePosition`), reservations (`TryReserve`/`IsPositionOccupied`/`ReleaseReservation`). `PrismSpatialIndexTests.cs`
- **Party / menu / multiplayer**: **PartyInviteControllerTests** (precondition guards, no live Netcode) · **PartyInviteSystemTests** (largest file, ~1,500 lines — full parse→SOAP→HashSet-dedup→slot lifecycle) · **MenuFreestyleToggleTests** (`MenuCrystalClickHandler` ownership guard + `IsMultiplayerSession`, `MainMenuController` freestyle SOAP wiring). 
- **Menu / tournament state machines**: **MainMenuStateTests** (`MainMenuState` enum stability + `MainMenuController` transition table, Ready↔Freestyle bidirectional) · **TournamentDataSOTests** (per-domain {2,1,0} placement fold determinism) · **TournamentStandingsFormatterTests** ("(You)" owner-row tagging, `Domains.Blue` tags nothing) · **TournamentStateMachineTests** (race-to-6 route to `Summary` valid from `InGame` and `Lobby`). 

### Vestigial `Game/` directory
CLAUDE.md describes `Game/` as non-code, but it still carries three live C# files plus data assets. In scope as "Game (vestigial)".

- **CapsuleMembrane** — `MonoBehaviour` (namespace `CosmicShore.Game`) that renders an icospheric arrangement of capsules as the cell membrane in one instanced `Graphics.RenderMeshInstanced` call; runtime interpolates a baked wobble preset (falls back to per-frame Perlin/quaternion/matrix math + warns if none). Public: `Radius`, `AnimationPreset`, `CurrentSignature`, `BakeAnimationInto(...)`. `Game/Environment/CapsuleMembrane.cs`
- **CapsuleMembraneAnimationSO** — `ScriptableObject` (menu `ScriptableObjects/Environment/Capsule Membrane Animation`) storing baked per-capsule layout + looped rotations and a `BakeSignature` fingerprint (`Matches`) for staleness detection; written by `SetBakedData`, produced by `CapsuleMembraneEditor`. `Game/Environment/CapsuleMembraneAnimationSO.cs`
- **@InputActionsAsset** — source-generated (Unity Input System 1.14.2) partial class implementing `IInputActionCollection2` from `InputActionsAsset.inputactions`; action maps **Flight** (`Rotate`, `Use`) and **Touch** (`PrimaryContact`, `PrimaryPosition`, `SecondaryPostion`), control schemes PC/Mobile. Auto-generated, no manual edits. `Game/IO/_Input Mapping/InputActionsAsset.cs`
- **Non-code assets** (kept in place, referenced elsewhere): `Environment/FloraAndFauna/BoidSimulation.compute`, flow-field data `FlowField/{OvalFlowData, PolarFlowData}.asset`, warp-field data `WarpField/{NewWarpData, TardisWarpData}.asset`, jet material/shader under `Vessel/Animation/`, `Vessel/TrailPassives/ScoutTrailPrismConfig.asset`, and the `Prisms/PRISM_PERFORMANCE_AUDIT.md` reference doc.

### Asset-only / empty scoped folders
- **SSUScripts/** — no C#; only the 65 `ShaderHintSSU` assets under `Editor/Resources/SSU/Hints/` (loaded by `SSUShaderGUI`). The SSU runtime component classes live in `CosmicShore.Utility`; their editors are the `*SSUEditor` files above.
- **Integrations/** — effectively empty: only `Playfab/` and `Playfab/PlayFabTests/` folder markers (`.meta` only). The actual PlayFab tests + `CosmicShore.PlayFabTests.asmdef` live under `System/Playfab/PlayFabTests/` (out of this scope's folders).

### Interactions & patterns
- **Menu conventions.** Two menu roots — `Tools/Cosmic Shore/*` for current first-party tooling (petal wirer, toybox setup, end-conditions, lifeform validator, profiler CSV, benchmark scene, avatar binder, crystal audio strip) and `FrogletTools/Legacy/*` for older utilities (dialogue/elemental-float editors, GUID finder, component copier, reserialize, bootstrap-on-play toggles). The `/EndGameConditions`, `/ecology`, and Toybox skills reference these tools directly.
- **SO-driven design mirrored in tooling.** Setup tools author or wire the exact SO assets the runtime reads — `ElementalBarsConfigSO`, `ToyboxSO`/`ToyDefinitionSO`, `EndConditionOverridesSO`, `CapsuleMembraneAnimationSO` — keeping "config lives in ScriptableObjects" true even for editor-generated content. `EndConditionBuildRestore` is the build-time enforcement half of that config.
- **Play-mode hygiene for a networked, SOAP-heavy project.** `SceneBootstrapper` guarantees Bootstrap loads first so `NetworkObject` hashes are cached, and `PlayModeSOProtector` prevents play-mode SO mutations (SOAP variables + `GameDataSO`) from dirtying `_SO_Assets/`; both hook `EditorApplication.playModeStateChanged` and lean on `SessionState`/`EditorPrefs` to survive domain reloads.
- **Tests as the guardrails around the fundamentals.** The edit-mode suite pins the load-bearing contracts the rest of the game composes on: enum integer values (serialization), data-struct field order (PlayFab JSON, HashSet keys), SOAP change-events (`OnValueChanged`, ItemAdded/Removed), `IRoundStats.Cleanup()` (score bleed), and the pure ecology/tournament/spatial rules (`CellPhaseRules`, `FaunaReproductionRules`, `MicroscenePatterns`, `PrismSpatialIndex`, `TournamentDataSO`) — deliberately pure functions so they need no scene or NetworkManager. Party/Netcode integration is explicitly deferred to play-mode (`PartyInviteControllerTests` only checks preconditions).
- **Third-party editor surfaces.** The SSU (`SSUShaderGUI` + `ShaderHintSSU` assets + `*SSUEditor` inspectors + `CodingHelper`) and Shapes-FX (`ShapeFxPackUI`) tooling are vendored shader-authoring UIs whose runtime counterparts sit in `CosmicShore.Utility`; they interact with the game only through materials/components, not SOAP or Netcode.
