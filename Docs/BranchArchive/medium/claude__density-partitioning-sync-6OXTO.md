# Branch archive: `claude/density-partitioning-sync-6OXTO`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-05-09 by Claude
- **Unmerged commits:** 6
- **Forked from:** `9537cefbe` (2026-05-05, Merge branch 'bleeding-edge' of https://github.com/froglet-studio/Cosmic-Shore)
- **Tip:** `0bd6b42f3`
- **Files touched (4):**
  - `Assets/_Scripts/Controller/Environment/Cell.cs`
  - `Assets/_Scripts/Controller/Managers/DensityPartitionSystem.cs`
  - `Assets/_Scripts/Utility/Tools/DensityPartitionDiagnostics.cs`
  - `Assets/_Scripts/Utility/Tools/LogControlWindow.cs`

### `13f5f9342` — feat(gameplay): add network-synced anti-domain density partitioning

_Claude, 2026-05-06 17:10:49 +0000_

```text
DensityPartitionSystem aggregates per-Cell density grids on the server
every 0.5s and broadcasts three "anti-domain" solutions
(densest region of {Ruby+Gold}, {Jade+Gold}, {Jade+Ruby}) via
NetworkVariable<PartitionSolution>. AI, fauna, and vessel abilities
poll the cached answer with no per-reader recompute cost.

- PartitionSolution: INetworkSerializable + IEquatable struct (atomic
  position + density + cellId + stride + version payload)
- Recompute coalescing: periodic tick + cooldowned RequestImmediateRecompute
- Cell.ActiveCells static registry replaces FindObjectsByType scans
- Local cache mirrors NetworkVariables so reads are O(1) on both server
  and clients (server fills directly; clients via OnValueChanged)
- Reuses existing per-Cell BlockCountDensityGrid anti-domain bucketing —
  no duplicate state, just picks strongest centroid across all cells

Toolbox "Density" tab adds Scene-view overlay toggles, live status
readout (version, cells scanned, last cost ms, next-tick countdown),
per-anti-domain solution rows with "Frame" buttons, and a force-recompute
shortcut. Overlay drawn via SceneView.duringSceneGui — grid wireframe
plus density heatmap on the Blue all-domain bucket, plus the three
anti-domain spheres scaled by sqrt(density) with optional labels.
```

```text
 Assets/_Scripts/Controller/Environment/Cell.cs                |  10 ++
 Assets/_Scripts/Controller/Managers/DensityPartitionSystem.cs | 350 ++++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Utility/Tools/DensityPartitionDiagnostics.cs  | 219 +++++++++++++++++++++++++
 Assets/_Scripts/Utility/Tools/LogControlWindow.cs             | 147 ++++++++++++++++-
 4 files changed, 724 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 798 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/Cell.cs b/Assets/_Scripts/Controller/Environment/Cell.cs
index da2f6aee4..facb515c5 100644
--- a/Assets/_Scripts/Controller/Environment/Cell.cs
+++ b/Assets/_Scripts/Controller/Environment/Cell.cs
@@ -57,6 +57,12 @@ namespace CosmicShore.Gameplay
         readonly Dictionary<Domains, float> teamVolumes = new();
         readonly Dictionary<Domains, int> domainBlockCounts = new();
 
+        // Live registry consumed by DensityPartitionSystem and editor diagnostics.
+        // OnEnable/OnDisable maintain it so the partition tick doesn't need a
+        // FindObjectsByType scan, and so destroyed cells drop out cleanly.
+        static readonly HashSet<Cell> _activeCells = new();
+        public static IReadOnlyCollection<Cell> ActiveCells => _activeCells;
+
         readonly List<GameObject> spawnedLifeForms = new();
         readonly HashSet<Prism> trackedBlocks = new();
         SnowChanger spawnedCytoplasm;
@@ -216,6 +222,8 @@ namespace CosmicShore.Gameplay
 
         void OnEnable()
         {
+            _activeCells.Add(this);
+
             // Clear stale config BEFORE subscribing to events.
             // CellRuntimeDataSO is a shared SO asset — Menu_Main's Cell sets
             // runtime.Config to Blob Cell Config, which persists into the next
@@ -253,6 +261,8 @@ namespace CosmicShore.Gameplay
 
         void OnDisable()
         {
+            _activeCells.Remove(this);
+
             if (gameData != null)
                 gameData.OnInitializeGame.OnRaised -= Initialize;
 
diff --git a/Assets/_Scripts/Controller/Managers/DensityPartitionSystem.cs b/Assets/_Scripts/Controller/Managers/DensityPartitionSystem.cs
new file mode 100644
index 000000000..23829bd90
--- /dev/null
+++ b/Assets/_Scripts/Controller/Managers/DensityPartitionSystem.cs
@@ -0,0 +1,350 @@
+using System.Diagnostics;
+using CosmicShore.Data;
+using CosmicShore.Utility;
+using Unity.Netcode;
+using UnityEngine;
+
+namespace CosmicShore.Gameplay
+{
+    /// <summary>
+    /// One pre-computed densest-region answer for a single anti-domain bucket
+    /// (i.e. the densest region of all prisms NOT belonging to the friendly
+    /// domain that's asking). Sent across the wire as one atomic payload so
+    /// position, density, owning-cell id, and version always replicate together.
+    /// </summary>
+    public struct PartitionSolution : INetworkSerializable, System.IEquatable<PartitionSolution>
+    {
+        public Vector3 Position;
+        public float Density;
+        public int CellId;
+
+        // Stride at the time of compute, so consumers can re-derive the bucket
+        // bounds for AOE / nav purposes without round-tripping to the cell.
+        public float Stride;
+
+        // Friendly domain this solution is "anti" to, packed as int for serializer.
+        public int AntiOfDomain;
+
+        // Monotonic counter so consumers can tell whether a poll returned a
+        // new answer or the same one they saw last frame.
+        public uint Version;
+
+        public bool HasResult => Density > 0f;
+
+        public Domains AntiOfDomainEnum => (Domains)AntiOfDomain;
+
+        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
+        {
+            serializer.SerializeValue(ref Position);
+            serializer.SerializeValue(ref Density);
+            serializer.SerializeValue(ref CellId);
+            serializer.SerializeValue(ref Stride);
+            serializer.SerializeValue(ref AntiOfDomain);
+            serializer.SerializeValue(ref Version);
+        }
+
+        public bool Equals(PartitionSolution other) =>
+            Position.Equals(other.Position)
+            && Density == other.Density
+            && CellId == other.CellId
+            && Stride == other.Stride
+            && AntiOfDomain == other.AntiOfDomain
+            && Version == other.Version;
+
+        public override bool Equals(object obj) => obj is PartitionSolution o && Equals(o);
+
+        public override int GetHashCode() =>
+            Position.GetHashCode() ^ Density.GetHashCode() ^ CellId ^ AntiOfDomain ^ (int)Version;
+    }
+
+    /// <summary>
+    /// Network-synced periodic aggregator over per-Cell density grids.
+    ///
+    /// Computes three "anti-domain" solutions on the server every
+    /// <see cref="recomputeIntervalSeconds"/> and replicates them as
+    /// <see cref="NetworkVariable{T}"/>s so any reader (AI, fauna, vessel
+    /// abilities) can poll the latest answer with no per-reader recompute cost:
+    ///
+    /// <list type="bullet">
+    ///   <item>Anti-Jade = densest region of {Ruby ∪ Gold} prisms.</item>
+    ///   <item>Anti-Ruby = densest region of {Jade ∪ Gold} prisms.</item>
+    ///   <item>Anti-Gold = densest region of {Jade ∪ Ruby} prisms.</item>
+    /// </list>
+    ///
+    /// The buckets aren't recomputed here — <see cref="Cell"/> already keeps a
+    /// per-team <see cref="BlockCountDensityGrid"/> where the per-domain bucket
+    /// stores every block NOT belonging to that domain (the existing
+    /// "anti-domain" semantic). This system only picks the strongest centroid
+    /// across all active cells, stamps a version, and broadcasts.
+    ///
+    /// Hybrid event entry: <see cref="RequestImmediateRecompute"/> is the
+    /// cooldowned event path. Many simultaneous callers within
+    /// <see cref="eventCooldownSeconds"/> coalesce to a single recompute.
+    /// </summary>
+    public class DensityPartitionSystem : NetworkBehaviour
+    {
+        [Header("Recompute cadence")]
+        [Tooltip("Seconds between server-side recomputes. Default 0.5s (2 Hz) " +
+                 "comfortably covers fauna seeking and AI target choice without " +
+                 "burning CPU on every frame.")]
+        [SerializeField] float recomputeIntervalSeconds = 0.5f;
+
+        [Tooltip("Minimum seconds between RequestImmediateRecompute() responses. " +
+                 "Many event-driven callers within the cooldown coalesce to a " +
+                 "single recompute, preventing thrash on volatile prism volumes.")]
+        [SerializeField] float eventCooldownSeconds = 0.25f;
+
+        [Header("Diagnostics")]
+        [Tooltip("If true, log each recompute's millisecond cost.")]
+        [SerializeField] bool verboseProfiling;
+
+        // ── Networked solutions (server writes, everyone reads) ─────────────
+        // Default read perm = Everyone, default write perm = Server. We use the
+        // explicit constructor so non-spawned writes don't throw on the host
+        // before OnNetworkSpawn (NetworkVariable buffers writes pre-spawn).
+        readonly NetworkVariable<PartitionSolution> _antiJade =
+            new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
+        readonly NetworkVariable<PartitionSolution> _antiRuby =
+            new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
+        readonly NetworkVariable<PartitionSolution> _antiGold =
```

</details>

### `75c9df74f` — fix(gameplay): rename diagnostics namespace away from CosmicShore.Editor

_Claude, 2026-05-06 18:24:12 +0000_

```text
DensityPartitionDiagnostics declared 'namespace CosmicShore.Editor' in
Assembly-CSharp.dll, which shadowed UnityEditor.Editor for any class in
CosmicShore.* that extended Editor (CameraSettingsSOEditor,
LeaderboardConfigSOEditor, UniversalStatsProviderEditor, ResourceDisplay).
C# resolves the inner CosmicShore.Editor namespace before falling through
to 'using UnityEditor;', producing CS0118.

The CosmicShore.Editor namespace was previously safe because it only
existed in Assembly-CSharp-Editor.dll, which Assembly-CSharp.dll does not
reference. Moving the diagnostics file's namespace to CosmicShore.Utility
(matching its folder and LogControlWindow's namespace) un-bridges the
two assemblies. Drop the now-redundant 'using CosmicShore.Editor;' from
LogControlWindow.
```

```text
 Assets/_Scripts/Utility/Tools/DensityPartitionDiagnostics.cs | 2 +-
 Assets/_Scripts/Utility/Tools/LogControlWindow.cs            | 1 -
 2 files changed, 1 insertion(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Tools/DensityPartitionDiagnostics.cs b/Assets/_Scripts/Utility/Tools/DensityPartitionDiagnostics.cs
index 8375639a5..c3e6f39a1 100644
--- a/Assets/_Scripts/Utility/Tools/DensityPartitionDiagnostics.cs
+++ b/Assets/_Scripts/Utility/Tools/DensityPartitionDiagnostics.cs
@@ -5,7 +5,7 @@ using CosmicShore.Gameplay;
 using UnityEditor;
 using UnityEngine;
 
-namespace CosmicShore.Editor
+namespace CosmicShore.Utility
 {
     /// <summary>
     /// Edit-mode + play-mode Scene-view overlay for the network-synced
diff --git a/Assets/_Scripts/Utility/Tools/LogControlWindow.cs b/Assets/_Scripts/Utility/Tools/LogControlWindow.cs
index a0822a10f..dcfe793e0 100644
--- a/Assets/_Scripts/Utility/Tools/LogControlWindow.cs
+++ b/Assets/_Scripts/Utility/Tools/LogControlWindow.cs
@@ -4,7 +4,6 @@ using System;
 using System.Collections.Generic;
 using System.Linq;
 using CosmicShore.Data;
-using CosmicShore.Editor;
 using CosmicShore.Gameplay;
 using CosmicShore.UI;
 using CosmicShore.Core;
```

</details>

### `8b748f3a1` — refactor(gameplay): make DensityPartitionSystem MonoBehaviour + auto-bootstrap

_Claude, 2026-05-06 21:42:19 +0000_

```text
Drop the NetworkBehaviour requirement so the system works in any scene
(including Menu_Main as a long-running ecosystem) without needing a
hand-placed GameObject with a NetworkObject. The system now bootstraps
itself on every scene load via [RuntimeInitializeOnLoadMethod] +
SceneManager.sceneLoaded — first scene load creates a hidden runtime
GameObject; subsequent loads re-spawn so cell registries from the
previous scene don't leak forward.

Each client computes locally over its own (Netcode-replicated) Cells.
Network sync is scoped to a future DensityPartitionNetworkSync sibling
component — for solo Menu_Main and game scenes where prism replication
is already deterministic, local compute matches across clients without
the extra wire traffic.

Beef up the toolbox "Density" tab with an at-a-glance ecosystem health
banner: red when no Cells in scene, amber when cells exist but have no
prisms yet, green when partitions are live with prism counts. The
"with prisms" cell count is now tracked separately so the banner can
distinguish "cells exist but empty" from "cells full of prisms".

PartitionSolution drops INetworkSerializable for now (still IEquatable);
will return when network sync lands.
```

```text
 Assets/_Scripts/Controller/Managers/DensityPartitionSystem.cs | 224 +++++++++++++++++++---------------------
 Assets/_Scripts/Utility/Tools/LogControlWindow.cs             |  86 +++++++++++++--
 2 files changed, 179 insertions(+), 131 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 496 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Managers/DensityPartitionSystem.cs b/Assets/_Scripts/Controller/Managers/DensityPartitionSystem.cs
index 23829bd90..e0be5834c 100644
--- a/Assets/_Scripts/Controller/Managers/DensityPartitionSystem.cs
+++ b/Assets/_Scripts/Controller/Managers/DensityPartitionSystem.cs
@@ -1,18 +1,18 @@
 using System.Diagnostics;
 using CosmicShore.Data;
 using CosmicShore.Utility;
-using Unity.Netcode;
 using UnityEngine;
+using UnityEngine.SceneManagement;
 
 namespace CosmicShore.Gameplay
 {
     /// <summary>
     /// One pre-computed densest-region answer for a single anti-domain bucket
     /// (i.e. the densest region of all prisms NOT belonging to the friendly
-    /// domain that's asking). Sent across the wire as one atomic payload so
-    /// position, density, owning-cell id, and version always replicate together.
+    /// domain that's asking). Held as a struct so consumers can pass it around
+    /// by value and so a future network-sync layer can serialize it directly.
     /// </summary>
-    public struct PartitionSolution : INetworkSerializable, System.IEquatable<PartitionSolution>
+    public struct PartitionSolution : System.IEquatable<PartitionSolution>
     {
         public Vector3 Position;
         public float Density;
@@ -22,7 +22,7 @@ namespace CosmicShore.Gameplay
         // bounds for AOE / nav purposes without round-tripping to the cell.
         public float Stride;
 
-        // Friendly domain this solution is "anti" to, packed as int for serializer.
+        // Friendly domain this solution is "anti" to (the team that's asking).
         public int AntiOfDomain;
 
         // Monotonic counter so consumers can tell whether a poll returned a
@@ -33,16 +33,6 @@ namespace CosmicShore.Gameplay
 
         public Domains AntiOfDomainEnum => (Domains)AntiOfDomain;
 
-        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
-        {
-            serializer.SerializeValue(ref Position);
-            serializer.SerializeValue(ref Density);
-            serializer.SerializeValue(ref CellId);
-            serializer.SerializeValue(ref Stride);
-            serializer.SerializeValue(ref AntiOfDomain);
-            serializer.SerializeValue(ref Version);
-        }
-
         public bool Equals(PartitionSolution other) =>
             Position.Equals(other.Position)
             && Density == other.Density
@@ -58,12 +48,10 @@ namespace CosmicShore.Gameplay
     }
 
     /// <summary>
-    /// Network-synced periodic aggregator over per-Cell density grids.
-    ///
-    /// Computes three "anti-domain" solutions on the server every
-    /// <see cref="recomputeIntervalSeconds"/> and replicates them as
-    /// <see cref="NetworkVariable{T}"/>s so any reader (AI, fauna, vessel
-    /// abilities) can poll the latest answer with no per-reader recompute cost:
+    /// Periodic aggregator over per-Cell density grids. Computes three
+    /// "anti-domain" solutions every <see cref="recomputeIntervalSeconds"/>
+    /// and caches them so any reader (AI, fauna, vessel abilities) can poll
+    /// with no per-reader recompute cost:
     ///
     /// <list type="bullet">
     ///   <item>Anti-Jade = densest region of {Ruby ∪ Gold} prisms.</item>
@@ -75,13 +63,32 @@ namespace CosmicShore.Gameplay
     /// per-team <see cref="BlockCountDensityGrid"/> where the per-domain bucket
     /// stores every block NOT belonging to that domain (the existing
     /// "anti-domain" semantic). This system only picks the strongest centroid
-    /// across all active cells, stamps a version, and broadcasts.
+    /// across all active cells, stamps a version, and caches.
+    ///
+    /// <para>
+    /// Auto-bootstrap: <see cref="EnsureExists"/> is called from a
+    /// <see cref="RuntimeInitializeOnLoadMethodAttribute"/> hook so every
+    /// loaded scene gets the system for free — no manual scene placement
+    /// required. The lifetime is per-scene; <see cref="ResetForSceneLoad"/>
+    /// re-spawns it after each load so cell registries from the previous
+    /// scene don't leak forward.
+    /// </para>
     ///
+    /// <para>
     /// Hybrid event entry: <see cref="RequestImmediateRecompute"/> is the
     /// cooldowned event path. Many simultaneous callers within
-    /// <see cref="eventCooldownSeconds"/> coalesce to a single recompute.
+    /// <see cref="eventCooldownSeconds"/> coalesce to a single recompute,
+    /// preventing thrash on volatile prism volumes.
+    /// </para>
+    ///
+    /// <para>
+    /// Network sync was scoped out of the initial system: each client computes
+    /// locally over its own (Netcode-replicated) Cells. A future
+    /// <c>DensityPartitionNetworkSync</c> sibling can be added if game scenes
+    /// require server-authoritative answers.
+    /// </para>
     /// </summary>
-    public class DensityPartitionSystem : NetworkBehaviour
+    public class DensityPartitionSystem : MonoBehaviour
     {
         [Header("Recompute cadence")]
         [Tooltip("Seconds between server-side recomputes. Default 0.5s (2 Hz) " +
@@ -98,26 +105,12 @@ namespace CosmicShore.Gameplay
         [Tooltip("If true, log each recompute's millisecond cost.")]
         [SerializeField] bool verboseProfiling;
 
-        // ── Networked solutions (server writes, everyone reads) ─────────────
-        // Default read perm = Everyone, default write perm = Server. We use the
-        // explicit constructor so non-spawned writes don't throw on the host
-        // before OnNetworkSpawn (NetworkVariable buffers writes pre-spawn).
-        readonly NetworkVariable<PartitionSolution> _antiJade =
-            new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
-        readonly NetworkVariable<PartitionSolution> _antiRuby =
-            new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
-        readonly NetworkVariable<PartitionSolution> _antiGold =
-            new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
-
-        // ── Local cache mirrors of the above ────────────────────────────────
-        // Server fills these directly each Recompute() so reads work even
-        // before/without OnNetworkSpawn. Clients overwrite them from the
-        // OnValueChanged callback so reads are O(1) (no Value-getter call).
-        PartitionSolution _localAntiJade;
-        PartitionSolution _localAntiRuby;
-        PartitionSolution _localAntiGold;
-
-        // ── Server-only tick state ──────────────────────────────────────────
+        // ── Cached solutions (read by anyone via the public API) ────────────
+        PartitionSolution _antiJade;
+        PartitionSolution _antiRuby;
+        PartitionSolution _antiGold;
+
+        // ── Tick state ──────────────────────────────────────────────────────
         float _nextRecomputeAt;
         float _earliestEventRecomputeAt;
         uint _version;
@@ -125,14 +118,15 @@ namespace CosmicShore.Gameplay
         // ── Diagnostics ─────────────────────────────────────────────────────
         public uint Version => _version;
         public int LastRecomputeCellsScanned { get; private set; }
+        public int LastRecomputeCellsWithPrisms { get; private set; }
         public float LastRecomputeMillis { get; private set; }
         public float RecomputeIntervalSeconds => recomputeIntervalSeconds;
         public float EventCooldownSeconds => eventCooldownSeconds;
         public float NextRecomputeIn => Mathf.Max(0f, _nextRecomputeAt - Time.time);
 
-        // ── Singleton-ish accessor ──────────────────────────────────────────
```

</details>

### `71b697d4e` — fix(gameplay): remove RuntimeInitializeOnLoadMethod auto-bootstrap

_Claude, 2026-05-07 23:01:31 +0000_

```text
The static SceneManager.sceneLoaded hook fired during Unity's earliest
init phase (SubsystemRegistration) and during every scene load,
including Netcode scene transitions, where its synchronous
'new GameObject()' + AddComponent could conflict with the engine's
own scene/network bookkeeping. Reported as crashing Unity.

Replace with explicit, deterministic bootstrap points:
- Cell.OnEnable() ensures the system at first cell-enable in any scene
  with a Cell (covers all gameplay scenes)
- The toolbox 'Density' tab now offers a 'Spawn DensityPartitionSystem'
  button when none is found, so Menu_Main (which has no Cell today) can
  spawn the system on demand for diagnostics

EnsureExists() remains the single bootstrap entry point and is safe to
call from any scene at any time.
```

```text
 Assets/_Scripts/Controller/Environment/Cell.cs                |  6 ++++++
 Assets/_Scripts/Controller/Managers/DensityPartitionSystem.cs | 33 +++++++--------------------------
 Assets/_Scripts/Utility/Tools/LogControlWindow.cs             | 13 +++++++++----
 3 files changed, 22 insertions(+), 30 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/Cell.cs b/Assets/_Scripts/Controller/Environment/Cell.cs
index facb515c5..21db775f5 100644
--- a/Assets/_Scripts/Controller/Environment/Cell.cs
+++ b/Assets/_Scripts/Controller/Environment/Cell.cs
@@ -224,6 +224,12 @@ namespace CosmicShore.Gameplay
         {
             _activeCells.Add(this);
 
+            // First Cell to come online in any scene boots the partition
+            // system so AI / fauna / abilities can poll the anti-domain
+            // solutions without a per-scene manual placement step. No-op
+            // if it already exists.
+            DensityPartitionSystem.EnsureExists();
+
             // Clear stale config BEFORE subscribing to events.
             // CellRuntimeDataSO is a shared SO asset — Menu_Main's Cell sets
             // runtime.Config to Blob Cell Config, which persists into the next
diff --git a/Assets/_Scripts/Controller/Managers/DensityPartitionSystem.cs b/Assets/_Scripts/Controller/Managers/DensityPartitionSystem.cs
index e0be5834c..e869c99fe 100644
--- a/Assets/_Scripts/Controller/Managers/DensityPartitionSystem.cs
+++ b/Assets/_Scripts/Controller/Managers/DensityPartitionSystem.cs
@@ -2,7 +2,6 @@ using System.Diagnostics;
 using CosmicShore.Data;
 using CosmicShore.Utility;
 using UnityEngine;
-using UnityEngine.SceneManagement;
 
 namespace CosmicShore.Gameplay
 {
@@ -66,12 +65,13 @@ namespace CosmicShore.Gameplay
     /// across all active cells, stamps a version, and caches.
     ///
     /// <para>
-    /// Auto-bootstrap: <see cref="EnsureExists"/> is called from a
-    /// <see cref="RuntimeInitializeOnLoadMethodAttribute"/> hook so every
-    /// loaded scene gets the system for free — no manual scene placement
-    /// required. The lifetime is per-scene; <see cref="ResetForSceneLoad"/>
-    /// re-spawns it after each load so cell registries from the previous
-    /// scene don't leak forward.
+    /// Bootstrap: <see cref="EnsureExists"/> spawns the system on demand.
+    /// <see cref="Cell.OnEnable"/> calls it so any scene with cells gets
+    /// the system for free; the editor toolbox's "Density" tab also calls
+    /// it so a Menu_Main session without cells still has the diagnostics
+    /// available the moment you open the tab. Scenes that have neither
+    /// cells nor toolbox access (e.g. headless tests) need to call
+    /// <see cref="EnsureExists"/> explicitly.
     /// </para>
     ///
     /// <para>
@@ -153,25 +153,6 @@ namespace CosmicShore.Gameplay
             return system;
         }
 
-        // ── Auto-bootstrap on every scene load ──────────────────────────────
-
-        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
-        static void HookSceneLoad()
-        {
-            // Wire once per game session. SubsystemRegistration runs before
-            // any scene loads, so we get to subscribe before Bootstrap.
-            SceneManager.sceneLoaded -= OnSceneLoadedStatic;
-            SceneManager.sceneLoaded += OnSceneLoadedStatic;
-        }
-
-        static void OnSceneLoadedStatic(Scene scene, LoadSceneMode mode)
-        {
-            // Drop the stale reference from the previous scene; Active getter
-            // will re-find or EnsureExists will create a fresh instance.
-            _active = null;
-            EnsureExists();
-        }
-
         void Awake()
         {
             if (_active != null && _active != this)
diff --git a/Assets/_Scripts/Utility/Tools/LogControlWindow.cs b/Assets/_Scripts/Utility/Tools/LogControlWindow.cs
index faccf4800..a28de8c25 100644
--- a/Assets/_Scripts/Utility/Tools/LogControlWindow.cs
+++ b/Assets/_Scripts/Utility/Tools/LogControlWindow.cs
@@ -831,11 +831,16 @@ namespace CosmicShore.Utility
                 GUILayout.Space(Pad);
                 GUILayout.Label(
                     "<b>No DensityPartitionSystem yet.</b>\n" +
-                    "It auto-bootstraps on every scene load via " +
-                    "[RuntimeInitializeOnLoadMethod]. If this message persists, " +
-                    "the static SceneManager.sceneLoaded hook hasn't fired — try " +
-                    "reloading the current scene or pressing Play.",
+                    "Cells auto-bootstrap one when they enable. For scenes " +
+                    "without Cells (e.g. Menu_Main as it stands today), spawn " +
+                    "one manually for diagnostics:",
                     _infoStyle);
+                GUILayout.Space(4);
+                EditorGUILayout.BeginHorizontal();
+                GUILayout.Space(Pad);
+                if (GUILayout.Button("Spawn DensityPartitionSystem"))
+                    DensityPartitionSystem.EnsureExists();
+                EditorGUILayout.EndHorizontal();
                 return;
             }
 
```

</details>

### `d4ba8cd83` — feat(gameplay): kernel-smoothed peak search + all-domain sanity marker

_Claude, 2026-05-09 00:07:07 +0000_

```text
The single-cell-max search inherited from BlockDensityGrid.FindDensestRegion
let an isolated 2-prism cluster outvote a wider 10-prism spread. In a
long-running ecosystem like Menu_Main this manifested as anti-domain
markers landing at "arbitrary" locations relative to the visible heatmap
cubes — fauna seek correctly via per-cell queries, but the aggregate
peak jittered across single-prism noise.

Replace with a (2r+1)^3 kernel-smoothed scan (default radius 1 = 3x3x3).
The strongest *region* now wins, not the strongest single cell.
'searchKernelRadius' is serialized [0..4] so users can dial it up for
larger smoothing on slower-growth scenes.

Add an all-domain diagnostic marker (white) computed from countGrids[Blue].
This bucket is added/removed unconditionally so it tracks the visible
heatmap exactly — useful as a sanity check: if the white marker lands
on the bright cubes but the per-team antis don't, the issue is bucket
staleness in Cell.AddBlock/RemoveBlock (block.Domain at add-time differs
from remove-time because HealthBlockTracker.Add calls AddBlock before
ChangeTeam, so non-Jade prisms that later become Jade leave stale +1s
in countGrids[Jade] forever). That fix is queued separately.

Toolbox tab grows a 'Show All-Domain (white)' toggle and a fourth
solution row labeled 'heatmap peak, sanity check'. 'Highlight Strongest'
now defaults off — users want to see all solutions side-by-side to
understand the system, not just the winning one.
```

```text
 Assets/_Scripts/Controller/Managers/DensityPartitionSystem.cs | 101 +++++++++++++++++++++++++++++++++-------
 Assets/_Scripts/Utility/Tools/DensityPartitionDiagnostics.cs  |  39 ++++++++++------
 Assets/_Scripts/Utility/Tools/LogControlWindow.cs             |  16 ++++---
 3 files changed, 120 insertions(+), 36 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 279 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Managers/DensityPartitionSystem.cs b/Assets/_Scripts/Controller/Managers/DensityPartitionSystem.cs
index e869c99fe..6c1c3651a 100644
--- a/Assets/_Scripts/Controller/Managers/DensityPartitionSystem.cs
+++ b/Assets/_Scripts/Controller/Managers/DensityPartitionSystem.cs
@@ -101,6 +101,14 @@ namespace CosmicShore.Gameplay
                  "single recompute, preventing thrash on volatile prism volumes.")]
         [SerializeField] float eventCooldownSeconds = 0.25f;
 
+        [Header("Search")]
+        [Tooltip("Half-extent (in grid cells) of the kernel-smoothed peak search. " +
+                 "0 = pick the single densest cell (raw max, brittle to single-prism " +
+                 "noise); 1 = pick the densest 3x3x3 region (default — smooths over " +
+                 "isolated tight clusters so the strongest *region* wins, not the " +
+                 "strongest single cell); 2 = densest 5x5x5; etc.")]
+        [SerializeField, Range(0, 4)] int searchKernelRadius = 1;
+
         [Header("Diagnostics")]
         [Tooltip("If true, log each recompute's millisecond cost.")]
         [SerializeField] bool verboseProfiling;
@@ -109,6 +117,11 @@ namespace CosmicShore.Gameplay
         PartitionSolution _antiJade;
         PartitionSolution _antiRuby;
         PartitionSolution _antiGold;
+        // All-domain (countGrids[Blue]) peak — diagnostic only, surfaces in
+        // the toolbox so the user can see whether the search itself agrees
+        // with the visible heatmap (it should). Disagreement between this
+        // and the per-team antis is a bucket-staleness symptom in Cell.cs.
+        PartitionSolution _allDomain;
 
         // ── Tick state ──────────────────────────────────────────────────────
         float _nextRecomputeAt;
@@ -196,6 +209,15 @@ namespace CosmicShore.Gameplay
             return solution.HasResult;
         }
 
+        /// <summary>
+        /// Diagnostic-only: densest region of the cell's all-domain bucket
+        /// (countGrids[Domains.Blue]). Useful as a sanity check against the
+        /// heatmap — if the all-domain marker tracks the bright cubes but
+        /// the per-team antis don't, that's a bucket-staleness issue in
+        /// Cell's Add/Remove path, not in this search.
+        /// </summary>
+        public PartitionSolution GetAllDomainSolution() => _allDomain;
+
         /// <summary>
         /// Convenience for AI / fauna / vessel abilities that just want the
         /// current anti-domain answer without null-checking <see cref="Active"/>.
@@ -253,7 +275,9 @@ namespace CosmicShore.Gameplay
             var bestJade = new PartitionSolution { AntiOfDomain = (int)Domains.Jade, Version = _version };
             var bestRuby = new PartitionSolution { AntiOfDomain = (int)Domains.Ruby, Version = _version };
             var bestGold = new PartitionSolution { AntiOfDomain = (int)Domains.Gold, Version = _version };
+            var bestAll  = new PartitionSolution { AntiOfDomain = (int)Domains.Blue, Version = _version };
 
+            int radius = Mathf.Max(0, searchKernelRadius);
             int scanned = 0;
             int withPrisms = 0;
             var cells = Cell.ActiveCells;
@@ -264,15 +288,17 @@ namespace CosmicShore.Gameplay
                 scanned++;
 
                 bool any = false;
-                any |= EvaluateCellAntiDomain(cell, Domains.Jade, ref bestJade);
-                any |= EvaluateCellAntiDomain(cell, Domains.Ruby, ref bestRuby);
-                any |= EvaluateCellAntiDomain(cell, Domains.Gold, ref bestGold);
+                any |= EvaluateCellGrid(cell, Domains.Jade, radius, ref bestJade);
+                any |= EvaluateCellGrid(cell, Domains.Ruby, radius, ref bestRuby);
+                any |= EvaluateCellGrid(cell, Domains.Gold, radius, ref bestGold);
+                EvaluateCellGrid(cell, Domains.Blue, radius, ref bestAll); // diagnostic
                 if (any) withPrisms++;
             }
 
             _antiJade = bestJade;
             _antiRuby = bestRuby;
             _antiGold = bestGold;
+            _allDomain = bestAll;
 
             sw.Stop();
             LastRecomputeMillis = (float)sw.Elapsed.TotalMilliseconds;
@@ -284,29 +310,70 @@ namespace CosmicShore.Gameplay
                 CSDebug.Log($"[DensityPartitionSystem] v{_version} scanned {scanned} cells " +
                             $"({withPrisms} with prisms) in {LastRecomputeMillis:F2}ms — " +
                             $"antiJ={bestJade.Density:F0} antiR={bestRuby.Density:F0} " +
-                            $"antiG={bestGold.Density:F0}");
+                            $"antiG={bestGold.Density:F0} all={bestAll.Density:F0}");
             }
         }
 
         /// <summary>
-        /// Returns true when this cell contributed any prism to <paramref name="friendlyDomain"/>'s
-        /// anti-domain bucket — used to count "cells with prisms" for diagnostics.
+        /// Kernel-smoothed peak search: scans the cell's grid for the position
+        /// where the sum over a (2r+1)³ neighborhood is largest. Smooths out
+        /// single-prism noise so a tight 2-prism cluster doesn't outvote a
+        /// wider 10-prism spread, which was making the original FindDensestRegion
+        /// (single-cell max) jitter to "arbitrary" peaks in long-running ecosystems.
+        /// Returns true when this cell contributed any mass — used to count
+        /// "cells with prisms" for diagnostics.
         /// </summary>
-        static bool EvaluateCellAntiDomain(Cell cell, Domains friendlyDomain, ref PartitionSolution best)
+        static bool EvaluateCellGrid(Cell cell, Domains bucketDomain, int radius,
+                                     ref PartitionSolution best)
         {
-            // Cell.countGrids[friendly] holds every block NOT in `friendly`
-            // (see Cell.AddBlock — friendly is the one team it skips). So the
-            // densest region of that grid is the anti-friendly answer.
-            if (!cell.countGrids.TryGetValue(friendlyDomain, out var grid) || grid == null)
+            // Cell.countGrids[bucketDomain] semantics:
+            //   Jade/Ruby/Gold = every block NOT in that team (anti-team bucket)
+            //   Blue           = every block regardless of team (all-domain wildcard)
+            if (!cell.countGrids.TryGetValue(bucketDomain, out var grid) || grid == null)
                 return false;
+            if (grid.values == null) return false;
+
+            int n = grid.values.GetLength(0);
+            if (n <= 0) return false;
+
+            int bestSum = 0;
+            int bx = 0, by = 0, bz = 0;
+
+            for (int x = 0; x < n; x++)
+            {
+                int xMin = x - radius; if (xMin < 0) xMin = 0;
+                int xMax = x + radius; if (xMax >= n) xMax = n - 1;
+
+                for (int y = 0; y < n; y++)
+                {
+                    int yMin = y - radius; if (yMin < 0) yMin = 0;
+                    int yMax = y + radius; if (yMax >= n) yMax = n - 1;
+
+                    for (int z = 0; z < n; z++)
+                    {
+                        int zMin = z - radius; if (zMin < 0) zMin = 0;
+                        int zMax = z + radius; if (zMax >= n) zMax = n - 1;
+
+                        int sum = 0;
+                        for (int xi = xMin; xi <= xMax; xi++)
+                            for (int yi = yMin; yi <= yMax; yi++)
+                                for (int zi = zMin; zi <= zMax; zi++)
+                                    sum += grid.values[xi, yi, zi];
+
+                        if (sum > bestSum)
+                        {
+                            bestSum = sum;
+                            bx = x; by = y; bz = z;
+                        }
+                    }
+                }
+            }
 
-            var pos = grid.FindDensestRegion();
```

</details>

### `0bd6b42f3` — fix(gameplay): symmetric Cell.AddBlock/RemoveBlock via add-time domain

_Claude, 2026-05-09 00:29:17 +0000_

```text
HealthBlockTracker.Add calls cell.AddBlock(hp) before hp.ChangeTeam(domain),
so block.Domain at add-time is whatever PrismTeamManager defaults to
(Domains.Blue) and not the team the prism will live and die as. AddBlock's
'if (t != block.Domain)' loop therefore added the prism to all three
team buckets [Jade], [Ruby], [Gold] (Blue != any of them). Then ChangeTeam
reassigned the domain.

When the prism died, RemoveBlock used block.Domain at remove-time (the
post-ChangeTeam value), so a Jade-team prism's death decremented [Ruby]
and [Gold] but skipped [Jade] — leaving a stale +1 in countGrids[Jade]
forever. Over a long-running ecosystem these stale +1s accumulated at
historical death sites, dragging anti-Jade peaks away from current mass.

Cell now snapshots block.Domain at AddBlock time into a Dictionary<Prism,
Domains> and uses that snapshot symmetrically on remove. Same buckets
incremented at add time get decremented at remove time, regardless of
mid-life ChangeTeam calls. countGrids[Blue] is unaffected (added/removed
unconditionally) and continues to track all-prism mass.

domainBlockCounts also uses the add-time domain, since underflowing
[Jade] -1 and inflating [Blue] +1 by mismatched calls was breaking
DominantDomain too.

Falls back to live block.Domain if no add-time snapshot is found, which
preserves correctness for any code path that calls RemoveBlock on a
prism that was never added through the new path (e.g. legacy code or
historic in-flight prisms during a hot-reload).
```

```text
 Assets/_Scripts/Controller/Environment/Cell.cs | 34 ++++++++++++++++++++++++++++------
 1 file changed, 28 insertions(+), 6 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/Cell.cs b/Assets/_Scripts/Controller/Environment/Cell.cs
index 21db775f5..3e88bd509 100644
--- a/Assets/_Scripts/Controller/Environment/Cell.cs
+++ b/Assets/_Scripts/Controller/Environment/Cell.cs
@@ -65,6 +65,14 @@ namespace CosmicShore.Gameplay
 
         readonly List<GameObject> spawnedLifeForms = new();
         readonly HashSet<Prism> trackedBlocks = new();
+        // Snapshots block.Domain at the moment AddBlock was called so RemoveBlock
+        // can decrement exactly the same buckets that AddBlock incremented.
+        // Without this, prisms added when their PrismTeamManager still reads
+        // Domains.Blue (the default before HealthBlockTracker.Add → ChangeTeam
+        // runs) and removed after their team has been assigned would leave a
+        // stale +1 in countGrids[NewTeam] forever — anti-NewTeam answers drift
+        // toward historical death sites instead of current mass.
+        readonly Dictionary<Prism, Domains> addTimeDomains = new();
         SnowChanger spawnedCytoplasm;
 
         CellPhase phase = CellPhase.Sprout;
@@ -300,6 +308,7 @@ namespace CosmicShore.Gameplay
             }
             spawnedLifeForms.Clear();
             trackedBlocks.Clear();
+            addTimeDomains.Clear();
             domainBlockCounts.Clear();
             phase = CellPhase.Sprout;
 
@@ -356,6 +365,7 @@ namespace CosmicShore.Gameplay
         {
             spawnedLifeForms.Clear();
             trackedBlocks.Clear();
+            addTimeDomains.Clear();
             domainBlockCounts.Clear();
             phase = CellPhase.Sprout;
 
@@ -504,15 +514,18 @@ namespace CosmicShore.Gameplay
 
             if (block)
             {
+                Domains addDomain = block.Domain;
+                addTimeDomains[block] = addDomain;
+
                 Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold };
                 foreach (var t in teams)
-                    if (t != block.Domain) countGrids[t].AddBlock(block);
+                    if (t != addDomain) countGrids[t].AddBlock(block);
 
                 if (countGrids.TryGetValue(Domains.Blue, out var anyGrid))
                     anyGrid.AddBlock(block);
 
-                domainBlockCounts.TryGetValue(block.Domain, out int count);
-                domainBlockCounts[block.Domain] = count + 1;
+                domainBlockCounts.TryGetValue(addDomain, out int count);
+                domainBlockCounts[addDomain] = count + 1;
             }
         }
 
@@ -523,15 +536,24 @@ namespace CosmicShore.Gameplay
 
             if (block)
             {
+                // Use the add-time domain so we decrement exactly the buckets
+                // we incremented. Falling back to the live block.Domain is a
+                // best-effort for prisms that were added before this snapshot
+                // mechanism existed (or removed without a matching add).
+                Domains removeDomain;
+                if (!addTimeDomains.TryGetValue(block, out removeDomain))
+                    removeDomain = block.Domain;
+                addTimeDomains.Remove(block);
+
                 Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold };
                 foreach (Domains t in teams)
-                    if (t != block.Domain) countGrids[t].RemoveBlock(block);
+                    if (t != removeDomain) countGrids[t].RemoveBlock(block);
 
                 if (countGrids.TryGetValue(Domains.Blue, out var anyGrid))
                     anyGrid.RemoveBlock(block);
 
-                if (domainBlockCounts.TryGetValue(block.Domain, out int count) && count > 0)
-                    domainBlockCounts[block.Domain] = count - 1;
+                if (domainBlockCounts.TryGetValue(removeDomain, out int count) && count > 0)
+                    domainBlockCounts[removeDomain] = count - 1;
             }
         }
 
```

</details>
