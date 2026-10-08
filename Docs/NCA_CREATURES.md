# NCA creatures: the lab's trained animals as game fauna

The research branch (`cece/gifted-curie-x2cpd0`, `Tools/NCA/`) trained three 3D **neural cellular automata**: a lizard, a
whale and a jellyfish. Each one grows its whole body from a single seed cell, swims with a stroke that lives inside
the network (there is no animation and no clock input), and regrows any hole cut in it. This document covers the
lizard's port into the game and how the whale and the jellyfish follow it.

| piece | file |
|---|---|
| the network, in pure C# (no UnityEngine) | `Assets/_Scripts/Controller/Environment/FloraAndFauna/NcaCreature/NcaVoxelCore.cs` |
| the creature (glue) | `.../NcaCreature/NcaCreatureFauna.cs` |
| every gameplay number | `.../NcaCreature/NcaCreatureConfigSO.cs` → `Assets/_SO_Assets/NCA Creatures/LizardNcaConfig.asset` |
| the trained weights | `Assets/_SO_Assets/NCA Creatures/LizardNcaWeights.json` |
| the prefab | `Assets/_Prefabs/FloraAndFauna/NcaLizardFauna.prefab` |
| where it lives | `Assets/_SO_Assets/NCA Creatures/Swarm Middle NcaLizard Fauna Config Data.asset` (the Swarm demo cell) |
| generator (owns all of the above) | `python3 Tools/Build/author_nca_creatures.py [--check]` |
| parity + behaviour gates | `bash Tools/Build/nca_creature_harness/run.sh` |

## 1. The core matches the lab exactly

`NcaVoxelCore` is the sparse step of the lab runtime (`Tools/Ecology/flight/creatures/nca_creature.src.js`) line for
line. It uses the same alive mask, the same row-major mulberry32 fire draws, and the same zero-padded 3x3x3 Sobel
perception. The per-cell MLP (64 → 128 ReLU → 16, residual) is evaluated the way the lab's WebAssembly SIMD kernel
(`nca_kernel.c`) evaluates it: `System.Numerics.Vector4` lanes in the same accumulation order.

The harness runs the lab's own built `nca_creature.js` on the research weights and requires the C# state to match it
at steps 1, 50, 200 and 300, including a radius-7 cut at step 200. It must match bit for bit against the wasm backend
and within 1e-5 against the JS backend. A negative control nudges one weight and must fail.

The harness also gates:

- growth;
- the swim bend;
- cut-and-regrow;
- scars held until paid;
- threaded vs inline stepping;
- segment order;
- surface culling;
- the body-frame round trip;
- death with respawn off.

It also re-measures `grown_voxels` / `grow_steps`, which the generator writes into the weights file.

Without the research ref, parity is skipped (loudly) and the gates still run. One step of the grown lizard takes
about 4.7 ms on CoreCLR. It runs on a worker thread; see §6 for players.

## 2. What the glue adds

**Body.**
- Every visible voxel is drawn as one prism entity: alpha > 0.05, interior voxels culled.
- The entity uses the `HealthBlock` mesh in the theme's per-domain **Block** material. That is the material a live
  prism of the creature's domain wears.
- Each prism lies on the skin like the lab renderer's scales:
  - its y axis runs along the outward normal (−∇alpha);
  - its z axis trails toward the tail;
  - its shape is `ScaleShape` (1.35, 0.6, 1.55) voxels;
  - its size is `smoothstep((a−0.12)/0.43)·(0.75+0.25a)`, so the growing and healing fringe draws small.
- The drawn state is interpolated between the last two published steps.
- There are no colliders on the body.

**Stepping.**
- `NcaVoxelTicker` runs one step at a time on the thread pool at `StepsPerSecond` (20; 30 while bolting).
- On WebGL it runs inline at `InlineStepsPerSecond`.
- A step only starts once the previous one is collected, so a slow device simply steps slower.
- Wounds and meals are applied to the network only between steps.

**Being hit.**
- The body is split into `Segments` (8) slabs along its long axis. Each slab is one **virtual** `PrismSpatialIndex`
  entry, so AOE, hitscan and projectiles find it.
- A weapon that must apply gameplay calls `MaterialiseVirtualPrism`. That spawns **one** transient, owner-hidden
  `HealthPrism` at the segment, and only one stands per creature at a time (`HasMaterialiseBudget` defers bulk paths
  meanwhile).
- When the hit prism explodes, `OnBodyPrismExploded` cuts a `BiteRadiusVoxels` sphere out of the network at that
  segment. The kill is credited to whoever fired.
- If nothing explodes it within `HitPrismSeconds`, it is retired unharmed.
- A vessel of another domain passing through the body takes a bite directly, credited to its pilot.
- A vessel closing fast makes the creature **bolt**: it speeds up, the stroke quickens, and it turns away.

**Mass.**
- Each segment entry is `BindVirtualMass` in the creature's domain. Its volume is the segment's share of
  `BodyVolume − unpaid wounds` (BodyVolume = `GrownVoxels × VoxelSize³`).
- A bite removes body volume the way a shot removes any prism's.
- The wound is a **scar**, re-cut after every step, so it cannot regrow until it is paid for.

**Food (the lifecycle rule).**
- The creature is a herbivore. `ResolveGoal` steers it to the nearest edible flora heart, using the same
  `IsPreyForMe` rule and band every grazer uses.
- Its mouth (just ahead of the head's front) suctions up to `PrismsPerMouthful` edible prisms every `FeedInterval`
  while it is hungry or wounded.
- Each meal pays `HealShareOfMeal` (half) toward open scars, oldest first, and a scar heals once it is paid in full.
  The rest goes to `Fauna.NotifyFed`: the stomach, the soil (conserved stomach), and **reproduction**
  (`FeedsPerOffspring` 24, cooldown 60 s, cap 2).
- So a lizard is born fed, has 120 s of stomach, and can feed and breed before it dies.

**Death.** Every death goes through the sealed `Fauna.Die`:

| cause | how it is detected |
|---|---|
| starvation | `IsStarving` |
| its heart jousted | the crystal's own path |
| devoured | `Predated` |
| the network going extinct | `NcaVoxelCore.Extinct`, with respawn off |
| its body shot below `DeathFraction` of grown | only once it has grown past `MatureFraction` |

- The body comes apart around its heart:
  - **withered**: outside-in, and the heart is released last (`DefersHeartRelease`);
  - **jousted**: from the hole outward;
  - **devoured**: pulled into the eater.
- There is no skeleton, because there are no body prisms to leave.

**Heart.**
- One elemental crystal (`ProvisionHeart`), seated `HeartSeatDepthVoxels` behind the head's front plane on the
  head's centre line, and it follows the head.
- Its size is `HeartWorldScale`, which the generator computes as `K·bodyLength^0.5` with the K that
  `author_lifeform_heart_sizes.py` solves (the lizard is 52.8 u long, so 2.89).

## 3. Budgets

- **Colliders**, worst case in the Swarm cell: 2 lizards × (heart + one hit prism) = 4. `author_swarm_fauna.py`
  counts them, giving 1178 of the 1200 ceiling.
- **Volume**: 2 × 1975 u³ is added to the cell's phase-ladder volume.
- **Draw**: at most `MaxShown` (1600) prism entities per lizard; a grown lizard draws about 1250 (the harness measured 1249 skin cells, with 439 interior cells culled).

## 4. Adding the whale and the jellyfish

They use the same architecture (C=16, hidden 128) and the same runtime. Only data differs:

1. Finish their trainings and export them (`Tools/NCA/export_nca3d_creature.py`), so that
   `Tools/NCA/results/<run>/weights.json` exists on the research ref.
2. Measure their body frame (`forward`, `offset`, the head direction in the grid). The lab reuses the lizard's
   offset for both, which is wrong. A wrong frame shows up in the harness as segments out of order and a heart that
   is not at the head.
3. Add a row to `SPECIES` in `author_nca_creatures.py` with `name`, `run`, `forward`, `offset`, `tint`,
   `length_voxels`, `voxel_size` and, optionally, `swarm=` (its band in the Swarm cell).
4. Run `bash Tools/Build/nca_creature_harness/run.sh measure`, copy `grown` / `grow_steps` into the row, then run
   `python3 Tools/Build/author_nca_creatures.py`, the full harness, and `python3 Tools/Build/author_swarm_fauna.py`.

## 5. The Spawn Matrix (PR #962)

The four per-element `FaunaConfigurationSO`s that the Spawn Matrix lists are **not authored yet**:
`check_generated_assets.py` rejects a fauna config no spawn profile lists. When #962 merges:

1. Emit them from `author_nca_creatures.py` (the hook is commented in `emit()`).
2. Add the lizard to `Tools/Build/author_spawn_matrix_roster.py`.

## 6. Limitations (what is not done or not verified)

- **Not run in Unity.** It compiles headless against Unity 6 references (`unity_refcompile`, player and editor), and
  the core is proven against the lab. Nothing here has been seen on screen. The look, the swim speed, the heart seat
  and the hit feel all need an editor pass.
- **Step cost on players.** The step is managed C#. CoreCLR runs it at about 4.7 ms; Mono and IL2CPP will be slower
  (no `Vector4` SIMD on Mono). Because the step self-limits, a slow device swims slower rather than dropping frames.
  A Burst job port of `Flush` is the follow-up if it matters.
- **Not networked** (no `FaunaNetworkSync`); the Swarm cell is a solo demo.
- **Not under ecology LOD** (`CellEcologyLod`): it always steps.
- **Predators do not hunt it yet.** It shows up only as virtual index entries, not as an `IVirtualFaunaOwner`.
- **The body is a single colour per domain.** The lab's per-voxel skin luminance is computed (`NcaVoxelFrame.Lum`)
  but not drawn.
