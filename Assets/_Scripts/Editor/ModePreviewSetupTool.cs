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
    /// preview video: the mode's own arena playing live under AI the moment a card is selected,
    /// taken over by tapping the window, with "LEVEL PREVIEW NOT AVAILABLE" as the only fallback.
    ///
    /// <para>It writes two things and records both in the tool ledger:</para>
    /// <list type="number">
    /// <item><b>The configure-modal PREFAB</b> (not a scene override — per Docs/GAMECANVAS.md a
    /// shared prefab is the single source of truth): a <see cref="ModePreviewWindow"/> with its
    /// RawImage surface, status label, transparent focus button and "tap to play" hint. It also
    /// DELETES the chrome earlier revisions authored — the TestFlightButton, the FocusFrame (a
    /// sprite-less sliced Image renders as a solid colour fill, the "blue overlay"), and any
    /// legacy VideoPlayer instances under the preview frame.</item>
    /// <item><b>Menu_Main</b>: a <see cref="ModePreviewSession"/> on the freestyle hub (the object
    /// already carrying <c>MenuCrystalClickHandler</c>, exactly where the toybox goes) wired to
    /// the menu vessel initializer, plus a small <see cref="ModePreviewHUD"/> — whose ExitButton,
    /// if an earlier revision built one, is deleted (leaving the preview is tapping outside the
    /// window, not a button).</item>
    /// </list>
    ///
    /// <para>The modal's <c>previewSession</c> field is deliberately left empty: a prefab cannot
    /// hold a reference to a scene object, so the modal resolves the session by scene lookup once
    /// per modal lifetime instead.</para>
    ///
    /// <para>Idempotent — safe to re-run. It never overwrites a reference somebody has already
    /// set by hand, and re-running is also the MIGRATION path off the earlier revisions.</para>
    /// </summary>
    public static class ModePreviewSetupTool
    {
        const string ToolName = "Setup Mode Preview";
        const string ModalPrefabPath = "Assets/_Prefabs/ArcadeGameConfigureModal.prefab";
        const string MenuScenePath = "Assets/_Scenes/Menu_Main.unity";
        const string LibraryAssetPath = "Assets/Resources/ModePreviewLibrary.asset";

        [MenuItem("FrogletTools/Scene Setup/Setup Mode Preview")]
        [FrogletTool(FrogletToolCategory.SceneSetup, Importance = 4,
            Description = "Wire the live arena diorama and the in-menu Test Flight into the arcade modal and Menu_Main.")]
        static void Setup()
        {
            var written = new List<string>();
            var report = new List<string>();

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

                // Migration: delete the chrome earlier revisions authored. The FocusFrame is the
                // reported "blue overlay" - an Image with type Sliced and NO sprite renders as a
                // solid colour fill over the whole window. The TestFlightButton belongs to the
                // retired full-screen design. Legacy VideoPlayer instances under the frame are
                // what leaked through as stale video / white rectangles.
                DeleteChild(window.transform, "FocusFrame");
                DeleteChild(window.transform.parent, "TestFlightButton");
                DeleteChild(window.transform, "TestFlightButton");
                foreach (var video in window.GetComponentsInChildren<UnityEngine.Video.VideoPlayer>(true))
                    Object.DestroyImmediate(video.gameObject);

                // 1. The surface the live camera renders into, stretched to fill the frame.
                var surface = FindChild(window.transform, "ModePreviewSurface");
                if (!surface)
                {
                    surface = NewUIChild(window.transform, "ModePreviewSurface");
                    var raw = surface.gameObject.AddComponent<RawImage>();
                    raw.raycastTarget = false;
                    Stretch(surface as RectTransform);
                }

                // 2. The status label - the window's only voice when it is not live.
                var status = FindChild(window.transform, "ModePreviewStatus");
                if (!status)
                {
                    status = NewUIChild(window.transform, "ModePreviewStatus");
                    Stretch(status as RectTransform);
                    var label = status.gameObject.AddComponent<TextMeshProUGUI>();
                    label.text = "LEVEL PREVIEW\nNOT AVAILABLE";
                    label.alignment = TextAlignmentOptions.Center;
                    label.fontSize = 30f;
                    label.raycastTarget = false;
                }

                // 3. The focus button, covering the surface. A Button rather than a raw pointer
                //    handler, so the window is reachable with a gamepad's Submit as well as a tap
                //    - and it is TRANSPARENT, because the thing you tap is the picture itself.
                var focus = FindChild(window.transform, "ModePreviewFocus");
                if (!focus)
                {
                    focus = NewUIChild(window.transform, "ModePreviewFocus");
                    Stretch(focus as RectTransform);
                    var hit = focus.gameObject.AddComponent<Image>();
                    hit.color = new Color(1f, 1f, 1f, 0f);   // invisible, still raycastable
                    var btn = focus.gameObject.AddComponent<Button>();
                    btn.targetGraphic = hit;
                    // No visual transition: a tint on press would flash the whole live view.
                    btn.transition = Selectable.Transition.None;
                }

                var hint = EnsureFocusHint(window.transform);

                // 4. The window component itself.
                if (!window.TryGetComponent(out ModePreviewWindow previewWindow))
                    previewWindow = window.AddComponent<ModePreviewWindow>();

                var wso = new SerializedObject(previewWindow);
                SetObjectIfEmpty(wso, "surface", surface.GetComponent<RawImage>());
                SetObjectIfEmpty(wso, "statusLabel", status.GetComponent<TextMeshProUGUI>());
                SetObjectIfEmpty(wso, "focusButton", focus.GetComponent<Button>());
                SetObjectIfEmpty(wso, "focusHint", hint);
                wso.ApplyModifiedPropertiesWithoutUndo();

                // 5. Bind them.
                SetObjectIfEmpty(so, "previewLibrary", library);
                SetObjectIfEmpty(so, "previewWindow", previewWindow);
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, ModalPrefabPath);
                written.Add(ModalPrefabPath);

                return "• Modal prefab wired: surface + status label + focus button + hint inside " +
                       "the preview frame; stale TestFlightButton / FocusFrame / video instances " +
                       "deleted.";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>The "TAP TO PLAY" hint, shown while live and unfocused.</summary>
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

        static void DeleteChild(Transform parent, string name)
        {
            if (!parent) return;
            var child = FindChild(parent, name);
            if (child) Object.DestroyImmediate(child.gameObject);
        }

        // ── Menu_Main ─        // ── Menu_Main ────────────────────────────────────────────────────────

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

            // No exit button (an earlier revision built one - delete it): leaving the preview
            // is tapping OUTSIDE the window, and while flying, on-screen buttons are exactly the
            // UI the focus gate exists to keep out of the pad's way.
            DeleteChild(rt, "ExitButton");

            var hso = new SerializedObject(hud);
            SetObjectIfEmpty(hso, "modeLabel", mode);
            SetObjectIfEmpty(hso, "objectiveLabel", objective);
            SetObjectIfEmpty(hso, "progressLabel", progress);
            SetObjectIfEmpty(hso, "timerLabel", timer);
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
