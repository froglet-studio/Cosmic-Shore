# Branch archive: `claude/fix-vessel-ui-bleed-67g3B`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-04-09 by Claude
- **Unmerged commits:** 1
- **Forked from:** `2606ca6a5` (2026-04-08, Merge pull request #470 from froglet-studio/claude/fix-modal-state-return-OwxV)
- **Tip:** `018119450`
- **Files touched (4):**
  - `Assets/_Scripts/Controller/Vessel/VesselHUD.cs`
  - `Assets/_Scripts/UI/MenuMiniGameHUD.cs`
  - `Assets/_Scripts/UI/MiniGameHUD.cs`
  - `Assets/_Scripts/UI/View/VesselHUDView.cs`

### `018119450` — fix(ui): prevent vessel HUD from briefly bleeding through menu UI

_Claude, 2026-04-09 17:59:03 +0000_

```text
Three root causes of vessel HUD elements flashing over menu/modal UI:

1. VesselHUDView had no initial visibility state — its CanvasGroup
   defaulted to alpha 1.0, making the HUD visible from the moment the
   vessel was instantiated until HideHUD()'s 0.2s fade tween completed.
   Fix: add Awake() that sets alpha=0, interactable=false, blocksRaycasts=false.

2. HUD reparenting used GetComponentsInChildren<Transform> which flattened
   the entire hierarchy, extracting VesselHUDView's children as siblings
   under "Game UI". When HideHUD() deactivated the VesselHUDView GO,
   those orphaned former children stayed active and could bleed through
   during canvas transitions. Fix: iterate only direct children so nested
   hierarchies stay intact and HideHUD() properly hides all descendants.

3. The vessel's embedded MiniGameHUD was left active after reparenting,
   allowing its Start() to fire, subscribe to SOAP events, and call
   Show() — making the HUD visible on the vessel's own Canvas.
   Fix: deactivate the embedded MiniGameHUD after raising the SOAP event
   and also search with includeInactive=true so prefabs can ship disabled.
```

```text
 Assets/_Scripts/Controller/Vessel/VesselHUD.cs | 29 ++++++++++++-----------------
 Assets/_Scripts/UI/MenuMiniGameHUD.cs          |  7 +++++--
 Assets/_Scripts/UI/MiniGameHUD.cs              |  7 +++++--
 Assets/_Scripts/UI/View/VesselHUDView.cs       | 12 ++++++++++++
 4 files changed, 34 insertions(+), 21 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/VesselHUD.cs b/Assets/_Scripts/Controller/Vessel/VesselHUD.cs
index bd9e916c9..b753fc771 100644
--- a/Assets/_Scripts/Controller/Vessel/VesselHUD.cs
+++ b/Assets/_Scripts/Controller/Vessel/VesselHUD.cs
@@ -15,32 +15,27 @@ namespace CosmicShore.Gameplay
 
         void Start()
         {
-
-            var shipHUD = GetComponentInChildren<MiniGameHUD>();
+            // Search includes inactive so the prefab can ship with the
+            // MiniGameHUD disabled, avoiding its Start() lifecycle.
+            var shipHUD = GetComponentInChildren<MiniGameHUD>(true);
 
             if (shipHUD == null)
-            {
                 return;
-            }
-            // TODO - Remove GameCanvas dependency
-
-            /*if (vessel.VesselStatus.Player.GameCanvas != null)
-            {
-                // Disable the default HUD
-                vessel.VesselStatus.Player.GameCanvas.MiniGameHUD.gameObject.SetActive(false);
-
-                // Enable the modified HUD in the child
-                shipHUD.gameObject.SetActive(true);
-
-                // Assign the modified HUD to the vessel's player
-                vessel.VesselStatus.Player.GameCanvas.MiniGameHUD = shipHUD;
-            }*/
 
+            // Temporarily activate so the scene-level handler
+            // (MenuMiniGameHUD / MiniGameHUD) can discover and reparent
+            // the direct children via the SOAP event.
             shipHUD.gameObject.SetActive(true);
             onShipHUDInitialized.Raise(new ShipHUDData()
             {
                 ShipHUD = shipHUD
             });
+
+            // Deactivate the vessel's embedded MiniGameHUD now that its
+            // children have been reparented. This prevents its Start()
+            // from firing (which would subscribe to SOAP events and call
+            // Show(), briefly making the HUD visible on the vessel's Canvas).
+            shipHUD.gameObject.SetActive(false);
         }
     }
 }
diff --git a/Assets/_Scripts/UI/MenuMiniGameHUD.cs b/Assets/_Scripts/UI/MenuMiniGameHUD.cs
index 6623105db..f05a6537c 100644
--- a/Assets/_Scripts/UI/MenuMiniGameHUD.cs
+++ b/Assets/_Scripts/UI/MenuMiniGameHUD.cs
@@ -141,9 +141,12 @@ namespace CosmicShore.UI
 
             Hide();
 
-            foreach (Transform child in data.ShipHUD.GetComponentsInChildren<Transform>(false))
+            // Move only direct children so nested hierarchies (e.g. VesselHUDView
+            // with its own child elements) stay intact. This ensures HideHUD() on the
+            // VesselHUDView properly hides all its descendants.
+            for (int i = data.ShipHUD.transform.childCount - 1; i >= 0; i--)
             {
-                if (child == data.ShipHUD.transform) continue;
+                var child = data.ShipHUD.transform.GetChild(i);
                 child.SetParent(transform.parent, false);
                 child.SetSiblingIndex(0);
             }
diff --git a/Assets/_Scripts/UI/MiniGameHUD.cs b/Assets/_Scripts/UI/MiniGameHUD.cs
index e32a98864..aa0ca5cf7 100644
--- a/Assets/_Scripts/UI/MiniGameHUD.cs
+++ b/Assets/_Scripts/UI/MiniGameHUD.cs
@@ -442,9 +442,12 @@ namespace CosmicShore.UI
 
             Hide();
 
-            foreach (Transform child in data.ShipHUD.GetComponentsInChildren<Transform>(false))
+            // Move only direct children so nested hierarchies (e.g. VesselHUDView
+            // with its own child elements) stay intact. This ensures HideHUD() on the
+            // VesselHUDView properly hides all its descendants.
+            for (int i = data.ShipHUD.transform.childCount - 1; i >= 0; i--)
             {
-                if (child == data.ShipHUD.transform) continue;
+                var child = data.ShipHUD.transform.GetChild(i);
                 child.SetParent(transform.parent, false);
                 child.SetSiblingIndex(0);
             }
diff --git a/Assets/_Scripts/UI/View/VesselHUDView.cs b/Assets/_Scripts/UI/View/VesselHUDView.cs
index 156b2771d..1904103f1 100644
--- a/Assets/_Scripts/UI/View/VesselHUDView.cs
+++ b/Assets/_Scripts/UI/View/VesselHUDView.cs
@@ -25,6 +25,18 @@ namespace CosmicShore.UI
         private CanvasGroup _canvasGroup;
         private Tween _fadeTween;
 
+        /// <summary>
+        /// Ensures the HUD starts fully hidden so it never flashes on screen
+        /// before an explicit <see cref="Show"/> call.
+        /// </summary>
+        protected virtual void Awake()
+        {
+            EnsureCanvasGroup();
+            _canvasGroup.alpha = 0f;
+            _canvasGroup.interactable = false;
+            _canvasGroup.blocksRaycasts = false;
+        }
+
         public abstract void Initialize();
 
         internal GameObject TrailBlockPrefab;
```

</details>
