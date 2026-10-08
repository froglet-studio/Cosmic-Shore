# Branch archive: `elemental-restore`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-05-22 by xghest
- **Unmerged commits:** 4
- **Forked from:** `6cc0166e1` (2026-05-20, Merge pull request #522 from froglet-studio/claude/fix-ai-crystal-indicator-98)
- **Tip:** `fe31126c8`
- **Files touched (2):**
  - `Assets/_Prefabs/Spacevessels/Squirrel.prefab`
  - `Assets/_Scripts/UI/View/ElementalBarsView.cs`

### `177dfda2a` — Fix squirrel elemental UI

_xghest, 2026-05-11 10:52:09 -0700_

```text
 Assets/_Prefabs/Spacevessels/Squirrel.prefab | 13362 ++++++-------------------------------------------------
 Assets/_Scripts/UI/View/ElementalBarsView.cs |   323 +-
 2 files changed, 1573 insertions(+), 12112 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 487 lines)</summary>

```diff
diff --git a/Assets/_Scripts/UI/View/ElementalBarsView.cs b/Assets/_Scripts/UI/View/ElementalBarsView.cs
index 0b9b5c3d5..f0f6943d6 100644
--- a/Assets/_Scripts/UI/View/ElementalBarsView.cs
+++ b/Assets/_Scripts/UI/View/ElementalBarsView.cs
@@ -8,11 +8,6 @@ using CosmicShore.Gameplay;
 
 namespace CosmicShore.UI
 {
-    /// <summary>
-    /// Displays 4 element columns, each with 15 discrete pip images.
-    /// Fill pips start disabled. On Build(), first zeroLineIndex (5) are enabled as level-0 baseline.
-    /// Buffs enable pips upward with staggered scale punch. Debuffs disable pips with shake + haptics.
-    /// </summary>
     public class ElementalBarsView : MonoBehaviour
     {
         [Serializable]
@@ -21,13 +16,13 @@ namespace CosmicShore.UI
             [Tooltip("The element this bar represents")]
             public Element element;
 
-            [Tooltip("Background pip images (bottom to top, 15 total)")]
-            public Image[] bgPips;
+            [Tooltip("Single flower Image — sprite + color are set at runtime")]
+            public Image flowerImage;
 
-            [Tooltip("Fill pip images (bottom to top, 15 total)")]
-            public Image[] fillPips;
+            [Tooltip("Sprites indexed by petal count: [0]=0 petals, [1]=1 petal … [5]=5 petals")]
+            public Sprite[] petalSprites;
 
-            [Tooltip("Label/icon image below the bar")]
+            [Tooltip("Label/icon image below the flower")]
             public Image labelIcon;
 
             [Tooltip("Normal sprite for the label (restored after drift)")]
@@ -37,72 +32,59 @@ namespace CosmicShore.UI
         [Header("Bar Bindings")]
         [SerializeField] private ElementBarBinding[] bars = new ElementBarBinding[0];
 
-        [Header("Range")]
-        [Tooltip("Pip index that represents level 0 (e.g. 5 means first 5 pips are negative territory)")]
-        [SerializeField] private int zeroLineIndex = 5;
-
         [Header("Colors")]
-        [SerializeField] private Color filledColor = Color.white;
-        [Tooltip("Fill color when level is below zero")]
-        [SerializeField] private Color negativeFillColor = new(1f, 0.3f, 0.3f, 0.8f);
-        [Tooltip("Color flash on debuff pips before they disappear")]
+        [SerializeField] private Color dangerColor   = new(1f,    0.2f, 0.2f, 1f);
+        [SerializeField] private Color baselineColor = Color.white;
+        [SerializeField] private Color buffedColor   = new(0.2f, 1f,   0.3f, 1f);
+        [Tooltip("Color flash on debuff before restoring to zone color")]
         [SerializeField] private Color debuffFlashColor = new(1f, 0.2f, 0.2f, 1f);
 
-        [Header("Juice — Pip Transitions")]
-        [Tooltip("Scale multiplier when a pip appears (buff)")]
-        [SerializeField] private float buffPopScale = 1.5f;
-        [Tooltip("Duration of the pop-in tween per pip")]
-        [SerializeField] private float buffPopDuration = 0.18f;
-        [Tooltip("Stagger delay between each pip appearing")]
-        [SerializeField] private float buffStaggerDelay = 0.04f;
-        [Tooltip("Duration of the shake-out tween per pip on debuff")]
-        [SerializeField] private float debuffShakeDuration = 0.15f;
-        [Tooltip("Shake strength on debuff pip removal")]
+        [Header("Juice — Flower Transitions")]
+        [SerializeField] private float buffPopScale        = 1.4f;
+        [SerializeField] private float buffPopDuration     = 0.25f;
+        [SerializeField] private float debuffShakeDuration = 0.20f;
         [SerializeField] private float debuffShakeStrength = 8f;
 
         [Header("Juice — Haptics")]
-        [Tooltip("Fire haptic on debuff (element level decrease)")]
-        [SerializeField] private bool hapticOnDebuff = true;
-        [Tooltip("Haptic intensity for debuff (0-1)")]
-        [SerializeField] private float debuffHapticAmplitude = 0.6f;
-        [Tooltip("Haptic frequency for debuff")]
-        [SerializeField] private float debuffHapticFrequency = 0.5f;
-        [Tooltip("Haptic duration for debuff")]
-        [SerializeField] private float debuffHapticDuration = 0.15f;
+        [SerializeField] private bool  hapticOnDebuff         = true;
+        [SerializeField] private float debuffHapticAmplitude  = 0.6f;
+        [SerializeField] private float debuffHapticFrequency  = 0.5f;
+        [SerializeField] private float debuffHapticDuration   = 0.15f;
 
         [Header("Juice — General")]
         [SerializeField] private float iconPunchDuration = 0.25f;
-        [SerializeField] private float iconPunchScale = 1.4f;
+        [SerializeField] private float iconPunchScale    = 1.4f;
         [SerializeField] private float colorTweenDuration = 0.35f;
 
         [Header("Juice — Joust")]
         [SerializeField] private Color joustFlashColor = Color.red;
 
         [Header("Juice — Drift")]
-        [SerializeField] private float driftRotationAngle = 15f;
-        [SerializeField] private float driftRotationDuration = 0.2f;
+        [SerializeField] private float  driftRotationAngle    = 15f;
+        [SerializeField] private float  driftRotationDuration = 0.2f;
         [SerializeField] private Sprite doubleDriftSprite;
 
         // Runtime state
         private RectTransform _rootRT;
-        private Tween _scaleTween;
-        private int[] _currentLevels;
+        private Tween   _scaleTween;
+        private int[]   _currentLevels;
         private Color[] _barDomainColors;
         private Color[] _originalLabelColors;
         private Vector3[] _originalLabelScales;
         private Tween[] _driftRotationTweens;
         private Tween[] _labelScaleTweens;
         private Tween[] _labelColorTweens;
-        private Tween[][] _pipTweens; // [barIndex][pipIndex]
+        private Tween[] _flowerTweens;
         private bool _built;
         private bool _overtakeActive;
 
         public bool IsBuilt => _built;
 
+        private const int BaselineLevel = 5;
+        private const int MaxPetals     = 5;
+
         void Start()
         {
-            // Self-initialize so the baseline pips show even if the controller
-            // chain hasn't called Build() yet (e.g. elementBars not wired on SilhouetteController).
             Build();
         }
 
@@ -114,14 +96,14 @@ namespace CosmicShore.UI
             _rootRT = (RectTransform)transform;
 
             int count = bars.Length;
-            _currentLevels = new int[count];
-            _barDomainColors = new Color[count];
+            _currentLevels       = new int[count];
+            _barDomainColors     = new Color[count];
             _originalLabelColors = new Color[count];
             _originalLabelScales = new Vector3[count];
             _driftRotationTweens = new Tween[count];
-            _labelScaleTweens = new Tween[count];
-            _labelColorTweens = new Tween[count];
-            _pipTweens = new Tween[count][];
+            _labelScaleTweens    = new Tween[count];
+            _labelColorTweens    = new Tween[count];
+            _flowerTweens        = new Tween[count];
 
             for (int i = 0; i < count; i++)
             {
@@ -136,24 +118,8 @@ namespace CosmicShore.UI
```

</details>

### `4a197f644` — update 20 values

_xghest, 2026-05-21 13:06:47 -0700_

```text
 Assets/_Prefabs/Spacevessels/Squirrel.prefab |  2 +-
 Assets/_Scripts/UI/View/ElementalBarsView.cs | 39 ++++++++++++++++++++++++---------------
 2 files changed, 25 insertions(+), 16 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/UI/View/ElementalBarsView.cs b/Assets/_Scripts/UI/View/ElementalBarsView.cs
index f0f6943d6..2ffde63bd 100644
--- a/Assets/_Scripts/UI/View/ElementalBarsView.cs
+++ b/Assets/_Scripts/UI/View/ElementalBarsView.cs
@@ -33,9 +33,11 @@ namespace CosmicShore.UI
         [SerializeField] private ElementBarBinding[] bars = new ElementBarBinding[0];
 
         [Header("Colors")]
-        [SerializeField] private Color dangerColor   = new(1f,    0.2f, 0.2f, 1f);
-        [SerializeField] private Color baselineColor = Color.white;
-        [SerializeField] private Color buffedColor   = new(0.2f, 1f,   0.3f, 1f);
+        [SerializeField] private Color dangerColor      = new(1f,    0.2f, 0.2f, 1f);
+        [SerializeField] private Color baselineColor    = Color.white;
+        [SerializeField] private Color normalColor      = Color.white;   // overridden per-bar by domain color
+        [SerializeField] private Color buffedGreenColor = new(0.2f, 1f,  0.3f, 1f);
+        [SerializeField] private Color buffedBlueColor  = new(0.3f, 0.7f, 1f, 1f);
         [Tooltip("Color flash on debuff before restoring to zone color")]
         [SerializeField] private Color debuffFlashColor = new(1f, 0.2f, 0.2f, 1f);
 
@@ -80,7 +82,7 @@ namespace CosmicShore.UI
 
         public bool IsBuilt => _built;
 
-        private const int BaselineLevel = 5;
+        private const int BaselineLevel = 0;
         private const int MaxPetals     = 5;
 
         void Start()
@@ -170,7 +172,7 @@ namespace CosmicShore.UI
             // Floor at BaselineLevel in normal gameplay so an uninitialized GetLevel(0) never
             // triggers the danger zone. Danger petals are only reachable via BeginOvertake().
             int clamped = _overtakeActive ? level : Mathf.Max(BaselineLevel, level);
-            clamped = Mathf.Clamp(clamped, 0, BaselineLevel + MaxPetals * 2);
+            clamped = Mathf.Clamp(clamped, -MaxPetals, BaselineLevel + MaxPetals * 2);
 
             _barDomainColors[idx] = domainColor;
             int prev = _currentLevels[idx];
@@ -357,28 +359,35 @@ namespace CosmicShore.UI
 
         void GetZoneInfo(int idx, int level, out int petalCount, out Color color)
         {
-            if (level < BaselineLevel)
+            if (level < 0)
             {
-                // Danger: level 0 → 5 petals, level 4 → 1 petal
-                petalCount = BaselineLevel - level;
+                // Danger: -1 → 1 petal, -5 → 5 petals (red)
+                petalCount = Mathf.Clamp(-level, 1, MaxPetals);
                 color = dangerColor;
             }
-            else if (level == BaselineLevel)
+            else if (level == 0)
             {
+                // Baseline: no petals
                 petalCount = 0;
                 color = baselineColor;
             }
-            else if (level <= BaselineLevel + MaxPetals)
+            else if (level <= MaxPetals)
             {
-                // Normal: level 6 → 1 petal, level 10 → 5 petals
-                petalCount = level - BaselineLevel;
+                // Normal: 1 → 1 petal, 5 → 5 petals (domain color)
+                petalCount = level;
                 color = _barDomainColors[idx];
             }
+            else if (level <= MaxPetals * 2)
+            {
+                // Green buffed: 6 → 1 petal, 10 → 5 petals
+                petalCount = level - MaxPetals;
+                color = buffedGreenColor;
+            }
             else
             {
-                // Buffed: level 11 → 1 petal, level 15 → 5 petals
-                petalCount = level - (BaselineLevel + MaxPetals);
-                color = buffedColor;
+                // Blue buffed: 11 → 1 petal, 15 → 5 petals
+                petalCount = Mathf.Clamp(level - MaxPetals * 2, 1, MaxPetals);
+                color = buffedBlueColor;
             }
         }
 
```

</details>

_Also contains 2 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
