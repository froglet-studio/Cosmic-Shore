# Branch archive: `claude/vessel-abilities-elements-bt7bcx`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-08-24 by Claude
- **Unmerged commits:** 1
- **Forked from:** `cf7b92e02` (2026-08-24, Merge branch 'bleeding-edge' of https://github.com/froglet-studio/Cosmic-Shore)
- **Tip:** `8bf09717c`
- **Files touched (4):**
  - `CLAUDE.md`
  - `Docs/ElementalAbilitySystem/ARCHITECTURE.md`
  - `Docs/ElementalAbilitySystem/COMPLETION_PUSH.md`
  - `Docs/ElementalAbilitySystem/FLEET_MAPS.md`

### `8bf09717c` — docs(vessel): add fleet completion push, fix elemental map drift

_Claude, 2026-08-24 19:59:37 +0000_

```text
Measure the fleet's elemental-ability completion from the shipped assets
rather than from the status tables, and record the intent + work order that
closes the six closest vessels.

New: Docs/ElementalAbilitySystem/COMPLETION_PUSH.md
- re-derivable scorecard (map rows, L5 upgrades, Input bindings, icon rows,
  HUD prefabs) ranking Sparrow > Dolphin > Squirrel > Scarab > Urchin > Manta
- the shipped ability/parameter/element/upgrade table for those six
- separates DRIFT / MECHANICAL / DESIGN-GATED holes; names the 6 open design
  decisions with their existing FLEET_MAPS proposals, none of them invented
- seven sequenced prompts (P0-P6) that finish the push
- records that four HUD prefabs are hard copies of VesselHUDPrefab rather
  than variants, which is why their control hints cannot bind

Drift fixed against the assets:
- FLEET_MAPS §2 Sparrow: Mass 5 is Shielded Prisms (returned by sign-off
  2026-08-13), Space 5 is Piercing Bullets only
- FLEET_MAPS §3: re-measured Input counts incl. Scarab; passive vs unset
- ARCHITECTURE §7.2: Scarab authors the four-icon row; Urchin has no HUD
- CLAUDE.md: add the missing Scarab row and the forked-HUD note

No asset or code change; no open slot filled.
```

```text
 CLAUDE.md                                      |   7 ++
 Docs/ElementalAbilitySystem/ARCHITECTURE.md    |   8 +-
 Docs/ElementalAbilitySystem/COMPLETION_PUSH.md | 307 +++++++++++++++++++++++++++++++++++++++++++++++++++++++
 Docs/ElementalAbilitySystem/FLEET_MAPS.md      |  21 ++--
 4 files changed, 335 insertions(+), 8 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 398 lines)</summary>

```diff
diff --git a/CLAUDE.md b/CLAUDE.md
index 22e1b4d8a..0e7258d0f 100644
--- a/CLAUDE.md
+++ b/CLAUDE.md
@@ -2766,11 +2766,18 @@ scale bump** with a one-shot unlock punch.
 - **Fleet status** (audit it yourself: **FrogletTools > Vessels > Audit Vessel Ability Rows**, which
   reports every vessel's compliance against this contract from assets alone, no play mode):
 
+  Measured scorecard, the six open design decisions and the sequenced prompts that close
+  them: `Docs/ElementalAbilitySystem/COMPLETION_PUSH.md`. Only **Squirrel, Manta and
+  Serpent** HUDs are true variants of `VesselHUDPrefab` and so inherit its
+  `InputDeviceIconSetSwitcher`; Sparrow, Dolphin, Scarab and Rhino are hard copies and
+  their control hints cannot bind until one is added.
+
   | vessel | map | icons | order | uniform | hints |
   |---|---|---|---|---|---|
   | Squirrel | complete | 4/4 | ✅ | ✅ | ✅ bound |
   | Sparrow | 4/4 named, **4/4 upgrades** (Time re-scoped 2026-08: indefinite boost, base roll, Elemental Ward. **Mass L5 = Shielded Prisms again** — it briefly moved to Space 5 in 2026-08 round 4 and was returned by design sign-off on 2026-08-13, settling the split: **MASS owns the SUBSTANCE of what you fire** (turret prism stretch, in-flight round growth, armour) and **SPACE owns its REACH** (range, and pierce on both fire modes)) | 4/4 | ✅ | ✅ | ⚠ no switcher on its HUD |
   | Dolphin | complete | 4/4 | ✅ | ✅ | ⚠ no switcher on its HUD |
+  | Scarab | 4/4 named, **3/4 upgrades** — Space 5 is deliberately open (`SCARAB.md` §7 and the asset's own `UpgradeDescription` say do not invent one without sign-off) | 4/4 | ✅ | ✅ | ⚠ no switcher — its HUD is a hard copy, not a variant of `VesselHUDPrefab` |
   | Urchin | complete (4/4 named, 4/4 upgrades; re-cut 2026-08-18 — Charge owns the merged spike weapon, Space the new track projector) | 0/4 | — | — | n/a — **no `UrchinHUDVariant.prefab` exists**, so `UrchinVesselHUDController`/`View` are unreferenced code |
   | Manta | 3/4 named, 0/4 upgrades | 0/4 | — | — | n/a |
   | Rhino | 1/4 named, 0/4 upgrades | 0/4 | — | — | n/a |
diff --git a/Docs/ElementalAbilitySystem/ARCHITECTURE.md b/Docs/ElementalAbilitySystem/ARCHITECTURE.md
index 8eca28733..4c4258be1 100644
--- a/Docs/ElementalAbilitySystem/ARCHITECTURE.md
+++ b/Docs/ElementalAbilitySystem/ARCHITECTURE.md
@@ -284,8 +284,12 @@ an ability that *is* bound to an input but has no hint labelling it.
 Reassigning an ability to a different input event in the action handler, or moving an icon in the
 row, now carries the label along with no manual repositioning.
 
-**Fleet status.** Squirrel, Sparrow and Dolphin author the row (four buttons, four bindings, uniform
-pitch and slot size, charge → mass → space → time). The Dolphin runs with **both**
+**Fleet status** (re-measured from the prefabs 2026-08-24). Squirrel, Sparrow, Dolphin **and Scarab**
+author the row (four buttons, four bindings, uniform pitch and slot size, charge → mass → space →
+time). Manta, Rhino and Serpent have HUD variants with **no** `abilityIcons` bindings; the **Urchin
+has no HUD variant at all**, so its complete map has nowhere to draw. Nothing binds the row from a
+*vessel* prefab — checked, since the Rhino's row was once missed by looking only at the HUD side.
+See `COMPLETION_PUSH.md` for the full scorecard. The Dolphin runs with **both**
 `tintIconOnUpgrade` and `showUpgradeBadge` off, because all four of its icons are live gauges — the
 persistent scale bump is its only upgrade signal, and its Time-slot jaw tint is a *gauge* colour on
 the jaw halves, not an upgrade tint on the (transparent) Time icon. It has no
diff --git a/Docs/ElementalAbilitySystem/COMPLETION_PUSH.md b/Docs/ElementalAbilitySystem/COMPLETION_PUSH.md
new file mode 100644
index 000000000..804ea044e
--- /dev/null
+++ b/Docs/ElementalAbilitySystem/COMPLETION_PUSH.md
@@ -0,0 +1,307 @@
+# Fleet Completion Push — the six closest vessels
+
+**Dated 2026-08-24.** Measured from the shipped assets, not from any status table.
+Companion to `FLEET_MAPS.md` (the per-vessel record) and `BACKLOG.md` (the phase plan).
+This doc is the **intent** of the completion push and the **work order** that finishes it.
+
+## 0. Intent
+
+The elemental ability contract is *"four abilities, each owned by one of the four elements, each
+with a level-5 upgrade, each shown as one of four HUD icons in charge → mass → space → time
+order."* Four vessels satisfy it end to end. The fleet's remaining incompleteness is **not**
+evenly spread and it is **not** all the same kind of work, and conflating the two kinds is why
+the fleet has looked "nearly done" for several branches without closing.
+
+This push separates them and closes them in that order:
+
+1. **DRIFT** — places where a doc contradicts a shipped asset. Free to fix, and dangerous to
+   leave: the next branch reads the doc, not the asset. (§7)
+2. **MECHANICAL holes** — an approved design that is simply not wired: a missing HUD variant, an
+   unbuilt icon row, an `Input` field left at `0`. No sign-off needed, no design risk. (§5)
+3. **DESIGN-GATED holes** — an element with no ability, or an ability with no level-5. These are
+   blocked on Garrett and **may not be invented to green an auditor** (the `/vessel` skill's §3
+   gate). Every one of them is named here with its existing proposal, if it has one. (§5)
+
+The push explicitly does **not** re-litigate any shipped row. Sparrow, Dolphin, Squirrel and
+Urchin maps are the record; Scarab's is Garrett's 2026-08-15 markup. Where this doc disagrees with
+an older table, **the asset wins and the table is the bug**.
+
+## 1. How "closest to completion" was measured
+
+Five binary-ish facts per vessel, all readable from assets with no Unity:
+
+| Axis | Source of truth | Complete means |
+|---|---|---|
+| Quantitative rows | `Assets/Resources/ElementalAbilityMaps/{Vessel}.asset` — `AbilityLabel` | 4 entries, none `(open design slot)` |
+| Level-5 upgrades | same asset — `UpgradeLabel` | 4 non-empty |
+| Input bindings | same asset — `Input`, cross-checked against the prefab's `_inputEventShipActions` / `_gamepadActionOverrides` | every non-passive ability names its real event |
+| Ability icon row | `abilityIcons` in the HUD variant **and** the vessel prefab (the Rhino's row was once missed by checking only one) | 4 bindings, charge → mass → space → time |
+| HUD prefab | `Assets/_Prefabs/UI Elements/VesselHUD/{Vessel}HUDVariant.prefab` | exists |
+
+**`Input: 0` is ambiguous by construction** — `InputEvents.FullSpeedStraightAction = 0`, so a
+genuinely-unset field and a genuinely-passive ability are indistinguishable in the asset. This doc
+resolves each case against the prefab's real bindings and says which it is. Fixing that ambiguity
+per row is part of the mechanical work.
+
+## 2. The scorecard
+
+| # | Vessel | Quant | L5 | Icons | HUD prefab | Inputs named | Remaining work is… |
+|---|---|---|---|---|---|---|---|
+| 1 | **Sparrow** (11) | 4/4 | 4/4 | 4/4 | ✅ | 4/4 | drift only |
+| 2 | **Dolphin** (2) | 4/4 | 4/4 | 4/4 | ✅ | 2/4 + 2 passive | drift only |
+| 3 | **Squirrel** (6) | 4/4 | 4/4 | 4/4 | ✅ | 2/4 + 2 passive | drift only |
+| 4 | **Scarab** (12) | 4/4 | **3/4** | 4/4 | ✅ | 2/4 + 2 passive | **1 design gate** |
+| 5 | **Urchin** (4) | 4/4 | 4/4 | **0/4** | **❌ none** | 3/4 + 1 passive | **mechanical** (whole HUD) |
+| 6 | **Manta** (1) | **3/4** | **0/4** | **0/4** | ✅ | 0/4 | **5 design gates** + row |
+| — | Rhino (3) | 1/4 | 0/4 | 0/4 | ✅ | 0/4 | 7 design gates + row |
+| — | Serpent (7) | 1/4 | 0/4 | 0/4 | ✅ | 0/4 | 7 design gates + row |
+| — | Grizzly (5) | no map | — | — | ❌ | — | everything |
+
+**The cut line sits between Manta and Rhino** and it is a real gap: Manta has three authored
+quantitative rows, Rhino and Serpent have one each. Termite (8), Falcon (9) and Shrike (10) are
+`VesselClassType` members with no prefab, no map and no HUD, and are out of scope entirely.
+
+**Urchin ranks above Manta on purpose.** Its map is complete and approved; every hole it has is
+wiring a human can close without a design decision. Manta's holes need Garrett first. Ranking by
+"how much is left" would invert these two; ranking by "how much is left that needs a *decision*"
+is the ordering that actually predicts how fast a branch closes.
+
+## 3. The map as shipped — the six
+
+Rows are in contract order (charge → mass → space → time), which is also HUD left-to-right order.
+"Parameter" is what the element scales; where an authored SO field carries the scaling the map's
+generic `MultiplierAtFullLevel` is pinned to 1 (the no-double-dip rule).
+
+### Sparrow (11) — shooter · COMPLETE
+
+| Element | Ability | Parameter (scaling) | Input | Level-5 upgrade |
+|---|---|---|---|---|
+| Charge | Skyburst Rockets | blast radius — authored 100→170 on the four skyburst effect assets (map 1.0) | LT `2` | **Domain-Safe Skybursts** |
+| Mass | Turret Stance | turret-fired prism stretch (map **2.5**, min 0.4) | X `6` | **Shielded Prisms** |
+| Space | Pulsefire Cannons | gun range (map **9.0**, min 0.4) | RT `1` | **Piercing Bullets** |
+| Time | Afterburner | boost speed (map **1.5**, min 0.5) | A `7` | **Elemental Ward** |
+
+### Dolphin (2) — energy economy · COMPLETE
+
+| Element | Ability | Parameter (scaling) | Input | Level-5 upgrade |
+|---|---|---|---|---|
+| Charge | Echo Sight | blast capsule **thickness** — 0.75× the authored core at rest → 1.5× at L10 (map 1.0) | RT `1` | **Pilot Echo** |
+| Mass | Crystal Seeding | seeding recharge ×0.5 at L10 (`cooldownMultiplierAtFullMass`; map 1.0) | **passive** | **Claimed Seed** |
+| Space | Echo Obliteration | blast **reach** ×2 at L10 (`_heightMultiplierAtFullSpace`; map 1.0) | **no button** — released by flying into a crystal | **Clean Blast** |
+| Time | Charge Fill Rate | boost charge rate while drifting ×1.5 at L10 (map 1.0) | LT `2` | **Drift Ward** (scoped `DangerPrism`) |
+
+### Squirrel (6) — racer · COMPLETE
+
+| Element | Ability | Parameter (scaling) | Input | Level-5 upgrade |
+|---|---|---|---|---|
+| Charge | Skimming | skim energy per prism-skimmer hit (map **2.0**, min 0.25) | **passive** (contact) | **Live Wire** |
+| Mass | Trail Volume | trail prism VOLUME — authored `trailVolume` 1→2.5, cube-root per axis (map 1.0) | drift — asset records touch `12`, gamepad is LT `2` | **Heavy Trail** |
+| Space | Skimmer Reach | skimmer sphere `Scale` 15→30 (map 1.0) | **passive** | **Shepherd** |
+| Time | Boost Ring | ring cooldown ×0.5 at L10 (`cooldownMultiplierAtFullTime`; map 1.0) | asset records touch `11`, gamepad is RT `1` | **Twin Rings** |
+
+### Scarab (12) — hoop-court · 3/4 upgrades
```

</details>
