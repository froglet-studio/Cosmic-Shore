# Branch archive: `claude/add-locust-fauna-XROBc`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-26 by Claude
- **Unmerged commits:** 1
- **Forked from:** `e4cdf4cf6` (2026-03-25, Assigned CellConfig to prefab)
- **Tip:** `4fa9d0998`
- **Files touched (5):**
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/Locust/CellAggressionManager.cs`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/Locust/Locust.cs`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/Locust/LocustConfigSO.cs`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/Locust/LocustSwarmManager.cs`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/Locust/LocustVariantSO.cs`

### `4fa9d0998` — Add Locust fauna class — swarm-based trail prism consumers

_Claude, 2026-03-26 20:42:46 +0000_

```text
New fauna species that targets and consumes open-ended trail prisms
to manage scene prism density for performance. Key components:

- Locust: Individual behavior with boid-like flocking, open-end
  targeting, attachment, gradual shrink-to-consume lifecycle
- LocustSwarmManager: Fauna-extending spawner that manages swarm
  population scaled by Cell aggression level
- LocustConfigSO: All tunable parameters (speeds, weights, radii,
  consumption rates) in ScriptableObject config
- LocustVariantSO: Species variant definitions for visual/behavioral
  diversity within a swarm
- CellAggressionManager: Monitors total prism volume and exposes
  normalized aggression (0-1) driving spawn/consumption rates

Uses boid-sized health prisms as stand-in visuals until 3D model
is ready. Prefab assembly required in Unity Editor.
```

```text
 .../Game/Environment/FloraAndFauna/Locust/CellAggressionManager.cs    |  96 +++++++
 Assets/_Scripts/Game/Environment/FloraAndFauna/Locust/Locust.cs       | 445 ++++++++++++++++++++++++++++++++
 .../_Scripts/Game/Environment/FloraAndFauna/Locust/LocustConfigSO.cs  |  67 +++++
 .../Game/Environment/FloraAndFauna/Locust/LocustSwarmManager.cs       | 217 ++++++++++++++++
 .../_Scripts/Game/Environment/FloraAndFauna/Locust/LocustVariantSO.cs |  40 +++
 5 files changed, 865 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 150 of 895 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/Locust/CellAggressionManager.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/Locust/CellAggressionManager.cs
new file mode 100644
index 000000000..17c5a7c93
--- /dev/null
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/Locust/CellAggressionManager.cs
@@ -0,0 +1,96 @@
+using CosmicShore.Game;
+using CosmicShore.Soap;
+using CosmicShore.Utility;
+using UnityEngine;
+
+namespace CosmicShore.Game.Fauna
+{
+    /// <summary>
+    /// Monitors the total volume of prisms within a Cell and exposes a normalized
+    /// Aggression level (0-1) that fauna spawning systems use to control birth rates
+    /// and consumption intensity.
+    ///
+    /// Aggression = 0 means prism count is below the activation threshold (no locusts needed).
+    /// Aggression = 1 means prism count has reached the critical ceiling (maximum culling urgency).
+    ///
+    /// This component should be placed on or near the Cell GameObject.
+    /// </summary>
+    public class CellAggressionManager : MonoBehaviour
+    {
+        [Header("Cell Reference")]
+        [SerializeField] CellRuntimeDataSO cellRuntime;
+        [SerializeField] GameDataSO gameData;
+
+        [Header("Thresholds")]
+        [Tooltip("Total prism volume below which aggression is 0 (no locusts spawn).")]
+        [SerializeField] float activationVolumeThreshold = 500f;
+        [Tooltip("Total prism volume at which aggression reaches 1.0 (maximum urgency).")]
+        [SerializeField] float criticalVolumeCeiling = 3000f;
+
+        [Header("Update Settings")]
+        [Tooltip("How often (seconds) to recalculate aggression level.")]
+        [SerializeField] float updateInterval = 2f;
+
+        [Header("Smoothing")]
+        [Tooltip("How quickly aggression ramps up/down. Higher = more responsive.")]
+        [SerializeField] float aggressionSmoothSpeed = 2f;
+
+        float rawAggression;
+        float smoothedAggression;
+
+        /// <summary>
+        /// The current smoothed aggression level, 0 to 1.
+        /// </summary>
+        public float Aggression => smoothedAggression;
+
+        /// <summary>
+        /// Whether the system has crossed the activation threshold at least once.
+        /// Once active, locusts may continue until prism volume drops back below threshold.
+        /// </summary>
+        public bool IsActive => smoothedAggression > 0f;
+
+        float nextUpdateTime;
+
+        void Update()
+        {
+            if (Time.time < nextUpdateTime) return;
+            nextUpdateTime = Time.time + updateInterval;
+
+            float totalVolume = GetTotalPrismVolume();
+            rawAggression = CalculateAggression(totalVolume);
+            smoothedAggression = Mathf.MoveTowards(smoothedAggression, rawAggression, aggressionSmoothSpeed * updateInterval);
+        }
+
+        float CalculateAggression(float totalVolume)
+        {
+            if (totalVolume <= activationVolumeThreshold)
+                return 0f;
+
+            float range = criticalVolumeCeiling - activationVolumeThreshold;
+            if (range <= 0f) return 1f;
+
+            return Mathf.Clamp01((totalVolume - activationVolumeThreshold) / range);
+        }
+
+        float GetTotalPrismVolume()
+        {
+            // Primary: use GameDataSO total volume which aggregates all team volumes
+            if (gameData)
+                return gameData.GetTotalVolume();
+
+            // Fallback: if a Cell reference is available, sum team volumes directly
+            if (cellRuntime && cellRuntime.Cell)
+            {
+                var cell = cellRuntime.Cell;
+                float total = 0f;
+                total += cell.GetTeamVolume(Domains.Jade);
+                total += cell.GetTeamVolume(Domains.Ruby);
+                total += cell.GetTeamVolume(Domains.Gold);
+                total += cell.GetTeamVolume(Domains.Blue);
+                return total;
+            }
+
+            return 0f;
+        }
+    }
+}
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/Locust/Locust.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/Locust/Locust.cs
new file mode 100644
index 000000000..4d2f22e48
--- /dev/null
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/Locust/Locust.cs
@@ -0,0 +1,445 @@
+using System.Collections;
+using System.Collections.Generic;
+using CosmicShore.Core;
+using CosmicShore.Game;
+using CosmicShore.Utility;
+using UnityEngine;
+
+namespace CosmicShore.Game.Fauna
+{
+    public class Locust : CosmicShore.Fauna
+    {
+        [Header("Locust Config")]
+        [SerializeField] LocustConfigSO config;
+
+        LocustSwarmManager swarmManager;
+        HealthPrism embeddedHealthPrism;
+        BoxCollider blockCollider;
+
+        // Flocking state
+        Vector3 currentVelocity;
+        Quaternion desiredRotation;
+
+        // Targeting state
+        Prism targetPrism;
+        bool isAttached;
+        bool isConsuming;
+        float lingerTimer;
+        bool isLingering;
+
+        // Swarm tracking — set by manager for staggered updates
+        public float NormalizedIndex { get; set; }
+
+        // Attachment slot tracking (static registry so locusts coordinate)
+        static readonly Dictionary<Prism, int> attachmentCounts = new();
+
+        public override void Initialize(Cell cell)
+        {
+            embeddedHealthPrism = GetComponentInChildren<HealthPrism>(true);
+            if (!embeddedHealthPrism)
+            {
+                CSDebug.LogError($"{nameof(Locust)} on {name} has no embedded HealthPrism in children.");
+                return;
```

</details>
