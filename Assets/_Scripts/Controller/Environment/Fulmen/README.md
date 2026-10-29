# Fulmen — cosmic thunderbolt (Charge fundamental)

Collecting an omni crystal fires a branching 3D bolt from the collector's **cell centre**. Its streams burn prisms
(through `PrismBurnManager`, server/offline only) and every omni crystal within reach of a branch spawns a smaller child
bolt (struck crystals are not consumed), so crystal clusters chain-react.

| Piece | Role |
|---|---|
| `FulmenMath` | Pure, seeded bolt-shape generator (EditMode tests: `FulmenMathTests`). |
| `FulmenConfigSO` | All tuning: reach, chain caps, growth, per-frame budgets, burn radius, shape, look. Listed in *Environmental Fundamentals*. |
| `FulmenSystem` | Scene singleton (made on demand). Pooled bolts/chains, budgeted segment queries + ignites, child chaining. |
| `FulmenView` | One world-space mesh for all bolts, added by `FulmenSystem`. |
| `FulmenByCrystalEffectSO` | The ability hook (`VesselCrystalEffectSO`). No vessel code. |

Assets: `_SO_Assets/Environment/FulmenConfig.asset`, `_SO_Assets/Effects/Vessel Crystal Effects/FulmenByCrystalEffect.asset`.

## Swapping the ability

* **Give Fulmen to another vessel (e.g. Gareet):** open its `…ImpactorDataContainer` asset
  (`_SO_Assets/Effects/Effect Containers/VesselContainers/`) and add `FulmenByCrystalEffect` to **Vessel Crystal Effects**.
* **Take it off the Stoat:** remove that entry from `StoatImpactorDataContainer`.

**Fulmen is the Stoat's omni-crystal ability (Yash, 2026-10-29: "Fulmen Network as Omni-crystal ability of Stout").**
It replaced the Stoat's Overclock: `StoatOverclockByCrystalEffect` is no longer in the container (the asset and its
code are kept, so it can be added back). The container now holds the haptics and Fulmen only.

The Meerkat had shared `StoatImpactorDataContainer` since it was first built (b8d6ef57a), so it would have fired Fulmen
and Overclock too. It now has its own `MeerkatImpactorDataContainer`: the same prism effects, haptics only for crystals.
Its omni-crystal ability is the Soliton Network, which `MeerkatSolitonExecutor` grows from
`OmniCrystalImpactor.CollectedByVessel` (MEERKAT.md section 9), not a container entry.
* **Different tuning per vessel:** duplicate `FulmenConfig` + `FulmenByCrystalEffect`, point the copy's *Config* at the new
  config, and reference the copy from that vessel's container.

No code changes are needed for any of these.
