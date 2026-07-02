# Vessel System — Architecture

Canonical engineering reference for the **Vessel** fundamental: the player/AI
ships whose class-specific abilities compose with the other fundamentals
(Domain, Mass, Cells, Elementals, Prisms, Flora & Fauna, Toys). This doc covers
the shared vessel architecture — anatomy, lifecycle, movement, actions,
resources, trail production, networking, HUD, camera, animation, audio,
telemetry, and the authored data model.

Companion docs in this folder:

| Doc | Content |
|---|---|
| `VESSEL_CLASSES.md` | Per-class reference for all 11 vessel classes: status, signature mechanics, input bindings, HUD, telemetry, AI, prefab component inventory |
| `ACTIONS.md` | The vessel action (ability) system: dispatch pipeline, complete SO + executor inventory, resource-slot conventions, legacy system |

All paths are repo-relative under `Assets/_Scripts/` unless noted. Namespace is
`CosmicShore.Gameplay` unless noted (enums in `CosmicShore.Data`, SOs in
`CosmicShore.ScriptableObjects`, UI in `CosmicShore.UI`).

---

## 1. Big picture

A **vessel** is a networked prefab (one per class, `Assets/_Prefabs/Spacevessels/*.prefab`)
owned by a **Player** (`Controller/Player/Player.cs`, a persistent NetworkObject).
The vessel is deliberately split across two root components:

| Component | Base | Role |
|---|---|---|
| `VesselController` (`Controller/Vessel/VesselController.cs`) | `NetworkBehaviour`, implements `IVessel` | Networked identity + lifecycle: `Initialize`, netvar replication (`n_Speed`/`n_Course`/`n_BlockRotation`/`n_IsTranslationRestricted`), `StartVessel`, `DestroyVessel`, `ChangePlayer`, RPCs (`SetPose_ClientRpc`, slowed-ship list) |
| `VesselStatus` (`Controller/Vessel/VesselStatus.cs`) | **plain `MonoBehaviour`**, implements `IVesselStatus` | Local state bag + component hub: status flags, kinematics, materials, lazy accessors for every sibling component |

> ⚠️ **`VesselStatus` is NOT a NetworkBehaviour.** The in-file comment is
> explicit ("Keep this class as monobehaviour, as the network vessel status
> needs to be a network behaviour"). Older docs that say "`VesselStatus`
> extends `NetworkBehaviour`" describe `VesselController`.

`VesselStatus` declares the prefab's required component stack via
`[RequireComponent]`: `VesselPrismController`, `ResourceSystem`,
`VesselTransformer`, `AIPilot`, `SilhouetteController`, `VesselCameraCustomizer`,
`VesselAnimation`, `R_VesselActionHandler`, `VesselCustomization`,
`R_ShipElementStatsHandler`. Every accessor is lazy
(`field != null ? field : gameObject.GetOrAdd<T>()`), so a missing component is
added at first access.

**Naming eras.** The Ship→Vessel rename is mid-flight. File/class mismatches to
know: `VesselHelper.cs` contains `static class ShipHelper`; `VesselHUD.cs`
contains class `ShipHUD`; `VesselHUDData.cs` contains struct `ShipHUDData`;
`VesselCardView.cs` contains `ShipCardView`; `SlowVesselViewer.cs` contains
`SlowShipViewer`; `R_VesselElementStatsHandler.cs` contains
`R_ShipElementStatsHandler`; the enum in `Data/Enums/VesselActions.cs` is still
`ShipActions`. Many members keep Ship naming (`SetShipMaterial`,
`ShipGeometries`, `PerformShipControllerActions`). `[FormerlySerializedAs]`
markers guard serialized data (`_shipType`→`vesselType`, `ShipList`→`VesselList`,
`Captains`→`Vessels`).

### 1.1 Interfaces

**`IVessel`** (`Controller/Vessel/IVessel.cs`) — extends `ITransform`. Events
`OnInitialized` / `OnBeforeDestroyed`. Identity: `VesselStatus`,
`IsNetworkOwner` (`IsSpawned && IsOwner`), `IsNetworkClient`
(`IsSpawned && !IsOwner`), `PlayerNetId`, `VesselNetId` (= `NetworkObjectId`),
`OwnerClientNetId`. Methods: `Initialize(IPlayer)`,
`PerformShipControllerActions`/`StopShipControllerActions(InputEvents)` (the
AI/synthetic-input entry), `Teleport`, `SetResourceLevels(ResourceCollection)`,
`SetShipUp(angle)`, `DisableSkimmer`, `SetBoostMultiplier`, the five theme
setters (`SetShipMaterial`, `SetBlockSilhouettePrefab`,
`SetAOEExplosionMaterial`, `SetAOEConicExplosionMaterial`, `SetSkimmerMaterial`)
+ `SetTrailColors`, `BindElementalFloat`, `ToggleAIPilot`, `StartVessel`,
`AllowClearPrismInitialization`, `DestroyVessel`, `ResetForPlay`, `SetPose`,
`ChangePlayer`, `ModifyThrottle`, `Add/RemoveSlowedShipTransformToGameData`.

**`IVesselStatus`** (`Controller/Vessel/IVesselStatus.cs`) — facade over all
per-vessel state. Notable default implementations:

- `Domain => Player.Domain` — a **live read**, never snapshotted on the vessel.
  This is what `ShipHelper.SetShipProperties` reads, so a replicated domain
  change re-themes without extra plumbing.
- `InputController => Player.InputController`, `InputStatus => Player.InputStatus`
  — **input state lives on the Player**, not the vessel.
- `AutoPilotEnabled => AIPilot.AutoPilotEnabled`, `IsLocalUser => Player.IsLocalUser`,
  `IsInitializedAsAI => Player.IsInitializedAsAI`.

Status flags (all get/set bools): `AlignmentEnabled`, `IsAttached`, `IsBoosting`,
`IsChargedBoostDischarging`, `IsDrifting`, `GunsActive`, `HasLiveProjectiles`,
`IsOverheating`, `IsPortrait`, `IsSingleStickControls`, `IsSlowed`,
`IsStationary`, `IsTranslationRestricted`. Kinematics: `Speed`, `Course`,
`blockRotation` (lowercase b — actual name), `BoostMultiplier`, `Inertia`,
`ChargedBoostCharge`, `AttachedPrism`.

### 1.2 Enums

```csharp
// Data/Enums/VesselClassType.cs
Any=-1, Random=0, Manta=1, Dolphin=2, Rhino=3, Urchin=4, Grizzly=5,
Squirrel=6, Serpent=7, Termite=8, Falcon=9, Shrike=10, Sparrow=11

// Data/Enums/InputEvents.cs
FullSpeedStraightAction=0, RightStickAction=1, LeftStickAction=2, FlipAction=3,
IdleAction=4, MinimumSpeedStraightAction=5, Button1Action=6, Button2Action=7,
Button3Action=8, NodeTapAction=9, SelfTapAction=10, OnlyRightStickAction=11,
OnlyLeftStickAction=12, BothSticksAction=13

// Data/Enums/Element.cs
None=0, Charge=1, Mass=2, Space=3, Time=4, Omni=5
```

`ShipActions` (`Data/Enums/VesselActions.cs`, `Boost=1 … ExplosiveAcorn=20`) is
**vestigial** — referenced only by `EnumIntegrityTests` and an unused
`ShowIfAttribute` constructor/property; action bindings are keyed by
`InputEvents`.

---

## 2. Lifecycle

### 2.1 Spawn paths

| Path | Entry | Used in | Networked |
|---|---|---|---|
| **Networked (primary)** | `Player.OnNetworkSpawn` → SOAP `GameDataSO.OnPlayerNetworkSpawnedUlong` → `ServerPlayerVesselInitializer` (+ `WithAI` / `Menu` variants) → `ClientPlayerVesselInitializer.InitializePair` | Menu_Main + all multiplayer game scenes (every session is a Relay host — solo included) | Yes |
| **Local (legacy single-player)** | `GameDataSO.OnInitializeGame` → `MiniGamePlayerSpawnerAdapter` → `PlayerSpawner.SpawnPlayerAndShip` → `VesselSpawner.SpawnShip` | Single-player arcade scenes, volume test | No (plain `Instantiate`) |

Both paths resolve the prefab through **`VesselPrefabContainer`**
(`ScriptableObjects/SOAP/VesselPrefabContainer.cs`, asset
`Assets/_SO_Assets/Vessel Prefab Container.asset`):
`TryGetShipPrefab(VesselClassType, out Transform)` matches each prefab's
`IVesselStatus.VesselType` (linear scan; last match wins if duplicates exist).
`VesselSpawner` resolves `Random`/`Any` to a random concrete class first. Both
paths run `GameObjectInjector.InjectRecursive` for Reflex DI — on the server at
instantiation, and **again on clients** in `InitializePair`
(Netcode-replicated prefabs bypass Reflex, so `[Inject]` fields are null on
non-server peers until injected there).

The full networked chain (timing, `preSpawnDelayMs`/`postSpawnDelayMs`, roster
RPCs, client-pull healing loop, AI pre-spawn ordering) is documented in
CLAUDE.md § "Player Spawning Architecture"; vessel-side specifics live here.

### 2.2 `VesselController.Initialize(IPlayer)` — canonical init order

Double-init guarded (errors if `VesselStatus.Player != null`):

1. `VesselStatus.Player = player`
2. `VesselAnimation.Initialize(VesselStatus)`
3. `VesselPrismController.Initialize(VesselStatus)`
4. default `CameraFollowTarget = transform` if unset
5. `ActionHandler.Initialize(VesselStatus)` (builds input→action maps)
6. `VesselTransformer.Initialize(this)`
7. `AIPilot.Initialize(this)`
8. `VesselHUDController.Initialize(VesselStatus)` + `HideHUD()`
9. `NearFieldSkimmer?.Initialize(VesselStatus)`, `FarFieldSkimmer?.Initialize(...)`
10. `Silhouette.Initialize(VesselStatus)`
11. `VesselTransformer.ToggleActive(true)`
12. **local user only**: `ActionHandler.ToggleSubscription(true)` (input SOAP
    subscription), `VesselCameraCustomizer.Initialize(this)`,
    `hudController.SubscribeToEvents()`
13. `ShipHelper.SetShipProperties(gameData.ThemeManagerData, this)` — theme
    **references** set before paint
14. `VesselStatus.Customization.Initialize(VesselStatus)` — one-shot hull paint
15. `VesselStatus.ResetForPlay()`
16. `OnInitialized?.Invoke()`

Step 13-before-14 ordering is load-bearing: `SetShipProperties` is init-aware —
it repaints (`Customization.RefreshShipMaterial()`) only when
`ShipGeometries` is already populated, i.e. on later domain changes, never
during first init.

### 2.3 Start / Reset / Teardown

- **Start**: `Player.StartPlayer()` → `Vessel.StartVessel()` =
  `IsStationary = false` + `VesselPrismController.StartSpawn()` — the moment
  the vessel moves and lays trail. AI players additionally `ToggleAIPilot(true)`
  + input paused.
- **Reset (turn/replay)**: `GameDataSO.ResetPlayers()` → `Player.ResetForPlay()`
  → `VesselController.ResetForPlay()` (owner zeroes netvar sources) →
  `VesselStatus.ResetForPlay()` (clears flags, `ResourceSystem.Reset()`,
  `VesselTransformer.ResetTransformer()`, `VesselPrismController.StopSpawn()` +
  `ClearTrails()`, stops animation flares; sets `IsStationary = true` until the
  next `StartVessel`).
- **Teardown**: `DestroyVessel()` — networked → server-only
  `NetworkObject.Despawn(true)`; local → `Destroy(gameObject)`. `OnDestroy`
  raises `OnBeforeDestroyed`. Game vessels are `destroyWithScene: true`; the
  persistent `Player` clears its stale reference in `PrepareForNewScene` /
  `OnNetVesselIdChanged(0)`.

### 2.4 `ChangePlayer(IPlayer)` — ownership/possession swap

Used by turn-based vessel-ownership swap (CellularDuel) and vessel-selection
swap. Always sets `VesselStatus.Player`. Then:

- AI or network-client player → HUD `UnsubscribeFromEvents()` + `HideHUD()`,
  `ActionHandler.ToggleSubscription(false)`; AI keeps the transformer active;
  network client disables the transformer and `SubscribeToNetworkVariables()`.
- Local human → `UnsubscribeFromNetworkVariables()`, HUD subscribe + show,
  transformer + actions on, `VesselCameraCustomizer.RetargetAndApply(this)`.

### 2.5 Vessel swap (menu vessel changer)

`MenuServerPlayerVesselInitializer.RequestSwap(VesselClassType)` — entry for
host UI, client UI (`MenuVesselSelectionPanelController`) and the
`VesselChangerToySet` toy. Guards `_isSwapping` / same-class / not-spawned.
Client path routes through
`ClientPlayerVesselInitializer.RequestVesselSwap_ServerRpc`. Server
`SwapVesselAsync`: snapshot `Pose` → `DespawnVessel(old)` →
`SpawnVesselForPlayer(ownerClientId, player, targetClass)` →
`ReInitializePair` (host) → `newVessel.SetPose(snapshot)` →
`ActivateAutopilot(player)` → delay → targeted
`ReplaceVesselForPlayer_ClientRpc` to non-host clients. The swap always drops
the new vessel into autopilot + paused input; callers restore freestyle control
after ~600 ms (`restoreFreestyleDelayMs` + `IsSwapping` polling). The swap
pipeline deliberately does **not** write `GameDataSO.selectedVesselClass`.

---

## 3. Movement & input

### 3.1 Pipeline

```
Hardware (Input System / EnhancedTouch / raw Win32 mice / gyro)
  → IInputStrategy.ProcessInput()      (selected per-frame by InputController)
      ├─ analog state → IInputStatus   (XSum/YSum/XDiff/YDiff, sticks, triggers)
      └─ discrete events → InputStatus.OnButtonPressed/OnButtonReleased
             (SOAP ScriptableEventInputEvents, payload = InputEvents)
  → R_VesselActionHandler              (owner replays via ServerRpc→ClientRpc)
      → ShipActionSO.StartAction/StopAction
  → VesselTransformer.Update()         (polls IInputStatus directly every frame)
      → RotateShip() + MoveShip()
  → VesselController.Update()          (owner only: replicate Speed/Course/blockRotation)
```

Analog state does **not** go through the action handler — the transformer polls
`IInputStatus` directly. AI bypasses strategies entirely: `AIPilot` writes
`XSum/YSum/XDiff/YDiff` / `EasedLeftJoystickPosition` straight into the same
`IInputStatus` and calls `IVessel.PerformShipControllerActions` for abilities —
the same vessel API a human drives.

### 3.2 InputController & InputStatus

`InputController` (`Controller/IO/InputController.cs`) lives on the **Player**
GameObject (lazy `GetOrAdd`). Strategy selection re-evaluated every frame:
gamepad if `Gamepad.current != null` → touch on handhelds → dual-mouse when
engaged (both physical mice LMB held; Escape disengages) → keyboard.
`InputStatus` (`Controller/IO/InputStatus.cs`) is a **NetworkBehaviour** on the
Player: every property is dual-backed (local field when not spawned; owner-write
NetworkVariable when spawned) — remote players' raw input state replicates
(26 NetworkVariables).

Strategies (`Controller/IO/`): `GamepadInputStrategy` (triggers = analog drift;
south/east/west = Button1/2/3), `TouchInputStrategy` (two virtual thumb sticks,
2→1-finger lift = drift via `OnlyLeft/RightStickAction`),
`KeyboardInputStrategy` (**file sits at `Assets/KeyboardInputStrategy.cs`**,
misplaced; WASD + P;L' sticks, Shifts = triggers, Space/B/N = buttons),
`DualMouseInputStrategy` + `MultiMouseService` (two physical mice).
`KeyboardMouseInputStrategy` in `IO/` is **orphaned dead code**.
`DeviceOrientationHandler` owns gyro + phone-flip (`FlipAction`).

All dual-stick strategies reparameterize two sticks into four scalars:
`XSum` = yaw, `YSum` = pitch, `YDiff` = roll, `XDiff` = throttle in [0,1].

**Pause layers** (distinct):

| Layer | Effect |
|---|---|
| `InputStatus.Paused` (owner-write netvar `n_paused`) | Blocks `ProcessInput`; raises `OnToggleInputPaused` → action handler unsubscribes. Used by autopilot/menu |
| `PauseSystem.Paused` (global static) | `Time.timeScale = 0`; InputController early-outs |
| `VesselStatus.IsStationary` | Transformer `Update` early-outs entirely; cleared by `StartVessel()` |
| `VesselStatus.IsTranslationRestricted` | Rotation still applies, translation skipped (turret modes) |
| `VesselTransformer.ToggleActive(false)` | Master gate; off on network non-owner vessels |

### 3.3 VesselTransformer family

`VesselTransformer` (`Controller/Vessel/VesselTransformer.cs`) is kinematic —
pure transform manipulation, no Rigidbody. Update order: write
`VesselStatus.blockRotation` → optional boost decay (raises SOAP
`ScriptableEventBoostChanged`) → drift-trigger smoothing → `ApplyAnalogDrift()`
→ `RotateShip()` (pitch/yaw/roll compose into `accumulatedRotation`, slerp
1.5·dt, optional gyro) → if `IsTranslationRestricted` return →
throttle/velocity modifiers → `MoveShip()`.

Speed model: `speed = Lerp(speed, XDiff·ThrottleScaler·ThrottleScalerMultiplier·boost + MinimumSpeed, 1.5·dt)`;
modifiers scale the frame **output** only (`effectiveSpeed`), never the
persistent field. **Course/drift decoupling**: while drifting, `Course` blends
between nose direction and a damped drift course by the analog trigger sum —
the nose points where you steer, the velocity vector lags. Public control
surface used by actions/effects: `SetPose`, `FlatSpinShip`, `SpinShip`,
`GentleSpinShip`, `ApplyRotation`, `TranslateShip`, `ModifyThrottle(amount, duration)`,
`ModifyVelocity(vector, duration)`, drift API
`BeginDrift(rotMult, damping, isSharp)` / `EndDrift(isSharp)`.

Subclasses & per-prefab assignment:

| Transformer | Vessels | Behavior delta |
|---|---|---|
| `VesselTransformer` (dual-stick) | Manta, Dolphin, Rhino, Squirrel | Full throttle/pitch/yaw/roll model above |
| `SingleStickVesselTransformer` | Grizzly, Serpent, Sparrow | One stick steers via a course transform; fixed cruise speed (no XDiff throttle); roll auto-banks |
| `CommandVesselTransformer` | Termite | RTS-style: lerps position toward `InputStatus.ThreeDPosition`; sets `CommandStickControls` |
| `GunVesselTransformer` | Urchin | State machine on `IsAttached`: attached → `BlockscapeFollower` surface slide + ammo recharge (×2 on shielded prisms); else base flight. `FinalBlockSlideEffects()` grows friendly / steals hostile prisms per block |

Support movement components: `TrailFollower` (rides a `Trail` block-by-block
with Friendly/Hostile/Destroyed terrain speeds; ping-pongs at non-loop trail
ends), `BlockscapeFollower` (crawls a single prism's surface, rolls across box
edges), `DriftJet` (Squirrel jet visual pointing along `Course` while
drifting), `GunTransformer` (Falcon/Shrike turret ring — aims child guns by
right-stick angle; not a VesselTransformer), `BoidController` (Termite drone
spawner — Termite.prefab is the only prefab carrying it), `SlowShipViewer`
(LineRenderer to explosion-debuffed victims).

---

## 4. Action system (summary — full inventory in `ACTIONS.md`)

The current pipeline is **R_VesselActions**: stateless `ShipActionSO` config
assets dispatched by `R_VesselActionHandler` (NetworkBehaviour) through a
per-vessel `ActionExecutorRegistry` of `ShipActionExecutorBase` MonoBehaviours
that hold all runtime state.

- Bindings are lists of `InputEventShipActionMapping { InputEvents → List<ShipActionSO> }`
  serialized on the vessel prefab, with optional touch/gamepad override lists.
- **Network replay**: for a spawned owner, button press/release round-trips
  `SendButtonPressed_ServerRpc → ClientRpc → PerformShipControllerActions` on
  **every peer** — abilities execute everywhere, not just server-side. Action
  SOs that raise global SOAP HUD events must therefore self-gate on
  `vesselStatus.IsLocalUser` (e.g. `DriftActionSO`) or carry the
  `VesselStatus` in the payload for receiver-side filtering.
- `MuteInput(InputEvents, seconds)` is the debuff hook (used by explosion /
  skimmer / danger-prism effects), raising `ScriptableEventInputEventBlock`
  payloads the HUD visualizes.
- Executors mass-reset on SOAP `OnMiniGameTurnEnd`.
- The legacy MonoBehaviour `ShipAction` system (`VesselActions/`) survives on
  the unfinished vessels (Urchin, Grizzly, Termite, Falcon, Shrike) plus one
  straggler on Squirrel (`ToggleAlignAction`).

---

## 5. Resources & elementals

### 5.1 Gauge resources

`ResourceSystem` (`Controller/Vessel/ResourceSystem.cs`, extends
`ElementalShipComponent`) holds `List<Resource> Resources` — normalized [0,1]
gauges addressed **by index**; slot meaning is per-vessel convention (ammo,
boost charge, heat, shield, energy — see `ACTIONS.md` §7). Passive regen: every
1 s each resource gains `resourceGainRate`. `OnResourceChanged(index, current, max)`
is change-gated. Consumers drain with negative `ChangeResourceAmount`.

### 5.2 Element levels (the Elementals fundamental on vessels)

- **Base levels** `Dictionary<Element, float>` in [-0.5, 1.5] — persistent
  progress written by crystals (`AdjustLevel`/`IncrementLevel`), the comeback
  system (`SetElementLevel`, clamped to [0, 1.5]), or init.
- **Temporary modifiers** via `ApplyElementalEffect(element, magnitude, duration)`
  — the standardized buff/debuff API; positive = buff, negative = debuff;
  linear decay to zero over `duration`; `duration <= 0` = permanent.
- Effective level = `clamp(base + modifiers, -0.5, 1.5)`;
  `GetLevel = floor(×10)` → **integer in [-5, 15]**.
- `OnElementLevelChange(Element, int)` fires only when the integer level
  changes. Subscribers: `ElementalFloat.ScaleValueWithLevel`,
  `SilhouetteController` → `ElementalBarsView.SetLevel` (petal flowers),
  `VesselAnimation.UpdateShapeKey` (blend-shape hull morph, indices Mass=0,
  Charge=1, Space=2, Time=3, weight = level/10).

**`ElementalFloat`** (`Controller/Vessel/ElementalFloat.cs`) binds a designer
float to an element: `Value = LerpUnclamped(Min, Max, level/10)` —
deliberately extrapolates for the comeback range beyond [0, 10]. Binding is by
reflection over fields in `ElementalShipComponent.BindElementalFloats`
(subclasses: `ResourceSystem`, `Skimmer`, `VesselCameraCustomizer`, legacy
`ShipAction`, `AOEExplosion`). ⚠️ **Known gap:** the binder call in
`ShipActionSO.Initialize` is commented out, so `ElementalFloat`s inside R_
action SO assets are currently inert constants; `ElementalFloatBinder` is dead
code (targets a renamed property).

Buff/debuff writers (all through `ResourceSystem`):

| Writer | API | Values |
|---|---|---|
| Elemental crystal pickup | `AdjustLevel` / `IncrementLevel` | `VesselAdjustLevelByCrystalEffectSO`, `VesselIncrementLevelByCrystalEffectSO` |
| Skimmed elemental crystal | `AdjustLevel(element, gain)` | gain = min(scale × 0.1, 0.5) — bigger crystal, bigger boost |
| Danger prism contact | `ApplyElementalEffect` ×4 | −0.5 over 4 s, all elements, 1 s cooldown, domain-blind |
| Skimmer overtake | `ApplyElementalEffect` ×4 on the slower vessel | ally +0.5 / opponent −0.5 over 3 s |
| `ElementalComebackSystem` (arcade) | `SetElementLevel` | domain-aggregate deficit vs leader, [0, 1.5] band only |

**Initial levels are currently dormant**: `SO_Vessel.InitialResourceLevels` →
`GameDataSO.ResourceCollection` flows exist (via `Arcade.Launch*`), but both
runtime consumers (`MiniGame.SetupTurn`, `Hangar.SetShipProperties`) are
commented out — vessels start at effective 0 and gain levels in-game only.

### 5.3 Display pipeline

`SilhouetteController` (on every prefab) drives: the energy "jaw" UI + trail
conveyor (`SilhouetteView`, config `SilhouetteConfigSO`, per-vessel assets at
`_SO_Assets/HUD/`), domain-tinted silhouette trail (theme `ColorSet`; danger
prisms tint `EnvironmentColors.Danger`), and the **elemental petal bars**
(`ElementalBarsView` + `ElementalBarsConfigSO` — see CLAUDE.md § "Elemental
Bars"; opt-in per vessel via the null-safe `elementBars` reference).
`ElementPipsView`/`ElementPipsConfigSO` is a superseded tick-column display —
wired on Sparrow.prefab but **inert** (nothing calls `Build()`/`SetLevel()`).
`Pip` (`Controller/Vessel/Pip.cs`) is unrelated: picture-in-picture camera
toggle raising SOAP `PipData` to MiniGameHUD.

---

## 6. Trail production, skimmer & shields

### 6.1 VesselPrismController — laying trail

`VesselPrismController` spawns trail prisms behind a moving vessel
(`spawnerEnabled && !IsAttached && Speed > 3`). Spawn delay =
`wavelength / Speed` (≈ constant world-space spacing). `Gap == 0` → one prism
on `Trail`; else a twin-rail pair on `Trail` + `Trail2` (Squirrel's ridable
tube). Each prism: scale from `BaseScale × X/Y/ZScaler`, rotation =
`blockRotation`, spawn through the SOAP request/return channel
`PrismEventChannelWithReturnSO` → `PrismFactory` (per-vessel pools:
`PrismType.{Dolphin, Serpent, Sparrow, Manta, Squirrel, Rhino}`), then
`ChangeTeam(Domain)`, `ownerID = PlayerName`, `waitTime` (collider/renderer
disabled window: the per-vessel serialized `defaultWaitTime` — code default
0.5 s, authored 0.2–3 s across prefabs — or the time to clear the skimmer),
`trail.Add(prism)`,
`Initialize`. Events `OnBlockCreated(xShift, wavelength, sx, sy, sz)` +
`OnBlockSpawned(Prism)` feed the silhouette.

Drift shaping: `SetDotProduct(dot)` (from `DriftTrailActionExecutor`) fattens Z
and compresses wavelength as the vessel drifts sideways — denser, thicker
tube. `SetNormalizedXScale` (skim effects, `ScoutTrailPrismScaler` — an
adaptive-radius probe against **`PrismSpatialIndex.IsAnyPrismWithin`**, no
physics). **Danger mode**: `EnableDangerMode(material, scaleMult, …)` marks new
prisms `IsDangerous` + blends the danger material (Sparrow overheat).
`SparrowPrismController` inverts the boost relationship (bigger, sparser trail
when NOT boosting). `ClearTrails()` clears list bookkeeping only — **laid mass
persists** (mass conservation; prisms die only to active forces:
`Damage`/`Consume` from vessels, fauna, projectiles, AOE).

### 6.2 Skimmer

`Skimmer` (`Controller/Vessel/Skimmer.cs`, `ElementalShipComponent`) — up to
two per vessel (`NearFieldSkimmer`/`FarFieldSkimmer` on `VesselStatus`), an
invisible sphere trigger + `SkimmerImpactor`. `Scale` is an **ElementalFloat**
(element-buffable size). Crystal vacuum (`vaccumAmount`), skim tracking, and
booster-ring visualization (`NudgeShard` markers along the trail ahead). Skim
effects run from `SkimmerImpactorDataContainerSO` per-vessel assets — boost
energy (`SkimmerBoostPrismEffectSO`, **danger prisms give 10×**), trail-tube
alignment assist (`SkimmerAlignPrismEffectSO`), steal, shield growth,
forcefield crackle (`ForcefieldCrackleController`, 16-impact MPB ring buffer).
See `ACTIONS.md` + `VESSEL_CLASSES.md` for per-vessel skimmer kits.

### 6.3 Prism shields

`PrismStateManager` states: `Normal, Shielded, SuperShielded, Dangerous`.
Shielded absorbs one damage/steal (drops shield instead); **super-shielded is
fully invulnerable to damage AND steal**. Visual/physical swap via
`PrismOctahedronShield` (box ↔ circumscribing octahedron, mass ×4.5, per-face
bloom engage / shatter-overlay disengage) and `PrismStellatedOctahedronShield`
(super-shield stella octangula, mass ×13.5 — exists but not yet wired into
`PrismStateManager`, which engages the plain octahedron for both states).
Shield state syncs to `PrismSpatialIndex.UpdateShieldState` so Burst AOE
respects it.

### 6.4 ClearPrisms

`ClearPrisms` (`Controller/Vessel/ClearPrisms.cs`) — camera-occlusion
transparency: a trigger capsule between the close camera and the vessel fades
prisms via MaterialPropertyBlock `_Alpha` (no material cloning). Gated by
`AllowClearPrismInitialization()` = owner or AI.

---

## 7. Networking

### 7.1 Vessel replication

`VesselController` NetworkVariables (all **Owner-write**, Everyone-read):
`n_Speed`, `n_Course`, `n_BlockRotation`, `n_IsTranslationRestricted`. The
owner writes the first three **every frame** in `Update()` — the hottest
netcode write path (ProfilerMarker `NetMarkers.Serialize`). Non-owners
subscribe and push values into their local `VesselStatus`; the non-owner
transformer is `ToggleActive(false)`, so these netvars feed trail placement and
status, **not** position sync.

Position/rotation sync is per-prefab:

| Prefab | Transform sync |
|---|---|
| Sparrow, Squirrel | `ClientNetworkTransform` (`Utility/Network/ClientNetworkTransform.cs`, owner-authoritative) |
| Manta, Dolphin, Rhino, Serpent | package `NetworkTransform` (server-authoritative) |
| Falcon, Shrike, Termite, Urchin, Grizzly | none |

Ability execution replicates via the action handler's ServerRpc→ClientRpc
replay (§4). Crystal impact effects replicate via `NetworkVesselImpactor`
RPCs; prism/skimmer/explosion/projectile effects run locally per peer (§9).

### 7.2 destroyWithScene matrix

| Object | Spawn call | destroyWithScene | Why |
|---|---|---|---|
| Human Player | connection approval (`CreatePlayerObject=true`) | false | Persists across scene loads; re-init via `PrepareForNewScene()` |
| Game-scene vessel | `SpawnWithOwnership(clientId, true)` | **true** | Dies with scene; `preSpawnDelayMs` avoids the scene-load batching race |
| Menu vessel | `SpawnWithOwnership(clientId, false)` | **false** | Joining client's scene-synchronize batching-destroy race; explicit despawn on menu→game / leave-party |
| AI Player / AI vessel | `Spawn(false)` (AI vessel host-owned, not SpawnWithOwnership) | **false** | AI spawns same tick as scene load; explicit cleanup in `SceneLoader.ClearPlayerVesselReferences` etc. |

### 7.3 Domain & theme

Domain is **never stored on the vessel** — `IVesselStatus.Domain` reads
`Player.Domain` live (mirror of server-write `Player.NetDomain`).
`ShipHelper.SetShipProperties(ThemeManagerDataContainerSO, IVessel)` swaps the
five material references from `TeamMaterialSets[domain]` + trail colors from
the `ColorSet`, then (init-aware) re-applies the hull material. Call sites:
`VesselController.Initialize`, `ClientPlayerVesselInitializer.InitializePair`/
`ReInitializePair` (which also stash the theme SO onto
`Player._vesselThemeManagerData`), and `Player.OnNetDomainChanged` — so a
replicated domain change fully re-themes with no extra calls. Hull material
slot convention (`ShipHelper.ApplyShipMaterial`): SkinnedMeshRenderer slot 0,
MeshRenderer slot 1.

### 7.4 Client caches

`NetworkClientCache<T>` (`Controller/Vessel/NetworkClientCache.cs`) — static
registry of active networked instances via `NetcodeHooks`
(`ActiveInstances`, `OwnInstance`, `OnNewInstanceAdded`). Subclasses on the
prefabs: `NetworkVesselClientCache` (`VesselController`),
`NetworkPlayerClientCache` (`Player`, + `GetPlayerByTeam`). Note
`GetInstanceByClientId` actually matches by `NetworkObjectId`.

---

## 8. HUD

Controller/view split per vessel: `*VesselHUDController` (extends
`VesselHUDController`, implements `IVesselHUDController`) subscribes to
gameplay events and pushes into a passive `*VesselHUDView` (extends abstract
`VesselHUDView`; `IVesselHUDView` is an empty marker). The base controller
wires `R_VesselActionHandler.OnInputEventStarted/Stopped` → button-highlight
images. Controllers gate on `!IsInitializedAsAI && IsLocalUser` (Dolphin
currently lacks the gate).

**Reparenting pipeline**: each vessel prefab carries its HUD widgets under a
stub `MiniGameHUD` + `ShipHUD` component (file `VesselHUD.cs`). `ShipHUD.Start()`
raises SOAP `onShipHUDInitialized (ShipHUDData)`; the scene consumer
(`MiniGameHUD` in game scenes, `MenuMiniGameHUD` in Menu_Main, legacy
`GameCanvas`) reparents the HUD children onto the scene HUD canvas at sibling
index 0. Visibility: `MiniGameHUD.ShowLocalVesselHUD()` on turn start /
`HideLocalVesselHUD()` on client-ready; per-view CanvasGroup fades
(`HUDAnimationSettingsSO`).

Shared-channel discipline: HUD SOAP channels (`boostChanged`,
`joustCollisionEvent`, crystal-explosion events, overcharge events) are global
per-asset — every controller self-filters by `IVesselStatus` reference or
player name. Per-vessel HUD contents are in `VESSEL_CLASSES.md`. Prefab
variants: `_Prefabs/UI Elements/VesselHUD/{Dolphin,Manta,Rhino,Serpent,Sparrow,Squirrel}HUDVariant.prefab`
+ generic `VesselHUDPrefab.prefab`.

⚠️ Folder gotcha: `SquirrelVesselHUDController` and `DolphinVesselHUDController`
live in `Controller/Vessel/R_VesselActions/Data Containers/`, not `UI/Controller/`.

---

## 9. Impact effects (vessel-facing)

The impactor/effect-SO matrix (`Controller/ImpactEffects/`) applies to vessels
through `VesselImpactor` (requires `IVessel` + `NetworkVesselImpactor`) and
`SkimmerImpactor`. Effects are authored per vessel in **container SOs** wired
on the prefab: `VesselImpactorDataContainerSO` (prism effects, omni-crystal
effects, per-element crystal buckets, skimmer effects) and
`SkimmerImpactorDataContainerSO`. Asset instances:
`_SO_Assets/Effects/Effect Containers/VesselContainers/{Manta,Dolphin,Rhino,Squirrel,Sparrow,Serpent}ImpactorDataContainer.asset`
(+ skimmer/projectile/explosion containers). Naming: shared generic assets
(`VesselDamagePrismEffect.asset`) mixed with per-vessel-tuned instances
(`SquirrelVesselChangeSpeedByPrism.asset`).

Key structural facts:

- **Execution is one-sided**: `PrismImpactor`/`MineImpactor` effect arrays are
  non-serialized (always empty) — the vessel/skimmer/projectile/explosion side
  owns every effect list.
- **Only crystal effects replicate** (`NetworkVesselImpactor`
  ServerRpc→ClientRpc broadcast of `CrystalImpactData`); everything else runs
  locally per peer from its own trigger events.
- **Danger prisms are domain-blind (locked design)**: danger effect SOs gate
  only on `IsDangerous`, never domain — friendly fire included. Risk/reward:
  10× skim energy vs. volume-independent full-stop slow
  (`VesselChangeSpeedByPrismEffectSO` danger path), all-element −0.5/4 s debuff,
  boost reset. Explosions, by contrast, DO domain-gate (unless `affectSelf`).
- Anti-spam cooldowns live in the SOs (static per-impactor dictionaries).

Full vessel-facing effect catalog: survey the `EffectsSO/` subfolders —
`Vessel Prism Effects/`, `Vessel Crystal Effects/`, `Vessel Explosion Effects/`,
`Vessel Projectile Effects/` (supports `vesselTypesToImpact` class filter),
`Vessel Skimmer Effects/` (joust scoring `VesselExplosionBySkimmerEffectSO`,
overtake elemental buff/debuff), `Skimmer Prism Effects/`.

---

## 10. Camera

Two coexisting stacks:

- **Gameplay**: three plain-`Camera` `CustomCameraController`s managed by
  `CameraManager` ("CM PlayerCam" / "CM DeathCam" / "CM EndCam").
- **Menu_Main**: Cinemachine (`MainMenuCameraController` — crystal orbit +
  vessel-follow modes) that hands off to the gameplay stack for freestyle via
  a pose-matched bridge vCam.

Binding flow (local user only): `VesselController.Initialize` →
`VesselCameraCustomizer.Initialize(this)` → SOAP `ScriptableEventTransform`
(`EventOnInitializePlayerCamera.asset`) → `CameraManager.SetupGamePlayCameras(followTarget)`
→ `VesselCameraCustomizer.Configure(controller)` applies the per-vessel
**`CameraSettingsSO`** (`Controller/Camera/CameraSettingsSO.cs`; assets at
`_SO_Assets/Camera/`). Modes: `FixedCamera` (followOffset + optional adaptive
zoom), `DynamicCamera` (min/max distance + smoothing), `Orthographic`.
`RetargetAndApply` re-runs it after possession/vessel swaps. Zoom actions
(`ZoomOutActionExecutor`, `CameraZoomFollowScaleProvider`) scale camera
distance by an `IScaleProvider` ratio (trail/skimmer growth).

⚠️ Urchin, Grizzly, Falcon, Shrike, Termite prefabs have **null**
`VesselCameraCustomizer.settings` (AI-only today; `Configure` would NRE).
`CameraManager.SetupEndCameraFollow` currently has zero callers (menu camera is
owned by `MainMenuCameraController`).

---

## 11. Animation, audio, FX

### 11.1 Animation

`VesselAnimation` (abstract, `Controller/Animation/VesselAnimation.cs`) reads
`InputStatus` per frame → `PerformShipPuppetry(pitch, yaw, roll, throttle)`;
subscribes `ResourceSystem.OnElementLevelChange` → blend-shape hull morph
(`UpdateShapeKey`, indices Mass=0/Charge=1/Space=2/Time=3). Flare API
(`FlareEngine`/`FlareBody`) drives `_ColorMultiplier` on the
SkinnedMeshRenderer (called from the transformer's boost/velocity modifiers).
Two styles: transform puppetry (`MantaAnimation` on Manta; `RhinoAnimation`;
`RiptideAnimation` on Dolphin — "Riptide" is Dolphin's legacy codename, handles
drift reparenting + resource-driven jaw; `UrchinAnimation`; `BufoAnimation` on
Grizzly — "Bufo" legacy codename) and Animator-driven
(`MantaAnimationContoller` — note typo — shared by Squirrel/Sparrow/Serpent +
planned Termite/Falcon/Shrike; params Pitch/Yaw/Roll/Throttle/Boost).
`DolphinAnimation`, `SparrowAnimationController`, `SingleStickAnimationController`
are dead code (no prefab references). Procedural jets: `ProceduralJetMesh`
(Rhino), `ParametricJetEffect` (dormant).

### 11.2 Audio — FMOD

**Vessel audio is FMOD** (`FMODUnity`), not Wwise — the `Wwise/` folder is
stale for vessels. Components (namespace `CosmicShore.Gameplay.Audio` / FX):

- `ShipAudioController` (Squirrel only today): looping engine event driven by
  transform-derived velocity ("Speed" param), quaternion-delta tilt ("Tilt
  Acel"), drift dip, and the four **element levels** as FMOD params
  (Charge/Mass/Space/Time).
- `DriftAudioController` + `ProximityBoostAudioController` (Squirrel only):
  drift loop ("Drift Amount" from analog triggers) and skim-boost
  sonification (reads the shared SOAP `boostBase/MaxMultiplier` variables so
  audio matches the gameplay clamp).
- `ShipStudioListenerGate` (**all 11 prefabs**): keeps the prefab's FMOD
  `StudioListener` disabled unless `IsLocalUser` — exactly one live listener.
- All three emitters gate to the local user (`onlyAudibleToController`) and
  honor `GameSetting.SFXLevel`/`SFXEnabled` per-instance (no FMOD SFX bus;
  `FMODOneShotVolumeHelper` exists for the same reason).
- One-shots route through `AudioSystem.PlayGameplaySFX(GameplaySFXCategory)`
  (DriftStart/DriftEnd, BoostActivate, GunFire, per-element crystal SFX, …).

### 11.3 FX

`ForcefieldCrackleController` (skimmer crackle, §6.2), `DriftJet` (Squirrel),
`OverheatTrailVisualBridge` (Sparrow overheat → silhouette danger visual),
`SlowShipViewer` (explosion-debuff link lines), `TrailScaleModulator` +
`TrailScaleProfileSO` (trail squash profiles).

---

## 12. Telemetry, stats & analytics

Three parallel stat layers:

1. **`RoundStats`** (`Data/Enums/RoundStats.cs` — path is historical; a
   `NetworkBehaviour` on the persistent Player object) — per-player in-game
   stats (score, prism counts/volumes, crystals, jousts, goals, ability active
   time). Local fields are the read source; server-write NetworkVariables
   replicate; `Domain` is a local mirror (no netvar — synced from
   `Player.NetDomain`). Written **only by `StatsManager`**
   (`Controller/Managers/StatsManager.cs`, server-authoritative via
   `_allowRecord`), which is wired 100% by SOAP `EventListener*` components on
   `_Prefabs/CORE/StatsManager.prefab` (PrismStats / CrystalStats /
   AbilityStats / string channels raised by `Prism`, `PrismTeamManager`,
   impactors, `R_VesselActionHandler`). **Anti-pattern reminder** (BUGS B15):
   never subscribe to RoundStats events with cleanup gated on turn-end or
   unsubscribe by iterating `RoundStatsList`; detach in `OnDestroy` from a
   tracked record.

2. **`VesselTelemetry`** (`Controller/Vessel/VesselTelemetry.cs`, abstract) —
   local-player-only per-turn vessel stats: longest drift, max boost time
   (above `boostMultiplierThreshold`), prisms damaged; raised through
   `VesselStatEventSO` assets (`_SO_Assets/VesselStats/`) consumed by
   `EventDrivenStatsProvider` for the scoreboard. Subclasses:
   `SparrowVesselTelemetry` (blocks shot, skyburst missiles, danger blocks),
   `SquirrelVesselTelemetry` (clean streak, jousts won, prisms stolen),
   `DefaultVesselTelemetry`. `VesselTelemetryBootstrapper` auto-adds the right
   subclass by `VesselType` on prefabs without one — but runtime-added
   telemetry has **null stat SO refs** (accumulators work, scoreboard rows
   don't); Squirrel/Sparrow author theirs directly on the prefab with refs
   wired.

3. **Cloud/lifetime**: `UGSStatsManager.ReportVesselTelemetry(telemetry, vesselTypeName)`
   → `VesselStatsCloudData` (per-vessel `BestDriftTime`/`BestBoostTime`/
   `TotalPrismsDamaged`/`GamesPlayed` + open-ended `Counters` dict) →
   `VesselStatsRepository` (Cloud Save key `VESSEL_STATS`, 2 s debounce).
   Flushed by mode score trackers at game end — winner-gated in
   HexRace/Joust/CrystalCapture (so `GamesPlayed` under-counts losses),
   unconditional in WildlifeBlitz. Leaderboards via
   `UGSStatsManager.SubmitScoreInternal` (`LeaderboardConfigSO`). Analytics:
   `AnalyticsServiceFacade` attaches `vessel_class` to `game_started`/
   `game_completed`, and `RecordVesselUnlocked` fires `vessel_unlocked`.

---

## 13. AI

`AIPilot` (`Controller/AI/AIPilot.cs`, on **all 11 prefabs**) drives the same
vessel API as a human: writes `IInputStatus` fields and calls
`IVessel.Perform/StopShipControllerActions`. No separate movement path.

- Toggle chain: `IVessel.ToggleAIPilot(bool)` → `AIPilot.Start/StopAIPilot()`;
  `IVesselStatus.AutoPilotEnabled` gates the action handler (AI never consumes
  the SOAP input channels) and HUD/telemetry.
- Targeting priority: external provider (`SetExternalTargetProvider` — Astro
  League ball striking) > player-seek (Joust: nearest opposing-domain vessel)
  > cell-item seek (targets `Buff` items + other domains' `Debuff` items —
  debuffs are disguised as desirable; falls back to cell centre).
- Config: `skillLevel` (0..1) lerps every Low/High tuning pair; flags `ram`
  (Rhino) and `drift` (Dolphin); `List<AIAbility> { ShipActionSO, Duration,
  Cooldown }` — ability SOs are **cloned per AI** and looped
  (start → duration → stop → cooldown).
- Backfill AI (`ServerPlayerVesselInitializerWithAI.ConfigureAIPilot`):
  `skill = Clamp01(SelectedIntensity × 0.25)`, `seekPlayers` only in
  MultiplayerJoust. Menu autopilot uses prefab-serialized values.
- `SO_AIProfileList` (`MainAIProfileList.asset`, 13 profiles) is **identity
  only** (name + avatar), not behavior tuning.
- `AIGunner` + `Gunner.prefab` are vestigial (logic commented out).
- `AICinematicBehavior` (Squirrel/Sparrow prefabs) is dormant (nothing drives
  the end-game flourish behaviors yet).

---

## 14. Data model, selection, loadout, unlock

### 14.1 Authored SOs

- **`SO_Vessel`** (`ScriptableObjects/SO_Vessel.cs`, global namespace):
  identity (`Class`, `Name`, `Description`), element config (`PrimaryElement`,
  `SO_Element`, `InitialResourceLevels : ResourceCollection`), UI sprites
  (icons, preview, card silhouettes — "Silohoutte" misspelling is in code),
  `List<SO_VesselAbility> Abilities` (marketing/ability-card metadata — not
  referenced by prefabs), `Games`/`TrainingGames`, three marketing
  `GameplayParameter` sliders, and the unlock state (`IsLocked`, `UnlockCost`,
  `Unlock()`/`Lock()`). Assets: `_SO_Assets/Classes/SO_Class_*.asset` (9 — no
  Falcon/Shrike) + `SO_Classlist_*` lists (`SO_VesselList`).
- **`SO_Captain`**: a named pilot persona = one `Vessel` × one `PrimaryElement`
  + preset `InitialResourceLevels`. Full 9-vessel × 4-element matrix authored
  under `_SO_Assets/Captains/`. Runtime `Captain` model (level → primary
  element = 0.1 × level) exists, but `CaptainManager` is PlayFab-disabled and
  its UGS replacement (`CaptainProgressCloudData`) isn't registered in
  `UGSDataService` yet — **the captain system is dormant**.
- **`SO_ArcadeGame.Vessels`** (`[FormerlySerializedAs("Captains")]`) — which
  vessels a game offers; the AI vessel pick draws from this list.

### 14.2 Selection channels

Two parallel "selected vessel" channels:

1. `GameDataSO.selectedVesselClass` (`VesselClassTypeVariable`) — host/local
   config, consumed at spawn. Writers: `AppManager.ConfigureGameData`
   (**Squirrel** menu default), `MainMenuController`, `GameDataSO.ResetAllData`
   (Manta), `ShipSelectionView`, `VesselSelectionPanelController`,
   `ArcadeGameConfigureModal`, host-config RPC, benchmark launcher.
2. `Player.NetDefaultVesselType` (owner-write NetworkVariable) — per-client
   authoritative for networked spawns. `ArcadeGameConfigureModal.SyncLocalPlayerVesselType`
   bridges the two; the menu swap pipeline bypasses channel 1 entirely.

Selection UIs: `ArcadeGameConfigureModal` (filters locked vessels; default
priority: previous selection → saved loadout → **Dolphin** → first available),
`VesselSelectionPanelController` (single-player in-game swap via
`VesselSpawner`), `MenuVesselSelectionPanelController` (network-aware menu
swap via `RequestSwap` + domain picker via `RequestSetDomain_ServerRpc`),
`ShipSelectionView` (type-driven grid), Hangar screens.

### 14.3 Loadout & unlock persistence

- `LoadoutSystem` (static, `System/LoadOut/`) — 4 player slots + per-game
  last-used configs (`Loadout { Intensity, PlayerCount, VesselType, GameMode,
  IsMultiplayer }`), persisted as **local JSON files** (`loadouts.data`,
  `game_loadouts.data`). `LoadoutCloudData`/`LoadoutRepository` exist in
  `UGSDataService` but have zero consumers (cloud mirror is scaffolding).
- `VesselUnlockSystem` (static) — mutates `SO_Vessel` lock state, persists to
  `HangarCloudData` (Cloud Save key `HANGAR_DATA`, keyed by vessel **Name**
  string), spends crystals via `PlayerDataService.TrySpendCrystals`; restored
  at sign-in by `UGSDataService.SyncHangarToVessels()` (unlock-only, never
  re-locks). Lock enforcement: picker filtering, click blocks, launch gates
  (`Arcade.LaunchArcadeGame`/`LaunchTrainingGame`), hangar overlays.
- Default vessel constants: bootstrap/menu = Squirrel; `ResetAllData` = Manta;
  selection-UI normalization + modal fallback + `MiniGame` statics = Dolphin;
  AI fallback = Sparrow; seeded loadout slot 0 = Manta.

---

## 15. Known discrepancies & tech-debt register

Verified against code (2026-07); useful when older docs/comments disagree:

1. **`VesselStatus` is a MonoBehaviour** — the NetworkBehaviour is
   `VesselController` (older CLAUDE.md revisions said otherwise).
2. Vessel prefabs live in **`Assets/_Prefabs/Spacevessels/`** (not
   `_Prefabs/Spaceships/`).
3. **Vessel audio is FMOD**, not Wwise.
4. `VesselController.PlayerNetId` is never assigned (always 0) — the
   authoritative player↔vessel link is `Player.NetVesselId`.
5. `CameraManager.SetupEndCameraFollow` has zero callers; menu camera is
   `MainMenuCameraController`. `SetNormalizedCloseCameraDistance` is a stub.
6. `ElementalFloat` binding for R_ action SOs is commented out
   (`ShipActionSO.Initialize`); `ElementalFloatBinder` is dead code.
7. `ElementPipsView` on Sparrow.prefab is wired but inert (superseded by
   `ElementalBarsView`).
8. `SilhouetteController.ElementBars` juice routing is exposed but uncalled;
   `SquirrelVesselHUDView` implements its own juice on its own icons.
9. Sparrow's impactor container carries a stale YAML key
   (`vesselElementalCrystalEffects`, renamed without FormerlySerializedAs) —
   that list silently no longer deserializes.
10. `Rhino/IncrementalBoostAction.asset` has a missing-script GUID.
11. Misplaced files: `KeyboardInputStrategy.cs` at `Assets/` root;
    `CloakSeedWallActionSO.cs` in `UI/View/`; three R_ files in the legacy
    `VesselActions/` folder; two HUD controllers in
    `R_VesselActions/Data Containers/`; `RoundStats`/`IRoundStats` in
    `Data/Enums/`.
12. `Trail2` on `VesselPrismController` is private and accessed via reflection
    in three wall-seeding call sites.
13. `ShardFieldBus` broadcast bodies are commented out — Dolphin's shard
    toggle is currently a visual no-op.
14. `VesselMineEffectSO` list on `MineImpactor` is non-serialized → mine→vessel
    effects are inert.
15. Adaptive zoom (`enableAdaptiveZoom`/`adaptiveMaxDistance`) has no active
    runtime driver; only Rhino's camera asset enables it.
16. `AllowClearPrismInitialization` gates ClearPrisms to owner-or-AI; remote
    client vessels skip it by design.

---

## 16. Key files index

| Role | File |
|---|---|
| Vessel contract | `Controller/Vessel/IVessel.cs`, `IVesselStatus.cs` |
| Networked vessel root | `Controller/Vessel/VesselController.cs` |
| State bag / component hub | `Controller/Vessel/VesselStatus.cs` |
| Theme/domain helper | `Controller/Vessel/VesselHelper.cs` (class `ShipHelper`) |
| Hull paint | `Controller/Vessel/VesselCustomization.cs`, `VesselTrailCustomization.cs` |
| Movement | `Controller/Vessel/VesselTransformer.cs` + `SingleStick`/`Command`/`Gun` variants, `DriftJet.cs`, `TrailFollower.cs`, `BlockscapeFollower.cs` |
| Input | `Controller/IO/InputController.cs`, `InputStatus.cs`, `IInputStrategy.cs` + strategies, `Assets/KeyboardInputStrategy.cs` |
| Actions | `Controller/Vessel/R_VesselActionHandler.cs`, `R_VesselActions/**` (see `ACTIONS.md`) |
| Resources / elementals | `Controller/Vessel/ResourceSystem.cs`, `Resource.cs`, `ElementalFloat.cs`, `ElementalVesselComponent.cs`, `R_VesselElementStatsHandler.cs` |
| Trail production | `Controller/Vessel/VesselPrismController.cs`, `SparrowPrismController.cs`, `Trail.cs`, `Prism.cs`; `Controller/Prisms/PrismFactory.cs` |
| Skimmer | `Controller/Vessel/Skimmer.cs`, `ForcefieldCrackleController.cs`; `Controller/ImpactEffects/Impactors/SkimmerImpactor.cs` |
| Prism shields | `Controller/Vessel/PrismOctahedronShield.cs`, `PrismStellatedOctahedronShield.cs`; `Controller/Managers/PrismStateManager.cs` |
| Impact effects | `Controller/ImpactEffects/Impactors/VesselImpactor.cs`, `NetworkVesselImpactor.cs`; `Containers/VesselImpactorDataContainerSO.cs`; `EffectsSO/**` |
| Silhouette / elemental bars | `Controller/Vessel/SilhouetteController.cs`, `SilhouetteView.cs`, `SilhouetteConfigSO.cs`; `UI/View/ElementalBarsView.cs`; `ScriptableObjects/ElementalBarsConfigSO.cs` |
| HUD | `UI/Interfaces/IVesselHUDController.cs`; `UI/Controller/VesselHUDController.cs` + per-vessel; `UI/View/VesselHUDView.cs` + per-vessel; `Controller/Vessel/VesselHUD.cs` (class `ShipHUD`) |
| Camera | `Controller/Camera/CameraSettingsSO.cs`, `CustomCameraController.cs`, `MainMenuCameraController.cs`; `Controller/Managers/CameraManager.cs`; `Controller/Vessel/VesselCameraCustomizer.cs` |
| Animation | `Controller/Animation/VesselAnimation.cs` + per-vessel subclasses |
| Audio | `Controller/Vessel/Audio/ShipAudioController.cs`, `ShipStudioListenerGate.cs`; `Controller/FX/DriftAudioController.cs`, `ProximityBoostAudioController.cs` |
| Telemetry / stats | `Controller/Vessel/VesselTelemetry.cs` + subclasses, `VesselTelemetryBootstrapper.cs`, `VesselStatEventSO.cs`, `VesselStatsCloudData.cs`, `EventDrivenStatsProvider.cs`; `Data/Enums/RoundStats.cs`, `IRoundStats.cs`; `Controller/Managers/StatsManager.cs`; `UI/UGSStatsManager.cs` |
| AI | `Controller/AI/AIPilot.cs`, `AIGunner.cs`; `ScriptableObjects/SO_AIProfileList.cs`; `Utility/DataContainers/AICinematicBehavior.cs` |
| Spawning | `Controller/Vessel/VesselSpawner.cs`; `Controller/Player/PlayerSpawner.cs` + adapters; `Controller/Multiplayer/ServerPlayerVesselInitializer.cs` (+ `WithAI`, `Menu`), `ClientPlayerVesselInitializer.cs`; `ScriptableObjects/SOAP/VesselPrefabContainer.cs` |
| Data model | `ScriptableObjects/SO_Vessel.cs`, `SO_VesselList.cs`, `SO_VesselAbility.cs`, `SO_Captain.cs`; `System/VesselUnlock/VesselUnlockSystem.cs`; `System/LoadOut/LoadoutSystem.cs`; `System/CloudData/Models/HangarCloudData.cs` |
| Caches / hooks | `Controller/Vessel/NetworkClientCache.cs`, `NetworkVesselClientCache.cs`, `NetworkPlayerClientCache.cs`; `Utility/Network/NetcodeHooks.cs` |
| Occlusion fade | `Controller/Vessel/ClearPrisms.cs` |
| Prefabs | `Assets/_Prefabs/Spacevessels/*.prefab` (+ `Components/`), `Assets/_Prefabs/UI Elements/VesselHUD/*.prefab` |
