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
| `SquirrelHUDVariant.prefab` | gauge/cooldown/impact re-bound; drift + overheat keys dropped. **Second pass:** the row re-bound to the Images that already carry the right ART, the retired drift placeholder re-pointed at `objective_joust.png`, skimming moved to `coreAbilities` |
| `CoreAbility.cs` *(new)* | the key of a non-elemental lockup card; `Skim` is its one member |
| `VesselHUDView.cs` | `CoreAbilityBinding` + `coreAbilities` + `CoreAbilityDisplayOrder`; `TryGetCoreAbility{Icon,Gauge}`; `SetCoreAbility{Cooldown,Pressed,Control}`; the row validator now walks the core cards first |
| `AbilityLockupView.cs` | core cards: `_coreSlots`, a signed slot index, `BuildSlot(… Element flowerElement)` where `Element.None` means *no element cell*, and the four element-keyed internals refactored to slot-keyed so both kinds share one body |
| `AbilityLockupAuditor.cs` | reports a vessel's core cards (a REPORT — an absent core card is an absence of a claim, not a defect) |
| `SquirrelVesselHUDView.cs` | drift + overheat retired (428 → 246 lines); impact rest scale re-anchored to Charge; `SetTubeCooldownReady` → `Element.Mass` |
| `SquirrelVesselHUDController.cs` | drift juice + its three subscriptions removed |

## Second pass (same day): the artwork, and the first non-elemental card

**The first pass moved the METERS and not the ART, and those are different fields.** Re-binding
`abilityIcons[i].icon` points a card at a different `Image`; the ability's picture is that Image's own
`m_Sprite`, which moves with neither the binding nor the gauge. So the gauges and the cooldown veil
landed on their new cards and every card went on showing the previous ability's icon — reported as
*"the ability icons did not move but the energy fill effect and cooldown indicators did"*, which is
exactly what happened. Measured off the prefab, the Mass card was still drawing
`DriftIcon-PLACEHOLDER`, i.e. the art of the one ability the re-cut had retired.

The fix re-binds each slot to the Image that **already carries the right art** rather than re-pointing
sprites, so exactly ONE sprite changed:

| card | host | Image | art |
|---|---|---|---|
| core `Skim` | `OverheatButton` | `Icon` | `New_Sparrow/boost icon.png` — unchanged |
| Charge (Joust) | `DriftButton` | `DriftIcon` | drift placeholder → **`ObjectiveIcons/objective_joust.png`** |
| Mass (Boost Ring) | `ShieldRingsButton` | `ShieldRingsIcon` | `Squirrel/BoostRingCrossSectionIcon.png` — unchanged |
| Space (Steal) | `DangerRingsButton` | `DangerRingsIcon` | `HuntIcon-PLACEHOLDER.png` — unchanged |
| Time (Skimming) | — | *(none)* | — |

`objective_joust.png` is the project's only drawing of a joust. It is an objective icon rather than
purpose-made HUD art, which is honest for a slot whose art pass has not happened; `StealIcon-PLACEHOLDER`
exists and was deliberately **not** swapped in over `HuntIcon` — that is a look call nobody asked for.

**Skimming then left the elemental row.** It is the hull's engine — no button, no cooldown, always
available, and what banks the boost energy every other Squirrel ability spends — so it is bound as
`CoreAbility.Skim` and the lockup draws it as an ability plate with **no element flower above it**,
one card pitch left of Charge, carrying `boostFill` as its gauge. That capability is the general one
asked for, not a Squirrel special case: `Docs/ABILITY_LOCKUP.md` § "Non-elemental cards" has the
contract, and every other vessel binds none and emits nothing.

Layout, measured on the shipped style (pitch 116, plate 104): the core card's ability plate sits at
host Y **+0.0** and its control chip at **−62.0**, both identical to the four elemental cards, and it
spans x `[−568, −464]` against Charge's `[−452, −348]` — one pitch left, same 12 px gap.

⚠ **Stated cost: the TIME card now binds no icon and renders LOCKED.** Time still scales skim energy
and still carries "Live Wire", so the flower above that card is doing real work while the plate below
it reads as an ability that does not exist yet — and because `SetUpgraded` early-returns on a locked
slot, **the Live Wire upgrade draws nothing on the card**. The two honest resolutions are an ability
of Time's own, or a third card state meaning *this element upgrades a core ability*. Both are design
calls, so neither is invented here. This is the one unfinished thing in the re-cut's HUD half.

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
   (expect Squirrel **3** bound slots, in order, uniform — Time unbound is deliberate),
   **Audit Ability Lockups** (expect a `core card 'Skim'` line naming `Icon` and `OverheatCounter`),
   **Audit Vessel Skimmers**.
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
6. **Skimming on Time** — raise Time and confirm skim energy per prism rises. At Time 5 confirm
   danger prisms pay 10×; **below** Time 5 confirm they pay base (this is the half that regresses
   silently if `dangerBonusElement` did not land).
6b. **The non-elemental card** — confirm a FIFTH card sits one pitch LEFT of Charge with **no flower
   above it**, that its plate and control chip line up with the four elemental plates, that the boost
   fill rises through IT rather than through the Time card, and that the four element flowers still
   sit over charge / mass / space / time in that order. Then confirm the **Time** card draws as
   locked (quiet plate, hairline mark, no gauge track).
6c. **The artwork** — read the row left to right and confirm: skim, joust, boost ring, steal,
   locked. If a card shows a neighbour's icon, the BINDING moved and the sprite did not — the defect
   this pass exists to fix.
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
- **The Time card** — the design call above. Until it is made, one element's upgrade is invisible.
- **Joust art** — the Charge card borrows an objective icon; purpose-made HUD art would replace it.
- The Squirrel's `Input: 11` ability (Boost Ring) still lays **danger** prisms; unchanged here.
