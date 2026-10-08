# Project Structure

> Moved verbatim from the root `CLAUDE.md`, which indexes every topic file. Paths in this file are relative to the repository root.

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
│   │   ├── IO/                # Input strategies (Keyboard, Gamepad, Touch, Mouse)
│   │   ├── Animation/         # Per-vessel animation controllers
│   │   ├── Camera/            # CustomCameraController, CameraSettingsSO, ICameraController
│   │   ├── Multiplayer/       # Netcode: ServerPlayerVesselInitializer (+ WithAI, Menu variants), ClientPlayerVesselInitializer, MultiplayerSetup, MenuCrystalClickHandler, NetworkStatsManager
│   │   ├── Player/            # Player (NetworkBehaviour), PlayerSpawner, IPlayer, PlayerSpawnerAdapterBase, MiniGamePlayerSpawnerAdapter
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
│   │   ├── Economy/           # CatalogManager, CaptainManager, Inventory, StoreShelve, VirtualItem, ItemPrice
│   │   ├── PlayerData/        # PlayerProfile, PlayerSession
│   │   ├── Instrumentation/   # AnalyticsServiceFacade (UGS Analytics, single writer)
│   │   ├── Runtime/           # Dialogue runtime (DialogueManager, models, views, helpers)
│   │   ├── RewindSystem/      # Rewind/replay functionality
│   │   ├── Audio/             # AudioSystem (FMOD events + legacy music AudioSources)
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
│   └── SSUScripts/            # Specialized subsystem scripts
├── _SO_Assets/                # ScriptableObject asset instances (48+ subdirectories)
├── _Prefabs/                  # CORE, Cameras, Characters, Environment, Pools, Projectile, Spaceships, Trails, UI Elements
├── _Scenes/                   # Game scenes organized by type
├── _Graphics/, _Models/, _Audio/, _Animations/
├── FTUE/                      # First-Time User Experience / Tutorial system
├── Plugins/                   # Obvious.Soap, Demigiant (DOTween), NativeShare, etc.
└── NiceVibrations/            # Haptic feedback
```

Three root folders were deleted on 12 Sep 2026 and should not come back: `Wwise/` (an empty
middleware fossil), `Parse/` (two importer-disabled DLLs nothing referenced) and
`SerializeInterface/`. The last was **not** dead — it was an unattributable code drop with no vendor,
licence or namespace, so it was **rewritten first-party** rather than removed: `[RequireInterface]`
now lives at `_Scripts/Utility/RequireInterfaceAttribute.cs` (runtime) plus
`_Scripts/Editor/RequireInterfaceDrawer.cs` (the drawer — under `Editor/`, so it cannot reach a
player). **The drawer is the load-bearing half**: a `[RequireInterface]` field with no drawer
degrades *silently* into an object field that accepts anything, which compiles, looks correct in the
inspector, and throws on the cast at runtime. `Docs/THIRD_PARTY_REGISTER.md` §2, §6.

Note: `_Scripts/Game/` is **not vestigial — do not delete it.** This line previously said it held "only non-code assets" and that all C# had been reorganised out of it; measured, it holds **3 `.cs` files, two of them live** (`Environment/CapsuleMembrane.cs` → `CapsuleMembrane.prefab`, `Environment/CapsuleMembraneAnimationSO.cs` → `CapsuleMembraneAnimation.asset`; `IO/_Input Mapping/InputActionsAsset.cs` is the generated wrapper and has no serialized referrer) **plus two assets wired into shipped vessels** — `Vessel/Animation/JetMaterial.mat` → `Rhino.prefab` and `Vessel/TrailPassives/ScoutTrailPrismConfig.asset` → `Manta.prefab`. It also still holds the compute shaders, input action mappings and `PRISM_PERFORMANCE_AUDIT.md`. *"Vestigial" in a folder description invites exactly the delete a reference check would have prevented* — `Docs/LAUNCH_BLOCKER_INDEX.md` §C6.

### Assembly Definitions

Most first-party code still compiles in Unity's default assembly, `Assembly-CSharp`. **That is a
state being actively unwound, not the design.** The monolith means every one-line edit recompiles
~1,481 files, and nothing enforces a dependency direction. The split proceeds bottom-up, one leaf
assembly at a time — full plan, measurement protocol and phase-2 candidates:
**`Docs/ASSEMBLY_SPLIT.md`**.

| Assembly | Scope |
|---|---|
| `CosmicShore.Data` | `_Scripts/Data/` — enums, structs, small interfaces. The first extracted leaf: depends on no first-party code |

**The rule that makes extraction safe, and safe in only one direction:** a predefined assembly
(`Assembly-CSharp`, `Assembly-CSharp-Editor`) **automatically references every auto-referenced
asmdef**, so moving code OUT into an asmdef is invisible to everything left behind — no `using`
change, no reference wiring, no big-bang. An asmdef can never reference a predefined assembly, so
code that still reaches back into gameplay cannot be extracted at all. Extraction therefore works
**bottom-up from the leaves**, and the compiler is the forcing function rather than a review
checklist.

Two things that do **not** survive an assembly boundary and must be checked before drawing one:
`internal` members and `partial` types (invisible / illegal across assemblies), and extension
methods (only found when their namespace is `using`-ed). A namespace *may* span assemblies — so
relocating a file that blocks a boundary is a `git mv` of the file **and** its `.meta`, never a
rename. Renaming is what breaks scene and prefab references; changing which assembly a class
compiles into does not.

> An earlier version of this table also listed `CosmicShore.Bootstrap.Tests`,
> `CosmicShore.Multiplayer.Tests` and `CosmicShore.Tests.EditMode`. **Those assemblies never
> existed.** The tests therefore fell into `Assembly-CSharp` and shipped into the player, where the
> IL2CPP linker hit their NUnit attributes and killed the Windows build (`error IL1005` → `Failed
> to resolve assembly: 'nunit.framework'`). Fixed by moving every test under an `Editor/` folder;
> see below.

### **Tests live under an `Editor/` folder, not in an asmdef — until their dependencies are extracted.**

Every first-party test is under a folder literally named `Editor`, which puts it in
`Assembly-CSharp-Editor`:

| Suite | Location |
|---|---|
| General edit-mode tests | `_Scripts/Tests/Editor/` |
| Bootstrap tests | `_Scripts/System/Bootstrap/Tests/Editor/` |
| Multiplayer tests | `_Scripts/Controller/Multiplayer/Tests/Editor/` |

Two properties make this work, and both are load-bearing:

1. `Assembly-CSharp-Editor` is **never included in a player build**, so NUnit never reaches the
   IL2CPP linker.
2. It **implicitly references `Assembly-CSharp`** — and every auto-referenced asmdef — so tests see
   both the gameplay types still in the monolith and every extracted assembly.

**Do not author a test `.asmdef` for a suite that touches gameplay types.** An asmdef cannot
reference `Assembly-CSharp`, so such a test would be blind to the very types it tests. That
constraint is almost certainly why the three documented assemblies above were never created.

**The constraint is a function of where the code under test lives, not a permanent law.** A suite
whose dependencies are *entirely* inside extracted assemblies (`CosmicShore.Data` today) can have a
real test asmdef referencing those plus the test-runner assemblies. `CosmicShore.PlayFabTests` used
to be the worked example of that shape; it was deleted with PlayFab, so there is currently no suite
in the project with its own runtime-facing asmdef. Take that per-suite as dependencies come out;
never as a project-wide flip.

**A new test file must be created under an `Editor/` folder** unless it meets the bar above. A test
anywhere else compiles into the player and breaks the Windows build at the linker stage, which the
compile tier and the edit-mode suite are both structurally blind to; only a player build catches it.

**Adding a new runtime assembly** is not a casual change — it alters the build for every branch in
flight. Follow the checklist in `Docs/ASSEMBLY_SPLIT.md` § "Adding an asmdef": prove the folder is a
leaf, check the three things that don't cross a boundary, `autoReferenced: true`, one asmdef per
commit, and run `validate_project.py` + `check_conditional_compilation.py`.

Third-party assemblies: `Obvious.Soap`, `Lofelt.NiceVibrations`, `NativeShare.Runtime`

### Scene Inventory

See `Docs/SCENES.md` for the full scene and game mode reference. Summary below.

#### Core Application Scenes

| Scene | Build Order | Purpose |
|---|---|---|
| **Bootstrap** | 0 (must be first) | App entry: DI registration, platform config, auth start, splash |
| **Authentication** | 1 | Auth UI, cached session check, NetworkManager host start |
| **Menu_Main** | 2 | Main menu with networked autopilot vessel, screen navigation |

#### Single-Player Game Scenes

**None ship.** `MinigameDuelForTheCell` and `MinigameWildlifeBlitz` were retired in 2026-09 — they had
been replaced by `MinigameDuelForCellMultiplayer_Gameplay` (`OnlineDuelForTheCell (29)`) and
`MinigameWildlifeBlitzMultuplayerCoOp` (`CoOpWildlifeBlitz (32)`), and every ability in them was
dead (their non-networked Player fails `IsLocalUser`). The `GameModes` members 8 and 26 are KEPT
(ids are never reused; cloud progress keys on the names). The single-player Wildlife Blitz stack
(`SinglePlayerWildlifeBlitzController` and friends, `PlayerSpawner`/`VesselSpawner`) survives only
because `BenchmarkStressTest.unity` — which Settings ▸ Run Benchmark launches — was cloned from it;
note that scene is NOT in Build Settings. The `ArcadeGameWildlifeBlitz` card survives only because
the (dead) hangar training entries for Rhino and Sparrow point at it — it is in no game list.

#### Multiplayer Game Scenes

| Scene | Game Mode | Controller |
|---|---|---|
| `MinigameSkimRace` | `SkimRace (33)` | `SkimRaceController` |
| `MinigameFreestyleMultiplayer_Gameplay` | `MultiplayerFreestyle (28)` | `MultiplayerFreestyleController` |
| `MinigameScurryMultiplayer_Gameplay` | `Scurry (35)` | `ScurryController` |
| `MinigameDuelForCellMultiplayer_Gameplay` | `OnlineDuelForTheCell (29)` | `OnlineDuelForTheCellController` |
| `MinigameJoust_Gameplay` | `Joust (34)` | `JoustController` |
| `MinigameWildlifeBlitzMultuplayerCoOp` | `CoOpWildlifeBlitz (32)` | `CoOpWildlifeBlitzMiniGame` |
| `MinigameAstroLeague` | `AstroLeague (36)` | `AstroLeagueController` |
| `MinigameBroodRush` | `BroodRush (38)` | `BroodRushController` |
| `MinigameRampage` | `Rampage (2)` | `RampageController` |
| `MinigameCleave` | `Cleave (39)` | `CleaveController` |
| `MinigameWildlifeLiberation` | `WildlifeLiberation (40)` | `WildlifeLiberationController` |
| `MinigameDogFight` | `DogFight (41)` | `DogFightController` |
| `MinigameBends` | `Bends (42)` | `BendsController` |
| `MinigameScarabScramble` | `ScarabScramble (43)` | `ScarabScrambleController` |
| `MinigameSalvo` | `Salvo (44)` | `SalvoController` |
| `MinigameSwitchback` | `Switchback (45)` | `SwitchbackController` |
| `MinigameTollway` | `Tollway (48)` | `TollwayController` |
| `MinigameHeadlong` | `Headlong (49)` | `HeadlongController` |
| `MinigameBreakwater` | `Breakwater (50)` | `BreakwaterController` |
| `MinigameSkein` | `Skein (51)` | `SkeinController` |
| `MinigameBloomrush` | `Bloomrush (52)` | `BloomrushController` |
| `MinigameRedline` | `Redline (53)` | `RedlineController` |
| `MinigameGrizzlyCharge` | `GrizzlyCharge (62)` | `DogFightController` |
| `MinigameGrizzlyTime` | `GrizzlyTime (63)` | `GrizzlyTimeController` |
| `ArcadeGameMultiplayer2v2CoOpVsAI` | `Multiplayer2v2CoOpVsAI (30)` | Domain games variant |

All in `Assets/_Scenes/Multiplayer Scenes/`.

#### Tool & Test Scenes

`Recording Studio`, `MattsRecording Studio`, `PhotoBooth` (in `_Scenes/Tools/`), `AudioTestSandbox` (in `_Scenes/Game_TestDesign/`).
