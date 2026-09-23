# Prompt — author the Urchin's HUD

Paste everything below into a fresh session.

---

The Urchin is the only vessel in the fleet whose elemental ability map is **complete** — four named
abilities, four level-5 upgrades — and which has **no HUD prefab at all**. There is no
`UrchinHUDVariant.prefab`, so `UrchinVesselHUDController.cs` and `UrchinVesselHUDView.cs` are
unreferenced code and the vessel flies with nothing on screen telling the player what it can do.

It locks two shipped modes — **Hijack** (the rail heist) and **Skein** (the cable race) — so this is
a hull outsiders will fly in the invite build.

Use the `/vessel` skill for this work. It loads the fleet-wide vessel contract, the audit tools and
the per-subsystem checklists so the requirements are not re-derived.

Read `Docs/ABILITY_LOCKUP.md`, `Docs/ElementalAbilitySystem/ARCHITECTURE.md` §7.1, and the three
Urchin ability docs in `Assets/_Scripts/Controller/Vessel/R_VesselActions/` — `URCHIN_CHAIN_SPIKES.md`,
`URCHIN_TRAIL_RIDER.md`, `URCHIN_TRACK_PROJECTOR.md`.

## What the map already says — measured 10 Sep 2026 from `Assets/Resources/ElementalAbilityMaps/Urchin.asset`

The row is fixed by the element contract: **charge → mass → space → time, left to right**, matching
the flower order above it, so position alone answers *which flower do I fill to upgrade this*.

| Element | Ability | Level-5 upgrade |
|---|---|---|
| Charge | **Chain Spikes** | Overcharge |
| Mass | **Trail Rider** | Reinforced Wake |
| Space | **Track Projector** | Long Haul |
| Time | **Slip** | Slipstream |

Nothing here needs designing. The map is authored, the abilities are implemented and documented,
and the upgrades are named. This is a wiring and authoring task.

## The mechanical half is one click

**FrogletTools > Vessels > Wire Vessel Ability Row** (`VesselAbilityRowWirer`) places the four
buttons at the fleet-standard bands, creates a `{Element}Icon` in each, and binds `abilityIcons` in
`AbilityDisplayOrder` — on any vessel, from nothing. It is idempotent (find-by-name, re-bind only)
and never touches sprites, so it is a repair path as well as a bring-up path.

**The ability lockup is structural, not opt-in.** `VesselHUDController.Initialize` calls
`VesselHUDView.EnsureAbilityLockup`, which every vessel HUD routes through — so the Urchin gets the
totem, the control chips and the four-card row by *existing*, before a single icon is authored. A
slot with no icon renders **LOCKED** rather than blank. Do not hand-build any of that.

**Control chips are drawn, not authored.** The card derives its own glyph: ability → its map
entry's `InputEvents` → `InputHintBindingMap.BindingFor` → the physical control → sprite or label
from `Resources/ControlGlyphSet`. A wrong label is structurally impossible, and you author no glyph
art. A passive ability correctly draws nothing.

## What actually needs a decision

**1 · Which gauges the Urchin wants.** The fleet's gauge is a **linear fill masked to the ability
plate**, never a ring, and a vessel binds an existing meter `Image` as `AbilityIconBinding.gauge`
while continuing to write `fillAmount` on the very same object — so no drive site changes. The
cooldown is separate: a **clockwise depleting radial veil over the icon**, pushed as a value via
`SetAbilityCooldown(element, remaining01)`, not an `Image` the vessel binds.

Candidates, from the ability docs — confirm each against the code rather than trusting this list:

- **Chain Spikes** carries a hold-and-release charge whose length sizes an omni burst. That is a
  gauge.
- **Track Projector** runs on the Squirrel boost ring's 20-second cooldown. That is the veil.
- **Trail Rider** is a state (attached / riding / launched) more than a quantity — decide whether
  it reads better as a gauge or as nothing.

Read `UrchinVesselHUDView.cs` first: it may already declare fields for these, in which case the
answer is already written down.

**2 · Note the gauge-under-the-wrong-button trap.** A meter is regularly authored under a
*different* ability's button than the ability it reports on — the Squirrel's boost fill sits under
its skimming button, the Scarab's ball energy under its throttle button. The lockup re-homes every
gauge it finds, and `Build` adopts **every** gauge before retiring **any** chrome, which is why the
order matters. If you author a gauge, put it where it belongs and let the lockup place it.

## Constraints

- **Author the prefab as a variant**, matching `SparrowHUDVariant` / `ScarabHUDVariant`, and wire it
  on `Urchin.prefab`. Do not fork `VesselHUDPrefab`.
- **Check for an instance override before believing the variant.** `Squirrel.prefab` overrode its
  Time icon's `m_Sprite` on the nested HUD instance, so the variant's value had been dead for as
  long as the override existed. Dump the vessel prefab's `m_Modifications` as raw lines — the entry
  wraps `- target:` across two lines, so a one-line regex reports zero overrides on a 166-override
  instance.
- **`NormaliseHudRoot` makes the HUD root the screen.** The row anchors to `(1,0)` of it. Do not
  give the Urchin's root a bespoke rect.
- Do not re-author any icon's scale to match the row — the lockup derives it as
  `iconBoxSize / the icon's authored size`.

## Definition of done

1. `Assets/_Prefabs/UI Elements/VesselHUD/UrchinHUDVariant.prefab` exists and is referenced by
   `Urchin.prefab`, with no instance override shadowing it.
2. **FrogletTools > Vessels > Audit Vessel Ability Rows** reports the Urchin at 4/4 icons, correct
   order, uniform, with control chips drawn.
3. **FrogletTools > Vessels > Audit Ability Lockups** passes for the Urchin.
4. Any gauge is bound through `AbilityIconBinding.gauge` on the object its existing drive site
   already writes, and any cooldown is pushed through `SetAbilityCooldown`.
5. The tool's asset output is committed — run `/ship-tools` if a wirer was used, so the prefab
   lands on the branch rather than sitting in the working tree.
6. `Docs/STEAM_RELEASE_TASKS.md` R11 is ticked, and the PR's *Verification status* says whether the
   HUD was seen in play.
