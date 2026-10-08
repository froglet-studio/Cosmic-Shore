# Branch archive: `codex/add-fbx-support-to-blend-shape-system`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2025-08-07 by Garrett Milliron
- **Unmerged commits:** 3
- **Open pull request:** https://github.com/froglet-studio/Cosmic-Shore/pull/60
- **Forked from:** `459c4dbde` (2025-08-06, Tuned Team Crystal Values + Changed Action Button for Crystal)
- **Tip:** `b07ffc385`
- **Files touched (7):**
  - `Assets/BlendShapes/BlendShapeAnimation.shader`
  - `Assets/BlendShapes/BlendShapeBatchProcessor.cs`
  - `Assets/BlendShapes/BlendShapeDebugger.cs`
  - `Assets/BlendShapes/BlendShapeExtractor.cs`
  - `Assets/BlendShapes/BlendShapePerformanceMonitor.cs`
  - `Assets/BlendShapes/BlendShapeValidator.cs`
  - `Docs/BlendShapeSetupChecklist.md`

### `3e36016cc` — Add blend shape utilities, shader enhancements, and setup guide

_Garrett Milliron, 2025-08-06 17:52:22 -0400_

```text
 Assets/BlendShapes/BlendShapeAnimation.shader      | 157 +++++++++++++++++++++++++++++++++++++++++++++++++++
 Assets/BlendShapes/BlendShapeBatchProcessor.cs     |  96 +++++++++++++++++++++++++++++++
 Assets/BlendShapes/BlendShapeDebugger.cs           | 138 ++++++++++++++++++++++++++++++++++++++++++++
 Assets/BlendShapes/BlendShapePerformanceMonitor.cs |  34 +++++++++++
 Assets/BlendShapes/BlendShapeValidator.cs          | 138 ++++++++++++++++++++++++++++++++++++++++++++
 Docs/BlendShapeSetupChecklist.md                   |  31 ++++++++++
 6 files changed, 594 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 150 of 630 lines)</summary>

```diff
diff --git a/Assets/BlendShapes/BlendShapeAnimation.shader b/Assets/BlendShapes/BlendShapeAnimation.shader
new file mode 100644
index 000000000..5324976db
--- /dev/null
+++ b/Assets/BlendShapes/BlendShapeAnimation.shader
@@ -0,0 +1,157 @@
+Shader "Custom/BlendShapeAnimation"
+{
+    Properties
+    {
+        _BlendShapeData ("Blend Shape Data", 2D) = "white" {}
+        _AnimationDuration ("Animation Duration", Float) = 1
+        [Header(Debug Options)]
+        _UseManualWeights ("Use Manual Weights", Float) = 0
+        _BlendWeights ("Manual Blend Weights", Vector) = (0,0,0,0)
+        _PauseAtProgress ("Pause At Progress", Range(0,1)) = 0
+        _DebugMode ("Debug Mode", Float) = 0
+    }
+    SubShader
+    {
+        Tags { "RenderType"="Opaque" }
+        Pass
+        {
+            CGPROGRAM
+            #pragma vertex vert
+            #pragma fragment frag
+            #pragma target 4.5
+            #include "UnityCG.cginc"
+
+            sampler2D _BlendShapeData;
+            float _AnimationDuration;
+            float _UseManualWeights;
+            float4 _BlendWeights;
+            float _PauseAtProgress;
+            float _DebugMode;
+
+            // Sample blend shape offset placeholder
+            float3 SampleBlendShapeOffset(uint vertexID, uint shapeIndex, bool sampleNormal)
+            {
+                return float3(0,0,0);
+            }
+
+            float4 GetBlendShapeWeightsEnhanced(float time)
+            {
+                // Check for manual override
+                if (_UseManualWeights > 0.5)
+                {
+                    return _BlendWeights;
+                }
+                
+                // Check for pause
+                float normalizedTime;
+                if (_PauseAtProgress > 0)
+                {
+                    normalizedTime = _PauseAtProgress;
+                }
+                else
+                {
+                    normalizedTime = frac(time / _AnimationDuration);
+                }
+                
+                float4 weights = float4(0, 0, 0, 0);
+                
+                // Enhanced easing for smoother transitions
+                if (normalizedTime < 0.25)
+                {
+                    float t = normalizedTime * 4.0;
+                    t = smoothstep(0.0, 1.0, t); // Smooth interpolation
+                    weights.x = t;
+                }
+                else if (normalizedTime < 0.5)
+                {
+                    float t = (normalizedTime - 0.25) * 4.0;
+                    t = smoothstep(0.0, 1.0, t);
+                    weights.x = 1.0;
+                    weights.y = t;
+                }
+                else if (normalizedTime < 0.75)
+                {
+                    float t = (normalizedTime - 0.5) * 4.0;
+                    t = smoothstep(0.0, 1.0, t);
+                    weights.z = t;
+                }
+                else
+                {
+                    float t = (normalizedTime - 0.75) * 4.0;
+                    t = smoothstep(0.0, 1.0, t);
+                    weights.z = 1.0;
+                    weights.w = t;
+                }
+                
+                return weights;
+            }
+
+            void ApplyBlendShapesWithNormalPreservation(
+                inout float3 positionOS, 
+                inout float3 normalOS, 
+                inout float3 tangentOS,
+                uint vertexID)
+            {
+                float4 weights = GetBlendShapeWeightsEnhanced(_Time.y);
+                
+                // Store original normal for blending
+                float3 originalNormal = normalOS;
+                float3 accumulatedNormalDelta = float3(0, 0, 0);
+                
+                // Apply each blend shape
+                for (uint i = 0; i < 4; i++)
+                {
+                    if (weights[i] > 0.001)
+                    {
+                        float3 vertexDelta = SampleBlendShapeOffset(vertexID, i, false);
+                        float3 normalDelta = SampleBlendShapeOffset(vertexID, i, true);
+                        
+                        positionOS += vertexDelta * weights[i];
+                        accumulatedNormalDelta += normalDelta * weights[i];
+                    }
+                }
+                
+                // Apply normal delta with preservation
+                normalOS += accumulatedNormalDelta;
+                
+                // Ensure normal remains unit length
+                normalOS = normalize(normalOS);
+                
+                // Recalculate tangent if needed
+                if (length(tangentOS) > 0.001)
+                {
+                    tangentOS = normalize(tangentOS - dot(tangentOS, normalOS) * normalOS);
+                }
+            }
+
+            struct appdata
+            {
+                float4 vertex : POSITION;
+                float3 normal : NORMAL;
+                float4 tangent : TANGENT;
+            };
+
+            struct v2f
+            {
+                float4 pos : SV_POSITION;
+            };
+
+            v2f vert(appdata v, uint id : SV_VertexID)
+            {
+                float3 pos = v.vertex.xyz;
+                float3 norm = v.normal;
+                float3 tan = v.tangent.xyz;
+                ApplyBlendShapesWithNormalPreservation(pos, norm, tan, id);
```

</details>

### `098918e7d` — Add blend shape extractor

_Garrett Milliron, 2025-08-07 15:00:15 -0400_

```text
 Assets/BlendShapes/BlendShapeDebugger.cs           |  3 +--
 Assets/BlendShapes/BlendShapeExtractor.cs          | 71 ++++++++++++++++++++++++++++++++++++++++++++++++++++
 Assets/BlendShapes/BlendShapePerformanceMonitor.cs | 10 ++++++--
 3 files changed, 80 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/BlendShapes/BlendShapeDebugger.cs b/Assets/BlendShapes/BlendShapeDebugger.cs
index d8e3f5f61..addf6f1de 100644
--- a/Assets/BlendShapes/BlendShapeDebugger.cs
+++ b/Assets/BlendShapes/BlendShapeDebugger.cs
@@ -65,8 +65,7 @@ public class BlendShapeDebugger : MonoBehaviour
         if (renderer != null)
         {
             // Calculate what the weights would be at this progress
-            Vector4 weights = CalculateWeightsAtProgress(animationProgress);
-            propertyBlock.SetVector("_DebugProgress", weights);
+            CalculateWeightsAtProgress(animationProgress);
             propertyBlock.SetFloat("_PauseAtProgress", animationProgress);
             renderer.SetPropertyBlock(propertyBlock);
         }
diff --git a/Assets/BlendShapes/BlendShapeExtractor.cs b/Assets/BlendShapes/BlendShapeExtractor.cs
new file mode 100644
index 000000000..257eb76ed
--- /dev/null
+++ b/Assets/BlendShapes/BlendShapeExtractor.cs
@@ -0,0 +1,71 @@
+using UnityEngine;
+
+public class BlendShapeExtractor : MonoBehaviour
+{
+    [Header("Source & Target")]
+    public SkinnedMeshRenderer sourceRenderer;
+    public Material targetMaterial;
+    public Texture2D blendShapeTexture;
+
+    [ContextMenu("Extract and Setup")]
+    public void ExtractAndSetup()
+    {
+        if (sourceRenderer == null || targetMaterial == null)
+        {
+            Debug.LogError("Missing source renderer or target material!");
+            return;
+        }
+
+        Mesh mesh = sourceRenderer.sharedMesh;
+        if (mesh == null)
+        {
+            Debug.LogError("Source renderer has no mesh!");
+            return;
+        }
+
+        int vertexCount = mesh.vertexCount;
+        int shapeCount = Mathf.Min(mesh.blendShapeCount, 4);
+
+        blendShapeTexture = new Texture2D(vertexCount, shapeCount * 2, TextureFormat.RGBAHalf, false);
+        blendShapeTexture.wrapMode = TextureWrapMode.Clamp;
+        blendShapeTexture.filterMode = FilterMode.Point;
+
+        Vector3[] deltaVertices = new Vector3[vertexCount];
+        Vector3[] deltaNormals = new Vector3[vertexCount];
+        Vector3[] deltaTangents = new Vector3[vertexCount];
+
+        for (int s = 0; s < shapeCount; s++)
+        {
+            int frameIndex = mesh.GetBlendShapeFrameCount(s) - 1;
+            mesh.GetBlendShapeFrameVertices(s, frameIndex, deltaVertices, deltaNormals, deltaTangents);
+
+            for (int v = 0; v < vertexCount; v++)
+            {
+                Vector3 dv = deltaVertices[v];
+                Vector3 dn = deltaNormals[v];
+                blendShapeTexture.SetPixel(v, s * 2, new Color(dv.x, dv.y, dv.z, 1f));
+                blendShapeTexture.SetPixel(v, s * 2 + 1, new Color(dn.x, dn.y, dn.z, 1f));
+            }
+        }
+
+        blendShapeTexture.Apply();
+        targetMaterial.SetTexture("_BlendShapeData", blendShapeTexture);
+
+        // Create static mesh without blend shapes
+        Mesh instanceMesh = Instantiate(mesh);
+        instanceMesh.name = mesh.name + "_Instance";
+        instanceMesh.ClearBlendShapes();
+
+        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
+        if (meshRenderer == null)
+            meshRenderer = gameObject.AddComponent<MeshRenderer>();
+        MeshFilter meshFilter = GetComponent<MeshFilter>();
+        if (meshFilter == null)
+            meshFilter = gameObject.AddComponent<MeshFilter>();
+
+        meshRenderer.sharedMaterial = targetMaterial;
+        meshFilter.sharedMesh = instanceMesh;
+
+        sourceRenderer.enabled = false;
+    }
+}
diff --git a/Assets/BlendShapes/BlendShapePerformanceMonitor.cs b/Assets/BlendShapes/BlendShapePerformanceMonitor.cs
index 34b058ec8..fcb573bc0 100644
--- a/Assets/BlendShapes/BlendShapePerformanceMonitor.cs
+++ b/Assets/BlendShapes/BlendShapePerformanceMonitor.cs
@@ -1,5 +1,8 @@
 using UnityEngine;
 using UnityEngine.Profiling;
+#if UNITY_EDITOR
+using UnityEditor;
+#endif
 
 public class BlendShapePerformanceMonitor : MonoBehaviour
 {
@@ -24,8 +27,11 @@ public class BlendShapePerformanceMonitor : MonoBehaviour
             fps = frameCount / timer;
             memoryUsage = Profiler.GetTotalAllocatedMemoryLong() / 1048576; // Convert to MB
             
-            Debug.Log($"[Performance] FPS: {fps:F1} | Memory: {memoryUsage}MB | " +
-                     $"Draw Calls: {UnityStats.drawCalls} | Vertices: {UnityStats.vertices}");
+            string message = $"[Performance] FPS: {fps:F1} | Memory: {memoryUsage}MB";
+#if UNITY_EDITOR
+            message += $" | Draw Calls: {UnityStats.drawCalls} | Vertices: {UnityStats.vertices}";
+#endif
+            Debug.Log(message);
             
             timer = 0f;
             frameCount = 0;
```

</details>

### `b07ffc385` — Implement blend shape texture sampling and improve validator

_Garrett Milliron, 2025-08-07 17:42:39 -0400_

```text
 Assets/BlendShapes/BlendShapeAnimation.shader |  8 ++++++--
 Assets/BlendShapes/BlendShapeDebugger.cs      | 62 +++++++++++++++++++++++++++------------------------------
 Assets/BlendShapes/BlendShapeValidator.cs     | 14 ++++++++++++-
 Docs/BlendShapeSetupChecklist.md              |  2 +-
 4 files changed, 49 insertions(+), 37 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 152 lines)</summary>

```diff
diff --git a/Assets/BlendShapes/BlendShapeAnimation.shader b/Assets/BlendShapes/BlendShapeAnimation.shader
index 5324976db..ef6c12e67 100644
--- a/Assets/BlendShapes/BlendShapeAnimation.shader
+++ b/Assets/BlendShapes/BlendShapeAnimation.shader
@@ -22,16 +22,20 @@ Shader "Custom/BlendShapeAnimation"
             #include "UnityCG.cginc"
 
             sampler2D _BlendShapeData;
+            float4 _BlendShapeData_TexelSize;
             float _AnimationDuration;
             float _UseManualWeights;
             float4 _BlendWeights;
             float _PauseAtProgress;
             float _DebugMode;
 
-            // Sample blend shape offset placeholder
+            // Sample vertex or normal delta from the blend shape texture
             float3 SampleBlendShapeOffset(uint vertexID, uint shapeIndex, bool sampleNormal)
             {
-                return float3(0,0,0);
+                float u = (vertexID + 0.5) * _BlendShapeData_TexelSize.x;
+                float row = shapeIndex * 2 + (sampleNormal ? 1 : 0);
+                float v = (row + 0.5) * _BlendShapeData_TexelSize.y;
+                return tex2D(_BlendShapeData, float2(u, v)).xyz;
             }
 
             float4 GetBlendShapeWeightsEnhanced(float time)
diff --git a/Assets/BlendShapes/BlendShapeDebugger.cs b/Assets/BlendShapes/BlendShapeDebugger.cs
index addf6f1de..ba3e07293 100644
--- a/Assets/BlendShapes/BlendShapeDebugger.cs
+++ b/Assets/BlendShapes/BlendShapeDebugger.cs
@@ -22,53 +22,49 @@ public class BlendShapeDebugger : MonoBehaviour
     public bool showBlendWeights = true;
     
     private MaterialPropertyBlock propertyBlock;
+    private MeshRenderer meshRenderer;
     private string currentPhase = "";
     private Vector4 currentWeights;
-    
+
     void OnEnable()
     {
         propertyBlock = new MaterialPropertyBlock();
-    }
-    
-    void Update()
-    {
-        if (debugMaterial == null) return;
-        
-        if (overrideAnimation)
-        {
-            ApplyManualWeights();
-        }
-        else if (pauseAnimation)
+        meshRenderer = GetComponent<MeshRenderer>();
+        if (meshRenderer != null && debugMaterial != null)
         {
-            ApplyPausedAnimation();
+            meshRenderer.sharedMaterial = debugMaterial;
         }
-        
-        UpdateDebugDisplay();
     }
-    
-    private void ApplyManualWeights()
+
+    void Update()
     {
-        // Create a custom property block for manual control
-        var renderer = GetComponent<MeshRenderer>();
-        if (renderer != null)
+        if (meshRenderer == null) return;
+
+        propertyBlock.Clear();
+
+        if (overrideAnimation)
         {
-            propertyBlock.SetVector("_BlendWeights", 
-                new Vector4(manualShape1, manualShape2, manualShape3, manualShape4));
+            currentWeights = new Vector4(manualShape1, manualShape2, manualShape3, manualShape4);
+            currentPhase = "Manual Override";
+            propertyBlock.SetVector("_BlendWeights", currentWeights);
             propertyBlock.SetFloat("_UseManualWeights", 1f);
-            renderer.SetPropertyBlock(propertyBlock);
         }
-    }
-    
-    private void ApplyPausedAnimation()
-    {
-        var renderer = GetComponent<MeshRenderer>();
-        if (renderer != null)
+        else
         {
-            // Calculate what the weights would be at this progress
-            CalculateWeightsAtProgress(animationProgress);
-            propertyBlock.SetFloat("_PauseAtProgress", animationProgress);
-            renderer.SetPropertyBlock(propertyBlock);
+            propertyBlock.SetFloat("_UseManualWeights", 0f);
+            if (pauseAnimation)
+            {
+                propertyBlock.SetFloat("_PauseAtProgress", animationProgress);
+                CalculateWeightsAtProgress(animationProgress);
+            }
+            else
+            {
+                propertyBlock.SetFloat("_PauseAtProgress", 0f);
+            }
         }
+
+        meshRenderer.SetPropertyBlock(propertyBlock);
+        UpdateDebugDisplay();
     }
     
     private Vector4 CalculateWeightsAtProgress(float progress)
diff --git a/Assets/BlendShapes/BlendShapeValidator.cs b/Assets/BlendShapes/BlendShapeValidator.cs
index b6f8fee10..3562bb1cf 100644
--- a/Assets/BlendShapes/BlendShapeValidator.cs
+++ b/Assets/BlendShapes/BlendShapeValidator.cs
@@ -40,7 +40,19 @@ public class BlendShapeValidator : MonoBehaviour
         
         Mesh mesh = targetRenderer.sharedMesh;
         detectedShapes.Clear();
-        
+
+        // Verify normals and tangents
+        bool hasNormals = mesh.normals != null && mesh.normals.Length == mesh.vertexCount;
+        bool hasTangents = mesh.tangents != null && mesh.tangents.Length == mesh.vertexCount;
+        if (!hasNormals)
+        {
+            Debug.LogWarning("Mesh is missing normals or they do not match the vertex count.");
+        }
+        if (!hasTangents)
+        {
+            Debug.LogWarning("Mesh is missing tangents or they do not match the vertex count.");
+        }
+
         Debug.Log($"Found {mesh.blendShapeCount} blend shapes in {mesh.name}:");
         
         for (int i = 0; i < mesh.blendShapeCount; i++)
diff --git a/Docs/BlendShapeSetupChecklist.md b/Docs/BlendShapeSetupChecklist.md
index 10ca77370..e61fd59d3 100644
--- a/Docs/BlendShapeSetupChecklist.md
+++ b/Docs/BlendShapeSetupChecklist.md
@@ -3,7 +3,7 @@
 ### Pre-Setup Validation
 - [ ] Run BlendShapeValidator to confirm all 4 blend shapes exist
 - [ ] Check that blend shape names match expected patterns
-- [ ] Verify mesh has proper normals and tangents
+- [ ] Ensure mesh has valid normals and tangents (validator logs warnings if missing)
 
```

</details>
