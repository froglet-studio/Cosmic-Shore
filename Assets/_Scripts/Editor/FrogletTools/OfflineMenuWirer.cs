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
        const string OnlineIconPath = IconFolder + "/icon_status_online.png";
        const string OfflineIconPath = IconFolder + "/icon_status_offline.png";

        /// <summary>
        /// Source resolution. Generous because these are UI sprites that can be scaled up by a
        /// CanvasScaler on a high-DPI display, and downscaling is free while upscaling is not.
        /// </summary>
        const int IconSize = 256;

        /// <summary>
        /// Samples per pixel per axis (so <c>Supersample²</c> total). The first pass used a
        /// single centre sample with a 1.5px linear feather, which is a coarse approximation of
        /// coverage and shows as slightly ragged curves. Analytic distance + 4×4 supersampling
        /// resolves a circle's edge properly at any radius.
        /// </summary>
        const int Supersample = 4;

        /// <summary>Display size of the lamp when the tool has to square up a stretched rect.</summary>
        const float LampPixelSize = 28f;

        /// <summary>
        /// What to gate, found by COMPONENT TYPE rather than by GameObject name - the panels live
        /// inside prefab instances whose object names differ from their script names
        /// (LeaderboardsMenu sits on "PortScreen", StoreScreen on "ArkScreen"), so a name match
        /// would silently miss most of them.
        /// </summary>
        /// <remarks>
        /// The style is per-target and load-bearing. A whole SCREEN the nav bar can reach must
        /// never be hidden - the player would navigate to a blank panel - so screens dim in
        /// place and stay navigable, while sub-panels hide outright.
        /// </remarks>
        readonly struct GateTarget
        {
            public readonly string TypeName;
            public readonly bool IsWholeScreen;
            public GateTarget(string typeName, bool isWholeScreen)
            {
                TypeName = typeName;
                IsWholeScreen = isWholeScreen;
            }
        }

        static readonly GateTarget[] OnlineOnlyPanels =
        {
            new("PartyInviteNotificationPanel", false),
            new("FriendsListPanel",             false),
            new("ArcadeLobbyList",              false),
            new("LeaderboardsMenu",             true),   // PortScreen - must stay navigable
            new("StoreScreen",                  true),   // ArkScreen  - must stay navigable
        };

        [MenuItem("FrogletTools/Interface/Wire Offline Menu Surfaces")]
        [FrogletTool(FrogletToolCategory.Interface, Importance = 4,
            Description = "Wire the online/offline lamp, its confirm bar, and the online-only UI gates into Menu_Main.")]
        static void Wire() => Run(regenerateIcons: false);

        [MenuItem("FrogletTools/Interface/Wire Offline Menu Surfaces (Regenerate Icons)")]
        [FrogletTool(FrogletToolCategory.Interface, Importance = 2,
            Description = "Same, but re-renders the offline icon set from scratch - use after changing the icon geometry.")]
        static void WireAndRegenerate() => Run(regenerateIcons: true);

        static void Run(bool regenerateIcons)
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
            var checkSprite   = EnsureIcon(CheckIconPath,   DrawCheck,       report, regenerateIcons);
            var crossSprite   = EnsureIcon(CrossIconPath,   DrawCross,       report, regenerateIcons);
            var onlineSprite  = EnsureIcon(OnlineIconPath,  DrawOnlineLamp,  report, regenerateIcons);
            var offlineSprite = EnsureIcon(OfflineIconPath, DrawOfflineLamp, report, regenerateIcons);

            int gates = WireGates(scene, report);
            bool lamp = WireLampAndBar(scene, checkSprite, crossSprite, onlineSprite, offlineSprite, report);

            EditorSceneManager.MarkSceneDirty(scene);
            FrogletToolChangeLedger.RecordOpenScenes(ToolName);
            if (checkSprite   != null) FrogletToolChangeLedger.Record(ToolName, CheckIconPath);
            if (crossSprite   != null) FrogletToolChangeLedger.Record(ToolName, CrossIconPath);
            if (onlineSprite  != null) FrogletToolChangeLedger.Record(ToolName, OnlineIconPath);
            if (offlineSprite != null) FrogletToolChangeLedger.Record(ToolName, OfflineIconPath);

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

            foreach (var target in OnlineOnlyPanels)
            {
                var panel = FindByComponentType(scene, target.TypeName);
                if (panel == null)
                {
                    report.Add($"• skipped '{target.TypeName}' - no such component in this scene.");
                    continue;
                }

                // A HIDDEN panel is gated from its PARENT: a gate on the object it deactivates
                // would kill its own OnEnable and could never restore it. A DIMMED screen keeps
                // its GameObject active, so it hosts its own gate.
                var host = target.IsWholeScreen
                    ? panel
                    : (panel.transform.parent != null ? panel.transform.parent.gameObject : panel);

                if (host.GetComponents<OfflineUIGate>().Any(g => GateTargets(g).Contains(panel)))
                {
                    report.Add($"• '{target.TypeName}' already gated - left alone.");
                    continue;
                }

                var gate = Undo.AddComponent<OfflineUIGate>(host);
                var so = new SerializedObject(gate);

                so.FindProperty("style").enumValueIndex = target.IsWholeScreen ? 1 : 0; // DisableAndDim : Hide

                var list = so.FindProperty("onlineOnlyObjects");
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = panel;
                so.ApplyModifiedProperties();

                wired++;
                report.Add($"• gated '{target.TypeName}' on '{host.name}' " +
                           $"({(target.IsWholeScreen ? "dimmed, stays navigable" : "hidden")}).");
            }

            return wired;
        }

        /// <summary>
        /// First GameObject in the scene carrying a component whose type is named
        /// <paramref name="typeName"/>. Matched on the type name rather than a hard reference so
        /// this editor tool does not need an assembly reference to every panel it gates.
        /// </summary>
        static GameObject FindByComponentType(UnityEngine.SceneManagement.Scene scene, string typeName)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var c in root.GetComponentsInChildren<Component>(true))
                {
                    if (c != null && c.GetType().Name == typeName)
                        return c.gameObject;
                }
            }
            return null;
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
                                   Sprite check, Sprite cross,
                                   Sprite onlineLamp, Sprite offlineLamp,
                                   List<string> report)
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

            var lampImage = indicatorGo.GetComponent<Image>();

            // The lamp is a round sprite, so its box must be square or the circle renders as an
            // ellipse. Authored stretch anchors (sizeDelta 0,-120 on the shipped object) inherit
            // the parent's aspect, which is not square.
            if (lampImage.TryGetComponent<RectTransform>(out var lampRect))
            {
                lampRect.anchorMin = new Vector2(0.5f, 0.5f);
                lampRect.anchorMax = new Vector2(0.5f, 0.5f);
                lampRect.pivot     = new Vector2(0.5f, 0.5f);
                if (Mathf.Abs(lampRect.sizeDelta.x - lampRect.sizeDelta.y) > 0.5f ||
                    lampRect.sizeDelta.x <= 0f)
                {
                    lampRect.sizeDelta = new Vector2(LampPixelSize, LampPixelSize);
                    report.Add($"• squared the lamp's rect to {LampPixelSize}x{LampPixelSize} " +
                               "(a round sprite in a non-square box renders as an ellipse).");
                }
            }

            lampImage.preserveAspect = true;
            lampImage.type = Image.Type.Simple;

            var indSo = new SerializedObject(indicator);
            indSo.FindProperty("lamp").objectReferenceValue = lampImage;
            indSo.FindProperty("questionBar").objectReferenceValue = bar;
            if (onlineLamp  != null) indSo.FindProperty("onlineSprite").objectReferenceValue  = onlineLamp;
            if (offlineLamp != null) indSo.FindProperty("offlineSprite").objectReferenceValue = offlineLamp;
            indSo.ApplyModifiedProperties();

            // Seed the visible sprite so the lamp looks right in the editor without entering
            // play mode. Runtime Apply() overwrites this on the first frame anyway.
            if (onlineLamp != null) lampImage.sprite = onlineLamp;

            report.Add("• lamp sprites bound (filled = online, hollow = offline).");

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

        static Sprite EnsureIcon(string path, System.Func<Texture2D> draw, List<string> report,
                                 bool force = false)
        {
            if (!force)
            {
                var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (existing != null) return existing;
            }

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
                importer.mipmapEnabled = true;          // these scale DOWN in UI; mips kill shimmer
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            report.Add($"• generated '{Path.GetFileName(path)}' ({IconSize}px, {Supersample}x{Supersample} supersampled).");
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ── Status lamps ─────────────────────────────────────────────────────
        //
        // One tint colour is applied at runtime (lime online / grey offline), so DEPTH has to
        // come from alpha rather than from a second hue: a fully opaque ring around a softer
        // fill reads as a bordered lamp in whatever colour it is handed.
        //
        // Online and offline differ in SHAPE as well as colour - filled versus hollow - so the
        // state survives being seen small, in peripheral vision, or by a player who cannot
        // separate lime from grey.

        const float LampOuterRadius = 0.44f;   // to the outside of the border
        const float LampBorderWidth = 0.058f;
        const float LampCoreGap     = 0.052f;  // clear space between border and fill

        static Texture2D DrawOnlineLamp() => DrawLamp(coreAlpha: 0.60f, ringAlpha: 1.00f);
        static Texture2D DrawOfflineLamp() => DrawLamp(coreAlpha: 0.00f, ringAlpha: 0.85f);

        static Texture2D DrawLamp(float coreAlpha, float ringAlpha)
        {
            float ringOuter = LampOuterRadius;
            float ringInner = LampOuterRadius - LampBorderWidth;
            float coreRadius = ringInner - LampCoreGap;

            return Render((x, y) =>
            {
                float d = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f));

                // Ring: inside the outer edge AND outside the inner edge.
                if (d <= ringOuter && d >= ringInner) return ringAlpha;

                // Core fill.
                if (coreAlpha > 0f && d <= coreRadius) return coreAlpha;

                return 0f;
            });
        }

        // ── Answer glyphs ────────────────────────────────────────────────────

        static Texture2D DrawCheck()
        {
            // Two strokes of a check, as distance-to-segment so the edges resolve analytically.
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
            return Render((x, y) =>
            {
                var p = new Vector2(x, y);
                float d = float.MaxValue;
                foreach (var (a, b) in strokes)
                    d = Mathf.Min(d, DistanceToSegment(p, a, b));
                return d <= halfWidth ? 1f : 0f;
            });
        }

        // ── Renderer ─────────────────────────────────────────────────────────

        /// <summary>
        /// Renders a white sprite whose alpha is <paramref name="coverage"/>, averaged over a
        /// <see cref="Supersample"/>² grid inside each pixel. The shape function returns hard
        /// 0/1 values - all smoothing comes from supersampling, so an edge is antialiased
        /// correctly at any curvature instead of being approximated by a fixed-width feather.
        ///
        /// RGB is left at white everywhere, including fully transparent pixels: a sprite whose
        /// transparent pixels carry black RGB fringes dark when the UI filters it.
        /// </summary>
        static Texture2D Render(System.Func<float, float, float> coverage)
        {
            var tex = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
            var pixels = new Color32[IconSize * IconSize];

            float step = 1f / (IconSize * Supersample);
            float half = step * 0.5f;
            float weight = 1f / (Supersample * Supersample);

            for (int py = 0; py < IconSize; py++)
            for (int px = 0; px < IconSize; px++)
            {
                float sum = 0f;
                for (int sy = 0; sy < Supersample; sy++)
                for (int sx = 0; sx < Supersample; sx++)
                {
                    float x = (px * Supersample + sx) * step + half;
                    float y = (py * Supersample + sy) * step + half;
                    sum += coverage(x, y) * weight;
                }

                pixels[py * IconSize + px] = new Color32(255, 255, 255, (byte)Mathf.Round(Mathf.Clamp01(sum) * 255f));
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
