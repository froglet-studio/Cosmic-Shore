# Fleet Completion Push — the six closest vessels

**Dated 2026-08-24.** Measured from the shipped assets, not from any status table.
Companion to `FLEET_MAPS.md` (the per-vessel record) and `BACKLOG.md` (the phase plan).
This doc is the **intent** of the completion push and the **work order** that finishes it.

## 0. Intent

The elemental ability contract is *"four abilities, each owned by one of the four elements, each
with a level-5 upgrade, each shown as one of four HUD icons in charge → mass → space → time
order."* Four vessels satisfy it end to end. The fleet's remaining incompleteness is **not**
evenly spread and it is **not** all the same kind of work, and conflating the two kinds is why
the fleet has looked "nearly done" for several branches without closing.

This push separates them and closes them in that order:

1. **DRIFT** — places where a doc contradicts a shipped asset. Free to fix, and dangerous to
   leave: the next branch reads the doc, not the asset. (§7)
2. **MECHANICAL holes** — an approved design that is simply not wired: a missing HUD variant, an
   unbuilt icon row, an `Input` field left at `0`. No sign-off needed, no design risk. (§5)
3. **DESIGN-GATED holes** — an element with no ability, or an ability with no level-5. These are
   blocked on Garrett and **may not be invented to green an auditor** (the `/vessel` skill's §3
   gate). Every one of them is named here with its existing proposal, if it has one. (§5)

The push explicitly does **not** re-litigate any shipped row. Sparrow, Dolphin, Squirrel and
Urchin maps are the record; Scarab's is Garrett's 2026-08-15 markup. Where this doc disagrees with
an older table, **the asset wins and the table is the bug**.

## 1. How "closest to completion" was measured

Five binary-ish facts per vessel, all readable from assets with no Unity:

| Axis | Source of truth | Complete means |
|---|---|---|
| Quantitative rows | `Assets/Resources/ElementalAbilityMaps/{Vessel}.asset` — `AbilityLabel` | 4 entries, none `(open design slot)` |
| Level-5 upgrades | same asset — `UpgradeLabel` | 4 non-empty |
| Input bindings | same asset — `Input`, cross-checked against the prefab's `_inputEventShipActions` / `_gamepadActionOverrides` | every non-passive ability names its real event |
| Ability icon row | `abilityIcons` in the HUD variant **and** the vessel prefab (the Rhino's row was once missed by checking only one) | 4 bindings, charge → mass → space → time |
| HUD prefab | `Assets/_Prefabs/UI Elements/VesselHUD/{Vessel}HUDVariant.prefab` | exists |

**`Input: 0` is ambiguous by construction** — `InputEvents.FullSpeedStraightAction = 0`, so a
genuinely-unset field and a genuinely-passive ability are indistinguishable in the asset. This doc
resolves each case against the prefab's real bindings and says which it is. Fixing that ambiguity
per row is part of the mechanical work.

## 2. The scorecard

| # | Vessel | Quant | L5 | Icons | HUD prefab | Inputs named | Remaining work is… |
|---|---|---|---|---|---|---|---|
| 1 | **Sparrow** (11) | 4/4 | 4/4 | 4/4 | ✅ | 4/4 | drift only |
| 2 | **Dolphin** (2) | 4/4 | 4/4 | 4/4 | ✅ | 2/4 + 2 passive | drift only |
| 3 | **Squirrel** (6) | 4/4 | 4/4 | 4/4 | ✅ | 2/4 + 2 passive | drift only |
| 4 | **Scarab** (12) | 4/4 | **3/4** | 4/4 | ✅ | 2/4 + 2 passive | **1 design gate** |
| 5 | **Urchin** (4) | 4/4 | 4/4 | **0/4** | **❌ none** | 3/4 + 1 passive | **mechanical** (whole HUD) |
| 6 | **Manta** (1) | **3/4** | **0/4** | **0/4** | ✅ | 0/4 | **5 design gates** + row |
| — | Rhino (3) | 1/4 | 0/4 | 0/4 | ✅ | 0/4 | 7 design gates + row |
| — | Serpent (7) | 1/4 | 0/4 | 0/4 | ✅ | 0/4 | 7 design gates + row |
| — | Grizzly (5) | no map | — | — | ❌ | — | everything |

**The cut line sits between Manta and Rhino** and it is a real gap: Manta has three authored
quantitative rows, Rhino and Serpent have one each. Termite (8), Falcon (9) and Shrike (10) are
`VesselClassType` members with no prefab, no map and no HUD, and are out of scope entirely.

**Urchin ranks above Manta on purpose.** Its map is complete and approved; every hole it has is
wiring a human can close without a design decision. Manta's holes need Garrett first. Ranking by
"how much is left" would invert these two; ranking by "how much is left that needs a *decision*"
is the ordering that actually predicts how fast a branch closes.

## 3. The map as shipped — the six

Rows are in contract order (charge → mass → space → time), which is also HUD left-to-right order.
"Parameter" is what the element scales; where an authored SO field carries the scaling the map's
generic `MultiplierAtFullLevel` is pinned to 1 (the no-double-dip rule).

### Sparrow (11) — shooter · COMPLETE

| Element | Ability | Parameter (scaling) | Input | Level-5 upgrade |
|---|---|---|---|---|
| Charge | Skyburst Rockets | blast radius — authored 100→170 on the four skyburst effect assets (map 1.0) | LT `2` | **Domain-Safe Skybursts** |
| Mass | Turret Stance | turret-fired prism stretch (map **2.5**, min 0.4) | X `6` | **Shielded Prisms** |
| Space | Pulsefire Cannons | gun range (map **9.0**, min 0.4) | RT `1` | **Piercing Bullets** |
| Time | Afterburner | boost speed (map **1.5**, min 0.5) | A `7` | **Elemental Ward** |

### Dolphin (2) — energy economy · COMPLETE

| Element | Ability | Parameter (scaling) | Input | Level-5 upgrade |
|---|---|---|---|---|
| Charge | Echo Sight | blast capsule **thickness** — 0.75× the authored core at rest → 1.5× at L10 (map 1.0) | RT `1` | **Pilot Echo** |
| Mass | Crystal Seeding | seeding recharge ×0.5 at L10 (`cooldownMultiplierAtFullMass`; map 1.0) | **passive** | **Claimed Seed** |
| Space | Echo Obliteration | blast **reach** ×2 at L10 (`_heightMultiplierAtFullSpace`; map 1.0) | **no button** — released by flying into a crystal | **Clean Blast** |
| Time | Charge Fill Rate | boost charge rate while drifting ×1.5 at L10 (map 1.0) | LT `2` | **Drift Ward** (scoped `DangerPrism`) |

### Squirrel (6) — racer · COMPLETE

| Element | Ability | Parameter (scaling) | Input | Level-5 upgrade |
|---|---|---|---|---|
| Charge | Skimming | skim energy per prism-skimmer hit (map **2.0**, min 0.25) | **passive** (contact) | **Live Wire** |
| Mass | Trail Volume | trail prism VOLUME — authored `trailVolume` 1→2.5, cube-root per axis (map 1.0) | drift — asset records touch `12`, gamepad is LT `2` | **Heavy Trail** |
| Space | Skimmer Reach | skimmer sphere `Scale` 15→30 (map 1.0) | **passive** | **Shepherd** |
| Time | Boost Ring | ring cooldown ×0.5 at L10 (`cooldownMultiplierAtFullTime`; map 1.0) | asset records touch `11`, gamepad is RT `1` | **Twin Rings** |

### Scarab (12) — hoop-court · 3/4 upgrades

| Element | Ability | Parameter (scaling) | Input | Level-5 upgrade |
|---|---|---|---|---|
| Charge | Cavitation Blast | blast cooldown ×0.5 at L10 (`cooldownMultiplierAtFullCharge`; map 1.0) | rides the base-kit RT dash (asset `0`) | **Cavitation Shear** |
| Mass | Switch | switch ring aperture + interior fill span (`switchScale` 1→2.5; map 1.0) | X `6` | **Armored Switch** |
| Space | Ball Forge | forged ball size ×1 → **×4** at L10 — **the map itself is the carrier** (min 0.5) | **passive** — fly through an omni crystal | ⚠ **OPEN** |
| Time | Throttle | top speed of the throttle ramp (`ThrottleScalerMultiplier` 1→1.5; map 1.0) | RT `1` | **Snap Dash** |

### Urchin (4) — chain spikes + trail rider · map COMPLETE, HUD ABSENT

| Element | Ability | Parameter (scaling) | Input | Level-5 upgrade |
|---|---|---|---|---|
| Charge | Chain Spikes | cascade **depth** (integer level, off-map) × spike **reach** (map **2.5**, min 0.4) | RT `1` | **Overcharge** |
| Mass | Trail Rider | volume each friendly prism gains as you ride it (`growthAmount` 0.6→1.2; map 1.0) | **passive** (contact) | **Reinforced Wake** |
| Space | Track Projector | track **length** — 100 u × `lengthMultiplierAtFullSpace` 2 at L10 (map 1.0) | LT `2` | **Long Haul** |
| Time | Slip | ghost duration 0.6 s → 1.6 s (map 1.0) | A `7` | **Slipstream** |

### Manta (1) — Reaper Ray · 3/4 quantitative, 0/4 upgrades

| Element | Ability | Parameter (scaling) | Input | Level-5 upgrade |
|---|---|---|---|---|
| Charge | Overcharge Detonation | detonation blast (map **1.75**, min 0.25) | ⚠ unset `0` | ⚠ **EMPTY** |
| Mass | Overcharge Harvest | harvest capacity (map **1.75**, min 0.25) | ⚠ unset `0` | ⚠ **EMPTY** |
| Space | Yawstery | turn rate (map **1.6**, min 0.25) | ⚠ unset `0` — prefab binds `11`/`12` (+ `MantaAnalogTurnBoostAction` on gamepad) | ⚠ **EMPTY** |
| Time | ⚠ **(open design slot)** | — (map 1.0) | ⚠ unset `0` | ⚠ **EMPTY** |

Manta's prefab also binds `BoostAction` on `BothSticks 13`, which no map row currently claims.

## 4. What the six are actually missing

| Vessel | Design-gated | Mechanical |
|---|---|---|
| Sparrow | — | — |
| Dolphin | — | disambiguate the two `Input: 0` rows as passive-by-design in the asset prose |
| Squirrel | — | same, plus record that its `Input` values are the **touch** map while gamepad differs |
| Scarab | **Space level-5** (1) | Charge/Space `Input: 0` disambiguation |
| Urchin | — | **`UrchinHUDVariant.prefab` does not exist**; 0/4 icon row; controller-hint switcher |
| Manta | **Time ability + parameter, and all four level-5 upgrades** (5) | 0/4 icon row; 4× `Input` field; claim or drop the `BothSticks` boost |

Totals for the six: **6 design decisions**, and everything else is wiring.

### 4.1 A finding this measurement turned up: the HUD prefabs are forked

`InputDeviceIconSetSwitcher` — the component that makes `BindHintsToAbilities` resolve a control
glyph onto its ability's icon — exists in exactly **one** place in the project:
`VesselHUDPrefab.prefab`. Only **Squirrel, Manta and Serpent** are true prefab variants of it and
inherit it. **Sparrow, Dolphin, Scarab and Rhino are hard copies**, so they carry no switcher, both
glyph sets render at once, and hints cannot bind — on three vessels whose maps are otherwise
complete.

That is the `Docs/GAMECANVAS.md` rule (*"a variant, never a copy"*) reproduced on the HUD side, and
it is why "Sparrow has no switcher" has read as a per-vessel oversight rather than as one
structural fact about four prefabs. Any prompt below that says *"add an `InputDeviceIconSetSwitcher`"*
should prefer **re-parenting the HUD as a variant of `VesselHUDPrefab`** where that is still
possible, and only add the component standalone where it is not. Re-forking is out of scope for
this push — recorded so the next branch does not rediscover it.

## 5. The design gate

Per the `/vessel` skill §3, an open slot is blocked on **design, not wiring**, and must never be
filled to green an auditor. The six open decisions, each with its existing proposal:

| # | Vessel · slot | Existing proposal | Status |
|---|---|---|---|
| D1 | Scarab · Space 5 | none — `SCARAB.md` §7 and the asset's own `UpgradeDescription` say *"do not invent one without sign-off"* | needs a design, or an explicit "stays empty" |
| D2 | Manta · Time quantitative | `FLEET_MAPS.md` §2 proposes **overcharge decay rate** | un-approved |
| D3 | Manta · Charge 5 | proposes **Domain-Safe Detonation** (reuse of Sparrow Charge 5) | un-approved |
| D4 | Manta · Mass 5 | proposes **Deep Harvest** (overcharge pops + collects shielded prisms) | un-approved |
| D5 | Manta · Space 5 | proposes **Wide Wake** (near-field skimmer size class up while overcharged) | un-approved |
| D6 | Manta · Time 5 | proposes **Held Charge** (overcharge stops bleeding between skims) | un-approved |

D1 is the only one of the six that has no proposal at all. D2–D6 are four years of Manta design
sitting one markup away from implementable.

**A decision that is genuinely "leave it open" is a valid outcome and must be written down as
such** — an `UpgradeLabel` left empty with a `UpgradeDescription` explaining why (the Scarab Space
row is the exemplar) is a *finished* row, not a hole. What is not acceptable is an empty label
with no reason, which is what Manta, Rhino and Serpent carry today.

## 6. The prompts

Run these as separate branches. Each is self-contained, states its gate, and ends at a
verification hand-back. **P0 first** — the rest are written against a corrected paper trail.

---

**P0 · Fix the elemental-ability paper trail (no gameplay change)**

> The elemental ability docs contradict the shipped assets in four places. Fix the docs; change no
> asset and no code. (1) `FLEET_MAPS.md` §2 Sparrow says Mass 5 is "open again" and that Space 5
> grants both Piercing Bullets and shielded turret prisms — `Sparrow.asset` says Mass 5 = Shielded
> Prisms and Space 5 = Piercing Bullets, and CLAUDE.md records the 2026-08-13 sign-off that
> returned it; the asset is the record. (2) `FLEET_MAPS.md` §3 claims the Dolphin has 3/4 `Input`
> fields filled — it has 2, and its other two abilities are passive by design; say which. (3)
> `ARCHITECTURE.md` §7.2 "Fleet status" lists Squirrel/Sparrow/Dolphin as authoring the four-icon
> row — Scarab authors it too (4 bindings in `ScarabHUDVariant.prefab`). (4) CLAUDE.md's
> vessel-ability fleet table has no Scarab row and does not record that no `UrchinHUDVariant.prefab`
> exists. Add a pointer from `FLEET_MAPS.md` §1 to `Docs/ElementalAbilitySystem/COMPLETION_PUSH.md`.

---

**P1 · Urchin: author the HUD and its four-icon row** *(mechanical — no design gate)*

> The Urchin's elemental map is complete and approved (Charge Chain Spikes / Mass Trail Rider /
> Space Track Projector / Time Slip, all four level-5 upgrades shipped) but the vessel has **no
> HUD prefab at all** — `Assets/_Prefabs/UI Elements/VesselHUD/UrchinHUDVariant.prefab` does not
> exist, so `UrchinVesselHUDController` and `UrchinVesselHUDView` are unreferenced code. Author the
> variant from `VesselHUDPrefab.prefab`, then run **FrogletTools > Vessels > Wire Vessel Ability
> Row** to build the four-icon row at the fleet-standard bands and bind `abilityIcons` in charge →
> mass → space → time order. Bind the controller/view pair on `Urchin.prefab`. Add an
> `InputDeviceIconSetSwitcher` — preferably by authoring the variant from `VesselHUDPrefab.prefab`
> so it inherits one (§4.1) — and let `BindHintsToAbilities` derive hint placement; do not
> hand-position glyphs. Trail Rider is passive (`Input: 0` deliberately), so it gets an icon but no
> hint. Run **Audit Vessel Ability Rows** and **Audit Vessel Skimmers** and report both. This is
> tool output: follow `/ship-tools` so the prefab actually lands on the branch.

---

**P2 · Scarab: resolve the Space level-5** *(design gate D1 — STOP and ask first)*

> `Scarab.asset`'s Space row (Ball Forge, ×4 ball size at L10) has an empty `UpgradeLabel` and an
> `UpgradeDescription` that says the design notes name no upgrade and one must not be invented
> without sign-off (`SCARAB.md` §7). Do not author one. Present Garrett with the row's context —
> Space owns forged ball size, the map itself is the carrier at `MultiplierAtFullLevel: 4`, the
> ball is stamped once at forge time and never resized, and forging is passive (fly a skimmer
> through an omni crystal) — plus the three shipped Scarab upgrades it must not overlap
> (Cavitation Shear, Armored Switch, Snap Dash), and ask for either a level-5 or an explicit "stays
> open". Implement only what comes back. If it stays open, that is a finished row: leave the label
> empty and keep the reason in the description.

---

**P3 · Manta: design the map** *(design gates D2–D6 — design only, no implementation)*

> The Manta has three authored quantitative rows (Charge → overcharge detonation blast 1.75, Mass →
> overcharge harvest capacity 1.75, Space → Yawstery turn rate 1.6), an `(open design slot)` on
> Time, and **zero** level-5 upgrades. `FLEET_MAPS.md` §2 carries un-approved proposals for all
> five: Time → overcharge decay rate, and Held Charge / Domain-Safe Detonation / Deep Harvest /
> Wide Wake. Take those to Garrett row by row with the Manta's real wiring in hand — the prefab
> binds Yawstery on `OnlyRightStick 11` / `OnlyLeftStick 12`, `MantaAnalogTurnBoostAction` over all
> three gamepad events, and `BoostAction` on `BothSticks 13`, which **no map row currently claims**
> — and ask whether the boost should take the Time slot instead of overcharge decay. Do not author
> anything into the asset until each row comes back approved. Ground rules stand: reuse existing
> primitives, never SuperShield, never a timer or decay, gate in the acting system's layer.

---

**P4 · Manta: implement the approved map and the icon row** *(after P3 signs off)*

> With P3's approved rows: author them into `Assets/Resources/ElementalAbilityMaps/Manta.asset`
> (`AbilityLabel`, `AbilityDescription`, `Input`, `UpgradeLabel`, `UpgradeDescription`), fill the
> four `Input` fields from `Manta.prefab`'s real bindings and say per row whether a `0` means
> passive or unset, and implement each level-5 gated on `IsUpgradeActive(element)` with a per-use
> snapshot — never a raw local level read, which desyncs the prismscape across peers. Pin the
> map's `MultiplierAtFullLevel` to 1 for any row whose scaling is carried by an authored SO field.
> Run **FrogletTools > Vessels > Wire Vessel Ability Row** for the four icons and add an
> `InputDeviceIconSetSwitcher`. Write
> `Assets/_Scripts/Controller/Vessel/R_VesselActions/MANTA_OVERCHARGE.md` in the
> `RHINO_SHIELD_SWIPE.md` shape, and update FLEET_MAPS §1 + §2, ARCHITECTURE §7.2, BACKLOG and
> CLAUDE.md in the same branch.

---

**P5 · Fleet: close the passive-vs-unset `Input` ambiguity** *(mechanical, cheap, fleet-wide)*

> `InputEvents.FullSpeedStraightAction = 0`, so an `Input: 0` in an `ElementalAbilityMapSO` entry
> cannot be distinguished from an unset field — `FLEET_MAPS.md` §3 flags this and asks that each
> row say which it is. Seven rows across the shipped six are `0`: Dolphin Mass (passive seeding
> loop) and Space (no button — released by flying into a crystal), Squirrel Charge and Space (both
> passive/contact), Scarab Charge (rides the base-kit RT dash) and Space (passive forge), Urchin
> Mass (passive contact). Every one is genuinely passive. State that in each row's
> `AbilityDescription` so a future reader cannot mistake it for an unset field. Also record in the
> Squirrel's two non-zero rows that `12`/`11` are its **touch** bindings and gamepad uses `2`/`1`
> via `_gamepadActionOverrides`. Consider whether `ElementalAbilityEntry` should grow an explicit
> `IsPassive` bool so the ambiguity cannot recur — propose it, do not add it unilaterally.

---

**P6 · Rhino and Serpent: the next two** *(design gate — scope only, not in this push)*

> Below the cut line. Rhino has one authored row (Mass → trail slab max size 1.5) and Serpent one
> (Time → boost duration 1.6); both have three `(open design slot)`s, zero level-5 upgrades and
> 0/4 icon rows, and both have HUD variants already. `FLEET_MAPS.md` §2 carries un-approved
> proposals for all fourteen slots. Their real wiring: Rhino binds `RhinoRampBoostAction` +
> `GrowTrailAction` on `FullSpeedStraight 0` and shield swipe L/R on `LeftStick 2` / `RightStick 1`;
> Serpent binds `ConsumeBoostAction` on `Button1 6`, `ToggleStationaryModeAction` on `Button2 7`
> and `CloakSeedWallAction` on `RightStick 1`. Take the proposals to Garrett before any asset edit.

## 7. Drift fixed by this change

| Doc | Was | Is |
|---|---|---|
| `FLEET_MAPS.md` §2 Sparrow | Mass 5 "open again"; Space 5 grants pierce **and** shielded turret prisms | Mass 5 = **Shielded Prisms**, Space 5 = **Piercing Bullets** (matches `Sparrow.asset` + the 2026-08-13 sign-off) |
| `FLEET_MAPS.md` §3 | "Dolphin (3/4)" inputs filled | 2/4 named + 2 passive by design |
| `ARCHITECTURE.md` §7.2 | "Squirrel, Sparrow and Dolphin author the row" | + **Scarab** (4 bindings) |
| CLAUDE.md fleet table | no Scarab row; Urchin HUD absence buried in prose | Scarab row added; Urchin's missing HUD variant stated |
| CLAUDE.md / `ARCHITECTURE.md` | "no switcher on its HUD" read as a per-vessel oversight | recorded as one structural fact — four HUDs are hard copies of `VesselHUDPrefab`, not variants (§4.1) |

## 8. Verification

Everything in this doc is asset-derived and re-checkable without Unity. The auditors that confirm
the wiring half, once P1/P4 land: **FrogletTools > Vessels > Audit Vessel Ability Rows** (expect
Sparrow/Dolphin/Squirrel/Scarab/Urchin/Manta all ✅ 4/4 in order), **Audit Vessel Skimmers**,
**Audit Vessel Elemental Morphs**. Anything not editor-verified goes to
`Docs/UNITY_VERIFICATION_CHECKLIST.md` with a 🔴, never only a PR body.
