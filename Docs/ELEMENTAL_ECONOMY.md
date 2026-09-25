# The Elemental Economy

**A vessel debuff MOVES elements; it does not decay them.** Before this change every
vessel-on-vessel elemental debuff in the game was a temporary modifier that faded back to zero
over about four seconds, so a match-long fight over elements changed nothing: what a pilot was
drained they got back, and what an attacker took they never had. A "debuff" was an inconvenience
with a timer on it.

Now every one of them is a **transfer** out of the victim's persistent level, and it lands in
exactly one of three places:

| Form | Who gets the petals | Which verbs | Conserves? |
|---|---|---|---|
| **Steal** | the attacker, immediately | contact — the Squirrel's joust, the Rhino's sword | yes |
| **Eject** | nobody yet: they are knocked out of the hull as free-for-all crystals | ranged — guns, rockets, blasts, the Serpent's rifle | yes |
| **Burn** | nobody, ever — destroyed | a **hostile danger prism**, and nothing else | **no — this is the sink** |

So elements circulate. Lifeform reproduction and spawning are the only **source**, a hostile
danger prism is the only **sink**, and everything in between is pilots trading the same material
back and forth.

---

## 1. The unit: one petal

**One petal = one integer element level = 0.1 normalized = one ejected crystal at world scale 1.**

That is not a coincidence to be admired, it is an arithmetic identity three files have to keep:

- `ResourceSystem.PetalNormalized` is `1f / LevelScale` — the step the HUD flower takes, and the
  step `IncrementLevel` already used.
- `ElementalCrystalEjector.PetalCrystalWorldScale` mints at world scale **1**.
- `SkimmerAdjustElementLevelByCrystalEffectSO.ComputeLevelGain` pays
  `lossyScale.x × levelPerUnitScale`, and the shipped `levelPerUnitScale` is **0.1**.

`1 × 0.1 = 0.1` = one petal. Retune `levelPerUnitScale` — a perfectly reasonable thing to want to
do — and every ejected petal silently starts paying more or less than it cost to knock loose.
`Tools/Build/check_elemental_economy.py` asserts the product, and its self-test fires on exactly
that edit.

**Why the amount is not encoded in the crystal's SIZE.** It could have been: mint one crystal
scaled to whatever was taken. It breaks at five petals, where `maxLevelGainPerCrystal` (0.5)
clips — and a clipped crystal is a quantity the player can see and a reward they cannot. Fixed
scale, variable count.

## 2. The two rules that make it conserve

Both live on the victim (`ResourceSystem.AccrueElementalLoss`), not on the caller, so no call
site can get them wrong.

**Nothing partial ever leaves.** A hit accrues against a pending pool; a petal only comes off the
base level when that pool reaches a *whole* one. The remainder stays pending and is spent by the
next hit, so ten cheap hits take exactly what one dear hit worth the same total takes — proven in
the harness, T2.

This is not tidiness. A Sparrow bullet drains 0.1 of a petal at ninety rounds a second; without
accrual, ejection would have to mint a 0.1-scale speck of a crystal ninety times a second. With
it, emptying a magazine into someone produces **one** crystal. And because a petal is also one
step of the HUD flower, **the flower's step, the crystal, and the loss are the same event** — the
player reads the transfer without being told about it.

**You cannot take what is not there.** The take is clamped to what the victim holds *above resting
level 0*. A stripped pilot has nothing left to give; an attacker can never be handed a petal that
did not exist. The base band **[0, 10] is the pot**, and every transfer is a move inside it.

A consequence worth stating: the deficit band **[−5, 0) is now reachable by transients only** —
the exact mirror of the overcharge rule (only transients reach 10–15), and for the same reason. A
permanent loss bottoms out at empty.
## 2.1 Where the material comes from — and why a fight can produce none

**The pot starts EMPTY, in every mode.** This is the design, confirmed on the playtest report
below, and it is the direct consequence of *you cannot take what is not there*: a pilot who holds
nothing yields nothing, however hard they are hit.

Measured, so nobody re-derives it:

- **Nothing seeds a pilot's levels at spawn.** `ElementalComebackSystem.ApplyInitialValues` is the
  one path that writes base levels at turn start, and every scene's profile authors
  `InitialMass/Charge/Space/Time: 0` — 21 of the 22 arcade scenes share
  `AstroLeagueComebackProfile`, and SkimRace's own profile is also all zeros. No arcade card
  authors `SO_ArcadeGame.StartingElements` either; that field is the **Arena** per-hull handicap
  (Regatta), and `VesselController.ApplyStartingElements` deliberately leaves a hull with no row
  at rest rather than writing zeros over it.
- **The only base-level GAIN in the game is collecting an ELEMENTAL crystal.**
  `SkimmerAdjustElementLevelByCrystalEffect` is the single live asset of its type, and it is
  carried by `Resources/ElementalCrystalSet` (so every lifeform heart, and every ejected petal,
  pays it back on pickup) and by the Wanderway toy. `VesselIncrementLevelByCrystalEffect` and
  `VesselAdjustLevelByCrystalEffect` exist as assets and are referenced by **nobody**.
- **An OMNI crystal grants no levels at all.** It runs the vessel's `vesselCrystalEffects`, and on
  the Sparrow that is `SparrowVesselWardByCrystalEffect` (8 s, warding **every** source) plus
  haptics. Its skimmer container is empty. So in a mode whose only pickups are omni crystals, the
  one thing a crystal does to this economy is make you *immune* to it for eight seconds.

### The on-ramp, per mode

A mode has an on-ramp iff its cell configs' spawn profiles list a lifeform — each drops exactly
one elemental crystal on death (`LifeFormCrystal`). Counts are configs summed across all
intensities, not live populations.

| Heart source | Modes |
|---|---|
| **yes** | Rampage · Wrecking Ball · Bloomrush · The Bends (8 fauna / 36 flora each) · Undertow · Wildlife Liberation (16) · Tollway (12/4) · Dog Fight · Salvo · Broadside (4) · Astro League · Scarab Scramble (3) · Headlong · Redline · Switchback (2) · Brood Rush (1) |
| **NO** | Breakwater · Cleave · Hijack · Regatta · Skein · SkimRace · Joust · Scurry · Freestyle MP · both Duel for the Cell · both Wildlife Blitz |

So the economy is **structurally inert** in the seven "NO" modes — there is no way for a petal to
enter that match — and in the "yes" modes it is inert *until somebody hunts*. Both are the rule
working, not a defect; what they are not is obvious from playing.

### The playtest report this came from

> *"I played a dogfight with the Sparrow and I saw no crystals leave either of our vessels when I
> was hit or hit them with missiles or guns."*

Correct behaviour, end to end. Both pilots sat at 0, so every hit's `AccrueElementalLoss` returned
0 petals and `ElementalCrystalEjector` was never reached. Dog Fight *has* an on-ramp — its Boneyard
scavengers — but a dogfight never uses it, and its crystals are omni.

**The general shape worth carrying: a conserved economy with no starting stake is indistinguishable
from a broken one.** Every piece can be individually correct and the whole thing still does nothing
that a player can see, because the clamp that makes it conserve is also what makes it silent. If a
mode is meant to have a fight over elements in it, its on-ramp has to be something the mode's own
verb reaches — a heart a dogfighter would actually fly through, or crystals that pay levels —
rather than a source that exists in the arena and nothing points at.

## 3. The recovery band: one level per five seconds

`ResourceSystem.elementalRecoveryRate` **0.05 → 0.02**. It still pulls base levels back into
[0, 10] from both ends; it now takes five seconds per level instead of two, so a pilot gets to
enjoy an overcharge and to feel a punishment rather than watching either evaporate.

⚠ **Four prefabs SERIALIZE this field (Sparrow, Squirrel, Rhino, Dolphin) and eight are silent.**
Editing the C# initializer alone splits the fleet in half — the rule-4-i trap, *a silent prefab is
not an unset one*. All five sites moved together and the gate asserts they agree.

## 4. The sink, and the locked rule it does not break

CLAUDE.md's danger-prism rule is **LOCKED**: *danger prisms are not safe to their own domain, and
a danger-prism effect must not GATE on domain.* Nothing here gates. Both branches run for every
vessel that touches the prism; the prism's domain only chooses **how the loss lands**:

- **opposing-domain danger** (someone else's trap, a creature's rods, neutral `Domains.Blue`
  environment mass) → **BURN**. Gone from the match.
- **own-domain danger** (your own trail) → the temporary debuff it always was. You are still
  punished for flying into your own hazard; the arena just does not eat your crystals for it.

It falls this way round because burning is an act of the **world** against a pilot, and a pilot's
own trail is not the world — and because a self-inflicted permanent burn would turn the Squirrel's
Live Wire (Charge 5, 10× energy for skimming danger) into a trap that destroys the levels it pays
out.

**Open question for playtest:** fauna spawn in the cell's *controlling* colour, so a creature's
danger rods burn the pilots who do not control that cell and merely sting the ones who do. That
asymmetry is emergent, not designed. It is either a satisfying reward for territory or a
rich-get-richer problem; only play will say.

**Also worth watching:** the Dolphin's Time-5 Drift Ward wards `DangerPrism` and nothing else,
which now means it wards *the sink*. That is a real and deliberate strengthening of it.

## 5. Every hull can now fight for it

The headline requirement — *every vessel needs the ability to debuff other vessels* — was false
for two hulls, in two different ways.

| Hull | Before | Now |
|---|---|---|
| Sparrow | Bullet + 3 missile tiers | eject |
| Urchin | Bullet (spike) | eject |
| Squirrel | Strike (joust overtake) | **steal** |
| Dolphin / Scarab | Debuff (cone / plate, one shared asset) | eject |
| Manta | Debuff (bloom, Mass+Space only) | eject |
| **Rhino** | reported a Strike that **drained nothing** | **steal** — `RhinoSwordStealBySkimmerEffect` |
| **Serpent** | **no anti-vessel verb at all** | **eject** — the sniper cone strips pilots |

The two gaps were not the same kind of gap, and that mattered:

- **The Rhino's was pure wiring.** Its sword already reported a Strike and already called the
  drain; the drain declines Strike (authored per weapon) and no Strike asset named the Rhino. Fixed
  with a second asset of the *existing, proven* `VesselOvertakeBySkimmerEffectSO` type plus one new
  serialized flag, `requireOvertake` — **default true**, so the Squirrel's asset, which does not
  carry the key, deserializes to exactly its old behaviour. The Rhino's authors it off: a blade
  connects on its own terms, and its own combat-hit reporter already prices being faster.
- **The Serpent's was capability.** It has no skimmer, no projectile container and no blast — its
  only weapon is a hitscan that queried *prisms and nothing else*. `StripVessels` sweeps the same
  cone against the live vessel roster, reusing `PrismSpatialIndex.ConeContains` — the same public
  predicate `QueryCone` applies to prisms, with the same apex, axis, range, half-angle and minimum
  path radius. **One cone, one answer**: a pilot the tracer visibly passes through cannot be missed
  by arithmetic that disagrees with the mass around them. It stops where the round stopped, so a
  limited `PierceCount` cannot strip someone standing behind the prism that halted it.

⚠ **The Serpent's magnitude is a placeholder, not a price.** `vesselStripPerElement` (0.1 = one
petal) is authored on `SniperShotAction.asset` rather than derived from Broadside's price table,
because this hull has no priced anti-vessel class yet — *what a sniper round is worth* is part of
the per-vessel Charge pass. Playtest it.

## 6. The consequence that will surprise you in playtest

**Losing petals can relock a level-5 upgrade.** `AccrueElementalLoss` goes through `AdjustLevel`,
which emits `OnElementLevelChange`, which drives the HUD flowers, the hull morphs *and* the
replicated `NetElementUnlocks` bits — and those relock below level 4. Under the old decaying
debuff this was nearly unreachable: a temporary modifier faded before it mattered. Now a pilot who
is stripped genuinely loses their upgrade until they earn the levels back.

That is a real escalation of what a debuff *is*, and it is the single most likely source of a
"this feels brutal" playtest note. It is also the thing that makes the economy matter — an element
level is now worth defending — so the dial to reach for is the per-weapon magnitude
(`author_combat_debuff_magnitudes.py`), not the permanence.

Note it interacts with the comeback system in the player's favour: `ElementalComebackSystem`
composites on its own layer and never touches the base, so a stripped pilot who is also losing
still gets their comeback bonus on top.

## 7. What this does NOT do

- **The Charge mapping is untouched, deliberately.** Charge does not yet uniformly own each hull's
  debuff ability; several hulls use one weapon for both destruction and debuff (the Dolphin's
  blast is the clear case), so that is a per-vessel pass rather than a bulk one.
- **The Bends and Undertow** are scored entirely on the Debuff drain, via `requireDebuffableVictim`
  — *the score follows the effect*. That still holds: a warded pilot yields nothing, so no transfer
  and no score, exactly as before. What changed is that a landed bend is now permanent, which makes
  both modes meaningfully harsher. **Wants a playtest.**
- **Ejected crystals are per-peer local objects**, exactly as the food web's crystals are per-peer
  unless a species sets `NetworkSynced`. Two peers can disagree about who collected one. That is
  the platform's existing stance rather than a new compromise; `FloraNetworkSync` is the precedent
  if a mode ever needs them authoritative.
- **The ally buff stays temporary.** A buff is not a transfer — there is no victim to take it from
  — so making the Squirrel's mirrored overtake buff permanent would mint petals out of nothing and
  break "lifeforms are the only source". Jousting an enemy *moves* material; jousting a friend only
  encourages them.

## 8. Verification

| What | How |
|---|---|
| The transfer arithmetic | `bash Tools/Build/elemental_transfer_harness/run.sh` — compiles the shipped C# against a stub surface and **runs** it. 7 blocks, negative-controlled. |
| The economy's four invariants | `python3 Tools/Build/check_elemental_economy.py` (`--self-test`: six controls, all fire) |
| The drain magnitudes | `python3 Tools/Build/author_combat_debuff_magnitudes.py --check` |
| Standing gates | `check_conditional_compilation` · `check_enum_member_references` · `check_switch_label_collisions` · `check_self_referential_locals` · `check_using_directives` |

**Nothing has been run in the editor.** The harness cannot bind a `MonoBehaviour` base (Roslyn
abandons class-body binding on an unresolved base type), so `ResourceSystem` and the four effect
SOs are type-checked only by the gates above and by reading.

In-editor verification is recorded in the PR body's **Verification status** section, which the
`/qa-backlog` scan picks up — `Docs/UNITY_VERIFICATION_CHECKLIST.md` is superseded and new work
does not get a section there.

**Step 0, and every step below depends on it: GIVE THE VICTIM SOMETHING TO LOSE.** Pilots spawn
at resting level 0 in every mode (§2.1), so a fight between two fresh pilots produces no transfer
at all and that is the clamp working, not a failure. Before testing any of this, have the victim
kill a lifeform and collect its heart until their flowers are visibly off grey — or test in a mode
with flora to graze (Rampage, Wrecking Ball, Bloomrush, The Bends). What a human needs to confirm:

1. **A petal visibly changes hands.** Two pilots, opposing domains. Joust one (Squirrel) — the
   victim's flower steps DOWN one colour and the jouster's steps UP, and neither drifts back
   within five seconds.
2. **A ranged hit ejects.** With a victim holding petals (step 0), shoot them with the Sparrow
   until a flower steps down: exactly one crystal should leave their hull per step, fly along the
   shot, slow to a stop and be collectable by anyone (it wears the lime free-for-all colour). A
   bullet is 0.1 of a petal per element, so ten admitted hits step all four flowers at once and
   eject four crystals; a missile's shockwave tier is a whole petal on each in one go.
3. **The sink burns.** Ram an OPPOSING domain's danger prism — the flower steps down and stays
   down. Ram YOUR OWN danger trail — the flower dips and recovers.
4. **A stripped pilot yields nothing.** Drive a victim back to level 0 and keep hitting them: no
   further crystals, no further steps, and the attacker stops being paid. This is the state every
   pilot STARTS in, so seeing it at spawn is confirmation rather than a bug (§2.1).
5. **The Serpent can fight.** Scope + fire through an opposing pilot: their flowers step down and
   crystals leave their hull.
6. **MPPM two-client**: confirm both peers agree on the flower levels after a joust, and note
   whether they agree on who collected an ejected crystal (they may not — §6).
