# Fauna params for -arkEscortParams (DONE)

Branch `claude/voyage-fauna-params` (off `claude/voyage-ark-core`) in `D:\Voyage\wt-a`. No `.asset` edited; one prefab
(`TadPoleFauna.prefab`) and one small `LightFauna.cs` method.

## How it applies

- `ArkEscortParams` parses `fauna.*` keys into the run's `Target.Fauna` (`FaunaRuntimeTuning`, new) and `Target.World`.
- `ArkEscortRun.PrepareCellMetrics` hands each escort world `cell.RuntimeFaunaTuning` and the per-world scales
  (`RuntimePopulationScale *= populationScale`, `RuntimeFaunaFloorScale`, `RuntimeFaunaCapScale`).
- Each creature takes its species' overrides as the cell registers it: `Fauna.Initialize` and again after its variant
  tuning in `AssignLineage` (so a run's value wins over the config variant). Only cells carrying a tuning are touched.
- LightFauna and the worm colony get a per-run COPY of their data SO (`LightFaunaDataSO`, `WormColonyConfigSO`) with the
  overrides; Boid writes its own instance fields; bites are a Fauna-level override. Assets are never written.
- Floors and caps: new `Cell.ResolveFaunaFloor(cfg, authored)` (used by `RandomLifeSpawner` and
  `IntensityWiseLifeSpawner`) and `Cell.ResolveFaunaCap` both multiply in the world scale and the species scale.
  Untuned cells resolve exactly as before. Production gating only: nothing is culled to meet a lowered cap.

## Species keys

Prefab name lower-cased, "(Clone)", a leading "Mass" and a trailing "Fauna" removed: `shark`, `brittlestar`,
`quadfish`, `clawfish`, `tadpole` (the Scarab tadpoles and piranhas share this prefab), `wormcolony`. `all` = every
species (a species' own value wins over `all`).

## Keys (value out of range or wrong class = echoed `Applied:false`, never clamped silently)

| Key | Applies to | Range | Notes |
|---|---|---|---|
| `fauna.<species>.shieldBite` | any eater with the bite path (LightFauna, Boid, worm) | 0-5 | armour points per bite; a shield cracks at 1 |
| `fauna.<species>.superShieldBite` | same | 0-5 | a super-shield cracks at 2 |
| `fauna.<species>.minSpeed` / `maxSpeed` | LightFauna (data copy), Boid (instance) | 0-250 u/s | max is floored at min |
| `fauna.<species>.consumeRadius` | LightFauna | 0-3000 | shipped 33-81 |
| `fauna.<species>.detectionRadius` | LightFauna | 0-3000 | shipped 60-100 |
| `fauna.<species>.feedingClusterRadius` | LightFauna, Boid | 0-3000 | shipped 10-24 |
| `fauna.<species>.attackRange` | LightFauna predators | 0-3000 | shipped 12-30 |
| `fauna.<species>.huntInterval` / `huntDuration` | LightFauna predators, worm | 0-600 s | shark 20/10, worm 26/12; interval 0 = always hunting |
| `fauna.<species>.territory` | LightFauna predators (territoryRadius) | 0-3000 | 600 shipped; 0 = cell-wide |
| `fauna.<species>.pursuitSpeed` | LightFauna predators (multiplier) | 1-20 | 1.5 shipped |
| `fauna.<species>.huntSpeed` | Boid (huntSpeedMultiplier) | 1-20 | 10 shipped |
| `fauna.<species>.cruiseSpeed` / `lungeSpeed` | worm | 0-250 | 18 / 70 shipped |
| `fauna.<species>.aggroRadius` / `mouthRadius` | worm | 0-3000 | 220 / 46 shipped |
| `fauna.<species>.floorScale` / `capScale` | that species' seed floor / live cap in escort worlds | 0-4 | move together (ecology §4.6) |
| `fauna.<1-9\|all>.populationScale` | world N (1-based leg), multiplies the tier population | 0-4 | escort.maxPopulation is not re-applied |
| `fauna.<1-9\|all>.floorScale` / `capScale` | every species in world N | 0-4 | `fauna.all.floorScale` is the world-all form |

Example: `{"fauna.shark.maxSpeed": 40, "fauna.all.shieldBite": 0.3, "fauna.5.capScale": 1.5, "fauna.quadfish.floorScale": 0.8}`.
Note for the collider budget: raising caps raises body prisms per world (state body prisms x cap before a sweep goes high).

## Other changes

- Plate eating extended to the worm colony (`WormFauna.TickFeeding` through `Fauna.BiteMass`; new
  `WormColonyConfigSO.ShieldBite / SuperShieldBite`, default 0, settable by `fauna.wormcolony.shieldBite`).
- `TadPoleFauna.prefab` Boid bites: shield 0.25, super 0.1 (4 holds per shield, 20 per super).
- `Fauna.ArmourBite` is now the tuned value else `AuthoredArmourBite` (the per-class override).
- Tests: `ArkPacingTests` gains species-key normalization, fauna/world key routing and refusal, and the data copy.

## Not done

- Swarm and Substrate eaters still never gnaw armour (they consume through their own data-population paths; not a
  small isolated change). Builders and the NCA lizard likewise.
- Substrate/swarm/builder species fields are not in the registry (their configs are big per-species tables; add by name
  with `ArkEscortParams.Register("fauna.{species}.<field>", ...)` plus a copy in `FaunaRuntimeTuning`).
- `FeedsPerOffspring`, reproduction cooldown, bands and element are not tunable yet (they live on the shared
  `FaunaConfigurationSO`, which would need a per-run config copy wired through the spawners).
- Runtime data copies are not destroyed at run end (creatures in still-draining cells read them); a few small SOs per run.
