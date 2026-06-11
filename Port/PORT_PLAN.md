# Cosmic Shore Port — Master Plan & Live Status

> **This file is the persistent state of the autonomous porting loop.** Each loop
> iteration: (1) read **NEXT UP**, (2) implement + test the next chunk, (3) update the
> status tables and NEXT UP, (4) commit + push to `claude/quirky-cannon-sk8a02`.
> The goal, verbatim from the prompter: *"remake the entire game … wholly ours without
> any dependency on unity or anything else that prevents you from developing in your
> own loop without a human's involvement. We want everything replicated. Loose nothing."*

## Ground rules

1. **Lose nothing.** Every behavior, tuned value, enum ID, event contract, and system
   in the Unity project gets a counterpart here. When a Unity feature can't be ported
   verbatim, record the deviation in the **Deviations log** below.
2. **Headless first.** The simulation must build, run, and verify with `dotnet test`
   alone. Rendering/audio/input are pluggable backends added later; they may never
   become load-bearing for game logic.
3. **Verbatim porting.** Same namespaces, file names, member names. Only the
   using-directive substitutions in README.md are allowed without justification.
4. **All architecture rules from /CLAUDE.md carry over**: SOAP for cross-system
   communication, single-writer pattern, fail-loud (no null guards on event channels),
   config in data assets not code, conserved mass / emergent-systems design philosophy,
   frozen enum numeric values.
5. **Tests accompany every port.** Enum values get freeze tests; logic gets behavior
   tests. The Unity project's existing edit-mode tests get ported alongside their
   subjects.
6. **Always shippable progress builds** (prompter requirement, 2026-06-10): the branch
   must always offer a one-command way for a human to try the current state — see
   "Progress builds — testing protocol" below. Growing the testable surface is part of
   every phase, not an afterthought.

## Progress builds — testing protocol

The prompter tests progress without prompting the loop. Contract:

1. **Green branch invariant.** Every push to `claude/quirky-cannon-sk8a02` has
   `cd Port && dotnet build && dotnet test` green. The branch is always safe to pull.
2. **Runnable harness: `CosmicShore.Cli`** (`src/CosmicShore.Cli`, lands iteration 2).
   One command to exercise the current port on any machine with the .NET 10 SDK:
   `cd Port && dotnet run --project src/CosmicShore.Cli`. It grows with the port:
   - now → engine smoke: boot loop, SOAP wiring, state machine walk, version banner
   - phase 2 → scripted simulation: AI vessels, prisms, crystals, cells; deterministic
     via `--seed`; emits a readable match transcript + final stats
   - phase 3 → full game-mode rounds headless (`--mode hexrace --players 4 --seed 42`)
   - phase 5 → `--render` flag for the interactive window; headless stays the default
3. **Visual artifacts before the renderer is interactive.** From the first render-capable
   milestone, headless render-to-PNG (and short MP4) smoke outputs are committed under
   `Port/artifacts/` (small, curated) and sent into the chat at milestones so progress
   is visible with zero local setup.
4. **Milestone log + notification.** Each numbered milestone (M1 first CLI sim, M2 first
   full game-mode round, M3 first picture, M4 first interactive build, …) is recorded in
   the milestone log below with its commit hash, and the prompter gets a push
   notification with the exact command (or file) to try. (Annotated `port-mN` tags are
   created locally, but this environment's git proxy only accepts branch pushes — the
   log + commit message are the durable record.)
5. **Local prerequisites for the prompter** (one-time):
   `winget install Microsoft.DotNet.SDK.10` (Windows) / `brew install dotnet-sdk` (macOS),
   then `git fetch origin claude/quirky-cannon-sk8a02 && git checkout claude/quirky-cannon-sk8a02`.

### Milestone log

| Tag | What became testable | Command | Status |
|---|---|---|---|
| `port-m1` | CLI engine smoke + deterministic sim (loop, lifecycle, tasks, DI, SOAP, RoundStats) | `cd Port && dotnet run --project src/CosmicShore.Cli` | ✅ 2026-06-11 (commit `25b5582d`) |
| `port-m2` | First full headless game-mode round (AI vs AI) | `… -- --mode <mode> --seed <n>` | ⬜ |
| `port-m3` | First rendered frame (PNG artifact in chat + repo) | pull + open artifact | ⬜ |
| `port-m4` | First interactive desktop build | `… -- --render` | ⬜ |

## Toolchain (re-verify each fresh container)

- .NET 10 SDK at `/opt/dotnet` (installed via `dotnet-install.sh --channel LTS`;
  `export PATH=/opt/dotnet:$PATH`, persisted in `~/.bashrc`). nuget.org reachable.
  crates.io blocked; npm available (unused).
- Build: `cd Port && dotnet build && dotnet test` — must be green before every commit.

## Phase roadmap

| Phase | Scope | Status |
|---|---|---|
| **0 — Foundation** | Toolchain, solution, engine math/SOAP/attrs/net-primitives, Data layer, test harness | ✅ **DONE** (iteration 1) |
| **1 — Engine core** | First-party async (UniTask replacement ✅), DI container (Reflex replacement ✅), update loop + scheduler ✅, logging ✅, GameObject/Transform/MonoBehaviour/Scene model ✅ (see `docs/ENGINE_CORE.md`); asset registry (.asset → JSON) deferred to content phase | ✅ **DONE** (iteration 2) — remaining: asset registry, prefab factories, multi-scene |
| **2 — Simulation core** | ResourceSystem, VesselStatus + transformer/controller, Prism/Trail/TrailFollower, impact-effects matrix (impactors × effect SOs), crystals, cells (CellPhase/aggression), flora/fauna ecosystem (conserved mass!), elementals | ⬜ |
| **3 — Game modes** | MiniGameControllerBase hierarchy (template method: rounds→turns→countdown→gameplay→end), turn monitors, scoring (incl. golf rules), AI pilot/gunner, all 36 GameModes' controllers (priority: Freestyle, CellularDuel, WildlifeBlitz, HexRace, Joust, CrystalCapture) | ⬜ |
| **4 — Networking** | First-party transport + replication: NetworkVariable wire sync, RPC equivalent, server-authoritative session flow, host/client lifecycle, replacing Unity Netcode + UGS Relay/Sessions/Lobby with self-hosted session server | ⬜ |
| **5 — Presentation** | Renderer backend (evaluate: custom GL via first-party bindings vs software-rendered headless screenshots first), camera system (CameraSettingsSO port), input strategies (Keyboard/Gamepad/Touch via IInputStrategy), HUD/UI framework, VFX/shader ports (HLSL sources exist in repo), audio backend (Wwise replacement) | ⬜ |
| **6 — Services** | Replace UGS/PlayFab/Firebase: auth, cloud save, leaderboards, friends/presence, parties/invites, analytics — self-hosted service + local-first fallback | ⬜ |
| **7 — Content pipeline** | Extract all `_SO_Assets/**/*.asset` (Unity YAML) → JSON for the asset registry; scene descriptions → first-party scene format; models/textures/audio export | ⬜ |
| **8 — Integration** | Full playable loop: boot → menu → game mode → end-game → menu, multiplayer session E2E, performance passes | ⬜ |

## Status — detailed inventory

### Phase 0 (✅ done, iteration 1 — 2026-06-10)

| Item | Port location | Tests |
|---|---|---|
| Solution + projects (net10.0) | `Port/CosmicShore.slnx`, `src/`, `tests/` | `dotnet test` green: 259/259 |
| Math: Vector2/3/4, Quaternion (YXZ Euler convention verified), Mathf (incl. SmoothDamp), Color | `src/CosmicShore.Engine/Math/` | `MathTests.cs` |
| Time (frame clock, harness-driven `Advance`) | `src/CosmicShore.Engine/Time.cs` | — |
| Attributes: SerializeField/Header/Tooltip/Range/Min/TextArea/CreateAssetMenu | `src/CosmicShore.Engine/Attributes.cs` | — |
| ScriptableObject base + CreateInstance | `src/CosmicShore.Engine/ScriptableObject.cs` | — |
| SOAP: ScriptableVariable<T> (+8 concrete), ScriptableEvent<T>/NoParam (+6 concrete), ScriptableList<T> | `src/CosmicShore.Engine/Soap/` | `SoapTests.cs` |
| Networking primitives: NetworkBehaviour (Spawn/Despawn lifecycle), NetworkVariable<T> (perm-aware, change callbacks), FixedString64Bytes (61-byte UTF-8 cap) | `src/CosmicShore.Engine/Networking/`, `Collections/` | `NetworkingTests.cs` |
| **Data layer: all 26 enum files + 5 struct files ported verbatim** (incl. RoundStats NetworkBehaviour, IRoundStats with default-method Cleanup, DomainStats) | `src/CosmicShore.Data/` | `EnumFreezeTests.cs` (full numeric freeze), `RoundStatsTests.cs` (both lifecycle modes) |

### Unity-project inventory still to port (tracked at directory granularity; refine as phases open)

| Source (under `Assets/`) | Files | Target phase | Status |
|---|---|---|---|
| `_Scripts/Data/` | 31 | 0 | ✅ done |
| `_Scripts/Utility/` (ClassExtensions, DataContainers incl. GameDataSO, PoolsAndBuffers, Effects, DataPersistence) | ~80 | 1–2 | ⬜ |
| `_Scripts/ScriptableObjects/` (SO_* defs + 16 SOAP subdirs + VesselPrefabContainer) | ~70 | 1–2 | ⬜ |
| `_Scripts/Controller/Vessel/` (VesselStatus, Prism, Trail, actions, ResourceSystem) | ~150 | 2 | ⬜ |
| `_Scripts/Controller/ImpactEffects/` (11 impactors, 20+ effect SO types) | ~60 | 2 | ⬜ |
| `_Scripts/Controller/Environment/` (cells, crystals, flora/fauna, flow/warp fields, spawners) | ~100 | 2 | ⬜ |
| `_Scripts/Controller/Managers/` (PrismScaleManager, MaterialStateManager, …) | ~15 | 2/5 | ⬜ |
| `_Scripts/Controller/Projectiles/` | ~30 | 2 | ⬜ |
| `_Scripts/Controller/Arcade/` (controllers, scoring, turn monitors) | ~70 | 3 | ⬜ |
| `_Scripts/Controller/AI/` (AIPilot, AIGunner) | ~10 | 3 | ⬜ |
| `_Scripts/Controller/Player/` + `Multiplayer/` + `Party/` | ~50 | 3–4 | ⬜ |
| `_Scripts/Controller/IO/` (input strategies) | ~10 | 5 | ⬜ |
| `_Scripts/Controller/Camera/`, `Animation/`, `FX/`, `Assemblers/`, `Prisms/`, `ECS/`, `XP/`, `Settings/` | ~40 | 2/5 | ⬜ |
| `_Scripts/System/` (Bootstrap, AppManager, state machine, scene loader, auth/friends facades, audio, instrumentation, dialogue runtime, rewind, quests, XP, ads, IAP…) | ~126 | 1/3/6 | ⬜ |
| `_Scripts/UI/` (HUD controllers/views, screens, modals, toast, event feed, elements) | ~188 | 5 | ⬜ |
| `FTUE/` (tutorial system, 25 files) | 25 | 5 | ⬜ |
| `_Scripts/DialogueSystem/` + `System/Runtime/` dialogue | ~30 | 3/5 | ⬜ |
| `_Scripts/Editor/` (tooling — re-imagine as CLI tools where still needed) | ~40 | 7 | ⬜ |
| `_Scripts/Tests/` (port existing edit-mode tests alongside subjects) | ~25 | rolling | ⬜ |
| `_SO_Assets/` (48+ dirs of .asset instances → JSON) | ~hundreds | 7 | ⬜ |
| Shaders/HLSL (`Assets/Materials/Graphs/`, prism/skybox/crackle shaders) | ~dozens | 5 | ⬜ |
| Scenes (`_Scenes/` — 3 core + 3 SP + 7 MP + tools) | 16+ | 7 | ⬜ |
| Prefabs (`_Prefabs/` — vessels, pools, trails, UI) | ~hundreds | 7 | ⬜ |

### Third-party dependency replacement map

| Unity-era dependency | Replacement strategy | Phase | Status |
|---|---|---|---|
| Unity engine core (GameObject/MonoBehaviour/scenes) | ✅ First-party component/scene model in `CosmicShore.Engine` (`docs/ENGINE_CORE.md`); Instantiate/prefabs + multi-scene deferred to content phase; physics deferred to phase 2 | 1 | ✅ core |
| UniTask | ✅ `CosmicShore.Engine.Tasks` (`GameTask.*`, structural main-thread affinity, synchronous cancellation parity) — `.AsMainThread()` retired | 1 | ✅ |
| Obvious.Soap | ✅ `CosmicShore.Engine.Soap` | 0 | ✅ |
| Reflex DI | ✅ `CosmicShore.Engine.Injection.Container` (RegisterValue/RegisterFactory-lazy, `[Inject]`, child scopes, InjectGameObject) | 1 | ✅ |
| Unity Netcode for GameObjects | First-party replication over UDP/TCP (NetworkVariable sync + RPC); API contract already established in `Engine.Networking` | 4 | ⬜ |
| UGS (Auth, Relay, Sessions/Lobby, Friends, CloudSave, Leaderboards, Analytics) | Self-hosted session/identity service (single small server, JSON protocol) + local-first offline mode | 6 | ⬜ |
| PlayFab (economy, catalog) | Same self-hosted service | 6 | ⬜ |
| Firebase (analytics) | First-party event log → pluggable sink | 6 | ⬜ |
| Wwise | First-party audio mixer over a low-level output lib (decide at phase 5; candidate: miniaudio P/Invoke, vendored) | 5 | ⬜ |
| DOTween | First-party tween library on engine update loop | 1–2 | ⬜ |
| Cinemachine | First-party camera rig (follow/zoom per CameraSettingsSO) | 5 | ⬜ |
| Unity Input System | IInputStrategy pattern already platform-agnostic; first-party backends | 5 | ⬜ |
| NiceVibrations | Haptics abstraction; platform backends when mobile target lands | 5 | ⬜ |
| Unity Jobs/Burst | System.Threading + SIMD (System.Numerics); profile before optimizing | 2+ | ⬜ |
| URP/ShaderGraph/VFXGraph | First-party renderer + ported HLSL (sources already in repo) | 5 | ⬜ |

## Deviations log

| # | Deviation | Rationale |
|---|---|---|
| 1 | `Mathf.Round` uses banker's rounding (MidpointRounding.ToEven) — same as Unity. `Vector3.SmoothDamp` is component-wise (Unity clamps the change vector once); identical for maxSpeed=∞ usage. | Documented at the source; revisit if a ported system clamps SmoothDamp speed. |
| 2 | `NetworkVariable<T>` change callback uses `EqualityComparer<T>.Default` dedup; Unity Netcode dedups on serialized-value equality. Behaviorally identical for the value types used. | — |
| 3 | SOAP `EventListener*` components are ported and functional in code (`Engine.Soap.EventListenerGeneric` + `Engine.Events.UnityEvent`); inspector wiring arrives with the scene-asset pipeline (phase 7). The `UnityEvent` type NAME is kept for verbatim porting — it is first-party code in `CosmicShore.Engine.Events`. | — |
| 4 | **Upstream latent bug fixed in port**: `new TrainingGameProgress()` zero-initialized (null `Progress`, intensity 0) — C# 9 couldn't express the intended parameterless init, and the 16 TrainingGameProgressTests documenting the contract were silently red upstream (no CI). Port adds a real parameterless ctor chaining to the dummy-arg one. Worth fixing upstream. | Test-documented contract wins. |
| 5 | **Upstream stale test fixed in port**: `GameModes_HasExpectedMemberCount` expected 34; the enum has 35 members (MultiplayerCrystalCapture added without updating). Updated to 35. | — |
| 6 | **Upstream latent red test reframed**: `ImpactEffects_AllValuesAreUnique` contradicts the shipped enum, which intentionally merges legacy effect groups sharing values 1-8, 10. Values are wire format — port freezes the exact duplicate set instead so NEW collisions still fail. | Enum values untouchable. |
| 7 | 10 SOAP files deferred pending gameplay types: ScriptableEventVesselImpactor / ExplosionDebuffApplied / SkimmerDebuffApplied (IVessel/IVesselStatus/VesselImpactor), ScriptableSilhouetteData/* (SilhouetteController), ScriptableVesselHUDData/* (MiniGameHUD), VesselPrefabContainer (IVesselStatus). MainMenuStateTests (MainMenuController) and GameObjectExtensionTests (physics types) likewise port with their subjects. | Tracked in NEXT UP. |
| 8 | Stat structs (CellStats/CrystalStats/PrismStats/AbilityStats), PrismType, and audio category enums are extracted into their own port files (source noted in headers) because their host classes (StatsManager, PrismFactory, AudioSystem) port in later phases. | File-split only; content verbatim. |

## Iteration log

- **Iteration 1** (2026-06-10): Phase 0 — toolchain, solution, engine math/SOAP/attrs/
  net primitives, full Data layer, 259 tests.
- **Iteration 2** (2026-06-11): Phase 1 — component/scene model (GameObject, Transform,
  MonoBehaviour with reflective lifecycle discovery, fake-null Object contract),
  GameLoop (fixed-step accumulator, phase ordering), `Engine.Tasks` (GameTask awaitables,
  synchronous-cancellation parity, GameSynchronizationContext), `Engine.Injection`
  Container, Debug/ColorUtility/LayerMask, `CosmicShore.Game` project with first ported
  Utility files (CSDebug, DebugExtensions, GameObjectExtension, TransformExtensions with
  GameTask), ported IRoundStatsCleanupTests, `docs/ENGINE_CORE.md`, **CosmicShore.Cli
  milestone M1** (tag `port-m1`). 322 tests.

- **Iteration 3** (2026-06-11): custom SOAP layer + pure-logic substrate — engine
  additions (UnityEvent/UnityEvent<T> in `Engine.Events`, EventListenerGeneric/
  EventResponse in `Engine.Soap`, AddComponentMenu/FormerlySerializedAs/Preserve
  attributes); 48/58 SOAP custom-type files ported verbatim (10 deferred on unported
  gameplay types — see Deviations); struct/enum extractions (StatsManager stat structs,
  PrismType, MenuAudioCategory/GameplaySFXCategory); Utility ports (DisposableGroup,
  GeometryUtils, SceneNameListSO, CellPhaseThresholds + CellPhaseRules); **new
  `tests/CosmicShore.Tests.Ported` project (NUnit 3, mirrors Unity Test Framework) with
  12 original EditMode test files ported verbatim** — which immediately caught three
  latent upstream issues (fixed in port, see Deviations #4-6); CLI section [4] (SOAP
  channels + cell phase hysteresis). 528 tests green (322 xunit + 206 NUnit).

## NEXT UP (iteration 4)

Goal: enter phase 2 — resource/elemental simulation core so VesselStatus/ResourceSystem can port in
iteration 4.

1. Port the remaining pure DataContainer subjects whose EditMode tests exist:
   GenericDataSO, RuntimeCollectionSO, CameraSettingsSO, XpData, HostConnectionDataSO
   (defer if it drags UGS types) + their tests verbatim into Tests.Ported.
2. Port the resource/elemental core from `Controller/Vessel/`: ResourceSystem (+
   Resource, element-level model `GetLevel` math) and whatever small SO configs it
   needs (SO_Element …). Port SkimmerAdjustElementLevelByCrystalEffectTests if its
   subject closure is tractable.
3. Map the IVessel/IVesselStatus interface closure; port the interfaces once the
   referenced types exist, then un-defer the 10 SOAP files from iteration 3
   (VesselImpactor/debuff events, SilhouetteData, VesselHUDData, VesselPrefabContainer)
   and MainMenuStateTests/GameObjectExtensionTests when their subjects land.
4. Extend the CLI with a resource/elemental demo (crystal pickup → element level walk).
5. Update this file (status tables, iteration log, NEXT UP), commit, push.

## Loop protocol (every iteration)

1. `export PATH=/opt/dotnet:$PATH` (reinstall SDK via dotnet-install.sh if container is fresh).
2. `git checkout claude/quirky-cannon-sk8a02 && git pull origin claude/quirky-cannon-sk8a02`.
3. Read **NEXT UP**. Implement. `dotnet build && dotnet test` green.
4. Update status tables, Deviations log, and **NEXT UP** for the following iteration.
5. Commit (`feat(port): …` per GIT_RULES.md) and push.
6. Re-arm the wakeup (~25 min) with the original /loop prompt.
