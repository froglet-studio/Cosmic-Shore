# Branch archive: `claude/membrane-dual-geometry-6InGh`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

## What this branch is

**Hex/pentagon dual-geodesic membrane experiment**

An experiment from Feb-Mar 2026 to give the cell membrane (the arena's boundary sphere) a honeycomb look made of hexagons and pentagons, built as the 'dual' of a geodesic sphere. It added a mesh generator editor tool, a short-lived runtime component (later reverted), a smooth-normals shader fix, and a dual-sphere spawnable hooked into Crystal Capture and HexRace. It was last touched in September by a one-line GameCard fix.

- **Status:** Abandoned experiment
- **Areas:** Cell membrane visuals, Environment geometry, Editor tooling, HexRace/Crystal Capture (retired)
- **Already in bleeding-edge:** None found: DualMeshGenerator.cs, DualMembrane.cs, DualMeshUtility.cs and SpawnableDualSpherene.cs are absent from bleeding-edge (only SpawnableSpherene exists). The modes it was wired into (HexRace, Crystal Capture) have since been retired.
- **Risk if deleted:** low
- **Suggestion (2026-10-08):** can be deleted after archiving — It is an old visual experiment tied to retired modes, and the archive keeps the generator code if someone revisits the look.

## Evidence

- **Last commit:** 2026-09-02 by Claude
- **Unmerged commits:** 11
- **Forked from:** `af1c0c5a4` (2026-03-02, Merge pull request #329 from froglet-studio/claude/add-drift-easing-i8LJN)
- **Tip:** `17dd01edd`
- **Files touched (19):**
  - `Assets/Resources/PerformanceTestRunInfo.json`
  - `Assets/Resources/PerformanceTestRunInfo.json.meta`
  - `Assets/Resources/PerformanceTestRunSettings.json`
  - `Assets/Resources/PerformanceTestRunSettings.json.meta`
  - `Assets/_Models/SkyboxModel.fbx.meta`
  - `Assets/_Prefabs/Environment/MembraneBase.prefab`
  - `Assets/_Prefabs/Spawnables/SpawnableConcetricSpheres.prefab`
  - `Assets/_Prefabs/Spawnables/SpawnableSpherene dual.prefab`
  - `Assets/_Prefabs/Spawnables/SpawnableSpherene dual.prefab.meta`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameHexRace.unity`
  - `Assets/_Scripts/App/UI/Elements/GameCard.cs`
  - `Assets/_Scripts/Editor/DualMeshGenerator.cs`
  - `Assets/_Scripts/Editor/DualMeshGenerator.cs.meta`
  - `Assets/_Scripts/Game/Environment/DualMembrane.cs`
  - `Assets/_Scripts/Game/Environment/DualMembrane.cs.meta`
  - `Assets/_Scripts/Game/Environment/DualMeshUtility.cs`
  - `Assets/_Scripts/Game/Environment/DualMeshUtility.cs.meta`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableDualSpherene.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableDualSpherene.cs.meta`

### `776b0a9df` — Add SpawnableDualSpherene — hex/pentagon membrane via dual geodesic polyhedron

_Claude, 2026-02-27 19:32:35 +0000_

```text
Computes the dual of a subdivided icosphere to produce a Goldberg polyhedron
topology (hexagons + pentagons) instead of triangles. The 12 original
icosahedron vertices become pentagonal faces; all subdivision vertices become
hexagonal faces. Prism blocks are placed along dual edges to outline the cells.

Perlin-noise radial undulation preserves the organic membrane feel. Pentagon
centers can optionally be highlighted with larger marker blocks.

Subdivision levels:
  0 → dodecahedron (12 pentagons, 30 edges)
  1 → truncated icosahedron (12 pent + 20 hex, 90 edges)
  2 → 12 pent + 80 hex, 360 edges
```

```text
 .../Game/Environment/MiniGameObjects/SpawnableDualSpherene.cs         | 306 ++++++++++++++++++++++++++++++++
 1 file changed, 306 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 312 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableDualSpherene.cs b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableDualSpherene.cs
new file mode 100644
index 000000000..31b5a1e3d
--- /dev/null
+++ b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableDualSpherene.cs
@@ -0,0 +1,306 @@
+using CosmicShore.Core;
+using CosmicShore.Game.Spawning;
+using System.Collections.Generic;
+using UnityEngine;
+
+namespace CosmicShore
+{
+    /// <summary>
+    /// Spawns prisms along the edges of the dual of a geodesic polyhedron — a Goldberg
+    /// polyhedron with hexagonal and pentagonal faces.
+    ///
+    /// Starting from a subdivided icosphere (triangular faces), the dual is computed by
+    /// placing a vertex at each triangle's centroid (projected onto the sphere) and
+    /// connecting centroids of triangles that share an original edge. Each of the 12
+    /// original icosahedron vertices has valence 5, producing 12 pentagons; all subdivision
+    /// vertices have valence 6, producing hexagons.
+    ///
+    /// The result is the classic soccer ball / fullerene / Goldberg polyhedron topology:
+    ///   - Subdivision 0 → dodecahedron (12 pentagons, 30 dual edges)
+    ///   - Subdivision 1 → truncated icosahedron (12 pentagons + 20 hexagons, 90 dual edges)
+    ///   - Subdivision 2 → 12 pentagons + 80 hexagons, 360 dual edges
+    ///
+    /// Perlin-noise undulation displaces vertices radially, preserving the organic membrane
+    /// feel while rendering the topology in hex/pent instead of triangles.
+    /// </summary>
+    public class SpawnableDualSpherene : SpawnableBase
+    {
+        [Header("Block Settings")]
+        [SerializeField] Prism prism;
+        [SerializeField] Vector3 blockScale = new Vector3(2f, 2f, 4f);
+
+        [Header("Geodesic Structure")]
+        [Tooltip("Subdivision level of the underlying icosphere whose dual is rendered.\n" +
+                 "0 = dodecahedron (12 pentagons)\n" +
+                 "1 = truncated icosahedron (12 pent + 20 hex)\n" +
+                 "2 = 12 pent + 80 hex")]
+        [Range(0, 3)]
+        [SerializeField] int subdivisions = 2;
+
+        [Tooltip("Radius of the sphere.")]
+        [SerializeField] float radius = 70f;
+
+        [Tooltip("Prism blocks per dual edge. More = smoother curves along edges.")]
+        [SerializeField] int blocksPerEdge = 6;
+
+        [Header("Undulation")]
+        [Tooltip("Amplitude of radial undulation applied to dual vertices.")]
+        [SerializeField] float undulationAmplitude = 3f;
+
+        [Tooltip("Frequency of the undulation pattern across the sphere surface.")]
+        [SerializeField] float undulationFrequency = 4f;
+
+        [Tooltip("Seed offset for undulation noise pattern.")]
+        [SerializeField] float undulationPhase = 0f;
+
+        [Header("Visual")]
+        [SerializeField] Domains edgeDomain = Domains.Blue;
+        [SerializeField] Domains pentagonDomain = Domains.Gold;
+
+        [Tooltip("Place marker blocks at pentagon centers to highlight the 12 pentagonal defects.")]
+        [SerializeField] bool highlightPentagons = true;
+
+        protected override SpawnTrailData[] GenerateTrailData()
+        {
+            // 1. Generate the underlying icosphere
+            var (vertices, triangles) = GenerateIcosphere(subdivisions);
+
+            // 2. Compute face centroids projected onto the unit sphere
+            int faceCount = triangles.Count / 3;
+            var faceCentroids = new Vector3[faceCount];
+            for (int f = 0; f < faceCount; f++)
+            {
+                Vector3 a = vertices[triangles[f * 3]];
+                Vector3 b = vertices[triangles[f * 3 + 1]];
```

</details>

### `8b4ca6e99` — Add DualMeshGenerator editor tool for hex/pent membrane mesh

_Claude, 2026-02-27 19:44:20 +0000_

```text
Replaces the incorrect SpawnableDualSpherene (which was a procedural prism
spawner) with an editor tool that computes the actual topological dual of
the SkyboxModel triangle mesh used by MembraneBase.

The tool (Tools > Dual Mesh Generator):
1. Reads any triangulated mesh (welds split FBX vertices to recover topology)
2. Computes face centroids as dual vertices (optionally projected onto sphere)
3. Builds dual faces by walking edge adjacency around each original vertex
4. Fan-triangulates the resulting pentagons/hexagons with flat normals
5. Saves the result as a .asset mesh that can replace the MeshFilter reference

Also enables Read/Write on SkyboxModel.fbx so the tool can access mesh data.
```

```text
 Assets/_Models/SkyboxModel.fbx.meta                                   |   2 +-
 Assets/_Scripts/Editor/DualMeshGenerator.cs                           | 522 ++++++++++++++++++++++++++++++++
 .../Game/Environment/MiniGameObjects/SpawnableDualSpherene.cs         | 306 -------------------
 3 files changed, 523 insertions(+), 307 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 840 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Editor/DualMeshGenerator.cs b/Assets/_Scripts/Editor/DualMeshGenerator.cs
new file mode 100644
index 000000000..5abde00e9
--- /dev/null
+++ b/Assets/_Scripts/Editor/DualMeshGenerator.cs
@@ -0,0 +1,522 @@
+using System.Collections.Generic;
+using System.Linq;
+using UnityEditor;
+using UnityEngine;
+
+namespace CosmicShore
+{
+    /// <summary>
+    /// Editor tool that converts a triangulated mesh into its topological dual.
+    ///
+    /// For a triangulated sphere (icosphere), the dual replaces every triangle with a
+    /// vertex at its centroid, and connects centroids of adjacent triangles to form
+    /// polygonal faces:
+    ///   - Vertices with valence 5 (original icosahedron verts) → pentagons
+    ///   - Vertices with valence 6 (subdivision verts) → hexagons
+    ///
+    /// The result is a Goldberg polyhedron — the soccer ball / fullerene / hex-pent
+    /// tiling that Euler's formula demands for a closed sphere.
+    ///
+    /// The generated mesh uses flat normals per face so polygon boundaries are visible
+    /// to edge-detection shaders. Vertex positions are projected onto the source mesh's
+    /// average radius so the spherical shape is preserved.
+    /// </summary>
+    public class DualMeshGenerator : EditorWindow
+    {
+        [SerializeField] Mesh sourceMesh;
+        [SerializeField] bool projectToSphere = true;
+        [SerializeField] bool flatShading = true;
+
+        [MenuItem("Tools/Dual Mesh Generator")]
+        public static void ShowWindow()
+        {
+            var window = GetWindow<DualMeshGenerator>("Dual Mesh Generator");
+            window.minSize = new Vector2(320, 200);
+        }
+
+        void OnGUI()
+        {
+            GUILayout.Label("Dual Mesh Generator", EditorStyles.boldLabel);
+            GUILayout.Label(
+                "Converts a triangulated mesh to its dual.\n" +
+                "Triangles → hexagons + pentagons.",
+                EditorStyles.wordWrappedLabel);
+            GUILayout.Space(10);
+
+            sourceMesh = (Mesh)EditorGUILayout.ObjectField("Source Mesh", sourceMesh, typeof(Mesh), false);
+            projectToSphere = EditorGUILayout.Toggle(
+                new GUIContent("Project to Sphere",
+                    "Project dual vertices onto the average radius sphere. " +
+                    "Keeps the spherical shape clean."),
+                projectToSphere);
+            flatShading = EditorGUILayout.Toggle(
+                new GUIContent("Flat Shading",
+                    "Use per-face normals so polygon edges are visible. " +
+                    "Disable for smooth shading."),
+                flatShading);
+
+            GUILayout.Space(10);
+
+            EditorGUI.BeginDisabledGroup(sourceMesh == null);
+            if (GUILayout.Button("Generate Dual Mesh"))
+                GenerateAndSave();
+            EditorGUI.EndDisabledGroup();
+        }
+
+        void GenerateAndSave()
+        {
+            var dual = ComputeDual(sourceMesh, projectToSphere, flatShading);
+            if (dual == null)
+            {
+                EditorUtility.DisplayDialog("Error", "Failed to generate dual mesh. " +
+                    "Make sure the source mesh is readable (enable Read/Write in import settings).", "OK");
+                return;
+            }
```

</details>

### `163db1f94` — Restore SpawnableDualSpherene.cs to avoid breaking references on pull

_Claude, 2026-02-27 20:33:59 +0000_

```text
 .../Game/Environment/MiniGameObjects/SpawnableDualSpherene.cs         | 306 ++++++++++++++++++++++++++++++++
 1 file changed, 306 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 312 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableDualSpherene.cs b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableDualSpherene.cs
new file mode 100644
index 000000000..31b5a1e3d
--- /dev/null
+++ b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableDualSpherene.cs
@@ -0,0 +1,306 @@
+using CosmicShore.Core;
+using CosmicShore.Game.Spawning;
+using System.Collections.Generic;
+using UnityEngine;
+
+namespace CosmicShore
+{
+    /// <summary>
+    /// Spawns prisms along the edges of the dual of a geodesic polyhedron — a Goldberg
+    /// polyhedron with hexagonal and pentagonal faces.
+    ///
+    /// Starting from a subdivided icosphere (triangular faces), the dual is computed by
+    /// placing a vertex at each triangle's centroid (projected onto the sphere) and
+    /// connecting centroids of triangles that share an original edge. Each of the 12
+    /// original icosahedron vertices has valence 5, producing 12 pentagons; all subdivision
+    /// vertices have valence 6, producing hexagons.
+    ///
+    /// The result is the classic soccer ball / fullerene / Goldberg polyhedron topology:
+    ///   - Subdivision 0 → dodecahedron (12 pentagons, 30 dual edges)
+    ///   - Subdivision 1 → truncated icosahedron (12 pentagons + 20 hexagons, 90 dual edges)
+    ///   - Subdivision 2 → 12 pentagons + 80 hexagons, 360 dual edges
+    ///
+    /// Perlin-noise undulation displaces vertices radially, preserving the organic membrane
+    /// feel while rendering the topology in hex/pent instead of triangles.
+    /// </summary>
+    public class SpawnableDualSpherene : SpawnableBase
+    {
+        [Header("Block Settings")]
+        [SerializeField] Prism prism;
+        [SerializeField] Vector3 blockScale = new Vector3(2f, 2f, 4f);
+
+        [Header("Geodesic Structure")]
+        [Tooltip("Subdivision level of the underlying icosphere whose dual is rendered.\n" +
+                 "0 = dodecahedron (12 pentagons)\n" +
+                 "1 = truncated icosahedron (12 pent + 20 hex)\n" +
+                 "2 = 12 pent + 80 hex")]
+        [Range(0, 3)]
+        [SerializeField] int subdivisions = 2;
+
+        [Tooltip("Radius of the sphere.")]
+        [SerializeField] float radius = 70f;
+
+        [Tooltip("Prism blocks per dual edge. More = smoother curves along edges.")]
+        [SerializeField] int blocksPerEdge = 6;
+
+        [Header("Undulation")]
+        [Tooltip("Amplitude of radial undulation applied to dual vertices.")]
+        [SerializeField] float undulationAmplitude = 3f;
+
+        [Tooltip("Frequency of the undulation pattern across the sphere surface.")]
+        [SerializeField] float undulationFrequency = 4f;
+
+        [Tooltip("Seed offset for undulation noise pattern.")]
+        [SerializeField] float undulationPhase = 0f;
+
+        [Header("Visual")]
+        [SerializeField] Domains edgeDomain = Domains.Blue;
+        [SerializeField] Domains pentagonDomain = Domains.Gold;
+
+        [Tooltip("Place marker blocks at pentagon centers to highlight the 12 pentagonal defects.")]
+        [SerializeField] bool highlightPentagons = true;
+
+        protected override SpawnTrailData[] GenerateTrailData()
+        {
+            // 1. Generate the underlying icosphere
+            var (vertices, triangles) = GenerateIcosphere(subdivisions);
+
+            // 2. Compute face centroids projected onto the unit sphere
+            int faceCount = triangles.Count / 3;
+            var faceCentroids = new Vector3[faceCount];
+            for (int f = 0; f < faceCount; f++)
+            {
+                Vector3 a = vertices[triangles[f * 3]];
+                Vector3 b = vertices[triangles[f * 3 + 1]];
```

</details>

### `711353732` — dual sphere hooked up to crystal capture

_Garrett Milliron, 2026-02-27 15:37:27 -0500_

```text
 Assets/Resources/PerformanceTestRunInfo.json                          |  1 +
 Assets/Resources/PerformanceTestRunInfo.json.meta                     |  7 ++++
 Assets/Resources/PerformanceTestRunSettings.json                      |  1 +
 Assets/Resources/PerformanceTestRunSettings.json.meta                 |  7 ++++
 Assets/_Prefabs/Spawnables/SpawnableConcetricSpheres.prefab           |  2 +-
 Assets/_Prefabs/Spawnables/SpawnableSpherene dual.prefab              | 63 +++++++++++++++++++++++++++++++++
 Assets/_Prefabs/Spawnables/SpawnableSpherene dual.prefab.meta         |  7 ++++
 Assets/_Scripts/Editor/DualMeshGenerator.cs.meta                      |  2 ++
 .../Game/Environment/MiniGameObjects/SpawnableDualSpherene.cs.meta    |  2 ++
 9 files changed, 91 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/Resources/PerformanceTestRunInfo.json b/Assets/Resources/PerformanceTestRunInfo.json
new file mode 100644
index 000000000..574fc4afd
--- /dev/null
+++ b/Assets/Resources/PerformanceTestRunInfo.json
@@ -0,0 +1 @@
+{"TestSuite":"","Date":0,"Player":{"Development":false,"ScreenWidth":0,"ScreenHeight":0,"ScreenRefreshRate":0,"Fullscreen":false,"Vsync":0,"AntiAliasing":0,"Batchmode":false,"RenderThreadingMode":"MultiThreaded","MtRendering":false,"GraphicsJobs":false,"GpuSkinning":true,"Platform":"","ColorSpace":"","AnisotropicFiltering":"","BlendWeights":"","GraphicsApi":"","ScriptingBackend":"IL2CPP","AndroidTargetSdkVersion":"AndroidApiLevel33","AndroidBuildSystem":"Gradle","BuildTarget":"Android","StereoRenderingPath":"MultiPass"},"Hardware":{"OperatingSystem":"","DeviceModel":"","DeviceName":"","ProcessorType":"","ProcessorCount":0,"GraphicsDeviceName":"","SystemMemorySizeMB":0},"Editor":{"Version":"6000.0.62f1","Branch":"6000.0/staging","Changeset":"f99f05b3e950","Date":1761781782},"Dependencies":["com.cysharp.unitask@2.5.10","com.unity.2d.sprite@1.0.0","com.unity.adaptiveperformance@5.1.6","com.unity.adaptiveperformance.samsung.android@5.1.0","com.unity.ads@4.12.0","com.unity.ai.navigation@2.0.9","com.unity.animation.rigging@1.3.0","com.unity.cinemachine@3.1.2","com.unity.collab-proxy@2.10.0","com.unity.device-simulator.devices@1.0.0","com.unity.entities@1.4.2","com.unity.entities.graphics@1.4.15","com.unity.feature.mobile@1.0.0","com.unity.ide.rider@3.0.38","com.unity.ide.visualstudio@2.0.25","com.unity.inputsystem@1.14.2","com.unity.mobile.android-logcat@1.4.6","com.unity.mobile.notifications@2.4.2","com.unity.multiplayer.center@1.0.0","com.unity.multiplayer.center.quickstart@1.0.1","com.unity.multiplayer.playmode@1.6.1","com.unity.multiplayer.tools@2.2.6","com.unity.multiplayer.widgets@1.0.1","com.unity.netcode.gameobjects@2.5.0","com.unity.nuget.newtonsoft-json@3.2.1","com.unity.performance.profile-analyzer@1.2.3","com.unity.purchasing@4.12.2","com.unity.recorder@5.1.2","com.unity.render-pipelines.universal@17.0.4","com.unity.services.analytics@6.2.1","com.unity.services.cloudsave@3.4.0","com.unity.services.core@1.16.0","com.unity.services.leaderboards@2.3.3","com.unity.services.multiplayer@1.1.8","com.unity.test-framework@1.6.0","com.unity.timeline@1.8.9","com.unity.toolchain.win-x86_64-linux-x86_64@2.0.10","com.unity.transport@2.6.0","com.unity.ugui@2.0.0","com.unity.visualeffectgraph@17.0.4","com.veriorpies.parrelsync@1.5.2","jp.hadashikick.vcontainer@1.6.3","com.unity.modules.accessibility@1.0.0","com.unity.modules.ai@1.0.0","com.unity.modules.androidjni@1.0.0","com.unity.modules.animation@1.0.0","com.unity.modules.assetbundle@1.0.0","com.unity.modules.audio@1.0.0","com.unity.modules.cloth@1.0.0","com.unity.modules.director@1.0.0","com.unity.modules.imageconversion@1.0.0","com.unity.modules.imgui@1.0.0","com.unity.modules.jsonserialize@1.0.0","com.unity.modules.particlesystem@1.0.0","com.unity.modules.physics@1.0.0","com.unity.modules.physics2d@1.0.0","com.unity.modules.screencapture@1.0.0","com.unity.modules.terrain@1.0.0","com.unity.modules.terrainphysics@1.0.0","com.unity.modules.tilemap@1.0.0","com.unity.modules.ui@1.0.0","com.unity.modules.uielements@1.0.0","com.unity.modules.umbra@1.0.0","com.unity.modules.unityanalytics@1.0.0","com.unity.modules.unitywebrequest@1.0.0","com.unity.modules.unitywebrequestassetbundle@1.0.0","com.unity.modules.unitywebrequestaudio@1.0.0","com.unity.modules.unitywebrequesttexture@1.0.0","com.unity.modules.unitywebrequestwww@1.0.0","com.unity.modules.vehicles@1.0.0","com.unity.modules.video@1.0.0","com.unity.modules.vr@1.0.0","com.unity.modules.wind@1.0.0","com.unity.modules.xr@1.0.0","com.unity.modules.subsystems@1.0.0","com.unity.modules.hierarchycore@1.0.0","com.unity.shadergraph@17.0.4","com.unity.render-pipelines.core@17.0.4","com.unity.burst@1.8.25","com.unity.collections@2.6.2","com.unity.mathematics@1.3.2","com.unity.sysroot@2.0.10","com.unity.sysroot.linux-x86_64@2.0.9","com.unity.ext.nunit@2.0.5","com.unity.services.qos@1.3.0","com.unity.services.wire@1.4.0","com.unity.services.deployment@1.3.0","com.unity.services.authentication@3.4.1","com.unity.render-pipelines.universal-config@17.0.3","com.unity.bindings.openimageio@1.0.0","com.unity.nuget.mono-cecil@1.11.5","com.unity.profiling.core@1.0.2","com.unity.serialization@3.1.2","com.unity.scriptablebuildpipeline@2.4.3","com.unity.test-framework.performance@3.2.0","com.unity.splines@2.8.2","com.unity.rendering.light-transport@1.0.1","com.unity.searcher@4.9.3","com.unity.services.deployment.api@1.0.0","com.unity.settings-manager@2.1.0"],"Results":[]}
\ No newline at end of file
diff --git a/Assets/Resources/PerformanceTestRunSettings.json b/Assets/Resources/PerformanceTestRunSettings.json
new file mode 100644
index 000000000..49438ae14
--- /dev/null
+++ b/Assets/Resources/PerformanceTestRunSettings.json
@@ -0,0 +1 @@
+{"MeasurementCount":-1}
\ No newline at end of file
```

</details>

### `b7efb0133` — Add runtime DualMeshUtility and DualMembrane component for hex/pent membranes

_Claude, 2026-02-28 18:09:55 +0000_

```text
Extract core dual mesh algorithm from editor-only DualMeshGenerator into
DualMeshUtility (runtime-accessible). Add DualMembrane MonoBehaviour that
generates and optionally animates a hex/pent dual mesh at runtime — drop-in
replacement for static MembraneBase prefab via CellConfigDataSO.MembranePrefab.

DualMeshGenerator now delegates to the shared utility. SpawnableDualSpherene
reuses DualMeshUtility.SampleUndulation instead of its own copy.
```

```text
 Assets/_Scripts/Editor/DualMeshGenerator.cs                           | 438 +-----------------------------
 Assets/_Scripts/Game/Environment/DualMembrane.cs                      | 137 ++++++++++
 Assets/_Scripts/Game/Environment/DualMeshUtility.cs                   | 461 ++++++++++++++++++++++++++++++++
 .../Game/Environment/MiniGameObjects/SpawnableDualSpherene.cs         |  18 +-
 4 files changed, 605 insertions(+), 449 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1112 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Editor/DualMeshGenerator.cs b/Assets/_Scripts/Editor/DualMeshGenerator.cs
index 5abde00e9..036e88230 100644
--- a/Assets/_Scripts/Editor/DualMeshGenerator.cs
+++ b/Assets/_Scripts/Editor/DualMeshGenerator.cs
@@ -1,5 +1,4 @@
-using System.Collections.Generic;
-using System.Linq;
+using CosmicShore.Game;
 using UnityEditor;
 using UnityEngine;
 
@@ -20,6 +19,9 @@ namespace CosmicShore
     /// The generated mesh uses flat normals per face so polygon boundaries are visible
     /// to edge-detection shaders. Vertex positions are projected onto the source mesh's
     /// average radius so the spherical shape is preserved.
+    ///
+    /// Core algorithm lives in DualMeshUtility (runtime-accessible). This window
+    /// provides the editor UI for baking mesh assets to disk.
     /// </summary>
     public class DualMeshGenerator : EditorWindow
     {
@@ -65,7 +67,7 @@ namespace CosmicShore
 
         void GenerateAndSave()
         {
-            var dual = ComputeDual(sourceMesh, projectToSphere, flatShading);
+            var dual = DualMeshUtility.ComputeDual(sourceMesh, projectToSphere, flatShading);
             if (dual == null)
             {
                 EditorUtility.DisplayDialog("Error", "Failed to generate dual mesh. " +
@@ -88,435 +90,5 @@ namespace CosmicShore
             Debug.Log($"[DualMeshGenerator] Saved {path} — " +
                       $"{dual.vertexCount} verts, {dual.triangles.Length / 3} tris");
         }
-
-        /// <summary>
-        /// Core algorithm: triangulated mesh → dual mesh with polygonal faces.
-        /// </summary>
-        public static Mesh ComputeDual(Mesh source, bool projectToSphere, bool flatShading)
-        {
-            Vector3[] srcVerts = source.vertices;
-            Vector3[] srcNormals = source.normals;
-            int[] srcTris = source.triangles;
-            if (srcTris.Length < 3) return null;
-
-            int srcFaceCount = srcTris.Length / 3;
-
-            // Detect whether source normals point inward or outward.
-            // Membrane meshes are viewed from inside → normals point inward.
-            bool normalsPointInward = DetectInwardNormals(srcVerts, srcNormals);
-
-            // ── Step 1: Weld split vertices ────────────────────────────────────
-            // FBX/modeled meshes duplicate vertices per-face for flat normals.
-            // We merge by position to recover the true topological connectivity.
-            var (weldMap, weldedPositions) = WeldVertices(srcVerts);
-
-            // Remap triangle indices to welded space
-            var faces = new List<(int a, int b, int c)>(srcFaceCount);
-            for (int f = 0; f < srcFaceCount; f++)
-            {
-                int a = weldMap[srcTris[f * 3]];
-                int b = weldMap[srcTris[f * 3 + 1]];
-                int c = weldMap[srcTris[f * 3 + 2]];
-                if (a != b && b != c && a != c)
-                    faces.Add((a, b, c));
-            }
-
-            int faceCount = faces.Count;
-            if (faceCount == 0) return null;
-
-            // ── Step 2: Dual vertices = face centroids ─────────────────────────
-            float avgRadius = 0f;
-            for (int i = 0; i < weldedPositions.Length; i++)
-                avgRadius += weldedPositions[i].magnitude;
-            avgRadius /= weldedPositions.Length;
-
-            var dualVerts = new Vector3[faceCount];
-            for (int f = 0; f < faceCount; f++)
-            {
-                var (a, b, c) = faces[f];
```

</details>

### `a21298a09` — Add SpawnableDualSpherene to HexRace scene as guaranteed spawnable

_Claude, 2026-03-01 19:43:00 +0000_

```text
Adds an inline SpawnableDualSpherene (subdivision 2, radius 150, 4 blocks/edge,
undulation amp 5) to the Spawners hierarchy in MinigameHexRace. Configured as
a guaranteed spawnable so it always appears alongside the waypoint track.

Also fixes SegmentSpawner serialization to use current weightedSegments +
guaranteedSpawnables format (was using orphaned old field names).
```

```text
 Assets/_Scenes/Multiplayer Scenes/MinigameHexRace.unity | 74 +++++++++++++++++++++++++++++++++++++++++++----
 1 file changed, 68 insertions(+), 6 deletions(-)
```

### `5ed5dd603` — Add DualMembrane to MembraneBase prefab, place BigMembraneVariant in HexRace

_Claude, 2026-03-02 08:56:06 +0000_

```text
MembraneBase now has a DualMembrane component that reads the SkyboxModel geobox
mesh at Awake and replaces it with the topological dual (hex/pent Goldberg
polyhedron). The existing SkyboxModelGraphMaterial and BigMembraneVariant scale
(1600) are preserved — only the mesh geometry changes.

HexRace scene: added BigMembraneVariant prefab instance at origin so the dual
membrane is visible when testing. Also fixed SegmentSpawner serialization to
use current weightedSegments format.
```

```text
 Assets/_Prefabs/Environment/MembraneBase.prefab          |  20 +++++++++
 Assets/_Scenes/Multiplayer Scenes/MinigameHexRace.unity  | 103 ++++++++++++++++++---------------------------
 Assets/_Scripts/Game/Environment/DualMembrane.cs.meta    |  11 +++++
 Assets/_Scripts/Game/Environment/DualMeshUtility.cs.meta |  11 +++++
 4 files changed, 82 insertions(+), 63 deletions(-)
```

### `23d990507` — Fix membrane shader: use smooth normals instead of flat shading

_Claude, 2026-03-03 19:06:55 +0000_

```text
The SkyboxModelGraph shader uses Fresnel and vertex displacement along normals.
Flat per-face normals broke both effects — Fresnel gave uniform values per polygon
instead of a smooth radial gradient, and vertex displacement stepped at polygon
boundaries. Smooth normals restore the original shader behavior while keeping the
hex/pent geometry visible through polygon edges.
```

```text
 Assets/_Prefabs/Environment/MembraneBase.prefab | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```

### `32f5587af` — Revert runtime DualMembrane, add editor-baked dual membrane generator

_Claude, 2026-03-03 19:44:13 +0000_

```text
Remove DualMembrane component from MembraneBase.prefab — restore it to its
original state (just MeshFilter + MeshRenderer, no runtime mesh swapping).

Enhance DualMeshGenerator editor window with a "Generate Dual Membrane" button
that bakes everything to disk in one click:
  - Dual mesh asset saved to Assets/_Graphics/Meshes/
  - Prefab with MeshFilter + MeshRenderer saved to Assets/_Prefabs/Environment/
  - Material and scale configurable in the editor window
  - Updates mesh in-place if asset already exists (preserves references)

Open Tools > Dual Mesh Generator, assign the SkyboxModel mesh and
SkyboxModelGraphMaterial, toggle flat/smooth shading, click Generate.
All assets are inspectable in the editor.
```

```text
 Assets/_Prefabs/Environment/MembraneBase.prefab |  20 ----------
 Assets/_Scripts/Editor/DualMeshGenerator.cs     | 113 ++++++++++++++++++++++++++++++++++++++++++++++--------
 2 files changed, 96 insertions(+), 37 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 167 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Editor/DualMeshGenerator.cs b/Assets/_Scripts/Editor/DualMeshGenerator.cs
index 036e88230..16ce6652e 100644
--- a/Assets/_Scripts/Editor/DualMeshGenerator.cs
+++ b/Assets/_Scripts/Editor/DualMeshGenerator.cs
@@ -5,7 +5,8 @@ using UnityEngine;
 namespace CosmicShore
 {
     /// <summary>
-    /// Editor tool that converts a triangulated mesh into its topological dual.
+    /// Editor tool that converts a triangulated mesh into its topological dual
+    /// and optionally bakes a complete membrane prefab (mesh + material).
     ///
     /// For a triangulated sphere (icosphere), the dual replaces every triangle with a
     /// vertex at its centroid, and connects centroids of adjacent triangles to form
@@ -13,15 +14,8 @@ namespace CosmicShore
     ///   - Vertices with valence 5 (original icosahedron verts) → pentagons
     ///   - Vertices with valence 6 (subdivision verts) → hexagons
     ///
-    /// The result is a Goldberg polyhedron — the soccer ball / fullerene / hex-pent
-    /// tiling that Euler's formula demands for a closed sphere.
-    ///
-    /// The generated mesh uses flat normals per face so polygon boundaries are visible
-    /// to edge-detection shaders. Vertex positions are projected onto the source mesh's
-    /// average radius so the spherical shape is preserved.
-    ///
     /// Core algorithm lives in DualMeshUtility (runtime-accessible). This window
-    /// provides the editor UI for baking mesh assets to disk.
+    /// provides the editor UI for baking mesh assets and prefabs to disk.
     /// </summary>
     public class DualMeshGenerator : EditorWindow
     {
@@ -29,11 +23,15 @@ namespace CosmicShore
         [SerializeField] bool projectToSphere = true;
         [SerializeField] bool flatShading = true;
 
+        [Header("Membrane Prefab")]
+        [SerializeField] Material membraneMaterial;
+        [SerializeField] float prefabScale = 1000f;
+
         [MenuItem("Tools/Dual Mesh Generator")]
         public static void ShowWindow()
         {
             var window = GetWindow<DualMeshGenerator>("Dual Mesh Generator");
-            window.minSize = new Vector2(320, 200);
+            window.minSize = new Vector2(360, 340);
         }
 
         void OnGUI()
@@ -48,24 +46,44 @@ namespace CosmicShore
             sourceMesh = (Mesh)EditorGUILayout.ObjectField("Source Mesh", sourceMesh, typeof(Mesh), false);
             projectToSphere = EditorGUILayout.Toggle(
                 new GUIContent("Project to Sphere",
-                    "Project dual vertices onto the average radius sphere. " +
-                    "Keeps the spherical shape clean."),
+                    "Project dual vertices onto the average radius sphere."),
                 projectToSphere);
             flatShading = EditorGUILayout.Toggle(
                 new GUIContent("Flat Shading",
-                    "Use per-face normals so polygon edges are visible. " +
-                    "Disable for smooth shading."),
+                    "Per-face normals for polygon edge visibility. " +
+                    "Disable for smooth shading (preserves Fresnel/displacement behavior)."),
                 flatShading);
 
             GUILayout.Space(10);
 
             EditorGUI.BeginDisabledGroup(sourceMesh == null);
-            if (GUILayout.Button("Generate Dual Mesh"))
-                GenerateAndSave();
+            if (GUILayout.Button("Save Dual Mesh Asset..."))
+                GenerateAndSaveMesh();
+            EditorGUI.EndDisabledGroup();
+
+            GUILayout.Space(20);
+            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
+            GUILayout.Label("Dual Membrane Prefab", EditorStyles.boldLabel);
+            GUILayout.Label(
+                "One-click: generates dual mesh + prefab with MeshFilter/MeshRenderer.\n" +
+                "Assets saved to Assets/_Graphics/Meshes/ and Assets/_Prefabs/Environment/.",
+                EditorStyles.wordWrappedLabel);
```

</details>

### `17dd01edd` — Remove invalid SerializeField from GameCard.Favorited property

_Claude, 2026-09-02 02:10:45 +0000_

```text
SerializeField is only valid on fields. Roslyn now enforces CS0592 on this
pre-existing declaration. The backing field is private and unserialized, so
the attribute never had any effect.
```

```text
 Assets/_Scripts/App/UI/Elements/GameCard.cs | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/App/UI/Elements/GameCard.cs b/Assets/_Scripts/App/UI/Elements/GameCard.cs
index 55db9fe49..911b0d77c 100644
--- a/Assets/_Scripts/App/UI/Elements/GameCard.cs
+++ b/Assets/_Scripts/App/UI/Elements/GameCard.cs
@@ -26,7 +26,7 @@ namespace CosmicShore.App.UI.Elements
         [SerializeField] int Index;
 
         bool favorited;
-        [SerializeField] public bool Favorited
+        public bool Favorited
         {
             get { return favorited; }
             set
```

</details>

_Also contains 1 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
