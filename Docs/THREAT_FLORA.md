# Threat flora: the snap trap and the physarum grove (round 11c)

Two plants that hurt you, ported from the research models on `cece/eco-flora`
(`Tools/Ecology/flora/snaptrap.py`, `physarum.py`; DISCOVERIES.md "Threat flora", "Top 2 to port"). One grove of
each lives in the Swarm cell's rim (§4). Linked from `Docs/SWARM_FAUNA.md` §21. QA: `QA-SWARM-ROUND11-3`.

- **The snap trap** is a Venus flytrap. Each trap has 27 prisms (a 3-prism stalk, two 8-plate lobes, and 8 danger
  teeth on the lips) and one heart crystal that sits inside the jaws. It reads its prey's path, glows, and then
  snaps. The clumps turn their mouths toward where you fly.
- **The physarum grove** is a slime mould (Jones 2010, in 3D). It cables the grove between food and your trails.
  The cables carry a peristaltic danger pulse that you can see coming, and its hearts (sclerotia) beat danger.

Each species keeps its simulation in a pure-C# core (`System.Numerics`, netstandard2.1, C# 9, no UnityEngine), so
the harness runs the shipped files headless. The Unity glue is thin.

---

## 1. Architecture

| layer | file | what it does |
|---|---|---|
| core | `ThreatFlora/SnapTrapCore.cs` | Every trap in a cell, stored as struct-of-arrays. It runs the 5-state machine as a per-plant int, plus heliotropism, the colony rhizome and the ledger. It emits **events**. |
| core | `ThreatFlora/PhysarumCore.cs` | The cell's one network: agents, the trail field, tubes, Greenberg-Hastings waves, hearts and the ledger. It emits **events**. |
| core | `ThreatFlora/ThreatFloraMath.cs` | RNG, the grove shape (a ball, or a sector of a shell), frames and turning. |
| data | `ThreatFlora/ThreatGroveDefaults.cs` | The game's numbers. The harness asserts them and the author script reads them. |
| glue | `ThreatFlora/ThreatGroveConfigSO.cs` | The authored grove: where it is, the leaves, rates and palette. |
| glue | `ThreatFlora/ThreatGrove.cs` | The per-cell driver, created on demand. It steps the cores on their own clocks and turns events into prism work. |
| glue | `ThreatFlora/SnapTrapFlora.cs` | `Flora`: one trap, one crystal. It lays and forgets prisms when the grove tells it to, and buds through `TrySpawnOneOffspring`. |
| glue | `ThreatFlora/PhysarumSclerotium.cs` | `Flora`: one heart, one crystal, and a six-prism beat shell. It buds through `TrySpawnOneOffspring` when the network's reserve holds a whole shell. |

**No per-frame CPU on a prism.** Prisms move only at a core **keyframe**: a state change, or one heliotropic re-pose
per 4° of turn. At a keyframe the glue writes the final transform once (collider, spatial index and entity matrix
are final at once). It then stamps one GPU flight, `PrismRenderService.StampFlight`, with velocity
`(end - start)·π / (2·d)`, because the stamp's full vector is `v·2·d/π`. Glow is one colour stamp:
`SetColors` to the domain's Danger palette plus `StampColorTransition` from Plain. Danger is the ordinary tier:
`MakeDangerous`, or the `IsDangerous = false; DeactivateShields()` pair, since there is no "make safe" API. The
flight clock carries translation only, so a plate's attitude settles at the stamp.

## 2. The snap trap

### 2.1 States and the trigger (`SnapTrapState`, static int values)

| state | what happens | leaves when |
|---|---|---|
| OPEN (0) | The lobes rest at the gape (35°). The trap senses any vessel within `Sense` (146 u). | a vessel is near |
| PRIMING (1) | The lobes **glow** and gape wider (+9.8°) over `TPrime` (0.73 s). This is the telegraph. | `TPrime` passes → ARMED; the vessel leaves → back to OPEN, glow off, never fired |
| ARMED (2) | Fully open and glowing. A vessel whose **path this tick crosses the mouth cone** fires it. | it fires |
| CLOSING (3) | The lobes sweep shut over `TClose` (0.52 s), and they are **danger prisms** while they do. A vessel between them when they meet is snapped. | `TClose` passes |
| SHUT (4) | Digesting: `TDigest` (9 s) after a catch, `TReset` (6.7 s) otherwise. Then the trap eases open over 1.5 s. | the reset passes |

- The trigger samples the vessel's path every 4 u. A fast vessel cannot tunnel between ticks.
- **Half-lobe rule:** a trap with fewer than half its 24 lobe and tooth slots cannot fire. Breaking a lobe is
  counterplay.
- **Glow ≥ strike:** the lit lobes reach at least the strike volume, at every element.
- Teeth are always danger prisms. Their tooth tier is the burn.

### 2.2 Ecology (every law, and where it holds)

- **Mass is conserved per colony.** The RHIZOME is the research's single `reserve`.
  - A seeded trap brings exactly its body: `Planted += BodyVolume`.
  - Every trap's roots (`AbsorbRequest` every 4 s, up to `RootBitesPerAbsorb` edible prisms within 90 u) and jaws
    (`MouthAbsorbRequest` at SHUT) feed the rhizome.
  - Every slot is laid from the rhizome, and a lost slot is re-laid only from it.
  - A daughter buds only while the rhizome holds a whole body. She brings nothing, and `Audit()` closes to 0.
- **Nothing pops.** A new trap blooms its slots in order over `GrowSeconds`: stalk, then each lobe from the hinge
  out, then its teeth. Each prism arrives through the pool's ordinary bloom.
- **No timers or TTLs.** Nothing in either core expires. Every duration is a state the plant is in.
- **Domain.** A seeded trap takes the spawner's domain, and a daughter inherits her parent's (`SpawnOffspring`).
  The grove prescribes no domain.
- **One crystal per lifeform.**
  - A jousted trap leaves its standing body as a skeleton. The ledger books it as `SkeletonOut`.
  - A trap grazed to nothing withers and drops its heart.
  - The rhizome lives on in the other traps.
- **Shielded mass is never food.** Food is cell mass: never shielded (`Fauna.IsShieldedMass`, public since this
  round), never a living lifeform's or creature's body, never the grove's own tubes, and always what
  `Cell.IsPreyForHerbivore` allows at that point.

### 2.3 What the game changes, and why

- **20 Hz, not 10.** The research ran at 10 Hz. The sweep test needs 20, so a 120 u/s vessel cannot skip the
  closing jaws between ticks. Every rate is per second, so the change is exact.
- **Teeth hold, then bite.** In CLOSING the lobes shut while the teeth hold their pose. At SHUT the teeth snap to
  the prey's depth in a 0.12 s flight. The research moved teeth and lobes as one rigid jaw. The hold keeps the
  danger prisms where the glow said they were until the strike lands.
- **The lobes are danger only from FIRED to SHUT.** The research's "snap" is a hit. In the game a hit is delivered
  only by touching a danger prism (the stakes rule), so the sweep must be one.
- **Heliotropism cone (rim only).** The turn is the research's: an EMA toward traffic within `Sense`, 7°/s, and
  `bud_bias` 0.3. In the rim grove a trap may turn at most `HelioConeDegrees` (60°) from the heading it was planted
  with (toward the cell centre). Its whole body then stays between the outer swarm band and the membrane at any
  heading it can reach (§4.2). The default of 180° is unlimited, as in the research.
- **Element Time in the Swarm cell.** Space (geometry ×1.35, the research's highest hit rate) makes a trap
  ~111 u long. That does not fit the rim's 120 u gap at any heading. Time keeps the anchor geometry, runs every
  timing ×0.8 and turns ×1.25.
- **Volumes are game volumes.** Research volume units are not game units: a squirrel trail prism is ~3.1, a
  Borromean plate ~73. Every slot is costed at its prism's own volume (stalk 4×4×7, lobe 9×1.6×10, tooth
  2.2×2.2×7). The Mass element thickens a plate or widens a rod by ×1.5 in volume, so the prism is the volume the
  core books.

## 3. The physarum grove

### 3.1 The model (research searched best, quoted per field in `PhysarumParams`)

- **Agents** (Jones 2010, in 3D): 16k per 56³-voxel ball, so the density is kept per inside voxel. Each step an
  agent samples the trail field 28 u ahead and at four sensors tilted **30°**. If the best sensor beats forward it
  turns by **40.4°/30°** of the gap. It then jitters, moves **44.3 u/s**, and deposits 1.
- **Field:** a 3-tap separable blur (diffuse 0.5), evaporation 0.08 and a slow EMA (0.05). Food deposits 1.5 a
  step. A vessel's wake deposits **-0.24**: the searched best is negative, so a wake repels.
- **Tubes are the body.** A voxel whose slow EMA passes **On = 6** becomes a tube prism laid **from the reserve**.
  One that falls under **Off = 4.12** is resorbed into it (hysteresis). A tube voxel holding food **digests** it
  into the reserve. The cables are made of what the network ate, and `Audit()` closes to 0.
- **Danger is peristalsis.** Greenberg-Hastings runs on tube voxels, advancing one voxel per `h / 64.7` s, so the
  pulse runs at **64.7 u/s**. A tube is danger for 2 wave ticks (0.50 s), then refractory for **4 ticks
  (0.99 s)**. A pacemaker beat inside the refractory is skipped.
- **Sclerotia (hearts):**
  - Each is a pacemaker firing its nearest tube every 3 s.
  - It **beats** danger for 0.6 s after each fire, after glowing 0.8 s before it. The crystal is taken by diving
    in between beats.
  - It climbs the slow trail gradient at **7.8 u/s**, so the crystal ends up inside the cables.
- **A cut cable re-finds the gap.** Cutting wipes the tubes and the trail. The agents re-route through the hole
  as the trail rebuilds (§6, P3).
- **Mass element** is authored as **thicker tubes at the same wave speed** (TubeVolume ×1.5; the leaf widens by
  √1.5). The research's slow-wave Mass variant collapsed to R 0.72, because slow waves are dodged.

### 3.2 What the game changes, and why

- **10 Hz, not 20.** The Jones rule's deposit, diffuse, evaporate and EMA are **per step**, and they were searched
  at the research's dt = 0.1. At 20 Hz the same numbers make a different network. `StepSeconds = 0.1`; the wave
  ticks at its own `h / WaveSpeed`.
- **Tube materialisation every 0.5 s** (`MaterializeEvery`). The research laid and resorbed every step. Digestion
  runs on the same pass, with dt = 0.5.
- **Tube cap 400** (`MaxTubes`, the collider budget). The rim sector holds ~150-190 in the harness (P4), so the cap
  is a backstop.
- **The warm-up is spread over frames.** "The grove before you arrive" is the research's 250 steps, run 3 a frame
  at ~1.4 ms each. The network it settles on is laid at the end. The tubes it tried on the way are never laid.
  Lays are budgeted at 24 a frame.
- **Tube colour:** each tube wears the domain of the nearest living sclerotium. Sclerotia take the spawner's
  domain, so a network meeting another network's heart changes colour there. Regions differ; none is prescribed.
- **The beat's sting is its shell.** The research stung within 50 u of the heart. The game's sting is six danger
  prisms (6 u cubes) 14 u from the crystal, laid from the network's reserve. That is smaller, and fully readable.
- **A sclerotium buds from the reserve.** The tube pass keeps one beat shell (`ShellVolume`, 1,296) in the
  reserve and extends the cables only with the surplus. Without that hold-back every pass spent the reserve to
  under one tube, so a lost shell prism was never re-laid and no heart could ever bud. While the reserve holds a
  shell, `ThreatGrove.BudSclerotium` asks a living heart (`PhysarumCore.BudParent`, parents in turn) to bud, at
  most every `BudRetrySeconds`, through `Flora.TrySpawnOneOffspring` (the species cap and the Frenzy freeze
  apply). The daughter is planted at a sclerotium site, brings no mass (`AddBud`), and lays her shell from the
  reserve, so the shell blooms in like every other. At the cap the held shell simply waits in the reserve.
- **Tubes belong to the network, not to a heart.** A jousted heart stops beating and climbing, and its agents
  freeze (`live = heart_alive[owner]`, as in the research). Its shell stays as a skeleton. The cables it fed are
  resorbed only while another heart lives.

### 3.3 Two porting bugs found and fixed (fidelity, not tuning)

Seed 7, research Python vs this port, before the fixes: the port had 785 vs 1,103 warm-up tubes, a field
max of 225.8 vs 83 (the means agreed), and reroute recovery slower than the research.

1. **Agents deposited inside the move loop.** A later agent smelled what an earlier one laid on the same step,
   and the field over-concentrated. The research's vectorised `np.add.at` deposits after every agent has sensed.
   The port now deposits after the loop.
2. **No digestion during warm-up.** `physarum.py` digests inside `_sim`, warm-up included. Its warmed network
   outgrows the planted reserve on what it ate. Without that, the port was reserve-capped at exactly
   12,000 / 15.28 = 785 tubes.

## 4. The grove in the Swarm cell

### 4.1 Where

The grove is a sector of the cell's rim shell: radius **1,095-1,192 u**, half-angle 18° about
`normalize(1, 0.3, 0)`. The swarm bands are 470-620, 690-840 and 910-1080. The membrane is at 1,200 and the
nucleus at 392. The grove is outside every band, so no swarm is penned through it and none grazes it. The
physarum network fills the sector (a 20×46×48 grid at 16 u, 9,405 inside voxels, 1,637 agents). The snap traps
sit in three clumps (radius 57 u) placed 9° either side of the grove axis.

Round 11b's substrate populations (`SUBSTRATE_FAUNA.md`) stop at 1,080 u, but they bite 24 u: a locust or pack
hunter at its band edge can reach a trap's prisms that lean inward (the closest measured is 1,085 u). That is
ordinary grazing. `SubstrateFauna.IsFood` accepts only prisms whose `LifeForm` is a `Flora`, so the tubes
(`LifeForm` null) are never its food, and a trap prism it eats goes through `RemoveHealthBlock` like any other
loss. Not tested together: the two rounds were built in parallel.

**Round 11e's builders and thieves** (`BUILDERS_AND_THIEVES.md`) share the rim: the thief nest's band is
1,085-1,140 u, inside the grove's radii, and a fortress worker forages its 845-905 u band ± 220 u, so up to 1,125 u.
Neither may take the grove's mass, and the grove may not take theirs:

- **Tubes are living tissue.** `BuilderPrismWorld.IsLivingTissue` asks `ThreatGrove.IsGroveTissue`. Before this a
  tube (a HealthPrism with no LifeForm) read as loose mass: a fortress worker could have stolen one into a wall
  while the grove's ledger still held it. Trap and sclerotium prisms were already safe through their LifeForm.
  Thieves only take a vessel's warm trail (`IsTrail`), so they never wanted grove prisms anyway.
- **Built and carried mass is not food.** `ThreatGrove.IsEdible` refuses `BuilderRegistry.IsBuilt` and
  `IsCarried`: a fortress wall, a thief's hoard, a prism in a thief's grip. It is another creature's held mass, as
  a shell is. Without this, a trap's roots or a tube could have eaten a hoard from under its nest.
- **The nest never sits on the grove.**
  - A nest perches on the nearest living flora heart within 400 u, and only on a heart within 0.9 × membrane
    (1,080 u). A trap's heart crystal sits at ≥ 1,137 u (harness S9) and a sclerotium stays inside the sector
    (≥ 1,095 u), so no grove plant is ever a perch.
  - A nest that finds no plant stays where the cell spawned it. That point is now moved sideways out of the grove
    at its own radius, 40 u clear: `ThreatGrove.OutsideGroves`, which uses `ThreatGroveShape.PushOutside` (S9).
  - A nest perched on an Outer Space plant just below the grove (heart ≤ 1,080, hoard ≤ ~17 u) may sit next to the
    grove's lowest trap prisms (≥ 1,085 u). This is allowed. Neither can take the other's mass, and a snap trap
    senses only vessels, so it never snaps a thief.
- **Stakes are the cell's rule.** Every grove danger prism is a pooled `HealthBlock` variant carrying a
  `PrismImpactor`, so a vessel's hit runs its `VesselPrismEffects`, which hold
  `VesselElementalDebuffByDangerPrismEffect`. That effect's only gate is `IsDangerous`. It reads the live cell's
  `PetalBurnRule` (round 11g), and the Swarm Cell Config authors Tuned. So grove hits burn 1 petal per element in
  the Swarm cell, the same as every other danger prism there. This was read from the code, not played.

### 4.2 The rim geometry (harness S9)

The rim gap is 120 u and a trap is ~85 u long. A trap therefore roots **15-25 u inside the grove's outer
radius** (`ThreatGrove.RootAtRim`), is planted facing the cell centre, and turns at most 60° from that. S9 sweeps
the cone's centre, middle and edge, 8 azimuths, both root depths, the shut, resting and primed gapes, and every
slot plus half its leaf. A trap's prisms span **1,085.3-1,192.1 u**: 5.3 u clear of the outer band and 7.9 u
inside the membrane. In the core, traffic held straight behind a trap for 90 s turns it to 60.1°.

### 4.3 Authoring (`Tools/Build/author_threat_flora.py`, `--check`)

The script writes:
- every `ThreatFlora/*.cs.meta` and the folder metas (stable guids, one owner each);
- `SnapTrapFlora.prefab` and `PhysarumSclerotium.prefab` in `Assets/_Prefabs/FloraAndFauna/Threat Flora/`. These
  are forks of `BorromeanFlora.prefab`: the root script is swapped, the Borromean-only fields are dropped,
  `grove` is wired, `minHealthBlocks` is -1, and the crystal is renamed. The crystal's nested prefab instance is
  kept byte for byte;
- the grove config and the two species configs in `Assets/_SO_Assets/Threat Flora/`:

  | species | seed floor | cap | element |
  |---|---|---|---|
  | snap trap | 9 | 15 | Time |
  | sclerotium | 5 | 8 | Space |

  Both have `GrowthPerOffspring` 0. Both bud through `TrySpawnOneOffspring` from what their colony digested: a
  trap when the rhizome holds a whole body, a sclerotium when the network's reserve holds a whole shell
  (`ThreatGroveDefaults.SclerotiumCap`; `--check` fails if the cap leaves no headroom over the floor).

The Swarm cell's spawn profile is owned by `author_swarm_fauna.py`. Its `profile_asset()` now lists the two
species through `author_threat_flora.profile_guids()`. Its collider gate adds `always_on_hearts()`. The assets
live in their own folder because `author_swarm_fauna.py`'s `stale()` deletes anything it does not own in the
Swarm Cell folder. Run both scripts; both `--check`s must pass.

## 5. Collider budget (`Docs/SWARM_FAUNA.md` §4.1 / §14.3)

| | always-on | at the caps |
|---|---|---|
| swarm cell before this round (§14.3) | 978 worst case | |
| substrate proxies (round 11b, `SWARM_FAUNA.md` §20) | +78 engaged → 1,056 | |
| builder proxies (round 11e, `BUILDERS_AND_THIEVES.md`) | +84 engaged → 1,140 | structures add 0 |
| snap-trap hearts | **+15** (cap) | 15 × 27 = 405 body prisms |
| sclerotium hearts | **+8** (cap) | 8 × 6 = 48 shell prisms |
| physarum tubes | 0 hearts | ≤ 400 tube prisms (`MaxTubes`) |
| **total** | **1,197 < 1,200** as measured by `author_threat_flora.py` at the sclerotium cap change (the swarm cell had grown to 1,174 by then; the NCA creatures' +4 later made it 1,201, and `SWARM_FAUNA.md` §14.3's proxy trim brought it to **1,171**; `COLLIDER_CEILING`, asserted by both author scripts) | ≤ 853 ordinary flora prisms |

The body prisms are ordinary LOD-culled flora prisms, like every plant's plates. The gate counts always-on
hearts plus engaged proxies, as §14.3 does. The grove adds ~800 prisms to the cell at its caps: about 5% of the
FrenzyEnter count (15,500). The phase ladder is unchanged (§7).

## 6. Proof

All commands run in `/home/claude/wt-flora` with `DOTNET_ROOT=/usr/lib/dotnet` and a private `TMPDIR`.

```
bash Tools/Build/threat_flora_harness/run.sh            # 66 passed, 0 failed (~110 s)
bash Tools/Build/swarm_core_harness/run.sh              # OK (unchanged)
bash Tools/Build/swarm_glue_typecheck/run.sh            # type-check OK
python3 Tools/Build/author_threat_flora.py --check      # OK
python3 Tools/Build/author_swarm_fauna.py --check       # OK
python3 Tools/Build/check_console_logging.py            # 0 problems
```

`threat_flora_harness/run.sh` runs three fatal gates:
1. the cores compile against netstandard2.1;
2. the glue type-checks against `GlueStubs.cs`;
3. the asserted harness runs the shipped core files in an arena ported from the research's `harness.py`.

**Snap trap (S1-S9):**

| test | asserts | measured |
|---|---|---|
| S1 state machine | OPEN→PRIMING→ARMED→CLOSING→SHUT→OPEN, with t_prime, t_close and t_digest+reopen to ±1 tick; one snap, one glow on and one off, one fire; 4 keyframes in 11.8 s (never per frame); ledger 0 | 0.70 / 0.55 / 10.45 s vs 0.73 / 0.52 / 10.5 |
| S2 | a primed trap whose vessel leaves relaxes without firing | |
| S3 half-lobes | 12 of 24 slots intact fires; 11 of 24 cannot | |
| S4 reserve-only rebud | nothing eaten means no regrowth; regrowth equals what was eaten; one body buds one daughter, who blooms from it; budding plants no mass; ledger 0 | |
| S5 glow ≥ strike | holds at every element | |
| S6 arena | wander hits/min; prisms at 2 min; ledger | 6.83 hits/min (research 4.17); 1,956 prisms (research 1,733); 0 |
| S7 route bias from heliotropism | see below | |
| S8 cost | 60 traps | 0.0084 ms per step |
| S9 rim geometry and cone | §4.2 | |

S7 route bias:
- **Heliotropism alone** (a fixed clump, no growth, 12 seeds): **1.25×**, against a no-turn control of **1.01×**.
- **Full ecology:** 1.15× (asserted ≥ 1.0). §7 explains why this differs from the research.

**Physarum (P1-P5, P7; P6, the far cadence, is in `Docs/ECOLOGY_LOD.md` §6.3):**

| test | asserts | measured |
|---|---|---|
| P1 wave | speed down a hand-laid cable | **64.66 u/s** vs 64.71 |
| P1 sting | danger time per pass | **0.50 s** (2 ticks) |
| P1 beat period | gaps in [Period - step, Period + tick + step] | |
| P1 refractory | a 1 s pacemaker pulses no faster than the refractory | 1.90 s ≥ 1.49 s |
| P2 telegraph | 5 seeds × 2 min of wander | 14 burns, lead **p10 0.73 s** (≥ 0.7, the research's unwarned bar), median **4.15 s** (≥ 2); 1.40 hits/min (research 1.33); mass drift 0 |
| P3 reroute | a 120 u ball through the densest tubes is cut at t = 60 s, same 5 seeds as the research | median t50 **41.1 s**, 3/5 half back within 90 s; research on the same seeds: t50 17.1 s, 4/5 |
| P4 game grove | forms a network under its cap, beats, ledger, cost | 158 tubes after warm-up, 170 after 60 s (cap 400; 166 / 183 before the shell hold-back); 95 beats; ledger 0; **1.25 ms per 10 Hz step** |
| P7 budding | no bud below a whole shell; only a living parent; the daughter brings no mass and lays her shell from the reserve; the game grove under the glue's rule buds from what it digests, every bud mass-neutral, daughters beat | 3 buds in 120 s (5 → 8 hearts, the cap); ledger 0 |
| P5 cost | | 6.3 ms per step at 16k agents / 56³ (research numpy 20.7 ms); 11.6 ms at 32k / 64³ |

**Not proved:**
- **Nothing here ran in the Unity editor.** The glue is type-checked against hand-copied stubs, which catch a
  misspelt member or a wrong signature but no runtime behaviour.
- Untested in the editor:
  - the GPU flight and colour stamps on these prisms;
  - the danger tier on tubes mid-bloom;
  - `Consume` toward a moving heart;
  - the spawner seeding these two species into the rim;
  - the Frenzy freeze on budding.
- Also unproved:
  - The cost was measured on CoreCLR. Mono will be slower.
  - The physarum core is single-threaded on the main thread. No compute-shader version was written: it was
    optional, and the game grove costs 1.4 ms per 0.1 s.
- The research numbers this port does **not** reproduce:
  - Telegraph p10 3.6 s, which the port cannot reach (§7).
  - Reroute t50 26.1 s and end fraction 0.80. Over the research's own five seeds these are 17.1 s and 0.14.

## 7. Findings for the research

1. **`refr = 4` is in wave TICKS**, not seconds. At h/ws = 0.248 s that is 0.99 s refractory after 0.50 s excited.
   The prose "4 s" is wrong.
2. **`beat_on = 0.6` s per 3 s period is a 20% duty cycle**, not "60% duty".
3. **The snap-trap route bias was never controlled.**
   - Heliotropism alone moves the clump onto a courier's loop: 1.25× vs 1.01× with turning off, over 12 seeds.
   - With food, trails and budding, the growth term dominates, and per-seed values run 0.38-3.34. Over 12
     seeds: 1.15× with turning and **1.32× without**.
   - The research's 1.61-1.83 (3 seeds, no control) is mostly where the colony happened to grow.
4. **The physarum telegraph p10 of 3.6 s is not a stable target.** It is the 10th percentile of ~8 burns
   (wander + reader). The research's own element variants of the same network scored 0.90-0.92 s
   (`results/summary.md`). The port's 0.73 s over 14 burns is in that family.
5. **Reroute is bimodal in the research too.** On the same 5 seeds:
   - t50: 26.1 s, never, 17.1, 17.1 and 11.6 s;
   - end fraction: 0.89, 0.00, 0.80, 0.14 and 0.00.

   Half-return is reliable and holding it is not, because regrown tubes are resorbed again as the agents
   re-route. The reported "80% back" is the 3-seed median of a bimodal outcome.
6. **Warm-up digestion matters.** Without it, the warmed network is capped by the planted reserve (§3.3).

## 8. Files

- **Cores and data:** `Assets/_Scripts/Controller/Environment/FloraAndFauna/ThreatFlora/ThreatFloraMath.cs`,
  `SnapTrapCore.cs`, `PhysarumCore.cs`, `ThreatGroveDefaults.cs`.
- **Glue:** `ThreatGroveConfigSO.cs`, `ThreatGrove.cs`, `SnapTrapFlora.cs`, `PhysarumSclerotium.cs`, plus
  `FloraAndFauna/Fauna.cs` (`IsShieldedMass` is now public).
- **Round 11e seams:** `Builders/BuilderPrismWorld.cs` (`IsLivingTissue` asks `ThreatGrove.IsGroveTissue`) and
  `Builders/BuilderColonyFauna.cs` (a nest with no plant is moved off the grove by `ThreatGrove.OutsideGroves`).
- **Assets, authored:**
  - `Assets/_Prefabs/FloraAndFauna/Threat Flora/{SnapTrapFlora,PhysarumSclerotium}.prefab`;
  - `Assets/_SO_Assets/Threat Flora/{Swarm Threat Grove Config, Swarm Snap Trap Flora Time Config Data, Swarm Physarum Flora Space Config Data}.asset`;
  - `Swarm Cell Spawn Profile.asset`, now listing both species.
- **Tools:** `Tools/Build/author_threat_flora.py`, and `author_swarm_fauna.py` (`threat_flora()` hook).
  `Tools/Build/threat_flora_harness/` holds `run.sh`, `Program.cs`, `FloraArena.cs`, `SnapTrapTests.cs`,
  `PhysarumTests.cs` and `GlueStubs.cs`. `swarm_glue_typecheck/Stubs.cs` now declares `IsShieldedMass` public and stubs the two `ThreatGrove` statics.
