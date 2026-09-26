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
| `SquirrelHUDVariant.prefab` | gauge/cooldown/impact re-bound; drift + overheat keys dropped. **Second pass:** the row re-bound to the Images that already carry the right ART, the retired drift placeholder re-pointed at `objective_joust.png`, skimming moved to `coreAbilities`. **Third pass:** skimming came back to TIME with its gauge, the core card became the DRIFT (its sprite restored, `input: 2`), Charge took the skull, and Space unbound so the view can generate it |
| `CoreAbility.cs` *(new)* | the key of a non-elemental lockup card; `Drift` is its one member |
| `VesselHUDView.cs` | `CoreAbilityBinding` (+ its own `input`) + `coreAbilities` + `CoreAbilityDisplayOrder`; `TryGetCoreAbility{Icon,Gauge}`; `SetCoreAbility{Cooldown,Pressed,Control}`; `SeedCoreAbilityControls`; `EnsureGeneratedAbilityIcons` / `BindGeneratedAbilityIcon`; the row validator now walks the core cards first |
| `AbilityLockupView.cs` | core cards: `_coreSlots`, a signed slot index, `BuildSlot(… Element flowerElement)` where `Element.None` means *no element cell*, and the four element-keyed internals refactored to slot-keyed so both kinds share one body. **Third pass:** `Build` calls the generated-icon hook, and `NormaliseIcon` writes the icon's kerning scale |
| `AbilityLockupAuditor.cs` | reports a vessel's core cards, and names the elemental slots that bind no AUTHORED icon (undesigned or generated — the asset cannot tell those apart) |
| `PerspectiveTunnelGraphic.cs` *(new)* | the Mass card's accent: a wall that fades as it converges, two rings at true perspective radii, and a core at the vanishing point. Its `Profile` is a parameter struct and never a serialized field — a missing struct key deserializes as all zeros where a missing float keeps its C# initializer |
| `SO_ColorSet.cs` | `GetDangerSignalColor()` — the third `*SignalColor` sibling. HDR-normalised, alpha forced to 1, alpha 0 when the palette authors none |
| `ThemeManagerDataContainerSO.cs` | the null-safe wrapper for it, beside the two that were already there |
| `Tools/Build/check_squirrel_card_fit.py` *(new)* | measures both generated cards against the sprite's own alpha, the font's own advance table and the shared style asset; 6 negative controls |
| `Tools/Build/check_using_directives.py` | reads with `utf-8-sig` — `\ufeff` is not whitespace, so the gate could not see the FIRST using in any of the 74 BOM'd files and reported it missing |
| `SquirrelVesselHUDView.cs` | drift + overheat retired (428 → 246 lines); impact rest scale re-anchored to Charge; `SetTubeCooldownReady` → `Element.Mass`. **Third pass:** builds the Space card (`EnsureGeneratedAbilityIcons`), `SetStealReach01` / `SetStealCount`. **Fourth pass:** also builds the Mass card's tunnel accent, `SetDangerTint` / `PaintBoostRing` / `PaintBoostRingTunnel`, and the steal count moved below the ring's maximum at a 4-digit-safe size |
| `SquirrelVesselHUDController.cs` | drift juice + its three subscriptions removed; **third pass:** `PushStealReadout` polls the skimmer's reach and `RoundStats.PrismStolen` |
| `Skimmer.cs` | new `ElementalScale01` — the live reach as a fraction of this skimmer's own authored range |

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

⚠ That pass left the TIME card unbound and therefore LOCKED, which the **third pass below
resolves**: Time keeps skimming, and it now has the skim icon to say so.

## Third pass (2026-09-25): the row the design actually wanted

Five changes, from one playtest read of the second pass.

| card | host | Image | art |
|---|---|---|---|
| core `Drift` | `DriftButton` | `DriftIcon` | `{PLACEHOLDERS}/Icons/DriftIcon-PLACEHOLDER.png` — restored |
| Charge (Joust) | `DangerRingsButton` | `DangerRingsIcon` | `{PLACEHOLDERS}/Icons/HuntIcon-PLACEHOLDER.png` — the SKULL, already there |
| Mass (Boost Ring) | `ShieldRingsButton` | `ShieldRingsIcon` | `Squirrel/BoostRingCrossSectionIcon.png` — unchanged |
| Space (Steal) | *generated* | *generated* | none — a ring + a number, drawn |
| Time (Skimming) | `OverheatButton` | `Icon` | `New_Sparrow/boost icon.png` (+ `OverheatCounter` as the gauge) |

**1. Time keeps skimming — the icon as well as the gauge.** The second pass moved skimming onto a
non-elemental card on the grounds that it is the hull's engine, which is true and was the wrong
call: skimming is what Time *scales* (1 → 2 skim energy) and what Live Wire upgrades, so a Time
flower over a locked plate was a flower doing real work above an ability that did not exist. Time
now carries the skim icon and `boostFill`, and the row is 4/4 again.

**2. The core card is the DRIFT.** The drift is the thing on this hull that genuinely has no
element: core flight, on LT, upgraded by nothing, and the *reason* its icon was retired in the first
pass was that it was squatting on the card the Boost Ring wanted. A card with no flower above it is
exactly the place for it. `CoreAbility.Skim` → `CoreAbility.Drift` (value 1 kept, so the prefab's
`ability: 1` is untouched).

**3. The chip comes from the BINDING.** `CoreAbilityBinding.input` is new, authored `2`
(`LeftStickAction`) here, and `VesselHUDController.SeedAbilityControls` pushes the core ones before
the elemental ones. An elemental card takes its control from its `ElementalAbilityMapSO` entry
because that is where an elemental ability's input lives; a core ability has no map entry, so the
binding names it. Both are still one authored fact with the glyph derived from it, so a wrong label
stays structurally impossible. No clash: Mass is the only elemental row with an input (RT).

**4. Charge shows the skull.** `HuntIcon-PLACEHOLDER.png` is a skull and was already the Space
card's sprite; the joust is the Charge ability, so the card binds *that* Image. `objective_joust.png`
(crossed lances), swapped in by the second pass, is reverted off `DriftIcon` in the same edit — the
drift needs its own art back.

**5. Space is GENERATED, and that is the interesting one.** Space scales the skimmer 15 → 30 on this
hull, and the steal reaches exactly as far as the skimmer does, so the honest readout of "what does
Space do for me" is the reach itself — measured, not illustrated. `SquirrelVesselHUDView` builds a
`ScopeRingGraphic` whose **radius IS that live measurement** with the running total of prisms stolen
inside it, hung off a deliberately-invisible bound `Image` so the card is not LOCKED and the lockup
still has something to kern. The Rhino's `skimmerSizeIcon` (a rect lerped between `minIconSize` and
`maxIconSize`) is the precedent; this is that idea inside a lockup card and reading from the element
rather than from a bespoke setter.

Three details of it are decisions rather than defaults. The ring **eases** toward its target, because
an element level moves in steps and a ring that stepped with it reads as a glitch rather than as a
measurement. The count is tinted in the pilot's **own domain colour**, which is not decoration — a
stolen prism *changes hands to that domain*, so the number is counting mass that now wears that
colour; the ring stays white because it measures the skimmer, which belongs to nobody. And both are
**polled** in the controller's existing `Update`: the reach is a continuous function of an element
level nothing raises an event for, and the count lives on a server-write `NetworkVariable`
(`RoundStats.n_PrismStolen`), so the owner of a steal learns about its own steal by reading it back.

### The kerning bug the core card exposed

The second pass's core card drew **a third larger than its four neighbours**, and the cause
generalises. An icon's drawn size is its authored `sizeDelta` times the lockup's kerning scale, and
that scale was written in exactly one place: `VesselHUDView.AbilityIconRestScale`, applied by
`SetAbilityUpgraded`, which `VesselHUDController` seeds **for every ELEMENT**. So every elemental
icon was kerned a moment after the row was built, and the one card no element seeds never was — it
drew at its authored 80 in a cell sized for 60. Measured: all four elemental icons are authored
80×80 at scale 1, so the authored data was uniform and only the *application* was not, which is why
it could not be found by reading the prefab.

`AbilityLockupView.NormaliseIcon` now writes the content scale as it centres an icon, so the row is
correct the instant it is laid out and the seeding pass writes the identical value (nothing is
upgraded at build time, so the rest scale IS the content scale). General rule: **a value applied
only by an event is missing on everything that event does not reach**, and it shows up on whichever
card is outside the loop rather than as an error.

### A vessel may generate an icon

`VesselHUDView.EnsureGeneratedAbilityIcons()` — virtual, empty by default, called by
`AbilityLockupView.Build()` before the row is laid out — plus `BindGeneratedAbilityIcon(element,
icon)`. An override must be idempotent, must create its host outside the row (the lockup re-homes
it), and **an authored icon always wins**, so a generated readout can never overwrite a prefab's own
art. Every other vessel is byte-for-byte unchanged.

⚠ Stated cost: the **asset** no longer shows the whole row, so *Audit Ability Lockups* now names the
unbound slots and says it cannot tell *undesigned* from *generated* apart — only one of those is in
the prefab. Check the Squirrel in play.

## Fourth pass (2026-09-26): the two generated cards say what they are made of

Two asks, both about a card's *contents* rather than its slot, and both landing in code rather than
in art.

### The Boost Ring reads as DANGER mass with YOUR tunnel through it

A Boost Ring is made of danger prisms in the pilot's own domain — and the danger tier is exactly
that composition: a domain-independent hot rim over the domain's shielded base
(`SO_ColorSet.GetPrismKindColors`, `Docs/PALETTE.md` § "The danger tier borrows the shielded base").
So the card now says both halves: the icon wears the **danger rim**, and a generated accent inside
it wears the **team**.

They are **SEPARATED, never blended**, which is what `Docs/PALETTE.md §4.3` prescribes for two
saturated hues. That is possible because of what the sprite actually is — not a solid annulus but a
ring of **eight prism blocks**, measured to start at r **0.520** of its own box, with the middle
completely empty. The accent lives entirely in that hole and the two colours never touch a pixel.

The accent is a **one-point-perspective tunnel with its vanishing point at the icon's centre**
(`PerspectiveTunnelGraphic`), i.e. what the pilot sees flying at their own ring. Radially symmetric,
so it cannot imply a direction the ability does not have.

**The shape is a judged result, not a derivation.** Ten candidates were rendered at the size this is
read at — the card is 104x88 and the icon is drawn at 60 — and the failures are the useful part:

| candidate | why it lost |
|---|---|
| haze brightest at the CENTRE + 3 rings | a filled teal blob with a hole in it; no depth at all |
| rings alone (2 or 3) | a TARGET. Concentric circles are a bullseye unless something converges |
| 3 rings + wall + core | the third ring crowds the core into mush at 60px |
| front radius 18 | crowds the blocks on **GOLD**, where warm-on-warm gets the least help from hue |
| front radius 15 | the tunnel stops reading as part of the ring around it |

What ships is **front radius 17, two rings, a wall, and a core**, and it needs all four:

- the **WALL** fades as it converges (brightest where it is nearest) — this is the foreshortening,
  and it is the single thing that separates a tunnel from a target;
- **TWO rings** at true perspective radii `frontRadius / (1 + k·depthStep)`, with **thickness
  projected by the same factor** — a ring further away is thinner as well as smaller, and dropping
  that is what makes a stack of rings look flat;
- a small bright **CORE** at the vanishing point — the light at the end, and what makes the middle
  read as somewhere the tunnel *goes* rather than as a hole.

Verified by rendering the shipped numbers in all three domains through the sprite's own alpha, in
linear light, gamma-encoded — Jade and Ruby are crisp, Gold works with the extra air that 17 buys.

### The steal count is planned for four digits and moved out of the ring's way

It sat centred *inside* the reach ring, where it competed with the ring for the same few pixels
**exactly when the ring was smallest** — which is its resting state, i.e. most of a match. It now
hangs **below the ring at its MAXIMUM radius**, so the two cannot overlap at any Space level.

Sized by measurement rather than by eye: Aldrich's widest digit advances **49.641** at its 68pt
atlas, so at **20pt** a `0000` is **58.4** of the icon's 80-unit box (73%) and even five digits fit
at 91%. It is **fixed, not auto-sized** — a number that shrinks as it ticks over reads as a glitch —
and wrapping is off with overflow on, because an overflowing number is a loud fixable fault where a
wrapped or truncated one is a wrong reading that looks deliberate.

It deliberately overhangs the icon's 80-unit rect into the ability plate's own lower margin, which
is empty and unmasked (the lockup's two `Mask`s are the gauge clip and the cooldown veil, neither of
them an ancestor of the icon). At the shipped numbers its bottom lands **4.25 drawn px** clear of
the plate's bottom edge, above the control chip.

### `Tools/Build/check_squirrel_card_fit.py`

Both readouts are laid out in the ICON's own units while being DRAWN at the lockup's kerning, so
every number in them is a relationship between **three files that do not know about each other** —
the view's C#, the shared style asset, and the sprite or the font. The gate measures all four
relationships from the files themselves (the sprite's hole off the PNG, the digit advance off the
font's own glyph table, the kerning off the prefab's rect and the style asset) and carries **six
negative controls**, each naming the check it must trip.

It also caught its own author: the `ROOT` assert fired on a `dirname` one level too shallow — the
trap `CLAUDE.md` records from the Borromean tool, met again the first time somebody wrote a
`Tools/Build` script from memory.

### Three findings

1. **A colour authored FOR A SHADER is not a colour a UI slot may read** — and this is now three for
   three. `DullCrystalColor` is black (§2.4), `DarkCTA` is a dark olive with alpha 0 (§2.5), and
   `EnvironmentColors.Danger` is **HDR at 1.498 with alpha 0** (§2.6). The shader composes,
   tolerates HDR and ignores alpha; a UI slot does none of those. The alpha is the nastier half:
   a transparent tint is indistinguishable from a tint that never ran. Add a `*SignalColor`
   accessor; never read the field.
2. **A generated accent belongs to the ICON, not to the card** — a child inherits the lockup's
   kerning for free and can only draw where its parent is transparent, which is exactly the
   constraint that keeps the danger tint and the team accent from blending. A sibling would need the
   kerning applied by hand and would be free to cover the art.
3. **A serialized STRUCT and a serialized FLOAT fail differently on a prefab nobody re-saved.**
   Unity applies only the keys a file carries, so a missing float field keeps its C# initializer
   (`/vessel` rule 4-i) — but a struct has no field initializers at all, so a missing struct key
   deserializes as **all zeros**. `PerspectiveTunnelGraphic.Profile` is therefore a plain parameter
   struct and never a serialized field: the authoring surface stays plain floats on the builder.

### ⚠ A gate was lying, and this pass is only how it was found

`check_using_directives.py` reported a missing `using CosmicShore.ScriptableObjects;` on
`ThemeManagerDataContainerSO.cs` — which is on **line 1**, preceded by a UTF-8 **BOM**. `\ufeff` is
Unicode category `Cf`, not whitespace, so `^\s*using` could not match the first using directive in
any BOM'd file: **74 of the project's 1,971 `.cs` files**. Fixed by reading with `utf-8-sig`, with
two cases added to the script's own self-test (one BOM'd file that must stay silent, one that must
still fire).

A false POSITIVE is the worse direction for a gate — it is what teaches people to stop reading it —
and this one had been latent for as long as the gate has existed, surfacing only because an edit
finally pulled a BOM'd file into its changed-file scope. *A gate's blind spots are found by what
you happen to edit, so widen the scope deliberately once in a while.*

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
trigger). That is honest — the drift is not an element ability — and the **third pass** put LT back
on screen where it belongs: on the drift's own non-elemental card, from
`CoreAbilityBinding.input`.

## In-editor verification

Nothing below has been run; there is no Unity in this session.

1. **Auditors** (asset-only, no play mode): *FrogletTools > Vessels >* **Audit Vessel Ability Rows**
   (expect Squirrel **3** bound slots plus a line naming **Space** as binding no authored icon —
   that one is GENERATED, not missing), **Audit Ability Lockups** (expect a `core card 'Drift'` line
   naming `DriftIcon` and `no gauge`), **Audit Vessel Skimmers**.
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
   above it**, wearing the DRIFT icon and an **LT** chip below it, and that its plate and chip line
   up with the four elemental ones. Its icon must be the **same drawn size** as theirs: it was a
   third larger before the kerning fix, so if it looks big again `NormaliseIcon` has stopped writing
   the content scale.
6c. **The artwork** — read the row left to right and confirm: **drift, skull, boost ring, ring +
   number, speed arrows**. If a card shows a neighbour's icon, the BINDING moved and the sprite did
   not — the defect the second pass existed to fix.
6d. **The generated Space card** — it must NOT render locked. Raise Space (crystals) and confirm the
   ring **grows smoothly** rather than stepping; steal prisms and confirm the number climbs in your
   own domain's colour. On a client as well as the host, since the count reads a server-write
   NetworkVariable.
6e. **Skimming back on Time** — the Time card shows the speed-arrow icon and the boost fill rises
   through IT (a linear fill inside that card), while the **Mass** card is the one the cooldown veil
   sweeps.
6f. **The Boost Ring is RED with YOUR tunnel in it** — the eight blocks wear the danger red and the
   middle shows a ring, a dark gap, an inner ring and a bright pip, all in your own domain. Change
   domain at the freestyle toy and confirm the tunnel follows while the blocks stay red. If the
   blocks are WHITE, `SetDangerTint` did not arrive or the palette authors no danger colour; if the
   middle is a filled blob rather than a tunnel, the wall is grading the wrong way.
6g. **The steal count is below the ring and never touches it** — raise Space to full and confirm the
   ring at its biggest still clears the number, and that at rest the small ring leaves the number
   alone. Type a four-digit value into `StealCount` in the hierarchy while playing (or steal that
   many) and confirm it neither wraps nor is clipped, and still clears the control chip below.
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
- The Squirrel's `Input: 11` ability (Boost Ring) still lays **danger** prisms; unchanged here —
  which is now what its card SAYS, rather than something only the code knew.
- **The other 73 BOM'd files** have never been seen by `check_using_directives.py`'s first-line
  rule either. The gate is scoped to changed files, so they will be checked as they are
  touched; a one-off `--all` run would clear the backlog and is not done here.
