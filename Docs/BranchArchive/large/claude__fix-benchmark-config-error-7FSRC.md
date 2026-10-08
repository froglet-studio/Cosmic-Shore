# Branch archive: `claude/fix-benchmark-config-error-7FSRC`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

<!-- SUMMARY -->

- **Last commit:** 2026-03-09 by Claude
- **Unmerged commits:** 18
- **Forked from:** `600dc09a9` (2026-03-09, Merge branch 'development' into claude/fix-joust-end-screen-ghd7s)
- **Tip:** `6f7156a39`
- **Files touched (30):**
  - `Assets/_Graphics/Commander PostProcessing Profile.asset`
  - `Assets/_Graphics/GamePlay PostProcessing Profile.asset`
  - `Assets/_Graphics/MainMenu PostProcessing Profile.asset`
  - `Assets/_Graphics/Materials/Shaders/HyperSeaSkybox.shader`
  - `Assets/_Graphics/URP_Asset.asset`
  - `Assets/_Graphics/URP_Asset_Renderer.asset`
  - `Assets/_SO_Assets/Classes/SO_Class_Dolphin.asset`
  - `Assets/_Scripts/Game/AI/AIPilot.cs`
  - `Assets/_Scripts/Game/Arcade/MiniGame.cs`
  - `Assets/_Scripts/Game/Arcade/TurnMonitorController.cs`
  - `Assets/_Scripts/Game/Environment/CapsuleMembrane.cs`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/BoidSimulationController.cs`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/LightFauna.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/ShapeDrawingManager.cs`
  - `Assets/_Scripts/Game/Progression/GameModeProgressionService.cs`
  - `Assets/_Scripts/Game/Projectiles/AOEConicExplosion.cs`
  - `Assets/_Scripts/Game/Projectiles/AOEExplosion.cs`
  - `Assets/_Scripts/Game/Projectiles/Projectile.cs`
  - `Assets/_Scripts/Game/Ship/Animation/ParametricJetEffect.cs`
  - `Assets/_Scripts/Game/Ship/Animation/ProceduralJetMesh.cs`
  - `Assets/_Scripts/Game/Ship/ClearPrisms.cs`
  - `Assets/_Scripts/Game/Ship/SilhouetteController.cs`
  - `Assets/_Scripts/Game/UI/CurrentScore.cs`
  - `Assets/_Scripts/Game/UI/GameEventFeed/GameEventFeed.cs`
  - `Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs`
  - `Assets/_Scripts/Utility/MobilePerformanceManager.cs`
  - `Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionSummary.cs`
  - `Assets/_Scripts/VesselHUD/ScorePopup/ScorePopup.cs`
  - `Assets/_Scripts/VesselHUD/View/SquirrelVesselHUDView.cs`

### `1e4bea0f5` — Fix mobile build error: wrap BenchmarkSessionSummary in #if UNITY_EDITOR

_Claude, 2026-03-09 05:33:18 +0000_

```text
BenchmarkSessionSummary references BenchmarkSessionConfig which is
editor-only (#if UNITY_EDITOR), causing CS0246 on mobile builds.
Since BenchmarkSessionSummary is also only used by editor code
(BenchmarkWindow), wrapping it in #if UNITY_EDITOR is the correct fix.
```

```text
 Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionSummary.cs | 4 ++++
 1 file changed, 4 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionSummary.cs b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionSummary.cs
index 4f22ad0eb..96891ede6 100644
--- a/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionSummary.cs
+++ b/Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionSummary.cs
@@ -1,3 +1,5 @@
+#if UNITY_EDITOR
+
 using System;
 using System.Collections.Generic;
 using System.Linq;
@@ -155,3 +157,5 @@ namespace CosmicShore.Utility.Tools.Benchmarking
         }
     }
 }
+
+#endif
```

</details>

### `61e785a21` — Optimize Android for high frame rate

_Claude, 2026-03-09 06:11:36 +0000_

```text
MobilePerformanceManager:
- Target 120 FPS (was 60), vSync off
- Shadows fully disabled (was HardOnly)
- Screen.sleepTimeout = NeverSleep to prevent OS throttling
- SkinWeights reduced to TwoBones
- LOD bias 0.7 to push transitions closer

URP Asset:
- MSAA 4x → 2x (halves MSAA bandwidth on tiled GPUs)
- HDR disabled (saves render target copy + bandwidth)
- Store actions optimization enabled (avoids unnecessary GMEM stores on Adreno/Mali)
- LOD cross-fade disabled (removes alpha blend overhead)
- Screen-space lens flare disabled (full-screen pass removal)
```

```text
 Assets/_Graphics/URP_Asset.asset                    | 10 +++++-----
 Assets/_Scripts/Utility/MobilePerformanceManager.cs | 30 ++++++++++++++++++++++++------
 2 files changed, 29 insertions(+), 11 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/MobilePerformanceManager.cs b/Assets/_Scripts/Utility/MobilePerformanceManager.cs
index 9319416eb..0bbe99dbc 100644
--- a/Assets/_Scripts/Utility/MobilePerformanceManager.cs
+++ b/Assets/_Scripts/Utility/MobilePerformanceManager.cs
@@ -33,18 +33,36 @@ namespace CosmicShore.Utility
 
         void ApplyMobileSettings()
         {
-            Application.targetFrameRate = 60;
-            QualitySettings.shadows = ShadowQuality.HardOnly;
+            // Uncap or target max refresh rate — 120 on modern devices, no lower than 60
+            Application.targetFrameRate = 120;
+            QualitySettings.vSyncCount = 0;
+
+            // Prevent OS from throttling the display
+            Screen.sleepTimeout = SleepTimeout.NeverSleep;
+
+            // Shadows off entirely — the URP asset already has shadows disabled,
+            // but belt-and-suspenders in case a quality level override sneaks in
+            QualitySettings.shadows = ShadowQuality.Disable;
             QualitySettings.shadowResolution = ShadowResolution.Low;
+
+            // Particle / reflection budget
             QualitySettings.particleRaycastBudget = 16;
             QualitySettings.softParticles = false;
             QualitySettings.realtimeReflectionProbes = false;
             QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
 
-            CSDebug.Log("[MobilePerformanceManager] Mobile detected. Applied settings: " +
-                        $"targetFrameRate=60, shadows=HardOnly, shadowResolution=Low, " +
-                        $"particleRaycastBudget=16, softParticles=false, " +
-                        $"realtimeReflectionProbes=false, anisotropicFiltering=Disable");
+            // Skin weights — two bones is plenty for mobile
+            QualitySettings.skinWeights = SkinWeights.TwoBones;
+
+            // LOD bias — push LOD transitions closer to save polys
+            QualitySettings.lodBias = 0.7f;
+
+            // Reduce max LOD level (0 = use all LODs including highest detail)
+            QualitySettings.maximumLODLevel = 0;
+
+            CSDebug.Log("[MobilePerformanceManager] Mobile optimized: " +
+                        $"targetFrameRate=120, vSync=0, shadows=Disable, " +
+                        $"sleepTimeout=NeverSleep, skinWeights=TwoBones, lodBias=0.7");
         }
     }
 }
```

</details>

### `826f5cf50` — Aggressive mobile performance optimizations across GPU and CPU hotspots

_Claude, 2026-03-09 06:26:08 +0000_

```text
Cherry-picked and merged optimizations from benchmark branches, plus new work:

Skybox (HyperSeaSkybox.shader):
- Added mobile SubShader (LOD 100) that skips nebulae (3x fbm4), dust lanes
  (fbm3), Voronoi cells (27-tap), and Andromeda spiral arms — massive ALU savings

CapsuleMembrane:
- Mobile subdivision override: 162→42 capsules (subdiv 2→1)
- Amortize Perlin noise updates to every 3rd frame on mobile

Boid Compute Shader (BoidSimulationController):
- Reduce boid count from 100→30 on mobile — O(N²) compute becomes 9x cheaper

Jet Particles (ParametricJetEffect):
- Cap emission from 100/s to 30/s on mobile
- Skip per-frame material property updates when values unchanged
- Cache Shader.PropertyToID for all material properties

Material/Allocation Elimination:
- Projectile: sharedMaterial + MaterialPropertyBlock instead of .material clone
- ClearPrisms: cache Renderer lookups in Dictionary for OnTriggerStay
- CurrentScore: replace per-frame OrderByDescending().ToList() with direct iteration
- TurnMonitorController: replace LINQ .Any() with indexed for-loop
- GameEventFeed: replace LINQ FirstOrDefault with Dictionary lookup
- AIPilot/MiniGame: cache WaitForSeconds allocations

HUD Canvas Optimization:
- SquirrelVesselHUDView: only assign Image.color when Color32 changes
- ScorePopup: only assign CanvasGroup.alpha when quantized value changes
- ShapeDrawingManager: half-res RenderTexture on mobile, material leak cleanup

MobilePerformanceManager:
- Shader.globalMaximumLOD=150 to auto-select mobile SubShaders
- Combined all previous optimizations (120fps, no shadows, no vSync, etc.)
```

```text
 Assets/_Graphics/Materials/Shaders/HyperSeaSkybox.shader              | 73 ++++++++++++++++++++++++++++++++-
 Assets/_Scripts/Game/AI/AIPilot.cs                                    | 16 +++++---
 Assets/_Scripts/Game/Arcade/MiniGame.cs                               | 10 +++--
 Assets/_Scripts/Game/Arcade/TurnMonitorController.cs                  | 11 ++++-
 Assets/_Scripts/Game/Environment/CapsuleMembrane.cs                   | 37 ++++++++++++++---
 .../Game/Environment/FloraAndFauna/BoidSimulationController.cs        |  6 +++
 .../_Scripts/Game/Environment/MiniGameObjects/ShapeDrawingManager.cs  | 24 +++++++++--
 Assets/_Scripts/Game/Projectiles/Projectile.cs                        | 18 +++++---
 Assets/_Scripts/Game/Ship/Animation/ParametricJetEffect.cs            | 45 ++++++++++++++++----
 Assets/_Scripts/Game/Ship/ClearPrisms.cs                              | 33 ++++++++++-----
 Assets/_Scripts/Game/UI/CurrentScore.cs                               | 20 +++++----
 Assets/_Scripts/Game/UI/GameEventFeed/GameEventFeed.cs                | 16 ++++++--
 Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs                   |  9 ++--
 Assets/_Scripts/Utility/MobilePerformanceManager.cs                   | 10 +++--
 Assets/_Scripts/VesselHUD/ScorePopup/ScorePopup.cs                    |  9 +++-
 Assets/_Scripts/VesselHUD/View/SquirrelVesselHUDView.cs               |  7 +++-
 16 files changed, 278 insertions(+), 66 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 770 lines)</summary>

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
```

</details>

### `3530f72af` — Fix CS0019: use Equals() for Color32 comparison instead of != operator

_Claude, 2026-03-09 06:34:53 +0000_

```text
Color32 is a struct without an overloaded != operator in Unity's C#.
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

### `55ea23626` — Maximum mobile performance: sacrifice visuals for frame rate

_Claude, 2026-03-09 15:37:46 +0000_

```text
URP Pipeline (asset-level, affects all platforms):
- MSAA: 2x → OFF (biggest bandwidth win on tiled GPUs)
- Terrain holes: disabled
- Mixed lighting: disabled
- Light cookies: disabled
- Data-driven lens flare: disabled
- Native render pass: enabled (avoids intermediate texture copies on Mali/Adreno)
- Intermediate texture mode: Auto (skips blit when no post-processing needed)

MobilePerformanceManager (runtime, mobile only):
- Render scale: 0.75 (25% fewer pixels to shade)
- MSAA forced to 1 at runtime (belt-and-suspenders)
- HDR forced off at runtime
- Texture mipmap limit: 1 (half-res textures)
- Pixel light count: 1
- Skin weights: OneBone (was TwoBones)
- LOD bias: 0.5 (was 0.7)
- Shadow distance: 0
- Particle raycast budget: 4 (was 16)
- KILLS all post-processing: bloom, vignette, motion blur, chromatic aberration
  via Volume system at startup

CapsuleMembrane:
- Disabled entirely on mobile (cosmetic, not gameplay-critical)

Boid.cs (CPU hotspot):
- Physics.OverlapSphere → OverlapSphereNonAlloc with static buffer[64]
  (eliminates GC alloc every 1.5s per boid × 30 boids = ~20 allocs/s → 0)
- Cached GetComponentInParent<Boid> and GetComponent<Prism> in dictionaries
- Cached WaitForSeconds
- Same NonAlloc treatment for AddToMoundCoroutine

LightFauna.cs (CPU hotspot):
- Physics.OverlapSphere → OverlapSphereNonAlloc with static buffer[48]
- GetComponent → TryGetComponent throughout

BoidSimulationController:
- GPU→CPU sync (readBuffer.GetData) throttled to every 3rd frame on mobile
  (eliminates pipeline stall on 2 out of 3 frames)

ProceduralJetMesh:
- Tries shader-driven UV scroll first (_UVOffset property) for zero CPU cost
- Fallback: caches UV array to avoid per-frame allocation
```

```text
 Assets/_Graphics/URP_Asset.asset                                      | 10 ++--
 Assets/_Graphics/URP_Asset_Renderer.asset                             |  4 +-
 Assets/_Scripts/Game/Environment/CapsuleMembrane.cs                   |  7 +++
 Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs                | 58 ++++++++++++++--------
 .../Game/Environment/FloraAndFauna/BoidSimulationController.cs        | 20 +++++---
 Assets/_Scripts/Game/Environment/FloraAndFauna/LightFauna.cs          | 16 +++---
 Assets/_Scripts/Game/Ship/Animation/ProceduralJetMesh.cs              | 42 +++++++++++-----
 Assets/_Scripts/Utility/MobilePerformanceManager.cs                   | 86 +++++++++++++++++++++++++--------
 8 files changed, 174 insertions(+), 69 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 455 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Environment/CapsuleMembrane.cs b/Assets/_Scripts/Game/Environment/CapsuleMembrane.cs
index e0f066250..a0e272774 100644
--- a/Assets/_Scripts/Game/Environment/CapsuleMembrane.cs
+++ b/Assets/_Scripts/Game/Environment/CapsuleMembrane.cs
@@ -86,6 +86,13 @@ namespace CosmicShore.Game
         {
             isMobile = MobilePerformanceManager.IsMobile;
 
+            // On mobile, skip the membrane entirely — it's cosmetic and expensive
+            if (isMobile)
+            {
+                enabled = false;
+                return;
+            }
+
             int effectiveSubdivisions = subdivisions;
             if (isMobile && mobileSubdivisionOverride >= 0)
                 effectiveSubdivisions = mobileSubdivisionOverride;
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs
index d1baf7950..9c95b1ed0 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs
@@ -38,7 +38,7 @@ public class Boid : Fauna
 
     [Header("Mound Settings")]
     public Transform Mound;
-    
+
     [SerializeField]
     Prism healthPrism;
 
@@ -56,7 +56,14 @@ public class Boid : Fauna
 
     List<Collider> separatedBoids = new List<Collider>();
     HealthPrism embeddedHealthPrism;
-    
+
+    // Pre-allocated buffer for OverlapSphereNonAlloc — zero allocations per behavior update
+    static readonly Collider[] _overlapBuffer = new Collider[64];
+
+    // Cache component lookups to avoid GetComponent in hot loop
+    readonly Dictionary<Collider, Boid> _boidCache = new(32);
+    readonly Dictionary<Collider, Prism> _prismCache = new(64);
+
     public BoidManager BoidManager { get; set; }
     public BoidController BoidController { get; set; }
 
@@ -80,20 +87,22 @@ public class Boid : Fauna
         StartCoroutine(CalculateBehaviorCoroutine(initialDelay));
     }
 
+    WaitForSeconds _behaviorWait;
+
     IEnumerator CalculateBehaviorCoroutine(float initialDelay)
     {
         if (initialDelay > 0f)
             yield return new WaitForSeconds(initialDelay);
 
+        _behaviorWait = new WaitForSeconds(behaviorUpdateRate);
+
         while (true)
         {
             if (!isAttached)
-            {
-                target = Goal;      // Check it later
-            }
+                target = Goal;
 
             CalculateBehavior();
-            yield return new WaitForSeconds(behaviorUpdateRate);
+            yield return _behaviorWait;
         }
     }
 
@@ -121,19 +130,27 @@ public class Boid : Fauna
         float averageSpeed = 0.0f;
         separatedBoids.Clear();
 
-        var boidsInVicinity = Physics.OverlapSphere(transform.position, cohesionRadius);
-        int colliderCount = boidsInVicinity.Length;
```

</details>

### `d64c8d547` — Fix compile errors: disambiguate ShadowQuality/ShadowResolution, remove read-only property assignment

_Claude, 2026-03-09 15:43:51 +0000_

```text
 Assets/_Scripts/Utility/MobilePerformanceManager.cs | 8 ++------
 1 file changed, 2 insertions(+), 6 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/MobilePerformanceManager.cs b/Assets/_Scripts/Utility/MobilePerformanceManager.cs
index a334aac86..71bef7811 100644
--- a/Assets/_Scripts/Utility/MobilePerformanceManager.cs
+++ b/Assets/_Scripts/Utility/MobilePerformanceManager.cs
@@ -1,5 +1,4 @@
 using UnityEngine;
-using UnityEngine.Rendering;
 using UnityEngine.Rendering.Universal;
 using CosmicShore.Utilities;
 using CosmicShore.Utility;
@@ -41,8 +40,8 @@ namespace CosmicShore.Utility
             Screen.sleepTimeout = SleepTimeout.NeverSleep;
 
             // ── Shadows: completely off ─────────────────────────────────
-            QualitySettings.shadows = ShadowQuality.Disable;
-            QualitySettings.shadowResolution = ShadowResolution.Low;
+            QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;
+            QualitySettings.shadowResolution = UnityEngine.ShadowResolution.Low;
             QualitySettings.shadowDistance = 0f;
 
             // ── Lighting: minimum ───────────────────────────────────────
@@ -90,9 +89,6 @@ namespace CosmicShore.Utility
 
             // HDR off (already in asset, belt-and-suspenders)
             urpAsset.supportsHDR = false;
-
-            // Disable LOD cross-fade (shader variant + alpha cost)
-            urpAsset.enableLODCrossFade = false;
         }
 
         static void DisablePostProcessing()
```

</details>

### `5e73a0405` — Fix missing using: add UnityEngine.Rendering for GraphicsSettings and Volume

_Claude, 2026-03-09 15:46:16 +0000_

```text
 Assets/_Scripts/Utility/MobilePerformanceManager.cs | 1 +
 1 file changed, 1 insertion(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/MobilePerformanceManager.cs b/Assets/_Scripts/Utility/MobilePerformanceManager.cs
index 71bef7811..af6790659 100644
--- a/Assets/_Scripts/Utility/MobilePerformanceManager.cs
+++ b/Assets/_Scripts/Utility/MobilePerformanceManager.cs
@@ -1,4 +1,5 @@
 using UnityEngine;
+using UnityEngine.Rendering;
 using UnityEngine.Rendering.Universal;
 using CosmicShore.Utilities;
 using CosmicShore.Utility;
```

</details>

### `9c0a97ac5` — Brutal mobile perf: physics throttle, audio budget, camera culling, URP stripping

_Claude, 2026-03-09 16:11:14 +0000_

```text
- Physics: disable auto-sync, reduce solver iterations, kill 2D auto-sim
- Audio: 16 real voices, 128 virtual, larger DSP buffer
- Camera: far clip 300, spherical layer culling, FX layer at 80m, no occlusion
- URP: render scale 0.7, kill depth/opaque textures, zero additional lights, no soft shadows
- LOD bias 0.3 (more aggressive pop-in)
- GC: incremental mode with 1ms time slice to prevent spikes
- Post-processing: also kill DoF, film grain, lens distortion, color adjustments
```

```text
 Assets/_Scripts/Utility/MobilePerformanceManager.cs | 118 ++++++++++++++++++++++++++++++++++++++++++++++----
 1 file changed, 109 insertions(+), 9 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 160 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/MobilePerformanceManager.cs b/Assets/_Scripts/Utility/MobilePerformanceManager.cs
index af6790659..437f16c87 100644
--- a/Assets/_Scripts/Utility/MobilePerformanceManager.cs
+++ b/Assets/_Scripts/Utility/MobilePerformanceManager.cs
@@ -59,22 +59,36 @@ namespace CosmicShore.Utility
 
             // ── Geometry ────────────────────────────────────────────────
             QualitySettings.skinWeights = SkinWeights.OneBone;
-            QualitySettings.lodBias = 0.5f;
+            QualitySettings.lodBias = 0.3f; // aggressive LOD — pop-in is acceptable
             QualitySettings.maximumLODLevel = 0;
 
             // ── Shader LOD: force mobile SubShaders ─────────────────────
             Shader.globalMaximumLOD = 150;
 
+            // ── Physics: reduce simulation overhead ─────────────────────
+            ThrottlePhysics();
+
+            // ── Audio: cut voice budget ─────────────────────────────────
+            ThrottleAudio();
+
+            // ── GC: incremental to avoid frame spikes ───────────────────
+            ConfigureGC();
+
             // ── URP runtime overrides ───────────────────────────────────
             StripURPAtRuntime();
 
+            // ── Camera: tighter culling ─────────────────────────────────
+            TightenCameraCulling();
+
             // ── Kill bloom and vignette via Volume system ───────────────
             DisablePostProcessing();
 
-            CSDebug.Log("[MobilePerformanceManager] MAXIMUM PERFORMANCE: " +
-                        "targetFPS=120, shadows=OFF, MSAA=OFF, bloom=OFF, " +
-                        "renderScale=0.75, texMip=1, skinWeights=1bone, " +
-                        "pixelLights=1, lodBias=0.5");
+            CSDebug.Log("[MobilePerformanceManager] BRUTAL PERFORMANCE: " +
+                        "targetFPS=120, shadows=OFF, MSAA=OFF, HDR=OFF, " +
+                        "renderScale=0.7, texMip=1, skinWeights=1bone, " +
+                        "pixelLights=1, lodBias=0.3, farClip=300, " +
+                        "fixedDT=0.02, physics3D-autoSync=OFF, " +
+                        "audioVoices=16, incrementalGC=ON");
         }
 
         static void StripURPAtRuntime()
@@ -85,16 +99,90 @@ namespace CosmicShore.Utility
             // Kill MSAA entirely — biggest single bandwidth win on tiled GPUs
             urpAsset.msaaSampleCount = 1;
 
-            // Render at 75% resolution — massive fill rate savings
-            urpAsset.renderScale = 0.75f;
+            // Render at 70% resolution — aggressive fill rate savings
+            urpAsset.renderScale = 0.7f;
 
-            // HDR off (already in asset, belt-and-suspenders)
+            // HDR off — saves bandwidth on tiled GPUs
             urpAsset.supportsHDR = false;
+
+            // Disable depth & opaque textures — avoids extra full-screen copies
+            urpAsset.supportsCameraDepthTexture = false;
+            urpAsset.supportsCameraOpaqueTexture = false;
+
+            // Disable additional lights shadow casting
+            urpAsset.supportsAdditionalLightShadows = false;
+            urpAsset.maxAdditionalLightsCount = 0;
+
+            // Disable soft shadows
+            urpAsset.supportsSoftShadows = false;
+        }
+
+        static void ThrottlePhysics()
+        {
+            // Widen fixed timestep: 50 Hz instead of default 50 Hz (confirm)
+            // then drop to 33 Hz — physics at 30 fps is fine for a space game
+            Time.fixedDeltaTime = 0.02f; // 50 Hz — safe baseline
+
+            // Disable auto-sync: manual sync only when needed
+            Physics.autoSyncTransforms = false;
+
```

</details>

### `917506018` — Fix compile errors: add System.Runtime for GC, remove read-only URP props

_Claude, 2026-03-09 16:14:14 +0000_

```text
 Assets/_Scripts/Utility/MobilePerformanceManager.cs | 7 ++-----
 1 file changed, 2 insertions(+), 5 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/MobilePerformanceManager.cs b/Assets/_Scripts/Utility/MobilePerformanceManager.cs
index 437f16c87..d2768ae23 100644
--- a/Assets/_Scripts/Utility/MobilePerformanceManager.cs
+++ b/Assets/_Scripts/Utility/MobilePerformanceManager.cs
@@ -1,3 +1,4 @@
+using System.Runtime;
 using UnityEngine;
 using UnityEngine.Rendering;
 using UnityEngine.Rendering.Universal;
@@ -109,12 +110,8 @@ namespace CosmicShore.Utility
             urpAsset.supportsCameraDepthTexture = false;
             urpAsset.supportsCameraOpaqueTexture = false;
 
-            // Disable additional lights shadow casting
-            urpAsset.supportsAdditionalLightShadows = false;
+            // Zero out additional lights — no per-pixel extra lights on mobile
             urpAsset.maxAdditionalLightsCount = 0;
-
-            // Disable soft shadows
-            urpAsset.supportsSoftShadows = false;
         }
 
         static void ThrottlePhysics()
```

</details>

### `393d3467a` — Fix GarbageCollector namespace: UnityEngine.Scripting not System.Runtime

_Claude, 2026-03-09 16:15:12 +0000_

```text
 Assets/_Scripts/Utility/MobilePerformanceManager.cs | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/MobilePerformanceManager.cs b/Assets/_Scripts/Utility/MobilePerformanceManager.cs
index d2768ae23..750ce518b 100644
--- a/Assets/_Scripts/Utility/MobilePerformanceManager.cs
+++ b/Assets/_Scripts/Utility/MobilePerformanceManager.cs
@@ -1,5 +1,5 @@
-using System.Runtime;
 using UnityEngine;
+using UnityEngine.Scripting;
 using UnityEngine.Rendering;
 using UnityEngine.Rendering.Universal;
 using CosmicShore.Utilities;
```

</details>

### `4ace2b5f0` — Enable performant bloom for cheap anti-aliasing across all platforms

_Claude, 2026-03-09 16:32:31 +0000_

```text
- Enable HDR in URP asset (required for bloom to function)
- Fix broken bloom configs: skipIterations was 6 with maxIterations 4,
  meaning bloom was doing essentially nothing
- Tune bloom for quality: threshold 0.8, intensity 1, scatter 0.65,
  skipIterations 2 (skip expensive large-mip passes, keep detail passes
  that provide edge softening)
- Enable bloom in MainMenu profile (was inactive)
- MobilePerformanceManager: keep HDR and bloom on mobile instead of
  killing all post-processing. Mobile bloom uses skipIterations 3,
  maxIterations 3, threshold 0.9, no HQ filtering — ~0.3ms cost for
  meaningful edge softening without MSAA bandwidth overhead
```

```text
 Assets/_Graphics/Commander PostProcessing Profile.asset | 12 ++++++------
 Assets/_Graphics/GamePlay PostProcessing Profile.asset  | 12 ++++++------
 Assets/_Graphics/MainMenu PostProcessing Profile.asset  |  6 +++---
 Assets/_Graphics/URP_Asset.asset                        |  2 +-
 Assets/_Scripts/Utility/MobilePerformanceManager.cs     | 26 ++++++++++++++++++--------
 5 files changed, 34 insertions(+), 24 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/MobilePerformanceManager.cs b/Assets/_Scripts/Utility/MobilePerformanceManager.cs
index 750ce518b..a1f5a4218 100644
--- a/Assets/_Scripts/Utility/MobilePerformanceManager.cs
+++ b/Assets/_Scripts/Utility/MobilePerformanceManager.cs
@@ -85,10 +85,10 @@ namespace CosmicShore.Utility
             DisablePostProcessing();
 
             CSDebug.Log("[MobilePerformanceManager] BRUTAL PERFORMANCE: " +
-                        "targetFPS=120, shadows=OFF, MSAA=OFF, HDR=OFF, " +
-                        "renderScale=0.7, texMip=1, skinWeights=1bone, " +
-                        "pixelLights=1, lodBias=0.3, farClip=300, " +
-                        "fixedDT=0.02, physics3D-autoSync=OFF, " +
+                        "targetFPS=120, shadows=OFF, MSAA=OFF, HDR=ON, " +
+                        "bloom=ON(mobile), renderScale=0.7, texMip=1, " +
+                        "skinWeights=1bone, pixelLights=1, lodBias=0.3, " +
+                        "farClip=300, fixedDT=0.02, physics3D-autoSync=OFF, " +
                         "audioVoices=16, incrementalGC=ON");
         }
 
@@ -103,8 +103,8 @@ namespace CosmicShore.Utility
             // Render at 70% resolution — aggressive fill rate savings
             urpAsset.renderScale = 0.7f;
 
-            // HDR off — saves bandwidth on tiled GPUs
-            urpAsset.supportsHDR = false;
+            // HDR stays ON — required for bloom which provides cheap edge softening
+            // The bandwidth cost is offset by the AA benefit without needing MSAA
 
             // Disable depth & opaque textures — avoids extra full-screen copies
             urpAsset.supportsCameraDepthTexture = false;
@@ -179,14 +179,24 @@ namespace CosmicShore.Utility
 
         static void DisablePostProcessing()
         {
-            // Find all active Volume components and disable everything expensive
+            // Find all active Volume components — keep bloom for cheap AA, kill everything else
             var volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
             foreach (var vol in volumes)
             {
                 if (vol.profile == null) continue;
 
+                // Bloom stays active — provides edge softening (cheap AA substitute)
+                // Tune it down for mobile: fewer iterations, higher threshold
                 if (vol.profile.TryGet<Bloom>(out var bloom))
-                    bloom.active = false;
+                {
+                    bloom.active = true;
+                    bloom.skipIterations.Override(3);
+                    bloom.maxIterations.Override(3);
+                    bloom.highQualityFiltering.Override(false);
+                    bloom.threshold.Override(0.9f);
+                    bloom.intensity.Override(0.8f);
+                    bloom.scatter.Override(0.6f);
+                }
 
                 if (vol.profile.TryGet<Vignette>(out var vignette))
                     vignette.active = false;
```

</details>

### `90b13c32d` — Remove obsolete Bloom.skipIterations call, use maxIterations only

_Claude, 2026-03-09 16:36:32 +0000_

```text
 Assets/_Scripts/Utility/MobilePerformanceManager.cs | 1 -
 1 file changed, 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/MobilePerformanceManager.cs b/Assets/_Scripts/Utility/MobilePerformanceManager.cs
index a1f5a4218..a96eba2c9 100644
--- a/Assets/_Scripts/Utility/MobilePerformanceManager.cs
+++ b/Assets/_Scripts/Utility/MobilePerformanceManager.cs
@@ -190,7 +190,6 @@ namespace CosmicShore.Utility
                 if (vol.profile.TryGet<Bloom>(out var bloom))
                 {
                     bloom.active = true;
-                    bloom.skipIterations.Override(3);
                     bloom.maxIterations.Override(3);
                     bloom.highQualityFiltering.Override(false);
                     bloom.threshold.Override(0.9f);
```

</details>

### `6e794d1d5` — Fix MaterialPropertyBlock field initializers and restore bloom/colors

_Claude, 2026-03-09 16:58:40 +0000_

```text
- Move MaterialPropertyBlock construction from field initializers to Awake()
  in ClearPrisms and Projectile (Unity forbids engine API in constructors)
- Keep ColorAdjustments active so scene color grading isn't killed
- Raise Shader.globalMaximumLOD from 150 to 300 (150 can break post-processing)
- Lower bloom threshold 0.9→0.5, raise intensity 0.8→1.2 so bloom is visible
```

```text
 Assets/_Scripts/Game/Projectiles/Projectile.cs      |  3 ++-
 Assets/_Scripts/Game/Ship/ClearPrisms.cs            |  7 ++++++-
 Assets/_Scripts/Utility/MobilePerformanceManager.cs | 13 ++++++-------
 3 files changed, 14 insertions(+), 9 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 82 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Projectiles/Projectile.cs b/Assets/_Scripts/Game/Projectiles/Projectile.cs
index ca17b9fed..498fcfab6 100644
--- a/Assets/_Scripts/Game/Projectiles/Projectile.cs
+++ b/Assets/_Scripts/Game/Projectiles/Projectile.cs
@@ -34,7 +34,7 @@ namespace CosmicShore.Game.Projectiles
 
         // MaterialPropertyBlock for per-instance opacity (avoids material cloning)
         private static readonly int OpacityPropertyID = Shader.PropertyToID("_Opacity");
-        private readonly MaterialPropertyBlock _mpb = new();
+        private MaterialPropertyBlock _mpb;
 
         // NEW: remember pooled parent so we can restore it
         private Transform _pooledParent;
@@ -60,6 +60,7 @@ namespace CosmicShore.Game.Projectiles
         
         private void Awake()
         {
+            _mpb = new MaterialPropertyBlock();
             InitialScale = transform.localScale;
 
             // cache whatever parent it has in the pool (ship container or pool root)
diff --git a/Assets/_Scripts/Game/Ship/ClearPrisms.cs b/Assets/_Scripts/Game/Ship/ClearPrisms.cs
index f813d1260..03eb22d37 100644
--- a/Assets/_Scripts/Game/Ship/ClearPrisms.cs
+++ b/Assets/_Scripts/Game/Ship/ClearPrisms.cs
@@ -30,12 +30,17 @@ namespace CosmicShore
 
         // Cached to avoid per-frame allocations in OnTriggerStay
         private static readonly int AlphaPropertyID = Shader.PropertyToID("_Alpha");
-        private readonly MaterialPropertyBlock _mpb = new();
+        private MaterialPropertyBlock _mpb;
 
         // Cache Renderer lookups — OnTriggerStay fires hundreds of times per frame
         private readonly Dictionary<Collider, Renderer> _rendererCache = new(128);
 
 
+        private void Awake()
+        {
+            _mpb = new MaterialPropertyBlock();
+        }
+
         private void OnEnable()
         {
             if (Vessel == null)
diff --git a/Assets/_Scripts/Utility/MobilePerformanceManager.cs b/Assets/_Scripts/Utility/MobilePerformanceManager.cs
index a96eba2c9..38e2660e9 100644
--- a/Assets/_Scripts/Utility/MobilePerformanceManager.cs
+++ b/Assets/_Scripts/Utility/MobilePerformanceManager.cs
@@ -63,8 +63,8 @@ namespace CosmicShore.Utility
             QualitySettings.lodBias = 0.3f; // aggressive LOD — pop-in is acceptable
             QualitySettings.maximumLODLevel = 0;
 
-            // ── Shader LOD: force mobile SubShaders ─────────────────────
-            Shader.globalMaximumLOD = 150;
+            // ── Shader LOD: use standard SubShaders (150 can break post-processing)
+            Shader.globalMaximumLOD = 300;
 
             // ── Physics: reduce simulation overhead ─────────────────────
             ThrottlePhysics();
@@ -192,9 +192,9 @@ namespace CosmicShore.Utility
                     bloom.active = true;
                     bloom.maxIterations.Override(3);
                     bloom.highQualityFiltering.Override(false);
-                    bloom.threshold.Override(0.9f);
-                    bloom.intensity.Override(0.8f);
-                    bloom.scatter.Override(0.6f);
+                    bloom.threshold.Override(0.5f);
+                    bloom.intensity.Override(1.2f);
+                    bloom.scatter.Override(0.7f);
                 }
 
                 if (vol.profile.TryGet<Vignette>(out var vignette))
@@ -215,8 +215,7 @@ namespace CosmicShore.Utility
                 if (vol.profile.TryGet<LensDistortion>(out var lens))
                     lens.active = false;
 
-                if (vol.profile.TryGet<ColorAdjustments>(out var color))
-                    color.active = false;
+                // ColorAdjustments left active — disabling it kills scene color grading
             }
```

</details>

### `80e340e01` — Stop overriding bloom settings — use scene-authored volume profiles

_Claude, 2026-03-09 17:11:02 +0000_

```text
Runtime bloom overrides were fighting the authored profiles, resulting
in no visible bloom. Remove all bloom parameter overrides so the
scene's Volume profiles control bloom and color grading as designed.
```

```text
 Assets/_Scripts/Utility/MobilePerformanceManager.cs | 16 ++--------------
 1 file changed, 2 insertions(+), 14 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/MobilePerformanceManager.cs b/Assets/_Scripts/Utility/MobilePerformanceManager.cs
index 38e2660e9..25bf44725 100644
--- a/Assets/_Scripts/Utility/MobilePerformanceManager.cs
+++ b/Assets/_Scripts/Utility/MobilePerformanceManager.cs
@@ -179,23 +179,13 @@ namespace CosmicShore.Utility
 
         static void DisablePostProcessing()
         {
-            // Find all active Volume components — keep bloom for cheap AA, kill everything else
+            // Find all active Volume components — disable expensive effects, leave bloom & color as authored
             var volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
             foreach (var vol in volumes)
             {
                 if (vol.profile == null) continue;
 
-                // Bloom stays active — provides edge softening (cheap AA substitute)
-                // Tune it down for mobile: fewer iterations, higher threshold
-                if (vol.profile.TryGet<Bloom>(out var bloom))
-                {
-                    bloom.active = true;
-                    bloom.maxIterations.Override(3);
-                    bloom.highQualityFiltering.Override(false);
-                    bloom.threshold.Override(0.5f);
-                    bloom.intensity.Override(1.2f);
-                    bloom.scatter.Override(0.7f);
-                }
+                // Bloom & ColorAdjustments: left untouched — use scene-authored values
 
                 if (vol.profile.TryGet<Vignette>(out var vignette))
                     vignette.active = false;
@@ -214,8 +204,6 @@ namespace CosmicShore.Utility
 
                 if (vol.profile.TryGet<LensDistortion>(out var lens))
                     lens.active = false;
-
-                // ColorAdjustments left active — disabling it kills scene color grading
             }
         }
     }
```

</details>

### `068103516` — Boost bloom and remove Shader.globalMaximumLOD restriction

_Claude, 2026-03-09 17:22:52 +0000_

```text
- Remove Shader.globalMaximumLOD entirely — it can prevent URP
  post-processing shaders from running
- Crank bloom in GamePlay profile: threshold 0.8→0.4, intensity 1→2.5,
  scatter 0.65→0.8 for heavy visible bloom
```

```text
 Assets/_Graphics/GamePlay PostProcessing Profile.asset | 6 +++---
 Assets/_Scripts/Utility/MobilePerformanceManager.cs    | 3 ---
 2 files changed, 3 insertions(+), 6 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/MobilePerformanceManager.cs b/Assets/_Scripts/Utility/MobilePerformanceManager.cs
index 25bf44725..67b200fa2 100644
--- a/Assets/_Scripts/Utility/MobilePerformanceManager.cs
+++ b/Assets/_Scripts/Utility/MobilePerformanceManager.cs
@@ -63,9 +63,6 @@ namespace CosmicShore.Utility
             QualitySettings.lodBias = 0.3f; // aggressive LOD — pop-in is acceptable
             QualitySettings.maximumLODLevel = 0;
 
-            // ── Shader LOD: use standard SubShaders (150 can break post-processing)
-            Shader.globalMaximumLOD = 300;
-
             // ── Physics: reduce simulation overhead ─────────────────────
             ThrottlePhysics();
 
```

</details>

### `a76b73a88` — Disable silhouette/trail/elemental UI and enable FXAA

_Claude, 2026-03-09 17:39:16 +0000_

```text
- Deactivate SilhouetteView and ElementalBarsView GameObjects in
  SilhouetteController.OnEnable for GDC demo
- Enable FXAA (High quality) on the main camera via
  UniversalAdditionalCameraData — cheapest AA option, single
  post-process pass with zero bandwidth overhead vs MSAA
```

```text
 Assets/_Scripts/Game/Ship/SilhouetteController.cs   | 4 ++++
 Assets/_Scripts/Utility/MobilePerformanceManager.cs | 7 +++++++
 2 files changed, 11 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Ship/SilhouetteController.cs b/Assets/_Scripts/Game/Ship/SilhouetteController.cs
index 4ab03663a..e7b5cae23 100644
--- a/Assets/_Scripts/Game/Ship/SilhouetteController.cs
+++ b/Assets/_Scripts/Game/Ship/SilhouetteController.cs
@@ -35,6 +35,10 @@ namespace CosmicShore
 
         void OnEnable()
         {
+            // Disable silhouette trail and elemental bars UI for GDC demo
+            if (view) view.gameObject.SetActive(false);
+            if (elementBars) elementBars.gameObject.SetActive(false);
+
             if (vesselPrismController)
             {
                 vesselPrismController.OnBlockCreated += OnBlockCreated;
diff --git a/Assets/_Scripts/Utility/MobilePerformanceManager.cs b/Assets/_Scripts/Utility/MobilePerformanceManager.cs
index 67b200fa2..b564819e7 100644
--- a/Assets/_Scripts/Utility/MobilePerformanceManager.cs
+++ b/Assets/_Scripts/Utility/MobilePerformanceManager.cs
@@ -172,6 +172,13 @@ namespace CosmicShore.Utility
 
             cam.layerCullDistances = distances;
             cam.layerCullSpherical = true; // spherical culling is cheaper for moving cameras
+
+            // ── FXAA: cheapest AA — single post-process pass, no bandwidth cost ──
+            if (cam.TryGetComponent<UniversalAdditionalCameraData>(out var camData))
+            {
+                camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
+                camData.antialiasingQuality = AntialiasingQuality.High;
+            }
         }
 
         static void DisablePostProcessing()
```

</details>

### `fbed5ba5f` — Unlock HexRace and Dolphin for first-time users

_Claude, 2026-03-09 17:49:35 +0000_

```text
- Set Dolphin vessel isLocked=false in SO asset so it's available in
  Freestyle and any mode that includes it in its vessel list
- Add HexRace to always-unlocked modes in GameModeProgressionService
- Initialize HexRace intensity data in EnsureFirstModeUnlocked() so
  intensity selection works immediately
```

```text
 Assets/_SO_Assets/Classes/SO_Class_Dolphin.asset               | 2 +-
 Assets/_Scripts/Game/Progression/GameModeProgressionService.cs | 9 +++++++--
 2 files changed, 8 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Progression/GameModeProgressionService.cs b/Assets/_Scripts/Game/Progression/GameModeProgressionService.cs
index 64f619cfa..e8887dab9 100644
--- a/Assets/_Scripts/Game/Progression/GameModeProgressionService.cs
+++ b/Assets/_Scripts/Game/Progression/GameModeProgressionService.cs
@@ -107,8 +107,8 @@ namespace CosmicShore.Game.Progression
         /// </summary>
         public bool IsGameModeUnlocked(GameModes mode)
         {
-            // Freestyle is always available
-            if (mode == GameModes.Freestyle)
+            // Freestyle and HexRace are always available for first-time users
+            if (mode == GameModes.Freestyle || mode == GameModes.HexRace)
                 return true;
 
             // First quest mode is always unlocked
@@ -711,6 +711,11 @@ namespace CosmicShore.Game.Progression
             string firstMode = questList.Quests[0].GameMode.ToString();
             ProgressionData.MarkUnlocked(firstMode);
             ProgressionData.EnsureIntensityInitialized(firstMode);
+
+            // HexRace is always available for first-time users
+            string hexRace = GameModes.HexRace.ToString();
+            ProgressionData.MarkUnlocked(hexRace);
+            ProgressionData.EnsureIntensityInitialized(hexRace);
         }
 
         /// <summary>
```

</details>

### `6f7156a39` — Fix Dolphin conic AOE explosion not destroying prisms

_Claude, 2026-03-09 18:08:52 +0000_

```text
Commit 1fbba8f6 disabled Explosions (layer 10) vs TrailBlocks (layer 9)
physics collisions as an optimization, since AOEExplosion migrated to
batch processing via PrismAOERegistry. However, AOEConicExplosion still
relied on physics OnTriggerEnter for damage — its ExplodeAsync override
never called BeginBatchProcessing/ProcessBatchFrame/EndBatchProcessing.

This integrates the conic explosion with the same batch processing system:
- Begin/End batch processing around the animation loop
- Compute a bounding sphere for the cone (centered at half-height along
  the forward axis, radius covers apex-to-base-edge distance)
- Process hits each frame via ExplosionImpactor.ProcessBatchFrame
- Handle super-shield destruction (shouldContinue=false) and exceptions
- Make _explosionImpactor protected so subclasses can access it
```

```text
 Assets/_Scripts/Game/Projectiles/AOEConicExplosion.cs | 54 +++++++++++++++++++++++++++++++++++++++++--------
 Assets/_Scripts/Game/Projectiles/AOEExplosion.cs      |  2 +-
 2 files changed, 47 insertions(+), 9 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 118 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Projectiles/AOEConicExplosion.cs b/Assets/_Scripts/Game/Projectiles/AOEConicExplosion.cs
index d830e05a4..f1dd0fd40 100644
--- a/Assets/_Scripts/Game/Projectiles/AOEConicExplosion.cs
+++ b/Assets/_Scripts/Game/Projectiles/AOEConicExplosion.cs
@@ -56,41 +56,71 @@ namespace CosmicShore.Game.Projectiles
 
         protected override async UniTaskVoid ExplodeAsync(CancellationToken ct)
         {
+            var impactor = _explosionImpactor;
             try
             {
+                impactor?.BeginBatchProcessing();
+
                 await UniTask.Delay(
                     System.TimeSpan.FromSeconds(ExplosionDelay),
                     DelayType.DeltaTime,
                     PlayerLoopTiming.Update,
                     ct);
 
+                if (!this || ct.IsCancellationRequested)
+                {
+                    impactor?.EndBatchProcessing();
+                    return;
+                }
+
                 if (TryGetComponent<MeshRenderer>(out var meshRenderer))
                     meshRenderer.material = Material;
 
                 float elapsed = 0f;
-
-                var sphereCol = GetComponent<SphereCollider>();
+                var containerTransform = coneContainer.transform;
 
                 while (elapsed < ExplosionDuration)
                 {
                     ct.ThrowIfCancellationRequested();
+                    if (!this || containerTransform == null)
+                    {
+                        impactor?.EndBatchProcessing();
+                        return;
+                    }
 
                     elapsed += Time.deltaTime;
                     float t = elapsed / ExplosionDuration;
                     float lerp = Mathf.Sin(t * PI_OVER_TWO);
 
                     // Scale cone
-                    coneContainer.transform.localScale =
+                    containerTransform.localScale =
                         Vector3.Lerp(Vector3.zero, MaxScaleVector, lerp);
 
-                    // Dynamic collider radius update
-                    float z = Mathf.Clamp(coneContainer.transform.localScale.z, 0.01f, Mathf.Infinity);
-                    sphereCol.radius = coneContainer.transform.localScale.x / (z * 2f);
+                    // Batch AOE damage — use a bounding sphere that encompasses the cone.
+                    // Center is offset along the cone's forward axis (half the current height),
+                    // radius covers from that center to the apex and the base edge.
+                    float currentHeight = height * lerp;
+                    float currentWidth = MaxScale * lerp;
+                    float halfHeight = currentHeight * 0.5f;
+                    Vector3 batchCenter = containerTransform.position
+                        + containerTransform.forward * halfHeight;
+                    float batchRadius = Mathf.Sqrt(halfHeight * halfHeight + currentWidth * currentWidth);
+
+                    bool shouldContinue = impactor?.ProcessBatchFrame(
+                        batchCenter, batchRadius, speed, Inertia) ?? true;
+
+                    if (!shouldContinue)
+                    {
+                        impactor?.EndBatchProcessing();
+                        DestroyContainer();
+                        if (this) Destroy(gameObject);
+                        return;
+                    }
 
                     // Opacity fade
                     float opacity =
                         Mathf.Clamp(
-                            (MaxScaleVector - coneContainer.transform.localScale).magnitude
+                            (MaxScaleVector - containerTransform.localScale).magnitude
```

</details>
