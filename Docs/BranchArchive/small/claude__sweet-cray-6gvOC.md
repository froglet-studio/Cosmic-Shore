# Branch archive: `claude/sweet-cray-6gvOC`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-06-05 by Claude
- **Unmerged commits:** 1
- **Forked from:** `4b1ff6305` (2026-06-05, Merge branch 'bleeding-edge' into claude/sweet-cray-6gvOC)
- **Tip:** `0a3dc9294`
- **Files touched (2):**
  - `Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/UI.prefab`
  - `Assets/_Scripts/UI/Views/XPTrackView.cs`

### `0a3dc9294` — feat(menu): wire the XP progress bar onto the Profile tab

_Claude, 2026-06-05 20:08:45 +0000_

```text
The XPTrackView script and its data (XPTrackData + 7 reward assets) existed but
the component was never attached, so the Profile tab's XP bar never appeared.

- Attach XPTrackView to the ProfileScreen GameObject, wiring xpTrackData and the
  existing (non-interactive) progress Slider already present under XPItemPanels.
- XPTrackView now reads PlayerDataService via the persistent Instance (not
  [Inject]) for DI-timing robustness, subscribes/refreshes symmetrically in
  OnEnable/OnDisable, and normalizes the authored 0..7 slider range to 0..1 so
  the fill tracks xp/totalXP. Item/level prefabs remain unwired (null-safe) — the
  bar fills without them; reward markers can be added later in the editor.
```

```text
 Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/UI.prefab | 22 ++++++++++++++++++++++
 Assets/_Scripts/UI/Views/XPTrackView.cs                    | 44 +++++++++++++++++++++++++++++++++-----------
 2 files changed, 55 insertions(+), 11 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/UI/Views/XPTrackView.cs b/Assets/_Scripts/UI/Views/XPTrackView.cs
index 1a418f1ad..061ccc35e 100644
--- a/Assets/_Scripts/UI/Views/XPTrackView.cs
+++ b/Assets/_Scripts/UI/Views/XPTrackView.cs
@@ -1,7 +1,6 @@
 using System.Collections.Generic;
 using CosmicShore.ScriptableObjects;
 using DG.Tweening;
-using Reflex.Attributes;
 using TMPro;
 using UnityEngine;
 using UnityEngine.UI;
@@ -31,24 +30,39 @@ namespace CosmicShore.UI
         [SerializeField] private float sliderAnimDuration = 1f;
         [SerializeField] private Ease sliderEase = Ease.OutCubic;
 
-        [Inject] private PlayerDataService playerDataService;
+        // Use the persistent singleton rather than [Inject]: reliably available from
+        // bootstrap and immune to DI-injection timing (OnEnable fires before [Inject]
+        // fields are populated) and null injection.
+        PlayerDataService Service => PlayerDataService.Instance;
 
         private readonly List<GameObject> _spawnedItems = new();
         private readonly List<GameObject> _spawnedLevels = new();
         private Tween _sliderTween;
+        private bool _loaded;
 
         void Start()
         {
-            if (playerDataService != null)
-                playerDataService.OnProfileChanged += OnProfileChanged;
-
             LoadTrack();
+            _loaded = true;
+        }
+
+        void OnEnable()
+        {
+            var service = Service;
+            if (service != null)
+                service.OnProfileChanged += OnProfileChanged;
+
+            // On re-show (already built once), refresh from the live profile. On first
+            // enable _loaded is false and Start() does the initial load instead.
+            if (_loaded)
+                UpdateXPDisplay(Service != null ? Service.GetXP() : 0);
         }
 
         void OnDisable()
         {
-            if (playerDataService != null)
-                playerDataService.OnProfileChanged -= OnProfileChanged;
+            var service = Service;
+            if (service != null)
+                service.OnProfileChanged -= OnProfileChanged;
 
             KillTween();
         }
@@ -60,8 +74,17 @@ namespace CosmicShore.UI
 
         public void LoadTrack()
         {
+            // The authored slider may use a milestone-based range (e.g. 0..7); drive it as a
+            // normalized 0..1 progress bar so GetNormalized() maps directly to the fill.
+            if (xpSlider != null)
+            {
+                xpSlider.wholeNumbers = false;
+                xpSlider.minValue = 0f;
+                xpSlider.maxValue = 1f;
+            }
+
             SpawnMilestones();
-            int currentXP = playerDataService != null ? playerDataService.GetXP() : 0;
+            int currentXP = Service != null ? Service.GetXP() : 0;
             SetXPImmediate(currentXP);
         }
 
@@ -73,9 +96,8 @@ namespace CosmicShore.UI
 
             int totalMilestones = xpTrackData.milestones.Count;
             int xpPerMilestone = xpTrackData.xpPerMilestone;
-            bool hasProfile = playerDataService != null &&
-                              playerDataService.CurrentProfile != null;
-            int currentXP = hasProfile ? playerDataService.GetXP() : 0;
+            bool hasProfile = Service != null && Service.CurrentProfile != null;
+            int currentXP = hasProfile ? Service.GetXP() : 0;
 
             for (int i = 0; i < totalMilestones; i++)
             {
```

</details>
