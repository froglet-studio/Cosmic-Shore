# Branch archive: `claude/fix-duel-cell-screen-urdHA`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-04-06 by Claude
- **Unmerged commits:** 5
- **Forked from:** `cf550f823` (2026-04-05, Merge pull request #468 from froglet-studio/claude/update-hex-race-docs-OdKEU)
- **Tip:** `c5c06eca9`
- **Files touched (4):**
  - `Assets/_Prefabs/ArcadeGameConfigureModal.prefab`
  - `Assets/_Scenes/Menu_Main.unity`
  - `Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs`
  - `Assets/_Scripts/UI/Modals/ModalWindowManager.cs`

### `4e0ddb3d3` — fix(ui): remove legacy UI.prefab and fix phantom Duel for Cell modal

_Claude, 2026-04-06 19:40:12 +0000_

```text
The "Duel for the Cell" configuration screen was appearing at
inappropriate times (e.g. when clicking Hex Race options or returning
to Main Menu) due to two compounding issues:

1. A legacy UI.prefab from "_Prefabs/MIgration_Prefabs (DELETE LATER)/"
   was still instantiated in Menu_Main.unity with zero scale (invisible
   but scripts still running). This created duplicate ScreenSwitcher,
   ArcadeExploreView, and ArcadeGameConfigureModal instances that
   interfered with navigation state and the singleton race condition.

2. The ArcadeGameConfigureModal's CanvasGroup was serialized with
   alpha=1/blocksRaycasts=1, so whenever its parent container activated,
   the modal appeared immediately with stale config data (CellularDuel
   from the legacy LaunchPartyAllGames list).

Changes:
- Remove the legacy UI.prefab instance from Menu_Main.unity scene
- Set ArcadeGameConfigureModal CanvasGroup to start hidden (alpha=0,
  blocksRaycasts=0) in both the scene and the prefab asset
- Add safety in ModalWindowManager.Start() to ensure all modals begin
  with their CanvasGroup hidden, preventing stale serialized values
  from causing modal flash-on-load
```

```text
 Assets/_Prefabs/ArcadeGameConfigureModal.prefab |     6 +-
 Assets/_Scenes/Menu_Main.unity                  | 14598 +++++++++-------------------------------------------
 Assets/_Scripts/UI/Modals/ModalWindowManager.cs |     5 +
 3 files changed, 2346 insertions(+), 12263 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/UI/Modals/ModalWindowManager.cs b/Assets/_Scripts/UI/Modals/ModalWindowManager.cs
index 77e4e450a..4457b0c08 100644
--- a/Assets/_Scripts/UI/Modals/ModalWindowManager.cs
+++ b/Assets/_Scripts/UI/Modals/ModalWindowManager.cs
@@ -36,6 +36,11 @@ namespace CosmicShore.UI
                 windowAnimator = GetComponent<Animator>();
 
             _screenSwitcher = FindAnyObjectByType<ScreenSwitcher>();
+
+            // Ensure modals start hidden — scene-serialized CanvasGroup values
+            // may be stale (e.g. alpha=1) which causes modals to flash on load.
+            if (!isOn)
+                SetCanvasGroupVisible(false);
         }
 
         protected virtual void Update()
```

</details>

### `9cf24c33a` — fix(scene): properly remove legacy UI.prefab from Menu_Main

_Claude, 2026-04-06 19:57:04 +0000_

```text
The previous removal script was too aggressive — it deleted ~12,000
lines including scene-level objects that merely referenced the prefab
instance's fileIDs, breaking all UI in the scene.

This redo removes only the 39 YAML document blocks that actually
belong to the legacy UI.prefab PrefabInstance (stripped objects and
the PrefabInstance block itself), nullifies 20 dangling references
to {fileID: 0}, and preserves the CanvasGroup fix (alpha=0,
blocksRaycasts=0) on the scene-level ArcadeGameConfigureModal.
```

```text
 Assets/_Scenes/Menu_Main.unity | 10146 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++------
 1 file changed, 9309 insertions(+), 837 deletions(-)
```

### `204959a64` — fix(scene): revert prefab removal, keep only CanvasGroup fix

_Claude, 2026-04-06 20:21:24 +0000_

```text
The legacy UI.prefab removal broke the scene because scene-level
objects were reparented under the prefab's RectTransforms. Removing
the prefab and nullifying those parent references severed UI objects
from the Canvas hierarchy, causing no UI to render.

Reverted to the original scene and applied only the safe change:
set ArcadeGameConfigureModal CanvasGroup to alpha=0, blocksRaycasts=0,
interactable=0 so it starts hidden. Combined with the
ModalWindowManager.Start() code fix, this prevents the phantom modal
from appearing without touching the scene hierarchy.

The legacy UI.prefab (zero-scale, scripts still running) remains in
the scene for now — removing it safely requires manual work in the
Unity Editor to reparent its child objects first.
```

```text
 Assets/_Scenes/Menu_Main.unity | 2422 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++--------------
 1 file changed, 1936 insertions(+), 486 deletions(-)
```

### `b646773af` — revert: undo all scene/prefab/code changes that broke UI

_Claude, 2026-04-06 20:50:56 +0000_

```text
Reverts all visual changes from previous commits:
- Menu_Main.unity scene: fully restored to original
- ArcadeGameConfigureModal.prefab: CanvasGroup alpha restored to 1
- ModalWindowManager.cs: removed Start() hide-all-modals logic

The working UI comes from the legacy UI.prefab (Screen Space Overlay
Canvas at zero root scale — zero scale is irrelevant for overlay
canvases). The scene-level UI root is completely inactive. All
previous fixes were targeting the wrong ArcadeGameConfigureModal
instance.
```

```text
 Assets/_Prefabs/ArcadeGameConfigureModal.prefab | 6 +++---
 Assets/_Scenes/Menu_Main.unity                  | 6 +++---
 Assets/_Scripts/UI/Modals/ModalWindowManager.cs | 5 -----
 3 files changed, 6 insertions(+), 11 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/UI/Modals/ModalWindowManager.cs b/Assets/_Scripts/UI/Modals/ModalWindowManager.cs
index 4457b0c08..77e4e450a 100644
--- a/Assets/_Scripts/UI/Modals/ModalWindowManager.cs
+++ b/Assets/_Scripts/UI/Modals/ModalWindowManager.cs
@@ -36,11 +36,6 @@ namespace CosmicShore.UI
                 windowAnimator = GetComponent<Animator>();
 
             _screenSwitcher = FindAnyObjectByType<ScreenSwitcher>();
-
-            // Ensure modals start hidden — scene-serialized CanvasGroup values
-            // may be stale (e.g. alpha=1) which causes modals to flash on load.
-            if (!isOn)
-                SetCanvasGroupVisible(false);
         }
 
         protected virtual void Update()
```

</details>

### `c5c06eca9` — fix(ui): clear ArcadeGameConfigSO state on modal close and game launch

_Claude, 2026-04-06 20:51:47 +0000_

```text
The "Duel for the Cell" configuration screen was appearing with stale
data because ArcadeGameConfigSO.SelectedGame persisted across scene
loads. When the modal closed (via back button) or a game launched
(via start button), the config SO retained the previously-selected
game. On returning to the menu, any code path that reopened the modal
would display that stale game.

Now both CloseAndNotifyClients() and HandleAllPlayersReady() reset
_selectedGame and call config.ResetState() before closing, ensuring
no stale game selection persists across modal opens or scene
transitions.
```

```text
 Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs | 10 ++++++++++
 1 file changed, 10 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs b/Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs
index cfa82099c..0888c0295 100644
--- a/Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs
+++ b/Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs
@@ -857,6 +857,12 @@ namespace CosmicShore.UI
                 arcadeConfigSyncManager.NotifyConfigClosed();
 
             _isClientMode = false;
+
+            // Clear stale state so the modal never reopens showing a
+            // previously-selected game (e.g. after returning from a game scene).
+            _selectedGame = null;
+            if (config) config.ResetState();
+
             ModalWindowOut();
         }
 
@@ -911,6 +917,10 @@ namespace CosmicShore.UI
                 gameData.InvokeGameLaunch();
             }
 
+            // Clear runtime state so it can't resurface after returning to menu
+            _selectedGame = null;
+            if (config) config.ResetState();
+
             // Close the modal on all instances
             ModalWindowOut();
         }
```

</details>
