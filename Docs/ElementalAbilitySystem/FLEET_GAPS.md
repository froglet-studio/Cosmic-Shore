# Fleet elemental gaps — what is INCOMPLETE, and what merely looked it

**Measured 2026-09-18** with `python3 Tools/Build/element_ability_table.py --gaps`, after the
element-scaling unification (`ELEMENT_SCALING_UNIFICATION.md`) and its follow-up pass.

This is the vessel-**incompleteness** report the refactor asked for. Everything here is a
DESIGN gap: a slot nobody has filled. Every wiring inconsistency the same pass found was fixed
rather than reported, and §3 lists those so the two are never confused again.

---

## 1. The open design slots (5 rows, 2 vessels)

Both vessels are `(open design slot)` in their own map asset — the entry exists, names no
ability, binds no input, and nothing in code reads that element on that hull. Neither is a
regression; neither has ever been filled.

| Vessel | Element | State | What filling it costs |
|---|---|---|---|
| **Rhino** | Charge | no ability, no scaling, no L5 | design + one `ElementalFloat` + (optionally) one `IsUpgradeActive` gate |
| **Rhino** | Space | no ability, no scaling, no L5 | as above |
| **Serpent** | Charge | no ability, no scaling, no L5 | as above |
| **Serpent** | Mass | no ability, no scaling, no L5 | as above |
| **Serpent** | Space | no ability, no scaling, no L5 | as above |

`Docs/ElementalAbilitySystem/FLEET_MAPS.md §2` carries the un-approved PROPOSALS for these ten
rows (four Rhino, four Serpent, minus the two already filled). They are proposals, not a
backlog: **do not implement one without sign-off**, and do not invent a mapping to make the
audit green — an invented mapping is worse than an honest hole, because the next reader cannot
tell it from a designed one.

### What the two vessels DO have

| Vessel | Element | Ability | Live scaling | L5 |
|---|---|---|---|---|
| Rhino | Mass | Trail Slabs | `massMaxSizeMultiplier` ×1 → ×1.5, floored ×0.25 (`GrowTrailAction.asset`) | — |
| Rhino | Time | Ramp Spool | `timeAccelerationMultiplier` ×1 → ×2.5, floored ×0.5 (`RhinoRampBoostAction.asset`) | — |
| Serpent | Time | Boost Duration | `timeDurationMultiplier` ×1 → ×1.6, floored ×0.25 (`ConsumeBoostAction.asset`) | — |

⚠ The Rhino's **Time** and the Serpent's **Time** each lost an undeclared SECOND application of
Time when the generic channel was removed (ramp ceiling ÷2.5, boost speed ÷1.6 at Time 10).
Neither number was ever authored as a design. **Both still need a playtest** — BACKLOG 5.2.

---

## 2. Level-5 slots deliberately left unfilled (3 rows)

These vessels' abilities are complete; the qualitative UPGRADE is the open slot. Each says so
in its own map asset, and the table prints `L5 -` with no disagreement flag.

| Vessel | Element | Ability | Why there is no upgrade |
|---|---|---|---|
| **Manta** | Time | Soar | "Wake Highway" was built and **cut** in 2026-09 after its first playtest — a boost ring read as an unexplained launcher. Nothing replaced it. |
| **Scarab** | Space | Ball Forge | Deliberate: the design notes give Space the ball's SIZE and name no qualitative upgrade (`SCARAB.md §7`). |
| **Scarab** | Mass | Switch | **"Armored Switch" was retired on 2026-08-24** with the switch's prism fill (`ScarabSwitch.cs`, `SCARAB.md §5.1` "Superseded"). Nothing replaced it. |

The Scarab's Mass row is the one worth reading twice. The map went on **declaring** Armored
Switch for three weeks after the code retired it, and the entry's `AbilityDescription` still
described the prism fill as a thing Mass grows. Every surface that asks the map — the HUD
lockup, the launch panel's controls block, this documentation set — reported a wired upgrade.
It was corrected on 2026-09-18 to a `(open design slot)` entry that records the retirement.

> **The general rule: retiring a MECHANIC is not finished until the DECLARATION that names it
> is retired too.** A map entry is not documentation, it is data several live surfaces read.

---

## 3. What was NOT incompleteness — fixed, not reported

The same audit first reported **12** disagreements. Seven of them were the tools and the data
lying about the code, not the code being unfinished, and all seven are fixed. They are listed
because each is a shape that will recur.

| # | Looked like | Actually was |
|---|---|---|
| 1 | Manta / Rhino / Squirrel each had a stray Scarab "Snap Dash" gate | `follow_static_calls` matched a bare `TypeName.` **inside a doc comment and two `[Tooltip]` strings**. Un-indexed scripts were read RAW, so comments were live text. Now comment- AND string-blanked. |
| 2 | **Manta Charge + Manta Space: "UPGRADE IS PROSE — no live gate"** | Both upgrades are fully implemented (`MantaStingActionExecutor.cs:295-296`). The executor writes `IsUpgradeActive(CosmicShore.Data.Element.Charge)` — **namespace-qualified** — and the tool's pattern was anchored on the bare `Element.X` spelling, so it read "Charge" as a serialized field name and dropped the gate. |
| 3 | **Squirrel Space: "NO SCALING — this element changes no number"** | `Skimmer.Scale` is authored 15 → 30 on Space, as a **nested-prefab-instance override** — scattered `m_Modifications` entries rather than a contiguous block. Six of twelve vessel prefabs author a float this way and all six were invisible. |
| 4 | Scarab Space named the skimmer radius, not the ball | `ScarabBallForge.BallSizeScale` is a C# `static readonly` (that class is static and can hold no serialized field), so its endpoints exist only in a field initializer. The tool now reads those too — which also covers the rule-4-i case where an asset predates the field. |
| 5 | Rhino Mass / Scarab Mass "gate exists but the map names no upgrade" | Both gates are branches guarded by `massUpgradeShieldsTrail` / `turnUpgradeShieldsTrail`, **absent from those prefabs' YAML and initialized `false` in C#**. `guard_state` only read authored YAML, so an unauthored bool counted as live — on ten of twelve hulls. |
| 6 | Rhino Mass appeared to scale twice | `GrowTrailActionSO.maxSize` was authored `Enabled` with a 4 → 8 Mass ramp **that had never run once**: it is declared on a ScriptableObject and read as `.Value`, and the binder reflects over MonoBehaviours only. Two more shipped the same way (`GrowSkimmerAction.shrinkRate` Charge 6 → 2, `FullAutoAction.speedValue` Space 375 → 4875). All three authored `Enabled: 0`; behaviour is byte-identical. |
| 7 | — (found while auditing 6) | `ElementalFloatBinder` was dead AND broken: it set a property named `"Ship"` that does not exist (`?.SetValue` → silent no-op) and its "clone" copied only `Value`, dropping `Min`/`Max`/`element`/`Enabled`. Its one reference was a commented-out call. Deleted. `AOERadialBlocks.depthScale` was a `private` ElementalFloat with **no `[SerializeField]`** — unserializable, permanently 1, and multiplying pooled instance fields, so a non-1 value would have compounded on reuse. Deleted. |

A footnote on #1 that is worth more than the fix: **the same defect then appeared in the new
gate**, within the hour. `check_elemental_floats.py` scanned raw C# for `ElementalFloat <name>`
declarations, and the comment recording #7's deletion quotes the deleted declaration verbatim —
so the removed field was back in the tool's inventory, from its own obituary. Both scanners now
blank comments and string literals. *A tool that matches code by pattern must not read prose,
and prose quotes code constantly — including the prose you write to explain the deletion.*

The standing guard against #6 is **`Tools/Build/check_elemental_floats.py`** (`--check`,
`--self-test`). It fails the build on any ElementalFloat that is authored `Enabled` with
`Min != Max` on a ScriptableObject nothing evaluates. It is keyed on the asset's own
`m_Script` guid, because a field NAME is not an identity — `maxSize` is declared on two
ScriptableObjects *and* on the MonoBehaviour `GrowActionBase`, and a name-keyed first cut
exempted both SOs. It also **states how many blocks it scanned**: its first version computed
`ROOT` one directory too high, scanned nothing, and reported OK.

---

## 4. One thing that was reported as a defect and is NOT

`ResourceSystem.GetLevel` is `FloorToInt(effective * 10)`, and `BACKLOG 5.5` recorded the
classic worry: `0.7 * 10` is `6.999999999999999`, so the floor is 6 and a pilot on level 7
reads as 6 — which would move the level-5 unlock test and the HUD petals together.

**Measured, in real C#: it does not happen.** The arithmetic is float32, not double. `0.7f` is
`0.699999988`, and `× 10` rounds to *exactly* `7f` (the error is under half an ulp at that
magnitude). Crystal progression also accumulates `+= 0.1f`, which drifts **upward**, away from
the boundary. All 26 cases — 15 accumulated steps and 11 direct ones — land on their integer.

Locked as `ElementalScalingUnificationTests.CrystalProgressionLandsOnEveryIntegerLevel`, as a
**standing refutation**: the hypothesis is refuted once rather than re-derived by the next
person who reads `FloorToInt` and reasons in double.

> **When a model and a build disagree, find out which one is wrong before recording it as a
> defect.** This one cost a backlog row and nearly cost an unlock-threshold change.

---

## 5. Still open, tracked in `BACKLOG.md`

| # | Item | Kind |
|---|---|---|
| 5.2 | Rhino + Serpent playtest (each lost an undeclared second Time application) | balance |
| 5.4b | **Six** ElementalFloat fields on five MonoBehaviour actions under `VesselActions/` (the pre-`R_` generation, still live on Falcon, Shrike and **Urchin**) read `.Value` and rely on the legacy bound path. Converting them to `EvaluateLive` is behaviour-neutral and needs the editor. | cleanup |
| 5.7 | **Nine** SO-hosted `ElementalFloat` fields are only ever read as `.Value` and should be plain `float`s (`DangerHemisphereConfigSO.depthScale`; `ConsumeBoostActionSO.boostMultiplier`; `FireGunActionSO.projectileTime`; `FullAutoActionSO.speedValue`; `GrowSkimmerActionSO.maxSize`/`shrinkRate`/`boostMultiplier`; `GrowTrailActionSO.maxSize`/`shrinkRate`). They cannot scale, so the type is the lie; the gate in §3 covers the dangerous case (an authored ramp) but not the misleading type. Changing a serialized field's type needs the editor. | cleanup |

Nothing in this document has been run in the Unity editor.
