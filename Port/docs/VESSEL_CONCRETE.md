# Vessel Concrete — Type-Closure Survey & Porting Sequence

Follow-up to `VESSEL_LAYER.md` (V1–V19, complete — IVesselStatus diff-verified
verbatim). This arc ports the **concrete classes that implement the trio**
(`IVessel` / `IVesselStatus` / `IPlayer`) plus the single-player spawning path, so a
headless CLI can spawn a real Player+Vessel pair through the verbatim pipeline:

- `Controller/Vessel/VesselStatus.cs` (260) — MonoBehaviour implementing `IVesselStatus`
- `Controller/Vessel/VesselController.cs` (364) — NetworkBehaviour implementing `IVessel`
- `Controller/Player/Player.cs` (529) — NetworkBehaviour implementing `IPlayer`
- `Controller/Vessel/VesselSpawner.cs` (56)
- `Controller/Player/PlayerSpawner.cs` (46) + `PlayerSpawnerAdapterBase.cs` (44) +
  `MiniGamePlayerSpawnerAdapter.cs` (61) + `VolumeTestPlayerSpawnerAdapter.cs` (12)

All paths under `Assets/_Scripts/` unless noted. Dispositions: **ALREADY-PORTED**
(in `Port/src/`) · **LEAF** (port now, using-swaps only) · **SHALLOW** (port after
listed engine additions) · **DEEP** (drags further systems).

**Closure size: 13 files, ≈1,888 game lines** (1,372 subject + 516 closure), plus
≈345 engine lines (E11–E16). This arc is dramatically narrower than V1–V19 because
the 19-row vessel layer already landed every component the concrete classes touch —
the only new game-side dependencies are the profile-service slice
(`PlayerDataService` + `PlayerProfileData` + `SO_ProfileIconList`), the prefab
container (`VesselPrefabContainer`), and the trail tinter
(`VesselTrailCustomization`).

## Already-ported types referenced by the closure

No action needed; the tables below omit them from "depends on".

| Category | Types |
|---|---|
| Engine core | `Transform`, `GameObject` (`SetActive`, `name`, `GetOrAdd`), `Component` (`TryGetComponent`, `GetComponentInChildren<T>(includeInactive)`), `MonoBehaviour` (fake-null bool), `ScriptableObject`, `Object` (`Instantiate` SO/GO/Component clone, `Destroy`, `DontDestroyOnLoad`), `Time`, `Random.Range`, `Mathf`, `Vector3`, `Quaternion`, `Color`, `Material`, `Pose`, `Sprite`, attributes (incl. `RequireInterface`, `FormerlySerializedAs`, `RequireComponent`) |
| Engine net | `NetworkBehaviour` (`IsSpawned/IsOwner/IsServer/IsClient`, `OwnerClientId`, `Spawn()/Despawn()` host-mode), `NetworkVariable<T>` (perm-aware, `OnValueChanged`), `[ServerRpc]`/`[ClientRpc]` attribute stubs (`Compat/EngineCompat.cs` — direct-invoke semantics) |
| Engine DI | `Container` (`[Inject]`, `InjectGameObject(root, recursive)`), `FixedString64Bytes` |
| Engine SOAP | `ScriptableEventNoParam/Ulong` (`OnRaised`, `Raise`), `ScriptableVariable<T>`, `VesselClassTypeVariable` |
| Data | `Domains`, `VesselClassType`, `InputEvents`, `Element`, `ResourceCollection`, `IRoundStats` (with `Cleanup()`), `RoundStats` (NetworkBehaviour, `Name`/`Domain` live mirror) |
| Game — trio + components | `IVessel`, `IVesselStatus` (verbatim, incl. default members `IsInitializedAsAI => Player.IsInitializedAsAI`, `AutoPilotEnabled => AIPilot.AutoPilotEnabled`), `IPlayer` (+ `IPlayer.InitializeData`), `IVesselHUDController`, `VesselPrismController`, `ResourceSystem`, `VesselTransformer` (`Initialize(IVessel)`, `ToggleActive`, `ResetTransformer`, `SetPose`, `ModifyThrottle`), `AIPilot` (`Initialize(IVessel)`, `StartAIPilot`/`StopAIPilot`), `AICinematicBehavior`, `SilhouetteController`, `VesselCameraCustomizer` (`Initialize`/`RetargetAndApply`), `VesselAnimation` (`Initialize`, `StopFlareEngine/Body`), `R_VesselActionHandler` (`Initialize`, `ToggleSubscription`, `Perform/StopShipControllerActions`), `VesselCustomization`, `R_ShipElementStatsHandler` (`BindElementalFloat`), `Skimmer`, `Prism`, `ShipHelper` (`SetShipProperties`, `Teleport`), `ThemeManagerDataContainerSO`, `InputController` (`Initialize()`, `SetPause`, `SetIdle`, `InputStatus`), `IInputStatus` (`ResetForReplay`), `GameDataSO` (`Players`/`Vessels`, `OnPlayerNetworkSpawnedUlong`, `InvokeVesselNetworkSpawned`, `selectedVesselClass`, `ThemeManagerData`, `SlowedShipTransforms`, `LocalPlayerDisplayName/AvatarId`, `IsActiveDomain`, `RequestedDomainCount`, `AddPlayer`, `SetSpawnPositions`, `SetPlayersActive`, `InitializeGame`), `CSDebug`, `NetMarkers` (`Serialize`, `RpcDispatch`, `CountRpc`, `CountNetVarDirty`) |

## Closure table — subjects

| Type | Defining file | Lines | Depends on (✅ ported / ❌ missing) | Disposition |
|---|---|---|---|---|
| `VesselStatus` | `Controller/Vessel/VesselStatus.cs` | 260 | All 10 `RequireComponent` types ✅, `RequireInterface` ✅, `IVesselHUDController` ✅, `Skimmer`/`Prism`/`AICinematicBehavior` ✅, `Material` ✅, `GetOrAdd` ✅ | **LEAF** — every dependency already landed in V1–V19. Concrete impl unblocks the stale `AutoPilotEnabled` deviation in `SilhouetteController` |
| `VesselController` | `Controller/Vessel/VesselController.cs` | 364 | `GameDataSO` ✅, `ShipHelper` ✅, `NetMarkers` ✅, RPC attrs ✅, `Pose` ✅; ❌ `VesselTrailCustomization`, ❌ E12 (`NetworkObjectId` + `NetworkObject.Despawn(bool)`) | **SHALLOW** after E12 + trail tinter |
| `Player` | `Controller/Player/Player.cs` | 529 | `GameDataSO` ✅, `RoundStats` ✅, `InputController` ✅, `ThemeManagerDataContainerSO` ✅, `[Inject]` ✅, RPC attrs ✅, `NetMarkers` ✅; ❌ `PlayerDataService` + `PlayerProfileData`, ❌ E11 (`FixedString128Bytes`), ❌ E12 (`NetworkObjectId`), ❌ E13 (UGS `AuthenticationService` shim) | **SHALLOW** after E11–E13 + profile slice. Closes GameDataSO's `BuildHumanCounts` deviation ("restore when Player ports") |
| `VesselSpawner` | `Controller/Vessel/VesselSpawner.cs` | 56 | `Random.Range` ✅, `Instantiate(Transform)` ✅; ❌ `VesselPrefabContainer`, ❌ E15 (`GameObjectInjector.InjectRecursive` façade + `Container` self-resolve), ❌ E16 (clone intra-hierarchy reference remap) | **SHALLOW** after E15/E16 |
| `PlayerSpawner` | `Controller/Player/PlayerSpawner.cs` | 46 | `RequireInterface` ✅, `Instantiate(Object)` ✅; ❌ VesselSpawner, ❌ E15 | **SHALLOW** (after VesselSpawner) |
| `PlayerSpawnerAdapterBase` | `Controller/Player/PlayerSpawnerAdapterBase.cs` | 44 | `GameDataSO` ✅ (`SetSpawnPositions`, `AddPlayer`), `IPlayer.InitializeData` ✅; ❌ PlayerSpawner | **LEAF** (lands with PlayerSpawner) |
| `MiniGamePlayerSpawnerAdapter` | `Controller/Player/MiniGamePlayerSpawnerAdapter.cs` | 61 | `OnInitializeGame.OnRaised` ✅; ❌ PlayerDataService | **LEAF** (after C2/C6 deps) |
| `VolumeTestPlayerSpawnerAdapter` | `Controller/Player/VolumeTestPlayerSpawnerAdapter.cs` | 12 | `GameDataSO.InitializeGame/SetPlayersActive` ✅ | **LEAF** |

## Closure table — transitive additions (not in `Port/src`)

| Type | Defining file | Lines | Depends on | Disposition |
|---|---|---|---|---|
| `VesselTrailCustomization` | `Controller/Vessel/VesselTrailCustomization.cs` | 62 | ❌ E14 (`Gradient`/`GradientColorKey`/`GradientAlphaKey` + `TrailRenderer.colorGradient`) | **SHALLOW** after E14 |
| `VesselPrefabContainer` | `ScriptableObjects/SOAP/VesselPrefabContainer.cs` | 50 | `IVesselStatus` ✅, `TryGetComponent` ✅, `CSDebug` ✅ | **LEAF** |
| `PlayerProfileData` | `UI/Views/PlayerProfileData.cs` | 15 | pure data | **LEAF** |
| `SO_ProfileIconList` (+`ProfileIcon`) | `ScriptableObjects/SO_ProfileIconList.cs` | 26 | `Sprite` ✅ | **LEAF** |
| `PlayerDataService` | `UI/Views/PlayerDataService.cs` | 363 | `GameDataSO` ✅, `[Inject]` ✅, `Random` ✅; ❌ `SO_ProfileIconList`, ❌ `PlayerProfileData`; **UGS-coupled**: `UGSDataService` (CloudSave repo), `Unity.Services.Core`, `AuthenticationService`, `LogControlWindow` (`#if UNITY_EDITOR`, drops out) | **SHALLOW** with **Deviation #14 extension** — UGS paths commented (same family as `GameSetting`). Class is already null-tolerant: with `_ugsDataService == null` it runs on the local default profile, which is exactly the headless behavior we want. `Instance` static singleton + `OnProfileChanged` + `CurrentProfile` + `IsInitialized` port verbatim |

Excluded from closure (dependents, not dependencies — they are the **next** arc, the
multiplayer spawn pipeline): `ServerPlayerVesselInitializer` (+`WithAI`, `Menu`),
`ClientPlayerVesselInitializer`, `NetcodeHooks`, `MultiplayerSetup`, `DomainAssigner`,
`PlayerSpawner`'s networked twin paths. Also excluded: `UGSDataService` itself
(services phase, Deviation #14 owner) and all HUD controllers (phase 5).

## Engine additions required (E11+, continuing VESSEL_LAYER's E1–E10)

| # | Addition | Needed by | Size guess |
|---|---|---|---|
| E11 | **`FixedString128Bytes`** — clone of `FixedString64Bytes` with 128-byte cap (`Collections/`) | `Player.NetName` | ~50 |
| E12 | **`NetworkObjectId` + `NetworkObject` handle** on `NetworkBehaviour` — monotonically allocated id at `Spawn()`; `NetworkObject` property exposing `Despawn(bool destroy)` (despawn + optional `Object.Destroy(gameObject)`) and `NetworkObjectId`. Keep existing `Spawn()/Despawn()` contract intact | `Player.PlayerNetId`, `VesselController.VesselNetId`, `DestroyPlayer`/`DestroyVessel`, `GameDataSO.TryGet*ByNetworkObjectId` consumers | ~60 |
| E13 | **`Unity.Services.Authentication` placeholder shim** — `AuthenticationService.Instance` with settable `PlayerName`/`PlayerId`/`IsSignedIn` (harness-configurable, defaults benign). Same precedent as the 1-line `ISession` placeholder. Keeps `Player`'s 3-tier name fallback and `PlayerDataService.MergeCloudProfile` verbatim instead of deviation-commenting behavior away | `Player.OnNetworkSpawn` tier-3 name fallback | ~30 |
| E14 | **`Gradient` + `GradientColorKey` + `GradientAlphaKey`** (data-only: `SetKeys`, `alphaKeys`) and `TrailRenderer.colorGradient` | `VesselTrailCustomization` | ~70 |
| E15 | **Reflex façade compat** — `GameObjectInjector.InjectRecursive(GameObject, Container)` static forwarding to `Container.InjectGameObject(go, recursive: true)`; ensure `[Inject] Container _container` resolves (Container self-binding). README substitution row: `using Reflex.Core;`/`using Reflex.Injectors;` → `using CosmicShore.Engine.Injection;` | `VesselSpawner`, `PlayerSpawner` | ~25 |
| E16 | **Prefab-faithful clone: intra-hierarchy reference remap.** `ObjectUtilities.CloneGameObject` currently copies serialized fields by value — `Component`/`GameObject`/`Transform` references (and collections of them) in the clone still point at the *original* hierarchy. Unity remaps these on `Instantiate`. Add a post-clone pass: build old→new maps for every GameObject/Transform/Component in the source tree, then rewrite reference fields (including `List<>`/array elements) that point inside the tree. Without this, the cloned vessel's `VesselStatus._shipInstance`, `_nearFieldSkimmer`, `vesselHUDController`, `orientationHandle` all alias the prefab template | `VesselSpawner.SpawnShip` (and every future prefab-shaped spawn, incl. the multiplayer arc) | ~110 + dedicated tests |

Forward-looking (explicitly **not** this arc, recorded for the multiplayer-spawn arc):
`SpawnWithOwnership(clientId, destroyWithScene)` semantics, RPC wire dispatch beyond
direct-invoke (single-process direct calls are semantically correct until transport,
per VESSEL_LAYER "RPC wire dispatch / phase 4"), `NetcodeHooks`, connection-approval
player-object auto-creation, and Netcode scene-load events.

## Staged-deviation strategy

This arc opens **one** deviation family extension and closes two existing markers:

- **#14-ext (open here, owned by services phase)** — `PlayerDataService` ports with
  its UGS surface commented under `PORT Deviation #14` markers: the
  `[Inject] UGSDataService` field, `HandleDataServiceReady` wiring in
  `Start`/`OnDestroy`, `MergeCloudProfile`'s repo read, and
  `SyncCurrentProfileToRepo`'s body. The local-profile fallback path (already in the
  Unity source) becomes the headless main path. `ApplyPendingDebugCrystals` is
  `#if UNITY_EDITOR` and compiles out naturally.
- **CLOSE: GameDataSO V10 marker** — `// PORT Deviation (V10, restore when Player
  ports)` around `BuildHumanCounts(IEnumerable<Player>, …)` + its doc comment
  (`Utility/DataContainers/GameDataSO.cs:777–797`). Restored in the Player iteration.
- **CLOSE: SilhouetteController V19 marker** — `// PORT Deviation (V19, restore when
  AIPilot ports — IVesselStatus.AutoPilotEnabled …)`
  (`Controller/Vessel/SilhouetteController.cs:140`). The stated blocker is stale:
  `AIPilot` landed in V18 and `IVesselStatus.AutoPilotEnabled` is live (verbatim
  default member). It only ever needed a concrete `IVesselStatus` in tests to
  exercise it — restore alongside the `VesselStatus` port.

Markers that this arc does **not** close (owners noted, verified by grep):
`ScoringRuleSO`/`SO_ArcadeGame` in GameDataSO (scoring/arcade arc),
`MaterialStateManager` in MaterialPropertyAnimator (V14 manager arc),
`PrismScaleManager` in PrismScaleAnimator, `PrismOctahedronShield` in
PrismStateManager, `HealthPrism`/`Fauna` keys in Prism + `Fauna`/`SnowChanger`/
`CellModifier`/`SpawnProfileSO`/`ICellLifeSpawner` keys in Cell/CellConfigDataSO
(flora/fauna arc), `GunVesselTransformer` in TrailFollower,
`ProjectileImpactor`/`ExplosionImpactor`/`ElementalCrystalImpactor`/
`OmniCrystalImpactor` in the impactors + `AOEExplosion` in
VesselExplosionByCrystalEffectSO (projectile/impact arc), `CinematicDefinitionSO`
note (V18), shells #11 (AudioSystem) / #12 (CameraManager) / #13
(SilhouetteView/ElementalBarsView) and #14 core (GameSetting/UGSDataService) — phases
4–5.

## Dependency-ordered porting sequence

Budget ≈300–600 ported game lines per iteration (single-file oversizes accepted).
Engine additions don't count against the budget. Every iteration ends
`dotnet build && dotnet test` green, committed, pushed.

| It | Steps | Game lines | Closes |
|---|---|---|---|
| **C1** | 1. Engine E11, E12, E13, E14, E15, **E16** (+ remap tests: synthetic hierarchy with intra-tree Component/GameObject/list refs round-trips through `Instantiate`). 2. Port LEAFs: `VesselPrefabContainer`, `SO_ProfileIconList`, `PlayerProfileData`, `VesselTrailCustomization`. Tests: FixedString128 truncation, NetworkObjectId allocation, trail gradient re-tint preserves alpha keys. | ~153 | — |
| **C2** | 3. Port `PlayerDataService` (Deviation #14-ext markers on UGS paths; `Instance` singleton, local default profile, crystal/XP math, `OnProfileChanged` chain to `GameDataSO.LocalPlayerDisplayName`). Tests: default profile generation, AddCrystals/TrySpendCrystals/AddXP events, UnlockReward idempotence, SyncProfileToGameData. | ~363 | opens #14-ext |
| **C3** | 4. Port `VesselStatus` (verbatim — all RequireComponent types live). 5. Restore the stale SilhouetteController `AutoPilotEnabled` marker. Tests: `GetOrAdd` lazy component accessors, `ResetForPlay` cascade (ResourceSystem/Transformer/PrismController/Animation calls), `Vessel`/`VesselHUDController` fail-loud null paths, interface-conformance freeze (concrete implements verbatim `IVesselStatus`). | ~270 | SilhouetteController V19 marker |
| **C4** | 6. Port `VesselController` (verbatim — NetworkVariables, RPC direct-invoke pairs, `Initialize` chain). Tests: full `Initialize(player)` ordering incl. local-user branch, double-init rejection, owner kinematic write path (n_Speed/n_Course/n_BlockRotation), `ChangePlayer` AI/client/human matrix, `SetPose` spawned vs local, `DestroyVessel` despawn-vs-destroy, slowed-transform add/remove via GameDataSO. | ~364 | — |
| **C5** | 7. Port `Player` (single-file oversize, verbatim — NetworkVariables, 3-tier name resolution, deferred spawn event, `PrepareForNewScene`, domain mirror). 8. **Restore GameDataSO `BuildHumanCounts`** (V10 marker). Tests: `OnNetworkSpawn` host path raises `OnPlayerNetworkSpawnedUlong` once name+vessel valid, deferred raise on late NetName replication, `RequestSetDomain_ServerRpc` validation vs `IsActiveDomain`, `OnNetDomainChanged` → RoundStats mirror + ShipHelper repaint guard, `InitializeForSinglePlayerMode` defaults (Jade, input paused), `StripPlayerNameSuffix`, `BuildHumanCounts` excludes AI/out-of-set domains. | ~560 | GameDataSO V10 marker |
| **C6** | 9. Port `VesselSpawner`, `PlayerSpawner`, `PlayerSpawnerAdapterBase`, `MiniGamePlayerSpawnerAdapter`, `VolumeTestPlayerSpawnerAdapter`. 10. CLI vertical slice: programmatic vessel "prefab" fixture (GameObject with VesselController + VesselStatus + 10 components + child skimmers, serialized refs wired) registered in a `VesselPrefabContainer`; run the exit-state scenario below as a CLI check + tests (random-class selection, orphaned-player destroy on vessel-spawn failure, adapter spawn-at-start vs OnInitializeGame). | ~219 + harness | — |

Total: **6 iterations**, ≈1,929 ported game lines, ≈345 engine lines.

## Exit state — "concrete arc complete" means

1. `VesselStatus`, `VesselController`, `Player`, both spawners, and all three
   adapters compile **verbatim** (using-swaps only; PlayerDataService's #14-ext
   markers are the lone exception, logged in PORT_PLAN's deviation log).
2. The trio's interface-surface freeze tests still pass untouched — porting the
   implementors required **zero** edits to `IVessel`/`IVesselStatus`/`IPlayer` or any
   V1–V19 file (other than the two marker restorations listed above).
3. **Headless CLI proof** (new check in `CosmicShore.Cli`, also as tests):
   - Build the vessel prefab fixture, clone it through verbatim
     `VesselSpawner.SpawnShip` — E16 remap verified by asserting the clone's
     `VesselStatus.Vessel` is the clone's own `VesselController`, not the template's.
   - `PlayerSpawner.SpawnPlayerAndShip(new IPlayer.InitializeData { vesselClass,
     PlayerName, AllowSpawning = true })` returns a live `IPlayer`;
     `vessel.Initialize(player)` fired `OnInitialized`; `gameData.AddPlayer` set
     `LocalPlayer` and a spawn pose.
   - `player.StartPlayer()` → vessel un-stations, `VesselPrismController.StartSpawn`,
     input unpaused; tick N frames — `VesselTransformer` moves the vessel;
     `ResetForPlay()` restores the documented reset state.
   - Networked variant: `player.Spawn()` (host-mode) drives `OnNetworkSpawn` →
     `gameData.Players` registration + `OnPlayerNetworkSpawnedUlong` raise with the
     name resolved through the PlayerDataService → GameDataSO → auth-shim fallback
     chain; `DestroyPlayer()` despawns via `NetworkObject.Despawn(true)`.
   - AI variant: `InitializeForSinglePlayerMode(IsAI: true)` + `StartPlayer()`
     toggles `AIPilot` on and keeps input paused.
4. `dotnet build && dotnet test` green; GameDataSO V10 `BuildHumanCounts` and
   SilhouetteController V19 markers gone from the `PORT Deviation` grep; #14-ext
   recorded.
5. The multiplayer spawn pipeline (`ServerPlayerVesselInitializer` family,
   `ClientPlayerVesselInitializer`, `NetcodeHooks`, `MultiplayerSetup`,
   `DomainAssigner`) is unblocked as the next arc — its remaining engine needs are
   exactly the forward-looking E-notes above (SpawnWithOwnership/destroyWithScene,
   RPC dispatch, scene-load events).
