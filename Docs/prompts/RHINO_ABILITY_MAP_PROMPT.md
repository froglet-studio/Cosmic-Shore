# Prompt — prepare the Rhino's elemental ability map for a design decision

Paste everything below into a fresh session.

---

The Rhino locks three shipped game modes — **Astro League**, **Peel the Cage** and **Headlong** —
and its elemental ability map has **one of four slots designed and none of its four level-5
upgrades authored**. Three modes outsiders will play in the invite build rest on a hull the fleet's
own contract calls unfinished, and its HUD consequently renders four cards of which three are
LOCKED.

**This task does not author the mapping.** Inventing an element→ability mapping to satisfy an audit
is explicitly forbidden — `Docs/ElementalAbilitySystem/FLEET_MAPS.md` §2 holds un-approved proposals
for exactly this reason, and the open slots are marked `(open design slot)` because design has not
made the call. What this task produces is **the proposal a designer can accept or reject in one
sitting**, grounded in what the Rhino actually does today.

Use the `/vessel` skill. Read `Docs/ElementalAbilitySystem/FLEET_MAPS.md` and `AUDIT.md`, and the
Rhino ability docs in `Assets/_Scripts/Controller/Vessel/R_VesselActions/` —
`RHINO_RAMP_BOOST.md` and `RHINO_SHIELD_SWIPE.md`.

## Measured 10 Sep 2026 from `Assets/Resources/ElementalAbilityMaps/Rhino.asset` — re-verify

| Element | Ability | Level-5 upgrade |
|---|---|---|
| Charge | `(open design slot)` — *"Charge mapping not yet designed for the Rhino."* | — |
| Mass | **Trail Slabs** — *"Mass raises the grown trail slab maximum size."* `MultiplierAtFullLevel: 1.5`, `MinMultiplier: 0.25` | — |
| Space | `(open design slot)` | — |
| Time | `(open design slot)` | — |

Note every entry has `Input: 0`, including Trail Slabs — the one named ability is a **passive
scalar**, not a bound action. So the Rhino currently has no ability on any button at all.

For scale, the fleet stands at **25/32 slots named and 19/32 level-5 upgrades authored**. Dolphin,
Sparrow, Squirrel and Urchin are complete at 4/4 + 4/4; Scarab is 4/4 named with 3/4 upgrades.
Manta (3/4, 0/4) and Serpent (1/4, 0/4) are behind the Rhino in priority because neither locks a
mode.

## What the Rhino demonstrably already does

The proposal has to be built from shipped behaviour, not from a blank page. Establish each of these
from the code and quote the authored numbers:

- **The ramp boost.** `RampBoostActionSO.MultiplierFor` pays full power only while the pilot holds
  full throttle and near-zero stick, and past that it **grades down** rather than switching off,
  lerping toward plain cruise as the stick goes over. Headlong's entire course design is cut against
  that curve — composing the lerp with linear-in-stick turn rate and speed-dependent max turn rate
  gives a continuous speed/radius trade from **332 u at 1210 u/s** down to **29 u at cruise**.
  `straightnessGraceBand` latches it back to the engage threshold.
- **The energy sword.** The Rhino's skimmer is a swung blade: `SkimmerSwingKinematics` resolves a
  contact on the **point of the blade that touched**, with the strike speed being that point's true
  velocity, so a swung tip fires an Astro League ball far harder than the hull — with an extra tip
  bonus on top. It is also the one force that breaks a super-shield.
- **Trail slabs.** The existing Mass scalar, and the reason Peel the Cage's arena reads the way it
  does.
- **Turn-radius convergence.** `RotationThrottleScaler 0.5` gives the Rhino an asymptotic turn
  radius of `180/(π·r)` = **115 u**, where every other hull's grows without bound. This is *why*
  Headlong is the Rhino's mode.

## What to produce

**1 · A proposal table**, one row per open element, each naming: the ability, the input it would
bind, what the element scales and over what range, the level-5 upgrade, and — the part that makes it
decidable — **which existing mechanic it is built from**. A proposal that needs new gameplay code is
a bigger conversation than this one.

**2 · The mode consequence, stated per option.** Each of the three modes is balanced against the
Rhino as it flies *today*. Say what each proposal would do to Astro League's ball physics, Peel the
Cage's destruction rate and Headlong's corner budget. Headlong is the sharpest constraint: its
`HeadlongCircuitSettings` carries a **compile-time copy of the ramp boost's numbers**, held in step
by `RhinoRampGradingTests`, so retuning `maxBoostMultiplier`, `straightnessGraceBand` or
`RotationThrottleScaler` **moves every corner on the course**. Any Time or Space proposal that
touches the boost has to say so.

**3 · The comeback-system check.** The elemental comeback system hands upgrades to whoever is
**losing**. Run each proposed level-5 upgrade against that: Wildlife Liberation's recorded trap is a
core promise gated behind a level the comeback hands the trailing player, and The Bends' is an
upgrade that became a hard counter to the only way you could be scored on. An upgrade that makes a
trailing Rhino unbeatable in Astro League fails before it is authored.

**4 · An explicit recommendation**, with the one you would ship and why — not a menu of four
equivalent options. Then stop and hand it to design.

## Constraints

- **Do not write the mapping into `Rhino.asset`.** Leave the open slots open. The deliverable is a
  section in `Docs/ElementalAbilitySystem/FLEET_MAPS.md` §2, in the shape that file already uses for
  un-approved proposals.
- **Do not retune the ramp boost, the sword or the trail slabs** to make a proposal fit. If a
  proposal requires a retune, that retune is part of the proposal and needs its own sign-off.
- Equal-elements is the law: whatever Charge, Space and Time get must be comparable in value, or the
  comeback system distributes an imbalance.
- If a slot genuinely has no good answer from shipped mechanics, **say so** and leave it open. Three
  good slots and one honest gap beats four invented ones.

## Definition of done

1. A proposal for Charge, Space and Time in `FLEET_MAPS.md` §2, each traced to an existing mechanic.
2. Per-mode consequences stated for all three modes, with Headlong's compile-time coupling called
   out.
3. Every proposed level-5 upgrade checked against the comeback system.
4. One clear recommendation.
5. `Rhino.asset` unchanged, and `Docs/STEAM_RELEASE_TASKS.md` R10 moved to *awaiting design*.
6. A note on whether Trail Slabs should stay a passive scalar or become a bound action, since the
   Rhino currently has nothing on any button.
