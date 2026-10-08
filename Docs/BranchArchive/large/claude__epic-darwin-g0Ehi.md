# Branch archive: `claude/epic-darwin-g0Ehi`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

## What this branch is

**Ecology volume, wither and crystal drops**

A big ecology pass: creatures spawn based on which team controls the area, starvation makes creatures wither into collectible elemental crystals that buff the collector, all prisms count toward per-team volume, fauna spawn in the Skim Race cell, plus docs locking 'nothing pops in or out' and mass conservation. It also wrote the ecology master plan and the /ecology skill.

- **Status:** Partly landed
- **Areas:** ecology, fauna, flora, elemental crystals, Skim Race, docs
- **Already in bleeding-edge:** Much of this direction is in bleeding-edge: Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md (LOCKED), the /ecology skill, volume-based Cell.LiveVolume, LifeFormCrystal/ElementalCrystal powerups, and commits like 7cd11fb1f 'conserved stomach' ecology LOD. Specific commits (extremity-first wither, HexRace fauna) were not found by subject.
- **Risk if deleted:** medium
- **Suggestion (2026-10-08):** can be deleted after archiving — Its core ideas (volume, crystal drops, locked invariants, /ecology skill) landed in bleeding-edge and the ecology has since moved well past this branch.

## Evidence

- **Last commit:** 2026-06-11 by Claude
- **Unmerged commits:** 22
- **Forked from:** `9ae47781e` (2026-06-11, Merge branch 'claude/charming-galileo-unjjqm' into bleeding-edge)
- **Tip:** `c22f66e3a`
- **Files touched (37):**
  - `.claude/skills/ecology/SKILL.md`
  - `Assets/Resources/ElementalCrystalSet.asset`
  - `Assets/Resources/ElementalCrystalSet.asset.meta`
  - `Assets/_SO_Assets/Cell Configs/Barren Cell/Barren Cell Spawn Profile.asset`
  - `Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Cell Config.asset`
  - `Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Cell Spawn Profile.asset`
  - `Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Fauna Config Data.asset`
  - `Assets/_Scripts/Controller/Environment/Cell.cs`
  - `Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs`
  - `Assets/_Scripts/Controller/Environment/CellNetworkSync.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/BodySegmentFauna.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/Boid.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/Flora.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/LifeForm.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/LifeFormCrystal.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/LifeFormCrystal.cs.meta`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFaunaDataSO.cs`
  - `Assets/_Scripts/Controller/Environment/IntensityWiseLifeSpawner.cs`
  - `Assets/_Scripts/Controller/Environment/IntensityWiseLifeSpawner.cs.meta`
  - `Assets/_Scripts/Controller/Environment/RandomLifeSpawner.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/ImpactCollider.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/Impactors/ElementalCrystalImpactor.cs`
  - `Assets/_Scripts/Controller/Multiplayer/MenuServerPlayerVesselInitializer.cs`
  - `Assets/_Scripts/Editor/LifeFormCrystalValidator.cs`
  - `Assets/_Scripts/Editor/LifeFormCrystalValidator.cs.meta`
  - `Assets/_Scripts/ScriptableObjects/ElementalCrystalSetSO.cs`
  - `Assets/_Scripts/ScriptableObjects/ElementalCrystalSetSO.cs.meta`
  - `Assets/_Scripts/UI/DomainVolumeHexGraphic.cs.meta`
  - `Assets/_Scripts/UI/DomainVolumeIndicator.cs`
  - `Assets/_Scripts/UI/DomainVolumeIndicator.cs.meta`
  - `Assets/_Scripts/Utility/DataContainers/CellPhaseThresholds.cs`
  - `Assets/_Scripts/Utility/DataContainers/SpawnProfileSO.cs`
  - `CLAUDE.md`
  - `Docs/ECOSYSTEM.md`
  - `Docs/ECOSYSTEM_MASTERPLAN.md`

### `6332e6e1a` — feat(ecology): cross-domain prey-weighted fauna spawn replaces the regrowth pulse

_Claude, 2026-06-04 23:08:17 +0000_

```text
Phase 2 step 1. Per prompter direction (no imposed prism lifespan; regulate cells
via consumption + starvation; all prisms consumable by domain), the dominant
domain's mass finally gets a down-force through consumption instead of decay or a
hard-coded pulse.

- Fauna now spawn across all playable domains weighted by available prey
  (CellLifeSpawnerBase.TryPickPreyWeightedDomain) instead of only the controlling
  color. A dominated cell is the biggest prey pool for the other colors, so their
  fauna spawn preferentially, graze the dominant canopy down, then starve as it
  thins — the food web regulates the cell, not a timer.
- Remove the flora regrowth pulse: Cell.FloraGrowingEnabled is now just
  `phase < Frozen` (growth stops on overpopulation, resumes only when consumption
  drops the count below Frozen-exit). Delete Cell.InFloraRegrowthPulse and the
  SpawnProfileSO.FloraRegrowthPulse* fields.
- Docs/ECOSYSTEM.md §5 / §5.1 / §10 updated to record the pivot.
```

```text
 Assets/_Scripts/Controller/Environment/Cell.cs                | 39 +++++------------------
 Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs | 37 ++++++++++++++++++++++
 Assets/_Scripts/Controller/Environment/RandomLifeSpawner.cs   | 28 ++++++++++-------
 Assets/_Scripts/Utility/DataContainers/SpawnProfileSO.cs      |  6 ----
 Docs/ECOSYSTEM.md                                             | 70 +++++++++++++++++++++++++++--------------
 5 files changed, 107 insertions(+), 73 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 267 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/Cell.cs b/Assets/_Scripts/Controller/Environment/Cell.cs
index ec4537150..6e4d16f55 100644
--- a/Assets/_Scripts/Controller/Environment/Cell.cs
+++ b/Assets/_Scripts/Controller/Environment/Cell.cs
@@ -239,39 +239,14 @@ namespace CosmicShore.Gameplay
         public bool FloraPlantingEnabled => phase < CellPhase.Settled;
 
         /// <summary>
-        /// True while existing flora may grow new prisms. Below Frozen flora grow
-        /// freely; between Frozen and Rabid they grow only during the periodic regrowth
-        /// pulse (so the canopy keeps "breathing" instead of freezing solid once the
-        /// cell fills); at Rabid growth stops hard (the density ceiling).
-        /// See Docs/ECOSYSTEM.md.
+        /// True while existing flora may grow new prisms: below Frozen. Growth stops once
+        /// the cell fills past Frozen (overpopulation) and resumes only when CONSUMPTION
+        /// brings the live count back below the Frozen-exit threshold — the cell breathes
+        /// through the food web (other-domain fauna grazing the dominant canopy, then
+        /// starving as it thins), not a hard-coded regrowth pulse or an imposed prism
+        /// lifespan. See Docs/ECOSYSTEM.md.
         /// </summary>
-        public bool FloraGrowingEnabled =>
-            phase < CellPhase.Frozen ||
-            (phase < CellPhase.Rabid && InFloraRegrowthPulse);
-
-        // Sensible fallbacks when a SpawnProfile predates the regrowth-pulse fields
-        // (serialized 0) so the pulse is on across the board by default.
-        const float FloraRegrowthPulsePeriodDefault = 15f;
-        const float FloraRegrowthPulseDurationDefault = 4f;
-
-        /// <summary>
-        /// True during the cell's periodic flora-regrowth window. Cell-global (all flora
-        /// pulse together) so the canopy visibly breathes. Duration &gt;= period ⇒ always on.
-        /// </summary>
-        bool InFloraRegrowthPulse
-        {
-            get
-            {
-                var profile = cellConfigData ? cellConfigData.SpawnProfile : null;
-                float period = profile && profile.FloraRegrowthPulsePeriod > 0f
-                    ? profile.FloraRegrowthPulsePeriod : FloraRegrowthPulsePeriodDefault;
-                float duration = profile && profile.FloraRegrowthPulseDuration > 0f
-                    ? profile.FloraRegrowthPulseDuration : FloraRegrowthPulseDurationDefault;
-
-                if (duration >= period) return true;
-                return (Time.time % period) < duration;
-            }
-        }
+        public bool FloraGrowingEnabled => phase < CellPhase.Frozen;
 
         /// <summary>True once the cell has crossed the fauna-spawn threshold (Phase &gt;= Quiet).</summary>
         public bool FaunaSpawningEnabled => phase >= CellPhase.Quiet;
diff --git a/Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs b/Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs
index f192abd5c..a08ff2d18 100644
--- a/Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs
+++ b/Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs
@@ -86,6 +86,43 @@ namespace CosmicShore.Gameplay
                 : candidates[UnityEngine.Random.Range(0, candidates.Count)];
         }
 
+        // Playable domains only — never Blue (the "no team" sentinel). Fauna spawn across
+        // ALL of these (weighted by available prey, see TryPickPreyWeightedDomain), not just
+        // the cell's controlling color, so every domain's mass — including the DOMINANT
+        // canopy — has opposing consumers. Consumption + starvation then regulate the cell.
+        static readonly Domains[] PlayableDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };
+
+        /// <summary>
+        /// Picks the domain for the next fauna population, weighted by available prey:
+        /// P(domain) ∝ <see cref="Cell.OpposingBlockCount"/>(domain), among the playable
+        /// domains whose prey is at least <paramref name="foodFloor"/>. Returns false when
+        /// no domain has enough prey — the caller skips the burst rather than spawn fauna
+        /// that would immediately starve.
+        ///
+        /// This is the consumption-driven down-force on the DOMINANT domain. A cell
+        /// dominated by one color is the largest prey pool for the OTHER colors, so their
+        /// fauna are the likeliest to spawn, graze that canopy back down, then starve as it
+        /// thins — the food web (consumption + starvation), not an imposed prism lifespan,
+        /// makes the cell breathe. Replaces the controlling-color-only spawn, which left the
+        /// dominant mass with no predator and forced the regrowth-pulse stopgap.
+        /// (Docs/ECOSYSTEM.md §5–6.)
+        /// </summary>
```

</details>

### `7ec09bb97` — feat(ecology): controlling-color spawn (no asymmetry); starvation withers fauna into a crystal

_Claude, 2026-06-05 01:32:55 +0000_

```text
Per prompter direction: no asymmetry between domains — fauna spawn only in the
controlling color (revert the cross-domain prey-weighted experiment); and a starving
creature does not vanish — it withers from its extremity spindles inward and leaves
its core crystal behind.

- Revert RandomLifeSpawner to host.ControllingDomain spawning; remove
  CellLifeSpawnerBase.TryPickPreyWeightedDomain.
- LightFauna: on starvation, BeginWither() runs WitherToCrystalCoroutine — collapses
  body spindles farthest-from-center first (Spindle.ForceWither, paced by
  LightFaunaDataSO.witherRingInterval) and activates the body's core Crystal
  (Crystal.ActivateCrystal) as the remnant, recycling the creature's mass into a
  collectible / flora-nucleating crystal. Movement + hunting freeze while withering.
- LightFaunaDataSO: add witherRingInterval (<= 0 falls back to 0.25s).
- Pulse stays removed. Docs/ECOSYSTEM.md updated; the dominant-domain down-force is
  flagged as an open item (controlling-color fauna cannot cull the dominant mass).
```

```text
 Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs         | 37 --------------
 Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs    | 83 +++++++++++++++++++++++++++++-
 .../_Scripts/Controller/Environment/FloraAndFauna/LightFaunaDataSO.cs |  7 +++
 Assets/_Scripts/Controller/Environment/RandomLifeSpawner.cs           | 32 ++++++------
 Docs/ECOSYSTEM.md                                                     | 90 +++++++++++++++++----------------
 5 files changed, 150 insertions(+), 99 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 362 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs b/Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs
index a08ff2d18..f192abd5c 100644
--- a/Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs
+++ b/Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs
@@ -86,43 +86,6 @@ namespace CosmicShore.Gameplay
                 : candidates[UnityEngine.Random.Range(0, candidates.Count)];
         }
 
-        // Playable domains only — never Blue (the "no team" sentinel). Fauna spawn across
-        // ALL of these (weighted by available prey, see TryPickPreyWeightedDomain), not just
-        // the cell's controlling color, so every domain's mass — including the DOMINANT
-        // canopy — has opposing consumers. Consumption + starvation then regulate the cell.
-        static readonly Domains[] PlayableDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };
-
-        /// <summary>
-        /// Picks the domain for the next fauna population, weighted by available prey:
-        /// P(domain) ∝ <see cref="Cell.OpposingBlockCount"/>(domain), among the playable
-        /// domains whose prey is at least <paramref name="foodFloor"/>. Returns false when
-        /// no domain has enough prey — the caller skips the burst rather than spawn fauna
-        /// that would immediately starve.
-        ///
-        /// This is the consumption-driven down-force on the DOMINANT domain. A cell
-        /// dominated by one color is the largest prey pool for the OTHER colors, so their
-        /// fauna are the likeliest to spawn, graze that canopy back down, then starve as it
-        /// thins — the food web (consumption + starvation), not an imposed prism lifespan,
-        /// makes the cell breathe. Replaces the controlling-color-only spawn, which left the
-        /// dominant mass with no predator and forced the regrowth-pulse stopgap.
-        /// (Docs/ECOSYSTEM.md §5–6.)
-        /// </summary>
-        protected bool TryPickPreyWeightedDomain(Cell host, int foodFloor, out Domains domain)
-        {
-            domain = Domains.Jade;
-            if (!host) return false;
-
-            var eligible = new List<Domains>(PlayableDomains.Length);
-            for (int i = 0; i < PlayableDomains.Length; i++)
-                if (host.OpposingBlockCount(PlayableDomains[i]) >= foodFloor)
-                    eligible.Add(PlayableDomains[i]);
-
-            if (eligible.Count == 0) return false;
-
-            domain = PickWeighted(eligible, d => host.OpposingBlockCount(d));
-            return true;
-        }
-
         protected T PickWeighted<T>(IReadOnlyList<T> items, Func<T, float> weightSelector)
         {
             if (items == null || items.Count == 0) return default;
diff --git a/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs b/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs
index 9cae28e66..a54599145 100644
--- a/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs
+++ b/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs
@@ -22,6 +22,10 @@ namespace CosmicShore.Gameplay
         private Vector3 desiredDirection;
         private Quaternion desiredRotation;
 
+        // True once starvation has begun the wither-to-crystal death animation. Freezes
+        // behavior + movement so the creature collapses in place instead of drifting.
+        bool _withering;
+
         [HideInInspector] public float Phase;
 
         public LightFaunaManager LightFaunaManager { get; set; }
@@ -76,6 +80,70 @@ namespace CosmicShore.Gameplay
                 Destroy(gameObject);
         }
 
+        /// <summary>
+        /// Starts the starvation death: the creature withers from its extremity spindles
+        /// inward to the center and leaves its core crystal behind. Idempotent — only the
+        /// first call takes effect.
+        /// </summary>
+        void BeginWither()
+        {
+            if (_withering) return;
+            _withering = true;
+            currentVelocity = Vector3.zero;
+            StartCoroutine(WitherToCrystalCoroutine());
+        }
+
```

</details>

### `aba309884` — feat(ecology): all prisms feed per-domain volume; all three domains seed flora

_Claude, 2026-06-05 14:52:39 +0000_

```text
Per prompter: every prism (flora HealthPrism or vessel trail prism) contributes to
both the count AND the volume of its domain; all three playable domains spawn flora
(no asymmetry). The dominant canopy persisting is intended — territorial permanence
(take a cell, leave it, it stays your domain's) — so no down-force is applied to the
dominant flora; the dynamic oscillations live in the minority/fauna churn underneath.

- Cell.AddBlock/RemoveBlock now feed teamVolumes (per-domain volume) alongside
  domainBlockCounts, snapshotting each prism's target-scale mass (RegisteredBlock).
  Cell.ChangeVolume had zero callers before — per-domain volume was dead.
- RandomLifeSpawner seeds flora across all playable domains (excluded = null);
  FloraExcludeLocalDomain is now ignored by the live spawner (only the dead
  IntensityWiseLifeSpawner still reads it — delete both together later).
- Docs/ECOSYSTEM.md §1/§5/§10 updated: count+volume model, all-three flora, and the
  dominant-persistence-is-intended resolution.
```

```text
 Assets/_Scripts/Controller/Environment/Cell.cs              | 49 +++++++++++++++++++++++++++++++++++--------
 Assets/_Scripts/Controller/Environment/RandomLifeSpawner.cs |  6 +++++-
 Docs/ECOSYSTEM.md                                           | 42 ++++++++++++++++++++++++++++---------
 3 files changed, 77 insertions(+), 20 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 176 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/Cell.cs b/Assets/_Scripts/Controller/Environment/Cell.cs
index 6e4d16f55..fbf0ea6ee 100644
--- a/Assets/_Scripts/Controller/Environment/Cell.cs
+++ b/Assets/_Scripts/Controller/Environment/Cell.cs
@@ -58,11 +58,20 @@ namespace CosmicShore.Gameplay
         readonly Dictionary<Domains, int> domainBlockCounts = new();
 
         readonly List<GameObject> spawnedLifeForms = new();
-        // Prism → the domain it was REGISTERED under. RemoveBlock decrements the
-        // grids/counts for the registration-time domain, not the prism's current
-        // one — so steals / ChangeTeam between Add and Remove can't desync the
-        // per-domain bookkeeping (the §2.3.1 phantom-count class of bug).
-        readonly Dictionary<Prism, Domains> trackedBlocks = new();
+        // Prism → the (domain, volume) it was REGISTERED under. RemoveBlock decrements the
+        // grids/counts/teamVolumes for the registration-time snapshot, not the prism's
+        // current state — so steals / ChangeTeam or growth between Add and Remove can't
+        // desync the per-domain bookkeeping (the §2.3.1 phantom-count class of bug).
+        readonly Dictionary<Prism, RegisteredBlock> trackedBlocks = new();
+
+        // Snapshot of a prism's domain + mass at registration time. Mass (volume) is the
+        // prism's target-scale volume; both count and volume feed the owning domain.
+        readonly struct RegisteredBlock
+        {
+            public readonly Domains Domain;
+            public readonly float Volume;
+            public RegisteredBlock(Domains domain, float volume) { Domain = domain; Volume = volume; }
+        }
         SnowChanger spawnedCytoplasm;
 
         // ---------------------------------------------------------------------
@@ -654,10 +663,12 @@ namespace CosmicShore.Gameplay
             if (block is null) return;
             if (trackedBlocks.ContainsKey(block)) return; // already counted
 
-            // Snapshot the domain at registration time — RemoveBlock uses this snapshot
-            // so a team change (steal) between Add and Remove can't desync the grids.
+            // Snapshot the domain + volume at registration time — RemoveBlock uses this
+            // snapshot so a team change (steal) or growth between Add and Remove can't
+            // desync the grids/counts/volume.
             Domains registeredDomain = block ? block.Domain : Domains.Blue;
-            trackedBlocks[block] = registeredDomain;
+            float registeredVolume = block ? PrismVolume(block) : 0f;
+            trackedBlocks[block] = new RegisteredBlock(registeredDomain, registeredVolume);
 
             if (block)
             {
@@ -670,14 +681,20 @@ namespace CosmicShore.Gameplay
 
                 domainBlockCounts.TryGetValue(registeredDomain, out int count);
                 domainBlockCounts[registeredDomain] = count + 1;
+
+                // Every prism — flora HealthPrism or vessel trail prism — also feeds its
+                // domain's VOLUME tally, so per-domain volume reflects ALL mass (not just
+                // the legacy scored-team total, which Cell.ChangeVolume otherwise never fed).
+                ChangeVolume(registeredDomain, registeredVolume);
             }
         }
 
         public void RemoveBlock(Prism block)
         {
             if (block is null) return;
-            if (!trackedBlocks.Remove(block, out Domains registeredDomain)) return; // not counted
+            if (!trackedBlocks.Remove(block, out RegisteredBlock tracked)) return; // not counted
 
+            Domains registeredDomain = tracked.Domain;
             if (block)
             {
                 Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold };
@@ -689,9 +706,23 @@ namespace CosmicShore.Gameplay
 
                 if (domainBlockCounts.TryGetValue(registeredDomain, out int count) && count > 0)
                     domainBlockCounts[registeredDomain] = count - 1;
+
+                // Subtract exactly the volume Add registered (the snapshot), keeping the
+                // per-domain volume tally symmetric with count even if the prism grew or
+                // changed team while it was alive.
+                ChangeVolume(registeredDomain, -tracked.Volume);
             }
         }
 
```

</details>

### `ae8cff80f` — refactor(ecology): delete the dead IntensityWiseLifeSpawner + vestigial exclude flags

_Claude, 2026-06-05 15:44:10 +0000_

```text
Intensity selects the CellConfig (CellTypeChoiceOptions.IntensityWise in AssignConfig),
and each config carries its own tuned SpawnProfile — so a separate intensity-wise
spawner is redundant. One spawner model (RandomLifeSpawner) now drives every cell.

- Delete IntensityWiseLifeSpawner.cs; Cell.StartSpawnerForMode always uses
  RandomLifeSpawner (CellTypeChoiceOptions still selects the config by intensity).
- Remove now-unused CellLifeSpawnerBase.GetExcludedDomain/GetLocalDomainOr and the
  dead SpawnProfileSO.FloraExcludeLocalDomain/FaunaExcludeLocalDomain flags.
- Refresh stale comments (Cell telemetry, CellNetworkSync).
```

```text
 Assets/_Scripts/Controller/Environment/Cell.cs                        |  18 +--
 Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs         |   9 --
 Assets/_Scripts/Controller/Environment/CellNetworkSync.cs             |   6 +-
 Assets/_Scripts/Controller/Environment/IntensityWiseLifeSpawner.cs    | 196 --------------------------------
 .../_Scripts/Controller/Environment/IntensityWiseLifeSpawner.cs.meta  |   3 -
 Assets/_Scripts/Utility/DataContainers/SpawnProfileSO.cs              |   9 +-
 6 files changed, 15 insertions(+), 226 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 320 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/Cell.cs b/Assets/_Scripts/Controller/Environment/Cell.cs
index fbf0ea6ee..127af3592 100644
--- a/Assets/_Scripts/Controller/Environment/Cell.cs
+++ b/Assets/_Scripts/Controller/Environment/Cell.cs
@@ -182,10 +182,9 @@ namespace CosmicShore.Gameplay
 
         // ---------------------------------------------------------------------
         //  Fauna spawn cycle telemetry — read by the volume-indicator ring HUD.
-        //  Written by IntensityWiseLifeSpawner.SpawnFaunaTypeLoop when it ticks a
-        //  periodic fauna spawn. The Cell exposes a 0..1 progress fraction toward
-        //  the next spawn so the indicator can draw a rotating ring without
-        //  knowing anything about the spawner's internals.
+        //  Written by RandomLifeSpawner's fauna loop (RecordFaunaSpawn) each periodic
+        //  spawn. The Cell exposes a 0..1 progress fraction toward the next spawn so the
+        //  indicator can draw a rotating ring without knowing the spawner's internals.
         // ---------------------------------------------------------------------
 
         float _lastFaunaSpawnTime = -1f;
@@ -193,7 +192,7 @@ namespace CosmicShore.Gameplay
         /// <summary>
         /// Records that a periodic fauna spawn just happened. The spawn-cycle ring
         /// resets to 0% and counts back up to 100% over the next CurrentFaunaSpawnPeriod
-        /// seconds. Called by IntensityWiseLifeSpawner's fauna loop.
+        /// seconds. Called by RandomLifeSpawner's fauna loop.
         /// </summary>
         public void RecordFaunaSpawn() => _lastFaunaSpawnTime = Time.time;
 
@@ -357,7 +356,6 @@ namespace CosmicShore.Gameplay
             return t.IsAllZero ? CellPhaseThresholds.Default : t;
         }
 
-        readonly ICellLifeSpawner intensitySpawner = new IntensityWiseLifeSpawner();
         readonly ICellLifeSpawner randomSpawner = new RandomLifeSpawner();
         ICellLifeSpawner activeSpawner;
         bool postInitilized = false;
@@ -629,9 +627,11 @@ namespace CosmicShore.Gameplay
         {
             StopSpawner();
 
-            activeSpawner = cellTypeChoiceOptions == CellTypeChoiceOptions.IntensityWise
-                ? intensitySpawner
-                : randomSpawner;
+            // One spawner model for every cell. Intensity selects the CellConfig
+            // (see AssignConfig's CellTypeChoiceOptions.IntensityWise) — and each config
+            // carries its own tuned SpawnProfile — so there is no separate intensity-wise
+            // spawner; RandomLifeSpawner drives them all. (Docs/ECOSYSTEM.md §5.)
+            activeSpawner = randomSpawner;
 
             activeSpawner.Start(this, cellConfigData, runtime, gameData);
 
diff --git a/Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs b/Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs
index f192abd5c..2335855bc 100644
--- a/Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs
+++ b/Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs
@@ -62,15 +62,6 @@ namespace CosmicShore.Gameplay
             host.RegisterSpawnedObject(go);
         }
 
-        protected Domains GetLocalDomainOr(GameDataSO gameData, Domains fallback) =>
-            gameData?.LocalRoundStats?.Domain ?? fallback;
-
-        protected Domains? GetExcludedDomain(bool excludeLocal, GameDataSO gameData, Domains fallbackLocal)
-        {
-            if (!excludeLocal) return null;
-            return GetLocalDomainOr(gameData, fallbackLocal);
-        }
-
         protected Domains PickRandomDomain(Domains? excluded)
         {
             // Playable domains only — never Blue, the "no team" sentinel. A Blue
diff --git a/Assets/_Scripts/Controller/Environment/CellNetworkSync.cs b/Assets/_Scripts/Controller/Environment/CellNetworkSync.cs
index c68b8b580..28a99c528 100644
--- a/Assets/_Scripts/Controller/Environment/CellNetworkSync.cs
+++ b/Assets/_Scripts/Controller/Environment/CellNetworkSync.cs
@@ -15,9 +15,9 @@ namespace CosmicShore.Gameplay
     ///     with the server's via <see cref="Cell.ApplyAuthoritativePhaseAndDomain"/>.
     ///
     /// Flora and fauna spawning is non-deterministic per-side (each client runs its own
-    /// IntensityWiseLifeSpawner with local Random.value rolls), so per-side LiveBlockCount
-    /// drifts. Server replication keeps shared gameplay rules (fauna goals, weights,
```

</details>

### `3eb20c30f` — feat(ecology): go all-in on volume — phase/dominant/prey/HUD key off mass, count is a safety cap

_Claude, 2026-06-05 15:51:16 +0000_

```text
Volume (mass) is now the cell's primary state variable; count is demoted to a rare
high-end frenzy/safety backstop. Players focus on volume, and the underpinnings react
to the same thing players see.

- Cell: add LiveVolume (sum of teamVolumes) + GetDomainVolume; DominantDomain,
  OpposingVolume, and the phase compute now use volume. RabidEnterThreshold is volume.
  Count survives only as CountFrenzyCap (forces Rabid when prism count is pathologically
  high — the perf backstop).
- CellPhaseThresholds: thresholds reinterpreted as VOLUME + new CountFrenzyCap field;
  Default recalibrated ~60x (avg Blob leaf volume). Blob asset recalibrated ~60x + cap.
- DomainVolumeIndicator reads GetDomainVolume (the gauge finally shows mass, per its name).
- RandomLifeSpawner prey gate uses OpposingVolume.
- Flora.AddHealthBlock sets leafSize before cell registration so the volume snapshot is the
  leaf's real target mass, not its spawn-time near-zero scale.
- Docs/ECOSYSTEM.md §1 + diagram: volume is the spine; count is the perf backstop.

Thresholds are x60 estimates from Blob's flora sizes — tune in-editor by watching
Cell.LiveVolume.
```

```text
 Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Cell Config.asset | 21 ++++-----
 Assets/_Scripts/Controller/Environment/Cell.cs                  | 91 ++++++++++++++++++++++++++++-----------
 Assets/_Scripts/Controller/Environment/FloraAndFauna/Flora.cs   |  6 ++-
 Assets/_Scripts/Controller/Environment/RandomLifeSpawner.cs     |  2 +-
 Assets/_Scripts/UI/DomainVolumeIndicator.cs                     | 33 +++++++-------
 Assets/_Scripts/Utility/DataContainers/CellPhaseThresholds.cs   | 33 ++++++++++----
 Docs/ECOSYSTEM.md                                               | 45 ++++++++++---------
 7 files changed, 147 insertions(+), 84 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 366 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/Cell.cs b/Assets/_Scripts/Controller/Environment/Cell.cs
index 127af3592..c44129de5 100644
--- a/Assets/_Scripts/Controller/Environment/Cell.cs
+++ b/Assets/_Scripts/Controller/Environment/Cell.cs
@@ -123,33 +123,51 @@ namespace CosmicShore.Gameplay
         CellPhase phase = CellPhase.Sprout;
 
         /// <summary>
-        /// Live count of unique prisms tracked through Add/RemoveBlock. Read-only signal
-        /// for systems that respond to prism load (e.g., LightFaunaManager scales its
-        /// fauna population with this so consumption keeps pace with growth, and the
-        /// phase system gates flora and fauna behavior on it).
+        /// Live count of unique prisms tracked through Add/RemoveBlock. Since the ecology
+        /// went "all in on volume", count is NO LONGER the gameplay driver — it survives
+        /// only as a cheap performance/safety signal (the high-end frenzy backstop in
+        /// <see cref="Update"/>) and for debug/telemetry. <see cref="LiveVolume"/> is the
+        /// real state variable. (Docs/ECOSYSTEM.md §1.)
         /// </summary>
         public int LiveBlockCount => trackedBlocks.Count;
 
         /// <summary>
-        /// Live leader by per-domain prism count. Recomputed on demand so the answer
-        /// always reflects the current Add/RemoveBlock-driven counts. Returns
-        /// <see cref="Domains.Blue"/> (the "no team" sentinel) when the cell has no
-        /// prisms tracked yet. Ties resolve in fixed order (Jade > Ruby > Gold > Blue).
+        /// Total live VOLUME (mass) tracked in the cell — the sum of every prism's
+        /// registration-time volume across all domains. This is the cell's primary state
+        /// variable: phase, dominant domain, prey, and the HUD all key off volume, not
+        /// count, so what players see (mass) is what the underpinnings react to.
+        /// (Docs/ECOSYSTEM.md §1.)
+        /// </summary>
+        public float LiveVolume
+        {
+            get
+            {
+                float v = 0f;
+                foreach (var kv in teamVolumes) v += kv.Value;
+                return v;
+            }
+        }
+
+        /// <summary>
+        /// Live leader by per-domain prism VOLUME (mass). Recomputed on demand so the
+        /// answer always reflects current Add/RemoveBlock-driven volume. Returns
+        /// <see cref="Domains.Blue"/> (the "no team" sentinel) when the cell holds no
+        /// mass yet. Ties resolve in fixed order (Jade > Ruby > Gold > Blue).
         /// </summary>
         public Domains DominantDomain
         {
             get
             {
                 Domains leader = Domains.Blue;
-                int leaderCount = 0;
+                float leaderVolume = 0f;
                 Domains[] order = { Domains.Jade, Domains.Ruby, Domains.Gold, Domains.Blue };
                 foreach (var d in order)
                 {
-                    if (!domainBlockCounts.TryGetValue(d, out int c)) continue;
-                    if (c > leaderCount)
+                    if (!teamVolumes.TryGetValue(d, out float v)) continue;
+                    if (v > leaderVolume)
                     {
                         leader = d;
-                        leaderCount = c;
+                        leaderVolume = v;
                     }
                 }
                 return leader;
@@ -157,18 +175,26 @@ namespace CosmicShore.Gameplay
         }
 
         /// <summary>
-        /// Live count of prisms tracked under <paramref name="domain"/>. Mirrors the
-        /// per-domain bookkeeping that <see cref="DominantDomain"/> reads, exposed so
-        /// HUD widgets (volume wedges, etc.) don't need to walk Add/RemoveBlock state
-        /// themselves. Returns 0 for untracked domains.
+        /// Live VOLUME (mass) tracked under <paramref name="domain"/> — the per-domain
+        /// signal <see cref="DominantDomain"/> and the HUD read. Returns 0 for domains
+        /// with no mass. (Alias of <see cref="GetTeamVolume"/>, named to match the HUD's
+        /// per-domain reads.)
```

</details>

### `dc77db465` — fix(ecology): harden wither-to-crystal — never strand a husk, bound crystal accumulation

_Claude, 2026-06-05 17:20:49 +0000_

```text
Starvation death could leak. If a fauna's core crystal had no / mis-wired cellData,
ActivateCrystal threw and the withered husk was never despawned; and even when it
worked, the left-behind crystals accumulated unbounded over a session (the menu
autopilot rarely collects them) — a slow perf/stability drain that matches the
"framerate lower / less stable" report.

- LightFauna.WitherToCrystalCoroutine: guard ActivateCrystal so a failure logs and
  discards the crystal instead of stranding the fauna; the creature ALWAYS despawns.
- Cell.RegisterWitherCrystal: bounded ring (MaxWitherCrystals = 16); oldest beyond the
  cap is destroyed. Cleared on Initialize/ResetCell.
- LightFaunaDataSO.leaveCrystalOnWither (default true): turn OFF to make withering just
  despawn — an isolation switch for crystal-accumulation perf testing.
```

```text
 Assets/_Scripts/Controller/Environment/Cell.cs                        | 34 +++++++++++++++++++++++++++++++++
 Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs    | 25 +++++++++++++++++++-----
 .../_Scripts/Controller/Environment/FloraAndFauna/LightFaunaDataSO.cs |  4 ++++
 3 files changed, 58 insertions(+), 5 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 119 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/Cell.cs b/Assets/_Scripts/Controller/Environment/Cell.cs
index c44129de5..696d54141 100644
--- a/Assets/_Scripts/Controller/Environment/Cell.cs
+++ b/Assets/_Scripts/Controller/Environment/Cell.cs
@@ -74,6 +74,13 @@ namespace CosmicShore.Gameplay
         }
         SnowChanger spawnedCytoplasm;
 
+        // Bounded ring of crystals left behind by withered (starved) fauna. Without a cap a
+        // long session would accumulate them unbounded (each is a live GameObject + collider
+        // + models) — a slow perf/stability leak, since the menu autopilot rarely collects
+        // them. Oldest beyond the cap is destroyed. Cleared with the rest of the cell's mass.
+        const int MaxWitherCrystals = 16;
+        readonly Queue<Crystal> witherCrystals = new();
+
         // ---------------------------------------------------------------------
         // Static spatial registry. Pooled prefab-spawned objects (trail prisms)
         // use this to find their containing cell — they have no scene identity
@@ -478,6 +485,7 @@ namespace CosmicShore.Gameplay
             spawnedLifeForms.Clear();
             trackedBlocks.Clear();
             domainBlockCounts.Clear();
+            ClearWitherCrystals();
             phase = CellPhase.Sprout;
 
             if (spawnedCytoplasm)
@@ -523,6 +531,31 @@ namespace CosmicShore.Gameplay
             UpdateCellStats();
         }
 
+        /// <summary>
+        /// Registers a crystal left behind by a withered (starved) fauna and bounds how many
+        /// persist: the oldest beyond <see cref="MaxWitherCrystals"/> is destroyed, so they
+        /// can't accumulate into a perf/stability leak over a long session.
+        /// </summary>
+        public void RegisterWitherCrystal(Crystal crystal)
+        {
+            if (!crystal) return;
+            witherCrystals.Enqueue(crystal);
+            while (witherCrystals.Count > MaxWitherCrystals)
+            {
+                var oldest = witherCrystals.Dequeue();
+                if (oldest) Destroy(oldest.gameObject);
+            }
+        }
+
+        void ClearWitherCrystals()
+        {
+            while (witherCrystals.Count > 0)
+            {
+                var c = witherCrystals.Dequeue();
+                if (c) Destroy(c.gameObject);
+            }
+        }
+
         public void UnregisterSpawnedObject(GameObject obj)
         {
             if (spawnedLifeForms.Remove(obj))
@@ -534,6 +567,7 @@ namespace CosmicShore.Gameplay
             spawnedLifeForms.Clear();
             trackedBlocks.Clear();
             domainBlockCounts.Clear();
+            ClearWitherCrystals();
             phase = CellPhase.Sprout;
 
             // Bind runtime -> this cell
diff --git a/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs b/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs
index a54599145..aa4bde964 100644
--- a/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs
+++ b/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs
@@ -132,13 +132,28 @@ namespace CosmicShore.Gameplay
                 else yield return null;
             }
 
-            // Leave the crystal behind at the center: re-home it on the cell and activate it
-            // (collider on, material fade-in) so vessels can collect it and flora can grow
-            // around it — the creature's mass re-enters the food web instead of vanishing.
-            if (crystal)
+            // Leave the crystal behind at the center — but NEVER let a crystal hand-off
+            // failure strand a withered husk: the creature must always despawn. Activation is
```

</details>

### `dce8ff707` — tune(ecology): Blob fauna spawn period ×4 (12→48s), population ×2 (4→8)

_Claude, 2026-06-05 17:52:05 +0000_

```text
Fewer, larger fauna bursts: lower spawn churn (and thus fewer wither/crystal events
per minute) while keeping swarm presence. Per prompter request.
```

```text
 Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Cell Spawn Profile.asset | 2 +-
 Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Fauna Config Data.asset  | 1 +
 2 files changed, 2 insertions(+), 1 deletion(-)
```

### `5219c6099` — fix(ecology): dropped wither-crystals are now collectible powerups (element buff + UI)

_Claude, 2026-06-05 17:52:05 +0000_

```text
The fauna's core crystal is CrystalMass.prefab, which (unlike flora crystals) ships
WITHOUT the ElementalCrystalImpactor + ImpactCollider — flora add them as a prefab
override, the fauna's instance doesn't. SkimmerImpactor only applies the element buff
on `case ElementalCrystalImpactor`, so a collected wither-crystal gave no buff and no
elemental UI change.

- LightFauna.EnsureCrystalCollectible: at drop time, attach the missing
  ElementalCrystalImpactor + wired ImpactCollider to the crystal (idempotent), carrying
  the crystal's authored Element (e.g. Mass for the brittlestar). Collection then routes
  through the normal skimmer buff path → ResourceSystem.AdjustLevel → ElementalBarsView.
- ImpactCollider.Configure(IImpactor): runtime wiring hook (authored prefabs set the
  field in the inspector; this is the code-path equivalent).

Verified the crystal collider is already a trigger and no crystal-buff effect reads the
crystal impactor's DI container, so the runtime-attached impactor needs no DI. The
cleaner long-term fix is to add these two components to the fauna's crystal in the
prefab (matching flora); this guarantees it regardless.
```

```text
 Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs | 20 ++++++++++++++++++++
 Assets/_Scripts/Controller/ImpactEffects/ImpactCollider.cs         |  7 +++++++
 2 files changed, 27 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs b/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs
index aa4bde964..23a6c230f 100644
--- a/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs
+++ b/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs
@@ -141,6 +141,7 @@ namespace CosmicShore.Gameplay
             if (leaveCrystal)
             {
                 crystal.gameObject.SetActive(true);
+                EnsureCrystalCollectible(crystal);
                 try { crystal.ActivateCrystal(); }
                 catch (System.Exception e)
                 {
@@ -159,6 +160,25 @@ namespace CosmicShore.Gameplay
             Die("starvation");
         }
 
+        /// <summary>
+        /// Make the dropped crystal a collectible powerup. The fauna's core crystal
+        /// (CrystalMass) ships WITHOUT the <see cref="ElementalCrystalImpactor"/> +
+        /// <see cref="ImpactCollider"/> that flora add as a prefab override, so a collected
+        /// wither-crystal gave no element buff/UI — <see cref="SkimmerImpactor"/> only buffs
+        /// on <c>case ElementalCrystalImpactor</c>. Attach them at drop time (carrying the
+        /// crystal's authored Element) so collection works. Idempotent. The cleaner long-term
+        /// fix is to add these to the fauna's crystal in the prefab, matching flora.
+        /// </summary>
+        static void EnsureCrystalCollectible(Crystal crystal)
+        {
+            if (!crystal) return;
+            if (!crystal.TryGetComponent<CrystalImpactor>(out var impactor))
+                impactor = crystal.gameObject.AddComponent<ElementalCrystalImpactor>();
+            if (!crystal.TryGetComponent<ImpactCollider>(out var impactCollider))
+                impactCollider = crystal.gameObject.AddComponent<ImpactCollider>();
+            impactCollider.Configure(impactor);
+        }
+
         IEnumerator UpdateBehaviorCoroutine()
         {
             while (true)
diff --git a/Assets/_Scripts/Controller/ImpactEffects/ImpactCollider.cs b/Assets/_Scripts/Controller/ImpactEffects/ImpactCollider.cs
index 1d4411918..c93638981 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/ImpactCollider.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/ImpactCollider.cs
@@ -12,5 +12,12 @@ namespace CosmicShore.Gameplay
         private Object impactorObject;
         
         public IImpactor Impactor => impactorObject as IImpactor;
+
+        /// <summary>
+        /// Runtime wiring for impactors attached after authoring — e.g. a fauna's dropped
+        /// crystal made collectible on wither. Authored prefabs set <c>impactorObject</c> in
+        /// the inspector; this is the equivalent code path.
+        /// </summary>
+        public void Configure(IImpactor impactor) => impactorObject = impactor as Object;
     }
 }
\ No newline at end of file
```

</details>

### `106f6436c` — add meta files

_Garrett Milliron, 2026-06-05 14:01:45 -0400_

```text
 Assets/_Scripts/UI/DomainVolumeHexGraphic.cs.meta | 2 ++
 Assets/_Scripts/UI/DomainVolumeIndicator.cs.meta  | 2 ++
 2 files changed, 4 insertions(+)
```

### `d81fd4682` — fix(ecology): menu trail prisms persist (no snake) — default trail cap 0

_Claude, 2026-06-05 18:28:51 +0000_

```text
The autopilot trail recycled its oldest prisms back to the pool past a 200-block cap,
producing a fixed-length "snake" trail. Trail prisms are mass: they should persist
until a player destroys them or fauna consume them (the ecology's down-force), not be
silently recycled. Default menuTrailBlockCap 200 -> 0 (unbounded).
```

```text
 Assets/_Scripts/Controller/Multiplayer/MenuServerPlayerVesselInitializer.cs | 10 ++++++----
 1 file changed, 6 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Multiplayer/MenuServerPlayerVesselInitializer.cs b/Assets/_Scripts/Controller/Multiplayer/MenuServerPlayerVesselInitializer.cs
index b5e4806d8..4ac3a6597 100644
--- a/Assets/_Scripts/Controller/Multiplayer/MenuServerPlayerVesselInitializer.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/MenuServerPlayerVesselInitializer.cs
@@ -27,10 +27,12 @@ namespace CosmicShore.Gameplay
     public class MenuServerPlayerVesselInitializer : ServerPlayerVesselInitializer
     {
         [Header("Menu Lava-Lamp Trail")]
-        [Tooltip("Per-trail block cap for the cosmetic autopilot trail in Menu_Main. The " +
-                 "oldest blocks recycle to the pool past this count so trail geometry and " +
-                 "trigger colliders stay bounded while the menu idles. 0 = unbounded.")]
-        [SerializeField] int menuTrailBlockCap = 200;
+        [Tooltip("Per-trail block cap for the autopilot trail in Menu_Main. The oldest " +
+                 "blocks recycle to the pool past this count. 0 = unbounded (default): trail " +
+                 "prisms are mass — they should persist until destroyed by a player or consumed " +
+                 "by fauna (the ecology's down-force), not silently recycled into a snake trail. " +
+                 "Set > 0 only if a cell's mass genuinely runs away with no consumers.")]
+        [SerializeField] int menuTrailBlockCap = 0;
 
         bool _isSwapping;
 
```

</details>

### `7d7358307` — fix(ecology): collecting an elemental crystal buffs the collector's element (+ UI)

_Claude, 2026-06-05 18:28:51 +0000_

```text
Crystal -> element-buff was never wired: every vessel's elemental crystal effect slots
(vesselMass/Charge/Space/TimeCrystalEffects) are empty, the Omni effects are
explosion/haptics/reporter (no buff), and ElementalCrystalImpactor.elementalCrystalShipEffects
isn't even [SerializeField] (dead). Elemental crystals are collected by the skimmer, which
disables the crystal collider, so the VesselImpactor crystal-buff path can't fire either.

Apply the buff at the reliable point — when the skimmer collects the crystal: raise the
collector's matching element via ResourceSystem.IncrementLevel(crystal.Element). The
elemental ship UI updates through ResourceSystem.OnElementLevelChange -> ElementalBarsView.
Works for all elemental crystals (flora + the fauna's wither drop, which is Mass).
```

```text
 Assets/_Scripts/Controller/ImpactEffects/Impactors/ElementalCrystalImpactor.cs | 12 +++++++++++-
 1 file changed, 11 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ElementalCrystalImpactor.cs b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ElementalCrystalImpactor.cs
index 9fe6b7c81..4ac8054a7 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ElementalCrystalImpactor.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ElementalCrystalImpactor.cs
@@ -32,7 +32,17 @@ namespace CosmicShore.Gameplay
 
             isImpacting = true;
             _hasBeenCollected = true;
-            
+
+            // Powerup buff: collecting an elemental crystal raises the collector's matching
+            // element level, which the elemental ship UI reflects via
+            // ResourceSystem.OnElementLevelChange → ElementalBarsView. This is the reliable
+            // buff point: elemental crystals are collected by the SKIMMER, which disables the
+            // collider just below, so the VesselImpactor crystal-buff path (which keys off the
+            // collider) can't fire. The crystal's authored Element selects which resource is
+            // buffed (e.g. Mass for the brittlestar's dropped crystal).
+            var resourceSystem = skimmerImpactor.Skimmer?.VesselStatus?.ResourceSystem;
+            resourceSystem?.IncrementLevel(Crystal.crystalProperties.Element);
+
             var col = Crystal.GetComponent<Collider>();
             if (col) col.enabled = false;
 
```

</details>

### `2da494148` — chore(ecology): reconcile with bleeding-edge elemental-crystal powerup

_Claude, 2026-06-05 18:35:24 +0000_

```text
bleeding-edge landed the proper elemental-crystal powerup (scale-based
SkimmerAdjustElementLevelByCrystalEffectSO, the [SerializeField] fix on
elementalCrystalShipEffects, wired into all 12 flora/fauna prefabs, plus
DestroyCrystal-after-collect). That supersedes my interim fix, so drop the
duplicates to avoid double-buffing:

- ElementalCrystalImpactor: remove the hardcoded ResourceSystem.IncrementLevel —
  the merge had kept it alongside their SO effect, which would double-buff.
- LightFauna: remove EnsureCrystalCollectible — the fauna's crystal already carries
  the ElementalCrystalImpactor; the prefabs now wire the scale-based effect onto it.
- ImpactCollider: remove the now-unused Configure() runtime-wiring hook.

The wither still drops the (prefab-wired) collectible crystal; collection grants the
element powerup + UI via bleeding-edge's SO effect.
```

```text
 Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs             | 20 --------------------
 Assets/_Scripts/Controller/ImpactEffects/ImpactCollider.cs                     |  7 -------
 Assets/_Scripts/Controller/ImpactEffects/Impactors/ElementalCrystalImpactor.cs | 10 ----------
 3 files changed, 37 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs b/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs
index 23a6c230f..aa4bde964 100644
--- a/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs
+++ b/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs
@@ -141,7 +141,6 @@ namespace CosmicShore.Gameplay
             if (leaveCrystal)
             {
                 crystal.gameObject.SetActive(true);
-                EnsureCrystalCollectible(crystal);
                 try { crystal.ActivateCrystal(); }
                 catch (System.Exception e)
                 {
@@ -160,25 +159,6 @@ namespace CosmicShore.Gameplay
             Die("starvation");
         }
 
-        /// <summary>
-        /// Make the dropped crystal a collectible powerup. The fauna's core crystal
-        /// (CrystalMass) ships WITHOUT the <see cref="ElementalCrystalImpactor"/> +
-        /// <see cref="ImpactCollider"/> that flora add as a prefab override, so a collected
-        /// wither-crystal gave no element buff/UI — <see cref="SkimmerImpactor"/> only buffs
-        /// on <c>case ElementalCrystalImpactor</c>. Attach them at drop time (carrying the
-        /// crystal's authored Element) so collection works. Idempotent. The cleaner long-term
-        /// fix is to add these to the fauna's crystal in the prefab, matching flora.
-        /// </summary>
-        static void EnsureCrystalCollectible(Crystal crystal)
-        {
-            if (!crystal) return;
-            if (!crystal.TryGetComponent<CrystalImpactor>(out var impactor))
-                impactor = crystal.gameObject.AddComponent<ElementalCrystalImpactor>();
-            if (!crystal.TryGetComponent<ImpactCollider>(out var impactCollider))
-                impactCollider = crystal.gameObject.AddComponent<ImpactCollider>();
-            impactCollider.Configure(impactor);
-        }
-
         IEnumerator UpdateBehaviorCoroutine()
         {
             while (true)
diff --git a/Assets/_Scripts/Controller/ImpactEffects/ImpactCollider.cs b/Assets/_Scripts/Controller/ImpactEffects/ImpactCollider.cs
index c93638981..1d4411918 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/ImpactCollider.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/ImpactCollider.cs
@@ -12,12 +12,5 @@ namespace CosmicShore.Gameplay
         private Object impactorObject;
         
         public IImpactor Impactor => impactorObject as IImpactor;
-
-        /// <summary>
-        /// Runtime wiring for impactors attached after authoring — e.g. a fauna's dropped
-        /// crystal made collectible on wither. Authored prefabs set <c>impactorObject</c> in
-        /// the inspector; this is the equivalent code path.
-        /// </summary>
-        public void Configure(IImpactor impactor) => impactorObject = impactor as Object;
     }
 }
\ No newline at end of file
diff --git a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ElementalCrystalImpactor.cs b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ElementalCrystalImpactor.cs
index 1e0a31e95..af8a50506 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ElementalCrystalImpactor.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ElementalCrystalImpactor.cs
@@ -36,16 +36,6 @@ namespace CosmicShore.Gameplay
             isImpacting = true;
             _hasBeenCollected = true;
 
-            // Powerup buff: collecting an elemental crystal raises the collector's matching
-            // element level, which the elemental ship UI reflects via
-            // ResourceSystem.OnElementLevelChange → ElementalBarsView. This is the reliable
-            // buff point: elemental crystals are collected by the SKIMMER, which disables the
-            // collider just below, so the VesselImpactor crystal-buff path (which keys off the
-            // collider) can't fire. The crystal's authored Element selects which resource is
-            // buffed (e.g. Mass for the brittlestar's dropped crystal).
-            var resourceSystem = skimmerImpactor.Skimmer?.VesselStatus?.ResourceSystem;
-            resourceSystem?.IncrementLevel(Crystal.crystalProperties.Element);
-
             var col = Crystal.GetComponent<Collider>();
             if (col) col.enabled = false;
 
```

</details>

### `fb8953cf7` — feat(ecology): spawn fauna in the Skim Race (HexRace) cell to graze accumulated prisms

_Claude, 2026-06-05 19:16:34 +0000_

```text
The Barren cell (HexRace / Skim Race) had empty SupportedFaunas — no fauna ever spawned,
so trail/track prism mass only ever grew. Wire the brittlestar fauna config in and drop
BaseFaunaSpawnTime 300 -> 30s so fauna appear during a race and consume opposing-domain
mass, pulling prism count/volume down as laps accumulate.

Tuning: FaunaFoodFloor (opposing-prey VOLUME gate) controls WHEN fauna appear — raise it
to delay them to later/final laps once the in-editor volume scale is known.
```

```text
 Assets/_SO_Assets/Cell Configs/Barren Cell/Barren Cell Spawn Profile.asset | 6 ++++--
 1 file changed, 4 insertions(+), 2 deletions(-)
```

### `7294f4ba1` — feat(ecology): enforce every lifeform drops one elemental-crystal powerup on death

_Claude, 2026-06-05 20:04:20 +0000_

```text
Per prompter: it must not be possible to make a lifeform that doesn't carry one of the
four elemental crystals (Charge/Mass/Space/Time) that drops as a powerup on death.
Element stays per-lifeform AUTHORED; random is only the misconfig fallback.

- LifeFormCrystal.EnsureElementalCrystal: the runtime guard, called by LifeForm (flora)
  and LightFauna (fauna) at init. Authored elemental crystal -> used as-is; non-elemental
  -> Element set to a random elemental in place; no crystal -> a default is provisioned
  from ElementalCrystalSet. Misconfig branches log loudly.
- ElementalCrystalSetSO + Resources/ElementalCrystalSet.asset: the four crystal prefabs,
  the fallback source (loaded from Resources).
- LifeFormCrystalValidator (Tools > Cosmic Shore > Validate Lifeform Crystals): scans all
  flora/fauna prefabs and flags any missing / non-elemental / duplicate crystal at author
  time so they get fixed properly.

Net: a spawned lifeform can never die without leaving an elemental powerup.
```

```text
 Assets/Resources/ElementalCrystalSet.asset                            | 18 +++++++++
 Assets/Resources/ElementalCrystalSet.asset.meta                       |  8 ++++
 Assets/_Scripts/Controller/Environment/FloraAndFauna/LifeForm.cs      |  4 +-
 .../_Scripts/Controller/Environment/FloraAndFauna/LifeFormCrystal.cs  | 67 +++++++++++++++++++++++++++++++++
 .../Controller/Environment/FloraAndFauna/LifeFormCrystal.cs.meta      | 11 ++++++
 Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs    |  6 +++
 Assets/_Scripts/Editor/LifeFormCrystalValidator.cs                    | 63 +++++++++++++++++++++++++++++++
 Assets/_Scripts/Editor/LifeFormCrystalValidator.cs.meta               | 11 ++++++
 Assets/_Scripts/ScriptableObjects/ElementalCrystalSetSO.cs            | 49 ++++++++++++++++++++++++
 Assets/_Scripts/ScriptableObjects/ElementalCrystalSetSO.cs.meta       | 11 ++++++
 10 files changed, 247 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 229 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/FloraAndFauna/LifeForm.cs b/Assets/_Scripts/Controller/Environment/FloraAndFauna/LifeForm.cs
index e95e90c2f..e3db92376 100644
--- a/Assets/_Scripts/Controller/Environment/FloraAndFauna/LifeForm.cs
+++ b/Assets/_Scripts/Controller/Environment/FloraAndFauna/LifeForm.cs
@@ -93,7 +93,9 @@ namespace CosmicShore.Gameplay
             healthTracker = new HealthBlockTracker(healthBlocksForMaturity, minHealthBlocks, cell);
             spindleTracker = new SpindleTracker();
 
-            crystal = GetComponentInChildren<Crystal>();
+            // Guarantee the lifeform invariant: every lifeform carries one elemental crystal
+            // that drops as a powerup on death (LifeFormCrystal fixes/provisions if needed).
+            crystal = LifeFormCrystal.EnsureElementalCrystal(this);
 
             BindEmbeddedParts();
 
diff --git a/Assets/_Scripts/Controller/Environment/FloraAndFauna/LifeFormCrystal.cs b/Assets/_Scripts/Controller/Environment/FloraAndFauna/LifeFormCrystal.cs
new file mode 100644
index 000000000..6c6d96fb6
--- /dev/null
+++ b/Assets/_Scripts/Controller/Environment/FloraAndFauna/LifeFormCrystal.cs
@@ -0,0 +1,67 @@
+using CosmicShore.ScriptableObjects;
+using CosmicShore.Utility;
+using UnityEngine;
+
+namespace CosmicShore.Gameplay
+{
+    /// <summary>
+    /// Enforces the lifeform invariant: every lifeform (flora and fauna) carries exactly one
+    /// elemental crystal (Charge / Mass / Space / Time) that drops as a collectible powerup on
+    /// death. Both <see cref="LifeForm"/> and the concrete fauna route their crystal through
+    /// <see cref="EnsureElementalCrystal"/> at init, so it is not possible for a spawned
+    /// lifeform to die without dropping a powerup:
+    ///
+    ///   • authored elemental crystal child   → used as-is (the normal, per-prefab path)
+    ///   • authored crystal but non-elemental  → its Element is set to a random element in place
+    ///   • no crystal at all                   → a default elemental crystal is provisioned from
+    ///                                           ElementalCrystalSet (Resources), random element
+    ///
+    /// The misconfigured branches log loudly; the editor validator (Tools ▸ Cosmic Shore ▸
+    /// Validate Lifeform Crystals) flags the same prefabs at author time so they get fixed.
+    /// Per the prompter: element is per-lifeform AUTHORED — random is only the misconfig fallback.
+    /// </summary>
+    public static class LifeFormCrystal
+    {
+        public static Crystal EnsureElementalCrystal(Component owner)
+        {
+            if (!owner) return null;
+
+            var crystal = owner.GetComponentInChildren<Crystal>(true);
+            if (crystal)
+            {
+                if (!crystal.crystalProperties.IsElemental)
+                {
+                    var element = ElementalCrystalSetSO.RandomElement();
+                    CSDebug.LogWarning($"[LifeFormCrystal] {owner.name}: crystal element was " +
+                        $"'{crystal.crystalProperties.Element}' (not one of the four elementals); assigning " +
+                        $"'{element}' so it still drops a powerup. Author an elemental crystal to fix.");
+                    crystal.crystalProperties.Element = element;
+                }
+                return crystal;
+            }
+
+            // No crystal authored — provision a default so death still drops a powerup.
+            var set = ElementalCrystalSetSO.Load();
+            if (!set)
+            {
+                CSDebug.LogError($"[LifeFormCrystal] {owner.name} has no crystal and " +
+                    $"Resources/{ElementalCrystalSetSO.ResourcePath} is missing — cannot guarantee a death " +
+                    $"powerup. Author an elemental crystal on the lifeform, or add the set asset.");
+                return null;
+            }
+
+            var fallbackElement = ElementalCrystalSetSO.RandomElement();
+            var prefab = set.GetPrefab(fallbackElement);
+            if (!prefab)
+            {
+                CSDebug.LogError($"[LifeFormCrystal] {owner.name} has no crystal and the elemental crystal " +
+                    $"set has no prefab for '{fallbackElement}' — cannot guarantee a death powerup.");
+                return null;
```

</details>

### `405a271fb` — docs(ecology): master plan + locked invariants + /ecology skill (foundations)

_Claude, 2026-06-08 17:46:13 +0000_

```text
Phase 0 of the living-ecosystem master plan. Grounds the next phase and kills the two
recurring frictions (design-intent reverts, perf regressions) by locking the canon where
it can be referenced.

- Docs/ECOSYSTEM_MASTERPLAN.md: north star (5 pillars); the artificial-life scorecard
  (NASA Ladder of Life Detection / Koshland PICERAS / Polyworld — we satisfy ~5/7 criteria,
  the gap is genome + reproduction + evolution); the collider-budget performance contract;
  the platform-fundamental wiring plan; the phased roadmap; the orchestration model.
- CLAUDE.md: new "Ecosystem Design Principles (LOCKED)" section — the invariants that
  previously lived only in conversation (no imposed death; controlling-color spawn;
  wither-to-crystal + mass conservation; volume spine; lifeform-crystal invariant;
  territorial permanence; endogenous selection; collider budget).
- .claude/skills/ecology/SKILL.md: the change protocol.

Key insight: the team's "favor emergence / don't cheat" law IS the artificial-life bar
(endogenous selection = no scripted fitness), and wither-to-crystal already conserves mass
(the self-sustaining economy). The design philosophy and the life criteria are one thing.
```

```text
 .claude/skills/ecology/SKILL.md |  51 ++++++++++++++++
 CLAUDE.md                       |  34 +++++++++++
 Docs/ECOSYSTEM_MASTERPLAN.md    | 224 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 3 files changed, 309 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 332 lines)</summary>

```diff
diff --git a/.claude/skills/ecology/SKILL.md b/.claude/skills/ecology/SKILL.md
new file mode 100644
index 000000000..ba5d1d0fb
--- /dev/null
+++ b/.claude/skills/ecology/SKILL.md
@@ -0,0 +1,51 @@
+---
+name: ecology
+description: Use for ANY change to the Cosmic Shore cell ecosystem — flora, fauna, cells, crystals, spawn profiles (SpawnProfileSO / CellConfigDataSO), phase/volume (Cell.LiveVolume, CellPhaseThresholds), lifeform powerups (LifeFormCrystal / ElementalCrystal*), wither/starvation, predator-prey, reproduction, evolution, or biome/intensity tuning. Loads the locked invariants + the collider-budget gate + the change protocol so changes start from the correct design and stay performant. Trigger when editing Assets/_Scripts/Controller/Environment/** (Cell, RandomLifeSpawner, Flora*, Fauna, LightFauna), the ecology SOs, or Docs/ECOSYSTEM*.md.
+---
+
+# Ecology Change Protocol
+
+You are changing the Cosmic Shore cell ecosystem — a **platform fundamental** on the path to
+credible artificial life. The system's design intent has caused repeated rework when guessed at;
+this protocol exists to prevent that. Follow it exactly.
+
+## 1. Read the canon first
+- `CLAUDE.md ▸ Ecosystem Design Principles (LOCKED)` — the invariants (authoritative).
+- `Docs/ECOSYSTEM_MASTERPLAN.md` — north star, the artificial-life scorecard (§3), the **collider
+  contract (§4)**, the platform-wiring plan (§5), the phased roadmap (§6), the orchestration (§7).
+- `Docs/ECOSYSTEM.md` — the mechanics log (how the current system actually works).
+
+## 2. Restate before you edit (this kills the #1 source of rework)
+In one or two lines, state which invariants the change touches and confirm it violates **none**:
+no imposed death/decay/lifespan · no domain asymmetry (controlling-color spawn only) ·
+wither-to-crystal + mass conservation · volume is the spine (not count) · the lifeform→elemental-
+crystal invariant · territorial permanence (don't cull the dominant canopy) · endogenous
+selection only (survival = fitness, never a scripted fitness function) · the collider budget.
+**If a change might violate one, STOP and ask (AskUserQuestion). Do not guess the design.**
+
+## 3. Implement (emergence first, surgically)
+- **Favor emergence:** never hard-code an outcome that should emerge from the fundamentals
+  (Domain · Mass/prisms · Cells · Elementals · Flora & Fauna · Vessels) interacting. A scripted
+  outcome is the same bug as a scripted fitness function — it breaks the gameplay *and* the
+  artificial-life claim. Order of preference: use a fundamental → tune it → extend it →
+  (with sign-off) propose a new one → bespoke only as last resort.
+- **Config-driven:** tunables in ScriptableObjects; cross-system comms via SOAP events/variables;
+  no singletons/static events. Variety = biome × intensity × heritable traits, not bespoke code.
+- **Surgical:** match surrounding style; three similar lines beat a premature abstraction.
+
+## 4. Respect the collider budget (HARD GATE — perf is collider-bound)
+- State the change's impact on **active colliders per cell**.
+- Prefer the Burst `BlockDensityGrid` / `PrismAOERegistry` for spatial queries over
+  `Physics.OverlapSphere` and over adding colliders.
+- Honor collider-LOD-by-phase (prism colliders disabled at Frozen) and the per-cell budget.
+- If a change adds colliders or queries, say explicitly how the budget stays met.
+
+## 5. Hand back verification — you cannot run Unity; the human is the gate
+- State the exact in-editor steps to verify, the scene to test, and the precise SO knobs to tune.
+- Use the collider/volume telemetry overlay when it exists to make the budget observable.
+- Never claim something works that you have not seen work. Report honestly (failures, skips, caveats).
+
+## 6. Commit
+One coherent step per commit; conventional-commit message; develop on the feature branch (never
+`bleeding-edge`); open a PR only when asked. After lifeform-prefab changes, note to run
+`Tools ▸ Cosmic Shore ▸ Validate Lifeform Crystals`.
diff --git a/CLAUDE.md b/CLAUDE.md
index d70bc1cd7..9a4d5250f 100644
--- a/CLAUDE.md
+++ b/CLAUDE.md
@@ -6,6 +6,40 @@ You are expected to work autonomously and persistently. Complete the entire task
 
 When a task spans multiple files or systems, complete ALL of them in a single pass. Do not stop after the first file and ask if you should proceed to the next.
 
+## Ecosystem Design Principles (LOCKED — read before any ecology change)
+
+The cell ecosystem (flora/fauna/cells/crystals) is a **platform fundamental** on the path to
+credible **artificial life**. North star + roadmap: `Docs/ECOSYSTEM_MASTERPLAN.md`. Mechanics
+log: `Docs/ECOSYSTEM.md`. These invariants are **locked** — do not relitigate or re-derive them.
+They are a direct application of "Favor Emergent Systems / Don't cheat emergence" (below) and —
+not by accident — they are also what makes the system credible as artificial life (a scripted
+outcome is optimization, not life). Use the `/ecology` skill for any change here.
+
+- **No imposed death.** No decay, lifespan, or fixed-period despawn timers. Populations are
+  bounded by **consumption + starvation**, never an imposed clock. (Repeatedly rejected.)
+- **No domain asymmetry.** Fauna spawn in **one color — the cell's controlling color** — and
+  hunt opposing mass. Never cross-domain / prey-weighted / per-domain-biased spawning.
+- **Starvation = wither-to-crystal.** A starving creature withers from its extremity spindles
+  inward and leaves a collectible elemental crystal — it does not vanish. **Mass is conserved**
```

</details>

### `d9d2ece5f` — docs(ecology): P1 execution plan (collider budget) — resumable handoff

_Claude, 2026-06-08 18:23:35 +0000_

```text
Captures the staged P1 order (telemetry -> fauna density-grid queries -> proximity
collider-LOD -> per-cell prism budget -> LifeformBootstrap) and the key dependency:
do NOT blanket-disable prism colliders at Frozen (player + fauna both need them), so the
fauna->grid query swap must land before any collider-LOD. Written so P1 resumes cleanly
in a fresh session via the /ecology skill.
```

```text
 Docs/ECOSYSTEM_MASTERPLAN.md | 32 ++++++++++++++++++++++++++++++--
 1 file changed, 30 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/ECOSYSTEM_MASTERPLAN.md b/Docs/ECOSYSTEM_MASTERPLAN.md
index 3e2912279..02d6b6884 100644
--- a/Docs/ECOSYSTEM_MASTERPLAN.md
+++ b/Docs/ECOSYSTEM_MASTERPLAN.md
@@ -220,5 +220,33 @@ Make a Cell+ecology a *default* any scene gets, like a `ContainerScope`:
 
 ---
 
-*Next: Phase 1. Recommended first orchestrated move — build the collider budget + telemetry
-(the hard gate) and the `LifeformBootstrap`, then wire the bare modes. See §6/§7.*
+---
+
+## 10. P1 execution detail — RESUME HERE
+
+**Decided: P1 first** (the collider budget gate, before scaling life to every mode).
+
+**The collider-LOD dependency (key insight — don't lose this):** you must NOT blanket-disable
+prism colliders at Frozen. Prism colliders are needed by (a) the **player** colliding with the
+canopy and (b) **fauna** finding prey via `Physics.OverlapSphere`. So the safe staged order is:
+
+1. **Telemetry (safe, do first).** Add `CellConfigDataSO.ColliderBudget` (int) + an in-editor
+   overlay showing per cell: phase, prism count (≈ active colliders today), `LiveVolume`, vs
+   budget (red when over). Reads existing `Cell.LiveBlockCount` / `LiveVolume` / `Phase`. Makes
+   the budget **observable** — the perf-regression guard. Lowest risk; ship + validate first.
+2. **Fauna → density-grid queries.** Replace `LightFauna.UpdateBehavior`'s `Physics.OverlapSphere`
+   (~LightFauna.cs:268) with a Burst `BlockDensityGrid` neighborhood query (new API, e.g.
+   `GetPrismsInRadius`). Removes fauna's collider dependency + saves ~2–5 ms. **Prerequisite for LOD.**
+3. **Proximity collider-LOD** (NOT a blanket phase-disable). Disable colliders on prisms far from
+   any vessel; re-enable on approach → active colliders bounded to "prisms near a vessel"
+   regardless of total prism count. Track the precise active count → feed the telemetry. Touch:
+   `Prism.cs` collider toggles (~247/305/452/OnDisable) gated on a coarse vessel-proximity check.
+4. **Per-cell active-prism budget.** Recycle oldest **trail** prisms when over budget — **never
+   cull the flora canopy** (territorial permanence + mass conservation). Generalize the menu trail cap.
+5. **`LifeformBootstrap`** prefab (Cell + CellConfig + budget + telemetry) → wire the ~9 bare modes.
+
+**P1 correctness invariants:** player-prism collision must survive; fauna must still find prey
+(via the grid); never cull flora mass to meet the budget (recycle trails only).
+
+**To resume:** invoke the `/ecology` skill, read this §10, continue from the first unchecked
+slice. All prior work is committed on `claude/epic-darwin-g0Ehi`.
```

</details>

### `79f6996d3` — feat(ecology): seal mass-conservation into the fauna death path (re-assert locked invariant)

_Claude, 2026-06-11 21:47:36 +0000_

```text
Bleeding-edge's creature fauna vanish on death — LightFauna.Die() and Boid.Die()
just Destroy the GameObject, dropping no crystal. That violates two locked
invariants: "starvation = wither-to-crystal, mass is conserved" and "every lifeform
drops one elemental crystal as a powerup on death — it must not be possible to make
a lifeform that violates this."

Re-assert it structurally on the merged foundation, mirroring the proven flora
pattern (LifeForm.Die -> crystal.ActivateCrystal):

- Fauna base: add `protected Crystal crystal` and SEAL the death chokepoint. `Die`
  is now non-virtual: it drops the elemental crystal (ActivateCrystal reparents it
  to the cell so it outlives the creature as a collectible powerup) and then calls
  a new `protected virtual void OnDeath` hook for subclass removal. Both death paths
  (starvation, Predated) route through Die, so no subclass can die without conserving
  its mass — the invariant can't be bypassed.
- LightFauna / Boid / BodySegmentFauna: `Die` override -> `OnDeath` override.
- LightFauna / Boid: provision the crystal in Initialize via
  LifeFormCrystal.EnsureElementalCrystal (authored-crystal fast path; provisions if
  missing). BodySegmentFauna stays crystal-null — the worm owns its crystal, a
  segment is not a standalone lifeform.

Verification (human gate — I can't run Unity): play a cell with fauna; on
starvation and on predation the creature should leave a collectible elemental
crystal that buffs the collector's matching element. Run Tools > Cosmic Shore >
Validate Lifeform Crystals and author one elemental crystal on each fauna prefab so
EnsureElementalCrystal is a no-op fast path (keeps the per-fauna collider budget neutral).
```

```text
 .../_Scripts/Controller/Environment/FloraAndFauna/BodySegmentFauna.cs |  2 +-
 Assets/_Scripts/Controller/Environment/FloraAndFauna/Boid.cs          |  8 +++++++-
 Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs         | 33 ++++++++++++++++++++++++++++++---
 Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs    |  8 +++++++-
 4 files changed, 45 insertions(+), 6 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 110 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/FloraAndFauna/BodySegmentFauna.cs b/Assets/_Scripts/Controller/Environment/FloraAndFauna/BodySegmentFauna.cs
index 92e855966..d680fec2a 100644
--- a/Assets/_Scripts/Controller/Environment/FloraAndFauna/BodySegmentFauna.cs
+++ b/Assets/_Scripts/Controller/Environment/FloraAndFauna/BodySegmentFauna.cs
@@ -14,7 +14,7 @@ namespace CosmicShore.Gameplay
         public bool IsHead;
         public bool IsTail;
 
-        protected override void Die(string killerName = "")
+        protected override void OnDeath(string killerName = "")
         {
             if (!IsHead && !IsTail)
             {
diff --git a/Assets/_Scripts/Controller/Environment/FloraAndFauna/Boid.cs b/Assets/_Scripts/Controller/Environment/FloraAndFauna/Boid.cs
index d4aa3d4e6..3462ffc47 100644
--- a/Assets/_Scripts/Controller/Environment/FloraAndFauna/Boid.cs
+++ b/Assets/_Scripts/Controller/Environment/FloraAndFauna/Boid.cs
@@ -101,6 +101,12 @@ namespace CosmicShore.Gameplay
                 hp.Initialize("tadpole");
             }
 
+            // Locked invariant: every lifeform carries one elemental crystal it drops as
+            // a powerup on death (mass conserved). EnsureElementalCrystal uses the prefab's
+            // authored crystal if present (validator-enforced fast path) or provisions one;
+            // the sealed Fauna.Die drops it on any death path (predation / forager starvation).
+            crystal = LifeFormCrystal.EnsureElementalCrystal(this);
+
             currentVelocity = transform.forward * Random.Range(minSpeed, Mathf.Max(minSpeed, maxSpeed));
             float initialDelay = normalizedIndex * behaviorUpdateRate;
             StartCoroutine(CalculateBehaviorCoroutine(initialDelay));
@@ -307,7 +313,7 @@ namespace CosmicShore.Gameplay
             desiredRotation = SafeLookRotation.TryGet(currentVelocity, out var desiredRot, this) ? desiredRot : transform.rotation;
         }
 
-        protected override void Die(string killerName = "")
+        protected override void OnDeath(string killerName = "")
         {
             isKilled = true;
             StopAllCoroutines();
diff --git a/Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs b/Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs
index b6a1912f9..f4a461355 100644
--- a/Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs
+++ b/Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs
@@ -233,10 +233,37 @@ namespace CosmicShore.Gameplay
         }
 
         /// <summary>
-        /// Handle this fauna's death. Default is empty - override in subclasses
-        /// that have meaningful death behavior.
+        /// The elemental crystal this fauna conserves its mass into on death. Set by
+        /// concrete creature subclasses in Initialize via
+        /// <see cref="LifeFormCrystal.EnsureElementalCrystal"/>; null for manager /
+        /// composite-segment fauna that are not standalone lifeforms (their crystal is
+        /// owned at the whole-creature level).
         /// </summary>
-        protected virtual void Die(string killerName = "") { }
+        protected Crystal crystal;
+
+        /// <summary>
+        /// Death chokepoint — SEALED so no fauna can die without conserving its mass.
+        /// Every death path (starvation, <see cref="Predated"/>) routes here; it drops
+        /// the elemental crystal (the locked "every lifeform drops one elemental crystal
+        /// on death, mass is conserved" invariant — the creature does not just vanish)
+        /// and then runs subclass removal via <see cref="OnDeath"/>. ActivateCrystal
+        /// reparents the crystal to the cell, so it survives this object's destruction
+        /// as a collectible powerup.
+        /// </summary>
+        protected void Die(string killerName = "")
+        {
+            if (crystal && crystal.gameObject && crystal.gameObject.activeInHierarchy)
+                crystal.ActivateCrystal();
+            OnDeath(killerName);
+        }
+
+        /// <summary>
+        /// Subclass death behavior (manager removal / destroy / worm-splitting). Override
+        /// THIS, not <see cref="Die"/> — the crystal drop is sealed into Die so the mass-
+        /// conservation invariant cannot be bypassed by a subclass. Default is empty so
+        /// managers and stubs don't need to throw NotImplementedException.
+        /// </summary>
```

</details>

### `b69bfe4e4` — docs(ecology): rebase the master plan onto the bleeding-edge foundation

_Claude, 2026-06-11 21:50:20 +0000_

```text
The "ground-up reconsideration": epic-darwin was a 148-commit-stale fork
reinventing PrismSpatialIndex. Record the new reality and retarget the plan.

- §2 / §6: food web (predator/herbivore) + reproduction are now DONE (adopted
  from bleeding-edge), not "unstarted". PrismSpatialIndex is the spatial backbone.
- §4: the collider contract now names PrismSpatialIndex (QuerySphere / TryReserve)
  as the adopted backbone; fauna-query migration is the remaining route to budget.
- §10 (RESUME HERE): replace the obsolete collider-budget P1 plan (superseded by
  PrismSpatialIndex) with the invariant re-assertion plan — the three LOCKED
  invariants bleeding-edge drifted from: (1) mass-conserving crystal drop ✅ done,
  (2) no-domain-asymmetry spawn (exclude flags still true), (3) volume-as-spine
  (foundation phase is count-driven; keep the cleaner 3-phase enum, key it off
  Cell.LiveVolume). Plus the budget remainder: fauna queries -> QuerySphere,
  proximity collider-LOD, telemetry overlay.
```

```text
 Docs/ECOSYSTEM_MASTERPLAN.md | 138 ++++++++++++++++++++++++++++++++++++++++++++++---------------------------
 1 file changed, 88 insertions(+), 50 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 176 lines)</summary>

```diff
diff --git a/Docs/ECOSYSTEM_MASTERPLAN.md b/Docs/ECOSYSTEM_MASTERPLAN.md
index 02d6b6884..c7106e7a6 100644
--- a/Docs/ECOSYSTEM_MASTERPLAN.md
+++ b/Docs/ECOSYSTEM_MASTERPLAN.md
@@ -28,14 +28,19 @@ invariants live in `CLAUDE.md ▸ Ecosystem Design Principles`. This doc is the
 
 ## 2. Where we are (grounded, June 2026)
 
-Mechanics solid: the **volume spine** (`Cell.LiveVolume` → phase, hysteresis), **flora**
-plant/grow gates, **fauna** controlling-color spawn + prey-linked **starvation → wither-to-
-crystal**, **consumption** as the cell's down-force, **crystals → elemental powerups**, and the
-**lifeform-crystal invariant** (`LifeFormCrystal`: every lifeform drops one elemental crystal).
-
-Open: roadmap steps 2–7 unstarted (seams ready); ecology wired into only ~2–3 of ~11 modes; no
-collider budget; no heredity/reproduction/evolution. **The architecture is ready; the work is
-the food web, the genome, the budget, and the wiring.**
+Mechanics solid (foundation adopted from `bleeding-edge`, §10): the **`PrismSpatialIndex`**
+collider-light spatial backbone, a two-tier **predator/herbivore Lotka–Volterra food web**,
+**fauna reproduction** (feeds→births; the spawner is now a seeder), **Boid foragers**, the
+sealed **mass-conserving crystal drop** on the fauna death path, **crystals → elemental
+powerups**, and the **lifeform-crystal invariant** (`LifeFormCrystal` + validator: every
+lifeform carries/drops one elemental crystal).
+
+Open: three LOCKED invariants still to re-assert on the adopted foundation — **no-domain-
+asymmetry spawn**, **volume as the spine** (foundation phase is count-driven), and authoring
+crystals on fauna prefabs (§10 items 2–3). Then: collider budget (telemetry + collider-LOD +
+fauna-queries-on-the-index), the **genome/heredity** (the evolution substrate), and wiring
+ecology into the ~9 bare modes. **The food web is now real; the remaining work is the invariant
+re-assertions, the budget, the genome, and the wiring.**
 
 ---
 
@@ -97,16 +102,19 @@ result even for dedicated ALife systems.
 ## 4. The performance contract — colliders below threshold
 
 The hard constraint. Current reality: ~3–4k active `BoxCollider`s per full cell, uncapped.
-The Burst `BlockDensityGrid` (per-domain spatial grid) and `PrismAOERegistry` already answer
-spatial queries **without colliders** — so the budget is reachable. The contract:
+**`PrismSpatialIndex`** (the adopted backbone — 16B-hot data, 8m-bucket
+`NativeParallelMultiHashMap`; `QuerySphere`/`IsAnyPrismWithin`/`TryReserve`) and the per-domain
+`BlockDensityGrid` already answer spatial queries **without colliders** — so the budget is
+reachable; the remaining job is to route fauna behavior through them. The contract:
 
 - **`ColliderBudget` (new):** a configurable per-cell ceiling on active prism colliders
   (default to be tuned, target ≤ ~1,500). Exposed in `CellConfigDataSO`.
 - **Collider-LOD by phase:** once a cell reaches **Frozen**, prism colliders are *disabled*
   (growth has stopped; collisions are no longer gameplay-critical there). AOE + fauna queries
   run off the Burst grids/registry, which need no colliders. (~60–75% reduction.)
-- **Fauna queries off the grid:** replace `LightFauna`'s per-tick `Physics.OverlapSphere`
-  with a `BlockDensityGrid` neighborhood query (saves 2–5 ms/frame *and* drops collider reliance).
+- **Fauna queries off the index:** replace `LightFauna`/`Boid`'s per-tick
+  `Physics.OverlapSphereNonAlloc` with `PrismSpatialIndex.QuerySphere` (saves 2–5 ms/frame *and*
+  drops collider reliance — prerequisite for collider-LOD).
 - **Global active-prism budget:** recycle the oldest/farthest prism when the budget is
   exceeded (generalize the menu trail-cap). Crystals are already capped (16/cell wither).
 - **Telemetry overlay (new):** an in-editor HUD showing active colliders / prism count / phase
@@ -134,19 +142,23 @@ Make a Cell+ecology a *default* any scene gets, like a `ContainerScope`:
 ## 6. The roadmap (each phase: emergence · collider cost · tunability · gameplay · life-criterion)
 
 **Phase 1 — Foundations & budget** *(prereq for everything; mostly mechanical)*
-- Lock invariants (`CLAUDE.md`), this plan, the `ecology` skill. ✅ *(this turn)*
-- Build the **collider budget + LOD + grid-fauna-queries + telemetry** (§4). *Life: enables scale.*
+- Lock invariants (`CLAUDE.md`), this plan, the `ecology` skill. ✅
+- Adopt the `PrismSpatialIndex` spatial backbone (`QuerySphere`/`TryReserve`). ✅ *(merged from `bleeding-edge`)*
+- Re-assert the three locked invariants on the foundation (§10): crystal-drop ✅, no-asymmetry
+  spawn, volume spine. *Life: keeps mass conservation + the credible-alife economy intact.*
+- Build the **collider budget + LOD + index-fauna-queries + telemetry** (§4). *Life: enables scale.*
 - **`LifeformBootstrap`** + wire ecology into the ~9 bare modes; intensity→biome selection.
 
-**Phase 2 — The food web (Lotka–Volterra)** *(roadmap step 2)*
-- `FaunaConfigurationSO.SubType` (Herbivore/Predator); **diet = "what counts as prey"** in
-  `Fauna.ResolveGoal`/consume; **two-tier starvation** (herbivores↔flora, predators↔herbivores).
-- *Emergence:* genuine population oscillation. *Life:* metabolism + homeostasis. *Collider:* neutral
-  (reuses consume path). *Gameplay:* predators players can bait; prey blooms to harvest.
+**Phase 2 — The food web (Lotka–Volterra)** ✅ *(adopted from `bleeding-edge`, §10)*
+- `Fauna.diet` (Herbivore/Predator); **diet = "what counts as prey"** (herbivores eat opposing
+  prism mass, predators hunt herbivore fauna via `Cell.LiveFauna`); **two-tier starvation** +
+  post-spawn predation-immunity window. *Emergence:* genuine population oscillation. *Life:*
+  metabolism + homeostasis. *Gameplay:* predators players can bait; prey blooms to harvest.
 
```

</details>

### `47121768a` — feat(ecology): restore extremity-first fauna wither (continuity — no pop-out)

_Claude, 2026-06-11 23:13:12 +0000_

```text
When we adopted bleeding-edge's foundation, its fauna death path just Destroy'd the
GameObject — creatures popped out of existence. That violates the platform-wide
continuity law (nothing appears/disappears instantly; everything grows/fades/
suctions/withers). Restore the wither lost in the merge, layered on the sealed
Fauna.Die crystal drop:

- LightFauna.OnDeath: collapse the body one spindle ring at a time, FARTHEST-from-
  centre first, via the same Spindle.ForceWither evaporation flora use. A shark's
  fins / a brittlestar's arms (geometrically the outermost rings) evaporate before
  the core body — emergent from geometry, no per-prefab special-casing. Falls back
  to suctioning HealthPrisms inward for spindle-less bodies. Removes the husk only
  after the body is gone. Freezes movement/behavior while withering (idempotent).
- Boid.OnDeath: shrink the body out over 0.4s instead of instant Destroy.
- LightFaunaDataSO.witherRingInterval (default 0.25s): tunable per-ring cadence.

The elemental crystal is already dropped by the sealed Fauna.Die before OnDeath, so
it grows in (ActivateCrystal's material lerp) as the body withers out — mass visibly
suctions from the dying creature into the crystal it leaves behind.

Collider-budget impact: neutral-to-positive — body colliders disappear AS the body
withers; no new persistent colliders; the crystal already existed.

Verification (human gate): play a cell with brittlestar/shark fauna; on starvation
and on predation the creature should wither inward (fins/arms first) and leave a
collectible elemental crystal — never blink out. Tune witherRingInterval on the
LightFaunaDataSO assets.
```

```text
 Assets/_Scripts/Controller/Environment/FloraAndFauna/Boid.cs          | 21 ++++++++++
 Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs    | 71 ++++++++++++++++++++++++++++++++-
 .../_Scripts/Controller/Environment/FloraAndFauna/LightFaunaDataSO.cs |  8 ++++
 3 files changed, 99 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 157 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/FloraAndFauna/Boid.cs b/Assets/_Scripts/Controller/Environment/FloraAndFauna/Boid.cs
index 3462ffc47..fc54df401 100644
--- a/Assets/_Scripts/Controller/Environment/FloraAndFauna/Boid.cs
+++ b/Assets/_Scripts/Controller/Environment/FloraAndFauna/Boid.cs
@@ -315,8 +315,29 @@ namespace CosmicShore.Gameplay
 
         protected override void OnDeath(string killerName = "")
         {
+            if (isKilled) return;
             isKilled = true;
             StopAllCoroutines();
+            // Continuity rule — nothing pops out of existence. The sealed Fauna.Die already
+            // dropped this boid's elemental crystal (mass conserved); shrink the body out
+            // (suction-like) instead of instantly destroying it, then remove the husk.
+            if (isActiveAndEnabled && gameObject.activeInHierarchy)
+                StartCoroutine(FadeOutAndRemove());
+            else
+                Destroy(gameObject);
+        }
+
+        IEnumerator FadeOutAndRemove()
+        {
+            Vector3 from = transform.localScale;
+            float t = 0f;
+            const float dur = 0.4f;
+            while (t < dur)
+            {
+                t += Time.deltaTime;
+                transform.localScale = Vector3.Lerp(from, Vector3.zero, t / dur);
+                yield return null;
+            }
             Destroy(gameObject);
         }
 
diff --git a/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs b/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs
index 179f24eaf..443f0276d 100644
--- a/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs
+++ b/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs
@@ -22,6 +22,10 @@ namespace CosmicShore.Gameplay
         private Vector3 desiredDirection;
         private Quaternion desiredRotation;
 
+        // True once death has begun the wither animation. Freezes movement/behavior so the
+        // husk withers in place instead of drifting, and makes the death path idempotent.
+        bool _withering;
+
         [HideInInspector] public float Phase;
 
         public LightFaunaManager LightFaunaManager { get; set; }
@@ -76,7 +80,25 @@ namespace CosmicShore.Gameplay
             StartCoroutine(UpdateBehaviorCoroutine());
         }
 
+        /// <summary>
+        /// Death = wither, never a pop. The sealed <see cref="Fauna.Die"/> has already dropped
+        /// this creature's elemental crystal (mass conserved); here the body withers from its
+        /// extremities inward so it FADES out of existence rather than vanishing — the
+        /// platform-wide continuity rule. Only the husk is removed, after the body is gone.
+        /// </summary>
         protected override void OnDeath(string killerName = "")
+        {
+            if (_withering) return;
+            _withering = true;
+            currentVelocity = Vector3.zero;
+
+            if (isActiveAndEnabled && gameObject.activeInHierarchy)
+                StartCoroutine(WitherCoroutine());
+            else
+                RemoveHusk(); // can't animate while inactive (scene teardown) — remove directly
+        }
+
+        void RemoveHusk()
         {
             if (LightFaunaManager)
                 LightFaunaManager.RemoveFauna(this);
@@ -84,6 +106,53 @@ namespace CosmicShore.Gameplay
                 Destroy(gameObject);
         }
 
+        /// <summary>
```

</details>

### `c22f66e3a` — docs: lock the continuity-of-existence platform law (nothing pops in/out)

_Claude, 2026-06-11 23:13:12 +0000_

```text
New fundamental, game-wide (not ecology-specific): nothing in Cosmic Shore may
instantly appear or disappear. Every entity — prisms, crystals, flora, fauna,
vessels, projectiles, UI — must grow/bloom/fade/suction/wither into and out of
existence over a visible transition. A bare Instantiate-then-show or Destroy of
anything the player can see is a bug. This is the law behind wither-to-crystal and
mass conservation, applied everywhere.

- CLAUDE.md: add it as the first locked law in Ecosystem Design Principles, marked
  PLATFORM-WIDE; strengthen the wither bullet (fins-before-body, sealed into Die).
- /ecology skill: add to the restate checklist so every ecology change confirms it.
- ECOSYSTEM_MASTERPLAN: add as a platform law above the five pillars; record the
  restored wither under §10 item 1.
```

```text
 .claude/skills/ecology/SKILL.md |  1 +
 CLAUDE.md                       | 16 +++++++++++++---
 Docs/ECOSYSTEM_MASTERPLAN.md    | 22 +++++++++++++++++-----
 3 files changed, 31 insertions(+), 8 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 83 lines)</summary>

```diff
diff --git a/.claude/skills/ecology/SKILL.md b/.claude/skills/ecology/SKILL.md
index ba5d1d0fb..1cacfb859 100644
--- a/.claude/skills/ecology/SKILL.md
+++ b/.claude/skills/ecology/SKILL.md
@@ -17,6 +17,7 @@ this protocol exists to prevent that. Follow it exactly.
 
 ## 2. Restate before you edit (this kills the #1 source of rework)
 In one or two lines, state which invariants the change touches and confirm it violates **none**:
+**continuity of existence** (nothing pops in/out — everything grows/fades/suctions/withers; PLATFORM-WIDE) ·
 no imposed death/decay/lifespan · no domain asymmetry (controlling-color spawn only) ·
 wither-to-crystal + mass conservation · volume is the spine (not count) · the lifeform→elemental-
 crystal invariant · territorial permanence (don't cull the dominant canopy) · endogenous
diff --git a/CLAUDE.md b/CLAUDE.md
index 2dbfb5a30..a4b7e8b07 100644
--- a/CLAUDE.md
+++ b/CLAUDE.md
@@ -15,13 +15,23 @@ They are a direct application of "Favor Emergent Systems / Don't cheat emergence
 not by accident — they are also what makes the system credible as artificial life (a scripted
 outcome is optimization, not life). Use the `/ecology` skill for any change here.
 
+- **Continuity of existence — nothing pops in or out (PLATFORM-WIDE LAW, all of Cosmic Shore).**
+  Nothing may *instantly* appear or disappear. Every entity — prisms, crystals, flora, fauna,
+  vessels, projectiles, even UI — must **grow / bloom / fade / suction / wither / evaporate** into
+  and out of existence over a visible transition. A bare `Instantiate`-then-show or `Destroy` of
+  anything the player can see is a bug. Spawns animate in (scale-from-zero / bloom); deaths animate
+  out (wither from the extremities inward, suction toward a point, or fade). This is *why*
+  starvation withers and mass is conserved — it is the same law applied to the ecosystem. It is not
+  ecology-specific: respect it everywhere.
 - **No imposed death.** No decay, lifespan, or fixed-period despawn timers. Populations are
   bounded by **consumption + starvation**, never an imposed clock. (Repeatedly rejected.)
 - **No domain asymmetry.** Fauna spawn in **one color — the cell's controlling color** — and
   hunt opposing mass. Never cross-domain / prey-weighted / per-domain-biased spawning.
-- **Starvation = wither-to-crystal.** A starving creature withers from its extremity spindles
-  inward and leaves a collectible elemental crystal — it does not vanish. **Mass is conserved**
-  (the "self-sustaining economy" that makes the system NASA-credible).
+- **Starvation = wither-to-crystal.** A starving (or predated) creature withers from its extremity
+  spindles inward — a shark's fins / a brittlestar's arms evaporate *before* the core body
+  (farthest-from-centre first, emergent from geometry) — and leaves a collectible elemental crystal.
+  It **does not vanish** (the continuity law above). **Mass is conserved** (the "self-sustaining
+  economy" that makes the system NASA-credible). Sealed into `Fauna.Die` so no fauna can bypass it.
 - **Volume is the spine.** Phase, dominant domain, prey, HUD all key off per-domain **VOLUME**
   (`Cell.LiveVolume`), not prism count. Count is a rare frenzy/perf backstop only.
 - **Every lifeform drops one elemental crystal** (Charge/Mass/Space/Time) as a powerup on death,
diff --git a/Docs/ECOSYSTEM_MASTERPLAN.md b/Docs/ECOSYSTEM_MASTERPLAN.md
index c7106e7a6..da299c6b2 100644
--- a/Docs/ECOSYSTEM_MASTERPLAN.md
+++ b/Docs/ECOSYSTEM_MASTERPLAN.md
@@ -13,6 +13,12 @@ invariants live in `CLAUDE.md ▸ Ecosystem Design Principles`. This doc is the
 
 ## 1. The five pillars (every feature must serve all five)
 
+> **Platform law (above the pillars): continuity of existence.** Nothing in Cosmic Shore ever
+> pops in or out — every entity (prisms, crystals, flora, fauna, vessels, projectiles, UI) must
+> grow / bloom / fade / suction / wither into and out of existence over a visible transition. A
+> bare `Instantiate`-then-show or `Destroy` of anything the player can see is a bug. This is the
+> *why* behind wither-to-crystal and mass conservation, applied game-wide.
+
 1. **Fundamental & emergent.** Behaviour arises from composing the fundamentals (Domain,
    Mass/prisms, Cells, Elementals, Flora & Fauna, Vessels) — never hard-coded outcomes.
 2. **Tunable & variable.** Every knob is SO-config; richness comes from *biome × intensity ×
@@ -250,11 +256,17 @@ old §10 collider-budget plan is **superseded by PrismSpatialIndex** for the spa
 locked CLAUDE.md section to hold the line). The remaining work is to **re-assert them as clean
 deltas on top of the adopted foundation** — not to rebuild anything:
 
-1. **Mass-conserving crystal drop on the fauna death path.** ✅ *(done — commit `79f6996d`)*
-   `bleeding-edge` fauna `Die()` just `Destroy`ed (vanished, no crystal). Sealed it into the
-   `Fauna` base: non-virtual `Die` drops the elemental crystal then calls a `protected virtual
-   OnDeath` hook — no subclass can die without conserving mass. `LightFauna`/`Boid` provision
-   the crystal in `Initialize` via `LifeFormCrystal.EnsureElementalCrystal`.
+1. **Mass-conserving crystal drop + wither (continuity) on the fauna death path.** ✅ *(done)*
+   `bleeding-edge` fauna `Die()` just `Destroy`ed — they **popped out of existence**, dropping no
+   crystal (violates both the mass-conservation invariant *and* the platform-wide **continuity
+   law**: nothing pops in/out). Fixed in two layers: (a) sealed the crystal drop into the `Fauna`
+   base — non-virtual `Die` drops the elemental crystal then calls a `protected virtual OnDeath`
+   hook, so no subclass can die without conserving mass; (b) restored the **extremity-first
+   wither** — `LightFauna.OnDeath` collapses spindle rings farthest-from-centre first (a shark's
+   fins / a brittlestar's arms evaporate before the core body — emergent from geometry, tunable
+   via `LightFaunaDataSO.witherRingInterval`), `Boid.OnDeath` shrinks out; both then remove the
+   spent husk. `LightFauna`/`Boid` provision the crystal in `Initialize` via
+   `LifeFormCrystal.EnsureElementalCrystal`.
```

</details>

_Also contains 2 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
