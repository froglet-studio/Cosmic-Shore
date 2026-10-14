# Duo vessel: code anchors

Where a duo plugs into the game, as of **2026-10-14** (`bleeding-edge` at `8faad765`). `C/` means
`Assets/_Scripts/Controller/`. **Re-verify an anchor before relying on it** (grep the class name,
not the file name; see `/vessel` §2.5). When something here gets built or moves, update this file
the same day.

## 1. The big picture

**The swap a duo needs already exists: it is the arena PILOT SWAP, and it is already on the
D-pad.** What does not exist is a vessel made of two hulls. Everything assumes **one Player flies
one Vessel**, and flight, ability input and the AI's stick all go through `Player.InputStatus`
(`IVesselStatus.InputStatus => Player.InputStatus`). The AI writes its stick into the Player it
pilots for.

So the cheapest duo is **each half is an ordinary vessel with its own Player (a human, or an AI
Player), and the duo is a SEAT that groups the two.** A swap is then exactly `PilotSwap.ApplyLocal`
restricted to the partner hull: the human Player and the AI Player trade hulls. This is option B
in SKILL.md §3.1, and it reuses the most code. Option A (one Player, two hulls) means rewriting the
one-Player-one-Vessel assumption. Do not start A without Garrett's call.

## 2. What exists (reuse it)

### Seats, classes, spawning
- **Teams:** `Assets/_Scripts/Data/Enums/Domains.cs`: Jade=1, Ruby=2, Blue=3, Gold=4.
- **`VesselClassType`:** `Assets/_Scripts/Data/Enums/VesselClassType.cs`.
  - Values run up to Stoat=15. **The next free value is 16. Read the enum again before taking
    it.**
  - `EnumIntegrityTests` guards the enum.
- **Card seat counts:** `Assets/_Scripts/ScriptableObjects/SO_ArcadeGame.cs`.
  - `MinPlayersAllowed` / `MaxPlayersAllowed`, `MinDomainsAllowed` / `MaxDomainsAllowed`.
  - `ArenaRules` gives one pilot per hull plus the D-pad swap.
  - `MaxSeats` = min(MaxPlayers, `DistinctHullCount`).
  - `Vessels` is the card's roster.
- **Match state:** `Assets/_Scripts/Utility/DataContainers/GameDataSO.cs`.
  - `selectedVesselClass`, `SelectedPlayerCount`, `RequestedAIBackfillCount`,
    `RequestedAIVessels`, `RequestedDomainCount`, `IsArenaMatch`.
  - `SwapVessels()`: the Cellular Duel hull exchange.
- **Human spawn:** `C/Multiplayer/ServerPlayerVesselInitializer.cs`.
  - `SpawnVesselForPlayer(clientId, Player)`, `ResolveSpawnVesselType`.
- **AI backfill:** `C/Multiplayer/ServerPlayerVesselInitializerWithAI.cs`.
  - `SpawnAIs` (one AI per `RequestedAIBackfillCount`), `ResolveAIVesselType`,
    `GetBalancedDomain`, static `ConfigureAIPilotForMode(AIPilot, GameDataSO)`.
  - **This is where a duo's AI half is spawned beside its human half.**
- **Hull per AI seat:** `C/Multiplayer/AIHullSeating.cs`.
- **Prefabs:** `Assets/_Scripts/ScriptableObjects/SOAP/VesselPrefabContainer.cs`
  (`TryGetShipPrefab`).
- **Class SO:** `Assets/_Scripts/ScriptableObjects/SO_Vessel.cs`, with assets in
  `Assets/_SO_Assets/Classes/SO_Class_*.asset`.
- **Party size:** `HostConnectionDataSO.MaxPartySlots` = 4 (asserted in
  `HostConnectionDataSOTests`).

### Player ↔ vessel, input, AI
- **`IPlayer` / `Player`:** `C/Player/IPlayer.cs`, `C/Player/Player.cs`.
  - A Player has one `Vessel`, `InputController`, `InputStatus`, `IsLocalPilot` and
    `IsInitializedAsAI`.
  - `ChangeVessel(IVessel)`, `RequestPilotSwap(int)` → `RequestPilotSwap_ServerRpc`.
  - `NetDefaultVesselType`, `NetArenaHullClaim`.
- **`VesselController.ChangePlayer`** (`C/Vessel/VesselController.cs`) retargets everything that is
  local-pilot-only (the occlusion corridor, speed tunnel, vision shading, rear view, placement
  view) and the HUD. **A swap gets these for free.** `ToggleAIPilot(bool)` starts or stops the AI.
- **`InputController`** (`C/IO/InputController.cs`) runs each frame: `RearViewGesture`, then
  `PilotSwapGesture`, then the strategy's `ProcessInput()`, then `orientationHandler.Update()`.
- **`R_VesselActionHandler`:** latched input subscription, `ReleaseHeldInputs`, `SetInputSuppressed`.
  **A held button must be released on swap.**
- **`AIPilot`** (`C/AI/AIPilot.cs`): `StartAIPilot` / `StopAIPilot`, `SetExternalTargetProvider`
  (a duo AI's target, e.g. "follow the Flier's trail"), `TakeModeHooksFrom`.

### The swap (PilotSwap, the template)
- **`C/Multiplayer/PilotSwap.cs`:**
  - `TryValidateServer`, `TransferOwnershipServer` (`NetworkObject.ChangeOwnership`).
  - `ApplyLocal`: `human.ChangeVessel(aiHull)`, `ai.ChangeVessel(humanHull)`, `ChangePlayer`, then
    `VesselTransformer.AdoptCurrentMotion()`, then the AI restart.
  - `VerifyLocalTakeoverAsync` fixes the camera if needed.
  - `CooldownSeconds = 0.75`.
  - Design: `Docs/HomeHub/ARCHITECTURE.md` §3.9.
- **`C/IO/PilotSwapGesture.cs`:** D-pad **left/right** (plus keyboard 1/2) cycles the team's hulls
  on arena cards. **D-pad up/down are free in flight.** Other D-pad readers: `SpectatorController`
  (left/right), menu navigation (`ArcadeDPadNav`), `DuelCellStatsRoundUIController` (up, in a UI).
  The D-pad is read directly (`Gamepad.current.dpad`), not through an InputActions asset or
  `InputEvents`.

### Phone flip
- **`C/IO/DeviceOrientationHandler.cs`:** the accelerometer raises `InputEvents.FlipAction`
  (=3) through the Player's `InputStatus`. On a pad, FlipAction is the **right shoulder**; on a
  keyboard it is **E**.
- **FlipAction is an ability input**: the Grizzly binds it (cycle weapon). **A duo that swaps on
  the flip must not also bind FlipAction as an ability on either half**, and must decide whether
  the right shoulder on a pad also swaps or stays an ability button.
- `C/IO/PhoneFlipDetector.cs` (static `onPhoneFlip`), `FlipUI.cs` (rotates the UI 180°).

### Cameras
- `C/Managers/CameraManager.cs`: `SetupGamePlayCameras(Transform)`, `SnapPlayerCameraToTarget`.
- `C/Vessel/VesselCameraCustomizer.cs`: `RetargetAndApply(IVessel)` applies the hull's
  `CameraSettingsSO`.
- **Serpent far cam:** `Assets/_SO_Assets/Camera/SerpentCameraSettingsSO.asset` (fixed mode,
  follow z = -250, dynamic min/max 250). `CameraSettingsSO.dynamicMaxDistance` is the far-cam
  distance.

### Flight references
- **Serpent:** `C/Vessel/SingleStickVesselTransformer.cs` (one stick, stationary stance).
  - Its impactor container has no skimmer effects, so it rams through prisms.
  - Docs: `R_VesselActions/SERPENT_*.md`.
  - The Serpent has no "ignore collisions" flag; a Flier that ignores collision needs its own.
- **Rhino straight-line boost:**
  - `C/IO/StraightLineGesture.cs` → `InputEvents.FullSpeedStraightAction`.
  - `RampBoostActionSO` / `RampBoostActionExecutor` (graded).
  - Doc: `R_VesselActions/RHINO_RAMP_BOOST.md`.
  - Reuse it at a lower ceiling for the Flier.
- **One / two thumbs:** `TouchInputStrategy`, `DualStickMix.cs`,
  `VesselStatus.IsSingleStickControls`. Doc: `C/IO/ONE_THUMB_MOUSE_CONTROLS.md`.

### Trails, prisms, the spline
- **Spawner:** `C/Vessel/VesselPrismController.cs` (`CreateBlock`, `StartSpawn`). The Flier's
  alternating block angles go here.
- **The spline already exists:** `C/Vessel/Trail.cs` `Project(...)` rides a **Catmull-Rom** curve
  through block centres (`CatmullRom` / `CatmullRomTangent`).
- **Riders:** `C/Vessel/TrailFollower.cs` (1D ride along a trail; the Urchin rider,
  `URCHIN_TRAIL_RIDER.md`) and `C/Vessel/BlockscapeFollower.cs` (2D surface ride). **These are the
  nearest thing to the Bird's 2.5D motion.**
- **Attraction:** `SkimmerAlignPrismEffectSO` (nose and position nudged along the trail; used by
  the Squirrel and Thresher), and `VesselAttachPrismEffectSO`.
- **Prism state:** `C/Vessel/Prism.cs` (`Domain`, `Damage`, `MakeDangerous`),
  `C/Environment/PrismProperties.cs` (`IsShielded`, `IsSuperShielded`, `IsDangerous`,
  `IsTransparent`), `C/Managers/PrismStateManager.cs`.
- **Friendly fire:**
  - **Ramming has no team check, so teammate prisms already break on contact.** The Bird's
    friendly fire is the default; the work is the exemption for the Flier's trail (see
    `C/ImpactEffects/SELF_TRAIL_CONTACT.md`, `SelfTrailContactConfigSO`).
  - Projectiles (`Projectile.friendlyFire`) and explosions (`ExplosionImpactor` `affectSelf`)
    spare their own team by default.
- **Per-viewer visibility precedent:** `CloakSeedWallActionExecutor` (Serpent cloak) picks the
  trail alpha by `IsLocalUser`: visible to the pilot, 0 for everyone else. Also
  `Assets/_Scripts/Utility/VesselVisionShading.cs` and `PrismOcclusionCorridor`, both
  local-pilot only. **There is no general "only player X sees this" API.**
- **Ghosting:** `UrchinSlipActionExecutor` (`GhostAsync`, `SetHullSolid`, `IsGhosted`). Legacy
  `C/Vessel/VesselActions/GhostAction.cs` disables colliders.

### Crystals, resources
- **Crystal effects:** `C/ImpactEffects/EffectsSO/Vessel Crystal Effects/`.
  - `VesselChangeResourceByCrystalEffectSO` (restock), `VesselExplosionByCrystalEffectSO`, plus
    per-vessel ones.
  - Wired per hull in `*ImpactorDataContainer.asset`.
  - Network replay: `NetworkCrystalManager.ReplayVesselCrystalEffects`.
- **`C/Vessel/ResourceSystem.cs`:** `ChangeResourceAmount`, `SetResourceAmount`, element levels.

### Launch / aim
- **Thresher aimed launch:** the sticks aim, `ThresherLaunchSight` draws the crosshair, and
  `ThresherChainSolver.ArmLaunch`. See `THRESHER.md` §1, §5a.
- **Stoat sling:** `StoatSlingExecutor`, `StoatSlingMath`.
- **Aim preview:** `IAimTelegraphAction` (Dolphin Echo Sight).

### Explosions
- `C/Projectiles/`: `AOEExplosion` (+ variants), `Mine`, `ChainReactionBudget` (cascade brake),
  `ProjectileDetonatorSO`.
- Detonation examples: `GrizzlyTriggerBombExecutor`, `MantaBomb*`.

## 3. What does NOT exist (build once, generically where it says so)

| Piece | Generic or per duo | Nearest thing to start from |
|---|---|---|
| A seat that holds two Players / two hulls and counts as one (team size, backfill, scoreboard, vessel select) | **generic** | `PilotSwap` ring, `AIHullSeating`, `RequestedAIVessels` |
| Swap restricted to the partner half, on a non-arena card | **generic** | `PilotSwap` + `PilotSwapGesture` |
| Swap input on D-pad up/down (or the agreed direction) and on the phone flip | **generic** | `PilotSwapGesture`, `DeviceOrientationHandler` |
| Co-op offer/accept handshake (two humans trading halves) | **generic** | none |
| Party/lobby: two players share one seat | **generic** | `PartySessionService`, `MaxPartySlots` |
| A shared cooldown / charges component | **generic, fleet-wide** | none; cooldowns are private per executor. `RateLimitCooldown.cs` is the only shared one |
| "Restock everything" on a crystal (charges + cooldowns) | generic once the above exists | `VesselChangeResourceByCrystalEffectSO` |
| Per-viewer rendering of prisms or a curve | **generic** | the Serpent cloak's `IsLocalUser` alpha |
| Speed gauge on the HUD | generic | `ThresherHUDView` fill |
| Speed from distance to another object | per duo (Flier) | `StoatPathfinderExecutor` `FlightTimeScale`, `SkimmerModifyThrotleByPrismEffectSO` |
| 2.5D / strip-constrained flight with warped yaw | per duo (Bird) | `TrailFollower`, `BlockscapeFollower`, `Trail.Project` |
| Alternating-angle trail blocks | per duo (Flier) | `VesselPrismController.CreateBlock` |
| Drag-to-aim launch on mobile | generic | `ThresherLaunchSight` (stick aim only) |
| Prime / hot prism states and their decay | per duo (Flier & Bird), maybe fleet | `PrismProperties` flags, `MakeDangerous` |
| Homing ("heat-seeking") projectiles, one per hot target | generic | `Projectile`, `Gun`; Sparrow missiles are unguided |
| An "ignore collision" flight for a whole hull | per duo (Flier) | the Urchin slip ghost |
