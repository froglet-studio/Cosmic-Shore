# Element scaling is PARAMETER-addressed — one channel, and why the other one had to go

**Shipped 2026-09-18.** The fleet had two ways to say "this element scales this number", and only
one of them could say *which* number. The other is removed.

> **The rule: a quantitative element→parameter scaling lives in an `ElementalFloat` on the asset or
> component that OWNS the number.** It can then only ever reach that number. There is no generic
> per-element multiplier and no `handler.Multiplier(element)`.

The map (`ElementalAbilityMapSO`) keeps the **qualitative** half — `UnlockLevel`,
`RelockBelowLevel`, `LatchPolicy`, `IsUpgradeActive` — because "is this element's upgrade on" is
genuinely a fact *about the element*, not an unlabelled number about something else. It carries no
numbers at all now; it is a declaration again.

## What was wrong, measured

`ElementalAbilityEntry.MultiplierAtFullLevel` + `MinMultiplier`, read back through
`R_VesselElementalAbilityHandler.Multiplier(Element)`, addressed **an element and never a
parameter**. Nothing in the type system or the asset said which number a vessel's Time multiplier
was for, so every reader of that element on that vessel got it.

The worst consumer was `VesselTransformer.CurrentBoostAmount()`, which multiplied
`BoostMultiplier` by `Multiplier(Element.Time)` for **every hull in the fleet**. Introduced
2026-07-14 in `2d84aa7b9` (the commit that brought the whole map layer live), justified in its own
message as *"TIME → boost speed: both MoveShip implementations scale boostAmount by the live Time
multiplier (1x for vessels without a map)"*. That parenthetical was true only while the Sparrow
owned the only authored map.

Measured across the eight hulls at removal:

| Hull | Time ability *declared* | map ×@L10 | What the fleet-wide read actually did |
|---|---|---|---|
| Manta | Soar | 1.3 / floor 0.7 | correct — Soar *is* boost speed |
| Sparrow | Afterburner | 1.5 / floor 0.5 | correct — the original wiring |
| Dolphin | Charge Fill Rate | **1.0** | inert — pinned defensively |
| Scarab | Throttle | **1.0** | inert — pinned defensively |
| Squirrel | Boost Ring | **1.0** | inert — pinned defensively |
| Urchin | Slip | **1.0** | inert — pinned defensively |
| Rhino | Ramp Spool | 2.5 / floor 0.5 | **×2.5 on the ramp CEILING — undeclared** |
| Serpent | Boost Duration | 1.6 / floor 0.25 | **×1.6 on boost SPEED — undeclared** |

**Two correct, four paying a tuning slot to ask a base class not to act, two silently wrong.** And
22 of the fleet's 32 map entries authored `1 / 1`, i.e. the field did nothing on 69% of rows.

The four defensive pins were not a workaround somebody forgot to clean up — they were the
*documented convention* (the vessel contract's old §4.2: *"pin the map's generic
`MultiplierAtFullLevel` to 1"*). That is the tell. **When the convention for using a mechanism is
to switch it off, the mechanism is the problem.**

### The two live defects it produced

**Rhino.** `RampBoostActionExecutor` sets `IsBoosting` and `BoostMultiplier = MaxBoostMultiplier`,
so `CurrentBoostAmount` fired and multiplied the ramp's **ceiling** by the same scalar line 89 had
already read for `accelerationPerSecond`. Time was applied twice to one ability. Three places said
otherwise: `CLAUDE.md` (*"Time is the ramp's WIND-UP RATE and not its ceiling"*), the map asset's
own `AbilityDescription` (*"It does not raise the ceiling, only how long the run-up to it takes"*),
and `regatta_balance.py:204`, which computes the Rhino's `top` with **no Time term**.

**Serpent.** `ConsumeBoostActionExecutor` sets `IsBoosting` and `BoostMultiplier`, while line 129
scales `BoostDuration`. Time both lengthened *and* sped the boost; the map declared duration only.

Both are corrected here (fix-and-flag, per sign-off). See **Playtest** below.

## What the channel looks like now

`ElementalFloat` was already the right shape — pure serialized data, named for its parameter,
living on its owner. It gained exactly two things:

1. **A floor** (`UseFloor` / `Floor`), which `ElementalScaling` had and it did not. Not polish: in
   the deficit band an unfloored multiplier with a large `Max` crosses zero and **inverts** the
   parameter. The Sparrow's SPACE muzzle-speed factor reaches `1 + (9−1)(−0.5) = −3`.
2. **One formula.** `EvaluateLive` and the legacy bound `ScaleValueWithLevel` now route through a
   single private `Evaluate`, so they cannot disagree — they did before, and only *outside* exact
   tenths, which is the hardest kind of disagreement to notice.

**There is deliberately no `Mode` enum.** `Min`/`Max` already mean "value at rest" / "value at
level 10", so *a multiplier is just an `ElementalFloat` whose `Min` is 1*. Whether the result is a
value or a factor is the consumer's business, not the data's. `ElementalFloat.Multiplier(atRest,
atFull, element, floor)` is a factory for readability, not a second code path.

`ElementalScaling` keeps its math (`Multiplier`, `MultiplierFromRest`, `RoundGrowthFactor`, the
qualitative threshold). Only the **addressing** died, not the arithmetic.

### One behaviour delta, stated

`EvaluateLive` previously used `GetLevel(element) / 10f` — i.e. `FloorToInt(normalized × 10) / 10`,
quantized to tenths — while `ElementalScaling.Multiplier` used the **continuous**
`GetNormalizedLevel`. The unified formula uses the continuous one.

Crystal progression moves the level in exact tenths (`AdjustLevel(±0.1)`) and the two agree there.
They diverge only while a temporary effect, fauna buff or comeback bonus is decaying — all
continuous — where the old form **stepped** and this one **glides**. No authored endpoint moves:
both return `Min` at rest and `Max` at level 10. Measured on the ×1→×2.5 case: 1986 of 2001 samples
differ, max delta 0.15, zero at every tenth.

## Where each multiplier went

| Element → parameter | Now lives on | ×@L10 / floor |
|---|---|---|
| Squirrel CHARGE → skim energy per hit | `SkimmerBoostPrismEffectSO.chargeEnergyMultiplier` | 2 / 0.25 |
| Urchin CHARGE → spike reach | `UrchinSpikeActionSO.chargeRangeMultiplier` | 2.5 / 0.4 |
| Rhino MASS → trail slab ceiling | `GrowTrailActionSO.massMaxSizeMultiplier` | 1.5 / 0.25 |
| Sparrow MASS → turret prism z-stretch | `FullAutoBlockShootActionSO.massPrismStretchMultiplier` | 2.5 / 0.4 |
| Sparrow SPACE → muzzle speed | `FullAutoActionSO.spaceSpeedMultiplier` | 9 / 0.4 |
| Scarab SPACE → forged ball size | `ScarabBallForge.BallSizeScale` (C#) | 4 / 0.5 |
| Manta TIME → boost speed (Soar) | `Manta.prefab` `VesselTransformer.BoostSpeedMultiplier` | 1.3 / 0.7 |
| Sparrow TIME → boost speed | `Sparrow.prefab` `VesselTransformer.BoostSpeedMultiplier` | 1.5 / 0.5 |
| Rhino TIME → ramp wind-up rate | `RampBoostActionSO.timeAccelerationMultiplier` | 2.5 / 0.5 |
| Serpent TIME → boost duration | `ConsumeBoostActionSO.timeDurationMultiplier` | 1.6 / 0.25 |

An eleventh site, `YawsteryActionExecutor`, read `Multiplier(_so.TurnRateElement)` where
`turnRateElement` defaults to `Element.None` and **both shipped Manta assets are silent** — so it
was inert as shipped. The element *picker* (itself the element-addressed pattern) is replaced by a
disabled `turnRateMultiplier` `ElementalFloat`, keeping the authoring hook in the one idiom.

Three notes on the homes:

- **Every host is a ScriptableObject or a per-prefab MonoBehaviour, so none is bound.**
  `ElementalVesselComponent.BindElementalFloats` reflects over the fields of an
  `ElementalShipComponent` (a MonoBehaviour), and `ShipActionSO : ScriptableObject`. That is why
  `FullAutoActionSO`'s and `UrchinSpikeActionSO`'s docstrings were right to forbid *binding* a float
  on a shared asset and why an `EvaluateLive`-only float there is safe: `Min`/`Max`/`element`/`Floor`
  are authored constants and the per-vessel part is the `status` argument. All eight action assets
  were verified single-hull before the move.
- **`VesselTransformer.BoostSpeedMultiplier` follows a shipped precedent** —
  `ThrottleScalerMultiplier` has been a per-prefab `ElementalFloat` on that class all along (and is
  also unbound, `VesselTransformer` not being an `ElementalShipComponent`). Boost speed is a
  property of a hull, so it is authored on the hull; six of eight leave it disabled, which makes
  `CurrentBoostAmount` arithmetically unchanged for them.
- **`ScarabBallForge` is a `static class`** and can hold no serialized field, so its multiplier is a
  `static readonly ElementalFloat` in C# — consistent with the six other Scarab knobs that live in
  code for the same reason (contract §4-i). Promote it to a config SO if design wants to tune it.
  Cost: `element_ability_table.py` reads assets, so it cannot see this one row.

## Verification

- **Executable equivalence proof.** The shipped `ElementalFloat.cs` was compiled (Roslyn, langversion
  9) against a verbatim transcription of the retired `ElementalScaling.Multiplier` and **run**: all
  ten migrated multipliers are **bit-identical** to what they replaced, 201 samples each across the
  whole [−5, 15] band. 18 assertions, 0 failures, with negative controls that fire (a wrong `atFull`,
  a wrong floor, and the quantized-vs-continuous delta above). Also proved: a disabled
  `ElementalFloat` returns exactly 1 everywhere, and an off-vessel read falls back to the authored
  base rather than 0.
- **Durable test:** `Assets/_Scripts/Tests/Editor/ElementalScalingUnificationTests.cs` locks each
  migration's rest / full / floored-deficit values, the disabled-is-1 invariant, and — structurally
  — that the retired surfaces stay gone and the map keeps the qualitative half.
- **Gates:** `element_ability_table.py` (12 disagreements, **unchanged** from before the branch and
  all pre-existing; zero `RETIRED CHANNEL PRESENT`), `regatta_balance.py --check`,
  `author_manta_kit_assets.py --check`, `author_urchin_assets.py --check`,
  `author_regatta_assets.py --check`, plus `check_enum_member_references`,
  `check_switch_label_collisions`, `check_using_directives`, `check_self_referential_locals`,
  `check_conditional_compilation`.
- **Roslyn over all 18 changed files** with the .NET reference pack: zero errors in the classes a
  missing `UnityEngine` cannot cause. **Stated limit:** 782 `CS0246` remain (no Unity DLLs in this
  container), and an unresolved base type stops Roslyn binding that class's body, so this pass is a
  backstop rather than a full type check. The equivalence proof above is the strong evidence.
- **Not run in the editor.**

### Playtest — Rhino and Serpent

Both lose an undeclared Time application. Neither number was ever authored as a design.

| Hull | What changes | At Time 10 |
|---|---|---|
| Rhino | ramp CEILING no longer scaled by Time (wind-up rate unchanged) | top speed ÷2.5 vs shipped |
| Serpent | boost SPEED no longer scaled by Time (duration unchanged) | boosted speed ÷1.6 vs shipped |

`regatta_balance.py` needs **no re-authoring**: its `rhino_model` never modelled the ceiling, so the
model was already describing the corrected build. Re-run after the fix, its output is **identical**
to clean HEAD (spread 5.99× / 5.67× / 5.41× / 5.27×, all under the asserted 6.1×). CLAUDE.md
previously recorded the missing ramp-ceiling endpoint as a *modelling gap*; it was not — the model
was right about intent and the build was wrong.

## Two gate bugs this branch surfaced

Both were latent, both were found by running the gates rather than reading them, and both are fixed
here because this branch depends on those gates.

1. **`check_using_directives.py` was blind to every path containing a space.** `git status
   --porcelain` *quotes* such paths, so `line[3:].strip()` kept the quotes, `endswith(".cs")` failed,
   and the file was dropped **silently**. It saw 10 of my 18 changed files — including two of the
   three whose entire edit was adding a `using`. Now uses `-z` (which never quotes) in both
   collectors; the scope line went 10 → 18 and the check still passes. Same disease as the stale-base
   bug its own docstring records: *a gate must not be able to shrink its own scope by accident.*
2. **`regatta_balance.py` read the Serpent's boost multiplier as the bare key `Value`.** `_key`'s
   pattern is `^\s*key:` — any indent, first match wins — so adding `timeDurationMultiplier` earlier
   in the same asset silently handed it a different `ElementalFloat`'s `Value`. The model then priced
   the Serpent with **no boost at all** and the fleet's spread doubled to 13.11×, while `--check`
   still exited 0 because its assert is on the spread, not on the read. Now `_nested_key(serpent,
   "boostMultiplier", "Value")`. *A name that does not identify its owner is not a measurement.*

## Follow-ups (logged, not done here)

1. **Two `ElementalFloat`s are authored `Enabled` and never evaluated**, because their accessor reads
   `.Value` on a ScriptableObject (which nothing binds): `GrowTrailActionSO.maxSize` (4 → 8 on Mass)
   and `FullAutoActionSO.speedValue` (375 → 4875 on Space). Shipped behaviour comes from the
   migrated multipliers instead (4 → 6, and 375 × [1..9]). Folding each pair into one float is a
   **balance change**, not a refactor, so it is a decision rather than a cleanup.
2. **The legacy bound path mutates a serialized field** (`ScaleValueWithLevel` writes `Value`), and
   several `ElementalFloat`s live on shared SO assets reached via `ShipAction`/`Skimmer` — the
   contract's rule 1. Retiring it in favour of `EvaluateLive` at every consumer is its own branch.
3. **`ResourceSystem.GetLevel` floors a float product**: `FloorToInt(0.7f * 10)` is 6, not 7, because
   `0.7f` is below 0.7. It feeds the HUD petals and the level-5 unlock test, so touching it moves
   unlock thresholds — out of scope, worth a deliberate look.
