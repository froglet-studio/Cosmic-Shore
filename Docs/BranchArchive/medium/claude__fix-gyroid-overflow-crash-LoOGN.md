# Branch archive: `claude/fix-gyroid-overflow-crash-LoOGN`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-04-21 by Claude
- **Unmerged commits:** 6
- **Forked from:** `dfc0139bc` (2026-04-17, Merge pull request #486 from froglet-studio/claude/update-document-changes-mnm)
- **Tip:** `df28ae618`
- **Files touched (15):**
  - `Assets/_Models/Fauna/MassBrittlestarFauna.prefab`
  - `Assets/_Prefabs/FloraAndFauna/Populations/MassBrittlestarPopulation.prefab`
  - `Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Cell Spawn Profile.asset`
  - `Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Fauna Config Data.asset`
  - `Assets/_Scripts/Controller/Environment/Cell.cs`
  - `Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/LifeForm.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFaunaManager.cs`
  - `Assets/_Scripts/Controller/Environment/IntensityWiseLifeSpawner.cs`
  - `Assets/_Scripts/Controller/Environment/Prisms/MaterialPropertyAnimator.cs`
  - `Assets/_Scripts/Controller/Managers/PrismStateManager.cs`
  - `Assets/_Scripts/Data/Enums/CellAggressionLevel.cs`

### `0f1606091` — fix(cell): throttle gyroid growth with cell-aggression feedback loop

_Claude, 2026-04-17 19:31:43 +0000_

```text
Menu_Main runs the autopilot vessels indefinitely and the gyroid flora in
the cell would grow unboundedly, eventually crashing the game as prism
counts overwhelmed the CPU/GPU. Now the Cell buckets its live prism count
into a CellAggressionLevel (Calm/Elevated/Stressed/Critical) on a cheap
1.5s poll, and the ecosystem responds organically:

- Gyroid growth (AssembledFlora.Grow) scales itemsPerGrow down with
  aggression; Critical halts growth entirely so fauna can catch up.
- IntensityWiseLifeSpawner stretches flora cadence under stress and gates
  new flora seeds at Critical, while tightening fauna cadence so
  reinforcements arrive faster.
- LightFaunaManager maintains an aggression-scaled target population
  (capped at maxActiveFauna so cleanup never worsens the load),
  reinforcing on both interval and state-change events.
- LightFauna accelerates behavior cadence, widens consume radius, and
  speeds up when its host cell is stressed, so consumption rate rises
  with load. Fauna.goalUpdateInterval tightens so they relocate to the
  densest prism region faster.
- Cell.AddBlock/RemoveBlock (previously dormant) is now wired through
  LifeForm.AddHealthBlock/RemoveHealthBlock with HashSet dedup, which
  also activates the existing density-grid targeting used by
  GetExplosionTarget.

The existing SuctionGraph.shadergraph (used by ImplodingPrismMaterial on
PrismImplosion VFX) already gives fauna-consumed prisms the visual of
being sucked into the fauna, since block.Consume(fauna.transform, ...)
sets the shader's _Location to the fauna position — no shader wiring
required, just verified.
```

```text
 Assets/_Scripts/Controller/Environment/Cell.cs                        |  92 ++++++++++++++++++++++++++
 .../_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs   |  23 ++++++-
 Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs         |  19 +++++-
 Assets/_Scripts/Controller/Environment/FloraAndFauna/LifeForm.cs      |   8 +++
 Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs    |  38 +++++++++--
 .../Controller/Environment/FloraAndFauna/LightFaunaManager.cs         | 111 ++++++++++++++++++++++++++++----
 Assets/_Scripts/Controller/Environment/IntensityWiseLifeSpawner.cs    |  23 +++++++
 Assets/_Scripts/Data/Enums/CellAggressionLevel.cs                     |  16 +++++
 8 files changed, 309 insertions(+), 21 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 571 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/Cell.cs b/Assets/_Scripts/Controller/Environment/Cell.cs
index 29888f655..c278509a9 100644
--- a/Assets/_Scripts/Controller/Environment/Cell.cs
+++ b/Assets/_Scripts/Controller/Environment/Cell.cs
@@ -1,4 +1,6 @@
 // Cell.cs
+using System;
+using System.Collections;
 using System.Collections.Generic;
 using System.Linq;
 using CosmicShore.Data;
@@ -26,6 +28,16 @@ namespace CosmicShore.Gameplay
 
         [SerializeField] float nucleusScaleMultiplier = 1f;
 
+        [Header("Aggression Thresholds")]
+        [Tooltip("Live prism count at or above which the cell enters Elevated.")]
+        [SerializeField, Min(1)] int elevatedThreshold = 400;
+        [Tooltip("Live prism count at or above which the cell enters Stressed.")]
+        [SerializeField, Min(1)] int stressedThreshold = 900;
+        [Tooltip("Live prism count at or above which the cell enters Critical (growth halts).")]
+        [SerializeField, Min(1)] int criticalThreshold = 1500;
+        [Tooltip("Seconds between aggression level re-evaluations.")]
+        [SerializeField, Min(0.1f)] float aggressionPollInterval = 1.5f;
+
 
         CellConfigDataSO cellConfigData => runtime ? runtime.Config : null;
         GameObject membrane;
@@ -55,6 +67,25 @@ namespace CosmicShore.Gameplay
         ICellLifeSpawner activeSpawner;
         bool postInitilized = false;
 
+        // Aggression state. Incremented / decremented in O(1) by AddBlock / RemoveBlock
+        // so we never iterate density grids to measure pressure.
+        int _liveBlockCount;
+        CellAggressionLevel _aggressionLevel = CellAggressionLevel.Calm;
+        Coroutine _aggressionPollRoutine;
+
+        // Dedupes AddBlock/RemoveBlock across multiple call paths (flora binding,
+        // HealthPrism.Initialize, future direct callers) so _liveBlockCount stays accurate.
+        readonly HashSet<Prism> _registeredBlocks = new();
+
+        /// <summary>Current live prism count in this cell (enemy-tracked blocks).</summary>
+        public int LiveBlockCount => _liveBlockCount;
+
+        /// <summary>Bucketed stress level driven by <see cref="LiveBlockCount"/>.</summary>
+        public CellAggressionLevel AggressionLevel => _aggressionLevel;
+
+        /// <summary>Raised when <see cref="AggressionLevel"/> transitions. New level is the argument.</summary>
+        public event Action<CellAggressionLevel> OnAggressionChanged;
+
         void OnEnable()
         {
             // Clear stale config BEFORE subscribing to events.
@@ -113,6 +144,7 @@ namespace CosmicShore.Gameplay
             }
 
             StopSpawner();
+            StopAggressionPoll();
             runtime?.ResetRuntimeData();
         }
 
@@ -135,6 +167,10 @@ namespace CosmicShore.Gameplay
             AssignConfig();
             ResetVolumes();
 
+            _liveBlockCount = 0;
+            _aggressionLevel = CellAggressionLevel.Calm;
+            StartAggressionPoll();
+
             runtime.EnsureCellStats(ID);
             UpdateCellStats();
         }
@@ -187,6 +223,10 @@ namespace CosmicShore.Gameplay
             SpawnVisuals();
             ResetVolumes();
 
+            _liveBlockCount = 0;
+            _aggressionLevel = CellAggressionLevel.Calm;
+            StartAggressionPoll();
+
             UpdateCellStats();
         }
         
@@ -309,16 +349,68 @@ namespace CosmicShore.Gameplay
 
         public void AddBlock(Prism block)
         {
+            if (!block) return;
+            if (!_registeredBlocks.Add(block)) return; // already registered
+
             Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold };
             foreach (var t in teams)
                 if (t != block.Domain) countGrids[t].AddBlock(block);
+
+            _liveBlockCount++;
         }
 
         public void RemoveBlock(Prism block)
         {
+            if (!block) return;
+            if (!_registeredBlocks.Remove(block)) return; // wasn't tracked
+
             Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold };
             foreach (Domains t in teams)
                 if (t != block.Domain) countGrids[t].RemoveBlock(block);
+
+            if (_liveBlockCount > 0) _liveBlockCount--;
+        }
+
+        CellAggressionLevel BucketForCount(int count)
+        {
+            if (count >= criticalThreshold) return CellAggressionLevel.Critical;
+            if (count >= stressedThreshold) return CellAggressionLevel.Stressed;
+            if (count >= elevatedThreshold) return CellAggressionLevel.Elevated;
+            return CellAggressionLevel.Calm;
+        }
+
+        IEnumerator PollAggressionLevel()
+        {
+            var wait = new WaitForSeconds(Mathf.Max(0.1f, aggressionPollInterval));
+            while (true)
+            {
+                var next = BucketForCount(_liveBlockCount);
+                if (next != _aggressionLevel)
+                {
+                    _aggressionLevel = next;
+                    try { OnAggressionChanged?.Invoke(next); }
+                    catch (Exception e) { CSDebug.LogError($"[Cell {ID}] OnAggressionChanged listener threw: {e}"); }
+                }
+                yield return wait;
+            }
+        }
+
+        void StartAggressionPoll()
+        {
+            StopAggressionPoll();
+            _aggressionPollRoutine = StartCoroutine(PollAggressionLevel());
+        }
+
+        void StopAggressionPoll()
+        {
+            if (_aggressionPollRoutine != null)
+            {
+                StopCoroutine(_aggressionPollRoutine);
+                _aggressionPollRoutine = null;
+            }
+            _aggressionLevel = CellAggressionLevel.Calm;
+            _liveBlockCount = 0;
+            _registeredBlocks.Clear();
```

</details>

### `bd8b881b9` — refactor(cell): rework regulation to staggered thresholds with hysteresis

_Claude, 2026-04-19 19:12:21 +0000_

```text
Rework the cell ecosystem regulation to match the spec: a single prism
count drives five independent gates along a staggered axis, each with
rising/falling hysteresis to buffer against thrashing at the boundary.

Thresholds (count rising → behavior):
  0     flora begin spawning with random domains (default-on gate)
  1000  fauna begin spawning in the controlling color, aggression L0
  4000  flora stop planting
  8000  fauna aggression L1 (target nearest opposing-color centroid)
  10000 flora stop growing
  15000 fauna aggression L2 berserk (nearest centroid any color, no
        friendly avoidance, forward-looking danger-prism immunity)

Per-gate rising/falling pairs (defaults 750/3500/7000/9000/13000) allow
the cell to relax cleanly back down without chattering across the
boundary.

Cell exposes explicit state:
  LiveBlockCount, AggressionLevel, FloraPlantingEnabled,
  FloraGrowingEnabled, FaunaSpawningEnabled, ControllingDomain,
  GetPrimaryCentroid()

IntensityWiseLifeSpawner
- Drops the once-at-startup AllowSpawn gate that permanently disabled
  fauna types when the first roll failed (this is why Menu_Main never
  saw fauna spawn — a single failed probability roll silenced them).
- Spawns fauna in ControllingDomain instead of a random domain.
- Falls back to cell.transform.position when no crystal exists, instead
  of silently skipping the spawn (another Menu_Main fauna silence).
- Gates flora loop on FloraPlantingEnabled, fauna loop on
  FaunaSpawningEnabled; each re-evaluates per cycle, not per startup.

AssembledFlora.Grow() now simply returns when FloraGrowingEnabled is
false (gate handles the halt with hysteresis, replacing the previous
bucketed throttle).

Fauna.ResolveGoal() picks its goal based on CellAggressionLevel:
  Level0 → crystal, Level1 → opposing-color centroid, Level2 → primary
  centroid (any color). LightFauna skips friendly separation at Level 2.

LightFaunaManager + LightFauna multiplier tables collapsed from 4 levels
to 3. Tuning: Level 0 keeps the world feeling alive; Level 2 is intended
to be rare (~1 in 10 matches) and aggressive enough to drive the count
back down under the rising thresholds.

Added CellLifeSpawnerBase.SpawnFaunaWithDomain(host, prefab, goal,
domain) for explicit-domain spawns.
```

```text
 Assets/_Scripts/Controller/Environment/Cell.cs                        | 176 +++++++++++++++++++++++++++-----
 Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs         |  16 +++
 .../_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs   |  27 ++---
 Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs         |  41 ++++++--
 Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs    |  33 ++++--
 .../Controller/Environment/FloraAndFauna/LightFaunaManager.cs         |   4 +-
 Assets/_Scripts/Controller/Environment/IntensityWiseLifeSpawner.cs    | 139 +++++++++++++------------
 Assets/_Scripts/Data/Enums/CellAggressionLevel.cs                     |  22 ++--
 8 files changed, 324 insertions(+), 134 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 760 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/Cell.cs b/Assets/_Scripts/Controller/Environment/Cell.cs
index c278509a9..70eca4321 100644
--- a/Assets/_Scripts/Controller/Environment/Cell.cs
+++ b/Assets/_Scripts/Controller/Environment/Cell.cs
@@ -28,14 +28,44 @@ namespace CosmicShore.Gameplay
 
         [SerializeField] float nucleusScaleMultiplier = 1f;
 
-        [Header("Aggression Thresholds")]
-        [Tooltip("Live prism count at or above which the cell enters Elevated.")]
-        [SerializeField, Min(1)] int elevatedThreshold = 400;
-        [Tooltip("Live prism count at or above which the cell enters Stressed.")]
-        [SerializeField, Min(1)] int stressedThreshold = 900;
-        [Tooltip("Live prism count at or above which the cell enters Critical (growth halts).")]
-        [SerializeField, Min(1)] int criticalThreshold = 1500;
-        [Tooltip("Seconds between aggression level re-evaluations.")]
+        // ---------------------------------------------------------------------
+        // Regulation thresholds with hysteresis.
+        //
+        // The cell tracks a single prism count (_liveBlockCount) and drives five
+        // independent regulation gates along that axis. The rising and falling
+        // values differ to buffer against thrashing at the boundary:
+        //
+        //   0          - flora begin spawning with random domains   (default-on)
+        //   1000       - fauna begin spawning in the controlling color (L0)
+        //   4000       - flora stop planting
+        //   8000       - fauna escalate to L1 (opposing-centroid seeking)
+        //   10000      - flora stop growing
+        //   15000      - fauna escalate to L2 (any centroid, no friendly avoidance)
+        //
+        // Tune per-biome via CellConfigDataSO later if needed; for now these live
+        // on the Cell so per-scene tuning is possible.
+        // ---------------------------------------------------------------------
+
+        [System.Serializable]
+        public struct PrismThreshold
+        {
+            [Min(0)] public int Rising;
+            [Min(0)] public int Falling;
+        }
+
+        [Header("Regulation Thresholds (prism count, hysteresis)")]
+        [Tooltip("Count at or above which fauna begin spawning (in controlling color).")]
+        [SerializeField] PrismThreshold faunaSpawnStart = new() { Rising = 1000, Falling = 750 };
+        [Tooltip("Count at or above which flora stop planting new instances.")]
+        [SerializeField] PrismThreshold floraPlantEnd = new() { Rising = 4000, Falling = 3500 };
+        [Tooltip("Count at or above which fauna escalate to aggression Level 1.")]
+        [SerializeField] PrismThreshold faunaAggressionL1 = new() { Rising = 8000, Falling = 7000 };
+        [Tooltip("Count at or above which flora stop growing existing instances.")]
+        [SerializeField] PrismThreshold floraGrowEnd = new() { Rising = 10000, Falling = 9000 };
+        [Tooltip("Count at or above which fauna escalate to aggression Level 2 (berserk).")]
+        [SerializeField] PrismThreshold faunaAggressionL2 = new() { Rising = 15000, Falling = 13000 };
+
+        [Tooltip("Seconds between regulation state re-evaluations.")]
         [SerializeField, Min(0.1f)] float aggressionPollInterval = 1.5f;
 
 
@@ -70,22 +100,59 @@ namespace CosmicShore.Gameplay
         // Aggression state. Incremented / decremented in O(1) by AddBlock / RemoveBlock
         // so we never iterate density grids to measure pressure.
         int _liveBlockCount;
-        CellAggressionLevel _aggressionLevel = CellAggressionLevel.Calm;
+        CellAggressionLevel _aggressionLevel = CellAggressionLevel.Level0;
+        bool _floraPlantingEnabled = true;   // default on; gated down by rising threshold
+        bool _floraGrowingEnabled = true;    // default on; gated down by rising threshold
+        bool _faunaSpawningEnabled = false;  // default off; gated up by rising threshold
         Coroutine _aggressionPollRoutine;
 
         // Dedupes AddBlock/RemoveBlock across multiple call paths (flora binding,
         // HealthPrism.Initialize, future direct callers) so _liveBlockCount stays accurate.
         readonly HashSet<Prism> _registeredBlocks = new();
 
-        /// <summary>Current live prism count in this cell (enemy-tracked blocks).</summary>
+        /// <summary>Current live prism count in this cell.</summary>
         public int LiveBlockCount => _liveBlockCount;
 
-        /// <summary>Bucketed stress level driven by <see cref="LiveBlockCount"/>.</summary>
+        /// <summary>Current fauna aggression level derived from <see cref="LiveBlockCount"/> with hysteresis.</summary>
         public CellAggressionLevel AggressionLevel => _aggressionLevel;
 
+        /// <summary>Is the cell currently allowing flora to plant new instances?</summary>
+        public bool FloraPlantingEnabled => _floraPlantingEnabled;
+
+        /// <summary>Is the cell currently allowing existing flora to grow new prisms?</summary>
+        public bool FloraGrowingEnabled => _floraGrowingEnabled;
+
+        /// <summary>Is the cell currently allowing new fauna to spawn?</summary>
+        public bool FaunaSpawningEnabled => _faunaSpawningEnabled;
+
         /// <summary>Raised when <see cref="AggressionLevel"/> transitions. New level is the argument.</summary>
         public event Action<CellAggressionLevel> OnAggressionChanged;
 
+        /// <summary>
+        /// Resolves the "controlling color" for fauna spawns. Falls back to local player
+        /// domain (useful in Menu_Main where there is no scored controlling team), then
+        /// to Jade as last resort.
+        /// </summary>
+        public Domains ControllingDomain
+        {
+            get
+            {
+                if (gameData != null)
+                {
+                    var top = gameData.GetControllingTeamStatsBasedOnVolumeRemaining();
+                    if (top.Team != Domains.None && top.Team != Domains.Unassigned && top.Volume > 0f)
+                        return top.Team;
+
+                    var local = gameData.LocalRoundStats?.Domain
+                                ?? gameData.LocalPlayer?.Domain
+                                ?? Domains.Unassigned;
+                    if (local != Domains.None && local != Domains.Unassigned)
+                        return local;
+                }
+                return Domains.Jade;
+            }
+        }
+
         void OnEnable()
         {
             // Clear stale config BEFORE subscribing to events.
@@ -167,8 +234,7 @@ namespace CosmicShore.Gameplay
             AssignConfig();
             ResetVolumes();
 
-            _liveBlockCount = 0;
-            _aggressionLevel = CellAggressionLevel.Calm;
+            ResetRegulationState();
             StartAggressionPoll();
 
             runtime.EnsureCellStats(ID);
@@ -223,8 +289,7 @@ namespace CosmicShore.Gameplay
             SpawnVisuals();
             ResetVolumes();
 
-            _liveBlockCount = 0;
-            _aggressionLevel = CellAggressionLevel.Calm;
+            ResetRegulationState();
             StartAggressionPoll();
 
             UpdateCellStats();
@@ -371,12 +436,34 @@ namespace CosmicShore.Gameplay
             if (_liveBlockCount > 0) _liveBlockCount--;
         }
 
-        CellAggressionLevel BucketForCount(int count)
+        // Hysteresis rule: when a gate is OPEN (default-on state), it stays open until
+        // count crosses the Rising threshold; once closed, it reopens only after count
+        // drops below the Falling threshold. Inverted for default-off gates.
+        static bool EvaluateDefaultOnGate(bool currentlyOpen, int count, PrismThreshold t)
+        {
```

</details>

### `7d4214a10` — fix(cell): wire Blob Cell fauna data and harden spawn diagnostics

_Claude, 2026-04-20 18:13:17 +0000_

```text
The reason Menu_Main saw flora but never fauna was entirely data:

  Blob Cell Spawn Profile.asset:  SupportedFaunas: []
  Blob Fauna Config Data.asset:   FaunaPrefab: null, SpawnProbability: 0
  MassBrittlestarPopulation.prefab: cellData: <orphaned GUID>
  MassBrittlestarFauna.prefab:      cellData: <orphaned GUID>

The fauna pipeline couldn't iterate (empty list), couldn't probe (zero
probability), couldn't find its prefab, and even if it had, the spawned
Fauna had a broken CellRuntimeDataSO reference that would NRE the moment
it tried to read cell.AggressionLevel or cell.transform.

Data fixes:
- Blob Fauna Config Data.asset wired to MassBrittlestarPopulation
  (LightFaunaManager component) with SpawnProbability=1.
- Blob Cell Spawn Profile.asset SupportedFaunas now contains the fauna
  config so IntensityWiseLifeSpawner actually iterates it.
- MassBrittlestarPopulation.prefab and MassBrittlestarFauna.prefab
  cellData references repointed from the orphaned GUID
  16d80244... (no asset in project) to the real Runtime Cell Data
  asset 8d4e8398... — the same SO the scene Cell wires into its
  `runtime` field — so fauna see the live cell at runtime.

Code fixes:
- IntensityWiseLifeSpawner now logs loud warnings at startup when
  SupportedFaunas is empty, a config is null, a prefab is missing, or
  SpawnProbability is <= 0. Any one of these would have surfaced the
  wiring gap during the first playtest instead of silently never
  spawning fauna across multiple sessions.
- LightFauna.UpdateBehavior guards against null cell in the goal
  fallback path, so unwired cellData doesn't NRE the coroutine.
```

```text
 Assets/_Models/Fauna/MassBrittlestarFauna.prefab                           |  2 +-
 Assets/_Prefabs/FloraAndFauna/Populations/MassBrittlestarPopulation.prefab |  6 +++---
 Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Cell Spawn Profile.asset     |  3 ++-
 Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Fauna Config Data.asset      |  6 +++---
 Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs         |  7 ++++++-
 Assets/_Scripts/Controller/Environment/IntensityWiseLifeSpawner.cs         | 21 ++++++++++++++++++++-
 6 files changed, 35 insertions(+), 10 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs b/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs
index a25733a39..10b39de95 100644
--- a/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs
+++ b/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs
@@ -100,7 +100,12 @@ namespace CosmicShore.Gameplay
 
             if (!IsFinite(Goal) || Goal.sqrMagnitude < 0.001f)
             {
-                Goal = cellData && cellData.CrystalTransform ? cellData.CrystalTransform.position : cell.transform.position;
+                if (cellData && cellData.CrystalTransform)
+                    Goal = cellData.CrystalTransform.position;
+                else if (cell != null)
+                    Goal = cell.transform.position;
+                else
+                    Goal = transform.position;
             }
 
             Vector3 goalDirection = (Goal - transform.position).normalized;
diff --git a/Assets/_Scripts/Controller/Environment/IntensityWiseLifeSpawner.cs b/Assets/_Scripts/Controller/Environment/IntensityWiseLifeSpawner.cs
index 7279ccf7d..b3d24752e 100644
--- a/Assets/_Scripts/Controller/Environment/IntensityWiseLifeSpawner.cs
+++ b/Assets/_Scripts/Controller/Environment/IntensityWiseLifeSpawner.cs
@@ -97,11 +97,30 @@ namespace CosmicShore.Gameplay
             var spawnProfile = config.SpawnProfile;
             if (!spawnProfile) yield break;
             if (spawnProfile.SupportedFaunas is not { Count: > 0 })
+            {
+                // Fail loud: empty SupportedFaunas is almost always a data-wiring mistake.
+                // Without this warning fauna silently never spawn (as happened in Menu_Main).
+                CSDebug.LogWarning($"[IntensityWiseLifeSpawner] '{config.name}' SpawnProfile has no SupportedFaunas wired; fauna will never spawn in this cell.");
                 yield break;
+            }
 
             foreach (var faunaCfg in spawnProfile.SupportedFaunas)
             {
-                if (!faunaCfg || !faunaCfg.FaunaPrefab) continue;
+                if (!faunaCfg)
+                {
+                    CSDebug.LogWarning($"[IntensityWiseLifeSpawner] '{config.name}' has a null FaunaConfigurationSO entry in SupportedFaunas.");
+                    continue;
+                }
+                if (!faunaCfg.FaunaPrefab)
+                {
+                    CSDebug.LogWarning($"[IntensityWiseLifeSpawner] FaunaConfiguration '{faunaCfg.name}' has no FaunaPrefab; skipping.");
+                    continue;
+                }
+                if (faunaCfg.SpawnProbability <= 0f)
+                {
+                    CSDebug.LogWarning($"[IntensityWiseLifeSpawner] FaunaConfiguration '{faunaCfg.name}' has SpawnProbability <= 0; fauna of this type will never roll true. Set to 1 to always spawn.");
+                    continue;
+                }
                 Track(host, SpawnFaunaTypeLoop(host, runtime, spawnProfile, faunaCfg));
             }
         }
```

</details>

### `45330bf25` — fix(cell): fall back to cell anchor when density grid is empty

_Claude, 2026-04-20 18:56:25 +0000_

```text
Fauna clustered in the world-space -x/-y/-z direction because
BlockDensityGrid.FindDensestRegion returns the grid's bottom-left-back
corner (origin - totalLength/2 = roughly (-500,-500,-500)) when no
blocks are registered in the queried grid. At aggression Level 0 this
isn't hit (Fauna.ResolveGoal returns the crystal directly), but at
Level 1 / Level 2 the fauna query GetExplosionTarget / GetPrimaryCentroid
and end up pulled toward an empty-grid corner that has nothing to
consume.

Cell.GetExplosionTarget and Cell.GetPrimaryCentroid now detect the
empty-grid case by reading back the density at the returned point
(cheap, no extra scan) and fall back to the local crystal position
(or cell transform if no crystal) when the grid is empty. Fauna still
swarm real density when it exists; when it doesn't, they loiter near
the cell instead of drifting to the world corner.
```

```text
 Assets/_Scripts/Controller/Environment/Cell.cs | 34 +++++++++++++++++++++++++++++++---
 1 file changed, 31 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/Cell.cs b/Assets/_Scripts/Controller/Environment/Cell.cs
index 70eca4321..801181399 100644
--- a/Assets/_Scripts/Controller/Environment/Cell.cs
+++ b/Assets/_Scripts/Controller/Environment/Cell.cs
@@ -515,7 +515,21 @@ namespace CosmicShore.Gameplay
             ResetRegulationState();
         }
 
-        public Vector3 GetExplosionTarget(Domains domain) => countGrids[domain].FindDensestRegion();
+        public Vector3 GetExplosionTarget(Domains domain)
+        {
+            if (!countGrids.TryGetValue(domain, out var grid) || grid == null)
+                return GetCellAnchorPosition();
+
+            var region = grid.FindDensestRegion();
+            // Empty grids return the grid's bottom-left-back corner (origin minus
+            // totalLength/2), which pulls every fauna that queries an empty grid
+            // toward the world-space -x/-y/-z corner and causes them to cluster
+            // far from any actual prism. Guard by reading back the density at the
+            // returned point; if it's zero the grid had nothing to find.
+            if (grid.GetDensityAtPosition(region) <= 0)
+                return GetCellAnchorPosition();
+            return region;
+        }
 
         /// <summary>
         /// Densest region across all domain grids (color-agnostic). Used by fauna at
@@ -525,8 +539,8 @@ namespace CosmicShore.Gameplay
         /// </summary>
         public Vector3 GetPrimaryCentroid()
         {
-            Vector3 best = transform.position;
-            int bestDensity = -1;
+            Vector3 best = GetCellAnchorPosition();
+            int bestDensity = 0;
             foreach (var kvp in countGrids)
             {
                 var grid = kvp.Value;
@@ -540,9 +554,23 @@ namespace CosmicShore.Gameplay
                     best = region;
                 }
             }
+            // If no grid had any density, bestDensity stays 0 and we return the cell
+            // anchor (crystal or cell transform) — keeping fauna near the cell
+            // instead of drifting toward the empty-grid corner fallback.
             return best;
         }
 
+        /// <summary>
+        /// Safe fallback position for goal resolution when density grids are empty:
+        /// the local crystal if one exists, otherwise the cell's own transform.
+        /// </summary>
+        Vector3 GetCellAnchorPosition()
+        {
+            if (runtime != null && runtime.CrystalTransform)
+                return runtime.CrystalTransform.position;
+            return transform.position;
+        }
+
         public bool ContainsPosition(Vector3 position)
         {
             if (membrane is null) return false;
```

</details>

### `d46cde949` — fix(fauna): grow brittlestar body and spread orbit around crystal

_Claude, 2026-04-20 20:09:37 +0000_

```text
Two visible artifacts from the last playtest:

1. Brittlestar bodies invisible / "health prisms at scale 0,0,0".
   MassBrittlestarFauna.prefab composes its body out of nested
   DynamicHealthBlock prefab instances. Those HealthPrisms only scale up
   from 0 when their Prism.Initialize is called — which LifeForm does
   automatically via BindEmbeddedParts, but LightFauna (which extends
   Fauna, not LifeForm) never did. LightFauna.Initialize now walks
   GetComponentsInChildren<HealthPrism> and calls ChangeTeam(domain) +
   Initialize("FaunaPrefab") on each. LifeForm is intentionally not set
   on the body prisms so they don't register with Cell as consumable
   targets.

2. Origin "regenerate + suction" depletion loop. The Squirrel's
   VesselExplosionByCrystalEffectSO spawns a ring of prisms every time a
   Squirrel vessel contacts the crystal (0.15s cooldown). When fauna at
   Level 0 all target the crystal position, they converge on the origin
   and consume each fresh ring as it spawns — correct behavior, but
   visually it looks like a single pulsing depletion zone. Fauna now
   pick a stable per-instance offset on a sphere of radius
   goalOrbitRadius (default 60) at Start and add it to the Level 0 / L1
   goal. The pack orbits the crystal instead of parking on it, so
   cleanup distributes around the cell. Level 2 still converges tightly
   — that's the berserk-cleanup semantic.
```

```text
 Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs      | 25 +++++++++++++++++++++----
 Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs | 18 ++++++++++++++++++
 2 files changed, 39 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs b/Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs
index fc1d063f6..248462df8 100644
--- a/Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs
+++ b/Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs
@@ -25,8 +25,18 @@ namespace CosmicShore.Gameplay
         [Tooltip("Multiplier applied to goalUpdateInterval per CellAggressionLevel: [Level0, Level1, Level2]. " +
                  "Lower values = faster relocation to targeted region under stress.")]
         [SerializeField] float[] goalUpdateIntervalByAggression = { 1f, 0.55f, 0.25f };
+        [Tooltip("Each fauna picks a stable random offset on a sphere of this radius and adds it " +
+                 "to its resolved goal. Prevents the whole pack from converging onto a single point " +
+                 "(e.g. the crystal at origin), which otherwise creates a visible depletion zone " +
+                 "where fauna repeatedly consume spawn-on-impact prism configurations.")]
+        [SerializeField] float goalOrbitRadius = 60f;
         public Vector3 Goal;
 
+        // Stable per-instance offset so each fauna orbits its resolved goal at a different
+        // point. Seeded once at Start from the fauna's domain + instance id so the spread
+        // is deterministic per spawn but varied across the pack.
+        Vector3 _goalOrbitOffset;
+
         // --- ILifeFormEntity ---
         public Domains Domain => domain;
         public GameObject GetGameObject() => gameObject;
@@ -38,6 +48,8 @@ namespace CosmicShore.Gameplay
             if (domain == Domains.Unassigned)
                 CSDebug.LogWarning($"{name}: Population domain is Unassigned. Assign it before spawning, or set it on the prefab.");
 
+            _goalOrbitOffset = Random.onUnitSphere * Mathf.Max(0f, goalOrbitRadius);
+
             StartCoroutine(UpdateGoalCoroutine());
         }
 
@@ -75,6 +87,10 @@ namespace CosmicShore.Gameplay
         ///   Level0 - head toward the cell's crystal
         ///   Level1 - head toward the nearest opposing-color centroid
         ///   Level2 - head toward the nearest centroid of ANY color
+        ///
+        /// A per-instance orbit offset is added so the pack spreads around the target
+        /// instead of piling onto a single point. Level 2 skips the offset — at berserk
+        /// aggression we want tight convergence onto the densest cleanup target.
         /// </summary>
         protected virtual Vector3 ResolveGoal()
         {
@@ -88,13 +104,14 @@ namespace CosmicShore.Gameplay
                 case CellAggressionLevel.Level1:
                     // GetExplosionTarget(domain) finds the densest region of blocks
                     // that are NOT of this domain - the nearest opposing centroid.
-                    return cell.GetExplosionTarget(domain);
+                    return cell.GetExplosionTarget(domain) + _goalOrbitOffset;
 
                 case CellAggressionLevel.Level0:
                 default:
-                    if (cellData && cellData.CrystalTransform)
-                        return cellData.CrystalTransform.position;
-                    return cell.transform.position;
+                    Vector3 anchor = cellData && cellData.CrystalTransform
+                        ? cellData.CrystalTransform.position
+                        : cell.transform.position;
+                    return anchor + _goalOrbitOffset;
             }
         }
 
diff --git a/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs b/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs
index 10b39de95..1f5d2469f 100644
--- a/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs
+++ b/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs
@@ -34,6 +34,24 @@ namespace CosmicShore.Gameplay
                 return;
             }
 
+            // The body rig is composed of nested HealthPrism prefab instances
+            // (e.g. MassBrittlestarFauna embeds DynamicHealthBlock children). They
+            // start at local scale 0 and only grow to their authored target scale
+            // when Prism.Initialize fires the scale animator. LifeForm does this
+            // automatically via BindEmbeddedParts, but LightFauna does not extend
+            // LifeForm — so without this step the brittlestar renders as a cluster
+            // of invisible prisms. We recolor them to the fauna's domain first, then
+            // kick off the growth animation. LifeForm is intentionally NOT set so
+            // these body prisms don't register with Cell as consumable targets.
+            var bodyPrisms = GetComponentsInChildren<HealthPrism>(true);
+            for (int i = 0; i < bodyPrisms.Length; i++)
+            {
+                var hp = bodyPrisms[i];
+                if (!hp) continue;
+                hp.ChangeTeam(domain);
+                hp.Initialize("FaunaPrefab");
+            }
+
             float minSpeed = Mathf.Max(0f, data.minSpeed);
             float maxSpeed = Mathf.Max(minSpeed, data.maxSpeed);
 
```

</details>

### `df28ae618` — fix(prism): swap shield material immediately instead of over 0.8s

_Claude, 2026-04-21 16:46:12 +0000_

```text
The Squirrel-crystal-explosion rings at the origin kept coming back
visually unshielded because MaterialPropertyAnimator.UpdateMaterial
animates the material blend over 0.8s and only performs the actual
MeshRenderer.sharedMaterial swap inside OnAnimationComplete. A fresh
ring prism therefore takes the full 0.8s before it *looks* shielded,
even though prismProperties.IsShielded flips true synchronously.
Fauna at the crystal consume the ring well inside that 0.8s window
(behaviorUpdateRate = 0.5-2s + detectionRadius = 100), so each new
ring ends up harvested before the shielded sharedMaterial ever lands.
Respawned rings therefore render with the previous (unshielded) team
material — exactly the artifact observed.

MaterialPropertyAnimator.SetMaterialImmediate(transparent, opaque) is
a new entry point that swaps sharedMaterial synchronously, updates the
cached active materials, and cancels any in-flight color animation so
the MaterialStateManager tick won't overwrite the swap.

PrismStateManager now calls SetMaterialImmediate from ApplyShieldState,
ApplyNormalState, ActivateSuperShield, and MakeDangerous — the four
paths where the state change must be visible before the prism might
get destroyed in the same tick. The shader-property blend still runs
for the color-interpolation niceness; only the sharedMaterial handoff
is moved forward.

Note: the octahedron shield already swaps its mesh synchronously in
ApplyShieldedPose and only skips the *material* override because
shieldMaterialOverride isn't wired on the base prism prefabs. With the
immediate sharedMaterial swap, the team's shielded material now
renders on the octahedron mesh as intended, without the 0.8s lag.
```

```text
 .../Controller/Environment/Prisms/MaterialPropertyAnimator.cs         | 33 ++++++++++++++++++++++++++++++++-
 Assets/_Scripts/Controller/Managers/PrismStateManager.cs              | 18 ++++++++++++++----
 2 files changed, 46 insertions(+), 5 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/Prisms/MaterialPropertyAnimator.cs b/Assets/_Scripts/Controller/Environment/Prisms/MaterialPropertyAnimator.cs
index 3e3c1309c..f8d796a2d 100644
--- a/Assets/_Scripts/Controller/Environment/Prisms/MaterialPropertyAnimator.cs
+++ b/Assets/_Scripts/Controller/Environment/Prisms/MaterialPropertyAnimator.cs
@@ -175,7 +175,7 @@ namespace CosmicShore.Gameplay
             {
                 activeTransparentMaterial = transparentMaterial;
                 activeOpaqueMaterial = opaqueMaterial;
-                
+
                 if (MeshRenderer != null && cachedPrism != null &&
                     cachedPrism.prismProperties != null)
                 {
@@ -187,6 +187,37 @@ namespace CosmicShore.Gameplay
             };
         }
 
+        /// <summary>
+        /// Synchronously swap sharedMaterial and update the cached active materials.
+        /// Bypasses the 0.8s color-blend animation run by MaterialStateManager — use
+        /// this when the visual state change must be immediate (e.g. shield engage
+        /// on a prism that may be consumed within a single fauna behavior tick).
+        /// </summary>
+        public void SetMaterialImmediate(Material transparentMaterial, Material opaqueMaterial)
+        {
+            if (!enabled || MeshRenderer == null) return;
+            if (transparentMaterial == null || opaqueMaterial == null) return;
+
+            activeTransparentMaterial = transparentMaterial;
+            activeOpaqueMaterial = opaqueMaterial;
+            materialsDirty = false;
+
+            bool useTransparent = cachedPrism != null
+                                  && cachedPrism.prismProperties != null
+                                  && cachedPrism.prismProperties.IsTransparent;
+
+            MeshRenderer.sharedMaterial = useTransparent ? transparentMaterial : opaqueMaterial;
+
+            // Kill any in-flight color animation so it doesn't overwrite the swap
+            // on the next MaterialStateManager tick.
+            if (IsAnimating)
+            {
+                IsAnimating = false;
+                AnimationProgress = 1f;
+                OnAnimationComplete = null;
+            }
+        }
+
         public void SetTransparency(bool transparent)
         {
             if (MeshRenderer != null && ValidateMaterials())
diff --git a/Assets/_Scripts/Controller/Managers/PrismStateManager.cs b/Assets/_Scripts/Controller/Managers/PrismStateManager.cs
index fc75a1647..776bff3b4 100644
--- a/Assets/_Scripts/Controller/Managers/PrismStateManager.cs
+++ b/Assets/_Scripts/Controller/Managers/PrismStateManager.cs
@@ -48,7 +48,7 @@ namespace CosmicShore.Gameplay
             prism.prismProperties.speedDebuffAmount = 0.1f;
             prism.prismProperties.IsShielded = false;
 
-            materialAnimator.UpdateMaterial(
+            materialAnimator.SetMaterialImmediate(
                 _themeManagerData.GetTeamTransparentDangerousBlockMaterial(teamManager.Domain),
                 _themeManagerData.GetTeamDangerousBlockMaterial(teamManager.Domain)
             );
@@ -75,7 +75,7 @@ namespace CosmicShore.Gameplay
             prism.prismProperties.IsSuperShielded = true;
             prism.prismProperties.IsDangerous = false;
 
-            materialAnimator.UpdateMaterial(
+            materialAnimator.SetMaterialImmediate(
                 _themeManagerData.GetTeamTransparentSuperShieldedBlockMaterial(teamManager.Domain),
                 _themeManagerData.GetTeamSuperShieldedBlockMaterial(teamManager.Domain)
             );
@@ -115,7 +115,15 @@ namespace CosmicShore.Gameplay
             prism.prismProperties.IsShielded = true;
             prism.prismProperties.IsDangerous = false;
 
-            materialAnimator.UpdateMaterial(
+            // Immediate swap rather than animated. If the shield state is animated
+            // over 0.8s (the default UpdateMaterial duration), a prism that gets
+            // instantiated + shielded + consumed all inside that window is rendered
+            // with its *unshielded* sharedMaterial despite being logically shielded.
+            // That's exactly the failure mode seen on Squirrel-crystal explosion
+            // rings at the origin: fresh rings still showed the unshielded material
+            // because fauna consumed them before the 0.8s color blend could swap
+            // sharedMaterial in OnAnimationComplete.
+            materialAnimator.SetMaterialImmediate(
                 _themeManagerData.GetTeamTransparentShieldedBlockMaterial(teamManager.Domain),
                 _themeManagerData.GetTeamShieldedBlockMaterial(teamManager.Domain)
             );
@@ -133,7 +141,9 @@ namespace CosmicShore.Gameplay
         {
             var wasShielded = prism.prismProperties.IsShielded || prism.prismProperties.IsSuperShielded;
 
-            materialAnimator.UpdateMaterial(
+            // Same immediate-swap reasoning as ApplyShieldState — avoid the 0.8s
+            // color-blend window leaving prisms in an incorrect visual state.
+            materialAnimator.SetMaterialImmediate(
                 _themeManagerData.GetTeamTransparentBlockMaterial(teamManager.Domain),
                 _themeManagerData.GetTeamBlockMaterial(teamManager.Domain)
             );
```

</details>
