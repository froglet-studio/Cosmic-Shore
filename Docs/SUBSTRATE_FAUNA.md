# Substrate fauna: the Living Ecology agent substrate in the game (round 11b)

**Status:** branch `overnight/substrate` (round 11b), `overnight/substrate2` (round 11c: the Burst agent pass, the ladder,
the pack's menace hold - §7). Headless harness, Roslyn type-check and asset generators all pass. **Not yet run in the
Unity editor**: see QA-SWARM-ROUND11-2 and QA-SWARM-ROUND11-3 in `Docs/QA/QA_BACKLOG.md`.

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
| `SubstrateKernel.cs` | Round 11c: ONE agent's step, `SubstrateKernel.StepAgent`, a static pure function over `SubstrateAgentSoA` spans, Burst-shaped (§7). |
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
| `SubstrateAgentJob.cs` | Round 11c: `SubstrateAgentJob`, the `[BurstCompile]` `IJobParallelFor` that runs `SubstrateKernel.StepAgent`, and `SubstrateAgentPass`, the host's NativeArray mirror that schedules it (§7). |

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
- When closure crosses `q_up` 0.65 (round 11c; bestiary 0.55) they strike **together** at ~168 u/s.
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
| `Assets/_SO_Assets/Substrate Fauna/Substrate {Pack Hunter,Locust,Lurker} Species.asset` | The species. Every `species:` number is from `game_params.json`. |
| `Assets/_SO_Assets/Substrate Fauna/Substrate {…} Fauna Config Data.asset` | One population each, with its band and heart element. |
| `Assets/_Prefabs/FloraAndFauna/SubstrateAgent.prefab` | The proxy. Derived each run from `author_swarm_fauna.tadpole_prefab()` with `SubstrateAgentFauna` in the member's place. |
| `Assets/_Prefabs/FloraAndFauna/Substrate{Pack,Locust,Lurker}Fauna.prefab` | The anchors. |

`author_swarm_fauna.py` owns the Swarm cell and calls this script through two hooks:
- `profile_entries()` adds the three configs to the spawn profile's `SupportedFaunas`.
- `proxy_colliders()` adds 2 × (7 + 24 + 8) = **78** to the collider ceiling check: 1056 worst case vs 1200.

| Population | Band | Why |
|---|---|---|
| Lurker | 470-620 u (Mass flora) | Mimics the Mass crystals it is seeded among. |
| Pack | 690-1080 u | Covers the locusts' shell, so it is never led to food it cannot reach (checked). |
| Locust | 910-1080 u (Space flora) | Grazes the Space flora. |

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
| Locust | 140 u | 24 |
| Lurker | 160 u | 8 |

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
| **P** pack | 5/5 seeds: the ring closes and then strikes. Telegraph 0.4-0.7 s from ring-closed to first strike (bestiary 0.9 s). 100% of the pack turns dangerous within 1 s of the first. The ring tightens (e.g. 112→76 u). Winded hunters are never dangerous and are slower (71 vs 106 u/s). **Menace before terror** (round 11c): the ring is held 10.6-21.9 s before the first strike in every seed (≥ one 8 s emotion-probe window); median stalk before any strike 10.7 s over 20 strikes. A careless pilot takes 20 bites per 5 × 90 s; a pilot who flies at the gap takes 1. Ablation `q_w_close = 0`: no strike. |
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

## 7. Round 11c: the Burst agent pass, the ladder, the pack's menace

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
  as far as we know (Burst 1.8 docs), but the Burst Inspector is the proof (QA-SWARM-ROUND11-3).
- **Burst's floats are not bit-matched.** Burst's math intrinsics may round differently from .NET's. The bit-match is
  .NET-to-.NET. Behaviour under Burst is expected to match to float rounding, not bits.
- **The safety system was not run.** The job's `[NativeDisableParallelForRestriction]` writes (slot `Live[q]`, not
  `q`) and the chain over shared arrays were not checked by the Jobs debugger.
- **The cost of the copies on the main thread was not profiled.**
- **The bench is a .NET upper bound**, not a Burst number, on a loaded host.
- **The menace read is inferred, not scored.** The hold time is measured against the research's window, but the game
  pack's motion was not re-scored by the research's emotion probe.

