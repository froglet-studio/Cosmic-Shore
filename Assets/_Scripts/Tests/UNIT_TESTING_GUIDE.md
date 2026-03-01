# Unit Testing Guide — Cosmic Shore

## Test Inventory

36 test files, ~500+ individual tests across 6 locations.

### Edit-Mode Tests (`Assets/_Scripts/Tests/EditMode/`) — 22 files

| File | Subject Under Test | Why It Exists |
|---|---|---|
| `EnumIntegrityTests` | `VesselClassType`, `Domains`, `GameModes`, `Element`, `ShipActions`, `ResourceType` | Unity serializes enums by int value. If someone reorders members, every SO/prefab/save silently points to the wrong value. |
| `EnumIntegrityExtendedTests` | `CaptainLevel`, `CSLogLevel`, `InputEvents`, `ImpactEffects`, `ShipCameraOverrides`, `CrystalImpactEffects`, `TrailBlockImpactEffects`, `SkimmerStayEffects`, `ShipImpactEffects`, `UserActionType`, `CallToActionTargetType` | `CaptainLevel` maps to PlayFab IAP product IDs. A shift = wrong purchase applied. |
| `ResourceCollectionTests` | `ResourceCollection` struct (Mass/Charge/Space/Time) | Flows through damage, scoring, ability costs. Constructor order bug = silent data corruption. |
| `XpDataTests` | `XpData` struct | Serialized to JSON for PlayFab. Field reorder = XP assigned to wrong element. |
| `PartyPlayerDataTests` | `PartyPlayerData` struct | SOAP event payload + HashSet/Dictionary key. Equality by PlayerId only. Break = party removal fails. |
| `PartyInviteDataTests` | `PartyInviteData` struct | Invite payload through SOAP events. Field loss = failed party join. |
| `ShipModifierTests` | `ShipThrottleModifier`, `ShipVelocityModifier` | Applied every frame. Constructor field swap = 2-second boost lasts 500 frames. |
| `TrainingGameProgressTests` | `TrainingGameProgress` | Tier progression (1-4). Wrong logic = players skip tiers or claim unearned rewards. |
| `DisposableGroupTests` | `DisposableGroup` | Composite disposal pattern. Double-dispose = crash. Missing add = leak. |
| `RuntimeCollectionSOTests` | `RuntimeCollectionSO<T>` | SOAP-compatible runtime list. Add not firing ItemAdded = listeners miss items. |
| `GenericDataSOTests` | `IntDataSO`, `StringDataSO` | Base data container. Setter not firing OnValueChanged = scores/HUD silently break. |
| `CameraSettingsSOTests` | `CameraSettingsSO` | Per-vessel camera config. Default drift = camera clips geometry or zooms to zero. |
| `HostConnectionDataSOTests` | `HostConnectionDataSO` | Central party state. HasOpenSlots wrong = crash. ResetRuntimeData missing IsHost = stale state. |
| `CSDebugTests` | `CSDebug` log levels | Wrong preset = silent failures in production OR verbose logs killing performance. |
| `GameObjectExtensionTests` | `GetOrAdd`, `OrNull`, `EnableChildren`, `DisableChildren`, `DestroyChildren`, `TryGetInterface`, `IsLayer` | Used everywhere. GetOrAdd duplicating = invisible component doubling. |
| `IRoundStatsCleanupTests` | `IRoundStats.Cleanup()` | Zeroes 30+ stats between rounds. Missing property = score bleed. Most common stats bug. |
| `GameDataSOTests` | `GameDataSO` — reset, sorting, domain stats, volume, turns | Single most important runtime data object. Bugs cascade to every game mode. |
| `MainMenuStateTests` | `MainMenuState` enum + `MainMenuController` transition table | Menu state machine validation. Invalid transition allowed = menu stuck or crash. |
| `MenuFreestyleToggleTests` | `MenuCrystalClickHandler` + `MainMenuController` freestyle SOAP | Autopilot-to-freestyle toggle API. Missing guards = Time.timeScale freeze in multiplayer. |
| `PartyInviteControllerTests` | `PartyInviteController` preconditions | Host-to-client transition guards. Missing guard = Netcode crash during invite accept. |
| `PartyInviteSystemTests` | Full party/invite lifecycle — ParseInvite, collection contracts, slot management, dedup, API | Most comprehensive test file. Entire invite pipeline from parsing to HashSet dedup to slot scenarios. |
| `FriendSystemTests` | Full friend system — FriendData struct, FriendPresenceActivity, FriendsDataSO, SOAP types, API contracts | Covers the UGS Friends integration end-to-end: struct equality/hashing, IsOnline logic, DataContract compliance, SO reset, computed properties, SOAP list behavior, and API surface contracts for FriendsServiceFacade, FriendsInitializer, and all friend UI components. |

### Bootstrap Tests (`Assets/_Scripts/System/Bootstrap/Tests/`) — 6 files

| File | Why |
|---|---|
| `BootstrapControllerTests` | HasBootstrapped re-entry guard, persistent root fallback, platform config. Two AppManagers fighting = undefined behavior. |
| `BootstrapConfigSOTests` | Default values: timeout, splash, framerate, vsync, logging. Silent default change = production build breaks. |
| `ApplicationStateMachineTests` | Every valid AND invalid state transition. Happy path, terminal states, paused/resume, disconnected recovery. |
| `SceneTransitionManagerTests` | Overlay creation, initial transparency, SetFadeImmediate behavior. Broken overlay = invisible blocking UI. |
| `ApplicationLifecycleManagerTests` | Pause/focus/quit propagation, SOAP wiring, ResetStatics, scene events. IsQuitting not set = cleanup skipped. |
| `SceneFlowIntegrationTests` | Build settings: Bootstrap is index 0, scene ordering, SceneNameListSO matches build settings, scene files exist. |

### Multiplayer Tests (`Assets/_Scripts/Controller/Multiplayer/Tests/`) — 1 file

| File | Why |
|---|---|
| `DomainAssignerTests` | Team pool: unique assignment, empty pool fallback, co-op returns Jade, Blue exclusion, re-initialization. Without this, two players get the same team. |

### Performance Benchmark Tests (`Assets/_Scripts/Utility/PerformanceBenchmark/Tests/Editor/`) — 6 files

| File | Why |
|---|---|
| `BenchmarkStatisticsTests` | Mean, median, stddev, P95/P99, rendering stats, memory tracking. Wrong stats = false performance conclusions. |
| `BenchmarkComparerTests` | Comparison logic between benchmark runs. |
| `BenchmarkConfigSOTests` | Benchmark configuration defaults. |
| `BenchmarkHistoryTests` | History persistence and retrieval. |
| `BenchmarkReportTests` | Report formatting and output. |
| `MetricDeltaTests` | Metric delta calculations between runs. |

### PlayFab Tests — 1 file (stub)

`PlayFabCatalogTests` — placeholder with trivial assert.

---

## How to Think About Unit Testing

### 1. Test Contracts, Not Implementation

The best tests don't test how something works internally — they test the **promises** a type makes to its callers.

`PartyPlayerDataTests` tests the equality contract: "Same PlayerId = equal, regardless of other fields." `HashSet`, `Dictionary`, and `ScriptableList.Contains()` all depend on that contract.

**Ask:** "What does every caller of this type assume to be true?"

### 2. Prioritize by Blast Radius

| Bug Location | Blast Radius | Priority |
|---|---|---|
| Enum integer value shifts | Every serialized asset, save file, network message | **Critical** |
| `GameDataSO.ResetRuntimeData()` missing a field | Score bleed in every game mode | **Critical** |
| `ApplicationStateMachine` invalid transition | App in undefined state | **High** |
| `CameraSettingsSO` default drift | Camera wrong for one vessel | **Medium** |

### 3. Guard Serialization Boundaries

Any enum, struct, or SO field that gets serialized needs a "value stability" test:
- Lock integer values with TestCase attributes
- Count members to force test updates on additions
- Verify uniqueness to prevent ambiguous deserialization

### 4. Test State Machines Exhaustively

For every state machine, test:
- Every valid transition (happy path)
- Every invalid transition (the denied cases are MORE important)
- Same-state idempotency
- Terminal states (allowed from anywhere?)
- Recovery paths (Disconnected → MainMenu)

### 5. Test the Reset Path

Every `Reset`/`Clear`/`Cleanup` method needs:
1. Populate ALL fields with non-zero values
2. Call reset
3. Assert each field's post-reset value
4. Also verify what should NOT be cleared (identity fields)

### 6. Test Collection Compatibility

Any type used as Dictionary key, HashSet element, or matched via Contains/Equals:
- Same key → deduplicates
- Different keys → both present
- Contains finds by the correct field
- Remove works by the correct field

### 7. Test Parse ↔ Format Round-Trips

Any serialization format needs:
- Valid round-trip preserves all fields
- Every invalid input variant (null, empty, malformed)
- Edge cases (unicode, special characters, overflow)
- Documented limitations

### 8. Test ScriptableObject Defaults

`ScriptableObject.CreateInstance<T>()` in tests catches default value drift that wouldn't affect existing assets but breaks new instances.

---

## Test Patterns Used in This Project

### Pattern: Reflection for API Contract Tests
When MonoBehaviours can't be instantiated in edit mode, use reflection to verify methods/fields exist with correct signatures. Documents the API surface without requiring full runtime.

### Pattern: Mock Implementation of Interfaces
`TestRoundStats` implements `IRoundStats` without Netcode dependency. Allows testing `Cleanup()` logic in pure C#.

### Pattern: AdvanceTo() Helper
`ApplicationStateMachineTests.AdvanceTo()` walks the happy path to reach a target state, avoiding setup duplication and documenting the canonical sequence.

### Pattern: SetUp/TearDown with CreateInstance
SO tests create instances in SetUp and DestroyImmediate in TearDown. No asset pollution.

---

## Assembly Definitions

| Assembly | Location |
|---|---|
| `CosmicShore.Tests.EditMode` | `Assets/_Scripts/Tests/EditMode/` (no .asmdef — compiles in default assembly) |
| `CosmicShore.Bootstrap.Tests` | `Assets/_Scripts/System/Bootstrap/Tests/` (no .asmdef) |
| `CosmicShore.Multiplayer.Tests` | `Assets/_Scripts/Controller/Multiplayer/Tests/` (no .asmdef) |
| `CosmicShore.PlayFabTests` | `Assets/_Scripts/System/Playfab/PlayFabTests/` (has .asmdef) |
| `CosmicShore.Benchmark.Tests` | `Assets/_Scripts/Utility/PerformanceBenchmark/Tests/Editor/` (no .asmdef) |

Note: Most test directories compile in Unity's default assembly. Only `PlayFabTests` has an explicit `.asmdef`.

---

## Coverage Gaps (Opportunities)

- **No play-mode tests** — multiplayer spawn chain, SOAP event runtime firing, async UniTask flows untested
- **No integration tests for SOAP event wiring** — data contracts verified but not cross-system event propagation
- **Impact effects system** — 20+ effect SO types and 11 impactor types, zero coverage
- **Vessel actions** — 40+ VesselActionSO types untested
- **Input system** — IInputStrategy implementations, InputController strategy switching
- **Pool system** — GenericPoolManager async buffer maintenance
- **PlayFab tests** — placeholder only
