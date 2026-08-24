using System.Collections.Generic;
using CosmicShore.Editor.Froglet;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CosmicShore.Editor
{
    /// <summary>
    /// One-click wiring for the <b>mode preview</b> — the window that replaces the arcade card's
    /// preview video, showing an idle scale model of the mode's arena until it is clicked and the
    /// real game playing in that same frame afterwards.
    ///
    /// <para>It writes two things and records both in the tool ledger:</para>
    /// <list type="number">
    /// <item><b>The configure-modal PREFAB</b> (not a scene override — per Docs/GAMECANVAS.md a
    /// shared prefab is the single source of truth): a <see cref="ModePreviewWindow"/> plus its
    /// RawImage surface, the transparent focus button that covers it, a focus frame, a
    /// "tap to play" hint, and the library reference.</item>
    /// <item><b>Menu_Main</b>: a <see cref="ModePreviewSession"/> on the freestyle hub (the object
    /// already carrying <c>MenuCrystalClickHandler</c>, exactly where the toybox goes) wired to
    /// the menu vessel initializer, plus a small <see cref="ModePreviewHUD"/> to position beside
    /// the window.</item>
    /// </list>
    ///
    /// <para>The modal's <c>previewSession</c> field is deliberately left empty: a prefab cannot
    /// hold a reference to a scene object, so the modal resolves the session by scene lookup once
    /// per modal lifetime instead.</para>
    ///
    /// <para>Idempotent — safe to re-run. It never overwrites a reference somebody has already
    /// set by hand.</para>
    /// </summary>
    public static class ModePreviewSetupTool
    {
        const string ToolName = "Setup Mode Preview";
        const string ModalPrefabPath = "Assets/_Prefabs/ArcadeGameConfigureModal.prefab";
        const string MenuScenePath = "Assets/_Scenes/Menu_Main.unity";
        const string LibraryAssetPath = "Assets/Resources/ModePreviewLibrary.asset";
        const string PreviewLayerName = "ModePreview";

        [MenuItem("FrogletTools/Scene Setup/Setup Mode Preview")]
        [FrogletTool(FrogletToolCategory.SceneSetup, Importance = 4,
            Description = "Wire the live arena diorama and the in-menu Test Flight into the arcade modal and Menu_Main.")]
        static void Setup()
        {
            var written = new List<string>();
            var report = new List<string>();

            if (LayerMask.NameToLayer(PreviewLayerName) < 0)
            {
                EditorUtility.DisplayDialog(ToolName,
                    $"The '{PreviewLayerName}' layer does not exist.\n\n" +
                    "Add it in Project Settings > Tags and Layers before running this. Without a " +
                    "private layer the diorama camera would render the whole menu world a second " +
                    "time, which is the one thing this feature must not do.",
                    "OK");
                return;
            }

            var library = AssetDatabase.LoadAssetAtPath<ModePreviewLibrarySO>(LibraryAssetPath);
            if (!library)
            {
                EditorUtility.DisplayDialog(ToolName,
                    $"No preview library at {LibraryAssetPath}.\n\n" +
                    "Create one (ScriptableObjects > Game > Mode Preview Library) and add a " +
                    "ModePreviewDefinitionSO per previewable mode first.",
                    "OK");
                return;
            }

            report.Add(WireModalPrefab(library, written));
            report.Add(WireMenuScene(written));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (written.Count > 0)
                FrogletToolChangeLedger.Record(ToolName, written);

            EditorUtility.DisplayDialog(ToolName,
                string.Join("\n\n", report) +
                "\n\nThe modal's 'previewSession' field stays EMPTY on purpose - a prefab cannot " +
                "reference a scene object, so the modal finds the session in the scene instead.\n\n" +
                "The generated UI is functional, not designed: nudge the focus frame, the hint and " +
                "the HUD panel to taste, then ship BOTH the prefab and the scene through " +
                "FrogletTools > Build > Pending Tool Changes. A tool's output lands in your working " +
                "tree, not on the branch.\n\n" +
                "See Docs/ModePreview/ARCHITECTURE.md.",
                "OK");
        }

        // ── The modal prefab ─────────────────────────────────────────────────

        static string WireModalPrefab(ModePreviewLibrarySO library, List<string> written)
        {
            if (!System.IO.File.Exists(ModalPrefabPath))
                return $"• Modal prefab not found at {ModalPrefabPath} - skipped.";

            var root = PrefabUtility.LoadPrefabContents(ModalPrefabPath);
            try
            {
                var modal = root.GetComponentInChildren<ArcadeGameConfigureModal>(true);
                if (!modal)
                    return "• The modal prefab carries no ArcadeGameConfigureModal - skipped.";

                var so = new SerializedObject(modal);

                var windowProp = so.FindProperty("selectedGamePreviewWindow");
                var window = windowProp?.objectReferenceValue as GameObject;
                if (!window)
                    return "• The modal has no 'selectedGamePreviewWindow' assigned - assign the " +
                           "preview frame and re-run.";

                // 1. The surface the stage renders into, stretched to fill the existing frame.
                var surface = FindChild(window.transform, "ModePreviewSurface");
                if (!surface)
                {
                    surface = NewUIChild(window.transform, "ModePreviewSurface");
                    var raw = surface.gameObject.AddComponent<RawImage>();
                    raw.raycastTarget = false;
                    Stretch(surface as RectTransform);
                }

                // 2. The focus button, covering the surface. A Button rather than a raw pointer
                //    handler, so the window is reachable with a gamepad's Submit as well as a tap -
                //    and it is TRANSPARENT, because the thing you click is the picture itself.
                var focus = FindChild(window.transform, "ModePreviewFocus");
                if (!focus)
                {
                    focus = NewUIChild(window.transform, "ModePreviewFocus");
                    Stretch(focus as RectTransform);
                    var hit = focus.gameObject.AddComponent<Image>();
                    hit.color = new Color(1f, 1f, 1f, 0f);   // invisible, still raycastable
                    var btn = focus.gameObject.AddComponent<Button>();
                    btn.targetGraphic = hit;
                }

                // 3. The focus frame and the hint - the two pieces of chrome that say where input
                //    is going. Neither resizes the window; that is the whole point.
                var indicator = EnsureFocusIndicator(window.transform);
                var hint = EnsureFocusHint(window.transform);

                // 4. The window component itself.
                if (!window.TryGetComponent(out ModePreviewWindow previewWindow))
                    previewWindow = window.AddComponent<ModePreviewWindow>();

                var wso = new SerializedObject(previewWindow);
                SetObjectIfEmpty(wso, "surface", surface.GetComponent<RawImage>());
                SetObjectIfEmpty(wso, "focusButton", focus.GetComponent<Button>());
                SetObjectIfEmpty(wso, "focusIndicator", indicator);
                SetObjectIfEmpty(wso, "focusHint", hint);
                wso.ApplyModifiedPropertiesWithoutUndo();

                // 5. Bind them.
                SetObjectIfEmpty(so, "previewLibrary", library);
                SetObjectIfEmpty(so, "previewWindow", previewWindow);
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, ModalPrefabPath);
                written.Add(ModalPrefabPath);

                return "• Modal prefab wired: diorama + surface inside the preview window, a Test " +
                       "Flight button beside it, and the preview library.";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>A frame drawn over the window's edge while it has focus.</summary>
        static GameObject EnsureFocusIndicator(Transform window)
        {
            var existing = FindChild(window, "FocusFrame");
            if (existing) return existing.gameObject;

            var rt = NewUIChild(window, "FocusFrame") as RectTransform;
            Stretch(rt);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = new Color(0.20f, 0.90f, 1.00f, 0.85f);
            image.raycastTarget = false;
            image.type = Image.Type.Sliced;
            rt.gameObject.SetActive(false);
            return rt.gameObject;
        }

        /// <summary>The "tap to play" hint, shown until the window takes focus.</summary>
        static GameObject EnsureFocusHint(Transform window)
        {
            var existing = FindChild(window, "FocusHint");
            if (existing) return existing.gameObject;

            var rt = NewUIChild(window, "FocusHint") as RectTransform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 10f);
            rt.sizeDelta = new Vector2(320f, 40f);

            var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = "TAP TO PLAY";
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 24f;
            label.raycastTarget = false;
            return rt.gameObject;
        }

        // ── Menu_Main ────────────────────────────────────────────────────────

        static string WireMenuScene(List<string> written)
        {
            if (!System.IO.File.Exists(MenuScenePath))
                return $"• {MenuScenePath} not found - the session was not added.";

            Scene scene = default;
            bool wasOpen = false;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.path == MenuScenePath) { scene = s; wasOpen = true; break; }
            }
            if (!wasOpen) scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Additive);
            if (!scene.IsValid()) return "• Could not open Menu_Main - the session was not added.";

            try
            {
                // The freestyle hub is where the toybox goes too - one obvious home for the
                // scene's menu-gameplay components.
                var handler = FindInScene<MenuCrystalClickHandler>(scene);
                var host = handler ? handler.gameObject : null;
                if (!host)
                    return "• Menu_Main has no MenuCrystalClickHandler to host the session - " +
                           "add ModePreviewSession to the Game object by hand.";
                if (!host.TryGetComponent(out ModePreviewSession session))
                    session = Undo.AddComponent<ModePreviewSession>(host);

                var sso = new SerializedObject(session);
                SetObjectIfEmpty(sso, "vesselInitializer",
                    FindInScene<MenuServerPlayerVesselInitializer>(scene));

                var hud = EnsurePreviewHud(scene);
                SetObjectIfEmpty(sso, "hud", hud);
                sso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(session);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                written.Add(MenuScenePath);

                return "• Menu_Main wired: ModePreviewSession on the freestyle hub" +
                       (hud ? ", plus a preview HUD to position beside the modal." : ".");
            }
            finally
            {
                if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>
        /// Build the flight HUD under the freestyle "Game UI" group, so it fades in and out with
        /// the rest of the flight UI through the CanvasGroup bracket that already exists rather
        /// than needing its own visibility wiring.
        /// </summary>
        static ModePreviewHUD EnsurePreviewHud(Scene scene)
        {
            var existing = FindInScene<ModePreviewHUD>(scene);
            if (existing) return existing;

            // Beside the modal, not over the whole screen: a windowed preview must never put
            // full-screen chrome up. The modal prefab is where it belongs, but a prefab cannot
            // hold the scene reference the session needs, so it is built in the scene next to the
            // canvas the modal lives on and positioned by hand afterwards.
            var canvasHost = FindInScene<Canvas>(scene);
            var parent = canvasHost ? canvasHost.transform : null;
            if (!parent) return null;

            var rt = NewUIChild(parent, "ModePreviewHUD") as RectTransform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -40f);
            rt.sizeDelta = new Vector2(720f, 150f);

            var hud = rt.gameObject.AddComponent<ModePreviewHUD>();

            var mode = MakeLabel(rt, "ModeLabel", new Vector2(0f, -4f), 34f, TextAlignmentOptions.Center);
            var objective = MakeLabel(rt, "ObjectiveLabel", new Vector2(0f, -46f), 24f, TextAlignmentOptions.Center);
            var progress = MakeLabel(rt, "ProgressLabel", new Vector2(-200f, -92f), 30f, TextAlignmentOptions.Left);
            var timer = MakeLabel(rt, "TimerLabel", new Vector2(200f, -92f), 30f, TextAlignmentOptions.Right);

            var exitRt = NewUIChild(rt, "ExitButton") as RectTransform;
            exitRt.anchorMin = new Vector2(0.5f, 0f);
            exitRt.anchorMax = new Vector2(0.5f, 0f);
            exitRt.pivot = new Vector2(0.5f, 1f);
            exitRt.anchoredPosition = new Vector2(0f, -8f);
            exitRt.sizeDelta = new Vector2(200f, 52f);
            var exitImage = exitRt.gameObject.AddComponent<Image>();
            exitImage.color = new Color(1f, 1f, 1f, 0.18f);
            var exitButton = exitRt.gameObject.AddComponent<Button>();
            exitButton.targetGraphic = exitImage;
            var exitLabelRt = NewUIChild(exitRt, "Label") as RectTransform;
            Stretch(exitLabelRt);
            var exitLabel = exitLabelRt.gameObject.AddComponent<TextMeshProUGUI>();
            exitLabel.text = "LEAVE";
            exitLabel.alignment = TextAlignmentOptions.Center;
            exitLabel.fontSize = 24f;
            exitLabel.raycastTarget = false;

            var hso = new SerializedObject(hud);
            SetObjectIfEmpty(hso, "modeLabel", mode);
            SetObjectIfEmpty(hso, "objectiveLabel", objective);
            SetObjectIfEmpty(hso, "progressLabel", progress);
            SetObjectIfEmpty(hso, "timerLabel", timer);
            SetObjectIfEmpty(hso, "exitButton", exitButton);
            hso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hud);

            return hud;
        }

        static TextMeshProUGUI MakeLabel(Transform parent, string name, Vector2 offset,
                                         float size, TextAlignmentOptions align)
        {
            var rt = NewUIChild(parent, name) as RectTransform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(680f, 40f);

            var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
            label.fontSize = size;
            label.alignment = align;
            label.raycastTarget = false;
            return label;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        static Transform FindChild(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).name == name) return parent.GetChild(i);
            return null;
        }

        static Transform NewUIChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            return go.transform;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Assign only when the field is empty. Re-running the tool must never stomp a reference
        /// somebody re-pointed by hand.
        /// </summary>
        static void SetObjectIfEmpty(SerializedObject so, string field, Object value)
        {
            if (!value) return;
            var p = so.FindProperty(field);
            if (p == null || p.objectReferenceValue) return;
            p.objectReferenceValue = value;
        }

        static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found) return found;
            }
            return null;
        }

        static Transform FindTransformNamed(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root.transform;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t;
            }
            return null;
        }
    }
}
