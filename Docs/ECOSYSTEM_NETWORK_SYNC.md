# Networked Fauna Sync — Server-Authoritative Fauna (Plan v2)

**Status: CODE COMPLETE — in-editor wiring + MPPM verification owed (the human gate).**
Decisions confirmed by the prompter: **D1 = transforms + client-local grazing**,
**D2 = client-local crystal drop on wither**, **D3 = rollout as planned**. The runtime
infrastructure (§3) and the rollout editor tool are implemented on PR **#597**; no
prefab is networked until the corresponding `Tools ▸ Cosmic Shore ▸ Fauna Sync` step
is run in-editor, so shipped behavior is unchanged until then. Revised after merging
`Ys-bleeding-edge` (PrismSpatialIndex fauna senses, sealed wither-to-crystal death
path, volume-as-the-spine, proximity collider-LOD). Supersedes v1 of this doc.

**In-editor rollout (one step at a time, verify between steps — §5/§7):**
`Tools ▸ Cosmic Shore ▸ Fauna Sync ▸ 0 — Wire Cell Phase Sync` →
`1 — Network Tadpole Prefabs` → `2 — Network Brittlestar Prefab` →
`3 — Network Shark Prefab`, then `Validate Fauna Network Setup` (checks components,
`GlobalObjectIdHash`, and `DefaultNetworkPrefabs` registration).

**Confirmed direction (prompter):** fauna are the universal prism-count reducer —
the only *legal* down-force on trail/flora accumulation (mass is conserved; no
decay). To use them as a perf lever in every scene/game mode, all peers must share
ONE fauna population. Scope now: **server-authoritative spawning + transform/
lifecycle sync for all fauna species, one prefab at a time.** Flora replication is
explicitly deferred.

Read first: `CLAUDE.md ▸ Ecosystem Design Principles (LOCKED)`,
`Docs/ECOSYSTEM_MASTERPLAN.md` (§4 collider contract, §8 netcode discipline),
`Docs/ECOSYSTEM.md` (§6–§7 food web), `Docs/SPATIAL_INDEX.md` (movers contract).

---

## 0. Why fauna sync is a PERF feature

Prism count is the dominant frame cost (ECOSYSTEM_MASTERPLAN §4). The only
invariant-legal way to reduce it is fauna consumption (foragers grazing trails —
the Skim Race hypothesis, ECOSYSTEM.md §7.2B). Today fauna are client-local and
divergent, so their grazing benefit — and population — differs per peer, and
`ECOSYSTEM.md` §7.2 caveat 4 blocks using them in competitive play at all.

Server-authoritative fauna make the population identical everywhere, which makes
the grazing perf lever deployable in every mode. **Note the catch (decision point
D1, §9):** if clients only *render* fauna, nothing consumes the client's local
prisms — the perf win would land on the host only. The puppet grazing tick (§3.5)
is what delivers the prism reduction on every peer.

**Invariants restated (ecology protocol):** this plan adds replication transport
only. Mass conservation, wither-to-crystal, no-imposed-death, controlling-color
spawn, volume-as-spine, endogenous selection — all decision logic stays exactly
where it is, computed once on the server instead of N divergent times. Continuity
of existence gains a new obligation: **a replicated death must wither on every
peer before the object despawns** (§3.6). Collider budget: **zero new colliders**
(§4).

---

## 1. Current state (verified post-merge)

| Layer | Networked? | Notes |
|---|---|---|
| Vessels / Players / AI | YES | NetworkObjects; AI = dynamic server-owned spawn precedent |
| Crystals (game scenes) | YES | `NetworkCrystalManager` — NetworkList slots driving local objects |
| Cell phase/domain | OPTIONAL | `CellNetworkSync` exists; **NOT on `Cell.prefab`** → inactive in Menu_Main |
| Trail prisms | NO | Client-reconstructed from replicated vessel motion (near-aligned) |
| Flora | NO | Local RNG placement + growth (fully divergent) |
| **Fauna** | **NO** | Local seeder/reproduction/starvation per peer (fully divergent) |

**Fauna code facts the design must respect (all landed in the recent merge):**

- **Senses/consume ride the spatial index**, not physics: `PrismSpatialIndex.QuerySphere`
  into shared `Fauna.PrismScratch`; one layer-masked physics overlap remains for
  vessels only (`Fauna.NonPrismOverlapMask`). (`LightFauna.UpdateBehavior`,
  `Boid.CalculateBehavior`)
- **Movers contract:** fauna body `HealthPrism`s are registered, MOVING prism mass —
  `Update()` must call `Fauna.NotifyBodyPrismsMoved()` every frame or index data
  (AOE, senses) goes stale. (`Fauna.cs` §body prisms)
- **Sealed death path:** `Fauna.Die()` is non-virtual — drops the elemental crystal
  (`LifeFormCrystal`), then `OnDeath()`. Removal happens at the END of a wither:
  `LightFauna.WitherCoroutine → RemoveHusk()`, `Boid.FadeOutAndRemove()`. The
  dropped `Crystal` is a **local** object (`Crystal : CellItem : MonoBehaviour`).
- **Volume is the spine:** prey gate = `Cell.OpposingVolume` (environment volume);
  phase/dominant from `Cell.LiveVolume`; count is only the Frenzy perf backstop.
- **Spawn sites (fauna):** exactly three —
  `CellLifeSpawnerBase.SpawnFauna` / `SpawnFaunaWithDomain` and
  `Fauna.SpawnOffspring` (reproduction). Seeder loop: `RandomLifeSpawner.
  SpawnFaunaTypeLoop_Random` (menu + most scenes); `IntensityWiseLifeSpawner`
  (WildlifeBlitz/Tournament) spawns via the same base helpers.

**Live species** (Blob/menu + Skim Race profiles):

| Species | Prefab | Class | Diet |
|---|---|---|---|
| Tadpole | `_Prefabs/FloraAndFauna/MassTadPoleFauna.prefab` (+Space/Time variants) | `Boid` (forager) | any unshielded prism |
| Brittlestar | `_Models/Fauna/MassBrittlestarFauna.prefab` | `LightFauna` | opposing-domain mass |
| Shark | `_Models/Fauna/MassSharkFauna.prefab` | `LightFauna` (predator) | herbivore fauna |

`QuadFish` (placeholder) and `Worm` (segmented, not a single Fauna root) are out of
scope until authored/redesigned.

---

## 2. Decision — one authority model, per-fauna NetworkObjects

**The server (menu/game host — under the locked EAGER-Relay design the NetworkManager
is always listening and the local player is the server unless they joined a party)
runs the ONE simulation: seeding, goals, feeding, reproduction, starvation,
predation. Clients render puppets.** Solo play: host-alone = server → identical to
today, replication is a no-op. True-offline (no NetworkManager): all new components
inert, behavior unchanged — the `CellNetworkSync` optional-component philosophy.

Transport per fauna: **`NetworkObject` + stock server-authoritative
`NetworkTransform`** (NOT `ClientNetworkTransform` — server-owned). Birth =
`Spawn()`, death = `Despawn()`; late joiners get the live population from NGO's
synchronization pass for free.

Rejected alternatives (recorded so they aren't relitigated):

- **Deterministic lockstep** (sync a seed, simulate everywhere): fauna steering
  consumes client-local state every tick (local prisms via the index, physics
  vessel overlap, wall-clock `WaitForSeconds`). Divergence in seconds. Dead end.
- **Manager slot-list** (`NetworkCrystalManager` pattern): right for quasi-static
  crystals; for ~14 continuously-moving creatures it means hand-rolling the
  interpolation, late-join catch-up, and lifecycle that NetworkTransform provides
  free and tested.
- **Refactor the whole spawning system:** unnecessary. The decision logic (seeder,
  food web, reproduction rules) is sound and stays; only an authority gate + one
  consolidated instantiation seam are added (§3.4).

---

## 3. Architecture

### 3.1 Prefab changes (per species, rolled out one by one — §5)

On each fauna prefab root: `NetworkObject` + stock `NetworkTransform` (server-auth)
+ `FaunaNetworkSync` (§3.2). NetworkTransform tuning: sync position + rotation
only, **no scale** (body prisms scale in locally per peer), half floats, position
threshold ~0.1, rotation threshold ~1–2°. Register the prefab in
`DefaultNetworkPrefabs.asset`.

Body `HealthPrism` children are untouched — they ride the root transform and
initialize locally on every peer (`CacheBodyPrisms` + `ChangeTeam` + `Initialize`).

### 3.2 `FaunaNetworkSync : NetworkBehaviour` (new)

`Assets/_Scripts/Controller/Environment/FloraAndFauna/FaunaNetworkSync.cs`,
namespace `CosmicShore.Gameplay` — the fauna counterpart of `CellNetworkSync`:
replication only, the creature keeps owning its behavior.

- `NetworkVariable<Domains> NetDomain` (server-write). Server stamps before
  `Spawn()`; clients read `.Value` in `OnNetworkSpawn` (late joiners get no
  `OnValueChanged` — same late-join note as `CellNetworkSync`).
- `NetworkVariable<byte> NetLifeState` (`Alive` / `Withering`, server-write) —
  drives replicated death (§3.6). A NetworkVariable (not an RPC) so a peer joining
  mid-wither still sees a withering husk, not a pop-out.
- **Client on spawn:** set the sibling `Fauna` to puppet mode (§3.3) *before*
  visual init, then run the client slice of `Initialize`: resolve the host `Cell`,
  apply `NetDomain` to body prisms, kick the scale-in. Server: no-op (spawner
  already ran full `Initialize`).
- Never network-spawned → all callbacks silent; fauna fully local as today.

### 3.3 `Fauna.IsSimAuthority` + despawn routing

- `Fauna.IsSimAuthority` (default `true`; cleared by `FaunaNetworkSync` on client
  puppets). Authority-only: goal coroutine (`UpdateGoalCoroutine`), starvation
  check, `NotifyFed`/`TryReproduce`, `Predated()` — i.e. every *decision*.
- **Despawn routing hooks the husk-removal points, not `Die()`** (which is sealed
  and must keep running everywhere the death is *decided* — the server):
  `LightFauna.RemoveHusk()` and the end of `Boid.FadeOutAndRemove()` call a new
  `protected Fauna.DespawnOrDestroy()`: spawned NetworkObject && server →
  `NetworkObject.Despawn(true)`; otherwise → existing behavior
  (`LightFaunaManager.RemoveFauna` / `Destroy`). Clients never destroy a
  replicated fauna locally (NGO error) — their wither ends in a hide, and the
  server's despawn removes the object (§3.6).

### 3.4 Spawn seam — consolidate, don't refactor

One helper on `CellLifeSpawnerBase` (also used by `Fauna.SpawnOffspring` via a
small static or the host cell):

```
Instantiate → set domain/goal → Initialize(host) →
if (NetworkManager listening && IsServer):
    stamp NetDomain → NetworkObject.Spawn(destroyWithScene: true)
```

Loop-level authority gate: `RandomLifeSpawner`/`IntensityWiseLifeSpawner` **fauna**
loops and `Fauna.TryReproduce` early-out when `IsListening && !IsServer`. Clients
never originate fauna. Flora loops untouched (still local everywhere — deferred).

### 3.5 Client puppets — what still runs (D1: the perf-critical decision)

A puppet's `Update()` keeps running with two jobs: **`NotifyBodyPrismsMoved()`**
(movers contract — NetworkTransform moves the root, the body prisms' index entries
must follow or client-side AOE/senses target the spawn point) — and *nothing else*
(no velocity integration; NetworkTransform owns the transform).

The behavior tick has two candidate scopes — **this is decision D1 (§9):**

- **(A) Transforms-only** (the literal minimal scope): puppets run no behavior
  tick. Same creatures visible everywhere; server's prisms get grazed; **client
  prism counts do NOT drop** (nothing consumes client-local prisms) and trails
  accumulate unboundedly on clients — the perf goal lands on the host only, and
  mass-conservation's only sink is missing on clients.
- **(B) Transforms + local grazing tick** *(recommended)*: puppets run a reduced
  tick — the existing `QuerySphere` consume sweep only (`Prism.Consume`/
  `HealthPrism.Consume` where the diet allows), **no** starvation/`Die`, **no**
  `Predated()`, **no** `NotifyFed`→reproduction, no steering. The replicated body
  eats what it passes through on each peer's local mass — same active force,
  applied to each peer's local representation. This is what makes the prism-count
  reduction land on every client. Implementation: extract the consume branch of
  `LightFauna.UpdateBehavior` / `Boid.CalculateBehavior` into `TickConsume()`
  reused by both paths — no duplicated diet logic.

Trails are near-aligned across peers (reconstructed from the same replicated
vessel motion), so puppet grazing trims client trails almost exactly where the
server trims its own. Flora positions diverge (local RNG) — a puppet may chew
empty space where the client has no plant; accepted until flora replication
(deferred, §6).

### 3.6 Death under the continuity law (wither-to-crystal, replicated)

Server decides every death (starvation / predation). Sequence:

1. Server: `Die()` runs as today — crystal drop + `OnDeath()` starts the wither —
   and `FaunaNetworkSync` sets `NetLifeState = Withering`.
2. Clients: on `NetLifeState → Withering`, run the same `OnDeath()` visual path
   (wither rings / shrink-out) on the puppet. Movement replication is irrelevant
   from here (the husk is stationary).
3. Server: wither completes → `DespawnOrDestroy()` → `Despawn(true)` after a small
   authored grace (`despawnGraceSeconds`, ~0.5 s) so a client wither started ~RTT
   later isn't clipped mid-ring. **Nothing pops out.**
4. **Crystal drop (decision D2, §9):** the sealed `Die()` drops the crystal — but
   only the server runs `Die()`. Recommended v1: `NetLifeState → Withering` also
   triggers the *client-local* crystal drop (`crystal.ActivateCrystal()` on the
   puppet's authored crystal), so the collectible appears on every peer at the
   same spot — mass conserved everywhere. Collection remains local-per-peer in v1
   (same as all crystal pickups outside `NetworkCrystalManager` modes); an
   authoritative collect channel is a later, separate slice.

### 3.7 Cell prerequisite

Add `NetworkObject` + `CellNetworkSync` to `Cell.prefab` (verified absent today) so
phase + dominant domain replicate in Menu_Main and every cell-bearing scene.
Client-side reads that must agree with the server's fauna: aggression-scaled
consume radius/cadence (puppet grazing juice), `IsDangerImmune`,
`DomainVolumeIndicator`. `CellNetworkSync` is already late-join-safe.

### 3.8 Scene transitions & cleanup

- Fauna spawn `destroyWithScene: true` — Netcode scene loads clean them up on all
  peers.
- Belt-and-braces: on `GameDataSO.OnLaunchGame` the server stops fauna loops and
  `Despawn(true)`s live brood *before* the scene load — closes the known
  same-tick spawn/scene-load batching race (the `[Invalid Destroy]` AI-spawn
  lesson) around launch-time births.

### 3.9 Late joiners

NGO's sync pass spawns every live fauna on connect; `FaunaNetworkSync.
OnNetworkSpawn` puppet-izes and visual-inits from `NetDomain.Value` +
`NetLifeState.Value` (a mid-wither husk withers, not pops). No bespoke catch-up
code.

---

## 4. Performance fixes preserved (explicit non-regression checklist)

The design deliberately adds transport around, never inside, the recent perf work:

| Recent fix | How this plan preserves it |
|---|---|
| `PrismSpatialIndex` fauna senses (SPATIAL_INDEX Phase 2) | Untouched on server; puppet grazing (D1-B) reuses the same `QuerySphere` sweep. No physics queries added anywhere. |
| **Movers contract** (`NotifyBodyPrismsMoved`) | Puppet `Update()` keeps the per-frame call — index data stays honest on every peer. |
| Proximity collider-LOD (`PrismColliderLodManager`) | Untouched. Fauna sync adds **zero colliders**; body-prism colliders already exist per peer and stay inside `MaxLivePopulation` caps. |
| Cell density grids driven by the index (Phase 3) | Clients keep registering local prisms exactly as today; puppet grazing keeps client grids converging toward the server's instead of diverging upward. |
| Shared scratch buffers (`OverlapScratch`/`PrismScratch`) | Puppet tick uses the same shared buffers on the same main thread — no new allocation. |
| AOE batch processing / `EcosystemPerfProbe` | Untouched. The probe's `[ECOSIM]` line is the verification instrument (§5). |
| Volume-as-spine cadence (0.25 s volume recompute) | Decision reads (`OpposingVolume`, `ControllingDomain`) stay server-side only — clients stop paying goal/steering/decision CPU entirely (a client-side *saving*). |

Wire cost: ≤ ~14 server-auth NetworkTransforms with thresholds + half floats — a
fraction of two vessels' replication; NetDomain/NetLifeState are write-once/rare.
Verify with the Benchmark tool's NetMarkers if wanted.

**Net client CPU: lower than today** (decision half of every tick removed; grazing
half kept only under D1-B). **Server CPU: unchanged** (it already ran the full sim
as a local player).

---

## 5. Rollout — one species at a time (each step in-editor verifiable)

0. **Cell prerequisite** — `Cell.prefab` + `CellNetworkSync` (§3.7). Verify in
   MPPM: client phase/dominant match host within 0.5 s.
1. **Infrastructure** — `IsSimAuthority`, `DespawnOrDestroy`, `TickConsume`
   extraction, spawn-seam helper + loop gates, `FaunaNetworkSync`. Zero prefabs
   networked yet → zero behavior change anywhere (F1/F9 regression tests).
2. **Tadpole** (`Boid` forager, the prism-eater — the perf lever) — network the
   `Mass/Space/Time TadPoleFauna` prefabs, register, verify F2–F10 + client trail
   grazing (the D1-B payoff) in Menu_Main.
3. **Brittlestar** (`LightFauna` herbivore) — same steps.
4. **Shark** (`LightFauna` predator) — adds replicated predation (F4): shark kill
   removes the same tadpole/brittlestar on all peers via server `Predated()` →
   wither → despawn.
5. **Game-mode adoption** — enable in Skim Race profile (12-tadpole swarm), then
   WildlifeBlitz/Tournament (`IntensityWiseLifeSpawner` shares the gated base
   helpers). Watch `[ECOSIM] prisms=/colliders=/fps=` per peer: the success
   metric is **client prism count tracking the host's downward**.

Worm / QuadFish: deferred (not authored into live profiles).

---

## 6. Deferred (unchanged decisions, recorded)

- ~~**Flora placement/growth replication**~~ — **IMPLEMENTED (Option B, prompter-
  confirmed):** `FloraNetworkSync` on the Cell replicates each plant DECISION
  (species index into the profile's `SupportedFloras`, root pose, domain) as a
  `NetworkList<FloraSlotData>` slot — late joiners reconstruct the whole standing
  population from the initial list sync (the host's world is never destroyed on a
  join). A low-cadence server mirror (2 s) tops up per-flora `GrowthTicks` (clients
  fast-forward as a paced one-`Grow()`-per-frame bloom-in, capped) and flips slots
  to `Withered` on death — clients then run the same `LifeForm` death path locally
  (crystal drop + spindle wither; continuity + mass conservation per peer). Slots
  are REUSED after wither so hours-long sessions don't grow the late-join payload.
  **Fidelity contract (deliberate):** same species, same place, same domain,
  approximately same size — NOT byte-identical shape. Growth consults the LOCAL
  spatial index (`TryReserve` against local occupancy, incl. client-local trails),
  so shape is emergent per peer by construction; a shared seed cannot fix that and
  is not attempted. Client planting loops are authority-gated off; flora spawned
  outside the profile (Wanderway conveyor) stay peer-local (documented).
- **Consume-event replication** (`PrismSpatialIndex` nearest-match) — only
  worthwhile after flora placement sync; trails don't need it.
- **Authoritative crystal collection + player→fauna damage authority** — same
  bucket as all client-local impacts today.
- **Genome/heredity netcode** (MASTERPLAN Phase C) — this plan is its
  prerequisite: birth/trait decisions will already be server-side at one seam.

---

## 7. Test matrix (MPPM, F-series)

| # | Scenario | Expect |
|---|---|---|
| F1 | Solo menu | Behavior identical to pre-change |
| F2 | Party of 2 | Same fauna count/species/positions on both screens |
| F3 | Late joiner | Live fauna appear correctly; mid-wither husk withers |
| F4 | Predation | Same prey dies on all peers — withers, drops crystal, then despawns |
| F5 | Starvation | Same — wither + crystal on every peer, no pop-out |
| F6 | Reproduction | Birth appears on all peers (scale-in bloom) |
| F7 | Launch game mid-births | No fauna leak; no `[Invalid Destroy]` on any peer |
| F8 | Return to menu | Seeder re-seeds; replication resumes |
| F9 | Offline/tool scene | No NGO errors; fully local behavior |
| F10 | Client grazing (D1-B) | Client's own trail prisms get eaten; `[ECOSIM] prisms=` falls on the client |

Edit-mode: authority-gate truth table; `DespawnOrDestroy` routing; puppet tick
never calls `Predated`/`NotifyFed`/`Die`.

---

## 8. Key files

| Concern | File |
|---|---|
| `IsSimAuthority`, `DespawnOrDestroy`, offspring gate | `Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs` |
| `TickConsume` split (brittlestar/shark) | `.../FloraAndFauna/LightFauna.cs` |
| `TickConsume` split (tadpole) | `.../FloraAndFauna/Boid.cs` |
| NEW replication component | `.../FloraAndFauna/FaunaNetworkSync.cs` |
| Spawn-seam helper | `.../Environment/CellLifeSpawnerBase.cs` |
| Fauna-loop gates + launch despawn | `.../Environment/RandomLifeSpawner.cs`, `IntensityWiseLifeSpawner.cs` |
| Cell phase replication (step 0) | `.../Environment/CellNetworkSync.cs`, `_Prefabs/Environment/Cell.prefab` |
| Fauna prefabs | `MassTadPoleFauna` (+Space/Time), `MassBrittlestarFauna`, `MassSharkFauna` |
| Netcode registration | `Assets/DefaultNetworkPrefabs.asset` |
| Precedents | `NetworkCrystalManager.cs`, `ServerPlayerVesselInitializerWithAI.cs` |

---

## 9. Decision record (confirmed by the prompter before coding)

- **D1 — puppet grazing: CONFIRMED (B), transforms + client-local grazing tick.**
  Implemented as `LightFauna.UpdatePuppetGraze` / `Boid.UpdatePuppetGraze`: the
  consume sweep only, over the (smaller) consume/grazing radius, revalidated by the
  existing `EatPrism` predicates and drained through the existing
  `maxConsumesPerFrame` pacing. Predator puppets run no tick at all.
- **D2 — death crystal: CONFIRMED, client-local drop on wither.** Implemented by
  routing the replicated `Withering` state through the same sealed `Fauna.Die` on
  every peer — each peer drops its own crystal and withers extremities-first;
  collection stays local (as all crystal pickups outside `NetworkCrystalManager`
  modes). The server despawns the husk only after its wither + a configurable
  `despawnGraceSeconds` (0.5 s default on `FaunaNetworkSync`).
- **D3 — rollout order: CONFIRMED as §5.** Encoded as the numbered
  `Tools ▸ Cosmic Shore ▸ Fauna Sync` menu steps; no prefab is networked until its
  step is run, so each species lands and verifies independently.

### Implementation notes (what landed where)

| Piece | Where |
|---|---|
| Authority rule (`IsSimAuthority`, pure + runtime) + spawn seam (`ServerSpawn`) + launch teardown (`ServerDespawnBrood`) + death replication (`NotifyDied`/`HandleHuskRemoval`, `NetDomain`/`NetLifeState`) | `FaunaNetworkSync.cs` (the ONLY environment file importing `Unity.Netcode`) |
| Per-creature authority flag, puppet entry, replicated-death entry, husk despawn routing, reproduction gate, goal-coroutine gate | `Fauna.cs` |
| Puppet graze tick + gated movement + husk routing | `LightFauna.cs`, `Boid.cs` |
| Spawn-seam calls + `OnLaunchGame` brood teardown + `SpawnsSuppressed` | `CellLifeSpawnerBase.cs` |
| Seeder-loop authority gates (wave tick + spawn-ring telemetry keep running on clients) | `RandomLifeSpawner.cs`, `IntensityWiseLifeSpawner.cs` |
| Rollout + validation menu items | `_Scripts/Editor/FaunaNetworkSetupTool.cs` |
| Authority truth-table tests | `_Scripts/Tests/EditMode/FaunaNetworkAuthorityTests.cs` |

Known v1 divergences (accepted, documented in §4/§6): flora positions per peer (so
puppet grazing on flora is approximate until flora replication), client-side
Wanderway-conveyor releases stay local-only on party clients, and NucleusRush wave
*data* on clients reports `spawned=0` (the tick itself still fires every period —
re-verify Brood Rush scoring in MPPM, test F11).
