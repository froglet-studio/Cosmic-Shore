# Swordfish — all sword (split off the Rhino, 2026-11-07)

Garrett, 2026-11-07: *"split the rhino into two vessels: the rhino and a swordfish. the sword fish will be all about
the sword. the rhino will be all about the plow."*

The **Swordfish** (`VesselClassType.Swordfish = 30`) is the Rhino's sword kit on the Rhino's airframe. The **Rhino**
(3) keeps the plough and the ramp, and gains a plough kit of its own (`RHINO_PLOUGH.md` § "The plough kit").

| | Swordfish (30) | Rhino (3) |
|---|---|---|
| Triggers | Swing the blade (`ShieldSwipeActionExecutor`); both held energize it | Steer the plough: a pull is a small shove that way, a hold throws every ploughed prism to that side (`RhinoPloughSteerExecutor`) |
| Charge | **Energize** / L5 **Sword Steal** | **Carry** / L5 **Avalanche** |
| Mass | **Afterimage** / L5 **Danger Afterimages** | **Blade** (a plough blade across the nose) / L5 **Berm** |
| Space | **Blade Reach** / L5 **Shield Breaker** | **Furrow** / L5 **Unstoppable** |
| Time | **Quickdraw** / L5 **Flurry** (since 2026-11-12; § "Fundamentals pass") | **Ramp Spool** / L5 **Momentum** |
| Hull on a prism | **Rams** — destroys it (`VesselDamagePrismEffect`, the fleet ram) | **Ploughs** it aside (`RhinoPloughPrismEffect`) |
| Omni crystal | **Surge** — absorbed into the hull, the 8 s ring of swords (no blast since 2026-11-12) | **Stampede** (#1927) |
| Modes | Cleave (only), Astro League (in the Rhino's place), Broadside (in the Rhino's place), Brood Rush | Headlong, Regatta, Brood Rush |

Everything the sword was is unchanged and still documented where it was: `RHINO_ENERGY_SWORD.md` (the blade, the
energize, the two keys), `RHINO_SHIELD_SWIPE.md` (the pose rig), `RHINO_SWORD_AFTERIMAGE.md`, `RHINO_SWORD_COMBOS.md`,
`RHINO_RAMP_BOOST.md`. Read "the Rhino" there as "the Swordfish" for anything about the sword. The code keeps its
`RhinoSword*` names: the classes are the sword's, and renaming them is a separate change.

## Fundamentals pass (2026-11-12)

Garrett, 2026-11-12: "lets also get the sword fish polished out as well", the way the Rhino was reworked (#1927): each
element owns its fleet identity (Space = reach, Time = rate, Charge = threat and PvP petals, Mass = physical size),
Charge has PvP, each vessel morphs the omni crystal into its own effect, and nothing is shared with the Rhino.

| Element | Before | Now | Fundamental |
|---|---|---|---|
| Charge | Energize / Sword Steal | unchanged | PvP: a sword hit takes x1 -> x2.5 petals; L5 pockets them |
| Mass | Afterimage / Danger Afterimages | unchanged | size: the afterimages' volume x1 -> x2.5 |
| Space | Blade Reach / Shield Breaker | unchanged | reach: the blade 24 -> 40 |
| Time | Ramp Spool / Momentum, **shared with the Rhino** | **Quickdraw / Flurry** | rate: the sword's rate x1 -> x2.5 |
| Omni | Surge **plus the Rhino's old crystal blast** (200-500 AOE) and the sphere-blast morph | **Surge** alone, the crystal **absorbed** into the hull | its own effect |

- **Quickdraw** divides the energize hold (1 -> 0.4 s), the energize cooldown (5 -> 2 s) and the swipe recovery
  (0.35 -> 0.14 s) by Time's rate, x1 at rest to x2.5 at level 10, floored x0.5
  (`ShieldSkimmerScaleConfigSO.timeSwordRateMultiplier`, `SwordfishShieldSwipeConfigSO.timeSwipeRateMultiplier`, read
  live; the energize is decided on the owner and replicated, so only the owner's read matters for it). The let-go tail
  is a duration and stays 5 s. The cooldown's length is taken when it starts, so the HUD veil sweeps what the pilot got.
- **Flurry** (Time L5) takes the swipe recovery away for good: the sword swings as freely as an energized blade, lit or
  not (`SwordfishShieldSwipeConfigSO.flurryUpgradeElement`, `IsUpgradeActive(Time)` in
  `ShieldSwipeActionExecutor.ApplySwipeRecovery`).
- **The ramp stays** (it is how the Swordfish flies, 840 u/s top speed), on its own asset
  `SwordfishRampBoostAction.asset` with the Time scaling switched off; `RhinoRampBoostAction.asset` is the Rhino's
  alone. The Momentum ward (`VesselElementalImmunity`) is gone from `Swordfish.prefab`; it stays on the Rhino.
- **The Surge** is the one omni effect: `SwordfishImpactorDataContainer` dropped `RhinoVesselExplosionByCrystalEffect`
  (the slow blast the old Rhino had, with Charge and Space scaling authored inert) and retires the crystal through
  `VesselAbsorbCrystalMorphByCrystal`, so the crystal flows into the hull and the ring of swords comes out of it.
- Tests: `SwordfishQuickdrawTests`. The HUD's Time card and the icons follow in the icon pass.
- **Its own HUD and icons (2026-11-12, icon pass)**: `SwordfishHUDVariant`, `SwordfishVesselHUDView`,
  `SwordfishHUDController` (the Rhino's HUD is the Rhino's alone now). Charge: the bill follows the real sword and lights
  while energized, the plate the energize. Mass: the afterimages. Space: the bill's live reach, the prism shattered under
  Shield Breaker. Time: Quickdraw, the plate the swing's recovery, the swoosh by Time's rate, a flash when Flurry unlocks.
  Drawn by `Tools/Build/icon_kit/specs/swordfish.py`; tests `SwordfishHUDTests`.

## A swordfish, and the sword is its bill (2026-11-08)

Garrett, 2026-11-08: *"the swordfish should look like a swordfish (and a space ship of course) ... its sword should be
the sword of the sword fish, so in the home position the camera should have no y-offset or x-offset, and the model
should look the most sword fish like. but when a trigger is pulled the sword should go to the side and the camera's x
offset should center the sword. when both triggers are pulled the sword should be vertical and the camera should only
have a y offset to again keep the sword centered. preserve what you can from ... the rhino's sword: moving the fins
with the triggers, the many swords from the omnicrystal, the combos, charging the sword when both triggers are pulled,
etc. mobile will need to do what the manta does and allow gyro controls while both thumbs are lifted to raise the
sword."*

| | Before | Now |
|---|---|---|
| Hull | the Rhino's rig | `Swordfish.fbx` (`specs/swordfish.py`, `Docs/VesselModels/Swordfish.md`): a swordfish ship on the Rhino rig's names and bones, so `RhinoAnimation` still drives it |
| The sword's look | Unity's capsule | `SwordfishBill.asset` (`Tools/Build/author_swordfish_bill.py`): a flat, tapering bill in the capsule's own space. The CapsuleCollider that cuts is unchanged |
| Rest pose | raised 20 degrees above the nose, mount 13 units ahead of the hull | straight ahead and level, its hilt in the flat snout (mount `(0, 0.3, 7.7)` under `Rhino_Test (1)`, rotation `(0.5, 0.5, 0.5, 0.5)`): the bill |
| One trigger | sweeps the raised blade (yaw + roll 90) | swings the bill out flat to that side (yaw 90, roll 0), about its root on the nose |
| Both triggers | a centred chop down (65) | stands the bill straight up (90). The raise is the shared part of the two pulls, `min(LT, RT)`, so one trigger never lifts it |
| Camera | looks 18 units above the hull (`lookAheadLift`) | no offset at rest (`SwordfishCameraSettingsSO`, lift 0); pans by the sword's midpoint, x for the side and y for up (`SwordfishSwordCamera` -> `CustomCameraController.FramingPan`) |
| Phone, both thumbs off | the sword energizes | it also stands up, and tilt steers as on the Manta (`tiltSteersWhileIdle`, `flipTiltSteerPitch`) |

The mapping is `SwordfishShieldSwipeConfigSO.poseMapping = Bill` (`SwordPoseMapping`, with `billSideYawDegrees`,
`billSideRollDegrees`, `billRaiseDegrees`, `billCameraCentring`, `billCameraPanSeconds`); `Chop` is the old swordsman,
kept for any hull that wants it. `ShieldSwipeActionExecutor.BillChannels` is the pure mapping, tested by
`SwordfishBillPoseTests` with the camera pan.

**Kept:** the fins swing with the sword (`RhinoAnimation`'s sword layer reads the same pose channels: the
pectoral plates turn with the bill's yaw and bow with its raise); both triggers held is still the energize stance (it
now also stands the bill up); the omni-crystal Surge's ring of swords (it clones the blade, so the ring is now of
bills); the blade tracers (re-seated on the blade every frame); the combos (they play as before, relative to the
new rest, pointing ahead, so a flourish authored for the raised blade now starts from the bill; see the checklist);
the swipe recovery and the AI's strokes (a stroke now lays the bill out to the side).

**Not changed:** the blade's length (Space, 24 -> 40 scale, so a 48-80 unit bill: longer than a real swordfish's
third of its length, because it is the sword's reach), its collider, its damage, its energy rules.

## How it was made

`Tools/Build/author_swordfish_assets.py` (`--check`, `--self-test`). It cloned `Rhino.prefab` with the sword (a fresh
Netcode `GlobalObjectIdHash`, `vesselType: 30`, its own vessel impactor container), and then stripped the sword from the
Rhino in a document-level pass gated by `dangling_refs` (vessel skill rule 46). Both passes are spent one-shots: they run
only while the Swordfish prefab is absent and the Rhino still carries the swing executor, so `--check` validates the
shipped prefabs and never diffs against a donor.

The Swordfish's hull ram is the one deliberate kit change from the Rhino: the plough is the Rhino's, so the Swordfish's
container (`SwordfishImpactorDataContainer`) carries the fleet ram where the Rhino's carries the plough. The Swordfish
still carries no prism slow, so ramming costs it no speed. In Cleave a hull ram scores again (it destroys), as it did
before the plough (2026-10-13).

## Files

| File | Role |
|---|---|
| `Assets/_Prefabs/Spacevessels/Swordfish.prefab` | The hull: the Rhino's prefab with the sword |
| `Assets/_SO_Assets/Classes/SO_Class_Swordfish.asset` | Class 30, in `SO_Classlist_All` / `_Classes` |
| `Assets/Resources/ElementalAbilityMaps/Swordfish.asset` | The four sword cards and the Surge |
| `Assets/_SO_Assets/Effects/Effect Containers/VesselContainers/SwordfishImpactorDataContainer.asset` | Ram (not plough), danger debuff, the Surge, the crystal explosion |
| `Assets/Resources/CrystalHullFusionConfig.asset` | Four class-30 entries reusing the Rhino's bakes (it draws the Rhino's mesh) |
| `Assets/_SO_Assets/Vessel Prefab Container.asset`, `Assets/DefaultNetworkPrefabs.asset` | Spawnable, networked |
| `Assets/_Scripts/Controller/Toys/ToyVesselRoster.cs` | In the Toybox's Vessel Changer |
| `Assets/Resources/TouchControlProfiles.asset` (`author_touch_profiles.py`) | The Rhino's old phone row (lifts swing, both off energizes) |
| `Assets/_Prefabs/CORE/NetworkManager.prefab` | ProtocolVersion 25 → 26 (a new network prefab) |

Shared with the Rhino, not copied: the sword's effects (the HUD and the ramp are its own since 2026-11-12). Its own since 2026-11-08: the model (`Swordfish.fbx`), the camera (`SwordfishCameraSettingsSO`), the blade
mesh (`SwordfishBill.asset`); the sword's configs are only the Swordfish's now (the Rhino has no sword).

## In-editor verification

No `unity` binary was available in the authoring session, so none of this has been seen in play. The reference compile
(`Tools/Build/unity_refcompile`, editor config) found 0 errors in the files changed here.

1. **Toybox → Vessel Changer.** The Swordfish is offered and flies exactly as the Rhino did before the split: triggers
   swing the blade, both held energize it, afterimages, the ramp, the Surge on an omni crystal.
2. **Its hull rams.** Fly it into a trail with the blade out of the way: the prisms the hull touches are destroyed
   (the Rhino ploughs them aside).
3. **Cleave** seats the Swordfish (player and four AI); the AI still cuts and the score ticks.
4. **Astro League** seats the Swordfish in the Rhino's place; a blade strike still moves the ball.
5. **Broadside**: the Swordfish's sword hits score as the Rhino's did. **Brood Rush** offers both hulls.
6. **MPPM two clients**: a Swordfish spawns on both (the new network prefab, protocol 26).
7. **Audits**: Audit Vessel Ability Rows, Skimmers, Elemental Morphs and Construction over both hulls.

## Follow-ups

- **The swordfish model is built (2026-11-08)**; the class card, the hangar art (`author_hangar_card_art.py`), the
  ability icons (`/ability-icon`) and its own crystal hull fusion bakes (its class-30 entries still name the Rhino's
  bakes, which now read stale and are solved at runtime) follow.
- **Regatta** still seats only the Rhino: its tip says "only an energised Rhino sword opens a hole", which is now the
  Swordfish's. Regatta's balance model is being re-searched on `claude/regatta-overnight-balance` (Garrett), so the
  Swordfish joins the roster there rather than in this branch.
- **Rename the `RhinoSword*` classes** to the Swordfish's when someone next works the sword (no asset names them by type).
- **The Vessel Studio** has no Swordfish page; its Rhino page is the sword.
