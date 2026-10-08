# Branch archive: `claude/polish-ui-gdc-launch-SoMFm`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-03-05 by Claude
- **Unmerged commits:** 7
- **Open pull request:** https://github.com/froglet-studio/Cosmic-Shore/pull/379
- **Forked from:** `edac93aae` (2026-03-05, Update SquirrelSkimmerImpactorDataContainer.asset)
- **Tip:** `2304618e7`
- **Files touched (2229):**
  - `"Assets/_Graphics/UI/Profile/\342\200\224Pngtree\342\200\224empty comic chat bubble retro_6866362.png"`
  - `"Assets/_Graphics/UI/Profile/\342\200\224Pngtree\342\200\224empty comic chat bubble retro_6866362.png.meta"`
  - `Assets/ArcadeDPadNav.cs`
  - `Assets/Editor/UIPolishSetup.cs`
  - `Assets/Editor/UIPolishSetup.cs.meta`
  - `Assets/_Graphics/ARCADE.meta`
  - `Assets/_Graphics/Buttons.meta`
  - `Assets/_Graphics/CardImages.meta`
  - `Assets/_Graphics/Cards/Charge_Background.png`
  - `Assets/_Graphics/Cards/Charge_Background.png.meta`
  - `Assets/_Graphics/Cards/Charge_Background_Selected.png`
  - `Assets/_Graphics/Cards/Charge_Background_Selected.png.meta`
  - `Assets/_Graphics/Cards/Dolphin.png`
  - `Assets/_Graphics/Cards/Dolphin.png.meta`
  - `Assets/_Graphics/Cards/Dolphin_Inactive.png`
  - `Assets/_Graphics/Cards/Dolphin_Inactive.png.meta`
  - `Assets/_Graphics/Cards/Dolphin_Square.png`
  - `Assets/_Graphics/Cards/Dolphin_Square.png.meta`
  - `Assets/_Graphics/Cards/Dolphin_Square_pressed.png`
  - `Assets/_Graphics/Cards/Dolphin_Square_pressed.png.meta`
  - `Assets/_Graphics/Cards/Dolphin_Square_selected.png`
  - `Assets/_Graphics/Cards/Dolphin_Square_selected.png.meta`
  - `Assets/_Graphics/Cards/Dolphin_Square_silhouette.png`
  - `Assets/_Graphics/Cards/Dolphin_Square_silhouette.png.meta`
  - `Assets/_Graphics/Cards/Dolphin_large.png`
  - `Assets/_Graphics/Cards/Dolphin_large.png.meta`
  - `Assets/_Graphics/Cards/Dolphin_pressed.png`
  - `Assets/_Graphics/Cards/Dolphin_pressed.png.meta`
  - `Assets/_Graphics/Cards/Dolphin_silhouette.png`
  - `Assets/_Graphics/Cards/Dolphin_silhouette.png.meta`
  - `Assets/_Graphics/Cards/Grizzly.png`
  - `Assets/_Graphics/Cards/Grizzly.png.meta`
  - `Assets/_Graphics/Cards/Grizzly_pressed.png`
  - `Assets/_Graphics/Cards/Grizzly_pressed.png.meta`
  - `Assets/_Graphics/Cards/Grizzly_selected.png`
  - `Assets/_Graphics/Cards/Grizzly_selected.png.meta`
  - `Assets/_Graphics/Cards/Grizzly_silhouette.png`
  - `Assets/_Graphics/Cards/Grizzly_silhouette.png.meta`
  - `Assets/_Graphics/Cards/Manta.png`
  - `Assets/_Graphics/Cards/Manta.png.meta`
  - … and 2189 more

### `fcc8d2728` — Polish UI for GDC demo: controller nav, animations, sprite reorg

_Claude, 2026-03-05 18:30:12 +0000_

```text
- Add universal gamepad/DPad navigation system (MenuDPadNavigator,
  AutoNavSetup, SelectionHighlight) for full controller support
  across all menu screens
- Add UIButtonJuice component for scale-punch press feedback on
  any button (works with both touch and gamepad)
- Upgrade NavLink with DOTween scale bounce + Y-offset animation
  on tab activation, plus ISelectHandler for gamepad navigation
- Replace ScreenSwitcher coroutine lerp with DOTween OutCubic
  transitions, add controller auto-select on screen change
- Add NavGroup DPad L1/R1 shoulder button cycling between tabs
- Add HomeScreen entrance animation (stagger scale+fade on elements)
- Upgrade ModalWindowManager with optional DOTween entrance/exit
  (scale+fade) and controller auto-focus on modal open
- Upgrade SwitchToggle from instant jump to smooth DOLocalMove
  with color tween
- Reorganize _Graphics folder: consolidate UI sprites under UI/,
  elements under Elements/, characters under Characters/, remove
  {LEGACY} and {PLACEHOLDERS} folders, fix "Design Assests" typo
- Add 4 sprite atlases (NavBar, Buttons, HUD, Arcade)
```

```text
 Assets/_Graphics/{PLACEHOLDERS}/Rectangle 820.png.meta                | 130 ------------------------
 Assets/_Graphics/{PLACEHOLDERS}/Rectangle 840.png                     | Bin 480 -> 0 bytes
 Assets/_Graphics/{PLACEHOLDERS}/Rectangle 840.png.meta                | 130 ------------------------
 Assets/_Graphics/{PLACEHOLDERS}/Serpent_Square_PLACEHOLDER.png        | Bin 60120 -> 0 bytes
 Assets/_Graphics/{PLACEHOLDERS}/Serpent_Square_PLACEHOLDER.png.meta   | 147 ---------------------------
 .../{PLACEHOLDERS}/Serpent_Square_Silhouette_PLACEHOLDER.png          | Bin 44568 -> 0 bytes
 .../{PLACEHOLDERS}/Serpent_Square_Silhouette_PLACEHOLDER.png.meta     | 147 ---------------------------
 Assets/_Graphics/{PLACEHOLDERS}/Sparrow_Square_PLACEHOLDER.png        | Bin 74137 -> 0 bytes
 Assets/_Graphics/{PLACEHOLDERS}/Sparrow_Square_PLACEHOLDER.png.meta   | 147 ---------------------------
 .../{PLACEHOLDERS}/Sparrow_Square_Silhouette_PLACEHOLDER.png          | Bin 40959 -> 0 bytes
 .../{PLACEHOLDERS}/Sparrow_Square_Silhouette_PLACEHOLDER.png.meta     | 147 ---------------------------
 Assets/_Graphics/{PLACEHOLDERS}/Squirrel_Square_PLACEHOLDER.png       | Bin 70538 -> 0 bytes
 Assets/_Graphics/{PLACEHOLDERS}/Squirrel_Square_PLACEHOLDER.png.meta  | 147 ---------------------------
 .../{PLACEHOLDERS}/Squirrel_Square_Silhouette_PLACEHOLDER.png         | Bin 36068 -> 0 bytes
 .../{PLACEHOLDERS}/Squirrel_Square_Silhouette_PLACEHOLDER.png.meta    | 147 ---------------------------
 Assets/_Graphics/{PLACEHOLDERS}/aRealSquirrel.png                     | Bin 86734 -> 0 bytes
 Assets/_Graphics/{PLACEHOLDERS}/aRealSquirrel.png.meta                | 147 ---------------------------
 Assets/_Graphics/{PLACEHOLDERS}/aSquirrel.png                         | Bin 6532 -> 0 bytes
 Assets/_Graphics/{PLACEHOLDERS}/aSquirrel.png.meta                    | 147 ---------------------------
 Assets/_Scripts/App/UI/Elements/NavGroup.cs                           |  40 +++++++-
 Assets/_Scripts/App/UI/Elements/NavLink.cs                            |  75 +++++++++++++-
 Assets/_Scripts/App/UI/Elements/SwitchToggle.cs                       |  36 ++++++-
 Assets/_Scripts/App/UI/FX/AutoNavSetup.cs                             |  78 +++++++++++++++
 Assets/_Scripts/App/UI/FX/MenuDPadNavigator.cs                        | 126 +++++++++++++++++++++++
 Assets/_Scripts/App/UI/FX/SelectionHighlight.cs                       | 112 +++++++++++++++++++++
 Assets/_Scripts/App/UI/FX/UIButtonJuice.cs                            |  83 ++++++++++++++++
 Assets/_Scripts/App/UI/Modals/ModalWindowManager.cs                   | 122 +++++++++++++++++++++--
 Assets/_Scripts/App/UI/ScreenSwitcher.cs                              |  33 +++---
 Assets/_Scripts/App/UI/Screens/HomeScreen.cs                          |  50 +++++++++-
 2193 files changed, 900 insertions(+), 25658 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 1026 lines)</summary>

```diff
diff --git a/Assets/_Scripts/App/UI/Elements/NavGroup.cs b/Assets/_Scripts/App/UI/Elements/NavGroup.cs
index 31e7fb352..f77a99f9d 100644
--- a/Assets/_Scripts/App/UI/Elements/NavGroup.cs
+++ b/Assets/_Scripts/App/UI/Elements/NavGroup.cs
@@ -1,4 +1,5 @@
 using UnityEngine;
+using UnityEngine.InputSystem;
 using System.Collections.Generic;
 using UnityEngine.UI;
 using CosmicShore.Utility;
@@ -15,21 +16,31 @@ namespace CosmicShore.App.UI
     {
         [SerializeField] NavGroupType navGroupType;
         [SerializeField] GameObject navLinkContainer;
+
+        [Header("Controller Navigation")]
+        [Tooltip("Enable DPad left/right to cycle between nav links in this group.")]
+        [SerializeField] private bool enableDPadCycling = true;
+
         List<NavLink> navLinks = new();
+        private int _activeIndex;
 
         public void ActivateLink(NavLink linkToActivate)
         {
             int selectionIndex = 0;
             foreach (var link in navLinks)
             {
-                link.SetActive(link.Index == linkToActivate.Index);
+                bool isTarget = link.Index == linkToActivate.Index;
+                link.SetActive(isTarget);
+                if (isTarget)
+                    _activeIndex = navLinks.IndexOf(link);
+
                 switch (navGroupType)
                 {
                     case NavGroupType.SelectView:
-                        link.view.gameObject.SetActive(link.Index == linkToActivate.Index);
+                        link.view.gameObject.SetActive(isTarget);
                         break;
                     case NavGroupType.UpdateView:
-                        if (link.Index == linkToActivate.Index)
+                        if (isTarget)
                             link.view.Select(selectionIndex);
                         break;
                     default:
@@ -40,6 +51,29 @@ namespace CosmicShore.App.UI
             }
         }
 
+        void Update()
+        {
+            if (!enableDPadCycling || Gamepad.current == null || navLinks.Count == 0)
+                return;
+
+            if (Gamepad.current.leftShoulder.wasPressedThisFrame)
+                CyclePrevious();
+            if (Gamepad.current.rightShoulder.wasPressedThisFrame)
+                CycleNext();
+        }
+
+        private void CycleNext()
+        {
+            int next = (_activeIndex + 1) % navLinks.Count;
+            ActivateLink(navLinks[next]);
+        }
+
+        private void CyclePrevious()
+        {
+            int prev = (_activeIndex - 1 + navLinks.Count) % navLinks.Count;
+            ActivateLink(navLinks[prev]);
+        }
+
         void Start()
         {
             Initialize();
diff --git a/Assets/_Scripts/App/UI/Elements/NavLink.cs b/Assets/_Scripts/App/UI/Elements/NavLink.cs
index 56e503a07..86988af96 100644
--- a/Assets/_Scripts/App/UI/Elements/NavLink.cs
+++ b/Assets/_Scripts/App/UI/Elements/NavLink.cs
@@ -1,7 +1,9 @@
 using UnityEngine;
 using UnityEngine.UI;
+using UnityEngine.EventSystems;
 using System.Collections;
 using System.Collections.Generic;
+using DG.Tweening;
 using TMPro;
 using CosmicShore.App.Systems.Audio;
 using CosmicShore.App.UI.Views;
@@ -10,9 +12,9 @@ using CosmicShore.Utility;
 namespace CosmicShore.App.UI
 {
     /// <summary>
-    /// 
+    /// Nav tab with crossfade, DOTween scale/position bounce, and gamepad select support.
     /// </summary>
-    public class NavLink : MonoBehaviour
+    public class NavLink : MonoBehaviour, ISelectHandler
     {
         [SerializeField] public View view;
         [SerializeField] List<Image> activeImageElements;
@@ -24,12 +26,24 @@ namespace CosmicShore.App.UI
         [SerializeField] Vector2 activeDimensions;
         [SerializeField] Vector2 inactiveDimensions;
         [SerializeField] float crossfadeDuration = 0.15f;
+
+        [Header("Activation Animation")]
+        [SerializeField] private float bounceScale = 1.15f;
+        [SerializeField] private float bounceDuration = 0.25f;
+        [SerializeField] private float activeYOffset = 4f;
+        [SerializeField] private float positionDuration = 0.2f;
+
         [HideInInspector]public NavGroup navGroup;
         public int Index;
 
         Coroutine currentCrossfade;
         bool isActive;
 
+        private Tween _scaleTween;
+        private Tween _positionTween;
+        private Vector3 _originalLocalPosition;
+        private Vector3 _originalScale;
+
         List<Color> activeImageStartColors = new();
         List<Color> inactiveImageStartColors = new();
         List<Color> activeTextStartColors = new();
@@ -37,6 +51,9 @@ namespace CosmicShore.App.UI
 
         void Awake()
         {
+            _originalLocalPosition = transform.localPosition;
+            _originalScale = transform.localScale;
+
             if (activeImageElements.Count != inactiveImageElements.Count)
                 CSDebug.LogError($"NavLink Configuration Error: activeImageElements.Count != inactiveImageElements.Count  --- for: {gameObject.name}");
 
@@ -62,6 +79,13 @@ namespace CosmicShore.App.UI
             navGroup.ActivateLink(this);
         }
 
+        public void OnSelect(BaseEventData eventData)
+        {
+            // When selected via gamepad DPad, activate this link
+            if (!isActive)
+                OnClick();
+        }
+
         public virtual void SetActive(bool isActive)
         {
             if (this.isActive == isActive)
```

</details>

### `2f991f4f7` — Add Editor batch tool, .meta files, and sprite atlas folder GUIDs

_Claude, 2026-03-05 18:39:43 +0000_

```text
- UIPolishSetup.cs: One-click Editor menu (Tools > Cosmic Shore) to batch-attach
  UIButtonJuice, SelectionHighlight, AutoNavSetup, and MenuDPadNavigator
- Created .meta files for all 4 new FX scripts
- Created .meta files for all UI sprite subfolders (15 folders)
- Created .meta files for all 4 sprite atlases
- Fixed sprite atlas packables to reference correct folder GUIDs
```

```text
 Assets/Editor/UIPolishSetup.cs                       | 170 +++++++++++++++++++++++++++++++++++++++++++++++++
 Assets/Editor/UIPolishSetup.cs.meta                  |  11 ++++
 Assets/_Graphics/UI/Arcade.meta                      |   8 +++
 Assets/_Graphics/UI/Buttons.meta                     |   8 +++
 Assets/_Graphics/UI/ControlsPanel.meta               |   8 +++
 Assets/_Graphics/UI/EndScene.meta                    |   8 +++
 Assets/_Graphics/UI/FX.meta                          |   8 +++
 Assets/_Graphics/UI/HUD.meta                         |   8 +++
 Assets/_Graphics/UI/Hangar.meta                      |   8 +++
 Assets/_Graphics/UI/Icons.meta                       |   8 +++
 Assets/_Graphics/UI/MainMenu.meta                    |   8 +++
 Assets/_Graphics/UI/Port.meta                        |   8 +++
 Assets/_Graphics/UI/Profile.meta                     |   8 +++
 Assets/_Graphics/UI/Settings.meta                    |   8 +++
 Assets/_Graphics/UI/ShipButtons.meta                 |   8 +++
 Assets/_Graphics/UI/Store.meta                       |   8 +++
 Assets/_Graphics/UI/Transitions.meta                 |   8 +++
 Assets/_Graphics/UI/UI_Arcade.spriteatlas            |   2 +-
 Assets/_Graphics/UI/UI_Arcade.spriteatlas.meta       |   8 +++
 Assets/_Graphics/UI/UI_Buttons.spriteatlas           |   2 +-
 Assets/_Graphics/UI/UI_Buttons.spriteatlas.meta      |   8 +++
 Assets/_Graphics/UI/UI_HUD.spriteatlas               |   2 +-
 Assets/_Graphics/UI/UI_HUD.spriteatlas.meta          |   8 +++
 Assets/_Graphics/UI/UI_NavBar.spriteatlas            |   2 +-
 Assets/_Graphics/UI/UI_NavBar.spriteatlas.meta       |   8 +++
 Assets/_Scripts/App/UI/FX/AutoNavSetup.cs.meta       |  11 ++++
 Assets/_Scripts/App/UI/FX/MenuDPadNavigator.cs.meta  |  11 ++++
 Assets/_Scripts/App/UI/FX/SelectionHighlight.cs.meta |  11 ++++
 Assets/_Scripts/App/UI/FX/UIButtonJuice.cs.meta      |  11 ++++
 29 files changed, 381 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 176 lines)</summary>

```diff
diff --git a/Assets/Editor/UIPolishSetup.cs b/Assets/Editor/UIPolishSetup.cs
new file mode 100644
index 000000000..0b572dbda
--- /dev/null
+++ b/Assets/Editor/UIPolishSetup.cs
@@ -0,0 +1,170 @@
+using UnityEditor;
+using UnityEngine;
+using UnityEngine.UI;
+using CosmicShore.App.UI.FX;
+
+/// <summary>
+/// One-click editor tool to attach UI polish components across the project.
+/// Run via menu: Tools > Cosmic Shore > Setup UI Polish Components
+/// </summary>
+public static class UIPolishSetup
+{
+    [MenuItem("Tools/Cosmic Shore/Setup UI Polish Components (All)")]
+    public static void SetupAll()
+    {
+        int totalAdded = 0;
+        totalAdded += AddButtonJuiceToAllButtons();
+        totalAdded += AddSelectionHighlightToAllSelectables();
+        totalAdded += AddAutoNavSetupToScreenRoots();
+
+        Debug.Log($"[UIPolishSetup] Complete! Added {totalAdded} components total.");
+    }
+
+    [MenuItem("Tools/Cosmic Shore/1 - Add UIButtonJuice to All Buttons")]
+    public static int AddButtonJuiceToAllButtons()
+    {
+        int count = 0;
+
+        // Find all Button components in loaded scenes and prefabs
+        var buttons = Resources.FindObjectsOfTypeAll<Button>();
+        foreach (var button in buttons)
+        {
+            if (button == null) continue;
+            if (PrefabUtility.IsPartOfImmutablePrefab(button.gameObject)) continue;
+
+            if (button.GetComponent<UIButtonJuice>() == null)
+            {
+                Undo.AddComponent<UIButtonJuice>(button.gameObject);
+                EditorUtility.SetDirty(button.gameObject);
+                count++;
+            }
+        }
+
+        Debug.Log($"[UIPolishSetup] Added UIButtonJuice to {count} buttons.");
+        return count;
+    }
+
+    [MenuItem("Tools/Cosmic Shore/2 - Add SelectionHighlight to All Selectables")]
+    public static int AddSelectionHighlightToAllSelectables()
+    {
+        int count = 0;
+
+        var selectables = Resources.FindObjectsOfTypeAll<Selectable>();
+        foreach (var selectable in selectables)
+        {
+            if (selectable == null) continue;
+            if (PrefabUtility.IsPartOfImmutablePrefab(selectable.gameObject)) continue;
+
+            if (selectable.GetComponent<SelectionHighlight>() == null)
+            {
+                Undo.AddComponent<SelectionHighlight>(selectable.gameObject);
+                EditorUtility.SetDirty(selectable.gameObject);
+                count++;
+            }
+        }
+
+        Debug.Log($"[UIPolishSetup] Added SelectionHighlight to {count} selectables.");
+        return count;
+    }
+
+    [MenuItem("Tools/Cosmic Shore/3 - Add AutoNavSetup to Screen Roots")]
+    public static int AddAutoNavSetupToScreenRoots()
+    {
+        int count = 0;
+
+        // Find ScreenSwitcher to locate menu screen roots
+        var screenSwitchers = Resources.FindObjectsOfTypeAll<CosmicShore.App.UI.ScreenSwitcher>();
+        foreach (var switcher in screenSwitchers)
+        {
+            if (switcher == null) continue;
+            // Add AutoNavSetup to each direct child (each is a screen)
+            for (int i = 0; i < switcher.transform.childCount; i++)
+            {
+                var screen = switcher.transform.GetChild(i).gameObject;
+                if (screen.GetComponent<AutoNavSetup>() == null)
+                {
+                    Undo.AddComponent<AutoNavSetup>(screen);
+                    EditorUtility.SetDirty(screen);
+                    count++;
+                }
+            }
+        }
+
+        // Also add to modal roots
+        var modals = Resources.FindObjectsOfTypeAll<CosmicShore.App.UI.Modals.ModalWindowManager>();
+        foreach (var modal in modals)
+        {
+            if (modal == null) continue;
+            if (PrefabUtility.IsPartOfImmutablePrefab(modal.gameObject)) continue;
+
+            if (modal.GetComponent<AutoNavSetup>() == null)
+            {
+                Undo.AddComponent<AutoNavSetup>(modal.gameObject);
+                EditorUtility.SetDirty(modal.gameObject);
+                count++;
+            }
+        }
+
+        Debug.Log($"[UIPolishSetup] Added AutoNavSetup to {count} screen/modal roots.");
+        return count;
+    }
+
+    [MenuItem("Tools/Cosmic Shore/4 - Add MenuDPadNavigator to EventSystem")]
+    public static void AddMenuDPadNavigator()
+    {
+        var eventSystem = Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
+        if (eventSystem == null)
+        {
+            Debug.LogWarning("[UIPolishSetup] No EventSystem found in the active scene.");
+            return;
+        }
+
+        if (eventSystem.GetComponent<MenuDPadNavigator>() == null)
+        {
+            var navigator = Undo.AddComponent<MenuDPadNavigator>(eventSystem.gameObject);
+            EditorUtility.SetDirty(eventSystem.gameObject);
+
+            // Try to auto-wire ScreenSwitcher reference
+            var switcher = Object.FindObjectOfType<CosmicShore.App.UI.ScreenSwitcher>();
+            if (switcher != null)
+            {
+                var so = new SerializedObject(navigator);
+                var prop = so.FindProperty("screenSwitcher");
+                if (prop != null)
+                {
+                    prop.objectReferenceValue = switcher;
+                    so.ApplyModifiedProperties();
+                }
+            }
+
+            Debug.Log("[UIPolishSetup] Added MenuDPadNavigator to EventSystem and wired ScreenSwitcher.");
+        }
+        else
+        {
+            Debug.Log("[UIPolishSetup] MenuDPadNavigator already on EventSystem.");
```

</details>

### `0529a59cf` — Fix controller A-button hijacking and abrupt modal transitions

_Claude, 2026-03-05 19:02:11 +0000_

```text
- Remove manual buttonSouth handler from ArcadeDPadNav that was
  bypassing EventSystem and launching wrong game modes on A press
- Invert DOTween flag in ModalWindowManager so all modals use smooth
  scale+fade entrance by default (previously required opt-in per modal)
```

```text
 Assets/ArcadeDPadNav.cs                             | 24 ++++++++++--------------
 Assets/_Scripts/App/UI/Modals/ModalWindowManager.cs | 26 +++++++++++++-------------
 2 files changed, 23 insertions(+), 27 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/ArcadeDPadNav.cs b/Assets/ArcadeDPadNav.cs
index 9d7c2f5bd..2c278616a 100644
--- a/Assets/ArcadeDPadNav.cs
+++ b/Assets/ArcadeDPadNav.cs
@@ -26,9 +26,8 @@ namespace CosmicShore
 
         void Update()
         {
-            // if (ScreenSwitcher.ScreenIsActive(ScreenSwitcher.MenuScreens.ARCADE))
-            // {
-            // }
+            if (!gameObject.activeInHierarchy) return;
+
             if (!initialized)
             {
                 if (Gamepad.current != null)
@@ -37,18 +36,15 @@ namespace CosmicShore
                 }
             }
 
-            if (Gamepad.current != null)
-            {
-                if (Gamepad.current.dpad.up.wasPressedThisFrame) NavigateUp();
-                if (Gamepad.current.dpad.down.wasPressedThisFrame) NavigateDown();
-                if (Gamepad.current.dpad.left.wasPressedThisFrame) NavigateLeft();
-                if (Gamepad.current.dpad.right.wasPressedThisFrame) NavigateRight();
-            }
+            if (Gamepad.current == null) return;
 
-            if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
-            {
-                selectedButton.onClick.Invoke();
-            }
+            // Only handle DPad grid navigation — Submit (A button) is handled
+            // by Unity's EventSystem / InputSystemUIInputModule to avoid
+            // double-firing and launching the wrong game mode.
+            if (Gamepad.current.dpad.up.wasPressedThisFrame) NavigateUp();
+            if (Gamepad.current.dpad.down.wasPressedThisFrame) NavigateDown();
+            if (Gamepad.current.dpad.left.wasPressedThisFrame) NavigateLeft();
+            if (Gamepad.current.dpad.right.wasPressedThisFrame) NavigateRight();
         }
 
         public void AddRow(List<Button> row)
diff --git a/Assets/_Scripts/App/UI/Modals/ModalWindowManager.cs b/Assets/_Scripts/App/UI/Modals/ModalWindowManager.cs
index 05fcec4cf..adabe9487 100644
--- a/Assets/_Scripts/App/UI/Modals/ModalWindowManager.cs
+++ b/Assets/_Scripts/App/UI/Modals/ModalWindowManager.cs
@@ -18,9 +18,9 @@ namespace CosmicShore.App.UI.Modals
         [SerializeField] Animator windowAnimator;
         bool isOn;
 
-        [Header("DOTween Entrance (optional)")]
-        [Tooltip("Use DOTween scale+fade instead of Animator. Leave false to keep Animator behavior.")]
-        [SerializeField] private bool useDOTweenEntrance;
+        [Header("DOTween Entrance")]
+        [Tooltip("Disable to fall back to Animator-only transitions (not recommended).")]
+        [SerializeField] private bool disableDOTweenEntrance;
         [SerializeField] private float tweenDuration = 0.3f;
         [SerializeField] private float entranceStartScale = 0.85f;
         [SerializeField] private Ease entranceEase = Ease.OutBack;
@@ -58,17 +58,17 @@ namespace CosmicShore.App.UI.Modals
                 if (screenSwitcher != null)
                     screenSwitcher.PushModal(ModalType);
 
-                if (useDOTweenEntrance)
-                {
-                    PlayDOTweenIn();
-                }
-                else
+                if (disableDOTweenEntrance && windowAnimator != null)
                 {
                     if (sharpAnimations == false)
                         windowAnimator.CrossFade("Window In", 0.1f);
                     else
                         windowAnimator.Play("Window In");
                 }
+                else
+                {
+                    PlayDOTweenIn();
+                }
 
                 AudioSystem.Instance.PlayMenuAudio(MenuAudioCategory.OpenView);
                 isOn = true;
@@ -86,17 +86,17 @@ namespace CosmicShore.App.UI.Modals
                 if (screenSwitcher)
                     screenSwitcher.PopModal();
 
-                if (useDOTweenEntrance)
-                {
-                    PlayDOTweenOut();
-                }
-                else
+                if (disableDOTweenEntrance && windowAnimator != null)
                 {
                     if (sharpAnimations == false)
                         windowAnimator.CrossFade("Window Out", 0.1f);
                     else
                         windowAnimator.Play("Window Out");
                 }
+                else
+                {
+                    PlayDOTweenOut();
+                }
 
                 AudioSystem.Instance.PlayMenuAudio(MenuAudioCategory.CloseView);
                 isOn = false;
```

</details>

### `d68857434` — Add gamepad controller support to in-game UI (pause, scoreboard, end-game)

_Claude, 2026-03-05 19:09:43 +0000_

```text
- PauseMenu: Start button toggles pause, B button closes pause menu,
  auto-focus first selectable for DPad navigation, multiplayer support
- Scoreboard: auto-focus Play Again button for gamepad users when shown
- EndGameCinematicView: auto-focus Continue button for gamepad when revealed
```

```text
 Assets/_Scripts/Game/UI/PauseMenu.cs                           | 81 ++++++++++++++++++++++++++++++++++++----
 Assets/_Scripts/Game/UI/Scoreboard.cs                          | 31 +++++++++++++++
 Assets/_Scripts/Utility/DataContainers/EndGameCinematicView.cs | 12 ++++++
 3 files changed, 116 insertions(+), 8 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 205 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/UI/PauseMenu.cs b/Assets/_Scripts/Game/UI/PauseMenu.cs
index bd78eee25..bfea0d025 100644
--- a/Assets/_Scripts/Game/UI/PauseMenu.cs
+++ b/Assets/_Scripts/Game/UI/PauseMenu.cs
@@ -6,7 +6,10 @@ using CosmicShore.Core;
 using CosmicShore.Soap;
 using Cysharp.Threading.Tasks;
 using Obvious.Soap;
+using UnityEngine.EventSystems;
+using UnityEngine.InputSystem;
 using UnityEngine.Serialization;
+using UnityEngine.UI;
 
 /// <summary>
 /// Displays and controls toggles and buttons on the Pause Menu Panel
@@ -17,20 +20,27 @@ namespace CosmicShore.App.UI.Screens
 {
     public class PauseMenu : MonoBehaviour
     {
-        [SerializeField] 
+        [SerializeField]
         ScriptableEventNoParam _onClickToMainMenu;
-        
-        [SerializeField] 
+
+        [SerializeField]
         ScriptableEventNoParam _onClickToRestartButton;
-        
+
         [SerializeField]
         GameDataSO gameData;
-        
+
         [FormerlySerializedAs("canvasGroup")]
         [SerializeField] GameObject pauseMenuPanel;
         [SerializeField]
         ModalWindowManager settingsModalWindowManager;
 
+        [Header("Controller Navigation")]
+        [Tooltip("First button to select when pause menu opens with a gamepad.")]
+        [SerializeField] private Selectable firstPauseSelectable;
+
+        [Tooltip("If true, this is a multiplayer pause menu (only pauses local player input, not the game).")]
+        [SerializeField] private bool isMultiplayer;
+
         GameSetting gameSetting;
 
         /// <summary>
@@ -38,11 +48,43 @@ namespace CosmicShore.App.UI.Screens
         /// </summary>
         bool wasLocalPlayerInputPausedBefore;
 
-        //void Awake() => Hide();
-        
-        // Start is called before the first frame update
+        bool IsOpen => pauseMenuPanel != null && pauseMenuPanel.activeSelf;
+
         void Start() => gameSetting = GameSetting.Instance;
 
+        void Update()
+        {
+            if (Gamepad.current == null) return;
+
+            // Start/Menu button toggles pause
+            if (Gamepad.current.startButton.wasPressedThisFrame)
+            {
+                if (IsOpen)
+                {
+                    if (isMultiplayer)
+                        OnClickMultiplayerResumeGameButton();
+                    else
+                        OnClickResumeGameButton();
+                }
+                else
+                {
+                    if (isMultiplayer)
+                        OnClickMultiplayerPauseButton();
+                    else
+                        OnClickPauseGameButton();
+                }
+            }
+
+            // B button closes pause menu
+            if (IsOpen && Gamepad.current.buttonEast.wasPressedThisFrame)
+            {
+                if (isMultiplayer)
+                    OnClickMultiplayerResumeGameButton();
+                else
+                    OnClickResumeGameButton();
+            }
+        }
+
         /// <summary>
         /// Toggles the Master Volume On/Off
         /// </summary>
@@ -99,6 +141,29 @@ namespace CosmicShore.App.UI.Screens
             pauseMenuPanel.gameObject.SetActive(true);
             settingsModalWindowManager.ModalWindowIn();
             AudioSystem.Instance.PlayGameplaySFX(GameplaySFXCategory.PauseOpen);
+            AutoFocusForGamepad();
+        }
+
+        private void AutoFocusForGamepad()
+        {
+            if (Gamepad.current == null || EventSystem.current == null) return;
+
+            if (firstPauseSelectable != null && firstPauseSelectable.gameObject.activeInHierarchy && firstPauseSelectable.interactable)
+            {
+                EventSystem.current.SetSelectedGameObject(firstPauseSelectable.gameObject);
+                return;
+            }
+
+            // Fallback: find first interactable button in the pause panel
+            var selectables = pauseMenuPanel.GetComponentsInChildren<Selectable>(false);
+            foreach (var s in selectables)
+            {
+                if (s.interactable && s.navigation.mode != Navigation.Mode.None)
+                {
+                    EventSystem.current.SetSelectedGameObject(s.gameObject);
+                    return;
+                }
+            }
         }
 
         public void Hide()
diff --git a/Assets/_Scripts/Game/UI/Scoreboard.cs b/Assets/_Scripts/Game/UI/Scoreboard.cs
index 894c78f06..c57f353d9 100644
--- a/Assets/_Scripts/Game/UI/Scoreboard.cs
+++ b/Assets/_Scripts/Game/UI/Scoreboard.cs
@@ -8,6 +8,8 @@ using System.Collections;
 using System.Collections.Generic;
 using TMPro;
 using UnityEngine;
+using UnityEngine.EventSystems;
+using UnityEngine.InputSystem;
 using UnityEngine.UI;
 using System;
 using CosmicShore.Utility;
@@ -137,6 +139,35 @@ namespace CosmicShore.Game.UI
             {
                 scoreboardPanel.gameObject.SetActive(true);
                 PlayEntranceAnimation();
+                AutoFocusForGamepad();
+            }
+        }
+
+        private void AutoFocusForGamepad()
+        {
+            if (Gamepad.current == null || EventSystem.current == null) return;
+
```

</details>

### `0c5f29ff3` — Fix modal visibility, add gamepad skip cinematic and ready button focus

_Claude, 2026-03-05 19:14:29 +0000_

```text
- ModalWindowManager: disable Animator component when using DOTween path
  to prevent Animator from overriding scale/alpha and hiding the modal
- PreGameCinematicController: A button or Start button skips the
  pre-game cinematic flythrough
- MiniGameHUD: auto-focus Ready button for gamepad users when shown
```

```text
 Assets/_Scripts/App/UI/Modals/ModalWindowManager.cs                    |  7 +++++++
 Assets/_Scripts/Game/UI/MiniGameHUD.cs                                 | 10 +++++++++-
 Assets/_Scripts/Game/UI/PreGameCinematic/PreGameCinematicController.cs | 14 ++++++++++++++
 3 files changed, 30 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/App/UI/Modals/ModalWindowManager.cs b/Assets/_Scripts/App/UI/Modals/ModalWindowManager.cs
index adabe9487..70f966a5b 100644
--- a/Assets/_Scripts/App/UI/Modals/ModalWindowManager.cs
+++ b/Assets/_Scripts/App/UI/Modals/ModalWindowManager.cs
@@ -108,6 +108,7 @@ namespace CosmicShore.App.UI.Modals
         private void PlayDOTweenIn()
         {
             EnsureCanvasGroup();
+            DisableAnimatorIfPresent();
             _scaleTween?.Kill();
             _fadeTween?.Kill();
 
@@ -138,6 +139,12 @@ namespace CosmicShore.App.UI.Modals
                 .SetUpdate(true);
         }
 
+        private void DisableAnimatorIfPresent()
+        {
+            if (windowAnimator != null)
+                windowAnimator.enabled = false;
+        }
+
         private void AutoFocusFirstSelectable()
         {
             if (UnityEngine.InputSystem.Gamepad.current == null) return;
diff --git a/Assets/_Scripts/Game/UI/MiniGameHUD.cs b/Assets/_Scripts/Game/UI/MiniGameHUD.cs
index d493b4073..4452bb5e1 100644
--- a/Assets/_Scripts/Game/UI/MiniGameHUD.cs
+++ b/Assets/_Scripts/Game/UI/MiniGameHUD.cs
@@ -9,6 +9,8 @@ using Cysharp.Threading.Tasks;
 using Obvious.Soap;
 using TMPro;
 using UnityEngine;
+using UnityEngine.EventSystems;
+using UnityEngine.InputSystem;
 using UnityEngine.UI;
 
 namespace CosmicShore.Game.UI
@@ -473,7 +475,13 @@ namespace CosmicShore.Game.UI
 
         public void Show() => view.ToggleView(true);
         public void Hide() => view.ToggleView(false);
-        public void ToggleReadyButton(bool toggle) => view.ReadyButton.gameObject.SetActive(toggle);
+        public void ToggleReadyButton(bool toggle)
+        {
+            view.ReadyButton.gameObject.SetActive(toggle);
+
+            if (toggle && Gamepad.current != null && EventSystem.current != null)
+                EventSystem.current.SetSelectedGameObject(view.ReadyButton.gameObject);
+        }
 
         /// <summary>
         /// Shows the connecting panel flow (connecting → wait → ready button).
diff --git a/Assets/_Scripts/Game/UI/PreGameCinematic/PreGameCinematicController.cs b/Assets/_Scripts/Game/UI/PreGameCinematic/PreGameCinematicController.cs
index 12fbeda6c..7677742bd 100644
--- a/Assets/_Scripts/Game/UI/PreGameCinematic/PreGameCinematicController.cs
+++ b/Assets/_Scripts/Game/UI/PreGameCinematic/PreGameCinematicController.cs
@@ -2,6 +2,7 @@ using System;
 using System.Collections;
 using CosmicShore.Game.CameraSystem;
 using UnityEngine;
+using UnityEngine.InputSystem;
 using UnityEngine.UI;
 using CosmicShore.Utility;
 
@@ -63,6 +64,19 @@ namespace CosmicShore.Game.UI
             SetSkipButtonVisible(false);
         }
 
+        private void Update()
+        {
+            if (!_isPlaying) return;
+            if (Gamepad.current == null) return;
+
+            // A button or Start button skips the cinematic
+            if (Gamepad.current.buttonSouth.wasPressedThisFrame ||
+                Gamepad.current.startButton.wasPressedThisFrame)
+            {
+                Skip();
+            }
+        }
+
         private void OnDestroy()
         {
             if (skipButton != null)
```

</details>

### `1ecb93139` — Hide vessel arrows for single-vessel modes, add tip system for connecting panel

_Claude, 2026-03-05 19:28:22 +0000_

```text
- ArcadeGameConfigureModal: hide next/prev vessel buttons when only one
  vessel is available for the selected game mode
- SO_GameModeTips: new ScriptableObject for per-game-mode tips with
  optional common tips merge (CreateAssetMenu: CosmicShore/UI/GameModeTips)
- SO_ArcadeGame: added Tips field reference
- ConnectingPanel: displays a random tip from the active SO_GameModeTips
  on the connecting panel TMP_Text (add text element in scene)
- MiniGameHUD: pushes tips from ArcadeGameConfigSO to ConnectingPanel
  before showing the panel
- MiniGameHUDView: exposed ConnectingPanel property
```

```text
 Assets/_Scripts/App/UI/Modals/ArcadeGameConfigureModal.cs   |  9 +++++++++
 Assets/_Scripts/Game/UI/ConnectingPanel.cs                  | 39 +++++++++++++++++++++++++++++++++------
 Assets/_Scripts/Game/UI/MiniGameHUD.cs                      | 14 ++++++++++++++
 Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs         |  1 +
 Assets/_Scripts/Models/ScriptableObjects/SO_ArcadeGame.cs   |  4 ++++
 Assets/_Scripts/Models/ScriptableObjects/SO_GameModeTips.cs | 26 ++++++++++++++++++++++++++
 6 files changed, 87 insertions(+), 6 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 197 lines)</summary>

```diff
diff --git a/Assets/_Scripts/App/UI/Modals/ArcadeGameConfigureModal.cs b/Assets/_Scripts/App/UI/Modals/ArcadeGameConfigureModal.cs
index 9c0bfa8e7..8562d0751 100644
--- a/Assets/_Scripts/App/UI/Modals/ArcadeGameConfigureModal.cs
+++ b/Assets/_Scripts/App/UI/Modals/ArcadeGameConfigureModal.cs
@@ -55,6 +55,11 @@ namespace CosmicShore.App.UI.Modals
         [SerializeField] private TMP_Text shipConfigurationText;
         [SerializeField] private TMP_Text shipVesselNameText;
 
+        [Header("Vessel Navigation Arrows")]
+        [Tooltip("Next/prev vessel buttons — hidden when only one vessel is available.")]
+        [SerializeField] private GameObject nextShipButton;
+        [SerializeField] private GameObject previousShipButton;
+
         [Tooltip("Optional secondary icon (e.g. config screen).")]
         [SerializeField] private Image iconInConfigurationSelectionView;
 
@@ -231,6 +236,10 @@ namespace CosmicShore.App.UI.Modals
             if (!game || game.Vessels == null) return;
 
             _availableShips.AddRange(game.Vessels.Where(s => s != null && !s.IsLocked));
+
+            bool showArrows = _availableShips.Count > 1;
+            if (nextShipButton) nextShipButton.SetActive(showArrows);
+            if (previousShipButton) previousShipButton.SetActive(showArrows);
         }
         
         void InitializeDefaultShipFromAvailable()
diff --git a/Assets/_Scripts/Game/UI/ConnectingPanel.cs b/Assets/_Scripts/Game/UI/ConnectingPanel.cs
index 641ab6931..c7bc5d2f9 100644
--- a/Assets/_Scripts/Game/UI/ConnectingPanel.cs
+++ b/Assets/_Scripts/Game/UI/ConnectingPanel.cs
@@ -1,10 +1,11 @@
+using TMPro;
 using UnityEngine;
 using UnityEngine.UI;
 
 namespace CosmicShore.Game.UI
 {
     /// <summary>
-    /// Displays a random sprite from a configured SO each time the panel is enabled.
+    /// Displays a random sprite and tip text each time the panel is enabled.
     /// Attach to the connecting panel GameObject inside MiniGameHUD.
     /// </summary>
     public class ConnectingPanel : MonoBehaviour
@@ -12,17 +13,43 @@ namespace CosmicShore.Game.UI
         [SerializeField] private SO_ConnectingPanelSpriteList spriteList;
         [SerializeField] private Image displayImage;
 
+        [Header("Tips")]
+        [Tooltip("Tip text element on the connecting panel (add in scene).")]
+        [SerializeField] private TMP_Text tipText;
+
+        private SO_GameModeTips _activeTips;
+
+        /// <summary>
+        /// Set the tips SO for the current game mode before the panel is enabled.
+        /// Called by MiniGameHUD when the connecting panel is shown.
+        /// </summary>
+        public void SetTips(SO_GameModeTips tips)
+        {
+            _activeTips = tips;
+        }
+
         private void OnEnable()
         {
             if (displayImage == null)
                 displayImage = GetComponentInChildren<Image>();
 
-            if (spriteList == null || displayImage == null)
-                return;
+            if (spriteList != null && displayImage != null)
+            {
+                var sprite = spriteList.GetRandomSprite();
+                if (sprite != null)
+                    displayImage.sprite = sprite;
+            }
 
-            var sprite = spriteList.GetRandomSprite();
-            if (sprite != null)
-                displayImage.sprite = sprite;
+            if (tipText != null && _activeTips != null)
+            {
+                var tip = _activeTips.GetRandomTip();
+                tipText.text = string.IsNullOrEmpty(tip) ? string.Empty : tip;
+                tipText.gameObject.SetActive(!string.IsNullOrEmpty(tip));
+            }
+            else if (tipText != null)
+            {
+                tipText.gameObject.SetActive(false);
+            }
         }
     }
 }
diff --git a/Assets/_Scripts/Game/UI/MiniGameHUD.cs b/Assets/_Scripts/Game/UI/MiniGameHUD.cs
index 4452bb5e1..e6225d83a 100644
--- a/Assets/_Scripts/Game/UI/MiniGameHUD.cs
+++ b/Assets/_Scripts/Game/UI/MiniGameHUD.cs
@@ -36,6 +36,7 @@ namespace CosmicShore.Game.UI
 
         [Header("Intro / Connecting")]
         [SerializeField] private float minConnectingSeconds = 5f;
+        [SerializeField] private ArcadeGameConfigSO arcadeGameConfig;
 
         [Header("Pre-Game Cinematic")]
         [SerializeField] private PreGameCinematicController preGameCinematic;
@@ -133,6 +134,7 @@ namespace CosmicShore.Game.UI
 
             SubscribeToEvents();
             CleanupUI();
+            PushTipsToConnectingPanel();
         }
 
         protected virtual void OnDisable()
@@ -360,6 +362,7 @@ namespace CosmicShore.Game.UI
             UpdateLifeformCounterDisplay("0");
             view.UpdateScoreUI("0");
 
+            PushTipsToConnectingPanel();
             view.ToggleConnectingPanel(true);
             ToggleReadyButton(false);
 
@@ -491,6 +494,17 @@ namespace CosmicShore.Game.UI
         public void UpdateTurnMonitorDisplay(string message) => view.UpdateCountdownTimer(message);
         public void UpdateLifeformCounterDisplay(string message) => view.UpdateLifeFormCounter(message);
 
+        private void PushTipsToConnectingPanel()
+        {
+            if (view.ConnectingPanel == null) return;
+
+            SO_GameModeTips tips = arcadeGameConfig != null && arcadeGameConfig.SelectedGame != null
+                ? arcadeGameConfig.SelectedGame.Tips
+                : null;
+
+            view.ConnectingPanel.SetTips(tips);
+        }
+
         private void HideLocalVesselHUD()
         {
             gameData?.LocalPlayer?.Vessel?.VesselStatus?.VesselHUDController?.HideHUD();
diff --git a/Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs b/Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs
index 2b6cf9d6d..e66502dd1 100644
--- a/Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs
+++ b/Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs
@@ -42,6 +42,7 @@ namespace CosmicShore.Game.UI
 
         public Transform PlayerScoreContainer => playerScoreContainer;
         public PlayerScoreCard PlayerScoreCardPrefab => playerScoreCardPrefab;
+        public ConnectingPanel ConnectingPanel => connectingPanel;
 
         private Tween _viewFadeTween;
```

</details>

### `2304618e7` — Consolidate tip system into single SO_GameModeTips list asset

_Claude, 2026-03-05 21:11:16 +0000_

```text
Replaced per-game-mode SO references with a single SO_GameModeTips
asset that maps GameModes to tip strings with common tips merged in.
ConnectingPanel now holds the tipsList reference directly and receives
only the GameMode from MiniGameHUD. Removed Tips field from
SO_ArcadeGame and ArcadeGameConfigSO dependency from MiniGameHUD.
```

```text
 Assets/_Scripts/Game/UI/ConnectingPanel.cs                  | 17 ++++++++++-------
 Assets/_Scripts/Game/UI/MiniGameHUD.cs                      | 10 ++--------
 Assets/_Scripts/Models/ScriptableObjects/SO_ArcadeGame.cs   |  4 ----
 Assets/_Scripts/Models/ScriptableObjects/SO_GameModeTips.cs | 34 +++++++++++++++++++++++-----------
 4 files changed, 35 insertions(+), 30 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/UI/ConnectingPanel.cs b/Assets/_Scripts/Game/UI/ConnectingPanel.cs
index c7bc5d2f9..7547ce6f2 100644
--- a/Assets/_Scripts/Game/UI/ConnectingPanel.cs
+++ b/Assets/_Scripts/Game/UI/ConnectingPanel.cs
@@ -17,15 +17,18 @@ namespace CosmicShore.Game.UI
         [Tooltip("Tip text element on the connecting panel (add in scene).")]
         [SerializeField] private TMP_Text tipText;
 
-        private SO_GameModeTips _activeTips;
+        [Tooltip("Single tips list asset containing tips for all game modes.")]
+        [SerializeField] private SO_GameModeTips tipsList;
+
+        private GameModes _activeMode;
 
         /// <summary>
-        /// Set the tips SO for the current game mode before the panel is enabled.
-        /// Called by MiniGameHUD when the connecting panel is shown.
+        /// Set the current game mode before the panel is enabled so the
+        /// correct tips are shown. Called by MiniGameHUD.
         /// </summary>
-        public void SetTips(SO_GameModeTips tips)
+        public void SetGameMode(GameModes mode)
         {
-            _activeTips = tips;
+            _activeMode = mode;
         }
 
         private void OnEnable()
@@ -40,9 +43,9 @@ namespace CosmicShore.Game.UI
                     displayImage.sprite = sprite;
             }
 
-            if (tipText != null && _activeTips != null)
+            if (tipText != null && tipsList != null)
             {
-                var tip = _activeTips.GetRandomTip();
+                var tip = tipsList.GetRandomTip(_activeMode);
                 tipText.text = string.IsNullOrEmpty(tip) ? string.Empty : tip;
                 tipText.gameObject.SetActive(!string.IsNullOrEmpty(tip));
             }
diff --git a/Assets/_Scripts/Game/UI/MiniGameHUD.cs b/Assets/_Scripts/Game/UI/MiniGameHUD.cs
index e6225d83a..d76c8b805 100644
--- a/Assets/_Scripts/Game/UI/MiniGameHUD.cs
+++ b/Assets/_Scripts/Game/UI/MiniGameHUD.cs
@@ -36,7 +36,6 @@ namespace CosmicShore.Game.UI
 
         [Header("Intro / Connecting")]
         [SerializeField] private float minConnectingSeconds = 5f;
-        [SerializeField] private ArcadeGameConfigSO arcadeGameConfig;
 
         [Header("Pre-Game Cinematic")]
         [SerializeField] private PreGameCinematicController preGameCinematic;
@@ -496,13 +495,8 @@ namespace CosmicShore.Game.UI
 
         private void PushTipsToConnectingPanel()
         {
-            if (view.ConnectingPanel == null) return;
-
-            SO_GameModeTips tips = arcadeGameConfig != null && arcadeGameConfig.SelectedGame != null
-                ? arcadeGameConfig.SelectedGame.Tips
-                : null;
-
-            view.ConnectingPanel.SetTips(tips);
+            if (view.ConnectingPanel == null || gameData == null) return;
+            view.ConnectingPanel.SetGameMode(gameData.GameMode);
         }
 
         private void HideLocalVesselHUD()
diff --git a/Assets/_Scripts/Models/ScriptableObjects/SO_ArcadeGame.cs b/Assets/_Scripts/Models/ScriptableObjects/SO_ArcadeGame.cs
index b2abca87a..83cfe93fa 100644
--- a/Assets/_Scripts/Models/ScriptableObjects/SO_ArcadeGame.cs
+++ b/Assets/_Scripts/Models/ScriptableObjects/SO_ArcadeGame.cs
@@ -20,9 +20,5 @@ namespace CosmicShore
         public CallToActionTargetType CallToActionTargetType;
         public UserActionType ViewUserAction;
         public UserActionType PlayUserAction;
-
-        [Header("Tips")]
-        [Tooltip("Tips shown on the connecting panel before a match starts.")]
-        public SO_GameModeTips Tips;
     }
 }
diff --git a/Assets/_Scripts/Models/ScriptableObjects/SO_GameModeTips.cs b/Assets/_Scripts/Models/ScriptableObjects/SO_GameModeTips.cs
index 89da4bd94..db34f21f8 100644
--- a/Assets/_Scripts/Models/ScriptableObjects/SO_GameModeTips.cs
+++ b/Assets/_Scripts/Models/ScriptableObjects/SO_GameModeTips.cs
@@ -1,26 +1,38 @@
+using System;
 using System.Collections.Generic;
 using UnityEngine;
 
 namespace CosmicShore
 {
-    [CreateAssetMenu(fileName = "New GameModeTips", menuName = "CosmicShore/UI/GameModeTips", order = 0)]
+    [CreateAssetMenu(fileName = "GameModeTipsList", menuName = "CosmicShore/UI/GameModeTipsList", order = 0)]
     public class SO_GameModeTips : ScriptableObject
     {
-        [Tooltip("Tips specific to this game mode.")]
+        [Tooltip("Tips shared across all game modes.")]
         [TextArea(2, 4)]
-        [SerializeField] private List<string> tips = new();
+        [SerializeField] private List<string> commonTips = new();
 
-        [Tooltip("Optional shared/common tips list. Entries here are merged with mode-specific tips.")]
-        [SerializeField] private SO_GameModeTips commonTips;
+        [Tooltip("Per-game-mode tip entries.")]
+        [SerializeField] private List<GameModeTipEntry> modeTips = new();
 
-        public string GetRandomTip()
+        public string GetRandomTip(GameModes mode)
         {
-            var all = new List<string>(tips);
-            if (commonTips != null && commonTips.tips is { Count: > 0 })
-                all.AddRange(commonTips.tips);
+            var pool = new List<string>(commonTips);
 
-            if (all.Count == 0) return string.Empty;
-            return all[Random.Range(0, all.Count)];
+            var entry = modeTips.Find(e => e.mode == mode);
+            if (entry != null && entry.tips is { Count: > 0 })
+                pool.AddRange(entry.tips);
+
+            if (pool.Count == 0) return string.Empty;
+            return pool[UnityEngine.Random.Range(0, pool.Count)];
         }
     }
+
+    [Serializable]
+    public class GameModeTipEntry
+    {
+        public GameModes mode;
+
+        [TextArea(2, 4)]
+        public List<string> tips = new();
+    }
 }
```

</details>
