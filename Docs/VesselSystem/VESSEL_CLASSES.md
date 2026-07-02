# Vessel Classes — Per-Class Reference

Per-class reference for all 11 vessel classes. Shared architecture is in
`ARCHITECTURE.md`; the action/ability inventory is in `ACTIONS.md`.

Prefabs: `Assets/_Prefabs/Spacevessels/{Manta,Dolphin,Rhino,Urchin,Grizzly,Squirrel,Serpent,Termite,Falcon,Shrike,Sparrow}.prefab`
(+ shared parts in `Spacevessels/Components/`). Every prefab carries an
`AIPilot`. Class SOs: `Assets/_SO_Assets/Classes/SO_Class_*.asset`
(9 authored — no Falcon/Shrike class asset yet).

```csharp
// Data/Enums/VesselClassType.cs
Any=-1, Random=0, Manta=1, Dolphin=2, Rhino=3, Urchin=4, Grizzly=5,
Squirrel=6, Serpent=7, Termite=8, Falcon=9, Shrike=10, Sparrow=11
```

## Status matrix

| Vessel | ID | Pipeline | Genre / role | Signature mechanics | Dedicated HUD | Telemetry | Transformer | Camera settings asset |
|---|---|---|---|---|---|---|---|---|
| **Manta** | 1 | Modern (R_) | Feature-complete playable | Yawstery steering, **skimmer overcharge** | ✅ Manta | Default (bootstrapped) | Dual-stick | ✅ Fixed |
| **Dolphin** | 2 | Modern (R_) | Feature-complete playable | Charge boost, drift, team-crystal deploy, shard toggle | ✅ Dolphin | Default (bootstrapped) | Dual-stick | ✅ (legacy schema) |
| **Rhino** | 3 | Modern (R_) | Feature-complete playable | Shield skimmer (grow/ram), trail growth, danger-prism debuffs | ✅ Rhino | Default (bootstrapped) | Dual-stick | ✅ Fixed + adaptive zoom |
| **Urchin** | 4 | **Legacy** | Playable (AI in progress) | Trail attach/ride, surface crawl, gun barrages, ghost cloak | — | — | GunVesselTransformer | ❌ null |
| **Grizzly** | 5 | **Legacy** | Playable (AI in progress) | Charged gun, turret mode | — | — | Single-stick | ❌ null |
| **Squirrel** | 6 | Modern (R_) | Racing/drift — vaporwave arcade racer | **Analog drift**, skim-boost economy, twin-rail tube trail | ✅ Squirrel | **SquirrelVesselTelemetry** | Dual-stick (+DriftJet) | ✅ (legacy schema) |
| **Serpent** | 7 | Modern (R_) | Playable with dedicated HUD | Boost magazine, seed wall, **cloak + seed**, stationary wall mode | ✅ Serpent | Default (bootstrapped) | Single-stick | ✅ Fixed |
| **Termite** | 8 | **Legacy** | Planned | Command-cursor movement, drone/boid swarm | — | — | CommandVesselTransformer | ❌ null |
| **Falcon** | 9 | **Legacy** | Planned | Full-auto turret ring | — | — | (mixed refs — verify in editor) | ❌ null |
| **Shrike** | 10 | **Legacy** | Planned | Full-auto turret ring + wall assembler | — | — | (mixed refs — verify in editor) | ❌ null |
| **Sparrow** | 11 | Modern (R_) | Shooter — arcade space combat | Guns + missiles, **overheat danger trail**, stationary block-shot turret | ✅ Sparrow | **SparrowVesselTelemetry** | Single-stick | ✅ (legacy schema) |

"Modern (R_)" = the SO/executor action pipeline + full network stack
(`VesselController`, `ActionExecutorRegistry`, `NetworkVesselImpactor`,
`NetcodeHooks`, `NetworkVesselClientCache`). "Legacy" prefabs still carry
MonoBehaviour `ShipAction` components and lack parts of the modern stack
(Urchin has no `VesselController`/`ActionExecutorRegistry`).

Common modern-vessel component stack: `VesselStatus`, `VesselController`,
`R_VesselActionHandler`, `R_VesselElementStatsHandler` (class
`R_ShipElementStatsHandler`), `ActionExecutorRegistry`, `ResourceSystem`,
`VesselPrismController` (Sparrow: `SparrowPrismController`), `Skimmer`,
`SilhouetteController` (+`SilhouetteView` — Serpent carries the controller
without the view), `VesselCameraCustomizer`, `VesselCustomization`,
`NetcodeHooks`, `NetworkVesselClientCache`,
`VesselImpactor`+`NetworkVesselImpactor`+`ImpactCollider`, `AIPilot`,
`ShipStudioListenerGate`, telemetry (bootstrapper or concrete). `Pip`
(picture-in-picture camera) is authored only on Manta, Serpent, and Squirrel.

Network transform sync: Sparrow/Squirrel = `ClientNetworkTransform`
(owner-authoritative); Manta/Dolphin/Rhino/Serpent = package `NetworkTransform`;
Urchin/Grizzly/Termite/Falcon/Shrike = none.

Input event IDs referenced below: `0=FullSpeedStraight, 1=RightStick,
2=LeftStick, 3=Flip, 6=Button1, 7=Button2, 8=Button3, 11=OnlyRightStick,
12=OnlyLeftStick, 13=BothSticks`.

---

## Manta (1)

- **Input → actions**: 11 → `YawsteryAction-Right`, 12 → `YawsteryAction-Left`
  (hold-to-yaw with ramp in/out, speed coupling), 13 → `BoostAction`.
- **Signature — skimmer overcharge** (`SkimmerOverchargeCollectPrismEffectSO`):
  skimming own-domain prisms shields them; skimming enemy prisms accumulates
  unique hits (blend to overcharged material) toward `maxBlockHits` (authored
  200 on the wired asset; script default 30) → HUD 3-2-1 countdown →
  `ConfirmOvercharge` chain-devastates the collected prisms outward (raycast
  along `TrailBlocks`, 0.1 s stagger) → 5 s cooldown.
- **HUD** (`MantaVesselHUDController`/`MantaVesselHUDView`): prism-hit count +
  radial fill (yellow at max), overcharge countdown + "OVERCHARGED!" toasts.
  Controller filters the SO's global events by its own `SkimmerImpactor`.
- **Animation**: `MantaAnimation` (transform puppetry — fuselage, wings, 4
  thrusters). **Crystal juice**: `VesselExplosionByCrystalEffectSO` fires the
  static `OnMantaFlowerExplosion` → silhouette flower overlay.
- **Camera**: Fixed, offset (0,0,-30), far clip 12000.

## Dolphin (2)

- **Input → actions**: 2 (LeftStick) → [`DolphinDriftAction`,
  `ChargeBoostAction`, `DriftTrailAction`, `ShardToggleAction`],
  1 (RightStick) → `DeployTeamCrystalAction`.
- **Signature — charge boost** (`ChargeBoostActionSO`): hold to fill resource
  slot 1 (authored 4 s to full; script default 2 s), release to discharge as a
  boost multiplier (max 2×) over 2 s; authored 4 s recharge cooldown (script
  default 1 s).
- **Team crystal deploy** (`DeployTeamCrystalActionSO`): press = ghost crystal
  held ahead of the vessel; release = detach + activate as an own-domain
  crystal.
- **Shard toggle** (`ShardToggleActionSO`): redirect the cell's shard field at
  the densest opposing-mass position vs restore-to-crystal — currently a
  visual no-op (`ShardFieldBus` broadcasts are commented out).
- **HUD** (`DolphinVesselHUDController` — lives in
  `R_VesselActions/Data Containers/`): charge bar stepping through sprites from
  resource index 0. ⚠️ Only per-vessel controller without a local-user gate.
- **Animation**: `RiptideAnimation` ("Riptide" = Dolphin's legacy codename) —
  drift reparenting under a `DriftHandle` aimed along `Course`, jaw driven by
  the ammo resource. AIPilot flag `drift = true`.
- **Camera**: legacy-schema asset, offset (0,0,-20).

## Rhino (3)

- **Input → actions**: 0 (FullSpeedStraight) → [`BoostAction`,
  `GrowTrailAction`] — boost + trail growth while flying full-speed-straight.
- **Signature — shield skimmer**: `ShieldSkimmerScaleDriver` scales the skimmer
  with the shield resource (slot 0) per `ShieldSkimmerScaleConfigSO` (base 30 →
  max 120; crystal pickup pins at max — authored 9 s, script default 5 s);
  `GrowSkimmerActionExecutor` /
  `ZoomOutActionExecutor` on the prefab. Ramming:
  `RhinoSkimmerDamagePrismEffectSO` — damages prisms (inertia 70); bounces off
  super-shielded prisms.
- **Danger-prism offense**: `SparrowDebuffByRhinoDangerPrismEffectSO` mutes the
  victim's `Button2Action` 5 s on danger-prism contact (domain-blind, locked
  design); `VesselDamageBySkimmerEffectSO` (Rhino-attacker-only input mute) and
  `VesselDangerBlockFormationBySkimmerEffectSO` (danger hemisphere at the
  victim, oriented toward the cell crystal).
- **HUD** (`RhinoVesselHUDController`/`RhinoVesselHUDView`): skimmer-size icon
  (50→100 px), crystal-explosion flash + unique-victims-slowed counter, debuff
  timer (from `ScriptableEventSkimmerDebuffApplied`, filtered attacker==self).
- **Animation**: `RhinoAnimation` (counter-rotating wings/engines with brake);
  `ProceduralJetMesh` ribbon jets. AIPilot flag `ram = true`.
- **Camera**: Fixed, offset (0,0,-120), **adaptive zoom enabled (max 200)** —
  the only asset with it on (no active runtime driver, see ARCHITECTURE §15).
- ⚠️ `Rhino/IncrementalBoostAction.asset` has a missing script.

## Urchin (4) — legacy

- **Kit** (legacy `ShipAction` MonoBehaviours + `Gun`): `FireGunAction`,
  `FireBarrageAction`, `EnergizeAction` (temporarily buffs a list of guns),
  `GhostAction` (collider-off cloak), `DetachAction`.
- **Signature — trail riding**: `GunVesselTransformer` state machine on
  `IsAttached` (set by `VesselAttachPrismEffectSO` on prism impact) →
  `BlockscapeFollower` crawls the prism surface (edge-aware face rolling) /
  `TrailFollower` rides trails with Friendly/Hostile/Destroyed terrain speeds;
  ammo recharges while attached (×2 on shielded prisms);
  `FinalBlockSlideEffects()` per block: restore destroyed, grow friendly,
  steal hostile. `TrailViewer` makes nearby ridden blocks transparent.
- No `VesselController`/`ActionExecutorRegistry`/`VesselImpactor` — pre-modern
  component set. `UrchinAnimation` (body spin while attached).
- No prism pool authored (see `BOOTSTRAP_AUDIT.md`).

## Grizzly (5) — legacy

- **Kit**: `ChargedFireGunAction` (charge energy while held),
  `DetonateProjectilesAction`, `SpinAroundAction`, `ToggleTurretModeAction`
  (stationary + doubled resource gain). `Gun`,
  `SingleStickVesselTransformer`. (No drones — the drone/boid stack lives on
  Termite only.)
- **Animation**: `BufoAnimation` ("Bufo" = Grizzly's legacy codename; portrait
  mode remaps axes). No dedicated HUD/telemetry/camera asset/prism pool.

## Squirrel (6)

The drift racer and the menu lava-lamp default vessel
(`AppManager.ConfigureGameData` sets `selectedVesselClass = Squirrel`).

- **Input → actions**: 11 (OnlyRightStick) → [`SquirrelDriftAction`,
  `DriftTrailAction`], 12 (OnlyLeftStick) → same, 13 (BothSticks) →
  [`SquirrelSharpDriftAction`, `DriftTrailAction`].
- **Signature — analog drift**: `DriftActionSO` →
  `VesselTransformer.BeginDrift` — nose/course decoupling scaled by the analog
  trigger sum (gamepad LT/RT; touch = finger-lift gesture; keyboard Shifts);
  single vs sharp drift tiers. `DriftTrailActionExecutor` feeds
  `VesselPrismController.SetDotProduct` — sideways drift fattens prisms and
  compresses wavelength (denser tube). `DriftJet` visuals point along the
  actual course. Drift HUD SOAP events (`OnDriftingStarted` /
  `OnDoubleDriftingStarted` / `OnDriftEnded`) raise **local-user only**.
- **Boost economy — skim-driven**: no boost button;
  `SkimmerBoostPrismEffectSO` adds `+0.1` per skimmed prism (clamped by shared
  `boostBaseMultiplier`/`boostMaxMultiplier` SOAP variables), **×10 on danger
  prisms** — its own overheat trail is the risk/reward surface. Boost decays
  toward 1 in the transformer (`decayBoost`); prism collision resets it
  (`VesselResetBoostPrismEffectSO`).
- **HUD** (`SquirrelVesselHUDController` — in
  `R_VesselActions/Data Containers/` — + `SquirrelVesselHUDView`): boost bar
  colored by the skimmed prism's **source domain** (persists across decay
  frames; `Domains.Blue` = none), drift/double-drift icon states, joust danger
  flash, crystal shield flash. All shared SOAP channels filtered by
  `payload.VesselStatus`/player name (multiplayer-safe pattern).
- **Telemetry** (`SquirrelVesselTelemetry`, authored on prefab): MaxCleanStreak
  (crystals), JoustsWon, PrismsStolen + universal drift/boost/prisms-damaged.
- **Audio**: the fully instrumented vessel — `ShipAudioController` (FMOD engine
  with Speed/Tilt/element params), `DriftAudioController`,
  `ProximityBoostAudioController`.
- **Misc**: `ClientNetworkTransform`, `AICinematicBehavior` (dormant),
  `ElementalBarsView` wired, `VesselTrailCustomization`, legacy
  `ToggleAlignAction` (hold to disable course alignment), prefab
  `skillLevel=10` anomaly (clamped 0..1 for backfill AI at runtime).
- Trail volume note: Squirrel trail prisms ≈ 3.1 volume each (~⅕ nominal) —
  modes hosting Squirrel must author explicit volume thresholds (see CLAUDE.md
  ecosystem invariants §5.1 pointer).

## Serpent (7)

- **Input → actions**: 6 (Button1) → `ConsumeBoostAction`, 7 (Button2) →
  `ToggleStationaryModeAction` (Serpent mode), 1 (RightStick) →
  `CloakSeedWallAction`.
- **Boost magazine** (`ConsumeBoostActionSO`/Executor): up to 4 stacking boost
  pips, each lasting 3 s (authored; script default 4 s); multiplier is
  currently **hardcoded `4^stacks`** (SO's `boostMultiplier` ignored);
  auto-reload when empty.
- **Seed wall** (`SeedWallActionSO` → `SeedAssemblerActionExecutor`): takes the
  latest trail prism, super-shields it, attaches a `WallAssembler`
  (`AssemblerKind.Gyroid` currently also maps to WallAssembler), starts
  bonding; cost = `MaxAmount / 3` from resource slot 0.
- **Cloak** (`CloakSeedWallActionSO` — file lives in `UI/View/`): seeds a wall,
  bakes a "SerpentGhost" mesh, fades the hull + cloaks all trail prisms for a
  15 s cooldown window (authored; script default 20 s — blocks spawned during
  cloak stay hidden until it ends).
- **Stationary wall mode** (`ToggleTranslationModeActionSO.Mode.Serpent`):
  restricts translation, stops the trail spawner, and seeds + bonds a wall
  while parked; toggling off resumes the spawner.
- **HUD** (`SerpentVesselHUDController`/View): shield-count sprite (0-4
  quantized from the shield resource) + 4 boost pips with per-pip drain
  animation.
- **Animation**: `MantaAnimationContoller` (shared Animator controller);
  single-stick transformer. **Camera**: Fixed, offset (0,0,-250).

## Termite (8) — planned

- `CommandVesselTransformer` (RTS-style cursor movement via
  `InputStatus.ThreeDPosition`; sets `CommandStickControls` so touch feeds
  `SingleTouchValue`/`NodeTapAction`).
- Drone/boid stack: `BoidController` + legacy `DeployDronesAction`,
  `MoundDronesVesselAction` (spawn at cell crystal), `QueenDronesVesselAction`,
  `RecallDronesAction`. No HUD/telemetry/camera asset/prism pool.

## Falcon (9) / Shrike (10) — planned

- Legacy mono `BoostAction` + `FullAutoAction`, `ToggleGyroAction`, `Gun`,
  `GunTransformer` — a turret ring of gun children orbiting the ship axis,
  aimed by right-stick angle, focus distance scaling with stick deflection.
- Both carry a `SeedAssemblerConfigurator`; Shrike additionally carries a
  `WallAssembler`.
- No SO_Class assets, HUD, telemetry, camera settings, or prism pools. Prefabs
  reference two transformer scripts (base + single-stick) — verify in-editor
  which is active before building on them.

## Sparrow (11)

The shooter. AI vessel-pick fallback class.

- **Input → actions**: 1 (RightStick) → `ModeSwitchingFire`
  (`SparrowModeSwitchingFireSO`: routes to `FullAutoAction` normally /
  `FullAutoBlockShootAction` when stationary; live-swaps mid-hold on the
  `stationaryModeChanged` SOAP bool), 2 (LeftStick) → `SkyBurstGunAction`
  (`FireGunActionSO` missile), 7 (Button2) → `OverheatingAction`,
  6 (Button1) → `ToggleStationaryModeAction` (Sparrow mode).
- **Guns**: only playable vessel hosting a `Gun` + `ProjectileFactory` +
  `BlockProjectileFactory` (+ pool managers). Full-auto volleys from
  `muzzles[]` (ammo slot 0; authored `ammoCost=0` — free volleys today; script
  default 0.03); skyburst missile per press (`OnAmmoChanged` → HUD missile
  icons); stationary mode fires **prism blocks** (`PrismType.Sparrow`;
  authored blockScale (0.8, 0.5, 5), anchor at 90-120 units — script defaults
  (20, 2, 6) / 90-100) — conserved mass, not projectiles.
- **Signature — overheat** (`OverheatingActionSO` wrapping `BoostAction`):
  boost builds heat in resource slot 1; at max →
  `VesselPrismController.EnableDangerMode` — the trail becomes **dangerous to
  everyone** (domain-blind, locked design) for `overheatDuration=7 s`, hull
  prisms squash to (0.7, 1, 0.7), `OverheatTrailVisualBridge` flips the
  silhouette danger visual; then decay. Static
  `VesselPrismController.OnDangerBlockCreated` feeds telemetry.
- **Trail**: `SparrowPrismController` inverts the usual relationship — when
  NOT boosting, prisms are ×2 scale, ×3 gap, ×2 spawn delay (big sparse
  blocks); boosting lays the normal tight trail.
- **Turret mode** (`ToggleTranslationModeActionSO.Mode.Sparrow`): restrict
  translation + stop trail spawn (no wall seeding, unlike Serpent).
- **HUD** (`SparrowHUDController`/`SparrowHUDView`, in `UI/Controller|View/`):
  heat/boost fill (overheat color), missile ammo sprite ladder, weapon-mode
  icon, and blocked-input red pulse driven by
  `ScriptableEventInputEventBlock` (the debuff-mute visualization).
- **Telemetry** (`SparrowVesselTelemetry`, authored on prefab):
  PrismBlocksShot, SkyburstMissilesShot, DangerBlocksSpawned.
- **Misc**: `ClientNetworkTransform`, `AICinematicBehavior` (dormant),
  `TrailScaleModulator`, inert `ElementPipsView`, single-stick transformer,
  `MantaAnimationContoller`. AI ability loadout on prefab: SkyBurstGun 2 s/5 s,
  FullAuto 3 s/0.8 s, Overheating 2 s/10 s, ModeSwitchingFire 2 s/10 s.
- Counter-play note: Sparrow's overheat-boost input (`Button2Action`) is
  exactly what Rhino's danger-prism debuff mutes for 5 s.

---

## Cross-class collateral

- **Per-vessel prism pools** (`PrismType` in `Controller/Prisms/PrismFactory.cs`):
  Dolphin, Serpent, Sparrow, Manta, Squirrel, Rhino (+ Interactive, Explosion,
  Implosion, Grow). Urchin/Grizzly/Termite/Falcon/Shrike pools are missing.
- **Impactor containers** (`_SO_Assets/Effects/Effect Containers/VesselContainers/`):
  Manta, Dolphin, Rhino, Squirrel, Sparrow, Serpent only.
- **Ability-card SOs** (`_SO_Assets/Abilities/`, 24 `SO_VesselAbility`):
  marketing/loadout metadata for Dolphin/Manta/Rhino/Serpent/Sparrow/Squirrel —
  not referenced by prefabs.
- **Captains**: full 9-vessel × 4-element `SO_Captain` matrix authored
  (`_SO_Assets/Captains/Elemental/`) + arcade/freestyle sets; system dormant
  (see ARCHITECTURE §14.1).
- **AI profiles** (`MainAIProfileList.asset`): 13 name+avatar identities used
  for backfill AI naming and scoreboard avatars — not behavior tuning.
