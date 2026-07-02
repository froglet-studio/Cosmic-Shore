# Networked Fauna Sync — Lava-Lamp (Menu_Main) Plan

**Status: PLAN — not yet implemented.** *(Extraction note, July 2026: this plan was
authored June 12 on `claude/optimistic-maxwell-uet05g`, before the late-June ecology rework —
its §1 "current state" claims predate Burst density-grid fauna queries, the 3-phase ladder,
and fauna reproduction. Treat §1 as historical; re-verify current state before implementing.)* This document is the design + sequenced
backlog for making the lava-lamp fauna (tadpoles, brittlestars, sharks) one
shared, synced population across all party members in Menu_Main, instead of a
divergent per-client simulation. It resolves caveat #4 of
`Docs/ECOSYSTEM.md` §7.2 ("Client-local fauna … diverges across clients").

Read first: `Docs/ECOSYSTEM.md` (the ecosystem bible — §0 conserved mass, §6
prey-linked starvation, §7 diet split), `CLAUDE.md` § "Multiplayer / Netcode"
and § "Don't cheat emergence".

---

## 0. Problem statement

The lava lamp *is* freestyle (one system, two names), and Menu_Main is a
networked scene: under the locked EAGER-Relay design every player hosts a
Relay session in the menu, and party members join as Netcode clients. The
vessels are NetworkObjects and replicate; the **ecosystem does not**. Each
peer runs its own `RandomLifeSpawner` with local `Random` rolls, so:

- Fauna **counts, species mix, spawn positions, and trajectories** differ per
  peer — host sees a shark chasing a tadpole; the client sees neither.
- **Births** (`Fauna.TryReproduce`) and **deaths** (starvation, predation)
  happen independently per peer.
- Consumption diverges: the host's fauna trim the host's trails; the client's
  fauna trim a *different* set of local prisms.

Goal: every peer in a menu party sees **the same creatures in the same places
doing the same things** — births, hunts, kills, and starvations included.

### Non-goals (this plan)

- Syncing **flora placement/growth** (separate follow-up — §6 Phase F).
- Shared **prism identity** for trails (trails stay client-reconstructed from
  replicated vessel motion, as today).
- Authoritative **player→fauna damage** (impacts stay client-local, as they
  are for all prisms today).
- `Worm`/`BodySegmentFauna` and the manager-spawned groups (`BoidManager`,
  `LightFaunaManager`, `WormManager`) — not in the Blob (menu) profile.
- The `IntensityWiseLifeSpawner` scenes (WildlifeBlitz, Tournament) — same
  architecture applies later; menu first.

---

## 1. Current state (verified in code)

| Object | Networked? | How |
|---|---|---|
| Vessels | YES | NetworkObject + owner-auth `ClientNetworkTransform` |
| Players / AI players | YES | Server-spawned NetworkObjects |
| Crystals (game scenes) | YES | `NetworkCrystalManager` — `NetworkList<CrystalSlotData>` driving **local** pooled crystals |
| Cell phase/domain | OPTIONAL | `CellNetworkSync` (NetworkVariables, 0.5 s server mirror) — **not present in Menu_Main**: `Cell.prefab` has no `NetworkObject` and no `CellNetworkSync` |
| Trail prisms | NO | Reconstructed per client from replicated vessel motion |
| Flora | NO | Local `Random` planting + growth |
| **Fauna** | **NO** | Local seeder + reproduction + starvation per client |

**Menu fauna inventory** (`Blob Cell Spawn Profile`, ≤ ~14 alive at caps):

| Species | Prefab | Class | Diet | Seed floor |
|---|---|---|---|---|
| Tadpole | `_Prefabs/FloraAndFauna/MassTadPoleFauna.prefab` | `Boid` (forager) | eats any unshielded prism | 4 |
| Brittlestar | `_Models/Fauna/MassBrittlestarFauna.prefab` | `LightFauna` | herbivore — opposing-domain mass | 3 |
| Shark | `_Models/Fauna/MassSharkFauna.prefab` | `LightFauna` | predator — eats herbivore fauna | 1 |

**Fauna spawn sites** (all three must be authority-gated; there are no others):

1. `CellLifeSpawnerBase.cs:145` — seeder spawn (`SpawnFauna`)
2. `CellLifeSpawnerBase.cs:163-170` — `SpawnFaunaWithDomain`
3. `Fauna.cs:159-162` — `SpawnOffspring` (reproduction)

**Per-creature sim shape** (identical gating points in both species):

- `Initialize(Cell)` — body-prism recolor + scale-in, then starts the behavior
  coroutine (`LightFauna.UpdateBehaviorCoroutine` / `Boid.CalculateBehaviorCoroutine`).
- Behavior tick — starvation check → `Die()`, goal resolution (cell phase /
  prey registry), `Physics.OverlapSphereNonAlloc` sweep doing separation +
  **consume** (`Prism.Consume` / `HealthPrism.Consume` / `prey.Predated()`) +
  `NotifyFed()` (resets starvation, drives `TryReproduce`).
- `Update()` — applies velocity + rotation lerp every frame.
- `Die()` — `Destroy(gameObject)` (or `LightFaunaManager.RemoveFauna`).

---

## 2. Decision — server-authoritative sim, replicated puppets

**One simulation runs on the menu host (the Netcode server). Everyone else
renders puppets.** Chosen over the alternatives:

- **Deterministic lockstep (replicate a seed, simulate everywhere) — rejected.**
  Fauna steering consumes client-local state at every tick: the prey it
  senses are *local* prisms (trails/flora differ per client), the sweep is
  `Physics.OverlapSphere` (non-deterministic across machines), cadence is
  wall-clock `WaitForSeconds`. Divergence is guaranteed within seconds.
- **Manager-level replication (the `NetworkCrystalManager` pattern: one
  NetworkBehaviour owning a `NetworkList<FaunaSlot>` that drives local
  creature instances) — rejected for fauna, kept as precedent.** Crystals are
  quasi-static (spawn/teleport/respawn), so slot replication is cheap and
  sufficient. Fauna move continuously: slot replication would mean
  hand-rolling position interpolation, late-join catch-up, and
  lifecycle semantics that `NetworkObject` + `NetworkTransform` provide for
  free, tested. ~14 creatures is far below the object-count scale where
  per-object overhead would matter.
- **Per-fauna `NetworkObject` + server-authoritative `NetworkTransform` —
  CHOSEN.** Matches the existing precedent for dynamic server-owned moving
  objects (AI players/vessels in `ServerPlayerVesselInitializerWithAI`).
  Birth = `Spawn()`, death = `Despawn()` — replicated to late joiners
  automatically by NGO's synchronization pass.

Authority is simple in the menu because of the locked EAGER-Relay design:
**the NetworkManager is always listening and the local player is the server
unless they joined a party.** Solo menu = host alone = server runs the sim
exactly as today, replication is a no-op. The same gate
(`IsListening && !IsServer` → puppet) therefore covers solo, party host, and
party client with one code path. True-offline contexts (no NetworkManager,
e.g. tool scenes) never spawn the NetworkObject, and every new component is
inert when unspawned — the `CellNetworkSync` "optional component" philosophy.

### Why this is not an emergence cheat

Nothing about the ecosystem's *rules* changes: mass stays conserved, the only
prism sinks remain active forces (vessel abilities + fauna consumption),
populations are still bounded by feeding/starvation/predation — computed once
on the server instead of N divergent times. Replication is transport, not
outcome-hard-coding. The one genuinely judgment-call piece is §3.5 cosmetic
grazing, which exists precisely to *preserve* conservation on clients.

---

## 3. Architecture

### 3.1 Prefab changes (3 menu fauna prefabs)

Add to `MassTadPoleFauna`, `MassBrittlestarFauna`, `MassSharkFauna` roots:

- `NetworkObject`
- `NetworkTransform` — the **stock server-authoritative** one (NOT
  `ClientNetworkTransform`; fauna are server-owned). Tune: sync position +
  rotation only (no scale — body prisms scale-in locally per client), half
  floats on, position threshold ~0.1, rot threshold ~1–2°. Fauna drift slowly;
  the default interpolation is fine.
- `FaunaNetworkSync` (new, §3.2)

Register all three in `DefaultNetworkPrefabs.asset`.

Body `HealthPrism` children stay exactly as they are — they ride the root
transform and are initialized locally on every peer (LightFauna deliberately
does not register body prisms with the Cell, `LightFauna.cs:47-64`).

### 3.2 `FaunaNetworkSync : NetworkBehaviour` (new)

`Assets/_Scripts/Controller/Environment/FloraAndFauna/FaunaNetworkSync.cs`,
namespace `CosmicShore.Gameplay`. The fauna counterpart of `CellNetworkSync`
— replication only, the creature still owns its behavior:

- `NetworkVariable<Domains> NetDomain` (server-write). Server stamps it in the
  spawn helper before `Spawn()`; clients apply it in `OnNetworkSpawn` (read
  `.Value` directly — late joiners get no `OnValueChanged`, same note as
  `CellNetworkSync.OnNetworkSpawn`).
- **Client (`!IsServer`) on spawn:** put the sibling `Fauna` into puppet mode
  (§3.3) *before* running visual init, then run the client-side slice of
  `Initialize`: resolve the host `Cell` (scene singleton in the menu), recolor
  + scale-in body prisms with `NetDomain`. Server path: no-op (the spawner
  already ran full `Initialize`).
- `PlayDeathFX_ClientRpc(byte reason)` — lets clients play the
  starvation-wither / predation-implosion juice before the despawn removes
  the object (§3.6).
- When never network-spawned (offline/tool scenes): all callbacks silent,
  fauna behaves exactly as today.

### 3.3 `Fauna.IsSimAuthority` + authority-aware despawn

Two small additions to the `Fauna` base:

- `public bool IsSimAuthority { get; }` — default `true`; set `false` by
  `FaunaNetworkSync` on client puppets. Gates, in both species:
  - behavior coroutine: full tick only when authority; puppets run the
    reduced grazing tick (§3.5),
  - `Update()` movement/rotation: skip when puppet (NetworkTransform owns the
    transform),
  - starvation check, goal resolution, `NotifyFed`/`TryReproduce`,
    `Predated()` calls: authority only.
- `protected void DespawnSelf()` — used by every `Die()` path instead of raw
  `Destroy(gameObject)`: if this object is a spawned `NetworkObject` and we
  are the server → `NetworkObject.Despawn(destroy: true)` (after firing
  `PlayDeathFX_ClientRpc`); otherwise → `Destroy(gameObject)` as today.
  Calling `Destroy` on a replicated NetworkObject from a client is an NGO
  error — this helper is what makes the existing `Die()` call sites safe.

`AssignLineage` (cell registry + per-species caps) stays **server-only** —
the spawner calls it and the spawner only runs on the server, so no change
needed; puppets simply never register, and `OnDestroy`'s unregister is a
no-op for them.

### 3.4 Spawn-site authority gating

One new helper on `CellLifeSpawnerBase` (used by all three sites in §1):

```
Fauna SpawnFaunaInstance(Fauna prefab, Vector3 pos, Quaternion rot, Domains domain)
    → Instantiate
    → if (NetworkManager listening && IsServer): stamp NetDomain, NetworkObject.Spawn(destroyWithScene: true)
    → return
```

Plus the loop-level gate: `RandomLifeSpawner`'s **fauna** loops (and
`Fauna.TryReproduce`) early-out when `IsListening && !IsServer`. Flora loops
are untouched in this phase (still local everywhere — see §4). Clients
therefore never originate fauna; they only receive replicated spawns.

`Fauna.SpawnOffspring` routes through the same helper so births replicate
identically to seeds.

### 3.5 Client puppets: cosmetic grazing (required, not optional)

The naive puppet ("disable everything, let NetworkTransform move it") breaks
**mass conservation on clients**. Trails and flora are client-local; today
each client's *local* fauna are the active force that consumes them. If
puppets ate nothing, client-side trail mass would accumulate forever (no
decay exists, by design — `Docs/ECOSYSTEM.md` §0) while the host's gets
trimmed. That is a regression, and re-introducing any cap/TTL to compensate
is an explicitly rejected cheat.

So puppets keep exactly one slice of the behavior tick — the **consume
sweep**:

- Same cadence coroutine, same `OverlapSphereNonAlloc`, same diet filter, but
  ONLY the `Prism.Consume` / `HealthPrism.Consume` branch. The replicated
  body passes through the client's local mass and eats what it touches —
  the same active force, applied to each peer's local representation.
- **Not** in the puppet tick: starvation → `Die` (server decides lifespan),
  `prey.Predated()` (fauna are shared NetworkObjects now — only the server
  may kill one), `NotifyFed`/`TryReproduce` (population is server state),
  goal/steering/velocity (NetworkTransform owns motion).

Convergence properties: trails are near-identical across peers (same vessel
paths), so puppet grazing trims client trails almost exactly where the server
trims its own. Flora positions diverge (local planting RNG), so a puppet may
chew through empty space where the server had a gyroid, and vice versa — a
visual artifact accepted in this phase, fixed properly by Phase F (flora
placement sync), and bounded meanwhile by the Frenzy planting gate.

Implementation note: extract the consume branch of
`LightFauna.UpdateBehavior` / `Boid.CalculateBehavior` into a
`TickConsume(consumeRadius)` method called from both the full tick and the
puppet tick — no duplicated diet logic.

### 3.6 Death, predation, starvation

All lethal decisions are server-only:

- Starvation: server's `IsStarving` fires `Die()` → `PlayDeathFX_ClientRpc`
  → `DespawnSelf()`. Puppets vanish with the right juice everywhere.
- Predation: server's shark calls `prey.Predated()`; the prey despawns
  network-wide. Spawn immunity (`predationImmunitySeconds`) only needs to
  exist server-side.
- Reproduction: server spawns offspring via the §3.4 helper; clients see a
  new creature pop in (body-prism scale-in already reads as a birth).

### 3.7 Cell prerequisite — phase/domain authority in the menu

`Cell.prefab` (the Blob Cell instance in Menu_Main) currently has **no
`NetworkObject` and no `CellNetworkSync`**. Add both (in-scene-placed
NetworkObject; Menu_Main is loaded through Netcode scene management, so it
replicates). Reasons:

- Server fauna decisions read `Cell.Phase` / `ControllingDomain` — already
  consistent because they run on one machine — but client-side **visuals**
  (puppet consume-radius/cadence aggression multipliers, the
  `DomainVolumeIndicator` spawn-ring UI, `IsDangerImmune`) read the *local*
  cell, which would otherwise disagree with what the replicated fauna do.
- `CellNetworkSync` exists for exactly this and is already
  client-reconciling; it just isn't wired in the menu.

### 3.8 Scene transitions & cleanup

- Fauna spawn with `destroyWithScene: true` — when the host drives the
  Netcode load out of Menu_Main, fauna die with the scene on all peers.
- Belt-and-braces: the spawner (server) subscribes to `GameDataSO.OnLaunchGame`
  → stop fauna loops + `Despawn(true)` all live brood **before** the scene
  load. This avoids the known message-batching race (a NetworkObject spawned
  on the same tick a client processes a scene load gets destroyed client-side
  — the `[Invalid Destroy]` AI-spawn lesson in CLAUDE.md). Fauna normally
  spawn mid-session, so the race window is only around launch-time births;
  closing the spawner first removes it entirely.
- Return-to-menu: scene reload re-runs the seeder fresh on the server; no
  explicit cleanup needed beyond the above.

### 3.9 Late joiners

Party members joining an in-progress menu get every live fauna from NGO's
connection synchronization pass: `FaunaNetworkSync.OnNetworkSpawn` runs on
each, reads `NetDomain.Value`, puppet-izes, and visual-inits. No bespoke
catch-up code (this is the main thing the per-object approach buys over the
slot-list approach).

---

## 4. Explicitly divergent after this plan (and why that's OK for now)

| Layer | State | Consequence | Fix |
|---|---|---|---|
| Trails | client-reconstructed, near-aligned | puppet grazing trims them consistently | none needed |
| Flora | local RNG placement/growth | fauna sometimes chew "empty space" on a client | Phase F |
| Consume precision | per-peer overlap results | a specific prism eaten on host may survive on client (and vice versa); aggregate converges | Phase G (optional) |
| Player→fauna damage | client-local impacts | a client's hit on a fauna body prism is visual-only; server health is authoritative | future impact-authority work, same bucket as trails |

---

## 5. Phased backlog

**Phase 0 — Cell network prerequisite** *(small, independent, ship first)*
- Add `NetworkObject` + `CellNetworkSync` to `Cell.prefab`; verify the
  Menu_Main instance replicates phase + dominant domain host→client in MPPM.
- Exit: client's `Cell.Phase`/`DominantDomain` match host within one mirror
  tick (0.5 s); single-player scenes unaffected.

**Phase 1 — Fauna sync core** *(the bulk of this plan)*
1. `Fauna.IsSimAuthority` + `DespawnSelf()`; route both `Die()` overrides.
2. Extract `TickConsume` in `LightFauna` + `Boid`; gate full tick vs puppet
   tick; gate `Update()` movement.
3. `FaunaNetworkSync` component (NetDomain, puppet init, death-FX RPC).
4. Prefab edits (NetworkObject + server-auth NetworkTransform + sync
   component) on the three menu fauna prefabs; register in
   `DefaultNetworkPrefabs.asset`.
5. Spawn helper + authority gates on the three spawn sites; reproduction
   routed through it.
6. `OnLaunchGame` brood-despawn + spawner stop (server).
- Exit: F-series tests below pass in MPPM; solo menu and offline scenes
  byte-for-byte behavior-identical to today.

**Phase F — Flora placement sync** *(follow-up, separate doc/PR)*
- Crystal-pattern manager (`NetworkLifeSpawner` or `CellNetworkSync`
  extension): server replicates plant events (species, position, domain);
  clients plant locally at identical spots. Growth stays local. This is what
  makes fauna goals visibly "about" the same gyroids on every peer.

**Phase G — Consume-event replication** *(optional, only after F)*
- Server broadcasts compact consume events (position + domain filter);
  clients consume their nearest matching prism via `PrismSpatialIndex`
  queries (never a parallel spatial store — `Docs/SPATIAL_INDEX.md`).
  Re-evaluate need once F lands; puppet grazing may already be close enough.

---

## 6. Bandwidth / perf budget

- **Wire**: ≤ ~14 server-auth NetworkTransforms with thresholds + half floats
  — a fraction of what 2 player vessels already cost; well inside the 4-player
  Relay budget. NetDomain is write-once. Death RPCs are rare.
- **Client CPU**: puppets *drop* goal resolution, steering math, separation,
  starvation, and reproduction; they *keep* the OverlapSphere consume sweep
  (same cost as today's full local sim, minus per-hit branches). Net: client
  frame cost ≤ today.
- **Server CPU**: unchanged (it already ran the full sim as a local player).
- Validate with the Performance Benchmark tool's NetMarkers (the
  `CrystalSlotData` serialize markers are the precedent).

---

## 7. Test matrix

**MPPM (F-series, run with `Docs/PartySystem/TESTS.md` S-series conventions):**

| # | Scenario | Expect |
|---|---|---|
| F1 | Solo menu (no party) | Ecosystem identical to pre-change behavior |
| F2 | Party of 2 in menu | Same fauna count/species/positions/headings on both screens (≤ interpolation lag) |
| F3 | Late joiner | All live fauna appear, correct domains, mid-swim |
| F4 | Predation | Shark kill removes the same tadpole on both screens, with death FX |
| F5 | Starvation | Despawn replicates with wither FX |
| F6 | Reproduction | Birth appears on both screens at the same spot |
| F7 | Launch game from party menu | No fauna leak into game scene; no `[Invalid Destroy]` on either peer; repeat while births are active |
| F8 | Return to menu | Seeder re-seeds; party intact; fauna replicate again |
| F9 | Offline/tool scene with fauna | No NGO errors; fauna fully local as today |
| F10 | Client trails | Puppet grazing trims the client's trail mass (fly loops, watch them get eaten) |

**Edit-mode (NUnit):** authority-gate truth table (no-NM / listening-server /
listening-client × spawn/tick/die paths); `DespawnSelf` routing; puppet tick
never calls `Predated`/`NotifyFed` (behavior seam test).

---

## 8. Key files

| Concern | File |
|---|---|
| Fauna base — add `IsSimAuthority`, `DespawnSelf`, gate `SpawnOffspring` | `Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs` |
| Brittlestar/Shark tick split (`TickConsume`) | `Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs` |
| Tadpole tick split | `Assets/_Scripts/Controller/Environment/FloraAndFauna/Boid.cs` |
| NEW — replication component | `Assets/_Scripts/Controller/Environment/FloraAndFauna/FaunaNetworkSync.cs` |
| Spawn helper + authority gates | `Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs` |
| Fauna-loop gate + `OnLaunchGame` brood despawn | `Assets/_Scripts/Controller/Environment/RandomLifeSpawner.cs` |
| Phase/domain replication (Phase 0 wiring) | `Assets/_Scripts/Controller/Environment/CellNetworkSync.cs`, `Assets/_Prefabs/Environment/Cell.prefab` |
| Menu fauna prefabs (NetworkObject + NetworkTransform + FaunaNetworkSync) | `Assets/_Prefabs/FloraAndFauna/MassTadPoleFauna.prefab`, `Assets/_Models/Fauna/MassBrittlestarFauna.prefab`, `Assets/_Models/Fauna/MassSharkFauna.prefab` |
| Netcode prefab registration | `Assets/DefaultNetworkPrefabs.asset` |
| Pattern precedents | `NetworkCrystalManager.cs` (slot replication), `ServerPlayerVesselInitializerWithAI.cs` (dynamic server-owned spawns, destroyWithScene lesson) |

---

## 9. Open questions (decide before/while implementing Phase 1)

1. **Puppet grazing cadence** — full aggression-scaled cadence, or a fixed
   relaxed cadence (e.g. Level0) to shave client CPU? Default: same cadence,
   measure first (Debugging Methodology: profile, don't guess).
2. **Death-FX transport** — `ClientRpc` (proposed) vs a `NetLifeState`
   NetworkVariable with a delayed despawn. RPC is simpler; NetworkVariable
   survives the edge case of an RPC arriving after despawn. Start with RPC +
   despawn next frame; revisit if FX visibly drop.
3. **Game scenes in the same pass?** HexRace's Skim Race forager swarm has the
   same divergence (it's called out in the same ECOSYSTEM caveat). The
   architecture carries over 1:1 (`RandomLifeSpawner` there too), but it adds
   `PopulationSize 12` tadpoles to the wire and MPPM surface. Default: menu
   first, Skim Race as a fast-follow once F1–F10 are green.
