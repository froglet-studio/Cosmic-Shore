# Branch archive: `vignette-testing`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-06-15 by xghest
- **Unmerged commits:** 3
- **Forked from:** `f121e6d56` (2026-06-01, Merge branch 'claude/loving-fermi-DVWsY' into bleeding-edge)
- **Tip:** `113742b8c`
- **Files touched (8):**
  - `Assets/_Prefabs/UI Elements/In Game/Vignette.prefab`
  - `Assets/_Prefabs/UI Elements/In Game/Vignette.prefab.meta`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameJoust_Gameplay.meta`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameJoust_Gameplay.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameJoust_Gameplay/Vignette Profile 2.asset`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameJoust_Gameplay/Vignette Profile 2.asset.meta`
  - `Assets/_Scripts/UI/View/SquirrelVignetteController.cs`
  - `Assets/_Scripts/UI/View/SquirrelVignetteController.cs.meta`

### `6d57008a6` — add squirrel vignette in Joust minigame

_xghest, 2026-06-14 11:19:19 -0700_

```text
 Assets/_Prefabs/UI Elements/In Game/Vignette.prefab                   |  76 ++++++++++++
 Assets/_Prefabs/UI Elements/In Game/Vignette.prefab.meta              |   7 ++
 .../MinigameJoust_Gameplay/Vignette Profile 2.asset                   |  44 +++++++
 .../MinigameJoust_Gameplay/Vignette Profile 2.asset.meta              |   8 ++
 Assets/_Scripts/UI/View/SquirrelVignetteController.cs                 | 214 ++++++++++++++++++++++++++++++++
 Assets/_Scripts/UI/View/SquirrelVignetteController.cs.meta            |   2 +
 6 files changed, 351 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 150 of 220 lines)</summary>

```diff
diff --git a/Assets/_Scripts/UI/View/SquirrelVignetteController.cs b/Assets/_Scripts/UI/View/SquirrelVignetteController.cs
new file mode 100644
index 000000000..8fd47a900
--- /dev/null
+++ b/Assets/_Scripts/UI/View/SquirrelVignetteController.cs
@@ -0,0 +1,214 @@
+using System.Collections.Generic;
+using Reflex.Attributes;
+using UnityEngine;
+using UnityEngine.Rendering;
+using UnityEngine.Rendering.Universal;
+using CosmicShore.Gameplay;
+using CosmicShore.Utility;
+
+namespace CosmicShore.UI
+{
+    public class SquirrelVignetteController : MonoBehaviour
+    {
+        [Header("Volume")]
+        [Tooltip("Assign the Global Volume that has a Vignette override.")]
+        [SerializeField] Volume _volume;
+
+        [Header("Detection — Distance")]
+        [SerializeField] float _warningRange = 50f;
+        [SerializeField] float _dangerRange  = 20f;
+
+        [Header("Detection — Direction")]
+        [Tooltip("Dot-product cutoff: enemy forward must point this close to you. 0.7 ≈ within 45°.")]
+        [SerializeField, Range(0f, 1f)] float _facingDotThreshold = 0.5f;
+
+        [Header("Detection — Speed")]
+        [Tooltip("Minimum closing speed (m/s) for the threat to register.")]
+        [SerializeField] float _minClosingSpeed = 2f;
+
+        [Header("Intensity")]
+        [SerializeField, Range(0f, 1f)] float _warningIntensity = 0.8f;
+        [SerializeField, Range(0f, 1f)] float _dangerIntensity  = 1f;
+        [SerializeField] float _fadeInSpeed  = 4f;
+        [SerializeField] float _fadeOutSpeed = 1.5f;
+        [SerializeField] float _pulseSpeed   = 3f;
+
+        [Header("Debug")]
+        [Tooltip("Preview intensity without needing enemies nearby. 0 = off.")]
+        [SerializeField, Range(0f, 1f)] float _previewIntensity = 0f;
+        [Tooltip("Log threat evaluation every N seconds. 0 = off.")]
+        [SerializeField] float _debugLogInterval = 2f;
+
+        [Inject] GameDataSO _gameData;
+
+        enum ThreatLevel { None, Warning, Danger }
+
+        Vignette _vignette;
+        Transform _localVesselTransform;
+        Vector3 _prevLocalPos;
+        Vector3 _localVelocity;
+        readonly Dictionary<IPlayer, Vector3> _prevEnemyPositions = new();
+        float _nextLogTime;
+
+        void Start()
+        {
+            if (_volume == null)
+            {
+                Debug.LogError("[SquirrelVignetteController] No Volume assigned — wire the Global Volume in the inspector.");
+                return;
+            }
+
+            if (!_volume.profile.TryGet(out _vignette))
+            {
+                _vignette = _volume.profile.Add<Vignette>(true);
+                _vignette.active = true;
+            }
+
+            _vignette.intensity.overrideState = true;
+            _vignette.intensity.value = 0f;
+        }
+
+        void Update()
+        {
+            if (_vignette == null) return;
+
+            float dt = Time.deltaTime;
+            if (dt <= 0f) return;
+
+            ResolveLocalVessel();
+
+            if (_previewIntensity > 0f)
+            {
+                _vignette.intensity.value = Mathf.Lerp(_vignette.intensity.value, _previewIntensity, _fadeInSpeed * dt);
+                return;
+            }
+
+            if (_localVesselTransform == null)
+            {
+                _vignette.intensity.value = Mathf.Lerp(_vignette.intensity.value, 0f, _fadeOutSpeed * dt);
+                return;
+            }
+
+            UpdateLocalVelocity(dt);
+
+            ThreatLevel threat = GetThreatLevel(dt);
+
+            float targetIntensity;
+            float lerpSpeed;
+            switch (threat)
+            {
+                case ThreatLevel.Danger:
+                    float pulse = (Mathf.Sin(Time.time * _pulseSpeed) + 1f) * 0.5f;
+                    targetIntensity = Mathf.Lerp(_warningIntensity, _dangerIntensity, pulse);
+                    lerpSpeed = _fadeInSpeed;
+                    break;
+                case ThreatLevel.Warning:
+                    targetIntensity = _warningIntensity;
+                    lerpSpeed = _fadeInSpeed;
+                    break;
+                default:
+                    targetIntensity = 0f;
+                    lerpSpeed = _fadeOutSpeed;
+                    break;
+            }
+
+            _vignette.intensity.value = Mathf.Lerp(_vignette.intensity.value, targetIntensity, lerpSpeed * dt);
+
+            CacheEnemyPositions();
+        }
+
+        void ResolveLocalVessel()
+        {
+            if (_localVesselTransform != null) return;
+            var lp = _gameData?.LocalPlayer;
+            if (lp?.Vessel == null) return;
+            _localVesselTransform = (lp.Vessel as MonoBehaviour)?.transform;
+            if (_localVesselTransform != null)
+                _prevLocalPos = _localVesselTransform.position;
+        }
+
+        void UpdateLocalVelocity(float dt)
+        {
+            var pos = _localVesselTransform.position;
+            _localVelocity = (pos - _prevLocalPos) / dt;
+            _prevLocalPos = pos;
+        }
+
+        ThreatLevel GetThreatLevel(float dt)
+        {
+            var localPlayer = _gameData?.LocalPlayer;
+            bool doLog = _debugLogInterval > 0f && Time.time >= _nextLogTime;
+            if (doLog) _nextLogTime = Time.time + _debugLogInterval;
+
+            if (localPlayer == null)
+            {
```

</details>

### `a0505ceec` — update vignette for joust minigame scene

_xghest, 2026-06-14 11:27:10 -0700_

```text
 Assets/_Scenes/Multiplayer Scenes/MinigameJoust_Gameplay.meta  |  8 +++++
 Assets/_Scenes/Multiplayer Scenes/MinigameJoust_Gameplay.unity | 78 ++++++++++++++++++++++++++++++++++++++++
 2 files changed, 86 insertions(+)
```

### `113742b8c` — Update SquirrelVignetteController.cs

_xghest, 2026-06-15 12:22:20 -0700_

```text
 Assets/_Scripts/UI/View/SquirrelVignetteController.cs | 50 ++++++++++++++++++++++---------------------------
 1 file changed, 22 insertions(+), 28 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/UI/View/SquirrelVignetteController.cs b/Assets/_Scripts/UI/View/SquirrelVignetteController.cs
index 8fd47a900..26f7948c2 100644
--- a/Assets/_Scripts/UI/View/SquirrelVignetteController.cs
+++ b/Assets/_Scripts/UI/View/SquirrelVignetteController.cs
@@ -19,7 +19,7 @@ namespace CosmicShore.UI
         [SerializeField] float _dangerRange  = 20f;
 
         [Header("Detection — Direction")]
-        [Tooltip("Dot-product cutoff: enemy forward must point this close to you. 0.7 ≈ within 45°.")]
+        [Tooltip("Enemy forward must point this close to you. 0.5 ≈ within 60°.")]
         [SerializeField, Range(0f, 1f)] float _facingDotThreshold = 0.5f;
 
         [Header("Detection — Speed")]
@@ -27,17 +27,18 @@ namespace CosmicShore.UI
         [SerializeField] float _minClosingSpeed = 2f;
 
         [Header("Intensity")]
-        [SerializeField, Range(0f, 1f)] float _warningIntensity = 0.8f;
+        [SerializeField, Range(0f, 1f)] float _warningIntensity = 0.7f;
         [SerializeField, Range(0f, 1f)] float _dangerIntensity  = 1f;
-        [SerializeField] float _fadeInSpeed  = 4f;
+        [Tooltip("How fast the vignette rises to the target level (exponential, higher = snappier).")]
+        [SerializeField] float _fadeInSpeed  = 6f;
+        [Tooltip("How fast the vignette drains to 0 once the threat clears (units/sec).")]
         [SerializeField] float _fadeOutSpeed = 1.5f;
-        [SerializeField] float _pulseSpeed   = 3f;
 
         [Header("Debug")]
         [Tooltip("Preview intensity without needing enemies nearby. 0 = off.")]
         [SerializeField, Range(0f, 1f)] float _previewIntensity = 0f;
         [Tooltip("Log threat evaluation every N seconds. 0 = off.")]
-        [SerializeField] float _debugLogInterval = 2f;
+        [SerializeField] float _debugLogInterval = 0f;
 
         [Inject] GameDataSO _gameData;
 
@@ -79,13 +80,13 @@ namespace CosmicShore.UI
 
             if (_previewIntensity > 0f)
             {
-                _vignette.intensity.value = Mathf.Lerp(_vignette.intensity.value, _previewIntensity, _fadeInSpeed * dt);
+                _vignette.intensity.value = _previewIntensity;
                 return;
             }
 
             if (_localVesselTransform == null)
             {
-                _vignette.intensity.value = Mathf.Lerp(_vignette.intensity.value, 0f, _fadeOutSpeed * dt);
+                _vignette.intensity.value = Mathf.MoveTowards(_vignette.intensity.value, 0f, _fadeOutSpeed * dt);
                 return;
             }
 
@@ -93,26 +94,20 @@ namespace CosmicShore.UI
 
             ThreatLevel threat = GetThreatLevel(dt);
 
-            float targetIntensity;
-            float lerpSpeed;
-            switch (threat)
+            float target = threat switch
             {
-                case ThreatLevel.Danger:
-                    float pulse = (Mathf.Sin(Time.time * _pulseSpeed) + 1f) * 0.5f;
-                    targetIntensity = Mathf.Lerp(_warningIntensity, _dangerIntensity, pulse);
-                    lerpSpeed = _fadeInSpeed;
-                    break;
-                case ThreatLevel.Warning:
-                    targetIntensity = _warningIntensity;
-                    lerpSpeed = _fadeInSpeed;
-                    break;
-                default:
-                    targetIntensity = 0f;
-                    lerpSpeed = _fadeOutSpeed;
-                    break;
-            }
-
-            _vignette.intensity.value = Mathf.Lerp(_vignette.intensity.value, targetIntensity, lerpSpeed * dt);
+                ThreatLevel.Danger  => _dangerIntensity,
+                ThreatLevel.Warning => _warningIntensity,
+                _                   => 0f
+            };
+
+            float current = _vignette.intensity.value;
+            if (target > current)
+                // smooth exponential rise — feels responsive without snapping
+                _vignette.intensity.value = Mathf.Lerp(current, target, 1f - Mathf.Exp(-_fadeInSpeed * dt));
+            else
+                // MoveTowards so it actually reaches 0 instead of asymptoting
+                _vignette.intensity.value = Mathf.MoveTowards(current, 0f, _fadeOutSpeed * dt);
 
             CacheEnemyPositions();
         }
@@ -178,8 +173,7 @@ namespace CosmicShore.UI
                 {
                     Vector3 enemyVelocity = (enemyT.position - prevEnemyPos) / dt;
                     Vector3 relVelocity = enemyVelocity - _localVelocity;
-                    // positive = enemy closing toward player
-                    float closingSpeed = Vector3.Dot(relVelocity, toLocal / dist);
+                    float closingSpeed = Vector3.Dot(relVelocity, toLocal / dist); // positive = approaching
                     if (closingSpeed < _minClosingSpeed)
                     {
                         if (doLog) Debug.Log($"[Vignette] enemy dist={dist:F1} closingSpeed={closingSpeed:F1} below min {_minClosingSpeed}");
```

</details>
