# Substrate fauna: the Living Ecology agent substrate in the game (round 11b)

**Status:** branch `overnight/substrate` (round 11b), `overnight/substrate2` (round 11b-2: the Burst agent pass, the ladder,
the pack's menace hold - §7). Headless harness, Roslyn type-check and asset generators all pass. **Not yet run in the
Unity editor**: see QA-SWARM-ROUND11-2 and QA-SWARM-ROUND11-8 in `Docs/QA/QA_BACKLOG.md`.

Linked from `Docs/SWARM_FAUNA.md` §20.

The research substrate (`Tools/Ecology/substrate`, `DISCOVERIES.md` "Bestiary" and "Cost and the substrate") showed
that one agent model, with the species as data, produces the whole bestiary: pack hunters that ring and strike
together, locusts that flip from cute to a storm, lurkers that creep while you are not looking. This round ports that
model into the game.

The design target, in Garrett's words: *"I can imagine being surrounded by them and having them all dive in at once
will be scary once the stakes are felt."*

## 1. What it is

All code lives in `Assets/_Scripts/Controller/Environment/FloraAndFauna/Substrate/`.

### Pure C#

These files have no `UnityEngine` and are compiled and **run** by `Tools/Build/substrate_harness`.

| File | What it holds |
|---|---|
| `SubstrateSpecies.cs` | `SubstrateRegime` (one end of the phase axis), `SubstrateSpeciesParams` (a species) and `SubstrateResearch`. `SubstrateResearch` holds the research numbers verbatim plus the game deltas, each with a stated reason. |
| `SubstrateFields.cs` | The cell's coarse stigmergy fields: FOOD, ALARM, THREAT and SCENT on a 40³ grid, with a separable `[a, 1-2a, a]` blur and decay on signals only. |
| `SubstrateCore.cs` | The agents, as struct-of-arrays (see the list below). The tick is `BeginStep` → `RunAgentPass` → `EndStep` (§7). |
| `SubstrateKernel.cs` | Round 11b-2: ONE agent's step, `SubstrateKernel.StepAgent`, a static pure function over `SubstrateAgentSoA` spans, Burst-shaped (§7). |
| `SubstrateTickJob.cs` | The off-thread tick, in the round-7 shape (`SwarmTickJob`): double buffers, `SwarmInstance` output, true bodies, engaged agents, events and requests. |

What `SubstrateCore.cs` does:

- **Neighbours.** Cell-moment neighbours (open-addressing hash, 27-cell reads) and the spacing spring.
- **Steering.** Context steering over Fibonacci directions with a soft danger mask.
- **Quorum.** A phase flip (sigmoid with hysteresis, plus contagion).
- **Fractional update.** Each tick steps `(i+tick) % k`, with the attention LOD (agents near a pilot or urgent are always stepped).
- **Bestiary primitives.** Closure quorum, posture clock, gaze, mimicry, prey and scent.
- **Lifeform rules.** Stomach, eat, breed, starve.

### Unity glue

Type-checked by `Tools/Build/substrate_glue_typecheck`.

| File | What it holds |
|---|---|
| `SubstrateSpeciesSO.cs` | A species as an asset: the serialized `SubstrateSpeciesParams`, plus seeding, drawing, proxy budget and feeding. |
| `SubstrateCellHost.cs` | One substrate per cell. It owns the core and the job, senses vessels and flora once per tick, and applies populations joining and leaving between ticks. |
| `SubstrateFauna.cs` | One population's anchor. Heartless and bodiless, spawned by the cell's ordinary spawner. Implements `IVirtualFaunaOwner`, `IVirtualPrismBudget` and `ISwarmEntrySink`. |
| `SubstrateAgentFauna.cs` | An agent's proxy: a heart and one body prism. |
| `SubstrateMemberRenderer.cs` | Draws hearts, and bodies when `PrismRenderService` is off, through the swarm's `SwarmMemberInstanced` shader. |
| `SubstrateAgentJob.cs` | Round 11b-2: `SubstrateAgentJob`, the `[BurstCompile]` `IJobParallelFor` that runs `SubstrateKernel.StepAgent`, and `SubstrateAgentPass`, the host's NativeArray mirror that schedules it (§7). |

Every population of every species in a cell is a block of slots in **one** core, so they share the fields:

- A pack's THREAT wake is a locust's danger signal.
- A locust's SCENT is what the pack hunts by.

Nothing is wired between them.

## 2. The species (data) and the primitives

A species is two `SubstrateRegime`s. Every steering weight is `lerp(solitary, gregarious, phase)`, so a phase flip is a
behaviour change and nothing in the core branches on species. The four bestiary primitives the research named are
parameters; zero switches each off.

| Primitive | Parameters | Mechanism |
|---|---|---|
| **Closure quorum** (pack ring) | `QWClose`, `CloseR` 350 | Per pilot, the moment of packmates' bearings: `clip((1 - abs(sum b)/cnt) * clip((cnt-1)/3) * 1.6)` from packmates within `CloseR`. It feeds the quorum signal. |
| **Posture clock** | `StaminaS`, `RestS`, `RestSpeed`, `WRestRetreat`, `RestTogether` | After `StaminaS` of strike the agent rests for `RestS`: slow, never dangerous, falling back. A bite sends every striking packmate near that pilot to rest. |
| **Gaze** | `WCreep`, `CreepR`, `CreepMin`, `CreepSpeed`, `CreepLeadS`, `GazeCos`, `Freeze` | Outside a pilot's forward 50° cone, the agent creeps toward where the pilot will be in 3 s. Inside it, it freezes. |
| **Mimicry** | `MimicBody` | While calm, the drawn body is 15% of its true size behind its heart, so what you see is a crystal. |
| **Prey and scent** (food web, game-only) | `PreyName`, `WPrey`, `PreySense`, `ScentDeposit` | A hungry predator follows the prey's scent gradient times hunger and pounces within `PreySense`. A catch inside `EatR` becomes a `SubstratePredation` request. |

`DangerPhase` 0.5: an agent is a **danger prism** while `aggr > 0.5 && phase > 0.5 && rest <= 0`, the research harm
rule.

### The three species

The numbers live in `Tools/Build/substrate_harness/game_params.json`, which the harness asserts and the author script
copies into the assets.

**Pack hunters** (Time hearts, 6 seeded, cap 7)
- Long, low 12 u bodies (aspect 2.6).
- Stalk ~98 u/s, slower than a 120 u/s pilot. They catch you by geometry, never by speed.
- They predict your line, fan out onto a 110 u ring and close it.
- When closure crosses `q_up` 0.65 (round 11b-2; bestiary 0.55) they strike **together** at ~168 u/s.
- After 3 s they are winded for 3 s: slower, harmless, falling back. That is the payoff window.
- They eat locusts by scent.

**Locusts** (Space hearts, 40 seeded, cap 360)
- Sparse and fed, they are solitary: slow, curious, bobbing (2.15 Hz gait).
- Dense and hungry (quorum = density × hunger), they flip gregarious: aligned, fast (95 u/s), biting.
- The swarm's size is the food it finds: births are paid from eaten flora.

**Lurkers** (Mass hearts, 8 seeded, cap 16)
- Seeded **at** the Mass flora's crystals, which they mimic.
- They creep while unwatched and freeze when looked at.
- A gape (the regime's size swell) telegraphs a 0.6 s snap at 230 u/s, after which they are spent for 3 s.

**Stampede, mobber, leech, leviathan** (round 11-11): §9.

**Thieves** (later) need only a new `SubstrateSpeciesSO`:
- A species with `PreyName` pointed at another species, and/or a new primitive parameter, is the whole hook.
- The core's request lists (`EatRequests`, `PreyRequests`) and the owner's resolution are species-agnostic.

## 3. Lifeform rules (the ecology laws)

**Mass is conserved.** An agent's body volume **is** its stock. `BodyOf(stock, aspect, thin 0.6)` gives a
`w × 0.6w × aspect·w` prism of exactly that volume. Mass moves only these ways:
- **Eating.** A flora prism is consumed into a mouth and its volume is paid into the agent 1:1.
- **Breeding.** At `BirthStock` the parent splits its stock in half with the child.
- **Starvation and death.** The body stays as the platform's skeleton.
- **Predation.** The prey's body is suctioned into the hunter's mouth and its stock is paid into the hunter.

The core's ledger (`MassIn - MassOut = MassHeld`) is asserted every tick, to 2e-9 relative.

**No imposed death, no timers.** Hunger can exceed 1; that is the reserve burning.
- `Starving` fires when `hunger >= 1 + StarveS * Metabolism`. Food pushes that back exactly; nothing counts time.
- The core **never kills**. It emits `Starving`, and the owner gives the agent a proxy and kills it through the sealed `Fauna.Die`.

**Every lifeform drops one crystal.** An agent dies only through its proxy (`SubstrateAgentFauna`), so every death
(gun, ram, joust, AOE, predator, starvation) releases its heart.

**Nothing pops.**
- Births bloom from a point in the shader (or the entity matrix).
- A proxy appears and retires under an unchanged picture.
- Deaths wither through the platform.
- No agent moves more than 25.8 u in a 10 Hz step (measured max 19-23 u).

**Spawning in the controlling domain.**
- The anchor is spawned by the cell's spawner in `Cell.ControllingDomain`.
- Every agent wears the anchor's domain; the configs prescribe no domain.
- `Cell.SetModeControlOverride` recolours the palette, every proxy and every index entry (`OnTeamChanged`).

**Shielded mass is never food.**
- The edibility check is `Fauna.IsShieldedMass` + `IsPreyForMe`.
- Charge plants (armoured leaves) are left out of the food field altogether.

### The stakes

A striking agent's proxy body prism goes through `MakeDangerous`, the existing danger tier, in the population's
domain. Contact with an opposing-domain vessel therefore burns petals, exactly as the round-10 swarm strikes do. The
pack's engage radius (260 u) and proxy budget (7 = the whole pack) mean every hunter in a strike is a real danger prism
before it arrives.

## 4. The assets

Generated by `python3 Tools/Build/author_substrate_fauna.py` (`--check` fails on drift).

| Asset | What it is |
|---|---|
| `Assets/_SO_Assets/Substrate Fauna/Substrate {Pack Hunter,Locust,Lurker,Stampede,Mobber,Leech,Leviathan,Siege} Species.asset` | The species. Every `species:` number is from `game_params.json`. |
| `Assets/_SO_Assets/Substrate Fauna/Substrate {…} Fauna Config Data.asset` | One population each, with its band and heart element. |
| `Assets/_Prefabs/FloraAndFauna/SubstrateAgent.prefab` | The proxy. Derived each run from `author_swarm_fauna.tadpole_prefab()` with `SubstrateAgentFauna` in the member's place. |
| `Assets/_Prefabs/FloraAndFauna/Substrate{Pack,Locust,Lurker,Stampede,Mobber,Leech,Leviathan,Siege}Fauna.prefab` | The anchors. |

`author_swarm_fauna.py` owns the Swarm cell and calls this script through two hooks:
- `profile_entries()` adds the seven configs to the spawn profile's `SupportedFaunas`.
- `proxy_colliders()` adds 2 × 39 = **78** to the collider ceiling check. Round 11-11 re-divided those 39 proxies over
  seven species without raising them (§9.6); the cell's worst case is still 1,172 + 20 grove hearts = 1,192 / 1,200.

| Population | Band | Why |
|---|---|---|
| Lurker | 470-620 u (Mass flora) | Mimics the Mass crystals it is seeded among. |
| Pack | 690-1080 u | Covers the locusts' shell, so it is never led to food it cannot reach (checked). |
| Locust | 910-1080 u (Space flora) | Grazes the Space flora. |
| Mobber, stampede, leech, leviathan | 625-685 u; 690-840 u in three sectors | §9.5. |

## 5. The glue (`SubstrateCellHost`, `SubstrateFauna`)

### The cadence (round 7)

`SubstrateCellHost.Advance` runs once per frame, from whichever population updates first. When the worker's tick is
`Done` it:
1. Collects the tick.
2. Calls every population's `OnTickPublished`.
3. Applies joins and leaves (the worker never sees a half-made block).
4. Senses vessels (one cell-wide `OverlapSphereNonAlloc` into a 4096 buffer, up to 8 pilots) and flora (`FloraHeartRegistry.Live`, one unit per non-Charge heart).
5. Kicks the next tick on the thread pool.

A slow worker slows the substrate, never the frame. `dt` is the research's 0.1 s.

### One prism system (round 11a)

**Detection.** An agent's body is a `PrismSpatialIndex` virtual entry.
- The swarm's own `SwarmEntryLedger` keeps the entries over this population's slice: `SubstrateTickJob.Slice` hands
  it the slice wearing each agent's **true** body, so the entry volume is the stock.
- Each entry is bound with `Cell.BindVirtualMass`, so `LiveVolume` and the phase ladder count it as fauna body mass.
- The entry is suspended while a proxy's real body stands in, and released at death.
- `VirtualFauna` (`IVirtualFaunaOwner`) lets every platform predator and heart-seeking blast reach an agent with no
  GameObject.
- `IVirtualPrismOwner.MaterialiseVirtualPrism` makes it real on a hit, budgeted by `IVirtualPrismBudget` (48 per
  frame, shared by all populations).

**Drawing.** When `PrismRenderService` is on, each body is an ordinary prism entity in its tier's material (plain or
danger, the population's domain):
- One `CreateBatch` for new slots.
- One `SetLooksBatch` per tick for tier changes, so a strike turns the body into a danger prism on screen.
- One Burst `SetTransformsBatch` per frame, with the pose from `SwarmBodyPose`.

The member shader then draws hearts only. With the service off (its default) the member shader draws both.

### Proxies (the collider budget)

An agent within `EngageRadius` of a vessel (nearest first, at most `MaxProxies`) is given a proxy:

| Species | `EngageRadius` | `MaxProxies` |
|---|---|---|
| Pack | 260 u | 7 |
| Locust | 140 u | 8 (24 before round 11-11, 12 before the siege) |
| Lurker | 160 u | 4 (8 before round 11-11) |
| Stampede | 200 u | 6 |
| Mobber | 120 u | 4 |
| Leech | 140 u | 2 |
| Leviathan | 200 u | 4 |
| Siege | 200 u | 4 (§10.4) |

Each population carries its own rule (`SubstratePopulation.EngageRadius`/`MaxEngaged`, set from the SO; §8 U1).
Round 11-11 re-divided the caps (the table above) and the proxies now go to DANGEROUS agents first,
then the nearest (§9.6).

The proxy takes the agent's true body and its danger tier every tick, and retires `ProxyLingerSeconds` (2 s) after the
vessel leaves. Proxies are client-local, like swarm members; `FaunaNetworkSync.ServerSpawn` neutralizes them.

### Requests from the core

- **`EatRequests`.** Hungry agents in this tick's slice, at most `MaxBitesPerTick`: query `PrismSpatialIndex.QuerySphere` within `BiteRadius` 24 u, consume the first edible flora prism into a mouth, then `QueueFeed(i, volume)`.
- **`PreyRequests`.** The prey's owner materialises it (forced), and `Predated(hunterName, hunterMouth)` kills it through the sealed death. The hunter is fed the prey's stock. At most 4 per population per tick.
- **`Starving`.** A forced proxy, then `Starve()` once the body is real, leaving the skeleton.

**Extinction.** With no living agent for 8 s, the anchor despawns (heartless, so nothing pops), and the seeder may hatch
a new population.

## 6. Proof

Commands:

```
export DOTNET_ROOT=/usr/lib/dotnet TMPDIR=/tmp/claude-0/tmp-substrate
bash Tools/Build/substrate_harness/run.sh            # every asserted group; exit 1 on any failure
bash Tools/Build/substrate_glue_typecheck/run.sh     # glue vs hand-copied stubs, -warnaserror
python3 Tools/Build/author_substrate_fauna.py --check
python3 Tools/Build/author_swarm_fauna.py --check
```

`run.sh` compiles the substrate core against netstandard2.1 first (Unity's profile), then runs it under net8.

| Group | What it asserts (numbers from the last run) |
|---|---|
| **F** fidelity | 86 research numbers per species equal the Python (`research_fixture.py` imports the research `species.py`), e.g. locust `q_up` 0.55, pack gregarious speed 150, locust gait 2.15 Hz × 27.6 u/s. Each game set differs in exactly its documented fields (locust 3, pack 24, lurker 9). `game_params.json` equals the shipped game sets. |
| **P** pack | 5/5 seeds: the ring closes and then strikes. Telegraph 0.4-0.7 s from ring-closed to first strike (bestiary 0.9 s). 100% of the pack turns dangerous within 1 s of the first. The ring tightens (e.g. 112→76 u). Winded hunters are never dangerous and are slower (71 vs 106 u/s). **Menace before terror** (round 11b-2): the ring is held 10.6-21.9 s before the first strike in every seed (≥ one 8 s emotion-probe window); median stalk before any strike 10.7 s over 20 strikes. A careless pilot takes 20 bites per 5 × 90 s; a pilot who flies at the gap takes 1. Ablation `q_w_close = 0`: no strike. |
| **L** locust | Sparse + fed: phase 0.01, 0 bites. Dense + hungry: phase 0.73 (20% flipped at 1.4 s), 19 bites in 30 s. Hungry but sparse: 0.00. With food, 159 births in 2 min; without, 0. |
| **U** lurker | Unwatched it creeps 78.7 u in 3 s; watched it moves 0.00 u. The gape precedes the snap by 0.5 s; spent after. |
| **M** ledger | Three species, real food, a pilot, 3 min: agent ledger drift 2.3e-9; world ledger (food + bodies) drift 3e-9; every death caused (3 starvations); max step 19 u (bound 25.8); 183 births; the pack lives on 71 locusts. |
| **J** job | 20 ticks off-thread with no error. Body volume equals stock. Stated volume equals held mass. Engaged = within 160 u, capped. Heart list = the living. A queued kill and feed land next tick. Predation requests cross the thread boundary. The split tick (worker parks after `BeginStep`, main-thread agent pass, pool finishes) publishes exactly the plain tick over 60 ticks. |
| **X** index | Through the swarm's `SwarmEntryLedger` over each population slice, 300 ticks with births, deaths and ~⅓ stand-ins: every living agent is in the index exactly once. Index volume equals stock to 1.7e-7. Negative control: ledgering the drawn scale fails at 20%. |
| **K** kernel | `SubstrateKernel.StepAgent` vs the pre-11c managed step (`ReferenceStep.cs`), agent by agent on the same input, three species + food web + two pilots, 120 s: **212,859/212,859 agent-steps bit-identical**, 0 decision mismatches; every branch exercised (7,434 ring/hunt, 642 watched, 10,053 creeping). |
| **B** cost | 10k locusts, k=8. ONE thread: 5.3-9.7 ms per step (kernel pass 2.6-4.7 ms). Kernel pass over 4 threads (`Parallel.For`, contiguous chunks): 4.8-7.6 ms per step (kernel pass 1.8-4.2 ms). All on a shared 4-core host at load 5-8, so the scaling is the host's, not the kernel's. Research numba 1.2 ms at 4 threads. The Swarm cell holds ~400 agents. |

### What is NOT proved

- Nothing here ran in Unity. The following are reasoned and type-checked, not seen:
  - proxies, colliders, the danger burn on petals;
  - rendering (instanced or entity), the shader pose of agent bodies;
  - `VirtualFauna` predators reaching agents;
  - the spawner seeding the three configs.
  QA-SWARM-ROUND11-2 is the in-editor proof.
- The Burst job was never compiled by Burst (§7.6). The kernel is bit-matched on .NET and passes a textual gate.
- The bench is noisy on a shared machine; k=4 and k=8 are within noise of each other at 10k.
- The main-thread costs (index sync, proxies, feeding) were not profiled. They are O(population) array work plus
  O(changes) index calls.
- Agents born after the cell changes controller wear the anchor's domain until `Cell.SetModeControlOverride` recolours
  it. This is the swarm's one-colour rule, not a per-birth lookup.

## 7. Round 11b-2: the Burst agent pass, the ladder, the pack's menace

### 7.1 The kernel

`SubstrateKernel.StepAgent(in SubstrateAgentSoA s, in SubstrateKernelPop k, in SubstrateKernelWorld w, int q, Span<float> I, Span<float> G)`
is the whole per-agent step: drives, the 1/k slice and attention LOD, the 27-cell moment read, the quorum, the context
map with every primitive, and the bounded integrate. It is the pre-11c `SubstrateCore.StepAgent` transliterated to
scalar maths:

- no `System.Numerics` method, operator or `Vector3` local - vectors are read and written as `.X/.Y/.Z`;
- no `System.Math`, no allocation, no managed type, no lambda, no `var`;
- `SubstrateKernelPop` (a population's numbers and its two regimes) and `SubstrateKernelWorld` (dt, R, tick, pilot
  count) are blittable;
- the fields are GATHERED per agent in `BeginStep` (`FThreat`, `FAlarm`, `GFood`, `GScent`, `GAlarm`, `GThreat`), so
  no 40³ grid crosses into a job;
- agent q writes only slot `Live[q]`.

The direction count is clamped to `SubstrateKernel.MaxDirs` (32; the species use 18 and 26), the job's scratch size.

### 7.2 The split tick

`SubstrateCore.Step` = `BeginStep` + `RunAgentPass` + `EndStep`, each over every active population:

| Phase | What it does |
|---|---|
| `BeginStep` | Inputs, `Fields.Update`, tick, prey resolution; per population the live list, moments, closure, the field gather and `pop.Kernel`. |
| `RunAgentPass` | The kernel per agent, populations in order (a predator reads its prey's moved positions). Managed: `Parallel.For` over `Workers` in contiguous chunks. |
| `EndStep` | Per population the starving drift, gait, field deposits and the world pass; then the clock. |

One behaviour change: a predator's agent pass now runs before its prey's world pass, so a locust born this tick is
not visible to a hunter until the next. Every asserted group passes unchanged.

In the game, `SubstrateTickJob.ExternalAgentPass` is on. The worker runs `BeginStep` and parks (`AwaitingAgentPass`,
state stays Running). On its next `Advance`, `SubstrateCellHost` copies the core's arrays into persistent
NativeArrays (`SubstrateAgentPass.Schedule`) and schedules one `SubstrateAgentJob` per population, chained. The
`Advance` after that completes the jobs, copies the written arrays back (`Complete`), and calls
`ResumeAfterAgentPass`, which queues `EndStep` and the frame build on the pool. Jobs are only scheduled from the main
thread, so the tick spans a few frames. Inline mode (WebGL, or off-thread disabled) schedules and completes at once.
The main thread pays the array copies (~30 capacity-sized arrays in, 14 out); the Profiler marker is
`SubstrateCellHost.AgentPass`. `SubstrateAgentPass` is disposed when the host's last population leaves.

### 7.3 Proof

- **Gate:** `Tools/Build/substrate_harness/check_burst_substrate.py`, run first by `run.sh`. It applies the swarm pose
  gate's rules plus the ones above to `StepAgent`, `Paint`, `PaintD`, `NearestPilot`, `NearestPrey` and the
  one-line helpers. It checks that the kernel structs hold only blittable fields, and that the job is
  `[BurstCompile] ... : IJobParallelFor` with only NativeArray/blittable fields and calls the kernel. Negative control:
  the pre-11c step trips 7 rules. Mutations checked by hand: `var`, `Math.`, a `Vector3` local, a managed array, a
  string field, a managed job field and a missing `[BurstCompile]` each fail it.
- **Bit-match (group K):** each agent is stepped by the reference and by the kernel from the same saved state. All
  212,859 agent-steps were bit-identical on .NET.
- **Split tick (group J):** the parked tick plus a main-thread `RunAgentPass` publishes exactly the plain tick.
- **Type-check:** `SubstrateAgentJob.cs` is checked against stubs of `NativeArray.AsSpan/AsReadOnlySpan/CopyTo`,
  `[ReadOnly]`, `[NativeDisableParallelForRestriction]`, `[BurstCompile]`, `IJobParallelFor` and `JobHandle`.

### 7.4 The ladder sees the substrate's bodies

Every agent body is a `BindVirtualMass` entry whose volume is its stock, so `LiveVolume` counts it.
`author_substrate_fauna.body_volume()` models the mature cell like the swarm bodies: each population (one per species
per cell) at its full pool, each body halfway between its seed stock and its split stock. That is
locust 360 × 57.5 + pack 7 × 225 + lurker 16 × 62.5 = **23,275** volume. `author_swarm_fauna.ladder` adds it, which
moves the Swarm cell's volume ladder:

- Restless 55,000 / 41,000 -> **63,000 / 47,000**.
- Frenzy 390,000 / 343,000 -> **448,000 / 394,000**.

The count ladder is unchanged, because agents are volume-only like every fauna body.

### 7.5 The pack's menace (research finding 11)

`DISCOVERIES.md` bestiary finding 11 says three things:
- a pack's read is set by whether it strikes;
- menace appears only while the ring holds without striking (4-5 big members holding the ring: menacing 0.37-0.42);
- a pack is a menace→terror ARC, read per phase.

The research's emotion timeline reads 8 s windows (`emotion/timeline.py`). So the stalk before the strike must fill
at least one 8 s window to read as menace before it turns to terror.

**Measured (group P):** the ring is held when ≥ 4 hunters are inside `CloseR` (350 u) and none is striking or
winded. With `q_up` 0.55, the hold before the first strike was 2.9 s in one seed of five. The tune is `q_up` **0.65**,
between the bestiary's 0.55 and the research's 0.75, recorded in `GameDeltas` with its reason. Results:
- the first hold is 10.6-21.9 s in every seed;
- the median hold before any strike is 10.7 s;
- the telegraph (ring-closed to strike) is still 0.4-0.7 s;
- careless bites fell from 26 to 20 per 5 × 90 s;
- counterplay is now 1 bite vs 20.

Both holds are asserted.

### 7.6 What is NOT proved

- **Burst never compiled the job.** Burst is not available here. `Span`/`ReadOnlySpan` locals, `stackalloc` into a
  `Span`, `NativeArray.AsSpan()/AsReadOnlySpan()` and `System.Numerics.Vector3` as a plain struct are Burst-supported
  as far as we know (Burst 1.8 docs), but the Burst Inspector is the proof (QA-SWARM-ROUND11-8).
- **Burst's floats are not bit-matched.** Burst's math intrinsics may round differently from .NET's. The bit-match is
  .NET-to-.NET. Behaviour under Burst is expected to match to float rounding, not bits.
- **The safety system was not run.** The job's `[NativeDisableParallelForRestriction]` writes (slot `Live[q]`, not
  `q`) and the chain over shared arrays were not checked by the Jobs debugger.
- **The cost of the copies on the main thread was not profiled.**
- **The bench is a .NET upper bound**, not a Burst number, on a loaded host.
- **The menace read is inferred, not scored.** The hold time is measured against the research's window, but the game
  pack's motion was not re-scored by the research's emotion probe.


### 7.7 Round 11-12: the demo cell's opt-in ring hold

**Why it exists.** Garrett's image for the pack is "surrounded, then everything dives in at once". With the research's
quorum this cannot happen against a pilot who flies straight in:
- six hunters reach closure 1.0 within 3-7 s;
- any `q_up` below 1 then fires within about 1 s;
- no setting inside the research's measured ranges gives a held, menacing ring (`Docs/SWARM_FAUNA.md` §27.3).

So the hold is a **designed** beat, off by default.

**The parameter.** `SubstrateSpeciesParams.RingHoldSeconds`:
- **0 is the research's behaviour, bit for bit:**
  - every new branch is guarded on it;
  - harness group K's kernel bit-match is unchanged;
  - `game_params.json` (the research port, test F) carries 0 for all three species.
- **The demo (Swarm) cell's pack is 6 s.** `author_substrate_fauna.py` `DEMO_OVERRIDES` writes it into
  `Substrate Pack Hunter Species.asset`, and `--check` verifies that it reached the asset.
- **The harness reads the value back from the asset** (`DemoPack()`), so the tests prove the shipped number.

**How it works:**
- **The clock.** `SubstrateCore.RingHoldClock` runs at the end of `PilotMoments`, so it runs in both the harness and
  the game tick. It keeps one clock per pilot, because closure is a per-pilot moment and every hunter around one
  pilot reads the same clock.
  - It runs while the closure around that pilot is at or above `q_up`.
  - It waits while the ring has loosened but not broken (between `q_down` and `q_up`).
  - It resets to 0 when the pilot breaks out (closure below `q_down`) or a packmate around that pilot is resting. A
    fresh ring must hold again.
  - Each agent's `RingGate` is its pilot's clock divided by `RingHoldSeconds`.
- **The strike gate** is in `World`, the posture clock where danger is decided. Until the gate reaches 1, a hunter
  that is not yet striking is held at or below `HoldPhase` 0.4 × `DangerPhase`.
  - Released, every hunter climbs from that same phase at `q_rate`. They all cross the danger phase about 0.5 s
    later, together, and that 0.5 s is the telegraph.
- **The look** is in the kernel (`SubstrateKernel.HoldSpeed` / `HoldOrbit` / `HoldTighten`; the job carries
  `RingGate`; the Burst text gate passes). It applies only while the agent's own closure is saturated:
  - top speed is 0.55 × stalk speed (47 u/s), or 0.9 × a faster pilot's speed, so the ring keeps station around a
    moving pilot;
  - the ring slots orbit the pilot at 0.3 rad/s;
  - the ring radius tightens by up to 45% as the hold runs out.
  - The result is a slow, steady, tightening circle: never frozen, never darting. The probe's `advise` named exactly
    these levers: lower `tau_inv`, `approach_retreat`, `unpredict` and `speed_rel`.

**Proof (harness group H, the asset's 6 s, the same 120 u/s wanderer as group P):**

| check | result |
|---|---|
| the ring closes, holds and the pack strikes | 5/5 seeds |
| strikes begun while the ring still held | **0** over 5 × 90 s |
| first ring-closed → first strike | 18-36 s (≥ 6 s) |
| telegraph, release → first strike | **0.5 s** in every seed (research 0.4-0.7) |
| together within 1 s | 100% in every seed |
| ring tightens into the strike | 104→64, 109→98, 86→58, 93→84, 94→82 u |
| held ring before the first strike (menace ≥ 8 s) | 30-48 s; median before any strike 44 s |
| bites on the careless wanderer / on the gap-flyer | 6 / 1 |
| **break-out**: a pilot inside a half-held ring sprints out through the gap | the hold resets in 5/5 dashes, no strike during the dash, the ring re-forms in 5/5 |
| control: the same seed without the hold | strikes 0.7 s after its ring first closes (with it: 36 s) |

**The read (`Tools/Build/emotion_range`, condition `pack_hold`, straight-in hover and cruise replays, `--assert`).**
A ring window reads menacing or eerie before the strike in **6/6 runs**; the research port does so in 0/4 runs that
had a ring window.
- **At cruise** the held ring reads **eerie** (0.33).
- **At hover** the ring reads mostly terrifying (0.34), with menacing and eerie windows during the hold. Six hunters
  closing slowly on a nearly still pilot read as terror.
- **The strike**, almost always bitten at once and so labelled winded, reads terrifying.

**Costs and limits:**
- **It is far less lethal to a fast wanderer.** It lands 6 bites over 5 × 90 s, where the research port lands 20. The
  first strike comes later (37-62 s), because a pilot that keeps moving rarely lets a ring saturate for 6 s. This is a
  balance question for the demo cell.
- **The hold is a designed beat, not an emergent one.** It reads as organic only because the ring keeps circling and
  breaking out resets it.
- **Not run in Unity**, and Burst compiled nothing; as in §7.6, the text gate and the reference compile are the proof.


## 8. Round 11-9: two fixes found by the whole-cell run

The showcase-cell harness (`Tools/Build/showcase_cell_harness`, `Docs/SWARM_FAUNA.md` §25) ran these cores beside
every other creature in the Swarm cell for the first time. It found two cross-system bugs in this folder. Each fix has a
unit group with a negative control in that harness.

- **Engagement was the first population's, for every population (U1).** `SubstrateTickJob.BuildEngaged` used the
  job's ONE `EngageRadius` / `MaxEngaged` for all three populations of the cell's shared core. The cell host builds
  the job from whichever `SubstrateFauna` registers first, so the pack's 260 u / 7 proxies, the locusts' 140 u / 24
  and the lurkers' 160 u / 8 (§5, Proxies) were never what ran: every population used the first one's.
  - `SubstratePopulation` gains `EngageRadius` and `MaxEngaged` (default −1 = the job's settings, so a
    single-population caller is unchanged). `SubstrateFauna.ClaimBlock` sets them from its species asset.
  - U1: two populations in one core, radii 100 u / 300 u, caps 2 / 10. The second engages 10 agents out to 169 u. The
    pre-fix job clips it to 2 within 44 u.
- **A meal queued in the pass that killed its eater vanished (U3).** `SubstrateTickJob.Run` applied the queued kills
  before the queued feeds, and `SubstrateCore.Feed` ignores a dead agent. The prism the bite consumed was already
  gone, so the meal left the cell's books. In the glue's order (`Feed` → `Hunt` → `ShedStarving`, and a vessel's ram
  at any frame) this happens whenever a biting locust is caught by the pack, or a hunter that just fed is shed or
  rammed.
  - The job now applies feeds first, then kills. The meal reaches the body and leaves with it (`MassIn` and `MassOut`
    both count it).
  - `SubstrateTickJob.Killed` publishes each killed agent with the stock it died holding (`SubstrateCore.Kill`'s
    return).
  - U3: a 25-volume locust bites 7.5 and is killed in the same pass: `Killed` = 32.5, `MassIn` +7.5, self-audit 0.
    Negative control: kill-then-feed against the core drops the 7.5.
  - **Still unstated in the game:** the proxy's skeleton is the PUBLISHED body (25 here), so the 7.5 leaves with the
    creature, as a builder's stomach does (`BUILDERS_AND_THIEVES.md` "Unstated volume"). The showcase harness lays
    the difference as a skeleton from `Killed` and its ledger closes. The game glue does not yet: one such meal in a
    5-minute showcase run (9.1 volume).

## 9. Round 11-11: the rest of the bestiary (stampede, mobber, leech, leviathan)

Four more species, as DATA on the same substrate. Each is a `SubstrateSpeciesParams` set
(`SubstrateResearch.Game{Stampede,Mobber,Leech,Leviathan}`) plus new primitives that are off (0) for every older
species. Kernel group K still matches the pre-11c managed step bit for bit for the old species.

Sources:
- the research substrate: `Tools/Ecology/substrate/species.py` (`stampede()`, `leviathan()` and its `BodyPlan`,
  `manta_slots`);
- the bestiary: `bestiary/species/{stampede,mobber,leech,leviathan}.py` and `bestiary/scorecards.json`;
- the burn rules: `/mnt/project-files/overnight/burn-rules.md` (a drain weighs 0.25).

`research_fixture.py` dumps the stampede and leviathan sets, `manta_slots(96)` and 54 bestiary numbers into
`research_params.json`. It reads them by pattern straight out of the bestiary source, so a rewritten rule fails the
dump instead of keeping a stale number. Test F asserts every one, plus every game delta with its reason.

### 9.1 The primitives

| Primitive | Parameters | Mechanism |
|---|---|---|
| **Strike role** | `ChargeEvery` | Only every Nth agent of the block ramps and climbs (the stampede's bulls, every 4th). |
| **Ramp** (the windup) | `RampS`, `RampSpeed`, `RampR`, `RampOnSight` | An armed role agent (gregarious, or on sight; not resting; a pilot inside `RampR`) holds `RampS` seconds at `RampSpeed` before it may harm. A `Windup` event marks the start. Once ramped it strikes until spent. |
| **Bite wind-up** | `StrikeWindupS` | A biter without a ramp must show its intent (aggressive at the gregarious end) for `StrikeWindupS` before its bite may land; a `Windup` event marks the start. The clock holds through a dip and resets once spent or at 0.4 x `DangerPhase`. Game pack 0.4 s (bestiary pack.py `WINDUP`, lab fair burns 2026-10-05); 0 elsewhere. |
| **Strike** | `WStrike`, `StrikeSpeed`, `StrikeAccel`, `StrikeTurn`, `HuntLeadMax` | While ramping and striking it aims at the pilot's lead point (clipped to `HuntLeadMax` s) at its own speed, acceleration and turn rate. The ramped strike IS the danger. |
| **Turns** | `RampTurns` | At most N agents ramp or strike at one pilot at once. An armed agent past the limit WAITS (negative `Ramp`), and the longest waiter goes next. |
| **Alarm climb** | `WAlarmClimb` | The role agent turns UP the alarm gradient, scaled by its phase; its own flee, alarm and threat are muted by the same amount. |
| **Trample closing** | `TrampleClose` | A trample needs velocity · bearing > `TrampleClose` × speed (bestiary 0.3): running INTO the pilot. |
| **Provocation** | `QWProvoke`, `SlowBelow`, `RoostR` | A quorum term: a pilot inside `Sense` that is slower than `SlowBelow`, or inside `RoostR` of the agent's roost. |
| **Rest holds phase** | `RestHoldsPhase` | A resting agent keeps its quorum target (a mobber pulls out of its dive and keeps orbiting). |
| **Jink** | `WJink`, `JinkR`, `JinkCos` | A pilot pointing at the agent inside `JinkR` makes it break sideways off the pilot's line. |
| **Cling** | `ClingMax`, `ClingRelSpeed`, `GripTurn`, `GripLoss`, `GripGain`, `DazeS`, `FlingSpeed`, `SipS`, `SipWeight` | A pouncer that touches a hull LATCHES (at most `ClingMax` per hull) and rides it in the ship's frame. It loses grip while the host turns faster than `GripTurn`; at zero grip it is FLUNG and dazed. Every `SipS` it SIPS: a contact of `SipWeight`. |
| **Contact weight** | `ContactWeight` | The burn-rule weight of a touch on its danger plate: a bite 1, a peck 0.25, a leech 0 (never dangerous). |
| **Body** (research `BodyPlan`) | `AttachRate`, `AttachOnH/OffH/OnF`, `BodySlots`, `BodyScale`, `BodyWell`, `BodySpeed`, `DangerAttached`, `BodyCurious(R)`, `Gulp*` | A sated (or frightened) school assembles: each member's attachment relaxes toward 1 and it is sprung to its slot in a rigid body. The body steers as one, burns to touch while assembled, and gulps a pilot ahead of its mouth. Hungry again, it dissolves. |
| **Sector pen** | `SubstrateCore.SetSector` (glue: SO `sectorAxis`, `sectorHalfAngle`) | A soft pen to a cone about an axis, inside the radial band. |

New events: `Windup` 5, `Sip` 6, `Latch` 7, `Shaken` 8, `Gulp` 9, `Assemble` 10, `Dissolve` 11.

### 9.2 The species

**Stampede** (Mass hearts; 48 seeded in 4 herds, cap 72).
- The research herd (`stampede()`, verbatim): an alarm quorum flips it gregarious.
- Every 4th animal is a BULL. A bull arms on sight inside 260 u, lowers its head for 0.9 s (slowed to 0.2×) and
  charges at 170 u/s (accel 400, turn 2.4 rad/s) for 1.5 s.
- Its own panic is muted, so it climbs the alarm toward its source. Then it rests 4 s.

**Mobber** (Time hearts; 40 seeded on 5 roosts, cap 50).
- Provoked by a slow pilot (< 100 u/s) or one near the roost, the colony mobs: it orbits the hull at 30 u.
- In turn (at most 3 at once), a bird pulls up for 0.8 s and dives at 160 u/s, and a contact inside 6 u is a PECK.
- A peck is a drain: contact weight 0.25.
- Pointed at inside 60 u, a bird jinks aside.

**Leech** (Charge hearts; 48 seeded at flora, cap 64).
- Puddles drift at 12 u/s. A pilot inside 140 u is POUNCED at 150 u/s.
- A touch latches onto the hull (≤ 6 per hull). The rider sips every 1.5 s: a drain of 0.25 through the danger
  effect's `ApplyContact`.
- A turn harder than 1 rad/s loses grip at 1.6 per rad/s per second. At zero grip it is flung at 90 u/s and dazed
  for 2.5 s.
- Its plate never burns (contact weight 0). Its proxy exists to be RAMMED (the bestiary payoff).

**Leviathan** (Space hearts; 96 seeded, cap 128).
- The research grazer school with its manta `BodyPlan`. K = n0 = 96 slots, the research rule;
  `manta_slots(96)` matches the fixture to 3e-6 u.
- The body is drawn at scale 2 (a 120 u manta). The grazers' own bodies are unchanged.
- Sated, the school assembles. Assembled, its members burn to touch.
- It turns toward a pilot within 700 u (bestiary curiosity 0.8). A pilot inside 220 u ahead of the mouth holds the
  jaws open 1.2 s; then the body surges at 115 u/s for 1.6 s and rests 4 s.
- Hungry again (mean hunger > 0.55), it dissolves back into a harmless shoal.

### 9.3 The stampede's open issue: it rarely trampled because it flees AWAY

The research herd, alone, managed 0.3 tramples/min: alarmed, it runs from the threat. This port makes the charge land
without a script.
- The **bulls** arm on sight (the bestiary's rule) and climb the alarm gradient toward its source while the cows
  flee.
- A trample is a ramped bull running INTO the pilot (closing 0.3).

| Measure (group S, 3 seeds × 180 s, a 120 u/s wanderer, 72 in 6 herds) | Value | Research range / reference |
|---|---|---|
| Trample contacts/min, counted the bestiary's way | 10.9 | skimmer 5.3 .. wanderer 10.9 (asserted 2.7 .. 16.3) |
| Harm events/min after the 1 s per-vessel cooldown | 3.4 | — |
| Tramples delivered by a charging bull | 31 / 31 | bestiary nobulls ablation: the herd alone hits nothing |
| Head-down before a trample (median) | 1.9 s | bestiary telegraph 0.7 s; burn rules ≥ 0.25 s |
| Ablation nobulls (the research herd) | 0.00 /min | research open note 0.3 /min |
| Ablation noclimb | 9.3 /min | < 10.9 with the climb |
| Alarm source 200 u off an alarmed herd: bulls / cows | +2.7 / −24.2 u/s | the herd SPLITS: the bulls hold and edge toward it |
| Herd flip after a pilot hovers at its edge | 13.5 s | asserted < 20 s |

### 9.4 The body (leviathan)

The body state is per population (`SubstratePopulation.Body*`, `SlotW/SlotV`). `SubstrateCore.Body` runs in the world
pass. The agent pass reads each member's slot goal and the slot's own velocity (`SlotGoal`, `SlotVel`): the spring is
scaled by `1 + attach` and separation by `1 - attach`.

Measured (group V):
- assembled 0.9 s after seeding sated, with 100 % attached;
- members held a median 7.8 u from their slots (body 120 u long);
- it dissolved hungry again at 26.7 s;
- with a pilot 140 u ahead of the mouth: the gulp began 1.2 s later and the first burn landed at 3.2 s;
- an unfed body falls apart within 60 s;
- no member is dangerous while the body is loose.

### 9.5 Placement

Every band in use was checked against every `author_*.py` model:
- nucleus 392;
- wearer 400-465;
- swarm shells 470-620 / 690-840 / 910-1080;
- builders 845-905 and 1085-1140;
- threat grove rim 1095+ (sector);
- substrate lurker 470-620, pack 690-1080, locust 910-1080.

| Species | Band | Sector (about the cell's Y axis) | Seeded |
|---|---|---|---|
| Mobber | 625-685 u (the free gap between the inner and middle shells) | whole shell | 5 roost clusters |
| Stampede | 690-840 u | axis (1, 0, 0), 55° | 4 herd clusters |
| Leech | 690-840 u | axis (−0.5, 0, 0.866), 55° | at flora hearts in the sector |
| Leviathan | 690-840 u | axis (−0.5, 0, −0.866), 55° | one school at the sector |

`author_substrate_fauna.py --check` asserts the following:
- no two new species share a (shell, sector) pen;
- none overlaps a builder or wearer band;
- none reaches past 1080 u (the grove keeps beyond it);
- the capacities (697) fit the cell substrate's 1024.

Populations spawn in the cell's CONTROLLING domain: the configs carry no domain. There is one crystal per agent, and
riding, assembling and gulping move no mass (group M2).

### 9.6 The collider budget: re-divided, never raised

The Swarm cell's worst case stays **1,192 / 1,200**. The substrate keeps exactly its pre-11-11 39 proxies (78
colliders), re-divided as the harness's `ProxyCaps` table. The author script reads that table and fails if its
proxies differ.

| | Pack | Locust | Lurker | Stampede | Mobber | Leech | Leviathan | Sum |
|---|---|---|---|---|---|---|---|---|
| Before | 7 | 24 | 8 | — | — | — | — | 39 |
| Now | 7 | 12 | 4 | 6 | 4 | 2 | 4 | 39 |

The measured argument (group Q, 3 seeds each): the cap is the previous frame's proxies, built by the tick job's rule
(dangerous first, then nearest, inside the species' engage radius). A contact is covered when an agent in contact had
a proxy.

| Species | Contacts covered | Harm events covered (1 s cooldown) | Most dangerous in the contact horizon at once |
|---|---|---|---|
| Pack (7) | 13/13 | 13/13 | 3 |
| Locust (12) | 105/106 (99.1 %) | 105/106 | 139 |
| Lurker (4) | 9/9 (an ambush lane) | 9/9 | 1 |
| Stampede (6) | 16/16 | 16/16 | 4 |
| Mobber (4) | 263/268 (98.1 %) | 263/268 | 3 (34 without turns) |
| Leech (2) | 18/19 rammable passes (94.7 %) | its plate never burns | — |
| Leviathan (4) | 6/6 | 6/6 | 12 |

Two changes made the small caps hold:
- **Dangerous first.** A diving mobber is pulled up, so it is not the nearest. Nearest-only covered 61 %.
- **Turns.** The mob of 14 was all ramped at once, so 3 dangerous agents became 14. The bestiary says "in turn", and
  `RampTurns` 3 says it too.

The leech asserts ≥ 90 % for rams: a missed ram is a lost swat (payoff), never a phantom burn. The one miss was a
pounce arriving from outside the nearest two at a full hull.

Riders never get a proxy: they would sit inside the hull's collider. The glue retires a rider's proxy at once, and
the tick job skips riders (`SubstrateTickJob.Riding`).

### 9.7 The glue

- **`PrismProperties.DangerWeight`** (runtime, reset to 1 in `Prism.InitializePrismProperties`):
  - `SubstrateAgentFauna.SetDanger(danger, weight)` sets it from `ContactWeight`; weight 0 is never dangerous.
  - `VesselElementalDebuffByDangerPrismEffectSO` scales its resolved size by it. Bites are unchanged at 1.
- **`VesselElementalDebuffByDangerPrismEffectSO.ApplyContact(victim, domain, weight)`** is the effect's one contact
  path. The prism collision uses it, and so does a leech's sip. The two share the per-vessel cooldown and the domain
  rule.
- **`SubstrateFauna.Sip`**:
  - a `Sip` event lands on the vessel `SubstrateCellHost.VesselById` names, through the SO's `contactEffect`;
  - the author script wires the shipped danger effect;
  - unassigned, it warns once.
- **Seeding.** `seedClusters` gives herds, roosts and puddles. With a sector, clusters are seeded at random points of
  the band ∩ sector, and flora seeding is filtered to the sector.
- **The ecology LOD.** All four carry `macroLod` like the others. A population with a rider never collapses
  (`CanCollapse`): a rider is on a pilot.

### 9.8 Proof

`bash Tools/Build/substrate_harness/run.sh all`: groups S, T, C, V, Q and M2, beside the older ones.

| Group | What it asserts |
|---|---|
| T (mobber) | 44.7 pecks/min on a hovering pilot and 44.0 on a 90 u/s skimmer, vs 0.00 on a 140 u/s flyer (bestiary: skimmer 70 uncapped, evader 0). The pull-up precedes every peck: median 0.90 s, min 0.70 s. Weight 0.25. Ablation noprovoke 0.00/min. At most 3 dive at once (34 without turns). The jink breaks 34 u off the line in 0.8 s (5.4 u without). |
| C (leech) | 33 latches, 142 sips (15.8/min; bestiary aware 5.3 .. wanderer 49) and 33 shaken off on a research-turn (2 rad/s) wanderer. At most 6 riders. The first sip comes 1.5 s after the latch. Flying straight: 36 sips, 0 shaken. Turning 2.5 rad/s: 0 sips, 5 flung. Ablation nogrip: 36 sips. The pounce never bites. |
| V (leviathan) | §9.4, plus `manta_slots(96)` vs the fixture (group F). |
| M2 | Seven species, two pilots, 3 min. The agent ledger closes to 1.2e-9 and the world ledger to 1.5e-9. Every starvation is reported. Rides, an assembly and windups ran. |

### 9.9 The whole cell (showcase_cell_harness)

`Tools/Build/showcase_cell_harness/layout.py` reads `author_substrate_fauna.SPECIES`, so the four join the whole-cell
run with no list to edit. `Systems.cs` mirrors the new glue:
- the sector pen;
- cluster and sector seeding;
- flora seeding filtered to the sector;
- each contact weighted by the species' `ContactWeight` (a peck burns a quarter of a bite), so `Pilot.Petals` is now
  a float;
- a leech's `Sip` as a contact of its weight.

The run also exposed a ledger-timing bug in the harness itself. A death is booked when the glue decides it (rammed
out, laid as a skeleton, fed to a hunter), but the core holds the agent's stock until the queued kill lands next tick.
A ledger read between the two, such as a ram on a minute boundary, counted the body twice (−48.9 of 136,400).
`SubstrateSystem.Held` now subtracts the pending kills.

Measured (`run.sh quick`, 1 seed × 2 min):
- C1 colliders: observed max 1,074, under the authored worst case of 1,192 / 1,200.
- C2 combined step: 0.89 ms/frame.
- C3 ledger: 2.4e-7.
- C7: no collapsed population seen.

**Finding: three of the four starve in the cell, as the locusts already do (C4).** The leviathan starved all 96 by
minute 2, with 0 bites of 1,168 asked. The stampede lost 19 of 48 and the mobber 27 of 40.
- The cell feeds the substrate one food point per living plant heart, and the middle shell carries 6-10 Time plants
  shared with the middle swarm.
- An assembled school steers as one body and rarely reaches a heart within its 24 u bite radius.
- The substrate harness scatters food everywhere, which is why it does not see this.

This is a balance question, the same one the locusts and the pack already raise (QA-SWARM-ROUND11-9 step 5). It is
left to the balance pass.

### 9.10 What is NOT proved

- **None of it ran in Unity.** The glue (DangerWeight, `ApplyContact`, sips, sectors, clusters, rider retirement) is
  type-checked against stubs (`substrate_glue_typecheck`, `elemental_transfer_harness`), not played
  (QA-SWARM-ROUND11-11).
- **The leviathan rarely gulps a wanderer.** It scored 0.00 burns/min over 3 × 180 s (bestiary wanderer 0.45,
  skimmer 3.1). Its signature is asserted on a pilot that sits ahead of the mouth, not on a passer-by.
- **The bulls' read is a split, not a charge from afar.** Alarmed with no pilot, a bull edges toward the alarm source
  at 2.7 u/s while the cows flee at 24 u/s. The charge itself arms on sight inside 260 u.
- **Leech ram coverage is 18/19**, a small sample: rams through a puddle are rare because the pouncers latch.
- **The new species were not re-scored by the research's emotion probe.**

## 10. The SIEGE (lab flight/src/70_siege.js)

**Status:** branch `claude/siege-port`. Harness group `siege` (S1-S4), group Q and the author checks pass. **Not yet run
in the Unity editor**: QA-SIEGE-1 in `Docs/QA/QA_BACKLOG.md`.

The lab's flight showcase has a species written for one feeling, in Garrett's words: *"being surrounded by them and
having them all dive in at once"*. He flew it on 2026-10-08 ("it was scary") and approved the port. It is not a research
substrate species: it is a phase machine shared by a whole cloud, so it is ported as its own director
(`SubstrateSiege.cs`) that moves one substrate population in place of the agent kernel. Everything else is the
substrate's: the members are ordinary agents (stock = body, proxies, the danger tier, index entries, eating, breeding,
starving, one crystal per death).

### 10.1 The behaviour

| Phase | What the pilot sees | Ends |
|---|---|---|
| ROAM | A loose cloud of 150 stalks the nearest pilot at ~430 u, working round AHEAD of its line. Harmless. | Pilot within 640 u, cooldown over |
| GATHER | The cloud streams onto a shell of radius 300 round a centre that trails the pilot (100 u/s). Members flow along the surface, the side nearest the cloud first, so the far side is an open IRIS. Members swerve round the pilot. | Half seated after 3 s (or 5 s) |
| CLOSE | The shell shrinks 300 -> 130 u and nearly stops (25 u/s). 0.4 s in, the glow is up: members are danger prisms, and brushing a seated one (35 u) is a BREACH - everyone dives at once. The iris keeps closing. | 2.6 s |
| HOLD | The shell sits at 130 u, pulsing, the gap closing to nothing. | 1.3 s |
| DIVE | Every member sprints at the pilot's position 0.35 s ahead (330 u/s). Each bites at most once. | 1.6 s |
| SCATTER | Burst outward, then ROAM; next GATHER after 10 s (6 s after an escape). | 1.6 s |

Escape = outside the shell by 45 u during GATHER/CLOSE/HOLD: the siege scatters with no bite. The intent (the
telegraph) is 0.15-0.45 in GATHER, 0.55 -> 0.8 in CLOSE, 0.8 -> 1 pulsing in HOLD, 1 in the DIVE. The game draws it as
the regime's size swell (3.4 -> 4.4 u, `Phase` = intent) plus the danger tier's colour while a member may bite.

### 10.2 What the game changes, and why

The phase machine is the lab's `act()` line for line, at the lab's step (3 sub-steps of the 0.1 s tick = 1/30 s); its
clocks are doubles so a phase ends on the lab's step. Every tunable is on `SubstrateSiegeParams` (the species asset's
`Siege` block); the numbers the lab wrote inline (glow delay, dive lead, separation, accelerations, roam jitter) are
named fields too. The changes:

| Change | Why |
|---|---|
| Members are substrate agents: they eat flora (a hungry member drifts to food while it roams, `FoodPull` 150) and split at `BirthStock` | The lab only regrew a rammed member by eating a loose prism. Garrett's rule: every life form must be able to feed and reproduce |
| `Metabolism` 0.0015, `StarveS` 120 | 150 mouths on the cell's scarce flora (§9.9 C4); a fed cloud lives ~13 min without food |
| It picks the pilot nearest its cloud and locks onto it for the encounter; a lost target ends the encounter as an escape | The lab has one pilot |
| With no pilot, the whole cloud drifts toward its hungry members' food and stays in its band | The lab always had a pilot. A cloud that held still grazed its patch bare |
| A leash: a pilot more than `Leash` (300 u) outside the band is never picked, and a target that flies further out is lost (an escape); ROAM stalks from a point clamped into the band | Unleashed, the stalk followed pilots across the cell and the cloud spent 38% of its time in its band (showcase C8) |
| The burn is the platform's: a member is a danger prism exactly while it may bite (`SubstrateSiegeState.Dangerous`) | The vessel's 1 s danger-contact cooldown makes a dive one or two burns, not the lab's one per 0.25 s |
| Rammed members: committed members (CLOSE..SCATTER) are not made un-rammable | The lab's "a dive must never be a crystal fountain" rule needs a shield on proxies; left for play-testing (QA-SIEGE-1 step 6) |

`REGROW_EVERY` (the lab's regrowth clock) has no port: breeding replaces it.

### 10.3 Placement

Time hearts (the lab's siege crystal), hatched as one cloud (spread 70) in 690-1080 u, the pack's band; that band is
also its food field. It is leashed rather than penned: ROAM homes into the band and a hunt follows a pilot up to
`Leash` (300 u) outside it. The shell is a 300 u sphere round the pilot, wider than the 390 u band, so the showcase cell
reports its band occupancy (69%) instead of asserting the 75% floor, as it does for thieves and wearers. Capacity 160 (N0 150): the cell's populations now sum to 857 of the core's
1,024 slots.

### 10.4 Proxies

The siege takes 4 proxies within 200 u from the locust (12 -> 8), so the substrate's share stays 39 proxies / 78
colliders and the Swarm cell's worst case is unchanged by the siege. (On bleeding-edge after the 2026-10-08 merges that
worst case is 1,178 + 23 threat-flora hearts = 1,201, one over the 1,200 ceiling, so `author_swarm_fauna.py` refuses to
write; that predates this port and is not the substrate's share.)

A dive arrives from every side at once: 39 dangerous members in the contact horizon together. "Dangerous first, then
nearest" covered 86% of its harm events at cap 4. The tick job now ranks the DANGEROUS by where they will be a tick from
now (`SubstrateTickJob.BuildEngaged`), which covers 92.2% at cap 4 and leaves every other species as good or better
(group Q, 3 seeds): pack 16/16, locust at 8 still 98/98, lurker 8/8, stampede 18/18, mobber 98.5%, leech 94.7% of
rammable passes, leviathan 3/3. A missed harm event is a bite the pilot did not feel, never a phantom burn.

### 10.5 Proof

`bash Tools/Build/substrate_harness/run.sh siege`. The fixture `siege_fixture.json` is written by `siege_fixture.py`
from the lab branch (`git fetch origin cece/gifted-curie-x2cpd0` first; it reads the lab with `git show` and runs
`siege_trace.js` on the lab's own `flight/sim.js` with node).

| Group | What it asserts |
|---|---|
| S1 fidelity | All 26 SIEGE_DEFAULTS plus the 13 numbers `act()` writes inline, plus N and SIZE, equal the lab's (41). |
| S2 parity | Five lab encounters (wander, evader, breaker, hunter and a hovering pilot), each replayed from the lab's snapshot at the GATHER step with the lab's pilot track: the same phase events on the same steps (escapes, a breach, the full GATHER -> CLOSE -> HOLD -> DIVE -> SCATTER clock), the same bites on the same steps, members within a median 0.005 u of the lab's. Negative controls: without the iris the replay leaves the lab's track; without the web it misses the hunter's breach. |
| S3 game tick | 6 seeds x 3 min per pilot. Careless wanderer: 3.78 encounters/min, escapes 0.25, breaches 0.76, 3.4 bites per encounter (lab 4.00, 0.13, 0.85; the extra escapes are the leash). Gap reader: escapes 0.96 (lab 0.91), 0 bites. Hovering pilot: dived on every encounter. Every bite comes after intent > 0.5 for >= 0.25 s. The danger tier equals "may bite" and is never up in ROAM/GATHER/SCATTER. Largest per-tick move 37.9 u (the dive's 330 u/s; the other species stay under 25.8 u). |
| S4 lifecycle | Real food, 4 min alone then 4 min with a wanderer and a ram every 4 s: it eats (875 u^3), breeds (3 births), survives 59 rams with no starvation (94 alive), keeps hunting, and the ledger closes. Breeding is slow: at `Metabolism` 0.0015 a member is hungry enough to eat only after ~170 s, so 8 min replaces few of the 59 rammed. |
| M2 | The siege joins the whole-bestiary ledger world. |

### 10.6 What is NOT proved

- **Nothing here ran in Unity.** The director is pure C# in the substrate's tick; the glue is the substrate's own,
  unchanged except the proxy ranking. QA-SIEGE-1 is the in-editor proof.
- **That it holds its numbers under heavy ramming.** S4 breeds 3 in 8 min against 59 rams. If play shows the cloud
  thinning out, raise `Metabolism` (it eats, and so breeds, sooner) or lower `BirthStock`; QA-SIEGE-1 step 9.
- **How it feels.** The lab scored burns per encounter under the tuned rule (careless 6.5, skilled 0.4); in the game a
  dive's burns are limited by the vessel's 1 s cooldown, so it is gentler than the lab's numbers.
- **Feeding in the real cell.** The harness scatters food; the cell feeds the substrate one point per plant heart
  (§9.9). Whether 150 members find enough in 690-1080 u is the showcase run's question (`showcase_cell_harness`).
- **Rams during a dive** turn members into crystals (§10.2).

