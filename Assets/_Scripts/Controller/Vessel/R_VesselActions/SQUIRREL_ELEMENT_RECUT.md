# Squirrel element re-cut (2026-09-24)

Every one of the Squirrel's four element rows moved. The organising idea is that each element now
owns the thing it is **named** for on this hull:

> **Mass creates mass. Time makes you faster. Charge is the threat you carry into a lifeform.
> Space is how far your steal reaches.**

| Element | Ability | Input | Scaling | L5 |
|---|---|---|---|---|
| **Charge** | Crystal Joust | passive | **none — a deliberate hole** | **Shepherd** *(moved from Space)* |
| **Mass** | Boost Ring *(moved from Time)* | RT | cooldown ×1 → ×0.5 | **Twin Rings** |
| **Space** | Steal | passive | skimmer sphere 15 → 30 *(unchanged)* | **Iron Grip** *(new)* |
| **Time** | Skimming *(moved from Charge)* | passive | skim energy ×1 → ×2 | **Live Wire** |

Mass's old row — trail VOLUME and the `Heavy Trail` L5 — is **retired to base**, not moved.

## The Charge hole is deliberate

`element_ability_table.py Squirrel` reports **`NO SCALING — this element changes no number`** on the
Charge row and `scaling wired 3/4` on the header. **That report is correct.** The joust has no
elemental parameter yet; it is deferred to the branch that reworks the joust, and the row was left
empty rather than given a placeholder. Do not fill it to green the tool — the design-approval gate
(`/vessel` §3) applies to inventing a parameter exactly as it applies to inventing an ability.

## Files

| File | Change |
|---|---|
| `Resources/ElementalAbilityMaps/Squirrel.asset` | all four entries rewritten (labels, inputs, prose, upgrades) |
| `SquirrelTubeActionExecutor.cs` | `Element.Time` → `Element.Mass` at the cooldown scale AND the `IsUpgradeActive` gate |
| `SquirrelTubeActionSO.cs` | `cooldownMultiplierAtFullTime` → `…AtFullMass` (`[FormerlySerializedAs]`) |
| `SkimmerBoostPrismEffect.cs` | `chargeEnergyMultiplier` → `energyMultiplier` (`[FormerlySerializedAs]`); new authored `dangerBonusElement` replaces a hardcoded `Element.Charge` |
| `SkimmerBoostPrismEffect.asset` | `element: 1 → 4`, `dangerBonusElement: 4` |
| `SkimmerStealPrismEffectSO.cs` | new `superStealEnabled` + `superStealElement`, gated on `IsUpgradeActive` |
| `SkimmerStealPrismEffect.asset` | `superStealEnabled: 1`, `superStealElement: 3` |
| `PrismEffectHelper.cs` | `Steal` gains an optional `superSteal` (the other two callers are unchanged) |
| `SquirrelVesselWitherLifeformByCrystalEffect.asset` | `allyUpgradeElement: 3 → 1` |
| `VesselPrismController.cs` | `massUpgradeShieldsTrail` → `driftShieldsTrail` (`[FormerlySerializedAs]`); the drift branch loses its `IsUpgradeActive(Element.Mass)` term |
| `Squirrel.prefab` | `trailVolume` → `Enabled: 0, Value: 1.35`; `driftShieldsTrail: 1`; three drift SOAP refs dropped |
| `SquirrelHUDVariant.prefab` | gauge/cooldown/impact re-bound; drift + overheat keys dropped |
| `SquirrelVesselHUDView.cs` | drift + overheat retired (428 → 246 lines); impact rest scale re-anchored to Charge; `SetTubeCooldownReady` → `Element.Mass` |
| `SquirrelVesselHUDController.cs` | drift juice + its three subscriptions removed |

## Findings worth more than the change

**1. `superSteal` already existed and nobody passed it.** `PrismTeamManager.Steal`'s third parameter
has been in the tree the whole time, and its two arms are exactly the base effect and the requested
upgrade: `!superSteal && IsShielded` sheds the shield and returns, while the flip that follows never
clears `IsShielded`. So Iron Grip is ~6 lines, not a new mechanic. Super-shielded mass is refused at
every level — breaking that stays an opt-in mechanic (the Rhino's energised blade, the Serpent's
Pierce). *Before designing a capability, grep for the parameter nobody passes.*

**2. Both effect assets were already Squirrel-only, measured.** `SkimmerBoostPrismEffect.asset` and
`SkimmerStealPrismEffect.asset` are each referenced by exactly ONE container
(`SquirrelSkimmerImpactorDataContainer`), so `/vessel` rule 8 (fork a shared effect SO before
changing it) was satisfied by measurement rather than by a fork. Both new element fields still
default to the previous behaviour, so a second vessel adopting either asset type is unchanged.

**3. A moved ability drags its HUD gauge with it — and on this hull EVERY icon carried a second
binding.** `boostFill` Charge → **Time**, `tubeCooldownIcon` Time → **Mass**, `impactIcon` Space →
**Charge**. The cooldown one is the trap: `SetAbilityCooldown` is addressed by ELEMENT, so
`SetTubeCooldownReady` still calling `Element.Time` would have drawn the ring's recharge veil over
the *skimming* card, correctly, forever, with nothing to report it.

**4. The trail size needed no code.** `ElementalFloat.EvaluateLive` is `if (!Enabled) return Value;`
— so `Enabled: 0, Value: 1.35` is a fixed multiplier authored entirely in the prefab. The field
stays live and un-renamed because the **Manta** still maps it to Mass at 1 → 2.5.

**5. Two bindings were retired rather than re-homed, and one of them had been a lie for months.**
`overheatIcon`'s drivers (`SetOverheatHeat`, `JuiceOverheatEngaged`, `JuiceOverheatRecovered`) have
had **no callers** since the Sparrow's overheat mechanic was deleted — a gauge whose meter is gone
(`/vessel` rule 15). `driftButtonIcon` was live, but it was sitting on the card the Boost Ring now
occupies, and the drift is core flight with no element.

## ⚠ Stated cost: the Squirrel has no drift readout on the HUD

The drift sprite/lean was the hull's only drift HUD feedback, and it is gone. The drift itself is
unchanged and the hull visibly drifts, so this is a readout decision rather than a mechanic one.

**`ElementalBarsView.JuiceDriftStart` / `JuiceDriftEnd` already exist, are fully written, and are
ALSO dead** (no callers anywhere in the tree) — they juice the petal flowers rather than an ability
icon, which is where a drift readout belongs now that all four icons mean their abilities. Restoring
drift feedback is one line in `SquirrelVesselHUDController` pointing at those instead. Not done here
because it is a separate design call.

## Also worth knowing

`LT` is no longer claimed by any map row (Trail Volume held `Input: 12`, which was really the drift's
trigger). That is honest — the drift is not an element ability — but it means the Squirrel's control
chips no longer name LT anywhere.

## In-editor verification

Nothing below has been run; there is no Unity in this session.

1. **Auditors** (asset-only, no play mode): *FrogletTools > Vessels >* **Audit Vessel Ability Rows**
   (expect Squirrel 4/4, in order, uniform), **Audit Ability Lockups**, **Audit Vessel Skimmers**.
2. **Reader**: `python3 Tools/Build/element_ability_table.py Squirrel` — expect
   `abilities 4/4  scaling wired 3/4  L5 gates wired 4/4`, with the single disagreement being the
   Charge `NO SCALING` hole.
3. **Trail size** — fly the Squirrel and confirm the ribbon reads well at the fixed 1.35. This is
   the one number chosen by eye rather than measured; it is `Squirrel.prefab` →
   `VesselPrismController.trailVolume.Value`. It no longer changes with Mass at all.
4. **Shield at base** — at Mass 0, drift and confirm the laid prisms arrive **shielded**
   (octahedra). Straight-line trail must stay unshielded.
5. **Boost Ring on Mass** — RT deploys as before; raise Mass and confirm the cooldown shortens and
   the recharge veil now sweeps the **Mass** card. At Mass 5 confirm two rings.
6. **Skimming on Time** — raise Time and confirm skim energy per prism rises; the boost fill is now
   the **Time** card's gauge. At Time 5 confirm danger prisms pay 10×; **below** Time 5 confirm they
   pay base (this is the half that regresses silently if `dangerBonusElement` did not land).
7. **Iron Grip** — skim an opposing **shielded** prism below Space 5: it should lose its shield and
   keep its domain. At Space 5: it should change domain **and keep the shield**. Then confirm a
   **super**-shielded prism is refused at both levels.
8. **Shepherd on Charge** — joust an own-domain lifeform's heart below Charge 5 (nothing) and at
   Charge 5 (nourished). Confirm Space 5 no longer does this.
9. **MPPM two-client** — element unlock bits are replicated (`NetElementUnlocks`), and Iron Grip
   changes who OWNS a prism, so confirm both peers agree on the stolen prism's domain and shield.
10. **Migration check** — open `Squirrel.prefab` and `SkimmerBoostPrismEffect.asset` and confirm the
    `[FormerlySerializedAs]` renames carried their values (cooldown 0.5, energy max 2, drift shield
    flag on). A silently-defaulted rename is `/vessel` rule 4-i's failure mode.

## Follow-ups

- **Charge scaling** — the hole, waiting on the joust branch.
- **Drift readout** — wire `ElementalBarsView.JuiceDriftStart/End`, or decide the hull is enough.
- The Squirrel's `Input: 11` ability (Boost Ring) still lays **danger** prisms; unchanged here.
