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
They diverge only while a temporary effect or comeback bonus is decaying — both
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

---

# Second pass — 2026-09-18, same day

The first pass removed the element-addressed channel. Asked to finish with "no straggling
issues", the audit ran `element_ability_table.py --gaps` and found **12** disagreements. Seven
were the tools and the data misreporting working code; three were data that claimed a mechanic
the code did not have; two were genuine. Result: **12 → 5, and all 5 remaining are open design
slots** (Rhino Charge + Space, Serpent Charge + Mass + Space). Report: **`FLEET_GAPS.md`**.

## What was actually wrong with the code or the data

1. **Three `ElementalFloat`s authored `Enabled` had never run.** `GrowTrailActionSO.maxSize`
   (Mass 4 → 8), `GrowSkimmerActionSO.shrinkRate` (Charge 6 → 2) and
   `FullAutoActionSO.speedValue` (Space 375 → 4875) are declared on **ScriptableObjects** and
   read as `.Value`. The binder that keeps `.Value` in step reflects over **MonoBehaviour**
   fields only, so an SO-hosted float can scale in exactly one way — `EvaluateLive` — and these
   three never call it. Authored `Enabled: 0`; runtime behaviour is byte-identical. Turning any
   of them on is a balance change (each would stack with a live multiplier on the same element),
   so it is a design call, not a cleanup.

2. **`ElementalFloatBinder` was dead AND actively wrong.** It set a property named `"Ship"` that
   does not exist on `ElementalFloat` — `?.SetValue` on a null `PropertyInfo`, a silent no-op —
   and the "clone" it installed was `new ElementalFloat(original.Value)`, which drops `Min`,
   `Max`, `element` and `Enabled`. Reviving it would have silently flattened every float it
   touched. Its one reference was a commented-out call. Deleted.

3. **`AOERadialBlocks.depthScale` was a constant wearing a scaling channel's clothes** —
   `private`, no `[SerializeField]`, so unserializable and permanently 1 — and it multiplied
   *instance* fields on a **pooled** component, so any value but 1 would have compounded on
   every reuse. Deleted.

4. **`Skimmer` bound a float it reads live.** Both readers of `Skimmer.Scale` go through
   `EvaluateLive`, so `BindElementalFloats` only subscribed a handler that maintains a `.Value`
   nobody reads. Removed.

5. **The Scarab's map declared an upgrade the code retired.** "Armored Switch" went with the
   switch's prism fill on 2026-08-24; the map named it for three weeks afterwards, so the HUD
   lockup, the launch panel and this documentation set all reported a wired Mass 5. Corrected to
   an `(open design slot)` entry that records the retirement.

> **Retiring a MECHANIC is not finished until the DECLARATION that names it is retired too.** A
> map entry is not documentation — it is data that several live surfaces read.

## The standing gate

`Tools/Build/check_elemental_floats.py` (`--check`, `--self-test`) fails the build on any
ElementalFloat authored `Enabled` with `Min != Max`, declared on a ScriptableObject, that
nothing evaluates. Three things about it generalise:

- **It is keyed on the asset's own `m_Script` guid, because a field NAME is not an identity.**
  `maxSize` is declared on two ScriptableObjects *and* on the MonoBehaviour `GrowActionBase`; a
  name-keyed first cut skipped any field with a MonoBehaviour host anywhere in the tree, which
  silently exempted both SOs — the exact defect it was written to catch.
- **It states how many blocks it scanned.** Its first version computed `ROOT` one directory too
  high, walked an empty tree and reported OK. A live negative control caught it; the self-test
  could not, because the self-test supplies its own paths. *A `--check` that never reads the
  disk is not a check* — so it now refuses to pass on an empty scan.
- **A gate nobody has watched fail is a gate nobody should trust.** Both directions were proven
  against the real tree: re-enable the three floats → 3 findings, exit 1; restore → OK, exit 0.

## What was wrong with the TOOL (seven of the twelve)

`element_ability_table.py` is the answer to "show me this vessel's map", so a false gap there
reads as a vessel nobody finished. Five fixes:

| Fix | What it was reporting |
|---|---|
| Blank comments **and string literals** in `follow_static_calls` | One doc comment in `VesselTransformer` and two `[Tooltip]` strings in `ScarabAnimation` name `ScarabVesselTransformer.…`, which hung that hull's Snap Dash gate on the **Manta, Rhino and Squirrel** |
| Accept a namespace-qualified `Element.X` argument | `MantaStingActionExecutor` writes `IsUpgradeActive(CosmicShore.Data.Element.Charge)`. A pattern anchored on the bare spelling read `"Charge"` as a serialized FIELD name and dropped the gate, so **both** of the Manta's implemented L5 upgrades reported as prose |
| Parse nested-prefab-instance `m_Modifications` | An ElementalFloat authored as a prefab override is scattered, one entry per key, not a contiguous block. **Six of twelve vessel prefabs** author one; all six were invisible, which is why the Squirrel's Space slot reported `NO SCALING` while `Skimmer.Scale` sat right there at 15 → 30 |
| Read C# field initializers | A `static` class can hold no serialized field, so `ScarabBallForge.BallSizeScale` — the Scarab's entire Space scaling — exists only in C#. Also covers rule 4-i: an asset written before a field existed ships the initializer |
| `guard_state` reads a serialized bool's **C# default** | `turnUpgradeShieldsTrail` is absent from ten of twelve vessel prefabs and initialized `false`. Reading only authored YAML called that branch LIVE on all ten — which is how the Scarab's retired upgrade read as wired |

Two of those are the same mistake from different directions: **a pattern anchored on one
spelling of a thing that has several** (bare vs. qualified `Element.X`; a contiguous YAML block
vs. scattered override entries). And the comment-blanking fix **recurred inside the new gate
within the hour**: `check_elemental_floats.py` scanned raw C#, and the comment recording fix 3's
deletion quotes the deleted declaration verbatim, so the removed field came back into the
inventory *from its own obituary*.

> **A tool that matches code by pattern must not read prose — and prose quotes code constantly,
> including the prose you write to explain a deletion.**

## One reported defect that was refuted

`BACKLOG 5.5` recorded that `ResourceSystem.GetLevel` floors a float product: `0.7 * 10` is
`6.999999999999999`, so a pilot on level 7 would read as 6 and the level-5 unlock would move.
**Measured in real C#: it does not happen.** The arithmetic is float32 — `0.7f` is
`0.699999988`, `× 10` rounds to exactly `7f` — and crystal progression accumulates `+= 0.1f`,
which drifts *upward*, away from the boundary. All 26 cases land on their integer. Locked as
`ElementalScalingUnificationTests.CrystalProgressionLandsOnEveryIntegerLevel`, as a **standing
refutation** rather than a guard, so the hypothesis is not re-derived by the next person who
reads `FloorToInt` and reasons in double.

> **When a model and a build disagree, find out which one is wrong before recording it as a
> defect.**

**Nothing in this pass has been run in the Unity editor.** The gates that did run: all six
out-of-editor checks, `check_elemental_floats.py --self-test` plus a live negative control, a
Roslyn syntax compile of the eight changed C# files (zero non-missing-type errors), and a REAL
Roslyn type check of `ElementalFloat.cs` + the test file against a stub harness (clean).

---

## Verification status (ship-deep, 2026-09-18)

**Nothing on this branch has been run in the Unity editor, and there is no compiler and no CI in
the environment it was written in.** No row below says "compiles". The human at the editor is the
only gate for everything marked *not compiled*, and the QA rows named in the last column are
already written up in `Docs/QA/QA_BACKLOG.md` (`QA-P1-RHINO-RAMP-CEILING`,
`QA-P1-SERPENT-BOOST-SPEED`, `QA-P1-MANTA-SOAR-SPARROW-AFTERBURNER`,
`QA-P2-ELEMENT-SCALING-REGRESSION`).

| System changed | Verified how | Still needs a human |
|---|---|---|
| The ten migrated multipliers (endpoints + floors) | **Measured off the shipped assets** at ship time, not transcribed — one had drifted (the Urchin's Charge reach is 2.5; a doc said 2.0) | Spot-check four in play — `QA-P2` |
| `VesselTransformer.CurrentBoostAmount` (the fleet-wide read removed) | Read-and-grep: the two hulls that used it author `BoostSpeedMultiplier` on their own prefab (Manta ×1.3/floor 0.7, Sparrow ×1.5/floor 0.5, both verified in prefab YAML in `ElementalFloat`'s exact declaration order); the other six author nothing and take the C# initializer, which is disabled → ×1. `EvaluateLive(null)` returns `Value`, so a pre-initialization read cannot divide by anything | `QA-P1-MANTA-SOAR-SPARROW-AFTERBURNER` — this is the one part that had to be hand-authored into prefab YAML, so "did it deserialize" is a real question |
| Rhino ramp ceiling ÷2.5, Serpent boost speed ÷1.6 (the two undeclared second applications removed) | Arithmetic only. Both were real behaviour changes and are **deliberately flagged, not preserved** — the user's call | `QA-P1-RHINO-RAMP-CEILING`, `QA-P1-SERPENT-BOOST-SPEED` — **balance, playtest required** |
| Three `ElementalFloat`s flipped `Enabled: 1 → 0` | **Proved a runtime no-op by reading every reader**: all are `.Value` (a plain field), and nothing outside `ElementalFloat` itself reads `.Enabled`. Turning any of them ON is a balance change and stays a design question | nothing — but see BACKLOG 5.3 before enabling one |
| `ElementalFloatBinder` deleted | Zero code references; its guid (`11e66081df874b8089f6de47d0c4efc3`) appears nowhere under `Assets/`; its one call site was already commented out | nothing |
| `Skimmer`'s bind call removed | `Scale` is the only `ElementalFloat` on `Skimmer` and **both** readers use `EvaluateLive` — re-verified after merging the base, in case it had added a `.Value` reader | nothing |
| `AOERadialBlocks.depthScale` deleted | It had no `[SerializeField]`, so it was unserializable and permanently 1 — the two `*= 1f` lines were no-ops. The compounding hazard is real and now proven: both targets are `[SerializeField]` INSTANCE fields on a pooled component that `Initialize` mutated with `*=`, and nothing resets them | nothing |
| `ResourceSystem.GetLevel` (reported off-by-one) | **REFUTED by running real C#** — float32, not double: `0.7f × 10` rounds to exactly `7f`, and `+= 0.1f` drifts upward. 26/26 cases land on their integer, locked as a standing test | nothing |
| `element_ability_table.py` (7 false gaps fixed) | Run over the whole fleet; disagreements 12 → 5 → **3** after the merge, and every remaining one is an open design slot | nothing |
| `check_elemental_floats.py` (the new gate) | `--self-test` PASS with **four** negative controls, plus a live negative control run in both directions on the real tree (3 findings/exit 1 dirty, OK/exit 0 restored). Reports how many blocks it scanned, because its first version computed `ROOT` one level too high and reported OK over nothing | nothing |
| Doc + asset claims about the retired channel | Blast-radius sweep: 19 sites outside `Docs/ElementalAbilitySystem/` corrected; every surviving mention is a record of the removal. `Docs/prompts/RHINO_ABILITY_MAP_PROMPT.md` instructed a future session to author the deleted field | nothing |
| Everything else on the merged tree | Six out-of-editor gates green (`check_conditional_compilation`, `check_enum_member_references`, `check_switch_label_collisions`, `check_using_directives`, `check_self_referential_locals --all`, `check_elemental_floats`); `regatta_balance.py --check` exit 0 with the intensity-1 spread at **5.99×** under its asserted 6.1×, unchanged by the merge; `author_manta_kit_assets.py --check` clean and idempotent | a player build — none of the above is a compile |

**Topology note (it matters for the two prefab rows):** element levels are simulated on the OWNING
machine and never replicate, so `BoostSpeedMultiplier` is read per-machine for its own vessel. There
is nothing here that behaves differently across two real machines than it does in one process, so
MPPM is not a weaker test for this branch than a two-machine session would be — the untested axis is
the editor, not the network.

---

# Third pass — 2026-09-20: the stragglers (BACKLOG 5.4b / 5.4c / 5.7 / 5.11a)

The unification left four rows open. Three were closed here; all three turned out to be a
**different shape than the row that described them**, and that is the finding worth carrying
more than any of the individual fixes:

> **A backlog row is a hypothesis written at the moment you stopped looking.** Re-measure it
> before you plan against it. Here, one row's live surface shrank from six fields to one, one
> row's "behaviour-neutral" claim was false for the only field that mattered, one row's
> "needs the editor" was wrong about which half was dangerous, and one row's hazard was
> **dead code with a live twin of the same name.**

## 5.11a — the dangerous copy was uncalled, and its twin is a different class

`GrowSkimmerActionSO.ApplyMaxSizeDebuff` did exactly what `ARCHITECTURE.md §2(a)` and the
vessel contract name as their cautionary tale: save `maxSize.Value`, multiply, `await`, write
it back — on a **shared** ScriptableObject. Measured: **nothing calls it.** The one live caller
of a method by that name, `VesselChangeSkimmerSizeByProjectileEffectSO`, holds a
`ShieldSkimmerScaleConfigSO` — a different class whose version writes a private runtime
`_maxScaleMultiplier` and never touches a serialized field.

**Two methods, one name, and only the uncalled one was dangerous.** Grepping the METHOD name
found the hazard; only resolving the CALLER'S TYPE said which one was live.

What survives is milder and already self-documented (*"if multiple skimmers share it, they
share the debuff too"*) and is **5.11b**, deliberately not fixed: before moving that latch
per-vessel, establish whether the debuff reaches anything at all — the config's `prismMaxScale`
is tooltipped *"the driver no longer reads it"*, which is a playtest, not a read.

## 5.4b — the write channel had to exist before the read could move

Of six `ElementalFloat` fields on the pre-`R_` `VesselActions/` generation, **four sit on
components nothing references** (5.4c) and one is authored `Enabled: 0` on both hulls that
carry it. The live surface was one field: `FireGunAction.ProjectileTime`, on the Urchin's two
guns, `Enabled: 1`, 4 → 8 on **Space**.

That field was also `EnergizeAction`'s **override channel** — `ProjectileTime.Value = x` on
start, restore on stop. Converting the read to `EvaluateLive` would have made the write a
no-op and switched the Urchin's energize off silently. So the write got a channel of its own
first: a FLOOR, composed as `Mathf.Max(element, floor)`.

> **When a value is read by one system and WRITTEN by another, a refactor of the read is a
> refactor of the write.** The write site does not appear in a grep for the read, and the
> failure is a feature that stops working with nothing in the console.

The floor also removed three defects the write-and-restore shape carried, none of which was
the point of the change: a level change mid-energize made `ScaleValueWithLevel` overwrite the
raise and drop it; the restored "default" was captured from `fireActions[0]` and written to
**every** gun; and that default was the pre-scaling authored `Value` (5), so a restore replaced
the element-scaled lifetime with a constant until the next level event. A floor cannot express
any of the three.

## 5.7 — a gate can catch a dishonest VALUE; only the type can fix a dishonest TYPE

Nine `ElementalFloat`s on ScriptableObjects, all read as `.Value`, all therefore unable to
scale, are now plain `float`s. `check_elemental_floats.py` already failed the build on the
dangerous case — an authored ramp that never runs — and structurally cannot see the misleading
type, which is what made the gate necessary in the first place.

Two things the row was wrong about:

- **Four of the nine exposed the whole `ElementalFloat` as a public property**, so this was an
  API change, not a field rename. `FullAutoActionSO.SpeedValue` had zero consumers and is
  deleted.
- **It did not need the editor.** The hazard is the serialized DATA: changing the type turns
  the YAML from a mapping into a scalar, and Unity applies only the keys a file carries, so a
  botched block falls back to the C# initializer (rule 4-i). On `GrowSkimmerActionSO.maxSize`
  that is 3 instead of 120 — a 40× blade-length change with nothing to report it. Every block
  was rewritten in the same commit, each initializer set to its asset's **authored** value
  rather than a tidy round number, and the migration asserted the old `Value` against the new
  scalar before writing.

## Verification status (2026-09-20)

Same rule as the second pass: **there is no compiler and no CI in the environment this was
written in**, so no row says "compiles".

| System changed | Verified how | Still needs a human |
|---|---|---|
| `GrowSkimmerActionSO.ApplyMaxSizeDebuff` deleted | Grep of the method name across `Assets` — one declaration, zero call sites; the one caller of that name resolves to `ShieldSkimmerScaleConfigSO` by its `[SerializeField]` type | nothing |
| `FireGunAction` output floors + `EnergizeAction` | Read-and-grep: `EnergizeAction` was the only external writer of that gun's `Speed`/`Energy`/`ProjectileTime` (`ToggleProjectileActionWrapper` writes `FullAutoAction`, a different class with plain floats). Arithmetically identical at the authored numbers — raise-to-6 against a 4 → 8 ramp is `Mathf.Max(scaled, 6)` either way, and both Urchin guns author the same Speed 80 / Energy 1 | **Fire the Urchin's guns, energize, confirm the shot lifetime lengthens and returns** — this is the only real playtest on the branch |
| `FullAutoAction.speed` → `EvaluateLive` | Authored `Enabled: 0` on Falcon and Shrike (read out of both prefabs), and `EvaluateLive` returns `Value` verbatim when disabled — a no-op by construction | nothing |
| Nine SO-hosted `ElementalFloat`s → `float` | The asset diff was reviewed line by line: every new scalar equals the block's old `Value`, and the other `ElementalFloat` blocks in the same assets (`massMaxSizeMultiplier`, `timeDurationMultiplier`, both live through `EvaluateLive`) are untouched. The migration script asserted the equality before writing. All four public-property consumers enumerated and migrated | Open the six assets once and confirm the inspector renders a float field with the same number |
| Everything changed, as C# | **All 13 changed files parse** under Roslyn 9.0 (per-user SDK, `-langversion:9.0`): 0 syntax errors, and every diagnostic is an unresolved type (`CS0518`/`CS0246`/`CS0234`) because there are no Unity DLLs in this container. **This is a parse, not a type check of the assembly** | a player build |
| The standing gates | `check_elemental_floats.py` OK, **20 blocks → 11**, `--self-test` PASS; `check_using_directives`, `check_enum_member_references`, `check_switch_label_collisions`, `check_conditional_compilation`, `check_self_referential_locals --all` all OK | nothing |
| Five dead components (5.4c) | Guid sweep per `.meta`: zero asset referrers each; only intra-cluster C# references plus one `[Tooltip]` string | **a delete/keep verdict** — not taken here |
