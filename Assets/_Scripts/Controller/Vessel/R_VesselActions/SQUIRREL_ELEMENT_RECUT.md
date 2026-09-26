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
| `AbilityLockupView.cs` | core cards: `_coreSlots`, a signed slot index, `BuildSlot(… Element flowerElement)` where `Element.None` means *no element cell*, and the four element-keyed internals refactored to slot-keyed so both kinds share one body. **Third pass:** `Build` calls the generated-icon hook, and `NormaliseIcon` writes the icon's kerning scale. **Sixth pass:** `PlaceHost` re-anchors the host's press juice, because normalising a scale does not reach a component that cached it |
| `AbilityLockupAuditor.cs` | reports a vessel's core cards, and names the elemental slots that bind no AUTHORED icon (undesigned or generated — the asset cannot tell those apart) |
| ~~`PerspectiveTunnelGraphic.cs`~~ | the Mass card's accent — **added in the fourth pass and DELETED in the fifth**, with the team colour it drew. Its one finding outlives it: a `Profile` is a parameter struct and never a serialized field, because a missing struct key deserializes as all zeros where a missing float keeps its C# initializer |
| `SO_ColorSet.cs` | `GetDangerSignalColor()` — the third `*SignalColor` sibling. HDR-normalised, alpha forced to 1, alpha 0 when the palette authors none |
| `ThemeManagerDataContainerSO.cs` | the null-safe wrapper for it, beside the two that were already there |
| `Tools/Build/check_squirrel_card_fit.py` *(new)* | measures both generated cards against the sprite's own alpha, the font's own advance table and the shared style asset; 6 negative controls |
| `Tools/Build/check_using_directives.py` | reads with `utf-8-sig` — `\ufeff` is not whitespace, so the gate could not see the FIRST using in any of the 74 BOM'd files and reported it missing |
| `SquirrelVesselHUDView.cs` | drift + overheat retired (428 → 246 lines); impact rest scale re-anchored to Charge; `SetTubeCooldownReady` → `Element.Mass`. **Third pass:** builds the Space card (`EnsureGeneratedAbilityIcons`), `SetStealReach01` / `SetStealCount`. **Fourth pass:** `SetDangerTint` / `PaintBoostRing`, and the steal count moved below the ring's maximum at a 4-digit-safe size. **Fifth pass:** the tunnel accent removed; `LayOutReachRing` lifts the ring so the whole readout fits the icon's own box |
| `SquirrelVesselHUDController.cs` | drift juice + its three subscriptions removed; **third pass:** `PushStealReadout` polls the skimmer's reach and `RoundStats.PrismStolen` |
| `Skimmer.cs` | new `ElementalScale01` — the live reach as a fraction of this skimmer's own authored range |
| `AbilityButtonPressJuice.cs` | **sixth pass:** captures its rest scale LAZILY instead of in `Awake`, `OnDisable` writes nothing until it has, and `SetRestScale` lets a layout owner re-anchor it. Its `Awake` capture of the prefab's authored 0.7 is what held four of the five cards below the size the style asks for |
| `Tools/Build/check_rest_scale_capture.py` *(new)* | fails any UI component that captures a scale into a field in `Awake` / `OnEnable` / `Start`; 4 negative controls, and it names the pre-fix line |

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

> ⚠ **The tunnel half of this was CUT in the fifth pass below.** The danger tint stays; the
> team-coloured accent and `PerspectiveTunnelGraphic` are gone. The section is kept as the
> retirement record, because what it measured is still true of the sprite and the findings under
> it are what survive.

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

It deliberately overhung the icon's 80-unit rect into the ability plate's own lower margin, which
is empty and unmasked (the lockup's two `Mask`s are the gauge clip and the cooldown veil, neither of
them an ancestor of the icon). ⚠ **That was the defect the fifth pass fixed**: nothing clips a card,
so overhanging it is possible — and it is what made this card read as bigger than the other four.

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
   kerning for free and can only draw where its parent is transparent. A sibling would need the
   kerning applied by hand and would be free to cover the art. (This survives the tunnel's
   retirement: the Space card's ring and count are children of theirs for the same two reasons.)
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

## Fifth pass (2026-09-26): the Mass card says ONE thing, and the Space card fits its box

Two asks from one look at the fourth pass, and they are the same correction from opposite ends: a
card must read at the size of its neighbours, and it must say one thing.

### Mass: keep the danger, lose the team

The team-coloured tunnel shipped in the fourth pass is **removed**, and `PerspectiveTunnelGraphic`
is deleted with it rather than left unreferenced — an unreferenced subsystem is eventually mistaken
for a live feature. The icon keeps its danger tint (`SO_ColorSet.GetDangerSignalColor`,
`Docs/PALETTE.md §2.6`), which is untouched and still the only UI read of the danger rim.

The measurement that let the two hues share the card was right — the sprite is a ring of eight prism
blocks starting at r 0.520 with the middle measured empty, so the accent lived entirely in the hole
and the two never touched a pixel, which is what `Docs/PALETTE.md §4.3` prescribes. **Separated is
what makes two hues legible; it is not what makes a second hue worth having.** On a card drawn at 60
units they still compete for the same glance, and the pilot's domain is already said by the trail
the ring lays, by the element flower above the card, and by the Space card's own count. Danger is
the one fact only this card can state, so it states it alone.

### Space: the readout fits the box an authored icon draws in

Reported as *the Space containers are oversized like the time ones were* — the same complaint as the
un-kerned core card, and **not the same cause**. `NormaliseIcon` reaches this card correctly (the
generated icon is an ordinary bound icon and measures the same 80x80 the four authored ones do); the
kerning was never missing. What was missing is that **the lockup kerns an icon's RECT and cannot see
what a generated child DRAWS inside it**. The first cut put the ring on the icon's centre at a
radius that nearly filled the box and hung the count off the plate *below* it, so the readout spanned
from +35 to -53 in an icon whose own box is ±40 — 10% taller than the plate itself.

The fix is to stop sharing a centre. The ring is **lifted** (`reachRingCenterY` 10) and the count
hangs from the ring's own bottom rather than from the icon's middle, so the two split the box
vertically instead of stacking out of it:

| | before | after |
|---|---|---|
| ring centre | 0 | **+10** |
| ring radius, rest → full | 20 → 34 | **13 → 21** |
| ring top at full (feather in) | +35 | **+32** |
| count top → bottom | −35 → −53 | **−13 → −31** |
| readout span vs the icon's ±40 | **−53 .. +35 (over)** | −31 .. +32 (inside) |

The ring is point-anchored with an offset rather than stretched, because `ScopeRingGraphic` draws
about its own `rect.center` and is **shared with the Serpent's scope reticle** — the lift belongs to
this card's layout, not to the component. The count hangs off `reachRingCenterY` too, so moving the
ring moves the pair and the fitted span survives a retune.

### The gate now measures the thing that was reported

`Tools/Build/check_squirrel_card_fit.py` loses its two tunnel checks and gains the one that matters:
**the whole readout's span against the icon's own box**. Its first negative control is literally the
shipped-before-this-pass numbers, and it fires — so the gate would have caught the report. Six
controls, all firing; it reads the layout from the view, the kerning from the style asset and the
prefab's rect, and the digit advance from the shipped font's own glyph table, and it writes nothing.

### The finding

**A card's SIZE is two separate questions, and the lockup only answers one of them.** The rect is
the lockup's (`NormaliseIcon`, applied at build); what is *drawn inside* the rect is the vessel's,
and a generated readout has no authored art to be wrong against. Both failures present identically —
*this card is bigger than the others* — which is why the report named the wrong one. When a
generated card reads oversized, check its CONTENT's extent before checking its kerning.

## Sixth pass (2026-09-26): the four cards were the wrong ones

The fifth pass fixed a real defect — the Space card's generated content really did overflow the box
an authored icon draws in — and it did **not** fix the report. The next frame came back with the
same sentence, now naming the plates as well: *"both ability and element display are oversized"*.
Content cannot reach a plate, so the second report was about something else.

**Measuring the frame settled it in one step.** At a single scanline through the row, the four
authored cards span 115 px and the Space card 166 px, with a uniform 200 px pitch between card
centres. `115 / 166 = 0.693`. Every Squirrel ability button is authored at `m_LocalScale: 0.7`.

**So Space is the card that is RIGHT.** `AbilityLockupView.PlaceHost` normalises a host to
`localScale 1`; all four authored hosts carry `AbilityButtonPressJuice`, whose `Awake` cached the
prefab's 0.7 — long before the lockup runs — and whose `OnDisable` wrote it back unconditionally,
on every hide of the HUD. The generated `StealReachButton` carries no juice, so it kept the 1.
*Four wrong cards agree with each other, so the one correct card is what looks wrong.*

| | authored hosts | generated Space host |
|---|---|---|
| authored `localScale` | 0.7 | — (created at 1) |
| `PlaceHost` writes | 1 | 1 |
| `AbilityButtonPressJuice` | yes — restores its `Awake` capture | none |
| drawn | **0.7** | **1** |
| measured in the report's frame | 115 px | 166 px (**ratio 0.693**) |

**The fix is two halves and both are needed.** `AbilityButtonPressJuice` captures **lazily**, at the
start of a press, and `OnDisable` writes nothing until it has — so it can only ever restore a scale
it took itself, anywhere it is used, not only here. And `PlaceHost` hands it the new rest outright
(`SetRestScale(Vector3.one)`), so an instance that already captured is corrected without waiting for
a press. `Tools/Build/check_rest_scale_capture.py` (`--check`, `--self-test`) fails any UI component
that captures a scale into a field in `Awake` / `OnEnable` / `Start`; it names the pre-fix line, and
its four negative controls (a local, a WRITE, a comment, the lazy fix) all hold.

Nothing in the prefab changed. The authored 0.7 stays where it is — absorbing it is what `PlaceHost`
is for.

### The finding

**A rest scale cached before the thing that OWNS the layout has run is a stale rest scale.** It is
the icon-level rule this hull already records (§"The kerning bug the core card exposed") met one
level up, at the HOST — and it hid longer because the icon version breaks ONE card while this one
breaks every card *except* one, which inverts where a reader looks.

Its companion is about the reporting loop: **when a fix lands and the same sentence comes back,
the second report is evidence about a different system, not a weaker version of the first.** Both
defects were real, both present as *this card is bigger than the others*, and the only thing that
told them apart was measuring the frame instead of reading the source. *Measure the picture before
re-reading the code that draws it.*

## Seventh pass (2026-09-26): the row's spacing, doubled fleet-wide

Once the four authored cards stopped drawing at 0.7 they drew at their real 104-unit plate width, so
the air between them closed from the ~43 units the shrunk cards left to the 12 the pitch actually
lays out — and the row read as one strip. The spacing was doubled on request:
`AbilityLockupStyle.cardPitch` **116 → 128**, i.e. the gap between adjacent plates 12 → 24, which is
`plateWidth + 2 × cellGap` → `plateWidth + 4 × cellGap`.

**It is ONE field on the shared style asset, which is the whole reason the request "do that for all
vessels, this should be consistent" needed no per-vessel work**: the lockup owns the row on every
hull (`Docs/ABILITY_LOCKUP.md`), a vessel cannot author its own pitch, and the element flowers ride
the same columns — so both halves of every totem move together and consistency is by construction
rather than by six edits that could drift.

What it costs: the Squirrel's five-card row now spans **616 units** against 568, reaching **656 of a
1920 reference canvas** (34%) once `rowMarginRight` is counted. Nothing else moves — plate geometry,
icon kerning, chip placement and the generated Space readout are all absolute and unchanged, which
`check_squirrel_card_fit.py` re-confirms.

**The pitch had no ceiling and now has one.** It is the dial somebody reaches for to space the cards
and nothing in the style said how wide the row it lays out ends up, so both `AbilityLockupStyleTests`
and the auditor now assert `(cards − 1) × cardPitch + plateWidth + rowMarginRight` stays inside the
right half of the reference canvas — past the middle the row crosses the bottom-centre HUD, and on a
narrower aspect it runs off the left. The card count is read from `VesselHUDView`'s own
`AbilityDisplayOrder` + `CoreAbilityDisplayOrder` rather than typed, so **adding a core ability
tightens the bound automatically instead of quietly invalidating it** — the same argument as deriving
an icon's scale from `iconBoxSize`. Max pitch at five cards: **204**.

## Eighth pass (2026-09-26): the OMNI CRYSTAL card, fleet-wide

The row gained a **sixth** card — `CoreAbility.OmniCrystal`, the second non-elemental one — and it
is the first card **no vessel may skip**: `VesselHUDView.EnsureOmniCrystalCard` is not virtual and
not opt-in, called by `AbilityLockupView.Build` beside `EnsureGeneratedAbilityIcons`, because every
hull can fly through a crystal. Full contract: `Docs/ABILITY_LOCKUP.md` § "The OMNI CRYSTAL card".

**The card introduced the EMBLEM**, which is the one genuinely new idea here. A core card previously
had no upper cell at all; now it has one if the style names a mark for it. So the lockup draws three
shapes and the upper cell is what differs — an elemental card's **flower** (a level readout), a core
card's **emblem** (a name), or nothing (the drift). The emblem shares the socket, the plate, the
bloom and every bit of the Y arithmetic with the flower; only what is docked differs. It is drawn
**untinted**, deliberately: an omni crystal belongs to nobody until somebody takes it, which is the
same thing the crystal's own palette says (`Docs/PALETTE.md §2.2`).

**The two halves come from different places and that is the design.** The emblem is ONE row on the
fleet-wide style (`AbilityLockupStyleSO.coreAbilityEmblems` → `ElementIcons/OmniCrystal_Active.png`,
the same crystal the Dolphin's Mass card wears) because the mark means the same thing on every hull;
the lower icon is per-vessel (`omniAbilitySprite`) because what a crystal *does* is a property of
the hull. The Squirrel authors one and the Dolphin generates one (the blast's prism tally) — the
other six render **LOCKED**, which is what a locked card is for (the table is in the lockup doc).

### The Squirrel's icon is a measurement, not a drawing

`Tools/Build/author_squirrel_shielded_ring_icon.py` (`--check`) is the exact sibling of
`author_squirrel_boost_ring_icon.py`: the ring's cross-section seen endwise, with every number read
out of the shipped assets rather than restated — `prismsPerRing` / `ringRadius` / `prismScale` from
`AOEShieldedRingSpawner.prefab` **and its base prefab** (a variant carries only its overrides, so
reading either file alone gets a different ring), and `CIRCUMSCRIBING_SCALE` from
`OctahedronMeshGenerator.cs`. It also fails if `isShielded` ever goes to 0, because the icon would
then be describing an ability that does not exist.

**The 45° turn the request asked for is what a shield IS.** `ActivateShield` engages the
circumscribing octahedron, and an octahedron's cross-section perpendicular to one axis is exactly
the box's square turned 45° and grown to the octahedron's own reach — so a shielded prism reads as a
**diamond** where a bare one reads as a **square**. At the shipped numbers (8 prisms, radius 8.2,
leaf 1.8, scale 3) the diamond's half-diagonal is **2.7** and neighbouring shields clear by **1.287**
world units, which is simultaneously a statement about the icon and about the ability.

**Stated plainly, because it is the interesting cost: the two rings are nearly the same figure.**
Both are 8 prisms at radius ~8, so the icons are each other's 45° rotation and differ only in which
positions carry diamonds. That IS the family resemblance the request asked for, and **colour is what
separates them** — the boost ring wears the palette's DANGER rim, this one wears the pilot's own
domain's shielded base face at signal strength (`SO_ColorSet.GetShieldedSignalColor`, the fourth
`*SignalColor` sibling; `Docs/PALETTE.md §2.7`).

### Two findings

**1. A survey that mis-parses reads exactly like a survey.** The first pass at "which hulls do
something with an omni crystal" used a regex that stopped at the wrong block and reported the
Dolphin and Urchin as authoring NO crystal effects. Both do. The wrong answer was written into two
doc comments before it was re-measured, and it was plausible enough to survive review — the Scarab
really is empty, so one third of the claim was right and carried the other two. *When a survey's
output is a table, spot-check one row against the file by eye before quoting it.* Corrected: only
the **Scarab** is empty (by design — its skimmer forges the crystal into a ball before the hull
reaches it), and the **Urchin** is haptics-only.

**2. A core card with no icon used to VANISH, not lock.** The lockup's core pass `continue`d on a
binding with no icon, where the elemental pass falls back to a locked host — a difference that
cost nothing while the only core card was one the Squirrel authors, and would have silently
deleted the omni card from seven hulls. Both passes now share one `ResolveLockedHost`, generalised
off the enum onto a string. *An extension point that has only ever had one user has only ever been
tested for that user's shape.*

## Ninth pass (2026-09-26): the omni card was painted the no-team SENTINEL

The eighth pass shipped and the card came back *"blue regardless of which domain I picked"*. The
follow-up correction is what identified it: **it was reported as NOT being Jade's shielded hue** —
*"a neutral hue of blue, not the jade shielded outside prism hue which has more green and is less
saturated."* Measured against the shipped `OriginalColorSetSO`:

| domain | `GetShieldedSignalColor()` | hue | saturation | green |
|---|---|---|---|---|
| Jade | (0.179, 0.489, 1.000) | 217.4° | 0.821 | 0.489 |
| Ruby | (0.704, 0.345, 1.000) | 272.9° | 0.655 | 0.345 |
| Gold | (1.000, 0.670, 0.262) | 33.1° | 0.738 | 0.670 |
| **Blue (the sentinel)** | **(0.000, 0.000, 1.000)** | **240.0°** | **1.000** | **0** |

Exactly one row in the palette is a pure, fully-saturated, green-free blue, and it is
**`Domains.Blue`** — the *"no team / not yet picked"* sentinel, which `SO_ColorSet` nevertheless
authors a full `DomainColorSet` for and `TryGetColorSetByDomain` happily returns. The baked sprite is
pure white + alpha (verified by decoding the PNG: 7 distinct pixels, every one `(255,255,255,a)`), so
the whole colour is the tint, and the tint resolved the sentinel.

**Two defects, and they are independent — either alone would have produced a wrong colour.**

**(1) The domain was SNAPSHOTTED at `Initialize`**, which is the rule CLAUDE.md states outright: *do
not snapshot domain at component-creation time.* `Player.NetDomain` is server-write and initialises to
Jade, the owner's own pick arrives later through `RequestSetDomain_ServerRpc`, and the match's active
set can move a human again at spawn (`NormalizeUnassignedHumans`) — so whatever this controller read
one frame after `Initialize` is what the card wore for the rest of the match. It was snapshotting it
**twice**: `SetOmniAbilityTint` (new) and `SetPlayerDomainColor` (pre-existing, which is why the steal
count and the boost fill had the identical defect and nobody had noticed). Both now go through one
`RepaintForDomain`, called from `Initialize` and then from `PushDomainPalette` in the existing
`Update`, which repaints only on a change. (Both were folded into one `PushPalette` by the tenth
pass below — named here as the record of what THIS pass shipped, not as current state.)

Polled rather than subscribed, for two reasons worth stating: this controller **already runs an
`Update`** for the tube cooldown and the steal readout, so the poll is free; and a
`NetDomain.OnValueChanged` subscription has to be torn down against a `Player` reference that a vessel
swap replaces underneath it, which is the asymmetric-binding failure the vessel contract has paid for
three times. The read is gated on `Player` being present because `IVesselStatus.Domain`
**`CSDebug.LogError`s** when it is not — a per-frame poll through that getter turns one missing
reference into console spam.

**(2) `Domains.Blue` was looked up as a colour at all.** That is the finding, and it generalises past
this card: **a sentinel that has a row in a lookup table gets a plausible answer, so a lookup that
failed to resolve does not render as a failure — it renders as a different team.** That is strictly
worse than the four traps `Docs/PALETTE.md` §§2.4–2.7 record, because a black or transparent slot reads
as *not implemented* and gets reported, while a saturated wrong hue reads as *implemented and
mis-tinted* and gets rationalised — which is exactly what happened for a whole round.

The refusal is in the **CALLER** (`ResolveShieldedColor` returns alpha 0 for Blue, so the icon keeps
its white and the poll keeps looking), not in `SO_ColorSet`: *"no pilot can fly Blue"* is a fact about
pilots, and a neutral mine or an uncommitted crystal legitimately wants to know what colour no-team is,
so the palette accessor stays a pure palette read like its three siblings. Note two executors already
used the mirror-image idiom before this — `EchoSightActionExecutor` and `SniperShotActionExecutor` both
write `status?.Player != null ? status.Domain : Domains.Blue`, i.e. **Blue already MEANS unresolved in
this codebase**, which is the whole reason it must never be asked for a colour. Recorded as
`Docs/PALETTE.md` §2.8.

**The Dolphin already had the right shape** and is the reason this is a Squirrel bug rather than a
fleet one: `DolphinVesselHUDController.ResolveDomainSignalColor()` is called from
`PushCrystalSeeding()` inside its own `Update`, so it has always resolved live. A fleet sweep found no
other HUD controller snapshotting a domain; every other reader in `R_VesselActions/` resolves at USE
time (per shot, per lay), which is correct.

## Tenth pass (2026-09-26): the ninth pass's own latch singled out Jade

The ninth pass shipped and the report came back: *"gold and ruby were switching to their colors just
fine, but jade was still returning to blue each time."* One domain wrong and two right is a very
specific shape, and it names the defect: **the retry latch recorded which domain it had ATTEMPTED to
paint, not whether the paint LANDED.**

`SetOmniAbilityTint` refuses `tint.a <= 0` — the view keeps its white rather than painting black
(§2.4's contract) — and `ResolveShieldedColor` returns alpha 0 whenever `gameData.ThemeManagerData`
is not resolvable yet, which it is not for part of the spawn chain (`gameData` is `[Inject]`, so it is
populated after `Awake` but before `Start`, while `Initialize` is called from the vessel's own spawn
chain). So a first push could legitimately fail, leave the card white — and still set
`_paintedDomain = Jade`. From there the retry was gated on the domain **changing**.

**Jade is `Player.NetDomain`'s own initialiser**, so Jade is the only value that can already be the
recorded one. Ruby and Gold always arrive as a change and always repaint; Jade never does. *A latch
that records its input rather than its outcome fails on exactly one input — whichever one is the
default — which is why this presented as a Jade bug rather than as a latch bug.* It is also the
**second** instance of a rule the vessel contract already states (`/vessel` rule 6: *resolution
retries until success; a query that latches on attempt pins null forever*), reached from a different
subsystem.

Fixed with two independent latches in one `PushPalette` — the danger tint is domain-independent and
can only ever be waiting on `ThemeManagerData`, the domain tints are additionally waiting on a domain
— neither closing until the palette actually answered:

```
_domainPainted = PaletteLanded(live, shielded);   // domain != Blue && resolved.a > 0f
```

**`Domains.Blue` must not close it either**, for the mirror reason: its shielded tint is refused
permanently by design, so a latch closed on the sentinel would freeze the card white for the rest of
the match the moment a pilot was once seen unresolved.

### Why this one is a TEST rather than a comment

`PaletteLanded` is a one-line pure static with its own suite (`SquirrelHudPaletteLatchTests`) because
**replacing it with `true` is a logic regression, not a type error** — the Roslyn stub harness
compiles the regression clean, and all eight textual gates pass it. Measured: the suite's 7
assertions all pass against the shipped predicate and **4 of them fail** against
`_domainPainted = true`. That is the whole justification for promoting one line to a named function;
without it the only thing standing between this bug and the next branch is a paragraph.

### And the reason it could not be verified by eye — a palette fact worth knowing

| | normalised | hue | sat |
|---|---|---|---|
| Jade shielded **base** (what the card reads) | (0.179, 0.489, 1.000) | 217.4° | 0.821 |
| Jade shielded **rim** (what sits over it on a prism) | (0.336, 0.528, 1.000) | 222.7° | 0.664 |
| Jade **identity** (`TrailHighlightColor`, what players call "Jade") | (0.067, 1.000, 0.947) | 176.6° | 0.933 |
| Blue / the sentinel | (0.000, 0.000, 1.000) | 240.0° | 1.000 |

**Jade's shielded tier is blue on BOTH halves** and sits **22.6° of hue** from the sentinel, while
Jade's *identity* colour is teal 41° away in the other direction. So a correct Jade card and the bug
look the same at 60 px, and no palette reading makes Jade's shielded mass teal — the icon is honest,
it is Jade's trail that is the teal players recognise. Confirmed across all three palettes:
`CosmicWaveColorSetSO` and `PastelColorSetSO` author their shielded base at **alpha 0** (unauthored,
correctly refused), so `OriginalColorSetSO` is the only live answer and `ThemeManager` never swaps it.

That is what makes the **white-when-unresolved** contract load-bearing rather than tidy: it is the
only thing that separates *"this is Jade"* from *"this never resolved"*, because the two colours
cannot be told apart. If the card should instead say WHICH DOMAIN rather than WHAT THE MASS IS, the
lever is `ResolveShieldedColor` reading `GetDomainSignalColor` — a different promise, and a design
call, not a fix.

## Eleventh pass (2026-09-26): LINEAR floats in a GAMMA slot

Three rounds chasing this colour were one mistake. **A palette float is a LINEAR intensity
(`m_ActiveColorSpace: 1`, `Docs/PALETTE.md §3`) and a UI `Image.color` is GAMMA** — so handing
`ShieldedOutsideBlockColor` to an Image is a space error, and every correction layered on top was
compensating for it rather than doing a job.

The report that broke it open came with a **screenshot**, then with the field's own value:

> *"the actual ring of jade shielded prisms is clearly a different color than what you are coloring
> the icon. also the color on the icon is clearly the exact same blue used to depict an elemental
> petal with 2 levels of upgrade."* … *"shielded outside block color comes out to 22, 60, 123."*

Both true, and the conversion reconciles them:

| Jade shielded base face | RGB | hue | sat | val |
|---|---|---|---|---|
| the raw field, read as bytes | (22, 60, 123) | 210.3 | 0.550 | 0.482 |
| **converted — what ships** | **(83, 134, 185)** | **210.3** | 0.550 | 0.725 |
| the previous answer (peak-normalised) | (46, 125, 255) | 217.3 | 0.821 | 1.000 |
| **the prisms, measured on screen** | (95, 176, 254) | **209.4** | 0.626 | 0.996 |

**The conversion lands under one degree from the measured prisms.** The 8° the old answer sat away
had been written down — in this file and in `PALETTE.md` — as an ACES hue shift. It was the missing
conversion. *A wrong hypothesis does not land within a degree.*

Two things the accessor used to do are deleted, and both were the compensation:

- **The peak normalisation**, justified as *"the authored colour is too dark for a UI slot"*. It is
  not dark, it is **linear**. And that normalisation is what pushed the answer to hue 217.3°, which
  is `ElementalBarsConfigSO.blueColor` to within **0.3°** — on this very row, the colour that means
  *two upgrades in*. The petal collision was **manufactured by the bug**, not an independent fact.
- **A 0.25 lerp toward white**, modelling bloom + ACES on top of the first compensation.

Converted honestly the value is legible (brightness 0.725), sits 0.230 of saturation clear of that
ladder rung, and needs neither. Shipped: **Jade (83, 134, 185) · Ruby (156, 113, 183) · Gold
(152, 126, 81)**.

**Stated gap:** the prisms read brighter (measured value ~1.0 against 0.725) because a bright HDR rim
sits over the base and blooms. This is the base face's colour, correctly converted — right hue, right
tier, no bloom. Inventing a lift for that is precisely what just went wrong twice.

`ShieldedSignalColorTests` gates the hue match against the measurement, the conversion contract
(convert, do not normalise, do not lift), the ladder clearance, the sentinel refusal and per-domain
hue separation — read off the shipped assets, so a palette edit that reopens any of it fails.

### What four rounds cost, and the rules

Rounds nine to twelve each found a real defect — the sentinel lookup, the latch, then this — and only
the last changed what the player saw. Every one presents as the same sentence (*"the icon is the
wrong blue"*) and **none is decidable from source**: two colours 0.3° apart are identical in a diff.

- **When a report is about a COLOUR, sample the frame before reading the code that sets it.** The
  screenshot answered in one measurement what three passes of correct reasoning could not.
- **A colour that looks too dark for UI may just be in the wrong space** — reach for the conversion
  before a brightness correction, because a correction tuned on an unconverted value is tuned on a
  different colour and moves hue as well as brightness.
- **The five petal-ladder colours are the HUD's vocabulary** and any new HUD tint is checked against
  them, even when — as here — a collision turns out to be a symptom rather than the disease.

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

## ~~Stated cost: the Squirrel has no drift readout on the HUD~~ — CLOSED (tenth pass, below)

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
6f. **The Boost Ring wears the DANGER colour and nothing else** — the eight blocks are hot red and
   the middle is EMPTY. If the blocks are WHITE, `SetDangerTint` did not arrive or the palette
   authors no danger colour (`CosmicWaveColorSetSO` and `PastelColorSetSO` both author none, and the
   accessor then returns alpha 0 so the icon correctly keeps what it had). If anything is drawn in
   the middle, a `BoostRingTunnel` child survived on the prefab from the fourth pass — it is created
   by name and the builder is gone, so delete the object.
6g. **The Space card is no bigger than its four neighbours** — this is the one the pass exists for.
   Raise Space to full and confirm the ring at its biggest still sits inside the same box the Mass
   sprite fills, with the number below it and clear of the plate's bottom edge and the control chip.
   At rest the small ring must leave the number alone. Type a four-digit value into `StealCount` in
   the hierarchy while playing (or steal that many) and confirm it neither wraps nor is clipped.
6h. **All five cards are the SAME size** — the sixth pass's check, and the fastest one on this list:
   the four authored plates and the generated Space plate must have the same width and height. Then
   **press an ability, release it, and hide/show the HUD** (fly into the vessel-changer toy, or
   toggle freestyle) and look again — that is the path that used to snap the four authored hosts
   back to the prefab's 0.7, and it only shows after an `OnDisable`. If a card shrinks, its
   `AbilityButtonPressJuice` captured a rest before `PlaceHost` wrote one.
6i. **The row's spacing** — the seventh pass. The gap between two adjacent plates must read as
   clearly wider than the gap between a card's own two plates (24 against 6), on EVERY vessel, not
   just this one: open the Dolphin, Scarab and Sparrow HUDs too and confirm their rows moved with
   it. Then confirm the leftmost card is still comfortably clear of the screen's middle.
6j. **The omni crystal card** — the eighth pass, and it must be checked on MORE than this vessel.
   On the **Squirrel**: a SIXTH card sits between the drift and Charge, its upper plate carrying the
   omni crystal emblem in plain white (NOT a domain colour, and NOT a flower), its lower plate
   carrying a ring of eight diamonds. Fly through an omni crystal and confirm the ring of
   shielded prisms it lays is the same colour as the icon. Then switch domain at the domain-changer
   toy and confirm the icon follows (Jade blue → Ruby violet → Gold amber) while the emblem above it
   does not change at all. Then open the **Dolphin, Scarab, Sparrow, Manta, Rhino and Serpent** and
   confirm each has the same card with the same emblem and a LOCKED lower plate — a card that is
   MISSING on any of them is the failure mode the locked-host fallback was added for.
6k. **The tint follows the LIVE domain, and the sentinel is never a colour** — the ninth pass, and
   the specific thing that shipped wrong. Read the three hues off the palette before you look at the
   HUD, because the failure was a *plausible* colour: Jade is hue **217°** at saturation 0.82 with a
   real green channel (0.489); the value that shipped was hue **240°** at saturation **1.000** with
   **zero green**, which is `Domains.Blue`'s row and nothing a playable domain can produce. So the
   check is not "is it blue" — it is **"does it have green in it"**. Then: switch domain mid-flight
   at the domain-changer toy and confirm the icon repaints *within a frame* (it is polled, not
   event-driven), and that the **steal count and the boost fill** repaint with it — those had the
   identical snapshot defect and are now on the same call. Finally, a card whose domain has not
   resolved yet must render **WHITE**, never blue: a white icon reads as untinted, a saturated one
   reads as a team.
6l. **Jade specifically** — the tenth pass. The ninth pass's retry latch failed on exactly one
   domain, and it was the DEFAULT one, so this step is not covered by 6k's cycle test. Start a match
   **without touching the domain changer at all** (a fresh pilot is Jade) and confirm the omni card
   is Jade's `(0.179, 0.489, 1.000)` and not white. Then Jade → Ruby → Jade and confirm it comes
   back. Do not try to tell Jade's card from the old sentinel bug by eye — they are 22.6° of hue
   apart; **white is the failure state now**, so the question is only *is it coloured at all*. Check
   the Boost Ring's danger tint in the same breath: it is pushed by the same retry and had the
   identical exposure.
6m. **The icon against the MASS, and against the ROW** — the eleventh pass, and the only step here
   that has to be judged on a frame rather than in the inspector. Fly through an omni crystal and
   look at the ring of shielded prisms it lays and the icon TOGETHER: they should read as the same
   pale ice blue (shipped Jade (98, 157, 255)), not the icon noticeably deeper than the mass. Then
   look along your own ability row: the icon must not match the blue of an element petal at **+2
   upgrades** — raise any element to level 10 and put the two side by side. If they read as one
   colour, the linear->gamma conversion has been undone and `ShieldedSignalColorTests` should be
   failing. The icon is deliberately a little DARKER than the prisms (0.725 brightness against
   their ~1.0): that is the bloom on their HDR rim, not a mismatch.
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
- ~~**Drift readout**~~ — done on the drift's own core card (tenth pass). `ElementalBarsView.JuiceDriftStart/End` stay dead.
- **The Time card** — the design call above. Until it is made, one element's upgrade is invisible.
- **Joust art** — the Charge card borrows an objective icon; purpose-made HUD art would replace it.
- The Squirrel's `Input: 11` ability (Boost Ring) still lays **danger** prisms; unchanged here —
  which is now what its card SAYS, rather than something only the code knew.
- **The other 73 BOM'd files** have never been seen by `check_using_directives.py`'s first-line
  rule either. The gate is scoped to changed files, so they will be checked as they are
  touched; a one-off `--all` run would clear the backlog and is not done here.

## Tenth pass (2026-09-26): full speed off the joust, the drift responds, the omni card flashes

**The "full speed indicator" on the joust card was a platform bug, not Squirrel art.**
`FullSpeedStraightAction` is the input enum's zero, and it is two things at once: a real event
(every input strategy raises it while the throttle is buried and the stick is centred) and the "no
button" sentinel a passive ability map entry is authored with. `VesselHUDController.Toggle` resolved
a press through the map by first match on `entry.Input`, so flying flat out pressed the FIRST
`Input: 0` entry — the Squirrel's joust, and the same card on Butterfly, Manta, Rhino and Scarab (the
Mass card on Dolphin, Serpent and Urchin). The chip side already treated the zero as "no control";
the press side now refuses it too (`VesselHUDController.IsPassiveSentinel`, held by
`HudPassiveInputSentinelTests`). Stated consequence: the Rhino's ramp genuinely engages on that
gesture, and its Charge card (an open slot) no longer lights for it — which was the wrong card anyway.

**Core cards now light on their own control.** `Toggle` resolved only the elemental map, so the
drift card drew an LT chip and never lit when LT was pulled. It now also matches
`coreAbilities[i].input` (the same field the chip is drawn from), with the sentinel refused so the
omni card — bound to no input — stays dark at full speed.

**The drift icon responds again**, on the core card rather than the Mass slot it was cut from:
sprite swap (`DriftIconSelected-PLACEHOLDER`, restored from the pre-cut prefab), tint, swell, and a
LEAN. The lean is no longer decided once at drift start — on that frame the nose has not left the
course, so any read is noise — but fed every frame by the controller from `Course` in the SHIP's own
frame (the old read projected onto world up, which only meant anything while level), re-tweening
only on a change of side. The icon is resolved off the core binding, not re-serialized. Releasing one
of the two drift actions while the other is held no longer ends the look (`IsDrifting` is written
before `driftEnded` is raised).

**The old sharp-drift look had never fired.** Pre-cut, `isDoubleDrifting` was wired to
`EventOnSharpDrifting`, which nothing in the project raises; the drift actions raise
`EventOnDoubleDriftStarted`. Re-wired to the one that is raised.

**An omni crystal pickup lights the omni card.** `squirrelCrystalExplosionEvent` is raised only by
the Squirrel's omni branch, and it used to flash the JOUST icon (the two shared one impact icon
before the omni card existed). It now calls `VesselHUDView.PlayOmniCrystalCollected` — the lockup's
one-shot press flash on the card's plate plus a punch-and-whiten of the shielded-ring icon that
settles back to its domain tint (a tint repaint mid-flash wins). Fleet-generic; only the Squirrel
calls it today because only it has a HUD event for the pickup.

**Not run in Unity.** Verified out of editor: the ten standing gates pass; a Roslyn pass over the
five changed .cs finds no syntax errors (type-resolution errors are the expected no-Unity noise).
In-editor check: fly a Squirrel flat out (joust card stays dark), hold LT (drift card lights, icon
swaps, swells and leans toward the swing, follows a side change, settles on release), fly through an
omni crystal (omni card flashes, icon punches white and returns to the shielded tint).
