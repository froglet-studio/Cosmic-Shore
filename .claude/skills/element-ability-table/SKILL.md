---
name: element-ability-table
description: Query the fleet's element → ability → level-5-upgrade table — which ability each element owns on each vessel, what its L5 upgrade is and where that upgrade is actually gated, and how the ability scales with its element (with the authored numbers). Reads the shipped assets and code, never the docs. Use when asked what an element does on a vessel, what a level-5 upgrade is or whether one is really wired, what a map's MultiplierAtFullLevel actually drives, whether an element slot is an open design slot, or before touching Assets/Resources/ElementalAbilityMaps/**, an ElementalAbilityMapSO, ElementalScaling, ElementalFloat, or any IsUpgradeActive gate.
---

# Element ability table

```bash
python3 Tools/Build/element_ability_table.py                  # whole fleet (~1s)
python3 Tools/Build/element_ability_table.py Dolphin Sparrow  # named vessels
python3 Tools/Build/element_ability_table.py -e Space         # one element, fleet-wide
python3 Tools/Build/element_ability_table.py --gaps           # only rows that disagree
python3 Tools/Build/element_ability_table.py -v Dolphin       # + the authored prose
python3 Tools/Build/element_ability_table.py --json           # machine-readable
```

Run it **before** answering any question about what an element does, and before editing a map
asset. It is a READER — it writes nothing, needs no ship contract (`Docs/TOOLING.md`), and never
opens Unity.

## What it is joining, and why that is the whole point

An element's ability is **declared** in one place and **implemented** in others, and the three
drift. `/vessel` SKILL.md §2 states the rule this tool automates: *docs drift; the map asset, the
prefab and the code are the record.*

| Source | What it is the record of |
|---|---|
| `Assets/Resources/ElementalAbilityMaps/{Vessel}.asset` | the DECLARATION — ability name, input, unlock/relock levels, latch policy, the L5 upgrade's name and prose. **No numbers**: the generic multiplier was retired 2026-09-18 |
| `IsUpgradeActive(Element.X)` call sites | the L5 UPGRADE, actually. The replicated `NetElementUnlocks` bit is the only thing that makes an upgrade real. **An `UpgradeLabel` with no reachable gate is prose.** |
| the four scaling channels below | the SCALING, actually |

The tool resolves which of those call sites a given vessel **reaches** — a reference walk from
`{Vessel}.prefab` through its wired action SOs, its executors and vessel-root components, and its
impact-effect containers, plus one hop through static calls. Never a naming convention. Where a
site takes its element or its endpoints from a serialized field, the value is read from the asset
instance the walk actually arrived at, so the numbers printed are **that vessel's** numbers.

## The four scaling channels

A row is only "no scaling" if all four are absent. This is the part that is easy to get wrong by
grepping.

1. ~~**Generic map multiplier**~~ — **RETIRED 2026-09-18** (`ELEMENT_SCALING_UNIFICATION.md`). It
   addressed an ELEMENT and not a parameter, so every reader of that element got it. A leftover
   `MultiplierAtFullLevel` in an asset is now a loud finding (`RETIRED CHANNEL PRESENT`), never a
   tuning value.
2. **Bespoke authored endpoints** — `ElementalScaling.Multiplier` / `MultiplierFromRest` /
   `RoundGrowthFactor` with a `…AtRest<Element>` / `…AtFull<Element>` pair on an action or effect
   SO. Printed as `×a at rest → ×b at L10`.
3. **`ElementalFloat`** — pure serialized data (`Enabled`/`Min`/`Max`/`element`) with **no call
   site at all**; `EvaluateLive` lerps Min→Max over level/10. Printed as `a → b across L0..L10`.
   The Squirrel's Mass slot is only this.
4. **A direct level read** feeding a bespoke lerp (`GetLevel(Element.X)` +
   `…AtResting<Element>` / `…AtFull<Element>` beside it). Printed as absolute values, not a
   multiplier. The Urchin's Slip is only this.

Several channels on ONE element is now ordinary and correct — the Sparrow's MASS scales turret
prism stretch, bullet growth and missile growth: three parameters, one element. The old
**TWO LIVE SCALING CHANNELS** flag is gone with the channel whose specific hazard it guarded
(generic + bespoke landing on the same number), which cannot happen once every channel names its
own parameter.

## Reading the flags

| Flag | What it means |
|---|---|
| `OPEN DESIGN SLOT` | the map authors nothing. **Blocked on design, not wiring** — see the gate below. |
| `UPGRADE IS PROSE` | `UpgradeLabel` is authored and no live gate is reachable |
| `RETIRED CHANNEL PRESENT` | a generic map multiplier is still authored, or something still calls `handler.Multiplier(element)` |
| `NO SCALING` | none of the four channels is live for this element |
| `gate exists but the map names no upgrade` | a SHARED effect SO puts a gate in this vessel's reach (every hull carries a crystal-explosion effect and a `VesselPrismController`) while the map declares no upgrade. Usually noise. |
| `(inert - authored ×1)` | a bespoke endpoint pair authored ×1 → ×1, i.e. "this element does not scale me" |
| `(inert - authored ×1)` | a bespoke endpoint pair authored ×1 → ×1, i.e. "this element does not scale me" |
| `[OFF: field = 0]` | the code is reachable but a serialized bool in the same condition is authored **false on this hull** — the Dolphin reaches the Squirrel's Heavy Trail gate with `massUpgradeShieldsTrail: 0` |

## The gate that still applies

**Never invent an element→ability→input mapping or an L5 upgrade to fill an open slot or to
clear a flag.** Open slots on Manta / Rhino / Serpent are blocked on design; proposals live in
`Docs/ElementalAbilitySystem/FLEET_MAPS.md` §2 and are un-implemented until Garrett marks them
up. A flagged row is a finding to REPORT, not a defect to silently fix — several are shipped,
play-tested behaviour. For any actual edit, use the `/vessel` skill.
