# Branch archive: `claude/android-build-no-unity-gh65ai`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

<!-- SUMMARY -->

- **Last commit:** 2026-07-07 by Claude
- **Unmerged commits:** 111
- **Forked from:** `c18af4922` (2026-07-06, Add SwordFish_A Model)
- **Tip:** `594acbfa5`
- **Files touched (639):**
  - `.gitignore`
  - `Port/.gitattributes`
  - `Port/.gitignore`
  - `Port/CosmicShore.slnx`
  - `Port/Directory.Build.props`
  - `Port/PORT_PLAN.md`
  - `Port/README.md`
  - `Port/artifacts/skimrace_s1.png`
  - `Port/artifacts/skimrace_s2_rival.png`
  - `Port/artifacts/skimrace_s4_bloom.png`
  - `Port/artifacts/skimrace_s5_circuit_trails.png`
  - `Port/artifacts/skimrace_s6_field_elements.png`
  - `Port/artifacts/skimrace_s6_photo_finish.png`
  - `Port/artifacts/skimrace_s7_hull_fixed.png`
  - `Port/artifacts/skimrace_squirrel.png`
  - `Port/dist/CosmicShore-Android.apk`
  - `Port/dist/CosmicShore-Windows.zip`
  - `Port/dist/README.txt`
  - `Port/dist/SkimRace-Windows.zip`
  - `Port/docs/DRIFT_2026-06-13.txt`
  - `Port/docs/DRIFT_2026-07-07.txt`
  - `Port/docs/ENGINE_CORE.md`
  - `Port/docs/VESSEL_CONCRETE.md`
  - `Port/docs/VESSEL_LAYER.md`
  - `Port/play-latest.bat`
  - `Port/run-source.bat`
  - `Port/setup.sh`
  - `Port/src/CosmicShore.Cli/AstroLeagueRound.cs`
  - `Port/src/CosmicShore.Cli/CosmicShore.Cli.csproj`
  - `Port/src/CosmicShore.Cli/CrystalCaptureRound.cs`
  - `Port/src/CosmicShore.Cli/HexRaceRound.cs`
  - `Port/src/CosmicShore.Cli/JoustRound.cs`
  - `Port/src/CosmicShore.Cli/Program.cs`
  - `Port/src/CosmicShore.Cli/TournamentRound.cs`
  - `Port/src/CosmicShore.Client.Android/CosmicShore.Client.Android.csproj`
  - `Port/src/CosmicShore.Client.Android/MainActivity.cs`
  - `Port/src/CosmicShore.Client.Android/SdlTouchBackend.cs`
  - `Port/src/CosmicShore.Client/Assets/squirrel.mesh`
  - `Port/src/CosmicShore.Client/AudioEngine.cs`
  - `Port/src/CosmicShore.Client/CosmicShore.Client.csproj`
  - … and 599 more

### `2a9ce66c8` — feat(port): bootstrap standalone Unity-free port — engine foundation + Data layer

_Claude, 2026-06-10 23:34:11 +0000_

```text
Phase 0 of the full Cosmic Shore port to a wholly first-party stack
(C# / .NET 10 LTS, headless-first, no Unity dependency):

- CosmicShore.Engine: math (Vector2/3/4, Quaternion with verified YXZ
  Euler convention, Mathf incl. SmoothDamp, Color), Time frame clock,
  serialization attributes, ScriptableObject base, SOAP layer
  (ScriptableVariable/Event/List replacing Obvious.Soap), networking
  primitives (NetworkBehaviour lifecycle, NetworkVariable with change
  callbacks, FixedString64Bytes) preserving the Unity-era API contracts
- CosmicShore.Data: all 26 enum files + 5 struct files ported verbatim,
  including RoundStats (NetworkBehaviour) in both lifecycle modes
- Tests: 259 xunit tests green — full enum numeric-value freeze suite,
  math behavior parity, SOAP semantics, networking primitives, RoundStats
- PORT_PLAN.md: master inventory, 8-phase roadmap, dependency replacement
  map, deviations log, and NEXT UP tracker driving the autonomous loop
```

```text
 Port/src/CosmicShore.Data/Enums/VesselClassType.cs             |  25 ++
 Port/src/CosmicShore.Data/Enums/VesselImpactEffects.cs         |  15 +
 Port/src/CosmicShore.Data/Enums/VesselThrottleModifier.cs      |  16 +
 Port/src/CosmicShore.Data/Enums/VesselVelocityModifier.cs      |  18 +
 Port/src/CosmicShore.Data/Structs/BootStatusRequest.cs         |  22 +
 Port/src/CosmicShore.Data/Structs/DailyChallenge.cs            |  12 +
 Port/src/CosmicShore.Data/Structs/DailyChallengeRewardState.cs |  16 +
 Port/src/CosmicShore.Data/Structs/GameplayReward.cs            |  14 +
 Port/src/CosmicShore.Data/Structs/TrainingGameProgress.cs      |  52 +++
 Port/src/CosmicShore.Engine/Attributes.cs                      |  56 +++
 Port/src/CosmicShore.Engine/Collections/FixedString64Bytes.cs  |  52 +++
 Port/src/CosmicShore.Engine/Math/Color.cs                      |  57 +++
 Port/src/CosmicShore.Engine/Math/Mathf.cs                      | 176 ++++++++
 Port/src/CosmicShore.Engine/Math/Quaternion.cs                 | 253 +++++++++++
 Port/src/CosmicShore.Engine/Math/Vector2.cs                    | 130 ++++++
 Port/src/CosmicShore.Engine/Math/Vector3.cs                    | 189 +++++++++
 Port/src/CosmicShore.Engine/Math/Vector4.cs                    |  55 +++
 Port/src/CosmicShore.Engine/Networking/NetworkBehaviour.cs     |  40 ++
 Port/src/CosmicShore.Engine/Networking/NetworkVariable.cs      |  59 +++
 Port/src/CosmicShore.Engine/ScriptableObject.cs                |  27 ++
 Port/src/CosmicShore.Engine/Soap/ScriptableEvent.cs            |  41 ++
 Port/src/CosmicShore.Engine/Soap/ScriptableList.cs             |  75 ++++
 Port/src/CosmicShore.Engine/Soap/ScriptableVariable.cs         |  68 +++
 Port/src/CosmicShore.Engine/Time.cs                            |  38 ++
 Port/tests/CosmicShore.Tests/EnumFreezeTests.cs                | 304 ++++++++++++++
 Port/tests/CosmicShore.Tests/MathTests.cs                      | 266 ++++++++++++
 Port/tests/CosmicShore.Tests/NetworkingTests.cs                | 123 ++++++
 Port/tests/CosmicShore.Tests/RoundStatsTests.cs                | 125 ++++++
 Port/tests/CosmicShore.Tests/SoapTests.cs                      | 163 ++++++++
 59 files changed, 4330 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 4657 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
new file mode 100644
index 000000000..dd4954469
--- /dev/null
+++ b/Port/PORT_PLAN.md
@@ -0,0 +1,143 @@
+# Cosmic Shore Port — Master Plan & Live Status
+
+> **This file is the persistent state of the autonomous porting loop.** Each loop
+> iteration: (1) read **NEXT UP**, (2) implement + test the next chunk, (3) update the
+> status tables and NEXT UP, (4) commit + push to `claude/quirky-cannon-sk8a02`.
+> The goal, verbatim from the prompter: *"remake the entire game … wholly ours without
+> any dependency on unity or anything else that prevents you from developing in your
+> own loop without a human's involvement. We want everything replicated. Loose nothing."*
+
+## Ground rules
+
+1. **Lose nothing.** Every behavior, tuned value, enum ID, event contract, and system
+   in the Unity project gets a counterpart here. When a Unity feature can't be ported
+   verbatim, record the deviation in the **Deviations log** below.
+2. **Headless first.** The simulation must build, run, and verify with `dotnet test`
+   alone. Rendering/audio/input are pluggable backends added later; they may never
+   become load-bearing for game logic.
+3. **Verbatim porting.** Same namespaces, file names, member names. Only the
+   using-directive substitutions in README.md are allowed without justification.
+4. **All architecture rules from /CLAUDE.md carry over**: SOAP for cross-system
+   communication, single-writer pattern, fail-loud (no null guards on event channels),
+   config in data assets not code, conserved mass / emergent-systems design philosophy,
+   frozen enum numeric values.
+5. **Tests accompany every port.** Enum values get freeze tests; logic gets behavior
+   tests. The Unity project's existing edit-mode tests get ported alongside their
+   subjects.
+
+## Toolchain (re-verify each fresh container)
+
+- .NET 10 SDK at `/opt/dotnet` (installed via `dotnet-install.sh --channel LTS`;
+  `export PATH=/opt/dotnet:$PATH`, persisted in `~/.bashrc`). nuget.org reachable.
+  crates.io blocked; npm available (unused).
+- Build: `cd Port && dotnet build && dotnet test` — must be green before every commit.
+
+## Phase roadmap
+
+| Phase | Scope | Status |
+|---|---|---|
+| **0 — Foundation** | Toolchain, solution, engine math/SOAP/attrs/net-primitives, Data layer, test harness | ✅ **DONE** (iteration 1) |
+| **1 — Engine core** | First-party async (UniTask replacement), DI container (Reflex replacement), asset registry (ScriptableObject .asset → JSON), update loop + scheduler, logging (Debug.Log replacement), GameObject/Transform/component model decision | ⬜ in progress — **next** |
+| **2 — Simulation core** | ResourceSystem, VesselStatus + transformer/controller, Prism/Trail/TrailFollower, impact-effects matrix (impactors × effect SOs), crystals, cells (CellPhase/aggression), flora/fauna ecosystem (conserved mass!), elementals | ⬜ |
+| **3 — Game modes** | MiniGameControllerBase hierarchy (template method: rounds→turns→countdown→gameplay→end), turn monitors, scoring (incl. golf rules), AI pilot/gunner, all 36 GameModes' controllers (priority: Freestyle, CellularDuel, WildlifeBlitz, HexRace, Joust, CrystalCapture) | ⬜ |
+| **4 — Networking** | First-party transport + replication: NetworkVariable wire sync, RPC equivalent, server-authoritative session flow, host/client lifecycle, replacing Unity Netcode + UGS Relay/Sessions/Lobby with self-hosted session server | ⬜ |
+| **5 — Presentation** | Renderer backend (evaluate: custom GL via first-party bindings vs software-rendered headless screenshots first), camera system (CameraSettingsSO port), input strategies (Keyboard/Gamepad/Touch via IInputStrategy), HUD/UI framework, VFX/shader ports (HLSL sources exist in repo), audio backend (Wwise replacement) | ⬜ |
+| **6 — Services** | Replace UGS/PlayFab/Firebase: auth, cloud save, leaderboards, friends/presence, parties/invites, analytics — self-hosted service + local-first fallback | ⬜ |
+| **7 — Content pipeline** | Extract all `_SO_Assets/**/*.asset` (Unity YAML) → JSON for the asset registry; scene descriptions → first-party scene format; models/textures/audio export | ⬜ |
+| **8 — Integration** | Full playable loop: boot → menu → game mode → end-game → menu, multiplayer session E2E, performance passes | ⬜ |
+
+## Status — detailed inventory
+
+### Phase 0 (✅ done, iteration 1 — 2026-06-10)
+
+| Item | Port location | Tests |
+|---|---|---|
+| Solution + projects (net10.0) | `Port/CosmicShore.slnx`, `src/`, `tests/` | `dotnet test` green: 259/259 |
+| Math: Vector2/3/4, Quaternion (YXZ Euler convention verified), Mathf (incl. SmoothDamp), Color | `src/CosmicShore.Engine/Math/` | `MathTests.cs` |
+| Time (frame clock, harness-driven `Advance`) | `src/CosmicShore.Engine/Time.cs` | — |
+| Attributes: SerializeField/Header/Tooltip/Range/Min/TextArea/CreateAssetMenu | `src/CosmicShore.Engine/Attributes.cs` | — |
+| ScriptableObject base + CreateInstance | `src/CosmicShore.Engine/ScriptableObject.cs` | — |
+| SOAP: ScriptableVariable<T> (+8 concrete), ScriptableEvent<T>/NoParam (+6 concrete), ScriptableList<T> | `src/CosmicShore.Engine/Soap/` | `SoapTests.cs` |
+| Networking primitives: NetworkBehaviour (Spawn/Despawn lifecycle), NetworkVariable<T> (perm-aware, change callbacks), FixedString64Bytes (61-byte UTF-8 cap) | `src/CosmicShore.Engine/Networking/`, `Collections/` | `NetworkingTests.cs` |
+| **Data layer: all 26 enum files + 5 struct files ported verbatim** (incl. RoundStats NetworkBehaviour, IRoundStats with default-method Cleanup, DomainStats) | `src/CosmicShore.Data/` | `EnumFreezeTests.cs` (full numeric freeze), `RoundStatsTests.cs` (both lifecycle modes) |
+
+### Unity-project inventory still to port (tracked at directory granularity; refine as phases open)
+
+| Source (under `Assets/`) | Files | Target phase | Status |
+|---|---|---|---|
+| `_Scripts/Data/` | 31 | 0 | ✅ done |
+| `_Scripts/Utility/` (ClassExtensions, DataContainers incl. GameDataSO, PoolsAndBuffers, Effects, DataPersistence) | ~80 | 1–2 | ⬜ |
+| `_Scripts/ScriptableObjects/` (SO_* defs + 16 SOAP subdirs + VesselPrefabContainer) | ~70 | 1–2 | ⬜ |
+| `_Scripts/Controller/Vessel/` (VesselStatus, Prism, Trail, actions, ResourceSystem) | ~150 | 2 | ⬜ |
+| `_Scripts/Controller/ImpactEffects/` (11 impactors, 20+ effect SO types) | ~60 | 2 | ⬜ |
+| `_Scripts/Controller/Environment/` (cells, crystals, flora/fauna, flow/warp fields, spawners) | ~100 | 2 | ⬜ |
+| `_Scripts/Controller/Managers/` (PrismScaleManager, MaterialStateManager, …) | ~15 | 2/5 | ⬜ |
```

</details>

### `76bee5e4b` — docs(port): add progress-build testing protocol + milestone log to loop plan

_Claude, 2026-06-10 23:48:38 +0000_

```text
Prompter requirement: the branch must always offer a one-command testable
build. Adds the testing protocol (green-branch invariant, CosmicShore.Cli
harness, visual artifacts, milestone tags + notifications) and pulls the
CLI harness forward into iteration 2 as milestone M1.
```

```text
 Port/PORT_PLAN.md | 47 +++++++++++++++++++++++++++++++++++++++++++++--
 1 file changed, 45 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index dd4954469..eb6d8206e 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -24,6 +24,45 @@
 5. **Tests accompany every port.** Enum values get freeze tests; logic gets behavior
    tests. The Unity project's existing edit-mode tests get ported alongside their
    subjects.
+6. **Always shippable progress builds** (prompter requirement, 2026-06-10): the branch
+   must always offer a one-command way for a human to try the current state — see
+   "Progress builds — testing protocol" below. Growing the testable surface is part of
+   every phase, not an afterthought.
+
+## Progress builds — testing protocol
+
+The prompter tests progress without prompting the loop. Contract:
+
+1. **Green branch invariant.** Every push to `claude/quirky-cannon-sk8a02` has
+   `cd Port && dotnet build && dotnet test` green. The branch is always safe to pull.
+2. **Runnable harness: `CosmicShore.Cli`** (`src/CosmicShore.Cli`, lands iteration 2).
+   One command to exercise the current port on any machine with the .NET 10 SDK:
+   `cd Port && dotnet run --project src/CosmicShore.Cli`. It grows with the port:
+   - now → engine smoke: boot loop, SOAP wiring, state machine walk, version banner
+   - phase 2 → scripted simulation: AI vessels, prisms, crystals, cells; deterministic
+     via `--seed`; emits a readable match transcript + final stats
+   - phase 3 → full game-mode rounds headless (`--mode hexrace --players 4 --seed 42`)
+   - phase 5 → `--render` flag for the interactive window; headless stays the default
+3. **Visual artifacts before the renderer is interactive.** From the first render-capable
+   milestone, headless render-to-PNG (and short MP4) smoke outputs are committed under
+   `Port/artifacts/` (small, curated) and sent into the chat at milestones so progress
+   is visible with zero local setup.
+4. **Milestone tags + notification.** Each numbered milestone (M1 first CLI sim, M2 first
+   full game-mode round, M3 first picture, M4 first interactive build, …) gets an
+   annotated git tag `port-mN` on the branch and a push notification to the prompter
+   with the exact command (or file) to try. Milestone log lives in this file.
+5. **Local prerequisites for the prompter** (one-time):
+   `winget install Microsoft.DotNet.SDK.10` (Windows) / `brew install dotnet-sdk` (macOS),
+   then `git fetch origin claude/quirky-cannon-sk8a02 && git checkout claude/quirky-cannon-sk8a02`.
+
+### Milestone log
+
+| Tag | What became testable | Command | Status |
+|---|---|---|---|
+| `port-m1` | CLI engine smoke + sim skeleton | `cd Port && dotnet run --project src/CosmicShore.Cli` | ⬜ next |
+| `port-m2` | First full headless game-mode round (AI vs AI) | `… -- --mode <mode> --seed <n>` | ⬜ |
+| `port-m3` | First rendered frame (PNG artifact in chat + repo) | pull + open artifact | ⬜ |
+| `port-m4` | First interactive desktop build | `… -- --render` | ⬜ |
 
 ## Toolchain (re-verify each fresh container)
 
@@ -128,10 +167,14 @@
 3. Implement the DI container (`CosmicShore.Engine.Injection`): `RegisterValue`,
    lazy `RegisterFactory`, `[Inject]` field injection, container scopes + tests.
 4. Implement `Debug` logging shim (`Debug.Log/LogWarning/LogError`) → pluggable sink.
-5. Port `_Scripts/Utility/ClassExtensions/` pure-logic extensions (skip UniTaskExtensions
+5. **Create `CosmicShore.Cli` (milestone M1)**: console runner that boots the engine
+   loop, walks the ApplicationState machine via SOAP events, prints a version/status
+   banner and a deterministic engine smoke transcript. Tag `port-m1`, notify prompter
+   per the testing protocol above.
+6. Port `_Scripts/Utility/ClassExtensions/` pure-logic extensions (skip UniTaskExtensions
    — superseded by Engine.Tasks) and any other Unity-free utility code + their existing
    tests from `_Scripts/Tests/EditMode/`.
-6. Update this file (status tables + NEXT UP), commit, push.
+7. Update this file (status tables, milestone log, NEXT UP), commit, push.
 
 ## Loop protocol (every iteration)
 
```

</details>

### `25b5582db` — feat(port): engine core — component model, game loop, async, DI + M1 CLI harness

_Claude, 2026-06-11 00:26:00 +0000_

```text
Phase 1 of the standalone port (see Port/docs/ENGINE_CORE.md for the
locked design):

- Scene model: Object (fake-null destroyed contract), GameObject,
  Component/Behaviour/MonoBehaviour with reflective lifecycle discovery
  (ported files keep private void Update() signatures verbatim),
  Transform (TRS hierarchy), Scene, DefaultExecutionOrder support
- GameLoop: deterministic Tick with fixed-step accumulator, phase
  ordering (Start → FixedUpdate → Update → scheduler → LateUpdate →
  end-of-frame → destroy), per-behaviour exception isolation
- CosmicShore.Engine.Tasks replaces UniTask: GameTask.Yield/Delay/
  WaitUntil/WaitForEndOfFrame/SwitchToMainThread awaitables with
  structural main-thread affinity and synchronous-cancellation parity
  (cts.Cancel runs awaiting catch/finally inline — regression-tested);
  GameSynchronizationContext marshals external Task continuations
- CosmicShore.Engine.Injection replaces Reflex: RegisterValue/lazy
  RegisterFactory/[Inject] fields+properties/child scopes/InjectGameObject
- Debug → pluggable ILogSink; ColorUtility; LayerMask registry
- CosmicShore.Game project: first verbatim ports (CSDebug,
  DebugExtensions, GameObjectExtension, TransformExtensions on GameTask)
  + ported IRoundStatsCleanupTests
- CosmicShore.Cli (milestone M1): deterministic headless smoke — SOAP
  state walk, 240-frame sim with lifecycle/tasks/DI, RoundStats events;
  exits 0/1 for scripted verification

322 tests green.
```

```text
 Port/src/CosmicShore.Cli/Program.cs                                   | 205 +++++++++++++++++++
 Port/src/CosmicShore.Engine/Attributes.cs                             |  11 ++
 Port/src/CosmicShore.Engine/ColorUtility.cs                           |  73 +++++++
 Port/src/CosmicShore.Engine/Debug.cs                                  |  90 +++++++++
 Port/src/CosmicShore.Engine/Injection/Container.cs                    | 167 ++++++++++++++++
 Port/src/CosmicShore.Engine/LayerMask.cs                              |  69 +++++++
 Port/src/CosmicShore.Engine/Object.cs                                 |  88 +++++++++
 Port/src/CosmicShore.Engine/SceneGraph/Component.cs                   |  55 ++++++
 Port/src/CosmicShore.Engine/SceneGraph/GameLoop.cs                    | 167 ++++++++++++++++
 Port/src/CosmicShore.Engine/SceneGraph/GameObject.cs                  | 184 +++++++++++++++++
 Port/src/CosmicShore.Engine/SceneGraph/LifecycleMethodCache.cs        |  68 +++++++
 Port/src/CosmicShore.Engine/SceneGraph/MonoBehaviour.cs               | 122 ++++++++++++
 Port/src/CosmicShore.Engine/SceneGraph/Scene.cs                       |  40 ++++
 Port/src/CosmicShore.Engine/SceneGraph/Transform.cs                   | 187 ++++++++++++++++++
 Port/src/CosmicShore.Engine/ScriptableObject.cs                       |   8 +-
 Port/src/CosmicShore.Engine/Tasks/GameSynchronizationContext.cs       |  56 ++++++
 Port/src/CosmicShore.Engine/Tasks/GameTask.cs                         | 210 ++++++++++++++++++++
 Port/src/CosmicShore.Engine/Tasks/GameTaskScheduler.cs                | 154 +++++++++++++++
 Port/src/CosmicShore.Engine/Time.cs                                   |  17 +-
 Port/src/CosmicShore.Game/Utility/CSDebug.cs                          | 181 +++++++++++++++++
 Port/src/CosmicShore.Game/Utility/ClassExtensions/DebugExtensions.cs  |  68 +++++++
 .../CosmicShore.Game/Utility/ClassExtensions/GameObjectExtension.cs   |  70 +++++++
 .../CosmicShore.Game/Utility/ClassExtensions/TransformExtensions.cs   | 143 ++++++++++++++
 Port/tests/CosmicShore.Tests/AssemblyInfo.cs                          |   3 +
 Port/tests/CosmicShore.Tests/GameLoopAndTasksTests.cs                 | 297 ++++++++++++++++++++++++++++
 Port/tests/CosmicShore.Tests/IRoundStatsCleanupTests.cs               | 219 ++++++++++++++++++++
 Port/tests/CosmicShore.Tests/InjectionTests.cs                        | 153 ++++++++++++++
 Port/tests/CosmicShore.Tests/SceneModelTests.cs                       | 340 ++++++++++++++++++++++++++++++++
 Port/tests/CosmicShore.Tests/UtilityPortTests.cs                      | 269 +++++++++++++++++++++++++
 33 files changed, 3875 insertions(+), 34 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 4156 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index eb6d8206e..eb88c527b 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -59,7 +59,7 @@ The prompter tests progress without prompting the loop. Contract:
 
 | Tag | What became testable | Command | Status |
 |---|---|---|---|
-| `port-m1` | CLI engine smoke + sim skeleton | `cd Port && dotnet run --project src/CosmicShore.Cli` | ⬜ next |
+| `port-m1` | CLI engine smoke + deterministic sim (loop, lifecycle, tasks, DI, SOAP, RoundStats) | `cd Port && dotnet run --project src/CosmicShore.Cli` | ✅ 2026-06-11 |
 | `port-m2` | First full headless game-mode round (AI vs AI) | `… -- --mode <mode> --seed <n>` | ⬜ |
 | `port-m3` | First rendered frame (PNG artifact in chat + repo) | pull + open artifact | ⬜ |
 | `port-m4` | First interactive desktop build | `… -- --render` | ⬜ |
@@ -76,7 +76,7 @@ The prompter tests progress without prompting the loop. Contract:
 | Phase | Scope | Status |
 |---|---|---|
 | **0 — Foundation** | Toolchain, solution, engine math/SOAP/attrs/net-primitives, Data layer, test harness | ✅ **DONE** (iteration 1) |
-| **1 — Engine core** | First-party async (UniTask replacement), DI container (Reflex replacement), asset registry (ScriptableObject .asset → JSON), update loop + scheduler, logging (Debug.Log replacement), GameObject/Transform/component model decision | ⬜ in progress — **next** |
+| **1 — Engine core** | First-party async (UniTask replacement ✅), DI container (Reflex replacement ✅), update loop + scheduler ✅, logging ✅, GameObject/Transform/MonoBehaviour/Scene model ✅ (see `docs/ENGINE_CORE.md`); asset registry (.asset → JSON) deferred to content phase | ✅ **DONE** (iteration 2) — remaining: asset registry, prefab factories, multi-scene |
 | **2 — Simulation core** | ResourceSystem, VesselStatus + transformer/controller, Prism/Trail/TrailFollower, impact-effects matrix (impactors × effect SOs), crystals, cells (CellPhase/aggression), flora/fauna ecosystem (conserved mass!), elementals | ⬜ |
 | **3 — Game modes** | MiniGameControllerBase hierarchy (template method: rounds→turns→countdown→gameplay→end), turn monitors, scoring (incl. golf rules), AI pilot/gunner, all 36 GameModes' controllers (priority: Freestyle, CellularDuel, WildlifeBlitz, HexRace, Joust, CrystalCapture) | ⬜ |
 | **4 — Networking** | First-party transport + replication: NetworkVariable wire sync, RPC equivalent, server-authoritative session flow, host/client lifecycle, replacing Unity Netcode + UGS Relay/Sessions/Lobby with self-hosted session server | ⬜ |
@@ -132,10 +132,10 @@ The prompter tests progress without prompting the loop. Contract:
 
 | Unity-era dependency | Replacement strategy | Phase | Status |
 |---|---|---|---|
-| Unity engine core (GameObject/MonoBehaviour/scenes) | First-party component/scene model in `CosmicShore.Engine` (design doc before code — see NEXT UP) | 1 | ⬜ |
-| UniTask | First-party awaitable scheduler on the engine update loop (`CosmicShore.Engine.Tasks`); main-thread affinity guaranteed by design — no `.AsMainThread()` needed once all continuations resume on the loop | 1 | ⬜ |
+| Unity engine core (GameObject/MonoBehaviour/scenes) | ✅ First-party component/scene model in `CosmicShore.Engine` (`docs/ENGINE_CORE.md`); Instantiate/prefabs + multi-scene deferred to content phase; physics deferred to phase 2 | 1 | ✅ core |
+| UniTask | ✅ `CosmicShore.Engine.Tasks` (`GameTask.*`, structural main-thread affinity, synchronous cancellation parity) — `.AsMainThread()` retired | 1 | ✅ |
 | Obvious.Soap | ✅ `CosmicShore.Engine.Soap` | 0 | ✅ |
-| Reflex DI | First-party container (registration API mirroring AppManager.InstallBindings usage: RegisterValue/RegisterFactory-lazy + `[Inject]`) | 1 | ⬜ |
+| Reflex DI | ✅ `CosmicShore.Engine.Injection.Container` (RegisterValue/RegisterFactory-lazy, `[Inject]`, child scopes, InjectGameObject) | 1 | ✅ |
 | Unity Netcode for GameObjects | First-party replication over UDP/TCP (NetworkVariable sync + RPC); API contract already established in `Engine.Networking` | 4 | ⬜ |
 | UGS (Auth, Relay, Sessions/Lobby, Friends, CloudSave, Leaderboards, Analytics) | Self-hosted session/identity service (single small server, JSON protocol) + local-first offline mode | 6 | ⬜ |
 | PlayFab (economy, catalog) | Same self-hosted service | 6 | ⬜ |
@@ -156,25 +156,41 @@ The prompter tests progress without prompting the loop. Contract:
 | 2 | `NetworkVariable<T>` change callback uses `EqualityComparer<T>.Default` dedup; Unity Netcode dedups on serialized-value equality. Behaviorally identical for the value types used. | — |
 | 3 | SOAP `ScriptableEvent` drops Unity-inspector listener components; subscription is code-only until the scene/component model lands (phase 1). Inspector-wired `EventListener*` components become scene-asset-driven bindings in phase 7. | No scenes yet. |
 
-## NEXT UP (iteration 2)
-
-1. **Design doc first**: `Port/docs/ENGINE_CORE.md` — decide the GameObject/Transform/
-   component model (recommendation: keep `MonoBehaviour`-shaped API — `Awake/Start/
-   Update/OnEnable` driven by a first-party `Scene` + `GameLoop` — so gameplay files
-   port verbatim), the update-loop architecture, and the async model replacing UniTask.
-2. Implement `CosmicShore.Engine.Tasks` (awaitable scheduler: `GameTask.Yield()`,
-   `Delay`, `WaitUntil` driven by the game loop; CancellationToken support) + tests.
-3. Implement the DI container (`CosmicShore.Engine.Injection`): `RegisterValue`,
-   lazy `RegisterFactory`, `[Inject]` field injection, container scopes + tests.
-4. Implement `Debug` logging shim (`Debug.Log/LogWarning/LogError`) → pluggable sink.
-5. **Create `CosmicShore.Cli` (milestone M1)**: console runner that boots the engine
-   loop, walks the ApplicationState machine via SOAP events, prints a version/status
-   banner and a deterministic engine smoke transcript. Tag `port-m1`, notify prompter
-   per the testing protocol above.
-6. Port `_Scripts/Utility/ClassExtensions/` pure-logic extensions (skip UniTaskExtensions
-   — superseded by Engine.Tasks) and any other Unity-free utility code + their existing
-   tests from `_Scripts/Tests/EditMode/`.
-7. Update this file (status tables, milestone log, NEXT UP), commit, push.
+## Iteration log
+
+- **Iteration 1** (2026-06-10): Phase 0 — toolchain, solution, engine math/SOAP/attrs/
+  net primitives, full Data layer, 259 tests.
+- **Iteration 2** (2026-06-11): Phase 1 — component/scene model (GameObject, Transform,
+  MonoBehaviour with reflective lifecycle discovery, fake-null Object contract),
+  GameLoop (fixed-step accumulator, phase ordering), `Engine.Tasks` (GameTask awaitables,
+  synchronous-cancellation parity, GameSynchronizationContext), `Engine.Injection`
+  Container, Debug/ColorUtility/LayerMask, `CosmicShore.Game` project with first ported
+  Utility files (CSDebug, DebugExtensions, GameObjectExtension, TransformExtensions with
+  GameTask), ported IRoundStatsCleanupTests, `docs/ENGINE_CORE.md`, **CosmicShore.Cli
+  milestone M1** (tag `port-m1`). 322 tests.
+
+## NEXT UP (iteration 3)
+
+Goal: finish the pure-logic substrate so VesselStatus/ResourceSystem can port in
+iteration 4.
+
+1. Port the custom SOAP types from `_Scripts/ScriptableObjects/SOAP/` (16 subdirs:
+   ClassType, CrystalStats, PrismStats, AbilityStats, InputEvents, Quaternion,
+   Transform, PipData, VesselHUDData, SilhouetteData, GameplaySFX, PartyData,
```

</details>

### `7bc0fdf5b` — docs(port): record M1 by commit hash — git proxy accepts branch pushes only

_Claude, 2026-06-11 00:26:58 +0000_

```text
 Port/PORT_PLAN.md | 12 +++++++-----
 1 file changed, 7 insertions(+), 5 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index eb88c527b..4dd4f1870 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -47,10 +47,12 @@ The prompter tests progress without prompting the loop. Contract:
    milestone, headless render-to-PNG (and short MP4) smoke outputs are committed under
    `Port/artifacts/` (small, curated) and sent into the chat at milestones so progress
    is visible with zero local setup.
-4. **Milestone tags + notification.** Each numbered milestone (M1 first CLI sim, M2 first
-   full game-mode round, M3 first picture, M4 first interactive build, …) gets an
-   annotated git tag `port-mN` on the branch and a push notification to the prompter
-   with the exact command (or file) to try. Milestone log lives in this file.
+4. **Milestone log + notification.** Each numbered milestone (M1 first CLI sim, M2 first
+   full game-mode round, M3 first picture, M4 first interactive build, …) is recorded in
+   the milestone log below with its commit hash, and the prompter gets a push
+   notification with the exact command (or file) to try. (Annotated `port-mN` tags are
+   created locally, but this environment's git proxy only accepts branch pushes — the
+   log + commit message are the durable record.)
 5. **Local prerequisites for the prompter** (one-time):
    `winget install Microsoft.DotNet.SDK.10` (Windows) / `brew install dotnet-sdk` (macOS),
    then `git fetch origin claude/quirky-cannon-sk8a02 && git checkout claude/quirky-cannon-sk8a02`.
@@ -59,7 +61,7 @@ The prompter tests progress without prompting the loop. Contract:
 
 | Tag | What became testable | Command | Status |
 |---|---|---|---|
-| `port-m1` | CLI engine smoke + deterministic sim (loop, lifecycle, tasks, DI, SOAP, RoundStats) | `cd Port && dotnet run --project src/CosmicShore.Cli` | ✅ 2026-06-11 |
+| `port-m1` | CLI engine smoke + deterministic sim (loop, lifecycle, tasks, DI, SOAP, RoundStats) | `cd Port && dotnet run --project src/CosmicShore.Cli` | ✅ 2026-06-11 (commit `25b5582d`) |
 | `port-m2` | First full headless game-mode round (AI vs AI) | `… -- --mode <mode> --seed <n>` | ⬜ |
 | `port-m3` | First rendered frame (PNG artifact in chat + repo) | pull + open artifact | ⬜ |
 | `port-m4` | First interactive desktop build | `… -- --render` | ⬜ |
```

</details>

### `a3f71593d` — feat(port): custom SOAP layer, utility substrate, verbatim NUnit EditMode suite

_Claude, 2026-06-11 00:44:39 +0000_

```text
Iteration 3 — the pure-logic substrate ahead of the phase-2 simulation core:

- Engine: UnityEvent/UnityEvent<T> (CosmicShore.Engine.Events),
  EventListenerGeneric + EventResponse (Engine.Soap), AddComponentMenu/
  FormerlySerializedAs/Preserve attributes; engine ScriptableEventUlong
  removed in favor of the game's own (matches original package split)
- 48 of 58 custom SOAP type files ported verbatim into
  CosmicShore.Game/ScriptableObjects/SOAP (10 deferred on unported
  gameplay types — tracked in PORT_PLAN deviations)
- Extractions with source provenance: StatsManager stat structs,
  PrismType, MenuAudioCategory/GameplaySFXCategory
- Utility ports: DisposableGroup, GeometryUtils, SceneNameListSO,
  CellPhaseThresholds + CellPhaseRules (hysteresis phase resolver)
- NEW tests/CosmicShore.Tests.Ported (NUnit 3, mirroring Unity Test
  Framework): 12 original EditMode test files ported verbatim. The suite
  immediately caught three latent upstream issues, fixed in the port:
  TrainingGameProgress parameterless-init contract (real C#10 ctor),
  stale GameModes member count (34→35), and the ImpactEffects uniqueness
  test contradicting the shipped duplicate-value enum (now frozen as-is)
- CLI section [4]: SOAP custom channels + party roster list semantics +
  cell phase hysteresis walk (climb, band-hold, multi-step collapse)

528 tests green (322 xunit + 206 NUnit).
```

```text
 .../SOAP/ScriptablePartyData/ScriptableEventPartyPlayerData.cs        |  12 ++
 .../SOAP/ScriptablePartyData/ScriptableListPartyPlayerData.cs         |  12 ++
 .../ScriptableObjects/SOAP/ScriptablePipData/EventListenerPipData.cs  |  31 ++++
 .../ScriptableObjects/SOAP/ScriptablePipData/PipData.cs               |   9 +
 .../SOAP/ScriptablePipData/ScriptableEventPipData.cs                  |  11 ++
 .../SOAP/ScriptablePrismStats/EventListenerPrismStats.cs              |  27 +++
 .../SOAP/ScriptablePrismStats/ScriptableEventPrismStats.cs            |  12 ++
 .../SOAP/ScriptableQuaternion/EventListenerQuaternion.cs              |  26 +++
 .../SOAP/ScriptableQuaternion/ScriptableEventQuaternion.cs            |  11 ++
 .../SOAP/ScriptableTransform/EventListenerTransform.cs                |  26 +++
 .../SOAP/ScriptableTransform/ScriptableEventTransform.cs              |  11 ++
 .../ScriptableObjects/SOAP/ScriptableUlong/ScriptableEventUlong.cs    |  10 ++
 Port/src/CosmicShore.Game/System/Audio/AudioCategories.cs             |  59 +++++++
 .../CosmicShore.Game/Utility/DataContainers/CellPhaseThresholds.cs    | 117 +++++++++++++
 Port/src/CosmicShore.Game/Utility/DataContainers/SceneNameListSO.cs   |  27 +++
 Port/src/CosmicShore.Game/Utility/DisposableGroup.cs                  |  26 +++
 Port/src/CosmicShore.Game/Utility/GeometryUtils.cs                    |  69 ++++++++
 Port/tests/CosmicShore.Tests.Ported/CSDebugTests.cs                   | 183 ++++++++++++++++++++
 Port/tests/CosmicShore.Tests.Ported/CellPhaseRulesTests.cs            | 174 +++++++++++++++++++
 Port/tests/CosmicShore.Tests.Ported/DisposableGroupTests.cs           | 174 +++++++++++++++++++
 Port/tests/CosmicShore.Tests.Ported/EcologyEnumIntegrityTests.cs      |  92 ++++++++++
 Port/tests/CosmicShore.Tests.Ported/EnumIntegrityExtendedTests.cs     | 242 ++++++++++++++++++++++++++
 Port/tests/CosmicShore.Tests.Ported/EnumIntegrityTests.cs             | 295 ++++++++++++++++++++++++++++++++
 Port/tests/CosmicShore.Tests.Ported/GeometryUtilsTests.cs             | 245 ++++++++++++++++++++++++++
 Port/tests/CosmicShore.Tests.Ported/PartyInviteDataTests.cs           | 105 ++++++++++++
 Port/tests/CosmicShore.Tests.Ported/PartyPlayerDataTests.cs           | 148 ++++++++++++++++
 Port/tests/CosmicShore.Tests.Ported/ResourceCollectionTests.cs        | 136 +++++++++++++++
 Port/tests/CosmicShore.Tests.Ported/ShipModifierTests.cs              | 156 +++++++++++++++++
 Port/tests/CosmicShore.Tests.Ported/TrainingGameProgressTests.cs      | 205 ++++++++++++++++++++++
 75 files changed, 3806 insertions(+), 22 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 4316 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 4dd4f1870..62fb0ca82 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -156,7 +156,12 @@ The prompter tests progress without prompting the loop. Contract:
 |---|---|---|
 | 1 | `Mathf.Round` uses banker's rounding (MidpointRounding.ToEven) — same as Unity. `Vector3.SmoothDamp` is component-wise (Unity clamps the change vector once); identical for maxSpeed=∞ usage. | Documented at the source; revisit if a ported system clamps SmoothDamp speed. |
 | 2 | `NetworkVariable<T>` change callback uses `EqualityComparer<T>.Default` dedup; Unity Netcode dedups on serialized-value equality. Behaviorally identical for the value types used. | — |
-| 3 | SOAP `ScriptableEvent` drops Unity-inspector listener components; subscription is code-only until the scene/component model lands (phase 1). Inspector-wired `EventListener*` components become scene-asset-driven bindings in phase 7. | No scenes yet. |
+| 3 | SOAP `EventListener*` components are ported and functional in code (`Engine.Soap.EventListenerGeneric` + `Engine.Events.UnityEvent`); inspector wiring arrives with the scene-asset pipeline (phase 7). The `UnityEvent` type NAME is kept for verbatim porting — it is first-party code in `CosmicShore.Engine.Events`. | — |
+| 4 | **Upstream latent bug fixed in port**: `new TrainingGameProgress()` zero-initialized (null `Progress`, intensity 0) — C# 9 couldn't express the intended parameterless init, and the 16 TrainingGameProgressTests documenting the contract were silently red upstream (no CI). Port adds a real parameterless ctor chaining to the dummy-arg one. Worth fixing upstream. | Test-documented contract wins. |
+| 5 | **Upstream stale test fixed in port**: `GameModes_HasExpectedMemberCount` expected 34; the enum has 35 members (MultiplayerCrystalCapture added without updating). Updated to 35. | — |
+| 6 | **Upstream latent red test reframed**: `ImpactEffects_AllValuesAreUnique` contradicts the shipped enum, which intentionally merges legacy effect groups sharing values 1-8, 10. Values are wire format — port freezes the exact duplicate set instead so NEW collisions still fail. | Enum values untouchable. |
+| 7 | 10 SOAP files deferred pending gameplay types: ScriptableEventVesselImpactor / ExplosionDebuffApplied / SkimmerDebuffApplied (IVessel/IVesselStatus/VesselImpactor), ScriptableSilhouetteData/* (SilhouetteController), ScriptableVesselHUDData/* (MiniGameHUD), VesselPrefabContainer (IVesselStatus). MainMenuStateTests (MainMenuController) and GameObjectExtensionTests (physics types) likewise port with their subjects. | Tracked in NEXT UP. |
+| 8 | Stat structs (CellStats/CrystalStats/PrismStats/AbilityStats), PrismType, and audio category enums are extracted into their own port files (source noted in headers) because their host classes (StatsManager, PrismFactory, AudioSystem) port in later phases. | File-split only; content verbatim. |
 
 ## Iteration log
 
@@ -171,27 +176,35 @@ The prompter tests progress without prompting the loop. Contract:
   GameTask), ported IRoundStatsCleanupTests, `docs/ENGINE_CORE.md`, **CosmicShore.Cli
   milestone M1** (tag `port-m1`). 322 tests.
 
-## NEXT UP (iteration 3)
-
-Goal: finish the pure-logic substrate so VesselStatus/ResourceSystem can port in
+- **Iteration 3** (2026-06-11): custom SOAP layer + pure-logic substrate — engine
+  additions (UnityEvent/UnityEvent<T> in `Engine.Events`, EventListenerGeneric/
+  EventResponse in `Engine.Soap`, AddComponentMenu/FormerlySerializedAs/Preserve
+  attributes); 48/58 SOAP custom-type files ported verbatim (10 deferred on unported
+  gameplay types — see Deviations); struct/enum extractions (StatsManager stat structs,
+  PrismType, MenuAudioCategory/GameplaySFXCategory); Utility ports (DisposableGroup,
+  GeometryUtils, SceneNameListSO, CellPhaseThresholds + CellPhaseRules); **new
+  `tests/CosmicShore.Tests.Ported` project (NUnit 3, mirrors Unity Test Framework) with
+  12 original EditMode test files ported verbatim** — which immediately caught three
+  latent upstream issues (fixed in port, see Deviations #4-6); CLI section [4] (SOAP
+  channels + cell phase hysteresis). 528 tests green (322 xunit + 206 NUnit).
+
+## NEXT UP (iteration 4)
+
+Goal: enter phase 2 — resource/elemental simulation core so VesselStatus/ResourceSystem can port in
 iteration 4.
 
-1. Port the custom SOAP types from `_Scripts/ScriptableObjects/SOAP/` (16 subdirs:
-   ClassType, CrystalStats, PrismStats, AbilityStats, InputEvents, Quaternion,
-   Transform, PipData, VesselHUDData, SilhouetteData, GameplaySFX, PartyData,
-   FriendData, AuthenticationData, ApplicationState, ScriptableEventWithReturn,
-   + VesselPrefabContainer) into `CosmicShore.Game/ScriptableObjects/SOAP/` —
-   most are thin `ScriptableVariable`/`ScriptableEvent` subclasses + data structs.
-   Port their EditMode tests (PartyInviteDataTests, PartyPlayerDataTests, …).
-2. Port Unity-free utility code: `Utility/DisposableGroup` (+ tests), GeometryUtils
-   (+ tests), `Utility/DataContainers/` pure-logic pieces (SceneNameListSO, simple
-   containers — defer GameDataSO until Player exists).
-3. Port remaining EditMode tests whose subjects now exist (EnumIntegrityTests,
-   EnumIntegrityExtendedTests, EcologyEnumIntegrityTests, ResourceCollectionTests,
-   TrainingGameProgressTests, ShipModifierTests, MainMenuStateTests, CSDebugTests,
-   GameObjectExtensionTests, CellPhaseRulesTests + subject if pure logic).
-4. Extend the CLI smoke with one new section exercising the SOAP custom types
-   (e.g. CrystalStats event round-trip).
+1. Port the remaining pure DataContainer subjects whose EditMode tests exist:
+   GenericDataSO, RuntimeCollectionSO, CameraSettingsSO, XpData, HostConnectionDataSO
+   (defer if it drags UGS types) + their tests verbatim into Tests.Ported.
+2. Port the resource/elemental core from `Controller/Vessel/`: ResourceSystem (+
+   Resource, element-level model `GetLevel` math) and whatever small SO configs it
+   needs (SO_Element …). Port SkimmerAdjustElementLevelByCrystalEffectTests if its
+   subject closure is tractable.
+3. Map the IVessel/IVesselStatus interface closure; port the interfaces once the
+   referenced types exist, then un-defer the 10 SOAP files from iteration 3
+   (VesselImpactor/debuff events, SilhouetteData, VesselHUDData, VesselPrefabContainer)
+   and MainMenuStateTests/GameObjectExtensionTests when their subjects land.
+4. Extend the CLI with a resource/elemental demo (crystal pickup → element level walk).
 5. Update this file (status tables, iteration log, NEXT UP), commit, push.
 
 ## Loop protocol (every iteration)
diff --git a/Port/src/CosmicShore.Cli/Program.cs b/Port/src/CosmicShore.Cli/Program.cs
index 5c8ce5e3d..cd3efc669 100644
--- a/Port/src/CosmicShore.Cli/Program.cs
+++ b/Port/src/CosmicShore.Cli/Program.cs
@@ -40,6 +40,7 @@ namespace CosmicShore.Cli
             RunStateMachineDemo();
             RunSimulationDemo(frames);
```

</details>

### `1bc63a276` — feat(port): standalone single-file binaries — exit pause for double-click + publish recipe

_Claude, 2026-06-11 04:05:34 +0000_

```text
cosmicshore.exe (win-x64, self-contained, ~36 MB) verified running with
no .NET installed. Trimming intentionally off: reflective lifecycle
discovery would be stripped.
```

```text
 Port/PORT_PLAN.md                   |  7 ++++++-
 Port/src/CosmicShore.Cli/Program.cs | 22 ++++++++++++++++++----
 2 files changed, 24 insertions(+), 5 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 62fb0ca82..5fc668048 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -53,7 +53,12 @@ The prompter tests progress without prompting the loop. Contract:
    notification with the exact command (or file) to try. (Annotated `port-mN` tags are
    created locally, but this environment's git proxy only accepts branch pushes — the
    log + commit message are the durable record.)
-5. **Local prerequisites for the prompter** (one-time):
+5. **Standalone binaries on request / at milestones.** No-install executables build with:
+   `dotnet publish src/CosmicShore.Cli -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true`
+   (swap `-r` for `linux-x64` / `osx-arm64`; ~36 MB single file; no trimming — the
+   engine's reflective lifecycle discovery forbids it). The exe holds its console window
+   open when double-clicked (`--no-wait` skips). Delivered into the chat at milestones.
+6. **Local prerequisites for the prompter** (only for running from source):
    `winget install Microsoft.DotNet.SDK.10` (Windows) / `brew install dotnet-sdk` (macOS),
    then `git fetch origin claude/quirky-cannon-sk8a02 && git checkout claude/quirky-cannon-sk8a02`.
 
diff --git a/Port/src/CosmicShore.Cli/Program.cs b/Port/src/CosmicShore.Cli/Program.cs
index cd3efc669..92ea447b0 100644
--- a/Port/src/CosmicShore.Cli/Program.cs
+++ b/Port/src/CosmicShore.Cli/Program.cs
@@ -43,15 +43,29 @@ namespace CosmicShore.Cli
             RunPortedLogicDemo();
 
             Console.WriteLine();
+            int exitCode;
             if (Failures.Count == 0)
             {
                 Console.WriteLine("RESULT: PASS — all engine smoke checks green.");
-                return 0;
+                exitCode = 0;
+            }
+            else
+            {
+                Console.WriteLine($"RESULT: FAIL — {Failures.Count} check(s) failed:");
+                foreach (var failure in Failures) Console.WriteLine($"  ✗ {failure}");
+                exitCode = 1;
+            }
+
+            // Double-clicked .exe on Windows: hold the window open so the transcript is
+            // readable. Scripted/terminal runs with redirected input are unaffected.
+            if (OperatingSystem.IsWindows() && !Console.IsInputRedirected && Array.IndexOf(args, "--no-wait") < 0)
+            {
+                Console.WriteLine();
+                Console.Write("Press Enter to exit...");
+                Console.ReadLine();
             }
 
-            Console.WriteLine($"RESULT: FAIL — {Failures.Count} check(s) failed:");
-            foreach (var failure in Failures) Console.WriteLine($"  ✗ {failure}");
-            return 1;
+            return exitCode;
         }
 
         static void Print(string message)
```

</details>

### `bba243fbf` — feat(port): engine coroutines + ResourceSystem elemental core (iteration 4)

_Claude, 2026-06-11 04:36:05 +0000_

```text
- Engine: CoroutineRunner with the original contract (synchronous first
  step, yield-null next frame, WaitForSeconds on scaled time, nested
  IEnumerator/Coroutine yields, death with owner destroy/deactivate);
  IEnumerator Start auto-runs as coroutine; RequireComponent +
  HideInInspector attributes
- Ported: Resource, ResourceSystem (elemental level math, permanent
  AdjustLevel vs linearly-decaying temporary effects over base levels;
  2 tracked deviations pending the vessel layer), GenericDataSO +
  Int/StringDataSO, RuntimeCollectionSO, CameraSettingsSO, plus their
  3 original EditMode test files verbatim (NUnit)
- CLI section [5]: crystal pickups raise Charge permanently; a
  danger-prism Space debuff decays back without touching base progress

578 tests green (329 xunit + 249 NUnit).
```

```text
 Port/PORT_PLAN.md                                                 |  40 ++++--
 Port/src/CosmicShore.Cli/Program.cs                               |  37 +++++
 Port/src/CosmicShore.Engine/Attributes.cs                         |  12 ++
 Port/src/CosmicShore.Engine/SceneGraph/Coroutines.cs              | 154 ++++++++++++++++++++
 Port/src/CosmicShore.Engine/SceneGraph/GameLoop.cs                |   2 +
 Port/src/CosmicShore.Engine/SceneGraph/LifecycleMethodCache.cs    |   9 ++
 Port/src/CosmicShore.Engine/SceneGraph/MonoBehaviour.cs           |   9 ++
 Port/src/CosmicShore.Game/Controller/Camera/CameraSettingsSO.cs   |  43 ++++++
 Port/src/CosmicShore.Game/Controller/Vessel/Resource.cs           |  34 +++++
 Port/src/CosmicShore.Game/Controller/Vessel/ResourceSystem.cs     | 275 ++++++++++++++++++++++++++++++++++++
 Port/src/CosmicShore.Game/Utility/DataContainers/GenericDataSO.cs |  31 ++++
 Port/src/CosmicShore.Game/Utility/DataContainers/IntDataSO.cs     |  11 ++
 Port/src/CosmicShore.Game/Utility/DataContainers/StringDataSO.cs  |  11 ++
 Port/src/CosmicShore.Game/Utility/RuntimeCollectionSO.cs          |  39 +++++
 Port/tests/CosmicShore.Tests.Ported/CameraSettingsSOTests.cs      | 189 +++++++++++++++++++++++++
 Port/tests/CosmicShore.Tests.Ported/GenericDataSOTests.cs         | 174 +++++++++++++++++++++++
 Port/tests/CosmicShore.Tests.Ported/RuntimeCollectionSOTests.cs   | 180 +++++++++++++++++++++++
 Port/tests/CosmicShore.Tests/ResourceAndCoroutineTests.cs         | 178 +++++++++++++++++++++++
 18 files changed, 1413 insertions(+), 15 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1594 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 5fc668048..ba087b11f 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -167,6 +167,7 @@ The prompter tests progress without prompting the loop. Contract:
 | 6 | **Upstream latent red test reframed**: `ImpactEffects_AllValuesAreUnique` contradicts the shipped enum, which intentionally merges legacy effect groups sharing values 1-8, 10. Values are wire format — port freezes the exact duplicate set instead so NEW collisions still fail. | Enum values untouchable. |
 | 7 | 10 SOAP files deferred pending gameplay types: ScriptableEventVesselImpactor / ExplosionDebuffApplied / SkimmerDebuffApplied (IVessel/IVesselStatus/VesselImpactor), ScriptableSilhouetteData/* (SilhouetteController), ScriptableVesselHUDData/* (MiniGameHUD), VesselPrefabContainer (IVesselStatus). MainMenuStateTests (MainMenuController) and GameObjectExtensionTests (physics types) likewise port with their subjects. | Tracked in NEXT UP. |
 | 8 | Stat structs (CellStats/CrystalStats/PrismStats/AbilityStats), PrismType, and audio category enums are extracted into their own port files (source noted in headers) because their host classes (StatsManager, PrismFactory, AudioSystem) port in later phases. | File-split only; content verbatim. |
+| 9 | ResourceSystem temporary deviations: (a) base class `ElementalShipComponent` → `MonoBehaviour` until IVessel/ElementalFloat port; (b) `[RequireComponent(typeof(IVesselStatus))]` commented until IVesselStatus ports. Class body verbatim. Restore both when the vessel layer lands. | Unblocks the elemental core. |
 
 ## Iteration log
 
@@ -193,24 +194,33 @@ The prompter tests progress without prompting the loop. Contract:
   latent upstream issues (fixed in port, see Deviations #4-6); CLI section [4] (SOAP
   channels + cell phase hysteresis). 528 tests green (322 xunit + 206 NUnit).
 
-## NEXT UP (iteration 4)
+- **Iteration 4** (2026-06-11): phase-2 entry — **engine coroutines** (CoroutineRunner:
+  synchronous first step, yield-null next frame, WaitForSeconds scaled time, nesting,
+  death-with-owner; `IEnumerator Start` auto-runs via LifecycleHooks), RequireComponent/
+  HideInInspector attributes, standalone-binary protocol (win-x64 exe delivered to
+  prompter); ported: Resource, **ResourceSystem** (elemental levels: GetLevel floor math,
+  AdjustLevel, temporary-effect linear decay over base levels — 2 deviations, see #9),
+  GenericDataSO/IntDataSO/StringDataSO, RuntimeCollectionSO, CameraSettingsSO (+ their
+  3 EditMode test files verbatim); xunit coroutine + ResourceSystem suites; CLI section
+  [5] (crystal pickups, danger-prism debuff decay). 578 tests green (329 + 249).
 
-Goal: enter phase 2 — resource/elemental simulation core so VesselStatus/ResourceSystem can port in
+## NEXT UP (iteration 5)
+
+Goal: widen the phase-2 simulation core toward the vessel layer so VesselStatus/ResourceSystem can port in
 iteration 4.
 
-1. Port the remaining pure DataContainer subjects whose EditMode tests exist:
-   GenericDataSO, RuntimeCollectionSO, CameraSettingsSO, XpData, HostConnectionDataSO
-   (defer if it drags UGS types) + their tests verbatim into Tests.Ported.
-2. Port the resource/elemental core from `Controller/Vessel/`: ResourceSystem (+
-   Resource, element-level model `GetLevel` math) and whatever small SO configs it
-   needs (SO_Element …). Port SkimmerAdjustElementLevelByCrystalEffectTests if its
-   subject closure is tractable.
-3. Map the IVessel/IVesselStatus interface closure; port the interfaces once the
-   referenced types exist, then un-defer the 10 SOAP files from iteration 3
-   (VesselImpactor/debuff events, SilhouetteData, VesselHUDData, VesselPrefabContainer)
-   and MainMenuStateTests/GameObjectExtensionTests when their subjects land.
-4. Extend the CLI with a resource/elemental demo (crystal pickup → element level walk).
-5. Update this file (status tables, iteration log, NEXT UP), commit, push.
+1. Port ElementalFloat (locate the real class — `Controller/Vessel/**`), ITransform,
+   then ElementalShipComponent; restore ResourceSystem's base class (deviation #9a).
+2. Begin the IVessel/IVesselStatus closure bottom-up: port the small leaf types they
+   reference (VesselAnimation? Skimmer? — survey first, port what's tractable, stub
+   nothing). Target: interfaces compile so deviation #9b (RequireComponent) and the
+   10 deferred SOAP files can un-defer.
+3. Port XpData + XpDataTests, and survey HostConnectionDataSO for UGS coupling.
+4. Port `Utility/DataContainers/CellPhaseThresholds`-adjacent ecology configs and the
+   prism density/BlockDensityGrid pure-logic pieces if reachable.
+5. Grow the CLI toward an M2 vertical slice: one cell + crystals + 2 scripted vessels
+   exchanging resource/elemental state on a seeded run.
+6. Update this file (status tables, iteration log, NEXT UP), commit, push.
 
 ## Loop protocol (every iteration)
 
diff --git a/Port/src/CosmicShore.Cli/Program.cs b/Port/src/CosmicShore.Cli/Program.cs
index 92ea447b0..981821ae1 100644
--- a/Port/src/CosmicShore.Cli/Program.cs
+++ b/Port/src/CosmicShore.Cli/Program.cs
@@ -41,6 +41,7 @@ namespace CosmicShore.Cli
             RunSimulationDemo(frames);
             RunRoundStatsDemo();
             RunPortedLogicDemo();
+            RunElementalDemo();
 
             Console.WriteLine();
             int exitCode;
@@ -266,5 +267,41 @@ namespace CosmicShore.Cli
             }
             Check(phasesOk, "cell phase transitions follow hysteresis thresholds");
         }
+
+        // ── [5] Ported ResourceSystem: crystals, buffs, decay ────────
+
```

</details>

### `8df8a4108` — feat(port): XpData struct + verbatim XpDataTests; vessel-layer closure mapped

_Claude, 2026-06-11 05:00:21 +0000_

```text
585 tests green. ElementalFloat→IVessel→IVesselStatus closure spans the
full vessel layer — planned as a dedicated arc starting iteration 6.
```

```text
 Port/PORT_PLAN.md                                        |  33 +++++++------
 Port/src/CosmicShore.Game/System/Xp/XpHandler.Structs.cs |  25 ++++++++++
 Port/tests/CosmicShore.Tests.Ported/XpDataTests.cs       | 113 +++++++++++++++++++++++++++++++++++++++++++++
 3 files changed, 158 insertions(+), 13 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 198 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index ba087b11f..0ea6ad1a7 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -204,23 +204,30 @@ The prompter tests progress without prompting the loop. Contract:
   3 EditMode test files verbatim); xunit coroutine + ResourceSystem suites; CLI section
   [5] (crystal pickups, danger-prism debuff decay). 578 tests green (329 + 249).
 
-## NEXT UP (iteration 5)
-
-Goal: widen the phase-2 simulation core toward the vessel layer so VesselStatus/ResourceSystem can port in
+- **Iteration 5** (2026-06-11, short): XpData struct extracted from PlayFab-coupled
+  XpHandler + XpDataTests verbatim (585 tests green: 329 + 256). **Key finding for the
+  next iterations**: ElementalFloat → IVessel → IVesselStatus/IPlayer closure spans the
+  whole vessel layer (~12 classes: AIPilot, Prism, Skimmer, InputController,
+  VesselTransformer, SilhouetteController, VesselPrismController, HUD controllers,
+  action handlers, Material/Pose engine types). Deviation #9 stays open; plan the
+  vessel layer as a dedicated multi-iteration arc (survey → engine Material/Pose →
+  leaf classes → interfaces → restore deviations).
+
+## NEXT UP (iteration 6)
+
+Goal: start the vessel-layer arc so VesselStatus/ResourceSystem can port in
 iteration 4.
 
-1. Port ElementalFloat (locate the real class — `Controller/Vessel/**`), ITransform,
-   then ElementalShipComponent; restore ResourceSystem's base class (deviation #9a).
-2. Begin the IVessel/IVesselStatus closure bottom-up: port the small leaf types they
-   reference (VesselAnimation? Skimmer? — survey first, port what's tractable, stub
-   nothing). Target: interfaces compile so deviation #9b (RequireComponent) and the
-   10 deferred SOAP files can un-defer.
-3. Port XpData + XpDataTests, and survey HostConnectionDataSO for UGS coupling.
-4. Port `Utility/DataContainers/CellPhaseThresholds`-adjacent ecology configs and the
+1. **Vessel-layer arc, step 1 — survey & map**: write `docs/VESSEL_LAYER.md` listing
+   every type in the IVessel/IVesselStatus/IPlayer closure with line counts, Unity
+   deps, and a dependency-ordered porting sequence (leaf classes first). Add engine
+   `Material` (minimal: name + color/float/property store) and `Pose` struct.
+2. Step 2 — port ITransform + the closure's leaf data types per the survey.
+3. Port `Utility/DataContainers/CellPhaseThresholds`-adjacent ecology configs and the
    prism density/BlockDensityGrid pure-logic pieces if reachable.
-5. Grow the CLI toward an M2 vertical slice: one cell + crystals + 2 scripted vessels
+4. Grow the CLI toward an M2 vertical slice: one cell + crystals + 2 scripted vessels
    exchanging resource/elemental state on a seeded run.
-6. Update this file (status tables, iteration log, NEXT UP), commit, push.
+5. Update this file (status tables, iteration log, NEXT UP), commit, push.
 
 ## Loop protocol (every iteration)
 
diff --git a/Port/src/CosmicShore.Game/System/Xp/XpHandler.Structs.cs b/Port/src/CosmicShore.Game/System/Xp/XpHandler.Structs.cs
new file mode 100644
index 000000000..69cbe49da
--- /dev/null
+++ b/Port/src/CosmicShore.Game/System/Xp/XpHandler.Structs.cs
@@ -0,0 +1,25 @@
+// Extracted verbatim from Assets/_Scripts/System/Xp/XpHandler.cs (the XpData struct
+// only). The XpHandler class itself ports in the services phase (PlayFab-coupled).
+namespace CosmicShore.Core
+{
+    /// <summary>
+    /// Captain Xp Data
+    /// Contains Captain class elements - Space, Time, Charge, Mass
+    /// </summary>
+    [System.Serializable]
+    public struct XpData
+    {
+        public int Space;
+        public int Time;
+        public int Charge;
+        public int Mass;
+
+        public XpData(int space, int time, int mass, int charge)
+        {
+            Space = space;
+            Time = time;
+            Mass = mass;
+            Charge = charge;
+        }
+    }
+}
diff --git a/Port/tests/CosmicShore.Tests.Ported/XpDataTests.cs b/Port/tests/CosmicShore.Tests.Ported/XpDataTests.cs
```

</details>

### `9d5429064` — feat(port): engine Material/Shader property store + Pose struct

_Claude, 2026-06-11 05:07:16 +0000_

```text
Vessel-layer arc prerequisites: Material as a pure property store
(clone ctor, Set/Get by name or Shader.PropertyToID) — rendering
interpretation lands in the presentation phase; Pose for spawn points.
```

```text
 Port/src/CosmicShore.Engine/Math/Pose.cs          | 32 ++++++++++++++++++
 Port/src/CosmicShore.Engine/Rendering/Material.cs | 96 +++++++++++++++++++++++++++++++++++++++++++++++++++++
 2 files changed, 128 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 140 lines)</summary>

```diff
diff --git a/Port/src/CosmicShore.Engine/Math/Pose.cs b/Port/src/CosmicShore.Engine/Math/Pose.cs
new file mode 100644
index 000000000..b51af2bb8
--- /dev/null
+++ b/Port/src/CosmicShore.Engine/Math/Pose.cs
@@ -0,0 +1,32 @@
+using System;
+
+namespace CosmicShore.Engine
+{
+    /// <summary>Position + rotation pair (spawn points, teleport targets).</summary>
+    [Serializable]
+    public struct Pose : IEquatable<Pose>
+    {
+        public Vector3 position;
+        public Quaternion rotation;
+
+        public Pose(Vector3 position, Quaternion rotation)
+        {
+            this.position = position;
+            this.rotation = rotation;
+        }
+
+        public static Pose identity => new(Vector3.zero, Quaternion.identity);
+
+        public Vector3 forward => rotation * Vector3.forward;
+        public Vector3 up => rotation * Vector3.up;
+        public Vector3 right => rotation * Vector3.right;
+
+        public bool Equals(Pose other) => position.Equals(other.position) && rotation.Equals(other.rotation);
+        public override bool Equals(object obj) => obj is Pose other && Equals(other);
+        public override int GetHashCode() => HashCode.Combine(position, rotation);
+        public override string ToString() => $"({position}, {rotation})";
+
+        public static bool operator ==(Pose a, Pose b) => a.position == b.position && a.rotation == b.rotation;
+        public static bool operator !=(Pose a, Pose b) => !(a == b);
+    }
+}
diff --git a/Port/src/CosmicShore.Engine/Rendering/Material.cs b/Port/src/CosmicShore.Engine/Rendering/Material.cs
new file mode 100644
index 000000000..16b3ea7d5
--- /dev/null
+++ b/Port/src/CosmicShore.Engine/Rendering/Material.cs
@@ -0,0 +1,96 @@
+using System.Collections.Generic;
+
+namespace CosmicShore.Engine
+{
+    /// <summary>
+    /// Named shader reference with the property-ID registry ported code uses
+    /// (`Shader.PropertyToID` for MaterialPropertyBlock-style access). The actual
+    /// shading backend arrives in the presentation phase.
+    /// </summary>
+    public sealed class Shader : Object
+    {
+        static readonly Dictionary<string, int> PropertyIds = new();
+        static readonly Dictionary<string, Shader> Registry = new();
+
+        Shader(string shaderName) { name = shaderName; }
+
+        public static Shader Find(string name)
+        {
+            if (!Registry.TryGetValue(name, out var shader))
+                Registry[name] = shader = new Shader(name);
+            return shader;
+        }
+
+        /// <summary>Stable per-process numeric ID for a shader property name.</summary>
+        public static int PropertyToID(string name)
+        {
+            if (!PropertyIds.TryGetValue(name, out int id))
+                PropertyIds[name] = id = PropertyIds.Count + 1;
+            return id;
+        }
+    }
+
+    /// <summary>
+    /// Material as a property store (colors/floats/vectors keyed by shader property),
+    /// preserving the API surface gameplay code touches: clone construction, color,
+    /// Set/Get by name or ID. Rendering interpretation arrives in the presentation
```

</details>

### `94ffc01d0` — docs(port): vessel-layer closure survey — 80 types, staged V1-V19 porting sequence

_Claude, 2026-06-11 05:20:25 +0000_

```text
 Port/docs/VESSEL_LAYER.md | 208 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 208 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 214 lines)</summary>

```diff
diff --git a/Port/docs/VESSEL_LAYER.md b/Port/docs/VESSEL_LAYER.md
new file mode 100644
index 000000000..4947e3970
--- /dev/null
+++ b/Port/docs/VESSEL_LAYER.md
@@ -0,0 +1,208 @@
+# Vessel Layer — Type-Closure Survey & Porting Sequence
+
+Iteration-6 survey (PORT_PLAN "NEXT UP" step 1). Maps the full type closure needed to
+port these files **verbatim** and to restore Deviation #9 (ResourceSystem base class +
+`[RequireComponent(typeof(IVesselStatus))]`):
+
+- `Controller/Vessel/IVessel.cs` (64)
+- `Controller/Vessel/IVesselStatus.cs` (139)
+- `Controller/Player/IPlayer.cs` (87)
+- `Controller/Vessel/ElementalFloat.cs` (50)
+- `Controller/Vessel/ElementalVesselComponent.cs` — class `ElementalShipComponent` (32)
+
+All paths below are under `Assets/_Scripts/` unless noted. Dispositions:
+**ALREADY-PORTED** (in `Port/src/`) · **LEAF** (port now, using-swaps only) ·
+**SHALLOW** (port after listed engine additions) · **DEEP** (drags further game
+systems — listed) · **PHASE-LATER** (rendering/audio/input/UI-bound).
+
+**Closure size: ~80 types in 73 files, ≈13,100 lines** (372 target + ≈12,730
+unported closure). Engine `Material` and `Pose` already landed
+(`Engine/Rendering/Material.cs`, `Engine/Math/Pose.cs`) — verified compatible with
+every use site in this closure.
+
+## Already-ported types referenced by the closure
+
+No action needed; listed so the tables below can omit them from "depends on".
+
+| Category | Types |
+|---|---|
+| Engine core | `Transform`, `GameObject`, `Component`, `MonoBehaviour`, `ScriptableObject`, `Object` (fake-null), coroutines + `WaitForSeconds`, `Time`, `Debug`, `Mathf` (incl. `LerpUnclamped`), `Vector2/3`, `Quaternion`, `Color`, **`Material`/`Shader`**, **`Pose`**, `LayerMask`, attributes (`SerializeField/Header/Tooltip/Range/HideInInspector/RequireComponent/FormerlySerializedAs`) |
+| Engine net | `NetworkBehaviour` (`IsSpawned/IsOwner/IsServer/IsClient`, despawn virtuals), `NetworkVariable<T>` (perm-aware) |
+| Engine SOAP | `ScriptableEvent<T>`, `ScriptableEventNoParam/String/Bool/Ulong`, `ScriptableVariable<T>`, `IntVariable`, listeners |
+| Data | `Domains`, `Element`, `InputEvents`, `InputDeviceType`, `ResourceEvents`, `VesselClassType`, `ResourceCollection`, `IRoundStats`, `RoundStats`, `DomainStats`, `ShipThrottleModifier`, `ShipVelocityModifier` |
+| Game | `ResourceSystem` + `Resource` (Deviation #9 pending), `CSDebug`, `DebugExtensions`, `TransformExtensions` (`ResizeForSeconds`/`CancelResize`), `GameObjectExtension` (`GetOrAdd`), `PrismType`, `AbilityStats`, `CameraSettingsSO` + `CameraMode`, `ScriptableEventInputEvents`, `ScriptableEventAbilityStats`, `ScriptableEventPrismStats`, `ScriptableEventTransform`, `PrismEventChannelWithReturnSO`, `VesselClassTypeVariable` |
+
+## Closure table — tier 1 (named directly by the five target files)
+
+| Type | Defining file | Lines | Depends on (Unity / game) | Disposition |
+|---|---|---|---|---|
+| `ITransform` | `Utility/ITransform.cs` | 8 | Transform | **LEAF** |
+| `IInputStatus` | `Controller/IO/IInputStatus.cs` | 55 | `ScreenOrientation` (engine enum needed); InputController, ScriptableEventInputEvents | **SHALLOW** — cyclic with InputController (see SCC note) |
+| `InputController` | `Controller/IO/InputController.cs` | 275 | UnityEngine.InputSystem (`Keyboard/Gamepad/EnhancedTouchSupport`), `Screen/SystemInfo/Application`; IVessel, GameSetting, PauseSystem, all input strategies, MultiMouseService, DeviceOrientationHandler | **SHALLOW** after inert input-device shim |
+| `InputStatus` | `Controller/IO/InputStatus.cs` | 292 | NetworkBehaviour/NetworkVariable (✅); IInputStatus, InputController, ScriptableEventInputEvents | **SHALLOW** |
+| `AIPilot` (+`AIAbility`) | `Controller/AI/AIPilot.cs` | 419 | `Physics.Raycast`/`RaycastHit`/`Debug.DrawLine` (unused-region only), coroutines (✅), `Instantiate(SO)`; GameDataSO, CellRuntimeDataSO, CellItem, ShipActionSO, ActionExecutorRegistry, IVessel/IVesselStatus | **DEEP** — drags GameDataSO + cell substrate |
+| `AICinematicBehavior` | `Utility/DataContainers/AICinematicBehavior.cs` | 265 | Transform; IVesselStatus, AIPilot | **SHALLOW** (after AIPilot) |
+| `Prism` | `Controller/Vessel/Prism.cs` | 484 | MeshRenderer, BoxCollider (stubs); MaterialPropertyAnimator, PrismScaleAnimator, PrismTeamManager, PrismStateManager, Cell, Trail, PrismProperties, PrismAOERegistry, AudioSystem | **DEEP** — prism cluster + cell + audio |
+| `VesselAnimation` | `Controller/Animation/VesselAnimation.cs` | 152 | SkinnedMeshRenderer (`SetBlendShapeWeight`, `materials`); IVesselStatus, IInputStatus, ResourceSystem (✅) | **SHALLOW** after renderer stubs |
+| `VesselCameraCustomizer` | `Controller/Vessel/VesselCameraCustomizer.cs` | 77 | ScriptableEventTransform (✅), CameraSettingsSO (✅); ElementalShipComponent, ICameraController, ICameraConfigurator, CustomCameraController, CameraManager | **DEEP** — CameraManager is Cinemachine-bound (shell, Deviation #12) |
+| `VesselTransformer` | `Controller/Vessel/VesselTransformer.cs` | 518 | Pose (✅); ElementalFloat, ScriptableEventBoostChanged, BoostChangedPayload, SafeLookRotation, VesselAnimation, modifier structs (✅) | **SHALLOW** |
+| `Skimmer` | `Controller/Vessel/Skimmer.cs` | 196 | TransformExtensions (✅), coroutines (✅); ElementalShipComponent, ElementalFloat, Prism, Trail, TrailFollowerDirection, NudgeShard(PoolManager), SafeLookRotation, ScriptableEventString (✅) | **DEEP** — via Prism + pool/audio chain |
+| `SilhouetteController` | `Controller/Vessel/SilhouetteController.cs` | 244 | — ; VesselPrismController, DriftTrailActionExecutor, SilhouetteConfigSO, SilhouetteView, ElementalBarsView, VesselExplosionByCrystalEffectSO, VesselImpactor | **DEEP** — UI views + impact-effects slice (Deviation #13) |
+| `VesselPrismController` | `Controller/Vessel/VesselPrismController.cs` | 375 | GameTask (✅), Material (✅), PrismType (✅); Skimmer, Trail, Prism | **DEEP** — via prism cluster |
+| `IVesselHUDController` | `UI/Interfaces/IVesselHUDController.cs` | 16 | GameObject; IVesselStatus | **LEAF** (also anchors the `CosmicShore.UI` namespace for using-directives) |
+| `VesselCustomization` | `Controller/Vessel/VesselCustomization.cs` | 65 | — ; IVesselStatus, ShipHelper | **SHALLOW** |
+| `R_VesselActionHandler` (+2 mapping structs) | `Controller/Vessel/R_VesselActionHandler.cs` | 321 | `[ServerRpc]`/`[ClientRpc]` attrs (engine), GameTask (✅); ActionExecutorRegistry, ShipActionSO, ShipHelper, NetMarkers, ScriptableEventInputEventBlock, InputEventBlockPayload, AbilityStats (✅) | **SHALLOW** after RPC attrs + profiling shim |
+| `R_ShipElementStatsHandler` (+`ElementStat`) | `Controller/Vessel/R_VesselElementStatsHandler.cs` | 32 | — ; Element (✅) | **LEAF** |
+| `ResourceSystem` | (ported) | — | — | **ALREADY-PORTED** — Deviation #9 restores here |
+
+## Closure table — tier 2/3 (transitive)
+
+| Type | Defining file | Lines | Depends on | Disposition |
+|---|---|---|---|---|
+| `SafeLookRotation` | `Utility/SafeLookRotation.cs` | 47 | DebugExtensions (✅) | **LEAF** |
+| `PauseSystem` | `System/PauseSystem.cs` | 28 | — | **LEAF** |
+| `Singleton<T>` / `SingletonPersistent<T>` | `Utility/Singleton.cs` | 114 | `DontDestroyOnLoad`, `FindFirstObjectByType` (engine) | **SHALLOW** |
+| `BoostChangedPayload` | `UI/View/BoostChangedPayload.cs` | 13 | Domains (✅) | **LEAF** |
+| `ScriptableEventBoostChanged` | `UI/View/ScriptableEventBoostChanged.cs` | 9 | SOAP (✅) | **LEAF** |
+| `InputEventBlockPayload` | `UI/Controller/InputEventBlockPayload.cs` | 15 | InputEvents (✅) | **LEAF** |
+| `ScriptableEventInputEventBlock` | `UI/Controller/ScriptableEventInputEventBlock.cs` | 13 | SOAP (✅) | **LEAF** |
+| `NetMarkers` | `Utility/PerformanceBenchmark/NetMarkers.cs` | 46 | `Unity.Profiling` (engine no-op shim) | **SHALLOW** |
+| `ShipActionSO` | `Controller/Vessel/R_VesselActions/Data Containers/VesselActionSO.cs` | 21 | IVesselStatus, ActionExecutorRegistry | **LEAF** (lands with trio) |
+| `ShipActionExecutorBase` | `Controller/Vessel/R_VesselActions/Executors/VesselActionExecutorBase.cs` | 10 | — | **LEAF** |
+| `ActionExecutorRegistry` | `Controller/Vessel/R_VesselActions/Executors/ActionExecutorRegistry.cs` | 38 | Reflex `[Inject]` (✅); ShipActionExecutorBase | **LEAF** |
+| `ShipAction` (legacy) | `Controller/Vessel/VesselActions/VesselAction.cs` | 23 | ElementalShipComponent, IVessel | **LEAF** (lands with trio; needed by ShipHelper) |
+| `ShipHelper` | `Controller/Vessel/VesselHelper.cs` | 167 | SkinnedMeshRenderer/MeshRenderer (stubs); IVessel/IVesselStatus, ShipActionSO, ShipAction, ThemeManagerDataContainerSO | **SHALLOW** |
```

</details>

### `de12d7bb5` — chore(port): distribute progress build via repo — chat attachments not downloadable

_Claude, 2026-06-11 05:22:49 +0000_

```text
Port/dist is a temporary channel; dropped before merge (squash-merge
never carries the blob).
```

```text
 Port/dist/CosmicShore-Windows.zip | Bin 0 -> 32097786 bytes
 Port/dist/README.txt              |   4 ++++
 2 files changed, 4 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Port/dist/README.txt b/Port/dist/README.txt
new file mode 100644
index 000000000..ef1946ce3
--- /dev/null
+++ b/Port/dist/README.txt
@@ -0,0 +1,4 @@
+Temporary distribution channel: chat attachments proved non-downloadable for
+large binaries, so progress builds are committed here. This folder is dropped
+before any merge to a mainline branch (a squash-merge never carries the blob).
+Build recipe: see PORT_PLAN.md "Progress builds" item 5.
```

</details>

### `54e63522e` — feat(port): SkimRace — first playable, windowed, visual build (sprint S1-S3)

_Claude, 2026-06-11 05:38:10 +0000_

```text
src/CosmicShore.Client on Silk.NET: seeded sum-of-sines track with twin
neon rails + magenta gate rings, golden octahedron crystals with skim
collection bursts, jade dart vessel with banked flight, fading glow
trail ribbon, starfield, seven-segment HUD (timer, crystal count, boost
bar), countdown/finish states. The sim is the ported engine end-to-end:
SkimRaceController is a real MonoBehaviour; boost runs on the ported
ResourceSystem (drain/regen/skim refunds), scoring on ported RoundStats,
HexRace rules (crystal target, time-as-score). Deterministic tracks by
seed. Headless --screenshot mode (Xvfb+Mesa) lets the loop verify
visuals autonomously — artifacts/skimrace_s1.png is rendered by the
actual game (autopilot collected 21/30 at capture).

Windows build: Port/dist/SkimRace-Windows.zip
```

```text
 Port/CosmicShore.slnx                      |   1 +
 Port/PORT_PLAN.md                          |  16 ++
 Port/artifacts/skimrace_s1.png             | Bin 0 -> 68352 bytes
 Port/dist/SkimRace-Windows.zip             | Bin 0 -> 32898385 bytes
 Port/src/CosmicShore.Client/MiniPng.cs     |  97 ++++++++++
 Port/src/CosmicShore.Client/Program.cs     |  40 ++++
 Port/src/CosmicShore.Client/RaceWindow.cs  | 595 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 Port/src/CosmicShore.Client/SkimRaceSim.cs | 192 +++++++++++++++++++
 8 files changed, 941 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 975 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 0ea6ad1a7..d6dde9029 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -78,6 +78,22 @@ The prompter tests progress without prompting the loop. Contract:
   crates.io blocked; npm available (unused).
 - Build: `cd Port && dotnet build && dotnet test` — must be green before every commit.
 
+## SPRINT MODE (prompter directive, 2026-06-11)
+
+> "Sprint toward gameplay, visuals, etc. … /loop until we have a clone of skim race
+> that I can test."
+
+Priorities inverted until **SkimRace** ships: a windowed, flyable crystal-skimming race
+(HexRace rules — steer down a neon track, collect the crystal target, finish time is
+the score) built on the already-ported sim (ResourceSystem, RoundStats, SOAP). Stack:
+`src/CosmicShore.Client` on Silk.NET (MIT: windowing/OpenGL/input), verified headlessly
+here via Xvfb+Mesa screenshots, shipped as win-x64 zips in `Port/dist/`. The verbatim
+fidelity arc (VESSEL_LAYER.md V1-V19, phases 2-8) continues underneath sprint
+iterations — sprint code reuses ported systems wherever they exist and must not fork
+their semantics. SkimRace milestones: **S1** window+starfield+vessel+camera (screenshot
+to prompter), **S2** track+crystals+collection+trail, **S3** HUD+finish+timing = M2/M4
+combined, **S4** polish (glow, palette, gamepad).
+
 ## Phase roadmap
 
 | Phase | Scope | Status |
diff --git a/Port/src/CosmicShore.Client/MiniPng.cs b/Port/src/CosmicShore.Client/MiniPng.cs
new file mode 100644
index 000000000..3dc448698
--- /dev/null
+++ b/Port/src/CosmicShore.Client/MiniPng.cs
@@ -0,0 +1,97 @@
+using System;
+using System.IO;
+using System.IO.Compression;
+
+namespace CosmicShore.Client
+{
+    /// <summary>Minimal PNG encoder (RGBA8) for headless screenshot verification.</summary>
+    public static class MiniPng
+    {
+        public static void Write(string path, byte[] rgba, int width, int height, bool flipY = true)
+        {
+            using var file = File.Create(path);
+            Span<byte> signature = stackalloc byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
+            file.Write(signature);
+
+            // IHDR
+            var ihdr = new byte[13];
+            WriteBE(ihdr, 0, width);
+            WriteBE(ihdr, 4, height);
+            ihdr[8] = 8;  // bit depth
+            ihdr[9] = 6;  // color type RGBA
+            WriteChunk(file, "IHDR", ihdr);
+
+            // IDAT: zlib stream of filter-prefixed rows
+            using (var idat = new MemoryStream())
+            {
+                using (var z = new ZLibStream(idat, CompressionLevel.Fastest, leaveOpen: true))
+                {
+                    int stride = width * 4;
+                    for (int y = 0; y < height; y++)
+                    {
+                        int row = flipY ? height - 1 - y : y;
+                        z.WriteByte(0); // filter: none
+                        z.Write(rgba, row * stride, stride);
+                    }
+                }
+                WriteChunk(file, "IDAT", idat.ToArray());
+            }
+
+            WriteChunk(file, "IEND", Array.Empty<byte>());
+        }
+
+        static void WriteBE(byte[] buffer, int offset, int value)
+        {
+            buffer[offset] = (byte)(value >> 24);
+            buffer[offset + 1] = (byte)(value >> 16);
+            buffer[offset + 2] = (byte)(value >> 8);
```

</details>

### `288f65335` — feat(port): SkimRace rival — contested crystals, overtake AI, rubber-band; gamepad input

_Claude, 2026-06-11 06:09:31 +0000_

```text
Second vessel (ruby dart + ruby trail) flies the identical flight model
with synthesized input: seeks nearest unclaimed crystal, concedes to the
closer pilot and lines up the next, rubber-bands boost and speed by
crystal deficit. Crystals are contested via a shared claim ledger; first
to majority (target/2+1) wins — gold or ruby finish banner with the
winner's time. Gamepad: left stick + trigger/A boost. Known issue noted
in PORT_PLAN: AI-vs-AI demo sweeps; human-vs-AI is the design target.
```

```text
 Port/PORT_PLAN.md                          |   8 ++-
 Port/artifacts/skimrace_s2_rival.png       | Bin 0 -> 105323 bytes
 Port/dist/SkimRace-Windows.zip             | Bin 32898385 -> 32900317 bytes
 Port/src/CosmicShore.Client/RaceWindow.cs  | 175 +++++++++++++++++++++++++++++++++++++++++-------------
 Port/src/CosmicShore.Client/SkimRaceSim.cs | 189 ++++++++++++++++++++++++++++++++++++++++++++---------------
 5 files changed, 282 insertions(+), 90 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 610 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index d6dde9029..f58dc53a4 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -90,9 +90,11 @@ the score) built on the already-ported sim (ResourceSystem, RoundStats, SOAP). S
 here via Xvfb+Mesa screenshots, shipped as win-x64 zips in `Port/dist/`. The verbatim
 fidelity arc (VESSEL_LAYER.md V1-V19, phases 2-8) continues underneath sprint
 iterations — sprint code reuses ported systems wherever they exist and must not fork
-their semantics. SkimRace milestones: **S1** window+starfield+vessel+camera (screenshot
-to prompter), **S2** track+crystals+collection+trail, **S3** HUD+finish+timing = M2/M4
-combined, **S4** polish (glow, palette, gamepad).
+their semantics. SkimRace milestones: **S1-S3 SHIPPED** (windowed race: track/crystals/trail/HUD/finish,
+win-x64 zip in dist). **S4 in progress**: AI rival shipped (contested crystals, overtake
+targeting, rubber-band boost+speed) + gamepad input; KNOWN ISSUE — rival can't beat a
+perfect autopilot in AI-vs-AI demos (sweeps happen); vs humans it contests missed
+crystals. Next: rival balance telemetry, glow/bloom pass, mouse steering, sound.
 
 ## Phase roadmap
 
diff --git a/Port/src/CosmicShore.Client/RaceWindow.cs b/Port/src/CosmicShore.Client/RaceWindow.cs
index cf7f4c796..128f79864 100644
--- a/Port/src/CosmicShore.Client/RaceWindow.cs
+++ b/Port/src/CosmicShore.Client/RaceWindow.cs
@@ -30,6 +30,7 @@ namespace CosmicShore.Client
 
         GameLoop _loop;
         SkimRaceController _race;
+        SkimRaceController _rival;
         readonly PilotInput _pilot = new();
 
         uint _program;
@@ -41,10 +42,13 @@ namespace CosmicShore.Client
         uint _ringVao, _ringVbo; int _ringCount;
         uint _crystalVao, _crystalVbo; int _crystalVertexCount;
         uint _vesselVao, _vesselVbo; int _vesselVertexCount;
+        uint _rivalVao, _rivalVbo; int _rivalVertexCount;
         uint _trailVao, _trailVbo;
+        uint _rivalTrailVao, _rivalTrailVbo;
         uint _hudVao, _hudVbo;
 
         readonly List<(Vector3 pos, Vector3 right)> _trail = new();
+        readonly List<(Vector3 pos, Vector3 right)> _rivalTrail = new();
         const int TrailMax = 110;
 
         Vector3 _camPos, _camLook;
@@ -84,11 +88,17 @@ namespace CosmicShore.Client
                 keyboard.KeyDown += (_, key, _) =>
                 {
                     if (key == Key.Escape) _window.Close();
-                    if (key == Key.R) _pilot.Restart = true;
+                    if (key == Key.R)
+                    {
+                        SkimRaceFactory.ResetRace(_race.Shared, _race, _rival);
+                        _trail.Clear();
+                        _rivalTrail.Clear();
+                    }
                 };
 
-            (_loop, _race) = SkimRaceFactory.Create(_seed, _crystalTarget, _pilot);
+            (_loop, _race, _rival) = SkimRaceFactory.Create(_seed, _crystalTarget, _pilot);
             _race.OnCrystalCollected += (_, pos) => _bursts.Add((pos, 0f));
+            _rival.OnCrystalCollected += (_, pos) => _bursts.Add((pos, 0f));
 
             _program = CompileProgram();
             _uMvp = _gl.GetUniformLocation(_program, "uMvp");
@@ -100,6 +110,9 @@ namespace CosmicShore.Client
             _trailVao = _gl.GenVertexArray();
             _trailVbo = _gl.GenBuffer();
             ConfigureDynamicVao(_trailVao, _trailVbo);
+            _rivalTrailVao = _gl.GenVertexArray();
+            _rivalTrailVbo = _gl.GenBuffer();
+            ConfigureDynamicVao(_rivalTrailVao, _rivalTrailVbo);
             _hudVao = _gl.GenVertexArray();
             _hudVbo = _gl.GenBuffer();
             ConfigureDynamicVao(_hudVao, _hudVbo);
@@ -279,6 +292,12 @@ void main() { frag = vColor; }";
         }
 
         void BuildVesselMesh()
+        {
```

</details>

### `308c6ed39` — feat(port): SkimRace bloom post chain + mouse steering (sprint S4)

_Claude, 2026-06-11 06:34:35 +0000_

```text
Bright-pass → half-res separable gaussian (2x) → additive composite with
soft tonemap; neon now genuinely glows (crystals, vessel, rails, HUD).
Camera-proximity fades: trail ribbons and collection bursts cull/fade
near the lens (bursts spawn on the camera path and bloomed into
screen-filling wedges — caught via headless screenshots). Mouse
steering: hold right button, offset from center. Fresh zip in dist.
```

```text
 Port/artifacts/skimrace_s4_bloom.png      | Bin 0 -> 107555 bytes
 Port/dist/SkimRace-Windows.zip            | Bin 32900317 -> 32902761 bytes
 Port/src/CosmicShore.Client/RaceWindow.cs | 183 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++--
 3 files changed, 179 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 261 lines)</summary>

```diff
diff --git a/Port/src/CosmicShore.Client/RaceWindow.cs b/Port/src/CosmicShore.Client/RaceWindow.cs
index 128f79864..6dace44ed 100644
--- a/Port/src/CosmicShore.Client/RaceWindow.cs
+++ b/Port/src/CosmicShore.Client/RaceWindow.cs
@@ -36,6 +36,13 @@ namespace CosmicShore.Client
         uint _program;
         int _uMvp;
 
+        // post chain: scene FBO → bright → blur ping/pong (half res) → composite
+        uint _postProgram, _blurProgram, _compositeProgram;
+        uint _sceneFbo, _sceneTex, _sceneDepth;
+        uint _pingFbo, _pingTex, _pongFbo, _pongTex;
+        uint _fsVao, _fsVbo;
+        int _fbWidth, _fbHeight;
+
         // geometry
         uint _starVao, _starVbo; int _starCount;
         uint _railVao, _railVbo; int _railCount;
@@ -117,6 +124,12 @@ namespace CosmicShore.Client
             _hudVbo = _gl.GenBuffer();
             ConfigureDynamicVao(_hudVao, _hudVbo);
 
+            _postProgram = CompileProgram(FullscreenVertexSrc, BrightFragmentSrc);
+            _blurProgram = CompileProgram(FullscreenVertexSrc, BlurFragmentSrc);
+            _compositeProgram = CompileProgram(FullscreenVertexSrc, CompositeFragmentSrc);
+            BuildFullscreenTriangle();
+            EnsureRenderTargets();
+
             _gl.Enable(EnableCap.Blend);
             _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One); // additive neon
             _gl.Enable(EnableCap.DepthTest);
@@ -125,9 +138,9 @@ namespace CosmicShore.Client
             _camPos = _race.transform.position - new Vector3(0f, -2.5f, 9f);
         }
 
-        uint CompileProgram()
-        {
-            const string vertexSrc = @"#version 330 core
+        uint CompileProgram() => CompileProgram(MainVertexSrc, MainFragmentSrc);
+
+        const string MainVertexSrc = @"#version 330 core
 layout(location=0) in vec3 aPos;
 layout(location=1) in vec4 aColor;
 uniform mat4 uMvp;
@@ -138,11 +151,61 @@ void main()
     vColor = aColor;
     gl_PointSize = max(1.0, 7.0 / max(gl_Position.w * 0.06, 1.0));
 }";
-            const string fragmentSrc = @"#version 330 core
+        const string MainFragmentSrc = @"#version 330 core
 in vec4 vColor;
 out vec4 frag;
 void main() { frag = vColor; }";
 
+        const string FullscreenVertexSrc = @"#version 330 core
+layout(location=0) in vec2 aPos;
+out vec2 vUv;
+void main() { vUv = aPos * 0.5 + 0.5; gl_Position = vec4(aPos, 0.0, 1.0); }";
+
+        const string BrightFragmentSrc = @"#version 330 core
+in vec2 vUv;
+out vec4 frag;
+uniform sampler2D uTex;
+void main()
+{
+    vec3 c = texture(uTex, vUv).rgb;
+    float lum = dot(c, vec3(0.30, 0.55, 0.15));
+    frag = vec4(c * smoothstep(0.32, 0.75, lum), 1.0);
+}";
+
+        const string BlurFragmentSrc = @"#version 330 core
+in vec2 vUv;
+out vec4 frag;
+uniform sampler2D uTex;
+uniform vec2 uDir; // (1/w,0) or (0,1/h)
+void main()
+{
+    float weights[5] = float[](0.227027, 0.194594, 0.121622, 0.054054, 0.016216);
+    vec3 sum = texture(uTex, vUv).rgb * weights[0];
+    for (int i = 1; i < 5; i++)
```

</details>

### `6f467eaca` — feat(port): procedural audio — engine hum, boost layer, chimes, stingers

_Claude, 2026-06-11 06:38:36 +0000_

```text
AudioEngine synthesizes every clip in code (no asset files): chime with
harmonics + exp decay, countdown/go beeps, 4-note win jingle, lose
slide, seamless hum/boost loops. OpenAL Soft via Silk.NET; construction
is fail-safe (no device → silent mode, verified with ALSOFT_DRIVERS=null
under Xvfb). Hum pitch/gain track speed; boost layer gates on boosting;
rival chimes are quieter and lower. Fresh zip in dist.
```

```text
 Port/PORT_PLAN.md                          |   7 +-
 Port/dist/SkimRace-Windows.zip             | Bin 32902761 -> 32966291 bytes
 Port/src/CosmicShore.Client/AudioEngine.cs | 214 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 Port/src/CosmicShore.Client/RaceWindow.cs  |  23 ++++++-
 4 files changed, 241 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 286 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index f58dc53a4..10bf63231 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -94,7 +94,12 @@ their semantics. SkimRace milestones: **S1-S3 SHIPPED** (windowed race: track/cr
 win-x64 zip in dist). **S4 in progress**: AI rival shipped (contested crystals, overtake
 targeting, rubber-band boost+speed) + gamepad input; KNOWN ISSUE — rival can't beat a
 perfect autopilot in AI-vs-AI demos (sweeps happen); vs humans it contests missed
-crystals. Next: rival balance telemetry, glow/bloom pass, mouse steering, sound.
+crystals. Shipped since: bloom post chain (bright-pass → half-res gaussian → tonemapped
+composite) with camera-proximity fades for trails/bursts; mouse steering; **procedural
+audio** (AudioEngine: synthesized PCM via OpenAL Soft — engine hum scaling with speed,
+boost layer, crystal chimes, countdown/go beeps, win/lose jingle; fail-safe silent mode
+when no device). Next: rival balance from prompter feedback, vessel-layer arc resumes
+(VESSEL_LAYER.md V1), richer vessel/crystal meshes, track variety.
 
 ## Phase roadmap
 
diff --git a/Port/src/CosmicShore.Client/AudioEngine.cs b/Port/src/CosmicShore.Client/AudioEngine.cs
new file mode 100644
index 000000000..e39ad9af3
--- /dev/null
+++ b/Port/src/CosmicShore.Client/AudioEngine.cs
@@ -0,0 +1,214 @@
+using System;
+using Silk.NET.OpenAL;
+
+namespace CosmicShore.Client
+{
+    /// <summary>
+    /// Fully procedural audio: every clip is synthesized PCM (no asset files), played
+    /// through OpenAL Soft. Construction is fail-safe — no audio device (or no native
+    /// lib) just disables the engine silently, the game runs identically.
+    /// </summary>
+    public sealed unsafe class AudioEngine : IDisposable
+    {
+        const int SampleRate = 44100;
+
+        AL _al;
+        ALContext _alc;
+        Device* _device;
+        Context* _context;
+        public bool Enabled { get; private set; }
+
+        uint _humSource, _boostSource;
+        uint _chime, _rivalChime, _beep, _goBeep, _winJingle, _loseTone;
+        readonly uint[] _oneShotSources = new uint[6];
+        int _nextOneShot;
+
+        public AudioEngine(bool disabled = false)
+        {
+            if (disabled) return;
+            try
+            {
+                _alc = ALContext.GetApi(soft: true);
+                _al = AL.GetApi(soft: true);
+                _device = _alc.OpenDevice("");
+                if (_device == null) return;
+                _context = _alc.CreateContext(_device, null);
+                _alc.MakeContextCurrent(_context);
+
+                _chime = MakeBuffer(Chime(880f, 0.35f, 0.5f));
+                _rivalChime = MakeBuffer(Chime(587f, 0.3f, 0.22f));
+                _beep = MakeBuffer(Tone(440f, 0.12f, 0.35f));
+                _goBeep = MakeBuffer(Tone(880f, 0.22f, 0.4f));
+                _winJingle = MakeBuffer(Jingle(new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.16f, 0.45f));
+                _loseTone = MakeBuffer(Slide(392f, 277f, 0.6f, 0.4f));
+
+                _humSource = MakeLoopingSource(MakeBuffer(HumLoop()), 0.0f);
+                _boostSource = MakeLoopingSource(MakeBuffer(BoostLoop()), 0.0f);
+                _al.SourcePlay(_humSource);
+                _al.SourcePlay(_boostSource);
+
+                for (int i = 0; i < _oneShotSources.Length; i++)
+                    _oneShotSources[i] = _al.GenSource();
+
+                Enabled = true;
+            }
+            catch
+            {
```

</details>

### `98d3d4bbf` — feat(port): vessel-layer arc V1 — engine compat layer + seven verbatim ports

_Claude, 2026-06-11 07:07:10 +0000_

```text
Engine: ServerRpc/ClientRpc attribute stubs, Unity.Profiling-equivalent
shim (ProfilerMarker/CounterValue — class not struct, documented),
Screen/SystemInfo/Application/PlayerPrefs(JSON)/Resources registry,
Object.DontDestroyOnLoad/Instantiate (SO+GameObject clone)/
FindFirstObjectByType, Physics.Raycast stub + RaycastHit + Box/Sphere
colliders, Debug.DrawLine/DrawRay, GameObject.GetComponent(Type);
NetworkBehaviour now extends MonoBehaviour (original hierarchy parity).
Ported verbatim: PauseSystem, SafeLookRotation, Singleton family (all 5),
NetMarkers, BoostChangedPayload + ScriptableEventBoostChanged,
InputEventBlockPayload, CellItem; ApplicationLifecycleManager.IsQuitting
extracted with provenance. 585 tests green; SkimRace + CLI verified.
```

```text
 Port/PORT_PLAN.md                                                     |  10 +-
 Port/src/CosmicShore.Engine/Compat/EngineCompat.cs                    | 228 ++++++++++++++++++++++++++++++++
 Port/src/CosmicShore.Engine/Debug.cs                                  |   4 +
 Port/src/CosmicShore.Engine/Networking/NetworkBehaviour.cs            |   2 +-
 Port/src/CosmicShore.Engine/Object.cs                                 |  10 ++
 Port/src/CosmicShore.Engine/Profiling/Profiling.cs                    |  43 ++++++
 Port/src/CosmicShore.Engine/SceneGraph/GameObject.cs                  |   8 ++
 .../Controller/Environment/MiniGameObjects/CellItem.cs                |  30 +++++
 .../CosmicShore.Game/System/ApplicationLifecycleManager.Statics.cs    |  13 ++
 Port/src/CosmicShore.Game/System/PauseSystem.cs                       |  29 ++++
 Port/src/CosmicShore.Game/UI/Controller/InputEventBlockPayload.cs     |  15 +++
 Port/src/CosmicShore.Game/UI/View/BoostChangedPayload.cs              |  13 ++
 Port/src/CosmicShore.Game/UI/View/ScriptableEventBoostChanged.cs      |  10 ++
 Port/src/CosmicShore.Game/Utility/PerformanceBenchmark/NetMarkers.cs  |  46 +++++++
 Port/src/CosmicShore.Game/Utility/SafeLookRotation.cs                 |  47 +++++++
 Port/src/CosmicShore.Game/Utility/Singleton.cs                        | 115 ++++++++++++++++
 16 files changed, 620 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 747 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 10bf63231..109202a2b 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -98,8 +98,14 @@ crystals. Shipped since: bloom post chain (bright-pass → half-res gaussian →
 composite) with camera-proximity fades for trails/bursts; mouse steering; **procedural
 audio** (AudioEngine: synthesized PCM via OpenAL Soft — engine hum scaling with speed,
 boost layer, crystal chimes, countdown/go beeps, win/lose jingle; fail-safe silent mode
-when no device). Next: rival balance from prompter feedback, vessel-layer arc resumes
-(VESSEL_LAYER.md V1), richer vessel/crystal meshes, track variety.
+when no device). Fidelity arc: **V1 DONE** (engine E3-E8/E10: RPC attrs, profiling shim, Screen/
+Application/PlayerPrefs/Resources statics, Object.DontDestroyOnLoad/Instantiate/
+FindFirstObjectByType, Physics+collider stubs; NetworkBehaviour now : MonoBehaviour
+(parity); ported PauseSystem, SafeLookRotation, Singleton family, NetMarkers,
+BoostChangedPayload+event, InputEventBlockPayload, CellItem; ApplicationLifecycleManager
+IsQuitting extraction). Next: V2 (input shim E1 + IInputStatus/IInputStrategy/
+BaseInputStrategy/KeyboardInputStrategy), rival balance from prompter feedback,
+richer meshes/track variety.
 
 ## Phase roadmap
 
diff --git a/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs b/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs
new file mode 100644
index 000000000..b3dc5e962
--- /dev/null
+++ b/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs
@@ -0,0 +1,228 @@
+using System;
+using System.Collections.Generic;
+using System.IO;
+using System.Reflection;
+using System.Text.Json;
+
+namespace CosmicShore.Engine.Networking
+{
+    /// <summary>Marks a server-executed RPC. Local-invoke semantics until the transport phase.</summary>
+    [AttributeUsage(AttributeTargets.Method)]
+    public sealed class ServerRpcAttribute : Attribute { public bool RequireOwnership = true; }
+
+    /// <summary>Marks a client-executed RPC. Local-invoke semantics until the transport phase.</summary>
+    [AttributeUsage(AttributeTargets.Method)]
+    public sealed class ClientRpcAttribute : Attribute { }
+}
+
+namespace CosmicShore.Engine
+{
+    public enum ScreenOrientation { Unknown = 0, Portrait = 1, PortraitUpsideDown = 2, LandscapeLeft = 3, LandscapeRight = 4, AutoRotation = 5 }
+    public enum DeviceType { Unknown = 0, Handheld = 1, Console = 2, Desktop = 3 }
+    public enum RuntimePlatform { WindowsPlayer = 2, OSXPlayer = 1, LinuxPlayer = 13, Android = 11, IPhonePlayer = 8 }
+
+    public static class Screen
+    {
+        public static int width = 1280;
+        public static int height = 720;
+        public static ScreenOrientation orientation = ScreenOrientation.LandscapeLeft;
+        public static bool sleepTimeout;
+    }
+
+    public static class SystemInfo
+    {
+        public static DeviceType deviceType = DeviceType.Desktop;
+    }
+
+    public static class Application
+    {
+        public static bool isPlaying = true;
+        public static bool isMobilePlatform => platform is RuntimePlatform.Android or RuntimePlatform.IPhonePlayer;
+        public static RuntimePlatform platform =
+            OperatingSystem.IsWindows() ? RuntimePlatform.WindowsPlayer :
+            OperatingSystem.IsMacOS() ? RuntimePlatform.OSXPlayer : RuntimePlatform.LinuxPlayer;
+        public static int targetFrameRate = -1;
+        public static string version = "0.2.0-port";
+        public static string persistentDataPath
+        {
+            get
+            {
+                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CosmicShore");
+                Directory.CreateDirectory(path);
+                return path;
+            }
```

</details>

### `d02d30340` — feat(port): vessel-layer arc V2 — input-device shim + IO strategy layer

_Claude, 2026-06-11 07:35:51 +0000_

```text
Engine E1: inert UnityEngine.InputSystem equivalent (Keyboard with full
key set, Gamepad sticks/triggers/buttons, Mouse, AttitudeSensor,
EnhancedTouch no-op) — devices report nothing until phase-5 backends;
.current stays null-able per the original API shape. Ported verbatim:
IInputStatus (Deviation #10a: InputController property + GetGyroRotation
commented until InputController lands at V5), IInputStrategy,
BaseInputStrategy, KeyboardInputStrategy (the live 297-line loose-file
strategy identified by the survey). 585 tests green.
```

```text
 Port/PORT_PLAN.md                                                |   8 +-
 Port/src/CosmicShore.Engine/InputSystem/InputDevices.cs          | 114 ++++++++++++++
 Port/src/CosmicShore.Game/Controller/IO/BaseInputStrategy.cs     |  58 ++++++++
 Port/src/CosmicShore.Game/Controller/IO/IInputStatus.cs          |  58 ++++++++
 Port/src/CosmicShore.Game/Controller/IO/IInputStrategy.cs        |  19 +++
 Port/src/CosmicShore.Game/Controller/IO/KeyboardInputStrategy.cs | 298 +++++++++++++++++++++++++++++++++++++
 6 files changed, 553 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 598 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 109202a2b..d9bc08a91 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -103,8 +103,12 @@ Application/PlayerPrefs/Resources statics, Object.DontDestroyOnLoad/Instantiate/
 FindFirstObjectByType, Physics+collider stubs; NetworkBehaviour now : MonoBehaviour
 (parity); ported PauseSystem, SafeLookRotation, Singleton family, NetMarkers,
 BoostChangedPayload+event, InputEventBlockPayload, CellItem; ApplicationLifecycleManager
-IsQuitting extraction). Next: V2 (input shim E1 + IInputStatus/IInputStrategy/
-BaseInputStrategy/KeyboardInputStrategy), rival balance from prompter feedback,
+IsQuitting extraction). **V2 DONE** (engine E1 inert input-device shim: Keyboard/Gamepad/Mouse/AttitudeSensor/
+EnhancedTouch; ported IInputStatus [Deviation #10a open: InputController property +
+GetGyroRotation commented until V5], IInputStrategy, BaseInputStrategy,
+KeyboardInputStrategy — the live loose-file strategy per the survey). Next: V3
+(TouchInputStrategy + GamepadInputStrategy) + input-layer unit tests (Ease curve,
+inert ProcessInput on a stub IInputStatus), rival balance from prompter feedback,
 richer meshes/track variety.
 
 ## Phase roadmap
diff --git a/Port/src/CosmicShore.Engine/InputSystem/InputDevices.cs b/Port/src/CosmicShore.Engine/InputSystem/InputDevices.cs
new file mode 100644
index 000000000..9d64be353
--- /dev/null
+++ b/Port/src/CosmicShore.Engine/InputSystem/InputDevices.cs
@@ -0,0 +1,114 @@
+namespace CosmicShore.Engine.InputSystem
+{
+    // Inert input-device shim (vessel-layer survey E1): ported strategies compile and
+    // run verbatim; devices report nothing pressed until platform input backends land
+    // (phase 5). The SkimRace client reads Silk.NET directly in the meantime.
+
+    public sealed class KeyControl
+    {
+        public bool isPressed;
+        public bool wasPressedThisFrame;
+        public bool wasReleasedThisFrame;
+    }
+
+    public sealed class Keyboard
+    {
+        /// <summary>Null when no keyboard backend is attached — matches the original API shape.</summary>
+        public static Keyboard current;
+
+        public readonly KeyControl aKey = new(), bKey = new(), cKey = new(), dKey = new(), eKey = new(),
+            nKey = new(), pKey = new(), qKey = new(), rKey = new(), sKey = new(), wKey = new(),
+            spaceKey = new(), quoteKey = new(), leftShiftKey = new(), rightShiftKey = new(),
+            upArrowKey = new(), downArrowKey = new(), leftArrowKey = new(), rightArrowKey = new(),
+            escapeKey = new(), enterKey = new(), tabKey = new(), leftCtrlKey = new(),
+            fKey = new(), gKey = new(), hKey = new(), iKey = new(), jKey = new(), kKey = new(),
+            lKey = new(), mKey = new(), oKey = new(), tKey = new(), uKey = new(), vKey = new(),
+            xKey = new(), yKey = new(), zKey = new(), semicolonKey = new(), commaKey = new(),
+            periodKey = new(), slashKey = new(), leftBracketKey = new(), rightBracketKey = new();
+    }
+
+    public sealed class ButtonControl
+    {
+        public bool isPressed;
+        public bool wasPressedThisFrame;
+        public bool wasReleasedThisFrame;
+        public float value;
+        public float ReadValue() => value;
+    }
+
+    public sealed class StickControl
+    {
+        public Vector2 value;
+        public Vector2 ReadValue() => value;
+    }
+
+    public sealed class Gamepad
+    {
+        public static Gamepad current;
+
+        public readonly StickControl leftStick = new(), rightStick = new();
+        public readonly ButtonControl leftTrigger = new(), rightTrigger = new(),
+            buttonSouth = new(), buttonNorth = new(), buttonEast = new(), buttonWest = new(),
+            leftShoulder = new(), rightShoulder = new(), startButton = new(), selectButton = new();
+    }
+
+    public sealed class MouseButtonControl
```

</details>

### `b2d4f344a` — feat(port): vessel-layer arc V3 — touch + gamepad strategies verbatim

_Claude, 2026-06-11 07:39:10 +0000_

```text
TouchInputStrategy (352L) and GamepadInputStrategy (255L) port with
using-swaps only. Shim additions: Screen.dpi/currentResolution,
full key set, unified TouchPhase (the original shares one enum between
InputSystem and EnhancedTouch namespaces — shim initially diverged,
caught by compile). InputStrategyTests: keyboard/gamepad/touch all run
inert against a stub IInputStatus without faulting; invert toggles write
through. 589 tests green.
```

```text
 Port/PORT_PLAN.md                                               |  10 +-
 Port/src/CosmicShore.Engine/Compat/EngineCompat.cs              |   9 +
 Port/src/CosmicShore.Engine/InputSystem/InputDevices.cs         |   7 +-
 Port/src/CosmicShore.Game/Controller/IO/GamepadInputStrategy.cs | 256 +++++++++++++++++++++++++++
 Port/src/CosmicShore.Game/Controller/IO/TouchInputStrategy.cs   | 352 ++++++++++++++++++++++++++++++++++++++
 Port/tests/CosmicShore.Tests/InputStrategyTests.cs              |  72 ++++++++
 6 files changed, 699 insertions(+), 7 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 771 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index d9bc08a91..aa1042d8f 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -106,10 +106,12 @@ BoostChangedPayload+event, InputEventBlockPayload, CellItem; ApplicationLifecycl
 IsQuitting extraction). **V2 DONE** (engine E1 inert input-device shim: Keyboard/Gamepad/Mouse/AttitudeSensor/
 EnhancedTouch; ported IInputStatus [Deviation #10a open: InputController property +
 GetGyroRotation commented until V5], IInputStrategy, BaseInputStrategy,
-KeyboardInputStrategy — the live loose-file strategy per the survey). Next: V3
-(TouchInputStrategy + GamepadInputStrategy) + input-layer unit tests (Ease curve,
-inert ProcessInput on a stub IInputStatus), rival balance from prompter feedback,
-richer meshes/track variety.
+KeyboardInputStrategy — the live loose-file strategy per the survey). **V3 DONE** (TouchInputStrategy + GamepadInputStrategy verbatim; shim grew Screen.dpi/
+currentResolution + unified TouchPhase — the original shares one enum across
+namespaces; InputStrategyTests: all three strategies run inert without faulting, invert
+toggles write through). Next: V4 (DualMouseInputStrategy, MultiMouseService,
+DeviceOrientationHandler), rival balance from prompter feedback, richer meshes/track
+variety.
 
 ## Phase roadmap
 
diff --git a/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs b/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs
index b3dc5e962..04cb34f7d 100644
--- a/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs
+++ b/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs
@@ -21,12 +21,21 @@ namespace CosmicShore.Engine
     public enum DeviceType { Unknown = 0, Handheld = 1, Console = 2, Desktop = 3 }
     public enum RuntimePlatform { WindowsPlayer = 2, OSXPlayer = 1, LinuxPlayer = 13, Android = 11, IPhonePlayer = 8 }
 
+    public struct Resolution
+    {
+        public int width;
+        public int height;
+        public int refreshRate;
+    }
+
     public static class Screen
     {
         public static int width = 1280;
         public static int height = 720;
+        public static float dpi = 96f;
         public static ScreenOrientation orientation = ScreenOrientation.LandscapeLeft;
         public static bool sleepTimeout;
+        public static Resolution currentResolution => new() { width = width, height = height, refreshRate = 60 };
     }
 
     public static class SystemInfo
diff --git a/Port/src/CosmicShore.Engine/InputSystem/InputDevices.cs b/Port/src/CosmicShore.Engine/InputSystem/InputDevices.cs
index 9d64be353..17c56c9b0 100644
--- a/Port/src/CosmicShore.Engine/InputSystem/InputDevices.cs
+++ b/Port/src/CosmicShore.Engine/InputSystem/InputDevices.cs
@@ -85,6 +85,8 @@ namespace CosmicShore.Engine.InputSystem
         public QuaternionControl attitude = new();
     }
 
+    public enum TouchPhase { None = 0, Began = 1, Moved = 2, Ended = 3, Canceled = 4, Stationary = 5 }
+
     public static class InputSystem
     {
         public static void EnableDevice(object device) { }
@@ -106,9 +108,8 @@ namespace CosmicShore.Engine.InputSystem.EnhancedTouch
     {
         public static readonly System.Collections.Generic.List<Touch> activeTouches = new();
         public Vector2 screenPosition;
-        public TouchPhase phase;
+        // Same enum as the parent namespace — the original API shares one TouchPhase.
+        public CosmicShore.Engine.InputSystem.TouchPhase phase;
         public int touchId;
     }
-
-    public enum TouchPhase { None = 0, Began = 1, Moved = 2, Ended = 3, Canceled = 4, Stationary = 5 }
 }
diff --git a/Port/src/CosmicShore.Game/Controller/IO/GamepadInputStrategy.cs b/Port/src/CosmicShore.Game/Controller/IO/GamepadInputStrategy.cs
new file mode 100644
index 000000000..5b9c92ee0
--- /dev/null
+++ b/Port/src/CosmicShore.Game/Controller/IO/GamepadInputStrategy.cs
@@ -0,0 +1,256 @@
+using CosmicShore.Engine;
+using CosmicShore.Engine.InputSystem;
```

</details>

### `38bd02683` — fix(port): bundle GLFW/OpenAL natives into SkimRace.exe — bare-exe zip failed to load

_Claude, 2026-06-11 14:10:27 +0000_

```text
Single-file publish leaves native libraries beside the exe by default;
the zip shipped only the exe, so window creation failed on user machines.
IncludeNativeLibrariesForSelfExtract=true embeds them; verified by
running the identically-published linux-x64 single binary under Xvfb
(renders, races). Publish recipe in PORT_PLAN corrected.
```

```text
 Port/PORT_PLAN.md              |   6 ++++--
 Port/dist/SkimRace-Windows.zip | Bin 32966291 -> 33613901 bytes
 2 files changed, 4 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index aa1042d8f..3e37922e0 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -53,8 +53,10 @@ The prompter tests progress without prompting the loop. Contract:
    notification with the exact command (or file) to try. (Annotated `port-mN` tags are
    created locally, but this environment's git proxy only accepts branch pushes — the
    log + commit message are the durable record.)
-5. **Standalone binaries on request / at milestones.** No-install executables build with:
-   `dotnet publish src/CosmicShore.Cli -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true`
+5. **Standalone binaries on request / at milestones.** No-install executables build with
+   (NOTE `IncludeNativeLibrariesForSelfExtract` — without it GLFW/OpenAL natives land
+   BESIDE the exe and a bare-exe zip fails to load, which shipped once):
+   `dotnet publish <proj> -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true`
    (swap `-r` for `linux-x64` / `osx-arm64`; ~36 MB single file; no trimming — the
    engine's reflective lifecycle discovery forbids it). The exe holds its console window
    open when double-clicked (`--no-wait` skips). Delivered into the chat at milestones.
```

</details>

### `de0494516` — feat(port): SkimRace startup diagnostics — stage breadcrumbs + crash log file

_Claude, 2026-06-11 14:23:12 +0000_

```text
Console prints [1/4]..[4/4] through window creation/GL init/race start so
a silent failure pinpoints its stage; unhandled exceptions print in full
and write skimrace-crash.txt beside the exe. For the Windows
window-never-appears report.
```

```text
 Port/dist/SkimRace-Windows.zip            | Bin 33613901 -> 33614499 bytes
 Port/src/CosmicShore.Client/Program.cs    |  25 +++++++++++++++++++++++--
 Port/src/CosmicShore.Client/RaceWindow.cs |   4 ++++
 3 files changed, 27 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Port/src/CosmicShore.Client/Program.cs b/Port/src/CosmicShore.Client/Program.cs
index fe7a19b31..904eb60c1 100644
--- a/Port/src/CosmicShore.Client/Program.cs
+++ b/Port/src/CosmicShore.Client/Program.cs
@@ -33,8 +33,29 @@ namespace CosmicShore.Client
             Console.WriteLine($"SkimRace — seed {seed}, crystal target {crystals}");
             Console.WriteLine("WASD/arrows steer · Space boost · R restart · Esc quit");
 
-            new RaceWindow(seed, crystals, screenshot, screenshotFrame).Run();
-            return 0;
+            try
+            {
+                new RaceWindow(seed, crystals, screenshot, screenshotFrame).Run();
+                return 0;
+            }
+            catch (Exception e)
+            {
+                Console.WriteLine();
+                Console.WriteLine("CRASH — please send this text (or skimrace-crash.txt next to the exe):");
+                Console.WriteLine(e);
+                try
+                {
+                    string dir = System.IO.Path.GetDirectoryName(Environment.ProcessPath) ?? ".";
+                    System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "skimrace-crash.txt"), e.ToString());
+                }
+                catch { /* log location unavailable — console output still shows it */ }
+                if (OperatingSystem.IsWindows() && !Console.IsInputRedirected)
+                {
+                    Console.Write("Press Enter to exit...");
+                    Console.ReadLine();
+                }
+                return 2;
+            }
         }
     }
 }
diff --git a/Port/src/CosmicShore.Client/RaceWindow.cs b/Port/src/CosmicShore.Client/RaceWindow.cs
index 5ffca9780..77478f477 100644
--- a/Port/src/CosmicShore.Client/RaceWindow.cs
+++ b/Port/src/CosmicShore.Client/RaceWindow.cs
@@ -81,10 +81,12 @@ namespace CosmicShore.Client
                 Title = "Cosmic Shore — SkimRace (port progress build)",
                 VSync = true,
             };
+            Console.WriteLine("[1/4] creating window (GLFW)...");
             _window = Window.Create(options);
             _window.Load += OnLoad;
             _window.Update += OnUpdate;
             _window.Render += OnRender;
+            Console.WriteLine("[2/4] entering run loop...");
             _window.Run();
         }
 
@@ -92,6 +94,7 @@ namespace CosmicShore.Client
 
         void OnLoad()
         {
+            Console.WriteLine("[3/4] window open — initializing GL/audio/scene...");
             _gl = GL.GetApi(_window);
             _inputContext = _window.CreateInput();
             foreach (var keyboard in _inputContext.Keyboards)
@@ -140,6 +143,7 @@ namespace CosmicShore.Client
             _gl.Enable(EnableCap.ProgramPointSize);
 
             _camPos = _race.transform.position - new Vector3(0f, -2.5f, 9f);
+            Console.WriteLine("[4/4] ready — racing. (If the game window isn't visible now, check the taskbar.)");
         }
 
         uint CompileProgram() => CompileProgram(MainVertexSrc, MainFragmentSrc);
```

</details>

### `f3a04260a` — fix(port): SkimRace Windows zip — VC++-free GLFW + natives beside exe

_Claude, 2026-06-11 14:40:58 +0000_

```text
User crash 'GlfwPlatform - not applicable': Silk's glfw3.dll imports
VCRUNTIME140.dll (VC++ redist), absent on the user's machine, so the
native failed to load and no window platform existed. Ship the official
GLFW 3.4 MinGW-w64 build instead (imports only universal system DLLs,
verified via PE import table) placed beside the exe (no temp extraction).
soft_oal stays VC-dependent but AudioEngine fail-safes to silent.
```

```text
 Port/dist/SkimRace-Windows.zip | Bin 33614499 -> 33650470 bytes
 1 file changed, 0 insertions(+), 0 deletions(-)
```

### `d492c846f` — feat(port): authentic Cosmic Shore controls in SkimRace — ported strategy + VesselTransformer parity

_Claude, 2026-06-11 14:58:01 +0000_

```text
Player input now flows Silk.NET → engine input shim → the PORTED
GamepadInputStrategy (verbatim dual-stick scheme: XSum/YSum pitch/yaw
sums, YDiff roll difference, XDiff stick-spread throttle, eased curves,
Button1 boost via the authentic event channel). Flight model rewritten
to VesselTransformer parity: free AngleAxis rotation about the vessel's
own axes (130/130/130 scalers), speed = XDiff*50*boost + 10 lerped at
1.5 — replaces the sprint Euler model and the prompter-reported
single-stick/inverted-yaw feel. AI + screenshot autopilot drive the same
IInputStatus. Keyboard = WASD/arrows as left/right sticks.
```

```text
 Port/PORT_PLAN.md                              |  10 +++-
 Port/dist/SkimRace-Windows.zip                 | Bin 33650470 -> 33652613 bytes
 Port/src/CosmicShore.Client/RaceWindow.cs      | 152 ++++++++++++++++++++++++++++++-------------------------
 Port/src/CosmicShore.Client/SkimInputStatus.cs |  57 +++++++++++++++++++++
 Port/src/CosmicShore.Client/SkimRaceSim.cs     |  98 ++++++++++++++++++++---------------
 5 files changed, 206 insertions(+), 111 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 484 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 3e37922e0..c5caf30c4 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -100,7 +100,15 @@ crystals. Shipped since: bloom post chain (bright-pass → half-res gaussian →
 composite) with camera-proximity fades for trails/bursts; mouse steering; **procedural
 audio** (AudioEngine: synthesized PCM via OpenAL Soft — engine hum scaling with speed,
 boost layer, crystal chimes, countdown/go beeps, win/lose jingle; fail-safe silent mode
-when no device). Fidelity arc: **V1 DONE** (engine E3-E8/E10: RPC attrs, profiling shim, Screen/
+when no device). **FIRST HUMAN FLIGHT 2026-06-11** (prompter, gamepad). Feedback applied same-day:
+SkimRace now uses the AUTHENTIC control scheme — the ported GamepadInputStrategy runs
+against a Silk→shim device bridge (XSum/YSum/XDiff/YDiff: dual-stick sums = pitch/yaw,
+difference = roll, stick spread = throttle), and the flight model is VesselTransformer
+parity (free AngleAxis rotation about own axes, scalers 130/130/130, throttle 50·XDiff
++ 10 min speed, LERP 1.5, boost as throttle multiplier). Keyboard fallback: WASD+arrows
+as two sticks. NEXT (prompter): Squirrel vessel model — binary FBX at
+Assets/_Models/Vessel Models/SquirrelVessel_CosmicShoresTest1.fbx; write a focused FBX
+geometry extractor (verts+indices → baked mesh) next tick. Fidelity arc: **V1 DONE** (engine E3-E8/E10: RPC attrs, profiling shim, Screen/
 Application/PlayerPrefs/Resources statics, Object.DontDestroyOnLoad/Instantiate/
 FindFirstObjectByType, Physics+collider stubs; NetworkBehaviour now : MonoBehaviour
 (parity); ported PauseSystem, SafeLookRotation, Singleton family, NetMarkers,
diff --git a/Port/src/CosmicShore.Client/RaceWindow.cs b/Port/src/CosmicShore.Client/RaceWindow.cs
index 77478f477..ad7c57c11 100644
--- a/Port/src/CosmicShore.Client/RaceWindow.cs
+++ b/Port/src/CosmicShore.Client/RaceWindow.cs
@@ -6,8 +6,12 @@ using Silk.NET.Input;
 using Silk.NET.Maths;
 using Silk.NET.OpenGL;
 using Silk.NET.Windowing;
+using CosmicShore.Data;
+using CosmicShore.Gameplay;
+using EngineInput = CosmicShore.Engine.InputSystem;
 using Vector3 = CosmicShore.Engine.Vector3;
 using Quaternion = CosmicShore.Engine.Quaternion;
+using Vector2 = CosmicShore.Engine.Vector2;
 
 namespace CosmicShore.Client
 {
@@ -31,7 +35,10 @@ namespace CosmicShore.Client
         GameLoop _loop;
         SkimRaceController _race;
         SkimRaceController _rival;
-        readonly PilotInput _pilot = new();
+        readonly SkimInputStatus _playerStatus = new();
+        readonly GamepadInputStrategy _gamepadStrategy = new(); // the ported, authentic dual-stick scheme
+        EngineInput.Gamepad _shimPad;
+        bool _prevA, _prevStart;
 
         uint _program;
         int _uMvp;
@@ -109,7 +116,9 @@ namespace CosmicShore.Client
                     }
                 };
 
-            (_loop, _race, _rival) = SkimRaceFactory.Create(_seed, _crystalTarget, _pilot);
+            (_loop, _race, _rival) = SkimRaceFactory.Create(_seed, _crystalTarget, _playerStatus);
+            _gamepadStrategy.Initialize(_playerStatus);
+            _gamepadStrategy.OnStrategyActivated();
             _audio = new AudioEngine(disabled: _screenshotPath != null);
             _race.OnCrystalCollected += (_, pos) => { _bursts.Add((pos, 0f)); _audio.CrystalChime(player: true); };
             _rival.OnCrystalCollected += (_, pos) => { _bursts.Add((pos, 0f)); _audio.CrystalChime(player: false); };
@@ -406,79 +415,81 @@ void main()
 
         void OnUpdate(double dt)
         {
-            // keyboard → pilot intent
-            float pitch = 0f, yaw = 0f;
-            bool boost = false;
-            foreach (var keyboard in _inputContext.Keyboards)
-            {
-                if (keyboard.IsKeyPressed(Key.W) || keyboard.IsKeyPressed(Key.Up)) pitch += 1f;
-                if (keyboard.IsKeyPressed(Key.S) || keyboard.IsKeyPressed(Key.Down)) pitch -= 1f;
-                if (keyboard.IsKeyPressed(Key.A) || keyboard.IsKeyPressed(Key.Left)) yaw -= 1f;
-                if (keyboard.IsKeyPressed(Key.D) || keyboard.IsKeyPressed(Key.Right)) yaw += 1f;
-                if (keyboard.IsKeyPressed(Key.Space)) boost = true;
-            }
-            foreach (var mouse in _inputContext.Mice)
+            if (_screenshotPath != null) return; // sim + autopilot tick in OnRender
+
+            ApplyHumanInput();
```

</details>

### `c597fd472` — docs(port): session resume protocol + fresh-container setup script

_Claude, 2026-06-11 15:03:55 +0000_

```text
 Port/PORT_PLAN.md | 13 +++++++++++++
 Port/setup.sh     | 14 ++++++++++++++
 2 files changed, 27 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index c5caf30c4..7b13f9128 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -73,6 +73,19 @@ The prompter tests progress without prompting the loop. Contract:
 | `port-m3` | First rendered frame (PNG artifact in chat + repo) | pull + open artifact | ⬜ |
 | `port-m4` | First interactive desktop build | `… -- --render` | ⬜ |
 
+## Session resume protocol (fresh session / fresh container)
+
+The loop's memory is THIS FILE plus the branch — no chat context is load-bearing.
+To resume in a brand-new session, the prompter pastes exactly:
+
+> /loop run Port/setup.sh if the toolchain is missing, then continue the Cosmic Shore
+> port per Port/PORT_PLAN.md: SPRINT MODE (SkimRace toward full Cosmic Shore parity)
+> + the fidelity arc (docs/VESSEL_LAYER.md). Keep dotnet test green, update this plan,
+> commit and push to claude/quirky-cannon-sk8a02, ship playable zips to Port/dist.
+
+That re-arms the heartbeat, rebuilds the toolchain (~3-5 min via setup.sh), and picks
+up at NEXT UP. Mid-iteration work is never stranded: every iteration ends pushed.
+
 ## Toolchain (re-verify each fresh container)
 
 - .NET 10 SDK at `/opt/dotnet` (installed via `dotnet-install.sh --channel LTS`;
diff --git a/Port/setup.sh b/Port/setup.sh
new file mode 100755
index 000000000..3517a4a72
--- /dev/null
+++ b/Port/setup.sh
@@ -0,0 +1,14 @@
+#!/usr/bin/env bash
+# Fresh-container bootstrap for the Cosmic Shore port loop (see PORT_PLAN.md).
+set -e
+if ! command -v dotnet >/dev/null 2>&1 && [ ! -x /opt/dotnet/dotnet ]; then
+  echo "installing .NET SDK (LTS)..."
+  curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel LTS --install-dir /opt/dotnet
+  grep -q '/opt/dotnet' ~/.bashrc || echo 'export PATH=/opt/dotnet:$PATH' >> ~/.bashrc
+fi
+export PATH=/opt/dotnet:$PATH
+echo "installing headless GL (screenshot verification)..."
+apt-get install -y --no-install-recommends xvfb libgl1 libglx-mesa0 libgl1-mesa-dri libglfw3 >/dev/null 2>&1 || true
+cd "$(dirname "$0")"
+dotnet build && dotnet test
+echo "Port toolchain ready — read PORT_PLAN.md 'NEXT UP' and continue the loop."
```

</details>

### `765922134` — feat(port): the Squirrel's real hull + inverted gamepad yaw

_Claude, 2026-06-11 19:13:41 +0000_

```text
First-party FBX geometry extraction (binary FBX 7.4 node parser:
zlib property arrays, polygon fan-triangulation, flat face normals) →
960KB embedded mesh, axis-remapped to game space, normalized to dart
scale, baked two-light flat shading in pilot palettes at load. Gamepad
yaw inverted after the ported strategy runs (prompter preference);
throttle/roll semantics untouched. 589 tests green.
```

```text
 Port/PORT_PLAN.md                                |   9 ++++++---
 Port/artifacts/skimrace_squirrel.png             | Bin 0 -> 120291 bytes
 Port/dist/SkimRace-Windows.zip                   | Bin 33652613 -> 33958350 bytes
 Port/src/CosmicShore.Client/Assets/squirrel.mesh | Bin 0 -> 983524 bytes
 Port/src/CosmicShore.Client/RaceWindow.cs        |  52 ++++++++++++++++++++++++++++++++++++++++++++++++++--
 5 files changed, 56 insertions(+), 5 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 90 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 7b13f9128..56ebd966a 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -119,9 +119,12 @@ against a Silk→shim device bridge (XSum/YSum/XDiff/YDiff: dual-stick sums = pi
 difference = roll, stick spread = throttle), and the flight model is VesselTransformer
 parity (free AngleAxis rotation about own axes, scalers 130/130/130, throttle 50·XDiff
 + 10 min speed, LERP 1.5, boost as throttle multiplier). Keyboard fallback: WASD+arrows
-as two sticks. NEXT (prompter): Squirrel vessel model — binary FBX at
-Assets/_Models/Vessel Models/SquirrelVessel_CosmicShoresTest1.fbx; write a focused FBX
-geometry extractor (verts+indices → baked mesh) next tick. Fidelity arc: **V1 DONE** (engine E3-E8/E10: RPC attrs, profiling shim, Screen/
+as two sticks. **SQUIRREL HULL SHIPPED**: first-party binary-FBX extractor (Python, in-session) parsed
+SquirrelVessel_CosmicShoresTest1.fbx (13,660 tris) → axis-remapped/normalized →
+Assets/squirrel.mesh embedded resource → flat-lit at load in jade/ruby palettes; dart
+remains fallback. **Gamepad yaw inverted** post-strategy (prompter preference; XDiff/
+YDiff semantics untouched). NEXT: prompter verifies Squirrel nose orientation in motion
+(flip is one sign in the extractor), drift/trail feel, rival tuning. Fidelity arc: **V1 DONE** (engine E3-E8/E10: RPC attrs, profiling shim, Screen/
 Application/PlayerPrefs/Resources statics, Object.DontDestroyOnLoad/Instantiate/
 FindFirstObjectByType, Physics+collider stubs; NetworkBehaviour now : MonoBehaviour
 (parity); ported PauseSystem, SafeLookRotation, Singleton family, NetMarkers,
diff --git a/Port/src/CosmicShore.Client/RaceWindow.cs b/Port/src/CosmicShore.Client/RaceWindow.cs
index ad7c57c11..0427cb28f 100644
--- a/Port/src/CosmicShore.Client/RaceWindow.cs
+++ b/Port/src/CosmicShore.Client/RaceWindow.cs
@@ -373,8 +373,51 @@ void main()
 
         void BuildVesselMesh()
         {
-            (_vesselVao, _vesselVbo, _vesselVertexCount) = BuildDart(jade: true);
-            (_rivalVao, _rivalVbo, _rivalVertexCount) = BuildDart(jade: false);
+            // The Squirrel's real hull, extracted from the game's FBX and baked with
+            // flat per-face lighting in the pilot palette. Dart stays as fallback.
+            try
+            {
+                (_vesselVao, _vesselVbo, _vesselVertexCount) = BuildSquirrel(jade: true);
+                (_rivalVao, _rivalVbo, _rivalVertexCount) = BuildSquirrel(jade: false);
+            }
+            catch (Exception e)
+            {
+                Console.WriteLine($"squirrel mesh unavailable ({e.Message}) — dart fallback");
+                (_vesselVao, _vesselVbo, _vesselVertexCount) = BuildDart(jade: true);
+                (_rivalVao, _rivalVbo, _rivalVertexCount) = BuildDart(jade: false);
+            }
+        }
+
+        (uint vao, uint vbo, int count) BuildSquirrel(bool jade)
+        {
+            using var stream = typeof(RaceWindow).Assembly
+                .GetManifestResourceStream("CosmicShore.Client.Assets.squirrel.mesh")
+                ?? throw new InvalidOperationException("embedded squirrel.mesh missing");
+            using var reader = new System.IO.BinaryReader(stream);
+            int triCount = reader.ReadInt32();
+
+            // two-light flat shade in the pilot palette, emissive floor for the neon look
+            var keyLight = new Vector3(0.42f, 0.78f, -0.46f).normalized;
+            var rimLight = new Vector3(-0.3f, -0.2f, 0.93f).normalized;
+            (float r, float g, float b) baseColor = jade ? (0.07f, 1f, 0.62f) : (1f, 0.17f, 0.32f);
+
+            var dataList = new List<float>(triCount * 3 * 7);
+            for (int t = 0; t < triCount; t++)
+            {
+                for (int v = 0; v < 3; v++)
+                {
+                    float px = reader.ReadSingle(), py = reader.ReadSingle(), pz = reader.ReadSingle();
+                    float nx = reader.ReadSingle(), ny = reader.ReadSingle(), nz = reader.ReadSingle();
+                    var normal = new Vector3(nx, ny, nz);
+                    float key = MathF.Max(0f, Vector3.Dot(normal, keyLight));
+                    float rim = MathF.Max(0f, Vector3.Dot(normal, rimLight));
+                    float shade = 0.22f + 0.62f * key + 0.3f * rim;
+                    Push(dataList, new Vector3(px, py, pz),
+                        baseColor.r * shade, baseColor.g * shade, baseColor.b * shade, 0.95f);
+                }
+            }
+            var (vao, vbo) = UploadStatic(dataList.ToArray());
+            return (vao, vbo, triCount * 3);
         }
 
         (uint vao, uint vbo, int count) BuildDart(bool jade)
@@ -464,6 +507,11 @@ void main()
                 _prevStart = start;
```

</details>

### `c76078600` — feat(port): play-latest.bat — one-click pull/extract/launch test loop

_Claude, 2026-06-11 19:20:17 +0000_

```text
 Port/PORT_PLAN.md    |  8 +++++++-
 Port/play-latest.bat | 12 ++++++++++++
 2 files changed, 19 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 56ebd966a..c5543f1d9 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -60,7 +60,13 @@ The prompter tests progress without prompting the loop. Contract:
    (swap `-r` for `linux-x64` / `osx-arm64`; ~36 MB single file; no trimming — the
    engine's reflective lifecycle discovery forbids it). The exe holds its console window
    open when double-clicked (`--no-wait` skips). Delivered into the chat at milestones.
-6. **Local prerequisites for the prompter** (only for running from source):
+6. **Fast test loop for the prompter** (replaces manual zip downloads):
+   one-time `git clone` + checkout `claude/quirky-cannon-sk8a02`, then double-click
+   `Port/play-latest.bat` after each push — it pulls, extracts dist, and launches
+   (~10 s). Developer alternative: install the .NET 10 SDK + VC++ redist once, then
+   `git pull && dotnet run --project Port/src/CosmicShore.Client` builds and runs
+   from source incrementally.
+7. **Local prerequisites for the prompter** (only for running from source):
    `winget install Microsoft.DotNet.SDK.10` (Windows) / `brew install dotnet-sdk` (macOS),
    then `git fetch origin claude/quirky-cannon-sk8a02 && git checkout claude/quirky-cannon-sk8a02`.
 
```

</details>

### `9287afc47` — chore(port): gitattributes — CRLF for .bat, binary for .mesh

_Claude, 2026-06-11 19:20:36 +0000_

```text
 Port/.gitattributes | 2 ++
 1 file changed, 2 insertions(+)
```

### `92456566f` — feat(port): run-source.bat — one-click pull/build/run from source

_Claude, 2026-06-11 19:23:14 +0000_

```text
 Port/run-source.bat | 8 ++++++++
 1 file changed, 8 insertions(+)
```

### `06974f0d8` — fix(port): re-sync ports after ecology merge — CellPhase ladder 6→3

_Claude, 2026-06-11 19:33:58 +0000_

```text
The brahmagupta merge collapsed CellPhase to None/Calm/Restless/Frenzy
and reshaped CellPhaseThresholds; re-ported CellPhase,
CellAggressionLevel, CellPhaseThresholds(+Rules) and the four ecology/
enum EditMode test files verbatim from the merged sources. Updated the
port-side freeze tests + CLI phase walk to the new ladder. Re-applied
the two documented test deviations the verbatim refresh overwrote
(upstream GameModes count now stale at 33 vs actual 35; ImpactEffects
duplicate-set freeze). 582 tests green; CLI + SkimRace verified.
```

```text
 Port/src/CosmicShore.Cli/Program.cs                                   | 14 +++--
 Port/src/CosmicShore.Data/Enums/CellAggressionLevel.cs                | 10 ++--
 Port/src/CosmicShore.Data/Enums/CellPhase.cs                          | 24 ++++++---
 .../CosmicShore.Game/Utility/DataContainers/CellPhaseThresholds.cs    | 56 +++++++-------------
 Port/tests/CosmicShore.Tests.Ported/CellPhaseRulesTests.cs            | 94 ++++++++++++++-------------------
 Port/tests/CosmicShore.Tests.Ported/EcologyEnumIntegrityTests.cs      | 33 ++++++------
 Port/tests/CosmicShore.Tests.Ported/EnumIntegrityExtendedTests.cs     |  9 ++--
 Port/tests/CosmicShore.Tests.Ported/EnumIntegrityTests.cs             |  5 +-
 Port/tests/CosmicShore.Tests/EnumFreezeTests.cs                       |  9 ++--
 9 files changed, 115 insertions(+), 139 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 503 lines)</summary>

```diff
diff --git a/Port/src/CosmicShore.Cli/Program.cs b/Port/src/CosmicShore.Cli/Program.cs
index 981821ae1..4e985eafd 100644
--- a/Port/src/CosmicShore.Cli/Program.cs
+++ b/Port/src/CosmicShore.Cli/Program.cs
@@ -247,16 +247,14 @@ namespace CosmicShore.Cli
             // Cell phase walk (ported CellPhaseRules + default thresholds): climb with the
             // prism count, hold inside the hysteresis band, multi-step descent in one call.
             var thresholds = Utility.CellPhaseThresholds.Default;
-            var phase = CellPhase.Sprout;
+            var phase = CellPhase.Calm;
             var walk = new (int count, CellPhase expected)[]
             {
-                (0, CellPhase.Sprout),
-                (1200, CellPhase.Quiet),
-                (5000, CellPhase.Settled),
-                (9000, CellPhase.Restless),
-                (12000, CellPhase.Frozen),
-                (9400, CellPhase.Restless),   // fell below FrozenExit, holds above RestlessExit
-                (700, CellPhase.Sprout),      // collapse: multi-step descent resolves at once
+                (0, CellPhase.Calm),
+                (9000, CellPhase.Restless),   // ≥ RestlessEnter (8000)
+                (16000, CellPhase.Frenzy),    // ≥ FrenzyEnter (15000)
+                (14500, CellPhase.Frenzy),    // hysteresis band: holds above FrenzyExit (14000)
+                (700, CellPhase.Calm),        // collapse: multi-step descent resolves at once
             };
             bool phasesOk = true;
             foreach (var (count, expected) in walk)
diff --git a/Port/src/CosmicShore.Data/Enums/CellAggressionLevel.cs b/Port/src/CosmicShore.Data/Enums/CellAggressionLevel.cs
index c08048533..b2a389623 100644
--- a/Port/src/CosmicShore.Data/Enums/CellAggressionLevel.cs
+++ b/Port/src/CosmicShore.Data/Enums/CellAggressionLevel.cs
@@ -3,10 +3,12 @@ namespace CosmicShore.Data
     // Always assign static numeric values; Unity serialization drift on enum reordering
     // breaks scene-wired NetworkVariables and SOAP asset references silently.
     //
-    // Fauna aggression state within a Cell, derived from the cell's CellPhase.
-    // Separately, the Cell also regulates flora planting and growing via independent
-    // phase gates — these do not share levels because the user spec staggers flora
-    // and fauna events along a single prism-count axis.
+    // Fauna aggression state within a Cell. Since the 3-phase collapse this maps 1:1
+    // onto CellPhase (Calm→Level0, Restless→Level1, Frenzy→Level2) — the phase IS the
+    // aggression band. The enum is kept distinct only because it indexes the per-level
+    // multiplier arrays (cadence / consume-radius / speed) as a clean 0/1/2 index.
+    // Flora are no longer staggered on separate phase rungs: they grow + plant at a
+    // steady rate until Frenzy, so there is one ladder now, not two. See ECOSYSTEM.md §0.
     //
     // Level behaviors:
     //   Level0 - Fauna head toward the cell's crystal; normal cleanup cadence and avoidance.
diff --git a/Port/src/CosmicShore.Data/Enums/CellPhase.cs b/Port/src/CosmicShore.Data/Enums/CellPhase.cs
index 6b46734f0..12d08be59 100644
--- a/Port/src/CosmicShore.Data/Enums/CellPhase.cs
+++ b/Port/src/CosmicShore.Data/Enums/CellPhase.cs
@@ -2,14 +2,26 @@ namespace CosmicShore.Data
 {
     // Always assign static numeric values; Unity serialization drift on enum reordering
     // breaks scene-wired NetworkVariables and SOAP asset references silently.
+    //
+    // The cell's ecological state along its single state axis — live prism count
+    // (Docs/ECOSYSTEM.md §1). There are exactly THREE active phases, and they map 1:1
+    // onto the fauna aggression bands (CellAggressionLevel): the phase IS the aggression
+    // band. (Earlier there were six rungs because flora-events and fauna-events were
+    // staggered along separate thresholds; flora now grow + plant at a steady rate all
+    // the way to Frenzy — the food web is the only down-force — so the staggering, and
+    // the extra rungs that staged it, are gone. See ECOSYSTEM.md §0/§5.)
+    //
+    //   Calm    — low mass. Flora grow + plant freely; fauna idle toward the crystal (L0).
+    //   Restless — mid mass. Fauna hunt the nearest opposing-color centroid (L1).
+    //   Frenzy  — frenzy ceiling. Flora STOP; fauna seek any-domain density, drop friendly
+    //             avoidance, and ignore danger prisms (L2). A cell only leaves Frenzy when
+    //             an active force (fauna grazing / vessel abilities) eats its mass back
+    //             below the Frenzy exit threshold — mass is conserved, there is no decay.
     public enum CellPhase
     {
         None = 0,
-        Sprout = 1,
-        Quiet = 2,
-        Settled = 3,
-        Restless = 4,
-        Frozen = 5,
-        Rabid = 6,
```

</details>

### `be71a920d` — feat(port): vessel-layer arc V4 — dual-mouse strategy, MultiMouse providers, device orientation

_Claude, 2026-06-11 19:39:38 +0000_

```text
DualMouseInputStrategy (298L), MultiMouse folder (IMultiMouseDevice,
MultiMouseService, UnityMultiMouseProvider, Win32RawInputMultiMouseProvider
— the Win32 provider's #if STANDALONE_WIN body compiles out verbatim),
DeviceOrientationHandler (167L, coroutine-driven attitude init runs
inert headless: AttitudeSensor.current stays null until phase-5
backends). Shim: Mouse.all/displayName, InputSystem.devices,
Accelerometer, AttitudeSensor.enabled, Cursor/CursorLockMode, Screen
autorotate flags. 582 tests green.
```

```text
 Port/PORT_PLAN.md                                                     |   9 +-
 Port/src/CosmicShore.Engine/Compat/EngineCompat.cs                    |  13 +
 Port/src/CosmicShore.Engine/InputSystem/InputDevices.cs               |  17 ++
 Port/src/CosmicShore.Game/Controller/IO/DeviceOrientationHandler.cs   | 167 ++++++++++++
 Port/src/CosmicShore.Game/Controller/IO/DualMouseInputStrategy.cs     | 298 +++++++++++++++++++++
 .../CosmicShore.Game/Controller/IO/MultiMouse/IMultiMouseDevice.cs    |  35 +++
 .../CosmicShore.Game/Controller/IO/MultiMouse/MultiMouseService.cs    |  78 ++++++
 .../Controller/IO/MultiMouse/UnityMultiMouseProvider.cs               |  97 +++++++
 .../Controller/IO/MultiMouse/Win32RawInputMultiMouseProvider.cs       | 461 ++++++++++++++++++++++++++++++++
 9 files changed, 1172 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1270 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index c5543f1d9..2b32c62a6 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -141,9 +141,12 @@ GetGyroRotation commented until V5], IInputStrategy, BaseInputStrategy,
 KeyboardInputStrategy — the live loose-file strategy per the survey). **V3 DONE** (TouchInputStrategy + GamepadInputStrategy verbatim; shim grew Screen.dpi/
 currentResolution + unified TouchPhase — the original shares one enum across
 namespaces; InputStrategyTests: all three strategies run inert without faulting, invert
-toggles write through). Next: V4 (DualMouseInputStrategy, MultiMouseService,
-DeviceOrientationHandler), rival balance from prompter feedback, richer meshes/track
-variety.
+toggles write through). **V4 DONE** (DualMouseInputStrategy, MultiMouse folder — Win32 raw-input provider
+compiles out under our defines, verbatim — DeviceOrientationHandler; shim grew Mouse.all/
+displayName, InputSystem.devices, Accelerometer, AttitudeSensor.enabled, Cursor/
+CursorLockMode, Screen autorotate flags). The IO SCC is now fully ported except
+InputStatus(V7) + InputController(V5). Next: V5 (GameSetting + InputController, closes
+deviation #10a), rival balance from prompter feedback, richer meshes/track variety.
 
 ## Phase roadmap
 
diff --git a/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs b/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs
index 04cb34f7d..bc636b88c 100644
--- a/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs
+++ b/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs
@@ -35,6 +35,10 @@ namespace CosmicShore.Engine
         public static float dpi = 96f;
         public static ScreenOrientation orientation = ScreenOrientation.LandscapeLeft;
         public static bool sleepTimeout;
+        public static bool autorotateToPortrait;
+        public static bool autorotateToPortraitUpsideDown;
+        public static bool autorotateToLandscapeLeft;
+        public static bool autorotateToLandscapeRight;
         public static Resolution currentResolution => new() { width = width, height = height, refreshRate = 60 };
     }
 
@@ -127,6 +131,15 @@ namespace CosmicShore.Engine
         }
     }
 
+    public enum CursorLockMode { None = 0, Locked = 1, Confined = 2 }
+
+    /// <summary>Cursor state holder; the windowing backend applies it (phase 5).</summary>
+    public static class Cursor
+    {
+        public static bool visible = true;
+        public static CursorLockMode lockState = CursorLockMode.None;
+    }
+
     public struct RaycastHit
     {
         public Vector3 point;
diff --git a/Port/src/CosmicShore.Engine/InputSystem/InputDevices.cs b/Port/src/CosmicShore.Engine/InputSystem/InputDevices.cs
index 17c56c9b0..5dffb0c39 100644
--- a/Port/src/CosmicShore.Engine/InputSystem/InputDevices.cs
+++ b/Port/src/CosmicShore.Engine/InputSystem/InputDevices.cs
@@ -69,8 +69,14 @@ namespace CosmicShore.Engine.InputSystem
     {
         public static Mouse current;
 
+        /// <summary>All attached mice (empty until a platform input backend registers devices).</summary>
+        public static readonly System.Collections.Generic.List<Mouse> all = new();
+
         public readonly MouseButtonControl leftButton = new(), rightButton = new(), middleButton = new();
         public readonly PositionControl position = new(), delta = new(), scroll = new();
+        public string name = "Mouse";
+        public string deviceId = "";
+        public string displayName = "Mouse";
     }
 
     public sealed class QuaternionControl
@@ -79,16 +85,27 @@ namespace CosmicShore.Engine.InputSystem
         public Quaternion ReadValue() => value;
     }
 
+    public sealed class Accelerometer
+    {
+        public static Accelerometer current;
+        public bool enabled;
+        public StickControl acceleration = new();
+    }
```

</details>

### `ec5d0dcc5` — feat(port): vessel-layer arc V5 — GameSetting + InputController; deviation #10a closed

_Claude, 2026-06-11 20:07:04 +0000_

```text
GameSetting (Singleton, PlayerPrefs-backed settings; Deviation #14:
UGS cloud-save sync commented until the services phase; pure
PlayerSettingsCloudData model ported). InputController (strategy
orchestration: device detection, pause wiring, invert sync, portrait
handling; Deviation #10b: IVessel member commented until V6;
Deviation #15: concrete InputStatus factory fail-loud until V7).
IInputStatus restored to verbatim (#10a closed) — SkimInputStatus and
test stubs implement the restored members. 582 tests green.
```

```text
 Port/PORT_PLAN.md                                                     |   9 +-
 Port/src/CosmicShore.Client/SkimInputStatus.cs                        |   3 +
 Port/src/CosmicShore.Game/Controller/IO/IInputStatus.cs               |   6 +-
 Port/src/CosmicShore.Game/Controller/IO/InputController.cs            | 279 ++++++++++++++++++++++++++++++++
 Port/src/CosmicShore.Game/Controller/Settings/GameSetting.cs          | 276 +++++++++++++++++++++++++++++++
 .../System/CloudData/Models/PlayerSettingsCloudData.cs                |  35 ++++
 Port/tests/CosmicShore.Tests/InputStrategyTests.cs                    |   2 +
 7 files changed, 604 insertions(+), 6 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 680 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 2b32c62a6..a6b93cb60 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -145,8 +145,13 @@ toggles write through). **V4 DONE** (DualMouseInputStrategy, MultiMouse folder 
 compiles out under our defines, verbatim — DeviceOrientationHandler; shim grew Mouse.all/
 displayName, InputSystem.devices, Accelerometer, AttitudeSensor.enabled, Cursor/
 CursorLockMode, Screen autorotate flags). The IO SCC is now fully ported except
-InputStatus(V7) + InputController(V5). Next: V5 (GameSetting + InputController, closes
-deviation #10a), rival balance from prompter feedback, richer meshes/track variety.
+InputStatus(V7) + InputController(V5). **V5 DONE** (GameSetting [282L; Deviation #14: UGS cloud-settings paths commented until
+services phase; pure PlayerSettingsCloudData model ported] + InputController [275L;
+Deviation #10b: IVessel field/usages commented, restore at V6; Deviation #15:
+TryAddInputStatus fail-loud until concrete InputStatus at V7]; **Deviation #10a
+CLOSED** — IInputStatus verbatim again). Next: V6 — the keystone (ITransform, IVessel,
+IPlayer, IVesselStatus #10c, ElementalFloat, ElementalShipComponent; restores #9 and
+#10b), rival balance from prompter feedback.
 
 ## Phase roadmap
 
diff --git a/Port/src/CosmicShore.Client/SkimInputStatus.cs b/Port/src/CosmicShore.Client/SkimInputStatus.cs
index 25e23ffcc..4f7b593fe 100644
--- a/Port/src/CosmicShore.Client/SkimInputStatus.cs
+++ b/Port/src/CosmicShore.Client/SkimInputStatus.cs
@@ -17,6 +17,9 @@ namespace CosmicShore.Client
         public ScriptableEventInputEvents OnButtonPressed { get; } = new() { name = "OnButtonPressed" };
         public ScriptableEventInputEvents OnButtonReleased { get; } = new() { name = "OnButtonReleased" };
 
+        public InputController InputController { get; set; }
+        public Quaternion GetGyroRotation() => Quaternion.identity;
+
         public float XSum { get; set; }
         public float YSum { get; set; }
         public float XDiff { get; set; } = 0.5f; // neutral sticks = mid throttle
diff --git a/Port/src/CosmicShore.Game/Controller/IO/IInputStatus.cs b/Port/src/CosmicShore.Game/Controller/IO/IInputStatus.cs
index 41dbe8a14..e12ae2e0f 100644
--- a/Port/src/CosmicShore.Game/Controller/IO/IInputStatus.cs
+++ b/Port/src/CosmicShore.Game/Controller/IO/IInputStatus.cs
@@ -14,8 +14,7 @@ namespace CosmicShore.Gameplay
         ScriptableEventInputEvents OnButtonPressed {get;}
         ScriptableEventInputEvents OnButtonReleased {get;}
         
-        // PORT Deviation #10a: restored when InputController lands (VESSEL_LAYER V5)
-        // InputController InputController { get; set; }
+        InputController InputController { get; set; }
 
         // Floats
         float XSum { get; set; }
@@ -50,8 +49,7 @@ namespace CosmicShore.Gameplay
         Vector2 SingleTouchValue { get; set; }
         Vector3 ThreeDPosition { get; set; }
 
-        // PORT Deviation #10a: restored when InputController lands (VESSEL_LAYER V5)
-        // Quaternion GetGyroRotation();
+        Quaternion GetGyroRotation();
         void ResetForReplay();
     }
 
diff --git a/Port/src/CosmicShore.Game/Controller/IO/InputController.cs b/Port/src/CosmicShore.Game/Controller/IO/InputController.cs
new file mode 100644
index 000000000..b9f3fa9fc
--- /dev/null
+++ b/Port/src/CosmicShore.Game/Controller/IO/InputController.cs
@@ -0,0 +1,279 @@
+using CosmicShore.Engine;
+using CosmicShore.Core;
+using CosmicShore.Gameplay;
+using CosmicShore.Gameplay.MultiMouse;
+using CosmicShore.Engine.InputSystem;
+using CosmicShore.Engine.InputSystem.EnhancedTouch;
+using CosmicShore.Utility;
+using CosmicShore.Engine.Injection;
+
+namespace CosmicShore.Gameplay
+{
+    /// <summary>
+    /// This class should only be initialized if user wants to control any ship.
+    /// Don't initialize this for any AI / Multiplayer Non Owner Players
+    /// </summary>
+    public class InputController : MonoBehaviour
```

</details>

### `0ba86a305` — feat(port): vessel-layer V6 step 1 — ITransform

_Claude, 2026-06-11 20:08:22 +0000_

```text
 Port/PORT_PLAN.md                               | 2 +-
 Port/src/CosmicShore.Game/Utility/ITransform.cs | 9 +++++++++
 2 files changed, 10 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index a6b93cb60..7bec4ff21 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -149,7 +149,7 @@ InputStatus(V7) + InputController(V5). **V5 DONE** (GameSetting [282L; Deviation
 services phase; pure PlayerSettingsCloudData model ported] + InputController [275L;
 Deviation #10b: IVessel field/usages commented, restore at V6; Deviation #15:
 TryAddInputStatus fail-loud until concrete InputStatus at V7]; **Deviation #10a
-CLOSED** — IInputStatus verbatim again). Next: V6 — the keystone (ITransform, IVessel,
+CLOSED** — IInputStatus verbatim again). V6 IN PROGRESS (ITransform ported). Next: V6 remainder — the keystone (ITransform, IVessel,
 IPlayer, IVesselStatus #10c, ElementalFloat, ElementalShipComponent; restores #9 and
 #10b), rival balance from prompter feedback.
 
diff --git a/Port/src/CosmicShore.Game/Utility/ITransform.cs b/Port/src/CosmicShore.Game/Utility/ITransform.cs
new file mode 100644
index 000000000..5a20b9e88
--- /dev/null
+++ b/Port/src/CosmicShore.Game/Utility/ITransform.cs
@@ -0,0 +1,9 @@
+using CosmicShore.Engine;
+
+namespace CosmicShore.Utility
+{
+    public interface ITransform
+    {
+        public Transform Transform { get; }
+    }
+}
\ No newline at end of file
```

</details>

### `7c6b65bcc` — docs(port): HARD RULE — never merge out of this branch; handoff prompt updated

_Claude, 2026-06-11 20:13:07 +0000_

```text
 Port/PORT_PLAN.md | 15 +++++++++++++--
 1 file changed, 13 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 7bec4ff21..65ab5853a 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -79,6 +79,13 @@ The prompter tests progress without prompting the loop. Contract:
 | `port-m3` | First rendered frame (PNG artifact in chat + repo) | pull + open artifact | ⬜ |
 | `port-m4` | First interactive desktop build | `… -- --render` | ⬜ |
 
+## HARD RULE (prompter, 2026-06-11): never merge OUT of this branch
+
+**NEVER merge, rebase, or cherry-pick anything from `claude/quirky-cannon-sk8a02`
+into `bleeding-edge` or any other branch.** Merging INTO this branch (as was done
+with bleeding-edge and brahmagupta) is fine; the reverse direction is prohibited.
+Push only to `claude/quirky-cannon-sk8a02`.
+
 ## Session resume protocol (fresh session / fresh container)
 
 The loop's memory is THIS FILE plus the branch — no chat context is load-bearing.
@@ -86,11 +93,15 @@ To resume in a brand-new session, the prompter pastes exactly:
 
 > /loop run Port/setup.sh if the toolchain is missing, then continue the Cosmic Shore
 > port per Port/PORT_PLAN.md: SPRINT MODE (SkimRace toward full Cosmic Shore parity)
-> + the fidelity arc (docs/VESSEL_LAYER.md). Keep dotnet test green, update this plan,
-> commit and push to claude/quirky-cannon-sk8a02, ship playable zips to Port/dist.
+> + the fidelity arc (docs/VESSEL_LAYER.md, V6 keystone next). Keep dotnet test green,
+> update PORT_PLAN, commit and push ONLY to claude/quirky-cannon-sk8a02 — NEVER merge
+> anything from this branch into bleeding-edge or any other branch. Ship playable zips
+> to Port/dist and tell me the download link when builds change.
 
 That re-arms the heartbeat, rebuilds the toolchain (~3-5 min via setup.sh), and picks
 up at NEXT UP. Mid-iteration work is never stranded: every iteration ends pushed.
+(Only ONE session should run this loop at a time; the 2026-06-11 session stopped its
+heartbeat on handoff.)
 
 ## Toolchain (re-verify each fresh container)
 
```

</details>

### `27be13618` — fix(port): commit the csproj files — root Unity .gitignore had swallowed all 7

_Claude, 2026-06-11 21:21:53 +0000_

```text
The branch could never build from a fresh clone: *.csproj / *.sln in the root
.gitignore excluded every project file from every push (the .slnx survived only
because *.sln doesn't match it). The prompter's first from-source run hit
'Couldn't find a project to run' at Port/src/CosmicShore.Client.

- .gitignore: negate Port/**/*.csproj|sln|slnx (Unity wildcards stay for Assets)
- reconstruct all 7 csprojs (Engine <- Data <- Game <- Cli/Client; xunit global
  Using; Silk.NET 2.22.0 pins + OpenAL.Soft.Native 1.23.1; embedded squirrel.mesh)
- Client: CopySilkNativesBesideApp target — Silk.NET's loader doesn't probe
  runtimes/<rid>/native, so dotnet run crashed 'GlfwPlatform not applicable';
  natives now copy flat into the output for the building machine's RID
- PORT_PLAN: incident logged, fresh-clone buildability added to the green invariant

Verified: dotnet build clean, 582 tests green (330 xunit + 252 NUnit), CLI smoke
PASS, headless Client screenshot smoke reaches Racing with the squirrel hull.
```

```text
 .gitignore                                                          |  7 ++++++
 Port/PORT_PLAN.md                                                   | 21 ++++++++++++++++++
 Port/src/CosmicShore.Cli/CosmicShore.Cli.csproj                     | 13 +++++++++++
 Port/src/CosmicShore.Client/CosmicShore.Client.csproj               | 42 +++++++++++++++++++++++++++++++++++
 Port/src/CosmicShore.Data/CosmicShore.Data.csproj                   |  7 ++++++
 Port/src/CosmicShore.Engine/CosmicShore.Engine.csproj               |  3 +++
 Port/src/CosmicShore.Game/CosmicShore.Game.csproj                   |  8 +++++++
 Port/tests/CosmicShore.Tests.Ported/CosmicShore.Tests.Ported.csproj | 19 ++++++++++++++++
 Port/tests/CosmicShore.Tests/CosmicShore.Tests.csproj               | 24 ++++++++++++++++++++
 9 files changed, 144 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 65ab5853a..0aabe973b 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -35,6 +35,10 @@ The prompter tests progress without prompting the loop. Contract:
 
 1. **Green branch invariant.** Every push to `claude/quirky-cannon-sk8a02` has
    `cd Port && dotnet build && dotnet test` green. The branch is always safe to pull.
+   This includes **buildable from a fresh clone**: `git ls-files Port | grep -c 'csproj$'`
+   must equal 7 — the root Unity `.gitignore` (`*.csproj`/`*.sln`) silently excluded
+   every project file until 2026-06-11 (negations added), which shipped a branch whose
+   source couldn't build on the prompter's machine.
 2. **Runnable harness: `CosmicShore.Cli`** (`src/CosmicShore.Cli`, lands iteration 2).
    One command to exercise the current port on any machine with the .NET 10 SDK:
    `cd Port && dotnet run --project src/CosmicShore.Cli`. It grows with the port:
@@ -299,6 +303,23 @@ IPlayer, IVesselStatus #10c, ElementalFloat, ElementalShipComponent; restores #9
   vessel layer as a dedicated multi-iteration arc (survey → engine Material/Pose →
   leaf classes → interfaces → restore deviations).
 
+- **Infra rescue** (2026-06-11, fresh session): prompter's first from-source run
+  (`dotnet run --project Port\src\CosmicShore.Client`) failed — "Couldn't find a
+  project to run". Root cause: the Unity root `.gitignore` ignores `*.csproj`/`*.sln`,
+  so all seven csproj files had NEVER been pushed (the `.slnx` survived only because
+  `*.sln` doesn't match it). Fixed: `.gitignore` negations (`!Port/**/*.csproj` etc.)
+  + all seven csprojs reconstructed and committed (graph: Engine ← Data ← Game ←
+  Cli/Client; xunit project needs `<Using Include="Xunit" />`; Client pins
+  Silk.NET 2.22.0 + Silk.NET.OpenAL.Soft.Native 1.23.1, embeds `Assets/squirrel.mesh`,
+  AllowUnsafeBlocks). Second find: Silk.NET's loader does not probe
+  `runtimes/<rid>/native/` on this stack ("GlfwPlatform - not applicable" despite the
+  native restoring; verified `dlopen`/`NativeLibrary` load it fine) — new
+  `CopySilkNativesBesideApp` target in the Client csproj copies the building machine's
+  `$(NETCoreSdkRuntimeIdentifier)` natives flat into the output, fixing `dotnet run`
+  on every OS. Verified: build clean, **582 tests green (330 xunit + 252 NUnit)**, CLI
+  smoke PASS, headless Client screenshot smoke reaches `Racing` with the squirrel hull
+  rendering. Dist zips untouched (game code unchanged).
+
 ## NEXT UP (iteration 6)
 
 Goal: start the vessel-layer arc so VesselStatus/ResourceSystem can port in
```

</details>

### `afa981460` — feat(port): vessel-layer V6 keystone — IVessel/IPlayer/IVesselStatus trio; deviations #9 + #10b closed

_Claude, 2026-06-11 21:59:31 +0000_

```text
- IVessel, IPlayer verbatim; IVesselStatus lands with Deviation #10c (13 members
  commented pending V7-V19 types, each tagged with its restore iteration)
- ElementalFloat (LerpUnclamped scaling, levels -5..15) + ElementalShipComponent
  (reflective binding); ResourceSystem base + RequireComponent restored (#9 closed)
- InputController.vessel field restored (#10b closed); CS0169 added to NoWarn (#16)
- ShipActionSO, ShipActionExecutorBase, ActionExecutorRegistry, legacy ShipAction,
  IVesselHUDController, R_ShipElementStatsHandler
- AudioSystem type-preserving shell (Deviation #11, pulled forward from V15)
- ElementalFloatTests: scaling theory, disabled inertness, reflective name
  composition, registry init/lookup/fallback, null-player defaults frozen

594 tests green (342 xunit + 252 NUnit); headless client smoke unaffected.
```

```text
 Port/Directory.Build.props                                            |   6 +-
 Port/PORT_PLAN.md                                                     |  58 ++++---
 .../Controller/Environment/MiniGameObjects/CellItem.cs                |   1 -
 Port/src/CosmicShore.Game/Controller/IO/InputController.cs            |   4 +-
 Port/src/CosmicShore.Game/Controller/Player/IPlayer.cs                |  82 ++++++++++
 Port/src/CosmicShore.Game/Controller/Vessel/ElementalFloat.cs         |  50 ++++++
 .../CosmicShore.Game/Controller/Vessel/ElementalVesselComponent.cs    |  32 ++++
 Port/src/CosmicShore.Game/Controller/Vessel/IVessel.cs                |  65 ++++++++
 Port/src/CosmicShore.Game/Controller/Vessel/IVesselStatus.cs          | 139 +++++++++++++++++
 .../Vessel/R_VesselActions/Data Containers/VesselActionSO.cs          |  21 +++
 .../Vessel/R_VesselActions/Executors/ActionExecutorRegistry.cs        |  38 +++++
 .../Vessel/R_VesselActions/Executors/VesselActionExecutorBase.cs      |  10 ++
 .../CosmicShore.Game/Controller/Vessel/R_VesselElementStatsHandler.cs |  32 ++++
 Port/src/CosmicShore.Game/Controller/Vessel/ResourceSystem.cs         |   5 +-
 .../CosmicShore.Game/Controller/Vessel/VesselActions/VesselAction.cs  |  23 +++
 Port/src/CosmicShore.Game/System/Audio/AudioSystem.cs                 |  26 ++++
 Port/src/CosmicShore.Game/UI/Interfaces/IVesselHUDController.cs       |  17 ++
 Port/tests/CosmicShore.Tests/ElementalFloatTests.cs                   | 266 ++++++++++++++++++++++++++++++++
 18 files changed, 849 insertions(+), 26 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 985 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 0aabe973b..02e04b8e9 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -164,9 +164,14 @@ InputStatus(V7) + InputController(V5). **V5 DONE** (GameSetting [282L; Deviation
 services phase; pure PlayerSettingsCloudData model ported] + InputController [275L;
 Deviation #10b: IVessel field/usages commented, restore at V6; Deviation #15:
 TryAddInputStatus fail-loud until concrete InputStatus at V7]; **Deviation #10a
-CLOSED** — IInputStatus verbatim again). V6 IN PROGRESS (ITransform ported). Next: V6 remainder — the keystone (ITransform, IVessel,
-IPlayer, IVesselStatus #10c, ElementalFloat, ElementalShipComponent; restores #9 and
-#10b), rival balance from prompter feedback.
+CLOSED** — IInputStatus verbatim again). **V6 DONE** (keystone: IVessel + IPlayer +
+IVesselStatus [Deviation #10c open — 13 members commented pending V7-V19 types] +
+ElementalFloat + ElementalShipComponent + IVesselHUDController +
+R_ShipElementStatsHandler + ShipActionSO/ShipActionExecutorBase/ActionExecutorRegistry/
+legacy ShipAction; AudioSystem shell pulled forward as Deviation #11; **Deviations #9
+and #10b CLOSED** — ResourceSystem : ElementalShipComponent + RequireComponent restored,
+InputController.vessel field live). Next: V7 (engine E2 renderer stubs; InputStatus,
+VesselAnimation + IVesselStatus member restore), rival balance from prompter feedback.
 
 ## Phase roadmap
 
@@ -257,7 +262,10 @@ IPlayer, IVesselStatus #10c, ElementalFloat, ElementalShipComponent; restores #9
 | 6 | **Upstream latent red test reframed**: `ImpactEffects_AllValuesAreUnique` contradicts the shipped enum, which intentionally merges legacy effect groups sharing values 1-8, 10. Values are wire format — port freezes the exact duplicate set instead so NEW collisions still fail. | Enum values untouchable. |
 | 7 | 10 SOAP files deferred pending gameplay types: ScriptableEventVesselImpactor / ExplosionDebuffApplied / SkimmerDebuffApplied (IVessel/IVesselStatus/VesselImpactor), ScriptableSilhouetteData/* (SilhouetteController), ScriptableVesselHUDData/* (MiniGameHUD), VesselPrefabContainer (IVesselStatus). MainMenuStateTests (MainMenuController) and GameObjectExtensionTests (physics types) likewise port with their subjects. | Tracked in NEXT UP. |
 | 8 | Stat structs (CellStats/CrystalStats/PrismStats/AbilityStats), PrismType, and audio category enums are extracted into their own port files (source noted in headers) because their host classes (StatsManager, PrismFactory, AudioSystem) port in later phases. | File-split only; content verbatim. |
-| 9 | ResourceSystem temporary deviations: (a) base class `ElementalShipComponent` → `MonoBehaviour` until IVessel/ElementalFloat port; (b) `[RequireComponent(typeof(IVesselStatus))]` commented until IVesselStatus ports. Class body verbatim. Restore both when the vessel layer lands. | Unblocks the elemental core. |
+| 9 | ResourceSystem temporary deviations: (a) base class `ElementalShipComponent` → `MonoBehaviour` until IVessel/ElementalFloat port; (b) `[RequireComponent(typeof(IVesselStatus))]` commented until IVesselStatus ports. Class body verbatim. **CLOSED at V6 (2026-06-11)** — both restored verbatim. | Unblocks the elemental core. |
+| 10c | `IVesselStatus` landed (V6) with 13 members commented pending their types: `AIPilot`, `AICinematicBehavior`, `AutoPilotEnabled` (→V18), `AttachedPrism` (→V15), `VesselAnimation` (→V7), `VesselTransformer` (→V8), `Customization` (→V9), `NearFieldSkimmer`/`FarFieldSkimmer` (→V16), `VesselPrismController`/`ActionHandler` (→V17), `VesselCameraCustomizer`/`Silhouette` (→V19). Each restore iteration uncomments its members; V19 closes. | Stages the vessel SCC per VESSEL_LAYER.md. |
+| 11 | `AudioSystem` type-preserving shell (`Instance`, two `PlayGameplaySFX` overloads; bodies no-op) — pulled forward from V15 to V6 because `ActionExecutorRegistry` needs the type. Real port with the phase-5 audio backend. | Keeps ActionExecutorRegistry verbatim. |
+| 16 | `Directory.Build.props` adds CS0169 to NoWarn (alongside CS0649): verbatim Unity-era private fields whose only usages are commented (e.g. `InputController.vessel` until its orientation block revives) or inspector-driven fire it; the Unity compiler tolerated them. | Verbatim fields without warning noise. |
 
 ## Iteration log
 
@@ -320,21 +328,35 @@ IPlayer, IVesselStatus #10c, ElementalFloat, ElementalShipComponent; restores #9
   smoke PASS, headless Client screenshot smoke reaches `Racing` with the squirrel hull
   rendering. Dist zips untouched (game code unchanged).
 
-## NEXT UP (iteration 6)
-
-Goal: start the vessel-layer arc so VesselStatus/ResourceSystem can port in
-iteration 4.
-
-1. **Vessel-layer arc, step 1 — survey & map**: write `docs/VESSEL_LAYER.md` listing
-   every type in the IVessel/IVesselStatus/IPlayer closure with line counts, Unity
-   deps, and a dependency-ordered porting sequence (leaf classes first). Add engine
-   `Material` (minimal: name + color/float/property store) and `Pose` struct.
-2. Step 2 — port ITransform + the closure's leaf data types per the survey.
-3. Port `Utility/DataContainers/CellPhaseThresholds`-adjacent ecology configs and the
-   prism density/BlockDensityGrid pure-logic pieces if reachable.
-4. Grow the CLI toward an M2 vertical slice: one cell + crystals + 2 scripted vessels
+- **Iteration 6** (2026-06-11): **V6 keystone — the vessel-layer trio lands.**
+  IVessel, IPlayer, IVesselStatus (Deviation #10c: 13 members commented pending
+  V7-V19 types), ElementalFloat (LerpUnclamped scaling over levels -5..15),
+  ElementalShipComponent (reflective ElementalFloat binding), IVesselHUDController,
+  R_ShipElementStatsHandler, ShipActionSO + ShipActionExecutorBase +
+  ActionExecutorRegistry + legacy ShipAction; AudioSystem type-preserving shell
+  (Deviation #11, pulled forward from V15 — ActionExecutorRegistry needs the type).
+  **Deviations #9 and #10b CLOSED**: ResourceSystem : ElementalShipComponent +
+  `[RequireComponent(typeof(IVesselStatus))]` restored verbatim;
+  `InputController.vessel` field live again. CS0169 added to NoWarn (Deviation #16);
+  CellItem duplicate-using substitution artifact fixed. New ElementalFloatTests:
+  scaling theory across the full level range, disabled-float inertness, reflective
+  name composition (`Type.field`), registry init/lookup/fallback, IVesselStatus
+  null-player defaults frozen ("No-name" / Jade). **594 tests green (342 + 252)**;
+  headless client smoke unaffected.
+
+## NEXT UP (iteration 7)
+
+Goal: V7 of the vessel-layer arc + sprint feedback.
+
+1. **V7**: engine E2 (renderer stubs per VESSEL_LAYER.md). Port `InputStatus`
+   (concrete; closes Deviation #15's fail-loud TryAddInputStatus) and
+   `VesselAnimation`; uncomment their IVesselStatus members (#10c partial restore).
+2. Sprint: rival balance from prompter feedback (S4 KNOWN ISSUE — rival can't beat a
+   perfect autopilot; vs humans it contests missed crystals). Tune overtake/rubber-band
+   so AI-vs-AI demos stay competitive.
+3. Grow the CLI toward an M2 vertical slice: one cell + crystals + 2 scripted vessels
    exchanging resource/elemental state on a seeded run.
-5. Update this file (status tables, iteration log, NEXT UP), commit, push.
```

</details>

### `67472bc2f` — feat(port): vessel-layer V7 — InputStatus + VesselAnimation; deviation #15 closed

_Claude, 2026-06-11 22:31:21 +0000_

```text
- engine E2 renderer data stubs: Renderer (material array, non-cloning material —
  documented engine deviation), MeshRenderer, SkinnedMeshRenderer (blend-shape
  weight store), TrailRenderer, data-only Camera with first-enabled main
- InputStatus verbatim: IsSpawned-switched local/NetworkVariable storage for all
  25 input channels, owner-gated writes, pause toggle on both paths,
  ResetForReplay preserves invert preferences
- VesselAnimation verbatim: idle/dual-stick/single-stick puppetry routing,
  element->blend-shape mapping, engine/body flare material writes
- Deviation #15 closed: TryAddInputStatus -> gameObject.GetOrAdd<InputStatus>()
- #10c partial restore: IVesselStatus.VesselAnimation live
- test doubles extracted to shared VesselLayerTestDoubles.cs; 16 new tests

610 tests green (358 xunit + 252 NUnit); headless client smoke unaffected.
```

```text
 Port/PORT_PLAN.md                                                 |  37 +++--
 Port/src/CosmicShore.Engine/Rendering/Camera.cs                   |  33 ++++
 Port/src/CosmicShore.Engine/Rendering/Renderers.cs                |  71 +++++++++
 Port/src/CosmicShore.Game/Controller/Animation/VesselAnimation.cs | 153 +++++++++++++++++++
 Port/src/CosmicShore.Game/Controller/IO/InputController.cs        |   5 +-
 Port/src/CosmicShore.Game/Controller/IO/InputStatus.cs            | 292 ++++++++++++++++++++++++++++++++++++
 Port/src/CosmicShore.Game/Controller/Vessel/IVesselStatus.cs      |   2 +-
 Port/tests/CosmicShore.Tests/ElementalFloatTests.cs               |  84 -----------
 Port/tests/CosmicShore.Tests/InputStatusTests.cs                  | 127 ++++++++++++++++
 Port/tests/CosmicShore.Tests/VesselAnimationTests.cs              | 142 ++++++++++++++++++
 Port/tests/CosmicShore.Tests/VesselLayerTestDoubles.cs            |  97 ++++++++++++
 11 files changed, 945 insertions(+), 98 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1138 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 02e04b8e9..295360733 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -170,8 +170,12 @@ ElementalFloat + ElementalShipComponent + IVesselHUDController +
 R_ShipElementStatsHandler + ShipActionSO/ShipActionExecutorBase/ActionExecutorRegistry/
 legacy ShipAction; AudioSystem shell pulled forward as Deviation #11; **Deviations #9
 and #10b CLOSED** — ResourceSystem : ElementalShipComponent + RequireComponent restored,
-InputController.vessel field live). Next: V7 (engine E2 renderer stubs; InputStatus,
-VesselAnimation + IVesselStatus member restore), rival balance from prompter feedback.
+InputController.vessel field live). **V7 DONE** (engine E2 renderer data stubs:
+Renderer/MeshRenderer/SkinnedMeshRenderer [blend-shape store]/TrailRenderer/Camera;
+InputStatus verbatim — IsSpawned-switched local/NetworkVariable storage, owner-gated
+writes, **Deviation #15 CLOSED** [TryAddInputStatus → GetOrAdd&lt;InputStatus&gt; verbatim];
+VesselAnimation verbatim + its IVesselStatus member uncommented [#10c partial restore]).
+Next: V8 (VesselTransformer + member restore), rival balance from prompter feedback.
 
 ## Phase roadmap
 
@@ -344,13 +348,28 @@ VesselAnimation + IVesselStatus member restore), rival balance from prompter fee
   null-player defaults frozen ("No-name" / Jade). **594 tests green (342 + 252)**;
   headless client smoke unaffected.
 
-## NEXT UP (iteration 7)
-
-Goal: V7 of the vessel-layer arc + sprint feedback.
-
-1. **V7**: engine E2 (renderer stubs per VESSEL_LAYER.md). Port `InputStatus`
-   (concrete; closes Deviation #15's fail-loud TryAddInputStatus) and
-   `VesselAnimation`; uncomment their IVesselStatus members (#10c partial restore).
+- **Iteration 7** (2026-06-11): **V7 — input/animation layer.** Engine E2 renderer
+  data stubs (`Renderer` material array + non-cloning `material` [documented engine
+  deviation], `SkinnedMeshRenderer` blend-shape weight store, `TrailRenderer`,
+  data-only `Camera` with first-enabled `main`). Ported verbatim: `InputStatus`
+  (292L — IsSpawned-switched local/NetworkVariable storage for all 25 input channels,
+  owner-gated writes, pause toggle event on both local and replicated paths,
+  ResetForReplay preserving player invert preferences) and `VesselAnimation` (152L —
+  abstract puppetry driver: idle/dual-stick/single-stick routing, element→blend-shape
+  mapping, engine/body flare material writes). **Deviation #15 CLOSED**
+  (`TryAddInputStatus` → `gameObject.GetOrAdd<InputStatus>()` verbatim); #10c partial
+  restore (`VesselAnimation` member live in IVesselStatus). Test doubles extracted to
+  shared `VesselLayerTestDoubles.cs`; 16 new tests (InputStatus spawn/ownership/reset
+  matrix, InputController Awake wiring, shape-key theory, flare, Update routing).
+  **610 tests green (358 + 252)**; client smoke unaffected.
+
+## NEXT UP (iteration 8)
+
+Goal: V8 of the vessel-layer arc + sprint feedback.
+
+1. **V8**: port `VesselTransformer` (518L, single-file oversize accepted) +
+   uncomment its IVesselStatus member (#10c partial restore). Behavior tests for the
+   flight model (AngleAxis rotation, throttle/boost composition, modifier structs).
 2. Sprint: rival balance from prompter feedback (S4 KNOWN ISSUE — rival can't beat a
    perfect autopilot; vs humans it contests missed crystals). Tune overtake/rubber-band
    so AI-vs-AI demos stay competitive.
diff --git a/Port/src/CosmicShore.Engine/Rendering/Camera.cs b/Port/src/CosmicShore.Engine/Rendering/Camera.cs
new file mode 100644
index 000000000..57f680d38
--- /dev/null
+++ b/Port/src/CosmicShore.Engine/Rendering/Camera.cs
@@ -0,0 +1,33 @@
+namespace CosmicShore.Engine
+{
+    /// <summary>
+    /// E2 camera data stub (VESSEL_LAYER.md): data-only — pose lives on the Transform,
+    /// projection parameters live here, nothing renders until the presentation phase.
+    /// <see cref="main"/> is the first enabled camera created, mirroring Unity's
+    /// MainCamera-tagged lookup closely enough for ported call sites.
+    /// </summary>
+    public class Camera : Component
+    {
+        public static Camera main { get; internal set; }
+
+        public bool enabled = true;
+        public float fieldOfView = 60f;
+        public float nearClipPlane = 0.3f;
+        public float farClipPlane = 1000f;
+        public bool orthographic;
+        public float orthographicSize = 5f;
+        public float depth;
```

</details>

### `b06eb6e9c` — fix(port): invert gamepad roll post-strategy (prompter feedback); correct publish recipe

_Claude, 2026-06-11 22:56:55 +0000_

```text
- roll sense flips with the inverted yaw: yaw-inverted steering banked opposite
  the turn unless roll inverts too. Same post-strategy treatment as the yaw flip —
  ported strategy YDiff wire semantic untouched, AI and keyboard paths untouched
- publish recipe corrected: IncludeNativeLibrariesForSelfExtract does NOT work —
  Silk.NET's loader cannot load self-extracted natives (verified headlessly:
  GlfwPlatform not applicable); natives must sit beside the exe. Recipe note in
  PORT_PLAN testing-protocol item 5 rewritten; single-file publish without the
  flag verified end-to-end on linux-x64 (boots to Racing, screenshot)
- both dist zips rebuilt from the fixed source with the proven beside-exe layout
  (exe + glfw3.dll + soft_oal.dll); SkimRace README controls note now says yaw
  AND roll inverted

582+28 tests green; headless client smoke reaches Racing.
```

```text
 Port/PORT_PLAN.md                         |  27 ++++++++++++++++++---------
 Port/dist/CosmicShore-Windows.zip         | Bin 32097786 -> 33906359 bytes
 Port/dist/SkimRace-Windows.zip            | Bin 33958350 -> 33906442 bytes
 Port/src/CosmicShore.Client/RaceWindow.cs |   5 +++++
 4 files changed, 23 insertions(+), 9 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 295360733..dd39626a0 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -57,13 +57,17 @@ The prompter tests progress without prompting the loop. Contract:
    notification with the exact command (or file) to try. (Annotated `port-mN` tags are
    created locally, but this environment's git proxy only accepts branch pushes — the
    log + commit message are the durable record.)
-5. **Standalone binaries on request / at milestones.** No-install executables build with
-   (NOTE `IncludeNativeLibrariesForSelfExtract` — without it GLFW/OpenAL natives land
-   BESIDE the exe and a bare-exe zip fails to load, which shipped once):
-   `dotnet publish <proj> -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true`
-   (swap `-r` for `linux-x64` / `osx-arm64`; ~36 MB single file; no trimming — the
-   engine's reflective lifecycle discovery forbids it). The exe holds its console window
-   open when double-clicked (`--no-wait` skips). Delivered into the chat at milestones.
+5. **Standalone binaries on request / at milestones.** No-install executables build with:
+   `dotnet publish <proj> -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true`
+   (swap `-r` for `linux-x64` / `osx-arm64`; ~39 MB exe; no trimming — the engine's
+   reflective lifecycle discovery forbids it). **Do NOT add
+   `IncludeNativeLibrariesForSelfExtract`** — verified 2026-06-11: Silk.NET's loader
+   cannot load self-extracted natives ("GlfwPlatform - not applicable"); it only finds
+   natives BESIDE the exe (same root cause as the `CopySilkNativesBesideApp` build
+   target). Without the flag, glfw3/soft_oal land beside the exe in the publish dir —
+   **zip them together with the exe** (the one historical bad ship was a bare-exe zip
+   missing those loose natives). The exe holds its console window open when
+   double-clicked (`--no-wait` skips). Delivered into the chat at milestones.
 6. **Fast test loop for the prompter** (replaces manual zip downloads):
    one-time `git clone` + checkout `claude/quirky-cannon-sk8a02`, then double-click
    `Port/play-latest.bat` after each push — it pulls, extracts dist, and launches
@@ -144,8 +148,13 @@ as two sticks. **SQUIRREL HULL SHIPPED**: first-party binary-FBX extractor (Pyth
 SquirrelVessel_CosmicShoresTest1.fbx (13,660 tris) → axis-remapped/normalized →
 Assets/squirrel.mesh embedded resource → flat-lit at load in jade/ruby palettes; dart
 remains fallback. **Gamepad yaw inverted** post-strategy (prompter preference; XDiff/
-YDiff semantics untouched). NEXT: prompter verifies Squirrel nose orientation in motion
-(flip is one sign in the extractor), drift/trail feel, rival tuning. Fidelity arc: **V1 DONE** (engine E3-E8/E10: RPC attrs, profiling shim, Screen/
+YDiff semantics untouched). **Gamepad roll inverted** post-strategy alongside it
+(prompter, 2026-06-11 second feedback round, first from-source flight): yaw-inverted
+steering banks opposite the turn unless roll flips too; AI and keyboard paths
+untouched. Both dist zips rebuilt on the corrected publish recipe (loose natives
+beside the exe — see testing-protocol item 5). NEXT: prompter verifies roll feel +
+Squirrel nose orientation in motion (flip is one sign in the extractor), drift/trail
+feel, rival tuning. Fidelity arc: **V1 DONE** (engine E3-E8/E10: RPC attrs, profiling shim, Screen/
 Application/PlayerPrefs/Resources statics, Object.DontDestroyOnLoad/Instantiate/
 FindFirstObjectByType, Physics+collider stubs; NetworkBehaviour now : MonoBehaviour
 (parity); ported PauseSystem, SafeLookRotation, Singleton family, NetMarkers,
diff --git a/Port/src/CosmicShore.Client/RaceWindow.cs b/Port/src/CosmicShore.Client/RaceWindow.cs
index 0427cb28f..d72dd1391 100644
--- a/Port/src/CosmicShore.Client/RaceWindow.cs
+++ b/Port/src/CosmicShore.Client/RaceWindow.cs
@@ -512,6 +512,11 @@ void main()
                 // after the ported strategy so XDiff throttle and YDiff roll semantics
                 // stay authentic; only the steering sense flips.
                 _playerStatus.XSum = -_playerStatus.XSum;
+                // Prompter preference (2026-06-11, second feedback round): roll sense
+                // flips with it — yaw-inverted steering banks opposite to the turn
+                // unless roll inverts too. Same post-strategy treatment; the ported
+                // strategy's YDiff wire semantic stays authentic, AI input untouched.
+                _playerStatus.YDiff = -_playerStatus.YDiff;
                 return;
             }
 
```

</details>

### `bf8688083` — feat(port): SkimRace S5 — closed circuit, persistent skimmable trails, energy top speed, analog drift

_Claude, 2026-06-11 23:24:21 +0000_

```text
Prompter directives (third feedback round):
- closed circuit: seeded ring with integer-harmonic radius/altitude undulation
  (loop closes exactly); rails/gates built in the loop's local frame; AI and
  screenshot autopilot retargeted by forward loop angle instead of '+z ahead'
- persistent trails: sim-owned skimmable race state emitted along the velocity
  path; never culled (conserved mass — race reset is the only sink); renderer
  rebuilds ribbons from sim state with freshness flare + steady aged body
- trail-skim energy: rival's trail always counts, own after 3s aging (lap back
  onto it); linear falloff over 7u; passive regen 0.12 -> 0.04
- energy raises top speed: throttle term x(1 + 0.6 * energy)
- analog drift on triggers: velocity decouples from the nose (align 7/s gripped
  -> 0.55/s full pull); drift-skimming charges up to 2x; keyboard Shift drifts;
  magenta drift gauge + energy bar flares while skimming
- crystals respawn 12s after claim; win target = full station count (30) so
  races span 2+ laps and the trail loop engages
- screenshot diag now reports energy/skim/trail counts; lap-2 run shows both
  pilots skimming and the trailing rival charging off the leader's ribbon

610 tests green; zips rebuilt (proven beside-exe layout); artifact committed.
```

```text
 Port/PORT_PLAN.md                             |  24 +++++-
 Port/artifacts/skimrace_s5_circuit_trails.png | Bin 0 -> 97159 bytes
 Port/dist/CosmicShore-Windows.zip             | Bin 33906359 -> 33907276 bytes
 Port/dist/SkimRace-Windows.zip                | Bin 33906442 -> 33907584 bytes
 Port/src/CosmicShore.Client/RaceWindow.cs     | 131 +++++++++++++++--------------
 Port/src/CosmicShore.Client/SkimRaceSim.cs    | 256 +++++++++++++++++++++++++++++++++++++++++++++-----------
 6 files changed, 295 insertions(+), 116 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 695 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index dd39626a0..5cd8821f2 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -152,9 +152,27 @@ YDiff semantics untouched). **Gamepad roll inverted** post-strategy alongside it
 (prompter, 2026-06-11 second feedback round, first from-source flight): yaw-inverted
 steering banks opposite the turn unless roll flips too; AI and keyboard paths
 untouched. Both dist zips rebuilt on the corrected publish recipe (loose natives
-beside the exe — see testing-protocol item 5). NEXT: prompter verifies roll feel +
-Squirrel nose orientation in motion (flip is one sign in the extractor), drift/trail
-feel, rival tuning. Fidelity arc: **V1 DONE** (engine E3-E8/E10: RPC attrs, profiling shim, Screen/
+beside the exe — see testing-protocol item 5). **S5 SHIPPED** (prompter directives,
+2026-06-11 third feedback round): **closed circuit** (seeded ring, integer-harmonic
+radius/altitude undulation so the loop closes exactly; loop-frame rails + gates;
+loop-angle AI/autopilot targeting replaces the old "+z ahead" logic), **persistent
+trails** (sim-owned skimmable race state, emitted along the velocity path, never
+culled — reset is the only sink, per conserved-mass rules), **trail-skim energy**
+(rival's trail always counts, own trail after 3s aging — lap back onto it; linear
+falloff over 7u; passive regen cut 0.12→0.04 so skimming is THE energy source),
+**energy raises top speed** (throttle term ×(1+0.6·energy)), **analog trigger drift**
+(triggers decouple velocity from the nose: course re-aligns at 7/s gripped → 0.55/s
+at full pull; drift-skimming charges up to 2×; keyboard Shift = full drift; magenta
+HUD gauge + energy bar flares while skimming). Crystals respawn 12s after claim so
+every lap is live; win target raised to the full station count (default 30) so races
+span 2+ laps and the lap-back-onto-your-trail loop actually engages. Verified
+headlessly: lap-1 diagnostic shows skim False (no ribbon to ride yet), lap-2 shows
+skim True for both pilots with the trailing rival charging off the leader's ribbon —
+an emergent slipstream catch-up. Artifact: `artifacts/skimrace_s5_circuit_trails.png`.
+KNOWN (next round): superhuman screenshot-autopilot beats the rival 30-9 — rival
+tuning should now exploit skim energy + drift deliberately. NEXT: prompter verifies
+drift feel/trail feel on the circuit, rival tuning, Squirrel nose orientation in
+motion (flip is one sign in the extractor). Fidelity arc: **V1 DONE** (engine E3-E8/E10: RPC attrs, profiling shim, Screen/
 Application/PlayerPrefs/Resources statics, Object.DontDestroyOnLoad/Instantiate/
 FindFirstObjectByType, Physics+collider stubs; NetworkBehaviour now : MonoBehaviour
 (parity); ported PauseSystem, SafeLookRotation, Singleton family, NetMarkers,
diff --git a/Port/src/CosmicShore.Client/RaceWindow.cs b/Port/src/CosmicShore.Client/RaceWindow.cs
index d72dd1391..33ceb14f3 100644
--- a/Port/src/CosmicShore.Client/RaceWindow.cs
+++ b/Port/src/CosmicShore.Client/RaceWindow.cs
@@ -61,9 +61,10 @@ namespace CosmicShore.Client
         uint _rivalTrailVao, _rivalTrailVbo;
         uint _hudVao, _hudVbo;
 
-        readonly List<(Vector3 pos, Vector3 right)> _trail = new();
-        readonly List<(Vector3 pos, Vector3 right)> _rivalTrail = new();
-        const int TrailMax = 110;
+        // trails live in the sim now (persistent race state, skimmable); these are
+        // reusable vertex scratch buffers so the per-frame ribbon rebuild doesn't churn GC
+        readonly List<float> _trailVerts = new();
+        readonly List<float> _rivalTrailVerts = new();
 
         Vector3 _camPos, _camLook;
         AudioEngine _audio;
@@ -109,11 +110,7 @@ namespace CosmicShore.Client
                 {
                     if (key == Key.Escape) _window.Close();
                     if (key == Key.R)
-                    {
                         SkimRaceFactory.ResetRace(_race.Shared, _race, _rival);
-                        _trail.Clear();
-                        _rivalTrail.Clear();
-                    }
                 };
 
             (_loop, _race, _rival) = SkimRaceFactory.Create(_seed, _crystalTarget, _playerStatus);
@@ -265,9 +262,9 @@ void main()
 
         unsafe void ConfigureDynamicVao(uint vao, uint vbo)
         {
+            // layout only — dynamic users re-BufferData each frame (trails grow unbounded)
             _gl.BindVertexArray(vao);
             _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
-            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(TrailMax * 2 * 7 * sizeof(float) * 4), null, BufferUsageARB.DynamicDraw);
             SetVertexLayout();
         }
 
@@ -291,10 +288,10 @@ void main()
             var data = new List<float>();
             for (int i = 0; i < 2600; i++)
             {
```

</details>

### `5aca9aef4` — feat(port): SkimRace S6 — rival field, elemental crystals, AI v2, minimap, scoreboard, skim/drift audio

_Claude, 2026-06-12 00:00:56 +0000_

```text
'Go hard, I want everything' round:
- field of rivals: --rivals N (default 3, max 7), Ruby/Gold/Blue domain palettes
  for hulls and ribbons, seeded temperaments (TurnSkill / DriftIQ / Aggression)
- elemental crystals: every station carries an element; claims raise that level
  permanently via the ported ResourceSystem and levels tune the vessel —
  Charge: skim charge rate, Mass: wider longer-reach trail (baked per TrailPoint),
  Space: turn rate, Time: boost burns slower; every 7th station Omni (+1 all);
  crystal octahedra tinted per element
- AI v2: multi-rival overtake on the loop, drift-to-charge when energy is low,
  drift through hard corners (DriftIQ), aggression-scaled boost reserve
- minimap (circuit + domain-colored pilot markers), lap + position HUD,
  finish scoreboard ranking the whole field
- audio: skim shimmer + drift rush loops, gain/pitch from contact and trigger
- screenshot diag reports per-pilot crystals, position/lap, elemental levels

Frame-1300 4-pilot diag: crystals [24,19,4,1], P1 lap 2, levels C13/M6/S8/T6 —
the field is competitive (was 30-9 in S5). 610 tests green; zips rebuilt.
```

```text
 Port/PORT_PLAN.md                             |  23 ++++-
 Port/artifacts/skimrace_s6_field_elements.png | Bin 0 -> 158002 bytes
 Port/dist/CosmicShore-Windows.zip             | Bin 33907276 -> 33911063 bytes
 Port/dist/SkimRace-Windows.zip                | Bin 33907584 -> 33911636 bytes
 Port/src/CosmicShore.Client/AudioEngine.cs    |  47 ++++++++-
 Port/src/CosmicShore.Client/Program.cs        |  15 +--
 Port/src/CosmicShore.Client/RaceWindow.cs     | 314 +++++++++++++++++++++++++++++++++++++-------------------
 Port/src/CosmicShore.Client/SkimRaceSim.cs    | 299 ++++++++++++++++++++++++++++++++++++++++-------------
 8 files changed, 510 insertions(+), 188 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1228 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 5cd8821f2..3c60effc6 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -169,10 +169,25 @@ span 2+ laps and the lap-back-onto-your-trail loop actually engages. Verified
 headlessly: lap-1 diagnostic shows skim False (no ribbon to ride yet), lap-2 shows
 skim True for both pilots with the trailing rival charging off the leader's ribbon —
 an emergent slipstream catch-up. Artifact: `artifacts/skimrace_s5_circuit_trails.png`.
-KNOWN (next round): superhuman screenshot-autopilot beats the rival 30-9 — rival
-tuning should now exploit skim energy + drift deliberately. NEXT: prompter verifies
-drift feel/trail feel on the circuit, rival tuning, Squirrel nose orientation in
-motion (flip is one sign in the extractor). Fidelity arc: **V1 DONE** (engine E3-E8/E10: RPC attrs, profiling shim, Screen/
+**S6 SHIPPED** ("go hard, I want everything" directive): **a field of rivals**
+(`--rivals N`, default 3, max 7 — Ruby/Gold/Blue domain palettes for hulls AND
+ribbons, seeded temperaments: TurnSkill/DriftIQ/Aggression), **elemental crystals**
+(every station carries an element, claims permanently raise that level via the
+ported ResourceSystem; levels tune the vessel — Charge→skim rate, Mass→wider
+longer-reach trail baked per TrailPoint, Space→turn rate, Time→cooler boost burn;
+every 7th station is Omni = +1 all; crystal octahedra tinted per element),
+**AI v2** (loop-aware multi-rival overtake, drift-to-charge when low on energy +
+drift through hard corners per DriftIQ, aggression-scaled boost reserve),
+**minimap** (circuit outline + domain-colored pilot markers, bottom-right),
+**lap + position HUD**, **finish scoreboard** (whole field ranked: domain diamond,
+position, crystals), **skim shimmer + drift rush audio layers** (gain/pitch follow
+contact strength and trigger pull). Verified headlessly: 4-pilot race, frame-1300
+diag `crystals [24,19,4,1], P1 lap 2, levels C13/M6/S8/T6, skim True, trail 5004`
+— the field is COMPETITIVE now (drift-charging rivals; was 30-9 in S5). Artifact:
+`artifacts/skimrace_s6_field_elements.png`. NEXT: prompter verifies the field race
+feel (drift, elements, rival pressure), Squirrel nose orientation in motion (flip
+is one sign in the extractor); deeper Cosmic Shore content (more vessel classes,
+cells/fauna ambience, shape modes) on request. Fidelity arc: **V1 DONE** (engine E3-E8/E10: RPC attrs, profiling shim, Screen/
 Application/PlayerPrefs/Resources statics, Object.DontDestroyOnLoad/Instantiate/
 FindFirstObjectByType, Physics+collider stubs; NetworkBehaviour now : MonoBehaviour
 (parity); ported PauseSystem, SafeLookRotation, Singleton family, NetMarkers,
diff --git a/Port/src/CosmicShore.Client/AudioEngine.cs b/Port/src/CosmicShore.Client/AudioEngine.cs
index e39ad9af3..fc2587e50 100644
--- a/Port/src/CosmicShore.Client/AudioEngine.cs
+++ b/Port/src/CosmicShore.Client/AudioEngine.cs
@@ -18,7 +18,7 @@ namespace CosmicShore.Client
         Context* _context;
         public bool Enabled { get; private set; }
 
-        uint _humSource, _boostSource;
+        uint _humSource, _boostSource, _skimSource, _driftSource;
         uint _chime, _rivalChime, _beep, _goBeep, _winJingle, _loseTone;
         readonly uint[] _oneShotSources = new uint[6];
         int _nextOneShot;
@@ -44,8 +44,12 @@ namespace CosmicShore.Client
 
                 _humSource = MakeLoopingSource(MakeBuffer(HumLoop()), 0.0f);
                 _boostSource = MakeLoopingSource(MakeBuffer(BoostLoop()), 0.0f);
+                _skimSource = MakeLoopingSource(MakeBuffer(SkimLoop()), 0.0f);
+                _driftSource = MakeLoopingSource(MakeBuffer(DriftLoop()), 0.0f);
                 _al.SourcePlay(_humSource);
                 _al.SourcePlay(_boostSource);
+                _al.SourcePlay(_skimSource);
+                _al.SourcePlay(_driftSource);
 
                 for (int i = 0; i < _oneShotSources.Length; i++)
                     _oneShotSources[i] = _al.GenSource();
@@ -156,6 +160,38 @@ namespace CosmicShore.Client
             return data;
         }
 
+        /// <summary>1s skim shimmer: glassy detuned fifths tremolo — the trail-charging glow.</summary>
+        static short[] SkimLoop()
+        {
+            var data = Alloc(1f);
+            for (int i = 0; i < data.Length; i++)
+            {
+                float t = i / (float)SampleRate;
+                float tremolo = 0.6f + 0.4f * MathF.Sin(MathF.Tau * 6f * t);
+                float v = MathF.Sin(MathF.Tau * 1318.5f * t) * 0.4f      // E6
+                        + MathF.Sin(MathF.Tau * 1975.5f * t) * 0.25f     // B6
+                        + MathF.Sin(MathF.Tau * 1320.8f * t) * 0.2f;     // detune beat
+                data[i] = Clip(v * tremolo * 0.35f);
+            }
+            return data;
+        }
+
```

</details>

### `d25b32b4b` — feat(port): vessel-layer V8 + V9 — VesselTransformer flight model, ShipHelper, theme/material sets, pooling

_Claude, 2026-06-12 00:17:04 +0000_

```text
V8: VesselTransformer verbatim (518L) — accumulated-rotation pitch/yaw/roll,
Slerp orientation, throttle/boost/charged-boost composition, analog drift
(single/sharp tiers, non-gamepad easing, course decoupling), throttle/velocity
modifier stacks with engine-flare hooks, pose controls, reset. 10 flight-model
tests. #10c partial restore: IVesselStatus.VesselTransformer live.

V9: engine E9 ObjectPool<T>; MonoBehaviour.destroyCancellationToken; ColorUsage
attribute; Camera.backgroundColor; Instantiate(original, parent, worldSpace).
Ported verbatim: ShipHelper (VesselHelper.cs), ThemeManagerDataContainerSO,
SO_MaterialSet, SO_ColorSet, VesselCustomization, GenericPoolManager (documented
GameTask substitutions for UniTask call sites); InputEventShipActionMapping +
ResourceEventShipActionMapping extracted per Deviation #8 pattern. #10c partial
restore: Customization live. 12 new tests.

632 tests green (380 xunit + 252 NUnit); headless client smoke unaffected.
```

```text
 Port/PORT_PLAN.md                                                     |  33 +-
 Port/src/CosmicShore.Engine/Attributes.cs                             |  10 +
 Port/src/CosmicShore.Engine/Collections/ObjectPool.cs                 |  89 ++++++
 Port/src/CosmicShore.Engine/Object.cs                                 |  14 +
 Port/src/CosmicShore.Engine/Rendering/Camera.cs                       |   1 +
 Port/src/CosmicShore.Engine/SceneGraph/MonoBehaviour.cs               |  14 +
 .../Controller/Managers/ThemeManagerDataContainerSO.cs                |  99 ++++++
 Port/src/CosmicShore.Game/Controller/Vessel/IVesselStatus.cs          |   4 +-
 Port/src/CosmicShore.Game/Controller/Vessel/R_VesselActionMappings.cs |  24 ++
 Port/src/CosmicShore.Game/Controller/Vessel/VesselCustomization.cs    |  65 ++++
 Port/src/CosmicShore.Game/Controller/Vessel/VesselHelper.cs           | 168 +++++++++++
 Port/src/CosmicShore.Game/Controller/Vessel/VesselTransformer.cs      | 517 ++++++++++++++++++++++++++++++++
 Port/src/CosmicShore.Game/ScriptableObjects/SO_ColorSet.cs            |  79 +++++
 Port/src/CosmicShore.Game/ScriptableObjects/SO_MaterialSet.cs         |  29 ++
 .../CosmicShore.Game/Utility/PoolsAndBuffers/GenericPoolManager.cs    | 241 +++++++++++++++
 Port/tests/CosmicShore.Tests/VesselLayerTestDoubles.cs                |  23 +-
 Port/tests/CosmicShore.Tests/VesselLayerV9Tests.cs                    | 277 +++++++++++++++++
 Port/tests/CosmicShore.Tests/VesselTransformerTests.cs                | 216 +++++++++++++
 18 files changed, 1885 insertions(+), 18 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 2096 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 3c60effc6..952bd492a 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -357,6 +357,24 @@ Next: V8 (VesselTransformer + member restore), rival balance from prompter feedb
   vessel layer as a dedicated multi-iteration arc (survey → engine Material/Pose →
   leaf classes → interfaces → restore deviations).
 
+- **Iteration 8** (2026-06-12, double): **V8 + V9 in one pass.** V8: `VesselTransformer`
+  ported verbatim (518L — the flight model: accumulated-rotation pitch/yaw/roll,
+  Slerp-smoothed orientation, throttle/boost/charged-boost speed composition, analog
+  drift with single/sharp tiers + non-gamepad MoveTowards easing + course decoupling,
+  throttle/velocity modifier stacks with engine-flare hooks, pose controls, reset);
+  10 flight-model tests freeze throttle convergence, boost composition, slow/velocity
+  modifiers, drift scaling + course lag, pose, reset, and gating. V9: engine E9
+  `ObjectPool<T>` + `MonoBehaviour.destroyCancellationToken` + `ColorUsage` +
+  `Camera.backgroundColor` + `Instantiate(original, parent, worldSpace)`; ported
+  verbatim: `ShipHelper` (VesselHelper.cs), `ThemeManagerDataContainerSO`,
+  `SO_MaterialSet`, `SO_ColorSet` (+DomainColorSet/EnvironmentColorSet),
+  `VesselCustomization`, `GenericPoolManager` (documented GameTask substitutions);
+  mapping structs extracted from R_VesselActionHandler per Deviation #8 pattern.
+  **#10c partial restores: VesselTransformer (V8) + Customization (V9) members live.**
+  12 new tests (pool semantics, pool-manager lifecycle, ShipHelper action wiring +
+  material application + theme push, customization paint). **632 tests green
+  (380 + 252)**; client smoke unaffected.
+
 - **Infra rescue** (2026-06-11, fresh session): prompter's first from-source run
   (`dotnet run --project Port\src\CosmicShore.Client`) failed — "Couldn't find a
   project to run". Root cause: the Unity root `.gitignore` ignores `*.csproj`/`*.sln`,
@@ -405,16 +423,15 @@ Next: V8 (VesselTransformer + member restore), rival balance from prompter feedb
   matrix, InputController Awake wiring, shape-key theory, flare, Update routing).
   **610 tests green (358 + 252)**; client smoke unaffected.
 
-## NEXT UP (iteration 8)
+## NEXT UP (iteration 9)
 
-Goal: V8 of the vessel-layer arc + sprint feedback.
+Goal: V10 of the vessel-layer arc + sprint feedback.
 
-1. **V8**: port `VesselTransformer` (518L, single-file oversize accepted) +
-   uncomment its IVesselStatus member (#10c partial restore). Behavior tests for the
-   flight model (AngleAxis rotation, throttle/boost composition, modifier structs).
-2. Sprint: rival balance from prompter feedback (S4 KNOWN ISSUE — rival can't beat a
-   perfect autopilot; vs humans it contests missed crystals). Tune overtake/rubber-band
-   so AI-vs-AI demos stay competitive.
+1. **V10**: port `GameDataSO` (~783L, single-file oversize accepted) + engine
+   `ISession` placeholder. Then V11 (CellConfigDataSO, BlockDensityGrid,
+   CellRuntimeDataSO) if the iteration has room.
+2. Sprint: react to prompter feedback on the S6 field race (drift feel, element
+   balance, rival pressure); fauna ambience / more hulls / shape modes on request.
 3. Grow the CLI toward an M2 vertical slice: one cell + crystals + 2 scripted vessels
    exchanging resource/elemental state on a seeded run.
 4. Update this file (status tables, iteration log, NEXT UP), commit, push.
diff --git a/Port/src/CosmicShore.Engine/Attributes.cs b/Port/src/CosmicShore.Engine/Attributes.cs
index d6e0e025c..42e748553 100644
--- a/Port/src/CosmicShore.Engine/Attributes.cs
+++ b/Port/src/CosmicShore.Engine/Attributes.cs
@@ -73,6 +73,16 @@ namespace CosmicShore.Engine
         public AddComponentMenuAttribute(string menuName) { this.menuName = menuName; }
     }
 
+    /// <summary>HDR/alpha color picker hint for serialized Color fields (inert at runtime).</summary>
+    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
+    public sealed class ColorUsageAttribute : Attribute
+    {
+        public readonly bool showAlpha;
+        public readonly bool hdr;
+        public ColorUsageAttribute(bool showAlpha) { this.showAlpha = showAlpha; }
+        public ColorUsageAttribute(bool showAlpha, bool hdr) { this.showAlpha = showAlpha; this.hdr = hdr; }
+    }
+
     /// <summary>
     /// Previous serialized name of a field — read by the asset pipeline (content phase)
     /// to migrate data written under the old name.
diff --git a/Port/src/CosmicShore.Engine/Collections/ObjectPool.cs b/Port/src/CosmicShore.Engine/Collections/ObjectPool.cs
new file mode 100644
index 000000000..45825dcbc
--- /dev/null
+++ b/Port/src/CosmicShore.Engine/Collections/ObjectPool.cs
@@ -0,0 +1,89 @@
```

</details>

### `c195ebeb4` — docs(port): README layout reflects Game/Cli/Client projects, dist, artifacts, both test suites

_Claude, 2026-06-12 00:35:57 +0000_

```text
 Port/README.md | 11 +++++++++--
 1 file changed, 9 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Port/README.md b/Port/README.md
index 1634fb918..c934e1a65 100644
--- a/Port/README.md
+++ b/Port/README.md
@@ -28,11 +28,18 @@ dotnet test
 Port/
 ├── PORT_PLAN.md                 # master inventory, phase roadmap, live status — START HERE
 ├── CosmicShore.slnx
+├── docs/                        # ENGINE_CORE.md, VESSEL_LAYER.md (arc survey + sequence)
 ├── src/
 │   ├── CosmicShore.Engine/      # first-party engine layer (Unity replacement)
-│   └── CosmicShore.Data/        # ported Data layer (verbatim from Assets/_Scripts/Data)
+│   ├── CosmicShore.Data/        # ported Data layer (verbatim from Assets/_Scripts/Data)
+│   ├── CosmicShore.Game/        # ported game code (mirrors Assets/_Scripts structure)
+│   ├── CosmicShore.Cli/         # headless smoke/sim harness (engine boot, SOAP, sims)
+│   └── CosmicShore.Client/      # playable SkimRace window (Silk.NET, sprint builds)
+├── dist/                        # playable progress-build zips (see play-latest.bat)
+├── artifacts/                   # curated headless render verifications
 └── tests/
-    └── CosmicShore.Tests/       # xunit suite (enum freezes, math, SOAP, networking, stats)
+    ├── CosmicShore.Tests/        # xunit suite (engine, vessel layer, enum freezes)
+    └── CosmicShore.Tests.Ported/ # NUnit 3 suite (Unity EditMode tests, verbatim)
 ```
 
 ## Porting conventions
```

</details>

### `b88e75b17` — feat(port): salvage V10/V11 agent output + finish-state victory lap

_Claude, 2026-06-12 01:34:51 +0000_

```text
Session-limit handoff: parallel port agents were cut off mid-iteration; their
compiling output is preserved — engine ISession + NetworkManager stubs (V10 prep),
engine Vector3Int + Sprite, BlockDensityGrid, CellConfigDataSO (V11 partial,
CellRuntimeDataSO + tests still open). V13/V14 produced no files. Client: finished
pilots now fly a victory lap around the circuit instead of gliding into the void.
PORT_PLAN NEXT UP documents the resume point.

632 tests green; client smoke clean.
```

```text
 Port/PORT_PLAN.md                                                    |   9 +
 Port/src/CosmicShore.Client/SkimRaceSim.cs                           |  16 +-
 Port/src/CosmicShore.Engine/Math/Vector3Int.cs                       |  74 +++++
 Port/src/CosmicShore.Engine/Networking/ISession.cs                   |  28 ++
 Port/src/CosmicShore.Engine/Networking/NetworkManager.cs             |  21 ++
 Port/src/CosmicShore.Engine/Rendering/Sprite.cs                      |  12 +
 Port/src/CosmicShore.Game/Controller/Managers/BlockDensityGrid.cs    | 475 +++++++++++++++++++++++++++++++++
 Port/src/CosmicShore.Game/Utility/DataContainers/CellConfigDataSO.cs |  40 +++
 8 files changed, 673 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 733 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 952bd492a..a6c5cc63e 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -425,6 +425,15 @@ Next: V8 (VesselTransformer + member restore), rival balance from prompter feedb
 
 ## NEXT UP (iteration 9)
 
+**Session-limit handoff (2026-06-12 ~00:30 UTC):** four parallel port agents
+(V10 GameDataSO, V11 cells, V13 prism managers, V14 trail layer) were cut off by
+the session usage limit (resets 01:30 UTC). Salvaged into the branch (compiles
+green, NOT complete): engine `ISession`+`NetworkManager` stubs (V10 prep), engine
+`Vector3Int`+`Sprite`, `BlockDensityGrid`, `CellConfigDataSO` (V11, missing
+CellRuntimeDataSO + tests). V13/V14 produced no files before cutoff. Client gained
+a finish-state victory lap (vessels cruise the circuit under the scoreboard).
+Resume by FINISHING V10 + V11 against the salvage, then V13/V14.
+
 Goal: V10 of the vessel-layer arc + sprint feedback.
 
 1. **V10**: port `GameDataSO` (~783L, single-file oversize accepted) + engine
diff --git a/Port/src/CosmicShore.Client/SkimRaceSim.cs b/Port/src/CosmicShore.Client/SkimRaceSim.cs
index ef1359467..ed53d0c6b 100644
--- a/Port/src/CosmicShore.Client/SkimRaceSim.cs
+++ b/Port/src/CosmicShore.Client/SkimRaceSim.cs
@@ -278,8 +278,20 @@ namespace CosmicShore.Client
                     if (Countdown <= 0f) State = RaceState.Racing;
                     return;
                 case RaceState.Finished:
-                    transform.position += Course * (Speed * Time.deltaTime);
-                    Speed = Mathf.Lerp(Speed, MinimumSpeed, Time.deltaTime);
+                    // victory lap: ease onto the circuit and keep cruising it, so the
+                    // scoreboard sits over the glowing prismscape instead of empty space
+                    {
+                        float lapAngle = SkimTrack.AngleOf(transform.position);
+                        var to = (Track.PointAt(lapAngle + 0.25f) - transform.position).normalized;
+                        if (to.sqrMagnitude > 0.001f)
+                            transform.rotation = Quaternion.Slerp(transform.rotation,
+                                Quaternion.LookRotation(to, Vector3.up),
+                                1f - MathF.Exp(-2f * Time.deltaTime));
+                        Course = Vector3.Slerp(Course, transform.forward,
+                            1f - MathF.Exp(-DriftCourseAlignFast * Time.deltaTime)).normalized;
+                        transform.position += Course * (Speed * Time.deltaTime);
+                        Speed = Mathf.Lerp(Speed, 32f, Time.deltaTime);
+                    }
                     return;
             }
 
diff --git a/Port/src/CosmicShore.Engine/Math/Vector3Int.cs b/Port/src/CosmicShore.Engine/Math/Vector3Int.cs
new file mode 100644
index 000000000..11bddbe45
--- /dev/null
+++ b/Port/src/CosmicShore.Engine/Math/Vector3Int.cs
@@ -0,0 +1,74 @@
+using System;
+
+namespace CosmicShore.Engine
+{
+    /// <summary>
+    /// Integer 3-component vector (voxel/grid indices). Engine addition for
+    /// vessel-layer V11 (BlockDensityGrid voxel indexing). Mirrors the slice of the
+    /// original engine API the ported code uses: component access, RoundToInt /
+    /// FloorToInt / CeilToInt construction from a Vector3, arithmetic, equality, and
+    /// the implicit widening conversion to Vector3.
+    /// </summary>
+    [Serializable]
+    public struct Vector3Int : IEquatable<Vector3Int>
+    {
+        public int x;
+        public int y;
+        public int z;
+
+        public Vector3Int(int x, int y, int z) { this.x = x; this.y = y; this.z = z; }
+
+        public static Vector3Int zero => new(0, 0, 0);
+        public static Vector3Int one => new(1, 1, 1);
+
+        public int this[int index]
+        {
+            get => index switch
+            {
```

</details>

### `503c0633a` — build(port): ship victory-lap build — finished pilots cruise the circuit under the scoreboard

_Claude, 2026-06-12 01:41:30 +0000_

```text
Long-run verification produced a 30-29 photo finish (rival won): the drift-charging
AI field is competitive with a perfect autopilot. Artifact committed.
```

```text
 Port/artifacts/skimrace_s6_photo_finish.png | Bin 0 -> 189613 bytes
 Port/dist/CosmicShore-Windows.zip           | Bin 33911063 -> 33924421 bytes
 Port/dist/SkimRace-Windows.zip              | Bin 33911636 -> 33924994 bytes
 3 files changed, 0 insertions(+), 0 deletions(-)
```

### `ca7ff9206` — docs(port): mark vessel-arc progress — V1-V9 done, V10/V11/V13/V14 in flight

_Claude, 2026-06-12 01:43:24 +0000_

```text
 Port/docs/VESSEL_LAYER.md | 26 +++++++++++++-------------
 1 file changed, 13 insertions(+), 13 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Port/docs/VESSEL_LAYER.md b/Port/docs/VESSEL_LAYER.md
index 4947e3970..1ea3bc816 100644
--- a/Port/docs/VESSEL_LAYER.md
+++ b/Port/docs/VESSEL_LAYER.md
@@ -168,20 +168,20 @@ a file is indivisible). Engine additions don't count against the budget.
 
 | It | Steps | Game lines | Closes |
 |---|---|---|---|
-| **V1** | 1. Engine E3, E4, E5, E6, E7, E8(SO-clone), E10. 2. Port PauseSystem, SafeLookRotation, Singleton.cs, NetMarkers, BoostChangedPayload(+event), InputEventBlockPayload(+event), CellItem. | ~315 | — |
-| **V2** | 3. Engine E1 (input shim). 4. Port IInputStatus (Deviation #10a), IInputStrategy, BaseInputStrategy, KeyboardInputStrategy. | ~430 | — |
-| **V3** | 5. TouchInputStrategy, GamepadInputStrategy. | ~607 | — |
-| **V4** | 6. DualMouseInputStrategy, MultiMouseService, DeviceOrientationHandler. | ~543 | — |
-| **V5** | 7. GameSetting. 8. InputController (Deviation #10b), restore #10a. | ~557 | #10a |
-| **V6** | 9. **Keystone**: ITransform, IVessel, IPlayer, IVesselStatus (Deviation #10c), ElementalFloat, ElementalShipComponent, IVesselHUDController, R_ShipElementStatsHandler, ShipActionSO, ShipActionExecutorBase, ActionExecutorRegistry, ShipAction (legacy); restore #10b. 10. **Restore Deviation #9** (ResourceSystem : ElementalShipComponent + RequireComponent). Tests: ElementalFloat LerpUnclamped scaling, reflective binding, ResourceSystem regression. | ~520 | #10b, **#9** |
-| **V7** | 11. Engine E2 (renderer stubs). 12. InputStatus, VesselAnimation (+member restore). | ~444 | — |
-| **V8** | 13. VesselTransformer (+member restore). | ~518 | — |
-| **V9** | 14. Engine E9. 15. ShipHelper, ThemeManagerDataContainerSO, VesselCustomization (+restore), GenericPoolManager. | ~562 | — |
-| **V10** | 16. GameDataSO (single-file oversize) + engine `ISession` placeholder. | ~783 | — |
-| **V11** | 17. CellConfigDataSO, BlockDensityGrid, CellRuntimeDataSO. | ~710 | — |
+| **V1** ✅ | 1. Engine E3, E4, E5, E6, E7, E8(SO-clone), E10. 2. Port PauseSystem, SafeLookRotation, Singleton.cs, NetMarkers, BoostChangedPayload(+event), InputEventBlockPayload(+event), CellItem. | ~315 | — |
+| **V2** ✅ | 3. Engine E1 (input shim). 4. Port IInputStatus (Deviation #10a), IInputStrategy, BaseInputStrategy, KeyboardInputStrategy. | ~430 | — |
+| **V3** ✅ | 5. TouchInputStrategy, GamepadInputStrategy. | ~607 | — |
+| **V4** ✅ | 6. DualMouseInputStrategy, MultiMouseService, DeviceOrientationHandler. | ~543 | — |
+| **V5** ✅ | 7. GameSetting. 8. InputController (Deviation #10b), restore #10a. | ~557 | #10a |
+| **V6** ✅ | 9. **Keystone**: ITransform, IVessel, IPlayer, IVesselStatus (Deviation #10c), ElementalFloat, ElementalShipComponent, IVesselHUDController, R_ShipElementStatsHandler, ShipActionSO, ShipActionExecutorBase, ActionExecutorRegistry, ShipAction (legacy); restore #10b. 10. **Restore Deviation #9** (ResourceSystem : ElementalShipComponent + RequireComponent). Tests: ElementalFloat LerpUnclamped scaling, reflective binding, ResourceSystem regression. | ~520 | #10b, **#9** |
+| **V7** ✅ | 11. Engine E2 (renderer stubs). 12. InputStatus, VesselAnimation (+member restore). | ~444 | — |
+| **V8** ✅ | 13. VesselTransformer (+member restore). | ~518 | — |
+| **V9** ✅ | 14. Engine E9. 15. ShipHelper, ThemeManagerDataContainerSO, VesselCustomization (+restore), GenericPoolManager. | ~562 | — |
+| **V10** 🔄 | 16. GameDataSO (single-file oversize) + engine `ISession` placeholder. | ~783 | — |
+| **V11** 🔄 | 17. CellConfigDataSO, BlockDensityGrid, CellRuntimeDataSO. | ~710 | — |
 | **V12** | 18. Cell (single-file oversize; flora/fauna touchpoints stay event-shaped per ECOSYSTEM rules — conserved mass untouched). | ~825 | — |
-| **V13** | 19. PrismStateManager, PrismTeamManager, PrismScaleAnimator. | ~474 | — |
-| **V14** | 20. MaterialPropertyAnimator, Trail, PrismProperties, TrailFollower. | ~587 | — |
+| **V13** 🔄 | 19. PrismStateManager, PrismTeamManager, PrismScaleAnimator. | ~474 | — |
+| **V14** 🔄 | 20. MaterialPropertyAnimator, Trail, PrismProperties, TrailFollower. | ~587 | — |
 | **V15** | 21. AudioSystem shell (Deviation #11), PrismAOERegistry (managed-array port). 22. Prism (+`AttachedPrism` restore). | ~925* | — |
 | **V16** | 23. NudgeShardPoolManager, NudgeShard, Skimmer (+skimmer member restores). 24. DriftTrailActionExecutor. | ~347 | — |
 | **V17** | 25. VesselPrismController (+restore). 26. R_VesselActionHandler (+restore). | ~696 | — |
```

</details>

### `d453deebd` — docs(port): record victory-lap ship + 30-29 photo-finish balance datum

_Claude, 2026-06-12 01:44:54 +0000_

```text
 Port/PORT_PLAN.md | 5 ++++-
 1 file changed, 4 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index a6c5cc63e..2527d0786 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -184,7 +184,10 @@ position, crystals), **skim shimmer + drift rush audio layers** (gain/pitch foll
 contact strength and trigger pull). Verified headlessly: 4-pilot race, frame-1300
 diag `crystals [24,19,4,1], P1 lap 2, levels C13/M6/S8/T6, skim True, trail 5004`
 — the field is COMPETITIVE now (drift-charging rivals; was 30-9 in S5). Artifact:
-`artifacts/skimrace_s6_field_elements.png`. NEXT: prompter verifies the field race
+`artifacts/skimrace_s6_field_elements.png`. Post-S6: finished pilots now fly a
+victory lap (scoreboard renders over the prismscape, not the void); long-run verify
+produced a **30-29 photo finish with the rival winning** — the drift-charging field
+is competitive with a perfect autopilot (`artifacts/skimrace_s6_photo_finish.png`). NEXT: prompter verifies the field race
 feel (drift, elements, rival pressure), Squirrel nose orientation in motion (flip
 is one sign in the extractor); deeper Cosmic Shore content (more vessel classes,
 cells/fauna ambience, shape modes) on request. Fidelity arc: **V1 DONE** (engine E3-E8/E10: RPC attrs, profiling shim, Screen/
```

</details>

### `563888e47` — feat(port): vessel-layer V11 complete — CellRuntimeDataSO verbatim + Crystal shell + 23 cell-layer tests

_Claude, 2026-06-12 01:50:23 +0000_

```text
- CellRuntimeDataSO (211L) verbatim; deviations inline-marked: GameDataSO field
  (restore when GameDataSO ports), Cell member + CellTransform + reset line
  (restore at V12), LocalPlayer domain fallback substituted with its own
  null-path value (Domains.Blue)
- Crystal type-preserving shell (CellItem subclass) until the real ~430L
  Crystal ports — same precedent as the AudioSystem shell
- salvaged BlockDensityGrid + CellConfigDataSO verified verbatim vs Unity
  source (managed-array conversion only)
- CellLayerTests: 23 tests — grid math (world/voxel round-trips, density
  add/remove guards, smoothing+argmax+mean-shift centre resolution, cache
  policy, Domains.Blue wildcard bucket), config defaults + phase hysteresis
  thresholds, runtime stats/phase events, crystal registry + reset semantics

655 tests green (403 xunit + 252 NUnit). Agent-ported in isolated worktree;
integrated after independent build+test gate.
```

```text
 Port/src/CosmicShore.Game/Controller/Environment/FlowField/Crystal.cs |  15 ++
 Port/src/CosmicShore.Game/Utility/DataContainers/CellRuntimeDataSO.cs | 213 +++++++++++++++
 Port/tests/CosmicShore.Tests/CellLayerTests.cs                        | 444 ++++++++++++++++++++++++++++++++
 3 files changed, 672 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 690 lines)</summary>

```diff
diff --git a/Port/src/CosmicShore.Game/Controller/Environment/FlowField/Crystal.cs b/Port/src/CosmicShore.Game/Controller/Environment/FlowField/Crystal.cs
new file mode 100644
index 000000000..e41760c7a
--- /dev/null
+++ b/Port/src/CosmicShore.Game/Controller/Environment/FlowField/Crystal.cs
@@ -0,0 +1,15 @@
+// PORT type-preserving SHELL (V11) — the full Crystal
+// (Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs, ~430L MonoBehaviour with
+// materials/VFX/coroutines/UniTask explode pipeline) is not yet scheduled in the porting
+// sequence, but CellRuntimeDataSO's crystal registry needs the TYPE: everything it touches
+// (Id, ownDomain) lives on CellItem, plus MonoBehaviour members (transform, gameObject,
+// lifetime bool). Precedent: AudioSystem shell (Deviation #11), BlockDensityGrid's
+// MonoBehaviour stand-in for Prism. When the real Crystal ports, replace this file in place.
+using CosmicShore.Engine;
+
+namespace CosmicShore.Gameplay
+{
+    public class Crystal : CellItem
+    {
+    }
+}
diff --git a/Port/src/CosmicShore.Game/Utility/DataContainers/CellRuntimeDataSO.cs b/Port/src/CosmicShore.Game/Utility/DataContainers/CellRuntimeDataSO.cs
new file mode 100644
index 000000000..95397aa6d
--- /dev/null
+++ b/Port/src/CosmicShore.Game/Utility/DataContainers/CellRuntimeDataSO.cs
@@ -0,0 +1,213 @@
+using System.Collections.Generic;
+using System.Linq;
+using CosmicShore.Gameplay;
+using CosmicShore.ScriptableObjects;
+using CosmicShore.Engine.Soap;
+using CosmicShore.Engine;
+using CosmicShore.Utility;
+using CosmicShore.Data;
+namespace CosmicShore.Utility
+{
+    [CreateAssetMenu(
+        fileName = "DataContainer_" + nameof(CellRuntimeDataSO),
+        menuName = "ScriptableObjects/Data Containers/" + nameof(CellRuntimeDataSO))]
+    public class CellRuntimeDataSO : ScriptableObject
+    {
+        // ---------------------------------------------------------------------
+        // References (runtime)
+        // ---------------------------------------------------------------------
+
+        [Header("Design Time References")]
+        // PORT Deviation (V11, restore when GameDataSO ports): [SerializeField] GameDataSO gameData;
+        [SerializeField] public ScriptableEventNoParam OnResetForReplay;
+        [SerializeField] public ScriptableEventNoParam OnCrystalSpawned;
+        [SerializeField] public ScriptableEventNoParam OnCellItemsUpdated;
+        [SerializeField] public ScriptableEventCellPhase OnPhaseChanged;
+
+        [Header("Run Time References")]
+        public CellConfigDataSO Config; // <- your "CellConfigData"
+
+        // ---------------------------------------------------------------------
+        // Runtime State
+        // ---------------------------------------------------------------------
+
+        public Dictionary<int, CellStats> CellStatsList = new();
+
+        // PORT Deviation (V11, restore when Cell ports): public Cell Cell;
+        // PORT Deviation (V11, restore when Cell ports): public Transform CellTransform => Cell ? Cell.transform : null;
+
+        public List<CellItem> CellItems = new();
+        public List<Crystal> Crystals = new();
+
+        // ---------------------------------------------------------------------
+        // Public API
+        // ---------------------------------------------------------------------
+
+        public void AddCrystalToList(Crystal crystal)
+        {
+            if (!crystal) return;
+
+            CellItems.Add(crystal);
+            Crystals.Add(crystal);
+
+            OnCellItemsUpdated.Raise();
```

</details>

### `91c938ed4` — feat(port): vessel-layer V13 — prism manager trio + PrismTimerManager, 27 tests

_Claude, 2026-06-12 01:51:55 +0000_

```text
- PrismStateManager (BlockState transitions, shield/danger/super-shield, timed
  deactivation via PrismTimerManager), PrismTeamManager (domain bookkeeping,
  SetInitialTeam/ChangeTeam/Steal + PrismStats payload), PrismScaleAnimator
  (authored-scale capture, per-axis clamping, growth animation, lossy-scale
  volume) — all verbatim
- PrismTimerManager (129L) ported as scope addition: PrismStateManager's timer
  contract lives there and all its deps were already ported
- deviations inline-tagged with restore points: Prism (V15), PrismProperties/
  MaterialPropertyAnimator (V14), PrismScaleManager + PrismOctahedronShield
  (unscheduled); conserved-mass volume delta reports 0 until PrismProperties
  lands rather than inventing different bookkeeping
- 27 tests: state transitions incl. GameLoop-driven timers, team change/steal
  events, scale clamping + volume math, singleton hygiene

682 tests green (430 xunit + 252 NUnit). Agent-ported in isolated worktree;
integrated after independent build+test gate.
```

```text
 .../Controller/Environment/Prisms/PrismScaleAnimator.cs               | 179 +++++++++++++
 Port/src/CosmicShore.Game/Controller/Managers/PrismStateManager.cs    | 175 +++++++++++++
 Port/src/CosmicShore.Game/Controller/Managers/PrismTeamManager.cs     | 130 ++++++++++
 Port/src/CosmicShore.Game/Controller/Managers/PrismTimerManager.cs    | 132 ++++++++++
 Port/tests/CosmicShore.Tests/PrismManagerTests.cs                     | 435 ++++++++++++++++++++++++++++++++
 5 files changed, 1051 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 1081 lines)</summary>

```diff
diff --git a/Port/src/CosmicShore.Game/Controller/Environment/Prisms/PrismScaleAnimator.cs b/Port/src/CosmicShore.Game/Controller/Environment/Prisms/PrismScaleAnimator.cs
new file mode 100644
index 000000000..4594885f5
--- /dev/null
+++ b/Port/src/CosmicShore.Game/Controller/Environment/Prisms/PrismScaleAnimator.cs
@@ -0,0 +1,179 @@
+using CosmicShore.Engine;
+using System;
+using CosmicShore.ScriptableObjects;
+using CosmicShore.Gameplay;
+using CosmicShore.Utility;
+
+namespace CosmicShore.Gameplay
+{
+    // PORT Deviation (V13, restore when Prism ports): [RequireComponent(typeof(Prism))]
+    public class PrismScaleAnimator : MonoBehaviour
+    {
+        [SerializeField] ScriptableEventPrismStats onPrismVolumeModified;
+
+        [Header("Scale Constraints")]
+        [SerializeField] private Vector3 minScale = new Vector3(0.5f, 0.5f, 0.5f);
+        [SerializeField] private Vector3 maxScale = new Vector3(10f, 10f, 10f);
+
+        [Header("Defaults")]
+        [SerializeField] private bool usePrefabScaleAsDefaultTarget;
+        [SerializeField] private Vector3 authoredTargetScale;
+        public Vector3 MinScale => minScale;
+        public Vector3 MaxScale { get => maxScale; set => maxScale = value; }
+
+        public Vector3 TargetScale { get; private set; }
+        public Vector3 AuthoredTargetScale => authoredTargetScale;
+        public float GrowthRate { get; set; } = 0.01f;
+
+        // PORT Deviation (V13, restore when Prism ports): private Prism prism;
+        private MeshRenderer meshRenderer;
+        private bool isRegistered;
+
+        private bool isScaling;
+        public bool IsScaling
+        {
+            get => isScaling;
+            set
+            {
+                if (isScaling.Equals(value)) return;
+                isScaling = value;
+
+                // PORT Deviation (V13, restore when PrismScaleManager ports): if (isScaling) PrismScaleManager.Instance?.OnBlockStartScaling(this);
+                // PORT Deviation (V13, restore when PrismScaleManager ports): else PrismScaleManager.Instance?.OnBlockStopScaling(this);
+            }
+        }
+
+        private void Awake()
+        {
+            meshRenderer = GetComponent<MeshRenderer>();
+            // PORT Deviation (V13, restore when Prism ports): prism = GetComponent<Prism>();
+
+            if (meshRenderer == null)
+            {
+                CSDebug.LogError($"MeshRenderer missing on {gameObject.name}");
+                enabled = false;
+                return;
+            }
+
+            if (authoredTargetScale.Equals(Vector3.zero))
+                authoredTargetScale = transform.localScale;
+
+            if (TargetScale == Vector3.zero)
+                SetTargetScale(authoredTargetScale);
+
+            transform.localScale = Vector3.zero;
+        }
+
+        public void Initialize()
+        {
+            if (isRegistered) return;
+            // PORT Deviation (V13, restore when PrismScaleManager ports): if (!PrismScaleManager.Instance) return;
+            // PORT Deviation (V13, restore when PrismScaleManager ports): PrismScaleManager.Instance.RegisterAnimator(this);
+            isRegistered = true;
+        }
+
```

</details>

### `02cfa93fb` — feat(port): vessel-layer V10 — GameDataSO verbatim (813L) + ScoreResult + engine Random, 22 tests

_Claude, 2026-06-12 01:53:02 +0000_

```text
- GameDataSO ported verbatim: player roster + LocalPlayer wiring, spawn poses
  (incl. seeded random pose path), domain metric sums + change-only events,
  ActiveDomains {Jade,Ruby,Gold} with Blue absent, results/winner derivation,
  golf/points sorting, ResetRuntimeData pre-launch preservation
- deviations inline-marked: ScoringRuleSO member, SyncFromArcadeGame
  (SO_ArcadeGame chain), BuildHumanCounts (concrete Player type) — each keyed
  to its restore point
- ScoreResult struct ported verbatim (leaf dep); engine Random added
  (InitState/Range/value, UnityEngine.Random contract) — client files take a
  one-line System.Random alias each to stay unambiguous
- GameDataSOTests: 22 tests over counts/backfill, domain slices, metric sums,
  AddPlayer/RemovePlayerData, results, sorting, reset

706 tests green (454 xunit + 252 NUnit). Agent-ported in isolated worktree;
integrated after independent build+test gate; client smoke clean.
```

```text
 Port/src/CosmicShore.Client/RaceWindow.cs                      |   1 +
 Port/src/CosmicShore.Client/SkimRaceSim.cs                     |   1 +
 Port/src/CosmicShore.Data/Structs/ScoreResult.cs               |  48 +++
 Port/src/CosmicShore.Engine/Math/Random.cs                     |  30 ++
 Port/src/CosmicShore.Game/Utility/DataContainers/GameDataSO.cs | 811 +++++++++++++++++++++++++++++++++++++++
 Port/tests/CosmicShore.Tests/GameDataSOTests.cs                | 340 ++++++++++++++++
 6 files changed, 1231 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 1277 lines)</summary>

```diff
diff --git a/Port/src/CosmicShore.Client/RaceWindow.cs b/Port/src/CosmicShore.Client/RaceWindow.cs
index 1807f5a0b..95abab2f9 100644
--- a/Port/src/CosmicShore.Client/RaceWindow.cs
+++ b/Port/src/CosmicShore.Client/RaceWindow.cs
@@ -12,6 +12,7 @@ using EngineInput = CosmicShore.Engine.InputSystem;
 using Vector3 = CosmicShore.Engine.Vector3;
 using Quaternion = CosmicShore.Engine.Quaternion;
 using Vector2 = CosmicShore.Engine.Vector2;
+using Random = System.Random; // disambiguate from CosmicShore.Engine.Random (V10 engine addition)
 
 namespace CosmicShore.Client
 {
diff --git a/Port/src/CosmicShore.Client/SkimRaceSim.cs b/Port/src/CosmicShore.Client/SkimRaceSim.cs
index ed53d0c6b..22b170012 100644
--- a/Port/src/CosmicShore.Client/SkimRaceSim.cs
+++ b/Port/src/CosmicShore.Client/SkimRaceSim.cs
@@ -3,6 +3,7 @@ using System.Collections.Generic;
 using CosmicShore.Data;
 using CosmicShore.Engine;
 using CosmicShore.Gameplay;
+using Random = System.Random; // disambiguate from CosmicShore.Engine.Random (V10 engine addition)
 
 namespace CosmicShore.Client
 {
diff --git a/Port/src/CosmicShore.Data/Structs/ScoreResult.cs b/Port/src/CosmicShore.Data/Structs/ScoreResult.cs
new file mode 100644
index 000000000..cb44ba46a
--- /dev/null
+++ b/Port/src/CosmicShore.Data/Structs/ScoreResult.cs
@@ -0,0 +1,48 @@
+namespace CosmicShore.Data
+{
+    /// <summary>
+    /// One row of final, ranked scoring results — the single source of truth for
+    /// "who placed where" that every end-game surface (scoreboard banner + cards,
+    /// end-game cinematic, crystal reward) reads. Produced once by the game mode
+    /// (server-side in networked modes, locally in single-player) and, in networked
+    /// modes, assembled identically on each client from the already-synced score
+    /// arrays. See Docs/ScoringSystem/REFACTOR.md R10.
+    /// </summary>
+    public readonly struct ScoreResult
+    {
+        /// <summary>1-based placement after sorting (1 = best).</summary>
+        public readonly int Rank;
+
+        public readonly string Name;
+
+        public readonly Domains Domain;
+
+        /// <summary>The mode's primary score value (golf rules: lower is better).</summary>
+        public readonly float Score;
+
+        /// <summary>
+        /// Mode-formatted PRIMARY display string — what the scoreboard card's main score
+        /// shows and what the end-game cinematic reveals (winner: "01:24:30"; loser:
+        /// "3 Crystals Left" / "3 Jousts Left"; CrystalCapture: "12 Crystals"). Computed
+        /// once by the producing mode so the scoreboard and the reveal can never disagree
+        /// (this is what dissolves BUGS.md B2).
+        /// </summary>
+        public readonly string ScoreText;
+
+        /// <summary>
+        /// Optional mode-formatted SECONDARY stat line (e.g. "12 Crystals", "7 Jousts"),
+        /// or null when the mode has none. Shown below the primary on the score card.
+        /// </summary>
+        public readonly string Secondary;
+
+        public ScoreResult(int rank, string name, Domains domain, float score, string scoreText, string secondary)
+        {
+            Rank = rank;
+            Name = name;
+            Domain = domain;
+            Score = score;
+            ScoreText = scoreText;
+            Secondary = secondary;
+        }
+    }
+}
diff --git a/Port/src/CosmicShore.Engine/Math/Random.cs b/Port/src/CosmicShore.Engine/Math/Random.cs
new file mode 100644
```

</details>

### `120bfe947` — feat(port): vessel-layer V14 — trail layer verbatim + engine MaterialPropertyBlock; CellRuntimeDataSO GameDataSO deviations restored

_Claude, 2026-06-12 01:55:00 +0000_

```text
- Trail (index/walk math live; Prism-typed members staged), TrailFollower
  (RideTheTrail walking live; Attach inert until Prism), PrismProperties,
  MaterialPropertyAnimator (property-block retarget + completion latching live;
  theme lookups staged) — deviations tagged for Prism (V15),
  MaterialStateManager and GunVesselTransformer (unscheduled)
- engine: MaterialPropertyBlock (PropertyToID-keyed store, copy-on-set
  renderer snapshot), Renderer.Set/GetPropertyBlock, Vector3.Magnitude
- CellRuntimeDataSO: GameDataSO-keyed deviations restored (gameData field +
  LocalPlayer domain path) now that V10 landed; Cell-keyed ones remain for V12
- TrailLayerTests: 22 tests (trail walk/projection incl. ping-pong + loop wrap
  quirks frozen, follower walk/boundary/flip math, animator latching, MPB
  round-trip)

728 tests green (476 xunit + 252 NUnit). V10+V11+V13+V14 all agent-ported in
isolated worktrees, integrated after independent gates.
```

```text
 Port/src/CosmicShore.Engine/Math/Vector3.cs                           |   2 +
 Port/src/CosmicShore.Engine/Rendering/MaterialPropertyBlock.cs        |  53 ++++
 Port/src/CosmicShore.Engine/Rendering/Renderers.cs                    |  17 +
 Port/src/CosmicShore.Game/Controller/Environment/PrismProperties.cs   |  22 ++
 .../Controller/Environment/Prisms/MaterialPropertyAnimator.cs         | 214 +++++++++++++
 Port/src/CosmicShore.Game/Controller/Vessel/Trail.cs                  | 220 +++++++++++++
 Port/src/CosmicShore.Game/Controller/Vessel/TrailFollower.cs          | 149 +++++++++
 Port/src/CosmicShore.Game/Utility/DataContainers/CellRuntimeDataSO.cs |   5 +-
 Port/tests/CosmicShore.Tests/TrailLayerTests.cs                       | 535 ++++++++++++++++++++++++++++++++
 9 files changed, 1214 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1293 lines)</summary>

```diff
diff --git a/Port/src/CosmicShore.Engine/Math/Vector3.cs b/Port/src/CosmicShore.Engine/Math/Vector3.cs
index 1b6d7cdf5..bb6dbddd5 100644
--- a/Port/src/CosmicShore.Engine/Math/Vector3.cs
+++ b/Port/src/CosmicShore.Engine/Math/Vector3.cs
@@ -79,6 +79,8 @@ namespace CosmicShore.Engine
 
         public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;
 
+        public static float Magnitude(Vector3 vector) => vector.magnitude;
+
         public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
         {
             t = Mathf.Clamp01(t);
diff --git a/Port/src/CosmicShore.Engine/Rendering/MaterialPropertyBlock.cs b/Port/src/CosmicShore.Engine/Rendering/MaterialPropertyBlock.cs
new file mode 100644
index 000000000..5eeb9c1b2
--- /dev/null
+++ b/Port/src/CosmicShore.Engine/Rendering/MaterialPropertyBlock.cs
@@ -0,0 +1,53 @@
+using System.Collections.Generic;
+
+namespace CosmicShore.Engine
+{
+    /// <summary>
+    /// Per-renderer material property overrides (colors/floats/vectors keyed by shader
+    /// property ID), preserving the original engine's copy semantics:
+    /// <see cref="Renderer.SetPropertyBlock"/> snapshots the block's contents onto the
+    /// renderer and <see cref="Renderer.GetPropertyBlock"/> overwrites the destination
+    /// with the renderer's current snapshot. Pure data until the presentation phase
+    /// interprets it (same contract as <see cref="Material"/>).
+    /// </summary>
+    public sealed class MaterialPropertyBlock
+    {
+        readonly Dictionary<int, Color> _colors = new();
+        readonly Dictionary<int, float> _floats = new();
+        readonly Dictionary<int, Vector4> _vectors = new();
+
+        public bool isEmpty => _colors.Count == 0 && _floats.Count == 0 && _vectors.Count == 0;
+
+        public void Clear()
+        {
+            _colors.Clear();
+            _floats.Clear();
+            _vectors.Clear();
+        }
+
+        public void SetColor(string name, Color value) => _colors[Shader.PropertyToID(name)] = value;
+        public void SetColor(int nameID, Color value) => _colors[nameID] = value;
+        public Color GetColor(string name) => GetColor(Shader.PropertyToID(name));
+        public Color GetColor(int nameID) => _colors.TryGetValue(nameID, out var v) ? v : Color.clear;
+
+        public void SetFloat(string name, float value) => _floats[Shader.PropertyToID(name)] = value;
+        public void SetFloat(int nameID, float value) => _floats[nameID] = value;
+        public float GetFloat(string name) => GetFloat(Shader.PropertyToID(name));
+        public float GetFloat(int nameID) => _floats.TryGetValue(nameID, out var v) ? v : 0f;
+
+        public void SetVector(string name, Vector4 value) => _vectors[Shader.PropertyToID(name)] = value;
+        public void SetVector(int nameID, Vector4 value) => _vectors[nameID] = value;
+        public Vector4 GetVector(string name) => GetVector(Shader.PropertyToID(name));
+        public Vector4 GetVector(int nameID) => _vectors.TryGetValue(nameID, out var v) ? v : Vector4.zero;
+
+        /// <summary>Overwrite this block's contents with <paramref name="source"/>'s (cleared if null).</summary>
+        internal void CopyFrom(MaterialPropertyBlock source)
+        {
+            Clear();
+            if (source == null) return;
+            foreach (var kv in source._colors) _colors[kv.Key] = kv.Value;
+            foreach (var kv in source._floats) _floats[kv.Key] = kv.Value;
+            foreach (var kv in source._vectors) _vectors[kv.Key] = kv.Value;
+        }
+    }
+}
diff --git a/Port/src/CosmicShore.Engine/Rendering/Renderers.cs b/Port/src/CosmicShore.Engine/Rendering/Renderers.cs
index 2c6967c32..a2d1f897a 100644
--- a/Port/src/CosmicShore.Engine/Rendering/Renderers.cs
+++ b/Port/src/CosmicShore.Engine/Rendering/Renderers.cs
@@ -43,6 +43,23 @@ namespace CosmicShore.Engine
             get => material;
             set => material = value;
         }
```

</details>

### `fc3184133` — fix(port): hull faced backwards + replay button (prompter feedback); arc docs current

_Claude, 2026-06-12 01:59:22 +0000_

```text
- squirrel mesh rotated 180 about up at load (positions+normals; proper
  rotation, winding intact) — nose now leads the direction of travel
- finish screen gains a pulsing replay glyph; A / Enter / R / Start / mouse
  click all start the rematch (R and Start work any time, as before)
- zips rebuilt with updated in-box README controls
- VESSEL_LAYER V10/V11/V13/V14 marked done; PORT_PLAN NEXT UP -> V12+V15

728 tests green; hull + replay verified in headless frames (artifact committed).
```

```text
 Port/PORT_PLAN.md                         |  31 +++++++++++++++++++------------
 Port/artifacts/skimrace_s7_hull_fixed.png | Bin 0 -> 118801 bytes
 Port/dist/CosmicShore-Windows.zip         | Bin 33924421 -> 33938508 bytes
 Port/dist/SkimRace-Windows.zip            | Bin 33924994 -> 33939112 bytes
 Port/docs/VESSEL_LAYER.md                 |   8 ++++----
 Port/src/CosmicShore.Client/RaceWindow.cs |  37 ++++++++++++++++++++++++++++++++++++-
 6 files changed, 59 insertions(+), 17 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 135 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 2527d0786..bb2de2b48 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -426,18 +426,25 @@ Next: V8 (VesselTransformer + member restore), rival balance from prompter feedb
   matrix, InputController Awake wiring, shape-key theory, flare, Update routing).
   **610 tests green (358 + 252)**; client smoke unaffected.
 
-## NEXT UP (iteration 9)
-
-**Session-limit handoff (2026-06-12 ~00:30 UTC):** four parallel port agents
-(V10 GameDataSO, V11 cells, V13 prism managers, V14 trail layer) were cut off by
-the session usage limit (resets 01:30 UTC). Salvaged into the branch (compiles
-green, NOT complete): engine `ISession`+`NetworkManager` stubs (V10 prep), engine
-`Vector3Int`+`Sprite`, `BlockDensityGrid`, `CellConfigDataSO` (V11, missing
-CellRuntimeDataSO + tests). V13/V14 produced no files before cutoff. Client gained
-a finish-state victory lap (vessels cruise the circuit under the scoreboard).
-Resume by FINISHING V10 + V11 against the salvage, then V13/V14.
-
-Goal: V10 of the vessel-layer arc + sprint feedback.
+## NEXT UP (iteration 10)
+
+**Parallel-agent round complete (2026-06-12):** V10 (GameDataSO + ScoreResult +
+engine Random), V11 (CellRuntimeDataSO + Crystal shell; GameDataSO-keyed deviations
+restored same-day), V13 (prism manager trio + PrismTimerManager), V14 (trail layer +
+engine MaterialPropertyBlock) all agent-ported in isolated worktrees and integrated
+behind independent build+test gates. 728 tests green. Prompter feedback shipped:
+hull 180° flip at mesh load (was facing backwards), replay button on the finish
+screen (pulsing glyph; A / Enter / R / Start / click all rematch).
+
+Goal: V12 + V15 (now unblocked) + sprint feedback.
+
+1. **V12**: port `Cell` (~825L, single-file oversize) against the landed cell layer;
+   restore CellRuntimeDataSO's Cell-keyed deviations.
+2. **V15**: `PrismAOERegistry` (managed-array port) + `Prism` (+`AttachedPrism` and
+   all V13/V14 Prism-keyed deviation restores).
+3. Then V16-V19 per VESSEL_LAYER.md (Skimmer, VesselPrismController, AIPilot, camera
+   layer + final #10c close).
+4. Update this file, commit, push.
 
 1. **V10**: port `GameDataSO` (~783L, single-file oversize accepted) + engine
    `ISession` placeholder. Then V11 (CellConfigDataSO, BlockDensityGrid,
diff --git a/Port/docs/VESSEL_LAYER.md b/Port/docs/VESSEL_LAYER.md
index 1ea3bc816..593e3dc11 100644
--- a/Port/docs/VESSEL_LAYER.md
+++ b/Port/docs/VESSEL_LAYER.md
@@ -177,11 +177,11 @@ a file is indivisible). Engine additions don't count against the budget.
 | **V7** ✅ | 11. Engine E2 (renderer stubs). 12. InputStatus, VesselAnimation (+member restore). | ~444 | — |
 | **V8** ✅ | 13. VesselTransformer (+member restore). | ~518 | — |
 | **V9** ✅ | 14. Engine E9. 15. ShipHelper, ThemeManagerDataContainerSO, VesselCustomization (+restore), GenericPoolManager. | ~562 | — |
-| **V10** 🔄 | 16. GameDataSO (single-file oversize) + engine `ISession` placeholder. | ~783 | — |
-| **V11** 🔄 | 17. CellConfigDataSO, BlockDensityGrid, CellRuntimeDataSO. | ~710 | — |
+| **V10** ✅ | 16. GameDataSO (single-file oversize) + engine `ISession` placeholder. | ~783 | — |
+| **V11** ✅ | 17. CellConfigDataSO, BlockDensityGrid, CellRuntimeDataSO. | ~710 | — |
 | **V12** | 18. Cell (single-file oversize; flora/fauna touchpoints stay event-shaped per ECOSYSTEM rules — conserved mass untouched). | ~825 | — |
-| **V13** 🔄 | 19. PrismStateManager, PrismTeamManager, PrismScaleAnimator. | ~474 | — |
-| **V14** 🔄 | 20. MaterialPropertyAnimator, Trail, PrismProperties, TrailFollower. | ~587 | — |
+| **V13** ✅ | 19. PrismStateManager, PrismTeamManager, PrismScaleAnimator. | ~474 | — |
+| **V14** ✅ | 20. MaterialPropertyAnimator, Trail, PrismProperties, TrailFollower. | ~587 | — |
 | **V15** | 21. AudioSystem shell (Deviation #11), PrismAOERegistry (managed-array port). 22. Prism (+`AttachedPrism` restore). | ~925* | — |
 | **V16** | 23. NudgeShardPoolManager, NudgeShard, Skimmer (+skimmer member restores). 24. DriftTrailActionExecutor. | ~347 | — |
 | **V17** | 25. VesselPrismController (+restore). 26. R_VesselActionHandler (+restore). | ~696 | — |
diff --git a/Port/src/CosmicShore.Client/RaceWindow.cs b/Port/src/CosmicShore.Client/RaceWindow.cs
index 95abab2f9..e8c41c1dc 100644
--- a/Port/src/CosmicShore.Client/RaceWindow.cs
+++ b/Port/src/CosmicShore.Client/RaceWindow.cs
@@ -128,7 +128,14 @@ namespace CosmicShore.Client
                 keyboard.KeyDown += (_, key, _) =>
                 {
                     if (key == Key.Escape) _window.Close();
-                    if (key == Key.R)
+                    if (key == Key.R || (key == Key.Enter && _race.State == RaceState.Finished))
+                        SkimRaceFactory.ResetRace(_race.Shared);
+                };
+            // replay button: any click on the finish screen starts the rematch
+            foreach (var mouse in _inputContext.Mice)
+                mouse.MouseDown += (_, _) =>
+                {
+                    if (_race.State == RaceState.Finished)
                         SkimRaceFactory.ResetRace(_race.Shared);
```

</details>

### `6bfdba5d7` — docs(port): from-source is the primary test channel; dist zips on request only

_Claude, 2026-06-12 02:07:32 +0000_

```text
 Port/PORT_PLAN.md | 16 +++++++++-------
 1 file changed, 9 insertions(+), 7 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index bb2de2b48..2099857e0 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -57,7 +57,10 @@ The prompter tests progress without prompting the loop. Contract:
    notification with the exact command (or file) to try. (Annotated `port-mN` tags are
    created locally, but this environment's git proxy only accepts branch pushes — the
    log + commit message are the durable record.)
-5. **Standalone binaries on request / at milestones.** No-install executables build with:
+5. **Standalone binaries ON REQUEST ONLY** (prompter, 2026-06-12: "i don't need the
+   zip anymore my powershell workflow is working great" — from-source is the primary
+   channel; stop refreshing `dist/` zips on build changes; the committed zips remain
+   as a fallback for SDK-less machines). When requested, build with:
    `dotnet publish <proj> -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true`
    (swap `-r` for `linux-x64` / `osx-arm64`; ~39 MB exe; no trimming — the engine's
    reflective lifecycle discovery forbids it). **Do NOT add
@@ -68,12 +71,11 @@ The prompter tests progress without prompting the loop. Contract:
    **zip them together with the exe** (the one historical bad ship was a bare-exe zip
    missing those loose natives). The exe holds its console window open when
    double-clicked (`--no-wait` skips). Delivered into the chat at milestones.
-6. **Fast test loop for the prompter** (replaces manual zip downloads):
-   one-time `git clone` + checkout `claude/quirky-cannon-sk8a02`, then double-click
-   `Port/play-latest.bat` after each push — it pulls, extracts dist, and launches
-   (~10 s). Developer alternative: install the .NET 10 SDK + VC++ redist once, then
-   `git pull && dotnet run --project Port/src/CosmicShore.Client` builds and runs
-   from source incrementally.
+6. **Fast test loop for the prompter — PRIMARY (confirmed working 2026-06-12)**:
+   .NET 10 SDK + VC++ redist installed once, then in PowerShell:
+   `git pull` + `dotnet run -c Release --project Port\src\CosmicShore.Client`
+   builds and runs from source incrementally (seconds per iteration).
+   `Port/play-latest.bat` (zip channel) remains as fallback only.
 7. **Local prerequisites for the prompter** (only for running from source):
    `winget install Microsoft.DotNet.SDK.10` (Windows) / `brew install dotnet-sdk` (macOS),
    then `git fetch origin claude/quirky-cannon-sk8a02 && git checkout claude/quirky-cannon-sk8a02`.
```

</details>

### `365577d21` — feat(port): vessel-layer V12 — Cell verbatim (904L) + CellRuntimeDataSO Cell restores, 23 tests

_Claude, 2026-06-12 02:24:03 +0000_

```text
- Cell ported verbatim: per-domain density grids (incl. Blue wildcard), block
  add/remove + domain-change re-registration, dominant-domain hysteresis phase
  machine with tick gating, authoritative phase/domain single-writer path,
  explosion/densest-region/centroid queries, ActiveCells spatial registry,
  lifeform + live-fauna registries, replay reset — conserved-mass semantics
  carried verbatim (no decay added; struct-copy quirk preserved, not 'fixed')
- deviations tagged for CapsuleMembrane, SnowChanger, SpawnProfileSO,
  CellModifier, spawner interfaces, Fauna config (registry stands in), Prism
  (tracked blocks read PrismTeamManager directly — bookkeeping fully live)
- CellRuntimeDataSO: all three Cell-keyed V11 deviations restored
- engine: Instantiate(original, position, rotation) pose overload
- CellTests: 23 tests incl. phase hysteresis through Update, steal-survival
  of registration snapshots, 4-tier ControllingDomain fallback, spatial queries

751 tests green (499 xunit + 252 NUnit). Agent-ported in isolated worktree.
```

```text
 Port/src/CosmicShore.Engine/Object.cs                                 |  18 +
 Port/src/CosmicShore.Game/Controller/Environment/Cell.cs              | 934 ++++++++++++++++++++++++++++++++
 Port/src/CosmicShore.Game/Utility/DataContainers/CellRuntimeDataSO.cs |   6 +-
 Port/tests/CosmicShore.Tests/CellTests.cs                             | 663 +++++++++++++++++++++++
 4 files changed, 1618 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1662 lines)</summary>

```diff
diff --git a/Port/src/CosmicShore.Engine/Object.cs b/Port/src/CosmicShore.Engine/Object.cs
index 9cb77e93f..1407b8a2c 100644
--- a/Port/src/CosmicShore.Engine/Object.cs
+++ b/Port/src/CosmicShore.Engine/Object.cs
@@ -73,6 +73,24 @@ namespace CosmicShore.Engine
         public static T Instantiate<T>(T original) where T : Object
             => ObjectUtilities.InstantiateObject(original);
 
+        /// <summary>Clone and place at a world pose in one step (cell visuals and spawners use this shape).</summary>
+        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation) where T : Object
+        {
+            var clone = ObjectUtilities.InstantiateObject(original);
+            var transform = clone switch
+            {
+                GameObject go => go.transform,
+                Component component => component.transform,
+                _ => null,
+            };
+            if (transform is not null)
+            {
+                transform.position = position;
+                transform.rotation = rotation;
+            }
+            return clone;
+        }
+
         /// <summary>Clone and parent in one step (pool managers and spawners use this shape).</summary>
         public static T Instantiate<T>(T original, Transform parent, bool instantiateInWorldSpace = false) where T : Object
         {
diff --git a/Port/src/CosmicShore.Game/Controller/Environment/Cell.cs b/Port/src/CosmicShore.Game/Controller/Environment/Cell.cs
new file mode 100644
index 000000000..d336d0b9e
--- /dev/null
+++ b/Port/src/CosmicShore.Game/Controller/Environment/Cell.cs
@@ -0,0 +1,934 @@
+// Cell.cs
+using System.Collections.Generic;
+using System.Linq;
+using CosmicShore.Data;
+// PORT Deviation (V12, restore when CapsuleMembrane ports): using CosmicShore.Game;
+// (CapsuleMembrane is the only type this file uses from the CosmicShore.Game namespace,
+// and that namespace does not exist in the port yet — the directive would not compile.)
+using CosmicShore.Gameplay;
+using CosmicShore.Utility;
+using CosmicShore.Engine.Injection;
+using CosmicShore.Engine;
+using Random = CosmicShore.Engine.Random;
+namespace CosmicShore.Gameplay
+{
+    public class Cell : MonoBehaviour
+    {
+        enum CellTypeChoiceOptions { Random, IntensityWise }
+
+        [SerializeField] public int ID;
+
+        [Header("Cell Config Selection")]
+        [SerializeField] List<CellConfigDataSO> CellConfigs;   // NEW (replaces CellTypes)
+        [SerializeField] CellTypeChoiceOptions cellTypeChoiceOptions = CellTypeChoiceOptions.Random;
+
+        [Header("Runtime Data")]
+        [SerializeField] CellRuntimeDataSO runtime;
+        [Inject] GameDataSO gameData;
+
+        [SerializeField] float nucleusScaleMultiplier = 1f;
+
+        // Local phase recompute interval. Constant rather than a serialized field so
+        // existing scene-placed Cells deserialized before this tick existed don't end
+        // up with phaseTickIntervalSeconds=0 (the default(float) for new serialized
+        // fields), which would silently disable phase advancement.
+        const float PhaseTickIntervalSeconds = 0.5f;
+
+        float _nextPhaseTickAt;
+
+
+        CellConfigDataSO cellConfigData => runtime ? runtime.Config : null;
+        public CellConfigDataSO Config => cellConfigData;
+        GameObject membrane;
+        GameObject nucleus;
+
+        public float NucleusRadius => nucleus ? nucleus.transform.localScale.x : 0f;
```

</details>

### `7edf56632` — feat(port): vessel-layer V15 — Prism + PrismAOERegistry; Cell↔Prism cross-restores live

_Claude, 2026-06-12 02:38:07 +0000_

```text
- Prism (484L) verbatim: pool lifecycle, damage/consume/restore matrix,
  shield/super-shield/devastate states, conserved-mass volume via
  PrismProperties; PrismAOERegistry managed-array port (flag packing, hot/cold
  split, free-list, per-frame damage cap, GuyFawkes anonymous path verbatim)
- restore sweep: Trail/PrismProperties/PrismTeamManager now FULLY VERBATIM
  (zero markers); PrismStateManager/PrismScaleAnimator/TrailFollower/
  MaterialPropertyAnimator restored to their remaining unscheduled keys;
  IVesselStatus.AttachedPrism (#10c) live; BlockDensityGrid signatures back
  to Prism
- cross-restores (post-agent, both directions): Cell's trackedBlocks/AddBlock/
  RemoveBlock/NotifyBlockDomainChanged re-typed Prism verbatim; Prism's
  _registeredCell + RegisterWithCell/UnregisterFromCell live against
  Cell.FindCellContaining — the conserved-mass spine (prism mass → cell
  density grids → phase machine) runs end-to-end
- engine: Mathf.NextPowerOfTwo; shared PrismTestRig in test doubles
- PrismTests (16) + staged-test updates; CellTests adapted to rig prisms

767 tests green (515 xunit + 252 NUnit); client smoke clean.
```

```text
 Port/PORT_PLAN.md                                                     |  36 ++-
 Port/docs/VESSEL_LAYER.md                                             |   4 +-
 Port/src/CosmicShore.Engine/Math/Mathf.cs                             |  13 +
 Port/src/CosmicShore.Game/Controller/Environment/Cell.cs              |  21 +-
 Port/src/CosmicShore.Game/Controller/Environment/PrismProperties.cs   |   2 +-
 .../Controller/Environment/Prisms/MaterialPropertyAnimator.cs         |  36 +--
 .../Controller/Environment/Prisms/PrismScaleAnimator.cs               |  35 +--
 Port/src/CosmicShore.Game/Controller/Managers/BlockDensityGrid.cs     |  14 +-
 Port/src/CosmicShore.Game/Controller/Managers/PrismAOERegistry.cs     | 444 +++++++++++++++++++++++++++++
 Port/src/CosmicShore.Game/Controller/Managers/PrismStateManager.cs    |  79 +++---
 Port/src/CosmicShore.Game/Controller/Managers/PrismTeamManager.cs     |  88 +++---
 Port/src/CosmicShore.Game/Controller/Vessel/IVesselStatus.cs          |   2 +-
 Port/src/CosmicShore.Game/Controller/Vessel/Prism.cs                  | 484 ++++++++++++++++++++++++++++++++
 Port/src/CosmicShore.Game/Controller/Vessel/Trail.cs                  |  36 +--
 Port/src/CosmicShore.Game/Controller/Vessel/TrailFollower.cs          |  21 +-
 Port/tests/CosmicShore.Tests/CellLayerTests.cs                        |  17 +-
 Port/tests/CosmicShore.Tests/CellTests.cs                             |  19 +-
 Port/tests/CosmicShore.Tests/PrismManagerTests.cs                     |  43 +--
 Port/tests/CosmicShore.Tests/PrismTests.cs                            | 373 ++++++++++++++++++++++++
 Port/tests/CosmicShore.Tests/TrailLayerTests.cs                       |  35 ++-
 Port/tests/CosmicShore.Tests/VesselLayerTestDoubles.cs                | 105 +++++++
 21 files changed, 1640 insertions(+), 267 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 2485 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 2099857e0..fd8d41530 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -428,25 +428,23 @@ Next: V8 (VesselTransformer + member restore), rival balance from prompter feedb
   matrix, InputController Awake wiring, shape-key theory, flare, Update routing).
   **610 tests green (358 + 252)**; client smoke unaffected.
 
-## NEXT UP (iteration 10)
-
-**Parallel-agent round complete (2026-06-12):** V10 (GameDataSO + ScoreResult +
-engine Random), V11 (CellRuntimeDataSO + Crystal shell; GameDataSO-keyed deviations
-restored same-day), V13 (prism manager trio + PrismTimerManager), V14 (trail layer +
-engine MaterialPropertyBlock) all agent-ported in isolated worktrees and integrated
-behind independent build+test gates. 728 tests green. Prompter feedback shipped:
-hull 180° flip at mesh load (was facing backwards), replay button on the finish
-screen (pulsing glyph; A / Enter / R / Start / click all rematch).
-
-Goal: V12 + V15 (now unblocked) + sprint feedback.
-
-1. **V12**: port `Cell` (~825L, single-file oversize) against the landed cell layer;
-   restore CellRuntimeDataSO's Cell-keyed deviations.
-2. **V15**: `PrismAOERegistry` (managed-array port) + `Prism` (+`AttachedPrism` and
-   all V13/V14 Prism-keyed deviation restores).
-3. Then V16-V19 per VESSEL_LAYER.md (Skimmer, VesselPrismController, AIPilot, camera
-   layer + final #10c close).
-4. Update this file, commit, push.
+## NEXT UP (iteration 11)
+
+**V12 + V15 integrated (2026-06-12):** Cell (904L) and Prism (484L) + PrismAOERegistry
+(managed-array) agent-ported; cross-restores applied both directions — Cell's block
+surface is Prism-typed verbatim, Prism's cell registration is live (FindCellContaining
+→ AddBlock → density grids → phase machine). Trail, PrismProperties, PrismTeamManager
+fully verbatim, zero markers. 767 tests green.
+
+Goal: V16-V19 finish the vessel arc.
+
+1. **V16**: NudgeShardPoolManager, NudgeShard, Skimmer (+skimmer #10c restores),
+   DriftTrailActionExecutor.
+2. **V17**: VesselPrismController, R_VesselActionHandler (+restores).
+3. **V18**: AIPilot, AICinematicBehavior (+restores).
+4. **V19**: camera layer + VesselImpactor slice + SilhouetteController; close #10c.
+5. CLI M2 vertical slice (cell + prisms + crystals + scripted vessels, seeded).
+6. Update this file, commit, push.
 
 1. **V10**: port `GameDataSO` (~783L, single-file oversize accepted) + engine
    `ISession` placeholder. Then V11 (CellConfigDataSO, BlockDensityGrid,
diff --git a/Port/docs/VESSEL_LAYER.md b/Port/docs/VESSEL_LAYER.md
index 593e3dc11..778af1ab6 100644
--- a/Port/docs/VESSEL_LAYER.md
+++ b/Port/docs/VESSEL_LAYER.md
@@ -179,10 +179,10 @@ a file is indivisible). Engine additions don't count against the budget.
 | **V9** ✅ | 14. Engine E9. 15. ShipHelper, ThemeManagerDataContainerSO, VesselCustomization (+restore), GenericPoolManager. | ~562 | — |
 | **V10** ✅ | 16. GameDataSO (single-file oversize) + engine `ISession` placeholder. | ~783 | — |
 | **V11** ✅ | 17. CellConfigDataSO, BlockDensityGrid, CellRuntimeDataSO. | ~710 | — |
-| **V12** | 18. Cell (single-file oversize; flora/fauna touchpoints stay event-shaped per ECOSYSTEM rules — conserved mass untouched). | ~825 | — |
+| **V12** ✅ | 18. Cell (single-file oversize; flora/fauna touchpoints stay event-shaped per ECOSYSTEM rules — conserved mass untouched). | ~825 | — |
 | **V13** ✅ | 19. PrismStateManager, PrismTeamManager, PrismScaleAnimator. | ~474 | — |
 | **V14** ✅ | 20. MaterialPropertyAnimator, Trail, PrismProperties, TrailFollower. | ~587 | — |
-| **V15** | 21. AudioSystem shell (Deviation #11), PrismAOERegistry (managed-array port). 22. Prism (+`AttachedPrism` restore). | ~925* | — |
+| **V15** ✅ | 21. AudioSystem shell (Deviation #11), PrismAOERegistry (managed-array port). 22. Prism (+`AttachedPrism` restore). | ~925* | — |
 | **V16** | 23. NudgeShardPoolManager, NudgeShard, Skimmer (+skimmer member restores). 24. DriftTrailActionExecutor. | ~347 | — |
 | **V17** | 25. VesselPrismController (+restore). 26. R_VesselActionHandler (+restore). | ~696 | — |
 | **V18** | 27. AIPilot, AICinematicBehavior (+restores). | ~684 | — |
diff --git a/Port/src/CosmicShore.Engine/Math/Mathf.cs b/Port/src/CosmicShore.Engine/Math/Mathf.cs
index 8eca6a6eb..c0c3a6f7e 100644
--- a/Port/src/CosmicShore.Engine/Math/Mathf.cs
+++ b/Port/src/CosmicShore.Engine/Math/Mathf.cs
@@ -64,6 +64,19 @@ namespace CosmicShore.Engine
 
         public static float Sign(float f) => f >= 0f ? 1f : -1f;
 
+        /// <summary>Smallest power of two ≥ <paramref name="value"/> (0 → 0; original engine contract).</summary>
+        public static int NextPowerOfTwo(int value)
+        {
+            if (value <= 0) return 0;
+            value--;
+            value |= value >> 1;
+            value |= value >> 2;
+            value |= value >> 4;
```

</details>

### `4572cb830` — feat(port): vessel-layer V17 — VesselPrismController + R_VesselActionHandler, 14 tests

_Claude, 2026-06-12 03:17:47 +0000_

```text
- VesselPrismController verbatim (trail spawn gating, full prism configuration,
  gap-pair spawning, trail cap pool recycling, danger-blend path); Skimmer field
  staged for the in-flight V16 agent
- R_VesselActionHandler verbatim (shared/touch/gamepad mapping dispatch,
  AbilityStats durations, SOAP button channels, mute/block with timed unblock,
  local-invoke RPC path); mapping structs deleted in favor of the V9-extracted
  R_VesselActionMappings.cs; AutoPilotEnabled guards staged for V18
- leaf ports: ScriptableEventInputEventBlock, MaterialBlendUtility (missing from
  the closure survey)
- engine: GetCancellationTokenOnDestroy() alias, GameTask.Delay(TimeSpan),
  Material.Lerp
- VesselActionLayerTests: 14 tests (dispatch matrix, overrides, block/unblock,
  spawner configuration via PrismTestRig, danger mode)

781 tests green (529 xunit + 252 NUnit). Agent-ported in isolated worktree.
```

```text
 Port/src/CosmicShore.Engine/Rendering/Material.cs                     |  26 ++
 Port/src/CosmicShore.Engine/SceneGraph/MonoBehaviour.cs               |   7 +
 Port/src/CosmicShore.Engine/Tasks/GameTask.cs                         |   4 +
 .../EffectsSO/Skimmer Prism Effects/MaterialBlendUtility.cs           | 148 +++++++++
 Port/src/CosmicShore.Game/Controller/Vessel/R_VesselActionHandler.cs  | 317 +++++++++++++++++++
 Port/src/CosmicShore.Game/Controller/Vessel/VesselPrismController.cs  | 384 +++++++++++++++++++++++
 .../CosmicShore.Game/UI/Controller/ScriptableEventInputEventBlock.cs  |  14 +
 Port/tests/CosmicShore.Tests/VesselActionLayerTests.cs                | 519 ++++++++++++++++++++++++++++++++
 8 files changed, 1419 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 1327 lines)</summary>

```diff
diff --git a/Port/src/CosmicShore.Engine/Rendering/Material.cs b/Port/src/CosmicShore.Engine/Rendering/Material.cs
index 16b3ea7d5..6bf12e6e0 100644
--- a/Port/src/CosmicShore.Engine/Rendering/Material.cs
+++ b/Port/src/CosmicShore.Engine/Rendering/Material.cs
@@ -92,5 +92,31 @@ namespace CosmicShore.Engine
         public bool HasProperty(int nameID)
             => _colors.ContainsKey(nameID) || _floats.ContainsKey(nameID)
             || _vectors.ContainsKey(nameID) || _ints.ContainsKey(nameID);
+
+        /// <summary>
+        /// Interpolate this material's properties between <paramref name="start"/> and
+        /// <paramref name="end"/> (the original engine's Material.Lerp): every color, float,
+        /// and vector property named by either endpoint is set to the blend of the two.
+        /// </summary>
+        public void Lerp(Material start, Material end, float t)
+        {
+            if (start is null || end is null) return;
+            t = Mathf.Clamp01(t);
+
+            var colorKeys = new HashSet<int>(start._colors.Keys);
+            colorKeys.UnionWith(end._colors.Keys);
+            foreach (var key in colorKeys)
+                _colors[key] = Color.Lerp(start.GetColor(key), end.GetColor(key), t);
+
+            var floatKeys = new HashSet<int>(start._floats.Keys);
+            floatKeys.UnionWith(end._floats.Keys);
+            foreach (var key in floatKeys)
+                _floats[key] = Mathf.Lerp(start.GetFloat(key), end.GetFloat(key), t);
+
+            var vectorKeys = new HashSet<int>(start._vectors.Keys);
+            vectorKeys.UnionWith(end._vectors.Keys);
+            foreach (var key in vectorKeys)
+                _vectors[key] = Vector4.Lerp(start.GetVector(key), end.GetVector(key), t);
+        }
     }
 }
diff --git a/Port/src/CosmicShore.Engine/SceneGraph/MonoBehaviour.cs b/Port/src/CosmicShore.Engine/SceneGraph/MonoBehaviour.cs
index 5a4da025b..59064857d 100644
--- a/Port/src/CosmicShore.Engine/SceneGraph/MonoBehaviour.cs
+++ b/Port/src/CosmicShore.Engine/SceneGraph/MonoBehaviour.cs
@@ -118,6 +118,13 @@ namespace CosmicShore.Engine
         public System.Threading.CancellationToken destroyCancellationToken
             => (_destroyCts ??= new System.Threading.CancellationTokenSource()).Token;
 
+        /// <summary>
+        /// UniTask-era spelling of <see cref="destroyCancellationToken"/> (originally a
+        /// `Cysharp.Threading.Tasks` extension method) so ported call sites stay verbatim.
+        /// </summary>
+        public System.Threading.CancellationToken GetCancellationTokenOnDestroy()
+            => destroyCancellationToken;
+
         internal override void DestroyComponentNow()
         {
             if (destroyedFlag) return;
diff --git a/Port/src/CosmicShore.Engine/Tasks/GameTask.cs b/Port/src/CosmicShore.Engine/Tasks/GameTask.cs
index 567ba9089..19f9397f1 100644
--- a/Port/src/CosmicShore.Engine/Tasks/GameTask.cs
+++ b/Port/src/CosmicShore.Engine/Tasks/GameTask.cs
@@ -40,6 +40,10 @@ namespace CosmicShore.Engine.Tasks
         public static DelayAwaitable Delay(int milliseconds, CancellationToken cancellationToken = default)
             => new(milliseconds / 1000f, unscaled: false, cancellationToken);
 
+        /// <summary>Resume after <paramref name="delayTimeSpan"/> of scaled game time (UniTask.Delay(TimeSpan) call sites).</summary>
+        public static DelayAwaitable Delay(TimeSpan delayTimeSpan, CancellationToken cancellationToken = default)
+            => new((float)delayTimeSpan.TotalSeconds, unscaled: false, cancellationToken);
+
         /// <summary>Resume on the frame the predicate first returns true (checked once per frame; completes synchronously if already true).</summary>
         public static WaitUntilAwaitable WaitUntil(Func<bool> predicate, CancellationToken cancellationToken = default)
             => new(predicate, cancellationToken);
diff --git a/Port/src/CosmicShore.Game/Controller/Vessel/R_VesselActionHandler.cs b/Port/src/CosmicShore.Game/Controller/Vessel/R_VesselActionHandler.cs
new file mode 100644
index 000000000..9ac118d45
--- /dev/null
+++ b/Port/src/CosmicShore.Game/Controller/Vessel/R_VesselActionHandler.cs
@@ -0,0 +1,317 @@
+using CosmicShore.Gameplay;
+using CosmicShore.ScriptableObjects;
+using System;
+using System.Collections.Generic;
+using System.Threading;
```

</details>

### `372eebbfa` — feat(port): vessel-layer V16 — skimmer layer verbatim; all V16/V17 cross-seams restored

_Claude, 2026-06-12 03:59:57 +0000_

```text
- Skimmer (196L) verbatim with ZERO code deviations (gaussian sweet-spot math,
  booster-ring spawn from NudgeShard pool along Trail.LookAhead, impact cooldown,
  elemental Skimmer.Scale binding); NudgeShard + NudgeShardPoolManager
  (GenericPoolManager subclass) verbatim; DriftTrailActionExecutor +
  DriftTrailActionSO verbatim
- engine: Transform.parent setter (world-pose-stays reparent contract)
- #10c restores: NearFieldSkimmer/FarFieldSkimmer (V16 agent) AND
  VesselPrismController/ActionHandler (integrator) — IVesselStatus now has only
  V18 (AI) + V19 (camera) markers left
- cross-restores: VesselPrismController skimmer field + wait-time ternary live;
  DriftTrailActionExecutor SetDotProduct calls live (AutoPilotEnabled guards
  stay staged for V18); spawner test rig takes the explicit-waitTime branch
- SkimmerLayerTests: 16 tests (pool, shard triggers via reflection, gaussian
  freeze, booster-ring integration, drift loop lifecycle incl. the async-void
  xunit deadlock guard)

797 tests green (545 xunit + 252 NUnit). Agent-ported in isolated worktree.
```

```text
 Port/docs/VESSEL_LAYER.md                                             |   4 +-
 Port/src/CosmicShore.Engine/SceneGraph/Transform.cs                   |  13 +-
 .../CosmicShore.Game/Controller/Environment/Cytoplasm/NudgeShard.cs   |  49 +++
 .../CosmicShore.Game/Controller/Environment/NudgeShardPoolManager.cs  |  11 +
 Port/src/CosmicShore.Game/Controller/Vessel/IVesselStatus.cs          |   8 +-
 .../Vessel/R_VesselActions/Data Containers/DriftTrailActionSO.cs      |  15 +
 .../Vessel/R_VesselActions/Executors/DriftTrailActionExecutor.cs      |  93 ++++++
 Port/src/CosmicShore.Game/Controller/Vessel/Skimmer.cs                | 197 ++++++++++++
 Port/src/CosmicShore.Game/Controller/Vessel/VesselPrismController.cs  |  15 +-
 Port/tests/CosmicShore.Tests/SkimmerLayerTests.cs                     | 543 ++++++++++++++++++++++++++++++++
 Port/tests/CosmicShore.Tests/TrailLayerTests.cs                       |   5 +
 Port/tests/CosmicShore.Tests/VesselActionLayerTests.cs                |   3 +
 Port/tests/CosmicShore.Tests/VesselLayerTestDoubles.cs                |   9 +-
 13 files changed, 944 insertions(+), 21 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1119 lines)</summary>

```diff
diff --git a/Port/docs/VESSEL_LAYER.md b/Port/docs/VESSEL_LAYER.md
index 778af1ab6..3172dfb11 100644
--- a/Port/docs/VESSEL_LAYER.md
+++ b/Port/docs/VESSEL_LAYER.md
@@ -183,8 +183,8 @@ a file is indivisible). Engine additions don't count against the budget.
 | **V13** ✅ | 19. PrismStateManager, PrismTeamManager, PrismScaleAnimator. | ~474 | — |
 | **V14** ✅ | 20. MaterialPropertyAnimator, Trail, PrismProperties, TrailFollower. | ~587 | — |
 | **V15** ✅ | 21. AudioSystem shell (Deviation #11), PrismAOERegistry (managed-array port). 22. Prism (+`AttachedPrism` restore). | ~925* | — |
-| **V16** | 23. NudgeShardPoolManager, NudgeShard, Skimmer (+skimmer member restores). 24. DriftTrailActionExecutor. | ~347 | — |
-| **V17** | 25. VesselPrismController (+restore). 26. R_VesselActionHandler (+restore). | ~696 | — |
+| **V16** ✅ | 23. NudgeShardPoolManager, NudgeShard, Skimmer (+skimmer member restores). 24. DriftTrailActionExecutor. | ~347 | — |
+| **V17** ✅ | 25. VesselPrismController (+restore). 26. R_VesselActionHandler (+restore). | ~696 | — |
 | **V18** | 27. AIPilot, AICinematicBehavior (+restores). | ~684 | — |
 | **V19** | 28. ICameraController, ICameraConfigurator, CustomCameraController, CameraManager shell (Deviation #12), VesselCameraCustomizer (+restore). 29. VesselImpactor + VesselExplosionByCrystalEffectSO (first impact-matrix slice), SilhouetteConfigSO, view shells (Deviation #13), SilhouetteController (+final restores). 30. **Close #10c — IVesselStatus verbatim.** Interface-surface freeze test; CLI vertical-slice growth (NEXT-UP item 4). | ~770* | **#10c** |
 
diff --git a/Port/src/CosmicShore.Engine/SceneGraph/Transform.cs b/Port/src/CosmicShore.Engine/SceneGraph/Transform.cs
index f51824535..73413fd24 100644
--- a/Port/src/CosmicShore.Engine/SceneGraph/Transform.cs
+++ b/Port/src/CosmicShore.Engine/SceneGraph/Transform.cs
@@ -22,7 +22,14 @@ namespace CosmicShore.Engine
         public Quaternion localRotation = Quaternion.identity;
         public Vector3 localScale = Vector3.one;
 
-        public Transform parent { get; private set; }
+        Transform _parent;
+
+        /// <summary>Assignment reparents keeping the world pose (original engine contract); use SetParent for control.</summary>
+        public Transform parent
+        {
+            get => _parent;
+            set => SetParent(value);
+        }
 
         public int childCount => _children.Count;
         public Transform GetChild(int index) => _children[index];
@@ -147,7 +154,7 @@ namespace CosmicShore.Engine
             parent?._children.Remove(this);
             if (parent is null) gameObject.scene?.RemoveRoot(gameObject);
 
-            parent = newParent;
+            _parent = newParent;
 
             if (newParent is not null) newParent._children.Add(this);
             else gameObject.scene?.AddRoot(gameObject);
@@ -175,7 +182,7 @@ namespace CosmicShore.Engine
         internal void SetParentForDestroy()
         {
             parent?._children.Remove(this);
-            parent = null;
+            _parent = null;
         }
 
         internal override void DestroyComponentNow()
diff --git a/Port/src/CosmicShore.Game/Controller/Environment/Cytoplasm/NudgeShard.cs b/Port/src/CosmicShore.Game/Controller/Environment/Cytoplasm/NudgeShard.cs
new file mode 100644
index 000000000..6d93ca180
--- /dev/null
+++ b/Port/src/CosmicShore.Game/Controller/Environment/Cytoplasm/NudgeShard.cs
@@ -0,0 +1,49 @@
+using CosmicShore.Core;
+using CosmicShore.Gameplay;
+using CosmicShore.Utility;
+using CosmicShore.Engine.Injection;
+using System.Collections;
+using System.Collections.Generic;
+using CosmicShore.Engine;
+using CosmicShore.Data;
+namespace CosmicShore.Gameplay
+{
+    public class NudgeShard : MonoBehaviour
+    {
+        [Inject] AudioSystem audioSystem;
+        float Displacement = 40f;
+        float Duration = .3f;
+        [SerializeField] int energyResourceIndex = 0;
+        [SerializeField] float energyAmount = 0.05f;
+
+        public List<Prism> Prisms;
+
+        private void Start()
```

</details>

### `a53f99a67` — feat(port): vessel-layer V18 — AIPilot + AICinematicBehavior; AI-keyed restores live

_Claude, 2026-06-12 12:20:15 +0000_

```text
- AIPilot (419L) + AICinematicBehavior (265L) + CinematicDefinitionSO verbatim
  (agent work salvaged complete from its worktree after a session-limit cutoff)
- #10c restores: AIPilot / AICinematicBehavior / AutoPilotEnabled live on
  IVesselStatus; staged autopilot guards restored in R_VesselActionHandler and
  DriftTrailActionExecutor; stubs across test files updated (null-AIPilot
  treated as autopilot-off)
- engine: Vector3 + Coroutines + MonoBehaviour additions (StopCoroutine(IEnumerator))
- AIPilotTests added

819 tests green (567 xunit + 252 NUnit).
```

```text
 Port/src/CosmicShore.Engine/Math/Vector3.cs                           |   2 +
 Port/src/CosmicShore.Engine/SceneGraph/Coroutines.cs                  |  19 +-
 Port/src/CosmicShore.Engine/SceneGraph/MonoBehaviour.cs               |   3 +
 Port/src/CosmicShore.Game/Controller/AI/AIPilot.cs                    | 420 ++++++++++++++++++++++
 Port/src/CosmicShore.Game/Controller/Vessel/IVesselStatus.cs          |   6 +-
 Port/src/CosmicShore.Game/Controller/Vessel/R_VesselActionHandler.cs  |  12 +-
 .../Vessel/R_VesselActions/Executors/DriftTrailActionExecutor.cs      |   8 +-
 .../CosmicShore.Game/Utility/DataContainers/AICinematicBehavior.cs    | 266 ++++++++++++++
 .../CosmicShore.Game/Utility/DataContainers/CinematicDefinitionSO.cs  |  20 ++
 Port/tests/CosmicShore.Tests/AIPilotTests.cs                          | 601 ++++++++++++++++++++++++++++++++
 Port/tests/CosmicShore.Tests/SkimmerLayerTests.cs                     |  10 +-
 Port/tests/CosmicShore.Tests/TrailLayerTests.cs                       |   5 +
 Port/tests/CosmicShore.Tests/VesselActionLayerTests.cs                |   6 +-
 Port/tests/CosmicShore.Tests/VesselLayerTestDoubles.cs                |   8 +
 14 files changed, 1362 insertions(+), 24 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1570 lines)</summary>

```diff
diff --git a/Port/src/CosmicShore.Engine/Math/Vector3.cs b/Port/src/CosmicShore.Engine/Math/Vector3.cs
index bb6dbddd5..3a80bfbf8 100644
--- a/Port/src/CosmicShore.Engine/Math/Vector3.cs
+++ b/Port/src/CosmicShore.Engine/Math/Vector3.cs
@@ -81,6 +81,8 @@ namespace CosmicShore.Engine
 
         public static float Magnitude(Vector3 vector) => vector.magnitude;
 
+        public static float SqrMagnitude(Vector3 vector) => vector.sqrMagnitude;
+
         public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
         {
             t = Mathf.Clamp01(t);
diff --git a/Port/src/CosmicShore.Engine/SceneGraph/Coroutines.cs b/Port/src/CosmicShore.Engine/SceneGraph/Coroutines.cs
index 64b0fc158..13f5fecc0 100644
--- a/Port/src/CosmicShore.Engine/SceneGraph/Coroutines.cs
+++ b/Port/src/CosmicShore.Engine/SceneGraph/Coroutines.cs
@@ -32,6 +32,7 @@ namespace CosmicShore.Engine
         {
             public MonoBehaviour Owner;
             public readonly Stack<IEnumerator> Frames = new();
+            public IEnumerator Root;
             public Coroutine Handle;
             public float WaitUntilTime = -1f;
             public Coroutine WaitingOn;
@@ -42,7 +43,7 @@ namespace CosmicShore.Engine
         public Coroutine Start(MonoBehaviour owner, IEnumerator routine)
         {
             if (routine is null) throw new ArgumentNullException(nameof(routine));
-            var entry = new Entry { Owner = owner, Handle = new Coroutine() };
+            var entry = new Entry { Owner = owner, Root = routine, Handle = new Coroutine() };
             entry.Frames.Push(routine);
             _entries.Add(entry);
             Step(entry); // synchronous run to first yield (original contract)
@@ -60,6 +61,22 @@ namespace CosmicShore.Engine
                 }
         }
 
+        /// <summary>
+        /// Original-engine StopCoroutine(IEnumerator) contract: stops the coroutine that
+        /// was started with this exact enumerator instance. A freshly-created enumerator
+        /// matches nothing and the call is a no-op (the documented original behavior).
+        /// </summary>
+        public void Stop(MonoBehaviour owner, IEnumerator routine)
+        {
+            for (int i = _entries.Count - 1; i >= 0; i--)
+                if (ReferenceEquals(_entries[i].Root, routine) && ReferenceEquals(_entries[i].Owner, owner))
+                {
+                    _entries[i].Handle.Done = true;
+                    _entries.RemoveAt(i);
+                    return;
+                }
+        }
+
         public void StopAll(MonoBehaviour owner)
         {
             for (int i = _entries.Count - 1; i >= 0; i--)
diff --git a/Port/src/CosmicShore.Engine/SceneGraph/MonoBehaviour.cs b/Port/src/CosmicShore.Engine/SceneGraph/MonoBehaviour.cs
index 59064857d..0a699abcd 100644
--- a/Port/src/CosmicShore.Engine/SceneGraph/MonoBehaviour.cs
+++ b/Port/src/CosmicShore.Engine/SceneGraph/MonoBehaviour.cs
@@ -106,6 +106,9 @@ namespace CosmicShore.Engine
         public void StopCoroutine(Coroutine routine)
             => GameLoop.Current?.Coroutines.Stop(this, routine);
 
+        public void StopCoroutine(System.Collections.IEnumerator routine)
+            => GameLoop.Current?.Coroutines.Stop(this, routine);
+
         public void StopAllCoroutines()
             => GameLoop.Current?.Coroutines.StopAll(this);
 
diff --git a/Port/src/CosmicShore.Game/Controller/AI/AIPilot.cs b/Port/src/CosmicShore.Game/Controller/AI/AIPilot.cs
new file mode 100644
index 000000000..b97d953e0
--- /dev/null
+++ b/Port/src/CosmicShore.Game/Controller/AI/AIPilot.cs
@@ -0,0 +1,420 @@
+using CosmicShore.Engine;
+using System.Collections.Generic;
+using System.Collections;
```

</details>

### `45f08688a` — feat(port): vessel-layer V19 + #10c CLOSED — the 19-row vessel arc is complete

_Claude, 2026-06-12 12:23:40 +0000_

```text
- V19 salvaged complete from the cut agent worktree: ICameraController/
  ICameraConfigurator/CustomCameraController, VesselCameraCustomizer,
  SilhouetteConfigSO/SilhouetteController (+SilhouetteView/ElementalBarsView
  phase-5 shells per #13, CameraManager shell per #12), first impact-matrix
  slice (ImpactorBase/IImpactor/ImpactCollider, Vessel/Prism/Skimmer/Crystal/
  NetworkVessel impactors, abstract effect SOs, VesselExplosionByCrystalEffectSO,
  impactor data containers), CrystalProperties + Crystal shell upgrade,
  engine INetworkSerializable + Camera.useOcclusionCulling
- #10c CLOSED: VesselCameraCustomizer + Silhouette restored; IVesselStatus is
  diff-verified VERBATIM against the Unity source — zero deviation markers
- VESSEL_LAYER.md: all 19 rows done, exit state recorded; PORT_PLAN NEXT UP ->
  V19 test backfill + VesselStatus concrete arc + CLI M2

819 tests green (567 xunit + 252 NUnit); client smoke clean.
```

```text
 .../ImpactEffects/Containers/SkimmerImpactorDataContainerSO.cs        |  18 +++
 .../ImpactEffects/Containers/VesselImpactorDataContainerSO.cs         |  34 +++++
 .../EffectsSO/Abstract Effect Types/SkimmerCrystalEffectSO.cs         |   8 +
 .../EffectsSO/Abstract Effect Types/SkimmerPrismEffectSO.cs           |   8 +
 .../EffectsSO/Abstract Effect Types/VesselCrystalEffectSO.cs          |   8 +
 .../EffectsSO/Abstract Effect Types/VesselPrismEffectSO.cs            |   8 +
 .../EffectsSO/Abstract Effect Types/VesselSkimmerEffectsSO.cs         |   8 +
 .../Controller/ImpactEffects/EffectsSO/ImpactEffectSO.cs              |  17 +++
 .../Vessel Crystal Effects/VesselExplosionByCrystalEffectSO.cs        | 100 +++++++++++++
 Port/src/CosmicShore.Game/Controller/ImpactEffects/ImpactCollider.cs  |  16 ++
 .../Controller/ImpactEffects/Impactors/CrystalImpactData.cs           |  35 +++++
 .../Controller/ImpactEffects/Impactors/CrystalImpactor.cs             |  23 +++
 .../CosmicShore.Game/Controller/ImpactEffects/Impactors/IImpactor.cs  |  15 ++
 .../Controller/ImpactEffects/Impactors/ImpactorBase.cs                |  41 +++++
 .../Controller/ImpactEffects/Impactors/NetworkVesselImpactor.cs       |  47 ++++++
 .../Controller/ImpactEffects/Impactors/PrismImpactor.cs               |  75 ++++++++++
 .../Controller/ImpactEffects/Impactors/SkimmerImpactor.cs             | 193 ++++++++++++++++++++++++
 .../Controller/ImpactEffects/Impactors/VesselImpactor.cs              | 104 +++++++++++++
 Port/src/CosmicShore.Game/Controller/Managers/CameraManager.cs        |  19 +++
 Port/src/CosmicShore.Game/Controller/Vessel/IVesselStatus.cs          |   4 +-
 Port/src/CosmicShore.Game/Controller/Vessel/SilhouetteConfigSO.cs     |  43 ++++++
 Port/src/CosmicShore.Game/Controller/Vessel/SilhouetteController.cs   | 258 ++++++++++++++++++++++++++++++++
 Port/src/CosmicShore.Game/Controller/Vessel/SilhouetteView.cs         |  35 +++++
 Port/src/CosmicShore.Game/Controller/Vessel/VesselCameraCustomizer.cs |  77 ++++++++++
 .../SOAP/ScriptableClassType/ScriptableEventVesselImpactor.cs         |  12 ++
 Port/src/CosmicShore.Game/UI/View/ElementalBarsView.cs                |  52 +++++++
 Port/tests/CosmicShore.Tests/SkimmerLayerTests.cs                     |   2 +
 Port/tests/CosmicShore.Tests/TrailLayerTests.cs                       |   2 +
 Port/tests/CosmicShore.Tests/VesselLayerTestDoubles.cs                |   2 +
 39 files changed, 1586 insertions(+), 21 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1739 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index fd8d41530..b466ec6d0 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -314,7 +314,7 @@ Next: V8 (VesselTransformer + member restore), rival balance from prompter feedb
 | 7 | 10 SOAP files deferred pending gameplay types: ScriptableEventVesselImpactor / ExplosionDebuffApplied / SkimmerDebuffApplied (IVessel/IVesselStatus/VesselImpactor), ScriptableSilhouetteData/* (SilhouetteController), ScriptableVesselHUDData/* (MiniGameHUD), VesselPrefabContainer (IVesselStatus). MainMenuStateTests (MainMenuController) and GameObjectExtensionTests (physics types) likewise port with their subjects. | Tracked in NEXT UP. |
 | 8 | Stat structs (CellStats/CrystalStats/PrismStats/AbilityStats), PrismType, and audio category enums are extracted into their own port files (source noted in headers) because their host classes (StatsManager, PrismFactory, AudioSystem) port in later phases. | File-split only; content verbatim. |
 | 9 | ResourceSystem temporary deviations: (a) base class `ElementalShipComponent` → `MonoBehaviour` until IVessel/ElementalFloat port; (b) `[RequireComponent(typeof(IVesselStatus))]` commented until IVesselStatus ports. Class body verbatim. **CLOSED at V6 (2026-06-11)** — both restored verbatim. | Unblocks the elemental core. |
-| 10c | `IVesselStatus` landed (V6) with 13 members commented pending their types: `AIPilot`, `AICinematicBehavior`, `AutoPilotEnabled` (→V18), `AttachedPrism` (→V15), `VesselAnimation` (→V7), `VesselTransformer` (→V8), `Customization` (→V9), `NearFieldSkimmer`/`FarFieldSkimmer` (→V16), `VesselPrismController`/`ActionHandler` (→V17), `VesselCameraCustomizer`/`Silhouette` (→V19). Each restore iteration uncomments its members; V19 closes. | Stages the vessel SCC per VESSEL_LAYER.md. |
+| 10c | `IVesselStatus` landed (V6) with 13 members commented pending their types: `AIPilot`, `AICinematicBehavior`, `AutoPilotEnabled` (→V18), `AttachedPrism` (→V15), `VesselAnimation` (→V7), `VesselTransformer` (→V8), `Customization` (→V9), `NearFieldSkimmer`/`FarFieldSkimmer` (→V16), `VesselPrismController`/`ActionHandler` (→V17), `VesselCameraCustomizer`/`Silhouette` (→V19). Each restore iteration uncomments its members; V19 closes. **CLOSED 2026-06-12 — IVesselStatus diff-verified verbatim.** | Stages the vessel SCC per VESSEL_LAYER.md. |
 | 11 | `AudioSystem` type-preserving shell (`Instance`, two `PlayGameplaySFX` overloads; bodies no-op) — pulled forward from V15 to V6 because `ActionExecutorRegistry` needs the type. Real port with the phase-5 audio backend. | Keeps ActionExecutorRegistry verbatim. |
 | 16 | `Directory.Build.props` adds CS0169 to NoWarn (alongside CS0649): verbatim Unity-era private fields whose only usages are commented (e.g. `InputController.vessel` until its orientation block revives) or inspector-driven fire it; the Unity compiler tolerated them. | Verbatim fields without warning noise. |
 
@@ -428,23 +428,24 @@ Next: V8 (VesselTransformer + member restore), rival balance from prompter feedb
   matrix, InputController Awake wiring, shape-key theory, flare, Update routing).
   **610 tests green (358 + 252)**; client smoke unaffected.
 
-## NEXT UP (iteration 11)
+## NEXT UP (iteration 12)
 
-**V12 + V15 integrated (2026-06-12):** Cell (904L) and Prism (484L) + PrismAOERegistry
-(managed-array) agent-ported; cross-restores applied both directions — Cell's block
-surface is Prism-typed verbatim, Prism's cell registration is live (FindCellContaining
-→ AddBlock → density grids → phase machine). Trail, PrismProperties, PrismTeamManager
-fully verbatim, zero markers. 767 tests green.
+**VESSEL ARC COMPLETE (2026-06-12):** all 19 rows done. V16 skimmer layer, V17
+action layer, V18 AI layer, V19 camera/silhouette/impact slice integrated;
+**Deviation #10c CLOSED** — IVesselStatus is diff-verified verbatim. 819 tests
+green. V18/V19 were salvaged complete from session-limit-cut agent worktrees.
 
-Goal: V16-V19 finish the vessel arc.
+Goal: post-arc consolidation + next arc.
 
-1. **V16**: NudgeShardPoolManager, NudgeShard, Skimmer (+skimmer #10c restores),
-   DriftTrailActionExecutor.
-2. **V17**: VesselPrismController, R_VesselActionHandler (+restores).
-3. **V18**: AIPilot, AICinematicBehavior (+restores).
-4. **V19**: camera layer + VesselImpactor slice + SilhouetteController; close #10c.
-5. CLI M2 vertical slice (cell + prisms + crystals + scripted vessels, seeded).
-6. Update this file, commit, push.
+1. **V19 test backfill**: the V19 agent was cut before writing
+   CameraSilhouetteLayerTests — add coverage for CustomCameraController data
+   logic, VesselCameraCustomizer wiring, SilhouetteController init,
+   VesselImpactor dispatch.
+2. **VesselStatus concrete arc**: the NetworkBehaviour itself (unblocked by the
+   completed closure) + IVessel concrete (Vessel.cs) — survey first.
+3. CLI M2 vertical slice (cell + prisms + crystals + scripted vessels, seeded).
+4. Sprint feedback as it arrives.
+5. Update this file, commit, push.
 
 1. **V10**: port `GameDataSO` (~783L, single-file oversize accepted) + engine
    `ISession` placeholder. Then V11 (CellConfigDataSO, BlockDensityGrid,
diff --git a/Port/docs/VESSEL_LAYER.md b/Port/docs/VESSEL_LAYER.md
index 3172dfb11..38d33a27c 100644
--- a/Port/docs/VESSEL_LAYER.md
+++ b/Port/docs/VESSEL_LAYER.md
@@ -185,13 +185,13 @@ a file is indivisible). Engine additions don't count against the budget.
 | **V15** ✅ | 21. AudioSystem shell (Deviation #11), PrismAOERegistry (managed-array port). 22. Prism (+`AttachedPrism` restore). | ~925* | — |
 | **V16** ✅ | 23. NudgeShardPoolManager, NudgeShard, Skimmer (+skimmer member restores). 24. DriftTrailActionExecutor. | ~347 | — |
 | **V17** ✅ | 25. VesselPrismController (+restore). 26. R_VesselActionHandler (+restore). | ~696 | — |
-| **V18** | 27. AIPilot, AICinematicBehavior (+restores). | ~684 | — |
-| **V19** | 28. ICameraController, ICameraConfigurator, CustomCameraController, CameraManager shell (Deviation #12), VesselCameraCustomizer (+restore). 29. VesselImpactor + VesselExplosionByCrystalEffectSO (first impact-matrix slice), SilhouetteConfigSO, view shells (Deviation #13), SilhouetteController (+final restores). 30. **Close #10c — IVesselStatus verbatim.** Interface-surface freeze test; CLI vertical-slice growth (NEXT-UP item 4). | ~770* | **#10c** |
+| **V18** ✅ | 27. AIPilot, AICinematicBehavior (+restores). | ~684 | — |
+| **V19** ✅ | 28. ICameraController, ICameraConfigurator, CustomCameraController, CameraManager shell (Deviation #12), VesselCameraCustomizer (+restore). 29. VesselImpactor + VesselExplosionByCrystalEffectSO (first impact-matrix slice), SilhouetteConfigSO, view shells (Deviation #13), SilhouetteController (+final restores). 30. **Close #10c — IVesselStatus verbatim.** Interface-surface freeze test; CLI vertical-slice growth (NEXT-UP item 4). | ~770* | **#10c** |
 
 \* V15/V19 run hot; split at commit granularity if the green-build invariant gets
 uncomfortable — each numbered step is independently green.
 
-**Exit state after V19**: IVessel + IVesselStatus + IPlayer compile **verbatim**;
+**EXIT STATE ACHIEVED 2026-06-12 — IVesselStatus diff-verified verbatim, zero #10c markers.** **Exit state after V19**: IVessel + IVesselStatus + IPlayer compile **verbatim**;
 ElementalFloat + ElementalShipComponent ported (V6); Deviation #9 closed (V6);
 deviations #11/#12/#13 open and owned by phase 5; VesselStatus itself (the concrete
 NetworkBehaviour, not in this closure's targets) unblocks for the next arc.
diff --git a/Port/src/CosmicShore.Engine/Attributes.cs b/Port/src/CosmicShore.Engine/Attributes.cs
index 42e748553..4b94bf2aa 100644
--- a/Port/src/CosmicShore.Engine/Attributes.cs
+++ b/Port/src/CosmicShore.Engine/Attributes.cs
@@ -106,6 +106,18 @@ namespace CosmicShore.Engine
     [AttributeUsage(AttributeTargets.Field)]
     public sealed class HideInInspectorAttribute : Attribute { }
```

</details>

### `ffe7ca442` — docs(port): VESSEL_CONCRETE.md — concrete-arc survey (C1-C6)

_Claude, 2026-06-12 12:38:03 +0000_

```text
13-file closure, ~1,888 game lines, 6 engine gaps (E11-E16). Riskiest item:
E16 prefab-faithful cloning — CloneGameObject copies serialized refs by value
with no intra-hierarchy remap, silently aliasing template components on every
spawned vessel; C1 lands an old->new remap pass with round-trip tests and C6
gates on clone self-reference assertions.
```

```text
 Port/docs/VESSEL_CONCRETE.md | 170 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 170 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 176 lines)</summary>

```diff
diff --git a/Port/docs/VESSEL_CONCRETE.md b/Port/docs/VESSEL_CONCRETE.md
new file mode 100644
index 000000000..d697f5ab3
--- /dev/null
+++ b/Port/docs/VESSEL_CONCRETE.md
@@ -0,0 +1,170 @@
+# Vessel Concrete — Type-Closure Survey & Porting Sequence
+
+Follow-up to `VESSEL_LAYER.md` (V1–V19, complete — IVesselStatus diff-verified
+verbatim). This arc ports the **concrete classes that implement the trio**
+(`IVessel` / `IVesselStatus` / `IPlayer`) plus the single-player spawning path, so a
+headless CLI can spawn a real Player+Vessel pair through the verbatim pipeline:
+
+- `Controller/Vessel/VesselStatus.cs` (260) — MonoBehaviour implementing `IVesselStatus`
+- `Controller/Vessel/VesselController.cs` (364) — NetworkBehaviour implementing `IVessel`
+- `Controller/Player/Player.cs` (529) — NetworkBehaviour implementing `IPlayer`
+- `Controller/Vessel/VesselSpawner.cs` (56)
+- `Controller/Player/PlayerSpawner.cs` (46) + `PlayerSpawnerAdapterBase.cs` (44) +
+  `MiniGamePlayerSpawnerAdapter.cs` (61) + `VolumeTestPlayerSpawnerAdapter.cs` (12)
+
+All paths under `Assets/_Scripts/` unless noted. Dispositions: **ALREADY-PORTED**
+(in `Port/src/`) · **LEAF** (port now, using-swaps only) · **SHALLOW** (port after
+listed engine additions) · **DEEP** (drags further systems).
+
+**Closure size: 13 files, ≈1,888 game lines** (1,372 subject + 516 closure), plus
+≈345 engine lines (E11–E16). This arc is dramatically narrower than V1–V19 because
+the 19-row vessel layer already landed every component the concrete classes touch —
+the only new game-side dependencies are the profile-service slice
+(`PlayerDataService` + `PlayerProfileData` + `SO_ProfileIconList`), the prefab
+container (`VesselPrefabContainer`), and the trail tinter
+(`VesselTrailCustomization`).
+
+## Already-ported types referenced by the closure
+
+No action needed; the tables below omit them from "depends on".
+
+| Category | Types |
+|---|---|
+| Engine core | `Transform`, `GameObject` (`SetActive`, `name`, `GetOrAdd`), `Component` (`TryGetComponent`, `GetComponentInChildren<T>(includeInactive)`), `MonoBehaviour` (fake-null bool), `ScriptableObject`, `Object` (`Instantiate` SO/GO/Component clone, `Destroy`, `DontDestroyOnLoad`), `Time`, `Random.Range`, `Mathf`, `Vector3`, `Quaternion`, `Color`, `Material`, `Pose`, `Sprite`, attributes (incl. `RequireInterface`, `FormerlySerializedAs`, `RequireComponent`) |
+| Engine net | `NetworkBehaviour` (`IsSpawned/IsOwner/IsServer/IsClient`, `OwnerClientId`, `Spawn()/Despawn()` host-mode), `NetworkVariable<T>` (perm-aware, `OnValueChanged`), `[ServerRpc]`/`[ClientRpc]` attribute stubs (`Compat/EngineCompat.cs` — direct-invoke semantics) |
+| Engine DI | `Container` (`[Inject]`, `InjectGameObject(root, recursive)`), `FixedString64Bytes` |
+| Engine SOAP | `ScriptableEventNoParam/Ulong` (`OnRaised`, `Raise`), `ScriptableVariable<T>`, `VesselClassTypeVariable` |
+| Data | `Domains`, `VesselClassType`, `InputEvents`, `Element`, `ResourceCollection`, `IRoundStats` (with `Cleanup()`), `RoundStats` (NetworkBehaviour, `Name`/`Domain` live mirror) |
+| Game — trio + components | `IVessel`, `IVesselStatus` (verbatim, incl. default members `IsInitializedAsAI => Player.IsInitializedAsAI`, `AutoPilotEnabled => AIPilot.AutoPilotEnabled`), `IPlayer` (+ `IPlayer.InitializeData`), `IVesselHUDController`, `VesselPrismController`, `ResourceSystem`, `VesselTransformer` (`Initialize(IVessel)`, `ToggleActive`, `ResetTransformer`, `SetPose`, `ModifyThrottle`), `AIPilot` (`Initialize(IVessel)`, `StartAIPilot`/`StopAIPilot`), `AICinematicBehavior`, `SilhouetteController`, `VesselCameraCustomizer` (`Initialize`/`RetargetAndApply`), `VesselAnimation` (`Initialize`, `StopFlareEngine/Body`), `R_VesselActionHandler` (`Initialize`, `ToggleSubscription`, `Perform/StopShipControllerActions`), `VesselCustomization`, `R_ShipElementStatsHandler` (`BindElementalFloat`), `Skimmer`, `Prism`, `ShipHelper` (`SetShipProperties`, `Teleport`), `ThemeManagerDataContainerSO`, `InputController` (`Initialize()`, `SetPause`, `SetIdle`, `InputStatus`), `IInputStatus` (`ResetForReplay`), `GameDataSO` (`Players`/`Vessels`, `OnPlayerNetworkSpawnedUlong`, `InvokeVesselNetworkSpawned`, `selectedVesselClass`, `ThemeManagerData`, `SlowedShipTransforms`, `LocalPlayerDisplayName/AvatarId`, `IsActiveDomain`, `RequestedDomainCount`, `AddPlayer`, `SetSpawnPositions`, `SetPlayersActive`, `InitializeGame`), `CSDebug`, `NetMarkers` (`Serialize`, `RpcDispatch`, `CountRpc`, `CountNetVarDirty`) |
+
+## Closure table — subjects
+
+| Type | Defining file | Lines | Depends on (✅ ported / ❌ missing) | Disposition |
+|---|---|---|---|---|
+| `VesselStatus` | `Controller/Vessel/VesselStatus.cs` | 260 | All 10 `RequireComponent` types ✅, `RequireInterface` ✅, `IVesselHUDController` ✅, `Skimmer`/`Prism`/`AICinematicBehavior` ✅, `Material` ✅, `GetOrAdd` ✅ | **LEAF** — every dependency already landed in V1–V19. Concrete impl unblocks the stale `AutoPilotEnabled` deviation in `SilhouetteController` |
+| `VesselController` | `Controller/Vessel/VesselController.cs` | 364 | `GameDataSO` ✅, `ShipHelper` ✅, `NetMarkers` ✅, RPC attrs ✅, `Pose` ✅; ❌ `VesselTrailCustomization`, ❌ E12 (`NetworkObjectId` + `NetworkObject.Despawn(bool)`) | **SHALLOW** after E12 + trail tinter |
+| `Player` | `Controller/Player/Player.cs` | 529 | `GameDataSO` ✅, `RoundStats` ✅, `InputController` ✅, `ThemeManagerDataContainerSO` ✅, `[Inject]` ✅, RPC attrs ✅, `NetMarkers` ✅; ❌ `PlayerDataService` + `PlayerProfileData`, ❌ E11 (`FixedString128Bytes`), ❌ E12 (`NetworkObjectId`), ❌ E13 (UGS `AuthenticationService` shim) | **SHALLOW** after E11–E13 + profile slice. Closes GameDataSO's `BuildHumanCounts` deviation ("restore when Player ports") |
+| `VesselSpawner` | `Controller/Vessel/VesselSpawner.cs` | 56 | `Random.Range` ✅, `Instantiate(Transform)` ✅; ❌ `VesselPrefabContainer`, ❌ E15 (`GameObjectInjector.InjectRecursive` façade + `Container` self-resolve), ❌ E16 (clone intra-hierarchy reference remap) | **SHALLOW** after E15/E16 |
+| `PlayerSpawner` | `Controller/Player/PlayerSpawner.cs` | 46 | `RequireInterface` ✅, `Instantiate(Object)` ✅; ❌ VesselSpawner, ❌ E15 | **SHALLOW** (after VesselSpawner) |
+| `PlayerSpawnerAdapterBase` | `Controller/Player/PlayerSpawnerAdapterBase.cs` | 44 | `GameDataSO` ✅ (`SetSpawnPositions`, `AddPlayer`), `IPlayer.InitializeData` ✅; ❌ PlayerSpawner | **LEAF** (lands with PlayerSpawner) |
+| `MiniGamePlayerSpawnerAdapter` | `Controller/Player/MiniGamePlayerSpawnerAdapter.cs` | 61 | `OnInitializeGame.OnRaised` ✅; ❌ PlayerDataService | **LEAF** (after C2/C6 deps) |
+| `VolumeTestPlayerSpawnerAdapter` | `Controller/Player/VolumeTestPlayerSpawnerAdapter.cs` | 12 | `GameDataSO.InitializeGame/SetPlayersActive` ✅ | **LEAF** |
+
+## Closure table — transitive additions (not in `Port/src`)
+
+| Type | Defining file | Lines | Depends on | Disposition |
+|---|---|---|---|---|
+| `VesselTrailCustomization` | `Controller/Vessel/VesselTrailCustomization.cs` | 62 | ❌ E14 (`Gradient`/`GradientColorKey`/`GradientAlphaKey` + `TrailRenderer.colorGradient`) | **SHALLOW** after E14 |
+| `VesselPrefabContainer` | `ScriptableObjects/SOAP/VesselPrefabContainer.cs` | 50 | `IVesselStatus` ✅, `TryGetComponent` ✅, `CSDebug` ✅ | **LEAF** |
+| `PlayerProfileData` | `UI/Views/PlayerProfileData.cs` | 15 | pure data | **LEAF** |
+| `SO_ProfileIconList` (+`ProfileIcon`) | `ScriptableObjects/SO_ProfileIconList.cs` | 26 | `Sprite` ✅ | **LEAF** |
+| `PlayerDataService` | `UI/Views/PlayerDataService.cs` | 363 | `GameDataSO` ✅, `[Inject]` ✅, `Random` ✅; ❌ `SO_ProfileIconList`, ❌ `PlayerProfileData`; **UGS-coupled**: `UGSDataService` (CloudSave repo), `Unity.Services.Core`, `AuthenticationService`, `LogControlWindow` (`#if UNITY_EDITOR`, drops out) | **SHALLOW** with **Deviation #14 extension** — UGS paths commented (same family as `GameSetting`). Class is already null-tolerant: with `_ugsDataService == null` it runs on the local default profile, which is exactly the headless behavior we want. `Instance` static singleton + `OnProfileChanged` + `CurrentProfile` + `IsInitialized` port verbatim |
+
+Excluded from closure (dependents, not dependencies — they are the **next** arc, the
+multiplayer spawn pipeline): `ServerPlayerVesselInitializer` (+`WithAI`, `Menu`),
+`ClientPlayerVesselInitializer`, `NetcodeHooks`, `MultiplayerSetup`, `DomainAssigner`,
+`PlayerSpawner`'s networked twin paths. Also excluded: `UGSDataService` itself
+(services phase, Deviation #14 owner) and all HUD controllers (phase 5).
+
+## Engine additions required (E11+, continuing VESSEL_LAYER's E1–E10)
+
+| # | Addition | Needed by | Size guess |
+|---|---|---|---|
+| E11 | **`FixedString128Bytes`** — clone of `FixedString64Bytes` with 128-byte cap (`Collections/`) | `Player.NetName` | ~50 |
+| E12 | **`NetworkObjectId` + `NetworkObject` handle** on `NetworkBehaviour` — monotonically allocated id at `Spawn()`; `NetworkObject` property exposing `Despawn(bool destroy)` (despawn + optional `Object.Destroy(gameObject)`) and `NetworkObjectId`. Keep existing `Spawn()/Despawn()` contract intact | `Player.PlayerNetId`, `VesselController.VesselNetId`, `DestroyPlayer`/`DestroyVessel`, `GameDataSO.TryGet*ByNetworkObjectId` consumers | ~60 |
```

</details>

### `9114948d2` — test(port): V19 backfill — 38 camera/silhouette/impact tests

_Claude, 2026-06-12 12:47:49 +0000_

```text
CameraSettingsSO + CustomCameraController (fixed/dynamic ApplySettings,
follow/snap/LateUpdate tracking), VesselCameraCustomizer configure matrix,
SilhouetteConfigSO freeze + SilhouetteController element-bar driving against
the #13 shells, ImpactorBase dispatch + ImpactEffectSO gating + crystal entry
points, VesselExplosionByCrystalEffectSO cooldown/routing. Inert surfaces
documented per deviation (#12/#13 shells, V19 staged impactors, netcode).

857 tests green (605 xunit + 252 NUnit).
```

```text
 Port/tests/CosmicShore.Tests/CameraSilhouetteLayerTests.cs | 1046 ++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 1046 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 1052 lines)</summary>

```diff
diff --git a/Port/tests/CosmicShore.Tests/CameraSilhouetteLayerTests.cs b/Port/tests/CosmicShore.Tests/CameraSilhouetteLayerTests.cs
new file mode 100644
index 000000000..d847ee51f
--- /dev/null
+++ b/Port/tests/CosmicShore.Tests/CameraSilhouetteLayerTests.cs
@@ -0,0 +1,1046 @@
+using System;
+using System.Collections.Generic;
+using System.Reflection;
+using CosmicShore.Core;
+using CosmicShore.Data;
+using CosmicShore.Engine;
+using CosmicShore.Gameplay;
+using CosmicShore.ScriptableObjects;
+using CosmicShore.UI;
+using CosmicShore.Utility;
+
+namespace CosmicShore.Tests;
+
+// V19: camera / silhouette / impact slice — CustomCameraController data+state logic
+// (offsets, mode flags, follow bookkeeping, snap/track), VesselCameraCustomizer wiring
+// into IVesselStatus and ICameraController, SilhouetteConfigSO / CameraSettingsSO
+// defaults, SilhouetteController initialization against the phase-5 shells (element-bar
+// driving via ResourceSystem level changes), ImpactorBase/VesselImpactor dispatch with
+// recording ImpactEffectSO subclasses, and VesselExplosionByCrystalEffectSO pure logic.
+//
+// Engine trigger dispatch is phase-2 physics, so OnTriggerEnter is invoked reflectively
+// (SkimmerLayerTests precedent). Paths that sink into the Deviation #13 view shells
+// (SilhouetteView energy/tint/conveyor forwarding, ElementalBarsView rendering) are
+// inert and covered only down to the shell's bookkeeping surface.
+
+// ── shared doubles (file-local additions; shared VesselLayerTestDoubles untouched) ──
+
+/// <summary>Minimal IPlayer — VesselImpactor.isInitialized only needs non-null.</summary>
+class CameraSilhouettePlayerStub : IPlayer
+{
+    public Domains Domain { get; set; } = Domains.Jade;
+    public string Name { get; set; } = "cam-pilot";
+    public int AvatarId => 0;
+    public string PlayerUUID => Name;
+    public IVessel Vessel => null;
+    public InputController InputController => null;
+    public IInputStatus InputStatus => null;
+    public IRoundStats RoundStats => null;
+    public bool IsActive { get; set; } = true;
+    public bool IsInitializedAsAI => false;
+    public bool IsMultiplayerOwner => false;
+    public bool IsNetworkOwner => false;
+    public bool IsNetworkClient => false;
+    public bool IsLocalUser => true;
+    public ulong PlayerNetId => 0;
+    public ulong VesselNetId => 0;
+    public ulong OwnerClientNetId => 0;
+    public Transform Transform { get; set; }
+    public void InitializeForSinglePlayerMode(IPlayer.InitializeData data, IVessel vessel) { }
+    public void InitializeForMultiplayerMode(IVessel vessel) { }
+    public void ToggleGameObject(bool toggle) { }
+    public void DestroyPlayer() { }
+    public void StartPlayer() { }
+    public void ResetForPlay() { }
+    public void ChangeVessel(IVessel vessel) { }
+}
+
+/// <summary>
+/// StubVesselStatus with a settable vessel class. The shared stub pins VesselType to
+/// Manta with a non-virtual member, so this re-implements IVesselStatus and supplies
+/// the override explicitly — a file-local extension, per the doubles ground rule.
+/// </summary>
+class TypedVesselStatus : StubVesselStatus, IVesselStatus
+{
+    public VesselClassType VesselTypeOverride = VesselClassType.Manta;
+    VesselClassType IVesselStatus.VesselType => VesselTypeOverride;
+}
+
+/// <summary>Recording ICameraController for VesselCameraCustomizer.Configure wiring.</summary>
+class RecordingCameraController : ICameraController
+{
+    public CameraSettingsSO AppliedSettings;
+    public Transform FollowTarget;
+    public readonly List<float> Distances = new();
```

</details>

### `8ea63bcfc` — feat(port): concrete-arc C1 — engine gaps E11-E16 incl. clone reference remap, leaf SOs, 31 tests

_Claude, 2026-06-12 12:57:34 +0000_

```text
- E16 (the survey's riskiest item): CloneGameObject now remaps every cloned
  serialized field and array/List element pointing inside the source tree to
  the clone's counterparts; collections re-allocated never mutated (in-place
  rewrite would corrupt the template); NetworkBehaviour spawn state joined the
  copy stop-set (clones previously aliased the template's NetworkObject handle
  — Despawn(true) on a clone would have destroyed the template); root-only
  (Clone) naming. Survey corruption scenario covered by name in tests.
- E11 FixedString128Bytes; E12 NetworkObject/NetworkObjectId/Despawn(bool);
  E13 AuthenticationService shim; E14 Gradient + TrailRenderer.colorGradient;
  E15 GameObjectInjector facade + Container implicit self-binding
- leaf ports verbatim: VesselPrefabContainer, SO_ProfileIconList,
  PlayerProfileData, VesselTrailCustomization; README substitution table
  gains the Reflex/UGS-auth rows
- ConcreteArcC1Tests: 31 tests (9 clone-remap round-trips incl. external-ref
  isolation, FixedString, Gradient, injector, leaf defaults)

888 tests green (636 xunit + 252 NUnit). Agent-ported in isolated worktree.
```

```text
 Port/README.md                                                        |   2 +
 Port/src/CosmicShore.Engine/Collections/FixedString128Bytes.cs        |  53 +++
 Port/src/CosmicShore.Engine/Compat/EngineCompat.cs                    | 112 ++++-
 Port/src/CosmicShore.Engine/Injection/Container.cs                    |   9 +
 Port/src/CosmicShore.Engine/Injection/GameObjectInjector.cs           |  14 +
 Port/src/CosmicShore.Engine/Math/Gradient.cs                          |  91 ++++
 Port/src/CosmicShore.Engine/Math/GradientAlphaKey.cs                  |  17 +
 Port/src/CosmicShore.Engine/Math/GradientColorKey.cs                  |  17 +
 Port/src/CosmicShore.Engine/Networking/NetworkBehaviour.cs            |  16 +
 Port/src/CosmicShore.Engine/Networking/NetworkObject.cs               |  34 ++
 Port/src/CosmicShore.Engine/Rendering/Renderers.cs                    |  13 +
 Port/src/CosmicShore.Engine/Services/AuthenticationService.cs         |  27 ++
 .../CosmicShore.Game/Controller/Vessel/VesselTrailCustomization.cs    |  62 +++
 .../CosmicShore.Game/ScriptableObjects/SOAP/VesselPrefabContainer.cs  |  51 ++
 Port/src/CosmicShore.Game/ScriptableObjects/SO_ProfileIconList.cs     |  26 +
 Port/src/CosmicShore.Game/UI/Views/PlayerProfileData.cs               |  16 +
 Port/tests/CosmicShore.Tests/ConcreteArcC1Tests.cs                    | 813 ++++++++++++++++++++++++++++++++
 17 files changed, 1369 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1540 lines)</summary>

```diff
diff --git a/Port/README.md b/Port/README.md
index c934e1a65..17ae35d51 100644
--- a/Port/README.md
+++ b/Port/README.md
@@ -53,6 +53,8 @@ same member names — except for these mechanical using-directive substitutions:
 | `using Unity.Netcode;` | `using CosmicShore.Engine.Networking;` |
 | `using Unity.Collections;` | `using CosmicShore.Engine.Collections;` |
 | `using Obvious.Soap;` | `using CosmicShore.Engine.Soap;` |
+| `using Reflex.Attributes;` / `using Reflex.Core;` / `using Reflex.Injectors;` | `using CosmicShore.Engine.Injection;` |
+| `using Unity.Services.Authentication;` | `using CosmicShore.Engine.Services;` |
 | `using Cysharp.Threading.Tasks;` | (phase 1: first-party async — see PORT_PLAN) |
 
 Every ported enum's numeric values are frozen by tests in
diff --git a/Port/src/CosmicShore.Engine/Collections/FixedString128Bytes.cs b/Port/src/CosmicShore.Engine/Collections/FixedString128Bytes.cs
new file mode 100644
index 000000000..dc010f4d9
--- /dev/null
+++ b/Port/src/CosmicShore.Engine/Collections/FixedString128Bytes.cs
@@ -0,0 +1,53 @@
+using System;
+using System.Text;
+
+namespace CosmicShore.Engine.Collections
+{
+    /// <summary>
+    /// Fixed-capacity string for replicated state (E12-family sibling of
+    /// <see cref="FixedString64Bytes"/>), preserving the original type's contract:
+    /// holds up to 125 bytes of UTF-8 (the wire format reserves 3 bytes of the 128
+    /// for length); longer input is truncated at a code-point boundary.
+    /// Implicitly converts to/from <see cref="string"/>.
+    /// </summary>
+    public struct FixedString128Bytes : IEquatable<FixedString128Bytes>
+    {
+        public const int Capacity = 125;
+
+        string _value;
+
+        public FixedString128Bytes(string value) { _value = Truncate(value); }
+
+        public string Value => _value ?? string.Empty;
+        public bool IsEmpty => string.IsNullOrEmpty(_value);
+        public int Length => Value.Length;
+
+        static string Truncate(string s)
+        {
+            if (string.IsNullOrEmpty(s)) return string.Empty;
+            if (Encoding.UTF8.GetByteCount(s) <= Capacity) return s;
+
+            int byteCount = 0;
+            var sb = new StringBuilder();
+            foreach (var rune in s.EnumerateRunes())
+            {
+                int runeBytes = rune.Utf8SequenceLength;
+                if (byteCount + runeBytes > Capacity) break;
+                byteCount += runeBytes;
+                sb.Append(rune.ToString());
+            }
+            return sb.ToString();
+        }
+
+        public static implicit operator FixedString128Bytes(string s) => new(s);
+        public static implicit operator string(FixedString128Bytes f) => f.Value;
+
+        public bool Equals(FixedString128Bytes other) => Value == other.Value;
+        public override bool Equals(object obj) => obj is FixedString128Bytes other && Equals(other);
+        public override int GetHashCode() => Value.GetHashCode();
+        public override string ToString() => Value;
+
+        public static bool operator ==(FixedString128Bytes a, FixedString128Bytes b) => a.Equals(b);
+        public static bool operator !=(FixedString128Bytes a, FixedString128Bytes b) => !a.Equals(b);
+    }
+}
diff --git a/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs b/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs
index bc636b88c..24dfffdf8 100644
--- a/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs
+++ b/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs
@@ -1,4 +1,5 @@
 using System;
+using System.Collections;
 using System.Collections.Generic;
```

</details>

### `bc426ecdd` — feat(port): concrete-arc C2+C3 — PlayerDataService + concrete VesselStatus, 24 tests

_Claude, 2026-06-12 13:17:54 +0000_

```text
- PlayerDataService (363L) verbatim; UGS CloudSave surface staged per the #14
  precedent (7 markers, logged as #14-ext); local default profile, crystal/XP
  math, reward unlocks, OnProfileChanged -> GameDataSO sync all live
- VesselStatus (260L) verbatim — including the source's gameObject.gameObject
  quirk; conformance-freeze test pins that it is a plain MonoBehaviour with NO
  NetworkVariables (the survey expected a NetworkBehaviour — replication lives
  on VesselController, C4); full 10-component VesselStatusRig in tests
- stale V19 AutoPilotEnabled marker in SilhouetteController restored — zero
  AutoPilotEnabled markers remain
- engine: UnityServices.State shim, GameObject.gameObject self-reference;
  README services substitution row extended

912 tests green (660 xunit + 252 NUnit). Agent-ported in isolated worktree.
```

```text
 Port/PORT_PLAN.md                                                   |   1 +
 Port/README.md                                                      |   2 +-
 Port/src/CosmicShore.Engine/SceneGraph/GameObject.cs                |   8 +
 Port/src/CosmicShore.Engine/Services/UnityServices.cs               |  32 ++
 Port/src/CosmicShore.Game/Controller/Vessel/SilhouetteController.cs |   5 +-
 Port/src/CosmicShore.Game/Controller/Vessel/VesselStatus.cs         | 260 ++++++++++++
 Port/src/CosmicShore.Game/UI/Views/PlayerDataService.cs             | 375 ++++++++++++++++++
 Port/tests/CosmicShore.Tests/ConcreteArcC2C3Tests.cs                | 719 ++++++++++++++++++++++++++++++++++
 8 files changed, 1397 insertions(+), 5 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1470 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index b466ec6d0..efab85f9a 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -316,6 +316,7 @@ Next: V8 (VesselTransformer + member restore), rival balance from prompter feedb
 | 9 | ResourceSystem temporary deviations: (a) base class `ElementalShipComponent` → `MonoBehaviour` until IVessel/ElementalFloat port; (b) `[RequireComponent(typeof(IVesselStatus))]` commented until IVesselStatus ports. Class body verbatim. **CLOSED at V6 (2026-06-11)** — both restored verbatim. | Unblocks the elemental core. |
 | 10c | `IVesselStatus` landed (V6) with 13 members commented pending their types: `AIPilot`, `AICinematicBehavior`, `AutoPilotEnabled` (→V18), `AttachedPrism` (→V15), `VesselAnimation` (→V7), `VesselTransformer` (→V8), `Customization` (→V9), `NearFieldSkimmer`/`FarFieldSkimmer` (→V16), `VesselPrismController`/`ActionHandler` (→V17), `VesselCameraCustomizer`/`Silhouette` (→V19). Each restore iteration uncomments its members; V19 closes. **CLOSED 2026-06-12 — IVesselStatus diff-verified verbatim.** | Stages the vessel SCC per VESSEL_LAYER.md. |
 | 11 | `AudioSystem` type-preserving shell (`Instance`, two `PlayGameplaySFX` overloads; bodies no-op) — pulled forward from V15 to V6 because `ActionExecutorRegistry` needs the type. Real port with the phase-5 audio backend. | Keeps ActionExecutorRegistry verbatim. |
+| 14-ext | `PlayerDataService` (C2) stages its UGS CloudSave surface exactly like GameSetting's #14: `UGSDataService` injection, repo read/write, ready-event hooks commented with `PORT Deviation #14 (C2, restore when UGSDataService ports)` markers (7 sites); local default profile, crystal/XP math, reward unlocks, and the OnProfileChanged → GameDataSO sync are live. | Services phase owns the restore. |
 | 16 | `Directory.Build.props` adds CS0169 to NoWarn (alongside CS0649): verbatim Unity-era private fields whose only usages are commented (e.g. `InputController.vessel` until its orientation block revives) or inspector-driven fire it; the Unity compiler tolerated them. | Verbatim fields without warning noise. |
 
 ## Iteration log
diff --git a/Port/README.md b/Port/README.md
index 17ae35d51..1bc7498fe 100644
--- a/Port/README.md
+++ b/Port/README.md
@@ -54,7 +54,7 @@ same member names — except for these mechanical using-directive substitutions:
 | `using Unity.Collections;` | `using CosmicShore.Engine.Collections;` |
 | `using Obvious.Soap;` | `using CosmicShore.Engine.Soap;` |
 | `using Reflex.Attributes;` / `using Reflex.Core;` / `using Reflex.Injectors;` | `using CosmicShore.Engine.Injection;` |
-| `using Unity.Services.Authentication;` | `using CosmicShore.Engine.Services;` |
+| `using Unity.Services.Authentication;` / `using Unity.Services.Core;` | `using CosmicShore.Engine.Services;` |
 | `using Cysharp.Threading.Tasks;` | (phase 1: first-party async — see PORT_PLAN) |
 
 Every ported enum's numeric values are frozen by tests in
diff --git a/Port/src/CosmicShore.Engine/SceneGraph/GameObject.cs b/Port/src/CosmicShore.Engine/SceneGraph/GameObject.cs
index 6c3871dc6..c3ce01785 100644
--- a/Port/src/CosmicShore.Engine/SceneGraph/GameObject.cs
+++ b/Port/src/CosmicShore.Engine/SceneGraph/GameObject.cs
@@ -13,6 +13,14 @@ namespace CosmicShore.Engine
         readonly List<Component> _components = new();
 
         public Transform transform { get; }
+
+        /// <summary>
+        /// Self-reference, matching the original engine's GameObject.gameObject property —
+        /// ported call sites occasionally chain it (e.g. VesselStatus's
+        /// <c>gameObject.gameObject.GetOrAdd&lt;AIPilot&gt;()</c>).
+        /// </summary>
+        public GameObject gameObject => this;
+
         public Scene scene { get; internal set; }
         public int layer;
         public string tag = "Untagged";
diff --git a/Port/src/CosmicShore.Engine/Services/UnityServices.cs b/Port/src/CosmicShore.Engine/Services/UnityServices.cs
new file mode 100644
index 000000000..f491603f8
--- /dev/null
+++ b/Port/src/CosmicShore.Engine/Services/UnityServices.cs
@@ -0,0 +1,32 @@
+namespace CosmicShore.Engine.Services
+{
+    /// <summary>
+    /// Mirror of <c>Unity.Services.Core.ServicesInitializationState</c> so verbatim
+    /// guards like <c>UnityServices.State == ServicesInitializationState.Initialized</c>
+    /// compile unchanged.
+    /// </summary>
+    public enum ServicesInitializationState
+    {
+        Uninitialized = 0,
+        Initializing = 1,
+        Initialized = 2,
+    }
+
+    /// <summary>
+    /// Placeholder shim for the Unity Gaming Services core singleton
+    /// (<c>Unity.Services.Core.UnityServices</c>) until the services phase ports the
+    /// real initialization layer — same precedent as the E13
+    /// <see cref="AuthenticationService"/> shim. Harness-configurable: tests / the CLI
+    /// set <see cref="State"/> directly; the default is benign (uninitialized) so
+    /// verbatim call sites like <c>PlayerDataService.MergeCloudProfile</c>'s auth-id
+    /// guard keep working headless.
+    /// </summary>
+    public static class UnityServices
+    {
+        public static ServicesInitializationState State { get; set; }
+            = ServicesInitializationState.Uninitialized;
+
+        /// <summary>Restore the benign uninitialized default (test isolation helper).</summary>
+        public static void Reset() => State = ServicesInitializationState.Uninitialized;
```

</details>

### `96931fdae` — feat(port): concrete-arc C5 — Player verbatim (529L, zero deviations), BuildHumanCounts restored, 45 tests

_Claude, 2026-06-12 13:36:11 +0000_

```text
- Player ported byte-identical (using-table swaps only): all six NetworkVariables
  with the CLAUDE.md permission matrix, 3-tier display-name fallback with #suffix
  strip, deferred spawn-event protocol (host/AI/remote-client/pure-client paths),
  RequestSetDomain_ServerRpc validation, OnNetDomainChanged -> Domain mirror +
  RoundStats + ShipHelper repaint, PrepareForNewScene
- GameDataSO.BuildHumanCounts restored verbatim — zero 'restore when Player
  ports' markers remain in Port/src
- ConcreteArcC5Tests: 45 tests (init paths, name-fallback tiers, spawn protocol,
  domain RPC validation, mirrors, lifecycle matrix, BuildHumanCounts)

957 tests green (705 xunit + 252 NUnit). Agent-ported in isolated worktree.
```

```text
 Port/src/CosmicShore.Game/Controller/Player/Player.cs          | 528 +++++++++++++++++++++
 Port/src/CosmicShore.Game/Utility/DataContainers/GameDataSO.cs |  42 +-
 Port/tests/CosmicShore.Tests/ConcreteArcC5Tests.cs             | 980 +++++++++++++++++++++++++++++++++++++++
 3 files changed, 1529 insertions(+), 21 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1573 lines)</summary>

```diff
diff --git a/Port/src/CosmicShore.Game/Controller/Player/Player.cs b/Port/src/CosmicShore.Game/Controller/Player/Player.cs
new file mode 100644
index 000000000..33139d5e2
--- /dev/null
+++ b/Port/src/CosmicShore.Game/Controller/Player/Player.cs
@@ -0,0 +1,528 @@
+using System;
+using System.Collections.Generic;
+using CosmicShore.UI;
+using CosmicShore.Gameplay;
+using CosmicShore.Utility;
+using CosmicShore.Engine.Injection;
+using CosmicShore.Engine.Collections;
+using CosmicShore.Engine.Networking;
+using CosmicShore.Engine.Services;
+using CosmicShore.Engine;
+using CosmicShore.Data;
+namespace CosmicShore.Gameplay
+{
+    public class Player : NetworkBehaviour, IPlayer
+    {
+        [FormerlySerializedAs("miniGameData")] [SerializeField]
+        GameDataSO gameData;
+
+        [Inject] private PlayerDataService _injectedPlayerDataService;
+
+        // Fallback to static singleton — Netcode-spawned Players (host's own player)
+        // bypass Reflex's auto-injection since they're instantiated by NetworkManager,
+        // not Instantiate() inside an injected scope.
+        private PlayerDataService playerDataService
+            => _injectedPlayerDataService != null
+                ? _injectedPlayerDataService
+                : PlayerDataService.Instance;
+
+        public NetworkVariable<VesselClassType> NetDefaultVesselType = new(VesselClassType.Random, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
+        public NetworkVariable<Domains> NetDomain = new(Domains.Jade, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
+        public NetworkVariable<FixedString128Bytes> NetName = new(string.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
+        public NetworkVariable<ulong> NetVesselId = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
+        public NetworkVariable<bool> NetIsAI = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
+        public NetworkVariable<int> NetAvatarId = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
+
+        public Domains Domain { get; private set; } = Domains.Jade;
+
+        /// <summary>
+        /// Theme data stashed by <see cref="ClientPlayerVesselInitializer"/> at vessel
+        /// spawn/swap. Used by <see cref="OnNetDomainChanged"/> to repaint the vessel
+        /// when domain replicates after spawn (modal Blue reset, server NormalizeUnassignedHumans,
+        /// shape-mode SetDomain, etc).
+        /// </summary>
+        internal ThemeManagerDataContainerSO _vesselThemeManagerData;
+
+        /// <summary>
+        /// Changes the player's domain at runtime. Used by shape mode to match
+        /// the player's prism color to the collided shape's domain.
+        /// </summary>
+        public void SetDomain(Domains newDomain)
+        {
+            Domain = newDomain;
+        }
+
+        /// <summary>
+        /// Owner-initiated request to change this player's domain.
+        /// NetDomain is server-write, so clients route their selections through this RPC.
+        /// Validated against <see cref="GameDataSO.IsActiveDomain"/> with the session's
+        /// configured <see cref="GameDataSO.RequestedDomainCount"/>; out-of-range picks
+        /// are rejected silently.
+        /// </summary>
+        [ServerRpc] // RequireOwnership = true is the default — only the player's owner may request
+        public void RequestSetDomain_ServerRpc(Domains domain)
+        {
+            using var _ = CosmicShore.Utility.PerformanceBenchmark.NetMarkers.RpcDispatch.Auto();
+            CosmicShore.Utility.PerformanceBenchmark.NetMarkers.CountRpc();
+
+            if (!GameDataSO.IsActiveDomain(domain, gameData.RequestedDomainCount))
+            {
+                CSDebug.LogWarning(
+                    $"[Player] RequestSetDomain_ServerRpc rejected domain {domain} for {NetName.Value} (DC={gameData.RequestedDomainCount})");
+                return;
+            }
+
```

</details>

### `ed0b285be` — feat(port): concrete-arc C4 — VesselController verbatim (364L, zero deviations), 38 tests

_Claude, 2026-06-12 14:14:45 +0000_

```text
- the concrete IVessel NetworkBehaviour: four owner-write kinematic
  NetworkVariables with per-frame replication + NetMarkers instrumentation,
  slowed-transform ServerRpc/ClientRpc pairs, SetPose_ClientRpc, full
  Initialize(player) chain (HUD, camera event, skimmers, transformer, theme
  paint, customization), ChangePlayer AI/client/local matrix,
  NetworkObject.Despawn(true) teardown
- engine E17: NetworkBehaviour.OnDestroy virtual (Netcode parity; reflective
  lifecycle discovery invokes the most-derived override)
- ConcreteArcC4Tests: 38 tests around a prefab-faithful C4VesselRig;
  root-caused + documented the async-void spawn-loop xunit trap
  (StartVessel -> SpawnLoopAsync; StopSpawn before test end)

995 tests green (743 xunit + 252 NUnit). Agent-ported in isolated worktree.
```

```text
 Port/src/CosmicShore.Engine/Networking/NetworkBehaviour.cs      |   9 +
 Port/src/CosmicShore.Game/Controller/Vessel/VesselController.cs | 364 ++++++++++++++
 Port/tests/CosmicShore.Tests/ConcreteArcC4Tests.cs              | 974 ++++++++++++++++++++++++++++++++++++++
 3 files changed, 1347 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 1370 lines)</summary>

```diff
diff --git a/Port/src/CosmicShore.Engine/Networking/NetworkBehaviour.cs b/Port/src/CosmicShore.Engine/Networking/NetworkBehaviour.cs
index f0970a25e..3dc2eb143 100644
--- a/Port/src/CosmicShore.Engine/Networking/NetworkBehaviour.cs
+++ b/Port/src/CosmicShore.Engine/Networking/NetworkBehaviour.cs
@@ -34,6 +34,15 @@ namespace CosmicShore.Engine.Networking
         public virtual void OnNetworkSpawn() { }
         public virtual void OnNetworkDespawn() { }
 
+        /// <summary>
+        /// E17 (C4): virtual destroy hook matching the original Netcode NetworkBehaviour
+        /// surface — ported subclasses write <c>public override void OnDestroy()</c>
+        /// (VesselController, Player). The MonoBehaviour lifecycle discovers the
+        /// most-derived declaration reflectively and invokes it on destruction; this
+        /// base declaration only provides the override target and is a no-op.
+        /// </summary>
+        public virtual void OnDestroy() { }
+
         /// <summary>Bring this behaviour into the networked world. Defaults model single-process host-mode.</summary>
         public void Spawn(bool isServer = true, bool isClient = true, bool isOwner = true, ulong ownerClientId = 0)
         {
diff --git a/Port/src/CosmicShore.Game/Controller/Vessel/VesselController.cs b/Port/src/CosmicShore.Game/Controller/Vessel/VesselController.cs
new file mode 100644
index 000000000..259ad6851
--- /dev/null
+++ b/Port/src/CosmicShore.Game/Controller/Vessel/VesselController.cs
@@ -0,0 +1,364 @@
+using System;
+using CosmicShore.Data;
+using CosmicShore.Utility;
+using CosmicShore.Engine.Networking;
+using CosmicShore.Engine;
+using CosmicShore.Gameplay;
+using CosmicShore.ScriptableObjects;
+using CosmicShore.UI;
+
+namespace CosmicShore.Gameplay
+{
+    /// <summary>
+    /// Combines behaviour of R_LocalVessel and R_NetworkVessel. Behaviour is
+    /// selected at runtime based on <see cref="IsSpawned"/> in multiplayer mode.
+    /// </summary>
+    [RequireComponent(typeof(IVesselStatus))]
+    public class VesselController : NetworkBehaviour, IVessel
+    {
+        [SerializeField]
+        GameDataSO gameData;
+
+        public event Action OnInitialized;
+        public event Action OnBeforeDestroyed;
+
+        IVesselStatus vesselStatus;
+        public IVesselStatus VesselStatus
+        {
+            get
+            {
+                vesselStatus ??= GetComponent<IVesselStatus>();
+                return vesselStatus;
+            }
+        }
+
+        public bool IsNetworkOwner => IsSpawned && IsOwner;
+        public bool IsNetworkClient => IsSpawned && !IsOwner;
+
+        readonly NetworkVariable<float> n_Speed = new(writePerm: NetworkVariableWritePermission.Owner);
+        readonly NetworkVariable<Vector3> n_Course = new(writePerm: NetworkVariableWritePermission.Owner);
+        readonly NetworkVariable<Quaternion> n_BlockRotation = new(writePerm: NetworkVariableWritePermission.Owner);
+        readonly NetworkVariable<bool> n_IsTranslationRestricted =
+            new(writePerm: NetworkVariableWritePermission.Owner);
+
+        public ulong PlayerNetId { get; private set; }
+        public ulong VesselNetId => NetworkObjectId;
+        public ulong OwnerClientNetId => OwnerClientId;
+
+        public override void OnDestroy()
+        {
+            Debug.Log($"<color=#FFFF00>[VESSEL] OnDestroy '{gameObject.name}' — IsSpawned={IsSpawned}, IsServer={IsServer}, IsOwner={IsOwner}, NetObjId={NetworkObjectId}</color>");
+            OnBeforeDestroyed?.Invoke();
+        }
+
+        public override void OnNetworkSpawn()
```

</details>

### `8c347cd51` — feat(port): concrete-arc C6 — spawners verbatim (zero deviations); ARC COMPLETE, exit state proven in CLI

_Claude, 2026-06-12 14:42:41 +0000_

```text
- VesselSpawner, PlayerSpawner, PlayerSpawnerAdapterBase, MiniGame + VolumeTest
  adapters: pure using-substitutions, no markers
- CLI section [6]: spawns a real Player+VesselController pair through verbatim
  PlayerSpawner.SpawnPlayerAndShip from a programmatic prefab; corruption
  sentinel green (clone wires to its OWN controller, template untouched);
  StartPlayer motion @60Hz, ResetForPlay freeze, AI autopilot variant, host-mode
  Player.Spawn() event + 3-tier name fallback, Despawn(true) teardown — exit 0
- ConcreteArcC6Tests: 19 tests (clone fidelity over all 11 vessel classes,
  pipeline ordering, adapter flows, AddPlayer semantics frozen)
- VESSEL_CONCRETE.md marked complete + E16 collection-by-reference limitation
  flagged to the multiplayer-spawn arc; PORT_PLAN NEXT UP -> scoring/arcade arc

1014 tests green (762 xunit + 252 NUnit); CLI exit 0.
```

```text
 Port/PORT_PLAN.md                                                     |  36 +-
 Port/docs/VESSEL_CONCRETE.md                                          |   8 +
 Port/src/CosmicShore.Cli/Program.cs                                   | 296 +++++++++++
 .../Controller/Player/MiniGamePlayerSpawnerAdapter.cs                 |  62 +++
 Port/src/CosmicShore.Game/Controller/Player/PlayerSpawner.cs          |  43 ++
 .../CosmicShore.Game/Controller/Player/PlayerSpawnerAdapterBase.cs    |  45 ++
 .../Controller/Player/VolumeTestPlayerSpawnerAdapter.cs               |  13 +
 Port/src/CosmicShore.Game/Controller/Vessel/VesselSpawner.cs          |  54 ++
 Port/tests/CosmicShore.Tests/ConcreteArcC6Tests.cs                    | 908 ++++++++++++++++++++++++++++++++
 9 files changed, 1447 insertions(+), 18 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1549 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index efab85f9a..3946bcac4 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -429,24 +429,24 @@ Next: V8 (VesselTransformer + member restore), rival balance from prompter feedb
   matrix, InputController Awake wiring, shape-key theory, flare, Update routing).
   **610 tests green (358 + 252)**; client smoke unaffected.
 
-## NEXT UP (iteration 12)
-
-**VESSEL ARC COMPLETE (2026-06-12):** all 19 rows done. V16 skimmer layer, V17
-action layer, V18 AI layer, V19 camera/silhouette/impact slice integrated;
-**Deviation #10c CLOSED** — IVesselStatus is diff-verified verbatim. 819 tests
-green. V18/V19 were salvaged complete from session-limit-cut agent worktrees.
-
-Goal: post-arc consolidation + next arc.
-
-1. **V19 test backfill**: the V19 agent was cut before writing
-   CameraSilhouetteLayerTests — add coverage for CustomCameraController data
-   logic, VesselCameraCustomizer wiring, SilhouetteController init,
-   VesselImpactor dispatch.
-2. **VesselStatus concrete arc**: the NetworkBehaviour itself (unblocked by the
-   completed closure) + IVessel concrete (Vessel.cs) — survey first.
-3. CLI M2 vertical slice (cell + prisms + crystals + scripted vessels, seeded).
-4. Sprint feedback as it arrives.
-5. Update this file, commit, push.
+## NEXT UP (iteration 13)
+
+**CONCRETE ARC COMPLETE (2026-06-12):** C1-C6 all landed. Player, VesselController,
+VesselStatus, PlayerDataService, spawners — all verbatim (C4/C5/C6 with ZERO
+deviations); engine gained E11-E17 incl. the clone reference remap. CLI section [6]
+proves the exit state headlessly (spawn → wire → move → reset → AI → host-mode spawn
+event → despawn). 1014 tests green. The async-void spawn-loop xunit trap is now
+documented in C4 tests + CLI.
+
+Goal: the scoring/arcade arc (largest open marker family), then multiplayer-spawn.
+
+1. **Scoring/arcade survey + arc**: ScoringRuleSO + ScoringMetric chain +
+   SO_ArcadeGame/SO_Game/SO_Vessel — closes GameDataSO's 26 staged markers and
+   unblocks headless game-mode rounds (milestone port-m2: first AI-vs-AI round).
+2. **Multiplayer-spawn arc prerequisite**: deep-clone plain-object collections in
+   the E16 remap (List<Resource> currently copied by reference — flagged by C6).
+3. Sprint feedback as it arrives.
+4. Update this file, commit, push.
 
 1. **V10**: port `GameDataSO` (~783L, single-file oversize accepted) + engine
    `ISession` placeholder. Then V11 (CellConfigDataSO, BlockDensityGrid,
diff --git a/Port/docs/VESSEL_CONCRETE.md b/Port/docs/VESSEL_CONCRETE.md
index d697f5ab3..7e4f8e9bb 100644
--- a/Port/docs/VESSEL_CONCRETE.md
+++ b/Port/docs/VESSEL_CONCRETE.md
@@ -1,3 +1,11 @@
+> **ARC COMPLETE 2026-06-12.** All six rows (C1-C6) landed; the exit state is proven
+> by CLI section [6]: a real Player+VesselController pair spawns through the verbatim
+> PlayerSpawner pipeline, the clone-corruption sentinel passes, motion/reset/AI/host-mode
+> spawn-event/despawn all verified headlessly. KNOWN LIMITATION flagged to the
+> multiplayer-spawn arc: the E16 clone remaps in-tree Component/GameObject/Transform refs
+> but copies plain-object collections (e.g. List<Resource>) by REFERENCE — deep-clone
+> needed before many-vessels-from-one-template spawning.
+
 # Vessel Concrete — Type-Closure Survey & Porting Sequence
 
 Follow-up to `VESSEL_LAYER.md` (V1–V19, complete — IVesselStatus diff-verified
diff --git a/Port/src/CosmicShore.Cli/Program.cs b/Port/src/CosmicShore.Cli/Program.cs
index 4e985eafd..75923c7fe 100644
--- a/Port/src/CosmicShore.Cli/Program.cs
+++ b/Port/src/CosmicShore.Cli/Program.cs
@@ -1,11 +1,19 @@
 using System;
 using System.Collections.Generic;
+using System.Linq;
+using System.Reflection;
 using System.Threading.Tasks;
 using CosmicShore.Data;
 using CosmicShore.Engine;
 using CosmicShore.Engine.Injection;
+using CosmicShore.Engine.Networking;
+using CosmicShore.Engine.Services;
 using CosmicShore.Engine.Soap;
 using CosmicShore.Engine.Tasks;
```

</details>

### `d4beb4685` — feat(port): E16 deep-clone — plain-object graphs clone Unity-faithfully; C6 shared-resource bug fixed

_Claude, 2026-06-12 15:00:09 +0000_

```text
- RemapValue funnel: List<T>/T[] always get fresh containers; [Serializable]
  plain classes deep-clone per field path (inline-by-value, Unity semantics);
  delegate fields inside deep-cloned objects reset to null; depth cap 10 with
  cycle termination; engine-Object remap/external/asset/null semantics intact
  (C1 contract re-verified)
- fixes the C6 flag: cloned vessels no longer share ResourceSystem.Resources
  with their template (3-clone independence tested with the real types)
- residual flagged for a future pass: Dictionary fields still reference-copied
  (Unity would reset to field initializers); SO Instantiate stays shallow
- SYSLIB0050 cleared via the SerializableAttribute pseudo-attribute check

1026 tests green (774 xunit + 252 NUnit), 0 warnings.
```

```text
 Port/src/CosmicShore.Engine/Compat/EngineCompat.cs | 173 ++++++++++++++----
 Port/tests/CosmicShore.Tests/CloneDeepCopyTests.cs | 486 +++++++++++++++++++++++++++++++++++++++++++++++++++
 2 files changed, 625 insertions(+), 34 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 704 lines)</summary>

```diff
diff --git a/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs b/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs
index 24dfffdf8..2755be533 100644
--- a/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs
+++ b/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs
@@ -222,7 +222,10 @@ namespace CosmicShore.Engine
         /// rewrites cloned serialized fields — and array/<see cref="List{T}"/> elements —
         /// that point INSIDE the source tree to their clone counterparts, matching the
         /// original engine's Instantiate. References outside the tree (other scene
-        /// objects, ScriptableObject assets) are left untouched. Only the root gains the
+        /// objects, ScriptableObject assets) are left untouched. Plain [Serializable]
+        /// class graphs (e.g. ResourceSystem.Resources' <c>List&lt;Resource&gt;</c>) are
+        /// deep-cloned the way the original engine's serializer inlines them — see
+        /// <see cref="RemapValue"/> for the full value rules. Only the root gains the
         /// "(Clone)" suffix; children keep their authored names.
         /// </summary>
         static GameObject CloneGameObject(GameObject source)
@@ -295,60 +298,162 @@ namespace CosmicShore.Engine
             }
         }
 
+        /// <summary>
+        /// The original engine's serialization depth limit for nested plain
+        /// ([Serializable]) classes. A plain-object reference nested deeper than this is
+        /// dropped to null, exactly like the engine truncating "Serialization depth limit
+        /// 10 exceeded" graphs. This doubles as the cycle guard: per-field independent
+        /// copies of a cyclic plain graph terminate here instead of recursing forever.
+        /// </summary>
+        const int MaxPlainObjectDepth = 10;
+
         static void RemapField(object target, FieldInfo field, Dictionary<object, object> map)
         {
-            // Value types (and therefore primitives/enums/structs) can't hold scene references.
+            // Value types (and therefore primitives/enums/structs) can't hold scene
+            // references and were already value-copied by CopyFields.
             if (field.FieldType.IsValueType) return;
 
             object value = field.GetValue(target);
             if (value is null) return;
 
+            object replacement = RemapValue(value, map, depth: 0);
+            if (!ReferenceEquals(replacement, value))
+                field.SetValue(target, replacement);
+        }
+
+        /// <summary>
+        /// E16 value rules, applied uniformly to component fields, collection elements, and
+        /// the fields of deep-cloned plain objects:
+        ///   • engine Object inside the source tree → its clone counterpart;
+        ///   • engine Object outside the tree (other scene objects, ScriptableObject
+        ///     assets) → kept, shared by design;
+        ///   • string → kept (immutable);
+        ///   • rank-1 array / <see cref="List{T}"/> → NEW container, elements rerun through
+        ///     these same rules (the original engine never shares a collection instance
+        ///     between a template and its clone, whatever the element type);
+        ///   • plain [Serializable] class → independent deep clone per field path. The
+        ///     original engine inlines plain classes BY VALUE, so aliasing within the
+        ///     template's plain-object graph intentionally does not survive cloning; graphs
+        ///     truncate to null past <see cref="MaxPlainObjectDepth"/>;
+        ///   • everything else (delegate fields on components, dictionaries, framework
+        ///     types, multidimensional arrays) → reference copy — the port's existing
+        ///     semantics for shapes the original engine never serialized.
+        /// <paramref name="depth"/> counts plain-object nesting levels already entered;
+        /// collection containers are transparent to it.
+        /// </summary>
+        static object RemapValue(object value, Dictionary<object, object> map, int depth)
+        {
             // Direct reference into the source tree (declared type may be an interface —
             // the runtime value is what's checked).
             if (map.TryGetValue(value, out var mapped))
-            {
-                field.SetValue(target, mapped);
-                return;
-            }
+                return mapped;
 
             switch (value)
             {
-                // Arrays: field copy aliased the SOURCE's array instance; if any element
-                // points into the tree, give the clone its own remapped array (never
-                // mutate in place — that would corrupt the template).
```

</details>

### `5d9bfaa1a` — feat(port): scoring/arcade arc — full scoring family + SO chain verbatim; GameDataSO marker-free

_Claude, 2026-06-12 15:04:25 +0000_

```text
- ScoringRuleSO, ScoringMetrics (SumByDomain), HexRaceScoringRuleSO (domain-
  aggregated objective, Jade->Ruby->Gold tie-break, golf AssignScores with
  10000+teamDeficit losing ties), composite scoring, trackers, sentinels,
  ScoreResultBuilder; SO_Game/SO_ArcadeGame/SO_Vessel + leaf SOs; ScoringMetric
  enum frozen
- ALL 26 GameDataSO staged markers restored (ScoringRule + SyncFromArcadeGame)
  — GameDataSO is now verbatim; zero scoring/arcade markers remain in Port/
- engine SA1: NetworkTime + NetworkManager.ServerTime, Time.timeAsDouble,
  VideoPlayer asset stub
- staged: LifeForm death events (flora/fauna arc), SO_QuestChain (quest arc)
- ScoringArcadeTests: 28 facts incl. HexRace teammate aggregation + golf rules

1058 tests green (806 xunit + 252 NUnit). PORT_PLAN NEXT UP -> port-m2 headless
AI-vs-AI round.
```

```text
 Port/src/CosmicShore.Data/Enums/ScoringMetric.cs                      |  17 +
 Port/src/CosmicShore.Engine/Networking/NetworkManager.cs              |   7 +
 Port/src/CosmicShore.Engine/Networking/NetworkTime.cs                 |  16 +
 Port/src/CosmicShore.Engine/Rendering/VideoPlayer.cs                  |  12 +
 Port/src/CosmicShore.Engine/Time.cs                                   |   7 +
 .../Controller/Arcade/FriendlyPrismsDestroyedScoring.cs               |  46 +++
 Port/src/CosmicShore.Game/Controller/Arcade/GolfScoreSentinels.cs     |  52 ++++
 Port/src/CosmicShore.Game/Controller/Arcade/IScoreTracker.cs          |   7 +
 Port/src/CosmicShore.Game/Controller/Arcade/ScoreResultBuilder.cs     |  78 +++++
 Port/src/CosmicShore.Game/Controller/Arcade/Scoring/BaseScoring.cs    |  37 +++
 .../src/CosmicShore.Game/Controller/Arcade/Scoring/BaseScoringMode.cs |  20 ++
 .../CosmicShore.Game/Controller/Arcade/Scoring/CompositeScoring.cs    |  87 ++++++
 .../Controller/Arcade/Scoring/CompositeScoringMode.cs                 |  79 +++++
 .../Controller/Arcade/Scoring/FriendlyVolumeDestroyedScoring.cs       |  42 +++
 .../Controller/Arcade/Scoring/HexRaceScoringRuleSO.cs                 |  69 ++++
 .../Controller/Arcade/Scoring/LifeFormsKilledScoring.cs               |  39 +++
 Port/src/CosmicShore.Game/Controller/Arcade/Scoring/ScoreReveal.cs    |  31 ++
 Port/src/CosmicShore.Game/Controller/Arcade/Scoring/ScoringMetrics.cs |  39 +++
 Port/src/CosmicShore.Game/Controller/Arcade/Scoring/ScoringRuleSO.cs  |  97 ++++++
 .../CosmicShore.Game/Controller/Arcade/Scoring/TimePlayedScoring.cs   | 104 +++++++
 Port/src/CosmicShore.Game/ScriptableObjects/SO_ArcadeGame.cs          |  22 ++
 Port/src/CosmicShore.Game/ScriptableObjects/SO_Element.cs             |  33 ++
 Port/src/CosmicShore.Game/ScriptableObjects/SO_Game.cs                |  23 ++
 Port/src/CosmicShore.Game/ScriptableObjects/SO_TrainingGame.cs        |  28 ++
 Port/src/CosmicShore.Game/ScriptableObjects/SO_Vessel.cs              |  83 +++++
 Port/src/CosmicShore.Game/ScriptableObjects/SO_VesselAbility.cs       |  19 ++
 Port/src/CosmicShore.Game/Utility/DataContainers/GameDataSO.cs        |  54 ++--
 Port/tests/CosmicShore.Tests/EnumFreezeTests.cs                       |   8 +
 Port/tests/CosmicShore.Tests/ScoringArcadeTests.cs                    | 537 ++++++++++++++++++++++++++++++++
 30 files changed, 1686 insertions(+), 43 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1931 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 3946bcac4..36ecb12a2 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -429,22 +429,26 @@ Next: V8 (VesselTransformer + member restore), rival balance from prompter feedb
   matrix, InputController Awake wiring, shape-key theory, flare, Update routing).
   **610 tests green (358 + 252)**; client smoke unaffected.
 
-## NEXT UP (iteration 13)
-
-**CONCRETE ARC COMPLETE (2026-06-12):** C1-C6 all landed. Player, VesselController,
-VesselStatus, PlayerDataService, spawners — all verbatim (C4/C5/C6 with ZERO
-deviations); engine gained E11-E17 incl. the clone reference remap. CLI section [6]
-proves the exit state headlessly (spawn → wire → move → reset → AI → host-mode spawn
-event → despawn). 1014 tests green. The async-void spawn-loop xunit trap is now
-documented in C4 tests + CLI.
-
-Goal: the scoring/arcade arc (largest open marker family), then multiplayer-spawn.
-
-1. **Scoring/arcade survey + arc**: ScoringRuleSO + ScoringMetric chain +
-   SO_ArcadeGame/SO_Game/SO_Vessel — closes GameDataSO's 26 staged markers and
-   unblocks headless game-mode rounds (milestone port-m2: first AI-vs-AI round).
-2. **Multiplayer-spawn arc prerequisite**: deep-clone plain-object collections in
-   the E16 remap (List<Resource> currently copied by reference — flagged by C6).
+## NEXT UP (iteration 14)
+
+**Scoring/arcade arc + E16 deep-clone COMPLETE (2026-06-12):** the full scoring
+family (ScoringRuleSO/Metrics/HexRace golf rules/composite/trackers) +
+SO_Game/SO_ArcadeGame/SO_Vessel chain ported; **all 26 GameDataSO markers
+restored — GameDataSO is verbatim**. E16 deep-clones plain-object graphs
+(cloned vessels own their resource state). 1058 tests green. Open staged keys:
+LifeForm death events (flora/fauna arc), SO_QuestChain (quest arc), Dictionary
+clone residual, MaterialStateManager, impactor crystal cases.
+
+Goal: **port-m2 — first full headless game-mode round (AI vs AI)**.
+
+1. Build the headless round harness in the CLI (`--mode hexrace --players N
+   --seed S`): spawn an AI field via the verbatim pipeline (C6), give them a
+   crystal course (cell layer), drive AIPilot + scoring (HexRaceScoringRuleSO)
+   to a winner, print the transcript + ScoreResults. Mark `port-m2` in the
+   milestone log with the commit hash.
+2. Then: multiplayer-spawn arc survey (ServerPlayerVesselInitializer family —
+   E16 prerequisite now met) or flora/fauna ecosystem arc (closes LifeForm keys
+   + brings the conserved-mass food web). Pick by what port-m2 reveals.
 3. Sprint feedback as it arrives.
 4. Update this file, commit, push.
 
diff --git a/Port/src/CosmicShore.Data/Enums/ScoringMetric.cs b/Port/src/CosmicShore.Data/Enums/ScoringMetric.cs
new file mode 100644
index 000000000..37d8f19bf
--- /dev/null
+++ b/Port/src/CosmicShore.Data/Enums/ScoringMetric.cs
@@ -0,0 +1,17 @@
+namespace CosmicShore.Data
+{
+    /// <summary>
+    /// The single per-player stat a scoring rule aggregates by Domain. One metric drives a
+    /// mode's HUD readout, its "remaining" counter, its end condition, and its scoreboard
+    /// secondary line — so the number players watch can never diverge from the number that
+    /// ends the game. Selected per mode on the mode's <c>ScoringRuleSO</c> asset.
+    /// Always assign explicit values to avoid Unity serialization drift.
+    /// </summary>
+    public enum ScoringMetric
+    {
+        Crystals = 0,
+        OmniCrystals = 1,
+        ElementalCrystals = 2,
+        Jousts = 3,
+    }
+}
diff --git a/Port/src/CosmicShore.Engine/Networking/NetworkManager.cs b/Port/src/CosmicShore.Engine/Networking/NetworkManager.cs
index fc09ff791..6a1c4dbdb 100644
--- a/Port/src/CosmicShore.Engine/Networking/NetworkManager.cs
+++ b/Port/src/CosmicShore.Engine/Networking/NetworkManager.cs
@@ -17,5 +17,12 @@ namespace CosmicShore.Engine.Networking
         public bool IsClient { get; set; } = true;
         public bool IsHost => IsServer && IsClient;
         public bool IsListening { get; set; } = true;
+
+        /// <summary>
```

</details>

### `dc2eb8760` — feat(port): MILESTONE port-m2 — first full headless game-mode round (AI vs AI)

_Claude, 2026-06-12 15:26:54 +0000_

```text
dotnet run --project src/CosmicShore.Cli -- --mode hexrace --players 4 --seed 42

- HexRaceRound orchestrator: verbatim AIPilot does the flying (crystal
  registration -> OnCellItemsUpdated -> UpdateCellContent retarget -> Update
  writes sums/diffs -> verbatim VesselTransformer moves the vessel); verbatim
  spawning pipeline builds the field; domains balanced Jade/Ruby/Gold
- end condition is the turn-monitor shape: per-frame
  ScoringRule.IsObjectiveReached (domain-aggregated SumByDomain) ->
  ResolveWinner -> golf AssignScores -> SetResults ranked standings
- deterministic: same seed -> byte-identical transcript (verified two ways);
  different seeds show real lead changes (seed 7: mid-race overtake, Jade wins)
- harness referee stands in only for the un-ported collider/impactor claim
  (proximity < 25u); faithful caveat noted: homogeneous AI skill mirrors
  ServerPlayerVesselInitializerWithAI, contact systems omitted
- HeadlessRoundTests: 4 tests (winner+golf rules, same-seed identity,
  cross-seed divergence); tests drive the same orchestrator in-process

1062 tests green (810 xunit + 252 NUnit); both CLI modes exit 0.
```

```text
 Port/src/CosmicShore.Cli/HexRaceRound.cs              | 551 ++++++++++++++++++++++++++++++++++++++++++++++++
 Port/src/CosmicShore.Cli/Program.cs                   | 111 ++++++++--
 Port/tests/CosmicShore.Tests/CosmicShore.Tests.csproj |   2 +
 Port/tests/CosmicShore.Tests/HeadlessRoundTests.cs    | 137 ++++++++++++
 4 files changed, 779 insertions(+), 22 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 850 lines)</summary>

```diff
diff --git a/Port/src/CosmicShore.Cli/HexRaceRound.cs b/Port/src/CosmicShore.Cli/HexRaceRound.cs
new file mode 100644
index 000000000..7f32604f1
--- /dev/null
+++ b/Port/src/CosmicShore.Cli/HexRaceRound.cs
@@ -0,0 +1,551 @@
+using System;
+using System.Collections.Generic;
+using System.Globalization;
+using System.Linq;
+using System.Reflection;
+using CosmicShore.Data;
+using CosmicShore.Engine;
+using CosmicShore.Engine.Injection;
+using CosmicShore.Engine.Networking;
+using CosmicShore.Engine.Soap;
+using CosmicShore.Gameplay;
+using CosmicShore.ScriptableObjects;
+using CosmicShore.UI;
+using CosmicShore.Utility;
+using Object = CosmicShore.Engine.Object;
+using Random = CosmicShore.Engine.Random;
+
+namespace CosmicShore.Cli
+{
+    /// <summary>Knobs for one headless AI-vs-AI HexRace-style round.</summary>
+    public sealed class HexRaceRoundOptions
+    {
+        public int PlayerCount = 4;
+        public int Seed = 42;
+
+        /// <summary>Per-domain crystal sum that ends the race (GameDataSO.CrystalTargetCount).</summary>
+        public int CrystalTarget = 15;
+
+        /// <summary>Fail-loud frame cap (default 10 simulated minutes @ 60 Hz).</summary>
+        public int MaxFrames = 60 * 60 * 10;
+
+        public float DeltaTime = 1f / 60f;
+
+        /// <summary>Proximity radius (world units) at which a vessel claims the active crystal.</summary>
+        public float ClaimRadius = 25f;
+    }
+
+    /// <summary>One row of the final standings (derived from GameDataSO.Results + RoundStats).</summary>
+    public sealed class HexRaceStanding
+    {
+        public int Rank;
+        public string Name;
+        public Domains Domain;
+        public int Crystals;
+        public float Score;
+        public string ScoreText;
+        public string Secondary;
+    }
+
+    public sealed class HexRaceRoundResult
+    {
+        public bool Finished;
+        public string WinnerName = "";
+        public Domains WinnerDomain = Domains.Blue;
+        public float FinishTime;
+        public int FramesSimulated;
+        public int TotalClaims;
+
+        /// <summary>Deterministic line-by-line log: claims, finish, standings. Same seed → identical list.</summary>
+        public List<string> Transcript = new();
+
+        public List<HexRaceStanding> Standings = new();
+
+        /// <summary>Error/Exception entries captured from the engine log during the round (expected empty).</summary>
+        public List<string> EngineErrors = new();
+    }
+
+    /// <summary>No-op HUD controller for the prefab fixture's serialized slot (headless).</summary>
+    sealed class RoundVesselHud : MonoBehaviour, IVesselHUDController
+    {
+        public void Initialize(IVesselStatus vesselStatus) { }
+        public void SubscribeToEvents() { }
+        public void UnsubscribeFromEvents() { }
+        public void ShowHUD() { }
```

</details>

### `2cfc35aee` — docs(port): mark milestone port-m2 achieved (commit dc2eb876)

_Claude, 2026-06-12 15:27:09 +0000_

```text
 Port/PORT_PLAN.md | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 36ecb12a2..0c31928cd 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -85,7 +85,7 @@ The prompter tests progress without prompting the loop. Contract:
 | Tag | What became testable | Command | Status |
 |---|---|---|---|
 | `port-m1` | CLI engine smoke + deterministic sim (loop, lifecycle, tasks, DI, SOAP, RoundStats) | `cd Port && dotnet run --project src/CosmicShore.Cli` | ✅ 2026-06-11 (commit `25b5582d`) |
-| `port-m2` | First full headless game-mode round (AI vs AI) | `… -- --mode <mode> --seed <n>` | ⬜ |
+| `port-m2` | First full headless game-mode round (AI vs AI) | `cd Port && dotnet run --project src/CosmicShore.Cli -- --mode hexrace --players 4 --seed 42` | ✅ 2026-06-12 (commit `dc2eb876`) |
 | `port-m3` | First rendered frame (PNG artifact in chat + repo) | pull + open artifact | ⬜ |
 | `port-m4` | First interactive desktop build | `… -- --render` | ⬜ |
 
```

</details>

### `289273f58` — feat(port): contact arc — engine trigger physics + crystal impactors; m2 round runs on real contact; PRIME AXIS reorientation

_Claude, 2026-06-12 17:56:50 +0000_

```text
- engine TriggerPass: deterministic per-frame overlap pass (registration-order
  iteration), OnTriggerEnter/OnTriggerExit reflective discovery via
  LifecycleHooks, both-sides delivery, exit-on-destroy; SphereCollider added
- OmniCrystalImpactor/ElementalCrystalImpactor/TeamCrystalImpactor verbatim;
  V19-staged crystal cases in VesselImpactor/SkimmerImpactor restored — zero
  crystal-impactor markers remain
- HexRaceRound claims now flow through the genuine trigger -> ImpactorBase ->
  crystal-effect pipeline (proximity referee deleted); same-seed transcript
  verified byte-identical across two runs, exit 0
- ContactArcTests: 17 tests (trigger semantics, dispatch, restored cases)
- PORT_PLAN: PRIME AXIS reorientation (prompter feedback) — every iteration
  ships a player-feelable client-convergence rung; ladder documented

1079 tests green (827 xunit + 252 NUnit).
```

```text
 Port/PORT_PLAN.md                                                     |  57 +--
 Port/src/CosmicShore.Cli/HexRaceRound.cs                              | 203 +++++++----
 Port/src/CosmicShore.Engine/Compat/EngineCompat.cs                    |   8 +
 Port/src/CosmicShore.Engine/SceneGraph/GameLoop.cs                    |  10 +-
 Port/src/CosmicShore.Engine/SceneGraph/GameObject.cs                  |   2 +
 Port/src/CosmicShore.Engine/SceneGraph/LifecycleMethodCache.cs        |  33 +-
 Port/src/CosmicShore.Engine/SceneGraph/MonoBehaviour.cs               |  17 +
 Port/src/CosmicShore.Engine/SceneGraph/TriggerPass.cs                 | 205 +++++++++++
 Port/src/CosmicShore.Game/Controller/Environment/FlowField/Crystal.cs |  51 ++-
 .../Controller/ImpactEffects/Impactors/ElementalCrystalImpactor.cs    | 132 +++++++
 .../Controller/ImpactEffects/Impactors/OmniCrystalImpactor.cs         | 104 ++++++
 .../Controller/ImpactEffects/Impactors/SkimmerImpactor.cs             |  20 +-
 .../Controller/ImpactEffects/Impactors/TeamCrystalImpactor.cs         |  10 +
 .../Controller/ImpactEffects/Impactors/VesselImpactor.cs              |  44 ++-
 Port/tests/CosmicShore.Tests/ContactArcTests.cs                       | 610 ++++++++++++++++++++++++++++++++
 15 files changed, 1383 insertions(+), 123 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1787 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 0c31928cd..dab90d4ad 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -429,28 +429,41 @@ Next: V8 (VesselTransformer + member restore), rival balance from prompter feedb
   matrix, InputController Awake wiring, shape-key theory, flare, Update routing).
   **610 tests green (358 + 252)**; client smoke unaffected.
 
-## NEXT UP (iteration 14)
-
-**Scoring/arcade arc + E16 deep-clone COMPLETE (2026-06-12):** the full scoring
-family (ScoringRuleSO/Metrics/HexRace golf rules/composite/trackers) +
-SO_Game/SO_ArcadeGame/SO_Vessel chain ported; **all 26 GameDataSO markers
-restored — GameDataSO is verbatim**. E16 deep-clones plain-object graphs
-(cloned vessels own their resource state). 1058 tests green. Open staged keys:
-LifeForm death events (flora/fauna arc), SO_QuestChain (quest arc), Dictionary
-clone residual, MaterialStateManager, impactor crystal cases.
-
-Goal: **port-m2 — first full headless game-mode round (AI vs AI)**.
-
-1. Build the headless round harness in the CLI (`--mode hexrace --players N
-   --seed S`): spawn an AI field via the verbatim pipeline (C6), give them a
-   crystal course (cell layer), drive AIPilot + scoring (HexRaceScoringRuleSO)
-   to a winner, print the transcript + ScoreResults. Mark `port-m2` in the
-   milestone log with the commit hash.
-2. Then: multiplayer-spawn arc survey (ServerPlayerVesselInitializer family —
-   E16 prerequisite now met) or flora/fauna ecosystem arc (closes LifeForm keys
-   + brings the conserved-mass food web). Pick by what port-m2 reveals.
-3. Sprint feedback as it arrives.
-4. Update this file, commit, push.
+## PRIME AXIS — CLIENT CONVERGENCE (prompter reorientation, 2026-06-12)
+
+> "our goal is to convert everything over so a player cannot tell the difference
+> between the original and port. I feel like i should be seeing bigger steps toward
+> closing the gaps with each play."
+
+The headless engine reached verbatim fidelity (V1-V19, C1-C6, scoring, port-m2,
+contact arc) but the PLAYABLE CLIENT still runs the sprint sim — pulls felt
+unchanging because the convergence was invisible from the cockpit. From now on
+**every iteration ships a player-feelable convergence step**: replace a client
+stand-in with the real ported system. Fidelity arcs continue only in service of
+the next rung.
+
+### Convergence ladder (each rung feelable on `git pull` + dotnet run)
+
+1. **Real flight + real AI** (NOW): vessels are real VesselController/VesselStatus/
+   VesselTransformer rigs; player input flows InputController→InputStatus verbatim;
+   drift is the real two-tier analog system; rivals are flown by the real AIPilot.
+2. **Real trails = prisms**: VesselPrismController spawns real Prisms (visible
+   blocks, conserved mass); real Skimmer contact grants energy through the trigger
+   pipeline (contact arc landed).
+3. **Real crystals/impactors**: claims via OnTriggerEnter → CrystalImpactor family
+   (landed for the CLI round; bring to the client).
+4. **Real scoring + HUD semantics**: HexRaceScoringRuleSO domain-aggregated end,
+   golf standings; domains share totals.
+5. **Real look**: SO_ColorSet domain palettes + SO_MaterialSet-driven visuals.
+6. Onward: cells/fauna ambience, more vessel classes, game modes — always through
+   the real systems.
+
+## NEXT UP (iteration 16)
+
+1. **Rung 1**: client-convergence agent — rebuild SkimRace's sim on the real rig
+   (real VesselTransformer flight, real analog drift, real AIPilot rivals).
+2. Then rung 2 (real prism trails + Skimmer contact in the client).
+3. Update this file, commit, push.
 
 1. **V10**: port `GameDataSO` (~783L, single-file oversize accepted) + engine
    `ISession` placeholder. Then V11 (CellConfigDataSO, BlockDensityGrid,
diff --git a/Port/src/CosmicShore.Cli/HexRaceRound.cs b/Port/src/CosmicShore.Cli/HexRaceRound.cs
index 7f32604f1..f24f15e3d 100644
--- a/Port/src/CosmicShore.Cli/HexRaceRound.cs
+++ b/Port/src/CosmicShore.Cli/HexRaceRound.cs
@@ -3,6 +3,7 @@ using System.Collections.Generic;
 using System.Globalization;
 using System.Linq;
 using System.Reflection;
+using CosmicShore.Core;
 using CosmicShore.Data;
 using CosmicShore.Engine;
 using CosmicShore.Engine.Injection;
```

</details>

### `6a82b534c` — feat(port): convergence rung 1 — SkimRace client flies the real rig

_Claude, 2026-06-12 18:58:34 +0000_

```text
SkimRaceSim rebuilt around SkimRacePilot: real Player + VesselController +
VesselStatus + VesselTransformer + ResourceSystem per pilot; rivals flown by
the real AIPilot with per-pilot CellRuntimeDataSO course registries for
crystal retargeting. RaceWindow's input sink is now the rig's real
InputStatus (SkimInputStatus deleted); keyboard fallback edges raise the
real SOAP button events; gamepad yaw+roll inversions preserved post-strategy.
Trails remain visual ribbons until rung 2 (real prisms).

ClientConvergenceTests drives the director in-process (no Silk types touched).
DebugExtensionsTests made config-agnostic: CSDebug info logs are
[Conditional("DEBUG")] and strip out of Release — assert per-config.
Gate green in BOTH configs: 832 + 252 tests.

PORT_PLAN: rung 1 marked done with headless evidence (4 pilots claiming,
crystals [8,3,8,3] @ frame 1200, exit 0); NEXT UP -> rung 2.
```

```text
 Port/PORT_PLAN.md                                      |   34 +-
 Port/src/CosmicShore.Client/RaceWindow.cs              |  194 ++++----
 Port/src/CosmicShore.Client/SkimInputStatus.cs         |   60 ---
 Port/src/CosmicShore.Client/SkimRaceSim.cs             | 1202 +++++++++++++++++++++++++++++++---------------
 Port/tests/CosmicShore.Tests/ClientConvergenceTests.cs |  251 ++++++++++
 Port/tests/CosmicShore.Tests/CosmicShore.Tests.csproj  |    5 +
 Port/tests/CosmicShore.Tests/UtilityPortTests.cs       |   14 +
 7 files changed, 1205 insertions(+), 555 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 2188 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index dab90d4ad..994302ba3 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -444,9 +444,13 @@ the next rung.
 
 ### Convergence ladder (each rung feelable on `git pull` + dotnet run)
 
-1. **Real flight + real AI** (NOW): vessels are real VesselController/VesselStatus/
-   VesselTransformer rigs; player input flows InputController→InputStatus verbatim;
-   drift is the real two-tier analog system; rivals are flown by the real AIPilot.
+1. **Real flight + real AI** ✅ (iteration 16): vessels are real VesselController/
+   VesselStatus/VesselTransformer rigs (`SkimRacePilot` in SkimRaceSim.cs); player
+   input writes the rig's real `InputStatus` (`RaceWindow._playerStatus = _pilot.Input`,
+   SkimInputStatus deleted); rivals are flown by the real AIPilot with per-pilot
+   `CellRuntimeDataSO` course registries for crystal retargeting. Verified headless:
+   4 pilots claiming (frame 1200: crystals [8,3,8,3], 22 claims), exit 0, HUD/minimap
+   intact, yaw+roll gamepad inversions preserved.
 2. **Real trails = prisms**: VesselPrismController spawns real Prisms (visible
    blocks, conserved mass); real Skimmer contact grants energy through the trigger
    pipeline (contact arc landed).
@@ -458,21 +462,21 @@ the next rung.
 6. Onward: cells/fauna ambience, more vessel classes, game modes — always through
    the real systems.
 
-## NEXT UP (iteration 16)
+## NEXT UP (iteration 17)
 
-1. **Rung 1**: client-convergence agent — rebuild SkimRace's sim on the real rig
-   (real VesselTransformer flight, real analog drift, real AIPilot rivals).
-2. Then rung 2 (real prism trails + Skimmer contact in the client).
+1. **Rung 2**: real prism trails + Skimmer contact in the client. Replace
+   SkimRaceSim's visual `TrailPoint` ribbons with real `VesselPrismController`
+   spawning (StopSpawn before any test return — async-void trap) and grant
+   trail-skim energy through the real Skimmer trigger pipeline (contact arc
+   landed). Trails must render as prism blocks; skim detection must come from
+   `OnTriggerEnter/Exit`, not distance checks.
+2. Then rung 3 (real crystal claims via OnTriggerEnter → CrystalImpactor family
+   in the client; landed for the CLI round already).
 3. Update this file, commit, push.
 
-1. **V10**: port `GameDataSO` (~783L, single-file oversize accepted) + engine
-   `ISession` placeholder. Then V11 (CellConfigDataSO, BlockDensityGrid,
-   CellRuntimeDataSO) if the iteration has room.
-2. Sprint: react to prompter feedback on the S6 field race (drift feel, element
-   balance, rival pressure); fauna ambience / more hulls / shape modes on request.
-3. Grow the CLI toward an M2 vertical slice: one cell + crystals + 2 scripted vessels
-   exchanging resource/elemental state on a seeded run.
-4. Update this file (status tables, iteration log, NEXT UP), commit, push.
+Note (test config): `CSDebug.Log/LogFormat` are `[Conditional("DEBUG")]` — info
+logs strip out of Release. DebugExtensionsTests asserts per-config (`#if DEBUG`).
+Gate BOTH configs when touching logging paths.
 
 ## Loop protocol (every iteration)
 
diff --git a/Port/src/CosmicShore.Client/RaceWindow.cs b/Port/src/CosmicShore.Client/RaceWindow.cs
index e8c41c1dc..2629fb270 100644
--- a/Port/src/CosmicShore.Client/RaceWindow.cs
+++ b/Port/src/CosmicShore.Client/RaceWindow.cs
@@ -35,12 +35,14 @@ namespace CosmicShore.Client
         IInputContext _inputContext;
 
         GameLoop _loop;
-        SkimRaceController _race;
-        List<SkimRaceController> _rivals;
-        readonly SkimInputStatus _playerStatus = new();
+        SkimRaceDirector _race;          // race rules — flight itself is the real ported rig
+        SkimRacePilot _pilot;            // the human's rig (vessel transform, stats, resources)
+        IInputStatus _playerStatus;      // the rig's REAL InputStatus (V7) — strategy writes here
         readonly GamepadInputStrategy _gamepadStrategy = new(); // the ported, authentic dual-stick scheme
         EngineInput.Gamepad _shimPad;
         bool _prevA, _prevStart;
+        bool _prevKbSpace, _prevKbShift; // keyboard fallback edges → real SOAP button events
+        bool _screenshotAutopilot;
 
         uint _program;
         int _uMvp;
@@ -129,23 +131,23 @@ namespace CosmicShore.Client
                 {
                     if (key == Key.Escape) _window.Close();
```

</details>

### `68907174e` — feat(port): ThemeManager + GameFeedAPI verbatim — rung-5 groundwork

_Claude, 2026-06-12 19:11:47 +0000_

```text
ThemeManager (115L, byte-verbatim modulo using substitution): generates the
four per-domain SO_MaterialSet copies from SO_ColorSet at Awake, writes
ThemeManagerDataContainerSO.TeamMaterialSets, hands the ColorSet to
GameFeedAPI as the single domain-color source.

GameFeedAPI + GameFeedPayload + ScriptableEventGameFeedPayload ported
verbatim; the TMP feed views (GameEventFeed/GameFeedEntry) and the
DOTween-bound GameFeedSettingsSO stay deferred with the UI-shell deviations.

Engine Resources gains the path-keyed Register(path, asset)/Load<T>(path)
the original Resources.Load contract needs (registry-backed until the
content phase wires real loading).

8 new tests (domain material generation, copy independence, color
application, crystal index selection, feed post/joust coloring, path
registry). Gate green both configs: 840 + 252.
```

```text
 Port/PORT_PLAN.md                                                     |  11 ++
 Port/src/CosmicShore.Engine/Compat/EngineCompat.cs                    |  14 +-
 Port/src/CosmicShore.Game/Controller/Managers/ThemeManager.cs         | 115 ++++++++++++++
 Port/src/CosmicShore.Game/UI/GameEventFeed/GameFeedAPI.cs             |  51 +++++++
 Port/src/CosmicShore.Game/UI/GameEventFeed/GameFeedPayload.cs         |  28 ++++
 .../UI/GameEventFeed/ScriptableEventGameFeedPayload.cs                |  11 ++
 Port/tests/CosmicShore.Tests/ThemeAndGameFeedTests.cs                 | 256 ++++++++++++++++++++++++++++++++
 7 files changed, 485 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 540 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 994302ba3..09f33f67d 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -478,6 +478,17 @@ Note (test config): `CSDebug.Log/LogFormat` are `[Conditional("DEBUG")]` — inf
 logs strip out of Release. DebugExtensionsTests asserts per-config (`#if DEBUG`).
 Gate BOTH configs when touching logging paths.
 
+Rung-5 groundwork (landed alongside iteration 17): `ThemeManager` ported verbatim
+(the single writer of `ThemeManagerDataContainerSO.TeamMaterialSets` — generates the
+4 per-domain SO_MaterialSet copies from SO_ColorSet, and hands the ColorSet to
+`GameFeedAPI`). GameFeedAPI + GameFeedPayload + ScriptableEventGameFeedPayload
+ported (GameEventFeed/GameFeedEntry TMP views + GameFeedSettingsSO (DOTween Ease)
+deferred with the UI-shell deviations). Engine `Resources` gained the path-keyed
+`Register(path, asset)` / `Load<T>(path)` the original Resources.Load contract
+needs. When rung 5 lands in the client: instantiate a ThemeManager with a wired
+container at startup and read `GetTeam*Material` per domain for prism/vessel draw
+colors.
+
 ## Loop protocol (every iteration)
 
 1. `export PATH=/opt/dotnet:$PATH` (reinstall SDK via dotnet-install.sh if container is fresh).
diff --git a/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs b/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs
index 796133465..989db4994 100644
--- a/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs
+++ b/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs
@@ -119,9 +119,21 @@ namespace CosmicShore.Engine
     public static class Resources
     {
         static readonly List<ScriptableObject> Registry = new();
+        static readonly Dictionary<string, Object> PathRegistry = new();
 
         public static void Register(ScriptableObject asset) { if (!Registry.Contains(asset)) Registry.Add(asset); }
-        public static void Clear() => Registry.Clear();
+
+        /// <summary>Registers an asset at a Resources-relative path for <see cref="Load{T}"/>.</summary>
+        public static void Register(string path, Object asset) => PathRegistry[path] = asset;
+
+        public static void Clear() { Registry.Clear(); PathRegistry.Clear(); }
+
+        /// <summary>
+        /// Original engine contract: returns the asset registered at the Resources-relative
+        /// path, or null when nothing (or a different type) is registered there.
+        /// </summary>
+        public static T Load<T>(string path) where T : Object
+            => PathRegistry.TryGetValue(path, out var asset) ? asset as T : null;
 
         public static T[] FindObjectsOfTypeAll<T>() where T : ScriptableObject
         {
diff --git a/Port/src/CosmicShore.Game/Controller/Managers/ThemeManager.cs b/Port/src/CosmicShore.Game/Controller/Managers/ThemeManager.cs
new file mode 100644
index 000000000..1eddadcec
--- /dev/null
+++ b/Port/src/CosmicShore.Game/Controller/Managers/ThemeManager.cs
@@ -0,0 +1,115 @@
+using CosmicShore.Engine;
+using CosmicShore.Data;
+using CosmicShore.ScriptableObjects;
+using CosmicShore.UI;
+
+
+namespace CosmicShore.Gameplay
+{
+
+    public class ThemeManager : MonoBehaviour
+    {
+        [SerializeField] ThemeManagerDataContainerSO _dataContainer;
+
+        void Awake()
+        {
+            var GreenTeamMaterialSet = GenerateDomainMaterialSet(_dataContainer.ColorSet.JadeColors, "Green");
+            var RedTeamMaterialSet = GenerateDomainMaterialSet(_dataContainer.ColorSet.RubyColors, "Red");
+            var GoldTeamMaterialSet = GenerateDomainMaterialSet(_dataContainer.ColorSet.GoldColors, "Gold");
+            var BlueTeamMaterialSet = GenerateDomainMaterialSet(_dataContainer.ColorSet.BlueColors, "Blue");
+
+            _dataContainer.TeamMaterialSets = new() {
+                { Domains.Jade, GreenTeamMaterialSet },
+                { Domains.Ruby,   RedTeamMaterialSet },
+                { Domains.Gold,  GoldTeamMaterialSet },
+                { Domains.Blue,  BlueTeamMaterialSet },
```

</details>

### `71590c294` — feat(port): ElementalComebackSystem + profile verbatim — rung-4 groundwork

_Claude, 2026-06-12 19:22:24 +0000_

```text
Both files byte-verbatim modulo using substitutions (UnityEngine→Engine,
Reflex.Attributes→Engine.Injection). The comeback system composes the
elementals fundamental with domain aggregation: buffs size to the TEAM
deficit from the leading domain (a personally-trailing player on the
leading domain gets nothing), write only the 0.0–1.5 normalized band so
the base pips below 0 stay reserved for the overtake impact effect, and
deactivate on turn end.

7 tests: profile per-vessel selection + element switches, trailing-domain
buff vs leading-domain none, domain-aggregate (not individual) deficit,
1.5 ceiling clamp, initial-level application at turn start, turn-end
deactivation. Gate green both configs: 847 + 252.
```

```text
 Port/PORT_PLAN.md                                                     |   8 +
 .../src/CosmicShore.Game/Controller/Arcade/ElementalComebackSystem.cs | 314 ++++++++++++++++++++++++++++++++
 .../CosmicShore.Game/ScriptableObjects/SO_ElementalComebackProfile.cs |  79 ++++++++
 Port/tests/CosmicShore.Tests/ElementalComebackTests.cs                | 256 ++++++++++++++++++++++++++
 4 files changed, 657 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 686 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 09f33f67d..f0cbf4e94 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -478,6 +478,14 @@ Note (test config): `CSDebug.Log/LogFormat` are `[Conditional("DEBUG")]` — inf
 logs strip out of Release. DebugExtensionsTests asserts per-config (`#if DEBUG`).
 Gate BOTH configs when touching logging paths.
 
+Rung-4 groundwork (landed alongside iteration 17): `ElementalComebackSystem` +
+`SO_ElementalComebackProfile` ported verbatim — comeback buffs sized to the DOMAIN
+deficit through the elementals fundamental (leading-domain players get nothing even
+when personally trailing; clamped to the 0.0–1.5 band so base pips stay reserved
+for the overtake effect). 7 tests cover profile selection, domain aggregation,
+clamping, initial levels, and turn-end deactivation. When rung 4 lands in the
+client: attach alongside the race director with a profile + the shared GameDataSO.
+
 Rung-5 groundwork (landed alongside iteration 17): `ThemeManager` ported verbatim
 (the single writer of `ThemeManagerDataContainerSO.TeamMaterialSets` — generates the
 4 per-domain SO_MaterialSet copies from SO_ColorSet, and hands the ColorSet to
diff --git a/Port/src/CosmicShore.Game/Controller/Arcade/ElementalComebackSystem.cs b/Port/src/CosmicShore.Game/Controller/Arcade/ElementalComebackSystem.cs
new file mode 100644
index 000000000..dca39d7e9
--- /dev/null
+++ b/Port/src/CosmicShore.Game/Controller/Arcade/ElementalComebackSystem.cs
@@ -0,0 +1,314 @@
+using System.Collections.Generic;
+using CosmicShore.Core;
+using CosmicShore.Gameplay;
+using CosmicShore.Utility;
+using CosmicShore.Engine.Injection;
+using CosmicShore.Engine;
+using CosmicShore.Data;
+using CosmicShore.ScriptableObjects;
+using System.Linq;
+namespace CosmicShore.Gameplay
+{
+    /// <summary>
+    /// Applies elemental buffs to losing players based on their score difference from the leader.
+    /// Attach to minigame scene alongside the minigame controller. Assign a comeback profile
+    /// to configure per-vessel, per-element weights.
+    ///
+    /// Operates only in the 0.0–1.5 normalized range (levels 0–15). The first 5 base pips
+    /// (levels -5 to 0) are reserved for the overtake impact effect and are never touched here.
+    /// </summary>
+    public class ElementalComebackSystem : MonoBehaviour
+    {
+        /// <summary>
+        /// Which stat to use when calculating who is ahead/behind.
+        /// HexRace tracks elapsed time as Score (same for everyone) so use CrystalsCollected.
+        /// CrystalCapture uses Score directly.
+        /// </summary>
+        public enum ScoreDifferenceSource
+        {
+            Score,
+            CrystalsCollected,
+        }
+
+        [Header("Config")]
+        [SerializeField] SO_ElementalComebackProfile comebackProfile;
+        [Inject] GameDataSO gameData;
+
+        [Header("Scoring")]
+        [Tooltip("Which stat drives the comeback calculation")]
+        [SerializeField] ScoreDifferenceSource differenceSource = ScoreDifferenceSource.CrystalsCollected;
+        [Tooltip("For Score source: enable when lower score is better (e.g. race times)")]
+        [SerializeField] bool useGolfRules;
+
+        [Header("Update Settings")]
+        [Tooltip("How often (in seconds) to recalculate comeback buffs")]
+        [SerializeField] float updateInterval = 1f;
+
+        [Header("Audio")]
+        [Tooltip("Minimum seconds between comeback audio events for the same element. " +
+                 "Prevents the sound firing every update tick while the buff is held.")]
+        [SerializeField, Min(0f)] float comebackAudioCooldown = 3f;
+
+        [Header("Debug")]
+        [SerializeField] bool debugLogging;
+
+        static readonly Element[] AllElements =
```

</details>

### `3f8a0a648` — feat(port): TurnMonitor + CrystalCollisionTurnMonitor — rung-4 groundwork

_Claude, 2026-06-12 19:32:59 +0000_

```text
TurnMonitor base ported with the sanctioned UniTask→GameTask mappings
(WaitUntil/Delay/Yield; async UniTaskVoid→async Task). The crystal monitor
ports verbatim except one deviation: the optionalEnvironment
waypoint-derived target restores when SpawnableWaypointTrack ports —
explicit inspector target and the 39 fallback are live now.

5 tests: threshold check, 39 fallback, remaining-count display event
(subscribe on start / unsubscribe on stop), base Update end-of-turn fires
OnTurnEnded exactly once then stops, ResetMonitor(restart) reruns a second
turn. Every test stops its monitor and ticks the loop before returning
(async-loop trap).

CS0414 joins NoWarn: verbatim serialized fields whose only consumer is a
PORT-deviated path. Gate green both configs: 852 + 252.
```

```text
 Port/Directory.Build.props                                            |   6 +-
 Port/PORT_PLAN.md                                                     |   8 ++
 .../Controller/Arcade/TurnMonitors/CrystalCollisionTurnMonitor.cs     |  78 ++++++++++++
 .../CosmicShore.Game/Controller/Arcade/TurnMonitors/TurnMonitor.cs    | 148 ++++++++++++++++++++++
 Port/tests/CosmicShore.Tests/TurnMonitorTests.cs                      | 210 ++++++++++++++++++++++++++++++++
 5 files changed, 448 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 473 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index f0cbf4e94..bf6940a4d 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -478,6 +478,14 @@ Note (test config): `CSDebug.Log/LogFormat` are `[Conditional("DEBUG")]` — inf
 logs strip out of Release. DebugExtensionsTests asserts per-config (`#if DEBUG`).
 Gate BOTH configs when touching logging paths.
 
+Rung-4 groundwork (landed alongside iteration 17, part 2): `TurnMonitor` (base) +
+`CrystalCollisionTurnMonitor` ported (UniTask→GameTask mechanical mappings; one
+deviation: `optionalEnvironment` waypoint-derived target restores when
+`SpawnableWaypointTrack` ports — explicit target + 39 fallback work now). 5 tests.
+CS0414 added to NoWarn (verbatim serialized fields whose only consumer is a
+deviated path). When rung 4 lands in the client: the race's crystal-target end
+condition runs through this monitor against the shared RoundStats.
+
 Rung-4 groundwork (landed alongside iteration 17): `ElementalComebackSystem` +
 `SO_ElementalComebackProfile` ported verbatim — comeback buffs sized to the DOMAIN
 deficit through the elementals fundamental (leading-domain players get nothing even
diff --git a/Port/src/CosmicShore.Game/Controller/Arcade/TurnMonitors/CrystalCollisionTurnMonitor.cs b/Port/src/CosmicShore.Game/Controller/Arcade/TurnMonitors/CrystalCollisionTurnMonitor.cs
new file mode 100644
index 000000000..552d8ae21
--- /dev/null
+++ b/Port/src/CosmicShore.Game/Controller/Arcade/TurnMonitors/CrystalCollisionTurnMonitor.cs
@@ -0,0 +1,78 @@
+using System; // Required for Action
+using CosmicShore.Gameplay;
+using CosmicShore.Data;
+using CosmicShore.Engine;
+using CosmicShore.Utility;
+using System.Linq;
+
+namespace CosmicShore.Gameplay
+{
+    public class CrystalCollisionTurnMonitor : TurnMonitor
+    {
+        [Tooltip("Crystal target to end the turn. When non-zero, overrides the waypoint-calculated count. " +
+                 "Leave at 0 to auto-calculate from optionalEnvironment waypoints.")]
+        [SerializeField] protected int CrystalCollisions;
+        protected IRoundStats ownStats;
+
+        [Header("Optional Configuration")]
+        // PORT Deviation (restore when SpawnableWaypointTrack ports): [SerializeField] SpawnableWaypointTrack optionalEnvironment;
+        [SerializeField] int optionalLaps = 4;
+
+        public override void StartMonitor()
+        {
+            CrystalCollisions = GetCrystalCollisionCount();
+
+            InitializeOwnStats();
+            if (ownStats != null) ownStats.OnCrystalsCollectedChanged += UpdateCrystals;
+            UpdateCrystals(ownStats);
+            base.StartMonitor();
+        }
+
+        public override void StopMonitor()
+        {
+            base.StopMonitor();
+            if (ownStats != null) ownStats.OnCrystalsCollectedChanged -= UpdateCrystals;
+        }
+
+        public override bool CheckForEndOfTurn()
+        {
+            if (ownStats == null) return false;
+            return ownStats.CrystalsCollected >= CrystalCollisions;
+        }
+
+        protected virtual void UpdateCrystals(IRoundStats stats) => UpdateCrystalsRemainingUI();
+
+        protected virtual void UpdateCrystalsRemainingUI()
+        {
+            string message = GetRemainingCrystalsCountToCollect();
+            if (onUpdateTurnMonitorDisplay) onUpdateTurnMonitorDisplay.Raise(message);
+        }
+
+        public string GetRemainingCrystalsCountToCollect()
+        {
+            InitializeOwnStats();
+            if (ownStats == null) return CrystalCollisions.ToString();
+            int remaining = CrystalCollisions - ownStats.CrystalsCollected;
```

</details>

### `214cf1ec8` — feat(port): convergence rung 2 — real prism trails + real Skimmer contact

_Claude, 2026-06-12 19:43:49 +0000_

```text
Trails are now conserved-mass Prisms spawned through the real
VesselPrismController: SkimRacePrismFactory (new SkimRacePrisms.cs) answers
the controller's spawn channel with the full V15 prism family (renderer,
BoxCollider, animators, PrismTeamManager/StateManager, Prism, PrismImpactor,
ImpactCollider); PrismGrowthDriver replicates PrismScaleManager's growth
math. TrailPoint ribbons and EmitTrail deleted; RaceWindow draws the real
prisms as domain-tinted oriented slabs. Only race-restart DespawnAll removes
prisms — nothing decays.

Trail-skim energy now flows through the genuine trigger pipeline: near-field
Skimmer (trigger SphereCollider + SkimmerImpactor + ImpactCollider) →
TriggerPass → ImpactorBase.OnTriggerEnter → SkimRaceTrailSkimEnergyEffectSO.
Skimmer Scale ElementalFloat binds to Mass (claims grow skim reach 7→9.5).
SkimProximity distance-scan deleted; own-fresh-trail protection is the
verbatim waitTillOutsideSkimmer collider-arming delay.

Engine perf (behavior-preserving, required at prism scale): TriggerPass pair
scan walks trigger indices (O(n·T)); GameLoop.UnregisterBehaviour
binary-searches the sorted registry. SkimRaceDirector.Shutdown()
deterministically stops all spawn loops.

4 new convergence tests (real-controller spawn, 2-sim-minute conserved-mass
soak, trigger-enter energy grant, full race over >100-prism prismscape).
Gate green both configs: 856 + 252. Headless 1200-frame run reproduces
rung-1 claim determinism with trail = 786 real prisms, exit 0.
```

```text
 Port/PORT_PLAN.md                                      |  66 ++++++++++---
 Port/src/CosmicShore.Client/RaceWindow.cs              |  78 ++++++++++-----
 Port/src/CosmicShore.Client/SkimRacePrisms.cs          | 217 ++++++++++++++++++++++++++++++++++++++++
 Port/src/CosmicShore.Client/SkimRaceSim.cs             | 253 ++++++++++++++++++++++++++---------------------
 Port/src/CosmicShore.Engine/SceneGraph/GameLoop.cs     |  14 ++-
 Port/src/CosmicShore.Engine/SceneGraph/TriggerPass.cs  |  48 +++++++--
 Port/tests/CosmicShore.Tests/ClientConvergenceTests.cs | 227 ++++++++++++++++++++++++++++++++++++++++--
 7 files changed, 734 insertions(+), 169 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1272 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index bf6940a4d..311cb616f 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -429,6 +429,28 @@ Next: V8 (VesselTransformer + member restore), rival balance from prompter feedb
   matrix, InputController Awake wiring, shape-key theory, flare, Update routing).
   **610 tests green (358 + 252)**; client smoke unaffected.
 
+- **Iteration 17** (2026-06-12): **Convergence rung 2 — real prism trails + real
+  Skimmer contact in the playable client.** New `SkimRacePrisms.cs`
+  (SkimRacePrismFactory answering the real VesselPrismController spawn channel with
+  the full V15 prism GameObject family; PrismGrowthDriver replicating
+  PrismScaleManager's growth math per prism; SkimContactTracker mirroring trigger
+  enter/exit overlap state; SkimRaceTrailSkimEnergyEffectSO — the skim-energy race
+  rule as a concrete SkimmerPrismEffectSO inside the real impactor dispatch).
+  SkimRaceSim: TrailPoint ribbons + distance-skim deleted; near-field skimmer rigged
+  (trigger sphere + SkimmerImpactor + Mass-bound Scale ElementalFloat);
+  prism-controller wiring (channel, skimmer ref, BaseScale 5×1×6, wavelength 6);
+  director Shutdown(); theme grew real (data-only) block-state materials.
+  RaceWindow renders the REAL prisms (oriented slabs from each Prism's transform,
+  per-corner camera fade) and reports real prism counts; burst cull made
+  expansion-aware. Engine perf, behavior-preserving: TriggerPass trigger-indexed
+  pair scan (identical visitation order; O(n·T) instead of O(n²) over conserved
+  prism fields), GameLoop.UnregisterBehaviour binary search (mass teardown was
+  quadratic). 4 new ClientConvergenceTests (spawn-through-real-controller,
+  conserved-mass soak, skimmer-trigger-enter energy grant, full race → Finished
+  with golf-scored winner). **1088 tests green (836 + 252) in BOTH configs**;
+  headless 300/1200-frame runs exit 0 with rung-1-identical claim determinism and
+  real-prism trail counts.
+
 ## PRIME AXIS — CLIENT CONVERGENCE (prompter reorientation, 2026-06-12)
 
 > "our goal is to convert everything over so a player cannot tell the difference
@@ -451,9 +473,24 @@ the next rung.
    `CellRuntimeDataSO` course registries for crystal retargeting. Verified headless:
    4 pilots claiming (frame 1200: crystals [8,3,8,3], 22 claims), exit 0, HUD/minimap
    intact, yaw+roll gamepad inversions preserved.
-2. **Real trails = prisms**: VesselPrismController spawns real Prisms (visible
-   blocks, conserved mass); real Skimmer contact grants energy through the trigger
-   pipeline (contact arc landed).
+2. **Real trails = prisms** ✅ (iteration 17): every trail block is a REAL `Prism`
+   spawned by the rig's real `VesselPrismController` async loop — the client's
+   `SkimRacePrismFactory` (SkimRacePrisms.cs) answers the spawn channel with the
+   full V15 prism family (MeshRenderer/BoxCollider/4 managers/Prism/PrismImpactor/
+   ImpactCollider + a `PrismGrowthDriver` per-prism stand-in replicating the
+   unported PrismScaleManager's exact growth math). Conserved mass: nothing decays
+   prisms; the only sink is race-restart `DespawnAll`. Trail-skim energy flows
+   through the REAL skimmer pipeline: near-field `Skimmer` (Scale ElementalFloat
+   bound to **Mass** — claims grow your skim reach) + trigger SphereCollider +
+   `SkimmerImpactor` → engine TriggerPass → `SkimRaceTrailSkimEnergyEffectSO`
+   (the per-vessel effect-asset pattern; drift + Charge bonuses inside). Own fresh
+   trail can't self-charge — the verbatim `waitTillOutsideSkimmer` arming delay is
+   the protection; lapping back re-arms it. Distance-check skim code deleted.
+   `SkimRaceDirector.Shutdown()` winds down all async spawn/AI loops. Engine perf
+   (behavior-preserving, prism-scale scenes): TriggerPass pair scan walks trigger
+   indices (identical pair visitation order), GameLoop.UnregisterBehaviour binary
+   search. Verified headless at frame 1200: claims identical to rung 1
+   ([8,3,8,3], 22 claims — determinism preserved), `trail 786` = real prism count.
 3. **Real crystals/impactors**: claims via OnTriggerEnter → CrystalImpactor family
    (landed for the CLI round; bring to the client).
 4. **Real scoring + HUD semantics**: HexRaceScoringRuleSO domain-aggregated end,
@@ -462,17 +499,18 @@ the next rung.
 6. Onward: cells/fauna ambience, more vessel classes, game modes — always through
    the real systems.
 
-## NEXT UP (iteration 17)
-
-1. **Rung 2**: real prism trails + Skimmer contact in the client. Replace
-   SkimRaceSim's visual `TrailPoint` ribbons with real `VesselPrismController`
-   spawning (StopSpawn before any test return — async-void trap) and grant
-   trail-skim energy through the real Skimmer trigger pipeline (contact arc
-   landed). Trails must render as prism blocks; skim detection must come from
-   `OnTriggerEnter/Exit`, not distance checks.
-2. Then rung 3 (real crystal claims via OnTriggerEnter → CrystalImpactor family
-   in the client; landed for the CLI round already).
-3. Update this file, commit, push.
+## NEXT UP (iteration 18)
+
+1. **Rung 3**: real crystal claims via OnTriggerEnter → CrystalImpactor family in
+   the client (largely landed with rung 1 — station crystals already run
```

</details>

### `7fe0a011a` — feat(port): SpawnableBase + SpawnPoint + SpawnTrailData verbatim — track-content groundwork

_Claude, 2026-06-12 19:53:24 +0000_

```text
The unified pattern-generation/spawning tree (cacheable SpawnTrailData by
parameter hash, nested children containers, leaf instantiation with
automatic Prism trail management). SpawnableWaypointTrack and the rest of
the spawnable family build on it; the CrystalCollisionTurnMonitor waypoint
deviation restores once the waypoint track ports (deferred: its
CrystalPositionSet dependency lives in CrystalManager.cs, which the rung-3
crystal-respawn work may touch).

6 tests: degenerate LookRotation, hash-keyed caching + invalidation,
first-trail point access, leaf spawning at generated points, child-tree
container nesting. Gate green both configs: 862 + 252.
```

```text
 Port/PORT_PLAN.md                                                     |   9 +
 .../CosmicShore.Game/Controller/Environment/Spawning/SpawnPoint.cs    |  60 +++++++
 .../Controller/Environment/Spawning/SpawnTrailData.cs                 |  27 +++
 .../CosmicShore.Game/Controller/Environment/Spawning/SpawnableBase.cs | 308 ++++++++++++++++++++++++++++++++
 Port/tests/CosmicShore.Tests/SpawnableBaseTests.cs                    | 150 ++++++++++++++++
 5 files changed, 554 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 589 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 311cb616f..ac76c1314 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -516,6 +516,15 @@ Note (test config): `CSDebug.Log/LogFormat` are `[Conditional("DEBUG")]` — inf
 logs strip out of Release. DebugExtensionsTests asserts per-config (`#if DEBUG`).
 Gate BOTH configs when touching logging paths.
 
+Track-content groundwork (landed alongside iteration 18): `SpawnableBase` +
+`SpawnPoint` + `SpawnTrailData` ported verbatim (one dropped using:
+UnityEngine.Serialization — FormerlySerializedAs lives in CosmicShore.Engine).
+6 tests (caching by parameter hash, invalidation, leaf spawning, child-tree
+nesting). Next in this lane: `SpawnableWaypointTrack` (539L) — needs
+`CrystalPositionSet` from CrystalManager.cs (352L, unported; deferred until
+rung-3 integration to avoid colliding with the crystal-respawn work) — which
+then restores the CrystalCollisionTurnMonitor waypoint deviation.
+
 Rung-4 groundwork (landed alongside iteration 17, part 2): `TurnMonitor` (base) +
 `CrystalCollisionTurnMonitor` ported (UniTask→GameTask mechanical mappings; one
 deviation: `optionalEnvironment` waypoint-derived target restores when
diff --git a/Port/src/CosmicShore.Game/Controller/Environment/Spawning/SpawnPoint.cs b/Port/src/CosmicShore.Game/Controller/Environment/Spawning/SpawnPoint.cs
new file mode 100644
index 000000000..805f930f2
--- /dev/null
+++ b/Port/src/CosmicShore.Game/Controller/Environment/Spawning/SpawnPoint.cs
@@ -0,0 +1,60 @@
+using CosmicShore.Engine;
+
+namespace CosmicShore.Gameplay
+{
+    /// <summary>
+    /// Immutable spatial data for a single spawned object.
+    /// Cached by SpawnableBase to avoid regeneration when parameters are unchanged.
+    /// </summary>
+    [System.Serializable]
+    public struct SpawnPoint
+    {
+        public Vector3 Position;
+        public Quaternion Rotation;
+        public Vector3 Scale;
+
+        public SpawnPoint(Vector3 position, Quaternion rotation, Vector3 scale)
+        {
+            Position = position;
+            Rotation = rotation;
+            Scale = scale;
+        }
+
+        public SpawnPoint(Vector3 position, Quaternion rotation)
+        {
+            Position = position;
+            Rotation = rotation;
+            Scale = Vector3.one;
+        }
+
+        public SpawnPoint(Vector3 position)
+        {
+            Position = position;
+            Rotation = Quaternion.identity;
+            Scale = Vector3.one;
+        }
+
+        /// <summary>
+        /// Compute a rotation that looks from this point toward a target point.
+        /// Returns Quaternion.identity if the direction is degenerate.
+        /// </summary>
+        public static Quaternion LookRotation(Vector3 from, Vector3 to, Vector3 up)
+        {
+            Vector3 forward = to - from;
+            if (forward.sqrMagnitude < 0.0001f)
+                return Quaternion.identity;
+            return Quaternion.LookRotation(forward.normalized, up);
+        }
+
+        /// <summary>
+        /// Compute a rotation that looks along a forward direction.
+        /// Returns Quaternion.identity if the direction is degenerate.
+        /// </summary>
+        public static Quaternion LookRotation(Vector3 forward, Vector3 up)
+        {
```

</details>

### `e3c061d15` — feat(port): LifeForm family verbatim — ecosystem groundwork, SA1 closed

_Claude, 2026-06-12 20:07:19 +0000_

```text
LifeForm + HealthPrism + Spindle + HealthBlockTracker + SpindleTracker +
ILifeFormEntity + ITeamAssignable ported (verbatim modulo substitutions).
One CT1 deviation: the crystal.ActivateCrystal() call in Die restores when
CrystalManager ports. SA1 deviation closed: LifeFormsKilledScoring
subscribes the real static LifeForm.OnLifeFormDeath again.

Engine gains original-contract API the family needs: Random.onUnitSphere /
insideUnitSphere (Marsaglia rejection), 4-arg Instantiate(original,
position, rotation, parent), Scene.isLoaded (true while the owning
GameLoop lives, false after Dispose so teardown probes skip
unloading-scene work).

6 tests: one-way maturity, lethality at min blocks, dedup add with domain
stamp, embedded health-prism binding via BindEmbeddedParts, death event
with killer + GameObject destruction through DieCoroutine, SetTeam
propagation. Conserved mass: health prisms die only through the active
Damage path — no decay anywhere in the family. Gate green both configs:
868 + 252.
```

```text
 Port/PORT_PLAN.md                                                     |  12 ++
 Port/src/CosmicShore.Data/ITeamAssignable.cs                          |  14 ++
 Port/src/CosmicShore.Engine/Math/Random.cs                            |  27 ++++
 Port/src/CosmicShore.Engine/Object.cs                                 |  14 ++
 Port/src/CosmicShore.Engine/SceneGraph/GameLoop.cs                    |   1 +
 Port/src/CosmicShore.Engine/SceneGraph/Scene.cs                       |   7 +
 .../Controller/Arcade/Scoring/LifeFormsKilledScoring.cs               |   4 +-
 .../Controller/Environment/FloraAndFauna/HealthBlockTracker.cs        | 113 +++++++++++++
 .../Controller/Environment/FloraAndFauna/ILifeFormEntity.cs           |  17 ++
 .../CosmicShore.Game/Controller/Environment/FloraAndFauna/LifeForm.cs | 279 ++++++++++++++++++++++++++++++++
 .../CosmicShore.Game/Controller/Environment/FloraAndFauna/Spindle.cs  | 268 ++++++++++++++++++++++++++++++
 .../Controller/Environment/FloraAndFauna/SpindleTracker.cs            |  58 +++++++
 Port/src/CosmicShore.Game/Controller/Environment/HealthPrism.cs       |  66 ++++++++
 Port/tests/CosmicShore.Tests/LifeFormFamilyTests.cs                   | 197 ++++++++++++++++++++++
 14 files changed, 1075 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1197 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index ac76c1314..5f5d44456 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -516,6 +516,18 @@ Note (test config): `CSDebug.Log/LogFormat` are `[Conditional("DEBUG")]` — inf
 logs strip out of Release. DebugExtensionsTests asserts per-config (`#if DEBUG`).
 Gate BOTH configs when touching logging paths.
 
+Ecosystem groundwork (landed alongside iteration 18): the LifeForm family ported —
+`LifeForm` + `HealthPrism` + `Spindle` + `HealthBlockTracker` + `SpindleTracker` +
+`ILifeFormEntity` + `ITeamAssignable` (verbatim modulo substitutions; one CT1
+deviation: the `crystal.ActivateCrystal()` call in `Die` restores with
+CrystalManager). SA1 deviation CLOSED: `LifeFormsKilledScoring` subscribes the real
+static `LifeForm.OnLifeFormDeath` again. Engine gains original-contract API:
+`Random.onUnitSphere`/`insideUnitSphere`, 4-arg `Object.Instantiate(original, pos,
+rot, parent)`, `Scene.isLoaded` (flips false on GameLoop.Dispose). 6 tests
+(maturity one-way, lethality at min blocks, dedup add, embedded-prism binding,
+death event + destroy, SetTeam propagation). Conserved mass: health prisms die
+only through the active Damage path — no decay anywhere in the family.
+
 Track-content groundwork (landed alongside iteration 18): `SpawnableBase` +
 `SpawnPoint` + `SpawnTrailData` ported verbatim (one dropped using:
 UnityEngine.Serialization — FormerlySerializedAs lives in CosmicShore.Engine).
diff --git a/Port/src/CosmicShore.Data/ITeamAssignable.cs b/Port/src/CosmicShore.Data/ITeamAssignable.cs
new file mode 100644
index 000000000..7b7a54704
--- /dev/null
+++ b/Port/src/CosmicShore.Data/ITeamAssignable.cs
@@ -0,0 +1,14 @@
+﻿using System;
+using System.Collections.Generic;
+using System.Linq;
+using System.Text;
+using System.Threading.Tasks;
+using CosmicShore.Data;
+
+namespace CosmicShore.Data
+{
+    public interface ITeamAssignable
+    {
+        public void SetTeam(Domains domain);
+    }
+}
diff --git a/Port/src/CosmicShore.Engine/Math/Random.cs b/Port/src/CosmicShore.Engine/Math/Random.cs
index 4a7696e4b..57e276c9d 100644
--- a/Port/src/CosmicShore.Engine/Math/Random.cs
+++ b/Port/src/CosmicShore.Engine/Math/Random.cs
@@ -26,5 +26,32 @@ namespace CosmicShore.Engine
 
         /// <summary>Random float in [0, 1].</summary>
         public static float value => (float)_rng.NextDouble();
+
+        /// <summary>Random point on the surface of a unit sphere (uniform — Marsaglia rejection).</summary>
+        public static Vector3 onUnitSphere
+        {
+            get
+            {
+                while (true)
+                {
+                    var p = new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f));
+                    float sqr = p.sqrMagnitude;
+                    if (sqr > 1e-6f && sqr <= 1f) return p / Mathf.Sqrt(sqr);
+                }
+            }
+        }
+
+        /// <summary>Random point inside (or on) a unit sphere.</summary>
+        public static Vector3 insideUnitSphere
+        {
+            get
+            {
+                while (true)
+                {
+                    var p = new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f));
+                    if (p.sqrMagnitude <= 1f) return p;
+                }
+            }
+        }
     }
 }
```

</details>

### `19b9491d9` — feat(port): Flora + Fauna verbatim — V12 fauna deviations in Cell closed

_Claude, 2026-06-12 20:17:59 +0000_

```text
Flora (growth/plant cycles on LifeForm), Fauna (goal-seeking, diet,
starvation clock, predation immunity, lineage reproduction via
FaunaReproductionRules), FaunaConfigurationSO, FaunaReproductionRules —
all verbatim modulo using substitutions.

Cell's live-fauna registry now uses the real types: per-species lineage
counts keyed by FaunaConfigurationSO, GetLiveHerbivoreCount filters by
Diet == Herbivore && IsAlivePrey again, Register/UnregisterLiveFauna take
Fauna. CellTests registry test rewritten against real lineage semantics:
AssignLineage registers + counts, lineage-less fauna are invisible,
OnDestroy unregisters and decrements the species count.

Gate green both configs: 868 + 252.
```

```text
 Port/PORT_PLAN.md                                                     |  10 +
 Port/src/CosmicShore.Game/Controller/Environment/Cell.cs              |  40 ++--
 .../CosmicShore.Game/Controller/Environment/FloraAndFauna/Fauna.cs    | 319 ++++++++++++++++++++++++++++++++
 .../CosmicShore.Game/Controller/Environment/FloraAndFauna/Flora.cs    |  81 ++++++++
 .../CosmicShore.Game/Utility/DataContainers/FaunaConfigurationSO.cs   |  35 ++++
 .../CosmicShore.Game/Utility/DataContainers/FaunaReproductionRules.cs |  51 +++++
 Port/tests/CosmicShore.Tests/CellTests.cs                             |  30 +--
 7 files changed, 527 insertions(+), 39 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 659 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 5f5d44456..b14a391e0 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -516,6 +516,16 @@ Note (test config): `CSDebug.Log/LogFormat` are `[Conditional("DEBUG")]` — inf
 logs strip out of Release. DebugExtensionsTests asserts per-config (`#if DEBUG`).
 Gate BOTH configs when touching logging paths.
 
+Ecosystem groundwork part 2 (landed alongside iteration 18): `Flora` + `Fauna` +
+`FaunaConfigurationSO` + `FaunaReproductionRules` ported verbatim. V12 fauna
+deviations in Cell CLOSED: liveFauna registry, per-species lineage counts
+(`GetLiveFaunaCount(FaunaConfigurationSO)`), and `GetLiveHerbivoreCount` now use
+the real types (diet + alive-prey filtering live again). CellTests registry test
+rewritten against real lineage semantics (AssignLineage → register; OnDestroy →
+unregister + species decrement). Still open in this lane: concrete fauna
+(Boid/BoidManager, LightFauna) and concrete flora (BranchingFlora, AssembledFlora)
+for rung 6 ambience.
+
 Ecosystem groundwork (landed alongside iteration 18): the LifeForm family ported —
 `LifeForm` + `HealthPrism` + `Spindle` + `HealthBlockTracker` + `SpindleTracker` +
 `ILifeFormEntity` + `ITeamAssignable` (verbatim modulo substitutions; one CT1
diff --git a/Port/src/CosmicShore.Game/Controller/Environment/Cell.cs b/Port/src/CosmicShore.Game/Controller/Environment/Cell.cs
index 8e63e0fac..22f75dfb3 100644
--- a/Port/src/CosmicShore.Game/Controller/Environment/Cell.cs
+++ b/Port/src/CosmicShore.Game/Controller/Environment/Cell.cs
@@ -553,22 +553,15 @@ namespace CosmicShore.Gameplay
         //  See Docs/ECOSYSTEM.md §6/§7.
         // ---------------------------------------------------------------------
 
-        // PORT Deviation (V12, restore when FaunaConfigurationSO ports): readonly Dictionary<FaunaConfigurationSO, int> liveFaunaCounts = new();
-        // FaunaConfigurationSO : ScriptableObject and it is only a registry key here, so the
-        // base type stands in until the flora/fauna cluster ports.
-        readonly Dictionary<ScriptableObject, int> liveFaunaCounts = new();
-        // PORT Deviation (V12, restore when Fauna ports): readonly List<Fauna> liveFauna = new();
-        // Fauna : MonoBehaviour, so the base type stands in until the flora/fauna cluster ports.
-        readonly List<MonoBehaviour> liveFauna = new();
+        readonly Dictionary<FaunaConfigurationSO, int> liveFaunaCounts = new();
+        readonly List<Fauna> liveFauna = new();
 
         /// <summary>Live population of the species defined by <paramref name="config"/> in this cell.</summary>
-        // PORT Deviation (V12, restore when FaunaConfigurationSO ports): public int GetLiveFaunaCount(FaunaConfigurationSO config) =>
-        public int GetLiveFaunaCount(ScriptableObject config) =>
+        public int GetLiveFaunaCount(FaunaConfigurationSO config) =>
             config && liveFaunaCounts.TryGetValue(config, out int c) ? c : 0;
 
         /// <summary>All lineage-registered live fauna in this cell (any species, any diet).</summary>
-        // PORT Deviation (V12, restore when Fauna ports): public IReadOnlyList<Fauna> LiveFauna => liveFauna;
-        public IReadOnlyList<MonoBehaviour> LiveFauna => liveFauna;
+        public IReadOnlyList<Fauna> LiveFauna => liveFauna;
 
         /// <summary>
         /// Live herbivores still eligible as prey — the prey signal for predator
@@ -580,33 +573,26 @@ namespace CosmicShore.Gameplay
             for (int i = 0; i < liveFauna.Count; i++)
             {
                 var f = liveFauna[i];
-                // PORT Deviation (V12, restore when Fauna ports): if (f && f.Diet == FaunaDiet.Herbivore && f.IsAlivePrey) n++;
-                // (Diet/IsAlivePrey live on the unported Fauna type — every live entry counts
-                // until it lands.)
-                if (f) n++;
+                if (f && f.Diet == FaunaDiet.Herbivore && f.IsAlivePrey) n++;
             }
             return n;
         }
 
-        // PORT Deviation (V12, restore when Fauna ports): public void RegisterLiveFauna(Fauna fauna)
-        public void RegisterLiveFauna(MonoBehaviour fauna)
+        public void RegisterLiveFauna(Fauna fauna)
         {
-            // PORT Deviation (V12, restore when Fauna + FaunaConfigurationSO port): if (!fauna || !fauna.SourceConfig) return;
-            // PORT Deviation (V12, restore when Fauna + FaunaConfigurationSO port): liveFaunaCounts.TryGetValue(fauna.SourceConfig, out int c);
-            // PORT Deviation (V12, restore when Fauna + FaunaConfigurationSO port): liveFaunaCounts[fauna.SourceConfig] = c + 1;
-            if (!fauna) return;
+            if (!fauna || !fauna.SourceConfig) return;
+            liveFaunaCounts.TryGetValue(fauna.SourceConfig, out int c);
+            liveFaunaCounts[fauna.SourceConfig] = c + 1;
             liveFauna.Add(fauna);
         }
 
-        // PORT Deviation (V12, restore when Fauna ports): public void UnregisterLiveFauna(Fauna fauna)
```

</details>

### `8d99b2418` — feat(port): Physics.OverlapSphereNonAlloc on the TriggerPass registry

_Claude, 2026-06-12 20:27:19 +0000_

```text
Original-contract sphere query over every registered collider (trigger and
non-trigger), deterministic registration-order results truncated at buffer
capacity. This is the scan Boid cohesion/separation/prism-interaction
behavior runs on — prerequisite for the concrete-fauna arc, which is
otherwise blocked on BoidController + the Assemblers family (~2,087L,
recorded in PORT_PLAN as the next self-contained arc).

4 tests: mixed shape/trigger hits, capacity truncation in registration
order, disabled-collider skip + radius miss, box edge-contact boundary.
Gate green both configs: 872 + 252.
```

```text
 Port/PORT_PLAN.md                                     |   8 ++++
 Port/src/CosmicShore.Engine/Compat/EngineCompat.cs    |   8 ++++
 Port/src/CosmicShore.Engine/SceneGraph/TriggerPass.cs |  46 +++++++++++++++++++++
 Port/tests/CosmicShore.Tests/PhysicsOverlapTests.cs   | 103 ++++++++++++++++++++++++++++++++++++++++++++++++
 4 files changed, 165 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 204 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index b14a391e0..3535fe961 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -516,6 +516,14 @@ Note (test config): `CSDebug.Log/LogFormat` are `[Conditional("DEBUG")]` — inf
 logs strip out of Release. DebugExtensionsTests asserts per-config (`#if DEBUG`).
 Gate BOTH configs when touching logging paths.
 
+Ecosystem groundwork part 3 (landed alongside iteration 18):
+`Physics.OverlapSphereNonAlloc` implemented against the TriggerPass collider
+registry (trigger + non-trigger, deterministic registration order, capacity
+truncation) — the spatial query Boid behavior scans with. 4 tests. Concrete fauna
+finding: `Boid` is blocked on `BoidController` (extends BoidManager) and the
+Assemblers family (`GyroidAssembler` + bond-mate data, ~2,087L) — that's the next
+self-contained arc for rung-6 ambience, not an idle-window port.
+
 Ecosystem groundwork part 2 (landed alongside iteration 18): `Flora` + `Fauna` +
 `FaunaConfigurationSO` + `FaunaReproductionRules` ported verbatim. V12 fauna
 deviations in Cell CLOSED: liveFauna registry, per-species lineage counts
diff --git a/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs b/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs
index 989db4994..e53833528 100644
--- a/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs
+++ b/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs
@@ -167,6 +167,14 @@ namespace CosmicShore.Engine
         public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance = float.PositiveInfinity)
         { hitInfo = default; return false; }
         public static bool Raycast(Vector3 origin, Vector3 direction, float maxDistance = float.PositiveInfinity) => false;
+
+        /// <summary>
+        /// Original-contract sphere query against every registered collider (trigger and
+        /// non-trigger), backed by the loop's collider registry. Deterministic
+        /// registration-order results, truncated at the buffer length.
+        /// </summary>
+        public static int OverlapSphereNonAlloc(Vector3 position, float radius, Collider[] results)
+            => GameLoop.Current?.Triggers.OverlapSphereNonAlloc(position, radius, results) ?? 0;
     }
 
     public class Collider : Behaviour
diff --git a/Port/src/CosmicShore.Engine/SceneGraph/TriggerPass.cs b/Port/src/CosmicShore.Engine/SceneGraph/TriggerPass.cs
index e4a2c9cba..581b04107 100644
--- a/Port/src/CosmicShore.Engine/SceneGraph/TriggerPass.cs
+++ b/Port/src/CosmicShore.Engine/SceneGraph/TriggerPass.cs
@@ -172,6 +172,52 @@ namespace CosmicShore.Engine
             }
         }
 
+        // ── Spatial queries ──────────────────────────────────────────
+
+        /// <summary>
+        /// Sphere query over the registered colliders — backs
+        /// <see cref="Physics.OverlapSphereNonAlloc"/>. Results fill in registration
+        /// order (deterministic), truncated at the buffer's capacity, exactly like the
+        /// original engine's non-alloc contract. Includes trigger AND non-trigger
+        /// colliders; inactive/disabled colliders are skipped.
+        /// </summary>
+        internal int OverlapSphereNonAlloc(Vector3 position, float radius, Collider[] results)
+        {
+            int count = 0;
+            foreach (var collider in _colliders)
+            {
+                if (count >= results.Length) break;
+                if (!collider.isActiveAndEnabled) continue;
+                if (!SphereOverlapsCollider(position, radius, collider)) continue;
+                results[count++] = collider;
+            }
+            return count;
+        }
+
+        static bool SphereOverlapsCollider(Vector3 center, float radius, Collider collider)
+        {
+            switch (collider)
+            {
+                case SphereCollider s:
+                {
+                    Vector3 otherCenter = s.transform.TransformPoint(s.center);
+                    float radii = radius + WorldRadius(s);
+                    return (center - otherCenter).sqrMagnitude <= radii * radii;
+                }
+                case BoxCollider box:
+                {
```

</details>

### `c8e9d6068` — feat(port): shape spawnable family + engine Bounds/Rigidbody — content groundwork

_Claude, 2026-06-12 20:46:05 +0000_

```text
SpawnableShapeBase (intensity-scaled block counts, gradual spawn coroutine,
auto trigger attachment with bounding-radius calculation) + 8 shape
spawnables (Circle, Ellipsoid, Helix, Cylinder, Diamond, Arrow, Heart,
FiveRings) + ShapeDefinition + ShapeCollisionTrigger + ShapeSign(+Events) —
verbatim; ShapeSign's two TMP_Text labels are UI-shell deviations until a
TMPro shim lands (trigger flow + static event bus verbatim).

Engine: Bounds struct (center/extents, min/max, Encapsulate, Contains,
Intersects), Renderer.bounds under the documented unit-cube convention,
and a kinematic Rigidbody placeholder — trigger physics needs no
rigidbodies, the type exists so [RequireComponent(typeof(Rigidbody))]
setup code ports verbatim.

5 tests: closed-loop circle generation on the scaled radius, intensity
scaling (30→50 points), instant-spawn trigger rig (Rigidbody + trigger
sphere + 12 prisms in trail), Bounds contract, Renderer.bounds pose/scale.
Gate green both configs: 877 + 252.
```

```text
 Port/PORT_PLAN.md                                                     |  11 +
 Port/src/CosmicShore.Engine/Compat/EngineCompat.cs                    |  14 ++
 Port/src/CosmicShore.Engine/Math/Bounds.cs                            |  81 ++++++
 Port/src/CosmicShore.Engine/Rendering/Renderers.cs                    |  16 ++
 .../Controller/Environment/MiniGameObjects/ShapeCollisionTrigger.cs   |  46 ++++
 .../Controller/Environment/MiniGameObjects/ShapeDefinition.cs         | 430 ++++++++++++++++++++++++++++++++
 .../Controller/Environment/MiniGameObjects/ShapeSign.cs               |  83 ++++++
 .../Controller/Environment/MiniGameObjects/SpawnableArrow.cs          |  95 +++++++
 .../Controller/Environment/MiniGameObjects/SpawnableCircle.cs         |  56 +++++
 .../Controller/Environment/MiniGameObjects/SpawnableCylinder.cs       |  73 ++++++
 .../Controller/Environment/MiniGameObjects/SpawnableDiamond.cs        |  86 +++++++
 .../Controller/Environment/MiniGameObjects/SpawnableEllipsoid.cs      |  95 +++++++
 .../Controller/Environment/MiniGameObjects/SpawnableFiveRings.cs      | 113 +++++++++
 .../Controller/Environment/MiniGameObjects/SpawnableHeart.cs          |  44 ++++
 .../Controller/Environment/MiniGameObjects/SpawnableHelix.cs          |  61 +++++
 .../Controller/Environment/MiniGameObjects/SpawnableShapeBase.cs      | 173 +++++++++++++
 Port/tests/CosmicShore.Tests/SpawnableShapeTests.cs                   | 140 +++++++++++
 17 files changed, 1617 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 1734 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 3535fe961..c1eb9a66d 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -516,6 +516,17 @@ Note (test config): `CSDebug.Log/LogFormat` are `[Conditional("DEBUG")]` — inf
 logs strip out of Release. DebugExtensionsTests asserts per-config (`#if DEBUG`).
 Gate BOTH configs when touching logging paths.
 
+Shape-content groundwork (landed alongside iteration 18): `SpawnableShapeBase` +
+8 shape spawnables (Circle, Ellipsoid, Helix, Cylinder, Diamond, Arrow, Heart,
+FiveRings) + `ShapeDefinition` + `ShapeCollisionTrigger` + `ShapeSign`(+Events)
+ported (verbatim; ShapeSign's two TMP_Text labels are UI-shell deviations until a
+TMPro shim lands — the trigger flow + static event bus are verbatim). Engine gains
+`Bounds` (full original contract), `Renderer.bounds` (unit-cube convention,
+documented), and a kinematic `Rigidbody` placeholder (trigger physics needs no
+rigidbodies; satisfies [RequireComponent] so authored setup ports verbatim).
+5 tests. These shapes are the Phase-2 shape-drawing content (lava-lamp
+freestyle) and general track decoration for the client.
+
 Ecosystem groundwork part 3 (landed alongside iteration 18):
 `Physics.OverlapSphereNonAlloc` implemented against the TriggerPass collider
 registry (trigger + non-trigger, deterministic registration order, capacity
diff --git a/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs b/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs
index e53833528..e7fda1506 100644
--- a/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs
+++ b/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs
@@ -177,6 +177,20 @@ namespace CosmicShore.Engine
             => GameLoop.Current?.Triggers.OverlapSphereNonAlloc(position, radius, results) ?? 0;
     }
 
+    /// <summary>
+    /// Kinematic placeholder. The port's trigger physics needs no rigidbodies (see
+    /// TriggerPass); fields hold authored values so [RequireComponent(typeof(Rigidbody))]
+    /// components and their setup code port verbatim. Dynamics arrive with the full
+    /// physics phase.
+    /// </summary>
+    public class Rigidbody : Component
+    {
+        public bool isKinematic = true;
+        public bool useGravity;
+        public float mass = 1f;
+        public Vector3 velocity;
+    }
+
     public class Collider : Behaviour
     {
         public bool isTrigger;
diff --git a/Port/src/CosmicShore.Engine/Math/Bounds.cs b/Port/src/CosmicShore.Engine/Math/Bounds.cs
new file mode 100644
index 000000000..30dd02c03
--- /dev/null
+++ b/Port/src/CosmicShore.Engine/Math/Bounds.cs
@@ -0,0 +1,81 @@
+using System;
+
+namespace CosmicShore.Engine
+{
+    /// <summary>Axis-aligned bounding box (original engine contract: center + extents).</summary>
+    [Serializable]
+    public struct Bounds : IEquatable<Bounds>
+    {
+        public Vector3 center;
+        Vector3 _extents;
+
+        public Bounds(Vector3 center, Vector3 size)
+        {
+            this.center = center;
+            _extents = size * 0.5f;
+        }
+
+        public Vector3 extents
+        {
+            get => _extents;
+            set => _extents = value;
+        }
+
+        public Vector3 size
+        {
+            get => _extents * 2f;
+            set => _extents = value * 0.5f;
```

</details>

### `b48463fd1` — feat(port): 25 spawnable shapes verbatim — content batch

_Claude, 2026-06-12 20:53:47 +0000_

```text
The remaining dependency-free spawnable family: BaseballCurve, Batman,
CardioidSmear, CliffordTorus, Comet, DartBoard, DriftCourse, Helicoid,
HopfFibration, Infinity, Lightning, LinkedRings, Pumpkin, RaceTrack,
SchwarzPSurface, SingleTrailBlock, Smiley, Spherene, Spiral, Star,
TorusKnot, Tube, Wave, Zigzag, ShapeSign — all verbatim modulo the using
substitution (covered by SpawnableBase/ShapeBase tests landed earlier).

Still blocked on other lanes: SpawnableCrystal/Flora/WaypointTrack
(CrystalManager), SpawnableGyroid/Wall (Assembler family), SpawnableLSystem.

Gate green both configs: 877 + 252.
```

```text
 Port/PORT_PLAN.md                                                     |   9 +
 .../Controller/Environment/MiniGameObjects/SpawnableBaseballCurve.cs  |  54 ++++
 .../Controller/Environment/MiniGameObjects/SpawnableBatman.cs         | 122 +++++++++
 .../Controller/Environment/MiniGameObjects/SpawnableCardioidSmear.cs  |  52 ++++
 .../Controller/Environment/MiniGameObjects/SpawnableCliffordTorus.cs  | 141 ++++++++++
 .../Controller/Environment/MiniGameObjects/SpawnableComet.cs          |  92 +++++++
 .../Controller/Environment/MiniGameObjects/SpawnableDartBoard.cs      | 100 +++++++
 .../Controller/Environment/MiniGameObjects/SpawnableDriftCourse.cs    |  61 +++++
 .../Controller/Environment/MiniGameObjects/SpawnableHelicoid.cs       | 128 +++++++++
 .../Controller/Environment/MiniGameObjects/SpawnableHopfFibration.cs  | 342 +++++++++++++++++++++++
 .../Controller/Environment/MiniGameObjects/SpawnableInfinity.cs       |  60 +++++
 .../Controller/Environment/MiniGameObjects/SpawnableLightning.cs      |  89 ++++++
 .../Controller/Environment/MiniGameObjects/SpawnableLinkedRings.cs    | 116 ++++++++
 .../Controller/Environment/MiniGameObjects/SpawnablePumpkin.cs        |  74 +++++
 .../Controller/Environment/MiniGameObjects/SpawnableRaceTrack.cs      | 463 ++++++++++++++++++++++++++++++++
 .../Environment/MiniGameObjects/SpawnableSchwarzPSurface.cs           | 168 ++++++++++++
 .../Controller/Environment/MiniGameObjects/SpawnableShapeSign.cs      |  38 +++
 .../Environment/MiniGameObjects/SpawnableSingleTrailBlock.cs          |  27 ++
 .../Controller/Environment/MiniGameObjects/SpawnableSmiley.cs         |  88 ++++++
 .../Controller/Environment/MiniGameObjects/SpawnableSpherene.cs       | 192 +++++++++++++
 .../Controller/Environment/MiniGameObjects/SpawnableSpiral.cs         |  50 ++++
 .../Controller/Environment/MiniGameObjects/SpawnableStar.cs           |  65 +++++
 .../Controller/Environment/MiniGameObjects/SpawnableTorusKnot.cs      | 112 ++++++++
 .../Controller/Environment/MiniGameObjects/SpawnableTube.cs           |  54 ++++
 .../Controller/Environment/MiniGameObjects/SpawnableWave.cs           |  54 ++++
 .../Controller/Environment/MiniGameObjects/SpawnableZigzag.cs         |  53 ++++
 26 files changed, 2804 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 2966 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index c1eb9a66d..563ac2133 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -516,6 +516,15 @@ Note (test config): `CSDebug.Log/LogFormat` are `[Conditional("DEBUG")]` — inf
 logs strip out of Release. DebugExtensionsTests asserts per-config (`#if DEBUG`).
 Gate BOTH configs when touching logging paths.
 
+Shape-content groundwork part 2 (landed alongside iteration 18): 25 more
+spawnables ported verbatim (BaseballCurve, Batman, CardioidSmear, CliffordTorus,
+Comet, DartBoard, DriftCourse, Helicoid, HopfFibration, Infinity, Lightning,
+LinkedRings, Pumpkin, RaceTrack, SchwarzPSurface, SingleTrailBlock, Smiley,
+Spherene, Spiral, Star, TorusKnot, Tube, Wave, Zigzag, ShapeSign). Still blocked:
+SpawnableCrystal/SpawnableFlora/SpawnableWaypointTrack (CrystalManager — rung-3
+lane), SpawnableGyroid/SpawnableWall (Assembler family — assembler-agent lane),
+SpawnableLSystem (check deps when lanes clear).
+
 Shape-content groundwork (landed alongside iteration 18): `SpawnableShapeBase` +
 8 shape spawnables (Circle, Ellipsoid, Helix, Cylinder, Diamond, Arrow, Heart,
 FiveRings) + `ShapeDefinition` + `ShapeCollisionTrigger` + `ShapeSign`(+Events)
diff --git a/Port/src/CosmicShore.Game/Controller/Environment/MiniGameObjects/SpawnableBaseballCurve.cs b/Port/src/CosmicShore.Game/Controller/Environment/MiniGameObjects/SpawnableBaseballCurve.cs
new file mode 100644
index 000000000..b72223d02
--- /dev/null
+++ b/Port/src/CosmicShore.Game/Controller/Environment/MiniGameObjects/SpawnableBaseballCurve.cs
@@ -0,0 +1,54 @@
+using CosmicShore.Gameplay;
+using CosmicShore.Engine;
+
+namespace CosmicShore.Gameplay
+{
+    public class SpawnableBaseballCurve : SpawnableBase
+    {
+        [FormerlySerializedAs("trailBlock")] [SerializeField] Prism prism;
+
+        public float radius = 1.0f;
+        public int numSegments = 16;
+        public float seamWidth = 0.2f;
+
+        public float b = 0.5f;
+        public float c = 0.75f;
+
+        protected override SpawnTrailData[] GenerateTrailData()
+        {
+            var seam1Points = new SpawnPoint[numSegments];
+            var seam2Points = new SpawnPoint[numSegments];
+
+            for (int i = 0; i < numSegments; i++)
+            {
+                float t = i / (float)numSegments * 2.0f * Mathf.PI;
+                float x = radius * Mathf.Cos(Mathf.PI / 2.0f - c) * Mathf.Cos(t) * Mathf.Cos(t / 2.0f + c * Mathf.Sin(2.0f * t));
+                float y = radius * Mathf.Cos(Mathf.PI / 2.0f - c) * Mathf.Cos(t) * Mathf.Sin(t / 2.0f + c * Mathf.Sin(2.0f * t));
+                float z = radius * Mathf.Sin(Mathf.PI / 2.0f - c) * Mathf.Cos(t);
+
+                var position1 = new Vector3(x, y, z);
+                var position2 = new Vector3(x, y, z + seamWidth);
+
+                seam1Points[i] = new SpawnPoint(position1, Quaternion.identity, Vector3.one);
+                seam2Points[i] = new SpawnPoint(position2, Quaternion.identity, Vector3.one);
+            }
+
+            return new[]
+            {
+                new SpawnTrailData(seam1Points, false, domain),
+                new SpawnTrailData(seam2Points, false, domain)
+            };
+        }
+
+        protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
+        {
+            foreach (var td in trailData)
+                SpawnPrismTrail(td.Points, container, prism, td.IsLoop, td.Domain);
+        }
+
+        protected override int GetParameterHash()
+        {
+            return System.HashCode.Combine(seed, radius, numSegments, seamWidth, b, c);
+        }
+    }
+}
```

</details>

### `305ce2a3b` — feat(port): convergence rung 3 — real crystal claims, real respawn, exact elemental attribution

_Claude, 2026-06-12 21:00:08 +0000_

```text
Crystal layer is now the real game's. Stations spawn per-kind impactor
rigs: OmniCrystalImpactor (vessel contact), TeamCrystalImpactor
(domain-locked via the real Crystal.ChangeDomain), ElementalCrystalImpactor
(skimmer collection — crystal flies to its claimer, verbatim path).
Element levels move ONLY through real impactor effect SOs
(SkimmerAdjustElementLevelByCrystalEffectSO +
VesselIncrementLevelByCrystalEffectSO, both verbatim, wired exactly where
the original's prefabs wire them); all director-side stat pokes deleted.
AI courses filter with the real Crystal.CanBeCollected.

Real respawn path: CrystalManager (abstract) + LocalCrystalManager ported
verbatim, closing the staged CT1 deviations (Crystal.Respawn →
RespawnCrystal, NotifyManagerToExplodeCrystal → ExplodeCrystal,
OmniCrystalImpactor.IsNetworkClient). SkimRaceCrystalManager joins the
real family; director-staged respawn deleted; course re-sorting is
event-driven off OnCellItemsUpdated.

Root-cause engine fix exposed by the exact-grant test: the clone rule
shared Dictionary fields between template and clones, so every pilot
shared ONE ResourceSystem.ElementalLevels — any rival's claim leaked
levels onto the human. Dictionary<K,V> fields now get fresh containers
per clone with remapped contents (+ freeze test).

11 new tests (7 CrystalManagerTests, 3 rung-3 convergence, 1 clone
freeze). Gate green both configs: 888 + 252. Headless 1200 frames:
exact attribution (claims 16, levels C6/M0/S1/T0), prism determinism
identical to rung 2 (trail 786), exit 0.
```

```text
 Port/PORT_PLAN.md                                                     |  87 ++++++--
 Port/src/CosmicShore.Cli/HexRaceRound.cs                              |  36 +++-
 Port/src/CosmicShore.Client/RaceWindow.cs                             |   9 +-
 Port/src/CosmicShore.Client/SkimRaceCrystals.cs                       | 367 ++++++++++++++++++++++++++++++++
 Port/src/CosmicShore.Client/SkimRaceSim.cs                            | 233 +++++++++-----------
 Port/src/CosmicShore.Engine/Compat/EngineCompat.cs                    |  35 ++-
 Port/src/CosmicShore.Game/Controller/Environment/FlowField/Crystal.cs | 104 +++++++--
 .../Controller/Environment/FlowField/CrystalManager.cs                | 352 ++++++++++++++++++++++++++++++
 .../Controller/Environment/FlowField/LocalCrystalManager.cs           |  72 +++++++
 .../SkimmerAdjustElementLevelByCrystalEffectSO.cs                     |  65 ++++++
 .../Vessel Crystal Effects/VesselIncrementLevelByCrystalEffectSO.cs   |  30 +++
 .../Controller/ImpactEffects/Impactors/OmniCrystalImpactor.cs         |   6 +-
 Port/tests/CosmicShore.Tests/ClientConvergenceTests.cs                | 228 +++++++++++++++++++-
 Port/tests/CosmicShore.Tests/CloneDeepCopyTests.cs                    |  40 ++++
 Port/tests/CosmicShore.Tests/ContactArcTests.cs                       |  47 +++-
 Port/tests/CosmicShore.Tests/CrystalManagerTests.cs                   | 239 +++++++++++++++++++++
 16 files changed, 1775 insertions(+), 175 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 2299 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 563ac2133..68ce56d55 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -451,6 +451,41 @@ Next: V8 (VesselTransformer + member restore), rival balance from prompter feedb
   headless 300/1200-frame runs exit 0 with rung-1-identical claim determinism and
   real-prism trail counts.
 
+- **Iteration 18** (2026-06-12): **Convergence rung 3 — the real CrystalImpactor
+  family + the real crystal respawn chain in the playable client.** Ported verbatim:
+  `CrystalManager` (abstract base: anchor placement, stable ids, batch spawn,
+  per-crystal anchor progression) + `LocalCrystalManager` +
+  `SkimmerAdjustElementLevelByCrystalEffectSO` + `VesselIncrementLevelByCrystalEffectSO`.
+  Crystal shell grew the manager surface verbatim (CrystalManager/InjectDependencies,
+  CanBeCollected, SphereRadius, MoveToNewPos, ChangeDomain + DecayingTheftCoroutine,
+  Explode + WaitForImpact — render-side bodies stripped with markers) and the staged
+  CT1 deviations CLOSED: `Crystal.Respawn()` → `CrystalManager.RespawnCrystal`,
+  `NotifyManagerToExplodeCrystal` → `ExplodeCrystal`, `OmniCrystalImpactor.IsNetworkClient`
+  verbatim. Client: new `SkimRaceCrystals.cs` — `SkimRaceCrystalManager` (game-mode
+  manager in the real family; per-kind station rigs: Omni / Team (domain-locked, slot
+  3 of every 7) / Elemental (skimmer-claimed, consumed via the real fly-to-vessel
+  collection); claimed vessel-claim crystals go dark IN PLACE and relight after 12s
+  through the real Respawn chain; consumed elemental stations respawn fresh) + the
+  race rules as effect assets (`SkimRaceOmniCrystalSurgeEffectSO`,
+  `SkimRaceCrystalEnergyEffectSO`, `SkimRaceElementalClaimEffectSO`). Director: all
+  level/energy grants deleted (effects own them); claim bookkeeping via the manager's
+  claim reports; courses re-sort event-driven off CourseData.OnCellItemsUpdated with
+  the real CanBeCollected filter. CLI HexRaceRound wires a manager (its crystals now
+  require one — the real chain). Engine: **clone rule extended — `Dictionary<K,V>`
+  fields get fresh containers per clone** (CopyFields was overwriting the clone's
+  field-initializer dict with the template's reference: every pilot shared ONE
+  ResourceSystem.ElementalLevels, so any rival's claim leaked levels onto the human —
+  caught by the rung-3 exact-grant test); `Random.insideUnitSphere`/`onUnitSphere`;
+  `Instantiate(original, pos, rot, parent)`. RaceWindow draws live crystal transforms
+  (claimed elemental crystals visibly fly to their claimer; dark stations hide).
+  New tests: CrystalManagerTests (7 — LocalCrystalManager batch/relocate/turn-end/
+  explode + shell chain + CanBeCollected), ClientConvergenceTests rung-3 trio
+  (exact-element grant through the pipeline; omni dark/relight respawn semantics +
+  re-claim; team domain locks in courses AND claims), clone dictionary freeze test.
+  **1119 tests green in BOTH configs (867 + 252)**; headless 300/1200-frame runs
+  exit 0 — frame 1200: `crystals [7,2,5,2], claims 16, levels C6/M0/S1/T0, trail 786`
+  (per-pilot level attribution now exact; prism determinism preserved vs rung 2).
+
 ## PRIME AXIS — CLIENT CONVERGENCE (prompter reorientation, 2026-06-12)
 
 > "our goal is to convert everything over so a player cannot tell the difference
@@ -491,25 +526,51 @@ the next rung.
    indices (identical pair visitation order), GameLoop.UnregisterBehaviour binary
    search. Verified headless at frame 1200: claims identical to rung 1
    ([8,3,8,3], 22 claims — determinism preserved), `trail 786` = real prism count.
-3. **Real crystals/impactors**: claims via OnTriggerEnter → CrystalImpactor family
-   (landed for the CLI round; bring to the client).
+3. **Real crystals/impactors** ✅ (iteration 18): the whole CrystalImpactor family
+   runs the client course — Omni stations claim through OmniCrystalImpactor
+   (vessel contact, any domain), every 7th-slot-3 station is a TEAM crystal
+   (TeamCrystalImpactor, domain-locked via the real Crystal.ChangeDomain +
+   CanBeCollected; AI courses filter on it so no pilot orbits a station it can't
+   take), and elemental stations claim through ElementalCrystalImpactor (skimmer
+   collection — the crystal flies to its claimer and is consumed). Element levels
+   move ONLY through impactor-side effect SOs: the real
+   SkimmerAdjustElementLevelByCrystalEffectSO (crystal-side, exactly where the
+   original's flora/fauna prefabs wire it) + VesselIncrementLevelByCrystalEffectSO
+   (team claims), with the race rules (omni all-four surge, claim energy kickers,
+   claim reporting) as SkimRace* effect assets in the same dispatch chains —
+   the director's level/energy pokes are deleted. Crystal lifetime runs the REAL
+   Crystal.Respawn()/NotifyManagerToExplodeCrystal → CrystalManager chain:
+   CrystalManager + LocalCrystalManager ported verbatim (closing the staged CT1
+   deviations in Crystal + OmniCrystalImpactor), and the race's
+   SkimRaceCrystalManager (a game-mode manager in the real family, like
+   Local/NetworkCrystalManager) owns station placement: vessel-claim crystals
+   survive their claim and relight in place after the 12s window; consumed
+   elemental crystals respawn fresh. Engine: clone rule extended — Dictionary
+   fields get fresh containers per clone (a template-shared runtime dict was
+   bleeding one pilot's ElementalLevels into the whole field); Random gained
+   insideUnitSphere/onUnitSphere; Instantiate gained the (pos, rot, parent)
+   overload. Renderer draws live crystal transforms (claimed elemental crystals
+   visibly fly to their claimer).
 4. **Real scoring + HUD semantics**: HexRaceScoringRuleSO domain-aggregated end,
    golf standings; domains share totals.
 5. **Real look**: SO_ColorSet domain palettes + SO_MaterialSet-driven visuals.
```

</details>

### `8e99e640d` — feat(port): Assemblers family + Boid fauna chain verbatim — rung-6 ambience arc

_Claude, 2026-06-12 21:03:58 +0000_

```text
Assembler (base) + GyroidAssembler + GyroidBondMate/Data/DataContainer
(48-entry baked bond table) + CornerSiteType + WallAssembler +
SchwarzPAssembler, plus AssembledFlora (carries GrowthInfo; one CT2
deviation: crystal.GrowCrystal restores with the crystal-growth arc),
Boid, BoidManager, BoidController — all verbatim modulo substitutions
(two serializer deviations: collisionEffects/Boids inline init, the
original engine's deserializer auto-creates serialized lists).

Engine original-contract additions: Physics.OverlapSphere (allocating,
optional layer mask) + Physics.CheckBox (AABB semantics) on the
TriggerPass registry, LayerMask defaults extended with the project's
TagManager layer table (Mound=17, TrailBlocks=11, ...), and
GetComponentsInChildren<T> returns T[] (original array contract).

23 new tests: bond-table completeness + Substrate invariant, gyroid
growth-site resolution through the real CheckBox probe, dangerous-block
flagging, wall Right/Left alternation, Schwarz P Newton-projected surface
stepping, boid goal steering, Attach grazing + Explode combat through the
real spatial query (opposing mass only — conserved-mass clean), manager
ring spawn with cell lineage. Gate green both configs: 907 + 252.
Headless client diag identical to rung 3 (engine changes are additive).
```

```text
 Port/PORT_PLAN.md                                                     |  26 +-
 Port/src/CosmicShore.Engine/Compat/EngineCompat.cs                    |  27 ++
 Port/src/CosmicShore.Engine/LayerMask.cs                              |  21 +-
 Port/src/CosmicShore.Engine/SceneGraph/Component.cs                   |   2 +-
 Port/src/CosmicShore.Engine/SceneGraph/GameObject.cs                  |   4 +-
 Port/src/CosmicShore.Engine/SceneGraph/TriggerPass.cs                 |  62 ++++
 Port/src/CosmicShore.Game/Controller/Assemblers/Assembler.cs          |  17 +
 Port/src/CosmicShore.Game/Controller/Assemblers/CornerSiteType.cs     |  12 +
 Port/src/CosmicShore.Game/Controller/Assemblers/GyroidAssembler.cs    | 555 ++++++++++++++++++++++++++++++
 Port/src/CosmicShore.Game/Controller/Assemblers/GyroidBondMate.cs     |  17 +
 Port/src/CosmicShore.Game/Controller/Assemblers/GyroidBondMateData.cs |  16 +
 .../Controller/Assemblers/GyroidBondMateDataContainer.cs              | 560 ++++++++++++++++++++++++++++++
 Port/src/CosmicShore.Game/Controller/Assemblers/SchwarzPAssembler.cs  | 325 ++++++++++++++++++
 Port/src/CosmicShore.Game/Controller/Assemblers/WallAssembler.cs      | 585 ++++++++++++++++++++++++++++++++
 .../Controller/Environment/FloraAndFauna/AssembledFlora.cs            | 285 ++++++++++++++++
 .../src/CosmicShore.Game/Controller/Environment/FloraAndFauna/Boid.cs | 380 +++++++++++++++++++++
 .../Controller/Environment/FloraAndFauna/BoidManager.cs               |  63 ++++
 Port/src/CosmicShore.Game/Controller/Vessel/BoidController.cs         |  91 +++++
 Port/tests/CosmicShore.Tests/AssemblerFamilyTests.cs                  | 267 +++++++++++++++
 Port/tests/CosmicShore.Tests/BoidChainTests.cs                        | 270 +++++++++++++++
 Port/tests/CosmicShore.Tests/PhysicsOverlapTests.cs                   |  53 +++
 21 files changed, 3628 insertions(+), 10 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 3814 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 68ce56d55..8cd6a761b 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -577,6 +577,27 @@ Note (test config): `CSDebug.Log/LogFormat` are `[Conditional("DEBUG")]` — inf
 logs strip out of Release. DebugExtensionsTests asserts per-config (`#if DEBUG`).
 Gate BOTH configs when touching logging paths.
 
+Ecosystem groundwork part 4 (landed alongside iteration 18): the Assemblers
+family + Boid fauna chain ported verbatim — `Assembler` (base) + `GyroidAssembler`
++ `GyroidBondMate`/`GyroidBondMateData`/`GyroidBondMateDataContainer` (48-entry
+baked bond table) + `CornerSiteType` + `WallAssembler` + `SchwarzPAssembler`, plus
+`AssembledFlora` (carries `GrowthInfo`; one CT2 deviation: `crystal.GrowCrystal`
+restores with the crystal-growth arc), `Boid`, `BoidManager`, and `BoidController`.
+Engine gains original-contract API: `Physics.OverlapSphere` (allocating, optional
+layer mask), `Physics.CheckBox` (AABB semantics, oriented overload accepted),
+`LayerMask` defaults extended with the project's TagManager layer table (3D UI=6 …
+TrailBlockOcclusion=18, incl. Mound=17 for the boid mound scan), and
+`GetComponentsInChildren<T>` now returns `T[]` (original array contract — Boid
+indexes by `.Length`). Two serializer deviations: `Boid.collisionEffects` and
+`BoidManager.Boids` init inline (the original engine's deserializer auto-creates
+serialized lists; the port engine has none). 23 tests across
+AssemblerFamilyTests (bond table coverage, growth-site resolution through the real
+CheckBox probe, wall alternation, Schwarz P surface stepping + reservation),
+BoidChainTests (goal steering, Attach grazing + Explode combat through the real
+spatial query — opposing mass only, conserved-mass clean), and PhysicsOverlapTests
+(CheckBox, layer-mask filtering). Still open in this lane: LightFauna and
+BranchingFlora for rung-6 ambience.
+
 Shape-content groundwork part 2 (landed alongside iteration 18): 25 more
 spawnables ported verbatim (BaseballCurve, Batman, CardioidSmear, CliffordTorus,
 Comet, DartBoard, DriftCourse, Helicoid, HopfFibration, Infinity, Lightning,
@@ -600,10 +621,7 @@ freestyle) and general track decoration for the client.
 Ecosystem groundwork part 3 (landed alongside iteration 18):
 `Physics.OverlapSphereNonAlloc` implemented against the TriggerPass collider
 registry (trigger + non-trigger, deterministic registration order, capacity
-truncation) — the spatial query Boid behavior scans with. 4 tests. Concrete fauna
-finding: `Boid` is blocked on `BoidController` (extends BoidManager) and the
-Assemblers family (`GyroidAssembler` + bond-mate data, ~2,087L) — that's the next
-self-contained arc for rung-6 ambience, not an idle-window port.
+truncation) — the spatial query Boid behavior scans with. 4 tests.
 
 Ecosystem groundwork part 2 (landed alongside iteration 18): `Flora` + `Fauna` +
 `FaunaConfigurationSO` + `FaunaReproductionRules` ported verbatim. V12 fauna
diff --git a/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs b/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs
index f2ec84b13..af3621efa 100644
--- a/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs
+++ b/Port/src/CosmicShore.Engine/Compat/EngineCompat.cs
@@ -175,6 +175,33 @@ namespace CosmicShore.Engine
         /// </summary>
         public static int OverlapSphereNonAlloc(Vector3 position, float radius, Collider[] results)
             => GameLoop.Current?.Triggers.OverlapSphereNonAlloc(position, radius, results) ?? 0;
+
+        /// <summary>All layers — the original engine's default mask for overlap queries.</summary>
+        public const int AllLayers = ~0;
+
+        /// <summary>
+        /// Allocating sphere query (original-engine contract): every registered, active
+        /// collider overlapping the sphere whose GameObject layer is in
+        /// <paramref name="layerMask"/>, in deterministic registration order.
+        /// </summary>
+        public static Collider[] OverlapSphere(Vector3 position, float radius, int layerMask = AllLayers)
+            => GameLoop.Current?.Triggers.OverlapSphere(position, radius, layerMask) ?? Array.Empty<Collider>();
+
+        /// <summary>
+        /// Box occupancy query (original-engine contract): true when any registered,
+        /// active collider overlaps the box.
+        /// </summary>
+        public static bool CheckBox(Vector3 center, Vector3 halfExtents)
+            => GameLoop.Current?.Triggers.CheckBox(center, halfExtents) ?? false;
+
+        /// <summary>
+        /// Oriented overload. The trigger pass treats boxes as world-space AABBs
+        /// (rotation ignored — same phase-2 deviation as <see cref="TriggerPass"/> box
+        /// overlap); the orientation parameter is accepted for source compatibility and
+        /// gains effect with the full physics phase.
+        /// </summary>
+        public static bool CheckBox(Vector3 center, Vector3 halfExtents, Quaternion orientation)
+            => CheckBox(center, halfExtents);
     }
```

</details>

### `74115a178` — feat(port): spawnable family complete + remaining rung-6 fauna concretes

_Claude, 2026-06-12 21:12:42 +0000_

```text
All previously-blocked spawnables ported verbatim now that CrystalManager
and the Assemblers family landed: SpawnableCrystal, SpawnableFlora,
SpawnableGyroid, SpawnableWall, SpawnableLSystem, SpawnableWaypointTrack —
the spawnable content family is COMPLETE.

Rung-6 flora/fauna concretes complete: BranchingFlora (crystaltropic
branching growth), LightFauna + LightFaunaManager + both DataSOs (phased
school movement), BodySegmentFauna + Bone + Worm + WormManager (segmented
body chains), LerpUtilities. All verbatim modulo substitutions. Engine
gains Random.rotation (uniform axis-angle).

The CrystalCollisionTurnMonitor optionalEnvironment waypoint deviation is
now restorable — deferred until rung-4 integration (that agent owns
TurnMonitors/). Gate green both configs: 907 + 252.
```

```text
 Port/PORT_PLAN.md                                                     |  10 +
 Port/src/CosmicShore.Engine/Math/Random.cs                            |   4 +
 .../Controller/Environment/FloraAndFauna/BodySegmentFauna.cs          |  34 ++
 .../src/CosmicShore.Game/Controller/Environment/FloraAndFauna/Bone.cs |  56 ++++
 .../Controller/Environment/FloraAndFauna/BranchingFlora.cs            | 228 ++++++++++++++
 .../Controller/Environment/FloraAndFauna/LightFauna.cs                | 330 +++++++++++++++++++
 .../Controller/Environment/FloraAndFauna/LightFaunaDataSO.cs          |  25 ++
 .../Controller/Environment/FloraAndFauna/LightFaunaManager.cs         | 194 ++++++++++++
 .../Controller/Environment/FloraAndFauna/LightFaunaManagerDataSO.cs   |  25 ++
 .../src/CosmicShore.Game/Controller/Environment/FloraAndFauna/Worm.cs | 222 +++++++++++++
 .../Controller/Environment/FloraAndFauna/WormManager.cs               | 124 ++++++++
 .../Controller/Environment/MiniGameObjects/SpawnableCrystal.cs        |  27 ++
 .../Controller/Environment/MiniGameObjects/SpawnableFlora.cs          |  27 ++
 .../Controller/Environment/MiniGameObjects/SpawnableGyroid.cs         | 201 ++++++++++++
 .../Controller/Environment/MiniGameObjects/SpawnableLSystem.cs        | 253 +++++++++++++++
 .../Controller/Environment/MiniGameObjects/SpawnableWall.cs           |  67 ++++
 .../Controller/Environment/MiniGameObjects/SpawnableWaypointTrack.cs  | 539 ++++++++++++++++++++++++++++++++
 Port/src/CosmicShore.Game/Utility/LerpUtilities.cs                    |  31 ++
 18 files changed, 2397 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 2518 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 8cd6a761b..0c9e9694e 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -577,6 +577,16 @@ Note (test config): `CSDebug.Log/LogFormat` are `[Conditional("DEBUG")]` — inf
 logs strip out of Release. DebugExtensionsTests asserts per-config (`#if DEBUG`).
 Gate BOTH configs when touching logging paths.
 
+Ecosystem + content completion (landed alongside iteration 19): the spawnable
+family is COMPLETE — SpawnableCrystal, SpawnableFlora, SpawnableGyroid,
+SpawnableWall, SpawnableLSystem, SpawnableWaypointTrack ported verbatim (all
+blockers cleared by the rung-3 CrystalManager and the Assemblers arc). Rung-6
+concretes complete too: BranchingFlora, LightFauna(+Manager/DataSO/ManagerDataSO),
+BodySegmentFauna, Bone, Worm, WormManager, LerpUtilities — all verbatim. Engine
+gains `Random.rotation`. NOTE: the CrystalCollisionTurnMonitor `optionalEnvironment`
+waypoint deviation can now be restored — deferred until rung-4 integration (the
+rung-4 agent owns TurnMonitors/).
+
 Ecosystem groundwork part 4 (landed alongside iteration 18): the Assemblers
 family + Boid fauna chain ported verbatim — `Assembler` (base) + `GyroidAssembler`
 + `GyroidBondMate`/`GyroidBondMateData`/`GyroidBondMateDataContainer` (48-entry
diff --git a/Port/src/CosmicShore.Engine/Math/Random.cs b/Port/src/CosmicShore.Engine/Math/Random.cs
index 57e276c9d..5a131a98f 100644
--- a/Port/src/CosmicShore.Engine/Math/Random.cs
+++ b/Port/src/CosmicShore.Engine/Math/Random.cs
@@ -41,6 +41,10 @@ namespace CosmicShore.Engine
             }
         }
 
+        /// <summary>Uniformly random rotation (axis from the unit sphere, angle in [0, 360)).</summary>
+        public static Quaternion rotation
+            => Quaternion.AngleAxis(Range(0f, 360f), onUnitSphere);
+
         /// <summary>Random point inside (or on) a unit sphere.</summary>
         public static Vector3 insideUnitSphere
         {
diff --git a/Port/src/CosmicShore.Game/Controller/Environment/FloraAndFauna/BodySegmentFauna.cs b/Port/src/CosmicShore.Game/Controller/Environment/FloraAndFauna/BodySegmentFauna.cs
new file mode 100644
index 000000000..92e855966
--- /dev/null
+++ b/Port/src/CosmicShore.Game/Controller/Environment/FloraAndFauna/BodySegmentFauna.cs
@@ -0,0 +1,34 @@
+using CosmicShore.Gameplay;
+
+namespace CosmicShore.Gameplay
+{
+    /// <summary>
+    /// Individual segment of a <see cref="Worm"/> creature.
+    /// Handles segment-specific death logic (splitting, head/tail status).
+    /// </summary>
+    public class BodySegmentFauna : Fauna
+    {
+        public Worm ParentWorm { get; set; }
+        public BodySegmentFauna PreviousSegment { get; set; }
+        public BodySegmentFauna NextSegment { get; set; }
+        public bool IsHead;
+        public bool IsTail;
+
+        protected override void Die(string killerName = "")
+        {
+            if (!IsHead && !IsTail)
+            {
+                ParentWorm.SplitWorm(this);
+            }
+            else if (IsHead)
+            {
+                ParentWorm.UpdateHeadStatus(false);
+            }
+            else if (IsTail)
+            {
+                ParentWorm.UpdateTailStatus(false);
+            }
+            ParentWorm.RemoveSegment(this);
+        }
+    }
+}
diff --git a/Port/src/CosmicShore.Game/Controller/Environment/FloraAndFauna/Bone.cs b/Port/src/CosmicShore.Game/Controller/Environment/FloraAndFauna/Bone.cs
new file mode 100644
index 000000000..dd749888e
--- /dev/null
```

</details>

### `56ab54398` — feat(port): quest data chain verbatim — SA1 quest deviation closed

_Claude, 2026-06-12 21:20:13 +0000_

```text
Quest, SO_QuestChain, CallToAction, UserAction, VirtualItem, ItemPrice —
all verbatim modulo substitutions. SO_TrainingGame.SO_QuestChain is a real
field again (the file is now fully verbatim, zero markers). QuestSystem
(the MonoBehaviour orchestrator) remains for the systems phase.

Gate green both configs: 907 + 252.
```

```text
 Port/PORT_PLAN.md                                               |  6 ++++
 Port/src/CosmicShore.Game/ScriptableObjects/SO_QuestChain.cs    | 12 +++++++
 Port/src/CosmicShore.Game/ScriptableObjects/SO_TrainingGame.cs  |  2 +-
 Port/src/CosmicShore.Game/System/CallToAction/CallToAction.cs   | 34 +++++++++++++++++++
 Port/src/CosmicShore.Game/System/Playfab/Economy/ItemPrice.cs   | 22 +++++++++++++
 Port/src/CosmicShore.Game/System/Playfab/Economy/VirtualItem.cs | 59 +++++++++++++++++++++++++++++++++
 Port/src/CosmicShore.Game/System/Quest/Quest.cs                 | 69 +++++++++++++++++++++++++++++++++++++++
 Port/src/CosmicShore.Game/System/UserAction/UserAction.cs       | 27 +++++++++++++++
 8 files changed, 230 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 294 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 0c9e9694e..fd4755c9b 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -577,6 +577,12 @@ Note (test config): `CSDebug.Log/LogFormat` are `[Conditional("DEBUG")]` — inf
 logs strip out of Release. DebugExtensionsTests asserts per-config (`#if DEBUG`).
 Gate BOTH configs when touching logging paths.
 
+Quest-data groundwork (landed alongside iteration 19): `Quest` + `SO_QuestChain`
++ `CallToAction` + `UserAction` + `VirtualItem` + `ItemPrice` ported verbatim.
+SA1 quest deviation CLOSED — `SO_TrainingGame.SO_QuestChain` is a real field
+again (file now fully verbatim). `QuestSystem` (105L, MonoBehaviour orchestrator)
+remains for the systems phase.
+
 Ecosystem + content completion (landed alongside iteration 19): the spawnable
 family is COMPLETE — SpawnableCrystal, SpawnableFlora, SpawnableGyroid,
 SpawnableWall, SpawnableLSystem, SpawnableWaypointTrack ported verbatim (all
diff --git a/Port/src/CosmicShore.Game/ScriptableObjects/SO_QuestChain.cs b/Port/src/CosmicShore.Game/ScriptableObjects/SO_QuestChain.cs
new file mode 100644
index 000000000..28e4f4856
--- /dev/null
+++ b/Port/src/CosmicShore.Game/ScriptableObjects/SO_QuestChain.cs
@@ -0,0 +1,12 @@
+using CosmicShore.Core;
+using System.Collections.Generic;
+using CosmicShore.Engine;
+
+namespace CosmicShore.ScriptableObjects
+{
+    [CreateAssetMenu(fileName = "Quest Chain", menuName = "ScriptableObjects/QuestChain", order = 12)]
+    public class SO_QuestChain : ScriptableObject
+    {
+        public List<Quest> Quests;
+    }
+}
\ No newline at end of file
diff --git a/Port/src/CosmicShore.Game/ScriptableObjects/SO_TrainingGame.cs b/Port/src/CosmicShore.Game/ScriptableObjects/SO_TrainingGame.cs
index dbb28e408..f66199263 100644
--- a/Port/src/CosmicShore.Game/ScriptableObjects/SO_TrainingGame.cs
+++ b/Port/src/CosmicShore.Game/ScriptableObjects/SO_TrainingGame.cs
@@ -23,6 +23,6 @@ namespace CosmicShore.ScriptableObjects
         [SerializeField] public GameplayReward IntensityThreeReward;
         [SerializeField] public GameplayReward IntensityFourReward;
 
-        // PORT Deviation (SA1, restore when SO_QuestChain / Quest system ports): public SO_QuestChain SO_QuestChain;
+        public SO_QuestChain SO_QuestChain;
     }
 }
diff --git a/Port/src/CosmicShore.Game/System/CallToAction/CallToAction.cs b/Port/src/CosmicShore.Game/System/CallToAction/CallToAction.cs
new file mode 100644
index 000000000..d8d139a98
--- /dev/null
+++ b/Port/src/CosmicShore.Game/System/CallToAction/CallToAction.cs
@@ -0,0 +1,34 @@
+using System;
+using System.Collections.Generic;
+using CosmicShore.Core;
+using CosmicShore.Data;
+using CosmicShore.Engine;
+using CosmicShore.Utility;
+
+namespace CosmicShore.Core
+{
+    [Serializable]
+    public class CallToAction
+    {
+        public CallToActionTargetType CallToActionTargetID;
+        public UserActionType CompletionUserAction;
+        public List<CallToActionTargetType> DependencyTargetIDs;
+
+        public CallToAction(CallToActionTargetType callToActionTargetID, UserActionType completionUserAction, List<CallToActionTargetType> dependencyTargets = null)
+        {
+            CallToActionTargetID = callToActionTargetID;
+            CompletionUserAction = completionUserAction;
+            CSDebug.LogFormat("{0} - {1} {2}",nameof(CallToAction), nameof(CallToActionTargetID), CallToActionTargetID);
+            CSDebug.LogFormat("{0} - {1} {2}",nameof(CallToAction), nameof(CompletionUserAction), CompletionUserAction);
+
+            if (dependencyTargets != null)
+            {
+                DependencyTargetIDs = dependencyTargets;
```

</details>

### `97fcb88fb` — feat(port): QuestSystem + UserActionSystem + CallToActionSystem verbatim

_Claude, 2026-06-12 21:27:46 +0000_

```text
The full quest/user-action/call-to-action orchestration chain:
UserActionSystem broadcasts completed actions, QuestSystem tracks
per-label active quests and completes them on matching action
counts/quantities, CallToActionSystem tracks active CTAs with dependency
chains. All three verbatim modulo the using substitution.

Gate green both configs: 907 + 252.
```

```text
 Port/PORT_PLAN.md                                                   |   5 ++
 Port/src/CosmicShore.Game/System/CallToAction/CallToActionSystem.cs | 161 ++++++++++++++++++++++++++++++++++
 Port/src/CosmicShore.Game/System/Quest/QuestSystem.cs               | 106 ++++++++++++++++++++++
 Port/src/CosmicShore.Game/System/UserAction/UserActionSystem.cs     |  21 +++++
 4 files changed, 293 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 325 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index fd4755c9b..f6027b3f2 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -577,6 +577,11 @@ Note (test config): `CSDebug.Log/LogFormat` are `[Conditional("DEBUG")]` — inf
 logs strip out of Release. DebugExtensionsTests asserts per-config (`#if DEBUG`).
 Gate BOTH configs when touching logging paths.
 
+Quest/action systems (landed alongside iteration 19, part 2): `QuestSystem` +
+`UserActionSystem` + `CallToActionSystem` ported verbatim — the full
+quest/user-action/CTA orchestration chain is in (SingletonPersistent-based,
+event-driven quest progress on UserActionSystem.OnUserActionCompleted).
+
 Quest-data groundwork (landed alongside iteration 19): `Quest` + `SO_QuestChain`
 + `CallToAction` + `UserAction` + `VirtualItem` + `ItemPrice` ported verbatim.
 SA1 quest deviation CLOSED — `SO_TrainingGame.SO_QuestChain` is a real field
diff --git a/Port/src/CosmicShore.Game/System/CallToAction/CallToActionSystem.cs b/Port/src/CosmicShore.Game/System/CallToAction/CallToActionSystem.cs
new file mode 100644
index 000000000..203283385
--- /dev/null
+++ b/Port/src/CosmicShore.Game/System/CallToAction/CallToActionSystem.cs
@@ -0,0 +1,161 @@
+using CosmicShore.Utility;
+using System;
+using System.Collections.Generic;
+using CosmicShore.Core;
+using CosmicShore.Data;
+using CosmicShore.Engine;
+
+namespace CosmicShore.Core
+{
+    public class CallToActionSystem : SingletonPersistent<CallToActionSystem>
+    {
+        [SerializeField] bool testMode;
+        List<CallToActionTargetType> ActiveTargets = new();
+        Dictionary<CallToActionTargetType, int> ActiveDependencyTargets = new(); // 
+        List<CallToAction> ActiveCallsToAction = new();
+        Dictionary<CallToActionTargetType, Action> CallToActionActivatedCallbacks = new();
+        Dictionary<CallToActionTargetType, Action> CallToActionDismissedCallbacks = new();
+
+        void Start()
+        {
+            LoadCallsToAction();
+
+            /*
+            foreach (var call in ActiveCallsToAction)
+            {
+                ActiveTargets.Add(call.CallToActionTargetID);
+
+            }*/
+
+            if (UserActionSystem.Instance != null)
+                UserActionSystem.Instance.OnUserActionCompleted += ResolveCallsToActionOnUserActionCompleted;
+            else
+                Debug.LogWarning($"{nameof(CallToActionSystem)}: UserActionSystem.Instance is null — skipping event subscription.");
+        }
+
+        public void AddCallToAction(CallToAction call)
+        {
+            // Add to tracking lists
+            ActiveCallsToAction.Add(call);
+            ActiveTargets.Add(call.CallToActionTargetID);
+
+            foreach (var targetId in call.DependencyTargetIDs)
+            {
+                // This one don't use TryGetValue because the value in the key-value pair should be incremented.
+                if (ActiveDependencyTargets.ContainsKey(targetId))
+                {
+                    ActiveDependencyTargets[targetId]++;
+                }
+                else
+                {
+                    ActiveDependencyTargets.Add(targetId, 1);
+
+                    // Notify anyone interested 
+                    // TryGetValue is faster, reference: https://stackoverflow.com/questions/9382681/what-is-more-efficient-dictionary-trygetvalue-or-containskeyitem
+                    if(CallToActionActivatedCallbacks.TryGetValue(targetId, out var callback))
+                        callback?.Invoke();
+                }
+            }
```

</details>

### `133d92b40` — feat(port): convergence rung 4 — real domain scoring, golf standings, live comeback

_Claude, 2026-06-12 21:35:53 +0000_

```text
Race end now fires through the real pipeline: verbatim
NetworkCrystalCollisionTurnMonitor (CheckForEndOfTurn delegates to
gameData.ScoringRule.IsObjectiveReached over domain-aggregated sums) +
verbatim TurnMonitorController driven by GameDataSO turn events. The
director's per-pilot win count is deleted; end flow is the
HexRaceController semantics collapsed to single-process: winners share
Score=finishTime, losing-domain players carry 10000+domain-deficit and
tie within a domain, SortRoundStats(golf), domain stats + results,
InvokeWinnerCalculated + InvokeMiniGameEnd.

ElementalComebackSystem runs live with the real HexRace profile values
(Squirrel Space 3 / Time 3, CrystalsCollected source) — trailing DOMAINS
get buffs, the leading domain none. Rivals rebalanced over ActiveDomains
(Blue can't win a domain objective and no longer races; the 4th pilot is
the human's Jade teammate). HUD renders domain totals + remaining from
the shared GameDataSO; scoreboard renders RoundStatsList golf order.

Energy economy tuned now that scoring is visible: GainPerPrism
0.045→0.025, BoostDrainPerSecond 0.45→0.55 — boosting outpaces plain
skim gain; only drift-skimming nearly sustains it.

CrystalCollisionTurnMonitor waypoint deviation RESTORED (file verbatim)
now that SpawnableWaypointTrack is in.

4 new rung-4 tests. Gate green both configs: 910 + 252. Headless 1200
frames deterministic: domains J7/R1/G4 (remaining 23), trail 786
identical to rung 3, exit 0.
```

```text
 Port/PORT_PLAN.md                                                     | 100 ++++++++++++--
 Port/src/CosmicShore.Client/RaceWindow.cs                             |  69 +++++++---
 Port/src/CosmicShore.Client/SkimRacePrisms.cs                         |   6 +-
 Port/src/CosmicShore.Client/SkimRaceSim.cs                            | 232 +++++++++++++++++++++++++++-----
 Port/src/CosmicShore.Game/Controller/Arcade/TurnMonitorController.cs  | 109 +++++++++++++++
 .../Controller/Arcade/TurnMonitors/CrystalCollisionTurnMonitor.cs     |   7 +-
 .../Arcade/TurnMonitors/NetworkCrystalCollisionTurnMonitor.cs         |  97 +++++++++++++
 Port/tests/CosmicShore.Tests/ClientConvergenceTests.cs                | 204 ++++++++++++++++++++++++++--
 8 files changed, 740 insertions(+), 84 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1141 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index f6027b3f2..c4385a81f 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -486,6 +486,51 @@ Next: V8 (VesselTransformer + member restore), rival balance from prompter feedb
   exit 0 — frame 1200: `crystals [7,2,5,2], claims 16, levels C6/M0/S1/T0, trail 786`
   (per-pilot level attribution now exact; prism determinism preserved vs rung 2).
 
+- **Iteration 19** (2026-06-12): **Convergence rung 4 — real scoring + HUD semantics
+  in the playable client.** Ported verbatim: `NetworkCrystalCollisionTurnMonitor`
+  (NetworkVariable target sync → `GameDataSO.CrystalTargetCount`; CheckForEndOfTurn
+  delegates to `gameData.ScoringRule.IsObjectiveReached`; domain-deficit remaining
+  display) + `TurnMonitorController` (OnMiniGameTurnStarted → StartMonitors;
+  per-frame end check → `InvokeGameTurnConditionsMet`). Client: the scoring rig
+  (monitor + controller + `ElementalComebackSystem`) is built per race, host-mode
+  `Spawn()`ed (IsServer=true — the scene-placed-NetworkBehaviour contract) and torn
+  down in `Shutdown()` (the monitor's async heartbeat must not outlive the race);
+  GameDataSO gains the full turn-event set, `GameMode=HexRace`,
+  `RequestedDomainCount`, a `HexRaceScoringRuleSO` instance, and reflection-mirrored
+  `LocalPlayer`/`LocalRoundStats` (single-process: the human IS the local user).
+  Rivals are balanced over the ACTIVE domains (`ActiveDomains[(i+1)%3]` — a 4th
+  pilot becomes the human's Jade teammate; Blue no longer races, it can't win a
+  domain-aggregated objective). The director's per-pilot win count is DELETED — the
+  race finishes off `OnMiniGameTurnEnd` with the HexRaceController end flow
+  collapsed to single-process (AssignScores: winners = finishTime, losers =
+  10000 + domain deficit tying within a domain; SortRoundStats(golf);
+  CalculateDomainStats; SetResults(BuildResults); InvokeWinnerCalculated +
+  InvokeMiniGameEnd); mid-race restart abandons the turn unscored through the same
+  protocol. Director publishes per-domain `SetDomainMetricSum`
+  (MultiplayerDomainGamesController's server role) → RaceWindow HUD shows ally vs
+  opposing DOMAIN totals + the monitor's remaining; the finish scoreboard renders
+  `gameData.RoundStatsList` golf order and VICTORY/DEFEAT is by `WinnerDomain`.
+  Comeback runs verbatim with the real HexRaceComebackProfile values (Squirrel:
+  Space 3 / Time 3, CrystalsCollected source) — trailing domains rise through the
+  elementals fundamental; composition note: the verbatim comeback overwrites base
+  levels to baseline+bonus each 1s tick while a turn runs, so claim-earned levels
+  are transient mid-race (identical to the original HexRace — levels are
+  comeback-anchored; Mass/Charge weights are 0 in the real profile). **Energy
+  economy balanced (NEXT-UP item 2 closed): `GainPerPrism` 0.045 → 0.025
+  (~0.25/s plain ribbon ride, ~0.5/s drift-skimming), boost drain 0.45 → 0.55/s**
+  — boosting now outpaces plain skim gain (-0.3/s net) and only drift-skimming
+  nearly sustains it (-0.05/s), so the bar breathes instead of pegging while
+  boosting (idle non-boosting riders still cap — energy is spent by boosting).
+  New tests: ClientConvergenceTests rung-4 quartet — domain-aggregated end (split
+  target across teammates, zero director claims), golf standings (winners share
+  finishTime; losing domains tie on Encode(deficit); RoundStatsList sorted;
+  Results ranked), comeback buffs trailing domain only, full short race through
+  the real pipeline. **1143 tests green in BOTH configs (891 + 252)**; headless
+  300/1200-frame runs exit 0 — frame 1200: `crystals [6,1,4,1], claims 12,
+  domains J7/R1/G4 (remaining 23), trail 786` — identical across repeat runs
+  (prism determinism preserved; claim pattern re-baselined by the domain remap +
+  comeback Space/Time buffs).
+
 ## PRIME AXIS — CLIENT CONVERGENCE (prompter reorientation, 2026-06-12)
 
 > "our goal is to convert everything over so a player cannot tell the difference
@@ -551,26 +596,53 @@ the next rung.
    insideUnitSphere/onUnitSphere; Instantiate gained the (pos, rot, parent)
    overload. Renderer draws live crystal transforms (claimed elemental crystals
    visibly fly to their claimer).
-4. **Real scoring + HUD semantics**: HexRaceScoringRuleSO domain-aggregated end,
-   golf standings; domains share totals.
+4. **Real scoring + HUD semantics** ✅ (iteration 19): the race ends through the
+   REAL pipeline — `NetworkCrystalCollisionTurnMonitor` + `TurnMonitorController`
+   ported verbatim and Spawn()ed host-mode in the client; the monitor publishes the
+   crystal target into `GameDataSO.CrystalTargetCount` and its `CheckForEndOfTurn`
+   delegates to `HexRaceScoringRuleSO.IsObjectiveReached` over
+   `ScoringMetrics.SumByDomain` — teammates share their domain total (rivals are
+   now balanced over the ACTIVE domains, `ActiveDomains[(i+1)%3]`, so a 4th pilot
+   is the human's Jade teammate; Blue no longer races). The director's claim-count
+   win check is deleted: it finishes the race off `OnMiniGameTurnEnd`
+   (HexRaceController.OnTurnEndedCustom + SyncFinalScores_ClientRpc collapsed to
+   single-process): `rule.AssignScores` (winners = finishTime; losers =
+   10000 + domain deficit, tying within a domain), `SortRoundStats(golf)`,
+   `CalculateDomainStats`, `SetResults(rule.BuildResults)`, `InvokeWinnerCalculated`
+   + `InvokeMiniGameEnd`. The RaceWindow scoreboard renders `gameData.RoundStatsList`
+   golf order; VICTORY = your DOMAIN won (`gameData.WinnerDomain`); the in-race HUD
+   shows ally-domain total vs opposing-domain totals via
+   `GameDataSO.GetDomainMetricSum` (the director publishes SumByDomain — the
+   MultiplayerDomainGamesController server role) + the monitor's display-channel
```

</details>

### `417385594` — feat(port): SegmentSpawner verbatim — deterministic track infrastructure

_Claude, 2026-06-12 21:45:20 +0000_

```text
The HexRace track spawner: seeded segment placement with per-segment prism
trails and intensity-scaled spacing. One deviation: the diagnostic
super-shield attachment restores when PrismStellatedOctahedronShield ports
with the engine Mesh/MeshFilter arc. Engine gains the
Transform.Rotate(axis, angle) overload (original contract).

This unblocks the real HexRaceController chain (MiniGameControllerBase →
MultiplayerMiniGameControllerBase → MultiplayerDomainGamesController →
HexRaceController, 1,112L) — recorded in PORT_PLAN as the next dedicated
arc toward replacing SkimRaceDirector with the real controller.

Gate green both configs: 910 + 252.
```

```text
 Port/PORT_PLAN.md                                                     |  10 +
 Port/src/CosmicShore.Engine/SceneGraph/Transform.cs                   |   7 +
 .../Controller/Environment/MiniGameObjects/SegmentSpawner.cs          | 413 ++++++++++++++++++++++++++++++++
 3 files changed, 430 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 458 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index c4385a81f..ad0af7618 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -649,6 +649,16 @@ Note (test config): `CSDebug.Log/LogFormat` are `[Conditional("DEBUG")]` — inf
 logs strip out of Release. DebugExtensionsTests asserts per-config (`#if DEBUG`).
 Gate BOTH configs when touching logging paths.
 
+Track infrastructure (landed alongside iteration 20): `SegmentSpawner` ported
+(the HexRace deterministic-track spawner — seeded segment placement, prism trails
+per segment, intensity-scaled spacing). One deviation: the diagnostic
+super-shield block restores when `PrismStellatedOctahedronShield` ports with the
+engine Mesh/MeshFilter arc (468L shield + 270L StellatedOctahedronMeshGenerator —
+flagged as the shield arc). Engine gains the `Transform.Rotate(axis, angle)`
+overload. This unblocks the real `HexRaceController` chain
+(MiniGameControllerBase → Multiplayer → DomainGames → HexRace, 1,112L) — the
+next dedicated agent arc for replacing SkimRaceDirector with the real controller.
+
 Quest/action systems (landed alongside iteration 19, part 2): `QuestSystem` +
 `UserActionSystem` + `CallToActionSystem` ported verbatim — the full
 quest/user-action/CTA orchestration chain is in (SingletonPersistent-based,
diff --git a/Port/src/CosmicShore.Engine/SceneGraph/Transform.cs b/Port/src/CosmicShore.Engine/SceneGraph/Transform.cs
index 73413fd24..392e43f35 100644
--- a/Port/src/CosmicShore.Engine/SceneGraph/Transform.cs
+++ b/Port/src/CosmicShore.Engine/SceneGraph/Transform.cs
@@ -130,6 +130,13 @@ namespace CosmicShore.Engine
         public void Rotate(float xAngle, float yAngle, float zAngle, Space relativeTo = Space.Self)
             => Rotate(new Vector3(xAngle, yAngle, zAngle), relativeTo);
 
+        public void Rotate(Vector3 axis, float angle, Space relativeTo = Space.Self)
+        {
+            var delta = Quaternion.AngleAxis(angle, axis);
+            if (relativeTo == Space.Self) localRotation *= delta;
+            else rotation = delta * rotation;
+        }
+
         public void LookAt(Transform target) => LookAt(target.position);
 
         public void LookAt(Vector3 worldPosition)
diff --git a/Port/src/CosmicShore.Game/Controller/Environment/MiniGameObjects/SegmentSpawner.cs b/Port/src/CosmicShore.Game/Controller/Environment/MiniGameObjects/SegmentSpawner.cs
new file mode 100644
index 000000000..1e9c37aea
--- /dev/null
+++ b/Port/src/CosmicShore.Game/Controller/Environment/MiniGameObjects/SegmentSpawner.cs
@@ -0,0 +1,413 @@
+using CosmicShore.Gameplay;
+using System.Collections.Generic;
+using System.Linq;
+using CosmicShore.Utility;
+using CosmicShore.Engine.Injection;
+using CosmicShore.Engine;
+using CosmicShore.Engine.Soap;
+using CosmicShore.Data;
+
+namespace CosmicShore.Gameplay
+{
+    public class SegmentSpawner : MonoBehaviour
+    {
+        [Header("Dependencies")]
+        [Inject] private GameDataSO gameData;
+
+        [Header("Weighted Segments")]
+        [Tooltip("Segments selected randomly by weight for each NumberOfSegments slot.")]
+        [SerializeField] List<WeightedSpawnable> weightedSegments = new();
+
+        [Header("Guaranteed Shapes")]
+        [Tooltip("These always spawn every time, in addition to the weighted segments. " +
+                 "Use for shape spawnables that must always be present.")]
+        [SerializeField] List<SpawnableBase> guaranteedSpawnables = new();
+
+        [Header("Intensity-Mapped Spawning")]
+        [Tooltip("Optional: map specific spawnables to intensity levels (index 0 = intensity 1). " +
+                 "When set, overrides random selection for that intensity.")]
+        [SerializeField] SpawnableBase[] spawnableByIntensity;
+
+        // Legacy fields — kept for backward compatibility with scenes serialized before
+        // the WeightedSpawnable refactor. Migrated to weightedSegments at startup.
+        [SerializeField, HideInInspector] List<SpawnableBase> spawnableSegments;
+        [SerializeField, HideInInspector] List<float> spawnSegmentWeights;
+
```

</details>

### `e2603a4bc` — docs(port): reorientation 2 — track bleeding-edge (drift-sync + new-systems lanes)

_Claude, 2026-07-02 00:28:57 +0000_

```text
bleeding-edge merged in at 842c825c (325 commits). 57 ported files drifted
upstream (list committed at Port/docs/DRIFT_2026-06-13.txt); new systems
shipped (Toys, AstroLeague, Tournament, SandboxBenchmark). PORT_PLAN gains
the standing DRIFT-SYNC and NEW SYSTEMS lanes and the gap-closure
definition for this loop.
```

```text
 Port/PORT_PLAN.md              | 29 +++++++++++++++++++++++++++++
 Port/docs/DRIFT_2026-06-13.txt | 57 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 2 files changed, 86 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 103 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index ad0af7618..19d670c67 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -631,6 +631,35 @@ the next rung.
 6. Onward: cells/fauna ambience, more vessel classes, game modes — always through
    the real systems.
 
+## REORIENTATION 2 — TRACK BLEEDING-EDGE (prompter, 2026-06-13)
+
+> "pull latest from bleeding-edge … /loop until this has closed a massive number
+> of gaps between what it is like to play on bleeding edge with this port"
+
+bleeding-edge merged INTO this branch at 842c825c (325 commits, merge-base moved
+29b5f422 → c833c580). The port now chases a LIVE game. Two standing lanes join the
+convergence ladder, and every future bleeding-edge merge reopens them:
+
+1. **DRIFT-SYNC** — 57 already-ported files changed upstream (full list:
+   `Port/docs/DRIFT_2026-06-13.txt`). Ported copies must be re-verbatimed against
+   the new merge-base: for each file apply `git diff 29b5f422..c833c580 -- <unity
+   file>` onto the ported copy with the mechanical substitutions. Load-bearing
+   first: VesselTransformer, VesselStatus, VesselPrismController, Prism, Trail,
+   AIPilot, Cell, GameDataSO, RoundStats, scoring/monitors, Boid/Fauna family.
+2. **NEW SYSTEMS** — shipped on bleeding-edge, absent from the port:
+   - **Toys** (`Controller/Toys` 11 files + `ScriptableObjects/Toys` 5): freestyle
+     toy system (domain/vessel changer flip-sets, mini ship models, painting toy) —
+     player-facing in the lava-lamp/freestyle flow.
+   - **AstroLeague** (`Controller/Arcade/AstroLeague/` 7 files + ASTROLEAGUE.md):
+     new ball-game arcade mode (arena, ball, goal, match monitor, scoring rule).
+   - **Tournament** (`Controller/Arcade/Tournament/` 4 files + TournamentSystem
+     docs + UI cards): bracket play across arcade games.
+   - SandboxBenchmarkController, Settings additions, CloudData, Privacy UI.
+
+Gap-closure definition for this /loop: drift-sync complete + toys playable in the
+client + AstroLeague headless round running + remaining ladder rungs (5: real look,
+6: ambience/modes) — each iteration ships a player-feelable step, per Reorientation 1.
+
 ## NEXT UP (iteration 20)
 
 1. **Rung 5**: real look — instantiate a ThemeManager with a wired
diff --git a/Port/docs/DRIFT_2026-06-13.txt b/Port/docs/DRIFT_2026-06-13.txt
new file mode 100644
index 000000000..b22523f29
--- /dev/null
+++ b/Port/docs/DRIFT_2026-06-13.txt
@@ -0,0 +1,57 @@
+Assets/_Scripts/Controller/AI/AIPilot.cs -> Port/src/CosmicShore.Game/Controller/AI/AIPilot.cs
+Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs -> Port/src/CosmicShore.Game/Controller/Arcade/ElementalComebackSystem.cs
+Assets/_Scripts/Controller/Arcade/Scoring/ScoringMetrics.cs -> Port/src/CosmicShore.Game/Controller/Arcade/Scoring/ScoringMetrics.cs
+Assets/_Scripts/Controller/Arcade/TurnMonitorController.cs -> Port/src/CosmicShore.Game/Controller/Arcade/TurnMonitorController.cs
+Assets/_Scripts/Controller/Arcade/TurnMonitors/CrystalCollisionTurnMonitor.cs -> Port/src/CosmicShore.Game/Controller/Arcade/TurnMonitors/CrystalCollisionTurnMonitor.cs
+Assets/_Scripts/Controller/Arcade/TurnMonitors/NetworkCrystalCollisionTurnMonitor.cs -> Port/src/CosmicShore.Game/Controller/Arcade/TurnMonitors/NetworkCrystalCollisionTurnMonitor.cs
+Assets/_Scripts/Controller/Assemblers/GyroidAssembler.cs -> Port/src/CosmicShore.Game/Controller/Assemblers/GyroidAssembler.cs
+Assets/_Scripts/Controller/Assemblers/SchwarzPAssembler.cs -> Port/src/CosmicShore.Game/Controller/Assemblers/SchwarzPAssembler.cs
+Assets/_Scripts/Controller/Assemblers/WallAssembler.cs -> Port/src/CosmicShore.Game/Controller/Assemblers/WallAssembler.cs
+Assets/_Scripts/Controller/Camera/CustomCameraController.cs -> Port/src/CosmicShore.Game/Controller/Camera/CustomCameraController.cs
+Assets/_Scripts/Controller/Environment/Cell.cs -> Port/src/CosmicShore.Game/Controller/Environment/Cell.cs
+Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs -> Port/src/CosmicShore.Game/Controller/Environment/FloraAndFauna/AssembledFlora.cs
+Assets/_Scripts/Controller/Environment/FloraAndFauna/BodySegmentFauna.cs -> Port/src/CosmicShore.Game/Controller/Environment/FloraAndFauna/BodySegmentFauna.cs
+Assets/_Scripts/Controller/Environment/FloraAndFauna/Boid.cs -> Port/src/CosmicShore.Game/Controller/Environment/FloraAndFauna/Boid.cs
+Assets/_Scripts/Controller/Environment/FloraAndFauna/BranchingFlora.cs -> Port/src/CosmicShore.Game/Controller/Environment/FloraAndFauna/BranchingFlora.cs
+Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs -> Port/src/CosmicShore.Game/Controller/Environment/FloraAndFauna/Fauna.cs
+Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs -> Port/src/CosmicShore.Game/Controller/Environment/FloraAndFauna/LightFauna.cs
+Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFaunaDataSO.cs -> Port/src/CosmicShore.Game/Controller/Environment/FloraAndFauna/LightFaunaDataSO.cs
+Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFaunaManager.cs -> Port/src/CosmicShore.Game/Controller/Environment/FloraAndFauna/LightFaunaManager.cs
+Assets/_Scripts/Controller/Environment/HealthPrism.cs -> Port/src/CosmicShore.Game/Controller/Environment/HealthPrism.cs
+Assets/_Scripts/Controller/Environment/MiniGameObjects/SegmentSpawner.cs -> Port/src/CosmicShore.Game/Controller/Environment/MiniGameObjects/SegmentSpawner.cs
+Assets/_Scripts/Controller/Environment/Spawning/SpawnPoint.cs -> Port/src/CosmicShore.Game/Controller/Environment/Spawning/SpawnPoint.cs
+Assets/_Scripts/Controller/IO/DualMouseInputStrategy.cs -> Port/src/CosmicShore.Game/Controller/IO/DualMouseInputStrategy.cs
+Assets/_Scripts/Controller/IO/TouchInputStrategy.cs -> Port/src/CosmicShore.Game/Controller/IO/TouchInputStrategy.cs
+Assets/_Scripts/Controller/ImpactEffects/Impactors/VesselImpactor.cs -> Port/src/CosmicShore.Game/Controller/ImpactEffects/Impactors/VesselImpactor.cs
+Assets/_Scripts/Controller/Managers/BlockDensityGrid.cs -> Port/src/CosmicShore.Game/Controller/Managers/BlockDensityGrid.cs
+Assets/_Scripts/Controller/Managers/CameraManager.cs -> Port/src/CosmicShore.Game/Controller/Managers/CameraManager.cs
+Assets/_Scripts/Controller/Managers/PrismAOERegistry.cs -> Port/src/CosmicShore.Game/Controller/Managers/PrismAOERegistry.cs
+Assets/_Scripts/Controller/Managers/PrismStateManager.cs -> Port/src/CosmicShore.Game/Controller/Managers/PrismStateManager.cs
+Assets/_Scripts/Controller/Player/Player.cs -> Port/src/CosmicShore.Game/Controller/Player/Player.cs
+Assets/_Scripts/Controller/Vessel/Prism.cs -> Port/src/CosmicShore.Game/Controller/Vessel/Prism.cs
+Assets/_Scripts/Controller/Vessel/Trail.cs -> Port/src/CosmicShore.Game/Controller/Vessel/Trail.cs
+Assets/_Scripts/Controller/Vessel/VesselCustomization.cs -> Port/src/CosmicShore.Game/Controller/Vessel/VesselCustomization.cs
+Assets/_Scripts/Controller/Vessel/VesselHelper.cs -> Port/src/CosmicShore.Game/Controller/Vessel/VesselHelper.cs
```

</details>

### `9373afa8a` — feat(port): convergence rung 5 — the real game's look via ThemeManager

_Claude, 2026-07-02 02:12:52 +0000_

```text
Salvaged and completed from the interrupted rung-5 run. SkimRaceTheme.cs
(client-side factory, not a port) transcribes the wired Unity theme assets
exactly — OriginalColorSetSO.asset + OriginalMaterialSet.asset via
ThemeManagerDataContainer.asset, HDR intensities preserved — and the
verbatim ThemeManager generates the four per-domain material sets at race
startup (the original Bootstrap flow; single writer of TeamMaterialSets
and GameFeedAPI.ColorSet).

RaceWindow draws from the theme: prism trails tinted per domain via
GetDomainUIColor (TrailHighlightColor — hardcoded DomainColor table
deleted), vessel tints from ShipColor1/2 via TeamMaterialSets,
team-crystal stations in their domain's crystal colors, sky from
EnvironmentColors.SkyColor, HUD domain panels from the same single color
source GameFeedAPI uses.

Gate green both configs: 910 + 252.
```

```text
 Port/src/CosmicShore.Client/RaceWindow.cs    | 164 +++++++++++++++++++++++++++++++----------
 Port/src/CosmicShore.Client/SkimRaceSim.cs   |  37 ++++------
 Port/src/CosmicShore.Client/SkimRaceTheme.cs | 228 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 3 files changed, 367 insertions(+), 62 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 566 lines)</summary>

```diff
diff --git a/Port/src/CosmicShore.Client/RaceWindow.cs b/Port/src/CosmicShore.Client/RaceWindow.cs
index 4f4700800..d4c0133bb 100644
--- a/Port/src/CosmicShore.Client/RaceWindow.cs
+++ b/Port/src/CosmicShore.Client/RaceWindow.cs
@@ -38,6 +38,8 @@ namespace CosmicShore.Client
         GameLoop _loop;
         SkimRaceDirector _race;          // race rules — flight itself is the real ported rig
         SkimRacePilot _pilot;            // the human's rig (vessel transform, stats, resources)
+        ThemeManagerDataContainerSO _theme; // the REAL theme (rung 5): every draw color reads here
+        Camera _camera;                  // engine camera — carries the theme's authored sky color
         IInputStatus _playerStatus;      // the rig's REAL InputStatus (V7) — strategy writes here
         readonly GamepadInputStrategy _gamepadStrategy = new(); // the ported, authentic dual-stick scheme
         EngineInput.Gamepad _shimPad;
@@ -59,8 +61,13 @@ namespace CosmicShore.Client
         uint _starVao, _starVbo; int _starCount;
         uint _railVao, _railVbo; int _railCount;
         uint _ringVao, _ringVbo; int _ringCount;
-        readonly Dictionary<Element, (uint vao, int count)> _crystalMeshes = new();
+        // crystal meshes keyed by (element, lock domain): uncommitted stations keep the
+        // per-element prefab tint, team stations bake the domain's crystal material colors
+        readonly Dictionary<(Element element, Domains domain), (uint vao, int count)> _crystalMeshes = new();
+        (uint vao, int count)[] _stationMeshes;          // per-station lookup into _crystalMeshes
+        (uint vao, int count) _burstMesh;                // omni flash for claim bursts
         readonly Dictionary<Domains, (uint vao, int count)> _vesselMeshes = new();
+        (uint vao, int count) _skimRingIdle, _skimRingActive; // skimmer reach in SkimmerColor
         uint _hudVao, _hudVbo;
 
         // per-pilot trail render slots (trails live in the sim — persistent, skimmable);
@@ -84,14 +91,19 @@ namespace CosmicShore.Client
             _screenshotFrame = screenshotFrame;
         }
 
-        /// <summary>Domain palette — jade player, ruby/gold/blue rivals.</summary>
-        static (float r, float g, float b) DomainColor(Domains domain) => domain switch
+        /// <summary>
+        /// Domain UI color — the theme's single source: <see cref="SO_ColorSet.GetDomainUIColor"/>
+        /// (the domain's TrailHighlightColor), the same color GameFeedAPI colors feed
+        /// messages with. Replaces the old hardcoded DomainColor table (rung 5).
+        /// </summary>
+        Color DomainUIColor(Domains domain) => _theme.GetDomainUIColor(domain);
+
+        /// <summary>Tuple view of <see cref="DomainUIColor"/> for the GL draw helpers.</summary>
+        (float r, float g, float b) DomainColor(Domains domain)
         {
-            Domains.Jade => (0.07f, 1f, 0.62f),
-            Domains.Ruby => (1f, 0.17f, 0.32f),
-            Domains.Gold => (1f, 0.8f, 0.25f),
-            _ => (0.35f, 0.6f, 1f), // Blue — the neutral domain
-        };
+            var c = DomainUIColor(domain);
+            return (c.r, c.g, c.b);
+        }
 
         /// <summary>Element palette for crystals (the buff each station carries).</summary>
         static (float r, float g, float b) ElementColor(Element element) => element switch
@@ -146,6 +158,13 @@ namespace CosmicShore.Client
             (_loop, _race) = SkimRaceFactory.Create(_seed, _crystalTarget, _rivalCount);
             _pilot = _race.HumanPilot;
             _playerStatus = _pilot.Input; // the rig's real InputStatus — single input sink
+
+            // Rung 5: the REAL theme (ThemeManager generated the per-domain material sets
+            // at factory time). The sky is the authored EnvironmentColors.SkyColor, applied
+            // through the container's own SetBackgroundColor onto an engine camera.
+            _theme = _race.GameData.ThemeManagerData;
+            _camera = new GameObject("MainCamera").AddComponent<Camera>();
+            _theme.SetBackgroundColor(_camera);
             _gamepadStrategy.Initialize(_playerStatus);
             _gamepadStrategy.OnStrategyActivated(); // ActiveInputDevice = Gamepad → pure analog triggers
             _audio = new AudioEngine(disabled: _screenshotPath != null);
@@ -387,14 +406,54 @@ void main()
 
         void BuildCrystalMeshes()
         {
-            // one octahedron per element — stations broadcast their buff by color
-            foreach (var element in new[] { Element.Charge, Element.Mass, Element.Space, Element.Time, Element.Omni })
-                _crystalMeshes[element] = BuildCrystalMesh(ElementColor(element));
+            // One octahedron per (element, lock domain) combination on the course.
+            // Uncommitted (Blue) stations broadcast their buff in the element tint — the
+            // original's per-element prefab defaultMaterial path. TEAM stations bake the
+            // domain's crystal material pair (GetTeamCrystalMaterial → _BrightCrystalColor /
```

</details>

### `6f9279b67` — feat(port): freestyle Toy system verbatim — new-systems lane (bleeding-edge PR #572)

_Claude, 2026-07-02 02:45:50 +0000_

```text
All 11 Controller/Toys files (Toy, ToyContext, ToyFactory, SwapToy,
SwapToySetCoordinator, DomainChangerToySet, VesselChangerToySet,
PaintingToy, MenuShapePainter, ToyboxController, VesselModelBuilder) + 5
ScriptableObjects/Toys definitions + MenuFreestyleEventsContainerSO —
verbatim modulo substitutions. Deviations marked for the unported
menu-swap arc (MenuServerPlayerVesselInitializer chain), the engine mesh
arc (VesselModelBuilder harvest → tinted-sphere fallback, tested), and one
RectTransform UI shell.

Engine original-contract additions: LineRenderer (buffer semantics
tested), GameObject.CreatePrimitive, Rendering.ShadowCastingMode, a
data-only TMPro shim under CosmicShore.Engine.UI (new substitution 'using
TMPro;' -> 'using CosmicShore.Engine.UI;', README updated),
CreateInstance<T> without the non-original new() constraint.

21 tests: toy bloom/arm/re-arm gating through the real TriggerPass, the
domain changer's FULL pipeline (trigger -> RequestSetDomain_ServerRpc ->
NetDomain -> mirror -> flip-set reconcile), vessel-changer flip-set +
control restore, painting-toy waypoints + conserved-mass teardown
(guides only — painted structure untouched), toybox unlock/placement.

Gate green both configs: 931 + 252. Headless 1200-frame fingerprint
byte-identical to the rung-4/5 baseline (crystals [6,1,4,1], trail 786).
```

```text
 Port/PORT_PLAN.md                                                     |   8 +
 Port/README.md                                                        |   2 +
 Port/src/CosmicShore.Engine/Rendering/Renderers.cs                    |  61 ++
 Port/src/CosmicShore.Engine/Rendering/ShadowCastingMode.cs            |  33 ++
 Port/src/CosmicShore.Engine/SceneGraph/GameObject.cs                  |  18 +
 Port/src/CosmicShore.Engine/ScriptableObject.cs                       |   7 +-
 Port/src/CosmicShore.Engine/UI/TextMeshPro.cs                         |  55 ++
 Port/src/CosmicShore.Game/Controller/Toys/DomainChangerToySet.cs      |  67 +++
 Port/src/CosmicShore.Game/Controller/Toys/MenuShapePainter.cs         | 130 +++++
 Port/src/CosmicShore.Game/Controller/Toys/PaintingToy.cs              |  55 ++
 Port/src/CosmicShore.Game/Controller/Toys/SwapToy.cs                  |  18 +
 Port/src/CosmicShore.Game/Controller/Toys/SwapToySetCoordinator.cs    | 201 +++++++
 Port/src/CosmicShore.Game/Controller/Toys/Toy.cs                      | 132 +++++
 Port/src/CosmicShore.Game/Controller/Toys/ToyContext.cs               |  50 ++
 Port/src/CosmicShore.Game/Controller/Toys/ToyFactory.cs               |  89 +++
 Port/src/CosmicShore.Game/Controller/Toys/ToyboxController.cs         | 235 ++++++++
 Port/src/CosmicShore.Game/Controller/Toys/VesselChangerToySet.cs      | 102 ++++
 Port/src/CosmicShore.Game/Controller/Toys/VesselModelBuilder.cs       | 100 ++++
 .../ScriptableObjects/MenuFreestyleEventsContainerSO.cs               |  45 ++
 .../ScriptableObjects/Toys/DomainChangerToyDefinitionSO.cs            |  23 +
 .../ScriptableObjects/Toys/PaintingToyDefinitionSO.cs                 |  40 ++
 Port/src/CosmicShore.Game/ScriptableObjects/Toys/ToyDefinitionSO.cs   |  78 +++
 Port/src/CosmicShore.Game/ScriptableObjects/Toys/ToyboxSO.cs          |  80 +++
 .../ScriptableObjects/Toys/VesselChangerToyDefinitionSO.cs            |  30 +
 Port/tests/CosmicShore.Tests/ToySystemTests.cs                        | 958 ++++++++++++++++++++++++++++++++
 25 files changed, 2615 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 2799 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 19d670c67..9b437ce9d 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -640,6 +640,14 @@ bleeding-edge merged INTO this branch at 842c825c (325 commits, merge-base moved
 29b5f422 → c833c580). The port now chases a LIVE game. Two standing lanes join the
 convergence ladder, and every future bleeding-edge merge reopens them:
 
+0. **TOYS ✅ (ported, iteration 21)** — all 11 Controller/Toys + 5 SO files verbatim
+   (menu-swap arc + mesh arc + UI-shell deviations marked; domain changer works
+   end-to-end through the real RequestSetDomain RPC today). Engine gains
+   LineRenderer, GameObject.CreatePrimitive, ShadowCastingMode, the TMPro
+   data-shim (`using TMPro;` → `using CosmicShore.Engine.UI;` — README updated),
+   and CreateInstance<T> without the non-original new() constraint. 21 tests.
+   Client integration notes live in the toys agent report (ToyboxController needs
+   _gameData/_freestyleEvents + freestyle transition events in the client scene).
 1. **DRIFT-SYNC** — 57 already-ported files changed upstream (full list:
    `Port/docs/DRIFT_2026-06-13.txt`). Ported copies must be re-verbatimed against
    the new merge-base: for each file apply `git diff 29b5f422..c833c580 -- <unity
diff --git a/Port/README.md b/Port/README.md
index 1bc7498fe..1ba965d13 100644
--- a/Port/README.md
+++ b/Port/README.md
@@ -56,6 +56,8 @@ same member names — except for these mechanical using-directive substitutions:
 | `using Reflex.Attributes;` / `using Reflex.Core;` / `using Reflex.Injectors;` | `using CosmicShore.Engine.Injection;` |
 | `using Unity.Services.Authentication;` / `using Unity.Services.Core;` | `using CosmicShore.Engine.Services;` |
 | `using Cysharp.Threading.Tasks;` | (phase 1: first-party async — see PORT_PLAN) |
+| `using TMPro;` | `using CosmicShore.Engine.UI;` (data-only TMP shim; frozen TMP numeric values) |
+| `using UnityEngine.Serialization;` | (delete the line — `FormerlySerializedAs` lives in `CosmicShore.Engine`) |
 
 Every ported enum's numeric values are frozen by tests in
 `tests/CosmicShore.Tests/EnumFreezeTests.cs` — these values are wire format, save
diff --git a/Port/src/CosmicShore.Engine/Rendering/Renderers.cs b/Port/src/CosmicShore.Engine/Rendering/Renderers.cs
index c9f23a2f9..f40b29118 100644
--- a/Port/src/CosmicShore.Engine/Rendering/Renderers.cs
+++ b/Port/src/CosmicShore.Engine/Rendering/Renderers.cs
@@ -14,6 +14,12 @@ namespace CosmicShore.Engine
     {
         public bool enabled = true;
 
+        /// <summary>Shadow casting mode (original default: On). Data-only until a render backend reads it.</summary>
+        public Rendering.ShadowCastingMode shadowCastingMode = Rendering.ShadowCastingMode.On;
+
+        /// <summary>Whether this renderer receives shadows (original default: true). Data-only.</summary>
+        public bool receiveShadows = true;
+
         /// <summary>
         /// World-space AABB. The headless engine carries no mesh data, so this assumes a
         /// unit-cube mesh: center at the transform position, size = |lossyScale| — the same
@@ -92,6 +98,61 @@ namespace CosmicShore.Engine
             => _blendShapeWeights.TryGetValue(index, out var w) ? w : 0f;
     }
 
+    /// <summary>
+    /// Polyline renderer (original LineRenderer contract, data-only). Holds the point
+    /// buffer, widths, endpoint colors, and cap/corner tessellation counts that ported
+    /// code writes; a render backend draws from the same state later.
+    /// </summary>
+    public class LineRenderer : Renderer
+    {
+        public bool useWorldSpace = true;
+        public float startWidth = 1f;
+        public float endWidth = 1f;
+        public Color startColor = Color.white;
+        public Color endColor = Color.white;
+        public int numCapVertices;
+        public int numCornerVertices;
+
+        Vector3[] _positions = System.Array.Empty<Vector3>();
+
+        /// <summary>
+        /// Number of line points. Resizing preserves existing points; new slots are
+        /// zero-initialized (original contract).
+        /// </summary>
+        public int positionCount
+        {
+            get => _positions.Length;
+            set
+            {
+                int count = value < 0 ? 0 : value;
```

</details>

### `35dc3a3b0` — feat(port): drift-sync 57/57 — re-verbatimed against the new bleeding-edge base

_Claude, 2026-07-02 02:57:51 +0000_

```text
45 files verbatim, 11 with carried deviations, 1 deleted upstream
(CinematicDefinitionSO — its enum moved into AICinematicBehavior).
Headliners: PrismAOERegistry renamed+rewritten upstream as
PrismSpatialIndex (1,003L: bucket occupancy grid, TryReserve
claim-before-spawn, QuerySphere/CopyLivePrisms, cell density binding);
Cell volume-is-the-spine (LiveVolume/OpposingVolume, Calm/Restless/Frenzy
phases); RoundStats B10 (n_Domain retired) + GoalsScored +
ClearEventSubscriptions; fauna senses served from the spatial index;
sealed Fauna.Die -> crystal drop; upstream's own revert of the menu trail
cap (mass conservation — the port's tests already encoded it).

New upstream deps ported: PrismColliderLodManager, EndConditionOverridesSO,
LifeFormCrystal, ElementalCrystalSetSO. Engine additions: Mathf.PerlinNoise,
layer-masked OverlapSphereNonAlloc, DisallowMultipleComponent;
Crystal.ActivateCrystal restored (render body deviation-marked). Two new
deviations: CrystalCollisionTurnMonitor keeps a pre-seeded target when no
EndConditionOverrides asset is registered; AnalyticsServiceFacade
deviation-commented in QuestSystem/PlayerDataService (UGS-coupled).

Tests updated to upstream behavior (reasons cited in-file): GameModes
freeze (Freestyle=7 retired, Tournament=36, AstroLeague=37), trail-cap
tests removed, CellTests volume-keyed, assembler/boid occupancy on the
spatial index, CellPhaseRules re-verbatimed. Per-line markers in
Port/docs/DRIFT_2026-06-13.txt.

Gate green both configs with toys integrated: 931 + 258. Headless
1200-frame fingerprint byte-identical (crystals [6,1,4,1], trail 786).
```

```text
 Port/src/CosmicShore.Game/Controller/Vessel/VesselPrismController.cs  |   30 -
 Port/src/CosmicShore.Game/Controller/Vessel/VesselStatus.cs           |   15 +
 Port/src/CosmicShore.Game/Controller/Vessel/VesselTransformer.cs      |   18 +-
 Port/src/CosmicShore.Game/ScriptableObjects/ElementalCrystalSetSO.cs  |   49 ++
 .../src/CosmicShore.Game/ScriptableObjects/EndConditionOverridesSO.cs |  131 ++++
 Port/src/CosmicShore.Game/ScriptableObjects/SO_ArcadeGame.cs          |    4 +
 Port/src/CosmicShore.Game/System/Audio/AudioCategories.cs             |    7 +
 Port/src/CosmicShore.Game/System/Quest/QuestSystem.cs                 |    4 +
 Port/src/CosmicShore.Game/UI/View/BoostChangedPayload.cs              |   12 +
 Port/src/CosmicShore.Game/UI/Views/PlayerDataService.cs               |   49 +-
 Port/src/CosmicShore.Game/UI/Views/PlayerProfileData.cs               |    4 +
 .../CosmicShore.Game/Utility/DataContainers/AICinematicBehavior.cs    |   16 +
 .../CosmicShore.Game/Utility/DataContainers/CellPhaseThresholds.cs    |  117 +++-
 .../CosmicShore.Game/Utility/DataContainers/CinematicDefinitionSO.cs  |   20 -
 Port/src/CosmicShore.Game/Utility/DataContainers/GameDataSO.cs        |   52 +-
 Port/src/CosmicShore.Game/Utility/GeometryUtils.cs                    |    5 +-
 Port/tests/CosmicShore.Tests.Ported/CellPhaseRulesTests.cs            |  155 +++--
 Port/tests/CosmicShore.Tests.Ported/EnumIntegrityTests.cs             |    7 +-
 Port/tests/CosmicShore.Tests/AssemblerFamilyTests.cs                  |   38 +-
 Port/tests/CosmicShore.Tests/BoidChainTests.cs                        |   18 +-
 Port/tests/CosmicShore.Tests/CellTests.cs                             |   36 +-
 Port/tests/CosmicShore.Tests/ClientConvergenceTests.cs                |    2 +-
 Port/tests/CosmicShore.Tests/EnumFreezeTests.cs                       |   13 +-
 Port/tests/CosmicShore.Tests/IRoundStatsCleanupTests.cs               |    4 +
 Port/tests/CosmicShore.Tests/PrismManagerTests.cs                     |    2 +-
 Port/tests/CosmicShore.Tests/PrismTests.cs                            |   24 +-
 Port/tests/CosmicShore.Tests/SkimmerLayerTests.cs                     |    2 +-
 Port/tests/CosmicShore.Tests/TrailLayerTests.cs                       |   24 +-
 Port/tests/CosmicShore.Tests/VesselActionLayerTests.cs                |   24 +-
 75 files changed, 3341 insertions(+), 1068 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 6504 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 9b437ce9d..71de9d563 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -648,7 +648,18 @@ convergence ladder, and every future bleeding-edge merge reopens them:
    and CreateInstance<T> without the non-original new() constraint. 21 tests.
    Client integration notes live in the toys agent report (ToyboxController needs
    _gameData/_freestyleEvents + freestyle transition events in the client scene).
-1. **DRIFT-SYNC** — 57 already-ported files changed upstream (full list:
+1. **DRIFT-SYNC ✅ (complete, iteration 21)** — 57/57 resolved (45 verbatim, 11
+   with carried deviations, 1 deleted-upstream). Headliners: PrismAOERegistry →
+   `PrismSpatialIndex` (upstream 1,003-line rewrite: bucket occupancy grid,
+   TryReserve, QuerySphere), Cell volume-is-the-spine (LiveVolume/OpposingVolume,
+   Calm/Restless/Frenzy), RoundStats B10 (n_Domain retired) + GoalsScored +
+   ClearEventSubscriptions, fauna index-served senses + sealed Die→crystal drop,
+   upstream's own revert of the menu trail cap. New deps ported:
+   PrismColliderLodManager, EndConditionOverridesSO, LifeFormCrystal,
+   ElementalCrystalSetSO. Engine: Mathf.PerlinNoise, layer-masked
+   OverlapSphereNonAlloc, DisallowMultipleComponent, Crystal.ActivateCrystal.
+   Per-line markers in Port/docs/DRIFT_2026-06-13.txt. Was: 57 already-ported
+   files changed upstream (full list:
    `Port/docs/DRIFT_2026-06-13.txt`). Ported copies must be re-verbatimed against
    the new merge-base: for each file apply `git diff 29b5f422..c833c580 -- <unity
    file>` onto the ported copy with the mechanical substitutions. Load-bearing
diff --git a/Port/docs/DRIFT_2026-06-13.txt b/Port/docs/DRIFT_2026-06-13.txt
index b22523f29..37b433fe6 100644
--- a/Port/docs/DRIFT_2026-06-13.txt
+++ b/Port/docs/DRIFT_2026-06-13.txt
@@ -1,57 +1,71 @@
-Assets/_Scripts/Controller/AI/AIPilot.cs -> Port/src/CosmicShore.Game/Controller/AI/AIPilot.cs
-Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs -> Port/src/CosmicShore.Game/Controller/Arcade/ElementalComebackSystem.cs
-Assets/_Scripts/Controller/Arcade/Scoring/ScoringMetrics.cs -> Port/src/CosmicShore.Game/Controller/Arcade/Scoring/ScoringMetrics.cs
-Assets/_Scripts/Controller/Arcade/TurnMonitorController.cs -> Port/src/CosmicShore.Game/Controller/Arcade/TurnMonitorController.cs
-Assets/_Scripts/Controller/Arcade/TurnMonitors/CrystalCollisionTurnMonitor.cs -> Port/src/CosmicShore.Game/Controller/Arcade/TurnMonitors/CrystalCollisionTurnMonitor.cs
-Assets/_Scripts/Controller/Arcade/TurnMonitors/NetworkCrystalCollisionTurnMonitor.cs -> Port/src/CosmicShore.Game/Controller/Arcade/TurnMonitors/NetworkCrystalCollisionTurnMonitor.cs
-Assets/_Scripts/Controller/Assemblers/GyroidAssembler.cs -> Port/src/CosmicShore.Game/Controller/Assemblers/GyroidAssembler.cs
-Assets/_Scripts/Controller/Assemblers/SchwarzPAssembler.cs -> Port/src/CosmicShore.Game/Controller/Assemblers/SchwarzPAssembler.cs
-Assets/_Scripts/Controller/Assemblers/WallAssembler.cs -> Port/src/CosmicShore.Game/Controller/Assemblers/WallAssembler.cs
-Assets/_Scripts/Controller/Camera/CustomCameraController.cs -> Port/src/CosmicShore.Game/Controller/Camera/CustomCameraController.cs
-Assets/_Scripts/Controller/Environment/Cell.cs -> Port/src/CosmicShore.Game/Controller/Environment/Cell.cs
-Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs -> Port/src/CosmicShore.Game/Controller/Environment/FloraAndFauna/AssembledFlora.cs
-Assets/_Scripts/Controller/Environment/FloraAndFauna/BodySegmentFauna.cs -> Port/src/CosmicShore.Game/Controller/Environment/FloraAndFauna/BodySegmentFauna.cs
-Assets/_Scripts/Controller/Environment/FloraAndFauna/Boid.cs -> Port/src/CosmicShore.Game/Controller/Environment/FloraAndFauna/Boid.cs
-Assets/_Scripts/Controller/Environment/FloraAndFauna/BranchingFlora.cs -> Port/src/CosmicShore.Game/Controller/Environment/FloraAndFauna/BranchingFlora.cs
-Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs -> Port/src/CosmicShore.Game/Controller/Environment/FloraAndFauna/Fauna.cs
-Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs -> Port/src/CosmicShore.Game/Controller/Environment/FloraAndFauna/LightFauna.cs
-Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFaunaDataSO.cs -> Port/src/CosmicShore.Game/Controller/Environment/FloraAndFauna/LightFaunaDataSO.cs
-Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFaunaManager.cs -> Port/src/CosmicShore.Game/Controller/Environment/FloraAndFauna/LightFaunaManager.cs
-Assets/_Scripts/Controller/Environment/HealthPrism.cs -> Port/src/CosmicShore.Game/Controller/Environment/HealthPrism.cs
-Assets/_Scripts/Controller/Environment/MiniGameObjects/SegmentSpawner.cs -> Port/src/CosmicShore.Game/Controller/Environment/MiniGameObjects/SegmentSpawner.cs
-Assets/_Scripts/Controller/Environment/Spawning/SpawnPoint.cs -> Port/src/CosmicShore.Game/Controller/Environment/Spawning/SpawnPoint.cs
-Assets/_Scripts/Controller/IO/DualMouseInputStrategy.cs -> Port/src/CosmicShore.Game/Controller/IO/DualMouseInputStrategy.cs
-Assets/_Scripts/Controller/IO/TouchInputStrategy.cs -> Port/src/CosmicShore.Game/Controller/IO/TouchInputStrategy.cs
-Assets/_Scripts/Controller/ImpactEffects/Impactors/VesselImpactor.cs -> Port/src/CosmicShore.Game/Controller/ImpactEffects/Impactors/VesselImpactor.cs
-Assets/_Scripts/Controller/Managers/BlockDensityGrid.cs -> Port/src/CosmicShore.Game/Controller/Managers/BlockDensityGrid.cs
-Assets/_Scripts/Controller/Managers/CameraManager.cs -> Port/src/CosmicShore.Game/Controller/Managers/CameraManager.cs
-Assets/_Scripts/Controller/Managers/PrismAOERegistry.cs -> Port/src/CosmicShore.Game/Controller/Managers/PrismAOERegistry.cs
-Assets/_Scripts/Controller/Managers/PrismStateManager.cs -> Port/src/CosmicShore.Game/Controller/Managers/PrismStateManager.cs
-Assets/_Scripts/Controller/Player/Player.cs -> Port/src/CosmicShore.Game/Controller/Player/Player.cs
-Assets/_Scripts/Controller/Vessel/Prism.cs -> Port/src/CosmicShore.Game/Controller/Vessel/Prism.cs
-Assets/_Scripts/Controller/Vessel/Trail.cs -> Port/src/CosmicShore.Game/Controller/Vessel/Trail.cs
-Assets/_Scripts/Controller/Vessel/VesselCustomization.cs -> Port/src/CosmicShore.Game/Controller/Vessel/VesselCustomization.cs
-Assets/_Scripts/Controller/Vessel/VesselHelper.cs -> Port/src/CosmicShore.Game/Controller/Vessel/VesselHelper.cs
-Assets/_Scripts/Controller/Vessel/VesselPrismController.cs -> Port/src/CosmicShore.Game/Controller/Vessel/VesselPrismController.cs
-Assets/_Scripts/Controller/Vessel/VesselStatus.cs -> Port/src/CosmicShore.Game/Controller/Vessel/VesselStatus.cs
-Assets/_Scripts/Controller/Vessel/VesselTransformer.cs -> Port/src/CosmicShore.Game/Controller/Vessel/VesselTransformer.cs
-Assets/_Scripts/Data/Enums/CellAggressionLevel.cs -> Port/src/CosmicShore.Data/Enums/CellAggressionLevel.cs
-Assets/_Scripts/Data/Enums/CellPhase.cs -> Port/src/CosmicShore.Data/Enums/CellPhase.cs
-Assets/_Scripts/Data/Enums/GameModes.cs -> Port/src/CosmicShore.Data/Enums/GameModes.cs
-Assets/_Scripts/Data/Enums/IRoundStats.cs -> Port/src/CosmicShore.Data/Enums/IRoundStats.cs
-Assets/_Scripts/Data/Enums/RoundStats.cs -> Port/src/CosmicShore.Data/Enums/RoundStats.cs
-Assets/_Scripts/Data/Enums/ScoringMetric.cs -> Port/src/CosmicShore.Data/Enums/ScoringMetric.cs
-Assets/_Scripts/ScriptableObjects/SO_ArcadeGame.cs -> Port/src/CosmicShore.Game/ScriptableObjects/SO_ArcadeGame.cs
-Assets/_Scripts/System/Audio/AudioSystem.cs -> Port/src/CosmicShore.Game/System/Audio/AudioSystem.cs
-Assets/_Scripts/System/Quest/QuestSystem.cs -> Port/src/CosmicShore.Game/System/Quest/QuestSystem.cs
-Assets/_Scripts/UI/View/BoostChangedPayload.cs -> Port/src/CosmicShore.Game/UI/View/BoostChangedPayload.cs
-Assets/_Scripts/UI/Views/PlayerDataService.cs -> Port/src/CosmicShore.Game/UI/Views/PlayerDataService.cs
-Assets/_Scripts/UI/Views/PlayerProfileData.cs -> Port/src/CosmicShore.Game/UI/Views/PlayerProfileData.cs
-Assets/_Scripts/Utility/DataContainers/AICinematicBehavior.cs -> Port/src/CosmicShore.Game/Utility/DataContainers/AICinematicBehavior.cs
-Assets/_Scripts/Utility/DataContainers/CellPhaseThresholds.cs -> Port/src/CosmicShore.Game/Utility/DataContainers/CellPhaseThresholds.cs
```

</details>

### `9256a7cde` — refactor(port): ShapeSign fully verbatim — TMP deviations closed via the TMPro shim

_Claude, 2026-07-02 03:11:50 +0000_

```text
The toys arc's data-only TMPro shim (CosmicShore.Engine.UI) unblocks
ShapeSign's two TMP_Text labels and ApplyDisplayData — the file is now
byte-verbatim modulo the sanctioned substitutions. Zero deviations remain
blocked on TMPro.

Gate green both configs: 931 + 258.
```

```text
 Port/src/CosmicShore.Game/Controller/Environment/MiniGameObjects/ShapeSign.cs | 11 +++++------
 1 file changed, 5 insertions(+), 6 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Port/src/CosmicShore.Game/Controller/Environment/MiniGameObjects/ShapeSign.cs b/Port/src/CosmicShore.Game/Controller/Environment/MiniGameObjects/ShapeSign.cs
index 714f36885..d3b97033b 100644
--- a/Port/src/CosmicShore.Game/Controller/Environment/MiniGameObjects/ShapeSign.cs
+++ b/Port/src/CosmicShore.Game/Controller/Environment/MiniGameObjects/ShapeSign.cs
@@ -1,5 +1,5 @@
 using CosmicShore.Data;
-// PORT Deviation (UI shell, restore when TMPro shim lands): using TMPro;
+using CosmicShore.Engine.UI;
 using CosmicShore.Engine;
 
 namespace CosmicShore.Gameplay
@@ -15,8 +15,8 @@ namespace CosmicShore.Gameplay
         [SerializeField] ShapeDefinition shapeDefinition;
 
         [Header("Display")]
-        // PORT Deviation (UI shell, restore when TMPro shim lands): [SerializeField] TMP_Text nameLabel;
-        // PORT Deviation (UI shell, restore when TMPro shim lands): [SerializeField] TMP_Text descriptionLabel;
+        [SerializeField] TMP_Text nameLabel;
+        [SerializeField] TMP_Text descriptionLabel;
 
         bool _triggered;
 
@@ -62,9 +62,8 @@ namespace CosmicShore.Gameplay
         void ApplyDisplayData()
         {
             if (shapeDefinition == null) return;
-            // PORT Deviation (UI shell, restore when TMPro shim lands):
-            // if (nameLabel)        nameLabel.text        = shapeDefinition.shapeName;
-            // if (descriptionLabel) descriptionLabel.text  = shapeDefinition.description;
+            if (nameLabel)        nameLabel.text        = shapeDefinition.shapeName;
+            if (descriptionLabel) descriptionLabel.text  = shapeDefinition.description;
         }
     }
 
```

</details>

### `6cb337ae5` — feat(port): real MiniGameControllerBase chain + AstroLeague — port-m3 milestone

_Claude, 2026-07-02 04:00:53 +0000_

```text
Arc 1: the real game-controller chain ported verbatim —
MiniGameControllerBase, MultiplayerMiniGameControllerBase (RPCs
local-invoke; scene-reload replay deviation-marked, everything in-scene
verbatim incl. replay AI despawns), MultiplayerDomainGamesController,
HexRaceController, CountdownTimer (timing-equivalent beat loop).

Arc 2: AstroLeague — the new bleeding-edge hypersea-soccer mode — ported
onto the chain: Controller, Ball (verbatim server rigidbody sim:
momentum-conserving vessel strikes, anti-clip ejection, spherical-boundary
radial reflect, per-tick PrismSpatialIndex scan, dead-reckoned
replication), Goal, Arena, MatchMonitor, ScoringRuleSO, SettingsSO,
ObjectiveProvider + IObjectiveProvider.

Engine (E17/E18): ballistic Rigidbody dynamics integrated per fixed step,
FixedString32Bytes, NetworkManager.ConnectedClientsIds,
ISession.Deleted/PlayerLeaving, FindAnyObjectByType, data-only
PhysicsMaterial/Light/BlendMode/TrailRenderer surfaces.

CLI: --mode astroleague drives a full match through the real chain
(ready -> countdown -> kickoff -> strikes -> goals -> mercy/full-time/
golden-goal -> ranked results), deterministic per seed; hexrace round
unchanged (winner AI-2 Ruby 70.53s).

Tests: +14 (MiniGameControllerChainTests + AstroLeagueTests). Gate green
both configs: 945 + 258 = 1203. Headless client fingerprint byte-identical
(crystals [6,1,4,1], trail 786).
```

```text
 Port/src/CosmicShore.Cli/AstroLeagueRound.cs                          |  665 +++++++++++++++++++
 Port/src/CosmicShore.Cli/Program.cs                                   |   44 +-
 Port/src/CosmicShore.Engine/Collections/FixedString32Bytes.cs         |   54 ++
 Port/src/CosmicShore.Engine/Compat/EngineCompat.cs                    |  159 ++++-
 Port/src/CosmicShore.Engine/Networking/ISession.cs                    |    9 +
 Port/src/CosmicShore.Engine/Networking/NetworkManager.cs              |    9 +
 Port/src/CosmicShore.Engine/Object.cs                                 |    7 +
 Port/src/CosmicShore.Engine/Rendering/BlendMode.cs                    |   22 +
 Port/src/CosmicShore.Engine/Rendering/Material.cs                     |   13 +
 Port/src/CosmicShore.Engine/Rendering/Renderers.cs                    |   40 ++
 Port/src/CosmicShore.Engine/SceneGraph/GameLoop.cs                    |   36 +-
 Port/src/CosmicShore.Engine/SceneGraph/GameObject.cs                  |    2 +
 .../Controller/Arcade/AstroLeague/AstroLeagueArena.cs                 |  212 ++++++
 .../CosmicShore.Game/Controller/Arcade/AstroLeague/AstroLeagueBall.cs | 1083 +++++++++++++++++++++++++++++++
 .../Controller/Arcade/AstroLeague/AstroLeagueController.cs            |  671 +++++++++++++++++++
 .../CosmicShore.Game/Controller/Arcade/AstroLeague/AstroLeagueGoal.cs |  111 ++++
 .../Controller/Arcade/AstroLeague/AstroLeagueMatchMonitor.cs          |  100 +++
 .../Controller/Arcade/AstroLeague/AstroLeagueScoringRuleSO.cs         |   77 +++
 .../Controller/Arcade/AstroLeague/AstroLeagueSettingsSO.cs            |  193 ++++++
 .../Controller/Arcade/AstroLeagueObjectiveProvider.cs                 |   33 +
 Port/src/CosmicShore.Game/Controller/Arcade/CountdownTimer.cs         |   96 +++
 Port/src/CosmicShore.Game/Controller/Arcade/HexRaceController.cs      |  336 ++++++++++
 Port/src/CosmicShore.Game/Controller/Arcade/MiniGameControllerBase.cs |  128 ++++
 .../Controller/Arcade/MultiplayerDomainGamesController.cs             |  211 ++++++
 .../Controller/Arcade/MultiplayerMiniGameControllerBase.cs            |  470 ++++++++++++++
 Port/src/CosmicShore.Game/Controller/Managers/CameraManager.cs        |    7 +
 Port/src/CosmicShore.Game/UI/Interfaces/IObjectiveProvider.cs         |   22 +
 Port/tests/CosmicShore.Tests/AstroLeagueTests.cs                      |  367 +++++++++++
 Port/tests/CosmicShore.Tests/MiniGameControllerChainTests.cs          |  310 +++++++++
 30 files changed, 5522 insertions(+), 12 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 5832 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 71de9d563..3850960f7 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -318,6 +318,8 @@ Next: V8 (VesselTransformer + member restore), rival balance from prompter feedb
 | 11 | `AudioSystem` type-preserving shell (`Instance`, two `PlayGameplaySFX` overloads; bodies no-op) — pulled forward from V15 to V6 because `ActionExecutorRegistry` needs the type. Real port with the phase-5 audio backend. | Keeps ActionExecutorRegistry verbatim. |
 | 14-ext | `PlayerDataService` (C2) stages its UGS CloudSave surface exactly like GameSetting's #14: `UGSDataService` injection, repo read/write, ready-event hooks commented with `PORT Deviation #14 (C2, restore when UGSDataService ports)` markers (7 sites); local default profile, crystal/XP math, reward unlocks, and the OnProfileChanged → GameDataSO sync are live. | Services phase owns the restore. |
 | 16 | `Directory.Build.props` adds CS0169 to NoWarn (alongside CS0649): verbatim Unity-era private fields whose only usages are commented (e.g. `InputController.vessel` until its orientation block revives) or inspector-driven fire it; the Unity compiler tolerated them. | Verbatim fields without warning noise. |
+| 17 | **Controller-chain arc scene deviations** (all marked `PORT Deviation (scene arc, …)` / `(UI shell, …)`): `MultiplayerMiniGameControllerBase` — the `SceneTransitionManager` fade field + its 2 call sites and the `nm.SceneManager.LoadScene` replay reload block are commented (no scene manager yet; everything in-scene — turn/round state machine, replay AI despawns, config sync — verbatim). `CountdownTimer` — the DOTween/Image/Sprite/beep presentation is replaced by a timing-equivalent GameTask beat loop (4 × countdownDuration unscaled → onComplete; `_seq?.Kill()` → CTS cancel parity). `CameraManager` shell grew a no-op `SnapPlayerCameraToTarget()` (Deviation #12 surface). Restore with the scene-management / UI arcs. | Smallest surface for the scene/UI gaps; the round/turn/score flow is verbatim. |
+| 18 | **AstroLeague arc**: engine gains E18 ballistic Rigidbody dynamics (linear/angular velocity + damping + `AddTorque` on a unit inertia tensor, integrated once per fixed step after the FixedUpdate phase; gravity not simulated — the HyperSea has none), data-only `PhysicsMaterial`/`Light`/`BlendMode`/material keywords+renderQueue, `FixedString32Bytes`, `NetworkManager.ConnectedClientsIds`, ISession `Deleted`/`PlayerLeaving` events, `FindAnyObjectByType`. `AstroLeagueBall` deviations (all presentation, marked): icosphere mesh swap (mesh arc), ParticleSystem aura/burst rig + haptics (presentation arc); the engine dispatches no `OnCollisionEnter/Stay`/`OnTriggerStay`, so the hull-collider strike path is carried as commented source and vessel contacts flow through the verbatim `OnTriggerEnter` path (the original's trigger-only-ship route — Serpent/Sparrow). `AstroLeagueArena`'s editor-only `OnDrawGizmos` body commented (no Gizmos). | Physics core verbatim on E18; the solver-dependent + render-side pieces restore with their phases. |
 
 ## Iteration log
 
@@ -669,8 +671,10 @@ convergence ladder, and every future bleeding-edge merge reopens them:
    - **Toys** (`Controller/Toys` 11 files + `ScriptableObjects/Toys` 5): freestyle
      toy system (domain/vessel changer flip-sets, mini ship models, painting toy) —
      player-facing in the lava-lamp/freestyle flow.
-   - **AstroLeague** (`Controller/Arcade/AstroLeague/` 7 files + ASTROLEAGUE.md):
-     new ball-game arcade mode (arena, ball, goal, match monitor, scoring rule).
+   - **AstroLeague ✅ (ported, controller-chain iteration)** (`Controller/Arcade/AstroLeague/`
+     7 files + `AstroLeagueObjectiveProvider` + `IObjectiveProvider`): the full mode runs
+     headless through the real chain — `dotnet run --project src/CosmicShore.Cli -- --mode
+     astroleague [--players N] [--seed S]`. See Deviation #18 + the iteration log.
    - **Tournament** (`Controller/Arcade/Tournament/` 4 files + TournamentSystem
      docs + UI cards): bracket play across arcade games.
    - SandboxBenchmarkController, Settings additions, CloudData, Privacy UI.
@@ -693,6 +697,38 @@ client + AstroLeague headless round running + remaining ladder rungs (5: real lo
    AIPilot-driven boost intent when balance work resumes.
 4. Update this file, commit, push.
 
+## Controller-chain + AstroLeague arc (landed after iteration 21 — dedicated agent arc)
+
+The real game-controller chain is IN (the arc flagged by the SegmentSpawner note
+below): `MiniGameControllerBase` → `MultiplayerMiniGameControllerBase` →
+`MultiplayerDomainGamesController` → `HexRaceController` ported verbatim to
+`src/CosmicShore.Game/Controller/Arcade/` (RPCs local-invoke per the Player/RoundStats
+precedent; GameTask mappings; scene-reload/fade surfaces deviation-marked — Deviation
+#17), plus `CountdownTimer` (timing-equivalent headless beat loop). **AstroLeague** is
+IN on top of it (all 7 `AstroLeague/` files + `AstroLeagueObjectiveProvider` +
+`UI/Interfaces/IObjectiveProvider`; ball physics verbatim on new engine E18 rigidbody
+dynamics; presentation deviations marked — Deviation #18). New headless CLI round:
+`--mode astroleague` (`src/CosmicShore.Cli/AstroLeagueRound.cs`) drives the WHOLE match
+through the real chain — ready → 3-2-1 countdown → kickoff parking → trigger-pass
+vessel strikes → goal-plane detection → RoundStats.GoalsScored → celebration/kickoff
+loops → mercy / full-time / golden-goal → `SyncFinalScores` → ranked
+`GameDataSO.Results` — deterministic per seed, exit 0 (seed sweep 1/2/3/7/42/99/2026
+and players 2/4/6 all PASS at the harness tuning: `settings.maxSpeed=100→35` for
+AI-catchable play, mouth 60, boundary 170). Engine additions: E18 Rigidbody
+integration step in `GameLoop.RunFixedSteps`, `PhysicsMaterial`, `Light`, `BlendMode`,
+material keywords/renderQueue, `FixedString32Bytes`,
+`NetworkManager.ConnectedClientsIds`, ISession events, `FindAnyObjectByType`.
+Tests: `MiniGameControllerChainTests` (template-method rounds→turns→end via
+GameDataSO turn events; countdown beats; HexRace domain-aggregated winner + golf
+sentinels + ranked Results + race-over latch) and `AstroLeagueTests` (scoring-rule
+mercy/points/results/reveal; match-monitor clock/pause/OT/ForceEnd; ball seeded
+bit-identical trace + boundary containment; full-match integration incl. the
+golden-goal path and seed purity). **1203 tests green in BOTH configs (945 + 258)**;
+hexrace CLI + client screenshot smoke byte-identical to the pre-arc baseline
+(`frame 300, crystals [4,0,1,1] … trail 126`). Remaining chain siblings for a later
+arc: SinglePlayer*, Joust/CellularDuel/CrystalCapture/Freestyle/WildlifeBlitz
+controllers, Tournament.
+
 Note (test config): `CSDebug.Log/LogFormat` are `[Conditional("DEBUG")]` — info
 logs strip out of Release. DebugExtensionsTests asserts per-config (`#if DEBUG`).
 Gate BOTH configs when touching logging paths.
@@ -703,9 +739,10 @@ per segment, intensity-scaled spacing). One deviation: the diagnostic
 super-shield block restores when `PrismStellatedOctahedronShield` ports with the
 engine Mesh/MeshFilter arc (468L shield + 270L StellatedOctahedronMeshGenerator —
 flagged as the shield arc). Engine gains the `Transform.Rotate(axis, angle)`
-overload. This unblocks the real `HexRaceController` chain
-(MiniGameControllerBase → Multiplayer → DomainGames → HexRace, 1,112L) — the
-next dedicated agent arc for replacing SkimRaceDirector with the real controller.
+overload. This unblocked the real `HexRaceController` chain
+(MiniGameControllerBase → Multiplayer → DomainGames → HexRace, 1,112L) — **DONE**,
+see "Controller-chain + AstroLeague arc" above; replacing SkimRaceDirector with the
+real controller in the CLIENT remains a convergence-ladder follow-up.
 
 Quest/action systems (landed alongside iteration 19, part 2): `QuestSystem` +
 `UserActionSystem` + `CallToActionSystem` ported verbatim — the full
diff --git a/Port/src/CosmicShore.Cli/AstroLeagueRound.cs b/Port/src/CosmicShore.Cli/AstroLeagueRound.cs
```

</details>

### `052c2a173` — feat(port): Tournament (Maelstrom) system — session meta through the real fold

_Claude, 2026-07-02 04:31:29 +0000_

```text
TournamentController (persistent brain: scene-load phases, network-free
standings fold on OnMiniGameEnd, host random draw with repeat avoidance +
intensity ceiling, authoritative IsShuffleComplete summary decision),
TournamentStateMachine, TournamentLobbyNetwork (ready-up/countdown,
local-invoke RPCs), TournamentSceneView (phase/button/text verbatim;
DOTween/card-population presentation deviation-marked), TournamentDataSO
(the {2,1,0} per-domain fold, race-to-N, BuildSortedStandings) +
StandingsFormatter + DomainColorPaletteSO — verbatim modulo substitutions.

Engine: CosmicShore.Engine.SceneManagement (LoadSceneMode + sceneLoaded
event + NotifySceneLoaded port surface), UI Button onClick shim, Netcode
2.x [Rpc(SendTo.…)] + RpcParams metadata.

CLI: --mode tournament chains 4 legs through the real draw/fold/summary
path (legs simulated by the headless HexRace harness until Joust/Crystal
Capture controllers port — stated in-transcript). Byte-identical per seed.

+35 tests (17 TournamentSystemTests + 18 ported TournamentDataSOTests).
Gate green both configs: 962 + 276 = 1238. All CLI modes exit 0; client
diag unchanged.
```

```text
 Port/PORT_PLAN.md                                                     |  44 ++-
 Port/src/CosmicShore.Cli/Program.cs                                   |  45 ++-
 Port/src/CosmicShore.Cli/TournamentRound.cs                           | 187 +++++++++++
 Port/src/CosmicShore.Engine/Compat/EngineCompat.cs                    |  42 +++
 Port/src/CosmicShore.Engine/SceneGraph/SceneManager.cs                |  48 +++
 Port/src/CosmicShore.Engine/UI/Button.cs                              |  17 +
 .../Controller/Arcade/Tournament/TournamentController.cs              | 380 +++++++++++++++++++++
 .../Controller/Arcade/Tournament/TournamentLobbyNetwork.cs            | 151 +++++++++
 .../Controller/Arcade/Tournament/TournamentSceneView.cs               | 491 +++++++++++++++++++++++++++
 .../Controller/Arcade/Tournament/TournamentStateMachine.cs            |  82 +++++
 Port/src/CosmicShore.Game/Controller/Vessel/DomainColorPaletteSO.cs   |  33 ++
 .../Utility/DataContainers/Tournament/TournamentDataSO.cs             | 461 +++++++++++++++++++++++++
 .../Utility/DataContainers/Tournament/TournamentStandingsFormatter.cs | 108 ++++++
 Port/tests/CosmicShore.Tests.Ported/TournamentDataSOTests.cs          | 319 ++++++++++++++++++
 Port/tests/CosmicShore.Tests/TournamentSystemTests.cs                 | 576 ++++++++++++++++++++++++++++++++
 15 files changed, 2980 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 3113 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 3850960f7..ca130d9e9 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -320,6 +320,7 @@ Next: V8 (VesselTransformer + member restore), rival balance from prompter feedb
 | 16 | `Directory.Build.props` adds CS0169 to NoWarn (alongside CS0649): verbatim Unity-era private fields whose only usages are commented (e.g. `InputController.vessel` until its orientation block revives) or inspector-driven fire it; the Unity compiler tolerated them. | Verbatim fields without warning noise. |
 | 17 | **Controller-chain arc scene deviations** (all marked `PORT Deviation (scene arc, …)` / `(UI shell, …)`): `MultiplayerMiniGameControllerBase` — the `SceneTransitionManager` fade field + its 2 call sites and the `nm.SceneManager.LoadScene` replay reload block are commented (no scene manager yet; everything in-scene — turn/round state machine, replay AI despawns, config sync — verbatim). `CountdownTimer` — the DOTween/Image/Sprite/beep presentation is replaced by a timing-equivalent GameTask beat loop (4 × countdownDuration unscaled → onComplete; `_seq?.Kill()` → CTS cancel parity). `CameraManager` shell grew a no-op `SnapPlayerCameraToTarget()` (Deviation #12 surface). Restore with the scene-management / UI arcs. | Smallest surface for the scene/UI gaps; the round/turn/score flow is verbatim. |
 | 18 | **AstroLeague arc**: engine gains E18 ballistic Rigidbody dynamics (linear/angular velocity + damping + `AddTorque` on a unit inertia tensor, integrated once per fixed step after the FixedUpdate phase; gravity not simulated — the HyperSea has none), data-only `PhysicsMaterial`/`Light`/`BlendMode`/material keywords+renderQueue, `FixedString32Bytes`, `NetworkManager.ConnectedClientsIds`, ISession `Deleted`/`PlayerLeaving` events, `FindAnyObjectByType`. `AstroLeagueBall` deviations (all presentation, marked): icosphere mesh swap (mesh arc), ParticleSystem aura/burst rig + haptics (presentation arc); the engine dispatches no `OnCollisionEnter/Stay`/`OnTriggerStay`, so the hull-collider strike path is carried as commented source and vessel contacts flow through the verbatim `OnTriggerEnter` path (the original's trigger-only-ship route — Serpent/Sparrow). `AstroLeagueArena`'s editor-only `OnDrawGizmos` body commented (no Gizmos). | Physics core verbatim on E18; the solver-dependent + render-side pieces restore with their phases. |
+| 19 | **Tournament (Maelstrom) arc**: engine gains the `CosmicShore.Engine.SceneManagement` surface (`SceneManager.sceneLoaded` + `LoadSceneMode`; loads are announced by harnesses via `NotifySceneLoaded` until real scene management lands — the controller's subscription + `HandleSceneLoaded` are verbatim), Netcode 2.x `[Rpc(SendTo.…)]`/`RpcParams` metadata (local-invoke), and the headless `Engine.UI.Button` shim. `TournamentSceneView` deviations (all presentation, marked): DOTween pulse/typewriter bodies no-op; round/summary card population commented (TournamentRoundCard / TournamentSummaryPlayerCard / TournamentPlayerCard / TournamentDomainScoreView are Image/CanvasGroup/DOTween prefab views — unported, restore with the UI arc); ScrollRect auto-scroll + LayoutRebuilder; SO_AIProfileList avatar branch; `NetworkManager.SpawnManager`/`LocalClient` roster/local-domain fallbacks. CLI `--mode tournament` legs are simulated by the headless `HexRaceRound` regardless of the drawn mode until the Joust / Crystal Capture controllers port (stated in the transcript). | Meta logic (fold, race-to-6, draw, phases) verbatim + tested; render-side pieces restore with their phases. |
 
 ## Iteration log
 
@@ -675,8 +676,13 @@ convergence ladder, and every future bleeding-edge merge reopens them:
      7 files + `AstroLeagueObjectiveProvider` + `IObjectiveProvider`): the full mode runs
      headless through the real chain — `dotnet run --project src/CosmicShore.Cli -- --mode
      astroleague [--players N] [--seed S]`. See Deviation #18 + the iteration log.
-   - **Tournament** (`Controller/Arcade/Tournament/` 4 files + TournamentSystem
-     docs + UI cards): bracket play across arcade games.
+   - **Tournament ✅ (ported, tournament arc)** (`Controller/Arcade/Tournament/` 4 files +
+     `TournamentDataSO` + `TournamentStandingsFormatter` + `DomainColorPaletteSO`): the
+     Maelstrom session meta runs headless through the real chain — `dotnet run --project
+     src/CosmicShore.Cli -- --mode tournament [--players N] [--seed S]`. See Deviation #19 +
+     the iteration log. Still open in this lane: TournamentRoundCard / TournamentPlayerCard /
+     TournamentSummaryPlayerCard / TournamentDomainScoreView (UI card prefab views) +
+     Scoreboard's tournament wallet credit.
    - SandboxBenchmarkController, Settings additions, CloudData, Privacy UI.
 
 Gap-closure definition for this /loop: drift-sync complete + toys playable in the
@@ -727,7 +733,39 @@ golden-goal path and seed purity). **1203 tests green in BOTH configs (945 + 258
 hexrace CLI + client screenshot smoke byte-identical to the pre-arc baseline
 (`frame 300, crystals [4,0,1,1] … trail 126`). Remaining chain siblings for a later
 arc: SinglePlayer*, Joust/CellularDuel/CrystalCapture/Freestyle/WildlifeBlitz
-controllers, Tournament.
+controllers (Tournament landed — see the Tournament arc below).
+
+## Tournament (Maelstrom) arc (landed after the controller-chain arc — dedicated agent arc)
+
+The session-level meta chaining the domain minigames is IN: all four
+`Controller/Arcade/Tournament/` files (`TournamentController` — the persistent
+network-free brain, `TournamentStateMachine`, `TournamentLobbyNetwork`,
+`TournamentSceneView`) + `Utility/DataContainers/Tournament/` (`TournamentDataSO`,
+`TournamentStandingsFormatter`) + `DomainColorPaletteSO` ported (fold/race-to-6/
+draw/phase logic verbatim; deviations in #19). Engine additions:
+`CosmicShore.Engine.SceneManagement` (`LoadSceneMode` + static `SceneManager` with
+the original `sceneLoaded` event; port surface `NotifySceneLoaded` /
+`ResetSceneLoadedSubscribers` until real scene transitions land), Netcode 2.x
+universal-RPC metadata (`RpcAttribute(SendTo)` / `RpcParams.Receive.SenderClientId` —
+local-invoke), and a headless `Engine.UI.Button` shim (onClick UnityEvent) beside
+the TMPro shim. New headless CLI session: `--mode tournament`
+(`src/CosmicShore.Cli/TournamentRound.cs`) drives lobby → host random draw
+(mode + intensity ∈ [1..ceiling], no immediate repeat) → headless leg → per-domain
+{2,1,0} standings fold from the synced `Results` → hub → … → race-to-6 / cap-7 →
+summary via `FormatFinal` — deterministic per seed (repeat-run transcripts
+byte-identical), exit 0. Every leg is simulated by the real headless
+`HexRaceRound` until the Joust / Crystal Capture controllers port (the drawn mode
+still exercises the real draw/repeat-avoidance path). Tests:
+`TournamentSystemTests` (17 — state-machine table incl. the Lobby→Complete
+race-to-6 route, fresh-start/ceiling capture, menu-return teardown, 3-game fold +
+cross-peer determinism from identical synced Results, race-to-6 hub/summary
+routing, game cap, the authoritative phase-independent summary decision at the
+Maelstrom load, Play-Again reset keeping the ceiling, splash-dwell window, seeded
+draw determinism + repeat avoidance, formatter (You)-tag/ordering, lobby-network
+arm/all-ready-snap/one-shot BeginNextRound) + `TournamentDataSOTests` ported
+verbatim into Tests.Ported (18 — the Unity edit-mode fold suite). **1238 tests
+green in BOTH configs (962 + 276)**; hexrace/astroleague/smoke CLI modes still
+exit 0.
 
 Note (test config): `CSDebug.Log/LogFormat` are `[Conditional("DEBUG")]` — info
 logs strip out of Release. DebugExtensionsTests asserts per-config (`#if DEBUG`).
diff --git a/Port/src/CosmicShore.Cli/Program.cs b/Port/src/CosmicShore.Cli/Program.cs
index eebe30727..6c257400a 100644
--- a/Port/src/CosmicShore.Cli/Program.cs
+++ b/Port/src/CosmicShore.Cli/Program.cs
@@ -60,6 +60,7 @@ namespace CosmicShore.Cli
                 {
                     "hexrace" => RunHexRaceMode(players, seed),
                     "astroleague" => RunAstroLeagueMode(players, seed),
+                    "tournament" => RunTournamentMode(players, seed),
                     _ => UnknownMode(mode),
                 };
```

</details>

### `fadc824fa` — feat(port): vessel-initializer (menu-swap) chain — 10 toy deviations closed

_Claude, 2026-07-02 04:52:28 +0000_

```text
ServerPlayerVesselInitializer + ClientPlayerVesselInitializer +
MenuServerPlayerVesselInitializer + NetcodeHooks — all verbatim (RPCs
local-invoke; GameTask mappings per README). RequestSwap works headless
end-to-end: despawn old vessel, spawn new class, ReInitializePair,
restore freestyle control.

Engine: NetworkObject grew from an id handle into the original-contract
COMPONENT — authored on prefab roots, Spawn/SpawnWithOwnership/Despawn
fan out to all NetworkBehaviours on object+children under ONE shared
object id (load-bearing: the vessel prefab's behaviour order broke the
NetVesselId round-trip under per-behaviour ids; direct behaviour.Spawn()
keeps prior semantics). NetworkManager gains LocalClientId,
ConnectedClients(List), SpawnManager; RpcParams family added.

All 10 'restore when MenuServerPlayerVesselInitializer ports' markers in
ToyContext/ToyboxController/VesselChangerToySet RESTORED (diff-verified
verbatim). ToySystemTests extended with the vessel-changer END-TO-END
test: fly into toy -> RequestSwap -> despawn/spawn -> flip-set shows the
previous class -> autopilot off, input unpaused. Zero new deviations.

+9 tests. Gate green both configs: 971 + 276 = 1247. Tournament/hexrace
CLI modes exit 0; client diag byte-identical.
```

```text
 Port/PORT_PLAN.md                                                     |  44 ++-
 Port/src/CosmicShore.Engine/Networking/NetworkBehaviour.cs            |  34 ++-
 Port/src/CosmicShore.Engine/Networking/NetworkManager.cs              |  68 +++++
 Port/src/CosmicShore.Engine/Networking/NetworkObject.cs               | 123 +++++++--
 Port/src/CosmicShore.Engine/Networking/RpcParams.cs                   |  32 +++
 .../Controller/Multiplayer/ClientPlayerVesselInitializer.cs           | 422 ++++++++++++++++++++++++++++
 .../Controller/Multiplayer/MenuServerPlayerVesselInitializer.cs       | 240 ++++++++++++++++
 .../Controller/Multiplayer/ServerPlayerVesselInitializer.cs           | 475 ++++++++++++++++++++++++++++++++
 Port/src/CosmicShore.Game/Controller/Toys/ToyContext.cs               |   5 +-
 Port/src/CosmicShore.Game/Controller/Toys/ToyboxController.cs         |   6 +-
 Port/src/CosmicShore.Game/Controller/Toys/VesselChangerToySet.cs      |  12 +-
 Port/src/CosmicShore.Game/Utility/Network/NetcodeHooks.cs             |  28 ++
 Port/tests/CosmicShore.Tests/ToySystemTests.cs                        | 133 ++++++++-
 Port/tests/CosmicShore.Tests/VesselInitializerChainTests.cs           | 388 ++++++++++++++++++++++++++
 14 files changed, 1968 insertions(+), 42 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 2220 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index ca130d9e9..874b82e3c 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -643,9 +643,11 @@ bleeding-edge merged INTO this branch at 842c825c (325 commits, merge-base moved
 29b5f422 → c833c580). The port now chases a LIVE game. Two standing lanes join the
 convergence ladder, and every future bleeding-edge merge reopens them:
 
-0. **TOYS ✅ (ported, iteration 21)** — all 11 Controller/Toys + 5 SO files verbatim
-   (menu-swap arc + mesh arc + UI-shell deviations marked; domain changer works
-   end-to-end through the real RequestSetDomain RPC today). Engine gains
+0. **TOYS ✅ (ported, iteration 21; menu-swap deviations CLOSED by the
+   vessel-initializer arc — see below)** — all 11 Controller/Toys + 5 SO files
+   verbatim (mesh arc + UI-shell deviations still marked; domain changer AND
+   vessel changer now work end-to-end — RequestSetDomain RPC + the real
+   MenuServerPlayerVesselInitializer.RequestSwap). Engine gains
    LineRenderer, GameObject.CreatePrimitive, ShadowCastingMode, the TMPro
    data-shim (`using TMPro;` → `using CosmicShore.Engine.UI;` — README updated),
    and CreateInstance<T> without the non-original new() constraint. 21 tests.
@@ -703,6 +705,42 @@ client + AstroLeague headless round running + remaining ladder rungs (5: real lo
    AIPilot-driven boost intent when balance work resumes.
 4. Update this file, commit, push.
 
+## Vessel-initializer (menu-swap) arc (landed after the controller-chain arc — dedicated agent arc)
+
+The networked player→vessel spawn/swap chain is IN: `ServerPlayerVesselInitializer`
++ `ClientPlayerVesselInitializer` + `MenuServerPlayerVesselInitializer` ported
+verbatim to `src/CosmicShore.Game/Controller/Multiplayer/` (GameTask mappings;
+RPCs local-invoke per the Player/RoundStats precedent — targeted ClientRpc fan-outs
+iterate the empty `ConnectedClientsList` in single-process host-mode; no UGS
+surface in these files, so no services-phase deviations), plus `NetcodeHooks`
+(`Utility/Network/`, verbatim). Engine grew the original-contract **NetworkObject
+component** (authored on prefab roots; `SpawnWithOwnership`/`Spawn`/`Despawn` fan
+out to every NetworkBehaviour on the object+children, all sharing ONE object id —
+`NetworkBehaviour.SpawnWithId` — so id round-trips like `NetVesselId` →
+`TryGetVesselByNetworkObjectId` resolve regardless of component order; the E12
+per-behaviour handle property now lazily resolves/adds the component, same
+Despawn(destroy) contract), `NetworkManager.LocalClientId` / `ConnectedClients` /
+`ConnectedClientsList` / `SpawnManager` (+ `NetworkClient`, `NetworkSpawnManager`),
+and the RPC params structs (`ClientRpcParams`/`ClientRpcSendParams`/
+`ServerRpcParams`/`ServerRpcReceiveParams`). **All 10 toy menu-swap deviations
+restored** (ToyContext.VesselInitializer, ToyboxController context wiring,
+VesselChangerToySet.Apply → `RequestSwap` + swap-wait loop): the vessel-changer
+toy works end-to-end headless — fly into the toy → RequestSwap → despawn/spawn/
+ReInitializePair → toy flips to the class you left → freestyle control restored.
+Tests: `VesselInitializerChainTests` (NetworkObject component contract; host
+spawn chain incl. server-side menu domain reset + autopilot; RequestSwap swap +
+pose snapshot + registry rewire; same-class no-op) and
+`ToySystemTests.VesselChangerEndToEndTests` (the full toy→swap→restore loop on
+the real chain). Test-rig note: calling `RequestSwap` (or anything that reaches
+`VesselPrismController.StartSpawn`) DIRECTLY from test code starts the spawn-loop
+async-void on xunit's AsyncTestSyncContext — stop the loops before the test
+method returns (the C4/C6 trap; the runner waits ahead of Dispose). **1212 tests
+green in BOTH configs (954 + 258)**; client screenshot smoke byte-identical to
+the pre-arc baseline (`frame 300, crystals [4,0,1,1] … trail 126`). Remaining for
+a later arc: `ServerPlayerVesselInitializerWithAI` (game-scene AI backfill),
+`MenuCrystalClickHandler`, `MultiplayerSetup`/`DomainAssigner`, and wiring the
+menu-swap chain into the playable client scene.
+
 ## Controller-chain + AstroLeague arc (landed after iteration 21 — dedicated agent arc)
 
 The real game-controller chain is IN (the arc flagged by the SegmentSpawner note
diff --git a/Port/src/CosmicShore.Engine/Networking/NetworkBehaviour.cs b/Port/src/CosmicShore.Engine/Networking/NetworkBehaviour.cs
index 3dc2eb143..1e31ddacf 100644
--- a/Port/src/CosmicShore.Engine/Networking/NetworkBehaviour.cs
+++ b/Port/src/CosmicShore.Engine/Networking/NetworkBehaviour.cs
@@ -28,8 +28,25 @@ namespace CosmicShore.Engine.Networking
         /// </summary>
         public ulong NetworkObjectId { get; private set; }
 
-        /// <summary>Handle exposing <c>Despawn(bool destroy)</c> + the id (E12).</summary>
-        public NetworkObject NetworkObject => _networkObject ??= new NetworkObject(this);
+        /// <summary>
+        /// The object-level <see cref="Networking.NetworkObject"/> component (E12 handle →
+        /// full component in the vessel-initializer arc): resolves the authored NetworkObject
+        /// on this GameObject, lazily adding one for behaviours spawned directly — preserving
+        /// the handle contract (<c>NetworkObject.Despawn(true)</c> despawns + destroys).
+        /// </summary>
+        public NetworkObject NetworkObject
+        {
```

</details>

### `508b894bb` — feat(port): Joust + Crystal Capture modes — Tournament chains all-real legs

_Claude, 2026-07-02 15:58:12 +0000_

```text
MultiplayerJoustController (golf scoring, HasEndGame=false, local-invoke
SyncJoustResults) + MultiplayerCrystalCaptureController (points scoring)
+ Joust/NetworkJoustCollisionTurnMonitor (B15 subscribed-stats
unsubscribe pattern, anti-recursion SyncCollision, server-validated
ReportCollision, EndConditionOverridesSO targets) + Joust/CrystalCapture
ScoringRuleSOs + VesselExplosionBySkimmerEffectSO (the joust trigger:
speed check -> opponent-domain check -> cooldown -> OnJoustCollision ->
GameFeedAPI.PostJoust; one V19 deviation for the AOE visual, matching
precedent) — all verbatim modulo substitutions.

CLI: --mode joust and --mode crystalcapture rounds; TournamentRound now
dispatches every drawn leg to its REAL harness — --mode tournament
chains genuine Crystal Capture / Joust / Skim Race legs off the shared
seeded RNG (Ruby takes the Maelstrom 7 pts at seed 42). All modes
byte-identical on repeat runs.

+12 tests (7 JoustTests, 5 CrystalCaptureTests; one Debug-only fixture
fix — CSDebug.Log argument evaluation that Release strips). Gate green
both configs: 983 + 276 = 1259. All 5 CLI modes exit 0; client diag
byte-identical.
```

```text
 Port/src/CosmicShore.Cli/CrystalCaptureRound.cs                       | 701 ++++++++++++++++++++++++++++++++
 Port/src/CosmicShore.Cli/JoustRound.cs                                | 623 ++++++++++++++++++++++++++++
 Port/src/CosmicShore.Cli/Program.cs                                   |  84 +++-
 Port/src/CosmicShore.Cli/TournamentRound.cs                           | 120 +++++-
 .../Controller/Arcade/MultiplayerCrystalCaptureController.cs          | 163 ++++++++
 .../CosmicShore.Game/Controller/Arcade/MultiplayerJoustController.cs  | 175 ++++++++
 .../Controller/Arcade/Scoring/CrystalCaptureScoringRuleSO.cs          |  69 ++++
 .../CosmicShore.Game/Controller/Arcade/Scoring/JoustScoringRuleSO.cs  |  75 ++++
 .../Controller/Arcade/TurnMonitors/JoustCollisionTurnMonitor.cs       |  82 ++++
 .../Arcade/TurnMonitors/NetworkJoustCollisionTurnMonitor.cs           | 137 +++++++
 .../Vessel Skimmer Effects/VesselExplosionBySkimmerEffectSO.cs        | 112 +++++
 Port/tests/CosmicShore.Tests/CrystalCaptureTests.cs                   | 301 ++++++++++++++
 Port/tests/CosmicShore.Tests/JoustTests.cs                            | 522 ++++++++++++++++++++++++
 13 files changed, 3142 insertions(+), 22 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 3175 lines)</summary>

```diff
diff --git a/Port/src/CosmicShore.Cli/CrystalCaptureRound.cs b/Port/src/CosmicShore.Cli/CrystalCaptureRound.cs
new file mode 100644
index 000000000..39d726ed8
--- /dev/null
+++ b/Port/src/CosmicShore.Cli/CrystalCaptureRound.cs
@@ -0,0 +1,701 @@
+using System;
+using System.Collections.Generic;
+using System.Globalization;
+using System.Linq;
+using System.Reflection;
+using CosmicShore.Core;
+using CosmicShore.Data;
+using CosmicShore.Engine;
+using CosmicShore.Engine.Injection;
+using CosmicShore.Engine.Networking;
+using CosmicShore.Engine.Soap;
+using CosmicShore.Gameplay;
+using CosmicShore.ScriptableObjects;
+using CosmicShore.UI;
+using CosmicShore.Utility;
+using Object = CosmicShore.Engine.Object;
+using Random = CosmicShore.Engine.Random;
+
+namespace CosmicShore.Cli
+{
+    /// <summary>Knobs for one headless AI-vs-AI Crystal Capture round.</summary>
+    public sealed class CrystalCaptureRoundOptions
+    {
+        /// <summary>Total AI players (Crystal Capture supports 1-4; domains balance Jade→Ruby→Gold).</summary>
+        public int PlayerCount = 4;
+        public int Seed = 42;
+
+        /// <summary>
+        /// Per-domain crystal sum that ends the turn. Authored through the REAL tool surface:
+        /// the harness registers an EndConditionOverrides asset at the Resources path the
+        /// ported CrystalCollisionTurnMonitor loads (Tools &gt; Cosmic Shore &gt; End Game Conditions).
+        /// </summary>
+        public int CrystalTarget = 10;
+
+        /// <summary>Fail-loud frame cap (default 10 simulated minutes @ 60 Hz).</summary>
+        public int MaxFrames = 60 * 60 * 10;
+
+        public float DeltaTime = 1f / 60f;
+
+        /// <summary>Center-to-center claim distance (crystal trigger radius + vessel bubble radius).</summary>
+        public float ClaimRadius = 25f;
+
+        /// <summary>Radius of the vessel's contact-bubble SphereCollider (world units).</summary>
+        public float VesselContactRadius = 4f;
+    }
+
+    /// <summary>One row of the final standings (derived from GameDataSO.Results + RoundStats).</summary>
+    public sealed class CrystalCaptureStanding
+    {
+        public int Rank;
+        public string Name;
+        public Domains Domain;
+        public int Crystals;
+        public float Score;
+        public string ScoreText;
+    }
+
+    public sealed class CrystalCaptureRoundResult
+    {
+        public bool Finished;
+        public string WinnerName = "";
+        public Domains WinnerDomain = Domains.Blue;
+        public int WinnerDomainCrystals;
+        public int FramesSimulated;
+        public int TotalClaims;
+
+        /// <summary>Deterministic line-by-line log: claims, finish, standings. Same seed → identical list.</summary>
+        public List<string> Transcript = new();
+
+        public List<CrystalCaptureStanding> Standings = new();
+
+        /// <summary>Error/Exception entries captured from the engine log during the round (expected empty).</summary>
+        public List<string> EngineErrors = new();
+    }
```

</details>

### `ff7a5355d` — feat(port): cell-ecology completion — a Cell runs fully alive headless

_Claude, 2026-07-02 16:09:41 +0000_

```text
SpawnProfileSO + FloraConfigurationSO + ICellLifeSpawner +
CellLifeSpawnerBase + RandomLifeSpawner + IntensityWiseLifeSpawner +
CellModifier/ExtraOmniCrystals + SnowChanger (cytoplasm shards) +
CapsuleMembrane(+AnimationSO) — verbatim modulo substitutions. All 41
V11/V12 family markers in Cell.cs + CellConfigDataSO RESTORED: the full
alive chain runs headless — crystal-triggered post-init ->
ApplyModifiers -> SpawnCytoplasm -> StartSpawnerForMode -> real
flora/fauna seeding. CapsuleMembrane's simulation surface (Radius ->
Cell.MembraneRadius, icosphere layout, bake math) live; its
instanced-draw internals carry 19 staged mesh-arc markers per the
VesselModelBuilder convention.

Locked-invariant soak coverage (8 CellEcologyTests): no imposed death
(2-sim-minute soak — populations shrink only through active forces),
starvation withers to ONE elemental crystal that outlives the creature
(mass conserved), fauna seed only the controlling color / flora never
Blue, phase ladder + FaunaSpawningEnabled stay volume-keyed, membrane
radius from CapsuleMembrane. Upstream findings documented: CellStats
struct-copy bug (LifeFormsInCell never persists) and double
SpawnCytoplasm call — ported verbatim, flagged in PORT_PLAN.

Gate green both configs: 991 + 276 = 1267. Client 1200-frame fingerprint
byte-identical (crystals [6,1,4,1], trail 786).
```

```text
 Port/PORT_PLAN.md                                                     |  30 ++
 Port/src/CosmicShore.Engine/Attributes.cs                             |  13 +
 Port/src/CosmicShore.Game/Controller/Environment/Cell.cs              |  84 +++--
 .../CosmicShore.Game/Controller/Environment/CellLifeSpawnerBase.cs    | 212 +++++++++++++
 .../Controller/Environment/CellModifiers/CellModifier.cs              |  12 +
 .../Controller/Environment/CellModifiers/ExtraOmniCrystals.cs         |  36 +++
 .../CosmicShore.Game/Controller/Environment/Cytoplasm/SnowChanger.cs  | 127 ++++++++
 Port/src/CosmicShore.Game/Controller/Environment/ICellLifeSpawner.cs  |  18 ++
 .../Controller/Environment/IntensityWiseLifeSpawner.cs                | 203 ++++++++++++
 Port/src/CosmicShore.Game/Controller/Environment/RandomLifeSpawner.cs | 194 ++++++++++++
 Port/src/CosmicShore.Game/Game/Environment/CapsuleMembrane.cs         | 423 +++++++++++++++++++++++++
 .../CosmicShore.Game/Game/Environment/CapsuleMembraneAnimationSO.cs   | 140 +++++++++
 Port/src/CosmicShore.Game/Utility/DataContainers/CellConfigDataSO.cs  |  10 +-
 .../CosmicShore.Game/Utility/DataContainers/FloraConfigurationSO.cs   |  20 ++
 Port/src/CosmicShore.Game/Utility/DataContainers/SpawnProfileSO.cs    |  52 +++
 Port/tests/CosmicShore.Tests/CellEcologyTests.cs                      | 539 ++++++++++++++++++++++++++++++++
 16 files changed, 2064 insertions(+), 49 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 2294 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 874b82e3c..cc8d816bc 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -882,6 +882,36 @@ rigidbodies; satisfies [RequireComponent] so authored setup ports verbatim).
 5 tests. These shapes are the Phase-2 shape-drawing content (lava-lamp
 freestyle) and general track decoration for the client.
 
+Ecosystem groundwork part 4 — cell-ecology completion (V12 families CLOSED):
+`SpawnProfileSO` + `FloraConfigurationSO` (engine gains the inert `MinMaxAttribute`
+shim for the original's `Unity.Entities.UI.MinMax`), the full spawner chain
+(`ICellLifeSpawner` + `CellLifeSpawnerBase` + `RandomLifeSpawner` +
+`IntensityWiseLifeSpawner`), `CellModifier` + `ExtraOmniCrystals`, `SnowChanger`
+(cytoplasm — fully live headless, shards are plain GameObjects), and
+`CapsuleMembrane` + `CapsuleMembraneAnimationSO`
+(`src/CosmicShore.Game/Game/Environment/`, mirrors `_Scripts/Game/Environment/`) —
+all verbatim modulo the README substitutions. **All 41 deviation markers of these
+families restored** (36 in `Cell.cs` — spawner fields/StartSpawnerForMode/
+StopSpawner, SpawnProfile in CurrentFaunaSpawnPeriod, ApplyModifiers,
+SpawnCytoplasm + both cytoplasm destroys, CapsuleMembrane in MembraneRadius, the
+`using CosmicShore.Game` directive; 5 in `CellConfigDataSO.cs` —
+CytoplasmPrefab/CellModifiers/SpawnProfile) — a Cell now runs FULLY ALIVE
+headless: crystal-triggered post-init → modifiers → cytoplasm → real spawner
+seeding flora + fauna. CapsuleMembrane's simulation surface (Radius — the
+Cell.MembraneRadius read — icosphere layout, placement noise, offline bake math)
+is live; only the instanced-draw internals carry 19 new
+`PORT Deviation (mesh arc, …)` markers (Mesh/MeshFilter/Matrix4x4/RenderParams/
+Graphics/Gizmos — same staging as VesselModelBuilder). New `CellEcologyTests` (8)
+exercise the restored paths end-to-end AND freeze the locked invariants
+(Docs/ECOSYSTEM.md): a 2-sim-minute soak proving a seeded population NEVER
+shrinks without an active force (no imposed death), starvation → wither → the
+ONE elemental crystal reparented to the cell (mass conserved, LifeFormCrystal
+fast path), fauna seeding in the ONE controlling color + flora never Blue (no
+domain asymmetry), and the phase ladder still climbing on LiveVolume with a live
+spawner attached. Test-only finding, preserved as-is: `Cell.UpdateCellStats`
+writes `LifeFormsInCell` onto a COPY (CellStats is a struct) — verbatim upstream
+quirk, worth fixing upstream.
+
 Ecosystem groundwork part 3 (landed alongside iteration 18):
 `Physics.OverlapSphereNonAlloc` implemented against the TriggerPass collider
 registry (trigger + non-trigger, deterministic registration order, capacity
diff --git a/Port/src/CosmicShore.Engine/Attributes.cs b/Port/src/CosmicShore.Engine/Attributes.cs
index 293bbc1ce..5e61b96e0 100644
--- a/Port/src/CosmicShore.Engine/Attributes.cs
+++ b/Port/src/CosmicShore.Engine/Attributes.cs
@@ -37,6 +37,19 @@ namespace CosmicShore.Engine
         public MinAttribute(float min) { this.min = min; }
     }
 
+    /// <summary>
+    /// Inspector slider metadata mirroring Unity.Entities.UI's MinMaxAttribute
+    /// (used by FloraConfigurationSO.SpawnProbability). Inert at runtime, like
+    /// the rest of this file.
+    /// </summary>
+    [AttributeUsage(AttributeTargets.Field)]
+    public sealed class MinMaxAttribute : Attribute
+    {
+        public readonly float min;
+        public readonly float max;
+        public MinMaxAttribute(float min, float max) { this.min = min; this.max = max; }
+    }
+
     [AttributeUsage(AttributeTargets.Field)]
     public sealed class TextAreaAttribute : Attribute
     {
diff --git a/Port/src/CosmicShore.Game/Controller/Environment/Cell.cs b/Port/src/CosmicShore.Game/Controller/Environment/Cell.cs
index f1ff39c38..01f59d048 100644
--- a/Port/src/CosmicShore.Game/Controller/Environment/Cell.cs
+++ b/Port/src/CosmicShore.Game/Controller/Environment/Cell.cs
@@ -2,9 +2,7 @@
 using System.Collections.Generic;
 using System.Linq;
 using CosmicShore.Data;
-// PORT Deviation (V12, restore when CapsuleMembrane ports): using CosmicShore.Game;
-// (CapsuleMembrane is the only type this file uses from the CosmicShore.Game namespace,
-// and that namespace does not exist in the port yet — the directive would not compile.)
+using CosmicShore.Game;
 using CosmicShore.Gameplay;
 using CosmicShore.Utility;
 using CosmicShore.Engine.Injection;
```

</details>

### `9d325fff2` — feat(port): engine Mesh arc — 57 mesh-blocked deviations restored

_Claude, 2026-07-02 16:43:01 +0000_

```text
Engine original-contract additions: Mesh (copy-on-get buffers, submesh
SetTriangles, RecalculateNormals/Bounds), MeshFilter (instance-on-access
mesh semantics), MeshCollider (mesh-bounds AABB overlap in TriggerPass,
documented; full mesh collision is a later physics phase),
SkinnedMeshRenderer.sharedMesh+BakeMesh, built-in primitive meshes
(CreatePrimitive now fills real geometry; icosphere sphere documented),
Rendering.Matrix4x4 (TRS/MultiplyPoint3x4; namespaced per the CS0104
precedent), AnimationCurve (cubic Hermite, EaseInOut/Linear/Constant),
Graphics.RenderMeshInstanced as a ring-bounded data-only submission
recorder, Renderer.bounds from real mesh extents via 8-corner TRS sweep.

Verbatim ports: OctahedronMeshGenerator + StellatedOctahedronMeshGenerator
+ IcosphereMeshGenerator, PrismOctahedronShield +
PrismStellatedOctahedronShield (full engage/disengage state machines,
Box<->convex MeshCollider swap, volume-scaled mass — ZERO presentation
deviations; AnimationCurve + Mesh covered everything).

Restorations (all diff-verified verbatim): VesselModelBuilder (31 markers
— toy mini ship models harvest real meshes now), CapsuleMembrane draw
internals (19 — TRS arrays, RenderParams, per-frame RenderMeshInstanced),
SegmentSpawner super-shield block, AstroLeagueBall icosphere. Zero
mesh-arc markers remain.

+41 tests (35 MeshArcTests + 3 VesselModelBuilder + 3 shield/spawner).
Gate green both configs: 1026 + 276 = 1302. All 5 CLI modes exit 0;
client diag byte-identical.
```

```text
 Port/PORT_PLAN.md                                                     |  67 ++-
 Port/src/CosmicShore.Engine/Attributes.cs                             |   8 +
 Port/src/CosmicShore.Engine/Compat/EngineCompat.cs                    |  17 +
 Port/src/CosmicShore.Engine/Math/AnimationCurve.cs                    | 136 ++++++
 Port/src/CosmicShore.Engine/Math/Matrix4x4.cs                         | 150 +++++++
 Port/src/CosmicShore.Engine/Rendering/Graphics.cs                     | 116 +++++
 Port/src/CosmicShore.Engine/Rendering/Mesh.cs                         | 278 ++++++++++++
 Port/src/CosmicShore.Engine/Rendering/PrimitiveMeshes.cs              | 295 +++++++++++++
 Port/src/CosmicShore.Engine/Rendering/Renderers.cs                    |  50 ++-
 Port/src/CosmicShore.Engine/SceneGraph/GameObject.cs                  |  14 +-
 Port/src/CosmicShore.Engine/SceneGraph/TriggerPass.cs                 |  83 ++++
 .../CosmicShore.Game/Controller/Arcade/AstroLeague/AstroLeagueBall.cs |  23 +-
 .../Controller/Environment/MiniGameObjects/SegmentSpawner.cs          |   8 +-
 Port/src/CosmicShore.Game/Controller/Toys/VesselModelBuilder.cs       |  64 ++-
 Port/src/CosmicShore.Game/Controller/Vessel/PrismOctahedronShield.cs  | 451 +++++++++++++++++++
 .../Controller/Vessel/PrismStellatedOctahedronShield.cs               | 468 ++++++++++++++++++++
 Port/src/CosmicShore.Game/Game/Environment/CapsuleMembrane.cs         | 199 ++++-----
 Port/src/CosmicShore.Game/Utility/IcosphereMeshGenerator.cs           | 171 ++++++++
 Port/src/CosmicShore.Game/Utility/OctahedronMeshGenerator.cs          | 217 ++++++++++
 Port/src/CosmicShore.Game/Utility/StellatedOctahedronMeshGenerator.cs | 270 ++++++++++++
 Port/tests/CosmicShore.Tests/MeshArcTests.cs                          | 740 ++++++++++++++++++++++++++++++++
 Port/tests/CosmicShore.Tests/ToySystemTests.cs                        |  80 ++++
 22 files changed, 3736 insertions(+), 169 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 4229 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index cc8d816bc..9ba07bd96 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -319,7 +319,7 @@ Next: V8 (VesselTransformer + member restore), rival balance from prompter feedb
 | 14-ext | `PlayerDataService` (C2) stages its UGS CloudSave surface exactly like GameSetting's #14: `UGSDataService` injection, repo read/write, ready-event hooks commented with `PORT Deviation #14 (C2, restore when UGSDataService ports)` markers (7 sites); local default profile, crystal/XP math, reward unlocks, and the OnProfileChanged → GameDataSO sync are live. | Services phase owns the restore. |
 | 16 | `Directory.Build.props` adds CS0169 to NoWarn (alongside CS0649): verbatim Unity-era private fields whose only usages are commented (e.g. `InputController.vessel` until its orientation block revives) or inspector-driven fire it; the Unity compiler tolerated them. | Verbatim fields without warning noise. |
 | 17 | **Controller-chain arc scene deviations** (all marked `PORT Deviation (scene arc, …)` / `(UI shell, …)`): `MultiplayerMiniGameControllerBase` — the `SceneTransitionManager` fade field + its 2 call sites and the `nm.SceneManager.LoadScene` replay reload block are commented (no scene manager yet; everything in-scene — turn/round state machine, replay AI despawns, config sync — verbatim). `CountdownTimer` — the DOTween/Image/Sprite/beep presentation is replaced by a timing-equivalent GameTask beat loop (4 × countdownDuration unscaled → onComplete; `_seq?.Kill()` → CTS cancel parity). `CameraManager` shell grew a no-op `SnapPlayerCameraToTarget()` (Deviation #12 surface). Restore with the scene-management / UI arcs. | Smallest surface for the scene/UI gaps; the round/turn/score flow is verbatim. |
-| 18 | **AstroLeague arc**: engine gains E18 ballistic Rigidbody dynamics (linear/angular velocity + damping + `AddTorque` on a unit inertia tensor, integrated once per fixed step after the FixedUpdate phase; gravity not simulated — the HyperSea has none), data-only `PhysicsMaterial`/`Light`/`BlendMode`/material keywords+renderQueue, `FixedString32Bytes`, `NetworkManager.ConnectedClientsIds`, ISession `Deleted`/`PlayerLeaving` events, `FindAnyObjectByType`. `AstroLeagueBall` deviations (all presentation, marked): icosphere mesh swap (mesh arc), ParticleSystem aura/burst rig + haptics (presentation arc); the engine dispatches no `OnCollisionEnter/Stay`/`OnTriggerStay`, so the hull-collider strike path is carried as commented source and vessel contacts flow through the verbatim `OnTriggerEnter` path (the original's trigger-only-ship route — Serpent/Sparrow). `AstroLeagueArena`'s editor-only `OnDrawGizmos` body commented (no Gizmos). | Physics core verbatim on E18; the solver-dependent + render-side pieces restore with their phases. |
+| 18 | **AstroLeague arc**: engine gains E18 ballistic Rigidbody dynamics (linear/angular velocity + damping + `AddTorque` on a unit inertia tensor, integrated once per fixed step after the FixedUpdate phase; gravity not simulated — the HyperSea has none), data-only `PhysicsMaterial`/`Light`/`BlendMode`/material keywords+renderQueue, `FixedString32Bytes`, `NetworkManager.ConnectedClientsIds`, ISession `Deleted`/`PlayerLeaving` events, `FindAnyObjectByType`. `AstroLeagueBall` deviations (all presentation, marked): ~~icosphere mesh swap (mesh arc)~~ **mesh half CLOSED by the mesh arc** — the faceted-icosphere swap + owned-mesh destroy are live (inert in the CLI harness, whose ball GO carries no MeshFilter — the `meshFilter != null` guard is the original's); still staged: ParticleSystem aura/burst rig + haptics (presentation arc); the engine dispatches no `OnCollisionEnter/Stay`/`OnTriggerStay`, so the hull-collider strike path is carried as commented source and vessel contacts flow through the verbatim `OnTriggerEnter` path (the original's trigger-only-ship route — Serpent/Sparrow). `AstroLeagueArena`'s editor-only `OnDrawGizmos` body commented (no Gizmos). | Physics core verbatim on E18; the solver-dependent + render-side pieces restore with their phases. |
 | 19 | **Tournament (Maelstrom) arc**: engine gains the `CosmicShore.Engine.SceneManagement` surface (`SceneManager.sceneLoaded` + `LoadSceneMode`; loads are announced by harnesses via `NotifySceneLoaded` until real scene management lands — the controller's subscription + `HandleSceneLoaded` are verbatim), Netcode 2.x `[Rpc(SendTo.…)]`/`RpcParams` metadata (local-invoke), and the headless `Engine.UI.Button` shim. `TournamentSceneView` deviations (all presentation, marked): DOTween pulse/typewriter bodies no-op; round/summary card population commented (TournamentRoundCard / TournamentSummaryPlayerCard / TournamentPlayerCard / TournamentDomainScoreView are Image/CanvasGroup/DOTween prefab views — unported, restore with the UI arc); ScrollRect auto-scroll + LayoutRebuilder; SO_AIProfileList avatar branch; `NetworkManager.SpawnManager`/`LocalClient` roster/local-domain fallbacks. CLI `--mode tournament` legs are simulated by the headless `HexRaceRound` regardless of the drawn mode until the Joust / Crystal Capture controllers port (stated in the transcript). | Meta logic (fold, race-to-6, draw, phases) verbatim + tested; render-side pieces restore with their phases. |
 
 ## Iteration log
@@ -811,15 +811,74 @@ Gate BOTH configs when touching logging paths.
 
 Track infrastructure (landed alongside iteration 20): `SegmentSpawner` ported
 (the HexRace deterministic-track spawner — seeded segment placement, prism trails
-per segment, intensity-scaled spacing). One deviation: the diagnostic
+per segment, intensity-scaled spacing). ~~One deviation: the diagnostic
 super-shield block restores when `PrismStellatedOctahedronShield` ports with the
-engine Mesh/MeshFilter arc (468L shield + 270L StellatedOctahedronMeshGenerator —
-flagged as the shield arc). Engine gains the `Transform.Rotate(axis, angle)`
+engine Mesh/MeshFilter arc~~ **CLOSED by the mesh arc (below)** — the super-shield
+diagnostic block is verbatim again. Engine gains the `Transform.Rotate(axis, angle)`
 overload. This unblocked the real `HexRaceController` chain
 (MiniGameControllerBase → Multiplayer → DomainGames → HexRace, 1,112L) — **DONE**,
 see "Controller-chain + AstroLeague arc" above; replacing SkimRaceDirector with the
 real controller in the CLIENT remains a convergence-ladder follow-up.
 
+## Mesh arc (landed after the cell-ecology completion — dedicated agent arc)
+
+The engine carries REAL MESH DATA now; every `PORT Deviation (mesh arc, …)` marker in
+`src/` is restored (grep count: 0 remaining). Engine additions
+(`src/CosmicShore.Engine/`): original-contract **`Mesh`** (Rendering/Mesh.cs —
+vertices/normals/uv/colors buffers with the original copy-on-get semantics,
+triangles ↔ submeshes + `subMeshCount`/`SetTriangles` (auto-grow port convenience,
+documented), settable `bounds` + `RecalculateBounds`, smooth `RecalculateNormals`,
+`Clear`, no-op `MarkDynamic`, `indexFormat` + `Engine.Rendering.IndexFormat`);
+**`MeshFilter`** (`sharedMesh` plain ref; `mesh` instance-on-access — clones the
+shared mesh into a cached "<name> Instance" and repoints sharedMesh, the original
+contract); **`SkinnedMeshRenderer.sharedMesh` + `BakeMesh`** (headless: the bake IS
+the bind pose — deep copy); **`MeshCollider`** (`sharedMesh`/`convex`; the
+TriggerPass overlaps it as its mesh-bounds world AABB — rotation-ignored like the
+phase-2 box convention, null mesh never overlaps; participates in
+OverlapSphere/CheckBox queries too); **`GameObject.CreatePrimitive` fills real
+shared primitive meshes** (PrimitiveMeshes.cs: Cube 24-vert flat, Sphere r=0.5
+icosphere [documented deviation: icosphere not UV-sphere], Capsule r=0.5 h=2,
+Cylinder, Plane 10×10 [single quad, documented], Quad; non-sphere colliders sized
+to mesh bounds); **`Renderer.bounds` refined** — a sibling MeshFilter (or SMR
+sharedMesh) supplies real extents via an 8-corner TRS sweep, unit-cube convention
+kept when meshless; **`Matrix4x4`** (Math/Matrix4x4.cs, in `Engine.Rendering` for
+the same System.Numerics CS0104 reason as PrimitiveType — TRS/MultiplyPoint3x4/
+MultiplyVector/columns/product); **`RenderParams` + `Graphics.RenderMeshInstanced`**
+(data-only SUBMISSION RECORDER, ring-bounded at 16 so per-frame callers never grow
+memory over soaks; thread-safe); **`AnimationCurve`/`Keyframe`** (cubic Hermite,
+clamp outside keys, EaseInOut/Linear/Constant factories); **`[ContextMenu]`**
+(inert marker attribute). Ported verbatim (README substitutions only; the
+qualified-name map gains `UnityEngine.Rendering.X` → `CosmicShore.Engine.Rendering.X`
+for ShadowCastingMode/IndexFormat): `Utility/OctahedronMeshGenerator` +
+`Utility/StellatedOctahedronMeshGenerator` + `Utility/IcosphereMeshGenerator`,
+`Controller/Vessel/PrismOctahedronShield` + `PrismStellatedOctahedronShield` (the
+full engage-bloom / shatter-overlay state machines, Box ↔ convex MeshCollider swap,
+mass = ρ·8abc ↔ ρ·36abc/ρ·108abc — no presentation deviations needed: AnimationCurve
++ Mesh cover everything). **Deviations RESTORED, diff-verified vs Assets**:
+`SegmentSpawner.SuperShieldSpawnedPrisms` (the stellated super-shield diagnostic —
+file now fully verbatim), `VesselModelBuilder` (all 31 markers — the
+MeshFilter/SkinnedMeshRenderer harvest + AddMesh + pose math live; TryBuild returns
+TRUE for mesh rigs, so the vessel-changer toy shows real mini hulls when prefab
+meshes exist), `CapsuleMembrane` (all 19 draw-internal markers — Matrix4x4 TRS
+arrays, RenderParams, per-frame `Graphics.RenderMeshInstanced`, preset/fallback
+matrix paths, `GetBuiltinCapsuleMesh`; sole remaining marker is the
+`OnDrawGizmosSelected` body, re-tagged "(restore when engine Gizmos lands)"),
+`AstroLeagueBall` (icosphere mesh swap + owned-mesh destroy — part of Deviation #18,
+now closed for the mesh half; ParticleSystem/haptics halves still staged). Tests:
+`MeshArcTests.cs` (35 — Mesh buffer/submesh/bounds/normals contracts, MeshFilter
+instancing, BakeMesh, MeshCollider trigger enter/exit + spatial queries + null-mesh
+inertness, CreatePrimitive real+shared meshes, Matrix4x4 TRS vs transform math,
+Graphics recorder ring bound, AnimationCurve smoothstep shape, octahedron/stellation
+vertex+face counts + ContainsPointLocal boundary cases (face/vertex/corner exactly
+on the L1/tetrahedral surfaces) + FaceScale topology stability + shatter normals,
```

</details>

### `06975b5d9` — feat(port): freestyle (lava-lamp) mode in the playable client

_Claude, 2026-07-02 19:54:38 +0000_

```text
--mode freestyle boots the lava lamp: the Squirrel orbits the cell
crystal on the real AIPilot, spawned through the real
Player->NetcodeHooks->Server/Client/Menu-initializer chain (Jade reset
server-side), laying a conserved prism ribbon inside a living Cell —
membrane wireframe, drifting cytoplasm motes, 3 BranchingFlora growing
leaf-prisms, 3 LightFauna in the controlling color. Eight toys bloom on
the membrane ring. Tab takes the stick (boost drains the real energy
bar; skim your own aged ribbon to recharge). Domain toy ->
RequestSetDomain_ServerRpc -> hull re-tints live. Vessel toy -> real
RequestSwap despawn/spawn/ReInitializePair. FLY BY NUMBERS -> star
guides traced with the real trail; guides fade, painted prisms REMAIN
(conserved mass — no caps/TTLs/cullers anywhere; soak-tested).

FreestyleSim (factory+director+tuning), FreestyleWindow (renderer/input/
diag + 16-segment vector font for labels). Engine original-contract
fixes (+4 tests): Instantiate builds clone trees deactivated with pose
applied before activation (Awake sees final placement); HashSet joins
the E16 fresh-container clone rule (shared template set leaked
BranchingFlora.activeBranches across clones).

+8 tests. Gate green both configs: 1034 + 276 = 1310. Race diag
byte-identical; freestyle diag deterministic per seed (flora 3, fauna 3,
prisms 39, toys 8).
```

```text
 Port/src/CosmicShore.Client/FreestyleSim.cs                    |  769 ++++++++++++++++++++++++++
 Port/src/CosmicShore.Client/FreestyleWindow.cs                 | 1135 ++++++++++++++++++++++++++++++++++++++
 Port/src/CosmicShore.Client/Program.cs                         |   32 +-
 Port/src/CosmicShore.Client/SkimRacePrisms.cs                  |   49 ++
 Port/src/CosmicShore.Client/SkimRaceSim.cs                     |    2 +-
 Port/src/CosmicShore.Engine/Compat/EngineCompat.cs             |   72 ++-
 Port/src/CosmicShore.Engine/Object.cs                          |   19 +-
 Port/tests/CosmicShore.Tests/ClientConvergenceTests.cs         |  319 +++++++++++
 Port/tests/CosmicShore.Tests/EngineInstantiateContractTests.cs |  175 ++++++
 9 files changed, 2556 insertions(+), 16 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 2776 lines)</summary>

```diff
diff --git a/Port/src/CosmicShore.Client/FreestyleSim.cs b/Port/src/CosmicShore.Client/FreestyleSim.cs
new file mode 100644
index 000000000..a2b0f5134
--- /dev/null
+++ b/Port/src/CosmicShore.Client/FreestyleSim.cs
@@ -0,0 +1,769 @@
+using System;
+using System.Collections.Generic;
+using CosmicShore.Core;
+using CosmicShore.Data;
+using CosmicShore.Engine;
+using CosmicShore.Engine.Injection;
+using CosmicShore.Engine.Networking;
+using CosmicShore.Engine.Services;
+using CosmicShore.Engine.Soap;
+using CosmicShore.Game;
+using CosmicShore.Gameplay;
+using CosmicShore.ScriptableObjects;
+using CosmicShore.UI;
+using CosmicShore.Utility;
+using EngineObject = CosmicShore.Engine.Object;
+
+namespace CosmicShore.Client
+{
+    /// <summary>
+    /// Optional per-run ecology tuning for <see cref="FreestyleFactory.Create"/>. The
+    /// defaults are the playable client's; tests override them to isolate invariants
+    /// (e.g. the conserved-mass soak zeroes starvation + fauna speed so NO active force
+    /// exists — proving populations then never change at all).
+    /// </summary>
+    public sealed class FreestyleTuning
+    {
+        public int FloraInitialCount = 3;
+        public int FloraMaxLivePrisms = 48;
+        public int FaunaSeedFloor = 3;
+        public int FaunaMaxPopulation = 6;
+        public float FaunaStarvationSeconds = 60f;
+        public float FaunaMinSpeed = 8f;
+        public float FaunaMaxSpeed = 16f;
+        public float FaunaConsumeRadius = 25f;
+        public float InitialFaunaSpawnWaitTime = 2f;
+        public float MembraneRadius = 420f;
+    }
+
+    /// <summary>
+    /// The freestyle (lava-lamp) brain. Unlike <see cref="SkimRaceDirector"/> there is no
+    /// track, scoring rule, turn monitor, or timer — freestyle has no score and no end
+    /// condition. The director owns only:
+    ///   • the autopilot ↔ player-control toggle (one system, two names: viewed from the
+    ///     menu it is the lava lamp; under player control it is freestyle), raising the
+    ///     REAL <see cref="MenuFreestyleEventsContainerSO"/> transition events the
+    ///     <see cref="ToyboxController"/> gates its toys on;
+    ///   • the SkimRace energy/boost feel (BoostActionSO shape gated on the real
+    ///     ResourceSystem; energy raises top speed through the real
+    ///     ThrottleScalerMultiplier hook; trail-skim charges through the real skimmer
+    ///     pipeline — identical rules to the race so the vessel feels the same);
+    ///   • scene upkeep the Unity scene gets for free: activating the initializer-chain
+    ///     vessel clone (runtime-built "prefabs" are inactive by construction) and keeping
+    ///     the rig's InputController paused so the window stays the single input driver;
+    ///   • a cached ecology census (flora / fauna / lifeform-prisms) for the renderer +
+    ///     the headless diag line;
+    ///   • deterministic Shutdown (stop the prism spawn loop, the cell's spawner
+    ///     coroutines, and the AI) so headless runs and tests wind down cleanly.
+    /// Mass is conserved here exactly like everywhere else: the director has NO despawn,
+    /// cap, or cull path — there is deliberately no RestartRace equivalent.
+    /// </summary>
+    public sealed class FreestyleDirector : MonoBehaviour
+    {
+        const float TopSpeedEnergyGain = 0.6f;   // SkimRace parity — full bar = +60% top speed
+        const float BoostDrainPerSecond = 0.55f; // SkimRace parity
+        const float CensusInterval = 0.5f;
+
+        public GameDataSO GameData { get; private set; }
+        public MenuFreestyleEventsContainerSO FreestyleEvents { get; private set; }
+        public Cell Cell { get; private set; }
+        public CellRuntimeDataSO Runtime { get; private set; }
+        public SkimRacePrismFactory PrismFactory { get; private set; }
+        public MenuServerPlayerVesselInitializer VesselInitializer { get; private set; }
+        public ToyboxController Toybox { get; private set; }
+
```

</details>

### `a18e1cb40` — refactor(port): PrismStateManager fully verbatim — V13 shield markers closed

_Claude, 2026-07-02 19:58:37 +0000_

```text
The mesh arc's PrismOctahedronShield unblocks the last 7 V13 markers:
every prism auto-adds the octahedron shield in Awake and Engage/Disengage
run on shield state transitions, exactly as upstream. File verbatim, zero
markers. Gate green both configs: 1034 + 276. Client diag byte-identical.
```

```text
 Port/src/CosmicShore.Game/Controller/Managers/PrismStateManager.cs | 14 +++++++-------
 1 file changed, 7 insertions(+), 7 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Port/src/CosmicShore.Game/Controller/Managers/PrismStateManager.cs b/Port/src/CosmicShore.Game/Controller/Managers/PrismStateManager.cs
index cf8f495aa..3625bf091 100644
--- a/Port/src/CosmicShore.Game/Controller/Managers/PrismStateManager.cs
+++ b/Port/src/CosmicShore.Game/Controller/Managers/PrismStateManager.cs
@@ -22,7 +22,7 @@ namespace CosmicShore.Gameplay
         private Prism prism;
         private MaterialPropertyAnimator materialAnimator;
         private PrismTeamManager teamManager;
-        // PORT Deviation (V13, restore when PrismOctahedronShield ports): private PrismOctahedronShield octahedronShield; // auto-added in Awake so every prism gets the octahedron on shield
+        private PrismOctahedronShield octahedronShield; // auto-added in Awake so every prism gets the octahedron on shield
 
         public BlockState CurrentState { get; private set; } = BlockState.Normal;
 
@@ -37,9 +37,9 @@ namespace CosmicShore.Gameplay
             // it; otherwise we add one at runtime so existing prefabs don't
             // need to be touched individually. The component's Awake resolves
             // BoxCollider / MeshFilter / Rigidbody from the same GameObject.
-            // PORT Deviation (V13, restore when PrismOctahedronShield ports): octahedronShield = GetComponent<PrismOctahedronShield>();
-            // PORT Deviation (V13, restore when PrismOctahedronShield ports): if (octahedronShield == null)
-            // PORT Deviation (V13, restore when PrismOctahedronShield ports):     octahedronShield = gameObject.AddComponent<PrismOctahedronShield>();
+            octahedronShield = GetComponent<PrismOctahedronShield>();
+            if (octahedronShield == null)
+                octahedronShield = gameObject.AddComponent<PrismOctahedronShield>();
         }
 
         public void MakeDangerous()
@@ -83,7 +83,7 @@ namespace CosmicShore.Gameplay
 
             // Opt-in octahedron shield visual/collider swap. Prisms without
             // this component keep the legacy material-only supershield.
-            // PORT Deviation (V13, restore when PrismOctahedronShield ports): if (octahedronShield != null) octahedronShield.Engage();
+            if (octahedronShield != null) octahedronShield.Engage();
 
             SyncAOERegistryShieldState();
         }
@@ -123,7 +123,7 @@ namespace CosmicShore.Gameplay
 
             // Engage the octahedron visual/collider swap for the regular
             // shield state too, matching super shield behavior.
-            // PORT Deviation (V13, restore when PrismOctahedronShield ports): if (octahedronShield != null) octahedronShield.Engage();
+            if (octahedronShield != null) octahedronShield.Engage();
 
             SyncAOERegistryShieldState();
             AudioSystem.Instance.PlayGameplaySFX(GameplaySFXCategory.ShieldActivate);
@@ -142,7 +142,7 @@ namespace CosmicShore.Gameplay
             prism.prismProperties.IsSuperShielded = false;
             CurrentState = BlockState.Normal;
 
-            // PORT Deviation (V13, restore when PrismOctahedronShield ports): if (octahedronShield != null) octahedronShield.Disengage();
+            if (octahedronShield != null) octahedronShield.Disengage();
 
             SyncAOERegistryShieldState();
 
```

</details>

### `20e1a5fb4` — feat(port): prism performance managers — MaterialStateManager + PrismScaleManager, 25 markers restored

_Claude, 2026-07-02 20:29:44 +0000_

```text
AdaptiveAnimationManager (registration sets, capacity doubling, adaptive
frame-interval throttle, fixed-step accumulator) + MaterialStateManager
(batched smoothstep color lerps, completion callbacks, batched
MaterialPropertyBlock apply — exact upstream ordering) +
PrismScaleManager (clamp(growthRate*dt, 0.05, 0.1) lerp, 0.01-sqr snap,
live Min/Max re-clamp, ExecuteOnScaleComplete) — the Jobs/Burst batching
converted to plain sequential loops under the sanctioned managed-array
markers (PrismSpatialIndex precedent), per-element math preserved
bit-exact (Mathf.SmoothStep, LerpUnclamped).

MaterialPropertyAnimator (17 markers) + PrismScaleAnimator (8) RESTORED —
both diff-verified verbatim. Engine: Time.realtimeSinceStartup,
Vector4.LerpUnclamped.

Test-hygiene fix found during integration: SceneModelTests +
GameLoopTests set Time.fixedDeltaTime=1/60 without restoring — a
process-global leak the new exact-fixed-step assertions exposed in
full-suite order (0.2083 vs 0.25 = 1/60 vs 0.02 step). Both classes now
restore the default in Dispose; the arc tests also pin 0.02 defensively.

+11 tests. Gate green both configs: 1046 + 276 = 1322. All 5 CLI modes
exit 0; both client modes byte-identical diags. PrismGrowthDriver
retirement assessed viable (recorded in the arc report) — deferred to a
dedicated client commit with diag re-baseline.
```

```text
 Port/src/CosmicShore.Engine/Math/Vector4.cs                           |   3 +
 Port/src/CosmicShore.Engine/Time.cs                                   |   7 +
 .../Controller/Environment/Prisms/MaterialPropertyAnimator.cs         |  34 +--
 .../Controller/Environment/Prisms/PrismScaleAnimator.cs               |  18 +-
 .../CosmicShore.Game/Controller/Managers/AdaptiveAnimationManager.cs  | 231 +++++++++++++++++++
 Port/src/CosmicShore.Game/Controller/Managers/MaterialStateManager.cs | 187 +++++++++++++++
 Port/src/CosmicShore.Game/Controller/Managers/PrismScaleManager.cs    | 172 ++++++++++++++
 Port/tests/CosmicShore.Tests/GameLoopAndTasksTests.cs                 |   8 +-
 Port/tests/CosmicShore.Tests/PrismManagerArcTests.cs                  | 397 ++++++++++++++++++++++++++++++++
 Port/tests/CosmicShore.Tests/PrismManagerTests.cs                     |  35 ++-
 Port/tests/CosmicShore.Tests/SceneModelTests.cs                       |   7 +-
 11 files changed, 1065 insertions(+), 34 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1288 lines)</summary>

```diff
diff --git a/Port/src/CosmicShore.Engine/Math/Vector4.cs b/Port/src/CosmicShore.Engine/Math/Vector4.cs
index abfaeb6bc..02a389383 100644
--- a/Port/src/CosmicShore.Engine/Math/Vector4.cs
+++ b/Port/src/CosmicShore.Engine/Math/Vector4.cs
@@ -36,6 +36,9 @@ namespace CosmicShore.Engine
             return new Vector4(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t);
         }
 
+        public static Vector4 LerpUnclamped(Vector4 a, Vector4 b, float t)
+            => new(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t);
+
         public static Vector4 operator +(Vector4 a, Vector4 b) => new(a.x + b.x, a.y + b.y, a.z + b.z, a.w + b.w);
         public static Vector4 operator -(Vector4 a, Vector4 b) => new(a.x - b.x, a.y - b.y, a.z - b.z, a.w - b.w);
         public static Vector4 operator *(Vector4 a, float d) => new(a.x * d, a.y * d, a.z * d, a.w * d);
diff --git a/Port/src/CosmicShore.Engine/Time.cs b/Port/src/CosmicShore.Engine/Time.cs
index da841314a..47535a74e 100644
--- a/Port/src/CosmicShore.Engine/Time.cs
+++ b/Port/src/CosmicShore.Engine/Time.cs
@@ -21,6 +21,13 @@ namespace CosmicShore.Engine
         /// </summary>
         public static double timeAsDouble => time;
         public static float unscaledTime { get; private set; }
+
+        /// <summary>
+        /// Wall-clock seconds since startup in the original engine. Backed by the
+        /// harness-driven unscaled clock here so headless runs stay deterministic
+        /// (readers: AdaptiveAnimationManager's frame-interval throttle).
+        /// </summary>
+        public static float realtimeSinceStartup => unscaledTime;
         public static float fixedDeltaTime { get; set; } = 0.02f;
         public static float timeScale { get; set; } = 1f;
         public static int frameCount { get; private set; }
diff --git a/Port/src/CosmicShore.Game/Controller/Environment/Prisms/MaterialPropertyAnimator.cs b/Port/src/CosmicShore.Game/Controller/Environment/Prisms/MaterialPropertyAnimator.cs
index 7c7a9ce57..73837eb7a 100644
--- a/Port/src/CosmicShore.Game/Controller/Environment/Prisms/MaterialPropertyAnimator.cs
+++ b/Port/src/CosmicShore.Game/Controller/Environment/Prisms/MaterialPropertyAnimator.cs
@@ -39,11 +39,11 @@ namespace CosmicShore.Gameplay
                     isAnimating = value;
                     if (isAnimating)
                     {
-                        // PORT Deviation (V14, restore when MaterialStateManager ports): MaterialStateManager.Instance?.OnAnimatorStartAnimating(this);
+                        MaterialStateManager.Instance?.OnAnimatorStartAnimating(this);
                     }
                     else
                     {
-                        // PORT Deviation (V14, restore when MaterialStateManager ports): MaterialStateManager.Instance?.OnAnimatorStopAnimating(this);
+                        MaterialStateManager.Instance?.OnAnimatorStopAnimating(this);
                     }
                 }
             }
@@ -82,11 +82,11 @@ namespace CosmicShore.Gameplay
 
         private void TryRegisterWithManager()
         {
-            // PORT Deviation (V14, restore when MaterialStateManager ports): if (MaterialStateManager.Instance != null && !isRegistered)
-            // PORT Deviation (V14, restore when MaterialStateManager ports): {
-            // PORT Deviation (V14, restore when MaterialStateManager ports):     MaterialStateManager.Instance.RegisterAnimator(this);
-            // PORT Deviation (V14, restore when MaterialStateManager ports):     isRegistered = true;
-            // PORT Deviation (V14, restore when MaterialStateManager ports): }
+            if (MaterialStateManager.Instance != null && !isRegistered)
+            {
+                MaterialStateManager.Instance.RegisterAnimator(this);
+                isRegistered = true;
+            }
         }
 
         private void OnEnable()
@@ -96,11 +96,11 @@ namespace CosmicShore.Gameplay
 
         private void OnDisable()
         {
-            // PORT Deviation (V14, restore when MaterialStateManager ports): if (MaterialStateManager.Instance != null && isRegistered)
-            // PORT Deviation (V14, restore when MaterialStateManager ports): {
-            // PORT Deviation (V14, restore when MaterialStateManager ports):     MaterialStateManager.Instance.UnregisterAnimator(this);
-            // PORT Deviation (V14, restore when MaterialStateManager ports):     isRegistered = false;
-            // PORT Deviation (V14, restore when MaterialStateManager ports): }
+            if (MaterialStateManager.Instance != null && isRegistered)
+            {
+                MaterialStateManager.Instance.UnregisterAnimator(this);
+                isRegistered = false;
```

</details>

### `e6b456c9f` — refactor(port): retire PrismGrowthDriver — client prisms animate via the real PrismScaleManager

_Claude, 2026-07-02 20:37:57 +0000_

```text
Both client sims (race + freestyle) now spawn the real PrismScaleManager
+ MaterialStateManager at rig creation; every PrismScaleAnimator /
MaterialPropertyAnimator registers with them at Initialize exactly as
upstream. The client-layer PrismGrowthDriver stand-in (per-prism Update
replica of the manager math) is deleted along with its two AddComponent
sites.

Both client diags verified byte-identical to the prior baselines
(race @1200: crystals [6,1,4,1], trail 786; freestyle @300: flora 3,
fauna 3, prisms 39, toys 8) — the manager's fixed-step accumulator
produces the same observable sim. Gate green both configs: 1046 + 276.
```

```text
 Port/src/CosmicShore.Client/FreestyleSim.cs   |  5 +++++
 Port/src/CosmicShore.Client/SkimRacePrisms.cs | 39 ---------------------------------------
 Port/src/CosmicShore.Client/SkimRaceSim.cs    |  5 +++++
 3 files changed, 10 insertions(+), 39 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 96 lines)</summary>

```diff
diff --git a/Port/src/CosmicShore.Client/FreestyleSim.cs b/Port/src/CosmicShore.Client/FreestyleSim.cs
index a2b0f5134..672cfb779 100644
--- a/Port/src/CosmicShore.Client/FreestyleSim.cs
+++ b/Port/src/CosmicShore.Client/FreestyleSim.cs
@@ -433,6 +433,11 @@ namespace CosmicShore.Client
             SkimRaceFactory.SetPrivateField(themeManager, "_dataContainer", theme);
             themeManagerGo.SetActive(true);
 
+            // ── the REAL prism performance managers (PrismGrowthDriver retired): every
+            // PrismScaleAnimator/MaterialPropertyAnimator registers here at Initialize ──
+            new GameObject("PrismScaleManager").AddComponent<PrismScaleManager>();
+            new GameObject("MaterialStateManager").AddComponent<MaterialStateManager>();
+
             // ── host-mode NetworkManager (the initializer chain is networked) ──
             var nm = new GameObject("network-manager").AddComponent<NetworkManager>();
             NetworkManager.Singleton = nm;
diff --git a/Port/src/CosmicShore.Client/SkimRacePrisms.cs b/Port/src/CosmicShore.Client/SkimRacePrisms.cs
index e7afd4e63..a7fd24136 100644
--- a/Port/src/CosmicShore.Client/SkimRacePrisms.cs
+++ b/Port/src/CosmicShore.Client/SkimRacePrisms.cs
@@ -94,8 +94,6 @@ namespace CosmicShore.Client
             var impactCollider = go.AddComponent<ImpactCollider>();
             SkimRaceFactory.SetPrivateField(impactCollider, "impactorObject", prismImpactor);
 
-            go.AddComponent<PrismGrowthDriver>();
-
             go.SetActive(true); // Awake chain: Prism caches managers, seeds prismProperties
             _live.Add(go);
             return new PrismReturnEventData { SpawnedObject = go };
@@ -137,7 +135,6 @@ namespace CosmicShore.Client
             var impactCollider = go.AddComponent<ImpactCollider>();
             SkimRaceFactory.SetPrivateField(impactCollider, "impactorObject", prismImpactor);
 
-            go.AddComponent<PrismGrowthDriver>();
             return prism;
         }
 
@@ -156,42 +153,6 @@ namespace CosmicShore.Client
         }
     }
 
-    /// <summary>
-    /// Client-layer scale animation driver — per-prism stand-in for the unported
-    /// PrismScaleManager (Assets/_Scripts/Controller/Managers/PrismScaleManager.cs, Jobs +
-    /// Burst). Replicates the manager's exact growth math per frame: while scaling,
-    /// lerpSpeed = clamp(GrowthRate·dt, 0.05, 0.1) toward PrismScaleAnimator.TargetScale;
-    /// within the 0.01 sqr completion threshold it snaps, clears IsScaling, and calls
-    /// ExecuteOnScaleComplete() (volume bookkeeping + largest/smallest checks) — the same
-    /// completion contract the manager drives in the original.
-    /// </summary>
-    public sealed class PrismGrowthDriver : MonoBehaviour
-    {
-        const float CompletionThresholdSqr = 0.01f; // PrismScaleManager.COMPLETION_THRESHOLD_SQR
-
-        PrismScaleAnimator _animator;
-
-        void Awake() => _animator = GetComponent<PrismScaleAnimator>();
-
-        void Update()
-        {
-            if (_animator == null || !_animator.enabled || !_animator.IsScaling) return;
-
-            var current = transform.localScale;
-            var target = _animator.TargetScale;
-            if ((target - current).sqrMagnitude > CompletionThresholdSqr)
-            {
-                float lerpSpeed = Mathf.Clamp(_animator.GrowthRate * Time.deltaTime, 0.05f, 0.1f);
-                transform.localScale = Vector3.Lerp(current, target, lerpSpeed);
-            }
-            else
-            {
-                transform.localScale = target;
-                _animator.IsScaling = false;
-                _animator.ExecuteOnScaleComplete();
-            }
-        }
-    }
 
     /// <summary>
     /// Live skim-contact state for HUD glow / audio shimmer, fed exclusively by the
```

</details>

### `4907e0b24` — feat(port): SO_AIProfileList verbatim — tournament AI avatars restored

_Claude, 2026-07-02 20:45:07 +0000_

```text
SO_AIProfileList (PickRandom unique draw, FindByName) ported verbatim;
TournamentSceneView's two AI-avatar deviation markers restored. Gate
green both configs: 1046 + 276.
```

```text
 .../Controller/Arcade/Tournament/TournamentSceneView.cs               |  9 +++--
 Port/src/CosmicShore.Game/ScriptableObjects/SO_AIProfileList.cs       | 58 +++++++++++++++++++++++++++++++++
 2 files changed, 62 insertions(+), 5 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 91 lines)</summary>

```diff
diff --git a/Port/src/CosmicShore.Game/Controller/Arcade/Tournament/TournamentSceneView.cs b/Port/src/CosmicShore.Game/Controller/Arcade/Tournament/TournamentSceneView.cs
index 972efda48..efa0339ef 100644
--- a/Port/src/CosmicShore.Game/Controller/Arcade/Tournament/TournamentSceneView.cs
+++ b/Port/src/CosmicShore.Game/Controller/Arcade/Tournament/TournamentSceneView.cs
@@ -106,7 +106,7 @@ namespace CosmicShore.Gameplay
 
         [Header("Avatars")]
         [SerializeField] SO_ProfileIconList profileIconList;
-        // PORT Deviation (restore when SO_AIProfileList ports): [SerializeField] SO_AIProfileList aiProfileList;
+        [SerializeField] SO_AIProfileList aiProfileList;
 
         bool IsHost => NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
 
@@ -435,10 +435,9 @@ namespace CosmicShore.Gameplay
 
         Sprite ResolveAvatar(TournamentPlayerSnapshot s)
         {
-            // PORT Deviation (restore when SO_AIProfileList ports): the AI avatar branch —
-            // if (s.IsAI && aiProfileList != null && aiProfileList.aiProfiles != null)
-            //     foreach (var p in aiProfileList.aiProfiles)
-            //         if (p.Name == s.Name) return p.AvatarSprite;
+            if (s.IsAI && aiProfileList != null && aiProfileList.aiProfiles != null)
+                foreach (var p in aiProfileList.aiProfiles)
+                    if (p.Name == s.Name) return p.AvatarSprite;
 
             if (profileIconList != null && profileIconList.profileIcons != null)
             {
diff --git a/Port/src/CosmicShore.Game/ScriptableObjects/SO_AIProfileList.cs b/Port/src/CosmicShore.Game/ScriptableObjects/SO_AIProfileList.cs
new file mode 100644
index 000000000..0b95ef63d
--- /dev/null
+++ b/Port/src/CosmicShore.Game/ScriptableObjects/SO_AIProfileList.cs
@@ -0,0 +1,58 @@
+using System.Collections.Generic;
+using CosmicShore.Engine;
+using System;
+
+namespace CosmicShore.ScriptableObjects
+{
+    [CreateAssetMenu(fileName = "New AI Profile List", menuName = "ScriptableObjects/AIProfileList", order = 21)]
+    public class SO_AIProfileList : ScriptableObject
+    {
+        [SerializeField] public List<AIProfile> aiProfiles;
+
+        /// <summary>
+        /// Pick <paramref name="count"/> unique random profiles.
+        /// If the list has fewer entries than requested, profiles may repeat.
+        /// </summary>
+        public List<AIProfile> PickRandom(int count)
+        {
+            var result = new List<AIProfile>(count);
+            if (aiProfiles == null || aiProfiles.Count == 0)
+                return result;
+
+            var pool = new List<AIProfile>(aiProfiles);
+            for (int i = 0; i < count; i++)
+            {
+                if (pool.Count == 0)
+                    pool = new List<AIProfile>(aiProfiles);
+
+                int idx = CosmicShore.Engine.Random.Range(0, pool.Count);
+                result.Add(pool[idx]);
+                pool.RemoveAt(idx);
+            }
+
+            return result;
+        }
+
+        /// <summary>
+        /// Find a profile by name (case-insensitive).
+        /// Returns null if not found.
+        /// </summary>
+        public AIProfile? FindByName(string name)
+        {
+            if (aiProfiles == null) return null;
+            foreach (var p in aiProfiles)
+            {
+                if (string.Equals(p.Name, name, System.StringComparison.OrdinalIgnoreCase))
+                    return p;
+            }
```

</details>

### `bf5b14f1f` — feat(port): drift-sync to bleeding-edge c18af492 — Wanderway conveyor family ported

_Claude, 2026-07-07 16:29:30 +0000_

```text
bleeding-edge merged in (42 commits, 87fdbc6b..c18af492). 28 ported files
re-verbatimed against the new base (ResourceSystem elemental drain, Toy
exit-gated re-arm + 5s regrow, four-toy default toybox, VesselModelBuilder
hull-filter + domain-tinted preview, AstroLeague court-boundary system,
Prism pool-reuse scale-animator re-arm, SetInitialSpeed chain,
FaunaReproductionRules PreyAvailable). 14 new files ported verbatim: the
microscene/conveyor family (ConveyorToy, Microscene, MicrosceneConveyor,
MicroscenePalette/Patterns/Plan, ConveyorToyDefinitionSO), the spawning
unification helpers (PrismGeometry, PrismKinds, PrismTrailBuilder,
PrismKind enum + freeze test), AstroLeagueBoundary,
VesselChangeSpeedByPrismEffectSO, and upstream's MicroscenePatternsTests.

Engine additions (original contract): static Vector3.Normalize(Vector3);
Collider.bounds with Box/Sphere/Mesh overrides on the TriggerPass
rotation-ignored world-AABB convention. New drift deviations (marked):
VesselModelBuilder MaterialGlobalIlluminationFlags, Microscene FadeIn.

Test adaptations to upstream contract changes: exit-gated re-arm rigs wire
GameData.LocalPlayer.Vessel; preview-material assertions; default toybox
3->4; golden-goal seed re-swept 3->2 (court boundaries changed
trajectories; CLI sweep validated). Full record:
Port/docs/DRIFT_2026-07-07.txt.

Gate green both configs: 1050 + 284 = 1334 (was 1046 + 276). All 5 CLI
modes exit 0. Race diag byte-identical (crystals [6,1,4,1], trail 786);
freestyle diag identical except toys 8->9 — the Wanderway conveyor
joining the default toybox ring.
```

```text
 .../Controller/Multiplayer/ClientPlayerVesselInitializer.cs           |  16 +
 .../Controller/Multiplayer/MenuServerPlayerVesselInitializer.cs       |   6 +
 Port/src/CosmicShore.Game/Controller/Toys/ConveyorToy.cs              |  97 ++++
 Port/src/CosmicShore.Game/Controller/Toys/Microscene.cs               | 404 ++++++++++++++++
 Port/src/CosmicShore.Game/Controller/Toys/MicrosceneConveyor.cs       | 353 ++++++++++++++
 Port/src/CosmicShore.Game/Controller/Toys/MicroscenePalette.cs        |  60 +++
 Port/src/CosmicShore.Game/Controller/Toys/MicroscenePatterns.cs       | 822 ++++++++++++++++++++++++++++++++
 Port/src/CosmicShore.Game/Controller/Toys/MicroscenePlan.cs           |  59 +++
 Port/src/CosmicShore.Game/Controller/Toys/SwapToySetCoordinator.cs    |  25 +
 Port/src/CosmicShore.Game/Controller/Toys/Toy.cs                      | 106 +++-
 Port/src/CosmicShore.Game/Controller/Toys/ToyboxController.cs         |   6 +
 Port/src/CosmicShore.Game/Controller/Toys/VesselChangerToySet.cs      |  59 ++-
 Port/src/CosmicShore.Game/Controller/Toys/VesselModelBuilder.cs       |  97 +++-
 Port/src/CosmicShore.Game/Controller/Vessel/IVessel.cs                |   1 +
 Port/src/CosmicShore.Game/Controller/Vessel/Prism.cs                  |  11 +
 Port/src/CosmicShore.Game/Controller/Vessel/ResourceSystem.cs         |  54 +++
 Port/src/CosmicShore.Game/Controller/Vessel/VesselController.cs       |  15 +
 Port/src/CosmicShore.Game/Controller/Vessel/VesselTransformer.cs      |   8 +
 .../ScriptableObjects/Toys/ConveyorToyDefinitionSO.cs                 | 132 +++++
 .../CosmicShore.Game/Utility/DataContainers/FaunaReproductionRules.cs |  17 +
 Port/tests/CosmicShore.Tests.Ported/MicroscenePatternsTests.cs        | 171 +++++++
 Port/tests/CosmicShore.Tests/AstroLeagueTests.cs                      |  15 +-
 Port/tests/CosmicShore.Tests/ClientConvergenceTests.cs                |   7 +-
 Port/tests/CosmicShore.Tests/ConcreteArcC2C3Tests.cs                  |   1 +
 Port/tests/CosmicShore.Tests/ConcreteArcC5Tests.cs                    |   1 +
 Port/tests/CosmicShore.Tests/EnumFreezeTests.cs                       |   8 +
 Port/tests/CosmicShore.Tests/JoustTests.cs                            |   1 +
 Port/tests/CosmicShore.Tests/ToySystemTests.cs                        |  37 +-
 Port/tests/CosmicShore.Tests/VesselLayerTestDoubles.cs                |   1 +
 54 files changed, 4583 insertions(+), 237 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 5842 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 9ba07bd96..8001de545 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -691,20 +691,31 @@ Gap-closure definition for this /loop: drift-sync complete + toys playable in th
 client + AstroLeague headless round running + remaining ladder rungs (5: real look,
 6: ambience/modes) — each iteration ships a player-feelable step, per Reorientation 1.
 
-## NEXT UP (iteration 20)
-
-1. **Rung 5**: real look — instantiate a ThemeManager with a wired
-   ThemeManagerDataContainerSO at client startup (rung-5 groundwork below),
-   SO_ColorSet domain palettes + SO_MaterialSet-driven prism/vessel draw colors
-   (GetTeam*Material per domain replacing the GL layer's hardcoded DomainColor).
-2. Team-crystal renderer polish (optional): team stations currently draw with
-   their element tint only — consider a domain-colored ring/tint so the lock is
-   readable before contact.
-3. AI boost (optional balance): rivals never boost (the real AIPilot doesn't
-   drive the SkimRace boost rule), so their energy bars sit full — consider an
-   AIPilot-driven boost intent when balance work resumes.
+## NEXT UP (after drift-sync 2026-07-07)
+
+1. **Wanderway content in the client**: the conveyor toy is live in the default
+   toybox (freestyle diag toys 8 → 9) but the code-built fallback definition has
+   no prism prefab, so its microscenes degrade to crystals + lifeforms. Wire a
+   client-built prism prefab into the fallback `ConveyorToyDefinitionSO` (the
+   client already builds prisms in code) so Wanderway transports real conserved
+   prisms; re-baseline the freestyle diag with the toy toggled on.
+2. **Vessel-initializer remainder** (from the menu-swap arc): port
+   `ServerPlayerVesselInitializerWithAI` (game-scene AI backfill),
+   `MenuCrystalClickHandler`, `MultiplayerSetup` / `DomainAssigner`, and wire the
+   menu-swap chain into the playable client scene.
+3. **Track bleeding-edge**: merge upstream again next iteration; every merge
+   reopens the drift-sync lane (survey with the
+   `git diff <old-sync> origin/bleeding-edge -- Assets/_Scripts` ∩ ported-files
+   intersection; record in `docs/DRIFT_<date>.txt` per the 2026-07-07 precedent).
 4. Update this file, commit, push.
 
+### Rung-5 leftovers (optional polish, from iteration 20)
+
+- Team-crystal renderer polish: domain-colored ring/tint so the lock is readable
+  before contact.
+- AI boost balance: rivals never boost (the real AIPilot doesn't drive the
+  SkimRace boost rule), so their energy bars sit full.
+
 ## Vessel-initializer (menu-swap) arc (landed after the controller-chain arc — dedicated agent arc)
 
 The networked player→vessel spawn/swap chain is IN: `ServerPlayerVesselInitializer`
@@ -1034,6 +1045,28 @@ needs. When rung 5 lands in the client: instantiate a ThemeManager with a wired
 container at startup and read `GetTeam*Material` per domain for prism/vessel draw
 colors.
 
+## Drift-sync 2026-07-07 — bleeding-edge c18af492 merged in (takeover iteration)
+
+bleeding-edge merged INTO this branch (42 commits, 87fdbc6b → c18af492: microscene/
+environment-spawning unification + Wanderway content PRs #576-#580, elemental drain,
+AstroLeague court boundaries, exit-gated toy re-arm). Full record:
+`Port/docs/DRIFT_2026-07-07.txt`. **28 ported files re-verbatimed** (headliners:
+ResourceSystem elemental drain, Toy exit-gated re-arm + 5s regrow, ToyboxController
+four-toy default, VesselModelBuilder hull-filter + preview material, AstroLeague
+court system, Prism pool-reuse scale-animator re-arm, IVessel/VesselController/
+VesselTransformer SetInitialSpeed, FaunaReproductionRules PreyAvailable) and
+**14 new files ported** (the whole microscene/conveyor family + spawning helpers +
+PrismKind + AstroLeagueBoundary + VesselChangeSpeedByPrismEffectSO + upstream's own
+MicroscenePatternsTests). Engine grew `Vector3.Normalize(Vector3)` and
+`Collider.bounds` (Box/Sphere/Mesh overrides on the TriggerPass rotation-ignored
+AABB convention). New drift deviations: VesselModelBuilder
+MaterialGlobalIlluminationFlags, Microscene FadeIn (both render-arc). Golden-goal
+seed re-swept 3 → 2 (court boundaries changed ball trajectories — CLI sweep
+validated seeds 2/4/5/7/8 reach overtime). **1334 tests green in BOTH configs
+(1050 + 284)**; all 5 CLI modes exit 0; race diag byte-identical
+(crystals [6,1,4,1], trail 786); freestyle diag identical except toys 8 → 9 —
+the Wanderway conveyor joining the default toybox ring.
+
 ## Loop protocol (every iteration)
 
 1. `export PATH=/opt/dotnet:$PATH` (reinstall SDK via dotnet-install.sh if container is fresh).
diff --git a/Port/docs/DRIFT_2026-07-07.txt b/Port/docs/DRIFT_2026-07-07.txt
new file mode 100644
index 000000000..86f16f4ef
```

</details>

### `95646f56d` — feat(port): Android build without Unity — APK head over the .NET port

_Claude, 2026-07-07 20:16:35 +0000_

```text
New src/CosmicShore.Client.Android (net10.0-android, deliberately NOT in the
slnx so plain dotnet build/test stay green without the android workload):
SilkActivity + SDL view + GLES 3.0 context wrapping the SAME RaceWindow /
FreestyleWindow presentation hosts as desktop. SdlTouchBackend polls SDL finger
state into the engine's EnhancedTouch shim — the backend the inert shim was
written for — so the ported, authentic TouchInputStrategy (the game's real
dual-thumb mobile scheme, Screen.dpi thumbsticks and all) drives the rig;
Bluetooth pads use the ported GamepadInputStrategy unchanged. Launch extras
pick mode/seed/crystals/rivals (default: SkimRace; -e mode freestyle for the
lava lamp). Three fingers down in freestyle = Tab; tap the finish screen to
rematch. No trimming/AOT (reflective lifecycle discovery, same rule as desktop
publishes); glibc SDL that NuGet RID-fallback drags in is dropped from the APK
(the Silk aar's libSDL2.so is the single instance both Java and C# load).

Shared client changes, desktop-neutral: windows now run on IView (desktop
Run() unchanged, Android passes its SDL view), #if GLES swaps the GL namespace,
PlatformShader retargets the GLSL 330 sources to ES 300 at compile-to-GL time,
ProgramPointSize guarded (always on in ES). Engine: Application.platform now
detects Android (isMobilePlatform selects the touch strategy).

Debug-signed arm64 APK committed at Port/dist/CosmicShore-Android.apk per the
dist progress-build channel; docs updated (README "Android build (no Unity)",
PORT_PLAN milestone port-m5 + csproj count 7→8, dist/README, .gitignore
negation for Port/dist/*.apk after the Unity *.apk rule).

Gate: 1050 + 284 = 1334 tests green; all 5 CLI modes exit 0; client diags
byte-identical to the recorded gate (race @1200: crystals [6,1,4,1], trail 786;
freestyle @300: flora 3, fauna 3, prisms 39, toys 9, autopilot True).
```

```text
 .gitignore                                                            |   3 ++
 Port/CosmicShore.slnx                                                 |   3 ++
 Port/PORT_PLAN.md                                                     |   8 ++-
 Port/README.md                                                        |  45 +++++++++++++++-
 Port/dist/CosmicShore-Android.apk                                     | Bin 0 -> 23057790 bytes
 Port/dist/README.txt                                                  |   6 +++
 Port/src/CosmicShore.Client.Android/CosmicShore.Client.Android.csproj |  68 ++++++++++++++++++++++++
 Port/src/CosmicShore.Client.Android/MainActivity.cs                   |  89 ++++++++++++++++++++++++++++++++
 Port/src/CosmicShore.Client.Android/SdlTouchBackend.cs                |  62 ++++++++++++++++++++++
 Port/src/CosmicShore.Client/FreestyleWindow.cs                        |  70 ++++++++++++++++++++++---
 Port/src/CosmicShore.Client/RaceWindow.cs                             |  77 ++++++++++++++++++++++++---
 Port/src/CosmicShore.Engine/Compat/EngineCompat.cs                    |   1 +
 12 files changed, 417 insertions(+), 15 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 565 lines)</summary>

```diff
diff --git a/Port/PORT_PLAN.md b/Port/PORT_PLAN.md
index 8001de545..75780eed8 100644
--- a/Port/PORT_PLAN.md
+++ b/Port/PORT_PLAN.md
@@ -36,9 +36,12 @@ The prompter tests progress without prompting the loop. Contract:
 1. **Green branch invariant.** Every push to `claude/quirky-cannon-sk8a02` has
    `cd Port && dotnet build && dotnet test` green. The branch is always safe to pull.
    This includes **buildable from a fresh clone**: `git ls-files Port | grep -c 'csproj$'`
-   must equal 7 — the root Unity `.gitignore` (`*.csproj`/`*.sln`) silently excluded
+   must equal 8 — the root Unity `.gitignore` (`*.csproj`/`*.sln`) silently excluded
    every project file until 2026-06-11 (negations added), which shipped a branch whose
-   source couldn't build on the prompter's machine.
+   source couldn't build on the prompter's machine. (The 8th is the Android head,
+   `src/CosmicShore.Client.Android` — deliberately NOT in the slnx so plain
+   `dotnet build`/`dotnet test` stay green without the android workload; see README
+   § "Android build".)
 2. **Runnable harness: `CosmicShore.Cli`** (`src/CosmicShore.Cli`, lands iteration 2).
    One command to exercise the current port on any machine with the .NET 10 SDK:
    `cd Port && dotnet run --project src/CosmicShore.Cli`. It grows with the port:
@@ -88,6 +91,7 @@ The prompter tests progress without prompting the loop. Contract:
 | `port-m2` | First full headless game-mode round (AI vs AI) | `cd Port && dotnet run --project src/CosmicShore.Cli -- --mode hexrace --players 4 --seed 42` | ✅ 2026-06-12 (commit `dc2eb876`) |
 | `port-m3` | First rendered frame (PNG artifact in chat + repo) | pull + open artifact | ⬜ |
 | `port-m4` | First interactive desktop build | `… -- --render` | ⬜ |
+| `port-m5` | First Android build, no Unity — APK head (SDL view + GLES 3.0 + real TouchInputStrategy) | `adb install Port/dist/CosmicShore-Android.apk` (build: README § "Android build") | ✅ 2026-07-07 (branch `claude/android-build-no-unity-gh65ai`) |
 
 ## HARD RULE (prompter, 2026-06-11): never merge OUT of this branch
 
diff --git a/Port/README.md b/Port/README.md
index 1ba965d13..34c73d215 100644
--- a/Port/README.md
+++ b/Port/README.md
@@ -22,6 +22,48 @@ dotnet build
 dotnet test
 ```
 
+## Android build (no Unity)
+
+`src/CosmicShore.Client.Android` wraps the same `RaceWindow`/`FreestyleWindow`
+presentation hosts in an Android APK: Silk.NET's SDL view (`SilkActivity`; the SDL2
+natives ship inside the Silk aar) + a GLES 3.0 context (`GLES` define swaps the GL
+namespace; `PlatformShader` retargets the GLSL 330 sources to ES 300), and an
+`SdlTouchBackend` that pumps SDL finger state into the engine's EnhancedTouch
+shim — so the ported, authentic `TouchInputStrategy` (the game's real dual-thumb
+mobile scheme) drives the rig. Bluetooth gamepads use the same ported
+`GamepadInputStrategy` as desktop.
+
+The project is deliberately **not** in `CosmicShore.slnx`: plain
+`dotnet build`/`dotnet test` must stay green on machines without the Android
+toolchain. Build it explicitly:
+
+```bash
+dotnet workload install android              # once per SDK
+export JAVA_HOME=<jdk-17-or-newer>           # JDK 21 verified
+# once: provision the Android SDK (downloads platform + build-tools; USER must be set)
+dotnet build src/CosmicShore.Client.Android -t:InstallAndroidDependencies \
+    -p:AndroidSdkDirectory=/opt/android-sdk -p:AcceptAndroidSDKLicenses=True \
+    "-p:JavaSdkDirectory=$JAVA_HOME"
+# then: the APK (debug-signed, sideload-ready; latest copy committed at dist/CosmicShore-Android.apk)
+dotnet build src/CosmicShore.Client.Android -c Release \
+    -p:AndroidSdkDirectory=/opt/android-sdk "-p:JavaSdkDirectory=$JAVA_HOME"
+adb install src/CosmicShore.Client.Android/bin/Release/net10.0-android/studio.froglet.cosmicshore.port-Signed.apk
+```
+
+Default mode is the SkimRace; launch extras pick mode/config:
+
+```bash
+adb shell am start -n studio.froglet.cosmicshore.port/.MainActivity \
+    -e mode freestyle -e seed 7
+```
+
+Touch: thumbs on glass fly (the real scheme — drift comes from finger-lift
+transitions, exactly as on the Unity mobile build); tap the finish screen to
+rematch; three fingers down in freestyle = Tab (take/release the stick). Audio
+is silent on device for now — OpenAL-soft ships no Android native and
+`AudioEngine` is fail-safe by design. No trimming/AOT (the engine's reflective
+lifecycle discovery forbids it — same rule as the desktop publishes).
+
 ## Layout
 
 ```
```

</details>

### `bd7026334` — fix(port/android): disable LLVM marshal methods — JCW natives were never registered

_Claude, 2026-07-07 20:35:48 +0000_

```text
Device run of the first APK died at startup: "No implementation found for void
crc…SilkActivity.n_loadLibraries()". Root cause: .NET 10's Release default
(LLVM marshal methods) generated Java callable wrappers WITHOUT the
Runtime.register static block while libxamarin-app.so exported no Java_* JNI
symbols either — so every Java→managed callback was unbound (SilkActivity's
loadLibraries is merely the first one hit). AndroidEnableMarshalMethods=false
restores classic dynamic registration; the packaged dex now carries the
Runtime.register strings for SilkActivity and MainActivity (verified), and the
rebuilt debug-signed arm64 APK replaces dist/CosmicShore-Android.apk.
```

```text
 Port/dist/CosmicShore-Android.apk                                     | Bin 23057790 -> 22996350 bytes
 Port/src/CosmicShore.Client.Android/CosmicShore.Client.Android.csproj |   6 ++++++
 2 files changed, 6 insertions(+)
```

### `594acbfa5` — fix(port/android): inverted yaw + roll on touch — gamepad-path parity

_Claude, 2026-07-07 22:52:08 +0000_

```text
On-device feedback (2026-07-07): roll and yaw were both inverted under touch.
The desktop gamepad path already applies the prompter-preferred post-strategy
sense flip (XSum/YDiff negation, 2026-06-11) but the new touch branch fed the
strategy's raw output. Apply the identical flip after TouchInputStrategy.
ProcessInput() in both windows — wire semantics stay authentic, pitch already
correct (confirms the SDL y-flip). Desktop untouched: race diag @1200 remains
byte-identical. dist APK rebuilt.
```

```text
 Port/dist/CosmicShore-Android.apk              | Bin 22996350 -> 22996350 bytes
 Port/src/CosmicShore.Client/FreestyleWindow.cs |   4 ++++
 Port/src/CosmicShore.Client/RaceWindow.cs      |   5 +++++
 3 files changed, 9 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Port/src/CosmicShore.Client/FreestyleWindow.cs b/Port/src/CosmicShore.Client/FreestyleWindow.cs
index 9ce4c3078..71416cd12 100644
--- a/Port/src/CosmicShore.Client/FreestyleWindow.cs
+++ b/Port/src/CosmicShore.Client/FreestyleWindow.cs
@@ -526,6 +526,10 @@ void main()
                 _prevTriple = triple;
                 if (!_director.IsFreestyle) return; // AIPilot owns the rig in the lava lamp
                 _touchStrategy.ProcessInput();      // authentic dual-thumb scheme → InputStatus
+                // Prompter preference (2026-07-07, on-device feedback): inverted yaw +
+                // roll on touch — RaceWindow/gamepad parity, applied post-strategy.
+                _playerStatus.XSum = -_playerStatus.XSum;
+                _playerStatus.YDiff = -_playerStatus.YDiff;
                 return;
             }
 
diff --git a/Port/src/CosmicShore.Client/RaceWindow.cs b/Port/src/CosmicShore.Client/RaceWindow.cs
index 7cbb19466..abe256456 100644
--- a/Port/src/CosmicShore.Client/RaceWindow.cs
+++ b/Port/src/CosmicShore.Client/RaceWindow.cs
@@ -682,6 +682,11 @@ void main()
                     return; // victory lap: the real AIPilot owns the rig
                 }
                 _touchStrategy.ProcessInput(); // authentic dual-thumb scheme → InputStatus
+                // Prompter preference (2026-07-07, on-device feedback): inverted yaw +
+                // roll on touch — the same post-strategy sense flip the gamepad path
+                // applies below, so the ported strategy's wire semantics stay authentic.
+                _playerStatus.XSum = -_playerStatus.XSum;
+                _playerStatus.YDiff = -_playerStatus.YDiff;
                 return;
             }
 
```

</details>

_Also contains 5 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
