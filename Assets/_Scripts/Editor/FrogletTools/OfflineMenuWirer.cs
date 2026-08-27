using System.Collections.Generic;
using System.IO;
using System.Linq;
using CosmicShore.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Editor.Froglet
{
    /// <summary>
    /// Wires the offline surfaces into Menu_Main: the online/offline lamp + its confirmation
    /// bar, and an <see cref="OfflineUIGate"/> on each online-only panel.
    ///
    /// <para>
    /// It operates on the OPEN scene, never on the YAML, so unsaved authoring is preserved and
    /// adopted rather than clobbered. Every object is found BY NAME first and only created when
    /// missing, which makes the tool idempotent and safe to re-run after hand-tuning: run it,
    /// move things where you want them, run it again.
    /// </para>
    ///
    /// <para>
    /// It also generates the accept / cancel icons (a crisp check and cross, anti-aliased, drawn
    /// as signed-distance strokes) into <c>Assets/_Graphics/UI/Offline/</c>. Generated rather
    /// than authored because they are pure geometry - two strokes each - and generating them
    /// keeps the tool self-contained instead of depending on art that may not exist yet. Replace
    /// the PNGs with authored art whenever you like; the tool will not overwrite an existing file.
    /// </para>
    /// </summary>
    public static class OfflineMenuWirer
    {
        const string ToolName = "Offline Menu Wirer";
        const string IconFolder = "Assets/_Graphics/UI/Offline";
        const string CheckIconPath = IconFolder + "/icon_check.png";
        const string CrossIconPath = IconFolder + "/icon_cross.png";
        const int IconSize = 128;

        // Panels gated as online-only. Matched by GameObject name, deepest-first, so a rename in
        // the scene surfaces here as a skipped panel in the report rather than a silent no-op.
        static readonly string[] OnlineOnlyPanelNames =
        {
            "PartyInviteNotificationPanel",
            "FriendsListPanel",
            "ArcadeLobbyList",
            "LeaderboardsMenu",
            "StoreScreen",
        };

        [MenuItem("FrogletTools/Interface/Wire Offline Menu Surfaces")]
        [FrogletTool(FrogletToolCategory.Interface, Importance = 4,
            Description = "Wire the online/offline lamp, its confirm bar, and the online-only UI gates into Menu_Main.")]
        static void Wire()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.name.Contains("Menu_Main"))
            {
                EditorUtility.DisplayDialog(ToolName,
                    "Open Menu_Main first - this tool wires the live scene so your unsaved work is preserved.\n\n" +
                    $"Active scene: '{scene.name}'.", "OK");
                return;
            }

            var report = new List<string>();
            var checkSprite = EnsureIcon(CheckIconPath, DrawCheck, report);
            var crossSprite = EnsureIcon(CrossIconPath, DrawCross, report);

            int gates = WireGates(scene, report);
            bool lamp = WireLampAndBar(scene, checkSprite, crossSprite, report);

            EditorSceneManager.MarkSceneDirty(scene);
            FrogletToolChangeLedger.RecordOpenScenes(ToolName);
            if (checkSprite != null) FrogletToolChangeLedger.Record(ToolName, CheckIconPath);
            if (crossSprite != null) FrogletToolChangeLedger.Record(ToolName, CrossIconPath);

            report.Insert(0, lamp
                ? "Lamp + confirm bar wired."
                : "Lamp NOT wired - see below.");
            report.Insert(1, $"OfflineUIGate on {gates} panel(s).");

            Debug.Log($"[{ToolName}]\n  " + string.Join("\n  ", report));
            EditorUtility.DisplayDialog(ToolName,
                string.Join("\n", report) +
                "\n\nThe scene is dirty - SAVE IT, then commit via FrogletTools > Build > Pending Tool Changes.",
                "OK");
        }

        // ── Gates ────────────────────────────────────────────────────────────

        static int WireGates(UnityEngine.SceneManagement.Scene scene, List<string> report)
        {
            int wired = 0;

            foreach (var panelName in OnlineOnlyPanelNames)
            {
                var panel = FindInScene(scene, panelName);
                if (panel == null)
                {
                    report.Add($"• skipped '{panelName}' - not found in scene.");
                    continue;
                }

                // The gate lives on the panel's PARENT where possible, so it can deactivate the
                // panel itself. A gate on the object it hides would disable its own OnEnable and
                // never be able to restore it.
                var host = panel.transform.parent != null ? panel.transform.parent.gameObject : panel;

                var gate = host.GetComponents<OfflineUIGate>()
                               .FirstOrDefault(g => GateTargets(g).Contains(panel));

                if (gate == null)
                {
                    gate = Undo.AddComponent<OfflineUIGate>(host);
                    var so = new SerializedObject(gate);
                    var list = so.FindProperty("onlineOnlyObjects");
                    list.arraySize = 1;
                    list.GetArrayElementAtIndex(0).objectReferenceValue = panel;
                    so.ApplyModifiedProperties();
                    wired++;
                    report.Add($"• gated '{panelName}' (gate on '{host.name}').");
                }
                else
                {
                    report.Add($"• '{panelName}' already gated - left alone.");
                }
            }

            return wired;
        }

        static IEnumerable<Object> GateTargets(OfflineUIGate gate)
        {
            var so = new SerializedObject(gate);
            var list = so.FindProperty("onlineOnlyObjects");
            for (int i = 0; i < list.arraySize; i++)
                yield return list.GetArrayElementAtIndex(i).objectReferenceValue;
        }

        // ── Lamp + bar ───────────────────────────────────────────────────────

        static bool WireLampAndBar(UnityEngine.SceneManagement.Scene scene,
                                   Sprite check, Sprite cross, List<string> report)
        {
            var indicatorGo = FindInScene(scene, "OnlineIndicator");
            if (indicatorGo == null)
            {
                report.Add("• 'OnlineIndicator' not found - create it under AvatarIcon (an Image + Button) and re-run.");
                return false;
            }

            var barGo = FindInScene(scene, "QuestionBar");
            if (barGo == null)
            {
                report.Add("• 'QuestionBar' not found - create it under Main_Menu_Panel and re-run.");
                return false;
            }

            // ── the bar ──
            var bar = barGo.GetComponent<ConfirmQuestionBar>() ?? Undo.AddComponent<ConfirmQuestionBar>(barGo);

            var label = FindChild(barGo, "Text (TMP)")?.GetComponent<TMPro.TMP_Text>()
                        ?? barGo.GetComponentInChildren<TMPro.TMP_Text>(true);
            var accept = FindChild(barGo, "AcceptButton")?.GetComponent<Button>();
            var cancel = FindChild(barGo, "CancelButton")?.GetComponent<Button>();

            if (accept == null || cancel == null)
            {
                report.Add("• QuestionBar needs child Buttons named 'AcceptButton' and 'CancelButton'.");
                return false;
            }

            StyleAnswerButton(accept, check, new Color(0.35f, 0.85f, 0.30f), report);
            StyleAnswerButton(cancel, cross, new Color(0.90f, 0.32f, 0.30f), report);

            var barSo = new SerializedObject(bar);
            barSo.FindProperty("questionLabel").objectReferenceValue = label;
            barSo.FindProperty("acceptButton").objectReferenceValue = accept;
            barSo.FindProperty("cancelButton").objectReferenceValue = cancel;
            barSo.FindProperty("animationRoot").objectReferenceValue = barGo.transform as RectTransform;
            barSo.ApplyModifiedProperties();

            // ── the lamp ──
            if (indicatorGo.GetComponent<Image>() == null)
            {
                report.Add("• 'OnlineIndicator' has no Image - add one so the lamp has something to tint.");
                return false;
            }
            if (indicatorGo.GetComponent<Button>() == null)
                Undo.AddComponent<Button>(indicatorGo);

            var indicator = indicatorGo.GetComponent<OnlineStatusIndicator>()
                            ?? Undo.AddComponent<OnlineStatusIndicator>(indicatorGo);

            var indSo = new SerializedObject(indicator);
            indSo.FindProperty("lamp").objectReferenceValue = indicatorGo.GetComponent<Image>();
            indSo.FindProperty("questionBar").objectReferenceValue = bar;
            indSo.ApplyModifiedProperties();

            EnsureContainerScope(scene, report);
            return true;
        }

        static void StyleAnswerButton(Button button, Sprite icon, Color accent, List<string> report)
        {
            if (button == null) return;

            // Tint the button's own graphic and give it real pressed/hover states, so the answer
            // controls read as controls rather than as two identical glyphs.
            if (button.TryGetComponent<Image>(out var bg))
            {
                bg.color = new Color(accent.r, accent.g, accent.b, 0.16f);
                bg.type = Image.Type.Sliced;
            }

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;

            if (icon == null) return;

            // The glyph is a child Image so the button keeps its own background graphic.
            var iconGo = FindChild(button.gameObject, "Icon");
            if (iconGo == null)
            {
                iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                Undo.RegisterCreatedObjectUndo(iconGo, "Create answer icon");
                iconGo.transform.SetParent(button.transform, false);

                var rt = (RectTransform)iconGo.transform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(22f, 22f);
            }

            var iconImage = iconGo.GetComponent<Image>();
            iconImage.sprite = icon;
            iconImage.color = accent;
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;

            report.Add($"• styled '{button.name}' with a generated icon.");
        }

        static void EnsureContainerScope(UnityEngine.SceneManagement.Scene scene, List<string> report)
        {
            var scope = scene.GetRootGameObjects()
                             .SelectMany(r => r.GetComponentsInChildren<Component>(true))
                             .Any(c => c != null && c.GetType().Name == "ContainerScope");

            if (!scope)
                report.Add("• WARNING: no ContainerScope in this scene - [Inject] will not resolve, " +
                           "so the lamp and gates will be inert. Add the ContainerScope prefab.");
        }

        // ── Icon generation ──────────────────────────────────────────────────

        static Sprite EnsureIcon(string path, System.Func<Texture2D> draw, List<string> report)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            Directory.CreateDirectory(IconFolder);

            var tex = draw();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            report.Add($"• generated '{Path.GetFileName(path)}'.");
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Texture2D DrawCheck()
        {
            // Two strokes of a check, as distance-to-segment so the edges anti-alias.
            var a = new Vector2(0.22f, 0.52f);
            var b = new Vector2(0.43f, 0.31f);
            var c = new Vector2(0.80f, 0.70f);
            return DrawStrokes(new[] { (a, b), (b, c) });
        }

        static Texture2D DrawCross()
        {
            var a = new Vector2(0.28f, 0.28f);
            var b = new Vector2(0.72f, 0.72f);
            var c = new Vector2(0.72f, 0.28f);
            var d = new Vector2(0.28f, 0.72f);
            return DrawStrokes(new[] { (a, b), (c, d) });
        }

        static Texture2D DrawStrokes((Vector2 a, Vector2 b)[] strokes, float halfWidth = 0.055f)
        {
            var tex = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
            var pixels = new Color32[IconSize * IconSize];

            // One pixel of feather either side of the stroke edge - enough to read smooth at the
            // 22px the buttons display, without blurring the glyph.
            float feather = 1.5f / IconSize;

            for (int y = 0; y < IconSize; y++)
            for (int x = 0; x < IconSize; x++)
            {
                var p = new Vector2((x + 0.5f) / IconSize, (y + 0.5f) / IconSize);

                float d = float.MaxValue;
                foreach (var (a, b) in strokes)
                    d = Mathf.Min(d, DistanceToSegment(p, a, b));

                float alpha = Mathf.Clamp01((halfWidth - d) / feather);
                pixels[y * IconSize + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            if (lengthSq < 1e-8f) return Vector2.Distance(p, a);

            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq);
            return Vector2.Distance(p, a + ab * t);
        }

        // ── Scene helpers ────────────────────────────────────────────────────

        static GameObject FindInScene(UnityEngine.SceneManagement.Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
            }
            return null;
        }

        static GameObject FindChild(GameObject parent, string name)
        {
            foreach (var t in parent.GetComponentsInChildren<Transform>(true))
                if (t.name == name && t.gameObject != parent) return t.gameObject;
            return null;
        }
    }
}
