# Vessel Actions — The Ability System

Reference for the vessel action (ability) system: dispatch pipeline, the
complete current SO + executor inventory, resource-slot conventions, per-vessel
asset instances, and the legacy system. Shared vessel architecture is in
`ARCHITECTURE.md`; per-class bindings in `VESSEL_CLASSES.md`.

Paths repo-relative under `Assets/_Scripts/` unless noted.

---

## 1. Two systems, one current

- **CURRENT — `R_VesselActions`** (SO-config + scene-executor): abilities are
  **`ShipActionSO`** ScriptableObject assets (stateless config,
  `Assets/_SO_Assets/VesselActions/<Vessel>/*.asset`) dispatched through a
  per-vessel **`ActionExecutorRegistry`** of **`ShipActionExecutorBase`**
  MonoBehaviours that hold all runtime state. All 6 playable vessels (Manta,
  Dolphin, Rhino, Squirrel, Serpent, Sparrow) use this path.
- **LEGACY — `VesselActions/`** (stateful MonoBehaviour `ShipAction`
  subclasses on the prefab): still compiled; remains on the unfinished
  vessels — Urchin (`FireGunAction` etc.), Grizzly, Termite (drones),
  Falcon/Shrike (`BoostAction`, `FullAutoAction`) — plus one straggler on
  Squirrel (`ToggleAlignAction`, alongside its R_ `ToggleAlignActionSO`
  asset).
- The `ShipActions` enum (`Data/Enums/VesselActions.cs`) is **vestigial** —
  bindings are keyed by `InputEvents`, and only `EnumIntegrityTests` reads it.
- `ResourceEvents` (`AboveThreeQuartersAmmo`, `AboveHalfAmmo`) wiring exists on
  the handler but **nothing fires resource-event actions** — dead path.

## 2. Dispatch pipeline

```
Input strategy (Gamepad / Touch / Keyboard / DualMouse)
  └─ InputStatus.OnButtonPressed.Raise(InputEvents.X)     [SOAP, global channel]
      └─ R_VesselActionHandler.OnButtonPressed             [subscribed for local user only;
          │                                                 skipped when AutoPilotEnabled or muted]
          ├─ spawned owner: SendButtonPressed_ServerRpc → ClientRpc
          │                 → PerformShipControllerActions on EVERY peer
          └─ else: PerformShipControllerActions locally
              └─ ResolveActions (touch/gamepad override dict → shared dict)
                  └─ foreach ShipActionSO: StartAction(ActionExecutorRegistry, IVesselStatus)
                      └─ execs.Get<XExecutor>().Begin(so, status)
Release → StopShipControllerActions → onAbilityExecuted.Raise(AbilityStats) → so.StopAction(...)
```

`R_VesselActionHandler` (`Controller/Vessel/R_VesselActionHandler.cs`,
NetworkBehaviour, required on every vessel):

- Mappings: `List<InputEventShipActionMapping> { InputEvents → List<ShipActionSO> }`
  shared + `_touchActionOverrides` + `_gamepadActionOverrides` (device override
  wins, resolved by `InputStatus.ActiveInputDevice`).
- `Initialize(IVesselStatus)` → `_executors.InitializeAll(status)` + builds the
  dictionaries via `ShipHelper.InitializeShipControlActions` (assets used
  directly — **no runtime clones**); local users also subscribe
  `InputStatus.OnToggleInputPaused → ToggleSubscription(!paused)` — this is how
  `SetPause` stops abilities.
- `ToggleSubscription(bool)` — SOAP channel subscribe/unsubscribe; toggled on
  only for local users (`VesselController.Initialize` / `ChangePlayer`).
- **Mute system**: `MuteInput(InputEvents, seconds)` (max-extends per-input
  deadline, raises `ScriptableEventInputEventBlock` Started/Ended payloads —
  the HUD debuff visualization). Used by
  `VesselChangeSpeedByExplosionEffectSO`, `VesselDamageBySkimmerEffectSO`,
  `SparrowDebuffByRhinoDangerPrismEffectSO`.
- On stop, raises `onAbilityExecuted (ScriptableEventAbilityStats)` with
  `{PlayerName, ControlType, Duration}` → StatsManager ability-active-time.
- AI/synthetic callers bypass SOAP and call
  `IVessel.Perform/StopShipControllerActions` directly (`AIPilot`,
  `AICinematicBehavior`, impact-effect force-stops).

**Multiplayer discipline**: because actions replay on every peer via
ClientRpc, action SOs raising global SOAP HUD events must self-gate on
`vesselStatus.IsLocalUser` (e.g. `DriftActionSO`) or include `VesselStatus` in
the payload for receiver-side filtering (`ScriptableEventBoostChanged`).

**Executor pattern**:

```csharp
// Data Containers/VesselActionSO.cs
public abstract class ShipActionSO : ScriptableObject {
    public abstract void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus);
    public abstract void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus);
}
// Executors/ActionExecutorRegistry.cs — [Inject] AudioSystem; Get<T>() by concrete type
// Executors/VesselActionExecutorBase.cs — base MonoBehaviour with Initialize(IVesselStatus)
```

Executor conventions: UniTask loops linked to `GetCancellationTokenOnDestroy`;
every executor subscribes SOAP `OnMiniGameTurnEnd` in `OnEnable` and hard-resets
on turn end.

## 3. Current action SO inventory

All in `Controller/Vessel/R_VesselActions/Data Containers/` unless noted.
CreateAssetMenu path `ScriptableObjects/Vessel Actions/...` (ChargeBoost oddly
under `ScriptableObjects/Actions/`).

| SO class | Behavior | Notable config | Executor |
|---|---|---|---|
| `ApplyRotationActionSO` | One-shot pitch/yaw/roll kick | `rotationAmount=45`, pitch/yaw/roll bools | — (direct) |
| `BoostActionSO` | Hold-to-boost (`IsBoosting=true`); SFX BoostActivate | — | — (direct) |
| `ChargeBoostActionSO` | Hold to charge resource slot, release to discharge as boost | `maxBoostMultiplier=2`, `chargeTimeToFull=2`, `dischargeTimeToEmpty=2`, `boostResourceIndex=1`, cooldown 1 s | `ChargeBoostActionExecutor` |
| `ConsumeBoostActionSO` | Magazine of stacking boost pips (each `boostDuration=4`); auto-reload | `maxCharges 1..4`, `reloadCooldown=3`, optional resource cost | `ConsumeBoostActionExecutor` — ⚠️ multiplier hardcoded `4^stacks`, SO field ignored |
| `DeployTeamCrystalActionSO` | Press: ghost crystal ahead; release: activate as own-domain crystal | `forwardOffset=12`, `fadeValue=0.5`; ⚠️ `cooldown=30` unused by executor | `DeployTeamCrystalActionExecutor` |
| `DriftActionSO` | Drift steering: `BeginDrift(Mult, damping, sharp)` + `IsDrifting`; HUD SOAP events **local-user only** | `Mult=1.5`, `driftDamping`, `isSharpDrifting`; SFX DriftStart/End | — (direct) |
| `DriftTrailActionSO` | 0.05 s loop: dot(Course, forward) → `VesselPrismController.SetDotProduct` (drift trail shaping) | — | `DriftTrailActionExecutor` |
| `FireGunActionSO` | Single shot per press via `Gun.FireGun` with inherited velocity | `ammoIndex=0`, `ammoCost=0.03`, `speed=90`, `projectileTime` (ElementalFloat) | `FireGunActionExecutor` |
| `FullAutoActionSO` | Hold-to-fire loop from N muzzles | `firingRate`, `firingPattern`, `energy`, `speedValue` (EF) | `FullAutoActionExecutor` |
| `FullAutoBlockShootActionSO` | Hold-to-fire loop launching **Prism blocks** that fly then anchor | `fireRate=0.75`, `blockSpeed=50`, stop 90-100, `blockScale=(20,2,6)`, `prismType=Sparrow` | `FullAutoBlockShootActionExecutor` |
| `GrowSkimmerActionSO` | Hold to grow skimmer Z-scale, release shrinks; optional boost while growing; `ApplyMaxSizeDebuff` hook | `maxSize(EF)=3`, `growRate=1.5`, `shrinkRate(EF)` | `GrowSkimmerActionExecutor` (IScaleProvider) |
| `GrowTrailActionSO` | Hold to grow trail X/Y/Z scalers + gap | `maxSize(EF)=3`, weights X/Y/Z/Gap | `GrowTrailActionExecutor` (IScaleProvider) |
| `ModifyVelocityActionSO` | One-shot forward velocity burst; SFX SpeedBurst | `magnitude`, `duration` | — (direct) |
| `OverheatingActionSO` | Decorator: runs `wrappedAction` while building heat; at max forces overheat (danger trail + squash) for `overheatDuration`, then decays | `heatResourceIndex`, `heatBuildRate`, `heatDecayRate(EF)`, `dangerPrismMaterial`, `overheatScaleMultiplier=(0.7,1,0.7)` | `OverheatingActionExecutor` |
| `SeedWallActionSO` | Seed latest trail prism with an assembler, shield it, spend resource | `assemblerType{Wall,Gyroid}` (⚠️ both map to `WallAssembler`), `shieldOnSeed=SuperShield`, `bondingDepth=50`, cost = `MaxAmount/enhancementsPerFullAmmo(3)` | `SeedAssemblerActionExecutor` |
| `ShardToggleActionSO` | Toggle cell shard-field redirect at densest opposing mass vs restore | `domain` | `ShardToggleActionExecutor` — ⚠️ `ShardFieldBus` broadcasts commented out (visual no-op) |
| `SparrowModeSwitchingFireSO` | Meta-action: `normalFire` vs `stationaryFire` sub-SO by `IsTranslationRestricted`; live-swaps mid-hold | sub-SOs + `stationaryModeChanged (ScriptableEventBool)`; ⚠️ **stateful SO** (holds `_isHeld` etc.) | delegates |
| `ToggleTranslationModeActionSO` (sealed) | Toggle stationary/turret mode via `SetTranslationRestricted`; Serpent mode also seeds a wall; authority-gated | `stationaryMode{Serpent,Sparrow}` | `ToggleTranslationModeActionExecutor` |
| `ToggleAlignActionSO` (sealed; file in `VesselActions/`) | Hold to disable course alignment | — | — (direct) |
| `FireTrailBlockActionSO` (file in `VesselActions/`) | Auto-fire loop of Prism "bullets" that expire | `friendlyFire` → sets `IsDangerous` literally | `FireTrailBlockActionExecutor` (same folder) — ⚠️ Instantiate/Destroy path, not wired to current prefabs |
| `YawsteryActionSO` | Hold-to-yaw with ramp in/out, speed coupling, optional angle lock (Manta) | `steerDirection`, `maxYawDegPerSec=120`, ramps 0.35/0.25 | `YawsteryActionExecutor` |
| `ZoomOutActionSO` | Camera zoom follows Trail or Skimmer scale growth | `scaleSource{Trail,Skimmer}` | `ZoomOutActionExecutor` (`[DefaultExecutionOrder(-1000)]`) |
| `CloakSeedWallActionSO` (⚠️ file at `UI/View/CloakSeedWallActionSO.cs`, ns `CosmicShore.UI`) | Serpent cloak: seed wall + ghost mesh + cloak trail prisms for cooldown | `cooldownSeconds=20`, nested `SeedWallActionSO`, cloak materials | `CloakSeedWallActionExecutor` |

Folder anomaly: `Data Containers/` also holds two HUD controllers
(`DolphinVesselHUDController.cs`, `SquirrelVesselHUDController.cs`) — not
actions.

## 4. Executor notes (state & events)

| Executor | Doc-worthy detail | HUD/consumer events |
|---|---|---|
| `ChargeBoostActionExecutor` | resource slot IS the charge store; discharge writes `BoostMultiplier` + `IsBoosting` | `OnCharge/DischargeStarted/Progress/Ended` |
| `ConsumeBoostActionExecutor` | pip magazine; multiplier `4^stacks` hardcoded; blocked while `IsTranslationRestricted`; raises SOAP `boostChanged` | `OnChargesSnapshot`, `OnChargeConsumed`, `OnReloadStarted/Completed` (Serpent pips) |
| `FireGunActionExecutor` | hidden `[MuzzleWorldAnchor]`; ammo gate; `[Inject] AudioSystem` | `OnAmmoChanged(float)`, static `OnShotFired(name)` (telemetry) |
| `FullAutoActionExecutor` | `1/firingRate` loop at PreLateUpdate; ammo per volley | static `OnVolleyFired(name)` |
| `FullAutoBlockShootActionExecutor` | `BlockProjectileFactory.GetBlock` → `ChangeTeam` → `MoveAndAnchorAsync`; colliders off in flight | static `OnBlockShot(name)` (telemetry) |
| `GrowSkimmerActionExecutor` / `GrowTrailActionExecutor` | implement `IScaleProvider` (MinScale/CurrentScale, polled by zoom executors) | `OnScaleChanged(cur,min,max)` on GrowSkimmer only (GrowTrail exposes no event) |
| `OverheatingActionExecutor` | heat gated off while `IsTranslationRestricted`; danger mode via `VesselPrismController.EnableDangerMode` | `OnHeatBuildStarted/OnOverheated/OnHeatDecayStarted/Completed`, `Heat01` (Sparrow HUD) |
| `SeedAssemblerActionExecutor` | takes latest trail prism (private `Trail2` via reflection fallback); `ApplyShield`; `EnsureAssembler` | `OnSeedStarted/OnBondingBegan/OnSeedStopped` |
| `ToggleTranslationModeActionExecutor` | per-frame dedup; authority check (offline OR server OR owner); raises `stationaryModeChanged` | — |
| `YawsteryActionExecutor` | SmoothStep ramps; opposite-press queues direction swap; skips while `IsTranslationRestricted` | `OnYawsteryStarted/Ended/IntensityChanged` |
| `ZoomOutActionExecutor` | local-camera-target gated; saves/restores `adaptiveZoomEnabled`; pads far clip | — |
| `CloakSeedWallActionExecutor` | ghost mesh bake; blocks spawned during cloak get extended `waitTime`; restore on end/turn-end | — |

Support (same folder, not action executors): `CameraZoomFollowScaleProvider`
(always-on zoom variant), `ShieldSkimmerScaleConfigSO` +
`ShieldSkimmerScaleDriver` (Rhino shield→skimmer-scale driver; ⚠️ debuff
mutates the shared SO asset).

## 5. Legacy `VesselActions/` inventory (MonoBehaviour `ShipAction`)

Base: `ShipAction : ElementalShipComponent` (`VesselAction.cs`) —
`Initialize(IVessel)` + reflection `BindElementalFloats`; parameterless
`StartAction()/StopAction()`, state on the component.

Still on prefabs: `FireGunAction` (Urchin), `FireBarrageAction`,
`EnergizeAction`, `GhostAction`, `DetachAction` (Urchin);
`ChargedFireGunAction`, `DetonateProjectilesAction`, `SpinAroundAction`,
`ToggleTurretModeAction` (Grizzly); `DeployDronesAction`,
`MoundDronesVesselAction`, `QueenDronesVesselAction`, `RecallDronesAction`
(Termite); `BoostAction`, `FullAutoAction`, `ToggleGyroAction`
(Falcon/Shrike); `ToggleAlignAction` (Squirrel).

Superseded predecessors kept in-tree (most have direct R_ equivalents):
`ApplyRotationAction`, `ChargeBoostAction`, `CloakSeedWallAction`,
`ConsumeBoostAction`, `DeployTeamCrystalAction`,
`DriftAction` (fixed ×1.5, no analog path), `DriftTrailAction`,
`GrowActionBase`/`GrowSkimmerAction`/`GrowTrailAction`, `OverheatingAction`,
`SeedWallAction`, `ShardToggleAction`, `ToggleStationaryModeAction`,
`ZoomOutAction`. No one-to-one R_ counterpart: `StopGunsAction` (only writer
of `GunsActive` outside VesselStatus), `ChangeRotationSpeedAction`;
`DisableTrailAction`'s StopSpawn/StartSpawn behavior is only functionally
subsumed by `ToggleTranslationModeActionExecutor`. Helpers/dead:
`ElementalFloatBinder` (dead — targets renamed property),
`AssembledArchBurstAction` (TODO-delete), `SeedAssemblerConfigurator`,
`SeedAssemblerMono` (stub), `SyncActionWrapper`,
`ToggleProjectileActionWrapper`, `ZoomGrowRateDistributeAction` (dead),
`ShardFieldBus` (broadcasts commented out), `IScaleProvider` (shared
interface), `MoundDronesVesselAction`/`QueenDronesVesselAction` (classes named
`*ShipAction`).

## 6. Per-vessel SO asset instances — `Assets/_SO_Assets/VesselActions/`

| Folder | Assets → SO class |
|---|---|
| `Common Vessel Action/` | `BoostAction` → BoostActionSO |
| `Manta/` | `ApplyRotationAction-Left/-Right`; `YawsteryAction-Left/-Right` |
| `Dolphin/` | `ChargeBoostAction`; `DeployTeamCrystalAction`; `DolphinDriftAction` → DriftActionSO; `DriftTrailAction`; `ShardToggleAction` |
| `Rhino/` | `GrowTrailAction`; `GrowSkimmerAction`; `ZoomOutAction`; ⚠️ `IncrementalBoostAction` (missing script) |
| `Serpent/` | `ConsumeBoostAction`; `SeedWallAction`; `CloakSeedWallAction`; `ToggleStationaryModeAction` → ToggleTranslationModeActionSO |
| `Sparrow/` | `FullAutoAction`; `FullAutoBlockShootAction`; `SkyBurstGunAction` → FireGunActionSO; `OverheatingAction`; `ToggleStationaryModeAction`; `ModeSwitchingFire` |
| `Squirrel/` | `SquirrelDriftAction` + `SquirrelSharpDriftAction` → DriftActionSO; `SquirrelModifyVelocityAction`; `ToggleAlignAction` |

## 7. Resource-slot conventions

`ResourceSystem.Resources` is index-addressed; slot meaning is per-vessel:

| Action / system | Slot | Model |
|---|---|---|
| `FireGunActionSO` / `FullAutoActionSO` | `ammoIndex=0` | −`ammoCost` per shot/volley, gated |
| `ChargeBoostActionSO` | `boostResourceIndex=1` | resource IS the charge store |
| `ConsumeBoostActionSO` | `resourceIndex=1` | optional one-time spend per pip |
| `SeedWallActionSO` (+ Cloak, Serpent stationary) | `resourceIndex=0` | −`MaxAmount/3` per seed |
| `OverheatingActionSO` | `heatResourceIndex` (Sparrow: 1) | resource IS the heat gauge |
| `ShieldSkimmerScaleDriver` (Rhino, passive) | `shieldIndex=0` | shield decays per tick; skimmer scale mirrors it |
| Dolphin HUD | `energyResourceIndex=0` | read-only display |
| No cost | Boost, Drift, DriftTrail, ApplyRotation, Yawstery, ZoomOut, GrowTrail/Skimmer (unless boost hook), ShardToggle, DeployTeamCrystal, FullAutoBlockShoot, ToggleAlign | |

## 8. Gotchas register

1. SO assets are shared live instances (no runtime clone) —
   `SparrowModeSwitchingFireSO` and the skimmer-debuff configs hold mutable
   state on the asset. (Exception: `AIPilot` **clones** its ability SOs per AI.)
2. `ElementalFloat`s inside R_ SOs are not level-bound today (binder commented
   out in `ShipActionSO.Initialize`) — they act as constants.
3. `ConsumeBoostActionExecutor` hardcodes `4^stacks`, ignoring the SO's
   `boostMultiplier`.
4. `ShardFieldBus` broadcast bodies are commented out — Dolphin shard toggle is
   a visual no-op.
5. `SeedAssemblerActionExecutor.EnsureAssembler` maps both `Wall` and `Gyroid`
   to `WallAssembler`.
6. Misfiled: `FireTrailBlockActionSO/Executor` + `ToggleAlignActionSO` in the
   legacy folder; `CloakSeedWallActionSO` in `UI/View/`; two HUD controllers in
   `Data Containers/`.
7. `Trail2` accessed via reflection in three wall-seeding call sites.
8. `Rhino/IncrementalBoostAction.asset` — missing-script GUID.
9. `DeployTeamCrystalActionSO.Cooldown` defined but never checked by the R_
   executor (the legacy MB did).
10. `ResourceEvents` action wiring is dead (nothing raises it).
