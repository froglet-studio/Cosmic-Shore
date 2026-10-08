# Branch archive: `suction-shader`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-01-03 by Christopher Stackpole
- **Unmerged commits:** 2
- **Forked from:** `b9ffec7fa` (2026-01-03, Configure Hex Race)
- **Tip:** `fb78d0dcc`
- **Files touched (23):**
  - `Assets/Editor/MeshGeneration/PrismMesh.cs`
  - `Assets/_Graphics/Materials/BigCageMaterial.mat`
  - `Assets/_Graphics/Materials/BlockMaterials/SuctionShaderMaterial.mat`
  - `Assets/_Graphics/Materials/Graphs/PrismGraphs/SequentialFaceConverger.shadersubgraph`
  - `Assets/_Graphics/Materials/Graphs/PrismGraphs/Subgraphs/VectorToHextant.shadersubgraph`
  - `Assets/_Graphics/Materials/Graphs/PrismGraphs/SuctionGraph.shadergraph`
  - `Assets/_Graphics/Materials/Graphs/PrismGraphs/SuctionMaterial.mat`
  - `Assets/_Graphics/Materials/Graphs/PrismGraphs/SuctionTestMat.mat`
  - `Assets/_Graphics/Materials/Graphs/PrismGraphs/SuctionTestMat.mat.meta`
  - `Assets/_Graphics/Materials/Graphs/PrismGraphs/VectorToFirstandLastFaces.shadersubgraph`
  - `Assets/_Graphics/Materials/Graphs/PrismGraphs/VertexFaceBools.shadersubgraph`
  - `Assets/_Graphics/Materials/Graphs/PrismGraphs/VertexIdToVectorEmbeddedFaceOrder.shadersubgraph`
  - `Assets/_Graphics/Materials/Graphs/PrismGraphs/VertexIdToVectorEmbeddedFaceOrder.shadersubgraph.meta`
  - `Assets/_Models/Testing/Prism.asset`
  - `Assets/_Prefabs/UI Elements/ShipHUD/MantaHUDVariant.prefab`
  - `Assets/_Prefabs/UI Elements/ShipHUD/SerpentHUDVariant.prefab`
  - `Assets/_Prefabs/UI Elements/ShipHUD/ShipHUDPrefab.prefab`
  - `Assets/_Prefabs/UI Elements/ShipHUD/SquirrelHUDVariant.prefab`
  - `Assets/_Scenes/TestScenes/CSShaderTest.unity`
  - `Assets/_Scenes/TestScenes/CSShaderTest.unity.meta`
  - `Assets/_Scripts/ShaderIntegration.meta`
  - `Assets/_Scripts/ShaderIntegration/SuctionShaderInterface.cs`
  - `Assets/_Scripts/ShaderIntegration/SuctionShaderInterface.cs.meta`

### `444f0b21b` — per face suction

_Christopher Stackpole, 2026-01-03 17:29:13 -0500_

```text
 Assets/Editor/MeshGeneration/PrismMesh.cs                             |    4 +-
 Assets/_Graphics/Materials/BigCageMaterial.mat                        |    5 +
 Assets/_Graphics/Materials/BlockMaterials/SuctionShaderMaterial.mat   |   11 +-
 .../Graphs/PrismGraphs/SequentialFaceConverger.shadersubgraph         | 7437 +++----------------------------
 .../Graphs/PrismGraphs/Subgraphs/VectorToHextant.shadersubgraph       |  337 +-
 .../_Graphics/Materials/Graphs/PrismGraphs/SuctionGraph.shadergraph   | 1876 +++++---
 Assets/_Graphics/Materials/Graphs/PrismGraphs/SuctionMaterial.mat     |   85 +-
 Assets/_Graphics/Materials/Graphs/PrismGraphs/SuctionTestMat.mat      |   81 +
 Assets/_Graphics/Materials/Graphs/PrismGraphs/SuctionTestMat.mat.meta |    8 +
 .../Graphs/PrismGraphs/VectorToFirstandLastFaces.shadersubgraph       | 6589 +++++++++++++++++++++++----
 .../Materials/Graphs/PrismGraphs/VertexFaceBools.shadersubgraph       |  192 +-
 .../PrismGraphs/VertexIdToVectorEmbeddedFaceOrder.shadersubgraph      | 6176 +++++++++++++++++++++++++
 .../PrismGraphs/VertexIdToVectorEmbeddedFaceOrder.shadersubgraph.meta |   10 +
 Assets/_Models/Testing/Prism.asset                                    |    2 +-
 Assets/_Prefabs/UI Elements/ShipHUD/MantaHUDVariant.prefab            |    3 +-
 Assets/_Prefabs/UI Elements/ShipHUD/SerpentHUDVariant.prefab          |   59 +-
 Assets/_Prefabs/UI Elements/ShipHUD/ShipHUDPrefab.prefab              |   40 -
 Assets/_Prefabs/UI Elements/ShipHUD/SquirrelHUDVariant.prefab         |    3 +-
 Assets/_Scenes/TestScenes/CSShaderTest.unity                          |  472 ++
 Assets/_Scenes/TestScenes/CSShaderTest.unity.meta                     |    7 +
 Assets/_Scripts/ShaderIntegration.meta                                |    8 +
 Assets/_Scripts/ShaderIntegration/SuctionShaderInterface.cs           |   52 +
 Assets/_Scripts/ShaderIntegration/SuctionShaderInterface.cs.meta      |    2 +
 23 files changed, 14570 insertions(+), 8889 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/Editor/MeshGeneration/PrismMesh.cs b/Assets/Editor/MeshGeneration/PrismMesh.cs
index 5642679ed..90f882c2f 100644
--- a/Assets/Editor/MeshGeneration/PrismMesh.cs
+++ b/Assets/Editor/MeshGeneration/PrismMesh.cs
@@ -35,8 +35,8 @@ public static class PrismMeshGenerator
             (Vector3.back,     new Vector3(s,-s,-s), new Vector3(-s,-s,-s), new Vector3(-s, s,-s), new Vector3(s, s,-s)),
             (Vector3.left,     new Vector3(-s,-s,-s), new Vector3(-s,-s, s), new Vector3(-s, s, s), new Vector3(-s, s,-s)),
             (Vector3.right,    new Vector3(s,-s, s), new Vector3(s,-s,-s), new Vector3(s, s,-s), new Vector3(s, s, s)),
-            (Vector3.up,       new Vector3(-s, s, s), new Vector3(s, s, s), new Vector3(s, s,-s), new Vector3(-s, s,-s)),
-            (Vector3.down,     new Vector3(-s,-s,-s), new Vector3(s,-s,-s), new Vector3(s,-s, s), new Vector3(-s,-s, s))
+            (Vector3.down,     new Vector3(-s,-s,-s), new Vector3(s,-s,-s), new Vector3(s,-s, s), new Vector3(-s,-s, s)),
+            (Vector3.up,       new Vector3(-s, s, s), new Vector3(s, s, s), new Vector3(s, s,-s), new Vector3(-s, s,-s))
         };
 
         // 6 faces * 4 triangles * 3 verts = 72 verts
diff --git a/Assets/_Scripts/ShaderIntegration/SuctionShaderInterface.cs b/Assets/_Scripts/ShaderIntegration/SuctionShaderInterface.cs
new file mode 100644
index 000000000..e1462a9ea
--- /dev/null
+++ b/Assets/_Scripts/ShaderIntegration/SuctionShaderInterface.cs
@@ -0,0 +1,52 @@
+using System.Collections;
+using UnityEngine;
+
+namespace CosmicShore
+{
+    public class SuctionShaderInterface : MonoBehaviour
+    {
+        public void ApplySuctionToTarget(GameObject target, Vector3 slocation, float duration = 5)
+        {
+            // Determine Necessary Information
+            float inverse_partial = 6 / duration;
+            int[] face_order = GenerateFaceOrder(target, slocation);
+            Material suction_material = new Material(suction_material_base);
+
+            // Encode Face Order Into Vertices
+            Vector3 pull_directions1 = new Vector3(face_order[0], face_order[1], face_order[2]);
+            Vector3 pull_directions2 = new Vector3(face_order[3], face_order[2], face_order[3]);
+
+            // Pass In Values And Apply Material
+            suction_material.SetFloat("InversePartialDuration", inverse_partial);
+            suction_material.SetVector("SuctionLocation", slocation);
+            suction_material.SetVector("PullDirections1", pull_directions1);
+            suction_material.SetVector("PullDirections2", pull_directions2);
+            suction_material.SetFloat("StartTime", Time.time);
+
+            MeshRenderer renderer = target.GetComponent<MeshRenderer>();
+            renderer.material = suction_material;
+
+            // Set Callback to Remove Shader After Duration
+            StartCoroutine(RemoveMaterialAfterDuration(target, suction_material, duration));
+        }
+
+        private int[] GenerateFaceOrder(GameObject target, Vector3 slocataion)
+        {
+            int[] face_order = new int[6];
+
+            // Generate Face Order
+
+            return face_order;
+        }
+
+        private IEnumerator RemoveMaterialAfterDuration(GameObject target, Material mat, float duration)
+        {
+            yield return new WaitForSeconds(duration);
+
+            // Remove Material
+        }
+
+        [SerializeField]
+        private Material suction_material_base;
+    }
+}
```

</details>

### `fb78d0dcc` — suction per triangle (unordered)

_Christopher Stackpole, 2026-01-03 18:01:41 -0500_

```text
triangles are sucked to the target instead of entire faces at a time, there is no order by proximity in the triangles; rather it is order by creation sequence (as per MeshGeneration/Prism.cs)
```

```text
 Assets/_Graphics/Materials/BlockMaterials/SuctionShaderMaterial.mat   |   4 +-
 .../Graphs/PrismGraphs/SequentialFaceConverger.shadersubgraph         | 468 +++++++++++++++++++++++++++++-
 .../_Graphics/Materials/Graphs/PrismGraphs/SuctionGraph.shadergraph   |  58 +++-
 .../PrismGraphs/VertexIdToVectorEmbeddedFaceOrder.shadersubgraph      | 497 ++++++++++++++++++++++++++++++++
 Assets/_Scripts/ShaderIntegration/SuctionShaderInterface.cs           |   2 +-
 5 files changed, 1008 insertions(+), 21 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/ShaderIntegration/SuctionShaderInterface.cs b/Assets/_Scripts/ShaderIntegration/SuctionShaderInterface.cs
index e1462a9ea..fc53c1121 100644
--- a/Assets/_Scripts/ShaderIntegration/SuctionShaderInterface.cs
+++ b/Assets/_Scripts/ShaderIntegration/SuctionShaderInterface.cs
@@ -8,7 +8,7 @@ namespace CosmicShore
         public void ApplySuctionToTarget(GameObject target, Vector3 slocation, float duration = 5)
         {
             // Determine Necessary Information
-            float inverse_partial = 6 / duration;
+            float inverse_partial = (float)24 / duration;
             int[] face_order = GenerateFaceOrder(target, slocation);
             Material suction_material = new Material(suction_material_base);
 
```

</details>
