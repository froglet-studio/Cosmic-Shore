# Branch archive: `claude/serene-cannon-iSydG`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-06-01 by Claude
- **Unmerged commits:** 5
- **Forked from:** `e81a9a5e7` (2026-05-22, Merge pull request #528 from froglet-studio/claude/kind-hawking-jCpSO)
- **Tip:** `f1dd85a92`
- **Files touched (33):**
  - `Assets/CosmicShore.meta`
  - `Assets/CosmicShore/Splats.meta`
  - `Assets/CosmicShore/Splats/Editor.meta`
  - `Assets/CosmicShore/Splats/Editor/SplatArcadeSetup.cs`
  - `Assets/CosmicShore/Splats/Editor/SplatArcadeSetup.cs.meta`
  - `Assets/CosmicShore/Splats/Editor/SplatBakeWindow.cs`
  - `Assets/CosmicShore/Splats/Editor/SplatBakeWindow.cs.meta`
  - `Assets/CosmicShore/Splats/Editor/SplatDecimator.cs`
  - `Assets/CosmicShore/Splats/Editor/SplatDecimator.cs.meta`
  - `Assets/CosmicShore/Splats/Editor/SplatPlyImporter.cs`
  - `Assets/CosmicShore/Splats/Editor/SplatPlyImporter.cs.meta`
  - `Assets/CosmicShore/Splats/Editor/SplatSandboxSceneBuilder.cs`
  - `Assets/CosmicShore/Splats/Editor/SplatSandboxSceneBuilder.cs.meta`
  - `Assets/CosmicShore/Splats/Editor/SplatSyntheticTest.cs`
  - `Assets/CosmicShore/Splats/Editor/SplatSyntheticTest.cs.meta`
  - `Assets/CosmicShore/Splats/Editor/Tests.meta`
  - `Assets/CosmicShore/Splats/Editor/Tests/SplatDecimatorTests.cs`
  - `Assets/CosmicShore/Splats/Editor/Tests/SplatDecimatorTests.cs.meta`
  - `Assets/CosmicShore/Splats/Editor/Tests/SplatPlyImporterTests.cs`
  - `Assets/CosmicShore/Splats/Editor/Tests/SplatPlyImporterTests.cs.meta`
  - `Assets/CosmicShore/Splats/Runtime.meta`
  - `Assets/CosmicShore/Splats/Runtime/SplatCloudRuntimeLoader.cs`
  - `Assets/CosmicShore/Splats/Runtime/SplatCloudRuntimeLoader.cs.meta`
  - `Assets/CosmicShore/Splats/Runtime/SplatPoint.cs`
  - `Assets/CosmicShore/Splats/Runtime/SplatPoint.cs.meta`
  - `Assets/CosmicShore/Splats/Runtime/SplatPrismSet.cs`
  - `Assets/CosmicShore/Splats/Runtime/SplatPrismSet.cs.meta`
  - `Assets/CosmicShore/Splats/Runtime/SplatPrismSpawner.cs`
  - `Assets/CosmicShore/Splats/Runtime/SplatPrismSpawner.cs.meta`
  - `Assets/CosmicShore/Splats/Runtime/SplatSandboxFlyCam.cs`
  - `Assets/CosmicShore/Splats/Runtime/SplatSandboxFlyCam.cs.meta`
  - `Assets/_Scripts/Data/Enums/GameModes.cs`
  - `Assets/_Scripts/System/Progression/GameModeProgressionService.cs`

### `8ee3d70da` — feat(splats): add PLY -> prism bake pipeline (HyperSea oddity prototype)

_Claude, 2026-05-24 03:45:24 +0000_

```text
Editor tooling parses 3DGS .ply files (binary little-endian, header-driven),
decodes the encoded fields (scale=exp, opacity=sigmoid, color=0.5+C0*f_dc,
w-first quat), crops to a centred fraction or explicit AABB, downsamples
via importance-weighted voxel grid, and caps at 10k splats. Output is a
SplatPrismSet ScriptableObject the runtime SplatPrismSpawner consumes,
instantiating one prism GameObject per splat with per-instance Material
and Box or convex MeshCollider.

Includes:
- Tools/Splats/Bake Prism Set EditorWindow with all knobs exposed
- Tools/Splats/Run Synthetic Pipeline Test for asset-free validation
- NUnit edit-mode tests covering decode math, voxel coalescing, hard cap,
  crop modes, handedness flip, and a synthetic-binary-PLY round trip
```

```text
 Assets/CosmicShore.meta                                              |   8 +
 Assets/CosmicShore/Splats.meta                                       |   8 +
 Assets/CosmicShore/Splats/Editor.meta                                |   8 +
 Assets/CosmicShore/Splats/Editor/SplatBakeWindow.cs                  | 165 ++++++++++++++++++
 Assets/CosmicShore/Splats/Editor/SplatBakeWindow.cs.meta             |  11 ++
 Assets/CosmicShore/Splats/Editor/SplatDecimator.cs                   | 269 +++++++++++++++++++++++++++++
 Assets/CosmicShore/Splats/Editor/SplatDecimator.cs.meta              |  11 ++
 Assets/CosmicShore/Splats/Editor/SplatPlyImporter.cs                 | 306 +++++++++++++++++++++++++++++++++
 Assets/CosmicShore/Splats/Editor/SplatPlyImporter.cs.meta            |  11 ++
 Assets/CosmicShore/Splats/Editor/SplatSyntheticTest.cs               | 101 +++++++++++
 Assets/CosmicShore/Splats/Editor/SplatSyntheticTest.cs.meta          |  11 ++
 Assets/CosmicShore/Splats/Editor/Tests.meta                          |   8 +
 Assets/CosmicShore/Splats/Editor/Tests/SplatDecimatorTests.cs        | 212 +++++++++++++++++++++++
 Assets/CosmicShore/Splats/Editor/Tests/SplatDecimatorTests.cs.meta   |  11 ++
 Assets/CosmicShore/Splats/Editor/Tests/SplatPlyImporterTests.cs      | 176 +++++++++++++++++++
 Assets/CosmicShore/Splats/Editor/Tests/SplatPlyImporterTests.cs.meta |  11 ++
 Assets/CosmicShore/Splats/Runtime.meta                               |   8 +
 Assets/CosmicShore/Splats/Runtime/SplatPoint.cs                      |  15 ++
 Assets/CosmicShore/Splats/Runtime/SplatPoint.cs.meta                 |  11 ++
 Assets/CosmicShore/Splats/Runtime/SplatPrismSet.cs                   |  29 ++++
 Assets/CosmicShore/Splats/Runtime/SplatPrismSet.cs.meta              |  11 ++
 Assets/CosmicShore/Splats/Runtime/SplatPrismSpawner.cs               | 222 ++++++++++++++++++++++++
 Assets/CosmicShore/Splats/Runtime/SplatPrismSpawner.cs.meta          |  11 ++
 23 files changed, 1634 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 150 of 1549 lines)</summary>

```diff
diff --git a/Assets/CosmicShore/Splats/Editor/SplatBakeWindow.cs b/Assets/CosmicShore/Splats/Editor/SplatBakeWindow.cs
new file mode 100644
index 000000000..7f5bb3966
--- /dev/null
+++ b/Assets/CosmicShore/Splats/Editor/SplatBakeWindow.cs
@@ -0,0 +1,165 @@
+using System;
+using System.IO;
+using UnityEditor;
+using UnityEngine;
+using CosmicShore.Splats;
+
+namespace CosmicShore.Tools.SplatImport
+{
+    public class SplatBakeWindow : EditorWindow
+    {
+        private string _plyPath;
+        private string _outputAssetPath = "Assets/CosmicShore/Splats/Baked/SplatPrismSet.asset";
+        private SplatDecimateSettings _settings = SplatDecimateSettings.Default;
+        private Vector2 _scroll;
+        private string _lastReport;
+
+        [MenuItem("Tools/Splats/Bake Prism Set")]
+        public static void Open()
+        {
+            var w = GetWindow<SplatBakeWindow>("Splat -> Prism Set");
+            w.minSize = new Vector2(420, 480);
+        }
+
+        [MenuItem("Tools/Splats/Run Synthetic Pipeline Test")]
+        public static void RunSyntheticPipelineTest()
+        {
+            string outAssetPath = "Assets/CosmicShore/Splats/Baked/SplatPrismSet_Synthetic.asset";
+            var (raw, _) = SplatSyntheticTest.MakeSyntheticSplats(50);
+            var settings = SplatDecimateSettings.Default;
+            settings.voxelSize = 0.1f;
+            settings.maxPrisms = 1000;
+            var points = SplatDecimator.DecimateToPoints(raw, settings, out var report);
+            Debug.Log($"[SplatBake][Synthetic] {report.ToLogString()}");
+            SplatSyntheticTest.AssertDecodedReasonable(points);
+            SaveBakedAsset(outAssetPath, points, report, settings, sourcePath: "<synthetic>");
+            Debug.Log($"[SplatBake][Synthetic] Saved {points.Length} points to {outAssetPath}.");
+        }
+
+        private void OnGUI()
+        {
+            _scroll = EditorGUILayout.BeginScrollView(_scroll);
+            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
+            EditorGUILayout.BeginHorizontal();
+            _plyPath = EditorGUILayout.TextField(".ply path", _plyPath);
+            if (GUILayout.Button("Browse", GUILayout.Width(70)))
+            {
+                string chosen = EditorUtility.OpenFilePanel("Pick a .ply", string.IsNullOrEmpty(_plyPath) ? Application.dataPath : Path.GetDirectoryName(_plyPath), "ply");
+                if (!string.IsNullOrEmpty(chosen)) _plyPath = chosen;
+            }
+            EditorGUILayout.EndHorizontal();
+
+            EditorGUILayout.Space();
+            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
+            _outputAssetPath = EditorGUILayout.TextField("Asset path", _outputAssetPath);
+
+            EditorGUILayout.Space();
+            EditorGUILayout.LabelField("Decode", EditorStyles.boldLabel);
+            _settings.flipHandedness = EditorGUILayout.Toggle("Flip Handedness (Z)", _settings.flipHandedness);
+            _settings.minOpacity = EditorGUILayout.Slider("Min Opacity (pre-decimate)", _settings.minOpacity, 0f, 1f);
+            EditorGUILayout.HelpBox("Global size tweak (scaleMultiplier) lives on SplatPrismSpawner at spawn time so you can iterate without re-baking.", MessageType.None);
+
+            EditorGUILayout.Space();
+            EditorGUILayout.LabelField("Crop", EditorStyles.boldLabel);
+            _settings.cropMode = (SplatCropMode)EditorGUILayout.EnumPopup("Crop Mode", _settings.cropMode);
+            if (_settings.cropMode == SplatCropMode.CenteredFraction)
+            {
+                _settings.cropFraction = EditorGUILayout.Slider("Crop Fraction", _settings.cropFraction, 0.01f, 1f);
+            }
+            else if (_settings.cropMode == SplatCropMode.ExplicitAABB)
+            {
+                _settings.cropMin = EditorGUILayout.Vector3Field("Crop Min", _settings.cropMin);
+                _settings.cropMax = EditorGUILayout.Vector3Field("Crop Max", _settings.cropMax);
+            }
+
+            EditorGUILayout.Space();
+            EditorGUILayout.LabelField("Decimate", EditorStyles.boldLabel);
+            _settings.voxelSize = EditorGUILayout.FloatField("Voxel Size", _settings.voxelSize);
+            _settings.maxPrisms = EditorGUILayout.IntField("Max Prisms (hard cap 10000)", _settings.maxPrisms);
+            if (_settings.maxPrisms > SplatPrismSet.MaxPrisms)
+                _settings.maxPrisms = SplatPrismSet.MaxPrisms;
+
+            EditorGUILayout.Space();
+            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(_plyPath));
+            if (GUILayout.Button("Bake", GUILayout.Height(36)))
+            {
+                try
+                {
+                    Bake();
+                }
+                catch (Exception ex)
+                {
+                    Debug.LogException(ex);
+                    EditorUtility.DisplayDialog("Bake failed", ex.Message, "OK");
+                }
+            }
+            EditorGUI.EndDisabledGroup();
+
+            if (GUILayout.Button("Run Synthetic Pipeline Test"))
+                RunSyntheticPipelineTest();
+
+            if (!string.IsNullOrEmpty(_lastReport))
+            {
+                EditorGUILayout.Space();
+                EditorGUILayout.LabelField("Last Report", EditorStyles.boldLabel);
+                EditorGUILayout.HelpBox(_lastReport, MessageType.Info);
+            }
+
+            EditorGUILayout.EndScrollView();
+        }
+
+        private void Bake()
+        {
+            EditorUtility.DisplayProgressBar("Bake Splat Prism Set", "Parsing PLY ...", 0.05f);
+            try
+            {
+                var imported = SplatPlyImporter.Load(_plyPath);
+                EditorUtility.DisplayProgressBar("Bake Splat Prism Set", "Decoding + decimating ...", 0.5f);
+                var points = SplatDecimator.DecimateToPoints(imported.splats, _settings, out var report);
+                EditorUtility.DisplayProgressBar("Bake Splat Prism Set", "Saving asset ...", 0.9f);
+                SaveBakedAsset(_outputAssetPath, points, report, _settings, _plyPath);
+                _lastReport = report.ToLogString();
+                Debug.Log($"[SplatBake] Saved {points.Length} prisms to {_outputAssetPath}\n{_lastReport}");
+            }
+            finally
+            {
+                EditorUtility.ClearProgressBar();
+            }
+        }
+
+        public static SplatPrismSet SaveBakedAsset(string assetPath, SplatPoint[] points, DecimateReport report, SplatDecimateSettings settings, string sourcePath)
+        {
+            if (points.Length > SplatPrismSet.MaxPrisms)
+                throw new InvalidOperationException($"Refusing to save {points.Length} points; hard cap is {SplatPrismSet.MaxPrisms}.");
+
+            string dir = Path.GetDirectoryName(assetPath);
+            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
+            {
+                Directory.CreateDirectory(dir);
+                AssetDatabase.Refresh();
+            }
+
+            var existing = AssetDatabase.LoadAssetAtPath<SplatPrismSet>(assetPath);
+            var so = existing != null ? existing : ScriptableObject.CreateInstance<SplatPrismSet>();
+            so.sourcePlyPath = sourcePath;
```

</details>

### `96bc9dc49` — feat(splats): wire GSplat minigame into arcade with one-time auto-setup

_Claude, 2026-05-31 15:17:25 +0000_

```text
Adds GameModes.GSplat (36) and a [InitializeOnLoad] editor setup that
runs once on project open to make the mode playable end-to-end:

- Bakes a synthetic SplatPrismSet (4000 splats, depth-and-radius hue ramp
  for visual interest) — gives the user something to fly through before
  a real .ply is checked in.
- Duplicates MinigameFreestyle.unity -> MinigameGSplat.unity and drops a
  SplatCloud GameObject into the duplicate, wiring the SplatPrismSpawner
  to the baked set via SerializedObject.
- Adds the new scene to EditorBuildSettings.
- Creates ArcadeGameGSplat.asset (Squirrel-only, single-player) and adds
  it to OrganicRematchGames + ArcadeGames + AllGames lists.
- Special-cases GSplat as always-unlocked in GameModeProgressionService
  so it isn't gated by the quest progression chain.

Setup is idempotent (gated on ArcadeGameGSplat.asset existing) and also
exposed as Tools/Splats/Setup GSplat Arcade Game for manual re-run.
```

```text
 Assets/CosmicShore/Splats/Editor/SplatArcadeSetup.cs             | 281 +++++++++++++++++++++++++++++++++++++
 Assets/CosmicShore/Splats/Editor/SplatArcadeSetup.cs.meta        |  11 ++
 Assets/_Scripts/Data/Enums/GameModes.cs                          |   1 +
 Assets/_Scripts/System/Progression/GameModeProgressionService.cs |   4 +
 4 files changed, 297 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 150 of 314 lines)</summary>

```diff
diff --git a/Assets/CosmicShore/Splats/Editor/SplatArcadeSetup.cs b/Assets/CosmicShore/Splats/Editor/SplatArcadeSetup.cs
new file mode 100644
index 000000000..ac12df59c
--- /dev/null
+++ b/Assets/CosmicShore/Splats/Editor/SplatArcadeSetup.cs
@@ -0,0 +1,281 @@
+using System;
+using System.Collections.Generic;
+using System.IO;
+using System.Linq;
+using UnityEditor;
+using UnityEditor.SceneManagement;
+using UnityEngine;
+using UnityEngine.SceneManagement;
+using CosmicShore.Data;
+using CosmicShore.ScriptableObjects;
+using CosmicShore.Splats;
+
+namespace CosmicShore.Tools.SplatImport
+{
+    // Wires the GSplat minigame into the arcade so it's playable end-to-end:
+    //   - bakes a synthetic SplatPrismSet asset
+    //   - duplicates MinigameFreestyle.unity -> MinigameGSplat.unity
+    //   - drops a SplatCloud GameObject into that new scene
+    //   - adds the scene to build settings
+    //   - creates ArcadeGameGSplat.asset (Squirrel-only) and adds it to the AllGames/ArcadeGames lists
+    //
+    // Idempotent — re-running only touches assets that don't already exist.
+    // Auto-runs once on project open via [InitializeOnLoad] so opening the branch is enough.
+    [InitializeOnLoad]
+    public static class SplatArcadeSetup
+    {
+        const string GSplatSceneName = "MinigameGSplat";
+        const string FreestyleScenePath = "Assets/_Scenes/Singleplayer Scenes/MinigameFreestyle.unity";
+        const string GSplatScenePath   = "Assets/_Scenes/Singleplayer Scenes/MinigameGSplat.unity";
+        const string SyntheticSetPath  = "Assets/CosmicShore/Splats/Baked/SplatPrismSet_Synthetic.asset";
+        const string ArcadeGameAsset   = "Assets/_SO_Assets/Games/ArcadeGameGSplat.asset";
+        const string SquirrelVessel    = "Assets/_SO_Assets/Classes/SO_Class_Squirrel.asset";
+        // AppManager.gameList points at OrganicRematchGames in CORE/AppManager.prefab; that's the
+        // list ArcadeExploreView injects. The other two are kept in sync as a safety net since
+        // other UI surfaces may pull from them. Add to all that exist.
+        static readonly string[] GameListPaths =
+        {
+            "Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset",
+            "Assets/_SO_Assets/Games/GameLists/ArcadeGames.asset",
+            "Assets/_SO_Assets/Games/GameLists/AllGames.asset",
+        };
+
+        const int SyntheticSplatCount = 4000;
+        const float SyntheticVoxelSize = 0.05f;
+        // Synthetic positions are in [-1,1]^3 by the generator's contract. Inflate to a flyable
+        // tunnel — XY narrow, Z elongated so Squirrel actually traverses the cloud rather than
+        // passing through it in one frame.
+        static readonly Vector3 SyntheticPositionSpread = new Vector3(15f, 15f, 60f);
+        static readonly Vector3 SyntheticPositionOffset = new Vector3(0f, 0f, 0f);
+        // Scale up individual prisms so they're not microscopic next to the inflated tunnel.
+        const float SyntheticPrismScale = 4f;
+
+        static SplatArcadeSetup()
+        {
+            // Defer until after asset import and compile finish so AssetDatabase calls don't trip.
+            EditorApplication.delayCall += AutoRunIfNeeded;
+        }
+
+        private static void AutoRunIfNeeded()
+        {
+            if (EditorApplication.isUpdating || EditorApplication.isCompiling)
+            {
+                EditorApplication.delayCall += AutoRunIfNeeded;
+                return;
+            }
+            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
+            // Idempotency gate: assume done if the SO_ArcadeGame asset already exists.
+            if (AssetDatabase.LoadAssetAtPath<SO_ArcadeGame>(ArcadeGameAsset) != null) return;
+
+            try { Run(silent: true); }
+            catch (Exception ex)
+            {
+                Debug.LogWarning($"[SplatArcadeSetup] Auto-setup failed: {ex.Message}. Run Tools/Splats/Setup GSplat Arcade Game manually.");
+            }
+        }
+
+        [MenuItem("Tools/Splats/Setup GSplat Arcade Game")]
+        public static void RunMenu() => Run(silent: false);
+
+        public static void Run(bool silent)
+        {
+            EnsureFolderTree("Assets/CosmicShore/Splats/Baked");
+
+            var splatSet = EnsureSyntheticSplatSet();
+            EnsureGSplatScene(splatSet);
+            EnsureSceneInBuildSettings(GSplatScenePath);
+            var arcadeGame = EnsureArcadeGameAsset();
+            foreach (var listPath in GameListPaths)
+                EnsureGameListContains(listPath, arcadeGame);
+            AssetDatabase.SaveAssets();
+
+            if (!silent)
+                EditorUtility.DisplayDialog("GSplat Arcade Game",
+                    "Setup complete. Enter Play Mode, open the Arcade screen, pick GSplat, choose Squirrel, and fly.",
+                    "OK");
+            Debug.Log("[SplatArcadeSetup] GSplat is wired in. Play -> Arcade -> GSplat (Squirrel).");
+        }
+
+        // 1) Bake (or reuse) a synthetic SplatPrismSet ----------------------------------------------
+        private static SplatPrismSet EnsureSyntheticSplatSet()
+        {
+            var existing = AssetDatabase.LoadAssetAtPath<SplatPrismSet>(SyntheticSetPath);
+            if (existing != null && existing.points != null && existing.points.Length > 0) return existing;
+
+            var (raw, _) = SplatSyntheticTest.MakeSyntheticSplats(SyntheticSplatCount);
+            var settings = SplatDecimateSettings.Default;
+            settings.voxelSize = SyntheticVoxelSize;
+            settings.maxPrisms = SplatPrismSet.MaxPrisms;
+            var points = SplatDecimator.DecimateToPoints(raw, settings, out var report);
+            InflateToTunnel(points, SyntheticPositionSpread, SyntheticPositionOffset, SyntheticPrismScale);
+            Debug.Log($"[SplatArcadeSetup] Baked synthetic set. {report.ToLogString()}");
+            return SplatBakeWindow.SaveBakedAsset(SyntheticSetPath, points, report, settings, sourcePath: "<synthetic>");
+        }
+
+        private static void InflateToTunnel(SplatPoint[] points, Vector3 spread, Vector3 offset, float prismScale)
+        {
+            // SH-decoded color of the synthetic generator is uniform magenta — replace with a
+            // depth-and-radius hue ramp so the prototype reads as a structured cloud, not a blob.
+            // (Discarded as soon as a real .ply is baked since real SH gives real colors.)
+            for (int i = 0; i < points.Length; i++)
+            {
+                var p = points[i];
+                p.position = new Vector3(p.position.x * spread.x, p.position.y * spread.y, p.position.z * spread.z) + offset;
+                p.scale = p.scale * prismScale;
+
+                float depthT = Mathf.InverseLerp(-spread.z, spread.z, p.position.z - offset.z);
+                float radius = new Vector2(p.position.x - offset.x, p.position.y - offset.y).magnitude;
+                float radialT = Mathf.Clamp01(radius / Mathf.Max(spread.x, spread.y));
+                float hue = Mathf.Repeat(depthT * 0.85f + 0.05f, 1f);
+                float sat = Mathf.Lerp(0.4f, 0.95f, radialT);
+                float val = Mathf.Lerp(1f, 0.6f, radialT);
+                p.color = Color.HSVToRGB(hue, sat, val);
+
+                points[i] = p;
+            }
+        }
+
+        // 2) Duplicate freestyle scene + drop a SplatCloud GameObject in it ------------------------
+        private static void EnsureGSplatScene(SplatPrismSet splatSet)
+        {
+            bool sceneAlreadyExisted = File.Exists(GSplatScenePath);
+            if (!sceneAlreadyExisted)
+            {
+                if (!File.Exists(FreestyleScenePath))
```

</details>

### `45c6f8b60` — fix(splats): make GSplat arcade setup self-healing and broader

_Claude, 2026-06-01 03:09:00 +0000_

```text
The earlier setup gated re-runs on the SO_ArcadeGame asset existing,
which meant a partial first-run (e.g. asset created but not added to
any game list) locked in a broken state. It also only added to a
hardcoded three lists — but the Arcade Screen prefab's serialized
GameList field points at LaunchPartyAllGames, so DI-bypass paths
wouldn't see the game.

- Replace the existence gate with IsFullyWired() which checks every
  artifact (asset + scene + build settings entry + presence in each
  game list).
- Auto-discover SO_GameList assets under _SO_Assets/Games/GameLists
  rather than hardcoding paths. Skip Training/Mission/Leaderboard
  lists; everything else gets GSplat.
- Add Tools/Splats/Diagnose GSplat Setup menu that reports which
  artifacts are present/missing so the next debugging round is
  faster.
- Defer through playmode transitions instead of returning early.
```

```text
 Assets/CosmicShore/Splats/Editor/SplatArcadeSetup.cs | 120 ++++++++++++++++++++++++++++++++++++++++++-------
 1 file changed, 104 insertions(+), 16 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 161 lines)</summary>

```diff
diff --git a/Assets/CosmicShore/Splats/Editor/SplatArcadeSetup.cs b/Assets/CosmicShore/Splats/Editor/SplatArcadeSetup.cs
index ac12df59c..5a0d3deb2 100644
--- a/Assets/CosmicShore/Splats/Editor/SplatArcadeSetup.cs
+++ b/Assets/CosmicShore/Splats/Editor/SplatArcadeSetup.cs
@@ -30,14 +30,18 @@ namespace CosmicShore.Tools.SplatImport
         const string SyntheticSetPath  = "Assets/CosmicShore/Splats/Baked/SplatPrismSet_Synthetic.asset";
         const string ArcadeGameAsset   = "Assets/_SO_Assets/Games/ArcadeGameGSplat.asset";
         const string SquirrelVessel    = "Assets/_SO_Assets/Classes/SO_Class_Squirrel.asset";
-        // AppManager.gameList points at OrganicRematchGames in CORE/AppManager.prefab; that's the
-        // list ArcadeExploreView injects. The other two are kept in sync as a safety net since
-        // other UI surfaces may pull from them. Add to all that exist.
-        static readonly string[] GameListPaths =
+        // Different surfaces of the arcade UI bind to different SO_GameList assets — AppManager
+        // registers one for DI but several screen prefabs have their own serialized references.
+        // Add to every arcade-shaped list we can find so the game appears regardless of which one
+        // the consumer actually reads from at runtime.
+        const string GameListsFolder = "Assets/_SO_Assets/Games/GameLists";
+        // Lists that should NOT receive GSplat (training programs, leaderboards) — kept as a
+        // small skip list rather than an allow list so newly-added lists default to inclusion.
+        static readonly HashSet<string> GameListsToSkip = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
         {
-            "Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset",
-            "Assets/_SO_Assets/Games/GameLists/ArcadeGames.asset",
-            "Assets/_SO_Assets/Games/GameLists/AllGames.asset",
+            "TrainingGames",
+            "MissionGames",
+            "LeaderboardGames",
         };
 
         const int SyntheticSplatCount = 4000;
@@ -63,15 +67,36 @@ namespace CosmicShore.Tools.SplatImport
                 EditorApplication.delayCall += AutoRunIfNeeded;
                 return;
             }
-            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
-            // Idempotency gate: assume done if the SO_ArcadeGame asset already exists.
-            if (AssetDatabase.LoadAssetAtPath<SO_ArcadeGame>(ArcadeGameAsset) != null) return;
+            if (EditorApplication.isPlayingOrWillChangePlaymode)
+            {
+                EditorApplication.delayCall += AutoRunIfNeeded;
+                return;
+            }
+
+            // Self-heal gate: if every artifact is already in place, skip silently. Otherwise,
+            // run the full Run() — each Ensure* step is individually idempotent so this is cheap.
+            if (IsFullyWired()) return;
 
             try { Run(silent: true); }
             catch (Exception ex)
             {
-                Debug.LogWarning($"[SplatArcadeSetup] Auto-setup failed: {ex.Message}. Run Tools/Splats/Setup GSplat Arcade Game manually.");
+                Debug.LogError($"[SplatArcadeSetup] Auto-setup threw: {ex}\nRun Tools/Splats/Setup GSplat Arcade Game manually for a verbose retry.");
+            }
+        }
+
+        private static bool IsFullyWired()
+        {
+            var arcadeGame = AssetDatabase.LoadAssetAtPath<SO_ArcadeGame>(ArcadeGameAsset);
+            if (arcadeGame == null) return false;
+            if (AssetDatabase.LoadAssetAtPath<SplatPrismSet>(SyntheticSetPath) == null) return false;
+            if (!File.Exists(GSplatScenePath)) return false;
+            if (!EditorBuildSettings.scenes.Any(s => s.path == GSplatScenePath && s.enabled)) return false;
+            foreach (var listPath in FindArcadeGameLists())
+            {
+                var list = AssetDatabase.LoadAssetAtPath<SO_GameList>(listPath);
+                if (list != null && (list.Games == null || !list.Games.Contains(arcadeGame))) return false;
             }
+            return true;
         }
 
         [MenuItem("Tools/Splats/Setup GSplat Arcade Game")]
@@ -79,21 +104,84 @@ namespace CosmicShore.Tools.SplatImport
 
         public static void Run(bool silent)
         {
+            Debug.Log("[SplatArcadeSetup] Starting setup ...");
             EnsureFolderTree("Assets/CosmicShore/Splats/Baked");
 
             var splatSet = EnsureSyntheticSplatSet();
             EnsureGSplatScene(splatSet);
             EnsureSceneInBuildSettings(GSplatScenePath);
             var arcadeGame = EnsureArcadeGameAsset();
-            foreach (var listPath in GameListPaths)
+
+            var lists = FindArcadeGameLists();
+            if (lists.Count == 0)
+                Debug.LogWarning($"[SplatArcadeSetup] No SO_GameList assets found under {GameListsFolder}.");
+            foreach (var listPath in lists)
                 EnsureGameListContains(listPath, arcadeGame);
+
             AssetDatabase.SaveAssets();
+            AssetDatabase.Refresh();
 
+            string summary = SummarizeWiring(arcadeGame, lists);
+            Debug.Log($"[SplatArcadeSetup] GSplat wiring complete:\n{summary}");
             if (!silent)
-                EditorUtility.DisplayDialog("GSplat Arcade Game",
-                    "Setup complete. Enter Play Mode, open the Arcade screen, pick GSplat, choose Squirrel, and fly.",
-                    "OK");
-            Debug.Log("[SplatArcadeSetup] GSplat is wired in. Play -> Arcade -> GSplat (Squirrel).");
+                EditorUtility.DisplayDialog("GSplat Arcade Game", "Setup complete.\n\n" + summary + "\n\nEnter Play Mode, open the Arcade, pick GSplat, choose Squirrel, and fly.", "OK");
+        }
+
+        [MenuItem("Tools/Splats/Diagnose GSplat Setup")]
+        public static void Diagnose()
+        {
+            var arcadeGame = AssetDatabase.LoadAssetAtPath<SO_ArcadeGame>(ArcadeGameAsset);
+            var splatSet = AssetDatabase.LoadAssetAtPath<SplatPrismSet>(SyntheticSetPath);
+            var scene = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(GSplatScenePath);
+            var lists = FindArcadeGameLists();
+            int containingCount = arcadeGame == null ? 0 :
+                lists.Count(p =>
+                {
+                    var l = AssetDatabase.LoadAssetAtPath<SO_GameList>(p);
+                    return l != null && l.Games != null && l.Games.Contains(arcadeGame);
+                });
+            bool inBuild = EditorBuildSettings.scenes.Any(s => s.path == GSplatScenePath && s.enabled);
+
+            string report =
+                $"ArcadeGameGSplat.asset:   {(arcadeGame != null ? "OK" : "MISSING")}\n" +
+                $"SplatPrismSet_Synthetic:  {(splatSet != null ? $"OK ({(splatSet.points?.Length ?? 0)} pts)" : "MISSING")}\n" +
+                $"MinigameGSplat.unity:     {(scene != null ? "OK" : "MISSING")}\n" +
+                $"In build settings:        {(inBuild ? "YES" : "NO")}\n" +
+                $"Game lists found:         {lists.Count}\n" +
+                $"Lists containing GSplat:  {containingCount}/{lists.Count}";
+            Debug.Log("[SplatArcadeSetup] Diagnosis:\n" + report);
+            EditorUtility.DisplayDialog("GSplat Diagnosis", report + "\n\nIf any line is MISSING/NO/0, run Tools/Splats/Setup GSplat Arcade Game.", "OK");
+        }
+
+        private static List<string> FindArcadeGameLists()
+        {
+            var results = new List<string>();
+            if (!AssetDatabase.IsValidFolder(GameListsFolder)) return results;
+            foreach (var guid in AssetDatabase.FindAssets("t:SO_GameList", new[] { GameListsFolder }))
+            {
+                var path = AssetDatabase.GUIDToAssetPath(guid);
+                var name = Path.GetFileNameWithoutExtension(path);
+                if (GameListsToSkip.Contains(name)) continue;
+                results.Add(path);
+            }
+            return results;
+        }
+
+        private static string SummarizeWiring(SO_ArcadeGame game, List<string> lists)
+        {
+            var sb = new System.Text.StringBuilder();
+            sb.Append("  arcade game asset: ").AppendLine(ArcadeGameAsset);
+            sb.Append("  scene: ").AppendLine(GSplatScenePath);
+            sb.Append("  splat set: ").AppendLine(SyntheticSetPath);
+            sb.Append("  in build settings: ").AppendLine(EditorBuildSettings.scenes.Any(s => s.path == GSplatScenePath && s.enabled) ? "yes" : "no");
```

</details>

### `6fc0dc9cd` — fix(splats): use freestyle scene + runtime cloud loader instead of clone

_Claude, 2026-06-01 15:18:07 +0000_

```text
Cloning MinigameFreestyle.unity into MinigameGSplat.unity broke Reflex
DI for the duplicated scene's components — gameData was null on
SinglePlayerFreestyleController, MiniGamePlayerSpawnerAdapter, and
LocalVolumeUIController, and the spawner's serialized SplatPrismSet
reference also didn't survive the save.

Switch architecture: ArcadeGameGSplat.asset now points at the stock
MinigameFreestyle scene (which is known-good and DI-wired). A new
runtime class SplatCloudRuntimeLoader subscribes to sceneLoaded and,
when GameDataSO.GameMode == GSplat, programmatically creates a
SplatCloud root GameObject and wires it to the SplatPrismSet loaded
from a Resources folder.

- Add SplatCloudRuntimeLoader (RuntimeInitializeOnLoadMethod).
- Add SplatPrismSpawner.SetSourceSet for runtime wiring; downgrade
  the "no set" error to a warning + skip (no more spam).
- Move synthetic SplatPrismSet asset path under Assets/CosmicShore/
  Splats/Resources so Resources.Load can find it at runtime.
- Drop the scene-clone + build-settings-add steps from setup.
- Add CleanupLegacyArtifacts to remove leftover MinigameGSplat.unity
  and its build settings entry (idempotent — safe to re-run).
- IsFullyWired now also asserts the SO_ArcadeGame's SceneName is
  MinigameFreestyle so a stale clone-pointing asset gets corrected.
```

```text
 Assets/CosmicShore/Splats/Editor/SplatArcadeSetup.cs              | 194 +++++++++++++++---------------------
 Assets/CosmicShore/Splats/Runtime/SplatCloudRuntimeLoader.cs      |  84 ++++++++++++++++
 Assets/CosmicShore/Splats/Runtime/SplatCloudRuntimeLoader.cs.meta |  11 ++
 Assets/CosmicShore/Splats/Runtime/SplatPrismSpawner.cs            |   9 +-
 4 files changed, 183 insertions(+), 115 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 404 lines)</summary>

```diff
diff --git a/Assets/CosmicShore/Splats/Editor/SplatArcadeSetup.cs b/Assets/CosmicShore/Splats/Editor/SplatArcadeSetup.cs
index 5a0d3deb2..b4e7c2923 100644
--- a/Assets/CosmicShore/Splats/Editor/SplatArcadeSetup.cs
+++ b/Assets/CosmicShore/Splats/Editor/SplatArcadeSetup.cs
@@ -3,9 +3,7 @@ using System.Collections.Generic;
 using System.IO;
 using System.Linq;
 using UnityEditor;
-using UnityEditor.SceneManagement;
 using UnityEngine;
-using UnityEngine.SceneManagement;
 using CosmicShore.Data;
 using CosmicShore.ScriptableObjects;
 using CosmicShore.Splats;
@@ -13,21 +11,30 @@ using CosmicShore.Splats;
 namespace CosmicShore.Tools.SplatImport
 {
     // Wires the GSplat minigame into the arcade so it's playable end-to-end:
-    //   - bakes a synthetic SplatPrismSet asset
-    //   - duplicates MinigameFreestyle.unity -> MinigameGSplat.unity
-    //   - drops a SplatCloud GameObject into that new scene
-    //   - adds the scene to build settings
-    //   - creates ArcadeGameGSplat.asset (Squirrel-only) and adds it to the AllGames/ArcadeGames lists
+    //   - bakes a synthetic SplatPrismSet asset under Resources/ for runtime loading
+    //   - creates ArcadeGameGSplat.asset (Squirrel-only, single-player) pointing at the
+    //     stock MinigameFreestyle scene
+    //   - adds the asset to every arcade-shaped SO_GameList found in the project
+    //   - cleans up any leftover MinigameGSplat.unity clone from the earlier approach
+    //
+    // The splat cloud itself is layered onto the freestyle scene at runtime by
+    // SplatCloudRuntimeLoader — see that class. Reusing the stock scene keeps Reflex DI
+    // and freestyle's player spawning intact, which the scene-clone approach broke.
     //
     // Idempotent — re-running only touches assets that don't already exist.
     // Auto-runs once on project open via [InitializeOnLoad] so opening the branch is enough.
     [InitializeOnLoad]
     public static class SplatArcadeSetup
     {
-        const string GSplatSceneName = "MinigameGSplat";
-        const string FreestyleScenePath = "Assets/_Scenes/Singleplayer Scenes/MinigameFreestyle.unity";
-        const string GSplatScenePath   = "Assets/_Scenes/Singleplayer Scenes/MinigameGSplat.unity";
-        const string SyntheticSetPath  = "Assets/CosmicShore/Splats/Baked/SplatPrismSet_Synthetic.asset";
+        // GSplat reuses the stock MinigameFreestyle scene to keep Reflex DI and player spawning
+        // intact — the splat cloud is layered on at runtime by SplatCloudRuntimeLoader when it
+        // detects GameMode == GSplat. The earlier scene-clone approach broke DI in the duplicate.
+        const string FreestyleSceneName = "MinigameFreestyle";
+        const string LegacyGSplatScenePath = "Assets/_Scenes/Singleplayer Scenes/MinigameGSplat.unity";
+        // SplatPrismSet lives under a Resources folder so SplatCloudRuntimeLoader can find it at
+        // runtime without serialized refs in the scene file.
+        const string SyntheticSetPath  = "Assets/CosmicShore/Splats/Resources/SplatPrismSet_Synthetic.asset";
+        const string LegacyBakedFolder = "Assets/CosmicShore/Splats/Baked";
         const string ArcadeGameAsset   = "Assets/_SO_Assets/Games/ArcadeGameGSplat.asset";
         const string SquirrelVessel    = "Assets/_SO_Assets/Classes/SO_Class_Squirrel.asset";
         // Different surfaces of the arcade UI bind to different SO_GameList assets — AppManager
@@ -88,9 +95,8 @@ namespace CosmicShore.Tools.SplatImport
         {
             var arcadeGame = AssetDatabase.LoadAssetAtPath<SO_ArcadeGame>(ArcadeGameAsset);
             if (arcadeGame == null) return false;
+            if (arcadeGame.SceneName != FreestyleSceneName) return false;
             if (AssetDatabase.LoadAssetAtPath<SplatPrismSet>(SyntheticSetPath) == null) return false;
-            if (!File.Exists(GSplatScenePath)) return false;
-            if (!EditorBuildSettings.scenes.Any(s => s.path == GSplatScenePath && s.enabled)) return false;
             foreach (var listPath in FindArcadeGameLists())
             {
                 var list = AssetDatabase.LoadAssetAtPath<SO_GameList>(listPath);
@@ -105,11 +111,10 @@ namespace CosmicShore.Tools.SplatImport
         public static void Run(bool silent)
         {
             Debug.Log("[SplatArcadeSetup] Starting setup ...");
-            EnsureFolderTree("Assets/CosmicShore/Splats/Baked");
+            EnsureFolderTree("Assets/CosmicShore/Splats/Resources");
 
-            var splatSet = EnsureSyntheticSplatSet();
-            EnsureGSplatScene(splatSet);
-            EnsureSceneInBuildSettings(GSplatScenePath);
+            EnsureSyntheticSplatSet();
+            CleanupLegacyArtifacts();
             var arcadeGame = EnsureArcadeGameAsset();
 
             var lists = FindArcadeGameLists();
@@ -127,12 +132,41 @@ namespace CosmicShore.Tools.SplatImport
                 EditorUtility.DisplayDialog("GSplat Arcade Game", "Setup complete.\n\n" + summary + "\n\nEnter Play Mode, open the Arcade, pick GSplat, choose Squirrel, and fly.", "OK");
         }
 
+        // Removes scene/build-settings artifacts left behind by the earlier scene-clone approach.
+        // The cloned MinigameGSplat.unity scene broke Reflex DI for the components inside it — we
+        // now layer the splat cloud onto the stock freestyle scene at runtime instead.
+        private static void CleanupLegacyArtifacts()
+        {
+            if (File.Exists(LegacyGSplatScenePath))
+            {
+                AssetDatabase.DeleteAsset(LegacyGSplatScenePath);
+                Debug.Log($"[SplatArcadeSetup] Removed legacy clone {LegacyGSplatScenePath}.");
+            }
+            var scenes = EditorBuildSettings.scenes;
+            if (scenes.Any(s => s.path == LegacyGSplatScenePath))
+            {
+                EditorBuildSettings.scenes = scenes.Where(s => s.path != LegacyGSplatScenePath).ToArray();
+                Debug.Log("[SplatArcadeSetup] Removed legacy clone from build settings.");
+            }
+            // Move any baked synthetic asset out of the legacy non-Resources path.
+            string legacyPath = LegacyBakedFolder + "/SplatPrismSet_Synthetic.asset";
+            if (File.Exists(legacyPath) && !File.Exists(SyntheticSetPath))
+            {
+                EnsureFolderTree(Path.GetDirectoryName(SyntheticSetPath).Replace('\\', '/'));
+                AssetDatabase.MoveAsset(legacyPath, SyntheticSetPath);
+                Debug.Log($"[SplatArcadeSetup] Moved synthetic splat set into Resources: {SyntheticSetPath}");
+            }
+            else if (File.Exists(legacyPath))
+            {
+                AssetDatabase.DeleteAsset(legacyPath);
+            }
+        }
+
         [MenuItem("Tools/Splats/Diagnose GSplat Setup")]
         public static void Diagnose()
         {
             var arcadeGame = AssetDatabase.LoadAssetAtPath<SO_ArcadeGame>(ArcadeGameAsset);
             var splatSet = AssetDatabase.LoadAssetAtPath<SplatPrismSet>(SyntheticSetPath);
-            var scene = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(GSplatScenePath);
             var lists = FindArcadeGameLists();
             int containingCount = arcadeGame == null ? 0 :
                 lists.Count(p =>
@@ -140,17 +174,17 @@ namespace CosmicShore.Tools.SplatImport
                     var l = AssetDatabase.LoadAssetAtPath<SO_GameList>(p);
                     return l != null && l.Games != null && l.Games.Contains(arcadeGame);
                 });
-            bool inBuild = EditorBuildSettings.scenes.Any(s => s.path == GSplatScenePath && s.enabled);
+            string sceneOnAsset = arcadeGame != null ? arcadeGame.SceneName : "<no asset>";
 
             string report =
                 $"ArcadeGameGSplat.asset:   {(arcadeGame != null ? "OK" : "MISSING")}\n" +
-                $"SplatPrismSet_Synthetic:  {(splatSet != null ? $"OK ({(splatSet.points?.Length ?? 0)} pts)" : "MISSING")}\n" +
-                $"MinigameGSplat.unity:     {(scene != null ? "OK" : "MISSING")}\n" +
-                $"In build settings:        {(inBuild ? "YES" : "NO")}\n" +
+                $"  -> SceneName:           {sceneOnAsset} (expected {FreestyleSceneName})\n" +
+                $"SplatPrismSet (Resources): {(splatSet != null ? $"OK ({(splatSet.points?.Length ?? 0)} pts)" : "MISSING")}\n" +
                 $"Game lists found:         {lists.Count}\n" +
-                $"Lists containing GSplat:  {containingCount}/{lists.Count}";
+                $"Lists containing GSplat:  {containingCount}/{lists.Count}\n" +
+                $"Legacy clone file:        {(File.Exists(LegacyGSplatScenePath) ? "PRESENT (run setup to delete)" : "absent")}";
             Debug.Log("[SplatArcadeSetup] Diagnosis:\n" + report);
-            EditorUtility.DisplayDialog("GSplat Diagnosis", report + "\n\nIf any line is MISSING/NO/0, run Tools/Splats/Setup GSplat Arcade Game.", "OK");
+            EditorUtility.DisplayDialog("GSplat Diagnosis", report + "\n\nIf any line is MISSING/wrong, run Tools/Splats/Setup GSplat Arcade Game.", "OK");
         }
 
         private static List<string> FindArcadeGameLists()
@@ -171,9 +205,8 @@ namespace CosmicShore.Tools.SplatImport
         {
             var sb = new System.Text.StringBuilder();
```

</details>

### `f1dd85a92` — feat(splats): add no-Bootstrap splat sandbox scene as fallback path

_Claude, 2026-06-01 15:57:04 +0000_

```text
The arcade flow depends on Bootstrap loading and Reflex DI injecting
gameData into the freestyle controller. Both are showing as broken in
the current branch state (NREs on gameData and on freestyle's
LocalCrystalManager / MiniGamePlayerSpawnerAdapter on scene load),
which prevents the arcade-launched GSplat path from running.

This sandbox sidesteps that entire stack:
- SplatSandboxSceneBuilder (editor menu) creates a SplatSandbox.unity
  scene with a SplatCloud GameObject wired to the synthetic
  SplatPrismSet, a Camera with a fly cam, and a directional light.
  No GameDataSO, no AppManager, no DI required.
- SplatSandboxFlyCam (runtime) is a minimal WASD + right-mouse-look
  camera so the cloud is navigable.

Reachable via Tools > Splats > Open Splat Sandbox (No Bootstrap).
The dialog reminds the user to disable
FrogletTools > Legacy > TestingMultiplayer > Load Bootstrap Scene on
Play so SceneBootstrapper doesn't redirect Play to Bootstrap.
```

```text
 Assets/CosmicShore/Splats/Editor/SplatSandboxSceneBuilder.cs      | 142 ++++++++++++++++++++++++++++++++++++
 Assets/CosmicShore/Splats/Editor/SplatSandboxSceneBuilder.cs.meta |  11 +++
 Assets/CosmicShore/Splats/Runtime/SplatSandboxFlyCam.cs           |  64 ++++++++++++++++
 Assets/CosmicShore/Splats/Runtime/SplatSandboxFlyCam.cs.meta      |  11 +++
 4 files changed, 228 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 150 of 218 lines)</summary>

```diff
diff --git a/Assets/CosmicShore/Splats/Editor/SplatSandboxSceneBuilder.cs b/Assets/CosmicShore/Splats/Editor/SplatSandboxSceneBuilder.cs
new file mode 100644
index 000000000..7c9fe643c
--- /dev/null
+++ b/Assets/CosmicShore/Splats/Editor/SplatSandboxSceneBuilder.cs
@@ -0,0 +1,142 @@
+using System.IO;
+using UnityEditor;
+using UnityEditor.SceneManagement;
+using UnityEngine;
+using UnityEngine.SceneManagement;
+using CosmicShore.Splats;
+
+namespace CosmicShore.Tools.SplatImport
+{
+    // Editor command that opens a minimal "SplatSandbox" scene with a fly camera and the
+    // splat cloud. Designed to bypass the gameplay/DI stack entirely so the splat pipeline
+    // is testable even when Bootstrap/Reflex injection isn't behaving — useful as a
+    // fallback when the arcade flow trips DI nulls on the cloned scene's components.
+    //
+    // NOTE: SceneBootstrapper (FrogletTools menu) will normally hijack Play to load
+    // Bootstrap first. Toggle "FrogletTools/Legacy/TestingMultiplayer/Do not load
+    // Bootstrap Scene on Play" before pressing Play so the sandbox actually starts here.
+    public static class SplatSandboxSceneBuilder
+    {
+        const string SandboxScenePath = "Assets/CosmicShore/Splats/SplatSandbox.unity";
+        const string SyntheticSetPath = "Assets/CosmicShore/Splats/Resources/SplatPrismSet_Synthetic.asset";
+        const string FallbackSyntheticSetPath = "Assets/CosmicShore/Splats/Baked/SplatPrismSet_Synthetic.asset";
+
+        [MenuItem("Tools/Splats/Open Splat Sandbox (No Bootstrap)")]
+        public static void OpenSandbox()
+        {
+            var splatSet = AssetDatabase.LoadAssetAtPath<SplatPrismSet>(SyntheticSetPath)
+                        ?? AssetDatabase.LoadAssetAtPath<SplatPrismSet>(FallbackSyntheticSetPath);
+            if (splatSet == null)
+            {
+                if (EditorUtility.DisplayDialog(
+                        "SplatPrismSet missing",
+                        $"No SplatPrismSet at {SyntheticSetPath}. Run Tools/Splats/Setup GSplat Arcade Game first to bake one.\n\nRun setup now?",
+                        "Run Setup", "Cancel"))
+                {
+                    SplatArcadeSetup.Run(silent: true);
+                    splatSet = AssetDatabase.LoadAssetAtPath<SplatPrismSet>(SyntheticSetPath);
+                }
+                if (splatSet == null)
+                {
+                    Debug.LogError("[SplatSandbox] Cannot proceed without a SplatPrismSet.");
+                    return;
+                }
+            }
+
+            Scene scene;
+            if (File.Exists(SandboxScenePath))
+            {
+                scene = EditorSceneManager.OpenScene(SandboxScenePath, OpenSceneMode.Single);
+                EnsureCloud(scene, splatSet);
+                EnsureCamera(scene);
+                EnsureLight(scene);
+            }
+            else
+            {
+                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
+                EnsureCamera(scene);
+                EnsureLight(scene);
+                EnsureCloud(scene, splatSet);
+                EditorSceneManager.SaveScene(scene, SandboxScenePath);
+            }
+            EditorSceneManager.MarkSceneDirty(scene);
+            EditorSceneManager.SaveScene(scene);
+
+            EditorUtility.DisplayDialog(
+                "Splat Sandbox Ready",
+                "The SplatSandbox scene is open.\n\n" +
+                "Before hitting Play:\n" +
+                "  1. Click FrogletTools > Legacy > TestingMultiplayer > 'Do not load Bootstrap Scene on Play' (so Play starts here, not in Bootstrap).\n" +
+                "  2. Press Play.\n\n" +
+                "Controls: Hold right mouse to look. WASD to move. Space/Q vertical. Shift fast, Ctrl slow.",
+                "OK");
+        }
+
+        private static void EnsureCloud(Scene scene, SplatPrismSet set)
+        {
+            foreach (var root in scene.GetRootGameObjects())
+                if (root.name == "SplatCloud") { WireSpawner(root, set); return; }
+
+            var go = new GameObject("SplatCloud");
+            SceneManager.MoveGameObjectToScene(go, scene);
+            go.transform.position = new Vector3(0f, 0f, 30f);
+            var spawner = go.AddComponent<SplatPrismSpawner>();
+            WireSpawnerSerialized(spawner, set);
+        }
+
+        private static void WireSpawner(GameObject go, SplatPrismSet set)
+        {
+            var spawner = go.GetComponent<SplatPrismSpawner>() ?? go.AddComponent<SplatPrismSpawner>();
+            WireSpawnerSerialized(spawner, set);
+        }
+
+        private static void WireSpawnerSerialized(SplatPrismSpawner spawner, SplatPrismSet set)
+        {
+            var so = new SerializedObject(spawner);
+            var prop = so.FindProperty("set");
+            if (prop != null)
+            {
+                prop.objectReferenceValue = set;
+                so.ApplyModifiedPropertiesWithoutUndo();
+            }
+            else
+            {
+                // Last-resort safety: still set via the runtime setter so play still works.
+                spawner.SetSourceSet(set);
+            }
+        }
+
+        private static void EnsureCamera(Scene scene)
+        {
+            foreach (var root in scene.GetRootGameObjects())
+                if (root.name == "Sandbox Camera") return;
+
+            var go = new GameObject("Sandbox Camera");
+            SceneManager.MoveGameObjectToScene(go, scene);
+            go.transform.position = new Vector3(0f, 5f, -25f);
+            go.transform.rotation = Quaternion.Euler(10f, 0f, 0f);
+            var cam = go.AddComponent<Camera>();
+            cam.fieldOfView = 65f;
+            cam.farClipPlane = 5000f;
+            cam.clearFlags = CameraClearFlags.SolidColor;
+            cam.backgroundColor = new Color(0.02f, 0.02f, 0.05f, 1f);
+            go.AddComponent<AudioListener>();
+            go.AddComponent<SplatSandboxFlyCam>();
+            go.tag = "MainCamera";
+        }
+
+        private static void EnsureLight(Scene scene)
+        {
+            foreach (var root in scene.GetRootGameObjects())
+                if (root.name == "Sun") return;
+
+            var go = new GameObject("Sun");
+            SceneManager.MoveGameObjectToScene(go, scene);
+            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
+            var light = go.AddComponent<Light>();
+            light.type = LightType.Directional;
+            light.intensity = 1.1f;
+            light.color = new Color(1f, 0.95f, 0.9f);
+        }
+    }
+}
diff --git a/Assets/CosmicShore/Splats/Runtime/SplatSandboxFlyCam.cs b/Assets/CosmicShore/Splats/Runtime/SplatSandboxFlyCam.cs
new file mode 100644
```

</details>
