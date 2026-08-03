---
name: ecology
description: Use for ANY change to the Cosmic Shore cell ecosystem — flora, fauna, cells, crystals, spawn profiles (SpawnProfileSO / CellConfigDataSO), phase/volume (Cell.LiveVolume, CellPhaseThresholds), lifeform powerups (LifeFormCrystal / ElementalCrystal*), wither/starvation, predator-prey, reproduction, evolution, or biome/intensity tuning. Loads the locked invariants + the collider-budget gate + the change protocol so changes start from the correct design and stay performant. Trigger when editing Assets/_Scripts/Controller/Environment/** (Cell, RandomLifeSpawner, Flora*, Fauna, LightFauna), the ecology SOs, or Docs/ECOSYSTEM*.md.
---

# Ecology Change Protocol

You are changing the Cosmic Shore cell ecosystem — a **platform fundamental** on the path to
credible artificial life. The system's design intent has caused repeated rework when guessed at;
this protocol exists to prevent that. Follow it exactly.

## 1. Read the canon first
- `CLAUDE.md ▸ Ecosystem Design Principles (LOCKED)` — the invariants (authoritative).
- `Docs/ECOSYSTEM_MASTERPLAN.md` — north star, the artificial-life scorecard (§3), the **collider
  contract (§4)**, the platform-wiring plan (§5), the phased roadmap (§6), the orchestration (§7).
- `Docs/ECOSYSTEM.md` — the mechanics log (how the current system actually works).

## 2. Restate before you edit (this kills the #1 source of rework)
In one or two lines, state which invariants the change touches and confirm it violates **none**:
**continuity of existence** (nothing pops in/out — everything grows/fades/suctions/withers; PLATFORM-WIDE) ·
no imposed death/decay/lifespan · no domain asymmetry (controlling-color spawn only) ·
wither-to-crystal + mass conservation · volume is the spine (not count) · the lifeform→elemental-
crystal invariant · territorial permanence (don't cull the dominant canopy) · endogenous
selection only (survival = fitness, never a scripted fitness function) · the collider budget.
**If a change might violate one, STOP and ask (AskUserQuestion). Do not guess the design.**

## 3. Implement (emergence first, surgically)
- **Favor emergence:** never hard-code an outcome that should emerge from the fundamentals
  (Domain · Mass/prisms · Cells · Elementals · Flora & Fauna · Vessels) interacting. A scripted
  outcome is the same bug as a scripted fitness function — it breaks the gameplay *and* the
  artificial-life claim. Order of preference: use a fundamental → tune it → extend it →
  (with sign-off) propose a new one → bespoke only as last resort.
- **Config-driven:** tunables in ScriptableObjects; cross-system comms via SOAP events/variables;
  no singletons/static events. Variety = biome × intensity × heritable traits, not bespoke code.
- **Surgical:** match surrounding style; three similar lines beat a premature abstraction.

## 4. Respect the collider budget (HARD GATE — perf is collider-bound)
- State the change's impact on **active colliders per cell**.
- Prefer the Burst `BlockDensityGrid` / `PrismSpatialIndex` (`QuerySphere` /
  `IsAnyPrismWithin` / `TryReserve` — see `Docs/SPATIAL_INDEX.md`) for spatial queries over
  `Physics.OverlapSphere` and over adding colliders. Fauna senses already ride the index.
- Honor collider-LOD-by-phase (prism colliders disabled at Frozen) and the per-cell budget.
- If a change adds colliders or queries, say explicitly how the budget stays met.

## 5. Hand back verification — you cannot run Unity; the human is the gate
- State the exact in-editor steps to verify, the scene to test, and the precise SO knobs to tune.
- Use the collider/volume telemetry overlay when it exists to make the budget observable.
- Never claim something works that you have not seen work. Report honestly (failures, skips, caveats).

## 6. Commit
One coherent step per commit; conventional-commit message; develop on the feature branch (never
`bleeding-edge`); open a PR only when asked. After lifeform-prefab changes, note to run
`FrogletTools ▸ Validation ▸ Validate Lifeform Crystals`.
