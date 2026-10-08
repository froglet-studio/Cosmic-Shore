# Branch archive: `claude/prism-grid-explosion-scene-bi74f9`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-08-01 by Claude
- **Unmerged commits:** 2
- **Forked from:** `bd6a8b405` (2026-07-23, last mix push)
- **Tip:** `cf3824206`
- **Files touched (6):**
  - `Assets/_Scripts/Editor/PrismGridTestSceneSetupTool.cs`
  - `Assets/_Scripts/Editor/PrismGridTestSceneSetupTool.cs.meta`
  - `Assets/_Scripts/ScriptableObjects/PrismGridTestConfigSO.cs`
  - `Assets/_Scripts/ScriptableObjects/PrismGridTestConfigSO.cs.meta`
  - `Assets/_Scripts/Utility/Tools/PrismGridExplosionHarness.cs`
  - `Assets/_Scripts/Utility/Tools/PrismGridExplosionHarness.cs.meta`

### `8436342fc` — feat(tools): add prism-grid explosion test scene harness

_Claude, 2026-07-24 21:54:13 +0000_

```text
Adds a self-contained rig for putting a known quantity of real prisms on
screen and detonating a large AOE blast into them - the gap between the two
existing prism harnesses. PrismRenderStressTest/PrismStressInjector spawn a
render-only ECS cloud (nothing damageable), and AOEBenchmarkRunner registers
synthetic index entries with damage application deliberately excluded. This
exercises the whole chain: mass spawn + grow-in, instanced rendering at a
known count, and Burst AOE damage WITH real destruction.

- PrismGridExplosionHarness: spawns a cuboid lattice centred on the origin
  (per-axis counts + centre-to-centre gap A), fires the blast at the origin,
  and drives a zoom slider that keeps the camera pointed at the origin.
  Code-built uGUI canvas, matching DiagnosticsHUD.BuildUI's idiom.
- PrismGridTestConfigSO: all tunables per the config-separation rule.
- PrismGridTestSceneSetupTool: Tools > Cosmic Shore > Setup Prism Grid
  Explosion Scene - authors the config asset and the scene, including the
  PrismManagers prefab instance that PrismScaleManager/MaterialStateManager
  need (Singleton<T> never auto-creates).

Uses the base spherical AOEExplosion at the Dolphin's giant MaxScale of 800
rather than its literal AOEConicExplosion: the cone hard-requires a live
vessel, is directional, and damages prisms through physics triggers only,
bypassing the Burst PrismSpatialIndex path this scene exists to measure.
Grid and blast domains are separate fields because AOEExplosion ships
affectSelf=false, so a same-domain blast shields prisms instead of killing
them - the harness warns when they match.

Ecosystem invariants: prisms grow in via the normal Prism.Initialize path and
are removed only by an active force. No TTL, decay, or idle culler. Clear
suctions the lattice toward the origin before freeing it, mirroring
Microscene.RecycleAsync's sanctioned continuity transition, rather than
per-prism Damage - which would mint one explosion VFX per prism.

Spawning goes through PrismTrailBuilder.LayBatched, the canonical
lay-a-prism primitive; performance readout and recording ride the
auto-spawning DiagnosticsHUD, so no perf overlay is duplicated.
```

```text
 Assets/_Scripts/Editor/PrismGridTestSceneSetupTool.cs           | 205 +++++++++++
 Assets/_Scripts/Editor/PrismGridTestSceneSetupTool.cs.meta      |  11 +
 Assets/_Scripts/ScriptableObjects/PrismGridTestConfigSO.cs      | 112 ++++++
 Assets/_Scripts/ScriptableObjects/PrismGridTestConfigSO.cs.meta |  11 +
 Assets/_Scripts/Utility/Tools/PrismGridExplosionHarness.cs      | 733 ++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Utility/Tools/PrismGridExplosionHarness.cs.meta |  11 +
 6 files changed, 1083 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 150 of 1068 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Editor/PrismGridTestSceneSetupTool.cs b/Assets/_Scripts/Editor/PrismGridTestSceneSetupTool.cs
new file mode 100644
index 000000000..fec3ebad2
--- /dev/null
+++ b/Assets/_Scripts/Editor/PrismGridTestSceneSetupTool.cs
@@ -0,0 +1,205 @@
+using CosmicShore.Gameplay;
+using CosmicShore.ScriptableObjects;
+using CosmicShore.Utility;
+using UnityEditor;
+using UnityEditor.SceneManagement;
+using UnityEngine;
+using UnityEngine.SceneManagement;
+
+namespace CosmicShore.Editor
+{
+    /// <summary>
+    /// One-click setup for the prism-grid explosion test scene. It:
+    ///   1. authors <c>Assets/Resources/PrismGridTestConfig.asset</c> and points it at the default
+    ///      prism prefab (Dolphin) and the base spherical AOE explosion prefab,
+    ///   2. creates/opens <c>Assets/_Scenes/Game_TestDesign/PrismGridExplosionTest.unity</c>, and
+    ///   3. populates it with the minimum the rig needs: a plain Main Camera, a Directional Light,
+    ///      an instance of <c>PrismManagers.prefab</c> (PrismScaleManager / MaterialStateManager are
+    ///      Singleton&lt;T&gt;, which never auto-creates), and the harness GameObject.
+    ///
+    /// Idempotent — safe to re-run; existing objects are reused rather than duplicated.
+    ///
+    /// The scene is deliberately NOT added to Build Settings, matching every other
+    /// Game_TestDesign scene (Bootstrap must stay at index 0 for SceneBootstrapper). Add it only if
+    /// you want it in the Performance Benchmark's automatic multi-scene sweep.
+    ///
+    /// No DiagnosticsHUD wiring is needed or wanted: it auto-spawns in every scene via
+    /// [RuntimeInitializeOnLoadMethod] — F7 overlay, F5 records to Documents/CosmicShore Diagnostics/.
+    /// </summary>
+    public static class PrismGridTestSceneSetupTool
+    {
+        const string ResourcesFolder = "Assets/Resources";
+        const string ConfigAssetPath = "Assets/Resources/PrismGridTestConfig.asset";
+        const string SceneFolder = "Assets/_Scenes/Game_TestDesign";
+        const string ScenePath = "Assets/_Scenes/Game_TestDesign/PrismGridExplosionTest.unity";
+
+        const string PrismPrefabPath = "Assets/_Prefabs/Trails/Prisms With Pools/Dolphin Prism.prefab";
+        const string ExplosionPrefabPath = "Assets/_Prefabs/Projectile/AOEExplosion.prefab";
+        const string PrismManagersPrefabPath = "Assets/_Prefabs/Environment/PrismManagers.prefab";
+
+        [MenuItem("Tools/Cosmic Shore/Setup Prism Grid Explosion Scene")]
+        static void SetupScene()
+        {
+            // The scene is opened Single, so give the user a chance to keep whatever they had open.
+            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
+
+            var config = LoadOrCreateConfig();
+
+            AssetDatabase.SaveAssets();
+            AssetDatabase.Refresh();
+
+            string report = BuildScene(config);
+
+            EditorUtility.DisplayDialog("Setup Prism Grid Explosion Scene",
+                $"Config: {ConfigAssetPath}\nScene: {ScenePath}\n\n{report}\n\n" +
+                "Before pressing Play, disable Bootstrap auto-load:\n" +
+                "Tools > Cosmic Shore > Testing Multiplayer > Do not load Bootstrap Scene on Play\n\n" +
+                "In Play mode: F7 shows the DiagnosticsHUD, F5 records a report to " +
+                "Documents/CosmicShore Diagnostics/.",
+                "OK");
+        }
+
+        // ── Config asset ─────────────────────────────────────────────────────
+
+        static PrismGridTestConfigSO LoadOrCreateConfig()
+        {
+            EnsureFolder(ResourcesFolder);
+
+            var config = AssetDatabase.LoadAssetAtPath<PrismGridTestConfigSO>(ConfigAssetPath);
+            if (!config)
+            {
+                config = ScriptableObject.CreateInstance<PrismGridTestConfigSO>();
+                AssetDatabase.CreateAsset(config, ConfigAssetPath);
+            }
+
+            var so = new SerializedObject(config);
+
+            // Only fill unset references, so a re-run never stomps a deliberate retarget.
+            SetObjectIfEmpty(so, "prismPrefab",
+                AssetDatabase.LoadAssetAtPath<Prism>(PrismPrefabPath));
+            SetObjectIfEmpty(so, "explosionPrefab",
+                AssetDatabase.LoadAssetAtPath<AOEExplosion>(ExplosionPrefabPath));
+
+            so.ApplyModifiedProperties();
+            EditorUtility.SetDirty(config);
+            return config;
+        }
+
+        // ── Scene ────────────────────────────────────────────────────────────
+
+        static string BuildScene(PrismGridTestConfigSO config)
+        {
+            EnsureFolder(SceneFolder);
+
+            bool existed = System.IO.File.Exists(ScenePath);
+            var scene = existed
+                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
+                : EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
+
+            var camera = EnsureCamera(scene);
+            EnsureLight(scene);
+            bool managersAdded = EnsurePrismManagers(scene);
+            EnsureHarness(scene, config, camera);
+
+            EditorSceneManager.MarkSceneDirty(scene);
+            EditorSceneManager.SaveScene(scene, ScenePath);
+
+            return existed
+                ? $"Updated existing scene. PrismManagers {(managersAdded ? "added" : "already present")}."
+                : $"Created new scene. PrismManagers {(managersAdded ? "added" : "MISSING — check " + PrismManagersPrefabPath)}.";
+        }
+
+        static Camera EnsureCamera(Scene scene)
+        {
+            var camera = Object.FindFirstObjectByType<Camera>();
+            if (camera == null)
+            {
+                var go = NewRoot("Main Camera", scene);
+                go.tag = "MainCamera";
+                camera = Undo.AddComponent<Camera>(go);
+            }
+
+            // The lattice can reach thousands of units across; the stock 1000 far plane clips it.
+            camera.farClipPlane = Mathf.Max(camera.farClipPlane, 20000f);
+            camera.transform.SetPositionAndRotation(new Vector3(0f, 0f, -600f), Quaternion.identity);
+            EditorUtility.SetDirty(camera);
+            return camera;
+        }
+
+        static void EnsureLight(Scene scene)
+        {
+            if (Object.FindFirstObjectByType<Light>() != null) return;
+
+            var go = NewRoot("Directional Light", scene);
+            var light = Undo.AddComponent<Light>(go);
+            light.type = LightType.Directional;
+            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
+        }
+
+        static bool EnsurePrismManagers(Scene scene)
+        {
+            // PrismScaleManager / MaterialStateManager are Singleton<T> — they never auto-create,
+            // so without this prefab prisms spawn but never animate or theme.
+            if (Object.FindFirstObjectByType<PrismScaleManager>() != null) return false;
+
```

</details>

### `cf3824206` — fix(tools): report prism-grid readiness by index registration, not instantiation

_Claude, 2026-08-01 08:50:46 +0000_

```text
Adversarial review found the rig's headline number was systematically wrong
for the first ~17s of every run.

Prism gates creation COMPLETION behind a static, process-wide budget of 6 per
frame (Prism.MaxCreationCompletionsPerFrame), and only past that gate does a
prism become visible, collider-enabled, and registered in PrismSpatialIndex.
Laying 6,000 prisms takes ~30 frames; registering them takes ~1,000. The
harness treated LayBatched completing as "the lattice is up" and flipped the
readout to "live 6,000" while the index still held a few hundred entries.

That is not cosmetic: AOEExplosion.ExplodeAsync calls BeginBatchProcessing and
ApplyPrismExclusion to deliberately remove prisms from PhysX, damaging solely
through PrismSpatialIndex. An unregistered prism has no index slot AND a
disabled collider, so it is unreachable by both the Burst path and the physics
fallback. Detonating on the old readout measured a lattice that wasn't there.

- Split the lifecycle into an explicit GridPhase (Idle/Laying/Materializing/
  Ready). Spawn now waits for registration before declaring Ready, with a
  stall guard so prisms dying mid-wait can't hang it.
- Readout and HUD report "laid" and "indexed" separately; Materializing says
  so and tells the operator to wait.
- Explode warns when fired before Ready, naming how many prisms can actually
  be hit.
- Count registration over our own prism list rather than the global
  PrismSpatialIndex.LiveCount: a previous lattice still suctioning out would
  inflate the global count and end the wait early.
- Warnings are now sticky for a few seconds; they were being wiped by the
  next PublishStats, which lands exactly when the operator looks up.
- Husk sweep runs only in Ready - nothing is destroyed while laying or
  materializing, so sweeping then was pure overhead inside the measurement.
- Centre the zoom slider handle pivot: Slider.UpdateVisuals overwrites the
  handle's anchors but never its pivot, so the shared CreateRect pivot of
  (0,1) hung it below the track and pushed it fully outside at value 1.

17 other findings were raised and refuted on verification.
```

```text
 Assets/_Scripts/Utility/Tools/PrismGridExplosionHarness.cs | 173 +++++++++++++++++++++++++++++++++++++------
 1 file changed, 151 insertions(+), 22 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 306 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Tools/PrismGridExplosionHarness.cs b/Assets/_Scripts/Utility/Tools/PrismGridExplosionHarness.cs
index 197ecb8da..097bdeefa 100644
--- a/Assets/_Scripts/Utility/Tools/PrismGridExplosionHarness.cs
+++ b/Assets/_Scripts/Utility/Tools/PrismGridExplosionHarness.cs
@@ -53,6 +53,34 @@ namespace CosmicShore.Utility
         /// <summary>Suction end-scale — never exactly zero, so lossyScale stays well-formed.</summary>
         const float SuctionScale = 0.002f;
 
+        /// <summary>
+        /// Seconds the materialization wait tolerates a stalled index count before declaring the
+        /// lattice ready anyway. Guards against waiting forever when prisms died during spawn (so
+        /// the indexed count can never reach the requested count).
+        /// </summary>
+        const float MaterializeStallSeconds = 2f;
+
+        /// <summary>How long a warning stays pinned under the readout.</summary>
+        const float WarningSeconds = 6f;
+
+        /// <summary>
+        /// Lattice lifecycle. Laying (instantiating) and materializing (becoming visible, collidable
+        /// and registered in PrismSpatialIndex) are SEPARATE and wildly different in duration:
+        /// Prism gates creation completion behind a static, process-wide budget of 6 per frame
+        /// (Prism.MaxCreationCompletionsPerFrame), so 6,000 prisms are laid in ~30 frames but take
+        /// ~1,000 frames to register. Until a prism registers it has no spatial-index slot AND a
+        /// disabled collider, so a blast fired early is invisible to it through both the Burst path
+        /// and the physics fallback. Conflating the two would make every early run measure a lattice
+        /// that is not there yet.
+        /// </summary>
+        enum GridPhase
+        {
+            Idle = 0,
+            Laying = 1,
+            Materializing = 2,
+            Ready = 3,
+        }
+
         [Header("Configuration")]
         [Tooltip("Tunables asset. When empty the harness falls back to any PrismGridTestConfigSO in " +
                  "Resources, so the rig still runs if the scene reference is lost.")]
@@ -67,7 +95,7 @@ namespace CosmicShore.Utility
         Transform _gridRoot;
         readonly List<Prism> _prisms = new();
         CancellationTokenSource _spawnCts;
-        bool _spawning;
+        GridPhase _phase = GridPhase.Idle;
         int _laid;
         int _requested;
         int _sweepCursor;
@@ -82,6 +110,8 @@ namespace CosmicShore.Utility
         InputField _countXInput, _countYInput, _countZInput, _gapInput;
         Slider _zoomSlider;
         Text _readout;
+        string _warning;
+        float _warningUntil;
 
         void Awake()
         {
@@ -122,7 +152,9 @@ namespace CosmicShore.Utility
 
         void Update()
         {
-            if (!_spawning) ReclaimHusks();
+            // Only sweep once the lattice has settled: nothing is destroyed while laying or
+            // materializing, so a sweep then is pure overhead inside the measurement window.
+            if (_phase == GridPhase.Ready) ReclaimHusks();
         }
 
         void OnDestroy()
@@ -173,6 +205,26 @@ namespace CosmicShore.Utility
             if (changed) PublishStats();
         }
 
+        /// <summary>
+        /// Prisms of THIS lattice that have actually registered with PrismSpatialIndex — the number
+        /// the blast can reach, and the only honest measure of readiness.
+        ///
+        /// Counted over our own list rather than read off <c>PrismSpatialIndex.LiveCount</c>: that
+        /// count is global, so a previous lattice still suctioning out would inflate it and end the
+        /// materialization wait early. The scan is a few thousand int reads — negligible next to the
+        /// ~6-per-frame registration it is waiting on.
+        /// </summary>
+        int IndexedCount()
+        {
+            int n = 0;
+            for (int i = 0; i < _prisms.Count; i++)
+            {
+                var prism = _prisms[i];
+                if (prism != null && !prism.destroyed && prism.SpatialIndexId >= 0) n++;
+            }
+            return n;
+        }
+
         // ── Grid geometry ────────────────────────────────────────────────────
 
         /// <summary>
@@ -219,21 +271,21 @@ namespace CosmicShore.Utility
         {
             if (config.PrismPrefab == null)
             {
-                SetReadout("<color=#ff8080>No prism prefab configured.</color>");
+                Warn("No prism prefab configured.");
                 return;
             }
 
             long total = (long)_counts.x * _counts.y * _counts.z;
             if (total <= 0)
             {
-                SetReadout("<color=#ff8080>Counts must all be >= 1.</color>");
+                Warn("Counts must all be >= 1.");
                 return;
             }
 
             if (total > config.MaxTotalPrisms)
             {
-                SetReadout($"<color=#ff8080>{total:N0} prisms exceeds the {config.MaxTotalPrisms:N0} " +
-                           "cap (PrismGridTestConfig.maxTotalPrisms).</color>");
+                Warn($"{total:N0} prisms exceeds the {config.MaxTotalPrisms:N0} cap " +
+                     "(PrismGridTestConfig.maxTotalPrisms).");
                 return;
             }
 
@@ -250,7 +302,7 @@ namespace CosmicShore.Utility
             var lays = BuildLays();
             _requested = lays.Count;
             _laid = 0;
-            _spawning = true;
+            _phase = GridPhase.Laying;
 
             _gridRoot = new GameObject(GridRootName).transform;
             _gridRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
@@ -268,6 +320,10 @@ namespace CosmicShore.Utility
                 await PrismTrailBuilder.LayBatched(
                     config.PrismPrefab, lays, _gridRoot, _trail,
                     OwnerPrefix, config.PrismsPerFrame, ct, _prisms);
+
+                _laid = _prisms.Count;
+                _phase = GridPhase.Materializing;
+                await WaitForMaterializationAsync(ct);
             }
             catch (OperationCanceledException)
             {
@@ -275,17 +331,52 @@ namespace CosmicShore.Utility
             }
             finally
             {
-                _spawning = false;
-                _laid = _prisms.Count;
+                // Only settle state if this run still owns the lattice; a Clear/re-Spawn during the
+                // await has already moved on, and stomping _phase here would lie about that one.
+                if (_spawnCts != null && _spawnCts.Token == ct)
```

</details>
