# Branch archive: `claude/benchmark-mobile-performance-SYydw`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-09 by Claude
- **Unmerged commits:** 3
- **Forked from:** `600dc09a9` (2026-03-09, Merge branch 'development' into claude/fix-joust-end-screen-ghd7s)
- **Tip:** `cda7abda8`
- **Files touched (7):**
  - `Assets/_Graphics/Materials/Shaders/HyperSeaSkybox.shader`
  - `Assets/_Scripts/Game/Environment/CapsuleMembrane.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/ShapeDrawingManager.cs`
  - `Assets/_Scripts/Game/UI/GameEventFeed/GameEventFeed.cs`
  - `Assets/_Scripts/Utility/MobilePerformanceManager.cs`
  - `Assets/_Scripts/VesselHUD/ScorePopup/ScorePopup.cs`
  - `Assets/_Scripts/VesselHUD/View/SquirrelVesselHUDView.cs`

### `4e450134d` — Optimize mobile performance across 7 hotspots identified in master→development analysis

_Claude, 2026-03-09 03:06:12 +0000_

```text
- CapsuleMembrane: lower subdivision on mobile (162→42 capsules), amortize
  Perlin noise updates to every 3rd frame (saves ~200 noise samples/frame)
- HyperSeaSkybox: add mobile SubShader (LOD 100) that skips nebulae (3x fbm4),
  dust lanes (fbm3), Voronoi cells (27-tap), and Andromeda spiral arms
- MobilePerformanceManager: set Shader.globalMaximumLOD=150 on mobile so the
  reduced skybox SubShader is auto-selected
- SquirrelVesselHUDView: only assign Image.color when Color32 actually changes,
  preventing per-frame Canvas rebuild during boost
- ScorePopup: only assign CanvasGroup.alpha when quantized value changes
- GameEventFeed: replace LINQ FirstOrDefault with Dictionary lookup for domain
  colors, remove System.Linq import
- ShapeDrawingManager: destroy runtime-created LineRenderer materials in
  OnDestroy to prevent material leaks
```

```text
 Assets/_Graphics/Materials/Shaders/HyperSeaSkybox.shader              | 73 ++++++++++++++++++++++++++++++++-
 Assets/_Scripts/Game/Environment/CapsuleMembrane.cs                   | 37 ++++++++++++++---
 .../_Scripts/Game/Environment/MiniGameObjects/ShapeDrawingManager.cs  |  9 ++++
 Assets/_Scripts/Game/UI/GameEventFeed/GameEventFeed.cs                | 16 ++++++--
 Assets/_Scripts/Utility/MobilePerformanceManager.cs                   | 12 +++++-
 Assets/_Scripts/VesselHUD/ScorePopup/ScorePopup.cs                    |  9 +++-
 Assets/_Scripts/VesselHUD/View/SquirrelVesselHUDView.cs               |  7 +++-
 7 files changed, 149 insertions(+), 14 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 323 lines)</summary>

```diff
diff --git a/Assets/_Graphics/Materials/Shaders/HyperSeaSkybox.shader b/Assets/_Graphics/Materials/Shaders/HyperSeaSkybox.shader
index a6d7b2dd7..f85da6804 100644
--- a/Assets/_Graphics/Materials/Shaders/HyperSeaSkybox.shader
+++ b/Assets/_Graphics/Materials/Shaders/HyperSeaSkybox.shader
@@ -680,7 +680,7 @@ Shader "CosmicShore/HyperSeaSkybox"
     }
 
     // ================================================================
-    // SHARED FRAGMENT
+    // SHARED FRAGMENT (full quality)
     // ================================================================
 
     half4 hyperSeaFrag(float3 viewDir)
@@ -718,10 +718,77 @@ Shader "CosmicShore/HyperSeaSkybox"
         return half4(color, 1.0);
     }
 
+    // ================================================================
+    // MOBILE FRAGMENT (reduced quality)
+    // Skips: nebulae (3x fbm4), dust lanes (fbm3), Voronoi cells (27-tap),
+    // Andromeda (spiral arms). Keeps: atmosphere, galactic plane, stars, core.
+    // ================================================================
+
+    half4 hyperSeaFragMobile(float3 viewDir)
+    {
+        float3 dir = normalize(viewDir);
+        float time = _Time.y;
+
+        half3 color = computeAmbient(dir, time);
+        color += computeGalacticPlane(dir, time);
+        color += computeStars(dir, time);
+        color += computeCore(dir);
+        color += computeAtmosphereBridge(dir);
+
+        return half4(color, 1.0);
+    }
+
     ENDCG
 
     // ================================================================
-    // SUBSHADER 1: Unity Skybox (RenderSettings.skybox)
+    // SUBSHADER 1: Mobile (reduced quality)
+    // Selected first on mobile GPUs (OpenGL ES 3.0 / Vulkan mobile).
+    // Drops nebulae, dust, Voronoi, Andromeda to save ALU.
+    // ================================================================
+
+    SubShader
+    {
+        Tags
+        {
+            "Queue"="Background"
+            "RenderType"="Background"
+            "PreviewType"="Skybox"
+            "RenderPipeline"="UniversalPipeline"
+        }
+
+        // LOD 100 ensures desktop (LOD 200+) prefers the full SubShader below
+        LOD 100
+
+        Cull Off
+        ZWrite Off
+
+        Pass
+        {
+            CGPROGRAM
+            #pragma vertex vert
+            #pragma fragment frag
+            #pragma target 3.0
+            #pragma only_renderers gles3 vulkan
+
+            v2f vert(appdata v)
+            {
+                v2f o;
+                o.pos = UnityObjectToClipPos(v.vertex);
+                o.viewDir = v.vertex.xyz;
+                return o;
+            }
+
+            half4 frag(v2f i) : SV_Target
+            {
+                return hyperSeaFragMobile(i.viewDir);
+            }
+
+            ENDCG
+        }
+    }
+
+    // ================================================================
+    // SUBSHADER 2: Desktop / Full Quality
     // Used when assigned as a skybox material in Lighting settings.
     // Vertex positions ARE the view directions.
     // ================================================================
@@ -736,6 +803,8 @@ Shader "CosmicShore/HyperSeaSkybox"
             "RenderPipeline"="UniversalPipeline"
         }
 
+        LOD 200
+
         Cull Off
         ZWrite Off
 
diff --git a/Assets/_Scripts/Game/Environment/CapsuleMembrane.cs b/Assets/_Scripts/Game/Environment/CapsuleMembrane.cs
index 44cc9730d..e0f066250 100644
--- a/Assets/_Scripts/Game/Environment/CapsuleMembrane.cs
+++ b/Assets/_Scripts/Game/Environment/CapsuleMembrane.cs
@@ -1,6 +1,7 @@
 using System.Collections.Generic;
 using UnityEngine;
 using UnityEngine.Rendering;
+using CosmicShore.Utility;
 
 namespace CosmicShore.Game
 {
@@ -21,6 +22,10 @@ namespace CosmicShore.Game
         [Range(0, 4)]
         [SerializeField] int subdivisions = 2;
 
+        [Tooltip("Override subdivision level on mobile (lower = fewer capsules). -1 uses default.")]
+        [Range(-1, 3)]
+        [SerializeField] int mobileSubdivisionOverride = 1;
+
         [Tooltip("Radius of the membrane sphere.")]
         [SerializeField] float radius = 500f;
 
@@ -59,6 +64,10 @@ namespace CosmicShore.Game
         [Tooltip("Rendering layer mask for the instanced draw.")]
         [SerializeField] uint renderingLayerMask = 1;
 
+        [Header("Mobile Performance")]
+        [Tooltip("How many frames between full noise updates on mobile. 1 = every frame, 3 = every 3rd frame.")]
+        [SerializeField] int mobileUpdateInterval = 3;
+
         Matrix4x4[] matrices;
         RenderParams renderParams;
         Mesh meshToRender;
@@ -70,13 +79,22 @@ namespace CosmicShore.Game
         // Noise sampling coordinates (one per capsule, derived from jittered position)
         Vector3[] noiseCoords;
 
+        bool isMobile;
+        int updateCounter;
+
         void Awake()
         {
+            isMobile = MobilePerformanceManager.IsMobile;
+
+            int effectiveSubdivisions = subdivisions;
+            if (isMobile && mobileSubdivisionOverride >= 0)
+                effectiveSubdivisions = mobileSubdivisionOverride;
+
```

</details>

### `bfecbb885` — Add Metal renderer to mobile skybox SubShader for iOS compatibility

_Claude, 2026-03-09 03:36:00 +0000_

```text
The #pragma only_renderers directive was missing Metal, which is the
primary renderer on iOS. Without it, iOS builds would skip the mobile
SubShader and fall through to the full-quality desktop version, negating
the mobile performance optimization.
```

```text
 Assets/_Graphics/Materials/Shaders/HyperSeaSkybox.shader | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Graphics/Materials/Shaders/HyperSeaSkybox.shader b/Assets/_Graphics/Materials/Shaders/HyperSeaSkybox.shader
index f85da6804..fa456155e 100644
--- a/Assets/_Graphics/Materials/Shaders/HyperSeaSkybox.shader
+++ b/Assets/_Graphics/Materials/Shaders/HyperSeaSkybox.shader
@@ -768,7 +768,7 @@ Shader "CosmicShore/HyperSeaSkybox"
             #pragma vertex vert
             #pragma fragment frag
             #pragma target 3.0
-            #pragma only_renderers gles3 vulkan
+            #pragma only_renderers gles3 vulkan metal
 
             v2f vert(appdata v)
             {
```

</details>

### `cda7abda8` — Fix CS0019: use Equals() for Color32 comparison instead of != operator

_Claude, 2026-03-09 03:54:40 +0000_

```text
Color32 is a struct without an overloaded != operator in Unity.
```

```text
 Assets/_Scripts/VesselHUD/View/SquirrelVesselHUDView.cs | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/VesselHUD/View/SquirrelVesselHUDView.cs b/Assets/_Scripts/VesselHUD/View/SquirrelVesselHUDView.cs
index f475450ab..a4c5021d0 100644
--- a/Assets/_Scripts/VesselHUD/View/SquirrelVesselHUDView.cs
+++ b/Assets/_Scripts/VesselHUD/View/SquirrelVesselHUDView.cs
@@ -145,7 +145,7 @@ namespace CosmicShore.Game
 
             // Only assign color (which dirties the Canvas) when the value actually changed.
             // Color32 comparison avoids dirtying on sub-pixel color differences.
-            if ((Color32)_currentBoostColor != (Color32)previousColor)
+            if (!((Color32)_currentBoostColor).Equals((Color32)previousColor))
                 boostFill.color = _currentBoostColor;
         }
 
```

</details>
