# Branch archive: `claude/optimize-menu-performance-5EODy`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-08 by Claude
- **Unmerged commits:** 1
- **Forked from:** `14a193123` (2026-03-07, Merge pull request #414 from froglet-studio/claude/migrate-debug-logs-fNeXt)
- **Tip:** `3316fcb70`
- **Files touched (5):**
  - `Assets/_Scripts/App/UI/InfiniteScroll.cs`
  - `Assets/_Scripts/App/UI/Modals/DailyChallengeModal.cs`
  - `Assets/_Scripts/App/UI/Modals/ModalWindowManager.cs`
  - `Assets/_Scripts/App/UI/Screens/HangarScreen.cs`
  - `Assets/_Scripts/App/UI/Views/QuestTrackView.cs`

### `3316fcb70` — Optimize menu UI performance: reduce per-frame waste and navigation spikes

_Claude, 2026-03-08 02:31:02 +0000_

```text
- Throttle DailyChallengeModal timer to 1Hz (was recalculating DateTime + formatting string every frame)
- Cache RectTransform/CanvasGroup refs in QuestTrackView parallax (eliminates N×2 GetComponent calls per frame)
- Gate parallax updates to only run when scroll is active (skip when idle)
- Pool QuestTrackView cards across enable/disable cycles (avoid repeated Instantiate/Destroy)
- Cache ScreenSwitcher reference in ModalWindowManager (removes FindAnyObjectByType on every modal open/close)
- Replace Canvas.ForceUpdateCanvases() with targeted LayoutRebuilder in InfiniteScroll
- Reuse HangarScreen grid cards instead of destroy/recreate on refresh
```

```text
 Assets/_Scripts/App/UI/InfiniteScroll.cs             |  2 +-
 Assets/_Scripts/App/UI/Modals/DailyChallengeModal.cs |  9 +++++++-
 Assets/_Scripts/App/UI/Modals/ModalWindowManager.cs  | 12 +++++-----
 Assets/_Scripts/App/UI/Screens/HangarScreen.cs       | 34 +++++++++++++++++++---------
 Assets/_Scripts/App/UI/Views/QuestTrackView.cs       | 61 ++++++++++++++++++++++++++++++++++++++------------
 5 files changed, 86 insertions(+), 32 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 282 lines)</summary>

```diff
diff --git a/Assets/_Scripts/App/UI/InfiniteScroll.cs b/Assets/_Scripts/App/UI/InfiniteScroll.cs
index 5f4dd337b..53e513553 100644
--- a/Assets/_Scripts/App/UI/InfiniteScroll.cs
+++ b/Assets/_Scripts/App/UI/InfiniteScroll.cs
@@ -108,7 +108,7 @@ namespace CosmicShore.App.UI
                     snapPosition,
                     contentPanelTransform.localPosition.z);
 
-                Canvas.ForceUpdateCanvases();
+                LayoutRebuilder.ForceRebuildLayoutImmediate(contentPanelTransform);
                 checkForSnap = false;
             }
         }
diff --git a/Assets/_Scripts/App/UI/Modals/DailyChallengeModal.cs b/Assets/_Scripts/App/UI/Modals/DailyChallengeModal.cs
index e02046aed..81566d646 100644
--- a/Assets/_Scripts/App/UI/Modals/DailyChallengeModal.cs
+++ b/Assets/_Scripts/App/UI/Modals/DailyChallengeModal.cs
@@ -15,6 +15,7 @@ namespace CosmicShore.App.UI.Modals
         [SerializeField] TMP_Text TimeRemaining;
         [SerializeField] TMP_Text TicketBalance;
         GameModes GameMode;
+        float _timerAccumulator;
 
         void OnEnable()
         {
@@ -41,8 +42,14 @@ namespace CosmicShore.App.UI.Modals
             TicketBalance.text = CatalogManager.Instance.GetDailyChallengeTicketBalance().ToString();
         }
 
-        void Update()
+        protected override void Update()
         {
+            base.Update();
+
+            _timerAccumulator += Time.unscaledDeltaTime;
+            if (_timerAccumulator < 1f) return;
+            _timerAccumulator = 0f;
+
             if (GameMode == GameModes.Random)
                 AssignGameMode();
 
diff --git a/Assets/_Scripts/App/UI/Modals/ModalWindowManager.cs b/Assets/_Scripts/App/UI/Modals/ModalWindowManager.cs
index 387dc2948..fbc1d977a 100644
--- a/Assets/_Scripts/App/UI/Modals/ModalWindowManager.cs
+++ b/Assets/_Scripts/App/UI/Modals/ModalWindowManager.cs
@@ -19,11 +19,13 @@ namespace CosmicShore.App.UI.Modals
 
         [SerializeField] Animator windowAnimator;
         bool isOn;
+        ScreenSwitcher _cachedScreenSwitcher;
 
         protected virtual void Start()
         {
             if(windowAnimator == null)
                 windowAnimator = GetComponent<Animator>();
+            _cachedScreenSwitcher = FindAnyObjectByType<ScreenSwitcher>();
         }
 
         protected virtual void Update()
@@ -41,9 +43,8 @@ namespace CosmicShore.App.UI.Modals
 
             if (isOn == false)
             {
-                var screenSwitcher = FindAnyObjectByType<ScreenSwitcher>();
-                if (screenSwitcher != null)
-                    screenSwitcher.PushModal(ModalType);
+                if (_cachedScreenSwitcher != null)
+                    _cachedScreenSwitcher.PushModal(ModalType);
 
                 if (sharpAnimations == false)
                     windowAnimator.CrossFade("Window In", 0.1f);
@@ -59,9 +60,8 @@ namespace CosmicShore.App.UI.Modals
         {
             if (isOn)
             {
-                var screenSwitcher = FindAnyObjectByType<ScreenSwitcher>();
-                if (screenSwitcher)
-                    screenSwitcher.PopModal();
+                if (_cachedScreenSwitcher != null)
+                    _cachedScreenSwitcher.PopModal();
 
                 if (sharpAnimations == false)
                     windowAnimator.CrossFade("Window Out", 0.1f);
diff --git a/Assets/_Scripts/App/UI/Screens/HangarScreen.cs b/Assets/_Scripts/App/UI/Screens/HangarScreen.cs
index a42f91d37..e6ed4e553 100644
--- a/Assets/_Scripts/App/UI/Screens/HangarScreen.cs
+++ b/Assets/_Scripts/App/UI/Screens/HangarScreen.cs
@@ -92,20 +92,34 @@ namespace CosmicShore.App.UI.Screens
 
         void PopulateGrid()
         {
-            _gridCards.Clear();
-            for (int i = gridContainer.childCount - 1; i >= 0; i--)
-                Destroy(gridContainer.GetChild(i).gameObject);
-
             // Sort: unlocked vessels first, then locked
             var sorted = Ships.OrderBy(s => s.IsLocked ? 1 : 0).ToList();
 
-            foreach (var ship in sorted)
+            // Remove excess cards
+            while (_gridCards.Count > sorted.Count)
             {
-                var card = Instantiate(gridCardPrefab, gridContainer);
-                card.name = $"GridCard_{ship.Name}";
-                card.Configure(ship, this);
-                card.SetNameVisible(_namesVisible);
-                _gridCards.Add(card);
+                var last = _gridCards[^1];
+                _gridCards.RemoveAt(_gridCards.Count - 1);
+                Destroy(last.gameObject);
+            }
+
+            // Reuse existing cards and create new ones as needed
+            for (int i = 0; i < sorted.Count; i++)
+            {
+                if (i < _gridCards.Count)
+                {
+                    _gridCards[i].name = $"GridCard_{sorted[i].Name}";
+                    _gridCards[i].Configure(sorted[i], this);
+                    _gridCards[i].SetNameVisible(_namesVisible);
+                }
+                else
+                {
+                    var card = Instantiate(gridCardPrefab, gridContainer);
+                    card.name = $"GridCard_{sorted[i].Name}";
+                    card.Configure(sorted[i], this);
+                    card.SetNameVisible(_namesVisible);
+                    _gridCards.Add(card);
+                }
             }
         }
 
diff --git a/Assets/_Scripts/App/UI/Views/QuestTrackView.cs b/Assets/_Scripts/App/UI/Views/QuestTrackView.cs
index 5071fbac0..0e835af02 100644
--- a/Assets/_Scripts/App/UI/Views/QuestTrackView.cs
+++ b/Assets/_Scripts/App/UI/Views/QuestTrackView.cs
@@ -50,7 +50,10 @@ namespace CosmicShore.App.UI.Views
         [SerializeField] private float parallaxFalloff = 400f;
 
         private readonly List<QuestItemCard> _cards = new();
+        private readonly List<RectTransform> _cardRects = new();
+        private readonly List<CanvasGroup> _cardCanvasGroups = new();
         private readonly List<CanvasGroup> _descriptionLabels = new();
+        private RectTransform _viewportRect;
         private Tween _sliderTween;
         private Tween _ghostSliderTween;
         private Tween _descFadeTween;
@@ -58,14 +61,22 @@ namespace CosmicShore.App.UI.Views
         private Tween _snapTween;
         private bool _wasMoving;
```

</details>
