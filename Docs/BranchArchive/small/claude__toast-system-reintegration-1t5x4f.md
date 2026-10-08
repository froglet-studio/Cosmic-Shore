# Branch archive: `claude/toast-system-reintegration-1t5x4f`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-07-01 by Claude
- **Unmerged commits:** 3
- **Forked from:** `472141ad4` (2026-06-30, another mixing push)
- **Tip:** `440eac2ba`
- **Files touched (5):**
  - `Assets/Editor/FontReplacerWindow.cs`
  - `Assets/Editor/FontReplacerWindow.cs.meta`
  - `Assets/_Scripts/Controller/Party/PartyInviteController.cs`
  - `Assets/_Scripts/UI/ToastNotification/ToastNotificationAPI.cs`
  - `Assets/_Scripts/UI/ToastNotification/ToastNotificationManager.cs`

### `b2a636a0c` — feat(ui): make toast system robust and callable from anywhere

_Claude, 2026-07-01 02:18:17 +0000_

```text
The ToastNotification system was only half-integrated: ToastNotificationAPI
was called from real sites (Hangar, EndGameSequencer, ArcadeGameConfigureModal,
FriendsListPanel, UGSCloudSaveProvider) but only rendered in Menu_Main, because
the manager was conjured at runtime via reflection and depended on a scene-placed
"ToastNotificationContainer" that exists only in that one scene. Everywhere else
toasts were silently dropped.

Make ToastNotificationManager fully self-contained:
- Self-boots before the first scene via RuntimeInitializeOnLoadMethod, so a live
  manager always exists no matter where Show() is first called.
- Auto-loads its settings + SOAP channel from Resources in Awake (no more
  reflection field-injection).
- Owns a persistent DontDestroyOnLoad screen-space-overlay Canvas + vertically
  stacked container built from the settings margins, so toasts render in every
  scene (menu, gameplay, loading) instead of only Menu_Main. An inspector-assigned
  container still takes precedence for bespoke placement.
- Fix the runtime default prefab to have a real height so layout can place it.

Simplify ToastNotificationAPI to a thin, allocation-free facade over the manager.

Route PartyInviteController's failed-join bounce notice through the robust API;
its old bounceToastChannel (ToastSystem/ToastService) was wired to nothing, so
that toast never appeared.
```

```text
 Assets/_Scripts/Controller/Party/PartyInviteController.cs        |  15 ++---
 Assets/_Scripts/UI/ToastNotification/ToastNotificationAPI.cs     | 118 +++++------------------------------
 Assets/_Scripts/UI/ToastNotification/ToastNotificationManager.cs | 122 +++++++++++++++++++++++++++++++++----
 3 files changed, 128 insertions(+), 127 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 381 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Party/PartyInviteController.cs b/Assets/_Scripts/Controller/Party/PartyInviteController.cs
index 3722b745d..c9e039107 100644
--- a/Assets/_Scripts/Controller/Party/PartyInviteController.cs
+++ b/Assets/_Scripts/Controller/Party/PartyInviteController.cs
@@ -37,9 +37,6 @@ namespace CosmicShore.Gameplay
         [Header("SOAP Data")]
         [SerializeField] private HostConnectionDataSO connectionData;
 
-        [Tooltip("Optional. Best-effort toast shown when a join fails and the client bounces back to its own menu. May be suppressed during the scene reload.")]
-        [SerializeField] private ToastChannel bounceToastChannel;
-
         [Header("Timing")]
         [Tooltip("Max time (seconds) to wait for NetworkManager shutdown.")]
         [SerializeField] private float shutdownTimeoutSeconds = 2f;
@@ -451,13 +448,11 @@ namespace CosmicShore.Gameplay
         {
             Debug.LogWarning($"[PartyInviteController] Bouncing to solo menu: {toastMessage}");
             await RecoverFromFailedTransitionAsync();
-            // Show the notice AFTER recovery. ToastService is a scene-bound MonoBehaviour
-            // (it subscribes to the channel in OnEnable), so it is destroyed + recreated by
-            // the Menu_Main reload — and is absent entirely in a game scene. A toast raised
-            // before recovery is therefore silently dropped (the channel event has no
-            // subscriber). Raising it here lands on the fresh menu's live ToastService.
-            // See Docs/PartySystem/BUGS.md B10.
-            bounceToastChannel?.ShowPrefix(toastMessage);
+            // Show the notice AFTER recovery. ToastNotificationManager is a persistent,
+            // self-bootstrapping singleton that owns its own overlay canvas, so it survives
+            // the Menu_Main reload and renders regardless of the active scene — no scene-bound
+            // subscriber to miss. See Docs/PartySystem/BUGS.md B10.
+            ToastNotificationAPI.Show(toastMessage);
         }
 
         /// <summary>
diff --git a/Assets/_Scripts/UI/ToastNotification/ToastNotificationAPI.cs b/Assets/_Scripts/UI/ToastNotification/ToastNotificationAPI.cs
index a04d3ec9e..0de2a7267 100644
--- a/Assets/_Scripts/UI/ToastNotification/ToastNotificationAPI.cs
+++ b/Assets/_Scripts/UI/ToastNotification/ToastNotificationAPI.cs
@@ -1,123 +1,33 @@
 using CosmicShore.Utility;
-using UnityEngine;
 
 namespace CosmicShore.UI
 {
     /// <summary>
     /// Static convenience API for showing toast notifications from anywhere in the codebase.
-    /// Auto-creates the ToastNotificationManager singleton if it doesn't exist in the scene.
-    /// Finds the container by searching for a GameObject named "ToastNotificationContainer".
+    ///
+    /// <para>The heavy lifting lives in <see cref="ToastNotificationManager"/>, which boots
+    /// itself before the first scene, loads its settings + SOAP channel from <c>Resources</c>,
+    /// and owns a persistent overlay canvas so toasts render in every scene. This class is a
+    /// thin, allocation-free facade over it.</para>
+    ///
+    /// <para>Main-thread only. Off-thread callers (UGS / Netcode continuations) must marshal
+    /// via <c>.AsMainThread()</c> before calling — see <c>Docs/THREADING.md</c>.</para>
     /// </summary>
     public static class ToastNotificationAPI
     {
-        private const string ChannelPath = "Channels/ToastNotificationChannel";
-        private const string SettingsPath = "ToastNotificationSettings";
-        private const string ContainerName = "ToastNotificationContainer";
-
-        private static ToastNotificationChannel _channel;
-
-        private static ToastNotificationChannel Channel =>
-            _channel != null
-                ? _channel
-                : (_channel = Resources.Load<ToastNotificationChannel>(ChannelPath));
-
-        /// <summary>
-        /// Show a toast notification with the given message.
-        /// Ensures the manager and container exist before dispatching.
-        /// </summary>
+        /// <summary>Show a toast notification with the given message.</summary>
         public static void Show(string message)
         {
-            EnsureManagerExists();
+            if (string.IsNullOrWhiteSpace(message)) return;
 
-            if (ToastNotificationManager.Instance != null)
+            var manager = ToastNotificationManager.EnsureInstance();
+            if (manager != null)
             {
-                ToastNotificationManager.Instance.Show(message);
+                manager.Show(message);
                 return;
             }
 
-            CSDebug.LogWarning(
-                $"[ToastNotificationAPI] Manager creation failed. Message dropped: {message}");
-        }
-
-        private static void EnsureManagerExists()
-        {
-            if (ToastNotificationManager.Instance != null)
-            {
-                // Manager exists but container may have been destroyed (scene change)
-                if (ToastNotificationManager.Instance.Container == null)
-                    TryAssignContainer(ToastNotificationManager.Instance);
-                return;
-            }
-
-            var go = new GameObject("ToastNotificationManager");
-            var mgr = go.AddComponent<ToastNotificationManager>();
-
-            // Wire settings from Resources
-            var settings = Resources.Load<ToastNotificationSettingsSO>(SettingsPath);
-            if (settings != null)
-            {
-                var field = typeof(ToastNotificationManager).GetField("settings",
-                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
-                field?.SetValue(mgr, settings);
-            }
-
-            // Wire channel
-            var channel = Channel;
-            if (channel != null)
-            {
-                var field = typeof(ToastNotificationManager).GetField("channel",
-                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
-                field?.SetValue(mgr, channel);
-
-                mgr.enabled = false;
-                mgr.enabled = true;
-            }
-
-            TryAssignContainer(mgr);
-
-            CSDebug.Log("[ToastNotificationAPI] Auto-created ToastNotificationManager.");
-        }
-
-        private static void TryAssignContainer(ToastNotificationManager mgr)
-        {
-            var rt = FindContainerIncludingInactive();
-            if (rt != null)
-            {
-                mgr.Container = rt;
-            }
-            else
-            {
-                CSDebug.LogWarning(
-                    $"[ToastNotificationAPI] No GameObject named '{ContainerName}' found in scene. " +
-                    "Toasts will not display until a container is available.");
-            }
-        }
-
-        /// <summary>
-        /// Finds the toast container by name. Unlike <see cref="GameObject.Find"/>, this also
-        /// matches inactive objects — the container commonly lives under a menu panel that is
-        /// inactive when the first toast fires, which previously caused the toast to be dropped.
```

</details>

### `5be285115` — feat(editor): add Font Replacer tool for swapping TMP font assets

_Claude, 2026-07-01 03:42:00 +0000_

```text
Editor-only EditorWindow at Tools > Cosmic Shore > Font Replacer (plus a
CONTEXT > TMP_Text > Replace Font… entry). Swaps the TextMeshPro font asset on
text components in two modes: on the current hierarchy selection (optionally
including children / inactive), or Find All TMP text in the loaded scene(s) and
replace together. Does not touch the underlying font files.

Correctness: applies a matching atlas material (target font default, or a
supplied preset) alongside the font swap so text does not render as tofu.
Optional source-font filter retargets only components using a chosen font.
Every change is a single-step Undo, registers prefab-instance overrides, and
dirties the affected scene(s). Works for both TextMeshProUGUI and TextMeshPro.
```

```text
 Assets/Editor/FontReplacerWindow.cs      | 322 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 Assets/Editor/FontReplacerWindow.cs.meta |  11 +++
 2 files changed, 333 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 150 of 328 lines)</summary>

```diff
diff --git a/Assets/Editor/FontReplacerWindow.cs b/Assets/Editor/FontReplacerWindow.cs
new file mode 100644
index 000000000..9bb81e242
--- /dev/null
+++ b/Assets/Editor/FontReplacerWindow.cs
@@ -0,0 +1,322 @@
+using System.Collections.Generic;
+using System.Linq;
+using TMPro;
+using UnityEditor;
+using UnityEditor.SceneManagement;
+using UnityEngine;
+using UnityEngine.SceneManagement;
+
+namespace CosmicShore.Editor
+{
+    /// <summary>
+    /// Editor-only tool that swaps the TextMeshPro font asset on text components.
+    ///
+    /// <para>Two modes: replace on the current hierarchy <b>Selection</b> (optionally
+    /// including children / inactive objects), or <b>Find All</b> TMP text in the loaded
+    /// scene(s) and replace them together. Nothing runs at play time.</para>
+    ///
+    /// <para>Correctness: changing a TMP font asset without also updating the font material
+    /// leaves the component pointing at the <i>old</i> atlas material, which renders as
+    /// blank/tofu. This tool always applies a matching material — the supplied preset, or the
+    /// target font's default material — right after the font swap. Every change goes through
+    /// <see cref="Undo"/>, registers prefab-instance overrides, and dirties the owning
+    /// scene(s) so it survives a save.</para>
+    ///
+    /// Open via <b>Tools ▸ Cosmic Shore ▸ Font Replacer</b>.
+    /// </summary>
+    public sealed class FontReplacerWindow : EditorWindow
+    {
+        // ── Config ────────────────────────────────────────────────────────────
+        private TMP_FontAsset _targetFont;
+        private Material _materialPreset;
+
+        private bool _useSourceFilter;
+        private TMP_FontAsset _sourceFilter;
+
+        private bool _includeChildren = true;
+        private bool _includeInactive = true;
+
+        // ── Cached scan results ───────────────────────────────────────────────
+        private readonly List<TMP_Text> _selectionTargets = new();
+        private readonly List<TMP_Text> _sceneTargets = new();
+
+        private Vector2 _scroll;
+        private bool _showPreview = true;
+
+        [MenuItem("Tools/Cosmic Shore/Font Replacer")]
+        public static void Open()
+        {
+            var window = GetWindow<FontReplacerWindow>("Font Replacer");
+            window.minSize = new Vector2(380f, 460f);
+            window.RefreshSelection();
+            window.Show();
+        }
+
+        /// <summary>Context-menu entry on any TMP component: opens the tool primed from it.</summary>
+        [MenuItem("CONTEXT/TMP_Text/Replace Font…")]
+        private static void OpenFromContext(MenuCommand command)
+        {
+            var window = GetWindow<FontReplacerWindow>("Font Replacer");
+            if (command.context is TMP_Text tmp && tmp.font != null)
+            {
+                window._useSourceFilter = true;
+                window._sourceFilter = tmp.font;
+            }
+            window.RefreshSelection();
+            window.Show();
+        }
+
+        private void OnEnable() => RefreshSelection();
+
+        private void OnSelectionChange()
+        {
+            RefreshSelection();
+            Repaint();
+        }
+
+        private void OnGUI()
+        {
+            EditorGUILayout.Space(4f);
+            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
+
+            _targetFont = (TMP_FontAsset)EditorGUILayout.ObjectField(
+                new GUIContent("Font Asset", "The TMP font asset to apply to matching text components."),
+                _targetFont, typeof(TMP_FontAsset), false);
+
+            _materialPreset = (Material)EditorGUILayout.ObjectField(
+                new GUIContent("Material Preset",
+                    "Optional. Font material preset to apply alongside the font (e.g. an outline/glow variant). " +
+                    "Leave empty to use the target font's default material."),
+                _materialPreset, typeof(Material), false);
+
+            EditorGUILayout.Space(6f);
+            EditorGUILayout.LabelField("Filter", EditorStyles.boldLabel);
+
+            _useSourceFilter = EditorGUILayout.ToggleLeft(
+                new GUIContent("Only replace a specific source font",
+                    "When on, only components currently using the Source Font are changed. " +
+                    "When off, every text in scope is changed regardless of its current font."),
+                _useSourceFilter);
+
+            using (new EditorGUI.DisabledScope(!_useSourceFilter))
+            {
+                EditorGUI.indentLevel++;
+                _sourceFilter = (TMP_FontAsset)EditorGUILayout.ObjectField(
+                    "Source Font", _sourceFilter, typeof(TMP_FontAsset), false);
+                if (GUILayout.Button("Set Source From Selection"))
+                    SetSourceFromSelection();
+                EditorGUI.indentLevel--;
+            }
+
+            EditorGUILayout.Space(6f);
+            EditorGUILayout.LabelField("Scope", EditorStyles.boldLabel);
+
+            EditorGUI.BeginChangeCheck();
+            _includeChildren = EditorGUILayout.ToggleLeft(
+                new GUIContent("Include children of selection",
+                    "Also affect TMP text on descendants of the selected objects."), _includeChildren);
+            _includeInactive = EditorGUILayout.ToggleLeft(
+                new GUIContent("Include inactive objects",
+                    "Also affect TMP text on disabled objects."), _includeInactive);
+            if (EditorGUI.EndChangeCheck())
+                RefreshSelection();
+
+            if (_targetFont == null)
+                EditorGUILayout.HelpBox("Assign a Target Font Asset to enable replacement.", MessageType.Info);
+
+            DrawSelectionSection();
+            DrawSceneSection();
+            DrawPreview();
+        }
+
+        // ── Selection ─────────────────────────────────────────────────────────
+        private void DrawSelectionSection()
+        {
+            EditorGUILayout.Space(8f);
+            EditorGUILayout.LabelField("Replace on Selection", EditorStyles.boldLabel);
+
+            int matching = Effective(_selectionTargets).Count();
+            EditorGUILayout.LabelField(
+                $"Selection: {_selectionTargets.Count} TMP text(s)   ·   matching filter: {matching}");
+
+            using (new EditorGUI.DisabledScope(_targetFont == null || matching == 0))
+            {
+                if (GUILayout.Button($"Replace on Selection  ({matching})", GUILayout.Height(28f)))
```

</details>

### `440eac2ba` — fix(editor): harden Font Replacer per adversarial review

_Claude, 2026-07-01 03:48:02 +0000_

```text
Applies four verified findings from the review pass:

- Validate the optional Material Preset's atlas against the target font; a
  preset built from a different font renders as tofu, so it is now ignored
  (default material used) with a warning surfaced at edit time and on run.
- Guard Play mode: Replace is disabled and buttons greyed out while playing,
  since scene edits there are discarded on exit (a warning banner explains it).
- Preview no longer silently hides scene results when something is selected;
  an explicit Selection|Scene toolbar (with live counts) drives the preview so
  it can never disagree with a Replace button's count.
- Effective() now honors the live "include inactive" toggle even against a
  stale scene scan, and toggling it clears the scan — so inactive text the user
  excluded can't be replaced.
```

```text
 Assets/Editor/FontReplacerWindow.cs | 93 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++----
 1 file changed, 88 insertions(+), 5 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 185 lines)</summary>

```diff
diff --git a/Assets/Editor/FontReplacerWindow.cs b/Assets/Editor/FontReplacerWindow.cs
index 9bb81e242..a4b774bc8 100644
--- a/Assets/Editor/FontReplacerWindow.cs
+++ b/Assets/Editor/FontReplacerWindow.cs
@@ -43,6 +43,9 @@ namespace CosmicShore.Editor
         private Vector2 _scroll;
         private bool _showPreview = true;
 
+        private enum PreviewSource { Selection, Scene }
+        private PreviewSource _previewSource = PreviewSource.Selection;
+
         [MenuItem("Tools/Cosmic Shore/Font Replacer")]
         public static void Open()
         {
@@ -66,11 +69,23 @@ namespace CosmicShore.Editor
             window.Show();
         }
 
-        private void OnEnable() => RefreshSelection();
+        private void OnEnable()
+        {
+            RefreshSelection();
+            EditorApplication.playModeStateChanged += OnPlayModeChanged;
+        }
+
+        private void OnDisable()
+        {
+            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
+        }
+
+        private void OnPlayModeChanged(PlayModeStateChange _) => Repaint();
 
         private void OnSelectionChange()
         {
             RefreshSelection();
+            _previewSource = PreviewSource.Selection; // the user just touched the hierarchy
             Repaint();
         }
 
@@ -89,6 +104,14 @@ namespace CosmicShore.Editor
                     "Leave empty to use the target font's default material."),
                 _materialPreset, typeof(Material), false);
 
+            if (_targetFont != null && _materialPreset != null && !MaterialMatchesFont(_materialPreset, _targetFont))
+            {
+                EditorGUILayout.HelpBox(
+                    "The Material Preset's atlas doesn't match the Target Font, so it would make text " +
+                    "unreadable. It will be ignored — the font's default material is applied instead.",
+                    MessageType.Warning);
+            }
+
             EditorGUILayout.Space(6f);
             EditorGUILayout.LabelField("Filter", EditorStyles.boldLabel);
 
@@ -115,15 +138,25 @@ namespace CosmicShore.Editor
             _includeChildren = EditorGUILayout.ToggleLeft(
                 new GUIContent("Include children of selection",
                     "Also affect TMP text on descendants of the selected objects."), _includeChildren);
+            if (EditorGUI.EndChangeCheck())
+                RefreshSelection();
+
+            EditorGUI.BeginChangeCheck();
             _includeInactive = EditorGUILayout.ToggleLeft(
                 new GUIContent("Include inactive objects",
                     "Also affect TMP text on disabled objects."), _includeInactive);
             if (EditorGUI.EndChangeCheck())
+            {
                 RefreshSelection();
+                _sceneTargets.Clear(); // this toggle changes what a scene scan returns — force a re-Find
+            }
 
             if (_targetFont == null)
                 EditorGUILayout.HelpBox("Assign a Target Font Asset to enable replacement.", MessageType.Info);
 
+            if (EditorApplication.isPlaying)
+                EditorGUILayout.HelpBox("Font replacement is disabled during Play mode.", MessageType.Warning);
+
             DrawSelectionSection();
             DrawSceneSection();
             DrawPreview();
@@ -139,7 +172,7 @@ namespace CosmicShore.Editor
             EditorGUILayout.LabelField(
                 $"Selection: {_selectionTargets.Count} TMP text(s)   ·   matching filter: {matching}");
 
-            using (new EditorGUI.DisabledScope(_targetFont == null || matching == 0))
+            using (new EditorGUI.DisabledScope(_targetFont == null || matching == 0 || EditorApplication.isPlaying))
             {
                 if (GUILayout.Button($"Replace on Selection  ({matching})", GUILayout.Height(28f)))
                     Replace(_selectionTargets, "Selection");
@@ -167,7 +200,7 @@ namespace CosmicShore.Editor
             EditorGUILayout.LabelField(
                 $"Found in scene: {_sceneTargets.Count}   ·   matching filter: {matching}");
 
-            using (new EditorGUI.DisabledScope(_targetFont == null || matching == 0))
+            using (new EditorGUI.DisabledScope(_targetFont == null || matching == 0 || EditorApplication.isPlaying))
             {
                 if (GUILayout.Button($"Replace All in Scene  ({matching})", GUILayout.Height(28f)))
                 {
@@ -188,7 +221,14 @@ namespace CosmicShore.Editor
             _showPreview = EditorGUILayout.Foldout(_showPreview, "Preview (targets matching filter)", true);
             if (!_showPreview) return;
 
-            var pool = _selectionTargets.Count > 0 ? _selectionTargets : _sceneTargets;
+            // Explicit source toggle so the preview can never silently disagree with a Replace button's count.
+            _previewSource = (PreviewSource)GUILayout.Toolbar((int)_previewSource, new[]
+            {
+                $"Selection ({Effective(_selectionTargets).Count()})",
+                $"Scene ({Effective(_sceneTargets).Count()})",
+            });
+
+            var pool = _previewSource == PreviewSource.Scene ? _sceneTargets : _selectionTargets;
             var list = Effective(pool).ToList();
 
             _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(150f));
@@ -252,11 +292,14 @@ namespace CosmicShore.Editor
             var mode = _includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;
             var all = FindObjectsByType<TMP_Text>(mode, FindObjectsSortMode.None);
             _sceneTargets.AddRange(all.Where(t => t != null));
+            _previewSource = PreviewSource.Scene; // the user just scanned the scene
         }
 
         private IEnumerable<TMP_Text> Effective(IEnumerable<TMP_Text> source)
         {
             var live = source.Where(t => t != null);
+            if (!_includeInactive)
+                live = live.Where(t => t.gameObject.activeInHierarchy); // honor the live toggle even on a stale scan
             return (_useSourceFilter && _sourceFilter != null)
                 ? live.Where(t => t.font == _sourceFilter)
                 : live;
@@ -275,14 +318,54 @@ namespace CosmicShore.Editor
             ShowNotification(new GUIContent("Selection has no TMP text with a font."));
         }
 
+        /// <summary>
+        /// The material to apply with the target font: the supplied preset when it belongs to the
+        /// target font's atlas, otherwise the font's own default material (a mismatched preset would
+        /// render as tofu, so it is ignored with a warning).
+        /// </summary>
+        private Material ResolveMaterial()
+        {
+            if (_materialPreset == null) return _targetFont.material;
+            if (MaterialMatchesFont(_materialPreset, _targetFont)) return _materialPreset;
+
+            Debug.LogWarning(
+                $"[FontReplacer] Material preset '{_materialPreset.name}' does not use '{_targetFont.name}' " +
+                "atlas — ignoring it and applying the font's default material to keep text readable.");
+            return _targetFont.material;
+        }
+
+        /// <summary>True if <paramref name="mat"/> samples one of <paramref name="font"/>'s atlas textures.</summary>
```

</details>
