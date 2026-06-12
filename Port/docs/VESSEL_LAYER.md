# Vessel Layer — Type-Closure Survey & Porting Sequence

Iteration-6 survey (PORT_PLAN "NEXT UP" step 1). Maps the full type closure needed to
port these files **verbatim** and to restore Deviation #9 (ResourceSystem base class +
`[RequireComponent(typeof(IVesselStatus))]`):

- `Controller/Vessel/IVessel.cs` (64)
- `Controller/Vessel/IVesselStatus.cs` (139)
- `Controller/Player/IPlayer.cs` (87)
- `Controller/Vessel/ElementalFloat.cs` (50)
- `Controller/Vessel/ElementalVesselComponent.cs` — class `ElementalShipComponent` (32)

All paths below are under `Assets/_Scripts/` unless noted. Dispositions:
**ALREADY-PORTED** (in `Port/src/`) · **LEAF** (port now, using-swaps only) ·
**SHALLOW** (port after listed engine additions) · **DEEP** (drags further game
systems — listed) · **PHASE-LATER** (rendering/audio/input/UI-bound).

**Closure size: ~80 types in 73 files, ≈13,100 lines** (372 target + ≈12,730
unported closure). Engine `Material` and `Pose` already landed
(`Engine/Rendering/Material.cs`, `Engine/Math/Pose.cs`) — verified compatible with
every use site in this closure.

## Already-ported types referenced by the closure

No action needed; listed so the tables below can omit them from "depends on".

| Category | Types |
|---|---|
| Engine core | `Transform`, `GameObject`, `Component`, `MonoBehaviour`, `ScriptableObject`, `Object` (fake-null), coroutines + `WaitForSeconds`, `Time`, `Debug`, `Mathf` (incl. `LerpUnclamped`), `Vector2/3`, `Quaternion`, `Color`, **`Material`/`Shader`**, **`Pose`**, `LayerMask`, attributes (`SerializeField/Header/Tooltip/Range/HideInInspector/RequireComponent/FormerlySerializedAs`) |
| Engine net | `NetworkBehaviour` (`IsSpawned/IsOwner/IsServer/IsClient`, despawn virtuals), `NetworkVariable<T>` (perm-aware) |
| Engine SOAP | `ScriptableEvent<T>`, `ScriptableEventNoParam/String/Bool/Ulong`, `ScriptableVariable<T>`, `IntVariable`, listeners |
| Data | `Domains`, `Element`, `InputEvents`, `InputDeviceType`, `ResourceEvents`, `VesselClassType`, `ResourceCollection`, `IRoundStats`, `RoundStats`, `DomainStats`, `ShipThrottleModifier`, `ShipVelocityModifier` |
| Game | `ResourceSystem` + `Resource` (Deviation #9 pending), `CSDebug`, `DebugExtensions`, `TransformExtensions` (`ResizeForSeconds`/`CancelResize`), `GameObjectExtension` (`GetOrAdd`), `PrismType`, `AbilityStats`, `CameraSettingsSO` + `CameraMode`, `ScriptableEventInputEvents`, `ScriptableEventAbilityStats`, `ScriptableEventPrismStats`, `ScriptableEventTransform`, `PrismEventChannelWithReturnSO`, `VesselClassTypeVariable` |

## Closure table — tier 1 (named directly by the five target files)

| Type | Defining file | Lines | Depends on (Unity / game) | Disposition |
|---|---|---|---|---|
| `ITransform` | `Utility/ITransform.cs` | 8 | Transform | **LEAF** |
| `IInputStatus` | `Controller/IO/IInputStatus.cs` | 55 | `ScreenOrientation` (engine enum needed); InputController, ScriptableEventInputEvents | **SHALLOW** — cyclic with InputController (see SCC note) |
| `InputController` | `Controller/IO/InputController.cs` | 275 | UnityEngine.InputSystem (`Keyboard/Gamepad/EnhancedTouchSupport`), `Screen/SystemInfo/Application`; IVessel, GameSetting, PauseSystem, all input strategies, MultiMouseService, DeviceOrientationHandler | **SHALLOW** after inert input-device shim |
| `InputStatus` | `Controller/IO/InputStatus.cs` | 292 | NetworkBehaviour/NetworkVariable (✅); IInputStatus, InputController, ScriptableEventInputEvents | **SHALLOW** |
| `AIPilot` (+`AIAbility`) | `Controller/AI/AIPilot.cs` | 419 | `Physics.Raycast`/`RaycastHit`/`Debug.DrawLine` (unused-region only), coroutines (✅), `Instantiate(SO)`; GameDataSO, CellRuntimeDataSO, CellItem, ShipActionSO, ActionExecutorRegistry, IVessel/IVesselStatus | **DEEP** — drags GameDataSO + cell substrate |
| `AICinematicBehavior` | `Utility/DataContainers/AICinematicBehavior.cs` | 265 | Transform; IVesselStatus, AIPilot | **SHALLOW** (after AIPilot) |
| `Prism` | `Controller/Vessel/Prism.cs` | 484 | MeshRenderer, BoxCollider (stubs); MaterialPropertyAnimator, PrismScaleAnimator, PrismTeamManager, PrismStateManager, Cell, Trail, PrismProperties, PrismAOERegistry, AudioSystem | **DEEP** — prism cluster + cell + audio |
| `VesselAnimation` | `Controller/Animation/VesselAnimation.cs` | 152 | SkinnedMeshRenderer (`SetBlendShapeWeight`, `materials`); IVesselStatus, IInputStatus, ResourceSystem (✅) | **SHALLOW** after renderer stubs |
| `VesselCameraCustomizer` | `Controller/Vessel/VesselCameraCustomizer.cs` | 77 | ScriptableEventTransform (✅), CameraSettingsSO (✅); ElementalShipComponent, ICameraController, ICameraConfigurator, CustomCameraController, CameraManager | **DEEP** — CameraManager is Cinemachine-bound (shell, Deviation #12) |
| `VesselTransformer` | `Controller/Vessel/VesselTransformer.cs` | 518 | Pose (✅); ElementalFloat, ScriptableEventBoostChanged, BoostChangedPayload, SafeLookRotation, VesselAnimation, modifier structs (✅) | **SHALLOW** |
| `Skimmer` | `Controller/Vessel/Skimmer.cs` | 196 | TransformExtensions (✅), coroutines (✅); ElementalShipComponent, ElementalFloat, Prism, Trail, TrailFollowerDirection, NudgeShard(PoolManager), SafeLookRotation, ScriptableEventString (✅) | **DEEP** — via Prism + pool/audio chain |
| `SilhouetteController` | `Controller/Vessel/SilhouetteController.cs` | 244 | — ; VesselPrismController, DriftTrailActionExecutor, SilhouetteConfigSO, SilhouetteView, ElementalBarsView, VesselExplosionByCrystalEffectSO, VesselImpactor | **DEEP** — UI views + impact-effects slice (Deviation #13) |
| `VesselPrismController` | `Controller/Vessel/VesselPrismController.cs` | 375 | GameTask (✅), Material (✅), PrismType (✅); Skimmer, Trail, Prism | **DEEP** — via prism cluster |
| `IVesselHUDController` | `UI/Interfaces/IVesselHUDController.cs` | 16 | GameObject; IVesselStatus | **LEAF** (also anchors the `CosmicShore.UI` namespace for using-directives) |
| `VesselCustomization` | `Controller/Vessel/VesselCustomization.cs` | 65 | — ; IVesselStatus, ShipHelper | **SHALLOW** |
| `R_VesselActionHandler` (+2 mapping structs) | `Controller/Vessel/R_VesselActionHandler.cs` | 321 | `[ServerRpc]`/`[ClientRpc]` attrs (engine), GameTask (✅); ActionExecutorRegistry, ShipActionSO, ShipHelper, NetMarkers, ScriptableEventInputEventBlock, InputEventBlockPayload, AbilityStats (✅) | **SHALLOW** after RPC attrs + profiling shim |
| `R_ShipElementStatsHandler` (+`ElementStat`) | `Controller/Vessel/R_VesselElementStatsHandler.cs` | 32 | — ; Element (✅) | **LEAF** |
| `ResourceSystem` | (ported) | — | — | **ALREADY-PORTED** — Deviation #9 restores here |

## Closure table — tier 2/3 (transitive)

| Type | Defining file | Lines | Depends on | Disposition |
|---|---|---|---|---|
| `SafeLookRotation` | `Utility/SafeLookRotation.cs` | 47 | DebugExtensions (✅) | **LEAF** |
| `PauseSystem` | `System/PauseSystem.cs` | 28 | — | **LEAF** |
| `Singleton<T>` / `SingletonPersistent<T>` | `Utility/Singleton.cs` | 114 | `DontDestroyOnLoad`, `FindFirstObjectByType` (engine) | **SHALLOW** |
| `BoostChangedPayload` | `UI/View/BoostChangedPayload.cs` | 13 | Domains (✅) | **LEAF** |
| `ScriptableEventBoostChanged` | `UI/View/ScriptableEventBoostChanged.cs` | 9 | SOAP (✅) | **LEAF** |
| `InputEventBlockPayload` | `UI/Controller/InputEventBlockPayload.cs` | 15 | InputEvents (✅) | **LEAF** |
| `ScriptableEventInputEventBlock` | `UI/Controller/ScriptableEventInputEventBlock.cs` | 13 | SOAP (✅) | **LEAF** |
| `NetMarkers` | `Utility/PerformanceBenchmark/NetMarkers.cs` | 46 | `Unity.Profiling` (engine no-op shim) | **SHALLOW** |
| `ShipActionSO` | `Controller/Vessel/R_VesselActions/Data Containers/VesselActionSO.cs` | 21 | IVesselStatus, ActionExecutorRegistry | **LEAF** (lands with trio) |
| `ShipActionExecutorBase` | `Controller/Vessel/R_VesselActions/Executors/VesselActionExecutorBase.cs` | 10 | — | **LEAF** |
| `ActionExecutorRegistry` | `Controller/Vessel/R_VesselActions/Executors/ActionExecutorRegistry.cs` | 38 | Reflex `[Inject]` (✅); ShipActionExecutorBase | **LEAF** |
| `ShipAction` (legacy) | `Controller/Vessel/VesselActions/VesselAction.cs` | 23 | ElementalShipComponent, IVessel | **LEAF** (lands with trio; needed by ShipHelper) |
| `ShipHelper` | `Controller/Vessel/VesselHelper.cs` | 167 | SkinnedMeshRenderer/MeshRenderer (stubs); IVessel/IVesselStatus, ShipActionSO, ShipAction, ThemeManagerDataContainerSO | **SHALLOW** |
| `ThemeManagerDataContainerSO` | `Controller/Managers/ThemeManagerDataContainerSO.cs` | 91 | Material (✅); domain color-set SO (drag: `SO_ColorSet`-adjacent, small) | **SHALLOW** |
| `GameSetting` | `Controller/Settings/GameSetting.cs` | 282 | `PlayerPrefs` (engine); SingletonPersistent | **SHALLOW** |
| `GameDataSO` | `Utility/DataContainers/GameDataSO.cs` | 783 | `ISession` (Unity.Services.Multiplayer — 1-line engine placeholder), Netcode refs, Pose (✅); IPlayer, IVessel, IRoundStats (✅), DomainStats (✅), SOAP events (✅) | **DEEP** — but self-contained once trio + placeholder exist |
| `CellItem` (+`ItemType`) | `Controller/Environment/MiniGameObjects/CellItem.cs` | 30 | Domains (✅) | **LEAF** |
| `CellRuntimeDataSO` | `Utility/DataContainers/CellRuntimeDataSO.cs` | 211 | SOAP (✅); Cell, CellItem | **DEEP** — via Cell |
| `Cell` | `Controller/Environment/Cell.cs` | 825 | Reflex (✅); GameDataSO, CellConfigDataSO, CellItem, BlockDensityGrid, flora/fauna touchpoints | **DEEP** — phase-2 simulation core (already roadmapped) |
| `CellConfigDataSO` | `Utility/DataContainers/CellConfigDataSO.cs` | 39 | SO (✅) | **LEAF** |
| `BlockDensityGrid` | `Controller/Managers/BlockDensityGrid.cs` | 460 | pure logic + Prism refs | **SHALLOW** (NEXT-UP item 3 already targets it) |
| `Trail` | `Controller/Vessel/Trail.cs` | 207 | `TrailRenderer` (stub: field + `Clear()`); Prism | **SHALLOW** — cyclic with Prism |
| `TrailFollower` (+`TrailFollowerDirection`) | `Controller/Vessel/TrailFollower.cs` | 145 | — ; Trail, IVesselStatus | **SHALLOW** |
| `PrismProperties` | `Controller/Environment/PrismProperties.cs` | 22 | — ; Prism, Trail | **LEAF** (lands with prism cluster) |
| `MaterialPropertyAnimator` | `Controller/Environment/Prisms/MaterialPropertyAnimator.cs` | 213 | MeshRenderer/MaterialPropertyBlock-style access (Material ✅); ThemeManagerDataContainerSO | **SHALLOW** |
| `PrismScaleAnimator` | `Controller/Environment/Prisms/PrismScaleAnimator.cs` | 172 | — ; Prism, ScriptableEventPrismStats (✅) | **SHALLOW** |
| `PrismTeamManager` | `Controller/Managers/PrismTeamManager.cs` | 130 | — ; ThemeManagerDataContainerSO, Domains (✅) | **SHALLOW** |
| `PrismStateManager` (+`BlockState`) | `Controller/Managers/PrismStateManager.cs` | 172 | coroutines (✅) | **SHALLOW** |
| `PrismAOERegistry` | `Controller/Managers/PrismAOERegistry.cs` | 441 | Unity Jobs/Burst `NativeArray` → managed-array port (phase-2 Jobs policy) | **SHALLOW** (logic port, drop Burst until profiled) |
| `GenericPoolManager<T>` | `Utility/PoolsAndBuffers/GenericPoolManager.cs` | 239 | `UnityEngine.Pool.ObjectPool<T>` + `Instantiate(Component)` (engine), GameTask (✅) | **SHALLOW** |
| `NudgeShardPoolManager` | `Controller/Environment/NudgeShardPoolManager.cs` | 11 | GenericPoolManager | **LEAF** |
| `NudgeShard` | `Controller/Environment/Cytoplasm/NudgeShard.cs` | 49 | — ; AudioSystem (`[Inject]` field), Prism, GenericPoolManager | **DEEP** — via AudioSystem |
| `AudioSystem` | `System/Audio/AudioSystem.cs` | 717 | Wwise | **PHASE-LATER** (phase 5) — type-preserving shell now (Deviation #11) |
| `IInputStrategy` | `Controller/IO/IInputStrategy.cs` | 19 | — | **LEAF** |
| `BaseInputStrategy` | `Controller/IO/BaseInputStrategy.cs` | 58 | — ; IInputStatus | **LEAF** |
| `KeyboardInputStrategy` | `Assets/KeyboardInputStrategy.cs` (loose root file!) | 297 | InputSystem shim | **SHALLOW** — relocate noted in header comment, file content verbatim |
| `GamepadInputStrategy` | `Controller/IO/GamepadInputStrategy.cs` | 255 | InputSystem shim | **SHALLOW** |
| `TouchInputStrategy` | `Controller/IO/TouchInputStrategy.cs` | 352 | InputSystem/EnhancedTouch shim | **SHALLOW** |
| `DualMouseInputStrategy` | `Controller/IO/DualMouseInputStrategy.cs` | 298 | MultiMouseService | **SHALLOW** |
| `MultiMouseService` | `Controller/IO/MultiMouse/MultiMouseService.cs` | 78 | raw-input backend (headless: `HasTwoMice=false`) | **SHALLOW** |
| `DeviceOrientationHandler` | `Controller/IO/DeviceOrientationHandler.cs` | 167 | gyro/attitude sensor (shim returns identity) | **SHALLOW** |
| `ICameraController` | `Controller/Camera/ICameraController.cs` | 19 | CameraSettingsSO (✅) | **LEAF** |
| `ICameraConfigurator` | `Controller/Camera/ICameraConfigurator.cs` | 7 | — | **LEAF** |
| `CustomCameraController` | `Controller/Camera/CustomCameraController.cs` | 192 | `Camera` (engine data stub: orthographic/size/transform) | **SHALLOW** |
| `CameraManager` | `Controller/Managers/CameraManager.cs` | 233 | **Unity.Cinemachine** | **PHASE-LATER** (phase 5) — shell (Deviation #12) |
| `SilhouetteConfigSO` | `Controller/Vessel/SilhouetteConfigSO.cs` | 42 | domain palette SO (small drag) | **LEAF** |
| `SilhouetteView` | `Controller/Vessel/SilhouetteView.cs` | 305 | UI/render objects | **PHASE-LATER** — shell (Deviation #13) |
| `ElementalBarsView` | `UI/View/ElementalBarsView.cs` | 544 | UnityEngine.UI `Image`, DOTween, `ElementalBarsConfigSO` | **PHASE-LATER** — shell (Deviation #13) |
| `DriftTrailActionExecutor` | `Controller/Vessel/R_VesselActions/Executors/DriftTrailActionExecutor.cs` | 91 | GameTask (✅), SOAP (✅); ShipActionExecutorBase, VesselTransformer | **SHALLOW** |
| `VesselExplosionByCrystalEffectSO` | `Controller/ImpactEffects/EffectsSO/Vessel Crystal Effects/VesselExplosionByCrystalEffectSO.cs` | 96 | impact-effect SO base chain | **DEEP** — first slice of the phase-2 impact matrix (only its static event is needed by SilhouetteController) |
| `VesselImpactor` | `Controller/ImpactEffects/Impactors/VesselImpactor.cs` | 100 | `ImpactorBase` + impact plumbing | **DEEP** — same slice |

Unreferenced near-misses, excluded from closure: `KeyboardMouseInputStrategy.cs` (281,
not constructed by InputController — the loose `Assets/KeyboardInputStrategy.cs` is the
live one).

## Engine additions required (beyond Material/Pose, already done)

| # | Addition | Needed by | Size guess |
|---|---|---|---|
| E1 | **Inert Input System shim** — `Keyboard/Gamepad/Mouse.current` return inert devices (no keys pressed), `EnhancedTouchSupport` no-op + empty touches, attitude sensor → identity | InputController + all 4 strategies, DeviceOrientationHandler | ~250 |
| E2 | **Renderer data stubs** — `Renderer`/`MeshRenderer`/`SkinnedMeshRenderer` (`materials`, `sharedMaterial`, `SetBlendShapeWeight` no-op), `TrailRenderer` (`Clear`), `Camera` (data-only) | VesselAnimation, ShipHelper, Prism, Trail, CustomCameraController | ~150 |
| E3 | **`[ServerRpc]`/`[ClientRpc]` attribute stubs** (local-invoke semantics until phase 4 wires transport) | R_VesselActionHandler, InputStatus | ~20 |
| E4 | **`Unity.Profiling` no-op shim** (`ProfilerMarker.Auto()`, `ProfilerCounterValue<T>`) | NetMarkers | ~60 |
| E5 | `ScreenOrientation` + `DeviceType` enums; `Screen`/`SystemInfo`/`Application` statics | IInputStatus, InputController | ~50 |
| E6 | `PlayerPrefs` (in-memory + JSON flush) | GameSetting | ~80 |
| E7 | `Object.DontDestroyOnLoad`, `Object.FindFirstObjectByType<T>` | Singleton.cs, InputController fallback | ~30 |
| E8 | `Object.Instantiate` minimal: ScriptableObject clone + Component/GameObject clone (pool path only; full prefab factories stay content-phase) | AIPilot (SO clone), GenericPoolManager | ~80 |
| E9 | `UnityEngine.Pool.ObjectPool<T>` | GenericPoolManager | ~100 |
| E10 | `Physics.Raycast` (returns false) + `RaycastHit` + `Debug.DrawLine` no-op; `BoxCollider`/`SphereCollider` data stubs | AIPilot (unused region), Prism | ~80 |

## Cycle (SCC) analysis — why the sequence uses staged deviations

The three interfaces sit at the center of the layer: `IVesselStatus` names 16 concrete
classes; most of those classes reference the interfaces back. Two strongly-connected
components emerge:

1. **IO SCC**: `IInputStatus ↔ InputController ↔ {4 strategies, MultiMouseService,
   DeviceOrientationHandler}` (~1,850 lines) — joined to the trio via
   `InputController.vessel : IVessel`.
2. **Vessel SCC**: trio + ElementalFloat/ElementalShipComponent + all 16 tier-1
   classes + their co-resident deps (~4,200+ lines).

Landing either SCC atomically blows the 300-600-line iteration budget. Per the
Deviation-#9 precedent, the sequence stages them with **tracked temporary deviations**
(each one logged in PORT_PLAN, each closed by a later step):

- **#10a** — `IInputStatus` lands with the `InputController` property +
  `GetGyroRotation()` commented; restored when InputController lands.
- **#10b** — `InputController` lands with its private `IVessel vessel` field
  commented; restored when the trio lands.
- **#10c** — `IVesselStatus` lands with the 11 members typed by not-yet-ported classes
  commented (`AIPilot`, `AICinematicBehavior`, `AttachedPrism`, `VesselAnimation`,
  `VesselCameraCustomizer`, `NearFieldSkimmer`/`FarFieldSkimmer`, `Silhouette`,
  `VesselTransformer`, `VesselPrismController`, `ActionHandler`); each later iteration
  uncomments the members whose types just landed. Final iteration ⇒ verbatim.
- **#11** — `AudioSystem` type-preserving shell (public surface used by closure:
  `Instance`, `PlayGameplaySFX`; bodies no-op) until phase 5 Wwise replacement.
- **#12** — `CameraManager` shell (Cinemachine-bound) until phase 5.
- **#13** — `SilhouetteView` / `ElementalBarsView` shells (UI `Image` + DOTween-bound)
  until phase 5; `SilhouetteController` itself ports verbatim against the shells.

## Dependency-ordered porting sequence

Budget ≈300-600 ported game lines per iteration (single-file oversizes accepted where
a file is indivisible). Engine additions don't count against the budget.

| It | Steps | Game lines | Closes |
|---|---|---|---|
| **V1** ✅ | 1. Engine E3, E4, E5, E6, E7, E8(SO-clone), E10. 2. Port PauseSystem, SafeLookRotation, Singleton.cs, NetMarkers, BoostChangedPayload(+event), InputEventBlockPayload(+event), CellItem. | ~315 | — |
| **V2** ✅ | 3. Engine E1 (input shim). 4. Port IInputStatus (Deviation #10a), IInputStrategy, BaseInputStrategy, KeyboardInputStrategy. | ~430 | — |
| **V3** ✅ | 5. TouchInputStrategy, GamepadInputStrategy. | ~607 | — |
| **V4** ✅ | 6. DualMouseInputStrategy, MultiMouseService, DeviceOrientationHandler. | ~543 | — |
| **V5** ✅ | 7. GameSetting. 8. InputController (Deviation #10b), restore #10a. | ~557 | #10a |
| **V6** ✅ | 9. **Keystone**: ITransform, IVessel, IPlayer, IVesselStatus (Deviation #10c), ElementalFloat, ElementalShipComponent, IVesselHUDController, R_ShipElementStatsHandler, ShipActionSO, ShipActionExecutorBase, ActionExecutorRegistry, ShipAction (legacy); restore #10b. 10. **Restore Deviation #9** (ResourceSystem : ElementalShipComponent + RequireComponent). Tests: ElementalFloat LerpUnclamped scaling, reflective binding, ResourceSystem regression. | ~520 | #10b, **#9** |
| **V7** ✅ | 11. Engine E2 (renderer stubs). 12. InputStatus, VesselAnimation (+member restore). | ~444 | — |
| **V8** ✅ | 13. VesselTransformer (+member restore). | ~518 | — |
| **V9** ✅ | 14. Engine E9. 15. ShipHelper, ThemeManagerDataContainerSO, VesselCustomization (+restore), GenericPoolManager. | ~562 | — |
| **V10** 🔄 | 16. GameDataSO (single-file oversize) + engine `ISession` placeholder. | ~783 | — |
| **V11** 🔄 | 17. CellConfigDataSO, BlockDensityGrid, CellRuntimeDataSO. | ~710 | — |
| **V12** | 18. Cell (single-file oversize; flora/fauna touchpoints stay event-shaped per ECOSYSTEM rules — conserved mass untouched). | ~825 | — |
| **V13** 🔄 | 19. PrismStateManager, PrismTeamManager, PrismScaleAnimator. | ~474 | — |
| **V14** 🔄 | 20. MaterialPropertyAnimator, Trail, PrismProperties, TrailFollower. | ~587 | — |
| **V15** | 21. AudioSystem shell (Deviation #11), PrismAOERegistry (managed-array port). 22. Prism (+`AttachedPrism` restore). | ~925* | — |
| **V16** | 23. NudgeShardPoolManager, NudgeShard, Skimmer (+skimmer member restores). 24. DriftTrailActionExecutor. | ~347 | — |
| **V17** | 25. VesselPrismController (+restore). 26. R_VesselActionHandler (+restore). | ~696 | — |
| **V18** | 27. AIPilot, AICinematicBehavior (+restores). | ~684 | — |
| **V19** | 28. ICameraController, ICameraConfigurator, CustomCameraController, CameraManager shell (Deviation #12), VesselCameraCustomizer (+restore). 29. VesselImpactor + VesselExplosionByCrystalEffectSO (first impact-matrix slice), SilhouetteConfigSO, view shells (Deviation #13), SilhouetteController (+final restores). 30. **Close #10c — IVesselStatus verbatim.** Interface-surface freeze test; CLI vertical-slice growth (NEXT-UP item 4). | ~770* | **#10c** |

\* V15/V19 run hot; split at commit granularity if the green-build invariant gets
uncomfortable — each numbered step is independently green.

**Exit state after V19**: IVessel + IVesselStatus + IPlayer compile **verbatim**;
ElementalFloat + ElementalShipComponent ported (V6); Deviation #9 closed (V6);
deviations #11/#12/#13 open and owned by phase 5; VesselStatus itself (the concrete
NetworkBehaviour, not in this closure's targets) unblocks for the next arc.

## Genuinely phase-bound (cannot port functionally before their phase)

| What | Phase | Notes |
|---|---|---|
| `ElementalBarsView`, `SilhouetteView` | 5 (UI/render + DOTween) | shells only until then |
| `CameraManager` | 5 (Cinemachine replacement) | shell; `CustomCameraController` is portable as data |
| `AudioSystem` | 5 (Wwise replacement) | shell; SOAP `GameplaySFX` channel already ported |
| Real input devices (strategy behavior) | 5 (input backends) | strategies port + compile now; produce zero input headlessly — AI drives `InputStatus` directly, so headless sim is unaffected |
| `Physics.Raycast` realism | 2-physics design | only AIPilot's unused `ShootLaser` region calls it; stub returning false is behavior-safe |
| RPC wire dispatch | 4 (transport) | `[ServerRpc]`/`[ClientRpc]` stubs invoke locally — semantics identical in single-process sim |
