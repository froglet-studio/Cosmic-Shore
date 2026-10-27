# Rhino kit, 2026-10-16: Afterimage, Live Wire, Momentum and the Surge

> **2026-10-26 (Garrett): the Rhino has NO ENERGY.** The meter, the energy banked per cut, the energize
> cost and the surge's fill and element buff are gone; **Charge** scales the sword's petal STEAL
> (×1 → ×2.5); **Space alone** sets the blade length (24 → 40) and the camera follows it; the omni-crystal
> Surge is an 8 s energized sword ring. Energy passages below are history - the current rules are
> `RHINO_ENERGY_SWORD.md` § "No energy (2026-10-26) — and Length: Space alone".

Garrett, 2026-10-16, closing the Rhino's open design slots:

- *"after leaving the energized state your sword leaves a trail of prisms that appear like after
  images of the sword for 5 seconds. after upgrade these become danger prisms."* (Mass)
- *"time level 5 should be danger prism immunity when traveling over 70% max speed. the icon should
  indicate this."* and *"[the hull costs nothing on a ram] should be in the time description"* (Time)
- *"remake the omni crystal ability so it energizes the rhino while also giving it temporary
  elements and the mass ability that typically waits till after the energize is gone."*
- Charge as the sword's energy economy with **Live Wire** as its level-5, and Space named for the
  blade length it already scaled - both approved from the deep-dive recommendations.

This doc is the record for all four rows and the surge. The sword itself is
`RHINO_ENERGY_SWORD.md`; the pose and the triggers are `RHINO_SHIELD_SWIPE.md`; the ramp is
`RHINO_RAMP_BOOST.md`; the hull is `RHINO_PLOUGH.md`.

## 1. The map (live, `Assets/Resources/ElementalAbilityMaps/Rhino.asset`)

`python3 Tools/Build/element_ability_table.py Rhino` - **4/4 abilities, 4/4 scaling, 4/4 level-5
gates, 0 disagreements.** Charge, Mass and Space are `Input: 13` (`BothSticksAction` - hold both
triggers to energize; the swing; the afterimages follow the energize; 2026-10-17, so they draw the
two-trigger chip). Time stays `Input: 0`, the passive sentinel (the ramp gesture).

| Element | Ability | What the element scales | Level 5 |
|---|---|---|---|
| Charge | **Energize** (was Sword Steal, and Sword Energy before that, until 2026-10-26) | petals a sword hit takes from a rival, x1 at rest -> x2.5 at level 10, floored x1 (`RhinoSwordStealBySkimmerEffect.stealScale`) | **Sword Steal** (was Live Wire until 2026-10-26) - the hit's petals land on the Rhino instead of being ejected as crystals (`VesselOvertakeBySkimmerEffectSO.stealUpgradeElement`) |
| Mass | **Afterimage** | afterimage VOLUME, x1 -> x2.5, floored x0.25, read at the REPLICATED level (`RhinoSwordAfterimageConfigSO.massVolumeMultiplier`) | **Danger Afterimages** - laid as danger prisms (`RhinoSwordAfterimageConfigSO.DangerActive`) |
| Space | **Blade Reach** | the blade's length, 24 -> 40 (`Skimmer.Scale` on the Rhino's ForceFieldSkimmer instance) - Space ALONE since 2026-10-26, with the camera following it | **Shield Breaker** (was Breaker until 2026-10-26) - the sword can pop super-shielded prisms at all, with an energized blade (`ShieldSkimmerScaleConfigSO.superShieldUpgradeElement`) |
| Time | **Ramp Spool** | the ramp's wind-up, 220 u/s per second x1 -> x2.5, floored x0.5 | **Momentum** - above 70% of top speed (588 of 840 u/s) danger prisms cannot drain the Rhino's elements |

Every gate goes through `IsUpgradeActive` (the replicated unlock bits), never a raw level read.

## 2. Charge - Sword Energy and Live Wire

Energy is the Rhino's Shield resource: it lengthens the blade (30 -> 120 with Space's base under
it) and pays for the energize (0.1 a time). Each prism the blade destroys banks 0.04, each
super-shield it pops 0.12, both multiplied by Charge - a charged Rhino refills the meter in half the
cuts.

**The tail rule changed.** Before, any frame in the chop stance during the energized tail reset the
tail, so a short chop every few seconds held any blade energized forever - with swipe recovery at
zero - for one 0.1 payment. Now:

- held in the stance from ignition: stays lit (unchanged);
- once let go: the 5 s tail runs out whatever the fingers do, then the 5 s cooldown;
- **Live Wire (Charge 5)**: every return to the stance re-lights it, so a pilot who keeps coming
  back to the chop keeps the blade lit.

## 3. Mass - Afterimage

`RhinoSwordAfterimage` (on `ScaleSkimmerObject`, beside the sword driver) listens to the driver's
`EnergizePhaseChanged`. On the way OUT of `Energized` it opens a **5 s window**; while a window is
open it lays an afterimage whenever the blade tip has moved at least 6 u and 0.3 s has passed, up to
16 per window. An afterimage is the blade itself, tiled: the hilt-to-tip line, 90% of it, cut into at
most 6 segments of at most 10 u, each `2 x 0.8 x segment` scaled by the cube root of the Mass
multiplier (volume - the conserved quantity - scales linearly with the level, the
`VesselPrismController.trailVolume` rule), oriented along the blade with its thin axis through the
blade's flat.

Each prism is laid through the vessel's own spawn channel and prism type, owned by the pilot
(`ownerID`, so the fleet's 1 s self-trail grace keeps the Rhino's hull off its fresh mass), with a
0.1 s collider delay, then stamped into the window's own **`RhinoAfterimageTrail`** after
`Prism.Initialize`.

**The blade cuts its own afterimages (changed 2026-10-17).** Garrett: *"the mass after image prisms
should be danger prisms and not immune to our destruction"* - they stay plain until Mass 5 (that
split is unchanged), and the Rhino's own blade now cuts them like any prism once the fleet's 1 s
self-trail grace is past, banking energy for them (0.04 x Charge each). Until 2026-10-17
`RhinoSkimmerDamagePrismEffectSO` skipped them and the blade never cut its own. The identity test
survives for the AI: `RhinoSwordAfterimage.IsMine` (`prism.Trail is RhinoAfterimageTrail t &&
t.Owner == this`, surfaced as `IRhinoSwordState.IsOwnAfterimage`) lets `RhinoSwordAutopilot` aim at
something else first. A recycled prism cannot pass it: pool reuse clears membership and its new
layer stamps its own. The hull still ploughs them (`RHINO_PLOUGH.md`, unchanged).

**Watch item, not a bug - the self-farm loop.** One energize (cost 0.1) followed by cutting its own
afterimages (up to 16 afterimages x up to 6 segments x 0.04) can refill the meter. Playtest whether
that loop is too strong; if it is, the lever is energy-per-own-afterimage, not immunity.

**Danger Afterimages (Mass 5)** sets `prismProperties.IsDangerous` before `Initialize` - the same
flag `VesselPrismController`'s danger mode uses, so the real danger pipeline makes them.

**Every peer lays its own**, like the trail: the window opens on the replicated energize phase, the
blade's pose comes from the replicated trigger mirrors, and size and danger read replicated state.

## 4. Time - Momentum, and the bulldozer

`VesselElementalImmunity` gained a window, `WhileAboveTopSpeedFraction`, measured against an
`ITopSpeedSource` on the vessel. The Rhino's is the ramp: `RampBoostActionExecutor.TopSpeed` =
authored cruise (`DefaultMinimumSpeed + DefaultThrottleScaler` = 50, not the runtime mirrors, which
read 0 on a replica) x `maxBoostMultiplier` 16.8 = 840. The component on the Rhino root holds
`condition 4, upgradeGate Time, wardedSources DangerPrism, topSpeedFraction 0.7`.

Scoped to **DangerPrism alone**, for the Dolphin Drift Ward's reason (vessel skill rule 31): an
opposing pilot's blast still lands, so Momentum is a ward against the arena, not against being
scored on.

The Time row's description now says what the hull does on a ram: **nothing to itself.** The Rhino
carries no prism slow, so ramming mass costs it no speed - the plough shoves what it hits aside and
the ramp keeps climbing. That was recorded as a gap; it is the design.

## 5. The omni crystal - the Surge

`RhinoVesselSurgeByCrystalEffect` replaces `RhinoVesselChangeResourceByCrystalEffect` in the
Rhino's vessel container (the explosion beside it is unchanged). On an omni pickup:

*(Reworked 2026-10-26 - the 2026-10-16 surge also filled the energy, burst the blade and lent every
element two levels; all three are gone.)* Today:

1. the blade is held **energized for 8 s** (`surgeSeconds`) with no stance, overriding a cooldown;
2. a **ring of 8 trigger-driven swords** surrounds the hull and the pilot's camera pulls back onto it
   (`RHINO_ENERGY_SWORD.md` § "The omni-crystal Surge");
3. **afterimages lay throughout** - the Mass ability that otherwise waits for the energize to end.

When the surge ends the blade goes out (unless Live Wire and the stance hold it), and the ordinary
5 s afterimage window follows with a fresh budget.

The **per-petal burst is gone**: `RhinoSwordCrystalBurstEffectSO` ran on every elemental crystal the
sword touched and drained the whole meter each time, so collecting the levels the Rhino's elements
are made of emptied the meter its blade is made of. The sword's crystal slot is empty; collection is
the crystal's side and is unaffected.

**Where it runs.** Omni collection resolves on the server, which runs the effect, and replays it on
a remote pilot's own machine (`OmniCrystalImpactor.ReplayOnRemoteOwner`). `Surge` is a no-op off
the owner; the owner's surge replicates through the relay. The elemental half lands on the server
and the owner, like every crystal's.

## 6. Replication (the sword was per-peer)

- **Pose.** Every peer poses the blade from `InputStatus`'s owner-write trigger mirrors; the local
  gamepad pilot reads the hardware. Before, a remote peer posed it from replayed press/release
  EVENTS - a full single-trigger swing at the deadzone - and every peer cuts against its own blade,
  so a half-pull sweep that missed for the pilot cut on everyone else's screen. Events still drive
  a device that writes no mirrors (touch).
- **State.** `RhinoSwordNetworkRelay` (NetworkBehaviour, Rhino root) carries energy (quantized
  1/1024), the energize phase and the surge bit, owner-write. The driver decides on the owner
  (`IsAuthority`) and mirrors elsewhere, routing the phase through `SetEnergizePhase` so the
  ignition re-dispatch, the FX and the afterimage window fire on every peer.
- Off the network (`!IsSpawned`) the driver is its own authority, exactly as before.

## 7. AI

- **Ramp** - `RampBoostAIPolicySO` / `RhinoAIBoostPolicy.asset` on the Rhino's `AIPilot`. Only a
  human input strategy raises `FullSpeedStraightAction`, so the AI never ramped. The driver ramps on a
  straight whose leg pays the wind-up back, brakes ON the ramp to a corner speed, and `End()`s it at
  a sharp corner (a graded ramp's own release is the top of its slope, not an end); it re-arms a ramp
  the grading let fall off. `Controller/AI/AI_BOOST.md`, Ramp.
- **Sword** - `RhinoSwordAutopilot` (Rhino root) writes the trigger MIRRORS on the machine that owns
  the AI's input, so the shipped sword code poses, energizes and replicates it like a human's. Every
  0.15 s at rest it picks: hold the energize stance when the meter can pay and hardened mass is near
  (or the meter is >= 75% and anything is in reach), else one 0.3 s stroke at the nearest hostile
  prism in reach (side from its lateral offset), then 0.45 s rest. It never steers, and lets go of
  both triggers the frame autopilot stops. The swipe executor follows its triggers through an
  autopilot (`IgnoresTriggers` honours `DrivesTriggers`).
- The AI's ability cycler list is empty: its only entry was GrowTrail at Duration 0 / Cooldown 0.

## 8. HUD

The row binds 4/4 with PLACEHOLDER art (`author_hull_icon_placeholders.py`, `author_hull_ability_rows.py`):

| Card | Lit | Also |
|---|---|---|
| Charge | while the blade is energized - re-asserted every frame, so letting go of the stance no longer darkens it during the 5 s tail | the recharge veil sweeps over the energize cooldown; the authored `ChargeGauge` fills with sword energy 0..1 |
| Mass | while afterimages are laying | |
| Space | (passive) | upgrade badge for Breaker |
| Time | while the **ramp is boosting** (Garrett: *"the time icon should respond to boosting"*) | flashes ONCE when Momentum takes hold (Time 5, above 70% of top speed = 588 u/s); the authored `TimeGauge` fills with speed / top speed (`ITopSpeedSource`) |

Both gauges are authored into `RhinoHUDVariant.prefab` by `Tools/Build/author_hull_ability_rows.py`
(`RHINO_GAUGES`, 2026-10-17). Before that day the Time card lit only while Momentum held and no
gauge was authored.

**Control chips (2026-10-17).** Charge, Mass and Space draw the two-trigger chip from their
`Input: 13` (`HintBinding.PadBothTriggers = 13` / `KeyBothShifts = 28`; a PLACEHOLDER L1+R1
composite, `Glyph_BothTriggers-PLACEHOLDER.png`, from `Tools/Build/author_two_trigger_glyph.py`;
keyboard "L+R SHIFT"). Time is the passive sentinel, so `RhinoHUDController` sets its
sticks-apart chip explicitly (`VesselHUDView.SetAbilityControlGlyph` ->
`AbilityLockupView.SetAbilityControlBinding`, `PadSticksOutward = 14` / `KeySticksOutward = 29`,
`Thumbsticks_Outward.png`, keyboard "A + '"). The press path lights only the FIRST map entry bound
to an input, so the both-triggers press lights Charge.

`RhinoHUDController` polls these with change detection, so no subscription can leak across a
re-initialise. The pre-lockup status cluster that came back when the Rhino bound its first icon is
deleted (`ABILITY_LOCKUP.md`).

## Files

| File | Role |
|---|---|
| `Executors/RhinoSwordAfterimage.cs` (+ `RhinoAfterimageTrail`) | Mass: lays the afterimages |
| `Executors/RhinoSwordAfterimageConfigSO.cs`, `_SO_Assets/VesselActions/Rhino/RhinoSwordAfterimageConfig.asset` | Mass tuning, Mass scaling, Mass-5 gate |
| `Executors/ShieldSkimmerScaleDriver.cs` | energy / energize / tail / Surge / authority + mirror / `EnergizePhaseChanged` |
| `Executors/ShieldSkimmerScaleConfigSO.cs`, `ShieldSkimmerScaleConfig.asset` | energize and surge tuning (`liveWireUpgradeElement` / `LiveWireActive` DELETED 2026-10-26) |
| `Executors/IRhinoSwordState.cs` | `IsSurging`, `Surge`, `IsOwnAfterimage` (TriggerCrystalBurst removed) |
| `Vessel/RhinoSwordNetworkRelay.cs` | owner-authoritative energy / phase / surge |
| `Executors/ShieldSwipeActionExecutor.cs` | pose from the replicated mirrors on every peer |
| `Skimmer Prism Effects/RhinoSkimmerDamagePrismEffectSO.cs`, `RhinoSkimmerDamagePrismEffect.asset` | Charge energy scaling; cuts own afterimages past the 1 s grace (since 2026-10-17) |
| `Vessel Crystal Effects/RhinoVesselSurgeByCrystalEffectSO.cs`, `RhinoVesselSurgeByCrystalEffect.asset` | the Surge |
| `Vessel/VesselElementalImmunity.cs`, `Vessel/ITopSpeedSource.cs`, `Executors/RampBoostActionExecutor.cs` | Momentum |
| `AI/Rhino/RhinoSwordAutopilot.cs`, `AI/Boost/RampBoostAIPolicySO.cs`, `AI Boost Policies/RhinoAIBoostPolicy.asset` | AI |
| `UI/Controller/RhinoHUDController.cs` | the card states, both gauges, the Time chip |
| `Tests/Editor/RhinoSwordAfterimageTests.cs` | the segment tiling |

## Tuning knobs (first pass - never played)

| Knob | Where | Value |
|---|---|---|
| Afterimage window | `RhinoSwordAfterimageConfig.windowSeconds` | 5 s |
| Cadence | `layIntervalSeconds` / `minTipTravel` | 0.3 s / 6 u |
| Budget | `maxAfterimagesPerWindow` | 16 |
| Shape | `segmentLength` / `maxSegments` / `lengthCoverage` / `width` / `thickness` | 10 / 6 / 0.9 / 2 / 0.8 |
| Collider delay | `colliderDelaySeconds` | 0.1 s |
| Mass volume | `massVolumeMultiplier` | x1 -> x2.5, floor x0.25 |
| Charge steal | `RhinoSwordStealBySkimmerEffect.stealScale` | x1 -> x2.5, floor x1 |
| Momentum | `VesselElementalImmunity.topSpeedFraction` (Rhino root) | 0.7 |
| Surge | `RhinoVesselSurgeByCrystalEffect` `surgeSeconds` | 8 (no fill, no element buff since 2026-10-26) |
| AI sword | `RhinoSwordAutopilot` think / stroke / rest / energize crowd / combo chance / finisher chance / tap / gap / combo rest | 0.15 / 0.3 / 0.45 / 6 / 0.35 / 0.4 / 0.08 / 0.1 / 0.9 |
| AI ramp | `RhinoAIBoostPolicy` `cornerSpeed` / `minCornerSpeed` / `minSpeedGain` / `cornerReleaseDegrees` / `minRearmSeconds` | 600 / 160 / 50 / 30° / 0.5 s (align / runway: fleet defaults) |

Budget: at most 16 x 6 = 96 prisms per window, each <= 2 x 0.8 x 10 = 16 volume at rest (x2.5 at
Mass 10). Measure it against Cleave's and Astro League's phase thresholds before tuning up.

## In-editor verification

Nothing here has run in the Unity editor (no editor in the authoring session). Compiled with
`Tools/Build/unity_refcompile` (player + editor, 0 errors); `peer_press_harness` 0 divergences;
`element_ability_table.py` 0 disagreements. Steps are in `Docs/UNITY_VERIFICATION_CHECKLIST.md`
(the 2026-10-16 Rhino entry) and `Docs/QA/QA_BACKLOG.md`.

1. **Afterimage** - any Rhino scene: energize (hold both triggers 1 s), swing, release. For 5 s after
   the blade goes out, blade-shaped prisms appear along its sweep; the Mass card is lit. For the
   first 1 s the blade passes through them; after that it cuts them and banks energy. At Mass 5
   they are danger prisms.
2. **Live Wire** - energize, release, re-enter the stance 2 s later: the blade goes out on time.
   With Charge 5, the same re-entry re-lights it.
3. **Momentum** - Time 5, ramp past ~588 u/s, ram a danger prism: no element drain, and the Time
   card (already lit by the boost) flashes once as Momentum takes hold. Below 588 the drain lands.
4. **Surge** - collect an omni crystal: blade bursts, energized for 6 s with no stance, petals rise
   two levels and decay, afterimages throughout, then the 5 s window.
5. **Petals** - collect elemental crystals with the sword: the energy meter no longer empties.
6. **MPPM two clients** - half-pull a swing past a prism: both clients cut (or both miss) it.
   Energize: both see the energized blade and the afterimages. Ramp then steer off: the remote
   copy's `IsBoosting` clears.
7. **AI** - an AI Rhino ramps on straights, swings at prisms near it, energizes near super-shields.

## Follow-ups

- **Mass budget playtest** - afterimage volume against Cleave / Astro League phase thresholds.
- ~~**Danger Afterimages x the input mute**~~ - RESOLVED 2026-10-17: `SparrowDebuffByRhinoDangerPrismEffect`
  (Sparrow and Grizzly containers, Button2 muted 5 s on ANY danger prism) is deleted - control theft
  is not an economy form (`Docs/ELEMENTAL_ECONOMY.md` §9). The Grizzly, which held only the mute,
  now carries the fleet burn `VesselElementalDebuffByDangerPrismEffect`.
- **Self-farm playtest** - one energize then self-cuts can refill the meter (§3); the lever is
  energy-per-own-afterimage.
- **Combos** - the AI never combos; a staggered energize stance can fire a combo
  (`RhinoSwordComboDetector` links a rolled press - `RhinoSwordComboTests.ARolledTap_StillLinks`).
- **Icons** are PLACEHOLDERS (`*-PLACEHOLDER.png`).
- **Engage SFX** for the surge / afterimages: no new FMOD slots were added; the existing burst FX
  reads for the surge.
