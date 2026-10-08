# Branch archive: `claude/dreamy-keller-lenwio`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-06-12 by Claude
- **Unmerged commits:** 2
- **Forked from:** `cb506b88e` (2026-06-12, Merge branch 'claude/beautiful-bohr-wspnmf' into bleeding-edge)
- **Tip:** `79b720800`
- **Files touched (7):**
  - `Assets/_Scripts/Controller/Environment/EcosystemPerfProbe.cs`
  - `Assets/_Scripts/Controller/Managers/PrismColliderLodManager.cs`
  - `Assets/_Scripts/Controller/Vessel/Prism.cs`
  - `Assets/_Scripts/Tests/EditMode/PrismSpatialIndexTests.cs`
  - `Assets/_Scripts/Utility/DataContainers/CellConfigDataSO.cs`
  - `Docs/ECOSYSTEM_MASTERPLAN.md`
  - `Docs/SPATIAL_INDEX.md`

### `ad323a936` — feat(ecology): Phase B — per-cell ColliderBudget contract + wire UpdateDomain into AOE friend/foe

_Claude, 2026-06-12 18:04:17 +0000_

```text
Two items off the post-merge plan (masterplan §10 Phase B):

- CellConfigDataSO.ColliderBudget (default 1500, 0 = unbudgeted — §4 target):
  PrismColliderLodManager now adapts its radius AIMD-style between
  minRadiusMeters (60) and the configured maximum — multiplicative decrease
  (x0.85/sweep) while active colliders exceed the strictest budget among
  active cells, additive recovery (+8m/sweep) when comfortably under (<80%).
  Pinned at the floor and still over, it warns (throttled 10s) that the biome
  needs a retune — enforcement is collider-only, mass is never culled. Probe
  line gains "budget= lodR=" so the contract is observable.

- UpdateDomain wired (closes the documented known gap, as its own change):
  Prism.HandleTeamChangedForCell now pushes steals/ChangeTeam into the index's
  AOE cold data alongside the cell-grid re-file, so batch explosions read the
  prism's LIVE friend/foe — a stolen prism shields with (and takes damage as)
  its new team instead of its registration-time one. Edit-mode tests cover the
  cold-data write and invalid-index robustness.

Invariants: mechanizes the collider-budget gate; no mass removal, no imposed
death — the budget tightens a radius and warns, never destroys prisms.

In-editor verification: (1) park in the densest canopy and watch colliders=
converge under budget while near-vessel collision stays correct; (2) steal a
prism, AOE it with the old team — it must now take damage (it used to
shield). Gameplay-affecting: verify before the next bleeding-edge merge.
```

```text
 Assets/_Scripts/Controller/Environment/EcosystemPerfProbe.cs   |  7 ++--
 Assets/_Scripts/Controller/Managers/PrismColliderLodManager.cs | 78 ++++++++++++++++++++++++++++++++++++++--
 Assets/_Scripts/Controller/Vessel/Prism.cs                     | 15 +++++---
 Assets/_Scripts/Tests/EditMode/PrismSpatialIndexTests.cs       | 23 ++++++++++++
 Assets/_Scripts/Utility/DataContainers/CellConfigDataSO.cs     | 12 ++++++-
 Docs/ECOSYSTEM_MASTERPLAN.md                                   | 16 ++++++---
 Docs/SPATIAL_INDEX.md                                          | 16 +++++----
 7 files changed, 146 insertions(+), 21 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 290 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/EcosystemPerfProbe.cs b/Assets/_Scripts/Controller/Environment/EcosystemPerfProbe.cs
index fba071e77..ff0e6a9b2 100644
--- a/Assets/_Scripts/Controller/Environment/EcosystemPerfProbe.cs
+++ b/Assets/_Scripts/Controller/Environment/EcosystemPerfProbe.cs
@@ -81,9 +81,12 @@ namespace CosmicShore.Gameplay
             // live prisms — the §4 budget made observable. 0/0 = LOD idle (no foci).
             int near = PrismColliderLodManager.LastNearCount;
             int live = PrismColliderLodManager.LastLiveCount;
+            int budget = PrismColliderLodManager.LastBudget;
+            float lodR = PrismColliderLodManager.LastRadius;
 
-            _lastLine = $"[ECOSIM] prisms={prisms} volume={volume:F0} colliders={near}/{live} " +
-                        $"fauna={fauna} phase={phases} fps={fps:F1}" +
+            _lastLine = $"[ECOSIM] prisms={prisms} volume={volume:F0} colliders={near}/{live}" +
+                        (budget > 0 ? $" budget={budget} lodR={lodR:F0}" : "") +
+                        $" fauna={fauna} phase={phases} fps={fps:F1}" +
                         (cells > 1 ? $"  (cells={cells})" : "");
             Debug.Log(_lastLine);
         }
diff --git a/Assets/_Scripts/Controller/Managers/PrismColliderLodManager.cs b/Assets/_Scripts/Controller/Managers/PrismColliderLodManager.cs
index 7540b344c..d748073fa 100644
--- a/Assets/_Scripts/Controller/Managers/PrismColliderLodManager.cs
+++ b/Assets/_Scripts/Controller/Managers/PrismColliderLodManager.cs
@@ -43,10 +43,17 @@ namespace CosmicShore.Gameplay
         [Tooltip("Master switch. OFF restores every culled collider and goes idle — the in-editor kill switch if any collider consumer was missed.")]
         [SerializeField] bool lodEnabled = true;
 
-        [Tooltip("Prism colliders stay enabled within this distance of any focus (vessel / projectile). " +
-                 "Must comfortably exceed focus speed × tick so fast vessels never outrun their collider bubble.")]
+        [Tooltip("MAXIMUM radius: prism colliders stay enabled within this distance of any focus " +
+                 "(vessel / projectile) when the collider budget allows. Must comfortably exceed " +
+                 "focus speed × tick so fast vessels never outrun their collider bubble.")]
         [Min(50f)] [SerializeField] float lodRadiusMeters = 200f;
 
+        [Tooltip("MINIMUM radius the budget adaptation may tighten to. Below this, near-vessel " +
+                 "collisions (hull/skimmer/triggers) would start to misbehave — if the budget is " +
+                 "still exceeded here, the manager warns instead of tightening further: the biome " +
+                 "is too dense and needs a retune. Never enforced by culling prisms.")]
+        [Min(30f)] [SerializeField] float minRadiusMeters = 60f;
+
         [Tooltip("Seconds between LOD sweeps. At 0.25s a 100 u/s vessel moves 25m per sweep — well inside the radius margin.")]
         [Min(0.05f)] [SerializeField] float tickIntervalSeconds = 0.25f;
 
@@ -59,6 +66,12 @@ namespace CosmicShore.Gameplay
         /// <summary>Live prisms seen in the last sweep (telemetry — EcosystemPerfProbe).</summary>
         public static int LastLiveCount { get; private set; }
 
+        /// <summary>The collider budget in force last sweep, 0 = unbudgeted (telemetry).</summary>
+        public static int LastBudget { get; private set; }
+
+        /// <summary>The (possibly budget-tightened) radius used last sweep (telemetry).</summary>
+        public static float LastRadius { get; private set; }
+
         public static void RegisterFocus(Transform focus)
         {
             if (focus && !s_foci.Contains(focus)) s_foci.Add(focus);
@@ -71,6 +84,8 @@ namespace CosmicShore.Gameplay
 
         float _nextTickAt;
         bool _culledAnything;
+        float _currentRadius = -1f; // initialized to lodRadiusMeters on first sweep
+        float _nextBudgetWarnAt;
         readonly List<Prism> _liveScratch = new(4096);
         readonly List<Prism> _queryScratch = new(1024);
         readonly HashSet<Prism> _nearSet = new();
@@ -117,11 +132,13 @@ namespace CosmicShore.Gameplay
                 return;
             }
 
+            if (_currentRadius < 0f) _currentRadius = lodRadiusMeters;
+
             // Union of per-focus neighborhoods → the prisms that keep colliders.
             _nearSet.Clear();
             for (int f = 0; f < s_foci.Count; f++)
             {
-                int hits = index.QuerySphere(s_foci[f].position, lodRadiusMeters, _queryScratch);
+                int hits = index.QuerySphere(s_foci[f].position, _currentRadius, _queryScratch);
                 for (int i = 0; i < hits; i++)
                     _nearSet.Add(_queryScratch[i]);
             }
@@ -136,6 +153,61 @@ namespace CosmicShore.Gameplay
             _culledAnything = true;
             LastNearCount = _nearSet.Count;
             LastLiveCount = live;
+
+            AdaptRadiusToBudget(_nearSet.Count);
+        }
+
+        /// <summary>
+        /// The §4 performance contract, mechanized: keep ACTIVE colliders under the
+        /// strictest <see cref="CellConfigDataSO.ColliderBudget"/> among the active
+        /// cells by tightening the LOD radius (multiplicative decrease), and relax
+        /// back toward the configured maximum when comfortably under (additive
+        /// increase — classic AIMD, no oscillation). Enforcement is collider-only —
+        /// the budget never destroys mass. When pinned at the minimum radius and
+        /// still over budget, warn (throttled): the biome is too dense and needs a
+        /// flora/threshold retune, which is a design decision, not ours to force.
+        /// </summary>
+        void AdaptRadiusToBudget(int activeColliders)
+        {
+            int budget = 0;
+            var cells = Cell.ActiveCellsSnapshot;
+            for (int i = 0; i < cells.Count; i++)
+            {
+                var cell = cells[i];
+                if (!cell || !cell.Config) continue;
+                int b = cell.Config.ColliderBudget;
+                if (b > 0 && (budget == 0 || b < budget)) budget = b;
+            }
+
+            LastBudget = budget;
+            LastRadius = _currentRadius;
+
+            if (budget <= 0)
+            {
+                _currentRadius = lodRadiusMeters; // unbudgeted: full configured radius
+                return;
+            }
+
+            if (activeColliders > budget)
+            {
+                if (_currentRadius > minRadiusMeters)
+                {
+                    _currentRadius = Mathf.Max(minRadiusMeters, _currentRadius * 0.85f);
+                }
+                else if (Time.time >= _nextBudgetWarnAt)
+                {
+                    _nextBudgetWarnAt = Time.time + 10f;
+                    CSDebug.LogWarning(
+                        $"[PrismColliderLod] {activeColliders} active colliders exceed the budget " +
+                        $"({budget}) at the minimum radius ({minRadiusMeters}m) — the canopy around " +
+                        "the foci is too dense. Retune the biome (flora caps / phase thresholds), " +
+                        "do NOT raise the budget blindly. Mass is never culled for this.");
+                }
+            }
+            else if (activeColliders < budget * 0.8f && _currentRadius < lodRadiusMeters)
+            {
+                _currentRadius = Mathf.Min(lodRadiusMeters, _currentRadius + 8f);
+            }
         }
 
         void OnDisable()
diff --git a/Assets/_Scripts/Controller/Vessel/Prism.cs b/Assets/_Scripts/Controller/Vessel/Prism.cs
index bb6ae930c..47561601d 100644
--- a/Assets/_Scripts/Controller/Vessel/Prism.cs
+++ b/Assets/_Scripts/Controller/Vessel/Prism.cs
@@ -134,10 +134,17 @@ namespace CosmicShore.Gameplay
         {
             // The spatial index owns the cell density-grid binding (Phase 3 — see
             // Docs/SPATIAL_INDEX.md): forward the steal / ChangeTeam so the bound
```

</details>

_Also contains 1 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
