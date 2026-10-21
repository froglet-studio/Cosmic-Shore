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
Player), and the duo is a SEAT that groups the two.** A swap is then exactly `PilotSwap`, with the
partner hull in the ring: the human Player and the AI Player trade hulls. Score already follows
the pilot, so the seat's score is the sum of its two Players. This is option B
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

### The crew pieces (built 2026-10-18; use them, never re-roll them)

| Piece | File | What it owns |
|---|---|---|
| `ICrewSeat` | `Controller/Vessel/Crew/CrewSeat.cs` | What makes a hull a crew hull: a component on its root with `SeatName` and `HumanInSeat`. `GobyPistolDuo` (Goby) implements it; `KiteNavigator` (Wrasse) joins in KITE_NAVIGATOR.md's work order, step 2 |
| `CrewSeat` | same | The wake rule (`ShouldWake`), the swap (`PollSwap`: `PilotSwapGesture`, `PilotSwap.CooldownSeconds`), the hand-over (`HandHullToAI` / `GiveHullBack`, `SetFlightInputPaused` through the player's `InputController`) |
| `CrewPictureInPicture` | `Controller/Vessel/Crew/CrewViews.cs` | The two views' layout: the seat's view a lower-left square while the human flies the hull; full screen, with the pilot camera (`CameraManager.ViewCamera`) in the square, while the human is in the seat; the pilot camera's rect and depth restored |
| `CrewViewRender` | same | A seat camera's look, for its render only: `PrismDepthPlane` (depth from the seat's plane) and `PrismOcclusionCorridor.BeginViewOverride` (the dither corridor re-aimed), restored in `endCameraRendering` |
| `SO_Vessel.CrewNames` / `CrewNameTemplate` / `CardName` | `ScriptableObjects/SO_Vessel.cs` | The card that names both subvessels; `Name` stays the key |
| `check_crew_vessels.py`, `CrewVesselTests` | `Tools/Build/`, `Tests/Editor/` | Every hull with an `ICrewSeat` has the crew card, is on the roster and in the container; every crew card has a seat |
| `author_<hull>_assets.py` | `Tools/Build/` | Clones a donor hull, puts the seat on its root, registers it everywhere (`author_wrasse_assets.py` also strips the donor's kit and swaps the HUD controller) |

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
  (a duo AI's target, e.g. "follow the Goby's trail"), `TakeModeHooksFrom`.

### The swap (PilotSwap, the template)
- **`C/Multiplayer/PilotSwap.cs`:**
  - `TryValidateServer`, `TransferOwnershipServer` (`NetworkObject.ChangeOwnership`).
  - `ApplyLocal`: `human.ChangeVessel(aiHull)`, `ai.ChangeVessel(humanHull)`, `ChangePlayer`, then
    `VesselTransformer.AdoptCurrentMotion()`, then the AI restart.
  - `VerifyLocalTakeoverAsync` fixes the camera if needed.
  - `CooldownSeconds = 0.75`.
  - Design: `Docs/HomeHub/ARCHITECTURE.md` §3.9.
- **`C/IO/PilotSwapGesture.cs`:** D-pad **left/right** (plus keyboard 1/2) cycles the team's
  hulls on arena cards. **The duo swap uses this exact gesture** (SKILL.md §2.2, LOCKED); only the
  ring changes. D-pad up/down are unread in flight. Other D-pad readers: `SpectatorController`
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
- These are all **chase** cameras. The Pistol's camera sits **beside** it, sidescroller-style, in
  its warped world, which `CameraMode` has no mode for.

### Flight references
- **Squirrel (the Goby's flight, Garrett 2026-10-14):** the dual-stick `VesselTransformer` on `Squirrel.prefab` (throttle 60, pitch / yaw 120, roll 130); the lab mirrors the Squirrel studio's `stepPilot` mix.
- **Serpent:** `C/Vessel/SingleStickVesselTransformer.cs` (one stick, stationary stance). It was the brief's first reading of the Goby; it is one-stick, so it is not the Goby's flight.
  - Its impactor container has no skimmer effects, so it rams through prisms.
  - Docs: `R_VesselActions/SERPENT_*.md`.
  - The Serpent has no "ignore collisions" flag; a Goby that ignores collision needs its own.
- **Rhino straight-line boost:**
  - `C/IO/StraightLineGesture.cs` → `InputEvents.FullSpeedStraightAction`.
  - `RampBoostActionSO` / `RampBoostActionExecutor` (graded).
  - Doc: `R_VesselActions/RHINO_RAMP_BOOST.md`.
  - Reuse it at a lower ceiling for the Goby.
- **One / two thumbs:** `TouchInputStrategy`, `DualStickMix.cs`,
  `VesselStatus.IsSingleStickControls`. Doc: `C/IO/ONE_THUMB_MOUSE_CONTROLS.md`.

### Trails, prisms, the spline
- **Spawner:** `C/Vessel/VesselPrismController.cs` (`CreateBlock`, `StartSpawn`). The Goby's
  alternating block angles go here.
- **The spline already exists:** `C/Vessel/Trail.cs` `Project(...)` rides a **Catmull-Rom** curve
  through block centres (`CatmullRom` / `CatmullRomTangent`).
- **Riders:** `C/Vessel/TrailFollower.cs` (1D ride along a trail; the Urchin rider,
  `URCHIN_TRAIL_RIDER.md`) and `C/Vessel/BlockscapeFollower.cs` (2D surface ride). **These are the
  nearest thing to the Pistol's 2D motion.**
- **Attraction:** `SkimmerAlignPrismEffectSO` (nose and position nudged along the trail; used by
  the Squirrel and Thresher), and `VesselAttachPrismEffectSO`.
- **Prism state:** `C/Vessel/Prism.cs` (`Domain`, `Damage`, `MakeDangerous`),
  `C/Environment/PrismProperties.cs` (`IsShielded`, `IsSuperShielded`, `IsDangerous`,
  `IsTransparent`), `C/Managers/PrismStateManager.cs`.
- **Friendly fire:**
  - **Ramming has no team check, so teammate prisms already break on contact.** The Pistol's
    friendly fire is the default; the work is the exemption for the Goby's trail (see
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
| The duo ring in `PilotSwap` (the partner half, on any card; with the team ring on arena cards: open) | **generic** | `PilotSwap.TryPickRingTarget`, `CollectTeamHulls` |
| Phone flip as the mobile form of the swap gesture (the Grizzly's FlipAction binding has to be resolved first) | **generic, fleet-wide** | `PilotSwapGesture`, `DeviceOrientationHandler` |
| Co-op offer/accept handshake (two humans trading halves; `PilotSwap` only trades a human with an AI) | **generic** | `PilotSwap.TryValidateServer` |
| One element meter shared by two hulls that two machines may own | **generic** | `ResourceSystem`; levels do not replicate today |
| Party/lobby: two players share one seat | **generic** | `PartySessionService`, `MaxPartySlots` |
| A shared cooldown / charges component | **generic, fleet-wide** | none; cooldowns are private per executor. `RateLimitCooldown.cs` is the only shared one |
| "Restock everything" on a crystal (charges + cooldowns) | generic once the above exists | `VesselChangeResourceByCrystalEffectSO` |
| Per-viewer rendering of prisms or a curve | **generic** | the Serpent cloak's `IsLocalUser` alpha |
| Speed gauge on the HUD | generic | `ThresherHUDView` fill |
| Speed from distance to another object | per duo (Goby) | `StoatPathfinderExecutor` `FlightTimeScale`, `SkimmerModifyThrotleByPrismEffectSO` |
| The unrolled-ribbon world: map world points to (s, h, d) and back, constrain the Pistol to the plane, draw parallax depth layers and distorted prisms, on the Pistol's client only | per duo (Pistol) | **The maths exists** (2026-10-15): `PistolRibbon`, `PistolBodyMath`, `VortexRing`, `PistolAI` in `Controller/Vessel/Crew/GobyPistol/`; `GobyPistolDuo` (on `Goby.prefab`, `VesselClassType.Goby`) draws the warped world through a second camera on the local pilot's machine. Still no seat, no networking |
| A side-on sidescroller camera mode | generic | `CameraSettingsSO.CameraMode` |
| Alternating-angle trail blocks | per duo (Goby) | `VesselPrismController.CreateBlock` |
| Drag-to-aim launch on mobile | generic | `ThresherLaunchSight` (stick aim only) |
| Prime / hot prism states and their decay | per duo (Goby & Pistol), maybe fleet | `PrismProperties` flags, `MakeDangerous` |
| Homing ("heat-seeking") projectiles, one per hot target | generic | `Projectile`, `Gun`; Sparrow missiles are unguided |
| An "ignore collision" flight for a whole hull | per duo (Goby) | the Urchin slip ghost |
