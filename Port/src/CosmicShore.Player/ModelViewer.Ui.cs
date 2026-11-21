using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Content;
using CosmicShore.Content.Models;
using CosmicShore.Engine;
using CosmicShore.Engine.InputSystem;
using CosmicShore.Engine.UI;

namespace CosmicShore.Player
{
    /// <summary>
    /// The model viewer's on-screen parts, drawn by the engine's own uGUI: what is shown (model,
    /// whose colours, size) and the controls (top left / bottom left, H hides the help), a slider per
    /// blend shape (top right: drag to set 0-100, B zeroes them) and the animation takes (bottom
    /// right: T next take, P pause, drag the bar to scrub). A take that drives blend shapes moves
    /// their sliders too.
    /// </summary>
    static partial class ModelViewer
    {
        const float Pad = 16f, RowH = 22f, NameW = 190f, TrackW = 160f, TimeW = 360f;
        static readonly Color PanelColor = new(0.06f, 0.06f, 0.08f, 0.78f);
        static readonly Color TrackColor = new(1f, 1f, 1f, 0.14f);
        static readonly Color FillColor = new(0.36f, 0.78f, 0.96f, 0.95f);

        sealed class ShapeRow
        {
            public SkinnedMeshRenderer Smr;
            public int Index;
            public RectTransform Fill;
            public TextMeshProUGUI Value;
            public float Top;   // the track's top, in pixels below the panel's top
        }

        static TMP_FontAsset s_font;
        static RectTransform s_canvas;
        static TextMeshProUGUI s_info, s_takeLabel;
        static GameObject s_help;
        static readonly List<ShapeRow> s_shapes = new();
        static int s_dragShape = -1;
        static bool s_dragTime;

        // Takes: the model's clips (Unity's sub-assets), sampled straight onto the hierarchy.
        static List<(string Name, long FileId)> s_takes = new();
        static int s_take = -1;
        static AnimationClip s_clip;
        static float s_time;
        static bool s_playing = true;
        static RectTransform s_timeFill;
        static GameObject s_timePanel;
        static GameObject s_root;
        static List<(Transform t, Vector3 p, Quaternion r, Vector3 s)> s_rest;

        /// <summary>Names of the takes (for the console line and the tests).</summary>
        public static IReadOnlyList<string> TakeNames => s_takes.Select(t => t.Name).ToList();
        /// <summary>The blend shapes with a slider: renderer, index, name.</summary>
        public static IEnumerable<(SkinnedMeshRenderer smr, int index, string name)> Shapes
            => s_shapes.Select(r => (r.Smr, r.Index, r.Smr.sharedMesh.GetBlendShapeName(r.Index)));

        static void BuildUi(GameObject model, string guid)
        {
            var content = ContentRuntime.Current;
            s_root = model;
            s_font = content.Fonts.DefaultFont;
            s_rest = model.GetComponentsInChildren<Transform>(true).Select(t => (t, t.localPosition, t.localRotation, t.localScale)).ToList();
            s_takes = s_model != null ? FbxAnimationImporter.ListClips(s_model) : new List<(string, long)>();

            var canvasGo = new GameObject("ModelViewerUi", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            s_canvas = (RectTransform)canvasGo.transform;

            // Top left: what is on screen.
            var info = Panel("Info", new Vector2(0, 1), new Vector2(Pad, -Pad), new Vector2(520, 74));
            s_info = Label(info, "Text", new Vector2(10, -8), new Vector2(500, 62), 15f);

            // Bottom left: the controls.
            var help = Panel("Help", new Vector2(0, 0), new Vector2(Pad, Pad), new Vector2(520, 58));
            Label(help, "Text", new Vector2(10, -8), new Vector2(500, 46), 13f).text =
                "Drag turn  ·  Wheel zoom  ·  Right-drag pan  ·  F frame  ·  R reset  ·  Space spin\n"
                + "Tab colours  ·  T take  ·  P pause  ·  B zero shapes  ·  H hide help";
            s_help = help.gameObject;

            // Top right: a slider per blend shape.
            var shapeList = s_renderers.Where(r => r.smr != null && r.smr.sharedMesh != null)
                .SelectMany(r => Enumerable.Range(0, r.smr.sharedMesh.blendShapeCount).Select(i => (r.smr, i))).ToList();
            if (shapeList.Count > 0)
            {
                float h = 36 + shapeList.Count * RowH;
                var panel = Panel("Shapes", new Vector2(1, 1), new Vector2(-Pad, -Pad), new Vector2(NameW + TrackW + 60, h));
                Label(panel, "Title", new Vector2(10, -6), new Vector2(300, 22), 14f).text = "<b>BLEND SHAPES</b>";
                for (int i = 0; i < shapeList.Count; i++)
                {
                    var (smr, idx) = shapeList[i];
                    float top = 32 + i * RowH;
                    string name = smr.sharedMesh.GetBlendShapeName(idx);
                    if (shapeList.Select(s => s.smr).Distinct().Count() > 1) name = $"{smr.name}: {name}";
                    Label(panel, "Name", new Vector2(10, -top), new Vector2(NameW - 10, RowH), 13f).text = Clip(name, 26);
                    var track = Box(panel, "Track", new Vector2(0, 1), new Vector2(NameW, -top - 6), new Vector2(TrackW, 8), TrackColor);
                    var fill = Box(track, "Fill", new Vector2(0, 1), Vector2.zero, new Vector2(0, 8), FillColor);
                    var value = Label(panel, "Value", new Vector2(NameW + TrackW + 8, -top), new Vector2(40, RowH), 13f);
                    s_shapes.Add(new ShapeRow { Smr = smr, Index = idx, Fill = fill, Value = value, Top = top + 6 });
                }
            }

            // Bottom right: the takes and their timeline.
            if (s_takes.Count > 0)
            {
                var panel = Panel("Takes", new Vector2(1, 0), new Vector2(-Pad, Pad), new Vector2(TimeW + 20, 58));
                s_takeLabel = Label(panel, "Text", new Vector2(10, -6), new Vector2(TimeW, 22), 13f);
                var track = Box(panel, "Track", new Vector2(0, 1), new Vector2(10, -38), new Vector2(TimeW, 8), TrackColor);
                s_timeFill = Box(track, "Fill", new Vector2(0, 1), Vector2.zero, new Vector2(0, 8), FillColor);
                s_timePanel = panel.gameObject;
            }
            RefreshInfo();
        }

        static RectTransform Panel(string name, Vector2 corner, Vector2 pos, Vector2 size)
        {
            var rt = Box(s_canvas, name, corner, pos, size, PanelColor);
            rt.pivot = corner;
            rt.anchoredPosition = pos;
            return rt;
        }

        /// <summary>A coloured rectangle hung from its parent's <paramref name="corner"/> (its own top-left at <paramref name="pos"/>).</summary>
        static RectTransform Box(Transform parent, string name, Vector2 corner, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = corner;
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        static TextMeshProUGUI Label(Transform parent, string name, Vector2 pos, Vector2 size, float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = go.AddComponent<TextMeshProUGUI>();
            if (s_font != null) t.font = s_font;
            t.fontSize = fontSize;
            t.color = new Color(0.92f, 0.93f, 0.96f, 1f);
            t.richText = true;
            t.raycastTarget = false;
            t.alignment = TextAlignmentOptions.TopLeft;
            return t;
        }

        static string Clip(string s, int n) => s.Length <= n ? s : s[..(n - 1)] + "…";

        static void RefreshInfo()
        {
            if (s_info == null) return;
            var content = ContentRuntime.Current;
            string prefab = s_sources.Count > 0 ? s_sources[s_source] : null;
            int tris = s_renderers.Sum(r => ((r.mesh?.Mesh ?? (r.mr != null ? r.mr.GetComponent<MeshFilter>()?.sharedMesh : r.smr.sharedMesh))?.triangles.Length ?? 0) / 3);
            string colours = s_model == null ? "the prefab's own materials"
                : prefab == null
                ? "the model's own materials"
                : "materials from " + Clip(System.IO.Path.GetFileNameWithoutExtension(prefab), 40) + ".prefab";
            string pick = s_sources.Count > 1 ? $"  <color=#8899AA>({s_source + 1}/{s_sources.Count}, Tab)</color>" : "";
            s_info.text = $"<b>{System.IO.Path.GetFileName(Path)}</b>\n"
                          + $"Colours: {colours}{pick}\n"
                          + $"<color=#8899AA>{s_renderers.Count} renderers · {tris:N0} triangles · {s_shapes.Count} blend shapes · {s_takes.Count} takes"
                          + (s_particles > 0 ? $" · {s_particles} particle systems" : "") + "</color>";
        }

        static void SetTake(int take)
        {
            // Back to the rest pose first, so one take's leftovers never show in the next.
            foreach (var (t, p, r, sc) in s_rest) { if (t == null) continue; t.localPosition = p; t.localRotation = r; t.localScale = sc; }
            foreach (var row in s_shapes) row.Smr.SetBlendShapeWeight(row.Index, 0f);
            s_take = take;
            s_time = 0f;
            s_clip = null;
            if (take >= 0 && take < s_takes.Count)
            {
                var guid = s_model.Guid;
                s_clip = ContentRuntime.Current.Assets.Load<AnimationClip>(new ObjRef(s_takes[take].FileId, guid, 3));
                if (s_clip == null) Console.WriteLine($"[viewer] take {s_takes[take].Name} did not import");
                else Console.WriteLine($"[viewer] take {s_takes[take].Name}: {s_clip.length:0.00} s, {s_clip.Bindings.Count} curves");
            }
            s_playing = true;
        }

        /// <summary>Mouse on the panels; true when the viewer's camera should leave the drag alone.</summary>
        static bool TickUi(float dt, Mouse mouse, Keyboard keys)
        {
            if (keys != null)
            {
                if (keys.hKey.wasPressedThisFrame && s_help != null) s_help.SetActive(!s_help.activeSelf);
                if (keys.bKey.wasPressedThisFrame) foreach (var row in s_shapes) row.Smr.SetBlendShapeWeight(row.Index, 0f);
                if (keys.tKey.wasPressedThisFrame && s_takes.Count > 0) SetTake(s_take + 1 >= s_takes.Count ? -1 : s_take + 1);
                if (keys.pKey.wasPressedThisFrame) s_playing = !s_playing;
            }

            bool busy = false;
            if (mouse != null)
            {
                var pos = mouse.position.ReadValue();
                float x = pos.x, yTop = Screen.height - pos.y;
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    s_dragShape = -1; s_dragTime = false;
                    // The shapes panel hangs from the top-right corner.
                    float left = Screen.width - Pad - (NameW + TrackW + 60) + NameW;
                    for (int i = 0; i < s_shapes.Count; i++)
                    {
                        float top = Pad + s_shapes[i].Top;
                        if (x >= left - 4 && x <= left + TrackW + 4 && yTop >= top - 7 && yTop <= top + 15) { s_dragShape = i; break; }
                    }
                    if (s_timePanel != null)
                    {
                        float tl = Screen.width - Pad - (TimeW + 20) + 10, tt = Screen.height - Pad - 58 + 38;
                        if (x >= tl - 4 && x <= tl + TimeW + 4 && yTop >= tt - 8 && yTop <= tt + 16) s_dragTime = true;
                    }
                }
                if (!mouse.leftButton.isPressed) { s_dragShape = -1; s_dragTime = false; }
                if (s_dragShape >= 0)
                {
                    float left = Screen.width - Pad - (NameW + TrackW + 60) + NameW;
                    var row = s_shapes[s_dragShape];
                    row.Smr.SetBlendShapeWeight(row.Index, Math.Clamp((x - left) / TrackW, 0f, 1f) * 100f);
                    busy = true;
                }
                if (s_dragTime && s_clip != null)
                {
                    float tl = Screen.width - Pad - (TimeW + 20) + 10;
                    s_time = Math.Clamp((x - tl) / TimeW, 0f, 1f) * Math.Max(1e-3f, s_clip.length);
                    s_playing = false;
                    busy = true;
                }
            }

            if (s_clip != null)
            {
                if (s_playing) s_time += dt;
                float len = Math.Max(1e-3f, s_clip.length);
                if (s_time > len) s_time %= len;
                s_clip.SampleAnimation(s_root, s_time);
            }

            foreach (var row in s_shapes)
            {
                float w = row.Smr.GetBlendShapeWeight(row.Index);
                row.Fill.sizeDelta = new Vector2(TrackW * Math.Clamp(w / 100f, 0f, 1f), 8);
                row.Value.text = $"{w:0}";
            }
            if (s_takeLabel != null)
            {
                s_takeLabel.text = s_clip == null
                    ? $"<b>TAKES</b>  {s_takes.Count}  <color=#8899AA>T to play one</color>"
                    : $"<b>{s_take + 1}/{s_takes.Count}</b>  {Clip(s_takes[s_take].Name, 30)}  {s_time:0.00} / {s_clip.length:0.00} s{(s_playing ? "" : "  <color=#8899AA>paused</color>")}";
                s_timeFill.sizeDelta = new Vector2(s_clip == null ? 0 : TimeW * Math.Clamp(s_time / Math.Max(1e-3f, s_clip.length), 0f, 1f), 8);
            }
            return busy;
        }
    }
}
