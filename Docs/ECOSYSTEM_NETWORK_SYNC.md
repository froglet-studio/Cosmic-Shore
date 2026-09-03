# Networked Ecology — server-authoritative fauna, replicated flora placement

**Status: CODE COMPLETE + WIRED. In-editor MPPM verification is owed (the human gate).**

The rule this implements, stated by the prompter:

> *Flora should spawn synced — the same species in the same place — but **how it grows is
> random for each player**. Fauna are not that case: fauna should be network synced.*

Those are two different contracts and the split is the whole design. A plant is a growth RULE
and its form is emergent from the peer it grows on; a creature is a thing that MOVES and eats,
and two peers disagreeing about where it is means they disagree about the food web.

Read first: `CLAUDE.md ▸ Ecosystem Design Principles (LOCKED)`, `Docs/ECOSYSTEM.md` (§6–§7 the
food web, §26 the wither, §40 element-as-identity), `Docs/SPATIAL_INDEX.md` (the movers
contract).

---

## 0. What this adds, and what it deliberately does not

**Replication transport only.** Every ecology DECISION — seeding, goals, feeding, starvation,
predation, reproduction, death — is the same code it was, computed once on the server instead of
N divergent times. Mass conservation, no-imposed-death, wither-to-crystal, controlling-colour
spawn, volume-as-the-spine and endogenous selection are untouched.

Continuity of existence gains one new obligation: **a replicated death withers on every peer
before the object despawns** (§3.4). Collider budget: **zero new colliders** — the bodies already
existed on every peer.

---

## 1. Why fauna are the harder half

Fauna are the platform's only legal down-force on accumulating mass. Today every peer simulates
its own divergent swarm, which is why `ECOSYSTEM.md` §7.2 caveat 4 blocks using them in
competitive play at all — and why, in **Wildlife Liberation**, whose entire objective is *killing
creatures*, two players are scoring against two different populations.

The catch that decides the architecture: if clients only RENDER fauna, nothing consumes the
client's local prisms, so the perf win lands on the host alone and mass conservation loses its
only sink on every client. So a puppet keeps **two** duties and drops everything else —
see §3.2.

---

## 2. Authority — one rule, one implementation

`FaunaNetworkSync.IsSimAuthority` ⇔ `!networkSessionLive || isServer`.

Under the locked EAGER-Relay design the NetworkManager is **always** listening, so a naive
`IsServer` test would stand the ecology down in every offline and tool scene. Solo play, offline
mode and a party host all simulate exactly as before; only a party **client** becomes a puppet
renderer. `FloraNetworkSync.IsSimAuthority` delegates to it rather than restating it — "who
simulates the ecology" is ONE question, and a second copy is a second thing to forget to update
(`EcologyNetworkAuthorityTests` is what fails if somebody re-implements it).

---

## 3. Fauna — server-authoritative creatures

### 3.1 Transport

Per creature: `NetworkObject` + stock **server-authoritative** `NetworkTransform`
(`AuthorityMode: 0`, position + rotation, **no scale** — body prisms scale in locally per peer;
half floats, position threshold 0.1, rotation threshold 1°, unreliable deltas) +
`FaunaNetworkSync`. Birth = `Spawn()`, death = `Despawn()`; late joiners get the live population
from NGO's own synchronization pass with no bespoke catch-up code.

### 3.2 What a puppet still does

- **The movers contract.** `NotifyBodyPrismsMoved()` every frame — the body prisms are
  registered, MOVING mass, so their spatial-index entries must follow the replicated transform
  or this peer's AOE and fauna senses target the spawn point forever.
- **Grazing.** `LightFauna.UpdatePuppetGraze` / `Boid.UpdatePuppetGraze` run the authority
  path's own mouthful — same diet predicates, same cluster query, same suction — against THIS
  peer's local mass. Same active force, applied to each peer's own representation, so the
  prism-count reduction lands everywhere and mass conservation keeps its sink.

It does **nothing** else: no goal coroutine, no steering, no starvation, no predation, no
reproduction, no velocity integration (the NetworkTransform owns the pose). Net client CPU is
**lower** than before — the decision half of every tick is gone.

### 3.3 Identity is replicated, because identity is gameplay

`FaunaIdentity` (domain, species index, palette index, element) is written **before** `Spawn()`,
so the whole identity rides the spawn payload — no Blue-then-recolour flicker, no wrong-element
heart for a frame, and a late joiner reads it straight out of the sync pass.

This is not cosmetic. A lifeform is its species and its ELEMENT and nothing else
(`ECOSYSTEM.md` §40), and the element decides the body scale, the variant tuning and the
**heart's world scale** — which IS the collect reward and the live domain fauna buff. A client
that re-rolled its own element would pay a different price for the same kill.

The species travels as an **index into the host cell's own spawn profile**, because a
ScriptableObject reference does not cross the wire and both peers resolve the same cell config
for a scene (the intensity that picks it is itself synced through `GameDataSO.GameConfigSynced`).
The palette index is carried separately because with `SpreadElements` the tuning comes from a
palette SIBLING, and the sibling is what states the body scale and the heart size — element
alone does not name it.

### 3.4 Death — the wither replicates, the husk is removed by its owner

1. Server: the sealed `Fauna.Die` runs as always (crystal drop + `OnDeath`), and flips
   `NetLifeState → Dying` carrying the **death STYLE**, because the style IS the animation
   (`ECOSYSTEM.md` §26): jousted unravels outward from the heart, starved inward from the
   extremities, devoured suctions into a mouth. Replicating only "it died" plays the wrong death
   on every client.
2. Clients: run the same sealed `Die` locally — each peer drops its own crystal and withers its
   own body, so mass is conserved per peer and nothing pops out.
3. Server: despawns only after its own wither **plus `despawnGraceSeconds` (0.5 s)**, so a
   client whose wither started ~RTT later is never clipped.
4. A client never destroys a replicated fauna itself (an NGO error); `Fauna.DespawnOrDestroy`
   routes the two husk-removal points (`LightFauna.RemoveHusk`, `Boid.FadeOutAndRemove`), never
   `Die`, which must keep running everywhere the death is *decided*.

**Attribution is authority-gated.** `Fauna.ReportKill` early-outs on a puppet. Without that, a
networked kill would be counted **twice** for the shooter: once by the server crediting
directly, and again by the client's replicated death re-entering `StatsManager`'s client branch
and firing `Player.ReportFaunaKill_ServerRpc`.

### 3.5 Combat damage — the one thing a client originates

**Projectiles are not networked.** A bullet is a pooled local object on whichever machine fired
it, which is exactly why `Player.ReportFaunaKill_ServerRpc` exists for the SCORE. Damage needs
the same owner-detects / server-decides round trip, or a client's kill is a kill on the client's
screen alone — the creature dies there and swims on for everyone else. In Wildlife Liberation,
where shooting creatures IS the mode, that is worse than not syncing at all.

So: the shooter destroys its own local copy of the body prism immediately (the hit has to read
instantly) and calls `ReportBodyPrismDestroyed_ServerRpc(prismIndex, killerName)`. The server
applies the same loss to the ONE simulation and fans it out to the other peers with
`DestroyBodyPrism_ClientRpc`, so a creature that SURVIVES the hit (a worm colony losing 1 of 26)
looks the same everywhere. If it was the last prism, the server's own `OnBodyPrismExploded` runs
the sealed death and the wither replicates back.

The prism is named by its **index in `Fauna.BodyPrisms`** — `GetComponentsInChildren` order over
an identical prefab hierarchy, so it names the same prism on every peer with nothing new to keep
in sync.

### 3.6 The spawn seam

`CellLifeSpawnerBase.SpawnFaunaBanded` — the one spawn call BOTH spawners share — calls
`FaunaNetworkSync.ServerSpawn` **after** `AssignLineage`, because the lineage bind is what rolls
this individual's element and the element is the identity the payload carries. Reproduction
(`Fauna.SpawnOffspring`) does the same. The same helper carries the **client gate**: a client
never originates a replicated species, or it would be running a second, invisible swarm.

It goes there and not in the loops for the reason the banded placement does: `IntensityWise`
silently swaps which spawner a cell runs, so a gate implemented in one of them is dead code in
exactly the modes that asked for it.

---

## 4. Flora — replicated PLACEMENT, local growth

Flora carry **no NetworkObject at all**. A forest is thousands of plants and one NetworkObject
each is not affordable; one slot list on the Cell is a few bytes per plant, paid once at
planting. That is the structural difference from fauna, which MOVE and each need a transform
stream.

`FloraNetworkSync` (on the Cell, beside `CellNetworkSync`) holds a
`NetworkList<FloraSlotData>` of planting DECISIONS — species index, root pose, domain, element,
state. The server registers a slot when it plants and flips it to `Withered` when the plant
dies; a client plants the same species at the same pose in the same domain carrying the same
element, and then **grows it locally**. Late joiners read the whole list on connect, so they
reconstruct the standing population **without the host's world being torn down**. Slots are
REUSED after a wither so an hours-long session cannot grow the late-join payload without bound.

**The fidelity contract is deliberate and is the whole point:** same species, same place, same
domain, same element — **not** the same shape. Growth consults the LOCAL `PrismSpatialIndex`
(it reserves against this peer's own occupancy, which includes this peer's own trails), so a
plant's form is emergent per peer by construction. A shared seed could not make two peers'
forests identical and is not attempted. That is also why there is no growth mirror on the wire:
replicating a growth rule's output would cost continuously for a fidelity nobody asked for.

`fromReplication` is a distinct parameter from `domainOverride` on purpose — **reproduction also
pins a domain**, and an offspring IS a new decision that must replicate. Keying the seam off
`domainOverride` would have silently excluded every offspring from the wire.

---

## 5. Rollout — one species at a time, and the reason the gate is DATA

The gate is `FaunaConfigurationSO.NetworkSynced` / `FloraConfigurationSO.NetworkSynced`, authored
per species, **not** inferred from the prefab. Affordability is a property of the POPULATION, not
of the prefab: the same shark prefab is 32 creatures in one biome's profile and could be 900 in
another.

That matters here more than the original plan assumed. **Wildlife Liberation's roster is 519
seed / 1,198 cap** — two orders of magnitude past the ~14 creatures the first design was sized
against. One NetworkObject + NetworkTransform per creature is not free at that count, especially
for a client on a long link.

| Species | Seed | Cap | Class | Networked? |
|---|---|---|---|---|
| Shark | 32 | 68 | `LightFauna` | **YES — step 1** |
| Brittlestar | 99 | 228 | `LightFauna` | wired, off |
| QuadFish | 383 | 893 | `LightFauna` | wired, off — the count to be careful about |
| Worm colony | 5 | 9 | `WormFauna` + `WormSegmentFauna` | **excluded, see below** |

**Shipped ON: Shark only.** It is the smallest population in the roster, so it is the honest
first measurement of what replication costs per creature, and the other three species are the
control — if sharks match across screens and quadfish do not, the mechanism is working. Turning
the next species on is one field on four assets.

**The worm colony is excluded deliberately, not forgotten.** It is a colony: the root grows its
members at runtime as separate `WormSegmentFauna` objects, each carrying its own heart. Networking
the root alone would give clients a headless worm. Replicating a colony means replicating its
TOPOLOGY (which segments exist, in what chain, after which splits), which is its own slice.

Prefabs wired with `NetworkObject` + `NetworkTransform` + `FaunaNetworkSync` and registered in
`DefaultNetworkPrefabs.asset`: `MassSharkFauna`, `MassBrittlestarFauna`, `QuadFish`,
`TadPoleFauna`. A species whose prefab is wired but whose config is off is fully local — a
half-wired species degrades to local, never to broken.

`FloraNetworkSync` is on the Cell in all 8 networked scenes and is **inert until a flora config
opts in** (none does today). Note Wildlife Liberation authors **no flora at all**
(`SupportedFloras: []`), so flora sync is not testable there — Rampage's cactus belt is the
place to try it.

---

## 6. Test matrix (MPPM)

| # | Scenario | Expect |
|---|---|---|
| F1 | Solo / offline | Behaviour identical to pre-change (authority rule = offline simulates) |
| F2 | Party of 2, WL | The **same sharks** in the same places on both screens; quadfish/brittlestar still diverge (the control) |
| F3 | Late joiner | Live sharks appear correctly; one mid-wither withers rather than popping |
| F4 | **Client shoots a shark** | It dies on EVERY screen, withers, drops its crystal, then despawns |
| F5 | Client shoots a shark once (survives) | The same body prism is gone on every screen |
| F6 | Scoring | The kill credits the shooter **once** — check the client's score does not double |
| F7 | Starvation / predation | Same shark dies on all peers, wither + crystal, no pop-out |
| F8 | Launch a game mid-spawn | No fauna leak, no `[Invalid Destroy]` on any peer |
| F9 | Tool scene | No NGO errors; fully local behaviour |
| F10 | Bandwidth | Watch the NetDiag overlay with 68 sharks live — this is the number that decides whether Brittlestar and QuadFish can follow |

---

## 7. Key files

| Concern | File |
|---|---|
| Fauna replication (the ONLY fauna file importing `Unity.Netcode`) | `Controller/Environment/FloraAndFauna/FaunaNetworkSync.cs` |
| Flora placement replication | `Controller/Environment/FloraNetworkSync.cs` |
| Authority flag, puppet entry, replicated identity/death/damage, husk routing | `.../FloraAndFauna/Fauna.cs` |
| Puppet graze + gated movement + husk routing | `.../FloraAndFauna/LightFauna.cs`, `Boid.cs` |
| Spawn seams + client gates | `.../Environment/CellLifeSpawnerBase.cs` |
| Per-species rollout gate | `Utility/DataContainers/FaunaConfigurationSO.cs`, `FloraConfigurationSO.cs` |
| Authority truth table | `_Scripts/Tests/Editor/EcologyNetworkAuthorityTests.cs` |
| Cell phase/domain replication (already shipped) | `.../Environment/CellNetworkSync.cs` |

## 8. Known gaps (open, not hidden)

- **The worm colony is not replicated** (§5) — colony topology is its own slice.
- **Flora sync is wired but no species opts in.** Nothing has been measured; Rampage is the
  place to try it.
- **Fauna spawned outside a cell's spawn profile stay peer-local** — the freestyle conveyor and
  the Lifeform Matrix toy release creatures that no profile lists, so there is no species index
  to name them by.
- **Crystal collection stays local per peer**, as it is for every crystal outside
  `NetworkCrystalManager`'s modes. An authoritative collect channel is a separate slice.

## An un-opted species must not leave its NetworkObject behind (2026-09-01)

The per-species opt-in (`FaunaConfigurationSO.NetworkSynced`) is authored on the CONFIG while
the NetworkObject lives on the PREFAB, so an un-opted species instantiates creatures that each
carry a live, never-spawned NetworkObject. That is not merely dead weight: Netcode adopts every
un-spawned NetworkObject as an in-scene placed object when a server starts, and two instances of
one prefab collide in its scene-object index — which threw inside
`NetworkSceneManager.PopulateScenePlacedObjects` and stopped every guest from joining any host
that had restarted in place (`Docs/PartySystem/BUGS.md` B16).

All four fauna prefabs (`QuadFish`, `TadPoleFauna`, `MassSharkFauna`, `MassBrittlestarFauna`)
carry the full staged rig - NetworkObject **and** `FaunaNetworkSync` - while all 42 fauna configs
leave `NetworkSynced` off. That is the documented pre-rollout state and is not a defect: the audit
reports it as INFORMATION, per prefab rather than per config. It warns only about a prefab with a
NetworkObject and NO rig (liability with no rollout behind it), and errors on a config that is
opted IN whose prefab cannot honour it.

`FaunaNetworkSync.ServerSpawn` therefore calls `NetworkSceneObjectGuard.NeutralizeStray` on both
of its declining branches — the species is not opted in, or this peer is not a live server — so
a creature that will never replicate loses its network layer at birth. Opting a species IN is
unchanged: set `NetworkSynced` and the same seam spawns it. The shipped opt-in state is OFF for
every fauna config, so today every creature takes the strip.
