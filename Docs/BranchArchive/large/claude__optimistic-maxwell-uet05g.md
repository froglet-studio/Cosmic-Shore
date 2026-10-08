# Branch archive: `claude/optimistic-maxwell-uet05g`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

## What this branch is

**Networked fauna and flora sync**

Made the living ecosystem (creatures and plants inside the Cell) consistent across multiplayer players. The server decides what creatures do and clients mirror them as 'puppets' that graze, and plant growth decisions are replicated without resetting the world. It includes a rollout tool and prefab updates for sharks, brittlestars and tadpoles.

- **Status:** Redone elsewhere
- **Areas:** Ecology (fauna/flora), Multiplayer/Netcode, Cell, Editor tools
- **Already in bleeding-edge:** bleeding-edge LightFauna.cs contains UpdatePuppetGraze() ('A replicated puppet takes the GRAZING half'), FaunaNetworkSync is referenced across bleeding-edge (BoidManager.cs, CellLifeSpawnerBase.cs), and there is the commit 'feat(wildlife-liberation): kill target 250; fauna sync retires the divergence caveat'.
- **Risk if deleted:** low
- **Suggestion (2026-10-08):** can be deleted after archiving — Server-authoritative fauna/flora sync is live in bleeding-edge.

## Evidence

- **Last commit:** 2026-07-17 by Yash Sadhukhan
- **Unmerged commits:** 15
- **Forked from:** `f457260ed` (2026-07-16, Merge pull request #596 from froglet-studio/claude/merge-bleeding-edge-conflic)
- **Tip:** `f6467fbdc`
- **Files touched (27):**
  - `Assets/DefaultNetworkPrefabs.asset`
  - `Assets/_Models/Fauna/MassBrittlestarFauna.prefab`
  - `Assets/_Models/Fauna/MassSharkFauna.prefab`
  - `Assets/_Prefabs/Environment/Cell.prefab`
  - `Assets/_Prefabs/FloraAndFauna/MassTadPoleFauna.prefab`
  - `Assets/_Prefabs/FloraAndFauna/SpaceTadPoleFauna.prefab`
  - `Assets/_Prefabs/FloraAndFauna/TimeTadPoleFauna.prefab`
  - `Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/Boid.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/BranchingFlora.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/FaunaNetworkSync.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/FaunaNetworkSync.cs.meta`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/Flora.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/LifeForm.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs`
  - `Assets/_Scripts/Controller/Environment/FloraNetworkSync.cs`
  - `Assets/_Scripts/Controller/Environment/FloraNetworkSync.cs.meta`
  - `Assets/_Scripts/Controller/Environment/IntensityWiseLifeSpawner.cs`
  - `Assets/_Scripts/Controller/Environment/RandomLifeSpawner.cs`
  - `Assets/_Scripts/Editor/FaunaNetworkSetupTool.cs`
  - `Assets/_Scripts/Editor/FaunaNetworkSetupTool.cs.meta`
  - `Assets/_Scripts/Tests/EditMode/FaunaNetworkAuthorityTests.cs`
  - `Assets/_Scripts/Tests/EditMode/FaunaNetworkAuthorityTests.cs.meta`
  - `Docs/ECOSYSTEM.md`
  - `Docs/ECOSYSTEM_NETWORK_SYNC.md`

### `b542457a9` — docs(ecosystem): rev fauna sync plan to v2 — post-merge mechanisms, perf motivation, per-species rollout

_Claude, 2026-07-16 20:24:33 +0000_

```text
Rescopes the plan to the confirmed direction: server-authoritative fauna
spawning + transform/lifecycle sync as a prism-count perf lever, rolled out
one species at a time; flora replication deferred. Updates every mechanism
to the merged foundation (PrismSpatialIndex senses + movers contract, sealed
wither-to-crystal death path, volume-as-spine), adds the continuity-law death
replication design (NetLifeState wither before despawn, client crystal drop)
and an explicit non-regression checklist for the recent perf fixes.
```

```text
 Docs/ECOSYSTEM_NETWORK_SYNC.md | 362 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 362 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 368 lines)</summary>

```diff
diff --git a/Docs/ECOSYSTEM_NETWORK_SYNC.md b/Docs/ECOSYSTEM_NETWORK_SYNC.md
new file mode 100644
index 000000000..afcdec39d
--- /dev/null
+++ b/Docs/ECOSYSTEM_NETWORK_SYNC.md
@@ -0,0 +1,362 @@
+# Networked Fauna Sync — Server-Authoritative Fauna (Plan v2)
+
+**Status: PLAN v2 — awaiting prompter confirmation before any code.** Revised after
+merging `Ys-bleeding-edge` (PrismSpatialIndex fauna senses, sealed wither-to-crystal
+death path, volume-as-the-spine, proximity collider-LOD). Supersedes v1 of this doc.
+Reviewed on **PR #597**.
+
+**Confirmed direction (prompter):** fauna are the universal prism-count reducer —
+the only *legal* down-force on trail/flora accumulation (mass is conserved; no
+decay). To use them as a perf lever in every scene/game mode, all peers must share
+ONE fauna population. Scope now: **server-authoritative spawning + transform/
+lifecycle sync for all fauna species, one prefab at a time.** Flora replication is
+explicitly deferred.
+
+Read first: `CLAUDE.md ▸ Ecosystem Design Principles (LOCKED)`,
+`Docs/ECOSYSTEM_MASTERPLAN.md` (§4 collider contract, §8 netcode discipline),
+`Docs/ECOSYSTEM.md` (§6–§7 food web), `Docs/SPATIAL_INDEX.md` (movers contract).
+
+---
+
+## 0. Why fauna sync is a PERF feature
+
+Prism count is the dominant frame cost (ECOSYSTEM_MASTERPLAN §4). The only
+invariant-legal way to reduce it is fauna consumption (foragers grazing trails —
+the Skim Race hypothesis, ECOSYSTEM.md §7.2B). Today fauna are client-local and
+divergent, so their grazing benefit — and population — differs per peer, and
+`ECOSYSTEM.md` §7.2 caveat 4 blocks using them in competitive play at all.
+
+Server-authoritative fauna make the population identical everywhere, which makes
+the grazing perf lever deployable in every mode. **Note the catch (decision point
+D1, §9):** if clients only *render* fauna, nothing consumes the client's local
+prisms — the perf win would land on the host only. The puppet grazing tick (§3.5)
+is what delivers the prism reduction on every peer.
+
+**Invariants restated (ecology protocol):** this plan adds replication transport
+only. Mass conservation, wither-to-crystal, no-imposed-death, controlling-color
+spawn, volume-as-spine, endogenous selection — all decision logic stays exactly
+where it is, computed once on the server instead of N divergent times. Continuity
+of existence gains a new obligation: **a replicated death must wither on every
+peer before the object despawns** (§3.6). Collider budget: **zero new colliders**
+(§4).
+
+---
+
+## 1. Current state (verified post-merge)
+
+| Layer | Networked? | Notes |
+|---|---|---|
+| Vessels / Players / AI | YES | NetworkObjects; AI = dynamic server-owned spawn precedent |
+| Crystals (game scenes) | YES | `NetworkCrystalManager` — NetworkList slots driving local objects |
+| Cell phase/domain | OPTIONAL | `CellNetworkSync` exists; **NOT on `Cell.prefab`** → inactive in Menu_Main |
+| Trail prisms | NO | Client-reconstructed from replicated vessel motion (near-aligned) |
+| Flora | NO | Local RNG placement + growth (fully divergent) |
+| **Fauna** | **NO** | Local seeder/reproduction/starvation per peer (fully divergent) |
+
+**Fauna code facts the design must respect (all landed in the recent merge):**
+
+- **Senses/consume ride the spatial index**, not physics: `PrismSpatialIndex.QuerySphere`
+  into shared `Fauna.PrismScratch`; one layer-masked physics overlap remains for
+  vessels only (`Fauna.NonPrismOverlapMask`). (`LightFauna.UpdateBehavior`,
+  `Boid.CalculateBehavior`)
+- **Movers contract:** fauna body `HealthPrism`s are registered, MOVING prism mass —
+  `Update()` must call `Fauna.NotifyBodyPrismsMoved()` every frame or index data
+  (AOE, senses) goes stale. (`Fauna.cs` §body prisms)
+- **Sealed death path:** `Fauna.Die()` is non-virtual — drops the elemental crystal
+  (`LifeFormCrystal`), then `OnDeath()`. Removal happens at the END of a wither:
+  `LightFauna.WitherCoroutine → RemoveHusk()`, `Boid.FadeOutAndRemove()`. The
+  dropped `Crystal` is a **local** object (`Crystal : CellItem : MonoBehaviour`).
+- **Volume is the spine:** prey gate = `Cell.OpposingVolume` (environment volume);
+  phase/dominant from `Cell.LiveVolume`; count is only the Frenzy perf backstop.
+- **Spawn sites (fauna):** exactly three —
+  `CellLifeSpawnerBase.SpawnFauna` / `SpawnFaunaWithDomain` and
+  `Fauna.SpawnOffspring` (reproduction). Seeder loop: `RandomLifeSpawner.
+  SpawnFaunaTypeLoop_Random` (menu + most scenes); `IntensityWiseLifeSpawner`
```

</details>

### `ed9f78a99` — docs(ecosystem): point caveat 4 (client-local fauna) at the sync plan

_Claude, 2026-07-16 20:26:31 +0000_

```text
 Docs/ECOSYSTEM.md | 2 ++
 1 file changed, 2 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/ECOSYSTEM.md b/Docs/ECOSYSTEM.md
index cd15863ea..518cf69a7 100644
--- a/Docs/ECOSYSTEM.md
+++ b/Docs/ECOSYSTEM.md
@@ -536,6 +536,8 @@ cleared.
 >    client-local (trails reconstructed from networked vessel movement; cell phase
 >    synced via `CellNetworkSync`). Fine for a per-client **perf** test and the
 >    menu; **diverges across clients**, so not yet fair for competitive play.
+>    **Sync plan (server-authoritative fauna, replicated puppets):**
+>    `Docs/ECOSYSTEM_NETWORK_SYNC.md`.
 > 5. **Net perf.** Fauna cost CPU (per-tick `OverlapSphere` per creature). Test
 >    whether trail savings beat fauna cost: start modest and profile before/after;
 >    scale `PopulationSize` only if net-positive.
```

</details>

### `811bcd50d` — feat(ecology): server-authoritative fauna sync — puppet grazing infrastructure + rollout tool

_Claude, 2026-07-16 21:10:09 +0000_

```text
Implements Docs/ECOSYSTEM_NETWORK_SYNC.md (decisions D1-B/D2/D3, prompter-confirmed):

- FaunaNetworkSync (new, the only environment file importing Unity.Netcode):
  IsSimAuthority rule, pre-spawn domain stamp + NetworkObject spawn seam
  (ServerSpawn), replicated wither-to-crystal death (NetLifeState — every peer
  runs the sealed Fauna.Die locally, server despawns after wither + grace),
  launch-time brood teardown, nearest-active-cell puppet init.
- Fauna: IsSimAuthority flag + puppet entry, goal-coroutine + reproduction
  gates, husk despawn routing (TryNetworkDespawn), offspring births replicate
  through the same seam.
- LightFauna/Boid: puppet graze tick (consume sweep only, smaller query radius,
  existing EatPrism revalidation + maxConsumesPerFrame pacing), movement gated
  to sim authority, movers contract (NotifyBodyPrismsMoved) kept on every peer.
- Spawners: seeder loops zero their spawn counts on non-authority peers while
  the wave tick + spawn-ring telemetry keep running; launch suppression closes
  the spawn-vs-scene-load batching race.
- Editor: Tools > Cosmic Shore > Fauna Sync — numbered per-species rollout steps
  (Cell phase sync, tadpole, brittlestar, shark) + validation; registers prefabs
  in DefaultNetworkPrefabs via serialized properties.
- Tests: authority truth table (EditMode).

No prefab is networked until its rollout step runs in-editor — shipped behavior
is unchanged until then. Zero new colliders; puppet ticks cost less than the
client-local sim they replace.
```

```text
 Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs         |  38 ++++
 Assets/_Scripts/Controller/Environment/FloraAndFauna/Boid.cs          |  86 ++++++++-
 Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs         |  63 ++++++-
 .../_Scripts/Controller/Environment/FloraAndFauna/FaunaNetworkSync.cs | 263 ++++++++++++++++++++++++++++
 .../Controller/Environment/FloraAndFauna/FaunaNetworkSync.cs.meta     |  11 ++
 Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs    |  88 +++++++++-
 Assets/_Scripts/Controller/Environment/IntensityWiseLifeSpawner.cs    |   6 +
 Assets/_Scripts/Controller/Environment/RandomLifeSpawner.cs           |   8 +
 Assets/_Scripts/Editor/FaunaNetworkSetupTool.cs                       | 299 ++++++++++++++++++++++++++++++++
 Assets/_Scripts/Editor/FaunaNetworkSetupTool.cs.meta                  |  11 ++
 Assets/_Scripts/Tests/EditMode/FaunaNetworkAuthorityTests.cs          |  49 ++++++
 Assets/_Scripts/Tests/EditMode/FaunaNetworkAuthorityTests.cs.meta     |  11 ++
 Docs/ECOSYSTEM_NETWORK_SYNC.md                                        |  62 +++++--
 13 files changed, 963 insertions(+), 32 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1185 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs b/Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs
index 3b2fe5a3a..f315ea7d7 100644
--- a/Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs
+++ b/Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs
@@ -12,6 +12,19 @@ namespace CosmicShore.Gameplay
     {
         readonly List<Coroutine> _running = new();
 
+        // Launch teardown (Docs/ECOSYSTEM_NETWORK_SYNC.md §3.8): the server despawns its
+        // networked brood and suppresses further fauna spawns the moment a game launch is
+        // requested, so a fauna spawn message can never batch into the same network tick
+        // as the scene-load message (the client-side "[Invalid Destroy]" race - the same
+        // lesson as the AI-spawn destroyWithScene fix in CLAUDE.md).
+        Cell _activeHost;
+        GameDataSO _activeGameData;
+        bool _suppressSpawns;
+
+        /// <summary>True from a game-launch request until the spawner restarts in the
+        /// next scene - fauna spawn loops must not originate new creatures.</summary>
+        protected bool SpawnsSuppressed => _suppressSpawns;
+
         public void Start(Cell host, CellConfigDataSO config, CellRuntimeDataSO runtime, GameDataSO gameData)
         {
             Stop(host);
@@ -19,11 +32,23 @@ namespace CosmicShore.Gameplay
             if (!Validate(host, config, runtime, gameData))
                 return;
 
+            _activeHost = host;
+            _activeGameData = gameData;
+            _suppressSpawns = false;
+            gameData.OnLaunchGame.OnRaised += HandleGameLaunching;
+
             OnStart(host, config, runtime, gameData);
         }
 
         public void Stop(Cell host)
         {
+            if (_activeGameData != null)
+            {
+                _activeGameData.OnLaunchGame.OnRaised -= HandleGameLaunching;
+                _activeGameData = null;
+            }
+            _activeHost = null;
+
             if (!host) return;
 
             for (int i = 0; i < _running.Count; i++)
@@ -36,6 +61,12 @@ namespace CosmicShore.Gameplay
             OnStop(host);
         }
 
+        void HandleGameLaunching()
+        {
+            _suppressSpawns = true;
+            FaunaNetworkSync.ServerDespawnBrood(_activeHost);
+        }
+
         protected abstract void OnStart(Cell host, CellConfigDataSO config, CellRuntimeDataSO runtime, GameDataSO gameData);
         protected virtual void OnStop(Cell host) { }
 
@@ -151,6 +182,9 @@ namespace CosmicShore.Gameplay
             pop.Initialize(host);
 
             RegisterSpawned(host, pop.gameObject);
+            // Replicate to clients when we are the server and the species is networked
+            // (no-op otherwise - the per-species rollout gate).
+            FaunaNetworkSync.ServerSpawn(pop);
             return pop;
         }
 
@@ -173,6 +207,10 @@ namespace CosmicShore.Gameplay
             pop.Initialize(host);
 
             RegisterSpawned(host, pop.gameObject);
+            // Replicate to clients when we are the server and the species is networked
+            // (no-op otherwise). Covers every caller of the canonical spawn sequence -
+            // spawner loops and the microscene conveyor alike.
+            FaunaNetworkSync.ServerSpawn(pop);
             return pop;
```

</details>

### `9e63dd922` — Update MassBrittlestarFauna.prefab

_Yash Sadhukhan, 2026-07-17 03:31:31 +0530_

```text
 Assets/_Models/Fauna/MassBrittlestarFauna.prefab | 90 ++++++++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 90 insertions(+)
```

### `cc89797e4` — Update MassSharkFauna.prefab

_Yash Sadhukhan, 2026-07-17 03:31:34 +0530_

```text
 Assets/_Models/Fauna/MassSharkFauna.prefab | 90 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 90 insertions(+)
```

### `e86c40cb3` — Update Cell.prefab

_Yash Sadhukhan, 2026-07-17 03:31:37 +0530_

```text
 Assets/_Prefabs/Environment/Cell.prefab | 43 ++++++++++++++++++++++++++++++++++++++++++-
 1 file changed, 42 insertions(+), 1 deletion(-)
```

### `f7da41e6e` — Update MassTadPoleFauna.prefab

_Yash Sadhukhan, 2026-07-17 03:31:40 +0530_

```text
 Assets/_Prefabs/FloraAndFauna/MassTadPoleFauna.prefab | 90 +++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 90 insertions(+)
```

### `444fb8424` — Update SpaceTadPoleFauna.prefab

_Yash Sadhukhan, 2026-07-17 03:31:44 +0530_

```text
 Assets/_Prefabs/FloraAndFauna/SpaceTadPoleFauna.prefab | 90 ++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 90 insertions(+)
```

### `988fa217e` — Update TimeTadPoleFauna.prefab

_Yash Sadhukhan, 2026-07-17 03:31:47 +0530_

```text
 Assets/_Prefabs/FloraAndFauna/TimeTadPoleFauna.prefab | 90 +++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 90 insertions(+)
```

### `fc88db9ff` — Update DefaultNetworkPrefabs.asset

_Yash Sadhukhan, 2026-07-17 03:45:45 +0530_

```text
 Assets/DefaultNetworkPrefabs.asset | 42 ++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 42 insertions(+)
```

### `6e9d725c9` — Update MassBrittlestarFauna.prefab

_Yash Sadhukhan, 2026-07-17 03:50:59 +0530_

```text
 Assets/_Models/Fauna/MassBrittlestarFauna.prefab | 6 +++---
 1 file changed, 3 insertions(+), 3 deletions(-)
```

### `19cb42a0b` — Update MassSharkFauna.prefab

_Yash Sadhukhan, 2026-07-17 03:51:08 +0530_

```text
 Assets/_Models/Fauna/MassSharkFauna.prefab | 4 ++--
 1 file changed, 2 insertions(+), 2 deletions(-)
```

### `9fdc73d87` — feat(ecology): flora sync Option B — replicated plant decisions + growth mirror, no world reset

_Claude, 2026-07-16 23:31:32 +0000_

```text
Implements the prompter-confirmed Option B (Docs/ECOSYSTEM_NETWORK_SYNC.md §6):
the host's world is never destroyed on a party join — clients reconstruct it.

- FloraNetworkSync (new, on the Cell alongside CellNetworkSync): every planted
  profile flora becomes a NetworkList slot {species, root pose, domain, growth
  ticks, state}; late joiners rebuild the standing population from the initial
  list sync; a 2s server mirror tops up GrowthTicks and flips slots to Withered
  on death. Withered slots are reused so the late-join payload stays bounded.
- Clients plant the same species at the same pose (Flora.UseAuthoredPlacement
  skips local random dispersal) and fast-forward growth one Grow() per frame
  (paced bloom-in — continuity law + the initial-batch frame-spike lesson);
  replicated deaths run the same LifeForm wither-to-crystal path locally.
- LifeForm: IsDying + ApplyReplicatedDeath (idempotent, same sealed path).
- Flora: GrowthTicks counter + FastForwardGrowth; AssembledFlora/BranchingFlora
  Plant() honors authored placement.
- Spawners: flora loops authority-gated (planting decisions server-only) +
  launch-suppression parity with fauna.
- Editor: Fauna Sync step 0 now also wires FloraNetworkSync; validation updated.

Fidelity contract: same species/place/domain/approximate size per peer — shape
stays locally emergent (growth consults the local spatial index, which includes
client-local trails; a shared seed cannot make it deterministic). Zero new
colliders; client flora are the same local prisms they grew before, now at
host-matching positions — which is what makes puppet fauna grazing converge.
```

```text
 Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs         |   4 +
 .../_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs   |   3 +
 .../_Scripts/Controller/Environment/FloraAndFauna/BranchingFlora.cs   |   3 +
 Assets/_Scripts/Controller/Environment/FloraAndFauna/Flora.cs         |  41 ++++
 Assets/_Scripts/Controller/Environment/FloraAndFauna/LifeForm.cs      |  18 ++
 Assets/_Scripts/Controller/Environment/FloraNetworkSync.cs            | 321 ++++++++++++++++++++++++++++++++
 Assets/_Scripts/Controller/Environment/FloraNetworkSync.cs.meta       |  11 ++
 Assets/_Scripts/Controller/Environment/IntensityWiseLifeSpawner.cs    |   5 +
 Assets/_Scripts/Controller/Environment/RandomLifeSpawner.cs           |   7 +
 Assets/_Scripts/Editor/FaunaNetworkSetupTool.cs                       |  25 ++-
 Docs/ECOSYSTEM_NETWORK_SYNC.md                                        |  19 +-
 11 files changed, 451 insertions(+), 6 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 580 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs b/Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs
index f315ea7d7..074311dd3 100644
--- a/Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs
+++ b/Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs
@@ -165,6 +165,10 @@ namespace CosmicShore.Gameplay
             flora.Initialize(host);
 
             RegisterSpawned(host, flora.gameObject);
+            // Replicate the plant DECISION (species/pose/domain) to clients when we are
+            // the server and the cell carries a FloraNetworkSync (no-op otherwise;
+            // Initialize already ran Plant(), so the final root pose is captured).
+            FloraNetworkSync.ServerOnPlanted(host, floraPrefab, flora);
             return flora;
         }
 
diff --git a/Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs b/Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs
index a42ad1425..4921ef5b6 100644
--- a/Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs
+++ b/Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs
@@ -341,6 +341,9 @@ namespace CosmicShore.Gameplay
         public override void Plant()
         {
             assembler = CreateNewAssembler();
+            // Client copies of a server-replicated plant keep the replicated root pose -
+            // the random dispersal below is the SERVER's placement decision.
+            if (UseAuthoredPlacement) return;
             // Disperse across the cell (fraction of membrane radius - see Flora base)
             // instead of the old hard-coded 200m huddle around the crystal. Dispersed,
             // domain-coherent flora clusters are what give fauna schools of different
diff --git a/Assets/_Scripts/Controller/Environment/FloraAndFauna/BranchingFlora.cs b/Assets/_Scripts/Controller/Environment/FloraAndFauna/BranchingFlora.cs
index 4884fb083..6b18bfef8 100644
--- a/Assets/_Scripts/Controller/Environment/FloraAndFauna/BranchingFlora.cs
+++ b/Assets/_Scripts/Controller/Environment/FloraAndFauna/BranchingFlora.cs
@@ -195,6 +195,9 @@ namespace CosmicShore.Gameplay
 
         public override void Plant()
         {
+            // Client copies of a server-replicated plant keep the replicated root pose -
+            // the random dispersal below is the SERVER's placement decision.
+            if (UseAuthoredPlacement) return;
             if (plantAroundCrystal)
             {
                 // Disperse across the cell (fraction of membrane radius - see Flora base)
diff --git a/Assets/_Scripts/Controller/Environment/FloraAndFauna/Flora.cs b/Assets/_Scripts/Controller/Environment/FloraAndFauna/Flora.cs
index 9a96eeb32..2e47abd71 100644
--- a/Assets/_Scripts/Controller/Environment/FloraAndFauna/Flora.cs
+++ b/Assets/_Scripts/Controller/Environment/FloraAndFauna/Flora.cs
@@ -26,6 +26,46 @@ namespace CosmicShore.Gameplay
 
         protected bool isGrowing = true;
 
+        /// <summary>
+        /// True when this flora's root pose was authored externally (a client
+        /// reconstructing a server-replicated plant event) - Plant() implementations
+        /// must then SKIP their own random positioning so the structure roots at the
+        /// replicated position on every peer. (Docs/ECOSYSTEM_NETWORK_SYNC.md, flora.)
+        /// </summary>
+        public bool UseAuthoredPlacement { get; set; }
+
+        /// <summary>
+        /// Number of Grow() cycles this flora has run (natural cadence + fast-forward).
+        /// Mirrored by FloraNetworkSync so late-joining clients can catch a plant up to
+        /// the server's SIZE. Shape stays locally emergent - growth consults the local
+        /// spatial index, so structures are same-species/same-place/same-size across
+        /// peers, not byte-identical.
+        /// </summary>
+        public int GrowthTicks { get; private set; }
+
+        /// <summary>
+        /// Runs <paramref name="ticks"/> extra Grow() cycles paced one per frame - a
+        /// visible bloom-in (continuity law: nothing pops in), which also spreads the
+        /// instantiation cost (the initial-batch frame-spike lesson). Grow()'s own
+        /// gates (live-prism budget, Frenzy) keep applying throughout.
+        /// </summary>
+        public void FastForwardGrowth(int ticks)
+        {
+            if (ticks <= 0 || !isActiveAndEnabled) return;
+            StartCoroutine(FastForwardGrowthCoroutine(ticks));
+        }
+
```

</details>

### `f6467fbdc` — Update Cell.prefab

_Yash Sadhukhan, 2026-07-17 06:26:04 +0530_

```text
 Assets/_Prefabs/Environment/Cell.prefab | 17 +++++++++++++++++
 1 file changed, 17 insertions(+)
```

_Also contains 1 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
