# Jewel Wasp (vessel class 31): the possession vessel

Garrett, 2026-11-14: "Make a new vessel ... It's signature ability should be to possess fauna and be able to fly around
and Consume prisms or attack vessels as that fauna." The concept doc (https://claude.ai/artifact/Tkc8stg6gCVa6HM9974qmB)
was signed off the same day with his changes, which this doc records as built. A jewel wasp stings a cockroach's brain
and walks it home by its antenna; this one stings a creature's heart crystal and flies it.

Code: `Executors/JewelWaspExecutor.cs` (every decision), `Data Containers/JewelWaspConfigSO.cs` (every number),
`Data Containers/JewelWaspActionSO.cs` (the three controls), `UI/Controller/JewelWaspHUDController.cs`,
`ImpactEffects/.../JewelWaspHatchByCrystalEffectSO.cs`, `Executors/PossessionRecorder.cs`. The fauna side:
`Environment/FloraAndFauna/IFaunaPossession.cs`, `Fauna.Possession.cs`, `FaunaAttackDriver.cs`, `LifeformHighlight.cs`.
Assets: `Tools/Build/author_jewel_wasp_assets.py` (`--check`, `--self-test`).

## 1. The loop

1. **Free**, the wasp flies like any hull. **RT held** aims at a creature: the one nearest the aim line within a 14 degree
   cone lights in the pilot's hue (`LifeformHighlight`, the leash mark's look; a world fact shown in the world). **LT held**
   aims at a plant the same way.
2. **Let go** and the wasp stings it. The range is the whole cell (its diameter; 3000 u in no cell). The hull flies to the
   heart at 1400 u/s, tucks in beside the crystal at its nested scale (0.22 against 0.45 free) and stings for 0.35 s. An
   onlooker sees a creature; an observant one sees the wasp nested by its heart (and a faint tint, lean 0.12).
3. **In a creature** the sticks fly it, any creature, any diet ("any host should always be available"):
   - RT at a rival vessel ahead plays the creature's attack and lands a Strike. At prisms, RT is suction.
   - LT is a burst of speed.
   - A leaves.
   - A worm segment takes the whole worm by its head. A swarm member takes the whole murmuration.
4. **In a plant** the camera stands far back (650 u), the sticks orbit it round the plant, and the plant grows. Either
   trigger aims and stings the next host from there, so a plant is the seat for picking one. A leaves.

## 2. The kit (one parameter per element)

| Element | Ability (input) | Scales | Level 5 |
|---|---|---|---|
| Charge | Leave / Attack (A; the attack is RT at a vessel) | the attack's recharge, 3 s to 0.8 s | **Burst**: leaving a creature bursts it, a MissileBlast-class hit on every rival vessel within 60 u, and the host dies jousted |
| Mass | Root (LT at a plant) | how fast the plant grows, x2 to x6 (`Flora.Hasten`) | **Overgrowth**: every plant within 200 u grows as fast |
| Space | Sting / Suction (RT) | the suction's reach, x1 to x3 the creature's own | **Maw**: suction pulls from every side, not only ahead |
| Time | Swim / Burst (LT in a creature) | a host's speed, x1.5 to x3 its own top speed | **Restless**: no wait between stings, none between bursts |

The Time card's icon swaps to the burst while the wasp rides a creature (`JewelWaspHUDController.timeInHostIcon`, empty
until the icon round). The Charge card is A on a pad and, on a phone, the on-screen A: the hull is a TotemButtons
profile, so its four cards are its buttons (`author_touch_profiles.py`).

**The omni crystal** hatches a random PREDATOR where it is collected (a Shark or a Worm Colony of any element,
`JewelWaspConfigSO.omniPredators`, picked by a hash of a serial) and the wasp possesses it at once. The crystal folds into
the newborn's heart (`FaunaBirthCrystalMorphByCrystal` retirement).

## 3. What possession is

`IFaunaPossession : IFaunaHerd : IFaunaLeash`. It is the Angler's leash with three changes, signed off by Garrett:

- **Any diet** can be taken, from any holder. A Jacks herd or an Angler's school member is stolen, and the old holder is
  told (`Released`).
- **Every shield is food** for a possessed creature. A super-shield is dropped (`DeactivateShields`) and eaten
  devastating in the same bite.
- **The rider's triggers reach the mouth and the attack.** `PossessedSuction` eats up to 8 of the nearest rival prisms
  within reach, nearest first, through the ordinary consume: the suction implosion fills the stomach, so mass is
  conserved. The rider's own team's mass, creatures' bodies and the race corridor are never food.

**The host keeps its starvation clock** (`Sustains` false): a wasp that never feeds its host loses it.

Its own steering and deciding stand down while it is possessed:
- `LightFauna`, `Boid` and the worm head steer from `TrySteer` every frame.
- A swarm's whole body follows it (`SwarmFauna.Kick`).

Colonies that are grids of agents (builder, substrate, NCA) and the managers say no (`CanBePossessed`).

**The attack** (`FaunaAttackDriver`, added on demand to any creature):
- A body with a rig whips every limb toward the target, with a wave down each chain: the Brittlestar's five arms, from its
  hub `Root1`.
- A body with no rig lunges: its model punches toward the target and swells.

It is presentation only. Whoever plays it decides what it hits: the wasp's scored Strike lands at 35% of the attack
(`StrikeAt`), through `CombatHitDrain` (PvP is petals only).

## 4. Wild strikes on Ark escort vessels

Garrett, with the same sign-off: "fauna missing this should have this and use it against vessels when attacking an ark".
On a hostile Ark escort world the squad has not pacified, a wild creature within 24 u (plus its body) of a squad vessel
plays its attack at it. The escort's danger contact (`ArkEscortSO.faunaStrikeEffect`,
`VesselElementalDebuffByDangerPrismEffect`, weight 1, petals only) then lands, at most once per creature every 2.5 s
(`ArkEscortRun.Strikes.cs`).

Possessed creatures, murmurations and creatures this machine does not simulate never strike there. This is the escort's
own carve-out, `Docs/ECOSYSTEM.md` section 0.w. Affects: Shombith's Voyages (queued for his `/approve`).

## 5. Recording rides for the fauna

Garrett, 2026-11-14: "this vessel will give us play time on fauna which will give us data that can be used for more
sophisticated fauna behavior ... similar to what we do already with vessels." `PossessionRecorder` is automatic, like
`HumanFlightRecorder`, and records a person's rides only (not an AI's), in the editor and development builds:

- **Ten times a second:** the host's pose, speed and top speed, the sticks, throttle, triggers and burst, the nearest
  rival vessel, and the rival prisms within its mouth reach.
- **Events:** each mouthful, attack, hit and burst.
- **Header:** the species, its diet, body and mouth size, the cell, the rider's element levels, and how the ride ended.

It writes `possession_<Mode>_<Species>_<time>.json` (schema `cosmic-shore.possession/1`) to the flights folder, and the
editor sends it with the flights to `bugledger/<user>` (`BugHuntReports`). The name is not `flight_*`, so no vessel
learner reads it. No learner reads it yet: that is the next step for fauna behaviour.

## 6. Stand-ins and follow-ups

- **The airframe is the Tiger Moth's** (`TigerMoth.prefab` cloned, the Mass/Dust mode removed; its dust skimmer stays
  switched off because nothing drives it). The **model** is a `/vessel-model` round: a wasp hull sized so its nested pose
  tucks in beside a heart crystal, with a sting take.
- **The icons** are a `/ability-icon` round. The HUD is the Tiger Moth's stand-in view; the Time card's in-host icon
  slot ships empty.
- **The six FMOD slots ship empty:** sting, nest, leave, attack, suction and burst.
- **Multiplayer:** possession needs the hull's owner and the creature's simulation authority on one machine. So it works
  solo and for the host's own hull, and a client hull's sting is refused with one warning. A client hull is a follow-up:
  a ServerRpc sting, with the host steering from the client's sticks.
- **The AI:**
  - Free, it stings the creature ahead, or any creature in the cell.
  - In a host, it heads for the nearest rival vessel, else the densest rival mass. It keeps RT held (suction or the
    attack) and moves on after 14 s.
  - It is a first cut, not trained (`Docs/AI_SYSTEM/AI_PLAN.md`: its element games come first).
- **Not run in the Unity editor yet:** `Docs/UNITY_VERIFICATION_CHECKLIST.md` lists the hand tests.
